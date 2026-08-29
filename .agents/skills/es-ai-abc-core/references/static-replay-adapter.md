# ABCC Static Replay Adapter

Responsibility profile: governance

The replay is project-relative, strict UTF-8 and read-only. It covers:
`normal-input`, `invalid-input`, `denied-expansion`, `repeat-idempotency`,
`hash-change-cache-invalidation`, `interruption-recovery`, and
`deterministic-output`. Custom checks are `authority-routing`,
`permission-boundary`, `deterministic-replay`, `evidence-contract`,
`knowledge-boundary`, `bounded-output`, `compatibility-boundary`, and
`runtime-escalation`.

The adapter verifies that the independent Core contract owns the six ABCD
capability IDs, that A↔B mappings are explicit, and that missing evidence or
semantic mismatch cannot silently execute. Runtime remains `runtime-not-run`.
