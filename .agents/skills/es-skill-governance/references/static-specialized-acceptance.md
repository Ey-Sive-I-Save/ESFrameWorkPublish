# Governance contract and authority closure

## Purpose

This is a responsibility-specific static acceptance plan for **es-skill-governance**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `governance`
- Acceptance id: `governance-contract`
- Runtime boundary: Runtime cannot prove authority closure; only execution authorization receipts can prove actual enforcement.
- Static assertions: authority refs are closed; runtime hard gate; StaticDeepReplay-first; permission expansion denied; governance hash

## Required specialized cases

- `metadata-completeness`: replay the metadata-completeness contract from source/configuration and record pass or blocked evidence.
- `authority-ref-closure`: replay the authority-ref-closure contract from source/configuration and record pass or blocked evidence.
- `permission-denial`: replay the permission-denial contract from source/configuration and record pass or blocked evidence.
- `profile-weight`: replay the profile-weight contract from source/configuration and record pass or blocked evidence.
- `stale-governance-hash`: replay the stale-governance-hash contract from source/configuration and record pass or blocked evidence.
- `es-entry-compatibility`: replay the ES AIBrain -> Facade -> TaskContract/Worker boundary and reject direct parallel execution entry points.

## Evidence artifacts

- `SKILL.md`
- `governance.json`
- `references/verification-semantics.md`
- `references/runtime-authorization-contract.md`
- `scripts/Test-ESSkillContract.ps1`
- `scripts/Test-ESRuntimeAuthorizationContract.ps1`
- `scripts/Test-ESAutomationCompatibility.ps1`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
