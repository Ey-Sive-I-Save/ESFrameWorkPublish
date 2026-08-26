using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ES_Logic.Editor.Generation.Tests")]

namespace ES
{
    /// <summary>
    /// 旧相机 Definition 字段的显式迁移入口。禁止在 OnValidate、加载或运行时猜测旧字符串；
    /// 只有操作者明确点击菜单时才扫描项目并写入已验证的双键引用。
    /// </summary>
    public static class ESCameraDefinitionMigrationTool
    {
        private const string MenuRoot = "【ES】/内容制作/相机/迁移 Definition 引用/";
        private static readonly string[] ScanFolders =
        {
            "Assets/ESNormalAssets",
            "Assets/Resources",
            "Assets/Plugins/ES/3_Examples",
            "Assets/Scenes",
        };

        [MenuItem(MenuRoot + "验证旧资产", false, 150)]
        public static void Validate()
        {
            MigrationReport report = Scan(migrate: false);
            Report("验证", report);
        }

        [MenuItem(MenuRoot + "迁移已知旧资产", false, 151)]
        public static void MigrateKnown()
        {
            if (!EditorUtility.DisplayDialog(
                    "迁移相机 Definition 引用",
                    "仅迁移能由当前 ESCameraViewDefinition 唯一解析的旧字符串。每个实际变更资产会先写入 Library 可恢复快照；未知或歧义值会保留并报告，不会猜测。\n\n扫描范围：" + string.Join("、", ScanFolders),
                    "迁移", "取消"))
                {
                    return;
                }

            MigrationReport report = Scan(migrate: true);
            Report("迁移", report);
        }

        [MenuItem(MenuRoot + "还原最近一次迁移快照", false, 152)]
        public static void RestoreLatestBackup()
        {
            if (!MigrationBackupSession.TryRestoreLatest(out string result))
            {
                Debug.LogError("[ESCamera] " + result);
                EditorUtility.DisplayDialog("还原相机 Definition 迁移", result, "确定");
                return;
            }

            Debug.Log("[ESCamera] " + result);
            EditorUtility.DisplayDialog("还原相机 Definition 迁移", result, "确定");
        }

        private static MigrationReport Scan(bool migrate)
        {
            var report = new MigrationReport();
            Dictionary<string, ESCameraDefinitionReference> references = BuildReferenceMap(report);
            if (report.catalogErrors > 0)
                return report;

            MigrationBackupSession backupSession = migrate ? new MigrationBackupSession() : null;
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", ScanFolders);
            string[] assetGuids = AssetDatabase.FindAssets("t:ScriptableObject", ScanFolders);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var prefabPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                prefabPaths.Add(path);
                paths.Add(path);
            }

            for (int i = 0; i < assetGuids.Length; i++)
                paths.Add(AssetDatabase.GUIDToAssetPath(assetGuids[i]));

            var orderedPaths = new List<string>(paths);
            orderedPaths.Sort(StringComparer.Ordinal);
            int total = orderedPaths.Count;
            try
            {
                for (int i = 0; i < total; i++)
                {
                    string path = orderedPaths[i];
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "扫描相机 Definition 旧引用",
                            path,
                            total == 0 ? 1f : (float)i / total))
                    {
                        report.cancelled = true;
                        report.messages.Add("操作者取消；已处理的资产保持其已完成状态。");
                        break;
                    }

                    try
                    {
                        if (prefabPaths.Contains(path))
                            ScanPrefab(path, references, migrate, backupSession, report);
                        else
                            ScanAsset(path, references, migrate, backupSession, report);
                    }
                    catch (Exception exception)
                    {
                        report.assetFailures++;
                        report.messages.Add(path + " :: " + exception.GetType().Name + " :: " + exception.Message);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (backupSession != null && !backupSession.TryComplete(out string completionError))
            {
                report.backupFailures++;
                report.messages.Add("迁移快照未完成 :: " + completionError);
            }

            report.backupPath = backupSession?.BackupPath;

            return report;
        }

        private static Dictionary<string, ESCameraDefinitionReference> BuildReferenceMap(MigrationReport report)
        {
            var result = new Dictionary<string, ESCameraDefinitionReference>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:ESCameraViewDefinitionCatalog", new[] { "Assets" });
            if (guids.Length != 1)
            {
                report.catalogErrors++;
                report.messages.Add("迁移要求唯一有效的 ESCameraViewDefinitionCatalog，实际数量=" + guids.Length);
                return result;
            }

            ESCameraViewDefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var definitions = new List<ESCameraViewDefinition>();
            string catalogError = null;
            if (catalog == null || !catalog.TryCopyDefinitionsForAuthoring(definitions, out catalogError))
            {
                report.catalogErrors++;
                report.messages.Add(catalogError ?? "无法读取 ESCameraViewDefinitionCatalog。");
                return result;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                ESCameraViewDefinition definition = definitions[i];
                if (definition == null || !definition.IsValid || string.IsNullOrWhiteSpace(definition.Definition.stringKey))
                {
                    report.catalogErrors++;
                    report.messages.Add("Catalog 包含无效 Definition。");
                    continue;
                }

                if (result.ContainsKey(definition.Definition.stringKey)
                    || ContainsEnumKey(result, definition.Definition))
                {
                    report.catalogErrors++;
                    report.messages.Add("Catalog Definition 别名冲突：" + definition.Definition);
                    continue;
                }

                result.Add(definition.Definition.stringKey, definition.Definition);
            }

            if (result.Count == 0)
            {
                report.catalogErrors++;
                report.messages.Add("未找到可迁移的 ESCameraViewDefinition。");
            }

            return result;
        }

        private static bool ContainsEnumKey(
            Dictionary<string, ESCameraDefinitionReference> references,
            ESCameraDefinitionReference candidate)
        {
            if (candidate.enumKey == ESCameraDefinitionEnumKey.None)
                return false;

            foreach (ESCameraDefinitionReference existing in references.Values)
            {
                if (existing.enumKey == candidate.enumKey && existing != candidate)
                    return true;
            }

            return false;
        }

        private static void ScanPrefab(
            string path,
            Dictionary<string, ESCameraDefinitionReference> references,
            bool migrate,
            MigrationBackupSession backupSession,
            MigrationReport report)
        {
            GameObject root = null;
            try
            {
                if (TryGetDirtyAssetError(path, out string dirtyError))
                {
                    report.assetFailures++;
                    report.messages.Add(path + " :: " + dirtyError);
                    return;
                }

                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    throw new InvalidOperationException("无法加载 Prefab Contents。");

                bool changed = false;
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    changed |= ScanObject(component, path, references, migrate, backupSession, report);

                if (migrate && changed)
                {
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new IOException("Prefab 保存失败。");

                    backupSession.MarkMigrated(path);
                }
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ScanAsset(
            string path,
            Dictionary<string, ESCameraDefinitionReference> references,
            bool migrate,
            MigrationBackupSession backupSession,
            MigrationReport report)
        {
            bool changed = false;
            UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < objects.Length; i++)
                changed |= ScanObject(objects[i], path, references, migrate, backupSession, report);

            if (migrate && changed)
            {
                UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                if (mainAsset == null)
                    throw new InvalidOperationException("无法保存已迁移的资产。");

                AssetDatabase.SaveAssetIfDirty(mainAsset);
                backupSession.MarkMigrated(path);
            }
        }

        internal static bool ScanObject(
            UnityEngine.Object target,
            string path,
            Dictionary<string, ESCameraDefinitionReference> references,
            bool migrate,
            MigrationBackupSession backupSession,
            MigrationReport report)
        {
            if (target == null || target is MonoScript)
                return false;

            using (SerializedObject serialized = new SerializedObject(target))
            {
                SerializedProperty iterator = serialized.GetIterator();
                bool changed = false;
                while (iterator.Next(true))
                {
                    string targetField = ResolveTargetField(iterator.name);
                    if (targetField == null || iterator.propertyType != SerializedPropertyType.String || string.IsNullOrWhiteSpace(iterator.stringValue))
                        continue;

                report.legacyFound++;
                string legacyKey = iterator.stringValue;
                if (!references.TryGetValue(legacyKey, out ESCameraDefinitionReference reference))
                {
                    report.unknown++;
                    report.messages.Add(path + " :: " + iterator.propertyPath + " = '" + legacyKey + "'");
                    continue;
                }

                string parentPath = GetParentPath(iterator.propertyPath);
                SerializedProperty definition = serialized.FindProperty(string.IsNullOrEmpty(parentPath)
                    ? targetField
                    : parentPath + "." + targetField);
                SerializedProperty enumKey = definition?.FindPropertyRelative(nameof(ESCameraDefinitionReference.enumKey));
                SerializedProperty stringKey = definition?.FindPropertyRelative(nameof(ESCameraDefinitionReference.stringKey));
                if (enumKey == null || stringKey == null)
                {
                    report.unknown++;
                    report.messages.Add(path + " :: 无法定位目标字段 " + targetField);
                    continue;
                }

                var current = new ESCameraDefinitionReference(
                    (ESCameraDefinitionEnumKey)enumKey.intValue,
                    stringKey.stringValue);
                if (!IsReferenceEmpty(current) && current != reference)
                {
                    report.conflicts++;
                    report.messages.Add(path + " :: " + iterator.propertyPath
                                        + " 的新引用 " + current
                                        + " 与旧键 '" + legacyKey + "' 解析值 " + reference + " 冲突，已拒绝。");
                    continue;
                }

                if (!migrate)
                    continue;

                if (!backupSession.TryCapture(path, out string backupError))
                {
                    report.backupFailures++;
                    report.messages.Add(path + " :: 无法创建迁移快照 :: " + backupError);
                    continue;
                }

                Undo.RecordObject(target, "迁移相机 Definition 引用");
                if (IsReferenceEmpty(current))
                {
                    enumKey.intValue = (int)reference.enumKey;
                    stringKey.stringValue = reference.stringKey;
                    report.migrated++;
                }
                else
                {
                    report.legacyCleared++;
                }

                    iterator.stringValue = string.Empty;
                    changed = true;
                }

                if (!changed)
                    return false;

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                report.changedPaths.Add(path);
                return true;
            }
        }

        private static bool TryGetDirtyAssetError(string assetPath, out string error)
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null
                && string.Equals(prefabStage.assetPath, assetPath, StringComparison.Ordinal))
            {
                if (prefabStage.scene.isDirty || IsDirtyPrefabStage(prefabStage.prefabContentsRoot))
                {
                    error = "Prefab Stage 尚未保存，拒绝覆盖其磁盘恢复点。";
                    return true;
                }
            }

            UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && EditorUtility.IsDirty(objects[i]))
                {
                    error = "目标资产或其 SubAsset 已有未保存修改，拒绝覆盖其磁盘恢复点。";
                    return true;
                }
            }

            error = null;
            return false;
        }

        private static bool IsDirtyPrefabStage(GameObject root)
        {
            if (root == null)
                return false;

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && EditorUtility.IsDirty(components[i]))
                    return true;
            }

            return false;
        }

        private static bool IsReferenceEmpty(ESCameraDefinitionReference reference)
        {
            return reference.enumKey == ESCameraDefinitionEnumKey.None
                   && string.IsNullOrWhiteSpace(reference.stringKey);
        }

        private static string ResolveTargetField(string legacyField)
        {
            switch (legacyField)
            {
                case "legacyDefaultCameraDefinitionKey": return "defaultCameraDefinition";
                case "legacyDriverCameraDefinitionKey": return "driverCameraDefinition";
                case "legacyDefinitionKey": return "definition";
                default: return null;
            }
        }

        private static string GetParentPath(string propertyPath)
        {
            int lastDot = propertyPath.LastIndexOf('.');
            return lastDot >= 0 ? propertyPath.Substring(0, lastDot) : string.Empty;
        }

        private static void Report(string operation, MigrationReport report)
        {
            string summary = operation + "完成：旧值=" + report.legacyFound
                              + "，已迁移=" + report.migrated
                              + "，仅清理旧键=" + report.legacyCleared
                              + "，新旧冲突=" + report.conflicts
                              + "，未知/拒绝=" + report.unknown
                              + "，Catalog 错误=" + report.catalogErrors
                              + "，资产失败=" + report.assetFailures
                              + "，快照失败=" + report.backupFailures
                              + "，已取消=" + report.cancelled;
            if (!string.IsNullOrEmpty(report.backupPath))
                summary += "\n快照目录=" + report.backupPath;
            if (report.messages.Count > 0)
                summary += "\n\n" + string.Join("\n", report.messages);

            if (report.unknown > 0 || report.conflicts > 0 || report.catalogErrors > 0 || report.assetFailures > 0 || report.backupFailures > 0 || report.cancelled)
                Debug.LogError("[ESCamera] " + summary);
            else
                Debug.Log("[ESCamera] " + summary);

            EditorUtility.DisplayDialog("相机 Definition " + operation, summary, "确定");
        }

        internal sealed class MigrationReport
        {
            public int legacyFound;
            public int migrated;
            public int legacyCleared;
            public int conflicts;
            public int unknown;
            public int catalogErrors;
            public int assetFailures;
            public int backupFailures;
            public bool cancelled;
            public string backupPath;
            public readonly List<string> messages = new List<string>();
            public readonly HashSet<string> changedPaths = new HashSet<string>(StringComparer.Ordinal);
        }

        internal sealed class MigrationBackupSession
        {
            private const string BackupDirectoryName = "ESCameraDefinitionMigrationBackups";
            private const string ManifestName = "manifest.json";
            private readonly string backupPath;
            internal readonly List<BackupEntry> entries = new List<BackupEntry>();
            private readonly HashSet<string> capturedPaths = new HashSet<string>(StringComparer.Ordinal);
            internal static Action<BackupEntry> TestBeforeRestoreCopy;
            internal static string TestRestoreRootOverride;

            public MigrationBackupSession()
                : this(Path.Combine(GetProjectRoot(), "Library", BackupDirectoryName,
                    DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N")))
            {
            }

            internal MigrationBackupSession(string backupPath)
            {
                if (string.IsNullOrWhiteSpace(backupPath))
                    throw new ArgumentException("快照目录不能为空。", nameof(backupPath));
                this.backupPath = backupPath;
            }

            public string BackupPath => entries.Count > 0 ? backupPath : null;
            internal string SnapshotPath => backupPath;

            public bool TryCapture(string assetPath, out string error)
            {
                error = null;
                if (capturedPaths.Contains(assetPath))
                    return true;

                try
                {
                    if (TryGetDirtyAssetError(assetPath, out error))
                        return false;

                    string sourcePath = ToAbsoluteProjectPath(assetPath);
                    if (!File.Exists(sourcePath))
                    {
                        error = "资产文件不存在。";
                        return false;
                    }

                    Directory.CreateDirectory(backupPath);
                    string fileName = entries.Count.ToString("D5") + "-" + Path.GetFileName(assetPath);
                    string backupFile = Path.Combine(backupPath, fileName);
                    File.Copy(sourcePath, backupFile, overwrite: false);

                    string sourceMetaPath = sourcePath + ".meta";
                    if (!File.Exists(sourceMetaPath))
                    {
                        error = "资产 .meta 不存在，拒绝迁移以保护 GUID。";
                        return false;
                    }

                    string backupMetaFile = backupFile + ".meta";
                    File.Copy(sourceMetaPath, backupMetaFile, overwrite: false);

                    entries.Add(new BackupEntry
                    {
                        assetPath = assetPath,
                        backupFile = fileName,
                        backupMetaFile = fileName + ".meta",
                        assetHashBefore = ComputeFileHash(backupFile),
                        metaHashBefore = ComputeFileHash(backupMetaFile),
                    });
                    capturedPaths.Add(assetPath);
                    WriteManifest(isComplete: false);
                    return true;
                }
                catch (Exception exception)
                {
                    error = exception.GetType().Name + " :: " + exception.Message;
                    return false;
                }
            }

            public void MarkMigrated(string assetPath)
            {
                BackupEntry entry = entries.Find(item => string.Equals(item.assetPath, assetPath, StringComparison.Ordinal));
                if (entry == null)
                    throw new InvalidOperationException("没有对应的迁移快照：" + assetPath);

                string sourcePath = ToAbsoluteProjectPath(assetPath);
                string metaPath = sourcePath + ".meta";
                if (!File.Exists(sourcePath) || !File.Exists(metaPath))
                    throw new InvalidOperationException("迁移后资产或 .meta 缺失：" + assetPath);

                entry.assetHashAfterMigration = ComputeFileHash(sourcePath);
                entry.metaHashAfterMigration = ComputeFileHash(metaPath);
                WriteManifest(isComplete: false);
            }

            public bool TryComplete(out string error)
            {
                error = null;
                if (entries.Count == 0)
                    return true;

                for (int i = 0; i < entries.Count; i++)
                {
                    if (string.IsNullOrEmpty(entries[i].assetHashAfterMigration)
                        || string.IsNullOrEmpty(entries[i].metaHashAfterMigration))
                    {
                        string incompletePath = entries[i].assetPath;
                        if (TryRestoreTransaction(entries, backupPath, out string rollbackError))
                        {
                            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                            error = "迁移后哈希未完整记录，已回滚本次迁移：" + incompletePath;
                        }
                        else
                        {
                            error = "迁移后哈希未完整记录，且自动回滚失败：" + incompletePath + "；" + rollbackError;
                        }

                        return false;
                    }
                }

                try
                {
                    WriteManifest(isComplete: true);
                    return true;
                }
                catch (Exception exception)
                {
                    if (TryRestoreTransaction(entries, backupPath, out string rollbackError))
                    {
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        error = "完成 Manifest 发布失败，已回滚本次迁移："
                                + exception.GetType().Name + " :: " + exception.Message;
                    }
                    else
                    {
                        error = "完成 Manifest 发布失败，且自动回滚失败："
                                + exception.GetType().Name + " :: " + exception.Message + "；" + rollbackError;
                    }

                    return false;
                }
            }

            public static bool TryRestoreLatest(out string result)
            {
                if (!TrySelectRestorableManifest(out BackupManifest manifest, out string selectedDirectory, out string error))
                {
                    result = error;
                    return false;
                }

                if (!EditorUtility.DisplayDialog(
                        "还原相机 Definition 迁移",
                        "将覆盖最近一次可验证快照涉及的 " + manifest.entries.Count + " 个资产。已检查目标不存在未保存修改。",
                        "还原", "取消"))
                {
                    result = "操作者取消还原。";
                    return false;
                }

                if (!TryRestoreTransaction(manifest.entries, selectedDirectory, out string restoreError))
                {
                    result = restoreError;
                    return false;
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                result = "已还原 " + manifest.entries.Count + " 个资产，来源：" + selectedDirectory;
                return true;
            }

            internal static bool TrySelectRestorableManifest(
                out BackupManifest manifest,
                out string selectedDirectory,
                out string error)
            {
                manifest = null;
                selectedDirectory = null;
                error = null;
                string root = string.IsNullOrEmpty(TestRestoreRootOverride)
                    ? Path.Combine(GetProjectRoot(), "Library", BackupDirectoryName)
                    : TestRestoreRootOverride;
                if (!Directory.Exists(root))
                {
                    error = "没有可还原的相机 Definition 迁移快照。";
                    return false;
                }

                string[] directories = Directory.GetDirectories(root);
                if (directories.Length == 0)
                {
                    error = "没有可还原的相机 Definition 迁移快照。";
                    return false;
                }

                Array.Sort(directories, (left, right) => string.CompareOrdinal(right, left));
                string lastSkipped = null;
                for (int i = 0; i < directories.Length; i++)
                {
                    string candidateDirectory = directories[i];
                    string manifestPath = Path.Combine(candidateDirectory, ManifestName);
                    if (!File.Exists(manifestPath))
                        continue;

                    BackupManifest candidate;
                    try
                    {
                        candidate = JsonUtility.FromJson<BackupManifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (candidate == null || !candidate.isComplete || candidate.entries == null || candidate.entries.Count == 0)
                        continue;

                    string skipped = null;
                    for (int entryIndex = 0; entryIndex < candidate.entries.Count; entryIndex++)
                    {
                        RestoreEntryStatus status = TryValidateRestoreEntry(
                            candidateDirectory,
                            candidate.entries[entryIndex],
                            out string detail);
                        if (status == RestoreEntryStatus.Block)
                        {
                            error = detail;
                            return false;
                        }

                        if (status == RestoreEntryStatus.Skip)
                        {
                            skipped = detail;
                            break;
                        }
                    }

                    if (skipped == null)
                    {
                        manifest = candidate;
                        selectedDirectory = candidateDirectory;
                        return true;
                    }

                    if (lastSkipped == null)
                        lastSkipped = skipped;
                }

                error = lastSkipped ?? "没有完整、可还原的相机 Definition 迁移快照。";
                return false;
            }

            private enum RestoreEntryStatus
            {
                Valid,
                Skip,
                Block,
            }

            private static RestoreEntryStatus TryValidateRestoreEntry(
                string directory,
                BackupEntry entry,
                out string detail)
            {
                detail = null;
                try
                {
                    if (TryGetDirtyAssetError(entry.assetPath, out string dirtyError))
                    {
                        detail = dirtyError;
                        return RestoreEntryStatus.Block;
                    }

                    string targetPath = ToAbsoluteProjectPath(entry.assetPath);
                    entry.resolvedBackupFile = ResolveContainedFile(directory, entry.backupFile);
                    entry.resolvedBackupMetaFile = ResolveContainedFile(directory, entry.backupMetaFile);
                    if (!File.Exists(entry.resolvedBackupFile) || !File.Exists(entry.resolvedBackupMetaFile)
                        || !File.Exists(targetPath) || !File.Exists(targetPath + ".meta"))
                    {
                        detail = "快照或目标 .meta 缺失，跳过候选：" + entry.assetPath;
                        return RestoreEntryStatus.Skip;
                    }

                    if (!FileHashEquals(entry.assetHashBefore, entry.resolvedBackupFile)
                        || !FileHashEquals(entry.metaHashBefore, entry.resolvedBackupMetaFile))
                    {
                        detail = "快照哈希不匹配，跳过候选：" + entry.assetPath;
                        return RestoreEntryStatus.Skip;
                    }

                    if (!FileHashEquals(entry.assetHashAfterMigration, targetPath)
                        || !FileHashEquals(entry.metaHashAfterMigration, targetPath + ".meta"))
                    {
                        detail = "目标资产已在迁移后漂移，跳过候选：" + entry.assetPath;
                        return RestoreEntryStatus.Skip;
                    }

                    return RestoreEntryStatus.Valid;
                }
                catch (Exception exception)
                {
                    detail = "快照无法安全恢复，跳过候选：" + entry.assetPath
                             + " :: " + exception.GetType().Name + " :: " + exception.Message;
                    return RestoreEntryStatus.Skip;
                }
            }

            private void WriteManifest(bool isComplete)
            {
                var manifest = new BackupManifest { entries = entries, isComplete = isComplete };
                string manifestPath = Path.Combine(backupPath, ManifestName);
                ESManagedFileIO.WriteTextAtomic(
                    manifestPath,
                    JsonUtility.ToJson(manifest, prettyPrint: true),
                    new UTF8Encoding(false),
                    backupPath);
            }

            private static string GetProjectRoot()
            {
                return Directory.GetParent(Application.dataPath).FullName;
            }

            private static string ToAbsoluteProjectPath(string assetPath)
            {
                if (string.IsNullOrWhiteSpace(assetPath)
                    || Path.IsPathRooted(assetPath)
                    || !assetPath.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Manifest 资产路径不在项目 Assets 内。");

                string projectRoot = GetProjectRoot();
                string assetsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Assets")) + Path.DirectorySeparatorChar;
                string resolved = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
                if (!resolved.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Manifest 资产路径越界。");

                return resolved;
            }

            private static string ResolveContainedFile(string root, string fileName)
            {
                if (string.IsNullOrWhiteSpace(fileName)
                    || Path.IsPathRooted(fileName)
                    || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
                    throw new InvalidOperationException("Manifest 快照文件名越界。");

                string resolvedRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
                string resolved = Path.GetFullPath(Path.Combine(root, fileName));
                if (!resolved.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Manifest 快照文件路径越界。");

                return resolved;
            }

            internal static bool TryRestoreTransaction(List<BackupEntry> entries, string snapshotDirectory, out string error)
            {
                string rollbackPath = Path.Combine(GetProjectRoot(), "Library", BackupDirectoryName,
                    "restore-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N"));
                var applied = new List<BackupEntry>();
                try
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        BackupEntry entry = entries[i];
                        entry.resolvedBackupFile = ResolveContainedFile(snapshotDirectory, entry.backupFile);
                        entry.resolvedBackupMetaFile = ResolveContainedFile(snapshotDirectory, entry.backupMetaFile);
                        if (!FileHashEquals(entry.assetHashBefore, entry.resolvedBackupFile)
                            || !FileHashEquals(entry.metaHashBefore, entry.resolvedBackupMetaFile))
                            throw new IOException("迁移前快照哈希不匹配：" + entry.assetPath);
                    }

                    Directory.CreateDirectory(rollbackPath);
                    for (int i = 0; i < entries.Count; i++)
                    {
                        BackupEntry entry = entries[i];
                        string targetPath = ToAbsoluteProjectPath(entry.assetPath);
                        entry.rollbackFile = Path.Combine(rollbackPath, i.ToString("D5") + "-" + Path.GetFileName(entry.assetPath));
                        entry.rollbackMetaFile = entry.rollbackFile + ".meta";
                        File.Copy(targetPath, entry.rollbackFile, overwrite: false);
                        File.Copy(targetPath + ".meta", entry.rollbackMetaFile, overwrite: false);
                        entry.rollbackAssetHash = ComputeFileHash(entry.rollbackFile);
                        entry.rollbackMetaHash = ComputeFileHash(entry.rollbackMetaFile);
                    }

                    for (int i = 0; i < entries.Count; i++)
                    {
                        BackupEntry entry = entries[i];
                        string targetPath = ToAbsoluteProjectPath(entry.assetPath);
                        if (TestBeforeRestoreCopy != null)
                            TestBeforeRestoreCopy(entry);
                        File.Copy(entry.resolvedBackupFile, targetPath, overwrite: true);
                        applied.Add(entry);
                        File.Copy(entry.resolvedBackupMetaFile, targetPath + ".meta", overwrite: true);
                        if (!FileHashEquals(entry.assetHashBefore, targetPath)
                            || !FileHashEquals(entry.metaHashBefore, targetPath + ".meta"))
                            throw new IOException("恢复后哈希不匹配：" + entry.assetPath);

                    }

                    error = null;
                    return true;
                }
                catch (Exception exception)
                {
                    bool rollbackSucceeded = true;
                    for (int i = applied.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            BackupEntry entry = applied[i];
                            string targetPath = ToAbsoluteProjectPath(entry.assetPath);
                            File.Copy(entry.rollbackFile, targetPath, overwrite: true);
                            File.Copy(entry.rollbackMetaFile, targetPath + ".meta", overwrite: true);
                            if (!FileHashEquals(entry.rollbackAssetHash, targetPath)
                                || !FileHashEquals(entry.rollbackMetaHash, targetPath + ".meta"))
                                throw new IOException("回滚后哈希不匹配：" + entry.assetPath);
                        }
                        catch (Exception)
                        {
                            rollbackSucceeded = false;
                        }
                    }

                    error = "恢复事务失败：" + exception.GetType().Name + " :: " + exception.Message
                            + (rollbackSucceeded ? "；已回滚已覆盖资产。" : "；回滚失败，需从快照目录人工恢复。");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return false;
                }
            }

            internal static string ComputeFileHash(string path)
            {
                using (SHA256 algorithm = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                    return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
            }

            private static bool FileHashEquals(string expectedHash, string path)
            {
                return !string.IsNullOrEmpty(expectedHash)
                       && File.Exists(path)
                       && string.Equals(expectedHash, ComputeFileHash(path), StringComparison.Ordinal);
            }
        }

        [Serializable]
        internal sealed class BackupManifest
        {
            public bool isComplete;
            public List<BackupEntry> entries;
        }

        [Serializable]
        internal sealed class BackupEntry
        {
            public string assetPath;
            public string backupFile;
            public string backupMetaFile;
            public string assetHashBefore;
            public string metaHashBefore;
            public string assetHashAfterMigration;
            public string metaHashAfterMigration;
            [NonSerialized] public string resolvedBackupFile;
            [NonSerialized] public string resolvedBackupMetaFile;
            [NonSerialized] public string rollbackFile;
            [NonSerialized] public string rollbackMetaFile;
            [NonSerialized] public string rollbackAssetHash;
            [NonSerialized] public string rollbackMetaHash;
        }
    }
}
