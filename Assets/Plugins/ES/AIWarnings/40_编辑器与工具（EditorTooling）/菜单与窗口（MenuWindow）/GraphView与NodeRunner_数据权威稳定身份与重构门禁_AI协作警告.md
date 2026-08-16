# Stable Graph V2：数据权威、稳定身份与正式接入门禁

> 状态：现行约束 + Legacy 已删除事实 + V2 联调中。
> 最后核对：2026-08-16。

## 当前结论

历史实验实现 `Assets/Plugins/ES/Editor/ESGraphView` 与
`Assets/Plugins/ES/1_Design/Define/0Define-NodeRunner` 已删除，不再保留兼容入口、菜单、
运行接口或序列化类型。禁止恢复 `ESGraphViewWindow`、`NodeRunnerSO`、`NodeContainerSO`
或以新名称复制同一套可变 ScriptableObject Runner 方案。

正式图基础统一使用：

```text
ESGraphAssetBase + 具体 Graph 资产类型（唯一作者权威）
  -> Stable NodeId / PortId / EdgeId
  -> SchemaVersion + 显式迁移
  -> ESGraphEditService 原子编辑与 Undo
  -> ESBakedGraphSnapshot（验证快照 / 编译输入）
  -> 消费者专属不可变产物
  -> 受控执行或产物工作流
```

GraphView 只是具体 Graph 资产的 Editor 投影，不拥有业务数据。运行或交付链只能消费已验证的
Snapshot 或消费者专属产物，不得依赖 GraphView 元素、窗口静态状态、SerializedObject 或
UnityEditor API。不得预设所有 Graph 都必须生成 `Plan`、`Program` 或共用同一种运行产物。

## V2 已实现基础

- `ESGraphAssetBase` 保存稳定 Graph、Node、Port、Edge 身份和 SchemaVersion；具体资产类型固定 Domain。
- 创建、连接、插入、复制、粘贴、删除与节点编辑统一通过受控编辑服务和 Undo。
- 端口方向、类型、容量、重复边、循环和跨领域关系在模型层校验。
- Snapshot 与作者资产分离，并使用内容签名绑定候选、批准和后续执行。
- Agent Authoring 复用同一资产、Profile、编辑服务和窗口基础设施，但一张图只能选择“产物生成”或
  “AISkill 执行”一种模式；混合模式必须在校验和 Bake 阶段硬失败。
- 候选必须经过隔离生成、Diff Review、人工批准和哈希复核。
- Graph AI 候选生成和单次执行统一进入 `es.agent.generate@1`、`es.agent.use@1`；
  `ESAutomationFacade` 负责 RunId、输入 Hash、RunRecord 与发送回执，Graph 不得绕过该入口直连会话窗口。
- AISkill 持久化执行工作流源码已具备受信 TaskContract、超时和幂等重试限制、条件分支、串行 ForEach、
  人工批准、父子 AISkill 调用、八层深度与递归阻断、父子取消、结构化输出、持久化 RunRecord，以及
  受 Asset GUID、GraphId、内容签名和父链约束的恢复；这些仍是源码事实，不是 Unity 实机或商业验收结论。

这些是源码与静态结构事实，不自动等于商业级验收完成。

## Agent 双模式、端口与顺序唯一语义

Agent Authoring 只共享作者基础设施，不共享执行产物：

```text
产物生成图
  -> ESAgentArtifactGenerationSpec / ESAgentSkillBundleContract
  -> Candidate / Diff / Approval

AISkill 执行图
  -> ESAISkillExecutionSpec
  -> ESAISkillExecutionCoordinator / Automation Task
  -> ESAISkillWorkflowRun
```

- 产物生成图可以包含 Goal、Reference、Constraint、产物 Branch/Traverse、AICommand/AISkill Output 和
  Validation；产物 Branch 的 matched/default/failure 是生成需求传播关系，不得解释为 AISkill 运行控制流。
- AISkill 执行图可以包含 Input、Task、SkillCall、Branch、ForEach、Approval、FanOut、Join 和 Output。
  执行 Branch 的 matched/default 是两个独立 `Single` 出口；普通成功、失败、超时和取消出口同样各自为
  `Single`。只有 FanOut 的分发出口允许 `Multi`；Join 使用 `Multi` 输入并按完整端点身份汇合。
- `ESGraphEdgeRecord.order` 是所有图关系的唯一作者顺序。它必须进入迁移、Snapshot、内容签名、消费者
  Spec、恢复校验和 Undo/Redo；禁止再建立节点私有顺序表，禁止使用画布位置、数组偶然顺序或 `EdgeId`
  推断业务顺序。`EdgeId` 只负责身份，并仅可在非法重复顺序、迁移或诊断中充当确定性兜底。
- 新建边获得新 `EdgeId` 和新顺序；重连同一关系保留 `EdgeId` 和原顺序；调整顺序只能经过
  `ESGraphEditService` 的原子事务，失败不得 Dirty、保存或留下 Undo。

## `Program` 后缀唯一语义与 BehaviorTree 保留合同

`Program` 是 ESFramework 的保留后缀，当前且唯一归属 BehaviorTree。它不是 Graph 通用产物名，
不得被 Story、Agent、Generic、Command 或其他领域重新解释、复用或建立泛型包装。

正式定义：

> `Program` 表示由行为树作者数据经过完整校验、解析、链接和编译后生成的不可变运行产物。
> 它可由多个行为树运行实例共享，并可被指定 Runner 直接推进；执行时不得再解释作者 Graph、
> 解析 JSON、按稳定字符串查找处理器或为每个节点构造运行对象。

唯一合法根类型名为 `ESBehaviorTreeProgram`。该名称落地后必须同时满足：

- 只由 `ESBehaviorTreeGraphAsset` 的受控编译流程生成，只允许 BehaviorTree Runner 消费。
- 保存紧凑指令、稳定子节点顺序、整数索引、强类型 Payload、Blackboard 布局、实例状态布局、
  格式版本、源内容哈希和 Program 哈希。
- Bake 成功后逻辑不可变；内容更新必须生成新版本并在完整验证后原子替换。
- 多个实例共享同一 Program；当前节点、活动栈、等待、取消和 Blackboard 值必须保存在独立实例状态中。
- 热路径禁止 Graph 遍历、JSON、反射、LINQ、字符串注册、Dictionary 查找和临时集合分配。
- `Sequence`、`Selector` 等有序组合节点必须在作者模型中保存显式稳定顺序；禁止用画布位置、列表偶然顺序
  或 `EdgeId` 推断执行顺序。

`Program` 明确不是作者 Graph、验证 Snapshot、Bake 中间态、Definition、配置、Plan、运行实例、
Blackboard、调度器、Command 或一次性任务。禁止建立 `ESGraphProgram`、`IESProgram`、
`IESGraphProgram`、`Program<T>`、通用 Program Registry，以及 `ESBehaviorTreeProgramAsset`、
`ESBehaviorTreeProgramData`、`ESBehaviorTreeProgramPlan` 等重复包装。

BehaviorTree 配套名称固定为：

```text
ESBehaviorTreeGraphAsset       作者权威
ESBehaviorTreeCompiler         验证快照 / 编译输入 -> Program
ESBehaviorTreeProgram          共享不可变执行产物
ESBehaviorTreeInstance         一次独立运行及其可变状态
ESBehaviorTreeRunner           推进一个实例
```

`Bake` 是生成流程，`Compiler` 是生成者，`Program` 是最终执行产物。当前源码尚未实现
`ESBehaviorTreeProgram`、Compiler 或 Runner；本节是后续实现必须遵守的命名与语义合同，
不得据此宣称 BehaviorTree 运行链已经完成。

实例状态默认直接归 `ESBehaviorTreeInstance` 所有。只有出现可独立池化、序列化、版本迁移或内存布局校验的
真实边界，才允许增加独立 State 类型，禁止只为包一组数组创建 `ESBehaviorTreeInstanceState`。
行为树的多实例 Tick 应跟随实际 AI 消费者进入现有运行程序集；禁止预建 `ESBehaviorTreeScheduler`。
只有实现注册/注销、稳定顺序、遍历期间增删保护、预算和明确宿主执行，并符合项目 Scheduler P0 时，
才可重新评审是否使用 Scheduler 后缀。

## Story Definition Snapshot 唯一语义与商业门禁

Story 不生成 `Program` 或 `Plan`。现行运行语义继续使用 `ESStoryDefinitionSnapshot`：

> `ESStoryDefinitionSnapshot` 是一份 Story Definition 在确定 `DefinitionId + ContentVersion +
> ContentSignature` 下生成、与作者可变数据脱离、可被多个 Story 实例共享的不可变剧情定义快照。
> 它由事件驱动的 Story Module 按稳定节点关系推进，不是每帧解释的指令程序。

配套名称与职责固定为：

```text
ESStoryDefinitionDataInfo      DefinitionId、版本、目录注册和非图元数据
ESStoryGraphAsset              完成迁移后，节点、连接和节点 Payload 的唯一作者权威
ESStoryDefinitionSnapshot      共享不可变剧情定义
ESStoryDefinitionCatalog       稳定身份到已验证 Snapshot 的查询目录
ESStoryInstance                一次活动剧情运行及其临时状态
ESQuestRecord                  存档所需的最小稳定任务进度
MODULE_ESStoryModule           实例、前台/UI、推进、取消和存档接入权威
```

当前源码事实必须与目标合同分开：`ESStoryDefinitionDataInfo` 目前仍直接保存入口、节点和跳转，
并由 `ESStoryDefinitionSnapshot.TryBake(...)` 生成现行运行快照；`ESStoryGraphAsset` 尚未接入该链。
在显式迁移完成前，DataInfo 仍是现行 Story 作者权威，Graph 不得保存或发布同一份正式 Story 的第二套节点数据。
迁移完成后，DataInfo 只保留身份、版本、Catalog 和非图元数据，Graph 独占拓扑与节点 Payload；禁止两边可编辑同一字段。

Story 达到商业级定义必须同时满足：

1. 唯一权威：迁移器保留 DefinitionId、ContentVersion、NodeId、OptionId 和引用关系；迁移、回滚与重复执行可验证，
   不允许 DataInfo 与 Graph 双写或运行时猜测哪个较新。
2. 稳定身份：Snapshot 与存档精确绑定 `DefinitionId + ContentVersion + ContentSignature`；内容缺失、签名漂移、
   重复 Key 和未知节点必须硬失败，不得回退到显示名、列表下标或另一份 Graph。
3. 完整签名：哈希必须覆盖全部可观察运行语义，包括入口、节点种类、跳转、Action、Tag 条件、文本、选项内容
   及选项显示/执行顺序。当前工作树源码已改为按作者选项顺序写入 `OptionId`、目标节点、文本和本地化引用；
   该修正仍需签名版本/迁移策略、选项重排回归及 Unity Test Runner 证据，不能仅凭源码宣称签名合同商业级完成。
4. 受控生成：Graph 接入后的适配与 Bake 位于能够同时引用 `ES_Design`、`ES_Logic` 和 Editor 工具的现有
   Editor 程序集；Player 只消费已验证 Story Snapshot/Catalog，不依赖 GraphView、UnityEditor 或 Payload JSON。
5. 原子发布：先在隔离候选中完成结构、语义、引用、签名和存档兼容验证，再整体替换 Catalog 版本；失败保留上一份
   有效定义，不得部分更新节点或让活动实例观察到半成品。
6. 存档兼容：`ESQuestRecord` 只保存稳定身份、版本、签名、当前 NodeId、运行状态和 Revision。内容升级若需要继续旧存档，
   必须提供显式、逐版本、可审计的 NodeId/状态迁移；没有迁移时应拒绝加载，不得静默重置或跳到入口。
7. 事务推进：UI 提交、Action 回执、前台切换和迟到结果必须继续绑定 InstanceId、Revision、Session Generation、
   ViewRevision 和 NodeVisitSequence；失败、取消、场景切换、Presenter 异常和 Interaction 结束必须释放 Lease 并形成确定终态。
8. 运行性能：Story 保持事件驱动，禁止每帧从入口遍历或重新 Bake。Catalog 在初始化/内容切换阶段完成验证和注入，
   `TryStart` 只能解析已注入 Snapshot；当前工作树中的 `MODULE_ESStoryModule.TryStart` 已只调用 `TryResolve`，但仍须
   验证所有正式初始化入口不会在每次启动前重复 Inject/Bake。同步无等待推进必须有步数上限；节点查询为 O(1)，
   正常推进不得使用反射、LINQ 或无界临时集合。
9. 验收证据：必须覆盖 Graph/DataInfo 迁移、签名顺序、重复身份、不可达/循环、迟到 UI/Action、取消、存档跨版本、
   原子失败恢复、多实例前台队列、Domain Reload、Player/IL2CPP，以及深图和批量实例 Profiler/GC。静态编译或测试源码存在
   不能替代 Unity Test Runner、PlayMode 和 Profiler 证据。

当前 Story 已有 DataInfo、Snapshot、Catalog、Instance、QuestRecord 和 Module 运行骨架；签名顺序与 TryStart 只读解析的
源码修正已经形成，但 Graph 接入、签名迁移/回归、Catalog 初始化闭环、作者权威迁移和上述 Unity/Player/性能证据仍未完成；
准确成熟度仍为 `Verifying`，不得标记商业级或 Stable。

## 正式业务接入门禁

1. V2 相关 Unity 程序集成功编译并完成 Domain Reload。
2. Graph 核心、Agent Authoring 与 AISkill 持久化执行测试必须由 Unity Test Runner 实际执行，不得只以测试源码存在签收。
3. 两种模式必须分别完成真实闭环，不得用一条混合链互相冒充：
   - 候选生成：`Graph -> Bake GenerationSpec/Bundle -> es.agent.generate -> Automation RunRecord/发送回执 ->
     Candidate -> Diff -> Approval -> 独立实现 Launch Envelope/接收回执`。
   - 单次使用：`Graph -> Bake GenerationSpec/Bundle -> es.agent.use -> Automation RunRecord/发送回执`。
   - AISkill 执行：`Graph -> Bake ExecutionSpec -> 参数输入 -> Coordinator -> Task/子 Skill/分支 -> 人工确认 ->
     结构化输出 -> WorkflowRun`，并覆盖取消与恢复。
4. 多 Graph、内容签名 stale、取消、失败注入、Domain Reload 和跨窗口恢复必须有可复现证据。
5. Graph 窗口关闭时不得保留扫描、更新或重绘负担；深图验证不得递归爆栈。
6. 旧 Graph/NodeRunner 类型和路径不得重新进入源码、link.xml、资产指南或正式文档。
7. 缺少完整运行证据时成熟度最高为 `Verifying`，不得称为 `Stable` 或商业级已验收。

## AI 能力工作流边界

必须区分作者资产、执行合同、运行协调器和编辑器入口的权威：

```text
ESAgentAuthoringGraphAsset       作者数据唯一权威，不保存运行态
  -> Baked Spec                 不可变执行或生成输入
  -> Coordinator / Automation  运行生命周期、TaskContract 与 RunRecord 权威
  -> Inspector                  发起受控命令并投影结果
```

Graph Inspector 可以通过公开受控入口发起生成、启动、审批、取消或打开证据，并投影自动恢复结果，但不得直接改写
`ESAutomationRunRecord`、`ESAISkillWorkflowRun` 或绕过 TaskContract。所有命令和投影必须继续绑定
GraphId、ContentSignature、Asset GUID、BundleId/ArtifactId、RunId 及对应 Hash。
运行结果不得自动修改 Graph；任何改进必须由用户确认后重新编辑、Bake 和批准。
