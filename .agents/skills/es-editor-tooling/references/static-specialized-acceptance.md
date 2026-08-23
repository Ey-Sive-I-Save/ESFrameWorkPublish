# Editor tooling host and write boundary

## Purpose

This is a responsibility-specific static acceptance plan for **es-editor-tooling**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `editor`
- Acceptance id: `editor-tooling-boundary`
- Runtime boundary: Static checks cannot prove Unity host rendering or click behavior.
- Static assertions: Editor Host; SerializedObject; Undo; Dirty; Panel rebuild

## Required specialized cases

- `host-selection`: replay the host-selection contract from source/configuration and record pass or blocked evidence.
- `window-lifecycle`: replay the window-lifecycle contract from source/configuration and record pass or blocked evidence.
- `serialized-property-boundary`: replay the serialized-property-boundary contract from source/configuration and record pass or blocked evidence.
- `undo-dirty-contract`: replay the undo-dirty-contract contract from source/configuration and record pass or blocked evidence.
- `panel-rebuild`: replay the panel-rebuild contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `references/control-contract.md`
- `references/project-map.md`
- `scripts/Test-ESEditorBoundary.ps1`
- `references/evidence-receipt-contract.md`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
