# 属性数值与 ValueChange 边界

级别：P0 运行时正确性与性能约束；P1 可观测性与配置体验约束。
适用范围：`ESSuperAttributeTable`、`ESSuperAttributeCatalog`、`ESFloatValueChangeSet`、`ESPermitSet`、`Entity`、Buff、装备、区域和所有数值效果写入者。

## P0：唯一数值权威

- 定义只写入 `ESSuperAttributeTable`；运行时 Float 聚合只使用 `ESFloatValueChangeSet`，Permit 聚合只使用 `ESPermitSet`。
- `GameCoreEditorGlobalData.characterAttributes` 与 `itemAttributes` 是唯一可编辑 Schema；`ESAttributeBakeTable` 是只读产物，`ESAttributeCatalogGameCore` 只负责 Consumer 注入，`ESAttributeRuntimeCatalog` 只提供当前进程查询。Entity、Item、Buff、Prefab 和普通 DataInfo 禁止再保存属性类型、范围、Hot/Sparse 或显示名的第二份表。
- `fixedApiName` 不是第二份身份或属性配置：它只是 `Attribute.Character` 中一个 `HotSlot` 行的编译期访问名。`EnumKey` / `StringKey` 仍是唯一稳定身份；留空的 Character HotSlot 与所有 Sparse 属性均保持纯 Catalog 路径，不生成 C# API。
- 固定角色属性 API 必须由 `GameCoreEditorGlobalData.characterAttributes` 确定性生成到 `ESCharacterAttributeCatalog.generated.cs`。禁止手改该文件、手工同步 `ESCharacter*AttributeId`、Key 数组、双向映射、switch 或默认表；禁止恢复第二份可编辑 Enum/映射表。
- 编译内建默认投影只可向旧 GameCore 资产**补入缺失稳定身份**，用于框架版本新增固定 KCC 属性后的兼容同步；它绝不覆盖已有行的基础值、范围、显示名、Storage 或身份。补入后 GameCore 行立即成为唯一可编辑权威。不得把这个受控补齐机制扩大为“代码覆盖策划属性表”。
- 固定 API 生成是按需分阶段门禁：仅 `fixedApiName` 的新增/删除/改名、Float/Permit 类型、稳定 EnumKey/StringKey 或实际固定槽位顺序变化时，才执行 `生成角色固定属性代码` -> 等待 Unity 编译/域重载 -> `Bake并应用GameCore Catalog`。基础值、范围、显示名、公式、迁移键和 Buff 绑定只需直接 Bake。Bake 必须完整比较确定性生成文件；它只投影结构，故普通配置不触发生成，但保留签名注释后篡改枚举、数组或映射代码也必须拒绝并保留旧 Catalog。不得伪装成一个会跨域重载自动完成的原子按钮。
- `ItemDataInfo.floatValues` / `permitValues` 只允许填写该物品自己的稳定 Key 与基础值。它们不是 Schema，禁止恢复 StoragePolicy、范围、显示名、公式或嵌套属性配置。
- Bake 必须先验证角色和物品两套源表，再更新任何旧产物；失败时保留旧 `ESAttributeBakeTable`、根 SchemaHash 和运行时可加载版本。RuntimeKey 仅在 Consumer 注入后由 Catalog 重建，绝不写进 Bake 资产、Item、存档或网络。
- 禁止新增 `ESStatCollection`、`ESStatConfig`、`EntityStatConfig`、`RuntimeStatConfig` 或任何仅转发 ValueChange 字段的包装。
- Tag 表达布尔事实，Buff 表达生命周期，Stat 表达数值。不得把层数、伤害、阵营、计时或状态机执行塞入 Stat。
- 外部持续效果必须持有 `ESEffectLease` 或自己拥有的 `ESValueChangeToken`；禁止按 StringKey 扫描删除、裸清空其他来源或以“同名属性”猜测归还目标。
- 角色高频读取只使用 `GetCharacterFloatStatValue(ESCharacterFloatAttributeId, ...)`。不得在 KCC、战斗 Tick、AI 热循环或 UI 每帧刷新中传 StringKey、查询 Catalog、创建 Resolver、Token、Lease、List、LINQ 结果或闭包。
- RuntimeKey、SetId、Token、OwnerId、SourceId 都不稳定且只属于当前进程。配置、存档、网络和回放只传稳定 Enum/String Key 与 SchemaHash。
- 定义 Formula 当前不支持且必须为空。动态值由明确生命周期的运行时 Modifier 写入，禁止引入任意公式字符串执行。
- `minValue` / `maxValue` 是定义层最后执行的硬边界。运行时 Modifier、Buff 或 Editor 工具不得绕过。
- Buff 当前只写入目标 Entity 的 `Attribute.Character`。Bake 必须扫描每个 Float/Permit Binding，拒绝未配置身份、缺少 `change`、双别名不一致、未解析身份、错误 ValueKind 或错误 Scope；运行时不得把这些错误静默跳过。未来 Item Buff 效果必须先增加明确目标域，再按该域验证，禁止猜测 Scope。

## P0：通知与清理

- ValueChange 的状态先提交，观察者仅旁路。单个 `Changed` 接收者抛异常不得阻断其他接收者、Lease 归还、Owner 清理或后续重入通知。
- `BeginBatch()` 只合并通知，不是事务回滚。需要“全部成功或不产生效果”的业务必须在写入前完成自身预检。
- Entity 重绑定义、销毁或池化复用前必须先按生命周期归还 Buff/装备等外部 Lease，再执行 ValueChange 清理；旧 Token/Lease 绝不能写入下一次使用者。
- `ClearValueChanges()` 期间禁止经由 `Get...Stat`、`Get...Permit`、`Get...Value` 或 `Set...Base/Fallback` 重建或修改容器。回调只允许 `TryGet`、Snapshot 等只读 API；否则会污染已清理 Hot 槽位或修改正在遍历的 Sparse Dictionary。
- Pool 生命周期清理必须清除运行时 Base/Fallback、Token、Owner 索引与 `Changed` 订阅；Hot Set 可以保留其内部容量，但必须重新标记为未物化。Sparse Set 必须从活动 RuntimeKey 字典移除，才可进入宿主私有复用缓存。
- `ESEffectLease` 槽位在池化时不得清空代际记录。下一位租户重新申请同一槽位时必须推进 generation，旧 Lease 只能失败，绝不能释放新租户效果。
- `ESEffectLease` 同时是外部效果的唯一写入资格与释放资格。Entity、Item 和业务 API 禁止返回可复用的裸 OwnerId/SlotId；新增 Float 或 Permit Modifier 必须经 Lease 在写入瞬间校验 slot+generation，并以 Set 的 O(1) 宿主标识验证 `ReferenceEquals`。旧 Lease、其复制值、异步延迟回调或其他 Host 的 Set 都只能被拒绝，绝不能把 Modifier 写入新租户或其他宿主。
- `BindEffectLeaseHost` / `IsEffectLeaseHost` 是 `ES_Stand` 对 `ES_Logic` 的内部容器协作接口，不是普通业务 API。由于 `InternalsVisibleTo("ES_Logic")` 允许同程序集访问，规则上只允许 Entity/Item 在物化自身 Hot/Sparse Set 时调用；Buff、Skill、装备逻辑和其他业务代码调用或伪造绑定一律按 P0 拒绝。
- `ESFloatValueChangeSet` / `ESPermitSet` 是宿主当前生命周期的运行时引用，不得跨 Buff、Item、Entity 的池化边界缓存。跨生命周期的效果只保存自己的 `ESEffectLease` / `ESValueChangeToken`，并在拥有者结束时归还。

## P1：配置和调试

- EnumKey-only、StringKey-only、Enum+String 同一声明均可接受；双别名冲突必须拒绝 Bake/绑定。
- `displayName` 只用于 Picker、Inspector 和调试，不可作为身份或运行时查找键。
- `ESFloatStatSnapshot` 与 `CopyDebugModifiersTo` 是冷路径。调试仅显示 OwnerId/SourceId 等数值诊断，不让集合常驻保存 Buff、Item、GameObject 等业务对象引用。
- 运行时数值面板必须使用 `MenuItemPathDefine.STAT_RUNTIME_PANEL_PATH`，入口是 `【ES】/运行时诊断/属性系统/运行时面板`。禁止新增 `ES/...`、`Tools/...` 或硬编码根菜单；该窗口只读，不得变成直接修改战斗状态的入口。
- Snapshot 或调试 List 如需稳定 0 GC，调用方必须复用并预热自己的 List 容量；不得把调试 API 放进普通 Tick。

## P1：池化与热身性能

- 空 Entity 的 Sparse Dictionary、EffectLease 槽位表和 Set 内 Modifier/Token 索引均必须延迟创建。固定角色 Hot 数组可常驻，不能为了节省少量冷内存退化 KCC 的枚举数组读取。
- 回池不得把已热身的 Hot Set 置空；Sparse Set 应在 Entity 私有复用缓存中按需复用。只有首次触及新类型、超出已热身容量或创建公开调试快照时允许分配。
- 关卡已知会频繁受 Buff 修改的 Hot Stat，应在预热阶段物化并预留合理 Modifier 容量；不要把首次扩容留给战斗峰值。

## 修改前核对

1. 这是否真的是数值事实，而不是 Tag、Buff 生命周期或状态机职责？
2. 热路径是否已经拿到固定枚举或 RuntimeKey，而非 StringKey？
3. 写入者是否拥有明确 Lease/Token，并且能在移除、失败、池化、销毁时归还？
4. 是否会破坏定义层上下界、稳定 Key 或 SchemaHash 契约？
5. 是否已经为重入、异常、Owner 释放和池化复用补回归测试？
