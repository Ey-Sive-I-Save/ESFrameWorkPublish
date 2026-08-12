using Sirenix.OdinInspector;
using System;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>ES 常用工具的统一菜单工作台。</summary>
    public class SimpleToolsWindow : ESMenuTreeWindow<SimpleToolsWindow>
    {
        public const string PageId_Overview = "simple-tools.overview";
        public const string PageId_RuntimeWatch = "simple-tools.runtime-watch";
        public const string PageId_MaterialReplacement = "simple-tools.material-replacement";
        public const string PageId_PrefabManagement = "simple-tools.prefab-management";
        public const string PageId_PhysicsAlign = "simple-tools.physics-align";
        public const string PageId_AnimationBatchSetting = "simple-tools.animation-batch-setting";
        public const string PageId_BatchStaticSetting = "simple-tools.batch-static-setting";
        public const string PageId_BatchRename = "simple-tools.batch-rename";
        public const string PageId_LightingSettings = "simple-tools.lighting-settings";
        public const string PageId_ParticleSystemAdjustment = "simple-tools.particle-system-adjustment";
        public const string PageId_TextureSpriteTool = "simple-tools.texture-sprite";
        public const string PageId_UnityPackageTool = "simple-tools.unity-package";
        public const string PageId_ObjectPool = "simple-tools.object-pool";
        public const string PageId_TopToolbar = "simple-tools.top-toolbar";
        public const string PageId_AssetReferenceChecker = "simple-tools.asset-reference-checker";
        public const string PageId_SceneOptimization = "simple-tools.scene-optimization";
        public const string PageId_SceneTextRepair = "simple-tools.scene-text-repair";

        private const string PathOverview = "00 工具总览";
        private const string PathRuntimeWatch = "01 观察与诊断/01 运行时观察";
        private const string PathMaterialReplacement = "02 场景批处理/01 材质批量替换";
        private const string PathPrefabManagement = "02 场景批处理/02 Prefab 实例管理";
        private const string PathPhysicsAlign = "02 场景批处理/03 物理对齐与布景";
        private const string PathAnimationBatchSetting = "02 场景批处理/04 动画器批量设置";
        private const string PathBatchStaticSetting = "02 场景批处理/05 批量静态设置";
        private const string PathBatchRename = "02 场景批处理/06 批量重命名";
        private const string PathLightingSettings = "02 场景批处理/07 灯光批量设置";
        private const string PathParticleSystemAdjustment = "02 场景批处理/08 粒子系统批量调整";
        private const string PathTextureSpriteTool = "03 资产与发布/01 纹理与 Sprite 批处理";
        private const string PathUnityPackageTool = "03 资产与发布/02 UnityPackage 打包";
        private const string PathObjectPool = "04 ES 配置与集成/01 对象池与预热配置";
        private const string PathTopToolbar = "04 ES 配置与集成/02 场景与资产快捷入口";
        private const string PathAssetReferenceChecker = "05 维护与修复/01 资源引用体检";
        private const string PathSceneOptimization = "05 维护与修复/02 场景优化检查";
        private const string PathSceneTextRepair = "05 维护与修复/03 场景文本修复";

        [NonSerialized] public Page_SimpleToolsOverview pageOverview;
        [NonSerialized] public Page_RuntimeWatch pageRuntimeWatch;
        [NonSerialized] public Page_MaterialReplacement pageMaterialReplacement;
        [NonSerialized] public Page_PrefabManagement pagePrefabManagement;
        [NonSerialized] public Page_PhysicsAlign pagePhysicsAlign;
        [NonSerialized] public Page_AnimationBatchSetting pageAnimationBatchSetting;
        [NonSerialized] public Page_BatchStaticSetting pageBatchStaticSetting;
        [NonSerialized] public Page_BatchRename pageBatchRename;
        [NonSerialized] public Page_LightingSettings pageLightingSettings;
        [NonSerialized] public Page_ParticleSystemAdjustment pageParticleSystemAdjustment;
        [NonSerialized] public Page_TextureSpriteTool pageTextureSpriteTool;
        [NonSerialized] public Page_UnityPackageTool pageUnityPackageTool;
        [NonSerialized] public Page_ObjectPool pageObjectPool;
        [NonSerialized] public Page_TopToolbar pageTopToolbar;
        [NonSerialized] public Page_AssetReferenceChecker pageAssetReferenceChecker;
        [NonSerialized] public Page_SceneOptimization pageSceneOptimization;
        [NonSerialized] public Page_SceneTextRepair pageSceneTextRepair;

        private static bool runtimeWatchWasFrontmost;

        [MenuItem(MenuItemPathDefine.SIMPLE_TOOLS_WINDOW_PATH, false, 0)]
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

        [MenuItem(MenuItemPathDefine.RUNTIME_WATCH_WINDOW_PATH + " %#w", false, 0)]
        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "RuntimeWatch", false, -940)]
        public static void OpenRuntimeWatchFromMenu()
        {
            ESWindowCommandRegistry.RecordOpened("runtime_watch");
            OpenWindow(PageId_RuntimeWatch);
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES 简单工具集", "观察、批处理、资产发布与维护工具");
        }

        protected override string ESWindow_Subtitle => "观察、场景批处理、资产发布与 ES 配置维护";
        protected override Vector2 ESWindow_MinSize => new Vector2(820f, 560f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(1260f, 820f);
        protected override float ESWindow_MenuWidth => 245f;

        protected override void ESWindow_OnHostEnable()
        {
            base.ESWindow_OnHostEnable();
            UsingWindow = this;
            EditorApplication.update -= TickRuntimeWatch;
            EditorApplication.update += TickRuntimeWatch;
        }

        protected override void ESWindow_OnHostDisable()
        {
            EditorApplication.update -= TickRuntimeWatch;
            runtimeWatchWasFrontmost = false;
            base.ESWindow_OnHostDisable();
        }

        protected override void ESWindow_BuildMenuTree(ESMenuTreeBuilder builder)
        {
            pageOverview ??= new Page_SimpleToolsOverview();
            pageRuntimeWatch ??= new Page_RuntimeWatch();
            pageMaterialReplacement ??= new Page_MaterialReplacement();
            pagePrefabManagement ??= new Page_PrefabManagement();
            pagePhysicsAlign ??= new Page_PhysicsAlign();
            pageAnimationBatchSetting ??= new Page_AnimationBatchSetting();
            pageBatchStaticSetting ??= new Page_BatchStaticSetting();
            pageBatchRename ??= new Page_BatchRename();
            pageLightingSettings ??= new Page_LightingSettings();
            pageParticleSystemAdjustment ??= new Page_ParticleSystemAdjustment();
            pageTextureSpriteTool ??= new Page_TextureSpriteTool();
            pageUnityPackageTool ??= new Page_UnityPackageTool();
            pageObjectPool ??= new Page_ObjectPool();
            pageTopToolbar ??= new Page_TopToolbar();
            pageAssetReferenceChecker ??= new Page_AssetReferenceChecker();
            pageSceneOptimization ??= new Page_SceneOptimization();
            pageSceneTextRepair ??= new Page_SceneTextRepair();

            builder.Add(CreateOdinDefinition(
                PageId_Overview, PathOverview, pageOverview,
                "d_UnityEditor.InspectorWindow", "总览 导航 工具 工作台",
                ESMenuTreePageLayout.Standard, 980f));

            ESMenuTreePageDefinition runtimeWatchDefinition = CreateOdinDefinition(
                PageId_RuntimeWatch, PathRuntimeWatch, pageRuntimeWatch,
                "d_UnityEditor.ConsoleWindow", "RuntimeWatch 运行时 观察 字段 属性 方法 诊断",
                ESMenuTreePageLayout.Wide, 0f)
                .AddPageAction(new ESMenuTreePageAction(
                        "runtime-watch.refresh-now",
                        "立即采样",
                        "立即刷新当前 RuntimeWatch 快照。",
                        RefreshRuntimeWatchNow)
                    .WithUnityIcon("Refresh")
                    .WithPriority(100));
            builder.Add(runtimeWatchDefinition);

            builder.Add(CreateOdinDefinition(
                PageId_MaterialReplacement, PathMaterialReplacement, pageMaterialReplacement,
                "d_Material Icon", "材质 批量 替换 场景 Prefab 预览",
                ESMenuTreePageLayout.Wide, 1320f)
                .AddPageAction(CreatePageAction<Page_MaterialReplacement>(
                    "material.refresh-preview", "刷新预览", "重新建立材质替换预览。",
                    page => page.RefreshReplacementPreview(), "Refresh", "材质替换预览已刷新")));
            builder.Add(CreateOdinDefinition(
                PageId_PrefabManagement, PathPrefabManagement, pagePrefabManagement,
                "d_Prefab Icon", "Prefab 实例 应用 还原 断开 审计",
                ESMenuTreePageLayout.Wide, 1320f)
                .AddPageAction(CreatePageAction<Page_PrefabManagement>(
                    "prefab.analyze-context", "分析上下文", "分析当前选区或 Prefab Stage。",
                    page => page.AnalyzeCurrentContext(), "d_Search Icon", "Prefab 上下文分析已完成")));
            builder.Add(CreateOdinDefinition(
                PageId_PhysicsAlign, PathPhysicsAlign, pagePhysicsAlign,
                "d_Grid.BoxTool", "对齐 分布 落地 网格 吸附 Transform 布景",
                ESMenuTreePageLayout.Wide, 1240f));
            builder.Add(CreateOdinDefinition(
                PageId_AnimationBatchSetting, PathAnimationBatchSetting, pageAnimationBatchSetting,
                "Animation.Record", "Animator 动画器 批量 设置 预览",
                ESMenuTreePageLayout.Wide, 1240f));
            builder.Add(CreateOdinDefinition(
                PageId_BatchStaticSetting, PathBatchStaticSetting, pageBatchStaticSetting,
                "Static On", "Static Flags 静态 标记 批量",
                ESMenuTreePageLayout.Wide, 1160f)
                .AddPageAction(CreatePageAction<Page_BatchStaticSetting>(
                    "static.refresh-preview", "刷新预览", "重新计算静态标记变更预览。",
                    page => page.RefreshStaticPreview(), "Refresh", "静态标记预览已刷新")));
            builder.Add(CreateOdinDefinition(
                PageId_BatchRename, PathBatchRename, pageBatchRename,
                "d_TreeEditor.Duplicate", "重命名 命名 规则 批量 冲突",
                ESMenuTreePageLayout.Wide, 1160f)
                .AddPageAction(CreatePageAction<Page_BatchRename>(
                    "rename.refresh-preview", "刷新预览", "重新生成批量重命名计划。",
                    page => page.RefreshRenamePreview(), "Refresh", "重命名计划已刷新")));
            builder.Add(CreateOdinDefinition(
                PageId_LightingSettings, PathLightingSettings, pageLightingSettings,
                "d_Lighting", "Light 灯光 批量 参数 设置",
                ESMenuTreePageLayout.Wide, 1160f));
            builder.Add(CreateOdinDefinition(
                PageId_ParticleSystemAdjustment, PathParticleSystemAdjustment, pageParticleSystemAdjustment,
                "d_PreMatCube", "ParticleSystem 粒子 批量 调整 预览",
                ESMenuTreePageLayout.Wide, 1240f)
                .AddPageAction(CreatePageAction<Page_ParticleSystemAdjustment>(
                    "particles.play", "播放预览", "播放当前目标粒子系统。",
                    page => page.PlayAllParticleSystems(), "PlayButton", "粒子预览已开始",
                    ESEditorFeedbackSoundKind.Confirm, 100))
                .AddPageAction(CreatePageAction<Page_ParticleSystemAdjustment>(
                    "particles.stop", "停止预览", "停止当前目标粒子系统。",
                    page => page.StopAllParticleSystems(), "PauseButton", "粒子预览已停止",
                    ESEditorFeedbackSoundKind.Navigate, 90)));

            builder.Add(CreateOdinDefinition(
                PageId_TextureSpriteTool, PathTextureSpriteTool, pageTextureSpriteTool,
                "d_Texture Icon", "Texture Sprite Importer 纹理 精灵 切分 导入",
                ESMenuTreePageLayout.Wide, 1240f));
            builder.Add(CreateOdinDefinition(
                PageId_UnityPackageTool, PathUnityPackageTool, pageUnityPackageTool,
                "Package Manager", "UnityPackage 打包 导出 发布 资源清单",
                ESMenuTreePageLayout.Wide, 1240f)
                .AddPageAction(CreatePageAction<Page_UnityPackageTool>(
                    "package.collect-selection", "采集选中项", "将 Project 当前选中资产加入打包清单。",
                    page => page.GetSelectedAssets(), "d_Toolbar Plus", "已采集 Project 选中资产")));

            builder.Add(CreateOdinDefinition(
                PageId_ObjectPool, PathObjectPool, pageObjectPool,
                "d_PreMatCube", "对象池 Pool 预热 配置 运行时",
                ESMenuTreePageLayout.Inspector, 1120f));
            builder.Add(CreateOdinDefinition(
                PageId_TopToolbar, PathTopToolbar, pageTopToolbar,
                "d_SceneAsset Icon", "顶部 工具栏 场景 资产 快捷入口",
                ESMenuTreePageLayout.Wide, 1180f));

            builder.Add(CreateOdinDefinition(
                PageId_AssetReferenceChecker, PathAssetReferenceChecker, pageAssetReferenceChecker,
                "d_Search Icon", "资源 引用 依赖 未使用 检查 隔离",
                ESMenuTreePageLayout.Wide, 1320f)
                .AddPageAction(CreatePageAction<Page_AssetReferenceChecker>(
                    "references.refresh-cache", "刷新缓存", "刷新资源引用分析缓存。",
                    page => page.RefreshCache(), "Refresh", "资源引用缓存已刷新")));
            builder.Add(CreateOdinDefinition(
                PageId_SceneOptimization, PathSceneOptimization, pageSceneOptimization,
                "d_UnityEditor.ProfilerWindow", "场景 优化 性能 扫描 修复",
                ESMenuTreePageLayout.Wide, 1320f)
                .AddPageAction(CreatePageAction<Page_SceneOptimization>(
                    "scene-optimization.analyze", "分析场景", "重新扫描当前场景的优化问题。",
                    page => page.QuickAnalyze(), "d_Search Icon", "场景分析已完成")));
            builder.Add(CreateOdinDefinition(
                PageId_SceneTextRepair, PathSceneTextRepair, pageSceneTextRepair,
                "d_TextAsset Icon", "场景 文本 修复 丢失引用 扫描 备份",
                ESMenuTreePageLayout.Wide, 1180f)
                .AddPageAction(CreatePageAction<Page_SceneTextRepair>(
                    "scene-text.scan-open-scenes", "扫描已打开场景", "扫描所有已打开场景的异常文本。",
                    page => page.ScanOpenScenes(), "d_Search Icon", "已完成场景文本扫描")));
        }

        private static ESMenuTreePageDefinition CreateOdinDefinition(
            string stableId,
            string path,
            ESWindowPageBase target,
            string iconName,
            string keywords,
            ESMenuTreePageLayout layout,
            float maxContentWidth)
        {
            return ESMenuTreePageDefinition
                .ForOdin(stableId, path, target)
                .WithUnityIcon(iconName)
                .WithKeywords(keywords)
                .WithLayout(layout, maxContentWidth, 18f)
                .WithSelectionFeedback("已打开" + GetLeafName(path), ESEditorFeedbackSoundKind.Navigate);
        }

        private static string GetLeafName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "工具页面";
            int separator = path.LastIndexOf('/');
            return separator >= 0 ? path.Substring(separator + 1) : path;
        }

        private static ESMenuTreePageAction CreatePageAction<TPage>(
            string id,
            string text,
            string tooltip,
            Action<TPage> execute,
            string iconName,
            string successMessage,
            ESEditorFeedbackSoundKind sound = ESEditorFeedbackSoundKind.Confirm,
            int priority = 100)
            where TPage : class
        {
            return new ESMenuTreePageAction(
                    id,
                    text,
                    tooltip,
                    context =>
                    {
                        TPage page = context.GetOdinTarget<TPage>();
                        if (page == null)
                            throw new InvalidOperationException("当前页面目标不可用：" + typeof(TPage).Name);
                        execute(page);
                    })
                .WithUnityIcon(iconName)
                .WithSuccessFeedback(successMessage, sound)
                .WithPriority(priority);
        }

        private static void RefreshRuntimeWatchNow(ESMenuTreePageContext context)
        {
            Page_RuntimeWatch page = context.GetOdinTarget<Page_RuntimeWatch>();
            if (page == null)
                return;

            page.RefreshNow();
            context.Notify(
                "RuntimeWatch 快照已刷新",
                ESMenuTreePageStatus.Ready,
                ESEditorFeedbackSoundKind.Confirm,
                false);
        }

        private static void TickRuntimeWatch()
        {
            SimpleToolsWindow window = UsingWindow;
            if (window == null)
                return;

            if (!IsRuntimeWatchPageVisible(window))
            {
                runtimeWatchWasFrontmost = false;
                return;
            }

            Page_RuntimeWatch runtimeWatch = window.pageRuntimeWatch;
            if (runtimeWatch == null)
            {
                runtimeWatchWasFrontmost = false;
                return;
            }

            if (!runtimeWatchWasFrontmost)
            {
                runtimeWatchWasFrontmost = true;
                runtimeWatch.RequestForegroundRefresh();
            }

            if (runtimeWatch.TryAutoRefreshFromEditorTick())
                window.Repaint();
        }

        private static bool IsRuntimeWatchPageVisible(SimpleToolsWindow window)
        {
            if (window == null
                || !string.Equals(window.ESWindow_SelectedPageId, PageId_RuntimeWatch, StringComparison.Ordinal)
                || !window.hasFocus)
            {
                return false;
            }

            Rect windowRect = window.position;
            if (windowRect.width <= 0f || windowRect.height <= 0f)
                return false;

            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            return focusedWindow == null || ReferenceEquals(focusedWindow, window);
        }

        [Serializable]
        public class Page_SimpleToolsOverview : ESWindowPageBase
        {
            [OnInspectorGUI]
            private void DrawOverview()
            {
                SimpleToolsPanelUtility.DrawToolHeader(
                    "ES 简单工具集",
                    "按当前工作对象选择工具：观察运行状态、批量处理场景、整理资产或维护 ES 配置。",
                    SimpleToolsMaturity.Upgrading,
                    "批处理页会修改场景或资产；进入页面后先确认范围与预览。");
                SimpleToolsPanelUtility.DrawSectionTitle(
                    "开始工作",
                    "按当前工作对象进入工具；进入批处理页后先确认范围与预览，再执行写入。");
                DrawQuickOpenRow(
                    ("看运行时数据", PageId_RuntimeWatch, "无侵入观察字段、属性和方法。"),
                    ("批量换材质", PageId_MaterialReplacement, "先预览命中，再替换场景或 Prefab 资产。"));
                DrawQuickOpenRow(
                    ("整理 Prefab", PageId_PrefabManagement, "分析实例、应用、还原、断开连接。"),
                    ("布景和落地", PageId_PhysicsAlign, "落地、归整、分布、对齐和轻随机。"));
                DrawQuickOpenRow(
                    ("批量改名", PageId_BatchRename, "保存命名方案，快速复用上次规则。"),
                    ("找资源引用", PageId_AssetReferenceChecker, "查未使用、被谁引用、依赖了谁。"));
                DrawQuickOpenRow(
                    ("导出包", PageId_UnityPackageTool, "确认资源清单后再导出 UnityPackage。"),
                    ("切 Sprite", PageId_TextureSpriteTool, "批量设置纹理导入和精灵切分。"));
            }

            private static void DrawQuickOpenRow(
                (string label, string stableId, string tip) left,
                (string label, string stableId, string tip) right)
            {
                if (EditorGUIUtility.currentViewWidth < 760f)
                {
                    DrawQuickOpenButton(left.label, left.stableId, left.tip);
                    DrawQuickOpenButton(right.label, right.stableId, right.tip);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    DrawQuickOpenButton(left.label, left.stableId, left.tip);
                    GUILayout.Space(8f);
                    DrawQuickOpenButton(right.label, right.stableId, right.tip);
                }
            }

            private static void DrawQuickOpenButton(string label, string stableId, string tip)
            {
                bool available = UsingWindow != null;
                using (new EditorGUILayout.HorizontalScope(GUILayout.MinHeight(26f), GUILayout.ExpandWidth(true)))
                {
                    using (new EditorGUI.DisabledScope(!available))
                    {
                        if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(104f), GUILayout.Height(22f)))
                            UsingWindow.ESWindow_TrySelectPage(stableId);
                    }

                    EditorGUILayout.LabelField(
                        tip,
                        EditorStyles.wordWrappedMiniLabel,
                        GUILayout.MinWidth(120f),
                        GUILayout.MaxWidth(260f));
                }
            }
        }
    }
}
