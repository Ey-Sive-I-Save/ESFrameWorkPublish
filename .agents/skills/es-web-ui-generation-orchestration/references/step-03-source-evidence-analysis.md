# Step 03 — Open-source source evidence

## AI analysis

Compare the six pinned framework mechanisms against the requested experience. Keep only bounded source snippets, license evidence, commit identity, and SHA-256; do not infer behavior from package names.

## Execution

Consume `open-source-source-manifest.json` and verify each external snapshot by hash. Source snapshots live outside the project and are provenance inputs, not vendored runtime code.

## Return

Return six `source-evidence` records (`repository`, `tag`, `commit`, `sourcePath`, `sourceSha256`, `licenseSha256`, `mechanism`). Missing/changed evidence returns `blocked.source-evidence.*`; mutable `sourceAbsolutePath` is never substituted.

