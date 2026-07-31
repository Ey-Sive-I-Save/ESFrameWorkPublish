# Asset Library 分发方式提案

## 结论

为每个 `ESAssetLibrary` 增加两个独立概念：

- `BuildEnabled`：是否参与资源构建，默认 `true`。
- `DeployMode`：构建产物如何交付给玩家。

`DeployMode` 使用三个短名称，并在 Inspector 显示为中文：

| 代码值 | Inspector 显示 | 初始包 | CDN 发布 | 运行时更新 |
| --- | --- | --- | --- | --- |
| `BuiltIn` | 随包 | 必须 | 不发布 | 不检查远端 |
| `Updateable` | 随包可更新 | 必须 | 必须 | 可校验后增量更新 |
| `Online` | 远端下载 | 不放入初始包 | 必须 | 首次使用前下载 |

这不是三份不同的 Library。一个 Library 始终只有一份 Catalog、一个版本和一套 Bundle 身份；差异只在于发布目标和运行时允许的来源。

## 当前现状与问题

`ESAssetLibrary` 已存在：

```csharp
public bool ContainsBuild = true;
public bool IsNet = true;
```

其中：

- `ContainsBuild` 已被引用烘焙与 AB 规划使用。
- `IsNet` 的 Inspector 含义是“允许热更新远端发布 / 仅随包本地”，但当前不参与构建规划、发布清单或运行时来源决策。

因此当前系统有“意图字段”，但还没有可执行的本地/远端 Library 分发规则。

## 第五步：阿里云 OSS 远端发布

资源四步构建完成后，由独立的“5. 发布到远端”窗口消费上传计划。阿里云 OSS Provider 使用原生签名和流式 `UploadHandlerFile`，不把凭据写入 Unity 资产。

环境变量约定：

```text
ES_OSS_ACCESS_KEY_ID
ES_OSS_ACCESS_KEY_SECRET
ES_OSS_SECURITY_TOKEN       # 使用 STS 时填写
```

配置了 `credentialProfile` 后，变量名追加大写配置名，例如 `credentialProfile=Production` 时使用 `ES_OSS_PRODUCTION_ACCESS_KEY_ID`。首次接入必须先在独立 `validationPrefix`（默认 `.es-validation`）执行探针 PUT、HEAD 和 DELETE；验证通过后才允许正式上传。正式文件按版本路径使用 immutable 缓存，Root Manifest 在所有叶子文件校验通过后最后上传并使用 `no-cache`。

## 配置模型

建议替换为：

```csharp
public bool BuildEnabled = true;
public ESAssetLibraryDeployMode DeployMode = ESAssetLibraryDeployMode.Updateable;

public enum ESAssetLibraryDeployMode
{
    BuiltIn,    // 随包
    Updateable, // 随包可更新
    Online      // 远端下载
}
```

`BuildEnabled = false` 的 Library 不产生 Catalog、Bundle、Identity 或发布条目。它可用于编辑器样例、开发中的库，不能被正式 Consumer 声明为运行时必需资源。

## 构建与发布规则

构建阶段对所有 `BuildEnabled` Library 生成同样的 staging 产物：

```text
BuildStaging/<Platform>/<LibraryId>/
  ESAssetLibraryCatalog.json
  ESAssetBundleManifest.json
  ESAssetLibraryIdentity.json
  Bundles/...
```

发布阶段才按 `DeployMode` 分流：

| 分发方式 | 初始包输出 | CDN 输出 | Release Manifest |
| --- | --- | --- | --- |
| 随包 | 写入 StreamingAssets | 无 | 不写远端下载地址 |
| 随包可更新 | 写入 StreamingAssets | 上传完整版本 | 写入地址、Hash、Size、版本 |
| 远端下载 | 不写 Bundle 与完整清单 | 上传完整版本 | 写入地址、Hash、Size、版本 |

根发布清单只发布远端可消费的 `Updateable`、`Online` 条目。`BuiltIn` 的本地 Identity 仍必须保留在初始包，用于本地校验与业务寻址。

## 运行时来源规则

资源模式与 Library 分发方式共同决定加载来源：

| 当前资源模式 | 随包 | 随包可更新 | 远端下载 |
| --- | --- | --- | --- |
| 本地模式 | 初始包 | 初始包 | 不可用，明确报“该库要求远端模式” |
| 远端模式 | 初始包 | 缓存/初始包校验后按 Release 更新 | 缓存校验后下载 |

固定来源优先级：

```text
已校验本地缓存 → 已校验初始包 → 已校验远端下载
```

但 `BuiltIn` 不进入远端检查；`Online` 不允许把初始包当作兜底来源。所有复用必须同时匹配 `SHA-256 + Size`，不能只按版本号或文件名判断。

## 启动与 Consumer 约束

- Bootstrap、维护公告、本地兜底 UI、最小启动场景所需的 Library 必须为 `BuiltIn` 或 `Updateable`，不能为 `Online`。
- `Online` Library 允许被普通关卡、活动、皮肤、高清包等 Consumer 使用；该 Consumer 进入前由 `ESResManager` 下载其依赖闭包。
- 若启动 Consumer 依赖 `Online` Library，Bootstrap Manifest 必须把它列为启动前必需下载项，并提供维护、失败重试和空间不足提示。
- 代码包与资源包必须由同一 Release/Consumer 版本关联，禁止代码版本单独指向不匹配的 Library Manifest。

## 迁移方案

第一阶段只做兼容迁移，不改变现有产物路径：

| 旧字段 | 新字段 |
| --- | --- |
| `ContainsBuild` | `BuildEnabled` |
| `IsNet == false` | `DeployMode = BuiltIn` |
| `IsNet == true` | `DeployMode = Updateable` |

`Online` 不从旧数据自动推断，必须由项目人员显式选择并通过发布检查。

保留旧字段一个小版本作为序列化兼容入口；加载旧资产时迁移并标记脏。迁移完成后删除 `IsNet`，避免两个字段表达同一策略。

## 发布前校验

发布器应阻止下列错误：

1. `BuildEnabled == false` 的 Library 被 Consumer 声明为必需。
2. `BuiltIn` Library 出现在远端发布条目中。
3. `Online` Library 的 Bundle 被复制进初始包。
4. 本地模式的启动链依赖 `Online` Library。
5. `Updateable`/`Online` 缺少 URL、Hash、Size、版本或依赖 Bundle。
6. 同一 Consumer 的代码版本与目标 Library Release 不兼容。

## 验收场景

1. `BuiltIn`：断网启动、校验初始包、进入游戏；不请求 CDN。
2. `Updateable`：初始包版本存在，CDN 有新版本；只下载 Hash 不同的文件。
3. `Online`：初始包不含其 Bundle；首次进入对应内容时下载，二次进入复用缓存。
4. 本地资源模式请求 `Online`：显示明确不可用原因，不做隐式网络请求。
5. CDN 下载中断：重启后按已校验文件复用，未完成文件重新校验或续传。
6. 发布中途失败：根 Release Manifest 未原子发布前，旧版本仍可启动。

## 不在本提案内

- AB 如何按依赖分包；仍由 Build Planner 决定。
- Asset Key、GUID、RuntimeKey 的寻址规则。
- CDN/OSS 的具体上传实现与凭据管理。
- ESResManager 的 UI、公告、强更、维护策略细节。

这些能力应消费本提案产出的分发语义，而不是反向决定 Library 的身份。
