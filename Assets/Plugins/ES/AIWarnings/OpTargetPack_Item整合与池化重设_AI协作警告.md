# Op TargetPack：Item 整合与池化重设警告

## 当前事实

`ESRuntimeTargetPack` 已经把 `Item` 接入为正式目标，不要再把 Item 临时塞进 `extras` 当主路径使用。

源码位置：

```text
Assets/Scripts/ESLogic/Runtime/Operation/Targets/ESRuntimeTargetPack.cs
Assets/Scripts/ESLogic/Runtime/Operation/ExpressionSources/ItemExpressionSource.cs
Assets/Scripts/ESLogic/Runtime/Operation/Operations/02_Targeting/OpTargeting_RuntimeTarget.cs
Assets/Scripts/ESLogic/Runtime/Operation/Operations/07_MovementPhysics/OpMovementPhysics.cs
Assets/Scripts/ESLogic/Runtime/Operation/Expressions/03_GameObject/GameObjectExpressions.cs
```

## 新增 Item 字段

```text
userItem          Item 使用者/发起者
itemMainTarget    主 Item 目标
targetItems       多 Item 目标列表
```

对应 Entity 旧字段仍保留：

```text
userEntity
entityMainTarget
targetEntities
```

## 池化重设要求

`ESRuntimeTargetPack` 是池化对象。回池时必须清空：

```text
userEntity
entityMainTarget
targetEntities
userItem
itemMainTarget
targetItems
runtimeFloat
runtimeBool
extras
recycleToken
recycleRequested
```

当前 `OnResetAsPoolable -> ResetAllFields / ResetAllExtras` 已覆盖 Item 字段和 Item 列表。

警告：飞行物、门、陷阱、区域这类 Item 会频繁复用 TargetPack。如果不清空 `itemMainTarget / targetItems`，下一次 Op 可能误操作上一轮 Item。

## GC 口径

当前采用实用方案：

```text
targetEntities = List<Entity>(8)
targetItems    = List<Item>(8)
extras         默认关闭，不在普通路径分配
```

如果某个玩法明确需要更多目标，应在初始化或预热阶段调用：

```text
EnsureListCapacity(entityTargetCapacity, itemTargetCapacity)
```

不要在高频 Tick 中依赖 List 临时扩容。

`extras` 只允许低频调试或兼容场景使用。默认 `AddExtra` 不会启用 extras；确实需要时必须先显式：

```text
EnableExtras(capacity)
```

高频逻辑不要使用 `extras`。优先使用原生字段：

```text
userEntity / entityMainTarget / targetEntities
userItem / itemMainTarget / targetItems
runtimeFloat / runtimeBool
```

## 使用口径

```text
Support = 谁持有这段生命周期
TargetPack = 这次 Op 要作用到谁
```

示例：

```text
飞行物 Item OnHit
  hostSupport = 飞行物 ItemSupport
  targetPack.userItem = 飞行物
  targetPack.entityMainTarget = 被命中的 Entity
```

```text
门交互
  hostSupport = 门 ItemSupport
  targetPack.userEntity = 玩家
  targetPack.itemMainTarget = 门
```

## 不要做的事

- 不要把 Item 只放进 `extras`。
- 不要让 TargetPack 负责对象池生成/回收，它只负责清引用。
- 不要让高频 Shot Tick 每帧跑 Op。
- 不要把伤害、VFX、Pool 混进 Item 目标包。
