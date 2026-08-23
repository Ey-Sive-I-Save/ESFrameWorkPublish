# Static case standard

Every Skill adapter declares these cases:

1. `normal-input`
2. `invalid-input`
3. `denied-expansion`
4. `repeat-idempotency`
5. `hash-change-cache-invalidation`
6. `interruption-recovery`
7. `deterministic-output`

Each case must have a deterministic input/expected-output description or an explicit `not-applicable` reason. Runtime is an escalation decision, never an automatic next step.
