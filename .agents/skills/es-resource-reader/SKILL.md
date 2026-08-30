---
name: es-resource-reader
description: Read project resources through bounded, cached, format-aware projections for AI analysis. Use for JSON, CSV/TSV, HTML, XLSX, PDF, SQLite, TOML/INI, ZIP/TAR archives, media signatures, bytes, Unity YAML, and unitypackage inspection; never use it to import, move, delete, download, build, publish, or load runtime assets.
---

# ES Resource Reader

## Contract

This is a thin routing Skill. It resolves one registered parser, consumes only the immutable read snapshot, and emits a validated `ProjectionPacket`. It does not replace `es-task-read-snapshot`, `es-resource-collection`, or the runtime resource pipeline.

Unity Editor persistence is represented by `Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/Data/ESResourceReaderProjectionData.cs`; this object is an editor projection of a validated packet, not a runtime source of truth.

## Workflow controls

### Mandatory gates

1. Read project `AGENTS.md`, `.agents/README.md`, `ES/AISpace/README.md`, AIWarnings Start/CurrentStatus/RuleIndex, AIBRAIN entry, the matched Knowledge entry, and `references/route.manifest.json`.
2. Build/verify an immutable task snapshot with `es-task-read-snapshot`; never read a mutable source when a snapshot is supplied.
3. Resolve exactly one parser by format and validate the route manifest. Missing, ambiguous, stale, or out-of-scope routes fail closed.
4. Reuse only a cache entry bound to source SHA-256, parser version, and projection schema version. Otherwise parse within file/count/byte budgets.
5. Validate `ProjectionPacket` before returning it. Preserve warnings, errors, source hashes, and non-claims; do not silently truncate.

## Fast path

Batch probe and hash files, process independent files with bounded parallelism, emit deterministic ordering, and return summaries/samples. Full bytes, browser execution, network retrieval, Unity import, and AssetPackage writes require separate explicit authorization.

## References

- `references/route.manifest.json`: bounded parser routing and required reads.
- `references/projection-packet.v1.schema.json`: output contract.
- `references/resource-index.v1.schema.json`: compact index JSON contract.
- `references/resource-catalog.v1.schema.json`: multi-source catalog JSON contract.
- `references/resource-reference-index.v1.schema.json`: GUID reverse-reference index JSON contract.
- `references/json-reader.contract.md`, `table-reader.contract.md`, `html-reader.contract.md`, `bytes-reader.contract.md`: format rules.
- `references/structured-reader.contract.md`: SQLite, TOML/INI and ZIP/TAR safety and projection rules.
- `scripts/Invoke-ESResourceReader.ps1`: read-only fast probe and projection for JSON/JSONL, CSV/TSV, HTML, YAML/Unity YAML, Markdown, XML, logs, SQLite, TOML/INI, ZIP/TAR archives and archive probes; UnityPackage uses the single-process `Parse-ESUnityPackage.py` parser.
- `scripts/Probe-ESStructuredPackage.py`: bounded read-only SQLite schema, TOML/INI key and ZIP/TAR member projection without executing archive contents.
- `scripts/Test-ESStructuredPackage.py`: self-contained SQLite/TOML/INI/ZIP fixtures and traversal-denial negative test; temporary files are removed by the test harness.
- `scripts/Parse-ESDelimited.py`: RFC 4180-compatible streaming CSV/TSV parser with bounded row projection.
- `scripts/Parse-ESDelimitedBatch.py`: single-process batch CSV/TSV parser; consumes a UTF-8 JSON manifest and returns one bounded projection per file, reducing per-file interpreter startup overhead.
- `scripts/Test-ESDelimitedBatchJson.py`: validates the batch envelope/projection contract, parser identity, unique paths, bounded row samples, and summary counts before collection consumption.
- `scripts/Parse-ESJsonBatch.py` / `scripts/Test-ESJsonBatchJson.py`: single-process JSON/JSONL batch parsing and deep envelope validation with unique paths, bounded entries, and per-file failure isolation.
- `scripts/Parse-ESStructuredBatch.py` / `scripts/Test-ESStructuredBatchJson.py`: single-process SQLite/TOML/INI/ZIP/unitypackage/TAR batch probing with bounded member/table projections and failure isolation.
- `scripts/Parse-ESBinaryBatch.py` / `scripts/Test-ESBinaryBatchJson.py`: single-process media/PDF/XLSX/model/font signature and container probing with bounded projections.
- `scripts/Parse-ESMarkupBatch.py`: single-process bounded YAML/HTML/Markdown/XML/Unity-YAML batch parser for summaries and per-file failure isolation.
- `scripts/Test-ESMarkupBatchJson.py` and `references/es-markup-batch.v1.schema.json`: deep validation of markup batch envelope, parser identity, unique absolute paths, bounded entries, and failed-item errors before collection consumption.
- `scripts/Probe-ESOfficePdf.py`: no-dependency XLSX container and PDF page-object probe.
- `scripts/Probe-ESMedia.py`: bounded signature/dimension probe for common image, audio, video, model and font formats.
- `scripts/Measure-ESResourceReaderPerf.ps1`: deterministic local cold-read benchmark emitting grouped P50/P95 and throughput JSON.
- `scripts/Measure-ESProjectionCachePerf.ps1`: repeated cache-hit benchmark emitting P50/P95 hit latency JSON.
- `scripts/Measure-ESProjectionCacheBatchPerf.ps1`: multi-file cache hit-rate and latency benchmark emitting item-level JSON.
- `scripts/Measure-ESProjectionCacheParallelPerf.ps1`: bounded RunspacePool cache-hit benchmark for parallel scheduling.
- `scripts/Invoke-ESResourceReaderBatch.ps1`: bounded batch dispatch with deterministic result ordering.
- `scripts/Build-ESResourceReaderIndex.ps1`: derive a compact, hash-bound resource index from bounded batch projections for fast AI lookup.
- `scripts/Update-ESResourceReaderIndex.ps1`: incrementally refresh the index, reparsing only new or SHA-256-changed files and reusing unchanged entries.
- `scripts/Test-ESResourceReaderIndex.ps1`: deep validation for index schema, paths, hashes, counts and statuses.
- `scripts/Query-ESResourceReaderIndex.ps1`: in-memory path/format/status/hash-prefix query without rescanning or reparsing sources.
- `scripts/Build-ESResourceReaderCatalog.ps1`: merge multiple source/stable-output indexes into one source-qualified catalog with deterministic global keys.
- `scripts/Test-ESResourceReaderCatalog.ps1`: deep validation for catalog sources, global keys, paths, hashes and counts.
- `scripts/Analyze-ESResourceReaderCatalog.ps1`: group cross-source duplicate hashes and report format/status conflicts without reading source bytes.
- `scripts/Query-ESResourceReaderCatalog.ps1`: in-memory multi-source query by source ID, path, format, status or hash prefix.
- `scripts/Build-ESResourceReaderReferenceIndex.ps1`: derive a compact GUID reverse-reference index from cached Unity YAML projections without rescanning source bytes.
- `scripts/Invoke-ESResourceReaderUnityCacheBatch.ps1`: bounded RunspacePool batch projection/cache writer for Unity YAML assets; cache writes are hash-keyed and source files remain untouched.
- `scripts/Query-ESResourceReaderReferenceIndex.ps1`: in-memory GUID reverse-reference query.
- `scripts/Merge-ESResourceReaderReferenceCatalog.ps1`: deterministic cross-source merge of GUID reverse indexes, with hash/path conflict summaries and optional `-PreviousCatalogPath` incremental change counts.
- `scripts/Query-ESResourceReaderReferenceCatalog.ps1`: in-memory cross-source GUID query with `sourceId` filter.
- `scripts/Test-ESResourceReaderReferenceCatalog.ps1`: deep validation for source-qualified reference catalog ordering, counts and hashes.
- `scripts/Compare-ESResourceReaderReferenceCatalog.ps1`: bounded, hash-only diff of two reference Catalogs; emits deterministic added/removed/changed JSON without rescanning sources.
- `scripts/Test-ESResourceReaderReferenceCatalogDiff.ps1`: validates the reference Catalog diff contract and truncation bounds.
- `scripts/Build-ESResourceReaderReferenceCatalogShards.ps1`: builds deterministic GUID-prefix shards and a hash-bound manifest for large Catalogs.
- `scripts/Query-ESResourceReaderReferenceCatalogShards.ps1`: reads only the shard selected by a GUID prefix, with bounded results.
- `scripts/Test-ESResourceReaderReferenceCatalogShards.ps1`: validates shard coverage, hashes and manifest paths.
- `scripts/Measure-ESResourceReaderReferenceShardPerf.ps1`: same-process cold-read versus in-memory shard-query P50/P95 benchmark.
- `Assets/.../ESResourceReaderCatalogDiffData.cs` and `ESResourceReaderCatalogDiffImporter.cs`: Odin/ESSO-persistent diff projection and bounded Editor import action.
- `Assets/.../ESResourceReaderReferenceShardManifestData.cs` and `ESResourceReaderReferenceShardManifestImporter.cs`: Odin/ESSO-persistent GUID shard manifest and bounded Editor import action.
- `scripts/Test-ESResourceReaderReferenceIndex.ps1`: deterministic ordering, count, hash and reference-index contract validation.
- `scripts/Invoke-ESResourceReaderCached.ps1`: bind a projection to the shared hash/parser-version cache.
- `scripts/Test-ESReaderRouteManifest.ps1`: route depth, duplicate and hash-boundary validation.
- `scripts/Test-ESResourceReaderProjectionData.ps1`: static contract check for the Odin/ESSO persistent editor projection.
- `scripts/Test-ESResourceReaderCatalogRegistry.ps1`: static contract check for the Odin/ESSO persistent multi-source Catalog registry and AssetPackage action.
- `scripts/Test-ESUnityYamlGraph.ps1`: temporary deterministic Unity YAML fixture validating stable object nodes and dependency GUID edges.
- `Assets/.../ESResourceReaderProjectionImporter.cs`: Unity Editor menu action that imports a validated Projection JSON into a persistent Odin/ESSO object without mutating source assets.

## Verification

Run `scripts/Test-ESReaderRouteManifest.ps1`, then `scripts/Test-ESSkillEvidence.ps1` and the Skill Creator/Validator contracts. Static evidence never proves Unity, Runtime, network, Player, or Release behavior.

## Skill 使用披露

首次使用说明本 Skill 负责受限资源读取与 AI 投影；最终答复列出实际使用的 Skill。披露不等于授权或验收。
