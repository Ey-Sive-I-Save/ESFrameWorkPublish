# ES Buff Runtime Standard

状态：代码与程序集编译通过；Unity Test Runner、实际 GameCore Bake 与长时间 Player/IL2CPP 压测待验收。  
最后验证：2026-07-31。  
适用源码入口：`BuffDefinitionDataInfo`、`EntityBuffDomain`、`ESActiveBuffRuntime`。

## 目标

Buff 是一次有生命周期的游戏效果，不是第二套 Tag、属性、事件或对象池系统。它只负责把一个稳定的 Buff 定义在目标对象上按规则启动、叠加、刷新、Tick 与结束；每种效果都必须委托给 ES 已有的权威基础设施。

```
GameCore BuffDefinition
    -> EntityBuffDomain（叠层、来源、冲突、Tick）
    -> ESActiveBuffRuntime（本次 Buff 生命周期）
    -> Entity.Tags / Entity ValueChange / ESOpSupport / Link / Pool
```

## 权威归属

| 内容 | 权威位置 | Buff 的职责 |
| --- | --- | --- |
| Buff 稳定身份 | GameCore `ESBuffConfigKey` | EnumKey / StringKey 解析为本进程 RuntimeKey；不持久化裸 RuntimeKey |
| 可组合事实 | `ESTagCollection` | Buff 只持有自己的 `ESTagLeaseSet`，向目标 `Tags` 写入并在结束时归还 |
| 数值与权限变化 | Entity 的 ValueChange / Permit | Buff 只持有对应 Effect Lease / Token |
| 行为执行 | `ESOutputOp` + `ESOpSupport` | Apply、Refresh、Tick、Remove 使用当前 Buff 的短生命周期 Support |
| 依赖刷新 | Expression Dependency + Link | 仅脏依赖或配置指定时刷新，不在稳定帧反复求值 |
| 生命周期与复用 | `ESSimplePool` + Entity Pool 生命周期 | Buff Runtime 回收前必须释放所有自己持有的资源 |

Buff 不承载阵营、伤害结算、资源身份、永久属性定义或状态机执行本身；需要这些能力时分别调用对应 ES 子系统。

## 业务调用

业务只传 Buff 定义或稳定 Key，Buff 域自行从 GameCore 表解析权威配置；业务代码不查询 RuntimeKey、不构造 `BuffSharedData`，也不逐项申请 Tag 或 ValueChange：

```csharp
// 直接引用定义：适合 Prefab、技能资产等已经持有定义的地方。
entity.buffDomain.AddBuff(buffDefinition);

// 固定 Enum Key：适合代码强约束的常用 Buff。
entity.buffDomain.AddBuff(ESBuffEnumKey.Burn);

// String Key：适合表格、活动、热更新内容；只在施加时解析一次。
entity.buffDomain.AddBuff("status.poison");

// 本次独立持续时间，仍由该 Buff 的叠层/刷新配置决定如何处理重施加。
entity.buffDomain.AddBuff(ESBuffEnumKey.Shield, durationOverride: 3f);

// 状态机时间作为该 Buff 的时钟来源。
entity.buffDomain.AddBuffByStateTime(ESBuffEnumKey.Aiming, state);
```

重施加、叠层和单次覆盖不应由业务层先 Remove 再 Add；再次调用同一 `AddBuff` 即可，定义中的 `stackMode`、`timeRefreshMode`、`buffGroup`、`groupConflictMode` 与 `strength` 是唯一权威：

| 目标效果 | 定义配置 |
| --- | --- |
| 独立多个实例 | `IndependentInstance` |
| 同来源层数叠加 | `StackSameBuff` + `maxStack` |
| 只刷新持续时间 | `RefreshSameBuff` |
| 覆写层数并刷新时间 | `ReplaceSameBuff` |
| 已达上限则不再变化 | `IgnoreSameBuff` |
| 不同 Buff 的强弱覆盖 | 相同 `buffGroup` + Group Conflict + `strength` |

`sourceIsolationMode` 决定“同一个 Buff”是否还要按施法者、装备、OpSupport 或 `customSourceId` 分开计数；多个来源的 Buff 生命周期始终独立。

### Buff 操作集：复杂业务的唯一入口

`AddBuff(key)` 仍是默认入口，完全遵循定义中的叠层、刷新和互斥规则。需要明确改变已存在效果时，不要手写 `Remove + Add`，使用零分配值类型 `ESBuffOperation`：

```csharp
// 命中同一来源的中毒：重置计时、增加一层、并设为 3 级。
entity.buffDomain.ApplyBuff(
    "status.poison",
    ESBuffOperation.Default
        .ResetDuration()
        .AddStack(1)
        .SetLevel(3),
    sourceSupport: attackSupport);

// 延长两秒；只在效果已存在时执行，未命中不创建。
entity.buffDomain.ApplyBuff(
    ESBuffEnumKey.Shield,
    ESBuffOperation.Default.AddDuration(2f).OnlyIfPresent());

// 按 Key + 来源移除一个可唯一定位的 Buff。
entity.buffDomain.ApplyBuff("status.poison", ESBuffOperation.Remove);
```

在 `ESOutputOp` 配置里，不需要把这些字段拆成多层 Buff 配置，也不需要手写业务调用。先用既有 Targeting Op 决定 `entityMainTarget`，再选择 **应用 Buff 到主目标**：

- `buff`：统一的 `ESBuffConfigKey` Picker，可填 Enum、String，或同一声明的双别名；冲突别名会被拒绝。
- `operation`：中文直接配置“执行、层数、持续时间、等级”；只显示当前操作需要的数值字段。代码侧仍可用 `Default`、`ResetDuration`、`AddStack`、`SetLevel`、`Remove` 组合。
- `customSourceId`：只在定义使用 `ByCustomSourceId` 来源隔离时填写。

因此“重置中毒时间并加一层”是一个 Op，不是策划配置多个互相依赖的步骤：

```csharp
operation = ESBuffOperation.Default.ResetDuration().AddStack(1);
```

对自己施加时，在它前面放“使用者设为主目标”；对敌人施加时，前面的 Targeting Op 已经把命中对象设为主目标。`OpCommon_Sequence` 仍是需要串联多个不同效果时的唯一组合工具。

| 操作 | 已存在 Buff | 不存在 Buff |
| --- | --- | --- |
| `Default` | 完全等同再次 `AddBuff`，使用定义的叠层/时间规则 | 创建默认实例 |
| `AddStack` / `SetStack` | 增加或设定层数，限制在 `maxStack` | 创建时直接采用指定层数 |
| `ResetDuration` | 回到定义持续时间 | 创建默认持续时间 |
| `AddDuration` / `SetDuration` | 增加或设定剩余时间；无限 Buff 不因加时变为有限 | 创建时采用给定时间 |
| `AddLevel` / `SetLevel` | 改变等级，限制在 `maxLevel` | 创建时采用指定等级 |
| `OnlyIfPresent()` | 正常执行 | 不创建，直接无效果 |
| `Remove` | 移除该实例 | 无效果 |

同一 Key 在同一来源下若配置为多个 `IndependentInstance`，Key 无法安全猜测该改哪一个；必须保留 `AddBuff` 返回的 `ESActiveBuffRuntime`，再调用 `ApplyBuff(runtime, operation)`。等级是运行时事实，Buff 的 Op/自定义表达式可从 `scopeSupport.GetOwner<ESActiveBuffRuntime>()` 读取 `Level`；它不会凭空给数值效果加倍率，具体倍率仍由 Buff 定义表达。

`sourceSupport` 只在施加瞬间用于解析来源身份和复制来源/目标信息。Buff 不保存这个 Support，也不在未来的 Refresh、Tick、Remove 中回头使用它，因为攻击和技能 Support 可以先于延时 Buff 回池。长效 Buff 后续 Op 始终使用自己的 Buff Support 与自有 TargetPack 快照；需要跨生命周期传递的数据必须放入 Buff 定义，或显式写入会被复制的 Entity/Item 目标与数值槽位，不能依赖临时 Support 的 Context、缓存或 TargetPack extras。

### 可选自定义机制逻辑

`logic` 是 `BuffSharedData` 上唯一的复杂机制扩展点。它只用于护盾吸收、受击累计、战斗事件订阅、动态目标规则等确实需要独立运行状态的 Buff；Tag、数值、权限、普通 Tick 与固定流程仍优先使用已有配置和 Op，禁止为了简单加减属性创建 Logic。

```text
BuffDefinition.logic (ESBuffLogic, 只读配置)
    -> 每次实际施加时 RentRuntime()
    -> ESBuffLogicRuntime (该 Active Buff 独占状态)
    -> Apply / Refresh / Tick / Remove / Release
```

`ESBuffLogic` 绝不保存目标、层数、订阅句柄、Lease、Token 或计时器。它必须从自己的池租出一个 `ESBuffLogicRuntime`；后者通过 `Buff`、`Owner`、`Target`、`Support` 访问当前实例，并在 `OnRelease()` 中归还自己建立的订阅、Lease 和 Token。框架随后调用 `TryAutoPushedToPool()`，因此多个来源或多个独立实例不会共享机制状态。

| 回调 | 调用时机 | 失败语义 |
| --- | --- | --- |
| `OnApply()` | 标准 Tag / ValueChange / Permit 已成功建立后，Apply Op 前 | 返回 `false` 或抛异常会完整回滚 Buff；不执行正常 `OnRemove()` |
| `OnRefresh()` | 层数、时间或等级变更及相关 ValueChange 刷新后，Refresh Op 前 | 记录并隔离异常，Buff 仍保持有效 |
| `OnTick(deltaTime)` | 复用该 Buff 的 TickMode、间隔和追帧上限，Tick Op 前 | 异常使当前 Buff 结束，不影响同 Entity 其他 Buff |
| `OnRemove()` | 正常移除时，Remove Op 前 | 记录并隔离异常，后续清理继续执行 |
| `OnRelease()` | 正常移除、Apply 回滚、池化清理都会执行；标准资源释放前 | 必须释放 Logic 自己资源；即使异常也仍尝试回池 |

没有配置 `logic` 时，Buff 不租用运行对象、不创建集合、不注册 Link，也不产生 GC；仅保留现有生命周期中的空引用快速判断。Logic 的 `OnTick` 与其调用链属于 Buff 热路径，稳定帧必须保持 0 GC。

### 状态效果帧：完整覆盖，不是逐项猜测删除

`AddBuff` 用于“施加一次并由 Buff 自己计时/结束”。状态机、姿态、装备槽预览等场景有另一种需求：**某个来源在本次更新后应该完整拥有哪一组状态效果**。此时使用与输入系统同样的三步帧语义：

```csharp
// state 是稳定的运行时对象；它只清理自己写出的状态效果。
if (entity.buffDomain.BeginBuffFrame(state))
{
    entity.buffDomain.SetBuff(ESBuffEnumKey.Aiming);
    entity.buffDomain.SetBuff("state.can_turn");
    entity.buffDomain.EndBuffFrame();
}

// 状态退出、不会再提交下一帧时：
entity.buffDomain.ClearBuffFrame(state);
```

| 调用 | 含义 |
| --- | --- |
| `BeginBuffFrame(owner)` | 开始该来源的完整状态写入；同一 Domain 不允许嵌套帧。 |
| `SetBuff(Enum / String / Definition)` | 声明当前应存在的效果；同 Key 同帧最后一次写入为准，不叠层。 |
| `EndBuffFrame()` | 提交差异：本帧未声明的、**同一 owner** 旧效果被移除；其他来源和普通 `AddBuff` 完全不受影响。 |
| 空帧后 `EndBuffFrame()` | 该 owner 的完整清空。 |
| `CancelBuffFrame()` | 放弃尚未提交的写入，保留该 owner 上一份已提交状态；仅用于业务中途异常退出。 |
| `ClearBuffFrame(owner)` | 生命周期结束时立即清空该 owner，不必等下一次帧。 |

帧效果的生存由 `owner` 的完整集合控制，运行时以无限持续时间持有；稳定帧再次 `SetBuff` 同一个效果只是标记为仍存在，不重新申请 Tag/ValueChange、不反复触发 Apply，也不产生层数。定时、中毒、护盾等普通 Buff 仍使用 `AddBuff`。若一帧存在无效 Key 或无效配置，`EndBuffFrame()` 返回 `false` 且保留该 owner 的上一份已提交状态，避免半帧错误把旧状态清空。

## 配置规则

一个 `BuffDefinitionDataInfo` 只配置一份 `BuffSharedData`：

- 必须配置 Buff 的 EnumKey 或 StringKey；两者同时存在时必须指向同一稳定定义。
- `tags` 只填写统一 `ESTagStableReference`，不出现 Core/Extension 两套字段。
- `applyTargetTagCondition` 是对目标的读取条件；条件必须先经 Catalog 编译。
- Tag、Float Change、Permit Change、Op 都是效果内容，不是额外的 Buff 子配置类型。
- Tag 可以表达 `Status.Poisoned`、`Combat.Invulnerable` 等事实；层数、持续时间和伤害数值仍由 Buff/属性系统表达。

## 启动、运行与结束

| 阶段 | 规则 | 失败结果 |
| --- | --- | --- |
| 施加前 | 校验 Tag 配置、编译目标条件、解析 Buff Key、处理来源隔离/组冲突/叠层 | 拒绝施加，不改目标状态 |
| TryApply | 先写入 Tag 与 ValueChange，再启动 Apply Op；Apply Op 可查询自身 Buff | 任一步失败即停止已启动 Apply Op、归还已写入资源，并在当前调用结束前移出 Active 列表 |
| Active | 支持独立实例、叠层、刷新、组冲突、固定间隔/逐帧/状态时间 Tick | 单个 Tick 异常只使该 Buff 结束，不阻断同一 Entity 的其他 Buff |
| Deactivate | 先尝试停止 Apply Op、执行 Remove Op；无论 Op 是否异常，继续释放 Tag、依赖、ValueChange 和 Support | 日志记录失败；资源释放与对象池归还继续执行 |
| Pool | Runtime 清零后回收到 Domain 缓存或全局 Pool | 旧 Tag Lease 与 ValueChange Token 不得影响下一次复用 |

这里的“完整”是所有权完整，不等于任意 `ESOutputOp` 的外部副作用可以自动回滚。具有不可逆副作用的 Op 必须自己提供幂等停止或补偿逻辑。

## 性能约束

- **P0：稳态 Tick 必须 0 GC。** 本条指 Buff 已完成初始化、容量已热身、没有新增/移除/刷新 Buff，且不创建调试快照或日志时的 `Update -> TickActiveBuffs -> Tick` 调用链。
- Buff 调度层不得在该链路中新建 Buff Runtime、Tag LeaseSet、ValueChange 跟踪器、依赖列表、字符串或临时集合；不得使用 LINQ、闭包、反射、组件扫描或运行时字符串 Key 解析。
- Buff Tags 使用 `ESTagLeaseSet` 的值类型 Token 路径，不为每个配置 Tag 创建托管 Lease 对象。
- 表达式只在 `OnDirty`、`OnStackChanged`、`EveryTick` 等明确策略下刷新。
- 固定间隔 Tick 默认每帧最多追赶 4 次；配置可提高或降低上限。超过上限的完整 Tick 直接丢弃，仅保留不足一个 Tick 间隔的余量，防止卡顿后的单帧追帧雪崩。
- `ESBuffChangedLink` 只报告 Applied / Refreshed / Removed 的值快照，供 UI 与战斗日志观察；它不暴露可修改的 Buff Runtime，也不逐帧推送倒计时。
- 错误日志、调试快照、首次容量扩展属于冷路径；不能把它们放到普通 Tick 热路径。

`onTickOp`、`EveryTick` ValueChange 表达式与它们调用的业务模块也属于同一条热路径。Buff 容器不会替配置代码分配；任何 Tick 配置只要包含 `new`、LINQ、字符串拼接、装箱、临时集合或未缓存的查找，即为 P0 缺陷，禁止作为常态 Tick 配置发布。

验收不能只依赖代码阅读：必须在 Unity Player 与 IL2CPP 上对预热后的 Buff 压测场景采样 `GC Alloc`，稳定帧为 0 B 才能签收；Buff 首次施加、结束、对象池扩容、日志与调试快照不计入此指标。

## 当前验收与后续

已完成代码级收口：稳定 Key 注入、来源隔离、叠层/组冲突、Tag 条件与写入、数值/权限变化、OpSupport、表达式依赖、对象池，以及 Apply/Remove/Tick 的异常隔离与清理。

正式商业验收仍必须补齐：

1. Unity Test Runner：Remove Op 异常后 Tag/ValueChange 归零；Apply 与 Tick 失败不污染其他 Buff；对象池反复复用无残留。
2. 正式 GameCore Bake 与稳定 Key 审计。
3. Buff 存档/联机协议：仅稳定 Buff Key、来源语义与 SchemaHash；不得传裸 RuntimeKey 或托管对象引用。
4. Unity Profiler：验证 Tick 追帧上限在长时间卡顿、对象池循环和目标设备上的实际帧耗与分配。
5. Buff 调试快照：只读显示当前 Buff、层数、剩余时间、稳定 Key 与来源语义；不得让 UI 反向修改 Buff 生命周期。
