# StaticDeepReplay engine integrity

## Purpose

This is a responsibility-specific static acceptance plan for **es-static-deep-replay**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `base`
- Acceptance id: `static-replay-engine`
- Runtime boundary: This engine deliberately does not claim Unity, display, timing, process, or release behavior.
- Static assertions: customCheckResults; responsibilityProfile; source hashes; deterministic output; runtime-not-run

## Required specialized cases

- `manifest-schema`: replay the manifest-schema contract from source/configuration and record pass or blocked evidence.
- `source-root-closure`: replay the source-root-closure contract from source/configuration and record pass or blocked evidence.
- `custom-check-dispatch`: replay the custom-check-dispatch contract from source/configuration and record pass or blocked evidence.
- `hash-determinism`: replay the hash-determinism contract from source/configuration and record pass or blocked evidence.
- `report-integrity`: replay the report-integrity contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `scripts/Invoke-ESStaticDeepReplay.ps1`
- `scripts/Test-ESStaticReplayManifest.ps1`
- `references/static-replay-contract.md`
- `references/static-case-standard.md`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
