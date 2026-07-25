# AssetRef + GameCore Hot-Update Test Scene

Scene: `ESAssetGameCoreFlowHotUpdateTest.unity`

## Runtime order

1. Download and initialize the Consumer that contains `ESAssetGameCoreFlowTestDataInfo`.
2. Load this scene through its registered `ESAssetReferScene`.
3. Use the runtime panel in order: Resolve, Config, GameCore, Load, Ready O(1), Release.

The scene resolves the test SO from `ESFlowTestGameCore.Table`; it does not serialize a direct SO reference. This verifies the real Consumer GameCore injection boundary.

## Hot-update code

The controller belongs to the existing `ES_Samples.Runtime` asmdef. In the Res panel, enable code hot update for the test Consumer and select that asmdef. ES then packages its DLL through the existing HybridCLR integration. No new asmdef is created.

Change `ESAssetFlowTestHotUpdateProbe.ScriptRevision`, rebuild and publish the Consumer, then verify the runtime panel/log reports the new revision.
