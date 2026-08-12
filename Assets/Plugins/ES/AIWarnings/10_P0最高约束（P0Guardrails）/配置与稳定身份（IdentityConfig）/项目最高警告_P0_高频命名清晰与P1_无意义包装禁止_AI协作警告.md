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
| 设置当前或下一步使用的值 | `SetDriverInput`、`SetCameraLook`、`SetPendingShotResult` | `SubmitDriverInput`、`SubmitCameraLook`、`SubmitShotResult` | 若实现只是校验后覆盖字段，`Set` 更准确；可能拒绝时使用 `TrySet...` |
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
- `ESTagCollection.Acquire(...)` 与 `TryAcquireField(...)` 可以保留：它们产生需要释放、带代际保护的 Lease，`Acquire` 表达了真实的生命周期所有权。
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

### 已存在但尚未迁移的语义债务

以下名称已经进入资产、序列化或大范围 API，本轮只登记风险，禁止后续代码继续照抄；迁移时必须单独设计兼容和资产升级：

- `ActionTemplateDataInfo` / `ActionTemplateDataGroup` 实际是会注入 `ESActionGameCoreTable` 的正式 Action Definition，不是可随意复制的作者模板。新类型不得继续使用 `*TemplateDataInfo` 表示正式 GameCore 定义；后续应评估迁移到 `ActionDefinitionDataInfo/Group`。
- `ESWeaponSceneTemplate.RuntimeBridgeSection` 当前只是模板内的作者引用分组与接入说明，不是独立 Runtime Service，也不拥有运行时生命周期。禁止据此新增 `*RuntimeBridge` 转发包装；未来若重命名，应按序列化字段迁移处理。
- `ESWeaponTemplateFireKind` 只描述模板的射击/弹道作者提示，却容易被误解为正式武器种类。正式近战/远程身份只认 `ItemWeaponSharedData.weaponKind`；新工具不得用 Template FireKind 替代 WeaponDefinition。
- `EntityWeaponBinding.fireOrigin`、`fireStateKey` 是远程执行字段，不是通用 Attack Origin/State。近战命中若需要独立参考，应新增语义明确且由 Action 消费的字段，不能偷偷复用“开火”字段后宣称已接线。

### 项目复杂用词扫描：首轮候选

本节是 2026-08-13 的只读首轮源码扫描结果，只登记已经回看实现的候选。它不是批量改名授权，也不能仅凭单词命中判定违规。`Obsolete`、`Generated`、测试名、第三方接口实现和 Unity/KCC/Odin 固定回调默认不进入业务 API 改名范围。

| 候选 | 当前实现事实 | 初步等级 | 建议方向 |
|---|---|---|---|
| `VehicleController.SubmitDriverInput(...)` | 校验当前驾驶者后只调用 `inputState.Set(...)`；属于每帧角色到载具的高频输入 | P0 新代码禁止继续扩散；既有改名需单独迁移 | 优先评估 `TrySetDriverInput(...)`；若上层已保证驾驶权且无需返回拒绝结果，可用 `SetDriverInput(...)` |
| `EntityMountable.SubmitDriverInput(...)` | 校验当前 Rider 后转发给 VehicleController，同样是每帧高频入口 | P0 新代码禁止继续扩散；与上一项成套迁移 | 与载具端统一为 `TrySetDriverInput(...)`，避免两层都暴露协议化动词 |
| `Entity.SubmitCameraLook(...)` | 检查本地控制权后调用 Lease 的 `TrySetLook(...)`，本质是高频输入设置 | P0 新代码禁止继续扩散；既有改名需检查输入调用链 | 优先评估 `TrySetCameraLook(...)`，调用点能直接理解“设置镜头观察输入” |
| `ItemMotionModule.SubmitShotResult(...)` | 只把 `ShotMotionResult` 写入 `_pendingResult` 并设置 `_hasPendingResult` | P1 命名债务；若成为跨模块日常入口则升级 P0 | 优先评估 `SetPendingShotResult(...)`；若结果会立刻进入合成阶段可评估 `ApplyShotResult(...)` |
| Track Editor Preview 的 `SubmitClipState(...)` | 缓存原始 Active，合并同一采样帧多个 Clip 请求并写最终 Active | 暂不判违规 | `Submit` 有“多来源请求合并”事实，但名称未说明仲裁对象；后续评估 `SetClipActiveRequest(...)` 或保留并补充职责说明 |
| `MatchTargetGizmosDrawer.Submit(...)` | 同一 Key 每帧覆盖诊断数据，属于开发诊断写入，不是玩法业务入口 | P2 可读性债务 | 可评估 `SetFrameData(...)`；不应与玩法 API 同优先级处理 |
| `ESMotionInfluenceReceiverResolver.TryResolve(...)` | 在目标父级 Core、VehicleController 和显式 Receiver 间按权威顺序查找接收器 | 合理 | 这里确实存在多来源解析和优先级，且 Resolver 被隐藏在 `ESMotion.AddVelocity(...)` 业务入口之后 |
| `ESStoryDefinitionCatalog.TryResolve(...)` | 通过稳定 Key、内容版本和签名解析不可变 Snapshot | 合理 | `Resolve` 表达跨稳定身份到运行时定义的解析，不应机械改成 `Get` |
| `ESTagCollection.Acquire(...)`、`TryAcquireStringKey(...)` | 创建 generation-safe Lease，调用方必须释放 | 合理 | `Acquire` 表达真实所有权；改为 `AddTag` 会丢失释放义务 |
| `VehicleController.RegisterMotionFeature(...)` | 按阶段向多个 `ESWorkScheduler` 注册能力并返回可释放 Registration | 合理但应关注高频入口文案 | 注册与注销生命周期真实存在；无需为了简单词改成 `AddFeature` |
| `VehicleController.ProcessHitStabilityReport(...)` | KCC 接口规定的固定回调签名 | 外部合同，不纳入改名 | 不得为满足本规则破坏 KCC 接口 |

后续扫描顺序：

1. 先处理 Runtime 业务调用面中的 `Submit/Process/Execute/Resolve/Acquire/Commit`；
2. 再检查 Inspector、菜单和 AICommand 的可见文案，优先于私有实现名；
3. 最后审查 `Manager/Bridge/Context/Request/Result/Payload` 等类型后缀是否有真实职责；
4. 每个候选必须记录声明、实现、调用频率、唯一权威、序列化影响和推荐名，不能直接全局替换。

## 语义优化计划：分级迁移，不把审计扩大成重构

本计划只确定顺序、风险和验收门禁，不授权自动执行后续大范围改名或职责拆分。每一阶段必须独立评审、独立验证；上一阶段的审计结果不能直接当作下一阶段的修改许可。

| 阶段 | 优先级与范围 | 序列化风险 | 主要风险 | 进入下一阶段前的门禁 |
|---|---|---|---|---|
| A：安全纠偏 | P0，已处理 `EntityPrimaryAttackRouter -> EntityPrimaryAttackSelector`、`GunWeaponSlot -> WeaponSlot`、`IItemShotTickScheduler -> IItemShotTickPolicy` | 不改变现有资产字段结构；脚本改名必须保留 `.meta` GUID | 遗漏源码、测试或文档引用；Prefab 反序列化异常 | 可执行源码与现行说明不再引用旧符号（迁移记录除外）；Runtime、Editor、Tests 静态编译通过；Selector EditMode 测试通过；正式角色 Prefab 可加载 |
| B：Action Definition 正名 | P1，评估 `ActionTemplateDataInfo/Group -> ActionDefinitionDataInfo/Group` | 高；涉及 ScriptableObject 脚本身份、Group 泛型、Picker、GameCore 注入与现有 `.asset` | Missing Script、资产类型丢失、创建菜单或 Table 扫描分叉 | 修改前先统计全部 Action 资产与引用；设计 `MovedFrom`/GUID 保留方案；准备迁移器与回滚备份；迁移后逐资产加载、GameCore 重建和 EditMode/PlayMode 验证 |
| C：Weapon 模板字段正名 | P1，先确认 `RuntimeBridgeSection` 是否仅为接入引用分组；确认 `ESWeaponTemplateFireKind` 是否只表达模板布局提示，再决定 `IntegrationReferencesSection`、`ESWeaponTemplateLayoutKind` 等最终名称 | 中；嵌套序列化字段和枚举值已进入 Prefab | 只改类型名却丢字段、枚举含义继续与正式 `ItemWeaponKind` 混淆、构建器与旧 Prefab 分叉 | 先做使用点与 Prefab 扫描；字段改名必须有 `FormerlySerializedAs`，类型迁移按 Unity 版本验证 `MovedFrom`；构建器重复执行哈希稳定；全部 Weapon Prefab 严格校验通过 |
| D：远程/近战字段边界 | P1，`fireOrigin`、`fireStateKey` 继续保持远程专用；只有近战 Action 出现真实消费需求时，才新增明确的近战命中参考 | 新字段通常低；若搬迁既有 Binding 字段则高 | 为复用挂点把近战伪装成开火，或建立一套没有消费者的“通用攻击绑定” | 先取得近战 Action、命中采样和伤害消费者证据；新增字段必须有唯一消费者、Prefab 作者校验与 PlayMode 命中证据；禁止仅因模板已有节点就宣称接线 |
| E：Combat 职责拆分评估 | P2，审计 `EntityBasicCombatModule` 中装备切换、挂载、远程射击和状态表现的生命周期；只在出现独立权威与可独立测试边界后拆分 | 很高；角色 Prefab、运行时状态和调用链均可能受影响 | 新增万能 Manager/Dispatcher/Scheduler、双权威、切枪与攻击状态不同步 | 先形成调用图、状态所有权表、池化/销毁顺序和 PlayMode 基线；评审通过后再决定是否拆分及最终名称，不预建空接口或兼容包装 |

执行约束：

1. 阶段 A 之外的改名不得与玩法功能开发混在同一批修改中。
2. 涉及 Unity 序列化的阶段必须先退出 PlayMode，并把 before 基线放入 `ES/Bak/Local` 或 `ES/Bak/Reviewed`；是否入 Git 按备份分层规则决定。
3. 迁移必须验证资产反序列化、GameCore 注入、构建器幂等和对应 PlayMode 行为；`dotnet build`、MCP clean 或菜单成功都不能单独作为完成证据。
4. 审计发现名称可疑时先登记债务，不得顺手创建 `WeaponManager`、`EquipmentScheduler`、`AttackDispatcher` 或第二套输入/攻击链。

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
