# Static replay adapter

Authority: project StaticDeepReplay contract and this Skill's manifest.
Scope: source/configuration replay only.
StaleWhen: shared replay contract, manifest or specialized acceptance changes.
Evidence: deterministic replay report produced by the shared validator.

Read `.agents/skills/es-static-deep-replay/references/static-replay-contract.md` and `static-replay.manifest.json`. The local runner delegates to the shared engine and must not invoke Feishu, Node, Unity or the network.

Case assertions:

- `normal-input`: fixed route and three-operation allowlist are present.
- `invalid-input`: malformed fields and unsupported operations are rejected by contract.
- `denied-expansion`: direct execution, secret input, mutation and path expansion are denied.
- `repeat-idempotency`: InvocationId semantics are declared and bound to normalized input.
- `hash-change-cache-invalidation`: stale plan/evidence/cache is invalidated.
- `interruption-recovery`: timeout, cancellation and Domain Reload terminal evidence is required.
- `deterministic-output`: bounded normalized output, SourceRef and receipt identity are declared.

Responsibility profile: engineering

Custom checks: `authority-routing`, `operation-allowlist`, `credential-isolation`, `external-data-boundary`, `evidence-contract`.
