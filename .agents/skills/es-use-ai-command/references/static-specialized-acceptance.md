# AICommand discovery and safe invocation boundary

## Purpose

This is a responsibility-specific static acceptance plan for **es-use-ai-command**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `governance`
- Acceptance id: `aicommand-usage-boundary`
- Runtime boundary: Static checks prove command selection and boundaries, not actual external execution.
- Static assertions: AICommand; authority; target path; dry-run; developer authorization

## Required specialized cases

- `command-discovery`: replay the command-discovery contract from source/configuration and record pass or blocked evidence.
- `authority-match`: replay the authority-match contract from source/configuration and record pass or blocked evidence.
- `target-path-boundary`: replay the target-path-boundary contract from source/configuration and record pass or blocked evidence.
- `runtime-authorization`: replay the runtime-authorization contract from source/configuration and record pass or blocked evidence.
- `dry-run-idempotency`: replay the dry-run-idempotency contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `scripts/Find-ESAICommands.ps1`
- `scripts/Test-ESAICommands.ps1`
- `references/evidence-receipt-contract.md`
- `governance.json`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
