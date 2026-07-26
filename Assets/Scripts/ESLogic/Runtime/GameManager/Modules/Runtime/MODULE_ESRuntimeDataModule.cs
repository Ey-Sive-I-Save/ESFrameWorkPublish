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
        public static readonly ESConfigKeyTable<ESBuffRuntimeData> Buffs = new ESConfigKeyTable<ESBuffRuntimeData>(128);
        public static readonly ESConfigKeyTable<ESShotRuntimeData> Shots = new ESConfigKeyTable<ESShotRuntimeData>(128);
        public static readonly ESConfigKeyTable<ESMonsterRuntimeData> Monsters = new ESConfigKeyTable<ESMonsterRuntimeData>(128);
        public static readonly ESConfigKeyTable<ESNpcRuntimeData> Npcs = new ESConfigKeyTable<ESNpcRuntimeData>(128);
        public static readonly ESConfigKeyTable<ESWeaponRuntimeData> Weapons = new ESConfigKeyTable<ESWeaponRuntimeData>(64);
        public static readonly ESConfigKeyTable<ESSkillRuntimeData> Skills = new ESConfigKeyTable<ESSkillRuntimeData>(128);

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
        public static readonly ESAssetConfigKeyTable<ESAssetReferPrefabConfigData, GameObject> Prefabs = new ESAssetConfigKeyTable<ESAssetReferPrefabConfigData, GameObject>(256);
        public static readonly ESAssetConfigKeyTable<ESAssetReferSpriteConfigData, Sprite> Sprites = new ESAssetConfigKeyTable<ESAssetReferSpriteConfigData, Sprite>(256);
        public static readonly ESAssetConfigKeyTable<ESAssetReferAudioClipConfigData, AudioClip> AudioClips = new ESAssetConfigKeyTable<ESAssetReferAudioClipConfigData, AudioClip>(256);
        public static readonly ESAssetConfigKeyTable<ESAssetReferAnimationClipConfigData, AnimationClip> AnimationClips = new ESAssetConfigKeyTable<ESAssetReferAnimationClipConfigData, AnimationClip>(256);
        public static readonly ESAssetConfigKeyTable<ESAssetReferAnimatorControllerConfigData, RuntimeAnimatorController> AnimatorControllers = new ESAssetConfigKeyTable<ESAssetReferAnimatorControllerConfigData, RuntimeAnimatorController>(128);
        public static readonly ESAssetConfigKeyTable<ESAssetReferMaterialConfigData, Material> Materials = new ESAssetConfigKeyTable<ESAssetReferMaterialConfigData, Material>(256);
        public static readonly ESAssetConfigKeyTable<ESAssetReferMeshConfigData, Mesh> Meshes = new ESAssetConfigKeyTable<ESAssetReferMeshConfigData, Mesh>(256);
        public static readonly ESAssetConfigKeyTable<ESAssetReferSceneConfigData, UnityEngine.Object> Scenes = new ESAssetConfigKeyTable<ESAssetReferSceneConfigData, UnityEngine.Object>(64);
        public static readonly ESAssetConfigKeyTable<ESAssetReferTextureConfigData, Texture> Textures = new ESAssetConfigKeyTable<ESAssetReferTextureConfigData, Texture>(128);
        public static readonly ESAssetConfigKeyTable<ESAssetReferTexture2DConfigData, Texture2D> Texture2Ds = new ESAssetConfigKeyTable<ESAssetReferTexture2DConfigData, Texture2D>(128);
        public static readonly ESAssetConfigKeyTable<ESAssetReferSpriteAtlasConfigData, UnityEngine.U2D.SpriteAtlas> SpriteAtlases = new ESAssetConfigKeyTable<ESAssetReferSpriteAtlasConfigData, UnityEngine.U2D.SpriteAtlas>(64);
        public static readonly ESAssetConfigKeyTable<ESAssetReferAvatarConfigData, Avatar> Avatars = new ESAssetConfigKeyTable<ESAssetReferAvatarConfigData, Avatar>(64);
        public static readonly ESAssetConfigKeyTable<ESAssetReferPlayableAssetConfigData, UnityEngine.Playables.PlayableAsset> PlayableAssets = new ESAssetConfigKeyTable<ESAssetReferPlayableAssetConfigData, UnityEngine.Playables.PlayableAsset>(64);
        public static readonly ESAssetConfigKeyTable<ESAssetReferScriptableObjectConfigData, ScriptableObject> ScriptableObjects = new ESAssetConfigKeyTable<ESAssetReferScriptableObjectConfigData, ScriptableObject>(128);
        public static readonly ESAssetConfigKeyTable<ESAssetReferTimelineAssetConfigData, UnityEngine.Object> TimelineAssets = new ESAssetConfigKeyTable<ESAssetReferTimelineAssetConfigData, UnityEngine.Object>(64);
        public static readonly ESAssetConfigKeyTable<ESAssetReferVideoClipConfigData, UnityEngine.Video.VideoClip> VideoClips = new ESAssetConfigKeyTable<ESAssetReferVideoClipConfigData, UnityEngine.Video.VideoClip>(64);
        public static readonly ESAssetConfigKeyTable<ESAssetReferTerrainDataConfigData, TerrainData> TerrainDatas = new ESAssetConfigKeyTable<ESAssetReferTerrainDataConfigData, TerrainData>(32);

        public static void BeginBuild(bool clear)
        {
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
        [MenuItem("ES/Asset Registry/Rebuild EditorConfig QueryTable From AssetLibraries")]
        public static void MenuRebuildEditorConfigQueryTableFromLibraries()
        {
            ESAssetAutoRegisterReport report = RebuildEditorConfigQueryTableFromLibraries(true, true);
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        public static ESAssetAutoRegisterReport RebuildEditorConfigQueryTableFromLibraries(bool rebuildAssetConfigTables = true, bool clearBeforeBuild = true)
        {
            ESAssetAutoRegisterReport report = new ESAssetAutoRegisterReport();
            List<ESAssetLibrary> indexedLibraries = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibrary>() ?? new List<ESAssetLibrary>(0);
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
                    return RegisterPrefab(CreateAssetDataFromPage<ESAssetReferPrefabConfigData, ESAssetReferPrefabConfigKey>(page, new ESAssetReferPrefabConfigData(), new ESAssetReferPrefabConfigKey()));
                case ESAssetReferKind.Scene:
                    return RegisterScene(CreateAssetDataFromPage<ESAssetReferSceneConfigData, ESAssetReferSceneConfigKey>(page, new ESAssetReferSceneConfigData(), new ESAssetReferSceneConfigKey()));
                case ESAssetReferKind.Sprite:
                    return RegisterSprite(CreateAssetDataFromPage<ESAssetReferSpriteConfigData, ESAssetReferSpriteConfigKey>(page, new ESAssetReferSpriteConfigData(), new ESAssetReferSpriteConfigKey()));
                case ESAssetReferKind.Texture2D:
                    return RegisterTexture2D(CreateAssetDataFromPage<ESAssetReferTexture2DConfigData, ESAssetReferTexture2DConfigKey>(page, new ESAssetReferTexture2DConfigData(), new ESAssetReferTexture2DConfigKey()));
                case ESAssetReferKind.Texture:
                    return RegisterTexture(CreateAssetDataFromPage<ESAssetReferTextureConfigData, ESAssetReferTextureConfigKey>(page, new ESAssetReferTextureConfigData(), new ESAssetReferTextureConfigKey()));
                case ESAssetReferKind.SpriteAtlas:
                    return RegisterSpriteAtlas(CreateAssetDataFromPage<ESAssetReferSpriteAtlasConfigData, ESAssetReferSpriteAtlasConfigKey>(page, new ESAssetReferSpriteAtlasConfigData(), new ESAssetReferSpriteAtlasConfigKey()));
                case ESAssetReferKind.Material:
                    return RegisterMaterial(CreateAssetDataFromPage<ESAssetReferMaterialConfigData, ESAssetReferMaterialConfigKey>(page, new ESAssetReferMaterialConfigData(), new ESAssetReferMaterialConfigKey()));
                case ESAssetReferKind.Mesh:
                    return RegisterMesh(CreateAssetDataFromPage<ESAssetReferMeshConfigData, ESAssetReferMeshConfigKey>(page, new ESAssetReferMeshConfigData(), new ESAssetReferMeshConfigKey()));
                case ESAssetReferKind.AnimationClip:
                    return RegisterAnimationClip(CreateAssetDataFromPage<ESAssetReferAnimationClipConfigData, ESAssetReferAnimationClipConfigKey>(page, new ESAssetReferAnimationClipConfigData(), new ESAssetReferAnimationClipConfigKey()));
                case ESAssetReferKind.AnimatorController:
                    return RegisterAnimatorController(CreateAssetDataFromPage<ESAssetReferAnimatorControllerConfigData, ESAssetReferAnimatorControllerConfigKey>(page, new ESAssetReferAnimatorControllerConfigData(), new ESAssetReferAnimatorControllerConfigKey()));
                case ESAssetReferKind.Avatar:
                    return RegisterAvatar(CreateAssetDataFromPage<ESAssetReferAvatarConfigData, ESAssetReferAvatarConfigKey>(page, new ESAssetReferAvatarConfigData(), new ESAssetReferAvatarConfigKey()));
                case ESAssetReferKind.AudioClip:
                    return RegisterAudioClip(CreateAssetDataFromPage<ESAssetReferAudioClipConfigData, ESAssetReferAudioClipConfigKey>(page, new ESAssetReferAudioClipConfigData(), new ESAssetReferAudioClipConfigKey()));
                case ESAssetReferKind.VideoClip:
                    return RegisterVideoClip(CreateAssetDataFromPage<ESAssetReferVideoClipConfigData, ESAssetReferVideoClipConfigKey>(page, new ESAssetReferVideoClipConfigData(), new ESAssetReferVideoClipConfigKey()));
                case ESAssetReferKind.TimelineAsset:
                    return RegisterTimelineAsset(CreateAssetDataFromPage<ESAssetReferTimelineAssetConfigData, ESAssetReferTimelineAssetConfigKey>(page, new ESAssetReferTimelineAssetConfigData(), new ESAssetReferTimelineAssetConfigKey()));
                case ESAssetReferKind.PlayableAsset:
                    return RegisterPlayableAsset(CreateAssetDataFromPage<ESAssetReferPlayableAssetConfigData, ESAssetReferPlayableAssetConfigKey>(page, new ESAssetReferPlayableAssetConfigData(), new ESAssetReferPlayableAssetConfigKey()));
                case ESAssetReferKind.ScriptableObject:
                    return RegisterScriptableObject(CreateAssetDataFromPage<ESAssetReferScriptableObjectConfigData, ESAssetReferScriptableObjectConfigKey>(page, new ESAssetReferScriptableObjectConfigData(), new ESAssetReferScriptableObjectConfigKey()));
                case ESAssetReferKind.TerrainData:
                    return RegisterTerrainData(CreateAssetDataFromPage<ESAssetReferTerrainDataConfigData, ESAssetReferTerrainDataConfigKey>(page, new ESAssetReferTerrainDataConfigData(), new ESAssetReferTerrainDataConfigKey()));
                default:
                    return false;
            }
        }

        private static TData CreateAssetDataFromPage<TData, TKey>(ESAssetPage page, TData data, TKey key)
            where TKey : IESConfigKey
        {
            Type keyType = key.GetType();
            var enumKeyField = keyType.GetField("enumKey");
            if (enumKeyField != null && enumKeyField.FieldType.IsEnum)
                enumKeyField.SetValue(key, Enum.ToObject(enumKeyField.FieldType, page.EnumKey));
            keyType.GetField("stringKey")?.SetValue(key, page.EffectiveStringKey);
            keyType.GetMethod("SetAssetAuthority")?.Invoke(key, new object[] { page.AssetGuid, page.LocalFileId, page.AssetTypeName, page.AssetPath });

            Type dataType = data.GetType();
            dataType.GetField("keyName")?.SetValue(data, page.EffectiveStringKey);
            dataType.GetField("displayName")?.SetValue(data, page.Name);
            dataType.GetField("sourcePackage")?.SetValue(data, page.SourceLibrary);
            dataType.GetField("key")?.SetValue(data, key);
            var setAssetIdentity = dataType.GetMethod("SetAssetIdentity");
            setAssetIdentity?.Invoke(data, new object[] { page.AssetGuid, page.LocalFileId });
            return data;
        }
#endif

        public static int RebuildAssetConfigTablesFromCatalogs(IReadOnlyList<ESRuntimeCatalog> catalogs)
        {
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

        private static bool RegisterCatalogEntryAsAssetConfigData(ESRuntimeCatalogEntry entry)
        {
            if (entry == null || !entry.isBusinessAsset || entry.identity == null || !entry.identity.IsValid || !Enum.TryParse(entry.kind, out ESAssetReferKind kind))
                return false;
            if (entry.enumKey == 0 && string.IsNullOrEmpty(entry.stringKey))
                throw new InvalidOperationException("Catalog \u4e1a\u52a1\u8d44\u4ea7\u7f3a\u5c11 EnumKey/StringKey\uff1a" + entry.identity.guid);

            switch (kind)
            {
                case ESAssetReferKind.Prefab: return RegisterPrefab(CreateAssetDataFromCatalog<ESAssetReferPrefabConfigData, ESAssetReferPrefabConfigKey>(entry, new ESAssetReferPrefabConfigData(), new ESAssetReferPrefabConfigKey()));
                case ESAssetReferKind.Scene: return RegisterScene(CreateAssetDataFromCatalog<ESAssetReferSceneConfigData, ESAssetReferSceneConfigKey>(entry, new ESAssetReferSceneConfigData(), new ESAssetReferSceneConfigKey()));
                case ESAssetReferKind.Sprite: return RegisterSprite(CreateAssetDataFromCatalog<ESAssetReferSpriteConfigData, ESAssetReferSpriteConfigKey>(entry, new ESAssetReferSpriteConfigData(), new ESAssetReferSpriteConfigKey()));
                case ESAssetReferKind.Texture2D: return RegisterTexture2D(CreateAssetDataFromCatalog<ESAssetReferTexture2DConfigData, ESAssetReferTexture2DConfigKey>(entry, new ESAssetReferTexture2DConfigData(), new ESAssetReferTexture2DConfigKey()));
                case ESAssetReferKind.Texture: return RegisterTexture(CreateAssetDataFromCatalog<ESAssetReferTextureConfigData, ESAssetReferTextureConfigKey>(entry, new ESAssetReferTextureConfigData(), new ESAssetReferTextureConfigKey()));
                case ESAssetReferKind.SpriteAtlas: return RegisterSpriteAtlas(CreateAssetDataFromCatalog<ESAssetReferSpriteAtlasConfigData, ESAssetReferSpriteAtlasConfigKey>(entry, new ESAssetReferSpriteAtlasConfigData(), new ESAssetReferSpriteAtlasConfigKey()));
                case ESAssetReferKind.Material: return RegisterMaterial(CreateAssetDataFromCatalog<ESAssetReferMaterialConfigData, ESAssetReferMaterialConfigKey>(entry, new ESAssetReferMaterialConfigData(), new ESAssetReferMaterialConfigKey()));
                case ESAssetReferKind.Mesh: return RegisterMesh(CreateAssetDataFromCatalog<ESAssetReferMeshConfigData, ESAssetReferMeshConfigKey>(entry, new ESAssetReferMeshConfigData(), new ESAssetReferMeshConfigKey()));
                case ESAssetReferKind.AnimationClip: return RegisterAnimationClip(CreateAssetDataFromCatalog<ESAssetReferAnimationClipConfigData, ESAssetReferAnimationClipConfigKey>(entry, new ESAssetReferAnimationClipConfigData(), new ESAssetReferAnimationClipConfigKey()));
                case ESAssetReferKind.AnimatorController: return RegisterAnimatorController(CreateAssetDataFromCatalog<ESAssetReferAnimatorControllerConfigData, ESAssetReferAnimatorControllerConfigKey>(entry, new ESAssetReferAnimatorControllerConfigData(), new ESAssetReferAnimatorControllerConfigKey()));
                case ESAssetReferKind.Avatar: return RegisterAvatar(CreateAssetDataFromCatalog<ESAssetReferAvatarConfigData, ESAssetReferAvatarConfigKey>(entry, new ESAssetReferAvatarConfigData(), new ESAssetReferAvatarConfigKey()));
                case ESAssetReferKind.AudioClip: return RegisterAudioClip(CreateAssetDataFromCatalog<ESAssetReferAudioClipConfigData, ESAssetReferAudioClipConfigKey>(entry, new ESAssetReferAudioClipConfigData(), new ESAssetReferAudioClipConfigKey()));
                case ESAssetReferKind.VideoClip: return RegisterVideoClip(CreateAssetDataFromCatalog<ESAssetReferVideoClipConfigData, ESAssetReferVideoClipConfigKey>(entry, new ESAssetReferVideoClipConfigData(), new ESAssetReferVideoClipConfigKey()));
                case ESAssetReferKind.TimelineAsset: return RegisterTimelineAsset(CreateAssetDataFromCatalog<ESAssetReferTimelineAssetConfigData, ESAssetReferTimelineAssetConfigKey>(entry, new ESAssetReferTimelineAssetConfigData(), new ESAssetReferTimelineAssetConfigKey()));
                case ESAssetReferKind.PlayableAsset: return RegisterPlayableAsset(CreateAssetDataFromCatalog<ESAssetReferPlayableAssetConfigData, ESAssetReferPlayableAssetConfigKey>(entry, new ESAssetReferPlayableAssetConfigData(), new ESAssetReferPlayableAssetConfigKey()));
                case ESAssetReferKind.ScriptableObject: return RegisterScriptableObject(CreateAssetDataFromCatalog<ESAssetReferScriptableObjectConfigData, ESAssetReferScriptableObjectConfigKey>(entry, new ESAssetReferScriptableObjectConfigData(), new ESAssetReferScriptableObjectConfigKey()));
                case ESAssetReferKind.TerrainData: return RegisterTerrainData(CreateAssetDataFromCatalog<ESAssetReferTerrainDataConfigData, ESAssetReferTerrainDataConfigKey>(entry, new ESAssetReferTerrainDataConfigData(), new ESAssetReferTerrainDataConfigKey()));
                default: return false;
            }
        }

        private static TData CreateAssetDataFromCatalog<TData, TKey>(ESRuntimeCatalogEntry entry, TData data, TKey key)
            where TKey : IESConfigKey
        {
            Type keyType = key.GetType();
            var enumKeyField = keyType.GetField("enumKey");
            if (enumKeyField != null && enumKeyField.FieldType.IsEnum)
                enumKeyField.SetValue(key, Enum.ToObject(enumKeyField.FieldType, entry.enumKey));
            keyType.GetField("stringKey")?.SetValue(key, entry.stringKey);
            keyType.GetMethod("SetAssetAuthority")?.Invoke(key, new object[] { entry.identity.guid, entry.identity.localFileId, entry.assetTypeName, null });

            Type dataType = data.GetType();
            dataType.GetField("keyName")?.SetValue(data, entry.stringKey);
            dataType.GetField("displayName")?.SetValue(data, entry.pageName);
            dataType.GetField("sourcePackage")?.SetValue(data, entry.libraryFolder);
            dataType.GetField("key")?.SetValue(data, key);
            dataType.GetMethod("SetAssetIdentity")?.Invoke(data, new object[] { entry.identity.guid, entry.identity.localFileId });
            return data;
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

            data.runtimeKey = Prefabs.Bake(data.key, data.keyName);
            return UpsertAssetData(Prefabs, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterSprite(ESAssetReferSpriteConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Sprites.Bake(data.key, data.keyName);
            return UpsertAssetData(Sprites, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterAudioClip(ESAssetReferAudioClipConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = AudioClips.Bake(data.key, data.keyName);
            return UpsertAssetData(AudioClips, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterAnimationClip(ESAssetReferAnimationClipConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = AnimationClips.Bake(data.key, data.keyName);
            return UpsertAssetData(AnimationClips, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterAnimatorController(ESAssetReferAnimatorControllerConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = AnimatorControllers.Bake(data.key, data.keyName);
            return UpsertAssetData(AnimatorControllers, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterMaterial(ESAssetReferMaterialConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Materials.Bake(data.key, data.keyName);
            return UpsertAssetData(Materials, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterMesh(ESAssetReferMeshConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Meshes.Bake(data.key, data.keyName);
            return UpsertAssetData(Meshes, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterScene(ESAssetReferSceneConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Scenes.Bake(data.key, data.keyName);
            return UpsertAssetData(Scenes, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterTexture(ESAssetReferTextureConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Textures.Bake(data.key, data.keyName);
            return UpsertAssetData(Textures, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterTexture2D(ESAssetReferTexture2DConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Texture2Ds.Bake(data.key, data.keyName);
            return UpsertAssetData(Texture2Ds, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterSpriteAtlas(ESAssetReferSpriteAtlasConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = SpriteAtlases.Bake(data.key, data.keyName);
            return UpsertAssetData(SpriteAtlases, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterAvatar(ESAssetReferAvatarConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = Avatars.Bake(data.key, data.keyName);
            return UpsertAssetData(Avatars, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterPlayableAsset(ESAssetReferPlayableAssetConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = PlayableAssets.Bake(data.key, data.keyName);
            return UpsertAssetData(PlayableAssets, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterScriptableObject(ESAssetReferScriptableObjectConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = ScriptableObjects.Bake(data.key, data.keyName);
            return UpsertAssetData(ScriptableObjects, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterTimelineAsset(ESAssetReferTimelineAssetConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = TimelineAssets.Bake(data.key, data.keyName);
            return UpsertAssetData(TimelineAssets, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterVideoClip(ESAssetReferVideoClipConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = VideoClips.Bake(data.key, data.keyName);
            return UpsertAssetData(VideoClips, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        public static bool RegisterTerrainData(ESAssetReferTerrainDataConfigData data)
        {
            if (data == null || data.key == null)
                return false;

            data.runtimeKey = TerrainDatas.Bake(data.key, data.keyName);
            return UpsertAssetData(TerrainDatas, data, data.runtimeKey, data.key.GetStringKey(data.keyName));
        }

        private static bool UpsertAssetData<TData>(ESConfigKeyTable<TData> table, TData data, int runtimeKey, string stringKey)
            where TData : class, IESAssetReferConfigData
        {
            // Asset EnumKey/StringKey are authoritative inside their typed table.
            // A duplicate must be rejected instead of silently replacing or falling back.
            return table.Register(runtimeKey, data, stringKey);
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
        public static readonly ESConfigKeyTable<ESBuffRuntimeData> BuffTable = ESRuntimeDataGameCore.Buffs;
        public static readonly ESConfigKeyTable<ESShotRuntimeData> ShotTable = ESRuntimeDataGameCore.Shots;
        public static readonly ESConfigKeyTable<ESMonsterRuntimeData> MonsterTable = ESRuntimeDataGameCore.Monsters;
        public static readonly ESConfigKeyTable<ESNpcRuntimeData> NpcTable = ESRuntimeDataGameCore.Npcs;
        public static readonly ESConfigKeyTable<ESWeaponRuntimeData> WeaponTable = ESRuntimeDataGameCore.Weapons;
        public static readonly ESConfigKeyTable<ESSkillRuntimeData> SkillTable = ESRuntimeDataGameCore.Skills;
        public static readonly ESRuntimeInstanceIndex<ESActiveBuffRuntime> BuffInstanceIndex = new ESRuntimeInstanceIndex<ESActiveBuffRuntime>(128);
        public static readonly ESRuntimeInstanceIndex<Item> ShotInstanceIndex = new ESRuntimeInstanceIndex<Item>(128);

        [ShowInInspector, ReadOnly, LabelText("Buff Table")]
        public readonly ESConfigKeyTable<ESBuffRuntimeData> Buffs = BuffTable;
        [ShowInInspector, ReadOnly, LabelText("\u98de\u884c\u7269\u8868")]
        public readonly ESConfigKeyTable<ESShotRuntimeData> Shots = ShotTable;

        [ShowInInspector, ReadOnly, LabelText("Monster Table")]
        public readonly ESConfigKeyTable<ESMonsterRuntimeData> Monsters = MonsterTable;

        [ShowInInspector, ReadOnly, LabelText("NPC Table")]
        public readonly ESConfigKeyTable<ESNpcRuntimeData> Npcs = NpcTable;

        [ShowInInspector, ReadOnly, LabelText("Weapon Table")]
        public readonly ESConfigKeyTable<ESWeaponRuntimeData> Weapons = WeaponTable;
        [ShowInInspector, ReadOnly, LabelText("\u6280\u80fd\u8868")]
        public readonly ESConfigKeyTable<ESSkillRuntimeData> Skills = SkillTable;
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

        public ESConsumerResidentAssetPreloadReport LastResidentAssetPreloadReport { get; private set; }

        public bool IsBuilding => isBuilding;
        public static bool IsBuildingStatic => isBuilding;
        public ESRuntimeDataAssetLoadingService AssetLoadingService => assetLoadingService ??= new ESRuntimeDataAssetLoadingService();

        public void InitializeAssetLoading(ESGlobalAssetRuntimeMap manifest, IESRuntimeAssetBundleProvider provider, ESRuntimeRetryPolicy retryPolicy)
        {
            DisposeConsumerResidentAssets();
            AssetLoadingService.Initialize(manifest, provider, retryPolicy);
        }

        public void InitializeAssetLoading(IESAssetRuntimeProvider provider)
        {
            DisposeConsumerResidentAssets();
            AssetLoadingService.Initialize(provider);
        }

        public void InitializeAssetLoadingForRunMode(ESGlobalAssetRuntimeMap manifest, ESGlobalResSetting settings, ESRuntimeRetryPolicy retryPolicy)
        {
            DisposeConsumerResidentAssets();
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
            ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(result.Catalogs);
            InitializeAssetLoadingForRunMode(result.RuntimeMap, settings, ESRuntimeRetryPolicy.Default);
            await PreloadConsumerResidentAssetsAsync(result.ResidentAssets, cancellationToken);
            await PreloadGameCoreAssetsAsync(result.GameCoreAssets, cancellationToken);
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

        /// <summary>
        /// Preloads Consumer GameCore assets after the runtime provider is ready.
        /// Each IGameCoreSO injects its own target GameCore table.
        /// </summary>
        public async UniTask<ESGameCoreAssetPreloadReport> PreloadGameCoreAssetsAsync(IEnumerable<ESRuntimeConsumerGameCoreReference> assets, CancellationToken cancellationToken = default)
        {
            if (!AssetLoadingService.IsInitialized) throw new InvalidOperationException("\u5fc5\u987b\u5148\u521d\u59cb\u5316\u65b0\u7248 Asset Provider\uff0c\u624d\u80fd\u9884\u70ed GameCore \u8d44\u4ea7\u3002");
            var report = new ESGameCoreAssetPreloadReport();
            var identities = new HashSet<ESAssetIdentity>();
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
                    ScriptableObject asset = await ESAssets.LoadAsync(refer, cancellationToken);
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
            return report;
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
        /// <summary>Compatibility entry; new boot flow uses Consumer GameCoreAssets.</summary>
        public UniTask<ESGameCoreAssetPreloadReport> PreloadGameCoreAssetsAsync(ESGameCoreAssetPreloadCatalog catalog, CancellationToken cancellationToken = default)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!AssetLoadingService.IsInitialized) throw new InvalidOperationException("\u5fc5\u987b\u5148\u521d\u59cb\u5316\u65b0\u7248 Asset Provider\uff0c\u624d\u80fd\u9884\u70ed GameCore \u8d44\u4ea7\u3002");
            return catalog.PreloadAsync(cancellationToken);
        }

        public void DisposeAssetLoading()
        {
            DisposeConsumerResidentAssets();
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
