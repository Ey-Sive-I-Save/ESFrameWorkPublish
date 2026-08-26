# TaskContextRuntime v1 State Transition Contract

The acceptance transition is fixed:

```text
Active + Live
  -> completionDecision=accepted
  -> Completed + Frozen
  -> deliveryAcceptance=pending
```

`accepted` requires an immutable RoutePlan whose canonical identity, exact routeKeys, stage/Profile/route/depth registry membership, GoalRevision and registry SourceRefs, Git HEAD, and all SourceRef hashes are replayed at creation and completion; an immutable frozen GoalRevision path/hash binding that is reread at completion; a frozen AcceptanceProfile whose required claims each select a registered verifier and whose central Evidence contract ID/hash still match; complete fresh platform-verified EvidenceSet; a current verified sourceScope; no critical contradiction; no unresolved SourceDrift; no violating UnverifiedClaims; an immutable Receipt bound to TaskId/RoutePlanHash/RoutePlanArtifactHash/RoutePlanSnapshotHash/GoalRevisionHash/TaskRevision/ContextVersion/EvidenceContractId/EvidenceContractHash/EvidenceSetHash/AcceptanceProfileHash/sourceScope hash; and a successful CAS. Missing or drifted RoutePlan, GoalRevision, or Evidence contract produces `undetermined` for this task completion claim; it does not create a project-global block.

## RoutePlan trust binding

`ES/Automation/RoutePlan/ESRoutePlanContract.psm1` is the PowerShell canonical owner for RoutePlan hashing and artifact replay. Producers, fixtures, the standalone validator, and TaskContext consumers must not copy its field order or snapshot rules. A `Ready` RoutePlan freezes an ordinal routeKeys set and is accepted only when every selected stage exactly matches one central registry definition for the frozen Profile and at least one frozen routeKey. Depth 2 additionally requires the exact directional registry authorization. The GoalRevision artifact and route-stage registry must each occur exactly once in the canonical SourceRef set.

RoutePlan format errors, stale snapshots, or registry mismatches affect only that RoutePlan and the task claim that consumes it. They do not become project-global P0 and do not narrow current-user-direct authority.

## Platform evidence derivation

```text
Skill / Worker / human CandidateEvidence
  -> validate es-platform-evidence-v1 contract identity/hash
  -> project legacy TaskContext v1 fields through an explicit compatibility adapter
  -> preserve candidateOutcome / candidateEvidenceHash / candidateProducerType
  -> resolve verifier from frozen AcceptanceProfile
  -> reread bounded artifact and source files
  -> recompute SHA-256 and derive outcome
  -> persist platform-normalized EvidenceSet
  -> reread again during completion evaluation
  -> issue Receipt only after all selected verifiers still agree
```

No candidate or artifact `outcome` field is authoritative. `ES/Automation/Contracts/es-platform-evidence-v1.schema.json` is the central structural source for canonical CandidateEvidence, the bounded legacy projection, and normalized EvidenceSet. The frozen profile binds its raw SHA-256, and the accepted Receipt repeats that binding. The initial deterministic verifier is `platform.file-hash-manifest-v1`: it accepts only `source-integrity...` claims and a bounded observation manifest, rehashes every observation path within the verified sourceScope, derives failure on any mismatch, unverified on an empty observation set, and passed only when all hashes match. It must never be bound to compile, Runtime, Release, performance, visual, or domain-success claims. The frozen profile has no implicit verifier default and binds `verifierRegistryHash` for provenance plus a `verifierDefinitionHash` per required claim; completion reuses only the bound definition and treats definition drift as evidence drift. A candidate identity is never promoted to platform authority merely because it says `platform`, `human`, `skill`, or `worker`.

`deliveryAcceptance=rejected` does not reverse platform acceptance. Delivery acceptance is a one-way transition from `pending` to either `accepted` or `rejected`; changing a final delivery decision requires an explicit `Reopen` or a follow-up task. Reopen creates a new revision and context version, returns to `Active+Live`, clears the current EvidenceSet and Receipt projection, and retains historical immutable events and receipts.

Completion outcomes:

| Decision | TaskStatus | ContextStatus | Meaning |
|---|---|---|---|
| accepted | Completed | Frozen | platform acceptance gates passed and Receipt is bound |
| rejected | Blocked | Live | authoritative evidence contradicts or fails a required claim |
| undetermined | Active | Live or PartiallyInvalidated | evidence is absent, stale, unverified, or source drift exists |

Context transitions are `Live -> Compacting -> Live`, `Live/Compacting -> PartiallyInvalidated -> Live` after reverification, `Frozen -> Archived`, any non-quarantined context to `Quarantined`, and Quarantined back to its exact recorded non-quarantined context. Terminal task states are never inferred from Automation, Skill, Worker, or delivery labels.

Every idempotency key is bound to a canonical operation fingerprint. An exact retry returns the original committed state; reuse for another action or different inputs is rejected.

## Advisory evaluation

`New-ESTaskEvaluationRecord` and the managed `es.task-context.evaluate@1` adapter are read-state/write-evidence operations, not lifecycle transitions. They require the current CAS pair for snapshot identity, derive a task-object `EvaluationRecord` through the frozen evaluator, and leave `TaskRevision`, `ContextVersion`, `TaskStatus`, and `ContextStatus` unchanged. Automation `Accepted`, `Completed`, or `Blocked`, Automation `Static/Runtime`, and `governanceHash` remain transport-contract values and cannot be projected into the evaluation decision, Profile, scope, record hash, snapshot hash, or global P0.

The adapter has a source-registered `TaskContract -> ESAutomationFacade -> managed Worker` path. Registration failure is isolated to this endpoint so other Automation endpoints continue registering. `sourceRegistrationIntegrated=true` is not Runtime evidence; until Unity-hosted discovery and execution are observed, `unityRuntimeRegistrationVerified=false`, `productionExecutionObserved=false`, and `runtime-not-run` remain mandatory.

## Integration acceptance boundary

Core v1 does not support global automatic Skill wrapping, adapter-owned lifecycle mutation, discovery-triggered business execution, or projection of `ESAutomationRunStatus.Accepted` into platform completion. Their absence is intentional and may not fail any acceptance profile.

AIBrain `runTask`, an `ESAutomationFacade` Task endpoint, Worker EvidenceSet submission, Codex Session context adaptation, Semantic Archive adaptation, and Unity Editor task creation are conditional adapters. The advisory `/eval` source path implements one narrow AIBrain/Facade route without enabling the other lifecycle actions. Conditional adapters are not Core v1 completion gates and do not block `StaticReview` or `EngineeringReadiness`. A conditional adapter becomes required only when its stable capability ID is explicitly listed in the active `RuntimeAcceptance` or `ReleaseAcceptance` profile's `requiredCapabilityIds`. Selecting `aibrain.run-task` also requires `automation-facade.task-endpoint`.

The machine-readable authority is `ES/Automation/Contracts/es-task-context-runtime-integration-policy-v1.json`; `Test-ESTaskContextRuntimeIntegrationPolicy.ps1` rejects profile leakage and capability identity drift.
