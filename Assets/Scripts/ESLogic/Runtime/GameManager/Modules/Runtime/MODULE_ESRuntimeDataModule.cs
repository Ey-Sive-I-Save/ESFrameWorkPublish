using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    public static class ESRuntimeDataGameCore
    {
        public static readonly ESBuffConfigKeyTable Buffs = new ESBuffConfigKeyTable(128);
        public static readonly ESShotConfigKeyTable Shots = new ESShotConfigKeyTable(128);
        public static readonly ESMonsterConfigKeyTable Monsters = new ESMonsterConfigKeyTable(128);
        public static readonly ESNpcConfigKeyTable Npcs = new ESNpcConfigKeyTable(128);
        public static readonly ESWeaponConfigKeyTable Weapons = new ESWeaponConfigKeyTable(64);
        public static readonly ESSkillConfigKeyTable Skills = new ESSkillConfigKeyTable(128);

        public static void BeginBuild(bool clear)
        {
            Buffs.BeginBuild(clear);
            Shots.BeginBuild(clear);
            Monsters.BeginBuild(clear);
            Npcs.BeginBuild(clear);
            Weapons.BeginBuild(clear);
            Skills.BeginBuild(clear);
        }

        public static void EndBuild()
        {
            Buffs.EndBuild();
            Shots.EndBuild();
            Monsters.EndBuild();
            Npcs.EndBuild();
            Weapons.EndBuild();
            Skills.EndBuild();
        }

        /// <summary>
        /// 仅在 Consumer/Provider 生命周期切换和全量资源安全点调用：先断开静态表
        /// 对旧 GameCore SO 的引用，随后由对应 Scope 归还底层 Handle。
        /// </summary>
        public static void ResetForResourceTransition()
        {
            if (Buffs.IsBuilding || Shots.IsBuilding || Monsters.IsBuilding || Npcs.IsBuilding
                || Weapons.IsBuilding || Skills.IsBuilding)
                throw new InvalidOperationException("[ESGameCore] 正在构建 GameCore 表，不能执行资源生命周期切换。");

            BeginBuild(true);
            EndBuild();
        }
    }

    [Serializable]
    public struct ESAssetAutoRegisterReport
    {
        public int libraryCount;
        public int normalizedPageCount;
        public int registeredPageCount;
        public int conflictCount;
        public string conflictReport;

        public override string ToString()
        {
            return "[ESAssetAutoRegister]"
                + " libraries=" + libraryCount
                + ", normalizedPages=" + normalizedPageCount
                + ", registeredPages=" + registeredPageCount
                + ", conflicts=" + conflictCount
                + (string.IsNullOrEmpty(conflictReport) ? string.Empty : "\n" + conflictReport);
        }
    }

    public static class ESRuntimeDataAsset
    {
        public static readonly ESAssetConfigKeyTable<ESAssetReferPrefabConfigData, GameObject> Prefabs = new ESAssetConfigKeyTable<ESAssetReferPrefabConfigData, GameObject>(256, "Asset.Prefab");
        public static readonly ESAssetConfigKeyTable<ESAssetReferSpriteConfigData, Sprite> Sprites = new ESAssetConfigKeyTable<ESAssetReferSpriteConfigData, Sprite>(256, "Asset.Sprite");
        public static readonly ESAssetConfigKeyTable<ESAssetReferAudioClipConfigData, AudioClip> AudioClips = new ESAssetConfigKeyTable<ESAssetReferAudioClipConfigData, AudioClip>(256, "Asset.AudioClip");
        public static readonly ESAssetConfigKeyTable<ESAssetReferAnimationClipConfigData, AnimationClip> AnimationClips = new ESAssetConfigKeyTable<ESAssetReferAnimationClipConfigData, AnimationClip>(256, "Asset.AnimationClip");
        public static readonly ESAssetConfigKeyTable<ESAssetReferAnimatorControllerConfigData, RuntimeAnimatorController> AnimatorControllers = new ESAssetConfigKeyTable<ESAssetReferAnimatorControllerConfigData, RuntimeAnimatorController>(128, "Asset.AnimatorController");
        public static readonly ESAssetConfigKeyTable<ESAssetReferMaterialConfigData, Material> Materials = new ESAssetConfigKeyTable<ESAssetReferMaterialConfigData, Material>(256, "Asset.Material");
        public static readonly ESAssetConfigKeyTable<ESAssetReferMeshConfigData, Mesh> Meshes = new ESAssetConfigKeyTable<ESAssetReferMeshConfigData, Mesh>(256, "Asset.Mesh");
        public static readonly ESAssetConfigKeyTable<ESAssetReferSceneConfigData, UnityEngine.Object> Scenes = new ESAssetConfigKeyTable<ESAssetReferSceneConfigData, UnityEngine.Object>(64, "Asset.Scene");
        public static readonly ESAssetConfigKeyTable<ESAssetReferTextureConfigData, Texture> Textures = new ESAssetConfigKeyTable<ESAssetReferTextureConfigData, Texture>(128, "Asset.Texture");
        public static readonly ESAssetConfigKeyTable<ESAssetReferTexture2DConfigData, Texture2D> Texture2Ds = new ESAssetConfigKeyTable<ESAssetReferTexture2DConfigData, Texture2D>(128, "Asset.Texture2D");
        public static readonly ESAssetConfigKeyTable<ESAssetReferSpriteAtlasConfigData, UnityEngine.U2D.SpriteAtlas> SpriteAtlases = new ESAssetConfigKeyTable<ESAssetReferSpriteAtlasConfigData, UnityEngine.U2D.SpriteAtlas>(64, "Asset.SpriteAtlas");
        public static readonly ESAssetConfigKeyTable<ESAssetReferAvatarConfigData, Avatar> Avatars = new ESAssetConfigKeyTable<ESAssetReferAvatarConfigData, Avatar>(64, "Asset.Avatar");
        public static readonly ESAssetConfigKeyTable<ESAssetReferPlayableAssetConfigData, UnityEngine.Playables.PlayableAsset> PlayableAssets = new ESAssetConfigKeyTable<ESAssetReferPlayableAssetConfigData, UnityEngine.Playables.PlayableAsset>(64, "Asset.PlayableAsset");
        public static readonly ESAssetConfigKeyTable<ESAssetReferScriptableObjectConfigData, ScriptableObject> ScriptableObjects = new ESAssetConfigKeyTable<ESAssetReferScriptableObjectConfigData, ScriptableObject>(128, "Asset.ScriptableObject");
        public static readonly ESAssetConfigKeyTable<ESAssetReferTimelineAssetConfigData, UnityEngine.Object> TimelineAssets = new ESAssetConfigKeyTable<ESAssetReferTimelineAssetConfigData, UnityEngine.Object>(64, "Asset.TimelineAsset");
        public static readonly ESAssetConfigKeyTable<ESAssetReferVideoClipConfigData, UnityEngine.Video.VideoClip> VideoClips = new ESAssetConfigKeyTable<ESAssetReferVideoClipConfigData, UnityEngine.Video.VideoClip>(64, "Asset.VideoClip");
        public static readonly ESAssetConfigKeyTable<ESAssetReferTerrainDataConfigData, TerrainData> TerrainDatas = new ESAssetConfigKeyTable<ESAssetReferTerrainDataConfigData, TerrainData>(32, "Asset.TerrainData");

        public static bool HasPendingAssetLoads =>
            Prefabs.HasPendingLoads || Sprites.HasPendingLoads || AudioClips.HasPendingLoads
            || AnimationClips.HasPendingLoads || AnimatorControllers.HasPendingLoads
            || Materials.HasPendingLoads || Meshes.HasPendingLoads || Scenes.HasPendingLoads
            || Textures.HasPendingLoads || Texture2Ds.HasPendingLoads || SpriteAtlases.HasPendingLoads
            || Avatars.HasPendingLoads || PlayableAssets.HasPendingLoads || ScriptableObjects.HasPendingLoads
            || TimelineAssets.HasPendingLoads || VideoClips.HasPendingLoads || TerrainDatas.HasPendingLoads;

        private static bool IsAnyAssetTableBuilding =>
            Prefabs.IsBuilding || Sprites.IsBuilding || AudioClips.IsBuilding
            || AnimationClips.IsBuilding || AnimatorControllers.IsBuilding
            || Materials.IsBuilding || Meshes.IsBuilding || Scenes.IsBuilding
            || Textures.IsBuilding || Texture2Ds.IsBuilding || SpriteAtlases.IsBuilding
            || Avatars.IsBuilding || PlayableAssets.IsBuilding || ScriptableObjects.IsBuilding
            || TimelineAssets.IsBuilding || VideoClips.IsBuilding || TerrainDatas.IsBuilding;

        public static void BeginBuild(bool clear)
        {
            EnsureCanBeginBuild(clear);

            Prefabs.BeginBuild(clear);
            Sprites.BeginBuild(clear);
            AudioClips.BeginBuild(clear);
            AnimationClips.BeginBuild(clear);
            AnimatorControllers.BeginBuild(clear);
            Materials.BeginBuild(clear);
            Meshes.BeginBuild(clear);
            Scenes.BeginBuild(clear);
            Textures.BeginBuild(clear);
            Texture2Ds.BeginBuild(clear);
            SpriteAtlases.BeginBuild(clear);
            Avatars.BeginBuild(clear);
            PlayableAssets.BeginBuild(clear);
            ScriptableObjects.BeginBuild(clear);
            TimelineAssets.BeginBuild(clear);
            VideoClips.BeginBuild(clear);
            TerrainDatas.BeginBuild(clear);
        }

        private static void EnsureCanBeginBuild(bool clear)
        {
            // 先全局预检，避免前几张分类表已经清空后才在后续表上发现冲突。
            if (IsAnyAssetTableBuilding)
                throw new InvalidOperationException("[ESAssetTable] 分类表已处于构建状态，不能重复开始全量构建。");
            if (clear && HasPendingAssetLoads)
                throw new InvalidOperationException("[ESAssetTable] 仍有加载请求，不能全量重建 Catalog。");
        }

        public static void EndBuild()
        {
            Prefabs.EndBuild();
            Sprites.EndBuild();
            AudioClips.EndBuild();
            AnimationClips.EndBuild();
            AnimatorControllers.EndBuild();
            Materials.EndBuild();
            Meshes.EndBuild();
            Scenes.EndBuild();
            Textures.EndBuild();
            Texture2Ds.EndBuild();
            SpriteAtlases.EndBuild();
            Avatars.EndBuild();
            PlayableAssets.EndBuild();
            ScriptableObjects.EndBuild();
            TimelineAssets.EndBuild();
            VideoClips.EndBuild();
            TerrainDatas.EndBuild();
        }

        private sealed class AssetConfigPreflightData : ESAssetReferConfigDataBase<UnityEngine.Object>
        {
        }

        /// <summary>
        /// Catalog/Page 全量重建的隔离预检表。它执行正式表相同的强类型初始化、重复键、
        /// 别名和 RuntimeKey 规则，但绝不触碰正式驻留外壳、Ready 或 Loader Handle。
        /// </summary>
        private sealed class AssetConfigBuildPreflight
        {
            private static readonly Func<AssetConfigPreflightData> CreateShell = CreatePreflightShell;
            private readonly Dictionary<ESAssetReferKind, ESAssetConfigKeyTable<AssetConfigPreflightData, UnityEngine.Object>> tables
                = new Dictionary<ESAssetReferKind, ESAssetConfigKeyTable<AssetConfigPreflightData, UnityEngine.Object>>(17);

            public bool Stage<TData, TKey, TAsset>(ESAssetReferKind kind, in ESAssetConfigRecord record)
                where TData : ESAssetReferConfigDataBase<TAsset>, IESAssetConfigDataInitializer<TKey>, new()
                where TKey : class, IESAssetConfigKeyInitializer, new()
                where TAsset : UnityEngine.Object
            {
                TKey key = new TKey();
                key.InitializeRuntimeKey(
                    record.enumKey,
                    record.stringKey,
                    record.assetGuid,
                    record.assetLocalFileId,
                    record.assetTypeName,
                    record.assetPath);

                // 在临时对象上执行真实字段初始化，确保正式外壳在任何初始化异常前保持原样。
                TData validationData = new TData();
                validationData.InitializeFromRecord(key, in record);

                ESAssetConfigKeyTable<AssetConfigPreflightData, UnityEngine.Object> table = GetOrCreateTable(kind);
                if (!table.TryAcquireBuildRecord(
                        key,
                        CreateShell,
                        record.assetGuid + ":" + record.assetLocalFileId,
                        out AssetConfigPreflightData shell))
                    return false;

                return table.RegisterPreparedBuildRecord(key, shell, record.stringKey) != 0;
            }

            public void EndBuild()
            {
                foreach (KeyValuePair<ESAssetReferKind, ESAssetConfigKeyTable<AssetConfigPreflightData, UnityEngine.Object>> pair in tables)
                    pair.Value.EndBuild();
            }

            private ESAssetConfigKeyTable<AssetConfigPreflightData, UnityEngine.Object> GetOrCreateTable(ESAssetReferKind kind)
            {
                if (tables.TryGetValue(kind, out ESAssetConfigKeyTable<AssetConfigPreflightData, UnityEngine.Object> table))
                    return table;

                table = new ESAssetConfigKeyTable<AssetConfigPreflightData, UnityEngine.Object>(16, "Asset." + kind);
                table.BeginBuild(true);
                tables.Add(kind, table);
                return table;
            }

            private static AssetConfigPreflightData CreatePreflightShell()
            {
                return new AssetConfigPreflightData();
            }
        }

        public static bool SetLoadedAsset<TData, TAsset>(ESConfigKeyTable<TData> table, int runtimeKey, TAsset asset)
            where TData : ESAssetReferConfigDataBase<TAsset>
            where TAsset : UnityEngine.Object
        {
            if (table == null || runtimeKey == 0 || !table.TryGet(runtimeKey, out TData data))
                return false;

            data.SetLoadedAsset(asset);
            return true;
        }

        public static bool ClearLoadedAsset<TData>(ESConfigKeyTable<TData> table, int runtimeKey)
            where TData : class, IESAssetReferConfigData
        {
            if (table == null || runtimeKey == 0 || !table.TryGet(runtimeKey, out TData data))
                return false;

            data.ClearLoadedAsset();
            return true;
        }

        public static int ClearAllLoadedAssets()
        {
            int count = 0;
            count += ClearLoadedAssets(Prefabs);
            count += ClearLoadedAssets(Sprites);
            count += ClearLoadedAssets(AudioClips);
            count += ClearLoadedAssets(AnimationClips);
            count += ClearLoadedAssets(AnimatorControllers);
            count += ClearLoadedAssets(Materials);
            count += ClearLoadedAssets(Meshes);
            count += ClearLoadedAssets(Scenes);
            count += ClearLoadedAssets(Textures);
            count += ClearLoadedAssets(Texture2Ds);
            count += ClearLoadedAssets(SpriteAtlases);
            count += ClearLoadedAssets(Avatars);
            count += ClearLoadedAssets(PlayableAssets);
            count += ClearLoadedAssets(ScriptableObjects);
            count += ClearLoadedAssets(TimelineAssets);
            count += ClearLoadedAssets(VideoClips);
            count += ClearLoadedAssets(TerrainDatas);
            return count;
        }

        /// <summary>
        /// 全量资源安全点/Provider 重建专用：逐表释放 AssetTable Loader 持有的
        /// Runtime Handle，并清理表内 LoadedAsset 状态。仅清字段会留下旧 Handle，
        /// 导致同一 RuntimeKey 下一次加载时发生重复键。
        /// </summary>
        public static int ResetAllAssetLoaders()
        {
            int count = 0;
            count += Prefabs.ResetLoader();
            count += Sprites.ResetLoader();
            count += AudioClips.ResetLoader();
            count += AnimationClips.ResetLoader();
            count += AnimatorControllers.ResetLoader();
            count += Materials.ResetLoader();
            count += Meshes.ResetLoader();
            count += Scenes.ResetLoader();
            count += Textures.ResetLoader();
            count += Texture2Ds.ResetLoader();
            count += SpriteAtlases.ResetLoader();
            count += Avatars.ResetLoader();
            count += PlayableAssets.ResetLoader();
            count += ScriptableObjects.ResetLoader();
            count += TimelineAssets.ResetLoader();
            count += VideoClips.ResetLoader();
            count += TerrainDatas.ResetLoader();
            return count;
        }

        public static void ClearAllPendingAssetLoads()
        {
            Prefabs.ClearPendingLoads();
            Sprites.ClearPendingLoads();
            AudioClips.ClearPendingLoads();
            AnimationClips.ClearPendingLoads();
            AnimatorControllers.ClearPendingLoads();
            Materials.ClearPendingLoads();
            Meshes.ClearPendingLoads();
            Scenes.ClearPendingLoads();
            Textures.ClearPendingLoads();
            Texture2Ds.ClearPendingLoads();
            SpriteAtlases.ClearPendingLoads();
            Avatars.ClearPendingLoads();
            PlayableAssets.ClearPendingLoads();
            ScriptableObjects.ClearPendingLoads();
            TimelineAssets.ClearPendingLoads();
            VideoClips.ClearPendingLoads();
            TerrainDatas.ClearPendingLoads();
        }

        private static int ClearLoadedAssets<TData>(ESConfigKeyTable<TData> table)
            where TData : class, IESAssetReferConfigData
        {
            int count = 0;
            for (int i = 0; i < table.Count; i++)
            {
                if (!table.TryGetBySlot(i, out TData data) || !data.HasLoadedAsset)
                    continue;

                data.ClearLoadedAsset();
                count++;
            }

            return count;
        }

#if UNITY_EDITOR
        [MenuItem("【ES】/资源与发布/索引与注册/从 AssetLibrary 重建编辑器查询表")]
        public static void MenuRebuildEditorConfigQueryTableFromLibraries()
        {
            ESAssetAutoRegisterReport report = RebuildEditorConfigQueryTableFromLibraries(true, true);
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        public static ESAssetAutoRegisterReport RebuildEditorConfigQueryTableFromLibraries(bool rebuildAssetConfigTables = true, bool clearBeforeBuild = true)
        {
            ESAssetAutoRegisterReport report = new ESAssetAutoRegisterReport();
            List<ESAssetLibrary> indexedLibraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>() ?? new List<ESAssetLibrary>(0);
            List<ESAssetLibrary> libraries = new List<ESAssetLibrary>(indexedLibraries.Count);
            for (int i = 0; i < indexedLibraries.Count; i++)
            {
                ESAssetLibrary library = indexedLibraries[i];
                if (library == null)
                    continue;

                if (libraries.Contains(library))
                    continue;

                report.normalizedPageCount += library.NormalizePagesEditor();
                libraries.Add(library);
                EditorUtility.SetDirty(library);
            }

            report.libraryCount = libraries.Count;
            ESAssetRegistry.BuildFromAssetLibraries(libraries, clearBeforeBuild);
            report.registeredPageCount = ESAssetRegistry.EditorConfigQueryTable.Count;

            if (rebuildAssetConfigTables)
                RebuildAssetConfigTablesFromPages(ESAssetRegistry.Pages);

            report.conflictCount = GetAssetConflictCount();
            report.conflictReport = GetAssetConflictReport();
            return report;
        }

        public static int RebuildAssetConfigTablesFromPages(IReadOnlyList<ESAssetPage> pages)
        {
            EnsureCanBeginBuild(true);
            PreflightAssetConfigTablesFromPages(pages);
            BeginBuild(true);
            try
            {
                int count = 0;
                if (pages != null)
                {
                    for (int i = 0; i < pages.Count; i++)
                    {
                        if (RegisterPageAsAssetConfigData(pages[i]))
                            count++;
                    }
                }

                return count;
            }
            finally
            {
                EndBuild();
            }
        }

        private static void PreflightAssetConfigTablesFromPages(IReadOnlyList<ESAssetPage> pages)
        {
            var preflight = new AssetConfigBuildPreflight();
            try
            {
                if (pages == null)
                    return;

                for (int i = 0; i < pages.Count; i++)
                    StagePageAsAssetConfigData(pages[i], preflight);
            }
            finally
            {
                preflight.EndBuild();
            }
        }

        private static bool StagePageAsAssetConfigData(ESAssetPage page, AssetConfigBuildPreflight preflight)
        {
            if (page == null)
                return false;

            if (page.Kind == ESAssetReferKind.ScriptableObject && page.OB is ScriptableObject scriptableObject
                && ESScriptableObjectClassification.GetClass(scriptableObject) == ESScriptableObjectClass.GameCore)
                return false;

            ESAssetConfigRecord record = CreateAssetConfigRecord(page);
            return StageAssetConfigRecord(page.Kind, in record, preflight);
        }

        private static bool RegisterPageAsAssetConfigData(ESAssetPage page)
        {
            if (page == null)
                return false;

            // GameCore SO is Consumer-owned and intentionally has no global AssetTable entry.
            if (page.Kind == ESAssetReferKind.ScriptableObject && page.OB is ScriptableObject scriptableObject
                && ESScriptableObjectClassification.GetClass(scriptableObject) == ESScriptableObjectClass.GameCore)
                return false;

            switch (page.Kind)
            {
                case ESAssetReferKind.Prefab:
                    return RegisterPrefab(CreateAssetDataFromPage<ESAssetReferPrefabConfigData, ESAssetReferPrefabConfigKey, GameObject>(page, Prefabs));
                case ESAssetReferKind.Scene:
                    return RegisterScene(CreateAssetDataFromPage<ESAssetReferSceneConfigData, ESAssetReferSceneConfigKey, UnityEngine.Object>(page, Scenes));
                case ESAssetReferKind.Sprite:
                    return RegisterSprite(CreateAssetDataFromPage<ESAssetReferSpriteConfigData, ESAssetReferSpriteConfigKey, Sprite>(page, Sprites));
                case ESAssetReferKind.Texture2D:
                    return RegisterTexture2D(CreateAssetDataFromPage<ESAssetReferTexture2DConfigData, ESAssetReferTexture2DConfigKey, Texture2D>(page, Texture2Ds));
                case ESAssetReferKind.Texture:
                    return RegisterTexture(CreateAssetDataFromPage<ESAssetReferTextureConfigData, ESAssetReferTextureConfigKey, Texture>(page, Textures));
                case ESAssetReferKind.SpriteAtlas:
                    return RegisterSpriteAtlas(CreateAssetDataFromPage<ESAssetReferSpriteAtlasConfigData, ESAssetReferSpriteAtlasConfigKey, UnityEngine.U2D.SpriteAtlas>(page, SpriteAtlases));
                case ESAssetReferKind.Material:
                    return RegisterMaterial(CreateAssetDataFromPage<ESAssetReferMaterialConfigData, ESAssetReferMaterialConfigKey, Material>(page, Materials));
                case ESAssetReferKind.Mesh:
                    return RegisterMesh(CreateAssetDataFromPage<ESAssetReferMeshConfigData, ESAssetReferMeshConfigKey, Mesh>(page, Meshes));
                case ESAssetReferKind.AnimationClip:
                    return RegisterAnimationClip(CreateAssetDataFromPage<ESAssetReferAnimationClipConfigData, ESAssetReferAnimationClipConfigKey, AnimationClip>(page, AnimationClips));
                case ESAssetReferKind.AnimatorController:
                    return RegisterAnimatorController(CreateAssetDataFromPage<ESAssetReferAnimatorControllerConfigData, ESAssetReferAnimatorControllerConfigKey, RuntimeAnimatorController>(page, AnimatorControllers));
                case ESAssetReferKind.Avatar:
                    return RegisterAvatar(CreateAssetDataFromPage<ESAssetReferAvatarConfigData, ESAssetReferAvatarConfigKey, Avatar>(page, Avatars));
                case ESAssetReferKind.AudioClip:
                    return RegisterAudioClip(CreateAssetDataFromPage<ESAssetReferAudioClipConfigData, ESAssetReferAudioClipConfigKey, AudioClip>(page, AudioClips));
                case ESAssetReferKind.VideoClip:
                    return RegisterVideoClip(CreateAssetDataFromPage<ESAssetReferVideoClipConfigData, ESAssetReferVideoClipConfigKey, UnityEngine.Video.VideoClip>(page, VideoClips));
                case ESAssetReferKind.TimelineAsset:
                    return RegisterTimelineAsset(CreateAssetDataFromPage<ESAssetReferTimelineAssetConfigData, ESAssetReferTimelineAssetConfigKey, UnityEngine.Object>(page, TimelineAssets));
                case ESAssetReferKind.PlayableAsset:
                    return RegisterPlayableAsset(CreateAssetDataFromPage<ESAssetReferPlayableAssetConfigData, ESAssetReferPlayableAssetConfigKey, UnityEngine.Playables.PlayableAsset>(page, PlayableAssets));
                case ESAssetReferKind.ScriptableObject:
                    return RegisterScriptableObject(CreateAssetDataFromPage<ESAssetReferScriptableObjectConfigData, ESAssetReferScriptableObjectConfigKey, ScriptableObject>(page, ScriptableObjects));
                case ESAssetReferKind.TerrainData:
                    return RegisterTerrainData(CreateAssetDataFromPage<ESAssetReferTerrainDataConfigData, ESAssetReferTerrainDataConfigKey, TerrainData>(page, TerrainDatas));
                default:
                    return false;
            }
        }

        private static TData CreateAssetDataFromPage<TData, TKey, TAsset>(
            ESAssetPage page,
            ESAssetConfigKeyTable<TData, TAsset> table)
            where TData : ESAssetReferConfigDataBase<TAsset>, IESAssetConfigDataInitializer<TKey>, new()
            where TKey : class, IESAssetConfigKeyInitializer, new()
            where TAsset : UnityEngine.Object
        {
            ESAssetConfigRecord record = CreateAssetConfigRecord(page);
            return CreateAssetDataFromRecord<TData, TKey, TAsset>(in record, table);
        }

        private static ESAssetConfigRecord CreateAssetConfigRecord(ESAssetPage page)
        {
            return new ESAssetConfigRecord(
                page.EnumKey,
                page.EffectiveStringKey,
                page.AssetGuid,
                page.LocalFileId,
                page.AssetTypeName,
                page.AssetPath,
                page.Name,
                page.SourceLibrary);
        }
#endif

        public static int RebuildAssetConfigTablesFromCatalogs(IReadOnlyList<ESRuntimeCatalog> catalogs)
        {
            EnsureCanBeginBuild(true);
            PreflightAssetConfigTablesFromCatalogs(catalogs);
            BeginBuild(true);
            try
            {
                int count = 0;
                if (catalogs == null)
                    return count;

                for (int catalogIndex = 0; catalogIndex < catalogs.Count; catalogIndex++)
                {
                    ESRuntimeCatalog catalog = catalogs[catalogIndex];
                    if (catalog?.assets == null)
                        continue;

                    for (int assetIndex = 0; assetIndex < catalog.assets.Count; assetIndex++)
                        if (RegisterCatalogEntryAsAssetConfigData(catalog.assets[assetIndex]))
                            count++;
                }
                return count;
            }
            finally
            {
                EndBuild();
            }
        }

        private static void PreflightAssetConfigTablesFromCatalogs(IReadOnlyList<ESRuntimeCatalog> catalogs)
        {
            var preflight = new AssetConfigBuildPreflight();
            try
            {
                if (catalogs == null)
                    return;

                for (int catalogIndex = 0; catalogIndex < catalogs.Count; catalogIndex++)
                {
                    ESRuntimeCatalog catalog = catalogs[catalogIndex];
                    if (catalog?.assets == null)
                        continue;

                    for (int assetIndex = 0; assetIndex < catalog.assets.Count; assetIndex++)
                        StageCatalogEntryAsAssetConfigData(catalog.assets[assetIndex], preflight);
                }
            }
            finally
            {
                preflight.EndBuild();
            }
        }

        private static bool StageCatalogEntryAsAssetConfigData(
            ESRuntimeCatalogEntry entry,
            AssetConfigBuildPreflight preflight)
        {
            if (entry == null || !entry.isBusinessAsset || entry.identity == null
                || !entry.identity.IsValid || !Enum.TryParse(entry.kind, out ESAssetReferKind kind))
                return false;
            if (entry.enumKey == 0 && string.IsNullOrEmpty(entry.stringKey))
                throw new InvalidOperationException("Catalog 业务资产缺少 EnumKey/StringKey：" + entry.identity.guid);

            ESAssetConfigRecord record = CreateAssetConfigRecord(entry);
            return StageAssetConfigRecord(kind, in record, preflight);
        }

        private static bool StageAssetConfigRecord(
            ESAssetReferKind kind,
            in ESAssetConfigRecord record,
            AssetConfigBuildPreflight preflight)
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab: return preflight.Stage<ESAssetReferPrefabConfigData, ESAssetReferPrefabConfigKey, GameObject>(kind, in record);
                case ESAssetReferKind.Scene: return preflight.Stage<ESAssetReferSceneConfigData, ESAssetReferSceneConfigKey, UnityEngine.Object>(kind, in record);
                case ESAssetReferKind.Sprite: return preflight.Stage<ESAssetReferSpriteConfigData, ESAssetReferSpriteConfigKey, Sprite>(kind, in record);
                case ESAssetReferKind.Texture2D: return preflight.Stage<ESAssetReferTexture2DConfigData, ESAssetReferTexture2DConfigKey, Texture2D>(kind, in record);
                case ESAssetReferKind.Texture: return preflight.Stage<ESAssetReferTextureConfigData, ESAssetReferTextureConfigKey, Texture>(kind, in record);
                case ESAssetReferKind.SpriteAtlas: return preflight.Stage<ESAssetReferSpriteAtlasConfigData, ESAssetReferSpriteAtlasConfigKey, UnityEngine.U2D.SpriteAtlas>(kind, in record);
                case ESAssetReferKind.Material: return preflight.Stage<ESAssetReferMaterialConfigData, ESAssetReferMaterialConfigKey, Material>(kind, in record);
                case ESAssetReferKind.Mesh: return preflight.Stage<ESAssetReferMeshConfigData, ESAssetReferMeshConfigKey, Mesh>(kind, in record);
                case ESAssetReferKind.AnimationClip: return preflight.Stage<ESAssetReferAnimationClipConfigData, ESAssetReferAnimationClipConfigKey, AnimationClip>(kind, in record);
                case ESAssetReferKind.AnimatorController: return preflight.Stage<ESAssetReferAnimatorControllerConfigData, ESAssetReferAnimatorControllerConfigKey, RuntimeAnimatorController>(kind, in record);
                case ESAssetReferKind.Avatar: return preflight.Stage<ESAssetReferAvatarConfigData, ESAssetReferAvatarConfigKey, Avatar>(kind, in record);
                case ESAssetReferKind.AudioClip: return preflight.Stage<ESAssetReferAudioClipConfigData, ESAssetReferAudioClipConfigKey, AudioClip>(kind, in record);
                case ESAssetReferKind.VideoClip: return preflight.Stage<ESAssetReferVideoClipConfigData, ESAssetReferVideoClipConfigKey, UnityEngine.Video.VideoClip>(kind, in record);
                case ESAssetReferKind.TimelineAsset: return preflight.Stage<ESAssetReferTimelineAssetConfigData, ESAssetReferTimelineAssetConfigKey, UnityEngine.Object>(kind, in record);
                case ESAssetReferKind.PlayableAsset: return preflight.Stage<ESAssetReferPlayableAssetConfigData, ESAssetReferPlayableAssetConfigKey, UnityEngine.Playables.PlayableAsset>(kind, in record);
                case ESAssetReferKind.ScriptableObject: return preflight.Stage<ESAssetReferScriptableObjectConfigData, ESAssetReferScriptableObjectConfigKey, ScriptableObject>(kind, in record);
                case ESAssetReferKind.TerrainData: return preflight.Stage<ESAssetReferTerrainDataConfigData, ESAssetReferTerrainDataConfigKey, TerrainData>(kind, in record);
                default: return false;
            }
        }

        private static bool RegisterCatalogEntryAsAssetConfigData(ESRuntimeCatalogEntry entry)
        {
            if (entry == null || !entry.isBusinessAsset || entry.identity == null || !entry.identity.IsValid || !Enum.TryParse(entry.kind, out ESAssetReferKind kind))
                return false;
            if (entry.enumKey == 0 && string.IsNullOrEmpty(entry.stringKey))
                throw new InvalidOperationException("Catalog \u4e1a\u52a1\u8d44\u4ea7\u7f3a\u5c11 EnumKey/StringKey\uff1a" + entry.identity.guid);

            switch (kind)
            {
                case ESAssetReferKind.Prefab: return RegisterPrefab(CreateAssetDataFromCatalog<ESAssetReferPrefabConfigData, ESAssetReferPrefabConfigKey, GameObject>(entry, Prefabs));
                case ESAssetReferKind.Scene: return RegisterScene(CreateAssetDataFromCatalog<ESAssetReferSceneConfigData, ESAssetReferSceneConfigKey, UnityEngine.Object>(entry, Scenes));
                case ESAssetReferKind.Sprite: return RegisterSprite(CreateAssetDataFromCatalog<ESAssetReferSpriteConfigData, ESAssetReferSpriteConfigKey, Sprite>(entry, Sprites));
                case ESAssetReferKind.Texture2D: return RegisterTexture2D(CreateAssetDataFromCatalog<ESAssetReferTexture2DConfigData, ESAssetReferTexture2DConfigKey, Texture2D>(entry, Texture2Ds));
                case ESAssetReferKind.Texture: return RegisterTexture(CreateAssetDataFromCatalog<ESAssetReferTextureConfigData, ESAssetReferTextureConfigKey, Texture>(entry, Textures));
                case ESAssetReferKind.SpriteAtlas: return RegisterSpriteAtlas(CreateAssetDataFromCatalog<ESAssetReferSpriteAtlasConfigData, ESAssetReferSpriteAtlasConfigKey, UnityEngine.U2D.SpriteAtlas>(entry, SpriteAtlases));
                case ESAssetReferKind.Material: return RegisterMaterial(CreateAssetDataFromCatalog<ESAssetReferMaterialConfigData, ESAssetReferMaterialConfigKey, Material>(entry, Materials));
                case ESAssetReferKind.Mesh: return RegisterMesh(CreateAssetDataFromCatalog<ESAssetReferMeshConfigData, ESAssetReferMeshConfigKey, Mesh>(entry, Meshes));
                case ESAssetReferKind.AnimationClip: return RegisterAnimationClip(CreateAssetDataFromCatalog<ESAssetReferAnimationClipConfigData, ESAssetReferAnimationClipConfigKey, AnimationClip>(entry, AnimationClips));
                case ESAssetReferKind.AnimatorController: return RegisterAnimatorController(CreateAssetDataFromCatalog<ESAssetReferAnimatorControllerConfigData, ESAssetReferAnimatorControllerConfigKey, RuntimeAnimatorController>(entry, AnimatorControllers));
                case ESAssetReferKind.Avatar: return RegisterAvatar(CreateAssetDataFromCatalog<ESAssetReferAvatarConfigData, ESAssetReferAvatarConfigKey, Avatar>(entry, Avatars));
                case ESAssetReferKind.AudioClip: return RegisterAudioClip(CreateAssetDataFromCatalog<ESAssetReferAudioClipConfigData, ESAssetReferAudioClipConfigKey, AudioClip>(entry, AudioClips));
                case ESAssetReferKind.VideoClip: return RegisterVideoClip(CreateAssetDataFromCatalog<ESAssetReferVideoClipConfigData, ESAssetReferVideoClipConfigKey, UnityEngine.Video.VideoClip>(entry, VideoClips));
                case ESAssetReferKind.TimelineAsset: return RegisterTimelineAsset(CreateAssetDataFromCatalog<ESAssetReferTimelineAssetConfigData, ESAssetReferTimelineAssetConfigKey, UnityEngine.Object>(entry, TimelineAssets));
                case ESAssetReferKind.PlayableAsset: return RegisterPlayableAsset(CreateAssetDataFromCatalog<ESAssetReferPlayableAssetConfigData, ESAssetReferPlayableAssetConfigKey, UnityEngine.Playables.PlayableAsset>(entry, PlayableAssets));
                case ESAssetReferKind.ScriptableObject: return RegisterScriptableObject(CreateAssetDataFromCatalog<ESAssetReferScriptableObjectConfigData, ESAssetReferScriptableObjectConfigKey, ScriptableObject>(entry, ScriptableObjects));
                case ESAssetReferKind.TerrainData: return RegisterTerrainData(CreateAssetDataFromCatalog<ESAssetReferTerrainDataConfigData, ESAssetReferTerrainDataConfigKey, TerrainData>(entry, TerrainDatas));
                default: return false;
            }
        }

        private static TData CreateAssetDataFromCatalog<TData, TKey, TAsset>(
            ESRuntimeCatalogEntry entry,
            ESAssetConfigKeyTable<TData, TAsset> table)
            where TData : ESAssetReferConfigDataBase<TAsset>, IESAssetConfigDataInitializer<TKey>, new()
            where TKey : class, IESAssetConfigKeyInitializer, new()
            where TAsset : UnityEngine.Object
        {
            ESAssetConfigRecord record = CreateAssetConfigRecord(entry);
            return CreateAssetDataFromRecord<TData, TKey, TAsset>(in record, table);
        }

        private static ESAssetConfigRecord CreateAssetConfigRecord(ESRuntimeCatalogEntry entry)
        {
            return new ESAssetConfigRecord(
                entry.enumKey,
                entry.stringKey,
                entry.identity.guid,
                entry.identity.localFileId,
                entry.assetTypeName,
                null,
                entry.pageName,
                entry.libraryFolder);
        }

        private static TData CreateAssetDataFromRecord<TData, TKey, TAsset>(
            in ESAssetConfigRecord record,
            ESAssetConfigKeyTable<TData, TAsset> table)
            where TData : ESAssetReferConfigDataBase<TAsset>, IESAssetConfigDataInitializer<TKey>, new()
            where TKey : class, IESAssetConfigKeyInitializer, new()
            where TAsset : UnityEngine.Object
        {
            TKey key = new TKey();
            key.InitializeRuntimeKey(
                record.enumKey,
                record.stringKey,
                record.assetGuid,
                record.assetLocalFileId,
                record.assetTypeName,
                record.assetPath);

            if (!table.TryAcquireBuildRecord(
                    key,
                    AssetConfigDataFactory<TData>.Create,
                    record.assetGuid + ":" + record.assetLocalFileId,
                    out TData data))
                return null;

            data.InitializeFromRecord(key, in record);
            return data;
        }

        private static class AssetConfigDataFactory<TData> where TData : new()
        {
            public static readonly Func<TData> Create = CreateInstance;

            private static TData CreateInstance()
            {
                return new TData();
            }
        }

        public static int GetAssetConflictCount()
        {
            return Prefabs.ConflictCount
                + Sprites.ConflictCount
                + AudioClips.ConflictCount
                + AnimationClips.ConflictCount
                + AnimatorControllers.ConflictCount
                + Materials.ConflictCount
                + Meshes.ConflictCount
                + Scenes.ConflictCount
                + Textures.ConflictCount
                + Texture2Ds.ConflictCount
                + SpriteAtlases.ConflictCount
                + Avatars.ConflictCount
                + PlayableAssets.ConflictCount
                + ScriptableObjects.ConflictCount
                + TimelineAssets.ConflictCount
                + VideoClips.ConflictCount
                + TerrainDatas.ConflictCount;
        }

        public static string GetAssetConflictReport()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(512);
            AppendConflictReport(builder, "Prefab", Prefabs.GetConflictReport());
            AppendConflictReport(builder, "Sprite", Sprites.GetConflictReport());
            AppendConflictReport(builder, "AudioClip", AudioClips.GetConflictReport());
            AppendConflictReport(builder, "AnimationClip", AnimationClips.GetConflictReport());
            AppendConflictReport(builder, "AnimatorController", AnimatorControllers.GetConflictReport());
            AppendConflictReport(builder, "Material", Materials.GetConflictReport());
            AppendConflictReport(builder, "Mesh", Meshes.GetConflictReport());
            AppendConflictReport(builder, "Scene", Scenes.GetConflictReport());
            AppendConflictReport(builder, "Texture", Textures.GetConflictReport());
            AppendConflictReport(builder, "Texture2D", Texture2Ds.GetConflictReport());
            AppendConflictReport(builder, "SpriteAtlas", SpriteAtlases.GetConflictReport());
            AppendConflictReport(builder, "Avatar", Avatars.GetConflictReport());
            AppendConflictReport(builder, "PlayableAsset", PlayableAssets.GetConflictReport());
            AppendConflictReport(builder, "ScriptableObject", ScriptableObjects.GetConflictReport());
            AppendConflictReport(builder, "TimelineAsset", TimelineAssets.GetConflictReport());
            AppendConflictReport(builder, "VideoClip", VideoClips.GetConflictReport());
            AppendConflictReport(builder, "TerrainData", TerrainDatas.GetConflictReport());
            return builder.ToString();
        }

        private static void AppendConflictReport(System.Text.StringBuilder builder, string title, string report)
        {
            if (string.IsNullOrEmpty(report))
                return;

            builder.Append('[').Append(title).Append(']').AppendLine();
            builder.Append(report);
        }

        public static bool RegisterPrefab(ESAssetReferPrefabConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Prefabs.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterSprite(ESAssetReferSpriteConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Sprites.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterAudioClip(ESAssetReferAudioClipConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = AudioClips.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterAnimationClip(ESAssetReferAnimationClipConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = AnimationClips.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterAnimatorController(ESAssetReferAnimatorControllerConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = AnimatorControllers.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterMaterial(ESAssetReferMaterialConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Materials.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterMesh(ESAssetReferMeshConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Meshes.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterScene(ESAssetReferSceneConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Scenes.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterTexture(ESAssetReferTextureConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Textures.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterTexture2D(ESAssetReferTexture2DConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Texture2Ds.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterSpriteAtlas(ESAssetReferSpriteAtlasConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = SpriteAtlases.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterAvatar(ESAssetReferAvatarConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Avatars.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterPlayableAsset(ESAssetReferPlayableAssetConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = PlayableAssets.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterScriptableObject(ESAssetReferScriptableObjectConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = ScriptableObjects.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterTimelineAsset(ESAssetReferTimelineAssetConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = TimelineAssets.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterVideoClip(ESAssetReferVideoClipConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = VideoClips.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

        public static bool RegisterTerrainData(ESAssetReferTerrainDataConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = TerrainDatas.RegisterAndGetRuntimeKey(data.key, data, data.keyName);
            return data.runtimeKey != 0;
        }

    }

    public sealed class ESConsumerResidentAssetPreloadReport
    {
        public int requestedCount;
        public int loadedCount;
        public int skippedCount;
        public readonly List<string> errors = new List<string>();
    }

    [Serializable, TypeRegistryItem("RuntimeData/Table")]
    public sealed class ESRuntimeDataModule : ESSystemModule
    {
        public static readonly ESBuffConfigKeyTable BuffTable = ESRuntimeDataGameCore.Buffs;
        public static readonly ESShotConfigKeyTable ShotTable = ESRuntimeDataGameCore.Shots;
        public static readonly ESMonsterConfigKeyTable MonsterTable = ESRuntimeDataGameCore.Monsters;
        public static readonly ESNpcConfigKeyTable NpcTable = ESRuntimeDataGameCore.Npcs;
        public static readonly ESWeaponConfigKeyTable WeaponTable = ESRuntimeDataGameCore.Weapons;
        public static readonly ESSkillConfigKeyTable SkillTable = ESRuntimeDataGameCore.Skills;
        public static readonly ESRuntimeInstanceIndex<ESActiveBuffRuntime> BuffInstanceIndex = new ESRuntimeInstanceIndex<ESActiveBuffRuntime>(128);
        public static readonly ESRuntimeInstanceIndex<Item> ShotInstanceIndex = new ESRuntimeInstanceIndex<Item>(128);

        [ShowInInspector, ReadOnly, LabelText("Buff Table")]
        public readonly ESBuffConfigKeyTable Buffs = BuffTable;
        [ShowInInspector, ReadOnly, LabelText("\u98de\u884c\u7269\u8868")]
        public readonly ESShotConfigKeyTable Shots = ShotTable;

        [ShowInInspector, ReadOnly, LabelText("Monster Table")]
        public readonly ESMonsterConfigKeyTable Monsters = MonsterTable;

        [ShowInInspector, ReadOnly, LabelText("NPC Table")]
        public readonly ESNpcConfigKeyTable Npcs = NpcTable;

        [ShowInInspector, ReadOnly, LabelText("Weapon Table")]
        public readonly ESWeaponConfigKeyTable Weapons = WeaponTable;
        [ShowInInspector, ReadOnly, LabelText("\u6280\u80fd\u8868")]
        public readonly ESSkillConfigKeyTable Skills = SkillTable;
        [ShowInInspector, ReadOnly, LabelText("Buff\u5b9e\u4f8b\u7d22\u5f15")]
        public readonly ESRuntimeInstanceIndex<ESActiveBuffRuntime> BuffInstances = BuffInstanceIndex;

        [ShowInInspector, ReadOnly, LabelText("Shot Instance Index")]
        public readonly ESRuntimeInstanceIndex<Item> ShotInstances = ShotInstanceIndex;

        [ShowInInspector, ReadOnly, LabelText("Building")]
        private static bool isBuilding;

        [NonSerialized]
        private ESRuntimeDataAssetLoadingService assetLoadingService;
        [NonSerialized]
        private ESAssetScope consumerResidentAssetScope;
        [NonSerialized]
        private ESAssetScope consumerGameCoreAssetScope;
        [NonSerialized]
        private ESGlobalResSetting activeReleaseSettings;
        [NonSerialized]
        private ESRuntimeReleaseDownloadResult activeReleaseResult;
        [NonSerialized]
        private readonly SemaphoreSlim onDemandReleaseGate = new SemaphoreSlim(1, 1);
        [NonSerialized]
        private readonly HashSet<string> activeConsumerIds = new HashSet<string>(StringComparer.Ordinal);
        [NonSerialized]
        private readonly HashSet<string> activeLibraryKeys = new HashSet<string>(StringComparer.Ordinal);

        public ESConsumerResidentAssetPreloadReport LastResidentAssetPreloadReport { get; private set; }

        public bool IsBuilding => isBuilding;
        public static bool IsBuildingStatic => isBuilding;
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public ESRuntimeDataAssetLoadingService ExistingAssetLoadingService => assetLoadingService;
        public ESRuntimeDataAssetLoadingService AssetLoadingService => assetLoadingService ??= new ESRuntimeDataAssetLoadingService();

        public void InitializeAssetLoading(ESGlobalAssetRuntimeMap manifest, IESRuntimeAssetBundleProvider provider, ESRuntimeRetryPolicy retryPolicy)
        {
            DisposeConsumerStartupAssets();
            AssetLoadingService.Initialize(manifest, provider, retryPolicy);
        }

        public void InitializeAssetLoading(IESAssetRuntimeProvider provider)
        {
            DisposeConsumerStartupAssets();
            AssetLoadingService.Initialize(provider);
        }

        public void InitializeAssetLoadingForRunMode(ESGlobalAssetRuntimeMap manifest, ESGlobalResSetting settings, ESRuntimeRetryPolicy retryPolicy)
        {
            DisposeConsumerStartupAssets();
            AssetLoadingService.Initialize(ESAssetRuntimeProviderFactory.Create(manifest, settings, retryPolicy));
        }
        /// <summary>Explicit entry for the current release pipeline.</summary>
        public async UniTask<ESRuntimeReleaseDownloadResult> InitializeAssetLoadingFromReleaseAsync(ESGlobalResSetting settings, CancellationToken cancellationToken = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            ESAssetRunMode runMode = ESAssetRunModeSession.Lock(settings);
            if (runMode != ESAssetRunMode.LocalBuild && runMode != ESAssetRunMode.HotUpdate)
                throw new InvalidOperationException($"\u53d1\u5e03\u8d44\u6e90\u94fe\u53ea\u652f\u6301 LocalBuild \u6216 HotUpdate\uff0c\u5f53\u524d\u6a21\u5f0f\u4e3a {runMode}\u3002");

            var result = await ESRuntimeReleaseBootstrap.InitializeAsync(settings, cancellationToken);
            await InitializeAssetLoadingFromReleaseResultAsync(settings, result, cancellationToken);
            return result;
        }

        public async UniTask InitializeAssetLoadingFromReleaseResultAsync(ESGlobalResSetting settings, ESRuntimeReleaseDownloadResult result, CancellationToken cancellationToken = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (result == null || result.RuntimeMap == null) throw new ArgumentNullException(nameof(result));
            bool releaseChanged = activeReleaseResult == null
                || !string.Equals(activeReleaseResult.ReleaseVersion, result.ReleaseVersion, StringComparison.Ordinal);
            DisposeConsumerStartupAssets();
            IESAssetRuntimeProvider provider = ESAssetRuntimeProviderFactory.Create(result.RuntimeMap, settings, ESRuntimeRetryPolicy.Default);
            await AssetLoadingService.InitializeAsync(
                provider,
                () => ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(result.Catalogs),
                cancellationToken);
            await PreloadConsumerResidentAssetsAsync(result.ResidentAssets, cancellationToken);
            await PreloadGameCoreAssetsAsync(result.GameCoreAssets, cancellationToken);

            // Only this point means the release is genuinely usable: its Provider is attached,
            // resident assets are retained, and GameCore tables have injected successfully.
            activeReleaseSettings = settings;
            activeReleaseResult = result;
            if (releaseChanged)
            {
                activeConsumerIds.Clear();
                activeLibraryKeys.Clear();
            }
            if (ESAssetRunModeSession.Lock(settings) == ESAssetRunMode.HotUpdate
                && !ESRuntimeReleaseDownloader.TryCommitLastKnownGood(settings, result.ReleaseVersion, out string fallbackError))
                Debug.LogWarning("[ESRes][Release] 无法提交离线回退版本：" + fallbackError);
        }

        /// <summary>
        /// Ensures one declared Consumer and all of its dependencies are downloaded and active.
        /// It is the business-facing on-demand package API; map merging, Provider replacement,
        /// code package initialization and Scope/Plan recovery remain framework responsibilities.
        /// Call this at a loading screen or another resource safe point.
        /// </summary>
        public async UniTask EnsureConsumerAvailableAsync(string consumerId, CancellationToken cancellationToken = default)
        {
            string key = NormalizeOnDemandId(consumerId, nameof(consumerId));
            await onDemandReleaseGate.WaitAsync(cancellationToken);
            try
            {
                EnsureOnDemandReleaseReady();
                if (activeConsumerIds.Contains(key))
                    return;
                var downloader = new ESRuntimeReleaseDownloader(activeReleaseSettings, ESAssetRunModeSession.Lock(activeReleaseSettings));
                ESRuntimeReleaseDownloadResult addition = await downloader.DownloadConsumerAsync(key, cancellationToken);
                await ActivateReleaseAdditionAsync(addition, cancellationToken);
                activeConsumerIds.Add(key);
            }
            finally { onDemandReleaseGate.Release(); }
        }

        /// <summary>Ensures one Library declared by a Consumer is downloaded and active without
        /// replacing any already active Consumer/Library content.</summary>
        public async UniTask EnsureLibraryAvailableAsync(string consumerId, string libraryFolder, CancellationToken cancellationToken = default)
        {
            string consumerKey = NormalizeOnDemandId(consumerId, nameof(consumerId));
            string libraryKey = NormalizeOnDemandId(libraryFolder, nameof(libraryFolder));
            string activeKey = consumerKey + "/" + libraryKey;
            await onDemandReleaseGate.WaitAsync(cancellationToken);
            try
            {
                EnsureOnDemandReleaseReady();
                if (activeLibraryKeys.Contains(activeKey))
                    return;
                var downloader = new ESRuntimeReleaseDownloader(activeReleaseSettings, ESAssetRunModeSession.Lock(activeReleaseSettings));
                ESRuntimeReleaseDownloadResult addition = await downloader.DownloadLibraryAsync(consumerKey, libraryKey, cancellationToken);
                await ActivateReleaseAdditionAsync(addition, cancellationToken);
                activeLibraryKeys.Add(activeKey);
            }
            finally { onDemandReleaseGate.Release(); }
        }

        /// <summary>Content Info entry point. The binding's Consumer SO is editor authority only;
        /// Player execution uses its baked ID and a caller-owned lifecycle Scope.</summary>
        public async UniTask EnterContentResourcesAsync(ESContentResourceBinding binding, ESAssetScope lifetimeScope, CancellationToken cancellationToken = default)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (lifetimeScope == null || lifetimeScope.IsDisposed) throw new ObjectDisposedException(nameof(lifetimeScope));
            if (binding.RequiresConsumer && !binding.HasBakedConsumerId)
                throw new InvalidOperationException("[ESRes][Content] Consumer 配置尚未烘焙，请重新执行资源烘焙。");
            if (binding.HasBakedConsumerId)
                await EnsureConsumerAvailableAsync(binding.BakedConsumerId, cancellationToken);
            if (binding.ActivePlan != null)
                await ESGameManager.ResourcePlans.ApplyAsync(binding.ActivePlan, lifetimeScope, cancellationToken);
        }

        /// <summary>Returns the active Plan retain associated with one content lifecycle Scope.
        /// Downloaded Consumer files are intentionally left in cache for later reuse.</summary>
        public async UniTask LeaveContentResourcesAsync(ESContentResourceBinding binding, ESAssetScope lifetimeScope, CancellationToken cancellationToken = default)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (lifetimeScope == null || lifetimeScope.IsDisposed) return;
            if (binding.ActivePlan != null)
                await ESGameManager.ResourcePlans.ReleaseAsync(binding.ActivePlan, lifetimeScope, cancellationToken);
        }

        /// <summary>Optional exit resources always use a separate transition Scope.</summary>
        public async UniTask EnterExitTransitionResourcesAsync(ESContentResourceBinding binding, ESAssetScope transitionScope, CancellationToken cancellationToken = default)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (transitionScope == null || transitionScope.IsDisposed) throw new ObjectDisposedException(nameof(transitionScope));
            if (binding.ExitTransitionPlan != null)
                await ESGameManager.ResourcePlans.ApplyAsync(binding.ExitTransitionPlan, transitionScope, cancellationToken);
        }

        private void EnsureOnDemandReleaseReady()
        {
            if (activeReleaseSettings == null || activeReleaseResult == null || activeReleaseResult.RuntimeMap == null)
                throw new InvalidOperationException("[ESRes][OnDemand] 当前尚未通过新版 Release Bootstrap 初始化，不能增量激活 Consumer/Library。");
        }

        private static string NormalizeOnDemandId(string value, string parameterName)
        {
            string normalized = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("资源标识不能为空。", parameterName);
            return normalized;
        }

        private async UniTask ActivateReleaseAdditionAsync(ESRuntimeReleaseDownloadResult addition, CancellationToken cancellationToken)
        {
            if (addition == null || addition.RuntimeMap == null)
                throw new InvalidOperationException("[ESRes][OnDemand] 下载结果缺少 RuntimeMap。");
            if (!string.Equals(activeReleaseResult.ReleaseVersion, addition.ReleaseVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("[ESRes][OnDemand] 下载期间发布版本已变化；请在下一个安全点重新执行完整 Bootstrap。");

            // A changed code hash deliberately throws here: HybridCLR cannot replace an already
            // loaded assembly in-process, so the user gets the same explicit restart boundary as boot.
            await ESRuntimeReleaseBootstrap.InitializeAdditionalCodePackagesAsync(addition.DownloadedCodePackages, cancellationToken);
            ESRuntimeReleaseDownloadResult merged = ESRuntimeReleaseDownloadResult.Merge(activeReleaseResult, addition);
            await InitializeAssetLoadingFromReleaseResultAsync(activeReleaseSettings, merged, cancellationToken);
        }

        /// <summary>Consumer 启动常驻资产在 GameCore 注入前加载；由模块持有到资源系统重置。</summary>
        public async UniTask<ESConsumerResidentAssetPreloadReport> PreloadConsumerResidentAssetsAsync(
            IEnumerable<ESRuntimeConsumerResidentAssetReference> assets, CancellationToken cancellationToken = default)
        {
            if (!AssetLoadingService.IsInitialized)
                throw new InvalidOperationException("[ESRes][Resident] 必须先初始化 Asset Provider。");

            consumerResidentAssetScope?.Dispose();
            consumerResidentAssetScope = ESAssets.CreateScope();
            var report = new ESConsumerResidentAssetPreloadReport();
            var identities = new HashSet<ESAssetIdentity>();
            try
            {
                foreach (ESRuntimeConsumerResidentAssetReference entry in assets ?? Array.Empty<ESRuntimeConsumerResidentAssetReference>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry == null || !entry.IsValid || !identities.Add(new ESAssetIdentity(entry.guid, entry.localFileId)))
                    {
                        report.skippedCount++;
                        continue;
                    }

                    report.requestedCount++;
                    try
                    {
                        var refer = new ESAssetReferUnityObject();
                        refer.InitializeGeneratedReference(entry.guid, entry.localFileId, ESAssetReferKind.Other, 0, string.Empty);
                        await consumerResidentAssetScope.LoadAsync(refer, cancellationToken);
                        report.loadedCount++;
                    }
                    catch (Exception exception)
                    {
                        string message = "[ESRes][Resident] 启动常驻资产加载失败：GUID=" + entry.guid
                            + ", LocalFileId=" + entry.localFileId + ", Error=" + exception.Message;
                        report.errors.Add(message);
                        Debug.LogError(message);
                        throw;
                    }
                }
                LastResidentAssetPreloadReport = report;
                return report;
            }
            catch
            {
                DisposeConsumerResidentAssets();
                throw;
            }
        }

        private void DisposeConsumerResidentAssets()
        {
            consumerResidentAssetScope?.Dispose();
            consumerResidentAssetScope = null;
            LastResidentAssetPreloadReport = null;
        }

        private void DisposeConsumerStartupAssets()
        {
            DisposeConsumerGameCoreAssets();
            DisposeConsumerResidentAssets();
        }

        private void DisposeConsumerGameCoreAssets()
        {
            ESRuntimeDataGameCore.ResetForResourceTransition();
            consumerGameCoreAssetScope?.Dispose();
            consumerGameCoreAssetScope = null;
        }

        /// <summary>
        /// Preloads Consumer GameCore assets after the runtime provider is ready.
        /// Each IGameCoreSO injects its own target GameCore table.
        /// </summary>
        public async UniTask<ESGameCoreAssetPreloadReport> PreloadGameCoreAssetsAsync(IEnumerable<ESRuntimeConsumerGameCoreReference> assets, CancellationToken cancellationToken = default)
        {
            if (!AssetLoadingService.IsInitialized) throw new InvalidOperationException("\u5fc5\u987b\u5148\u521d\u59cb\u5316\u65b0\u7248 Asset Provider\uff0c\u624d\u80fd\u9884\u70ed GameCore \u8d44\u4ea7\u3002");
            // GameCore 归属于当前 Consumer，而非默认全局 residentScope。重新加载 Consumer
            // 时整体清表、释放旧持有后再注入，避免跨 Provider 保留旧 SO。
            DisposeConsumerGameCoreAssets();
            consumerGameCoreAssetScope = ESAssets.CreateScope();
            var report = new ESGameCoreAssetPreloadReport();
            var identities = new HashSet<ESAssetIdentity>();
            try
            {
                foreach (ESRuntimeConsumerGameCoreReference entry in OrderGameCoreAssets(assets))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry == null || !entry.IsValid || !identities.Add(new ESAssetIdentity(entry.guid, entry.localFileId)))
                    {
                        report.skippedCount++;
                        continue;
                    }

                    report.requestedCount++;
                    try
                    {
                        var refer = new ESAssetReferScriptableObject();
                        refer.InitializeGeneratedReference(entry.guid, entry.localFileId, ESAssetReferKind.ScriptableObject, 0, string.Empty);
                        ScriptableObject asset = await consumerGameCoreAssetScope.LoadAsync(refer, cancellationToken);
                        if (!(asset is IGameCoreSO gameCore))
                            throw new InvalidOperationException("Consumer GameCore \u8d44\u4ea7\u672a\u5b9e\u73b0 IGameCoreSO\uff1a" + entry.guid);
                        gameCore.InjectGameCoreTables();
                        report.loadedCount++;
                    }
                    catch (Exception exception)
                    {
                        report.failedCount++;
                        report.errors.Add(entry.guid + ":" + entry.localFileId + " : " + exception.Message);
                        throw;
                    }
                }

                // ResourcePlanInfo is an IGameCoreSO: all plans have now registered their
                // target index. On first boot this enters Global; after a Provider replacement
                // it restores exactly the targets that were active before the transition.
                await AssetLoadingService.RestoreResourcePlansAfterGameCoreAsync(cancellationToken);
                return report;
            }
            catch
            {
                // 包括循环开始前的取消、依赖排序失败与任一注入失败；不能留下半张表。
                DisposeConsumerGameCoreAssets();
                throw;
            }
        }

        private static IReadOnlyList<ESRuntimeConsumerGameCoreReference> OrderGameCoreAssets(IEnumerable<ESRuntimeConsumerGameCoreReference> source)
        {
            var byId = new Dictionary<ESAssetIdentity, ESRuntimeConsumerGameCoreReference>();
            foreach (ESRuntimeConsumerGameCoreReference entry in source ?? Array.Empty<ESRuntimeConsumerGameCoreReference>())
                if (entry != null && entry.IsValid)
                    byId[new ESAssetIdentity(entry.guid, entry.localFileId)] = entry;

            var ordered = new List<ESRuntimeConsumerGameCoreReference>(byId.Count);
            var visited = new HashSet<ESAssetIdentity>();
            var visiting = new HashSet<ESAssetIdentity>();
            void Visit(ESAssetIdentity id)
            {
                if (visited.Contains(id)) return;
                if (!byId.TryGetValue(id, out ESRuntimeConsumerGameCoreReference entry))
                    throw new InvalidOperationException("Consumer \u7f3a\u5c11 GameCore \u4f9d\u8d56\uff1a" + id);
                if (!visiting.Add(id))
                    throw new InvalidOperationException("Consumer GameCore \u5b58\u5728\u5faa\u73af\u4f9d\u8d56\uff1a" + id);
                foreach (ESRuntimeConsumerGameCoreDependencyReference dependency in entry.dependencies ?? new List<ESRuntimeConsumerGameCoreDependencyReference>())
                {
                    if (dependency == null || !dependency.IsValid)
                        throw new InvalidOperationException("GameCore \u5305\u542b\u65e0\u6548\u4f9d\u8d56\uff1a" + id);
                    Visit(new ESAssetIdentity(dependency.guid, dependency.localFileId));
                }
                visiting.Remove(id);
                visited.Add(id);
                ordered.Add(entry);
            }

            foreach (ESAssetIdentity id in byId.Keys.OrderBy(item => item.Guid, StringComparer.Ordinal).ThenBy(item => item.LocalFileId))
                Visit(id);
            return ordered;
        }
        public void DisposeAssetLoading()
        {
            DisposeConsumerStartupAssets();
            assetLoadingService?.Dispose();
            assetLoadingService = null;
        }

        public override void OnDestroy()
        {
            DisposeAssetLoading();
            base.OnDestroy();
        }

        public void BeginBuild(bool clear = false)
        {
            BeginBuildStatic(clear);
        }

        public void EndBuild()
        {
            EndBuildStatic();
        }

        public static void BeginBuildStatic(bool clear = false)
        {
            if (isBuilding)
            {
                if (clear)
                    throw new InvalidOperationException("ESRuntimeDataModule is already building. Clear rebuild cannot be nested.");

                return;
            }

            isBuilding = true;
            ESRuntimeDataGameCore.BeginBuild(clear);
            ESRuntimeDataAsset.BeginBuild(clear);
        }

        public static void EndBuildStatic()
        {
            if (!isBuilding)
                return;

            try
            {
                ESRuntimeDataGameCore.EndBuild();
                ESRuntimeDataAsset.EndBuild();
            }
            finally
            {
                isBuilding = false;
            }
        }

        public bool TryGetBuff(int runtimeKey, out ESBuffRuntimeData data) => Buffs.TryGet(runtimeKey, out data);
        public bool TryGetShot(int runtimeKey, out ESShotRuntimeData data) => Shots.TryGet(runtimeKey, out data);
        public bool TryGetMonster(int runtimeKey, out ESMonsterRuntimeData data) => Monsters.TryGet(runtimeKey, out data);
        public bool TryGetNpc(int runtimeKey, out ESNpcRuntimeData data) => Npcs.TryGet(runtimeKey, out data);
        public bool TryGetWeapon(int runtimeKey, out ESWeaponRuntimeData data) => Weapons.TryGet(runtimeKey, out data);
        public bool TryGetSkill(int runtimeKey, out ESSkillRuntimeData data) => Skills.TryGet(runtimeKey, out data);

        public bool TryGetBuff(ESBuffEnumKey enumKey, out ESBuffRuntimeData data) => Buffs.TryGet((int)enumKey, out data);
        public bool TryGetShot(ESShotEnumKey enumKey, out ESShotRuntimeData data) => Shots.TryGet((int)enumKey, out data);
        public bool TryGetMonster(ESMonsterEnumKey enumKey, out ESMonsterRuntimeData data) => Monsters.TryGet((int)enumKey, out data);
        public bool TryGetNpc(ESNpcEnumKey enumKey, out ESNpcRuntimeData data) => Npcs.TryGet((int)enumKey, out data);
        public bool TryGetWeapon(ESWeaponEnumKey enumKey, out ESWeaponRuntimeData data) => Weapons.TryGet((int)enumKey, out data);
        public bool TryGetSkill(ESSkillEnumKey enumKey, out ESSkillRuntimeData data) => Skills.TryGet((int)enumKey, out data);

        public bool TryGetBuff(string stringKey, out ESBuffRuntimeData data) => TryGetByString(Buffs, stringKey, out data);
        public bool TryGetShot(string stringKey, out ESShotRuntimeData data) => TryGetByString(Shots, stringKey, out data);
        public bool TryGetMonster(string stringKey, out ESMonsterRuntimeData data) => TryGetByString(Monsters, stringKey, out data);
        public bool TryGetNpc(string stringKey, out ESNpcRuntimeData data) => TryGetByString(Npcs, stringKey, out data);
        public bool TryGetWeapon(string stringKey, out ESWeaponRuntimeData data) => TryGetByString(Weapons, stringKey, out data);
        public bool TryGetSkill(string stringKey, out ESSkillRuntimeData data) => TryGetByString(Skills, stringKey, out data);

        public string GetConflictReport()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(512);
            AppendConflictReport(builder, "Buff", Buffs);
            AppendConflictReport(builder, "Shot", Shots);
            AppendConflictReport(builder, "Monster", Monsters);
            AppendConflictReport(builder, "Npc", Npcs);
            AppendConflictReport(builder, "Weapon", Weapons);
            AppendConflictReport(builder, "Skill", Skills);
            return builder.ToString();
        }

        private static bool TryGetByString<TData>(ESConfigKeyTable<TData> table, string stringKey, out TData data)
            where TData : class
        {
            if (table != null && table.TryGetRuntimeKey(stringKey, out int runtimeKey))
                return table.TryGet(runtimeKey, out data);

            data = null;
            return false;
        }

        private static void AppendConflictReport<TData>(System.Text.StringBuilder builder, string tableName, ESConfigKeyTable<TData> table)
            where TData : class
        {
            if (table == null || table.ConflictCount == 0)
                return;

            builder.Append("[").Append(tableName).Append("]").AppendLine();
            builder.Append(table.GetConflictReport());
        }
    }
}
