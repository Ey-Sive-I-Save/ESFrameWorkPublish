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

        [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "选择或创建打包配置，\n设置要打包的资源，\n点击打包按钮生成UnityPackage";

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

        [HorizontalGroup("ConfigButtons", 0.5f), Button("新建配置", ButtonHeight = 25), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void CreateNewConfig()
        {
            var globalConfigs = GlobalPackageConfigs;
            var newConfig = new ESGlobalEditorDefaultConfi.UnityPackageConfig
            {
                ConfigName = $"配置 {globalConfigs.Count + 1}",
                OutputPath = ESGlobalEditorDefaultConfi.Instance?.PackageOutputPath ?? "Assets/../ESOutput/UnityPackage",
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
#endif
                EditorUtility.DisplayDialog("成功", "配置已保存！", "确定");
            }
        }

        [HorizontalGroup("ConfigButtons"), Button("删除配置", ButtonHeight = 25), GUIColor("@ESDesignUtility.ColorSelector.Color_06")]
        public void DeleteCurrentConfig()
        {
            var globalConfigs = GlobalPackageConfigs;
            if (globalConfigs.Count <= 1)
            {
                EditorUtility.DisplayDialog("错误", "至少需要保留一个配置！", "确定");
                return;
            }

            if (EditorUtility.DisplayDialog("确认删除", $"确定要删除配置 '{globalConfigs[currentConfigIndex].ConfigName}' 吗？", "删除", "取消"))
            {
                globalConfigs.RemoveAt(currentConfigIndex);
                if (currentConfigIndex >= globalConfigs.Count)
                    currentConfigIndex = globalConfigs.Count - 1;

                // 标记全局配置为已修改
#if UNITY_EDITOR
                if (ESGlobalEditorDefaultConfi.Instance != null)
                    EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
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
                        config.ConfigName = newName;
                        // 标记全局配置为已修改
#if UNITY_EDITOR
                        if (ESGlobalEditorDefaultConfi.Instance != null)
                            EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
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
                    return ESGlobalEditorDefaultConfi.Instance?.PackageOutputPath ?? "Assets/../ESOutput/UnityPackage";
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
                        ESGlobalEditorDefaultConfi.Instance.PackageOutputPath = value;
                        EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
                    }
                }
                else
                {
                    // 修改扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                    {
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
                    return ESGlobalEditorDefaultConfi.Instance?.IncludeDependencies ?? true;
                }
                else
                {
                    // 使用扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                        return globalConfigs[currentConfigIndex].IncludeDependencies;
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
                        ESGlobalEditorDefaultConfi.Instance.IncludeDependencies = value;
                        EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
                    }
                }
                else
                {
                    // 修改扩展配置
                    var globalConfigs = GlobalPackageConfigs;
                    if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                    {
                        globalConfigs[currentConfigIndex].IncludeDependencies = value;
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
                    OutputPath = ESGlobalEditorDefaultConfi.Instance?.PackageOutputPath ?? "Assets/../ESOutput/UnityPackage",
                    PackageName = ESGlobalEditorDefaultConfi.Instance?.PackageName ?? "ESPackage0.35_",
                    CollectPaths = new List<string>(ESGlobalEditorDefaultConfi.Instance?.PackageCollectPath ?? new List<string>() { "Assets/Plugins/ES" }),
                    ExcludeFolders = new List<string>(),
                    IsEnabled = true,
                    IncludeDependencies = ESGlobalEditorDefaultConfi.Instance?.IncludeDependencies ?? true
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
                            globalConfig.PackageCollectPath.AddRange(selected.Select(obj => AssetDatabase.GetAssetPath(obj)));
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
                        currentConfig.CollectPaths.AddRange(selected.Select(obj => AssetDatabase.GetAssetPath(obj)));
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
                        currentConfig.OutputPath = ESGlobalEditorDefaultConfi.Instance?.PackageSelfPath ?? currentConfig.OutputPath;
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

        [Button("仅获取选中资源", ButtonHeight = 15), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void GetSelectedAssets()
        {
            // 只获取当前选中的资源，不自动加入PackageCollectPath内容
            if (currentConfigIndex == -1)
            {
                // 处理默认配置
                var globalConfig = ESGlobalEditorDefaultConfi.Instance;
                if (globalConfig != null)
                {
                    globalConfig.PackageCollectPath = new List<string>();
                    var selected = Selection.objects;
                    if (selected != null && selected.Length > 0)
                    {
                        globalConfig.PackageCollectPath.AddRange(selected.Select(obj => AssetDatabase.GetAssetPath(obj)));
                    }

                    // 标记全局配置为已修改
#if UNITY_EDITOR
                    EditorUtility.SetDirty(globalConfig);
#endif
                }
            }
            else
            {
                // 处理扩展配置
                var globalConfigs = GlobalPackageConfigs;
                if (currentConfigIndex >= 0 && currentConfigIndex < globalConfigs.Count)
                {
                    var currentConfig = globalConfigs[currentConfigIndex];
                    currentConfig.CollectPaths.Clear();
                    var selected = Selection.objects;
                    if (selected != null && selected.Length > 0)
                    {
                        currentConfig.CollectPaths.AddRange(selected.Select(obj => AssetDatabase.GetAssetPath(obj)));
                    }

                    // 标记全局配置为已修改
#if UNITY_EDITOR
                    if (ESGlobalEditorDefaultConfi.Instance != null)
                        EditorUtility.SetDirty(ESGlobalEditorDefaultConfi.Instance);
#endif
                }
            }
        }

        [Button("应用到全局设置", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
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
            config.PackageName = currentConfig.PackageName;
            config.PackageOutputPath = currentConfig.OutputPath;
            config.IncludeDependencies = currentConfig.IncludeDependencies;
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
#endif
            EditorUtility.DisplayDialog("成功", "已将设置应用到全局配置！", "确定");
        }

        [Button("开始打包", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
        public void ExportPackage()
        {
            List<string> selectedAssets;
            string outputPath;
            string packageName;
            bool includeDependencies;
            string configName;

            if (currentConfigIndex == -1)
            {
                // 处理默认配置
                var globalConfig = ESGlobalEditorDefaultConfi.Instance;
                if (globalConfig == null)
                {
                    EditorUtility.DisplayDialog("错误", "未找到全局配置对象！", "确定");
                    return;
                }

                selectedAssets = globalConfig.PackageCollectPath;
                outputPath = globalConfig.PackageOutputPath ?? "Assets/../ESOutput/UnityPackage";
                packageName = globalConfig.PackageName ?? "ESPackage0.35_";
                includeDependencies = globalConfig.IncludeDependencies;
                configName = "默认配置";
            }
            else
            {
                // 处理扩展配置
                var globalConfigs = GlobalPackageConfigs;
                if (currentConfigIndex < 0 || currentConfigIndex >= globalConfigs.Count)
                {
                    EditorUtility.DisplayDialog("错误", "无效的配置索引！", "确定");
                    return;
                }

                var currentConfig = globalConfigs[currentConfigIndex];
                selectedAssets = currentConfig.CollectPaths;
                outputPath = currentConfig.OutputPath;
                packageName = currentConfig.PackageName;
                includeDependencies = currentConfig.IncludeDependencies;
                configName = currentConfig.ConfigName;
            }

            if (selectedAssets == null || selectedAssets.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先设置要打包的资源路径！", "确定");
                return;
            }

            // 将文件夹路径递归展开为所有资源文件路径
            var expandedPaths = new List<string>();
            foreach (var path in selectedAssets)
            {
                if (AssetDatabase.IsValidFolder(path))
                {
                    // 文件夹，递归收集所有资源
                    var guids = AssetDatabase.FindAssets("", new[] { path });
                    foreach (var guid in guids)
                    {
                        var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (!expandedPaths.Contains(assetPath))
                            expandedPaths.Add(assetPath);
                    }
                }
                else if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                {
                    // 单个资源
                    if (!expandedPaths.Contains(path))
                        expandedPaths.Add(path);
                }
            }
            var assetPaths = expandedPaths.ToArray();
            var finalOutputPath = Path.Combine(outputPath, packageName + ".unitypackage");

            // 确保导出目录存在
            Directory.CreateDirectory(outputPath);

            try
            {
                AssetDatabase.ExportPackage(assetPaths, finalOutputPath,
                    includeDependencies ? ExportPackageOptions.IncludeDependencies : ExportPackageOptions.Default);

                EditorUtility.DisplayDialog("成功", $"Package导出成功！\n配置: {configName}\n路径: {finalOutputPath}", "确定");
                EditorUtility.RevealInFinder(finalOutputPath);
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", $"导出失败: {e.Message}", "确定");
            }
        }

        [Button("发布打包", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
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
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPaths.Contains(assetPath))
                    assetPaths.Add(assetPath);
            }

            if (assetPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "发布路径下没有找到任何资源！", "确定");
                return;
            }

            // 使用PackageOutputPath作为输出目录
            var outputDir = config.PackageOutputPath;
            if (string.IsNullOrEmpty(outputDir))
            {
                outputDir = "Assets/../ESOutput/UnityPackage";
            }

            // 确保输出目录存在
            Directory.CreateDirectory(outputDir);

            // 生成包名（使用全局配置的固定包名 + 时间戳）
            var packageName = config.PackageName;
            if (string.IsNullOrEmpty(packageName))
            {
                packageName = "ESPackage";
            }
            var timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputPath = Path.Combine(outputDir, $"{packageName}_{timestamp}.unitypackage");

            try
            {
                // 不包含依赖的发布打包
                AssetDatabase.ExportPackage(assetPaths.ToArray(), outputPath, ExportPackageOptions.Default);

                EditorUtility.DisplayDialog("成功", $"发布包导出成功！\n路径: {outputPath}", "确定");
                EditorUtility.RevealInFinder(outputPath);
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", $"发布打包失败: {e.Message}", "确定");
            }
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