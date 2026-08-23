# Skill creation and registration pipeline

## Purpose

This is a responsibility-specific static acceptance plan for **es-skill-creator**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `governance`
- Acceptance id: `creator-pipeline`
- Runtime boundary: Runtime is not required to prove scaffold structure or catalog registration.
- Static assertions: explicit output path; registration is idempotent; quick validation; catalog hash refresh; UTF-8

## Required specialized cases

- `scaffold-contract`: replay the scaffold-contract contract from source/configuration and record pass or blocked evidence.
- `invalid-name-rejection`: replay the invalid-name-rejection contract from source/configuration and record pass or blocked evidence.
- `resource-composition`: replay the resource-composition contract from source/configuration and record pass or blocked evidence.
- `registration-idempotency`: replay the registration-idempotency contract from source/configuration and record pass or blocked evidence.
- `catalog-refresh`: replay the catalog-refresh contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `scripts/init_skill.py`
- `scripts/Build-ESSkillCatalog.py`
- `scripts/quick_validate.py`
- `references/evidence-receipt-contract.md`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
