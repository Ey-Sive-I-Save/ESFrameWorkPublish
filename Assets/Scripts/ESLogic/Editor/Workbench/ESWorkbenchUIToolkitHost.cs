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
            public Vector2 pointerStart;
            public bool pointerDown;
        }

        private const string DragPayloadKey = "ES.Workbench.ObjectDescriptor";

        private readonly EditorWindow owner;
        private readonly ESWorkbenchActionContext actions;
        private readonly string workbenchId;
        private ESWorkbenchHostPresentationDescriptor presentation;
        private readonly Type assetType;
        private readonly Func<UnityEngine.Object> getAsset;
        private readonly Action<UnityEngine.Object> bindAsset;
        private readonly Func<IReadOnlyList<ESWorkbenchPageDefinition>> getPages;
        private readonly Func<IReadOnlyList<ESWorkbenchViewportDescriptor>> getViewports;
        private readonly Func<IReadOnlyList<ESWorkbenchObjectDescriptor>> getObjects;
        private readonly Func<IReadOnlyList<ESWorkbenchHierarchyDescriptor>> getHierarchy;
        private readonly Func<IReadOnlyList<ESWorkbenchInspectorDescriptor>> getInspectors;
        private readonly Func<IReadOnlyList<ESWorkbenchToolDescriptor>> getTools;
        private readonly Func<IReadOnlyList<ESWorkbenchCommandDescriptor>> getCommands;
        private readonly Func<IReadOnlyList<ESWorkbenchIssueDescriptor>> getIssues;
        private readonly Func<IReadOnlyList<ESWorkbenchBottomPanelDescriptor>> getBottomPanels;
        private readonly Func<bool> isDirty;
        private readonly ESWorkbenchLayoutState layout;
        private readonly Func<ESWorkbenchPageDefinition, VisualElement> createPageView;
        private readonly Action<string> selectPage;
        private readonly Func<string> getSelectedPage;
        private readonly Dictionary<string, IESWorkbenchViewport> liveViewports = new Dictionary<string, IESWorkbenchViewport>(StringComparer.Ordinal);
        private readonly Dictionary<string, ToolbarToggle> viewportToggles = new Dictionary<string, ToolbarToggle>(StringComparer.Ordinal);
        private readonly List<ESWorkbenchObjectDescriptor> visibleObjects = new List<ESWorkbenchObjectDescriptor>();
        private readonly List<ESWorkbenchHierarchyDescriptor> visibleHierarchy = new List<ESWorkbenchHierarchyDescriptor>();
        private readonly Dictionary<string, ESWorkbenchHierarchyDescriptor> hierarchyById = new Dictionary<string, ESWorkbenchHierarchyDescriptor>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ESWorkbenchHierarchyDescriptor>> hierarchyChildren = new Dictionary<string, List<ESWorkbenchHierarchyDescriptor>>(StringComparer.Ordinal);
        private readonly HashSet<string> expandedHierarchyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> hiddenHierarchyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> lockedHierarchyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ESWorkbenchBottomPanelDescriptor> standardBottomPanels = new List<ESWorkbenchBottomPanelDescriptor>();
        private readonly List<ESWorkbenchBottomPanelDescriptor> resolvedBottomPanels = new List<ESWorkbenchBottomPanelDescriptor>();

        private VisualElement root;
        private VisualElement commandBar;
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
        private ToolbarMenu categoryMenu;
        private ListView objectList;
        private ListView hierarchyList;
        private Label objectEmptyLabel;
        private Label hierarchyEmptyLabel;
        private string categoryFilter = "全部";
        private string activeLeftTab;
        private string activeDocument;
        private string activeViewportId;
        private string activeBottomTab;
        private IESWorkbenchViewport activeViewport;
        private bool hierarchyExpansionInitialized;
        private float availableWidth = 1200f;
        private bool disposed;

        internal ESWorkbenchUIToolkitHost(
            EditorWindow owner,
            ESWorkbenchActionContext actions,
            string workbenchId,
            string brandTitle,
            Type assetType,
            Func<UnityEngine.Object> getAsset,
            Action<UnityEngine.Object> bindAsset,
            Func<IReadOnlyList<ESWorkbenchPageDefinition>> getPages,
            Func<IReadOnlyList<ESWorkbenchViewportDescriptor>> getViewports,
            Func<IReadOnlyList<ESWorkbenchObjectDescriptor>> getObjects,
            Func<IReadOnlyList<ESWorkbenchHierarchyDescriptor>> getHierarchy,
            Func<IReadOnlyList<ESWorkbenchInspectorDescriptor>> getInspectors,
            Func<IReadOnlyList<ESWorkbenchToolDescriptor>> getTools,
            Func<IReadOnlyList<ESWorkbenchCommandDescriptor>> getCommands,
            ESWorkbenchLayoutState layout,
            Func<ESWorkbenchPageDefinition, VisualElement> createPageView,
            Action<string> selectPage,
            Func<string> getSelectedPage,
            Func<IReadOnlyList<ESWorkbenchIssueDescriptor>> getIssues = null,
            Func<bool> isDirty = null,
            Func<IReadOnlyList<ESWorkbenchBottomPanelDescriptor>> getBottomPanels = null,
            ESWorkbenchHostPresentationDescriptor presentation = null)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
            this.workbenchId = string.IsNullOrWhiteSpace(workbenchId) ? owner.GetType().FullName : workbenchId.Trim();
            this.presentation = presentation ?? ESWorkbenchHostPresentationDescriptor.CreateDefault(brandTitle);
            this.assetType = assetType ?? typeof(UnityEngine.Object);
            this.getAsset = getAsset;
            this.bindAsset = bindAsset;
            this.getPages = getPages;
            this.getViewports = getViewports;
            this.getObjects = getObjects;
            this.getHierarchy = getHierarchy;
            this.getInspectors = getInspectors;
            this.getTools = getTools;
            this.getCommands = getCommands;
            this.layout = layout ?? new ESWorkbenchLayoutState();
            this.createPageView = createPageView;
            this.selectPage = selectPage;
            this.getSelectedPage = getSelectedPage;
            this.getIssues = getIssues;
            this.isDirty = isDirty;
            this.getBottomPanels = getBottomPanels;
            CreateStandardBottomPanelDescriptors();
            if (!this.layout.responsiveLayoutInitialized)
            {
                this.layout.leftPaneVisible = true;
                this.layout.inspectorPaneVisible = true;
                this.layout.compactSidePane = "inspector";
                this.layout.responsiveLayoutInitialized = true;
            }
            activeLeftTab = string.IsNullOrWhiteSpace(this.layout.activeLeftTab) ? "objects" : this.layout.activeLeftTab;
            activeDocument = string.IsNullOrWhiteSpace(this.layout.activeDocument) ? "viewport" : this.layout.activeDocument;
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
            actions.Selection.Changed += OnSelectionChanged;
            actions.Tools.Changed += OnToolChanged;
        }

        public VisualElement Build()
        {
            root = new VisualElement { name = "ESWorkbenchHost" };
            root.style.flexGrow = 1f;
            root.style.minWidth = 0f;
            root.style.minHeight = 0f;
            root.style.backgroundColor = ESEditorPresentation.WindowSurfaceColor;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            commandBar = CreateHorizontalBar("ESWorkbenchCommandBar", 32f);
            root.Add(commandBar);

            float leftWidth = Mathf.Clamp(layout.leftPaneWidth, 190f, 420f);
            outerSplit = new TwoPaneSplitView(0, leftWidth, TwoPaneSplitViewOrientation.Horizontal) { name = "ESWorkbenchOuterSplit" };
            outerSplit.style.flexGrow = 1f;
            outerSplit.style.minHeight = 0f;
            outerSplit.Add(BuildLeftPanel());

            float inspectorWidth = Mathf.Clamp(layout.inspectorPaneWidth, 240f, 520f);
            contentSplit = new TwoPaneSplitView(1, inspectorWidth, TwoPaneSplitViewOrientation.Horizontal) { name = "ESWorkbenchContentSplit" };
            contentSplit.style.flexGrow = 1f;
            contentSplit.style.minHeight = 0f;
            contentSplit.Add(BuildCenterPanel());
            contentSplit.Add(BuildInspectorPanel());
            outerSplit.Add(contentSplit);
            workspaceSplit = new TwoPaneSplitView(1, Mathf.Clamp(layout.bottomDrawerHeight, 150f, 360f), TwoPaneSplitViewOrientation.Vertical)
            {
                name = "ESWorkbenchWorkspaceSplit"
            };
            workspaceSplit.style.flexGrow = 1f;
            workspaceSplit.style.minHeight = 0f;
            workspaceSplit.Add(outerSplit);
            workspaceSplit.Add(BuildBottomDrawer());
            root.Add(workspaceSplit);

            VisualElement status = CreateHorizontalBar("ESWorkbenchStatusBar", 25f);
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
        }

        internal void ReleaseContributedContent()
        {
            ReleaseBottomPanelContent();
            bottomContent?.Clear();
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
            VisualElement panel = new VisualElement { name = "ESWorkbenchLeftPanel" };
            panel.style.flexGrow = 1f;
            panel.style.minWidth = 190f;
            panel.style.minHeight = 0f;
            panel.style.backgroundColor = ESEditorPresentation.WindowInsetSurfaceColor;
            panel.style.borderRightWidth = 1f;
            panel.style.borderRightColor = ESEditorPresentation.DividerColor;
            panel.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float width = evt.newRect.width;
                if (width >= 189f && width <= 421f) layout.leftPaneWidth = width;
            });

            VisualElement tabs = CreateHorizontalBar("ESWorkbenchLeftTabs", 30f);
            AddLeftTab(tabs, "objects", "对象");
            AddLeftTab(tabs, "hierarchy", "层级");
            AddLeftTab(tabs, "tools", "工具");
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
            VisualElement panel = new VisualElement { name = "ESWorkbenchCenterPanel" };
            panel.style.flexGrow = 1f;
            panel.style.minWidth = 280f;
            panel.style.minHeight = 0f;
            documentTabs = CreateHorizontalBar("ESWorkbenchDocumentTabs", 31f);
            panel.Add(documentTabs);
            viewportModeBar = CreateHorizontalBar("ESWorkbenchViewportModes", 29f);
            panel.Add(viewportModeBar);
            viewportHost = new VisualElement { name = "ESWorkbenchViewportHost" };
            viewportHost.style.flexGrow = 1f;
            viewportHost.style.minWidth = 0f;
            viewportHost.style.minHeight = 0f;
            viewportHost.style.flexDirection = FlexDirection.Row;
            toolRail = new VisualElement { name = "ESWorkbenchToolRail" };
            toolRail.style.width = 54f;
            toolRail.style.minWidth = 54f;
            toolRail.style.flexShrink = 0f;
            toolRail.style.alignItems = Align.Center;
            toolRail.style.paddingTop = 5f;
            toolRail.style.backgroundColor = ESEditorPresentation.ToolbarSurfaceColor;
            toolRail.style.borderRightWidth = 1f;
            toolRail.style.borderRightColor = ESEditorPresentation.DividerColor;
            viewportHost.Add(toolRail);
            VisualElement surface = new VisualElement { name = "ESWorkbenchViewportSurface" };
            surface.style.flexGrow = 1f;
            surface.style.minWidth = 0f;
            surface.style.minHeight = 0f;
            surface.style.position = Position.Relative;
            centerContent = new VisualElement { name = "ESWorkbenchCenterContent" };
            centerContent.style.flexGrow = 1f;
            centerContent.style.minWidth = 0f;
            centerContent.style.minHeight = 0f;
            centerContent.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            centerContent.RegisterCallback<DragPerformEvent>(OnDragPerform);
            centerContent.RegisterCallback<DragLeaveEvent>(_ => HideDropFeedback());
            surface.Add(centerContent);
            dropFeedback = new VisualElement { name = "ESWorkbenchDropFeedback", pickingMode = PickingMode.Ignore };
            dropFeedback.style.position = Position.Absolute;
            dropFeedback.style.display = DisplayStyle.None;
            dropFeedback.style.paddingLeft = 10f;
            dropFeedback.style.paddingRight = 10f;
            dropFeedback.style.paddingTop = 6f;
            dropFeedback.style.paddingBottom = 6f;
            dropFeedback.style.backgroundColor = new Color(0.04f, 0.09f, 0.12f, 0.94f);
            dropFeedback.style.borderLeftWidth = 2f;
            dropFeedback.style.borderLeftColor = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            dropFeedback.Add(new Label { name = "DropTitle" });
            surface.Add(dropFeedback);
            viewportHost.Add(surface);
            panel.Add(viewportHost);
            viewportFooter = CreateHorizontalBar("ESWorkbenchViewportFooter", 25f);
            viewportFooter.style.height = 44f;
            viewportFooter.style.flexWrap = Wrap.Wrap;
            viewportFooter.style.borderTopWidth = 1f;
            viewportFooter.style.borderTopColor = ESEditorPresentation.DividerColor;
            panel.Add(viewportFooter);
            return panel;
        }

        private VisualElement BuildBottomDrawer()
        {
            VisualElement drawer = new VisualElement { name = "ESWorkbenchBottomDrawer" };
            drawer.style.flexGrow = 1f;
            drawer.style.minHeight = 120f;
            drawer.style.backgroundColor = ESEditorPresentation.WindowInsetSurfaceColor;
            drawer.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float height = evt.newRect.height;
                if (height >= 149f && height <= 361f) layout.bottomDrawerHeight = height;
            });
            bottomTabs = CreateHorizontalBar("ESWorkbenchBottomTabs", 29f);
            drawer.Add(bottomTabs);
            BuildBottomTabs();
            bottomContent = new ScrollView(ScrollViewMode.Vertical) { name = "ESWorkbenchBottomContent" };
            ((ScrollView)bottomContent).horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            bottomContent.style.flexGrow = 1f;
            bottomContent.style.minHeight = 0f;
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
                "history", "操作历史",
                _ => CreateActivityPanel(
                    ESWorkbenchActivityChannel.History,
                    "暂无操作记录"),
                "按项目持久化的工作台操作记录", 400));
            standardBottomPanels.Add(new ESWorkbenchBottomPanelDescriptor(
                "tasks", "任务中心",
                _ => CreateActivityPanel(
                    ESWorkbenchActivityChannel.Task,
                    "暂无持久任务"),
                "按项目持久化的构建与处理任务", 300));
            standardBottomPanels.Add(new ESWorkbenchBottomPanelDescriptor(
                "performance", "性能与配额",
                _ => CreateIssuePanel(
                    issue => issue.Channel == ESWorkbenchIssueChannel.Performance
                        || issue.Channel == ESWorkbenchIssueChannel.Security,
                    "没有性能或配额问题"),
                "性能预算、安全限制与生产配额", 200));
            standardBottomPanels.Add(new ESWorkbenchBottomPanelDescriptor(
                "logs", "日志",
                _ => CreateActivityPanel(
                    ESWorkbenchActivityChannel.Log,
                    "暂无日志"),
                "按项目持久化的工作台日志", 100));
        }

        private VisualElement BuildInspectorPanel()
        {
            VisualElement panel = new VisualElement { name = "ESWorkbenchInspectorPanel" };
            panel.style.flexGrow = 1f;
            panel.style.minWidth = 240f;
            panel.style.minHeight = 0f;
            panel.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            panel.style.borderLeftWidth = 1f;
            panel.style.borderLeftColor = ESEditorPresentation.DividerColor;
            panel.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float width = evt.newRect.width;
                if (width >= 239f && width <= 521f) layout.inspectorPaneWidth = width;
            });
            VisualElement titleBar = CreateHorizontalBar("ESWorkbenchInspectorTitleBar", 31f);
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
            Label brand = new Label(presentation.BrandTitle);
            brand.AddToClassList("es-brand-title");
            brand.style.unityFontStyleAndWeight = FontStyle.Bold;
            brand.style.marginRight = 10f;
            commandBar.Add(brand);
            leftPaneButton = CreateActionButton(
                EditorGUIUtility.IconContent("d_Project").image,
                string.Empty,
                "显示或隐藏对象库与作者层级",
                ToggleLeftPane);
            leftPaneButton.name = "ESWorkbenchToggleLeftPane";
            leftPaneButton.style.width = 28f;
            commandBar.Add(leftPaneButton);
            inspectorPaneButton = CreateActionButton(
                EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow").image,
                string.Empty,
                "显示或隐藏上下文 Inspector",
                ToggleInspectorPane);
            inspectorPaneButton.name = "ESWorkbenchToggleInspectorPane";
            inspectorPaneButton.style.width = 28f;
            commandBar.Add(inspectorPaneButton);
            bottomPaneButton = CreateActionButton(
                EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image,
                string.Empty,
                "展开或收起问题、历史、构建与性能抽屉",
                ToggleBottomDrawer);
            bottomPaneButton.name = "ESWorkbenchToggleBottomDrawer";
            bottomPaneButton.style.width = 28f;
            commandBar.Add(bottomPaneButton);
            var assetField = new ObjectField(presentation.AssetFieldLabel)
            {
                objectType = assetType,
                allowSceneObjects = false,
                value = getAsset?.Invoke()
            };
            assetField.style.minWidth = 145f;
            assetField.style.maxWidth = 280f;
            assetField.style.flexGrow = 1f;
            assetField.style.marginRight = 8f;
            assetField.RegisterValueChangedCallback(evt => bindAsset?.Invoke(evt.newValue));
            commandBar.Add(assetField);
            documentStatusLabel = new Label();
            documentStatusLabel.style.paddingLeft = 7f;
            documentStatusLabel.style.paddingRight = 7f;
            documentStatusLabel.style.marginRight = 5f;
            documentStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            commandBar.Add(documentStatusLabel);
            UpdateDocumentStatus();
            IReadOnlyList<ESWorkbenchCommandDescriptor> commands = getCommands?.Invoke();
            if (commands == null) return;
            foreach (ESWorkbenchCommandDescriptor command in commands.OrderByDescending(value => value.Priority).ThenBy(value => value.CommandId, StringComparer.Ordinal))
            {
                if (command == null || !command.ShowInToolbar) continue;
                string text = command.IconOnly && command.Icon != null ? string.Empty : command.DisplayName;
                Button button = CreateActionButton(command.Icon, text, command.Tooltip, () => ExecuteCommand(command));
                if (command.IconOnly) button.style.width = 28f;
                button.SetEnabled(command.CanExecute == null || command.CanExecute(actions));
                commandBar.Add(button);
            }
            UpdatePaneButtons();
        }

        private void UpdateDocumentStatus()
        {
            if (documentStatusLabel == null) return;
            bool dirty = isDirty != null && isDirty();
            documentStatusLabel.text = dirty ? "● 未保存" : "已保存";
            documentStatusLabel.style.color = dirty
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
        }

        private void BuildToolBar()
        {
            if (toolRail == null) return;
            toolRail.Clear();
            IReadOnlyList<ESWorkbenchToolDescriptor> source = getTools?.Invoke();
            if (source == null) return;
            foreach (ESWorkbenchToolDescriptor tool in source
                .Where(value => value != null && (value.IsAvailable == null || value.IsAvailable(actions)))
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
            AddDocumentTab(
                "viewport",
                presentation.ViewportDocumentTitle,
                presentation.ViewportDocumentTooltip);
            IReadOnlyList<ESWorkbenchPageDefinition> pages = getPages?.Invoke();
            if (pages != null)
            for (int i = 0; i < pages.Count; i++)
            {
                ESWorkbenchPageDefinition page = pages[i];
                if (page == null || (page.isAvailable != null && !page.isAvailable())) continue;
                AddDocumentTab("page:" + page.pageId, page.title, page.tooltip);
            }
            bool activeExists = activeDocument == "viewport"
                || (pages != null && pages.Any(value => value != null
                    && activeDocument == "page:" + value.pageId
                    && (value.isAvailable == null || value.isAvailable())));
            if (!activeExists)
            {
                activeDocument = "viewport";
                layout.activeDocument = activeDocument;
            }
        }

        private void BuildViewportModes()
        {
            viewportModeBar.Clear();
            viewportToggles.Clear();
            IReadOnlyList<ESWorkbenchViewportDescriptor> descriptors = getViewports?.Invoke();
            ESWorkbenchViewportDescriptor[] available = (descriptors ?? Array.Empty<ESWorkbenchViewportDescriptor>())
                .Where(value => value != null && (value.IsAvailable == null || value.IsAvailable(actions)))
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.ViewportId, StringComparer.Ordinal)
                .ToArray();
            ReleaseUnavailableViewports(available);
            foreach (ESWorkbenchViewportDescriptor descriptor in available)
            {
                var toggle = new ToolbarToggle { text = descriptor.DisplayName, tooltip = descriptor.Tooltip };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) ActivateViewport(descriptor);
                    else if (activeViewportId == descriptor.ViewportId) toggle.SetValueWithoutNotify(true);
                });
                viewportToggles.Add(descriptor.ViewportId, toggle);
                viewportModeBar.Add(toggle);
            }
            if (activeViewport == null || string.IsNullOrEmpty(activeViewportId) || !viewportToggles.ContainsKey(activeViewportId))
            {
                ESWorkbenchViewportDescriptor first = available.FirstOrDefault();
                if (first != null) ActivateViewport(first);
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

        private void AddSnapMenu()
        {
            if (string.IsNullOrEmpty(activeViewportId)) return;
            ESWorkbenchViewportLayoutState state = layout.GetOrCreateViewportState(activeViewportId);
            var menu = new ToolbarMenu
            {
                text = state.snapEnabled ? "吸附 开" : "吸附 关",
                tooltip = "控制当前视口的移动、旋转和缩放吸附"
            };
            menu.menu.AppendAction("启用吸附", _ =>
            {
                state.snapEnabled = !state.snapEnabled;
                menu.text = state.snapEnabled ? "吸附 开" : "吸附 关";
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
                    menu.text = "吸附 开";
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
                    activeViewport.Deactivate();
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
            activeLeftTab = id;
            layout.activeLeftTab = id;
            VisualElement tabBar = root?.Q<VisualElement>("ESWorkbenchLeftTabs");
            tabBar?.Query<ToolbarToggle>().ForEach(toggle => toggle.SetValueWithoutNotify((string)toggle.userData == id));
            if (leftContent == null) return;
            leftContent.Clear();
            if (id == "hierarchy") BuildHierarchyPanel();
            else if (id == "tools") BuildToolsPanel();
            else BuildObjectsPanel();
        }

        private void BuildObjectsPanel()
        {
            VisualElement filter = CreateHorizontalBar("ESWorkbenchObjectFilter", 32f);
            objectSearch = new ToolbarSearchField();
            objectSearch.style.flexGrow = 1f;
            objectSearch.RegisterValueChangedCallback(_ => RebuildObjectList());
            filter.Add(objectSearch);
            categoryMenu = new ToolbarMenu { text = categoryFilter };
            BuildCategoryMenu();
            filter.Add(categoryMenu);
            leftContent.Add(filter);
            objectList = new ListView
            {
                itemsSource = visibleObjects,
                fixedItemHeight = 70f,
                selectionType = SelectionType.Single,
                makeItem = CreateObjectRow,
                bindItem = BindObjectRow
            };
            objectList.style.flexGrow = 1f;
            objectList.selectionChanged += selection =>
            {
                ESWorkbenchObjectDescriptor selected = selection.OfType<ESWorkbenchObjectDescriptor>().FirstOrDefault();
                if (selected != null) actions.Selection.Select(selected.ToSelection());
            };
            leftContent.Add(objectList);
            objectEmptyLabel = CreateListEmptyLabel("没有可用对象", "注册对象源后可搜索并拖入中央视口。");
            leftContent.Add(objectEmptyLabel);
            RebuildObjectList();
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
                selectionType = SelectionType.Single,
                makeItem = CreateHierarchyRow,
                bindItem = BindHierarchyRow
            };
            hierarchyList.style.flexGrow = 1f;
            hierarchyList.selectionChanged += selection =>
            {
                ESWorkbenchHierarchyDescriptor selected = selection.OfType<ESWorkbenchHierarchyDescriptor>().FirstOrDefault();
                if (selected != null) actions.Selection.Select(selected.ToSelection());
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

        private void BuildToolsPanel()
        {
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            IReadOnlyList<ESWorkbenchToolDescriptor> tools = getTools?.Invoke();
            if (tools != null)
                foreach (ESWorkbenchToolDescriptor tool in tools.OrderByDescending(value => value.Priority).ThenBy(value => value.ToolId, StringComparer.Ordinal))
                {
                    if (tool == null || (tool.IsAvailable != null && !tool.IsAvailable(actions))) continue;
                    Button button = CreateToolButton(tool, false);
                    button.style.height = 34f;
                    button.style.marginBottom = 4f;
                    scroll.Add(button);
                }
            leftContent.Add(scroll);
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
                button.style.backgroundColor = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
                button.style.color = Color.white;
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
            Image icon = new Image { name = "Icon", scaleMode = ScaleMode.ScaleToFit };
            icon.style.width = 52f;
            icon.style.height = 52f;
            icon.style.flexShrink = 0f;
            icon.style.marginRight = 7f;
            row.Add(icon);
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
            Label badge = new Label { name = "Badge" };
            badge.style.fontSize = 9f;
            badge.style.paddingLeft = 5f;
            badge.style.paddingRight = 5f;
            badge.style.marginLeft = 4f;
            badge.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            row.Add(badge);
            row.userData = new ObjectRowState();
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                ObjectRowState state = (ObjectRowState)row.userData;
                state.pointerStart = evt.position;
                state.pointerDown = true;
            });
            row.RegisterCallback<PointerMoveEvent>(evt => TryStartObjectDrag(row, evt));
            row.RegisterCallback<PointerUpEvent>(_ => ((ObjectRowState)row.userData).pointerDown = false);
            return row;
        }

        private void BindObjectRow(VisualElement element, int index)
        {
            ESWorkbenchObjectDescriptor item = visibleObjects[index];
            element.Q<Label>("Title").text = item.DisplayName;
            element.Q<Label>("Category").text = item.Category;
            element.Q<Label>("Subtitle").text = string.IsNullOrWhiteSpace(item.Subtitle)
                ? (item.Source == null ? "工作台模板" : item.Source.GetType().Name)
                : item.Subtitle;
            Label badge = element.Q<Label>("Badge");
            badge.text = string.IsNullOrWhiteSpace(item.Badge) ? "可拖入" : item.Badge;
            Texture preview = item.Icon;
            if (preview == null && item.Source != null) preview = AssetPreview.GetAssetPreview(item.Source);
            element.Q<Image>("Icon").image = preview != null ? preview : AssetPreview.GetMiniThumbnail(item.Source);
            element.tooltip = string.IsNullOrWhiteSpace(item.Tooltip) ? item.ObjectId : item.Tooltip;
            ((ObjectRowState)element.userData).item = item;
        }

        private void TryStartObjectDrag(VisualElement row, PointerMoveEvent evt)
        {
            ObjectRowState state = row.userData as ObjectRowState;
            if (state == null || !state.pointerDown || Vector2.Distance(state.pointerStart, evt.position) < 5f) return;
            ESWorkbenchObjectDescriptor item = state.item;
            if (item == null) return;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(DragPayloadKey, item);
            DragAndDrop.objectReferences = item.Source == null ? Array.Empty<UnityEngine.Object>() : new[] { item.Source };
            DragAndDrop.StartDrag(item.DisplayName);
            state.pointerDown = false;
            evt.StopPropagation();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            ESWorkbenchObjectDescriptor item = ResolveDragItem();
            bool accepted = item != null && activeViewport != null && activeViewport.CanAccept(item);
            DragAndDrop.visualMode = accepted ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            ShowDropFeedback(item, evt.mousePosition, accepted);
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            HideDropFeedback();
            ESWorkbenchObjectDescriptor item = ResolveDragItem();
            if (item == null || activeViewport == null || !activeViewport.CanAccept(item))
            {
                DragAndDrop.SetGenericData(DragPayloadKey, null);
                evt.StopPropagation();
                return;
            }
            Vector2 local = centerContent.WorldToLocal(evt.mousePosition);
            var context = new ESWorkbenchDropContext(actions, item, local, centerContent.contentRect);
            if (activeViewport.TryAccept(context, out string message))
            {
                DragAndDrop.AcceptDrag();
                SetStatus(
                    string.IsNullOrWhiteSpace(message) ? "对象已放入工作区。" : message,
                    actions.Authoring.LastOperationCommittedWithPostCommitFailure
                        ? MessageType.Error : MessageType.Info);
            }
            else SetStatus(string.IsNullOrWhiteSpace(message) ? "当前视口拒绝该对象。" : message, MessageType.Warning);
            DragAndDrop.SetGenericData(DragPayloadKey, null);
            evt.StopPropagation();
        }

        private void ShowDropFeedback(ESWorkbenchObjectDescriptor item, Vector2 mousePosition, bool accepted)
        {
            if (dropFeedback == null || centerContent == null || item == null)
            {
                HideDropFeedback();
                return;
            }
            Vector2 local = centerContent.WorldToLocal(mousePosition);
            dropFeedback.style.left = Mathf.Clamp(local.x + 16f, 8f, Mathf.Max(8f, centerContent.contentRect.width - 220f));
            dropFeedback.style.top = Mathf.Clamp(local.y + 16f, 8f, Mathf.Max(8f, centerContent.contentRect.height - 44f));
            dropFeedback.style.borderLeftColor = accepted
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
            Label label = dropFeedback.Q<Label>("DropTitle");
            label.text = accepted ? "释放以放置 · " + item.DisplayName : "当前视口不能放置 · " + item.DisplayName;
            label.style.color = ESEditorPresentation.SectionTextColor;
            dropFeedback.style.display = DisplayStyle.Flex;
        }

        private void HideDropFeedback()
        {
            if (dropFeedback != null) dropFeedback.style.display = DisplayStyle.None;
        }

        private ESWorkbenchObjectDescriptor ResolveDragItem()
        {
            ESWorkbenchObjectDescriptor internalItem = DragAndDrop.GetGenericData(DragPayloadKey) as ESWorkbenchObjectDescriptor;
            if (internalItem != null) return internalItem;
            UnityEngine.Object[] references = DragAndDrop.objectReferences;
            if (references == null || references.Length != 1 || references[0] == null) return null;
            IReadOnlyList<ESWorkbenchObjectDescriptor> source = getObjects?.Invoke();
            return source?.FirstOrDefault(value => value != null && value.Source == references[0]);
        }

        private void RebuildObjectList()
        {
            visibleObjects.Clear();
            IReadOnlyList<ESWorkbenchObjectDescriptor> source = getObjects?.Invoke();
            string query = objectSearch?.value ?? string.Empty;
            if (source != null)
                visibleObjects.AddRange(source.Where(item => item != null
                    && (categoryFilter == "全部" || item.Category == categoryFilter)
                    && (string.IsNullOrWhiteSpace(query)
                        || item.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                        || item.ObjectId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderByDescending(item => item.Priority)
                    .ThenBy(item => item.Category, StringComparer.Ordinal)
                    .ThenBy(item => item.DisplayName, StringComparer.Ordinal));
            objectList?.Rebuild();
            if (objectEmptyLabel != null) objectEmptyLabel.style.display = visibleObjects.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (objectList != null) objectList.style.display = visibleObjects.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            BuildCategoryMenu();
        }

        private void BuildCategoryMenu()
        {
            if (categoryMenu == null) return;
            categoryMenu.menu.MenuItems().Clear();
            categoryMenu.text = categoryFilter;
            categoryMenu.menu.AppendAction("全部", _ => SetCategory("全部"), _ => categoryFilter == "全部" ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            IReadOnlyList<ESWorkbenchObjectDescriptor> source = getObjects?.Invoke();
            if (source == null) return;
            foreach (string category in source.Where(value => value != null).Select(value => value.Category).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            {
                string captured = category;
                categoryMenu.menu.AppendAction(captured, _ => SetCategory(captured), _ => categoryFilter == captured ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
        }

        private void SetCategory(string category)
        {
            categoryFilter = category;
            RebuildObjectList();
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
            SynchronizeListSelection(actions.Selection.Current);
        }

        private void RestoreStableSelection()
        {
            if (string.IsNullOrWhiteSpace(layout.selectedStableId)) return;
            if (!hierarchyById.TryGetValue(layout.selectedStableId, out ESWorkbenchHierarchyDescriptor item)) return;
            if (!string.IsNullOrEmpty(layout.selectedKind) && item.Kind != layout.selectedKind) return;
            actions.Selection.Select(item.ToSelection());
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
            Button visibility = new Button { name = "Visibility", text = "●", tooltip = "显示或隐藏该对象及其子项" };
            visibility.style.width = 24f;
            visibility.style.height = 21f;
            visibility.style.flexShrink = 0f;
            visibility.clicked += () =>
            {
                if (row.userData is ESWorkbenchHierarchyDescriptor item) ToggleHierarchyVisibility(item.ItemId);
            };
            row.Add(visibility);
            Button locking = new Button { name = "Locking", text = "开", tooltip = "锁定或解锁该对象及其子项" };
            locking.style.width = 28f;
            locking.style.height = 21f;
            locking.style.flexShrink = 0f;
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
            visibility.text = visible ? "●" : "○";
            visibility.tooltip = visible ? "当前可见，点击隐藏" : "当前隐藏，点击显示";
            Button locking = element.Q<Button>("Locking");
            bool locked = IsHierarchyLocked(item.ItemId);
            locking.text = locked ? "锁" : "开";
            locking.tooltip = locked ? "当前已锁定，点击解锁" : "当前可编辑，点击锁定";
            title.style.color = visible
                ? ESEditorPresentation.SectionTextColor
                : ESEditorPresentation.SectionMutedTextColor;
            element.tooltip = item.ItemId;
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
            activeDocument = id;
            layout.activeDocument = id;
            documentTabs?.Query<ToolbarToggle>().ForEach(toggle => toggle.SetValueWithoutNotify((string)toggle.userData == id));
            centerContent?.Clear();
            if (centerContent == null) return;
            bool showViewport = id == "viewport";
            viewportModeBar.style.display = showViewport ? DisplayStyle.Flex : DisplayStyle.None;
            if (toolRail != null) toolRail.style.display = showViewport ? DisplayStyle.Flex : DisplayStyle.None;
            if (viewportFooter != null) viewportFooter.style.display = showViewport ? DisplayStyle.Flex : DisplayStyle.None;
            if (showViewport)
            {
                if (activeViewport != null) centerContent.Add(activeViewport.Root);
                return;
            }
            if (!id.StartsWith("page:", StringComparison.Ordinal)) return;
            string pageId = id.Substring("page:".Length);
            ESWorkbenchPageDefinition page = getPages?.Invoke()?.FirstOrDefault(value => value.pageId == pageId);
            if (page == null) return;
            selectPage?.Invoke(pageId);
            VisualElement view = createPageView?.Invoke(page);
            if (view != null) centerContent.Add(view);
        }

        private void ActivateViewport(ESWorkbenchViewportDescriptor descriptor)
        {
            if (descriptor == null) return;
            activeViewport?.Deactivate();
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
                        IsHierarchyLocked));
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
            activeViewport.Activate();
            if (activeDocument == "viewport")
            {
                centerContent.Clear();
                centerContent.Add(activeViewport.Root);
            }
            Button frameAll = viewportModeBar?.Q<Button>("ESWorkbenchFrameAll");
            if (frameAll != null) frameAll.SetEnabled(activeViewport is IESWorkbenchFrameableViewport);
            UpdateViewportFooter();
        }

        private void FrameActiveViewport()
        {
            if (!(activeViewport is IESWorkbenchFrameableViewport frameable)) return;
            frameable.FrameAll();
            activeViewport.Refresh(ESWorkbenchRefreshReason.Explicit);
            SetStatus("视口已适配全部内容。", MessageType.Info);
        }

        private void OnSelectionChanged(ESWorkbenchSelection selection)
        {
            SynchronizeListSelection(selection);
            RebuildInspector(selection);
            activeViewport?.Refresh(ESWorkbenchRefreshReason.SelectionChanged);
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            float width = evt.newRect.width;
            if (width <= 1f || Mathf.Abs(width - availableWidth) < 1f) return;
            availableWidth = width;
            ApplyPaneVisibility(width);
        }

        private void ToggleLeftPane()
        {
            if (availableWidth < 1160f)
                layout.compactSidePane = layout.compactSidePane == "left" ? string.Empty : "left";
            else
                layout.leftPaneVisible = !layout.leftPaneVisible;
            ApplyPaneVisibility(availableWidth);
        }

        private void ToggleInspectorPane()
        {
            if (availableWidth < 1160f)
                layout.compactSidePane = layout.compactSidePane == "inspector" ? string.Empty : "inspector";
            else
                layout.inspectorPaneVisible = !layout.inspectorPaneVisible;
            ApplyPaneVisibility(availableWidth);
        }

        private void ApplyPaneVisibility(float width)
        {
            if (outerSplit == null || contentSplit == null) return;
            bool compact = width < 1160f;
            bool showLeft = compact ? layout.compactSidePane == "left" : layout.leftPaneVisible;
            bool showInspector = compact ? layout.compactSidePane == "inspector" : layout.inspectorPaneVisible;
            if (showLeft) outerSplit.UnCollapse();
            else outerSplit.CollapseChild(0);
            if (showInspector) contentSplit.UnCollapse();
            else contentSplit.CollapseChild(1);
            UpdatePaneButtons(showLeft, showInspector);
        }

        private void UpdatePaneButtons()
        {
            bool compact = availableWidth < 1160f;
            UpdatePaneButtons(
                compact ? layout.compactSidePane == "left" : layout.leftPaneVisible,
                compact ? layout.compactSidePane == "inspector" : layout.inspectorPaneVisible);
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
            layout.bottomDrawerExpanded = !layout.bottomDrawerExpanded;
            ApplyBottomDrawerVisibility();
        }

        private void ApplyBottomDrawerVisibility()
        {
            if (workspaceSplit == null) return;
            if (layout.bottomDrawerExpanded) workspaceSplit.UnCollapse();
            else workspaceSplit.CollapseChild(1);
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
            for (int i = 0; i < resolvedBottomPanels.Count; i++)
                AddBottomTab(bottomTabs, resolvedBottomPanels[i]);
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
                text = panel.Title,
                tooltip = panel.Tooltip,
                value = activeBottomTab == id,
                userData = id
            };
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) ShowBottomTab(id);
                else if (activeBottomTab == id) toggle.SetValueWithoutNotify(true);
            });
            parent.Add(toggle);
        }

        private void ShowBottomTab(string id)
        {
            activeBottomTab = id ?? string.Empty;
            layout.activeBottomTab = activeBottomTab;
            root?.Q<VisualElement>("ESWorkbenchBottomTabs")?.Query<ToolbarToggle>()
                .ForEach(toggle => toggle.SetValueWithoutNotify((string)toggle.userData == activeBottomTab));
            RebuildBottomDrawer();
        }

        private void RebuildBottomDrawer()
        {
            if (bottomContent == null) return;
            ReleaseBottomPanelContent();
            bottomContent.Clear();
            ResolveBottomPanels();
            ESWorkbenchBottomPanelDescriptor descriptor = resolvedBottomPanels.FirstOrDefault(
                value => value.PanelId == activeBottomTab);
            if (descriptor == null)
            {
                return;
            }
            try
            {
                var context = new ESWorkbenchBottomPanelContext(workbenchId, actions, getIssues?.Invoke());
                activeBottomPanelContent = descriptor.CreateContent(context);
                if (activeBottomPanelContent?.Root != null)
                    bottomContent.Add(activeBottomPanelContent.Root);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                bottomContent.Add(ESWindowPresentation.CreateEmptyState(
                    "面板加载失败",
                    "当前通道无法创建：" + exception.Message,
                    null,
                    null));
            }
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
                container.Add(ESWindowPresentation.CreateEmptyState(
                    emptyTitle,
                    "记录会按项目持久化并限制数量；窗口或脚本域重载后仍可查询。",
                    null,
                    null));
                return new ESWorkbenchBottomPanelContent(container);
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
                container.Add(ESWindowPresentation.CreateEmptyState(
                    emptyTitle,
                    "问题源会在资产、作者数据或任务状态变化后增量刷新。",
                    null,
                    null));
            else
                for (int i = 0; i < issues.Length; i++) container.Add(CreateIssueRow(issues[i]));
            return new ESWorkbenchBottomPanelContent(container);
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
            activeDocument = "viewport";
            ShowDocument("viewport");
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
            if (activeViewport is IESWorkbenchViewportStatusProvider statusProvider)
            {
                IReadOnlyList<ESWorkbenchViewportStatusDescriptor> statuses = statusProvider.GetStatusSnapshot();
                foreach (ESWorkbenchViewportStatusDescriptor status in
                    (statuses ?? Array.Empty<ESWorkbenchViewportStatusDescriptor>())
                    .Where(value => value != null)
                    .OrderByDescending(value => value.Priority)
                    .ThenBy(value => value.StatusId, StringComparer.Ordinal))
                {
                    Label label = new Label(string.IsNullOrEmpty(status.Label)
                        ? status.Value : status.Label + "：" + status.Value) { tooltip = status.Tooltip };
                    label.style.flexShrink = 0f;
                    viewportFooter.Add(label);
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
            viewportFooter.Add(new Label("中键/Alt 拖动画布 · 滚轮缩放"));
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
            string stableId = selection?.StableId ?? string.Empty;
            if (objectList != null)
            {
                int objectIndex = visibleObjects.FindIndex(value => value.ObjectId == stableId);
                if (objectIndex >= 0) objectList.SetSelectionWithoutNotify(new[] { objectIndex });
                else objectList.SetSelectionWithoutNotify(Array.Empty<int>());
            }
            if (hierarchyList != null)
            {
                int hierarchyIndex = visibleHierarchy.FindIndex(value => value.ItemId == stableId);
                if (hierarchyIndex >= 0) hierarchyList.SetSelectionWithoutNotify(new[] { hierarchyIndex });
                else hierarchyList.SetSelectionWithoutNotify(Array.Empty<int>());
            }
        }

        private void OnToolChanged(string toolId)
        {
            layout.activeToolId = toolId ?? string.Empty;
            BuildToolBar();
            if (activeLeftTab == "tools") ShowLeftTab("tools");
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
            IReadOnlyList<ESWorkbenchInspectorDescriptor> inspectors = getInspectors?.Invoke();
            ESWorkbenchInspectorDescriptor descriptor = inspectors?
                .Where(value => value != null && value.Matches(selection ?? ESWorkbenchSelection.Empty))
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.InspectorId, StringComparer.Ordinal)
                .FirstOrDefault();
            VisualElement view = descriptor?.CreateView(actions, selection ?? ESWorkbenchSelection.Empty);
            if (view != null)
            {
                view.style.flexGrow = 1f;
                view.style.minWidth = 0f;
                inspectorContent.Add(view);
                return;
            }
            inspectorContent.Add(ESWindowPresentation.CreateEmptyState(
                selection == null || selection.IsEmpty ? "未选择对象" : selection.StableId,
                "当前选择没有注册上下文 Inspector。",
                null,
                null));
        }

        private string ResolveSelectionTitle(ESWorkbenchSelection selection)
        {
            if (selection == null || selection.IsEmpty) return "检查器";
            ESWorkbenchHierarchyDescriptor hierarchyItem = hierarchyById.TryGetValue(selection.StableId, out ESWorkbenchHierarchyDescriptor value)
                ? value
                : null;
            if (hierarchyItem != null) return hierarchyItem.DisplayName;
            ESWorkbenchObjectDescriptor objectItem = visibleObjects.FirstOrDefault(item => item.ObjectId == selection.StableId);
            if (objectItem != null) return objectItem.DisplayName;
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
            IReadOnlyList<ESWorkbenchCommandDescriptor> commands = getCommands?.Invoke();
            if (commands == null) return;
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
            if (tool == null) return;
            ExecuteTool(tool);
            evt.StopImmediatePropagation();
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
            actions.Selection.Changed -= OnSelectionChanged;
            actions.Tools.Changed -= OnToolChanged;
            ReleaseContributedContent();
            activeViewport?.Deactivate();
            foreach (IESWorkbenchViewport viewport in liveViewports.Values)
            {
                try { viewport?.Dispose(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            liveViewports.Clear();
            activeViewport = null;
            root?.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root?.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }
    }

    internal sealed class ESWorkbenchCanvas2DViewport : VisualElement, IESWorkbenchViewport, IESWorkbenchFrameableViewport
    {
        private readonly ESWorkbenchViewportContext context;
        private readonly ESWorkbenchViewportLayoutState viewportLayout;
        private readonly List<ESWorkbenchHierarchyDescriptor> projected = new List<ESWorkbenchHierarchyDescriptor>();
        private readonly VisualElement labelOverlay;
        private Vector2 pan;
        private float zoom;
        private bool panning;
        private int panPointerId = -1;
        private Vector2 lastPointer;
        private bool moving;
        private int movePointerId = -1;
        private Vector2 moveStart;
        private Vector3 pendingMove;
        private bool pendingMoveValid;
        private ESWorkbenchSelection movingSelection;

        public ESWorkbenchCanvas2DViewport(ESWorkbenchViewportContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            viewportLayout = context.Layout;
            pan = viewportLayout.pan;
            zoom = Mathf.Clamp(viewportLayout.zoom <= 0f ? 1f : viewportLayout.zoom, 0.35f, 12f);
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
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<WheelEvent>(OnWheel);
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
            StopPanning();
            StopMoving();
        }

        public void Refresh(ESWorkbenchRefreshReason reason)
        {
            if (reason != ESWorkbenchRefreshReason.SelectionChanged) RebuildProjection();
            else UpdateLabelPositions();
            MarkDirtyRepaint();
        }

        public void FrameAll()
        {
            pan = Vector2.zero;
            zoom = 1f;
            SaveViewTransform();
            UpdateLabelPositions();
            MarkDirtyRepaint();
        }

        public bool CanAccept(ESWorkbenchObjectDescriptor item) =>
            item != null && context.Actions.Authoring.CanCreate(item);

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
            Rect worldBounds = ResolveWorldBounds();
            Rect canvasBounds = ResolveCanvasBounds(viewport, worldBounds);
            DrawGrid(painter, canvasBounds);
            for (int i = 0; i < projected.Count; i++) DrawItem(painter, projected[i], worldBounds, canvasBounds);
            if (moving && pendingMoveValid)
                DrawCrosshair(painter, WorldToCanvas(pendingMove, worldBounds, canvasBounds), 9f,
                    ESEditorPresentation.SelectionColor);
        }

        private static void DrawGrid(Painter2D painter, Rect rect)
        {
            painter.strokeColor = new Color(0.24f, 0.28f, 0.3f, 0.34f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            const int lines = 12;
            for (int i = 0; i <= lines; i++)
            {
                float x = Mathf.Lerp(rect.xMin, rect.xMax, i / (float)lines);
                float y = Mathf.Lerp(rect.yMin, rect.yMax, i / (float)lines);
                painter.MoveTo(new Vector2(x, rect.yMin));
                painter.LineTo(new Vector2(x, rect.yMax));
                painter.MoveTo(new Vector2(rect.xMin, y));
                painter.LineTo(new Vector2(rect.xMax, y));
            }
            painter.Stroke();
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
            Color color = selected ? ESEditorPresentation.SelectionColor : spatial.Color;
            if (spatial.Shape == ESWorkbenchSpatialShape.Rectangle)
            {
                Vector2 half = WorldSizeToCanvas(spatial.Size, worldBounds, canvasBounds) * 0.5f;
                Rect rect = new Rect(center - half, half * 2f);
                FillRect(painter, rect, color);
                if (selected) StrokeRect(painter, rect, ESEditorPresentation.SelectionColor, 2.5f);
                return;
            }
            float radius = selected ? 7f : spatial.Shape == ESWorkbenchSpatialShape.Point ? 5f : 6f;
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
            Focus();
            Vector2 local = this.WorldToLocal(evt.position);
            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                panning = true;
                panPointerId = evt.pointerId;
                lastPointer = local;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }
            if (evt.button != 0) return;
            ESWorkbenchHierarchyDescriptor hit = HitTest(local);
            if (hit == null) return;
            context.Selection.Select(hit.ToSelection());
            if (IsMoveTool() && !context.IsHierarchyLocked(context.Selection.Current?.StableId)
                && context.Actions.Authoring.CanMove(context.Selection.Current))
            {
                moving = true;
                movePointerId = evt.pointerId;
                moveStart = local;
                movingSelection = context.Selection.Current;
                pendingMove = hit.Spatial.Position;
                pendingMoveValid = false;
                this.CapturePointer(evt.pointerId);
            }
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            Vector2 local = this.WorldToLocal(evt.position);
            if (moving && evt.pointerId == movePointerId && this.HasPointerCapture(evt.pointerId))
            {
                if (Vector2.Distance(moveStart, local) >= 3f)
                {
                    pendingMove = context.SnapPosition(CanvasToWorld(local));
                    pendingMoveValid = true;
                    MarkDirtyRepaint();
                }
                evt.StopPropagation();
                return;
            }
            if (!panning || evt.pointerId != panPointerId || !this.HasPointerCapture(evt.pointerId)) return;
            pan += local - lastPointer;
            lastPointer = local;
            SaveViewTransform();
            UpdateLabelPositions();
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (moving && evt.pointerId == movePointerId)
            {
                ESWorkbenchSelection target = movingSelection;
                Vector3 world = pendingMove;
                bool commit = pendingMoveValid;
                if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
                StopMoving();
                if (commit) context.Actions.Authoring.TryMove(target, world, out _);
                evt.StopPropagation();
                return;
            }
            if (!panning || evt.pointerId != panPointerId) return;
            if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
            StopPanning();
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == panPointerId) StopPanning();
            if (evt.pointerId == movePointerId) StopMoving();
        }

        private void OnWheel(WheelEvent evt)
        {
            Vector2 local = this.WorldToLocal(evt.mousePosition);
            Rect worldBounds = ResolveWorldBounds();
            Rect before = ResolveCanvasBounds(contentRect, worldBounds);
            Vector2 normalized = new Vector2(
                Mathf.InverseLerp(before.xMin, before.xMax, local.x),
                Mathf.InverseLerp(before.yMin, before.yMax, local.y));
            zoom = Mathf.Clamp(zoom * Mathf.Exp(-evt.delta.y * 0.035f), 0.35f, 12f);
            Rect after = ResolveCanvasBounds(contentRect, worldBounds);
            Vector2 anchored = new Vector2(
                Mathf.Lerp(after.xMin, after.xMax, normalized.x),
                Mathf.Lerp(after.yMin, after.yMax, normalized.y));
            pan += local - anchored;
            SaveViewTransform();
            UpdateLabelPositions();
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private ESWorkbenchHierarchyDescriptor HitTest(Vector2 local)
        {
            Rect worldBounds = ResolveWorldBounds();
            Rect canvasBounds = ResolveCanvasBounds(contentRect, worldBounds);
            for (int i = projected.Count - 1; i >= 0; i--)
            {
                ESWorkbenchHierarchyDescriptor item = projected[i];
                Vector2 center = WorldToCanvas(item.Spatial.Position, worldBounds, canvasBounds);
                if (item.Spatial.Shape == ESWorkbenchSpatialShape.Rectangle)
                {
                    Vector2 half = WorldSizeToCanvas(item.Spatial.Size, worldBounds, canvasBounds) * 0.5f;
                    if (new Rect(center - half, half * 2f).Contains(local)) return item;
                }
                else if (Vector2.Distance(center, local) <= 10f) return item;
            }
            return null;
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
            float availableWidth = Mathf.Max(1f, viewport.width - 32f);
            float availableHeight = Mathf.Max(1f, viewport.height - 32f);
            float aspect = worldBounds.width / Mathf.Max(0.001f, worldBounds.height);
            float width = availableWidth;
            float height = width / aspect;
            if (height > availableHeight) { height = availableHeight; width = height * aspect; }
            Vector2 size = new Vector2(width, height) * zoom;
            return new Rect(viewport.center + pan - size * 0.5f, size);
        }

        private static Vector2 WorldToCanvas(Vector3 value, Rect worldBounds, Rect canvasBounds)
        {
            return new Vector2(
                Mathf.Lerp(canvasBounds.xMin, canvasBounds.xMax, Mathf.InverseLerp(worldBounds.xMin, worldBounds.xMax, value.x)),
                Mathf.Lerp(canvasBounds.yMax, canvasBounds.yMin, Mathf.InverseLerp(worldBounds.yMin, worldBounds.yMax, value.z)));
        }

        private static Vector2 WorldSizeToCanvas(Vector3 size, Rect worldBounds, Rect canvasBounds)
        {
            return new Vector2(
                size.x / Mathf.Max(0.001f, worldBounds.width) * canvasBounds.width,
                size.z / Mathf.Max(0.001f, worldBounds.height) * canvasBounds.height);
        }

        private Vector3 CanvasToWorld(Vector2 value)
        {
            Rect worldBounds = ResolveWorldBounds();
            Rect canvasBounds = ResolveCanvasBounds(contentRect, worldBounds);
            float x = Mathf.Lerp(worldBounds.xMin, worldBounds.xMax,
                Mathf.InverseLerp(canvasBounds.xMin, canvasBounds.xMax, value.x));
            float z = Mathf.Lerp(worldBounds.yMin, worldBounds.yMax,
                1f - Mathf.InverseLerp(canvasBounds.yMin, canvasBounds.yMax, value.y));
            float y = movingSelection?.StableId != null
                ? projected.FirstOrDefault(item => item.ItemId == movingSelection.StableId)?.Spatial?.Position.y ?? 0f
                : 0f;
            return new Vector3(x, y, z);
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

        private bool IsMoveTool()
        {
            string toolId = context.Actions.Tools.ActiveToolId ?? string.Empty;
            return toolId == "core.move" || toolId == "core.select" || toolId.EndsWith(".select", StringComparison.Ordinal);
        }

        private void SaveViewTransform()
        {
            viewportLayout.pan = pan;
            viewportLayout.zoom = zoom;
        }

        private void StopPanning()
        {
            panning = false;
            panPointerId = -1;
        }

        private void StopMoving()
        {
            moving = false;
            movePointerId = -1;
            pendingMoveValid = false;
            movingSelection = null;
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
            generateVisualContent -= DrawCanvas;
            projected.Clear();
            labelOverlay.Clear();
        }
    }

    internal sealed class ESWorkbenchPreview3DViewport : IESWorkbenchViewport, IESWorkbenchFrameableViewport
    {
        private readonly ESWorkbenchViewportContext context;
        private readonly VisualElement root;
        private readonly IMGUIContainer renderHost;
        private readonly List<GameObject> instances = new List<GameObject>();
        private readonly List<ESWorkbenchHierarchyDescriptor> instanceItems = new List<ESWorkbenchHierarchyDescriptor>();
        private ESEditorPreviewRenderContext preview;
        private Vector3 focus;
        private float distance = 8f;
        private float yaw = 35f;
        private float pitch = 25f;
        private bool orbiting;
        private bool panning;
        private bool moving;
        private bool rotating;
        private bool scaling;
        private bool pendingMoveValid;
        private Vector3 pendingMove;
        private Vector2 transformStartMouse;
        private Vector3 transformStartValue;
        private Vector3 pendingTransformValue;
        private bool pendingTransformValid;
        private ESWorkbenchSelection movingSelection;
        private Vector2 lastMouse;
        private int activeControlId;

        public ESWorkbenchPreview3DViewport(ESWorkbenchViewportContext context)
        {
            this.context = context;
            root = new VisualElement { name = "ESWorkbenchPreview3D" };
            root.style.flexGrow = 1f;
            root.style.minWidth = 0f;
            root.style.minHeight = 0f;
            renderHost = new IMGUIContainer(DrawPreview);
            renderHost.style.flexGrow = 1f;
            renderHost.style.minWidth = 0f;
            renderHost.style.minHeight = 0f;
            renderHost.tooltip = "右键旋转视角，中键平移，滚轮缩放；选择变换工具后拖动对象提交作者事务。";
            root.Add(renderHost);
            EnsurePreview();
            RebuildInstances(true);
        }

        public VisualElement Root => root;
        public void Activate() => renderHost.MarkDirtyRepaint();
        public void Deactivate()
        {
            bool restorePreview = moving || rotating || scaling;
            orbiting = false;
            panning = false;
            StopMoving();
            ReleaseMouseControl();
            if (restorePreview) RebuildInstances();
        }
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
            focus = aggregate.center;
            distance = Mathf.Max(2f, aggregate.extents.magnitude * 2.5f);
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
            if (frameContent && instances.Count > 0) FrameAll();
        }

        private bool TryResolveWorldPoint(Vector2 localPosition, out Vector3 worldPosition)
        {
            Rect rect = renderHost.contentRect;
            return TryResolveWorldPoint(rect, localPosition, out worldPosition);
        }

        private bool TryResolveWorldPoint(Rect rect, Vector2 localPosition, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (preview?.Camera == null || rect.width <= 1f || rect.height <= 1f) return false;
            Vector3 viewportPoint = new Vector3(
                Mathf.InverseLerp(rect.xMin, rect.xMax, localPosition.x),
                1f - Mathf.InverseLerp(rect.yMin, rect.yMax, localPosition.y),
                0f);
            Ray ray = preview.Camera.ViewportPointToRay(viewportPoint);
            Plane plane = new Plane(Vector3.up, preview.GroupOrigin);
            if (!plane.Raycast(ray, out float hitDistance)) return false;
            worldPosition = ray.GetPoint(hitDistance) - preview.GroupOrigin;
            return true;
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
                preview.Camera.fieldOfView = 40f;
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
                    preview.GroupOrigin + focus,
                    1f,
                    yaw,
                    pitch,
                    Mathf.Max(0.05f, distance / 2.8f)),
                ESEditorPreviewRenderOptions.Balanced);
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleInput(rect, controlId);
            Rect badge = new Rect(rect.x + 10f, rect.y + 10f, 112f, 23f);
            EditorGUI.DrawRect(badge, new Color(0.01f, 0.015f, 0.02f, 0.82f));
            GUI.Label(new Rect(badge.x + 7f, badge.y + 3f, badge.width - 12f, 17f), "三维作者视图", EditorStyles.miniLabel);
        }

        private void HandleInput(Rect rect, int controlId)
        {
            Event evt = Event.current;
            if (!rect.Contains(evt.mousePosition) && !orbiting && !panning && !moving && !rotating && !scaling) return;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape
                && (moving || rotating || scaling))
            {
                StopMoving();
                ReleaseMouseControl();
                RebuildInstances();
                evt.Use();
                return;
            }
            if (evt.type == EventType.ScrollWheel)
            {
                distance = Mathf.Clamp(distance * (1f + evt.delta.y * 0.055f), 0.3f, 5000f);
                evt.Use();
                return;
            }
            if (evt.type == EventType.MouseDown)
            {
                lastMouse = evt.mousePosition;
                orbiting = evt.button == 1 || (evt.button == 0 && evt.alt);
                panning = evt.button == 2;
                if (orbiting || panning) evt.Use();
                else if (evt.button == 0 && TryHitItem(rect, evt.mousePosition, out ESWorkbenchHierarchyDescriptor item))
                {
                    context.Selection.Select(item.ToSelection());
                    if (IsMoveTool() && !context.IsHierarchyLocked(context.Selection.Current?.StableId)
                        && context.Actions.Authoring.CanMove(context.Selection.Current))
                    {
                        moving = true;
                        movingSelection = context.Selection.Current;
                        pendingMove = item.Spatial.Position;
                        pendingMoveValid = false;
                    }
                    else if (IsRotateTool() && !context.IsHierarchyLocked(context.Selection.Current?.StableId)
                        && context.Actions.Authoring.CanRotate(context.Selection.Current))
                    {
                        rotating = true;
                        movingSelection = context.Selection.Current;
                        transformStartMouse = evt.mousePosition;
                        transformStartValue = item.Spatial.RotationEuler;
                        pendingTransformValue = transformStartValue;
                        pendingTransformValid = false;
                    }
                    else if (IsScaleTool() && !context.IsHierarchyLocked(context.Selection.Current?.StableId)
                        && context.Actions.Authoring.CanScale(context.Selection.Current))
                    {
                        scaling = true;
                        movingSelection = context.Selection.Current;
                        transformStartMouse = evt.mousePosition;
                        transformStartValue = item.Spatial.Size;
                        pendingTransformValue = transformStartValue;
                        pendingTransformValid = false;
                    }
                    evt.Use();
                }
                if (orbiting || panning || moving || rotating || scaling)
                {
                    activeControlId = controlId;
                    GUIUtility.hotControl = controlId;
                }
            }
            if (evt.type == EventType.MouseDrag && (rotating || scaling))
            {
                Vector2 delta = evt.mousePosition - transformStartMouse;
                if (rotating)
                    pendingTransformValue = context.SnapRotation(
                        transformStartValue + new Vector3(0f, delta.x * 0.6f, 0f));
                else
                    pendingTransformValue = context.SnapScale(
                        transformStartValue * Mathf.Exp((delta.x - delta.y) * 0.01f));
                pendingTransformValid = true;
                ApplyPreviewTransform(
                    movingSelection?.StableId,
                    rotating ? ESWorkbenchMutationKind.Rotate : ESWorkbenchMutationKind.Scale,
                    pendingTransformValue);
                renderHost.MarkDirtyRepaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && moving)
            {
                if (TryResolveWorldPoint(rect, evt.mousePosition, out Vector3 worldPosition))
                {
                    pendingMove = context.SnapPosition(worldPosition);
                    pendingMoveValid = true;
                    ApplyPreviewTransform(movingSelection?.StableId, ESWorkbenchMutationKind.Move, pendingMove);
                    renderHost.MarkDirtyRepaint();
                }
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && (orbiting || panning))
            {
                Vector2 delta = evt.mousePosition - lastMouse;
                lastMouse = evt.mousePosition;
                if (orbiting)
                {
                    yaw += delta.x * 0.35f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.25f, -80f, 80f);
                }
                else
                {
                    Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
                    focus += (-(rotation * Vector3.right) * delta.x + rotation * Vector3.up * delta.y) * distance * 0.0018f;
                }
                evt.Use();
            }
            if (evt.type == EventType.MouseUp && (rotating || scaling))
            {
                ESWorkbenchSelection target = movingSelection;
                Vector3 value = pendingTransformValue;
                bool commit = pendingTransformValid;
                bool commitRotation = rotating;
                StopMoving();
                ReleaseMouseControl();
                if (commit)
                {
                    bool succeeded = commitRotation
                        ? context.Actions.Authoring.TryRotate(target, value, out _)
                        : context.Actions.Authoring.TryScale(target, value, out _);
                    if (!succeeded) RebuildInstances();
                }
                else RebuildInstances();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && moving)
            {
                ESWorkbenchSelection target = movingSelection;
                Vector3 worldPosition = pendingMove;
                bool commit = pendingMoveValid;
                StopMoving();
                ReleaseMouseControl();
                if (commit)
                {
                    if (!context.Actions.Authoring.TryMove(target, worldPosition, out _)) RebuildInstances();
                }
                else RebuildInstances();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && (orbiting || panning))
            {
                orbiting = false;
                panning = false;
                ReleaseMouseControl();
                evt.Use();
            }
        }

        private bool TryHitItem(Rect rect, Vector2 guiPoint, out ESWorkbenchHierarchyDescriptor item)
        {
            item = null;
            if (preview?.Camera == null || rect.width <= 1f || rect.height <= 1f) return false;
            Vector3 viewportPoint = new Vector3(
                Mathf.InverseLerp(rect.xMin, rect.xMax, guiPoint.x),
                1f - Mathf.InverseLerp(rect.yMin, rect.yMax, guiPoint.y),
                0f);
            Ray ray = preview.Camera.ViewportPointToRay(viewportPoint);
            float nearest = float.MaxValue;
            for (int i = 0; i < instances.Count && i < instanceItems.Count; i++)
            {
                GameObject instance = instances[i];
                if (instance == null) continue;
                if (!CalculateBounds(instance).IntersectRay(ray, out float distanceToBounds) || distanceToBounds >= nearest) continue;
                nearest = distanceToBounds;
                item = instanceItems[i];
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

        private bool IsMoveTool()
        {
            string toolId = context.Actions.Tools.ActiveToolId ?? string.Empty;
            return toolId == "core.move" || toolId == "core.select" || toolId.EndsWith(".select", StringComparison.Ordinal);
        }

        private bool IsRotateTool()
        {
            return string.Equals(context.Actions.Tools.ActiveToolId, "core.rotate", StringComparison.Ordinal);
        }

        private bool IsScaleTool()
        {
            return string.Equals(context.Actions.Tools.ActiveToolId, "core.scale", StringComparison.Ordinal);
        }

        private void StopMoving()
        {
            moving = false;
            rotating = false;
            scaling = false;
            pendingMoveValid = false;
            pendingTransformValid = false;
            movingSelection = null;
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
            Bounds bounds = CalculateBounds(instances[0]);
            for (int i = 1; i < instances.Count; i++) bounds.Encapsulate(CalculateBounds(instances[i]));
            bounds.center -= preview?.GroupOrigin ?? Vector3.zero;
            return bounds;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        public void Dispose()
        {
            StopMoving();
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
        }
    }

    internal sealed class ESWorkbenchPopupWindow : EditorWindow
    {
        private ESWorkbenchPopupRequest request;
        private ESWorkbenchActionContext context;
        private IDisposable ownerHold;

        internal static void Open(EditorWindow owner, ESWorkbenchPopupRequest request, ESWorkbenchActionContext context, Rect screenAnchor)
        {
            if (owner == null || request == null || context == null) return;
            ESWorkbenchPopupWindow window = CreateInstance<ESWorkbenchPopupWindow>();
            window.request = request;
            window.context = context;
            window.titleContent = new GUIContent(request.Title);
            window.ownerHold = ESWindowFoundation.HoldInteraction(owner, "ES Workbench Popup");
            window.ShowAsDropDown(screenAnchor, request.Size);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;
            rootVisualElement.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            VisualElement content = request?.CreateContent(context);
            if (content != null)
            {
                content.style.flexGrow = 1f;
                content.style.minWidth = 0f;
                content.style.minHeight = 0f;
                rootVisualElement.Add(content);
            }
        }

        private void OnDisable()
        {
            ownerHold?.Dispose();
            ownerHold = null;
        }
    }
}
#endif
