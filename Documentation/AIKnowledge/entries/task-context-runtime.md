`KnowledgeId`: `es.automation.task-context-runtime.v1`
`Authority`: `Derived`
`RouteKeys`: `task-context-runtime, task-lifecycle, context-lifecycle, goal-revision, route-plan, completion-decision, delivery-acceptance, evidence-set, evidence-verifier, source-scope, receipt, cas, reopen`
`EvidenceLevel`: `S2`
`StaleWhen`: `TaskContextRuntime intent、schema、平台模块、状态迁移合同、Skill 或 AICommand 入口变化`

## SourceRefs

- `ES/Automation/Contracts/es-task-context-runtime-intent-v1.json` (`861e227ce0447931fea7266fd97b4b329b644f666df28c655937d6f3659398a0`)
- `ES/Automation/Contracts/es-task-context-runtime-integration-policy-v1.json` (`665a5ade3446ddc8b54ab4ebc74dacaa0f56eebe2c2216d84806b262aedefae1`)
- `ES/Automation/Contracts/es-task-context-runtime-v1.schema.json` (`65407612e0afb787a772c7089065138d2013d6d2348658a0590d89c41d358f1a`)
- `ES/Automation/Contracts/es-route-plan-v1.schema.json` (`55af28480cf41fc8ffdf148226a0cad9a2eec7b6b8678f96adae47b921d6ef39`)
- `ES/Automation/Contracts/es-route-stage.registry.json` (`8606bd20d226e0df2911857ef1ccf7ad70260aa84bb239af95302966f4a04613`)
- `ES/Automation/RoutePlan/ESRoutePlanContract.psm1` (`d8295130600386163a2e88fe3d81ab35fdce5cf10b8b794e1d9ba091da6adf29`)
- `ES/Automation/RoutePlan/Test-ESRoutePlanContract.ps1` (`557da29f1cbc8f59c93e4397edc509cbfc8f63dc07f1e7f49d8e26440945c1a8`)
- `ES/Automation/Contracts/es-task-context-evaluation-adapter-v1.json` (`96f04d0fd6e3cdcba8edfce2a4de6f0fa68e7f8725c47e371b6d91fc85d68f20`)
- `ES/Automation/Contracts/es-task-context-evaluation-adapter-v1.schema.json` (`370382ec732625e9d4ff78ef4557f331b70d137c3abc12d02913a18c43744f6f`)
- `ES/Automation/TaskContextRuntime/ESTaskContextRuntime.psm1` (`c5c95fff32068153c3165cff5e899b86411f1bbdc1b63a27d36ab9d81f3f9138`)
- `ES/Automation/TaskContextRuntime/Test-ESTaskContextRoutePlanFixture.ps1` (`dfef733d75b0def2ec1a25ef7c720599f7a24c6b849a206d91323cd87ba5cf02`)
- `ES/Automation/TaskContextRuntime/Test-ESTaskContextRuntime.ps1` (`8cbb069b8694cc5778ca3199c972849a6d266a55cd9834a0a97ae62950234c7f`)
- `ES/Automation/TaskContextRuntime/references/state-transition-contract.md` (`a72b9b233e28e9cbd9c55d69a3a5648e2ab57c9bf5b971557018ae86a87813ec`)
- `ES/Automation/Workers/PowerShell/Invoke-ESTaskContextEvaluationWorker.ps1` (`bb2046146493e97c7d3d49b46bdab766f52ed1ca3c55889720cebdce86e19f10`)
- `.agents/skills/es-task-context-runtime/SKILL.md` (`49a167b54872548197ddd7a579b2ee21aa5b8b27939dbc41e68cea88020f75d4`)
- `Assets/Plugins/ES/AICommands/任务上下文运行时_受控生命周期_AI命令.md` (`ac8e41b425e3ede7f8ae7748768b9620e4d357f6c92fae7403e2300968e65fab`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`22f658dce8abb6eacbce9bd92c732d734c1c2af184dd879b28fc75829fc6fed8`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` (`809f6b2669fb033ee6d391d258c9f8f827b697f88650169522c768f484e7853d`)

## EvidenceRefs

- `ES/Automation/RoutePlan/Test-ESRoutePlanContract.ps1`
- `ES/Automation/TaskContextRuntime/Test-ESTaskContextRuntime.ps1`
- `ES/Automation/TaskContextRuntime/Test-ESTaskContextRuntimeSchema.ps1`

`ContentHash`: `536b478f58ba2dcdf9a3d4143a2966dcdccc5d333523aacdb1bfc2566467c3e5`

TaskContextRuntime 是 Skill-agnostic 平台状态所有者。任务主线为 GoalRevision -> immutable RoutePlan -> TaskContextRuntime -> requested/resolved/verified sourceScope -> CandidateEvidence -> Platform Verifier -> OutcomeEvaluator -> immutable Completion Receipt -> TaskStatus/ContextStatus；Skill、Worker、Automation Run、Codex Session、ReadSnapshot 与 Semantic Archive 都只通过适配器接入。

TaskContext 创建必须提交真实 RoutePlan 工件；自由 `planHash` 只保留兼容字段且必须等于平台重算的 `routePlanHash`。共享 `ESRoutePlanContract.psm1` 会重读 GoalRevision、Route Stage Registry 和全部 SourceRefs，重算 canonical hash/ID、SourceRefsHash，核对 Git HEAD，并按冻结 routeKeys 重放每个 stage 的 Skill/Profile/route/depth 注册关系。缺失、伪造或漂移只拒绝当前 TaskContext 创建或把当前完成结论限制为 `undetermined`，不扩大成项目级 P0。

平台接受迁移固定为 `Active+Live -> completionDecision=accepted -> Completed+Frozen`，此时 deliveryAcceptance 等待用户确认。交付拒绝不会回滚平台接受；显式 Reopen 会推进 TaskRevision 和 ContextVersion，清空当前证据/Receipt 投影，但保留历史不可变事件与 Receipt。

所有变更要求 TaskRevision + ContextVersion CAS，且 idempotency key 必须绑定同一操作指纹；跨操作复用会被拒绝。sourceScope 和 StoreRoot 同时拒绝项目越界及项目根下的 reparse-point 穿透。accepted 还要求冻结 RoutePlan 与 AcceptanceProfile、完整且 Fresh 的必需证据、当前 verified sourceScope、无关键 Contradiction、无未处理 SourceDrift、UnverifiedClaims 不越过验收范围，以及绑定 TaskId、RoutePlan identity/artifact/snapshot、GoalRevisionHash、TaskRevision、ContextVersion、EvidenceSetHash、AcceptanceProfileHash 和 sourceScope hash 的 Receipt。

`ESAutomationRunStatus.Accepted` 只表示 Automation Run 被接收，不等于平台 `completionDecision=accepted`。`task.context-runtime.mutate` 保持完整 CLI 生命周期入口；唯一 advisory `es.task-context.evaluate@1` 已具备源码注册的 `planTask -> runTask -> ESAutomationFacade -> managed PowerShell Worker -> TaskContextRuntime Evaluate` 路径。它只创建 task-object EvaluationRecord，不改变 TaskRevision、ContextVersion、TaskStatus 或 ContextStatus。`sourceRegistrationIntegrated=true` 不能解释成 Unity 已加载该注册、生产执行已发生或全局 P0 已接入；这些仍为 `runtime-not-run`/未验证。

集成能力按独立策略分类：全局 Skill 自动包装、适配器直接拥有生命周期、发现即自动执行，以及 Automation Accepted 投影完成状态属于禁止能力，缺失不会成为任何 Profile 的失败原因。AIBrain `runTask`、`ESAutomationFacade` Endpoint、Worker EvidenceSet、Codex Session、Semantic Archive 和 Unity Editor 适配属于条件能力；默认不阻断 `StaticReview`/`EngineeringReadiness`，只有其稳定 capability ID 被 Runtime/Release Profile 的 `requiredCapabilityIds` 显式选中后才成为该 Profile 的必需证据。

## 关键失败面

- `adapter-registration-cascade`：若 Worker/Schema hash 漂移导致 `/eval` 注册失败，错误只隔离该 Endpoint；禁止中断其他 Automation Endpoint 注册。恢复方式是修复绑定并重新加载，不删除既有 RunRecord 或 EvaluationRecord。
- `worker-output-expansion`：直接调用 Worker 时，`OutputDirectory` 必须严格位于 `ES/Automation/Runs/TaskContextEvaluation/<N-format runId>`，且输入必须是同目录 `request.json`；越界在生成可信 `result.json` 前拒绝。
- `transport-authority-projection`：Automation CompletionDecision、Accepted/Blocked、Static/Runtime 和 governanceHash 都不是 TaskContext evaluation 输入；任何注入、身份/hash 错配或输出 scope 扩大使该 Automation Run 失败，但不会投影成项目级 P0。
- `stale-cas-evaluation`：Evaluation 使用当前 CAS 对绑定读取快照；错配时不创建 EvaluationRecord，也不改变任务生命周期。调用方必须重读当前状态后以新幂等键重试。

外部权威校准对本条不适用：这里记录的是当前仓库自有平台合同、实现与回放边界，不包含外部版本 API 事实。Unity-hosted 注册、进程时序与发布行为仍必须由对应 Runtime/Release 证据证明。
