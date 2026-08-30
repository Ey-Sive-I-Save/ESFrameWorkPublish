# Collection contract

`ESResourceGroupState` is the authoritative collection snapshot for one resource group. Its JSON file is `<groupName>.json`; `groupId` is immutable.

Required identity fields:

- group: `schemaVersion`, `groupId`, `groupName`, `lifecycleState`, `authorityStage`
- source: `sourceId`, `sourceKind`, `sourceReference`, `provenance`, `license`, `observedUtc`
- item: `itemId`, `guid`, `localFileId`, `contentSha256`, `dependencySha256`, `assetType`, `relativePath`
- decision: `classification`, `deliveryIntent`, `targetPath`, `migrationAction`
- evidence: `verification.status`, `verification.checks[]`, `rollback.transactionId`

Reject empty IDs, non-canonical paths, missing hashes for claimed verification, duplicate physical identities, dependency cycles, and transitions that skip staging. Network references are metadata until a separately authorized download occurs.

Machine validation is defined by `es-resource-group-state.v1.schema.json` and enforced in Deep mode by `scripts/Test-ESResourceGroupJson.ps1`; the validator must pass before AssetPackage projection.
