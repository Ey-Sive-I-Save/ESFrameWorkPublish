# ESFramework 资源运行时 Lease 与释放边界

`KnowledgeId`: `es.project.resource-runtime-lease-boundaries.v1`
`Authority`: `Current source + AIWarnings P0 + Unity 2022.3 local package metadata`
`RouteKeys`: `resource`, `asset`, `manifest`, `resource-plan`, `asset-refer`, `owner-scope`, `temporary-scope`, `lease`, `release`, `provider-transition`
`ContentHash`: `2254ee450168bc87c482d2cc3385994f7be77efe3d30435b7098f72a68c35d01`
`EvidenceLevel`: `S1`
`RuntimeStatus`: `runtime-not-run`

## 适用范围

本条目回答三个问题：资源如何从发布清单解析到运行时对象，谁拥有一次运行时持有，以及调用结束时应该释放什么。它只覆盖当前静态源码和规则事实，不替代源文件、Unity 实跑或发布验收。

项目基线为 Unity `2022.3.45f1`。本机 `com.unity.modules.assetbundle@1.0.0` 包元数据确认项目使用 Unity AssetBundle 模块；ESFramework 在其上实现自己的 Manifest、RuntimeMap、Provider、Scope 和 Lease 分层，不以 Unity 包模块本身替代 ES 所有权协议。

## AI 首读协议

AI 使用本条目前必须依次执行：

1. 校验全部 SourceRef 路径和 SHA-256，并按合同重算 `ContentHash`。任一不匹配时立即把本条目标记为 stale，回读当前源码和资源 P0；禁止继续按本文写代码。
2. 明确当前任务处于作者数据、Bake、发布 Manifest、RuntimeMap、Provider/Loader、Scope/Lease 中的哪一层。跨层任务必须分别声明各层权威。
3. 先确定生命周期 Owner，再选择加载入口；禁止根据“接口最短”“已有缓存”或“最终返回同一个 Unity 对象”倒推所有权。
4. 在修改源码前填写本文的“AI 修改前强制声明”。任一字段未知时停止，不得猜测默认释放者。
5. 只把静态源码事实标记为 S1。没有对应运行证据时统一保留 `runtime-not-run`，不得宣称编译、PlayMode、Profiler、Player、IL2CPP 或发布通过。

`RegistrationStatus`: `Registered`。本条目已登记到共享 `KnowledgeIndex.yaml`；AIBrain 仍须按 routeKeys 只选择 1～3 个最相关条目，并在使用前校验 SourceRef 与 `ContentHash`。

## 五层权威

```text
ESAssetRefer / ConfigKey
  -> AssetTable / Catalog 解析 ESAssetIdentity
  -> Release Manifest + ESAssetReleaseBundleIndex 定位和校验物理文件
  -> RuntimeMap + Provider/Loader 合并加载并持有底层引用
  -> Resident / Registry Scope / Owner Scope / ResourcePlan / Temporary Lease 表达生命周期所有权
  -> 安全点或 Provider Transition 执行底层 Bundle 卸载
```

1. `ESAssetRefer<T>` 是类型安全的稳定资产身份和编辑器配置载体。它不保存运行时 Handle，不是隐式 Owner，也不拥有 `Release()` 语义。
2. 运行时正式寻址不读取 `ESAssetLibrary`。发布根清单提供全局 Bundle Index 的地址和哈希；全局索引记录文件位置、SHA-256、CRC、大小和依赖，下载器校验后构建 `ESGlobalAssetRuntimeMap`。
3. Provider/Loader 是资产、Bundle 和合并加载的底层持有权威。Scope 或 Lease 的释放只归还本方持有，不等于当帧物理卸载 Bundle。
4. 业务生命周期必须落入一种明确所有权。相同 Unity 对象可被多个 Scope 复用，但每个真实 Owner 仍需自己的 Provider Lease。
5. `AssetBundle.Unload(false)` 只在 ES 的安全点或 Provider 收尾路径发生；普通业务不直接驱动底层 Bundle 卸载。

## 不可违反的不变量

1. 身份、持有和物理卸载是三件事：`ESAssetRefer` 表达身份，Scope/Plan/Lease 表达持有，Provider/Loader 决定底层引用与卸载。
2. 每一次需要跨调用栈保存资产的使用，都必须能指出唯一的生命周期 Owner；只读即时诊断例外不得把结果保存到字段或长期状态。
3. 一个 Owner 只能释放自己取得的 Scope、Plan retain 或 Lease；共享同一个 Unity 对象不产生跨 Owner 释放权。
4. `ESAssetScope` 对同一 identity 是 Owner 聚合，不是逐次调用计数器；TemporaryScope 的 `ReferenceCount` 和 `LeaseCount` 才是逐次持有。
5. ResourcePlan 外部 Lifetime Scope 只拥有 Plan retain；Plan Context 始终拥有自己的私有资源 Scope。
6. 活动 Plan 资产借用不建立持有，不允许 Release，且必须在所有权结束通知完成前停止使用。
7. 调用方取消等待不自动等于底层合并加载已取消，也不自动等于 Scope 已释放；必须按具体入口判断回滚规则。
8. `ReleaseScope`、Lease Dispose 或 Plan retain 归零只表示逻辑所有权归还，不证明 Bundle、GPU 或托管对象已在当前帧卸载。
9. Provider Transition 开始后，所有旧 Scope、旧 Token 和旧代迟到结果都不得进入新 Provider 状态。
10. 缺失 Manifest、Bundle Index、AssetTable 或 RuntimeMap 时必须失败并修复产物；禁止扫描 Library/AssetDatabase 或切换 RunMode 作为静默回退。

## 入口与所有权决策表

| 入口 | 所有者 | 重复加载语义 | 正确释放 | 禁止误读 |
|---|---|---|---|---|
| `ESAssets.LoadAsync(refer)` | 默认 `GameSession` Registry Scope | 同 Scope、同 identity 最多持有一次 | 游戏流程唯一责任方调用 `ReleaseScope(GameSession)` | 不是 Resident，也不是一次调用一个引用计数 |
| `ESAssets.LoadAsync(refer, domain/stringKey)` | 指定 Registry Scope | Scope 内聚合；首次可自动创建 | 对应流程管理器调用一次 `ReleaseScope`，父域先释放子域 | 自动创建不等于自动释放；StringKey 不能冒充枚举域 |
| `ESAssets.LoadAsync(refer, owner)` | Unity `Component` 对应的 Owner Scope | 同 Owner、同 identity 最多持有一次；不同 Owner 各自持有 | Owner 销毁时由跟踪器结束其 Scope | Provider 缓存命中不等于借用 ResourcePlan |
| `ESAssets.LoadResidentAsync(refer)` / 非 Scene `ESAssetRefer<T>.PreloadAsync()` | 资源会话基础设施 | Resident Scope 聚合 | 资源安全点、Provider Transition 或会话结束 | 不用于一般场景、界面或短期任务；`ESAssetReferScene.PreloadAsync` 当前是 no-op |
| `ResourcePlan.ApplyAsync()` / ActiveLink / Binder | ResourcePlan Context 的私有 Scope；调用方持有 Plan retain | 多个调用方增加 retain，共享同一可用 Context | 每个持有方只归还自己的 retain；最后一个 retain 才进入释放 | 外部 Lifetime Scope 只拥有 retain，不拥有或替换 Plan 私有 Scope |
| `ESAssetTemporaryScope.LoadAsync(refer)` | 全局 Temporary Scope 中按 identity 记录的一次 `ReferenceCount` | 每次成功调用增加一次计数 | 每次成功调用必须配对一次同 Scope 的 `Release(refer)` | `Release(refer)` 是按身份扣一次，不是单次调用的幂等 Token |
| `ESAssets.LoadTemporaryAsync(refer)` / `LoadAsyncLease` | 独立 Lease Token | 每次成功调用分配独立 Token；值复制共享同一 Token | 仅 Dispose 自己的 Lease；重复 Dispose 只归还一次 | 不要 Dispose 全局 Temporary Scope 来代替局部释放 |
| `TryGetActivePlanAsset` | 活动 Plan 资产的只读借用者 | 不新增持有 | 不释放；在 `ActivePlanAssetOwnershipEnding` 完成前停止使用 | 借用不能保存为越过 Plan 生命周期的长期状态 |

普通短期异步任务应优先选择独立 Temporary Lease，因为其释放与其他调用者隔离。只有明确接受“清空整个全局临时域、推进 generation、使全部旧 Token 失效”时，高级框架代码才可 Dispose `ESAssets.TemporaryScope`。

## 机械选择算法

本算法针对“当前调用方的这一份使用责任”，不是给资产全局贴唯一标签。同一资产可以同时由 Plan、Owner 或其他合法 Owner 各自持有；资产已列入 Plan 不代表普通消费者可以借用 Plan，也不阻止消费者取得自己的 Owner Scope。对当前这一份使用按顺序回答，命中后停止；不得叠加多个入口“保险持有”：

1. 只需同步观察且绝不保存结果？已有 Owner 时用 `TryLoad(owner, out asset)`；无 Owner 的 `TryLoad(out asset)` 只允许即时诊断或同一调用栈短暂只读，未命中不得触发加载。
2. 一组资源是否随地图、区域、模式、预热或功能计划共同准备和退出？使用 ResourcePlan，由每个调用方持有并归还自己的 retain。
3. 资产是否严格绑定一个 Unity `Component`/GameObject 的销毁？使用 Owner Scope：`LoadAsync(refer, owner)`。
4. 资产是否属于一个有唯一流程关闭者的共享生命周期？使用枚举或稳定前缀 StringKey Registry Scope，并指定唯一 `ReleaseScope` 责任方。
5. 是否为资源会话启动和全局基础设施必需资产？只有此时使用 Resident。
6. 是否为一次短期任务且需要独立、幂等释放？使用 `LoadTemporaryAsync` / `LoadAsyncLease` 并 Dispose 自己的 Lease。
7. 是否只是受信框架系统读取已由活动 Plan 持有的资产？仅可用 `TryGetActivePlanAsset` 借用，并接入所有权结束通知；普通业务不得选择此项。
8. 以上均不匹配时停止设计，先补充真实生命周期 Owner；禁止新增全局缓存、隐式 Resident、临时 Scope 或第二套引用计数兜底。

## ResourcePlan 事务边界

`ESResourcePlanInfo` 只保存作者数据和烘焙快照，不保存运行时资源状态。`ESResourcePlanRuntimeService` 为每个活动 Plan 创建 `Context`，其中包含私有 `ESAssetScope`、加载取消源、预热记录、扩展 Lease、已发布资产和 retain 归属。

```text
Apply
  -> 校验 AssetTable / Provider Ready
  -> 创建或复用可恢复的 Context
  -> 先登记本次 retain
  -> ConfigKey 经 Catalog 解析为 ESAssetIdentity
  -> 私有 Scope.LoadResolvedAsync
  -> Required 全部完成且 RequiredFailureCount = 0 才进入 Ready，否则 Failed

Final Release
  -> retain 降至 0
  -> 从活动表移除并进入 ReleasePending
  -> 取消尚未完成的加载
  -> 等待所有加载收尾
  -> releaseDelay 防抖
  -> 逆序释放扩展 Lease，归还预热
  -> Dispose Plan 私有 Scope
  -> Released
```

外部取消 `ApplyAsync` 等待时，服务会回滚该调用方刚取得的 retain；它不是只取消观察而继续暗中持有。释放冷却期只允许完整可恢复的 Context 被重新激活；失败、取消或仍在取消加载的 Context 不能冒充可复用的 Ready 事务。

## TemporaryScope 与 Lease 机制

`ESAssetTemporaryScope` 在一个内部 `ESAssetScope` 上维护两套独立计数：

- `ReferenceCount` 对应普通 `LoadAsync` / `Release(refer)` 配对。
- `LeaseCount` 对应独立 Token；Token 表中同时记录 `ESAssetIdentity` 和 `generation`。

只有两套计数均归零且加载完成，TemporaryScope 才从内部 Scope 归还该 identity。等待取消或加载异常会回滚当前入口增加的计数。安全点和 Dispose 都会推进 generation 并清空 Token；旧 Lease 此后释放失败，但不能扣减新一代 Scope 的持有。

Lease 是轻量值类型，不代表无限唯一身份。复制 Lease 会复制同一个 Token，因此多个副本重复 Dispose 仍只生效一次；这解决的是单次租期的幂等归还，不是跨 Provider 或跨代际复用。

## 取消语义判定表

| 入口 | 调用方取消后的当前源码语义 | AI 不得推断 |
|---|---|---|
| 普通 `ESAssetScope.LoadAsync`，包括 Registry、Owner、Resident | 调用方 Token 只取消自己的等待；首次请求启动的合并加载使用 `CancellationToken.None`，Scope 若仍有效会接收结果并保留自己的 Lease | 不得写成“取消 await 自动释放 Owner Scope”或“取消底层共享加载” |
| `ESAssetTemporaryScope.LoadAsync` | 取消等待会进入回滚路径，归还本次增加的 `ReferenceCount`；其他引用和底层合并加载按各自状态继续 | 不得扣减其他调用者，也不得把整个 TemporaryScope Dispose |
| `LoadTemporaryAsync` / `LoadAsyncLease` | 公开 Token 只在加载成功后创建；等待失败或取消先归还本次增加的 `LeaseCount`，不会留下可用 Lease | 不得伪造一个无效 Token 再要求调用方释放 |
| `ResourcePlan.ApplyAsync` | retain 在等待前登记；外部取消会调用相同归属的 `ReleaseAsync` 回滚该调用方 retain | 不得只停止观察却暗中保留该 retain，也不得释放其他持有者 |
| Provider Transition / Scope Dispose | 新请求被拒绝；旧合并加载可以迟到完成，但只允许归还旧 Handle，不得写入新代状态 | 不得把旧结果挂到同名新 Scope、RuntimeMap 或 Provider |

以上是当前静态源码行为，不是已执行的竞态测试结论。修改任何一行对应实现时，必须新增或更新并发、取消和代际隔离验证。

## 释放顺序与失败边界

正确关闭顺序是：

```text
拒绝新请求
  -> 取消或隔离在途业务续体
  -> 停止仍借用资产的 Voice/VFX/UI/玩法实例
  -> 归还本方 Lease、Owner Scope 或 Plan retain
  -> 等待旧 Scope 的迟到 Handle 自动归还
  -> Provider 安全点检查引用与在途请求
  -> 卸载零引用资产或 Bundle
  -> 必要时重建 RuntimeMap / Provider
```

以下行为属于边界错误：

- 运行时扫描 `ESAssetLibrary`、AssetDatabase 或项目目录补救缺失 Manifest。
- `LocalBuild` 缺少发布入口时自动降级 `EditorDirect`。
- 把 `ESAssetRefer` 当成持有者，或给它增加 `Refer.Release()`。
- Owner Scope、Registry Scope 或 Temporary Scope 借用活动 Plan 并冒充自己的持有。
- 一个调用方释放另一个 Scope、Plan retain 或 Lease。
- 把 `ReleaseScope` 的同步返回描述成所有异步请求和 GPU/Bundle 内存已物理终结。
- Provider Transition 后允许旧 Scope、旧 Token 或迟到回调写入新一代状态。

## AI 修改前强制声明

任何资源运行时代码修改前，AI 必须先给出以下字段；禁止省略或用“框架自动处理”代替：

```text
TargetStage: Authoring / Bake / Manifest / RuntimeMap / Provider / Scope
AssetIdentitySource: ConfigKey / AssetTable / ESAssetIdentity / other
OwnershipKind: Resident / Registry / Owner / ResourcePlan / TemporaryReference / TemporaryLease / ActivePlanBorrow
AcquireEntry: 精确 API
ReleaseAuthority: 唯一责任方与触发时机
CancellationEffect: 只取消等待 / 回滚本次计数 / 回滚本次 retain / other
ProviderTransitionEffect: 新请求如何拒绝，旧结果如何收尾
BorrowingRule: 是否借用；若借用，结束通知和停止点
EvidenceTarget: Static / Compile / PlayMode / Profiler / Player / IL2CPP / Release
```

若 `OwnershipKind` 是 `ActivePlanBorrow`，则 `ReleaseAuthority` 必须明确为“借用方不得释放”；若选择 `Registry`，必须给出唯一流程关闭者；若选择 `TemporaryReference`，必须证明每次成功调用都有一一配对的 `Release(refer)`；否则方案直接判定不成立。

## AI 反例自检

在提交设计或代码前，AI 必须逐项确认自己的方案不会违反以下场景：

- 两个短期任务加载同一 identity，释放 A 的 Lease 不影响 B。
- Lease 被值复制并多次 Dispose，底层只扣减一次。
- Temporary 普通引用与独立 Lease 混用时，`ReferenceCount`、`LeaseCount` 分别归零后才归还内部 Scope 持有。
- 普通 Owner/Registry Scope 的等待被取消，不被误写为 Owner 生命周期已经结束。
- 一个 ResourcePlan 调用方取消或释放，不改变其他调用方的 retain。
- `TemporaryScope.Dispose()` 被视为全域破坏性清理，而不是某次任务的便捷释放。
- `ReleaseScope` 返回后仍允许旧在途请求在旧代完成并自动归还，且不宣称物理卸载已经完成。
- Provider 切换后旧 Lease、旧 Scope 和迟到结果均不能触碰新代。
- `LocalBuild` 缺发布入口时明确失败，不回退 EditorDirect、AssetDatabase 或 Library 扫描。
- `TryGetActivePlanAsset` 的借用方只停止使用，不释放、不缓存到 Plan 生命周期之外。

这些场景是静态审查清单，不是运行通过证据。任何无法由当前源码或对应测试证明的结果必须标记为 `Deferred` 或 `runtime-not-run`。

## 已验证事实、派生结论与非声明

已验证事实：上述类型、入口、计数、generation、retain 回滚、发布索引读取、运行模式锁定和安全点卸载路径存在于当前 SourceRefs；现有资源 AIWarnings 与源码 SourceRef 哈希未发生漂移。

派生结论：调用方选择 API 时应先确定生命周期 Owner，而不是先按“同步/异步”或“是否缓存”选入口。缓存复用、业务持有和物理卸载是三个不同问题。

非声明：本条目未运行 Unity、Test Runner、PlayMode、Profiler、Player、IL2CPP、下载、AssetBundle 发布或远端发布；不证明当前分支可编译，不证明 T1-T7、R1-R11 或 ResourcePlan P1-P10 已通过，也不证明资源释放后内存会在同一帧归还。

静态测试绑定：`ESAssetScopePoolingTests` 覆盖 Scope 聚合、ReleaseScope 所有权和 ProviderTransition 旧 Scope 拒绝；`ESDynamicAtlasProviderAcceptanceTests` 覆盖 Provider transition 下旧 Lease/代际隔离。两者仅绑定当前测试源码，未执行 Unity Test Runner。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Library/PackageCache/com.unity.modules.assetbundle@1.0.0/package.json` (`ebd091c022c34316fa17029dafbcb391a37284196ba07e12b369e332fd135271`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md` (`3690baec342ba8262d9d64bbafdca85e9c43cf63670f3d12470e4307ffc5df43`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源工具链_四阶段严格隔离_AI协作警告.md` (`98e2336f8e9197051c93eaa3fe774f087463089cc931ffc85f459516e6d48563`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/资源计划验收（ResourcePlanAcceptance）/资源计划_Scope生命周期绑定_商业项目验收标准.md` (`6d46d8685a24964f6eec820aebb6fd59895a1a0bdbd283a5c811191fce727c1d`)
- `Assets/Plugins/ES/0_Stand/_Res/ResUse/ESAssetRefer.cs` (`c2cadfc99bb078f36fc29e0f0d111c93f30bedeaf7ecf1201c076961ad9fca6d`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESAssetScope.cs` (`d1551ea4cbc8bccefd7f24038548fa2d650b70ffe6815600f7614b9a543d5ade`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeAssetLoader.cs` (`f0f8ea295b57a8527d2c664ff4b19dc27c0497dab4f6959601df6de860835a6c`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeAssetProviderFactory.cs` (`c63353575fa5f1824cdd4399965efbe47b3b47d0d298f89bf1f9f659f8212ee4`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeReleaseDownloader.cs` (`50ea89012643e14501c07f2ca6964b2eb46175d885fb10ccaf22fe998552a117`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESResourcePlanInfo.cs` (`20eb8d22012e8fa72d5394b405ffec91bd67087b74d752782b270e3e3bb71822`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESResourcePlanRuntimeService.cs` (`b7d63add470de84de3516c374a5f85d41fb1f74181946664b520ec753b153b22`)
- `Assets/Plugins/ES/1_Design/Tests/ESAssetScopePoolingTests.cs` (`0cdf084d3b2ac8ebfeca5e12063a1a8b41eeb70eab5033895d32f84693814650`)
- `Assets/Scripts/ESLogic/Tests/DynamicAtlas/PlayMode/ESDynamicAtlasProviderAcceptanceTests.cs` (`12bb3cfb4cab04e8a4aeb963e38d841257adfb2e3b56b6c7131d71c2e429408b`)

`StaleWhen`: Unity/AssetBundle 模块版本、发布 Manifest 或 Bundle Index schema、AssetTable/RuntimeMap、RunMode/Provider、`ESAssetRefer` 身份合同、Scope/Temporary generation、ResourcePlan retain/Context 状态机、释放安全点、任一 SourceRef 路径或 SHA-256 变化。
