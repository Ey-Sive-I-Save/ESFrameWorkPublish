# 任务上下文运行时：受控生命周期 AI 命令

本命令只允许通过 TaskContextRuntime v1 平台 API 创建、读取和推进一个任务上下文。它不执行用户业务、不启动 Unity 或 Worker、不替代 Automation Run 状态，也不允许 Skill 或 Worker 自行接受任务完成。

命令类型：安全执行。
默认改文件：是，仅允许当前 TaskId 对应的项目内 TaskContextRuntime StoreRoot 下 create-only event 与 accepted Completion Receipt。
风险等级：L2。

## 稳定身份

```text
commandId: task.context-runtime.mutate
platformContract: ES/Automation/Contracts/es-task-context-runtime-v1.schema.json
evidenceContract: ES/Automation/Contracts/es-platform-evidence-v1.schema.json
intentContract: ES/Automation/Contracts/es-task-context-runtime-intent-v1.json
entrypoint: ES/Automation/TaskContextRuntime/Invoke-ESTaskContextRuntime.ps1
skill: es-task-context-runtime
```

本命令以 CLI 为完整生命周期入口，并为唯一 advisory 动作注册 `es.task-context.evaluate@1`：`planTask -> runTask -> ESAutomationFacade -> managed PowerShell Worker -> TaskContextRuntime Evaluate`。该 Endpoint 只生成 task-object `EvaluationRecord`，不推进 TaskRevision、ContextVersion、TaskStatus 或 ContextStatus。源码注册不等于 Unity Runtime 或生产执行已验证；除 `/eval` 外，不得把其他生命周期动作描述为可通过 AIBrain `runTask` 执行。

## 必须先读

执行前必须以严格 UTF-8 读取本文件、`.agents/skills/es-task-context-runtime/SKILL.md`、两个平台合同和 `ES/Automation/TaskContextRuntime/references/state-transition-contract.md`，并核对当前 branch、HEAD、工作树及目标 StoreRoot 是否与其他写入重叠。

## 输入与写入边界

- `InputPath` 必须是项目相对 UTF-8 JSON；CLI action 只允许 `Create`、`Get`、`VerifySources`、`SubmitEvidence`、`Evaluate`、`Complete`、`SetDelivery`、`Transition`、`Integrity`。
- `ProjectRoot`、`StoreRoot`、sourceScope、event 和 receipt 必须留在项目根内，且不得穿过 reparse point。
- `Create` 必须携带稳定 TaskId、64 位 PlanHash、冻结 `GoalRevision`、冻结 AcceptanceProfile、requested sourceScope 和 idempotency key。
- `GoalRevision` 必须通过平台合同校验其 `goalId`、`goalRevision`、`scope`、`acceptanceIntent`、`budget`、冻结状态和 `revisionHash`；TaskContext 绑定其项目相对路径和 hash，并在完成前重新读取。TaskContext 不得自行生成或覆盖目标，Goal 漂移只限制当前完成声明。
- AcceptanceProfile 必须为每个 required claim 显式冻结中央注册的 verifierId、verifierDefinitionHash 和 registry snapshot hash；平台不得猜默认 verifier。verifier 只能证明注册表声明的 claim 范围；当前文件哈希 verifier 仅证明 `source-integrity...`，不得用于编译、Runtime、Release、性能、视觉或任意业务成功。未注册、缺失绑定、claim 范围不匹配或同名定义漂移不能完成 accepted。
- 所有后续 mutation 必须携带当前 TaskRevision、ContextVersion 和绑定同一 canonical operation fingerprint 的 idempotency key。
- Automation Run、Codex Session、ReadSnapshot、Semantic Archive、Skill 和 Worker 只能提供适配输入或候选证据；它们不得直接写 event、Receipt 或 completionDecision。
- 新提交使用中央合同的 `candidateOutcome`、`candidateEvidenceHash`、`candidateProducerType`，并绑定合同 ID/hash；旧 TaskContext v1 的 `outcome`、`evidenceHash`、`producerType` 只允许经显式 legacy projection 进入同一平台规范化路径。两者都只保存在 candidate 字段。平台必须按 Profile 选择 verifier、重读工件和 sourceScope 文件、重算 SHA-256 并推导 normalized outcome；提交者声明、合同 hash 或平台推导冲突时拒绝提交。
- `completionDecision=accepted` 只能由平台 evaluator 产生，并绑定不可变 Receipt；`deliveryAcceptance` 保持独立。

任何路径越界、源漂移、CAS 冲突、证据过期/缺失、关键 Contradiction、Receipt/Event Hash 不一致、未知 action 或幂等键跨操作复用都必须 fail closed。禁止通过修改现有 event/receipt、删除 orphan 或回滚历史来恢复。

## 取消、恢复与验证

取消发生在下一次 create-only mutation 之前；已提交 event 不回滚。中断后重新读取最后一条连续、Hash 有效的 event，忽略未被 event 引用的 orphan Receipt，并以新的 idempotency key 和当前 CAS 对重试。

## ContractCompleteness

```text
commandId: task.context-runtime.mutate
cancellation: before each create-only mutation; committed events and receipts are immutable and never rolled back.
recovery: reread last contiguous hash-valid event, ignore unreferenced orphan receipts, retry with current CAS and new idempotencyKey; conflicts fail closed.
validation: platform schema/action, TaskRevision/ContextVersion, GoalRevision/AcceptanceProfile verifier bindings, sourceScope, Event/Receipt hashes and evaluator result.
evidenceRef: commandId, commandBodyHash, planHash, taskId, TaskRevision, ContextVersion, Event/Receipt hashes, verifier snapshot and source SHA-256.
allowRoots: current TaskId events and accepted Completion Receipt under the declared project-relative StoreRoot only.
denyPaths: existing event/receipt edits, orphan deletion, Assets, source, Git, Unity/Worker startup, release, Runtime and external state; deny-overrides.
```

## 交付格式

交付前运行平台确定性测试、OutcomeEvaluator、`/eval` Adapter 回放、StaticDeepReplay、严格 Evidence Receipt 校验、Skill/Knowledge/route 发现验证、严格 UTF-8 与目标 diff 检查。报告 TaskId、TaskRevision、ContextVersion、completionDecision、deliveryAcceptance、Receipt 绑定、实际 StoreRoot、验证状态和 `runtime-not-run`；静态证据只能证明 advisory `/eval` 的源码注册和隔离 Worker 回放，不得声明 Unity 注册、生产执行、其他 AIBrain 生命周期动作或 Release acceptance。
