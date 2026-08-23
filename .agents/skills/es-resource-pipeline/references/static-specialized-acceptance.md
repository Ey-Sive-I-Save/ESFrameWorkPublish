# Resource pipeline manifest and provider closure

## Purpose

This is a responsibility-specific static acceptance plan for **es-resource-pipeline**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `authoring`
- Acceptance id: `resource-pipeline-static`
- Runtime boundary: Static manifest closure does not prove remote provider or upload behavior.
- Static assertions: manifest; provider; dependency; duplicate; recovery

## Required specialized cases

- `stage-manifest`: replay the stage-manifest contract from source/configuration and record pass or blocked evidence.
- `provider-identity`: replay the provider-identity contract from source/configuration and record pass or blocked evidence.
- `dependency-closure`: replay the dependency-closure contract from source/configuration and record pass or blocked evidence.
- `duplicate-resource`: replay the duplicate-resource contract from source/configuration and record pass or blocked evidence.
- `recovery-boundary`: replay the recovery-boundary contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `references/control-contract.md`
- `references/project-map.md`
- `scripts/Test-ESResourceStageManifest.ps1`
- `references/evidence-receipt-contract.md`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
