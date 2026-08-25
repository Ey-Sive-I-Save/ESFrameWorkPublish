# Knowledge entry contract

A detailed Markdown entry must contain:

- `KnowledgeId`
- `Authority`
- `RouteKeys`
- `ContentHash`
- `SourceRefs` with existing paths and lowercase SHA-256 values
- `EvidenceLevel` (`S0`–`S6`)
- `StaleWhen`

New or migrated detailed entries use `HashSchema: v2` and additionally contain `SourceSetHash` and `EntryBodyHash`. During the bounded compatibility period, an entry without `HashSchema` is legacy.

For legacy entries, `ContentHash` is SHA-256 of the UTF-8 bytes of the concatenated, ordinal-sorted SourceRef hashes. It is not a body-integrity proof. For v2, `SourceSetHash` owns that source-set calculation and compatibility `ContentHash` must equal it. `EntryBodyHash` uses the normalization algorithm in the Knowledge Validator contract: strict UTF-8, LF line endings, trailing spaces/tabs removed, its own metadata line excluded, trailing empty lines removed, exactly one final LF, and no semantic reordering or Unicode normalization. `KnowledgeIndex.yaml` must store and match all v2 hash fields. `relatedSkills` and `requiredReads` must point to existing project paths.

Canonical Entry and Index `routeKeys` must be order-insensitive exact sets. `SharedRouteProjection` is the only multi-binding exception and must declare one exact `RouteProjections` mapping per binding; overlap alone is insufficient.

The entry must distinguish verified source facts, derived routing, assumptions, and non-claims. SourceRefs must be rehashed whenever source files change.

For a `detailed-entry`, the entry must additionally include executable failure-prevention content. Each material failure mode identifies erroneous behavior, trigger/symptom, root cause, prevention check, correct action, recovery action, present evidence, missing evidence, and source ownership.

Version-sensitive external facts require a project-local provenance snapshot before they can become long-lived verified facts. The snapshot records the official URL/source identity, product/version, retrieval context, relevant contract, content hash, and stale conditions. A live URL or successful HTTP request alone is not a valid project `SourceRef` and does not prove Runtime behavior.
