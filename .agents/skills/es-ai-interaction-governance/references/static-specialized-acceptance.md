# Interaction governance static acceptance

Required cases:

- `intent-contract-positive`: aligned contract maps to `executionDecision=allow`.
- `intent-contract-invalid`: incomplete, contradictory or invalid revision input is rejected.
- `intent-contract-denied`: `partial`, `unverifiable` and `misaligned` states cannot authorize implementation.
- `intent-contract-idempotency`: repeated validation preserves the normalized contract hash.
- `intent-contract-recovery`: an interrupted or partial contract declares a bounded resume action and stale-state policy.

Static evidence does not prove hidden user intent, human satisfaction, Unity/editor behavior, visual layout, timing, performance, Player, IL2CPP or release behavior.
