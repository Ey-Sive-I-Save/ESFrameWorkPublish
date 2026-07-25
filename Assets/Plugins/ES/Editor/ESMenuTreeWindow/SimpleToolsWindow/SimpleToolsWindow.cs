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
        [MenuItem(MenuItemPathDefine.EDITOR_OPTIMIZATION_PATH + "简单工具集", false, 0)]
        public static void TryOpenWindow()
        {
            OpenWindow();
        }

        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "简单工具集", false, -950)]
        public static void TryOpenWindowFromQuickWindows()
        {
            OpenWindow();
        }

        #region 简单重写
        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            var content = new GUIContent("ES 简单工具集", "使用 ES 简单工具完成快速开发和项目管理");
            return content;
        }

        public override void ESWindow_OnOpen()
        {
            base.ESWindow_OnOpen();
            EditorApplication.delayCall -= ApplyDefaultMenuWidth;
            EditorApplication.delayCall += ApplyDefaultMenuWidth;
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

        private static void ApplyDefaultMenuWidth()
        {
            if (UsingWindow != null)
                UsingWindow.MenuWidth = 245;
        }
        #endregion

        #region 数据滞留与声明
        //根页面名
        public const string PageName_CoreWorkbench = "01 常用工具";
        public const string PageName_SceneBatchTools = "02 场景批处理";
        public const string PageName_AssetPublishTools = "03 资产与发布";
        public const string PageName_DiagnosticsTools = "04 诊断与集成";
        public const string PageName_LegacyTools = "90 旧工具与待升级";
        public const string PageName_Overview = "00 工具总览";
        public const string PageName_AssetTools = "03 资产与发布";
        public const string PageName_HierarchyTools = "02 场景批处理";
        public const string PageName_ESIntegrationTools = "04 诊断与集成";
        public const string PageName_ObjectPool = "对象池工具";
        public const string PageName_TopToolbar = "顶部工具栏";
        public const string PageName_SceneTextRepair = "场景文本修复";
        public const string PageName_RuntimeWatch = "运行时观察";
        [NonSerialized] public Page_ObjectPool pageObjectPool;
        [NonSerialized] public Page_TopToolbar pageTopToolbar;
        [NonSerialized] public Page_SceneTextRepair pageSceneTextRepair;
        [NonSerialized] public Page_RuntimeWatch pageRuntimeWatch;
        public const string PageName_UnityPackageTool = "UnityPackage打包工具";
        public const string PageName_BatchRename = "批量重命名";
        public const string PageName_PhysicsAlign = "物理对齐";
        public const string PageName_BatchStaticSetting = "批量静态设置";
        public const string PageName_TextureSpriteTool = "纹理精灵生成工具";
        public const string PageName_PrefabManagement = "Prefab实例管理工具";
        public const string PageName_MaterialReplacement = "材质批量替换工具";
        public const string PageName_SceneOptimization = "场景优化工具";
        public const string PageName_AnimationBatchSetting = "动画器批量设置工具";
        public const string PageName_AssetReferenceChecker = "资源引用检查工具";
        public const string PageName_LightingSettings = "灯光设置工具";
        public const string PageName_ParticleSystemAdjustment = "粒子系统批量调整工具";

        private const string MenuPath_RuntimeWatch = PageName_CoreWorkbench + "/07 运行时观察";
        private const string MenuPath_MaterialReplacement = PageName_CoreWorkbench + "/01 材质批量替换";
        private const string MenuPath_PrefabManagement = PageName_CoreWorkbench + "/02 Prefab实例管理";
        private const string MenuPath_PhysicsAlign = PageName_CoreWorkbench + "/03 物理对齐与布景";
        private const string MenuPath_AnimationBatchSetting = PageName_CoreWorkbench + "/04 动画器批量设置";
        private const string MenuPath_BatchStaticSettingCore = PageName_CoreWorkbench + "/05 批量静态设置";
        private const string MenuPath_AssetReferenceChecker = PageName_CoreWorkbench + "/06 资源引用检查";
        private const string MenuPath_BatchRename = PageName_SceneBatchTools + "/01 " + PageName_BatchRename;
        private const string MenuPath_SceneOptimization = PageName_SceneBatchTools + "/02 " + PageName_SceneOptimization;
        private const string MenuPath_LightingSettings = PageName_SceneBatchTools + "/03 " + PageName_LightingSettings;
        private const string MenuPath_ParticleSystemAdjustment = PageName_SceneBatchTools + "/04 " + PageName_ParticleSystemAdjustment;
        private const string MenuPath_UnityPackageTool = PageName_AssetPublishTools + "/01 " + PageName_UnityPackageTool;
        private const string MenuPath_TextureSpriteTool = PageName_AssetPublishTools + "/02 " + PageName_TextureSpriteTool;
        private const string MenuPath_ObjectPool = PageName_DiagnosticsTools + "/01 " + PageName_ObjectPool + "  [诊断]";
        private const string MenuPath_TopToolbar = PageName_DiagnosticsTools + "/02 " + PageName_TopToolbar + "  [配置]";
        private const string MenuPath_SceneTextRepair = PageName_DiagnosticsTools + "/03 " + PageName_SceneTextRepair + "  [修复]";

        [NonSerialized] public Page_SimpleToolsOverview pageOverview;
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
            tree.Config.DrawSearchToolbar = true;
            {
                QuickBuildRootMenu(tree, PageName_Overview, ref pageOverview, SdfIconType.Speedometer2);

                // Frequently used editor tools.
                QuickBuildRootMenu(tree, MenuPath_RuntimeWatch, ref pageRuntimeWatch, SdfIconType.Activity);
                QuickBuildRootMenu(tree, MenuPath_MaterialReplacement, ref pageMaterialReplacement, SdfIconType.Palette);
                QuickBuildRootMenu(tree, MenuPath_PrefabManagement, ref pagePrefabManagement, SdfIconType.Box);
                QuickBuildRootMenu(tree, MenuPath_PhysicsAlign, ref pagePhysicsAlign, SdfIconType.Grid);
                QuickBuildRootMenu(tree, MenuPath_AnimationBatchSetting, ref pageAnimationBatchSetting, SdfIconType.Play);
                QuickBuildRootMenu(tree, MenuPath_BatchStaticSettingCore, ref pageBatchStaticSetting, SdfIconType.ToggleOn);
                QuickBuildRootMenu(tree, MenuPath_AssetReferenceChecker, ref pageAssetReferenceChecker, SdfIconType.Search);

                // 场景批处理：仍有价值，但需要继续统一 UI 和大批量保护。
                QuickBuildRootMenu(tree, MenuPath_BatchRename, ref pageBatchRename, SdfIconType.Pencil);
                QuickBuildRootMenu(tree, MenuPath_SceneOptimization, ref pageSceneOptimization, SdfIconType.Speedometer);
                QuickBuildRootMenu(tree, MenuPath_LightingSettings, ref pageLightingSettings, SdfIconType.Lightbulb);
                QuickBuildRootMenu(tree, MenuPath_ParticleSystemAdjustment, ref pageParticleSystemAdjustment, SdfIconType.Stars);

                // 资产与发布：偏资产流水线，下一阶段重点补报告导出和批处理历史。
                QuickBuildRootMenu(tree, MenuPath_UnityPackageTool, ref pageUnityPackageTool, SdfIconType.Archive);
                QuickBuildRootMenu(tree, MenuPath_TextureSpriteTool, ref pageTextureSpriteTool, SdfIconType.Image);

                // 诊断与集成：ES 框架配套工具，不混在批量写入工具里。
                QuickBuildRootMenu(tree, MenuPath_ObjectPool, ref pageObjectPool, SdfIconType.Droplet);
                QuickBuildRootMenu(tree, MenuPath_TopToolbar, ref pageTopToolbar, SdfIconType.Map);
                QuickBuildRootMenu(tree, MenuPath_SceneTextRepair, ref pageSceneTextRepair, SdfIconType.Search);
                ConfigureDefaultMenuExpansion(tree);
            }
            ES_LoadData();
        }

        private static void ConfigureDefaultMenuExpansion(OdinMenuTree tree)
        {
            if (tree == null)
                return;

            foreach (var item in tree.EnumerateTree())
            {
                if (item == null)
                    continue;

                item.Toggled = item.Name == PageName_CoreWorkbench || item.Name == PageName_SceneBatchTools;
            }
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

        [Serializable]
        public class Page_SimpleToolsOverview : ESWindowPageBase
        {
            [OnInspectorGUI]
            private void DrawOverview()
            {
                EditorGUILayout.HelpBox("选择左侧工具后再执行操作。批量修改场景或资产前，请先确认目标范围。", MessageType.Info);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawQuickOpenRow(
                        ("看运行时数据", MenuPath_RuntimeWatch, "无侵入观察字段、属性和方法。"),
                        ("批量换材质", MenuPath_MaterialReplacement, "先预览命中，再替换场景或 Prefab 资产。"));
                    DrawQuickOpenRow(
                        ("整理 Prefab", MenuPath_PrefabManagement, "分析实例、应用、还原、断开连接。"),
                        ("布景和落地", MenuPath_PhysicsAlign, "落地、归整、分布、对齐和轻随机。"));
                    DrawQuickOpenRow(
                        ("批量改名", MenuPath_BatchRename, "保存命名方案，快速复用上次规则。"),
                        ("找资源引用", MenuPath_AssetReferenceChecker, "查未使用、被谁引用、依赖了谁。"));
                    DrawQuickOpenRow(
                        ("导出包", MenuPath_UnityPackageTool, "确认资源清单后再导出 UnityPackage。"),
                        ("切 Sprite", MenuPath_TextureSpriteTool, "批量设置纹理导入和精灵切分。"));
                }
            }

            private static void DrawOverviewRow(string title, string description, SimpleToolsMaturity maturity)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.Width(120));
                    EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
                    SimpleToolsPanelUtility.DrawMaturityBadge(maturity);
                }
            }

            private static void DrawQuickOpenRow(
                (string label, string menuPath, string tip) left,
                (string label, string menuPath, string tip) right)
            {
                if (EditorGUIUtility.currentViewWidth < 760f)
                {
                    DrawQuickOpenButton(left.label, left.menuPath, left.tip);
                    DrawQuickOpenButton(right.label, right.menuPath, right.tip);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    DrawQuickOpenButton(left.label, left.menuPath, left.tip);
                    GUILayout.Space(8);
                    DrawQuickOpenButton(right.label, right.menuPath, right.tip);
                }
            }

            private static void DrawQuickOpenButton(string label, string menuPath, string tip)
            {
                bool exists = MenuItems.TryGetValue(menuPath, out var item) && item != null;
                using (new EditorGUILayout.HorizontalScope(GUILayout.MinHeight(26), GUILayout.ExpandWidth(true)))
                {
                    using (new EditorGUI.DisabledScope(!exists))
                    {
                        if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(104), GUILayout.Height(22)))
                            item.Select();
                    }

                    EditorGUILayout.LabelField(tip, EditorStyles.wordWrappedMiniLabel, GUILayout.MinWidth(120));
                }
            }
        }



        // Page_UnityPackageTool 类已移动到 AssetsTools/Simple_AssetTool_Page_UnityPackageTool.cs

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

