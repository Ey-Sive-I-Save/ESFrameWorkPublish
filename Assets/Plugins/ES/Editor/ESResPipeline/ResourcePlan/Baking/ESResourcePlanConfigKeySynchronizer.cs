using System;
using System.Collections.Generic;
using System.Linq;
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
        private static readonly string[] ManagedAssetRoots =
        {
            "Assets/ESNormalAssets",
            "Assets/Plugins/ES"
        };

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

        [MenuItem(MenuItemPathDefine.RESOURCE_DELIVERY_PATH + "ConfigKey/预览全部过期绑定 Key")]
        private static void PreviewAllMenu()
        {
            ESAssetCatalogKeyPicker.RefreshForValidation();
            int ownerCount;
            var changes = new List<string>();
            int pending = CollectPendingChanges(true, changes, out ownerCount);
            string detail = changes.Count == 0
                ? "当前受管 ES 资产没有发现需要同步的绑定 Key。"
                : "受管范围：Assets/ESNormalAssets、Assets/Plugins/ES\n\n" + string.Join("\n", changes.Take(80));
            if (changes.Count > 80)
                detail += "\n...（其余 " + (changes.Count - 80) + " 项未展开）";
            EditorUtility.DisplayDialog("ConfigKey 同步预览", "对象：" + ownerCount + "，待同步：" + pending + "\n\n" + detail, "确定");
        }

        public static int SynchronizeAll()
        {
            ESAssetCatalogKeyPicker.RefreshForValidation();
            int ownerCount;
            var preview = new List<string>();
            int pending = CollectPendingChanges(true, preview, out ownerCount);
            if (pending == 0)
            {
                Debug.Log("[ESRes][ConfigKey] managed owner scan=" + ownerCount + ", synchronized=0.");
                return 0;
            }

            string previewText = string.Join("\n", preview.Take(40));
            if (preview.Count > 40)
                previewText += "\n...（其余 " + (preview.Count - 40) + " 项未展开）";
            if (!EditorUtility.DisplayDialog(
                "确认同步受管 ConfigKey",
                "只会修改以下 ES 受管目录中的已绑定源快照：\nAssets/ESNormalAssets\nAssets/Plugins/ES\n\n待同步：" + pending + " 项\n\n" + previewText + "\n\n操作支持 Undo，手填 Key 不会被修改。",
                "同步",
                "取消"))
                return 0;

            int syncCount = CollectPendingChanges(false, null, out ownerCount);
            if (syncCount > 0)
                AssetDatabase.SaveAssets();
            Debug.Log("[ESRes][ConfigKey] managed owner scan=" + ownerCount + ", synchronized=" + syncCount + ".");
            return syncCount;
        }

        private static int CollectPendingChanges(bool dryRun, List<string> changes, out int ownerCount)
        {
            ownerCount = 0;
            int syncCount = 0;
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in FindManagedGuids("t:ScriptableObject"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !seenPaths.Add(path))
                    continue;
                foreach (UnityEngine.Object owner in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(owner is ScriptableObject))
                        continue;
                    ownerCount++;
                    syncCount += Synchronize(owner, dryRun, changes);
                }
            }
            return syncCount;
        }

        private static IEnumerable<string> FindManagedGuids(string filter)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string root in ManagedAssetRoots)
            {
                if (!AssetDatabase.IsValidFolder(root))
                    continue;
                foreach (string guid in AssetDatabase.FindAssets(filter, new[] { root }))
                {
                    if (seen.Add(guid))
                        yield return guid;
                }
            }
        }

        /// <summary>
        /// 所有烘焙入口共用的只读强一致校验：验证已绑定源资产的 Key 快照
        /// 仍能定位同一源。同步动作必须由用户通过上面的显式菜单主动触发；
        /// 这里绝不能为了继续烘焙而修改任意 ScriptableObject。
        /// </summary>
        public static void ValidateAllForBake()
        {
            ESAssetCatalogKeyPicker.RefreshForValidation();
            var errors = new List<string>();
            foreach (string guid in FindManagedGuids("t:ESResourcePlanInfo"))
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
            => Synchronize((UnityEngine.Object)plan, false, null);

        private static int Synchronize(UnityEngine.Object owner, bool dryRun, List<string> changes)
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

                synchronized++;
                if (changes != null)
                    changes.Add(owner.name + " :: " + pending[i].PropertyPath + " (" + pending[i].Kind + ")");
                if (dryRun)
                    continue;

                if (!undoRecorded)
                {
                    Undo.RecordObject(owner, "Synchronize ConfigKey Snapshots");
                    undoRecorded = true;
                }
                ESAssetCatalogKeyPicker.ApplyCandidate(key, candidate, recordUndo: false);
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
