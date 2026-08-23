---
name: es-static-deep-replay
description: Run bounded, read-only StaticDeepReplay for an ESFramework Skill using source/configuration contracts, fixed negative and recovery cases, hashes, cache invalidation, and deterministic output. Use before any Runtime proposal or when a Skill needs a repeatable source-level evidence packet.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, deterministic scripts, and replay manifests.
- **Runtime**: external Unity/process/display/timing behavior is not executed by this Skill.
- `runtime-not-run` is expected output here and is never treated as a static failure.

## Contract

Every replay is read-only, project-relative, bounded, UTF-8 strict, and hash-bound. The manifest must declare the seven fixed cases: normal input, invalid input, denied expansion, repeat/idempotency, hash-change/cache invalidation, interruption/recovery, and deterministic output. Missing or inapplicable cases are reported; they are never silently skipped.

## Responsibility-specific acceptance

The seven cases are only the common floor. Every Skill listed in `references/specialized-acceptance-registry.json` must also provide `specializedAcceptance` in its manifest and a `references/static-specialized-acceptance.md` guide. The guide defines responsibility-specific cases, source assertions, evidence artifacts, and the Runtime boundary. A common pass cannot substitute for a failed or missing specialized plan.

## Workflow

1. Read the target Skill's `static-replay.manifest.json`.
2. Run `scripts/Invoke-ESStaticDeepReplay.ps1` with an explicit project root and report path.
3. Review `staticStatus`, `overallVerdict`, `claimsNotProven`, and `runtimeEscalation`; do not start Runtime from this Skill.
4. Re-run unchanged input to verify deterministic hashes and output.

## Resources

- `references/static-replay-contract.md`
- `references/static-case-standard.md`
- `scripts/Invoke-ESStaticDeepReplay.ps1`
- `scripts/Test-ESStaticReplayManifest.ps1`

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `static-replay-engine`
- Required cases: `manifest-schema, source-root-closure, custom-check-dispatch, hash-determinism, report-integrity`
- Static assertions: customCheckResults; responsibilityProfile; source hashes; deterministic output; runtime-not-run
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `base`
- Custom checks: `authority-routing, permission-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Read-only project-relative execution, strict UTF-8, bounded case count, and explicit report paths.
- No Unity, process, display, network, or release execution is performed by this Skill.
- Missing, stale, malformed, or hash-inconsistent manifests fail closed and require re-planning.


## Specialized static acceptance

Acceptance ID: `static-replay-engine`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- customCheckResults
- responsibilityProfile
- source hashes
- deterministic output
- runtime-not-run

Required specialized cases: `manifest-schema, source-root-closure, custom-check-dispatch, hash-determinism, report-integrity`
Guidance: `references/static-specialized-acceptance.md`
