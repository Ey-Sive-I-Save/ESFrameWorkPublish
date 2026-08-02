# 项目最高警告：资源工具链四阶段严格隔离

> 级别：P0。阶段职责、产物权威和调用顺序不得合并；编辑器菜单必须遵守 `【ES】/` 统一根。

资源构建必须保持四个本地构建阶段与一个独立远端发布阶段，禁止重新合并为一个“构建资源”入口：

1. `ESAssetReferenceBaker`：只分析并输出 `ESAssetLibraryCatalog.json`、`ESAssetReferenceGraph.json`。禁止改 AB 标签、调用 `BuildPipeline`、复制发布文件。
2. `ESAssetBundleBuildPlanner`：读取烘焙结果，输出 `ESAssetBundleBuildPlan.json`、`ESAssetBundleAssetList.json`。这是唯一允许修改 ES 管理范围内 `AssetImporter.assetBundleName` 的阶段，禁止真正构建。
3. `ESAssetBundleBuilder`：校验标签与计划一致，唯一允许调用 `BuildPipeline.BuildAssetBundles`，只写 `BuildStaging`。
4. `ESAssetBundlePublisher`：只校验已有 staging 产物并输出本地 Release/上传计划，禁止分析、改标签、重新构建或执行远端网络；`ESAssetReleaseManifest.json` 必须最后原子写入。
5. 远端发布：只读取第四步确认的上传计划并执行发布前预检、上传和验证；禁止回头分析、规划、改标签或构建。

身份权威固定为：主资源 `GUID + LocalFileId(0)`；独立子资源 `GUID + LocalFileId`。`EnumKey/StringKey` 只在资产类型内作为业务寻址权威，不能进入全局物理加载映射。

## 唯一用户入口

开发者统一从以下窗口进入：

```text
【ES】/资源与发布/资源管理/资源管理窗口
```

窗口内部按顺序提供四个明确动作：

```text
1. 烘焙资产引用
2. 规划并标记 AB
3. 构建资源包
4. 发布资源包
5. 发布到远端
```

五个阶段是独立操作和独立产物，不要求重新创建五个顶层菜单。自动化或测试可以直接调用对应强类型阶段 API，但不得绕过前置产物验证。

## P0 阶段边界

- `ESAssetReferenceBaker` 不能修改任何 AB 标签，也不能调用 Builder/Publisher。
- `ESAssetBundleBuildPlanner` 是唯一允许修改 ES 管理范围内 AB 标签的阶段；必须只消费已烘焙输入。
- `ESAssetBundleBuilder` 是唯一允许调用 `BuildPipeline.BuildAssetBundles` 的阶段；必须只写 staging。
- `ESAssetBundlePublisher` 不能重新分析、规划、标记、构建或执行远端上传；本地 Release Manifest 必须最后提交。
- 远端发布不能修改本地构建产物；版本文件上传和校验成功后才能最后切换 Root Manifest。
- 任一阶段失败不得伪造下阶段产物，也不得用旧产物冒充本次成功。
- 资产身份、业务 Key、BundleKey 和发布文件身份必须保持各自边界，禁止互相代替。

## 远端发布与 Provider 状态

当前 Provider 状态必须如实表达：

| Provider | 当前状态 |
| --- | --- |
| `ManualPlan` | 可用；只生成/检查计划，不执行网络。 |
| Aliyun OSS | 源码已实现；尚缺真实 Bucket 网络验收。 |
| S3Compatible | 未实现。 |
| HttpPut | 未实现。 |
| ExternalCommand | 未实现。 |

不得把接口、菜单项或未来 Provider 写成已经可用。生成 `.csproj` 的自动修复也仍是延期项：不得手工改 Unity 生成工程，必须等待 Unity 刷新项目文件。

远端缓存与校验规则：

- 带版本的 Bundle/索引文件可使用长期 immutable 缓存。
- Root Manifest 必须使用 `no-cache`，确保启动读取当前版本入口。
- 上传后用 HEAD 校验内容长度和自定义 SHA-256 元数据 `x-oss-meta-es-sha256`。
- ETag 不是可靠的文件 Hash，不得把它作为内容完整性结论。

## 禁止恢复的旧菜单

以下入口已经废弃，任何源码、有效警告或 AI Command 都不得把它们写成正式入口：

```text
ES/Resource/1. Bake Asset References
ES/Resource/2. Plan And Mark AssetBundles
ES/Resource/3. Build AssetBundles
ES/Resource/4. Publish AssetBundles
Window/ES/...
Tools/ES/...
```

磁盘路径中的 `Assets/Plugins/ES/` 不属于菜单，不得机械替换。

## 修改后验收

1. 扫描 `MenuItem`、`CreateAssetMenu`、`AddComponentMenu`，确认没有新增旧菜单根。
2. 从资源管理窗口分别执行五个阶段，确认每一步只产生自己的产物和副作用。
3. 故意移除或损坏上一阶段产物，下一阶段必须明确拒绝执行。
4. Builder 前验证当前 AB 标签与 BuildPlan 一致。
5. Publisher 前验证 staging、Bundle 索引和依赖闭包；远端发布前再验证上传计划，Root Manifest 最后切换。
6. 检查 UTF-8、乱码与 `git diff --check`。
