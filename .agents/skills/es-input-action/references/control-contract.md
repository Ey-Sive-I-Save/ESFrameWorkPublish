# es-input-action control contract

- Verify scope, authority, and source evidence before changing project state.
- Apply the central user-directed action authority: a current explicit user request authorizes its bounded action; only inferred expansion is denied. Action-specific side effects must be named, and AIBrain/AICommand inputs apply only when their managed channel is selected.
- Record positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery results.
- Stop on missing evidence, stale hashes, encoding failures, or ownership ambiguity.
