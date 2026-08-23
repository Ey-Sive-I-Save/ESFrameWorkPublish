# Validator profile isolation and evidence semantics

## Purpose

This is a responsibility-specific static acceptance plan for **es-skill-validator**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `governance`
- Acceptance id: `validator-profile-isolation`
- Runtime boundary: Only Runtime can prove external behavior; the validator must not conflate static and runtime verdicts.
- Static assertions: profile isolation; runtime-not-run; StaticDeepReplay; blocked scope; catalog hash

## Required specialized cases

- `profile-isolation`: replay the profile-isolation contract from source/configuration and record pass or blocked evidence.
- `negative-contract`: replay the negative-contract contract from source/configuration and record pass or blocked evidence.
- `catalog-hash-check`: replay the catalog-hash-check contract from source/configuration and record pass or blocked evidence.
- `boundary-report`: replay the boundary-report contract from source/configuration and record pass or blocked evidence.
- `runtime-not-run-scope`: replay the runtime-not-run-scope contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `scripts/Invoke-ESSkillValidation.ps1`
- `references/validation-rubric.md`
- `references/boundary-decision-contract.md`
- `references/evidence-receipt-contract.md`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
