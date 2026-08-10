# P0：稳定 Key 必须经 Catalog 烘焙，RuntimeKey 仅本进程权威

> 级别：P0。违反时停止当前设计或代码修改，先恢复稳定身份、Catalog 和迁移边界；不得用兼容分支、裸字符串 fallback 或手工 RuntimeKey 暂时绕过。

## 适用范围

适用于一切会跨越对象实例、配置资产、版本、存档、网络、DLC、Mod 或外部数据契约的业务身份：GameCore ConfigKey、GameTag、SuperAttribute、Input Config/Scheme/Action/Binding、任务、装备、角色能力、可配置状态和未来扩展系统。

不适用于只在一个所有者实例内创建、销毁和解释的局部容器键，例如单个 `ContextPool`、单个状态机、单个实体 Transform map、对象池临时句柄。局部键一旦进入存档、网络、独立资产引用、DLC 或 Mod 契约，必须立即升级为有 Scope 的稳定 Catalog Key。

## 唯一模型

```text
编辑器 / 配置 / 存档 / 网络：EnumKey 和/或 StringKey
                                      |
                                      v
确定性 Catalog 构建：Scope + 定义 Schema -> RuntimeKey + SchemaHash
                                      |
                                      v
当前进程热路径：RuntimeKey / HotSlot / Sparse 存储
```

- `EnumKey` 与 `StringKey` 都是正式稳定身份。枚举仅在编辑器发现、受限配置和重命名安全方面更强；StringKey 不是 Mod 的低级替代品。
- 两个别名同时存在时，必须在同一个 Scope 的同一条 Catalog 定义上解析到同一个 RuntimeKey；别名冲突是构建错误。
- `RuntimeKey` 只对**当前进程、当前 Catalog、当前 Catalog 生命周期**有效。它不是配置身份、存档身份、网络身份、跨 Catalog 整数，也不是可手工指定的 ID。
- `HotSlot` 与 `Sparse` 只定义存储和访问策略，绝不改变 Key 的权威性、持久化格式、网络格式或迁移规则。
- Key 定义必须拥有类型、默认值、范围、公式、弃用/迁移信息和声明所有者；这些规则不得散落在 Buff、UI、网络或任意消费者代码中。

## P0 禁令

以下任一行为都视为 P0 失败：

1. 将 RuntimeKey 写入 ScriptableObject、JSON、存档、Manifest、Catalog、网络包、日志重放、DLC/Mod 数据或跨进程缓存。
2. 根据注册顺序、数组顺序、对象 InstanceID、GUID、路径、Address、Bundle Hash、显示名、`KeyName` 或 Inspector 文本生成/恢复业务 RuntimeKey。
3. 在热路径反复以裸 StringKey 查表，或让未注册 StringKey 隐式创建属性、Tag、输入动作、配置数据或网络字段。
4. 让同一个稳定 Key 在不同值类型、不同定义 Schema 或不同别名配对下静默共存。
5. 假定两个客户端/两个进程的 RuntimeKey 数值相同，或把一个 Catalog 的 RuntimeKey 传给另一个 Catalog 解释。
6. 因为“旧数据兼容”而重新引入裸字符串 fallback、旧 RuntimeKey 恢复、双写 RuntimeKey，或未验证地把旧档案套到新 Schema。
7. 将所有 `Dictionary<string, ...>` 一律全局 Catalog 化。先判断其是否跨所有者和跨生命周期；局部键误升级同样是架构错误。

## 强制实现流程

1. 先判定 Key 边界：局部容器键，或跨资产/跨版本的稳定业务 Key。
2. 稳定业务 Key 必须声明 `Scope`、EnumKey/StringKey、值类型、定义 Schema、存储策略、迁移信息和 owner。
3. Catalog 必须按稳定身份确定性排序构建，生成紧凑 RuntimeKey 与 SchemaHash；不得依赖声明/加载顺序。
4. 配置、存档、网络、Mod 数据只保存稳定身份与必要 SchemaHash；进程启动或资源切换后重新解析 RuntimeKey。
5. 高频入口在初始化边界解析并缓存 RuntimeKey/HotSlot；低频或可选数据按需 Sparse 化，API 语义保持一致。
6. 联机、云档案、跨版本导入前比较 Catalog 名称/Scope 与 SchemaHash。不同则执行明确迁移，或拒绝并回退安全默认值。
7. 编辑器必须能验证别名、类型、重复声明、未使用项、读写所有者和跨资产稳定 ID 冲突。审计失败不得伪装为运行时可接受警告。

## AI 自动化内容定义门禁

AI 生成、选择或迁移 GameCore 内容时，只能把强类型稳定 Key 与可序列化结构化参数作为跨定义协议。`RuntimeKey`、运行时 Handle、`InstanceID`、委托、自由字符串约定以及裸 `ScriptableObject`、Prefab、`GameObject`、`Transform` 均不得成为 AI 输入输出、持久化内容身份或 Player 运行时权威。

作者侧 Inspector 可以提供 SO 或资源对象选择体验，但必须在 Bake、注入或发布边界转换为稳定 Key / 类型化 AssetKey，并校验对象与稳定身份指向同一条定义。运行时对象只能由领域 Table、Catalog 或资源 Provider 解析获得；作者引用不得绕过这些权威入口。

新增一类内容 Key 前必须同时满足：

1. 该对象是可独立复用、查询、版本化或迁移的内容定义，而不是耐久、冷却、弹药、目标、仇恨、阶段等实例状态，也不是某个 Owner 内部的局部槽位。
2. 面向正式可枚举 ES 内容资产时，同一实施批次交付 `Info + Group -> GameCore 注入 -> 强类型 RuntimeTable -> Consumer`；涉及资源时再接入 AssetKey / ResourcePlan / Provider。服务器数据、JSON/二进制、程序生成数据等非 SO 来源可以按 GameCore P0 直接使用领域 `InjectWith/TryInjectWith`，但仍必须交付强类型 Schema、Table、Consumer 和验证。只有 Key、空表或占位 DTO 不构成正式内容链。
3. AI 候选进入正式内容前，必须验证未配置 Key、重复或歧义别名、缺失引用、错误类型、循环依赖、未接入 Consumer 和 Schema/迁移缺口；任一失败都必须 fail-closed。
4. 行为、技能或世界定义 Key 不得兼任 Prefab、AudioClip、VFX、Bundle、地址或路径。资源身份继续由类型化 AssetKey 和资源系统负责。
5. 尚未拥有正式定义、聚合、运行表和消费者的 Targeting、Behavior、Perception 等领域，禁止预建万能空 Key。若内容仅属于 Action、Skill 或其他 Owner 的内部编排，应优先采用 Owner Key 加稳定局部 ID，而不是升级为全局 ConfigKey。

具体 GameCore 根、Info/Group 和资源反向依赖规则仍以 `GameCore边界（GameCore）` 下的现行 P0 为权威；本节只补充 AI 内容协议与新增 Key 的组合准入条件，不复制其注入实现。

## 当前实现入口

- 通用基础：`Assets/Plugins/ES/1_Design/ConfigKey/ESKeyCatalog.cs`
- GameCore/Asset 双键表：`ESConfigKeyTable<T>`
- Tag：`ESTagBakeTable`
- 属性：`ESSuperAttributeCatalog` / `ESSuperAttributeTable`
- 输入：`ESInputSchemeCatalog`、`ESInputActionCatalog`、`ESInputConfigSchemaHandshake`、`ESInputBindingProfile`
- 编辑器审计：`【ES】/项目设置/GameCore/审计项目稳定Key治理`，输出 `Documentation/KEY_AUDIT_REPORT.md`
- 总体规则：`Documentation/KEY_GOVERNANCE.md`

## 输入档案的 P0 例子

`ESInputBindingProfile` 只保存 `configId`、方案/动作 SchemaHash、稳定 `bindingId` 与 `schemeId`，绝不保存 RuntimeKey。旧档案只有在每条启用覆盖仍精确匹配当前 `bindingId + actionId + schemeId` 时才允许迁移；配置或 Schema 不匹配时必须拒绝，运行时使用当前配置的默认键位。

## 与其他最高警告的关系

本规则是所有业务稳定 Key 的总约束。资源加载仍额外遵守“Library 只属 Editor，Runtime 只认 Manifest/Table”及其 RuntimeKey 边界；GameCore 根 SO 仍额外遵守“KeyName 不是 GameCore Key”。这些规则互相加强，不允许以其中任一条作为绕过另一条的理由。
