# GameTag 身份与存储策略正交化整改提案

状态：实施记录，不是现行规范。当前规则以 `ESTAG_FULL_LIFECYCLE_STANDARD.md` 和 `KEY_GOVERNANCE.md` 为准；本文件只保留整改背景与迁移验收项。

状态：已实施代码闭环；待 Unity Test Runner 与正式 Bake 实跑。
范围：`ESTagBakeTable`、通用 Tag 容器、条件配置、直接 Tag 列表、存档/联机快照、编辑器 Picker、GameCore 审计与文档

## 一、检讨

实施前的 GameTag 曾把两个本应独立的概念错误地绑定在一起：

| 被错误绑定的概念 | 当前错误规则 |
|---|---|
| 稳定身份的表达形式 | Core 只能是 `ESGameTag EnumKey`，Extension 只能是 `StringKey` |
| 运行时存储策略 | Core 固定进入 `ulong`，Extension 固定进入稀疏字典 |

这违背了已确立的 Stable Key 治理原则：`EnumKey` 与 `StringKey` 都是正式稳定身份；Enum 仅在编辑器配置上更直接；HotSlot 与 Sparse 只决定内存布局和访问成本，不能决定业务身份的权威性。

此前把“StringKey 适合项目扩展”错误表述为“Extension/低频只能使用 StringKey”，将 `StringKey` 降格为 Mod 或低频业务专用。这是错误的设计判断和错误的对外说明。该规则已经从 Catalog、Picker、Condition、Snapshot、GameCore 模板和治理文档中移除，不能重新作为框架约束引入。

## 二、实施前问题与影响

实施前 `ESTagBakeTable.BuildRuntimeCache()` 存在以下硬绑定：

1. `Core` 条目必须声明 `ESGameTag EnumKey`，并强制 `RuntimeKey == EnumKey`。
2. `Extension` 条目必须有 `StringKey` 且不得有 EnumKey。
3. 条件配置只从 Extension 类别选择 StringKey；快照分别保存 Core Enum 与 Extension String。
4. GameCore 指令和 `KEY_GOVERNANCE.md` 使用了同样的错误措辞。

结果是无法表达以下合法需求：

- 一个正式 `StringKey` Tag 被明确分配为 HotSlot，以承担高频战斗判断；
- 一个已有 EnumKey Tag 因低频、实例按需而选择 Sparse；
- EnumKey 与 StringKey 同时绑定同一条 Tag 声明，用于编辑器便利和跨版本可读性；
- 使用统一 Picker、统一条件和统一 Snapshot，而不是由存储层级反推身份类型。

## 三、已完成的模型

将 Tag 定义拆成三个正交维度：

| 维度 | 责任 | 规则 |
|---|---|---|
| 稳定身份 | `EnumKey`、`StringKey` | 至少一个；两者同时存在时必须绑定同一条声明，且全局唯一 |
| 存储策略 | `HotSlot`、`Sparse` | 只决定实体内存布局与查询路径，不影响身份权威性 |
| RuntimeKey | Catalog Bake 输出 | 当前进程内加速索引；不得裸写入配置、存档、网络或跨 Schema 数据 |

现有语义名称为：

```csharp
public enum ESTagStorageTier : byte
{
    HotSlot, // RuntimeKey 1-63，对应 Entity ulong mask
    Sparse   // RuntimeKey >= 64，对应 Entity sparse count map
}

public struct ESTagStableIdentity
{
    public ushort enumKey; // 0 表示未声明
    public string stringKey; // null/empty 表示未声明
}
```

`ESTagBakeTable.Entry` 持有 `ESTagStableIdentity`、`ESTagStorageTier`、`availability`、`stableTransferScopes` 与 Bake 产物。`Core`/`Extension` 不再是以 Key 类型命名的分类；展示分组只可用于编辑器，不能参与验证、序列化或运行时分支。

## 四、目标运行时模型

```text
EnumKey ─┐
         ├─ ESTagBakeTable 声明 ─ Bake/验证 ─ RuntimeKey ─┬─ HotSlot: ulong + count
StringKey ─┘                                               └─ Sparse: RuntimeKey -> count
```

1. `HotSlot` 的唯一限制是 RuntimeKey 必须在 `1-63`；它可以由 EnumKey、StringKey 或双 Key 声明获得。
2. `Sparse` 的唯一限制是 RuntimeKey 必须大于等于 `64`；它同样可以由 EnumKey、StringKey 或双 Key 声明获得。
3. 所有写入者直接持有 `List<ESTagStableReference> tags`，并以 `ESTagLeaseSet` 管理自身生命周期；Lease 来源隔离不因存储层级变化。
4. 所有读取者继续使用统一 `Has` 与 `Matches`；条件 Bake 在内部按存储策略分出 HotMask 与 Sparse RuntimeKey 数组。
5. 存档和联机按声明中实际存在的稳定身份传输，并携带 Catalog SchemaHash；不传 RuntimeKey、Count、Source 或 HotSlot 位。

## 五、实施结果

### P0：Catalog 语义纠正（完成）

1. 用 `ESTagStorageTier` 替换以身份类型命名的 `Core/Extension` 运行时分类。
2. 移除“HotSlot 必须 Enum”“Sparse 必须 String”“RuntimeKey 必须等于 Enum”的验证规则。
3. 建立稳定身份校验：至少一个 Key、Enum 全局唯一、String 全局唯一、双 Key 对应唯一声明、RuntimeKey 全局唯一、存储区间匹配。
4. Bake 按稳定身份排序和显式 Slot 分配生成 RuntimeKey；不得依赖资产注册顺序。
5. 将当前 31 项迁移为 `HotSlot + EnumKey`，但不把该历史布局视为未来限制。

### P0：统一配置与 API（完成）

1. 用统一 `ESTagStableReference` 取代按身份类型拆开的条件字段。
2. Picker 显示所有 Runtime-available Tag，可按 `HotSlot/Sparse` 过滤查看，但不得按 Enum/String 限制可选项。
3. 写入者的直接 `tags` 列表与 `ESTagConditionConfig` 只持久化稳定引用；加载时集中 Bake。
4. `Has` 接受解析后的 Tag 引用；`Matches` 先进行 HotMask 判断，仅有 Sparse 条件时才访问稀疏表。

### P1：稳定传输、迁移与诊断（完成）

1. `ESTagStableSnapshot` 改为保存统一稳定 Tag 引用，而非 `CoreEnumTags` 与 `ExtensionStringKeys` 两套语义容器。
2. SchemaHash 计算覆盖稳定身份、存储策略、RuntimeKey、Availability、弃用替换和传输 Scope。
3. 审计报告列出每个 Tag 的 EnumKey、StringKey、StorageTier、RuntimeKey、写入者、读取者、存档/联机 Scope。
4. 旧资产执行一次明确迁移；迁移完成后删除旧分类兼容分支，避免长期双模型。

### P1：全流程回归（代码与编译完成；Unity 实跑待执行）

Buff、装备、状态投影、区域、Skill、AI、Interaction、命中资格、切换本地控制实体、实体销毁均通过统一引用、条件和 Lease 路径回归。命中系统仍只消费 Tag 条件；伤害、阵营与物理候选保持在其各自领域。

## 六、验收门禁

以下用例必须自动化并纳入预构建审计：

1. StringKey-only Tag 分配到 HotSlot，能够写入、查询、组合判断和独立释放。
2. EnumKey-only Tag 分配到 Sparse，能够写入、查询、组合判断和独立释放。
3. EnumKey + StringKey 双 Key 声明解析为同一 RuntimeKey；任一别名冲突必须阻断 Bake。
4. HotSlot 超出 `1-63`、Sparse 落入 `1-63`、RuntimeKey 重复、空稳定身份均必须失败。
5. 4096 个以上 Sparse RuntimeKey 不产生按最大编号预分配的实体数组。
6. 相同 Tag 多来源 Lease 的 2 -> 1 -> 0 变化、重复 Dispose、实体销毁清理均正确。
7. 条件在无 Sparse 项时不访问稀疏容器；有 Sparse 条件时 SchemaHash 或 RuntimeLayoutHash 不匹配必须明确失败。
8. Snapshot/网络只含稳定身份和 SchemaHash；任何 RuntimeKey、Count、Source、HotMask 出现在稳定载荷中均判为失败。
9. 发布审计中不存在“Key 类型决定 StorageTier”的规则、文档、AI 指令或代码分支。

## 七、交付标准

完成后可以准确表述为：

> ES GameTag 同时支持 EnumKey 与 StringKey 作为正式稳定身份。任一声明可按实际性能需求 Bake 到 64 个 HotSlot 或 Sparse RuntimeKey 容器；身份、存储策略和 RuntimeKey 生命周期彼此独立，并通过 SchemaHash、Lease 与发布审计形成闭环。

可准确表述为“Enum/String 身份与 Hot/Sparse 存储已经正交化并通过程序集编译”。在 Unity Test Runner、正式 Bake 和运行时性能采样完成前，不应将该结论扩展为 Unity 运行时的完整商业验收。
