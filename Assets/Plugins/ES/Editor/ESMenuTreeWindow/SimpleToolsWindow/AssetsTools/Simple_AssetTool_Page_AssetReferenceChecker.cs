using ES;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 抑制私有字段未使用警告
#pragma warning disable CS0414
// 抑制无法访问的代码警告（提前return）
#pragma warning disable CS0162

namespace ES
{
    #region 商业级资源引用检查工具
    [Serializable]
    public class Page_AssetReferenceChecker : ESWindowPageBase
    {
        [Title("资产引用检查与清理工具", "分析资源依赖关系，标记疑似未使用资产并提供安全隔离", bold: true, titleAlignment: TitleAlignments.Centered)]

        [InfoBox("分析结果用于辅助判断，不会直接永久删除资源。清理会先移动到隔离区，确认项目正常后再手动删除。", InfoMessageType.Info)]
        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "选择检查范围，先分析引用，再人工确认。\n疑似未使用资源默认只隔离，不直接删除。";

        private string PanelSummary =>
            $"范围: {checkFolder} | 已检查: {totalFilesChecked} | 疑似未使用: {unusedAssets.Count} " +
            $"(高 {unusedAssets.Count(a => a.Confidence == "高")} / 中 {unusedAssets.Count(a => a.Confidence == "中")} / 低 {unusedAssets.Count(a => a.Confidence == "低")}) | " +
            $"引用结果: {selectedAssetReferences.Count} | 依赖结果: {selectedAssetDependencies.Count} | 状态: {(lastScanCanceled ? "不完整" : "完整")}";

        #region 基础设置
        [TabGroup("检查配置", "目标设置")]
        [LabelText("检查范围"), FolderPath, Space(5)]
        [InfoBox("选择要分析的文件夹范围。建议从Assets根目录开始以获得完整分析。")]
        public string checkFolder = "Assets";

        [TabGroup("检查配置", "目标设置")]
        [LabelText("排除文件夹"), FolderPath(AbsolutePath = false), Space(5)]
        [InfoBox("排除不需要检查的文件夹，如ThirdParty、Plugins等。")]
        public List<string> excludeFolders = new List<string> { "Assets/Plugins", "Assets/Editor" };

        [TabGroup("检查配置", "目标设置")]
        [LabelText("包含文件类型"), Space(5)]
        [InfoBox("指定要检查的文件类型。留空则检查所有类型。")]
        public List<string> includeExtensions = new List<string>();

        [TabGroup("检查配置", "目标设置")]
        [LabelText("排除文件类型"), Space(5)]
        [InfoBox("排除不需要检查的文件类型，如.meta、.cs、.txt、.md等。")]
        public List<string> excludeExtensions = new List<string> { ".meta", ".cs", ".js", ".dll", ".txt", ".md" };

        [TabGroup("检查配置", "资源包分离")]
        [LabelText("导入包根目录"), FolderPath(AbsolutePath = false), Space(5)]
        [InfoBox("填导入的大资源包目录，例如 Assets/ArtPack。工具只会在这个目录内部筛选候选资源。")]
        public string packageRootFolder = "Assets";

        [TabGroup("检查配置", "资源包分离")]
        [LabelText("已使用入口路径"), Space(5)]
        [InfoBox("可手填路径。更推荐使用下面的“已使用入口资产”直接拖资源。")]
        public List<string> packageUsedEntryPaths = new List<string>();

        [TabGroup("检查配置", "资源包分离")]
        [LabelText("已使用入口资产"), Space(5)]
        [InfoBox("拖入你确认会用的 Prefab、Scene、材质、模型、配置，或资源包里的文件夹。工具会保留入口和它们依赖到的资源。")]
        public List<UnityEngine.Object> packageUsedEntryAssets = new List<UnityEngine.Object>();

        [TabGroup("检查配置", "资源包分离")]
        [LabelText("保护包内入口资产")]
        [InfoBox("开启后，入口路径本身一定保留，不会被列入疑似未使用。")]
        public bool packageProtectEntryAssets = true;

        [TabGroup("检查配置", "安全保护")]
        [LabelText("保护入口资产"), Space(5)]
        [InfoBox("开启后，Resources、StreamingAssets、Editor Default Resources、Addressables配置、场景、Prefab、脚本等入口资产不会进入隔离候选。")]
        public bool protectEntryAssets = true;

        [TabGroup("检查配置", "安全保护")]
        [LabelText("额外保护路径"), FolderPath(AbsolutePath = false), Space(5)]
        public List<string> protectedFolders = new List<string>
        {
            "Assets/Resources",
            "Assets/StreamingAssets",
            "Assets/Editor Default Resources",
            "Assets/AddressableAssetsData"
        };
        #endregion

        #region 高级选项
        [TabGroup("检查配置", "高级选项")]
        [LabelText("启用深度分析"), Space(5)]
        [InfoBox("深度分析模式：检查所有引用链，包括间接引用。准确但较慢。")]
        public bool deepAnalysis = true;

        [TabGroup("检查配置", "高级选项")]
        [LabelText("检查场景引用"), Space(5)]
        [InfoBox("分析场景文件中的引用关系。")]
        public bool checkScenes = true;

        [TabGroup("检查配置", "高级选项")]
        [LabelText("检查预制件引用"), Space(5)]
        [InfoBox("分析预制件文件中的引用关系。")]
        public bool checkPrefabs = true;

        [TabGroup("检查配置", "高级选项")]
        [LabelText("检查脚本引用"), Space(5)]
        [InfoBox("分析脚本中的资源引用（通过AssetDatabase）。")]
        public bool checkScripts = true;

        [TabGroup("检查配置", "高级选项")]
        [LabelText("启用缓存优化"), Space(5)]
        [InfoBox("使用缓存机制提升重复检查的性能。")]
        public bool useCache = true;

        [TabGroup("检查配置", "高级选项")]
        [LabelText("内存优化模式"), Space(5)]
        [InfoBox("在大项目中启用以减少内存使用，但会略微降低性能。")]
        public bool memoryOptimization = false;
        #endregion

        #region 结果显示
        [TabGroup("分析结果", "疑似未使用")]
        [HideInInspector]
        public List<AssetReferenceInfo> unusedAssets = new List<AssetReferenceInfo>();

        private string UnusedStats => $"总文件数: {totalFilesChecked}, 疑似未使用: {unusedAssets.Count}, 结果状态: {(lastScanCanceled ? "扫描已取消，不完整" : "完整")}";

        [TabGroup("分析结果", "引用分析")]
        [HideInInspector]
        public List<AssetReferenceInfo> selectedAssetReferences = new List<AssetReferenceInfo>();

        private string ReferenceStats => $"直接引用: {selectedAssetReferences.Count(r => !r.IsIndirect)}, 间接引用: {selectedAssetReferences.Count(r => r.IsIndirect)}";

        [TabGroup("分析结果", "依赖分析")]
        [HideInInspector]
        public List<AssetReferenceInfo> selectedAssetDependencies = new List<AssetReferenceInfo>();

        private string DependencyStats => $"直接依赖: {selectedAssetDependencies.Count(r => !r.IsIndirect)}, 间接依赖: {selectedAssetDependencies.Count(r => r.IsIndirect)}";

        [HideInInspector]
        public List<AssetReferenceInfo> packageKeepAssets = new List<AssetReferenceInfo>();

        [HideInInspector]
        public List<AssetReferenceInfo> packageExternalDependencies = new List<AssetReferenceInfo>();

        private string PackageSeparateStats =>
            $"包内保留: {packageKeepAssets.Count} | 包外依赖: {packageExternalDependencies.Count} | 包内可隔离候选: {unusedAssets.Count}";
        #endregion

        #region 数据结构
        [Serializable]
        public class AssetReferenceInfo
        {
            [DisplayAsString, LabelWidth(100), HorizontalGroup("AssetInfo", 0.7f)]
            public string AssetPath;

            [DisplayAsString, LabelWidth(80), HorizontalGroup("AssetInfo", 0.15f)]
            public string FileSize;

            [DisplayAsString, LabelWidth(100), HorizontalGroup("AssetInfo", 0.15f)]
            public string LastModified;

            [DisplayAsString, LabelWidth(70), HorizontalGroup("AssetMeta")]
            public string Confidence;

            [DisplayAsString, LabelWidth(90), HorizontalGroup("AssetMeta")]
            public string Reason;

            [HorizontalGroup("AssetInfo", 50), Button("📂", ButtonHeight = 20), GUIColor(0.4f, 0.8f, 1f)]
            public void JumpToAsset()
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetPath);
                if (asset != null)
                {
                    // 选中资源并在Project窗口中显示
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);

                    // 根据资源类型决定如何打开
                    string extension = Path.GetExtension(AssetPath).ToLower();
                    if (extension == ".cs" || extension == ".shader" || extension == ".txt" || extension == ".json")
                    {
                        // 打开脚本/文本文件进行编辑
                        AssetDatabase.OpenAsset(asset);
                    }
                    else if (extension == ".prefab" || extension == ".unity")
                    {
                        // 对于预制件和场景，只在Project中选中
                        // 用户可以手动双击打开
                    }
                    else
                    {
                        // 对于其他资源类型，尝试打开
                        AssetDatabase.OpenAsset(asset);
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", $"无法找到资源: {AssetPath}", "确定");
                }
            }

            // 自定义显示名称
            public override string ToString()
            {
                return $"{Path.GetFileName(AssetPath)} ({FileSize}) - {LastModified} - {Confidence} - {Reason}";
            }

            [HideInInspector]
            public bool IsIndirect;

            public AssetReferenceInfo(string path, bool indirect = false, string confidence = "中", string reason = "未发现显式依赖")
            {
                AssetPath = path;
                // 将Unity Asset路径转换为文件系统路径
                string fullPath = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
                FileSize = GetFileSizeString(fullPath);
                LastModified = GetLastModifiedString(fullPath);
                IsIndirect = indirect;
                Confidence = confidence;
                Reason = reason;
            }

            private static string GetFileSizeString(string path)
            {
                try
                {
                    var fi = new FileInfo(path);
                    return fi.Exists ? FormatFileSize(fi.Length) : "未知";
                }
                catch { return "未知"; }
            }

            private static string GetLastModifiedString(string path)
            {
                try
                {
                    var fi = new FileInfo(path);
                    return fi.Exists ? fi.LastWriteTime.ToString("yyyy-MM-dd") : "未知";
                }
                catch { return "未知"; }
            }

            private static string FormatFileSize(long bytes)
            {
                string[] sizes = { "B", "KB", "MB", "GB" };
                int order = 0;
                double size = bytes;
                while (size >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    size /= 1024;
                }
                return $"{size:F1} {sizes[order]}";
            }
        }
        #endregion

        #region 自定义绘制
        [OnInspectorGUI, PropertyOrder(-200)]
        private void DrawCustomLists()
        {
            DrawAssetReferenceWorkbench();
        }

        private void DrawAssetReferenceWorkbench()
        {
            DrawWorkbenchHeader();
            DrawTargetSnapshot();
            DrawQuickSettings();
            DrawWorkflowActions();
            DrawResultDashboard();
            DrawResultFilters();
            DrawCommercialResultLists();
        }

        private void DrawWorkbenchHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("资源引用体检台", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("先确认扫描范围和当前选中资源，再执行引用、依赖、未使用或资源包分离分析。所有清理只进入隔离区，不会直接永久删除。", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetric("检查范围", checkFolder);
                    DrawMetric("已检查", totalFilesChecked.ToString());
                    DrawMetric("疑似未用", unusedAssets.Count.ToString());
                    DrawMetric("引用/依赖", $"{selectedAssetReferences.Count}/{selectedAssetDependencies.Count}");
                    DrawMetric("状态", lastScanCanceled ? "结果不完整" : "可用");
                }
            }
        }

        private void DrawTargetSnapshot()
        {
            var selected = Selection.activeObject;
            string selectedPath = selected == null ? "" : SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(selected));
            bool hasSelectedAsset = selected != null && IsValidAssetFile(selectedPath);

            SimpleToolsPanelUtility.DrawSectionTitle("当前选择", "这里决定引用/依赖分析的目标，也说明未使用扫描会覆盖哪些范围。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawInfoRow("扫描文件夹", checkFolder);
                DrawInfoRow("排除文件夹", excludeFolders.Count == 0 ? "无" : string.Join(", ", excludeFolders));
                DrawInfoRow("文件类型", includeExtensions.Count == 0 ? $"全部有效资源，排除 {string.Join(", ", excludeExtensions)}" : string.Join(", ", includeExtensions));
                DrawInfoRow("安全保护", protectEntryAssets ? $"开启，保护入口和 {protectedFolders.Count} 个保护路径" : "关闭");
                DrawInfoRow("当前选中", hasSelectedAsset ? $"{selected.name}  |  {selectedPath}" : "未选中 Project 资源。引用/依赖分析前，请先在 Project 窗口点一个资源。");
                DrawInfoRow("资源包入口", $"{packageRootFolder}  |  路径 {packageUsedEntryPaths.Count} 个，拖入资产 {packageUsedEntryAssets.Count(a => a != null)} 个");

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = selected != null;
                    if (GUILayout.Button("用选中目录作为扫描范围", EditorStyles.miniButtonLeft, GUILayout.Height(24)))
                        UseSelectionAsCheckFolder();
                    if (GUILayout.Button("加入资源包入口", EditorStyles.miniButtonMid, GUILayout.Height(24)))
                        AddSelectionToPackageEntries();
                    GUI.enabled = packageUsedEntryPaths.Count > 0 || packageUsedEntryAssets.Any(a => a != null);
                    if (GUILayout.Button("清空入口", EditorStyles.miniButtonRight, GUILayout.Height(24)))
                        ClearPackageEntries();
                    GUI.enabled = true;
                }
            }
        }

        private void DrawQuickSettings()
        {
            foldoutQuickSettings = EditorGUILayout.Foldout(foldoutQuickSettings, "常用设置", true);
            if (!foldoutQuickSettings)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                checkFolder = EditorGUILayout.TextField("扫描范围", checkFolder);
                packageRootFolder = EditorGUILayout.TextField("资源包根目录", packageRootFolder);

                using (new EditorGUILayout.HorizontalScope())
                {
                    deepAnalysis = GUILayout.Toggle(deepAnalysis, "深度分析", EditorStyles.miniButtonLeft, GUILayout.Height(24));
                    protectEntryAssets = GUILayout.Toggle(protectEntryAssets, "保护入口", EditorStyles.miniButtonMid, GUILayout.Height(24));
                    useCache = GUILayout.Toggle(useCache, "缓存优化", EditorStyles.miniButtonMid, GUILayout.Height(24));
                    memoryOptimization = GUILayout.Toggle(memoryOptimization, "低内存", EditorStyles.miniButtonRight, GUILayout.Height(24));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    checkScenes = GUILayout.Toggle(checkScenes, "场景", EditorStyles.miniButtonLeft, GUILayout.Height(22));
                    checkPrefabs = GUILayout.Toggle(checkPrefabs, "Prefab", EditorStyles.miniButtonMid, GUILayout.Height(22));
                    checkScripts = GUILayout.Toggle(checkScripts, "脚本", EditorStyles.miniButtonRight, GUILayout.Height(22));
                }
            }
        }

        private void DrawWorkflowActions()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("执行分析", "四个入口对应四种明确问题：哪些没用、谁在用它、它用到了谁、导入包哪些能剥离。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(GetNextActionText(), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("扫描疑似未用", SimpleToolsActionTone.Primary, 32))
                        FindUnusedAssets();
                    if (SimpleToolsPanelUtility.DrawActionButton("查谁引用了它", SimpleToolsActionTone.Warning, 32))
                        FindReferencesToSelected();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("查看它的依赖", SimpleToolsActionTone.Success, 30))
                        FindDependenciesOfSelected();
                    if (SimpleToolsPanelUtility.DrawActionButton("资源包瘦身分析", SimpleToolsActionTone.Primary, 30))
                        SeparatePackageByUsedEntries();
                }

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = unusedAssets.Count > 0;
                    if (GUILayout.Button("选中候选", EditorStyles.miniButtonLeft, GUILayout.Height(24)))
                        SelectUnusedAssets();
                    if (GUILayout.Button("移入隔离区", EditorStyles.miniButtonMid, GUILayout.Height(24)))
                        QuarantineUnusedAssets();
                    GUI.enabled = HasAnyResult();
                    if (GUILayout.Button("导出报告", EditorStyles.miniButtonMid, GUILayout.Height(24)))
                        ExportAnalysisReport();
                    GUI.enabled = true;
                    if (GUILayout.Button("清空结果", EditorStyles.miniButtonRight, GUILayout.Height(24)))
                        ClearResults();
                }
            }
        }

        private void DrawResultDashboard()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("分析结果", "结果区只放扫描产物和复核动作，避免和配置项混在一起。");

            if (!HasAnyResult() && string.IsNullOrWhiteSpace(lastResultSummary))
            {
                SimpleToolsPanelUtility.DrawEmptyState("还没有结果。先看“当前选择”是否正确，然后点上面的一个分析按钮。未使用扫描读取检查范围；引用/依赖分析读取当前 Project 选中资源；资源包瘦身读取包根目录和入口资产。");
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!string.IsNullOrWhiteSpace(lastResultSummary))
                    EditorGUILayout.LabelField(lastResultSummary, EditorStyles.boldLabel);

                if (!string.IsNullOrWhiteSpace(lastResultDetail))
                    EditorGUILayout.TextArea(lastResultDetail, GUILayout.MinHeight(36), GUILayout.MaxHeight(92));

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetric("高置信", unusedAssets.Count(a => a.Confidence == "高").ToString());
                    DrawMetric("中置信", unusedAssets.Count(a => a.Confidence == "中").ToString());
                    DrawMetric("低置信", unusedAssets.Count(a => a.Confidence == "低").ToString());
                    DrawMetric("包内保留", packageKeepAssets.Count.ToString());
                    DrawMetric("包外依赖", packageExternalDependencies.Count.ToString());
                }
            }
        }

        private void DrawResultFilters()
        {
            if (!HasAnyResult())
                return;

            SimpleToolsPanelUtility.DrawSectionTitle("结果筛选", "搜索会匹配路径、文件名、置信度和判断原因。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", EditorStyles.miniBoldLabel, GUILayout.Width(42));
                    resultSearch = EditorGUILayout.TextField(resultSearch);
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                        resultSearch = string.Empty;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("置信度", EditorStyles.miniBoldLabel, GUILayout.Width(52));
                    confidenceFilterIndex = GUILayout.Toolbar(confidenceFilterIndex, ConfidenceFilterLabels, EditorStyles.miniButton, GUILayout.Height(22));
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField("排序", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                    resultSortIndex = GUILayout.Toolbar(resultSortIndex, ResultSortLabels, EditorStyles.miniButton, GUILayout.Width(238), GUILayout.Height(22));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("每页", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                    int pageSizeIndex = Mathf.Clamp(Array.IndexOf(PageSizeOptions, pageSize), 0, PageSizeOptions.Length - 1);
                    pageSizeIndex = GUILayout.Toolbar(pageSizeIndex, PageSizeLabels, EditorStyles.miniButton, GUILayout.Width(126), GUILayout.Height(22));
                    pageSize = PageSizeOptions[pageSizeIndex];
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("打开隔离区", EditorStyles.miniButton, GUILayout.Width(82), GUILayout.Height(22)))
                        RevealQuarantineFolder();
                }
            }
        }

        private void DrawCommercialResultLists()
        {
            DrawResultInsightPanel();
            foldoutUnused = DrawAssetTableFoldout("疑似未使用资源", unusedAssets, ref currentPageUnused, foldoutUnused, "这里会显示未被依赖图引用、且没有命中保护规则的隔离候选。");
            foldoutReferences = DrawAssetTableFoldout("谁引用了当前选中资源", selectedAssetReferences, ref currentPageReferences, foldoutReferences, "先在 Project 窗口选择一个资源，再执行“查谁引用了它”。");
            foldoutDependencies = DrawAssetTableFoldout("当前选中资源依赖了谁", selectedAssetDependencies, ref currentPageDependencies, foldoutDependencies, "先在 Project 窗口选择一个资源，再执行“查看它的依赖”。");

            if (packageKeepAssets.Count > 0 || packageExternalDependencies.Count > 0)
            {
                DrawAssetTableFoldout("资源包内需要保留", packageKeepAssets, ref currentPagePackageKeep, true, "入口资产和它们依赖到的包内资源。");
                DrawAssetTableFoldout("资源包外部依赖", packageExternalDependencies, ref currentPagePackageExternal, true, "入口资产依赖到包外资源，迁移包时需要一起评估。");
            }
        }

        private bool DrawAssetTableFoldout(string title, List<AssetReferenceInfo> assetList, ref int currentPage, bool foldout, string emptyHint)
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int filteredCount = CountFilteredAssets(assetList);
                string countText = filteredCount == assetList.Count ? assetList.Count.ToString() : $"{filteredCount}/{assetList.Count}";
                foldout = EditorGUILayout.Foldout(foldout, $"{title}  ({countText})", true);
                if (!foldout)
                    return foldout;

                if (assetList.Count == 0)
                {
                    EditorGUILayout.LabelField(emptyHint, EditorStyles.wordWrappedMiniLabel);
                    return foldout;
                }

                var filtered = GetFilteredAssets(assetList);
                if (filtered.Count == 0)
                {
                    EditorGUILayout.LabelField("当前筛选条件下没有命中结果。可以清空搜索或切换置信度。", EditorStyles.wordWrappedMiniLabel);
                    return foldout;
                }

                DrawAssetTable(filtered, ref currentPage);
                return foldout;
            }
        }

        private void DrawResultInsightPanel()
        {
            if (!HasAnyResult())
                return;

            var candidates = GetFilteredAssets(unusedAssets);
            if (candidates.Count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("候选聚类", EditorStyles.boldLabel);
                DrawInfoRow("按类型", BuildTopExtensionSummary(candidates, 6));
                DrawInfoRow("按目录", BuildTopDirectorySummary(candidates, 5));
                DrawInfoRow("体积估算", $"当前筛选候选约 {FormatFileSize(SumKnownFileSize(candidates))}，低置信候选 {candidates.Count(a => a.Confidence == "低")} 个。");
            }
        }

        private void DrawAssetTable(List<AssetReferenceInfo> assetList, ref int currentPage)
        {
            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)assetList.Count / pageSize));
            currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("资源路径", EditorStyles.miniBoldLabel, GUILayout.MinWidth(260));
                EditorGUILayout.LabelField("大小", EditorStyles.miniBoldLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField("日期", EditorStyles.miniBoldLabel, GUILayout.Width(86));
                EditorGUILayout.LabelField("判断", EditorStyles.miniBoldLabel, GUILayout.Width(150));
                GUILayout.Space(54);
            }

            int start = currentPage * pageSize;
            int end = Mathf.Min(start + pageSize, assetList.Count);
            for (int i = start; i < end; i++)
            {
                var asset = assetList[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(asset.AssetPath, EditorStyles.miniLabel, GUILayout.MinWidth(260));
                    EditorGUILayout.LabelField(asset.FileSize, EditorStyles.miniLabel, GUILayout.Width(70));
                    EditorGUILayout.LabelField(asset.LastModified, EditorStyles.miniLabel, GUILayout.Width(86));
                    EditorGUILayout.LabelField($"{asset.Confidence} {asset.Reason}", EditorStyles.miniLabel, GUILayout.Width(150));
                    if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(48)))
                        asset.JumpToAsset();
                }
            }

            if (totalPages > 1)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("上一页", EditorStyles.miniButtonLeft, GUILayout.Width(64)) && currentPage > 0)
                        currentPage--;
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"第 {currentPage + 1} / {totalPages} 页", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(90));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("下一页", EditorStyles.miniButtonRight, GUILayout.Width(64)) && currentPage < totalPages - 1)
                        currentPage++;
                }
            }
        }

        private void DrawMetric(string label, string value)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(72)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(value) ? "-" : value, EditorStyles.boldLabel);
            }
        }

        private void DrawInfoRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(86));
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(value) ? "-" : value, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private bool HasAnyResult()
        {
            return unusedAssets.Count > 0 ||
                   selectedAssetReferences.Count > 0 ||
                   selectedAssetDependencies.Count > 0 ||
                   packageKeepAssets.Count > 0 ||
                   packageExternalDependencies.Count > 0;
        }

        private int CountFilteredAssets(List<AssetReferenceInfo> assetList)
        {
            if (assetList == null || assetList.Count == 0)
                return 0;

            int count = 0;
            foreach (var asset in assetList)
            {
                if (PassesResultFilter(asset))
                    count++;
            }
            return count;
        }

        private List<AssetReferenceInfo> GetFilteredAssets(List<AssetReferenceInfo> assetList)
        {
            if (assetList == null || assetList.Count == 0)
                return new List<AssetReferenceInfo>();

            return SortAssets(assetList.Where(PassesResultFilter)).ToList();
        }

        private IEnumerable<AssetReferenceInfo> SortAssets(IEnumerable<AssetReferenceInfo> assets)
        {
            switch (resultSortIndex)
            {
                case 1:
                    return assets.OrderByDescending(GetConfidenceSort).ThenBy(a => a.AssetPath);
                case 2:
                    return assets.OrderByDescending(GetAssetFileSize).ThenBy(a => a.AssetPath);
                case 3:
                    return assets.OrderByDescending(GetAssetLastWriteTime).ThenBy(a => a.AssetPath);
                case 4:
                    return assets.OrderBy(a => Path.GetExtension(a.AssetPath)).ThenBy(a => a.AssetPath);
                default:
                    return assets.OrderBy(a => a.AssetPath);
            }
        }

        private bool PassesResultFilter(AssetReferenceInfo asset)
        {
            if (asset == null)
                return false;

            if (confidenceFilterIndex > 0)
            {
                string expected = ConfidenceFilterLabels[Mathf.Clamp(confidenceFilterIndex, 0, ConfidenceFilterLabels.Length - 1)];
                if (!string.Equals(asset.Confidence, expected, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (string.IsNullOrWhiteSpace(resultSearch))
                return true;

            string keyword = resultSearch.Trim();
            return ContainsIgnoreCase(asset.AssetPath, keyword) ||
                   ContainsIgnoreCase(Path.GetFileName(asset.AssetPath), keyword) ||
                   ContainsIgnoreCase(asset.Confidence, keyword) ||
                   ContainsIgnoreCase(asset.Reason, keyword) ||
                   ContainsIgnoreCase(asset.FileSize, keyword) ||
                   ContainsIgnoreCase(asset.LastModified, keyword);
        }

        private bool ContainsIgnoreCase(string source, string keyword)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string BuildTopExtensionSummary(List<AssetReferenceInfo> assets, int limit)
        {
            var groups = assets
                .GroupBy(a => string.IsNullOrWhiteSpace(Path.GetExtension(a.AssetPath)) ? "<无扩展>" : Path.GetExtension(a.AssetPath).ToLowerInvariant())
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(limit)
                .Select(g => $"{g.Key} {g.Count()}");

            return string.Join("  |  ", groups);
        }

        private string BuildTopDirectorySummary(List<AssetReferenceInfo> assets, int limit)
        {
            var groups = assets
                .GroupBy(a => GetDirectoryBucket(a.AssetPath))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(limit)
                .Select(g => $"{g.Key} {g.Count()}");

            return string.Join("  |  ", groups);
        }

        private string GetDirectoryBucket(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return "<无路径>";

            var parts = assetPath.Replace("\\", "/").Split('/');
            if (parts.Length <= 2)
                return assetPath;

            return $"{parts[0]}/{parts[1]}";
        }

        private long SumKnownFileSize(IEnumerable<AssetReferenceInfo> assets)
        {
            long total = 0;
            foreach (var asset in assets)
                total += GetAssetFileSize(asset);
            return total;
        }

        private long GetAssetFileSize(AssetReferenceInfo asset)
        {
            try
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.AssetPath))
                    return 0;

                var fullPath = SimpleToolsSafetyUtility.AssetPathToFullPath(asset.AssetPath);
                var fi = new FileInfo(fullPath);
                return fi.Exists ? fi.Length : 0;
            }
            catch
            {
                return 0;
            }
        }

        private DateTime GetAssetLastWriteTime(AssetReferenceInfo asset)
        {
            try
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.AssetPath))
                    return DateTime.MinValue;

                var fullPath = SimpleToolsSafetyUtility.AssetPathToFullPath(asset.AssetPath);
                var fi = new FileInfo(fullPath);
                return fi.Exists ? fi.LastWriteTime : DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:F1} {sizes[order]}";
        }

        private void RevealQuarantineFolder()
        {
            if (!SimpleToolsSafetyUtility.EnsureAssetFolder(SimpleToolsSafetyUtility.QuarantineFolder, out var error))
            {
                ShowErrorDialog($"无法打开隔离区：{error}");
                return;
            }

            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SimpleToolsSafetyUtility.QuarantineFolder);
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
            EditorUtility.RevealInFinder(SimpleToolsSafetyUtility.AssetPathToFullPath(SimpleToolsSafetyUtility.QuarantineFolder));
        }

        private void UseSelectionAsCheckFolder()
        {
            var selected = Selection.activeObject;
            if (selected == null)
                return;

            string path = SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(selected));
            if (string.IsNullOrWhiteSpace(path))
                return;

            checkFolder = AssetDatabase.IsValidFolder(path) ? path : SimpleToolsSafetyUtility.NormalizeAssetPath(Path.GetDirectoryName(path));
            packageRootFolder = checkFolder;
            lastResultSummary = "已更新扫描范围";
            lastResultDetail = $"扫描范围和资源包根目录已设为：{checkFolder}";
        }

        private void AddSelectionToPackageEntries()
        {
            foreach (var selected in Selection.objects ?? Array.Empty<UnityEngine.Object>())
            {
                if (selected == null)
                    continue;

                string path = SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(selected));
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    if (!packageUsedEntryPaths.Any(p => string.Equals(SimpleToolsSafetyUtility.NormalizeAssetPath(p), path, StringComparison.OrdinalIgnoreCase)))
                        packageUsedEntryPaths.Add(path);
                }
                else if (!packageUsedEntryAssets.Contains(selected))
                {
                    packageUsedEntryAssets.Add(selected);
                }
            }

            lastResultSummary = "已加入资源包入口";
            lastResultDetail = $"入口路径 {packageUsedEntryPaths.Count} 个，入口资产 {packageUsedEntryAssets.Count(a => a != null)} 个。";
        }

        private void ClearPackageEntries()
        {
            packageUsedEntryPaths.Clear();
            packageUsedEntryAssets.Clear();
            lastResultSummary = "资源包入口已清空";
            lastResultDetail = "资源包瘦身分析前，需要重新添加已确认会使用的入口资源。";
        }

        private string GetNextActionText()
        {
            if (!HasAnyResult())
                return "建议先执行一次分析：想清理项目就点“扫描疑似未用”；想看单个资源关系，就先选中 Project 资源再查引用或依赖；处理导入大包就先填包根目录和入口资产。";

            if (unusedAssets.Count > 0)
                return "已经有隔离候选。下一步建议先“选中候选”在 Project 里复核，确认后再“移入隔离区”或导出报告给团队确认。";

            return "已经有引用或依赖结果。可以继续换选中资源分析，也可以导出报告留档。";
        }
        #endregion

        #region 私有字段
        private int totalFilesChecked;
        private Dictionary<string, List<string>> referenceCache = new Dictionary<string, List<string>>();
        private HashSet<string> processedAssets = new HashSet<string>();
        private bool foldoutUnused = true;
        private bool foldoutReferences = true;
        private bool foldoutDependencies = true;
        private bool foldoutQuickSettings = true;
        private int pageSize = 10;
        private int currentPageUnused = 0;
        private int currentPageReferences = 0;
        private int currentPageDependencies = 0;
        private int currentPagePackageKeep = 0;
        private int currentPagePackageExternal = 0;
        private int confidenceFilterIndex = 0;
        private int resultSortIndex = 0;
        private string resultSearch = "";
        private bool lastScanCanceled = false;
        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private static readonly string[] ConfidenceFilterLabels = { "全部", "高", "中", "低", "保留", "保护", "包外" };
        private static readonly string[] ResultSortLabels = { "路径", "风险", "大小", "日期", "类型" };
        private static readonly int[] PageSizeOptions = { 10, 25, 50 };
        private static readonly string[] PageSizeLabels = { "10", "25", "50" };
        #endregion

        #region 商业级核心方法
        public void FindUnusedAssets()
        {
            if (!ValidateCheckFolder())
                return;

            if (!EditorUtility.DisplayDialog("确认操作",
                $"即将对文件夹 '{checkFolder}' 执行深度引用分析。\n\n这可能需要几分钟时间，取决于项目大小。\n\n是否继续？",
                "开始分析", "取消"))
                return;

            try
            {
                ClearResults();
                InitializeCache();

                var allAssetPaths = GetFilteredAssetPaths();
                totalFilesChecked = allAssetPaths.Count;

                lastScanCanceled = false;
                var referencedAssets = new HashSet<string>();
                var progressTitle = deepAnalysis ? "深度引用分析" : "快速引用分析";

                // 第一遍：收集所有被引用的资源
                if (!CollectReferencedAssets(allAssetPaths, referencedAssets, progressTitle))
                {
                    lastScanCanceled = true;
                    unusedAssets.Clear();
                    ShowInfoDialog("扫描已取消，结果不完整。为了安全，本次不会生成疑似未使用清单。");
                    return;
                }

                // 将场景和预制件标记为已使用（作为入口点）
                foreach (var path in allAssetPaths)
                {
                    var extension = Path.GetExtension(path).ToLower();
                    if ((extension == ".unity" && checkScenes) || (extension == ".prefab" && checkPrefabs))
                    {
                        referencedAssets.Add(path);
                    }
                }

                // 第二遍：找出疑似未使用资源
                FindUnusedAssetsFromList(allAssetPaths, referencedAssets);

                // 强制刷新UI
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

                ShowAnalysisCompleteDialog();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                lastResultSummary = "引用分析失败";
                lastResultDetail = ex.Message;
                EditorUtility.DisplayDialog("错误", $"分析过程中发生错误：{ex.Message}", "确定");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public void FindReferencesToSelected()
        {
            var selectedAsset = Selection.activeObject;
            if (selectedAsset == null)
            {
                ShowErrorDialog("请先在Project窗口中选择一个资源文件！");
                return;
            }

            selectedAssetReferences.Clear();
            var assetPath = SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(selectedAsset));
            
            if (!IsValidAssetFile(assetPath))
            {
                Debug.LogError($"选中的对象路径无效或文件不存在: {assetPath}");
                ShowErrorDialog("选中的对象不是有效的资源文件！");
                return;
            }

            ExecuteWithProgress("查找引用", "正在分析引用关系...", () =>
            {
                // 获取所有资源路径，包括可能引用目标资源的任何文件
                var allAssetPaths = AssetDatabase.GetAllAssetPaths();
                var filteredPaths = FilterAssetPathsForReferenceCheck(allAssetPaths);

                for (int i = 0; i < filteredPaths.Count; i++)
                {
                    var currentPath = filteredPaths[i];

                    // 检查用户是否取消操作
                    if (EditorUtility.DisplayCancelableProgressBar("查找引用",
                        $"检查: {Path.GetFileName(currentPath)} ({i + 1}/{filteredPaths.Count})",
                        (float)i / filteredPaths.Count))
                    {
                        ShowInfoDialog("查找已取消，当前引用结果不完整。");
                        break;
                    }

                    try
                    {
                        // 检查直接引用
                        var dependencies = AssetDatabase.GetDependencies(currentPath, false);
                        if (Array.IndexOf(dependencies, assetPath) >= 0)
                        {
                            selectedAssetReferences.Add(new AssetReferenceInfo(currentPath, false));
                        }
                        // 检查间接引用（如果启用了深度分析）
                        else if (deepAnalysis)
                        {
                            var allDeps = AssetDatabase.GetDependencies(currentPath, true);
                            if (Array.IndexOf(allDeps, assetPath) >= 0)
                            {
                                selectedAssetReferences.Add(new AssetReferenceInfo(currentPath, true));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"分析文件 {currentPath} 时出错: {ex.Message}");
                    }
                }

                RefreshUI();

                ShowCompletionDialog("引用分析完成",
                    $"找到 {selectedAssetReferences.Count} 个引用文件！\n" +
                    $"直接引用: {selectedAssetReferences.Count(r => !r.IsIndirect)}\n" +
                    $"间接引用: {selectedAssetReferences.Count(r => r.IsIndirect)}");
            });
        }

        public void FindDependenciesOfSelected()
        {
            var selectedAsset = Selection.activeObject;
            if (selectedAsset == null)
            {
                ShowErrorDialog("请先在Project窗口中选择一个资源文件！");
                return;
            }

            selectedAssetDependencies.Clear();
            var assetPath = SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(selectedAsset));

            if (!IsValidAssetFile(assetPath))
            {
                ShowErrorDialog("选中的对象不是有效的资源文件！");
                return;
            }

            ExecuteWithProgress("查找依赖", "正在分析依赖关系...", () =>
            {
                // 获取直接依赖
                var directDependencies = AssetDatabase.GetDependencies(assetPath, false);
                foreach (var dep in directDependencies)
                {
                    if (dep != assetPath) // 排除自身
                    {
                        selectedAssetDependencies.Add(new AssetReferenceInfo(dep, false));
                    }
                }

                // 如果启用了深度分析，获取所有依赖
                if (deepAnalysis)
                {
                    var allDependencies = AssetDatabase.GetDependencies(assetPath, true);
                    var indirectDeps = allDependencies.Except(directDependencies).ToArray();
                    foreach (var dep in indirectDeps)
                    {
                        if (dep != assetPath) // 排除自身
                        {
                            selectedAssetDependencies.Add(new AssetReferenceInfo(dep, true));
                        }
                    }
                }

                RefreshUI();

                ShowCompletionDialog("依赖分析完成",
                    $"找到 {selectedAssetDependencies.Count} 个依赖文件！\n" +
                    $"直接依赖: {selectedAssetDependencies.Count(r => !r.IsIndirect)}\n" +
                    $"间接依赖: {selectedAssetDependencies.Count(r => r.IsIndirect)}");
            });
        }

        public void SeparatePackageByUsedEntries()
        {
            if (!AssetDatabase.IsValidFolder(packageRootFolder))
            {
                ShowErrorDialog($"导入包根目录无效：{packageRootFolder}");
                return;
            }

            bool hasEntryPaths = packageUsedEntryPaths != null && packageUsedEntryPaths.Any(p => !string.IsNullOrWhiteSpace(p));
            bool hasEntryAssets = packageUsedEntryAssets != null && packageUsedEntryAssets.Any(a => a != null);
            if (!hasEntryPaths && !hasEntryAssets)
            {
                ShowErrorDialog("请先填写“已使用入口路径”，或拖入“已使用入口资产”。可以填 Prefab/Scene/材质/模型路径，也可以填包含入口资产的文件夹。");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认分析",
                $"将分析导入包：\n{packageRootFolder}\n\n入口路径：{packageUsedEntryPaths.Count} 个\n入口资产：{packageUsedEntryAssets.Count(a => a != null)} 个\n\n结果只生成候选清单，不会直接删除资源。",
                "开始分析", "取消"))
                return;

            try
            {
                ClearResults();
                packageKeepAssets.Clear();
                packageExternalDependencies.Clear();

                string normalizedRoot = SimpleToolsSafetyUtility.NormalizeAssetPath(packageRootFolder);
                var packageAssets = GetAssetFilesUnderFolder(normalizedRoot);
                var packageAssetSet = new HashSet<string>(packageAssets, StringComparer.OrdinalIgnoreCase);
                var entryAssets = CollectEntryAssetFiles(packageUsedEntryPaths, packageUsedEntryAssets);

                if (entryAssets.Count == 0)
                {
                    ShowErrorDialog("入口路径没有收集到有效资源。请确认路径在 Assets 下，且不是空目录。");
                    return;
                }

                totalFilesChecked = packageAssets.Count;
                var keepSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var externalSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                lastScanCanceled = false;

                for (int i = 0; i < entryAssets.Count; i++)
                {
                    string entry = entryAssets[i];
                    if (EditorUtility.DisplayCancelableProgressBar("资源包分离分析",
                        $"分析入口: {Path.GetFileName(entry)} ({i + 1}/{entryAssets.Count})",
                        entryAssets.Count == 0 ? 1f : (float)i / entryAssets.Count))
                    {
                        lastScanCanceled = true;
                        unusedAssets.Clear();
                        packageKeepAssets.Clear();
                        packageExternalDependencies.Clear();
                        ShowInfoDialog("分析已取消，结果不完整。本次不会生成隔离候选。");
                        return;
                    }

                    if (packageProtectEntryAssets)
                        AddDependencyToPackageSets(entry, packageAssetSet, keepSet, externalSet);

                    foreach (var dep in AssetDatabase.GetDependencies(entry, true))
                        AddDependencyToPackageSets(SimpleToolsSafetyUtility.NormalizeAssetPath(dep), packageAssetSet, keepSet, externalSet);
                }

                foreach (var keep in keepSet.OrderBy(p => p))
                    packageKeepAssets.Add(new AssetReferenceInfo(keep, false, "保留", "入口资产或入口依赖"));

                foreach (var external in externalSet.OrderBy(p => p))
                    packageExternalDependencies.Add(new AssetReferenceInfo(external, false, "包外", "入口依赖到包外资源"));

                foreach (var assetPath in packageAssets)
                {
                    if (keepSet.Contains(assetPath))
                        continue;

                    if (ShouldProtectAssetFromPackageSeparation(assetPath, out var protectReason))
                    {
                        packageKeepAssets.Add(new AssetReferenceInfo(assetPath, false, "保护", protectReason));
                        continue;
                    }

                    if (TryBuildUnusedCandidate(assetPath, out var info))
                    {
                        info.Reason = "包内未被已使用入口依赖";
                        unusedAssets.Add(info);
                    }
                }

                unusedAssets = unusedAssets.OrderByDescending(GetConfidenceSort).ThenBy(a => a.AssetPath).ToList();
                packageKeepAssets = packageKeepAssets.OrderBy(a => a.AssetPath).ToList();
                packageExternalDependencies = packageExternalDependencies.OrderBy(a => a.AssetPath).ToList();

                RefreshUI();
                ShowCompletionDialog("资源包分离完成",
                    $"包内资源: {packageAssets.Count}\n" +
                    $"入口资源: {entryAssets.Count}\n" +
                    $"包内保留: {packageKeepAssets.Count}\n" +
                    $"包外依赖: {packageExternalDependencies.Count}\n" +
                    $"包内可隔离候选: {unusedAssets.Count}\n\n" +
                    "建议先导出报告或选中候选复核，再使用移入隔离区。");
            }
            catch (Exception ex)
            {
                lastResultSummary = "资源包分离失败";
                lastResultDetail = ex.Message;
                ShowErrorDialog($"资源包分离失败：{ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public void SelectUnusedAssets()
        {
            if (unusedAssets.Count == 0)
            {
                ShowInfoDialog("没有疑似未使用资源可以选中！");
                return;
            }

            var objects = LoadUnusedAssetObjects(out var failedPaths);

            Selection.objects = objects;
            if (objects.Length > 0)
            {
                EditorGUIUtility.PingObject(objects[0]);
                string detail = failedPaths.Count > 0 ? "\n\n未能加载：\n" + SimpleToolsSafetyUtility.JoinPreview(failedPaths, 8) : string.Empty;
                ShowCompletionDialog("操作完成", $"已选中 {objects.Length} / {unusedAssets.Count} 个疑似未使用资源。{detail}");
            }
            else
            {
                ShowErrorDialog("没有能加载到 Project 窗口的资源对象。\n\n" + SimpleToolsSafetyUtility.JoinPreview(failedPaths, 8));
            }
        }

        public void QuarantineUnusedAssets()
        {
            if (unusedAssets.Count == 0)
            {
                ShowInfoDialog("没有疑似未使用资源可以隔离。");
                return;
            }

            if (lastScanCanceled)
            {
                ShowInfoDialog("上次扫描已取消，结果不完整。请重新完整扫描后再隔离资源。");
                return;
            }

            int lowConfidenceCount = unusedAssets.Count(a => a.Confidence == "低");
            string preview = SimpleToolsSafetyUtility.JoinPreview(unusedAssets.Select(a => $"{a.AssetPath} | {a.Confidence} | {a.Reason}"), 10);
            if (!EditorUtility.DisplayDialog("确认移入隔离区",
                $"将把 {unusedAssets.Count} 个疑似未使用资源移动到：\n{SimpleToolsSafetyUtility.QuarantineFolder}\n\n低置信度 {lowConfidenceCount} 个，建议人工复核。\n\n{preview}\n\n这不是永久删除，但会改变资源路径，可能影响引用。建议先提交或备份项目。\n\n继续吗？",
                "移入隔离区", "取消"))
                return;

            ExecuteWithProgress("隔离资源", "正在移动疑似未使用资源...", () =>
            {
                int movedCount = 0;
                int failedCount = 0;
                var failedMessages = new List<string>();
                var movedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var quarantineRecords = unusedAssets.ToList();
                var quarantineMoveMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                SimpleToolsSafetyUtility.RunAssetEditing(() =>
                {
                    foreach (var assetInfo in unusedAssets)
                    {
                        if (SimpleToolsSafetyUtility.MoveAssetToQuarantine(assetInfo.AssetPath, out var newPath, out var error))
                        {
                            movedCount++;
                            movedAssets.Add(assetInfo.AssetPath);
                            quarantineMoveMap[assetInfo.AssetPath] = newPath;
                        }
                        else
                        {
                            failedCount++;
                            failedMessages.Add($"{assetInfo.AssetPath}: {error}");
                        }
                    }
                });

                WriteQuarantineManifest(quarantineRecords, quarantineMoveMap, movedCount, failedCount, failedMessages);
                unusedAssets.RemoveAll(asset => movedAssets.Contains(asset.AssetPath));
                string detail = failedMessages.Count > 0 ? "\n\n失败项：\n" + SimpleToolsSafetyUtility.JoinPreview(failedMessages, 8) : string.Empty;
                ShowCompletionDialog("隔离完成", $"已移动 {movedCount} 个资源到隔离区，失败 {failedCount} 个。\n当前列表保留 {unusedAssets.Count} 个未移动资源，方便继续复核。{detail}");
            });
        }

        public void ExportAnalysisReport()
        {
            var reportPath = EditorUtility.SaveFilePanel("导出分析报告",
                Application.dataPath, "AssetAnalysisReport.txt", "txt");

            if (string.IsNullOrEmpty(reportPath))
                return;

            try
            {
                using (var writer = new StreamWriter(reportPath, false, Encoding.UTF8))
                {
                    writer.WriteLine("=== 资产引用分析报告 ===");
                    writer.WriteLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"检查文件夹: {checkFolder}");
                    writer.WriteLine($"深度分析: {(deepAnalysis ? "启用" : "禁用")}");
                    writer.WriteLine();

                    if (unusedAssets.Count > 0)
                    {
                        writer.WriteLine("=== 风险总览 ===");
                        writer.WriteLine($"疑似未使用: {unusedAssets.Count}");
                        writer.WriteLine($"高置信: {unusedAssets.Count(a => a.Confidence == "高")}");
                        writer.WriteLine($"中置信: {unusedAssets.Count(a => a.Confidence == "中")}");
                        writer.WriteLine($"低置信: {unusedAssets.Count(a => a.Confidence == "低")}");
                        writer.WriteLine($"体积估算: {FormatFileSize(SumKnownFileSize(unusedAssets))}");
                        writer.WriteLine($"类型分布: {BuildTopExtensionSummary(unusedAssets, 12)}");
                        writer.WriteLine($"目录分布: {BuildTopDirectorySummary(unusedAssets, 12)}");
                        writer.WriteLine();
                    }

                    if (unusedAssets.Count > 0)
                    {
                        writer.WriteLine($"=== 疑似未使用资源 ({unusedAssets.Count} 个) ===");
                        foreach (var asset in unusedAssets)
                        {
                            writer.WriteLine($"{asset.FileSize}\t{asset.LastModified}\t{asset.Confidence}\t{asset.Reason}\t{asset.AssetPath}");
                        }
                        writer.WriteLine();
                    }

                    if (selectedAssetReferences.Count > 0)
                    {
                        writer.WriteLine($"=== 引用分析 ({selectedAssetReferences.Count} 个) ===");
                        foreach (var reference in selectedAssetReferences)
                        {
                            writer.WriteLine($"{(reference.IsIndirect ? "[间接]" : "[直接]")}\t{reference.FileSize}\t{reference.LastModified}\t{reference.AssetPath}");
                        }
                        writer.WriteLine();
                    }

                    if (selectedAssetDependencies.Count > 0)
                    {
                        writer.WriteLine($"=== 依赖分析 ({selectedAssetDependencies.Count} 个) ===");
                        foreach (var dependency in selectedAssetDependencies)
                        {
                            writer.WriteLine($"{(dependency.IsIndirect ? "[间接]" : "[直接]")}\t{dependency.FileSize}\t{dependency.LastModified}\t{dependency.AssetPath}");
                        }
                        writer.WriteLine();
                    }

                    if (packageKeepAssets.Count > 0 || packageExternalDependencies.Count > 0)
                    {
                        writer.WriteLine($"=== 资源包分离：包内保留 ({packageKeepAssets.Count} 个) ===");
                        foreach (var asset in packageKeepAssets)
                        {
                            writer.WriteLine($"{asset.FileSize}\t{asset.LastModified}\t{asset.Confidence}\t{asset.Reason}\t{asset.AssetPath}");
                        }
                        writer.WriteLine();

                        writer.WriteLine($"=== 资源包分离：包外依赖 ({packageExternalDependencies.Count} 个) ===");
                        foreach (var asset in packageExternalDependencies)
                        {
                            writer.WriteLine($"{asset.FileSize}\t{asset.LastModified}\t{asset.Confidence}\t{asset.Reason}\t{asset.AssetPath}");
                        }
                    }
                }

                EditorUtility.RevealInFinder(reportPath);
                ShowCompletionDialog("导出完成", $"分析报告已导出到：\n{reportPath}");
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"导出报告时发生错误：{ex.Message}");
            }
        }

        public void ClearResults()
        {
            unusedAssets.Clear();
            selectedAssetReferences.Clear();
            selectedAssetDependencies.Clear();
            packageKeepAssets.Clear();
            packageExternalDependencies.Clear();
            referenceCache.Clear();
            processedAssets.Clear();
            totalFilesChecked = 0;
            currentPageUnused = 0;
            currentPageReferences = 0;
            currentPageDependencies = 0;
            currentPagePackageKeep = 0;
            currentPagePackageExternal = 0;
            lastResultSummary = "分析结果已清除";
            lastResultDetail = "疑似未使用、引用分析、依赖分析、资源包分离和缓存结果已清空。";
        }

        public void JumpToAllUnusedAssets()
        {
            if (unusedAssets.Count == 0)
            {
                lastResultSummary = "跳转取消: 没有疑似未使用资源";
                lastResultDetail = "请先执行扫描并复核结果。";
                EditorUtility.DisplayDialog("提示", "没有疑似未使用资源可以跳转！", "确定");
                return;
            }

            var objects = LoadUnusedAssetObjects(out var failedPaths);

            if (objects.Length > 0)
            {
                Selection.objects = objects;
                EditorGUIUtility.PingObject(objects[0]);
                string detail = failedPaths.Count > 0 ? "\n\n未能加载：\n" + SimpleToolsSafetyUtility.JoinPreview(failedPaths, 8) : string.Empty;
                lastResultSummary = $"已选中疑似未使用资源: {objects.Length} / {unusedAssets.Count}";
                lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(unusedAssets.Select(asset => asset.AssetPath), 12) + detail;
                EditorUtility.DisplayDialog("完成", $"已选中 {objects.Length} / {unusedAssets.Count} 个疑似未使用资源。\n\n在 Project 窗口中查看选中的资源。{detail}", "确定");
            }
            else
            {
                lastResultSummary = "跳转失败: 无法加载资源对象";
                lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(failedPaths, 12);
                EditorUtility.DisplayDialog("错误", "无法加载任何资源对象。\n\n" + SimpleToolsSafetyUtility.JoinPreview(failedPaths, 8), "确定");
            }
        }

        public void RefreshCache()
        {
            referenceCache.Clear();
            processedAssets.Clear();
            AssetDatabase.Refresh();
            ShowCompletionDialog("操作完成", "缓存已刷新！");
        }
        #endregion

        #region 辅助方法
        private bool ValidateCheckFolder()
        {
            if (!AssetDatabase.IsValidFolder(checkFolder))
            {
                ShowErrorDialog($"文件夹 '{checkFolder}' 不存在或无效！");
                return false;
            }
            return true;
        }

        private UnityEngine.Object[] LoadUnusedAssetObjects(out List<string> failedPaths)
        {
            var objects = new List<UnityEngine.Object>();
            failedPaths = new List<string>();

            foreach (var info in unusedAssets)
            {
                if (info == null || string.IsNullOrWhiteSpace(info.AssetPath))
                {
                    failedPaths.Add("<空路径>");
                    continue;
                }

                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(info.AssetPath);
                if (obj != null)
                {
                    objects.Add(obj);
                }
                else
                {
                    failedPaths.Add(info.AssetPath);
                }
            }

            return objects.ToArray();
        }

        private void InitializeCache()
        {
            if (useCache)
            {
                referenceCache.Clear();
                processedAssets.Clear();
            }
        }

        private List<string> GetFilteredAssetPaths()
        {
            var allPaths = AssetDatabase.GetAllAssetPaths();
            return FilterAssetPaths(allPaths);
        }

        private List<string> FilterAssetPaths(string[] allPaths)
        {
            var filteredPaths = new List<string>();
            string normalizedCheckFolder = SimpleToolsSafetyUtility.NormalizeAssetPath(checkFolder);

            foreach (var path in allPaths)
            {
                string normalizedPath = SimpleToolsSafetyUtility.NormalizeAssetPath(path);
                // 检查是否在检查范围内
                if (!IsPathInsideFolder(normalizedPath, normalizedCheckFolder))
                    continue;

                // 检查是否在排除文件夹中
                if (excludeFolders.Any(exclude => IsPathInsideFolder(normalizedPath, SimpleToolsSafetyUtility.NormalizeAssetPath(exclude))))
                    continue;

                // 检查文件类型过滤
                var extension = Path.GetExtension(normalizedPath).ToLower();
                if (excludeExtensions.Contains(extension))
                    continue;

                if (includeExtensions.Count > 0 && !includeExtensions.Contains(extension))
                    continue;

                // 跳过文件夹
                if (AssetDatabase.IsValidFolder(normalizedPath))
                    continue;

                filteredPaths.Add(normalizedPath);
            }

            return filteredPaths;
        }

        private List<string> FilterAssetPathsForReferenceCheck(string[] allPaths)
        {
            var filteredPaths = new List<string>();
            string normalizedCheckFolder = SimpleToolsSafetyUtility.NormalizeAssetPath(checkFolder);

            foreach (var path in allPaths)
            {
                string normalizedPath = SimpleToolsSafetyUtility.NormalizeAssetPath(path);
                // 检查是否在检查范围内
                if (!IsPathInsideFolder(normalizedPath, normalizedCheckFolder))
                    continue;

                // 检查是否在排除文件夹中
                if (excludeFolders.Any(exclude => IsPathInsideFolder(normalizedPath, SimpleToolsSafetyUtility.NormalizeAssetPath(exclude))))
                    continue;

                // 对于引用检查，我们需要包含更多文件类型，因为任何文件都可能引用资源
                // 只排除.meta文件和文件夹
                var extension = Path.GetExtension(normalizedPath).ToLower();
                if (extension == ".meta")
                    continue;

                // 跳过文件夹
                if (AssetDatabase.IsValidFolder(normalizedPath))
                    continue;

                filteredPaths.Add(normalizedPath);
            }

            return filteredPaths;
        }

        private bool IsPathInsideFolder(string assetPath, string folderPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(folderPath))
                return false;

            assetPath = SimpleToolsSafetyUtility.NormalizeAssetPath(assetPath);
            folderPath = SimpleToolsSafetyUtility.NormalizeAssetPath(folderPath);
            return string.Equals(assetPath, folderPath, StringComparison.OrdinalIgnoreCase) ||
                   assetPath.StartsWith(folderPath + "/", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsValidAssetFile(string assetPath)
        {
            if (!SimpleToolsSafetyUtility.IsAssetPath(assetPath) || AssetDatabase.IsValidFolder(assetPath))
                return false;

            try
            {
                return File.Exists(SimpleToolsSafetyUtility.AssetPathToFullPath(assetPath));
            }
            catch
            {
                return false;
            }
        }

        private List<string> GetAssetFilesUnderFolder(string folderPath)
        {
            folderPath = SimpleToolsSafetyUtility.NormalizeAssetPath(folderPath);
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folderPath });
            var result = new List<string>(guids.Length);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var guid in guids)
            {
                string path = SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (!IsValidAssetFile(path))
                    continue;

                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (excludeExtensions.Contains(extension))
                    continue;

                if (includeExtensions.Count > 0 && !includeExtensions.Contains(extension))
                    continue;

                if (seen.Add(path))
                    result.Add(path);
            }

            return result.OrderBy(p => p).ToList();
        }

        private List<string> CollectEntryAssetFiles(IEnumerable<string> entryPaths, IEnumerable<UnityEngine.Object> entryObjects)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entryObject in entryObjects ?? Enumerable.Empty<UnityEngine.Object>())
            {
                if (entryObject == null)
                    continue;

                AddEntryPath(SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(entryObject)));
            }

            foreach (var rawPath in entryPaths ?? Enumerable.Empty<string>())
            {
                AddEntryPath(SimpleToolsSafetyUtility.NormalizeAssetPath(rawPath));
            }

            return result.OrderBy(p => p).ToList();

            void AddEntryPath(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var asset in GetAssetFilesUnderFolder(path))
                    {
                        if (seen.Add(asset))
                            result.Add(asset);
                    }
                }
                else if (IsValidAssetFile(path))
                {
                    if (seen.Add(path))
                        result.Add(path);
                }
            }
        }

        private void AddDependencyToPackageSets(string assetPath, HashSet<string> packageAssetSet, HashSet<string> keepSet, HashSet<string> externalSet)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            assetPath = SimpleToolsSafetyUtility.NormalizeAssetPath(assetPath);
            if (!IsValidAssetFile(assetPath))
                return;

            if (packageAssetSet.Contains(assetPath))
            {
                keepSet.Add(assetPath);
            }
            else
            {
                externalSet.Add(assetPath);
            }
        }

        private int GetConfidenceSort(AssetReferenceInfo info)
        {
            switch (info?.Confidence)
            {
                case "高": return 3;
                case "中": return 2;
                case "低": return 1;
                default: return 0;
            }
        }

        private bool ShouldProtectAssetFromPackageSeparation(string assetPath, out string reason)
        {
            reason = null;
            if (!protectEntryAssets)
                return false;

            string normalized = assetPath.Replace("\\", "/");
            string extension = Path.GetExtension(normalized).ToLowerInvariant();

            if (protectedFolders.Any(folder => IsPathInsideFolder(normalized, SimpleToolsSafetyUtility.NormalizeAssetPath(folder))))
            {
                reason = "位于保护路径";
                return true;
            }

            if (normalized.Contains("/Resources/", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("/Resources", StringComparison.OrdinalIgnoreCase))
            {
                reason = "Resources运行时入口";
                return true;
            }

            if (normalized.Contains("/StreamingAssets/", StringComparison.OrdinalIgnoreCase))
            {
                reason = "StreamingAssets运行时入口";
                return true;
            }

            if (normalized.Contains("/Editor Default Resources/", StringComparison.OrdinalIgnoreCase))
            {
                reason = "Editor Default Resources入口";
                return true;
            }

            switch (extension)
            {
                case ".cs":
                case ".asmdef":
                case ".dll":
                    reason = "代码或程序集入口";
                    return true;
                default:
                    return false;
            }
        }

        private bool CollectReferencedAssets(List<string> allAssetPaths, HashSet<string> referencedAssets, string progressTitle)
        {
            int totalSteps = allAssetPaths.Count;
            bool shouldCheckScenes = checkScenes;
            bool shouldCheckPrefabs = checkPrefabs;
            bool shouldCheckScripts = checkScripts;

            for (int i = 0; i < totalSteps; i++)
            {
                var assetPath = allAssetPaths[i];

                if (EditorUtility.DisplayCancelableProgressBar(progressTitle,
                    $"分析引用: {Path.GetFileName(assetPath)} ({i + 1}/{totalSteps})",
                    (float)i / totalSteps))
                {
                    return false;
                }

                bool shouldAnalyze = false;
                var extension = Path.GetExtension(assetPath).ToLower();

                if (extension == ".unity" && shouldCheckScenes) shouldAnalyze = true;
                else if (extension == ".prefab" && shouldCheckPrefabs) shouldAnalyze = true;
                else if (deepAnalysis && IsDependencyCarrierExtension(extension)) shouldAnalyze = true;
                else if (extension == ".cs" && shouldCheckScripts) shouldAnalyze = true;

                if (shouldAnalyze)
                {
                    try
                    {
                        var dependencies = AssetDatabase.GetDependencies(assetPath, deepAnalysis);

                        foreach (var dep in dependencies)
                        {
                            referencedAssets.Add(dep);
                        }

                        // Mark entry points as referenced
                        referencedAssets.Add(assetPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"分析文件 {assetPath} 时出错: {ex.Message}");
                    }
                }
            }

            return true;
        }

        private bool IsDependencyCarrierExtension(string extension)
        {
            switch (extension)
            {
                case ".asset":
                case ".mat":
                case ".controller":
                case ".overridecontroller":
                case ".anim":
                case ".playable":
                case ".timeline":
                case ".rendertexture":
                case ".shadergraph":
                case ".prefab":
                case ".unity":
                    return true;
                default:
                    return false;
            }
        }

        private void FindUnusedAssetsFromList(List<string> allAssetPaths, HashSet<string> referencedAssets)
        {
            EditorUtility.DisplayProgressBar("查找疑似未使用资源", "正在筛选...", 0f);

            foreach (var assetPath in allAssetPaths)
            {
                if (referencedAssets.Contains(assetPath))
                    continue;

                if (ShouldProtectAssetFromUnusedList(assetPath, out _))
                    continue;

                if (TryBuildUnusedCandidate(assetPath, out var info))
                {
                    unusedAssets.Add(info);
                }
            }
        }

        private bool ShouldProtectAssetFromUnusedList(string assetPath, out string reason)
        {
            reason = null;
            if (!protectEntryAssets)
                return false;

            string normalized = assetPath.Replace("\\", "/");
            string extension = Path.GetExtension(normalized).ToLowerInvariant();

            if (protectedFolders.Any(folder => IsPathInsideFolder(normalized, SimpleToolsSafetyUtility.NormalizeAssetPath(folder))))
            {
                reason = "位于保护路径";
                return true;
            }

            if (normalized.Contains("/Resources/", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("/Resources", StringComparison.OrdinalIgnoreCase))
            {
                reason = "Resources运行时入口";
                return true;
            }

            if (normalized.Contains("/StreamingAssets/", StringComparison.OrdinalIgnoreCase))
            {
                reason = "StreamingAssets运行时入口";
                return true;
            }

            if (normalized.Contains("/Editor Default Resources/", StringComparison.OrdinalIgnoreCase))
            {
                reason = "Editor Default Resources入口";
                return true;
            }

            if (normalized.Contains("AddressableAssetsData", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("/Addressables/", StringComparison.OrdinalIgnoreCase))
            {
                reason = "Addressables配置或分组";
                return true;
            }

            switch (extension)
            {
                case ".unity":
                    reason = "场景入口";
                    return true;
                case ".prefab":
                    reason = "Prefab可能由代码或Addressables加载";
                    return true;
                case ".cs":
                case ".asmdef":
                case ".dll":
                    reason = "代码或程序集入口";
                    return true;
                case ".asset":
                    if (normalized.Contains("/GlobalData/", StringComparison.OrdinalIgnoreCase) ||
                        normalized.Contains("/Data/", StringComparison.OrdinalIgnoreCase) ||
                        normalized.Contains("/Settings/", StringComparison.OrdinalIgnoreCase) ||
                        normalized.Contains("/Config", StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "配置资产路径";
                        return true;
                    }
                    break;
            }

            return false;
        }

        private bool TryBuildUnusedCandidate(string assetPath, out AssetReferenceInfo info)
        {
            info = null;
            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            string confidence = "中";
            string reason = "未发现显式依赖";

            switch (extension)
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".psd":
                case ".exr":
                case ".wav":
                case ".mp3":
                case ".ogg":
                case ".fbx":
                case ".obj":
                    confidence = "高";
                    reason = "内容资源未被依赖图引用";
                    break;
                case ".mat":
                case ".asset":
                case ".controller":
                case ".overridecontroller":
                case ".anim":
                case ".playable":
                    confidence = "低";
                    reason = "配置/控制类资源可能被代码或运行时加载";
                    break;
                default:
                    confidence = "中";
                    reason = "未发现显式依赖，仍需人工确认";
                    break;
            }

            info = new AssetReferenceInfo(assetPath, false, confidence, reason);
            return true;
        }

        private void WriteQuarantineManifest(List<AssetReferenceInfo> records, Dictionary<string, string> moveMap, int movedCount, int failedCount, List<string> failedMessages)
        {
            try
            {
                if (!SimpleToolsSafetyUtility.EnsureAssetFolder(SimpleToolsSafetyUtility.QuarantineFolder, out var error))
                {
                    Debug.LogWarning($"[资源引用检查] 无法创建隔离清单目录: {error}");
                    return;
                }

                string manifestAssetPath = $"{SimpleToolsSafetyUtility.QuarantineFolder}/AssetReferenceQuarantine_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string fullPath = SimpleToolsSafetyUtility.AssetPathToFullPath(manifestAssetPath);
                var lines = new List<string>
                {
                    "=== ES 资源引用检查隔离清单 ===",
                    $"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"检查范围: {checkFolder}",
                    $"移动成功: {movedCount}",
                    $"移动失败: {failedCount}",
                    "",
                    "文件大小\t修改日期\t置信度\t原因\t原路径\t隔离后路径"
                };

                foreach (var record in records)
                {
                    string movedPath = "";
                    moveMap?.TryGetValue(record.AssetPath, out movedPath);
                    lines.Add($"{record.FileSize}\t{record.LastModified}\t{record.Confidence}\t{record.Reason}\t{record.AssetPath}\t{movedPath}");
                }

                if (failedMessages != null && failedMessages.Count > 0)
                {
                    lines.Add("");
                    lines.Add("=== 失败项 ===");
                    lines.AddRange(failedMessages);
                }

                File.WriteAllLines(fullPath, lines, Encoding.UTF8);
                AssetDatabase.ImportAsset(manifestAssetPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[资源引用检查] 写入隔离清单失败: {ex.Message}");
            }
        }

        private void ShowAnalysisCompleteDialog()
        {
            string message = $"深度引用分析完成！\n\n" +
                           $"总检查文件: {totalFilesChecked}\n" +
                           $"疑似未使用资源: {unusedAssets.Count}\n" +
                           $"高置信度: {unusedAssets.Count(a => a.Confidence == "高")} | 中: {unusedAssets.Count(a => a.Confidence == "中")} | 低: {unusedAssets.Count(a => a.Confidence == "低")}\n" +
                           $"结果状态: {(lastScanCanceled ? "不完整" : "完整")}";

            if (unusedAssets.Count > 0)
            {
                message += "\n\n建议先选中检查结果。需要清理时，使用“移入隔离区”，不要直接删。";
            }

            EditorUtility.DisplayDialog("分析完成", message, "确定");
            lastResultSummary = $"引用分析完成: 检查 {totalFilesChecked} 个 | 疑似未使用 {unusedAssets.Count} 个 | 状态 {(lastScanCanceled ? "不完整" : "完整")}";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(unusedAssets.Select(asset => $"{asset.AssetPath} | {asset.Confidence} | {asset.Reason}"), 12);
        }

        #region 统一UI辅助方法
        private void ShowErrorDialog(string message)
        {
            lastResultSummary = "操作失败";
            lastResultDetail = message;
            EditorUtility.DisplayDialog("错误", message, "确定");
        }

        private void ShowInfoDialog(string message)
        {
            lastResultSummary = "操作提示";
            lastResultDetail = message;
            EditorUtility.DisplayDialog("提示", message, "确定");
        }

        private void ShowCompletionDialog(string title, string message)
        {
            lastResultSummary = title;
            lastResultDetail = message;
            EditorUtility.DisplayDialog(title, message, "确定");
        }

        private void ExecuteWithProgress(string title, string initialMessage, Action action)
        {
            try
            {
                EditorUtility.DisplayProgressBar(title, initialMessage, 0f);
                action();
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"{title}过程中发生错误：{ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void UpdateProgress(string message, float progress)
        {
            EditorUtility.DisplayProgressBar("处理中", message, progress);
        }

        private void RefreshUI()
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        #endregion

        #endregion
    }
    #endregion
}

// 恢复警告
#pragma warning restore CS0414
#pragma warning restore CS0162
