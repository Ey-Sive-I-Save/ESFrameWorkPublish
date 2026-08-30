# Static replay adapter

Responsibility profile: authoring
Responsibility scope: Custom static acceptance for bounded resource collection and AssetPackage projection responsibilities.

The local runner delegates to `es-static-deep-replay` with `static-replay.manifest.json`. It checks Skill structure, governance metadata, boundary declarations, deterministic ordering, invalid-input rejection, idempotency and interruption recovery. Cases are normal-input, invalid-input, denied-expansion, repeat-idempotency, hash-change-cache-invalidation, interruption-recovery, and deterministic-output. Custom checks are change-boundary, resource-projection, deterministic-replay, and evidence-contract. `responsibilityProfile`: `authoring`. It does not run Unity, download files, modify Assets, or publish releases.
