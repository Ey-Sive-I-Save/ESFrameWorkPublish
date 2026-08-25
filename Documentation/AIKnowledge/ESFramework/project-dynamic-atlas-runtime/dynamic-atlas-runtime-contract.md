# ES Dynamic Atlas 运行时所有权与恢复契约

`KnowledgeId`: `es.project.dynamic-atlas-runtime-contract.v1`  
`Authority`: `Current project source + AIWarnings + test definitions`  
`RouteKeys`: `dynamic-atlas`, `es-dynamic-atlas`, `runtime-texture`, `atlas-domain`, `atlas-lease`, `atlas-provider`, `atlas-budget`, `atlas-page`, `atlas-recovery`, `atlas-quarantine`, `atlas-monitor`, `remote-avatar`  
`ContentHash`: `22a1875dd48a0cc4a59f5b64b33d7a81439c39449a2b601b186b84548ccca145`
`EvidenceLevel`: `S1`  
`StaleWhen`: Dynamic Atlas Domain/Content/Request 身份、Lease、Provider generation、页分配、上传预算、GPU Fence/隔离、恢复、Graphic 或测试定义及任一 SourceRef 哈希变化。
`RelatedSkills`: `es-resource-pipeline`, `es-performance-budgeting`, `es-observability-evidence`, `es-release-acceptance`
`RequiredReads`: `Documentation/AIKnowledge/Unity/unity-rendering-material-atlas/unity-rendering-material-atlas.md`、`Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/UI与图集（UIAtlas）/P2_UI图标_SpriteAtlas与运行时动态图集分流_AI协作警告.md`

## Scope

本条目负责构建前无法预打包纹理进入 `ESDynamicAtlas` 后的 Domain、Content、Request、Lease、页预算、Provider 代际、隔离和恢复决策。

普通可预打包 Sprite 的选择由 AIWarning 与 `es.unity.rendering-material-atlas.v1` 负责；配置在运行时选择图标不等于需要动态图集。Composite Shader 参数和材质实例不属于本条目。真实 Draw Call、GPU/显存收益由 `es.engineering.rendering-batching-evidence.v1` 负责。

本条目是 Dynamic Atlas 运行时事实的 canonical owner。旧 `es.project.shader-atlas-rendering.v1` 已收缩为共享路由投影，只处理 Shader/图集概念混用并链接到各 canonical 条目，不再复制本条目的运行时事实。

## Trigger and routing

自然语言触发包括：“远端头像怎么进图集”“Lease 什么时候 Dispose”“Provider 重建后旧图还能用吗”“Page Lost 怎么恢复”“动态图集显存上限”“为什么显示占位图”“Quarantined 是什么”“Monitor 数字能否证明性能”“Content revision 怎么用”。

- 出现远端头像、UGC、截图、临时 Texture、Domain/Content/Lease/Provider/Page：首选本条目。
- 出现 `IconKey`、可随包 Sprite、SpriteAtlas：先读分流 AIWarning；大多数情况不应加载本条目。
- 出现实际批次、GPU 成本、显存达标：追加证据条目，Monitor 快照不能单独签收。
- 只出现宽泛 `atlas` 时，先判断是 SpriteAtlas 资产还是 ESDynamicAtlas 运行时对象，避免双路由误命中。

## Decision rules

### 入口选择

- 构建前可收集的 Sprite：使用 `Image + SpriteAtlas`，不得仅因运行时按 Key 选择而进入动态图集。
- 资源系统可重载纹理：使用带 `ESAssetReferTexture2D` 的 Load/Acquire 路径。
- 调用方持有的临时二维 Texture：使用 Copy 路径，并确保调用方生命周期覆盖初始上传；非二维 Texture 必须拒绝。

### 身份与所有权

- Content identity 是 `value + revision`；内容变更必须推进 revision，不能让同一身份指向静默变化的像素。
- `ESDynamicAtlasDomainLease`、`ESDynamicAtlasLease` 与 Observation 都是可释放所有权；复制 struct 不会创建第二份底层引用计数所有权，调用方必须定义唯一 Dispose Owner。
- 不缓存一次 `TryResolve` 得到的 UV。每次需要使用时通过 Lease 解析，并比较 `slotGeneration`、`placementRevision`、`pageGeneration`。

### 必须停止或降级

- Lease 非 Ready/Retired，或页为 Recovering/Quarantined/Failed/Lost：显示占位，不使用旧 UV 冒充成功。
- Provider、Domain policy、SourceRef 或测试定义漂移：旧计划 stale，重新读取并规划。
- 启动 Unity、切换 Provider、制造 Page Lost、运行 PlayMode/Profiler 必须由当前用户明确点名；用户明确要求写资产时可直接实施。选用受管通道时才要求对应 AICommand/TaskContract，缺失只阻断该通道。

## Verified facts

- Domain policy 经净化后约束 pageSize、maxPages、maxGpuBytes、每帧上传数量/像素和未引用保留时间；平台默认值区分 mobile 与非 mobile。[来源：`ESDynamicAtlasContracts.cs`]
- Entry identity 同时包含 Domain、Content、Request、Provider generation 与 Domain generation；Request 固化 padding、颜色空间、Alpha 和过滤模式。[来源：`ESDynamicAtlasRuntime.cs`、Contracts]
- Lease 通过 token 解析当前 Texture/UV，并暴露 Ready、Retired、Recovering、Quarantined、Failed、Lost；解析结果携带 slot、placement、page 三类 generation。[来源：Contracts、Runtime]
- Runtime 在页数或 GPU 字节达到上限时先尝试回收零引用 Ready/Retired 条目；仍不能分配则失败，不静默突破预算。[来源：Runtime、Allocator tests]
- 上传按 Domain 的 `maxUploadsPerFrame` 与 `maxUploadPixelsPerFrame` 预算启动，并区分 CopyTexture、PaddingShader 与 DeferredFenceFallback。[来源：Runtime、Runtime PlayMode tests]
- Provider 迁移、页丢失、GPU 完成状态未知和恢复分别具有显式状态；不安全 GPU 使用结束前不会释放对应 placement/page。[来源：Runtime、Provider acceptance tests]
- `ESDynamicAtlasGraphic` 在 Disable/Destroy/Clear 时取消请求、释放 Observation 与 Lease；请求 revision 防止旧异步结果覆盖新请求。[来源：Graphic、Contract tests]
- MonitorWindow 读取 Snapshot 供诊断。Snapshot 字段和窗口存在只证明可观察入口，不证明目标场景预算或性能已达标。[来源：MonitorWindow、Contracts]

## Common AI failure modes

| 错误行为 | 症状/根因 | 预防与正确动作 | 恢复与证据缺口 |
|---|---|---|---|
| 普通图标全部进动态图集 | 重复上传、额外显存与生命周期复杂度 | 先判定是否可预打包；可预打包则 SpriteAtlas | 迁移资产需独立授权；先标记错误路由 |
| Content 更新不改 revision | 缓存命中旧像素 | 将来源版本/etag 映射到 revision | 释放旧 Lease，使用新身份重新 Acquire |
| 永久缓存 UV | Page 重建后采样错误区域 | 每次经 Lease `TryResolve`，观察 generation | Lost/Recovering 时清空显示并等待通知 |
| 忘记 Dispose Lease/DomainLease/Observation | refCount 不归零、条目和 Domain 长期存活 | 为组件/请求指定唯一 Owner，在 Disable/Destroy/取消路径释放 | 用 Snapshot 定位引用；不得直接篡改 Runtime 内部表 |
| struct Lease 被多 Owner Dispose | 所有权语义混乱、提前释放 | 复制仅用于只读传递；明确一个 Dispose Owner | 重新 Acquire，不依赖已释放 token |
| 把 Retired 当 Ready 或 Failed | Provider 切换期间错误清理/错误宣称 | Retired 仅表示旧代兼容显示；新请求走新 generation | 等新 Lease Ready 后再替换旧 Lease |
| Quarantined 时释放页或继续采样 | GPU 未完成，可能使用不安全资源 | 保持隔离并显示占位，等待探针/恢复 | 使用 Snapshot 记录原因；缺 GPU 回执保持 Blocked |
| 临时 Texture 提前销毁 | 上传前 Source 失效，Acquire 失败 | 调用方保证 Source 覆盖初始上传完成 | 重新创建 Source 并使用新 revision |
| 直接把 Monitor 数字写成性能结论 | 单快照、无基线、无平台/规模 | 记录场景、输入规模、预热、时间窗和 Profiler | 转证据条目执行同输入对照 |
| 看到测试文件就声称恢复通过 | 测试未运行或结果已 stale | 区分测试定义与当前执行回执 | 运行对应 EditMode/PlayMode 后绑定结果 |

## Execution checklist

### 开始前

- 确认纹理确实无法预打包；否则回退 SpriteAtlas。
- 定义 Domain、Content value/revision、Request、Source Owner、Lease Owner 和取消边界。
- 根据平台与峰值输入声明 pageSize、maxPages、maxGpuBytes、每帧上传预算及保留时间。

### 实施中

- Acquire/Copy 支持取消；新请求用 revision 隔离旧异步结果。
- 只在 Lease 可解析时提交 Texture/UV；其他状态统一占位并订阅变更。
- 不持久化 UV 或 generation；不从 MonitorWindow 直接修改 Runtime 权威状态。
- Provider 切换时保留旧 Lease 的 Retired 语义，等新 Lease Ready 后原子替换并释放旧 Lease。

### 完成后

- 静态检查身份、预算、引用计数、取消、失败、隔离、页丢失、恢复和 Dispose 路径。
- EditMode 覆盖 Request 净化、Lease/Graphic/Domain 合同与 Allocator；PlayMode 覆盖上传预算、Provider 迁移、页丢失恢复、隔离与 Graphic 替换。
- Profiler/目标平台验证 GPU 格式支持、上传尖峰、显存峰值和稳态占用；没有执行记录则保持 runtime-not-run。

## Evidence boundary

静态证据可证明状态机、所有权 API、预算字段、回收/恢复代码路径及测试定义存在。它不能证明当前测试已通过、目标 GPU 支持所选上传路径、显存预算达标、Provider 切换无闪烁、无泄漏或发布通过。

`EvidenceRefs`: 未绑定本轮 Unity Test Runner、PlayMode、Profiler、Player 或发布回执；测试源码仅定义应验证的行为。`runtime-not-run`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/UI与图集（UIAtlas）/P2_UI图标_SpriteAtlas与运行时动态图集分流_AI协作警告.md` (`207f74a74d0f5e9cdcf91c5dd23d4f5afb9f40e3899938460a6c159666d4b5c5`)
- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasContracts.cs` (`0efeef56604386ae1f9bc174561d610e0a5b3838e6206bc524c10203262ce8bb`)
- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasRuntime.cs` (`4ad8fafdcc1ed9a4e2d2b8516e6bbaafa0a192d897212886bdd6b168f13b34cf`)
- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasGraphic.cs` (`b7fdb5bf72de1973e3e3085d8ceb0ea1e2cbd47657e05cdd1682b43650e95d0a`)
- `Assets/Scripts/ESLogic/Editor/DynamicAtlas/ESDynamicAtlasMonitorWindow.cs` (`bd611fddaca78a941ec51dd1dc681883fef533c78d81637fed09743f19f8d60d`)
- `Assets/Scripts/ESLogic/Tests/DynamicAtlas/EditMode/ESDynamicAtlasContractTests.cs` (`10794eed401f3d9b6671dfaaa9c88945a445f2fc890579687552e849ced582c4`)
- `Assets/Scripts/ESLogic/Tests/DynamicAtlas/EditMode/ESDynamicAtlasAllocatorTests.cs` (`d3064479deb682b23bb82061d443591513034f67608dbebee6611e70adfd0daa`)
- `Assets/Scripts/ESLogic/Tests/DynamicAtlas/PlayMode/ESDynamicAtlasRuntimePlayModeTests.cs` (`50f6bb312cffbb8d24057ab7a0e71c1edc66239f35a91ba25976c852509dbdd9`)
- `Assets/Scripts/ESLogic/Tests/DynamicAtlas/PlayMode/ESDynamicAtlasProviderAcceptanceTests.cs` (`12bb3cfb4cab04e8a4aeb963e38d841257adfb2e3b56b6c7131d71c2e429408b`)
