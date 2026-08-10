using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using ES;
using System.IO;
using System.Linq;

namespace ES
{
    #region UnityPackage打包工具

    [Serializable]
    [ESSimpleToolsLayout]
    public class Page_UnityPackageTool : ESWindowPageBase
    {
        [Serializable]
        private sealed class InstallerPackageMetadata
        {
            public string version = string.Empty;
            public InstallerDependencyMetadata[] unityDependencies = Array.Empty<InstallerDependencyMetadata>();
            public InstallerDependencyMetadata[] gitDependencies = Array.Empty<InstallerDependencyMetadata>();
        }

        [Serializable]
        private sealed class InstallerDependencyMetadata
        {
            public string name = string.Empty;
            public string packageId = string.Empty;
            public string checkClass = string.Empty;
            public string gitUrl = string.Empty;
            public bool isRequired;
        }

        private sealed class PublishAssetPlan
        {
            public readonly List<string> assetPaths = new List<string>();
            public readonly List<string> packageDependencies = new List<string>();
            public readonly List<string> externalAssetDependencies = new List<string>();
            public int directAssetCount;
            public int dependencyAssetCount;
            public int requiredAssetCount;
            public long sourceBytes;
            public ESFrameworkPublishHardcodedPathAudit hardcodedPathAudit;
        }


        [HideInInspector]
        [DisplayAsString(fontSize: 13), HideLabel]
        public string readMe = "选择或创建打包配置，\n设置要打包的资源，\n点击打包按钮生成UnityPackage";

        [HideInInspector]
        private string PanelSummary
        {
            get
            {
                string configName = currentConfigIndex == -1 ? "默认配置" : GetExtensionConfigName();
                int collectCount = SelectedAssets != null ? SelectedAssets.Count : 0;
                return $"配置: {configName} | 包名: {PackageName} | 收集路径: {collectCount} 个 | 输出: {ExportPath} | 包含依赖: {SimpleToolsSafetyUtility.YesNo(IncludeDependencies)}";
            }
        }

        private string GetExtensionConfigName()
        {
            var globalConfigs = GlobalPackageConfigs;
            if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                return globalConfigs[currentConfigIndex].ConfigName;

            return "扩展配置";
        }

        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private string packagePreviewSearch = "";
        private int packagePreviewPageIndex;
        private const int PackagePreviewPageSize = 14;
        private readonly List<string> cachedPackagePreviewPaths = new List<string>();
        private readonly List<string> cachedFilteredPackagePreviewPaths = new List<string>();
        private string cachedPackagePreviewSignature = "";
        private string cachedPackagePreviewFilterSearch = null;
        private bool packagePreviewLoaded;
        private bool cachedPackagePreviewConfigValid;
        private string cachedPackagePreviewConfigName = "";
        private string cachedPackagePreviewOutputPath = "";
        private string cachedPackagePreviewPackageName = "";
        private bool cachedPackagePreviewIncludeDependencies;
        private const double PublishConfigValidationIntervalSeconds = 2d;
        [NonSerialized] private double nextPublishConfigValidationTime;
        [NonSerialized] private bool cachedPublishConfigValid;
        [NonSerialized] private List<string> cachedPublishRoots = new List<string>();
        [NonSerialized] private List<string> cachedPublishDependencyAllowRoots = new List<string>();
        [NonSerialized] private List<string> cachedPublishExternalReferenceRoots = new List<string>();
        [NonSerialized] private List<string> cachedPublishExclusions = new List<string>();
        [NonSerialized] private string cachedPublishConfigError = string.Empty;

        [OnInspectorGUI, PropertyOrder(100)]
        private void DrawResultPanel()
        {
            var previewPaths = EnsurePackagePreviewCache(false, out var configName, out var outputPath, out var packageName, out var includeDependencies, out var configValid);
            SimpleToolsPanelUtility.DrawToolHeader(
                "UnityPackage 打包",
                "用于把明确的资源路径导出为 .unitypackage，适合框架发布、演示包、局部模块交付和版本归档。",
                SimpleToolsMaturity.Upgrading,
                "导出会递归展开文件夹并可选择包含依赖；请确认收集路径、输出路径和排除规则，避免把临时资源或内部工具打进包里。");
            SimpleToolsPanelUtility.DrawLargeListGuard(previewPaths.Count, "待导出资源");
            DrawPackagePreviewPanel(previewPaths, configName, outputPath, packageName, includeDependencies);
            if (!configValid)
                SimpleToolsPanelUtility.DrawWarning("当前打包配置无效，预览和导出会保持空结果。请检查全局配置对象或扩展配置索引。");
            DrawPackageActionPanel(previewPaths.Count, configValid);
            SimpleToolsPanelUtility.DrawResultSummary("最近打包结果", lastResultSummary, lastResultDetail);
        }

        private void DrawPackageActionPanel(int previewCount, bool configValid)
        {
            SimpleToolsPanelUtility.DrawSectionTitle("执行操作", "普通导出不进入发布链；正式主包必须先通过闭包检查，再签名并交给旧 ESInstaller。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("使用当前选中资源", SimpleToolsActionTone.Neutral, 30, GUILayout.MinWidth(120)))
                        GetSelectedAssets();
                    if (SimpleToolsPanelUtility.DrawActionButton("应用到全局设置", SimpleToolsActionTone.Warning, 30, GUILayout.MinWidth(120)))
                        ApplyToGlobalConfig();
                    if (SimpleToolsPanelUtility.DrawActionButton("定位发布配置", SimpleToolsActionTone.Neutral, 30, GUILayout.MinWidth(110)))
                    {
                        var config = ESGlobalEditorDefaultConfi.Instance;
                        if (config != null)
                        {
                            Selection.activeObject = config;
                            EditorGUIUtility.PingObject(config);
                        }
                    }
                    GUILayout.FlexibleSpace();
                }

                bool publishConfigValid = TryResolvePublishAssetRootsForDisplay(
                    out List<string> publishRoots,
                    out List<string> dependencyAllowRoots,
                    out List<string> externalReferenceRoots,
                    out List<string> publishExclusions,
                    out string publishRootsError);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = configValid && previewCount > 0;
                    if (SimpleToolsPanelUtility.DrawActionButton("普通导出（非发布）", SimpleToolsActionTone.Neutral, 34, GUILayout.MinWidth(150)))
                        ExportPackage();
                    GUI.enabled = publishConfigValid;
                    if (SimpleToolsPanelUtility.DrawActionButton("闭包检查", SimpleToolsActionTone.Primary, 34, GUILayout.MinWidth(100)))
                        AuditPublishPackage();
                    if (SimpleToolsPanelUtility.DrawActionButton("正式发布", SimpleToolsActionTone.Success, 34, GUILayout.MinWidth(105)))
                        PublishPackage();
                    GUI.enabled = true;
                    GUILayout.FlexibleSpace();
                }

                if (publishConfigValid)
                {
                    EditorGUILayout.LabelField(
                        "正式发布白名单：" + string.Join("、", publishRoots)
                        + (dependencyAllowRoots.Count > 0 ? " | 依赖允许根：" + string.Join("、", dependencyAllowRoots) : string.Empty)
                        + (externalReferenceRoots.Count > 0 ? " | 外部引用根：" + string.Join("、", externalReferenceRoots) : string.Empty)
                        + (publishExclusions.Count > 0 ? " | 排除：" + string.Join("、", publishExclusions) : string.Empty),
                        EditorStyles.wordWrappedMiniLabel);
                }
                else
                {
                    SimpleToolsPanelUtility.DrawWarning("正式发布不可用：" + publishRootsError);
                }
            }
        }

        private bool TryResolvePublishAssetRootsForDisplay(
            out List<string> publishRoots,
            out List<string> dependencyAllowRoots,
            out List<string> externalReferenceRoots,
            out List<string> publishExclusions,
            out string error)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now >= nextPublishConfigValidationTime)
            {
                cachedPublishConfigValid = TryResolvePublishAssetRoots(
                    out cachedPublishRoots,
                    out cachedPublishDependencyAllowRoots,
                    out cachedPublishExternalReferenceRoots,
                    out cachedPublishExclusions,
                    out cachedPublishConfigError);
                nextPublishConfigValidationTime = now + PublishConfigValidationIntervalSeconds;
            }

            publishRoots = cachedPublishRoots;
            dependencyAllowRoots = cachedPublishDependencyAllowRoots;
            externalReferenceRoots = cachedPublishExternalReferenceRoots;
            publishExclusions = cachedPublishExclusions;
            error = cachedPublishConfigError;
            return cachedPublishConfigValid;
        }

        private void DrawPackagePreviewPanel(List<string> previewPaths, string configName, string outputPath, string packageName, bool includeDependencies)
        {
            SimpleToolsPanelUtility.DrawSectionTitle("导出预览", "按资源路径搜索；开始打包前先确认展开后的真实资源清单。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                string finalOutputPath = Path.Combine(outputPath ?? string.Empty, SanitizeFileName(packageName) + ".unitypackage").Replace("\\", "/");
                EditorGUILayout.LabelField($"配置: {configName} | 资源: {previewPaths.Count} | 依赖: {GetDependencyInclusionText(includeDependencies)}", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("输出: " + finalOutputPath, EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                    packagePreviewSearch = EditorGUILayout.TextField(packagePreviewSearch);
                    if (GUILayout.Button("刷新预览", EditorStyles.miniButton, GUILayout.Width(68)))
                    {
                        previewPaths = EnsurePackagePreviewCache(true, out configName, out outputPath, out packageName, out includeDependencies, out _);
                        packagePreviewPageIndex = 0;
                    }
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                    {
                        packagePreviewSearch = string.Empty;
                        packagePreviewPageIndex = 0;
                    }
                }

                var rows = FilterPackagePreview(previewPaths);
                if (!packagePreviewLoaded)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("尚未生成资源预览。点击“刷新预览”后才会扫描当前配置，首次打开不会自动遍历项目资源。");
                    return;
                }
                if (rows.Count == 0)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("当前配置没有可导出的资源，或搜索条件没有命中。");
                    return;
                }

                int packagePreviewStart;
                int packagePreviewEnd;
                SimpleToolsPanelUtility.GetPageRange(
                    rows,
                    ref packagePreviewPageIndex,
                    PackagePreviewPageSize,
                    out _,
                    out packagePreviewStart,
                    out packagePreviewEnd);
                for (int i = packagePreviewStart; i < packagePreviewEnd; i++)
                {
                    string path = rows[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                        if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44)))
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                            if (obj != null)
                            {
                                Selection.activeObject = obj;
                                EditorGUIUtility.PingObject(obj);
                            }
                        }
                    }
                }

                SimpleToolsPanelUtility.DrawPager(ref packagePreviewPageIndex, rows.Count, PackagePreviewPageSize);
            }
        }

        private List<string> FilterPackagePreview(List<string> paths)
        {
            if (paths == null)
                return new List<string>();

            if (string.IsNullOrWhiteSpace(packagePreviewSearch))
                return paths;

            string keyword = packagePreviewSearch.Trim();
            if (string.Equals(cachedPackagePreviewFilterSearch, keyword, StringComparison.Ordinal))
                return cachedFilteredPackagePreviewPaths;

            cachedFilteredPackagePreviewPaths.Clear();
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (path != null && path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    cachedFilteredPackagePreviewPaths.Add(path);
            }
            cachedPackagePreviewFilterSearch = keyword;
            return cachedFilteredPackagePreviewPaths;
        }

        private List<string> BuildExpandedPackageAssetPaths(out string configName, out string outputPath, out string packageName, out bool includeDependencies)
        {
            List<string> selectedAssets;
            if (!ResolveCurrentPackageConfig(out selectedAssets, out outputPath, out packageName, out includeDependencies, out configName))
                return new List<string>();
            return ExpandPackageAssetPaths(selectedAssets, ResolveCurrentPackageExcludePaths());
        }

        private List<string> EnsurePackagePreviewCache(bool forceRefresh, out string configName, out string outputPath, out string packageName, out bool includeDependencies, out bool configValid)
        {
            string signature = BuildPackagePreviewSignature(out configValid, out configName, out outputPath, out packageName, out includeDependencies);
            if (signature != cachedPackagePreviewSignature)
            {
                cachedPackagePreviewPaths.Clear();
                InvalidatePackagePreviewFilter();
                packagePreviewLoaded = false;
                cachedPackagePreviewSignature = signature;
                cachedPackagePreviewConfigValid = configValid;
                cachedPackagePreviewConfigName = configName;
                cachedPackagePreviewOutputPath = outputPath;
                cachedPackagePreviewPackageName = packageName;
                cachedPackagePreviewIncludeDependencies = includeDependencies;
            }

            if (forceRefresh)
            {
                cachedPackagePreviewPaths.Clear();
                InvalidatePackagePreviewFilter();
                if (configValid)
                    cachedPackagePreviewPaths.AddRange(BuildExpandedPackageAssetPaths(out configName, out outputPath, out packageName, out includeDependencies));
                packagePreviewLoaded = true;
                cachedPackagePreviewConfigValid = configValid;
                cachedPackagePreviewConfigName = configName;
                cachedPackagePreviewOutputPath = outputPath;
                cachedPackagePreviewPackageName = packageName;
                cachedPackagePreviewIncludeDependencies = includeDependencies;
            }

            configValid = cachedPackagePreviewConfigValid;
            configName = cachedPackagePreviewConfigName;
            outputPath = cachedPackagePreviewOutputPath;
            packageName = cachedPackagePreviewPackageName;
            includeDependencies = cachedPackagePreviewIncludeDependencies;
            return cachedPackagePreviewPaths;
        }

        private void InvalidatePackagePreviewFilter()
        {
            cachedPackagePreviewFilterSearch = null;
            cachedFilteredPackagePreviewPaths.Clear();
        }

        private string BuildPackagePreviewSignature(out bool configValid, out string configName, out string outputPath, out string packageName, out bool includeDependencies)
        {
            configValid = ResolveCurrentPackageConfig(out var selectedAssets, out outputPath, out packageName, out includeDependencies, out configName);
            string pathPart = selectedAssets == null ? "<null>" : string.Join("|", selectedAssets.Select(SimpleToolsSafetyUtility.NormalizeAssetPath));
            string excludePart = string.Join("|", ResolveCurrentPackageExcludePaths().Select(SimpleToolsSafetyUtility.NormalizeAssetPath));
            return $"{currentConfigIndex}|{configValid}|{configName}|{outputPath}|{packageName}|{includeDependencies}|{pathPart}|{excludePart}";
        }

        private bool ResolveCurrentPackageConfig(out List<string> selectedAssets, out string outputPath, out string packageName, out bool includeDependencies, out string configName)
        {
            selectedAssets = null;
            outputPath = ESGlobalEditorDefaultConfi.DefaultUnityPackageOutputPath;
            packageName = "ESPackage";
            includeDependencies = true;
            configName = "默认配置";

            if (currentConfigIndex == -1)
            {
                var globalConfig = ESGlobalEditorDefaultConfi.Instance;
                if (globalConfig == null)
                    return false;

                selectedAssets = globalConfig.PackageCollectPath;
                outputPath = globalConfig.PackageSelfPathForMain ?? ESGlobalEditorDefaultConfi.DefaultUnityPackageOutputPath;
                packageName = globalConfig.PackageName ?? "ESPackage0.35_";
                includeDependencies = globalConfig.IncludeDependencies_;
                configName = "默认配置";
                return true;
            }

            var globalConfigs = GlobalPackageConfigs;
            if (currentConfigIndex < 0 || currentConfigIndex >= globalConfigs.Count)
                return false;

            var currentConfig = globalConfigs[currentConfigIndex];
            selectedAssets = currentConfig.CollectPaths;
            outputPath = currentConfig.OutputPath;
            packageName = currentConfig.PackageName;
            includeDependencies = currentConfig.IncludeDependencies_;
            configName = currentConfig.ConfigName;
            return true;
        }

        private IEnumerable<string> ResolveCurrentPackageExcludePaths()
        {
            if (currentConfigIndex < 0)
                return Array.Empty<string>();
            var globalConfigs = GlobalPackageConfigs;
            if (currentConfigIndex >= globalConfigs.Count)
                return Array.Empty<string>();
            return globalConfigs[currentConfigIndex]?.ExcludeFolders ?? new List<string>();
        }

        private List<string> ExpandPackageAssetPaths(IEnumerable<string> selectedAssets, IEnumerable<string> excludedPaths = null)
        {
            var expandedPaths = new List<string>();
            var expandedPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizedExclusions = new List<string>();
            foreach (string excludedPath in excludedPaths ?? Array.Empty<string>())
            {
                string normalizedExclusion = SimpleToolsSafetyUtility.NormalizeAssetPath(excludedPath)?.TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(normalizedExclusion))
                    normalizedExclusions.Add(normalizedExclusion);
            }
            if (selectedAssets == null)
                return expandedPaths;

            foreach (var path in selectedAssets)
            {
                var normalizedPath = SimpleToolsSafetyUtility.NormalizeAssetPath(path);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                    continue;

                if (AssetDatabase.IsValidFolder(normalizedPath))
                {
                    var guids = AssetDatabase.FindAssets("", new[] { normalizedPath });
                    foreach (var guid in guids)
                    {
                        var assetPath = SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                        if (!AssetDatabase.IsValidFolder(assetPath)
                            && !IsExcludedPackageAsset(assetPath, normalizedExclusions)
                            && CanExportPackageAsset(assetPath)
                            && expandedPathSet.Add(assetPath))
                            expandedPaths.Add(assetPath);
                    }
                }
                else if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalizedPath) != null)
                {
                    if (!IsExcludedPackageAsset(normalizedPath, normalizedExclusions)
                        && CanExportPackageAsset(normalizedPath)
                        && expandedPathSet.Add(normalizedPath))
                        expandedPaths.Add(normalizedPath);
                }
            }

            expandedPaths.Sort(StringComparer.OrdinalIgnoreCase);
            return expandedPaths;
        }

        private static bool IsExcludedPackageAsset(string assetPath, IReadOnlyList<string> excludedRoots)
        {
            for (int i = 0; i < excludedRoots.Count; i++)
            {
                string excludedRoot = excludedRoots[i];
                if (assetPath.Equals(excludedRoot, StringComparison.OrdinalIgnoreCase)
                    || assetPath.StartsWith(excludedRoot + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool CanExportPackageAsset(string assetPath)
        {
            if (!SimpleToolsSafetyUtility.IsAssetPath(assetPath))
                return false;

            const string installerDownloads = "Assets/Plugins/ES/Editor/Installer/Downloads";
            return !assetPath.Equals(installerDownloads, StringComparison.OrdinalIgnoreCase)
                && !assetPath.StartsWith(installerDownloads + "/", StringComparison.OrdinalIgnoreCase);
        }

        #region 配置管理

        [LabelText("当前配置"), ValueDropdown("GetConfigNames"), Space(10)]
        [OnValueChanged("OnConfigChanged")]
        public int currentConfigIndex = -1; // -1 表示默认配置，非负表示扩展配置

        // 使用全局配置列表
        private List<ESGlobalEditorDefaultConfi.UnityPackageConfig> GlobalPackageConfigs
        {
            get
            {
                if (ESGlobalEditorDefaultConfi.Instance == null)
                    return new List<ESGlobalEditorDefaultConfi.UnityPackageConfig>();
                return ESGlobalEditorDefaultConfi.Instance.ExtendedPackageConfigs;
            }
        }

        private bool RecordGlobalConfigUndo(string actionName)
        {
            var config = ESGlobalEditorDefaultConfi.Instance;
            if (config == null)
                return false;

            Undo.RecordObject(config, actionName);
            return true;
        }

        [HorizontalGroup("ConfigButtons", 0.5f), Button("新建配置", ButtonHeight = 25)]
        public void CreateNewConfig()
        {
            var globalConfigs = GlobalPackageConfigs;
            RecordGlobalConfigUndo("新建UnityPackage配置");
            var newConfig = new ESGlobalEditorDefaultConfi.UnityPackageConfig
            {
                ConfigName = $"配置 {globalConfigs.Count + 1}",
                OutputPath = ESGlobalEditorDefaultConfi.Instance?.PackageSelfPathForMain ?? ESGlobalEditorDefaultConfi.DefaultUnityPackageOutputPath,
                PackageName = $"ESPackage_Ext_{globalConfigs.Count + 1}_",
                CollectPaths = new List<string>(ESGlobalEditorDefaultConfi.Instance?.PackageCollectPath ?? new List<string>() { "Assets/Plugins/ES" }),
                ExcludeFolders = new List<string>(),
                IsEnabled = true
            };

            // 从当前设置复制初始值。
            if (globalConfigs.Count > 0 && currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
            {
                var currentConfig = globalConfigs[currentConfigIndex];
                newConfig.PackageName = currentConfig.PackageName;
                newConfig.OutputPath = currentConfig.OutputPath;
                newConfig.CollectPaths = new List<string>(currentConfig.CollectPaths);
                newConfig.ExcludeFolders = new List<string>(currentConfig.ExcludeFolders);
                newConfig.IsEnabled = currentConfig.IsEnabled;
            }

            globalConfigs.Add(newConfig);
            currentConfigIndex = globalConfigs.Count - 1;

            // 标记全局配置为已修改
#if UNITY_EDITOR
            if (ESGlobalEditorDefaultConfi.Instance != null)
                EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
            AssetDatabase.SaveAssets();
#endif
        }

        [HorizontalGroup("ConfigButtons"), Button("保存配置", ButtonHeight = 25)]
        public void SaveCurrentConfig()
        {
            var globalConfigs = GlobalPackageConfigs;
            if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
            {
                // 标记全局配置为已修改
#if UNITY_EDITOR
                if (ESGlobalEditorDefaultConfi.Instance != null)
                    EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
                AssetDatabase.SaveAssets();
#endif
                EditorUtility.DisplayDialog("成功", "配置已保存！", "确定");
            }
        }

        [HorizontalGroup("ConfigButtons"), Button("删除配置", ButtonHeight = 25)]
        public void DeleteCurrentConfig()
        {
            var globalConfigs = GlobalPackageConfigs;
            if (currentConfigIndex < 0 || currentConfigIndex >= globalConfigs.Count)
            {
                EditorUtility.DisplayDialog("不能删除默认配置", "当前选中的是默认配置，不能从扩展配置列表中删除。", "知道了");
                return;
            }

            if (globalConfigs.Count <= 1)
            {
                EditorUtility.DisplayDialog("错误", "至少需要保留一个配置！", "确定");
                return;
            }

            if (EditorUtility.DisplayDialog("确认删除", $"确定要删除配置 '{globalConfigs[currentConfigIndex].ConfigName}' 吗？", "删除", "取消"))
            {
                RecordGlobalConfigUndo("删除UnityPackage配置");
                globalConfigs.RemoveAt(currentConfigIndex);
                if (currentConfigIndex >= globalConfigs.Count)
                    currentConfigIndex = globalConfigs.Count - 1;

                // 标记全局配置为已修改
#if UNITY_EDITOR
                if (ESGlobalEditorDefaultConfi.Instance != null)
                    EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
                AssetDatabase.SaveAssets();
#endif
            }
        }

        [HorizontalGroup("ConfigButtons"), Button("重命名", ButtonHeight = 25)]
        public void RenameCurrentConfig()
        {
            var globalConfigs = GlobalPackageConfigs;
            if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
            {
                var config = globalConfigs[currentConfigIndex];
                EditorInputDialog.Show("重命名配置", "输入新的配置名称:", config.ConfigName, (newName) =>
                {
                    if (!string.IsNullOrEmpty(newName) && newName != config.ConfigName)
                    {
                        RecordGlobalConfigUndo("重命名UnityPackage配置");
                        config.ConfigName = newName;
                        // 标记全局配置为已修改
#if UNITY_EDITOR
                        if (ESGlobalEditorDefaultConfi.Instance != null)
                            EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
                        AssetDatabase.SaveAssets();
#endif
                    }
                });
            }
        }

        private IEnumerable<ValueDropdownItem<int>> GetConfigNames()
        {
            // 添加默认配置选项
            var globalConfig = ESGlobalEditorDefaultConfi.Instance;
            if (globalConfig != null)
            {
                string defaultDisplayName = $"默认配置 ({globalConfig.PackageName})";
                if (globalConfig.PackageCollectPath != null && globalConfig.PackageCollectPath.Count > 0)
                    defaultDisplayName += $" - {globalConfig.PackageCollectPath.Count} 个路径";
                yield return new ValueDropdownItem<int>(defaultDisplayName, -1);
            }

            // 添加扩展配置选项
            var globalConfigs = GlobalPackageConfigs;
            for (int i = 0; i < globalConfigs.Count; i++)
            {
                var config = globalConfigs[i];
                string displayName = $"{config.ConfigName} ({config.PackageName})";
                if (config.CollectPaths.Count > 0)
                    displayName += $" - {config.CollectPaths.Count} 个路径";
                if (!config.IsEnabled)
                    displayName += " [禁用]";
                yield return new ValueDropdownItem<int>(displayName, i);
            }
        }

        private void OnConfigChanged()
        {
            // 配置切换时的处理
            var globalConfigs = GlobalPackageConfigs;
            if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
            {
                // 可以在这里添加配置切换的逻辑
            }
        }

        #endregion

        #region 当前配置属性

        [ShowInInspector, LabelText("包名")]
        public string PackageName
        {
            get
            {
                if (currentConfigIndex == -1)
                {
                    // 使用默认配置
                    return ESGlobalEditorDefaultConfi.Instance?.PackageName ?? "ESPackage0.35_";
                }
                else
                {
                    // 使用扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                        return globalConfigs[currentConfigIndex].PackageName;
                    return "ESPackage_Ext_";
                }
            }
            set
            {
                if (currentConfigIndex == -1)
                {
                    // 修改默认配置
                    if (ESGlobalEditorDefaultConfi.Instance != null)
                    {
                        RecordGlobalConfigUndo("修改UnityPackage包名");
                        ESGlobalEditorDefaultConfi.Instance.PackageName = value;
                        EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
                    }
                }
                else
                {
                    // 修改扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                    {
                        RecordGlobalConfigUndo("修改UnityPackage包名");
                        globalConfigs[currentConfigIndex].PackageName = value;
                        // 标记全局配置为已修改
#if UNITY_EDITOR
                        if (ESGlobalEditorDefaultConfi.Instance != null)
                            EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
#endif
                    }
                }
            }
        }

        [ShowInInspector, LabelText("导出路径"), FolderPath]
        public string ExportPath
        {
            get
            {
                if (currentConfigIndex == -1)
                {
                    // 使用默认配置
                    return ESGlobalEditorDefaultConfi.Instance?.PackageSelfPathForMain ?? ESGlobalEditorDefaultConfi.DefaultUnityPackageOutputPath;
                }
                else
                {
                    // 使用扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                        return globalConfigs[currentConfigIndex].OutputPath;
                    return ESGlobalEditorDefaultConfi.DefaultUnityPackageOutputPath;
                }
            }
            set
            {
                if (currentConfigIndex == -1)
                {
                    // 修改默认配置
                    if (ESGlobalEditorDefaultConfi.Instance != null)
                    {
                        RecordGlobalConfigUndo("修改UnityPackage导出路径");
                        ESGlobalEditorDefaultConfi.Instance.PackageSelfPathForMain = value;
                        EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
                    }
                }
                else
                {
                    // 修改扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                    {
                        RecordGlobalConfigUndo("修改UnityPackage导出路径");
                        globalConfigs[currentConfigIndex].OutputPath = value;
                        // 标记全局配置为已修改
#if UNITY_EDITOR
                        if (ESGlobalEditorDefaultConfi.Instance != null)
                            EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
#endif
                    }
                }
            }
        }

        [ShowInInspector, LabelText("包含依赖项")]
        public bool IncludeDependencies
        {
            get
            {
                if (currentConfigIndex == -1)
                {
                    // 使用默认配置
                    return ESGlobalEditorDefaultConfi.Instance?.IncludeDependencies_ ?? true;
                }
                else
                {
                    // 使用扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                        return globalConfigs[currentConfigIndex].IncludeDependencies_;
                    return true;
                }
            }
            set
            {
                if (currentConfigIndex == -1)
                {
                    // 修改默认配置
                    if (ESGlobalEditorDefaultConfi.Instance != null)
                    {
                        RecordGlobalConfigUndo("修改UnityPackage依赖设置");
                        ESGlobalEditorDefaultConfi.Instance.IncludeDependencies_ = value;
                        EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
                    }
                }
                else
                {
                    // 修改扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                    {
                        RecordGlobalConfigUndo("修改UnityPackage依赖设置");
                        globalConfigs[currentConfigIndex].IncludeDependencies_ = value;
                        // 标记全局配置为已修改
#if UNITY_EDITOR
                        if (ESGlobalEditorDefaultConfi.Instance != null)
                            EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
#endif
                    }
                }
            }
        }

        [ShowInInspector, LabelText("选中的资源路径"), FolderPath, ListDrawerSettings(DraggableItems = false, ShowPaging = true, NumberOfItemsPerPage = 20)]
        public List<string> SelectedAssets
        {
            get
            {
                if (currentConfigIndex == -1)
                {
                    // 使用默认配置
                    return ESGlobalEditorDefaultConfi.Instance?.PackageCollectPath ?? new List<string>();
                }
                else
                {
                    // 使用扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                        return globalConfigs[currentConfigIndex].CollectPaths;
                    return new List<string>();
                }
            }
            set
            {
                if (currentConfigIndex == -1)
                {
                    // 修改默认配置
                    if (ESGlobalEditorDefaultConfi.Instance != null)
                    {
                        RecordGlobalConfigUndo("修改UnityPackage收集路径");
                        ESGlobalEditorDefaultConfi.Instance.PackageCollectPath = value ?? new List<string>();
                        EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
                    }
                }
                else
                {
                    // 修改扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                    {
                        RecordGlobalConfigUndo("修改UnityPackage收集路径");
                        globalConfigs[currentConfigIndex].CollectPaths = value ?? new List<string>();
                        // 标记全局配置为已修改
#if UNITY_EDITOR
                        if (ESGlobalEditorDefaultConfi.Instance != null)
                            EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
#endif
                    }
                }
            }
        }

        [ShowInInspector, LabelText("配置描述"), MultiLineProperty(3)]
        public string ConfigDescription
        {
            get
            {
                if (currentConfigIndex == -1)
                {
                    // 默认配置描述
                    var collectPaths = ESGlobalEditorDefaultConfi.Instance?.PackageCollectPath;
                    int pathCount = collectPaths != null ? collectPaths.Count : 0;
                    return $"默认配置 - 包含 {pathCount} 个路径";
                }
                else
                {
                    // 扩展配置描述
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                        return $"{globalConfigs[currentConfigIndex].ConfigName} - 包含 {globalConfigs[currentConfigIndex].CollectPaths.Count} 个路径";
                    return "";
                }
            }
            set
            {
                // 描述是只读的，由其他字段自动生成
            }
        }

        #endregion

        public override ESWindowPageBase ES_Refresh()
        {
            nextPublishConfigValidationTime = 0d;
            cachedPackagePreviewSignature = string.Empty;
            cachedPackagePreviewPaths.Clear();
            InvalidatePackagePreviewFilter();
            packagePreviewLoaded = false;

            // 初始化全局配置列表
            var globalConfigs = GlobalPackageConfigs;
            if (globalConfigs == null || globalConfigs.Count == 0)
            {
                // 如果全局配置为空，创建一个默认配置。
                var defaultConfig = new ESGlobalEditorDefaultConfi.UnityPackageConfig
                {
                    ConfigName = "默认配置",
                    OutputPath = ESGlobalEditorDefaultConfi.Instance?.PackageSelfPathForMain ?? ESGlobalEditorDefaultConfi.DefaultUnityPackageOutputPath,
                    PackageName = ESGlobalEditorDefaultConfi.Instance?.PackageName ?? "ESPackage0.35_",
                    CollectPaths = new List<string>(ESGlobalEditorDefaultConfi.Instance?.PackageCollectPath ?? new List<string>() { "Assets/Plugins/ES" }),
                    ExcludeFolders = new List<string>(),
                    IsEnabled = true,
                    IncludeDependencies_ = ESGlobalEditorDefaultConfi.Instance?.IncludeDependencies_ ?? true
                };
                globalConfigs.Add(defaultConfig);
                currentConfigIndex = 0;

                // 标记全局配置为已修改
#if UNITY_EDITOR
                if (ESGlobalEditorDefaultConfi.Instance != null)
                    EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
#endif
            }

            // 确保当前配置索引有效
            if (currentConfigIndex != -1 && (currentConfigIndex < 0 || currentConfigIndex >= globalConfigs.Count))
            {
                currentConfigIndex = 0;
            }

            // 获取当前选中的资源。
            var selected = Selection.objects;

            // 处理默认配置的情况。
            if (currentConfigIndex == -1)
            {
                // 对于默认配置，直接使用全局配置的值。
                var globalConfig = ESGlobalEditorDefaultConfi.Instance;
                if (globalConfig != null)
                {
                    // 只有当没有手动设置资源时，才自动更新选中的资源。
                    if (globalConfig.PackageCollectPath == null || globalConfig.PackageCollectPath.Count == 0)
                    {
                        globalConfig.PackageCollectPath = new List<string>();

                        // 1. 添加当前选中的资源。
                        if (selected != null && selected.Length > 0)
                        {
                            globalConfig.PackageCollectPath.AddRange(GetValidSelectedAssetPaths(selected));
                        }

                        // 2. 添加默认收集路径
                        var defaultPaths = new List<string>() { "Assets/Plugins/ES" };
                        foreach (var path in defaultPaths)
                        {
                            if (AssetDatabase.IsValidFolder(path))
                            {
                                // 直接添加文件夹路径。
                                if (!globalConfig.PackageCollectPath.Contains(path))
                                    globalConfig.PackageCollectPath.Add(path);
                            }
                            else if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                            {
                                // 单个资源
                                if (!globalConfig.PackageCollectPath.Contains(path))
                                    globalConfig.PackageCollectPath.Add(path);
                            }
                        }

                        // 标记全局配置为已修改
#if UNITY_EDITOR
                        EditorUtility.SetDirty(globalConfig);
#endif
                    }
                }
            }
            else
            {
                // 处理扩展配置
                var currentConfig = globalConfigs[currentConfigIndex];

                // 只有当没有手动设置资源时，才自动更新选中的资源。
                if (currentConfig.CollectPaths == null || currentConfig.CollectPaths.Count == 0)
                {
                    currentConfig.CollectPaths = new List<string>();

                    // 1. 添加当前选中的资源。
                    if (selected != null && selected.Length > 0)
                    {
                        currentConfig.CollectPaths.AddRange(GetValidSelectedAssetPaths(selected));
                    }

                    // 2. 添加全局配置收集路径（直接添加文件夹或资源路径，不递归展开）
                    var collectPaths = ESGlobalEditorDefaultConfi.Instance?.PackageCollectPath;
                    if (collectPaths != null)
                    {
                        foreach (var path in collectPaths)
                        {
                            if (AssetDatabase.IsValidFolder(path))
                            {
                                // 直接添加文件夹路径。
                                if (!currentConfig.CollectPaths.Contains(path))
                                    currentConfig.CollectPaths.Add(path);
                            }
                            else if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                            {
                                // 单个资源
                                if (!currentConfig.CollectPaths.Contains(path))
                                    currentConfig.CollectPaths.Add(path);
                            }
                        }
                    }

                    // 标记全局配置为已修改
#if UNITY_EDITOR
                    if (ESGlobalEditorDefaultConfi.Instance != null)
                        EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
#endif
                }

                // 初始化时,使用 ESGlobalEditorDefaultConfi 里的默认路径和包名（仅当用户未修改时）
                if (string.IsNullOrWhiteSpace(currentConfig.OutputPath) || currentConfig.OutputPath == ESGlobalEditorDefaultConfi.DefaultUnityPackageOutputPath)
                {
                    try
                    {
                        currentConfig.OutputPath = ESGlobalEditorDefaultConfi.Instance?.PackageSelfPathForMain ?? currentConfig.OutputPath;
                    }
                    catch
                    {
                        // 如果配置不存在或访问失败，保持当前值。
                    }
                }

                if (string.IsNullOrWhiteSpace(currentConfig.PackageName) || currentConfig.PackageName == "ESPackage_Ext_")
                {
                    try
                    {
                        var defaultName = ESGlobalEditorDefaultConfi.Instance?.PackageName;
                        if (!string.IsNullOrWhiteSpace(defaultName))
                            currentConfig.PackageName = defaultName;
                    }
                    catch
                    {
                        // 如果配置不存在或访问失败，保持当前值。
                    }
                }
            }

            return base.ES_Refresh();
        }

        public void GetSelectedAssets()
        {
            var selected = Selection.objects;
            var selectedPaths = GetValidSelectedAssetPaths(selected);
            if (selectedPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("没有选中资源", "请先在 Project 中选择要作为打包收集路径的资源。", "知道了");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(selectedPaths, 8);
            if (!EditorUtility.DisplayDialog("确认替换收集路径",
                $"将用当前选中的 {selectedPaths.Count} 个资源替换当前配置的收集路径。\n\n{preview}\n\n原列表会被清空后重建。继续吗？",
                "替换", "取消"))
                return;

            // 只获取当前选中的资源，不自动加入PackageCollectPath内容
            if (currentConfigIndex == -1)
            {
                // 处理默认配置
                var globalConfig = ESGlobalEditorDefaultConfi.Instance;
                if (globalConfig != null)
                {
                    RecordGlobalConfigUndo("替换UnityPackage收集路径");
                    globalConfig.PackageCollectPath = new List<string>();
                    globalConfig.PackageCollectPath.AddRange(selectedPaths);

                    // 标记全局配置为已修改
#if UNITY_EDITOR
                    EditorUtility.SetDirty(globalConfig);
                    AssetDatabase.SaveAssets();
#endif
                }
            }
            else
            {
                // 处理扩展配置
                var globalConfigs = GlobalPackageConfigs;
                if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                {
                    RecordGlobalConfigUndo("替换UnityPackage收集路径");
                    var currentConfig = globalConfigs[currentConfigIndex];
                    currentConfig.CollectPaths.Clear();
                    currentConfig.CollectPaths.AddRange(selectedPaths);

                    // 标记全局配置为已修改
#if UNITY_EDITOR
                    if (ESGlobalEditorDefaultConfi.Instance != null)
                        EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
                    AssetDatabase.SaveAssets();
#endif
                }
            }
        }

        public void ApplyToGlobalConfig()
        {
            if (currentConfigIndex == -1)
            {
                // 默认配置已经是全局配置，无需应用
                EditorUtility.DisplayDialog("提示", "当前已是默认配置，无需应用到全局设置！", "确定");
                return;
            }

            var globalConfigs = GlobalPackageConfigs;
            if (currentConfigIndex < 0 || currentConfigIndex >= globalConfigs.Count)
                return;

            var currentConfig = globalConfigs[currentConfigIndex];
            var config = ESGlobalEditorDefaultConfi.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到全局配置对象！", "确定");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(currentConfig.CollectPaths, 8);
            if (!EditorUtility.DisplayDialog("确认应用到全局设置",
                $"将把扩展配置“{currentConfig.ConfigName}”应用到全局打包设置。\n\n包名：{currentConfig.PackageName}\n输出：{currentConfig.OutputPath}\n收集路径：{currentConfig.CollectPaths.Count} 个\n{preview}\n\n会修改全局编辑器配置。继续吗？",
                "应用", "取消"))
                return;

            RecordGlobalConfigUndo("应用UnityPackage配置到全局设置");
            config.PackageName = currentConfig.PackageName;
            config.PackageSelfPathForMain = currentConfig.OutputPath;
            config.IncludeDependencies_ = currentConfig.IncludeDependencies_;
            // 合并收集路径，去重。
            var allPaths = new HashSet<string>(config.PackageCollectPath ?? new List<string>());
            foreach (var path in currentConfig.CollectPaths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                    allPaths.Add(path);
            }
            config.PackageCollectPath = allPaths.ToList();
#if UNITY_EDITOR
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
#endif
            EditorUtility.DisplayDialog("成功", "已将设置应用到全局配置！", "确定");
        }

        public void ExportPackage()
        {
            List<string> selectedAssets;
            string outputPath;
            string packageName;
            bool includeDependencies;
            string configName;

            if (!ResolveCurrentPackageConfig(out selectedAssets, out outputPath, out packageName, out includeDependencies, out configName))
            {
                EditorUtility.DisplayDialog("错误", "当前打包配置无效或全局配置不存在。", "确定");
                return;
            }

            if (selectedAssets == null || selectedAssets.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先设置要打包的资源路径！", "确定");
                return;
            }

            var expandedPaths = ExpandPackageAssetPaths(selectedAssets, ResolveCurrentPackageExcludePaths());
            var assetPaths = expandedPaths.ToArray();
            if (assetPaths.Length == 0)
            {
                EditorUtility.DisplayDialog("没有可导出的资源", "收集路径展开后没有找到可导出的有效资源。", "知道了");
                return;
            }

            string managedOutputDirectory;
            string outputPathError;
            if (!TryResolveManagedPackageOutputDirectory(outputPath, true, out managedOutputDirectory, out outputPathError))
            {
                EditorUtility.DisplayDialog("输出路径不受管", outputPathError, "确定");
                return;
            }

            var finalOutputPath = Path.Combine(managedOutputDirectory, SanitizeFileName(packageName) + ".unitypackage");

            string exportPreview = SimpleToolsSafetyUtility.JoinPreview(assetPaths, 12);
            if (!SimpleToolsPanelUtility.ConfirmHeavyOperation(
                "确认导出 UnityPackage",
                assetPaths.Length,
                $"配置：{configName}\n输出：{finalOutputPath}\n包含依赖：{SimpleToolsSafetyUtility.YesNo(includeDependencies)}\n\n{exportPreview}",
                "会把展开后的资源写入 unitypackage 文件。包含依赖时包体可能明显变大，请确认没有把临时文件或内部工具打进去。"))
                return;

            try
            {
                string stagedOutputPath = BuildUniqueStagingPackagePath(managedOutputDirectory, finalOutputPath);
                AssetDatabase.ExportPackage(assetPaths, stagedOutputPath,
                    includeDependencies ? ExportPackageOptions.IncludeDependencies : ExportPackageOptions.Default);

                PromoteExportedPackage(stagedOutputPath, finalOutputPath, managedOutputDirectory);

                lastResultSummary = $"打包完成: {assetPaths.Length} 个资源 | 配置 {configName} | 依赖 {GetDependencyInclusionText(includeDependencies)}";
                lastResultDetail = $"输出文件:\n{finalOutputPath}\n\n资源预览:\n" + SimpleToolsSafetyUtility.JoinPreview(assetPaths, 12);
                EditorUtility.DisplayDialog("成功", $"Package导出成功！\n配置: {configName}\n路径: {finalOutputPath}", "确定");
                ESEditorFeedbackSoundHook.NotifyBuildCompleted(true);
                EditorUtility.RevealInFinder(finalOutputPath);
            }
            catch (System.Exception e)
            {
                lastResultSummary = $"打包失败: 配置 {configName} | 资源 {assetPaths.Length} 个";
                lastResultDetail = e.Message;
                ESEditorFeedbackSoundHook.NotifyBuildCompleted(false);
                EditorUtility.DisplayDialog("错误", $"导出失败: {e.Message}", "确定");
            }
        }

        private void AuditPublishPackage()
        {
            if (!TryBuildPublishAssetPlan(out PublishAssetPlan plan, out string error))
            {
                lastResultSummary = "发布闭包检查失败";
                lastResultDetail = error;
                EditorUtility.DisplayDialog("发布闭包未通过", error, "确定");
                return;
            }

            string packageText = plan.packageDependencies.Count == 0
                ? "无"
                : string.Join("、", plan.packageDependencies);
            lastResultSummary =
                $"发布闭包通过: 直接 {plan.directAssetCount} | 必需 {plan.requiredAssetCount} | 依赖 {plan.dependencyAssetCount} | 总计 {plan.assetPaths.Count} | {FormatBytes(plan.sourceBytes)}";
            lastResultDetail =
                "UPM/Git 依赖（不进入 unitypackage，由 ESInstaller 配置负责）：\n" + packageText
                + "\n\n商业/外部 Assets 依赖（只允许引用，不进入 unitypackage）：\n"
                + (plan.externalAssetDependencies.Count == 0 ? "无" : string.Join("、", plan.externalAssetDependencies))
                + "\n\n硬编码资产路径分类：随包 " + (plan.hardcodedPathAudit?.requiredCount ?? 0)
                + " | 按需生成 " + (plan.hardcodedPathAudit?.generatedCount ?? 0)
                + " | 项目状态 " + (plan.hardcodedPathAudit?.projectOwnedCount ?? 0)
                + " | 可选重内容 " + (plan.hardcodedPathAudit?.optionalHeavyCount ?? 0)
                + "\n\n最终资源预览：\n" + SimpleToolsSafetyUtility.JoinPreview(plan.assetPaths, 40);
            EditorUtility.DisplayDialog(
                "发布闭包通过",
                $"直接资源：{plan.directAssetCount}\n必需资产：{plan.requiredAssetCount}\n依赖资源：{plan.dependencyAssetCount}\n总计：{plan.assetPaths.Count}\n源文件体积：{FormatBytes(plan.sourceBytes)}\n外部包：{packageText}\n外部 Assets：{string.Join("、", plan.externalAssetDependencies)}",
                "确定");
        }

        public void PublishPackage()
        {
            const string packageId = "es_main";
            if (!TryBuildPublishAssetPlan(out PublishAssetPlan publishPlan, out string publishPlanError))
            {
                EditorUtility.DisplayDialog("正式发布闭包无效", publishPlanError, "确定");
                return;
            }

            List<string> assetPaths = publishPlan.assetPaths;

            if (assetPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "正式发布白名单中没有可发布资源。", "确定");
                return;
            }

            if (!TryReadMainPackageVersion(out string packageVersion, out string versionError))
            {
                EditorUtility.DisplayDialog("主包元数据无效", versionError, "确定");
                return;
            }

            string outputDir = ESGlobalEditorDefaultConfi.DefaultUnityPackageOutputPath;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string managedOutputDirectory;
            string outputPathError;
            if (!TryResolveManagedPackageOutputDirectory(outputDir, true, out managedOutputDirectory, out outputPathError))
            {
                EditorUtility.DisplayDialog("输出路径不受管", outputPathError, "确定");
                return;
            }

            string packageName = "ESFramework_" + packageVersion + "_" + timestamp;
            string outputPath = Path.Combine(managedOutputDirectory, SanitizeFileName(packageName) + ".unitypackage");

            string publishPreview = SimpleToolsSafetyUtility.JoinPreview(assetPaths, 12);
            if (!SimpleToolsPanelUtility.ConfirmHeavyOperation(
                "确认发布 ES Framework 主包",
                assetPaths.Count,
                $"直接资源：{publishPlan.directAssetCount}\n必需资产：{publishPlan.requiredAssetCount}\n闭包依赖：{publishPlan.dependencyAssetCount}\n总计：{publishPlan.assetPaths.Count}\n源文件体积：{FormatBytes(publishPlan.sourceBytes)}\n外部包：{string.Join("、", publishPlan.packageDependencies)}\n外部 Assets：{string.Join("、", publishPlan.externalAssetDependencies)}\n\n版本：{packageVersion}\n归档输出：{outputPath}\n安装入口：旧 ESInstaller Downloads/Main\n自动包含全部依赖：否\n\n{publishPreview}",
                "发布会精确加入允许根内的资源依赖，归档 Downloads/Main 中的旧正式包，再写入新主包和 ESInstaller 签名清单。"))
                return;

            try
            {
                // 不包含依赖的发布打包
                string stagedOutputPath = BuildUniqueStagingPackagePath(managedOutputDirectory, outputPath);
                AssetDatabase.ExportPackage(assetPaths.ToArray(), stagedOutputPath, ExportPackageOptions.Default);
                if (!ESFrameworkPublishContentPolicy.TryValidateExportedUnityPackage(
                        stagedOutputPath,
                        assetPaths,
                        out int packagedPathCount,
                        out string packageContentError))
                    throw new InvalidDataException(packageContentError);
                PromoteExportedPackage(stagedOutputPath, outputPath, managedOutputDirectory);

                if (!TryPublishThroughExistingInstaller(
                        outputPath,
                        packageId,
                        packageVersion,
                        out string installerPackagePath,
                        out string manifestPath,
                        out string signingKeyId,
                        out string publishError))
                    throw new InvalidOperationException(publishError);

                AssetDatabase.Refresh();
                bool developmentSigned = string.Equals(
                    signingKeyId,
                    "es-local-dev",
                    StringComparison.Ordinal);
                string trustText = developmentSigned
                    ? "本机开发签名，仅用于本机安装验证"
                    : "生产签名 " + signingKeyId;
                lastResultSummary = $"主包发布完成: 计划 {assetPaths.Count} 个资源 | 包内路径 {packagedPathCount} 个 | {packageVersion} | {trustText}";
                lastResultDetail =
                    "归档产物:\n" + outputPath
                    + "\n\nESInstaller 主包:\n" + installerPackagePath
                    + "\n\n签名清单:\n" + manifestPath
                    + "\n\n资源预览:\n" + SimpleToolsSafetyUtility.JoinPreview(assetPaths, 12);
                EditorUtility.DisplayDialog(
                    "发布完成",
                    "主包和签名清单已接入旧 ESInstaller。\n\n版本：" + packageVersion
                    + "\n签名：" + trustText
                    + "\n安装目录：" + Path.GetDirectoryName(installerPackagePath),
                    "确定");
                ESEditorFeedbackSoundHook.NotifyBuildCompleted(true);
                EditorUtility.RevealInFinder(installerPackagePath);
            }
            catch (System.Exception e)
            {
                lastResultSummary = "发布打包失败：正式发布白名单未能完成导出或签名。";
                lastResultDetail = e.Message;
                ESEditorFeedbackSoundHook.NotifyBuildCompleted(false);
                EditorUtility.DisplayDialog("错误", $"发布打包失败: {e.Message}", "确定");
            }
        }

        private bool TryBuildPublishAssetPlan(out PublishAssetPlan plan, out string error)
        {
            plan = null;
            error = string.Empty;
            if (!TryResolvePublishAssetRoots(
                    out List<string> publishRoots,
                    out List<string> dependencyAllowRoots,
                    out List<string> externalReferenceRoots,
                    out List<string> publishExclusions,
                    out error))
                return false;

            ESGlobalEditorDefaultConfi config = ESGlobalEditorDefaultConfi.Instance;
            List<string> requiredAssetPaths = ResolveRequiredPublishAssetPaths(config);
            if (!ESFrameworkPublishContentPolicy.TryValidateConfiguration(
                    publishRoots,
                    requiredAssetPaths,
                    publishExclusions,
                    out error))
                return false;

            if (!ESFrameworkPublishContentPolicy.TryAuditHardcodedAssetPaths(
                    new[] { "Assets/Plugins/ES", "Assets/Scripts/ESLogic" },
                    out ESFrameworkPublishHardcodedPathAudit hardcodedPathAudit,
                    out error))
                return false;

            if (!TryValidateMainPackageDependencyMetadata(out error))
                return false;

            List<string> rootAssets = ExpandPackageAssetPaths(publishRoots, publishExclusions);
            List<string> requiredAssets = ExpandPackageAssetPaths(requiredAssetPaths, publishExclusions);
            var directAssetSet = new HashSet<string>(rootAssets, StringComparer.OrdinalIgnoreCase);
            directAssetSet.UnionWith(requiredAssets);
            List<string> directAssets = directAssetSet
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (directAssets.Count == 0)
            {
                error = "正式发布白名单中没有可发布资源。";
                return false;
            }

            var assetSet = new HashSet<string>(directAssets, StringComparer.OrdinalIgnoreCase);
            var packageSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var externalAssetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rejectedDependencies = new List<string>();
            string[] dependencies;
            try
            {
                dependencies = AssetDatabase.GetDependencies(directAssets.ToArray(), true);
            }
            catch (Exception exception)
            {
                error = "读取发布资源依赖失败：" + exception.Message;
                return false;
            }

            foreach (string dependency in dependencies ?? Array.Empty<string>())
            {
                string normalizedDependency = SimpleToolsSafetyUtility.NormalizeAssetPath(dependency);
                if (string.IsNullOrWhiteSpace(normalizedDependency)
                    || AssetDatabase.IsValidFolder(normalizedDependency)
                    || assetSet.Contains(normalizedDependency))
                    continue;

                if (normalizedDependency.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                {
                    string[] segments = normalizedDependency.Split('/');
                    if (segments.Length >= 2)
                        packageSet.Add(segments[1]);
                    continue;
                }

                if (IsUnityBuiltInDependency(normalizedDependency))
                    continue;

                if (!SimpleToolsSafetyUtility.IsAssetPath(normalizedDependency))
                {
                    rejectedDependencies.Add(normalizedDependency + "（不是受管 Assets/Packages 路径）");
                    continue;
                }

                if (IsExcludedPackageAsset(normalizedDependency, publishExclusions))
                {
                    rejectedDependencies.Add(normalizedDependency + "（命中正式发布排除路径）");
                    continue;
                }

                bool isInsideDirectRoot = publishRoots.Any(root => IsPathAtOrUnder(normalizedDependency, root));
                bool isInsideDependencyAllowRoot = dependencyAllowRoots.Any(root => IsPathAtOrUnder(normalizedDependency, root));
                string externalReferenceRoot = externalReferenceRoots.FirstOrDefault(root => IsPathAtOrUnder(normalizedDependency, root));
                if (!string.IsNullOrEmpty(externalReferenceRoot))
                {
                    externalAssetSet.Add(externalReferenceRoot);
                    continue;
                }
                if (!isInsideDirectRoot && !isInsideDependencyAllowRoot)
                {
                    rejectedDependencies.Add(normalizedDependency + "（未进入依赖允许根）");
                    continue;
                }

                if (CanExportPackageAsset(normalizedDependency))
                    assetSet.Add(normalizedDependency);
            }

            if (rejectedDependencies.Count > 0)
            {
                rejectedDependencies.Sort(StringComparer.OrdinalIgnoreCase);
                error =
                    "发现 " + rejectedDependencies.Count + " 个未闭合依赖，正式发布已拒绝。"
                    + "\n请把确属框架/示例的上级目录加入“发布依赖允许根”，或修复/移除该引用。"
                    + "\n不要开启 IncludeDependencies 绕过门禁。\n\n"
                    + SimpleToolsSafetyUtility.JoinPreview(rejectedDependencies, 40);
                return false;
            }

            var result = new PublishAssetPlan
            {
                directAssetCount = directAssets.Count,
                requiredAssetCount = requiredAssets.Count,
                dependencyAssetCount = assetSet.Count - directAssets.Count,
                hardcodedPathAudit = hardcodedPathAudit
            };
            result.assetPaths.AddRange(assetSet.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            result.packageDependencies.AddRange(packageSet.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            result.externalAssetDependencies.AddRange(externalAssetSet.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

            int maxAssetCount = config?.PackagePublishMaxAssetCount
                ?? ESGlobalEditorDefaultConfi.DefaultPackagePublishMaxAssetCount;
            long maxSourceBytes = config?.PackagePublishMaxSourceBytes
                ?? ESGlobalEditorDefaultConfi.DefaultPackagePublishMaxSourceBytes;
            if (maxAssetCount <= 0 || maxSourceBytes <= 0)
            {
                error = "正式发布资源数/源文件体积预算必须大于 0。";
                return false;
            }
            if (result.assetPaths.Count > maxAssetCount)
            {
                error = $"正式发布资源数 {result.assetPaths.Count} 超过预算 {maxAssetCount}。请先审计新增内容，不允许静默放宽。";
                return false;
            }
            if (!ESFrameworkPublishContentPolicy.TryMeasureSourceBytes(result.assetPaths, out result.sourceBytes, out error))
                return false;
            if (result.sourceBytes > maxSourceBytes)
            {
                error = $"正式发布源文件体积 {FormatBytes(result.sourceBytes)} 超过预算 {FormatBytes(maxSourceBytes)}。请拆分可选重内容或显式调整预算。";
                return false;
            }

            plan = result;
            return true;
        }

        private static List<string> ResolveRequiredPublishAssetPaths(ESGlobalEditorDefaultConfi config)
        {
            IEnumerable<string> configured = config?.PackagePublishRequiredAssetPaths;
            if (configured == null || !configured.Any())
                configured = ESGlobalEditorDefaultConfi.CreateDefaultPackagePublishRequiredAssetPaths();

            return configured
                .Select(SimpleToolsSafetyUtility.NormalizeAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool TryResolvePublishAssetRoots(
            out List<string> publishRoots,
            out List<string> dependencyAllowRoots,
            out List<string> externalReferenceRoots,
            out List<string> publishExclusions,
            out string error)
        {
            publishRoots = new List<string>();
            dependencyAllowRoots = new List<string>();
            externalReferenceRoots = new List<string>();
            publishExclusions = new List<string>();
            error = string.Empty;
            var config = ESGlobalEditorDefaultConfi.Instance;
            if (config == null)
            {
                error = "未找到 ES 全局编辑器配置。";
                return false;
            }

            IEnumerable<string> configuredRoots = config.PackagePublishAssetPaths;
            if (configuredRoots == null || !configuredRoots.Any())
                configuredRoots = ESGlobalEditorDefaultConfi.CreateDefaultPackagePublishAssetPaths();

            var uniqueRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string configuredRoot in configuredRoots)
            {
                string normalizedRoot = SimpleToolsSafetyUtility.NormalizeAssetPath(configuredRoot);
                if (!SimpleToolsSafetyUtility.IsAssetPath(normalizedRoot))
                {
                    error = "发布白名单只允许项目 Assets 路径：" + configuredRoot;
                    return false;
                }
                if (normalizedRoot.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                {
                    error = "发布白名单不能直接使用整个 Assets 根。";
                    return false;
                }
                if (!AssetDatabase.IsValidFolder(normalizedRoot))
                {
                    error = "发布白名单目录不存在：" + normalizedRoot;
                    return false;
                }
                if (normalizedRoot.Equals("Assets/Plugins/ES/Editor/Installer/Downloads", StringComparison.OrdinalIgnoreCase)
                    || normalizedRoot.StartsWith("Assets/Plugins/ES/Editor/Installer/Downloads/", StringComparison.OrdinalIgnoreCase))
                {
                    error = "发布白名单不能包含 Installer/Downloads：" + normalizedRoot;
                    return false;
                }
                if (uniqueRoots.Add(normalizedRoot))
                    publishRoots.Add(normalizedRoot);
            }

            if (!uniqueRoots.Contains("Assets/Plugins/ES"))
            {
                error = "正式主包必须包含权威框架根 Assets/Plugins/ES。";
                return false;
            }

            publishRoots.Sort(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> configuredDependencyAllowRoots = config.PackagePublishDependencyAllowPaths;
            if (configuredDependencyAllowRoots == null || !configuredDependencyAllowRoots.Any())
            {
                configuredDependencyAllowRoots = new[]
                {
                    "Assets/ESNormalAssets",
                    "Assets/LoafbrrAssets",
                    "Assets/Demo_FGT"
                };
            }

            var uniqueDependencyAllowRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string configuredDependencyAllowRoot in configuredDependencyAllowRoots)
            {
                string normalizedRoot = SimpleToolsSafetyUtility.NormalizeAssetPath(configuredDependencyAllowRoot)?.TrimEnd('/');
                if (!SimpleToolsSafetyUtility.IsAssetPath(normalizedRoot)
                    || normalizedRoot.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                    || !AssetDatabase.IsValidFolder(normalizedRoot))
                {
                    error = "发布依赖允许根不存在、过宽或不是 Assets 目录：" + configuredDependencyAllowRoot;
                    return false;
                }
                if (uniqueDependencyAllowRoots.Add(normalizedRoot))
                    dependencyAllowRoots.Add(normalizedRoot);
            }
            dependencyAllowRoots.Sort(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> configuredExternalReferenceRoots = config.PackagePublishExternalReferencePaths;
            if (configuredExternalReferenceRoots == null)
                configuredExternalReferenceRoots = Array.Empty<string>();

            var uniqueExternalReferenceRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string configuredExternalReferenceRoot in configuredExternalReferenceRoots)
            {
                string normalizedRoot = SimpleToolsSafetyUtility.NormalizeAssetPath(configuredExternalReferenceRoot)?.TrimEnd('/');
                if (!SimpleToolsSafetyUtility.IsAssetPath(normalizedRoot)
                    || normalizedRoot.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                    || !AssetDatabase.IsValidFolder(normalizedRoot))
                {
                    error = "外部依赖引用根不存在、过宽或不是 Assets 目录：" + configuredExternalReferenceRoot;
                    return false;
                }
                if (publishRoots.Any(root => IsPathAtOrUnder(normalizedRoot, root) || IsPathAtOrUnder(root, normalizedRoot))
                    || dependencyAllowRoots.Any(root => IsPathAtOrUnder(normalizedRoot, root) || IsPathAtOrUnder(root, normalizedRoot)))
                {
                    error = "外部依赖引用根不能与导出根或依赖允许根重叠：" + normalizedRoot;
                    return false;
                }
                if (uniqueExternalReferenceRoots.Add(normalizedRoot))
                    externalReferenceRoots.Add(normalizedRoot);
            }
            externalReferenceRoots.Sort(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> configuredExclusions = config.PackagePublishExcludePaths;
            if (configuredExclusions == null || !configuredExclusions.Any())
            {
                configuredExclusions = new[]
                {
                    "Assets/Plugins/ES/Obsolete",
                    "Assets/Plugins/ES/Editor/Installer/Downloads",
                    "Assets/Plugins/ES/0_Stand/Tests",
                    "Assets/Plugins/ES/1_Design/Tests",
                    "Assets/Scripts/ESLogic/Tests",
                    "Assets/Scripts/ESLogic/Editor/Generation/Tests",
                    "Assets/Scripts/ESLogic/Runtime/Developer/AITest",
                    "Assets/Plugins/RootMotion/Shared Demo Assets",
                    "Assets/Plugins/RootMotion/FinalIK/_DEMOS",
                    "Assets/Plugins/RootMotion/FinalIK/_Integration",
                    "Assets/Plugins/RootMotion/Baker",
                    "Assets/Plugins/RootMotion/Editor/Baker",
                    "Assets/Plugins/RootMotion/Editor/FinalIK/_DEMOS",
                    "Assets/Plugins/RootMotion/Editor/Shared Demo Scripts",
                    "Assets/Plugins/RootMotion/FinalIK/Tools/VRIK Animated Locomotion.controller",
                    "Assets/Plugins/Easy Save 3/Scripts/Save Slots",
                    "Assets/Plugins/ES/3_Examples/1_Runtime/Example_SimpleTools/New Scene 1.unity",
                    "Assets/Plugins/ES/ThirdParty/JUMP_SystemSpeech.asset"
                };
            }
            var uniqueExclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string configuredExclusion in configuredExclusions)
            {
                string normalizedExclusion = SimpleToolsSafetyUtility.NormalizeAssetPath(configuredExclusion)?.TrimEnd('/');
                if (!SimpleToolsSafetyUtility.IsAssetPath(normalizedExclusion))
                {
                    error = "正式发布排除路径不是受管 Assets 文件/目录：" + configuredExclusion;
                    return false;
                }
                bool exclusionExists = AssetDatabase.IsValidFolder(normalizedExclusion)
                    || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(normalizedExclusion));
                if (!exclusionExists)
                {
                    error = "正式发布排除路径不存在：" + configuredExclusion;
                    return false;
                }
                if (normalizedExclusion.Equals("Assets/Plugins/ES", StringComparison.OrdinalIgnoreCase))
                {
                    error = "不能排除权威框架根 Assets/Plugins/ES。";
                    return false;
                }
                bool belongsToPublishRoot = publishRoots.Any(root =>
                    normalizedExclusion.Equals(root, StringComparison.OrdinalIgnoreCase)
                    || normalizedExclusion.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
                if (!belongsToPublishRoot)
                {
                    error = "排除路径必须位于正式发布白名单内：" + normalizedExclusion;
                    return false;
                }
                if (uniqueExclusions.Add(normalizedExclusion))
                    publishExclusions.Add(normalizedExclusion);
            }
            publishExclusions.Sort(StringComparer.OrdinalIgnoreCase);
            return true;
        }

        private static bool IsPathAtOrUnder(string assetPath, string root)
        {
            return assetPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUnityBuiltInDependency(string path)
        {
            return path.Equals("Resources/unity_builtin_extra", StringComparison.OrdinalIgnoreCase)
                || path.Equals("Library/unity default resources", StringComparison.OrdinalIgnoreCase)
                || path.Equals("Library/unity editor resources", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryPublishThroughExistingInstaller(
            string exportedPackagePath,
            string packageId,
            string packageVersion,
            out string installerPackagePath,
            out string manifestPath,
            out string signingKeyId,
            out string error)
        {
            installerPackagePath = string.Empty;
            manifestPath = string.Empty;
            signingKeyId = string.Empty;
            error = string.Empty;
            try
            {
                Type publisherType = Type.GetType(
                    "ES.EditorInternal.Installer.ESInstallerPackageTrust, ESInstaller",
                    false);
                if (publisherType == null)
                {
                    error = "旧 ESInstaller 发布模块未加载；请确认 ESInstaller.asmdef 已成功编译。";
                    return false;
                }

                var publishMethod = publisherType.GetMethod(
                    "TryPublishMainPackage",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (publishMethod == null)
                {
                    error = "旧 ESInstaller 发布模块缺少固定入口 TryPublishMainPackage。";
                    return false;
                }

                object[] arguments =
                {
                    exportedPackagePath,
                    packageId,
                    packageVersion,
                    null,
                    null,
                    null,
                    null,
                };
                bool succeeded = (bool)publishMethod.Invoke(null, arguments);
                installerPackagePath = arguments[3] as string ?? string.Empty;
                manifestPath = arguments[4] as string ?? string.Empty;
                signingKeyId = arguments[5] as string ?? string.Empty;
                error = arguments[6] as string ?? string.Empty;
                return succeeded;
            }
            catch (Exception exception)
            {
                error = "调用旧 ESInstaller 发布模块失败：" + (exception.InnerException?.Message ?? exception.Message);
                return false;
            }
        }

        private static readonly string[] RequiredUnityDependencyPackageIds =
        {
            "com.unity.textmeshpro",
            "com.unity.ugui",
            "com.unity.timeline",
            "com.unity.render-pipelines.universal",
            "com.unity.inputsystem",
            "com.unity.cinemachine"
        };

        private static readonly string[] RequiredGitDependencyCheckClasses =
        {
            "Cysharp.Threading.Tasks.UniTask",
            "MemoryPack.MemoryPackSerializer",
            "HybridCLR.RuntimeApi",
            "Luban.EditorBeanBase",
            "PrimeTween.Tween"
        };

        private static bool TryValidateMainPackageDependencyMetadata(out string error)
        {
            error = string.Empty;
            if (!TryReadMainPackageMetadata(out InstallerPackageMetadata metadata, out error))
                return false;

            InstallerDependencyMetadata[] unityDependencies = metadata.unityDependencies
                ?? Array.Empty<InstallerDependencyMetadata>();
            InstallerDependencyMetadata[] gitDependencies = metadata.gitDependencies
                ?? Array.Empty<InstallerDependencyMetadata>();

            List<string> duplicateUnityIds = unityDependencies
                .Where(dependency => dependency != null && !string.IsNullOrWhiteSpace(dependency.packageId))
                .GroupBy(dependency => dependency.packageId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicateUnityIds.Count > 0)
            {
                error = "package.json 存在重复 Unity 依赖：" + string.Join("、", duplicateUnityIds);
                return false;
            }

            for (int i = 0; i < RequiredUnityDependencyPackageIds.Length; i++)
            {
                string packageId = RequiredUnityDependencyPackageIds[i];
                InstallerDependencyMetadata dependency = unityDependencies.FirstOrDefault(candidate =>
                    candidate != null
                    && string.Equals(candidate.packageId?.Trim(), packageId, StringComparison.OrdinalIgnoreCase));
                if (dependency == null || !dependency.isRequired)
                {
                    error = "package.json 缺少必需 Unity 依赖或未标记为必需：" + packageId;
                    return false;
                }
            }

            for (int i = 0; i < RequiredGitDependencyCheckClasses.Length; i++)
            {
                string checkClass = RequiredGitDependencyCheckClasses[i];
                InstallerDependencyMetadata dependency = gitDependencies.FirstOrDefault(candidate =>
                    candidate != null
                    && string.Equals(candidate.checkClass?.Trim(), checkClass, StringComparison.Ordinal));
                if (dependency == null || !dependency.isRequired)
                {
                    error = "package.json 缺少必需 Git 依赖或未标记为必需：" + checkClass;
                    return false;
                }
                if (!IsPinnedGitUrl(dependency.gitUrl))
                {
                    error = "package.json 的必需 Git 依赖没有固定到完整 commit：" + dependency.name;
                    return false;
                }
            }

            InstallerDependencyMetadata whisper = gitDependencies.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(candidate.checkClass?.Trim(), "Whisper.WhisperManager", StringComparison.Ordinal));
            if (whisper != null && whisper.isRequired)
            {
                error = "Whisper 未进入主包源码，不能标记为主包必需依赖。请改为可选扩展。";
                return false;
            }

            return true;
        }

        private static bool IsPinnedGitUrl(string gitUrl)
        {
            string value = gitUrl?.Trim() ?? string.Empty;
            int marker = value.LastIndexOf('#');
            if (marker <= 0 || marker == value.Length - 1)
                return false;

            string commit = value.Substring(marker + 1);
            return (commit.Length == 40 || commit.Length == 64) && commit.All(Uri.IsHexDigit);
        }

        private static bool TryReadMainPackageMetadata(out InstallerPackageMetadata metadata, out string error)
        {
            metadata = null;
            error = string.Empty;
            try
            {
                string metadataPath = Path.Combine(
                    ProjectRootPath,
                    "Assets",
                    "Plugins",
                    "ES",
                    "Editor",
                    "Installer",
                    "Downloads",
                    "Main",
                    "package.json");
                if (!File.Exists(metadataPath))
                {
                    error = "旧 ESInstaller 主包元数据不存在：" + metadataPath;
                    return false;
                }

                metadata = JsonUtility.FromJson<InstallerPackageMetadata>(
                    File.ReadAllText(metadataPath, System.Text.Encoding.UTF8));
                if (metadata == null)
                {
                    error = "package.json 无法反序列化。";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = "读取旧 ESInstaller 主包版本失败：" + exception.Message;
                return false;
            }
        }

        private static bool TryReadMainPackageVersion(out string version, out string error)
        {
            version = string.Empty;
            if (!TryReadMainPackageMetadata(out InstallerPackageMetadata metadata, out error))
                return false;

            version = metadata.version?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(version)
                || version.Length > 64
                || version.Any(character =>
                    !char.IsLetterOrDigit(character)
                    && character != '.'
                    && character != '_'
                    && character != '-'))
            {
                error = "package.json 中的 version 为空或包含非法字符。";
                return false;
            }

            return true;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024L)
                return bytes + " B";
            if (bytes < 1024L * 1024L)
                return (bytes / 1024d).ToString("F1") + " KiB";
            if (bytes < 1024L * 1024L * 1024L)
                return (bytes / (1024d * 1024d)).ToString("F1") + " MiB";
            return (bytes / (1024d * 1024d * 1024d)).ToString("F2") + " GiB";
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "ESPackage";

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalidChar, '_');

            fileName = fileName.Trim();
            return string.IsNullOrEmpty(fileName) ? "ESPackage" : fileName;
        }

        private static string ProjectRootPath
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        private static string[] ManagedPackageOutputRoots
        {
            get
            {
                string projectRoot = ProjectRootPath;
                return new[]
                {
                    Path.Combine(projectRoot, "ES", "Output", "UnityPackages"),
                    Path.Combine(projectRoot, "Assets", "Plugins", "ES", "Editor", "Installer", "Downloads", "Main")
                };
            }
        }

        private static bool TryResolveManagedPackageOutputDirectory(string configuredPath, bool createDirectory, out string normalizedDirectory, out string error)
        {
            normalizedDirectory = null;
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(configuredPath))
                {
                    error = "输出目录不能为空。";
                    return false;
                }

                string raw = configuredPath.Trim().Replace("\\", "/");
                string candidate = Path.IsPathRooted(raw)
                    ? raw
                    : Path.Combine(ProjectRootPath, raw);
                candidate = ESManagedFileIO.NormalizeFullPath(candidate);

                string matchedRoot = null;
                foreach (string root in ManagedPackageOutputRoots)
                {
                    string normalizedRoot = ESManagedFileIO.NormalizeFullPath(root);
                    if (ESManagedFileIO.IsWithinRoot(candidate, normalizedRoot))
                    {
                        matchedRoot = normalizedRoot;
                        break;
                    }
                }

                if (matchedRoot == null)
                {
                    error = "UnityPackage 只能写入 ES/Output/UnityPackages 或 ES Installer Downloads/Main 受管目录。";
                    return false;
                }

                if (ESManagedFileIO.ContainsExistingReparsePoint(candidate))
                {
                    error = "输出目录不能穿过 junction/symlink/reparse point。";
                    return false;
                }

                if (createDirectory)
                    Directory.CreateDirectory(candidate);

                if (ESManagedFileIO.ContainsExistingReparsePoint(candidate))
                {
                    error = "输出目录创建后变成了重解析路径，已拒绝写入。";
                    return false;
                }

                normalizedDirectory = candidate;
                return true;
            }
            catch (Exception exception)
            {
                error = "输出目录无效：" + exception.Message;
                return false;
            }
        }

        private static string BuildUniqueStagingPackagePath(string outputDirectory, string finalPath)
        {
            string stagingPath;
            do
            {
                string baseName = Path.GetFileNameWithoutExtension(finalPath);
                stagingPath = Path.Combine(outputDirectory, "." + baseName + ".staging-" + Guid.NewGuid().ToString("N") + ".unitypackage");
            }
            while (File.Exists(stagingPath));
            return stagingPath;
        }

        private static void PromoteExportedPackage(string stagingPath, string finalPath, string allowedRoot)
        {
            try
            {
                ESManagedFileIO.EnsurePath(stagingPath, true, allowedRoot);
                if (!File.Exists(stagingPath) || new FileInfo(stagingPath).Length <= 0)
                    throw new InvalidDataException("UnityPackage 暂存产物为空或不存在。");

                ESManagedFileIO.EnsurePath(finalPath, false, allowedRoot);
                if (File.Exists(finalPath))
                    throw new IOException("目标 UnityPackage 已存在，拒绝覆盖：" + finalPath);

                File.Move(stagingPath, finalPath);
                ESManagedFileIO.EnsurePath(finalPath, true, allowedRoot);
                if (!File.Exists(finalPath) || new FileInfo(finalPath).Length <= 0)
                    throw new InvalidDataException("UnityPackage 提升后校验失败。");
            }
            finally
            {
                try
                {
                    if (File.Exists(stagingPath))
                        File.Delete(stagingPath);
                }
                catch
                {
                    // 保留原始异常；残留暂存文件会在下一次受管门禁中被发现。
                }
            }
        }

        private static string GetDependencyInclusionText(bool includeDependencies)
        {
            return includeDependencies ? "包含" : "不包含";
        }

        private List<string> GetValidSelectedAssetPaths(UnityEngine.Object[] selected)
        {
            if (selected == null || selected.Length == 0)
                return new List<string>();

            return selected
                .Select(obj => SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(obj)))
                .Where(path => SimpleToolsSafetyUtility.IsAssetPath(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

    #endregion
    }

    /// <summary>
    /// 简单的编辑器输入对话框
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private new string title;
        private string message;
        private string inputValue = "";
        private string defaultValue = "";
        private System.Action<string> onConfirm;
        private bool isInitialized = false;

        public static void Show(string title, string message, string defaultValue, System.Action<string> onConfirm)
        {
            var window = GetWindow<EditorInputDialog>(true, title, true);
            window.title = title;
            window.message = message;
            window.defaultValue = defaultValue;
            window.inputValue = defaultValue;
            window.onConfirm = onConfirm;
            window.isInitialized = true;
            window.minSize = new Vector2(300, 120);
            window.maxSize = new Vector2(300, 120);
            window.ShowModal();
        }

        public static string Show(string title, string message, string defaultValue = "")
        {
            string result = null;
            Show(title, message, defaultValue, (value) => result = value);
            return result;
        }

        private void OnGUI()
        {
            if (!isInitialized) return;

            GUILayout.Label(message, EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);

            GUI.SetNextControlName("InputField");
            inputValue = EditorGUILayout.TextField("输入:", inputValue);

            // 自动聚焦到输入框
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl("InputField");
            }

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("确定", GUILayout.Width(80), GUILayout.Height(24)))
            {
                onConfirm?.Invoke(inputValue);
                Close();
            }
            if (GUILayout.Button("取消", GUILayout.Width(80), GUILayout.Height(24)))
            {
                onConfirm?.Invoke(null);
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
