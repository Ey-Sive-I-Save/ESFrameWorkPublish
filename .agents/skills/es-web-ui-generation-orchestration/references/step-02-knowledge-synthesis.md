# Step 02 — Knowledge synthesis

## AI analysis

AI must read every `knowledgeRefs` entry selected by the preflight, hash the bytes, and translate principles into design decisions. The return receipt records each path, character count and SHA-256; existence alone is not acceptance.

## Execution

Use the task read snapshot and explicit UTF-8 decoding; re-read authoritative sources when a ContentHash is stale.

## Return

Return `knowledge-synthesis` with `decisions[]`, `sourceRefs[]`, `contentHashes[]`, and `freshness`; unresolved evidence is `stale.knowledge`.
