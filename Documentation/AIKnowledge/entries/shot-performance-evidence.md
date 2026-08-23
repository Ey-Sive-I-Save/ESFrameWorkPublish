# Shot 热路径、调度与性能证据完整机制

`KnowledgeId`: `es.project.shot-performance-evidence.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `shot`, `projectile`, `weapon`, `scheduler`, `hot-path`, `allocation`, `gc`, `profiler`, `playable-evidence`  
`ContentHash`: `f5081ad977aba6772c2b6ef5813127d66bb022169445762c85171a7aeecd3f64`

## 性能警告在源码中的落点

Shot/Projectile 的约束不是“所有代码禁止分配”，而是要求把首次构建、稳态 Tick、容量增长和证据分开。`ESShotSimulationScheduler` 集中持有活跃 Shot，避免每颗弹丸各自建立无主 Update；它负责注册、注销、调度顺序和生命周期清理。热路径中的候选、碰撞结果、排序/筛选工作区必须由明确对象预热并复用。

## 四类分配必须分别记录

| 阶段 | 可接受行为 | 必须报告 |
|---|---|---|
| 首次初始化 | 建表、缓存组件、预热容器、建立池 | 初始容量、一次性分配量、依赖缺失 |
| 稳态 Tick/Fire | 复用结果对象和工作区 | 每帧/每发 GC、调用次数、峰值耗时 |
| 容量增长 | 在明确上限外扩容 | 触发条件、新容量、是否造成帧尖峰 |
| 重建/切换 | 场景、武器或 Profile 变化后重建 | 旧对象释放、并发/重入、恢复时间 |

声称“0 GC”必须限定输入规模、预热状态、Unity 版本、Profiler 采样方式和统计窗口；源码中使用 List/Dictionary 并不自动证明稳态有分配，也不能用一次静态编译证明无分配。

## 结果身份与工作区所有权

- 业务结果对象、调度器内部列表、Physics 命中缓冲和排序工作区必须分别声明 owner。
- 调度器不得把一个可变结果列表跨并发调用共享给不相关请求。
- 扩容后可以复用更大缓冲，但必须有预算与诊断；不能为追求“永不扩容”无界预分配。
- 核心热路径依赖缺失应在初始化强失败，不应在每帧以 null-check 静默跳过。

## 现有诊断与测试入口

`ESWeaponShotProfilerScenario` 是运行时诊断场景组件，用于形成可重复武器/Shot 负载；`ESWeaponShotRuntimeTests` 定义调度、定义校验和运行行为测试。只有在 Unity PlayMode/Profiler 中实际运行固定场景、预热后采样并保存 Profiler/报告，才能把证据提升到运行性能结论。

## 实际可玩闭环

完整 Shot 交付至少包含：输入触发、武器定义解析、发射、调度、命中候选、命中解析、伤害/表现、对象回收和可观察诊断。只证明 Scheduler 或数据类编译通过，不足以说明玩家可以操作、表现正确、性能达标或发布可用。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_ProjectileWeapon热路径冻结合同_初始化预热与无分配门禁_AI协作警告.md` (`b8083b6db57ddfea9cf597183d87213e18abc63a1aaffacef1f91edea2a929ce`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md` (`2f5cbca2bf00645da654a88262a228e60999e0a7af44cc35d7a8a7b8267f7665`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_实际可玩闭环与运行证据_AI协作警告.md` (`ef80427c19ab315e9d69ec810caaabb0164a7a2b93f6406d7ee4c5cdd8b7d740`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`d8404c32f25ea889401f0f8c63a969d8fb7e377533200d0d92a8b269d43c2629`)
- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ESShotSimulationScheduler.cs` (`63f344422f57d5ef2aad0a10050e65a3beb2c2e10f0d06c819de041998a0d8ed`)
- `Assets/Scripts/ESLogic/Runtime/Developer/Diagnostics/ESWeaponShotProfilerScenario.cs` (`7f9c9cd16c0ab746872160cfbe2f427b07c6e91af54b3dedfafcb3ef97a57408`)
- `Assets/Scripts/ESLogic/Editor/Generation/Tests/ESWeaponShotRuntimeTests.cs` (`c142475b1ad5721049872bac44d48ce0e37eac0b42b56ffe6b62e34b769ee1c8`)

`EvidenceLevel`: `S1`（源码与诊断入口存在；没有本次 Profiler/PlayMode 结果）  
`StaleWhen`: Shot scheduler、缓冲所有权、武器运行链、Profiler 场景或性能门槛变化。
