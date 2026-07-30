# Entity 固有 Tag、Prefab 入口与池化闭环实施记录

状态：已完成代码闭环；待 Unity EditMode / PlayMode 实跑。  
目标：让“出生时天然拥有的事实”和“其他系统临时添加的事实”可以同时存在、各自释放，且池化复用不会遗留上一轮状态。

## 冻结后的使用方式

```text
ActorDataInfo / MonsterDataInfo / NpcDataInfo
  └─ 直接 tags: List<ESTagStableReference>
       └─ Entity.BindDefinition(...)
            └─ Entity 的 intrinsicTagLeases
                 └─ Entity.Tags

Buff / 装备 / 区域 / 状态投影
  └─ 各自直接 tags + 各自 ESTagLeaseSet
       └─ 同一个 Entity.Tags
```

- DataInfo 是 Entity 固有 Tag 的唯一配置权威；Prefab 只引用角色定义，绝不再保存第二份 Tag 列表。
- 通用池模板可以不填角色定义，由明确的租出方在本次租出时直接调用 `Entity.BindDefinition(...)`。
- 写入者直接持有 `List<ESTagStableReference> tags`。禁止恢复已删除的单字段 Tag 配置包装。
- `ESTagCollection` 只保存聚合 Count、HotSlot/Sparse 存储、查询、快照和 Link 事件；它不知道 Entity、Buff、阵营或业务权限。
- Lease 的 Source 只属于 Lease 自身的生命周期和按需诊断，容器不保存业务对象引用。

## Entity 的实际状态与 API

Entity 只保存当前定义引用、其直接 `tags` 引用和一个 `intrinsicTagLeases`。不复制 DataInfo 配置。

```csharp
entity.BindDefinition(monsterInfo);
entity.ApplyIntrinsicTags();
entity.ReleaseIntrinsicTags();
entity.ClearDefinition();
entity.HasIntrinsicTag(tag);
```

状态定义固定为：

| 状态 | 含义 |
| --- | --- |
| `Empty` | 没有固有 Tag 配置。 |
| `Pending` | Catalog 尚未绑定，等待 `CatalogBound` 后重试。 |
| `Applied` | 当前定义的 Tag 已成功持有。 |
| `Failed` | 当前新配置无法解析或申请；旧的有效 Lease 保留。 |

相同定义、相同 Catalog 且 Lease 仍有效时，`ApplyIntrinsicTags()` 不会释放再申请，不产生无意义的状态抖动。替换配置先完整校验并申请候选 Lease，成功后才归还旧 Lease；失败时旧事实不丢失。这里保证的是所有权状态可回滚，不是事件层严格原子切换：Link 接收者可能先看到候选 Tag 增加，再看到旧 Tag 释放。

## 固有与临时事实的边界

同一 Tag 可以同时被固有定义和多个临时系统持有：

```text
固有 Tag 1 + Buff 1 = Count 2
Buff 结束          = Count 1
Entity 回池         = 本轮所有旧 Lease 失效
```

Buff、装备、区域和状态投影可拥有自己的 `ESTagCollection` 表达自身流程状态；它们若要影响 Entity，再持有 Entity.Tags 的 Lease。Tag 名称必须能说明状态归属，例如 `Buff.Active` 只描述 Buff 本身，`Status.Poisoned` 才是写到目标 Entity 的事实。

## 池化语义

对象池在重新激活前调用 `OnGetInPool()`；Entity 在此阶段重建本轮结构、绑定 Prefab 上的定义并申请固有 Tag。回池时 `OnPushToPool()`：

1. 结束 Buff 与固有 Tag 的本轮生命周期；
2. 失效本轮 ValueChange；
3. 调用 `Tags.ResetForReuse()`。

`ResetForReuse()` 推进 generation、清空 HotSlot/Sparse Count 和旧诊断，因此旧 Lease 即使迟到释放也不能影响下一次取出的对象。它不在稳定循环中创建临时集合。

普通池的“取出后再绑定定义”仍是允许的租出方流程，但它发生在激活后。若业务要求新定义必须在 `OnEnable` 前可见，应单独设计明确的激活前参数入口；不得把它伪装成宽泛的 `Object` 类型分发或新的单字段包装。

## 快照与存档

Entity 创建的稳定 Tag 快照会排除定义固有 Tag。恢复顺序是：先绑定定义并重建固有 Lease，再由 Buff、装备、任务等各自的存档数据恢复各自 Lease。

稳定快照只传 `ESTagStableReference` 与 `SchemaHash`，不传 RuntimeKey、Count、HotMask 或 Source。聚合 Count 不能反推业务来源，因此禁止把临时来源混进 Entity 快照。

## 验收边界

已覆盖的代码规则：直接 DataInfo 配置、无抖动替换、Catalog 延迟绑定、旧 Lease generation 失效、快照排除固有事实、取出前清空旧 Tag。

仍须在 Unity Test Runner 实跑的场景：

1. 固有 Tag 与两个 Buff 叠加并逐一释放；
2. Catalog 延迟绑定后只申请一次；
3. 反复回池/取出后旧 Lease 不影响新生命周期；
4. 专用 Prefab、通用池模板、定义切换和快照恢复；
5. 热路径 GC 采样。

## 不在本项内的独立 P0

GameCore / RuntimeData 反向持有 Prefab、GameObject 或 Scene 的迁移是独立任务。目标仍是：Prefab、Spawner、Scene 可以引用 DataInfo / GameCore；GameCore 不直接引用内容对象。不要为完成本 Tag 生命周期闭环顺手大范围重构该资源依赖图。
