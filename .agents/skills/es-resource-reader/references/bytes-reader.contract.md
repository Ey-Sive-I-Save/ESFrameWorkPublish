# Bytes reader

Read size, SHA-256, magic bytes, MIME guess, and bounded archive/package entry metadata. UnityPackage uses a single-process tar/gzip parser to build a deterministic full `groupId -> pathname -> asset.meta` association index under entry and association budgets. Never claim semantic contents from a probe alone; Unity YAML and unitypackage require their registered specialized parser.

Unity YAML projections additionally emit deterministic object nodes (`stableId=classId:fileId`) and deduplicated `dependencyGuids`; `summary.dependencyEdgeCount` is bounded projection metadata, not semantic completeness. The reader never imports or instantiates Unity objects.
