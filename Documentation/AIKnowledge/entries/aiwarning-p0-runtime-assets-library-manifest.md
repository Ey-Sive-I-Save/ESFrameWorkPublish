# Runtime 资源、Library/Manifest、Scope 与发布边界

`KnowledgeId`: `es.aiwarning.p0.runtime-assets-library-manifest.v1`  
`Authority`: `AIWarnings + current resource pipeline source`  
`RouteKeys`: `aiwarnings`, `p0`, `runtime-assets`, `library`, `manifest`, `asset-table`, `runtime-key`, `scope`, `release`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `35754ec45bbc72118a0267dc4f58444da2e863af214c0d88f428818649a5a32f`  
`SourceSetHash`: `35754ec45bbc72118a0267dc4f58444da2e863af214c0d88f428818649a5a32f`  
`EntryBodyHash`: `e3084309ef8719ffca36f20ff7a4669d26c31f0059ff932800667cfbc43944a1`  
`StaleWhen`: `资源索引、Manifest/Table、Loader、Scope、Provider 或发布合同变化。`

## 迁移说明

原 Warning 584 行、39,452 UTF-8 字节；现 Warning 保留资源所有权、RuntimeKey、发布索引和证据边界。详细加载模式、Scope 生命周期和迁移说明进入本条目，原文及源码由 SourceRefs 回溯。

## Editor 与 Runtime

- `ESAssetLibrary` 仅用于 Editor 收集/分类/校验/烘焙；Runtime 只依赖 `ESAssetManifest`、`ESAssetTable`、`ESAssetRecord`、类型化 RuntimeKey、RunMode 和 Loader。业务代码不应接触 GUID、AssetDatabase、BundleName、StreamingAssets、远端 URL 或缓存路径。
- EditorDirect 仅供开发直连；EditorSimulateBuild 只验证发布身份和 RuntimeMap，不验证 AssetBundle、下载、缓存、解压、Player 或设备。LocalBuild/HotUpdate 必须以正式 Manifest/ReleaseBundleIndex、releaseVersion、Hash、CRC、Size 和 Dependencies 为准。
- 物理寻址为 `Enum/String Key → 当前类型 Catalog/Table → GUID/BundleKey → ESAssetReleaseBundleIndex`；禁止猜文件名/URL、绕过全局索引、冲突后继续或把 Library Manifest 物理字段当唯一发布权威。

## Scope 与 Lease

- Resident 仅承载启动/全局基础资源；GameSession、ApplicationSession、Scene/UI/Feature 等 Domain 只代表明确生命周期；Owner Scope 绑定具体 Unity Owner；ResourcePlan 保持私有 Scope；Temporary Lease 是独立短租。它们不能互相伪装或扩大常驻范围。
- ReferenceCount 与 LeaseCount 独立；普通 Scope 同一 identity 至多一次，Temporary 按调用/Token 独立归还。Owner/流程管理器是唯一释放者，父级关闭时子级先关。`ReleaseScope` 表示逻辑关闭和 Dispose，不保证 Provider 请求物理静默。
- Provider 切换/安全点推进 Lease generation 并阻止新请求；旧 Scope、旧 Provider 的迟到 Handle/结果不得写回新状态。旧 Token 失效不能影响下一代。无命中 `TryGetOwned` 不得创建 Scope/Tracker/加载。
- `GameInternal`、枚举 Domain 与业务 StringKey 必须按保留语义使用；并行实例使用稳定前缀 StringKey，不扩充大量枚举。普通业务不得主动释放框架内部域。

## 发布证据

需验证索引冲突/依赖缺失硬失败、Manifest/Table 构建、Provider/Cache/Download/Retry、Scope/Lease 回收、LocalBuild 与 HotUpdate、目标 Player/设备和 releaseVersion 重校验；当前仅静态，未运行 Unity/下载/Player/IL2CPP/发布。

## EvidenceRefs

- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESAssetScope.cs`
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeReleaseDownloader.cs`
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeAssetProviderFactory.cs`
- `Assets/Plugins/ES/Editor/ESResPipeline/ESAssetPipelineContracts.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md` (`6ee72697e24d9dc57a3e6bc8c644f72e9b26b979d4a32ef47bbc7c49a895615d`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESAssetScope.cs` (`d1551ea4cbc8bccefd7f24038548fa2d650b70ffe6815600f7614b9a543d5ade`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeReleaseDownloader.cs` (`50ea89012643e14501c07f2ca6964b2eb46175d885fb10ccaf22fe998552a117`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeAssetProviderFactory.cs` (`c63353575fa5f1824cdd4399965efbe47b3b47d0d298f89bf1f9f659f8212ee4`)
- `Assets/Plugins/ES/Editor/ESResPipeline/ESAssetPipelineContracts.cs` (`3d3a5d44965ddf0aa666d97fd8d9a802a4ab8041a0465a6c603947d404cd1013`)
