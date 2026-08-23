---
name: es-task-read-snapshot
description: Govern task-scoped file reads with a deterministic read manifest, SHA-256 cache keys, duplicate-read reuse, and drift invalidation. Use when an AI task reads the same project files through multiple Skills, needs reproducible analysis, or must prove that conclusions came from one consistent file state.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Task Read Snapshot

Use one immutable read manifest per task. It is a consistency and reuse boundary, not an authorization grant and not a replacement for the project source of truth. This Skill is intentionally strict: it must fail closed instead of guessing, partially accepting a ReadSet, or silently retrying around file errors.

## Workflow

1. Define a project-relative `TaskId`, explicit input paths, and a parser/schema version.
2. Run `scripts/Invoke-ESTaskReadSnapshot.ps1 -Mode Build` before analysis. The script records normalized path, byte length, last-write time, SHA-256, parser version, and a deterministic cache key.
3. Reuse a file only when its path, SHA-256, parser version, and task snapshot match. A cache hit means the AI must not reread or reinterpret the same bytes through another Skill.
4. Run `-Mode Verify` before producing a final conclusion. Any source drift, missing file, path escape, or parser-version change invalidates the snapshot and requires a fresh plan/read.
5. Bind the snapshot hash into the AIBrain `PlanHash` or task receipt. Never use a snapshot to authorize writes, commands, Git, Unity, network, or release actions.

## Strict use limits

- Declare an explicit ReadSet; never pass a directory, wildcard, repository root, or “all files” request.
- Keep the default limits (`MaxFiles=256`, `MaxTotalBytes=512 MiB`, `MaxFileBytes=100 MiB`) unless a Deep Path plan explicitly raises them with a budget and owner.
- Duplicate normalized paths are always rejected. Duplicate content hashes are rejected by default because two paths must not be analyzed twice; use `-AllowDuplicateContent` only when both paths are independently authoritative and record the reason.
- Any missing, unreadable, changing, malformed, over-limit, reparse-point, or encoding-invalid input aborts the whole snapshot. No partial manifest is valid.
- Do not use cached projections when the source hash, parser version, projection hash, or registry entry is missing or stale.

## Write and cleanup boundary

- Build/Verify may write only the task manifest under `ES/Output/TaskReadSnapshots/`.
- Projection cache Write/Invalidate may create, replace, or delete only the computed manifest, projection artifact, and temporary files under `ES/Output/FileProjectionCache/`; no source file, Git index, Assets content, or external path may be moved or deleted.
- Cleanup is limited to stale/corrupt cache artifacts selected by the current hash key. Recovery is to rerun the authorized Write or Build operation; partial temporary files are safe to remove and never treated as a valid cache hit.

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `task-snapshot-consistency`
- Required cases: `snapshot-identity, source-hash, cache-hit, cache-invalidation, interrupted-recovery`
- Static assertions: snapshot; source hash; cache hit; stale; recovery
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `session`
- Custom checks: `consistency-cache, change-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Accept only project-relative paths; reject `..`, rooted paths, wildcards, and reparse-point escapes.
- Keep the manifest task-scoped. Do not reuse execution results or permissions across tasks.
- Fail closed on drift; do not silently refresh during analysis.
- Keep cache accounting explicit: `cacheHitCount`, `cacheMissCount`, `invalidatedCount`, and `readCount`.
- A snapshot proves byte consistency only. It does not prove Unity compilation, runtime behavior, or release acceptance.

## Resource

- [`scripts/Invoke-ESTaskReadSnapshot.ps1`](scripts/Invoke-ESTaskReadSnapshot.ps1): build or verify a task read manifest.
- [`scripts/Invoke-ESProjectionCache.ps1`](scripts/Invoke-ESProjectionCache.ps1): reuse trusted parser output for large/binary files by source hash and parser version; a cache miss must invoke the authorized parser, then write its JSON projection.
- [`scripts/Test-ESProjectionPacket.ps1`](scripts/Test-ESProjectionPacket.ps1): validate the parser output envelope before it enters the cache.
- [`scripts/Test-ESProjectionRegistry.ps1`](scripts/Test-ESProjectionRegistry.ps1): validate the discoverable parser registry.
- [`scripts/Invoke-ESProjectionPipeline.ps1`](scripts/Invoke-ESProjectionPipeline.ps1): resolve one registered parser and commit only a matching validated ProjectionPacket into the cache.
- [`references/task-read-snapshot-contract.md`](references/task-read-snapshot-contract.md): manifest fields and invalidation rules.


## Specialized static acceptance

Acceptance ID: `task-snapshot-consistency`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- snapshot
- source hash
- cache hit
- stale
- recovery

Required specialized cases: `snapshot-identity, source-hash, cache-hit, cache-invalidation, interrupted-recovery`
Guidance: `references/static-specialized-acceptance.md`
