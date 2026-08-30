# Step 08 — Quality closeout

## AI analysis

AI analyses acceptance criteria and compares validator observations with the requested objective; it must not inflate scores from file existence.

## Execution

Run intent audit, static pipeline, artifact-integrity, and UTF-8 validators against the same immutable artifact manifest.

## Return

The return is layered (`static-generated`, `static-validated`, `static-artifact-closed`, `release-not-run`) and lists unproven runtime claims. Any failed check keeps its reason code and recovery action.
