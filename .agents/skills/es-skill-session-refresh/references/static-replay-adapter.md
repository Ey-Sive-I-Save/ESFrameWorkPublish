See `.agents/skills/es-static-deep-replay/references/static-replay-contract.md` and this Skill's `static-replay.manifest.json`. StaticDeepReplay is required before any Runtime escalation.

Responsibility profile: session

Case assertion coverage:
- normal-input: Build creates a deterministic session snapshot.
- invalid-input: Invalid session and project-relative paths are rejected.
- denied-expansion: Baseline and output paths cannot escape ProjectRoot.
- repeat-idempotency: Repeated Build yields the same snapshot hash for unchanged bytes.
- hash-change-cache-invalidation: Changed Skill metadata or resource hashes produce a delta.
- interruption-recovery: A written baseline can be compared after a later session invocation.
- deterministic-output: Sorted paths and stable hashes produce deterministic output.

Custom checks:
- change-boundary
- authority-routing
- bounded-output
- deterministic-replay
- evidence-contract
- consistency-cache
