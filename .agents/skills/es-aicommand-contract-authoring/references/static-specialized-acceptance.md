# AICommand and TaskContract binding

## Purpose

This is a responsibility-specific static acceptance plan for **es-aicommand-contract-authoring**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `governance`
- Acceptance id: `aicommand-contract`
- Runtime boundary: Runtime cannot be inferred from command text; only static authorization binding is proven.
- Static assertions: AICommand; TaskContract; write scope; risk level; command hash

## Required specialized cases

- `command-id-closure`: replay the command-id-closure contract from source/configuration and record pass or blocked evidence.
- `task-contract-binding`: replay the task-contract-binding contract from source/configuration and record pass or blocked evidence.
- `write-scope-denial`: replay the write-scope-denial contract from source/configuration and record pass or blocked evidence.
- `risk-level-consistency`: replay the risk-level-consistency contract from source/configuration and record pass or blocked evidence.
- `command-hash-stale`: replay the command-hash-stale contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `references/command-contract.md`
- `scripts/Test-ESAICommandContract.ps1`
- `references/evidence-receipt-contract.md`
- `governance.json`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
