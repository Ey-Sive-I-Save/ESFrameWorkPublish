See `../../es-static-deep-replay/references/static-replay-contract.md` and this Skill's
`static-replay.manifest.json`. StaticDeepReplay is required before Runtime escalation.

Acceptance id: external-migration-static.

Specialized static cases:

- path-containment: external source paths cannot resolve inside the target project.
- mapping-schema: the persistent migration map validates against its JSON schema.
- permission-denial: network, Git-history write, and protected-target mutation remain denied; external checkout delete/rename is bounded to the explicit in-place replacement plan.
- partition-determinism: mapping rows and Agent partitions use deterministic ordering.
- recovery-state: interruption records recovery state while preserving the target.
- recovery-phase-authority: the journal distinguishes a pre-commit missing staging tree (safe recovery)
  from a commit-started missing staging tree (fail-closed), including legacy journals without `phase`.
- identity-hardening: compound root fragments are remapped in text, paths and filenames without double-prefixing.
- developer-branding: package developer metadata and legacy author seeds converge on the canonical ES studio identity.
- evidence-separation: binary/licensed trees and Static versus Runtime claims remain separate evidence axes.

Required cases:

- normal-input: external source locator, protected target, in-place default and map schema are declared.
- invalid-input: missing URL/revision/license or malformed map is rejected.
- denied-expansion: target-root source path, network, Git-history write, and unplanned path expansion are denied.
- repeat-idempotency: the same map revision produces the same deterministic partition.
- hash-change-cache-invalidation: source/target hash drift marks the map stale.
- interruption-recovery: cancellation or partial in-place commit restores the external checkout from the transaction journal.
- deterministic-output: mapping rows and agent partitions are stable-sorted.

Responsibility profile: engineering

Custom checks: input-boundary, recovery-cache, deterministic-replay, evidence-contract.

Runtime claims (Unity, Player, IL2CPP, release and process
behavior) are not proven by this adapter.
