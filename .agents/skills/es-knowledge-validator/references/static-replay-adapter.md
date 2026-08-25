See `.agents/skills/es-static-deep-replay/references/static-replay-contract.md` and this Skill's `static-replay.manifest.json`. StaticDeepReplay is required before Runtime escalation.

Case assertion coverage:
- normal-input : required validator files and contracts are present
- invalid-input : malformed entries and index structures are blocked
- denied-expansion : rooted, traversing, and external report paths are denied
- repeat-idempotency : unchanged inputs produce deterministic findings
- hash-change-cache-invalidation : SourceRef changes invalidate prior results
- interruption-recovery : rerun from current files after an interrupted or concurrent read
- deterministic-output : findings use stable ordering and input hashing

Responsibility profile: knowledge
Responsibility scope: Source, hash, identity, route, required-read, and related-Skill closure.
Custom checks:
- knowledge-boundary
- permission-boundary
- deterministic-replay
- evidence-contract
