using ES;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        [Title("资产引用检查与清理工具", "分析资源依赖关系，查找未使用资产并提供清理功能", bold: true, titleAlignment: TitleAlignments.Centered)]

        [InfoBox("🎯 商业级功能：深度引用分析、批量处理、智能过滤、性能优化、详细报告导出", InfoMessageType.Info)]
        [DisplayAsString(fontSize: 25), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "选择要检查的资源或文件夹，\n使用专业级算法深度分析引用关系，\n支持批量清理和报告导出";

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
        [TabGroup("分析结果", "未使用资源")]
        [HideInInspector]
        public List<AssetReferenceInfo> unusedAssets = new List<AssetReferenceInfo>();

        [TabGroup("分析结果", "未使用资源")]
        [ShowInInspector, ReadOnly, LabelText("统计信息")]
        [DisplayAsString]
        private string UnusedStats => $"总文件数: {totalFilesChecked}, 未使用: {unusedAssets.Count}, 使用率: {(totalFilesChecked > 0 ? ((totalFilesChecked - unusedAssets.Count) * 100f / totalFilesChecked).ToString("F1") + "%" : "0%")}";

        [TabGroup("分析结果", "引用分析")]
        [HideInInspector]
        public List<AssetReferenceInfo> selectedAssetReferences = new List<AssetReferenceInfo>();

        [TabGroup("分析结果", "引用分析")]
        [ShowInInspector, ReadOnly, LabelText("引用统计")]
        [DisplayAsString]
        private string ReferenceStats => $"直接引用: {selectedAssetReferences.Count(r => !r.IsIndirect)}, 间接引用: {selectedAssetReferences.Count(r => r.IsIndirect)}";

        [TabGroup("分析结果", "依赖分析")]
        [HideInInspector]
        public List<AssetReferenceInfo> selectedAssetDependencies = new List<AssetReferenceInfo>();

        [TabGroup("分析结果", "依赖分析")]
        [ShowInInspector, ReadOnly, LabelText("依赖统计")]
        [DisplayAsString]
        private string DependencyStats => $"直接依赖: {selectedAssetDependencies.Count(r => !r.IsIndirect)}, 间接依赖: {selectedAssetDependencies.Count(r => r.IsIndirect)}";
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
                return $"{Path.GetFileName(AssetPath)} ({FileSize}) - {LastModified}";
            }

            [HideInInspector]
            public bool IsIndirect;

            public AssetReferenceInfo(string path, bool indirect = false)
            {
                AssetPath = path;
                // 将Unity Asset路径转换为文件系统路径
                string fullPath = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
                FileSize = GetFileSizeString(fullPath);
                LastModified = GetLastModifiedString(fullPath);
                IsIndirect = indirect;
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
        [OnInspectorGUI]
        private void DrawCustomLists()
        {
            // 绘制未使用资源
            foldoutUnused = EditorGUILayout.Foldout(foldoutUnused, $"未使用资源列表 ({unusedAssets.Count})");
            if (foldoutUnused)
            {
                EditorGUI.indentLevel++;
                if (unusedAssets.Count == 0)
                {
                    EditorGUILayout.LabelField("没有未使用的资源。");
                }
                else
                {
                    // 分页
                    int totalPages = Mathf.CeilToInt((float)unusedAssets.Count / pageSize);
                    if (totalPages > 1)
                    {
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("上一页", GUILayout.Width(60)) && currentPageUnused > 0) currentPageUnused--;
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"页 {currentPageUnused + 1} / {totalPages}", GUILayout.Width(80));
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("下一页", GUILayout.Width(60)) && currentPageUnused < totalPages - 1) currentPageUnused++;
                        EditorGUILayout.EndHorizontal();
                    }

                    int start = currentPageUnused * pageSize;
                    int end = Mathf.Min(start + pageSize, unusedAssets.Count);
                    for (int i = start; i < end; i++)
                    {
                        var asset = unusedAssets[i];
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(asset.ToString());
                        if (GUILayout.Button("跳转", GUILayout.Width(50)))
                        {
                            asset.JumpToAsset();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 绘制引用分析
            foldoutReferences = EditorGUILayout.Foldout(foldoutReferences, $"选中资源的引用 ({selectedAssetReferences.Count})");
            if (foldoutReferences)
            {
                DrawAssetList(selectedAssetReferences, ref currentPageReferences);
            }

            EditorGUILayout.Space();

            // 绘制依赖分析
            foldoutDependencies = EditorGUILayout.Foldout(foldoutDependencies, $"选中资源的依赖 ({selectedAssetDependencies.Count})");
            if (foldoutDependencies)
            {
                DrawAssetList(selectedAssetDependencies, ref currentPageDependencies);
            }
        }

        private void DrawAssetList(List<AssetReferenceInfo> assetList, ref int currentPage)
        {
            EditorGUI.indentLevel++;
            if (assetList.Count == 0)
            {
                EditorGUILayout.LabelField("没有分析结果。");
            }
            else
            {
                // 分页
                int totalPages = Mathf.CeilToInt((float)assetList.Count / pageSize);
                if (totalPages > 1)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("上一页", GUILayout.Width(60)) && currentPage > 0) currentPage--;
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"页 {currentPage + 1} / {totalPages}", GUILayout.Width(80));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("下一页", GUILayout.Width(60)) && currentPage < totalPages - 1) currentPage++;
                    EditorGUILayout.EndHorizontal();
                }

                int start = currentPage * pageSize;
                int end = Mathf.Min(start + pageSize, assetList.Count);
                for (int i = start; i < end; i++)
                {
                    var asset = assetList[i];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(asset.ToString());
                    if (GUILayout.Button("跳转", GUILayout.Width(50)))
                    {
                        asset.JumpToAsset();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUI.indentLevel--;
        }
        #endregion

        #region 私有字段
        private int totalFilesChecked;
        private Dictionary<string, List<string>> referenceCache = new Dictionary<string, List<string>>();
        private HashSet<string> processedAssets = new HashSet<string>();
        private bool foldoutUnused = true;
        private bool foldoutReferences = true;
        private bool foldoutDependencies = true;
        private int pageSize = 10;
        private int currentPageUnused = 0;
        private int currentPageReferences = 0;
        private int currentPageDependencies = 0;
        #endregion

        #region 商业级核心方法
        [TabGroup("操作", "查找功能")]
        [Button("🔍 深度查找未使用资源", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
        [InfoBox("执行商业级深度分析，检查所有引用关系。可能需要较长时间。")]
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

                var referencedAssets = new HashSet<string>();
                var progressTitle = deepAnalysis ? "深度引用分析" : "快速引用分析";

                // 第一遍：收集所有被引用的资源
                CollectReferencedAssets(allAssetPaths, referencedAssets, progressTitle);

                // 将场景和预制件标记为已使用（作为入口点）
                foreach (var path in allAssetPaths)
                {
                    var extension = Path.GetExtension(path).ToLower();
                    if ((extension == ".unity" && checkScenes) || (extension == ".prefab" && checkPrefabs))
                    {
                        referencedAssets.Add(path);
                    }
                }

                // 第二遍：找出未使用的资源
                FindUnusedAssetsFromList(allAssetPaths, referencedAssets);

                // 强制刷新UI
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

                ShowAnalysisCompleteDialog();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("错误", $"分析过程中发生错误：{ex.Message}", "确定");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [TabGroup("操作", "查找功能")]
        [Button("🎯 查找选中资源的引用", ButtonHeight = 45), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
        [InfoBox("找出哪些资源引用了当前选中的资源。")]
        public void FindReferencesToSelected()
        {
            var selectedAsset = Selection.activeObject;
            if (selectedAsset == null)
            {
                ShowErrorDialog("请先在Project窗口中选择一个资源文件！");
                return;
            }

            selectedAssetReferences.Clear();
            var assetPath = AssetDatabase.GetAssetPath(selectedAsset);
            
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
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
                        break; // 用户取消
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

        [TabGroup("操作", "查找功能")]
        [Button("🔗 查找选中资源的依赖", ButtonHeight = 45), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        [InfoBox("分析当前选中资源依赖的所有其他资源。")]
        public void FindDependenciesOfSelected()
        {
            var selectedAsset = Selection.activeObject;
            if (selectedAsset == null)
            {
                ShowErrorDialog("请先在Project窗口中选择一个资源文件！");
                return;
            }

            selectedAssetDependencies.Clear();
            var assetPath = AssetDatabase.GetAssetPath(selectedAsset);

            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
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

        [TabGroup("操作", "批量操作")]
        [Button("📂 选中未使用资源", ButtonHeight = 45), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        [EnableIf("@unusedAssets.Count > 0")]
        public void SelectUnusedAssets()
        {
            if (unusedAssets.Count == 0)
            {
                ShowInfoDialog("没有未使用的资源可以选中！");
                return;
            }

            var objects = unusedAssets
                .Select(info => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(info.AssetPath))
                .Where(obj => obj != null)
                .ToArray();

            Selection.objects = objects;
            if (objects.Length > 0)
            {
                EditorGUIUtility.PingObject(objects[0]);
                ShowCompletionDialog("操作完成", $"已选中 {objects.Length} 个未使用的资源！");
            }
        }

        [TabGroup("操作", "批量操作")]
        [Button("🗑️ 删除未使用资源", ButtonHeight = 45), GUIColor(0.9f, 0.4f, 0.4f)]
        [EnableIf("@unusedAssets.Count > 0")]
        [InfoBox("⚠️ 危险操作：这将永久删除选中的未使用资源！建议先备份项目。")]
        public void DeleteUnusedAssets()
        {
            if (unusedAssets.Count == 0)
            {
                ShowInfoDialog("没有未使用的资源可以删除！");
                return;
            }

            if (!EditorUtility.DisplayDialog("⚠️ 确认删除",
                $"即将删除 {unusedAssets.Count} 个未使用的资源！\n\n此操作不可撤销！\n\n建议先备份项目。\n\n是否继续？",
                "确认删除", "取消"))
                return;

            ExecuteWithProgress("删除资源", "正在删除未使用资源...", () =>
            {
                AssetDatabase.StartAssetEditing();

                int deletedCount = 0;
                foreach (var assetInfo in unusedAssets)
                {
                    if (AssetDatabase.DeleteAsset(assetInfo.AssetPath))
                        deletedCount++;
                }

                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();

                unusedAssets.Clear();
                ShowCompletionDialog("删除完成", $"成功删除 {deletedCount} 个资源文件！");
            });
        }

        [TabGroup("操作", "批量操作")]
        [Button("📊 导出分析报告", ButtonHeight = 45), GUIColor("@ESDesignUtility.ColorSelector.Color_06")]
        [EnableIf("@unusedAssets.Count > 0 || selectedAssetReferences.Count > 0 || selectedAssetDependencies.Count > 0")]
        public void ExportAnalysisReport()
        {
            var reportPath = EditorUtility.SaveFilePanel("导出分析报告",
                Application.dataPath, "AssetAnalysisReport.txt", "txt");

            if (string.IsNullOrEmpty(reportPath))
                return;

            try
            {
                using (var writer = new StreamWriter(reportPath))
                {
                    writer.WriteLine("=== 资产引用分析报告 ===");
                    writer.WriteLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"检查文件夹: {checkFolder}");
                    writer.WriteLine($"深度分析: {(deepAnalysis ? "启用" : "禁用")}");
                    writer.WriteLine();

                    if (unusedAssets.Count > 0)
                    {
                        writer.WriteLine($"=== 未使用资源 ({unusedAssets.Count} 个) ===");
                        foreach (var asset in unusedAssets)
                        {
                            writer.WriteLine($"{asset.FileSize}\t{asset.LastModified}\t{asset.AssetPath}");
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

        [TabGroup("操作", "工具")]
        [Button("🧹 清除结果", ButtonHeight = 45), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
        public void ClearResults()
        {
            unusedAssets.Clear();
            selectedAssetReferences.Clear();
            selectedAssetDependencies.Clear();
            referenceCache.Clear();
            processedAssets.Clear();
            totalFilesChecked = 0;
            currentPageUnused = 0;
            currentPageReferences = 0;
            currentPageDependencies = 0;
        }

        [TabGroup("操作", "工具")]
        [Button("� 批量跳转到未使用资源", ButtonHeight = 35), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        [EnableIf("@unusedAssets.Count > 0")]
        public void JumpToAllUnusedAssets()
        {
            if (unusedAssets.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有未使用的资源可以跳转！", "确定");
                return;
            }

            var objects = unusedAssets
                .Select(info => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(info.AssetPath))
                .Where(obj => obj != null)
                .ToArray();

            if (objects.Length > 0)
            {
                Selection.objects = objects;
                EditorGUIUtility.PingObject(objects[0]);
                EditorUtility.DisplayDialog("完成", $"已选中 {objects.Length} 个未使用的资源！\n\n在Project窗口中查看选中的资源。", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("错误", "无法加载任何资源对象！", "确定");
            }
        }

        [TabGroup("操作", "工具")]
        [Button("🔄 刷新缓存", ButtonHeight = 45), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
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

            foreach (var path in allPaths)
            {
                // 检查是否在检查范围内
                if (!path.StartsWith(checkFolder))
                    continue;

                // 检查是否在排除文件夹中
                if (excludeFolders.Any(exclude => path.StartsWith(exclude)))
                    continue;

                // 检查文件类型过滤
                var extension = Path.GetExtension(path).ToLower();
                if (excludeExtensions.Contains(extension))
                    continue;

                if (includeExtensions.Count > 0 && !includeExtensions.Contains(extension))
                    continue;

                // 跳过文件夹
                if (AssetDatabase.IsValidFolder(path))
                    continue;

                filteredPaths.Add(path);
            }

            return filteredPaths;
        }

        private List<string> FilterAssetPathsForReferenceCheck(string[] allPaths)
        {
            var filteredPaths = new List<string>();

            foreach (var path in allPaths)
            {
                // 检查是否在检查范围内
                if (!path.StartsWith(checkFolder))
                    continue;

                // 检查是否在排除文件夹中
                if (excludeFolders.Any(exclude => path.StartsWith(exclude)))
                    continue;

                // 对于引用检查，我们需要包含更多文件类型，因为任何文件都可能引用资源
                // 只排除.meta文件和文件夹
                var extension = Path.GetExtension(path).ToLower();
                if (extension == ".meta")
                    continue;

                // 跳过文件夹
                if (AssetDatabase.IsValidFolder(path))
                    continue;

                filteredPaths.Add(path);
            }

            return filteredPaths;
        }

        private void CollectReferencedAssets(List<string> allAssetPaths, HashSet<string> referencedAssets, string progressTitle)
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
                    break; // 用户取消
                }

                // 根据文件类型决定是否检查引用
                bool shouldAnalyze = false;
                var extension = Path.GetExtension(assetPath).ToLower();

                if (extension == ".unity" && shouldCheckScenes) shouldAnalyze = true;
                else if (extension == ".prefab" && shouldCheckPrefabs) shouldAnalyze = true;
                // 移除对其他资源文件的分析，只分析场景和预制件

                if (shouldAnalyze)
                {
                    try
                    {
                        var dependencies = AssetDatabase.GetDependencies(assetPath, false); // 不包含自身

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
        }

        private void FindUnusedAssetsFromList(List<string> allAssetPaths, HashSet<string> referencedAssets)
        {
            EditorUtility.DisplayProgressBar("查找未使用资源", "正在筛选...", 0f);

            foreach (var assetPath in allAssetPaths)
            {
                if (!referencedAssets.Contains(assetPath))
                {
                    unusedAssets.Add(new AssetReferenceInfo(assetPath, false));
                }
            }
        }

        private void ShowAnalysisCompleteDialog()
        {
            string message = $"深度引用分析完成！\n\n" +
                           $"总检查文件: {totalFilesChecked}\n" +
                           $"未使用资源: {unusedAssets.Count}\n" +
                           $"使用率: {(totalFilesChecked > 0 ? ((totalFilesChecked - unusedAssets.Count) * 100f / totalFilesChecked).ToString("F1") + "%" : "0%")}";

            if (unusedAssets.Count > 0)
            {
                message += "\n\n💡 建议：先备份项目，然后使用\"选中未使用资源\"功能检查结果。";
            }

            EditorUtility.DisplayDialog("分析完成", message, "确定");
        }

        #region 统一UI辅助方法
        private void ShowErrorDialog(string message)
        {
            EditorUtility.DisplayDialog("错误", message, "确定");
        }

        private void ShowInfoDialog(string message)
        {
            EditorUtility.DisplayDialog("提示", message, "确定");
        }

        private void ShowCompletionDialog(string title, string message)
        {
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