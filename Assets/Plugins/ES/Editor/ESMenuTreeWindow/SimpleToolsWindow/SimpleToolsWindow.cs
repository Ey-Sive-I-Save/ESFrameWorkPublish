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
        [MenuItem(MenuItemPathDefine.EDITOR_TOOLS_PATH + "简单工具窗口", false, 6)]
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
        public const string PageName_AssetTools = "【资产工具集】";
        public const string PageName_HierarchyTools = "【层级工具集】";
        public const string PageName_ESIntegrationTools = "【ES集成工具集】";
        public const string PageName_ObjectPool = "对象池工具";
        public const string PageName_TopToolbar = "顶部工具栏";
        [NonSerialized] public Page_ObjectPool pageObjectPool;
        [NonSerialized] public Page_TopToolbar pageTopToolbar;
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
            {
                // 资产工具集
                QuickBuildRootMenu(tree, PageName_AssetTools + "/" + PageName_UnityPackageTool, ref pageUnityPackageTool, SdfIconType.Archive);
                QuickBuildRootMenu(tree, PageName_AssetTools + "/" + PageName_TextureSpriteTool, ref pageTextureSpriteTool, SdfIconType.Image);
                QuickBuildRootMenu(tree, PageName_AssetTools + "/" + PageName_AssetReferenceChecker, ref pageAssetReferenceChecker, SdfIconType.Search);

                // 层级工具集
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_BatchRename, ref pageBatchRename, SdfIconType.Pencil);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_PhysicsAlign, ref pagePhysicsAlign, SdfIconType.Grid);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_BatchStaticSetting, ref pageBatchStaticSetting, SdfIconType.ToggleOn);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_PrefabManagement, ref pagePrefabManagement, SdfIconType.Box);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_MaterialReplacement, ref pageMaterialReplacement, SdfIconType.Palette);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_SceneOptimization, ref pageSceneOptimization, SdfIconType.Speedometer);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_AnimationBatchSetting, ref pageAnimationBatchSetting, SdfIconType.Play);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_LightingSettings, ref pageLightingSettings, SdfIconType.Lightbulb);
                QuickBuildRootMenu(tree, PageName_HierarchyTools + "/" + PageName_ParticleSystemAdjustment, ref pageParticleSystemAdjustment, SdfIconType.Stars);

                // ES集成工具集
                QuickBuildRootMenu(tree, PageName_ESIntegrationTools + "/" + PageName_ObjectPool, ref pageObjectPool, SdfIconType.Droplet);
                QuickBuildRootMenu(tree, PageName_ESIntegrationTools + "/" + PageName_TopToolbar, ref pageTopToolbar, SdfIconType.Map);
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

