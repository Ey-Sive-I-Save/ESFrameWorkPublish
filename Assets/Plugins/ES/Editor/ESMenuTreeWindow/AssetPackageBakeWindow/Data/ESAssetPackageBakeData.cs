using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    public enum ESAssetPackageCategory
    {
        [InspectorName("预制体")] Prefab,
        [InspectorName("场景")] Scene,
        [InspectorName("材质")] Material,
        [InspectorName("贴图")] Texture,
        [InspectorName("模型")] Model,
        [InspectorName("音频")] Audio,
        [InspectorName("动画")] Animation,
        [InspectorName("SO资产")] ScriptableObject,
        [InspectorName("Shader")] Shader,
        [InspectorName("字体")] Font,
        [InspectorName("视频")] Video,
        [InspectorName("其他")] Other
    }

    [Serializable]
    public sealed class ESAssetPackageBakeRecord
    {
        [LabelText("使用"), HorizontalGroup("Top", Width = 48)]
        public bool selectedForUse;

        [LabelText("分类"), HorizontalGroup("Top", Width = 88), ReadOnly]
        public ESAssetPackageCategory category;

        [LabelText("名称"), HorizontalGroup("Top"), ReadOnly]
        public string assetName;

        [LabelText("路径"), ReadOnly]
        public string assetPath;

        [LabelText("GUID"), ReadOnly]
        public string guid;

        [LabelText("类型"), ReadOnly]
        public string typeName;

        [LabelText("大小"), ReadOnly]
        public string fileSize;

        [LabelText("导出子目录"), ReadOnly]
        public string exportSubFolder;

#if UNITY_EDITOR
        [HorizontalGroup("Ops"), Button("Ping", ButtonSizes.Small)]
        public void Ping()
        {
            UnityEngine.Object asset = LoadAsset();
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        [HorizontalGroup("Ops"), Button("选中", ButtonSizes.Small)]
        public void SelectAsset()
        {
            UnityEngine.Object asset = LoadAsset();
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        [HorizontalGroup("Ops"), Button("设为使用", ButtonSizes.Small)]
        public void MarkUsed()
        {
            selectedForUse = true;
        }

        [HorizontalGroup("Ops"), Button("取消使用", ButtonSizes.Small)]
        public void UnmarkUsed()
        {
            selectedForUse = false;
        }

        public UnityEngine.Object LoadAsset()
        {
            if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(guid))
                assetPath = AssetDatabase.GUIDToAssetPath(guid);

            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadMainAssetAtPath(assetPath);
        }
#endif
    }

    [Serializable]
    public sealed class ESAssetPackageExportLink
    {
        [LabelText("源GUID"), ReadOnly]
        public string sourceGuid;

        [LabelText("源路径"), ReadOnly]
        public string sourceAssetPath;

        [LabelText("导出GUID"), ReadOnly]
        public string targetGuid;

        [LabelText("导出路径"), ReadOnly]
        public string targetAssetPath;

        [LabelText("分类"), ReadOnly]
        public ESAssetPackageCategory category;

        [LabelText("用户直接选择"), ReadOnly]
        public bool rootSelected;

        [LabelText("最后导出会话"), ReadOnly]
        public string lastExportSessionId;

        [LabelText("最后导出时间"), ReadOnly]
        public string lastExportTime;

        [LabelText("导出次数"), ReadOnly]
        public int exportCount;
    }

    [Serializable]
    public sealed class ESAssetPackageExportChain
    {
        [LabelText("源GUID"), ReadOnly]
        public string sourceGuid;

        [LabelText("源路径"), ReadOnly]
        public string sourceAssetPath;

        [LabelText("目标GUID"), ReadOnly]
        public string targetGuid;

        [LabelText("目标路径"), ReadOnly]
        public string targetAssetPath;

        [LabelText("分类"), ReadOnly]
        public ESAssetPackageCategory category;

        [LabelText("用户直接选择"), ReadOnly]
        public bool rootSelected;

        [LabelText("有效"), ReadOnly]
        public bool targetExists;

        [LabelText("最后导出会话"), ReadOnly]
        public string lastExportSessionId;

        [LabelText("最后导出时间"), ReadOnly]
        public string lastExportTime;

        [LabelText("导出次数"), ReadOnly]
        public int exportCount;

        public ESAssetPackageExportLink ToLink()
        {
            return new ESAssetPackageExportLink
            {
                sourceGuid = sourceGuid,
                sourceAssetPath = sourceAssetPath,
                targetGuid = targetGuid,
                targetAssetPath = targetAssetPath,
                category = category,
                rootSelected = rootSelected,
                lastExportSessionId = lastExportSessionId,
                lastExportTime = lastExportTime,
                exportCount = exportCount
            };
        }

        public void FromLink(ESAssetPackageExportLink link)
        {
            if (link == null)
                return;

            sourceGuid = link.sourceGuid;
            sourceAssetPath = link.sourceAssetPath;
            targetGuid = link.targetGuid;
            targetAssetPath = link.targetAssetPath;
            category = link.category;
            rootSelected = link.rootSelected;
#if UNITY_EDITOR
            targetExists = !string.IsNullOrEmpty(targetAssetPath) && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetAssetPath) != null;
#else
            targetExists = false;
#endif
            lastExportSessionId = link.lastExportSessionId;
            lastExportTime = link.lastExportTime;
            exportCount = link.exportCount;
        }
    }

    [Serializable]
    public sealed class ESAssetPackageExportSession
    {
        [LabelText("会话ID"), ReadOnly]
        public string sessionId;

        [LabelText("配置名称"), ReadOnly]
        public string configName;

        [LabelText("导出时间"), ReadOnly]
        public string exportTime;

        [LabelText("导出根目录"), ReadOnly]
        public string exportRootPath;

        [LabelText("直接选择数"), ReadOnly]
        public int selectedRootCount;

        [LabelText("导出总数"), ReadOnly]
        public int totalAssetCount;

        [LabelText("依赖数"), ReadOnly]
        public int dependencyAssetCount;

        [LabelText("新增数"), ReadOnly]
        public int createdCount;

        [LabelText("更新数"), ReadOnly]
        public int updatedCount;

        [LabelText("重映射文件"), ReadOnly]
        public int remappedFileCount;

        [LabelText("失败数"), ReadOnly]
        public int errorCount;

        [LabelText("事务状态"), ReadOnly]
        public string transactionState;

        [LabelText("事务警告"), ReadOnly]
        public string transactionWarning;

        [LabelText("重复跳过数"), ReadOnly]
        public int duplicateSkippedCount;

        [LabelText("导出目标路径"), ReadOnly]
        public List<string> targetAssetPaths = new List<string>();

        [LabelText("导出目标GUID"), ReadOnly]
        public List<string> targetAssetGuids = new List<string>();

        [LabelText("依赖源路径"), ReadOnly]
        public List<string> dependencyAssetPaths = new List<string>();

        [LabelText("重复跳过源路径"), ReadOnly]
        public List<string> duplicateSkippedSourcePaths = new List<string>();

        [LabelText("失败源路径"), ReadOnly]
        public List<string> errorAssetPaths = new List<string>();
    }

    [Serializable]
    public sealed class ESAssetPackageCategoryFolderSetting
    {
        [LabelText("分类"), ReadOnly]
        public ESAssetPackageCategory category;

        [LabelText("文件夹名")]
        public string folderName;
    }

    [ESOnlyEditorSO("资产包烘焙数据只保存编辑器复制/收集状态，不应进入运行时构建或AB资源包。")]
    [CreateAssetMenu(fileName = "资产包烘焙数据", menuName = MenuItemPathDefine.ASSET_DEV_MANAGEMENT_PATH + "资产包烘焙数据")]
    public class ESAssetPackageBakeData : ESSO
    {
        [DisplayAsString(fontSize: 24, Alignment = TextAlignment.Center), HideLabel]
        [GUIColor(0.45f, 0.82f, 1f)]
        public string title = "资产包烘焙数据";

        [LabelText("显示名称")]
        public string displayName = "新资产包";

        [LabelText("目标文件夹路径"), FolderPath(AbsolutePath = false)]
        public string targetFolderPath = "Assets";

        [LabelText("默认导出根目录"), FolderPath(AbsolutePath = false)]
        public string exportRootPath = "Assets/_ESAssetPackageExport";

        [FoldoutGroup("导出配置"), LabelText("配置名称")]
        public string exportConfigName = "默认导出配置";

        [FoldoutGroup("导出配置"), LabelText("预览兜底材质")]
        public Material previewFallbackMaterial;

        [FoldoutGroup("导出配置"), LabelText("动作预览默认模型")]
        public GameObject animationPreviewModel;

        [FoldoutGroup("导出配置"), LabelText("动作预览Avatar")]
        public Avatar animationPreviewAvatar;

        [LabelText("包含子文件夹")]
        public bool includeSubFolders = true;

        [FoldoutGroup("导出配置"), LabelText("导出依赖资源")]
        public bool exportDependencies = true;

        [FoldoutGroup("导出配置"), LabelText("重映射导出内部GUID")]
        public bool remapExportedGuids = true;

        [FoldoutGroup("导出配置"), LabelText("重复导出时覆盖旧目标")]
        public bool overwriteExistingExport = false;

        [FoldoutGroup("导出配置"), LabelText("导出文件名前缀")]
        public string exportFileNamePrefix = "ES选用_";

        [LabelText("最后烘焙时间"), ReadOnly]
        public string lastBakeTime;

        [LabelText("总资产数"), ReadOnly]
        public int totalAssetCount;

        [LabelText("已选使用数"), ReadOnly]
        public int selectedUseCount;

        [LabelText("分类统计"), DictionaryDrawerSettings(KeyLabel = "分类", ValueLabel = "数量")]
        public Dictionary<ESAssetPackageCategory, int> categoryCounts = new Dictionary<ESAssetPackageCategory, int>();

        [LabelText("资产记录"), Searchable]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "assetName", NumberOfItemsPerPage = 20)]
        public List<ESAssetPackageBakeRecord> records = new List<ESAssetPackageBakeRecord>();

        [FoldoutGroup("导出链路"), LabelText("最后导出时间"), ReadOnly]
        public string lastExportTime;

        [FoldoutGroup("导出链路"), LabelText("最后导出根目录"), ReadOnly]
        public string lastExportRootPath;

        [FoldoutGroup("导出链路"), LabelText("最后导出总数"), ReadOnly]
        public int lastExportAssetCount;

        [FoldoutGroup("导出链路"), LabelText("最后导出依赖数"), ReadOnly]
        public int lastExportDependencyCount;

        [LabelText("最近导出尝试状态"), ReadOnly]
        public string lastExportAttemptState = "Idle";

        [LabelText("最近导出尝试会话"), ReadOnly]
        public string lastExportAttemptSessionId;

        [LabelText("最近导出尝试时间"), ReadOnly]
        public string lastExportAttemptTime;

        [LabelText("最近导出尝试说明"), ReadOnly]
        public string lastExportAttemptMessage;

        [FoldoutGroup("导出链路"), LabelText("导出链路")]
        [ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 12)]
        public List<ESAssetPackageExportLink> exportLinks = new List<ESAssetPackageExportLink>();

        [FoldoutGroup("导出链路"), LabelText("导出链路字典(SourceGuid)")]
        [DictionaryDrawerSettings(KeyLabel = "源GUID", ValueLabel = "链路")]
        public Dictionary<string, ESAssetPackageExportChain> exportChainBySourceGuid = new Dictionary<string, ESAssetPackageExportChain>();

        [FoldoutGroup("导出链路"), LabelText("导出会话")]
        [ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 8)]
        public List<ESAssetPackageExportSession> exportSessions = new List<ESAssetPackageExportSession>();

        [FoldoutGroup("导出配置"), LabelText("分类导出文件夹")]
        [ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 12)]
        public List<ESAssetPackageCategoryFolderSetting> categoryFolderSettings = new List<ESAssetPackageCategoryFolderSetting>();

        public void EnsureCategoryFolderSettings()
        {
            if (categoryFolderSettings == null)
                categoryFolderSettings = new List<ESAssetPackageCategoryFolderSetting>();

            foreach (ESAssetPackageCategory category in Enum.GetValues(typeof(ESAssetPackageCategory)))
            {
                ESAssetPackageCategoryFolderSetting setting = categoryFolderSettings.FirstOrDefault(x => x != null && x.category == category);
                if (setting == null)
                {
                    categoryFolderSettings.Add(new ESAssetPackageCategoryFolderSetting
                    {
                        category = category,
                        folderName = GetDefaultExportSubFolder(category)
                    });
                }
                else if (string.IsNullOrWhiteSpace(setting.folderName))
                {
                    setting.folderName = GetDefaultExportSubFolder(category);
                }
            }

            categoryFolderSettings.RemoveAll(x => x == null);
            categoryFolderSettings.Sort((a, b) => a.category.CompareTo(b.category));
        }

        public string GetConfiguredExportSubFolder(ESAssetPackageCategory category)
        {
            EnsureCategoryFolderSettings();
            ESAssetPackageCategoryFolderSetting setting = categoryFolderSettings.FirstOrDefault(x => x != null && x.category == category);
            return SanitizeFolderName(string.IsNullOrWhiteSpace(setting?.folderName) ? GetDefaultExportSubFolder(category) : setting.folderName);
        }

        public static string GetDefaultExportSubFolder(ESAssetPackageCategory category)
        {
            switch (category)
            {
                case ESAssetPackageCategory.Prefab: return "Prefabs";
                case ESAssetPackageCategory.Scene: return "Scenes";
                case ESAssetPackageCategory.Material: return "Materials";
                case ESAssetPackageCategory.Texture: return "Textures";
                case ESAssetPackageCategory.Model: return "Models";
                case ESAssetPackageCategory.Audio: return "Audio";
                case ESAssetPackageCategory.Animation: return "Animations";
                case ESAssetPackageCategory.ScriptableObject: return "ScriptableObjects";
                case ESAssetPackageCategory.Shader: return "Shaders";
                case ESAssetPackageCategory.Font: return "Fonts";
                case ESAssetPackageCategory.Video: return "Videos";
                default: return "Others";
            }
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Others";

            string result = name.Trim().Replace("\\", "_").Replace("/", "_").Replace(":", "_");
            foreach (char c in Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');
            return string.IsNullOrWhiteSpace(result) ? "Others" : result;
        }

        public IEnumerable<ESAssetPackageBakeRecord> GetRecords(ESAssetPackageCategory category)
        {
            if (records == null)
                yield break;

            for (int i = 0; i < records.Count; i++)
            {
                ESAssetPackageBakeRecord record = records[i];
                if (record != null && record.category == category)
                    yield return record;
            }
        }

        public void RebuildStats()
        {
            totalAssetCount = records != null ? records.Count : 0;
            selectedUseCount = 0;
            categoryCounts.Clear();

            if (records == null)
                return;

            for (int i = 0; i < records.Count; i++)
            {
                ESAssetPackageBakeRecord record = records[i];
                if (record == null)
                    continue;

                if (record.selectedForUse)
                    selectedUseCount++;

                if (!categoryCounts.ContainsKey(record.category))
                    categoryCounts[record.category] = 0;
                categoryCounts[record.category]++;
            }
        }

#if UNITY_EDITOR
        [Button("重新烘焙目标文件夹", ButtonHeight = 32), GUIColor(0.32f, 0.58f, 0.9f)]
        public void BakeNow()
        {
            ESAssetPackageBakeUtility.Bake(this);
        }

        [Button("选中所有已使用资产", ButtonHeight = 24)]
        public void SelectUsedAssets()
        {
            if (records == null)
                return;

            var assets = new List<UnityEngine.Object>();
            for (int i = 0; i < records.Count; i++)
            {
                ESAssetPackageBakeRecord record = records[i];
                if (record == null || !record.selectedForUse)
                    continue;

                UnityEngine.Object asset = record.LoadAsset();
                if (asset != null)
                    assets.Add(asset);
            }

            Selection.objects = assets.ToArray();
            if (assets.Count > 0)
                EditorGUIUtility.PingObject(assets[0]);
        }

        [Button("复制勾选资产到分类文件夹", ButtonHeight = 32), GUIColor(0.35f, 0.72f, 0.45f)]
        public void ExportSelectedAssetsByCategory()
        {
            ESAssetPackageBakeUtility.ExportSelectedAssetsByCategory(this);
        }

        [Button("回退最近一次导出", ButtonHeight = 28), GUIColor(0.8f, 0.45f, 0.35f)]
        public void RollbackLastExport()
        {
            ESAssetPackageBakeUtility.RollbackLastExport(this);
        }
#endif
    }

#if UNITY_EDITOR
public static class ESAssetPackageBakeUtility
{
        private sealed class ExportPlanItem
        {
            public string sourcePath;
            public string targetPath;
            public ESAssetPackageCategory category;
            public bool rootSelected;
            public bool dependency;
            public bool overwrite;
        }

        private sealed class ExportTransactionItem
        {
            public ExportPlanItem plan;
            public string stagedPath;
            public string backupPath;
            public bool hadExistingTarget;
            public bool committed;
        }

        public static void Bake(ESAssetPackageBakeData data)
        {
            if (data == null)
                return;

            data.EnsureCategoryFolderSettings();

            string folder = NormalizeAssetPath(data.targetFolderPath);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                EditorUtility.DisplayDialog("资产包烘焙", "目标文件夹无效：" + folder, "确定");
                return;
            }

            var oldUseByGuid = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (data.records != null)
            {
                for (int i = 0; i < data.records.Count; i++)
                {
                    ESAssetPackageBakeRecord record = data.records[i];
                    if (record != null && !string.IsNullOrEmpty(record.guid))
                        oldUseByGuid[record.guid] = record.selectedForUse;
                }
            }

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            data.records.Clear();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                    continue;

                if (!data.includeSubFolders && !string.Equals(Path.GetDirectoryName(path)?.Replace("\\", "/"), folder, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (IsOnlyEditorSOAsset(path))
                    continue;

                Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                ESAssetPackageCategory category = DetermineCategory(path, type);
                string guid = AssetDatabase.AssetPathToGUID(path);

                data.records.Add(new ESAssetPackageBakeRecord
                {
                    selectedForUse = oldUseByGuid.TryGetValue(guid, out bool selected) && selected,
                    category = category,
                    assetName = Path.GetFileNameWithoutExtension(path),
                    assetPath = path,
                    guid = guid,
                    typeName = type != null ? type.Name : "Unknown",
                    fileSize = FormatFileSize(path),
                    exportSubFolder = data.GetConfiguredExportSubFolder(category)
                });
            }

            data.records.Sort((a, b) =>
            {
                int c = a.category.CompareTo(b.category);
                return c != 0 ? c : string.Compare(a.assetPath, b.assetPath, StringComparison.OrdinalIgnoreCase);
            });

            data.lastBakeTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            data.RebuildStats();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        public static void ExportSelectedAssetsByCategory(ESAssetPackageBakeData data)
        {
            if (data == null)
                return;

            data.EnsureCategoryFolderSettings();
            data.exportLinks ??= new List<ESAssetPackageExportLink>();
            data.exportSessions ??= new List<ESAssetPackageExportSession>();
            data.exportChainBySourceGuid ??= new Dictionary<string, ESAssetPackageExportChain>();
            SyncExportChainDictionary(data);

            string exportRoot = NormalizeAssetPath(data.exportRootPath);
            if (string.IsNullOrEmpty(exportRoot) || !exportRoot.StartsWith("Assets/", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("资源包导出", "导出根目录必须位于 Assets 下。", "确定");
                return;
            }

            if (!ValidateExportRootForUse(exportRoot))
            {
                return;
            }

            data.exportRootPath = exportRoot;
            data.exportFileNamePrefix = SanitizeFileNamePrefix(data.exportFileNamePrefix);

            int skipped = 0;
            var errors = new List<string>();
            var exportPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rootPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dependencyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var copiedPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rootSelectedByPath = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string exportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var record in data.records)
            {
                if (record == null || !record.selectedForUse || string.IsNullOrEmpty(record.assetPath))
                {
                    skipped++;
                    continue;
                }

                string rootPath = NormalizeAssetPath(record.assetPath);
                if (AddExportPath(rootPath, exportRoot, exportPaths))
                {
                    rootPaths.Add(rootPath);
                    rootSelectedByPath[rootPath] = true;
                }

                if (!data.exportDependencies)
                    continue;

                string[] dependencies = AssetDatabase.GetDependencies(rootPath, true);
                for (int i = 0; i < dependencies.Length; i++)
                {
                    string dependency = NormalizeAssetPath(dependencies[i]);
                    if (string.Equals(dependency, rootPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (AddExportPath(dependency, exportRoot, exportPaths))
                        dependencyPaths.Add(dependency);
                }
            }

            if (exportPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("资源包导出", "没有可导出的已使用资源。", "确定");
                return;
            }

            var previousLinks = BuildExportLinkLookup(data.exportLinks);
            var usedTargetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var plan = new List<ExportPlanItem>();
            var duplicateSkipped = new List<string>();
            var categoryFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int created = 0;
            int updated = 0;

            foreach (string sourcePath in exportPaths.OrderBy(p => rootPaths.Contains(p) ? 0 : 1).ThenBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                Type type = AssetDatabase.GetMainAssetTypeAtPath(sourcePath);
                ESAssetPackageCategory category = DetermineCategory(sourcePath, type);
                string categoryFolder = $"{exportRoot}/{data.GetConfiguredExportSubFolder(category)}";
                categoryFolders.Add(categoryFolder);

                string targetPath = ResolveExportTargetPath(
                    sourcePath,
                    categoryFolder,
                    data.exportFileNamePrefix,
                    previousLinks,
                    usedTargetPaths,
                    data.overwriteExistingExport,
                    out bool overwrite,
                    out string skipReason);

                if (string.IsNullOrEmpty(targetPath))
                {
                    duplicateSkipped.Add(string.IsNullOrEmpty(skipReason) ? sourcePath : sourcePath + "  ->  " + skipReason);
                    continue;
                }

                plan.Add(new ExportPlanItem
                {
                    sourcePath = sourcePath,
                    targetPath = targetPath,
                    category = category,
                    rootSelected = rootPaths.Contains(sourcePath),
                    dependency = dependencyPaths.Contains(sourcePath),
                    overwrite = overwrite
                });
            }

            if (plan.Count == 0)
            {
                string duplicateText = duplicateSkipped.Count > 0 ? "\n\n重复/冲突项:\n" + string.Join("\n", duplicateSkipped.Take(20)) : string.Empty;
                EditorUtility.DisplayDialog("资源包导出", "没有需要新导出的资源。默认不重复导出已有有效链路。" + duplicateText, "确定");
                return;
            }

            data.lastExportAttemptState = "AwaitingInput";
            data.lastExportAttemptSessionId = sessionId;
            data.lastExportAttemptTime = exportTime;
            data.lastExportAttemptMessage = "导出计划已生成，等待用户确认。";
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            if (!DisplayExportPreflight(data, exportRoot, plan, rootPaths, dependencyPaths, duplicateSkipped))
            {
                data.lastExportAttemptState = "Cancelled";
                data.lastExportAttemptMessage = "用户取消了本次导出，未修改目标资产。";
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                return;
            }



            EnsureAssetFolder(exportRoot);
            foreach (string folder in categoryFolders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                EnsureAssetFolder(folder);

            string transactionRoot = NormalizeAssetPath($"{exportRoot}/.ESBakeTransactions/{sessionId}");
            data.lastExportAttemptState = "Running";
            data.lastExportAttemptSessionId = sessionId;
            data.lastExportAttemptTime = exportTime;
            data.lastExportAttemptMessage = "事务正在准备暂存、备份和提交。若 Unity 中途重载，下次打开窗口时请优先检查事务状态。";
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            if (!TryCommitExportTransaction(
                    transactionRoot,
                    plan,
                    out copiedPathMap,
                    out created,
                    out updated,
                    out string transactionError))
            {
                errors.Add("事务导出失败：" + transactionError);
                data.lastExportAttemptState = "Failed";
                data.lastExportAttemptMessage = transactionError;
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog(
                    "资源包导出失败",
                    "导出事务未提交，已尝试恢复原有目标。\n\n" + transactionError,
                    "确定");
                return;
            }

            int remapped = data.remapExportedGuids ? RemapCopiedAssetGuids(copiedPathMap) : 0;
            AssetDatabase.Refresh();
            UpdateExportLinks(data, copiedPathMap, rootSelectedByPath, sessionId, exportTime);
            SyncExportChainDictionary(data);
            MarkRecordsWithValidExportLinksAsSelected(data);
            int copiedDependencyCount = copiedPathMap.Keys.Count(path => dependencyPaths.Contains(NormalizeAssetPath(path)));

            var session = new ESAssetPackageExportSession
            {
                sessionId = sessionId,
                configName = string.IsNullOrWhiteSpace(data.exportConfigName) ? data.displayName : data.exportConfigName,
                exportTime = exportTime,
                exportRootPath = exportRoot,
                selectedRootCount = rootPaths.Count,
                totalAssetCount = copiedPathMap.Count,
                dependencyAssetCount = copiedDependencyCount,
                createdCount = created,
                updatedCount = updated,
                remappedFileCount = remapped,
                errorCount = errors.Count,
                transactionState = "Committed",
                transactionWarning = transactionError,
                duplicateSkippedCount = duplicateSkipped.Count,
                targetAssetPaths = copiedPathMap.Values.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
                targetAssetGuids = copiedPathMap.Values
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .Select(AssetDatabase.AssetPathToGUID)
                    .ToList(),
                dependencyAssetPaths = dependencyPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
                duplicateSkippedSourcePaths = duplicateSkipped.ToList(),
                errorAssetPaths = errors.ToList()
            };
            data.exportSessions.Add(session);
            TrimExportSessions(data.exportSessions, 30);

            data.lastExportTime = exportTime;
            data.lastExportRootPath = exportRoot;
            data.lastExportAssetCount = copiedPathMap.Count;
            data.lastExportDependencyCount = session.dependencyAssetCount;
            data.lastExportAttemptState = string.IsNullOrEmpty(transactionError) ? "Committed" : "CommittedWithWarning";
            data.lastExportAttemptMessage = string.IsNullOrEmpty(transactionError)
                ? "导出事务已提交，目标和会话记录已刷新。"
                : transactionError;
            data.RebuildStats();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            string message = $"导出完成。\n新增: {created}\n更新: {updated}\n总导出: {copiedPathMap.Count}\n其中依赖: {session.dependencyAssetCount}\n重映射文件: {remapped}\n未选跳过: {skipped}\n重复/冲突跳过: {duplicateSkipped.Count}\n失败: {errors.Count}\n\n导出目录:\n{exportRoot}\n命名前缀:\n{data.exportFileNamePrefix}\n会话:\n{sessionId}";
            if (duplicateSkipped.Count > 0)
                message += "\n\n重复/冲突项:\n" + string.Join("\n", duplicateSkipped.Take(8));
            if (errors.Count > 0)
                message += "\n\n失败项:\n" + string.Join("\n", errors.Take(8));
            if (!string.IsNullOrEmpty(transactionError))
                message += "\n\n事务警告:\n" + transactionError;

            EditorUtility.DisplayDialog("资源包导出", message, "确定");
            UnityEngine.Object folderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(exportRoot);
            if (folderAsset != null)
                EditorGUIUtility.PingObject(folderAsset);
        }

        private static bool TryCommitExportTransaction(
            string transactionRoot,
            List<ExportPlanItem> plan,
            out Dictionary<string, string> copiedPathMap,
            out int created,
            out int updated,
            out string transactionError)
        {
            copiedPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            created = 0;
            updated = 0;
            transactionError = string.Empty;
            var items = new List<ExportTransactionItem>(plan?.Count ?? 0);
            bool editing = false;

            try
            {
                if (string.IsNullOrWhiteSpace(transactionRoot) || !transactionRoot.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("事务目录必须位于 Assets 下。");
                if (AssetDatabase.IsValidFolder(transactionRoot))
                    throw new IOException("事务目录已存在，拒绝复用旧事务：" + transactionRoot);

                string stagedRoot = NormalizeAssetPath(transactionRoot + "/Staged");
                string backupRoot = NormalizeAssetPath(transactionRoot + "/Backup");
                EnsureAssetFolder(stagedRoot);
                EnsureAssetFolder(backupRoot);

                AssetDatabase.StartAssetEditing();
                editing = true;
                for (int index = 0; index < plan.Count; index++)
                {
                    ExportPlanItem exportPlan = plan[index];
                    if (exportPlan == null || string.IsNullOrEmpty(exportPlan.sourcePath) || string.IsNullOrEmpty(exportPlan.targetPath))
                        throw new InvalidDataException("导出事务包含空的源或目标路径。");
                    if (AssetDatabase.LoadMainAssetAtPath(exportPlan.sourcePath) == null)
                        throw new FileNotFoundException("导出源资产不存在：" + exportPlan.sourcePath);

                    bool existedBefore = AssetDatabase.LoadMainAssetAtPath(exportPlan.targetPath) != null;
                    if (existedBefore && !exportPlan.overwrite)
                        throw new IOException("目标在事务阶段已存在且不允许覆盖：" + exportPlan.targetPath);

                    var item = new ExportTransactionItem
                    {
                        plan = exportPlan,
                        hadExistingTarget = existedBefore,
                        stagedPath = BuildTransactionAssetPath(stagedRoot, index, exportPlan.targetPath),
                        backupPath = existedBefore ? BuildTransactionAssetPath(backupRoot, index, exportPlan.targetPath) : string.Empty
                    };

                    if (existedBefore && !AssetDatabase.CopyAsset(exportPlan.targetPath, item.backupPath))
                        throw new IOException("无法备份已有目标，事务已中止：" + exportPlan.targetPath);
                    if (!AssetDatabase.CopyAsset(exportPlan.sourcePath, item.stagedPath))
                        throw new IOException("无法生成暂存资产，事务已中止：" + exportPlan.sourcePath);
                    items.Add(item);
                }

                AssetDatabase.StopAssetEditing();
                editing = false;
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                VerifyTransactionAssets(items);

                AssetDatabase.StartAssetEditing();
                editing = true;
                for (int index = 0; index < items.Count; index++)
                {
                    ExportTransactionItem item = items[index];
                    bool targetExistsNow = AssetDatabase.LoadMainAssetAtPath(item.plan.targetPath) != null;
                    if (targetExistsNow && !item.plan.overwrite)
                        throw new IOException("提交阶段发现新的目标冲突：" + item.plan.targetPath);
                    if (targetExistsNow && !AssetDatabase.DeleteAsset(item.plan.targetPath))
                        throw new IOException("无法删除待替换目标，事务已中止：" + item.plan.targetPath);

                    string moveError = AssetDatabase.MoveAsset(item.stagedPath, item.plan.targetPath);
                    if (!string.IsNullOrEmpty(moveError))
                        throw new IOException("暂存资产提交失败：" + item.plan.targetPath + "，" + moveError);

                    item.committed = true;
                    copiedPathMap[item.plan.sourcePath] = item.plan.targetPath;
                    if (item.hadExistingTarget) updated++;
                    else created++;
                }

                AssetDatabase.StopAssetEditing();
                editing = false;
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                if (!AssetDatabase.DeleteAsset(transactionRoot))
                    transactionError = "导出已提交，但事务目录清理失败，已保留供恢复：" + transactionRoot;
                else
                    RemoveEmptyFolders(NormalizeAssetPath(transactionRoot.Substring(0, transactionRoot.LastIndexOf('/'))));
                return true;
            }
            catch (Exception exception)
            {
                if (editing)
                {
                    AssetDatabase.StopAssetEditing();
                    editing = false;
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                string rollbackError = RollbackExportTransaction(items, transactionRoot);
                transactionError = exception.Message + (string.IsNullOrEmpty(rollbackError) ? string.Empty : "\n回滚异常：" + rollbackError);
                return false;
            }
            finally
            {
                if (editing)
                    AssetDatabase.StopAssetEditing();
            }
        }

        private static void VerifyTransactionAssets(List<ExportTransactionItem> items)
        {
            for (int index = 0; index < items.Count; index++)
            {
                ExportTransactionItem item = items[index];
                if (AssetDatabase.LoadMainAssetAtPath(item.stagedPath) == null)
                    throw new IOException("暂存资产刷新后不可见：" + item.stagedPath);
                if (item.hadExistingTarget && AssetDatabase.LoadMainAssetAtPath(item.backupPath) == null)
                    throw new IOException("已有目标备份刷新后不可见：" + item.backupPath);
            }
        }

        private static string RollbackExportTransaction(List<ExportTransactionItem> items, string transactionRoot)
        {
            var rollbackErrors = new List<string>();
            bool editing = false;
            try
            {
                AssetDatabase.StartAssetEditing();
                editing = true;
                for (int index = items.Count - 1; index >= 0; index--)
                {
                    ExportTransactionItem item = items[index];
                    if (item.committed && AssetDatabase.LoadMainAssetAtPath(item.plan.targetPath) != null
                        && !AssetDatabase.DeleteAsset(item.plan.targetPath))
                        rollbackErrors.Add("无法删除已提交目标：" + item.plan.targetPath);

                    if (item.hadExistingTarget && AssetDatabase.LoadMainAssetAtPath(item.backupPath) != null)
                    {
                        string moveError = AssetDatabase.MoveAsset(item.backupPath, item.plan.targetPath);
                        if (!string.IsNullOrEmpty(moveError))
                            rollbackErrors.Add("无法恢复原目标：" + item.plan.targetPath + "，" + moveError);
                    }
                }
            }
            catch (Exception exception)
            {
                rollbackErrors.Add(exception.Message);
            }
            finally
            {
                if (editing)
                    AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            if (rollbackErrors.Count == 0 && AssetDatabase.IsValidFolder(transactionRoot))
                AssetDatabase.DeleteAsset(transactionRoot);
            return string.Join("；", rollbackErrors);
        }

        private static string BuildTransactionAssetPath(string root, int index, string targetPath)
        {
            string name = Path.GetFileNameWithoutExtension(targetPath);
            string extension = Path.GetExtension(targetPath);
            string safeName = SanitizeFileNamePrefix(name).TrimEnd('_');
            if (string.IsNullOrEmpty(safeName)) safeName = "Asset";
            return NormalizeAssetPath($"{root}/item_{index:D5}_{safeName}{extension}");
        }

        private static void ExportSelectedAssetsByCategory_Legacy(ESAssetPackageBakeData data)
        {
            if (data == null)
                return;

            data.EnsureCategoryFolderSettings();

            string exportRoot = NormalizeAssetPath(data.exportRootPath);
            if (string.IsNullOrEmpty(exportRoot) || !exportRoot.StartsWith("Assets/", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("资产包导出", "导出根目录必须位于 Assets 下。", "确定");
                return;
            }

            if (!ValidateExportRootForUse(exportRoot))
            {
                return;
            }

            EnsureAssetFolder(exportRoot);
            data.exportLinks ??= new List<ESAssetPackageExportLink>();
            data.exportSessions ??= new List<ESAssetPackageExportSession>();

            int skipped = 0;
            var errors = new List<string>();
            var exportPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rootPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var copiedPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rootSelectedByPath = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string exportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var record in data.records)
            {
                if (record == null || !record.selectedForUse || string.IsNullOrEmpty(record.assetPath))
                {
                    skipped++;
                    continue;
                }

                string rootPath = NormalizeAssetPath(record.assetPath);
                if (AddExportPath(rootPath, exportRoot, exportPaths))
                {
                    rootPaths.Add(rootPath);
                    rootSelectedByPath[rootPath] = true;
                }

                if (data.exportDependencies)
                {
                    string[] dependencies = AssetDatabase.GetDependencies(rootPath, true);
                    for (int i = 0; i < dependencies.Length; i++)
                        AddExportPath(dependencies[i], exportRoot, exportPaths);
                }
            }

            if (exportPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("资产包导出", "没有可导出的已使用资产。", "确定");
                return;
            }

            var previousLinks = BuildExportLinkLookup(data.exportLinks);
            var usedTargetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int created = 0;
            int updated = 0;

            var plan = new List<ExportPlanItem>();
            foreach (string sourcePath in exportPaths)
            {
                Type type = AssetDatabase.GetMainAssetTypeAtPath(sourcePath);
                ESAssetPackageCategory category = DetermineCategory(sourcePath, type);
                string categoryFolder = $"{exportRoot}/{data.GetConfiguredExportSubFolder(category)}";
                EnsureAssetFolder(categoryFolder);

                string targetPath = ResolveExportTargetPath(sourcePath, categoryFolder, previousLinks, usedTargetPaths);
                bool overwrite = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath) != null;
                if (overwrite && !data.overwriteExistingExport)
                {
                    targetPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);
                    usedTargetPaths.Add(targetPath);
                    overwrite = false;
                }

                plan.Add(new ExportPlanItem
                {
                    sourcePath = sourcePath,
                    targetPath = targetPath,
                    category = category,
                    rootSelected = rootPaths.Contains(sourcePath),
                    dependency = !rootPaths.Contains(sourcePath),
                    overwrite = overwrite
                });
            }

            string transactionRoot = NormalizeAssetPath($"{exportRoot}/.ESBakeTransactions/{sessionId}");
            data.lastExportAttemptState = "Running";
            data.lastExportAttemptSessionId = sessionId;
            data.lastExportAttemptTime = exportTime;
            data.lastExportAttemptMessage = "事务正在准备暂存、备份和提交。";
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            if (!TryCommitExportTransaction(transactionRoot, plan, out copiedPathMap, out created, out updated, out string transactionError))
            {
                errors.Add(transactionError);
                data.lastExportAttemptState = "Failed";
                data.lastExportAttemptMessage = transactionError;
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("资源包导出失败", "导出事务未提交，已尝试恢复原有目标。\n\n" + transactionError, "确定");
                return;
            }

            int remapped = data.remapExportedGuids ? RemapCopiedAssetGuids(copiedPathMap) : 0;
            AssetDatabase.Refresh();
            UpdateExportLinks(data, copiedPathMap, rootSelectedByPath, sessionId, exportTime);

            var session = new ESAssetPackageExportSession
            {
                sessionId = sessionId,
                configName = string.IsNullOrWhiteSpace(data.exportConfigName) ? data.displayName : data.exportConfigName,
                exportTime = exportTime,
                exportRootPath = exportRoot,
                selectedRootCount = rootPaths.Count,
                totalAssetCount = copiedPathMap.Count,
                dependencyAssetCount = Mathf.Max(0, copiedPathMap.Count - rootPaths.Count),
                createdCount = created,
                updatedCount = updated,
                remappedFileCount = remapped,
                errorCount = errors.Count,
                transactionState = "Committed",
                transactionWarning = transactionError,
                targetAssetPaths = copiedPathMap.Values.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
                targetAssetGuids = copiedPathMap.Values
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .Select(AssetDatabase.AssetPathToGUID)
                    .ToList(),
                errorAssetPaths = errors.ToList()
            };
            data.exportSessions.Add(session);
            TrimExportSessions(data.exportSessions, 30);

            data.lastExportTime = exportTime;
            data.lastExportRootPath = exportRoot;
            data.lastExportAssetCount = copiedPathMap.Count;
            data.lastExportDependencyCount = session.dependencyAssetCount;
            data.lastExportAttemptState = string.IsNullOrEmpty(transactionError) ? "Committed" : "CommittedWithWarning";
            data.lastExportAttemptMessage = string.IsNullOrEmpty(transactionError)
                ? "导出事务已提交，目标和会话记录已刷新。"
                : transactionError;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            string message = $"导出完成。\n新增: {created}\n更新: {updated}\n总导出: {copiedPathMap.Count}\n其中依赖: {session.dependencyAssetCount}\n重映射文件: {remapped}\n跳过: {skipped}\n失败: {errors.Count}\n\n导出目录:\n{exportRoot}\n会话:\n{sessionId}";
            if (errors.Count > 0)
                message += "\n\n失败项:\n" + string.Join("\n", errors.Take(8));

            EditorUtility.DisplayDialog("资产包导出", message, "确定");
            UnityEngine.Object folderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(exportRoot);
            if (folderAsset != null)
                EditorGUIUtility.PingObject(folderAsset);
        }

        public static void RollbackLastExport(ESAssetPackageBakeData data)
        {
            if (data == null || data.exportSessions == null || data.exportSessions.Count == 0)
            {
                EditorUtility.DisplayDialog("资产包导出回退", "没有可回退的导出会话。", "确定");
                return;
            }

            ESAssetPackageExportSession session = data.exportSessions[data.exportSessions.Count - 1];
            string exportRoot = NormalizeAssetPath(session.exportRootPath);
            if (string.IsNullOrEmpty(exportRoot) || !exportRoot.StartsWith("Assets/", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("资产包导出回退", "最近导出会话的根目录无效，已拒绝回退。", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "资产包导出回退",
                    $"将删除最近一次导出的 {session.targetAssetPaths.Count} 个目标资源。\n\n导出根目录:\n{exportRoot}\n会话:\n{session.sessionId}\n\n此操作只处理导出链路记录中的目标路径。",
                    "确认回退",
                    "取消"))
                return;

            int deleted = 0;
            int missing = 0;
            int changed = 0;
            List<string> recordedTargetPaths = session.targetAssetPaths != null
                ? session.targetAssetPaths.Select(NormalizeAssetPath).ToList()
                : new List<string>();
            var recordedGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (session.targetAssetGuids != null)
            {
                for (int index = 0; index < recordedTargetPaths.Count && index < session.targetAssetGuids.Count; index++)
                    recordedGuids[recordedTargetPaths[index]] = session.targetAssetGuids[index];
            }
            List<string> targets = recordedTargetPaths.OrderByDescending(p => p.Length).ToList();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string rawPath in targets)
                {
                    string targetPath = NormalizeAssetPath(rawPath);
                    if (!IsPathInsideRoot(targetPath, exportRoot))
                        continue;

                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath) == null)
                    {
                        missing++;
                        continue;
                    }

                    if (recordedGuids.TryGetValue(targetPath, out string expectedGuid)
                        && !string.IsNullOrEmpty(expectedGuid)
                        && !string.Equals(AssetDatabase.AssetPathToGUID(targetPath), expectedGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        changed++;
                        continue;
                    }

                    if (AssetDatabase.DeleteAsset(targetPath))
                        deleted++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            if (data.exportLinks != null)
            {
                var targetSet = new HashSet<string>(targets.Select(NormalizeAssetPath), StringComparer.OrdinalIgnoreCase);
                data.exportLinks.RemoveAll(link =>
                    link != null &&
                    (string.Equals(link.lastExportSessionId, session.sessionId, StringComparison.OrdinalIgnoreCase) ||
                     targetSet.Contains(NormalizeAssetPath(link.targetAssetPath))));
            }

            SyncExportChainDictionary(data);

            data.exportSessions.RemoveAt(data.exportSessions.Count - 1);
            data.lastExportTime = data.exportSessions.Count > 0 ? data.exportSessions[data.exportSessions.Count - 1].exportTime : string.Empty;
            data.lastExportRootPath = data.exportSessions.Count > 0 ? data.exportSessions[data.exportSessions.Count - 1].exportRootPath : string.Empty;
            data.lastExportAssetCount = data.exportSessions.Count > 0 ? data.exportSessions[data.exportSessions.Count - 1].totalAssetCount : 0;
            data.lastExportDependencyCount = data.exportSessions.Count > 0 ? data.exportSessions[data.exportSessions.Count - 1].dependencyAssetCount : 0;

            RemoveEmptyFolders(exportRoot);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("资产包导出回退", $"回退完成。\n删除: {deleted}\n已不存在: {missing}\n已被修改而跳过: {changed}", "确定");
        }

        private static bool AddExportPath(string path, string exportRoot, HashSet<string> exportPaths)
        {
            path = NormalizeAssetPath(path);
            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.StartsWith(exportRoot + "/", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.IsValidFolder(path) ||
                IsCodeOrEditorOnlyDependency(path))
                return false;

            if (IsOnlyEditorSOAsset(path))
                return false;

            return exportPaths.Add(path);
        }

        private static bool ValidateExportRootForUse(string exportRoot)
        {
            exportRoot = NormalizeAssetPath(exportRoot);
            if (IsPathInsideRoot(exportRoot, "Assets/Resources") ||
                IsPathInsideRoot(exportRoot, "Assets/Editor Default Resources") ||
                IsPathInsideRoot(exportRoot, "Assets/Editor") ||
                exportRoot.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                exportRoot.EndsWith("/Editor", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "资源包导出已阻止",
                    "导出根目录不能放在 Unity 自动打包或编辑器专用目录下。\n\n" +
                    "当前目录:\n" + exportRoot + "\n\n" +
                    "禁止目录:\n" +
                    "- Assets/Resources\n" +
                    "- Assets/Editor Default Resources\n" +
                    "- Assets/Editor 或任意 /Editor/ 子目录",
                    "确定");
                return false;
            }

            if (IsPathInsideRoot(exportRoot, "Assets/StreamingAssets"))
            {
                return EditorUtility.DisplayDialog(
                    "确认导出到资源目录",
                    "当前导出根目录会进入资源管理或构建链路。\n\n" +
                    "当前目录:\n" + exportRoot + "\n\n" +
                    "这适合正式分离选用资源；如果只是临时预览，建议使用默认目录:\n" +
                    "Assets/_ESAssetPackageExport\n\n" +
                    "是否继续复制勾选资产？",
                    "继续导出",
                    "取消");
            }

            return true;
        }

        private static Dictionary<string, ESAssetPackageExportLink> BuildExportLinkLookup(List<ESAssetPackageExportLink> links)
        {
            var result = new Dictionary<string, ESAssetPackageExportLink>(StringComparer.OrdinalIgnoreCase);
            if (links == null)
                return result;

            for (int i = 0; i < links.Count; i++)
            {
                ESAssetPackageExportLink link = links[i];
                if (link == null)
                    continue;

                if (!string.IsNullOrEmpty(link.sourceGuid))
                    result[link.sourceGuid] = link;
                if (!string.IsNullOrEmpty(link.sourceAssetPath))
                    result[NormalizeAssetPath(link.sourceAssetPath)] = link;
            }

            return result;
        }

        private static string ResolveExportTargetPath(
            string sourcePath,
            string categoryFolder,
            string fileNamePrefix,
            Dictionary<string, ESAssetPackageExportLink> previousLinks,
            HashSet<string> usedTargetPaths,
            bool overwriteExistingExport,
            out bool overwrite,
            out string skipReason)
        {
            overwrite = false;
            skipReason = string.Empty;
            sourcePath = NormalizeAssetPath(sourcePath);
            categoryFolder = NormalizeAssetPath(categoryFolder);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            ESAssetPackageExportLink oldLink = null;
            if (!string.IsNullOrEmpty(sourceGuid))
                previousLinks?.TryGetValue(sourceGuid, out oldLink);
            if (oldLink == null)
                previousLinks?.TryGetValue(sourcePath, out oldLink);

            string oldTarget = NormalizeAssetPath(oldLink?.targetAssetPath);
            bool oldTargetValid = !string.IsNullOrEmpty(oldTarget) &&
                                  oldTarget.StartsWith("Assets/", StringComparison.Ordinal) &&
                                  IsPathInsideRoot(oldTarget, categoryFolder);
            bool oldTargetExists = oldTargetValid && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(oldTarget) != null;
            if (oldTargetExists && !overwriteExistingExport)
            {
                skipReason = "已有有效导出链路";
                return string.Empty;
            }

            if (oldTargetExists && overwriteExistingExport && !usedTargetPaths.Contains(oldTarget))
            {
                usedTargetPaths.Add(oldTarget);
                overwrite = true;
                return oldTarget;
            }

            string desired = BuildPrefixedTargetPath(sourcePath, categoryFolder, fileNamePrefix);
            if (usedTargetPaths.Contains(desired))
            {
                skipReason = "本次导出目标路径重复: " + desired;
                return string.Empty;
            }

            UnityEngine.Object existingTarget = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(desired);
            if (existingTarget != null)
            {
                skipReason = "目标已存在且没有可覆盖链路: " + desired;
                return string.Empty;
            }

            usedTargetPaths.Add(desired);
            return desired;
        }

        private static string BuildPrefixedTargetPath(string sourcePath, string categoryFolder, string fileNamePrefix)
        {
            string extension = Path.GetExtension(sourcePath);
            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            fileNamePrefix = SanitizeFileNamePrefix(fileNamePrefix);
            if (!string.IsNullOrEmpty(fileNamePrefix) && !fileName.StartsWith(fileNamePrefix, StringComparison.OrdinalIgnoreCase))
                fileName = fileNamePrefix + fileName;

            return NormalizeAssetPath($"{categoryFolder}/{fileName}{extension}");
        }

        private static bool DisplayExportPreflight(
            ESAssetPackageBakeData data,
            string exportRoot,
            List<ExportPlanItem> plan,
            HashSet<string> rootPaths,
            HashSet<string> dependencyPaths,
            List<string> duplicateSkipped)
        {
            int rootCount = plan.Count(x => x.rootSelected);
            int dependencyCount = plan.Count(x => x.dependency);
            int overwriteCount = plan.Count(x => x.overwrite);
            string message =
                $"导出前确认\n\n" +
                $"配置: {(string.IsNullOrWhiteSpace(data.exportConfigName) ? data.displayName : data.exportConfigName)}\n" +
                $"导出根目录: {exportRoot}\n" +
                $"命名前缀: {data.exportFileNamePrefix}\n" +
                $"按类型分目录: 已启用\n" +
                $"重复导出覆盖: {(data.overwriteExistingExport ? "允许覆盖已有链路目标" : "默认跳过已有有效链路")}\n\n" +
                $"直接选中: {rootPaths.Count}\n" +
                $"计划复制直接资源: {rootCount}\n" +
                $"依赖资源: {dependencyPaths.Count}\n" +
                $"计划复制依赖: {dependencyCount}\n" +
                $"计划覆盖: {overwriteCount}\n" +
                $"重复/冲突跳过: {duplicateSkipped.Count}\n\n";

            message += "事务策略: 先暂存全部源资产并备份已有目标，全部校验通过后才提交；任一阶段失败将尝试自动回滚。\n\n";

            if (dependencyPaths.Count > 0)
                message += "依赖文件预览:\n" + string.Join("\n", dependencyPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Take(18)) + "\n\n";

            if (duplicateSkipped.Count > 0)
                message += "重复/冲突预览:\n" + string.Join("\n", duplicateSkipped.Take(12)) + "\n\n";

            message += "是否继续导出？";
            return EditorUtility.DisplayDialog("资源包导出前通报", message, "继续导出", "取消");
        }

        private static string ResolveExportTargetPath(
            string sourcePath,
            string categoryFolder,
            Dictionary<string, ESAssetPackageExportLink> previousLinks,
            HashSet<string> usedTargetPaths)
        {
            sourcePath = NormalizeAssetPath(sourcePath);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            ESAssetPackageExportLink oldLink = null;
            if (!string.IsNullOrEmpty(sourceGuid))
                previousLinks?.TryGetValue(sourceGuid, out oldLink);
            if (oldLink == null)
                previousLinks?.TryGetValue(sourcePath, out oldLink);

            string oldTarget = NormalizeAssetPath(oldLink?.targetAssetPath);
            if (!string.IsNullOrEmpty(oldTarget) &&
                oldTarget.StartsWith("Assets/", StringComparison.Ordinal) &&
                IsPathInsideRoot(oldTarget, categoryFolder) &&
                !usedTargetPaths.Contains(oldTarget))
            {
                usedTargetPaths.Add(oldTarget);
                return oldTarget;
            }

            string desired = NormalizeAssetPath($"{categoryFolder}/{Path.GetFileName(sourcePath)}");
            string target = desired;
            string existingGuid = AssetDatabase.AssetPathToGUID(target);
            if (!string.IsNullOrEmpty(existingGuid) && !string.Equals(existingGuid, sourceGuid, StringComparison.OrdinalIgnoreCase))
                target = AssetDatabase.GenerateUniqueAssetPath(desired);

            while (usedTargetPaths.Contains(target))
                target = AssetDatabase.GenerateUniqueAssetPath(target);

            usedTargetPaths.Add(target);
            return target;
        }

        private static void UpdateExportLinks(
            ESAssetPackageBakeData data,
            Dictionary<string, string> copiedPathMap,
            Dictionary<string, bool> rootSelectedByPath,
            string sessionId,
            string exportTime)
        {
            if (data.exportLinks == null)
                data.exportLinks = new List<ESAssetPackageExportLink>();

            var lookup = BuildExportLinkLookup(data.exportLinks);
            foreach (var pair in copiedPathMap)
            {
                string sourcePath = NormalizeAssetPath(pair.Key);
                string targetPath = NormalizeAssetPath(pair.Value);
                string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                ESAssetPackageExportLink link = null;
                if (!string.IsNullOrEmpty(sourceGuid))
                    lookup.TryGetValue(sourceGuid, out link);
                if (link == null)
                    lookup.TryGetValue(sourcePath, out link);

                if (link == null)
                {
                    link = new ESAssetPackageExportLink();
                    data.exportLinks.Add(link);
                }

                Type type = AssetDatabase.GetMainAssetTypeAtPath(sourcePath);
                link.sourceGuid = sourceGuid;
                link.sourceAssetPath = sourcePath;
                link.targetGuid = AssetDatabase.AssetPathToGUID(targetPath);
                link.targetAssetPath = targetPath;
                link.category = DetermineCategory(sourcePath, type);
                link.rootSelected = rootSelectedByPath != null && rootSelectedByPath.TryGetValue(sourcePath, out bool rootSelected) && rootSelected;
                link.lastExportSessionId = sessionId;
                link.lastExportTime = exportTime;
                link.exportCount++;
            }

            data.exportLinks.Sort((a, b) => string.Compare(a?.targetAssetPath, b?.targetAssetPath, StringComparison.OrdinalIgnoreCase));
        }

        private static void SyncExportChainDictionary(ESAssetPackageBakeData data)
        {
            if (data == null)
                return;

            data.exportLinks ??= new List<ESAssetPackageExportLink>();
            data.exportChainBySourceGuid ??= new Dictionary<string, ESAssetPackageExportChain>();
            data.exportChainBySourceGuid.Clear();

            for (int i = 0; i < data.exportLinks.Count; i++)
            {
                ESAssetPackageExportLink link = data.exportLinks[i];
                if (link == null || string.IsNullOrEmpty(link.sourceGuid))
                    continue;

                var chain = new ESAssetPackageExportChain();
                chain.FromLink(link);
                data.exportChainBySourceGuid[link.sourceGuid] = chain;
            }
        }

        private static void MarkRecordsWithValidExportLinksAsSelected(ESAssetPackageBakeData data)
        {
            if (data == null || data.records == null || data.exportLinks == null)
                return;

            var exportedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < data.exportLinks.Count; i++)
            {
                ESAssetPackageExportLink link = data.exportLinks[i];
                if (link == null || string.IsNullOrEmpty(link.targetAssetPath))
                    continue;

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(link.targetAssetPath) == null)
                    continue;

                if (!string.IsNullOrEmpty(link.sourceGuid))
                    exportedSources.Add(link.sourceGuid);
                if (!string.IsNullOrEmpty(link.sourceAssetPath))
                    exportedSources.Add(NormalizeAssetPath(link.sourceAssetPath));
            }

            for (int i = 0; i < data.records.Count; i++)
            {
                ESAssetPackageBakeRecord record = data.records[i];
                if (record == null)
                    continue;

                if ((!string.IsNullOrEmpty(record.guid) && exportedSources.Contains(record.guid)) ||
                    (!string.IsNullOrEmpty(record.assetPath) && exportedSources.Contains(NormalizeAssetPath(record.assetPath))))
                {
                    record.selectedForUse = true;
                }
            }
        }

        private static void TrimExportSessions(List<ESAssetPackageExportSession> sessions, int keepCount)
        {
            if (sessions == null || keepCount <= 0)
                return;

            while (sessions.Count > keepCount)
                sessions.RemoveAt(0);
        }

        private static bool IsPathInsideRoot(string path, string root)
        {
            path = NormalizeAssetPath(path);
            root = NormalizeAssetPath(root);
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }


        private static void RemoveEmptyFolders(string root)
        {
            root = NormalizeAssetPath(root);
            if (!AssetDatabase.IsValidFolder(root))
                return;

            string fullRoot = AssetPathToFullPath(root);
            if (!Directory.Exists(fullRoot))
                return;

            foreach (string directory in ESManagedFileIO.EnumerateDirectoriesSafely(fullRoot)
                         .OrderByDescending(d => d.Length))
            {
                if (Directory.EnumerateFileSystemEntries(directory).Any())
                    continue;

                string assetPath = FullPathToAssetPath(directory);
                if (IsPathInsideRoot(assetPath, root) && !string.Equals(assetPath, root, StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static bool IsCodeOrEditorOnlyDependency(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".cs" ||
                   ext == ".asmdef" ||
                   ext == ".dll" ||
                   ext == ".pdb" ||
                   ext == ".mdb";
        }

        private static bool IsOnlyEditorSOAsset(string path)
        {
            path = NormalizeAssetPath(path);
            if (string.IsNullOrEmpty(path))
                return false;

            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type != null &&
                   typeof(ScriptableObject).IsAssignableFrom(type) &&
                   Attribute.IsDefined(type, typeof(ESOnlyEditorSOAttribute), true);
        }

        private static int RemapCopiedAssetGuids(Dictionary<string, string> copiedPathMap)
        {
            if (copiedPathMap == null || copiedPathMap.Count == 0)
                return 0;

            var guidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in copiedPathMap)
            {
                string oldGuid = AssetDatabase.AssetPathToGUID(pair.Key);
                string newGuid = AssetDatabase.AssetPathToGUID(pair.Value);
                if (!string.IsNullOrEmpty(oldGuid) && !string.IsNullOrEmpty(newGuid) && oldGuid != newGuid)
                    guidMap[oldGuid] = newGuid;
            }

            int changedFiles = 0;
            foreach (string targetPath in copiedPathMap.Values)
            {
                if (!IsTextSerializedAsset(targetPath))
                    continue;

                string fullPath = AssetPathToFullPath(targetPath);
                if (!File.Exists(fullPath))
                    continue;

                string text = File.ReadAllText(fullPath);
                string newText = text;
                foreach (var pair in guidMap)
                    newText = newText.Replace(pair.Key, pair.Value);

                if (newText == text)
                    continue;

                ESManagedFileIO.WriteTextAtomic(fullPath, newText, new UTF8Encoding(false), Application.dataPath);
                changedFiles++;
            }

            return changedFiles;
        }

        private static bool IsTextSerializedAsset(string assetPath)
        {
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return ext == ".mat" ||
                   ext == ".prefab" ||
                   ext == ".anim" ||
                   ext == ".controller" ||
                   ext == ".overridecontroller" ||
                   ext == ".playable" ||
                   ext == ".asset" ||
                   ext == ".unity";
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return assetPath;

            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string FullPathToAssetPath(string fullPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(fullPath))
                return string.Empty;

            string normalizedFullPath = Path.GetFullPath(fullPath).Replace("\\", "/");
            string normalizedProjectRoot = Path.GetFullPath(projectRoot).Replace("\\", "/").TrimEnd('/');
            if (!normalizedFullPath.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return normalizedFullPath.Substring(normalizedProjectRoot.Length + 1);
        }

        public static ESAssetPackageCategory DetermineCategory(string path, Type type)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".prefab") return ESAssetPackageCategory.Prefab;
            if (ext == ".unity") return ESAssetPackageCategory.Scene;
            if (ext == ".mat") return ESAssetPackageCategory.Material;
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".psd" || ext == ".exr" || ext == ".tif" || ext == ".tiff") return ESAssetPackageCategory.Texture;
            if (ext == ".fbx" || ext == ".obj" || ext == ".blend" || ext == ".dae")
                return IsAnimationModelAsset(path) ? ESAssetPackageCategory.Animation : ESAssetPackageCategory.Model;
            if (ext == ".wav" || ext == ".mp3" || ext == ".ogg" || ext == ".aiff") return ESAssetPackageCategory.Audio;
            if (ext == ".anim") return ESAssetPackageCategory.Animation;
            if (ext == ".controller" || ext == ".overridecontroller" || ext == ".playable") return ESAssetPackageCategory.Other;
            if (ext == ".shader" || ext == ".shadergraph") return ESAssetPackageCategory.Shader;
            if (ext == ".ttf" || ext == ".otf" || ext == ".fontsettings") return ESAssetPackageCategory.Font;
            if (ext == ".mp4" || ext == ".mov" || ext == ".webm") return ESAssetPackageCategory.Video;

            if (type != null && typeof(ScriptableObject).IsAssignableFrom(type))
                return ESAssetPackageCategory.ScriptableObject;

            return ESAssetPackageCategory.Other;
        }

        private static bool IsAnimationModelAsset(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                return false;

            if (importer.animationType == ModelImporterAnimationType.None)
                return false;

            string normalizedPath = NormalizeAssetPath(path);
            string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
            bool animationNamedAsset = normalizedPath.IndexOf("/Animations/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       fileName.IndexOf('@') >= 0;
            if (!animationNamedAsset)
                return false;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips != null && clips.Length > 0)
                return true;

            ModelImporterClipAnimation[] defaultClips = importer.defaultClipAnimations;
            return defaultClips != null && defaultClips.Length > 0;
        }

        public static string GetExportSubFolder(ESAssetPackageCategory category)
        {
            return ESAssetPackageBakeData.GetDefaultExportSubFolder(category);
        }

        private static string FormatFileSize(string assetPath)
        {
            try
            {
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)));
                var info = new FileInfo(fullPath);
                if (!info.Exists)
                    return "未知";

                double size = info.Length;
                string[] units = { "B", "KB", "MB", "GB" };
                int unit = 0;
                while (size >= 1024 && unit < units.Length - 1)
                {
                    size /= 1024;
                    unit++;
                }

                return $"{size:F1} {units[unit]}";
            }
            catch
            {
                return "未知";
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").TrimEnd('/');
        }

        private static string SanitizeFileNamePrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return "ES选用_";

            string result = prefix.Trim().Replace("\\", "_").Replace("/", "_").Replace(":", "_");
            foreach (char c in Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');

            return string.IsNullOrWhiteSpace(result) ? "ES选用_" : result;
        }

        private static void EnsureAssetFolder(string folder)
        {
            folder = NormalizeAssetPath(folder);
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
#endif
}
