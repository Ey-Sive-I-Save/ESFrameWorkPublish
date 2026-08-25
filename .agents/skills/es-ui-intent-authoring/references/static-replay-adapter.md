# Static replay adapter: engineering

Responsibility profile: authoring

This adapter replays IntentSpec parsing and validation without Unity or project asset writes.
Cases covered: `normal-input`, `invalid-input`, `denied-expansion`, `repeat-idempotency`,
`hash-change-cache-invalidation`, `interruption-recovery`, and `deterministic-output`.

Custom checks covered: `authority-routing`, `bounded-output`, `deterministic-replay`,
`evidence-contract`, and `compatibility-boundary`.

The local self-test exercises valid, malformed, ambiguous, business-expansion, runtime-expansion,
repeat and recovery-shaped inputs. The shared replay manifest carries the source-hash invalidation
and evidence boundary contract.
