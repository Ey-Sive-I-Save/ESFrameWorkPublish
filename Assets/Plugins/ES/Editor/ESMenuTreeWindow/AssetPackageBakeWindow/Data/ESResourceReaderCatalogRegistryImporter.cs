using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [Serializable]
    internal sealed class ESResourceReaderCatalogImportItem
    {
        public string sourceId;
        public string sourceIndexPath;
    }

    [Serializable]
    internal sealed class ESResourceReaderCatalogImportData
    {
        public string[] sources;
        public ESResourceReaderCatalogImportItem[] items;
    }

    internal static class ESResourceReaderCatalogRegistryImporter
    {
        private const string Menu = "Assets/ES/资源读取/从 Catalog 创建来源注册";

        [MenuItem(Menu, true)]
        private static bool ValidateImport()
        {
            var obj = Selection.activeObject;
            if (obj == null) return false;
            var path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem(Menu)]
        public static void ImportSelectedCatalog()
        {
            var jsonPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(jsonPath)) return;
            CreateFromCatalogFile(jsonPath);
        }

        public static ESResourceReaderCatalogRegistryData CreateFromCatalogFile(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            var catalog = JsonUtility.FromJson<ESResourceReaderCatalogImportData>(json);
            if (catalog == null || catalog.sources == null || catalog.sources.Length == 0)
            {
                EditorUtility.DisplayDialog("Catalog 注册失败", "JSON 未包含有效的 sources。", "确定");
                return null;
            }

            var targetPath = Path.ChangeExtension(jsonPath, ".catalog-registry.asset");
            var data = AssetDatabase.LoadAssetAtPath<ESResourceReaderCatalogRegistryData>(targetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ESResourceReaderCatalogRegistryData>();
                AssetDatabase.CreateAsset(data, targetPath);
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            data.sources = new List<ESResourceReaderCatalogSource>();
            foreach (var item in catalog.items ?? Array.Empty<ESResourceReaderCatalogImportItem>())
            {
                var sourceId = item != null ? item.sourceId : string.Empty;
                var indexPath = item != null ? item.sourceIndexPath : string.Empty;
                if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(indexPath) || !seen.Add(sourceId)) continue;
                data.sources.Add(new ESResourceReaderCatalogSource { sourceId = sourceId, sourceIndexPath = indexPath.Replace('\\', '/'), sourceKind = "catalog" });
            }
            if (data.sources.Count > 0)
            {
                data.Seal();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                Selection.activeObject = data;
                EditorGUIUtility.PingObject(data);
                return data;
            }
            seen.Clear();
            foreach (var sourcePath in catalog.sources)
            {
                var normalized = (sourcePath ?? string.Empty).Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized)) continue;
                data.sources.Add(new ESResourceReaderCatalogSource { sourceId = Path.GetFileNameWithoutExtension(normalized), sourceIndexPath = normalized, sourceKind = "catalog" });
            }
            data.Seal();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
            return data;
        }
    }
}
