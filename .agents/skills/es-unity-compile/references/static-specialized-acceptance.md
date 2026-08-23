# Unity compile evidence and deterministic gate

## Purpose

This is a responsibility-specific static acceptance plan for **es-unity-compile**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `engineering`
- Acceptance id: `unity-compile-static`
- Runtime boundary: Static compilation artifacts do not prove Unity Editor reload or PlayMode behavior.
- Static assertions: asmdef; compile log; zero errors; stale receipt; project identity

## Required specialized cases

- `project-identity`: replay the project-identity contract from source/configuration and record pass or blocked evidence.
- `asmdef-closure`: replay the asmdef-closure contract from source/configuration and record pass or blocked evidence.
- `compile-log-classification`: replay the compile-log-classification contract from source/configuration and record pass or blocked evidence.
- `error-zero-contract`: replay the error-zero-contract contract from source/configuration and record pass or blocked evidence.
- `stale-receipt`: replay the stale-receipt contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `references/control-contract.md`
- `scripts/Invoke-ESDotnetBuild.ps1`
- `scripts/Test-ESUnityEvidencePacket.ps1`
- `references/evidence-receipt-contract.md`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
