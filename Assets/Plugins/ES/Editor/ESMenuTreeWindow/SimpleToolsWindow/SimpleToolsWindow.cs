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
        private Vector2 toolContentScrollPosition;
        private static bool runtimeWatchWasFrontmost;

        // Keep Odin's normal inspectors, but replace its two-axis outer scroll
        // container with a vertical-only one for this tool collection.
        public override bool UseScrollView => false;

        [MenuItem(MenuItemPathDefine.DEVELOPMENT_MAINTENANCE_PATH + "综合工具/简单工具集", false, 0)]
        public static void TryOpenWindow()
        {
            ESWindowCommandRegistry.RecordOpened("simple_tools");
            OpenWindow();
        }

        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "简单工具集", false, -950)]
        public static void TryOpenWindowFromQuickWindows()
        {
            ESWindowCommandRegistry.RecordOpened("simple_tools");
            OpenWindow();
        }

        [MenuItem(MenuItemPathDefine.RUNTIME_DIAGNOSTICS_PATH + "RuntimeWatch/打开运行时观察 %#w", false, 0)]
        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "RuntimeWatch", false, -940)]
        public static void OpenRuntimeWatchFromMenu()
        {
            ESWindowCommandRegistry.RecordOpened("runtime_watch");
            OpenWindow();
            EditorApplication.delayCall -= SelectRuntimeWatchPage;
            EditorApplication.delayCall += SelectRuntimeWatchPage;
        }

        private static void SelectRuntimeWatchPage()
        {
            if (UsingWindow == null || !MenuItems.TryGetValue(MenuPath_RuntimeWatch, out OdinMenuItem item) || item == null)
                return;

            UsingWindow.MenuTree.Selection.Clear();
            UsingWindow.MenuTree.Selection.Add(item);
            UsingWindow.Repaint();
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
            EditorApplication.update -= TickRuntimeWatch;
            EditorApplication.update += TickRuntimeWatch;
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

        private static void TickRuntimeWatch()
        {
            SimpleToolsWindow window = UsingWindow;
            if (window == null)
            {
                EditorApplication.update -= TickRuntimeWatch;
                return;
            }

            // RuntimeWatch is an expensive diagnostic reader. The editor update hook
            // may stay registered while this window is open, but it must never sample
            // a hidden/background dock tab. Unity's public EditorWindow API cannot
            // prove pixel-level occlusion, so the safe gate is selected page + focus
            // + a valid window rect. Background sampling is intentionally disabled.
            if (!IsRuntimeWatchPageVisible(window))
            {
                runtimeWatchWasFrontmost = false;
                return;
            }

            Page_RuntimeWatch runtimeWatch = window.pageRuntimeWatch;
            if (!runtimeWatchWasFrontmost)
            {
                runtimeWatchWasFrontmost = true;
                runtimeWatch?.RequestForegroundRefresh();
            }

            if (runtimeWatch != null && runtimeWatch.TryAutoRefreshFromEditorTick())
                window.Repaint();
        }

        private static bool IsRuntimeWatchPageVisible(SimpleToolsWindow window)
        {
            if (window == null || window.MenuTree == null)
                return false;

            if (!MenuItems.TryGetValue(MenuPath_RuntimeWatch, out OdinMenuItem runtimeWatchItem)
                || runtimeWatchItem == null
                || window.MenuTree.Selection == null)
            {
                return false;
            }

            if (!window.MenuTree.Selection.Contains(runtimeWatchItem))
                return false;

            Rect windowRect = window.position;
            if (windowRect.width <= 0f || windowRect.height <= 0f || !window.hasFocus)
                return false;

            // focusedWindow can be null during editor focus transitions. In that
            // short state hasFocus remains the least surprising public signal.
            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            return focusedWindow == null || ReferenceEquals(focusedWindow, window);
        }

        private static void ApplyDefaultMenuWidth()
        {
            if (UsingWindow != null)
                UsingWindow.MenuWidth = 245;
        }

        protected override void OnDestroy()
        {
            EditorApplication.update -= TickRuntimeWatch;
            runtimeWatchWasFrontmost = false;
            if (ReferenceEquals(UsingWindow, this))
                UsingWindow = null;
            base.OnDestroy();
        }

        #endregion

        #region 数据滞留与声明
        //根页面名
        public const string PageName_ObservationTools = "01 观察与诊断";
        public const string PageName_SceneBatchTools = "02 场景批处理";
        public const string PageName_AssetPublishTools = "03 资产与发布";
        public const string PageName_ESIntegrationTools = "04 ES 配置与集成";
        public const string PageName_MaintenanceTools = "05 维护与修复";
        public const string PageName_LegacyTools = "90 旧工具与待升级";
        public const string PageName_Overview = "00 工具总览";
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

        private const string MenuPath_RuntimeWatch = PageName_ObservationTools + "/01 运行时观察";
        private const string MenuPath_MaterialReplacement = PageName_SceneBatchTools + "/01 材质批量替换";
        private const string MenuPath_PrefabManagement = PageName_SceneBatchTools + "/02 Prefab实例管理";
        private const string MenuPath_PhysicsAlign = PageName_SceneBatchTools + "/03 物理对齐与布景";
        private const string MenuPath_AnimationBatchSetting = PageName_SceneBatchTools + "/04 动画器批量设置";
        private const string MenuPath_BatchStaticSettingCore = PageName_SceneBatchTools + "/05 批量静态设置";
        private const string MenuPath_BatchRename = PageName_SceneBatchTools + "/06 " + PageName_BatchRename;
        private const string MenuPath_LightingSettings = PageName_SceneBatchTools + "/07 " + PageName_LightingSettings;
        private const string MenuPath_ParticleSystemAdjustment = PageName_SceneBatchTools + "/08 " + PageName_ParticleSystemAdjustment;
        private const string MenuPath_TextureSpriteTool = PageName_AssetPublishTools + "/01 " + PageName_TextureSpriteTool;
        private const string MenuPath_UnityPackageTool = PageName_AssetPublishTools + "/02 " + PageName_UnityPackageTool;
        private const string MenuPath_ObjectPool = PageName_ESIntegrationTools + "/01 " + PageName_ObjectPool;
        private const string MenuPath_TopToolbar = PageName_ESIntegrationTools + "/02 " + PageName_TopToolbar;
        private const string MenuPath_AssetReferenceChecker = PageName_MaintenanceTools + "/01 " + PageName_AssetReferenceChecker;
        private const string MenuPath_SceneOptimization = PageName_MaintenanceTools + "/02 " + PageName_SceneOptimization;
        private const string MenuPath_SceneTextRepair = PageName_MaintenanceTools + "/03 " + PageName_SceneTextRepair;

        private static readonly Dictionary<string, SimpleToolsPagePresentation> PagePresentations
            = new Dictionary<string, SimpleToolsPagePresentation>(StringComparer.Ordinal)
            {
                [PageName_Overview] = new SimpleToolsPagePresentation(
                    "ES 简单工具集",
                    "按当前工作对象选择工具：观察运行状态、批量处理场景、整理资产或维护 ES 配置。",
                    SimpleToolsMaturity.Upgrading,
                    "批处理页会修改场景或资产；进入页面后先确认范围与预览。"),
                [MenuPath_RuntimeWatch] = new SimpleToolsPagePresentation(
                    "RuntimeWatch 运行时观察",
                    "只读查看当前场景注册对象的字段、属性和状态变化。",
                    SimpleToolsMaturity.Upgrading,
                    "自动刷新只在此窗口前台聚焦时运行；方法调用始终需要明确操作。"),
                [MenuPath_MaterialReplacement] = new SimpleToolsPagePresentation(
                    "材质批量替换",
                    "先建立引用预览，再替换场景对象或 Prefab 资产中的材质。",
                    SimpleToolsMaturity.Upgrading,
                    "会写入场景或资产；必须先确认目标来源与命中数量。"),
                [MenuPath_PrefabManagement] = new SimpleToolsPagePresentation(
                    "Prefab 实例管理",
                    "审计当前选区的 Prefab 实例，并执行应用、还原或断开等明确操作。",
                    SimpleToolsMaturity.Upgrading,
                    "会修改场景实例或 Prefab 关联，操作前需确认预览与 Undo 范围。"),
                [MenuPath_PhysicsAlign] = new SimpleToolsPagePresentation(
                    "物理对齐与布景",
                    "对场景对象进行对齐、分布、落地、网格吸附和轻度随机布置。",
                    SimpleToolsMaturity.Upgrading,
                    "会直接修改 Transform 或 RectTransform；先审计选区再执行。"),
                [MenuPath_AnimationBatchSetting] = new SimpleToolsPagePresentation(
                    "动画器批量设置",
                    "按选区规则预览 Animator 配置变更，再批量应用。",
                    SimpleToolsMaturity.Upgrading,
                    "会写入场景对象；需要先确认目标、缺失组件处理和预览结果。"),
                [MenuPath_BatchStaticSettingCore] = new SimpleToolsPagePresentation(
                    "批量静态设置",
                    "审计当前选区的 Static Flags，并按明确规则批量应用。",
                    SimpleToolsMaturity.Upgrading,
                    "会修改场景对象静态标记；大范围操作前应先预览。"),
                [MenuPath_BatchRename] = new SimpleToolsPagePresentation(
                    "批量重命名",
                    "为当前选区建立命名计划，确认冲突后再写入对象名称。",
                    SimpleToolsMaturity.Upgrading,
                    "会修改场景对象名称；预览与执行必须使用同一套规则。"),
                [MenuPath_LightingSettings] = new SimpleToolsPagePresentation(
                    "灯光批量设置",
                    "按当前选区预览 Light 参数，再应用统一的灯光调整。",
                    SimpleToolsMaturity.Upgrading,
                    "会写入场景灯光组件；请先确认包含子对象与筛选范围。"),
                [MenuPath_ParticleSystemAdjustment] = new SimpleToolsPagePresentation(
                    "粒子系统批量调整",
                    "按当前选区预览 ParticleSystem 参数，再批量应用。",
                    SimpleToolsMaturity.Upgrading,
                    "会修改场景粒子组件；先确认目标数、筛选条件和预览。"),
                [MenuPath_TextureSpriteTool] = new SimpleToolsPagePresentation(
                    "纹理与 Sprite 批处理",
                    "批量调整 TextureImporter，或从选中 Sprite 生成独立纹理文件。",
                    SimpleToolsMaturity.Upgrading,
                    "会触发资产重新导入或写入新文件；执行前确认输出目录与冲突策略。"),
                [MenuPath_UnityPackageTool] = new SimpleToolsPagePresentation(
                    "UnityPackage 打包",
                    "预览真实资源清单后导出 UnityPackage 或执行发布打包。",
                    SimpleToolsMaturity.Upgrading,
                    "导出范围由配置决定；先刷新预览，再执行唯一的发布动作。"),
                [MenuPath_ObjectPool] = new SimpleToolsPagePresentation(
                    "对象池与预热配置",
                    "查看运行时池数据，审计预热配置，并将明确配置接入当前场景。",
                    SimpleToolsMaturity.Upgrading,
                    "运行时统计只读；配置接入会写入场景 GameManager。"),
                [MenuPath_TopToolbar] = new SimpleToolsPagePresentation(
                    "场景与资产快捷入口",
                    "维护 ESSceneGlobalData 中的场景和资产快捷入口。",
                    SimpleToolsMaturity.Upgrading,
                    "添加、删除和分组会写入配置资产；场景切换可能触发保存确认。"),
                [MenuPath_AssetReferenceChecker] = new SimpleToolsPagePresentation(
                    "资源引用体检台",
                    "检查资源引用、依赖、未使用候选与资源包外部依赖。",
                    SimpleToolsMaturity.Upgrading,
                    "隔离操作不会直接删除资源，但分析结论仍必须人工复核。"),
                [MenuPath_SceneOptimization] = new SimpleToolsPagePresentation(
                    "场景优化检查",
                    "扫描当前场景的性能与配置问题，预览后执行可恢复的修复。",
                    SimpleToolsMaturity.Upgrading,
                    "修复会写入场景；风险项必须先确认影响范围。"),
                [MenuPath_SceneTextRepair] = new SimpleToolsPagePresentation(
                    "场景文本修复",
                    "扫描并修复场景中的异常文本或丢失引用文本。",
                    SimpleToolsMaturity.Upgrading,
                    "修复会修改场景并建立备份；请先检查扫描报告。")
            };

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
        /// <summary>
        /// 刷新窗口
        /// </summary>
        protected override void OnImGUI()
        {
            if (UsingWindow == null)
            {
                UsingWindow = this;
                ES_LoadData();
            }

            ClampLeakedEditorGuiWidths();
            try
            {
                base.OnImGUI();
            }
            finally
            {
                // A drawer used by the selected page can run during base.OnImGUI.
                // Do not carry an invalid width into Odin's next Layout/Repaint pass.
                ClampLeakedEditorGuiWidths();
            }
        }

        protected override void DrawEditors()
        {
            toolContentScrollPosition.x = 0f;
            toolContentScrollPosition = GUILayout.BeginScrollView(
                toolContentScrollPosition,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar);
            toolContentScrollPosition.x = 0f;
            try
            {
                DrawCurrentPagePresentation();
                using (SimpleToolsPanelUtility.SuppressNestedToolHeaders())
                    base.DrawEditors();
            }
            finally
            {
                GUILayout.EndScrollView();
            }
        }

        private void DrawCurrentPagePresentation()
        {
            if (!TryGetSelectedPresentation(out SimpleToolsPagePresentation presentation))
                return;

            SimpleToolsPanelUtility.DrawToolHeader(
                presentation.Title,
                presentation.Purpose,
                presentation.Maturity,
                presentation.Risk);
        }

        private bool TryGetSelectedPresentation(out SimpleToolsPagePresentation presentation)
        {
            presentation = default;
            if (MenuTree == null || MenuTree.Selection == null)
                return false;

            foreach (KeyValuePair<string, SimpleToolsPagePresentation> pair in PagePresentations)
            {
                if (MenuItems.TryGetValue(pair.Key, out OdinMenuItem item)
                    && item != null
                    && MenuTree.Selection.Contains(item))
                {
                    presentation = pair.Value;
                    return true;
                }
            }

            return false;
        }

        private readonly struct SimpleToolsPagePresentation
        {
            public readonly string Title;
            public readonly string Purpose;
            public readonly SimpleToolsMaturity Maturity;
            public readonly string Risk;

            public SimpleToolsPagePresentation(string title, string purpose, SimpleToolsMaturity maturity, string risk)
            {
                Title = title;
                Purpose = purpose;
                Maturity = maturity;
                Risk = risk;
            }
        }

        private static void ClampLeakedEditorGuiWidths()
        {
            // Third-party material/property inspectors may leave these process-wide
            // values at zero or at the previous inspector's full width. Both cases
            // collapse Odin value fields or make the editor scroll area grow forever.
            if (EditorGUIUtility.fieldWidth < 16f || EditorGUIUtility.fieldWidth > 320f)
                EditorGUIUtility.fieldWidth = 50f;

            if (EditorGUIUtility.labelWidth < 0f || EditorGUIUtility.labelWidth > 480f)
                EditorGUIUtility.labelWidth = 0f;
        }

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

                // Directory order follows actual working object and highest write risk.
                QuickBuildRootMenu(tree, MenuPath_RuntimeWatch, ref pageRuntimeWatch, SdfIconType.Activity);
                QuickBuildRootMenu(tree, MenuPath_MaterialReplacement, ref pageMaterialReplacement, SdfIconType.Palette);
                QuickBuildRootMenu(tree, MenuPath_PrefabManagement, ref pagePrefabManagement, SdfIconType.Box);
                QuickBuildRootMenu(tree, MenuPath_PhysicsAlign, ref pagePhysicsAlign, SdfIconType.Grid);
                QuickBuildRootMenu(tree, MenuPath_AnimationBatchSetting, ref pageAnimationBatchSetting, SdfIconType.Play);
                QuickBuildRootMenu(tree, MenuPath_BatchStaticSettingCore, ref pageBatchStaticSetting, SdfIconType.ToggleOn);
                QuickBuildRootMenu(tree, MenuPath_AssetReferenceChecker, ref pageAssetReferenceChecker, SdfIconType.Search);

                QuickBuildRootMenu(tree, MenuPath_BatchRename, ref pageBatchRename, SdfIconType.Pencil);
                QuickBuildRootMenu(tree, MenuPath_SceneOptimization, ref pageSceneOptimization, SdfIconType.Speedometer);
                QuickBuildRootMenu(tree, MenuPath_LightingSettings, ref pageLightingSettings, SdfIconType.Lightbulb);
                QuickBuildRootMenu(tree, MenuPath_ParticleSystemAdjustment, ref pageParticleSystemAdjustment, SdfIconType.Stars);

                QuickBuildRootMenu(tree, MenuPath_UnityPackageTool, ref pageUnityPackageTool, SdfIconType.Archive);
                QuickBuildRootMenu(tree, MenuPath_TextureSpriteTool, ref pageTextureSpriteTool, SdfIconType.Image);

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

                item.Toggled = item.Name == PageName_ObservationTools || item.Name == PageName_SceneBatchTools;
            }
        }


        #region 页面构建方法
        private void Part_BuildUnityPackageTool(OdinMenuTree tree)
        {
            QuickBuildRootMenu(tree, PageName_UnityPackageTool, ref pageUnityPackageTool, SdfIconType.Archive);
        }

        private void Part_BuildHierarchyTools(OdinMenuTree tree)
        {
            QuickBuildRootMenu(tree, PageName_SceneBatchTools, ref pageHierarchyTools, SdfIconType.LayerForward);
            QuickBuildRootMenu(tree, PageName_SceneBatchTools + "/" + PageName_BatchRename, ref pageBatchRename, SdfIconType.Pencil);
            QuickBuildRootMenu(tree, PageName_SceneBatchTools + "/" + PageName_PhysicsAlign, ref pagePhysicsAlign, SdfIconType.Grid);
            QuickBuildRootMenu(tree, PageName_SceneBatchTools + "/" + PageName_BatchStaticSetting, ref pageBatchStaticSetting, SdfIconType.ToggleOn);
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
                SimpleToolsPanelUtility.DrawSectionTitle("开始工作", "按当前工作对象进入工具；进入批处理页后先确认范围与预览，再执行写入。" );
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

                    EditorGUILayout.LabelField(tip, EditorStyles.wordWrappedMiniLabel, GUILayout.MinWidth(120), GUILayout.MaxWidth(260));
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

