# Knowledge entry contract

A detailed Markdown entry must contain:

- `KnowledgeId`
- `Authority`
- `RouteKeys`
- `ContentHash`
- `SourceRefs` with existing paths and lowercase SHA-256 values
- `EvidenceLevel` (`S0`–`S6`)
- `StaleWhen`

`ContentHash` is SHA-256 of the UTF-8 bytes of the concatenated, ordinal-sorted SourceRef hashes. It is not a self-hash of the Markdown body. `relatedSkills` and `requiredReads` in KnowledgeIndex must point to existing project paths.

The entry must distinguish verified source facts, derived routing, assumptions, and non-claims. SourceRefs must be rehashed whenever source files change.
