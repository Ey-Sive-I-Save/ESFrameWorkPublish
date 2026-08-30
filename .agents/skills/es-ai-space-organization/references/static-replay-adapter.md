# Static Replay Adapter

Responsibility profile: governance

This Skill is a deterministic placement guide. Replay checks canonical roots,
unknown-path quarantine, denied destructive expansion, repeat classification,
interruption recovery, and source-hash invalidation. It does not move files and
cannot prove Unity, Runtime, network, or release behavior.

Replay cases: `normal-input`, `invalid-input`, `denied-expansion`,
`repeat-idempotency`, `hash-change-cache-invalidation`,
`interruption-recovery`, and `deterministic-output`. Authority checks additionally cover
`authority-identity`, `discovery-closure`, `non-redundant-body`, `no-competing-root`, and
`no-runtime-competition` through `scripts/Test-ESAISpaceAuthority.ps1`.

Custom checks: `authority-routing`, `permission-boundary`,
`deterministic-replay`.
