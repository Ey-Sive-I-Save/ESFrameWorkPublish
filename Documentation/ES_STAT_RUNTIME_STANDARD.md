# ES Stat Runtime Standard

状态：代码结构与定向回归测试已补齐；当前 Unity 生成的项目文件仍需刷新：`ES_Logic.csproj` 实测为 0 warning / 3 errors，原因是既有 RawConfig 源码未收录；`ESCharacterAttributeCatalog.generated.cs` 已被 ES_Logic 收录，但两个 Editor 生成器文件未被 `ES_Logic.Editor.csproj` 收录。不能宣称静态构建已验收。Unity Test Runner、Player/IL2CPP 与 Profiler 实测待验收。
最后验证：2026-08-02。
适用源码入口：`ESFloatValueChangeSet`、`ESPermitSet`、`ESSuperAttributeTable`、`ESSuperAttributeCatalog`、`Entity`。

## 目标

Stat 是 ES 的数值事实系统：一个宿主拥有基础值，多个运行时来源叠加修正，最终得到可高频读取、可安全撤销的值。它不承担 Buff 生命周期、布尔条件、伤害结算、阵营或状态机执行。

| 领域 | 只负责什么 | 不负责什么 |
| --- | --- | --- |
| Tag | 可组合的布尔事实和条件 | 数值、层数、伤害结算 |
| Buff | 生命周期、叠层、机制，向宿主写入效果 | 自己再造数值容器 |
| Stat / ValueChange | 基础值、数值修正、最终值和上下界 | Buff 的计时、Tag 的条件语义 |

运行链路：

```text
GameCoreEditorGlobalData（角色属性 / 物品属性）
    -> ESAttributeBakeTable（只读稳定定义 + SchemaHash）
    -> ESAttributeCatalogGameCore（Consumer 注入）
    -> ESAttributeRuntimeCatalog（当前进程 Catalog）
    -> Entity / Item（HotSlot 数组 / Sparse Dictionary）
    -> ESFloatValueChangeSet（Base + Modifier -> Final）
    -> KCC / Combat / UI 读取最终值
```

不要新建 `ESStatCollection`、`ESStatConfig` 或“属性运行时配置”包装。`GameCoreEditorGlobalData` 内联的两张 `ESSuperAttributeTable` 是唯一可编辑定义权威；`ESFloatValueChangeSet` / `ESPermitSet` 是数值聚合权威。

## 配置

策划只打开 `【ES】/项目设置/GameCore/编辑器全局数据`，在“角色属性”或“物品属性”分区直接配置 `ESSuperFloatAttributeDefinition`：

| 字段 | 含义 |
| --- | --- |
| `enumKey` / `key` | 至少一项；两项同时填写必须绑定同一稳定身份 |
| `storagePolicy` | `HotSlot` 给已固定的角色热点枚举；其余使用 `Sparse` |
| `fixedApiName` | 仅 `Attribute.Character` 的 `HotSlot` 可填写；标记需要编译期数组 API 的固定热点。它不是身份，留空仍是可配置的 Catalog HotSlot |
| `displayName` | 仅供 Picker、Inspector、调试面板显示 |
| `overrideBaseValue` / `baseValue` | 定义提供的默认基础值 |
| `minValue` / `maxValue` | 定义层最终硬上下界，运行时 Modifier 不能绕过 |
| `migrationKey` | 稳定身份迁移信息 |

公式字段当前必须为空。需要动态数值时，由 Buff、装备、区域或业务系统在运行时写入 ValueChange；禁止把不可审计的公式字符串塞进定义。

普通属性直接执行 `【ES】/项目设置/GameCore/Bake并应用GameCore Catalog`。固定角色属性仅在结构变化时才需要先执行 `【ES】/项目设置/GameCore/生成角色固定属性代码`：新增、删除或重命名 `fixedApiName`，修改其 Float/Permit 类型、稳定 EnumKey/StringKey，或改变实际固定槽位顺序。基础值、范围、显示名、公式、迁移键和 Buff 绑定仍是纯 GameCore 配置，直接 Bake，不触发 C# 生成或 Unity 编译。Bake 会完整比较确定性生成源码；该源码只投影固定 API 结构，因此普通配置不会造成过期，任何枚举、数组或映射代码篡改都会被拒绝并保留旧产物。随后系统验证 Tag、角色属性、物品属性和所有 Buff 的 Tag/属性绑定，再同时更新只读 Bake 产物与两个 Consumer 根。运行时加载的是 `ESAttributeCatalogGameCore`，绝不读取 `GameCoreEditorGlobalData`。

框架升级新增内建固定角色属性时，执行“补齐角色与物品属性表”会把编译投影中的缺失稳定身份添加进旧 GameCore 资产，并可为已识别旧内建行补齐空的 `fixedApiName`。它不改写已有策划值、范围、公式、显示名、Storage 或非空访问名；补入固定 API 结构后才需要生成代码并 Bake。它服务代码版本兼容，不是第二份策划 Schema，更不能用代码覆盖基础值、范围、显示名或 Storage。

`ItemDataInfo` 不再配置一张属性表；它只在 `floatValues` / `permitValues` 填写本物品的稳定 Key 和基础值。类型、范围、显示名、存储策略和迁移信息始终由 GameCore 决定。

`RuntimeKey`、`SetId`、`ESValueChangeToken` 和 OwnerId 都是本进程运行期身份，绝不进入配置、存档或网络载荷。持久化和联机只使用稳定 Enum/String Key 与 Catalog SchemaHash。

## 业务使用

角色高频读取只使用固定枚举：

```csharp
float speed = entity.GetCharacterFloatStatValue(
    ESCharacterFloatAttributeId.GroundMaxMoveSpeed,
    fallbackBaseValue: 5f);
```

这是数组和标量路径；未受修正的 Slot 不会创建 `ESFloatValueChangeSet`。String/Enum 双别名是配置、加载和低频业务边界，不能放进每帧 KCC、战斗循环或 UI 刷新。

GameCore 新增的角色 Hot 属性不要求再修改 `ESCharacterFloatAttributeId`、数组、String switch 或默认表。Bake 会为它分配 Catalog HotSlot；业务在初始化边界解析一次 RuntimeKey 后，使用 `GetFloatStatValue(runtimeKey)`。只有 KCC 内置运动/控制属性继续使用编译期枚举，保持最短数组路径。

修改基础值属于宿主的业务状态，不会撤销外部效果：

```csharp
entity.SetCharacterFloatStatBaseValue(
    ESCharacterFloatAttributeId.GroundMaxMoveSpeed,
    6f);
```

Item 使用完全相同的 ValueChange 规则，但高频系统应在初始化边界缓存 Item RuntimeKey：

```csharp
item.TryGetAttributeRuntimeKey(enumKey, stringKey, out int damageKey);
float damage = item.GetFloatStatValue(damageKey);
```

Item HotSlot 由 GameCore Catalog 转换为紧凑数组位置；Sparse 项只在第一次写入时创建字典项。两者的稳定身份、配置和存档规则相同，差别仅是运行时存储方式。

短期或跨系统数值效果必须由拥有者持有 `ESEffectLease`。Lease 释放时会按 OwnerId 归还该来源在 Entity 全部 Float/Permit 集合中的 Token：

```csharp
ESEffectLease effectLease = entity.CreateValueChangeEffectLease(out int ownerId);
ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(
    ESCharacterFloatAttributeId.GroundMaxMoveSpeed,
    fallbackBaseValue: 5f);

speed.Add(ESFloatValueChangeOp.AddPercent, 0.20f, ownerId: ownerId);

// Buff、装备或区域结束时调用；同一 Lease 的重复释放安全失败。
effectLease.Dispose();
```

同一来源需要动态调值、启停某一条 Modifier 时，保留 `ESValueChangeToken` 并调用 `Update`、`SetEnabled` 或 `Release`。不能依赖扫描、字符串查找或“删掉同名属性”来归还效果。

## 运算规则

`ESFloatValueChangeSet` 的顺序固定，任何配置顺序都不会改变阶段：

```text
Override（最高 priority；同 priority 后加入者胜）或 Base
    -> Add 总和
    -> AddPercent 总和
    -> Multiply 连乘
    -> Modifier Min / Max
    -> Definition minValue / maxValue
    -> Final
```

`Min` 取所有启用项中的最大下限，`Max` 取所有启用项中的最小上限。定义上下界最后执行，因此运行时效果不能让最终值越出定义范围。输入拒绝 NaN/Infinity，聚合溢出会钳制为有限 Float，避免污染下游系统。

`BeginBatch()` 只合并通知，不延迟数值可见性或 Revision。观察者异常会单独记录并继续后续观察者，绝不会回滚已经提交的 Stat 改动；派发期新增/移除订阅从下一次通知生效，重复订阅被忽略。

Entity 清理、重绑定和池化复用期间会暂时拒绝所有可能创建容器或修改 Base/Fallback 的 Float/Permit API。监听回调只能使用 `TryGet` 或调试快照查看旧状态；若尝试 `GetFloatStat`、`GetCharacterFloatStat`、`GetPermit`、`Set...BaseValue` 等可变入口，会得到明确异常。这样 Hot 槽位不会在已清理后复活，Sparse Dictionary 也不会在枚举清理时被监听回调修改。

池化清理会同时清除显式 Base/Fallback、Modifier、Token、Owner/Source 索引和旧 `Changed` 订阅。Hot Set 保留在固定槽位内，但会标记为未物化，因此 `TryGet...` 与调试面板不会把上一位租户的容器视为当前状态；下一次实际写入才重新配置 Base、上下界和 Fallback。Sparse Set 会从当前 RuntimeKey 字典移除并进入该 Entity 的私有复用缓存。`ESEffectLease` 槽位保留 generation，旧 Lease 不能释放下一次租用者的效果。

`ESFloatValueChangeSet`、`ESPermitSet` 是当前宿主生命周期内的引用，不是跨池化边界的所有权句柄。Buff、装备和区域必须保存 `ESEffectLease` / `ESValueChangeToken`，并在自身结束时归还；不得把原始 Set 引用带到下一次 Host 租用。

## 调试与可视化

`ESFloatStatSnapshot` 是值类型快照，包含 Base、Add、Percent、Multiply、Override 胜者、两层上下界、每个阶段值、Final、Revision 和修改项计数。`CopyDebugModifiersTo(List<ESFloatStatModifierSnapshot>)` 提供 OwnerId、SourceId、Token、优先级、启用状态和 Override 胜者明细；调用方复用 List 容量即可避免额外分配。

`Entity` 的只读入口：

```csharp
entity.TryGetFloatStatDebugSnapshot(enumKey, stringKey, fallbackBase, out snapshot);
entity.CopyFloatStatDebugEntriesTo(entries);
```

它们不会为了显示面板实例化未受修正的 Hot/Sparse Stat。Unity 编辑器入口为 `【ES】/运行时诊断/属性系统/运行时面板`：选中运行中的 Entity 后显示稳定 Key、诊断 RuntimeKey、存储策略、Base/Final、阶段数值、上下界和所有活动修正明细。该窗口只读，不允许反向修改游戏状态。

## 性能边界

| 路径 | 目标 | 前提 |
| --- | --- | --- |
| `GetCharacterFloatStatValue` | 0 GC | 仅固定枚举读取；不走字符串或 Catalog |
| 已有 `ESFloatValueChangeSet.Value` | 0 GC | 稳态、无变更时直接返回缓存 |
| 有修改后的首次 `Value` | 0 GC | 已有容器和容量；按 Modifier 数量线性计算 |
| Buff / Equipment 结束 | 0 GC 目标 | 使用已分配的 Lease、Token、Owner 索引；首次扩容不计入稳态 |
| 同一批 Entity 池化复用 | 0 GC 目标 | Hot Set、Sparse Set 和 EffectLease 槽位均已热身；不首次接触新的 Sparse Key |
| 同一批 Item 池化复用 | 0 GC 目标 | 已触及的 Hot Set、Sparse Set 和效果槽均已热身；不首次触及新 Sparse Key |
| Snapshot、Debug Window、String Key 解析 | 冷路径 | 允许按需分配，禁止放进 Tick |

不得在每帧创建 Token、Lease、临时 List、字符串、闭包、LINQ 结果或通过 StringKey 解析 Stat。大量对象只在确有数值组合查询时才实例化 Sparse Stat；未受修正的 Hot 属性保持数组基础值路径。

## 验收状态

已新增并完成测试程序集编译的定向回归：运算顺序、Override 优先级、定义上下界、Token/Owner 归还、重入批处理、观察者异常隔离、派发期增删订阅、Entity Hot/Sparse 快照不实例化、重置期 Hot/Sparse/Permit 重入写入拒绝，以及属性 Bake 被非法定义拒绝后保留旧 Schema 与 Hot/Sparse 布局。Unity Test Runner 尚未实跑，因此这些仍不是运行时验收证据。

最终商业验收仍需：

1. Unity Test Runner 实跑新增回归测试。
2. Unity Player/IL2CPP 下对预热后 Hot 读取、Buff 施加/归还和 Snapshot 分别采样 GC Alloc。
3. 长时间池化循环，验证旧 Lease/Token 不能影响下一次 Entity 使用。
4. 正式 Attribute Catalog Bake 与稳定 Key/SchemaHash 审计。
