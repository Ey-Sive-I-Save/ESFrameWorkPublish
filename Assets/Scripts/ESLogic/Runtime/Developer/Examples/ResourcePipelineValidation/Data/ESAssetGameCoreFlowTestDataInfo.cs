using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Runtime/Developer/Examples/ResourcePipelineValidation/Data/ESAssetGameCoreFlowTestDataInfo.cs")]
    public enum ESFlowTestEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("商业流程验收")]
        CommercialFlowTest = 1
    }

    [Serializable]
    public sealed class ESFlowTestConfigKey : ESGameCoreConfigKey<ESFlowTestEnumKey> { }

    [Serializable]
    public sealed class ESFlowTestRuntimeData
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public ESAssetGameCoreFlowTestDataInfo source;
    }

    /// <summary>Dedicated table for the commercial AssetRefer + GameCoreConfigKey integration test root.</summary>
    public static class ESFlowTestGameCore
    {
        public static readonly ESConfigKeyTable<ESFlowTestRuntimeData> Table = new ESConfigKeyTable<ESFlowTestRuntimeData>(4, "GameCore.FlowTest");

        public static bool Register(ESAssetGameCoreFlowTestDataInfo source)
        {
            if (source == null || source.testKey == null || !source.testKey.IsConfigured)
                return false;

            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild(false);
            try
            {
                if (Table.TryGet(source.testKey, out ESFlowTestRuntimeData existing))
                    return ReferenceEquals(existing.source, source);

                var data = new ESFlowTestRuntimeData
                {
                    keyName = ESConfigKeyMatch.Describe(source.testKey.EnumKeyInt, source.testKey.StringKey),
                    source = source
                };
                data.runtimeKey = Table.RegisterAndGetRuntimeKey(source.testKey, data, debugName: source.name);
                return data.runtimeKey != 0;
            }
            finally
            {
                if (ownsBuild) Table.EndBuild();
            }
        }
    }

    [ESCreatePath("DataInfo", "Asset + GameCore Flow Test")]
    public sealed class ESAssetGameCoreFlowTestDataInfo : SoDataInfo, IGameCoreSO
    {
        [NonSerialized] private ESAssetScope testScope;
        [NonSerialized] private ESRuntimeSceneHandle testSceneHandle;
        [NonSerialized] private object testReferenceOwner;
        [NonSerialized] private CancellationTokenSource testSessionCancellation;
        [NonSerialized] private long testReferenceGeneration;

        [TitleGroup("Test Root")]
        [ESConfigKeyUsage(ESConfigKeyUsage.Declaration)]
        [HideLabel, InlineProperty]
        public ESFlowTestConfigKey testKey = new ESFlowTestConfigKey
        {
            enumKey = ESFlowTestEnumKey.CommercialFlowTest,
            stringKey = "commercial_flow_test"
        };

        [TitleGroup("GameCore Keys")]
        [LabelText("Buff"), InlineProperty] public ESBuffConfigKey buff = new ESBuffConfigKey();
        [LabelText("Skill"), InlineProperty] public ESSkillConfigKey skill = new ESSkillConfigKey();
        [LabelText("Monster"), InlineProperty] public ESMonsterConfigKey monster = new ESMonsterConfigKey();
        [LabelText("NPC"), InlineProperty] public ESNpcConfigKey npc = new ESNpcConfigKey();
        [LabelText("Weapon"), InlineProperty] public ESWeaponConfigKey weapon = new ESWeaponConfigKey();
        [LabelText("Shot"), InlineProperty] public ESShotConfigKey shot = new ESShotConfigKey();

        [TitleGroup("Asset References")]
        public ESAssetReferPrefab prefab = new ESAssetReferPrefab();
        public ESAssetReferSprite sprite = new ESAssetReferSprite();
        public ESAssetReferAudioClip audioClip = new ESAssetReferAudioClip();
        public ESAssetReferMaterial material = new ESAssetReferMaterial();
        public ESAssetReferTexture texture = new ESAssetReferTexture();
        public ESAssetReferTexture2D texture2D = new ESAssetReferTexture2D();
        public ESAssetReferAnimationClip animationClip = new ESAssetReferAnimationClip();
        public ESAssetReferAnimatorController animatorController = new ESAssetReferAnimatorController();
        public ESAssetReferSpriteAtlas spriteAtlas = new ESAssetReferSpriteAtlas();
        public ESAssetReferAvatar avatar = new ESAssetReferAvatar();
        public ESAssetReferVideoClip videoClip = new ESAssetReferVideoClip();
        public ESAssetReferTimelineAsset timelineAsset = new ESAssetReferTimelineAsset();
        public ESAssetReferPlayableAsset playableAsset = new ESAssetReferPlayableAsset();
        public ESAssetReferMesh mesh = new ESAssetReferMesh();
        public ESAssetReferTerrainData terrainData = new ESAssetReferTerrainData();
        public ESAssetReferRaw raw = new ESAssetReferRaw();
        public ESAssetReferScriptableObject scriptableObject = new ESAssetReferScriptableObject();
        public ESAssetReferUnityObject unityObject = new ESAssetReferUnityObject();
        public ESAssetReferScene scene = new ESAssetReferScene();

        [TitleGroup("Execution")]
        [LabelText("Load Scene (changes active scene)")]
        public bool loadScene;

        [TitleGroup("Execution")]
        [ReadOnly, MultiLineProperty(8)]
        public string lastReport;

        public void InjectGameCoreTables()
        {
            if (!ESFlowTestGameCore.Register(this))
                throw new InvalidOperationException("[ESGameCore][FlowTest] Test root registration failed: " + name);
        }

        [Button("1. Validate Configuration", ButtonSizes.Large)]
        public void DebugConfiguration()
        {
            var report = new StringBuilder(2048);
            report.AppendLine("[ESFlowTest][Config] AssetRefer + ConfigKey configuration report");
            AppendGameCoreConfiguration(report);
            AppendAssetConfiguration(report, "Prefab", prefab);
            AppendAssetConfiguration(report, "Sprite", sprite);
            AppendAssetConfiguration(report, "AudioClip", audioClip);
            AppendAssetConfiguration(report, "Material", material);
            AppendAssetConfiguration(report, "Texture", texture);
            AppendAssetConfiguration(report, "Texture2D", texture2D);
            AppendAssetConfiguration(report, "AnimationClip", animationClip);
            AppendAssetConfiguration(report, "AnimatorController", animatorController);
            AppendAssetConfiguration(report, "SpriteAtlas", spriteAtlas);
            AppendAssetConfiguration(report, "Avatar", avatar);
            AppendAssetConfiguration(report, "VideoClip", videoClip);
            AppendAssetConfiguration(report, "TimelineAsset", timelineAsset);
            AppendAssetConfiguration(report, "PlayableAsset", playableAsset);
            AppendAssetConfiguration(report, "Mesh", mesh);
            AppendAssetConfiguration(report, "TerrainData", terrainData);
            AppendAssetConfiguration(report, "Raw", raw);
            AppendAssetConfiguration(report, "ScriptableObject", scriptableObject);
            AppendAssetConfiguration(report, "UnityObject", unityObject);
            AppendAssetConfiguration(report, "Scene", scene);
            PublishReport(report);
        }

        [Button("2. Debug GameCore Queries", ButtonSizes.Large)]
        public void DebugGameCoreQueries()
        {
            var report = new StringBuilder(1024);
            report.AppendLine("[ESFlowTest][GameCore] Typed table query report");
            AppendGameCoreResult(report, "TestRoot", testKey, ESFlowTestGameCore.Table);
            AppendGameCoreResult(report, "Buff", buff, ESRuntimeDataGameCore.Buffs);
            AppendGameCoreResult(report, "Skill", skill, ESRuntimeDataGameCore.Skills);
            AppendGameCoreResult(report, "Monster", monster, ESRuntimeDataGameCore.Monsters);
            AppendGameCoreResult(report, "NPC", npc, ESRuntimeDataGameCore.Npcs);
            AppendGameCoreResult(report, "Weapon", weapon, ESRuntimeDataGameCore.Weapons);
            AppendGameCoreResult(report, "Shot", shot, ESRuntimeDataGameCore.Shots);
            PublishReport(report);
        }

        [Button("3. Load All Valid Assets", ButtonSizes.Large)]
        public void DebugLoadAllAssets()
        {
            RunAssetLoadTestAsync().Forget();
        }

        [Button("4. Debug Ready Hot Path", ButtonSizes.Large)]
        public void DebugReadyHotPath()
        {
            var report = new StringBuilder(1536);
            report.AppendLine("[ESFlowTest][Ready] O(1) ready-cache query report");
            AppendReady(report, "Prefab", prefab);
            AppendReady(report, "Sprite", sprite);
            AppendReady(report, "AudioClip", audioClip);
            AppendReady(report, "Material", material);
            AppendReady(report, "Texture", texture);
            AppendReady(report, "Texture2D", texture2D);
            AppendReady(report, "AnimationClip", animationClip);
            AppendReady(report, "AnimatorController", animatorController);
            AppendReady(report, "SpriteAtlas", spriteAtlas);
            AppendReady(report, "Avatar", avatar);
            AppendReady(report, "VideoClip", videoClip);
            report.AppendLine("[INFO] TimelineAsset: configuration is covered; use PlayableAsset for runtime generic loading in this assembly.");
            AppendReady(report, "PlayableAsset", playableAsset);
            AppendReady(report, "Mesh", mesh);
            AppendReady(report, "TerrainData", terrainData);
            AppendReady(report, "Raw", raw);
            AppendReady(report, "ScriptableObject", scriptableObject);
            AppendReady(report, "UnityObject", unityObject);
            PublishReport(report);
        }

        [Button("5. Release Test References")]
        public void ReleaseTestReferences()
        {
            ReleaseTestReferences(this);
        }

        public async UniTask<string> RunAssetLoadTestAsync(CancellationToken token = default)
        {
            return await RunAssetLoadTestAsync(this, token);
        }

        public bool ReleaseTestReferences(object owner)
        {
            if (owner == null || !ReferenceEquals(testReferenceOwner, owner))
                return false;

            testReferenceGeneration++;
            testReferenceOwner = null;
            ReleaseHeldTestReferences();
            Debug.Log("[ESFlowTest][Release] Dedicated test Scope and Scene Handle disposed; all test-held asset references were returned.", this);
            return true;
        }

        public async UniTask<string> RunAssetLoadTestAsync(object owner, CancellationToken token = default)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (testReferenceOwner != null && !ReferenceEquals(testReferenceOwner, owner))
                throw new InvalidOperationException(
                    "[ESFlowTest][Ownership] Test references are already owned by another controller. Release that test session before starting a new one.");

            long generation = ++testReferenceGeneration;
            ReleaseHeldTestReferences();
            testReferenceOwner = owner;
            testSessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            CancellationToken sessionToken = testSessionCancellation.Token;
            ESAssetScope runScope;
            try
            {
                runScope = ESAssets.CreateScope();
            }
            catch
            {
                ReleaseTestReferences(owner, generation);
                throw;
            }
            testScope = runScope;
            var report = new StringBuilder(3072);
            report.AppendLine("[ESFlowTest][Load] Full asset loading report");
            try
            {
                await LoadAndAppend(report, "Prefab", prefab, runScope, sessionToken);
                await LoadAndAppend(report, "Sprite", sprite, runScope, sessionToken);
                await LoadAndAppend(report, "AudioClip", audioClip, runScope, sessionToken);
                await LoadAndAppend(report, "Material", material, runScope, sessionToken);
                await LoadAndAppend(report, "Texture", texture, runScope, sessionToken);
                await LoadAndAppend(report, "Texture2D", texture2D, runScope, sessionToken);
                await LoadAndAppend(report, "AnimationClip", animationClip, runScope, sessionToken);
                await LoadAndAppend(report, "AnimatorController", animatorController, runScope, sessionToken);
                await LoadAndAppend(report, "SpriteAtlas", spriteAtlas, runScope, sessionToken);
                await LoadAndAppend(report, "Avatar", avatar, runScope, sessionToken);
                await LoadAndAppend(report, "VideoClip", videoClip, runScope, sessionToken);
                report.AppendLine("[INFO] TimelineAsset: skipped; PlayableAsset covers the runtime load contract without a hard Timeline assembly edge.");
                await LoadAndAppend(report, "PlayableAsset", playableAsset, runScope, sessionToken);
                await LoadAndAppend(report, "Mesh", mesh, runScope, sessionToken);
                await LoadAndAppend(report, "TerrainData", terrainData, runScope, sessionToken);
                await LoadAndAppend(report, "Raw", raw, runScope, sessionToken);
                await LoadAndAppend(report, "ScriptableObject", scriptableObject, runScope, sessionToken);
                await LoadAndAppend(report, "UnityObject", unityObject, runScope, sessionToken);

                if (scene != null && scene.IsValid)
                {
                    if (!loadScene) report.AppendLine("[SKIP] Scene: valid, loading disabled to avoid changing the active scene.");
                    else
                    {
                        try
                        {
                            ESRuntimeSceneHandle handle = await scene.LoadAsync(LoadSceneMode.Single, sessionToken);
                            if (!OwnsTestSession(owner, generation))
                            {
                                handle.Dispose();
                                throw new OperationCanceledException("AssetFlow test session ownership ended before the scene load completed.");
                            }
                            testSceneHandle = handle;
                            report.Append("[PASS] Scene: ").Append(handle.Scene.path).AppendLine();
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception exception) { AppendFailure(report, "Scene", exception); }
                    }
                }
                else report.AppendLine("[SKIP] Scene: not configured.");

                if (OwnsTestSession(owner, generation))
                    PublishReport(report);
                return report.ToString();
            }
            catch (OperationCanceledException)
            {
                ReleaseTestReferences(owner, generation);
                throw;
            }
            catch
            {
                ReleaseTestReferences(owner, generation);
                throw;
            }
        }

        private void ReleaseHeldTestReferences()
        {
            CancellationTokenSource sessionCancellation = testSessionCancellation;
            ESAssetScope scope = testScope;
            ESRuntimeSceneHandle sceneHandle = testSceneHandle;

            // Detach shared state before invoking cancellation callbacks. A callback may re-enter
            // this test asset or throw, but it must never release a newer session or prevent the
            // current Scope and Scene Handle from being returned.
            testSessionCancellation = null;
            testScope = null;
            testSceneHandle = default;

            try
            {
                sessionCancellation?.Cancel();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                try
                {
                    sessionCancellation?.Dispose();
                }
                finally
                {
                    try
                    {
                        scope?.Dispose();
                    }
                    finally
                    {
                        // ESRuntimeSceneHandle owns the scene lease; Dispose is the current resource-runtime release contract.
                        sceneHandle.Dispose();
                    }
                }
            }
        }

        private bool OwnsTestSession(object owner, long generation)
        {
            return ReferenceEquals(testReferenceOwner, owner) && testReferenceGeneration == generation;
        }

        private void ReleaseTestReferences(object owner, long generation)
        {
            if (!OwnsTestSession(owner, generation))
                return;
            testReferenceGeneration++;
            testReferenceOwner = null;
            ReleaseHeldTestReferences();
        }

        private void AppendGameCoreConfiguration(StringBuilder report)
        {
            AppendKeyConfiguration(report, "TestRoot", testKey);
            AppendKeyConfiguration(report, "Buff", buff);
            AppendKeyConfiguration(report, "Skill", skill);
            AppendKeyConfiguration(report, "Monster", monster);
            AppendKeyConfiguration(report, "NPC", npc);
            AppendKeyConfiguration(report, "Weapon", weapon);
            AppendKeyConfiguration(report, "Shot", shot);
        }

        private static void AppendKeyConfiguration<TEnum>(StringBuilder report, string label, ESGameCoreConfigKey<TEnum> key) where TEnum : struct, Enum
        {
            if (key == null) { report.Append("[FAIL] ").Append(label).AppendLine(": key object is null."); return; }
            bool valid = key.EnumKeyInt != 0 || !string.IsNullOrEmpty(key.StringKey);
            report.Append(valid ? "[PASS] " : "[SKIP] ").Append(label).Append(": enum=").Append(key.EnumKeyInt)
                .Append(", string=").Append(key.StringKey ?? string.Empty).AppendLine();
        }

        private static void AppendGameCoreResult<TEnum, TData>(StringBuilder report, string label, ESGameCoreConfigKey<TEnum> key, ESConfigKeyTable<TData> table)
            where TEnum : struct, Enum where TData : class
        {
            if (key == null || (key.EnumKeyInt == 0 && string.IsNullOrEmpty(key.StringKey)))
            { report.Append("[SKIP] ").Append(label).AppendLine(": no key configured."); return; }
            bool found = table.TryGet(key, out TData data);
            report.Append(found ? "[PASS] " : "[FAIL] ").Append(label).Append(": enum=").Append(key.EnumKeyInt)
                .Append(", string=").Append(key.StringKey ?? string.Empty).Append(", result=")
                .Append(data != null ? data.GetType().FullName : "null").AppendLine();
        }

        private static void AppendAssetConfiguration(StringBuilder report, string label, ESAssetReferBase refer)
        {
            if (refer == null) { report.Append("[FAIL] ").Append(label).AppendLine(": reference object is null."); return; }
            if (!refer.IsValid) { report.Append("[SKIP] ").Append(label).AppendLine(": not configured."); return; }
            report.Append("[PASS] ").Append(label).Append(": kind=").Append(refer.AssetKind)
                .Append(", id=").Append(refer.AssetIdentity).Append(", subAsset=").Append(refer.IsSubAsset)
                .Append(", enumKey=").Append(refer.ResolvedEnumKey).Append(", stringKey=")
                .Append(refer.ResolvedStringKey ?? string.Empty).AppendLine();
        }

        private static async UniTask LoadAndAppend<T>(StringBuilder report, string label, ESAssetRefer<T> refer, ESAssetScope scope, CancellationToken token) where T : UnityEngine.Object
        {
            if (refer == null || !refer.IsValid) { report.Append("[SKIP] ").Append(label).AppendLine(": not configured."); return; }
            try
            {
                T asset = await scope.LoadAsync(refer, token);
                report.Append("[PASS] ").Append(label).Append(": name=").Append(asset != null ? asset.name : "null")
                    .Append(", type=").Append(asset != null ? asset.GetType().FullName : "null")
                    .Append(", id=").Append(refer.AssetIdentity).Append(", subAsset=").Append(refer.IsSubAsset).AppendLine();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { AppendFailure(report, label, exception); }
        }

        private static void AppendReady<T>(StringBuilder report, string label, ESAssetRefer<T> refer) where T : UnityEngine.Object
        {
            if (refer == null || !refer.IsValid) { report.Append("[SKIP] ").Append(label).AppendLine(": not configured."); return; }
            bool ready = refer.TryLoad(out T asset);
            report.Append(ready ? "[PASS] " : "[MISS] ").Append(label).Append(": id=").Append(refer.AssetIdentity)
                .Append(", object=").Append(asset != null ? asset.name : "null").AppendLine();
        }

        private static void AppendFailure(StringBuilder report, string label, Exception exception)
        {
            report.Append("[FAIL] ").Append(label).Append(": ").Append(exception.GetType().Name).Append(" - ").Append(exception.Message).AppendLine();
        }

        private void PublishReport(StringBuilder report)
        {
            lastReport = report.ToString();
            Debug.Log(lastReport, this);
        }
    }
}
