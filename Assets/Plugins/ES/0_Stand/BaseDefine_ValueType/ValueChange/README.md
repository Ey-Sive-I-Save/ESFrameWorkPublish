# ValueChange

`ValueChange` 是可撤销的运行期修正器集合。它的完整流转为：

```text
Buff 配置 / 代码创建
        -> 规范 Key 在应用时路由为固定 AttributeId 或自定义稀疏 Key
        -> 当前实体实现由 EntityBuffDomain 创建 EffectLease 并取得 OwnerId
        -> 每个运行期实例持有自己的 Token
        -> ESFloatValueChangeSet 或 ESPermitSet 裁决
        -> 角色超级属性表定义基础值权威
        -> KCC 通过固定数组槽位 / 业务通过稀疏 Key 读取最终值
        -> Buff 移除或 Tracker.ReleaseAll 释放 Token
```

## 当前验收状态

| 范围 | 结论 |
| --- | --- |
| Tag Lease 现有链路 | 通过 |
| Lease 与批量操作防重入 | 通过 |
| P0 构建门禁与 KCC 固定槽位热路径 | 通过 |
| 相关程序集编译 | 0 warning、0 error |
| Unity NUnit 实际运行 | 待 Unity Test Runner 执行 |

本次不签收“全域属性底层标准”。当前状态是“P0 构建门禁与 KCC 热路径代码闭环通过，全域属性系统仍处于迁移阶段”。后续必须拆出 `EntityAttributeRuntime`、将装备/区域/天赋/AI 迁移到 Lease 入口、收窄裸 Set/Token API，并在 Unity Test Runner 中执行回归用例。完成前，禁止据此扩大业务层对裸 Set、Token 或 `ownerId` 的依赖。

## Float

`ESFloatValueChangeSet` 的计算顺序固定为：`Override 或 Base` -> `Add` -> `AddPercent` -> `Multiply` -> `Min` 下限 -> `Max` 上限。

多个 `Override` 按 `priority` 高者生效；同优先级时，后加入者生效。`Min` 取最高下限，`Max` 取最低上限。

属性定义的 `minValue` / `maxValue` 是最终不可撤销边界，在全部运行期修正后执行；运行期 `Min` / `Max` 不能突破它。定义中的 `formula` 当前不支持，非空配置会被 Catalog 拒绝，不能假定它会在运行期计算。

Float 的基础值、`Add` 与 `Update` 一律拒绝 `NaN`、正无穷和负无穷。聚合多个有限修正器时若发生溢出，结算器会饱和到有限的 `float.MinValue` / `float.MaxValue`，不会把非有限结果发布给 KCC 或其他消费者。表达式产生非有限值时，现有 Token 保持原值；首次应用则不会创建 Modifier Token。

持续 ValueChange 只接受确定性 Expression。`ESRandomRangeFloatExpression` 以及任何包含它的组合表达式会被 Float/Permit Binding 和 Buff 拒绝，不会创建 Token 或改变现有值。随机结果应由一次性 Op 生成后作为稳定数值写入修正器；因此网络权威路径不会把进程内随机性带入持续属性结算。

```csharp
ESFloatValueChangeSet moveSpeed = buffDomain.GetFloatStat("MoveSpeed", 5f);
ESFloatValueChangeTracker tracker = new ESFloatValueChangeTracker(moveSpeed, ownerId, sourceId);
ESValueChangeToken token = tracker.Add(ESFloatValueChangeOp.AddPercent, 0.2f);

float finalSpeed = buffDomain.GetFloatStatValue("MoveSpeed", 5f);
tracker.Release(token); // 或在生命周期结束时 tracker.ReleaseAll();
```

## Permit

`ESPermitSet` 解析 `ESPermitLaw`。`Ignore` 不作决定；硬规则优先于软规则；同级别规则再按 `priority` 和后加入顺序决定。`Result` 会给出最终值及胜出的规则元数据。

```csharp
bool mayJump = buffDomain.GetPermitValue("MayJump", true);
ESPermitLawResult reason = buffDomain.GetPermitResult("MayJump", true);
```

## 生命周期与刷新

- Token 同时记录创建它的 Set 身份。把来自另一 Set 的 Token 传给 `Update`、`Release` 或 `Contains` 会被拒绝；`Clear()` 会提升 Token 版本，旧 Token 不能误操作清空后的新条目。
- `Tracker` 有活动 Token 时不可绑定到另一个 Set，必须先 `ReleaseAll()`。
- `Changed` 和 `Revision` 用于通知拉取式消费者刷新缓存；读取 `Value` 或 `Result` 时会懒计算。
- 对同一个 Set 的多次写入可用 `using (set.BeginBatch())` 合并为一次 `Changed` 通知。按 Owner/Source 批量释放或启停也会在内部使用该批次，因此监听器只能看到完成后的集合状态；回调中创建的新修正不会被旧批量操作误删。每次真实输入变更仍会递增 `Revision`，因此 Revision 可作为精确的缓存失效版本。
- `ES*ValueChangeExpressionBinding` 保存单个运行期 Token，适用于一个绑定实例对应一个运行期拥有者。
- Buff 配置对象是共享数据，因此 `ESActiveBuffRuntime` 不使用配置对象上的 Token，而是为每个活动 Buff 维护独立 Token。配置中的刷新时机默认 `OnApplyOnly`；动态表达式可选 `OnStackChanged`、`OnDirty`、`EveryTick`，或由返回的活动 Buff 调用 `RefreshValueChanges()` 手动刷新。
- `OnDirty` 是事件驱动模式：当前 `ESContextFloatExpression` 与 `ESContextBoolExpression` 在首次求值时自动订阅已存在的同名 Context 键，键变化只标脏，Buff Tick 在标脏后才重新计算。Buff 销毁、重施加或回收时会自动解除订阅并恢复 Context 原有 Link 设置。其他表达式依赖（实体状态、Tag、外部服务等）必须由其所有者调用活动 Buff 的 `MarkValueChangesDirty()`；未声明依赖不能被误判为自动订阅。`EveryTick` 仅保留给时间、距离、随机等确实逐帧变化的表达式。

`EntityBuffDomain.ClearValueChanges()` 会清空领域内所有 Set 并使已有 Token 失效，适用于领域销毁或显式重置。有活动 `EffectLease` 时它会拒绝执行；必须先释放 Lease，避免活跃 Buff 持有失效 Token。清理/重绑定期间也禁止创建新 Lease，防止 `Changed` 回调在清理枚举中插入新的运行期所有权。

`ownerId` / `sourceId` 的 `0` 表示未归属，不会建立批量回收索引；`ReleaseAllByOwner(0)` 与 `ReleaseAllBySource(0)` 都不会释放任何修正器。需要批量清理时必须传入非零的领域内拥有者/来源 ID。

## 底层边界

ValueChange 是 ES 的可撤销修正器内核，不是 Buff 专属系统，也不持有任何角色、资源或配置状态。当前实体实现的物理所有者仍是 `EntityBuffDomain`，尚未拆出 `EntityAttributeRuntime`：

```text
EntityBuffDomain: 属性 ID 路由、基础值缓存、固定/稀疏槽位、EffectLease 所有权
        -> ValueChange: Token、Float/Permit 裁决、缓存、批处理
            -> Buff: 创建、更新、释放自己的效果
```

`ESEffectLease` 是领域级 API 的零分配生命周期契约。`EntityBuffDomain` 持有实际 Binding/Token；Lease 只保存领域 Slot 与 Generation，重复或过期释放由领域拒绝。释放期间槽位不会复用，防止通知回调内的新效果与旧 OwnerId 混淆。

`ESEffectInstanceId` 目前只有值类型契约，尚未有生产所有者或诊断链路。它不能作为玩法、存档、网络或资产标识使用；在接入真实 Effect API 前，不得据此扩展业务依赖。

装备、天赋、区域和 AI 尚未迁移到 Lease 入口；在它们具备各自的生命周期所有者前，不应扩大对裸 Set、Token 或 `ownerId` 的直接依赖。

## 角色超级属性表

`Entity.superAttributes` 是角色的属性目录和基础值权威层；它不保存 Token、不重复做 ValueChange 聚合。内置目录覆盖地面/空中速度、地面响应、跳跃、重力倍率、下蹲倍率、转向、根运动，以及 `Move`、`Jump`、`Rotate` 三项 Permit。默认基础值直接跟随 `EntityKCCData`；在表中勾选“覆盖基础值”后，表内值成为该角色的基础值。

定义表提供的是默认基础值/默认许可。业务通过 `SetFloatStatBaseValue`、`SetCharacterFloatStatBaseValue`、`SetPermitFallbackValue` 或 `SetCharacterPermitFallbackValue` 显式写入的运行期基础值优先，并且不属于 ValueChange 修正器。固定槽位的显式基础值写入紧凑数组，不会创建 Set；稀疏值只为实际写入过的 Key 保存覆盖项。禁用整张属性表时，角色不使用其定义和默认值，内置固定槽位退回调用方的默认值。

固定角色属性使用 `ESCharacterFloatAttributeId` 和 `ESCharacterPermitAttributeId`。KCC 只传递这些枚举，因此其热路径不进行字符串或 Dictionary 查询；未被修正的槽位不会创建 `ESFloatValueChangeSet` / `ESPermitSet`。Buff 配置仍可使用 `ESCharacterSuperAttributeKeys` 字符串常量，Buff 应用时会一次性路由到相同的固定槽位。

非固定内容（装备词条、附魔、模组属性）继续使用自定义 Key，并只在首次实际写入时创建稀疏 ValueChange Set。例如普通物品不拥有完整的附魔数组；装备上的 `Item.Enchantment.Sharpness` 才会产生一个实际修正器。

配置层会拒绝重复 Key、Float/Permit 跨类型 Key。`Character.*` 的内置 Key 不能被另一种值类型静默注册。

Buff 的 `statKey` / `permitKey` 使用 `ESCharacterSuperAttributeKeys` 中的常量即可直接影响 KCC 主运动路径。例如：

```csharp
binding.statKey = ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed;
binding.change.op = ESFloatValueChangeOp.AddPercent;
binding.change.value.SetDirect(0.25f);
```

表也支持任意自定义 Key，供生命、攻击、防御等后续业务模块注册并通过 `EntityBuffDomain` 读取；Buff 不持有或篡改这些基础值。

## 性能边界

- KCC 固定属性读取：数组索引、标量计算、按需读取已存在的 Resolver；稳定后不产生每帧 GC。
- 已热身 Buff 的激活/移除：Owner/Source Token 索引列表在 Set 内回收复用；不创建新的列表对象。首次写入或超过历史并发容量时，仍可能创建 Resolver、索引列表或触发容器扩容，应在加载期预热。
- 自定义属性：Dictionary 是配置/事件路径，不应进入每帧角色运动路径。
- Permit 的硬规则、优先级与后加入顺序统一由 `ESPermitLawResolver` 定义，`ESPermitSet` 调用同一比较规则。
