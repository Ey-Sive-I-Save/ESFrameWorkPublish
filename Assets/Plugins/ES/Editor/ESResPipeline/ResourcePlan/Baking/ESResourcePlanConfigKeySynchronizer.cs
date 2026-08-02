using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// 已绑定资产的 Key 以 AssetLibrary 为唯一真源；本工具同步所有已绑定 GUID/FileId 的引用，
    /// 不会碰没有源资产身份的手填 Key。ResourcePlan 只是其中最主要的使用方。
    /// </summary>
    internal static class ESResourcePlanConfigKeySynchronizer
    {
        private readonly struct PendingSync
        {
            public readonly string PropertyPath;
            public readonly ESAssetReferKind Kind;

            public PendingSync(string propertyPath, ESAssetReferKind kind)
            {
                PropertyPath = propertyPath;
                Kind = kind;
            }
        }

        [MenuItem(MenuItemPathDefine.RESOURCE_DELIVERY_PATH + "ConfigKey/同步全部过期绑定 Key")]
        private static void SynchronizeAllMenu()
            => SynchronizeAll();

        public static int SynchronizeAll()
        {
            ESAssetCatalogKeyPicker.RefreshForValidation();
            int ownerCount = 0;
            int syncCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object owner in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(owner is ScriptableObject))
                        continue;
                    ownerCount++;
                    syncCount += Synchronize(owner);
                }
            }

            if (syncCount > 0)
                AssetDatabase.SaveAssets();
            Debug.Log("[ESRes][ConfigKey] owner scan=" + ownerCount + ", synchronized=" + syncCount + ".");
            return syncCount;
        }

        /// <summary>
        /// 所有烘焙入口共用的强一致校验：先把已绑定源资产的最新 Key 写回引用，
        /// 再验证每个绑定仍能定位同一源。手填的高级 Key 不在这里被改写。
        /// </summary>
        public static void ValidateAllForBake()
        {
            SynchronizeAll();
            ESAssetCatalogKeyPicker.RefreshForValidation();
            var errors = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:ESResourcePlanInfo"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ESResourcePlanInfo plan = AssetDatabase.LoadAssetAtPath<ESResourcePlanInfo>(path);
                if (plan == null)
                    continue;

                var serialized = new SerializedObject(plan);
                SerializedProperty iterator = serialized.GetIterator();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    if (!TryResolveKind(iterator.type, out ESAssetReferKind kind))
                        continue;

                    ESAssetCatalogKeyPicker.Candidate source = ESAssetCatalogKeyPicker.FindCurrent(kind, iterator);
                    if (ESAssetCatalogKeyPicker.HasLibraryKeyConflict(source))
                    {
                        errors.Add(path + " :: " + iterator.propertyPath + " - same asset has conflicting Keys across Libraries.");
                    }
                    else if (ESAssetCatalogKeyPicker.IsBoundSourceMissing(iterator, source))
                    {
                        errors.Add(path + " :: " + iterator.propertyPath + " - bound source is not registered in any project Library/Catalog.");
                    }
                    else if (ESAssetCatalogKeyPicker.IsStale(iterator, source))
                    {
                        errors.Add(path + " :: " + iterator.propertyPath + " - source Key changed; synchronize the Key snapshot before baking.");
                    }
                    enterChildren = false;
                }
            }

            if (errors.Count > 0)
                throw new InvalidOperationException("[ESRes][Bake] ResourcePlan ConfigKey validation failed:\n" + string.Join("\n", errors));
        }

        public static int Synchronize(ESResourcePlanInfo plan)
            => Synchronize((UnityEngine.Object)plan);

        private static int Synchronize(UnityEngine.Object owner)
        {
            if (owner == null)
                return 0;

            var pending = new List<PendingSync>();
            var scan = new SerializedObject(owner);
            SerializedProperty iterator = scan.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (!TryResolveKind(iterator.type, out ESAssetReferKind kind))
                    continue;

                SerializedProperty guid = iterator.FindPropertyRelative("guid");
                if (guid == null || string.IsNullOrEmpty(guid.stringValue))
                    continue; // Advanced manual Key mode: never overwrite.
                pending.Add(new PendingSync(iterator.propertyPath, kind));
                enterChildren = false;
            }

            int synchronized = 0;
            bool undoRecorded = false;
            for (int i = 0; i < pending.Count; i++)
            {
                var serialized = new SerializedObject(owner);
                SerializedProperty key = serialized.FindProperty(pending[i].PropertyPath);
                ESAssetCatalogKeyPicker.Candidate candidate = ESAssetCatalogKeyPicker.FindCurrent(pending[i].Kind, key);
                if (ESAssetCatalogKeyPicker.HasLibraryKeyConflict(candidate))
                    continue;
                if (!ESAssetCatalogKeyPicker.IsStale(key, candidate))
                    continue;

                if (!undoRecorded)
                {
                    Undo.RecordObject(owner, "Synchronize ConfigKey Snapshots");
                    undoRecorded = true;
                }
                ESAssetCatalogKeyPicker.ApplyCandidate(key, candidate, recordUndo: false);
                synchronized++;
            }
            return synchronized;
        }

        private static bool TryResolveKind(string typeName, out ESAssetReferKind kind)
        {
            switch (typeName)
            {
                case nameof(ESAssetReferPrefabConfigKey): kind = ESAssetReferKind.Prefab; return true;
                case nameof(ESAssetReferSpriteConfigKey): kind = ESAssetReferKind.Sprite; return true;
                case nameof(ESAssetReferAudioClipConfigKey): kind = ESAssetReferKind.AudioClip; return true;
                case nameof(ESAssetReferAnimationClipConfigKey): kind = ESAssetReferKind.AnimationClip; return true;
                case nameof(ESAssetReferAnimatorControllerConfigKey): kind = ESAssetReferKind.AnimatorController; return true;
                case nameof(ESAssetReferMaterialConfigKey): kind = ESAssetReferKind.Material; return true;
                case nameof(ESAssetReferMeshConfigKey): kind = ESAssetReferKind.Mesh; return true;
                case nameof(ESAssetReferTextureConfigKey): kind = ESAssetReferKind.Texture; return true;
                case nameof(ESAssetReferTexture2DConfigKey): kind = ESAssetReferKind.Texture2D; return true;
                case nameof(ESAssetReferSpriteAtlasConfigKey): kind = ESAssetReferKind.SpriteAtlas; return true;
                case nameof(ESAssetReferAvatarConfigKey): kind = ESAssetReferKind.Avatar; return true;
                case nameof(ESAssetReferPlayableAssetConfigKey): kind = ESAssetReferKind.PlayableAsset; return true;
                case nameof(ESAssetReferScriptableObjectConfigKey): kind = ESAssetReferKind.ScriptableObject; return true;
                case nameof(ESAssetReferTimelineAssetConfigKey): kind = ESAssetReferKind.TimelineAsset; return true;
                case nameof(ESAssetReferVideoClipConfigKey): kind = ESAssetReferKind.VideoClip; return true;
                case nameof(ESAssetReferTerrainDataConfigKey): kind = ESAssetReferKind.TerrainData; return true;
                case nameof(ESAssetReferRawConfigKey): kind = ESAssetReferKind.Raw; return true;
                default: kind = ESAssetReferKind.None; return false;
            }
        }
    }
}
