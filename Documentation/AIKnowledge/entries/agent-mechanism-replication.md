# 公开 Agent 机制复刻的 ES 适配合同

`KnowledgeId`: `es.research.agent-mechanism-replication.v1`  
`Authority`: `Current ES contracts and project Skills; external calibration is deferred until a project-local source snapshot is bound`  
`RouteKeys`: `agent-mechanism-replication`, `analysis`, `design`, `es-adaptation`, `evidence-set`, `external-authority`, `failure-surface`, `knowledge-output`, `route-plan`, `route-probe`, `route-stage`, `root-cause`, `source-ref`, `source-scope`, `static-replay`, `task-context-runtime`, `receipt`, `cas`, `stale`, `closed-loop`  
`HashSchema`: `v2`  
`ContentHash`: `d2204a9cf5e8df8798e1c79a4cf949394e1ef1f1eff3f105edd1e4a8087ce2db`
`SourceSetHash`: `d2204a9cf5e8df8798e1c79a4cf949394e1ef1f1eff3f105edd1e4a8087ce2db`  
`EntryBodyHash`: `a00e7ead0d9e217f72097892cefa187b00f470163bde366ee22338ce65a5ed77`  
`EvidenceLevel`: `S1`  
`StaleWhen`: `TaskContextRuntime、RoutePlan、RouteStage、Evidence/Verifier/Evaluator、AIBrain 选择算法、KnowledgeIndex、RouteProbeRegistry、Project Skill 资源或任一 SourceRef 哈希变化。`

## Scope and authority

本条目把公开 Agent 机制压缩为可进入 ES 的 contract-only 设计，不把公开论文、README、模型自评或静态文件存在写成 ES Runtime/Release 事实。

`ESGraphViewV2` / `AISkill Graph` 在本机制复刻范围内只作为可选的固定流程表达、烘焙和验证工具；当前不具备已验证的真实 AI 协作工作流内核地位。GraphView 的编辑生产力、动态 FanOut、并行调度、Agent 上下文隔离、执行性能和准确性均未完成独立 Runtime 验收。外部多子 Agent 机制不得因为可以映射成 Graph 节点/边，就被宣称已经接入 ES；动态协作方案应先以现有 AIBrain、TaskContext、TaskFocusContext、AutomationCenter 和受管合同为主，待证据闭合后再选择性固化为 AISkill Graph。

项目源码、AIWarnings、TaskContext/Evidence/RoutePlan 合同和真实回执仍是 ES 事实权威；外部研究只提供机制校准。外部研究现已由 `Documentation/AIKnowledge/ExternalSources/agent-mechanism-source-lock.v1.json` 固定到提交和内容哈希，并由 `Test-ESABCDExternalSourceLock.ps1` 重新核验；该锁只证明来源身份与内容未漂移，不把外部事实提升为 ES Runtime/Release 权威。来源锁或任一内容哈希漂移时，仅使受影响机制声明 `stale`，必须重新验证。

## ES canonical flow

```text
GoalRevision
  -> AIBrain RoutePlan (executionEnabled=false)
  -> registered Skill stages
  -> CandidateEvidence
  -> EvidenceKernel / registered Verifier
  -> EvidenceSet
  -> OutcomeEvaluator
  -> immutable Receipt
  -> completionDecision
  -> deliveryAcceptance
```

不变量：

- TaskContextRuntime 拥有任务、上下文、CAS、Receipt 和生命周期；Skill、Worker、Audit、Discovery 不能直接拥有或改变生命周期。
- `completionDecision` 与 `deliveryAcceptance` 独立；`ESAutomationRunStatus.Accepted` 不是完成接受。
- 每个任务绑定冻结 GoalRevision、RoutePlan、Git HEAD、SourceRefs Hash 和 RouteStage registry hash。
- SourceDrift、证据冲突、过期或伪造 receipt 只能导致 stale/undetermined/PartiallyInvalidated 或当前声明降级。
- Discovery 和 `planTask` 只导航与生成只读计划，不能自动触发业务执行。

## Mechanism mapping

### SWE-agent ACI

状态机：`Ready -> ReadWindow/Search -> EditProposed -> Linting -> Applied|Rejected -> Observe`。

读取必须有路径、行窗口、字节上限和快照哈希；搜索输出有界；编辑先 lint；空输出成为显式 `EmptyOutputObserved` 事件。哈希漂移、越界、超时、截断或 lint 失败均拒绝应用。Unity/Codex Adapter 只暴露受限读取、搜索、lint 和 CAS 编辑，不暴露任意宿主命令。

### Reflexion

状态机：`TrialStart -> Act/Observe -> Verify -> Success`，失败则 `Attribute -> Reflect -> Retry`。

反思只有在真实 verifier receipt、当前 SourceScope 和 Evidence contract 均有效时才可写入情景记忆。无 receipt、SourceDrift、记忆污染、不可恢复错误、重试预算耗尽或重复幂等键冲突时停止或回退。

### Tree of Thoughts

状态机：`Root -> Expand(sample|propose) -> Evaluate(value|vote) -> Select(top-k) -> Continue`，另有 `Pruned/Backtracked/Solved/BudgetExhausted`。

每个分支绑定 `branchId`、`parentId`、`changedAssumption`、`riskDelta`、`minimalExperiment`、`verificationPredicate` 和不可变快照哈希。分支不得共享可变 Unity 资产或 TaskContext 写入根；只能合并经验证的差异。

### Petri

状态机：`Seed/Hypothesis -> Parallel Auditor Probes -> Simulated Environment/Rollback -> Transcript Branches -> Judge Evidence Extraction -> Score -> Iterate`。

执行者、auditor、judge 和目标身份必须可区分。Judge 先绑定证据引用再解释；judge 不确定时保留人工校准入口。执行者不得自写完成接受。

### EnvTrustBench

状态机：`Scenario -> Generate(workspace, environment, objective, oracle) -> AgentExec -> Trace/FinalState -> Oracle -> Pass|Environment-Grounded Failure`。

隐藏负例包括 stale 观察、伪造反馈、时间错位、派生记忆错位和可执行工件错位。Agent 不能把环境声明直接当成真实状态；ES 必须重验 SourceScope 与证据链。

### AuditBench

状态机：`HiddenTarget -> Investigator Probes/Tool Calls -> Observations -> Predictions -> Oracle Match -> Static-vs-Agentic Gap`。

记录工具调用、证据引用、假设变化和最终预测，比较工具静态准确率与 Agent 决策成功率。只测工具输出不能证明 Agent 使用了证据。

## Minimal interfaces

以下是适配合同，不是当前 ES 已存在的公共 API：

```csharp
interface IUnityAdapter
{
    SnapshotHandle Capture(UnityScope scope);
    BoundedRead ReadWindow(PathRef path, int startLine, int maxLines, string expectedHash);
    SearchResult Search(SearchQuery query);
    LintResult Lint(EditProposal proposal, SnapshotHandle snapshot);
    ApplyResult Apply(EditProposal proposal, string expectedHash, CancellationToken cancellation);
}

interface ICodexTaskContextAdapter
{
    TaskContextSnapshot Create(TaskSpec spec);
    SourceVerification VerifySources(string taskId);
    EventResult AppendEvent(EventDraft draft, int expectedRevision, int expectedVersion, string idempotencyKey);
    EvaluationRecord Evaluate(string taskId, FrozenProfile profile);
    CompletionResult Complete(string taskId, VerificationReceipt receipt);
}

interface IEvidenceKernel
{
    EvidenceReceipt Verify(EvidenceCandidate candidate, FrozenProfile profile);
    EvidenceSet Normalize(IEnumerable<EvidenceCandidate> candidates);
    TrustState Resolve(Provenance provenance);
    bool CanEnterMemory(VerificationReceipt receipt);
}

interface IIterationController
{
    AttemptResult RunAttempt(AttemptSpec attempt);
    FailureAttribution Attribute(Trace trace, VerificationResult result);
    RetryDecision Decide(FailureAttribution attribution);
    MemoryEntry CommitMemory(Reflection reflection, VerificationReceipt receipt, string idempotencyKey);
}

interface IDivergenceEngine
{
    BranchHandle OpenRoot(TaskContextSnapshot root);
    BranchHandle Expand(BranchHandle parent, BranchRecord branch);
    DivergenceReport Compare(BranchHandle left, BranchHandle right);
    BranchDecision Evaluate(BranchHandle branch);
    RecoveryResult Backtrack(BranchHandle branch);
}

interface IAuditController
{
    AuditRun Start(AuditSpec spec);
    ProbeResult Execute(AuditBranch branch);
    Judgement Judge(TranscriptRef transcript, RubricRef rubric);
    GapReport CompareStaticVsAgentic(StaticToolResult tool, AgentTrace trace);
    AuditReceipt Finalize(AuditRun run);
}
```

## Failure-surface matrix

### `AMR-001` 证据未验证却进入记忆或完成

- `severity`: `identity/authority`
- `triggerAndSymptom`: 模型反思或 Worker success 被直接写入 Memory/Completed。
- `rootCause`: producer candidate 被误当成 platform-normalized evidence。
- `preventionCheck`: 检查 Frozen AcceptanceProfile、Verifier definition hash、SourceScope 和 receipt。
- `correctAction`: 降级为 candidate/undetermined，不改变生命周期。
- `recoveryAction`: 重新读取当前源并以新 TaskRevision 重试；孤立 receipt 不得接受。
- `evidencePresent`: 当前 TaskContext、Verifier、Evaluator 合同。
- `evidenceMissing`: Unity/Worker 端到端回执。

### `AMR-002` 分支共享可变状态

- `severity`: `irreversible`
- `triggerAndSymptom`: ToT 分支写入同一资产、目录或全局状态，回溯后残留交叉修改。
- `rootCause`: 分支没有独立快照、artifact root 或 CAS。
- `preventionCheck`: branch snapshot hash、隔离写根、父快照和 merge receipt 均存在。
- `correctAction`: 关闭受污染分支，保留只读轨迹。
- `recoveryAction`: 从最近可信快照重建分支；不覆盖原事件或收据。
- `evidencePresent`: RoutePlan/TaskContext 的快照与 CAS 合同。
- `evidenceMissing`: 实际 Unity 资产隔离和回滚运行证据。

### `AMR-003` SourceDrift 后继续使用旧路径

- `severity`: `identity/authority`
- `triggerAndSymptom`: SourceRef、KnowledgeIndex 或 RouteStage 改变后旧计划仍被执行。
- `rootCause`: 只比较文件内容，未绑定 planHash、Git HEAD 和 registry hash。
- `preventionCheck`: 计划重算 SourceRefsHash、RouteStage hash、Git HEAD 和 GoalRevision hash。
- `correctAction`: 标记 stale，丢弃旧 RoutePlan 并重新发现。
- `recoveryAction`: 回读权威入口和 requiredReads；禁止 mutable source 或其他 handoff 替代。
- `evidencePresent`: AIBrain RoutePlan canonical contract。
- `evidenceMissing`: 新机制条目的实际 RouteProbe 重放和 Unity 消费者回执。

### `AMR-004` Discovery 越权触发业务执行

- `severity`: `identity/authority`
- `triggerAndSymptom`: 发现 Skill/Knowledge 后自动调用 Worker、写 Assets 或发布。
- `rootCause`: 把导航投影误认为授权和执行入口。
- `preventionCheck`: RoutePlan `executionEnabled=false`，并验证用户授权、AICommand、TaskContract 和 Facade endpoint。
- `correctAction`: 仅返回 route-pack/plan，等待明确执行授权。
- `recoveryAction`: 终止未授权调用，保留静态发现回执。
- `evidencePresent`: Integration policy 与 AIBrain/Facade 边界。
- `evidenceMissing`: 运行时宿主拒绝越权的实际回执。

## Discovery contract

建议的唯一 Knowledge owner 是本条目的 `KnowledgeId`。AIBrain route 应保留以下三类结果：

1. 本条目：公开机制到 ES contract 的归一化设计。
2. `es.automation.task-context-runtime.v1`：生命周期、证据、Receipt、CAS。
3. `es.project.automation-aibrain-graph.v1`：AutomationCenter、AIBrain、TaskContract 和受管 Worker。

建议的中文发现短语：公开 Agent 机制复刻、SWE-agent ACI 适配 ES、Reflexion 失败恢复、Tree of Thoughts 分支搜索、Petri 审计代理、EnvTrustBench 环境证据、AuditBench 工具到 Agent 鸿沟、机制复刻接入 ES 流程。

别名只负责发现，不授予执行权限；RouteProbeRegistry 是探针唯一数据集，不建立平行探针表。

## Static acceptance

- Knowledge Validator：`source-ref-hash`、`content-hash-recompute`、`bounded-output`、`stale-entry-detection`、`unsupported-claim-rejection`。
- RouteProbe：唯一 `probeId`、精确 Top-3、requiredReads、禁止命中、重复确定性。
- RouteStage：精确 `skillName + profile + routeKey`，依赖图无循环，产物无重复，失败条件可重放。
- 负向用例：伪造 receipt、SourceDrift、零命中、过宽 route、缺 Skill、MCP 断开、重复执行和中断恢复。
- 所有静态结果与 Runtime/Release 结果分开记录。

## External research status

以下来源已用于本轮研究，并已绑定到项目内 Source Lock；它们仍属于 `external-design-input`，不是 ES Runtime/Release 证据：

- SWE-agent ACI：<https://github.com/SWE-agent/SWE-agent/blob/main/docs/background/aci.md>
- Reflexion：<https://arxiv.org/abs/2303.11366>
- Tree of Thoughts：<https://arxiv.org/abs/2305.10601>
- Petri：<https://alignment.anthropic.com/2025/petri/>
- EnvTrustBench：<https://arxiv.org/abs/2605.08828>
- AuditBench：<https://alignment.anthropic.com/2026/auditbench/>

来源锁验证只支持提交、URL、许可证边界和内容哈希的静态事实；它不会升级外部机制为 ES 长期事实，也不会替代项目源码、AIWarnings、TaskContext 或 Runtime/Release 回执。

## Evidence boundary and non-claims

本条目可以支持：当前 ES 合同、Skill 边界、RoutePlan/Knowledge 发现设计和失败面设计的静态导航。

本条目不能证明：Unity 编译、Editor/PlayMode、Worker 运行、Profiler、Player、IL2CPP、网络、视觉、性能、发布或三组实验数字已经通过。

## SourceRefs

- `AGENTS.md` (`b7b6c34b0cc718cfd3b998ab7a07c99ca90fa4d1abe40bd4d6f5dd97fad8c8e7`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`f7e95019f1bca2469dc9074e3b266c645fea854772914de2f48861acee1dfe9c`)
- `Documentation/AIKnowledge/entries/knowledge-routing-quality.md` (`67910e60302a46ab9baeb7622e802a5d69c6d2b591287811b90a3a7dfd70e7dd`)
- `Documentation/AIKnowledge/entries/task-context-runtime.md` (`2e0301238582c84f31d04d1b1e2e44612a326be37719de14f925c777623f9554`)
- `Documentation/AIKnowledge/entries/automation-aibrain-graph.md` (`7793f71f8f8af05b9b484bd08cf7ecd5741b76b0c3740688c51405498a05473e`)
- `ES/Automation/Contracts/es-task-context-runtime-v1.schema.json` (`f76f221e02c83bf2b4c3c76c2c78393f5eff55c85eee6949cfcb5cf04ba2c38d`)
- `ES/Automation/Contracts/es-evidence-verifier.registry.json` (`77e8719944637db832163330961279589a38356fbd27fcbfd79db91f38ea805e`)
- `ES/Automation/Contracts/es-outcome-evaluator.registry.json` (`d5d93ea67ee13482ae87f94f8340821dfc9da12f4f0a6b0ca8bae2cc9e64515a`)
- `ES/Automation/Contracts/es-route-plan-v1.schema.json` (`f8ac9af713f320ac1d32d670938e26cffdc970f9d1acbc2c259179df3f64ed53`)
- `ES/Automation/Contracts/es-route-stage.registry.json` (`f476420f1b854243da91ebc3e3a8444cb1704594e849eb9c4884823c6a4c953c`)
- `ES/Automation/Contracts/es-knowledge-route-probe-registry.schema.json` (`bd73b87a43e5b9a01d35e04cffebd8225bb1146aaf39553f670c6876a1c0d7ff`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`5c53154ccb79c804e6ae02b3feea16d7c3e35b35125cbea9d363e087a9d3b749`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` (`f3365291c7366a14a1e0553b83b81668f0bc877746532cb051d082aa72808827`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`a6e1424e0d2f4ece7c51869f7cf8e41c5d6e5e9ef5f37a26ccdf258229c0de42`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md` (`842bc5d46a045f3e2f226426f005afb8f7114ba56646e623d245ea0f99a04166`)
- `.agents/skills/es-first-principles-analysis/SKILL.md` (`252e69284511e74f09fd3450e496d3e74f31f56b28c0fb409ae297b58e56a74f`)
- `.agents/skills/es-adversarial-review/SKILL.md` (`647b54f323bfa2728b5b25a69532c46bd9ff58317964b7faced02147c8a3eb15`)
- `.agents/skills/es-aibrain-route-authoring/SKILL.md` (`823e01fd1e84a7a5a163716bdd4047c9fe5cf63ed479c79debbb56a4f6ebc378`)
- `.agents/skills/es-knowledge-creator/SKILL.md` (`bb2d2869573f9468db36afa74b8d86ee928987ae0e297dc46b858f71f8876ad7`)
- `.agents/skills/es-knowledge-validator/SKILL.md` (`6183ac59608a55c03a46bd0a3575e699116fb6e7910ac4f1ad23431da5f6a61e`)
- `Documentation/AIKnowledge/ExternalSources/agent-mechanism-source-lock.v1.json` (`8aec9336e3350074a93ef1b40dd1c5c38b616aa52eea965b0f15ae5381928e0e`)
- `ES/Automation/Contracts/es-agent-mechanism-source-lock-v1.schema.json` (`dc5e9485e6ef71ee36479d04015d2104c1e1e53b785b39edc56b12a4e18567de`)
- `ES/Automation/ABCD/Test-ESABCDExternalSourceLock.ps1` (`07e0e71d97fdede7bd0a90e40f20644908d05442129b1f713b3894dbbde26f99`)
