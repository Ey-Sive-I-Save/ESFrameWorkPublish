using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    using Sirenix.OdinInspector;
    using Sirenix.OdinInspector.Editor;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using System;
    using ES;
    using Sirenix.Serialization;
    using System.IO;
    using UnityEngine.UIElements;
    using System.Linq;
    using Sirenix.Utilities;

    namespace ES
    {
        //简单工具窗口
        public class SimpleToolsWindow : ESMenuTreeWindowAB<SimpleToolsWindow>
        {
            [MenuItem("Tools/ES工具/简单工具窗口")]
            public static void TryOpenWindow()
            {
                OpenWindow();
            }

            #region 简单重写
            public override GUIContent ESWindow_GetWindowGUIContent()
            {
                var content = new GUIContent("ES简单工具", "使用ES简单工具完成快速开发和项目管理");
                return content;
            }

            public override void ESWindow_OnOpen()
            {
                base.ESWindow_OnOpen();
                if (UsingWindow.HasDelegate)
                {
                    //已经注册委托
                }
                else
                {
                    UsingWindow.DelegateHandle();
                }
            }

            private void DelegateHandle()
            {
                HasDelegate = true;
            }
            #endregion

            #region 数据滞留与声明
            //根页面名
            public const string PageName_UnityPackageTool = "UnityPackage打包工具";
            public const string PageName_HierarchyTools = "层级工具集";
            public const string PageName_BatchRename = "批量重命名";
            public const string PageName_PhysicsAlign = "物理对齐";
            public const string PageName_BatchStaticSetting = "批量静态设置";
            public const string PageName_TextureSpriteTool = "纹理精灵生成工具";
            public const string PageName_PrefabManagement = "Prefab管理工具";
            public const string PageName_MaterialReplacement = "材质批量替换工具";
            public const string PageName_SceneOptimization = "场景优化工具";
            public const string PageName_AnimationBatchSetting = "动画批量设置工具";
            public const string PageName_ScriptBatchAttachment = "脚本批量挂载工具";
            public const string PageName_AssetReferenceChecker = "资源引用检查工具";
            public const string PageName_LightingSettings = "灯光设置工具";
            public const string PageName_ParticleSystemAdjustment = "粒子系统批量调整工具";

            [NonSerialized] public Page_UnityPackageTool pageUnityPackageTool;
            [NonSerialized] public Page_HierarchyTools pageHierarchyTools;
            [NonSerialized] public Page_BatchRename pageBatchRename;
            [NonSerialized] public Page_PhysicsAlign pagePhysicsAlign;
            [NonSerialized] public Page_BatchStaticSetting pageBatchStaticSetting;
            [NonSerialized] public Page_TextureSpriteTool pageTextureSpriteTool;
            [NonSerialized] public Page_PrefabManagement pagePrefabManagement;
            [NonSerialized] public Page_MaterialReplacement pageMaterialReplacement;
            [NonSerialized] public Page_SceneOptimization pageSceneOptimization;
            [NonSerialized] public Page_AnimationBatchSetting pageAnimationBatchSetting;
            [NonSerialized] public Page_ScriptBatchAttachment pageScriptBatchAttachment;
            [NonSerialized] public Page_AssetReferenceChecker pageAssetReferenceChecker;
            [NonSerialized] public Page_LightingSettings pageLightingSettings;
            [NonSerialized] public Page_ParticleSystemAdjustment pageParticleSystemAdjustment;

            private bool HasDelegate = false;
            #endregion

            #region 缓冲刷新和加载保存
            //缓冲回执
            protected override void OnImGUI()
            {
                if (UsingWindow == null)
                {
                    UsingWindow = this;
                    ES_LoadData();
                }
                if (UsingWindow != null)
                {

                }
                base.OnImGUI();
            }

            /// <summary>
            /// 刷新窗口
            /// </summary>
            public override void ESWindow_RefreshWindow()
            {
                base.ESWindow_RefreshWindow();
                ES_SaveData();
            }

            public override void ES_LoadData()
            {
                // 加载数据逻辑
            }

            public override void ES_SaveData()
            {
                // 保存数据逻辑
            }
            #endregion

            protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
            {
                base.ES_OnBuildMenuTree(tree);
                {
                    //独立功能块
                    Part_BuildUnityPackageTool(tree);
                    Part_BuildHierarchyTools(tree);
                    Part_BuildTextureSpriteTool(tree);
                    Part_BuildPrefabManagement(tree);
                    Part_BuildMaterialReplacement(tree);
                    Part_BuildSceneOptimization(tree);
                    Part_BuildAnimationBatchSetting(tree);
                    Part_BuildScriptBatchAttachment(tree);
                    Part_BuildAssetReferenceChecker(tree);
                    Part_BuildLightingSettings(tree);
                    Part_BuildParticleSystemAdjustment(tree);
                }
                ES_LoadData();
            }

            #region 页面构建方法
            private void Part_BuildUnityPackageTool(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_UnityPackageTool, ref pageUnityPackageTool, SdfIconType.Archive);
            }

            private void Part_BuildHierarchyTools(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_HierarchyTools, ref pageHierarchyTools, SdfIconType.LayerForward);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_BatchRename, ref pageBatchRename, SdfIconType.Pencil);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_PhysicsAlign, ref pagePhysicsAlign, SdfIconType.Grid);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_BatchStaticSetting, ref pageBatchStaticSetting, SdfIconType.ToggleOn);
            }

            private void Part_BuildTextureSpriteTool(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_TextureSpriteTool, ref pageTextureSpriteTool, SdfIconType.Image);
            }

            private void Part_BuildPrefabManagement(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_PrefabManagement, ref pagePrefabManagement, SdfIconType.Box);
            }

            private void Part_BuildMaterialReplacement(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_MaterialReplacement, ref pageMaterialReplacement, SdfIconType.Palette);
            }

            private void Part_BuildSceneOptimization(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_SceneOptimization, ref pageSceneOptimization, SdfIconType.Speedometer);
            }

            private void Part_BuildAnimationBatchSetting(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_AnimationBatchSetting, ref pageAnimationBatchSetting, SdfIconType.Play);
            }

            private void Part_BuildScriptBatchAttachment(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_ScriptBatchAttachment, ref pageScriptBatchAttachment, SdfIconType.CodeSlash);
            }

            private void Part_BuildAssetReferenceChecker(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_AssetReferenceChecker, ref pageAssetReferenceChecker, SdfIconType.Search);
            }

            private void Part_BuildLightingSettings(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_LightingSettings, ref pageLightingSettings, SdfIconType.Lightbulb);
            }

            private void Part_BuildParticleSystemAdjustment(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_ParticleSystemAdjustment, ref pageParticleSystemAdjustment, SdfIconType.Stars);
            }
            #endregion
        }

        #region UnityPackage打包工具
        [Serializable]
        public class Page_UnityPackageTool : ESWindowPageBase
        {

            [Title("UnityPackage打包工具", "快速打包选中的资源为UnityPackage", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择要打包的资源文件夹或文件，\n设置包名和导出路径，\n点击打包按钮生成UnityPackage";

            [LabelText("包名"), Space(5)]
            public string packageName = "MyPackage";

            [LabelText("导出路径"), FolderPath, Space(5)]
            public string exportPath = "Assets/../Export";

            [LabelText("包含依赖"), Space(5)]
            public bool includeDependencies = true;

            [LabelText("选中的资源路径"), FolderPath, ListDrawerSettings(DraggableItems = false), Space(5)]
            public List<string> selectedAssets = new List<string>();

            public override ESWindowPageBase ES_Refresh()
            {
                // 获取当前选中的资源
                var selected = Selection.objects;
                selectedAssets.Clear();
                // 1. 添加当前选中的资源
                if (selected != null && selected.Length > 0)
                {
                    selectedAssets.AddRange(selected.Select(obj => AssetDatabase.GetAssetPath(obj)));
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
                            if (!selectedAssets.Contains(path))
                                selectedAssets.Add(path);
                        }
                        else if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                        {
                            // 单个资源
                            if (!selectedAssets.Contains(path))
                                selectedAssets.Add(path);
                        }
                    }
                }

                // 初始化时,使用 ESGlobalEditorDefaultConfi 里的默认路径和包名（仅当用户未修改时）
                if (string.IsNullOrWhiteSpace(exportPath) || exportPath == "Assets/../Export")
                {
                    try
                    {
                        exportPath = ESGlobalEditorDefaultConfi.Instance?.PackageOutputPath ?? exportPath;
                    }
                    catch
                    {
                        // 如果配置不存在或访问失败，保持当前值
                    }
                }

                if (string.IsNullOrWhiteSpace(packageName) || packageName == "MyPackage")
                {
                    try
                    {
                        var defaultName = ESGlobalEditorDefaultConfi.Instance?.PackageName;
                        if (!string.IsNullOrWhiteSpace(defaultName))
                            packageName = defaultName;
                    }
                    catch
                    {
                        // 如果配置不存在或访问失败，保持当前值
                    }
                }

                return base.ES_Refresh();
            }

            [Button("仅获取选中资源", ButtonHeight = 15), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void GetSelectedAssets()
            {
                // 只获取当前选中的资源，不自动加入PackageCollectPath内容
                selectedAssets.Clear();
                var selected = Selection.objects;
                if (selected != null && selected.Length > 0)
                {
                    selectedAssets.AddRange(selected.Select(obj => AssetDatabase.GetAssetPath(obj)));
                }
            }

            [Button("应用到全局设置", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
            public void ApplyToGlobalConfig()
            {
                var config = ESGlobalEditorDefaultConfi.Instance;
                if (config == null)
                {
                    EditorUtility.DisplayDialog("错误", "未找到全局配置对象！", "确定");
                    return;
                }
                config.PackageName = packageName;
                config.PackageOutputPath = exportPath;
                // 合并收集路径，去重
                var allPaths = new HashSet<string>(config.PackageCollectPath ?? new List<string>());
                foreach (var path in selectedAssets)
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
                var outputPath = Path.Combine(exportPath, packageName + ".unitypackage");

                // 确保导出目录存在
                Directory.CreateDirectory(exportPath);

                try
                {
                    AssetDatabase.ExportPackage(assetPaths, outputPath,
                        includeDependencies ? ExportPackageOptions.IncludeDependencies : ExportPackageOptions.Default);

                    EditorUtility.DisplayDialog("成功", $"Package导出成功！\n路径: {outputPath}", "确定");
                    EditorUtility.RevealInFinder(outputPath);
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("错误", $"导出失败: {e.Message}", "确定");
                }
            }

        #endregion

            // 辅助：安全地按名称设置 StaticEditorFlags（如果枚举成员存在）
            private static void SetFlagByNameIfExists(ref StaticEditorFlags flags, string flagName, bool enable)
            {
                try
                {
                    var type = typeof(StaticEditorFlags);
                    var field = type.GetField(flagName);
                    if (field != null)
                    {
                        var value = (StaticEditorFlags)field.GetValue(null);
                        if (enable)
                            flags |= value;
                        else
                            flags &= ~value;
                    }
                    else
                    {
                        // 在没有该字段的Unity版本上，尝试兼容处理或静默忽略
                    }
                }
                catch
                {
                    // 反射可能在受限的环境失败，静默忽略以保持工具稳定
                }
            }
        }

        #region 层级工具集介绍
        [Serializable]
        public class Page_HierarchyTools : ESWindowPageBase
        {
            [Title("层级工具集介绍", "快速入门指南", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "层级工具集包含以下功能：\n\n1. 批量重命名：批量修改选中GameObject的名称，支持前缀、后缀、替换和编号模式。\n\n2. 物理对齐：对齐多个GameObject的位置，支持各种对齐方式和间距设置。\n\n3. 批量静态设置：批量设置GameObject的静态标记，用于优化渲染和导航。";
        }
        #endregion

        #region 批量重命名工具
        [Serializable]
        public class Page_BatchRename : ESWindowPageBase
        {
            [Title("批量重命名工具", "批量重命名选中的GameObject", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择层级中的GameObject，\n设置重命名规则，\n点击重命名按钮批量修改";

            [ShowInInspector, ReadOnly, LabelText("重命名预览（前10项）"), ListDrawerSettings(DraggableItems = false)]
            [PropertyTooltip("显示选中对象的示例预览：原名 -> 新名，最多显示前10项。")]
            public List<string> renamePreview
            {
                get
                {
                    var result = new List<string>();
                    var selected = Selection.gameObjects ?? new GameObject[0];
                    int limit = Math.Min(selected.Length, 10);
                    for (int i = 0; i < limit; i++)
                    {
                        var obj = selected[i];
                        string newName = obj.name;
                        switch (renameMode)
                        {
                            case RenameMode.Prefix:
                                newName = prefixText + obj.name;
                                break;
                            case RenameMode.Suffix:
                                newName = obj.name + suffixText;
                                break;
                            case RenameMode.Replace:
                                if (!string.IsNullOrEmpty(findText))
                                {
                                    if (replaceCaseSensitive)
                                        newName = obj.name.Replace(findText, replaceText);
                                    else
                                        newName = System.Text.RegularExpressions.Regex.Replace(obj.name, System.Text.RegularExpressions.Regex.Escape(findText), replaceText, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                }
                                break;
                            case RenameMode.Number:
                                newName = baseName + numberSeparator + (startNumber + i).ToString($"D{numberDigits}");
                                break;
                        }
                        result.Add($"{obj.name} -> {newName}");
                    }
                    if (selected.Length > 10) result.Add($"... 共 {selected.Length} 个对象，显示前 {limit} 个示例");
                    if (result.Count == 0) result.Add("未选择对象");
                    return result;
                }
            }

            public enum RenameMode
            {
                [LabelText("前缀模式")]
                Prefix,
                [LabelText("后缀模式")]
                Suffix,
                [LabelText("替换模式")]
                Replace,
                [LabelText("编号模式")]
                Number
            }

            [InfoBox("@RenameModeInfo", InfoMessageType.Info)]
            [LabelText("重命名模式"), Space(5)]
            public RenameMode renameMode = RenameMode.Prefix;

            [ShowInInspector, HideLabel]
            private string RenameModeInfo
            {
                get
                {
                    switch (renameMode)
                    {
                        case RenameMode.Prefix:
                            return "前缀模式：在现有名称前添加指定前缀。例如，将 'Cube' 变为 'New_Cube'。";
                        case RenameMode.Suffix:
                            return "后缀模式：在现有名称后添加指定后缀。例如，将 'Cube' 变为 'Cube_Copy'。";
                        case RenameMode.Replace:
                            return "替换模式：将名称中匹配的文本替换为新的文本。例如，将 'OldCube' 中的 'Old' 替换为 'New'。";
                        case RenameMode.Number:
                            return "编号模式：使用基础名称并附加按序号格式化的编号。例如，'Object_001'、'Object_002'（可配置起始编号和位数）。";
                        default:
                            return string.Empty;
                    }
                }
            }

            [LabelText("前缀文本"), ShowIf("renameMode", RenameMode.Prefix), Space(5)]
            public string prefixText = "New_";

            [LabelText("后缀文本"), ShowIf("renameMode", RenameMode.Suffix), Space(5)]
            public string suffixText = "_Copy";

            [LabelText("查找文本"), ShowIf("renameMode", RenameMode.Replace), Space(5)]
            public string findText = "Old";

            [LabelText("替换文本"), ShowIf("renameMode", RenameMode.Replace), Space(5)]
            public string replaceText = "New";

            [LabelText("基础名称"), ShowIf("renameMode", RenameMode.Number), Space(5)]
            public string baseName = "Object";

            [LabelText("起始编号"), ShowIf("renameMode", RenameMode.Number), Space(5)]
            public int startNumber = 1;

            [ShowIf("renameMode", RenameMode.Number)]
            [InfoBox("指定序号的位数，左侧补零。例如：3 -> 001。", InfoMessageType.Info)]
            [LabelText("编号位数"), Range(1, 5), Space(5)]
            public int numberDigits = 3;

            [ShowIf("renameMode", RenameMode.Replace), LabelText("区分大小写"), Space(5)]
            public bool replaceCaseSensitive = true;

            [ShowIf("renameMode", RenameMode.Number), LabelText("编号分隔符"), Space(5)]
            public string numberSeparator = "_";


            [Button("批量重命名", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void BatchRename()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择要重命名的GameObject！", "确定");
                    return;
                }
                // 输入校验
                if (renameMode == RenameMode.Replace && string.IsNullOrEmpty(findText))
                {
                    EditorUtility.DisplayDialog("错误", "替换模式下请输入要查找的文本。", "确定");
                    return;
                }

                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Batch Rename");

                try
                {
                    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var newNames = new string[selectedObjects.Length];

                    // 预计算新名称并检测冲突
                    for (int i = 0; i < selectedObjects.Length; i++)
                    {
                        var obj = selectedObjects[i];
                        string newName = obj.name;
                        switch (renameMode)
                        {
                            case RenameMode.Prefix:
                                newName = prefixText + obj.name;
                                break;
                            case RenameMode.Suffix:
                                newName = obj.name + suffixText;
                                break;

                            case RenameMode.Replace:
                                if (replaceCaseSensitive)
                                    newName = obj.name.Replace(findText, replaceText);
                                else
                                    newName = System.Text.RegularExpressions.Regex.Replace(obj.name, System.Text.RegularExpressions.Regex.Escape(findText), replaceText, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                break;
                            case RenameMode.Number:
                                newName = baseName + numberSeparator + (startNumber + i).ToString($"D{numberDigits}");
                                break;
                        }
                        newNames[i] = newName;
                        if (usedNames.Contains(newName))
                        {
                            // 冲突：追加索引以保证唯一
                            int suffix = 1;
                            var candidate = newName + "(" + suffix + ")";
                            while (usedNames.Contains(candidate))
                            {
                                suffix++;
                                candidate = newName + "(" + suffix + ")";
                            }
                            newName = candidate;
                            newNames[i] = newName;
                        }
                        usedNames.Add(newName);
                    }

                    EditorUtility.DisplayProgressBar("批量重命名", "准备重命名...", 0f);

                    // 应用修改
                    for (int i = 0; i < selectedObjects.Length; i++)
                    {
                        var obj = selectedObjects[i];
                        var newName = newNames[i];

                        // 跳过无变化的项
                        if (obj.name == newName) continue;

                        Undo.RecordObject(obj, "Rename Object");
                        obj.name = newName;

                        if (i % 10 == 0)
                            EditorUtility.DisplayProgressBar("批量重命名", $"正在重命名: {i + 1}/{selectedObjects.Length}", (float)i / selectedObjects.Length);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    Undo.CollapseUndoOperations(group);
                }

                EditorUtility.DisplayDialog("成功", $"已处理 {selectedObjects.Length} 个对象（跳过名称未改变的对象）。", "确定");
            }
        }
        #endregion

        #region 物理对齐工具
        [Serializable]
        public class Page_PhysicsAlign : ESWindowPageBase
        {
            [Title("物理对齐工具", "对齐选中的GameObject位置", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择多个GameObject，\n选择对齐方式，\n点击对齐按钮执行对齐";

            public enum AlignMode
            {
                [LabelText("左对齐")]
                Left,
                [LabelText("右对齐")]
                Right,
                [LabelText("上对齐")]
                Top,
                [LabelText("下对齐")]
                Bottom,
                [LabelText("水平居中")]
                HorizontalCenter,
                [LabelText("垂直居中")]
                VerticalCenter
            }

            [LabelText("对齐模式"), Space(5)]
            public AlignMode alignMode = AlignMode.Left;

            [InfoBox("@GetAlignModeInfo()", InfoMessageType.Info)]
            [LabelText("对齐间距"), Space(5)]
            public float spacing = 0f;

            [LabelText("使用世界坐标"), Space(5)]
            public bool useWorldSpace = true;

            [Button("执行对齐", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void AlignObjects()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length < 2)
                {
                    EditorUtility.DisplayDialog("错误", "请选择至少两个GameObject！", "确定");
                    return;
                }

                Undo.RecordObjects(selectedObjects.Select(obj => obj.transform).ToArray(), "Align Objects");

                var transforms = selectedObjects.Select(obj => obj.transform).ToArray();
                var bounds = GetAccurateBounds(transforms);

                for (int i = 0; i < transforms.Length; i++)
                {
                    var transform = transforms[i];
                    var position = useWorldSpace ? transform.position : transform.localPosition;

                    switch (alignMode)
                    {
                        case AlignMode.Left:
                            position.x = bounds.min.x + (i * spacing);
                            break;
                        case AlignMode.Right:
                            position.x = bounds.max.x - (i * spacing);
                            break;
                        case AlignMode.Top:
                            position.y = bounds.max.y - (i * spacing);
                            break;
                        case AlignMode.Bottom:
                            position.y = bounds.min.y + (i * spacing);
                            break;
                        case AlignMode.HorizontalCenter:
                            position.x = bounds.center.x;
                            break;
                        case AlignMode.VerticalCenter:
                            position.y = bounds.center.y;
                            break;
                    }

                    if (useWorldSpace)
                        transform.position = position;
                    else
                        transform.localPosition = position;
                }

                EditorUtility.DisplayDialog("成功", $"成功对齐 {selectedObjects.Length} 个对象！", "确定");
            }

            private Bounds GetAccurateBounds(Transform[] transforms)
            {
                if (transforms.Length == 0) return new Bounds();

                Bounds? combinedBounds = null;

                foreach (var transform in transforms)
                {
                    Bounds? objectBounds = null;

                    // 优先使用 Renderer 的 bounds
                    var renderer = transform.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        objectBounds = renderer.bounds;
                    }

                    // 如果没有 Renderer，尝试使用 Collider 的 bounds
                    if (objectBounds == null)
                    {
                        var collider = transform.GetComponent<Collider>();
                        if (collider != null)
                        {
                            objectBounds = collider.bounds;
                        }
                    }

                    // 如果没有 Renderer 或 Collider，使用 Transform 的位置
                    if (objectBounds == null)
                    {
                        var position = useWorldSpace ? transform.position : transform.localPosition;
                        objectBounds = new Bounds(position, Vector3.zero);
                    }

                    // 合并当前对象的 bounds
                    if (combinedBounds == null)
                    {
                        combinedBounds = objectBounds;
                    }
                    else
                    {
                        combinedBounds.Value.Encapsulate(objectBounds.Value);
                    }
                }

                return combinedBounds ?? new Bounds();
            }

            private string GetAlignModeInfo()
            {
                switch (alignMode)
                {
                    case AlignMode.Left:
                        return "左对齐：将所有对象的左边缘对齐到最左边的对象。";
                    case AlignMode.Right:
                        return "右对齐：将所有对象的右边缘对齐到最右边的对象。";
                    case AlignMode.Top:
                        return "上对齐：将所有对象的上边缘对齐到最上边的对象。";
                    case AlignMode.Bottom:
                        return "下对齐：将所有对象的下边缘对齐到最下边的对象。";
                    case AlignMode.HorizontalCenter:
                        return "水平居中：将所有对象的水平中心对齐到中间位置。";
                    case AlignMode.VerticalCenter:
                        return "垂直居中：将所有对象的垂直中心对齐到中间位置。";
                    default:
                        return "未知对齐模式。";
                }
            }
        }
        #endregion

        #region 批量静态设置工具
        [Serializable]
        public class Page_BatchStaticSetting : ESWindowPageBase
        {
            [Title("批量静态设置工具", "批量设置GameObject的静态标记", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择GameObject，\n设置静态标记选项，\n点击应用按钮批量设置";

            [LabelText("包含子对象"), Space(5)]
            public bool includeChildren = true;

            private static readonly Color EnabledColor = new Color(0.7f, 0.9f, 0.7f);
            private static readonly Color DisabledColor = new Color(0.9f, 0.7f, 0.7f);

            [InfoBox("启用此选项后，该对象会影响场景中的全局光照计算，例如光线反射和间接光照。适合需要参与光照效果的静态物体，例如地面、墙壁等。依赖于光照贴图的生成。", InfoMessageType.Info)]
            [LabelText("贡献全局光照"), GUIColor("@contributeGI ? EnabledColor : DisabledColor")]
            public bool contributeGI = false;

            [InfoBox("启用此选项后，该对象会在遮挡剔除过程中被视为静态物体，从而减少渲染时的计算量，提升性能。适合用于大型静态物体，例如建筑物。依赖于遮挡剔除系统的设置。", InfoMessageType.Info)]
            [LabelText("遮挡剔除静态"), GUIColor("@occluderStatic ? EnabledColor : DisabledColor")]
            public bool occluderStatic = false;

            [InfoBox("启用此选项后，多个静态对象会被合并为一个批次进行渲染，从而减少渲染调用次数，提升性能。适合用于小型重复的静态物体，例如树木、石头等。依赖于静态批处理功能的开启。", InfoMessageType.Info)]
            [LabelText("批处理静态"), GUIColor("@batchingStatic ? EnabledColor : DisabledColor")]
            public bool batchingStatic = false;

            [InfoBox("启用此选项后，该对象会在导航网格生成时被视为静态障碍物，适合需要参与路径规划的物体，例如墙壁、障碍物等。依赖于导航网格系统的生成。", InfoMessageType.Info)]
            [LabelText("导航静态"), GUIColor("@navigationStatic ? EnabledColor : DisabledColor")]
            public bool navigationStatic = false;

            [InfoBox("启用此选项后，该对象会在反射探针的计算中被视为静态物体，从而提升反射效果的质量。适合需要高质量反射效果的物体，例如镜子、水面等。依赖于反射探针的设置。", InfoMessageType.Info)]
            [LabelText("反射探针静态"), GUIColor("@reflectionProbeStatic ? EnabledColor : DisabledColor")]
            public bool reflectionProbeStatic = false;

            [Button("应用静态设置", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void ApplyStaticSettings()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                var allObjects = new List<GameObject>();
                foreach (var obj in selectedObjects)
                {
                    allObjects.Add(obj);
                    if (includeChildren)
                    {
                        allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                    }
                }

                Undo.RecordObjects(allObjects.ToArray(), "Batch Static Setting");

                foreach (var obj in allObjects)
                {
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(obj);

                    // 常规标志（直接使用枚举成员，现代Unity仍支持）
                    if (contributeGI)
                        flags |= StaticEditorFlags.ContributeGI;
                    else
                        flags &= ~StaticEditorFlags.ContributeGI;

                    if (occluderStatic)
                        flags |= StaticEditorFlags.OccluderStatic;
                    else
                        flags &= ~StaticEditorFlags.OccluderStatic;

                    if (batchingStatic)
                        flags |= StaticEditorFlags.BatchingStatic;
                    else
                        flags &= ~StaticEditorFlags.BatchingStatic;

                    if (reflectionProbeStatic)
                        flags |= StaticEditorFlags.ReflectionProbeStatic;
                    else
                        flags &= ~StaticEditorFlags.ReflectionProbeStatic;

                    GameObjectUtility.SetStaticEditorFlags(obj, flags);
                }

                EditorUtility.DisplayDialog("成功", $"成功设置 {allObjects.Count} 个对象的静态标记！", "确定");
            }

            [Button("重置静态标记设置", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
            public void ResetStaticSettings()
            {
                contributeGI = false;
                occluderStatic = false;
                batchingStatic = false;
                navigationStatic = false;
                reflectionProbeStatic = false;
            }

            [Button("清除所有静态标记", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
            public void ClearAllStaticFlags()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                var allObjects = new List<GameObject>();
                foreach (var obj in selectedObjects)
                {
                    allObjects.Add(obj);
                    if (includeChildren)
                    {
                        allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                    }
                }

                Undo.RecordObjects(allObjects.ToArray(), "Clear Static Flags");

                foreach (var obj in allObjects)
                {
                    GameObjectUtility.SetStaticEditorFlags(obj, 0);
                }

                EditorUtility.DisplayDialog("成功", $"成功清除 {allObjects.Count} 个对象的静态标记！", "确定");
            }
        }
        #endregion

        #region 纹理精灵生成工具
        [Serializable]
        public class Page_TextureSpriteTool : ESWindowPageBase
        {
            [Title("纹理精灵生成工具", "批量处理纹理并生成Sprite", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择纹理文件，\n配置Sprite设置，\n点击处理按钮批量生成";

            [LabelText("纹理文件夹"), FolderPath, Space(5)]
            public string textureFolder;

            [LabelText("输出文件夹"), FolderPath, Space(5)]
            public string outputFolder;

            [LabelText("Sprite模式"), Space(5)]
            public SpriteImportMode spriteMode = SpriteImportMode.Single;

            [LabelText("像素每单位"), Space(5)]
            public float pixelsPerUnit = 100f;

            [LabelText("过滤模式"), Space(5)]
            public FilterMode filterMode = FilterMode.Point;

            [LabelText("压缩质量 (0: 最低质量, 100: 最高质量)"), Range(0, 100), Space(5)]
            public int compressionQuality = 50;

            [LabelText("最大纹理尺寸"), Space(5)]
            public int maxTextureSize = 2048;

            [LabelText("生成可读写纹理"), Space(5)]
            public bool isReadable = false;

            [LabelText("生成MipMaps"), Space(5)]
            public bool generateMipMaps = false;
            public override ESWindowPageBase ES_Refresh()
            {
                outputFolder = ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent+"/Sprites";

                return base.ES_Refresh();

            }


            [Button("处理选中纹理", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void ProcessSelectedTextures()
            {
                var selectedTextures = Selection.objects.OfType<Texture2D>().ToArray();
                if (selectedTextures.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择纹理文件！", "确定");
                    return;
                }

                ProcessTextures(selectedTextures.Select(t => AssetDatabase.GetAssetPath(t)).ToArray());
            }

            [Button("处理文件夹中的纹理", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void ProcessFolderTextures()
            {
                if (!AssetDatabase.IsValidFolder(textureFolder))
                {
                    EditorUtility.DisplayDialog("错误", "请选择有效的纹理文件夹！", "确定");
                    return;
                }

                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder });
                var texturePaths = guids.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToArray();

                ProcessTextures(texturePaths);
            }

            
            private void ProcessTextures(string[] texturePaths)
            {
                if (!AssetDatabase.IsValidFolder(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder.Replace("Assets/", ""));
                    AssetDatabase.Refresh();
                }

                int processedCount = 0;
                try
                {
                    AssetDatabase.StartAssetEditing();

                    for (int i = 0; i < texturePaths.Length; i++)
                    {
                        var texturePath = texturePaths[i];
                        EditorUtility.DisplayProgressBar("处理纹理", $"处理: {Path.GetFileName(texturePath)}", (float)i / texturePaths.Length);

                        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                        if (importer != null)
                        {
                            importer.textureType = TextureImporterType.Sprite;
                            importer.spriteImportMode = spriteMode;
                            importer.spritePixelsPerUnit = pixelsPerUnit;
                            importer.filterMode = filterMode;
                            importer.maxTextureSize = maxTextureSize;
                            importer.isReadable = isReadable;
                            importer.mipmapEnabled = generateMipMaps;

                            // 设置压缩设置
                            var settings = importer.GetDefaultPlatformTextureSettings();
                            settings.compressionQuality = compressionQuality;
                            importer.SetPlatformTextureSettings(settings);

                            importer.SaveAndReimport();
                            processedCount++;
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    EditorUtility.ClearProgressBar();
                    AssetDatabase.Refresh();
                }

                EditorUtility.DisplayDialog("成功", $"成功处理 {processedCount} 个纹理文件！", "确定");
            }

            [Button("重置为默认设置", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
            public void ResetToDefaults()
            {
                spriteMode = SpriteImportMode.Single;
                pixelsPerUnit = 100f;
                filterMode = FilterMode.Point;
                compressionQuality = 50;
                maxTextureSize = 2048;
                isReadable = false;
                generateMipMaps = false;
            }
        }
        #endregion

        #region Prefab管理工具
        [Serializable]
        public class Page_PrefabManagement : ESWindowPageBase
        {
            [Title("Prefab管理工具", "批量管理Prefab实例", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择Prefab实例，\n批量应用、还原或断开Prefab连接，\n或替换为新的Prefab";

            [LabelText("包含子对象"), Space(5)]
            public bool includeChildren = true;

            [LabelText("替换目标Prefab"), AssetsOnly, Space(5)]
            public GameObject targetPrefab;

            [Button("应用所有Prefab更改", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void ApplyAllPrefabs()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                int appliedCount = 0;
                foreach (var obj in selectedObjects)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(obj))
                    {
                        PrefabUtility.ApplyPrefabInstance(obj, InteractionMode.UserAction);
                        appliedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功应用 {appliedCount} 个Prefab实例的更改！", "确定");
            }

            [Button("还原所有Prefab更改", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void RevertAllPrefabs()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                int revertedCount = 0;
                foreach (var obj in selectedObjects)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(obj))
                    {
                        PrefabUtility.RevertPrefabInstance(obj, InteractionMode.UserAction);
                        revertedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功还原 {revertedCount} 个Prefab实例的更改！", "确定");
            }

            [Button("断开Prefab连接", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
            public void UnpackPrefabs()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                int unpackedCount = 0;
                foreach (var obj in selectedObjects)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(obj))
                    {
                        PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.UserAction);
                        unpackedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功断开 {unpackedCount} 个Prefab连接！", "确定");
            }

            [Button("替换为目标Prefab", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
            public void ReplacePrefabs()
            {
                if (targetPrefab == null)
                {
                    EditorUtility.DisplayDialog("错误", "请先设置目标Prefab！", "确定");
                    return;
                }

                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择要替换的GameObject！", "确定");
                    return;
                }

                int replacedCount = 0;
                foreach (var obj in selectedObjects)
                {
                    var parent = obj.transform.parent;
                    var position = obj.transform.position;
                    var rotation = obj.transform.rotation;
                    var scale = obj.transform.localScale;
                    var name = obj.name;

                    var newObj = PrefabUtility.InstantiatePrefab(targetPrefab) as GameObject;
                    newObj.transform.SetParent(parent);
                    newObj.transform.position = position;
                    newObj.transform.rotation = rotation;
                    newObj.transform.localScale = scale;
                    newObj.name = name;

                    Undo.RegisterCreatedObjectUndo(newObj, "Replace Prefab");
                    Undo.DestroyObjectImmediate(obj);
                    replacedCount++;
                }

                EditorUtility.DisplayDialog("成功", $"成功替换 {replacedCount} 个对象为目标Prefab！", "确定");
            }
        }
        #endregion

        #region 材质批量替换工具
        [Serializable]
        public class Page_MaterialReplacement : ESWindowPageBase
        {
            [Title("材质批量替换工具", "批量替换选中对象的材质", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择GameObject，\n设置源材质和目标材质，\n点击替换按钮批量修改";

            [LabelText("包含子对象"), Space(5)]
            public bool includeChildren = true;

            public enum ReplacementMode
            {
                [LabelText("替换指定材质")]
                ReplaceSpecific,
                [LabelText("替换所有材质")]
                ReplaceAll,
                [LabelText("按名称匹配")]
                MatchByName
            }

            [LabelText("替换模式"), Space(5)]
            public ReplacementMode replacementMode = ReplacementMode.ReplaceSpecific;

            [LabelText("源材质"), AssetsOnly, ShowIf("replacementMode", ReplacementMode.ReplaceSpecific), Space(5)]
            public Material sourceMaterial;

            [LabelText("目标材质"), AssetsOnly, Space(5)]
            public Material targetMaterial;

            [LabelText("匹配名称"), ShowIf("replacementMode", ReplacementMode.MatchByName), Space(5)]
            public string matchName = "";

            [Button("执行替换", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void ReplaceMaterials()
            {
                if (targetMaterial == null)
                {
                    EditorUtility.DisplayDialog("错误", "请先设置目标材质！", "确定");
                    return;
                }

                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                var allObjects = new List<GameObject>();
                foreach (var obj in selectedObjects)
                {
                    allObjects.Add(obj);
                    if (includeChildren)
                    {
                        allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                    }
                }

                int replacedCount = 0;
                foreach (var obj in allObjects)
                {
                    var renderer = obj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Undo.RecordObject(renderer, "Replace Material");
                        var materials = renderer.sharedMaterials;
                        bool changed = false;

                        for (int i = 0; i < materials.Length; i++)
                        {
                            bool shouldReplace = false;

                            switch (replacementMode)
                            {
                                case ReplacementMode.ReplaceSpecific:
                                    shouldReplace = materials[i] == sourceMaterial;
                                    break;
                                case ReplacementMode.ReplaceAll:
                                    shouldReplace = true;
                                    break;
                                case ReplacementMode.MatchByName:
                                    shouldReplace = materials[i] != null && materials[i].name.Contains(matchName);
                                    break;
                            }

                            if (shouldReplace)
                            {
                                materials[i] = targetMaterial;
                                changed = true;
                            }
                        }

                        if (changed)
                        {
                            renderer.sharedMaterials = materials;
                            replacedCount++;
                        }
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功替换 {replacedCount} 个对象的材质！", "确定");
            }
        }
        #endregion

        #region 场景优化工具
        [Serializable]
        public class Page_SceneOptimization : ESWindowPageBase
        {
            [Title("场景优化工具", "检测并优化场景性能问题", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "点击分析按钮检测场景问题，\n查看优化建议，\n一键应用优化";

            [ShowInInspector, ReadOnly, LabelText("分析结果"), TextArea(10, 20)]
            public string analysisResult = "点击'分析场景'按钮开始检测...";

            private int lightCount = 0;
            private int realtimeLightCount = 0;
            private int emptyObjectCount = 0;
            private int missingScriptCount = 0;
            private int highPolyCount = 0;

            [Button("分析场景", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void AnalyzeScene()
            {
                lightCount = 0;
                realtimeLightCount = 0;
                emptyObjectCount = 0;
                missingScriptCount = 0;
                highPolyCount = 0;

                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();

                foreach (var obj in allObjects)
                {
                    // 检测灯光
                    var light = obj.GetComponent<Light>();
                    if (light != null)
                    {
                        lightCount++;
                        if (light.type == LightType.Point || light.type == LightType.Spot)
                        {
                            realtimeLightCount++;
                        }
                    }

                    // 检测空对象
                    if (obj.GetComponents<Component>().Length == 1) // 只有Transform组件
                    {
                        emptyObjectCount++;
                    }

                    // 检测丢失脚本
                    var components = obj.GetComponents<Component>();
                    foreach (var comp in components)
                    {
                        if (comp == null)
                        {
                            missingScriptCount++;
                        }
                    }

                    // 检测高面数模型
                    var meshFilter = obj.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        if (meshFilter.sharedMesh.triangles.Length / 3 > 10000)
                        {
                            highPolyCount++;
                        }
                    }
                }

                analysisResult = $"场景分析报告:\n\n" +
                    $"总灯光数量: {lightCount}\n" +
                    $"实时灯光数量: {realtimeLightCount} {(realtimeLightCount > 4 ? "⚠️ 过多" : "✓")}\n" +
                    $"空对象数量: {emptyObjectCount} {(emptyObjectCount > 10 ? "⚠️ 建议清理" : "✓")}\n" +
                    $"丢失脚本数量: {missingScriptCount} {(missingScriptCount > 0 ? "⚠️ 需要修复" : "✓")}\n" +
                    $"高面数模型数量: {highPolyCount} {(highPolyCount > 5 ? "⚠️ 建议优化" : "✓")}\n\n" +
                    $"优化建议:\n" +
                    (realtimeLightCount > 4 ? "- 减少实时灯光数量，使用烘焙灯光\n" : "") +
                    (emptyObjectCount > 10 ? "- 清理无用的空对象\n" : "") +
                    (missingScriptCount > 0 ? "- 修复或移除丢失的脚本引用\n" : "") +
                    (highPolyCount > 5 ? "- 优化高面数模型，使用LOD系统\n" : "");
            }

            [Button("清理空对象", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void CleanEmptyObjects()
            {
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                int cleanedCount = 0;

                foreach (var obj in allObjects)
                {
                    if (obj.GetComponents<Component>().Length == 1 && obj.transform.childCount == 0)
                    {
                        Undo.DestroyObjectImmediate(obj);
                        cleanedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功清理 {cleanedCount} 个空对象！", "确定");
                AnalyzeScene();
            }

            [Button("移除丢失的脚本", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void RemoveMissingScripts()
            {
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                int cleanedCount = 0;

                foreach (var obj in allObjects)
                {
                    var components = obj.GetComponents<Component>();
                    var serializedObject = new SerializedObject(obj);
                    var prop = serializedObject.FindProperty("m_Component");

                    for (int i = components.Length - 1; i >= 0; i--)
                    {
                        if (components[i] == null)
                        {
                            prop.DeleteArrayElementAtIndex(i);
                            cleanedCount++;
                        }
                    }

                    serializedObject.ApplyModifiedProperties();
                }

                EditorUtility.DisplayDialog("成功", $"成功移除 {cleanedCount} 个丢失的脚本！", "确定");
                AnalyzeScene();
            }
        }
        #endregion

        #region 动画批量设置工具
        [Serializable]
        public class Page_AnimationBatchSetting : ESWindowPageBase
        {
            [Title("动画批量设置工具", "批量设置Animator属性", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择带有Animator的GameObject，\n设置动画属性，\n点击应用按钮批量修改";

            [LabelText("包含子对象"), Space(5)]
            public bool includeChildren = true;

            [LabelText("Animator Controller"), AssetsOnly, Space(5)]
            public RuntimeAnimatorController animatorController;

            [LabelText("更新模式"), Space(5)]
            public AnimatorUpdateMode updateMode = AnimatorUpdateMode.Normal;

            [LabelText("剔除模式"), Space(5)]
            public AnimatorCullingMode cullingMode = AnimatorCullingMode.AlwaysAnimate;

            [LabelText("应用根运动"), Space(5)]
            public bool applyRootMotion = false;

            [Button("应用设置", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void ApplyAnimatorSettings()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                var allObjects = new List<GameObject>();
                foreach (var obj in selectedObjects)
                {
                    allObjects.Add(obj);
                    if (includeChildren)
                    {
                        allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                    }
                }

                int modifiedCount = 0;
                foreach (var obj in allObjects)
                {
                    var animator = obj.GetComponent<Animator>();
                    if (animator != null)
                    {
                        Undo.RecordObject(animator, "Modify Animator");

                        if (animatorController != null)
                        {
                            animator.runtimeAnimatorController = animatorController;
                        }

                        animator.updateMode = updateMode;
                        animator.cullingMode = cullingMode;
                        animator.applyRootMotion = applyRootMotion;

                        EditorUtility.SetDirty(animator);
                        modifiedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功修改 {modifiedCount} 个Animator组件！", "确定");
            }

            [Button("批量添加Animator组件", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void AddAnimatorComponents()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                int addedCount = 0;
                foreach (var obj in selectedObjects)
                {
                    if (obj.GetComponent<Animator>() == null)
                    {
                        var animator = Undo.AddComponent<Animator>(obj);
                        if (animatorController != null)
                        {
                            animator.runtimeAnimatorController = animatorController;
                        }
                        addedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功添加 {addedCount} 个Animator组件！", "确定");
            }
        }
        #endregion

        #region 脚本批量挂载工具
        [Serializable]
        public class Page_ScriptBatchAttachment : ESWindowPageBase
        {
            [Title("脚本批量挂载工具", "批量为GameObject添加脚本组件", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择GameObject，\n输入脚本类型名称，\n点击挂载按钮批量添加";

            [LabelText("包含子对象"), Space(5)]
            public bool includeChildren = true;

            [LabelText("脚本类型名称（含命名空间）"), Space(5)]
            public string scriptTypeName = "";

            [LabelText("跳过已有相同组件的对象"), Space(5)]
            public bool skipExisting = true;

            [Button("挂载脚本", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void AttachScripts()
            {
                if (string.IsNullOrEmpty(scriptTypeName))
                {
                    EditorUtility.DisplayDialog("错误", "请输入脚本类型名称！", "确定");
                    return;
                }

                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                // 查找类型
                Type scriptType = null;
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    scriptType = assembly.GetType(scriptTypeName);
                    if (scriptType != null) break;
                }

                if (scriptType == null || !typeof(Component).IsAssignableFrom(scriptType))
                {
                    EditorUtility.DisplayDialog("错误", $"未找到类型 '{scriptTypeName}' 或它不是Component类型！", "确定");
                    return;
                }

                var allObjects = new List<GameObject>();
                foreach (var obj in selectedObjects)
                {
                    allObjects.Add(obj);
                    if (includeChildren)
                    {
                        allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                    }
                }

                int addedCount = 0;
                foreach (var obj in allObjects)
                {
                    if (skipExisting && obj.GetComponent(scriptType) != null)
                    {
                        continue;
                    }

                    Undo.AddComponent(obj, scriptType);
                    addedCount++;
                }

                EditorUtility.DisplayDialog("成功", $"成功为 {addedCount} 个对象添加脚本组件！", "确定");
            }

            [LabelText("常用脚本快捷添加"), Space(10)]
            [InfoBox("点击下方按钮快速添加常用组件", InfoMessageType.Info)]
            public bool commonScriptsSection;

            [Button("添加Rigidbody", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void AddRigidbody()
            {
                scriptTypeName = "UnityEngine.Rigidbody";
                AttachScripts();
            }

            [Button("添加BoxCollider", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void AddBoxCollider()
            {
                scriptTypeName = "UnityEngine.BoxCollider";
                AttachScripts();
            }

            [Button("添加AudioSource", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void AddAudioSource()
            {
                scriptTypeName = "UnityEngine.AudioSource";
                AttachScripts();
            }
        }
        #endregion

        #region 资源引用检查工具
        [Serializable]
        public class Page_AssetReferenceChecker : ESWindowPageBase
        {
            [Title("资源引用检查工具", "检查资源的引用关系", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择要检查的资源，\n点击查找按钮检测引用，\n查看引用列表";

            [LabelText("检查文件夹"), FolderPath, Space(5)]
            public string checkFolder = "Assets";

            [ShowInInspector, ReadOnly, LabelText("未使用的资源"), ListDrawerSettings(DraggableItems = false)]
            public List<string> unusedAssets = new List<string>();

            [Button("查找未使用的资源", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void FindUnusedAssets()
            {
                if (!AssetDatabase.IsValidFolder(checkFolder))
                {
                    EditorUtility.DisplayDialog("错误", "请选择有效的文件夹！", "确定");
                    return;
                }

                unusedAssets.Clear();
                var allAssets = AssetDatabase.FindAssets("", new[] { checkFolder });

                EditorUtility.DisplayProgressBar("检查资源引用", "正在分析...", 0f);

                for (int i = 0; i < allAssets.Length; i++)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(allAssets[i]);

                    // 跳过文件夹和脚本
                    if (AssetDatabase.IsValidFolder(assetPath) || assetPath.EndsWith(".cs"))
                        continue;

                    EditorUtility.DisplayProgressBar("检查资源引用", $"检查: {Path.GetFileName(assetPath)}", (float)i / allAssets.Length);

                    // 查找依赖
                    var dependencies = AssetDatabase.GetDependencies(assetPath, false);
                    bool isReferenced = false;

                    // 简化检查：查看是否在场景中被引用
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (obj != null)
                    {
                        // 这是一个简化的检查，实际项目中可能需要更复杂的逻辑
                        var referencingAssets = AssetDatabase.FindAssets("t:Scene");
                        foreach (var sceneGuid in referencingAssets)
                        {
                            var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                            var sceneDeps = AssetDatabase.GetDependencies(scenePath, true);
                            if (System.Array.IndexOf(sceneDeps, assetPath) >= 0)
                            {
                                isReferenced = true;
                                break;
                            }
                        }
                    }

                    if (!isReferenced)
                    {
                        unusedAssets.Add(assetPath);
                    }
                }

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("完成", $"找到 {unusedAssets.Count} 个可能未使用的资源！", "确定");
            }

            [Button("选中未使用的资源", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void SelectUnusedAssets()
            {
                var objects = unusedAssets.Select(path => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)).ToArray();
                Selection.objects = objects;
                EditorGUIUtility.PingObject(objects[0]);
            }

            [ShowInInspector, ReadOnly, LabelText("选中资源的引用"), ListDrawerSettings(DraggableItems = false)]
            public List<string> selectedAssetReferences = new List<string>();

            [Button("查找选中资源的引用", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
            public void FindReferencesToSelected()
            {
                selectedAssetReferences.Clear();

                var selectedAsset = Selection.activeObject;
                if (selectedAsset == null)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择一个资源！", "确定");
                    return;
                }

                var assetPath = AssetDatabase.GetAssetPath(selectedAsset);
                var allAssets = AssetDatabase.GetAllAssetPaths();

                EditorUtility.DisplayProgressBar("查找引用", "正在分析...", 0f);

                for (int i = 0; i < allAssets.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("查找引用", $"检查: {Path.GetFileName(allAssets[i])}", (float)i / allAssets.Length);

                    var dependencies = AssetDatabase.GetDependencies(allAssets[i], false);
                    if (System.Array.IndexOf(dependencies, assetPath) >= 0)
                    {
                        selectedAssetReferences.Add(allAssets[i]);
                    }
                }

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("完成", $"找到 {selectedAssetReferences.Count} 个引用！", "确定");
            }
        }
        #endregion

        #region 灯光设置工具
        [Serializable]
        public class Page_LightingSettings : ESWindowPageBase
        {
            [Title("灯光设置工具", "批量调整灯光属性", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择带有Light组件的GameObject，\n设置灯光属性，\n点击应用按钮批量修改";

            [LabelText("包含子对象"), Space(5)]
            public bool includeChildren = true;

            [LabelText("灯光类型"), Space(5)]
            public LightType lightType = LightType.Point;

            [LabelText("颜色"), Space(5)]
            public Color lightColor = Color.white;

            [LabelText("强度"), Range(0f, 10f), Space(5)]
            public float intensity = 1f;

            [LabelText("范围"), Range(0f, 100f), Space(5)]
            public float range = 10f;

            [LabelText("阴影类型"), Space(5)]
            public LightShadows shadowType = LightShadows.None;

            [LabelText("烘焙模式"), Space(5)]
            public LightmapBakeType bakeType = LightmapBakeType.Realtime;

            [Button("应用灯光设置", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void ApplyLightingSettings()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                var allObjects = new List<GameObject>();
                foreach (var obj in selectedObjects)
                {
                    allObjects.Add(obj);
                    if (includeChildren)
                    {
                        allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                    }
                }

                int modifiedCount = 0;
                foreach (var obj in allObjects)
                {
                    var light = obj.GetComponent<Light>();
                    if (light != null)
                    {
                        Undo.RecordObject(light, "Modify Light");

                        light.type = lightType;
                        light.color = lightColor;
                        light.intensity = intensity;
                        light.range = range;
                        light.shadows = shadowType;
                        light.lightmapBakeType = bakeType;

                        EditorUtility.SetDirty(light);
                        modifiedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功修改 {modifiedCount} 个灯光组件！", "确定");
            }

            [Button("批量添加Light组件", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void AddLightComponents()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                int addedCount = 0;
                foreach (var obj in selectedObjects)
                {
                    if (obj.GetComponent<Light>() == null)
                    {
                        var light = Undo.AddComponent<Light>(obj);
                        light.type = lightType;
                        light.color = lightColor;
                        light.intensity = intensity;
                        light.range = range;
                        addedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功添加 {addedCount} 个Light组件！", "确定");
            }

            [Button("将所有灯光转为烘焙", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
            public void ConvertAllToBaked()
            {
                var allLights = UnityEngine.Object.FindObjectsOfType<Light>();
                int convertedCount = 0;

                foreach (var light in allLights)
                {
                    Undo.RecordObject(light, "Convert to Baked");
                    light.lightmapBakeType = LightmapBakeType.Baked;
                    EditorUtility.SetDirty(light);
                    convertedCount++;
                }

                EditorUtility.DisplayDialog("成功", $"成功转换 {convertedCount} 个灯光为烘焙模式！", "确定");
            }
        }
        #endregion

        #region 粒子系统批量调整工具
        [Serializable]
        public class Page_ParticleSystemAdjustment : ESWindowPageBase
        {
            [Title("粒子系统批量调整工具", "批量调整粒子系统参数", bold: true, titleAlignment: TitleAlignments.Centered)]

            [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string readMe = "选择带有ParticleSystem的GameObject，\n设置粒子参数，\n点击应用按钮批量修改";

            [LabelText("包含子对象"), Space(5)]
            public bool includeChildren = true;

            [LabelText("持续时间"), Range(0f, 10f), Space(5)]
            public float duration = 5f;

            [LabelText("循环播放"), Space(5)]
            public bool looping = true;

            [LabelText("开始生命周期"), Range(0f, 10f), Space(5)]
            public float startLifetime = 5f;

            [LabelText("开始速度"), Range(0f, 100f), Space(5)]
            public float startSpeed = 5f;

            [LabelText("开始大小"), Range(0f, 10f), Space(5)]
            public float startSize = 1f;

            [LabelText("开始颜色"), Space(5)]
            public Color startColor = Color.white;

            [LabelText("发射速率"), Range(0f, 1000f), Space(5)]
            public float emissionRate = 10f;

            [LabelText("模拟空间"), Space(5)]
            public ParticleSystemSimulationSpace simulationSpace = ParticleSystemSimulationSpace.Local;

            [Button("应用粒子系统设置", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void ApplyParticleSystemSettings()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                var allObjects = new List<GameObject>();
                foreach (var obj in selectedObjects)
                {
                    allObjects.Add(obj);
                    if (includeChildren)
                    {
                        allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                    }
                }

                int modifiedCount = 0;
                foreach (var obj in allObjects)
                {
                    var ps = obj.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        Undo.RecordObject(ps, "Modify Particle System");

                        var main = ps.main;
                        main.duration = duration;
                        main.loop = looping;
                        main.startLifetime = startLifetime;
                        main.startSpeed = startSpeed;
                        main.startSize = startSize;
                        main.startColor = startColor;
                        main.simulationSpace = simulationSpace;

                        var emission = ps.emission;
                        emission.rateOverTime = emissionRate;

                        EditorUtility.SetDirty(ps);
                        modifiedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功修改 {modifiedCount} 个粒子系统！", "确定");
            }

            [Button("批量播放粒子系统", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void PlayAllParticleSystems()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                var allObjects = new List<GameObject>();
                foreach (var obj in selectedObjects)
                {
                    allObjects.Add(obj);
                    if (includeChildren)
                    {
                        allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                    }
                }

                int playedCount = 0;
                foreach (var obj in allObjects)
                {
                    var ps = obj.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Play();
                        playedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功播放 {playedCount} 个粒子系统！", "确定");
            }

            [Button("批量停止粒子系统", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
            public void StopAllParticleSystems()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                var allObjects = new List<GameObject>();
                foreach (var obj in selectedObjects)
                {
                    allObjects.Add(obj);
                    if (includeChildren)
                    {
                        allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                    }
                }

                int stoppedCount = 0;
                foreach (var obj in allObjects)
                {
                    var ps = obj.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Stop();
                        stoppedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功停止 {stoppedCount} 个粒子系统！", "确定");
            }

            [Button("批量清空粒子", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
            public void ClearAllParticleSystems()
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                    return;
                }

                var allObjects = new List<GameObject>();
                foreach (var obj in selectedObjects)
                {
                    allObjects.Add(obj);
                    if (includeChildren)
                    {
                        allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                    }
                }

                int clearedCount = 0;
                foreach (var obj in allObjects)
                {
                    var ps = obj.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Clear();
                        clearedCount++;
                    }
                }

                EditorUtility.DisplayDialog("成功", $"成功清空 {clearedCount} 个粒子系统！", "确定");
            }
        }
        #endregion
    }
}
