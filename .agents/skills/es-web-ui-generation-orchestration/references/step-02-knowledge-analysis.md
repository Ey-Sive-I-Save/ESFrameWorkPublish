# Step 02 — Knowledge analysis

## AI analysis

Read `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`, then select only matching `KnowledgeIndex.yaml` entries. Convert each source into a design decision; record stale or mismatched hashes instead of treating text as truth.

## Execution

Read each entry's `requiredReads` and SourceRefs using UTF-8. Reuse the task read snapshot when available; never recursively load the whole knowledge base.

## Return

Return one receipt per entry with `knowledgeId`, `sourceRefs`, `contentHash`, `decision`, and `freshness`. No match returns `NoKnowledgeRoute` with recovery reads; hash drift returns `stale.knowledge.content-hash-mismatch`.

Required reads: AIWarnings Start/CurrentStatus/RuleIndex and each selected entry's `requiredReads` and SourceRefs.
