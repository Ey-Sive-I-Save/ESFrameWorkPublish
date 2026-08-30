# AssetPackage aggregation mapping

Collection snapshots project into existing `ESAssetPackageBakeData` resolution records:

| Collection | AssetPackage |
|---|---|
| `groupId` | package/bake identity context |
| item `GUID + LocalFileId` | `ESAssetPackageResolutionItem.sourceGuid` plus dependency identity |
| source path/hash | `sourcePath`, `sourceFileHash`, `sourceDependencyHash` |
| target path/hash | `targetPath`, `expectedTargetGuid`, `expectedTargetFileHash` |
| classification | `category` |
| root/dependency role | `rootSelected`, `dependency` |
| migration reason | `operation`, `reasonCode` |

AssetPackage owns preview, export-link resolution, staging and commit/rollback. Collection owns provenance, intake and deduplication decisions. Neither layer may substitute for ResourcePlan bake or runtime Manifest/Provider contracts.
