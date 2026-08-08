using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES.EditorInternal
{
    internal enum ESGraphNodeAlignment : byte
    {
        Left,
        HorizontalCenter,
        Right,
        Top,
        VerticalCenter,
        Bottom
    }

    internal enum ESGraphNodeDistribution : byte
    {
        Horizontal,
        Vertical
    }

    internal enum ESStableGraphCreationTemplateKind : byte
    {
        Blank,
        GenericFlow,
        Story,
        BehaviorTree,
        AgentAICommand,
        AgentSkill,
        AgentPaired,
        AgentMindMap
    }

    public sealed class ESStableGraphViewWindow : EditorWindow
    {
        private const string DefaultGraphFolder = "Assets/ESNormalAssets/Data/Graphs";
        private const string OnboardingPreferencePrefix = "ES.StableGraphV2.OnboardingCompleted.";
        private const string EdgeFlowPreferenceKey = "ES.StableGraphV2.EdgeFlowEnabled";
        // Coalesce a burst of graph edits into one disk write while still protecting normal editor work.
        private const double AutoSaveDelaySeconds = 1.25d;
        private const double AssetRevisionPollSeconds = 0.2d;
        private const double ProjectChangeDebounceSeconds = 0.15d;
        private const long SearchDelayMilliseconds = 250L;
        private const float InitialWindowMargin = 24f;
        private static readonly Vector2 MinimumWindowSize = new Vector2(760f, 500f);
        private static readonly Vector2 DefaultWindowSize = new Vector2(1180f, 720f);
        private enum ToolbarLayoutMode : byte { Compact, Standard, Wide }
        private ESStableGraphView graphView;
        private ESStableGraphInspector inspector;
        private VisualElement toolbarContainer;
        private Toolbar primaryToolbar;
        private Toolbar secondaryToolbar;
        private ObjectField assetField;
        private ToolbarButton createButton;
        private ToolbarButton openButton;
        private ToolbarButton addNodeButton;
        private ToolbarButton duplicateButton;
        private ToolbarMenu organizeMenu;
        private ToolbarButton frameAllButton;
        private ToolbarButton validateButton;
        private ToolbarButton bakeButton;
        private ToolbarButton saveButton;
        private ToolbarButton saveAsButton;
        private ToolbarButton guideButton;
        private ToolbarMenu moreMenu;
        private Label searchLabel;
        private ToolbarSearchField searchField;
        private Label statusLabel;
        private IVisualElementScheduledItem searchSchedule;
        private ToolbarLayoutMode toolbarLayoutMode = (ToolbarLayoutMode)byte.MaxValue;
        private ESGraphAsset autoSaveAsset;
        private double autoSaveDueTime;
        private bool autoSavePending;
        private ESGraphAsset observedAsset;
        private int observedAssetDirtyCount = int.MinValue;
        private Hash128 observedAssetDependencyHash;
        private double nextAssetRevisionPollTime;
        private double projectChangeCheckDueTime;
        private bool projectChangeCheckPending;
        [SerializeField] private string currentGraphAssetGuid = string.Empty;

        [MenuItem(MenuItemPathDefine.CONTENT_CREATION_PATH + "图与流程/稳定图编辑器 V2", false, 31)]
        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "稳定图编辑器 V2", false, -955)]
        public static ESStableGraphViewWindow ShowWindow()
        {
            ESWindowCommandRegistry.RecordOpened("stable_graph_v2");
            bool alreadyOpen = HasOpenInstances<ESStableGraphViewWindow>();
            ESStableGraphViewWindow window = GetWindow<ESStableGraphViewWindow>();
            window.ApplyWindowPresentation();
            if (!alreadyOpen && !window.docked)
                window.PlaceInitialFloatingWindow();
            window.Show();
            window.Focus();
            return window;
        }

        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTEXT_PATH + MenuItemPathDefine.CONTENT_CREATION + "/图与流程/通用流程图", false, 181)]
        private static void CreateGenericGraphFromAssetsMenu()
        {
            ShowWindow().CreateTemplate(ESStableGraphCreationTemplateKind.GenericFlow);
        }

        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTEXT_PATH + MenuItemPathDefine.CONTENT_CREATION + "/图与流程/剧情任务与对话图", false, 182)]
        private static void CreateStoryGraphFromAssetsMenu()
        {
            ShowWindow().CreateTemplate(ESStableGraphCreationTemplateKind.Story);
        }

        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTEXT_PATH + MenuItemPathDefine.CONTENT_CREATION + "/图与流程/行为树调度图", false, 183)]
        private static void CreateBehaviorTreeFromAssetsMenu()
        {
            ShowWindow().CreateTemplate(ESStableGraphCreationTemplateKind.BehaviorTree);
        }

        private void ApplyWindowPresentation()
        {
            titleContent = new GUIContent("ES 稳定图 V2");
            minSize = MinimumWindowSize;
        }

        private void PlaceInitialFloatingWindow()
        {
            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            if (mainWindow.width <= 0f || mainWindow.height <= 0f)
                return;

            float horizontalMargin = Mathf.Min(InitialWindowMargin, mainWindow.width * 0.05f);
            float verticalMargin = Mathf.Min(InitialWindowMargin, mainWindow.height * 0.05f);
            float availableWidth = Mathf.Max(1f, mainWindow.width - horizontalMargin * 2f);
            float availableHeight = Mathf.Max(1f, mainWindow.height - verticalMargin * 2f);
            float width = Mathf.Clamp(
                DefaultWindowSize.x,
                Mathf.Min(MinimumWindowSize.x, availableWidth),
                availableWidth);
            float height = Mathf.Clamp(
                DefaultWindowSize.y,
                Mathf.Min(MinimumWindowSize.y, availableHeight),
                availableHeight);

            position = new Rect(
                mainWindow.x + (mainWindow.width - width) * 0.5f,
                mainWindow.y + (mainWindow.height - height) * 0.5f,
                width,
                height);
        }

        [OnOpenAsset]
        private static bool OpenAsset(int instanceId, int line)
        {
            ESGraphAsset asset = EditorUtility.InstanceIDToObject(instanceId) as ESGraphAsset;
            if (asset == null)
                return false;
            ESStableGraphViewWindow window = ShowWindow();
            window.OpenGraph(asset);
            return true;
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            ESEditorPresentation.BindWindow(this);
            toolbarContainer = new VisualElement();
            toolbarContainer.style.flexShrink = 0f;
            primaryToolbar = new Toolbar();
            secondaryToolbar = new Toolbar();
            toolbarContainer.Add(primaryToolbar);
            toolbarContainer.Add(secondaryToolbar);
            assetField = new ObjectField("图资产（可拖入）")
            {
                objectType = typeof(ESGraphAsset),
                allowSceneObjects = false,
                tooltip = "从 Project 窗口拖入一张图资产，或点击“新建图”开始。"
            };
            assetField.style.flexGrow = 1f;
            assetField.RegisterValueChangedCallback(evt => SetAsset(evt.newValue as ESGraphAsset));
            createButton = CreateToolbarButton("新建图", "选择领域模板后创建图资产。",
                () => ShowCreationTemplateMenu(createButton));
            openButton = CreateToolbarButton("打开已有", "搜索并打开项目中的已有图资产。",
                () => OpenExistingAssetMenu(openButton));
            addNodeButton = CreateToolbarButton("添加节点", "在画布中选择一个中文节点模板。",
                () => graphView?.OpenNodeSearchAtCenter());
            duplicateButton = CreateToolbarButton("复制选中", "复制当前选中的节点及其内部连线。",
                () => graphView?.DuplicateSelection());
            organizeMenu = CreateOrganizeMenu();
            frameAllButton = CreateToolbarButton("视图居中", "把当前图完整显示在画布中央。",
                () => graphView?.FrameAll());
            validateButton = CreateToolbarButton("检查图", "检查节点、连线和业务规则，定位需要修正的地方。",
                ValidateCurrentAsset);
            bakeButton = CreateToolbarButton("生成检查快照", "生成只读检查结果，不会运行图，也不会直接写正式文件。",
                BakeCurrentAsset);
            saveButton = CreateToolbarButton("立即保存", "通常会自动保存；需要时可立即写入当前图资产。",
                SaveCurrentAsset);
            saveAsButton = CreateToolbarButton("另存为", "复制当前图资产并建立新的独立 GraphId。",
                SaveCurrentAssetAs);
            guideButton = CreateToolbarButton("使用引导", "再次查看面向新用户的四步操作说明。", OpenOnboarding);
            moreMenu = CreateMoreMenu();
            searchLabel = new Label("查找：");
            searchField = new ToolbarSearchField();
            searchField.style.minWidth = 180f;
            searchField.style.flexGrow = 1f;
            searchField.tooltip = "输入中文标题、节点类型、节点编号或端口编号；停止输入约 0.25 秒后自动定位。";
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchSchedule?.Pause();
                string query = evt.newValue;
                searchSchedule = searchField.schedule.Execute(() => graphView?.FindAndFrame(query))
                    .StartingIn(SearchDelayMilliseconds);
            });
            statusLabel = new Label("请选择图资产，或点击“新建图”开始");
            statusLabel.tooltip = "这里显示当前图的数量、检查结果和自动保存状态。";
            statusLabel.style.marginLeft = 8f;
            statusLabel.style.flexGrow = 1f;
            statusLabel.style.whiteSpace = WhiteSpace.NoWrap;
            statusLabel.style.overflow = Overflow.Hidden;
            statusLabel.style.textOverflow = TextOverflow.Ellipsis;
            rootVisualElement.Add(toolbarContainer);
            toolbarLayoutMode = (ToolbarLayoutMode)byte.MaxValue;
            ApplyToolbarLayout(position.width > 0f ? position.width : 1200f);
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            graphView = new ESStableGraphView(this, UpdateStatus);
            graphView.SetEdgeFlowEnabled(EditorPrefs.GetBool(EdgeFlowPreferenceKey, true));
            graphView.SetOnboardingVisible(!HasCompletedOnboarding());
            graphView.style.flexGrow = 1f;
            graphView.style.backgroundColor = new Color(0.075f, 0.085f, 0.11f, 1f);
            inspector = new ESStableGraphInspector(this, () => graphView?.Rebuild(), UpdateStatus,
                id => graphView?.FindAndFrame(id), () => RequestAutoSave(graphView?.Asset));
            TwoPaneSplitView workspace = new TwoPaneSplitView(1, 360f, TwoPaneSplitViewOrientation.Horizontal);
            workspace.style.flexGrow = 1f;
            workspace.Add(graphView);
            workspace.Add(inspector);
            rootVisualElement.Add(workspace);
            graphView.SelectionChanged += inspector.SetSelection;

            if (!TryRestoreCurrentAsset() && Selection.activeObject is ESGraphAsset selected)
                SetAsset(selected);
        }

        private ToolbarMenu CreateMoreMenu()
        {
            ToolbarMenu menu = new ToolbarMenu { text = "更多" };
            menu.tooltip = "窄窗口下从这里访问完整操作。";
            menu.menu.AppendAction("新建图...", _ => ShowCreationTemplateMenu(menu));
            menu.menu.AppendAction("打开已有图...", _ => OpenExistingAssetMenu(menu));
            menu.menu.AppendAction("保存/立即保存", _ => SaveCurrentAsset(),
                _ => graphView?.Asset != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction("保存/另存为...", _ => SaveCurrentAssetAs(),
                _ => graphView?.Asset != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("选择/全选节点    Ctrl/Cmd+A", _ => graphView?.SelectAllNodes(),
                _ => graphView?.Asset != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction("选择/选择同类节点", _ => graphView?.SelectSameType(),
                _ => HasSelectedNodes(1) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction("选择/取消选择", _ => graphView?.ClearGraphSelection(),
                _ => graphView != null && graphView.HasSelection
                    ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction("选择/复制选中    Ctrl/Cmd+D", _ => graphView?.DuplicateSelection(),
                _ => HasSelectedNodes(1) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            AppendOrganizeActions(menu.menu, "整理/");
            menu.menu.AppendAction("视图/聚焦选中    F", _ => graphView?.FrameSelectionOrAll(),
                _ => graphView?.Asset != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction("视图/显示整张图", _ => graphView?.FrameAll(),
                _ => graphView?.Asset != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction("显示/关系流向动效", _ => ToggleEdgeFlow(),
                _ => graphView == null || !graphView.SupportsEdgeFlow
                    ? DropdownMenuAction.Status.Disabled
                    : graphView.EdgeFlowEnabled
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("检查图", _ => ValidateCurrentAsset());
            menu.menu.AppendAction("生成检查快照", _ => BakeCurrentAsset());
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("使用引导", _ => OpenOnboarding());
            return menu;
        }

        private void ToggleEdgeFlow()
        {
            if (graphView == null || !graphView.SupportsEdgeFlow)
            {
                UpdateStatus("当前 Unity GraphView 版本无法提供可靠连线几何，关系动效已停用");
                return;
            }

            bool enabled = !graphView.EdgeFlowEnabled;
            graphView.SetEdgeFlowEnabled(enabled);
            EditorPrefs.SetBool(EdgeFlowPreferenceKey, enabled);
            UpdateStatus(enabled
                ? "关系流向动效已开启；大图会自动降低动画预算"
                : "关系流向动效已关闭；连线本体和端口方向仍正常显示");
        }

        private ToolbarMenu CreateOrganizeMenu()
        {
            ToolbarMenu menu = new ToolbarMenu { text = "整理" };
            menu.tooltip = "自动布局、对齐、等距分布或吸附网格；批量操作只产生一次撤销记录。";
            AppendOrganizeActions(menu.menu, string.Empty);
            return menu;
        }

        private void AppendOrganizeActions(DropdownMenu menu, string prefix)
        {
            menu.AppendAction(prefix + "自动布局整张图", _ => graphView?.AutoLayout(),
                _ => graphView?.Asset != null && graphView.Asset.Nodes.Count > 0
                    ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(prefix + "自动布局选中节点", _ => graphView?.AutoLayoutSelection(),
                _ => HasSelectedNodes(2) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(prefix + "左对齐", _ => graphView?.AlignSelection(ESGraphNodeAlignment.Left),
                _ => SelectionStatus(2));
            menu.AppendAction(prefix + "水平居中对齐", _ => graphView?.AlignSelection(ESGraphNodeAlignment.HorizontalCenter),
                _ => SelectionStatus(2));
            menu.AppendAction(prefix + "右对齐", _ => graphView?.AlignSelection(ESGraphNodeAlignment.Right),
                _ => SelectionStatus(2));
            menu.AppendAction(prefix + "顶部对齐", _ => graphView?.AlignSelection(ESGraphNodeAlignment.Top),
                _ => SelectionStatus(2));
            menu.AppendAction(prefix + "垂直居中对齐", _ => graphView?.AlignSelection(ESGraphNodeAlignment.VerticalCenter),
                _ => SelectionStatus(2));
            menu.AppendAction(prefix + "底部对齐", _ => graphView?.AlignSelection(ESGraphNodeAlignment.Bottom),
                _ => SelectionStatus(2));
            menu.AppendAction(prefix + "水平等距分布", _ => graphView?.DistributeSelection(ESGraphNodeDistribution.Horizontal),
                _ => SelectionStatus(3));
            menu.AppendAction(prefix + "垂直等距分布", _ => graphView?.DistributeSelection(ESGraphNodeDistribution.Vertical),
                _ => SelectionStatus(3));
            menu.AppendAction(prefix + "选中节点吸附网格", _ => graphView?.SnapSelectionToGrid(),
                _ => SelectionStatus(1));
        }

        private DropdownMenuAction.Status SelectionStatus(int minimumCount)
        {
            return HasSelectedNodes(minimumCount)
                ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
        }

        private bool HasSelectedNodes(int minimumCount)
        {
            return graphView != null && graphView.SelectedNodeCount >= minimumCount;
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyToolbarLayout(evt.newRect.width);
        }

        private void ApplyToolbarLayout(float width)
        {
            ToolbarLayoutMode next = width >= 1650f ? ToolbarLayoutMode.Wide
                : width >= 1100f ? ToolbarLayoutMode.Standard : ToolbarLayoutMode.Compact;
            if (next == toolbarLayoutMode || primaryToolbar == null || secondaryToolbar == null)
                return;
            toolbarLayoutMode = next;
            primaryToolbar.Clear();
            secondaryToolbar.Clear();

            assetField.style.minWidth = next == ToolbarLayoutMode.Wide ? 300f
                : next == ToolbarLayoutMode.Standard ? 230f : 160f;
            if (next == ToolbarLayoutMode.Wide)
                searchField.style.maxWidth = 260f;
            else
                searchField.style.maxWidth = StyleKeyword.None;

            primaryToolbar.Add(assetField);
            primaryToolbar.Add(createButton);
            primaryToolbar.Add(openButton);
            primaryToolbar.Add(addNodeButton);
            if (next != ToolbarLayoutMode.Compact)
            {
                primaryToolbar.Add(duplicateButton);
                primaryToolbar.Add(organizeMenu);
            }
            if (next == ToolbarLayoutMode.Wide)
                primaryToolbar.Add(frameAllButton);
            if (next != ToolbarLayoutMode.Compact)
                primaryToolbar.Add(validateButton);
            if (next == ToolbarLayoutMode.Wide)
                primaryToolbar.Add(bakeButton);
            primaryToolbar.Add(saveButton);
            if (next == ToolbarLayoutMode.Wide)
                primaryToolbar.Add(saveAsButton);
            if (next == ToolbarLayoutMode.Wide)
                primaryToolbar.Add(guideButton);
            else
                primaryToolbar.Add(moreMenu);

            if (next == ToolbarLayoutMode.Wide)
            {
                primaryToolbar.Add(searchLabel);
                primaryToolbar.Add(searchField);
                primaryToolbar.Add(statusLabel);
                secondaryToolbar.style.display = DisplayStyle.None;
            }
            else
            {
                secondaryToolbar.style.display = DisplayStyle.Flex;
                secondaryToolbar.Add(searchLabel);
                secondaryToolbar.Add(searchField);
                secondaryToolbar.Add(statusLabel);
            }
        }

        private void OnEnable()
        {
            ESEditorPresentation.BindWindow(this);
            ApplyWindowPresentation();
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            Selection.selectionChanged -= OnGlobalSelectionChanged;
            Selection.selectionChanged += OnGlobalSelectionChanged;
            nextAssetRevisionPollTime = 0d;
        }

        private void OnDisable()
        {
            ESEditorPresentation.UnbindWindow(this);
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.projectChanged -= OnProjectChanged;
            Selection.selectionChanged -= OnGlobalSelectionChanged;
            FlushAutoSave();
            graphView?.Dispose();
        }

        private void OnUndoRedo()
        {
            graphView?.Rebuild();
            inspector?.NotifyAssetChanged();
            RequestAutoSave(graphView?.Asset);
            UpdateStatus("已应用撤销 / 重做");
            ESEditorPresentation.PulseWindow(this, ESStatusKind.Modified);
            Repaint();
        }

        private void OnGlobalSelectionChanged()
        {
            if (Selection.activeObject is ESGraphAsset selected && selected != graphView?.Asset)
                SetAsset(selected);
        }

        private void SetAsset(ESGraphAsset asset)
        {
            if (graphView != null && !ReferenceEquals(graphView.Asset, asset))
                FlushAutoSave();
            bool identityChanged = asset != null && asset.EnsureGraphIdentity();
            currentGraphAssetGuid = GetAssetGuid(asset);
            if (assetField != null)
                assetField.SetValueWithoutNotify(asset);
            graphView?.SetAsset(asset);
            inspector?.SetAsset(asset);
            CaptureAssetRevision(asset, true);
            if (identityChanged)
            {
                EditorUtility.SetDirty(asset);
                RequestAutoSave(asset);
            }
            UpdateStatus(asset == null ? "请选择图资产，或点击“新建图”开始" : BuildAssetSummary(asset));
        }

        private bool TryRestoreCurrentAsset()
        {
            if (string.IsNullOrWhiteSpace(currentGraphAssetGuid))
                return false;
            string path = AssetDatabase.GUIDToAssetPath(currentGraphAssetGuid);
            if (string.IsNullOrWhiteSpace(path))
            {
                currentGraphAssetGuid = string.Empty;
                return false;
            }
            ESGraphAsset asset = AssetDatabase.LoadAssetAtPath<ESGraphAsset>(path);
            if (asset == null)
            {
                currentGraphAssetGuid = string.Empty;
                return false;
            }
            SetAsset(asset);
            return true;
        }

        private static string GetAssetGuid(ESGraphAsset asset)
        {
            if (asset == null)
                return string.Empty;
            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static string GetProjectAssetFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string GetAssetRevisionToken(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return string.Empty;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            Hash128 dependencyHash = AssetDatabase.GetAssetDependencyHash(assetPath);
            return guid + "|" + dependencyHash;
        }

        internal void OpenGraph(ESGraphAsset asset)
        {
            SetAsset(asset);
            FocusAssetAfterOpen();
            Focus();
        }

        private void FocusAssetAfterOpen()
        {
            if (graphView == null || graphView.Asset == null)
                return;
            graphView.schedule.Execute(() => graphView.FrameAll()).StartingIn(80);
        }

        private void ShowCreationTemplateMenu(VisualElement anchor)
        {
            var entries = new List<ESSearchDropdown.Entry>
            {
                ESSearchDropdown.Entry.Item(
                    "通用流程图",
                    () => CreateTemplate(ESStableGraphCreationTemplateKind.GenericFlow),
                    "业务图/流程",
                    subtitle: "起点 → 流程 → 终点，适合快速搭建可读实现链。",
                    badge: "基础"),
                ESSearchDropdown.Entry.Item(
                    "剧情 / 任务与对话",
                    () => CreateTemplate(ESStableGraphCreationTemplateKind.Story),
                    "业务图/剧情",
                    subtitle: "开始、对话、选择、行为、完成与失败节点。"),
                ESSearchDropdown.Entry.Item(
                    "行为树调度",
                    () => CreateTemplate(ESStableGraphCreationTemplateKind.BehaviorTree),
                    "行为图",
                    subtitle: "根节点、组合、装饰、条件与行为节点。"),
                ESSearchDropdown.Entry.Item(
                    "AICommand 实现链",
                    () => CreateTemplate(ESStableGraphCreationTemplateKind.AgentAICommand),
                    "AI 协作图/命令",
                    subtitle: "以中文文本描述需求、权限、执行步骤和验收，交给 AI 生成可审查命令。",
                    badge: "推荐"),
                ESSearchDropdown.Entry.Item(
                    "Agent Skill 能力链",
                    () => CreateTemplate(ESStableGraphCreationTemplateKind.AgentSkill),
                    "AI 协作图/技能",
                    subtitle: "编排触发边界、工作流、非目标和验证步骤。"),
                ESSearchDropdown.Entry.Item(
                    "AICommand + Agent Skill",
                    () => CreateTemplate(ESStableGraphCreationTemplateKind.AgentPaired),
                    "AI 协作图/配套产物",
                    subtitle: "一次需求同时产出命令合同与可复用技能。"),
                ESSearchDropdown.Entry.Item(
                    "完整需求思路图",
                    () => CreateTemplate(ESStableGraphCreationTemplateKind.AgentMindMap),
                    "AI 协作图/完整思路",
                    subtitle: "目标、权威资料、四类约束、双产物与人工批准。"),
                ESSearchDropdown.Entry.Item(
                    "空白稳定图（高级）",
                    () => CreateTemplate(ESStableGraphCreationTemplateKind.Blank),
                    "高级",
                    subtitle: "创建通用空图，需手动选择领域并添加节点。")
            };
            ESSearchDropdown.Open(anchor, this, "选择新建图模板", entries,
                minimumWindowSize: new Vector2(560f, 420f));
        }

        private void OpenExistingAssetMenu(VisualElement anchor)
        {
            string[] guids = AssetDatabase.FindAssets("t:ESGraphAsset");
            var entries = new List<ESSearchDropdown.Entry>();
            var paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path);
            }
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                ESGraphAsset graph = AssetDatabase.LoadAssetAtPath<ESGraphAsset>(path);
                if (graph == null)
                    continue;
                string domain = ESGraphChinesePresentation.GetDomainKindName(graph.DomainKind);
                if (graph.DomainKind == ESGraphDomainKind.Custom &&
                    ESGraphAuthoringRegistry.TryGetProfile(graph.DomainKey, out IESGraphAuthoringProfile profile))
                    domain = profile.DisplayName;
                string label = string.IsNullOrEmpty(graph.name) ? System.IO.Path.GetFileNameWithoutExtension(path) : graph.name;
                entries.Add(ESSearchDropdown.Entry.Item(
                    label,
                    () => OpenGraph(graph),
                    domain,
                    subtitle: path,
                    badge: graph.Nodes.Count + " 节点"));
            }

            if (entries.Count == 0)
                entries.Add(ESSearchDropdown.Entry.Disabled("项目中尚无 ESGraphAsset 图资产"));
            ESSearchDropdown.Open(anchor, this, "打开已有图资产", entries,
                minimumWindowSize: new Vector2(620f, 420f));
        }

        private void CreateTemplate(ESStableGraphCreationTemplateKind kind)
        {
            switch (kind)
            {
                case ESStableGraphCreationTemplateKind.AgentAICommand:
                    CreateAgentAuthoringPreset(ESAgentAuthoringPresetKind.AICommandOnly);
                    return;
                case ESStableGraphCreationTemplateKind.AgentSkill:
                    CreateAgentAuthoringPreset(ESAgentAuthoringPresetKind.AgentSkillOnly);
                    return;
                case ESStableGraphCreationTemplateKind.AgentPaired:
                    CreateAgentAuthoringPreset(ESAgentAuthoringPresetKind.Paired);
                    return;
                case ESStableGraphCreationTemplateKind.AgentMindMap:
                    CreateAgentAuthoringPreset(ESAgentAuthoringPresetKind.MindMapPaired);
                    return;
                case ESStableGraphCreationTemplateKind.Blank:
                    CreateBlankGraph();
                    return;
                default:
                    CreateDomainTemplate(kind);
                    return;
            }
        }

        private void CreateBlankGraph()
        {
            CreateDomainTemplate(ESStableGraphCreationTemplateKind.Blank);
        }

        private void CreateDomainTemplate(ESStableGraphCreationTemplateKind kind)
        {
            ESGraphDomainKind domainKind = kind == ESStableGraphCreationTemplateKind.Story
                ? ESGraphDomainKind.Story
                : kind == ESStableGraphCreationTemplateKind.BehaviorTree
                    ? ESGraphDomainKind.BehaviorTree
                    : ESGraphDomainKind.Generic;
            string folder = GetTemplateFolder(kind);
            string defaultName = GetTemplateAssetName(kind);
            ESAgentAuthoringGraphPreset.EnsureAssetFolder(folder);
            string path = EditorUtility.SaveFilePanelInProject("创建" + defaultName, defaultName, "asset",
                GetTemplateDescription(kind), folder);
            if (string.IsNullOrEmpty(path))
                return;

            ESGraphAsset asset = CreateInstance<ESGraphAsset>();
            try
            {
                if (!asset.TrySetDomain(ESGraphDomainKey.FromKind(domainKind), out string domainError))
                    throw new InvalidOperationException(domainError);
                PopulateDomainTemplate(asset, kind);
                AssetDatabase.CreateAsset(asset, path);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                Selection.activeObject = asset;
                SetAsset(asset);
                FocusAssetAfterOpen();
            }
            catch (Exception exception)
            {
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
                    DestroyImmediate(asset);
                UpdateStatus("创建图模板失败：" + exception.Message);
            }
        }

        private static void PopulateDomainTemplate(ESGraphAsset asset, ESStableGraphCreationTemplateKind kind)
        {
            if (kind == ESStableGraphCreationTemplateKind.Blank)
                return;

            if (kind == ESStableGraphCreationTemplateKind.GenericFlow)
            {
                ESGraphNodeRecord source = AddTemplateNode(asset, ESGraphBuiltInNodeKind.GenericSource, new Vector2(0f, 0f));
                ESGraphNodeRecord flow = AddTemplateNode(asset, ESGraphBuiltInNodeKind.GenericFlow, new Vector2(320f, 0f));
                ESGraphNodeRecord sink = AddTemplateNode(asset, ESGraphBuiltInNodeKind.GenericSink, new Vector2(640f, 0f));
                ConnectTemplateNodes(asset, source, flow);
                ConnectTemplateNodes(asset, flow, sink);
                return;
            }

            if (kind == ESStableGraphCreationTemplateKind.Story)
            {
                ESGraphNodeRecord start = AddTemplateNode(asset, ESGraphBuiltInNodeKind.StoryStart, new Vector2(0f, 0f));
                ESGraphNodeRecord dialogue = AddTemplateNode(asset, ESGraphBuiltInNodeKind.StoryDialogue, new Vector2(320f, 0f));
                ESGraphNodeRecord choice = AddTemplateNode(asset, ESGraphBuiltInNodeKind.StoryChoice, new Vector2(640f, 0f));
                ESGraphNodeRecord action = AddTemplateNode(asset, ESGraphBuiltInNodeKind.StoryAction, new Vector2(960f, -100f));
                ESGraphNodeRecord complete = AddTemplateNode(asset, ESGraphBuiltInNodeKind.StoryComplete, new Vector2(1280f, -100f));
                ESGraphNodeRecord fail = AddTemplateNode(asset, ESGraphBuiltInNodeKind.StoryFail, new Vector2(960f, 120f));
                ConnectTemplateNodes(asset, start, dialogue);
                ConnectTemplateNodes(asset, dialogue, choice);
                ConnectTemplateNodes(asset, choice, action);
                ConnectTemplateNodes(asset, choice, fail);
                ConnectTemplateNodes(asset, action, complete);
                return;
            }

            ESGraphNodeRecord root = AddTemplateNode(asset, ESGraphBuiltInNodeKind.BehaviorRoot, new Vector2(0f, 0f));
            ESGraphNodeRecord sequence = AddTemplateNode(asset, ESGraphBuiltInNodeKind.BehaviorSequence, new Vector2(320f, 0f));
            ESGraphNodeRecord selector = AddTemplateNode(asset, ESGraphBuiltInNodeKind.BehaviorSelector, new Vector2(640f, 0f));
            ESGraphNodeRecord condition = AddTemplateNode(asset, ESGraphBuiltInNodeKind.BehaviorCondition, new Vector2(960f, -100f));
            ESGraphNodeRecord behaviorAction = AddTemplateNode(asset, ESGraphBuiltInNodeKind.BehaviorAction, new Vector2(960f, 120f));
            ConnectTemplateNodes(asset, root, sequence);
            ConnectTemplateNodes(asset, sequence, selector);
            ConnectTemplateNodes(asset, selector, condition);
            ConnectTemplateNodes(asset, selector, behaviorAction);
        }

        private static ESGraphNodeRecord AddTemplateNode(ESGraphAsset asset, ESGraphBuiltInNodeKind kind, Vector2 position)
        {
            ESGraphNodeTypeKey type = ESGraphNodeTypeKey.FromKind(kind);
            if (!ESGraphAuthoringRegistry.TryGetNodeDefinition(asset.DomainKey, type, out IESGraphNodeDefinition definition))
                throw new InvalidOperationException("未注册模板节点：" + type.StableId);
            ESGraphNodeRecord node = asset.AddNode(type, definition.DisplayName, position, definition.Ports);
            asset.UpdateNode(node.nodeId, type, definition.CurrentVersion, node.title,
                definition.CreateDefaultPayload(), out _);
            return node;
        }

        private static void ConnectTemplateNodes(ESGraphAsset asset, ESGraphNodeRecord from, ESGraphNodeRecord to)
        {
            ESGraphPortRecord output = from.ports.First(port => port.direction == ESGraphPortDirection.Output);
            ESGraphPortRecord input = to.ports.First(port => port.direction == ESGraphPortDirection.Input);
            if (!asset.TryAddEdge(output.portId, input.portId, out _, out string error))
                throw new InvalidOperationException(error);
        }

        private static string GetTemplateFolder(ESStableGraphCreationTemplateKind kind)
        {
            switch (kind)
            {
                case ESStableGraphCreationTemplateKind.Story: return "Assets/ESNormalAssets/Data/Graphs/Story";
                case ESStableGraphCreationTemplateKind.BehaviorTree: return "Assets/ESNormalAssets/Data/Graphs/BehaviorTree";
                case ESStableGraphCreationTemplateKind.GenericFlow: return "Assets/ESNormalAssets/Data/Graphs/Flow";
                default: return DefaultGraphFolder;
            }
        }

        private static string GetTemplateAssetName(ESStableGraphCreationTemplateKind kind)
        {
            switch (kind)
            {
                case ESStableGraphCreationTemplateKind.Story: return "剧情任务图";
                case ESStableGraphCreationTemplateKind.BehaviorTree: return "行为树图";
                case ESStableGraphCreationTemplateKind.GenericFlow: return "通用流程图";
                default: return "ESGraphAsset";
            }
        }

        private static string GetTemplateDescription(ESStableGraphCreationTemplateKind kind)
        {
            switch (kind)
            {
                case ESStableGraphCreationTemplateKind.Story: return "创建剧情、任务与对话图模板。";
                case ESStableGraphCreationTemplateKind.BehaviorTree: return "创建行为树调度模板。";
                case ESStableGraphCreationTemplateKind.GenericFlow: return "创建通用流程实现链模板。";
                default: return "创建空白稳定图资产。";
            }
        }

        private void OpenOnboarding()
        {
            graphView?.SetOnboardingVisible(true);
        }

        internal void CompleteOnboarding()
        {
            EditorPrefs.SetBool(GetOnboardingPreferenceKey(), true);
            graphView?.SetOnboardingVisible(false);
        }

        private static bool HasCompletedOnboarding()
        {
            return EditorPrefs.GetBool(GetOnboardingPreferenceKey(), false);
        }

        private static string GetOnboardingPreferenceKey()
        {
            string projectKey = (Application.dataPath ?? string.Empty).Replace('\\', '/');
            return OnboardingPreferencePrefix + projectKey;
        }

        private void ShowAgentAuthoringPresetMenu(VisualElement anchor)
        {
            var entries = new List<ESSearchDropdown.Entry>
            {
                ESSearchDropdown.Entry.Item(
                    "配套命令 + 技能",
                    () => CreateAgentAuthoringPreset(ESAgentAuthoringPresetKind.Paired),
                    "常用模板",
                    subtitle: "同时生成 AICommand 与 Agent Skill 候选",
                    badge: "推荐"),
                ESSearchDropdown.Entry.Item(
                    "完整需求思路图",
                    () => CreateAgentAuthoringPreset(ESAgentAuthoringPresetKind.MindMapPaired),
                    "常用模板",
                    subtitle: "从目标、规则、验证到两类产物的完整编排"),
                ESSearchDropdown.Entry.Item(
                    "AICommand 实现链",
                    () => CreateAgentAuthoringPreset(ESAgentAuthoringPresetKind.AICommandOnly),
                    "专项模板",
                    subtitle: "只生成 AICommand 命令候选"),
                ESSearchDropdown.Entry.Item(
                    "Agent Skill 能力链",
                    () => CreateAgentAuthoringPreset(ESAgentAuthoringPresetKind.AgentSkillOnly),
                    "专项模板",
                    subtitle: "只生成 Agent Skill 技能候选")
            };
            ESSearchDropdown.Open(anchor, this, "选择智能助手预设", entries,
                minimumWindowSize: new Vector2(460f, 300f));
        }

        private void CreateAgentAuthoringPreset(ESAgentAuthoringPresetKind kind)
        {
            if (ESAgentAuthoringGraphPreset.TryCreateAsset(kind, out ESGraphAsset asset, out string error)) SetAsset(asset);
            else if (!string.IsNullOrEmpty(error)) UpdateStatus(error);
        }

        private void ValidateCurrentAsset()
        {
            ESGraphAsset asset = graphView?.Asset;
            if (asset == null)
                return;
            List<ESGraphValidationIssue> issues = ESGraphAuthoringRegistry.Validate(asset);
            inspector?.ShowIssues(issues);
            int errors = issues.Count(issue => issue != null && issue.severity == ESGraphValidationSeverity.Error);
            if (errors == 0)
            {
                UpdateStatus("检查通过");
                ESEditorPresentation.PulseWindow(this, ESStatusKind.Ready);
                EditorUtility.DisplayDialog("稳定图检查", "检查通过，可以继续生成检查快照。", "确定");
                return;
            }

            string details = string.Join("\n", issues.Take(12).Select(issue =>
                "• " + issue.message + (string.IsNullOrEmpty(issue.elementId) ? string.Empty : "（请检查对应节点或连线）")));
            if (issues.Count > 12)
                details += "\n……其余 " + (issues.Count - 12) + " 项请通过模型校验接口查看。";
            UpdateStatus("检查未通过：" + errors + " 个问题");
            ESEditorPresentation.PulseWindow(this, ESStatusKind.Warning);
            EditorUtility.DisplayDialog("稳定图需要调整", details, "确定");
        }

        private void BakeCurrentAsset()
        {
            ESGraphAsset asset = graphView?.Asset;
            if (asset == null)
                return;
            if (!ESGraphAuthoringRegistry.TryBake(asset, out ESBakedGraphSnapshot snapshot,
                    out IESBakedGraphPlan domainPlan, out List<ESGraphValidationIssue> issues))
            {
                inspector?.ShowIssues(issues);
                UpdateStatus("生成检查快照失败：请先处理图中问题");
                return;
            }
            inspector?.ShowIssues(issues);
            string result = domainPlan == null ? "通用检查结果" : "领域检查结果";
            UpdateStatus("检查快照已生成 / " + result + " / 编号："
                         + snapshot.ContentSignature.Substring(0, 12));
        }

        private void SaveCurrentAsset()
        {
            ESGraphAsset asset = graphView?.Asset;
            if (asset == null)
                return;
            if (autoSaveAsset == asset)
            {
                autoSavePending = false;
                autoSaveAsset = null;
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            CaptureAssetRevision(asset, true);
            UpdateStatus("已保存：" + AssetDatabase.GetAssetPath(asset));
            ESEditorPresentation.PulseWindow(this, ESStatusKind.Modified);
        }

        private void SaveCurrentAssetAs()
        {
            ESGraphAsset source = graphView?.Asset;
            if (source == null)
                return;
            SaveCurrentAsset();
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                UpdateStatus("另存为失败：当前图还不是项目资产。");
                return;
            }

            string sourceDirectory = (Path.GetDirectoryName(sourcePath) ?? DefaultGraphFolder).Replace('\\', '/');
            string defaultName = Path.GetFileNameWithoutExtension(sourcePath) + "_副本";
            string targetPath = EditorUtility.SaveFilePanelInProject("图资产另存为", defaultName, "asset",
                "创建独立 GraphId 的图资产副本；节点、端口和连线身份会保留，便于比较与更新。", sourceDirectory);
            if (string.IsNullOrWhiteSpace(targetPath))
                return;
            if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                UpdateStatus("另存为失败：目标路径不能与当前图相同。");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath) != null
                || File.Exists(GetProjectAssetFullPath(targetPath)))
            {
                UpdateStatus("另存为失败：目标路径已有资产，已拒绝覆盖。请先选择新路径。");
                return;
            }

            bool created = false;
            string createdTargetToken = null;
            try
            {
                if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                    throw new InvalidOperationException("Unity 未能复制图资产。");
                created = true;
                ESGraphAsset copy = AssetDatabase.LoadAssetAtPath<ESGraphAsset>(targetPath);
                if (copy == null)
                    throw new InvalidOperationException("复制完成后无法重新加载图资产。");
                if (string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(targetPath)))
                    throw new InvalidOperationException("复制完成后无法确认目标 GUID。");
                createdTargetToken = GetAssetRevisionToken(targetPath);
                copy.InitializeAsIndependentCopyOf(source.GraphId);
                if (string.IsNullOrWhiteSpace(copy.GraphId)
                    || string.Equals(copy.GraphId, source.GraphId, StringComparison.Ordinal))
                    throw new InvalidOperationException("目标图未获得独立 GraphId，已拒绝继续保存。");
                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssetIfDirty(copy);
                createdTargetToken = GetAssetRevisionToken(targetPath);
                Undo.RegisterCreatedObjectUndo(copy, "图资产另存为");
                Selection.activeObject = copy;
                SetAsset(copy);
                FocusAssetAfterOpen();
                UpdateStatus("已另存为独立图：" + targetPath);
            }
            catch (Exception exception)
            {
                bool rollbackConfirmed = false;
                if (created && !string.IsNullOrWhiteSpace(createdTargetToken))
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    rollbackConfirmed = string.Equals(
                        GetAssetRevisionToken(targetPath), createdTargetToken, StringComparison.Ordinal)
                        && AssetDatabase.DeleteAsset(targetPath);
                }

                if (rollbackConfirmed)
                    UpdateStatus("另存为失败并已确认回滚：" + exception.Message);
                else
                    UpdateStatus("另存为失败，回滚未确认，已保留目标现场供人工核对：" + exception.Message);
            }
        }

        internal void RequestAutoSave(ESGraphAsset asset)
        {
            if (asset == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
                return;
            autoSaveAsset = asset;
            autoSaveDueTime = EditorApplication.timeSinceStartup + AutoSaveDelaySeconds;
            autoSavePending = true;
            CaptureAssetRevision(asset, false);
        }

        internal void NotifyGraphModelChanged()
        {
            inspector?.NotifyAssetChanged();
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            bool forceDependencyCheck = projectChangeCheckPending && now >= projectChangeCheckDueTime;
            if (forceDependencyCheck)
                projectChangeCheckPending = false;
            if (forceDependencyCheck || now >= nextAssetRevisionPollTime)
            {
                nextAssetRevisionPollTime = now + AssetRevisionPollSeconds;
                SynchronizeCurrentAssetIfChanged(forceDependencyCheck);
            }
            if (autoSavePending && now >= autoSaveDueTime)
                FlushAutoSave();
        }

        private void OnProjectChanged()
        {
            if (graphView?.Asset == null)
                return;
            projectChangeCheckPending = true;
            projectChangeCheckDueTime = EditorApplication.timeSinceStartup + ProjectChangeDebounceSeconds;
        }

        private void SynchronizeCurrentAssetIfChanged(bool includeDependencyHash)
        {
            ESGraphAsset asset = graphView?.Asset;
            if (asset == null)
            {
                if (graphView != null && !ReferenceEquals(graphView.Asset, null))
                    SetAsset(null);
                CaptureAssetRevision(null, false);
                return;
            }
            if (observedAsset != asset)
            {
                CaptureAssetRevision(asset, true);
                return;
            }

            int dirtyCount = EditorUtility.GetDirtyCount(asset);
            Hash128 dependencyHash = includeDependencyHash
                ? GetAssetDependencyHash(asset) : observedAssetDependencyHash;
            bool dirtyChanged = dirtyCount != observedAssetDirtyCount;
            bool dependencyChanged = includeDependencyHash && dependencyHash != observedAssetDependencyHash;
            if (!dirtyChanged && !dependencyChanged)
                return;

            observedAssetDirtyCount = dirtyCount;
            if (includeDependencyHash)
                observedAssetDependencyHash = dependencyHash;
            graphView.Rebuild();
            inspector?.NotifyAssetChanged();
            if (dirtyChanged && EditorUtility.IsDirty(asset))
                RequestAutoSave(asset);
            UpdateStatus("已同步图资产外部修改 · " + BuildAssetSummary(asset));
        }

        private void FlushAutoSave()
        {
            if (!autoSavePending)
                return;
            ESGraphAsset asset = autoSaveAsset;
            autoSavePending = false;
            autoSaveAsset = null;
            if (asset == null)
                return;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
                return;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            CaptureAssetRevision(asset, true);
            if (asset == graphView?.Asset)
                UpdateStatus("已自动保存：" + AssetDatabase.GetAssetPath(asset));
        }

        private void CaptureAssetRevision(ESGraphAsset asset, bool includeDependencyHash)
        {
            bool assetChanged = !ReferenceEquals(observedAsset, asset);
            observedAsset = asset;
            observedAssetDirtyCount = asset == null ? int.MinValue : EditorUtility.GetDirtyCount(asset);
            if (asset == null)
                observedAssetDependencyHash = default;
            else if (includeDependencyHash || assetChanged)
                observedAssetDependencyHash = GetAssetDependencyHash(asset);
        }

        private static Hash128 GetAssetDependencyHash(ESGraphAsset asset)
        {
            string path = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? default : AssetDatabase.GetAssetDependencyHash(path);
        }

        private void UpdateStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message ?? string.Empty;
                statusLabel.tooltip = message ?? string.Empty;
            }
        }

        private static string BuildAssetSummary(ESGraphAsset asset)
        {
            if (asset == null)
                return string.Empty;
            string domain = asset.DomainId;
            if (ESGraphAuthoringRegistry.TryGetProfile(asset.DomainKey, out IESGraphAuthoringProfile profile))
                domain = profile.DisplayName;
            string identity = asset.GraphId.Length >= 8 ? asset.GraphId.Substring(0, 8) : asset.GraphId;
            string summary = domain + " · " + asset.Nodes.Count + " 个节点 · " + asset.Edges.Count + " 条连线 · 图 " + identity;
            if (asset.DomainKind == ESGraphDomainKind.AgentAuthoring
                && ESAgentAuthoringGraphValidator.TryGetFinalPurpose(asset, out string purpose, out _))
                summary += " · 目的：" + CompactStatusText(purpose, 42);
            return summary;
        }

        private static string CompactStatusText(string value, int maximumLength)
        {
            string compact = string.Join(" ", (value ?? string.Empty)
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            return compact.Length <= maximumLength ? compact : compact.Substring(0, maximumLength - 1) + "…";
        }

        private static ToolbarButton CreateToolbarButton(string text, string tooltip, Action action)
        {
            return new ToolbarButton(action) { text = text, tooltip = tooltip };
        }
    }

    internal sealed class ESStableGraphView : GraphView, IDisposable
    {
        private static readonly FieldInfo EdgeRenderPointsField = typeof(EdgeControl).GetField(
            "m_RenderPoints", BindingFlags.Instance | BindingFlags.NonPublic);
        private const int AnimatedEdgeBudget = 160;
        private const int DirectionMarkerBudget = 480;
        private const int MediumGraphEdgeThreshold = 1500;
        private const int LargeGraphEdgeThreshold = 4000;
        private const int AdjacencyListPoolCapacity = 2048;
        private const long EdgeAnimationIntervalMilliseconds = 50L;
        private const long EdgeReconnectLongPressMilliseconds = 460L;
        private const float EdgeReconnectMovementTolerance = 8f;
        private const float LayoutHorizontalSpacing = 310f;
        private const float LayoutVerticalSpacing = 180f;
        private const float PositionGridSize = 32f;

        private readonly struct EdgeEndpointKey : IEquatable<EdgeEndpointKey>
        {
            private readonly string outputPortId;
            private readonly string inputPortId;

            public EdgeEndpointKey(string outputPortId, string inputPortId)
            {
                this.outputPortId = outputPortId ?? string.Empty;
                this.inputPortId = inputPortId ?? string.Empty;
            }

            public bool Equals(EdgeEndpointKey other)
            {
                return string.Equals(outputPortId, other.outputPortId, StringComparison.Ordinal)
                    && string.Equals(inputPortId, other.inputPortId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) { return obj is EdgeEndpointKey other && Equals(other); }
            public override int GetHashCode()
            {
                unchecked
                {
                    return ((outputPortId != null ? outputPortId.GetHashCode() : 0) * 397)
                        ^ (inputPortId != null ? inputPortId.GetHashCode() : 0);
                }
            }
        }

        private readonly ESStableGraphViewWindow ownerWindow;
        private readonly Action<string> report;
        private readonly ESStableGraphNodeSearchProvider searchProvider;
        private readonly ESStableGraphEdgeConnectorListener edgeConnectorListener;
        private readonly VisualElement emptyState;
        private readonly MiniMap miniMap;
        private readonly VisualElement edgeFlowOverlay;
        private readonly Dictionary<string, ESStableGraphNodeView> nodeViews = new Dictionary<string, ESStableGraphNodeView>(StringComparer.Ordinal);
        private readonly Dictionary<string, Edge> edgeViews = new Dictionary<string, Edge>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> edgeFlowPhases = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<Edge, List<Vector2>> edgeRenderPointViews = new Dictionary<Edge, List<Vector2>>();
        private readonly List<Vector2> edgePointBuffer = new List<Vector2>(32);
        private readonly Dictionary<string, Port> portViews = new Dictionary<string, Port>(StringComparer.Ordinal);
        private readonly Dictionary<string, ESGraphPortRecord> portRecords = new Dictionary<string, ESGraphPortRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> nodeIdsByPort = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> connectionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<EdgeEndpointKey> edgeEndpointKeys = new HashSet<EdgeEndpointKey>();
        private readonly Dictionary<string, List<string>> outgoingNodes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> incomingNodes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> reachableNodeBuffer = new HashSet<string>(StringComparer.Ordinal);
        private readonly Stack<string> traversalStack = new Stack<string>();
        private readonly HashSet<string> rebuildSelectedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> rebuildSelectedEdgeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, ESGraphNodeRecord> rebuildDesiredNodes = new Dictionary<string, ESGraphNodeRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ESGraphEdgeRecord> rebuildDesiredEdges = new Dictionary<string, ESGraphEdgeRecord>(StringComparer.Ordinal);
        private readonly HashSet<string> rebuildChangedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> rebuildStaleEdgeIds = new List<string>();
        private readonly List<GraphElement> graphElementRemovalBuffer = new List<GraphElement>();
        private readonly HashSet<string> activeNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> staleAdjacencyNodeIds = new List<string>();
        private readonly Stack<List<string>> adjacencyListPool = new Stack<List<string>>();
        private IVisualElementScheduledItem edgeAnimationSchedule;
        private int edgeAnimationTick;
        private int lastSelectionHash;
        private int lastSelectionCount = -1;
        private bool rebuilding;
        private bool moveUndoRecorded;
        private bool onboardingVisible;
        private bool edgeFlowGeometryAvailable;
        private bool edgeFlowEnabled;
        private Edge pendingEdgeReconnect;
        private Vector2 pendingEdgeReconnectStart;
        private IVisualElementScheduledItem edgeReconnectSchedule;
        private bool edgeReconnectTriggered;

        public ESGraphAsset Asset { get; private set; }
        public int SelectedNodeCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < selection.Count; i++)
                    if (selection[i] is ESStableGraphNodeView)
                        count++;
                return count;
            }
        }
        public bool HasSelection => selection.Count > 0;
        public bool EdgeFlowEnabled => edgeFlowEnabled && edgeFlowGeometryAvailable;
        public bool SupportsEdgeFlow => edgeFlowGeometryAvailable;
        public event Action<IEnumerable<ISelectable>> SelectionChanged;

        public ESStableGraphView(ESStableGraphViewWindow ownerWindow, Action<string> report)
        {
            this.ownerWindow = ownerWindow;
            this.report = report;
            // EdgeControl 的内部渲染点属于 Unity 私有实现，不能作为动画可用性的唯一门禁。
            // 如果当前 Unity 版本没有该字段，后续会退回到端口中心的稳定贝塞尔路径。
            edgeFlowGeometryAvailable = true;
            searchProvider = ScriptableObject.CreateInstance<ESStableGraphNodeSearchProvider>();
            searchProvider.hideFlags = HideFlags.HideAndDontSave;
            searchProvider.Initialize(this);
            edgeConnectorListener = new ESStableGraphEdgeConnectorListener(this);
            style.flexGrow = 1f;
            focusable = true;
            tabIndex = 0;
            Insert(0, new GridBackground());
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            edgeFlowOverlay = new VisualElement
            {
                name = "es-stable-graph-edge-flow-overlay",
                pickingMode = PickingMode.Ignore
            };
            edgeFlowOverlay.style.position = Position.Absolute;
            edgeFlowOverlay.style.left = 0f;
            edgeFlowOverlay.style.top = 0f;
            edgeFlowOverlay.style.right = 0f;
            edgeFlowOverlay.style.bottom = 0f;
            edgeFlowOverlay.style.overflow = Overflow.Hidden;
            edgeFlowOverlay.generateVisualContent += OnGenerateEdgeFlowVisualContent;
            Add(edgeFlowOverlay);
            miniMap = new MiniMap { anchored = true };
            miniMap.SetPosition(new Rect(12f, 12f, 210f, 145f));
            Add(miniMap);
            emptyState = new VisualElement();
            emptyState.style.position = Position.Absolute;
            emptyState.style.left = 36f;
            emptyState.style.top = 72f;
            emptyState.style.width = 460f;
            emptyState.style.whiteSpace = WhiteSpace.Normal;
            emptyState.style.paddingLeft = 18f;
            emptyState.style.paddingRight = 18f;
            emptyState.style.paddingTop = 16f;
            emptyState.style.paddingBottom = 16f;
            emptyState.style.color = new Color(0.84f, 0.88f, 0.95f, 1f);
            emptyState.style.backgroundColor = new Color(0.11f, 0.13f, 0.18f, 0.96f);
            emptyState.style.borderTopWidth = 1f;
            emptyState.style.borderBottomWidth = 1f;
            emptyState.style.borderLeftWidth = 1f;
            emptyState.style.borderRightWidth = 1f;
            emptyState.style.borderTopColor = new Color(0.28f, 0.38f, 0.55f, 0.9f);
            emptyState.style.borderBottomColor = new Color(0.28f, 0.38f, 0.55f, 0.9f);
            emptyState.style.borderLeftColor = new Color(0.28f, 0.38f, 0.55f, 0.9f);
            emptyState.style.borderRightColor = new Color(0.28f, 0.38f, 0.55f, 0.9f);
            emptyState.style.borderTopLeftRadius = 8f;
            emptyState.style.borderTopRightRadius = 8f;
            emptyState.style.borderBottomLeftRadius = 8f;
            emptyState.style.borderBottomRightRadius = 8f;
            Label guideTitle = new Label("快速上手");
            guideTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            guideTitle.style.fontSize = 14f;
            guideTitle.style.marginBottom = 8f;
            emptyState.Add(guideTitle);
            Label guideBody = new Label(
                "1. 点击“新建图”选择模板，或点击“打开已有”/从 Project 窗口拖入图资产\n" +
                "2. 点击“添加节点”开始搭建思路\n" +
                "3. 从输出端口拖到输入端口建立关系\n" +
                "4. 在右侧填写标题和业务内容\n\n" +
                "高效操作：拖动空白区域可框选，Shift / Ctrl 可增减选择；\n" +
                "Ctrl/Cmd+A 全选，Ctrl/Cmd+D 复制，F 聚焦，“整理”可对齐和等距。\n\n" +
                "图形编辑会自动保存；正式产物仍需检查和人工批准。\n" +
                "以后可以点击顶部“使用引导”再次查看。");
            guideBody.style.whiteSpace = WhiteSpace.Normal;
            emptyState.Add(guideBody);
            Button closeGuide = new Button(() => ownerWindow?.CompleteOnboarding()) { text = "知道了" };
            closeGuide.tooltip = "关闭首次使用引导；以后可从顶部“使用引导”再次打开。";
            closeGuide.style.marginTop = 10f;
            emptyState.Add(closeGuide);
            Add(emptyState);
            graphViewChanged = OnGraphViewChanged;
            nodeCreationRequest = context => OpenNodeSearch(context.screenMousePosition,
                contentViewContainer.WorldToLocal(context.screenMousePosition - ownerWindow.position.position));
            serializeGraphElements = SerializeSelection;
            unserializeAndPaste = PasteSelection;
            canPasteSerializedData = data => Asset != null && string.Equals(data, "ESStableGraph.Selection", StringComparison.Ordinal);
            RegisterCallback<MouseUpEvent>(_ =>
            {
                moveUndoRecorded = false;
                NotifySelectionChanged();
            });
            RegisterCallback<MouseDownEvent>(OnGraphMouseDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseMoveEvent>(OnGraphMouseMove, TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(OnGraphMouseUp, TrickleDown.TrickleDown);
            RegisterCallback<KeyUpEvent>(_ => NotifySelectionChanged());
            RegisterCallback<KeyDownEvent>(OnGraphKeyDown);
            RegisterCallback<GeometryChangedEvent>(OnGraphGeometryChanged);
            edgeAnimationSchedule = edgeFlowOverlay.schedule.Execute(OnEdgeAnimationTick)
                .Every(EdgeAnimationIntervalMilliseconds);
        }

        public void Dispose()
        {
            graphViewChanged = null;
            serializeGraphElements = null;
            unserializeAndPaste = null;
            canPasteSerializedData = null;
            nodeCreationRequest = null;
            SelectionChanged = null;
            edgeAnimationSchedule?.Pause();
            edgeAnimationSchedule = null;
            CancelEdgeReconnect();
            if (edgeFlowOverlay != null)
                edgeFlowOverlay.generateVisualContent -= OnGenerateEdgeFlowVisualContent;
            UnregisterCallback<MouseDownEvent>(OnGraphMouseDown, TrickleDown.TrickleDown);
            UnregisterCallback<MouseMoveEvent>(OnGraphMouseMove, TrickleDown.TrickleDown);
            UnregisterCallback<MouseUpEvent>(OnGraphMouseUp, TrickleDown.TrickleDown);
            UnregisterCallback<KeyDownEvent>(OnGraphKeyDown);
            UnregisterCallback<GeometryChangedEvent>(OnGraphGeometryChanged);
            if (searchProvider != null)
                UnityEngine.Object.DestroyImmediate(searchProvider);
        }

        public void SetAsset(ESGraphAsset asset)
        {
            if (ReferenceEquals(Asset, asset))
                return;
            ClearProjection();
            Asset = asset;
            Rebuild();
        }

        public void SetOnboardingVisible(bool visible)
        {
            onboardingVisible = visible;
            UpdateOnboardingVisibility();
        }

        public void SetEdgeFlowEnabled(bool enabled)
        {
            edgeFlowEnabled = enabled && edgeFlowGeometryAvailable;
            edgeAnimationTick = 0;
            edgeFlowOverlay?.MarkDirtyRepaint();
        }

        public void Rebuild()
        {
            rebuildSelectedNodeIds.Clear();
            rebuildSelectedEdgeIds.Clear();
            for (int i = 0; i < selection.Count; i++)
            {
                if (selection[i] is ESStableGraphNodeView nodeView)
                    rebuildSelectedNodeIds.Add(nodeView.NodeId);
                else if (selection[i] is Edge edge && edge.userData is string edgeId
                    && !string.IsNullOrEmpty(edgeId))
                    rebuildSelectedEdgeIds.Add(edgeId);
            }
            rebuilding = true;
            try
            {
                ClearSelection();
                UpdateOnboardingVisibility();
                if (Asset == null)
                    return;

                rebuildDesiredNodes.Clear();
                for (int i = 0; i < Asset.Nodes.Count; i++)
                {
                    ESGraphNodeRecord record = Asset.Nodes[i];
                    if (record == null || string.IsNullOrEmpty(record.nodeId))
                        continue;
                    rebuildDesiredNodes[record.nodeId] = record;
                }

                rebuildChangedNodeIds.Clear();
                foreach (KeyValuePair<string, ESStableGraphNodeView> pair in nodeViews)
                    if (!rebuildDesiredNodes.TryGetValue(pair.Key, out ESGraphNodeRecord record)
                        || !pair.Value.MatchesRecord(record))
                        rebuildChangedNodeIds.Add(pair.Key);

                rebuildDesiredEdges.Clear();
                for (int i = 0; i < Asset.Edges.Count; i++)
                {
                    ESGraphEdgeRecord record = Asset.Edges[i];
                    if (record == null || string.IsNullOrEmpty(record.edgeId))
                        continue;
                    rebuildDesiredEdges[record.edgeId] = record;
                }

                rebuildStaleEdgeIds.Clear();
                foreach (KeyValuePair<string, Edge> pair in edgeViews)
                {
                    Edge edge = pair.Value;
                    bool endpointsChanged = !rebuildDesiredEdges.TryGetValue(pair.Key, out ESGraphEdgeRecord desired)
                        || !string.Equals(edge?.output?.userData as string, desired.outputPortId, StringComparison.Ordinal)
                        || !string.Equals(edge?.input?.userData as string, desired.inputPortId, StringComparison.Ordinal);
                    string outputNodeId = (edge?.output?.node as ESStableGraphNodeView)?.NodeId;
                    string inputNodeId = (edge?.input?.node as ESStableGraphNodeView)?.NodeId;
                    if (endpointsChanged || rebuildChangedNodeIds.Contains(outputNodeId ?? string.Empty)
                        || rebuildChangedNodeIds.Contains(inputNodeId ?? string.Empty))
                        rebuildStaleEdgeIds.Add(pair.Key);
                }
                for (int i = 0; i < rebuildStaleEdgeIds.Count; i++)
                {
                    string edgeId = rebuildStaleEdgeIds[i];
                    if (!edgeViews.TryGetValue(edgeId, out Edge edge))
                        continue;
                    edgeRenderPointViews.Remove(edge);
                    RemoveGraphElementSafe(edge);
                    edgeViews.Remove(edgeId);
                    edgeFlowPhases.Remove(edgeId);
                }

                foreach (string nodeId in rebuildChangedNodeIds)
                {
                    if (!nodeViews.TryGetValue(nodeId, out ESStableGraphNodeView oldView))
                        continue;
                    RemoveGraphElementSafe(oldView);
                    nodeViews.Remove(nodeId);
                }

                foreach (KeyValuePair<string, ESGraphNodeRecord> pair in rebuildDesiredNodes)
                {
                    if (!nodeViews.TryGetValue(pair.Key, out ESStableGraphNodeView nodeView))
                    {
                        ESGraphAuthoringRegistry.TryGetNodeDefinition(Asset.DomainKey, pair.Value.TypeKey,
                            out IESGraphNodeDefinition definition);
                        nodeView = new ESStableGraphNodeView(Asset.DomainKey, pair.Value, definition,
                            edgeConnectorListener);
                        nodeViews[pair.Key] = nodeView;
                        AddElement(nodeView);
                    }
                    else
                    {
                        nodeView.SyncPosition(pair.Value.position);
                    }
                }

                BuildGraphIndexes();
                foreach (KeyValuePair<string, ESGraphEdgeRecord> pair in rebuildDesiredEdges)
                {
                    if (edgeViews.ContainsKey(pair.Key)
                        || !portViews.TryGetValue(pair.Value.outputPortId, out Port output)
                        || !portViews.TryGetValue(pair.Value.inputPortId, out Port input))
                        continue;
                    Edge edge = output.ConnectTo(input);
                    edge.userData = pair.Key;
                    edgeViews[pair.Key] = edge;
                    ConfigureEdgeReconnectGesture(edge);
                    RegisterEdgeFlowGeometry(edge);
                    AddElement(edge);
                }
                RefreshPortRelationVisuals();

                foreach (string nodeId in rebuildSelectedNodeIds)
                    if (nodeViews.TryGetValue(nodeId, out ESStableGraphNodeView nodeView))
                        AddToSelection(nodeView);
                foreach (string edgeId in rebuildSelectedEdgeIds)
                    if (edgeViews.TryGetValue(edgeId, out Edge edge))
                        AddToSelection(edge);
            }
            finally
            {
                rebuilding = false;
                rebuildSelectedNodeIds.Clear();
                rebuildSelectedEdgeIds.Clear();
                rebuildDesiredNodes.Clear();
                rebuildDesiredEdges.Clear();
                rebuildChangedNodeIds.Clear();
                rebuildStaleEdgeIds.Clear();
            }
            report?.Invoke(Asset == null ? "请选择图资产，或点击“新建图”开始" :
                Asset.Nodes.Count + " 个节点 / " + Asset.Edges.Count + " 条连线");
            edgeFlowOverlay?.MarkDirtyRepaint();
            NotifySelectionChanged(true);
        }

        private void ClearProjection()
        {
            bool wasRebuilding = rebuilding;
            rebuilding = true;
            try
            {
                graphElementRemovalBuffer.Clear();
                foreach (GraphElement element in graphElements)
                    if (element is ESStableGraphNodeView || element is Edge)
                        graphElementRemovalBuffer.Add(element);
                for (int i = 0; i < graphElementRemovalBuffer.Count; i++)
                    RemoveGraphElementSafe(graphElementRemovalBuffer[i]);
                graphElementRemovalBuffer.Clear();
                ClearSelection();
                nodeViews.Clear();
                edgeViews.Clear();
                edgeFlowPhases.Clear();
                edgeRenderPointViews.Clear();
                edgePointBuffer.Clear();
                portViews.Clear();
                portRecords.Clear();
                nodeIdsByPort.Clear();
                connectionCounts.Clear();
                edgeEndpointKeys.Clear();
                RecycleAdjacencyIndex(outgoingNodes);
                RecycleAdjacencyIndex(incomingNodes);
                activeNodeIds.Clear();
                staleAdjacencyNodeIds.Clear();
            }
            finally
            {
                rebuilding = wasRebuilding;
            }
        }

        private void BuildGraphIndexes()
        {
            portViews.Clear();
            portRecords.Clear();
            nodeIdsByPort.Clear();
            connectionCounts.Clear();
            edgeEndpointKeys.Clear();
            ClearAdjacencyIndex(outgoingNodes);
            ClearAdjacencyIndex(incomingNodes);
            activeNodeIds.Clear();
            if (Asset == null)
                return;

            for (int i = 0; i < Asset.Nodes.Count; i++)
            {
                ESGraphNodeRecord node = Asset.Nodes[i];
                if (node == null || string.IsNullOrEmpty(node.nodeId))
                    continue;
                activeNodeIds.Add(node.nodeId);
                GetOrCreateAdjacencyList(outgoingNodes, node.nodeId);
                GetOrCreateAdjacencyList(incomingNodes, node.nodeId);
                if (!nodeViews.TryGetValue(node.nodeId, out ESStableGraphNodeView nodeView)
                    || node.ports == null)
                    continue;
                for (int p = 0; p < node.ports.Count; p++)
                {
                    ESGraphPortRecord port = node.ports[p];
                    if (port == null || string.IsNullOrEmpty(port.portId)
                        || !nodeView.PortViews.TryGetValue(port.portId, out Port portView))
                        continue;
                    portViews[port.portId] = portView;
                    portRecords[port.portId] = port;
                    nodeIdsByPort[port.portId] = node.nodeId;
                }
            }

            PruneAdjacencyIndex(outgoingNodes);
            PruneAdjacencyIndex(incomingNodes);

            for (int i = 0; i < Asset.Edges.Count; i++)
            {
                ESGraphEdgeRecord edge = Asset.Edges[i];
                if (edge == null)
                    continue;
                IncrementConnectionCount(connectionCounts, edge.outputPortId);
                IncrementConnectionCount(connectionCounts, edge.inputPortId);
                edgeEndpointKeys.Add(new EdgeEndpointKey(edge.outputPortId, edge.inputPortId));
                if (!nodeIdsByPort.TryGetValue(edge.outputPortId ?? string.Empty, out string from)
                    || !nodeIdsByPort.TryGetValue(edge.inputPortId ?? string.Empty, out string to))
                    continue;
                if (!outgoingNodes.TryGetValue(from, out List<string> outgoing))
                    outgoingNodes[from] = outgoing = new List<string>();
                if (!incomingNodes.TryGetValue(to, out List<string> incoming))
                    incomingNodes[to] = incoming = new List<string>();
                outgoing.Add(to);
                incoming.Add(from);
            }
        }

        private static void ClearAdjacencyIndex(Dictionary<string, List<string>> index)
        {
            foreach (KeyValuePair<string, List<string>> pair in index)
                pair.Value?.Clear();
        }

        private List<string> GetOrCreateAdjacencyList(Dictionary<string, List<string>> index, string nodeId)
        {
            if (index.TryGetValue(nodeId, out List<string> list))
                return list;
            list = adjacencyListPool.Count > 0 ? adjacencyListPool.Pop() : new List<string>();
            index[nodeId] = list;
            return list;
        }

        private void PruneAdjacencyIndex(Dictionary<string, List<string>> index)
        {
            staleAdjacencyNodeIds.Clear();
            foreach (KeyValuePair<string, List<string>> pair in index)
                if (!activeNodeIds.Contains(pair.Key))
                    staleAdjacencyNodeIds.Add(pair.Key);
            for (int i = 0; i < staleAdjacencyNodeIds.Count; i++)
            {
                string nodeId = staleAdjacencyNodeIds[i];
                if (!index.TryGetValue(nodeId, out List<string> list))
                    continue;
                index.Remove(nodeId);
                list.Clear();
                ReturnAdjacencyList(list);
            }
            staleAdjacencyNodeIds.Clear();
        }

        private void RecycleAdjacencyIndex(Dictionary<string, List<string>> index)
        {
            foreach (KeyValuePair<string, List<string>> pair in index)
            {
                pair.Value?.Clear();
                if (pair.Value != null)
                    ReturnAdjacencyList(pair.Value);
            }
            index.Clear();
        }

        private void ReturnAdjacencyList(List<string> list)
        {
            if (list != null && adjacencyListPool.Count < AdjacencyListPoolCapacity)
                adjacencyListPool.Push(list);
        }

        private void RefreshPortRelationVisuals()
        {
            foreach (KeyValuePair<string, Port> pair in portViews)
            {
                connectionCounts.TryGetValue(pair.Key, out int count);
                if (pair.Value is ESStableGraphPortView stablePort)
                    stablePort.SetConnectionCount(count);
            }
        }

        private void RemoveGraphElementSafe(GraphElement element)
        {
            if (element != null && element.parent != null)
                RemoveElement(element);
        }

        private void UpdateOnboardingVisibility()
        {
            if (emptyState != null)
                emptyState.style.display = onboardingVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (miniMap != null)
                miniMap.style.display = onboardingVisible ? DisplayStyle.None : DisplayStyle.Flex;
            UpdateOverlayLayout(layout.width, layout.height);
        }

        private void OnGraphGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateOverlayLayout(evt.newRect.width, evt.newRect.height);
            edgeFlowOverlay?.MarkDirtyRepaint();
        }

        private void UpdateOverlayLayout(float width, float height)
        {
            if (width <= 0f || height <= 0f)
                return;
            float mapWidth = Mathf.Min(Mathf.Clamp(width * 0.28f, 120f, 210f), Mathf.Max(80f, width - 24f));
            float mapHeight = mapWidth * 0.69f;
            miniMap?.SetPosition(new Rect(Mathf.Max(12f, width - mapWidth - 12f), 12f, mapWidth, mapHeight));

            if (emptyState == null)
                return;
            float horizontalMargin = width < 620f ? 16f : 36f;
            float cardWidth = Mathf.Min(520f, Mathf.Max(120f, width - horizontalMargin * 2f));
            cardWidth = Mathf.Min(cardWidth, Mathf.Max(80f, width - 16f));
            emptyState.style.width = cardWidth;
            emptyState.style.left = Mathf.Max(horizontalMargin, (width - cardWidth) * 0.5f);
            emptyState.style.top = height < 480f ? 20f : 52f;
        }

        private void OnEdgeAnimationTick()
        {
            if (edgeFlowOverlay == null || edgeViews.Count == 0 || !EdgeFlowEnabled)
                return;
            edgeAnimationTick++;
            int repaintDivisor = edgeViews.Count >= LargeGraphEdgeThreshold ? 4
                : edgeViews.Count >= MediumGraphEdgeThreshold ? 2 : 1;
            if (ownerWindow != null && !ownerWindow.hasFocus)
                repaintDivisor *= 4;
            if (edgeAnimationTick % repaintDivisor != 0)
                return;
            edgeFlowOverlay.MarkDirtyRepaint();
        }

        private void OnGenerateEdgeFlowVisualContent(MeshGenerationContext context)
        {
            if (rebuilding || Asset == null || edgeViews.Count == 0 || edgeFlowOverlay == null
                || !EdgeFlowEnabled)
                return;

            Painter2D painter = context.painter2D;
            double time = EditorApplication.timeSinceStartup;
            int animatedCount = 0;
            int markerCount = 0;
            int animatedBudget = edgeViews.Count >= LargeGraphEdgeThreshold ? 24
                : edgeViews.Count >= MediumGraphEdgeThreshold ? 80 : AnimatedEdgeBudget;
            int markerBudget = edgeViews.Count >= LargeGraphEdgeThreshold ? 240
                : edgeViews.Count >= MediumGraphEdgeThreshold ? 360 : DirectionMarkerBudget;
            bool allowAmbientAnimation = edgeViews.Count < LargeGraphEdgeThreshold;
            DrawEdgeFlowPass(painter, time, true, allowAmbientAnimation, animatedBudget, markerBudget,
                ref animatedCount, ref markerCount);
            if (animatedCount < animatedBudget || markerCount < markerBudget)
                DrawEdgeFlowPass(painter, time, false, allowAmbientAnimation, animatedBudget, markerBudget,
                    ref animatedCount, ref markerCount);
        }

        private void DrawEdgeFlowPass(Painter2D painter, double time, bool priorityOnly,
            bool allowAmbientAnimation, int animatedBudget, int markerBudget,
            ref int animatedCount, ref int markerCount)
        {
            Rect viewport = edgeFlowOverlay.worldBound;
            foreach (KeyValuePair<string, Edge> pair in edgeViews)
            {
                Edge edge = pair.Value;
                EdgeControl edgeControl = edge?.edgeControl;
                bool priority = IsPriorityEdge(edge);
                Rect edgeWorldBound = edgeControl == null ? default : edgeControl.worldBound;
                edgeWorldBound = new Rect(edgeWorldBound.x - 12f, edgeWorldBound.y - 12f,
                    edgeWorldBound.width + 24f, edgeWorldBound.height + 24f);
                if (priority != priorityOnly || edge == null || edge.parent == null
                    || edge.output == null || edge.input == null || edgeControl == null
                    || edgeControl.panel == null || !IsUsableWorldBound(edgeWorldBound)
                    || !edgeWorldBound.Overlaps(viewport))
                    continue;
                bool canDrawMarker = markerCount < markerBudget;
                bool canDrawAnimated = animatedCount < animatedBudget
                    && (priorityOnly || allowAmbientAnimation);
                if (!canDrawMarker && !canDrawAnimated)
                    break;

                if (!TryDrawEdgeFlow(painter, edge, pair.Key, time, canDrawMarker, canDrawAnimated))
                    continue;
                if (canDrawMarker)
                    markerCount++;
                if (canDrawAnimated)
                    animatedCount++;
            }
        }

        private bool TryDrawEdgeFlow(Painter2D painter, Edge edge, string edgeId, double time,
            bool drawMarker, bool drawAnimated)
        {
            if (!TryBuildActualEdgePath(edge, out float pathLength) || pathLength < 4f)
                return false;

            Color accent = edge.output.portColor;
            if (drawMarker && TryEvaluateEdgePath(0.64f, pathLength,
                    out Vector2 markerPosition, out Vector2 markerTangent))
            {
                DrawFlowArrow(painter, markerPosition, markerTangent, 13f, 4.8f,
                    new Color(accent.r, accent.g, accent.b, edge.selected ? 1f : 0.82f));
            }

            if (drawAnimated)
            {
                float phase = GetEdgeFlowPhase(edgeId);
                float baseT = Mathf.Repeat((float)(time * 0.24d) + phase, 1f);
                for (int i = 0; i < 2; i++)
                {
                    float flowT = Mathf.Repeat(baseT - i * 0.13f, 1f);
                    flowT = Mathf.Lerp(0.06f, 0.94f, flowT);
                    if (!TryEvaluateEdgePath(flowT, pathLength,
                            out Vector2 flowPosition, out Vector2 flowTangent))
                        continue;
                    float alpha = i == 0 ? 0.96f : 0.34f;
                    DrawFlowArrow(painter, flowPosition, flowTangent, 21f, 9f,
                        new Color(accent.r, accent.g, accent.b, alpha * 0.16f));
                    DrawFlowArrow(painter, flowPosition, flowTangent, i == 0 ? 16f : 13f,
                        i == 0 ? 5.5f : 4.3f,
                        new Color(Mathf.Lerp(accent.r, 1f, 0.58f),
                            Mathf.Lerp(accent.g, 1f, 0.58f),
                            Mathf.Lerp(accent.b, 1f, 0.58f), alpha));
                }
            }
            return true;
        }

        private void RegisterEdgeFlowGeometry(Edge edge)
        {
            if (!edgeFlowGeometryAvailable || EdgeRenderPointsField == null || edge == null || edge.edgeControl == null
                || edgeRenderPointViews.ContainsKey(edge))
                return;
            try
            {
                if (EdgeRenderPointsField.GetValue(edge.edgeControl) is List<Vector2> renderPoints)
                    edgeRenderPointViews[edge] = renderPoints;
                else
                    edgeRenderPointViews.Remove(edge);
            }
            catch (Exception)
            {
                edgeRenderPointViews.Remove(edge);
            }
        }

        private bool TryBuildActualEdgePath(Edge edge, out float pathLength)
        {
            pathLength = 0f;
            edgePointBuffer.Clear();
            RegisterEdgeFlowGeometry(edge);
            if (!edgeFlowGeometryAvailable || edge == null || edge.output == null || edge.input == null
                || edgeFlowOverlay == null || edgeFlowOverlay.panel == null)
                return false;

            // Unity 2022 的 EdgeControl 通常能提供真实曲线点。优先使用它，保证动画与实际连线重合。
            if (edge.edgeControl != null && edgeRenderPointViews.TryGetValue(edge, out List<Vector2> renderPoints)
                && renderPoints != null && renderPoints.Count >= 2 && EdgeRenderPointsField != null)
            {
                bool valid = true;
                for (int i = 0; i < renderPoints.Count; i++)
                {
                    Vector2 point = edgeFlowOverlay.WorldToLocal(
                        edge.edgeControl.LocalToWorld(renderPoints[i]));
                    if (!IsFinite(point))
                    {
                        valid = false;
                        break;
                    }
                    if (edgePointBuffer.Count > 0)
                    {
                        float segmentLength = Vector2.Distance(edgePointBuffer[edgePointBuffer.Count - 1], point);
                        if (segmentLength < 0.01f)
                            continue;
                        pathLength += segmentLength;
                    }
                    edgePointBuffer.Add(point);
                }
                if (valid && edgePointBuffer.Count >= 2 && pathLength >= 4f)
                    return true;
                edgePointBuffer.Clear();
                pathLength = 0f;
            }

            // 反射字段缺失、首次布局或 EdgeControl 尚未刷新时，使用端口中心兜底。
            // 该路径不依赖 Unity 私有 API，因此不会出现“动画开关打开但完全不可见”。
            Vector2 start = edgeFlowOverlay.WorldToLocal(edge.output.worldBound.center);
            Vector2 end = edgeFlowOverlay.WorldToLocal(edge.input.worldBound.center);
            if (!IsFinite(start) || !IsFinite(end) || (end - start).sqrMagnitude < 0.01f)
                return false;

            float horizontalDistance = Mathf.Abs(end.x - start.x);
            float controlOffset = Mathf.Clamp(horizontalDistance * 0.45f, 42f, 220f);
            float directionSign = end.x >= start.x ? 1f : -1f;
            Vector2 controlA = start + Vector2.right * (controlOffset * directionSign);
            Vector2 controlB = end - Vector2.right * (controlOffset * directionSign);
            const int fallbackSegments = 20;
            edgePointBuffer.Add(start);
            for (int i = 1; i <= fallbackSegments; i++)
            {
                float t = i / (float)fallbackSegments;
                float inverse = 1f - t;
                Vector2 point = inverse * inverse * inverse * start
                    + 3f * inverse * inverse * t * controlA
                    + 3f * inverse * t * t * controlB
                    + t * t * t * end;
                if (!IsFinite(point))
                {
                    edgePointBuffer.Clear();
                    return false;
                }
                float segmentLength = Vector2.Distance(edgePointBuffer[edgePointBuffer.Count - 1], point);
                if (segmentLength >= 0.01f)
                {
                    pathLength += segmentLength;
                    edgePointBuffer.Add(point);
                }
            }
            return edgePointBuffer.Count >= 2 && pathLength >= 4f;
        }

        private bool TryEvaluateEdgePath(float normalizedDistance, float pathLength,
            out Vector2 position, out Vector2 tangent)
        {
            position = default;
            tangent = default;
            if (edgePointBuffer.Count < 2 || pathLength < 0.01f)
                return false;

            float targetDistance = Mathf.Clamp01(normalizedDistance) * pathLength;
            float traversed = 0f;
            for (int i = 1; i < edgePointBuffer.Count; i++)
            {
                Vector2 start = edgePointBuffer[i - 1];
                Vector2 end = edgePointBuffer[i];
                Vector2 segment = end - start;
                float segmentLength = segment.magnitude;
                if (segmentLength < 0.01f)
                    continue;
                if (traversed + segmentLength >= targetDistance || i == edgePointBuffer.Count - 1)
                {
                    float segmentT = Mathf.Clamp01((targetDistance - traversed) / segmentLength);
                    position = Vector2.LerpUnclamped(start, end, segmentT);
                    tangent = segment;
                    return true;
                }
                traversed += segmentLength;
            }
            return false;
        }

        private float GetEdgeFlowPhase(string edgeId)
        {
            edgeId = edgeId ?? string.Empty;
            if (edgeFlowPhases.TryGetValue(edgeId, out float phase))
                return phase;
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < edgeId.Length; i++)
                    hash = (hash ^ edgeId[i]) * 16777619u;
                phase = (hash % 1000u) / 1000f;
            }
            edgeFlowPhases[edgeId] = phase;
            return phase;
        }

        private static bool IsPriorityEdge(Edge edge)
        {
            if (edge == null)
                return false;
            if (edge.selected)
                return true;
            return edge.output?.node is GraphElement outputNode && outputNode.selected
                || edge.input?.node is GraphElement inputNode && inputNode.selected;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        private static bool IsUsableWorldBound(Rect rect)
        {
            return rect.width > 0f && rect.height > 0f
                && IsFinite(rect.min) && IsFinite(rect.max);
        }

        private static void DrawFlowArrow(Painter2D painter, Vector2 center, Vector2 tangent,
            float length, float width, Color color)
        {
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector2.right;
            Vector2 direction = tangent.normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float halfLength = length * 0.5f;
            float shaftHalfWidth = width * 0.34f;
            Vector2 tip = center + direction * halfLength;
            Vector2 neck = center + direction * (length * 0.08f);
            Vector2 tail = center - direction * halfLength;
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(tail + normal * shaftHalfWidth);
            painter.LineTo(neck + normal * shaftHalfWidth);
            painter.LineTo(neck + normal * width);
            painter.LineTo(tip);
            painter.LineTo(neck - normal * width);
            painter.LineTo(neck - normal * shaftHalfWidth);
            painter.LineTo(tail - normal * shaftHalfWidth);
            painter.ClosePath();
            painter.Fill();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            if (Asset == null)
                return;
            Vector2 position = contentViewContainer.WorldToLocal(evt.mousePosition);
            Vector2 screenPosition = ownerWindow.position.position + evt.mousePosition;
            Edge contextEdge = evt.target as Edge
                ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<Edge>();
            if (contextEdge != null && contextEdge.output != null
                && contextEdge.userData is string contextEdgeId
                && !string.IsNullOrEmpty(contextEdgeId))
            {
                evt.menu.AppendAction("关系/断开并续接...",
                    _ => OpenCompatibleNodeSearch(contextEdge.output,
                        ownerWindow.position.position + contextEdge.worldBound.center, contextEdgeId));
                evt.menu.AppendSeparator();
            }
            evt.menu.AppendAction("创建节点...", _ => OpenNodeSearch(screenPosition, position));
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("选择/全选节点    Ctrl/Cmd+A", _ => SelectAllNodes());
            evt.menu.AppendAction("选择/选择同类节点", _ => SelectSameType(),
                _ => GetSelectionStatus(1));
            evt.menu.AppendAction("选择/取消选择", _ => ClearGraphSelection(),
                _ => HasSelection ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("选择/复制选中    Ctrl/Cmd+D", _ => DuplicateSelection(),
                _ => GetSelectionStatus(1));
            evt.menu.AppendAction("整理/自动布局整张图", _ => AutoLayout());
            evt.menu.AppendAction("整理/自动布局选中节点", _ => AutoLayoutSelection(),
                _ => GetSelectionStatus(2));
            evt.menu.AppendAction("整理/左对齐", _ => AlignSelection(ESGraphNodeAlignment.Left),
                _ => GetSelectionStatus(2));
            evt.menu.AppendAction("整理/水平居中对齐", _ => AlignSelection(ESGraphNodeAlignment.HorizontalCenter),
                _ => GetSelectionStatus(2));
            evt.menu.AppendAction("整理/右对齐", _ => AlignSelection(ESGraphNodeAlignment.Right),
                _ => GetSelectionStatus(2));
            evt.menu.AppendAction("整理/顶部对齐", _ => AlignSelection(ESGraphNodeAlignment.Top),
                _ => GetSelectionStatus(2));
            evt.menu.AppendAction("整理/垂直居中对齐", _ => AlignSelection(ESGraphNodeAlignment.VerticalCenter),
                _ => GetSelectionStatus(2));
            evt.menu.AppendAction("整理/底部对齐", _ => AlignSelection(ESGraphNodeAlignment.Bottom),
                _ => GetSelectionStatus(2));
            evt.menu.AppendAction("整理/水平等距分布", _ => DistributeSelection(ESGraphNodeDistribution.Horizontal),
                _ => GetSelectionStatus(3));
            evt.menu.AppendAction("整理/垂直等距分布", _ => DistributeSelection(ESGraphNodeDistribution.Vertical),
                _ => GetSelectionStatus(3));
            evt.menu.AppendAction("整理/选中节点吸附网格", _ => SnapSelectionToGrid(),
                _ => GetSelectionStatus(1));
            evt.menu.AppendAction("视图/聚焦选中    F", _ => FrameSelectionOrAll());
            evt.menu.AppendAction("视图/显示整张图", _ => FrameAll());
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("检查当前图", _ => ReportValidation());
        }

        public void OpenNodeSearchAtCenter()
        {
            if (Asset == null)
                return;
            Vector2 panelCenter = layout.center;
            Vector2 graphPosition = contentViewContainer.WorldToLocal(panelCenter);
            OpenNodeSearch(ownerWindow.position.position + panelCenter, graphPosition);
        }

        private void OpenNodeSearch(Vector2 screenPosition, Vector2 graphPosition)
        {
            if (Asset == null)
                return;
            IReadOnlyList<IESGraphNodeDefinition> definitions =
                ESGraphAuthoringRegistry.GetNodeDefinitions(Asset);
            if (definitions.Count == 0)
            {
                report?.Invoke("当前图类型没有可用的节点定义：" + Asset.DomainId);
                return;
            }
            string profileName = ESGraphAuthoringRegistry.TryGetProfile(Asset.DomainKey,
                out IESGraphAuthoringProfile profile) ? profile.DisplayName : Asset.DomainId;
            searchProvider.SetDefinitions(definitions, profileName);
            searchProvider.SetGraphPosition(graphPosition);
            SearchWindow.Open(new SearchWindowContext(screenPosition), searchProvider);
        }

        internal void OpenCompatibleNodeSearch(Port sourcePort, Vector2 screenPosition,
            string replaceEdgeId = null)
        {
            if (Asset == null || sourcePort == null || !(sourcePort.userData is string sourcePortId)
                || !portRecords.TryGetValue(sourcePortId, out ESGraphPortRecord sourceRecord))
                return;
            if (IsSingleAndConnected(sourceRecord, sourcePortId, connectionCounts, replaceEdgeId))
            {
                report?.Invoke("该端口只允许一条连接；请先删除原连线，或把端口容量改为多连接。");
                return;
            }

            IReadOnlyList<IESGraphNodeDefinition> definitions = ESGraphAuthoringRegistry.GetNodeDefinitions(Asset);
            var choices = new List<ESStableGraphNodeCreationChoice>();
            for (int i = 0; i < definitions.Count; i++)
            {
                IESGraphNodeDefinition definition = definitions[i];
                if (definition?.Ports == null)
                    continue;
                for (int p = 0; p < definition.Ports.Count; p++)
                {
                    ESGraphPortDefinition candidate = definition.Ports[p];
                    if (candidate == null || candidate.direction == sourceRecord.direction)
                        continue;
                    string outputType = sourceRecord.direction == ESGraphPortDirection.Output
                        ? sourceRecord.valueTypeId : candidate.valueTypeId;
                    string inputType = sourceRecord.direction == ESGraphPortDirection.Input
                        ? sourceRecord.valueTypeId : candidate.valueTypeId;
                    if (ArePortTypesCompatible(outputType, inputType))
                        choices.Add(new ESStableGraphNodeCreationChoice(definition, candidate, sourcePortId, p,
                            replaceEdgeId));
                }
            }

            if (choices.Count == 0)
            {
                report?.Invoke("没有找到可与「" + ESGraphChinesePresentation.GetPortName(sourceRecord.name)
                    + "」连接的节点类型。");
                return;
            }

            string profileName = ESGraphAuthoringRegistry.TryGetProfile(Asset.DomainKey,
                out IESGraphAuthoringProfile profile) ? profile.DisplayName : Asset.DomainId;
            string sourceName = ESGraphChinesePresentation.GetDirectionName(sourceRecord.direction) + " · "
                + ESGraphChinesePresentation.GetPortName(sourceRecord.name) + " · "
                + ESGraphChinesePresentation.GetPortValueTypeName(sourceRecord.valueTypeId);
            Vector2 graphPosition = contentViewContainer.WorldToLocal(
                screenPosition - ownerWindow.position.position);
            searchProvider.SetConnectionChoices(choices, profileName, sourceName);
            searchProvider.SetGraphPosition(graphPosition);
            SearchWindow.Open(new SearchWindowContext(screenPosition), searchProvider);
        }

        internal void CreateNode(ESStableGraphNodeCreationChoice choice, Vector2 position)
        {
            if (choice.AutoConnect)
                CreateNodeAndConnect(choice, position);
            else
                CreateNode(choice.Definition, position);
        }

        private void CreateNode(IESGraphNodeDefinition definition, Vector2 position)
        {
            if (Asset == null || definition == null || !definition.Domain.Equals(Asset.DomainKey))
                return;
            Undo.RecordObject(Asset, "创建图节点");
            ESGraphNodeRecord created = Asset.AddNode(definition.NodeType, definition.DisplayName, position,
                definition.Ports);
            Asset.UpdateNode(created.nodeId, definition.NodeType, definition.CurrentVersion, created.title,
                definition.CreateDefaultPayload(), out _);
            MarkAssetDirty();
            Rebuild();
            if (nodeViews.TryGetValue(created.nodeId, out ESStableGraphNodeView view))
            {
                ClearSelection();
                AddToSelection(view);
                NotifySelectionChanged();
            }
            report?.Invoke("已创建节点：" + definition.DisplayName);
        }

        private void CreateNodeAndConnect(ESStableGraphNodeCreationChoice choice, Vector2 position)
        {
            IESGraphNodeDefinition definition = choice.Definition;
            if (Asset == null || definition == null || choice.CompatiblePort == null
                || !definition.Domain.Equals(Asset.DomainKey))
                return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("创建并连接图节点");
            Undo.RegisterCompleteObjectUndo(Asset, "创建并连接图节点");
            try
            {
                ESGraphNodeRecord created = Asset.AddNode(definition.NodeType, definition.DisplayName, position,
                    definition.Ports);
                Asset.UpdateNode(created.nodeId, definition.NodeType, definition.CurrentVersion, created.title,
                    definition.CreateDefaultPayload(), out _);
                ESGraphPortRecord targetPort = choice.CompatiblePortIndex >= 0
                    && created.ports != null && choice.CompatiblePortIndex < created.ports.Count
                    ? created.ports[choice.CompatiblePortIndex] : null;
                if (targetPort == null || targetPort.direction != choice.CompatiblePort.direction)
                    targetPort = created.ports?.FirstOrDefault(port => port != null
                        && string.Equals(port.stableKey, choice.CompatiblePort.stableKey, StringComparison.Ordinal)
                        && port.direction == choice.CompatiblePort.direction);
                if (targetPort == null)
                    throw new InvalidOperationException("新节点没有找到预期的兼容端口。");
                if (!Asset.TryFindPort(choice.SourcePortId, out _, out ESGraphPortRecord sourcePort))
                    throw new InvalidOperationException("起始端口已不存在，请重新拖线。");

                if (!string.IsNullOrEmpty(choice.ReplaceEdgeId))
                {
                    ESGraphEdgeRecord replacement = Asset.FindEdge(choice.ReplaceEdgeId);
                    if (replacement == null
                        || (!string.Equals(replacement.outputPortId, choice.SourcePortId, StringComparison.Ordinal)
                            && !string.Equals(replacement.inputPortId, choice.SourcePortId, StringComparison.Ordinal)))
                        throw new InvalidOperationException("原关系已不存在，请重新选择续接目标。");
                    if (!Asset.RemoveEdge(choice.ReplaceEdgeId))
                        throw new InvalidOperationException("原关系删除失败，请重试。");
                }

                string outputPortId = sourcePort.direction == ESGraphPortDirection.Output
                    ? sourcePort.portId : targetPort.portId;
                string inputPortId = sourcePort.direction == ESGraphPortDirection.Input
                    ? sourcePort.portId : targetPort.portId;
                if (!Asset.TryAddEdge(outputPortId, inputPortId, out _, out string error))
                    throw new InvalidOperationException(error);

                Undo.CollapseUndoOperations(undoGroup);
                MarkAssetDirty();
                Rebuild();
                if (nodeViews.TryGetValue(created.nodeId, out ESStableGraphNodeView view))
                {
                    ClearSelection();
                    AddToSelection(view);
                    NotifySelectionChanged();
                }
                report?.Invoke("已创建并连接：" + definition.DisplayName + " · "
                    + ESGraphChinesePresentation.GetPortName(choice.CompatiblePort.name));
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Rebuild();
                report?.Invoke("创建并连接失败：" + exception.Message);
            }
        }

        internal void CommitDraggedEdge(Edge edge)
        {
            if (Asset == null || edge?.output == null || edge.input == null
                || !(edge.output.userData is string outputPortId)
                || !(edge.input.userData is string inputPortId))
                return;

            Undo.RecordObject(Asset, "创建图连线");
            if (!Asset.TryAddEdge(outputPortId, inputPortId, out ESGraphEdgeRecord record, out string error))
            {
                report?.Invoke(string.IsNullOrEmpty(error) ? "连线被模型拒绝" : error);
                return;
            }

            edge.userData = record.edgeId;
            edgeViews[record.edgeId] = edge;
            ConfigureEdgeReconnectGesture(edge);
            RegisterEdgeFlowGeometry(edge);
            AddElement(edge);
            edge.output.Connect(edge);
            edge.input.Connect(edge);
            MarkAssetDirty();
            BuildGraphIndexes();
            RefreshPortRelationVisuals();
            edgeFlowOverlay?.MarkDirtyRepaint();
            report?.Invoke("已建立关系：" + ESGraphChinesePresentation.GetPortName(
                portRecords[outputPortId].name) + " → "
                + ESGraphChinesePresentation.GetPortName(portRecords[inputPortId].name));
        }

        private void OnGraphKeyDown(KeyDownEvent evt)
        {
            if (Asset == null || evt.altKey || IsTextEditingTarget(evt.target))
                return;
            bool actionKey = evt.ctrlKey || evt.commandKey;
            if (actionKey && evt.keyCode == KeyCode.A)
                SelectAllNodes();
            else if (actionKey && evt.keyCode == KeyCode.D)
                DuplicateSelection();
            else if (!actionKey && evt.keyCode == KeyCode.F)
                FrameSelectionOrAll();
            else
                return;
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private void OnGraphMouseDown(MouseDownEvent evt)
        {
            if (evt.button == 0 && !IsTextEditingTarget(evt.target))
                Focus();
        }

        private void OnGraphMouseMove(MouseMoveEvent evt)
        {
            if (pendingEdgeReconnect == null)
                return;
            if ((evt.mousePosition - pendingEdgeReconnectStart).sqrMagnitude
                > EdgeReconnectMovementTolerance * EdgeReconnectMovementTolerance)
                CancelEdgeReconnect();
        }

        private void OnGraphMouseUp(MouseUpEvent evt)
        {
            if (evt.button == 0 && !edgeReconnectTriggered)
                CancelEdgeReconnect();
            edgeReconnectTriggered = false;
        }

        private void ConfigureEdgeReconnectGesture(Edge edge)
        {
            if (edge == null)
                return;
            edge.RegisterCallback<MouseDownEvent>(OnEdgeReconnectMouseDown, TrickleDown.TrickleDown);
            edge.RegisterCallback<MouseUpEvent>(OnEdgeReconnectMouseUp, TrickleDown.TrickleDown);
            edge.RegisterCallback<MouseCaptureOutEvent>(_ => CancelEdgeReconnect(), TrickleDown.TrickleDown);
        }

        private void OnEdgeReconnectMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || !(evt.currentTarget is Edge edge)
                || edge.output == null || edge.input == null || !(edge.userData is string edgeId)
                || string.IsNullOrEmpty(edgeId) || Asset == null)
                return;
            CancelEdgeReconnect();
            pendingEdgeReconnect = edge;
            pendingEdgeReconnectStart = evt.mousePosition;
            edgeReconnectTriggered = false;
            edgeReconnectSchedule = edge.schedule.Execute(() =>
            {
                Edge current = pendingEdgeReconnect;
                pendingEdgeReconnect = null;
                edgeReconnectSchedule = null;
                if (current == null || current.parent == null || Asset == null
                    || !(current.userData is string currentEdgeId)
                    || string.IsNullOrEmpty(currentEdgeId))
                    return;
                edgeReconnectTriggered = true;
                ClearSelection();
                AddToSelection(current);
                NotifySelectionChanged();
                Vector2 screenPosition = ownerWindow.position.position + current.worldBound.center;
                OpenCompatibleNodeSearch(current.output, screenPosition, currentEdgeId);
                report?.Invoke("已进入续接模式：选择兼容节点后会自动断开旧关系并完成新连接。按 Esc 可取消。");
            }).StartingIn(EdgeReconnectLongPressMilliseconds);
        }

        private void OnEdgeReconnectMouseUp(MouseUpEvent evt)
        {
            if (evt.button == 0 && !edgeReconnectTriggered)
                CancelEdgeReconnect();
        }

        private void CancelEdgeReconnect()
        {
            edgeReconnectSchedule?.Pause();
            edgeReconnectSchedule = null;
            pendingEdgeReconnect = null;
            edgeReconnectTriggered = false;
        }

        private static bool IsTextEditingTarget(IEventHandler target)
        {
            if (target is TextField)
                return true;
            return target is VisualElement element && element.GetFirstAncestorOfType<TextField>() != null;
        }

        public void SelectAllNodes()
        {
            if (Asset == null)
                return;
            ClearSelection();
            foreach (KeyValuePair<string, ESStableGraphNodeView> pair in nodeViews)
                AddToSelection(pair.Value);
            NotifySelectionChanged();
            edgeFlowOverlay?.MarkDirtyRepaint();
            report?.Invoke("已选择全部 " + nodeViews.Count + " 个节点。可继续复制、整理或删除。");
        }

        public void SelectSameType()
        {
            if (Asset == null)
                return;
            List<ESStableGraphNodeView> selectedViews = GetSelectedNodeViews();
            if (selectedViews.Count == 0)
            {
                report?.Invoke("请先选择至少一个节点，再执行“选择同类节点”。");
                return;
            }

            var selectedIds = new HashSet<string>(selectedViews.Select(view => view.NodeId), StringComparer.Ordinal);
            var selectedTypes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Asset.Nodes.Count; i++)
            {
                ESGraphNodeRecord node = Asset.Nodes[i];
                if (node != null && selectedIds.Contains(node.nodeId) && !string.IsNullOrEmpty(node.typeId))
                    selectedTypes.Add(node.typeId);
            }
            ClearSelection();
            for (int i = 0; i < Asset.Nodes.Count; i++)
            {
                ESGraphNodeRecord node = Asset.Nodes[i];
                if (node != null && selectedTypes.Contains(node.typeId)
                    && nodeViews.TryGetValue(node.nodeId, out ESStableGraphNodeView view))
                    AddToSelection(view);
            }
            NotifySelectionChanged();
            edgeFlowOverlay?.MarkDirtyRepaint();
            report?.Invoke("已选择同类节点：" + SelectedNodeCount + " 个。");
        }

        public void ClearGraphSelection()
        {
            ClearSelection();
            NotifySelectionChanged();
            edgeFlowOverlay?.MarkDirtyRepaint();
            report?.Invoke("已取消选择。");
        }

        public void FrameSelectionOrAll()
        {
            if (selection.OfType<GraphElement>().Any())
                FrameSelection();
            else
                FrameAll();
        }

        private DropdownMenuAction.Status GetSelectionStatus(int minimumCount)
        {
            return SelectedNodeCount >= minimumCount
                ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
        }

        private List<ESStableGraphNodeView> GetSelectedNodeViews()
        {
            return selection.OfType<ESStableGraphNodeView>().ToList();
        }

        public void DuplicateSelection()
        {
            if (Asset == null)
                return;
            List<string> selectedIds = selection.OfType<ESStableGraphNodeView>().Select(view => view.NodeId).ToList();
            if (selectedIds.Count == 0)
                return;
            Undo.RecordObject(Asset, "复制图节点");
            List<string> createdIds = Asset.DuplicateNodes(selectedIds, new Vector2(32f, 32f));
            MarkAssetDirty();
            Rebuild();
            ClearSelection();
            for (int i = 0; i < createdIds.Count; i++)
            {
                if (nodeViews.TryGetValue(createdIds[i], out ESStableGraphNodeView view))
                    AddToSelection(view);
            }
            NotifySelectionChanged();
        }

        public void AutoLayout()
        {
            AutoLayoutNodes(null, false);
        }

        public void AutoLayoutSelection()
        {
            var selectedIds = new HashSet<string>(
                selection.OfType<ESStableGraphNodeView>().Select(view => view.NodeId), StringComparer.Ordinal);
            if (selectedIds.Count < 2)
            {
                report?.Invoke("请至少选择两个节点，再整理选中内容。");
                return;
            }
            AutoLayoutNodes(selectedIds, true);
        }

        private void AutoLayoutNodes(HashSet<string> restrictedNodeIds, bool selectionOnly)
        {
            if (Asset == null || Asset.Nodes.Count == 0)
                return;
            var nodeByPort = new Dictionary<string, string>(StringComparer.Ordinal);
            var nodesById = new Dictionary<string, ESGraphNodeRecord>(StringComparer.Ordinal);
            var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var incoming = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var depth = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (ESGraphNodeRecord node in Asset.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.nodeId)
                    || restrictedNodeIds != null && !restrictedNodeIds.Contains(node.nodeId))
                    continue;
                nodesById[node.nodeId] = node;
                outgoing[node.nodeId] = new List<string>();
                incoming[node.nodeId] = new List<string>();
                indegree[node.nodeId] = 0;
                depth[node.nodeId] = 0;
                if (node.ports != null)
                    for (int p = 0; p < node.ports.Count; p++)
                    {
                        ESGraphPortRecord port = node.ports[p];
                        if (port != null && !string.IsNullOrEmpty(port.portId))
                            nodeByPort[port.portId] = node.nodeId;
                    }
            }
            foreach (ESGraphEdgeRecord edge in Asset.Edges)
            {
                if (edge == null || !nodeByPort.TryGetValue(edge.outputPortId, out string from)
                    || !nodeByPort.TryGetValue(edge.inputPortId, out string to)
                    || !nodesById.ContainsKey(from) || !nodesById.ContainsKey(to))
                    continue;
                outgoing[from].Add(to);
                incoming[to].Add(from);
                indegree[to]++;
            }
            if (nodesById.Count == 0)
                return;

            IComparer<string> layoutComparer = Comparer<string>.Create((left, right) =>
            {
                if (string.Equals(left, right, StringComparison.Ordinal))
                    return 0;
                ESGraphNodeRecord leftNode = nodesById[left];
                ESGraphNodeRecord rightNode = nodesById[right];
                int result = leftNode.position.y.CompareTo(rightNode.position.y);
                if (result == 0)
                    result = string.Compare(leftNode.title, rightNode.title, StringComparison.Ordinal);
                return result != 0 ? result : string.Compare(left, right, StringComparison.Ordinal);
            });
            var ready = new SortedSet<string>(layoutComparer);
            foreach (KeyValuePair<string, int> pair in indegree)
                if (pair.Value == 0)
                    ready.Add(pair.Key);
            var processed = new HashSet<string>(StringComparer.Ordinal);
            int cycleBreakCount = 0;
            while (processed.Count < nodesById.Count)
            {
                if (ready.Count == 0)
                {
                    string cycleEntry = FindStableCycleEntry(nodesById, processed);
                    if (string.IsNullOrEmpty(cycleEntry))
                        break;
                    indegree[cycleEntry] = 0;
                    ready.Add(cycleEntry);
                    cycleBreakCount++;
                }
                string current = ready.Min;
                ready.Remove(current);
                if (!processed.Add(current))
                    continue;
                foreach (string next in outgoing[current])
                {
                    if (processed.Contains(next))
                        continue;
                    depth[next] = Math.Max(depth[next], depth[current] + 1);
                    indegree[next] = Math.Max(0, indegree[next] - 1);
                    if (indegree[next] == 0)
                        ready.Add(next);
                }
            }
            if (processed.Count != nodesById.Count)
            {
                report?.Invoke("自动布局未能建立完整层级，请检查节点稳定身份和连线数据。");
                return;
            }

            int maximumDepth = depth.Values.Max();
            var layers = new List<List<string>>(maximumDepth + 1);
            for (int i = 0; i <= maximumDepth; i++)
                layers.Add(new List<string>());
            foreach (KeyValuePair<string, int> pair in depth)
                layers[pair.Value].Add(pair.Key);
            for (int i = 0; i < layers.Count; i++)
                layers[i].Sort(layoutComparer);
            OptimizeLayerOrdering(layers, incoming, outgoing, depth, nodesById);

            float originX = nodesById.Values.Min(node => node.position.x);
            float verticalCenter = (nodesById.Values.Min(node => node.position.y)
                + nodesById.Values.Max(node => node.position.y)) * 0.5f;
            var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                List<string> ids = layers[layerIndex];
                float totalHeight = Math.Max(0, ids.Count - 1) * LayoutVerticalSpacing;
                for (int i = 0; i < ids.Count; i++)
                    positions[ids[i]] = new Vector2(originX + layerIndex * LayoutHorizontalSpacing,
                        verticalCenter + i * LayoutVerticalSpacing - totalHeight * 0.5f);
            }

            string cycleNote = cycleBreakCount > 0
                ? " 检测到循环，已仅拆分布局约束，所有真实连线保持不变。" : string.Empty;
            if (!ApplyNodePositions(positions, selectionOnly ? "自动布局选中节点" : "自动布局图节点",
                    "已稳定整理 " + nodesById.Count + " 个节点，并优化连线交叉。" + cycleNote))
                return;
            if (selectionOnly)
                FrameSelection();
            else
                FrameAll();
        }

        private static string FindStableCycleEntry(Dictionary<string, ESGraphNodeRecord> nodesById,
            HashSet<string> processed)
        {
            string bestId = null;
            ESGraphNodeRecord bestNode = null;
            foreach (KeyValuePair<string, ESGraphNodeRecord> pair in nodesById)
            {
                if (processed.Contains(pair.Key))
                    continue;
                ESGraphNodeRecord node = pair.Value;
                if (bestNode == null || node.position.x < bestNode.position.x
                    || Mathf.Approximately(node.position.x, bestNode.position.x) && node.position.y < bestNode.position.y
                    || Mathf.Approximately(node.position.x, bestNode.position.x)
                    && Mathf.Approximately(node.position.y, bestNode.position.y)
                    && string.Compare(pair.Key, bestId, StringComparison.Ordinal) < 0)
                {
                    bestId = pair.Key;
                    bestNode = node;
                }
            }
            return bestId;
        }

        private static void OptimizeLayerOrdering(List<List<string>> layers,
            Dictionary<string, List<string>> incoming, Dictionary<string, List<string>> outgoing,
            Dictionary<string, int> depth, Dictionary<string, ESGraphNodeRecord> nodesById)
        {
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            var scores = new Dictionary<string, float>(StringComparer.Ordinal);
            UpdateLayerOrder(layers, order);
            for (int sweep = 0; sweep < 4; sweep++)
            {
                for (int layerIndex = 1; layerIndex < layers.Count; layerIndex++)
                    SortLayerByBarycenter(layers[layerIndex], incoming, order, scores,
                        depth, layerIndex, true, nodesById);
                UpdateLayerOrder(layers, order);
                for (int layerIndex = layers.Count - 2; layerIndex >= 0; layerIndex--)
                    SortLayerByBarycenter(layers[layerIndex], outgoing, order, scores,
                        depth, layerIndex, false, nodesById);
                UpdateLayerOrder(layers, order);
            }
        }

        private static void SortLayerByBarycenter(List<string> layer,
            Dictionary<string, List<string>> adjacency, Dictionary<string, int> order,
            Dictionary<string, float> scores,
            Dictionary<string, int> depth, int layerIndex, bool useEarlierLayers,
            Dictionary<string, ESGraphNodeRecord> nodesById)
        {
            scores.Clear();
            for (int i = 0; i < layer.Count; i++)
            {
                string nodeId = layer[i];
                float total = 0f;
                int count = 0;
                if (adjacency.TryGetValue(nodeId, out List<string> neighbours))
                {
                    for (int n = 0; n < neighbours.Count; n++)
                    {
                        string neighbour = neighbours[n];
                        if (!depth.TryGetValue(neighbour, out int neighbourDepth)
                            || useEarlierLayers && neighbourDepth >= layerIndex
                            || !useEarlierLayers && neighbourDepth <= layerIndex
                            || !order.TryGetValue(neighbour, out int neighbourOrder))
                            continue;
                        total += neighbourOrder;
                        count++;
                    }
                }
                scores[nodeId] = count > 0 ? total / count : order[nodeId];
            }
            layer.Sort((left, right) =>
            {
                int result = scores[left].CompareTo(scores[right]);
                if (result == 0)
                    result = nodesById[left].position.y.CompareTo(nodesById[right].position.y);
                return result != 0 ? result : string.Compare(left, right, StringComparison.Ordinal);
            });
            for (int i = 0; i < layer.Count; i++)
                order[layer[i]] = i;
        }

        private static void UpdateLayerOrder(List<List<string>> layers, Dictionary<string, int> order)
        {
            order.Clear();
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
                for (int i = 0; i < layers[layerIndex].Count; i++)
                    order[layers[layerIndex][i]] = i;
        }

        public void AlignSelection(ESGraphNodeAlignment alignment)
        {
            List<ESStableGraphNodeView> selectedViews = GetSelectedNodeViews();
            if (selectedViews.Count < 2)
            {
                report?.Invoke("对齐至少需要两个选中节点。");
                return;
            }

            float target = 0f;
            for (int i = 0; i < selectedViews.Count; i++)
            {
                Rect rect = selectedViews[i].GetPosition();
                float value = alignment == ESGraphNodeAlignment.Left ? rect.xMin
                    : alignment == ESGraphNodeAlignment.HorizontalCenter ? rect.center.x
                    : alignment == ESGraphNodeAlignment.Right ? rect.xMax
                    : alignment == ESGraphNodeAlignment.Top ? rect.yMin
                    : alignment == ESGraphNodeAlignment.VerticalCenter ? rect.center.y
                    : rect.yMax;
                if (i == 0)
                    target = value;
                else if (alignment == ESGraphNodeAlignment.Left || alignment == ESGraphNodeAlignment.Top)
                    target = Mathf.Min(target, value);
                else if (alignment == ESGraphNodeAlignment.Right || alignment == ESGraphNodeAlignment.Bottom)
                    target = Mathf.Max(target, value);
                else
                    target += value;
            }
            if (alignment == ESGraphNodeAlignment.HorizontalCenter
                || alignment == ESGraphNodeAlignment.VerticalCenter)
                target /= selectedViews.Count;

            var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            for (int i = 0; i < selectedViews.Count; i++)
            {
                ESStableGraphNodeView view = selectedViews[i];
                Rect rect = view.GetPosition();
                Vector2 position = rect.position;
                if (alignment == ESGraphNodeAlignment.Left)
                    position.x = target;
                else if (alignment == ESGraphNodeAlignment.HorizontalCenter)
                    position.x = target - rect.width * 0.5f;
                else if (alignment == ESGraphNodeAlignment.Right)
                    position.x = target - rect.width;
                else if (alignment == ESGraphNodeAlignment.Top)
                    position.y = target;
                else if (alignment == ESGraphNodeAlignment.VerticalCenter)
                    position.y = target - rect.height * 0.5f;
                else
                    position.y = target - rect.height;
                positions[view.NodeId] = position;
            }
            ApplyNodePositions(positions, "对齐图节点", "已对齐 " + selectedViews.Count + " 个节点。");
        }

        public void DistributeSelection(ESGraphNodeDistribution distribution)
        {
            List<ESStableGraphNodeView> selectedViews = GetSelectedNodeViews();
            if (selectedViews.Count < 3)
            {
                report?.Invoke("等距分布至少需要三个选中节点。");
                return;
            }

            if (distribution == ESGraphNodeDistribution.Horizontal)
                selectedViews.Sort((left, right) => CompareNodeViews(left, right, true));
            else
                selectedViews.Sort((left, right) => CompareNodeViews(left, right, false));

            var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            if (distribution == ESGraphNodeDistribution.Horizontal)
            {
                float first = selectedViews[0].GetPosition().xMin;
                float last = selectedViews[selectedViews.Count - 1].GetPosition().xMax;
                float totalSize = selectedViews.Sum(view => view.GetPosition().width);
                float gap = Mathf.Max(24f, (last - first - totalSize) / (selectedViews.Count - 1));
                float cursor = first;
                for (int i = 0; i < selectedViews.Count; i++)
                {
                    Rect rect = selectedViews[i].GetPosition();
                    positions[selectedViews[i].NodeId] = new Vector2(cursor, rect.y);
                    cursor += rect.width + gap;
                }
            }
            else
            {
                float first = selectedViews[0].GetPosition().yMin;
                float last = selectedViews[selectedViews.Count - 1].GetPosition().yMax;
                float totalSize = selectedViews.Sum(view => view.GetPosition().height);
                float gap = Mathf.Max(24f, (last - first - totalSize) / (selectedViews.Count - 1));
                float cursor = first;
                for (int i = 0; i < selectedViews.Count; i++)
                {
                    Rect rect = selectedViews[i].GetPosition();
                    positions[selectedViews[i].NodeId] = new Vector2(rect.x, cursor);
                    cursor += rect.height + gap;
                }
            }
            ApplyNodePositions(positions, "等距分布图节点",
                distribution == ESGraphNodeDistribution.Horizontal ? "已水平等距分布选中节点。" : "已垂直等距分布选中节点。");
        }

        public void SnapSelectionToGrid()
        {
            List<ESStableGraphNodeView> selectedViews = GetSelectedNodeViews();
            if (selectedViews.Count == 0)
            {
                report?.Invoke("请先选择需要吸附到网格的节点。");
                return;
            }
            var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            for (int i = 0; i < selectedViews.Count; i++)
            {
                ESStableGraphNodeView view = selectedViews[i];
                Vector2 position = view.GetPosition().position;
                position.x = Mathf.Round(position.x / PositionGridSize) * PositionGridSize;
                position.y = Mathf.Round(position.y / PositionGridSize) * PositionGridSize;
                positions[view.NodeId] = position;
            }
            ApplyNodePositions(positions, "节点吸附网格", "已将选中节点吸附到 32 像素网格。");
        }

        private bool ApplyNodePositions(Dictionary<string, Vector2> positions, string undoName, string successMessage)
        {
            if (Asset == null || positions == null || positions.Count == 0)
                return false;
            bool hasChanges = false;
            foreach (KeyValuePair<string, Vector2> pair in positions)
            {
                ESGraphNodeRecord node = Asset.FindNode(pair.Key);
                if (node != null && (node.position - pair.Value).sqrMagnitude > 0.01f)
                {
                    hasChanges = true;
                    break;
                }
            }
            if (!hasChanges)
            {
                report?.Invoke("节点已经处于目标位置，无需调整。");
                return false;
            }

            Undo.RecordObject(Asset, undoName);
            foreach (KeyValuePair<string, Vector2> pair in positions)
                Asset.SetNodePosition(pair.Key, pair.Value);
            MarkAssetDirty(false);
            Rebuild();
            report?.Invoke(successMessage);
            return true;
        }

        private static int CompareNodeViews(ESStableGraphNodeView left, ESStableGraphNodeView right, bool horizontal)
        {
            Rect leftRect = left.GetPosition();
            Rect rightRect = right.GetPosition();
            int result = (horizontal ? leftRect.xMin : leftRect.yMin)
                .CompareTo(horizontal ? rightRect.xMin : rightRect.yMin);
            return result != 0 ? result : string.Compare(left.NodeId, right.NodeId, StringComparison.Ordinal);
        }

        public void FindAndFrame(string query)
        {
            if (Asset == null || string.IsNullOrWhiteSpace(query))
                return;
            query = query.Trim();
            ESStableGraphNodeView foundNode = null;
            if (nodeViews.TryGetValue(query, out ESStableGraphNodeView exact))
                foundNode = exact;
            if (foundNode == null && Asset.TryFindPort(query, out ESGraphNodeRecord portOwner, out _))
                nodeViews.TryGetValue(portOwner.nodeId, out foundNode);
            if (foundNode == null)
            {
                for (int i = 0; i < Asset.Nodes.Count; i++)
                {
                    ESGraphNodeRecord node = Asset.Nodes[i];
                    if (node == null)
                        continue;
                    if (ContainsIgnoreCase(node.title, query) || ContainsIgnoreCase(node.typeId, query)
                        || ContainsIgnoreCase(node.nodeId, query))
                    {
                        nodeViews.TryGetValue(node.nodeId, out foundNode);
                        break;
                    }
                }
            }
            ClearSelection();
            if (foundNode != null)
            {
                AddToSelection(foundNode);
                FrameSelection();
                NotifySelectionChanged();
                return;
            }
            foreach (Edge edge in edges)
            {
                if (edge.userData is string edgeId && string.Equals(edgeId, query, StringComparison.Ordinal))
                {
                    AddToSelection(edge);
                    FrameSelection();
                    NotifySelectionChanged();
                    return;
                }
            }
            report?.Invoke("未找到图元素：" + query);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> compatible = new List<Port>();
            if (Asset == null || !(startPort.userData is string startPortId)
                || !portRecords.TryGetValue(startPortId, out ESGraphPortRecord startRecord)
                || !nodeIdsByPort.TryGetValue(startPortId, out string startNodeId))
                return compatible;

            HashSet<string> cycleBlockedNodes = null;
            if (!Asset.allowCycles)
                cycleBlockedNodes = CollectReachable(startNodeId,
                    startRecord.direction == ESGraphPortDirection.Output ? incomingNodes : outgoingNodes);

            foreach (Port candidate in ports)
            {
                if (candidate == startPort || !(candidate.userData is string candidateId))
                    continue;
                if (!portRecords.TryGetValue(candidateId, out ESGraphPortRecord candidateRecord))
                    continue;
                if (!nodeIdsByPort.TryGetValue(candidateId, out string candidateNodeId))
                    continue;
                if (startRecord.direction == candidateRecord.direction)
                    continue;
                if (!ArePortTypesCompatible(startRecord.direction == ESGraphPortDirection.Output
                        ? startRecord.valueTypeId : candidateRecord.valueTypeId,
                    startRecord.direction == ESGraphPortDirection.Output
                        ? candidateRecord.valueTypeId : startRecord.valueTypeId))
                    continue;
                if (IsSingleAndConnected(startRecord, startPortId, connectionCounts)
                    || IsSingleAndConnected(candidateRecord, candidateId, connectionCounts))
                    continue;

                string outputId = startRecord.direction == ESGraphPortDirection.Output ? startPortId : candidateId;
                string inputId = startRecord.direction == ESGraphPortDirection.Input ? startPortId : candidateId;
                if (edgeEndpointKeys.Contains(new EdgeEndpointKey(outputId, inputId)))
                    continue;
                if (cycleBlockedNodes != null && cycleBlockedNodes.Contains(candidateNodeId))
                    continue;
                compatible.Add(candidate);
            }
            return compatible;
        }

        private HashSet<string> CollectReachable(string startNodeId,
            Dictionary<string, List<string>> adjacency)
        {
            reachableNodeBuffer.Clear();
            traversalStack.Clear();
            if (string.IsNullOrEmpty(startNodeId))
                return reachableNodeBuffer;
            traversalStack.Push(startNodeId);
            while (traversalStack.Count > 0)
            {
                string current = traversalStack.Pop();
                if (!reachableNodeBuffer.Add(current) || !adjacency.TryGetValue(current, out List<string> nextNodes))
                    continue;
                for (int i = 0; i < nextNodes.Count; i++)
                    traversalStack.Push(nextNodes[i]);
            }
            return reachableNodeBuffer;
        }

        private static void IncrementConnectionCount(Dictionary<string, int> counts, string portId)
        {
            if (string.IsNullOrEmpty(portId)) return;
            counts.TryGetValue(portId, out int count);
            counts[portId] = count + 1;
        }

        private bool IsSingleAndConnected(ESGraphPortRecord port, string portId,
            Dictionary<string, int> connectionCounts, string ignoredEdgeId = null)
        {
            if (port == null || port.capacity != ESGraphPortCapacity.Single
                || !connectionCounts.TryGetValue(portId, out int count) || count <= 0)
                return false;
            if (string.IsNullOrEmpty(ignoredEdgeId) || Asset?.Edges == null)
                return true;
            for (int i = 0; i < Asset.Edges.Count; i++)
            {
                ESGraphEdgeRecord edge = Asset.Edges[i];
                if (edge == null || string.Equals(edge.edgeId, ignoredEdgeId, StringComparison.Ordinal))
                    continue;
                if (string.Equals(edge.outputPortId, portId, StringComparison.Ordinal)
                    || string.Equals(edge.inputPortId, portId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool ArePortTypesCompatible(string outputType, string inputType)
        {
            return string.Equals(outputType, inputType, StringComparison.Ordinal)
                || string.Equals(outputType, "*", StringComparison.Ordinal)
                || string.Equals(inputType, "*", StringComparison.Ordinal);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (rebuilding || Asset == null)
                return change;

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                Undo.RecordObject(Asset, "删除图元素");
                for (int i = 0; i < change.elementsToRemove.Count; i++)
                {
                    GraphElement element = change.elementsToRemove[i];
                    if (element is ESStableGraphNodeView nodeView)
                        Asset.RemoveNode(nodeView.NodeId);
                    else if (element is Edge edge && edge.userData is string edgeId)
                    {
                        if (ReferenceEquals(pendingEdgeReconnect, edge))
                            CancelEdgeReconnect();
                        Asset.RemoveEdge(edgeId);
                        edgeViews.Remove(edgeId);
                        edgeFlowPhases.Remove(edgeId);
                        edgeRenderPointViews.Remove(edge);
                    }
                }
                MarkAssetDirty();
                BuildGraphIndexes();
                RefreshPortRelationVisuals();
                schedule.Execute(Rebuild).StartingIn(1);
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                List<Edge> accepted = new List<Edge>(change.edgesToCreate.Count);
                Undo.RecordObject(Asset, "创建图连线");
                bool changed = false;
                for (int i = 0; i < change.edgesToCreate.Count; i++)
                {
                    Edge edge = change.edgesToCreate[i];
                    string error = null;
                    if (edge?.output?.userData is string outputPortId && edge.input?.userData is string inputPortId
                        && Asset.TryAddEdge(outputPortId, inputPortId, out ESGraphEdgeRecord record, out error))
                    {
                        edge.userData = record.edgeId;
                        edgeViews[record.edgeId] = edge;
                        ConfigureEdgeReconnectGesture(edge);
                        RegisterEdgeFlowGeometry(edge);
                        accepted.Add(edge);
                        changed = true;
                    }
                    else
                    {
                        report?.Invoke(string.IsNullOrEmpty(error) ? "连线被模型拒绝" : error);
                    }
                }
                change.edgesToCreate = accepted;
                if (changed)
                {
                    MarkAssetDirty();
                    BuildGraphIndexes();
                    RefreshPortRelationVisuals();
                    edgeFlowOverlay?.MarkDirtyRepaint();
                }
            }

            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                bool recorded = false;
                bool moved = false;
                for (int i = 0; i < change.movedElements.Count; i++)
                {
                    if (!(change.movedElements[i] is ESStableGraphNodeView nodeView))
                        continue;
                    moved = true;
                    if (!recorded && !moveUndoRecorded)
                    {
                        Undo.RecordObject(Asset, "移动图节点");
                        moveUndoRecorded = true;
                        recorded = true;
                    }
                    Asset.SetNodePosition(nodeView.NodeId, nodeView.GetPosition().position);
                }
                if (moved)
                    MarkAssetDirty(false);
            }

            report?.Invoke(Asset.Nodes.Count + " 个节点 / " + Asset.Edges.Count + " 条连线");
            return change;
        }

        private string SerializeSelection(IEnumerable<GraphElement> elements)
        {
            return elements.OfType<ESStableGraphNodeView>().Any() ? "ESStableGraph.Selection" : string.Empty;
        }

        private void PasteSelection(string operationName, string data)
        {
            if (string.Equals(data, "ESStableGraph.Selection", StringComparison.Ordinal))
                DuplicateSelection();
        }

        private void ReportValidation()
        {
            if (Asset == null)
                return;
            List<ESGraphValidationIssue> issues = ESGraphAuthoringRegistry.Validate(Asset);
            int errors = issues.Count(issue => issue != null && issue.severity == ESGraphValidationSeverity.Error);
            report?.Invoke(errors == 0 ? "校验通过" : "校验失败：" + errors + " 个错误");
        }

        private void MarkAssetDirty(bool affectsValidation = true)
        {
            EditorUtility.SetDirty(Asset);
            ownerWindow?.RequestAutoSave(Asset);
            if (affectsValidation)
                ownerWindow?.NotifyGraphModelChanged();
        }

        private void NotifySelectionChanged(bool force = false)
        {
            int hash = 0;
            int count = 0;
            for (int i = 0; i < selection.Count; i++)
            {
                string stableId;
                int kindSalt;
                if (selection[i] is ESStableGraphNodeView nodeView)
                {
                    stableId = nodeView.NodeId;
                    kindSalt = 0x2f6e2b1;
                }
                else if (selection[i] is Edge edge && edge.userData is string edgeId)
                {
                    stableId = edgeId;
                    kindSalt = 0x56d72c3;
                }
                else
                {
                    continue;
                }
                if (string.IsNullOrEmpty(stableId))
                    continue;
                hash ^= StringComparer.Ordinal.GetHashCode(stableId) ^ kindSalt;
                count++;
            }
            if (!force && count == lastSelectionCount && hash == lastSelectionHash)
                return;
            lastSelectionCount = count;
            lastSelectionHash = hash;
            SelectionChanged?.Invoke(selection);
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal readonly struct ESStableGraphNodeCreationChoice
    {
        public readonly IESGraphNodeDefinition Definition;
        public readonly ESGraphPortDefinition CompatiblePort;
        public readonly int CompatiblePortIndex;
        public readonly string SourcePortId;
        public readonly string ReplaceEdgeId;

        public bool AutoConnect => CompatiblePort != null && !string.IsNullOrEmpty(SourcePortId);

        public ESStableGraphNodeCreationChoice(IESGraphNodeDefinition definition,
            ESGraphPortDefinition compatiblePort = null, string sourcePortId = null,
            int compatiblePortIndex = -1, string replaceEdgeId = null)
        {
            Definition = definition;
            CompatiblePort = compatiblePort;
            CompatiblePortIndex = compatiblePortIndex;
            SourcePortId = sourcePortId;
            ReplaceEdgeId = replaceEdgeId;
        }
    }

    internal sealed class ESStableGraphNodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private ESStableGraphView graphView;
        private IReadOnlyList<ESStableGraphNodeCreationChoice> choices;
        private string profileName;
        private string sourcePortName;
        private Vector2 graphPosition;

        public void Initialize(ESStableGraphView value)
        {
            graphView = value;
        }

        public void SetDefinitions(IReadOnlyList<IESGraphNodeDefinition> value, string domainDisplayName)
        {
            var nextChoices = new List<ESStableGraphNodeCreationChoice>(value?.Count ?? 0);
            if (value != null)
                for (int i = 0; i < value.Count; i++)
                    nextChoices.Add(new ESStableGraphNodeCreationChoice(value[i]));
            choices = nextChoices;
            profileName = domainDisplayName;
            sourcePortName = null;
        }

        public void SetConnectionChoices(IReadOnlyList<ESStableGraphNodeCreationChoice> value,
            string domainDisplayName, string sourceName)
        {
            choices = value;
            profileName = domainDisplayName;
            sourcePortName = sourceName;
        }

        public void SetGraphPosition(Vector2 value)
        {
            graphPosition = value;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent(string.IsNullOrEmpty(sourcePortName)
                    ? "创建节点 · " + (profileName ?? "未指定领域")
                    : "从「" + sourcePortName + "」继续 · " + (profileName ?? "未指定领域")), 0)
            };
            if (choices == null)
                return tree;
            HashSet<string> groups = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < choices.Count; i++)
            {
                ESStableGraphNodeCreationChoice choice = choices[i];
                IESGraphNodeDefinition definition = choice.Definition;
                if (definition == null)
                    continue;
                string label = LocalizeMenuPath(definition.MenuPath);
                int separator = label.LastIndexOf('/');
                if (separator >= 0)
                {
                    string group = label.Substring(0, separator);
                    if (groups.Add(group))
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(group), 1));
                    label = label.Substring(separator + 1);
                }
                if (choice.AutoConnect)
                {
                    string portName = ESGraphChinesePresentation.GetPortName(
                        string.IsNullOrWhiteSpace(choice.CompatiblePort.name)
                            ? choice.CompatiblePort.stableKey : choice.CompatiblePort.name);
                    label += "  · 连接到「" + portName + "」";
                    if (!string.IsNullOrEmpty(choice.ReplaceEdgeId))
                        label += "（替换原关系）";
                }
                tree.Add(new SearchTreeEntry(new GUIContent(label))
                {
                    level = separator >= 0 ? 2 : 1,
                    userData = choice
                });
            }
            return tree;
        }

        private static string LocalizeMenuPath(string path)
        {
            return (path ?? string.Empty)
                .Replace("Agent Authoring", "智能助手编排")
                .Replace("Outputs", "产物输出")
                .Replace("AICommand", "AI 命令")
                .Replace("Agent Skill", "代理技能")
                .Replace("Story", "剧情")
                .Replace("Goal", "生成目标")
                .Replace("Reference", "引用资料")
                .Replace("Constraint", "生成约束")
                .Replace("Validation", "验证与批准")
                .Replace("Root", "根节点")
                .Replace("Sequence", "顺序组合")
                .Replace("Selector", "选择组合")
                .Replace("Parallel", "并行组合")
                .Replace("Decorator", "装饰节点")
                .Replace("Condition", "条件节点")
                .Replace("Action", "行为节点");
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (!(entry.userData is ESStableGraphNodeCreationChoice choice) || graphView == null)
                return false;
            graphView.CreateNode(choice, graphPosition);
            return true;
        }
    }

    internal sealed class ESStableGraphEdgeConnectorListener : IEdgeConnectorListener
    {
        private readonly ESStableGraphView graphView;

        public ESStableGraphEdgeConnectorListener(ESStableGraphView graphView)
        {
            this.graphView = graphView;
        }

        public void OnDrop(GraphView targetGraphView, Edge edge)
        {
            graphView?.CommitDraggedEdge(edge);
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            Port draggedPort = (edge?.output != null
                ? edge.output.edgeConnector.edgeDragHelper.draggedPort : null)
                ?? (edge?.input != null ? edge.input.edgeConnector.edgeDragHelper.draggedPort : null);
            graphView?.OpenCompatibleNodeSearch(draggedPort, position);
        }
    }

    internal sealed class ESStableGraphPortView : Port
    {
        private readonly ESGraphPortDirection semanticDirection;
        private string baseName = string.Empty;
        private string baseTooltip = string.Empty;

        private ESStableGraphPortView(Orientation orientation, Direction direction, Capacity capacity,
            Type portType, ESGraphPortDirection semanticDirection)
            : base(orientation, direction, capacity, portType)
        {
            this.semanticDirection = semanticDirection;
        }

        public static ESStableGraphPortView Create(Orientation orientation, Direction direction,
            Capacity capacity, Type portType, ESGraphPortDirection semanticDirection,
            IEdgeConnectorListener connectorListener)
        {
            var port = new ESStableGraphPortView(orientation, direction, capacity, portType,
                semanticDirection)
            {
                m_EdgeConnector = new EdgeConnector<Edge>(connectorListener)
            };
            port.AddManipulator(port.m_EdgeConnector);
            return port;
        }

        public void ConfigurePresentation(string displayName, string tooltipText)
        {
            baseName = displayName ?? string.Empty;
            baseTooltip = tooltipText ?? string.Empty;
            SetConnectionCount(0);
        }

        public void SetConnectionCount(int count)
        {
            bool connected = count > 0;
            string countText = count > 1 ? " ×" + count : string.Empty;
            if (semanticDirection == ESGraphPortDirection.Input)
                portName = (connected ? "◀ " : "◁ ") + baseName + countText;
            else
                portName = baseName + countText + (connected ? " ▶" : " ▷");
            tooltip = baseTooltip + "\n当前连接：" + (connected ? count + " 条" : "未连接")
                + (semanticDirection == ESGraphPortDirection.Output
                    ? "\n拖到空白处可创建匹配节点并自动连接。" : string.Empty);
            style.opacity = connected ? 1f : 0.68f;
        }
    }

    internal sealed class ESStableGraphNodeView : Node
    {
        private const float NodeWidth = 286f;
        private const float NodeHeight = 126f;
        private readonly Dictionary<string, Port> portViews = new Dictionary<string, Port>(StringComparer.Ordinal);
        private readonly string projectedTypeId;
        private readonly int projectedVersion;
        private readonly string projectedTitle;
        private readonly PortProjectionState[] projectedPorts;

        private readonly struct PortProjectionState
        {
            private readonly bool exists;
            private readonly string portId;
            private readonly string stableKey;
            private readonly string name;
            private readonly string valueTypeId;
            private readonly ESGraphPortDirection direction;
            private readonly ESGraphPortCapacity capacity;

            public PortProjectionState(ESGraphPortRecord port)
            {
                exists = port != null;
                portId = port?.portId;
                stableKey = port?.stableKey;
                name = port?.name;
                valueTypeId = port?.valueTypeId;
                direction = port?.direction ?? default;
                capacity = port?.capacity ?? default;
            }

            public bool Matches(ESGraphPortRecord port)
            {
                if (!exists)
                    return port == null;
                return port != null
                    && string.Equals(portId, port.portId, StringComparison.Ordinal)
                    && string.Equals(stableKey, port.stableKey, StringComparison.Ordinal)
                    && string.Equals(name, port.name, StringComparison.Ordinal)
                    && string.Equals(valueTypeId, port.valueTypeId, StringComparison.Ordinal)
                    && direction == port.direction
                    && capacity == port.capacity;
            }
        }
        public string NodeId { get; }
        public IReadOnlyDictionary<string, Port> PortViews => portViews;

        public ESStableGraphNodeView(ESGraphDomainKey domain, ESGraphNodeRecord record,
            IESGraphNodeDefinition definition, IEdgeConnectorListener edgeConnectorListener)
        {
            NodeId = record.nodeId;
            projectedTypeId = record.typeId;
            projectedVersion = record.version;
            projectedTitle = record.title;
            int portCount = record.ports?.Count ?? 0;
            projectedPorts = portCount == 0 ? Array.Empty<PortProjectionState>() : new PortProjectionState[portCount];
            for (int i = 0; i < portCount; i++)
                projectedPorts[i] = new PortProjectionState(record.ports[i]);
            userData = record.nodeId;
            title = string.IsNullOrWhiteSpace(record.title) ? record.typeId : record.title;
            string typeName = definition?.DisplayName
                ?? ESGraphChinesePresentation.GetNodeTypeName(domain.StableId, record.typeId);
            tooltip = "节点类型：" + typeName
                + "\n内部类型标识：" + record.typeId + "\n数据版本：" + record.version + "\n节点编号：" + record.nodeId
                + "\n连接端口：" + (record.ports == null ? 0 : record.ports.Count) + " 个";
            if (!string.IsNullOrWhiteSpace(definition?.Description))
                tooltip += "\n说明：" + definition.Description;
            SetPosition(new Rect(record.position, new Vector2(NodeWidth, NodeHeight)));
            Color accent = ESGraphNodeThemePalette.GetAccentColor(definition);
            Color surface = new Color(0.115f, 0.13f, 0.17f, 0.98f);
            Color border = new Color(0.28f, 0.32f, 0.4f, 0.95f);
            titleContainer.style.height = 31f;
            titleContainer.style.paddingLeft = 8f;
            titleContainer.style.paddingRight = 6f;
            titleContainer.style.backgroundColor = new Color(accent.r * 0.38f, accent.g * 0.38f, accent.b * 0.38f, 1f);
            mainContainer.style.backgroundColor = surface;
            mainContainer.style.paddingLeft = 6f;
            mainContainer.style.paddingRight = 6f;
            style.width = NodeWidth;
            style.minWidth = NodeWidth;
            style.maxWidth = NodeWidth;
            style.borderTopWidth = 3f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopColor = accent;
            style.borderBottomColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
            style.borderTopLeftRadius = 7f;
            style.borderTopRightRadius = 7f;
            style.borderBottomLeftRadius = 7f;
            style.borderBottomRightRadius = 7f;
            Label typeBadge = new Label(string.IsNullOrWhiteSpace(definition?.BadgeText)
                ? typeName
                : definition.BadgeText);
            typeBadge.style.fontSize = 9f;
            typeBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            typeBadge.style.color = Color.white;
            typeBadge.style.backgroundColor = new Color(accent.r * 0.82f, accent.g * 0.82f, accent.b * 0.82f, 0.92f);
            typeBadge.style.borderTopLeftRadius = 3f;
            typeBadge.style.borderTopRightRadius = 3f;
            typeBadge.style.borderBottomLeftRadius = 3f;
            typeBadge.style.borderBottomRightRadius = 3f;
            typeBadge.style.paddingLeft = 5f;
            typeBadge.style.paddingRight = 5f;
            typeBadge.style.marginLeft = 5f;
            titleContainer.Add(typeBadge);

            VisualElement metadata = new VisualElement();
            metadata.style.flexDirection = FlexDirection.Row;
            metadata.style.marginTop = 3f;
            metadata.style.marginBottom = 2f;
            Label versionLabel = new Label("数据版本 " + record.version + " · 编号 " + ShortId(record.nodeId));
            versionLabel.style.fontSize = 9f;
            versionLabel.style.color = new Color(0.62f, 0.68f, 0.78f, 0.95f);
            metadata.Add(versionLabel);
            extensionContainer.Add(metadata);

            if (record.ports != null)
            {
                for (int i = 0; i < record.ports.Count; i++)
                {
                    ESGraphPortRecord portRecord = record.ports[i];
                    if (portRecord == null || string.IsNullOrEmpty(portRecord.portId))
                        continue;
                    Direction direction = portRecord.direction == ESGraphPortDirection.Input ? Direction.Input : Direction.Output;
                    Port.Capacity capacity = portRecord.capacity == ESGraphPortCapacity.Multi ? Port.Capacity.Multi : Port.Capacity.Single;
                    ESStableGraphPortView port = ESStableGraphPortView.Create(Orientation.Horizontal,
                        direction, capacity, typeof(object), portRecord.direction, edgeConnectorListener);
                    string portTooltip = "方向：" + ESGraphChinesePresentation.GetDirectionName(portRecord.direction)
                        + "\n业务数据：" + ESGraphChinesePresentation.GetPortValueTypeName(portRecord.valueTypeId)
                        + "\n连接容量：" + ESGraphChinesePresentation.GetCapacityName(portRecord.capacity)
                        + "\n内部类型标识：" + portRecord.valueTypeId + "\n端口编号：" + portRecord.portId;
                    port.ConfigurePresentation(BuildPortLabel(portRecord), portTooltip);
                    port.userData = portRecord.portId;
                    port.portColor = GetPortColor(portRecord.valueTypeId);
                    port.style.minHeight = 20f;
                    if (direction == Direction.Input) inputContainer.Add(port);
                    else outputContainer.Add(port);
                    portViews[portRecord.portId] = port;
                }
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        public bool MatchesRecord(ESGraphNodeRecord record)
        {
            if (record == null || !string.Equals(NodeId, record.nodeId, StringComparison.Ordinal)
                || !string.Equals(projectedTypeId, record.typeId, StringComparison.Ordinal)
                || projectedVersion != record.version
                || !string.Equals(projectedTitle, record.title, StringComparison.Ordinal))
                return false;
            int portCount = record.ports?.Count ?? 0;
            if (projectedPorts.Length != portCount)
                return false;
            for (int i = 0; i < portCount; i++)
                if (!projectedPorts[i].Matches(record.ports[i]))
                    return false;
            return true;
        }

        public void SyncPosition(Vector2 position)
        {
            Rect current = GetPosition();
            if ((current.position - position).sqrMagnitude <= 0.01f)
                return;
            current.position = position;
            SetPosition(current);
        }

        private static Color GetPortColor(string valueTypeId)
        {
            switch (ESGraphPortValueCatalog.GetKind(valueTypeId))
            {
                case ESGraphPortValueKind.Flow: return new Color(0.68f, 0.72f, 0.8f);
                case ESGraphPortValueKind.Any: return new Color(0.72f, 0.72f, 0.76f);
                case ESGraphPortValueKind.Boolean: return new Color(0.88f, 0.52f, 0.35f);
                case ESGraphPortValueKind.Number: return new Color(0.42f, 0.76f, 0.54f);
                case ESGraphPortValueKind.Text: return new Color(0.42f, 0.67f, 0.92f);
                case ESGraphPortValueKind.Object: return new Color(0.74f, 0.58f, 0.88f);
                case ESGraphPortValueKind.AgentContext: return new Color(0.28f, 0.72f, 0.95f);
                case ESGraphPortValueKind.AgentRequirement: return new Color(0.96f, 0.64f, 0.24f);
                case ESGraphPortValueKind.AgentArtifact: return new Color(0.75f, 0.43f, 0.92f);
                default: return new Color(0.64f, 0.66f, 0.72f);
            }
        }

        private static string BuildPortLabel(ESGraphPortRecord port)
        {
            string name = ESGraphChinesePresentation.GetPortName(
                string.IsNullOrWhiteSpace(port.name) ? port.stableKey : port.name);
            string semantic = GetPortSemanticLabel(port.valueTypeId);
            return name + " · " + semantic;
        }

        private static string GetPortSemanticLabel(string valueTypeId)
        {
            return ESGraphChinesePresentation.GetPortValueTypeName(valueTypeId);
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrEmpty(value)) return "无身份";
            return value.Length <= 8 ? value : value.Substring(0, 8);
        }
    }
}
