using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [Serializable] internal sealed class ESResourceReaderShardManifestImportItem { public string prefix; public string path; public int guidCount; public int edgeCount; public string sha256; }
    [Serializable] internal sealed class ESResourceReaderShardManifestImportData { public string manifestId; public string catalogSha256; public int prefixLength; public ESResourceReaderShardManifestImportItem[] shards; }
    internal static class ESResourceReaderReferenceShardManifestImporter
    {
        private const string Menu = "Assets/ES/资源读取/从分片 Manifest 创建持久对象";
        [MenuItem(Menu, true)] private static bool ValidateImport() { var o = Selection.activeObject; var p = o == null ? string.Empty : AssetDatabase.GetAssetPath(o); return p.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(p); }
        [MenuItem(Menu)] public static void ImportSelectedManifest() { var p = AssetDatabase.GetAssetPath(Selection.activeObject); if (!string.IsNullOrEmpty(p)) CreateFromManifestFile(p); }
        public static void RefreshSelectedManifest()
        {
            var p = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(p) || !p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("分片 Manifest 刷新", "请先在 Project 窗口选中一个分片 Manifest JSON。", "确定");
                return;
            }
            CreateFromManifestFile(p);
        }
        public static ESResourceReaderReferenceShardManifestData CreateFromManifestFile(string jsonPath)
        {
            var m = JsonUtility.FromJson<ESResourceReaderShardManifestImportData>(File.ReadAllText(jsonPath));
            if (m == null || m.manifestId != "es-resource-reader.reference-catalog-shards.v1") { EditorUtility.DisplayDialog("分片 Manifest 导入失败", "JSON 不是有效的 Reference 分片 Manifest。", "确定"); return null; }
            string targetPath = Path.ChangeExtension(jsonPath, ".reference-shards.asset"); var data = AssetDatabase.LoadAssetAtPath<ESResourceReaderReferenceShardManifestData>(targetPath);
            if (data == null) { data = ScriptableObject.CreateInstance<ESResourceReaderReferenceShardManifestData>(); AssetDatabase.CreateAsset(data, targetPath); }
            data.catalogSha256 = m.catalogSha256; data.prefixLength = m.prefixLength; data.shards = new List<ESResourceReaderReferenceShardInfo>();
            foreach (var s in m.shards ?? Array.Empty<ESResourceReaderShardManifestImportItem>()) if (s != null) data.shards.Add(new ESResourceReaderReferenceShardInfo { prefix=s.prefix, path=s.path, guidCount=s.guidCount, edgeCount=s.edgeCount, sha256=s.sha256 });
            data.Seal(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets(); Selection.activeObject = data; EditorGUIUtility.PingObject(data); return data;
        }
    }
}
