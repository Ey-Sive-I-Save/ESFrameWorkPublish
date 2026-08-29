# ESAutomationCenter Contracts

这些 JSON Schema 是 Python、PowerShell 和未来其他 Worker 共用的机器协议。它们不授予执行权限；权限由 C# Editor 的 `TaskRegistry`、`PathPolicy` 和当前用户授权共同决定。

## 文件

- `es-automation-task-contract.schema.json`：任务声明、输入范围、能力、超时、取消和输出合同。
- `es-automation-run-result.schema.json`：Worker 返回的结构化结果、Worker 身份、输入/输出 Hash、证据和错误。
- `es-platform-evidence-v1.schema.json`：平台中央 `CandidateEvidence -> EvidenceSet` 单源合同；新提交绑定合同 ID/hash，旧 TaskContext v1 输入仅通过显式兼容投影进入同一平台规范化路径。
- `es-skill-evidence-receipt-v1.schema.json`：所有 Skill 执行回执的中央结构合同；新回执绑定合同 ID/hash，旧回执只通过严格验证器的显式兼容投影读取。
- `es-skill-evidence-binding-v1.schema.json`：Skill 本地绑定合同；绑定中央 Schema 的版本/hash、本地领域扩展和稳定验证入口，禁止本地副本静默成为第二权威。
- `es-evidence-verifier-registry-v1.schema.json`：平台注册 verifier 的结构合同；语义验证额外检查唯一 ID、完整锚定 claim 范围、字段集合、策略和定义漂移。
- `es-route-stage-registry-v1.schema.json` / `es-route-stage.registry.json`：RoutePlan 阶段输入、输出、失败条件与深度 2 授权的中央注册表；未注册阶段不能进入组合计划。
- `es-route-plan-v1.schema.json`：只读组合路由计划合同，绑定 GoalRevision、Git HEAD、SourceRefs 与 Registry Hash；`executionEnabled=false`，不接管生产路由或全局 P0。
- `es-interaction-binding-ref-v1.schema.json` / `es-interaction-session-binding-receipt-v1.schema.json` / `es-interaction-session-authority-proof-v1.schema.json`：生产会话身份绑定的三层静态合同。TaskContext 公共引用仅保留 `bindingId + bindingHash`；Receipt 绑定 Task/Goal/Route/Session/Transcript，AuthorityProof 独立保存注册表、接受回执、进程祖先与令牌哈希证据。当前未接生产路由或全局 P0。
- `es-task-context-evaluation-adapter-v1.schema.json` / `es-task-context-evaluation-adapter-v1.json`：唯一 advisory `/eval` 的来源注册、Worker/hash、生命周期零变更、跨合同禁止投影、观测与回滚合同；`sourceRegistrationIntegrated` 不代表 Unity Runtime 已验证。
- `es-commercial-evaluation-v1.schema.json` / `es-commercial-metric-registry-v1.schema.json` / `es-commercial-metric.registry.json`：平台只读商业指标控制面；已验证 TaskContext 记录可推导成功、稳定成功、任务级硬违规、延迟和恢复率，缺少权威来源的成本、人工纠正、声明夸大和回归指标保持 `evidence-pending/null`。
- `ESJsonSchemaLite.psm1`：当前 Automation 合同使用的有界 Draft 2020-12 子集校验器，支持同目录外部 `$ref`；遇到未实现关键字时拒绝，不静默跳过。
- `es-automation-stage-result.schema.json`：分阶段 Worker 的检查点结果；Unity 只依据此文件决定是否展示已注册输入步骤或继续运行。
- `es-automation-input-response.schema.json`：由 C# Editor 规范化并写入的表单响应，回显 RunId、代次、StepId 和 SchemaHash 以拒绝过期提交。
- `es-scene-scan-report-options.schema.json`：首个 `es.scene.scan` 原型的已注册输入表单。它是固定协议，不允许 Worker 动态扩展字段或控件。
- `es-automation-ai-request.schema.json` / `es-automation-ai-response.schema.json`：本机受信 AI Bridge 的固定请求/响应信封；动作 payload 仍由 C# 按动作精确校验。
- `es-automation-python-runtime.schema.json`：项目受管 Python 解释器及可选依赖锁文件的身份与 SHA-256 锁定协议。
- `es-unity-build-identity-receipt-v1.schema.json`：Unity 构建意图、输入指纹、执行身份和逐项产物哈希的绑定协议；由 `es-unity-compile` 的 Capture/Finalize/Validate 脚本消费，不负责启动构建或证明 Runtime/发布通过。
- `es-aiwarning-knowledge-candidate-v1.schema.json`：Warning 保存后的候选编排合同；绑定 Warning 快照、StableId+WarningHash 幂等键、匹配信号、冲突、预期 Knowledge 哈希和候选-only 回放命令。
- `es-aiwarning-knowledge-receipt-v1.schema.json`：Warning→Knowledge 候选编排回执合同；明确 `transactionExecuted=false`、`formalRegistration=not-run`，禁止把候选回执冒充 Apply 或正式注册。
- `Test-ESAIWarningKnowledgeApply.ps1`：显式 Apply 前置 CAS 门禁（只读）；重读 Warning 与目标 Knowledge，检测哈希漂移、缺失正文和越界路径，未提供正式 Apply 权限与内容补丁时不写入任何权威文件。
- `es-aiwarning-save-observer-receipt-v1.schema.json` / `Invoke-ESAIWarningSaveObserver.ps1`：受限保存观察器回执与实现；去抖、稳定重读、进程锁、队列上限和 Warning 哈希 CAS 只触发 Candidate-only 编排，不执行正式 Apply。

## 规则

- Schema 版本变化必须递增，并保留明确迁移策略。
- Worker 不得自行扩展能力名称或写入根目录。
- Worker 请求输入时必须退出并留下 `NeedsInput` 检查点；不得保持 Python 进程等待 Unity 对话框。
- C# 必须同时核对 `RunId + Generation + StepId + SchemaHash`，才可把输入响应交回下一阶段。
- AI 只能调用 C# 已注册且显式允许的 Task；`submitContentProposal` 也只能进入已注册领域内容入口，不能直接写 `Assets/`。
- `DryRun` 结果仍必须落盘报告，但不得修改业务目标目录。
- 结果 `Passed` 不等于发布通过；必须经过 C# `ReleaseGate`、受信 RunRecord 和目标平台验收证据。
- `platform.static-replay-v1` 只为 task-object `regression...` claim 重新运行哈希绑定的共享 StaticDeepReplay；它产生平台 OutcomeAssertion，可进入 `regressionPassRate`，但不证明 Unity/Worker Runtime 或 Release。
- `CandidateEvidence` 的 outcome/hash/producer 只属于候选输入。只有冻结 AcceptanceProfile 选定的平台注册 verifier 可以重读工件、推导规范化 EvidenceSet，并由 TaskContextRuntime 决定 completion。
