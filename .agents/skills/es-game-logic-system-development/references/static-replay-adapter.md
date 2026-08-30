See .agents/skills/es-static-deep-replay/references/static-replay-contract.md and this Skill's static-replay.manifest.json. StaticDeepReplay is required before Runtime escalation.

Case assertion coverage:
- normal-input : source roots and required Skill files are present
- invalid-input : invalid input is rejected by the declared contract
- denied-expansion : permission or path expansion is denied
- repeat-idempotency : repeat execution is idempotent
- hash-change-cache-invalidation : source drift invalidates stale state
- interruption-recovery : interruption and recovery are declared
- deterministic-output : deterministic output and stable ordering are declared

Responsibility profile: engineering
Responsibility scope: Custom static acceptance for governed game-logic system design and integration.
Custom checks:
- authority-routing
- permission-boundary
- deterministic-replay
- evidence-contract
