# Static replay adapter: engineering

Responsibility profile: authoring

This adapter validates ScreenSpec v3 schema, registered component ownership, bounded artifact declarations, deterministic ordering, and evidence boundaries without starting Unity or writing Assets. Cases covered: `normal-input`, `invalid-input`, `denied-expansion`, `repeat-idempotency`, `hash-change-cache-invalidation`, `interruption-recovery`, and `deterministic-output`.

Custom checks covered: `authority-routing`, `bounded-output`, `deterministic-replay`, `evidence-contract`, and `compatibility-boundary`.
