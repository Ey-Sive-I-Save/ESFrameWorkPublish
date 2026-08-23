# Editor availability, layout and lifecycle static acceptance

## Purpose

This is a responsibility-specific static acceptance plan for **es-editor-availability-validator**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `editor`
- Acceptance id: `editor-availability-static`
- Runtime boundary: Window geometry, panel mounting and real interaction remain Runtime claims; static code layout proof is reported separately.
- Static assertions: minSize; maxSize; narrow; DPI; ReloadDomain; Unbind

## Required specialized cases

- `min-max-window-contract`: replay the min-max-window-contract contract from source/configuration and record pass or blocked evidence.
- `narrow-wide-layout`: replay the narrow-wide-layout contract from source/configuration and record pass or blocked evidence.
- `dpi-boundary`: replay the dpi-boundary contract from source/configuration and record pass or blocked evidence.
- `reload-unbind`: replay the reload-unbind contract from source/configuration and record pass or blocked evidence.
- `runtime-escalation-scope`: replay the runtime-escalation-scope contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `references/availability-matrix.md`
- `scripts/Invoke-ESEditorAvailability.ps1`
- `references/evidence-receipt-contract.md`
- `governance.json`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
