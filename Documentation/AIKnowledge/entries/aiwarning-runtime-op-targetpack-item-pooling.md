# Op TargetPack：Item 整合与池化重设保真 Knowledge

`KnowledgeId`: `es.aiwarning.runtime.op-targetpack-item-pooling.v1`  
`Authority`: `AIWarnings` + current TargetPack/Operation source  
`RouteKeys`: `aiwarnings`, `runtime`, `item`, `shot`, `pool`, `target-pack`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `28ec0723f18a03bcf46de38dba604a120c98802ac660c13d87bd8ad88d78232c`  
`SourceSetHash`: `28ec0723f18a03bcf46de38dba604a120c98802ac660c13d87bd8ad88d78232c`  
`EntryBodyHash`: `230f7364ef11843e5717faa07522b97eb23a4027a63026da1fec146baab5ffb1`  
`StaleWhen`: TargetPack 字段、Item 表达式、目标 Operation、池化租约或容量策略变化。

## 迁移说明

Warning 只保留长期约束、权限/生命周期边界和导航；本条目保存详细字段、源码映射、容量策略、使用口径、失败面和原文快照。Knowledge 不授予对象池、Runtime、Profiler 或发布权限。

## 当前事实与源码映射

- `ESRuntimeTargetPack` 已将 `userItem`、`itemMainTarget`、`targetItems` 作为正式字段，与 `userEntity`、`entityMainTarget`、`targetEntities` 并列；`ItemExpressionSource` 和目标 Operation 直接读取这些字段。
- `OnResetAsPoolable` 调用 `ResetAllFields` 与 `ResetAllExtras`，清空 Entity/Item 引用和列表、`runtimeFloat`/`runtimeBool`、扩展槽位，并递增 `Version`。飞行物、门、陷阱、区域等高频复用场景不得遗留上一租期引用。
- `TryCopySnapshotFrom` 先检查 `IsRecycled`，再复制原生字段和列表；长期 Owner 仍必须联合校验 Pack 引用与 `Version`，不能单独依赖 `IsRecycled`。
- `EnsureListCapacity(entityTargetCapacity, itemTargetCapacity)` 用于初始化/预热容量；普通路径使用 `List<Entity>(8)` 与 `List<Item>(8)`，高频 Tick 不应依赖临时扩容。
- `extras` 默认关闭；需要调试/兼容扩展时必须显式 `EnableExtras(capacity)`，高频业务优先使用原生 Entity/Item 字段和轻量槽位。

## 生命周期与职责边界

`Support` 表示谁持有这段生命周期，`TargetPack` 表示本次 Operation 作用对象。创建层保存租约并归还 Pack，Operation 只借用；TargetPack 不负责对象池生成/回收，不得混入伤害、VFX 或 Pool 控制。Item OnHit 可将 Item 写入 `userItem`、命中 Entity 写入 `entityMainTarget`；门交互可将玩家写入 `userEntity`、门写入 `itemMainTarget`。

## 失败面与验证边界

重点防止 Item 只进 `extras`、回池字段未清空、租期错配、列表在热路径扩容和把 Shot Tick 当作每帧执行入口。当前证据为源码静态读取；Unity、Runtime、Profiler、Player、IL2CPP 与发布行为仍未验证。

## 原 Warning 保真快照（HEAD）

以下保留迁移前 Warning 的完整文本；其 HEAD SHA-256 为 `99110b128954dbaf6c73125e76426290974777d90da6601e85561d0e1e2256de`。

```markdown
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
- 普通业务不得访问 TargetPack Pool 或直接归还 Pack。创建层保存租用版本并负责回收，Operation 只借用。
- `IsRecycled` 不能识别对象是否已进入下一租期；长期 Owner 必须用 Pack 引用与 `Version` 联合校验。
- 不要让高频 Shot Tick 每帧跑 Op。
- 不要把伤害、VFX、Pool 混进 Item 目标包。
```

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Item与Shot物理（ItemShotPhysics）/OpTargetPack_Item整合与池化重设_AI协作警告.md` (`50e7e43b3a08f39c38fae5a011cd7f798324a376e2cffac0902ef698341ced5e`)
- `Assets/Scripts/ESLogic/Runtime/Operation/Targets/ESRuntimeTargetPack.cs` (`a6acbca526fccefebe96e9762c04a658abb8369efed8e4dc2fe649b5684a4c12`)
- `Assets/Scripts/ESLogic/Runtime/Operation/ExpressionSources/ItemExpressionSource.cs` (`ccc02e2f422c58179923eaeb55c6947815c242ae28ecb24776a40d2220310588`)
- `Assets/Scripts/ESLogic/Runtime/Operation/Operations/02_Targeting/OpTargeting_RuntimeTarget.cs` (`32a2b4aa5105686f7ff87069d32823ff33646c32dd28e4499efd378c146c97a0`)
- `Assets/Scripts/ESLogic/Runtime/Operation/Operations/07_MovementPhysics/OpMovementPhysics.cs` (`3ee5eb8d9f2382392952a6e4f90aee7c5fa91e4800736a5e6b532d29a57adf95`)
- `Assets/Scripts/ESLogic/Runtime/Operation/Expressions/03_GameObject/GameObjectExpressions.cs` (`5306e9e1eebc48aacc896fdbd447d089afbb01ffbbeb75ad44120267488e0b22`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-runtime-op-targetpack-item-pooling.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Scripts/ESLogic/Runtime/Operation/Targets/ESRuntimeTargetPack.cs`
