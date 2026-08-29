# TaskContextRuntime StaticDeepReplay Adapter

Before the shared replay hashes the bounded source set, the Skill runner executes GoalRevision, RoutePlan real-artifact replay, integration-policy, central Evidence, verifier registry, OutcomeEvaluator, core lifecycle, representative Schema, and advisory `/eval` adapter validators. RoutePlan production, validation, TaskContext consumption, and fixtures share one canonical PowerShell module. Its negative matrix rejects forged plan/source hashes, missing Goal/Registry SourceRefs, stale HEAD or artifacts, unregistered stages, Profile/routeKey mismatch, and unauthorized depth. The platform tests use isolated temporary projects. The Adapter test starts only a bounded Windows PowerShell Worker fixture and verifies source registration, CAS rejection, hash drift, CompletionDecision injection denial, output-scope confinement, registration-failure isolation, and the explicit non-claims for Unity Runtime and production execution.

The integration policy validator checks stable prohibited/conditional capability IDs, Core profile isolation, explicit Runtime/Release selection, and the AIBrain-to-Facade dependency. Missing unselected adapters are evidence gaps only for a profile that explicitly selects them.

- normal-input: accepted transition and Receipt binding
- invalid-input: malformed state, evidence, transition, or CAS rejection
- denied-expansion: project-relative source/store boundary
- repeat-idempotency: duplicate key returns the original revision
- hash-change-cache-invalidation: source drift produces local partial invalidation
- interruption-recovery: orphan Receipt is ignored and retry completes
- deterministic-output: canonical SHA-256 event/Receipt chain
- route-plan-binding: one canonical implementation replays routeKeys, stage registry membership, Goal/Registry SourceRefs, Git HEAD, and depth authorization before lifecycle mutation

Responsibility profile: engineering
Responsibility scope: state ownership and recoverable platform mutation.

Custom checks:
- input-boundary
- recovery-cache
- change-boundary
- deterministic-replay
- evidence-contract

`runtime-not-run` leaves Unity, Worker, host adapter, timing, and release behavior unproven.

The full runner accepts `-ValidatorTimeoutSeconds` and `-ProgressPath`; each
validator is executed in an isolated external PowerShell process and progress is
written as UTF-8 JSON. A timeout returns exit code `124` and is never promoted
to a passing replay.
