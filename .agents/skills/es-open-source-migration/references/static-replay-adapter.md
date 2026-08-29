See `../../es-static-deep-replay/references/static-replay-contract.md` and this Skill's
`static-replay.manifest.json`. StaticDeepReplay is required before Runtime escalation.

Acceptance id: external-migration-static.

Specialized static cases:

- path-containment: isolated source paths cannot resolve inside the target project.
- mapping-schema: the persistent migration map validates against its JSON schema.
- permission-denial: network, Git write, delete, rename, and protected-target mutation remain denied.
- partition-determinism: mapping rows and Agent partitions use deterministic ordering.
- recovery-state: interruption records recovery state while preserving the target.

Required cases:

- normal-input: isolated source locator, protected target and map schema are declared.
- invalid-input: missing URL/revision/license or malformed map is rejected.
- denied-expansion: target-root source path, network, Git write, delete and rename are denied.
- repeat-idempotency: the same map revision produces the same deterministic partition.
- hash-change-cache-invalidation: source/target hash drift marks the map stale.
- interruption-recovery: cancellation or partial batch preserves the target and quarantines the batch.
- deterministic-output: mapping rows and agent partitions are stable-sorted.

Responsibility profile: engineering

Custom checks: input-boundary, recovery-cache, deterministic-replay, evidence-contract.

Runtime claims (Unity, Player, IL2CPP, release and process
behavior) are not proven by this adapter.
