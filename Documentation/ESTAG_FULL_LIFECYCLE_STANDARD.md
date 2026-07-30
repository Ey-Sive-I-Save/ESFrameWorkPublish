# ESTag 全流程标准

状态：架构冻结；Entity 已完成运行时接线，Item 已完成代码接线，Unity Bake / Test Runner 待最终签收。

## 一、职责

ESTag 表达可组合、可查询、可撤销的运行时事实。它不替代阵营关系、数值、资源身份、伤害结算、物理候选或状态机执行。

```text
GameCore 定义
→ Bake Catalog
→ 配置保存稳定引用
→ 启动绑定 Catalog
→ Host 聚合事实
→ 写入者持有 Lease
→ 消费者查询条件
→ 生命周期释放 / 池化失效 / 稳定快照
```

## 二、定义、Bake 与运行时

| 阶段 | 标准 | 边界 |
| --- | --- | --- |
| 定义 | `GameCoreTagDefinition` 声明 EnumKey、StringKey、StorageTier、Availability、传输范围 | Enum-only、String-only、双 Key 都可；至少一个稳定身份 |
| Bake | 生成唯一 `ESTagBakeTable`、RuntimeKey、SchemaHash、RuntimeLayoutHash | RuntimeKey 仅当前进程有效，不能写入配置、存档或网络 |
| 配置 | Picker 保存 `ESTagStableReference` | 策划不填写 RuntimeKey |
| 启动 | `ESTagCatalogGameCore` 绑定唯一 Catalog | Catalog 未绑定时拒绝解析和写入 |
| HotSlot | `ulong Mask + Count` | Catalog 已绑定、条件已编译、容量已热身后，高频 `Has/Matches` 目标为 0 GC |
| Sparse | `RuntimeKey → Count` 按需存储 | 不按最大 RuntimeKey 为每个实例预分配数组 |

首次条件编译、字典首次扩容、调试快照与稳定快照属于冷路径，不承诺零分配。

### 编辑器 Picker

所有 `ESTagStableReference` 字段和列表均由统一的 `ESTagStableReferenceDrawer` 绘制，并通过 `ESSearchDropdown` 从唯一正式 Catalog 选择。Picker 显示 InspectorName、StringKey 与 Hot/Sparse 存储策略；作者不手填 RuntimeKey。

- 不得在单个 DataInfo 上恢复 `ValueDropdown`、`GetTagOptions()` 或自行复制 Tag 选项列表；这会制造第二条编辑器选择链。
- `ActorDataInfo`、`MonsterDataInfo`、`NpcDataInfo`、`ItemDataInfo`、Buff 与条件配置直接持有 `ESTagStableReference`，因此天然使用同一 Picker。

## 三、Host 与写入者

任何独立运行时对象都可以成为 `ESTagCollection` Host，但只有它自身存在跨系统组合查询需求时才实际持有。影响其他对象时，必须通过自己拥有的 `ESTagLeaseSet` 写入目标 Host。

| 对象 | 自身 Collection | 对其他 Host 的影响 | 当前状态 |
| --- | --- | --- | --- |
| Entity | `Entity.Tags` | 接收 Buff、装备、区域等 Lease | 已实现 |
| Item | `Item.Tags` | 装备/附魔/耐久等模块可向持有者或目标申请 Lease | 已接线，待 Unity 实跑 |
| Buff Runtime | 按需，不默认创建 | 当前向目标 Entity 申请 Lease | Entity 写入已实现 |
| Equipment | 若本质是 Item，不建立重复 Host | 向持有者 Entity 申请 Lease | 装备写入已实现 |
| Area | 仅自身需要被查询时创建 | 当前可向进入 Entity 申请 Lease | 区域写入已实现 |
| Skill | 仅 Skill 实例成为查询主体时创建 | 当前不写入 Tag | 当前只读取施法条件 |
| Task / Quest | 待正式 Runtime 对象出现后决定 | 可按自身规则影响其他 Host | 未接线 |

Buff 层数是数值，Buff 结束是生命周期；Area 启用是组件状态。没有跨系统事实查询需求时，不要为了“拥有 Tag”重复编码这些状态。

## 四、固有 Tag

```text
ActorDataInfo / MonsterDataInfo / NpcDataInfo
  → Entity.BindDefinition(...)
  → Entity.intrinsicTagLeases
  → Entity.Tags

ItemDataInfo.tags
  → Item.BindDefinition(...)
  → Item.intrinsicTagLeases
  → Item.Tags
```

- DataInfo 是固有 Tag 的唯一配置权威；Prefab 只引用定义，绝不复制 Tag 列表。
- 定义不变且 Lease 有效时，重复 Apply 不产生 `1 → 0 → 1` 抖动。
- 新定义无效时保留旧所有权；这保证所有权可回滚，不保证事件层严格原子。观察者可能先看到候选 Tag 增加，再看到旧 Tag 释放。

## 五、写入、读取与归还

| 角色 | 规则 |
| --- | --- |
| 写入者 | 在自身激活路径调用 `ESTagLeaseSet.TryApply(target.Tags, tags, source, out error)` |
| 归还者 | 在切换、离开、禁用、结束、销毁时 Dispose 自己的 LeaseSet |
| 读取者 | 使用 `ESTagConditionConfig` 的 `required`、`requiredAny`、`forbidden` |
| 禁止项 | 业务代码不得直接改 Count、按 Tag 强制删除或从聚合 Count 反推来源 |

Skill、State、AI、Interaction 当前已经接入条件读取。Hit 的 `ESHitTagEligibility.TryAllows()` 尚未接入命中主链。

## 六、池化、存档与调试

| 场景 | 标准 |
| --- | --- |
| 回池 | 释放本轮固有 Lease，再调用 `ResetForReuse()`；generation 推进使旧 Lease 永久失效 |
| 取出 | 维持 inactive，重建本轮容器状态，绑定定义并申请固有 Tag，再激活 |
| 快照 | 只保存 `ESTagStableReference + SchemaHash`；排除固有 Lease 持有的 Tag |
| 恢复 | 先绑定定义，再由 Buff、装备、任务等各自恢复自己的 Lease 所有权 |
| 调试 | 可查看 Hot/Sparse Count、最近变化、最近拒绝、观察者异常、Schema 与布局；容器不保存业务 Source |

## 七、验收

| 验收项 | 状态 |
| --- | --- |
| Catalog、Lease、多来源计数、Link、Hot/Sparse | 代码完成 |
| Entity 固有 Tag、池化、快照过滤 | 代码完成 |
| Item 固有 Tag、池化、快照过滤 | 代码接线完成，待 Unity 实跑 |
| Buff、装备、状态环境、区域写入 | 已接线 |
| Skill、State、AI、Interaction 条件 | 已接线 |
| Hit 主链、Task/Quest | 待接线 |
| String-only / 双 Key 正式资产样例 | 待 Bake 验收 |
| Unity Test Runner、稳定 Key 审计 | 待最终签收 |

测试代码必须以项目当前实际引用的 NUnit API 和 `IPoolable` 契约为准：不可假设存在未引用的 NUnit Attribute；实现 `IPoolableAuto` 时必须完整实现其继承的 `IPoolable.OnResetAsPoolable()`。这只保证测试程序集能够参与编译，不代替 Test Runner 实跑。
