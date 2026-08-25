See `.agents/skills/es-static-deep-replay/references/static-replay-contract.md` and this Skill's `static-replay.manifest.json`. StaticDeepReplay is required before any Runtime escalation.

Responsibility profile: governance

Case assertion coverage:

- normal-input: source roots and required Skill files are present.
- invalid-input: invalid input is rejected by the declared contract.
- denied-expansion: permission or path expansion is denied by the alignment gate.
- repeat-idempotency: repeat execution is idempotent.
- hash-change-cache-invalidation: contract revision/hash invalidates stale plans.
- interruption-recovery: interruption and recovery are declared.
- deterministic-output: deterministic output and stable ordering are declared.

Custom checks:

- authority-routing
- deterministic-replay
- evidence-contract

This adapter adds the interaction-specific cases `intent-contract-positive`, `intent-contract-invalid`, `intent-contract-denied`, `intent-contract-idempotency`, and `intent-contract-recovery`. It validates only task-scoped contract structure, deterministic output, stale-revision signaling and bounded recovery; it never treats a score or contract as authorization or runtime proof.
