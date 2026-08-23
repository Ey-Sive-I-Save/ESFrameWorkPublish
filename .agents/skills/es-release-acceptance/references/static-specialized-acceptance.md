# Release acceptance evidence matrix

## Purpose

This is a responsibility-specific static acceptance plan for **es-release-acceptance**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `release`
- Acceptance id: `release-evidence-static`
- Runtime boundary: Player, IL2CPP, performance and platform behavior require Runtime/Release evidence.
- Static assertions: acceptance matrix; fresh hash; compatibility; missing evidence; release gate

## Required specialized cases

- `matrix-completeness`: replay the matrix-completeness contract from source/configuration and record pass or blocked evidence.
- `missing-evidence`: replay the missing-evidence contract from source/configuration and record pass or blocked evidence.
- `hash-freshness`: replay the hash-freshness contract from source/configuration and record pass or blocked evidence.
- `compatibility-boundary`: replay the compatibility-boundary contract from source/configuration and record pass or blocked evidence.
- `runtime-gate-scope`: replay the runtime-gate-scope contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `references/evidence-matrix.md`
- `scripts/Test-ESAcceptanceMatrix.ps1`
- `references/evidence-receipt-contract.md`
- `governance.json`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
