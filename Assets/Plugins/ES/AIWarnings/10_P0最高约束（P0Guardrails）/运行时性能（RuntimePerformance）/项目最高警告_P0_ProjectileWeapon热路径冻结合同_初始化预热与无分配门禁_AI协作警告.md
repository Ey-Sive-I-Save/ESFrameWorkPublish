# P0：Projectile / Weapon 热路径冻结合同

`Status`: `current`
`StableId`: `es.aiwarning.p0.projectile-weapon-hotpath.v1`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `projectile-hotpath`, `weapon-fire-hotpath`, `hitscan`, `beam`, `steady-gc`
`Applicability`: Shot/Projectile/HitScan/Beam/Fire/Tick、命中候选/解析及直接调用的缓存、事件、查询。
`EvidenceRef`: `Documentation/AIKnowledge/entries/aiwarning-p0-projectile-weapon-hotpath.md`
`StaleWhen`: 热路径入口、Prepare/Running 生命周期、容量/Lease 规则、静态门禁或 SourceRefs 变化。

## 长期 P0 约束

- 每发子弹、每次开火、活动飞行物和每帧更新都先按热路径处理；正式入口包括 ItemShotModule、ItemShotPhysicsHitSolver、EntityBasicCombatModule 开火/射线/攻击事件。新入口加 `[ESHotPath]` 并纳入静态门禁；无标记不代表可分配。
- 高频实例严格 `Authoring → Prepare → Running → Despawn`：Prepare 完成数组、集合、事件快照、Physics 缓冲和依赖；Running 只读已准备结构，不创建、扩容、重建或首次查找；Despawn 清空状态但复用缓冲。
- 热路径禁止 Ensure、GetComponent、反射、LINQ、格式化/动态日志、异常控制流、Resize、临时集合/数组、GetInvocationList 和首发补建委托。回调在 Prepare 缓存，未准备必须拒绝。
- NonAlloc 查询须有作者可见容量、上限和溢出计数；饱和行为、穿透/忽略集合容量和事件快照语义必须确定，不能以 HashSet(capacity) 声称永不扩容。
- 发射不得逐发 TryAcquireReady 或创建 Lease；Shot Prefab 由 ResourcePlan 预热并通过 ActivePlan 借用。ActivePlan 缺失拒绝发射，Plan Owner 结束前停止并归池活动 Shot。
- Shot 只负责飞行/追踪/碰撞候选/到达/过期/停止；伤害、Buff、VFX、音频、网络和池业务由外层消费。WeaponDefinition 由 Shared/Variable/RuntimeData 持有，特殊子弹复用 Solver/Policy/Resolver。
- 修改链路后执行 `Test-ESProjectileWeaponHotPath.ps1`。静态通过最高为 `Implemented-Unverified`；`Accepted` 必须有 Profiler 稳态 0 B GC、Physics/主线程/溢出率证据，`Released` 还需 Player/IL2CPP/设备与资源生命周期证据。

详细调用链、容量矩阵、自动门禁和原文快照见 Knowledge：`es.aiwarning.p0.projectile-weapon-hotpath.v1`。
