#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [Serializable] internal sealed class ESResourceCollectionBatchImportFile { public string path; public string status; public string sha256; }
    [Serializable] internal sealed class ESResourceCollectionBatchImportData { public string batchId; public string root; public bool canceled; public ESResourceCollectionBatchImportFile[] files; }

    internal static class ESResourceCollectionBatchImporter
    {
        [MenuItem("ES/资源读取/将已验证批快照加入 AssetPackage 候选")]
        public static void ImportValidatedBatchToSelectedBake()
        {
            ESAssetPackageBakeData bake = ESAssetPackageBakeWindow.GetSelectedBakeForResourceCollection();
            if (bake == null) { EditorUtility.DisplayDialog("批快照聚合", "请先选择 AssetPackage 烘焙配置。", "确定"); return; }
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string jsonPath = Path.Combine(projectRoot, "ES/Output/ResourceCollection/collection-batch.json");
            if (!File.Exists(jsonPath)) { EditorUtility.DisplayDialog("批快照聚合", "未找到批快照，请先运行 Invoke-ESResourceCollectionBatch.ps1。", "确定"); return; }
            ESResourceCollectionBatchImportData batch;
            try { batch = JsonUtility.FromJson<ESResourceCollectionBatchImportData>(File.ReadAllText(jsonPath)); }
            catch (Exception e) { EditorUtility.DisplayDialog("批快照聚合", "批快照 JSON 无法解析：" + e.Message, "确定"); return; }
            if (batch == null || batch.batchId != "es-resource-collection.batch.v1") { EditorUtility.DisplayDialog("批快照聚合", "批快照合同无效。", "确定"); return; }
            if (batch.canceled) { EditorUtility.DisplayDialog("批快照聚合", "批快照处于已取消状态，请先完成一次新的批处理。", "确定"); return; }
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (bake.records == null) bake.records = new List<ESAssetPackageBakeRecord>();
            foreach (var r in bake.records ?? new List<ESAssetPackageBakeRecord>()) if (r != null && !string.IsNullOrEmpty(r.guid)) existing.Add(r.guid);
            int added = 0, skipped = 0;
            Undo.RecordObject(bake, "导入资源收集批快照候选");
            foreach (var f in batch.files ?? Array.Empty<ESResourceCollectionBatchImportFile>())
            {
                if (f == null || string.IsNullOrWhiteSpace(f.path)) { skipped++; continue; }
                if (f.status != "parsed" && f.status != "reused") { skipped++; continue; }
                string full = Path.IsPathRooted(f.path) ? f.path : Path.Combine(batch.root ?? projectRoot, f.path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full) || !string.Equals(ComputeSha256(full), f.sha256, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                string assetPath = ToAssetPath(full, projectRoot);
                if (string.IsNullOrEmpty(assetPath)) { skipped++; continue; }
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid) || !existing.Add(guid)) { skipped++; continue; }
                Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                ESAssetPackageCategory category = ESAssetPackageBakeUtility.DetermineCategory(assetPath, type);
                var record = new ESAssetPackageBakeRecord { selectedForUse = false, category = category, assetName = Path.GetFileNameWithoutExtension(assetPath), assetPath = assetPath, guid = guid, typeName = type != null ? type.Name : "Unknown", exportSubFolder = bake.GetConfiguredExportSubFolder(category) };
                bake.records.Add(record); added++;
            }
            bake.records.Sort((a,b) => { int c=a.category.CompareTo(b.category); return c != 0 ? c : string.Compare(a.assetPath,b.assetPath,StringComparison.OrdinalIgnoreCase); });
            bake.RebuildStats(); bake.MarkAnalysisStale(); EditorUtility.SetDirty(bake); AssetDatabase.SaveAssetIfDirty(bake);
            EditorUtility.DisplayDialog("批快照聚合", $"已加入候选：{added}；跳过：{skipped}。外部路径保持为收集快照，不会伪造 Unity GUID。", "确定");
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ToAssetPath(string fullPath, string projectRoot)
        {
            string full = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assetsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Assets"));
            if (!full.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !string.Equals(full, assetsRoot, StringComparison.OrdinalIgnoreCase)) return string.Empty;
            return full.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
#endif
