# 项目最高警告：P0 高频命名清晰；P1 无意义包装禁止

职责：保护策划、业务开发和 AI 日常高频接触的配置、Inspector、菜单与公共 API 的可理解性；同时防止为“看起来整齐”而产生没有职责的类型层级。

## P0：高频入口必须使用直观、常用的名称

以下入口属于高频入口：GameCore 配置字段、Inspector 文案、Picker、菜单、AI 命令、策划会填写的资产字段，以及业务层日常直接调用的公共类型和方法。

- 禁止用需要翻译、猜测或查词才能理解的生僻英语组成高频功能名。
- 名称必须让目标使用者直接看出“它是什么、何时使用、会产生什么效果”。优先项目已有常用词和简单动词，例如 `Apply`、`Add`、`Remove`、`Tags`、`Condition`。
- 底层私有实现可保留必要技术术语；但它不得泄漏为策划字段、Picker 文案或业务层日常入口。
- 不能以“代码更短”“英文更专业”作为保留生僻名的理由。

违反本条为 P0：阻止新增、重命名和交付。除非用户明确要求兼容，否则旧名不得作为新 API 的兼容别名继续扩散。

### 高频动词选择：先说业务效果，再说内部协议

高频方法名应优先描述调用者能观察到的直接效果。不能因为内部存在队列、仲裁、缓存、接收器或返回码，就把这些实现细节抬升为业务入口名称。

| 实际语义 | 高频推荐 | 通常不推荐 | 判定理由 |
|---|---|---|---|
| 给现有数值增加一段量 | `AddVelocity`、`AddTag`、`AddItem` | `SubmitVelocityDelta`、`DispatchVelocityChangeRequest` | 调用者关心“增加了什么”，不应先理解提交协议 |
| 设置当前或下一步使用的值 | `SetMoveInput`、`SetLookInput`、`SetPendingResult` | `SubmitValueMutationRequest`、`ProcessPendingValueSubmission` | 若实现只是校验后覆盖字段，`Set` 更准确；可能拒绝时使用 `TrySet...` |
| 立即把规则或效果作用到目标 | `ApplyDamage`、`ApplyMotionInfluences`、`ApplyDefinition` | `ExecuteDamageApplication`、`ProcessMotionInfluenceRequest` | `Apply` 已能表达“根据规则作用”，无需重复包装成执行协议 |
| 清除、释放或移除已有内容 | `ClearDriverInput`、`ReleaseDriver`、`RemoveTag` | `InvalidateDriverInputSubmission`、`ProcessDriverRelease` | 直接说明结果和对象，避免抽象名词化 |
| 查询并返回一个值 | `GetAnchorPosition`、`TryGetTransform` | `ResolveAnchorPosition`、`ExecuteTransformResolution` | 单源读取或简单二选一通常是 `Get/TryGet`；只有真正跨来源消歧时才使用 `Resolve` |
| 从明确候选中选一个结果 | `SelectPrimaryAttack`、`TrySelectTarget` | `DispatchPrimaryAttack`、`ScheduleTargetDecision` | 选择本身不执行副作用，也不拥有调度生命周期 |
| 持续资源或能力租约 | `AcquireTag`、`TryAcquireField`、`ReleaseDriver` | 强制改成 `Add/Remove` | `Acquire` 合理的前提是确实返回或建立必须释放、可失效且有代际保护的所有权 |
| 事务最终落定 | `TryCommit` | 强制改成 `Apply` | `Commit` 合理的前提是存在预检、版本戳/事务边界、失效拒绝以及失败恢复或不落地保证 |
| 把完整输入交给独立权威接受或拒绝 | `SubmitContinue`、`SubmitOption` | 强制改成 `Set` | `Submit` 合理的前提是输入携带会话、版本或身份，接收方会验证并可能拒绝过期输入 |
| 把确定事件送给既定接收者 | `DispatchChanged`、`DispatchMessage` | 用于普通字段赋值或能力选择 | `Dispatch` 只在存在明确接收者集合、派发顺序或遍历期增删规则时成立 |

`Try` 不是复杂度装饰。方法有可预期的业务拒绝分支并通过 `bool`/`out` 表达时，应优先使用 `TrySet...`、`TryGet...`、`TryAttach...`；无拒绝语义时不得为了显得安全机械添加 `Try`。

### `Submit` 的允许边界

`Submit` 不是禁用词。满足以下大部分条件时可以保留：

1. 调用方提交的是一份完整输入，而不是单个待赋值字段；
2. 接收方是该输入的独立权威，会做身份、代际、版本、会话或事务校验；
3. 提交可能因过期、冲突、权限或当前状态被拒绝；
4. 接受后会推进独立状态机、事务或异步流程，而不只是把值复制到另一个字段；
5. 调用者确实需要理解“提交并等待权威裁决”这层语义。

反之，只要实现主体接近 `field = value`、`pending += value`、`dictionary[key] = value` 或一次直接转发，业务入口通常不应使用 `Submit`。

已核实案例：

- `ESMotion.AddVelocity(target, velocity)` 是正确的高频业务入口。它表达技能、Zone 或碰撞想要的直接效果；接收器解析、权限检查和最终运动权威仍可留在底层。
- `ESStoryModule.SubmitContinue(ESStoryViewSubmission)` 与 `SubmitOption(...)` 可以保留：Submission 携带 Instance、View Session、Revision 与选项身份，模块会拒绝旧视图或错误节点，并在接受后推进 Story 状态。
- 自动化系统的 `SubmitInput(ESAutomationTaskInputSubmission)` 可以保留：它跨 Facade/Endpoint 权威边界，输入带运行身份和 Schema 约束，并存在明确的接受、拒绝和等待继续流程。
- `EntityEquipmentAttachmentModule.TryCommit(...)` 可以保留：它校验四项 Transition Stamp，在挂载前后复核代际，并在中途失效时恢复原父级和局部变换。这是真正的提交阶段，不是一次普通 `SetParent` 包装。
- `ESTagCollection.Acquire(...)` 与 `IESMotionInfluenceReceiver.TryAcquireField(...)` 可以保留：它们分别产生需要释放、带代际保护的 Tag Lease 与 Motion Field Lease，`Acquire` 表达了真实的生命周期所有权。
- 底层 `DispatchVelocity(...)` 可以保留为私有实现：它遍历已注册的运动能力并遵守确定顺序；但不得直接作为技能或策划高频入口。

## P0：核心架构词具有项目语义所有权

高频架构词不是装饰性后缀。名称一旦使用，就必须承担项目内已经约定的职责；禁止把普通条件判断、单次转发或一个 `switch` 包装成更重的架构概念。

| 术语 | ES 项目中的唯一语义 | 禁止误用 |
|---|---|---|
| `Scheduler` / 调度器 | ES 框架拥有的可注册工作调度语义：至少具备任务注册/注销、稳定顺序、遍历期间增删保护，并由宿主明确执行；通用实现是 `ESWorkScheduler<TTask>` | 单个 `ShouldTick` 条件、一个枚举 `switch`、一次方法转发、按类型选择分支 |
| `Program` / 执行程序 | 当前且唯一归属 BehaviorTree：由行为树作者数据完整校验、解析、链接和编译后生成，可由多个实例共享并被指定 Runner 直接推进的不可变紧凑执行产物；唯一合法根类型名为 `ESBehaviorTreeProgram` | Story、Agent、Generic、Command 或其他领域的产物；作者 Graph、Snapshot、Definition、Plan、实例状态、调度器；`ESGraphProgram`、`IESProgram`、`Program<T>` 或重复 Asset/Data/Plan 包装 |
| `Compiler` / 编译器 | 把已验证的作者数据或中间表示解析、链接并生成可直接消费的不可变产物；负责完整诊断和产物校验，不执行产物、不拥有运行实例 | 普通字段复制、单次序列化、运行时分支选择、Runner、Scheduler 或只转发一个 Bake 方法的包装 |
| `Runner` / 运行驱动器 | 推进一种已验证运行单元或其活动实例集合，维护明确的开始、推进、完成、取消和异常边界；不拥有作者数据或内容编译权威 | 作者窗口、GraphView、Compiler、一次方法转发、只有 `Update()` 名称但没有运行生命周期的包装 |
| `Snapshot` / 快照 | 从明确权威源在确定时点、版本或签名下取得，并与后续源修改脱离的只读状态或定义视图；用于共享读取、诊断、传输或恢复校验，不接受业务写入 | 可变作者数据、活动实例、持续自动同步的 ViewModel、无复制/冻结边界的集合别名、伪装成最终执行 Program |
| `Dispatcher` / 派发器 | 把已经确定的消息或事件送达一个或多个既定接收者；不得伪装成能力仲裁器或工作调度器 | 用 Action 是否注册、执行是否失败来决定落入另一个武器后端 |
| `Router` / 路由器 | 依据稳定输入选择唯一目的地或执行类别；不注册任务、不拥有更新循环、不执行目标副作用 | 持有任务生命周期、排序额度或偷偷执行 Transform/物理写入 |
| `Selector` / 选择器 | 从明确候选中返回一个选择结果；不负责执行结果对应的副作用 | 包装执行链、持有任务生命周期，或用执行失败偷偷改选其他能力 |
| `Policy` / 策略 | 只回答规则判断或参数选择，例如 `ShouldTick`、是否允许、优先级计算 | 自称调度器、注册中心或运行时权威 |
| `Definition` / 定义 | 可复用作者配置或注入后的共享定义；不保存单个实例的临时状态 | 写入弹药余量、当前目标、冷却剩余、Transform 或运行副作用 |
| `Template` / 模板 | 作者结构、默认布局和挂点骨架；存在字段或节点不代表运行时已经接线 | 当作 GameCore Definition、运行时执行器或可玩证据 |
| `Binding` / 绑定 | 明确连接已有对象、挂点、状态或资源引用；不拥有业务规则权威 | 在 Binding 中复制伤害、射击、输入或生命周期系统 |
| `Table` / 表 | 稳定 Key 到已验证运行时数据的权威映射，遵守注入和重复 Key 规则 | 用显示名、Prefab 名或层级扫描临时拼表 |
| `Registry` / 注册表 | 接受显式注册并维护重复、移除和生命周期规则 | 只读常量列表、一次性搜索工具或无状态转发类 |
| `Catalog` / 目录 | 面向发现、选择或查询的集合视图；通常不接管实例生命周期 | 冒充动态注册表、GameCore 注入表或执行系统 |

`Scheduler/调度器` 的架构语义由 ES 框架独占。新玩法代码需要调度能力时，应复用 `ESWorkScheduler<TTask>` 或先证明自己具备同等调度契约；否则必须使用 `Selector`、`Router`、`Policy`、`Resolver`、`Executor` 等与真实职责一致的名称。不得仅为了显得可扩展而新增 `WeaponScheduler`、`AttackDispatcher`、`TickScheduler`。

已确认的纠偏示例：

- 主攻击选择器只返回“执行类别 + 攻击来源”：武器使用 `EntityPrimaryAttackSelector.Select(...)`，徒手使用 `SelectUnarmed(...)`，双持组合技使用 `SelectPairedWeapons(...)`；Combat 的副作用入口仍是 `TryExecutePrimaryAttack()`。选择器不拥有玩法状态或效果决策，不执行 Action 或射击，也不是 Router、Dispatcher 或 Scheduler。
- Shot 每帧是否 Tick 只是规则判断，使用 `IItemShotTickPolicy`，不得恢复 `IItemShotTickScheduler`。
- 武器槽位同时承载近战和远程，使用 `WeaponSlot`，不得恢复会把近战伪装成枪械的 `GunWeaponSlot`。

### 审查流程与动态记录边界

P0 正文只保存长期稳定的语义、禁止事项和判定流程。带扫描日期、候选数量、具体改名建议、迁移阶段或当前验证状态的内容必须放入独立审查报告；`CurrentStatus` 只登记报告入口和当前阶段。动态候选不得反向成为永久禁词。

项目扫描必须遵守以下流程：

1. 先识别入口是否真的由策划、业务代码或 AI 高频接触，私有实现和外部固定合同不得混入同一等级；
2. 同时检查声明、实现和调用点，禁止仅凭 `Submit`、`Resolve`、`Manager` 等单词命中判错；
3. 记录唯一权威、调用频率、拒绝语义、生命周期、序列化资产和兼容影响；
4. 将结果分为“确认合理”“待复核候选”“已确认问题”，候选不能写成已经违反 P0；
5. 成对协议、接口实现和调用链必须整体评估，不得只改其中一个符号；
6. 批量改名、兼容别名、序列化迁移和源码修改均需单独授权，审查报告本身不提供实施权限；
7. UTF-8、`git diff --check`、静态编译和会话上下文验收只证明各自范围，不能代替命名判断或 Unity 迁移验收。

当前动态候选和迁移建议统一记录于 `ES/Documentation/Status/API_NAMING_REVIEW_20260813.md`。

### 既有命名问题分级

| 等级 | 情况 | 默认处理 |
|---|---|---|
| A | 策划字段、Inspector、菜单、业务高频 API 明显难懂 | 优先整改；先保证使用者可直接理解，再处理低频内部名称 |
| B | 公共协议名与真实职责不符，可能误导后续架构 | 按完整调用链迁移；声明、实现、调用方、测试和现行文档必须同步评估 |
| C | 内部或低频名称不够好，但职责尚可理解 | 登记；只在相关代码本来就要修改且风险可控时顺带处理，不单独扩大重构 |
| D | 私有实现、第三方回调、生成代码、历史代码 | 默认不动；外部固定合同不得为了项目命名风格强行改写 |

等级描述的是影响面和处理顺序，不是对单词的永久定罪。同一符号可以同时命中 A 与 B，此时按更严格的完整调用链迁移；D 类若泄漏成现行高频入口，必须重新分级。

## P1：禁止无职责包装、无意义嵌套

禁止以下结构：

- 只包一个字段的 Config / Data / Info / Runtime 类型；
- 只做转发、不维护自身不变量或生命周期的中间类型；
- 为“以后可能有字段”预留的嵌套；
- 同一配置在外层和内层各保存一次，形成双权威。

新类型只有在至少具备一项独立职责时才允许建立：

1. 维护多个字段共同的不变量；
2. 拥有独立生命周期或资源释放边界；
3. 承担版本迁移或稳定序列化边界；
4. 提供不能由调用方安全重复完成的独立验证。

否则，字段应直接放在真正拥有它的对象上；容器、资产、运行时对象之间也不得为转发而再套一层。

违反本条默认是 P1：评审警告，必须删除或证明其独立职责。若该包装暴露在高频配置/API、引入双权威、掩盖生命周期或使配置者无法直观看到实际数据，自动升级为 P0。

## Tag 的明确示例

`ESTagGrantConfig` 是本规则的既有反例，现已删除：`Grant` 不适合作为高频配置词，且若它只包一份 `tags` 列表，就不应存在这个额外层级。

- 新代码禁止恢复 `ESTagGrantConfig`，也禁止创建 `EntityRuntimeFactConfig` 一类只包装 Tag 配置的类型。
- 写入者直接在自己的真实配置对象上持有 `List<ESTagStableReference> tags`。Host 自身幂等事实使用 `Tags.SetTag(tag, active)`，不创建句柄；Buff、装备、区域等外部生命周期使用自己的 `ESTagLeaseSet`；只有确实需要单独释放时才使用 `Tags.Acquire(...)` 返回公开 Lease。禁止用无来源的聚合 Count 加减代替这三层边界。
- `ESTagStableReference` 的 Inspector 只能走统一 `ESTagStableReferenceDrawer + ESSearchDropdown`。禁止在某个 DataInfo 上恢复 Odin `ValueDropdown`、`GetTagOptions()`，或复制一套 Tag 选项缓存；这会造成 Picker 规则、显示文案和 Catalog 可用性分叉。
- 只有未来确实出现多字段整体校验、版本迁移或独立生命周期，才可建立新类型；命名必须使用高频可懂词，并在提交中写明其独立职责。

## 生命周期命名与边界示例

`ESGenericLife` 的职责是组织根对象的生命周期分部、校验根接收者并管理显式注入的扩展；它不是 Pool、Entity 或 Item 的重复包装。

- Pool 分部直接使用 `IESGameObjectPoolLifecycle` 与 `OnPoolSpawned/OnPoolDespawned`。这些名称说明了调用者和时机，禁止改成难以判断来源的 `IESGenericLifeHandler`、`Run`、`Apply` 等泛化高频入口。
- 不要为 Root 和 Extension 暴露两套只有命名不同的回调接口。两者都实现同一个 Pool 协议；差异只在 `ESGenericLife` 的注册角色与派发顺序。
- 允许 Extension 按具体类型唯一注册，解决“Entity 不应直接引用所有扩展”的问题；禁止为此再添加只转发列表的 Config/Manager/Bridge 包装。
- `IESGameObjectPoolResettable` 是旧的全子树广播思路，已废止，禁止恢复或以新名称复刻。

## 审查问题

新增或重命名前逐项回答：

1. 策划或业务开发者第一次看到这个名字，是否无需翻译就知道用途？
2. 去掉这个中间类型后，是否丢失真实不变量、生命周期、迁移或验证？
3. 此数据的唯一权威是否仍在真正拥有它的对象上？
4. 类型后缀是否真的满足上表的项目语义，而不是为了“听起来像架构”借用了更重的词？

任一答案不清楚时，不得新增该高频入口或包装层。
