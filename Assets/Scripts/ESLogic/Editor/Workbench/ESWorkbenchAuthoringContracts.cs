#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    public enum ESWorkbenchLayoutPreset : byte
    {
        Authoring,
        Focus,
        Content,
        Diagnostics,
        Production,
        Custom = byte.MaxValue
    }

    public enum ESWorkbenchContentViewMode : byte
    {
        List,
        Grid
    }

    public enum ESWorkbenchContentSortMode : byte
    {
        Recommended,
        Priority,
        Name,
        Recent,
        MostUsed,
        Type
    }

    public enum ESWorkbenchContentScope : byte
    {
        All,
        Favorites,
        Recent,
        Recommended
    }

    /// <summary>底部生产通道的内容密度。宿主据此分配高度，不把空状态当成完整生产面板。</summary>
    public enum ESWorkbenchBottomPanelDensity : byte
    {
        Empty,
        Compact,
        Normal,
        Expanded
    }

    [Serializable]
    public sealed class ESWorkbenchContentPresetSelectionState
    {
        public string objectId = string.Empty;
        public string presetId = string.Empty;
    }

    [Serializable]
    public sealed class ESWorkbenchLayoutState
    {
        public int layoutSchemaVersion = 6;
        public ESWorkbenchLayoutPreset layoutPreset = ESWorkbenchLayoutPreset.Authoring;
        public float leftPaneWidth = 320f;
        public float inspectorPaneWidth = 320f;
        public string activeLeftTab = "objects";
        public string activeContentKind = "all";
        public string activeContentCategory = "全部";
        public ESWorkbenchContentViewMode contentViewMode = ESWorkbenchContentViewMode.List;
        public ESWorkbenchContentSortMode contentSortMode = ESWorkbenchContentSortMode.Type;
        public ESWorkbenchContentScope contentScope = ESWorkbenchContentScope.All;
        public float contentBatchSpacing = 4f;
        public string activeDocument = "authoring";
        public string activeAuthoringModeId = "terrain";
        public string activeViewportId = string.Empty;
        public string activeToolId = string.Empty;
        public string selectedStableId = string.Empty;
        public string selectedKind = string.Empty;
        public string selectedAssetGuid = string.Empty;
        public bool leftPaneVisible = true;
        public bool inspectorPaneVisible = true;
        public string compactSidePane = "left";
        public bool responsiveLayoutInitialized;
        public bool hierarchyExpansionInitialized;
        public bool bottomDrawerExpanded;
        public float bottomDrawerHeight = 220f;
        public bool bottomDrawerUserSized;
        public string activeBottomTab = "problems";
        public List<string> expandedHierarchyIds = new List<string>();
        public List<string> hiddenHierarchyIds = new List<string>();
        public List<string> lockedHierarchyIds = new List<string>();
        public List<string> selectedContentIds = new List<string>();
        public List<string> expandedContentCategoryPaths = new List<string>();
        public List<ESWorkbenchContentPresetSelectionState> contentPresetSelections =
            new List<ESWorkbenchContentPresetSelectionState>();
        public List<ESWorkbenchViewportLayoutState> viewportStates = new List<ESWorkbenchViewportLayoutState>();

        internal ESWorkbenchViewportLayoutState GetOrCreateViewportState(string viewportId)
        {
            viewportStates ??= new List<ESWorkbenchViewportLayoutState>();
            string stableId = viewportId ?? string.Empty;
            ESWorkbenchViewportLayoutState state = viewportStates.Find(value => value != null && value.viewportId == stableId);
            if (state != null) return state;
            state = new ESWorkbenchViewportLayoutState { viewportId = stableId };
            viewportStates.Add(state);
            return state;
        }
    }

    public enum ESWorkbenchResponsiveTier : byte
    {
        Wide,
        Compact,
        Narrow
    }

    /// <summary>统一内容目录中的作者内容类型。它决定发现和交互语义，不替代领域稳定身份。</summary>
    public enum ESWorkbenchContentKind : byte
    {
        General,
        Prefab,
        Brush,
        SceneTemplate,
        RegionTemplate,
        Terrain,
        Material,
        Vegetation,
        WaterWeather,
        Navigation,
        Collision,
        Streaming,
        Gameplay
    }

    /// <summary>内容从目录拖入作者视口时的默认动作。视口仍必须执行领域预检。</summary>
    public enum ESWorkbenchContentDragMode : byte
    {
        Place,
        ActivateTool,
        ApplyTemplate,
        CreateRegion,
        InspectOnly
    }

    public enum ESWorkbenchVisualTheme : byte
    {
        Light,
        Dark
    }

    public readonly struct ESWorkbenchVisualEnvironment
    {
        public ESWorkbenchVisualEnvironment(
            float windowWidth,
            float windowHeight,
            float centerWidth,
            float pixelsPerPoint,
            ESWorkbenchVisualTheme theme,
            bool longChineseContent = false)
        {
            WindowWidth = windowWidth;
            WindowHeight = windowHeight;
            CenterWidth = centerWidth;
            PixelsPerPoint = pixelsPerPoint;
            Theme = theme;
            LongChineseContent = longChineseContent;
        }

        public float WindowWidth { get; }
        public float WindowHeight { get; }
        public float CenterWidth { get; }
        public float PixelsPerPoint { get; }
        public ESWorkbenchVisualTheme Theme { get; }
        public bool LongChineseContent { get; }
    }

    public readonly struct ESWorkbenchVisualValidationResult
    {
        public ESWorkbenchVisualValidationResult(
            ESWorkbenchResponsiveTier tier,
            bool windowMinimumSatisfied,
            bool centerProtected,
            bool highDpiSupported,
            string summary)
        {
            Tier = tier;
            WindowMinimumSatisfied = windowMinimumSatisfied;
            CenterProtected = centerProtected;
            HighDpiSupported = highDpiSupported;
            Summary = summary ?? string.Empty;
        }

        public ESWorkbenchResponsiveTier Tier { get; }
        public bool WindowMinimumSatisfied { get; }
        public bool CenterProtected { get; }
        public bool HighDpiSupported { get; }
        public bool LayoutContractPassed => WindowMinimumSatisfied && CenterProtected && HighDpiSupported;
        public string Summary { get; }
    }

    /// <summary>当前真实窗口与一条视觉验收场景的匹配结果；不负责切换主题、DPI 或窗口尺寸。</summary>
    public readonly struct ESWorkbenchVisualScenarioMatch
    {
        public ESWorkbenchVisualScenarioMatch(bool passed, string summary)
        {
            Passed = passed;
            Summary = summary ?? string.Empty;
        }

        public bool Passed { get; }
        public string Summary { get; }
    }

    public sealed class ESWorkbenchVisualValidationScenario
    {
        public ESWorkbenchVisualValidationScenario(
            string scenarioId,
            float width,
            float height,
            float pixelsPerPoint,
            ESWorkbenchVisualTheme theme,
            bool longChineseContent,
            ESWorkbenchResponsiveTier expectedTier)
        {
            ScenarioId = scenarioId ?? string.Empty;
            Width = width;
            Height = height;
            PixelsPerPoint = pixelsPerPoint;
            Theme = theme;
            LongChineseContent = longChineseContent;
            ExpectedTier = expectedTier;
        }

        public string ScenarioId { get; }
        public float Width { get; }
        public float Height { get; }
        public float PixelsPerPoint { get; }
        public ESWorkbenchVisualTheme Theme { get; }
        public bool LongChineseContent { get; }
        public ESWorkbenchResponsiveTier ExpectedTier { get; }
    }

    /// <summary>工作台外壳的响应式约束。领域可覆盖参数，但布局降级与功能保留由底座统一执行。</summary>
    public sealed class ESWorkbenchResponsiveLayoutPolicy
    {
        public ESWorkbenchResponsiveLayoutPolicy(
            float minimumWindowWidth = 980f,
            float minimumWindowHeight = 640f,
            float wideBreakpoint = 1180f,
            float narrowBreakpoint = 980f,
            float minimumCenterWidth = 600f,
            float minimumCenterHeight = 340f,
            float minimumLeftPaneWidth = 280f,
            float minimumInspectorPaneWidth = 280f,
            float maximumLeftPaneRatio = 0.30f,
            float maximumInspectorPaneRatio = 0.28f,
            float maximumBottomDrawerRatio = 0.34f,
            float preferredLeftPaneWidth = 320f,
            float maximumLeftPaneWidth = 420f,
            float preferredInspectorPaneWidth = 320f,
            float maximumInspectorPaneWidth = 420f,
            float collapsedBottomDrawerHeight = 32f,
            float compactBottomDrawerHeight = 96f,
            float minimumBottomDrawerHeight = 112f,
            float preferredBottomDrawerHeight = 220f,
            float maximumBottomDrawerHeight = 320f,
            float minimumUsableWindowWidth = 560f,
            float minimumUsableWindowHeight = 360f,
            float floatingWindowSafeInset = 8f)
        {
            MinimumCenterWidth = Mathf.Max(280f, minimumCenterWidth);
            MinimumCenterHeight = Mathf.Max(220f, minimumCenterHeight);
            MinimumLeftPaneWidth = Mathf.Max(160f, minimumLeftPaneWidth);
            MinimumInspectorPaneWidth = Mathf.Max(200f, minimumInspectorPaneWidth);
            MaximumLeftPaneWidth = Mathf.Max(MinimumLeftPaneWidth, maximumLeftPaneWidth);
            MaximumInspectorPaneWidth = Mathf.Max(MinimumInspectorPaneWidth, maximumInspectorPaneWidth);
            PreferredLeftPaneWidth = Mathf.Clamp(
                preferredLeftPaneWidth, MinimumLeftPaneWidth, MaximumLeftPaneWidth);
            PreferredInspectorPaneWidth = Mathf.Clamp(
                preferredInspectorPaneWidth, MinimumInspectorPaneWidth, MaximumInspectorPaneWidth);
            CollapsedBottomDrawerHeight = Mathf.Clamp(collapsedBottomDrawerHeight, 30f, 48f);
            CompactBottomDrawerHeight = Mathf.Clamp(
                compactBottomDrawerHeight,
                Mathf.Max(72f, CollapsedBottomDrawerHeight + 24f),
                144f);
            MinimumBottomDrawerHeight = Mathf.Max(CompactBottomDrawerHeight, minimumBottomDrawerHeight);
            MaximumBottomDrawerHeight = Mathf.Max(
                MinimumBottomDrawerHeight, maximumBottomDrawerHeight);
            PreferredBottomDrawerHeight = Mathf.Clamp(
                preferredBottomDrawerHeight, MinimumBottomDrawerHeight, MaximumBottomDrawerHeight);
            MaximumLeftPaneRatio = Mathf.Clamp(maximumLeftPaneRatio, 0.18f, 0.4f);
            MaximumInspectorPaneRatio = Mathf.Clamp(maximumInspectorPaneRatio, 0.22f, 0.45f);
            MaximumBottomDrawerRatio = Mathf.Clamp(maximumBottomDrawerRatio, 0.24f, 0.48f);
            MinimumWindowWidth = Mathf.Max(
                680f,
                minimumWindowWidth,
                MinimumCenterWidth + Mathf.Min(MinimumLeftPaneWidth, MinimumInspectorPaneWidth) + 12f);
            MinimumWindowHeight = Mathf.Max(
                480f,
                minimumWindowHeight,
                MinimumCenterHeight + MinimumBottomDrawerHeight + 60f);
            MinimumUsableWindowWidth = Mathf.Clamp(
                minimumUsableWindowWidth,
                420f,
                MinimumWindowWidth);
            MinimumUsableWindowHeight = Mathf.Clamp(
                minimumUsableWindowHeight,
                320f,
                MinimumWindowHeight);
            FloatingWindowSafeInset = Mathf.Clamp(floatingWindowSafeInset, 0f, 32f);
            NarrowBreakpoint = Mathf.Max(560f, narrowBreakpoint);
            WideBreakpoint = Mathf.Max(
                NarrowBreakpoint + 120f,
                wideBreakpoint,
                MinimumCenterWidth + MinimumLeftPaneWidth + MinimumInspectorPaneWidth + 12f);
        }

        public float MinimumWindowWidth { get; }
        public float MinimumWindowHeight { get; }
        /// <summary>高 DPI 或小屏环境仍须保持主路径可达的降级下限；不是理想商业尺寸。</summary>
        public float MinimumUsableWindowWidth { get; }
        public float MinimumUsableWindowHeight { get; }
        public float FloatingWindowSafeInset { get; }
        public float WideBreakpoint { get; }
        public float NarrowBreakpoint { get; }
        public float MinimumCenterWidth { get; }
        public float MinimumCenterHeight { get; }
        public float MinimumLeftPaneWidth { get; }
        public float MinimumInspectorPaneWidth { get; }
        public float PreferredLeftPaneWidth { get; }
        public float MaximumLeftPaneWidth { get; }
        public float PreferredInspectorPaneWidth { get; }
        public float MaximumInspectorPaneWidth { get; }
        public float CollapsedBottomDrawerHeight { get; }
        public float CompactBottomDrawerHeight { get; }
        public float MinimumBottomDrawerHeight { get; }
        public float PreferredBottomDrawerHeight { get; }
        public float MaximumBottomDrawerHeight { get; }
        public float MaximumLeftPaneRatio { get; }
        public float MaximumInspectorPaneRatio { get; }
        public float MaximumBottomDrawerRatio { get; }

        public ESWorkbenchResponsiveTier ResolveTier(float width)
        {
            if (width >= WideBreakpoint) return ESWorkbenchResponsiveTier.Wide;
            return width >= NarrowBreakpoint
                ? ESWorkbenchResponsiveTier.Compact
                : ESWorkbenchResponsiveTier.Narrow;
        }

        /// <summary>
        /// 根据 Unity 主窗口的逻辑点工作区解析当前机器可达的最小窗口尺寸。
        /// 理想最小值不会反向撑破可用区；极小工作区仍保留一个可交互的安全下限。
        /// </summary>
        public Vector2 ResolveAdaptiveMinimum(Rect availableArea)
        {
            float availableWidth = Mathf.Max(320f, availableArea.width - FloatingWindowSafeInset * 2f);
            float availableHeight = Mathf.Max(260f, availableArea.height - FloatingWindowSafeInset * 2f);
            return new Vector2(
                Mathf.Min(MinimumWindowWidth, availableWidth),
                Mathf.Min(MinimumWindowHeight, availableHeight));
        }

        /// <summary>把浮动窗口夹取到 Unity 主窗口可用区；停靠窗口不应调用本方法。</summary>
        public Rect ClampFloatingWindow(Rect current, Rect availableArea)
        {
            Vector2 minimum = ResolveAdaptiveMinimum(availableArea);
            float safeWidth = Mathf.Max(320f, availableArea.width - FloatingWindowSafeInset * 2f);
            float safeHeight = Mathf.Max(260f, availableArea.height - FloatingWindowSafeInset * 2f);
            float width = Mathf.Min(Mathf.Max(current.width, minimum.x), safeWidth);
            float height = Mathf.Min(Mathf.Max(current.height, minimum.y), safeHeight);
            float minX = availableArea.xMin + FloatingWindowSafeInset;
            float minY = availableArea.yMin + FloatingWindowSafeInset;
            float maxX = Mathf.Max(minX, availableArea.xMax - FloatingWindowSafeInset - width);
            float maxY = Mathf.Max(minY, availableArea.yMax - FloatingWindowSafeInset - height);
            return new Rect(
                Mathf.Clamp(current.x, minX, maxX),
                Mathf.Clamp(current.y, minY, maxY),
                width,
                height);
        }

        public float ResolveProtectedCenterWidth(float width)
        {
            float safeWidth = Mathf.Max(1f, width);
            ESWorkbenchResponsiveTier tier = ResolveTier(safeWidth);
            float ratio = tier == ESWorkbenchResponsiveTier.Wide ? 0.5f
                : tier == ESWorkbenchResponsiveTier.Compact ? 0.68f : 0.72f;
            float target = Mathf.Max(MinimumCenterWidth, safeWidth * ratio);
            return Mathf.Min(target, Mathf.Max(280f, safeWidth - MinimumLeftPaneWidth));
        }

        public int ResolveVisibleCommandCount(float width)
        {
            float safeWidth = Mathf.Max(1f, width);
            if (safeWidth >= NarrowBreakpoint + 300f) return 8;
            if (safeWidth >= NarrowBreakpoint + 120f) return 6;
            return safeWidth >= NarrowBreakpoint ? 4 : 2;
        }

        public int ResolveVisibleDocumentCount(float centerWidth)
        {
            return Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(1f, centerWidth) - 96f) / 104f), 2, 6);
        }

        public int ResolveVisibleBottomPanelCount(float width)
        {
            return Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(1f, width) - 112f) / 96f), 2, 8);
        }

        public int ResolveVisibleViewportStatusCount(float centerWidth)
        {
            return Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(1f, centerWidth) - 280f) / 132f), 1, 5);
        }

        public ESWorkbenchVisualValidationResult EvaluateVisualEnvironment(
            ESWorkbenchVisualEnvironment environment)
        {
            ESWorkbenchResponsiveTier tier = ResolveTier(environment.WindowWidth);
            bool windowMinimumSatisfied = environment.WindowWidth >=
                    (tier == ESWorkbenchResponsiveTier.Narrow
                        ? MinimumUsableWindowWidth
                        : MinimumWindowWidth)
                && environment.WindowHeight >=
                    (tier == ESWorkbenchResponsiveTier.Narrow
                        ? MinimumUsableWindowHeight
                        : MinimumWindowHeight);
            bool centerProtected = environment.CenterWidth >=
                ResolveProtectedCenterWidth(environment.WindowWidth);
            bool highDpiSupported = environment.PixelsPerPoint >= 1f
                && environment.PixelsPerPoint <= 3f;
            string summary = (windowMinimumSatisfied && centerProtected && highDpiSupported
                    ? "布局保护合同通过"
                    : "布局保护合同未通过")
                + " · " + tier
                + " · " + environment.WindowWidth.ToString("0") + "×" + environment.WindowHeight.ToString("0")
                + " · " + environment.PixelsPerPoint.ToString("0.##") + "x"
                + " · " + (environment.Theme == ESWorkbenchVisualTheme.Dark ? "深色" : "浅色")
                + (environment.LongChineseContent ? " · 长中文" : string.Empty);
            return new ESWorkbenchVisualValidationResult(
                tier,
                windowMinimumSatisfied,
                centerProtected,
                highDpiSupported,
                summary);
        }

        public ESWorkbenchVisualScenarioMatch EvaluateScenario(
            ESWorkbenchVisualEnvironment environment,
            ESWorkbenchVisualValidationScenario scenario)
        {
            if (scenario == null)
                return new ESWorkbenchVisualScenarioMatch(false, "未选择视觉验收场景。\n恢复：先选择一条矩阵场景。\n影响：不会写入截图证据。");

            ESWorkbenchResponsiveTier actualTier = ResolveTier(environment.WindowWidth);
            bool tierMatches = actualTier == scenario.ExpectedTier;
            bool themeMatches = environment.Theme == scenario.Theme;
            bool scaleMatches = Mathf.Abs(environment.PixelsPerPoint - scenario.PixelsPerPoint) < 0.01f;
            bool chineseMatches = environment.LongChineseContent == scenario.LongChineseContent;
            float widthTolerance = Mathf.Max(8f, scenario.Width * 0.02f);
            float heightTolerance = Mathf.Max(8f, scenario.Height * 0.02f);
            bool widthMatches = Mathf.Abs(environment.WindowWidth - scenario.Width) <= widthTolerance;
            bool heightMatches = Mathf.Abs(environment.WindowHeight - scenario.Height) <= heightTolerance;
            bool passed = tierMatches && themeMatches && scaleMatches && chineseMatches
                && widthMatches && heightMatches;
            string summary = (passed ? "场景匹配" : "场景不匹配")
                + " · 期望 " + scenario.ScenarioId
                + " · 实际 " + actualTier.ToString().ToLowerInvariant()
                + " / " + (environment.Theme == ESWorkbenchVisualTheme.Dark ? "dark" : "light")
                + " / " + Mathf.RoundToInt(environment.PixelsPerPoint * 100f) + "%"
                + (environment.LongChineseContent ? " / long-cn" : " / normal")
                + (passed ? string.Empty : "\n恢复：调整窗口尺寸、主题、系统缩放或长中文压力标记后重试。\n影响：当前截图不会被记录为该矩阵场景的有效证据。");
            return new ESWorkbenchVisualScenarioMatch(passed, summary);
        }

        public IReadOnlyList<ESWorkbenchVisualValidationScenario> CreateCommercialVisualMatrix()
        {
            float wideWidth = Mathf.Max(WideBreakpoint + 240f, MinimumWindowWidth);
            float compactWidth = Mathf.Max(MinimumWindowWidth, (WideBreakpoint + NarrowBreakpoint) * 0.5f);
            float narrowWidth = Mathf.Max(
                MinimumUsableWindowWidth,
                Mathf.Min(MinimumWindowWidth, NarrowBreakpoint - 40f));
            float wideHeight = Mathf.Max(MinimumWindowHeight, 680f);
            float compactHeight = Mathf.Max(MinimumWindowHeight, 640f);
            var tiers = new[]
            {
                (id: "wide", width: wideWidth, height: wideHeight),
                (id: "compact", width: compactWidth, height: compactHeight),
                (id: "narrow", width: narrowWidth, height: MinimumUsableWindowHeight)
            };
            var themes = new[]
            {
                (id: "dark", value: ESWorkbenchVisualTheme.Dark),
                (id: "light", value: ESWorkbenchVisualTheme.Light)
            };
            float[] scales = { 1f, 1.25f, 1.5f, 2f };
            var scenarios = new List<ESWorkbenchVisualValidationScenario>(48);
            for (int tierIndex = 0; tierIndex < tiers.Length; tierIndex++)
            for (int themeIndex = 0; themeIndex < themes.Length; themeIndex++)
            for (int scaleIndex = 0; scaleIndex < scales.Length; scaleIndex++)
            for (int chineseIndex = 0; chineseIndex < 2; chineseIndex++)
            {
                bool longChinese = chineseIndex == 1;
                float scale = scales[scaleIndex];
                string scenarioId = tiers[tierIndex].id
                    + "-" + themes[themeIndex].id
                    + "-" + Mathf.RoundToInt(scale * 100f)
                    + (longChinese ? "-long-cn" : string.Empty);
                scenarios.Add(new ESWorkbenchVisualValidationScenario(
                    scenarioId,
                    tiers[tierIndex].width,
                    tiers[tierIndex].height,
                    scale,
                    themes[themeIndex].value,
                    longChinese,
                    ResolveTier(tiers[tierIndex].width)));
            }
            return scenarios;
        }
    }

    /// <summary>无作者资产时的商业启动面合同；命令仍由正式贡献注册表提供。</summary>
    public sealed class ESWorkbenchEmptyStateDescriptor
    {
        public ESWorkbenchEmptyStateDescriptor(
            string title,
            string description,
            string primaryCommandId = null,
            string secondaryCommandId = null,
            string footnote = null,
            bool blocksAuthoringViewport = true)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "尚未开始创作" : title.Trim();
            Description = description ?? string.Empty;
            PrimaryCommandId = primaryCommandId ?? string.Empty;
            SecondaryCommandId = secondaryCommandId ?? string.Empty;
            Footnote = footnote ?? string.Empty;
            BlocksAuthoringViewport = blocksAuthoringViewport;
        }

        public string Title { get; }
        public string Description { get; }
        public string PrimaryCommandId { get; }
        public string SecondaryCommandId { get; }
        public string Footnote { get; }
        public bool BlocksAuthoringViewport { get; }
    }

    /// <summary>工作台外壳的展示合同。稳定 ID 用于冲突判定，展示文案和布局政策可以由领域贡献覆盖。</summary>
    public sealed class ESWorkbenchHostPresentationDescriptor
    {
        public ESWorkbenchHostPresentationDescriptor(
            string presentationId,
            string brandTitle,
            string assetFieldLabel = "资产",
            string viewportDocumentTitle = "场景",
            string viewportDocumentTooltip = "二维 / 三维可视作者区域",
            string inspectorTitle = "检查器",
            ESWorkbenchResponsiveLayoutPolicy layoutPolicy = null,
            string leftPanelTitle = "内容与结构",
            string workspaceTitle = "作者场景",
            ESWorkbenchEmptyStateDescriptor emptyState = null)
        {
            if (string.IsNullOrWhiteSpace(presentationId))
                throw new ArgumentException("展示合同 ID 不能为空。", nameof(presentationId));
            PresentationId = presentationId.Trim();
            BrandTitle = string.IsNullOrWhiteSpace(brandTitle) ? "ES 内容工作台" : brandTitle.Trim();
            AssetFieldLabel = string.IsNullOrWhiteSpace(assetFieldLabel) ? "资产" : assetFieldLabel.Trim();
            ViewportDocumentTitle = string.IsNullOrWhiteSpace(viewportDocumentTitle) ? "场景" : viewportDocumentTitle.Trim();
            ViewportDocumentTooltip = viewportDocumentTooltip ?? string.Empty;
            InspectorTitle = string.IsNullOrWhiteSpace(inspectorTitle) ? "检查器" : inspectorTitle.Trim();
            LayoutPolicy = layoutPolicy ?? new ESWorkbenchResponsiveLayoutPolicy();
            LeftPanelTitle = string.IsNullOrWhiteSpace(leftPanelTitle) ? "内容与结构" : leftPanelTitle.Trim();
            WorkspaceTitle = string.IsNullOrWhiteSpace(workspaceTitle) ? "作者场景" : workspaceTitle.Trim();
            EmptyState = emptyState;
        }

        public string PresentationId { get; }
        public string BrandTitle { get; }
        public string AssetFieldLabel { get; }
        public string ViewportDocumentTitle { get; }
        public string ViewportDocumentTooltip { get; }
        public string InspectorTitle { get; }
        public ESWorkbenchResponsiveLayoutPolicy LayoutPolicy { get; }
        public string LeftPanelTitle { get; }
        public string WorkspaceTitle { get; }
        public ESWorkbenchEmptyStateDescriptor EmptyState { get; }

        public static ESWorkbenchHostPresentationDescriptor CreateDefault(string brandTitle = null)
        {
            return new ESWorkbenchHostPresentationDescriptor(
                "core.default",
                string.IsNullOrWhiteSpace(brandTitle) ? "ES 内容工作台" : brandTitle);
        }
    }

    /// <summary>底部面板工厂收到的只读窗口上下文，不持有可序列化业务状态。</summary>
    public sealed class ESWorkbenchBottomPanelContext
    {
        internal ESWorkbenchBottomPanelContext(
            string workbenchId,
            ESWorkbenchActionContext actions,
            IReadOnlyList<ESWorkbenchIssueDescriptor> issues)
        {
            WorkbenchId = workbenchId ?? string.Empty;
            Actions = actions;
            Issues = issues ?? Array.Empty<ESWorkbenchIssueDescriptor>();
        }

        public string WorkbenchId { get; }
        public ESWorkbenchActionContext Actions { get; }
        public IReadOnlyList<ESWorkbenchIssueDescriptor> Issues { get; }
    }

    /// <summary>一次底部面板实例。宿主在切换、重载或关闭时确定性释放。</summary>
    public sealed class ESWorkbenchBottomPanelContent : IDisposable
    {
        private Action release;

        public ESWorkbenchBottomPanelContent(VisualElement root, Action release = null)
            : this(root, ESWorkbenchBottomPanelDensity.Normal, 0f, release)
        {
        }

        public ESWorkbenchBottomPanelContent(
            VisualElement root,
            ESWorkbenchBottomPanelDensity density,
            float preferredHeight = 0f,
            Action release = null)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Density = density;
            PreferredHeight = Mathf.Max(0f, preferredHeight);
            this.release = release;
        }

        public VisualElement Root { get; }
        public ESWorkbenchBottomPanelDensity Density { get; }
        public float PreferredHeight { get; }

        public void Dispose()
        {
            Action callback = release;
            release = null;
            callback?.Invoke();
        }
    }

    /// <summary>可排序、可覆盖的底部通道合同。内部稳定 ID 与中文展示文案相互独立。</summary>
    public sealed class ESWorkbenchBottomPanelDescriptor
    {
        public ESWorkbenchBottomPanelDescriptor(
            string panelId,
            string title,
            Func<ESWorkbenchBottomPanelContext, ESWorkbenchBottomPanelContent> createContent,
            string tooltip = null,
            int priority = 0,
            Func<ESWorkbenchBottomPanelContext, bool> isAvailable = null)
        {
            if (string.IsNullOrWhiteSpace(panelId))
                throw new ArgumentException("底部面板 ID 不能为空。", nameof(panelId));
            PanelId = panelId.Trim();
            Title = string.IsNullOrWhiteSpace(title) ? PanelId : title.Trim();
            CreateContent = createContent ?? throw new ArgumentNullException(nameof(createContent));
            Tooltip = tooltip ?? string.Empty;
            Priority = priority;
            IsAvailable = isAvailable;
        }

        public string PanelId { get; }
        public string Title { get; }
        public string Tooltip { get; }
        public int Priority { get; }
        public Func<ESWorkbenchBottomPanelContext, ESWorkbenchBottomPanelContent> CreateContent { get; }
        public Func<ESWorkbenchBottomPanelContext, bool> IsAvailable { get; }
    }

    [Serializable]
    public sealed class ESWorkbenchViewportLayoutState
    {
        public string viewportId = string.Empty;
        public Vector2 pan;
        public float zoom = 1f;
        public bool snapEnabled;
        public float moveSnap = 1f;
        public float rotationSnap = 15f;
        public float scaleSnap = 0.1f;
        // 业务视口可持久化自己的稳定模式，不保存 Unity 对象或临时相机引用。
        public string previewCameraMode = string.Empty;
        // 3D 视口只保存可重建的轨道相机状态，不保存 Camera、PreviewScene 或 Unity 对象引用。
        public Vector3 cameraFocus;
        public float cameraDistance = 8f;
        public float cameraYaw = 35f;
        public float cameraPitch = 25f;
        public bool cameraInitialized;
    }

    public enum ESWorkbenchViewportKind : byte
    {
        Canvas2D,
        Scene3D,
        Game,
        Custom
    }

    public sealed class ESWorkbenchViewportStatusDescriptor
    {
        public ESWorkbenchViewportStatusDescriptor(
            string statusId,
            string label,
            string value,
            string tooltip = null,
            int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(statusId))
                throw new ArgumentException("视口状态 ID 不能为空。", nameof(statusId));
            StatusId = statusId.Trim();
            Label = label?.Trim() ?? string.Empty;
            Value = value?.Trim() ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            Priority = priority;
        }

        public string StatusId { get; }
        public string Label { get; }
        public string Value { get; }
        public string Tooltip { get; }
        public int Priority { get; }
    }

    public enum ESWorkbenchRefreshReason : byte
    {
        Initial,
        AssetChanged,
        SelectionChanged,
        DataChanged,
        UndoRedo,
        Explicit
    }

    public enum ESWorkbenchIssueSeverity : byte
    {
        Information,
        Warning,
        Error,
        Blocker
    }

    public enum ESWorkbenchIssueChannel : byte
    {
        Validation,
        Build,
        Performance,
        Security,
        System
    }

    /// <summary>
    /// 面向作者的问题与生产状态投影。问题源只描述事实和就近动作，不持有第二份业务数据。
    /// </summary>
    public sealed class ESWorkbenchIssueDescriptor
    {
        public ESWorkbenchIssueDescriptor(
            string issueId,
            string title,
            ESWorkbenchIssueSeverity severity,
            ESWorkbenchIssueChannel channel = ESWorkbenchIssueChannel.Validation,
            string description = null,
            string targetStableId = null,
            string actionLabel = null,
            Action<ESWorkbenchActionContext> action = null,
            int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(issueId)) throw new ArgumentException("问题 ID 不能为空。", nameof(issueId));
            IssueId = issueId.Trim();
            Title = string.IsNullOrWhiteSpace(title) ? IssueId : title.Trim();
            Severity = severity;
            Channel = channel;
            Description = description ?? string.Empty;
            TargetStableId = targetStableId ?? string.Empty;
            ActionLabel = actionLabel ?? string.Empty;
            Action = action;
            Priority = priority;
        }

        public string IssueId { get; }
        public string Title { get; }
        public string Description { get; }
        public string TargetStableId { get; }
        public string ActionLabel { get; }
        public ESWorkbenchIssueSeverity Severity { get; }
        public ESWorkbenchIssueChannel Channel { get; }
        public Action<ESWorkbenchActionContext> Action { get; }
        public int Priority { get; }
    }

    public readonly struct ESWorkbenchShortcut
    {
        public readonly KeyCode key;
        public readonly EventModifiers modifiers;

        public ESWorkbenchShortcut(KeyCode key, EventModifiers modifiers = EventModifiers.None)
        {
            this.key = key;
            this.modifiers = modifiers;
        }

        internal bool Matches(KeyDownEvent evt)
        {
            if (evt == null || evt.keyCode != key) return false;
            EventModifiers actual = evt.modifiers &
                (EventModifiers.Control | EventModifiers.Command | EventModifiers.Shift | EventModifiers.Alt);
            EventModifiers expected = modifiers &
                (EventModifiers.Control | EventModifiers.Command | EventModifiers.Shift | EventModifiers.Alt);
            return actual == expected;
        }
    }

    public sealed class ESWorkbenchSelection
    {
        public static readonly ESWorkbenchSelection Empty = new ESWorkbenchSelection(string.Empty, string.Empty, null, null);

        public ESWorkbenchSelection(string stableId, string kind, UnityEngine.Object unityObject, object payload)
        {
            StableId = stableId ?? string.Empty;
            Kind = kind ?? string.Empty;
            UnityObject = unityObject;
            Payload = payload;
        }

        public string StableId { get; }
        public string Kind { get; }
        public UnityEngine.Object UnityObject { get; }
        public object Payload { get; }
        public bool IsEmpty => string.IsNullOrEmpty(StableId);
    }

    public sealed class ESWorkbenchSelectionService
    {
        private ESWorkbenchSelection current = ESWorkbenchSelection.Empty;

        public ESWorkbenchSelection Current => current;
        public event Action<ESWorkbenchSelection> Changed;

        public void Select(ESWorkbenchSelection selection)
        {
            ESWorkbenchSelection next = selection ?? ESWorkbenchSelection.Empty;
            if (ReferenceEquals(current, next)
                || (current.StableId == next.StableId && current.Kind == next.Kind
                    && current.UnityObject == next.UnityObject && Equals(current.Payload, next.Payload)))
                return;
            current = next;
            Changed?.Invoke(current);
        }

        public void Clear()
        {
            Select(ESWorkbenchSelection.Empty);
        }
    }

    public sealed class ESWorkbenchToolStateService
    {
        private readonly Dictionary<string, ESWorkbenchToolCapabilities> capabilities =
            new Dictionary<string, ESWorkbenchToolCapabilities>(StringComparer.Ordinal);
        private string activeToolId = string.Empty;

        public string ActiveToolId => activeToolId;
        public ESWorkbenchToolCapabilities ActiveCapabilities =>
            capabilities.TryGetValue(activeToolId, out ESWorkbenchToolCapabilities declared)
                ? ESWorkbenchToolCapabilityResolver.Resolve(activeToolId, declared)
                : ESWorkbenchToolCapabilityResolver.Resolve(activeToolId);
        public event Action<string> Changed;

        internal void RegisterCapabilities(string toolId, ESWorkbenchToolCapabilities value)
        {
            if (!string.IsNullOrWhiteSpace(toolId)) capabilities[toolId.Trim()] = value;
        }

        internal void ClearRegisteredCapabilities()
        {
            capabilities.Clear();
        }

        public bool IsActive(string toolId)
        {
            return !string.IsNullOrEmpty(toolId)
                && string.Equals(activeToolId, toolId, StringComparison.Ordinal);
        }

        public void Activate(string toolId)
        {
            string next = toolId?.Trim() ?? string.Empty;
            if (string.Equals(activeToolId, next, StringComparison.Ordinal)) return;
            activeToolId = next;
            Changed?.Invoke(activeToolId);
        }

        public void Clear()
        {
            Activate(string.Empty);
        }
    }

    /// <summary>
    /// 动态集合源在工作台刷新时按需解析，不要求领域为了列表变化重新注入全部贡献。
    /// </summary>
    public sealed class ESWorkbenchCollectionSource<T> where T : class
    {
        public ESWorkbenchCollectionSource(
            string sourceId,
            Func<ESWorkbenchActionContext, IEnumerable<T>> query,
            int priority = 0,
            Func<ESWorkbenchActionContext, bool> isAvailable = null)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("集合源 ID 不能为空。", nameof(sourceId));
            SourceId = sourceId.Trim();
            Query = query ?? throw new ArgumentNullException(nameof(query));
            Priority = priority;
            IsAvailable = isAvailable;
        }

        public string SourceId { get; }
        public int Priority { get; }
        public Func<ESWorkbenchActionContext, IEnumerable<T>> Query { get; }
        public Func<ESWorkbenchActionContext, bool> IsAvailable { get; }
    }

    public sealed class ESWorkbenchPopupRequest
    {
        public ESWorkbenchPopupRequest(
            string title,
            Vector2 size,
            Func<ESWorkbenchActionContext, VisualElement> createContent)
        {
            if (createContent == null) throw new ArgumentNullException(nameof(createContent));
            Title = string.IsNullOrWhiteSpace(title) ? "ES 工作台" : title.Trim();
            Size = new Vector2(Mathf.Max(220f, size.x), Mathf.Max(120f, size.y));
            CreateContent = createContent;
        }

        public string Title { get; }
        public Vector2 Size { get; }
        public Func<ESWorkbenchActionContext, VisualElement> CreateContent { get; }
    }

    public sealed class ESWorkbenchActionContext
    {
        private readonly Action<string, MessageType> setStatus;
        private readonly Action<ESWorkbenchPopupRequest, Rect> showPopup;
        private readonly Action<ESWorkbenchRefreshReason> refresh;
        private readonly Action<string, ESWorkbenchDirtyFlags> markDirty;

        internal ESWorkbenchActionContext(
            EditorWindow window,
            ESWorkbenchSelectionService selection,
            ESWorkbenchToolStateService tools,
            ESWorkbenchAuthoringService authoring,
            Action<string, MessageType> setStatus,
            Action<ESWorkbenchPopupRequest, Rect> showPopup,
            Action<ESWorkbenchRefreshReason> refresh,
            Action<string, ESWorkbenchDirtyFlags> markDirty)
        {
            Window = window;
            Selection = selection;
            Tools = tools ?? throw new ArgumentNullException(nameof(tools));
            Authoring = authoring ?? throw new ArgumentNullException(nameof(authoring));
            this.setStatus = setStatus;
            this.showPopup = showPopup;
            this.refresh = refresh;
            this.markDirty = markDirty;
        }

        public EditorWindow Window { get; }
        public ESWorkbenchSelectionService Selection { get; }
        public ESWorkbenchToolStateService Tools { get; }
        public ESWorkbenchAuthoringService Authoring { get; }
        public void SetStatus(string message, MessageType type = MessageType.Info) => setStatus?.Invoke(message, type);
        public void ShowPopup(ESWorkbenchPopupRequest request, Rect screenAnchor) => showPopup?.Invoke(request, screenAnchor);
        public void Refresh(ESWorkbenchRefreshReason reason = ESWorkbenchRefreshReason.Explicit) => refresh?.Invoke(reason);
        public void MarkDirty(string dirtyKey, ESWorkbenchDirtyFlags flags = ESWorkbenchDirtyFlags.Authoring) =>
            markDirty?.Invoke(dirtyKey, flags);
    }

    public enum ESWorkbenchMutationKind : byte
    {
        Create,
        Move,
        Rotate,
        Scale,
        Duplicate,
        Delete
    }

    public sealed class ESWorkbenchMutationContext
    {
        internal ESWorkbenchMutationContext(
            ESWorkbenchActionContext actions,
            ESWorkbenchAuthoringAdapterDescriptor adapter,
            ESWorkbenchMutationKind kind,
            ESWorkbenchSelection selection,
            ESWorkbenchObjectDescriptor item,
            Vector3 worldPosition)
        {
            Actions = actions;
            Adapter = adapter;
            Kind = kind;
            Selection = selection ?? ESWorkbenchSelection.Empty;
            Item = item;
            WorldPosition = worldPosition;
        }

        public ESWorkbenchActionContext Actions { get; }
        public ESWorkbenchAuthoringAdapterDescriptor Adapter { get; }
        public ESWorkbenchMutationKind Kind { get; }
        public ESWorkbenchSelection Selection { get; }
        public ESWorkbenchObjectDescriptor Item { get; }
        public Vector3 WorldPosition { get; }
        public Vector3 RotationEuler => WorldPosition;
        public Vector3 Scale => WorldPosition;
    }

    public readonly struct ESWorkbenchCreateRequest
    {
        public ESWorkbenchCreateRequest(ESWorkbenchObjectDescriptor item, Vector3 worldPosition)
        {
            Item = item;
            WorldPosition = worldPosition;
        }

        public ESWorkbenchObjectDescriptor Item { get; }
        public Vector3 WorldPosition { get; }
    }

    public sealed class ESWorkbenchMutationResult
    {
        private ESWorkbenchMutationResult(
            bool succeeded,
            string message,
            ESWorkbenchSelection selection,
            string dirtyKey,
            ESWorkbenchDirtyFlags dirtyFlags,
            ESWorkbenchRefreshReason refreshReason)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Selection = selection;
            DirtyKey = dirtyKey ?? string.Empty;
            DirtyFlags = dirtyFlags;
            RefreshReason = refreshReason;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public ESWorkbenchSelection Selection { get; }
        public string DirtyKey { get; }
        public ESWorkbenchDirtyFlags DirtyFlags { get; }
        public ESWorkbenchRefreshReason RefreshReason { get; }

        public static ESWorkbenchMutationResult Success(
            string message,
            ESWorkbenchSelection selection = null,
            string dirtyKey = null,
            ESWorkbenchDirtyFlags dirtyFlags = ESWorkbenchDirtyFlags.Authoring,
            ESWorkbenchRefreshReason refreshReason = ESWorkbenchRefreshReason.DataChanged)
        {
            return new ESWorkbenchMutationResult(true, message, selection, dirtyKey, dirtyFlags, refreshReason);
        }

        public static ESWorkbenchMutationResult Failure(string message)
        {
            return new ESWorkbenchMutationResult(false, message, null, null,
                ESWorkbenchDirtyFlags.None, ESWorkbenchRefreshReason.Explicit);
        }
    }

    public sealed class ESWorkbenchAuthoringAdapterDescriptor
    {
        public ESWorkbenchAuthoringAdapterDescriptor(
            string adapterId,
            Func<ESWorkbenchSelection, bool> matchesSelection,
            Func<ESWorkbenchObjectDescriptor, bool> canCreate = null,
            Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> create = null,
            Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> move = null,
            Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> duplicate = null,
            Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> delete = null,
            Func<ESWorkbenchMutationContext, IEnumerable<UnityEngine.Object>> resolveUndoTargets = null,
            Action<ESWorkbenchMutationContext, ESWorkbenchMutationResult> committed = null,
            int priority = 0,
            Func<ESWorkbenchActionContext, bool> isAvailable = null,
            Func<ESWorkbenchSelection, bool> canRotate = null,
            Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> rotate = null,
            Func<ESWorkbenchSelection, bool> canScale = null,
            Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> scale = null)
        {
            if (string.IsNullOrWhiteSpace(adapterId)) throw new ArgumentException("作者适配器 ID 不能为空。", nameof(adapterId));
            if (matchesSelection == null) throw new ArgumentNullException(nameof(matchesSelection));
            if (create == null && move == null && rotate == null && scale == null && duplicate == null && delete == null)
                throw new ArgumentException("作者适配器必须声明至少一种变更操作。", nameof(create));
            AdapterId = adapterId.Trim();
            MatchesSelection = matchesSelection;
            CanCreate = canCreate;
            Create = create;
            Move = move;
            Duplicate = duplicate;
            Delete = delete;
            CanRotate = canRotate;
            Rotate = rotate;
            CanScale = canScale;
            Scale = scale;
            ResolveUndoTargets = resolveUndoTargets;
            Committed = committed;
            Priority = priority;
            IsAvailable = isAvailable;
        }

        public string AdapterId { get; }
        public int Priority { get; }
        public Func<ESWorkbenchSelection, bool> MatchesSelection { get; }
        public Func<ESWorkbenchObjectDescriptor, bool> CanCreate { get; }
        public Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> Create { get; }
        public Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> Move { get; }
        public Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> Duplicate { get; }
        public Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> Delete { get; }
        public Func<ESWorkbenchSelection, bool> CanRotate { get; }
        public Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> Rotate { get; }
        public Func<ESWorkbenchSelection, bool> CanScale { get; }
        public Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> Scale { get; }
        public Func<ESWorkbenchMutationContext, IEnumerable<UnityEngine.Object>> ResolveUndoTargets { get; }
        public Action<ESWorkbenchMutationContext, ESWorkbenchMutationResult> Committed { get; }
        public Func<ESWorkbenchActionContext, bool> IsAvailable { get; }
    }

    public sealed class ESWorkbenchAuthoringService
    {
        private ESWorkbenchActionContext actions;
        private Func<IReadOnlyList<ESWorkbenchAuthoringAdapterDescriptor>> getAdapters;
        private Func<ESWorkbenchMutationKind, ESWorkbenchSelection, ESWorkbenchObjectDescriptor, string> validateMutation;

        public bool LastOperationCommittedWithPostCommitFailure { get; private set; }

        internal void Bind(
            ESWorkbenchActionContext actionContext,
            Func<IReadOnlyList<ESWorkbenchAuthoringAdapterDescriptor>> adapterSource,
            Func<ESWorkbenchMutationKind, ESWorkbenchSelection, ESWorkbenchObjectDescriptor, string> mutationValidator = null)
        {
            actions = actionContext;
            getAdapters = adapterSource;
            validateMutation = mutationValidator;
        }

        internal void Unbind()
        {
            actions = null;
            getAdapters = null;
            validateMutation = null;
            LastOperationCommittedWithPostCommitFailure = false;
        }

        public bool CanCreate(ESWorkbenchObjectDescriptor item) =>
            IsMutationAllowed(ESWorkbenchMutationKind.Create, ESWorkbenchSelection.Empty, item, out _)
            && ResolveForCreate(item)?.Create != null;
        public bool CanMove(ESWorkbenchSelection selection) =>
            IsMutationAllowed(ESWorkbenchMutationKind.Move, selection, null, out _)
            && ResolveForSelection(selection, ESWorkbenchMutationKind.Move) != null;
        public bool CanRotate(ESWorkbenchSelection selection) =>
            IsMutationAllowed(ESWorkbenchMutationKind.Rotate, selection, null, out _)
            && ResolveForSelection(selection, ESWorkbenchMutationKind.Rotate) != null;
        public bool CanScale(ESWorkbenchSelection selection) =>
            IsMutationAllowed(ESWorkbenchMutationKind.Scale, selection, null, out _)
            && ResolveForSelection(selection, ESWorkbenchMutationKind.Scale) != null;
        public bool CanDuplicate(ESWorkbenchSelection selection) =>
            IsMutationAllowed(ESWorkbenchMutationKind.Duplicate, selection, null, out _)
            && ResolveForSelection(selection, ESWorkbenchMutationKind.Duplicate) != null;
        public bool CanDelete(ESWorkbenchSelection selection) =>
            IsMutationAllowed(ESWorkbenchMutationKind.Delete, selection, null, out _)
            && ResolveForSelection(selection, ESWorkbenchMutationKind.Delete) != null;

        public bool TryCreate(ESWorkbenchObjectDescriptor item, Vector3 worldPosition, out string message) =>
            Execute(ESWorkbenchMutationKind.Create, ESWorkbenchSelection.Empty, item, worldPosition, out message);

        public bool CanCreateBatch(IReadOnlyList<ESWorkbenchCreateRequest> requests, out string message)
        {
            message = string.Empty;
            if (actions == null) { message = "作者服务尚未绑定工作台。"; return false; }
            if (requests == null || requests.Count == 0) { message = "没有可批量放置的内容。"; return false; }
            for (int i = 0; i < requests.Count; i++)
            {
                ESWorkbenchObjectDescriptor item = requests[i].Item;
                if (item == null) { message = "批量放置第 " + (i + 1) + " 项为空。"; return false; }
                if (!IsMutationAllowed(ESWorkbenchMutationKind.Create, ESWorkbenchSelection.Empty, item, out message))
                    return false;
                ESWorkbenchAuthoringAdapterDescriptor adapter = ResolveForCreate(item);
                if (adapter?.Create == null)
                {
                    message = "内容“" + item.DisplayName + "”没有注册批量放置所需的创建能力。";
                    return false;
                }
                var context = new ESWorkbenchMutationContext(
                    actions, adapter, ESWorkbenchMutationKind.Create, ESWorkbenchSelection.Empty,
                    item, requests[i].WorldPosition);
                UnityEngine.Object[] targets = adapter.ResolveUndoTargets?.Invoke(context)?
                    .Where(value => value != null)
                    .Distinct()
                    .ToArray() ?? Array.Empty<UnityEngine.Object>();
                if (targets.Length == 0)
                {
                    message = "内容“" + item.DisplayName + "”的作者适配器没有声明 Undo 目标。";
                    return false;
                }
            }
            return true;
        }

        public bool TryCreateBatch(IReadOnlyList<ESWorkbenchCreateRequest> requests, out string message)
        {
            message = string.Empty;
            LastOperationCommittedWithPostCommitFailure = false;
            if (!CanCreateBatch(requests, out message))
            {
                actions?.SetStatus(message, MessageType.Warning);
                return false;
            }

            var contexts = new List<ESWorkbenchMutationContext>(requests.Count);
            var targets = new List<UnityEngine.Object>();
            for (int i = 0; i < requests.Count; i++)
            {
                ESWorkbenchCreateRequest request = requests[i];
                ESWorkbenchAuthoringAdapterDescriptor adapter = ResolveForCreate(request.Item);
                var context = new ESWorkbenchMutationContext(
                    actions, adapter, ESWorkbenchMutationKind.Create, ESWorkbenchSelection.Empty,
                    request.Item, request.WorldPosition);
                contexts.Add(context);
                targets.AddRange(adapter.ResolveUndoTargets(context).Where(value => value != null));
            }

            const string undoName = "批量放置工作台内容";
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            var results = new List<ESWorkbenchMutationResult>(contexts.Count);
            try
            {
                UnityEngine.Object[] distinctTargets = targets.Distinct().ToArray();
                Undo.RegisterCompleteObjectUndo(distinctTargets, undoName);
                for (int i = 0; i < contexts.Count; i++)
                {
                    ESWorkbenchMutationContext context = contexts[i];
                    ESWorkbenchMutationResult result = context.Adapter.Create(context)
                        ?? ESWorkbenchMutationResult.Failure("作者操作没有返回结果。");
                    if (!result.Succeeded)
                    {
                        Undo.RevertAllDownToGroup(undoGroup);
                        message = "批量放置在第 " + (i + 1) + " 项失败：" + result.Message;
                        actions.SetStatus(message + "（全部作者数据已回滚）", MessageType.Error);
                        return false;
                    }
                    results.Add(result);
                }
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogException(exception);
                message = undoName + "失败：" + exception.Message;
                actions.SetStatus(message + "（全部作者数据已回滚）", MessageType.Error);
                return false;
            }

            try
            {
                ESWorkbenchSelection finalSelection = null;
                ESWorkbenchRefreshReason refreshReason = ESWorkbenchRefreshReason.DataChanged;
                var dirty = new Dictionary<string, ESWorkbenchDirtyFlags>(StringComparer.Ordinal);
                for (int i = 0; i < contexts.Count; i++)
                {
                    ESWorkbenchMutationContext context = contexts[i];
                    ESWorkbenchMutationResult result = results[i];
                    context.Adapter.Committed?.Invoke(context, result);
                    if (result.Selection != null) finalSelection = result.Selection;
                    if (!string.IsNullOrWhiteSpace(result.DirtyKey))
                    {
                        dirty.TryGetValue(result.DirtyKey, out ESWorkbenchDirtyFlags flags);
                        dirty[result.DirtyKey] = flags | result.DirtyFlags;
                    }
                    if ((int)result.RefreshReason > (int)refreshReason) refreshReason = result.RefreshReason;
                }
                foreach (KeyValuePair<string, ESWorkbenchDirtyFlags> pair in dirty)
                    actions.MarkDirty(pair.Key, pair.Value);
                if (finalSelection != null) actions.Selection.Select(finalSelection);
                actions.Refresh(refreshReason);
                message = "已批量放置 " + requests.Count + " 项内容；Undo 将作为一个整体回退。";
                actions.SetStatus(message, MessageType.Info);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                LastOperationCommittedWithPostCommitFailure = true;
                message = undoName + "已提交，但提交后同步失败：" + exception.Message;
                actions.SetStatus(message + "（请刷新工作台并检查持久化状态）", MessageType.Error);
                return true;
            }
        }

        public bool TryMove(ESWorkbenchSelection selection, Vector3 worldPosition, out string message) =>
            Execute(ESWorkbenchMutationKind.Move, selection, null, worldPosition, out message);

        public bool TryRotate(ESWorkbenchSelection selection, Vector3 rotationEuler, out string message) =>
            Execute(ESWorkbenchMutationKind.Rotate, selection, null, rotationEuler, out message);

        public bool TryScale(ESWorkbenchSelection selection, Vector3 scale, out string message) =>
            Execute(ESWorkbenchMutationKind.Scale, selection, null, scale, out message);

        public bool TryDuplicate(ESWorkbenchSelection selection, out string message) =>
            Execute(ESWorkbenchMutationKind.Duplicate, selection, null, default, out message);

        public bool TryDelete(ESWorkbenchSelection selection, out string message) =>
            Execute(ESWorkbenchMutationKind.Delete, selection, null, default, out message);

        private bool Execute(
            ESWorkbenchMutationKind kind,
            ESWorkbenchSelection selection,
            ESWorkbenchObjectDescriptor item,
            Vector3 worldPosition,
            out string message)
        {
            message = string.Empty;
            LastOperationCommittedWithPostCommitFailure = false;
            if (actions == null) { message = "作者服务尚未绑定工作台。"; return false; }
            if (!IsMutationAllowed(kind, selection, item, out message))
            {
                actions.SetStatus(message, MessageType.Warning);
                return false;
            }
            ESWorkbenchAuthoringAdapterDescriptor adapter = kind == ESWorkbenchMutationKind.Create
                ? ResolveForCreate(item)
                : ResolveForSelection(selection, kind);
            Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> handler = ResolveHandler(adapter, kind);
            if (adapter == null || handler == null)
            {
                message = "当前对象没有注册" + ResolveOperationName(kind) + "能力。";
                return false;
            }

            var context = new ESWorkbenchMutationContext(actions, adapter, kind, selection, item, worldPosition);
            string undoName = ResolveOperationName(kind) + " · " + adapter.AdapterId;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            ESWorkbenchMutationResult result;
            try
            {
                UnityEngine.Object[] targets = adapter.ResolveUndoTargets?.Invoke(context)?
                    .Where(value => value != null)
                    .Distinct()
                    .ToArray() ?? Array.Empty<UnityEngine.Object>();
                if (targets.Length == 0)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    message = undoName + "被阻止：适配器没有声明 Undo 目标。";
                    actions.SetStatus(message + "（变更回调未执行）", MessageType.Error);
                    return false;
                }
                Undo.RegisterCompleteObjectUndo(targets, undoName);
                result = handler(context)
                    ?? ESWorkbenchMutationResult.Failure("作者操作没有返回结果。");
                if (!result.Succeeded)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    message = string.IsNullOrWhiteSpace(result.Message) ? undoName + "失败。" : result.Message;
                    actions.SetStatus(message + "（操作未提交，作者数据已回滚）", MessageType.Error);
                    return false;
                }

                Undo.CollapseUndoOperations(undoGroup);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogException(exception);
                message = undoName + "失败：" + exception.Message;
                actions.SetStatus(message + "（作者数据已回滚）", MessageType.Error);
                return false;
            }

            try
            {
                adapter.Committed?.Invoke(context, result);
                if (result.Selection != null) actions.Selection.Select(result.Selection);
                if (!string.IsNullOrWhiteSpace(result.DirtyKey)) actions.MarkDirty(result.DirtyKey, result.DirtyFlags);
                actions.Refresh(result.RefreshReason);
                message = string.IsNullOrWhiteSpace(result.Message) ? undoName + "完成。" : result.Message;
                actions.SetStatus(message, MessageType.Info);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                LastOperationCommittedWithPostCommitFailure = true;
                message = undoName + "已提交，但提交后同步失败：" + exception.Message;
                actions.SetStatus(message + "（请刷新工作台并检查持久化状态）", MessageType.Error);
                return true;
            }
        }

        private bool IsMutationAllowed(
            ESWorkbenchMutationKind kind,
            ESWorkbenchSelection selection,
            ESWorkbenchObjectDescriptor item,
            out string message)
        {
            message = string.Empty;
            if (validateMutation == null) return true;
            try
            {
                message = validateMutation(kind, selection ?? ESWorkbenchSelection.Empty, item) ?? string.Empty;
                return string.IsNullOrWhiteSpace(message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                message = "作者操作策略检查失败：" + exception.Message;
                return false;
            }
        }

        private ESWorkbenchAuthoringAdapterDescriptor ResolveForCreate(ESWorkbenchObjectDescriptor item)
        {
            foreach (ESWorkbenchAuthoringAdapterDescriptor adapter in OrderedAdapters())
            {
                if (adapter.Create == null || adapter.CanCreate == null) continue;
                if (EvaluateAdapterPredicate(adapter, "创建能力查询", () => adapter.CanCreate(item))) return adapter;
            }
            return null;
        }

        private ESWorkbenchAuthoringAdapterDescriptor ResolveForSelection(
            ESWorkbenchSelection selection,
            ESWorkbenchMutationKind kind)
        {
            if (selection == null || selection.IsEmpty) return null;
            foreach (ESWorkbenchAuthoringAdapterDescriptor adapter in OrderedAdapters())
            {
                if (ResolveHandler(adapter, kind) == null) continue;
                if (!EvaluateAdapterPredicate(adapter, "选择匹配", () => adapter.MatchesSelection(selection))) continue;
                if (kind == ESWorkbenchMutationKind.Rotate && adapter.CanRotate != null
                    && !EvaluateAdapterPredicate(adapter, "旋转能力查询", () => adapter.CanRotate(selection))) continue;
                if (kind == ESWorkbenchMutationKind.Scale && adapter.CanScale != null
                    && !EvaluateAdapterPredicate(adapter, "缩放能力查询", () => adapter.CanScale(selection))) continue;
                return adapter;
            }
            return null;
        }

        private IReadOnlyList<ESWorkbenchAuthoringAdapterDescriptor> OrderedAdapters()
        {
            IReadOnlyList<ESWorkbenchAuthoringAdapterDescriptor> source;
            try
            {
                source = getAdapters?.Invoke() ?? Array.Empty<ESWorkbenchAuthoringAdapterDescriptor>();
            }
            catch (Exception exception)
            {
                ReportAdapterException("作者适配器源", "枚举", exception);
                return Array.Empty<ESWorkbenchAuthoringAdapterDescriptor>();
            }

            return source
                .Where(value => value != null && (value.IsAvailable == null
                    || EvaluateAdapterPredicate(value, "可用性查询", () => value.IsAvailable(actions))))
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.AdapterId, StringComparer.Ordinal)
                .ToArray();
        }

        private bool EvaluateAdapterPredicate(
            ESWorkbenchAuthoringAdapterDescriptor adapter,
            string operation,
            Func<bool> predicate)
        {
            try
            {
                return predicate != null && predicate();
            }
            catch (Exception exception)
            {
                ReportAdapterException(adapter?.AdapterId, operation, exception);
                return false;
            }
        }

        private void ReportAdapterException(string adapterId, string operation, Exception exception)
        {
            Debug.LogException(exception);
            actions?.SetStatus(
                "作者适配器异常：" + (string.IsNullOrWhiteSpace(adapterId) ? "未命名" : adapterId)
                + " · " + operation + "失败，已隔离该能力。",
                MessageType.Error);
        }

        private static Func<ESWorkbenchMutationContext, ESWorkbenchMutationResult> ResolveHandler(
            ESWorkbenchAuthoringAdapterDescriptor adapter,
            ESWorkbenchMutationKind kind)
        {
            if (adapter == null) return null;
            switch (kind)
            {
                case ESWorkbenchMutationKind.Create: return adapter.Create;
                case ESWorkbenchMutationKind.Move: return adapter.Move;
                case ESWorkbenchMutationKind.Rotate: return adapter.Rotate;
                case ESWorkbenchMutationKind.Scale: return adapter.Scale;
                case ESWorkbenchMutationKind.Duplicate: return adapter.Duplicate;
                case ESWorkbenchMutationKind.Delete: return adapter.Delete;
                default: return null;
            }
        }

        private static string ResolveOperationName(ESWorkbenchMutationKind kind)
        {
            switch (kind)
            {
                case ESWorkbenchMutationKind.Create: return "放置对象";
                case ESWorkbenchMutationKind.Move: return "移动对象";
                case ESWorkbenchMutationKind.Rotate: return "旋转对象";
                case ESWorkbenchMutationKind.Scale: return "缩放对象";
                case ESWorkbenchMutationKind.Duplicate: return "复制对象";
                case ESWorkbenchMutationKind.Delete: return "删除对象";
                default: return "作者操作";
            }
        }
    }

    public sealed class ESWorkbenchToolDescriptor
    {
        public ESWorkbenchToolDescriptor(
            string toolId,
            string displayName,
            Action<ESWorkbenchActionContext> activate,
            string tooltip = null,
            Texture icon = null,
            int priority = 0,
            Func<ESWorkbenchActionContext, bool> isAvailable = null,
            ESWorkbenchShortcut? shortcut = null,
            ESWorkbenchToolCapabilities capabilities = ESWorkbenchToolCapabilities.Auto)
        {
            if (string.IsNullOrWhiteSpace(toolId)) throw new ArgumentException("工具 ID 不能为空。", nameof(toolId));
            ToolId = toolId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ToolId : displayName.Trim();
            Tooltip = tooltip ?? string.Empty;
            Icon = icon;
            Priority = priority;
            Activate = activate ?? throw new ArgumentNullException(nameof(activate));
            IsAvailable = isAvailable;
            Shortcut = shortcut;
            Capabilities = capabilities;
        }

        public string ToolId { get; }
        public string DisplayName { get; }
        public string Tooltip { get; }
        public Texture Icon { get; }
        public int Priority { get; }
        public Action<ESWorkbenchActionContext> Activate { get; }
        public Func<ESWorkbenchActionContext, bool> IsAvailable { get; }
        public ESWorkbenchShortcut? Shortcut { get; }
        public ESWorkbenchToolCapabilities Capabilities { get; }
    }

    public enum ESWorkbenchCommandRole : byte
    {
        Primary,
        History,
        Validation,
        Authoring,
        Build,
        Dangerous,
        Utility
    }

    public enum ESWorkbenchCommandVisibility : byte
    {
        Adaptive,
        Pinned
    }

    public sealed class ESWorkbenchCommandDescriptor
    {
        public ESWorkbenchCommandDescriptor(
            string commandId,
            string displayName,
            Action<ESWorkbenchActionContext> execute,
            string tooltip = null,
            Texture icon = null,
            int priority = 0,
            ESWorkbenchShortcut? shortcut = null,
            Func<ESWorkbenchActionContext, bool> canExecute = null,
            bool showInToolbar = true,
            bool showInContextMenu = false,
            bool iconOnly = false,
            ESWorkbenchCommandRole role = ESWorkbenchCommandRole.Utility,
            ESWorkbenchCommandVisibility visibility = ESWorkbenchCommandVisibility.Adaptive,
            string unityIconName = null)
        {
            if (string.IsNullOrWhiteSpace(commandId)) throw new ArgumentException("命令 ID 不能为空。", nameof(commandId));
            CommandId = commandId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? CommandId : displayName.Trim();
            Tooltip = tooltip ?? string.Empty;
            Icon = icon;
            Priority = priority;
            Shortcut = shortcut;
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            CanExecute = canExecute;
            ShowInToolbar = showInToolbar;
            ShowInContextMenu = showInContextMenu;
            IconOnly = iconOnly;
            Role = role;
            Visibility = visibility;
            UnityIconName = unityIconName?.Trim() ?? string.Empty;
        }

        public string CommandId { get; }
        public string DisplayName { get; }
        public string Tooltip { get; }
        public Texture Icon { get; }
        public int Priority { get; }
        public ESWorkbenchShortcut? Shortcut { get; }
        public Action<ESWorkbenchActionContext> Execute { get; }
        public Func<ESWorkbenchActionContext, bool> CanExecute { get; }
        public bool ShowInToolbar { get; }
        public bool ShowInContextMenu { get; }
        public bool IconOnly { get; }
        public ESWorkbenchCommandRole Role { get; }
        public ESWorkbenchCommandVisibility Visibility { get; }
        public string UnityIconName { get; }
        public bool HasIcon => Icon != null || !string.IsNullOrEmpty(UnityIconName);
    }

    public sealed class ESWorkbenchContentPresetDescriptor
    {
        public ESWorkbenchContentPresetDescriptor(
            string presetId,
            string displayName,
            string tooltip = null,
            object payload = null,
            bool overridePayload = false,
            Texture icon = null,
            string subtitle = null,
            string badge = null)
        {
            if (string.IsNullOrWhiteSpace(presetId))
                throw new ArgumentException("内容预设 ID 不能为空。", nameof(presetId));
            PresetId = presetId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? PresetId : displayName.Trim();
            Tooltip = tooltip ?? string.Empty;
            Payload = payload;
            OverridePayload = overridePayload;
            Icon = icon;
            Subtitle = subtitle ?? string.Empty;
            Badge = badge ?? string.Empty;
        }

        public string PresetId { get; }
        public string DisplayName { get; }
        public string Tooltip { get; }
        public object Payload { get; }
        public bool OverridePayload { get; }
        public Texture Icon { get; }
        public string Subtitle { get; }
        public string Badge { get; }
    }

    [Serializable]
    public sealed class ESWorkbenchContentUsageRecord
    {
        public string objectId = string.Empty;
        public bool favorite;
        public long lastUsedUtcTicks;
        public int useCount;
    }

    /// <summary>
    /// 内容中心的轻量本地偏好。只持久化 WorkbenchId + ObjectId + 时间/次数，
    /// 不保存 Unity 对象、描述器、Payload 或 InstanceId。
    /// </summary>
    internal sealed class ESWorkbenchContentUsageStore
    {
        [Serializable]
        private sealed class UsageDocument
        {
            public int schemaVersion = 1;
            public List<ESWorkbenchContentUsageRecord> entries = new List<ESWorkbenchContentUsageRecord>();
        }

        private const int MaximumEntryCount = 256;
        private const string KeyPrefix = "ES.Workbench.ContentUsage.v1.";
        private readonly string preferencesKey;
        private readonly Dictionary<string, ESWorkbenchContentUsageRecord> records =
            new Dictionary<string, ESWorkbenchContentUsageRecord>(StringComparer.Ordinal);

        public ESWorkbenchContentUsageStore(string workbenchId)
        {
            string stableWorkbenchId = string.IsNullOrWhiteSpace(workbenchId) ? "unknown" : workbenchId.Trim();
            preferencesKey = KeyPrefix + Hash128.Compute(stableWorkbenchId).ToString();
            Load();
        }

        public ESWorkbenchContentUsageRecord Get(string objectId)
        {
            string stableId = NormalizeObjectId(objectId);
            return records.TryGetValue(stableId, out ESWorkbenchContentUsageRecord record)
                ? record
                : null;
        }

        public bool IsFavorite(string objectId) => Get(objectId)?.favorite == true;

        public bool ToggleFavorite(string objectId)
        {
            ESWorkbenchContentUsageRecord record = GetOrCreate(objectId);
            record.favorite = !record.favorite;
            Save();
            return record.favorite;
        }

        public void RecordUse(string objectId)
        {
            ESWorkbenchContentUsageRecord record = GetOrCreate(objectId);
            record.lastUsedUtcTicks = DateTime.UtcNow.Ticks;
            if (record.useCount < int.MaxValue) record.useCount++;
            Save();
        }

        internal IReadOnlyList<ESWorkbenchContentUsageRecord> Snapshot()
        {
            return records.Values
                .OrderByDescending(value => value.favorite)
                .ThenByDescending(value => value.lastUsedUtcTicks)
                .ThenByDescending(value => value.useCount)
                .ThenBy(value => value.objectId, StringComparer.Ordinal)
                .Select(Clone)
                .ToArray();
        }

        private ESWorkbenchContentUsageRecord GetOrCreate(string objectId)
        {
            string stableId = NormalizeObjectId(objectId);
            if (string.IsNullOrEmpty(stableId))
                throw new ArgumentException("内容稳定 ID 不能为空。", nameof(objectId));
            if (records.TryGetValue(stableId, out ESWorkbenchContentUsageRecord existing)) return existing;
            var created = new ESWorkbenchContentUsageRecord { objectId = stableId };
            records.Add(stableId, created);
            return created;
        }

        private void Load()
        {
            records.Clear();
            string json = EditorPrefs.GetString(preferencesKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                UsageDocument document = JsonUtility.FromJson<UsageDocument>(json);
                if (document?.entries == null) return;
                foreach (ESWorkbenchContentUsageRecord entry in document.entries)
                {
                    if (entry == null) continue;
                    string stableId = NormalizeObjectId(entry.objectId);
                    if (string.IsNullOrEmpty(stableId) || records.ContainsKey(stableId)) continue;
                    entry.objectId = stableId;
                    entry.useCount = Mathf.Max(0, entry.useCount);
                    entry.lastUsedUtcTicks = Math.Max(0L, entry.lastUsedUtcTicks);
                    records.Add(stableId, entry);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ESWorkbench] 内容使用记录读取失败，将使用空状态：" + exception.Message);
                records.Clear();
            }
        }

        private void Save()
        {
            ESWorkbenchContentUsageRecord[] retained = records.Values
                .Where(value => value != null && (!string.IsNullOrWhiteSpace(value.objectId)))
                .OrderByDescending(value => value.favorite)
                .ThenByDescending(value => value.lastUsedUtcTicks)
                .ThenByDescending(value => value.useCount)
                .ThenBy(value => value.objectId, StringComparer.Ordinal)
                .Take(MaximumEntryCount)
                .Select(Clone)
                .ToArray();
            records.Clear();
            foreach (ESWorkbenchContentUsageRecord entry in retained) records.Add(entry.objectId, entry);
            var document = new UsageDocument { entries = retained.ToList() };
            EditorPrefs.SetString(preferencesKey, JsonUtility.ToJson(document));
        }

        private static string NormalizeObjectId(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId)) return string.Empty;
            string trimmed = objectId.Trim();
            int presetIndex = trimmed.IndexOf("::preset::", StringComparison.Ordinal);
            return presetIndex > 0 ? trimmed.Substring(0, presetIndex) : trimmed;
        }

        private static ESWorkbenchContentUsageRecord Clone(ESWorkbenchContentUsageRecord value)
        {
            return new ESWorkbenchContentUsageRecord
            {
                objectId = value.objectId,
                favorite = value.favorite,
                lastUsedUtcTicks = value.lastUsedUtcTicks,
                useCount = value.useCount
            };
        }
    }

    public sealed class ESWorkbenchObjectDescriptor
    {
        public ESWorkbenchObjectDescriptor(
            string objectId,
            string displayName,
            string category,
            UnityEngine.Object source,
            object payload = null,
            Texture icon = null,
            string tooltip = null,
            int priority = 0,
            string subtitle = null,
            string badge = null,
            ESWorkbenchContentKind contentKind = ESWorkbenchContentKind.General,
            ESWorkbenchContentDragMode dragMode = ESWorkbenchContentDragMode.Place,
            string selectionKind = null,
            IReadOnlyList<ESWorkbenchContentPresetDescriptor> presets = null,
            string baseObjectId = null,
            string presetId = null)
        {
            if (string.IsNullOrWhiteSpace(objectId)) throw new ArgumentException("对象 ID 不能为空。", nameof(objectId));
            ObjectId = objectId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ObjectId : displayName.Trim();
            Category = string.IsNullOrWhiteSpace(category) ? "常用" : category.Trim();
            Source = source;
            Payload = payload;
            Icon = icon;
            Tooltip = tooltip ?? string.Empty;
            Priority = priority;
            Subtitle = subtitle ?? string.Empty;
            Badge = badge ?? string.Empty;
            ContentKind = contentKind;
            DragMode = dragMode;
            SelectionKind = string.IsNullOrWhiteSpace(selectionKind)
                ? "palette-object"
                : selectionKind.Trim();
            BaseObjectId = string.IsNullOrWhiteSpace(baseObjectId) ? ObjectId : baseObjectId.Trim();
            PresetId = presetId?.Trim() ?? string.Empty;
            Presets = presets == null
                ? Array.Empty<ESWorkbenchContentPresetDescriptor>()
                : presets.Where(value => value != null)
                    .GroupBy(value => value.PresetId, StringComparer.Ordinal)
                    .Select(value => value.First())
                    .ToArray();
        }

        public string ObjectId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public UnityEngine.Object Source { get; }
        public object Payload { get; }
        public Texture Icon { get; }
        public string Tooltip { get; }
        public int Priority { get; }
        public string Subtitle { get; }
        public string Badge { get; }
        public ESWorkbenchContentKind ContentKind { get; }
        public ESWorkbenchContentDragMode DragMode { get; }
        public string SelectionKind { get; }
        public string BaseObjectId { get; }
        public string PresetId { get; }
        public IReadOnlyList<ESWorkbenchContentPresetDescriptor> Presets { get; }
        public bool HasPresets => Presets.Count > 0;
        public bool CanDrag => DragMode != ESWorkbenchContentDragMode.InspectOnly;
        public ESWorkbenchSelection ToSelection() => new ESWorkbenchSelection(BaseObjectId, SelectionKind, Source, this);

        public ESWorkbenchObjectDescriptor CreatePresetVariant(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId) || Presets.Count == 0) return this;
            ESWorkbenchContentPresetDescriptor preset = Presets.FirstOrDefault(value =>
                string.Equals(value.PresetId, presetId, StringComparison.Ordinal));
            if (preset == null) return this;
            string effectiveObjectId = BaseObjectId + "::preset::" + preset.PresetId;
            return new ESWorkbenchObjectDescriptor(
                effectiveObjectId,
                DisplayName,
                Category,
                Source,
                preset.OverridePayload ? preset.Payload : Payload,
                preset.Icon ?? Icon,
                string.IsNullOrWhiteSpace(preset.Tooltip) ? Tooltip : preset.Tooltip,
                Priority,
                string.IsNullOrWhiteSpace(preset.Subtitle) ? Subtitle : preset.Subtitle,
                string.IsNullOrWhiteSpace(preset.Badge) ? Badge : preset.Badge,
                ContentKind,
                DragMode,
                SelectionKind,
                Presets,
                BaseObjectId,
                preset.PresetId);
        }

        public string ContentKindDisplayName
        {
            get
            {
                switch (ContentKind)
                {
                    case ESWorkbenchContentKind.Prefab: return "预制件";
                    case ESWorkbenchContentKind.Brush: return "笔刷";
                    case ESWorkbenchContentKind.SceneTemplate: return "场景";
                    case ESWorkbenchContentKind.RegionTemplate: return "区域";
                    case ESWorkbenchContentKind.Terrain: return "地形";
                    case ESWorkbenchContentKind.Vegetation: return "植被";
                    case ESWorkbenchContentKind.Gameplay: return "玩法";
                    default: return "通用";
                }
            }
        }

        public string DefaultDragHint
        {
            get
            {
                switch (DragMode)
                {
                    case ESWorkbenchContentDragMode.ActivateTool: return "拖入使用";
                    case ESWorkbenchContentDragMode.ApplyTemplate: return "拖入应用";
                    case ESWorkbenchContentDragMode.CreateRegion: return "拖入创建";
                    case ESWorkbenchContentDragMode.InspectOnly: return "仅查看";
                    default: return "可拖放";
                }
            }
        }
    }

    public sealed class ESWorkbenchHierarchyDescriptor
    {
        public ESWorkbenchHierarchyDescriptor(
            string itemId,
            string displayName,
            string parentId = null,
            string kind = null,
            UnityEngine.Object unityObject = null,
            object payload = null,
            Texture icon = null,
            int order = 0,
            ESWorkbenchSpatialDescriptor spatial = null)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("层级项 ID 不能为空。", nameof(itemId));
            ItemId = itemId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName.Trim();
            ParentId = parentId ?? string.Empty;
            Kind = kind ?? "hierarchy-object";
            UnityObject = unityObject;
            Payload = payload;
            Icon = icon;
            Order = order;
            Spatial = spatial;
        }

        public string ItemId { get; }
        public string DisplayName { get; }
        public string ParentId { get; }
        public string Kind { get; }
        public UnityEngine.Object UnityObject { get; }
        public object Payload { get; }
        public Texture Icon { get; }
        public int Order { get; }
        public ESWorkbenchSpatialDescriptor Spatial { get; }
        public ESWorkbenchSelection ToSelection() => new ESWorkbenchSelection(ItemId, Kind, UnityObject, Payload ?? this);
    }

    public enum ESWorkbenchSpatialShape : byte
    {
        Point,
        Rectangle,
        Object
    }

    /// <summary>
    /// 层级对象的只读空间投影。领域仍拥有作者数据与变更语义，工作台只用它完成通用绘制、命中与落点换算。
    /// </summary>
    public sealed class ESWorkbenchSpatialDescriptor
    {
        public ESWorkbenchSpatialDescriptor(
            Vector3 position,
            Vector3 size,
            Vector3 rotationEuler = default,
            ESWorkbenchSpatialShape shape = ESWorkbenchSpatialShape.Object,
            Color? color = null,
            bool visibleIn2D = true,
            bool visibleIn3D = true)
        {
            Position = position;
            Size = new Vector3(
                Mathf.Max(0.001f, Mathf.Abs(size.x)),
                Mathf.Max(0.001f, Mathf.Abs(size.y)),
                Mathf.Max(0.001f, Mathf.Abs(size.z)));
            RotationEuler = rotationEuler;
            Shape = shape;
            Color = color ?? new Color(0.19f, 0.66f, 0.82f, 0.78f);
            VisibleIn2D = visibleIn2D;
            VisibleIn3D = visibleIn3D;
        }

        public Vector3 Position { get; }
        public Vector3 Size { get; }
        public Vector3 RotationEuler { get; }
        public ESWorkbenchSpatialShape Shape { get; }
        public Color Color { get; }
        public bool VisibleIn2D { get; }
        public bool VisibleIn3D { get; }
        public Bounds Bounds => new Bounds(Position, Size);
    }

    public sealed class ESWorkbenchInspectorDescriptor
    {
        public ESWorkbenchInspectorDescriptor(
            string inspectorId,
            Func<ESWorkbenchSelection, bool> matches,
            Func<ESWorkbenchActionContext, ESWorkbenchSelection, VisualElement> createView,
            int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(inspectorId)) throw new ArgumentException("Inspector ID 不能为空。", nameof(inspectorId));
            InspectorId = inspectorId.Trim();
            Matches = matches ?? throw new ArgumentNullException(nameof(matches));
            CreateView = createView ?? throw new ArgumentNullException(nameof(createView));
            Priority = priority;
        }

        public string InspectorId { get; }
        public Func<ESWorkbenchSelection, bool> Matches { get; }
        public Func<ESWorkbenchActionContext, ESWorkbenchSelection, VisualElement> CreateView { get; }
        public int Priority { get; }
    }

    public sealed class ESWorkbenchDropContext
    {
        internal ESWorkbenchDropContext(
            ESWorkbenchActionContext actionContext,
            ESWorkbenchObjectDescriptor item,
            Vector2 localPosition,
            Rect viewportRect)
        {
            Actions = actionContext;
            Item = item;
            LocalPosition = localPosition;
            ViewportRect = viewportRect;
        }

        public ESWorkbenchActionContext Actions { get; }
        public ESWorkbenchObjectDescriptor Item { get; }
        public Vector2 LocalPosition { get; }
        public Rect ViewportRect { get; }
    }

    public sealed class ESWorkbenchBatchDropContext
    {
        internal ESWorkbenchBatchDropContext(
            ESWorkbenchActionContext actionContext,
            IReadOnlyList<ESWorkbenchObjectDescriptor> items,
            Vector2 localPosition,
            Rect viewportRect,
            float spacing)
        {
            Actions = actionContext;
            Items = items ?? Array.Empty<ESWorkbenchObjectDescriptor>();
            LocalPosition = localPosition;
            ViewportRect = viewportRect;
            Spacing = Mathf.Max(0.25f, spacing);
        }

        public ESWorkbenchActionContext Actions { get; }
        public IReadOnlyList<ESWorkbenchObjectDescriptor> Items { get; }
        public Vector2 LocalPosition { get; }
        public Rect ViewportRect { get; }
        public float Spacing { get; }
    }

    /// <summary>
    /// 拖放目标的公共视觉状态。它只表达预检结果，不代表正式提交结果；
    /// 所有视口都应使用同一接受/拒绝语义和原因文本。
    /// </summary>
    public readonly struct ESWorkbenchDropPreviewState
    {
        public ESWorkbenchDropPreviewState(bool accepted, string reason = null)
        {
            Accepted = accepted;
            Reason = reason ?? string.Empty;
        }

        public bool Accepted { get; }
        public string Reason { get; }
        public bool Rejected => !Accepted;

        public static ESWorkbenchDropPreviewState Allowed =>
            new ESWorkbenchDropPreviewState(true);

        public static ESWorkbenchDropPreviewState RejectedBy(string reason) =>
            new ESWorkbenchDropPreviewState(false, reason);

        public string ShortReason(int maximumCharacters = 48)
        {
            int limit = Mathf.Clamp(maximumCharacters, 8, 256);
            if (Reason.Length <= limit) return Reason;
            return Reason.Substring(0, limit) + "…";
        }
    }

    /// <summary>拖放悬停期间的只读预览请求。预览不得写作者数据或注册 Undo。</summary>
    public sealed class ESWorkbenchDropPreviewContext
    {
        internal ESWorkbenchDropPreviewContext(
            ESWorkbenchActionContext actions,
            ESWorkbenchObjectDescriptor primaryItem,
            IReadOnlyList<ESWorkbenchObjectDescriptor> items,
            Vector2 localPosition,
            Rect viewportRect,
            float spacing,
            bool accepted,
            string reason,
            Vector3 resolvedWorldPosition = default,
            bool hasResolvedWorldPosition = false)
        {
            Actions = actions;
            PrimaryItem = primaryItem;
            Items = items ?? Array.Empty<ESWorkbenchObjectDescriptor>();
            LocalPosition = localPosition;
            ViewportRect = viewportRect;
            Spacing = Mathf.Max(0.25f, spacing);
            Accepted = accepted;
            Reason = reason ?? string.Empty;
            ResolvedWorldPosition = resolvedWorldPosition;
            HasResolvedWorldPosition = hasResolvedWorldPosition;
        }

        public ESWorkbenchActionContext Actions { get; }
        public ESWorkbenchObjectDescriptor PrimaryItem { get; }
        public IReadOnlyList<ESWorkbenchObjectDescriptor> Items { get; }
        public Vector2 LocalPosition { get; }
        public Rect ViewportRect { get; }
        public float Spacing { get; }
        public bool Accepted { get; }
        public string Reason { get; }
        /// <summary>
        /// 宿主在本次拖动事件中已经通过正式落点合同解析出的世界位置。
        /// 预览可以直接复用它，避免同一事件内因高度场、相机或边缘平移
        /// 状态变化而重复解析出不同落点。
        /// </summary>
        public Vector3 ResolvedWorldPosition { get; }
        public bool HasResolvedWorldPosition { get; }
        public ESWorkbenchDropPreviewState State =>
            new ESWorkbenchDropPreviewState(Accepted, Reason);
        public bool IsBatch => Items.Count > 1;
    }

    public sealed class ESWorkbenchViewportContext
    {
        internal ESWorkbenchViewportContext(
            EditorWindow window,
            ESWorkbenchActionContext actions,
            string viewportId,
            ESWorkbenchViewportLayoutState layout,
            Func<IReadOnlyList<ESWorkbenchHierarchyDescriptor>> getHierarchy = null,
            Func<string, bool> isHierarchyVisible = null,
            Func<string, bool> isHierarchyLocked = null,
            Action statusChanged = null,
            ESWorkbenchViewportFeelSettings feel = null,
            ESWorkbenchPointerInteractionCoordinator pointerCoordinator = null)
        {
            Window = window;
            Actions = actions;
            ViewportId = viewportId ?? string.Empty;
            Layout = layout ?? new ESWorkbenchViewportLayoutState { viewportId = ViewportId };
            GetHierarchy = getHierarchy;
            IsHierarchyVisible = isHierarchyVisible ?? (_ => true);
            IsHierarchyLocked = isHierarchyLocked ?? (_ => false);
            StatusChanged = statusChanged;
            Feel = feel ?? ESWorkbenchViewportFeelSettings.Standard;
            PointerCoordinator = pointerCoordinator ?? new ESWorkbenchPointerInteractionCoordinator();
        }

        public EditorWindow Window { get; }
        public ESWorkbenchActionContext Actions { get; }
        public string ViewportId { get; }
        public ESWorkbenchViewportLayoutState Layout { get; }
        public Func<IReadOnlyList<ESWorkbenchHierarchyDescriptor>> GetHierarchy { get; }
        public Func<string, bool> IsHierarchyVisible { get; }
        public Func<string, bool> IsHierarchyLocked { get; }
        /// <summary>视口状态发生高频变化（例如指针坐标）时通知宿主刷新状态栏。</summary>
        public Action StatusChanged { get; }
        /// <summary>当前工作台统一的视口手感合同；贡献视口不得各自重新定义默认响应曲线。</summary>
        public ESWorkbenchViewportFeelSettings Feel { get; }
        /// <summary>宿主共享的主指针仲裁器；所有贡献视口必须复用此实例。</summary>
        public ESWorkbenchPointerInteractionCoordinator PointerCoordinator { get; }
        public IReadOnlyList<ESWorkbenchHierarchyDescriptor> Hierarchy =>
            GetHierarchy?.Invoke() ?? Array.Empty<ESWorkbenchHierarchyDescriptor>();
        public ESWorkbenchSelectionService Selection => Actions.Selection;

        public Vector3 SnapPosition(Vector3 value)
        {
            return Layout.snapEnabled ? Snap(value, Mathf.Max(0.001f, Layout.moveSnap)) : value;
        }

        public Vector3 SnapRotation(Vector3 value)
        {
            return Layout.snapEnabled ? Snap(value, Mathf.Max(0.1f, Layout.rotationSnap)) : value;
        }

        public Vector3 SnapScale(Vector3 value)
        {
            return Layout.snapEnabled ? Snap(value, Mathf.Max(0.001f, Layout.scaleSnap)) : value;
        }

        private static Vector3 Snap(Vector3 value, float step)
        {
            return new Vector3(
                Mathf.Round(value.x / step) * step,
                Mathf.Round(value.y / step) * step,
                Mathf.Round(value.z / step) * step);
        }
    }

    public interface IESWorkbenchViewport : IDisposable
    {
        VisualElement Root { get; }
        void Activate();
        void Deactivate();
        void Refresh(ESWorkbenchRefreshReason reason);
        bool CanAccept(ESWorkbenchObjectDescriptor item);
        bool TryAccept(ESWorkbenchDropContext context, out string message);
    }

    /// <summary>
    /// 可选的当前手势取消合同。外部内容拖放接管主指针时优先调用它，
    /// 只清理临时手势/预览，不通过停用再激活视口触发无关重建。
    /// </summary>
    public interface IESWorkbenchCancelableViewport
    {
        void CancelInteraction();
    }

    /// <summary>可选的拖放预检诊断合同；预检只解释可接受性，不得修改作者数据。</summary>
    public interface IESWorkbenchViewportDropDiagnostics
    {
        bool CanAccept(ESWorkbenchObjectDescriptor item, out string reason);
    }

    /// <summary>
    /// 可选的拖放位置预检合同。内容类型可用并不等于当前指针位置可提交；
    /// 视口应在这里复用正式落点解析，但不得修改作者数据；成功时同时返回
    /// 本次预检得到的世界落点，供预览直接复用，避免重复投影造成漂移。
    /// </summary>
    public interface IESWorkbenchViewportDropPositionDiagnostics
    {
        bool TryResolveDropPosition(
            ESWorkbenchObjectDescriptor item,
            Vector2 localPosition,
            out Vector3 worldPosition,
            out string reason);
    }

    /// <summary>
    /// 可选的统一空间投影合同。调用方必须声明投影意图，不能用一组布尔参数
    /// 猜测“严格命中、地形绘制、拖放夹取、边缘平移”之间的边界。
    /// </summary>
    public interface IESWorkbenchViewportProjection
    {
        bool TryResolveProjection(
            Vector2 localPosition,
            ESWorkbenchViewportProjectionRequest request,
            out Vector3 worldPosition);
    }

    /// <summary>
    /// 可选的批量放置合同。实现方必须先完整预检，再以一个 Undo 事务提交或整体回滚。
    /// </summary>
    public interface IESWorkbenchBatchViewport
    {
        bool CanAcceptBatch(IReadOnlyList<ESWorkbenchObjectDescriptor> items, out string reason);
        bool TryAcceptBatch(ESWorkbenchBatchDropContext context, out string message);
    }

    /// <summary>可选的实时拖放预览合同；离开、取消、切换视口和释放时必须清理预览资源。</summary>
    public interface IESWorkbenchDropPreviewViewport
    {
        void UpdateDropPreview(ESWorkbenchDropPreviewContext context);
        void ClearDropPreview();
    }

    /// <summary>
    /// 可选的视口边缘平移能力。宿主只在外部拖放期间调度，视口自行拥有导航状态和重绘。
    /// </summary>
    public interface IESWorkbenchEdgePannableViewport
    {
        bool TryEdgePan(Vector2 localPosition, float deltaTime);
    }

    /// <summary>可选的键盘微调能力；宿主负责快捷键优先级，视口负责目标和正式事务。</summary>
    public interface IESWorkbenchNudgeableViewport
    {
        bool TryNudge(KeyCode keyCode, bool shift, bool controlOrCommand, out string message);
    }

    public interface IESWorkbenchFrameableViewport
    {
        void FrameAll();
    }

    /// <summary>视口对宿主提供的只读生产状态；状态不拥有领域数据，也不执行写入。</summary>
    public interface IESWorkbenchViewportStatusProvider
    {
        IReadOnlyList<ESWorkbenchViewportStatusDescriptor> GetStatusSnapshot();
    }

    public sealed class ESWorkbenchViewportDescriptor
    {
        public ESWorkbenchViewportDescriptor(
            string viewportId,
            string displayName,
            ESWorkbenchViewportKind kind,
            Func<ESWorkbenchViewportContext, IESWorkbenchViewport> create,
            string tooltip = null,
            Texture icon = null,
            int priority = 0,
            Func<ESWorkbenchActionContext, bool> isAvailable = null)
        {
            if (string.IsNullOrWhiteSpace(viewportId)) throw new ArgumentException("视口 ID 不能为空。", nameof(viewportId));
            ViewportId = viewportId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ViewportId : displayName.Trim();
            Kind = kind;
            Create = create ?? throw new ArgumentNullException(nameof(create));
            Tooltip = tooltip ?? string.Empty;
            Icon = icon;
            Priority = priority;
            IsAvailable = isAvailable;
        }

        public string ViewportId { get; }
        public string DisplayName { get; }
        public ESWorkbenchViewportKind Kind { get; }
        public string Tooltip { get; }
        public Texture Icon { get; }
        public int Priority { get; }
        public Func<ESWorkbenchViewportContext, IESWorkbenchViewport> Create { get; }
        public Func<ESWorkbenchActionContext, bool> IsAvailable { get; }
    }
}
#endif
