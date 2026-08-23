See .agents/skills/es-static-deep-replay/references/static-replay-contract.md and this Skill's static-replay.manifest.json. StaticDeepReplay is required before Runtime escalation.

Case assertion coverage:
- normal-input : source roots and required Skill files are present
- invalid-input : invalid input is rejected by the declared contract
- denied-expansion : permission or path expansion is denied
- repeat-idempotency : repeat execution is idempotent
- hash-change-cache-invalidation : hash or cache changes invalidate stale state
- interruption-recovery : interruption and recovery are declared
- deterministic-output : deterministic output and stable ordering are declared

Responsibility profile: session
Responsibility scope: Custom static acceptance for session responsibilities.
The shared StaticDeepReplay report is also a strict evidence receipt: it carries `case=StaticDeepReplay`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, `sourceRefHashes`, `toolId`, `unityVersion`, `capturedUtc`, and `planHash`, while retaining the per-case `cases[]` detail.
Custom checks:
- consistency-cache
- change-boundary
- deterministic-replay
- evidence-contract
