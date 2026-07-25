# 项目最高警告：资源加载底层，Library 只属 Editor，Runtime 只认 Manifest/Table

最后核对：2026-07-25

职责：这是 ESFramework 给后续 AI 的项目最高警告。当前正在搭建资源加载底层，任何资源系统、GameManager 启动流程、AssetRegistry、AssetLibrary、RuntimeKey、Manifest/Table 相关改动都必须先读本文件。

## 核心边界

### 当前发布文件索引权威（2026-07-25 最高约束）

当前正式发布链的物理文件定位只认 `ESAssetReleaseBundleIndex.json`。

完整寻址分层为：

```text
EnumKey / StringKey
  -> 当前类型 Catalog / AssetTable
  -> GUID 或 GUID + LocalFileId
  -> Library AssetBundleManifest 中的 AssetBundleKey + InternalName
  -> ESAssetReleaseBundleIndex 中的文件位置、Hash、CRC、Size、Dependencies
```

`ESAssetReleaseBundleIndex` 是运行时 `AssetBundleKey -> 物理文件` 的唯一权威，记录：

- `libraryFolder`；
- `fileUrl`；
- `localRelativePath`；
- `sha256`；
- `crc`；
- `size`；
- `dependencies`。

运行时禁止：

- 根据 Library 名称自行猜测或拼接 AB 文件名；
- 根据 `AssetBundleKey` 自行生成 CDN URL；
- 绕过全局 Bundle 索引直接相信 Library Manifest 中的物理路径；
- 索引冲突、GUID 冲突或依赖缺失时记录日志后继续运行。

Library Manifest 只负责 `GUID -> AssetBundleKey + InternalName`，其 Bundle 文件字段只用于和全局索引交叉校验。下载、缓存、重试及 RuntimeMap 构建必须以全局 Bundle 索引为准。

本地已验证文件索引必须携带 `releaseVersion`。发布版本不匹配时不得直接信任旧验证结果，必须重新校验文件 Hash。

`ESAssetLibrary` 只属于编辑器。

它的职责是：

- 给开发者拖资产。
- 按 Book/Page 分类。
- 自动收集指定文件夹。
- 校验重复 key。
- 生成/维护编辑器侧配置。
- 烘焙导出运行时 Manifest/Table。
- 服务 ResWindow/AssetWindow 等编辑器工具。

它不是运行时资产数据库本体。不要让正式运行时依赖 `ESAssetLibrary`。

运行时只认：

- `ESAssetManifest`
- `ESAssetTable`
- `ESAssetRecord`
- `runtimeKey`
- 当前 `ESAssetRunMode`
- 当前 Loader

正确链路：

```text
Editor:
ESAssetLibrary / Book / Page
  -> 收集资产、分类、校验、生成 key
  -> 烘焙导出 Runtime Manifest / AssetTable

Runtime:
ESAssetManifest / ESAssetTable
  -> runtimeKey 查表
  -> Loader 按当前 ESAssetRunMode 加载
```

## 四种加载模式

`EditorDirect`

- 中文名：编辑器直连模式。
- 机制：`editorGuid -> AssetDatabase.GUIDToAssetPath -> LoadAssetAtPath`。
- 用途：开发期最快跑起来，不要求构建资源包。
- 限制：只在 Editor 可用，真机构建不能走。

`EditorSimulateBuild`

- 中文名：编辑器模拟发布模式。
- 机制：`(AssetKind, runtimeKey) -> Typed Manifest/Table -> 模拟构建后地址规则`。
- 用途：在 Editor 中提前验证发布版 key、路径、依赖、包规则。
- 限制：仍不是真机加载，但可以暴露大部分发布链路错误。

`LocalBuild`

- 中文名：本地构建资源模式。
- 机制：`(AssetKind, runtimeKey) -> Typed Manifest/Table -> StreamingAssets/本地包`。
- 用途：无热更或首包内资源加载。
- 限制：只信任本地构建产物。

`HotUpdate`

- 中文名：热更新资源模式。
- 机制：`runtimeKey -> RemoteManifest/LocalCache -> 校验/下载/加载`。
- 用途：正式商业发布。优先使用本地缓存，缺失或版本不符再下载远端资源。
- 限制：启动阶段必须先完成清单校验。

## GameManager 启动准备

GameManager 启动资源系统时，不应该持有 Library 作为运行时入口。

错误方向：

```csharp
ESGameManager.AssetModule.ActiveLibrary = someLibrary;
```

正确方向：

```csharp
ESGameManager.AssetModule.Initialize(runMode, manifest);
```

启动链路建议：

```text
BootScene
  -> 创建 ESGameManager
  -> 读取 ESGlobalResSetting.AssetRunMode
  -> 加载 Runtime Manifest
  -> 构建 ESAssetTable
  -> 创建对应 ESAssetLoader
  -> 资源系统 Ready
  -> RuntimeData / Input / Save 初始化
  -> SceneModule 加载 MainMenu 或首个游戏场景
```

业务层最终只应该这样用：

```csharp
var prefab = ESGameManager.AssetModule.Load<GameObject>(key);
var handle = ESGameManager.AssetModule.LoadAsync<AudioClip>(key);
ESGameManager.AssetModule.Release(handle);
```

业务层不应该关心：

- GUID
- AssetDatabase
- BundleName
- StreamingAssets
- 远端 URL
- 本地缓存路径

这些都由 `RunMode + Manifest + Loader` 解决。

## RuntimeKey 稳定性

RuntimeKey 是运行时查表核心，不允许在同一资产重复注入时抖动。

禁止出现：

```text
同一个 guid:
runtime=30001
下一次注入变成 runtime=30007
```

`ESAssetRegistry` 生成 RuntimeKey 时必须遵守：

- 如果 `enumKey != 0`，优先使用 enumKey。
- 如果同 GUID 已有快照，并且 stringKey 没变，复用旧 runtimeKey。
- 如果 page 自身已有有效 runtimeKey，复用它。
- 如果当前同 AssetKind 的表已有同 stringKey，复用该类型表内 runtimeKey。
- 最后才从 `DefaultStringRuntimeKeyStart` 开始递增分配。

看到 `RuntimeKey changed by asset self override` 时，优先怀疑 RuntimeKey 烘焙策略不稳定，而不是把 warning 当成普通噪音。

### RuntimeKey 的持久化与边界（最高约束）

`RuntimeKey` 可以持久化，但只属于某一份 `AssetTable/Manifest`，不属于资产的跨进程、跨版本权威身份。

允许写入：

- `ESAssetPage`；
- 构建产物中的 `AssetTable/Manifest`；
- 与该表版本绑定的本地构建缓存。

禁止写入或传递：

- 任意 `SoDataInfo`、`SoDataGroup`、玩法配置或资源计划；
- `ESAssetConfigKey` 等 ConfigKey 定义；
- 玩家存档；
- 网络协议和服务端数据；
- 跨进程通信数据；
- 跨版本业务配置；
- 任何脱离 `TableVersion/ManifestVersion` 的外部数据。

跨进程、跨版本或热更新数据必须使用 `(AssetKind, StringKey)`、`(EnumType, EnumKey)` 或 GUID 对应的稳定业务身份；新进程加载当前强类型 `AssetTable/Manifest` 后，再解析为该类型表内的 `RuntimeKey`。

因此：

- RuntimeKey 变化不代表资产身份变化；
- RuntimeKey 只能在匹配的 AssetTable/Manifest 内查询；
- 任何缓存的 RuntimeKey 都必须携带表版本，版本不一致时必须丢弃并重新解析；
- 不得把 RuntimeKey 当作存档、网络或跨版本兼容键；
- 不得由 Inspector、拖拽 Solver 或配置复制按钮写入 RuntimeKey；
- 运行时业务层不得依赖编辑器侧 `ESAssetLibrary` 直接恢复 RuntimeKey。

## AssetRegistry 现状

当前编辑器侧 `ESAssetRegistry` 是 Editor 配置镜像和查询表，不等价于最终 Runtime AssetTable。

它可以做：

- 编辑器注入。
- key 冲突提示。
- GUID 权威去重。
- 编辑器配置查询。
- 为后续 Manifest/Table 烘焙提供中间状态。

它不应该做：

- 真机资源加载。
- 热更新下载。
- 运行时直接依赖 Library。
- 每次刷新随机生成不稳定 RuntimeKey。

## 场景管理与资源系统边界

SceneModule 不应该直接知道 AssetBundle、Addressables、EditorDirect 细节。

合理调用：

```csharp
ESGameManager.Asset.LoadSceneAsync(sceneKey);
ESGameManager.Asset.Preload(groupKey);
ESGameManager.Asset.ReleaseScope(sceneScope);
```

资源 Scope 建议：

- `Global`
- `GameSession`
- `Scene`
- `UI`
- `Temporary`

场景结束时按 Scope 释放，而不是到处散落释放。

## 旧输入模块结论

旧类型：

- `EntityAIInputSystemModule`
- `EntityInputStateModule`

不再补兼容壳。不要为了旧场景/Prefab 的 Missing Type 恢复旧类型。

正确处理：

- 清理场景/Prefab 上的 SerializeReference 坏引用。
- 使用当前链路：`EntityAIDomain.inputState + EntityPlayerInputWriteModule + EntityAIInputDispatchModule`。
- 如果 Unity 报 `serialized array of [SerializeReference] objects is missing entry`，通过打开场景、移除坏引用、重新保存解决，不通过恢复旧代码解决。

## 管理员权限警告

Unity 管理员权限警告不是项目代码问题。

但开发时不建议用管理员权限启动 Unity，因为 Unity 会执行项目脚本和第三方二进制，管理员权限会放大误操作风险。

## 给后续 AI 的结论

资源系统当前最高原则：

```text
Library 是 Editor 编排源。
Manifest/Table 是 Runtime 查询源。
ESAssetReleaseBundleIndex 是 AssetBundle 物理文件定位权威。
RunMode 决定 Loader 行为。
RuntimeKey 必须稳定。
旧垃圾不恢复，坏引用清序列化。
```

任何试图让运行时直接依赖 `ESAssetLibrary`、恢复旧输入兼容壳、把 RuntimeKey 抖动 warning 当成无所谓、或在业务层手写 AssetDatabase/Bundle 路径的方案，都应停止并重审。
