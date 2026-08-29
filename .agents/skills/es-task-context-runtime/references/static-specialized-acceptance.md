# TaskContextRuntime State Machine Acceptance

Acceptance id: `task-context-runtime-state-machine`

## Static cases

- `accepted-transition`: required fresh evidence and current sourceScope produce accepted `Completed+Frozen` with delivery pending.
- `delivery-independence`: delivery rejection preserves platform completion and the bound Receipt.
- `cas-conflict`: stale TaskRevision or ContextVersion cannot append an event.
- `source-drift`: a changed source hash prevents acceptance and moves Context to partial invalidation.
- `goal-revision-drift`: a missing, mutable, or hash-drifted frozen GoalRevision prevents accepted completion without creating a project-global block.
- `route-plan-binding`: a Ready RoutePlan is independently replayed from real artifacts; its canonical hash/ID, routeKeys, stage/Profile/route/depth registry membership, Goal/Registry SourceRefs, Git HEAD, and SourceRef hashes must close before task creation and again before accepted completion.
- `verifier-claim-scope`: every required claim has an explicit registered verifier binding, and a verifier cannot prove a claim outside its registered pattern.
- `verifier-registry-contract`: registry structure and semantics reject duplicate IDs, unanchored claim patterns, duplicate fields, authority expansion, and definition drift; unrelated registered verifiers do not invalidate a task's unchanged bound verifier. `platform.static-replay-v1` binds the shared runner hash, enforces pre-read file/byte budgets and sourceScope containment, reruns StaticDeepReplay, rejects candidate outcome forgery, and removes bounded scratch output.
- `platform-evidence-contract`: canonical CandidateEvidence and the legacy compatibility projection both normalize through the same frozen central contract; forged contract hashes, added authority fields, missing fields, and contract drift cannot produce accepted completion.
- `outcome-evaluator`: frozen evaluator identity, decision derivation, EvaluationRecord hashing, failure records, evidence/influence references, and lifecycle-zero-change behavior are deterministic.
- `advisory-eval-adapter-isolation`: the exact source-registered `/eval` path binds its Schema and Worker hashes, rejects CAS/hash/scope/cross-contract forgeries, isolates registration failure, and cannot claim Unity Runtime, production execution, or global P0 integration.
- `commercial-metric-aggregation`: verified task observations deterministically derive success, stable success, task-scoped hard-violation, latency, recovery, and registered regression metrics; unavailable cost, correction, and claim-audit sources remain `evidence-pending/null`.
- `receipt-integrity`: event and Receipt hashes/bindings detect mutation.
- `interruption-recovery`: orphan Receipt data is non-authoritative and does not block a valid retry.
- `reopen-revision`: explicit Reopen advances both versions and starts a new active evaluation revision.
- `idempotency-operation-binding`: an exact retry is stable and cross-operation key reuse is rejected.
- `reparse-boundary`: source, store, and bound Receipt paths reject descendant junction or symbolic-link traversal.
- `quarantine-recovery`: recovery restores the exact recorded non-quarantined ContextStatus.
- `integration-profile-isolation`: prohibited capabilities never become acceptance requirements, while conditional adapters are non-blocking unless explicitly selected by a Runtime/Release profile.
- `focus-runtime-integration`: a confirmed FocusContext (including a restored checkpoint) maps to a TaskContextRuntime request without widening scope or forbidden capabilities; stale confirmation, identity mismatch, CAS conflict, and idempotency-key operation reuse fail closed.

## Source assertions

The platform source must preserve frozen RoutePlan and GoalRevision identity, exact routeKeys and registry membership, verifier definition identity, `TaskRevision`, `ContextVersion`, `completionDecision`, `deliveryAcceptance`, source drift, and immutable `Receipt` semantics. FocusContext projection and Runtime mapping must preserve scope, forbidden capabilities, required reads, signals, identity, and checkpoint hash without becoming lifecycle authority. The Skill must remain an adapter and never become lifecycle authority.

## Evidence

Run `scripts/Test-ESGoalV1.ps1`, `scripts/Test-ESRoutePlanContract.ps1`, `scripts/Test-ESEvidenceVerifierRegistry.ps1`, `scripts/Test-ESPlatformEvidenceContract.ps1`, `scripts/Test-ESOutcomeEvaluation.ps1`, `scripts/Test-ESTaskContextRuntime.ps1`, `scripts/Test-ESTaskContextRuntimeSchema.ps1`, `scripts/Test-ESTaskContextRuntimeIntegrationPolicy.ps1`, `scripts/Test-ESTaskContextEvaluationAdapter.ps1`, `scripts/Test-ESCommercialEvaluation.ps1`, `scripts/Test-ESTaskFocusStaticReplay.ps1`, the Skill validator profiles, and `scripts/Test-es-task-context-runtime-StaticReplay.ps1`. All listed cases, representative Schema documents, RoutePlan real-artifact negatives, integration profile-isolation rules, commercial metric false-zero negatives, FocusContext mapping/CAS/idempotency negatives, and source-tamper rejection must pass. Runtime evidence remains `runtime-not-run` and does not prove Unity-hosted registration, production Worker execution, external timing, or release behavior.
