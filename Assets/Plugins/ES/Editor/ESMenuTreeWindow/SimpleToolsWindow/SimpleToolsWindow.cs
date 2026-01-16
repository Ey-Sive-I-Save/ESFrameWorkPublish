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
        public const string PageName_AssetTools = "资产工具集";
        public const string PageName_HierarchyTools = "层级工具集";
        public const string PageName_ESIntegrationTools = "ES集成工具集";
        public const string PageName_ObjectPool = "对象池工具";
        [NonSerialized] public Page_ObjectPool pageObjectPool;
        public const string PageName_UnityPackageTool = "UnityPackage打包工具";
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
                // 资产工具集
                QuickBuildRootMenu(tree, PageName_AssetTools + "/" + PageName_UnityPackageTool, ref pageUnityPackageTool, SdfIconType.Archive);
                QuickBuildRootMenu(tree, PageName_AssetTools + "/" + PageName_TextureSpriteTool, ref pageTextureSpriteTool, SdfIconType.Image);
                QuickBuildRootMenu(tree, PageName_AssetTools + "/" + PageName_AnimationBatchSetting, ref pageAnimationBatchSetting, SdfIconType.Play);
                QuickBuildRootMenu(tree, PageName_AssetTools + "/" + PageName_AssetReferenceChecker, ref pageAssetReferenceChecker, SdfIconType.Search);

                // 层级工具集
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_BatchRename, ref pageBatchRename, SdfIconType.Pencil);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_PhysicsAlign, ref pagePhysicsAlign, SdfIconType.Grid);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_BatchStaticSetting, ref pageBatchStaticSetting, SdfIconType.ToggleOn);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_PrefabManagement, ref pagePrefabManagement, SdfIconType.Box);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_MaterialReplacement, ref pageMaterialReplacement, SdfIconType.Palette);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_SceneOptimization, ref pageSceneOptimization, SdfIconType.Speedometer);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_ScriptBatchAttachment, ref pageScriptBatchAttachment, SdfIconType.CodeSlash);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_LightingSettings, ref pageLightingSettings, SdfIconType.Lightbulb);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_ParticleSystemAdjustment, ref pageParticleSystemAdjustment, SdfIconType.Stars);

                // ES集成工具集
                QuickBuildRootMenu(tree, PageName_ESIntegrationTools + "/" + PageName_ObjectPool, ref pageObjectPool, SdfIconType.Droplet);
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

 
   


}

