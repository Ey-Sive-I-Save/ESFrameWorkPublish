# ES Automation 治理合同（兼容式扩展）

本文档定义 ES Automation 在保留既有 TaskContract、Facade、Worker 和 Receipt 行为基础上的商业级扩展。

## 兼容原则

- 旧 TaskContract 不要求立即填写治理字段，现有 Worker 入口保持不变。
- 新任务可以声明 `acceptanceCriteria` 和 `capabilityEnvelope`。
- 迁移期间先观察（Observe），再告警（Warn），最后对高风险任务启用强制门禁（Enforce）。
- `Completed` 只表示执行器结束；`Accepted` 必须由外部 CompletionDecision 确认。

## 核心合同

`ESAutomationAcceptanceCriteria` 为每个必要验收项绑定稳定的 `criterionId` 和独立 `verifierId`，并允许声明禁止条件。

`ESAutomationCapabilityEnvelope` 的有效权限为：

```text
UserAuthorization ∩ TaskContract ∩ AICommand ∩ WorkerCapability ∩ ProjectBoundary
```

`ESAutomationExecutionSnapshot` 绑定输入清单、源文件、TaskContract、AICommand 和 AIBrain PlanHash。Worker 完成后应重新计算关键哈希，发现漂移不得进入 Accepted。

`ESAutomationCompletionDecision` 只有在执行状态为 `Passed` 时才可能 Accepted，同时检查正向验收项和负向禁止条件，包括越权调用、过期证据、冲突证据、源漂移、预算违规和 Trace 未对账。
`CanAccept()` 会先执行自身结构校验；格式不完整或 RunId 无效的决定即使包含通过项，也只能得到 `false`。

Facade 注册时若已存在同身份的 TaskContract，Endpoint 必须实现 `IESAutomationContractBoundEndpoint`，并且 Descriptor 的任务 ID/版本必须与合同完全一致；未绑定的 Endpoint 不会进入可发现列表。

ES 原有 `TaskDescriptor.inputSchemaHash` 现在可以由 `TaskContract.inputSchemaHash` 进行可选绑定。两者均声明时必须完全一致；历史任务留空即可继续运行。这样保留 ES 既有输入发现机制，同时避免 AI Brain 看到的输入合同与 Worker 实际执行合同发生语义漂移。

内置 `es.scene.scan` 已启用这条绑定：TaskContract、Facade Descriptor、受管 Schema 文件和 Worker 的输入检查点共同使用同一 `OptionsSchemaHash`；Schema 文件发生漂移时，既有 Worker 指纹检查和注册门禁都会拒绝继续执行。

AIBrain 的一次性授权指纹现在同时绑定任务、版本、输入、调用身份、幂等键和可选 ExecutionSnapshot。快照或幂等键发生替换时，即使仍使用同一个 PlanHash，也会被授权消费门禁拒绝。

`ESAutomationRunResult.Validate()` 是 Receipt 的第一道结构门禁：它会校验时间单调性、重试计数、幂等键格式、ExecutionSnapshot，以及 CompletionDecision 与 RunId 的绑定。它不能替代业务 Verifier，但可以阻止格式合法、语义却串 Run 的收据进入写入边界。

新任务可额外提供 `ESAutomationTraceReconciliation`。它记录预期调用数、实际调用数、越权调用数和重复调用数；一旦提供，就必须满足逐项对账，不能只把 `traceReconciled` 布尔值写成 `true`。历史任务仍可使用兼容的布尔字段。

`ESAutomationPerformanceBudget` 为任务声明最大时长、输出体积、重试次数和发现数量；`ESAutomationFreshnessPolicy` 定义证据最大年龄和源哈希要求；`ESAutomationClaimEvidenceBinding` 将业务声明绑定到具体 Criterion、证据哈希、源哈希和捕获时间。

输出体积预算由治理层按 RunId 下的 Reports/Temp 受控目录实际读取文件大小，而不是信任 Worker 上报的数字；缺失、越界或无法读取的声明输出会被视为不可验证。

ReleaseGate 还会将每个 `RunResult.outputs` 与 `TaskContract.outputs` 对照；未声明的文件名或路径不能进入发布结果。历史合同未声明输出清单时保持兼容观察模式。

TaskContract 注册时会拒绝空输出、重复输出、绝对路径和 `..` 路径穿越；输出合同只能声明 Run 目录下的相对产物名。

输入声明也在同一协议层接受空值、重复项、绝对路径和 `..` 路径穿越检查；空输入清单仍兼容历史任务。这样 Worker 的输入/输出边界都先经过合同层筛选，再进入具体 ES Endpoint 的路径根目录门禁。

输入和输出声明按 Windows 文件系统的大小写不敏感规则去重，避免 `Result.json` 与 `result.json` 在合同中被当作两个实际不同的产物。

ReleaseGate 的目录精确匹配也采用同一大小写不敏感语义，保证合同注册、文件系统和最终发布判定一致。

报告恢复也必须经过 `ESAutomationReportCenter.TryReadJson`：它会验证受管 Reports 根目录、严格 UTF-8、RunId 目录绑定和完整 `RunResult.Validate()`，损坏或被替换的 JSON 不会被恢复流程当作有效收据。

`CapabilityEnvelope` 的五段交集仍由 ES Facade 在每次调用时计算；现在还会拒绝未知能力位，避免未来枚举扩展或外部 JSON 注入未定义权限被静默解释。

对带 `AcceptanceCriteria` 或 `PerformanceBudget` 的严格任务，`ESAutomationReleaseGate` 还会重新读取每个受管输出文件并比对 `RunResult.outputHashes`；历史未启用治理合同的任务继续保留兼容观察模式。

输出声明匹配也区分两种兼容语义：合同只声明文件名时保留文件名匹配；合同声明了目录时必须路径精确匹配，避免同名文件从其他 Run 子目录冒充正式产物。

`CompletionDecision` 在严格发布路径会对照 `TaskContract.acceptanceCriteria`：每个 `required` Criterion 必须出现且使用合同指定的 `verifierId`；仅提交一部分“看起来通过”的 Criterion 不能获得 Accepted。

该对账是双向的：收据不得添加合同未声明的 Criterion，也不得重复提交同一 Criterion；因此验收结果集合既不能缺项，也不能扩权。

合同声明 `FreshnessPolicy` 时，CompletionDecision 必须携带完全一致的策略参数；收据不能通过省略或放宽自身策略来绕过合同的新鲜度和源哈希要求。

当任务同时提供 `ExecutionSnapshot` 时，严格发布门禁还会要求每个 `ClaimEvidenceBinding.sourceHash` 等于当前快照的 `sourceHash`，防止跨源码版本复用新鲜但不属于本次执行的证据。

Facade 执行前还会将 Endpoint 声明的 `inputManifestHash` 与同一 `ExecutionSnapshot.inputManifestHash` 对账；输入清单发生漂移时，Worker 尚未启动就会被阻断。

有 ExecutionSnapshot 的调用还会重新计算当前注册 `TaskContract` 的稳定摘要，并与 `taskContractHash` 对账；合同版本或权限/输入/输出定义变化后，旧快照不能继续使用。

合同摘要使用递归 JSON 规范化：对象属性按序排列，合同数组保持声明顺序，避免不同运行时的反射字段顺序造成误判。

AIBrain 生成计划后还会立即对账 `brainPlanHash` 与当前 PlanHash，以及 `commandHash` 与当前 AICommand 合同；计划摘要在计算时排除待回填的 `brainPlanHash` 字段，避免产生自引用哈希。快照不能跨计划或跨命令复用。

CompletionDecision 协议层现在还会校验 `decisionId`、Criterion/Verifier 身份、EvidenceState 枚举和通过项的证据哈希；格式非法的收据会在业务 Verifier 之前被拒绝。

`ESAutomationCriterionResult.Validate()` 现在是独立的收据结构门禁。报告读取器、Worker 适配器和 CompletionDecision 可以复用它，统一校验 Criterion/Verifier 身份、EvidenceState、通过项哈希以及 EvidenceBinding 一致性；它只验证结构，不代替注册 Verifier 的业务判断。

Criterion 收据还声明 `evidenceScope`：未声明的历史收据按 `Static` 解释；合同标记 `runtimeRequired=true` 时，只有明确标记为 `Runtime` 的新鲜证据才可通过。这样静态深度回放可以给出明确的静态通过信号，但不能冒充 Unity 实机验收。

跨语言合同 `ES/Automation/Contracts/es-automation-task-contract.schema.json` 与 `es-automation-run-result.schema.json` 已同步治理字段、能力枚举、快照、Trace 和 CompletionDecision；外部 Worker 使用同一 Schema 时不会因商业扩展字段被旧版 `additionalProperties: false` 拒绝。
Receipt 协议层也会提前拒绝 `RunResult.outputs` 中的 `..` 路径穿越；合法的受控绝对路径仍由后续 Reports/Temp 根目录门禁验证。

## 状态语义

| 状态 | 含义 |
|---|---|
| `ExecutionCompleted` | Worker 已结束，不代表验收完成 |
| `PartiallyDone` | 已产生部分副作用，仍有未完成项 |
| `Unverified` | 可能完成，但缺少足够新鲜证据 |
| `Blocked` | 合同、权限、证据或漂移检查禁止继续 |
| `Accepted` | 所有必要条件由独立验证器确认 |

## 迁移验收

静态阶段应验证：合同结构、稳定 ID、权限交集、哈希字段、状态转换和禁止条件。

运行时阶段再验证：具体工具边界、Unity 生命周期、性能、窗口行为和真实副作用结果。静态通过不等价于运行时通过，但运行时未运行也不应抹掉已经完成的静态结论。
