using System;
using System.Collections.Generic;
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
        public static readonly ESConfigKeyTable<ESAssetReferPrefabConfigData> Prefabs = new ESConfigKeyTable<ESAssetReferPrefabConfigData>(256);
        public static readonly ESConfigKeyTable<ESAssetReferSpriteConfigData> Sprites = new ESConfigKeyTable<ESAssetReferSpriteConfigData>(256);
        public static readonly ESConfigKeyTable<ESAssetReferAudioClipConfigData> AudioClips = new ESConfigKeyTable<ESAssetReferAudioClipConfigData>(256);
        public static readonly ESConfigKeyTable<ESAssetReferAnimationClipConfigData> AnimationClips = new ESConfigKeyTable<ESAssetReferAnimationClipConfigData>(256);
        public static readonly ESConfigKeyTable<ESAssetReferAnimatorControllerConfigData> AnimatorControllers = new ESConfigKeyTable<ESAssetReferAnimatorControllerConfigData>(128);
        public static readonly ESConfigKeyTable<ESAssetReferMaterialConfigData> Materials = new ESConfigKeyTable<ESAssetReferMaterialConfigData>(256);
        public static readonly ESConfigKeyTable<ESAssetReferMeshConfigData> Meshes = new ESConfigKeyTable<ESAssetReferMeshConfigData>(256);
        public static readonly ESConfigKeyTable<ESAssetReferSceneConfigData> Scenes = new ESConfigKeyTable<ESAssetReferSceneConfigData>(64);
        public static readonly ESConfigKeyTable<ESAssetReferTextureConfigData> Textures = new ESConfigKeyTable<ESAssetReferTextureConfigData>(128);
        public static readonly ESConfigKeyTable<ESAssetReferTexture2DConfigData> Texture2Ds = new ESConfigKeyTable<ESAssetReferTexture2DConfigData>(128);
        public static readonly ESConfigKeyTable<ESAssetReferSpriteAtlasConfigData> SpriteAtlases = new ESConfigKeyTable<ESAssetReferSpriteAtlasConfigData>(64);
        public static readonly ESConfigKeyTable<ESAssetReferAvatarConfigData> Avatars = new ESConfigKeyTable<ESAssetReferAvatarConfigData>(64);
        public static readonly ESConfigKeyTable<ESAssetReferPlayableAssetConfigData> PlayableAssets = new ESConfigKeyTable<ESAssetReferPlayableAssetConfigData>(64);
        public static readonly ESConfigKeyTable<ESAssetReferTimelineAssetConfigData> TimelineAssets = new ESConfigKeyTable<ESAssetReferTimelineAssetConfigData>(64);
        public static readonly ESConfigKeyTable<ESAssetReferVideoClipConfigData> VideoClips = new ESConfigKeyTable<ESAssetReferVideoClipConfigData>(64);
        public static readonly ESConfigKeyTable<ESAssetReferTerrainDataConfigData> TerrainDatas = new ESConfigKeyTable<ESAssetReferTerrainDataConfigData>(32);

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
            count += ClearLoadedAssets(TimelineAssets);
            count += ClearLoadedAssets(VideoClips);
            count += ClearLoadedAssets(TerrainDatas);
            return count;
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
        [MenuItem("ES/Asset Registry/Rebuild AssetTable From AssetLibraries")]
        public static void MenuRebuildAssetTableFromLibrariesEditor()
        {
            ESAssetAutoRegisterReport report = RebuildAssetTableFromLibrariesEditor(true, true);
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        public static ESAssetAutoRegisterReport RebuildAssetTableFromLibrariesEditor(bool rebuildLegacyConfigTables = true, bool clearBeforeBuild = true)
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
            ESAssetRecord[] records = ESAssetRegistry.BuildFromAssetLibraries(libraries, clearBeforeBuild);
            ESGameManager.AssetTable.Load(records);
            report.registeredPageCount = ESGameManager.AssetTable.Count;

            if (rebuildLegacyConfigTables)
                RebuildLegacyConfigTablesFromRecords(records);

            report.conflictCount = GetAssetConflictCount();
            report.conflictReport = GetAssetConflictReport();
            return report;
        }

        public static int RebuildLegacyConfigTablesFromRecords(IReadOnlyList<ESAssetRecord> records)
        {
            BeginBuild(true);
            try
            {
                int count = 0;
                if (records != null)
                {
                    for (int i = 0; i < records.Count; i++)
                    {
                        if (RegisterRecordAsLegacyConfigData(records[i]))
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

        private static bool RegisterRecordAsLegacyConfigData(ESAssetRecord record)
        {
            switch (record.kind)
            {
                case ESAssetReferKind.Prefab:
                    return RegisterPrefab(CreateAssetDataFromRecord<ESAssetReferPrefabConfigData, ESAssetReferPrefabConfigKey>(record, new ESAssetReferPrefabConfigData(), new ESAssetReferPrefabConfigKey()));
                case ESAssetReferKind.Scene:
                    return RegisterScene(CreateAssetDataFromRecord<ESAssetReferSceneConfigData, ESAssetReferSceneConfigKey>(record, new ESAssetReferSceneConfigData(), new ESAssetReferSceneConfigKey()));
                case ESAssetReferKind.Sprite:
                    return RegisterSprite(CreateAssetDataFromRecord<ESAssetReferSpriteConfigData, ESAssetReferSpriteConfigKey>(record, new ESAssetReferSpriteConfigData(), new ESAssetReferSpriteConfigKey()));
                case ESAssetReferKind.Texture2D:
                    return RegisterTexture2D(CreateAssetDataFromRecord<ESAssetReferTexture2DConfigData, ESAssetReferTexture2DConfigKey>(record, new ESAssetReferTexture2DConfigData(), new ESAssetReferTexture2DConfigKey()));
                case ESAssetReferKind.Texture:
                    return RegisterTexture(CreateAssetDataFromRecord<ESAssetReferTextureConfigData, ESAssetReferTextureConfigKey>(record, new ESAssetReferTextureConfigData(), new ESAssetReferTextureConfigKey()));
                case ESAssetReferKind.SpriteAtlas:
                    return RegisterSpriteAtlas(CreateAssetDataFromRecord<ESAssetReferSpriteAtlasConfigData, ESAssetReferSpriteAtlasConfigKey>(record, new ESAssetReferSpriteAtlasConfigData(), new ESAssetReferSpriteAtlasConfigKey()));
                case ESAssetReferKind.Material:
                    return RegisterMaterial(CreateAssetDataFromRecord<ESAssetReferMaterialConfigData, ESAssetReferMaterialConfigKey>(record, new ESAssetReferMaterialConfigData(), new ESAssetReferMaterialConfigKey()));
                case ESAssetReferKind.Mesh:
                    return RegisterMesh(CreateAssetDataFromRecord<ESAssetReferMeshConfigData, ESAssetReferMeshConfigKey>(record, new ESAssetReferMeshConfigData(), new ESAssetReferMeshConfigKey()));
                case ESAssetReferKind.AnimationClip:
                    return RegisterAnimationClip(CreateAssetDataFromRecord<ESAssetReferAnimationClipConfigData, ESAssetReferAnimationClipConfigKey>(record, new ESAssetReferAnimationClipConfigData(), new ESAssetReferAnimationClipConfigKey()));
                case ESAssetReferKind.AnimatorController:
                    return RegisterAnimatorController(CreateAssetDataFromRecord<ESAssetReferAnimatorControllerConfigData, ESAssetReferAnimatorControllerConfigKey>(record, new ESAssetReferAnimatorControllerConfigData(), new ESAssetReferAnimatorControllerConfigKey()));
                case ESAssetReferKind.Avatar:
                    return RegisterAvatar(CreateAssetDataFromRecord<ESAssetReferAvatarConfigData, ESAssetReferAvatarConfigKey>(record, new ESAssetReferAvatarConfigData(), new ESAssetReferAvatarConfigKey()));
                case ESAssetReferKind.AudioClip:
                    return RegisterAudioClip(CreateAssetDataFromRecord<ESAssetReferAudioClipConfigData, ESAssetReferAudioClipConfigKey>(record, new ESAssetReferAudioClipConfigData(), new ESAssetReferAudioClipConfigKey()));
                case ESAssetReferKind.VideoClip:
                    return RegisterVideoClip(CreateAssetDataFromRecord<ESAssetReferVideoClipConfigData, ESAssetReferVideoClipConfigKey>(record, new ESAssetReferVideoClipConfigData(), new ESAssetReferVideoClipConfigKey()));
                case ESAssetReferKind.TimelineAsset:
                    return RegisterTimelineAsset(CreateAssetDataFromRecord<ESAssetReferTimelineAssetConfigData, ESAssetReferTimelineAssetConfigKey>(record, new ESAssetReferTimelineAssetConfigData(), new ESAssetReferTimelineAssetConfigKey()));
                case ESAssetReferKind.PlayableAsset:
                    return RegisterPlayableAsset(CreateAssetDataFromRecord<ESAssetReferPlayableAssetConfigData, ESAssetReferPlayableAssetConfigKey>(record, new ESAssetReferPlayableAssetConfigData(), new ESAssetReferPlayableAssetConfigKey()));
                case ESAssetReferKind.TerrainData:
                    return RegisterTerrainData(CreateAssetDataFromRecord<ESAssetReferTerrainDataConfigData, ESAssetReferTerrainDataConfigKey>(record, new ESAssetReferTerrainDataConfigData(), new ESAssetReferTerrainDataConfigKey()));
                default:
                    return false;
            }
        }

        private static TData CreateAssetDataFromRecord<TData, TKey>(ESAssetRecord record, TData data, TKey key)
            where TKey : IESConfigKey
        {
            Type keyType = key.GetType();
            keyType.GetField("stringKey")?.SetValue(key, record.stringKey);
            keyType.GetField("assetRuntimeKey")?.SetValue(key, record.runtimeKey);
            keyType.GetMethod("SetAssetAuthority")?.Invoke(key, new object[] { record.guid, record.localFileId, record.assetType != null ? record.assetType.FullName : null, record.assetPath });

            Type dataType = data.GetType();
            dataType.GetField("runtimeKey")?.SetValue(data, record.runtimeKey);
            dataType.GetField("keyName")?.SetValue(data, record.stringKey);
            dataType.GetField("displayName")?.SetValue(data, record.assetName);
            dataType.GetField("sourcePackage")?.SetValue(data, record.libraryName);
            dataType.GetField("key")?.SetValue(data, key);
            return data;
        }
#endif

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
            if (table.TryGet(runtimeKey, out TData existing) || table.TryGetByStringKey(stringKey, out existing))
                data.CopyRuntimeAssetStateFrom(existing);

            return table.Upsert(runtimeKey, data, stringKey);
        }

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

        [ShowInInspector, ReadOnly, LabelText("椋炶鐗╄〃")]
        public readonly ESConfigKeyTable<ESShotRuntimeData> Shots = ShotTable;

        [ShowInInspector, ReadOnly, LabelText("Monster Table")]
        public readonly ESConfigKeyTable<ESMonsterRuntimeData> Monsters = MonsterTable;

        [ShowInInspector, ReadOnly, LabelText("NPC Table")]
        public readonly ESConfigKeyTable<ESNpcRuntimeData> Npcs = NpcTable;

        [ShowInInspector, ReadOnly, LabelText("Weapon Table")]
        public readonly ESConfigKeyTable<ESWeaponRuntimeData> Weapons = WeaponTable;

        [ShowInInspector, ReadOnly, LabelText("鎶€鑳借〃")]
        public readonly ESConfigKeyTable<ESSkillRuntimeData> Skills = SkillTable;

        [ShowInInspector, ReadOnly, LabelText("Buff瀹炰緥绱㈠紩")]
        public readonly ESRuntimeInstanceIndex<ESActiveBuffRuntime> BuffInstances = BuffInstanceIndex;

        [ShowInInspector, ReadOnly, LabelText("Shot Instance Index")]
        public readonly ESRuntimeInstanceIndex<Item> ShotInstances = ShotInstanceIndex;

        [ShowInInspector, ReadOnly, LabelText("Building")]
        private static bool isBuilding;

        public bool IsBuilding => isBuilding;
        public static bool IsBuildingStatic => isBuilding;

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

        public void Initialize(
            IEnumerable<BuffDefinitionDataInfo> buffs = null,
            IEnumerable<ItemDataInfo> items = null,
            IEnumerable<ActorDataInfo> actors = null,
            IEnumerable<SkillDefinitionDataInfo> skills = null,
            IEnumerable<ESGameCoreConfigKeyJsonRecord> jsonRecords = null,
            bool clear = true)
        {
            BeginBuild(clear);
            try
            {
                RegisterBuffs(buffs);
                RegisterItems(items);
                RegisterActors(actors);
                RegisterSkills(skills);
                RegisterJsonRecords(jsonRecords);
            }
            finally
            {
                EndBuild();
            }
        }

        public int RegisterBuffs(IEnumerable<BuffDefinitionDataInfo> infos)
        {
            if (infos == null)
                return 0;

            int count = 0;
            foreach (BuffDefinitionDataInfo info in infos)
            {
                if (RegisterBuff(info))
                    count++;
            }

            return count;
        }

        public int RegisterItems(IEnumerable<ItemDataInfo> infos)
        {
            if (infos == null)
                return 0;

            int count = 0;
            foreach (ItemDataInfo info in infos)
            {
                if (RegisterShot(info))
                    count++;

                if (RegisterWeapon(info))
                    count++;
            }

            return count;
        }

        public int RegisterActors(IEnumerable<ActorDataInfo> infos)
        {
            if (infos == null)
                return 0;

            int count = 0;
            foreach (ActorDataInfo info in infos)
            {
                if (RegisterActor(info))
                    count++;
            }

            return count;
        }

        public int RegisterSkills(IEnumerable<SkillDefinitionDataInfo> infos)
        {
            if (infos == null)
                return 0;

            int count = 0;
            foreach (SkillDefinitionDataInfo info in infos)
            {
                if (RegisterSkill(info))
                    count++;
            }

            return count;
        }

        public bool RegisterBuff(BuffDefinitionDataInfo info, GameObject prefab = null, UnityEngine.Object extraAsset = null)
        {
            if (info == null || info.sharedData == null)
                return false;

            if (info.sharedData.key == null)
                info.sharedData.key = new ESBuffConfigKey();

            if (IsSameBuffAlreadyRegistered(info))
                return true;

            ESBuffRuntimeData data = new ESBuffRuntimeData
            {
                keyName = info.KeyName,
                displayName = info.KeyName,
                sourcePackage = info.name,
                soSource = info,
                sharedData = info.sharedData,
                defaultVariableData = info.variableData,
                prefab = prefab,
                extraAsset = extraAsset
            };

            data.runtimeKey = Buffs.Bake(info.sharedData.key, info.KeyName);
            return Buffs.Upsert(data.runtimeKey, data, info.sharedData.key.GetStringKey(info.KeyName));
        }

        public bool RegisterShot(ItemDataInfo info, GameObject prefab = null, UnityEngine.Object extraAsset = null)
        {
            if (info == null || info.baseConfig == null || info.baseConfig.kind != ItemKind.Shot)
                return false;

            if (info.shotKey == null)
                info.shotKey = new ESShotConfigKey();

            if (IsSameShotAlreadyRegistered(info))
                return true;

            ESShotRuntimeData data = new ESShotRuntimeData
            {
                keyName = info.KeyName,
                displayName = DisplayName(info),
                sourcePackage = info.name,
                soSource = info,
                sharedData = info.shotShared,
                defaultVariableData = info.shotVariable,
                prefab = prefab != null ? prefab : info.baseConfig.prefab,
                extraAsset = extraAsset
            };

            data.runtimeKey = Shots.Bake(info.shotKey, info.KeyName);
            return Shots.Upsert(data.runtimeKey, data, info.shotKey.GetStringKey(info.KeyName));
        }

        public bool RegisterWeapon(ItemDataInfo info, GameObject prefab = null, UnityEngine.Object extraAsset = null)
        {
            if (info == null || info.baseConfig == null || info.baseConfig.kind != ItemKind.Weapon)
                return false;

            if (info.weaponKey == null)
                info.weaponKey = new ESWeaponConfigKey();

            if (IsSameWeaponAlreadyRegistered(info))
                return true;

            ESWeaponRuntimeData data = new ESWeaponRuntimeData
            {
                keyName = info.KeyName,
                displayName = DisplayName(info),
                sourcePackage = info.name,
                soSource = info,
                sharedData = info.weaponShared,
                defaultVariableData = info.weaponVariable,
                prefab = prefab != null ? prefab : info.baseConfig.prefab,
                extraAsset = extraAsset
            };

            data.runtimeKey = Weapons.Bake(info.weaponKey, info.KeyName);
            return Weapons.Upsert(data.runtimeKey, data, info.weaponKey.GetStringKey(info.KeyName));
        }

        public bool RegisterActor(ActorDataInfo info, GameObject prefab = null, UnityEngine.Object extraAsset = null)
        {
            if (info == null)
                return false;

            if (info.actorKind == ActorDataKind.Monster)
            {
                if (info.monsterKey == null)
                    info.monsterKey = new ESMonsterConfigKey();

                if (IsSameMonsterAlreadyRegistered(info))
                    return true;

                ESMonsterRuntimeData data = new ESMonsterRuntimeData
                {
                    keyName = info.KeyName,
                    displayName = DisplayName(info),
                    sourcePackage = info.name,
                    soSource = info,
                    sharedData = info.motionShared,
                    defaultVariableData = info.motionVariable,
                    prefab = prefab,
                    extraAsset = extraAsset
                };

                data.runtimeKey = Monsters.Bake(info.monsterKey, info.KeyName);
                return Monsters.Upsert(data.runtimeKey, data, info.monsterKey.GetStringKey(info.KeyName));
            }

            if (info.actorKind == ActorDataKind.NPC)
            {
                if (info.npcKey == null)
                    info.npcKey = new ESNpcConfigKey();

                if (IsSameNpcAlreadyRegistered(info))
                    return true;

                ESNpcRuntimeData data = new ESNpcRuntimeData
                {
                    keyName = info.KeyName,
                    displayName = DisplayName(info),
                    sourcePackage = info.name,
                    soSource = info,
                    sharedData = info.motionShared,
                    defaultVariableData = info.motionVariable,
                    prefab = prefab,
                    extraAsset = extraAsset
                };

                data.runtimeKey = Npcs.Bake(info.npcKey, info.KeyName);
                return Npcs.Upsert(data.runtimeKey, data, info.npcKey.GetStringKey(info.KeyName));
            }

            return false;
        }

        public bool RegisterSkill(SkillDefinitionDataInfo info, GameObject prefab = null, UnityEngine.Object extraAsset = null)
        {
            if (info == null)
                return false;

            if (info.skillKey == null)
                info.skillKey = new ESSkillConfigKey();

            if (IsSameSkillAlreadyRegistered(info))
                return true;

            ESSkillRuntimeData data = new ESSkillRuntimeData
            {
                keyName = info.KeyName,
                displayName = info.KeyName,
                sourcePackage = info.name,
                soSource = info,
                trackProcess = info.trackProcess,
                baseStateInfo = info.baseStateInfo,
                prefab = prefab,
                extraAsset = extraAsset
            };

            data.runtimeKey = Skills.Bake(info.skillKey, info.KeyName);
            return Skills.Upsert(data.runtimeKey, data, info.skillKey.GetStringKey(info.KeyName));
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

        public bool RegisterJsonRecord(ESGameCoreConfigKeyJsonRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.kind) || (record.enumKey == 0 && string.IsNullOrEmpty(record.stringKey)))
                return false;

            switch (record.kind)
            {
                case ESGameCoreConfigKeyJsonKinds.Buff:
                    return RegisterJsonBuff(record);
                case ESGameCoreConfigKeyJsonKinds.Shot:
                    return RegisterJsonShot(record);
                case ESGameCoreConfigKeyJsonKinds.Monster:
                    return RegisterJsonMonster(record);
                case ESGameCoreConfigKeyJsonKinds.Npc:
                    return RegisterJsonNpc(record);
                case ESGameCoreConfigKeyJsonKinds.Weapon:
                    return RegisterJsonWeapon(record);
                case ESGameCoreConfigKeyJsonKinds.Skill:
                    return RegisterJsonSkill(record);
                default:
                    return false;
            }
        }

        public int RegisterJsonRecords(IEnumerable<ESGameCoreConfigKeyJsonRecord> records)
        {
            if (records == null)
                return 0;

            int count = 0;
            foreach (ESGameCoreConfigKeyJsonRecord record in records)
            {
                if (RegisterJsonRecord(record))
                    count++;
            }

            return count;
        }

        public bool RegisterJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return false;

            ESGameCoreConfigKeyJsonRecord record = JsonUtility.FromJson<ESGameCoreConfigKeyJsonRecord>(json);
            return RegisterJsonRecord(record);
        }

        public int RegisterJsonBatch(string json)
        {
            if (string.IsNullOrEmpty(json))
                return 0;

            ESGameCoreConfigKeyJsonBatch batch = JsonUtility.FromJson<ESGameCoreConfigKeyJsonBatch>(json);
            return batch != null ? RegisterJsonRecords(batch.records) : 0;
        }

        public string CreateJson(ESGameCoreConfigKeyJsonRecord record, bool prettyPrint = true)
        {
            return record == null ? string.Empty : JsonUtility.ToJson(record, prettyPrint);
        }

        public string CreateJsonBatch(IList<ESGameCoreConfigKeyJsonRecord> records, bool prettyPrint = true)
        {
            ESGameCoreConfigKeyJsonBatch batch = new ESGameCoreConfigKeyJsonBatch
            {
                records = records != null ? ToArray(records) : Array.Empty<ESGameCoreConfigKeyJsonRecord>()
            };
            return JsonUtility.ToJson(batch, prettyPrint);
        }

        public ESGameCoreConfigKeyJsonRecord CreateBuffJsonRecord(BuffDefinitionDataInfo info)
        {
            if (info == null || info.sharedData == null)
                return null;

            ESBuffConfigKey key = info.sharedData.key;
            return CreateRecord(
                ESGameCoreConfigKeyJsonKinds.Buff,
                key != null ? key.EnumKeyInt : 0,
                key != null ? key.GetStringKey(info.KeyName) : info.KeyName,
                info.KeyName,
                null,
                info.name,
                JsonUtility.ToJson(info.sharedData),
                JsonUtility.ToJson(info.variableData));
        }

        public ESGameCoreConfigKeyJsonRecord CreateShotJsonRecord(ItemDataInfo info)
        {
            if (info == null || info.baseConfig == null || info.baseConfig.kind != ItemKind.Shot)
                return null;

            ESShotConfigKey key = info.shotKey;
            return CreateRecord(
                ESGameCoreConfigKeyJsonKinds.Shot,
                key != null ? key.EnumKeyInt : 0,
                key != null ? key.GetStringKey(info.KeyName) : info.KeyName,
                DisplayName(info),
                null,
                info.name,
                JsonUtility.ToJson(info.shotShared),
                JsonUtility.ToJson(info.shotVariable));
        }

        public ESGameCoreConfigKeyJsonRecord CreateWeaponJsonRecord(ItemDataInfo info)
        {
            if (info == null || info.baseConfig == null || info.baseConfig.kind != ItemKind.Weapon)
                return null;

            ESWeaponConfigKey key = info.weaponKey;
            return CreateRecord(
                ESGameCoreConfigKeyJsonKinds.Weapon,
                key != null ? key.EnumKeyInt : 0,
                key != null ? key.GetStringKey(info.KeyName) : info.KeyName,
                DisplayName(info),
                null,
                info.name,
                JsonUtility.ToJson(info.weaponShared),
                JsonUtility.ToJson(info.weaponVariable));
        }

        public ESGameCoreConfigKeyJsonRecord CreateActorJsonRecord(ActorDataInfo info)
        {
            if (info == null)
                return null;

            if (info.actorKind == ActorDataKind.Monster)
            {
                ESMonsterConfigKey key = info.monsterKey;
                return CreateRecord(
                    ESGameCoreConfigKeyJsonKinds.Monster,
                    key != null ? key.EnumKeyInt : 0,
                    key != null ? key.GetStringKey(info.KeyName) : info.KeyName,
                    DisplayName(info),
                    null,
                    info.name,
                    JsonUtility.ToJson(info.motionShared),
                    JsonUtility.ToJson(info.motionVariable));
            }

            if (info.actorKind == ActorDataKind.NPC)
            {
                ESNpcConfigKey key = info.npcKey;
                return CreateRecord(
                    ESGameCoreConfigKeyJsonKinds.Npc,
                    key != null ? key.EnumKeyInt : 0,
                    key != null ? key.GetStringKey(info.KeyName) : info.KeyName,
                    DisplayName(info),
                    null,
                    info.name,
                    JsonUtility.ToJson(info.motionShared),
                    JsonUtility.ToJson(info.motionVariable));
            }

            return null;
        }

        public ESGameCoreConfigKeyJsonRecord CreateSkillJsonRecord(SkillDefinitionDataInfo info)
        {
            if (info == null)
                return null;

            ESSkillConfigKey key = info.skillKey;
            return CreateRecord(
                ESGameCoreConfigKeyJsonKinds.Skill,
                key != null ? key.EnumKeyInt : 0,
                key != null ? key.GetStringKey(info.KeyName) : info.KeyName,
                info.KeyName,
                null,
                info.name,
                string.Empty,
                string.Empty);
        }

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

        private bool RegisterJsonBuff(ESGameCoreConfigKeyJsonRecord record)
        {
            int runtimeKey = Buffs.BakeRaw(record.enumKey, record.stringKey);
            if (runtimeKey == 0)
                return false;

            ESBuffRuntimeData data = new ESBuffRuntimeData
            {
                runtimeKey = runtimeKey,
                keyName = record.stringKey,
                displayName = record.displayName,
                sourcePackage = record.sourcePackage,
                version = record.version,
                sharedData = FromJsonOrDefault<BuffSharedData>(record.sharedDataJson),
                defaultVariableData = FromJsonOrDefault<BuffVariableData>(record.variableDataJson),
                jsonSource = record.ToSource()
            };

            return Buffs.Upsert(runtimeKey, data, record.stringKey);
        }

        private bool RegisterJsonShot(ESGameCoreConfigKeyJsonRecord record)
        {
            int runtimeKey = Shots.BakeRaw(record.enumKey, record.stringKey);
            if (runtimeKey == 0)
                return false;

            ESShotRuntimeData data = new ESShotRuntimeData
            {
                runtimeKey = runtimeKey,
                keyName = record.stringKey,
                displayName = record.displayName,
                sourcePackage = record.sourcePackage,
                version = record.version,
                sharedData = FromJsonOrDefault<ItemShotSharedData>(record.sharedDataJson),
                defaultVariableData = FromJsonOrDefault<ItemShotVariableData>(record.variableDataJson),
                jsonSource = record.ToSource()
            };

            return Shots.Upsert(runtimeKey, data, record.stringKey);
        }

        private bool RegisterJsonMonster(ESGameCoreConfigKeyJsonRecord record)
        {
            int runtimeKey = Monsters.BakeRaw(record.enumKey, record.stringKey);
            if (runtimeKey == 0)
                return false;

            ESMonsterRuntimeData data = new ESMonsterRuntimeData
            {
                runtimeKey = runtimeKey,
                keyName = record.stringKey,
                displayName = record.displayName,
                sourcePackage = record.sourcePackage,
                version = record.version,
                sharedData = FromJsonOrDefault<EntityMotionSharedData>(record.sharedDataJson),
                defaultVariableData = FromJsonOrDefault<EntityMotionVariableData>(record.variableDataJson),
                jsonSource = record.ToSource()
            };

            return Monsters.Upsert(runtimeKey, data, record.stringKey);
        }

        private bool RegisterJsonNpc(ESGameCoreConfigKeyJsonRecord record)
        {
            int runtimeKey = Npcs.BakeRaw(record.enumKey, record.stringKey);
            if (runtimeKey == 0)
                return false;

            ESNpcRuntimeData data = new ESNpcRuntimeData
            {
                runtimeKey = runtimeKey,
                keyName = record.stringKey,
                displayName = record.displayName,
                sourcePackage = record.sourcePackage,
                version = record.version,
                sharedData = FromJsonOrDefault<EntityMotionSharedData>(record.sharedDataJson),
                defaultVariableData = FromJsonOrDefault<EntityMotionVariableData>(record.variableDataJson),
                jsonSource = record.ToSource()
            };

            return Npcs.Upsert(runtimeKey, data, record.stringKey);
        }

        private bool RegisterJsonWeapon(ESGameCoreConfigKeyJsonRecord record)
        {
            int runtimeKey = Weapons.BakeRaw(record.enumKey, record.stringKey);
            if (runtimeKey == 0)
                return false;

            ESWeaponRuntimeData data = new ESWeaponRuntimeData
            {
                runtimeKey = runtimeKey,
                keyName = record.stringKey,
                displayName = record.displayName,
                sourcePackage = record.sourcePackage,
                version = record.version,
                sharedData = FromJsonOrDefault<ItemWeaponSharedData>(record.sharedDataJson),
                defaultVariableData = FromJsonOrDefault<ItemWeaponVariableData>(record.variableDataJson),
                jsonSource = record.ToSource()
            };

            return Weapons.Upsert(runtimeKey, data, record.stringKey);
        }

        private bool RegisterJsonSkill(ESGameCoreConfigKeyJsonRecord record)
        {
            int runtimeKey = Skills.BakeRaw(record.enumKey, record.stringKey);
            if (runtimeKey == 0)
                return false;

            ESSkillRuntimeData data = new ESSkillRuntimeData
            {
                runtimeKey = runtimeKey,
                keyName = record.stringKey,
                displayName = record.displayName,
                sourcePackage = record.sourcePackage,
                version = record.version,
                jsonSource = record.ToSource()
            };

            return Skills.Upsert(runtimeKey, data, record.stringKey);
        }

        private bool IsSameBuffAlreadyRegistered(BuffDefinitionDataInfo info)
        {
            return TryGetByKey(Buffs, info.sharedData.key, info.KeyName, out ESBuffRuntimeData data)
                && ReferenceEquals(data.soSource, info);
        }

        private bool IsSameShotAlreadyRegistered(ItemDataInfo info)
        {
            return TryGetByKey(Shots, info.shotKey, info.KeyName, out ESShotRuntimeData data)
                && ReferenceEquals(data.soSource, info);
        }

        private bool IsSameWeaponAlreadyRegistered(ItemDataInfo info)
        {
            return TryGetByKey(Weapons, info.weaponKey, info.KeyName, out ESWeaponRuntimeData data)
                && ReferenceEquals(data.soSource, info);
        }

        private bool IsSameMonsterAlreadyRegistered(ActorDataInfo info)
        {
            return TryGetByKey(Monsters, info.monsterKey, info.KeyName, out ESMonsterRuntimeData data)
                && ReferenceEquals(data.soSource, info);
        }

        private bool IsSameNpcAlreadyRegistered(ActorDataInfo info)
        {
            return TryGetByKey(Npcs, info.npcKey, info.KeyName, out ESNpcRuntimeData data)
                && ReferenceEquals(data.soSource, info);
        }

        private bool IsSameSkillAlreadyRegistered(SkillDefinitionDataInfo info)
        {
            return TryGetByKey(Skills, info.skillKey, info.KeyName, out ESSkillRuntimeData data)
                && ReferenceEquals(data.soSource, info);
        }

        private static bool TryGetByString<TData>(ESConfigKeyTable<TData> table, string stringKey, out TData data)
            where TData : class
        {
            if (table != null && table.TryGetRuntimeKey(stringKey, out int runtimeKey))
                return table.TryGet(runtimeKey, out data);

            data = null;
            return false;
        }

        private static bool TryGetByKey<TEnumKey, TData>(ESConfigKeyTable<TData> table, ESGameCoreConfigKey<TEnumKey> key, string fallbackStringKey, out TData data)
            where TEnumKey : struct, Enum
            where TData : class
        {
            data = null;
            if (table == null || key == null)
                return false;

            int enumKey = key.EnumKeyInt;
            if (enumKey != 0 && table.TryGet(enumKey, out data))
                return true;

            string stringKey = key.GetStringKey(fallbackStringKey);
            return TryGetByString(table, stringKey, out data);
        }

        private static T FromJsonOrDefault<T>(string json)
        {
            return string.IsNullOrEmpty(json) ? default : JsonUtility.FromJson<T>(json);
        }

        private static ESGameCoreConfigKeyJsonRecord CreateRecord(
            string kind,
            int enumKey,
            string stringKey,
            string displayName,
            string prefabAddress,
            string sourcePackage,
            string sharedDataJson,
            string variableDataJson)
        {
            return new ESGameCoreConfigKeyJsonRecord
            {
                kind = kind,
                enumKey = enumKey,
                stringKey = stringKey,
                displayName = displayName,
                prefabAddress = prefabAddress,
                sourcePackage = sourcePackage,
                sharedDataJson = sharedDataJson,
                variableDataJson = variableDataJson
            };
        }

        private static ESGameCoreConfigKeyJsonRecord[] ToArray(IList<ESGameCoreConfigKeyJsonRecord> records)
        {
            ESGameCoreConfigKeyJsonRecord[] array = new ESGameCoreConfigKeyJsonRecord[records.Count];
            for (int i = 0; i < records.Count; i++)
                array[i] = records[i];

            return array;
        }

        private static string DisplayName(ItemDataInfo info)
        {
            if (info != null && info.baseConfig != null && !string.IsNullOrEmpty(info.baseConfig.displayName))
                return info.baseConfig.displayName;

            return info != null ? info.KeyName : string.Empty;
        }

        private static string DisplayName(ActorDataInfo info)
        {
            if (info != null && !string.IsNullOrEmpty(info.displayName))
                return info.displayName;

            return info != null ? info.KeyName : string.Empty;
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
