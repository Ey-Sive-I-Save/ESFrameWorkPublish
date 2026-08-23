# P0：Projectile / Weapon 热路径冻结合同

> 状态：现行 P0 约束。
> 规则 ID：`es.p0.projectile-weapon-hotpath-freeze`。
> 路由键：`projectile-hotpath`、`weapon-fire-hotpath`、`hitscan`、`beam`、`steady-gc`。
> 适用范围：`Shot`、`Projectile`、`HitScan`、`Beam`、`Fire`、`Tick`、命中候选、命中解析和它们直接调用的缓存/事件/查询代码。

## 最高结论

只要代码会被每发子弹、每次开火、每个活动飞行物或每帧更新调用，就先按热路径处理，再讨论功能扩展。不能等 Profiler 或用户提醒后才补预热和低 GC。

当前正式热路径入口为：

```text
ItemShotModule.Tick / TickScan / TryBuildHitCandidate / ResolveHit
ItemShotPhysicsHitSolver.Query
EntityBasicCombatModule.TryFireWeapon / TryResolveWeaponRaycast / PublishPrimaryAttackEvent
```

新入口必须使用 `[ESHotPath]` 标记，并纳入本规则的静态门禁。没有标记不代表可以分配；标记只是让审查工具能够拒绝漏检。

## 冻结生命周期

每个高频实例必须遵守：

```text
Authoring -> Prepare -> Running -> Despawn
```

- `Authoring`：可配置容量、比较器、Solver、Policy、事件订阅和依赖。
- `Prepare`：在 `Start`、`OnPoolSpawned`、`Internal_InitializeSpawn`、场景预热或明确绑定边界完成数组、集合、事件快照、Physics 缓冲和必需依赖初始化。
- `Running`：配置视为只读；热路径只读已准备结构，不得创建、扩容、重建或首次查找。
- `Despawn`：清空实例状态，不销毁可复用缓冲；下一次 Spawn 必须复用同一结构。

热路径不得调用 `Ensure*`、`GetComponent*`、`GetComponents*`、反射、LINQ、字符串格式化、动态日志、异常控制流、`Array.Resize`、临时集合、临时数组或每次派发 `GetInvocationList()`。

实例方法组转换为 `Action`/委托也必须按分配处理：Shot 生命周期观察者、命中回调和其他每发回调必须在 `Prepare` 阶段创建并缓存，热路径只能读取已缓存委托；未准备时必须确定性拒绝，禁止在首发时偷偷补建。

## 容量与正确性

1. 所有 `NonAlloc` 查询必须有作者可见容量、明确上限和溢出计数。
2. 饱和时禁止隐式扩容；必须记录溢出，并在定义/测试中说明“可能遗漏未返回命中”还是采用替代 Solver。
3. 穿透/忽略命中集合必须说明稳定容量和超过容量后的行为；仅仅用 `new HashSet(capacity)` 不能声称永不扩容。
4. 事件订阅产生的快照只能在订阅冷路径重建；派发期间必须复用稳定快照，并保留订阅变更期间的快照语义。
5. 调试名称、日志和完整诊断只能在显式诊断开关下读取或格式化，不能污染默认开火路径。
6. 每次发射不得调用 `TryAcquireReady` 或创建独立资源 Lease；Shot Prefab 必须由 ResourcePlan 预热并通过 ActivePlan 只读借用。ActivePlan 缺失时拒绝发射，禁止退回 Provider 缓存或临时租约。活动 Shot 必须订阅 `ActivePlanAssetOwnershipEnding`，并在最终 Plan Owner 释放前同步停止和归池。

## 扩展边界

- Shot 只负责飞行、追踪、碰撞候选、到达、过期和停止；伤害、Buff、VFX、音频、网络和对象池业务由外层消费。
- WeaponDefinition 只由 `ItemWeaponSharedData` / `ItemWeaponVariableData` / `ESWeaponRuntimeData` 持有；`EntityBasicCombatModule` 只读取已解析定义并执行。
- 特殊子弹应通过 `IItemShotHitSolver`、`IItemShotTickPolicy`、命中 Resolver 和生命周期事件扩展，不复制第二套 Tick 或 Combat 管线。
- 需要批量 Physics、空间索引、Job/Burst 或分组 Tick 时，替换 Solver/调度边界，不把复杂策略塞进单个 Shot 的每帧方法。

## 自动门禁

修改上述链路后必须执行：

```powershell
& '.agents/skills/es-entity-authoring/scripts/Test-ESProjectileWeaponHotPath.ps1' `
  -ProjectRoot 'F:\aaProject\ESFrameWorkPublish' -Json
```

门禁至少拒绝标记热方法中的动态缓存准备、数组/集合扩容、组件查找、反射、LINQ、临时集合、未缓存的每发委托、每次事件调用列表复制，以及 Shot Spawner 中的逐发 `TryAcquireReady`。命中时先修复或明确把代码移到 `Prepare`/诊断冷路径，不能用注释压过检查。

## 验收层级

- `Implemented-Unverified`：静态门禁通过，生成工程编译通过，定向 EditMode 覆盖容量饱和、池复用、跳帧累计和事件快照语义。
- `Accepted`：在声明的实例规模、命中密度和容量下，Unity Profiler 稳态 `GC Alloc = 0 B`，并记录 Physics、主线程、溢出率和调用次数。
- `Released`：Player/IL2CPP 和目标设备复验，且资源预热、对象池、生命周期和失败回收证据完整。

静态检查、Editor 单次测试或生成 `.csproj` 编译不得冒充 `Accepted` / `Released`。没有 Profiler 证据只能报告“静态无已识别显式分配，待实测”。

## 审查清单

1. 是否列出了完整调用链，而不是只看某个数组或某个方法？
2. 所有容量、事件快照、比较器和依赖是否在 `Prepare` 完成？
3. 命中饱和、穿透集合饱和、池复用和定义缺失是否有确定行为？
4. 特殊子弹是否复用 Solver/Policy/Resolver，而不是复制 Combat 或 Tick？
5. 结论属于源码、静态编译、EditMode、Profiler、Player 还是 IL2CPP 哪一层？
