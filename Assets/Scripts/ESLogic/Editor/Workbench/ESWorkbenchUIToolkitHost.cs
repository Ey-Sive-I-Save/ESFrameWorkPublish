#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ES.EditorInternal;

namespace ES
{
    internal sealed class ESWorkbenchUIToolkitHost : IDisposable
    {
        private sealed class ObjectRowState
        {
            public ESWorkbenchObjectDescriptor item;
            public readonly ESWorkbenchContentDragSession drag;
            public bool hovered;
            public int pulseVersion;

            public ObjectRowState(float dragThreshold)
            {
                drag = new ESWorkbenchContentDragSession(
                    ESWorkbenchContentDragSource.ObjectRow,
                    dragThreshold);
            }
        }

        private sealed class ContentGridRow
        {
            public ESWorkbenchObjectDescriptor first;
            public ESWorkbenchObjectDescriptor second;
        }

        private sealed class ContentCardState
        {
            public ESWorkbenchObjectDescriptor item;
            public readonly ESWorkbenchContentDragSession drag;
            public bool hovered;
            public int pulseVersion;

            public ContentCardState(float dragThreshold)
            {
                drag = new ESWorkbenchContentDragSession(
                    ESWorkbenchContentDragSource.ContentCard,
                    dragThreshold);
            }
        }

        private sealed class ContentCategoryNode
        {
            public string path;
            public string label;
            public int depth;
            public int count;
            public bool hasChildren;
        }

        private sealed class ThumbnailEntry
        {
            public UnityEngine.Object source;
            public Texture texture;
            public Texture fallback;
            public int attempts;
            public bool complete;
        }

        private sealed class DocumentTabItem
        {
            public string id;
            public string label;
            public string tooltip;
        }

        private sealed class ContentKindTabItem
        {
            public string id;
            public string label;
            public int count;
        }

        private const string DragPayloadKey = "ES.Workbench.ObjectDescriptor";
        private const string BatchDragPayloadKey = "ES.Workbench.ObjectDescriptor.Batch";
        internal const string DragSessionKey = "ES.Workbench.DragSession";

        internal static bool IsExternalContentDragActive =>
            DragAndDrop.GetGenericData(DragSessionKey) != null;
        private const int MaximumThumbnailAttempts = 24;
        private const int GeneratedThumbnailWidth = 192;
        private const int GeneratedThumbnailHeight = 128;
        private const int MaximumGeneratedThumbnailCacheEntries = 256;
        private static readonly ESWorkbenchResponsiveLayoutPolicy DefaultLayoutPolicy = new ESWorkbenchResponsiveLayoutPolicy();

        private readonly EditorWindow owner;
        private readonly ESWorkbenchActionContext actions;
        private readonly string workbenchId;
        private ESWorkbenchHostPresentationDescriptor presentation;
        private readonly Type assetType;
        private readonly Func<UnityEngine.Object> getAsset;
        private readonly Action<UnityEngine.Object> bindAsset;
        private readonly Func<IReadOnlyList<ESWorkbenchDocumentDefinition>> getDocuments;
        private readonly Func<IReadOnlyList<ESWorkbenchAuthoringModeDefinition>> getAuthoringModes;
        private readonly Func<IReadOnlyList<ESWorkbenchViewportDescriptor>> getViewports;
        private readonly Func<IReadOnlyList<ESWorkbenchObjectDescriptor>> getObjects;
        private readonly Func<IReadOnlyList<ESWorkbenchHierarchyDescriptor>> getHierarchy;
        private readonly Func<IReadOnlyList<ESWorkbenchInspectorDescriptor>> getInspectors;
        private readonly Func<IReadOnlyList<ESWorkbenchToolDescriptor>> getTools;
        private readonly Func<IReadOnlyList<ESWorkbenchCommandDescriptor>> getCommands;
        private readonly Func<IReadOnlyList<ESWorkbenchIssueDescriptor>> getIssues;
        private readonly Func<IReadOnlyList<ESWorkbenchBottomPanelDescriptor>> getBottomPanels;
        private readonly ESWorkbenchViewportFeelSettings viewportFeel;
        private readonly Func<bool> isDirty;
        private readonly VisualElement foundationActionStack;
        private readonly ESWorkbenchLayoutState layout;
        private readonly Func<ESWorkbenchDocumentDefinition, VisualElement> createDocumentView;
        private readonly Action<string> selectDocument;
        private readonly Func<string> getSelectedDocument;
        private readonly Dictionary<string, IESWorkbenchViewport> liveViewports = new Dictionary<string, IESWorkbenchViewport>(StringComparer.Ordinal);
        private readonly Dictionary<string, ToolbarToggle> viewportToggles = new Dictionary<string, ToolbarToggle>(StringComparer.Ordinal);
        private readonly List<ESWorkbenchObjectDescriptor> visibleObjects = new List<ESWorkbenchObjectDescriptor>();
        private readonly List<ESWorkbenchObjectDescriptor> contentSourceSnapshot = new List<ESWorkbenchObjectDescriptor>();
        private readonly List<ESWorkbenchObjectDescriptor> externalDragBatch = new List<ESWorkbenchObjectDescriptor>();
        private readonly Dictionary<string, ESWorkbenchObjectDescriptor> contentSourceById =
            new Dictionary<string, ESWorkbenchObjectDescriptor>(StringComparer.Ordinal);
        private readonly Dictionary<UnityEngine.Object, ESWorkbenchObjectDescriptor> contentSourceBySource =
            new Dictionary<UnityEngine.Object, ESWorkbenchObjectDescriptor>();
        private readonly HashSet<string> externalDragIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ContentGridRow> visibleGridRows = new List<ContentGridRow>();
        private readonly List<ContentKindTabItem> contentKindTabs = new List<ContentKindTabItem>();
        private readonly List<ContentCategoryNode> contentCategoryNodes = new List<ContentCategoryNode>();
        private readonly List<ESWorkbenchHierarchyDescriptor> visibleHierarchy = new List<ESWorkbenchHierarchyDescriptor>();
        private readonly Dictionary<string, ESWorkbenchHierarchyDescriptor> hierarchyById = new Dictionary<string, ESWorkbenchHierarchyDescriptor>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ESWorkbenchHierarchyDescriptor>> hierarchyChildren = new Dictionary<string, List<ESWorkbenchHierarchyDescriptor>>(StringComparer.Ordinal);
        private readonly HashSet<string> expandedHierarchyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> hiddenHierarchyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> lockedHierarchyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ESWorkbenchBottomPanelDescriptor> standardBottomPanels = new List<ESWorkbenchBottomPanelDescriptor>();
        private readonly List<ESWorkbenchBottomPanelDescriptor> resolvedBottomPanels = new List<ESWorkbenchBottomPanelDescriptor>();
        private readonly Dictionary<string, Dictionary<string, ESWorkbenchVisualInteractionObservation>>
            visualInteractionObservationsByScenario =
                new Dictionary<string, Dictionary<string, ESWorkbenchVisualInteractionObservation>>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, Texture> unityIconCache =
            new Dictionary<string, Texture>(StringComparer.Ordinal);
        private readonly Dictionary<string, ThumbnailEntry> thumbnailCache =
            new Dictionary<string, ThumbnailEntry>(StringComparer.Ordinal);
        private readonly Dictionary<ESWorkbenchContentKind, Texture2D> semanticThumbnailCache =
            new Dictionary<ESWorkbenchContentKind, Texture2D>();
        private readonly Dictionary<string, Texture2D> generatedThumbnailCache =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly LinkedList<string> generatedThumbnailLru = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> generatedThumbnailLruNodes =
            new Dictionary<string, LinkedListNode<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> batchContentIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> expandedContentCategoryPaths = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> selectedPresetByObjectId =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly ESWorkbenchContentUsageStore contentUsage;
        private readonly ESWorkbenchPointerOwnershipGate contentPointerGate =
            new ESWorkbenchPointerOwnershipGate();
        private readonly ESWorkbenchPointerInteractionCoordinator pointerCoordinator =
            new ESWorkbenchPointerInteractionCoordinator();

        private VisualElement root;
        private VisualElement commandBar;
        private VisualElement leftPanel;
        private VisualElement leftPanelTitle;
        private VisualElement leftTabs;
        private VisualElement centerPanel;
        private VisualElement inspectorPanel;
        private VisualElement toolRail;
        private VisualElement leftContent;
        private VisualElement documentTabs;
        private VisualElement viewportModeBar;
        private VisualElement centerContent;
        private VisualElement viewportHost;
        private VisualElement viewportFooter;
        private VisualElement dropFeedback;
        private VisualElement inspectorContent;
        private TwoPaneSplitView outerSplit;
        private TwoPaneSplitView contentSplit;
        private TwoPaneSplitView workspaceSplit;
        private VisualElement bottomDrawer;
        private VisualElement bottomTabs;
        private VisualElement bottomContent;
        private ESWorkbenchBottomPanelContent activeBottomPanelContent;
        private Label inspectorTitle;
        private Label statusLabel;
        private Label documentStatusLabel;
        private Button leftPaneButton;
        private Button inspectorPaneButton;
        private Button bottomPaneButton;
        private Button recoveryButton;
        private ToolbarSearchField objectSearch;
        private ToolbarSearchField hierarchySearch;
        private VisualElement contentLibraryHeader;
        private Label contentLibraryTitle;
        private Label contentLibraryDescription;
        private VisualElement objectFilterBar;
        private VisualElement contentKindQuickBar;
        private VisualElement contentScopeBar;
        private VisualElement contentBreadcrumbBar;
        private ToolbarMenu categoryBreadcrumb;
        private ToolbarMenu compactContentFilterMenu;
        private ToolbarMenu sortMenu;
        private ToolbarMenu contentViewMenu;
        private ToolbarMenu batchMenu;
        private ListView contentCategoryList;
        private ListView objectList;
        private ListView objectGridList;
        private ListView hierarchyList;
        private VisualElement contentResults;
        private VisualElement contentBrowser;
        private VisualElement contentKindRail;
        private VisualElement contentModeBar;
        private Button listModeButton;
        private Button gridModeButton;
        private Button batchPlaceButton;
        private Label contentSummaryLabel;
        private Label objectEmptyLabel;
        private Label hierarchyEmptyLabel;
        private string categoryFilter = "全部";
        private string contentKindFilter = "all";
        private ESWorkbenchContentViewMode contentViewMode;
        private ESWorkbenchContentSortMode contentSortMode;
        private ESWorkbenchContentScope contentScope;
        private int contentGridColumns = 1;
        private int contentKindShortcutCapacity = 3;
        private int duplicateContentIdCount;
        private bool compactContentBrowser;
        private bool compactContentVertical;
        private VisualElement contentBrowserResponsiveRail;
        private ToolbarMenu contentBrowserResponsiveMenu;
        private VisualElement contentVerticalResponsiveHeader;
        private string activeLeftTab;
        private string activeDocument;
        private string activeAuthoringModeId;
        private string activatedAuthoringModeId;
        private string activeViewportId;
        private string activeBottomTab;
        private IESWorkbenchViewport activeViewport;
        private bool activeViewportActivated;
        private bool hierarchyExpansionInitialized;
        private float availableWidth = 1200f;
        private float availableHeight = 800f;
        private float availableCenterWidth = 720f;
        private int responsiveSignature = int.MinValue;
        private ESWorkbenchVisualEvidenceCaptureResult? latestVisualEvidence;
        private string latestVisualEvidenceSourceGuid = string.Empty;
        private bool visualLongChineseContent;
        private string selectedVisualScenarioId = string.Empty;
        private IVisualElementScheduledItem thumbnailRefreshSchedule;
        private IVisualElementScheduledItem dragEdgePanSchedule;
        private IESWorkbenchEdgePannableViewport dragEdgePanViewport;
        private readonly ESWorkbenchEdgePanSession dragEdgePanSession =
            new ESWorkbenchEdgePanSession();
        private Vector2 dragEdgePanMousePosition;
        private ESWorkbenchObjectDescriptor dragEdgePanItem;
        private IReadOnlyList<ESWorkbenchObjectDescriptor> dragEdgePanBatch;
        private bool dragEdgePanAccepted;
        private string dragEdgePanReason = string.Empty;
        private object activeDragSessionToken;
        // Native StartDrag transfers control away from UI Toolkit. The source
        // pointer is released synchronously and can emit PointerCaptureOut before
        // DragUpdated/DragPerform; keep the native payload alive across that edge.
        private object externalDragPayloadToken;
        private bool externalDragTransferInFlight;
        private bool externalDragWatchdogRegistered;
        private double externalDragLastSignalTime;
        private const double ExternalDragWatchdogTimeoutSeconds = 1.5d;
        private object externalPointerSessionToken;
        private ESWorkbenchBottomPanelDensity activeBottomPanelDensity = ESWorkbenchBottomPanelDensity.Normal;
        private float appliedBottomDrawerHeight;
        private bool disposed;
        private bool viewportFooterRefreshQueued;

        private ESWorkbenchResponsiveLayoutPolicy LayoutPolicy => presentation?.LayoutPolicy ?? DefaultLayoutPolicy;
        private bool BlocksAuthoringViewport => getAsset?.Invoke() == null
            && presentation?.EmptyState?.BlocksAuthoringViewport == true;

        internal ESWorkbenchUIToolkitHost(
            EditorWindow owner,
            ESWorkbenchActionContext actions,
            string workbenchId,
            string brandTitle,
            Type assetType,
            Func<UnityEngine.Object> getAsset,
            Action<UnityEngine.Object> bindAsset,
            Func<IReadOnlyList<ESWorkbenchDocumentDefinition>> getDocuments,
            Func<IReadOnlyList<ESWorkbenchAuthoringModeDefinition>> getAuthoringModes,
            Func<IReadOnlyList<ESWorkbenchViewportDescriptor>> getViewports,
            Func<IReadOnlyList<ESWorkbenchObjectDescriptor>> getObjects,
            Func<IReadOnlyList<ESWorkbenchHierarchyDescriptor>> getHierarchy,
            Func<IReadOnlyList<ESWorkbenchInspectorDescriptor>> getInspectors,
            Func<IReadOnlyList<ESWorkbenchToolDescriptor>> getTools,
            Func<IReadOnlyList<ESWorkbenchCommandDescriptor>> getCommands,
            ESWorkbenchLayoutState layout,
            Func<ESWorkbenchDocumentDefinition, VisualElement> createDocumentView,
            Action<string> selectDocument,
            Func<string> getSelectedDocument,
            Func<IReadOnlyList<ESWorkbenchIssueDescriptor>> getIssues = null,
            Func<bool> isDirty = null,
            Func<IReadOnlyList<ESWorkbenchBottomPanelDescriptor>> getBottomPanels = null,
            ESWorkbenchHostPresentationDescriptor presentation = null,
            ESWindowActionHosts actionHosts = null,
            ESWorkbenchViewportFeelSettings viewportFeel = null)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
            this.workbenchId = string.IsNullOrWhiteSpace(workbenchId) ? owner.GetType().FullName : workbenchId.Trim();
            contentUsage = new ESWorkbenchContentUsageStore(this.workbenchId);
            this.presentation = presentation ?? ESWorkbenchHostPresentationDescriptor.CreateDefault(brandTitle);
            this.assetType = assetType ?? typeof(UnityEngine.Object);
            this.getAsset = getAsset;
            this.bindAsset = bindAsset;
            this.getDocuments = getDocuments;
            this.getAuthoringModes = getAuthoringModes;
            this.getViewports = getViewports;
            this.getObjects = getObjects;
            this.getHierarchy = getHierarchy;
            this.getInspectors = getInspectors;
            this.getTools = getTools;
            this.getCommands = getCommands;
            this.layout = layout ?? new ESWorkbenchLayoutState();
            this.createDocumentView = createDocumentView;
            this.selectDocument = selectDocument;
            this.getSelectedDocument = getSelectedDocument;
            this.getIssues = getIssues;
            this.isDirty = isDirty;
            this.getBottomPanels = getBottomPanels;
            this.viewportFeel = viewportFeel ?? ESWorkbenchViewportFeelSettings.Standard;
            foundationActionStack = ResolveFoundationActionStack(actionHosts);
            CreateStandardBottomPanelDescriptors();
            if (this.layout.layoutSchemaVersion != 6) ResetLayoutToSchema6(this.layout);
            if (!this.layout.responsiveLayoutInitialized)
            {
                this.layout.leftPaneVisible = true;
                this.layout.inspectorPaneVisible = true;
                this.layout.compactSidePane = "left";
                this.layout.leftPaneWidth = LayoutPolicy.PreferredLeftPaneWidth;
                this.layout.inspectorPaneWidth = LayoutPolicy.PreferredInspectorPaneWidth;
                this.layout.responsiveLayoutInitialized = true;
            }
            activeLeftTab = this.layout.activeLeftTab == "hierarchy" ? "hierarchy" : "objects";
            this.layout.activeLeftTab = activeLeftTab;
            contentKindFilter = string.IsNullOrWhiteSpace(this.layout.activeContentKind)
                ? "all"
                : this.layout.activeContentKind;
            categoryFilter = string.IsNullOrWhiteSpace(this.layout.activeContentCategory)
                ? "全部"
                : this.layout.activeContentCategory;
            contentViewMode = this.layout.contentViewMode;
            contentSortMode = this.layout.contentSortMode;
            contentScope = this.layout.contentScope;
            activeDocument = string.IsNullOrWhiteSpace(this.layout.activeDocument) ? "authoring" : this.layout.activeDocument;
            activeAuthoringModeId = string.IsNullOrWhiteSpace(this.layout.activeAuthoringModeId)
                ? "terrain"
                : this.layout.activeAuthoringModeId;
            activeViewportId = this.layout.activeViewportId ?? string.Empty;
            activeBottomTab = string.IsNullOrWhiteSpace(this.layout.activeBottomTab) ? "problems" : this.layout.activeBottomTab;
            if (activeBottomTab == "build") activeBottomTab = this.layout.activeBottomTab = "tasks";
            hierarchyExpansionInitialized = this.layout.hierarchyExpansionInitialized;
            if (this.layout.expandedHierarchyIds != null)
                expandedHierarchyIds.UnionWith(this.layout.expandedHierarchyIds.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (this.layout.hiddenHierarchyIds != null)
                hiddenHierarchyIds.UnionWith(this.layout.hiddenHierarchyIds.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (this.layout.lockedHierarchyIds != null)
                lockedHierarchyIds.UnionWith(this.layout.lockedHierarchyIds.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (this.layout.selectedContentIds != null)
                batchContentIds.UnionWith(this.layout.selectedContentIds.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (this.layout.expandedContentCategoryPaths != null)
                expandedContentCategoryPaths.UnionWith(
                    this.layout.expandedContentCategoryPaths.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (this.layout.contentPresetSelections != null)
                foreach (ESWorkbenchContentPresetSelectionState state in this.layout.contentPresetSelections)
                    if (state != null && !string.IsNullOrWhiteSpace(state.objectId)
                        && !string.IsNullOrWhiteSpace(state.presetId))
                        selectedPresetByObjectId[state.objectId] = state.presetId;
            actions.Selection.SetChanged += OnSelectionSetChanged;
            actions.Tools.Changed += OnToolChanged;
        }

        private static void ResetLayoutToSchema6(ESWorkbenchLayoutState state)
        {
            state.layoutSchemaVersion = 6;
            state.layoutPreset = ESWorkbenchLayoutPreset.Authoring;
            state.leftPaneWidth = 320f;
            state.inspectorPaneWidth = 320f;
            state.activeLeftTab = "objects";
            state.activeContentKind = "all";
            state.activeContentCategory = "全部";
            state.contentViewMode = ESWorkbenchContentViewMode.List;
            state.contentSortMode = ESWorkbenchContentSortMode.Type;
            state.contentScope = ESWorkbenchContentScope.All;
            state.contentBatchSpacing = 4f;
            state.activeDocument = "authoring";
            state.activeAuthoringModeId = "terrain";
            state.leftPaneVisible = true;
            state.inspectorPaneVisible = true;
            state.compactSidePane = "left";
            state.responsiveLayoutInitialized = false;
            state.bottomDrawerExpanded = false;
            state.bottomDrawerHeight = 220f;
            state.bottomDrawerUserSized = false;
            state.activeBottomTab = "problems";
        }

        internal static void ResetLayoutToSchema6ForTest(ESWorkbenchLayoutState state)
        {
            if (state != null) ResetLayoutToSchema6(state);
        }

        public VisualElement Build()
        {
            // Unshown EditorWindow instances expose a synthetic default rectangle. Only a
            // window attached to a real panel can provide trustworthy initial layout points.
            if (owner.rootVisualElement?.panel != null)
            {
                Rect ownerRect = owner.position;
                if (ownerRect.width > 1f) availableWidth = ownerRect.width;
                if (ownerRect.height > 1f) availableHeight = ownerRect.height;
            }
            availableCenterWidth = LayoutPolicy.ResolveProtectedCenterWidth(availableWidth);
            ApplyMinimumWindowSize();
            root = new VisualElement { name = "ESWorkbenchHost" };
            root.AddToClassList("es-workbench-shell");
            root.style.flexGrow = 1f;
            root.style.minWidth = 0f;
            root.style.minHeight = 0f;
            ESEditorPresentation.ApplyPresentationStyle(
                root,
                ESEditorPresentation.ESPresentationRole.WindowSurface,
                borderWidth: 0f);
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            root.RegisterCallback<PointerDownEvent>(OnVisualEvidencePointerDown, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCancelEvent>(OnRootPointerCancel, TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(OnVisualEvidenceWheel, TrickleDown.TrickleDown);
            root.RegisterCallback<DragExitedEvent>(OnDragExited, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCaptureOutEvent>(OnRootPointerCaptureOut, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusOutEvent>(OnRootFocusOut, TrickleDown.TrickleDown);
            root.RegisterCallback<DetachFromPanelEvent>(OnRootDetachedFromPanel);

            commandBar = CreateHorizontalBar("ESWorkbenchCommandBar", 44f);
            commandBar.AddToClassList("es-workbench-command-bar");
            commandBar.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            root.Add(commandBar);

            float leftWidth = Mathf.Clamp(
                layout.leftPaneWidth,
                LayoutPolicy.MinimumLeftPaneWidth,
                LayoutPolicy.MaximumLeftPaneWidth);
            outerSplit = new TwoPaneSplitView(0, leftWidth, TwoPaneSplitViewOrientation.Horizontal) { name = "ESWorkbenchOuterSplit" };
            outerSplit.style.flexGrow = 1f;
            outerSplit.style.minHeight = 0f;
            outerSplit.Add(BuildLeftPanel());

            float inspectorWidth = Mathf.Clamp(
                layout.inspectorPaneWidth,
                LayoutPolicy.MinimumInspectorPaneWidth,
                LayoutPolicy.MaximumInspectorPaneWidth);
            contentSplit = new TwoPaneSplitView(1, inspectorWidth, TwoPaneSplitViewOrientation.Horizontal) { name = "ESWorkbenchContentSplit" };
            contentSplit.style.flexGrow = 1f;
            contentSplit.style.minHeight = 0f;
            contentSplit.Add(BuildCenterPanel());
            contentSplit.Add(BuildInspectorPanel());
            float maximumBottomHeight = Mathf.Min(
                LayoutPolicy.MaximumBottomDrawerHeight,
                availableHeight * LayoutPolicy.MaximumBottomDrawerRatio);
            workspaceSplit = new TwoPaneSplitView(
                1,
                Mathf.Clamp(
                    layout.bottomDrawerExpanded
                        ? layout.bottomDrawerHeight
                        : LayoutPolicy.CollapsedBottomDrawerHeight,
                    LayoutPolicy.CollapsedBottomDrawerHeight,
                    Mathf.Max(LayoutPolicy.MinimumBottomDrawerHeight, maximumBottomHeight)),
                TwoPaneSplitViewOrientation.Vertical)
            {
                name = "ESWorkbenchWorkspaceSplit"
            };
            workspaceSplit.style.flexGrow = 1f;
            workspaceSplit.style.minHeight = 0f;
            workspaceSplit.Add(contentSplit);
            workspaceSplit.Add(BuildBottomDrawer());
            outerSplit.Add(workspaceSplit);
            RegisterPaneResizeTracking(
                outerSplit,
                () => leftPanel == null ? 0f : leftPanel.resolvedStyle.width,
                value => layout.leftPaneWidth = Mathf.Clamp(
                    value, LayoutPolicy.MinimumLeftPaneWidth, LayoutPolicy.MaximumLeftPaneWidth));
            RegisterPaneResizeTracking(
                contentSplit,
                () => inspectorPanel == null ? 0f : inspectorPanel.resolvedStyle.width,
                value => layout.inspectorPaneWidth = Mathf.Clamp(
                    value, LayoutPolicy.MinimumInspectorPaneWidth, LayoutPolicy.MaximumInspectorPaneWidth));
            RegisterPaneResizeTracking(
                workspaceSplit,
                () => bottomDrawer == null ? 0f : bottomDrawer.resolvedStyle.height,
                value =>
                {
                    layout.bottomDrawerHeight = Mathf.Clamp(
                        value, LayoutPolicy.CompactBottomDrawerHeight, LayoutPolicy.MaximumBottomDrawerHeight);
                    layout.bottomDrawerUserSized = true;
                    appliedBottomDrawerHeight = layout.bottomDrawerHeight;
                });
            root.Add(outerSplit);

            VisualElement status = CreateHorizontalBar("ESWorkbenchStatusBar", 24f);
            status.style.borderTopWidth = 1f;
            status.style.borderTopColor = ESEditorPresentation.DividerColor;
            statusLabel = new Label("就绪") { name = "ESWorkbenchStatus" };
            statusLabel.style.flexGrow = 1f;
            statusLabel.style.minWidth = 0f;
            statusLabel.style.fontSize = 10f;
            statusLabel.style.overflow = Overflow.Hidden;
            statusLabel.style.textOverflow = TextOverflow.Ellipsis;
            status.Add(statusLabel);
            recoveryButton = CreateActionButton(
                EditorGUIUtility.IconContent("d_Refresh").image,
                string.Empty,
                "操作失败后重新解析当前层级、选择、Inspector 与视口。作者事务已经回滚。",
                () => actions.Refresh(ESWorkbenchRefreshReason.Explicit));
            recoveryButton.name = "ESWorkbenchRecovery";
            recoveryButton.style.width = 27f;
            recoveryButton.style.height = 21f;
            recoveryButton.style.display = DisplayStyle.None;
            status.Add(recoveryButton);
            root.Add(status);

            RefreshRegistrations();
            root.schedule.Execute(() =>
            {
                if (disposed) return;
                ApplyPaneVisibility(availableWidth);
                ApplyBottomDrawerVisibility();
            });
            return root;
        }

        public void RefreshRegistrations()
        {
            if (disposed || root == null) return;
            BuildCommandBar();
            BuildToolBar();
            BuildDocumentTabs();
            BuildViewportModes();
            ActivateCurrentAuthoringModeIfNeeded();
            RebuildObjectList();
            RebuildHierarchyList();
            RebuildInspector(actions.Selection.Current);
            BuildBottomTabs();
            RebuildBottomDrawer();
            ShowDocument(activeDocument);
        }

        internal void UpdatePresentation(ESWorkbenchHostPresentationDescriptor descriptor)
        {
            presentation = descriptor ?? ESWorkbenchHostPresentationDescriptor.CreateDefault();
            ApplyMinimumWindowSize();
            responsiveSignature = int.MinValue;
        }

        internal void ReleaseContributedContent()
        {
            externalDragTransferInFlight = false;
            CancelWorkbenchDrag(true);
            if (!string.IsNullOrEmpty(activatedAuthoringModeId))
            {
                try
                {
                    getAuthoringModes?.Invoke()?.FirstOrDefault(value => value != null
                        && value.ModeId == activatedAuthoringModeId)?.Deactivate?.Invoke(actions);
                }
                catch (Exception exception) { Debug.LogException(exception); }
                activatedAuthoringModeId = string.Empty;
            }
            ReleaseBottomPanelContent();
            bottomContent?.Clear();
            centerContent?.Clear();
            DeactivateCurrentViewport();
            foreach (IESWorkbenchViewport viewport in liveViewports.Values)
            {
                try { viewport?.Dispose(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            liveViewports.Clear();
            activeViewport = null;
            viewportToggles.Clear();
        }

        public void Refresh(ESWorkbenchRefreshReason reason)
        {
            if (disposed) return;
            UpdateDocumentStatus();
            activeViewport?.Refresh(reason);
            UpdateViewportFooter();
            if (reason == ESWorkbenchRefreshReason.AssetChanged || reason == ESWorkbenchRefreshReason.DataChanged
                || reason == ESWorkbenchRefreshReason.UndoRedo || reason == ESWorkbenchRefreshReason.Explicit)
            {
                RebuildObjectList();
                RebuildHierarchyList();
                RebuildBottomDrawer();
            }
            // PropertyField is bound to the active SerializedObject. Recreating it while a change
            // event is dispatching loses keyboard focus and can discard an in-progress edit.
            if (reason == ESWorkbenchRefreshReason.AssetChanged || reason == ESWorkbenchRefreshReason.UndoRedo
                || reason == ESWorkbenchRefreshReason.Explicit)
            {
                RebuildInspector(actions.Selection.Current);
            }
        }

        public void SetStatus(string message, MessageType type)
        {
            if (statusLabel == null) return;
            UpdateDocumentStatus();
            statusLabel.text = string.IsNullOrWhiteSpace(message) ? "就绪" : message.Trim();
            ESStatusKind kind = type == MessageType.Error ? ESStatusKind.Error
                : type == MessageType.Warning ? ESStatusKind.Warning : ESStatusKind.Ready;
            statusLabel.style.color = ESEditorPresentation.GetStatusAccent(0, kind);
            statusLabel.tooltip = type == MessageType.Error
                ? statusLabel.text + "\n影响：当前动作没有确认成功。\n恢复：点击右侧刷新按钮重新解析当前工作区；是否已回滚以错误正文为准。"
                : statusLabel.text;
            if (recoveryButton != null)
                recoveryButton.style.display = type == MessageType.Error ? DisplayStyle.Flex : DisplayStyle.None;
            RecordActivity(statusLabel.text, type);
        }

        private VisualElement BuildLeftPanel()
        {
            VisualElement panel = leftPanel = new VisualElement { name = "ESWorkbenchLeftPanel" };
            panel.AddToClassList("es-workbench-side-panel");
            panel.AddToClassList("es-workbench-content-panel");
            panel.style.flexGrow = 1f;
            panel.style.minWidth = LayoutPolicy.MinimumLeftPaneWidth;
            panel.style.minHeight = 0f;
            panel.style.backgroundColor = ESEditorPresentation.WindowInsetSurfaceColor;
            panel.style.borderRightWidth = 0f;
            panel.style.borderRightColor = ESEditorPresentation.DividerColor;
            panel.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float width = evt.newRect.width;
                if (width >= LayoutPolicy.MinimumLeftPaneWidth - 1f
                    && width <= LayoutPolicy.MaximumLeftPaneWidth + 1f)
                    layout.leftPaneWidth = width;
            });

            leftPanelTitle = CreateSectionTitle("ESWorkbenchLeftPanelTitle", presentation.LeftPanelTitle);
            panel.Add(leftPanelTitle);
            VisualElement tabs = leftTabs = CreateHorizontalBar("ESWorkbenchLeftTabs", 38f);
            tabs.style.paddingLeft = 5f;
            tabs.style.paddingRight = 5f;
            AddLeftTab(tabs, "objects", "内容库");
            AddLeftTab(tabs, "hierarchy", "当前结构");
            panel.Add(tabs);

            leftContent = new VisualElement { name = "ESWorkbenchLeftContent" };
            leftContent.style.flexGrow = 1f;
            leftContent.style.minHeight = 0f;
            panel.Add(leftContent);
            ShowLeftTab(activeLeftTab);
            return panel;
        }

        private VisualElement BuildCenterPanel()
        {
            VisualElement panel = centerPanel = new VisualElement { name = "ESWorkbenchCenterPanel" };
            panel.AddToClassList("es-workbench-center-stage");
            panel.style.flexGrow = 1f;
            panel.style.minWidth = LayoutPolicy.MinimumCenterWidth;
            panel.style.minHeight = 0f;
            panel.RegisterCallback<GeometryChangedEvent>(OnCenterGeometryChanged);
            documentTabs = CreateHorizontalBar("ESWorkbenchDocumentTabs", 31f);
            documentTabs.AddToClassList("es-workbench-subbar");
            panel.Add(documentTabs);
            viewportModeBar = CreateHorizontalBar("ESWorkbenchViewportModes", 29f);
            viewportModeBar.AddToClassList("es-workbench-subbar");
            panel.Add(viewportModeBar);
            viewportHost = new VisualElement { name = "ESWorkbenchViewportHost" };
            viewportHost.style.flexGrow = 1f;
            viewportHost.style.minWidth = 0f;
            viewportHost.style.minHeight = 0f;
            viewportHost.style.flexDirection = FlexDirection.Row;
            toolRail = new VisualElement { name = "ESWorkbenchToolRail" };
            toolRail.AddToClassList("es-workbench-tool-rail");
            toolRail.style.width = 46f;
            toolRail.style.minWidth = 46f;
            toolRail.style.flexShrink = 0f;
            toolRail.style.alignItems = Align.Center;
            toolRail.style.paddingTop = 5f;
            toolRail.style.backgroundColor = ESEditorPresentation.ToolbarSurfaceColor;
            toolRail.style.borderRightWidth = 1f;
            toolRail.style.borderRightColor = ESEditorPresentation.DividerColor;
            viewportHost.Add(toolRail);
            VisualElement surface = new VisualElement { name = "ESWorkbenchViewportSurface" };
            surface.AddToClassList("es-workbench-viewport-surface");
            surface.style.flexGrow = 1f;
            surface.style.minWidth = 0f;
            surface.style.minHeight = 0f;
            surface.style.position = Position.Relative;
            centerContent = new VisualElement { name = "ESWorkbenchCenterContent" };
            centerContent.style.flexGrow = 1f;
            centerContent.style.minWidth = 0f;
            centerContent.style.minHeight = 0f;
            // Drag events can target the active viewport's IMGUIContainer or another
            // nested VisualElement. Register in trickle-down so the workbench host
            // receives the native drag before a child consumes/stops propagation;
            // bubbling-only registration made preview/perform depend on the child.
            centerContent.RegisterCallback<DragUpdatedEvent>(OnDragUpdated, TrickleDown.TrickleDown);
            centerContent.RegisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);
            centerContent.RegisterCallback<DragLeaveEvent>(OnDragLeave, TrickleDown.TrickleDown);
            surface.Add(centerContent);
            dropFeedback = new VisualElement { name = "ESWorkbenchDropFeedback", pickingMode = PickingMode.Ignore };
            dropFeedback.style.position = Position.Absolute;
            dropFeedback.style.display = DisplayStyle.None;
            dropFeedback.style.paddingLeft = 10f;
            dropFeedback.style.paddingRight = 10f;
            dropFeedback.style.paddingTop = 6f;
            dropFeedback.style.paddingBottom = 6f;
            dropFeedback.style.backgroundColor = new Color(0.04f, 0.09f, 0.12f, 0.94f);
            dropFeedback.style.minWidth = 268f;
            dropFeedback.style.maxWidth = 340f;
            dropFeedback.style.borderLeftWidth = 3f;
            dropFeedback.style.borderRightWidth = 1f;
            dropFeedback.style.borderTopWidth = 1f;
            dropFeedback.style.borderBottomWidth = 1f;
            dropFeedback.style.borderLeftColor = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            dropFeedback.style.borderRightColor = ESEditorPresentation.DividerColor;
            dropFeedback.style.borderTopColor = ESEditorPresentation.DividerColor;
            dropFeedback.style.borderBottomColor = ESEditorPresentation.DividerColor;
            dropFeedback.style.borderTopLeftRadius = 5f;
            dropFeedback.style.borderTopRightRadius = 5f;
            dropFeedback.style.borderBottomLeftRadius = 5f;
            dropFeedback.style.borderBottomRightRadius = 5f;
            dropFeedback.style.flexDirection = FlexDirection.Row;
            dropFeedback.style.alignItems = Align.Center;
            Image dropPreview = new Image { name = "DropPreview", scaleMode = ScaleMode.ScaleToFit };
            dropPreview.style.width = 52f;
            dropPreview.style.height = 52f;
            dropPreview.style.marginRight = 8f;
            dropPreview.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
            dropFeedback.Add(dropPreview);
            VisualElement dropLabels = new VisualElement();
            dropLabels.style.flexGrow = 1f;
            dropLabels.style.minWidth = 0f;
            VisualElement dropTitleRow = new VisualElement();
            dropTitleRow.style.flexDirection = FlexDirection.Row;
            dropTitleRow.style.alignItems = Align.Center;
            Label dropStatus = new Label { name = "DropStatus", text = "✓" };
            dropStatus.style.width = 18f;
            dropStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
            dropStatus.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            Label dropTitle = new Label { name = "DropTitle" };
            dropTitle.style.flexGrow = 1f;
            dropTitle.style.minWidth = 0f;
            dropTitle.style.overflow = Overflow.Hidden;
            dropTitle.style.textOverflow = TextOverflow.Ellipsis;
            dropTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            Label dropCount = new Label { name = "DropCount" };
            dropCount.style.paddingLeft = 5f;
            dropCount.style.paddingRight = 5f;
            dropCount.style.fontSize = 9f;
            dropCount.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            dropTitleRow.Add(dropStatus);
            dropTitleRow.Add(dropTitle);
            dropTitleRow.Add(dropCount);
            Label dropDetail = new Label { name = "DropDetail" };
            dropDetail.style.fontSize = 9f;
            dropDetail.style.color = ESEditorPresentation.SectionMutedTextColor;
            dropDetail.style.marginTop = 2f;
            dropLabels.Add(dropTitleRow);
            dropLabels.Add(dropDetail);
            dropFeedback.Add(dropLabels);
            surface.Add(dropFeedback);
            viewportHost.Add(surface);
            panel.Add(viewportHost);
            viewportFooter = CreateHorizontalBar("ESWorkbenchViewportFooter", 25f);
            viewportFooter.AddToClassList("es-workbench-viewport-footer");
            viewportFooter.style.height = 44f;
            viewportFooter.style.flexWrap = Wrap.Wrap;
            viewportFooter.style.borderTopWidth = 1f;
            viewportFooter.style.borderTopColor = ESEditorPresentation.DividerColor;
            panel.Add(viewportFooter);
            return panel;
        }

        private VisualElement BuildBottomDrawer()
        {
            VisualElement drawer = bottomDrawer = new VisualElement { name = "ESWorkbenchBottomDrawer" };
            drawer.AddToClassList("es-workbench-bottom-drawer");
            drawer.style.flexGrow = 1f;
            drawer.style.minHeight = LayoutPolicy.CollapsedBottomDrawerHeight;
            drawer.style.backgroundColor = ESEditorPresentation.WindowInsetSurfaceColor;
            bottomTabs = CreateHorizontalBar("ESWorkbenchBottomTabs", 29f);
            drawer.Add(bottomTabs);
            BuildBottomTabs();
            bottomContent = new ScrollView(ScrollViewMode.Vertical) { name = "ESWorkbenchBottomContent" };
            ((ScrollView)bottomContent).horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            bottomContent.style.flexGrow = 1f;
            bottomContent.style.minHeight = 0f;
            bottomContent.style.display = layout.bottomDrawerExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            drawer.Add(bottomContent);
            return drawer;
        }

        private void CreateStandardBottomPanelDescriptors()
        {
            standardBottomPanels.Add(new ESWorkbenchBottomPanelDescriptor(
                "problems", "问题",
                _ => CreateIssuePanel(
                    issue => issue.Channel != ESWorkbenchIssueChannel.Build
                        && issue.Channel != ESWorkbenchIssueChannel.Performance,
                    "当前没有已知问题"),
                "资产、作者数据与系统问题", 500));
            standardBottomPanels.Add(new ESWorkbenchBottomPanelDescriptor(
                "history", "状态与历史",
                _ => CreateActivityPanel(
                    ESWorkbenchActivityChannel.History,
                    "暂无操作记录"),
                "按项目持久化的状态、日志与操作记录", 300));
            standardBottomPanels.Add(new ESWorkbenchBottomPanelDescriptor(
                "logs", "日志",
                _ => CreateActivityPanel(
                    ESWorkbenchActivityChannel.Log,
                    "暂无工作台日志"),
                "独立记录警告、错误与作者工具诊断", 350));
            standardBottomPanels.Add(new ESWorkbenchBottomPanelDescriptor(
                "tasks", "生产任务",
                _ => CreateActivityPanel(
                    ESWorkbenchActivityChannel.Task,
                    "暂无持久任务"),
                "按项目持久化的构建与处理任务", 400));
            standardBottomPanels.Add(new ESWorkbenchBottomPanelDescriptor(
                "diagnostics", "诊断与验收",
                _ => CreateVisualValidationPanel(),
                "性能预算、安全限制、窗口环境和商业视觉矩阵", 200));
        }

        private VisualElement BuildInspectorPanel()
        {
            VisualElement panel = inspectorPanel = new VisualElement { name = "ESWorkbenchInspectorPanel" };
            panel.AddToClassList("es-workbench-inspector-panel");
            panel.style.flexGrow = 1f;
            panel.style.minWidth = LayoutPolicy.MinimumInspectorPaneWidth;
            panel.style.minHeight = 0f;
            panel.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            panel.style.borderLeftWidth = 0f;
            panel.style.borderLeftColor = ESEditorPresentation.DividerColor;
            panel.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float width = evt.newRect.width;
                if (width >= LayoutPolicy.MinimumInspectorPaneWidth - 1f
                    && width <= LayoutPolicy.MaximumInspectorPaneWidth + 1f)
                    layout.inspectorPaneWidth = width;
            });
            VisualElement titleBar = CreateHorizontalBar("ESWorkbenchInspectorTitleBar", 31f);
            titleBar.AddToClassList("es-workbench-panel-header");
            inspectorTitle = new Label(presentation.InspectorTitle);
            inspectorTitle.AddToClassList("es-brand-title");
            inspectorTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            inspectorTitle.style.overflow = Overflow.Hidden;
            inspectorTitle.style.textOverflow = TextOverflow.Ellipsis;
            titleBar.Add(inspectorTitle);
            panel.Add(titleBar);
            inspectorContent = new ScrollView(ScrollViewMode.Vertical) { name = "ESWorkbenchInspectorContent" };
            inspectorContent.style.flexGrow = 1f;
            ((ScrollView)inspectorContent).horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            panel.Add(inspectorContent);
            return panel;
        }

        private void BuildCommandBar()
        {
            commandBar.Clear();
            ESWorkbenchResponsiveTier headerTier = LayoutPolicy.ResolveTier(availableWidth);
            bool stacked = headerTier != ESWorkbenchResponsiveTier.Wide;
            commandBar.style.flexDirection = stacked ? FlexDirection.Column : FlexDirection.Row;
            commandBar.style.alignItems = stacked ? Align.Stretch : Align.Center;
            commandBar.style.height = StyleKeyword.Auto;
            commandBar.style.minHeight = stacked ? 70f : 44f;
            commandBar.style.maxHeight = stacked ? 76f : 44f;

            VisualElement identityRow = commandBar;
            VisualElement actionRow = commandBar;
            if (stacked)
            {
                identityRow = CreateCommandBarRow("ESWorkbenchCommandIdentityRow");
                identityRow.style.height = 34f;
                actionRow = CreateCommandBarRow("ESWorkbenchCommandActionRow");
                actionRow.style.height = 34f;
                commandBar.Add(identityRow);
                commandBar.Add(actionRow);
            }
            VisualElement brandMarker = new VisualElement { name = "ESWorkbenchBrandMarker" };
            brandMarker.style.width = 3f;
            brandMarker.style.height = 18f;
            brandMarker.style.marginRight = 8f;
            brandMarker.style.flexShrink = 0f;
            brandMarker.style.backgroundColor = ESEditorPresentation.MapPoiColor;
            identityRow.Add(brandMarker);
            Label brand = new Label(presentation.BrandTitle) { name = "ESWorkbenchBrandTitle" };
            brand.AddToClassList("es-brand-title");
            brand.tooltip = presentation.BrandTitle;
            brand.style.unityFontStyleAndWeight = FontStyle.Bold;
            brand.style.marginRight = 10f;
            float brandWidth = ResolveBrandWidth(presentation.BrandTitle, headerTier);
            brand.style.width = brandWidth;
            brand.style.minWidth = brandWidth;
            brand.style.maxWidth = brandWidth;
            brand.style.flexShrink = 0f;
            brand.style.overflow = Overflow.Hidden;
            brand.style.textOverflow = TextOverflow.Ellipsis;
            identityRow.Add(brand);
            leftPaneButton = CreateActionButton(
                EditorGUIUtility.IconContent("d_Project").image,
                headerTier == ESWorkbenchResponsiveTier.Narrow ? "内容" : string.Empty,
                "显示或隐藏对象库与作者层级",
                ToggleLeftPane);
            leftPaneButton.name = "ESWorkbenchToggleLeftPane";
            leftPaneButton.style.width = headerTier == ESWorkbenchResponsiveTier.Narrow ? 64f : 28f;
            actionRow.Add(leftPaneButton);
            inspectorPaneButton = CreateActionButton(
                EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow").image,
                headerTier == ESWorkbenchResponsiveTier.Narrow ? "检查" : string.Empty,
                "显示或隐藏上下文 Inspector",
                ToggleInspectorPane);
            inspectorPaneButton.name = "ESWorkbenchToggleInspectorPane";
            inspectorPaneButton.style.width = headerTier == ESWorkbenchResponsiveTier.Narrow ? 64f : 28f;
            actionRow.Add(inspectorPaneButton);
            bottomPaneButton = CreateActionButton(
                EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image,
                headerTier == ESWorkbenchResponsiveTier.Narrow ? "任务" : string.Empty,
                "展开或收起问题、历史、构建与性能抽屉",
                ToggleBottomDrawer);
            bottomPaneButton.name = "ESWorkbenchToggleBottomDrawer";
            bottomPaneButton.style.width = headerTier == ESWorkbenchResponsiveTier.Narrow ? 64f : 28f;
            actionRow.Add(bottomPaneButton);
            actionRow.Add(CreateLayoutMenu());
            var assetField = new ObjectField(presentation.AssetFieldLabel)
            {
                name = "ESWorkbenchAssetField",
                objectType = assetType,
                allowSceneObjects = false,
                value = getAsset?.Invoke()
            };
            assetField.style.minWidth = headerTier == ESWorkbenchResponsiveTier.Wide ? 145f
                : headerTier == ESWorkbenchResponsiveTier.Compact ? 108f : 96f;
            assetField.style.maxWidth = headerTier == ESWorkbenchResponsiveTier.Wide ? 280f
                : headerTier == ESWorkbenchResponsiveTier.Compact ? 144f : 118f;
            assetField.style.flexGrow = headerTier == ESWorkbenchResponsiveTier.Wide ? 1f : 0f;
            assetField.style.flexShrink = 1f;
            assetField.style.marginRight = 8f;
            if (headerTier != ESWorkbenchResponsiveTier.Wide)
            {
                assetField.label = string.Empty;
                assetField.labelElement.style.display = DisplayStyle.None;
                assetField.tooltip = presentation.AssetFieldLabel;
            }
            assetField.RegisterValueChangedCallback(evt => bindAsset?.Invoke(evt.newValue));
            identityRow.Add(assetField);
            documentStatusLabel = new Label { name = "ESWorkbenchDocumentStatus" };
            documentStatusLabel.style.paddingLeft = 7f;
            documentStatusLabel.style.paddingRight = 7f;
            documentStatusLabel.style.marginRight = 5f;
            documentStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            documentStatusLabel.style.flexShrink = 0f;
            documentStatusLabel.style.maxWidth = headerTier == ESWorkbenchResponsiveTier.Wide ? 90f
                : 28f;
            documentStatusLabel.style.overflow = Overflow.Hidden;
            documentStatusLabel.style.textOverflow = TextOverflow.Ellipsis;
            identityRow.Add(documentStatusLabel);
            UpdateDocumentStatus();
            IReadOnlyList<ESWorkbenchCommandDescriptor> commands = getCommands?.Invoke()
                ?? Array.Empty<ESWorkbenchCommandDescriptor>();
            ESWorkbenchCommandDescriptor[] toolbarCommands = commands
                .Where(value => value != null && value.ShowInToolbar)
                .OrderByDescending(value => value.Visibility == ESWorkbenchCommandVisibility.Pinned)
                .ThenBy(value => value.Role)
                .ThenByDescending(value => value.Priority)
                .ThenBy(value => value.CommandId, StringComparer.Ordinal)
                .ToArray();
            ESWorkbenchResponsiveTier tier = LayoutPolicy.ResolveTier(availableWidth);
            int visibleCount = ResolveVisibleToolbarCommandCount(toolbarCommands, tier);
            ESWorkbenchCommandRole? previousRole = null;
            for (int i = 0; i < visibleCount; i++)
            {
                ESWorkbenchCommandDescriptor command = toolbarCommands[i];
                if (previousRole.HasValue && previousRole.Value != command.Role)
                    actionRow.Add(CreateToolbarDivider());
                previousRole = command.Role;
                Texture icon = ResolveCommandIcon(command);
                bool responsiveIconOnly = tier != ESWorkbenchResponsiveTier.Wide && icon != null;
                string text = (command.IconOnly || responsiveIconOnly) && icon != null
                    ? string.Empty : command.DisplayName;
                Button button = CreateActionButton(icon, text, command.Tooltip, () => ExecuteCommand(command));
                button.name = "ESWorkbenchCommand_" + command.CommandId.Replace('.', '_');
                if (command.IconOnly || responsiveIconOnly) button.style.width = 28f;
                else
                {
                    button.style.maxWidth = tier == ESWorkbenchResponsiveTier.Wide ? 104f : 76f;
                    button.style.overflow = Overflow.Hidden;
                }
                ApplyCommandPresentation(button, command.Role);
                button.SetEnabled(command.CanExecute == null || command.CanExecute(actions));
                actionRow.Add(button);
            }
            if (visibleCount < toolbarCommands.Length)
            {
                ToolbarMenu overflow = ESWindowPresentation.CreateHeaderOverflowMenu(
                    "ESWorkbenchCommandOverflow",
                    tier == ESWorkbenchResponsiveTier.Narrow ? "命令" : "更多",
                    "显示当前宽度下收纳的工作台命令");
                ESWorkbenchCommandRole? overflowRole = null;
                for (int i = visibleCount; i < toolbarCommands.Length; i++)
                {
                    ESWorkbenchCommandDescriptor command = toolbarCommands[i];
                    if (overflowRole.HasValue && overflowRole.Value != command.Role)
                        overflow.menu.AppendSeparator();
                    overflowRole = command.Role;
                    overflow.menu.AppendAction(
                        command.DisplayName,
                        _ =>
                        {
                            RecordVisualInteraction(
                                "command-overflow",
                                command.CommandId,
                                "ui-event/command-overflow");
                            ExecuteCommand(command);
                        },
                        _ => command.CanExecute == null || command.CanExecute(actions)
                            ? DropdownMenuAction.Status.Normal
                            : DropdownMenuAction.Status.Disabled);
                }
                actionRow.Add(overflow);
            }
            AttachFoundationActionStack(actionRow);
            UpdatePaneButtons();
        }

        private static VisualElement CreateCommandBarRow(string name)
        {
            var row = new VisualElement { name = name };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0f;
            row.style.minWidth = 0f;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;
            return row;
        }

        private static VisualElement ResolveFoundationActionStack(ESWindowActionHosts hosts)
        {
            VisualElement stack = hosts?.System?.parent?.parent;
            if (stack == null)
                return null;
            bool ownsGlobal = hosts.Global == null || stack.Contains(hosts.Global);
            bool ownsWindow = hosts.Window == null || stack.Contains(hosts.Window);
            return ownsGlobal && ownsWindow ? stack : null;
        }

        private void AttachFoundationActionStack(VisualElement target)
        {
            if (target == null || foundationActionStack == null)
                return;
            foundationActionStack.RemoveFromHierarchy();
            foundationActionStack.style.flexGrow = 0f;
            foundationActionStack.style.flexShrink = 1f;
            foundationActionStack.style.minWidth = 0f;
            foundationActionStack.style.marginLeft = 4f;
            foundationActionStack.style.maxHeight = 28f;
            foundationActionStack.style.overflow = Overflow.Hidden;
            target.Add(foundationActionStack);

            VisualElement originalHeader = owner?.rootVisualElement?.Q<VisualElement>("ESWindowHeader");
            if (originalHeader != null)
            {
                originalHeader.style.display = DisplayStyle.None;
                originalHeader.style.minHeight = 0f;
                originalHeader.style.maxHeight = 0f;
            }
        }

        private static float ResolveBrandWidth(
            string title,
            ESWorkbenchResponsiveTier tier)
        {
            string value = string.IsNullOrWhiteSpace(title) ? "ES 内容工作台" : title.Trim();
            float estimated = 18f;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                estimated += char.IsWhiteSpace(character) ? 5f
                    : character <= 0x7f ? 8f : 15f;
            }
            float maximum = tier == ESWorkbenchResponsiveTier.Wide ? 224f
                : tier == ESWorkbenchResponsiveTier.Compact ? 150f : 128f;
            float minimum = tier == ESWorkbenchResponsiveTier.Compact ? 112f : 128f;
            return Mathf.Clamp(Mathf.Ceil(estimated), minimum, maximum);
        }

        private ToolbarMenu CreateLayoutMenu()
        {
            ToolbarMenu menu = ESWindowPresentation.CreateHeaderOverflowMenu(
                "ESWorkbenchLayoutMenu",
                "布局",
                "切换或恢复工作台面板布局",
                46f,
                64f);
            AppendLayoutPresetAction(menu, "标准创作", ESWorkbenchLayoutPreset.Authoring);
            AppendLayoutPresetAction(menu, "专注视口", ESWorkbenchLayoutPreset.Focus);
            AppendLayoutPresetAction(menu, "内容整理", ESWorkbenchLayoutPreset.Content);
            AppendLayoutPresetAction(menu, "生产任务", ESWorkbenchLayoutPreset.Production);
            AppendLayoutPresetAction(menu, "诊断", ESWorkbenchLayoutPreset.Diagnostics);
            menu.menu.AppendSeparator();
            menu.menu.AppendAction(
                "恢复标准布局",
                _ => ApplyLayoutPreset(
                    layout.layoutPreset == ESWorkbenchLayoutPreset.Custom
                        ? ESWorkbenchLayoutPreset.Authoring
                        : layout.layoutPreset));
            return menu;
        }

        private void AppendLayoutPresetAction(
            ToolbarMenu menu,
            string displayName,
            ESWorkbenchLayoutPreset preset)
        {
            menu.menu.AppendAction(
                displayName,
                _ => ApplyLayoutPreset(preset),
                _ => layout.layoutPreset == preset
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
        }

        private static void ApplyCommandPresentation(Button button, ESWorkbenchCommandRole role)
        {
            if (button == null) return;
            ESEditorPresentation.ESPresentationState state;
            switch (role)
            {
                case ESWorkbenchCommandRole.Primary:
                case ESWorkbenchCommandRole.Validation:
                case ESWorkbenchCommandRole.Build:
                    state = ESEditorPresentation.ESPresentationState.Selected;
                    break;
                case ESWorkbenchCommandRole.Dangerous:
                    state = ESEditorPresentation.ESPresentationState.Warning;
                    break;
                default:
                    state = ESEditorPresentation.ESPresentationState.Normal;
                    break;
            }
            ESWindowPresentation.SetButtonPresentationState(button, state);
        }

        private Texture ResolveCommandIcon(ESWorkbenchCommandDescriptor command)
        {
            if (command?.Icon != null) return command.Icon;
            string iconName = command?.UnityIconName;
            if (string.IsNullOrEmpty(iconName)) return null;
            if (unityIconCache.TryGetValue(iconName, out Texture cached) && cached != null) return cached;
            Texture icon = EditorGUIUtility.IconContent(iconName)?.image;
            if (icon != null) unityIconCache[iconName] = icon;
            return icon;
        }

        private void UpdateDocumentStatus()
        {
            if (documentStatusLabel == null) return;
            bool dirty = isDirty != null && isDirty();
            bool compact = LayoutPolicy.ResolveTier(availableWidth) != ESWorkbenchResponsiveTier.Wide;
            string fullStatus = dirty ? "未保存" : "已保存";
            documentStatusLabel.text = compact ? (dirty ? "●" : "✓") : dirty ? "● 未保存" : fullStatus;
            documentStatusLabel.tooltip = fullStatus;
            documentStatusLabel.style.color = dirty
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
        }

        private int ResolveVisibleToolbarCommandCount(
            IReadOnlyList<ESWorkbenchCommandDescriptor> commands,
            ESWorkbenchResponsiveTier tier)
        {
            if (commands == null || commands.Count == 0) return 0;
            int policyLimit = Mathf.Min(LayoutPolicy.ResolveVisibleCommandCount(availableWidth), commands.Count);
            bool reserveOverflow = commands.Count > policyLimit;
            int visible = FitToolbarCommands(commands, policyLimit, tier, reserveOverflow);
            if (visible < commands.Count && !reserveOverflow)
                visible = FitToolbarCommands(commands, policyLimit, tier, true);
            return Mathf.Clamp(visible, 1, commands.Count);
        }

        private int FitToolbarCommands(
            IReadOnlyList<ESWorkbenchCommandDescriptor> commands,
            int maximum,
            ESWorkbenchResponsiveTier tier,
            bool reserveOverflow)
        {
            float fixedWidth = 14f + 11f
                + ResolveBrandWidth(presentation.BrandTitle, tier) + 10f
                + 3f * 31f + 64f
                + (tier == ESWorkbenchResponsiveTier.Wide ? 288f
                    : tier == ESWorkbenchResponsiveTier.Compact ? 152f : 126f)
                + (tier == ESWorkbenchResponsiveTier.Wide ? 98f
                    : 36f);
            float commandBudget = Mathf.Max(0f, availableWidth - fixedWidth
                - (reserveOverflow ? 64f : 0f));
            float used = 0f;
            int count = 0;
            ESWorkbenchCommandRole? previousRole = null;
            for (int i = 0; i < commands.Count && i < maximum; i++)
            {
                ESWorkbenchCommandDescriptor command = commands[i];
                if (command == null) continue;
                float next = EstimateToolbarCommandWidth(command, tier);
                if (previousRole.HasValue && previousRole.Value != command.Role) next += 11f;
                if (count > 0 && used + next > commandBudget) break;
                used += next;
                count++;
                previousRole = command.Role;
            }
            return count;
        }

        private static float EstimateToolbarCommandWidth(
            ESWorkbenchCommandDescriptor command,
            ESWorkbenchResponsiveTier tier)
        {
            bool iconOnly = command.IconOnly || (tier != ESWorkbenchResponsiveTier.Wide && command.HasIcon);
            if (iconOnly) return 31f;
            int textLength = string.IsNullOrEmpty(command.DisplayName) ? 2 : command.DisplayName.Length;
            float maximum = tier == ESWorkbenchResponsiveTier.Wide ? 107f : 79f;
            return Mathf.Clamp(28f + textLength * 13f, 48f, maximum);
        }

        private void BuildToolBar()
        {
            if (toolRail == null) return;
            toolRail.Clear();
            IReadOnlyList<ESWorkbenchToolDescriptor> source = getTools?.Invoke();
            if (source == null) return;
            ESWorkbenchAuthoringModeDefinition mode = GetActiveAuthoringMode();
            HashSet<string> allowedToolIds = mode == null
                ? null
                : new HashSet<string>(mode.ToolIds, StringComparer.Ordinal);
            foreach (ESWorkbenchToolDescriptor tool in source
                .Where(value => value != null && (value.IsAvailable == null || value.IsAvailable(actions)))
                .Where(value => allowedToolIds == null || allowedToolIds.Contains(value.ToolId))
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.ToolId, StringComparer.Ordinal))
            {
                Button button = CreateToolButton(tool, true);
                button.style.width = 44f;
                button.style.height = 38f;
                button.style.marginBottom = 4f;
                button.style.marginRight = 0f;
                if (tool.Icon == null)
                    button.text = tool.DisplayName.Length > 3 ? tool.DisplayName.Substring(0, 2) : tool.DisplayName;
                toolRail.Add(button);
            }
            UpdateViewportFooter();
        }

        private void BuildDocumentTabs()
        {
            if (documentTabs == null) return;
            documentTabs.Clear();
            if (availableCenterWidth >= 520f)
            {
                Label workspaceTitle = new Label(presentation.WorkspaceTitle)
                {
                    name = "ESWorkbenchWorkspaceTitle",
                    tooltip = presentation.WorkspaceTitle
                };
                workspaceTitle.style.minWidth = 72f;
                workspaceTitle.style.maxWidth = 150f;
                workspaceTitle.style.marginRight = 8f;
                workspaceTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                workspaceTitle.style.color = ESEditorPresentation.SectionMutedTextColor;
                workspaceTitle.style.overflow = Overflow.Hidden;
                workspaceTitle.style.textOverflow = TextOverflow.Ellipsis;
                workspaceTitle.style.flexShrink = 1f;
                documentTabs.Add(workspaceTitle);
                documentTabs.Add(CreateToolbarDivider());
            }
            var items = new List<DocumentTabItem>();
            IReadOnlyList<ESWorkbenchDocumentDefinition> documents = getDocuments?.Invoke();
            if (documents != null)
                for (int i = 0; i < documents.Count; i++)
                {
                    ESWorkbenchDocumentDefinition document = documents[i];
                    if (document == null || (document.isAvailable != null && !document.isAvailable())) continue;
                    items.Add(new DocumentTabItem
                    {
                        id = document.documentId,
                        label = document.title,
                        tooltip = document.tooltip
                    });
                }
            bool activeExists = documents != null && documents.Any(value => value != null
                    && activeDocument == value.documentId
                    && (value.isAvailable == null || value.isAvailable()));
            if (!activeExists)
            {
                activeDocument = items.FirstOrDefault()?.id ?? string.Empty;
                layout.activeDocument = activeDocument;
            }

            int capacity = Mathf.Min(LayoutPolicy.ResolveVisibleDocumentCount(availableCenterWidth), items.Count);
            var visibleIds = new HashSet<string>(StringComparer.Ordinal) { activeDocument };
            if (items.Count > 0) visibleIds.Add(items[0].id);
            for (int i = 0; i < items.Count && visibleIds.Count < capacity; i++)
                visibleIds.Add(items[i].id);
            for (int i = 0; i < items.Count; i++)
                if (visibleIds.Contains(items[i].id))
                    AddDocumentTab(items[i].id, items[i].label, items[i].tooltip);

            DocumentTabItem[] hidden = items.Where(value => !visibleIds.Contains(value.id)).ToArray();
            if (hidden.Length == 0) return;
            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            documentTabs.Add(spacer);
            ToolbarMenu overflow = ESWindowPresentation.CreateHeaderOverflowMenu(
                "ESWorkbenchDocumentOverflow",
                 availableCenterWidth < 520f ? "文档" : "更多文档",
                 "显示当前宽度下收纳的工作台文档");
            for (int i = 0; i < hidden.Length; i++)
            {
                DocumentTabItem item = hidden[i];
                overflow.menu.AppendAction(
                    item.label,
                    _ => ShowDocument(item.id),
                    _ => activeDocument == item.id
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
            documentTabs.Add(overflow);
        }

        private void BuildViewportModes()
        {
            viewportModeBar.Clear();
            viewportToggles.Clear();
            if (BlocksAuthoringViewport)
            {
                DeactivateCurrentViewport();
                ReleaseUnavailableViewports(Array.Empty<ESWorkbenchViewportDescriptor>());
                return;
            }
            ESWorkbenchAuthoringModeDefinition[] modes = ResolveAvailableAuthoringModes();
            EnsureActiveAuthoringMode(modes);
            int modeCapacity = availableCenterWidth >= 820f ? 6 : availableCenterWidth >= 620f ? 4 : 2;
            var visibleModeIds = new HashSet<string>(StringComparer.Ordinal) { activeAuthoringModeId };
            foreach (ESWorkbenchAuthoringModeDefinition mode in modes.Where(value => value.Primary))
            {
                if (visibleModeIds.Count >= modeCapacity) break;
                visibleModeIds.Add(mode.ModeId);
            }
            foreach (ESWorkbenchAuthoringModeDefinition mode in modes)
            {
                if (visibleModeIds.Count >= modeCapacity) break;
                visibleModeIds.Add(mode.ModeId);
            }
            foreach (ESWorkbenchAuthoringModeDefinition mode in modes.Where(value => visibleModeIds.Contains(value.ModeId)))
                AddAuthoringModeToggle(mode);
            ESWorkbenchAuthoringModeDefinition[] hiddenModes = modes
                .Where(value => !visibleModeIds.Contains(value.ModeId))
                .ToArray();
            if (hiddenModes.Length > 0)
            {
                ToolbarMenu more = ESWindowPresentation.CreateHeaderOverflowMenu(
                    "ESWorkbenchAuthoringModeOverflow", "更多", "选择当前宽度下收纳的作者模式");
                foreach (ESWorkbenchAuthoringModeDefinition mode in hiddenModes)
                {
                    ESWorkbenchAuthoringModeDefinition captured = mode;
                    more.menu.AppendAction(
                        captured.Title,
                        _ => ActivateAuthoringMode(captured.ModeId),
                        _ => captured.ModeId == activeAuthoringModeId
                            ? DropdownMenuAction.Status.Checked
                            : DropdownMenuAction.Status.Normal);
                }
                viewportModeBar.Add(more);
            }
            viewportModeBar.Add(CreateToolbarDivider());
            IReadOnlyList<ESWorkbenchViewportDescriptor> descriptors = getViewports?.Invoke();
            ESWorkbenchViewportDescriptor[] available = (descriptors ?? Array.Empty<ESWorkbenchViewportDescriptor>())
                .Where(value => value != null && (value.IsAvailable == null || value.IsAvailable(actions)))
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.ViewportId, StringComparer.Ordinal)
                .ToArray();
            ReleaseUnavailableViewports(available);
            foreach (ESWorkbenchViewportDescriptor descriptor in available)
            {
                var toggle = new ToolbarToggle
                {
                    name = "ESWorkbenchViewport_" + SanitizeElementName(descriptor.ViewportId),
                    text = descriptor.DisplayName,
                    tooltip = descriptor.Tooltip
                };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                    {
                        ActivateViewport(descriptor);
                        RecordVisualInteraction(
                            "viewport-switch",
                            descriptor.ViewportId,
                            "ui-event/viewport-toggle");
                    }
                    else if (activeViewportId == descriptor.ViewportId) toggle.SetValueWithoutNotify(true);
                });
                viewportToggles.Add(descriptor.ViewportId, toggle);
                viewportModeBar.Add(toggle);
            }
            if (activeViewport == null || string.IsNullOrEmpty(activeViewportId) || !viewportToggles.ContainsKey(activeViewportId))
            {
                ESWorkbenchViewportDescriptor selected = available.FirstOrDefault(
                    value => value.ViewportId == activeViewportId) ?? available.FirstOrDefault();
                if (selected != null) ActivateViewport(selected);
            }
            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            viewportModeBar.Add(spacer);
            AddSnapMenu();
            Texture frameIcon = EditorGUIUtility.IconContent("ViewToolZoom On").image;
            Button frameAll = CreateActionButton(
                frameIcon,
                frameIcon == null ? "适配" : string.Empty,
                "适配全部内容",
                FrameActiveViewport);
            frameAll.name = "ESWorkbenchFrameAll";
            frameAll.SetEnabled(activeViewport is IESWorkbenchFrameableViewport);
            viewportModeBar.Add(frameAll);
        }

        private ESWorkbenchAuthoringModeDefinition[] ResolveAvailableAuthoringModes()
        {
            return (getAuthoringModes?.Invoke() ?? Array.Empty<ESWorkbenchAuthoringModeDefinition>())
                .Where(value => value != null && (value.IsAvailable == null || value.IsAvailable(actions)))
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.ModeId, StringComparer.Ordinal)
                .ToArray();
        }

        private void EnsureActiveAuthoringMode(IReadOnlyList<ESWorkbenchAuthoringModeDefinition> modes)
        {
            if (modes == null || modes.Count == 0)
            {
                activeAuthoringModeId = string.Empty;
                layout.activeAuthoringModeId = string.Empty;
                return;
            }
            if (modes.Any(value => value.ModeId == activeAuthoringModeId)) return;
            activeAuthoringModeId = modes[0].ModeId;
            layout.activeAuthoringModeId = activeAuthoringModeId;
        }

        private void AddAuthoringModeToggle(ESWorkbenchAuthoringModeDefinition mode)
        {
            var toggle = new ToolbarToggle
            {
                name = "ESWorkbenchAuthoringMode_" + SanitizeElementName(mode.ModeId),
                text = mode.Title,
                tooltip = mode.Tooltip,
                value = mode.ModeId == activeAuthoringModeId
            };
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) ActivateAuthoringMode(mode.ModeId);
                else if (mode.ModeId == activeAuthoringModeId) toggle.SetValueWithoutNotify(true);
            });
            toggle.userData = "mode:" + mode.ModeId;
            viewportModeBar.Add(toggle);
        }

        private void ActivateAuthoringMode(string modeId)
        {
            ESWorkbenchAuthoringModeDefinition[] modes = ResolveAvailableAuthoringModes();
            ESWorkbenchAuthoringModeDefinition next = modes.FirstOrDefault(value => value.ModeId == modeId);
            if (next == null) return;
            ESWorkbenchAuthoringModeDefinition previous = modes.FirstOrDefault(
                value => value.ModeId == activeAuthoringModeId);
            if (!string.Equals(activeAuthoringModeId, next.ModeId, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(activatedAuthoringModeId))
                {
                    try { previous?.Deactivate?.Invoke(actions); }
                    catch (Exception exception) { Debug.LogException(exception); }
                    activatedAuthoringModeId = string.Empty;
                }
                activeAuthoringModeId = next.ModeId;
                layout.activeAuthoringModeId = next.ModeId;
                if (next.ContentKinds.Count > 0)
                {
                    contentKindFilter = next.ContentKinds[0].ToString();
                    layout.activeContentKind = contentKindFilter;
                }
                ESWorkbenchToolDescriptor defaultTool = getTools?.Invoke()?.FirstOrDefault(
                    value => value != null && value.ToolId == next.DefaultToolId);
                if (defaultTool != null) ExecuteTool(defaultTool);
            }
            ActivateCurrentAuthoringModeIfNeeded(next);
            BuildViewportModes();
            BuildToolBar();
            RebuildObjectList();
            RebuildInspector(actions.Selection.Current);
            UpdateViewportFooter();
        }

        internal void SelectAuthoringMode(string modeId)
        {
            ActivateAuthoringMode(modeId);
        }

        internal string ActiveAuthoringModeIdForTest => activeAuthoringModeId;
        internal string ActiveDocumentIdForTest => activeDocument;
        internal IESWorkbenchViewport ActiveViewportForTest => activeViewport;

        private void ActivateCurrentAuthoringModeIfNeeded(ESWorkbenchAuthoringModeDefinition mode = null)
        {
            mode ??= GetActiveAuthoringMode();
            if (mode == null || string.Equals(activatedAuthoringModeId, mode.ModeId, StringComparison.Ordinal)) return;
            try
            {
                mode.Activate?.Invoke(actions);
                activatedAuthoringModeId = mode.ModeId;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("作者模式启动失败：" + mode.Title + " · " + exception.Message, MessageType.Error);
            }
        }

        private void AddSnapMenu()
        {
            if (string.IsNullOrEmpty(activeViewportId)) return;
            ESWorkbenchViewportLayoutState state = layout.GetOrCreateViewportState(activeViewportId);
            var menu = new ToolbarMenu
            {
                text = state.snapEnabled ? "吸附：已启用" : "吸附：已关闭",
                tooltip = "控制当前视口的移动、旋转和缩放吸附"
            };
            menu.menu.AppendAction("启用吸附", _ =>
            {
                state.snapEnabled = !state.snapEnabled;
                menu.text = state.snapEnabled ? "吸附：已启用" : "吸附：已关闭";
                activeViewport?.Refresh(ESWorkbenchRefreshReason.Explicit);
            }, _ => state.snapEnabled ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            float[] steps = { 0.25f, 0.5f, 1f, 2f, 5f };
            for (int i = 0; i < steps.Length; i++)
            {
                float step = steps[i];
                menu.menu.AppendAction("移动步长/" + step.ToString("0.##"), _ =>
                {
                    state.moveSnap = step;
                    state.snapEnabled = true;
                    menu.text = "吸附：已启用";
                    activeViewport?.Refresh(ESWorkbenchRefreshReason.Explicit);
                }, _ => Mathf.Approximately(state.moveSnap, step)
                    ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            viewportModeBar.Add(menu);
        }

        private void ReleaseUnavailableViewports(IReadOnlyList<ESWorkbenchViewportDescriptor> available)
        {
            var ids = new HashSet<string>(available.Select(value => value.ViewportId), StringComparer.Ordinal);
            string[] removed = liveViewports.Keys.Where(id => !ids.Contains(id)).ToArray();
            for (int i = 0; i < removed.Length; i++)
            {
                string id = removed[i];
                IESWorkbenchViewport viewport = liveViewports[id];
                if (ReferenceEquals(viewport, activeViewport))
                {
                    DeactivateCurrentViewport();
                    activeViewport = null;
                    activeViewportId = string.Empty;
                    layout.activeViewportId = string.Empty;
                }
                try { viewport?.Dispose(); }
                catch (Exception exception) { Debug.LogException(exception); }
                liveViewports.Remove(id);
            }
        }

        private void AddLeftTab(VisualElement parent, string id, string label)
        {
            var toggle = new ToolbarToggle { text = label, value = activeLeftTab == id };
            toggle.style.flexGrow = 1f;
            toggle.style.minWidth = 0f;
            toggle.style.height = 30f;
            toggle.style.unityFontStyleAndWeight = FontStyle.Bold;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) ShowLeftTab(id);
                else if (activeLeftTab == id) toggle.SetValueWithoutNotify(true);
            });
            toggle.userData = id;
            parent.Add(toggle);
        }

        private void ShowLeftTab(string id)
        {
            if (id != "hierarchy") id = "objects";
            activeLeftTab = id;
            layout.activeLeftTab = id;
            VisualElement tabBar = root?.Q<VisualElement>("ESWorkbenchLeftTabs");
            tabBar?.Query<ToolbarToggle>().ForEach(toggle => toggle.SetValueWithoutNotify((string)toggle.userData == id));
            if (leftContent == null) return;
            leftContent.Clear();
            if (id == "hierarchy") BuildHierarchyPanel();
            else BuildObjectsPanel();
        }

        private void BuildObjectsPanel()
        {
            VisualElement header = contentLibraryHeader = new VisualElement { name = "ESWorkbenchContentLibraryHeader" };
            header.AddToClassList("es-workbench-panel-header");
            header.style.paddingLeft = 9f;
            header.style.paddingRight = 9f;
            header.style.paddingTop = 7f;
            header.style.paddingBottom = 5f;
            header.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            Label title = contentLibraryTitle = new Label("可用内容") { name = "ESWorkbenchContentLibraryTitle" };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 13f;
            Label description = contentLibraryDescription = new Label("按类型发现内容，点击查看属性，拖入作者视口使用。")
            {
                name = "ESWorkbenchContentLibraryDescription"
            };
            description.style.fontSize = 9f;
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.color = ESEditorPresentation.SectionMutedTextColor;
            header.Add(title);
            header.Add(description);
            contentSummaryLabel = new Label { name = "ESWorkbenchContentSummary" };
            contentSummaryLabel.style.fontSize = 9f;
            contentSummaryLabel.style.marginTop = 4f;
            contentSummaryLabel.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            header.Add(contentSummaryLabel);
            leftContent.Add(header);

            VisualElement filter = objectFilterBar = CreateHorizontalBar("ESWorkbenchObjectFilter", 36f);
            filter.style.paddingLeft = 7f;
            filter.style.paddingRight = 7f;
            objectSearch = new ToolbarSearchField();
            objectSearch.style.flexGrow = 1f;
            objectSearch.style.minWidth = 0f;
            objectSearch.RegisterValueChangedCallback(_ => RebuildObjectList(refreshSource: false));
            filter.Add(objectSearch);
            sortMenu = new ToolbarMenu { text = ResolveContentSortName(contentSortMode) };
            sortMenu.name = "ESWorkbenchContentSortMenu";
            sortMenu.tooltip = "内容排序方式";
            sortMenu.style.width = 72f;
            BuildContentSortMenu();
            filter.Add(sortMenu);
            leftContent.Add(filter);

            VisualElement kindQuickBar = contentKindQuickBar =
                CreateHorizontalBar("ESWorkbenchContentKindQuickBar", 32f);
            kindQuickBar.style.paddingLeft = 7f;
            kindQuickBar.style.paddingRight = 7f;
            kindQuickBar.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            kindQuickBar.RegisterCallback<GeometryChangedEvent>(evt =>
                ApplyContentKindShortcutResponsive(evt.newRect.width));
            leftContent.Add(kindQuickBar);

            VisualElement scopeBar = contentScopeBar = CreateHorizontalBar("ESWorkbenchContentScopeBar", 31f);
            scopeBar.style.paddingLeft = 7f;
            scopeBar.style.paddingRight = 7f;
            AddContentScopeButton(scopeBar, ESWorkbenchContentScope.All, "全部");
            AddContentScopeButton(scopeBar, ESWorkbenchContentScope.Favorites, "收藏");
            AddContentScopeButton(scopeBar, ESWorkbenchContentScope.Recent, "最近");
            AddContentScopeButton(scopeBar, ESWorkbenchContentScope.Recommended, "推荐");
            leftContent.Add(scopeBar);

            VisualElement breadcrumbBar = contentBreadcrumbBar = CreateHorizontalBar("ESWorkbenchContentBreadcrumbBar", 29f);
            breadcrumbBar.style.paddingLeft = 7f;
            breadcrumbBar.style.paddingRight = 7f;
            categoryBreadcrumb = new ToolbarMenu { text = "全部内容" };
            categoryBreadcrumb.tooltip = "当前业务分类路径；菜单可快速返回任一上级";
            categoryBreadcrumb.style.flexGrow = 1f;
            categoryBreadcrumb.style.minWidth = 0f;
            breadcrumbBar.Add(categoryBreadcrumb);
            compactContentFilterMenu = new ToolbarMenu
            {
                name = "ESWorkbenchCompactContentFilter",
                text = "筛选"
            };
            compactContentFilterMenu.tooltip = "窄栏模式下切换内容类型和业务分类";
            compactContentFilterMenu.style.width = 54f;
            compactContentFilterMenu.style.flexShrink = 0f;
            compactContentFilterMenu.style.display = DisplayStyle.None;
            breadcrumbBar.Add(compactContentFilterMenu);
            leftContent.Add(breadcrumbBar);

            VisualElement browser = contentBrowser = new VisualElement { name = "ESWorkbenchContentBrowser" };
            browser.AddToClassList("es-workbench-content-browser");
            browser.style.flexDirection = FlexDirection.Row;
            browser.style.flexGrow = 1f;
            browser.style.minHeight = 0f;
            browser.RegisterCallback<GeometryChangedEvent>(evt =>
                ApplyContentBrowserResponsive(evt.newRect.width));

            VisualElement kindRail = contentKindRail = new VisualElement { name = "ESWorkbenchContentKindRail" };
            kindRail.style.width = 116f;
            kindRail.style.minWidth = 102f;
            kindRail.style.maxWidth = 136f;
            kindRail.style.flexShrink = 0f;
            kindRail.style.borderRightWidth = 1f;
            kindRail.style.borderRightColor = ESEditorPresentation.DividerColor;
            kindRail.style.backgroundColor = ESEditorPresentation.WindowInsetSurfaceColor;
            Label categoryTitle = new Label("业务分类");
            categoryTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            categoryTitle.style.fontSize = 10f;
            categoryTitle.style.paddingLeft = 8f;
            categoryTitle.style.paddingTop = 7f;
            categoryTitle.style.paddingBottom = 5f;
            kindRail.Add(categoryTitle);
            contentCategoryList = new ListView
            {
                name = "ESWorkbenchContentCategoryTree",
                itemsSource = contentCategoryNodes,
                fixedItemHeight = 30f,
                selectionType = SelectionType.Multiple,
                makeItem = CreateContentCategoryRow,
                bindItem = BindContentCategoryRow
            };
            contentCategoryList.style.flexGrow = 1f;
            contentCategoryList.selectionChanged += selection =>
            {
                ContentCategoryNode selected = selection.OfType<ContentCategoryNode>().FirstOrDefault();
                if (selected != null) SetCategory(selected.path);
            };
            kindRail.Add(contentCategoryList);
            browser.Add(kindRail);

            VisualElement results = contentResults = new VisualElement { name = "ESWorkbenchContentResults" };
            results.AddToClassList("es-workbench-content-results");
            results.style.flexGrow = 1f;
            results.style.minWidth = 0f;
            results.style.minHeight = 0f;
            results.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                int nextColumns = evt.newRect.width >= 318f ? 2 : 1;
                if (contentGridColumns != nextColumns)
                {
                    contentGridColumns = nextColumns;
                    RebuildContentGridRows();
                    objectGridList?.Rebuild();
                }
                ApplyContentResultsResponsive(evt.newRect.width);
            });

            contentModeBar = CreateHorizontalBar("ESWorkbenchContentModeBar", 31f);
            contentModeBar.style.paddingLeft = 5f;
            contentModeBar.style.paddingRight = 5f;
            listModeButton = ESWindowPresentation.CreateToolbarButton("列表", "使用高密度内容列表", () => SetContentViewMode(ESWorkbenchContentViewMode.List));
            gridModeButton = ESWindowPresentation.CreateToolbarButton("大图", "使用虚拟化大缩略图网格", () => SetContentViewMode(ESWorkbenchContentViewMode.Grid));
            listModeButton.name = "ESWorkbenchContentListMode";
            gridModeButton.name = "ESWorkbenchContentGridMode";
            listModeButton.style.width = 43f;
            gridModeButton.style.width = 43f;
            contentModeBar.Add(listModeButton);
            contentModeBar.Add(gridModeButton);
            contentViewMenu = new ToolbarMenu
            {
                name = "ESWorkbenchContentViewMenu",
                text = "视图"
            };
            contentViewMenu.tooltip = "切换列表或大缩略图网格";
            contentViewMenu.style.width = 50f;
            contentViewMenu.style.display = DisplayStyle.None;
            contentModeBar.Add(contentViewMenu);
            VisualElement modeSpacer = new VisualElement();
            modeSpacer.style.flexGrow = 1f;
            contentModeBar.Add(modeSpacer);
            batchMenu = new ToolbarMenu
            {
                name = "ESWorkbenchContentBatchMenu",
                text = "批选"
            };
            batchMenu.tooltip = "批量选择、放置和间距设置";
            batchMenu.style.width = 48f;
            contentModeBar.Add(batchMenu);
            batchPlaceButton = ESWindowPresentation.CreateToolbarButton("批量 0", "把勾选内容作为一个 Undo 事务放置到当前视口中心；也可拖动任一已勾选卡片", PlaceBatchAtViewportCenter);
            batchPlaceButton.name = "ESWorkbenchContentBatchPlace";
            batchPlaceButton.style.minWidth = 62f;
            contentModeBar.Add(batchPlaceButton);
            results.Add(contentModeBar);
            objectList = new ListView
            {
                name = "ESWorkbenchContentList",
                itemsSource = visibleObjects,
                fixedItemHeight = 84f,
                selectionType = SelectionType.Single,
                makeItem = CreateObjectRow,
                bindItem = BindObjectRow
            };
            objectList.style.flexGrow = 1f;
            objectList.selectionChanged += selection =>
            {
                actions.Selection.SelectMany(selection
                    .OfType<ESWorkbenchObjectDescriptor>()
                    .Select(value => GetEffectiveDescriptor(value).ToSelection()));
            };
            results.Add(objectList);
            objectGridList = new ListView
            {
                name = "ESWorkbenchContentGrid",
                itemsSource = visibleGridRows,
                fixedItemHeight = compactContentVertical ? 160f : 184f,
                selectionType = SelectionType.None,
                makeItem = CreateContentGridRow,
                bindItem = BindContentGridRow
            };
            objectGridList.style.flexGrow = 1f;
            results.Add(objectGridList);
            objectEmptyLabel = CreateListEmptyLabel("没有匹配内容", "切换分类、清除搜索，或注册新的内容贡献。");
            results.Add(objectEmptyLabel);
            browser.Add(results);
            leftContent.Add(browser);
            BuildContentViewMenu();
            BuildContentBatchMenu();
            BuildContentKindQuickBar();
            ApplyContentViewMode();
            ApplyContentVerticalResponsive(availableHeight);
        }

        private void ApplyContentKindShortcutResponsive(float width)
        {
            if (width <= 1f) return;
            int capacity = width < 260f ? 2
                : width < 360f ? 3
                : width < 440f ? 4 : 5;
            if (contentKindShortcutCapacity == capacity) return;
            contentKindShortcutCapacity = capacity;
            BuildContentKindQuickBar();
        }

        private void BuildContentKindQuickBar()
        {
            if (contentKindQuickBar == null) return;
            contentKindQuickBar.Clear();

            Label title = new Label("按类型")
            {
                name = "ESWorkbenchContentKindQuickTitle",
                tooltip = "内容库默认按类型组织；点击即可只查看该类型。"
            };
            title.style.flexShrink = 0f;
            title.style.marginRight = 5f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = ESEditorPresentation.SectionMutedTextColor;
            contentKindQuickBar.Add(title);

            if (contentKindTabs.Count == 0) return;
            ContentKindTabItem all = contentKindTabs.FirstOrDefault(value => value?.id == "all");
            var concrete = contentKindTabs
                .Where(value => value != null && value.id != "all")
                .ToList();
            var visible = new List<ContentKindTabItem>();
            if (all != null) visible.Add(all);
            int concreteCapacity = Mathf.Max(1, contentKindShortcutCapacity - visible.Count);
            visible.AddRange(concrete.Take(concreteCapacity));

            ContentKindTabItem active = concrete.FirstOrDefault(value =>
                string.Equals(value.id, contentKindFilter, StringComparison.Ordinal));
            if (active != null && !visible.Contains(active))
            {
                if (visible.Count > (all == null ? 0 : 1)) visible[visible.Count - 1] = active;
                else visible.Add(active);
            }

            foreach (ContentKindTabItem item in visible)
                contentKindQuickBar.Add(CreateContentKindShortcut(item));

            ContentKindTabItem[] hidden = contentKindTabs
                .Where(value => value != null && !visible.Contains(value))
                .ToArray();
            if (hidden.Length == 0) return;
            var more = new ToolbarMenu
            {
                name = "ESWorkbenchContentKindMore",
                text = "更多"
            };
            more.tooltip = "查看其余内容类型";
            more.style.minWidth = 46f;
            more.style.flexShrink = 0f;
            foreach (ContentKindTabItem item in hidden)
            {
                ContentKindTabItem captured = item;
                more.menu.AppendAction(
                    captured.label + "（" + captured.count + "）",
                    _ => SetContentKind(captured.id),
                    _ => string.Equals(contentKindFilter, captured.id, StringComparison.Ordinal)
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
            contentKindQuickBar.Add(more);
        }

        private Button CreateContentKindShortcut(ContentKindTabItem item)
        {
            bool active = string.Equals(contentKindFilter, item.id, StringComparison.Ordinal);
            var button = new Button(() => SetContentKind(item.id))
            {
                name = "ESWorkbenchContentKindShortcut_" + SanitizeElementName(item.id),
                text = item.label + " " + item.count,
                tooltip = item.label + "，共 " + item.count + " 项内容"
            };
            button.style.height = 24f;
            button.style.minWidth = 44f;
            button.style.flexShrink = 0f;
            button.style.marginRight = 3f;
            button.style.paddingLeft = 5f;
            button.style.paddingRight = 5f;
            button.style.backgroundColor = active
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                : ESEditorPresentation.ControlSurfaceColor;
            button.style.color = active ? Color.white : ESEditorPresentation.SectionTextColor;
            return button;
        }

        private void AddContentScopeButton(
            VisualElement parent,
            ESWorkbenchContentScope scope,
            string label)
        {
            Button button = ESWindowPresentation.CreateToolbarButton(
                label,
                "组合筛选 · " + label,
                () => SetContentScope(scope));
            button.name = "ESWorkbenchContentScope_" + scope;
            button.userData = scope;
            button.style.flexGrow = 1f;
            button.style.minWidth = 0f;
            parent.Add(button);
        }

        private void SetContentScope(ESWorkbenchContentScope scope)
        {
            if (contentScope == scope) return;
            contentScope = scope;
            layout.contentScope = scope;
            RebuildObjectList(refreshSource: false);
        }

        private void UpdateContentScopeButtons()
        {
            root?.Query<Button>().ForEach(button =>
            {
                if (!(button.userData is ESWorkbenchContentScope scope)) return;
                bool active = scope == contentScope;
                button.style.backgroundColor = active
                    ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                    : ESEditorPresentation.ControlSurfaceColor;
                button.style.color = active ? Color.white : ESEditorPresentation.SectionTextColor;
            });
        }

        private void BuildContentSortMenu()
        {
            if (sortMenu == null) return;
            sortMenu.menu.MenuItems().Clear();
            AppendContentSortAction(ESWorkbenchContentSortMode.Type, "按类型");
            AppendContentSortAction(ESWorkbenchContentSortMode.Recommended, "智能推荐");
            AppendContentSortAction(ESWorkbenchContentSortMode.Priority, "贡献优先级");
            AppendContentSortAction(ESWorkbenchContentSortMode.Name, "名称");
            AppendContentSortAction(ESWorkbenchContentSortMode.Recent, "最近使用");
            AppendContentSortAction(ESWorkbenchContentSortMode.MostUsed, "使用频率");
        }

        private void AppendContentSortAction(ESWorkbenchContentSortMode mode, string label)
        {
            sortMenu.menu.AppendAction(
                label,
                _ => SetContentSortMode(mode),
                _ => contentSortMode == mode
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
        }

        private void SetContentSortMode(ESWorkbenchContentSortMode mode)
        {
            contentSortMode = mode;
            layout.contentSortMode = mode;
            if (sortMenu != null) sortMenu.text = ResolveContentSortName(mode);
            RebuildObjectList(refreshSource: false);
        }

        private static string ResolveContentSortName(ESWorkbenchContentSortMode mode)
        {
            switch (mode)
            {
                case ESWorkbenchContentSortMode.Type: return "按类型";
                case ESWorkbenchContentSortMode.Priority: return "优先级";
                case ESWorkbenchContentSortMode.Name: return "名称";
                case ESWorkbenchContentSortMode.Recent: return "最近";
                case ESWorkbenchContentSortMode.MostUsed: return "常用";
                default: return "推荐";
            }
        }

        private static string ResolveContentScopeName(ESWorkbenchContentScope scope)
        {
            switch (scope)
            {
                case ESWorkbenchContentScope.Favorites: return "收藏";
                case ESWorkbenchContentScope.Recent: return "最近";
                case ESWorkbenchContentScope.Recommended: return "推荐";
                default: return "全部";
            }
        }

        private void ApplyContentBrowserResponsive(float width)
        {
            bool compact = width > 1f
                && (width < 330f
                    || (contentViewMode == ESWorkbenchContentViewMode.Grid && width < 520f));
            if (compactContentBrowser == compact
                && ReferenceEquals(contentBrowserResponsiveRail, contentKindRail)
                && ReferenceEquals(contentBrowserResponsiveMenu, compactContentFilterMenu))
                return;
            compactContentBrowser = compact;
            contentBrowserResponsiveRail = contentKindRail;
            contentBrowserResponsiveMenu = compactContentFilterMenu;
            if (contentKindRail != null)
                contentKindRail.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            if (compactContentFilterMenu != null)
                compactContentFilterMenu.style.display = compact || compactContentVertical
                    ? DisplayStyle.Flex : DisplayStyle.None;
            BuildCompactContentFilterMenu();
        }

        private void ApplyContentVerticalResponsive(float height)
        {
            bool compact = height > 1f
                && (compactContentVertical ? height < 790f : height < 760f);
            bool changed = compactContentVertical != compact
                || !ReferenceEquals(contentVerticalResponsiveHeader, contentLibraryHeader);
            compactContentVertical = compact;
            contentVerticalResponsiveHeader = contentLibraryHeader;
            if (leftPanelTitle != null)
                leftPanelTitle.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            if (leftTabs != null)
            {
                leftTabs.style.height = compact ? 31f : 38f;
                leftTabs.Query<ToolbarToggle>().ForEach(toggle =>
                    toggle.style.height = compact ? 26f : 30f);
            }
            if (contentLibraryHeader != null)
            {
                contentLibraryHeader.style.flexDirection = compact ? FlexDirection.Row : FlexDirection.Column;
                contentLibraryHeader.style.alignItems = compact ? Align.Center : Align.Stretch;
                contentLibraryHeader.style.paddingTop = compact ? 4f : 7f;
                contentLibraryHeader.style.paddingBottom = compact ? 3f : 5f;
            }
            if (contentLibraryTitle != null)
            {
                contentLibraryTitle.style.fontSize = compact ? 11f : 13f;
                contentLibraryTitle.style.flexShrink = 0f;
                contentLibraryTitle.style.marginRight = compact ? 8f : 0f;
            }
            if (contentLibraryDescription != null)
                contentLibraryDescription.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            if (contentSummaryLabel != null)
            {
                contentSummaryLabel.style.marginTop = compact ? 0f : 4f;
                contentSummaryLabel.style.flexGrow = compact ? 1f : 0f;
                contentSummaryLabel.style.minWidth = 0f;
                contentSummaryLabel.style.whiteSpace = compact ? WhiteSpace.NoWrap : WhiteSpace.Normal;
                contentSummaryLabel.style.overflow = compact ? Overflow.Hidden : Overflow.Visible;
                contentSummaryLabel.style.textOverflow = compact ? TextOverflow.Ellipsis : TextOverflow.Clip;
            }
            if (objectFilterBar != null) objectFilterBar.style.height = compact ? 30f : 36f;
            if (contentKindQuickBar != null) contentKindQuickBar.style.height = compact ? 29f : 32f;
            if (contentScopeBar != null)
                contentScopeBar.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            if (contentBreadcrumbBar != null) contentBreadcrumbBar.style.height = compact ? 26f : 29f;
            if (contentModeBar != null) contentModeBar.style.height = compact ? 28f : 31f;
            if (objectGridList != null)
            {
                objectGridList.fixedItemHeight = compact ? 160f : 184f;
                if (changed) objectGridList.Rebuild();
            }
            if (compactContentFilterMenu != null)
                compactContentFilterMenu.style.display = compact || compactContentBrowser
                    ? DisplayStyle.Flex : DisplayStyle.None;
            BuildCompactContentFilterMenu();
        }

        private void ApplyContentResultsResponsive(float width)
        {
            bool compactViewSwitch = width > 1f && width < 205f;
            if (listModeButton != null)
                listModeButton.style.display = compactViewSwitch ? DisplayStyle.None : DisplayStyle.Flex;
            if (gridModeButton != null)
                gridModeButton.style.display = compactViewSwitch ? DisplayStyle.None : DisplayStyle.Flex;
            if (contentViewMenu != null)
                contentViewMenu.style.display = compactViewSwitch ? DisplayStyle.Flex : DisplayStyle.None;
            if (batchPlaceButton != null)
                batchPlaceButton.style.display = width > 1f && width < 188f
                    ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void BuildContentViewMenu()
        {
            if (contentViewMenu == null) return;
            contentViewMenu.menu.MenuItems().Clear();
            contentViewMenu.menu.AppendAction(
                "高密度列表",
                _ => SetContentViewMode(ESWorkbenchContentViewMode.List),
                _ => contentViewMode == ESWorkbenchContentViewMode.List
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            contentViewMenu.menu.AppendAction(
                "大缩略图网格",
                _ => SetContentViewMode(ESWorkbenchContentViewMode.Grid),
                _ => contentViewMode == ESWorkbenchContentViewMode.Grid
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            contentViewMenu.text = contentViewMode == ESWorkbenchContentViewMode.Grid ? "大图" : "列表";
        }

        private void BuildCompactContentFilterMenu()
        {
            if (compactContentFilterMenu == null) return;
            compactContentFilterMenu.menu.MenuItems().Clear();
            compactContentFilterMenu.menu.AppendAction(
                "范围/全部",
                _ => SetContentScope(ESWorkbenchContentScope.All),
                _ => contentScope == ESWorkbenchContentScope.All
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            compactContentFilterMenu.menu.AppendAction(
                "范围/收藏",
                _ => SetContentScope(ESWorkbenchContentScope.Favorites),
                _ => contentScope == ESWorkbenchContentScope.Favorites
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            compactContentFilterMenu.menu.AppendAction(
                "范围/最近",
                _ => SetContentScope(ESWorkbenchContentScope.Recent),
                _ => contentScope == ESWorkbenchContentScope.Recent
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            compactContentFilterMenu.menu.AppendAction(
                "范围/推荐",
                _ => SetContentScope(ESWorkbenchContentScope.Recommended),
                _ => contentScope == ESWorkbenchContentScope.Recommended
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            compactContentFilterMenu.menu.AppendSeparator();
            compactContentFilterMenu.menu.AppendAction(
                "类型/全部内容",
                _ => SetContentKind("all"),
                _ => string.Equals(contentKindFilter, "all", StringComparison.Ordinal)
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            foreach (ContentKindTabItem tab in contentKindTabs.Where(value => value != null && value.id != "all"))
            {
                ContentKindTabItem captured = tab;
                compactContentFilterMenu.menu.AppendAction(
                    "类型/" + captured.label + "（" + captured.count + "）",
                    _ => SetContentKind(captured.id),
                    _ => string.Equals(contentKindFilter, captured.id, StringComparison.Ordinal)
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
            compactContentFilterMenu.menu.AppendSeparator();
            compactContentFilterMenu.menu.AppendAction(
                "分类/全部内容",
                _ => SetCategory("全部"),
                _ => string.Equals(categoryFilter, "全部", StringComparison.Ordinal)
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            IReadOnlyList<ESWorkbenchObjectDescriptor> source = contentSourceSnapshot;
            string[] categories = source.Count == 0
                ? Array.Empty<string>()
                : source.Where(value => value != null && MatchesContentKind(value, contentKindFilter))
                    .Select(value => string.IsNullOrWhiteSpace(value.Category) ? "其他" : value.Category.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
            foreach (string category in categories)
            {
                string captured = category;
                compactContentFilterMenu.menu.AppendAction(
                    "分类/" + captured,
                    _ => SetCategory(captured),
                    _ => string.Equals(categoryFilter, captured, StringComparison.Ordinal)
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
            int activeFilters = (contentKindFilter == "all" ? 0 : 1)
                + (categoryFilter == "全部" ? 0 : 1)
                + (contentScope == ESWorkbenchContentScope.All ? 0 : 1);
            compactContentFilterMenu.text = activeFilters == 0 ? "筛选" : "筛选 " + activeFilters;
            compactContentFilterMenu.tooltip = activeFilters == 0
                ? "紧凑模式下切换内容范围、类型和业务分类"
                : "当前范围：" + ResolveContentScopeName(contentScope)
                    + "；当前类型：" + ResolveContentKindFilterName()
                    + "；当前分类：" + categoryFilter;
        }

        private string ResolveContentKindFilterName()
        {
            return contentKindTabs.FirstOrDefault(value => value != null
                && string.Equals(value.id, contentKindFilter, StringComparison.Ordinal))?.label ?? "全部";
        }

        private VisualElement CreateContentCategoryRow()
        {
            VisualElement row = new VisualElement { name = "ESWorkbenchContentCategoryRow" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginLeft = 3f;
            row.style.marginRight = 3f;
            Button fold = new Button { name = "Fold", text = "" };
            fold.style.width = 20f;
            fold.style.height = 22f;
            fold.style.paddingLeft = 0f;
            fold.style.paddingRight = 0f;
            fold.clicked += () =>
            {
                if (!(row.userData is ContentCategoryNode node) || !node.hasChildren) return;
                if (!expandedContentCategoryPaths.Add(node.path)) expandedContentCategoryPaths.Remove(node.path);
                PersistContentCategoryExpansion();
                BuildContentCategoryTree();
                contentCategoryList?.Rebuild();
            };
            Label label = new Label { name = "Label" };
            label.style.flexGrow = 1f;
            label.style.minWidth = 0f;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            Label count = new Label { name = "Count" };
            count.style.fontSize = 9f;
            count.style.color = ESEditorPresentation.SectionMutedTextColor;
            row.Add(fold);
            row.Add(label);
            row.Add(count);
            return row;
        }

        private void BindContentCategoryRow(VisualElement element, int index)
        {
            ContentCategoryNode node = contentCategoryNodes[index];
            element.userData = node;
            element.style.paddingLeft = 2f + node.depth * 9f;
            Button fold = element.Q<Button>("Fold");
            fold.text = node.hasChildren
                ? (expandedContentCategoryPaths.Contains(node.path) ? "▾" : "▸")
                : string.Empty;
            fold.SetEnabled(node.hasChildren);
            element.Q<Label>("Label").text = node.label;
            element.Q<Label>("Count").text = node.count.ToString();
            bool active = string.Equals(categoryFilter, node.path, StringComparison.Ordinal);
            element.style.backgroundColor = active
                ? ESEditorPresentation.WindowRaisedSurfaceColor
                : Color.clear;
            element.tooltip = (node.path == "全部" ? "全部内容" : node.path) + " · " + node.count + " 项";
        }

        private void BuildContentCategoryTree(IReadOnlyList<ESWorkbenchObjectDescriptor> source = null)
        {
            source ??= contentSourceSnapshot;
            IEnumerable<ESWorkbenchObjectDescriptor> filtered = source == null
                ? Enumerable.Empty<ESWorkbenchObjectDescriptor>()
                : source.Where(value => value != null && MatchesContentKind(value, contentKindFilter));
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var parents = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ESWorkbenchObjectDescriptor item in filtered)
            {
                string[] segments = (string.IsNullOrWhiteSpace(item.Category) ? "其他" : item.Category)
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string path = string.Empty;
                for (int i = 0; i < segments.Length; i++)
                {
                    string parent = path;
                    path = string.IsNullOrEmpty(path) ? segments[i].Trim() : path + "/" + segments[i].Trim();
                    counts.TryGetValue(path, out int count);
                    counts[path] = count + 1;
                    parents[path] = parent;
                }
            }

            if (categoryFilter != "全部"
                && !counts.Keys.Any(path => string.Equals(path, categoryFilter, StringComparison.Ordinal)
                    || path.StartsWith(categoryFilter + "/", StringComparison.Ordinal)))
            {
                categoryFilter = "全部";
                layout.activeContentCategory = "全部";
            }

            contentCategoryNodes.Clear();
            contentCategoryNodes.Add(new ContentCategoryNode
            {
                path = "全部",
                label = "全部内容",
                count = filtered.Count(),
                depth = 0,
                hasChildren = false
            });
            string[] roots = parents.Where(pair => string.IsNullOrEmpty(pair.Value))
                .Select(pair => pair.Key)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (expandedContentCategoryPaths.Count == 0)
                foreach (string rootPath in roots) expandedContentCategoryPaths.Add(rootPath);
            foreach (string rootPath in roots) AddContentCategoryNodeRecursive(rootPath, 0, counts, parents);
            contentCategoryList?.Rebuild();
            int selectedIndex = contentCategoryNodes.FindIndex(value =>
                string.Equals(value.path, categoryFilter, StringComparison.Ordinal));
            if (selectedIndex >= 0) contentCategoryList?.SetSelectionWithoutNotify(new[] { selectedIndex });
            UpdateContentBreadcrumb();
        }

        private void AddContentCategoryNodeRecursive(
            string path,
            int depth,
            IReadOnlyDictionary<string, int> counts,
            IReadOnlyDictionary<string, string> parents)
        {
            string[] children = parents.Where(pair => string.Equals(pair.Value, path, StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string label = path.Substring(path.LastIndexOf('/') + 1);
            contentCategoryNodes.Add(new ContentCategoryNode
            {
                path = path,
                label = label,
                depth = depth,
                count = counts[path],
                hasChildren = children.Length > 0
            });
            if (!expandedContentCategoryPaths.Contains(path)) return;
            foreach (string child in children)
                AddContentCategoryNodeRecursive(child, depth + 1, counts, parents);
        }

        private void PersistContentCategoryExpansion()
        {
            layout.expandedContentCategoryPaths = expandedContentCategoryPaths
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private void UpdateContentBreadcrumb()
        {
            if (categoryBreadcrumb == null) return;
            categoryBreadcrumb.menu.MenuItems().Clear();
            categoryBreadcrumb.menu.AppendAction("全部内容", _ => SetCategory("全部"));
            if (categoryFilter == "全部")
            {
                categoryBreadcrumb.text = "全部内容";
                return;
            }
            string[] segments = categoryFilter.Split('/');
            string path = string.Empty;
            for (int i = 0; i < segments.Length; i++)
            {
                path = string.IsNullOrEmpty(path) ? segments[i] : path + "/" + segments[i];
                string captured = path;
                categoryBreadcrumb.menu.AppendAction(captured, _ => SetCategory(captured));
            }
            categoryBreadcrumb.text = "全部内容 > " + categoryFilter.Replace("/", " > ");
            categoryBreadcrumb.tooltip = categoryBreadcrumb.text;
        }

        private void BuildHierarchyPanel()
        {
            VisualElement filter = CreateHorizontalBar("ESWorkbenchHierarchyFilter", 32f);
            hierarchySearch = new ToolbarSearchField();
            hierarchySearch.style.flexGrow = 1f;
            hierarchySearch.RegisterValueChangedCallback(_ => RebuildHierarchyList());
            filter.Add(hierarchySearch);
            Button expand = ESWindowPresentation.CreateToolbarButton("展开", "展开全部层级", ExpandAllHierarchy);
            Button collapse = ESWindowPresentation.CreateToolbarButton("折叠", "折叠到根节点", CollapseHierarchy);
            filter.Add(expand);
            filter.Add(collapse);
            leftContent.Add(filter);
            hierarchyList = new ListView
            {
                itemsSource = visibleHierarchy,
                fixedItemHeight = 27f,
                selectionType = SelectionType.Multiple,
                makeItem = CreateHierarchyRow,
                bindItem = BindHierarchyRow
            };
            hierarchyList.style.flexGrow = 1f;
            hierarchyList.selectionChanged += selection =>
            {
                actions.Selection.SelectMany(selection
                    .OfType<ESWorkbenchHierarchyDescriptor>()
                    .Select(value => value.ToSelection()));
            };
            leftContent.Add(hierarchyList);
            hierarchyEmptyLabel = CreateListEmptyLabel("没有作者对象", "绑定资产或注册动态层级源后将在这里显示稳定层级。");
            leftContent.Add(hierarchyEmptyLabel);
            RebuildHierarchyList();
        }

        private static Label CreateListEmptyLabel(string title, string detail)
        {
            var label = new Label(title + "\n" + detail);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = ESEditorPresentation.SectionMutedTextColor;
            label.style.paddingLeft = 14f;
            label.style.paddingRight = 14f;
            label.style.paddingTop = 20f;
            label.style.display = DisplayStyle.None;
            return label;
        }

        private Button CreateToolButton(ESWorkbenchToolDescriptor tool, bool compact)
        {
            string shortcut = tool.Shortcut.HasValue ? FormatShortcut(tool.Shortcut.Value) : string.Empty;
            string tooltip = string.IsNullOrWhiteSpace(shortcut)
                ? tool.Tooltip
                : tool.Tooltip + " (" + shortcut + ")";
            string text = compact && tool.Icon != null ? string.Empty : tool.DisplayName;
            Button button = CreateActionButton(tool.Icon, text, tooltip,
                () => ExecuteTool(tool));
            if (compact)
            {
                button.style.width = tool.Icon != null ? 31f : 76f;
                button.style.height = 25f;
            }
            if (actions.Tools.IsActive(tool.ToolId))
            {
                ESWindowPresentation.SetButtonPresentationState(
                    button,
                    ESEditorPresentation.ESPresentationState.Selected);
                button.style.borderLeftWidth = 3f;
                button.style.borderLeftColor = ESEditorPresentation.SelectionColor;
                button.tooltip = "当前工具 · " + tooltip;
            }
            else
            {
                button.style.borderLeftWidth = 3f;
                button.style.borderLeftColor = Color.clear;
            }
            button.name = "ESWorkbenchTool_" + tool.ToolId.Replace('.', '_');
            return button;
        }

        private static string FormatShortcut(ESWorkbenchShortcut shortcut)
        {
            string prefix = string.Empty;
            if ((shortcut.modifiers & (EventModifiers.Control | EventModifiers.Command)) != 0) prefix += "Ctrl+";
            if ((shortcut.modifiers & EventModifiers.Shift) != 0) prefix += "Shift+";
            if ((shortcut.modifiers & EventModifiers.Alt) != 0) prefix += "Alt+";
            return prefix + shortcut.key;
        }

        private VisualElement CreateObjectRow()
        {
            VisualElement row = new VisualElement { name = "ESWorkbenchObjectRow" };
            row.style.position = Position.Relative;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 6f;
            row.style.paddingRight = 5f;
            row.style.marginLeft = 5f;
            row.style.marginRight = 5f;
            row.style.marginTop = 3f;
            row.style.marginBottom = 3f;
            row.style.backgroundColor = ESEditorPresentation.ControlSurfaceColor;
            row.style.borderLeftWidth = 2f;
            row.style.borderLeftColor = ESEditorPresentation.DividerColor;
            row.style.borderTopWidth = 1f;
            row.style.borderRightWidth = 1f;
            row.style.borderBottomWidth = 1f;
            row.style.borderTopColor = ESEditorPresentation.DividerColor;
            row.style.borderRightColor = ESEditorPresentation.DividerColor;
            row.style.borderBottomColor = ESEditorPresentation.DividerColor;
            VisualElement selectionBar = CreateContentSelectionBar();
            row.Add(selectionBar);
            VisualElement previewWell = CreateThumbnailWell("ESWorkbenchListThumbnail", 58f, 58f);
            Image icon = new Image { name = "Icon", scaleMode = ScaleMode.ScaleToFit };
            icon.style.flexGrow = 1f;
            icon.style.minWidth = 0f;
            icon.style.minHeight = 0f;
            previewWell.Add(icon);
            Label placeholder = new Label { name = "PreviewPlaceholder" };
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = 4f;
            placeholder.style.right = 4f;
            placeholder.style.top = 4f;
            placeholder.style.bottom = 4f;
            placeholder.style.unityTextAlign = TextAnchor.MiddleCenter;
            placeholder.style.fontSize = 9f;
            placeholder.style.color = ESEditorPresentation.SectionMutedTextColor;
            previewWell.Add(placeholder);
            previewWell.Add(CreateContentUsageChip());
            previewWell.Add(CreateThumbnailKindChip());
            previewWell.style.marginRight = 7f;
            row.Add(previewWell);
            VisualElement labels = new VisualElement();
            labels.style.flexGrow = 1f;
            labels.style.minWidth = 0f;
            Label title = new Label { name = "Title" };
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            Label subtitle = new Label { name = "Subtitle" };
            subtitle.style.fontSize = 9f;
            subtitle.style.color = ESEditorPresentation.SectionMutedTextColor;
            subtitle.style.overflow = Overflow.Hidden;
            subtitle.style.textOverflow = TextOverflow.Ellipsis;
            Label category = new Label { name = "Category" };
            category.style.fontSize = 9f;
            category.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            labels.Add(title);
            labels.Add(subtitle);
            labels.Add(category);
            row.Add(labels);
            VisualElement actionsColumn = new VisualElement();
            actionsColumn.style.alignItems = Align.FlexEnd;
            actionsColumn.style.flexShrink = 0f;
            VisualElement quickActions = new VisualElement();
            quickActions.style.flexDirection = FlexDirection.Row;
            quickActions.style.alignItems = Align.Center;
            Toggle batch = new Toggle { name = "Batch" };
            batch.tooltip = "加入批量放置选择";
            batch.style.width = 18f;
            batch.style.marginRight = 3f;
            Button favorite = new Button { name = "Favorite", text = "☆" };
            favorite.tooltip = "收藏或取消收藏";
            favorite.style.width = 24f;
            favorite.style.height = 22f;
            quickActions.Add(batch);
            quickActions.Add(favorite);
            actionsColumn.Add(quickActions);
            ToolbarMenu preset = new ToolbarMenu { name = "Preset", text = "预设" };
            preset.style.maxWidth = 84f;
            preset.style.height = 22f;
            actionsColumn.Add(preset);
            Label badge = new Label { name = "Badge" };
            badge.style.fontSize = 9f;
            badge.style.paddingLeft = 5f;
            badge.style.paddingRight = 5f;
            badge.style.marginTop = 2f;
            badge.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            actionsColumn.Add(badge);
            row.Add(actionsColumn);
            row.userData = new ObjectRowState(viewportFeel.DragStartPixels);
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || IsContentInteractiveTarget(evt.target as VisualElement)) return;
                ObjectRowState state = (ObjectRowState)row.userData;
                if (!contentPointerGate.TryAcquire(row, evt.pointerId))
                {
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!pointerCoordinator.TryAcquire(
                        row,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Content))
                {
                    contentPointerGate.Release(row, evt.pointerId);
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!state.drag.Arm(evt.pointerId, evt.position, state.item))
                {
                    contentPointerGate.Release(row, evt.pointerId);
                    pointerCoordinator.Release(
                        row,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Content);
                    evt.StopImmediatePropagation();
                    return;
                }
                row.CapturePointer(evt.pointerId);
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            row.RegisterCallback<PointerMoveEvent>(evt =>
            {
                TryStartObjectDrag(row, evt);
                if (((ObjectRowState)row.userData)?.drag.IsActive == true)
                    evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            row.RegisterCallback<PointerUpEvent>(evt =>
            {
                ObjectRowState state = (ObjectRowState)row.userData;
                bool click = state.drag.ShouldClick(evt.pointerId, evt.position);
                ESWorkbenchObjectDescriptor item = state.item;
                ReleaseObjectDragPointer(row, state, evt.pointerId);
                contentPointerGate.Release(row, evt.pointerId);
                pointerCoordinator.Release(
                    row,
                    evt.pointerId,
                    ESWorkbenchPointerOwnerKind.Content);
                if (click && item != null)
                    actions.Selection.Select(GetEffectiveDescriptor(item).ToSelection());
                if (click) evt.StopImmediatePropagation();
            });
            row.RegisterCallback<PointerCancelEvent>(evt =>
            {
                ObjectRowState state = (ObjectRowState)row.userData;
                int pointerId = state?.drag?.PointerId ?? evt.pointerId;
                ReleaseObjectDragPointer(row, state, pointerId);
                contentPointerGate.Release(row, pointerId);
                pointerCoordinator.Release(
                    row,
                    pointerId,
                    ESWorkbenchPointerOwnerKind.Content);
                evt.StopImmediatePropagation();
            });
            row.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                ObjectRowState state = (ObjectRowState)row.userData;
                if (state.drag.IsActive)
                    state.drag.End(ESWorkbenchContentDragEndReason.CaptureLost);
                state.drag.Reset();
                contentPointerGate.Release(row, evt.pointerId);
                pointerCoordinator.Release(
                    row,
                    evt.pointerId,
                    ESWorkbenchPointerOwnerKind.Content);
            });
            row.RegisterCallback<PointerEnterEvent>(_ =>
            {
                ObjectRowState state = (ObjectRowState)row.userData;
                state.hovered = true;
                ApplyContentCardVisual(row, state.item, true);
            });
            row.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                ObjectRowState state = (ObjectRowState)row.userData;
                state.hovered = false;
                if (!state.drag.IsActive || !row.HasPointerCapture(state.drag.PointerId))
                {
                    int pointerId = state.drag.PointerId;
                    state.drag.Reset();
                    if (pointerId >= 0) contentPointerGate.Release(row, pointerId);
                    if (pointerId >= 0)
                        pointerCoordinator.Release(
                            row,
                            pointerId,
                            ESWorkbenchPointerOwnerKind.Content);
                }
                ApplyContentCardVisual(row, state.item, false);
            });
            batch.RegisterValueChangedCallback(evt =>
            {
                ObjectRowState state = (ObjectRowState)row.userData;
                SetBatchContentSelected(state.item, evt.newValue);
            });
            favorite.clicked += () => ToggleContentFavorite(((ObjectRowState)row.userData).item);
            return row;
        }

        private void BindObjectRow(VisualElement element, int index)
        {
            ESWorkbenchObjectDescriptor item = visibleObjects[index];
            ESWorkbenchObjectDescriptor effective = GetEffectiveDescriptor(item);
            element.Q<Label>("Title").text = effective.DisplayName;
            element.Q<Label>("Category").text = item.ContentKindDisplayName + " · " + item.Category;
            element.Q<Label>("Subtitle").text = string.IsNullOrWhiteSpace(effective.Subtitle)
                ? (item.Source == null ? "工作台模板" : item.Source.GetType().Name)
                : effective.Subtitle;
            Label badge = element.Q<Label>("Badge");
            badge.text = string.IsNullOrWhiteSpace(effective.Badge) ? item.DefaultDragHint : effective.Badge;
            Texture preview = ResolveContentThumbnail(effective);
            Image image = element.Q<Image>("Icon");
            image.image = preview;
            image.style.display = preview == null ? DisplayStyle.None : DisplayStyle.Flex;
            Label placeholder = element.Q<Label>("PreviewPlaceholder");
            placeholder.text = preview == null ? ResolveContentKindShortName(item.ContentKind) : string.Empty;
            placeholder.style.display = preview == null ? DisplayStyle.Flex : DisplayStyle.None;
            ApplyThumbnailWellVisual(
                element.Q<VisualElement>("ESWorkbenchListThumbnail"),
                element.Q<Label>("KindChip"),
                item);
            ApplyContentUsageChip(element.Q<Label>("UsageChip"), item);
            Toggle batch = element.Q<Toggle>("Batch");
            batch.SetValueWithoutNotify(batchContentIds.Contains(item.BaseObjectId));
            Button favorite = element.Q<Button>("Favorite");
            bool isFavorite = contentUsage.IsFavorite(item.BaseObjectId);
            favorite.text = isFavorite ? "★" : "☆";
            favorite.style.color = isFavorite
                ? new Color(1f, 0.76f, 0.24f, 1f)
                : ESEditorPresentation.SectionMutedTextColor;
            ConfigurePresetMenu(element.Q<ToolbarMenu>("Preset"), item);
            element.tooltip = string.IsNullOrWhiteSpace(effective.Tooltip) ? effective.ObjectId : effective.Tooltip;
            ObjectRowState state = (ObjectRowState)element.userData;
            ReleaseObjectDragPointer(element, state, state.drag.PointerId);
            state.pulseVersion++;
            state.item = item;
            ApplyContentCardVisual(element, item, state.hovered);
        }

        private VisualElement CreateContentGridRow()
        {
            VisualElement row = new VisualElement { name = "ESWorkbenchContentGridRow" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 4f;
            row.Add(CreateContentGridCard("First"));
            row.Add(CreateContentGridCard("Second"));
            return row;
        }

        private VisualElement CreateContentGridCard(string slotName)
        {
            VisualElement card = new VisualElement
            {
                name = "ESWorkbenchContentGridCard" + slotName,
                userData = new ContentCardState(viewportFeel.DragStartPixels)
            };
            card.AddToClassList("es-workbench-resource-card");
            card.style.flexGrow = 1f;
            card.style.flexBasis = 0f;
            card.style.minWidth = 0f;
            card.style.height = compactContentVertical ? 148f : 172f;
            card.style.marginLeft = 3f;
            card.style.marginRight = 3f;
            card.style.paddingLeft = 5f;
            card.style.paddingRight = 5f;
            card.style.paddingTop = 5f;
            card.style.paddingBottom = 5f;
            card.style.backgroundColor = ESEditorPresentation.ControlSurfaceColor;
            card.style.borderLeftWidth = 2f;
            card.style.borderRightWidth = 0f;
            card.style.borderTopWidth = 0f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftColor = ESEditorPresentation.DividerColor;
            card.style.borderRightColor = ESEditorPresentation.DividerColor;
            card.style.borderTopColor = ESEditorPresentation.DividerColor;
            card.style.borderBottomColor = ESEditorPresentation.DividerColor;
            card.Add(CreateContentSelectionBar());

            VisualElement previewWell = CreateThumbnailWell(
                "PreviewWell",
                0f,
                compactContentVertical ? 72f : 104f);
            previewWell.style.width = StyleKeyword.Auto;
            previewWell.style.flexGrow = compactContentVertical ? 0f : 1f;
            previewWell.style.flexShrink = compactContentVertical ? 0f : 1f;
            if (compactContentVertical) previewWell.style.height = 72f;
            Image image = new Image { name = "Icon", scaleMode = ScaleMode.ScaleToFit };
            image.style.flexGrow = 1f;
            previewWell.Add(image);
            Label placeholder = new Label { name = "PreviewPlaceholder" };
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = 4f;
            placeholder.style.right = 4f;
            placeholder.style.top = 4f;
            placeholder.style.bottom = 4f;
            placeholder.style.unityTextAlign = TextAnchor.MiddleCenter;
            placeholder.style.fontSize = 11f;
            placeholder.style.color = ESEditorPresentation.SectionMutedTextColor;
            previewWell.Add(placeholder);
            previewWell.Add(CreateContentUsageChip());
            previewWell.Add(CreateThumbnailKindChip());
            VisualElement overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 4f;
            overlay.style.right = 4f;
            overlay.style.top = 4f;
            overlay.style.flexDirection = FlexDirection.Row;
            Toggle batch = new Toggle { name = "Batch" };
            batch.tooltip = "加入批量放置选择";
            batch.style.width = 18f;
            Button favorite = new Button { name = "Favorite", text = "☆" };
            favorite.tooltip = "收藏或取消收藏";
            favorite.style.width = 24f;
            favorite.style.height = 22f;
            VisualElement overlaySpacer = new VisualElement();
            overlaySpacer.style.flexGrow = 1f;
            overlay.Add(batch);
            overlay.Add(overlaySpacer);
            overlay.Add(favorite);
            previewWell.Add(overlay);
            card.Add(previewWell);

            Label title = new Label { name = "Title" };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = compactContentVertical ? 10f : 12f;
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            title.style.marginTop = compactContentVertical ? 2f : 4f;
            card.Add(title);
            Label category = new Label { name = "Category" };
            category.style.fontSize = compactContentVertical ? 8f : 9f;
            category.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            category.style.overflow = Overflow.Hidden;
            category.style.textOverflow = TextOverflow.Ellipsis;
            card.Add(category);
            ToolbarMenu preset = new ToolbarMenu { name = "Preset", text = "预设" };
            preset.style.height = compactContentVertical ? 19f : 22f;
            preset.style.marginTop = compactContentVertical ? 1f : 2f;
            card.Add(preset);

            card.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || IsContentInteractiveTarget(evt.target as VisualElement)) return;
                ContentCardState state = (ContentCardState)card.userData;
                if (!contentPointerGate.TryAcquire(card, evt.pointerId))
                {
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!pointerCoordinator.TryAcquire(
                        card,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Content))
                {
                    contentPointerGate.Release(card, evt.pointerId);
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!state.drag.Arm(evt.pointerId, evt.position, state.item))
                {
                    contentPointerGate.Release(card, evt.pointerId);
                    pointerCoordinator.Release(
                        card,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Content);
                    evt.StopImmediatePropagation();
                    return;
                }
                card.CapturePointer(evt.pointerId);
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            card.RegisterCallback<PointerMoveEvent>(evt =>
            {
                TryStartContentCardDrag(card, evt);
                if (((ContentCardState)card.userData)?.drag.IsActive == true)
                    evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            card.RegisterCallback<PointerUpEvent>(evt =>
            {
                ContentCardState state = (ContentCardState)card.userData;
                bool click = state.drag.ShouldClick(evt.pointerId, evt.position);
                ReleaseContentCardDragPointer(card, state, evt.pointerId);
                contentPointerGate.Release(card, evt.pointerId);
                pointerCoordinator.Release(
                    card,
                    evt.pointerId,
                    ESWorkbenchPointerOwnerKind.Content);
                if (click && state.item != null)
                    actions.Selection.Select(GetEffectiveDescriptor(state.item).ToSelection());
                if (click) evt.StopImmediatePropagation();
            });
            card.RegisterCallback<PointerCancelEvent>(evt =>
            {
                ContentCardState state = (ContentCardState)card.userData;
                int pointerId = state?.drag?.PointerId ?? evt.pointerId;
                ReleaseContentCardDragPointer(card, state, pointerId);
                contentPointerGate.Release(card, pointerId);
                pointerCoordinator.Release(
                    card,
                    pointerId,
                    ESWorkbenchPointerOwnerKind.Content);
                evt.StopImmediatePropagation();
            });
            card.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                ContentCardState state = (ContentCardState)card.userData;
                if (state.drag.IsActive)
                    state.drag.End(ESWorkbenchContentDragEndReason.CaptureLost);
                state.drag.Reset();
                contentPointerGate.Release(card, evt.pointerId);
                pointerCoordinator.Release(
                    card,
                    evt.pointerId,
                    ESWorkbenchPointerOwnerKind.Content);
            });
            card.RegisterCallback<PointerEnterEvent>(_ =>
            {
                ContentCardState state = (ContentCardState)card.userData;
                state.hovered = true;
                ApplyContentCardVisual(card, state.item, true);
            });
            card.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                ContentCardState state = (ContentCardState)card.userData;
                state.hovered = false;
                if (!state.drag.IsActive || !card.HasPointerCapture(state.drag.PointerId))
                {
                    int pointerId = state.drag.PointerId;
                    state.drag.Reset();
                    if (pointerId >= 0) contentPointerGate.Release(card, pointerId);
                    if (pointerId >= 0)
                        pointerCoordinator.Release(
                            card,
                            pointerId,
                            ESWorkbenchPointerOwnerKind.Content);
                }
                ApplyContentCardVisual(card, state.item, false);
            });
            batch.RegisterValueChangedCallback(evt =>
                SetBatchContentSelected(((ContentCardState)card.userData).item, evt.newValue));
            favorite.clicked += () => ToggleContentFavorite(((ContentCardState)card.userData).item);
            return card;
        }

        private void BindContentGridRow(VisualElement element, int index)
        {
            ContentGridRow row = visibleGridRows[index];
            BindContentGridCard(element.Q<VisualElement>("ESWorkbenchContentGridCardFirst"), row.first);
            BindContentGridCard(element.Q<VisualElement>("ESWorkbenchContentGridCardSecond"), row.second);
        }

        private void BindContentGridCard(VisualElement card, ESWorkbenchObjectDescriptor item)
        {
            if (card == null) return;
            card.style.display = item == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (item == null)
            {
                ((ContentCardState)card.userData).item = null;
                return;
            }
            ESWorkbenchObjectDescriptor effective = GetEffectiveDescriptor(item);
            card.Q<Label>("Title").text = effective.DisplayName;
            card.Q<Label>("Category").text = item.ContentKindDisplayName + " · " + item.Category;
            Texture preview = ResolveContentThumbnail(effective);
            Image image = card.Q<Image>("Icon");
            image.image = preview;
            image.style.display = preview == null ? DisplayStyle.None : DisplayStyle.Flex;
            Label placeholder = card.Q<Label>("PreviewPlaceholder");
            placeholder.text = preview == null ? ResolveContentKindShortName(item.ContentKind) : string.Empty;
            placeholder.style.display = preview == null ? DisplayStyle.Flex : DisplayStyle.None;
            ApplyThumbnailWellVisual(
                card.Q<VisualElement>("PreviewWell"),
                card.Q<Label>("KindChip"),
                item);
            ApplyContentUsageChip(card.Q<Label>("UsageChip"), item);
            Toggle batch = card.Q<Toggle>("Batch");
            batch.SetValueWithoutNotify(batchContentIds.Contains(item.BaseObjectId));
            Button favorite = card.Q<Button>("Favorite");
            bool isFavorite = contentUsage.IsFavorite(item.BaseObjectId);
            favorite.text = isFavorite ? "★" : "☆";
            favorite.style.color = isFavorite
                ? new Color(1f, 0.76f, 0.24f, 1f)
                : ESEditorPresentation.SectionMutedTextColor;
            ConfigurePresetMenu(card.Q<ToolbarMenu>("Preset"), item);
            card.tooltip = string.IsNullOrWhiteSpace(effective.Tooltip) ? effective.ObjectId : effective.Tooltip;
            ContentCardState state = (ContentCardState)card.userData;
            ReleaseContentCardDragPointer(card, state, state.drag.PointerId);
            state.pulseVersion++;
            state.item = item;
            ApplyContentCardVisual(card, item, state.hovered);
        }

        private void TryStartContentCardDrag(VisualElement card, PointerMoveEvent evt)
        {
            ContentCardState state = card.userData as ContentCardState;
            if (state == null) return;
            TryStartContentDrag(card, state.item, state.drag, evt);
        }

        private void TryStartObjectDrag(VisualElement row, PointerMoveEvent evt)
        {
            ObjectRowState state = row.userData as ObjectRowState;
            if (state == null) return;
            TryStartContentDrag(row, state.item, state.drag, evt);
        }

        private void TryStartContentDrag(
            VisualElement source,
            ESWorkbenchObjectDescriptor sourceItem,
            ESWorkbenchContentDragSession drag,
            PointerMoveEvent evt)
        {
            if (source == null || drag == null || !drag.ShouldStart(evt.pointerId, evt.position)) return;
            ESWorkbenchObjectDescriptor item = GetEffectiveDescriptor(sourceItem);
            if (item == null || !item.CanDrag)
            {
                ReleaseDragPointer(source, drag, evt.pointerId);
                contentPointerGate.Release(source, evt.pointerId);
                return;
            }
            IReadOnlyList<ESWorkbenchObjectDescriptor> batch = ResolveBatchDragItems(item);
            if (!drag.TryStart(evt.pointerId, evt.position, item, batch)) return;
            batch = drag.Items;
            activeDragSessionToken = new object();
            if (!pointerCoordinator.TryPromoteToExternalContent(
                    source,
                    evt.pointerId,
                    activeDragSessionToken))
            {
                ReleaseDragPointer(source, drag, evt.pointerId);
                contentPointerGate.Release(source, evt.pointerId);
                pointerCoordinator.Release(
                    source,
                    evt.pointerId,
                    ESWorkbenchPointerOwnerKind.Content);
                activeDragSessionToken = null;
                return;
            }
            try
            {
                // External content drag owns the pointer. Cancel any in-flight viewport
                // gesture before Unity transfers capture, so the scene cannot retain a
                // transient transform while the card is being placed.
                CancelActiveViewportInteraction();
                ClearOwnedDragPayload(true);
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DragSessionKey, activeDragSessionToken);
                DragAndDrop.SetGenericData(DragPayloadKey, item);
                DragAndDrop.SetGenericData(BatchDragPayloadKey, batch.Count > 1 ? batch : null);
                DragAndDrop.objectReferences = batch
                    .Select(value => value?.Source)
                    .Where(value => value != null)
                    .Distinct()
                    .ToArray();
                externalDragPayloadToken = activeDragSessionToken;
                externalDragTransferInFlight = true;
                NoteExternalDragSignal();
                DragAndDrop.StartDrag(batch.Count > 1 ? "批量放置 " + batch.Count + " 项" : item.DisplayName);
            }
            catch (Exception exception)
            {
                externalDragTransferInFlight = false;
                ClearOwnedDragPayload(true);
                pointerCoordinator.EndExternalContent(activeDragSessionToken);
                activeDragSessionToken = null;
                Debug.LogException(exception);
                SetStatus("无法开始拖动：" + exception.Message, MessageType.Error);
            }
            finally
            {
                ReleaseDragPointer(source, drag, evt.pointerId);
                contentPointerGate.Release(source, evt.pointerId);
            }
            evt.StopPropagation();
        }

        private void CancelActiveViewportInteraction()
        {
            if (activeViewport == null || !activeViewportActivated) return;
            ClearViewportDropPreview();
            try
            {
                if (activeViewport is IESWorkbenchCancelableViewport cancelable)
                    cancelable.CancelInteraction();
                else
                {
                    activeViewport.Deactivate();
                    activeViewport.Activate();
                }
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void ReleaseObjectDragPointer(VisualElement row, ObjectRowState state, int pointerId)
        {
            if (state == null) return;
            ReleaseDragPointer(row, state.drag, pointerId);
        }

        private static void ReleaseContentCardDragPointer(VisualElement card, ContentCardState state, int pointerId)
        {
            if (state == null) return;
            ReleaseDragPointer(card, state.drag, pointerId);
        }

        private static void ReleaseDragPointer(
            VisualElement source,
            ESWorkbenchContentDragSession drag,
            int fallbackPointerId)
        {
            if (source == null || drag == null) return;
            int capturedId = drag.IsActive ? drag.PointerId : fallbackPointerId;
            if (drag.IsActive)
                drag.End(ESWorkbenchContentDragEndReason.Invalidated);
            else
                drag.Reset();
            if (capturedId >= 0 && source.HasPointerCapture(capturedId)) source.ReleasePointer(capturedId);
        }

        private static VisualElement CreateThumbnailWell(string name, float width, float height)
        {
            VisualElement well = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            well.AddToClassList("es-workbench-resource-preview");
            if (width > 0f)
            {
                well.style.width = width;
                well.style.flexShrink = 0f;
            }
            well.style.height = height;
            well.style.minHeight = height;
            well.style.position = Position.Relative;
            well.style.overflow = Overflow.Hidden;
            well.style.backgroundColor = new Color(0.075f, 0.085f, 0.1f, 1f);
            well.style.borderLeftWidth = 0f;
            well.style.borderRightWidth = 0f;
            well.style.borderTopWidth = 0f;
            well.style.borderBottomWidth = 1f;
            well.style.borderLeftColor = new Color(0.22f, 0.24f, 0.28f, 1f);
            well.style.borderRightColor = new Color(0.22f, 0.24f, 0.28f, 1f);
            well.style.borderTopColor = new Color(0.26f, 0.28f, 0.32f, 1f);
            well.style.borderBottomColor = new Color(0.14f, 0.15f, 0.18f, 1f);
            return well;
        }

        private static Label CreateThumbnailKindChip()
        {
            var chip = new Label { name = "KindChip", pickingMode = PickingMode.Ignore };
            chip.style.position = Position.Absolute;
            chip.style.right = 4f;
            chip.style.bottom = 4f;
            chip.style.paddingLeft = 4f;
            chip.style.paddingRight = 4f;
            chip.style.paddingTop = 1f;
            chip.style.paddingBottom = 1f;
            chip.style.fontSize = 8f;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.color = Color.white;
            return chip;
        }

        private static VisualElement CreateContentSelectionBar()
        {
            var bar = new VisualElement
            {
                name = "SelectionBar",
                pickingMode = PickingMode.Ignore
            };
            bar.style.position = Position.Absolute;
            bar.style.left = 0f;
            bar.style.top = 0f;
            bar.style.bottom = 0f;
            bar.style.width = 3f;
            bar.style.backgroundColor = Color.clear;
            return bar;
        }

        private static Label CreateContentUsageChip()
        {
            var chip = new Label
            {
                name = "UsageChip",
                pickingMode = PickingMode.Ignore
            };
            chip.style.position = Position.Absolute;
            chip.style.left = 4f;
            chip.style.bottom = 4f;
            chip.style.paddingLeft = 4f;
            chip.style.paddingRight = 4f;
            chip.style.paddingTop = 1f;
            chip.style.paddingBottom = 1f;
            chip.style.fontSize = 8f;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.color = Color.white;
            chip.style.display = DisplayStyle.None;
            return chip;
        }

        private static void ApplyThumbnailWellVisual(
            VisualElement well,
            Label chip,
            ESWorkbenchObjectDescriptor item)
        {
            if (well == null || item == null) return;
            Color accent = ResolveContentKindAccent(item.ContentKind);
            well.style.borderLeftColor = Color.Lerp(accent, Color.black, 0.18f);
            well.style.borderRightColor = Color.Lerp(accent, Color.black, 0.42f);
            well.style.borderTopColor = Color.Lerp(accent, Color.white, 0.08f);
            well.style.borderBottomColor = Color.Lerp(accent, Color.black, 0.5f);
            if (chip == null) return;
            chip.text = item.ContentKindDisplayName;
            chip.style.backgroundColor = new Color(accent.r * 0.72f, accent.g * 0.72f, accent.b * 0.72f, 0.94f);
        }

        private void ApplyContentUsageChip(Label chip, ESWorkbenchObjectDescriptor item)
        {
            if (chip == null || item == null) return;
            ESWorkbenchContentUsageRecord usage = contentUsage.Get(item.BaseObjectId);
            Color accent;
            if (usage?.favorite == true)
            {
                chip.text = "★ 收藏";
                accent = new Color(0.92f, 0.62f, 0.14f, 1f);
            }
            else if ((usage?.useCount ?? 0) >= 3)
            {
                chip.text = "常用 " + usage.useCount;
                accent = new Color(0.16f, 0.65f, 0.78f, 1f);
            }
            else if ((usage?.lastUsedUtcTicks ?? 0L) > 0L)
            {
                chip.text = "最近";
                accent = new Color(0.35f, 0.55f, 0.9f, 1f);
            }
            else if (item.Priority > 0)
            {
                chip.text = "推荐";
                accent = ResolveContentKindAccent(item.ContentKind);
            }
            else
            {
                chip.text = string.Empty;
                chip.style.display = DisplayStyle.None;
                return;
            }
            chip.style.backgroundColor = new Color(accent.r * 0.7f, accent.g * 0.7f, accent.b * 0.7f, 0.94f);
            chip.style.display = DisplayStyle.Flex;
        }

        private static bool IsContentInteractiveTarget(VisualElement target)
        {
            VisualElement current = target;
            while (current != null)
            {
                if (current is Button || current is Toggle || current is ToolbarMenu) return true;
                if (current.name == "ESWorkbenchObjectRow" || current.name.StartsWith("ESWorkbenchContentGridCard", StringComparison.Ordinal))
                    break;
                current = current.parent;
            }
            return false;
        }

        private void ApplyContentCardVisual(
            VisualElement element,
            ESWorkbenchObjectDescriptor item,
            bool hovered)
        {
            if (element == null || item == null) return;
            bool selected = string.Equals(
                actions.Selection.Current?.StableId,
                item.BaseObjectId,
                StringComparison.Ordinal);
            Color accent = ResolveContentKindAccent(item.ContentKind);
            Color background = selected
                ? new Color(accent.r * 0.18f, accent.g * 0.18f, accent.b * 0.18f, 1f)
                : hovered
                    ? ESEditorPresentation.WindowRaisedSurfaceColor
                    : ESEditorPresentation.ControlSurfaceColor;
            element.style.backgroundColor = background;
            element.style.borderLeftColor = selected || hovered ? accent : ESEditorPresentation.DividerColor;
            element.style.borderRightColor = selected ? accent : ESEditorPresentation.DividerColor;
            element.style.borderTopColor = selected ? accent : ESEditorPresentation.DividerColor;
            element.style.borderBottomColor = selected || hovered ? accent : ESEditorPresentation.DividerColor;
            element.style.opacity = hovered || selected ? 1f : 0.94f;
            VisualElement selectionBar = element.Q<VisualElement>("SelectionBar");
            if (selectionBar != null)
            {
                selectionBar.style.width = selected ? 4f : hovered ? 2f : 0f;
                selectionBar.style.backgroundColor = selected || hovered ? accent : Color.clear;
            }
            VisualElement previewWell = element.Q<VisualElement>("PreviewWell")
                ?? element.Q<VisualElement>("ESWorkbenchListThumbnail");
            if (previewWell != null)
                previewWell.style.backgroundColor = selected
                    ? new Color(accent.r * 0.105f, accent.g * 0.105f, accent.b * 0.105f, 1f)
                    : hovered
                        ? new Color(0.09f, 0.105f, 0.125f, 1f)
                        : new Color(0.075f, 0.085f, 0.1f, 1f);
        }

        private void ToggleContentFavorite(ESWorkbenchObjectDescriptor item)
        {
            if (item == null) return;
            bool favorite = contentUsage.ToggleFavorite(item.BaseObjectId);
            SetStatus((favorite ? "已收藏：" : "已取消收藏：") + item.DisplayName, MessageType.Info);
            RebuildObjectList(refreshSource: false);
        }

        private void SetBatchContentSelected(ESWorkbenchObjectDescriptor item, bool selected)
        {
            if (item == null) return;
            if (selected) batchContentIds.Add(item.BaseObjectId);
            else batchContentIds.Remove(item.BaseObjectId);
            PersistBatchContentSelection();
            UpdateBatchPlaceButton();
            objectList?.RefreshItems();
            objectGridList?.RefreshItems();
        }

        private void SelectVisibleBatchContent()
        {
            foreach (ESWorkbenchObjectDescriptor item in visibleObjects)
                if (item?.CanDrag == true) batchContentIds.Add(item.BaseObjectId);
            PersistBatchContentSelection();
            UpdateBatchPlaceButton();
            objectList?.RefreshItems();
            objectGridList?.RefreshItems();
            SetStatus("已选择当前结果中的 " + ResolveSelectedBatchItems().Count + " 项可放置内容。", MessageType.Info);
        }

        private void ClearBatchContentSelection()
        {
            if (batchContentIds.Count == 0) return;
            batchContentIds.Clear();
            PersistBatchContentSelection();
            UpdateBatchPlaceButton();
            objectList?.RefreshItems();
            objectGridList?.RefreshItems();
            SetStatus("已清空批量内容选择。", MessageType.Info);
        }

        private void PersistBatchContentSelection()
        {
            layout.selectedContentIds = batchContentIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private void SetContentBatchSpacing(float spacing)
        {
            layout.contentBatchSpacing = Mathf.Clamp(spacing, 0.25f, 32f);
            BuildContentBatchMenu();
            UpdateBatchPlaceButton();
            SetStatus("批量放置间距已设为 " + layout.contentBatchSpacing.ToString("0.#") + "。", MessageType.Info);
        }

        private void BuildContentBatchMenu()
        {
            if (batchMenu == null) return;
            int selectedCount = ResolveSelectedBatchItems().Count;
            int visiblePlaceableCount = visibleObjects.Count(value => value?.CanDrag == true);
            batchMenu.menu.MenuItems().Clear();
            batchMenu.menu.AppendAction(
                "选择当前结果（" + visiblePlaceableCount + " 项）",
                _ => SelectVisibleBatchContent(),
                _ => visiblePlaceableCount > 0
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            batchMenu.menu.AppendAction(
                "清空批量选择",
                _ => ClearBatchContentSelection(),
                _ => batchContentIds.Count > 0
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            batchMenu.menu.AppendSeparator();
            AppendBatchSpacingAction(1f);
            AppendBatchSpacingAction(2f);
            AppendBatchSpacingAction(4f);
            AppendBatchSpacingAction(8f);
            batchMenu.menu.AppendSeparator();
            batchMenu.menu.AppendAction(
                "立即放置（" + selectedCount + " 项）",
                _ => PlaceBatchAtViewportCenter(),
                _ => selectedCount > 1 && activeViewport is IESWorkbenchBatchViewport
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            batchMenu.text = selectedCount > 0 ? "批选 " + selectedCount : "批选";
            batchMenu.tooltip = "已选择 " + selectedCount + " 项；放置间距 "
                + Mathf.Max(0.25f, layout.contentBatchSpacing).ToString("0.#");
        }

        private void AppendBatchSpacingAction(float spacing)
        {
            batchMenu.menu.AppendAction(
                "放置间距/" + spacing.ToString("0.#"),
                _ => SetContentBatchSpacing(spacing),
                _ => Mathf.Approximately(layout.contentBatchSpacing, spacing)
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
        }

        private void UpdateBatchPlaceButton()
        {
            if (batchPlaceButton == null) return;
            int count = ResolveSelectedBatchItems().Count;
            batchPlaceButton.text = "批量 " + count;
            batchPlaceButton.SetEnabled(count > 1 && activeViewport is IESWorkbenchBatchViewport);
            batchPlaceButton.tooltip = count > 1
                ? "把 " + count + " 项内容作为一个 Undo 事务放置到当前视口中心；也可拖动任一已勾选卡片"
                : "至少勾选两项可批量放置的内容";
            BuildContentBatchMenu();
        }

        private void ConfigurePresetMenu(ToolbarMenu menu, ESWorkbenchObjectDescriptor item)
        {
            if (menu == null) return;
            menu.menu.MenuItems().Clear();
            bool hasPresets = item?.HasPresets == true;
            menu.style.display = hasPresets ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasPresets) return;
            selectedPresetByObjectId.TryGetValue(item.BaseObjectId, out string selectedPresetId);
            ESWorkbenchContentPresetDescriptor selectedPreset = item.Presets.FirstOrDefault(value =>
                string.Equals(value.PresetId, selectedPresetId, StringComparison.Ordinal));
            menu.text = selectedPreset?.DisplayName ?? "默认";
            menu.tooltip = selectedPreset == null
                ? "当前使用内容默认参数"
                : selectedPreset.DisplayName + " · " + selectedPreset.Tooltip;
            menu.menu.AppendAction(
                "默认参数",
                _ => SelectContentPreset(item.BaseObjectId, string.Empty),
                _ => string.IsNullOrWhiteSpace(selectedPresetId)
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            foreach (ESWorkbenchContentPresetDescriptor preset in item.Presets)
            {
                ESWorkbenchContentPresetDescriptor captured = preset;
                menu.menu.AppendAction(
                    captured.DisplayName,
                    _ => SelectContentPreset(item.BaseObjectId, captured.PresetId),
                    _ => string.Equals(selectedPresetId, captured.PresetId, StringComparison.Ordinal)
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
        }

        private void SelectContentPreset(string objectId, string presetId)
        {
            if (string.IsNullOrWhiteSpace(objectId)) return;
            if (string.IsNullOrWhiteSpace(presetId)) selectedPresetByObjectId.Remove(objectId);
            else selectedPresetByObjectId[objectId] = presetId;
            layout.contentPresetSelections = selectedPresetByObjectId
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ESWorkbenchContentPresetSelectionState
                {
                    objectId = pair.Key,
                    presetId = pair.Value
                })
                .ToList();
            contentSourceById.TryGetValue(objectId, out ESWorkbenchObjectDescriptor descriptor);
            if (descriptor != null && string.Equals(actions.Selection.Current?.StableId, objectId, StringComparison.Ordinal))
                actions.Selection.Select(GetEffectiveDescriptor(descriptor).ToSelection());
            objectList?.RefreshItems();
            objectGridList?.RefreshItems();
        }

        private ESWorkbenchObjectDescriptor GetEffectiveDescriptor(ESWorkbenchObjectDescriptor item)
        {
            if (item == null || !item.HasPresets) return item;
            return selectedPresetByObjectId.TryGetValue(item.BaseObjectId, out string presetId)
                ? item.CreatePresetVariant(presetId)
                : item;
        }

        private IReadOnlyList<ESWorkbenchObjectDescriptor> ResolveSelectedBatchItems()
        {
            if (batchContentIds.Count == 0) return Array.Empty<ESWorkbenchObjectDescriptor>();
            if (contentSourceSnapshot.Count == 0) return Array.Empty<ESWorkbenchObjectDescriptor>();
            return contentSourceSnapshot.Where(value => value != null && value.CanDrag && batchContentIds.Contains(value.BaseObjectId))
                .Select(GetEffectiveDescriptor)
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.BaseObjectId, StringComparer.Ordinal)
                .ToArray();
        }

        private IReadOnlyList<ESWorkbenchObjectDescriptor> ResolveBatchDragItems(ESWorkbenchObjectDescriptor anchor)
        {
            if (anchor == null || !batchContentIds.Contains(anchor.BaseObjectId))
                return anchor == null ? Array.Empty<ESWorkbenchObjectDescriptor>() : new[] { anchor };
            IReadOnlyList<ESWorkbenchObjectDescriptor> selected = ResolveSelectedBatchItems();
            return selected.Count > 1 ? selected : new[] { anchor };
        }

        private void PlaceBatchAtViewportCenter()
        {
            IReadOnlyList<ESWorkbenchObjectDescriptor> items = ResolveSelectedBatchItems();
            if (items.Count <= 1)
            {
                SetStatus("至少勾选两项可放置内容后才能批量操作。", MessageType.Warning);
                return;
            }
            Vector2 local = centerContent?.contentRect.center ?? Vector2.zero;
            TryPlaceBatch(items, local);
        }

        private bool TryPlaceBatch(
            IReadOnlyList<ESWorkbenchObjectDescriptor> items,
            Vector2 localPosition)
        {
            if (!(activeViewport is IESWorkbenchBatchViewport batchViewport))
            {
                SetStatus("当前视口没有注册批量放置合同。", MessageType.Warning);
                return false;
            }
            if (!batchViewport.CanAcceptBatch(items, out string reason))
            {
                SetStatus(string.IsNullOrWhiteSpace(reason) ? "当前视口拒绝批量内容。" : reason, MessageType.Warning);
                return false;
            }
            localPosition = ResolveActiveViewportLocalPosition(localPosition);
            var context = new ESWorkbenchBatchDropContext(
                actions,
                items,
                localPosition,
                activeViewport?.Root?.contentRect ?? centerContent?.contentRect ?? Rect.zero,
                Mathf.Max(0.25f, layout.contentBatchSpacing));
            if (!batchViewport.TryAcceptBatch(context, out string message))
            {
                SetStatus(string.IsNullOrWhiteSpace(message) ? "批量放置失败。" : message, MessageType.Warning);
                return false;
            }
            foreach (ESWorkbenchObjectDescriptor item in items) contentUsage.RecordUse(item.BaseObjectId);
            SetStatus(string.IsNullOrWhiteSpace(message) ? "批量内容已放入工作区。" : message,
                actions.Authoring.LastOperationCommittedWithPostCommitFailure ? MessageType.Error : MessageType.Info);
            RebuildObjectList();
            return true;
        }

        private Texture ResolveContentThumbnail(ESWorkbenchObjectDescriptor item)
        {
            if (item == null) return null;
            if (item.Icon != null) return item.Icon;
            if (item.Source == null) return ResolveGeneratedContentThumbnail(item);
            string key = item.BaseObjectId + "@" + item.Source.GetInstanceID();
            if (!thumbnailCache.TryGetValue(key, out ThumbnailEntry entry))
            {
                entry = new ThumbnailEntry { source = item.Source };
                thumbnailCache.Add(key, entry);
            }
            if (entry.complete)
                return entry.texture ?? entry.fallback ?? ResolveGeneratedContentThumbnail(item);
            Texture preview = AssetPreview.GetAssetPreview(item.Source);
            if (preview != null)
            {
                entry.texture = preview;
                entry.complete = true;
                return preview;
            }
            entry.fallback ??= AssetPreview.GetMiniThumbnail(item.Source);
            if (!AssetPreview.IsLoadingAssetPreview(item.Source.GetInstanceID())
                && entry.attempts >= MaximumThumbnailAttempts)
                entry.complete = true;
            else EnsureThumbnailRefreshScheduled();
            return entry.fallback ?? ResolveGeneratedContentThumbnail(item);
        }

        private Texture2D ResolveGeneratedContentThumbnail(ESWorkbenchObjectDescriptor item)
        {
            if (item == null) return null;
            string key = item.ContentKind + ":" + item.BaseObjectId + ":" + item.PresetId;
            if (generatedThumbnailCache.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                TouchGeneratedThumbnail(key);
                return cached;
            }
            int seed = ComputeStableThumbnailSeed(key);
            Texture2D texture = CreateGeneratedContentThumbnail(
                item.ContentKind,
                seed,
                "ESWorkbenchContentThumbnail_" + Hash128.Compute(key));
            generatedThumbnailCache[key] = texture;
            TouchGeneratedThumbnail(key);
            TrimGeneratedThumbnailCache();
            return texture;
        }

        private void TouchGeneratedThumbnail(string key)
        {
            if (generatedThumbnailLruNodes.TryGetValue(key, out LinkedListNode<string> existing))
                generatedThumbnailLru.Remove(existing);
            LinkedListNode<string> node = generatedThumbnailLru.AddLast(key);
            generatedThumbnailLruNodes[key] = node;
        }

        private void TrimGeneratedThumbnailCache()
        {
            while (generatedThumbnailCache.Count > MaximumGeneratedThumbnailCacheEntries
                   && generatedThumbnailLru.First != null)
            {
                string expiredKey = generatedThumbnailLru.First.Value;
                generatedThumbnailLru.RemoveFirst();
                generatedThumbnailLruNodes.Remove(expiredKey);
                if (!generatedThumbnailCache.TryGetValue(expiredKey, out Texture2D expired)) continue;
                generatedThumbnailCache.Remove(expiredKey);
                if (expired != null) UnityEngine.Object.DestroyImmediate(expired);
            }
        }

        private Texture2D ResolveSemanticContentThumbnail(ESWorkbenchContentKind kind)
        {
            if (semanticThumbnailCache.TryGetValue(kind, out Texture2D cached) && cached != null)
                return cached;
            Texture2D texture = CreateGeneratedContentThumbnail(
                kind,
                ComputeStableThumbnailSeed("semantic:" + kind),
                "ESWorkbenchSemanticThumbnail_" + kind);
            semanticThumbnailCache[kind] = texture;
            return texture;
        }

        private static Texture2D CreateGeneratedContentThumbnail(
            ESWorkbenchContentKind kind,
            int seed,
            string name)
        {
            int width = GeneratedThumbnailWidth;
            int height = GeneratedThumbnailHeight;
            var texture = new Texture2D(width, height, UnityEngine.TextureFormat.RGBA32, false, true)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                Color32[] pixels = BuildGeneratedContentThumbnailPixels(kind, seed, width, height);
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                return texture;
            }
            catch
            {
                SafeDestroyThumbnail(texture);
                throw;
            }
        }

        private static Color32[] BuildGeneratedContentThumbnailPixels(
            ESWorkbenchContentKind kind,
            int seed,
            int width,
            int height)
        {
            var pixels = new Color32[width * height];
            Color accent = ResolveVariantAccent(ResolveContentKindAccent(kind), seed);
            Color top = Color.Lerp(new Color(0.055f, 0.068f, 0.085f, 1f), accent, 0.14f);
            Color bottom = Color.Lerp(new Color(0.025f, 0.032f, 0.043f, 1f), accent, 0.025f);
            Color minorGrid = Color.Lerp(bottom, accent, 0.12f);
            Color majorGrid = Color.Lerp(bottom, accent, 0.22f);
            float offsetX = (seed & 7) * 0.013f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float vertical = y / (float)(height - 1);
                    float horizontal = x / (float)(width - 1);
                    float centerGlow = Mathf.Clamp01(1f - Mathf.Abs(horizontal - 0.5f - offsetX) * 1.8f);
                    Color value = Color.Lerp(bottom, top, vertical * 0.82f + centerGlow * 0.1f);
                    float distance = Vector2.Distance(new Vector2(horizontal, vertical), new Vector2(0.5f, 0.52f));
                    value = Color.Lerp(value, bottom, Mathf.Clamp01((distance - 0.34f) * 1.45f));
                    bool major = x % 32 == 0 || y % 32 == 0;
                    bool minor = x % 16 == 0 || y % 16 == 0;
                    if (major) value = Color.Lerp(value, majorGrid, 0.78f);
                    else if (minor) value = Color.Lerp(value, minorGrid, 0.58f);
                    pixels[y * width + x] = (Color32)value;
                }
            }
            Color32 topRule = (Color32)Color.Lerp(accent, Color.white, 0.12f);
            FillThumbnailRect(pixels, width, height, 0, height - 3, width, 3, topRule);
            DrawSemanticThumbnailMotif(pixels, width, height, kind, (Color32)accent, seed);
            return pixels;
        }

        private static int ComputeStableThumbnailSeed(string value)
        {
            unchecked
            {
                int hash = (int)2166136261;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++) hash = (hash ^ source[i]) * 16777619;
                return hash & int.MaxValue;
            }
        }

        private static Color ResolveVariantAccent(Color accent, int seed)
        {
            Color.RGBToHSV(accent, out float hue, out float saturation, out float value);
            float hueOffset = (((seed >> 3) & 15) - 7f) * 0.0045f;
            float saturationScale = 0.92f + ((seed >> 8) & 7) * 0.018f;
            return Color.HSVToRGB(
                Mathf.Repeat(hue + hueOffset, 1f),
                Mathf.Clamp01(saturation * saturationScale),
                Mathf.Clamp01(value * 0.96f + 0.035f));
        }

        private static void DrawSemanticThumbnailMotif(
            Color32[] pixels,
            int width,
            int height,
            ESWorkbenchContentKind kind,
            Color32 accent,
            int seed)
        {
            Color32 bright = (Color32)Color.Lerp((Color)accent, Color.white, 0.28f);
            Color32 muted = (Color32)Color.Lerp((Color)accent, Color.black, 0.28f);
            int variant = seed % 11;
            int X(int value) => Mathf.RoundToInt(value * width / 96f);
            int Y(int value) => Mathf.RoundToInt(value * height / 72f);
            int T(int value) => Mathf.Max(1, Mathf.RoundToInt(value * Mathf.Min(width / 96f, height / 72f)));
            void Line(int x0, int y0, int x1, int y1, Color32 color, int thickness) =>
                DrawThumbnailLine(pixels, width, height, X(x0), Y(y0), X(x1), Y(y1), color, T(thickness));
            void FillRect(int x, int y, int rectWidth, int rectHeight, Color32 color) =>
                FillThumbnailRect(pixels, width, height, X(x), Y(y), X(rectWidth), Y(rectHeight), color);
            void Rect(int x, int y, int rectWidth, int rectHeight, Color32 color, int thickness) =>
                DrawThumbnailRect(pixels, width, height, X(x), Y(y), X(rectWidth), Y(rectHeight), color, T(thickness));
            void FillCircle(int x, int y, int radius, Color32 color) =>
                FillThumbnailCircle(pixels, width, height, X(x), Y(y), T(radius), color);
            void Circle(int x, int y, int radius, Color32 color, int thickness) =>
                DrawThumbnailCircle(pixels, width, height, X(x), Y(y), T(radius), color, T(thickness));
            void Pixel(int x, int y, Color32 color, int thickness) =>
                SetThumbnailPixel(pixels, width, height, X(x), Y(y), color, T(thickness));
            switch (kind)
            {
                case ESWorkbenchContentKind.Prefab:
                    FillRect(27, 21, 42, 31, muted);
                    Rect(27, 21, 42, 31, bright, 2);
                    Line(23, 21, 48, 7 + variant % 3, bright, 2);
                    Line(48, 7 + variant % 3, 73, 21, bright, 2);
                    FillRect(43, 21, 10, 18 + variant % 5, bright);
                    Line(20, 55, 76, 55, muted, 1);
                    break;
                case ESWorkbenchContentKind.Brush:
                    int radius = 18 + variant % 7;
                    FillCircle(48, 36, radius, muted);
                    Circle(48, 36, radius, bright, 2);
                    for (int contour = -10; contour <= 10; contour += 10)
                        Line(26, 36 + contour / 2, 70, 36 + contour / 2 + (variant % 3 - 1) * 2, bright, 1);
                    Line(29, 50 - variant % 5, 69, 22 + variant % 5, bright, 3);
                    break;
                case ESWorkbenchContentKind.SceneTemplate:
                    Line(9, 20, 31, 44 - variant % 4, muted, 3);
                    Line(31, 44 - variant % 4, 50, 24 + variant % 5, muted, 3);
                    Line(50, 24 + variant % 5, 72, 49, bright, 3);
                    Line(72, 49, 88, 32 + variant % 5, bright, 3);
                    Line(8, 14, 88, 14, bright, 2);
                    FillCircle(76, 24, 5, bright);
                    break;
                case ESWorkbenchContentKind.RegionTemplate:
                    Rect(16 + variant % 4, 13, 63 - variant % 5, 44, bright, 2);
                    Rect(25, 22 + variant % 4, 45, 25 - variant % 3, muted, 2);
                    Line(48, 9, 48, 63, bright, 1);
                    Line(11, 35, 85, 35, bright, 1);
                    FillCircle(48, 35, 4, bright);
                    break;
                case ESWorkbenchContentKind.Terrain:
                    for (int y = 18; y <= 52; y += 9)
                        for (int x = 8; x < 88; x++)
                            Pixel(x,
                                y + Mathf.RoundToInt(Mathf.Sin((x + y + variant * 3) * 0.12f) * 4f),
                                y == 45 ? bright : muted, 1);
                    break;
                case ESWorkbenchContentKind.Vegetation:
                    for (int x = 22; x <= 74; x += 26)
                    {
                        FillRect(x - 2, 15, 4, 20 + variant % 5, muted);
                        FillCircle(x, 43, 9 + (x + variant) % 4, bright);
                    }
                    break;
                case ESWorkbenchContentKind.Gameplay:
                    Line(24, 21, 48, 49, muted, 3);
                    Line(48, 49, 73, 24 + variant % 4, muted, 3);
                    FillCircle(24, 21, 7, bright);
                    FillCircle(48, 49, 8, bright);
                    FillCircle(73, 24 + variant % 4, 7, bright);
                    break;
                default:
                    Line(48, 9, 79, 36, bright, 3);
                    Line(79, 36, 48, 63, bright, 3);
                    Line(48, 63, 17, 36, muted, 3);
                    Line(17, 36, 48, 9, muted, 3);
                    Circle(48, 36, 8 + variant % 5, bright, 2);
                    break;
            }
        }

        private static void SetThumbnailPixel(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            Color32 color,
            int thickness = 1)
        {
            int radius = Mathf.Max(0, thickness - 1);
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    int targetX = x + offsetX;
                    int targetY = y + offsetY;
                    if (targetX < 0 || targetX >= width || targetY < 0 || targetY >= height) continue;
                    pixels[targetY * width + targetX] = color;
                }
        }

        private static void DrawThumbnailLine(
            Color32[] pixels,
            int width,
            int height,
            int x0,
            int y0,
            int x1,
            int y1,
            Color32 color,
            int thickness)
        {
            int deltaX = Mathf.Abs(x1 - x0);
            int stepX = x0 < x1 ? 1 : -1;
            int deltaY = -Mathf.Abs(y1 - y0);
            int stepY = y0 < y1 ? 1 : -1;
            int error = deltaX + deltaY;
            while (true)
            {
                SetThumbnailPixel(pixels, width, height, x0, y0, color, thickness);
                if (x0 == x1 && y0 == y1) break;
                int doubled = error * 2;
                if (doubled >= deltaY)
                {
                    error += deltaY;
                    x0 += stepX;
                }
                if (doubled <= deltaX)
                {
                    error += deltaX;
                    y0 += stepY;
                }
            }
        }

        private static void FillThumbnailRect(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            int rectWidth,
            int rectHeight,
            Color32 color)
        {
            for (int targetY = y; targetY < y + rectHeight; targetY++)
                for (int targetX = x; targetX < x + rectWidth; targetX++)
                    SetThumbnailPixel(pixels, width, height, targetX, targetY, color);
        }

        private static void DrawThumbnailRect(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            int rectWidth,
            int rectHeight,
            Color32 color,
            int thickness)
        {
            DrawThumbnailLine(pixels, width, height, x, y, x + rectWidth, y, color, thickness);
            DrawThumbnailLine(pixels, width, height, x + rectWidth, y, x + rectWidth, y + rectHeight, color, thickness);
            DrawThumbnailLine(pixels, width, height, x + rectWidth, y + rectHeight, x, y + rectHeight, color, thickness);
            DrawThumbnailLine(pixels, width, height, x, y + rectHeight, x, y, color, thickness);
        }

        private static void FillThumbnailCircle(
            Color32[] pixels,
            int width,
            int height,
            int centerX,
            int centerY,
            int radius,
            Color32 color)
        {
            int squaredRadius = radius * radius;
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= squaredRadius)
                        SetThumbnailPixel(pixels, width, height, centerX + x, centerY + y, color);
        }

        private static void DrawThumbnailCircle(
            Color32[] pixels,
            int width,
            int height,
            int centerX,
            int centerY,
            int radius,
            Color32 color,
            int thickness)
        {
            int steps = Mathf.Max(24, radius * 5);
            int previousX = centerX + radius;
            int previousY = centerY;
            for (int i = 1; i <= steps; i++)
            {
                float angle = i / (float)steps * Mathf.PI * 2f;
                int x = centerX + Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int y = centerY + Mathf.RoundToInt(Mathf.Sin(angle) * radius);
                DrawThumbnailLine(pixels, width, height, previousX, previousY, x, y, color, thickness);
                previousX = x;
                previousY = y;
            }
        }

        private void EnsureThumbnailRefreshScheduled()
        {
            if (disposed || root == null || thumbnailRefreshSchedule != null) return;
            thumbnailRefreshSchedule = root.schedule.Execute(PollContentThumbnails).Every(180);
        }

        private void PollContentThumbnails()
        {
            if (disposed)
            {
                thumbnailRefreshSchedule?.Pause();
                thumbnailRefreshSchedule = null;
                return;
            }

            bool pending = false;
            bool changed = false;
            foreach (ThumbnailEntry entry in thumbnailCache.Values)
            {
                if (entry == null || entry.complete || entry.source == null) continue;
                entry.attempts++;
                Texture preview = AssetPreview.GetAssetPreview(entry.source);
                if (preview != null)
                {
                    entry.texture = preview;
                    entry.complete = true;
                    changed = true;
                }
                else if (entry.attempts >= MaximumThumbnailAttempts
                    && !AssetPreview.IsLoadingAssetPreview(entry.source.GetInstanceID()))
                    entry.complete = true;
                else pending = true;
            }
            if (changed)
            {
                objectList?.RefreshItems();
                objectGridList?.RefreshItems();
            }
            if (pending) return;
            thumbnailRefreshSchedule?.Pause();
            thumbnailRefreshSchedule = null;
        }

        private static string ResolveContentKindShortName(ESWorkbenchContentKind kind)
        {
            switch (kind)
            {
                case ESWorkbenchContentKind.Prefab: return "预制件\n预览";
                case ESWorkbenchContentKind.Brush: return "笔刷\n预设";
                case ESWorkbenchContentKind.SceneTemplate: return "场景\n模板";
                case ESWorkbenchContentKind.RegionTemplate: return "区域\n模板";
                case ESWorkbenchContentKind.Terrain: return "地形\n内容";
                case ESWorkbenchContentKind.Vegetation: return "植被\n内容";
                case ESWorkbenchContentKind.Gameplay: return "玩法\n内容";
                default: return "内容\n预览";
            }
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            NoteExternalDragSignal();
            ESWorkbenchObjectDescriptor item = ResolveDragItem();
            IReadOnlyList<ESWorkbenchObjectDescriptor> batch = ResolveDragBatch();
            string reason = string.Empty;
            Vector3 resolvedDropPosition = default;
            bool hasResolvedDropPosition = false;
            bool ownsExternalPointer = activeDragSessionToken != null
                ? pointerCoordinator.IsExternalContentActive
                : ((item != null || batch.Count > 0) && EnsureExternalPointerOwnership());
            bool accepted = ownsExternalPointer && (batch.Count > 1
                ? CanViewportAcceptBatch(activeViewport, batch, out reason)
                : CanViewportAccept(activeViewport, item, out reason));
            if (accepted
                && activeViewport is IESWorkbenchViewportDropPositionDiagnostics positionDiagnostics)
            {
                Vector2 centerLocal = centerContent == null
                    ? evt.mousePosition
                    : centerContent.WorldToLocal(evt.mousePosition);
                Vector2 viewportLocal = ResolveActiveViewportLocalPosition(centerLocal);
                ESWorkbenchObjectDescriptor positionItem = item
                    ?? (batch != null && batch.Count > 0 ? batch[0] : null);
                if (!positionDiagnostics.TryResolveDropPosition(
                        positionItem,
                        viewportLocal,
                        out resolvedDropPosition,
                        out string positionReason))
                {
                    accepted = false;
                    if (!string.IsNullOrWhiteSpace(positionReason)) reason = positionReason;
                }
                else if (!ESWorkbenchDropPointPolicy.IsFinite(resolvedDropPosition))
                {
                    accepted = false;
                    reason = "当前视口返回了无效的拖放坐标。";
                }
                else hasResolvedDropPosition = true;
            }
            if (!ownsExternalPointer)
                reason = "当前工作台已有其他主指针手势，暂不能接管外部拖放。";
            DragAndDrop.visualMode = accepted ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            ShowDropFeedback(item, batch.Count, evt.mousePosition, accepted, reason);
            UpdateViewportDropPreview(
                item,
                batch,
                evt.mousePosition,
                accepted,
                reason,
                hasResolvedDropPosition,
                resolvedDropPosition);
            UpdateDragEdgePan(item, batch, evt.mousePosition, accepted, reason);
            evt.StopPropagation();
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            if (disposed)
                return;
            externalDragTransferInFlight = false;
            StopDragEdgePan();
            HideDropFeedback();
            CancelWorkbenchDrag(true);
        }

        private void OnDragExited(DragExitedEvent evt)
        {
            externalDragTransferInFlight = false;
            CancelWorkbenchDrag(true);
        }

        private void OnRootPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            CancelWorkbenchDrag(true);
        }

        private void OnRootFocusOut(FocusOutEvent evt)
        {
            CancelWorkbenchDrag(true);
        }

        private void OnRootPointerCancel(PointerCancelEvent evt)
        {
            // 系统取消可能发生在内容卡片、视口或 pane handle 上；根级统一
            // 清理外部拖放和活动视口，避免局部回调漏掉 owner 或 drop 反馈。
            CancelWorkbenchDrag(true);
            CancelActiveViewportInteraction();
        }

        private void OnRootDetachedFromPanel(DetachFromPanelEvent evt)
        {
            // Unity 关闭/重挂载窗口时不保证先发出 DragExited。Panel 脱离是宿主
            // 能观察到的最后生命周期边界，必须幂等释放外部拖放、边缘平移和
            // 当前视口的临时预览，避免旧 owner 阻塞下一次打开的工作台。
            StopDragEdgePan();
            ClearViewportDropPreview();
            externalDragTransferInFlight = false;
            CancelWorkbenchDrag(true);
            DeactivateCurrentViewport();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            NoteExternalDragSignal();
            ESWorkbenchObjectDescriptor item = ResolveDragItem();
            IReadOnlyList<ESWorkbenchObjectDescriptor> batch = ResolveDragBatch();
            // DragPerform can race panel detach/rebind. Resolve the coordinate without
            // dereferencing a torn-down centerContent so the finally block always owns
            // the external-drag release contract.
            Vector2 local = centerContent == null
                ? evt.mousePosition
                : centerContent.WorldToLocal(evt.mousePosition);
            try
            {
                if ((activeDragSessionToken == null && !EnsureExternalPointerOwnership())
                    || !pointerCoordinator.IsExternalContentActive)
                {
                    SetStatus("当前工作台已有其他主指针手势，不能接管外部拖放。", MessageType.Warning);
                    return;
                }
                if (batch.Count > 1)
                {
                    bool placed = TryPlaceBatch(batch, local);
                    if (placed) DragAndDrop.AcceptDrag();
                    return;
                }
                if (!CanViewportAccept(activeViewport, item, out string reason))
                {
                    if (!string.IsNullOrWhiteSpace(reason)) SetStatus(reason, MessageType.Warning);
                    return;
                }
                local = ResolveActiveViewportLocalPosition(local);
                var context = new ESWorkbenchDropContext(
                    actions,
                    item,
                    local,
                    activeViewport?.Root?.contentRect ?? centerContent.contentRect);
                if (activeViewport.TryAccept(context, out string message))
                {
                    DragAndDrop.AcceptDrag();
                    contentUsage.RecordUse(item.BaseObjectId);
                    SetStatus(
                        string.IsNullOrWhiteSpace(message) ? "对象已放入工作区。" : message,
                        actions.Authoring.LastOperationCommittedWithPostCommitFailure
                            ? MessageType.Error : MessageType.Info);
                    RebuildObjectList();
                }
                else SetStatus(string.IsNullOrWhiteSpace(message) ? "当前视口拒绝该对象。" : message, MessageType.Warning);
            }
            finally
            {
                externalDragTransferInFlight = false;
                CancelWorkbenchDrag(true);
                evt.StopPropagation();
            }
        }

        private void ShowDropFeedback(
            ESWorkbenchObjectDescriptor item,
            int batchCount,
            Vector2 mousePosition,
            bool accepted,
            string rejectionReason)
        {
            if (dropFeedback == null || centerContent == null || item == null)
            {
                HideDropFeedback();
                return;
            }
            Vector2 local = centerContent.WorldToLocal(mousePosition);
            dropFeedback.style.left = Mathf.Clamp(local.x + 18f, 8f, Mathf.Max(8f, centerContent.contentRect.width - 350f));
            dropFeedback.style.top = Mathf.Clamp(local.y + 18f, 8f, Mathf.Max(8f, centerContent.contentRect.height - 76f));
            Color statusAccent = accepted
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
            dropFeedback.style.borderLeftColor = statusAccent;
            dropFeedback.style.borderRightColor = Color.Lerp(statusAccent, ESEditorPresentation.DividerColor, 0.68f);
            dropFeedback.style.borderTopColor = Color.Lerp(statusAccent, ESEditorPresentation.DividerColor, 0.72f);
            dropFeedback.style.borderBottomColor = Color.Lerp(statusAccent, ESEditorPresentation.DividerColor, 0.72f);
            dropFeedback.style.backgroundColor = accepted
                ? new Color(0.035f, 0.095f, 0.115f, 0.96f)
                : new Color(0.13f, 0.045f, 0.05f, 0.96f);
            Label status = dropFeedback.Q<Label>("DropStatus");
            status.text = accepted ? "✓" : "×";
            status.style.color = statusAccent;
            Label label = dropFeedback.Q<Label>("DropTitle");
            label.text = accepted
                ? (batchCount > 1
                    ? "释放以批量放置 · " + batchCount + " 项"
                    : ResolveAcceptedDropText(item) + " · " + item.DisplayName)
                : (string.IsNullOrWhiteSpace(rejectionReason)
                    ? "当前视口不能使用 · " + item.DisplayName
                    : rejectionReason);
            label.style.color = ESEditorPresentation.SectionTextColor;
            Label detail = dropFeedback.Q<Label>("DropDetail");
            detail.text = accepted
                ? (batchCount > 1
                    ? "单一 Undo 事务 · 间距 " + Mathf.Max(0.25f, layout.contentBatchSpacing).ToString("0.#")
                    : item.ContentKindDisplayName + " · " + item.Category)
                : "拖放预检未通过，不会修改作者数据";
            Label count = dropFeedback.Q<Label>("DropCount");
            count.text = batchCount > 1 ? batchCount + " 项" : item.ContentKindDisplayName;
            count.style.color = accepted ? statusAccent : ESEditorPresentation.SectionMutedTextColor;
            Image preview = dropFeedback.Q<Image>("DropPreview");
            preview.image = ResolveContentThumbnail(item);
            preview.style.display = preview.image == null ? DisplayStyle.None : DisplayStyle.Flex;
            dropFeedback.style.display = DisplayStyle.Flex;
        }

        internal static bool CanViewportAccept(
            IESWorkbenchViewport viewport,
            ESWorkbenchObjectDescriptor item,
            out string reason)
        {
            reason = string.Empty;
            if (item == null)
            {
                reason = "没有可用的拖放内容。";
                return false;
            }
            if (viewport == null)
            {
                reason = "当前没有可用的作者视口。";
                return false;
            }
            if (viewport is IESWorkbenchViewportDropDiagnostics diagnostics)
            {
                bool accepted = diagnostics.CanAccept(item, out reason);
                if (!accepted && string.IsNullOrWhiteSpace(reason)) reason = "当前视口不能使用该内容。";
                return accepted;
            }
            bool fallbackAccepted = viewport.CanAccept(item);
            if (!fallbackAccepted) reason = "当前视口不能使用该内容。";
            return fallbackAccepted;
        }

        internal static bool CanViewportAcceptBatch(
            IESWorkbenchViewport viewport,
            IReadOnlyList<ESWorkbenchObjectDescriptor> items,
            out string reason)
        {
            reason = string.Empty;
            if (items == null || items.Count <= 1)
            {
                reason = "批量放置至少需要两项内容。";
                return false;
            }
            if (!(viewport is IESWorkbenchBatchViewport batchViewport))
            {
                reason = "当前视口没有注册批量放置合同。";
                return false;
            }
            bool accepted = batchViewport.CanAcceptBatch(items, out reason);
            if (!accepted && string.IsNullOrWhiteSpace(reason)) reason = "当前视口拒绝批量内容。";
            return accepted;
        }

        private void HideDropFeedback()
        {
            if (dropFeedback != null) dropFeedback.style.display = DisplayStyle.None;
            ClearViewportDropPreview();
        }

        private void UpdateViewportDropPreview(
            ESWorkbenchObjectDescriptor item,
            IReadOnlyList<ESWorkbenchObjectDescriptor> batch,
            Vector2 mousePosition,
            bool accepted,
            string reason,
            bool hasResolvedDropPosition = false,
            Vector3 resolvedDropPosition = default)
        {
            if (!(activeViewport is IESWorkbenchDropPreviewViewport previewViewport)
                || centerContent == null)
                return;
            Vector2 local = centerContent.WorldToLocal(mousePosition);
            local = ResolveActiveViewportLocalPosition(local);
            IReadOnlyList<ESWorkbenchObjectDescriptor> items = batch != null && batch.Count > 1
                ? batch
                : item == null ? Array.Empty<ESWorkbenchObjectDescriptor>() : new[] { item };
            previewViewport.UpdateDropPreview(new ESWorkbenchDropPreviewContext(
                actions,
                item,
                items,
                local,
                activeViewport?.Root?.contentRect ?? centerContent.contentRect,
                layout.contentBatchSpacing,
                accepted,
                reason,
                resolvedDropPosition,
                hasResolvedDropPosition));
        }

        private Vector2 ResolveActiveViewportLocalPosition(Vector2 centerLocalPosition)
        {
            if (centerContent == null || activeViewport?.Root == null)
                return centerLocalPosition;
            Vector2 panelPosition = centerContent.LocalToWorld(centerLocalPosition);
            return activeViewport.Root.WorldToLocal(panelPosition);
        }

        private void ClearViewportDropPreview()
        {
            if (activeViewport is IESWorkbenchDropPreviewViewport previewViewport)
            {
                try { previewViewport.ClearDropPreview(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private ESWorkbenchObjectDescriptor ResolveDragItem()
        {
            ESWorkbenchObjectDescriptor internalItem = DragAndDrop.GetGenericData(DragPayloadKey) as ESWorkbenchObjectDescriptor;
            if (internalItem != null) return internalItem;
            UnityEngine.Object[] references = DragAndDrop.objectReferences;
            if (references == null || references.Length == 0) return null;
            for (int i = 0; i < references.Length; i++)
            {
                UnityEngine.Object reference = references[i];
                if (reference != null && contentSourceBySource.TryGetValue(reference, out ESWorkbenchObjectDescriptor item))
                    return item;
            }
            return null;
        }

        private IReadOnlyList<ESWorkbenchObjectDescriptor> ResolveDragBatch()
        {
            IReadOnlyList<ESWorkbenchObjectDescriptor> internalBatch =
                DragAndDrop.GetGenericData(BatchDragPayloadKey) as IReadOnlyList<ESWorkbenchObjectDescriptor>;
            if (internalBatch != null && internalBatch.Count > 0) return internalBatch;
            externalDragBatch.Clear();
            externalDragIds.Clear();
            UnityEngine.Object[] references = DragAndDrop.objectReferences;
            if (references == null || references.Length == 0) return externalDragBatch;
            for (int i = 0; i < references.Length; i++)
            {
                UnityEngine.Object reference = references[i];
                if (reference != null
                    && contentSourceBySource.TryGetValue(reference, out ESWorkbenchObjectDescriptor item)
                    && externalDragIds.Add(item.BaseObjectId))
                    externalDragBatch.Add(item);
            }
            return externalDragBatch;
        }

        private void NoteExternalDragSignal()
        {
            if (!externalDragTransferInFlight) return;
            externalDragLastSignalTime = EditorApplication.timeSinceStartup;
            if (externalDragWatchdogRegistered) return;
            EditorApplication.update += OnExternalDragWatchdog;
            externalDragWatchdogRegistered = true;
        }

        private void StopExternalDragWatchdog()
        {
            if (!externalDragWatchdogRegistered) return;
            EditorApplication.update -= OnExternalDragWatchdog;
            externalDragWatchdogRegistered = false;
        }

        private void OnExternalDragWatchdog()
        {
            if (disposed || !externalDragTransferInFlight || externalDragPayloadToken == null)
            {
                StopExternalDragWatchdog();
                return;
            }
            if (!ReferenceEquals(
                    DragAndDrop.GetGenericData(DragSessionKey),
                    externalDragPayloadToken))
            {
                externalDragTransferInFlight = false;
                CancelWorkbenchDrag(true);
                return;
            }
            if (EditorApplication.timeSinceStartup - externalDragLastSignalTime
                < ExternalDragWatchdogTimeoutSeconds)
                return;

            // Unity may omit DragExited when the native drag leaves an editor
            // surface. The handoff grace period is over; force the same terminal
            // cancellation contract so the next gesture cannot inherit stale data.
            externalDragTransferInFlight = false;
            CancelWorkbenchDrag(true);
        }

        private void CancelWorkbenchDrag(bool clearObjectReferences)
        {
            bool preserveNativePayload = externalDragTransferInFlight
                && externalDragPayloadToken != null
                && ReferenceEquals(
                    DragAndDrop.GetGenericData(DragSessionKey),
                    externalDragPayloadToken);
            StopDragEdgePan();
            HideDropFeedback();
            externalDragBatch.Clear();
            externalDragIds.Clear();
            pointerCoordinator.EndExternalContent(activeDragSessionToken);
            pointerCoordinator.EndExternalContent(externalPointerSessionToken);
            externalPointerSessionToken = null;
            // PointerCaptureOut/FocusOut can be emitted while StartDrag hands
            // control to Unity. Release the UI owner, but retain the native payload
            // until DragPerform/DragExited reaches a terminal path.
            activeDragSessionToken = null;
            if (preserveNativePayload)
            {
                NoteExternalDragSignal();
            }
            else
            {
                StopExternalDragWatchdog();
                ClearOwnedDragPayload(clearObjectReferences);
                externalDragPayloadToken = null;
                externalDragTransferInFlight = false;
            }
        }

        private bool EnsureExternalPointerOwnership()
        {
            if (activeDragSessionToken != null)
                return pointerCoordinator.IsExternalContentActive;
            if (externalPointerSessionToken != null)
                return pointerCoordinator.IsExternalContentActive;
            object token = new object();
            if (!pointerCoordinator.TryBeginExternalContent(token)) return false;
            externalPointerSessionToken = token;
            return true;
        }

        private void UpdateDragEdgePan(
            ESWorkbenchObjectDescriptor item,
            IReadOnlyList<ESWorkbenchObjectDescriptor> batch,
            Vector2 mousePosition,
            bool accepted,
            string reason)
        {
            if (!accepted || centerContent == null
                || !(activeViewport is IESWorkbenchEdgePannableViewport edgeViewport))
            {
                StopDragEdgePan();
                return;
            }
            Vector2 centerLocal = centerContent.WorldToLocal(mousePosition);
            Vector2 local = ResolveActiveViewportLocalPosition(centerLocal);
            dragEdgePanViewport = edgeViewport;
            double now = EditorApplication.timeSinceStartup;
            if (!dragEdgePanSession.IsActive)
                dragEdgePanSession.Begin(local, false, now);
            else
                dragEdgePanSession.UpdatePointer(local, false);
            dragEdgePanMousePosition = mousePosition;
            dragEdgePanItem = item;
            dragEdgePanBatch = batch;
            dragEdgePanAccepted = accepted;
            dragEdgePanReason = reason ?? string.Empty;
            if (dragEdgePanSchedule == null)
            {
                dragEdgePanSchedule = centerContent.schedule.Execute(ApplyDragEdgePan).Every(16);
                dragEdgePanSchedule.Pause();
            }
            if (!dragEdgePanSchedule.isActive)
            {
                dragEdgePanSchedule.Resume();
            }
        }

        private void ApplyDragEdgePan()
        {
            if (dragEdgePanViewport == null || centerContent == null
                || !pointerCoordinator.IsExternalContentActive)
            {
                StopDragEdgePan();
                ClearViewportDropPreview();
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            if (!dragEdgePanSession.TryAdvance(now, out float deltaTime)) return;
            if (!dragEdgePanViewport.TryEdgePan(
                    dragEdgePanSession.Pointer, deltaTime)) return;
            UpdateViewportDropPreview(
                dragEdgePanItem,
                dragEdgePanBatch,
                dragEdgePanMousePosition,
                dragEdgePanAccepted,
                dragEdgePanReason);
        }

        private void StopDragEdgePan()
        {
            dragEdgePanSchedule?.Pause();
            dragEdgePanSchedule = null;
            dragEdgePanSession.Stop();
            dragEdgePanViewport = null;
            dragEdgePanItem = null;
            dragEdgePanBatch = null;
            dragEdgePanAccepted = false;
            dragEdgePanReason = string.Empty;
        }

        private void ClearOwnedDragPayload(bool clearObjectReferences)
        {
            object payloadToken = activeDragSessionToken ?? externalDragPayloadToken;
            if (payloadToken == null)
            {
                StopExternalDragWatchdog();
                return;
            }
            object currentToken = DragAndDrop.GetGenericData(DragSessionKey);
            if (!ReferenceEquals(currentToken, payloadToken))
            {
                // A new source gesture installs activeDragSessionToken before
                // clearing a stale native payload. Do not erase that new token
                // merely because the global DragAndDrop slot still belongs to
                // the previous/another host; otherwise StartDrag writes a null
                // session key and the next drop can never resolve its payload.
                if (!ReferenceEquals(payloadToken, activeDragSessionToken))
                    activeDragSessionToken = null;
                if (ReferenceEquals(payloadToken, externalDragPayloadToken))
                    externalDragPayloadToken = null;
                externalDragTransferInFlight = false;
                StopExternalDragWatchdog();
                return;
            }
            DragAndDrop.SetGenericData(DragSessionKey, null);
            DragAndDrop.SetGenericData(DragPayloadKey, null);
            DragAndDrop.SetGenericData(BatchDragPayloadKey, null);
            if (clearObjectReferences) DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
            activeDragSessionToken = null;
            externalDragPayloadToken = null;
            externalDragTransferInFlight = false;
            StopExternalDragWatchdog();
        }

        private void RebuildObjectList(bool refreshSource = true)
        {
            IReadOnlyList<ESWorkbenchObjectDescriptor> resolvedSource = null;
            if (refreshSource)
            {
                try
                {
                    resolvedSource = getObjects?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "ES 工作台对象 Provider 刷新失败，已保留上一次有效列表。", exception));
                    return;
                }
            }
            contentPointerGate.Reset();
            pointerCoordinator.ResetIfOwnerKind(ESWorkbenchPointerOwnerKind.Content);
            visibleObjects.Clear();
            if (refreshSource)
            {
                contentSourceSnapshot.Clear();
                contentSourceById.Clear();
                contentSourceBySource.Clear();
                duplicateContentIdCount = 0;
                if (resolvedSource != null)
                {
                    for (int i = 0; i < resolvedSource.Count; i++)
                    {
                        ESWorkbenchObjectDescriptor item = resolvedSource[i];
                        if (item == null) continue;
                        if (contentSourceById.ContainsKey(item.BaseObjectId))
                        {
                            duplicateContentIdCount++;
                            continue;
                        }
                        contentSourceById.Add(item.BaseObjectId, item);
                        if (item.Source != null && !contentSourceBySource.ContainsKey(item.Source))
                            contentSourceBySource.Add(item.Source, item);
                        contentSourceSnapshot.Add(item);
                    }
                    var validIds = new HashSet<string>(
                        contentSourceById.Keys,
                        StringComparer.Ordinal);
                    batchContentIds.RemoveWhere(value => !validIds.Contains(value));
                    string[] stalePresetIds = selectedPresetByObjectId.Keys
                        .Where(value => !validIds.Contains(value))
                        .ToArray();
                    foreach (string staleId in stalePresetIds) selectedPresetByObjectId.Remove(staleId);
                    PersistBatchContentSelection();
                }
            }
            IReadOnlyList<ESWorkbenchObjectDescriptor> source = contentSourceSnapshot;
            string query = objectSearch?.value ?? string.Empty;
            if (source.Count > 0)
            {
                IEnumerable<ESWorkbenchObjectDescriptor> filtered = source.Where(item => item != null
                    && MatchesContentKind(item, contentKindFilter)
                    && MatchesContentCategory(item, categoryFilter)
                    && MatchesContentScope(item, contentScope, contentUsage)
                    && (string.IsNullOrWhiteSpace(query)
                        || item.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                        || item.BaseObjectId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                        || item.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                        || item.ContentKindDisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                        || item.Subtitle.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
                filtered = OrderContent(filtered, contentSortMode, contentUsage);
                if (contentScope == ESWorkbenchContentScope.Recommended) filtered = filtered.Take(24);
                visibleObjects.AddRange(filtered);
            }
            BuildContentKindTabs(source);
            BuildContentCategoryTree(source);
            BuildCompactContentFilterMenu();
            RebuildContentGridRows();
            objectList?.Rebuild();
            objectGridList?.Rebuild();
            if (objectEmptyLabel != null) objectEmptyLabel.style.display = visibleObjects.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            ApplyContentViewMode();
            UpdateContentScopeButtons();
            UpdateBatchPlaceButton();
            if (contentSummaryLabel != null)
            {
                int total = source.Count;
                int favorites = source.Count(value => contentUsage.IsFavorite(value.BaseObjectId));
                contentSummaryLabel.text = "显示 " + visibleObjects.Count + " / " + total
                    + " · 收藏 " + favorites
                    + " · 批量选择 " + ResolveSelectedBatchItems().Count
                    + (duplicateContentIdCount > 0 ? " · 已去重 " + duplicateContentIdCount : string.Empty);
            }
            RestoreStableSelection(source);
            SynchronizeListSelection(actions.Selection.CurrentSet);
        }

        internal static bool MatchesContentKind(ESWorkbenchObjectDescriptor item, string filter)
        {
            if (item == null) return false;
            if (string.IsNullOrWhiteSpace(filter) || string.Equals(filter, "all", StringComparison.Ordinal)) return true;
            return string.Equals(filter, item.ContentKind.ToString(), StringComparison.Ordinal);
        }

        internal static bool MatchesContentCategory(ESWorkbenchObjectDescriptor item, string filter)
        {
            if (item == null) return false;
            if (string.IsNullOrWhiteSpace(filter) || string.Equals(filter, "全部", StringComparison.Ordinal)) return true;
            return string.Equals(item.Category, filter, StringComparison.Ordinal)
                || item.Category.StartsWith(filter + "/", StringComparison.Ordinal);
        }

        private static bool MatchesContentScope(
            ESWorkbenchObjectDescriptor item,
            ESWorkbenchContentScope scope,
            ESWorkbenchContentUsageStore usage)
        {
            if (item == null) return false;
            ESWorkbenchContentUsageRecord record = usage.Get(item.BaseObjectId);
            switch (scope)
            {
                case ESWorkbenchContentScope.Favorites: return record?.favorite == true;
                case ESWorkbenchContentScope.Recent: return (record?.lastUsedUtcTicks ?? 0L) > 0L;
                default: return true;
            }
        }

        private static IEnumerable<ESWorkbenchObjectDescriptor> OrderContent(
            IEnumerable<ESWorkbenchObjectDescriptor> source,
            ESWorkbenchContentSortMode mode,
            ESWorkbenchContentUsageStore usage)
        {
            switch (mode)
            {
                case ESWorkbenchContentSortMode.Type:
                    return source.OrderBy(item => ResolveContentKindOrder(item.ContentKind))
                        .ThenBy(item => item.Category, StringComparer.Ordinal)
                        .ThenByDescending(item => item.Priority)
                        .ThenBy(item => item.DisplayName, StringComparer.Ordinal);
                case ESWorkbenchContentSortMode.Priority:
                    return source.OrderByDescending(item => item.Priority)
                        .ThenBy(item => item.ContentKind)
                        .ThenBy(item => item.Category, StringComparer.Ordinal)
                        .ThenBy(item => item.DisplayName, StringComparer.Ordinal);
                case ESWorkbenchContentSortMode.Name:
                    return source.OrderBy(item => item.DisplayName, StringComparer.Ordinal)
                        .ThenBy(item => item.BaseObjectId, StringComparer.Ordinal);
                case ESWorkbenchContentSortMode.Recent:
                    return source.OrderByDescending(item => usage.Get(item.BaseObjectId)?.lastUsedUtcTicks ?? 0L)
                        .ThenByDescending(item => item.Priority)
                        .ThenBy(item => item.DisplayName, StringComparer.Ordinal);
                case ESWorkbenchContentSortMode.MostUsed:
                    return source.OrderByDescending(item => usage.Get(item.BaseObjectId)?.useCount ?? 0)
                        .ThenByDescending(item => usage.Get(item.BaseObjectId)?.lastUsedUtcTicks ?? 0L)
                        .ThenByDescending(item => item.Priority)
                        .ThenBy(item => item.DisplayName, StringComparer.Ordinal);
                default:
                    return source.OrderByDescending(item => usage.Get(item.BaseObjectId)?.favorite == true)
                        .ThenByDescending(item => usage.Get(item.BaseObjectId)?.useCount ?? 0)
                        .ThenByDescending(item => usage.Get(item.BaseObjectId)?.lastUsedUtcTicks ?? 0L)
                        .ThenByDescending(item => item.Priority)
                        .ThenBy(item => item.DisplayName, StringComparer.Ordinal);
            }
        }

        private void BuildContentKindTabs(IReadOnlyList<ESWorkbenchObjectDescriptor> source)
        {
            contentKindTabs.Clear();
            contentKindTabs.Add(new ContentKindTabItem
            {
                id = "all",
                label = "全部",
                count = source?.Count(value => value != null) ?? 0
            });
            if (source != null)
            {
                foreach (IGrouping<ESWorkbenchContentKind, ESWorkbenchObjectDescriptor> group in source
                    .Where(value => value != null)
                    .GroupBy(value => value.ContentKind)
                    .OrderBy(value => ResolveContentKindOrder(value.Key)))
                {
                    ESWorkbenchObjectDescriptor sample = group.First();
                    contentKindTabs.Add(new ContentKindTabItem
                    {
                        id = group.Key.ToString(),
                        label = sample.ContentKindDisplayName,
                        count = group.Count()
                    });
                }
            }
            if (!contentKindTabs.Exists(value => string.Equals(value.id, contentKindFilter, StringComparison.Ordinal)))
            {
                contentKindFilter = "all";
                layout.activeContentKind = "all";
            }
            BuildContentKindQuickBar();
        }

        private static int ResolveContentKindOrder(ESWorkbenchContentKind kind)
        {
            switch (kind)
            {
                case ESWorkbenchContentKind.Prefab: return 0;
                case ESWorkbenchContentKind.Brush: return 10;
                case ESWorkbenchContentKind.SceneTemplate: return 20;
                case ESWorkbenchContentKind.RegionTemplate: return 30;
                case ESWorkbenchContentKind.Terrain: return 40;
                case ESWorkbenchContentKind.Vegetation: return 50;
                case ESWorkbenchContentKind.Gameplay: return 60;
                default: return 100;
            }
        }

        private static Color ResolveContentKindAccent(ESWorkbenchContentKind kind)
        {
            switch (kind)
            {
                case ESWorkbenchContentKind.Brush:
                case ESWorkbenchContentKind.Terrain:
                    return new Color(0.35f, 0.72f, 0.42f, 1f);
                case ESWorkbenchContentKind.SceneTemplate:
                    return new Color(0.54f, 0.62f, 0.95f, 1f);
                case ESWorkbenchContentKind.RegionTemplate:
                    return new Color(0.94f, 0.64f, 0.25f, 1f);
                case ESWorkbenchContentKind.Vegetation:
                    return new Color(0.3f, 0.7f, 0.5f, 1f);
                case ESWorkbenchContentKind.Gameplay:
                    return new Color(0.88f, 0.42f, 0.48f, 1f);
                default:
                    return ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            }
        }

        private static string ResolveAcceptedDropText(ESWorkbenchObjectDescriptor item)
        {
            switch (item.DragMode)
            {
                case ESWorkbenchContentDragMode.ActivateTool: return "释放以使用";
                case ESWorkbenchContentDragMode.ApplyTemplate: return "释放以应用";
                case ESWorkbenchContentDragMode.CreateRegion: return "释放以创建";
                default: return "释放以放置";
            }
        }

        private void RebuildContentGridRows()
        {
            visibleGridRows.Clear();
            int columns = Mathf.Clamp(contentGridColumns, 1, 2);
            for (int i = 0; i < visibleObjects.Count; i += columns)
            {
                visibleGridRows.Add(new ContentGridRow
                {
                    first = visibleObjects[i],
                    second = columns > 1 && i + 1 < visibleObjects.Count ? visibleObjects[i + 1] : null
                });
            }
        }

        private void SetContentViewMode(ESWorkbenchContentViewMode mode)
        {
            if (contentViewMode == mode) return;
            contentViewMode = mode;
            layout.contentViewMode = mode;
            if (contentBrowser != null)
                ApplyContentBrowserResponsive(contentBrowser.resolvedStyle.width);
            ApplyContentViewMode();
            BuildContentViewMenu();
        }

        private void ApplyContentViewMode()
        {
            bool hasItems = visibleObjects.Count > 0;
            bool list = contentViewMode == ESWorkbenchContentViewMode.List;
            if (objectList != null) objectList.style.display = hasItems && list ? DisplayStyle.Flex : DisplayStyle.None;
            if (objectGridList != null) objectGridList.style.display = hasItems && !list ? DisplayStyle.Flex : DisplayStyle.None;
            if (listModeButton != null)
            {
                listModeButton.style.backgroundColor = list
                    ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                    : ESEditorPresentation.ControlSurfaceColor;
                listModeButton.style.color = list ? Color.white : ESEditorPresentation.SectionTextColor;
            }
            if (gridModeButton != null)
            {
                gridModeButton.style.backgroundColor = !list
                    ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                    : ESEditorPresentation.ControlSurfaceColor;
                gridModeButton.style.color = !list ? Color.white : ESEditorPresentation.SectionTextColor;
            }
            BuildContentViewMenu();
        }

        private void SetContentKind(string id)
        {
            string next = string.IsNullOrWhiteSpace(id) ? "all" : id;
            if (string.Equals(contentKindFilter, next, StringComparison.Ordinal)) return;
            contentKindFilter = next;
            layout.activeContentKind = next;
            categoryFilter = "全部";
            layout.activeContentCategory = "全部";
            RebuildObjectList(refreshSource: false);
            BuildCompactContentFilterMenu();
        }

        private void SetCategory(string category)
        {
            categoryFilter = string.IsNullOrWhiteSpace(category) ? "全部" : category;
            layout.activeContentCategory = categoryFilter;
            RebuildObjectList(refreshSource: false);
            BuildCompactContentFilterMenu();
        }

        private void RebuildHierarchyList()
        {
            visibleHierarchy.Clear();
            hierarchyById.Clear();
            hierarchyChildren.Clear();
            IReadOnlyList<ESWorkbenchHierarchyDescriptor> source = getHierarchy?.Invoke();
            if (source != null)
            {
                foreach (ESWorkbenchHierarchyDescriptor item in source.Where(value => value != null))
                    if (!hierarchyById.ContainsKey(item.ItemId)) hierarchyById.Add(item.ItemId, item);
                foreach (ESWorkbenchHierarchyDescriptor item in hierarchyById.Values)
                {
                    string parentId = !string.IsNullOrEmpty(item.ParentId) && hierarchyById.ContainsKey(item.ParentId)
                        ? item.ParentId
                        : string.Empty;
                    if (!hierarchyChildren.TryGetValue(parentId, out List<ESWorkbenchHierarchyDescriptor> children))
                    {
                        children = new List<ESWorkbenchHierarchyDescriptor>();
                        hierarchyChildren.Add(parentId, children);
                    }
                    children.Add(item);
                }
                foreach (List<ESWorkbenchHierarchyDescriptor> children in hierarchyChildren.Values)
                    children.Sort(CompareHierarchy);
                HashSet<string> visibleFilter = BuildHierarchyFilter();
                if (hierarchyChildren.TryGetValue(string.Empty, out List<ESWorkbenchHierarchyDescriptor> roots))
                {
                    if (!hierarchyExpansionInitialized)
                    {
                        if (expandedHierarchyIds.Count == 0)
                            for (int i = 0; i < roots.Count; i++) expandedHierarchyIds.Add(roots[i].ItemId);
                        hierarchyExpansionInitialized = true;
                        layout.hierarchyExpansionInitialized = true;
                    }
                    for (int i = 0; i < roots.Count; i++)
                        AppendVisibleHierarchy(roots[i], new HashSet<string>(StringComparer.Ordinal), visibleFilter);
                }
            }
            hierarchyList?.Rebuild();
            if (hierarchyEmptyLabel != null) hierarchyEmptyLabel.style.display = visibleHierarchy.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (hierarchyList != null) hierarchyList.style.display = visibleHierarchy.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            RestoreStableSelection();
            SynchronizeListSelection(actions.Selection.CurrentSet);
        }

        private void RestoreStableSelection(IReadOnlyList<ESWorkbenchObjectDescriptor> contentSource = null)
        {
            if (string.IsNullOrWhiteSpace(layout.selectedStableId)) return;
            if (hierarchyById.TryGetValue(layout.selectedStableId, out ESWorkbenchHierarchyDescriptor hierarchyItem))
            {
                if (string.IsNullOrEmpty(layout.selectedKind) || hierarchyItem.Kind == layout.selectedKind)
                    actions.Selection.Select(hierarchyItem.ToSelection());
                return;
            }
            contentSource ??= contentSourceSnapshot;
            ESWorkbenchObjectDescriptor content = contentSource?.FirstOrDefault(value =>
                value != null
                && value.BaseObjectId == layout.selectedStableId
                && (string.IsNullOrEmpty(layout.selectedKind) || value.SelectionKind == layout.selectedKind));
            if (content != null) actions.Selection.Select(GetEffectiveDescriptor(content).ToSelection());
        }

        private HashSet<string> BuildHierarchyFilter()
        {
            string query = hierarchySearch?.value;
            if (string.IsNullOrWhiteSpace(query)) return null;
            var visible = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESWorkbenchHierarchyDescriptor item in hierarchyById.Values)
            {
                if (item.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                    && item.ItemId.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                    && item.Kind.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string current = item.ItemId;
                var visited = new HashSet<string>(StringComparer.Ordinal);
                while (!string.IsNullOrEmpty(current) && visited.Add(current)
                    && hierarchyById.TryGetValue(current, out ESWorkbenchHierarchyDescriptor currentItem))
                {
                    visible.Add(current);
                    expandedHierarchyIds.Add(current);
                    current = currentItem.ParentId;
                }
            }
            return visible;
        }

        private static int CompareHierarchy(ESWorkbenchHierarchyDescriptor left, ESWorkbenchHierarchyDescriptor right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.Compare(left.ItemId, right.ItemId, StringComparison.Ordinal);
        }

        private void AppendVisibleHierarchy(
            ESWorkbenchHierarchyDescriptor item,
            HashSet<string> path,
            HashSet<string> visibleFilter)
        {
            if (item == null || (visibleFilter != null && !visibleFilter.Contains(item.ItemId)) || !path.Add(item.ItemId)) return;
            visibleHierarchy.Add(item);
            if (expandedHierarchyIds.Contains(item.ItemId)
                && hierarchyChildren.TryGetValue(item.ItemId, out List<ESWorkbenchHierarchyDescriptor> children))
            {
                for (int i = 0; i < children.Count; i++) AppendVisibleHierarchy(children[i], path, visibleFilter);
            }
            path.Remove(item.ItemId);
        }

        private VisualElement CreateHierarchyRow()
        {
            VisualElement row = new VisualElement { name = "ESWorkbenchHierarchyRow" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            Label foldout = new Label { name = "Foldout" };
            foldout.style.width = 17f;
            foldout.style.unityTextAlign = TextAnchor.MiddleCenter;
            foldout.style.flexShrink = 0f;
            foldout.RegisterCallback<PointerDownEvent>(evt =>
            {
                ESWorkbenchHierarchyDescriptor item = row.userData as ESWorkbenchHierarchyDescriptor;
                if (item == null || !hierarchyChildren.ContainsKey(item.ItemId)) return;
                ToggleHierarchy(item.ItemId);
                evt.StopImmediatePropagation();
            });
            row.Add(foldout);
            Image icon = new Image { name = "Icon", scaleMode = ScaleMode.ScaleToFit };
            icon.style.width = 18f;
            icon.style.height = 18f;
            icon.style.flexShrink = 0f;
            icon.style.marginRight = 4f;
            row.Add(icon);
            Label label = new Label { name = "Title" };
            label.style.flexGrow = 1f;
            label.style.minWidth = 0f;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(label);
            Button visibility = CreateHierarchyStateButton("Visibility", "显示或隐藏该对象及其子项");
            visibility.clicked += () =>
            {
                if (row.userData is ESWorkbenchHierarchyDescriptor item) ToggleHierarchyVisibility(item.ItemId);
            };
            row.Add(visibility);
            Button locking = CreateHierarchyStateButton("Locking", "锁定或解锁该对象及其子项");
            locking.clicked += () =>
            {
                if (row.userData is ESWorkbenchHierarchyDescriptor item) ToggleHierarchyLock(item.ItemId);
            };
            row.Add(locking);
            row.RegisterCallback<ContextClickEvent>(evt =>
            {
                ESWorkbenchHierarchyDescriptor item = row.userData as ESWorkbenchHierarchyDescriptor;
                if (item == null) return;
                actions.Selection.Select(item.ToSelection());
                ShowSelectionContextMenu();
                evt.StopPropagation();
            });
            return row;
        }

        private void BindHierarchyRow(VisualElement element, int index)
        {
            ESWorkbenchHierarchyDescriptor item = visibleHierarchy[index];
            element.userData = item;
            int depth = ResolveHierarchyDepth(item);
            element.style.paddingLeft = 5f + depth * 14f;
            bool hasChildren = hierarchyChildren.TryGetValue(item.ItemId, out List<ESWorkbenchHierarchyDescriptor> children)
                && children.Count > 0;
            Label foldout = element.Q<Label>("Foldout");
            foldout.text = hasChildren ? (expandedHierarchyIds.Contains(item.ItemId) ? "▼" : "▶") : string.Empty;
            Image icon = element.Q<Image>("Icon");
            icon.image = item.Icon != null ? item.Icon : AssetPreview.GetMiniThumbnail(item.UnityObject);
            Label title = element.Q<Label>("Title");
            title.text = item.DisplayName;
            Button visibility = element.Q<Button>("Visibility");
            bool visible = IsHierarchyVisible(item.ItemId);
            UpdateHierarchyStateButton(
                visibility,
                visible ? "d_scenevis_visible_hover" : "d_scenevis_hidden_hover",
                !visible,
                visible ? "当前可见，点击隐藏" : "当前隐藏，点击显示");
            Button locking = element.Q<Button>("Locking");
            bool locked = IsHierarchyLocked(item.ItemId);
            UpdateHierarchyStateButton(
                locking,
                locked ? "d_IN LockButton on" : "d_IN LockButton",
                locked,
                locked ? "当前已锁定，点击解锁" : "当前可编辑，点击锁定");
            title.style.color = visible
                ? ESEditorPresentation.SectionTextColor
                : ESEditorPresentation.SectionMutedTextColor;
            element.tooltip = item.ItemId;
        }

        private static Button CreateHierarchyStateButton(string name, string tooltip)
        {
            var button = new Button { name = name, tooltip = tooltip };
            button.style.width = 25f;
            button.style.height = 21f;
            button.style.flexShrink = 0f;
            button.style.paddingLeft = 4f;
            button.style.paddingRight = 4f;
            var icon = new Image
            {
                name = "StateIcon",
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.style.width = 14f;
            icon.style.height = 14f;
            button.Add(icon);
            return button;
        }

        private static void UpdateHierarchyStateButton(
            Button button,
            string iconName,
            bool selected,
            string tooltip)
        {
            if (button == null) return;
            Image icon = button.Q<Image>("StateIcon");
            if (icon != null) icon.image = EditorGUIUtility.IconContent(iconName)?.image;
            button.tooltip = tooltip;
            ESWindowPresentation.SetButtonPresentationState(
                button,
                selected
                    ? ESEditorPresentation.ESPresentationState.Selected
                    : ESEditorPresentation.ESPresentationState.Normal);
        }

        private void ToggleHierarchyVisibility(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            if (!hiddenHierarchyIds.Add(itemId)) hiddenHierarchyIds.Remove(itemId);
            layout.hiddenHierarchyIds.Clear();
            layout.hiddenHierarchyIds.AddRange(hiddenHierarchyIds.OrderBy(value => value, StringComparer.Ordinal));
            hierarchyList?.RefreshItems();
            activeViewport?.Refresh(ESWorkbenchRefreshReason.Explicit);
            SetStatus(IsHierarchyVisible(itemId) ? "对象已恢复显示。" : "对象已在作者视口中隐藏。", MessageType.Info);
        }

        private void ToggleHierarchyLock(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            if (!lockedHierarchyIds.Add(itemId)) lockedHierarchyIds.Remove(itemId);
            layout.lockedHierarchyIds.Clear();
            layout.lockedHierarchyIds.AddRange(lockedHierarchyIds.OrderBy(value => value, StringComparer.Ordinal));
            hierarchyList?.RefreshItems();
            RebuildInspector(actions.Selection.Current);
            activeViewport?.Refresh(ESWorkbenchRefreshReason.Explicit);
            SetStatus(IsHierarchyLocked(itemId) ? "对象已锁定，视口变换将被阻止。" : "对象已解锁。", MessageType.Info);
        }

        private bool IsHierarchyVisible(string itemId)
        {
            return !IsHierarchyStateInherited(itemId, hiddenHierarchyIds);
        }

        private bool IsHierarchyLocked(string itemId)
        {
            return IsHierarchyStateInherited(itemId, lockedHierarchyIds);
        }

        private bool IsHierarchyStateInherited(string itemId, HashSet<string> values)
        {
            string current = itemId;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
            {
                if (values.Contains(current)) return true;
                current = hierarchyById.TryGetValue(current, out ESWorkbenchHierarchyDescriptor item)
                    ? item.ParentId
                    : string.Empty;
            }
            return false;
        }

        private IReadOnlyList<ESWorkbenchHierarchyDescriptor> GetVisibleViewportHierarchy()
        {
            IReadOnlyList<ESWorkbenchHierarchyDescriptor> source = getHierarchy?.Invoke();
            return source == null
                ? Array.Empty<ESWorkbenchHierarchyDescriptor>()
                : source.Where(item => item != null && IsHierarchyVisible(item.ItemId)).ToArray();
        }

        private int ResolveHierarchyDepth(ESWorkbenchHierarchyDescriptor item)
        {
            int depth = 0;
            string parentId = item?.ParentId;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrEmpty(parentId) && hierarchyById.TryGetValue(parentId, out ESWorkbenchHierarchyDescriptor parent)
                && visited.Add(parentId) && depth < 32)
            {
                depth++;
                parentId = parent.ParentId;
            }
            return depth;
        }

        private void ToggleHierarchy(string itemId)
        {
            if (!expandedHierarchyIds.Add(itemId)) expandedHierarchyIds.Remove(itemId);
            PersistExpandedHierarchy();
            RebuildHierarchyList();
        }

        private void ExpandAllHierarchy()
        {
            expandedHierarchyIds.UnionWith(hierarchyById.Keys);
            PersistExpandedHierarchy();
            RebuildHierarchyList();
        }

        private void CollapseHierarchy()
        {
            expandedHierarchyIds.Clear();
            PersistExpandedHierarchy();
            RebuildHierarchyList();
        }

        private void PersistExpandedHierarchy()
        {
            layout.expandedHierarchyIds.Clear();
            layout.expandedHierarchyIds.AddRange(expandedHierarchyIds.OrderBy(value => value, StringComparer.Ordinal));
        }

        private void AddDocumentTab(string id, string label, string tooltip)
        {
            var toggle = new ToolbarToggle { text = label, tooltip = tooltip, value = activeDocument == id };
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) ShowDocument(id);
                else if (activeDocument == id) toggle.SetValueWithoutNotify(true);
            });
            toggle.userData = id;
            documentTabs.Add(toggle);
        }

        private void ShowDocument(string id)
        {
            bool previousHostsViewport = GetActiveDocument()?.hostsAuthoringViewport == true;
            ESWorkbenchDocumentDefinition document = getDocuments?.Invoke()?.FirstOrDefault(value => value != null
                && value.documentId == id && (value.isAvailable == null || value.isAvailable()));
            if (document == null) return;
            activeDocument = id;
            layout.activeDocument = id;
            documentTabs?.Query<ToolbarToggle>().ForEach(toggle => toggle.SetValueWithoutNotify((string)toggle.userData == id));
            centerContent?.Clear();
            if (centerContent == null) return;
            bool showViewport = document.hostsAuthoringViewport;
            viewportModeBar.style.display = showViewport ? DisplayStyle.Flex : DisplayStyle.None;
            if (toolRail != null) toolRail.style.display = showViewport ? DisplayStyle.Flex : DisplayStyle.None;
            if (viewportFooter != null) viewportFooter.style.display = showViewport ? DisplayStyle.Flex : DisplayStyle.None;
            selectDocument?.Invoke(document.documentId);
            if (showViewport)
            {
                if (BlocksAuthoringViewport)
                {
                    DeactivateCurrentViewport();
                    centerContent.Add(CreateAuthoringEmptyState());
                    return;
                }
                ActivateCurrentViewportIfVisible();
                if (activeViewport != null) centerContent.Add(activeViewport.Root);
                return;
            }
            if (previousHostsViewport || activeViewportActivated) DeactivateCurrentViewport();
            VisualElement view = createDocumentView?.Invoke(document);
            if (view != null) centerContent.Add(view);
        }

        private VisualElement CreateAuthoringEmptyState()
        {
            ESWorkbenchEmptyStateDescriptor descriptor = presentation?.EmptyState
                ?? new ESWorkbenchEmptyStateDescriptor(
                    "尚未绑定作者资产",
                    "选择或创建资产后，工作台才会启动正式作者视口。");
            var surface = new VisualElement { name = "ESWorkbenchAuthoringEmptyState" };
            surface.style.flexGrow = 1f;
            surface.style.alignItems = Align.Center;
            surface.style.justifyContent = Justify.Center;
            surface.style.paddingLeft = 24f;
            surface.style.paddingRight = 24f;
            surface.style.paddingTop = 24f;
            surface.style.paddingBottom = 24f;

            var card = new VisualElement { name = "ESWorkbenchAuthoringStartCard" };
            card.style.width = Length.Percent(100f);
            card.style.maxWidth = 620f;
            card.style.paddingLeft = 24f;
            card.style.paddingRight = 24f;
            card.style.paddingTop = 22f;
            card.style.paddingBottom = 20f;
            ESEditorPresentation.ApplyPresentationStyle(
                card,
                ESEditorPresentation.ESPresentationRole.RaisedSurface,
                borderWidth: 1f);
            surface.Add(card);

            var state = new Label("准备开始") { name = "ESWorkbenchAuthoringEmptyStateBadge" };
            state.style.fontSize = 10f;
            state.style.unityFontStyleAndWeight = FontStyle.Bold;
            state.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Info);
            card.Add(state);
            var title = new Label(descriptor.Title) { name = "ESWorkbenchAuthoringEmptyStateTitle" };
            title.AddToClassList("es-brand-title");
            title.style.fontSize = 20f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginTop = 7f;
            title.style.whiteSpace = WhiteSpace.Normal;
            card.Add(title);
            var description = new Label(descriptor.Description)
            {
                name = "ESWorkbenchAuthoringEmptyStateDescription"
            };
            description.style.marginTop = 8f;
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.color = ESEditorPresentation.SectionMutedTextColor;
            card.Add(description);

            var buttons = new VisualElement { name = "ESWorkbenchAuthoringEmptyStateActions" };
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.marginTop = 16f;
            AddEmptyStateCommand(buttons, descriptor.PrimaryCommandId, true);
            AddEmptyStateCommand(buttons, descriptor.SecondaryCommandId, false);
            card.Add(buttons);
            if (!string.IsNullOrWhiteSpace(descriptor.Footnote))
            {
                var footnote = new Label(descriptor.Footnote)
                {
                    name = "ESWorkbenchAuthoringEmptyStateFootnote"
                };
                footnote.style.marginTop = 14f;
                footnote.style.fontSize = 10f;
                footnote.style.whiteSpace = WhiteSpace.Normal;
                footnote.style.color = ESEditorPresentation.SectionMutedTextColor;
                card.Add(footnote);
            }
            return surface;
        }

        private void AddEmptyStateCommand(VisualElement parent, string commandId, bool primary)
        {
            if (parent == null || string.IsNullOrWhiteSpace(commandId))
                return;
            ESWorkbenchCommandDescriptor command = getCommands?.Invoke()?.FirstOrDefault(
                value => value != null && string.Equals(value.CommandId, commandId, StringComparison.Ordinal));
            if (command == null)
                return;
            Button button = CreateActionButton(
                ResolveCommandIcon(command),
                command.DisplayName,
                command.Tooltip,
                () => ExecuteCommand(command));
            button.name = primary
                ? "ESWorkbenchAuthoringEmptyStatePrimary"
                : "ESWorkbenchAuthoringEmptyStateSecondary";
            button.style.minWidth = primary ? 132f : 116f;
            button.style.height = 32f;
            button.style.marginRight = 8f;
            ApplyCommandPresentation(
                button,
                primary ? ESWorkbenchCommandRole.Primary : command.Role);
            button.SetEnabled(command.CanExecute == null || command.CanExecute(actions));
            parent.Add(button);
        }

        private ESWorkbenchDocumentDefinition GetActiveDocument()
        {
            return getDocuments?.Invoke()?.FirstOrDefault(value => value != null
                && value.documentId == activeDocument);
        }

        private ESWorkbenchAuthoringModeDefinition GetActiveAuthoringMode()
        {
            return getAuthoringModes?.Invoke()?.FirstOrDefault(value => value != null
                && value.ModeId == activeAuthoringModeId
                && (value.IsAvailable == null || value.IsAvailable(actions)));
        }

        private ESWorkbenchViewportDescriptor ResolveActiveViewportDescriptor()
        {
            if (string.IsNullOrWhiteSpace(activeViewportId)) return null;
            return getViewports?.Invoke()?.FirstOrDefault(value => value != null
                && string.Equals(value.ViewportId, activeViewportId, StringComparison.Ordinal));
        }

        private void ActivateViewport(ESWorkbenchViewportDescriptor descriptor)
        {
            if (descriptor == null) return;
            StopDragEdgePan();
            DeactivateCurrentViewport();
            if (!liveViewports.TryGetValue(descriptor.ViewportId, out IESWorkbenchViewport viewport) || viewport == null)
            {
                try
                {
                    viewport = descriptor.Create(new ESWorkbenchViewportContext(
                        owner,
                        actions,
                        descriptor.ViewportId,
                        layout.GetOrCreateViewportState(descriptor.ViewportId),
                        GetVisibleViewportHierarchy,
                        IsHierarchyVisible,
                        IsHierarchyLocked,
                        RequestViewportFooterRefresh,
                        viewportFeel,
                        pointerCoordinator));
                    if (viewport == null || viewport.Root == null)
                        throw new InvalidOperationException("视口工厂返回空实例：" + descriptor.ViewportId);
                    viewport.Root.style.flexGrow = 1f;
                    viewport.Root.style.minWidth = 0f;
                    viewport.Root.style.minHeight = 0f;
                    liveViewports[descriptor.ViewportId] = viewport;
                }
                catch (Exception exception)
                {
                    try { viewport?.Dispose(); }
                    catch (Exception disposeException) { Debug.LogException(disposeException); }
                    Debug.LogException(exception);
                    SetStatus("视口启动失败：" + descriptor.DisplayName + " · " + exception.Message, MessageType.Error);
                    activeViewport = null;
                    activeViewportId = string.Empty;
                    layout.activeViewportId = string.Empty;
                    return;
                }
            }
            activeViewportId = descriptor.ViewportId;
            layout.activeViewportId = activeViewportId;
            activeViewport = viewport;
            foreach (KeyValuePair<string, ToolbarToggle> pair in viewportToggles)
                pair.Value.SetValueWithoutNotify(pair.Key == activeViewportId);
            ActivateCurrentViewportIfVisible();
            if (GetActiveDocument()?.hostsAuthoringViewport == true)
            {
                centerContent.Clear();
                centerContent.Add(activeViewport.Root);
            }
            Button frameAll = viewportModeBar?.Q<Button>("ESWorkbenchFrameAll");
            if (frameAll != null) frameAll.SetEnabled(activeViewport is IESWorkbenchFrameableViewport);
            UpdateBatchPlaceButton();
            UpdateViewportFooter();
        }

        private void ActivateCurrentViewportIfVisible()
        {
            if (activeViewport == null || activeViewportActivated
                || GetActiveDocument()?.hostsAuthoringViewport != true) return;
            try
            {
                activeViewport.Activate();
                activeViewportActivated = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("视口激活失败：" + exception.Message, MessageType.Error);
            }
        }

        private void DeactivateCurrentViewport()
        {
            StopDragEdgePan();
            ClearViewportDropPreview();
            if (!activeViewportActivated) return;
            try { activeViewport?.Deactivate(); }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { activeViewportActivated = false; }
        }

        private void FrameActiveViewport()
        {
            if (!(activeViewport is IESWorkbenchFrameableViewport frameable)) return;
            frameable.FrameAll();
            activeViewport.Refresh(ESWorkbenchRefreshReason.Explicit);
            SetStatus("视口已适配全部内容。", MessageType.Info);
        }

        private void OnSelectionSetChanged(IReadOnlyList<ESWorkbenchSelection> selections)
        {
            ESWorkbenchSelection selection = selections?.FirstOrDefault()
                ?? ESWorkbenchSelection.Empty;
            SynchronizeListSelection(selections);
            objectList?.RefreshItems();
            objectGridList?.RefreshItems();
            PulseSelectedContentCard();
            RebuildInspector(selection);
            activeViewport?.Refresh(ESWorkbenchRefreshReason.SelectionChanged);
        }

        private void PulseSelectedContentCard()
        {
            if (root?.panel == null) return;
            root.schedule.Execute(() =>
            {
                if (disposed) return;
                root.Query<VisualElement>("ESWorkbenchObjectRow").ForEach(PulseContentElement);
                root.Query<VisualElement>("ESWorkbenchContentGridCardFirst").ForEach(PulseContentElement);
                root.Query<VisualElement>("ESWorkbenchContentGridCardSecond").ForEach(PulseContentElement);
            }).StartingIn(1);
        }

        private void PulseContentElement(VisualElement element)
        {
            ObjectRowState rowState = element?.userData as ObjectRowState;
            ContentCardState cardState = element?.userData as ContentCardState;
            ESWorkbenchObjectDescriptor item = rowState?.item ?? cardState?.item;
            if (item == null || !string.Equals(
                    actions.Selection.Current?.StableId,
                    item.BaseObjectId,
                    StringComparison.Ordinal))
                return;
            int pulseVersion;
            if (rowState != null) pulseVersion = ++rowState.pulseVersion;
            else pulseVersion = ++cardState.pulseVersion;
            element.style.opacity = 0.78f;
            element.schedule.Execute(() =>
            {
                if (disposed) return;
                ObjectRowState currentRow = element.userData as ObjectRowState;
                ContentCardState currentCard = element.userData as ContentCardState;
                if ((currentRow != null && currentRow.pulseVersion != pulseVersion)
                    || (currentCard != null && currentCard.pulseVersion != pulseVersion))
                    return;
                bool hovered = currentRow?.hovered == true || currentCard?.hovered == true;
                ESWorkbenchObjectDescriptor currentItem = currentRow?.item ?? currentCard?.item;
                ApplyContentCardVisual(element, currentItem, hovered);
            }).StartingIn(95);
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            float width = evt.newRect.width;
            float height = evt.newRect.height;
            if (width <= 1f || height <= 1f) return;
            bool changed = Mathf.Abs(width - availableWidth) >= 1f
                || Mathf.Abs(height - availableHeight) >= 1f;
            if (!changed) return;
            availableWidth = width;
            availableHeight = height;
            ApplyPaneVisibility(width);
            ApplyContentVerticalResponsive(height);
            RefreshResponsiveChrome();
        }

        private void OnVisualEvidencePointerDown(PointerDownEvent evt)
        {
            RecordVisualInteraction(
                "window-open-focus",
                "pointer-focus",
                "ui-event/window-pointer");
            if (IsViewportTarget(evt.target as VisualElement))
                RecordVisualInteraction(
                    "viewport-input",
                    "pointer",
                    "ui-event/viewport-pointer");
        }

        private void OnVisualEvidenceWheel(WheelEvent evt)
        {
            if (IsViewportTarget(evt.target as VisualElement))
                RecordVisualInteraction(
                    "viewport-input",
                    "wheel",
                    "ui-event/viewport-wheel");
        }

        private bool IsViewportTarget(VisualElement target)
        {
            if (target == null || centerContent == null || GetActiveDocument()?.hostsAuthoringViewport != true) return false;
            for (VisualElement current = target; current != null; current = current.parent)
                if (ReferenceEquals(current, centerContent)) return true;
            return false;
        }

        private string ResolveCurrentVisualScenarioId()
        {
            ESWorkbenchVisualEnvironment environment = CreateCurrentVisualEnvironment();
            IReadOnlyList<ESWorkbenchVisualValidationScenario> scenarios =
                LayoutPolicy.CreateCommercialVisualMatrix();
            ESWorkbenchVisualValidationScenario scenario = scenarios.FirstOrDefault(value =>
                value != null && LayoutPolicy.EvaluateScenario(environment, value).Passed);
            return scenario?.ScenarioId ?? string.Empty;
        }

        private void RecordVisualInteraction(string checkId, string target, string source)
        {
            if (disposed || string.IsNullOrWhiteSpace(checkId)) return;
            string scenarioId = ResolveCurrentVisualScenarioId();
            if (string.IsNullOrWhiteSpace(scenarioId)) return;
            if (!visualInteractionObservationsByScenario.TryGetValue(
                    scenarioId,
                    out Dictionary<string, ESWorkbenchVisualInteractionObservation> observations))
            {
                observations = new Dictionary<string, ESWorkbenchVisualInteractionObservation>(StringComparer.Ordinal);
                visualInteractionObservationsByScenario.Add(scenarioId, observations);
            }
            if (!observations.TryGetValue(checkId, out ESWorkbenchVisualInteractionObservation observation))
            {
                observation = new ESWorkbenchVisualInteractionObservation();
                observations.Add(checkId, observation);
            }
            observation.Record(source, target);
        }

        private void OnCenterGeometryChanged(GeometryChangedEvent evt)
        {
            float width = evt.newRect.width;
            if (width <= 1f || Mathf.Abs(width - availableCenterWidth) < 1f) return;
            availableCenterWidth = width;
            RefreshResponsiveChrome();
        }

        private void ToggleLeftPane()
        {
            layout.layoutPreset = ESWorkbenchLayoutPreset.Custom;
            if (LayoutPolicy.ResolveTier(availableWidth) != ESWorkbenchResponsiveTier.Wide)
                layout.compactSidePane = layout.compactSidePane == "left" ? string.Empty : "left";
            else
                layout.leftPaneVisible = !layout.leftPaneVisible;
            ApplyPaneVisibility(availableWidth);
            RecordVisualInteraction(
                "pane-collapse-restore",
                IsLeftPaneVisible() ? "visible" : "hidden",
                "ui-event/pane-toggle");
        }

        private void ToggleInspectorPane()
        {
            layout.layoutPreset = ESWorkbenchLayoutPreset.Custom;
            if (LayoutPolicy.ResolveTier(availableWidth) != ESWorkbenchResponsiveTier.Wide)
                layout.compactSidePane = layout.compactSidePane == "inspector" ? string.Empty : "inspector";
            else
                layout.inspectorPaneVisible = !layout.inspectorPaneVisible;
            ApplyPaneVisibility(availableWidth);
            RecordVisualInteraction(
                "pane-collapse-restore",
                IsInspectorPaneVisible() ? "visible" : "hidden",
                "ui-event/pane-toggle");
        }

        private void ApplyLayoutPreset(ESWorkbenchLayoutPreset preset)
        {
            if (preset == ESWorkbenchLayoutPreset.Custom) return;
            layout.layoutPreset = preset;
            layout.leftPaneWidth = LayoutPolicy.PreferredLeftPaneWidth;
            layout.inspectorPaneWidth = LayoutPolicy.PreferredInspectorPaneWidth;
            layout.bottomDrawerHeight = LayoutPolicy.PreferredBottomDrawerHeight;
            layout.bottomDrawerUserSized = false;

            switch (preset)
            {
                case ESWorkbenchLayoutPreset.Focus:
                    layout.leftPaneVisible = false;
                    layout.inspectorPaneVisible = false;
                    layout.bottomDrawerExpanded = false;
                    layout.compactSidePane = string.Empty;
                    break;
                case ESWorkbenchLayoutPreset.Content:
                    layout.leftPaneVisible = true;
                    layout.inspectorPaneVisible = false;
                    layout.bottomDrawerExpanded = false;
                    layout.compactSidePane = "left";
                    break;
                case ESWorkbenchLayoutPreset.Diagnostics:
                    layout.leftPaneVisible = false;
                    layout.inspectorPaneVisible = true;
                    layout.bottomDrawerExpanded = true;
                    layout.compactSidePane = "inspector";
                    break;
                case ESWorkbenchLayoutPreset.Production:
                    layout.leftPaneVisible = false;
                    layout.inspectorPaneVisible = true;
                    layout.bottomDrawerExpanded = true;
                    layout.compactSidePane = "inspector";
                    activeBottomTab = "tasks";
                    layout.activeBottomTab = activeBottomTab;
                    break;
                default:
                    layout.leftPaneVisible = true;
                    layout.inspectorPaneVisible = true;
                    layout.bottomDrawerExpanded = true;
                    layout.compactSidePane = "inspector";
                    break;
            }

            if (outerSplit != null)
                outerSplit.fixedPaneInitialDimension = layout.leftPaneWidth;
            if (contentSplit != null)
                contentSplit.fixedPaneInitialDimension = layout.inspectorPaneWidth;
            if (workspaceSplit != null)
                workspaceSplit.fixedPaneInitialDimension = layout.bottomDrawerHeight;
            if (leftPanel != null) leftPanel.style.width = layout.leftPaneWidth;
            if (inspectorPanel != null) inspectorPanel.style.width = layout.inspectorPaneWidth;
            if (bottomDrawer != null) bottomDrawer.style.height = layout.bottomDrawerHeight;

            ApplyPaneVisibility(availableWidth);
            ApplyBottomDrawerVisibility();
            root?.MarkDirtyRepaint();
        }

        private void RegisterPaneResizeTracking(
            TwoPaneSplitView split,
            Func<float> readDimension,
            Action<float> persistDimension)
        {
            if (split == null || readDimension == null || persistDimension == null) return;
            VisualElement dragHandle = split.Q<VisualElement>(className: "unity-two-pane-split-view__dragline-anchor")
                ?? split.Q<VisualElement>(className: "unity-two-pane-split-view__dragline");
            if (dragHandle == null) return;
            var drag = new ESWorkbenchPointerDragState(viewportFeel.DragStartPixels);
            var resizeSession = new ESWorkbenchPaneResizeSession();
            Action<int> completeResize = pointerId =>
            {
                if (!drag.IsActive || (pointerId >= 0 && pointerId != drag.PointerId)) return;
                int capturedId = drag.Reset();
                bool committed = resizeSession.TryCommit(
                    capturedId,
                    readDimension(),
                    out float resizeStart,
                    out float resizeEnd);
                if (!committed
                    && resizeSession.TryCancel(capturedId, out float restoreDimension))
                    split.fixedPaneInitialDimension = restoreDimension;
                if (capturedId >= 0)
                    pointerCoordinator.Release(
                        dragHandle,
                        capturedId,
                        ESWorkbenchPointerOwnerKind.PaneResize);
                if (committed && HasDimensionChanged(resizeStart, resizeEnd))
                {
                    persistDimension(resizeEnd);
                    MarkCustomLayoutIfDimensionChanged(resizeStart, resizeEnd);
                }
                if (capturedId >= 0 && dragHandle.HasPointerCapture(capturedId))
                    dragHandle.ReleasePointer(capturedId);
            };
            Action<int> cancelResize = pointerId =>
            {
                if (!drag.IsActive || (pointerId >= 0 && pointerId != drag.PointerId)) return;
                int capturedId = drag.Reset();
                bool cancelled = resizeSession.TryCancel(capturedId, out float restoreDimension);
                if (capturedId >= 0)
                    pointerCoordinator.Release(
                        dragHandle,
                        capturedId,
                        ESWorkbenchPointerOwnerKind.PaneResize);
                if (cancelled) split.fixedPaneInitialDimension = restoreDimension;
                if (capturedId >= 0 && dragHandle.HasPointerCapture(capturedId))
                    dragHandle.ReleasePointer(capturedId);
            };
            dragHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0
                    || !pointerCoordinator.TryAcquire(
                        dragHandle,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.PaneResize)
                    || !drag.Arm(evt.pointerId, evt.position))
                {
                    pointerCoordinator.Release(
                        dragHandle,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.PaneResize);
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!resizeSession.Begin(evt.pointerId, readDimension()))
                {
                    drag.Reset();
                    pointerCoordinator.Release(
                        dragHandle,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.PaneResize);
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!dragHandle.HasPointerCapture(evt.pointerId)) dragHandle.CapturePointer(evt.pointerId);
            });
            dragHandle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (drag.ShouldStart(evt.pointerId, evt.position)) drag.MarkStarted(evt.pointerId);
            });
            dragHandle.RegisterCallback<PointerUpEvent>(evt =>
            {
                completeResize(evt.pointerId);
                evt.StopImmediatePropagation();
            });
            dragHandle.RegisterCallback<PointerCancelEvent>(evt =>
            {
                cancelResize(evt.pointerId);
                evt.StopImmediatePropagation();
            });
            dragHandle.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                // CaptureOut is ambiguous with an explicit PointerUp release, but
                // completeResize has already reset the session in that path. When
                // capture is lost because focus/window ownership changes, the active
                // session must be cancelled and the original dimension restored;
                // committing a partial split here leaves a stale layout behind.
                cancelResize(evt.pointerId);
                evt.StopImmediatePropagation();
            });
        }

        private void MarkCustomLayoutIfDimensionChanged(float before, float after)
        {
            if (!HasDimensionChanged(before, after)) return;
            layout.layoutPreset = ESWorkbenchLayoutPreset.Custom;
            RecordVisualInteraction(
                "pane-resize",
                Mathf.RoundToInt(before) + "-to-" + Mathf.RoundToInt(after),
                "ui-event/split-resize");
        }

        private static bool HasDimensionChanged(float before, float after)
        {
            return !float.IsNaN(before) && !float.IsInfinity(before)
                && !float.IsNaN(after) && !float.IsInfinity(after)
                && Mathf.Abs(before - after) >= 1f;
        }

        private void ApplyPaneVisibility(float width)
        {
            if (disposed || outerSplit == null || contentSplit == null) return;
            bool compact = LayoutPolicy.ResolveTier(width) != ESWorkbenchResponsiveTier.Wide;
            bool showLeft = compact ? layout.compactSidePane == "left" : layout.leftPaneVisible;
            bool showInspector = compact ? layout.compactSidePane == "inspector" : layout.inspectorPaneVisible;
            ApplyProtectedPaneDimensions(width, showLeft, showInspector);
            if (showLeft) outerSplit.UnCollapse();
            else outerSplit.CollapseChild(0);
            if (showInspector) contentSplit.UnCollapse();
            else contentSplit.CollapseChild(1);
            UpdatePaneButtons(showLeft, showInspector);
        }

        private bool IsLeftPaneVisible()
        {
            return LayoutPolicy.ResolveTier(availableWidth) == ESWorkbenchResponsiveTier.Wide
                ? layout.leftPaneVisible
                : layout.compactSidePane == "left";
        }

        private bool IsInspectorPaneVisible()
        {
            return LayoutPolicy.ResolveTier(availableWidth) == ESWorkbenchResponsiveTier.Wide
                ? layout.inspectorPaneVisible
                : layout.compactSidePane == "inspector";
        }

        private void UpdatePaneButtons()
        {
            bool compact = LayoutPolicy.ResolveTier(availableWidth) != ESWorkbenchResponsiveTier.Wide;
            UpdatePaneButtons(
                compact ? layout.compactSidePane == "left" : layout.leftPaneVisible,
                compact ? layout.compactSidePane == "inspector" : layout.inspectorPaneVisible);
        }

        private void ApplyMinimumWindowSize()
        {
            if (owner == null) return;
            Rect availableArea = EditorGUIUtility.GetMainWindowPosition();
            Vector2 adaptiveMinimum = LayoutPolicy.ResolveAdaptiveMinimum(availableArea);
            owner.minSize = adaptiveMinimum;
            if (owner.docked || owner.rootVisualElement?.panel == null)
                return;
            Rect current = owner.position;
            if (current.width <= 1f || current.height <= 1f)
                return;
            Rect clamped = LayoutPolicy.ClampFloatingWindow(current, availableArea);
            if (Mathf.Abs(current.x - clamped.x) >= 1f
                || Mathf.Abs(current.y - clamped.y) >= 1f
                || Mathf.Abs(current.width - clamped.width) >= 1f
                || Mathf.Abs(current.height - clamped.height) >= 1f)
                owner.position = clamped;
        }

        private void ApplyProtectedPaneDimensions(float width, bool showLeft, bool showInspector)
        {
            if (centerPanel == null) return;
            float sideMinimum = (showLeft ? LayoutPolicy.MinimumLeftPaneWidth : 0f)
                + (showInspector ? LayoutPolicy.MinimumInspectorPaneWidth : 0f);
            float centerWidth = LayoutPolicy.ResolveProtectedCenterWidth(width);
            if (sideMinimum > 0f)
                centerWidth = Mathf.Min(centerWidth, Mathf.Max(280f, width - sideMinimum - 12f));
            centerPanel.style.minWidth = centerWidth;

            float sideBudget = Mathf.Max(0f, width - centerWidth - 12f);
            float desiredLeft = Mathf.Min(
                LayoutPolicy.MaximumLeftPaneWidth,
                Mathf.Max(LayoutPolicy.PreferredLeftPaneWidth, width * LayoutPolicy.MaximumLeftPaneRatio));
            float desiredInspector = Mathf.Min(
                LayoutPolicy.MaximumInspectorPaneWidth,
                Mathf.Max(LayoutPolicy.PreferredInspectorPaneWidth, width * LayoutPolicy.MaximumInspectorPaneRatio));
            if (showLeft && showInspector)
            {
                float minimumTotal = LayoutPolicy.MinimumLeftPaneWidth + LayoutPolicy.MinimumInspectorPaneWidth;
                float availableExtra = Mathf.Max(0f, sideBudget - minimumTotal);
                float desiredLeftExtra = Mathf.Max(0f, desiredLeft - LayoutPolicy.MinimumLeftPaneWidth);
                float desiredInspectorExtra = Mathf.Max(0f, desiredInspector - LayoutPolicy.MinimumInspectorPaneWidth);
                float desiredExtra = desiredLeftExtra + desiredInspectorExtra;
                float scale = desiredExtra <= 0f ? 0f : Mathf.Min(1f, availableExtra / desiredExtra);
                desiredLeft = LayoutPolicy.MinimumLeftPaneWidth + desiredLeftExtra * scale;
                desiredInspector = LayoutPolicy.MinimumInspectorPaneWidth + desiredInspectorExtra * scale;
            }
            else if (showLeft)
            {
                desiredLeft = Mathf.Max(LayoutPolicy.MinimumLeftPaneWidth, Mathf.Min(desiredLeft, sideBudget));
            }
            else if (showInspector)
            {
                desiredInspector = Mathf.Max(LayoutPolicy.MinimumInspectorPaneWidth, Mathf.Min(desiredInspector, sideBudget));
            }

            if (leftPanel != null) leftPanel.style.maxWidth = Mathf.Max(LayoutPolicy.MinimumLeftPaneWidth, desiredLeft);
            if (inspectorPanel != null) inspectorPanel.style.maxWidth = Mathf.Max(LayoutPolicy.MinimumInspectorPaneWidth, desiredInspector);
            if (bottomDrawer != null)
            {
                bottomDrawer.style.maxHeight = ResolveMaximumBottomDrawerHeight();
                ApplyBottomPanelHeight();
            }
        }

        private void RefreshResponsiveChrome()
        {
            int signature = ((int)LayoutPolicy.ResolveTier(availableWidth) * 100000)
                + LayoutPolicy.ResolveVisibleCommandCount(availableWidth) * 10000
                + LayoutPolicy.ResolveVisibleDocumentCount(availableCenterWidth) * 1000
                + LayoutPolicy.ResolveVisibleBottomPanelCount(availableWidth) * 100
                + LayoutPolicy.ResolveVisibleViewportStatusCount(availableCenterWidth) * 10
                + (availableCenterWidth >= 620f ? 1 : 0);
            if (signature == responsiveSignature) return;
            responsiveSignature = signature;
            BuildCommandBar();
            BuildDocumentTabs();
            BuildBottomTabs();
            UpdateViewportFooter();
        }

        internal void ApplyResponsiveLayoutForTest(float width, float centerWidth, float height = 720f)
        {
            availableWidth = Mathf.Max(1f, width);
            availableCenterWidth = Mathf.Max(1f, centerWidth);
            availableHeight = Mathf.Max(1f, height);
            responsiveSignature = int.MinValue;
            ApplyPaneVisibility(availableWidth);
            ApplyContentVerticalResponsive(availableHeight);
            RefreshResponsiveChrome();
        }

        internal void ApplyContentBrowserResponsiveForTest(float browserWidth, float resultsWidth)
        {
            ApplyContentBrowserResponsive(browserWidth);
            ApplyContentResultsResponsive(resultsWidth);
        }

        internal void SetContentViewModeForTest(ESWorkbenchContentViewMode mode)
        {
            SetContentViewMode(mode);
        }

        internal void SetContentKindForTest(string id)
        {
            SetContentKind(id);
        }

        internal string ActiveContentKindForTest => contentKindFilter;

        internal int VisibleContentCountForTest => visibleObjects.Count;

        internal ESWorkbenchContentSortMode ActiveContentSortModeForTest => contentSortMode;

        internal int ContentSourceSnapshotCountForTest => contentSourceSnapshot.Count;

        internal int DuplicateContentIdCountForTest => duplicateContentIdCount;

        internal int GeneratedThumbnailCacheCountForTest => generatedThumbnailCache.Count;

        internal void ShowLeftTabForTest(string id)
        {
            ShowLeftTab(id);
        }

        internal Texture2D ResolveSemanticContentThumbnailForTest(ESWorkbenchContentKind kind)
        {
            return ResolveSemanticContentThumbnail(kind);
        }

        internal Texture ResolveContentThumbnailForTest(ESWorkbenchObjectDescriptor item)
        {
            return ResolveContentThumbnail(item);
        }

        internal int ResolveGeneratedThumbnailFingerprintForTest(ESWorkbenchObjectDescriptor item)
        {
            if (item == null) return 0;
            string key = item.ContentKind + ":" + item.BaseObjectId + ":" + item.PresetId;
            Color32[] pixels = BuildGeneratedContentThumbnailPixels(
                item.ContentKind,
                ComputeStableThumbnailSeed(key),
                GeneratedThumbnailWidth,
                GeneratedThumbnailHeight);
            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    hash = (hash ^ pixel.r) * 16777619;
                    hash = (hash ^ pixel.g) * 16777619;
                    hash = (hash ^ pixel.b) * 16777619;
                    hash = (hash ^ pixel.a) * 16777619;
                }
                return hash;
            }
        }

        internal bool ContentCategoryRootHasFoldForTest()
        {
            return contentCategoryNodes.FirstOrDefault(value => value?.path == "全部")?.hasChildren == true;
        }

        internal void ApplyLayoutPresetForTest(ESWorkbenchLayoutPreset preset)
        {
            ApplyLayoutPreset(preset);
        }

        internal void CommitPaneResizeForTest(float before, float after)
        {
            MarkCustomLayoutIfDimensionChanged(before, after);
        }

        internal void ShowBottomTabForTest(string id)
        {
            ShowBottomTab(id);
        }

        internal void ShowDocumentForTest(string id)
        {
            ShowDocument(id);
        }

        internal ESWorkbenchBottomPanelDensity ActiveBottomPanelDensityForTest => activeBottomPanelDensity;

        internal float AppliedBottomDrawerHeightForTest => appliedBottomDrawerHeight;

        internal void SetBottomDrawerExpandedForTest(bool expanded)
        {
            layout.bottomDrawerExpanded = expanded;
            ApplyBottomDrawerVisibility();
        }

        internal bool BottomContentVisibleForTest => bottomContent != null
            && bottomContent.style.display.value != DisplayStyle.None;

        internal void CommitBottomPaneResizeForTest(float before, float after)
        {
            if (!HasDimensionChanged(before, after)) return;
            layout.bottomDrawerHeight = Mathf.Clamp(
                after,
                LayoutPolicy.CompactBottomDrawerHeight,
                LayoutPolicy.MaximumBottomDrawerHeight);
            layout.bottomDrawerUserSized = true;
            MarkCustomLayoutIfDimensionChanged(before, after);
            ApplyBottomPanelHeight();
        }

        private void UpdatePaneButtons(bool showLeft, bool showInspector)
        {
            if (leftPaneButton != null)
                leftPaneButton.style.backgroundColor = showLeft
                    ? ESEditorPresentation.SelectionColor : ESEditorPresentation.ControlSurfaceColor;
            if (inspectorPaneButton != null)
                inspectorPaneButton.style.backgroundColor = showInspector
                    ? ESEditorPresentation.SelectionColor : ESEditorPresentation.ControlSurfaceColor;
            if (bottomPaneButton != null)
                bottomPaneButton.style.backgroundColor = layout.bottomDrawerExpanded
                    ? ESEditorPresentation.SelectionColor : ESEditorPresentation.ControlSurfaceColor;
        }

        private void ToggleBottomDrawer()
        {
            layout.layoutPreset = ESWorkbenchLayoutPreset.Custom;
            layout.bottomDrawerExpanded = !layout.bottomDrawerExpanded;
            ApplyBottomDrawerVisibility();
            RecordVisualInteraction(
                "pane-collapse-restore",
                layout.bottomDrawerExpanded ? "visible" : "hidden",
                "ui-event/pane-toggle");
        }

        private void ApplyBottomDrawerVisibility()
        {
            if (disposed || workspaceSplit == null) return;
            workspaceSplit.UnCollapse();
            float height = layout.bottomDrawerExpanded
                ? ResolveBottomPanelHeight()
                : LayoutPolicy.CollapsedBottomDrawerHeight;
            appliedBottomDrawerHeight = height;
            workspaceSplit.fixedPaneInitialDimension = height;
            if (bottomDrawer != null) bottomDrawer.style.height = height;
            if (bottomContent != null)
                bottomContent.style.display = layout.bottomDrawerExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            UpdatePaneButtons();
            if (layout.bottomDrawerExpanded) RebuildBottomDrawer();
        }

        private void BuildBottomTabs()
        {
            if (bottomTabs == null) return;
            bottomTabs.Clear();
            ResolveBottomPanels();
            if (!resolvedBottomPanels.Exists(value => value.PanelId == activeBottomTab))
            {
                activeBottomTab = resolvedBottomPanels.FirstOrDefault()?.PanelId ?? string.Empty;
                layout.activeBottomTab = activeBottomTab;
            }
            int capacity = Mathf.Min(
                LayoutPolicy.ResolveVisibleBottomPanelCount(availableWidth),
                resolvedBottomPanels.Count);
            var visibleIds = new HashSet<string>(StringComparer.Ordinal) { activeBottomTab };
            for (int i = 0; i < resolvedBottomPanels.Count && visibleIds.Count < capacity; i++)
                visibleIds.Add(resolvedBottomPanels[i].PanelId);
            for (int i = 0; i < resolvedBottomPanels.Count; i++)
                if (visibleIds.Contains(resolvedBottomPanels[i].PanelId))
                    AddBottomTab(bottomTabs, resolvedBottomPanels[i]);

            ESWorkbenchBottomPanelDescriptor[] hidden = resolvedBottomPanels
                .Where(value => !visibleIds.Contains(value.PanelId))
                .ToArray();
            if (hidden.Length > 0)
            {
                ToolbarMenu overflow = ESWindowPresentation.CreateHeaderOverflowMenu(
                    "ESWorkbenchBottomOverflow",
                    LayoutPolicy.ResolveTier(availableWidth) == ESWorkbenchResponsiveTier.Narrow
                        ? "通道" : "更多通道",
                    "显示当前宽度下收纳的生产与诊断通道");
                for (int i = 0; i < hidden.Length; i++)
                {
                    ESWorkbenchBottomPanelDescriptor panel = hidden[i];
                    overflow.menu.AppendAction(
                        panel.Title,
                        _ => ShowBottomTab(panel.PanelId, true),
                        _ => activeBottomTab == panel.PanelId
                            ? DropdownMenuAction.Status.Checked
                            : DropdownMenuAction.Status.Normal);
                }
                bottomTabs.Add(overflow);
            }
            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            bottomTabs.Add(spacer);
            bottomTabs.Add(CreateActionButton(null, "收起", "收起生产诊断抽屉", ToggleBottomDrawer));
        }

        private void ResolveBottomPanels()
        {
            resolvedBottomPanels.Clear();
            var merged = new Dictionary<string, ESWorkbenchBottomPanelDescriptor>(StringComparer.Ordinal);
            for (int i = 0; i < standardBottomPanels.Count; i++)
                merged[standardBottomPanels[i].PanelId] = standardBottomPanels[i];
            IReadOnlyList<ESWorkbenchBottomPanelDescriptor> contributed = getBottomPanels?.Invoke();
            if (contributed != null)
                for (int i = 0; i < contributed.Count; i++)
                {
                    ESWorkbenchBottomPanelDescriptor panel = contributed[i];
                    if (panel != null) merged[panel.PanelId] = panel;
                }
            var context = new ESWorkbenchBottomPanelContext(workbenchId, actions, getIssues?.Invoke());
            foreach (ESWorkbenchBottomPanelDescriptor panel in merged.Values
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.PanelId, StringComparer.Ordinal))
            {
                try
                {
                    if (panel.IsAvailable == null || panel.IsAvailable(context))
                        resolvedBottomPanels.Add(panel);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ESWorkbench] 底部面板可用性判断失败："
                        + panel.PanelId + "，" + exception.Message);
                }
            }
        }

        private void AddBottomTab(VisualElement parent, ESWorkbenchBottomPanelDescriptor panel)
        {
            string id = panel.PanelId;
            var toggle = new ToolbarToggle
            {
                name = "ESWorkbenchBottomTab_" + SanitizeElementName(id),
                text = panel.Title,
                tooltip = panel.Tooltip,
                value = activeBottomTab == id,
                userData = id
            };
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) ShowBottomTab(id, true);
                else if (activeBottomTab == id) toggle.SetValueWithoutNotify(true);
            });
            parent.Add(toggle);
        }

        private static string SanitizeElementName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unnamed";
            var builder = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                builder.Append(char.IsLetterOrDigit(character) || character == '_'
                    ? character : '_');
            }
            return builder.ToString();
        }

        private void ShowBottomTab(string id, bool userInitiated = false)
        {
            string previous = activeBottomTab;
            activeBottomTab = id ?? string.Empty;
            layout.activeBottomTab = activeBottomTab;
            root?.Q<VisualElement>("ESWorkbenchBottomTabs")?.Query<ToolbarToggle>()
                .ForEach(toggle => toggle.SetValueWithoutNotify((string)toggle.userData == activeBottomTab));
            RebuildBottomDrawer();
            if (userInitiated && !string.Equals(previous, activeBottomTab, StringComparison.Ordinal))
                RecordVisualInteraction(
                    "bottom-channel-switch",
                    activeBottomTab,
                    "ui-event/bottom-channel");
        }

        private void RebuildBottomDrawer()
        {
            if (disposed || bottomContent == null) return;
            ReleaseBottomPanelContent();
            bottomContent.Clear();
            ResolveBottomPanels();
            ESWorkbenchBottomPanelDescriptor descriptor = resolvedBottomPanels.FirstOrDefault(
                value => value.PanelId == activeBottomTab);
            if (descriptor == null)
            {
                activeBottomPanelDensity = ESWorkbenchBottomPanelDensity.Empty;
                ApplyBottomPanelHeight();
                return;
            }
            try
            {
                var context = new ESWorkbenchBottomPanelContext(workbenchId, actions, getIssues?.Invoke());
                activeBottomPanelContent = descriptor.CreateContent(context);
                if (activeBottomPanelContent?.Root != null)
                    bottomContent.Add(activeBottomPanelContent.Root);
                activeBottomPanelDensity = activeBottomPanelContent?.Density
                    ?? ESWorkbenchBottomPanelDensity.Empty;
                ApplyBottomPanelHeight();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                bottomContent.Add(ESWindowPresentation.CreateEmptyState(
                    "面板加载失败",
                    "当前通道无法创建：" + exception.Message,
                    null,
                    null));
                activeBottomPanelDensity = ESWorkbenchBottomPanelDensity.Compact;
                ApplyBottomPanelHeight();
            }
        }

        private float ResolveMaximumBottomDrawerHeight()
        {
            float ratioLimit = Mathf.Max(
                LayoutPolicy.CompactBottomDrawerHeight,
                availableHeight * LayoutPolicy.MaximumBottomDrawerRatio);
            float verticalBudget = Mathf.Max(
                LayoutPolicy.CompactBottomDrawerHeight,
                availableHeight - LayoutPolicy.MinimumCenterHeight - 57f);
            return Mathf.Min(LayoutPolicy.MaximumBottomDrawerHeight, ratioLimit, verticalBudget);
        }

        private float ResolveBottomPanelHeight()
        {
            float maximum = ResolveMaximumBottomDrawerHeight();
            if (layout.bottomDrawerUserSized)
                return Mathf.Clamp(
                    layout.bottomDrawerHeight,
                    LayoutPolicy.CompactBottomDrawerHeight,
                    maximum);

            float preferredHeight = activeBottomPanelContent?.PreferredHeight ?? 0f;
            float desired;
            switch (activeBottomPanelDensity)
            {
                case ESWorkbenchBottomPanelDensity.Empty:
                    desired = preferredHeight > 0f
                        ? preferredHeight
                        : LayoutPolicy.CompactBottomDrawerHeight;
                    break;
                case ESWorkbenchBottomPanelDensity.Compact:
                    desired = preferredHeight > 0f
                        ? preferredHeight
                        : Mathf.Min(LayoutPolicy.MinimumBottomDrawerHeight, 132f);
                    break;
                case ESWorkbenchBottomPanelDensity.Expanded:
                    desired = preferredHeight > 0f
                        ? preferredHeight
                        : Mathf.Max(LayoutPolicy.PreferredBottomDrawerHeight, 280f);
                    break;
                default:
                    desired = preferredHeight > 0f
                        ? preferredHeight
                        : layout.bottomDrawerHeight;
                    break;
            }
            return Mathf.Clamp(desired, LayoutPolicy.CompactBottomDrawerHeight, maximum);
        }

        private void ApplyBottomPanelHeight()
        {
            if (bottomDrawer == null || workspaceSplit == null || !layout.bottomDrawerExpanded) return;
            float desired = ResolveBottomPanelHeight();
            appliedBottomDrawerHeight = desired;
            workspaceSplit.fixedPaneInitialDimension = desired;
            bottomDrawer.style.height = desired;
        }

        private void ReleaseBottomPanelContent()
        {
            ESWorkbenchBottomPanelContent content = activeBottomPanelContent;
            activeBottomPanelContent = null;
            try { content?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private ESWorkbenchBottomPanelContent CreateActivityPanel(
            ESWorkbenchActivityChannel channel,
            string emptyTitle)
        {
            VisualElement container = new VisualElement();
            IReadOnlyList<ESWorkbenchActivityRecord> records =
                ESWorkbenchPersistentActivityStore.Query(workbenchId, channel);
            if (records.Count == 0)
            {
                container.Add(CreateCompactBottomEmptyState(
                    emptyTitle,
                    "记录会按项目持久化并限制数量；窗口或脚本域重载后仍可查询。"));
                return new ESWorkbenchBottomPanelContent(
                    container,
                    ESWorkbenchBottomPanelDensity.Empty);
            }
            for (int i = 0; i < records.Count; i++)
            {
                ESWorkbenchActivityRecord entry = records[i];
                VisualElement row = CreateProductionRow();
                DateTime.TryParse(entry.updatedUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime updatedUtc);
                Label time = new Label(updatedUtc == default
                    ? "--:--:--" : updatedUtc.ToLocalTime().ToString("HH:mm:ss"));
                time.style.width = 64f;
                time.style.flexShrink = 0f;
                time.style.color = ESEditorPresentation.SectionMutedTextColor;
                row.Add(time);
                string localizedStatus = LocalizeActivityStatus(entry.status);
                string prefix = string.IsNullOrWhiteSpace(localizedStatus)
                    ? string.Empty : "[" + localizedStatus + "] ";
                Label message = new Label(prefix + entry.message);
                message.style.flexGrow = 1f;
                message.style.whiteSpace = WhiteSpace.Normal;
                message.style.color = entry.status.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0
                    || entry.status.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
                    ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error)
                    : entry.status.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0
                        ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning)
                        : ESEditorPresentation.SectionTextColor;
                row.Add(message);
                if (!string.IsNullOrWhiteSpace(entry.artifactPath))
                {
                    Label artifact = new Label(entry.artifactPath) { tooltip = entry.artifactPath };
                    artifact.style.maxWidth = 320f;
                    artifact.style.overflow = Overflow.Hidden;
                    artifact.style.textOverflow = TextOverflow.Ellipsis;
                    artifact.style.color = ESEditorPresentation.SectionMutedTextColor;
                    row.Add(artifact);
                }
                container.Add(row);
            }
            return new ESWorkbenchBottomPanelContent(container);
        }

        private VisualElement CreateCompactBottomEmptyState(string title, string detail)
        {
            VisualElement row = new VisualElement { name = "ESWorkbenchBottomEmptyState" };
            row.style.height = 54f;
            row.style.minHeight = 54f;
            row.style.flexShrink = 0f;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginLeft = 8f;
            row.style.marginRight = 8f;
            row.style.marginTop = 5f;
            row.style.marginBottom = 4f;
            row.style.paddingLeft = 10f;
            row.style.paddingRight = 10f;
            row.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            row.style.borderLeftWidth = 3f;
            row.style.borderLeftColor = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            Label titleLabel = new Label(title ?? string.Empty);
            titleLabel.style.minWidth = 118f;
            titleLabel.style.flexShrink = 0f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            row.Add(titleLabel);
            Label detailLabel = new Label(detail ?? string.Empty)
            {
                tooltip = detail ?? string.Empty
            };
            detailLabel.style.flexGrow = 1f;
            detailLabel.style.minWidth = 0f;
            detailLabel.style.overflow = Overflow.Hidden;
            detailLabel.style.textOverflow = TextOverflow.Ellipsis;
            detailLabel.style.whiteSpace = WhiteSpace.NoWrap;
            detailLabel.style.color = ESEditorPresentation.SectionMutedTextColor;
            row.Add(detailLabel);
            return row;
        }

        private ESWorkbenchBottomPanelContent CreateVisualValidationPanel()
        {
            var container = new VisualElement();
            ESWorkbenchVisualEnvironment environment = CreateCurrentVisualEnvironment();
            ESWorkbenchVisualValidationResult current = LayoutPolicy.EvaluateVisualEnvironment(environment);

            VisualElement status = CreateProductionRow();
            status.style.borderLeftWidth = 3f;
            status.style.borderLeftColor = current.LayoutContractPassed
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
            Label conclusion = new Label(current.Summary);
            conclusion.style.flexGrow = 1f;
            conclusion.style.unityFontStyleAndWeight = FontStyle.Bold;
            status.Add(conclusion);
            status.Add(CreateActionButton(null, "刷新", "重新读取当前窗口尺寸、主题和 UI 缩放", RebuildBottomDrawer));
            status.Add(CreateActionButton(null, "标准布局", "恢复标准创作布局和受保护面板尺寸", () =>
            {
                ApplyLayoutPreset(ESWorkbenchLayoutPreset.Authoring);
                RebuildBottomDrawer();
            }));
            status.Add(CreateActionButton(null, "复制环境", "复制当前视觉验收环境信息", () =>
            {
                EditorGUIUtility.systemCopyBuffer = current.Summary;
                SetStatus("已复制当前视觉验收环境。", MessageType.Info);
            }));
            Button captureButton = CreateActionButton(
                EditorGUIUtility.IconContent("d_SceneViewCamera").image,
                "采集窗口",
                "采集当前真实 EditorWindow，并写入 PNG 与 UTF-8 manifest",
                ScheduleVisualEvidenceCapture);
            captureButton.name = "ESWorkbenchCaptureVisualEvidence";
            status.Add(captureButton);
            container.Add(status);

            var longChineseToggle = new Toggle("启用真实长中文压力样例")
            {
                name = "ESWorkbenchVisualLongChineseToggle",
                value = visualLongChineseContent,
                tooltip = "在当前视觉验收面板渲染长中文标题、路径和恢复动作；该状态会写入视觉证据清单。"
            };
            longChineseToggle.RegisterValueChangedCallback(evt =>
            {
                visualLongChineseContent = evt.newValue;
                selectedVisualScenarioId = string.Empty;
                root?.schedule.Execute(() =>
                {
                    if (!disposed) RebuildBottomDrawer();
                });
            });
            longChineseToggle.style.marginLeft = 8f;
            longChineseToggle.style.marginTop = 4f;
            container.Add(longChineseToggle);

            if (visualLongChineseContent)
            {
                VisualElement stressSample = CreateProductionRow();
                stressSample.style.flexWrap = Wrap.Wrap;
                stressSample.style.marginLeft = 8f;
                stressSample.style.marginRight = 8f;
                stressSample.style.marginBottom = 6f;
                Label stressTitle = new Label(
                    "长中文样例：世界地图资源引用与正式输出事务状态验证");
                stressTitle.style.minWidth = 0f;
                stressTitle.style.flexGrow = 1f;
                stressTitle.style.whiteSpace = WhiteSpace.Normal;
                stressTitle.tooltip =
                    "这是实际渲染到当前工作台的压力内容，不是仅写入清单的标记。";
                stressSample.Add(stressTitle);
                Label stressPath = new Label(
                    "Assets/ESWorldGenerated/示例世界地图/运行时地形与导航发布数据/正式输出校验报告.json");
                stressPath.style.minWidth = 0f;
                stressPath.style.flexBasis = 240f;
                stressPath.style.flexGrow = 1f;
                stressPath.style.whiteSpace = WhiteSpace.Normal;
                stressPath.style.color = ESEditorPresentation.SectionMutedTextColor;
                stressPath.tooltip = stressPath.text;
                stressSample.Add(stressPath);
                stressSample.Add(CreateActionButton(
                    EditorGUIUtility.IconContent("Clipboard").image,
                    "复制",
                    "复制长中文压力样例路径",
                    () => EditorGUIUtility.systemCopyBuffer = stressPath.text));
                container.Add(stressSample);
            }

            Label boundary = new Label(
                "布局合同只证明尺寸、中心保护和缩放输入有效；深浅主题、长中文、控件遮挡与交互仍必须取得真实窗口截图。")
            {
                tooltip = "真实截图未采集时，不能把本面板的绿色状态表述为视觉商业级验收通过。"
            };
            boundary.style.whiteSpace = WhiteSpace.Normal;
            boundary.style.color = ESEditorPresentation.SectionMutedTextColor;
            boundary.style.marginLeft = 8f;
            boundary.style.marginRight = 8f;
            boundary.style.marginTop = 4f;
            boundary.style.marginBottom = 6f;
            container.Add(boundary);

            AddLatestVisualEvidenceRow(container);

            IReadOnlyList<ESWorkbenchVisualValidationScenario> scenarios = LayoutPolicy.CreateCommercialVisualMatrix();
            ResolveVisualEvidenceSource(out _, out string currentSourceGuid);
            int capturedScenarioCount = ESWorkbenchVisualEvidenceCapture.CountCapturedScenarios(
                workbenchId, scenarios, currentSourceGuid);
            Label coverage = new Label(
                "视觉矩阵证据：" + capturedScenarioCount + "/" + scenarios.Count
                + (string.IsNullOrWhiteSpace(currentSourceGuid)
                    ? " · 请先绑定已保存的 Source"
                    : capturedScenarioCount == scenarios.Count
                    ? " · 全部场景已有可重读截图"
                    : " · 仍需在对应真实环境逐项采集"));
            coverage.style.marginLeft = 8f;
            coverage.style.marginBottom = 5f;
            coverage.style.unityFontStyleAndWeight = FontStyle.Bold;
            coverage.style.color = capturedScenarioCount == scenarios.Count
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
            container.Add(coverage);
            ESWorkbenchVisualValidationScenario selectedScenario = ResolveSelectedVisualScenario(scenarios, environment);
            ESWorkbenchVisualScenarioMatch scenarioMatch =
                LayoutPolicy.EvaluateScenario(environment, selectedScenario);
            VisualElement scenarioRow = CreateProductionRow();
            ToolbarMenu scenarioMenu = new ToolbarMenu
            {
                text = selectedScenario == null ? "选择验证场景" : "场景：" + selectedScenario.ScenarioId,
                tooltip = "选择一条真实窗口验收场景；主题、DPI、尺寸和长中文标记必须匹配后才能采集"
            };
            for (int i = 0; i < scenarios.Count; i++)
            {
                ESWorkbenchVisualValidationScenario scenario = scenarios[i];
                scenarioMenu.menu.AppendAction(
                    scenario.ScenarioId,
                    _ =>
                    {
                        selectedVisualScenarioId = scenario.ScenarioId;
                        RebuildBottomDrawer();
                    },
                    _ => selectedScenario != null && selectedScenario.ScenarioId == scenario.ScenarioId
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
            scenarioRow.Add(scenarioMenu);
            scenarioRow.Add(CreateActionButton(
                EditorGUIUtility.IconContent("RectTransformBlueprint").image,
                "应用尺寸",
                "把当前工作台调整到所选场景的逻辑尺寸；主题和编辑器缩放仍必须在真实环境中切换",
                () => ApplyVisualScenarioWindowSize(selectedScenario)));
            Label scenarioConclusion = new Label(scenarioMatch.Summary);
            scenarioConclusion.style.flexGrow = 1f;
            scenarioConclusion.style.minWidth = 0f;
            scenarioConclusion.style.whiteSpace = WhiteSpace.Normal;
            scenarioConclusion.style.marginLeft = 8f;
            scenarioConclusion.style.color = scenarioMatch.Passed
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
            scenarioRow.Add(scenarioConclusion);
            container.Add(scenarioRow);

            IReadOnlyList<ESWorkbenchVisualInteractionCheck> interactionChecks =
                CreateVisualInteractionChecks(selectedScenario?.ScenarioId);
            int passedInteractionChecks = interactionChecks.Count(value => value.passed);
            Label interactionTitle = new Label(
                "实机交互矩阵：" + passedInteractionChecks + "/" + interactionChecks.Count
                + " · 仅记录真实 UI 事件")
            {
                tooltip = "切换视口、拖动分隔线、切换底部通道和执行溢出命令后，系统自动记录事件；静态截图不能替代交互证据。"
            };
            interactionTitle.style.marginLeft = 8f;
            interactionTitle.style.marginTop = 5f;
            interactionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            interactionTitle.style.color = passedInteractionChecks == interactionChecks.Count
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
            container.Add(interactionTitle);
            for (int i = 0; i < interactionChecks.Count; i++)
            {
                ESWorkbenchVisualInteractionCheck check = interactionChecks[i];
                VisualElement row = CreateProductionRow();
                row.style.marginLeft = 16f;
                row.style.marginRight = 8f;
                Label state = new Label(check.passed ? "通过" : "待操作");
                state.style.width = 48f;
                state.style.flexShrink = 0f;
                state.style.color = ESEditorPresentation.GetStatusAccent(
                    0, check.passed ? ESStatusKind.Ready : ESStatusKind.Warning);
                row.Add(state);
                Label title = new Label(check.title);
                title.style.flexGrow = 1f;
                title.style.minWidth = 0f;
                title.style.whiteSpace = WhiteSpace.Normal;
                title.tooltip = check.expected;
                row.Add(title);
                Label observed = new Label(
                    check.observationCount + "/" + check.requiredObservationCount
                    + (string.IsNullOrWhiteSpace(check.observationSummary)
                        ? string.Empty : " · " + check.observationSummary));
                observed.style.minWidth = 120f;
                observed.style.maxWidth = 280f;
                observed.style.overflow = Overflow.Hidden;
                observed.style.textOverflow = TextOverflow.Ellipsis;
                observed.tooltip = string.IsNullOrWhiteSpace(check.evidenceSource)
                    ? check.expected
                    : check.evidenceSource + " · " + check.observedUtc;
                row.Add(observed);
                container.Add(row);
            }
            container.Add(CreateActionButton(
                EditorGUIUtility.IconContent("TreeEditor.Trash").image,
                "重置交互记录",
                "清除当前窗口会话中的真实交互记录；不会删除已写入的截图或清单",
                ResetCurrentVisualInteractionObservations));

            for (int i = 0; i < scenarios.Count; i++)
            {
                ESWorkbenchVisualValidationScenario scenario = scenarios[i];
                VisualElement row = CreateProductionRow();
                Label id = new Label(scenario.ScenarioId) { tooltip = scenario.ScenarioId };
                id.style.width = 205f;
                id.style.flexShrink = 0f;
                row.Add(id);
                Label environmentLabel = new Label(
                    scenario.Width.ToString("0") + "×" + scenario.Height.ToString("0")
                    + " · " + scenario.PixelsPerPoint.ToString("0.##") + "x"
                    + " · " + (scenario.Theme == ESWorkbenchVisualTheme.Dark ? "深色" : "浅色")
                    + " · " + scenario.ExpectedTier
                    + (scenario.LongChineseContent ? " · 长中文" : string.Empty))
                {
                    tooltip = scenario.ScenarioId
                };
                environmentLabel.style.flexGrow = 1f;
                environmentLabel.style.minWidth = 0f;
                environmentLabel.style.overflow = Overflow.Hidden;
                environmentLabel.style.textOverflow = TextOverflow.Ellipsis;
                row.Add(environmentLabel);
                bool hasEvidence = ESWorkbenchVisualEvidenceCapture.TryGetScenario(
                    workbenchId,
                    scenario.ScenarioId,
                    currentSourceGuid,
                    out ESWorkbenchVisualEvidenceCaptureResult scenarioEvidence);
                Label evidence = new Label(hasEvidence ? "已采集" : "待截图");
                evidence.style.width = 58f;
                evidence.style.flexShrink = 0f;
                evidence.style.color = ESEditorPresentation.GetStatusAccent(
                    0, hasEvidence ? ESStatusKind.Ready : ESStatusKind.Warning);
                row.Add(evidence);
                if (hasEvidence)
                {
                    row.Add(CreateActionButton(
                        EditorGUIUtility.IconContent("ViewToolOrbit").image,
                        string.Empty,
                        "打开该矩阵场景的真实窗口截图",
                        () =>
                        {
                            if (!ESWorkbenchVisualEvidenceCapture.TryOpenFile(
                                scenarioEvidence.ScreenshotPath))
                                SetStatus("该场景截图未通过安全检查或已经不存在。", MessageType.Warning);
                        }));
                }
                container.Add(row);
            }
            return new ESWorkbenchBottomPanelContent(container);
        }

        private void ApplyVisualScenarioWindowSize(ESWorkbenchVisualValidationScenario scenario)
        {
            if (scenario == null || owner == null) return;
            selectedVisualScenarioId = scenario.ScenarioId;
            Rect rect = owner.position;
            rect.width = Mathf.Max(owner.minSize.x, scenario.Width);
            rect.height = Mathf.Max(owner.minSize.y, scenario.Height);
            owner.position = rect;
            owner.Repaint();
            SetStatus(
                "已应用场景逻辑尺寸；请核对主题和编辑器缩放是否匹配。",
                MessageType.Info);
            root?.schedule.Execute(() =>
            {
                if (!disposed) RebuildBottomDrawer();
            }).StartingIn(120);
        }

        private ESWorkbenchVisualValidationScenario ResolveSelectedVisualScenario(
            IReadOnlyList<ESWorkbenchVisualValidationScenario> scenarios,
            ESWorkbenchVisualEnvironment environment)
        {
            if (scenarios == null || scenarios.Count == 0) return null;
            ESWorkbenchVisualValidationScenario selected = scenarios.FirstOrDefault(
                value => value != null && value.ScenarioId == selectedVisualScenarioId);
            if (selected != null) return selected;

            selected = scenarios.FirstOrDefault(value => value != null
                && LayoutPolicy.EvaluateScenario(environment, value).Passed);
            selected ??= scenarios[0];
            selectedVisualScenarioId = selected?.ScenarioId ?? string.Empty;
            return selected;
        }

        private ESWorkbenchVisualEnvironment CreateCurrentVisualEnvironment()
        {
            return new ESWorkbenchVisualEnvironment(
                availableWidth,
                availableHeight,
                availableCenterWidth,
                Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint),
                EditorGUIUtility.isProSkin ? ESWorkbenchVisualTheme.Dark : ESWorkbenchVisualTheme.Light,
                visualLongChineseContent);
        }

        private void ResolveVisualEvidenceSource(out string sourceAssetPath, out string sourceAssetGuid)
        {
            UnityEngine.Object source = getAsset?.Invoke();
            sourceAssetPath = source == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(source).Replace('\\', '/');
            sourceAssetGuid = string.IsNullOrWhiteSpace(sourceAssetPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(sourceAssetPath);
        }

        private void ScheduleVisualEvidenceCapture()
        {
            if (disposed || root == null) return;
            SetStatus("正在等待当前工作台完成重绘，然后采集真实窗口。", MessageType.Info);
            owner.Repaint();
            root.schedule.Execute(() =>
            {
                if (disposed) return;
                ResolveVisualEvidenceSource(out string sourceAssetPath, out string sourceAssetGuid);
                if (string.IsNullOrWhiteSpace(sourceAssetPath)
                    || string.IsNullOrWhiteSpace(sourceAssetGuid))
                {
                    SetStatus(
                        "视觉证据采集已阻断：请先绑定已经保存的 Source 资产。",
                        MessageType.Warning);
                    RebuildBottomDrawer();
                    return;
                }
                ESWorkbenchVisualEnvironment environment = CreateCurrentVisualEnvironment();
                ESWorkbenchVisualValidationResult validation = LayoutPolicy.EvaluateVisualEnvironment(environment);
                if (!validation.LayoutContractPassed)
                {
                    SetStatus("视觉证据采集已阻断：" + validation.Summary, MessageType.Warning);
                    RebuildBottomDrawer();
                    return;
                }
                ESWorkbenchVisualValidationScenario selectedScenario = ResolveSelectedVisualScenario(
                    LayoutPolicy.CreateCommercialVisualMatrix(), environment);
                ESWorkbenchVisualScenarioMatch scenarioMatch = LayoutPolicy.EvaluateScenario(
                    environment, selectedScenario);
                if (!scenarioMatch.Passed)
                {
                    SetStatus("视觉证据采集已阻断：" + scenarioMatch.Summary, MessageType.Warning);
                    RebuildBottomDrawer();
                    return;
                }
                IReadOnlyList<ESWorkbenchVisualInteractionCheck> interactionChecks =
                    CreateVisualInteractionChecks(selectedScenario?.ScenarioId);
                if (interactionChecks.Count == 0 || interactionChecks.Any(value => !value.passed))
                {
                    SetStatus(
                        "视觉证据采集已阻断：请先在当前真实窗口完成并确认全部实机交互检查。",
                        MessageType.Warning);
                    RebuildBottomDrawer();
                    return;
                }
                var request = new ESWorkbenchVisualEvidenceCaptureRequest(
                    workbenchId,
                    environment,
                    LayoutPolicy.ResolveTier(availableWidth),
                    validation.LayoutContractPassed,
                    validation.Summary,
                    activeDocument,
                    activeViewportId,
                    selectedScenario?.ScenarioId,
                    scenarioMatch.Passed,
                    scenarioMatch.Summary,
                    interactionChecks,
                    sourceAssetPath,
                    sourceAssetGuid);
                ESWorkbenchVisualEvidenceCaptureResult result =
                    ESWorkbenchVisualEvidenceCapture.Capture(owner, request);
                if (result.Success)
                {
                    latestVisualEvidence = result;
                    latestVisualEvidenceSourceGuid = sourceAssetGuid;
                }
                SetStatus(result.Message, result.Success ? MessageType.Info : MessageType.Error);
                RebuildBottomDrawer();
            }).StartingIn(160);
        }

        private IReadOnlyList<ESWorkbenchVisualInteractionCheck> CreateVisualInteractionChecks(
            string scenarioId)
        {
            visualInteractionObservationsByScenario.TryGetValue(
                scenarioId ?? string.Empty,
                out Dictionary<string, ESWorkbenchVisualInteractionObservation> observations);
            return ESWorkbenchVisualEvidenceCapture.CreateObservedInteractionChecklist(observations);
        }

        private void ResetCurrentVisualInteractionObservations()
        {
            string scenarioId = ResolveCurrentVisualScenarioId();
            if (!string.IsNullOrWhiteSpace(scenarioId))
                visualInteractionObservationsByScenario.Remove(scenarioId);
            SetStatus("已重置当前视觉场景的交互记录。", MessageType.Info);
            root?.schedule.Execute(()
            {
                if (!disposed) RebuildBottomDrawer();
            });
        }

        private void AddLatestVisualEvidenceRow(VisualElement container)
        {
            ResolveVisualEvidenceSource(out _, out string sourceAssetGuid);
            if (!string.Equals(
                latestVisualEvidenceSourceGuid, sourceAssetGuid, StringComparison.Ordinal))
            {
                latestVisualEvidence = null;
                latestVisualEvidenceSourceGuid = sourceAssetGuid;
            }
            if (!latestVisualEvidence.HasValue
                && ESWorkbenchVisualEvidenceCapture.TryGetLatest(
                    workbenchId, sourceAssetGuid, out ESWorkbenchVisualEvidenceCaptureResult restored))
                latestVisualEvidence = restored;

            if (!latestVisualEvidence.HasValue)
            {
                Label missing = new Label("当前工作台尚未采集真实窗口截图。");
                missing.style.marginLeft = 8f;
                missing.style.marginBottom = 6f;
                missing.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
                container.Add(missing);
                return;
            }

            ESWorkbenchVisualEvidenceCaptureResult evidence = latestVisualEvidence.Value;
            VisualElement row = CreateProductionRow();
            Label path = new Label(evidence.ManifestPath)
            {
                tooltip = evidence.ManifestPath
            };
            path.style.flexGrow = 1f;
            path.style.minWidth = 0f;
            path.style.overflow = Overflow.Hidden;
            path.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(path);
            row.Add(CreateActionButton(null, "目录", "打开本次视觉证据目录", () =>
            {
                if (!ESWorkbenchVisualEvidenceCapture.TryRevealDirectory(evidence.RunDirectory))
                    SetStatus("视觉证据目录未通过安全检查或已经不存在。", MessageType.Warning);
            }));
            row.Add(CreateActionButton(null, "截图", "打开当前真实窗口截图", () =>
            {
                if (!ESWorkbenchVisualEvidenceCapture.TryOpenFile(evidence.ScreenshotPath))
                    SetStatus("视觉证据截图未通过安全检查或已经不存在。", MessageType.Warning);
            }));
            row.Add(CreateActionButton(null, "清单", "打开 UTF-8 视觉证据清单", () =>
            {
                if (!ESWorkbenchVisualEvidenceCapture.TryOpenFile(evidence.ManifestPath))
                    SetStatus("视觉证据清单未通过安全检查或已经不存在。", MessageType.Warning);
            }));
            row.Add(CreateActionButton(null, "复制", "复制视觉证据清单绝对路径", () =>
            {
                EditorGUIUtility.systemCopyBuffer = evidence.ManifestPath;
                SetStatus("已复制视觉证据清单路径。", MessageType.Info);
            }));
            container.Add(row);
        }

        private ESWorkbenchBottomPanelContent CreateIssuePanel(
            Func<ESWorkbenchIssueDescriptor, bool> filter,
            string emptyTitle)
        {
            VisualElement container = new VisualElement();
            ESWorkbenchIssueDescriptor[] issues = (getIssues?.Invoke()
                    ?? Array.Empty<ESWorkbenchIssueDescriptor>())
                .Where(issue => issue != null && (filter == null || filter(issue)))
                .OrderByDescending(issue => issue.Severity)
                .ThenByDescending(issue => issue.Priority)
                .ThenBy(issue => issue.IssueId, StringComparer.Ordinal)
                .ToArray();
            if (issues.Length == 0)
                container.Add(CreateCompactBottomEmptyState(
                    emptyTitle,
                    "问题源会在资产、作者数据或任务状态变化后增量刷新。"));
            else
                for (int i = 0; i < issues.Length; i++) container.Add(CreateIssueRow(issues[i]));
            return new ESWorkbenchBottomPanelContent(
                container,
                issues.Length == 0
                    ? ESWorkbenchBottomPanelDensity.Empty
                    : ESWorkbenchBottomPanelDensity.Normal);
        }

        private static string LocalizeActivityStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return string.Empty;
            switch (status.Trim().ToLowerInvariant())
            {
                case "running": return "进行中";
                case "succeeded":
                case "success": return "已成功";
                case "failed":
                case "failure": return "已失败";
                case "cancelled":
                case "canceled": return "已取消";
                case "warning": return "警告";
                case "error": return "错误";
                case "info":
                case "information": return "信息";
                default: return status.Trim();
            }
        }

        private VisualElement CreateIssueRow(ESWorkbenchIssueDescriptor issue)
        {
            VisualElement row = CreateProductionRow();
            row.style.borderLeftWidth = 3f;
            row.style.borderLeftColor = GetIssueColor(issue.Severity);
            Label severity = new Label(GetIssueSeverityLabel(issue.Severity));
            severity.style.width = 54f;
            severity.style.flexShrink = 0f;
            severity.style.unityFontStyleAndWeight = FontStyle.Bold;
            severity.style.color = GetIssueColor(issue.Severity);
            row.Add(severity);
            VisualElement text = new VisualElement();
            text.style.flexGrow = 1f;
            text.style.minWidth = 0f;
            Label title = new Label(issue.Title);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            Label description = new Label(issue.Description);
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.color = ESEditorPresentation.SectionMutedTextColor;
            text.Add(title);
            if (!string.IsNullOrWhiteSpace(issue.Description)) text.Add(description);
            row.Add(text);
            if (!string.IsNullOrWhiteSpace(issue.TargetStableId))
                row.Add(CreateActionButton(null, "定位", "在层级、场景和 Inspector 中定位问题对象", () => LocateIssue(issue)));
            if (issue.Action != null)
                row.Add(CreateActionButton(null,
                    string.IsNullOrWhiteSpace(issue.ActionLabel) ? "处理" : issue.ActionLabel,
                    issue.Description,
                    () => ExecuteIssueAction(issue)));
            return row;
        }

        private static VisualElement CreateProductionRow()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;
            row.style.paddingTop = 5f;
            row.style.paddingBottom = 5f;
            row.style.marginBottom = 2f;
            row.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            return row;
        }

        private void LocateIssue(ESWorkbenchIssueDescriptor issue)
        {
            if (issue == null || !hierarchyById.TryGetValue(issue.TargetStableId, out ESWorkbenchHierarchyDescriptor item))
            {
                SetStatus("问题目标已失效，请刷新工作台：" + issue?.TargetStableId, MessageType.Warning);
                return;
            }
            actions.Selection.Select(item.ToSelection());
            ESWorkbenchDocumentDefinition authoring = getDocuments?.Invoke()?.FirstOrDefault(
                value => value != null && value.hostsAuthoringViewport);
            if (authoring != null) ShowDocument(authoring.documentId);
            SetStatus("已定位问题对象：" + item.DisplayName, MessageType.Info);
        }

        private void ExecuteIssueAction(ESWorkbenchIssueDescriptor issue)
        {
            if (issue?.Action == null) return;
            try
            {
                issue.Action(actions);
                actions.Refresh(ESWorkbenchRefreshReason.DataChanged);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("问题处理失败：" + exception.Message, MessageType.Error);
            }
        }

        private static string GetIssueSeverityLabel(ESWorkbenchIssueSeverity severity)
        {
            switch (severity)
            {
                case ESWorkbenchIssueSeverity.Blocker: return "阻断";
                case ESWorkbenchIssueSeverity.Error: return "错误";
                case ESWorkbenchIssueSeverity.Warning: return "警告";
                default: return "信息";
            }
        }

        private static Color GetIssueColor(ESWorkbenchIssueSeverity severity)
        {
            switch (severity)
            {
                case ESWorkbenchIssueSeverity.Blocker:
                case ESWorkbenchIssueSeverity.Error:
                    return ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
                case ESWorkbenchIssueSeverity.Warning:
                    return ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
                default:
                    return ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            }
        }

        private void RecordActivity(string message, MessageType type)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            string status = type == MessageType.Error ? "Error"
                : type == MessageType.Warning ? "Warning" : "Info";
            ESWorkbenchPersistentActivityStore.Append(
                workbenchId, ESWorkbenchActivityChannel.History, status, message);
            if (type == MessageType.Warning || type == MessageType.Error)
                ESWorkbenchPersistentActivityStore.Append(
                    workbenchId, ESWorkbenchActivityChannel.Log, status, message);
            if (layout.bottomDrawerExpanded) RebuildBottomDrawer();
        }

        internal void RefreshPersistentPanels()
        {
            if (layout.bottomDrawerExpanded) RebuildBottomDrawer();
        }

        private void UpdateViewportFooter()
        {
            if (viewportFooter == null) return;
            viewportFooter.Clear();
            viewportFooter.style.height = availableCenterWidth < 520f ? 50f : 44f;
            if (activeViewport is IESWorkbenchViewportStatusProvider statusProvider)
            {
                ESWorkbenchViewportStatusDescriptor[] statuses =
                    (statusProvider.GetStatusSnapshot() ?? Array.Empty<ESWorkbenchViewportStatusDescriptor>())
                    .Where(value => value != null)
                    .OrderByDescending(value => value.Priority)
                    .ThenBy(value => value.StatusId, StringComparer.Ordinal)
                    .ToArray();
                int visibleCount = Mathf.Min(
                    LayoutPolicy.ResolveVisibleViewportStatusCount(availableCenterWidth),
                    statuses.Length);
                for (int i = 0; i < visibleCount; i++)
                {
                    ESWorkbenchViewportStatusDescriptor status = statuses[i];
                    Label label = new Label(string.IsNullOrEmpty(status.Label)
                        ? status.Value : status.Label + "：" + status.Value) { tooltip = status.Tooltip };
                    label.style.flexShrink = 0f;
                    viewportFooter.Add(label);
                    viewportFooter.Add(CreateFooterDivider());
                }
                if (visibleCount < statuses.Length)
                {
                    ToolbarMenu overflow = ESWindowPresentation.CreateHeaderOverflowMenu(
                        "ESWorkbenchViewportStatusOverflow",
                        availableCenterWidth < 520f ? "状态" : "更多状态",
                        "查看当前宽度下收纳的视口状态");
                    for (int i = visibleCount; i < statuses.Length; i++)
                    {
                        ESWorkbenchViewportStatusDescriptor status = statuses[i];
                        string message = string.IsNullOrEmpty(status.Label)
                            ? status.Value : status.Label + "：" + status.Value;
                        string tooltip = status.Tooltip;
                        overflow.menu.AppendAction(message, _ => ShowTransientViewportStatus(message, tooltip));
                    }
                    viewportFooter.Add(overflow);
                    viewportFooter.Add(CreateFooterDivider());
                }
            }
            string toolName = getTools?.Invoke()?.FirstOrDefault(value => value != null && actions.Tools.IsActive(value.ToolId))?.DisplayName ?? "无工具";
            ESWorkbenchViewportLayoutState state = string.IsNullOrWhiteSpace(activeViewportId)
                ? null
                : layout.GetOrCreateViewportState(activeViewportId);
            viewportFooter.Add(new Label("工具：" + toolName));
            viewportFooter.Add(CreateFooterDivider());
            viewportFooter.Add(new Label(state != null && state.snapEnabled
                ? "吸附：" + state.moveSnap.ToString("0.##") + "m"
                : "吸附：关"));
            viewportFooter.Add(CreateFooterDivider());
            ESWorkbenchSelection selected = actions.Selection.Current;
            Label selectionLabel = new Label(selected == null || selected.IsEmpty ? "未选择对象" : selected.StableId);
            selectionLabel.style.flexGrow = 1f;
            selectionLabel.style.minWidth = 0f;
            selectionLabel.style.overflow = Overflow.Hidden;
            selectionLabel.style.textOverflow = TextOverflow.Ellipsis;
            viewportFooter.Add(selectionLabel);
            if (availableCenterWidth >= 620f)
                viewportFooter.Add(new Label("中键/Alt 拖动画布 · 滚轮缩放"));
        }

        /// <summary>合并视口高频状态通知，避免鼠标移动期间反复重建底部 UI。</summary>
        private void RequestViewportFooterRefresh()
        {
            if (disposed || root == null || root.panel == null || viewportFooterRefreshQueued) return;
            viewportFooterRefreshQueued = true;
            root.schedule.Execute(() =>
            {
                viewportFooterRefreshQueued = false;
                if (!disposed) UpdateViewportFooter();
            }).StartingIn(0);
        }

        private void ShowTransientViewportStatus(string message, string tooltip)
        {
            if (statusLabel == null) return;
            statusLabel.text = string.IsNullOrWhiteSpace(message) ? "就绪" : message;
            statusLabel.tooltip = string.IsNullOrWhiteSpace(tooltip) ? statusLabel.text : tooltip;
            statusLabel.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            if (recoveryButton != null) recoveryButton.style.display = DisplayStyle.None;
        }

        private static VisualElement CreateFooterDivider()
        {
            VisualElement divider = new VisualElement();
            divider.style.width = 1f;
            divider.style.height = 13f;
            divider.style.marginLeft = 8f;
            divider.style.marginRight = 8f;
            divider.style.backgroundColor = ESEditorPresentation.DividerColor;
            return divider;
        }

        private void SynchronizeListSelection(ESWorkbenchSelection selection)
        {
            SynchronizeListSelection(selection == null || selection.IsEmpty
                ? Array.Empty<ESWorkbenchSelection>()
                : new[] { selection });
        }

        private void SynchronizeListSelection(IReadOnlyList<ESWorkbenchSelection> selections)
        {
            var stableIds = new HashSet<string>(
                (selections ?? Array.Empty<ESWorkbenchSelection>())
                    .Where(value => value != null && !value.IsEmpty)
                    .Select(value => value.StableId),
                StringComparer.Ordinal);
            if (objectList != null)
            {
                objectList.SetSelectionWithoutNotify(visibleObjects
                    .Select((value, index) => new { value, index })
                    .Where(pair => pair.value != null && stableIds.Contains(pair.value.BaseObjectId))
                    .Select(pair => pair.index));
            }
            if (hierarchyList != null)
            {
                hierarchyList.SetSelectionWithoutNotify(visibleHierarchy
                    .Select((value, index) => new { value, index })
                    .Where(pair => pair.value != null && stableIds.Contains(pair.value.ItemId))
                    .Select(pair => pair.index));
            }
        }

        private void OnToolChanged(string toolId)
        {
            layout.activeToolId = toolId ?? string.Empty;
            BuildToolBar();
            activeViewport?.Refresh(ESWorkbenchRefreshReason.Explicit);
            UpdateViewportFooter();
        }

        private void RebuildInspector(ESWorkbenchSelection selection)
        {
            if (inspectorContent == null) return;
            if (inspectorTitle != null)
            {
                inspectorTitle.text = ResolveSelectionTitle(selection);
                inspectorTitle.tooltip = selection?.StableId ?? string.Empty;
            }
            inspectorContent.Clear();
            if (selection == null || selection.IsEmpty)
            {
                ESWorkbenchAuthoringModeDefinition mode = GetActiveAuthoringMode();
                if (mode != null)
                {
                    if (inspectorTitle != null) inspectorTitle.text = mode.Title + "设置";
                    VisualElement modeInspector = mode.CreateInspector?.Invoke(actions);
                    inspectorContent.Add(modeInspector ?? ESWindowPresentation.CreateEmptyState(
                            mode.Title,
                            string.IsNullOrWhiteSpace(mode.Tooltip) ? "当前作者模式已就绪。" : mode.Tooltip,
                            null,
                            null));
                    return;
                }
            }
            IReadOnlyList<ESWorkbenchInspectorDescriptor> inspectors = getInspectors?.Invoke();
            ESWorkbenchInspectorDescriptor descriptor = inspectors?
                .Where(value => value != null && value.Matches(selection ?? ESWorkbenchSelection.Empty))
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.InspectorId, StringComparer.Ordinal)
                .FirstOrDefault();
            VisualElement view = descriptor?.CreateView(actions, selection ?? ESWorkbenchSelection.Empty);
            if (view == null && selection?.Payload is ESWorkbenchObjectDescriptor content)
                view = CreateDefaultContentInspector(content);
            ESWorkbenchHierarchyDescriptor hierarchyItem = selection == null
                ? null
                : (getHierarchy?.Invoke() ?? Array.Empty<ESWorkbenchHierarchyDescriptor>())
                    .FirstOrDefault(value => value != null
                        && string.Equals(value.ItemId, selection.StableId, StringComparison.Ordinal));
            bool readOnlyViewport = ResolveActiveViewportDescriptor()?.Kind == ESWorkbenchViewportKind.Game;
            if (hierarchyItem?.Spatial != null
                && (actions.Authoring.CanMove(selection)
                    || actions.Authoring.CanRotate(selection)
                    || actions.Authoring.CanScale(selection)))
            {
                string viewportId = string.IsNullOrWhiteSpace(activeViewportId)
                    ? "core.canvas-2d"
                    : activeViewportId;
                inspectorContent.Add(new ESWorkbenchPrecisionTransformElement(
                    new ESWorkbenchViewportContext(
                        owner,
                        actions,
                        viewportId,
                        layout.GetOrCreateViewportState(viewportId),
                        GetVisibleViewportHierarchy,
                        IsHierarchyVisible,
                        IsHierarchyLocked,
                        feel: viewportFeel,
                        pointerCoordinator: pointerCoordinator),
                    selection,
                    hierarchyItem.Spatial,
                    readOnlyViewport || IsHierarchyLocked(selection.StableId),
                    actions.Selection.CurrentSet));
            }
            if (view != null)
            {
                view.style.flexGrow = 1f;
                view.style.minWidth = 0f;
                if (readOnlyViewport) view.SetEnabled(false);
                inspectorContent.Add(view);
                return;
            }
            inspectorContent.Add(ESWindowPresentation.CreateEmptyState(
                selection == null || selection.IsEmpty ? "未选择对象" : selection.StableId,
                "当前选择没有注册上下文 Inspector。",
                null,
                null));
        }

        private VisualElement CreateDefaultContentInspector(ESWorkbenchObjectDescriptor content)
        {
            var root = new VisualElement { name = "ESWorkbenchDefaultContentInspector" };
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            Label kind = new Label("内容库 · " + content.ContentKindDisplayName);
            kind.style.fontSize = 9f;
            kind.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            Label title = new Label(content.DisplayName);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 15f;
            title.style.marginTop = 3f;
            title.style.marginBottom = 8f;
            root.Add(kind);
            root.Add(title);
            Texture preview = ResolveContentThumbnail(content);
            if (preview != null)
            {
                var image = new Image { image = preview, scaleMode = ScaleMode.ScaleToFit };
                image.style.height = 128f;
                image.style.marginBottom = 8f;
                root.Add(image);
            }
            root.Add(CreateContentInspectorValue("业务分类", content.Category));
            root.Add(CreateContentInspectorValue("默认动作", content.DefaultDragHint));
            root.Add(CreateContentInspectorValue("稳定标识", content.BaseObjectId));
            if (!string.IsNullOrWhiteSpace(content.PresetId))
                root.Add(CreateContentInspectorValue("参数预设", content.PresetId));
            ESWorkbenchContentUsageRecord usage = contentUsage.Get(content.BaseObjectId);
            root.Add(CreateContentInspectorValue("使用状态",
                (usage?.favorite == true ? "已收藏" : "未收藏")
                + " · 使用 " + (usage?.useCount ?? 0) + " 次"));
            if (!string.IsNullOrWhiteSpace(content.Subtitle))
                root.Add(CreateContentInspectorValue("来源", content.Subtitle));
            if (!string.IsNullOrWhiteSpace(content.Tooltip))
            {
                Label description = new Label(content.Tooltip);
                description.style.whiteSpace = WhiteSpace.Normal;
                description.style.marginTop = 8f;
                root.Add(description);
            }
            if (content.Source != null)
            {
                Button locate = ESWindowPresentation.CreateToolbarButton(
                    "在项目中定位",
                    "在 Project 中定位内容源资产",
                    () =>
                    {
                        Selection.activeObject = content.Source;
                        EditorGUIUtility.PingObject(content.Source);
                    });
                locate.style.height = 30f;
                locate.style.marginTop = 10f;
                root.Add(locate);
            }
            return root;
        }

        private static VisualElement CreateContentInspectorValue(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4f;
            Label key = new Label(label);
            key.style.width = 72f;
            key.style.flexShrink = 0f;
            key.style.color = ESEditorPresentation.SectionMutedTextColor;
            Label text = new Label(value ?? string.Empty);
            text.style.flexGrow = 1f;
            text.style.minWidth = 0f;
            text.style.whiteSpace = WhiteSpace.Normal;
            text.tooltip = text.text;
            row.Add(key);
            row.Add(text);
            return row;
        }

        private string ResolveSelectionTitle(ESWorkbenchSelection selection)
        {
            if (selection == null || selection.IsEmpty) return "检查器";
            ESWorkbenchHierarchyDescriptor hierarchyItem = hierarchyById.TryGetValue(selection.StableId, out ESWorkbenchHierarchyDescriptor value)
                ? value
                : null;
            if (hierarchyItem != null) return hierarchyItem.DisplayName;
            ESWorkbenchObjectDescriptor objectItem = visibleObjects.FirstOrDefault(item => item.BaseObjectId == selection.StableId);
            if (objectItem != null) return GetEffectiveDescriptor(objectItem).DisplayName;
            if (selection.UnityObject != null) return selection.UnityObject.name;
            return selection.StableId;
        }

        private void ExecuteCommand(ESWorkbenchCommandDescriptor command)
        {
            if (command == null || (command.CanExecute != null && !command.CanExecute(actions))) return;
            try { command.Execute(actions); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("命令执行失败：" + exception.Message, MessageType.Error);
            }
            BuildCommandBar();
        }

        private void ExecuteTool(ESWorkbenchToolDescriptor tool)
        {
            if (tool == null || (tool.IsAvailable != null && !tool.IsAvailable(actions))) return;
            try
            {
                tool.Activate(actions);
                actions.Tools.Activate(tool.ToolId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("工具激活失败：" + exception.Message, MessageType.Error);
            }
        }

        private void ShowSelectionContextMenu()
        {
            IReadOnlyList<ESWorkbenchCommandDescriptor> commands = getCommands?.Invoke();
            if (commands == null) return;
            ESWorkbenchCommandDescriptor[] contextual = commands
                .Where(value => value != null && value.ShowInContextMenu)
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.CommandId, StringComparer.Ordinal)
                .ToArray();
            if (contextual.Length == 0) return;
            var menu = new GenericMenu();
            for (int i = 0; i < contextual.Length; i++)
            {
                ESWorkbenchCommandDescriptor command = contextual[i];
                GUIContent label = new GUIContent(command.DisplayName);
                if (command.CanExecute == null || command.CanExecute(actions))
                    menu.AddItem(label, false, () => ExecuteCommand(command));
                else
                    menu.AddDisabledItem(label);
            }
            menu.ShowAsContext();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (IsTextEditingTarget(evt.target as VisualElement)) return;
            IReadOnlyList<ESWorkbenchCommandDescriptor> commands =
                getCommands?.Invoke() ?? Array.Empty<ESWorkbenchCommandDescriptor>();
            ESWorkbenchCommandDescriptor match = commands.FirstOrDefault(command => command?.Shortcut != null
                && command.Shortcut.Value.Matches(evt)
                && (command.CanExecute == null || command.CanExecute(actions)));
            if (match != null)
            {
                ExecuteCommand(match);
                evt.StopImmediatePropagation();
                return;
            }
            IReadOnlyList<ESWorkbenchToolDescriptor> tools = getTools?.Invoke();
            ESWorkbenchToolDescriptor tool = tools?.FirstOrDefault(value => value?.Shortcut != null
                && value.Shortcut.Value.Matches(evt)
                && (value.IsAvailable == null || value.IsAvailable(actions)));
            if (tool == null
                && IsWithinActiveViewport(evt.target as VisualElement)
                && activeViewport is IESWorkbenchNudgeableViewport nudgeable
                && nudgeable.TryNudge(
                    evt.keyCode,
                    evt.shiftKey,
                    evt.ctrlKey || evt.commandKey,
                    out _))
            {
                evt.StopImmediatePropagation();
                return;
            }
            if (tool == null) return;
            ExecuteTool(tool);
            evt.StopImmediatePropagation();
        }

        private bool IsWithinActiveViewport(VisualElement target)
        {
            VisualElement viewportRoot = activeViewport?.Root;
            if (viewportRoot == null) return false;
            for (VisualElement current = target; current != null; current = current.parent)
                if (ReferenceEquals(current, viewportRoot)) return true;
            return false;
        }

        internal static bool IsTextEditingTarget(VisualElement target)
        {
            for (VisualElement current = target; current != null; current = current.parent)
            {
                if (current is TextField
                    || current.ClassListContains("unity-base-text-field")
                    || current.ClassListContains("unity-text-input"))
                    return true;
            }
            return false;
        }

        private static VisualElement CreateHorizontalBar(string name, float height)
        {
            VisualElement bar = new VisualElement { name = name };
            bar.style.height = height;
            bar.style.minHeight = height;
            bar.style.flexShrink = 0f;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 7f;
            bar.style.paddingRight = 7f;
            bar.style.backgroundColor = ESEditorPresentation.ToolbarSurfaceColor;
            bar.style.borderBottomWidth = 1f;
            bar.style.borderBottomColor = ESEditorPresentation.DividerColor;
            return bar;
        }

        private static Label CreateSectionTitle(string name, string title)
        {
            Label label = new Label(title ?? string.Empty)
            {
                name = name,
                tooltip = title ?? string.Empty
            };
            label.style.height = 27f;
            label.style.minHeight = 27f;
            label.style.flexShrink = 0f;
            label.style.paddingLeft = 10f;
            label.style.paddingRight = 8f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = ESEditorPresentation.SectionMutedTextColor;
            label.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            label.style.borderBottomWidth = 1f;
            label.style.borderBottomColor = ESEditorPresentation.DividerColor;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            return label;
        }

        private static VisualElement CreateToolbarDivider()
        {
            VisualElement divider = new VisualElement { pickingMode = PickingMode.Ignore };
            divider.style.width = 1f;
            divider.style.height = 16f;
            divider.style.flexShrink = 0f;
            divider.style.marginLeft = 5f;
            divider.style.marginRight = 5f;
            divider.style.backgroundColor = ESEditorPresentation.DividerColor;
            return divider;
        }

        private static Button CreateActionButton(Texture icon, string text, string tooltip, Action action)
        {
            Button button = ESWindowPresentation.CreateHeaderActionButton(icon, text, tooltip, action);
            button.style.marginRight = 3f;
            return button;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            StopDragEdgePan();
            actions.Selection.SetChanged -= OnSelectionSetChanged;
            actions.Tools.Changed -= OnToolChanged;
            ReleaseContributedContent();
            foreach (IESWorkbenchViewport viewport in liveViewports.Values)
            {
                try { viewport?.Dispose(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            liveViewports.Clear();
            activeViewport = null;
            root?.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root?.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            root?.UnregisterCallback<PointerDownEvent>(OnVisualEvidencePointerDown, TrickleDown.TrickleDown);
            root?.UnregisterCallback<PointerCancelEvent>(OnRootPointerCancel, TrickleDown.TrickleDown);
            root?.UnregisterCallback<WheelEvent>(OnVisualEvidenceWheel, TrickleDown.TrickleDown);
            root?.UnregisterCallback<DragExitedEvent>(OnDragExited, TrickleDown.TrickleDown);
            root?.UnregisterCallback<PointerCaptureOutEvent>(OnRootPointerCaptureOut, TrickleDown.TrickleDown);
            root?.UnregisterCallback<FocusOutEvent>(OnRootFocusOut, TrickleDown.TrickleDown);
            root?.UnregisterCallback<DetachFromPanelEvent>(OnRootDetachedFromPanel);
            centerContent?.UnregisterCallback<DragUpdatedEvent>(OnDragUpdated, TrickleDown.TrickleDown);
            centerContent?.UnregisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);
            centerContent?.UnregisterCallback<DragLeaveEvent>(OnDragLeave, TrickleDown.TrickleDown);
            centerPanel?.UnregisterCallback<GeometryChangedEvent>(OnCenterGeometryChanged);
            thumbnailRefreshSchedule?.Pause();
            thumbnailRefreshSchedule = null;
            thumbnailCache.Clear();
            foreach (Texture2D texture in semanticThumbnailCache.Values)
                SafeDestroyThumbnail(texture);
            semanticThumbnailCache.Clear();
            foreach (Texture2D texture in generatedThumbnailCache.Values)
                SafeDestroyThumbnail(texture);
            generatedThumbnailCache.Clear();
            generatedThumbnailLru.Clear();
            generatedThumbnailLruNodes.Clear();
            externalDragTransferInFlight = false;
            CancelWorkbenchDrag(true);
            pointerCoordinator.Reset();
            contentPointerGate.Reset();
            visualInteractionObservationsByScenario.Clear();
            unityIconCache.Clear();
        }

        private static void SafeDestroyThumbnail(Texture2D texture)
        {
            if (texture == null)
                return;
            try { UnityEngine.Object.DestroyImmediate(texture); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    internal sealed class ESWorkbenchCanvas2DViewport : VisualElement, IESWorkbenchViewport, IESWorkbenchCancelableViewport, IESWorkbenchFrameableViewport,
        IESWorkbenchEdgePannableViewport, IESWorkbenchNudgeableViewport, IESWorkbenchViewportStatusProvider
    {
        private readonly ESWorkbenchViewportContext context;
        private readonly ESWorkbenchViewportFeelSettings feel;
        private readonly ESWorkbenchCanvasNavigationState navigation;
        private readonly ESWorkbenchEdgePanController edgePan;
        private readonly ESWorkbenchHoverState hover = new ESWorkbenchHoverState();
        private readonly List<ESWorkbenchHierarchyDescriptor> projected = new List<ESWorkbenchHierarchyDescriptor>();
        private readonly VisualElement labelOverlay;
        private bool panning;
        private int panPointerId = -1;
        private readonly ESWorkbenchEdgePanSession edgePanSession =
            new ESWorkbenchEdgePanSession();
        private IVisualElementScheduledItem edgePanSchedule;
        private bool moving;
        private int movePointerId = -1;
        private readonly ESWorkbenchPointerGestureSession gestureSession;
        private readonly ESWorkbenchMoveGestureAnchor moveAnchor;
        private Vector3 pendingMove;
        private Vector3 moveOrigin;
        private bool pendingMoveValid;
        private ESWorkbenchSelection movingSelection;
        private ESWorkbenchSpatialDescriptor movingSpatial;
        private readonly List<ESWorkbenchViewportStatusDescriptor> statusSnapshot =
            new List<ESWorkbenchViewportStatusDescriptor>();
        private Vector3 pointerWorld;
        private bool pointerWorldValid;

        public ESWorkbenchCanvas2DViewport(
            ESWorkbenchViewportContext context,
            ESWorkbenchViewportFeelSettings feel = null)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.feel = feel ?? context?.Feel ?? ESWorkbenchViewportFeelSettings.Standard;
            gestureSession = new ESWorkbenchPointerGestureSession(
                this.feel.DragStartPixels, this.feel);
            moveAnchor = new ESWorkbenchMoveGestureAnchor();
            navigation = new ESWorkbenchCanvasNavigationState(
                context.Layout,
                this.feel.CanvasMinimumZoom,
                this.feel.CanvasMaximumZoom,
                this.feel.CanvasViewportPaddingPixels,
                this.feel);
            edgePan = new ESWorkbenchEdgePanController(this.feel.EdgePanSettings);
            name = "ESWorkbenchCanvas2D";
            style.flexGrow = 1f;
            style.minWidth = 0f;
            style.minHeight = 240f;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = new Color(0.055f, 0.065f, 0.07f, 1f);
            focusable = true;
            generateVisualContent += DrawCanvas;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<FocusOutEvent>(OnFocusOut);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            edgePanSchedule = schedule.Execute(ApplyEdgePan).Every(16);
            edgePanSchedule.Pause();
            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                UpdateLabelPositions();
                MarkDirtyRepaint();
            });
            labelOverlay = new VisualElement { name = "ESWorkbenchCanvas2DLabels", pickingMode = PickingMode.Ignore };
            labelOverlay.style.position = Position.Absolute;
            labelOverlay.style.left = 0f;
            labelOverlay.style.right = 0f;
            labelOverlay.style.top = 0f;
            labelOverlay.style.bottom = 0f;
            Add(labelOverlay);
            RebuildProjection();
        }

        public VisualElement Root => this;
        public void Activate() => Focus();
        public void Deactivate()
        {
            CancelInteraction();
        }

        public void Refresh(ESWorkbenchRefreshReason reason)
        {
            if (reason != ESWorkbenchRefreshReason.SelectionChanged) RebuildProjection();
            else UpdateLabelPositions();
            MarkDirtyRepaint();
        }

        public void FrameAll()
        {
            navigation.Reset();
            UpdateLabelPositions();
            context.StatusChanged?.Invoke();
            MarkDirtyRepaint();
        }

        public bool CanAccept(ESWorkbenchObjectDescriptor item) =>
            item != null && context.Actions.Authoring.CanCreate(item);

        public IReadOnlyList<ESWorkbenchViewportStatusDescriptor> GetStatusSnapshot()
        {
            statusSnapshot.Clear();
            if (pointerWorldValid)
                statusSnapshot.Add(new ESWorkbenchViewportStatusDescriptor(
                    "canvas.pointer-coordinate",
                    "指针",
                    pointerWorld.x.ToString("0.##") + ", "
                        + pointerWorld.y.ToString("0.##") + ", "
                        + pointerWorld.z.ToString("0.##"),
                    "鼠标当前落点的画布世界坐标；移出视口后清除",
                    450));
            statusSnapshot.Add(new ESWorkbenchViewportStatusDescriptor(
                "canvas.zoom",
                "缩放",
                (navigation.Zoom * 100f).ToString("0.#") + "%",
                "当前二维画布缩放比例",
                320));
            return statusSnapshot;
        }

        public bool TryAccept(ESWorkbenchDropContext drop, out string message)
        {
            message = string.Empty;
            if (drop?.Item == null) { message = "拖放对象为空。"; return false; }
            if (!CanAccept(drop.Item)) { message = "当前领域没有为该对象注册创建事务。"; return false; }
            Vector3 world = context.SnapPosition(CanvasToWorld(drop.LocalPosition));
            return context.Actions.Authoring.TryCreate(drop.Item, world, out message);
        }

        private void RebuildProjection()
        {
            hover.Clear();
            projected.Clear();
            IReadOnlyList<ESWorkbenchHierarchyDescriptor> hierarchy = context.Hierarchy;
            if (hierarchy != null)
                for (int i = 0; i < hierarchy.Count; i++)
                {
                    ESWorkbenchHierarchyDescriptor item = hierarchy[i];
                    if (item?.Spatial != null && item.Spatial.VisibleIn2D) projected.Add(item);
                }
            labelOverlay.Clear();
            for (int i = 0; i < projected.Count; i++)
            {
                ESWorkbenchHierarchyDescriptor item = projected[i];
                var label = new Label(item.DisplayName) { name = "ESWorkbenchSpatialLabel", userData = item };
                label.style.position = Position.Absolute;
                label.style.maxWidth = 150f;
                label.style.fontSize = 9f;
                label.style.color = ESEditorPresentation.SectionTextColor;
                label.style.backgroundColor = new Color(0.02f, 0.025f, 0.03f, 0.76f);
                label.style.paddingLeft = 4f;
                label.style.paddingRight = 4f;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                labelOverlay.Add(label);
            }
            UpdateLabelPositions();
        }

        private void DrawCanvas(MeshGenerationContext generationContext)
        {
            Rect viewport = contentRect;
            if (viewport.width <= 1f || viewport.height <= 1f) return;
            Painter2D painter = generationContext.painter2D;
            ESWorkbenchViewportRenderStyle.DrawCanvasBackdrop(painter, viewport);
            Rect worldBounds = ResolveWorldBounds();
            Rect canvasBounds = ResolveCanvasBounds(viewport, worldBounds);
            DrawGrid(painter, canvasBounds);
            for (int i = 0; i < projected.Count; i++) DrawItem(painter, projected[i], worldBounds, canvasBounds);
            if (moving && pendingMoveValid)
            {
                Vector2 origin = WorldToCanvas(moveOrigin, worldBounds, canvasBounds);
                Vector2 target = WorldToCanvas(pendingMove, worldBounds, canvasBounds);
                painter.strokeColor = ESEditorPresentation.SelectionColor;
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(origin);
                painter.LineTo(target);
                painter.Stroke();
                DrawCrosshair(painter, target, 9f, ESEditorPresentation.SelectionColor);
                if (movingSpatial?.Shape == ESWorkbenchSpatialShape.Rectangle)
                {
                    Vector2 half = WorldSizeToCanvas(movingSpatial.Size, worldBounds, canvasBounds) * 0.5f;
                    StrokeRect(painter, new Rect(target - half, half * 2f),
                        ESEditorPresentation.SelectionColor, 2f);
                }
                else
                {
                    painter.strokeColor = ESEditorPresentation.SelectionColor;
                    painter.lineWidth = 2f;
                    painter.BeginPath();
                    painter.Arc(target, 10f, 0f, 360f);
                    painter.Stroke();
                }
            }
        }

        private static void DrawGrid(Painter2D painter, Rect rect)
        {
            ESWorkbenchViewportRenderStyle.DrawCanvasGrid(painter, rect, 12, 12);
        }

        private void DrawItem(
            Painter2D painter,
            ESWorkbenchHierarchyDescriptor item,
            Rect worldBounds,
            Rect canvasBounds)
        {
            ESWorkbenchSpatialDescriptor spatial = item.Spatial;
            Vector2 center = WorldToCanvas(spatial.Position, worldBounds, canvasBounds);
            bool selected = context.Selection.Current?.StableId == item.ItemId;
            bool hovered = !selected && hover.IsHovered(item.ItemId);
            Color color = selected ? ESEditorPresentation.SelectionColor
                : hovered ? Color.Lerp(spatial.Color, ESEditorPresentation.SelectionColor, 0.45f)
                : spatial.Color;
            if (spatial.Shape == ESWorkbenchSpatialShape.Rectangle)
            {
                Vector2 half = WorldSizeToCanvas(spatial.Size, worldBounds, canvasBounds) * 0.5f;
                Rect rect = new Rect(center - half, half * 2f);
                FillRect(painter, rect, color);
                if (selected || hovered) StrokeRect(
                    painter, rect, ESEditorPresentation.SelectionColor, selected ? 2.5f : 1.5f);
                return;
            }
            float radius = feel.ResolveMarkerRadiusPixels(selected, hovered);
            if (spatial.Shape == ESWorkbenchSpatialShape.Point)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.Arc(center, radius, 0f, 360f);
                painter.Fill();
                return;
            }
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(center + Vector2.up * radius);
            painter.LineTo(center + Vector2.right * radius);
            painter.LineTo(center + Vector2.down * radius);
            painter.LineTo(center + Vector2.left * radius);
            painter.ClosePath();
            painter.Fill();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (hover.Clear()) MarkDirtyRepaint();
            if (ESWorkbenchUIToolkitHost.IsExternalContentDragActive
                || context.PointerCoordinator.IsExternalContentActive)
            {
                if (gestureSession.IsActive || panning || moving)
                    CancelInteraction();
                evt.StopImmediatePropagation();
                return;
            }
            Focus();
            Vector2 local = this.WorldToLocal(evt.position);
            UpdatePointerWorldStatus(local);
            if (gestureSession.IsActive)
            {
                if (!context.PointerCoordinator.Owns(
                        this, evt.pointerId, ESWorkbenchPointerOwnerKind.Viewport))
                    CancelInteraction();
                evt.StopImmediatePropagation();
                return;
            }
            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                if (!context.PointerCoordinator.TryAcquire(
                        this,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Viewport))
                {
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!gestureSession.TryArm(
                        ESWorkbenchPointerGestureSession.Kind.Pan, evt.pointerId, local))
                {
                    context.PointerCoordinator.Release(
                        this,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Viewport);
                    evt.StopImmediatePropagation();
                    return;
                }
                panning = true;
                panPointerId = evt.pointerId;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }
            if (evt.button != 0) return;
            ESWorkbenchHierarchyDescriptor hit = HitTest(local);
            ESWorkbenchSelection hitSelection = hit?.ToSelection();
            ESWorkbenchToolCapabilities toolCapabilities = context.Actions.Tools.ActiveCapabilities;
            ESWorkbenchPointerIntentDecision intentDecision = ESWorkbenchPointerIntentResolver.ResolveDecision(
                new ESWorkbenchPointerIntentContext(
                externalContentDragActive: ESWorkbenchUIToolkitHost.IsExternalContentDragActive,
                navigationGestureActive: gestureSession.IsActive,
                toolCapabilities: toolCapabilities,
                viewportCapabilities: ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move,
                targetCapabilities: ESWorkbenchToolCapabilityResolver.Has(
                        toolCapabilities, ESWorkbenchToolCapabilities.Select)
                    && hitSelection != null && !hitSelection.IsEmpty
                    ? ESWorkbenchToolCapabilityResolver.ResolveTarget(
                        context.Actions.Authoring.CanMove(hitSelection), false, false)
                    : ESWorkbenchToolCapabilities.Select,
                hasHitTarget: hitSelection != null && !hitSelection.IsEmpty,
                hierarchyLocked: hitSelection != null && context.IsHierarchyLocked(hitSelection.StableId),
                groundActionEnabled: false));
            if (!intentDecision.CanStart)
            {
                evt.StopImmediatePropagation();
                return;
            }
            ESWorkbenchPointerIntentKind intent = intentDecision.Intent;
            bool additiveSelection = evt.shiftKey || evt.ctrlKey || evt.commandKey;
            bool toggleSelection = evt.ctrlKey || evt.commandKey;
            if (additiveSelection && intent == ESWorkbenchPointerIntentKind.Manipulate)
                intent = ESWorkbenchPointerIntentKind.Select;
            if (intent == ESWorkbenchPointerIntentKind.None)
            {
                evt.StopImmediatePropagation();
                return;
            }
            if (hitSelection == null || hitSelection.IsEmpty)
            {
                if (intent == ESWorkbenchPointerIntentKind.Select
                    && !additiveSelection && !toggleSelection)
                    context.Selection.Clear();
                evt.StopPropagation();
                return;
            }
            context.Selection.Select(hitSelection, additiveSelection, toggleSelection);
            if (intent == ESWorkbenchPointerIntentKind.Manipulate)
            {
                if (!context.PointerCoordinator.TryAcquire(
                        this,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Viewport))
                {
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!gestureSession.TryArm(
                        ESWorkbenchPointerGestureSession.Kind.Move, evt.pointerId, local))
                {
                    context.PointerCoordinator.Release(
                        this,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Viewport);
                    evt.StopImmediatePropagation();
                    return;
                }
                moving = true;
                movePointerId = evt.pointerId;
                movingSelection = context.Selection.Current;
                movingSpatial = hit.Spatial;
                pendingMove = hit.Spatial.Position;
                moveOrigin = hit.Spatial.Position;
                if (!TryCanvasToWorld(local, out Vector3 pointerWorld)
                    || !moveAnchor.Capture(hit.Spatial.Position, pointerWorld))
                {
                    StopMoving();
                    gestureSession.Cancel();
                    context.PointerCoordinator.Release(
                        this,
                        evt.pointerId,
                        ESWorkbenchPointerOwnerKind.Viewport);
                    evt.StopImmediatePropagation();
                    return;
                }
                pendingMoveValid = false;
                this.CapturePointer(evt.pointerId);
            }
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (ESWorkbenchUIToolkitHost.IsExternalContentDragActive
                || context.PointerCoordinator.IsExternalContentActive)
            {
                if (hover.Clear()) MarkDirtyRepaint();
                if (panning || moving) CancelInteraction();
                evt.StopImmediatePropagation();
                return;
            }
            Vector2 local = this.WorldToLocal(evt.position);
            UpdatePointerWorldStatus(local);
            if (((moving && evt.pointerId == movePointerId)
                    || (panning && evt.pointerId == panPointerId))
                && !context.PointerCoordinator.Owns(
                    this, evt.pointerId, ESWorkbenchPointerOwnerKind.Viewport))
            {
                CancelInteraction();
                evt.StopImmediatePropagation();
                return;
            }
            if (!panning && !moving)
            {
                ESWorkbenchHierarchyDescriptor hoveredItem = contentRect.Contains(local) ? HitTest(local) : null;
                if (hover.Update(hoveredItem?.ItemId)) MarkDirtyRepaint();
            }
            if (moving && evt.pointerId == movePointerId
                && context.PointerCoordinator.Owns(
                    this, evt.pointerId, ESWorkbenchPointerOwnerKind.Viewport)
                && this.HasPointerCapture(evt.pointerId))
            {
                if (UpdateMovePreview(local, evt.shiftKey)) MarkDirtyRepaint();
                BeginEdgePan(local, evt.shiftKey);
                evt.StopPropagation();
                return;
            }
            if (!panning || evt.pointerId != panPointerId
                || !context.PointerCoordinator.Owns(
                    this, evt.pointerId, ESWorkbenchPointerOwnerKind.Viewport)
                || !this.HasPointerCapture(evt.pointerId)) return;
            if (!gestureSession.TryAdvance(
                    evt.pointerId,
                    local,
                    final: false,
                    out ESWorkbenchPointerGestureSession.AdvanceResult advance))
            {
                if (!advance.IsStarted)
                {
                    evt.StopPropagation();
                    return;
                }
                StopPanning();
                gestureSession.Cancel(ESWorkbenchPointerGestureSession.EndReason.CaptureLost);
                context.PointerCoordinator.Release(
                    this,
                    evt.pointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
                evt.StopPropagation();
                return;
            }
            navigation.PanBy(advance.Delta);
            navigation.ConstrainPan(contentRect, ResolveWorldBounds(), feel.CanvasOverscrollPixels);
            UpdatePointerWorldStatus(local);
            UpdateLabelPositions();
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            bool ownsViewportPointer = context.PointerCoordinator.Owns(
                this, evt.pointerId, ESWorkbenchPointerOwnerKind.Viewport);
            bool isCurrentGesturePointer =
                (moving && evt.pointerId == movePointerId)
                || (panning && evt.pointerId == panPointerId);
            if (isCurrentGesturePointer && !ownsViewportPointer)
            {
                CancelInteraction();
                evt.StopImmediatePropagation();
                return;
            }
            if (moving && evt.pointerId == movePointerId)
            {
                bool ownsGesture = gestureSession.Owns(
                    ESWorkbenchPointerGestureSession.Kind.Move, evt.pointerId);
                if (ownsGesture)
                    UpdateMovePreview(this.WorldToLocal(evt.position), evt.shiftKey);
                ESWorkbenchSelection target = movingSelection;
                Vector3 world = pendingMove;
                bool commit = ownsGesture && pendingMoveValid;
                StopMoving();
                gestureSession.TryFinishOwned(
                    evt.pointerId,
                    ESWorkbenchPointerGestureSession.EndReason.Commit);
                context.PointerCoordinator.Release(
                    this,
                    evt.pointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
                if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
                if (commit) context.Actions.Authoring.TryMove(target, world, out _);
                evt.StopPropagation();
                return;
            }
            if (!panning || evt.pointerId != panPointerId) return;
            Vector2 panLocal = this.WorldToLocal(evt.position);
            if (gestureSession.TryAdvance(
                    evt.pointerId,
                    panLocal,
                    final: true,
                    out ESWorkbenchPointerGestureSession.AdvanceResult advance))
            {
                navigation.PanBy(advance.Delta);
                navigation.ConstrainPan(contentRect, ResolveWorldBounds(), feel.CanvasOverscrollPixels);
                UpdatePointerWorldStatus(panLocal);
                UpdateLabelPositions();
            }
            StopPanning();
            gestureSession.TryFinishOwned(
                evt.pointerId,
                ESWorkbenchPointerGestureSession.EndReason.Commit);
            context.PointerCoordinator.Release(
                this,
                evt.pointerId,
                ESWorkbenchPointerOwnerKind.Viewport);
            if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (gestureSession.IsActive || panning || moving)
                CancelInteraction();
            evt.StopImmediatePropagation();
        }

        private bool UpdateMovePreview(Vector2 local, bool lockDominantAxis)
        {
            if (!moving || !gestureSession.Owns(
                    ESWorkbenchPointerGestureSession.Kind.Move, movePointerId)) return false;
            if (!gestureSession.TryEnsureStarted(movePointerId, local)) return false;
            bool invalidatesPreviousPreview = pendingMoveValid;
            pendingMoveValid = false;
            if (!TryCanvasToWorld(local, out Vector3 pointerWorld)) return invalidatesPreviousPreview;
            if (!moveAnchor.TryResolve(
                    pointerWorld,
                    context.SnapPosition,
                    ESWorkbenchMoveAxes.Horizontal,
                    lockDominantAxis,
                    out pendingMove)) return invalidatesPreviousPreview;
            pendingMoveValid = true;
            return true;
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == panPointerId) StopPanning();
            if (evt.pointerId == movePointerId) StopMoving();
            if (gestureSession.PointerId == evt.pointerId)
                gestureSession.TryFinishOwned(
                    evt.pointerId,
                    ESWorkbenchPointerGestureSession.EndReason.CaptureLost);
            context.PointerCoordinator.Release(
                this,
                evt.pointerId,
                ESWorkbenchPointerOwnerKind.Viewport);
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            ClearPointerWorldStatus();
            if (!panning && !moving && hover.Clear()) MarkDirtyRepaint();
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            if (!gestureSession.IsActive && !panning && !moving)
            {
                ClearPointerWorldStatus();
                if (hover.Clear()) MarkDirtyRepaint();
                return;
            }

            int capturedPanPointerId = panPointerId;
            int capturedMovePointerId = movePointerId;
            StopPanning();
            StopMoving();
            gestureSession.Finish(ESWorkbenchPointerGestureSession.EndReason.CaptureLost);
            if (capturedPanPointerId >= 0)
                context.PointerCoordinator.Release(
                    this,
                    capturedPanPointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
            if (capturedMovePointerId >= 0)
                context.PointerCoordinator.Release(
                    this,
                    capturedMovePointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
            if (capturedPanPointerId >= 0 && this.HasPointerCapture(capturedPanPointerId))
                this.ReleasePointer(capturedPanPointerId);
            if (capturedMovePointerId >= 0 && this.HasPointerCapture(capturedMovePointerId))
                this.ReleasePointer(capturedMovePointerId);
            hover.Clear();
            ClearPointerWorldStatus();
            MarkDirtyRepaint();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || (!panning && !moving)) return;
            CancelInteraction();
            evt.StopPropagation();
        }

        public void CancelInteraction()
        {
            hover.Clear();
            ClearPointerWorldStatus();
            int capturedPanPointerId = panPointerId;
            int capturedMovePointerId = movePointerId;
            gestureSession.Cancel();
            StopPanning();
            StopMoving();
            if (capturedPanPointerId >= 0)
                context.PointerCoordinator.Release(
                    this,
                    capturedPanPointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
            if (capturedMovePointerId >= 0)
                context.PointerCoordinator.Release(
                    this,
                    capturedMovePointerId,
                    ESWorkbenchPointerOwnerKind.Viewport);
            if (capturedPanPointerId >= 0 && this.HasPointerCapture(capturedPanPointerId))
                this.ReleasePointer(capturedPanPointerId);
            if (capturedMovePointerId >= 0 && this.HasPointerCapture(capturedMovePointerId))
                this.ReleasePointer(capturedMovePointerId);
            MarkDirtyRepaint();
        }

        private void OnWheel(WheelEvent evt)
        {
            hover.Clear();
            if (!ESWorkbenchInteractionPolicy.ShouldHandleNavigation(
                    ESWorkbenchUIToolkitHost.IsExternalContentDragActive
                        || context.PointerCoordinator.IsExternalContentActive,
                    gestureSession.IsActive))
            {
                evt.StopPropagation();
                return;
            }
            Vector2 local = this.WorldToLocal(evt.mousePosition);
            Rect worldBounds = ResolveWorldBounds();
            navigation.ZoomAt(local, evt.delta.y, contentRect, worldBounds);
            navigation.ConstrainPan(contentRect, worldBounds, feel.CanvasOverscrollPixels);
            UpdateLabelPositions();
            context.StatusChanged?.Invoke();
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void UpdatePointerWorldStatus(Vector2 local)
        {
            if (!contentRect.Contains(local)
                || !TryCanvasToWorld(local, out Vector3 next))
            {
                ClearPointerWorldStatus();
                return;
            }
            if (pointerWorldValid && (pointerWorld - next).sqrMagnitude <= 0.0001f) return;
            pointerWorld = next;
            pointerWorldValid = true;
            context.StatusChanged?.Invoke();
        }

        private void ClearPointerWorldStatus()
        {
            if (!pointerWorldValid) return;
            pointerWorldValid = false;
            pointerWorld = default;
            context.StatusChanged?.Invoke();
        }

        private ESWorkbenchHierarchyDescriptor HitTest(Vector2 local)
        {
            Rect worldBounds = ResolveWorldBounds();
            Rect canvasBounds = ResolveCanvasBounds(contentRect, worldBounds);
            return ESWorkbenchSpatialHitResolver.HitTest2D(
                projected,
                local,
                worldBounds,
                canvasBounds,
                feel.SelectionHitRadiusPixels);
        }

        private Rect ResolveWorldBounds()
        {
            if (projected.Count == 0) return new Rect(-5f, -5f, 10f, 10f);
            Bounds first = projected[0].Spatial.Bounds;
            float minX = first.min.x;
            float maxX = first.max.x;
            float minZ = first.min.z;
            float maxZ = first.max.z;
            for (int i = 1; i < projected.Count; i++)
            {
                Bounds bounds = projected[i].Spatial.Bounds;
                minX = Mathf.Min(minX, bounds.min.x);
                maxX = Mathf.Max(maxX, bounds.max.x);
                minZ = Mathf.Min(minZ, bounds.min.z);
                maxZ = Mathf.Max(maxZ, bounds.max.z);
            }
            float width = Mathf.Max(1f, maxX - minX);
            float height = Mathf.Max(1f, maxZ - minZ);
            float padding = Mathf.Max(1f, Mathf.Max(width, height) * 0.08f);
            return Rect.MinMaxRect(minX - padding, minZ - padding, maxX + padding, maxZ + padding);
        }

        private Rect ResolveCanvasBounds(Rect viewport, Rect worldBounds)
        {
            return navigation.ResolveCanvasBounds(viewport, worldBounds);
        }

        private static Vector2 WorldToCanvas(Vector3 value, Rect worldBounds, Rect canvasBounds)
        {
            return ESWorkbenchCanvasNavigationState.WorldToCanvas(
                new Vector2(value.x, value.z), worldBounds, canvasBounds);
        }

        private static Vector2 WorldSizeToCanvas(Vector3 size, Rect worldBounds, Rect canvasBounds)
        {
            return new Vector2(
                size.x / Mathf.Max(0.001f, worldBounds.width) * canvasBounds.width,
                size.z / Mathf.Max(0.001f, worldBounds.height) * canvasBounds.height);
        }

        private Vector3 CanvasToWorld(Vector2 value)
        {
            return TryCanvasToWorld(value, out Vector3 world)
                ? world
                : Vector3.zero;
        }

        private bool TryCanvasToWorld(Vector2 value, out Vector3 world)
        {
            Rect worldBounds = ResolveWorldBounds();
            float y = movingSelection?.StableId != null
                ? projected.FirstOrDefault(item => item.ItemId == movingSelection.StableId)?.Spatial?.Position.y ?? 0f
                : 0f;
            return navigation.TryCanvasToWorld(value, worldBounds, contentRect, y, out world);
        }

        private void UpdateLabelPositions()
        {
            if (labelOverlay == null || contentRect.width <= 1f || contentRect.height <= 1f) return;
            Rect worldBounds = ResolveWorldBounds();
            Rect canvasBounds = ResolveCanvasBounds(contentRect, worldBounds);
            labelOverlay.Query<Label>("ESWorkbenchSpatialLabel").ForEach(label =>
            {
                ESWorkbenchHierarchyDescriptor item = label.userData as ESWorkbenchHierarchyDescriptor;
                if (item?.Spatial == null) return;
                Vector2 position = WorldToCanvas(item.Spatial.Position, worldBounds, canvasBounds);
                label.style.left = position.x + 8f;
                label.style.top = position.y - 10f;
            });
        }

        private void StopPanning()
        {
            panning = false;
            panPointerId = -1;
        }

        private void BeginEdgePan(Vector2 local, bool lockDominantAxis)
        {
            double now = EditorApplication.timeSinceStartup;
            if (!edgePanSession.IsActive)
                edgePanSession.Begin(local, lockDominantAxis, now);
            else
                edgePanSession.UpdatePointer(local, lockDominantAxis);
            if (moving && gestureSession.IsStarted && edgePanSchedule?.isActive == false)
            {
                edgePanSchedule.Resume();
            }
        }

        private void ApplyEdgePan()
        {
            if (!moving || !edgePanSession.IsActive || !gestureSession.IsStarted
                || !context.PointerCoordinator.Owns(
                    this, movePointerId, ESWorkbenchPointerOwnerKind.Viewport)) return;
            double now = EditorApplication.timeSinceStartup;
            if (!edgePanSession.TryAdvance(now, out float deltaTime)) return;
            if (!edgePan.Evaluate(
                    contentRect, edgePanSession.Pointer, deltaTime, out Vector2 delta)) return;
            navigation.PanBy(delta);
            navigation.ConstrainPan(contentRect, ResolveWorldBounds(), feel.CanvasOverscrollPixels);
            UpdatePointerWorldStatus(edgePanSession.Pointer);
            UpdateLabelPositions();
            UpdateMovePreview(edgePanSession.Pointer, edgePanSession.LockDominantAxis);
            MarkDirtyRepaint();
        }

        public bool TryEdgePan(Vector2 localPosition, float deltaTime)
        {
            if (!edgePan.Evaluate(contentRect, localPosition, deltaTime, out Vector2 delta)) return false;
            navigation.PanBy(delta);
            navigation.ConstrainPan(contentRect, ResolveWorldBounds(), feel.CanvasOverscrollPixels);
            UpdatePointerWorldStatus(localPosition);
            context.StatusChanged?.Invoke();
            UpdateLabelPositions();
            MarkDirtyRepaint();
            return true;
        }

        public bool TryNudge(KeyCode keyCode, bool shift, bool controlOrCommand, out string message)
        {
            message = string.Empty;
            ESWorkbenchSelection selection = context.Selection.Current;
            if (!ESWorkbenchNudgeResolver.TryResolveDelta(
                    keyCode, shift, controlOrCommand, feel, out Vector3 delta)
                || !ESWorkbenchNudgeResolver.TryResolvePosition(
                    context.Hierarchy, selection, out Vector3 position)
                || context.IsHierarchyLocked(selection.StableId)
                || !context.Actions.Authoring.CanMove(selection)) return false;
            Vector3 target = context.SnapPosition(position + delta);
            bool committed = context.Actions.Authoring.TryMove(selection, target, out message);
            if (committed) Refresh(ESWorkbenchRefreshReason.DataChanged);
            return committed;
        }

        private void StopMoving()
        {
            moving = false;
            movePointerId = -1;
            edgePanSchedule?.Pause();
            pendingMoveValid = false;
            movingSelection = null;
            movingSpatial = null;
            moveAnchor.Reset();
            edgePanSession.Stop();
            MarkDirtyRepaint();
        }

        private static void FillRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(rect.max);
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private static void StrokeRect(Painter2D painter, Rect rect, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(rect.max);
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Stroke();
        }

        private static void DrawCrosshair(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(center - Vector2.right * radius);
            painter.LineTo(center + Vector2.right * radius);
            painter.MoveTo(center - Vector2.up * radius);
            painter.LineTo(center + Vector2.up * radius);
            painter.Stroke();
        }

        public void Dispose()
        {
            edgePanSchedule?.Pause();
            edgePanSchedule = null;
            CancelInteraction();
            generateVisualContent -= DrawCanvas;
            projected.Clear();
            labelOverlay.Clear();
        }
    }

    internal sealed class ESWorkbenchPreview3DViewport : IESWorkbenchViewport, IESWorkbenchCancelableViewport, IESWorkbenchFrameableViewport,
        IESWorkbenchEdgePannableViewport, IESWorkbenchNudgeableViewport, IESWorkbenchViewportStatusProvider
    {
        private readonly ESWorkbenchViewportContext context;
        private readonly VisualElement root;
        private readonly IMGUIContainer renderHost;
        private readonly List<GameObject> instances = new List<GameObject>();
        private readonly List<ESWorkbenchHierarchyDescriptor> instanceItems = new List<ESWorkbenchHierarchyDescriptor>();
        private readonly ESWorkbenchRendererBoundsCache instanceBounds =
            new ESWorkbenchRendererBoundsCache();
        private readonly ESWorkbenchOrbitCameraState cameraNavigation;
        private readonly ESWorkbenchIMGUIOrbitInput orbitInput;
        private readonly ESWorkbenchEdgePanController edgePan;
        private readonly ESWorkbenchViewportFeelSettings feel;
        private readonly ESWorkbenchHoverState hover = new ESWorkbenchHoverState();
        private ESEditorPreviewRenderContext preview;
        private bool moving;
        private bool rotating;
        private bool scaling;
        private bool pendingMoveValid;
        private Vector3 pendingMove;
        private readonly ESWorkbenchPointerGestureSession gestureSession;
        private readonly ESWorkbenchMoveGestureAnchor moveAnchor;
        private readonly ESWorkbenchTransformGestureSession transformGesture;
        private Vector3 transformStartValue;
        private Vector3 pendingTransformValue;
        private bool pendingTransformValid;
        private ESWorkbenchSelection movingSelection;
        private int activeControlId;
        private readonly ESWorkbenchEdgePanSession edgePanSession =
            new ESWorkbenchEdgePanSession();
        private IVisualElementScheduledItem edgePanSchedule;
        private readonly List<ESWorkbenchViewportStatusDescriptor> statusSnapshot =
            new List<ESWorkbenchViewportStatusDescriptor>();
        private Vector3 pointerWorld;
        private bool pointerWorldValid;

        public ESWorkbenchPreview3DViewport(
            ESWorkbenchViewportContext context,
            ESWorkbenchViewportFeelSettings feel = null)
        {
            this.context = context;
            this.feel = feel ?? context?.Feel ?? ESWorkbenchViewportFeelSettings.Standard;
            orbitInput = new ESWorkbenchIMGUIOrbitInput(
                this.feel,
                this.feel.VerticalFieldOfViewDegrees,
                context?.PointerCoordinator);
            edgePan = new ESWorkbenchEdgePanController(this.feel.EdgePanSettings);
            gestureSession = new ESWorkbenchPointerGestureSession(
                this.feel.DragStartPixels, this.feel);
            moveAnchor = new ESWorkbenchMoveGestureAnchor();
            transformGesture = new ESWorkbenchTransformGestureSession(this.feel);
            cameraNavigation = new ESWorkbenchOrbitCameraState(
                context?.Layout,
                Vector3.zero,
                8f,
                35f,
                25f,
                -80f,
                80f,
                0.3f,
                5000f,
                this.feel,
                presentationRadiusScale: this.feel.PresentationRadiusScale);
            root = new VisualElement { name = "ESWorkbenchPreview3D" };
            root.style.flexGrow = 1f;
            root.style.minWidth = 0f;
            root.style.minHeight = 0f;
            renderHost = new IMGUIContainer(DrawPreview);
            renderHost.style.flexGrow = 1f;
            renderHost.style.minWidth = 0f;
            renderHost.style.minHeight = 0f;
            renderHost.tooltip = "右键旋转视角，中键平移，滚轮缩放；选择变换工具后拖动对象提交作者事务。";
            renderHost.RegisterCallback<FocusOutEvent>(_ => Deactivate());
            renderHost.RegisterCallback<PointerCancelEvent>(_ => Deactivate());
            edgePanSchedule = root.schedule.Execute(ApplyEdgePan).Every(16);
            edgePanSchedule.Pause();
            root.Add(renderHost);
            EnsurePreview();
            RebuildInstances(true);
        }

        public VisualElement Root => root;
        public void Activate() => renderHost.MarkDirtyRepaint();

        public IReadOnlyList<ESWorkbenchViewportStatusDescriptor> GetStatusSnapshot()
        {
            statusSnapshot.Clear();
            if (pointerWorldValid)
                statusSnapshot.Add(new ESWorkbenchViewportStatusDescriptor(
                    "preview3d.pointer-coordinate",
                    "指针",
                    pointerWorld.x.ToString("0.##") + ", "
                        + pointerWorld.y.ToString("0.##") + ", "
                        + pointerWorld.z.ToString("0.##"),
                    "鼠标当前落点的作者平面坐标；移出视口后清除",
                    450));
            statusSnapshot.Add(new ESWorkbenchViewportStatusDescriptor(
                "preview3d.camera-focus",
                "焦点",
                cameraNavigation.Focus.x.ToString("0.##") + ", "
                    + cameraNavigation.Focus.y.ToString("0.##") + ", "
                    + cameraNavigation.Focus.z.ToString("0.##"),
                "轨道相机当前关注点",
                340));
            statusSnapshot.Add(new ESWorkbenchViewportStatusDescriptor(
                "preview3d.camera-pose",
                "相机",
                "距离 " + cameraNavigation.Distance.ToString("0.##")
                    + " · 偏航 " + cameraNavigation.Yaw.ToString("0.#")
                    + " · 俯仰 " + cameraNavigation.Pitch.ToString("0.#"),
                "轨道相机距离、偏航和俯仰",
                330));
            return statusSnapshot;
        }

        public void Deactivate()
        {
            hover.Clear();
            ClearPointerWorldStatus();
            bool restorePreview = pendingMoveValid || pendingTransformValid;
            orbitInput.Release();
            StopMoving();
            gestureSession.Cancel(ESWorkbenchPointerGestureSession.EndReason.Deactivate);
            context?.PointerCoordinator?.Release(
                this,
                0,
                ESWorkbenchPointerOwnerKind.Viewport);
            ReleaseMouseControl();
            if (restorePreview) RebuildInstances();
            renderHost.MarkDirtyRepaint();
        }
        public void CancelInteraction() => Deactivate();
        public void Refresh(ESWorkbenchRefreshReason reason)
        {
            if (reason != ESWorkbenchRefreshReason.SelectionChanged)
                RebuildInstances(reason == ESWorkbenchRefreshReason.Initial
                    || reason == ESWorkbenchRefreshReason.AssetChanged);
            renderHost.MarkDirtyRepaint();
        }

        public void FrameAll()
        {
            Bounds aggregate = CalculateAllBounds();
            cameraNavigation.FrameRecommended(aggregate, 2.5f, 2f, 35f, 25f);
            context.StatusChanged?.Invoke();
            renderHost.MarkDirtyRepaint();
        }

        public bool CanAccept(ESWorkbenchObjectDescriptor item) =>
            item?.Source is GameObject && context.Actions.Authoring.CanCreate(item);

        public bool TryAccept(ESWorkbenchDropContext drop, out string message)
        {
            message = string.Empty;
            if (drop?.Item == null || !CanAccept(drop.Item))
            {
                message = "当前领域没有为该 GameObject 注册创建事务。";
                return false;
            }
            if (!TryResolveWorldPoint(drop.LocalPosition, out Vector3 worldPosition))
            {
                message = "无法把拖放位置投影到 3D 作者平面。";
                return false;
            }
            if (!context.Actions.Authoring.TryCreate(drop.Item, context.SnapPosition(worldPosition), out message)) return false;
            return true;
        }

        private void RebuildInstances(bool frameContent = false)
        {
            hover.Clear();
            ClearInstances();
            EnsurePreview();
            IReadOnlyList<ESWorkbenchHierarchyDescriptor> hierarchy = context.Hierarchy;
            if (hierarchy == null) return;
            for (int i = 0; i < hierarchy.Count; i++)
            {
                ESWorkbenchHierarchyDescriptor item = hierarchy[i];
                ESWorkbenchSpatialDescriptor spatial = item?.Spatial;
                if (spatial == null || !spatial.VisibleIn3D || !(item.UnityObject is GameObject source)) continue;
                ESEditorPreviewModelHandle handle = preview.CreateModelGroup(
                    source, item.DisplayName + " · Preview", samplingTarget: false);
                GameObject instance = handle?.Instance;
                if (instance == null) continue;
                instance.transform.SetPositionAndRotation(
                    preview.GroupOrigin + spatial.Position,
                    Quaternion.Euler(spatial.RotationEuler));
                instance.transform.localScale = spatial.Size;
                instances.Add(instance);
                instanceItems.Add(item);
            }
            if (instances.Count > 0)
            {
                Bounds aggregate = CalculateAllBounds();
                preview.ConfigureGroundPlane(
                    aggregate.center,
                    Mathf.Max(25f, Mathf.Max(aggregate.size.x, aggregate.size.z) * 1.2f));
                if (frameContent) FrameAll();
            }
        }

        private bool TryResolveWorldPoint(Vector2 localPosition, out Vector3 worldPosition)
        {
            Rect rect = renderHost.contentRect;
            return TryResolveWorldPoint(rect, localPosition, false, out worldPosition);
        }

        private bool TryResolveWorldPoint(
            Rect rect,
            Vector2 localPosition,
            out Vector3 worldPosition)
        {
            return TryResolveWorldPoint(rect, localPosition, false, out worldPosition);
        }

        private bool TryResolveWorldPoint(
            Rect rect,
            Vector2 localPosition,
            bool allowOutside,
            out Vector3 worldPosition)
        {
            worldPosition = default;
            if (preview?.Camera == null || rect.width <= 1f || rect.height <= 1f) return false;
            Rect interactionRect = ESWorkbenchViewportOverlay.GetInteractionRect(rect);
            if (!ESWorkbenchCameraViewportProjection.TryNormalize(
                    rect, interactionRect, localPosition, out Vector3 viewportPoint, allowOutside))
                return false;
            Ray ray = preview.Camera.ViewportPointToRay(viewportPoint);
            Plane plane = new Plane(Vector3.up, preview.GroupOrigin);
            if (!plane.Raycast(ray, out float hitDistance)) return false;
            worldPosition = ray.GetPoint(hitDistance) - preview.GroupOrigin;
            return true;
        }

        private void UpdatePointerWorldStatus(
            Rect rect,
            Vector2 localPosition,
            bool allowOutside)
        {
            if (!TryResolveWorldPoint(rect, localPosition, allowOutside, out Vector3 next))
            {
                ClearPointerWorldStatus();
                return;
            }
            if (pointerWorldValid && (pointerWorld - next).sqrMagnitude <= 0.0001f) return;
            pointerWorld = next;
            pointerWorldValid = true;
            context.StatusChanged?.Invoke();
        }

        private void ClearPointerWorldStatus()
        {
            if (!pointerWorldValid) return;
            pointerWorldValid = false;
            pointerWorld = default;
            context.StatusChanged?.Invoke();
        }

        private bool EnsurePreview()
        {
            if (preview?.IsReady == true) return false;
            preview?.Dispose();
            preview = new ESEditorPreviewRenderContext(
                "ES Workbench 3D Viewport",
                ESEditorPreviewSceneMode.PreviewScene);
            preview.Ensure();
            if (preview.Camera != null)
            {
                preview.Camera.fieldOfView = feel.VerticalFieldOfViewDegrees;
                preview.Camera.backgroundColor = new Color(0.045f, 0.052f, 0.058f, 1f);
            }
            return true;
        }

        private void DrawPreview()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            bool recreated = EnsurePreview();
            if (recreated && instances.Count > 0) RebuildInstances();
            preview.RenderGUI(
                rect,
                new ESEditorPreviewCameraPose(
                    preview.GroupOrigin + cameraNavigation.Focus,
                    1f,
                        cameraNavigation.Yaw,
                        cameraNavigation.Pitch,
                        cameraNavigation.ResolvePresentationRadius()),
                ESEditorPreviewRenderOptions.Balanced);
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleInput(rect, controlId);
            DrawHoverOutline();
            DrawTransformTargetOutline();
            ESWorkbenchViewportOverlay.DrawNavigationToolbar(
                rect,
                cameraNavigation,
                "三维作者视图",
                context.Selection.Current == null ? "未选择对象" : context.Selection.Current.StableId,
                FrameAll);
        }

        private void HandleInput(Rect rect, int controlId)
        {
            Event evt = Event.current;
            if (ESWorkbenchUIToolkitHost.IsExternalContentDragActive
                || context.PointerCoordinator.IsExternalContentActive)
            {
                ClearPointerWorldStatus();
                if (hover.Clear()) renderHost.MarkDirtyRepaint();
                if (orbitInput.IsCapturing || moving || rotating || scaling)
                {
                    Deactivate();
                    Activate();
                }
                return;
            }
            if ((moving || rotating || scaling)
                && !context.PointerCoordinator.Owns(
                    this, 0, ESWorkbenchPointerOwnerKind.Viewport))
            {
                Deactivate();
                return;
            }
            Rect interactionRect = ESWorkbenchViewportOverlay.GetInteractionRect(rect);
            if ((evt.type == EventType.MouseLeaveWindow || !interactionRect.Contains(evt.mousePosition))
                && !orbitInput.IsCapturing && !moving && !rotating && !scaling)
            {
                ClearPointerWorldStatus();
                if (hover.Clear()) renderHost.MarkDirtyRepaint();
            }
            if (!interactionRect.Contains(evt.mousePosition) && !orbitInput.IsCapturing
                && !moving && !rotating && !scaling) return;
            if (evt.type == EventType.MouseMove
                || evt.type == EventType.MouseDown
                || evt.type == EventType.MouseDrag)
                UpdatePointerWorldStatus(
                    rect,
                    evt.mousePosition,
                    orbitInput.IsCapturing || moving || rotating || scaling);
            if (evt.type == EventType.MouseMove && !orbitInput.IsCapturing
                && !moving && !rotating && !scaling)
            {
                TryHitItem(rect, evt.mousePosition, out ESWorkbenchHierarchyDescriptor hoveredItem);
                if (hover.Update(hoveredItem?.ItemId)) renderHost.MarkDirtyRepaint();
            }
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape
                && (moving || rotating || scaling))
            {
                bool restorePreview = pendingMoveValid || pendingTransformValid;
                StopMoving();
                gestureSession.Cancel();
                ReleaseMouseControl();
                if (restorePreview) RebuildInstances();
                evt.Use();
                return;
            }
            if (evt.type == EventType.Ignore && (orbitInput.IsCapturing || moving || rotating || scaling))
            {
                Deactivate();
                return;
            }

            // IMGUI 没有 PointerCaptureOut；当另一个编辑器控件抢走 hotControl
            // 时，当前变换也必须按捕获丢失处理，避免临时预览残留在预览实例上。
            if (evt.type != EventType.MouseDown
                && evt.type != EventType.MouseUp
                && evt.type != EventType.Ignore
                && activeControlId != 0
                && GUIUtility.hotControl != activeControlId
                && (orbitInput.IsCapturing || moving || rotating || scaling))
            {
                Deactivate();
                return;
            }

            ESWorkbenchOrbitInputResult cameraResult = ESWorkbenchInteractionPolicy.ShouldHandleNavigation(
                    ESWorkbenchUIToolkitHost.IsExternalContentDragActive
                        || context.PointerCoordinator.IsExternalContentActive,
                    gestureSession.IsActive)
                ? orbitInput.Handle(interactionRect, rect, cameraNavigation, controlId)
                : ESWorkbenchOrbitInputResult.None;
            if (cameraResult != ESWorkbenchOrbitInputResult.None)
            {
                hover.Clear();
                if (cameraResult == ESWorkbenchOrbitInputResult.Orbit
                    || cameraResult == ESWorkbenchOrbitInputResult.Pan
                    || cameraResult == ESWorkbenchOrbitInputResult.Zoom)
                {
                    UpdatePointerWorldStatus(
                        rect,
                        evt.mousePosition,
                        orbitInput.IsCapturing || moving || rotating || scaling);
                    context.StatusChanged?.Invoke();
                    renderHost.MarkDirtyRepaint();
                }
                return;
            }
            if (evt.type == EventType.MouseDown)
            {
                hover.Clear();
                if (gestureSession.IsActive)
                {
                    evt.Use();
                    return;
                }
                if (evt.button == 0)
                {
                    bool hasHit = TryHitItem(
                        rect, evt.mousePosition, out ESWorkbenchHierarchyDescriptor item);
                    ESWorkbenchSelection hitSelection = hasHit ? item.ToSelection() : null;
                    ESWorkbenchToolCapabilities toolCapabilities = context.Actions.Tools.ActiveCapabilities;
                    ESWorkbenchToolCapabilities targetCapabilities = hitSelection == null
                        || !ESWorkbenchToolCapabilityResolver.Has(
                            toolCapabilities, ESWorkbenchToolCapabilities.Select)
                        ? ESWorkbenchToolCapabilities.Select
                        : ESWorkbenchToolCapabilityResolver.ResolveTarget(
                            context.Actions.Authoring.CanMove(hitSelection),
                            context.Actions.Authoring.CanRotate(hitSelection),
                            context.Actions.Authoring.CanScale(hitSelection));
                    bool moveTool = ESWorkbenchToolCapabilityResolver.Has(
                            toolCapabilities, ESWorkbenchToolCapabilities.Move)
                        && (toolCapabilities & (ESWorkbenchToolCapabilities.Rotate
                            | ESWorkbenchToolCapabilities.Scale)) == 0;
                    bool rotateTool = ESWorkbenchToolCapabilityResolver.Has(toolCapabilities, ESWorkbenchToolCapabilities.Rotate);
                    bool scaleTool = ESWorkbenchToolCapabilityResolver.Has(toolCapabilities, ESWorkbenchToolCapabilities.Scale);
                    ESWorkbenchPointerIntentDecision intentDecision = ESWorkbenchPointerIntentResolver.ResolveDecision(
                        new ESWorkbenchPointerIntentContext(
                        externalContentDragActive: ESWorkbenchUIToolkitHost.IsExternalContentDragActive,
                        navigationGestureActive: gestureSession.IsActive,
                        toolCapabilities: toolCapabilities,
                        viewportCapabilities: ESWorkbenchToolCapabilities.Select
                            | ESWorkbenchToolCapabilities.Move
                            | ESWorkbenchToolCapabilities.Rotate
                            | ESWorkbenchToolCapabilities.Scale,
                        targetCapabilities: targetCapabilities,
                        hasHitTarget: hasHit,
                        hierarchyLocked: hitSelection != null
                            && context.IsHierarchyLocked(hitSelection.StableId),
                        groundActionEnabled: false));
                    if (!intentDecision.CanStart)
                    {
                        evt.Use();
                        return;
                    }
                    ESWorkbenchPointerIntentKind intent = intentDecision.Intent;
                    bool additiveSelection = evt.shift || evt.control || evt.command;
                    bool toggleSelection = evt.control || evt.command;
                    if (additiveSelection && intent == ESWorkbenchPointerIntentKind.Manipulate)
                        intent = ESWorkbenchPointerIntentKind.Select;
                    if (!hasHit)
                    {
                        if (intent == ESWorkbenchPointerIntentKind.Select
                            && !additiveSelection && !toggleSelection)
                            context.Selection.Clear();
                        if (intent != ESWorkbenchPointerIntentKind.None) evt.Use();
                    }
                    else if (intent == ESWorkbenchPointerIntentKind.Select)
                    {
                        context.Selection.Select(hitSelection, additiveSelection, toggleSelection);
                        evt.Use();
                    }
                    else if (intent == ESWorkbenchPointerIntentKind.Manipulate)
                    {
                        if (!context.PointerCoordinator.TryAcquire(
                                this,
                                0,
                                ESWorkbenchPointerOwnerKind.Viewport))
                        {
                            evt.Use();
                            return;
                        }
                        context.Selection.Select(hitSelection);
                        if (moveTool
                            && TryResolveWorldPoint(rect, evt.mousePosition, out Vector3 pointerWorld))
                        {
                            pointerWorld.y = item.Spatial.Position.y;
                            if (moveAnchor.Capture(item.Spatial.Position, pointerWorld))
                            {
                                moving = true;
                                movingSelection = context.Selection.Current;
                                pendingMove = item.Spatial.Position;
                                transformStartValue = item.Spatial.Position;
                                pendingMoveValid = false;
                            }
                        }
                        else if (rotateTool)
                        {
                            rotating = true;
                            movingSelection = context.Selection.Current;
                            transformStartValue = item.Spatial.RotationEuler;
                            if (!transformGesture.Begin(
                                    ESWorkbenchMutationKind.Rotate,
                                    evt.mousePosition,
                                    transformStartValue))
                                rotating = false;
                            pendingTransformValue = transformStartValue;
                            pendingTransformValid = false;
                        }
                        else if (scaleTool)
                        {
                            scaling = true;
                            movingSelection = context.Selection.Current;
                            transformStartValue = item.Spatial.Size;
                            if (!transformGesture.Begin(
                                    ESWorkbenchMutationKind.Scale,
                                    evt.mousePosition,
                                    transformStartValue))
                                scaling = false;
                            pendingTransformValue = transformStartValue;
                            pendingTransformValid = false;
                        }
                        if (moving || rotating || scaling)
                        {
                            if (!gestureSession.TryArm(
                                    ESWorkbenchPointerGestureSession.Kind.Transform, 0, evt.mousePosition))
                            {
                                StopMoving();
                                context.PointerCoordinator.Release(
                                    this,
                                    0,
                                    ESWorkbenchPointerOwnerKind.Viewport);
                            }
                        }
                        else
                        {
                            context.PointerCoordinator.Release(
                                this,
                                0,
                                ESWorkbenchPointerOwnerKind.Viewport);
                        }
                        evt.Use();
                    }
                }
                if (moving || rotating || scaling)
                {
                    activeControlId = controlId;
                    GUIUtility.hotControl = controlId;
                }
            }
            if (evt.type == EventType.MouseDrag && (moving || rotating || scaling))
            {
                UpdateTransformPreview(rect, evt.mousePosition, evt.shift);
                if (moving) BeginEdgePan(evt.mousePosition, evt.shift);
                renderHost.MarkDirtyRepaint();
                evt.Use();
            }
            if (evt.type == EventType.MouseUp && (rotating || scaling))
            {
                UpdateTransformPreview(rect, evt.mousePosition, evt.shift, true);
                ESWorkbenchSelection target = movingSelection;
                Vector3 value = pendingTransformValue;
                bool commit = pendingTransformValid;
                bool commitRotation = rotating;
                StopMoving();
                gestureSession.Finish(ESWorkbenchPointerGestureSession.EndReason.Commit);
                context.PointerCoordinator.Release(
                    this,
                    0,
                    ESWorkbenchPointerOwnerKind.Viewport);
                ReleaseMouseControl();
                if (commit)
                {
                    bool succeeded = commitRotation
                        ? context.Actions.Authoring.TryRotate(target, value, out _)
                        : context.Actions.Authoring.TryScale(target, value, out _);
                    if (!succeeded) RebuildInstances();
                }
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && moving)
            {
                UpdateTransformPreview(rect, evt.mousePosition, evt.shift, true);
                ESWorkbenchSelection target = movingSelection;
                Vector3 worldPosition = pendingMove;
                bool commit = pendingMoveValid;
                StopMoving();
                gestureSession.Finish(ESWorkbenchPointerGestureSession.EndReason.Commit);
                context.PointerCoordinator.Release(
                    this,
                    0,
                    ESWorkbenchPointerOwnerKind.Viewport);
                ReleaseMouseControl();
                if (commit)
                {
                    if (!context.Actions.Authoring.TryMove(target, worldPosition, out _)) RebuildInstances();
                }
                evt.Use();
            }
        }

        private void UpdateTransformPreview(
            Rect rect,
            Vector2 mousePosition,
            bool lockDominantAxis,
            bool finalize = false)
        {
            if (!moving && !rotating && !scaling) return;
            if (!gestureSession.TryEnsureStarted(0, mousePosition)) return;
            if (moving)
            {
                pendingMoveValid = false;
                if (!TryResolveWorldPoint(
                        rect,
                        mousePosition,
                        true,
                        out Vector3 worldPosition))
                {
                    ApplyPreviewTransform(
                        movingSelection?.StableId,
                        ESWorkbenchMutationKind.Move,
                        transformStartValue);
                    return;
                }
                worldPosition.y = moveAnchor.PointerStart.y;
                if (!moveAnchor.TryResolve(
                        worldPosition,
                        context.SnapPosition,
                        ESWorkbenchMoveAxes.Horizontal,
                        lockDominantAxis,
                        out pendingMove))
                {
                    ApplyPreviewTransform(
                        movingSelection?.StableId,
                        ESWorkbenchMutationKind.Move,
                        transformStartValue);
                    return;
                }
                pendingMoveValid = true;
                ApplyPreviewTransform(movingSelection?.StableId, ESWorkbenchMutationKind.Move, pendingMove);
                return;
            }

            ESWorkbenchMutationKind gestureKind = rotating
                ? ESWorkbenchMutationKind.Rotate
                : ESWorkbenchMutationKind.Scale;
            // 阈值只负责防止误触；越过阈值的首帧位移必须立即进入增量解析。
            pendingTransformValid = finalize
                ? transformGesture.TryFinalize(
                    mousePosition,
                    rotating ? context.SnapRotation : context.SnapScale,
                    out pendingTransformValue)
                : transformGesture.TryUpdate(
                    mousePosition,
                    rotating ? context.SnapRotation : context.SnapScale,
                    out pendingTransformValue);
            if (!pendingTransformValid)
            {
                ApplyPreviewTransform(movingSelection?.StableId, gestureKind, transformStartValue);
                return;
            }
            ApplyPreviewTransform(
                movingSelection?.StableId,
                rotating ? ESWorkbenchMutationKind.Rotate : ESWorkbenchMutationKind.Scale,
                pendingTransformValue);
        }

        private bool TryHitItem(Rect rect, Vector2 guiPoint, out ESWorkbenchHierarchyDescriptor item)
        {
            item = null;
            if (preview?.Camera == null || rect.width <= 1f || rect.height <= 1f) return false;
            Rect interactionRect = ESWorkbenchViewportOverlay.GetInteractionRect(rect);
            if (!ESWorkbenchCameraViewportProjection.TryNormalize(
                    rect, interactionRect, guiPoint, out Vector3 viewportPoint))
                return false;
            Ray ray = preview.Camera.ViewportPointToRay(viewportPoint);
            float nearest = float.MaxValue;
            for (int i = 0; i < instances.Count && i < instanceItems.Count; i++)
            {
                GameObject instance = instances[i];
                if (instance == null) continue;
                if (!instanceBounds.Calculate(instance).IntersectRay(ray, out float distanceToBounds) || distanceToBounds >= nearest) continue;
                nearest = distanceToBounds;
                item = instanceItems[i];
            }
            if (item != null) return true;

            // A tiny authoring object can be visually obvious while its bounds are
            // smaller than one pixel. Use the shared screen tolerance only when no
            // real ray target exists, preserving depth/occlusion semantics.
            float nearestScreenDistance = float.MaxValue;
            float nearestScreenDepth = float.MaxValue;
            for (int i = 0; i < instances.Count && i < instanceItems.Count; i++)
            {
                GameObject instance = instances[i];
                ESWorkbenchHierarchyDescriptor candidate = instanceItems[i];
                if (instance == null || candidate == null) continue;
                Bounds bounds = instanceBounds.Calculate(instance);
                if (!ESWorkbenchCameraViewportProjection.TryProjectWorldToGui(
                        preview.Camera,
                        bounds.center,
                        rect,
                        interactionRect,
                        out Vector2 screenPoint,
                        out float depth))
                    continue;
                float screenDistance = Vector2.Distance(screenPoint, guiPoint);
                if (screenDistance > feel.SelectionHitRadiusPixels
                    || screenDistance >= nearestScreenDistance
                    || depth >= nearestScreenDepth)
                    continue;
                nearestScreenDistance = screenDistance;
                nearestScreenDepth = depth;
                item = candidate;
            }
            return item != null;
        }

        private void ApplyPreviewTransform(string stableId, ESWorkbenchMutationKind kind, Vector3 value)
        {
            if (string.IsNullOrEmpty(stableId)) return;
            for (int i = 0; i < instances.Count && i < instanceItems.Count; i++)
                if (instanceItems[i]?.ItemId == stableId && instances[i] != null)
                {
                    if (kind == ESWorkbenchMutationKind.Move) instances[i].transform.position = preview.GroupOrigin + value;
                    else if (kind == ESWorkbenchMutationKind.Rotate) instances[i].transform.rotation = Quaternion.Euler(value);
                    else if (kind == ESWorkbenchMutationKind.Scale) instances[i].transform.localScale = value;
                    return;
                }
        }

        private void DrawTransformTargetOutline()
        {
            if ((!moving && !rotating && !scaling) || movingSelection == null || preview?.Camera == null) return;
            for (int i = 0; i < instances.Count && i < instanceItems.Count; i++)
            {
                if (instances[i] == null || instanceItems[i]?.ItemId != movingSelection.StableId) continue;
                Color previous = Handles.color;
                Handles.SetCamera(preview.Camera);
                Handles.color = ESEditorPresentation.SelectionColor;
                Bounds bounds = instanceBounds.Calculate(instances[i]);
                Handles.DrawWireCube(bounds.center, bounds.size);
                if (moving && pendingMoveValid)
                    Handles.DrawLine(preview.GroupOrigin + transformStartValue, bounds.center);
                Handles.color = previous;
                return;
            }
        }

        private void DrawHoverOutline()
        {
            if (!hover.HasValue || moving || rotating || scaling || preview?.Camera == null) return;
            if (string.Equals(
                    context.Selection.Current?.StableId,
                    hover.StableId,
                    StringComparison.Ordinal)) return;
            for (int i = 0; i < instances.Count && i < instanceItems.Count; i++)
            {
                if (instances[i] == null || !hover.IsHovered(instanceItems[i]?.ItemId)) continue;
                Color previous = Handles.color;
                Handles.SetCamera(preview.Camera);
                Handles.color = new Color(
                    ESEditorPresentation.SelectionColor.r,
                    ESEditorPresentation.SelectionColor.g,
                    ESEditorPresentation.SelectionColor.b,
                    0.72f);
                Bounds bounds = instanceBounds.Calculate(instances[i]);
                Handles.DrawWireCube(bounds.center, bounds.size * 1.03f);
                Handles.color = previous;
                return;
            }
        }

        private void StopMoving()
        {
            moving = false;
            rotating = false;
            scaling = false;
            pendingMoveValid = false;
            pendingTransformValid = false;
            movingSelection = null;
            moveAnchor.Reset();
            transformGesture.Reset();
            edgePanSession.Stop();
            edgePanSchedule?.Pause();
        }

        private void BeginEdgePan(Vector2 renderPosition, bool lockDominantAxis)
        {
            if (!moving) return;
            double now = EditorApplication.timeSinceStartup;
            if (!edgePanSession.IsActive)
                edgePanSession.Begin(renderPosition, lockDominantAxis, now);
            else
                edgePanSession.UpdatePointer(renderPosition, lockDominantAxis);
            if (gestureSession.IsStarted && edgePanSchedule?.isActive == false)
            {
                edgePanSchedule.Resume();
            }
        }

        private void ApplyEdgePan()
        {
            if (!moving || !edgePanSession.IsActive || !gestureSession.IsStarted) return;
            double now = EditorApplication.timeSinceStartup;
            if (!edgePanSession.TryAdvance(now, out float deltaTime)) return;
            if (!TryEdgePanRenderPosition(edgePanSession.Pointer, deltaTime)) return;
            UpdateTransformPreview(
                renderHost.contentRect,
                edgePanSession.Pointer,
                edgePanSession.LockDominantAxis);
            renderHost.MarkDirtyRepaint();
        }

        public bool TryEdgePan(Vector2 localPosition, float deltaTime)
        {
            Vector2 renderPosition = renderHost.WorldToLocal(root.LocalToWorld(localPosition));
            return TryEdgePanRenderPosition(renderPosition, deltaTime);
        }

        public bool TryNudge(KeyCode keyCode, bool shift, bool controlOrCommand, out string message)
        {
            message = string.Empty;
            ESWorkbenchSelection selection = context.Selection.Current;
            if (!ESWorkbenchNudgeResolver.TryResolveDelta(
                    keyCode, shift, controlOrCommand, feel, out Vector3 delta)
                || !ESWorkbenchNudgeResolver.TryResolvePosition(
                    context.Hierarchy, selection, out Vector3 position)
                || context.IsHierarchyLocked(selection.StableId)
                || !context.Actions.Authoring.CanMove(selection)) return false;
            Vector3 target = context.SnapPosition(position + delta);
            bool committed = context.Actions.Authoring.TryMove(selection, target, out message);
            if (committed) Refresh(ESWorkbenchRefreshReason.DataChanged);
            return committed;
        }

        private bool TryEdgePanRenderPosition(Vector2 renderPosition, float deltaTime)
        {
            Rect interactionRect = ESWorkbenchViewportOverlay.GetInteractionRect(renderHost.contentRect);
            if (!ESWorkbenchViewportOverlay.AllowsEdgePanPointer(
                    renderHost.contentRect, interactionRect, renderPosition)) return false;
            if (!edgePan.Evaluate(interactionRect, renderPosition, deltaTime, out Vector2 delta)) return false;
            cameraNavigation.Pan(
                delta, renderHost.contentRect, feel.VerticalFieldOfViewDegrees);
            UpdatePointerWorldStatus(
                renderHost.contentRect,
                renderPosition,
                allowOutside: true);
            context.StatusChanged?.Invoke();
            renderHost.MarkDirtyRepaint();
            return true;
        }

        private void ReleaseMouseControl()
        {
            if (activeControlId != 0 && GUIUtility.hotControl == activeControlId)
                GUIUtility.hotControl = 0;
            activeControlId = 0;
        }

        private Bounds CalculateAllBounds()
        {
            if (instances.Count == 0) return new Bounds(Vector3.zero, Vector3.one);
            Bounds bounds = instanceBounds.Calculate(instances[0]);
            for (int i = 1; i < instances.Count; i++) bounds.Encapsulate(instanceBounds.Calculate(instances[i]));
            bounds.center -= preview?.GroupOrigin ?? Vector3.zero;
            return bounds;
        }

        public void Dispose()
        {
            hover.Clear();
            edgePanSchedule?.Pause();
            edgePanSchedule = null;
            StopMoving();
            gestureSession.Cancel(ESWorkbenchPointerGestureSession.EndReason.Deactivate);
            ReleaseMouseControl();
            ClearInstances();
            if (preview != null)
            {
                preview.Dispose();
                preview = null;
            }
        }

        private void ClearInstances()
        {
            preview?.DestroyAllModelGroups();
            instances.Clear();
            instanceItems.Clear();
            instanceBounds.Clear();
        }
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Transient,
        ESWindowSurfaceKind.Popup,
        "短生命周期工作台弹窗")]
    [ESWindowPresentationShortTitle("弹窗")]
    internal sealed class ESWorkbenchPopupWindow : EditorWindow
    {
        private static ESWorkbenchPopupWindow activeWindow;
        private static bool openingWindow;

        private ESWorkbenchPopupRequest request;
        private ESWorkbenchActionContext context;
        private EditorWindow ownerWindow;
        private IDisposable ownerHold;
        private bool configured;

        internal static void Open(EditorWindow owner, ESWorkbenchPopupRequest request, ESWorkbenchActionContext context, Rect screenAnchor)
        {
            if (owner == null || request == null || context == null) return;
            if (!ESWindowFoundation.IsBound(owner))
                throw new InvalidOperationException(
                    "ES Workbench Popup 只接受已接入 ESWindowFoundation 的 owner。");
            if (activeWindow != null)
            {
                try
                {
                    activeWindow.Close();
                }
                catch (Exception closeException)
                {
                    Debug.LogException(new InvalidOperationException(
                        "ES Workbench Popup 现有实例关闭失败，已拒绝创建第二个实例。",
                        closeException));
                    return;
                }
                if (activeWindow != null)
                    return;
            }
            openingWindow = true;
            ESWorkbenchPopupWindow window = null;
            try
            {
                window = CreateInstance<ESWorkbenchPopupWindow>();
                activeWindow = window;
                window.hideFlags = HideFlags.DontSave;
                window.request = request;
                window.context = context;
                window.ownerWindow = owner;
                window.configured = true;
                window.titleContent = new GUIContent(request.Title);
                window.ownerHold = ESWindowFoundation.HoldInteraction(owner, "ES Workbench Popup");
                window.ShowAsDropDown(screenAnchor, request.Size);
            }
            catch
            {
                if (window != null)
                {
                    try { window.ownerHold?.Dispose(); }
                    catch (Exception cleanupException) { Debug.LogException(cleanupException); }
                    window.ownerHold = null;
                    try { window.Close(); }
                    catch (Exception closeException) { Debug.LogException(closeException); }
                }
                throw;
            }
            finally
            {
                openingWindow = false;
            }
        }

        private void OnEnable()
        {
            // Popup lifetime is owner-scoped; keep ES cleanup hooks without
            // exposing a standalone sleep state for the transient surface.
            ESWindowFoundation.BindTransient(this);
            if (openingWindow)
                return;
            EditorApplication.delayCall -= CloseIfContextWasLost;
            EditorApplication.delayCall += CloseIfContextWasLost;
        }

        public void CreateGUI()
        {
            ESWindowFoundation.Unbind(this);
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;
            rootVisualElement.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            VisualElement content = null;
            try
            {
                content = request?.CreateContent(context);
            }
            catch (Exception exception)
            {
                configured = false;
                Debug.LogException(new InvalidOperationException(
                    "ES Workbench Popup 内容创建失败，已安排安全关闭。", exception));
                rootVisualElement.Add(new HelpBox(
                    "弹窗内容创建失败，窗口将自动关闭。",
                    HelpBoxMessageType.Error));
                EditorApplication.delayCall -= CloseIfContextWasLost;
                EditorApplication.delayCall += CloseIfContextWasLost;
            }
            if (content != null)
            {
                content.style.flexGrow = 1f;
                content.style.minWidth = 0f;
                content.style.minHeight = 0f;
                rootVisualElement.Add(content);
            }
            ESWindowFoundation.BindTransient(this);
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= CloseIfContextWasLost;
            ESWindowFoundation.Suspend(this);
            IDisposable currentOwnerHold = ownerHold;
            ownerHold = null;
            try { currentOwnerHold?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
            configured = false;
            request = null;
            context = null;
            ownerWindow = null;
            if (ReferenceEquals(activeWindow, this))
                activeWindow = null;
        }

        private void OnDestroy()
        {
            ESWindowFoundation.Close(this);
        }

        private void CloseIfContextWasLost()
        {
            EditorApplication.delayCall -= CloseIfContextWasLost;
            bool ownerContextLost = ownerWindow == null
                || !ESWindowFoundation.IsBound(ownerWindow);
            if (this != null && (!configured || ownerContextLost))
                Close();
        }
    }
}
#endif
