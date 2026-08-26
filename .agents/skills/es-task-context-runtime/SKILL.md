---
name: es-task-context-runtime
description: Create, inspect, transition, complete, reopen, archive, quarantine, recover, and verify ESFramework TaskContextRuntime tasks through the platform-owned event/CAS/Receipt API. Use for task lifecycle, context lifecycle, completionDecision, deliveryAcceptance, AcceptanceProfile, EvidenceSet, sourceScope drift, immutable completion receipts, or adapters from Automation, Codex Session, ReadSnapshot, Semantic Archive, Skill, and Worker results.
---

# ES Task Context Runtime

Use the platform core under `ES/Automation/TaskContextRuntime/`. This Skill is a discovery and invocation adapter; it does not own lifecycle state and must not reproduce the state machine in a Skill, Worker, Automation Run, or session handoff.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的“Skill 使用披露”规范。实际使用本 Skill 时，首次用户可见的进度更新必须说明该 Skill 与任务的关系；最终答复必须列出本轮实际影响工作的 Skill 与作用。不要列出仅可用、未使用的 Skill，也不得把披露视为授权、执行或验收证据。

## Platform workflow

1. Read `ES/Automation/Contracts/es-task-context-runtime-intent-v1.json`, the TaskContext and RoutePlan schemas, and [the state transition contract](../../../ES/Automation/TaskContextRuntime/references/state-transition-contract.md).
2. Create a task with a stable TaskId, one immutable RoutePlan artifact, frozen AcceptanceProfile, requested sourceScope, and an idempotency key. `planHash` remains a compatibility projection and must equal the platform-recomputed `routePlanHash`. The platform independently replays the exact routeKeys, route-stage registry membership for the frozen Profile and route, directional depth authorization, GoalRevision and registry SourceRefs, Git HEAD, and canonical hashes through `ESRoutePlanContract.psm1`. Every required claim must explicitly bind a registered verifier whose declared claim scope matches; no default verifier is guessed.
3. Resolve and verify project-relative source paths. Bind every Evidence item to the current verified sourceScope hash.
4. Submit candidate EvidenceSet data. Skills and Workers are producers only; candidate outcome/hash/producer identity remain non-authoritative. The platform rereads artifacts and bounded sources, derives outcome through the frozen verifier definition, normalizes hashes, and computes completionDecision.
5. Complete only through the platform evaluator. Every mutation requires the current TaskRevision and ContextVersion compare-and-swap pair.
6. Treat the immutable Receipt as authoritative only when a contiguous event references it and all bindings verify. Ignore orphan receipts after interruption.
7. Keep deliveryAcceptance independent. A rejected delivery never rolls back accepted platform completion; use feedback plus explicit Reopen or a follow-up task.
8. Run the platform test and integrity validator. Report Static and Runtime evidence separately.

## Entry points

- Managed contract: `Assets/Plugins/ES/AICommands/任务上下文运行时_受控生命周期_AI命令.md` (`task.context-runtime.mutate`)
- Platform module: `ES/Automation/TaskContextRuntime/ESTaskContextRuntime.psm1`
- Platform CLI: `ES/Automation/TaskContextRuntime/Invoke-ESTaskContextRuntime.ps1`
- Skill adapter: `scripts/Invoke-ESTaskContextRuntime.ps1`
- Deterministic platform replay: `scripts/Test-ESTaskContextRuntime.ps1`
- Central CandidateEvidence/EvidenceSet contract: `scripts/Test-ESPlatformEvidenceContract.ps1`
- Evidence verifier registry contract: `scripts/Test-ESEvidenceVerifierRegistry.ps1`
- Frozen GoalRevision contract validation: `scripts/Test-ESGoalV1.ps1`
- RoutePlan artifact and snapshot validation: `scripts/Test-ESRoutePlanContract.ps1`
- Representative JSON Schema validation: `scripts/Test-ESTaskContextRuntimeSchema.ps1`
- Integration profile isolation: `scripts/Test-ESTaskContextRuntimeIntegrationPolicy.ps1`
- Managed advisory `/eval` adapter: `scripts/Test-ESTaskContextEvaluationAdapter.ps1`
- Commercial task-cohort metrics: `scripts/Test-ESCommercialEvaluation.ps1`
- StaticDeepReplay: `scripts/Test-es-task-context-runtime-StaticReplay.ps1`

The CLI accepts one project-relative UTF-8 JSON input and a fixed action allowlist: `Create`, `Get`, `VerifySources`, `SubmitEvidence`, `Complete`, `SetDelivery`, `Transition`, and `Integrity`. Input and output state are bounded by the project root and the configured event store.

The AICommand makes this contract discoverable to the command palette and AIBrain capability surface. One exact advisory endpoint, `es.task-context.evaluate@1`, now has a source-registered `TaskContract -> ESAutomationFacade -> managed PowerShell Worker -> TaskContextRuntime Evaluate` path. It creates only a task-object `EvaluationRecord`, does not mutate lifecycle state, and is reachable only through the existing `planTask -> runTask` transport. Source registration is not Unity Runtime or production-execution evidence; all other TaskContext mutations continue to use the bounded project CLI.

`ES/Automation/Evaluation/ESCommercialEvaluation.psm1` is a read-only task-cohort projection over platform-verified event chains and EvaluationRecords. It derives `successRate`, `stableSuccessRate`, task-scoped `hardViolationRate`, `meanLatency`, `recoveryRate`, and `regressionPassRate`. Regression is counted only when `platform.static-replay-v1` reruns the hash-bound shared StaticDeepReplay implementation inside the verified task sourceScope and the resulting task-object `OutcomeAssertion` is closed. `claimOverstatementRate`, `humanCorrectionRate`, and `meanCost` remain `evidence-pending` with `value=null` until their registered authoritative sources exist; missing telemetry is never converted to zero or project-global failure.

## Integration acceptance policy

`ES/Automation/Contracts/es-task-context-runtime-integration-policy-v1.json` is the machine-readable capability boundary. Core v1 prohibits global automatic Skill wrapping, adapter-owned lifecycle mutation, discovery-triggered business execution, and Automation Accepted-to-completion projection. Those capabilities are not missing requirements.

AIBrain `runTask`, an `ESAutomationFacade` Task endpoint, Worker EvidenceSet submission, Codex Session and Semantic Archive adapters, and Unity Editor task creation are conditional integrations. The advisory `/eval` source path is the first narrow implementation of the AIBrain/Facade capability pair; it does not make the pair Runtime-verified or enable other lifecycle actions. Conditional gaps do not block `StaticReview` or `EngineeringReadiness`; they block `RuntimeAcceptance` or `ReleaseAcceptance` only when the active profile explicitly selects their stable ID through `requiredCapabilityIds`. `aibrain.run-task` additionally requires `automation-facade.task-endpoint`.

## Lifecycle and authority boundary

The fixed accepted transition is `Active+Live -> completionDecision=accepted -> Completed+Frozen`, initially with `deliveryAcceptance=pending`. Accepted requires a frozen profile, complete fresh required evidence, verified current sourceScope, no critical contradiction, no unresolved source drift, bounded unverified claims, immutable Receipt bindings, and a successful CAS.

`ESAutomationRunStatus.Accepted` means only that an Automation run was admitted. It is not `completionDecision=accepted`. Codex Session starts/restores Context, ReadSnapshot contributes source consistency, Semantic Archive requests `Archived`, and Workers provide candidate evidence. None may write platform events directly.

## Failure and recovery

- Invalid input, path expansion, unknown transition, stale CAS, event gaps, and hash mismatch fail closed.
- Repeat/idempotency returns the originally committed event state and does not append a duplicate revision.
- Hash-change/cache invalidation is local: source drift moves Context to `PartiallyInvalidated`; reverify sources and resubmit evidence before completion.
- Interruption/recovery reads the last contiguous valid chain. An unreferenced Receipt remains non-authoritative.
- Quarantine preserves the compatible recovery context. Reopen advances both TaskRevision and ContextVersion while retaining historical immutable artifacts.

## Engineering controls

- Owners: ESFramework Automation maintainers own the platform contract; the task requester owns acceptance.
- Permission boundary: current-user-direct work may invoke the bounded platform API; AIBrain/Worker execution additionally requires its plan and transport contracts. Git, Unity/Worker Runtime, network, release, deletion, and credentials remain separate actions.
- Change boundary: write scope is one task store, one create-only event per mutation, and at most one accepted Completion Receipt. Stop on path expansion, CAS conflict, hash mismatch, or unknown transition.
- Concurrency and idempotency: per-task mutex plus TaskRevision/ContextVersion CAS prevents stale writes; a repeated idempotency key returns the original event state.
- Recovery cache: read only the contiguous hash-valid event chain, reject stale source bindings, and ignore orphan Receipt files.
- Compatibility and performance: adapters preserve existing ES entry points and status meanings. Normal reads are bounded to one task event directory; no repository scan, Unity action, Worker action, or external process is hidden in the fast path.

## Verification boundary

Static verification covers contract shape, representative event/EvidenceSet/Receipt JSON Schema validation, event/hash/Receipt integrity, input boundary, deterministic replay, change boundary, recovery cache, and the fixed cases in `static-replay.manifest.json`. `runtime-not-run` does not fail StaticReview. It does not prove Unity, Worker, external process, display/timing, adapter Runtime, or release behavior.

## Responsibility-specific static acceptance

- Profile: `engineering`
- Acceptance ID: `task-context-runtime-state-machine`
- Custom checks: `input-boundary`, `recovery-cache`, `change-boundary`, `deterministic-replay`, `evidence-contract`
- Required cases: `accepted-transition`, `delivery-independence`, `cas-conflict`, `source-drift`, `receipt-integrity`, `interruption-recovery`, `reopen-revision`

See `references/static-specialized-acceptance.md` and `references/static-replay-adapter.md`.
