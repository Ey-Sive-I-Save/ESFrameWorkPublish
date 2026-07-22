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
    public class Page_UnityPackageTool : ESWindowPageBase
    {

        [Title("UnityPackage打包工具", "支持多个打包配置管理", bold: true, titleAlignment: TitleAlignments.Centered)]

        [HideInInspector]
        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "选择或创建打包配置，\n设置要打包的资源，\n点击打包按钮生成UnityPackage";

        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel, PropertyOrder(-10)]
        private string PanelSummary
        {
            get
            {
                string configName = currentConfigIndex == -1 ? "默认配置" : GetExtensionConfigName();
                int collectCount = SelectedAssets != null ? SelectedAssets.Count : 0;
                return $"配置: {configName} | 包名: {PackageName} | 收集路径: {collectCount} 个 | 输出: {ExportPath} | 包含依赖: {(IncludeDependencies ? "是" : "否")}";
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
        private string cachedPackagePreviewSignature = "";
        private bool cachedPackagePreviewConfigValid;
        private string cachedPackagePreviewConfigName = "";
        private string cachedPackagePreviewOutputPath = "";
        private string cachedPackagePreviewPackageName = "";
        private bool cachedPackagePreviewIncludeDependencies;

        [OnInspectorGUI, PropertyOrder(-200)]
        private void DrawResultPanel()
        {
            var previewPaths = EnsurePackagePreviewCache(false, out var configName, out var outputPath, out var packageName, out var includeDependencies, out var configValid);
            SimpleToolsPanelUtility.DrawToolHeader(
                "UnityPackage 打包工作台",
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
            SimpleToolsPanelUtility.DrawSectionTitle("执行操作", "先刷新/确认资源清单，再选择普通导出或发布打包。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("使用当前选中资源", SimpleToolsActionTone.Neutral, 30, GUILayout.MinWidth(120)))
                        GetSelectedAssets();
                    if (SimpleToolsPanelUtility.DrawActionButton("应用到全局设置", SimpleToolsActionTone.Warning, 30, GUILayout.MinWidth(120)))
                        ApplyToGlobalConfig();
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = configValid && previewCount > 0;
                    if (SimpleToolsPanelUtility.DrawActionButton("导出 UnityPackage", SimpleToolsActionTone.Primary, 34, GUILayout.MinWidth(150)))
                        ExportPackage();
                    if (SimpleToolsPanelUtility.DrawActionButton("发布打包", SimpleToolsActionTone.Success, 34, GUILayout.MinWidth(110)))
                        PublishPackage();
                    GUI.enabled = true;
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawPackagePreviewPanel(List<string> previewPaths, string configName, string outputPath, string packageName, bool includeDependencies)
        {
            SimpleToolsPanelUtility.DrawSectionTitle("导出预览", "按资源路径搜索；开始打包前先确认展开后的真实资源清单。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string finalOutputPath = Path.Combine(outputPath ?? string.Empty, SanitizeFileName(packageName) + ".unitypackage").Replace("\\", "/");
                EditorGUILayout.LabelField($"配置: {configName} | 资源: {previewPaths.Count} | 依赖: {(includeDependencies ? "包含" : "不包含")}", EditorStyles.wordWrappedMiniLabel);
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
                if (rows.Count == 0)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("当前配置没有可导出的资源，或搜索条件没有命中。");
                    return;
                }

                foreach (string path in SimpleToolsPanelUtility.PageItems(rows, ref packagePreviewPageIndex, PackagePreviewPageSize, out _))
                {
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
            return paths.Where(path => path != null && path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        private List<string> BuildExpandedPackageAssetPaths(out string configName, out string outputPath, out string packageName, out bool includeDependencies)
        {
            List<string> selectedAssets;
            if (!ResolveCurrentPackageConfig(out selectedAssets, out outputPath, out packageName, out includeDependencies, out configName))
                return new List<string>();
            return ExpandPackageAssetPaths(selectedAssets);
        }

        private List<string> EnsurePackagePreviewCache(bool forceRefresh, out string configName, out string outputPath, out string packageName, out bool includeDependencies, out bool configValid)
        {
            string signature = BuildPackagePreviewSignature(out configValid, out configName, out outputPath, out packageName, out includeDependencies);
            if (forceRefresh || signature != cachedPackagePreviewSignature)
            {
                cachedPackagePreviewPaths.Clear();
                if (configValid)
                    cachedPackagePreviewPaths.AddRange(BuildExpandedPackageAssetPaths(out configName, out outputPath, out packageName, out includeDependencies));

                cachedPackagePreviewSignature = signature;
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
            return new List<string>(cachedPackagePreviewPaths);
        }

        private string BuildPackagePreviewSignature(out bool configValid, out string configName, out string outputPath, out string packageName, out bool includeDependencies)
        {
            configValid = ResolveCurrentPackageConfig(out var selectedAssets, out outputPath, out packageName, out includeDependencies, out configName);
            string pathPart = selectedAssets == null ? "<null>" : string.Join("|", selectedAssets.Select(SimpleToolsSafetyUtility.NormalizeAssetPath));
            return $"{currentConfigIndex}|{configValid}|{configName}|{outputPath}|{packageName}|{includeDependencies}|{pathPart}";
        }

        private bool ResolveCurrentPackageConfig(out List<string> selectedAssets, out string outputPath, out string packageName, out bool includeDependencies, out string configName)
        {
            selectedAssets = null;
            outputPath = "Assets/../ESOutput/UnityPackage";
            packageName = "ESPackage";
            includeDependencies = true;
            configName = "默认配置";

            if (currentConfigIndex == -1)
            {
                var globalConfig = ESGlobalEditorDefaultConfi.Instance;
                if (globalConfig == null)
                    return false;

                selectedAssets = globalConfig.PackageCollectPath;
                outputPath = globalConfig.PackageSelfPathForMain ?? "Assets/../ESOutput/UnityPackage";
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

        private List<string> ExpandPackageAssetPaths(IEnumerable<string> selectedAssets)
        {
            var expandedPaths = new List<string>();
            var expandedPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                        if (CanExportPackageAsset(assetPath) && expandedPathSet.Add(assetPath))
                            expandedPaths.Add(assetPath);
                    }
                }
                else if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalizedPath) != null)
                {
                    if (CanExportPackageAsset(normalizedPath) && expandedPathSet.Add(normalizedPath))
                        expandedPaths.Add(normalizedPath);
                }
            }

            expandedPaths.Sort(StringComparer.OrdinalIgnoreCase);
            return expandedPaths;
        }

        private static bool CanExportPackageAsset(string assetPath)
        {
            if (!SimpleToolsSafetyUtility.IsAssetPath(assetPath))
                return false;

            return !assetPath.StartsWith("Assets/Plugins/ES/Editor/Installer/", StringComparison.OrdinalIgnoreCase) &&
                   !assetPath.Equals("Assets/Plugins/ES/Editor/Installer", StringComparison.OrdinalIgnoreCase);
        }

        #region 配置管理

        [LabelText("当前配置"), ValueDropdown("GetConfigNames"), Space(10)]
        [OnValueChanged("OnConfigChanged")]
        public int currentConfigIndex = -1; // -1 表示默认配置，0+ 表示扩展配置

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

        [HorizontalGroup("ConfigButtons", 0.5f), Button("新建配置", ButtonHeight = 25), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void CreateNewConfig()
        {
            var globalConfigs = GlobalPackageConfigs;
            RecordGlobalConfigUndo("新建UnityPackage配置");
            var newConfig = new ESGlobalEditorDefaultConfi.UnityPackageConfig
            {
                ConfigName = $"配置 {globalConfigs.Count + 1}",
                OutputPath = ESGlobalEditorDefaultConfi.Instance?.PackageSelfPathForMain ?? "Assets/../ESOutput/UnityPackage",
                PackageName = $"ESPackage_Ext_{globalConfigs.Count + 1}_",
                CollectPaths = new List<string>(ESGlobalEditorDefaultConfi.Instance?.PackageCollectPath ?? new List<string>() { "Assets/Plugins/ES" }),
                ExcludeFolders = new List<string>(),
                IsEnabled = true
            };

            // 从当前设置复制初始值
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

        [HorizontalGroup("ConfigButtons"), Button("保存配置", ButtonHeight = 25), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
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

        [HorizontalGroup("ConfigButtons"), Button("删除配置", ButtonHeight = 25), GUIColor("@ESDesignUtility.ColorSelector.Color_06")]
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

        [HorizontalGroup("ConfigButtons"), Button("重命名", ButtonHeight = 25), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
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
                    return ESGlobalEditorDefaultConfi.Instance?.PackageSelfPathForMain ?? "Assets/../ESOutput/UnityPackage";
                }
                else
                {
                    // 使用扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                        return globalConfigs[currentConfigIndex].OutputPath;
                    return "Assets/../ESOutput/UnityPackage";
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

        [ShowInInspector, LabelText("选中的资源路径"), FolderPath, ListDrawerSettings(DraggableItems = false)]
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
            // 初始化全局配置列表
            var globalConfigs = GlobalPackageConfigs;
            if (globalConfigs == null || globalConfigs.Count == 0)
            {
                // 如果全局配置为空，创建一个默认配置
                var defaultConfig = new ESGlobalEditorDefaultConfi.UnityPackageConfig
                {
                    ConfigName = "默认配置",
                    OutputPath = ESGlobalEditorDefaultConfi.Instance?.PackageSelfPathForMain ?? "Assets/../ESOutput/UnityPackage",
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

            // 获取当前选中的资源
            var selected = Selection.objects;

            // 处理默认配置的情况
            if (currentConfigIndex == -1)
            {
                // 对于默认配置，直接使用全局配置的值
                var globalConfig = ESGlobalEditorDefaultConfi.Instance;
                if (globalConfig != null)
                {
                    // 只有当没有手动设置资源时，才自动更新选中的资源
                    if (globalConfig.PackageCollectPath == null || globalConfig.PackageCollectPath.Count == 0)
                    {
                        globalConfig.PackageCollectPath = new List<string>();

                        // 1. 添加当前选中的资源
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
                                // 直接添加文件夹路径
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

                // 只有当没有手动设置资源时，才自动更新选中的资源
                if (currentConfig.CollectPaths == null || currentConfig.CollectPaths.Count == 0)
                {
                    currentConfig.CollectPaths = new List<string>();

                    // 1. 添加当前选中的资源
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
                                // 直接添加文件夹路径
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
                if (string.IsNullOrWhiteSpace(currentConfig.OutputPath) || currentConfig.OutputPath == "Assets/../ESOutput/UnityPackage")
                {
                    try
                    {
                        currentConfig.OutputPath = ESGlobalEditorDefaultConfi.Instance?.PackageSelfPathForMain ?? currentConfig.OutputPath;
                    }
                    catch
                    {
                        // 如果配置不存在或访问失败，保持当前值
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
                        // 如果配置不存在或访问失败，保持当前值
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
            // 合并收集路径，去重
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

            var expandedPaths = ExpandPackageAssetPaths(selectedAssets);
            var assetPaths = expandedPaths.ToArray();
            if (assetPaths.Length == 0)
            {
                EditorUtility.DisplayDialog("没有可导出的资源", "收集路径展开后没有找到可导出的有效资源。", "知道了");
                return;
            }

            var finalOutputPath = Path.Combine(outputPath, SanitizeFileName(packageName) + ".unitypackage");

            string exportPreview = SimpleToolsSafetyUtility.JoinPreview(assetPaths, 12);
            if (!SimpleToolsPanelUtility.ConfirmHeavyOperation(
                "确认导出 UnityPackage",
                assetPaths.Length,
                $"配置：{configName}\n输出：{finalOutputPath}\n包含依赖：{(includeDependencies ? "是" : "否")}\n\n{exportPreview}",
                "会把展开后的资源写入 unitypackage 文件。包含依赖时包体可能明显变大，请确认没有把临时文件或内部工具打进去。"))
                return;

            try
            {
                // 确保导出目录存在
                Directory.CreateDirectory(outputPath);

                AssetDatabase.ExportPackage(assetPaths, finalOutputPath,
                    includeDependencies ? ExportPackageOptions.IncludeDependencies : ExportPackageOptions.Default);

                lastResultSummary = $"打包完成: {assetPaths.Length} 个资源 | 配置 {configName} | 依赖 {(includeDependencies ? "包含" : "不包含")}";
                lastResultDetail = $"输出文件:\n{finalOutputPath}\n\n资源预览:\n" + SimpleToolsSafetyUtility.JoinPreview(assetPaths, 12);
                EditorUtility.DisplayDialog("成功", $"Package导出成功！\n配置: {configName}\n路径: {finalOutputPath}", "确定");
                EditorUtility.RevealInFinder(finalOutputPath);
            }
            catch (System.Exception e)
            {
                lastResultSummary = $"打包失败: 配置 {configName} | 资源 {assetPaths.Length} 个";
                lastResultDetail = e.Message;
                EditorUtility.DisplayDialog("错误", $"导出失败: {e.Message}", "确定");
            }
        }

        public void PublishPackage()
        {
            var config = ESGlobalEditorDefaultConfi.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到全局配置对象！", "确定");
                return;
            }

            // 使用PackagePublishPath作为构建路径
            var publishPath = config.PackagePublishPath;
            if (string.IsNullOrEmpty(publishPath))
            {
                EditorUtility.DisplayDialog("错误", "发布路径未设置！", "确定");
                return;
            }

            // 检查路径是否存在
            if (!AssetDatabase.IsValidFolder(publishPath))
            {
                EditorUtility.DisplayDialog("错误", $"发布路径不存在: {publishPath}", "确定");
                return;
            }

            // 收集发布路径下的所有资源
            var guids = AssetDatabase.FindAssets("", new[] { publishPath });
            var assetPaths = new List<string>();
            var assetPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var guid in guids)
            {
                var assetPath = SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (CanExportPackageAsset(assetPath) && assetPathSet.Add(assetPath))
                    assetPaths.Add(assetPath);
            }

            if (assetPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "发布路径下没有找到任何资源！", "确定");
                return;
            }

            // 使用PackageOutputPath作为输出目录
            var outputDir = config.PackageOutputPathForPublish;
            if (string.IsNullOrEmpty(outputDir))
            {
                outputDir = "Assets/../ESOutput/UnityPackage";
            }

            // 生成包名（使用全局配置的固定包名 + 时间戳）
            var packageName = config.PackageName;
            if (string.IsNullOrEmpty(packageName))
            {
                packageName = "ESPackage";
            }
            var timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputPath = Path.Combine(outputDir, $"{SanitizeFileName(packageName)}_{timestamp}.unitypackage");

            string publishPreview = SimpleToolsSafetyUtility.JoinPreview(assetPaths, 12);
            if (!SimpleToolsPanelUtility.ConfirmHeavyOperation(
                "确认发布打包",
                assetPaths.Count,
                $"从发布路径导出 {assetPaths.Count} 个资源。\n\n发布路径：{publishPath}\n输出：{outputPath}\n包含依赖：否\n\n{publishPreview}",
                "发布打包不会包含依赖，适合明确发布目录已经完整的情况。请确认发布目录没有临时文件。"))
                return;

            try
            {
                // 确保输出目录存在
                Directory.CreateDirectory(outputDir);

                // 不包含依赖的发布打包
                AssetDatabase.ExportPackage(assetPaths.ToArray(), outputPath, ExportPackageOptions.Default);

                lastResultSummary = $"发布打包完成: {assetPaths.Count} 个资源 | 发布路径 {publishPath}";
                lastResultDetail = $"输出文件:\n{outputPath}\n\n资源预览:\n" + SimpleToolsSafetyUtility.JoinPreview(assetPaths, 12);
                EditorUtility.DisplayDialog("成功", $"发布包导出成功！\n路径: {outputPath}", "确定");
                EditorUtility.RevealInFinder(outputPath);
            }
            catch (System.Exception e)
            {
                lastResultSummary = $"发布打包失败: 发布路径 {publishPath}";
                lastResultDetail = e.Message;
                EditorUtility.DisplayDialog("错误", $"发布打包失败: {e.Message}", "确定");
            }
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
            if (GUILayout.Button("确定"))
            {
                onConfirm?.Invoke(inputValue);
                Close();
            }
            if (GUILayout.Button("取消"))
            {
                onConfirm?.Invoke(null);
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
