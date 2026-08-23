using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// ES Composite 材质的显式版本迁移入口。版本只在迁移完成后写入材质，禁止静默降级未来版本。
    /// </summary>
    public static class ESCompositeMaterialMigration
    {
        public const int CurrentVersion = 1;
        public const string VersionPropertyName = "_ESMaterialVersion";
        public const string VersionTagName = "ESMaterialVersion";

        private static readonly int VersionPropertyId = Shader.PropertyToID(VersionPropertyName);
        private static readonly int SpriteUVRectPropertyId = Shader.PropertyToID("_SpriteUVRect");
        private static readonly Vector4 FullSpriteUVRect = new Vector4(0f, 0f, 1f, 1f);

        public static bool IsSupported(Material material)
        {
            return ESCompositeMaterialInstance.IsCompositeMaterial(material)
                && material.HasProperty(VersionPropertyId);
        }

        public static int GetStoredVersion(Material material)
        {
            if (!IsSupported(material))
                return -1;

            string value = material.GetTag(VersionTagName, false, string.Empty);
            return int.TryParse(value, out int version) ? Mathf.Max(0, version) : 0;
        }

        public static bool NeedsMigration(Material material)
        {
            int version = GetStoredVersion(material);
            return version >= 0 && version < CurrentVersion;
        }

        public static bool Migrate(Material material, bool recordUndo = true)
        {
            int version = GetStoredVersion(material);
            if (version < 0 || version > CurrentVersion)
                return false;

            bool requiresVersionStamp = version < CurrentVersion;
            bool changed = requiresVersionStamp;

            // SyncMaterialKeywords and the baseline migration can both mutate a
            // current-version material. Record before either path so an explicit
            // repair is always reversible, not only a version bump.
            if (recordUndo)
                Undo.RecordObject(material, "升级 ES Composite 材质");

            while (version < CurrentVersion)
            {
                switch (version)
                {
                    case 0:
                        changed |= MigrateBaselineToVersion1(material);
                        version = 1;
                        break;
                    default:
                        return false;
                }
            }

            if (material.HasProperty(VersionPropertyId)
                && (requiresVersionStamp
                    || !Mathf.Approximately(material.GetFloat(VersionPropertyId), CurrentVersion)))
            {
                material.SetFloat(VersionPropertyId, CurrentVersion);
                changed = true;
            }

            string currentTag = material.GetTag(VersionTagName, false, string.Empty);
            string targetTag = CurrentVersion.ToString();
            if (requiresVersionStamp || currentTag != targetTag)
            {
                material.SetOverrideTag(VersionTagName, targetTag);
                changed = true;
            }

            changed |= ESCompositeShaderGUI.SyncMaterialKeywords(material);
            if (changed)
                EditorUtility.SetDirty(material);
            return changed;
        }

        [MenuItem(MenuItemPathDefine.CONTENT_CREATION_PATH + "Shader/升级选中的 Composite 材质", false, 2100)]
        private static void MigrateSelectedMaterials()
        {
            if (!TryCollectSelectedMaterials(out List<Material> materials))
            {
                ESDialog.InfoModal(
                    "es.composite.material-migration.no-selection",
                    "ES Composite 材质升级",
                    "读取材质已取消；没有修改任何材质。",
                    host: ESDialogHost.Editor,
                    allowMainWorkspaceFallback: true);
                return;
            }
            if (materials.Count == 0)
            {
                ESDialog.InfoModal(
                    "es.composite.material-migration.empty",
                    "ES Composite 材质升级",
                    "当前选择中没有 ES Composite 材质。",
                    host: ESDialogHost.Editor,
                    allowMainWorkspaceFallback: true);
                return;
            }

            if (materials.Count > 1
                && !ESDialog.ConfirmModal(
                    "es.composite.material-migration.confirm",
                    "ES Composite 材质升级",
                    "将检查并升级 " + materials.Count + " 个材质。操作支持 Undo，是否继续？",
                    "升级",
                    "取消",
                    tone: ESDialogTone.Warning,
                    host: ESDialogHost.Editor,
                    allowMainWorkspaceFallback: true))
                return;

            int upgraded = 0;
            for (int i = 0; i < materials.Count; i++)
            {
                if (Migrate(materials[i]))
                {
                    upgraded++;
                    AssetDatabase.SaveAssetIfDirty(materials[i]);
                }
            }
            Debug.Log("[ES Composite] 材质升级完成：检查 " + materials.Count + "，更新 " + upgraded + "。");
        }

        [MenuItem(MenuItemPathDefine.CONTENT_CREATION_PATH + "Shader/升级选中的 Composite 材质", true)]
        private static bool ValidateMigrateSelectedMaterials()
        {
            return Selection.objects != null && Selection.objects.Length > 0;
        }

        private static bool MigrateBaselineToVersion1(Material material)
        {
            bool changed = false;
            if (material.HasProperty(SpriteUVRectPropertyId))
            {
                Vector4 rect = material.GetVector(SpriteUVRectPropertyId);
                if (rect.z <= rect.x || rect.w <= rect.y)
                {
                    material.SetVector(SpriteUVRectPropertyId, FullSpriteUVRect);
                    changed = true;
                }
            }
            return changed;
        }

        private static bool TryCollectSelectedMaterials(out List<Material> result)
        {
            result = new List<Material>();
            var ids = new HashSet<int>();
            Object[] selected = Selection.objects;
            try
            {
                for (int i = 0; i < selected.Length; i++)
                {
                    Object value = selected[i];
                    Material material = value as Material;
                    if (material != null)
                    {
                        AddIfSupported(material, result, ids);
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(value);
                    if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
                        continue;

                    string[] guids = AssetDatabase.FindAssets("t:Material", new[] { path });
                    for (int g = 0; g < guids.Length; g++)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar(
                                "读取 ES Composite 材质",
                                path + " (" + (g + 1) + "/" + guids.Length + ")",
                                guids.Length == 0 ? 1f : (float)g / guids.Length))
                        {
                            result.Clear();
                            return false;
                        }
                        Material child = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[g]));
                        AddIfSupported(child, result, ids);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return true;
        }

        private static void AddIfSupported(Material material, List<Material> result, HashSet<int> ids)
        {
            if (IsSupported(material) && ids.Add(material.GetInstanceID()))
                result.Add(material);
        }
    }
}
