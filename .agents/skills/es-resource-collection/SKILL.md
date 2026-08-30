---
name: es-resource-collection
description: Collect, classify, deduplicate, verify, and stage ESFramework project resources for later aggregation by the AssetPackage window. Use for local or network source intake, resource-group JSON state, AISpace quarantine, unitypackage intake, migration planning, and reversible AssetPackage preparation; never use it as a runtime loader or release publisher.
---

# ES Resource Collection

## Site resource workflow (authoritative)

Use this section whenever the source is a public resource website or the user asks to search, download, preview, organize, register, or remove a site.

### Canonical folder contract

Under `ES/AISpace/Public/可用资源站点/`, each domain has exactly one first-level folder named:

```text
【✓】【类型[,类型]】<site-core-name>
【✗】【类型[,类型]】<site-core-name>
```

The status prefix is evidence-driven: `【✓】` requires at least 10 real visible resources and a successful cleanup check; otherwise use `【✗】`. One domain must never be split into separate 2D/3D/audio site folders: merge its resources and list supported semantic types in the short bracket. Inside a site folder, use one level of semantic type folders only (`纹理`, `HDRI`, `模型`, `动画`, `音效`, `字体`, `UI`, `工具`). Do not introduce meaningless wrappers such as `resources`, `assets`, `downloads`, `package`, or `sample`; a migration may flatten such a wrapper only after checking name collisions and recording before/after paths. Keep `site.md`, `provenance.json`, and a first-level `README.md` at the site root. The README must be short, filename-oriented, and state the current status; it is an index, not a license substitute.

When site exploration discovers a reusable search trick, API endpoint pattern, pagination rule, download limitation, or naming heuristic, write it into that site's `site.md` under a dated `探索技巧` entry and preserve the source URL/evidence. Do not put site-specific tricks in the global Skill or silently apply them to another domain.

### Search and download policy

For an ordinary request, search the registered source list and choose the smallest sufficient set; do not widen to a broad crawl. When the user specifies a name, format, quality, resolution, license, URP, or quantity, expand search only within those constraints and record the query terms and rejected matches. Candidate identity is based on URL/source ID plus content hash; fuzzy name matching is for discovery and deduplication suggestions only and must never grant identity, license, or overwrite authority.

Before download, require a public direct URL (or an explicitly authorized authenticated channel), license evidence, format/size evidence, and a per-site byte budget. Download sequentially by site by default; at most three child agents may run concurrently, and all agents must target the same current site. Stage in `ES/AISpace/Local`, verify size and SHA-256, extract to the semantic type folder, then delete archives and staging. A site is not `【可用】` until `archiveCount=0`, visible file count is at least 10, and `provenance.json` records the observed counts and bytes. Authentication, CAPTCHA, dynamic URLs, or missing license evidence produce `【不可用】`/`unverified`; never fabricate assets.

### Preview, collection, comparison, and lifecycle

Preview is a separate, opt-in action: open only an existing local image/model/audio preview or a safe public page, record `previewAttempted`, `previewPath`/URL, and result; preview does not imply download or Unity import. Collection is incremental and idempotent: compare physical identity, SHA-256, then semantic type; record `identical`, `variant`, `conflict`, or `needs-review`, preserving the original filename and source. Site registration writes one canonical entry with domain, categories, searchability, license evidence, priority, budget, and status. Removal is a tombstone (`removed`, reason, timestamp, prior revision) rather than silent deletion; re-registration creates a new revision and never revives stale files automatically.

Use `scripts/Normalize-ESResourceSiteFolders.ps1` for a dry-run folder projection; pass `-Apply` only for an explicitly authorized migration. Use `scripts/Update-ESResourceSiteRegistry.ps1` with `-Action Register` or `-Action Remove` to maintain the canonical `SITES.json` registry; removal is a tombstone and never deletes files. Use `scripts/Preview-ESResourceSite.ps1` to list a local preview candidate; pass `-Open` only when the user explicitly requests opening it. Preview/open operations remain opt-in and host-dependent; a Skill must report `preview-unavailable` rather than claiming a window opened when no host consumer exists.

### Required receipt fields

Every site run emits a bounded receipt containing `siteId`, `status`, `query`, `searchScope`, `candidateCount`, `downloadedCount`, `visibleFileCount`, `downloadedBytes`, `visibleBytes`, `archiveCount`, `hashVerifiedCount`, `licenseEvidence`, `previewAttempted`, `childAgentCount`, `cleanup`, `unverified`, and `nonClaims`. Missing optional metadata is normalized and marked; missing core evidence keeps the site `unverified` or `blocked` without preventing unrelated sites from running.

### Memory and output budget

All collection and download workers use bounded queues and incremental processing. A
site run defaults to a 10 MiB total-byte budget, a maximum of 100 candidate files per
semantic type, and at most three workers. Workers must release response bodies,
temporary streams, and extracted archive handles after each item; they must not retain
full HTML, binary payloads, or unbounded logs in the command shell. Receipts record
`maxTotalBytes`, `maxFilesPerType`, `peakQueueItems`, and `releasedAfterItem`. When a
budget or queue cap is reached, stop that site cleanly and mark remaining candidates
`deferred-by-budget`; do not silently increase the cap or spill into another site.

## Overview

This Skill governs resource intake before AssetPackage aggregation. It creates a complete, replayable resource-group state, preserves provenance and hashes, and emits bounded staging plans without silently importing, moving, deleting, downloading, building, or publishing.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露规范。首次使用时说明本 Skill 负责资源收集与 AssetPackage 聚合前置；最终答复列出实际使用的 Skill。披露不等于授权、执行或验收证据。

## Authority and boundaries

1. Read `AGENTS.md`, `ES/AISpace/README.md`, AIWarnings Start/CurrentStatus/RuleIndex, and the resource-pipeline Knowledge entry before project work.
2. Treat `ESAssetLibrary`/`ESAssetBook` as Editor authoring structures; use `GUID + LocalFileId` as physical identity.
3. Treat `ESAssetPackageBakeData` as the editor aggregation workspace. Do not create a second runtime loader, Provider, Manifest, BundleIndex, or release protocol.
4. Runtime consumes baked Manifest/Table/BundleIndex only. Never scan AssetDatabase, Library, project folders, or collection JSON at runtime.
5. Network, Unity, import, move, replace, delete, build, publish, and release actions require explicit current-user authorization; planning and inspection remain safe defaults.

## Workflow controls

- Keep collection, AssetPackage aggregation, ResourcePlan baking, and release publishing as separate stages.
- Require explicit scope, source hashes, stable ordering, and a transaction ID before staging.
- On ambiguity or conflict, stop at `NeedsReview`/`Quarantined`; do not infer permission or silently switch targets.
- Preserve prior snapshots on retry and report `runtime-not-run` for unexecuted Unity or external operations.

## Non-skippable gates (hard contract)

The following gates are mandatory and ordered. A missing read, invalid field, hash mismatch, identity collision, dependency-cycle, or unauthorized expansion fails closed; later gates must not be substituted or silently skipped:

1. Resolve the project root, read `AGENTS.md`, `ES/AISpace/README.md`, AIWarnings Start/CurrentStatus/RuleIndex, `AIBRAIN_ENTRY.md`, the routed Knowledge entry, and this Skill's referenced contracts.
2. Freeze the source descriptor and provenance (owner, license, original reference, observed UTC, size, SHA-256) before classification; use only immutable per-launch snapshots when a handoff supplies files.
3. Validate the group JSON with `references/es-resource-group-state.v1.schema.json` and `scripts/Test-ESResourceGroupJson.ps1 -Mode Deep`. Invalid UTF-8, duplicate IDs, non-canonical paths, missing hashes, unresolved dependencies, cycles, and nondeterministic ordering are hard failures.
4. Apply scope, authority, and permission gates. Emit only a plan/snapshot unless the current user explicitly authorizes import, download, move, replace, delete, build, publish, or runtime work.
5. Project the accepted snapshot to AssetPackage resolution items, write an evidence receipt, and label Unity/Runtime/Release claims `runtime-not-run` unless separately evidenced.

The deep JSON validator is the acceptance gate for every group snapshot, not a best-effort lint. Revalidation is required after any source/configuration hash change.

## Workflow

### 1. Intake and provenance

- Normalize a source descriptor: `sourceId`, source kind (`local`, `network`, `unitypackage`, `aispace`), original reference, owner, license/provenance, size, SHA-256, and observed UTC.
- For network sources, record URL and retrieval metadata but do not download unless explicitly authorized.
- Reject path traversal, ambiguous identity, missing provenance, unsupported type, or hash mismatch into `Quarantine/<task>/` planning state.

### 2. Group state

Each group has one same-name top-level JSON and a corresponding C# state type. Minimum fields:

```text
schemaVersion, groupId, groupName, lifecycleState, authorityStage,
sourceKind, classification, deliveryIntent, targetRoot, items,
dependencies, duplicates, migration, verification, rollback
```

Item identity is `GUID + LocalFileId` when imported, otherwise a source fingerprint until identity is assigned. Display names and paths are never unique identity.

### 3. Classification and target planning

- Final Unity assets → existing `Assets` locations after import authorization.
- `unitypackage` → normally `Assets`, followed by Unity import and ES registration.
- Temporary, out-of-scope, or non-release resources → `ES/AISpace/Local` or `Public` using its category/date/owner rules.
- Uncertain ownership or licensing → AISpace quarantine; preserve source and reason.

Produce a `targetPath` and `migrationAction` (`keep`, `stage`, `import`, `move`, `quarantine`) without executing the action by default.

### 4. Deduplication and dependency closure

- First compare physical identity, then content SHA-256, then dependency hash and semantic type.
- Do not merge two assets solely because names or paths match.
- Record direct and transitive dependencies; detect cycles and missing nodes before emitting an AssetPackage input.
- Mark duplicate decisions as `identical`, `variant`, `conflict`, or `needs-review`.

### 5. AssetPackage handoff

Emit an immutable collection snapshot that AssetPackage can consume to populate resolution items and preview categories. Preserve source/target hashes, expected GUID, root/dependency flags, and transaction identity. AssetPackage remains responsible for editor preview, export-link resolution, staging, and rollback commit.

## State and recovery

Use explicit states such as `Discovered → Verified → Classified → Staged → ReadyForAggregation`, with `NeedsReview`, `Quarantined`, `Failed`, and `Canceled` branches. A retry with the same input fingerprint is idempotent; changed source/configuration creates a new attempt. Never overwrite a conflicting target or revive a failed transaction. Every staged operation carries `transactionId`, before/after hashes, created/moved/replaced sets, and rollback status.

## Validation

Static checks must cover: schema and UTF-8, stable identity, provenance, duplicate detection, dependency closure, denied path/permission expansion, deterministic ordering, repeat idempotency, interruption recovery, and AssetPackage boundary preservation. Static evidence does not prove Unity import, Runtime, Player, network, or Release behavior; label those `runtime-not-run` unless separately authorized and evidenced.

Read `references/collection-contract.md` for the JSON/C# contract, `references/es-resource-group-state.v1.schema.json` for the machine-readable schema, and `references/assetpackage-aggregation.md` for the integration mapping. Run `scripts/Test-ESResourceGroupJson.ps1 -Mode Deep` for each snapshot, then `scripts/Test-es-resource-collection-StaticReplay.ps1` for the bounded static replay.

For fast local intake, `scripts/Invoke-ESResourceCollectionBatch.ps1` performs bounded parallel reader dispatch, optional `-AutoParallel` tuning (file count plus total-byte budget), SHA-256 incremental reuse, deterministic path ordering, per-file failure isolation, and a resumable UTF-8 batch snapshot. Supplying `-SchedulePath` consumes the AssetPackage-exported `collection-schedule.json` (schema v1) for the file/parallel/size limits; explicit script parameters remain the fallback when no schedule is supplied. Supplying `-CancelFile` enables cooperative cancellation; an existing state file is preserved as the reuse source and unchanged files are not reparsed. The batch snapshot records `totalBytes` and `parallelReason` as performance evidence. The batch snapshot is staging evidence only and does not import or move Unity assets.
Validate that snapshot with `scripts/Test-ESResourceCollectionBatch.ps1`; it enforces `references/es-resource-collection-batch.v1.schema.json`, counts, hashes, canonical paths, statuses, and stable ordering before AssetPackage projection.
CSV/TSV files in a batch are routed through the Reader's single-process `Parse-ESDelimitedBatch.py` manifest path; unchanged files still bypass parsing through SHA-256 reuse. The batch snapshot records `delimitedBatchElapsedMilliseconds` for parser-stage attribution.
JSON files use the Reader's single-process `Parse-ESJsonBatch.py` path with the same count/path and failure-isolation guarantees.
Use `scripts/Measure-ESResourceCollectionBatchPerf.ps1` to record cold versus incremental throughput and a reproducible speedup ratio in `ES/Output/Benchmarks`; pass `-AutoParallel` to apply the same small/medium/large batch worker heuristic and capture `effectiveParallel` for both phases.
Validate that report with `scripts/Test-ESResourceCollectionBatchPerf.ps1` against `references/es-resource-collection-batch-perf.v1.schema.json`.
Compare a candidate against a pinned report with `scripts/Test-ESResourceCollectionBatchPerfRegression.ps1`; it fails when cold/incremental throughput or speedup falls below the declared ratio thresholds. Use `-RequirePackageMatch` for AssetPackage-bound reports.
Use `scripts/Merge-ESResourceCollectionPerfTrend.ps1` to create a stable, format-aware trend snapshot from benchmark reports.
Validate an AssetPackage-exported schedule independently with `scripts/Test-ESResourceCollectionSchedule.ps1` against `references/es-resource-collection-schedule.v1.schema.json`; batch execution fails closed when the schedule is malformed or outside its bounded ranges.
The editor action `asset-package.import-resource-collection-candidates` projects only validated files that resolve to real `Assets/...` GUIDs into the selected AssetPackage; external paths remain snapshot-only and are never assigned synthetic Unity identities.
Run `scripts/Test-ESAssetPackageResourceClosure.ps1` for a bounded static check that the existing AssetPackage menu action, selected-bake handoff, batch contract, SHA-256 verification, Assets boundary, and GUID resolution are all wired. This proves source-level closure only; Unity Editor execution remains `runtime-not-run` until separately authorized.
