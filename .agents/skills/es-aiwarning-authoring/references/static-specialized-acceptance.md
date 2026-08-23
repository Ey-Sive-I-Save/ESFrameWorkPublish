# AIWarnings route authoring and authority

## Purpose

This is a responsibility-specific static acceptance plan for **es-aiwarning-authoring**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `governance`
- Acceptance id: `aiwarning-route-governance`
- Runtime boundary: Runtime is not needed to prove route catalog structure; enforcement remains an external claim.
- Static assertions: AIWarnings; P0; RuleIndex; route identity; archive

## Required specialized cases

- `route-identity`: replay the route-identity contract from source/configuration and record pass or blocked evidence.
- `p0-priority`: replay the p0-priority contract from source/configuration and record pass or blocked evidence.
- `rule-index-closure`: replay the rule-index-closure contract from source/configuration and record pass or blocked evidence.
- `duplicate-route`: replay the duplicate-route contract from source/configuration and record pass or blocked evidence.
- `archive-transition`: replay the archive-transition contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `references/aiwarning-contract.md`
- `scripts/Test-ESAIWarningRoute.ps1`
- `references/evidence-receipt-contract.md`
- `governance.json`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
