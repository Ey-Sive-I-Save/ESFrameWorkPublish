# Task Read Snapshot Contract

The manifest is JSON under `ES/Output/TaskReadSnapshots/<TaskId>.json` and contains `schemaVersion`, `taskId`, `projectRoot`, `parserVersion`, `entries`, `snapshotHash`, `createdUtc`, `verifiedUtc`, and counters.

Each entry contains only project-relative `path`, `length`, `lastWriteUtc`, `sha256`, and `cacheKey` (`path|sha256|parserVersion`). Source content is never copied into the manifest. The manifest also records `totalBytes`, `duplicateContentCount`, and cache counters.

`Build` may reuse an existing entry only when all cache-key inputs still match. `Verify` recomputes every hash and fails if a file is missing, changed, or no longer resolves inside the project root. Any failure makes the prior snapshot stale; callers must rebuild and re-plan.

The default ReadSet budget is 256 files, 512 MiB total, and 100 MiB per file. Duplicate normalized paths and duplicate content hashes fail closed unless the caller explicitly records `-AllowDuplicateContent`. Errors occur before manifest replacement, so a failed run cannot publish a partial snapshot.

Parser output must use ProjectionPacket schema 1: `sourcePath`, `sourceHash`, `parserId`, `parserVersion`, `projectionKind`, `generatedUtc`, and an array `records`. Validate it with `Test-ESProjectionPacket.ps1` before storing it.
