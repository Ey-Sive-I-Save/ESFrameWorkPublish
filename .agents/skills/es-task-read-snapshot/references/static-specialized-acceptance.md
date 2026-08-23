# Task snapshot, projection cache and stale-state control

## Purpose

This is a responsibility-specific static acceptance plan for **es-task-read-snapshot**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `session`
- Acceptance id: `task-snapshot-consistency`
- Runtime boundary: Static checks prove file projection consistency, not external process timing.
- Static assertions: snapshot; source hash; cache hit; stale; recovery

## Required specialized cases

- `snapshot-identity`: replay the snapshot-identity contract from source/configuration and record pass or blocked evidence.
- `source-hash`: replay the source-hash contract from source/configuration and record pass or blocked evidence.
- `cache-hit`: replay the cache-hit contract from source/configuration and record pass or blocked evidence.
- `cache-invalidation`: replay the cache-invalidation contract from source/configuration and record pass or blocked evidence.
- `interrupted-recovery`: replay the interrupted-recovery contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `references/task-read-snapshot-contract.md`
- `scripts/Invoke-ESTaskReadSnapshot.ps1`
- `scripts/Invoke-ESProjectionCache.ps1`
- `references/evidence-receipt-contract.md`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
