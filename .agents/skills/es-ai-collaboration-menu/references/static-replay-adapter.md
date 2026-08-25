# Static Replay Adapter

Responsibility profile: governance

This Skill's StaticDeepReplay surface is the deterministic menu renderer and its
contract. Replay supplies bounded prompt/context fixtures and checks stable option
IDs, one-or-zero recommendation, invalid-input rejection, and no-execute claims.
It does not prove that a user chooses the correct route or that any selected domain
Skill, Unity process, editor window, or release path succeeds.

## Replay coverage

The adapter documents the governance responsibility profile and all manifest cases:
`normal-input`, `invalid-input`, `denied-expansion`, `repeat-idempotency`,
`hash-change-cache-invalidation`, `interruption-recovery`, and `deterministic-output`.
The replay also checks the custom checks `authority-routing`, `deterministic-replay`,
and `evidence-contract`. It checks natural-language intent routing, negation safety,
compound stages, decision receipts, and bracketed numbering in the local behavior
suite. Deterministic replay records source hashes and requires a
fresh replay when a bundled source changes. Interruption recovery is a rerun of the
stateless renderer and therefore has no cleanup obligation.
