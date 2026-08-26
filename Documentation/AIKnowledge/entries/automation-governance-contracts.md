`KnowledgeId`: `es.automation.governance-contracts.v1`
`Authority`: `Derived`
`RouteKeys`: `automation, task-contract, capability, acceptance, receipt, completion-decision, source-drift`
`EvidenceLevel`: `S1`
`StaleWhen`: `ESAutomation TaskContract、CompletionDecision、Verifier 或 Receipt 合同变更`

## SourceRefs

- `Documentation/ES_AUTOMATION_CENTER_STANDARD.md` (`fda3f8e4408e507fd257bb4093b8e19f83c1374834578639b443b52690280121`)
- `Documentation/ES_AUTOMATION_GOVERNANCE_CONTRACTS.md` (`f980736e6e2e5f4569f0239494492407b12b88ea9e25e3b1460c357ad436b949`)
- `ES/Automation/Contracts/es-automation-task-contract.schema.json` (`54e2cce394895b33d23f5c23c5af296a0ecdbcebbcbe5b567fe72497d0b15c9d`)
- `ES/Automation/Contracts/es-automation-run-result.schema.json` (`65068ccad7fd2632703b53536971068c0b09dea79ea192b73ce19316c93b83ea`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs` (`6fc627e16930d541b1275bb5d687e1fdad8d96b751616002dbf2fbdbfa38fbc3`)

`ContentHash`: `aa7e483c14ecbc5b389d466ce36e8d6cf6b26b7b01719ae0dc2f87b6c2a405d6`

当任务涉及 AICommand、TaskContract、Worker、权限、验收、Receipt、幂等、源漂移或商业级交付时，必须先阅读上述两个源文件。

现有 ES 入口保持兼容；治理合同通过可选 `acceptanceCriteria`、`capabilityEnvelope`、`executionSnapshot` 和 `completionDecision` 逐步接入。

`Completed` 只代表执行器结束，`Accepted` 必须由外部验证器根据新鲜、绑定且无冲突的证据确认。

当前 AIBrain 受管授权为 Policy v5 / Store schema 3：永久跨进程锁、受管原子持久化、PlanHash/InvocationId 双唯一、终态墓碑和预检后消费。授权同时绑定策略代际、授权分类、任务、版本、输入、调用身份和可选 ExecutionSnapshot；幂等键作为每次可复用消费的独立唯一键持久化。Facade 先完成 Endpoint、TaskContract、能力、快照、路径、AI 与 PlayMode 的不消费预检，再在执行前立即重验并消费，确定性预检失败不消耗次数。当前文件 Bridge 可通过 `userDirectedRuntime` 绑定 `CurrentUserDirect` proof，hostId 固定为 `es.automation.ai-bridge`；该分支仍要求当前用户指令哈希和固定 allowlist，不能把 20 次低风险预算理解成真实宿主或运行验收已经完成。

当前 `CompletionDecision.RefreshDecisionSemantics()` 会把缺失/过期/矛盾/源漂移/预算越界等情况降级为 `Blocked`，并记录 `ClaimDowngraded`；持久化 `accepted=true` 不能单独构成外部验收。严格源漂移合同要求每个 Criterion 的 EvidenceBinding 同时绑定 `snapshotId`、`inputManifestHash`、`taskContractHash`、`commandHash`、`brainPlanHash` 与 `sourceHash`；`ESAutomationRunResult.Validate()` 还校验时间单调性、重试计数、幂等键、ExecutionSnapshot 以及 CompletionDecision 与 RunId 的绑定。上述结构和静态门禁仍不能证明 Worker、Unity 或外部服务的真实执行结果。
