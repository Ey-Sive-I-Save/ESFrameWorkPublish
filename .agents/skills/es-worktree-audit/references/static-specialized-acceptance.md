# Worktree change boundary and impact audit

## Purpose

This is a responsibility-specific static acceptance plan for **es-worktree-audit**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `engineering`
- Acceptance id: `worktree-boundary`
- Runtime boundary: No Runtime is needed to prove Git/worktree boundaries.
- Static assertions: worktree; scope; untracked; generated output; reversible

## Required specialized cases

- `tracked-untracked`: replay the tracked-untracked contract from source/configuration and record pass or blocked evidence.
- `scope-expansion`: replay the scope-expansion contract from source/configuration and record pass or blocked evidence.
- `generated-output`: replay the generated-output contract from source/configuration and record pass or blocked evidence.
- `encoding-drift`: replay the encoding-drift contract from source/configuration and record pass or blocked evidence.
- `reversible-recovery`: replay the reversible-recovery contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `scripts/Get-ESWorktreeImpact.ps1`
- `references/evidence-receipt-contract.md`
- `governance.json`
- `static-replay.manifest.json`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
