using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ES_Logic.Editor.Generation.Tests")]

namespace ES
{
    /// <summary>
    /// Explicit one-version migration from the former Odin dictionaries to EntityTransformMap.
    /// This tool never scans unknown assets and never runs from editor initialization or a drawer.
    /// </summary>
    public static class ESEntityTransformMappingMigration
    {
        public static readonly string[] KnownPrefabPaths =
        {
            ESBasicCharacterTemplateBuilder.TemplatePath,
            ESBasicCharacterTemplateBuilder.CompleteTemplatePath,
            ESFormalHertaPlayerVariantBuilder.VariantPath,
        };

        [MenuItem("【ES】/内容制作/角色模板/挂点迁移/审计旧 Odin 挂点数据", false, 130)]
        public static void AuditKnownPrefabsMenu()
        {
            if (!TryAuditKnownPrefabs(out string report))
            {
                Debug.LogError(report);
                return;
            }

            Debug.Log(report);
        }

        [MenuItem("【ES】/内容制作/角色模板/挂点迁移/迁移已知角色 Prefab", false, 131)]
        public static void MigrateKnownPrefabsMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "迁移角色挂点",
                    "将三份已知角色 Prefab 的旧 Odin defaultMap/dynamicMap 显式迁移到 EntityTransformMap。迁移会先全量预检，冲突时不会保存任何资产。是否继续？",
                    "迁移并验证",
                    "取消"))
            {
                return;
            }

            if (!TryMigrateKnownPrefabs(out string report))
            {
                Debug.LogError(report);
                return;
            }

            Debug.Log(report, AssetDatabase.LoadAssetAtPath<GameObject>(ESFormalHertaPlayerVariantBuilder.VariantPath));
        }

        public static void AuditKnownPrefabsBatch()
        {
            if (!TryAuditKnownPrefabs(out string report))
                throw new InvalidOperationException(report);
            Debug.Log(report);
        }

        public static void MigrateKnownPrefabsBatch()
        {
            if (!TryMigrateKnownPrefabs(out string report))
                throw new InvalidOperationException(report);
            Debug.Log(report);
        }

        public static bool TryAuditKnownPrefabs(out string report)
        {
            var lines = new List<string> { "[EntityTransformMapping] 旧挂点审计：" };
            bool valid = true;
            for (int i = 0; i < KnownPrefabPaths.Length; i++)
            {
                if (!TryCreatePlan(KnownPrefabPaths[i], out MigrationPlan plan, out string error))
                {
                    valid = false;
                    lines.Add("- 阻断 " + KnownPrefabPaths[i] + "：" + error);
                    continue;
                }

                lines.Add("- " + plan.Describe());
            }

            report = string.Join("\n", lines);
            return valid;
        }

        public static bool TryMigrateKnownPrefabs(out string report)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report = "PlayMode 或模式切换期间禁止迁移角色 Prefab。";
                return false;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                report = "Unity 正在编译、域重载或导入资产，禁止迁移角色 Prefab。";
                return false;
            }

            var plans = new List<MigrationPlan>(KnownPrefabPaths.Length);
            for (int i = 0; i < KnownPrefabPaths.Length; i++)
            {
                if (!TryCreatePlan(KnownPrefabPaths[i], out MigrationPlan plan, out string error))
                {
                    report = "迁移预检失败，尚未保存任何资产：" + KnownPrefabPaths[i] + "：" + error;
                    return false;
                }
                plans.Add(plan);
            }

            bool requiresMigration = false;
            for (int i = 0; i < plans.Count; i++)
                requiresMigration |= plans[i].RequiresMigration;

            if (!requiresMigration)
            {
                for (int i = 0; i < plans.Count; i++)
                {
                    if (!TryVerifyReloadedPlan(plans[i], out string error))
                    {
                        report = "三份已知角色 Prefab 无待迁移数据，但重载验证失败："
                                 + plans[i].PrefabPath + "：" + error;
                        return false;
                    }
                }

                report = "三份已知角色 Prefab 已是 Unity 原生 EntityTransformMap，重载验证通过；没有创建备份，也没有改写资产。";
                return true;
            }

            if (!TryCreateBackup(out BackupSet backup, out string backupError))
            {
                report = "迁移前备份失败，尚未保存任何资产：" + backupError
                         + "。请审查可能残留的本地备份目录：" + backup.RootPath;
                return false;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("迁移 EntityTransformMapping Odin 挂点数据");
            var migratedPaths = new List<string>();
            try
            {
                for (int i = 0; i < plans.Count; i++)
                {
                    MigrationPlan plan = plans[i];
                    if (!plan.RequiresMigration)
                        continue;

                    if (!TryApplyPlan(plan, out string error))
                    {
                        bool restored = TryRestoreBackup(backup, out string restoreError);
                        report = "迁移失败：" + plan.PrefabPath + "：" + error
                                 + "。自动恢复=" + restored
                                 + (restored ? string.Empty : "，恢复错误=" + restoreError)
                                 + "。本地备份：" + backup.RootPath;
                        return false;
                    }
                    migratedPaths.Add(plan.PrefabPath);
                }

                AssetDatabase.SaveAssets();
                for (int i = 0; i < plans.Count; i++)
                    AssetDatabase.ImportAsset(plans[i].PrefabPath, ImportAssetOptions.ForceUpdate);

                for (int i = 0; i < plans.Count; i++)
                {
                    if (!TryVerifyReloadedPlan(plans[i], out string error))
                    {
                        bool restored = TryRestoreBackup(backup, out string restoreError);
                        report = "迁移后的 Prefab 重载验证失败：" + plans[i].PrefabPath + "：" + error
                                 + "。自动恢复=" + restored
                                 + (restored ? string.Empty : "，恢复错误=" + restoreError)
                                 + "。本地备份：" + backup.RootPath;
                        return false;
                    }
                }
            }
            catch (Exception exception)
            {
                bool restored = TryRestoreBackup(backup, out string restoreError);
                report = "迁移期间发生未处理异常：" + exception.Message
                         + "。自动恢复=" + restored
                         + (restored ? string.Empty : "，恢复错误=" + restoreError)
                         + "。本地备份：" + backup.RootPath;
                return false;
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            report = "已迁移并重载验证 " + migratedPaths.Count + " 份角色 Prefab：\n- "
                     + string.Join("\n- ", migratedPaths)
                     + "\n本地迁移前备份：" + backup.RootPath;
            return true;
        }

        private static bool TryCreateBackup(out BackupSet backup, out string error)
        {
            string taskKey = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")
                             + "_EntityTransformMappingMigration";
            string rootPath = Path.Combine("ES", "Bak", "Local", taskKey).Replace('\\', '/');
            backup = new BackupSet(rootPath);
            error = null;
            try
            {
                for (int i = 0; i < KnownPrefabPaths.Length; i++)
                {
                    string sourcePath = KnownPrefabPaths[i];
                    string absoluteSource = ToProjectAbsolutePath(sourcePath);
                    if (!File.Exists(absoluteSource))
                        throw new FileNotFoundException("迁移源 Prefab 不存在。", sourcePath);

                    string backupPath = Path.Combine(rootPath, sourcePath).Replace('\\', '/');
                    string absoluteBackup = ToProjectAbsolutePath(backupPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(absoluteBackup));
                    File.Copy(absoluteSource, absoluteBackup, false);
                    backup.Files.Add(new BackupFile(sourcePath, backupPath, new FileInfo(absoluteSource).Length,
                        ComputeSha256(absoluteSource)));
                }

                string manifestPath = Path.Combine(rootPath, "BACKUP_MANIFEST.md").Replace('\\', '/');
                var manifest = new StringBuilder();
                manifest.AppendLine("# EntityTransformMapping Migration Backup");
                manifest.AppendLine();
                manifest.AppendLine("- Created UTC: " + DateTime.UtcNow.ToString("O"));
                manifest.AppendLine("- Purpose: before snapshot for explicit Odin dictionary migration");
                manifest.AppendLine();
                manifest.AppendLine("| Source | Backup | Bytes | SHA-256 |");
                manifest.AppendLine("|---|---|---:|---|");
                for (int i = 0; i < backup.Files.Count; i++)
                {
                    BackupFile file = backup.Files[i];
                    manifest.AppendLine("| `" + file.SourcePath + "` | `" + file.BackupPath + "` | "
                                        + file.Length + " | `" + file.Sha256 + "` |");
                }
                File.WriteAllText(ToProjectAbsolutePath(manifestPath), manifest.ToString(), new UTF8Encoding(false));
                backup.ManifestPath = manifestPath;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryRestoreBackup(BackupSet backup, out string error)
        {
            error = null;
            try
            {
                for (int i = 0; i < backup.Files.Count; i++)
                {
                    BackupFile file = backup.Files[i];
                    string absoluteBackup = ToProjectAbsolutePath(file.BackupPath);
                    if (!File.Exists(absoluteBackup)
                        || !string.Equals(ComputeSha256(absoluteBackup), file.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("备份缺失或哈希不匹配：" + file.BackupPath);
                    }
                }

                for (int i = 0; i < backup.Files.Count; i++)
                {
                    BackupFile file = backup.Files[i];
                    File.Copy(ToProjectAbsolutePath(file.BackupPath), ToProjectAbsolutePath(file.SourcePath), true);
                    AssetDatabase.ImportAsset(file.SourcePath, ImportAssetOptions.ForceUpdate);
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string ComputeSha256(string path)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            byte[] hash = sha256.ComputeHash(stream);
            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }

        private static string ToProjectAbsolutePath(string projectRelativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath) || Path.IsPathRooted(projectRelativePath))
                throw new ArgumentException("必须提供项目内相对路径。", nameof(projectRelativePath));

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath));
            string rootPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
            if (!absolutePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("路径超出 Unity 项目根：" + projectRelativePath);

            return absolutePath;
        }

        private static bool TryCreatePlan(string prefabPath, out MigrationPlan plan, out string error)
        {
            plan = null;
            error = null;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                error = "无法加载 Prefab 内容。";
                return false;
            }

            try
            {
                EntityTransformMapping mapping = root.GetComponent<EntityTransformMapping>();
                if (mapping == null)
                {
                    error = "根对象缺少 EntityTransformMapping。";
                    return false;
                }

                if (mapping.HasLegacyOdinMappings)
                {
                    if (!TryDecodeLegacy(mapping, out Dictionary<DefaultTransformKey, Transform> defaults,
                            out Dictionary<string, Transform> dynamics, out error))
                    {
                        return false;
                    }
                    if (!TryBuildMigratedEntries(defaults, dynamics, out List<EntityTransformMap.Entry> entries, out error))
                        return false;
                    if (!TryCreateSnapshot(root.transform, entries, out List<SnapshotEntry> snapshot, out error))
                        return false;

                    var probe = new EntityTransformMap();
                    if (!probe.TryReplaceEntries(entries, out EntityTransformMap.Conflict conflict))
                    {
                        error = "迁移结果冲突：" + conflict.Message;
                        return false;
                    }
                    if (!TryValidateKnownMap(probe, out error))
                        return false;

                    List<EntityTransformMap.Entry> currentEntries = new List<EntityTransformMap.Entry>();
                    mapping.TransformMappings.CopyEntries(currentEntries);
                    if (currentEntries.Count > 0
                        && (!TryCreateSnapshot(root.transform, currentEntries, out List<SnapshotEntry> currentSnapshot, out error)
                            || !SnapshotsEqual(snapshot, currentSnapshot)))
                    {
                        error = "旧 Odin 数据与已存在的新 EntityTransformMap 不一致，拒绝覆盖。";
                        return false;
                    }

                    plan = new MigrationPlan(prefabPath, snapshot, true, defaults.Count, dynamics.Count);
                    return true;
                }

                if (!mapping.TransformMappings.IsValid)
                {
                    error = "EntityTransformMap 无效：" + mapping.TransformMappings.LastConflict.Message;
                    return false;
                }
                if (!TryValidateKnownMap(mapping.TransformMappings, out error))
                    return false;

                var nativeEntries = new List<EntityTransformMap.Entry>();
                mapping.TransformMappings.CopyEntries(nativeEntries);
                if (!TryCreateSnapshot(root.transform, nativeEntries, out List<SnapshotEntry> nativeSnapshot, out error))
                    return false;
                plan = new MigrationPlan(prefabPath, nativeSnapshot, false, 0, 0);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool TryApplyPlan(MigrationPlan plan, out string error)
        {
            error = null;
            GameObject root = PrefabUtility.LoadPrefabContents(plan.PrefabPath);
            if (root == null)
            {
                error = "无法重新加载 Prefab 内容。";
                return false;
            }

            try
            {
                EntityTransformMapping mapping = root.GetComponent<EntityTransformMapping>();
                if (mapping == null || !mapping.HasLegacyOdinMappings)
                {
                    error = "预检后旧载荷状态发生变化，请重新审计。";
                    return false;
                }
                if (!TryDecodeLegacy(mapping, out Dictionary<DefaultTransformKey, Transform> defaults,
                        out Dictionary<string, Transform> dynamics, out error)
                    || !TryBuildMigratedEntries(defaults, dynamics, out List<EntityTransformMap.Entry> entries, out error)
                    || !TryCreateSnapshot(root.transform, entries, out List<SnapshotEntry> currentSnapshot, out error))
                {
                    return false;
                }
                if (!SnapshotsEqual(plan.ExpectedEntries, currentSnapshot))
                {
                    error = "预检后旧挂点内容发生变化，请重新审计。";
                    return false;
                }

                Undo.RegisterCompleteObjectUndo(mapping, "迁移 EntityTransformMapping Odin 挂点数据");
                if (!mapping.TransformMappings.TryReplaceEntries(entries, out EntityTransformMap.Conflict conflict))
                {
                    error = conflict.Message;
                    return false;
                }
                mapping.ClearLegacyOdinSerializationData();
                mapping.RebuildRuntimeCache();
                EditorUtility.SetDirty(mapping);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, plan.PrefabPath);
                if (saved == null)
                {
                    error = "PrefabUtility.SaveAsPrefabAsset 返回空。";
                    return false;
                }
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool TryVerifyReloadedPlan(MigrationPlan plan, out string error)
        {
            error = null;
            GameObject root = PrefabUtility.LoadPrefabContents(plan.PrefabPath);
            if (root == null)
            {
                error = "无法加载保存后的 Prefab。";
                return false;
            }

            try
            {
                EntityTransformMapping mapping = root.GetComponent<EntityTransformMapping>();
                if (mapping == null)
                {
                    error = "保存后缺少 EntityTransformMapping。";
                    return false;
                }
                if (mapping.HasLegacyOdinMappings)
                {
                    error = "保存后仍包含旧 Odin 载荷。";
                    return false;
                }
                if (!mapping.TransformMappings.IsValid)
                {
                    error = "保存后的 EntityTransformMap 无效：" + mapping.TransformMappings.LastConflict.Message;
                    return false;
                }
                if (!TryValidateKnownMap(mapping.TransformMappings, out error))
                    return false;

                var entries = new List<EntityTransformMap.Entry>();
                mapping.TransformMappings.CopyEntries(entries);
                if (!TryCreateSnapshot(root.transform, entries, out List<SnapshotEntry> actual, out error))
                    return false;
                if (!SnapshotsEqual(plan.ExpectedEntries, actual))
                {
                    error = "保存/重载后的键或 Transform 路径与迁移前快照不一致。";
                    return false;
                }
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool TryDecodeLegacy(
            EntityTransformMapping mapping,
            out Dictionary<DefaultTransformKey, Transform> defaults,
            out Dictionary<string, Transform> dynamics,
            out string error)
        {
            defaults = null;
            dynamics = null;
            error = null;
            EntityTransformMapping.LegacyOdinSerializationData snapshot = mapping.CopyLegacyOdinSerializationData();
            if (!snapshot.ContainsData)
            {
                error = "没有可解码的旧 Odin 载荷。";
                return false;
            }

            SerializationData data = ConvertLegacySerializationData(snapshot);

            LegacyMappingHost host = ScriptableObject.CreateInstance<LegacyMappingHost>();
            try
            {
                UnitySerializationUtility.DeserializeUnityObject(host, ref data);
                defaults = host.defaultMap != null
                    ? new Dictionary<DefaultTransformKey, Transform>(host.defaultMap)
                    : new Dictionary<DefaultTransformKey, Transform>();
                dynamics = host.dynamicMap != null
                    ? new Dictionary<string, Transform>(host.dynamicMap, StringComparer.Ordinal)
                    : new Dictionary<string, Transform>(StringComparer.Ordinal);
                return true;
            }
            catch (Exception exception)
            {
                error = "Odin 旧载荷解码失败：" + exception.Message;
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static SerializationData ConvertLegacySerializationData(
            EntityTransformMapping.LegacyOdinSerializationData snapshot)
        {
            var nodes = new List<SerializationNode>(snapshot.SerializationNodes?.Count ?? 0);
            if (snapshot.SerializationNodes != null)
            {
                for (int i = 0; i < snapshot.SerializationNodes.Count; i++)
                {
                    EntityTransformMapping.LegacyOdinSerializationNode source = snapshot.SerializationNodes[i];
                    nodes.Add(new SerializationNode
                    {
                        Name = source.Name,
                        Entry = (EntryType)source.Entry,
                        Data = source.Data,
                    });
                }
            }

            return new SerializationData
            {
                SerializedFormat = (DataFormat)snapshot.SerializedFormat,
                SerializedBytes = snapshot.SerializedBytes,
                ReferencedUnityObjects = snapshot.ReferencedUnityObjects,
                SerializedBytesString = snapshot.SerializedBytesString,
                Prefab = snapshot.Prefab,
                PrefabModificationsReferencedUnityObjects = snapshot.PrefabModificationsReferencedUnityObjects,
                PrefabModifications = snapshot.PrefabModifications,
                SerializationNodes = nodes,
            };
        }

        internal static bool TryBuildMigratedEntries(
            IReadOnlyDictionary<DefaultTransformKey, Transform> defaults,
            IReadOnlyDictionary<string, Transform> dynamics,
            out List<EntityTransformMap.Entry> entries,
            out string error)
        {
            entries = new List<EntityTransformMap.Entry>((defaults?.Count ?? 0) + (dynamics?.Count ?? 0));
            error = null;
            var consumedStrings = new HashSet<string>(StringComparer.Ordinal);

            if (!TryAddKnownPair(DefaultTransformKey.Weapon, EntityEquipmentSocketKeys.WeaponSocket,
                    defaults, dynamics, entries, consumedStrings, out error)
                || !TryAddKnownPair(DefaultTransformKey.Camera, "CameraTarget",
                    defaults, dynamics, entries, consumedStrings, out error))
            {
                return false;
            }

            if (defaults != null)
            {
                var keys = new List<DefaultTransformKey>(defaults.Keys);
                keys.Sort((left, right) => Convert.ToInt64(left).CompareTo(Convert.ToInt64(right)));
                for (int i = 0; i < keys.Count; i++)
                {
                    DefaultTransformKey key = keys[i];
                    if (key == DefaultTransformKey.Weapon || key == DefaultTransformKey.Camera)
                        continue;
                    Transform value = defaults[key];
                    if (value == null)
                    {
                        error = "Enum 挂点为空：" + key;
                        return false;
                    }
                    entries.Add(new EntityTransformMap.Entry(key, value));
                }
            }

            if (dynamics != null)
            {
                var keys = new List<string>(dynamics.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    string key = keys[i];
                    if (consumedStrings.Contains(key))
                        continue;
                    if (string.IsNullOrWhiteSpace(key) || !string.Equals(key, key.Trim(), StringComparison.Ordinal))
                    {
                        error = "String 挂点 Key 非法：'" + key + "'。";
                        return false;
                    }
                    Transform value = dynamics[key];
                    if (value == null)
                    {
                        error = "String 挂点为空：" + key;
                        return false;
                    }
                    entries.Add(new EntityTransformMap.Entry(key, value));
                }
            }

            return true;
        }

        private static bool TryValidateKnownMap(EntityTransformMap map, out string error)
        {
            DefaultTransformKey[] requiredEnums =
            {
                DefaultTransformKey.Root,
                DefaultTransformKey.Head,
                DefaultTransformKey.Chest,
                DefaultTransformKey.Hip,
                DefaultTransformKey.LeftHand,
                DefaultTransformKey.RightHand,
                DefaultTransformKey.LeftFoot,
                DefaultTransformKey.RightFoot,
                DefaultTransformKey.Weapon,
                DefaultTransformKey.Camera,
            };
            for (int i = 0; i < requiredEnums.Length; i++)
            {
                if (!map.ContainsKey(requiredEnums[i]))
                {
                    error = "已知角色 Prefab 缺少固定 Enum 挂点：" + requiredEnums[i];
                    return false;
                }
            }

            string[] requiredStrings = { EntityEquipmentSocketKeys.WeaponSocket, "CameraTarget" };
            for (int i = 0; i < requiredStrings.Length; i++)
            {
                if (!map.ContainsKey(requiredStrings[i]))
                {
                    error = "已知角色 Prefab 缺少稳定 String 挂点：" + requiredStrings[i];
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryAddKnownPair(
            DefaultTransformKey enumKey,
            string stringKey,
            IReadOnlyDictionary<DefaultTransformKey, Transform> defaults,
            IReadOnlyDictionary<string, Transform> dynamics,
            List<EntityTransformMap.Entry> entries,
            HashSet<string> consumedStrings,
            out string error)
        {
            error = null;
            Transform enumValue = null;
            Transform stringValue = null;
            bool hasEnum = defaults != null && defaults.TryGetValue(enumKey, out enumValue);
            bool hasString = dynamics != null && dynamics.TryGetValue(stringKey, out stringValue);
            if (!hasEnum && !hasString)
                return true;
            if (!hasEnum || !hasString)
            {
                error = "已知双别名必须同时存在：" + enumKey + " / " + stringKey;
                return false;
            }
            if (enumValue == null || stringValue == null)
            {
                error = "已知双别名包含空 Transform：" + enumKey + " / " + stringKey;
                return false;
            }
            if (enumValue != stringValue)
            {
                error = "已知双别名指向不同 Transform：" + enumKey + " / " + stringKey;
                return false;
            }

            entries.Add(new EntityTransformMap.Entry(enumKey, stringKey, enumValue));
            consumedStrings.Add(stringKey);
            return true;
        }

        private static bool TryCreateSnapshot(
            Transform root,
            IReadOnlyList<EntityTransformMap.Entry> entries,
            out List<SnapshotEntry> snapshot,
            out string error)
        {
            snapshot = new List<SnapshotEntry>(entries.Count);
            error = null;
            for (int i = 0; i < entries.Count; i++)
            {
                EntityTransformMap.Entry entry = entries[i];
                if (entry.value == null)
                {
                    error = "条目 " + i + " 的 Transform 为空。";
                    return false;
                }
                string path = entry.value == root ? string.Empty : AnimationUtility.CalculateTransformPath(entry.value, root);
                if (entry.value != root && string.IsNullOrEmpty(path))
                {
                    error = "条目 " + i + " 指向 Prefab 根之外的 Transform：" + entry.value.name;
                    return false;
                }
                snapshot.Add(new SnapshotEntry(entry.hasEnumKey, entry.enumKey, entry.stringKey, path));
            }
            snapshot.Sort(SnapshotEntry.Compare);
            return true;
        }

        private static bool SnapshotsEqual(IReadOnlyList<SnapshotEntry> left, IReadOnlyList<SnapshotEntry> right)
        {
            if (left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (!left[i].Equals(right[i]))
                    return false;
            }
            return true;
        }

        private sealed class LegacyMappingHost : ScriptableObject
        {
            [OdinSerialize] public Dictionary<DefaultTransformKey, Transform> defaultMap;
            [OdinSerialize] public Dictionary<string, Transform> dynamicMap;
        }

        private sealed class MigrationPlan
        {
            public readonly string PrefabPath;
            public readonly List<SnapshotEntry> ExpectedEntries;
            public readonly bool RequiresMigration;
            private readonly int legacyEnumCount;
            private readonly int legacyStringCount;

            public MigrationPlan(
                string prefabPath,
                List<SnapshotEntry> expectedEntries,
                bool requiresMigration,
                int legacyEnumCount,
                int legacyStringCount)
            {
                PrefabPath = prefabPath;
                ExpectedEntries = expectedEntries;
                RequiresMigration = requiresMigration;
                this.legacyEnumCount = legacyEnumCount;
                this.legacyStringCount = legacyStringCount;
            }

            public string Describe()
            {
                return RequiresMigration
                    ? PrefabPath + "：待迁移 Enum=" + legacyEnumCount + "，String=" + legacyStringCount
                      + "，目标条目=" + ExpectedEntries.Count
                    : PrefabPath + "：已是 Unity 原生条目，条目=" + ExpectedEntries.Count;
            }
        }

        private sealed class BackupSet
        {
            public readonly string RootPath;
            public readonly List<BackupFile> Files = new List<BackupFile>();
            public string ManifestPath;

            public BackupSet(string rootPath)
            {
                RootPath = rootPath;
            }
        }

        private readonly struct BackupFile
        {
            public readonly string SourcePath;
            public readonly string BackupPath;
            public readonly long Length;
            public readonly string Sha256;

            public BackupFile(string sourcePath, string backupPath, long length, string sha256)
            {
                SourcePath = sourcePath;
                BackupPath = backupPath;
                Length = length;
                Sha256 = sha256;
            }
        }

        private readonly struct SnapshotEntry : IEquatable<SnapshotEntry>
        {
            private readonly bool hasEnum;
            private readonly DefaultTransformKey enumKey;
            private readonly string stringKey;
            private readonly string transformPath;

            public SnapshotEntry(bool hasEnum, DefaultTransformKey enumKey, string stringKey, string transformPath)
            {
                this.hasEnum = hasEnum;
                this.enumKey = enumKey;
                this.stringKey = stringKey ?? string.Empty;
                this.transformPath = transformPath ?? string.Empty;
            }

            public bool Equals(SnapshotEntry other)
            {
                return hasEnum == other.hasEnum
                       && enumKey.Equals(other.enumKey)
                       && string.Equals(stringKey, other.stringKey, StringComparison.Ordinal)
                       && string.Equals(transformPath, other.transformPath, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is SnapshotEntry other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(hasEnum, enumKey, stringKey, transformPath);

            public static int Compare(SnapshotEntry left, SnapshotEntry right)
            {
                int result = left.hasEnum.CompareTo(right.hasEnum);
                if (result != 0) return result;
                result = Convert.ToInt64(left.enumKey).CompareTo(Convert.ToInt64(right.enumKey));
                if (result != 0) return result;
                result = string.CompareOrdinal(left.stringKey, right.stringKey);
                return result != 0 ? result : string.CompareOrdinal(left.transformPath, right.transformPath);
            }
        }
    }
}
