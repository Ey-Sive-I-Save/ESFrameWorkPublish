# 资源烘焙、Provider、Scope 与 ResourcePlan 完整机制

`KnowledgeId`: `es.project.resource-pipeline-runtime.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `resource`, `asset`, `library`, `manifest`, `provider`, `scope`, `resource-plan`, `bake`, `release`  
`ContentHash`: `d03d1b310247e4ab951cd6d78a375626406e0de9c6cd3a823d44af0320f27e5f`

## 权威链

资源系统不是运行时直接读取 Editor Library，而是严格分层：

```text
Editor AssetLibrary / GameCore authoring
  -> Content Registration
  -> ResourcePlan dependency expansion and bake
  -> Release Manifest + Bundle Index + Runtime Map
  -> ProviderFactory locks RunMode
  -> Runtime Loader
  -> Asset Handle / AssetScope
  -> ResourcePlan retain and release lifecycle
```

Library、AssetDatabase 路径与编辑器选择器只属于作者阶段。Player 和非 Editor 运行路径依赖烘焙出的 Runtime Map、Manifest 与 Bundle Index；不能在缺失产物时扫描 Library 或回退到 AssetDatabase。

## RunMode 与 Provider

`ESAssetRunModeSession.Lock` 在一个 Editor/Player 会话中锁定运行模式，避免系统运行到一半切换 Provider：

- `EditorDirect`：Editor 下直接资产 Provider。
- `EditorSimulateBuild`：Editor Provider，但要求 Runtime Map 身份存在。
- `LocalBuild`：Bundle Provider，不允许远端回退；Editor 下缺 Root Manifest/Bundle Index 会强失败。
- `HotUpdate`：Bundle Provider，允许受控远端回退。
- Player 遇到 EditorDirect/EditorSimulateBuild 直接失败，不自动改设置。

业务代码应使用 `ESAssetRefer` 或 ResourcePlan；不直接构造 Provider。ProviderFactory 只接受 RuntimeMap、全局设置和重试策略。

## Loader、Handle 与 Scope

Runtime Loader 负责主资产、子资产和场景加载、状态查询、引用释放、零引用资产卸载、Bundle 安全点卸载与 awaiting operation 等待。`ESAssetScope` 是所有权边界：Scope 释放自己的 Handle，不应该替其他 Scope 释放资产；Loader 的失败诊断和安全点卸载不能由业务绕开。

## ResourcePlan 作者数据

`ESResourcePlanInfo` 不是单一 Prefab 列表。它包含 Prefab、预热、烘焙资产、扩展源/扩展产物以及 Sprite、Audio、Animation、Animator、Material、Mesh、Texture、Atlas、Avatar、Playable、ScriptableObject、Timeline、Video、Terrain 和 Raw 等强类型条目。Bake 阶段解析 ConfigKey、收集依赖并生成运行时可消费条目；运行时不重新推断作者依赖。

## ResourcePlan 运行事务

每个 Plan 有独立 `Context`：子 Scope、加载取消源、预热 Prefab、扩展 Lease、报告、完成信号、已发布资产表和 retain 计数。状态机为：

```text
Idle -> Loading -> Prewarming -> Ready
                         |-> Failed / Canceled
Ready -> ReleaseAwaiting -> Released
```

- `Core/Game/Override` 是面向玩法的三个 ActiveLinkList：常驻基础、当前内容、临时覆盖。
- 每一次真实激活对应一次 Plan retain；Provider 未 Ready 时先记录，恢复后统一应用。
- Lifetime Scope 只拥有自己的 Plan retain，不拥有其他调用者的 Context。
- Release cooldown 中只有先前完整 `Ready` 且所有加载已完成的 Context 可以 revive。
- Failed、Canceled 或半加载 Context 必须新建事务，不能复活旧失败状态。
- Context Dispose 会取消加载、释放扩展 Lease、撤销已发布 Plan 资产、清空缓存并释放子 Scope。

## Bake 与发布边界

Bake 扩展负责依赖收集、ConfigKey 同步、GameCore 展开和 Extension companion。Preview 与 Bake 输出不能被当成 Player 发布证据；发布至少要验证 Manifest/Bundle Index 完整性、目标平台 Player 读取、Scope 生命周期和释放后引用状态。

## Unity 2022.3 版本校准（外部资料未绑定为项目 SourceRef）

以下内容只校准 Unity/AssetBundle API 的外部行为；ESFramework 的 Manifest、RuntimeMap、Provider、Lease
和回滚合同仍以本条目 SourceRefs 对应的源码、P0 和测试为准：

- Unity 的 AssetBundle Manifest/下载接口可按 bundle Hash、CRC 和依赖关系参与缓存命中与完整性校验；Hash/CRC
  不匹配时不得把结果标记为 `Ready`，也不能把“文件存在”当作已验证资产。
- `AssetBundle.LoadAssetAsync` 只表示异步加载 API 语义，不证明 ES Loader、Scope、Lease 或 Provider transition
  已经正确完成；异步请求完成也不等于资源已正式发布或可安全卸载。
- Hash/CRC/依赖校验只能作为 ES 下载器的输入门禁，不能替代 Manifest/Bundle Index/RuntimeMap 的版本、平台、
  owner、generation 和发布来源校验。
- 外部资料不能证明下载失败、取消、损坏缓存、旧代迟到结果或 Provider 切换已经回滚；这些必须由项目源码、
  负例/故障注入和真实回执分别证明。
- 本版本校准来源未保存为项目本地快照，因此长期声明标记为 `external-source-not-bound`；版本或官方 API
  语义变化时必须重新校准，不能把本节当作项目权威事实。

## 常见失败

- LocalBuild 没有本地发布入口：阻断，不降级 EditorDirect。
- Plan required entry 失败：报告 RequiredFailureCount，不能用 optional 成功掩盖。
- Scope 提前释放：检查 retain 所有者，而不是增加全局常驻引用。
- ReleaseAwaiting 中重入失败 Plan：必须创建新事务。
- 运行时依赖 AssetDatabase/Library：分层违规，应补 Bake 产物。
- `ResourcePlan` Context Dispose 请求取消，不等于底层 Bundle 请求已即时取消；当前 Loader 的合并加载/场景与 Bundle 入口可使用 `CancellationToken.None`，必须等待 pending/收尾回执后才能声明资源已停止加载。
- 依赖获取侧有循环检测不等于释放侧天然安全；当前 `ReleaseAssetBundleTree` 递归释放未形成与获取侧等价的 cycle guard。RuntimeMap 的依赖环必须在构建/验证阶段阻断，或补释放侧防环，不能凭静态状态机宣称异常依赖已闭合。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md` (`3690baec342ba8262d9d64bbafdca85e9c43cf63670f3d12470e4307ffc5df43`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源工具链_四阶段严格隔离_AI协作警告.md` (`98e2336f8e9197051c93eaa3fe774f087463089cc931ffc85f459516e6d48563`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESAssetScope.cs` (`d1551ea4cbc8bccefd7f24038548fa2d650b70ffe6815600f7614b9a543d5ade`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeAssetProviderFactory.cs` (`c63353575fa5f1824cdd4399965efbe47b3b47d0d298f89bf1f9f659f8212ee4`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeAssetLoader.cs` (`f0f8ea295b57a8527d2c664ff4b19dc27c0497dab4f6959601df6de860835a6c`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESResourcePlanInfo.cs` (`20eb8d22012e8fa72d5394b405ffec91bd67087b74d752782b270e3e3bb71822`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESResourcePlanRuntimeService.cs` (`b7d63add470de84de3516c374a5f85d41fb1f74181946664b520ec753b153b22`)
- `Assets/Plugins/ES/Editor/ESResPipeline/ResourcePlan/Baking/ESResourcePlanBakeExtensions.cs` (`48bfac6922d3fcb2d576737d445b75775ef1af79f77ae9aaea7e97e6ef5a53e0`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/资源计划验收（ResourcePlanAcceptance）/资源计划_Scope生命周期绑定_商业项目验收标准.md` (`6d46d8685a24964f6eec820aebb6fd59895a1a0bdbd283a5c811191fce727c1d`)

`EvidenceLevel`: `S1`（源码事实；未取得本次 Player/IL2CPP/发布运行证据）  
`StaleWhen`: RunMode、Provider、RuntimeMap/Manifest、Scope、ResourcePlan 状态机、Bake 合同或 SourceRef 变化。
