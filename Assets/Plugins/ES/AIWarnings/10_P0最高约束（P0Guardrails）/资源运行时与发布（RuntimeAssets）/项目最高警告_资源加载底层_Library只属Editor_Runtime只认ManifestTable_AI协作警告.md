# 项目最高警告：资源加载底层，Library 只属 Editor，Runtime 只认 Manifest/Table

Status: current
StableId: es.aiwarnings.p0.runtime-assets-library-manifest.v1
Authority: ESFramework AIWarnings
RouteKeys: aiwarnings, p0, runtime-assets, library, manifest, asset-table, runtime-key, scope, release
Applicability: AssetLibrary、Manifest/Table、Loader、RuntimeKey、Scope 与资源发布链
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-p0-runtime-assets-library-manifest.md`
StaleWhen: 资源索引、Manifest/Table、Loader、Scope、Provider 或发布合同变化
Knowledge: `es.aiwarning.p0.runtime-assets-library-manifest.v1`

## P0 长期约束

- `ESAssetLibrary` 只属于 Editor：收集、分类、Key 校验、配置维护和烘焙；Runtime 只认 `ESAssetManifest`、`ESAssetTable`、`ESAssetRecord`、已解析 RuntimeKey、当前 RunMode 和 Loader。
- 运行时寻址固定为类型化 Key → Catalog/Table → GUID/BundleKey → `ESAssetReleaseBundleIndex` 物理文件、Hash、CRC、Size、Dependencies；禁止猜 AB 文件名/CDN URL、绕过全局索引或冲突后继续运行。
- EditorDirect/EditorSimulateBuild 不能证明真实包、下载、缓存、设备或 Player 行为；LocalBuild/HotUpdate 必须先校验 releaseVersion、Hash、Manifest/依赖后再加载。
- Resident、Domain/Owner、ResourcePlan 私有 Scope、Temporary Lease 的所有权和释放语义必须分离；ReferenceCount 与 LeaseCount 独立，Provider 切换推进 generation，迟到结果不得写回新状态。
- 默认 `LoadAsync` 进入明确 GameSession/Registry Scope；Owner 入口由 Owner 销毁释放；Temporary Lease 逐次归还。ReleaseScope 完成逻辑关闭不等于物理请求已静默。
- 业务不得感知 GUID、AssetDatabase、BundleName、StreamingAssets、URL 或缓存路径；不得把 `GameInternal` 当万能全局域，不得让 ResourcePlan 注入/窃取 Domain Scope。
- 索引、GUID、依赖、Key 或类型无效必须硬失败；不把日志、静态编译、Editor 模拟或资产存在写成 Runtime/Player/发布通过。

## 证据边界

详细四种加载模式、Scope 表、Provider 切换、发布链和历史迁移说明已纳入 Knowledge；执行前必须回读源码。Unity/Player/下载/设备/IL2CPP/发布行为未由静态结果证明。
