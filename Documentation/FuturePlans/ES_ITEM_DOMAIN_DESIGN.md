# ES 物品域设计与聚合方案

状态：`Proposed`。设计边界已完成，尚未新增生产运行时代码；不得将本文描述为现有背包、装备、掉落或商店功能。
最后核对：2026-08-03（现有 `Item`、`ItemDataInfo`、Entity、Tag、ValueChange、对象池、资源与存档边界）。
适用源码入口：未来 `ESGameManager.Items` / `ESItemModule`；现有 `Assets/Scripts/ESLogic/Runtime/Item`、`ItemDataInfo`、`Entity`、`ESTagCollection`、ValueChange、GameObject Pool。

## 1. 目标与一句话模型

建立一个可选、单入口的 **ES 物品域**，统一协调“物品实例如何创建、跨持有者移动、装备、拾取、保存和销毁”。Container 的实际状态始终属于自己的持有者，不属于 GameManager。

它适用于玩家、怪物、NPC、箱子、商店和任务奖励，但不把背包、掉落或装备逻辑塞进 `Entity`，也不要求每个游戏或每个角色启用完整背包。

```text
Loot / Reward 决定产生什么
  -> ESGameManager.Items 决定如何创建、持有、移动和销毁实例
    -> Pickup 只表现世界拾取物
      -> Entity 只接收已经提交的装备逻辑/表现投影
```

对项目开发者只暴露直白的物品动作：

```csharp
ESGameManager.Items.Add(...);
ESGameManager.Items.Move(...);
ESGameManager.Items.Equip(...);
ESGameManager.Items.Unequip(...);
ESGameManager.Items.Pickup(...);
ESGameManager.Items.Buy(...);
ESGameManager.Items.Save(...);
ESGameManager.Items.Load(...);
```

事务、锁、Revision、幂等收据和补偿是内部机制，不要求玩法代码手动拼装。

## 2. 非目标与硬边界

本方案不是第二套角色、战斗、奖励、寻路或网络系统。

| 主题 | 权威系统 | 物品域的边界 |
| --- | --- | --- |
| 角色运动、状态、Animator、KCC | `Entity` / State / KCC | 装备不能直接写 KCC、Animator 或 Transform |
| 武器换父节点 | `EntityBasicCombatModule` | 装备表现只请求其正式 API；不得自行 `SetParent` 到手骨 |
| Tag、属性、权限 | `ESTagCollection` / ValueChange / Permit | 装备投影只持有并释放自己的 Lease/Token |
| 掉落概率、保底、奖励上下文 | Loot / Reward 域 | 物品域只接收确定的掉落结果 |
| 商店定价、刷新、无限供应 | Shop / Offer 域 | 只有唯一、可被买走的商品才成为真实物品实例 |
| 任务进度 | Quest / Story 域 | 仅可持有、转移、丢弃的任务物品进入 Container |
| 网络权威、回滚、反作弊 | 网络/服务端层 | 物品域提供幂等事务和快照边界，不假装完成网络方案 |

禁止事项：

- 禁止新建 `PlayerInventory`、`MonsterInventory` 等平行系统。
- 禁止把持有数据、背包格、装备栏直接序列化进 `Entity`。
- 禁止让 UI、世界拾取物、角色表现直接改 Container 或 Loadout。
- 禁止用 `Item` GameObject 的存在与否表达库存所有权。
- 禁止为每个背包格子、物品实例或装备槽创建 MonoBehaviour。

## 3. 聚合方式：一个模块协调，不接管持有者状态

实现层只有一个可选 GameManager 模块：`ESItemModule`。对外静态门面为 `ESGameManager.Items`；“ES 物品域”是这个模块覆盖的业务范围，不再额外创建 `ESInventoryDomain`、`ESEquipmentDomain` 等顶层模块。

`ESItemModule` **不是全局背包仓库**。它不持有玩家、怪物、箱子或商店的 Container 数据真相；它只解析定义、定位已经存在的持有者状态、协调跨 Container 事务、分配实例/事务身份，并维护有生命周期范围的 Receipt 和世界 Pickup 索引。

```text
ESGameManager
  -> ESItemModule                 // 唯一物品协调模块，可按项目启用
      -> Data 解析 / ID / Transaction / Receipt / Pickup 索引

玩家存档、怪物 Spawn Runtime、箱子世界状态、商店状态
  -> 各自持有 ESItemHolderState
      -> 自己的 Backpack / Storage Container
      -> 可选 ESEquipmentLoadout
      -> 自己的 ItemInstance
```

普通开发者主要理解四个词即可：**物品、背包、装备、拾取**。`Transaction`、`Revision`、`Receipt` 只出现在诊断、存档、网络或高级扩展代码中。

### 3.1 现有 `Item` 的定位

现有 `Runtime/Item/Item.cs` 是一个带 Domain 的世界 Core，可用于交互物、投射物、门、陷阱等内容；它不是未来库存实例的存储实体。

- 保留 `ItemDataInfo` 作为物品定义资产的既有入口；后续在其上增加“可堆叠、装备槽、耐久/随机状态模板、世界拾取表现”等只读配置，禁止复制第二套定义资产。
- 未来 `ESItemInstance` 是纯运行时数据，不带 GameObject、不参加 Unity Update。
- 未来 `ESWorldItem` 只挂在可拾取世界 Prefab 上，是 `ESItemInstance` 的表现代理；不得把现有 `Item` 的投射物职责强行迁入它。
- 已有 `Item` 与 `ItemDataInfo` 不作目录整理或兼容性大迁移；首个切片只新增物品域代码并以桥接方式使用现有定义。

## 4. 核心数据模型

### 4.1 定义、实例、持有者与容器

```text
ItemDataInfo（只读定义，稳定 Key）
  -> 被 ESItemInstance 引用

各持有者自己的 ESItemHolderState
  -> ESItemContainer（Backpack / Storage）
  -> ESEquipmentLoadout（可选）
  -> ESItemInstance
```

| 对象 | 必备内容 | 说明 |
| --- | --- | --- |
| `ItemDataInfo` | 稳定定义 Key、堆叠规则、装备规则、表现引用、基础数值/Tag | 内容资产；不得保存运行时数量、归属或 UID |
| `ESItemInstance` | `ItemInstanceId`、定义 Key、数量、可保存自定义状态、StackKey | 纯数据；同定义的两份掉落仍是不同实例 |
| `ESItemHolderState` | HolderRef、自己拥有的 Container 集合、所属生命周期 | 由玩家/怪物/箱子/商店对应运行时状态持有，不由 GameManager 持有 |
| `ESItemHolderRef` | `HolderKind`、`StableId`、`RuntimeInstanceId`、`Generation` | 强类型定位身份，禁止裸 `int ownerId` |
| `ESItemContainer` | `ContainerId`、Holder、容量/槽规则、实例列表、`Revision` | 一个持有者可有多个 Container |
| `ESEquipmentLoadout` | Holder、装备槽、已装备 `ItemInstanceId`、`Revision` | 独立装备聚合；只负责槽位合法性和装备事实，不持有 Entity Lease 或表现对象 |
| `ESEquipmentProjectionModule` | 目标 Entity、已应用 Loadout Revision、逻辑 Lease、表现句柄 | 可重建的角色侧投影；根据已提交 Loadout 应用/撤销逻辑与表现 |

`ContainerId` 独立于 Holder：一个玩家可以同时拥有背包、仓库和任务容器；一个怪物可只有背包；一个箱子只有普通容器。装备栏不是普通 Container，而是可选的 `ESEquipmentLoadout`。模块为跨对象事务短暂定位这些状态，不能成为它们的第二份或唯一副本。

每个 `ESItemContainer` 由一个 `ContainerConfig` 决定容量、过滤和布局，并在内部选择一种 `IItemStorageStrategy`：`ListStorageStrategy`、`FixedSlotStorageStrategy` 或未来的 `GridStorageStrategy`。策略只负责“能否放入、放在哪里、如何枚举”；它不负责装备、商店、掉落、角色属性或 UI。新增网格策略不得修改 ItemInstance、Loadout、Transaction 或 Pickup 的公开契约。

`ESItemHolderRef` 的身份来源固定如下：

| HolderKind | 稳定身份 | 运行时身份 |
| --- | --- | --- |
| Player | 账号或角色稳定 ID | 当前角色实例 ID + Generation |
| Monster | SpawnId；持久 Boss 可加世界稳定 ID | Entity Instance ID + Generation |
| NPC | NPC 稳定 ID | 当前场景实例 ID + Generation |
| Chest | StableWorldIdentity | 场景实例 ID + Generation |
| Shop | ShopId | 当前会话/场景实例 ID + Generation |

调用方通常不手填这些字段：`Items.Equip(entity, itemId)`、`Items.Open(chest)` 等入口内部负责解析 HolderRef。强类型引用主要用于防止跨对象、回池复用和延迟回调串写。

### 4.2 实例与堆叠规则

- `ItemInstanceId` 在存档、网络和交易边界必须稳定；不可用 List 索引、Unity InstanceID 或 RuntimeKey 替代。
- 堆叠只发生在定义允许、StackKey 相同且实例状态兼容时。耐久、随机词条、绑定归属、剩余冷却等不同的实例不得错误合并。
- 数量变化、拆分、合并、转移、装备、消耗和销毁只能经 `ESItemModule` 入口提交。
- 容器的 `Revision` 每次成功写入递增；它用于 UI 刷新、存档一致性、网络条件写入和投影重建，不作为业务身份。

## 5. 唯一写入：内部事务

Container 和 `ESEquipmentLoadout` 不主动依赖 Transaction；它们只保存各自聚合状态。`ESEquipmentProjectionModule` 只消费已提交的 Loadout Revision，不能回写装备事实。物品域内部的 `ESItemTransactionModule` 是唯一修改协调者，不是额外挂到 GameManager 的第二个业务模块：

```text
Transaction
  -> 解析 ItemDataInfo / 装备槽规则
  -> 校验并锁定 Container + Loadout
  -> 原子写入 Container + ESEquipmentLoadout
  -> 请求 ESEquipmentProjectionModule 同步
  -> 发布带 Revision 的结果
  -> 异步刷新装备表现
```

### 5.1 标准提交顺序

1. 解析定义和由各持有者提供的源/目标 Container / Loadout；验证 `Revision`、容量、槽位、数量、绑定、权限和可达性。
2. 按稳定 `ContainerId` 与 Loadout ID 的全局顺序锁定所有参与聚合；失败立即释放，禁止动态锁顺序。
3. 若目标 Entity/Projection 已就绪，预检可补偿的逻辑投影参与者，例如属性、Tag、Permit、技能授权 Lease；若角色未生成，则标记为 Deferred，不能把“暂时没有表现目标”误判为装备失败。
4. 原子提交 Container 与 `ESEquipmentLoadout`。装备后实例从源 Container 移入 Loadout；不保留“仍在背包格但被预留”的双重所有权。
5. Projection 就绪时提交逻辑 Lease。若该同步逻辑提交失败，在锁仍持有且结果尚未发布时恢复本次 Container/Loadout 修改并释放已创建 Lease；Deferred Projection 则在角色生成/重建时按 Loadout Revision 补做。
6. 写入 `TransactionId`/Receipt，递增 Revision，发布成功结果。
7. `ESEquipmentProjectionModule` 异步刷新模型、武器外观、特效或图标。表现失败只显示占位/旧表现并重试；绝不能回滚已成功的库存事务。

任何异常都不得在步骤 4--6 留下“背包已经少了物品、装备效果却未生效”的半提交状态。

### 5.2 结果与幂等

每个公开写入入口返回简单的 `ESItemResult`：成功标记、结果码、相关 Item/Container ID、变更 Revision 和有限诊断。高级调用可传稳定 `TransactionId`。

- 相同 `TransactionId` 的重试必须返回第一次的确定结果，而不是再次移动或再次扣除物品。
- 事务收据保留范围由存档/网络场景决定；至少覆盖当前会话及所有可能重放的请求窗口。
- 不允许 UI 以“先本地扣除、失败后猜测补回”的方式绕过事务结果。

## 6. 装备投影：逻辑优先，表现可重建

```text
ESEquipmentLoadout 已提交变更
  -> ESEquipmentProjectionModule
    -> Entity Tags / ValueChange / Permit / Skill 授权（同步逻辑）
    -> Combat 正式装备 API（武器意图）
    -> TransformMapping / WeaponBinding（仅由既有权威执行实际挂载）
    -> 异步模型、武器外观、特效（表现）
```

- 每个已装备实例的逻辑 Lease 集属于 `ESEquipmentProjectionModule`；卸下、替换、Entity 回池/销毁时精确释放本实例贡献，但不得修改 Loadout 事实。
- Projection 可以在 Entity 未生成时不存在，也可在对象池复用、场景重载后重建；它只需按当前 `ESEquipmentLoadout` + Revision 恢复逻辑与表现。
- 逻辑投影必须可补偿；表现投影必须可重试、可降级。外观加载失败不能回滚 Container 或 Loadout。

### 6.1 Projection 实现门禁

- 每次 Apply、异步完成和释放都必须校验 `OwnerId + LoadoutRevision + EntityGeneration`。旧角色、旧 Loadout 或对象池复用前发出的回调不得修改当前 Entity。
- 同一 Loadout Revision 的重复 Apply 必须幂等；切换到新 Revision 时，先精确撤销旧 Revision 的 Lease/句柄，再应用新 Revision，不能累积贡献。
- Projection 明确维护两个独立结果：`LogicAppliedRevision` 与 `VisualAppliedRevision`。逻辑 Lease 成功但外观仍在加载是正常中间态；外观失败只进入视觉降级/重试状态，绝不能把已提交装备标为失败。
- 角色、武器、Inventory UI 均不得直接对 Humanoid 骨骼调用 `SetParent`。现有 Combat 是武器换父节点权威；`WeaponSocket`、`EntityWeaponBinding`、双手 IK 的正式契约仍由角色/武器系统维护。
- 普通物品、材料和任务物品没有必要创建任何 Entity 投影。

## 7. 世界拾取、奖励与死亡掉落

### 7.1 世界拾取物

`ESWorldItem` 是唯一需要挂在世界拾取 Prefab 上的小型代理：保存当前 `ItemInstanceId`、外观状态和 Claim 信息，向 `ESGameManager.Items.Pickup(...)` 发起请求。

```text
玩家请求拾取
  -> ESWorldItem 仅提交请求
  -> Items 校验 Claim / Holder / 距离 / 容量
  -> Transaction：世界 ItemInstance -> 玩家目标 Container
  -> 成功后才回收 ESWorldItem
```

世界物体不是库存权威；回池、销毁、重复碰撞或网络重试都不能复制实例。Claim/Reservation 必须绑定持有者身份和有限期限，避免多个玩家/AI 同时拾取。

### 7.2 掉落边界

```text
DeathEventId 或 LootReceiptId
  -> Loot / Reward 权威计算
  -> 确定 DropResult
  -> Items.CreateDrop(receipt, result)
  -> 创建 ESItemInstance
  -> Pickup 投影为 ESWorldItem（若该掉落需要落地）
```

- Loot/Reward 决定权重、保底、随机种子、奖励上下文和“产生什么”。
- Item 模块决定实例 UID、归属、容器进入、世界投影、拾取和销毁。
- `DeathEventId` / `LootReceiptId` 是幂等边界：死亡回调重放、断线恢复或重复事件不能生成双份掉落。
- 掉落可以直接进入奖励者 Container，不必都生成地面对象；是否落地是 Reward 结果的一部分。

## 8. 玩家、怪物、NPC、箱子与商店

所有单位共享 Holder / Container / `ESEquipmentLoadout` / Transaction 契约，不建立 `PlayerInventory`、`MonsterInventory` 平行代码。

| 对象 | 默认配置 | 何时升级 |
| --- | --- | --- |
| 玩家 | 背包 + `ESEquipmentLoadout`；可保存 | 仓库、任务容器、钱包等按产品加入 |
| 普通怪 | 只有 Loot 配置，不创建 Container | 需要持有真实可掉落物时再创建 |
| 装备型怪物 | 仅 `ESEquipmentLoadout` | 需要偷窃、缴械、持久化或库存操作时加背包 |
| Boss / 持久 NPC | 背包 + `ESEquipmentLoadout` | 使用世界稳定 ID 和存档快照 |
| 箱子 | 一个普通 Container | 需要刷新/锁定/任务规则由外层对象提供 |
| 商店 | Offer/报价模型，不默认真实库存 | 唯一装备、可卖空商品才使用真实 Container |

怪物死亡永远通过 Loot/Reward 产生掉落；装备型怪物的“掉落已穿装备”则由 Reward 读取其 `ESEquipmentLoadout` 中的实例并生成确定 DropResult，不能重新按概率伪造第二份。

## 9. 存档、加载与网络边界

### 9.1 存档快照

快照必须只保存稳定数据：

- SchemaVersion；
- Holder / Container / ItemInstance 的稳定 ID；
- 定义稳定 Key、数量、实例自定义状态、装备槽、Revision；
- 尚需防重放的 Transaction Receipt / Loot Receipt 范围；
- 世界拾取物的实例 ID、位置、归属/Claim 状态（仅产品需要持久化时）。

禁止保存 Unity Object 引用、Dictionary RuntimeKey、List 下标、Tag Count、对象池句柄或 Cinemachine/Prefab 运行时引用。

加载顺序固定为：恢复实例与 Container -> 校验/迁移定义 -> 恢复 `ESEquipmentLoadout` -> 重建逻辑投影 -> 异步刷新表现 -> 恢复世界拾取物。加载后必须以 Container/Loadout Revision 为事实来源重建 UI 和外观，不能信任旧 UI 或旧 GameObject。

### 9.2 网络准备

本轮不实现网络同步。未来联机时，服务器是 Transaction、DropResult 和 Receipt 的权威；客户端只能提交意图、等待结果，并以 Revision/Receipt 校正本地预测。`Generation` 防止回池后的旧消息修改新对象，但不是网络安全机制。

## 10. 文件夹与资产策略

不移动现有 `Runtime/Item`；新物品域采用复数 `Items`，避免与现有世界 `Item` Core 混淆。

```text
Assets/Scripts/ESLogic/Runtime/
├─ GameManager/Modules/Runtime/
│  └─ MODULE_ESItemModule.cs             // 唯一 GameManager 模块：ESGameManager.Items
└─ Items/
   ├─ API/                               // 简单公开请求、结果、HolderRef、ID 值类型
   ├─ Data/                              // ItemDataInfo 的物品域扩展、装备槽规则
   ├─ Inventory/                         // HolderState 所拥有的 Container、Config、StorageStrategy、堆叠与移动规则
   ├─ Equipment/                         // ESEquipmentLoadout（事实）与 ESEquipmentProjectionModule（Entity 投影）
   ├─ Pickup/                            // ESWorldItem、Claim、世界拾取请求
   ├─ Save/                              // 快照、SchemaVersion、迁移、重建入口
   └─ Internal/                          // Transaction、锁、Receipt、Revision、池化辅助

Assets/Scripts/ESLogic/Editor/Items/
├─ ItemDataInfoInspector.cs              // 定义、堆叠和装备规则检查
├─ ItemDomainAuditWindow.cs              // Key、槽位、投影、掉落引用审计
└─ ItemDomainTestSceneBuilder.cs         // 仅测试场景构建；不进入运行时

Assets/ESNormalAssets/
├─ Data/Items/
│  ├─ Definitions/                       // ItemDataInfo 资产
│  └─ EquipmentRules/                    // 共享装备槽/兼容规则（若产品需要）
└─ Items/World/                          // ESWorldItem 拾取 Prefab 与纯表现资源
```

Loot Table、奖励保底、Shop Offer、Quest Item 规则分别保留在未来的 `Reward`、`Shop`、`Quest` 数据目录；禁止复制进 `Data/Items`。

目录中的 `API`、`Data`、`Inventory`、`Equipment`、`Pickup`、`Save` 都是用户可理解的常用词。`Internal` 仅收纳实现细节，避免把每一种内部机制都伪装成独立“系统模块”。

## 11. 与 ES 既有框架的单向聚合

| ES 系统 | 物品域如何使用 | 禁止反向耦合 |
| --- | --- | --- |
| GameManager | 可选登记 `ESItemModule`，缓存 `ESGameManager.Items`；只协调定义、事务、ID、Receipt 和 Pickup 索引 | 不拥有各 Holder 的 Container 真相；Entity、UI、Pickup 不得自行构造第二个 ItemDomain |
| GameCore / RuntimeData | 解析 `ItemDataInfo` 稳定 Key 与定义 | 实例不得保存裸 RuntimeKey |
| Entity | 装备结果投影属性、Tag、Permit、武器意图 | Entity 不拥有 Inventory 实现，不随机掉落 |
| Tag / ValueChange / Op | 每件装备只申请并释放自己的 Lease/Token | 不复制第二套数值/标签聚合器 |
| Pool | 仅池化世界 `ESWorldItem`、表现对象和内部短命运行时 | ItemInstance/Container 的所有权不依赖池对象 |
| Interaction | Pickup 发起 `Items.Pickup` 请求 | Interaction 不直接写库存 |
| Save | 通过稳定快照写入并在加载后重建 | 不序列化 Unity 引用或 UI 状态 |
| Camera / State / AI | 只消费装备已提交后的许可、数值或定义结果 | 不取得 Container 写入权 |

## 12. 性能与生命周期规则

- 常规背包浏览、堆叠、装备和拾取路径不使用 LINQ、层级 Find、每帧 List 分配或每实例 `Update`。
- Container 在预期容量内预热；热路径按数组/List 索引访问，字典只用于 ID 到位置的边界解析。
- 世界拾取物按需池化；视觉对象被回收前先解除 `ItemInstanceId` 与 Claim，避免下一租户继承。
- 物品域不会以“距离远”为由销毁权威库存数据；未来 LOD 只可降低世界 Pickup 的表现、碰撞探测或 UI 刷新频率。
- 性能结论必须在 Unity Profiler、Player/IL2CPP 下验证；未测前不得宣称零 GC 或具体容量上限。

## 13. 最小实施切片与验收

### 切片顺序

1. `ESItemModule`、Holder、Container、ItemInstance、`ListStorageStrategy`、`Add/Move/Remove`、稳定快照的纯数据单测。
2. `ESEquipmentLoadout`、`ESEquipmentProjectionModule`、`Equip/Unequip`、属性/Tag Lease 的成功、Deferred 与补偿路径；暂不加载模型。
3. `ESWorldItem`、Claim、`Pickup`、`DeathEventId/LootReceiptId` 幂等的世界掉落链。
4. Entity 装备逻辑/表现同步、武器正式 API 接入、回池重建与异步表现失败重试。
5. 存档迁移、商店唯一库存；网络接入只在服务端权威方案批准后开始。

### 必须通过的测试

- 同一实例移动、拆分、合并、容量不足、槽位不匹配、旧 Revision、重复 TransactionId。
- 装备后实例只存在于 `ESEquipmentLoadout`；已就绪 Projection 的逻辑 Lease 失败时 Container/Loadout 完整恢复；Entity 缺席时 Projection 为 Deferred；外观失败时库存不回滚。
- 玩家、装备怪、普通怪、NPC、箱子和唯一商店库存走同一 Container/Loadout/Transaction 契约。
- 同一个 DeathEvent/LootReceipt 重放不会生成第二份实例或第二个世界拾取物。
- 多人/多 AI 同时请求拾取时，仅一个 Claim 成功。
- Entity 与世界 Pickup 回池后，旧 Generation、旧 Claim、旧异步外观回调均不能影响新租户。
- Projection 必须覆盖：同 Revision 幂等 Apply、新 Revision 先撤旧 Lease、Owner/Loadout/EntityGeneration 任一不匹配的旧异步回调被拒绝，以及逻辑投影成功但视觉投影失败的独立状态。
- Save -> Load 后 Container、`ESEquipmentLoadout`、`ESEquipmentProjectionModule` 的 Tag/属性逻辑投影与世界拾取物一致重建。
- Unity PlayMode：拾取、装备、卸下、怪物死亡掉落、装备怪掉落已穿装备、箱子、唯一商品售罄。
- Profiler：连续背包操作与大量世界拾取物下无稳定帧 GC；结论附实际 Player/IL2CPP 证据。

## 14. 支持范围

适合 ARPG、开放世界动作 RPG、JRPG/队伍 RPG、刷宝射击、Roguelite、生存制作、怪物收集、商店经营和联机 RPG 的物品需求。纯赛车、格斗、跑酷、解谜或只有固定 Loadout 的项目可只启用轻量装备配置，或完全不登记 `ESItemModule`。

最终冻结原则：

> Loot/Reward 决定产生什么；ESItemModule 决定实例如何创建、持有、移动和销毁；Pickup 只负责世界表现；Entity 只接收已提交装备投影。一个持有者可拥有多个 Container 和一个可选 `ESEquipmentLoadout`；事务是唯一写入口；异步表现失败不回滚已成功的逻辑库存事务。
