using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESAssetGameCoreFlowTestDataInfo.cs")]
    public enum ESFlowTestEnumKey : ushort
    {
        None = 0,
        CommercialFlowTest = 1
    }

    [Serializable]
    public sealed class ESFlowTestConfigKey : ESGameCoreConfigKey<ESFlowTestEnumKey> { }

    [Serializable]
    public sealed class ESFlowTestRuntimeData
    {
        public int runtimeKey;
        public string keyName;
        public ESAssetGameCoreFlowTestDataInfo source;
    }

    /// <summary>Dedicated table for the commercial AssetRefer + GameCoreConfigKey integration test root.</summary>
    public static class ESFlowTestGameCore
    {
        public static readonly ESConfigKeyTable<ESFlowTestRuntimeData> Table = new ESConfigKeyTable<ESFlowTestRuntimeData>(4);

        public static bool Register(ESAssetGameCoreFlowTestDataInfo source)
        {
            if (source == null || source.testKey == null)
                return false;

            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild(false);
            try
            {
                string stringKey = source.testKey.GetStringKey(source.KeyName);
                int runtimeKey = Table.Bake(source.testKey, source.KeyName);
                if (runtimeKey == 0)
                    return false;
                if (Table.TryGet(runtimeKey, out ESFlowTestRuntimeData existing))
                    return ReferenceEquals(existing.source, source);

                return Table.Register(source.testKey, new ESFlowTestRuntimeData
                {
                    runtimeKey = runtimeKey,
                    keyName = stringKey,
                    source = source
                }, source.KeyName);
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

        [TitleGroup("Test Root")]
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
        [LabelText("Prefab"), InlineProperty] public ESAssetReferPrefab prefab = new ESAssetReferPrefab();
        [LabelText("Sprite / SubAsset"), InlineProperty] public ESAssetReferSprite sprite = new ESAssetReferSprite();
        [LabelText("AudioClip"), InlineProperty] public ESAssetReferAudioClip audioClip = new ESAssetReferAudioClip();
        [LabelText("Material"), InlineProperty] public ESAssetReferMaterial material = new ESAssetReferMaterial();
        [LabelText("Texture"), InlineProperty] public ESAssetReferTexture texture = new ESAssetReferTexture();
        [LabelText("Texture2D"), InlineProperty] public ESAssetReferTexture2D texture2D = new ESAssetReferTexture2D();
        [LabelText("AnimationClip / SubAsset"), InlineProperty] public ESAssetReferAnimationClip animationClip = new ESAssetReferAnimationClip();
        [LabelText("AnimatorController"), InlineProperty] public ESAssetReferAnimatorController animatorController = new ESAssetReferAnimatorController();
        [LabelText("SpriteAtlas"), InlineProperty] public ESAssetReferSpriteAtlas spriteAtlas = new ESAssetReferSpriteAtlas();
        [LabelText("Avatar / SubAsset"), InlineProperty] public ESAssetReferAvatar avatar = new ESAssetReferAvatar();
        [LabelText("VideoClip"), InlineProperty] public ESAssetReferVideoClip videoClip = new ESAssetReferVideoClip();
        [LabelText("TimelineAsset"), InlineProperty] public ESAssetReferTimelineAsset timelineAsset = new ESAssetReferTimelineAsset();
        [LabelText("PlayableAsset"), InlineProperty] public ESAssetReferPlayableAsset playableAsset = new ESAssetReferPlayableAsset();
        [LabelText("Mesh / SubAsset"), InlineProperty] public ESAssetReferMesh mesh = new ESAssetReferMesh();
        [LabelText("TerrainData"), InlineProperty] public ESAssetReferTerrainData terrainData = new ESAssetReferTerrainData();
        [LabelText("ScriptableObject"), InlineProperty] public ESAssetReferScriptableObject scriptableObject = new ESAssetReferScriptableObject();
        [LabelText("UnityObject"), InlineProperty] public ESAssetReferUnityObject unityObject = new ESAssetReferUnityObject();
        [LabelText("Scene"), InlineProperty] public ESAssetReferScene scene = new ESAssetReferScene();

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
            AppendReady(report, "ScriptableObject", scriptableObject);
            AppendReady(report, "UnityObject", unityObject);
            PublishReport(report);
        }

        [Button("5. Release Test References")]
        public void ReleaseTestReferences()
        {
            testScope?.Dispose();
            testScope = null;
            scene.Release();
            Debug.Log("[ESFlowTest][Release] Dedicated test Scope disposed; all test-held asset references were returned.", this);
        }

        public async UniTask<string> RunAssetLoadTestAsync(CancellationToken token = default)
        {
            testScope?.Dispose();
            testScope = ESAssets.CreateScope();
            var report = new StringBuilder(3072);
            report.AppendLine("[ESFlowTest][Load] Full asset loading report");
            await LoadAndAppend(report, "Prefab", prefab, testScope, token);
            await LoadAndAppend(report, "Sprite", sprite, testScope, token);
            await LoadAndAppend(report, "AudioClip", audioClip, testScope, token);
            await LoadAndAppend(report, "Material", material, testScope, token);
            await LoadAndAppend(report, "Texture", texture, testScope, token);
            await LoadAndAppend(report, "Texture2D", texture2D, testScope, token);
            await LoadAndAppend(report, "AnimationClip", animationClip, testScope, token);
            await LoadAndAppend(report, "AnimatorController", animatorController, testScope, token);
            await LoadAndAppend(report, "SpriteAtlas", spriteAtlas, testScope, token);
            await LoadAndAppend(report, "Avatar", avatar, testScope, token);
            await LoadAndAppend(report, "VideoClip", videoClip, testScope, token);
            report.AppendLine("[INFO] TimelineAsset: skipped; PlayableAsset covers the runtime load contract without a hard Timeline assembly edge.");
            await LoadAndAppend(report, "PlayableAsset", playableAsset, testScope, token);
            await LoadAndAppend(report, "Mesh", mesh, testScope, token);
            await LoadAndAppend(report, "TerrainData", terrainData, testScope, token);
            await LoadAndAppend(report, "ScriptableObject", scriptableObject, testScope, token);
            await LoadAndAppend(report, "UnityObject", unityObject, testScope, token);

            if (scene != null && scene.IsValid)
            {
                if (!loadScene) report.AppendLine("[SKIP] Scene: valid, loading disabled to avoid changing the active scene.");
                else
                {
                    try
                    {
                        ESRuntimeSceneHandle handle = await scene.LoadAsync(LoadSceneMode.Single, token);
                        report.Append("[PASS] Scene: ").Append(handle.Scene.path).AppendLine();
                    }
                    catch (Exception exception) { AppendFailure(report, "Scene", exception); }
                }
            }
            else report.AppendLine("[SKIP] Scene: not configured.");
            PublishReport(report);
            return lastReport;
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
