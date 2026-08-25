# Knowledge validation contract

## Scope

The validator reads `Documentation/AIKnowledge/KnowledgeIndex.yaml`, selected Markdown entries, their project-local SourceRefs and referenced Skill metadata. It does not mutate knowledge or source files.

## Entry fields

A detailed entry must declare exactly one `KnowledgeId`, `Authority`, `RouteKeys`, `ContentHash`, and `EvidenceLevel`, plus `StaleWhen` metadata or a `StaleWhen` section and a `SourceRefs` section containing one or more project-relative files with lowercase SHA-256 values. A summary and a detailed `StaleWhen` section may coexist. Legacy entries omit `HashSchema`; v2 entries declare exactly one `HashSchema: v2`, `SourceSetHash`, and `EntryBodyHash`.

Canonical entries must have exactly one same-file index binding with the same `KnowledgeId`. Entry and index `routeKeys` are compared as ordinal, order-insensitive sets and must be exactly equal. A difference returns `ROUTE_SET_MISMATCH` with separately sorted `missingFromEntry` and `missingFromIndex` values.

An entry may declare `EntryMode: SharedRouteProjection` only when `KnowledgeIndex.yaml` explicitly enables `qualityGate.deduplication.sharedRouteProjectionAllowed`. It must contain a `## RouteProjections` section with exactly one line per same-file binding in the form ``- `knowledge.id`: `route-a`, `route-b` ``. Each binding's route set must exactly equal its declared projection, the projection identities must exactly equal the same-file binding identities, and the union of projected routes must exactly equal the entry `RouteKeys`. It owns routing decisions only and must not duplicate canonical domain facts.

For legacy entries, `ContentHash` remains the lowercase SHA-256 of the UTF-8 bytes formed by concatenating the ordinal-sorted declared SourceRef hashes. It proves the declared source set, not body integrity.

For v2 entries, `SourceSetHash` performs that source-set role and compatibility `ContentHash` must equal it. `EntryBodyHash` is SHA-256 over the normalized entry body after strict UTF-8 decoding: convert CRLF and CR to LF, remove every `EntryBodyHash` metadata line, remove trailing spaces and tabs from each remaining line, remove trailing empty lines, append exactly one LF, preserve all other content and ordering, then hash the UTF-8 bytes without BOM. The index binding must declare and match `hashSchema: v2`, `sourceSetHash`, and `entryBodyHash`. No semantic reordering, heading sorting, or Unicode normalization is performed.

The index binding must declare `knowledgeId`, `file`, `topic`, `routeKeys`, `relatedSkills`, `requiredReads`, `authority`, `evidenceLevel`, `contentHash`, and `staleWhen` exactly once. A v2 binding additionally declares `hashSchema`, `sourceSetHash`, and `entryBodyHash` exactly once. Partial v2 metadata is invalid.

## Path rules

- Entry files must remain below `Documentation/AIKnowledge`.
- SourceRefs and requiredReads must remain below the project root and identify existing files.
- Related Skills must be lowercase direct children below `.agents/skills` and contain `SKILL.md` plus `agents/openai.yaml`.
- Rooted paths, `..` traversal, reparse points, directories where files are required, and report paths outside `ES/Output` are blocked.

## Finding families

- `ENTRY_*`: entry structure or field failure.
- `SOURCE_*`: SourceRef path, duplication, existence, or hash failure.
- `CONTENT_HASH_*`: recomputation or index binding mismatch.
- `SOURCE_SET_HASH_*`: v2 source-set recomputation or index projection mismatch.
- `ENTRY_BODY_HASH_*`: normalized body recomputation or index projection mismatch.
- `INDEX_*`: index structure, duplicate identity, or missing binding.
- `ROUTE_*`: exact route-set or explicit projection failure.
- `REQUIRED_READ_*`: unresolved required read.
- `RELATED_SKILL_*`: unresolved or incomplete Skill binding.
- `PATH_*`: path expansion or containment failure.
- `UTF8_*`: strict UTF-8 decode failure.

Any finding blocks the selected static validation scope. Runtime remains `runtime-not-run` and is not converted into a static source failure.

## Determinism and recovery

Order entries by KnowledgeId and findings by code, path, then message. Repeated validation over unchanged files must return the same `inputHash`, result fields, and finding sequence; timestamps are intentionally excluded from the result. If any input changes, the old result is stale and must be replaced by a new run.
