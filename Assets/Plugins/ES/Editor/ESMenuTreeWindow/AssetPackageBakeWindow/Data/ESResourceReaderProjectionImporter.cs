using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [Serializable]
    internal sealed class ESProjectionPacketImportEntry
    {
        public string stableId;
        public string relativePath;
        public string entryType;
        public string guid;
        public string fileId;
        public string groupId;
        public string pathname;
    }

    [Serializable]
    internal sealed class ESProjectionPacketImportData
    {
        public int projectionVersion;
        public string sourcePath;
        public string sourceSha256;
        public string parserId;
        public string detectedFormat;
        public ESProjectionPacketImportEntry[] entries;
        public string[] warnings;
        public string[] errors;
        public string[] nonClaims;
    }

    internal static class ESResourceReaderProjectionImporter
    {
        private const string Menu = "Assets/ES/资源读取/导入 Projection 数据";

        [MenuItem(Menu, true)]
        private static bool ValidateImport()
        {
            var obj = Selection.activeObject;
            if (obj == null) return false;
            var path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem(Menu)]
        public static void ImportSelectedProjection()
        {
            var jsonPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(jsonPath)) return;
            var json = File.ReadAllText(jsonPath);
            var packet = JsonUtility.FromJson<ESProjectionPacketImportData>(json);
            if (packet == null || packet.projectionVersion <= 0 || string.IsNullOrWhiteSpace(packet.sourceSha256))
            {
                EditorUtility.DisplayDialog("Projection 导入失败", "JSON 不是有效的 ProjectionPacket。", "确定");
                return;
            }

            var targetPath = Path.ChangeExtension(jsonPath, ".projection.asset");
            var data = AssetDatabase.LoadAssetAtPath<ESResourceReaderProjectionData>(targetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ESResourceReaderProjectionData>();
                AssetDatabase.CreateAsset(data, targetPath);
            }

            data.sourcePath = packet.sourcePath;
            data.sourceSha256 = packet.sourceSha256;
            data.parserId = packet.parserId;
            data.parserVersion = "1";
            data.detectedFormat = packet.detectedFormat;
            data.entries = new List<ESResourceReaderProjectionEntry>();
            foreach (var item in packet.entries ?? Array.Empty<ESProjectionPacketImportEntry>())
            {
                data.entries.Add(new ESResourceReaderProjectionEntry
                {
                    stableId = !string.IsNullOrEmpty(item.stableId) ? item.stableId : (!string.IsNullOrEmpty(item.groupId) ? item.groupId : (!string.IsNullOrEmpty(item.guid) ? item.guid : item.pathname)),
                    relativePath = !string.IsNullOrEmpty(item.relativePath) ? item.relativePath : item.pathname,
                    entryType = item.entryType,
                    guid = item.guid,
                    fileId = item.fileId
                });
            }
            data.entryCount = data.entries.Count;
            data.warnings = new List<string>(packet.warnings ?? Array.Empty<string>());
            data.errors = new List<string>(packet.errors ?? Array.Empty<string>());
            data.nonClaims = new List<string>(packet.nonClaims ?? Array.Empty<string>());
            data.rawProjectionJson = json;
            var summaryMatch = Regex.Match(json, "\\\"summary\\\"\\s*:\\s*(\\{.*?\\})\\s*,\\s*\\\"entries\\\"", RegexOptions.Singleline);
            data.summaryJson = summaryMatch.Success ? summaryMatch.Groups[1].Value : "{}";
            data.Seal();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssociateBakeRecords(data);
            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
        }

        private static void AssociateBakeRecords(ESResourceReaderProjectionData projection)
        {
            var projectionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in projection.entries ?? new List<ESResourceReaderProjectionEntry>())
                if (entry != null && !string.IsNullOrEmpty(entry.relativePath))
                    projectionPaths.Add(entry.relativePath.Replace('\\', '/'));
            foreach (var guid in AssetDatabase.FindAssets("t:ESAssetPackageBakeData"))
            {
                var bake = AssetDatabase.LoadAssetAtPath<ESAssetPackageBakeData>(AssetDatabase.GUIDToAssetPath(guid));
                if (bake == null || bake.records == null) continue;
                var changed = false;
                foreach (var record in bake.records)
                {
                    if (record == null) continue;
                    var path = (record.assetPath ?? string.Empty).Replace('\\', '/');
                    var recordGuid = record.guid ?? string.Empty;
                    var match = projectionPaths.Contains(path) || projection.entries.Exists(x => x != null && string.Equals(x.guid, recordGuid, StringComparison.OrdinalIgnoreCase));
                    if (!match) continue;
                    var stable = projection.entries.Find(x => x != null && (string.Equals(x.relativePath, path, StringComparison.OrdinalIgnoreCase) || string.Equals(x.guid, recordGuid, StringComparison.OrdinalIgnoreCase)))?.stableId;
                    if (record.readerProjectionStableId == stable && record.readerProjectionHash == projection.projectionHash) continue;
                    record.readerProjectionStableId = stable;
                    record.readerProjectionHash = projection.projectionHash;
                    changed = true;
                }
                if (changed) EditorUtility.SetDirty(bake);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
