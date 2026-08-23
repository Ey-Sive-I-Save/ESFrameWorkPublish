using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES.EditorInternal
{
    /// <summary>
    /// Read-only health overview for the ES editor presentation layer.
    /// Refreshing this window never scans assets, creates GlobalData, or changes the scene.
    /// Any mutation is exposed as a separately labelled user action.
    /// </summary>
    public sealed class ESEditorHealthWindow : ESSinglePageIMGUIWindow<ESEditorHealthWindow>
    {
        private const string WindowMenuPath
            = MenuItemPathDefine.VALIDATION_EDITOR_HEALTH_PATH + "打开 ES 编辑器健康检查";
        private const string BoundaryRootName = "ES 边界测试层级";
        private static readonly Vector2 MaximumWindowSize = new Vector2(1400f, 1000f);

        private readonly List<HealthCheck> checks = new List<HealthCheck>(6);
        private Vector2 scrollPosition;
        private double lastRefreshTime;
        private bool hostActive;
        private bool refreshQueued;

        [MenuItem(WindowMenuPath, false, 20)]
        private static void Open()
        {
            var window = GetWindow<ESEditorHealthWindow>();
            window.titleContent = new GUIContent("ES 健康检查");
            window.minSize = new Vector2(500f, 420f);
            window.maxSize = MaximumWindowSize;
            window.Show();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES 健康检查", "只读检查编辑器主题、缓存与绘制基础状态");
        }
        public override string ESWindow_PresentationShortTitle => "健康";

        protected override string ESWindow_Subtitle => "编辑器 Presentation 与工具链健康状态";
        protected override Vector2 ESWindow_MinSize => new Vector2(500f, 420f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(760f, 620f);
        protected override string ESWindow_PageStableId => "editor.health";
        protected override string ESWindow_PageTitle => "编辑器健康检查";
        protected override string ESWindow_PageKeywords => "编辑器 健康 主题 缓存 多态 Drawer Presentation";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "health.refresh",
                    "刷新检查",
                    "重新读取当前编辑器状态；不会扫描资产或写入项目。",
                    context =>
                    {
                        RefreshChecks();
                        context.SetStatus("编辑器健康状态已刷新");
                    })
                .WithUnityIcon("Refresh")
                .WithPriority(100));
        }

        protected override void ESWindow_OnHostEnable()
        {
            hostActive = true;
            refreshQueued = false;
            UnqueueRefresh();
            minSize = ESWindow_MinSize;
            maxSize = MaximumWindowSize;
            RefreshChecks();
        }

        protected override void ESWindow_OnHostDisable()
        {
            hostActive = false;
            UnqueueRefresh();
        }

        private void OnFocus()
        {
            RefreshChecks();
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            DrawTitle();
            GUILayout.Label(
                "上次检查 " + FormatElapsedTime(EditorApplication.timeSinceStartup - lastRefreshTime),
                EditorStyles.miniLabel);

            if (checks.Count == 0)
                RefreshChecks();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            try
            {
                DrawSummary();
                EditorGUILayout.Space(6f);
                for (int i = 0; i < checks.Count; i++)
                    DrawCheck(checks[i]);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawTitle()
        {
            Rect titleRect = GUILayoutUtility.GetRect(0f, 54f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(titleRect, ESEditorPresentation.GetDepthBackground(0));
                ESEditorPresentation.DrawFrame(titleRect, ESEditorPresentation.GetDepthAccent(0));
                EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.y, 3f, titleRect.height), ESEditorPresentation.LogicSteelBlue);
                EditorGUI.DrawRect(new Rect(titleRect.x + 3f, titleRect.yMax - 1f, titleRect.width - 3f, 1f), ESEditorPresentation.LogicGold);
            }

            GUI.Label(
                new Rect(titleRect.x + 12f, titleRect.y + 7f, titleRect.width - 24f, 22f),
                "ES 编辑器健康检查",
                ESEditorPresentation.HeaderStyle);
            GUI.Label(
                new Rect(titleRect.x + 12f, titleRect.y + 30f, titleRect.width - 24f, 17f),
                "只读诊断 · 不会扫描后写入资产、修改场景或改变绘制方案",
                ESEditorPresentation.MetaStyle);
        }

        private void DrawSummary()
        {
            int ready = 0;
            int attention = 0;
            for (int i = 0; i < checks.Count; i++)
            {
                if (checks[i].Status == ESStatusKind.Ready)
                    ready++;
                else if (checks[i].Status == ESStatusKind.Warning || checks[i].Status == ESStatusKind.Error)
                    attention++;
            }

            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            try
            {
                string summary = attention > 0
                    ? "需要关注 " + attention + " 项 · 已就绪 " + ready + " 项"
                    : "当前没有阻断项 · 已就绪 " + ready + " 项";
                ESFieldRow.DrawStatus(
                    GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true)),
                    attention > 0 ? ESStatusKind.Warning : ESStatusKind.Ready,
                    summary,
                    ESEditorPresentation.HeaderStyle);
                GUILayout.Label(
                    "提示项不等于错误：例如类型目录冷缓存和未安装测试层级都是正常的按需状态。",
                    ESEditorPresentation.MetaStyle);
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawCheck(HealthCheck check)
        {
            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            try
            {
                Rect headerRect = GUILayoutUtility.GetRect(0f, 23f, GUILayout.ExpandWidth(true));
                ESFieldRow.DrawStatus(headerRect, check.Status, check.Title, ESEditorPresentation.HeaderStyle);
                GUILayout.Label(check.Summary, ESEditorPresentation.MetaStyle);

                if (!string.IsNullOrEmpty(check.NextStep))
                {
                    EditorGUILayout.Space(2f);
                    GUILayout.Label("处理方式：" + check.NextStep, ESEditorPresentation.SubtitleStyle);
                }

                if (check.Action == null)
                    return;

                if (!string.IsNullOrEmpty(check.ActionHint))
                    GUILayout.Label(check.ActionHint, ESEditorPresentation.MetaStyle);

                EditorGUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                try
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(check.ActionLabel, GUILayout.MinWidth(112f), GUILayout.Height(23f)))
                    {
                        check.Action();
                        QueueRefresh();
                    }
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(4f);
        }

        private void RefreshChecks()
        {
            checks.Clear();
            AddWindowFoundationCheck();
            AddThemeCheck();
            AddTypeCatalogCheck();
            AddPolymorphicRendererCheck();
            AddSectionNavigatorCheck();
            AddBoundaryFixtureCheck();
            lastRefreshTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void QueueRefresh()
        {
            if (!hostActive || refreshQueued || this == null)
                return;

            refreshQueued = true;
            EditorApplication.delayCall -= HandleDelayedRefresh;
            EditorApplication.delayCall += HandleDelayedRefresh;
        }

        private void HandleDelayedRefresh()
        {
            // Remove the delegate even when Unity invokes delayCall more than once during
            // a reload/inspector update boundary. This keeps the global event from retaining
            // this window instance after the one-shot refresh has completed.
            EditorApplication.delayCall -= HandleDelayedRefresh;
            refreshQueued = false;
            if (!hostActive || this == null)
                return;

            RefreshChecks();
        }

        private void UnqueueRefresh()
        {
            EditorApplication.delayCall -= HandleDelayedRefresh;
            refreshQueued = false;
        }

        private void AddWindowFoundationCheck()
        {
            ESWindowPresentationHealthSnapshot snapshot =
                ESEditorPresentation.CaptureWindowHealthSnapshot();
            ESStatusKind status = snapshot.HasIssues
                ? ESStatusKind.Warning
                : snapshot.LiveWindowCount > 0
                    ? ESStatusKind.Ready
                    : ESStatusKind.Info;
            string summary = snapshot.LiveWindowCount == 0
                ? "当前没有活动的 ES Presentation 窗口。"
                : "活动 " + snapshot.LiveWindowCount
                    + " · 支持休眠 " + snapshot.SleepSupportedCount
                    + " · 已收起 " + snapshot.SleepingCount
                    + " · 过渡中 " + snapshot.TransitioningCount
                    + " · 重复实例 " + snapshot.DuplicateWindowInstanceCount
                    + " · 待绑定父窗口 " + snapshot.PendingOwnerCount;
            string nextStep;
            if (snapshot.DuplicateWindowInstanceCount > 0)
            {
                nextStep = "发现 " + snapshot.DuplicateWindowInstanceCount
                    + " 个同具体类型的额外窗口实例；首个问题："
                    + snapshot.FirstIssueWindowType
                    + "。健康检查不会猜测关闭对象；应关闭多余实例并修复其打开入口。";
            }
            else if (snapshot.MissingSystemHostCount > 0)
            {
                nextStep = "有 " + snapshot.MissingSystemHostCount
                    + " 个支持休眠的窗口未声明 System 动作宿主；首个问题："
                    + snapshot.FirstIssueWindowType + "。应修复窗口接入，禁止回退为覆盖式按钮。";
            }
            else if (snapshot.GeometryMismatchCount > 0)
            {
                nextStep = "有 " + snapshot.GeometryMismatchCount
                    + " 个已稳定休眠窗口的原生尺寸与视觉状态不一致；首个问题："
                    + snapshot.FirstIssueWindowType + "。应执行 ReloadDomain 与真实窗口恢复验收。";
            }
            else if (snapshot.StaleEntryCount > 0)
            {
                nextStep = "发现 " + snapshot.StaleEntryCount
                    + " 个失效绑定或父窗口待绑定记录；首个问题："
                    + snapshot.FirstIssueWindowType + "。应检查解绑和窗口销毁路径。";
            }
            else
            {
                nextStep = snapshot.BindingSlotCount == 0
                    ? "打开任意采用 ES 新版底层的窗口后再次刷新即可查看其生命周期状态。"
                    : "无需处理；该检查只读取当前绑定表，不扫描全部 EditorWindow。";
            }

            checks.Add(new HealthCheck(
                "ES 窗口生命周期",
                status,
                summary,
                nextStep,
                null,
                null,
                null));
        }

        private void AddThemeCheck()
        {
            ESGlobalEditorTheme theme = ESGlobalEditorThemeMenu.LoadTheme();
            if (theme != null)
            {
                checks.Add(new HealthCheck(
                    "ES 编辑器主题",
                    ESStatusKind.Ready,
                    "已加载项目共享主题：" + theme.name,
                    "主题变更会重绘已接入 ES Presentation 的窗口与 Inspector。",
                    "定位主题资产",
                    "定位主题",
                    () =>
                    {
                        Selection.activeObject = theme;
                        EditorGUIUtility.PingObject(theme);
                    }));
                return;
            }

            checks.Add(new HealthCheck(
                "ES 编辑器主题",
                ESStatusKind.Info,
                "尚未创建项目主题资产；当前继续使用内存默认色板。",
                "这不是错误。若要共享项目色板、密度和分区副标题配置，再创建 GlobalData。",
                "点击“创建默认主题”会显式创建一个 GlobalData 资产。",
                "创建默认主题",
                CreateDefaultTheme));
        }

        private static void CreateDefaultTheme()
        {
            ESGlobalEditorTheme theme = ESGlobalEditorThemeMenu.EnsureDefaultTheme();
            ESGlobalEditorTheme.Instance = theme;
            ESEditorPresentation.InvalidateTheme();
            Selection.activeObject = theme;
            EditorGUIUtility.PingObject(theme);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void AddTypeCatalogCheck()
        {
            ESTypeCatalog.CacheDiagnostics diagnostics = ESTypeCatalog.GetCacheDiagnostics();
            if (!diagnostics.IsWarm)
            {
                checks.Add(new HealthCheck(
                    "多态类型目录缓存",
                    ESStatusKind.Info,
                    "冷缓存：尚未打开过多态类型选择器。",
                    "这是按需行为；健康检查不会为了取数而触发 TypeCache 扫描。",
                    "打开任意 SerializeReference 类型选择器后，目录会按声明基类延迟建立。",
                    null,
                    null));
                return;
            }

            checks.Add(new HealthCheck(
                "多态类型目录缓存",
                ESStatusKind.Ready,
                "已缓存 " + diagnostics.CatalogCount + " 个声明基类、"
                + diagnostics.DescriptorCount + " 个类型描述符。",
                "缓存世代 #" + diagnostics.Generation + "；程序集流或手动清除后会延迟重建。",
                "清除只影响内存缓存，不会修改 SerializeReference 数据。",
                "清除缓存",
                () =>
                {
                    ESTypeCatalog.Clear();
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }));
        }

        private void AddPolymorphicRendererCheck()
        {
            string renderer = ESPolymorphicReferencePreferences.CurrentDisplayName;
            string collectionRenderer = ESPolymorphicReferencePreferences.CurrentCollectionDisplayName;
            bool usesESRenderer = ESPolymorphicReferencePreferences.UseESRenderer;
            checks.Add(new HealthCheck(
                "多态引用绘制方案",
                usesESRenderer ? ESStatusKind.Ready : ESStatusKind.Info,
                "单体：" + renderer + " · 集合：" + collectionRenderer,
                "方案是个人 EditorPrefs 项目默认值；字段可用 ESCollectionDrawStyle 做局部覆盖，均不写入资产。",
                "局部覆盖优先于项目默认；集合增删、拖拽、序列化、Undo 和运行时数据不受影响。",
                "切换集合方案",
                ESPolymorphicReferencePreferences.ShowCollectionMenu));
        }

        private void AddSectionNavigatorCheck()
        {
            checks.Add(new HealthCheck(
                "配置目录状态隔离",
                ESStatusKind.Ready,
                "ESEditorSection 的选中分区按目标对象集、类型和 Navigator ID 独立保存。",
                "缓存属性在绘制前会确认仍属于当前 Odin PropertyTree，避免重编译或切换对象后的陈旧引用。",
                "无需手动处理；切换对象或域重载后会按当前 PropertyTree 重建。",
                null,
                null));
        }

        private void AddBoundaryFixtureCheck()
        {
            bool exists = FindBoundaryRoot(SceneManager.GetActiveScene()) != null;
            if (exists)
            {
                checks.Add(new HealthCheck(
                    "多态边界测试层级",
                    ESStatusKind.Ready,
                    "当前场景已安装“" + BoundaryRootName + "”。",
                    "可验证单对象、多目标 2 个、多目标 10 个、超出 10 个和类型不一致状态。",
                    "测试层级只用于编辑器验收，不会参与运行时功能。",
                    "定位层级",
                    SelectBoundaryRoot));
                return;
            }

            checks.Add(new HealthCheck(
                "多态边界测试层级",
                ESStatusKind.Info,
                "当前场景未安装边界测试层级。",
                "这不是项目配置缺失；仅在修改多态 Drawer 后需要使用。",
                EditorApplication.isPlayingOrWillChangePlaymode
                    ? "播放模式中不创建场景测试对象。"
                    : "点击后会在当前场景显式创建可 Undo 的独立测试层级。",
                EditorApplication.isPlayingOrWillChangePlaymode ? null : "创建测试层级",
                EditorApplication.isPlayingOrWillChangePlaymode ? null : CreateBoundaryFixture));
        }

        private static GameObject FindBoundaryRoot(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && string.Equals(roots[i].name, BoundaryRootName, StringComparison.Ordinal))
                    return roots[i];
            }

            return null;
        }

        private static void SelectBoundaryRoot()
        {
            GameObject root = FindBoundaryRoot(SceneManager.GetActiveScene());
            if (root == null)
                return;

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }

        private static void CreateBoundaryFixture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!EditorApplication.ExecuteMenuItem(
                    MenuItemPathDefine.VALIDATION_EDITOR_EXTENSION_TESTS_PATH + "ES 编辑器扩展/创建多态边界测试层级"))
            {
                Debug.LogWarning("[ES] 无法执行“创建多态边界测试层级”菜单。请检查 ES 编辑器程序集是否已完成编译。");
            }
        }

        private static string FormatElapsedTime(double seconds)
        {
            if (seconds < 1d)
                return "刚刚";
            if (seconds < 60d)
                return Mathf.FloorToInt((float)seconds) + " 秒前";
            return Mathf.FloorToInt((float)(seconds / 60d)) + " 分钟前";
        }

        private sealed class HealthCheck
        {
            public readonly string Title;
            public readonly ESStatusKind Status;
            public readonly string Summary;
            public readonly string NextStep;
            public readonly string ActionHint;
            public readonly string ActionLabel;
            public readonly Action Action;

            public HealthCheck(
                string title,
                ESStatusKind status,
                string summary,
                string nextStep,
                string actionHint,
                string actionLabel,
                Action action)
            {
                Title = title;
                Status = status;
                Summary = summary;
                NextStep = nextStep;
                ActionHint = actionHint;
                ActionLabel = actionLabel;
                Action = action;
            }
        }
    }
}
