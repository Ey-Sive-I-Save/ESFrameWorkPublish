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
        public const string PageName_CoreWorkbench = "01 高价值工作台";
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

        private const string MenuPath_RuntimeWatch = PageName_CoreWorkbench + "/运行时观察  [工业级]";
        private const string MenuPath_MaterialReplacement = PageName_CoreWorkbench + "/材质批量替换  [工业级]";
        private const string MenuPath_PrefabManagement = PageName_CoreWorkbench + "/Prefab实例管理  [工业级]";
        private const string MenuPath_PhysicsAlign = PageName_CoreWorkbench + "/物理对齐与布景  [工业级]";
        private const string MenuPath_AnimationBatchSetting = PageName_CoreWorkbench + "/动画器批量设置  [工业级]";
        private const string MenuPath_BatchStaticSettingCore = PageName_CoreWorkbench + "/批量静态设置  [工业级]";
        private const string MenuPath_AssetReferenceChecker = PageName_CoreWorkbench + "/资源引用检查  [工业级]";
        private const string MenuPath_BatchRename = PageName_SceneBatchTools + "/" + PageName_BatchRename + "  [升级中]";
        private const string MenuPath_SceneOptimization = PageName_SceneBatchTools + "/" + PageName_SceneOptimization + "  [升级中]";
        private const string MenuPath_LightingSettings = PageName_SceneBatchTools + "/" + PageName_LightingSettings + "  [升级中]";
        private const string MenuPath_ParticleSystemAdjustment = PageName_SceneBatchTools + "/" + PageName_ParticleSystemAdjustment + "  [升级中]";
        private const string MenuPath_UnityPackageTool = PageName_AssetPublishTools + "/" + PageName_UnityPackageTool + "  [升级中]";
        private const string MenuPath_TextureSpriteTool = PageName_AssetPublishTools + "/" + PageName_TextureSpriteTool + "  [升级中]";
        private const string MenuPath_ObjectPool = PageName_DiagnosticsTools + "/" + PageName_ObjectPool + "  [诊断]";
        private const string MenuPath_TopToolbar = PageName_DiagnosticsTools + "/" + PageName_TopToolbar + "  [配置]";
        private const string MenuPath_SceneTextRepair = PageName_DiagnosticsTools + "/" + PageName_SceneTextRepair + "  [修复]";

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
            {
                QuickBuildRootMenu(tree, PageName_Overview, ref pageOverview, SdfIconType.Speedometer2);

                // 高价值工作台：已重点工业化，优先显示。
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

        [Serializable]
        public class Page_SimpleToolsOverview : ESWindowPageBase
        {
            [OnInspectorGUI]
            private void DrawOverview()
            {
                SimpleToolsPanelUtility.DrawToolHeader(
                    "ES 简单工具集总览",
                    "这里按商业价值和操作风险组织工具。优先使用高价值工作台；场景批处理和资产发布区仍在继续统一 UI、分页、报告和大批量保护。",
                    SimpleToolsMaturity.Upgrading,
                    "批量写场景、Prefab、材质、动画、静态标记前必须先预览目标和规则。旧工具没有预览时要按高风险处理。");

                SimpleToolsPanelUtility.DrawSectionTitle("推荐使用顺序", "先用成熟工具处理高频问题，再进入升级中工具。");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawOverviewRow("01 高价值工作台", "RuntimeWatch、材质替换、Prefab管理、物理布景、Animator、静态标记、资源引用检查。", SimpleToolsMaturity.Industrial);
                    DrawOverviewRow("02 场景批处理", "批量重命名、场景优化、灯光设置、粒子调整。已开始接入统一风险提示和大列表保护。", SimpleToolsMaturity.Upgrading);
                    DrawOverviewRow("03 资产与发布", "UnityPackage、纹理精灵。下一步补资产批处理报告、失败项复查和导出记录。", SimpleToolsMaturity.Upgrading);
                    DrawOverviewRow("04 诊断与集成", "对象池、顶部工具栏、场景文本修复。偏框架配套和诊断，不混入场景批量写入区。", SimpleToolsMaturity.Upgrading);
                }

                SimpleToolsPanelUtility.DrawSectionTitle("按任务进入", "不记工具名也能开工；按钮只负责跳页，不会执行任何写入。");
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

                SimpleToolsPanelUtility.DrawSectionTitle("风险分层", "判断是否必须预览、是否要二次确认。");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("低风险：只扫描、只定位、只复制报告、只读运行时数据。", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("中风险：改当前场景对象的名称、位置、Static Flags、Animator 参数、材质槽位。必须支持 Undo。", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("高风险：保存 Prefab Asset、改导入设置、批量创建/删除资产、跨场景写入。必须先预览并二次确认。", EditorStyles.wordWrappedMiniLabel);
                }

                SimpleToolsPanelUtility.DrawSectionTitle("统一化进度", "这部分是后续 AI 和开发者判断工具状态的基准。");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("已落地：总入口按商业价值重排；公共分页、报告、历史、重操作确认、大列表提示；部分旧工具接入统一头部。", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("继续推进：所有表格统一分页；批量工具统一预览签名；失败项报告结构化；导出报告落到每个核心工具。", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("保守边界：不把低价值工具平均工业化；批量写资产和场景的工具优先。", EditorStyles.wordWrappedMiniLabel);
                }

                SimpleToolsPanelUtility.DrawSectionTitle("人工验证清单", "Unity 面板里必须实际点过，不用截图也要按这个顺序过一遍。");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("1. 空选区：空状态清楚，不报错，不弹无意义异常。", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("2. 小选区：预览、定位、执行、Undo、Dirty、报告都能跑通。", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("3. 大选区：分页不卡，搜索能缩小范围，执行前会提示目标数量和风险。", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("4. Prefab/资产：不应误改 Prefab Asset；需要改资产时必须明确进入资产模式或二次确认。", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("5. 失败项：失败原因必须进入报告，不能只在 Console 里刷一串异常。", EditorStyles.wordWrappedMiniLabel);
                }

                SimpleToolsPanelUtility.DrawOperationHistory();
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

