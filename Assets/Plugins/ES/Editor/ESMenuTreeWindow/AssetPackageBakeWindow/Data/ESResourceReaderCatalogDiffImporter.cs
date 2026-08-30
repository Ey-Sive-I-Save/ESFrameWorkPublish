using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [Serializable] internal sealed class ESResourceReaderCatalogDiffImportEntry { public string guid; public string sourceId; public string sourcePath; public string objectStableId; public string beforeSha256; public string afterSha256; }
    [Serializable] internal sealed class ESResourceReaderCatalogDiffImportData { public string diffId; public string baselineSha256; public string currentSha256; public int addedCount; public int removedCount; public int changedCount; public ESResourceReaderCatalogDiffImportEntry[] added; public ESResourceReaderCatalogDiffImportEntry[] removed; public ESResourceReaderCatalogDiffImportEntry[] changed; }

    internal static class ESResourceReaderCatalogDiffImporter
    {
        private const string Menu = "Assets/ES/资源读取/从 Catalog Diff 创建持久对象";
        [MenuItem(Menu, true)] private static bool ValidateImport() { var o = Selection.activeObject; var p = o == null ? string.Empty : AssetDatabase.GetAssetPath(o); return p.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(p); }
        [MenuItem(Menu)] public static void ImportSelectedDiff() { var p = AssetDatabase.GetAssetPath(Selection.activeObject); if (!string.IsNullOrEmpty(p)) CreateFromDiffFile(p); }

        public static ESResourceReaderCatalogDiffData CreateFromDiffFile(string jsonPath)
        {
            var diff = JsonUtility.FromJson<ESResourceReaderCatalogDiffImportData>(File.ReadAllText(jsonPath));
            if (diff == null || diff.diffId != "es-resource-reader.reference-catalog-diff.v1") { EditorUtility.DisplayDialog("Catalog Diff 导入失败", "JSON 不是有效的 reference catalog diff。", "确定"); return null; }
            string targetPath = Path.ChangeExtension(jsonPath, ".catalog-diff.asset");
            var data = AssetDatabase.LoadAssetAtPath<ESResourceReaderCatalogDiffData>(targetPath);
            if (data == null) { data = ScriptableObject.CreateInstance<ESResourceReaderCatalogDiffData>(); AssetDatabase.CreateAsset(data, targetPath); }
            data.baselineSha256 = diff.baselineSha256; data.currentSha256 = diff.currentSha256; data.addedCount = diff.addedCount; data.removedCount = diff.removedCount; data.changedCount = diff.changedCount;
            data.added = Convert(diff.added); data.removed = Convert(diff.removed); data.changed = Convert(diff.changed); data.Seal(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets(); Selection.activeObject = data; EditorGUIUtility.PingObject(data); return data;
        }
        private static List<ESResourceReaderCatalogDiffEntry> Convert(ESResourceReaderCatalogDiffImportEntry[] items) { var result = new List<ESResourceReaderCatalogDiffEntry>(); foreach (var i in items ?? Array.Empty<ESResourceReaderCatalogDiffImportEntry>()) if (i != null) result.Add(new ESResourceReaderCatalogDiffEntry { guid=i.guid, sourceId=i.sourceId, sourcePath=i.sourcePath, objectStableId=i.objectStableId, beforeSha256=i.beforeSha256, afterSha256=i.afterSha256 }); return result; }
    }
}
