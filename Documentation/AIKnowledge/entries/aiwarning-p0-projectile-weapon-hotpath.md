# Projectile / Weapon 热路径冻结：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.projectile-weapon-hotpath.v1`  
`Authority`: `AIWarnings` 原文与当前热路径/性能证据合同  
`RouteKeys`: `aiwarnings`, `p0`, `projectile-hotpath`, `weapon-fire-hotpath`, `hitscan`, `beam`, `steady-gc`  
`HashSchema`: `v2`  
`ContentHash`: `ea427fae64a1a072b45b317810ac5062d3987e46e20923abf8f4e3c452bf9830`  
`SourceSetHash`: `ea427fae64a1a072b45b317810ac5062d3987e46e20923abf8f4e3c452bf9830`  
`EntryBodyHash`: `11797d7d45d31cbac8f930245eb9b6baa491695ca78a477c629e960836007318`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: 热路径入口、Prepare/Running 生命周期、容量/Lease 规则、静态门禁或任一 SourceRef 哈希变化。

## 迁移说明

Warning 保留热路径识别、冻结生命周期、无分配禁令、容量溢出、ResourcePlan 借用、扩展职责和证据等级；本条目承载完整调用链、容量/事件快照规则、门禁命令、Profiler 接受条件和原文语义。Knowledge 不授予 Runtime、Profiler 或发布权限。

## 详细合同

正式入口：`ItemShotModule.Tick/TickScan/TryBuildHitCandidate/ResolveHit`、`ItemShotPhysicsHitSolver.Query`、`EntityBasicCombatModule.TryFireWeapon/TryResolveWeaponRaycast/PublishPrimaryAttackEvent`。新入口必须 `[ESHotPath]`。生命周期为 `Authoring → Prepare → Running → Despawn`，Prepare 建立容量、比较器、Solver、Policy、事件快照、Physics 缓冲和委托；Running 只读稳定结构；Despawn 清状态并复用缓冲。

热路径禁止 Ensure、组件查找、反射、LINQ、字符串格式化、动态日志、异常控制流、Resize、临时集合/数组、GetInvocationList 和首发补建委托。NonAlloc 查询必须声明容量/上限/溢出计数并定义饱和行为；穿透集合、事件快照和比较器变更必须保留确定语义。Shot Prefab 必须由 ResourcePlan 预热并经 ActivePlan 借用，缺失拒绝发射，Plan Owner 结束前停止并归池。

Shot 只负责飞行、追踪、碰撞候选、到达、过期和停止；伤害、Buff、VFX、音频、网络和池业务由外层消费。WeaponDefinition 由 Shared/Variable/RuntimeData 持有；特殊子弹复用 `IItemShotHitSolver`、`IItemShotTickPolicy`、Resolver 和生命周期事件，不复制 Tick/Combat 管线。复杂 Physics/空间索引/Job/Burst 通过替换 Solver/调度边界实现。

修改后执行 `.agents/skills/es-entity-authoring/scripts/Test-ESProjectileWeaponHotPath.ps1 -ProjectRoot <root> -Json`。静态门禁/生成工程/定向 EditMode 最多证明 `Implemented-Unverified`；`Accepted` 要求声明规模和命中密度下 Profiler 稳态 `GC Alloc = 0 B`、Physics/主线程/溢出率/调用次数；`Released` 还要 Player/IL2CPP、设备、预热、池和失败回收证据。

## 原文快照

迁移前完整 Warning（81 行、5505 字节）由以下 SourceRef 保留，原始 SHA-256 为 `b8083b6db57ddfea9cf597183d87213e18abc63a1aaffacef1f91edea2a929ce`。

`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_ProjectileWeapon热路径冻结合同_初始化预热与无分配门禁_AI协作警告.md`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_ProjectileWeapon热路径冻结合同_初始化预热与无分配门禁_AI协作警告.md` (`38751e2e809d1e885ad2d8d0ffae04b4dc63c155d4c4cfb29044ee927c44d330`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`2022a1023244fcc2d3894e6575123f21b73a1e4faa542a4e79f3e7ee596fca04`)

## EvidenceRefs

- `.agents/skills/es-entity-authoring/scripts/Test-ESProjectileWeaponHotPath.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-projectile-weapon-hotpath.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
