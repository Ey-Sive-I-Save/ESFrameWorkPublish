# Weapon ABCP Static Replay Adapter

Responsibility profile: authoring

The replay is project-relative, strict UTF-8 and read-only. It covers:
`normal-input`, `invalid-input`, `denied-expansion`, `repeat-idempotency`,
`hash-change-cache-invalidation`, `interruption-recovery`, and
`deterministic-output`. Custom checks are `authority-routing`,
`permission-boundary`, `deterministic-replay`, `evidence-contract`,
`knowledge-boundary`, `bounded-output`, `compatibility-boundary`, and
`runtime-escalation`.

The adapter verifies that Weapon ABCP references ABCC by stable ID, keeps Part
as canonical owner, and permits Dynamic fallback only explicitly. Runtime is
`runtime-not-run`.
