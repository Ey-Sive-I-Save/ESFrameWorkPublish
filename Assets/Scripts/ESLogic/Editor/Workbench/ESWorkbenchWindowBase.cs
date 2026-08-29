#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using ES.EditorInternal;

namespace ES
{
    [Flags]
    public enum ESWorkbenchDirtyFlags
    {
        None = 0,
        Authoring = 1 << 0,
        Preview = 1 << 1,
        FormalOutput = 1 << 2,
        Build = 1 << 3,
        All = ~0
    }

    public sealed class ESWorkbenchDocumentDefinition
    {
        public readonly string documentId;
        public readonly string title;
        public readonly string tooltip;
        public readonly bool hostsAuthoringViewport;
        public readonly ESWorkbenchDirtyFlags dirtyFlags;
        public readonly string dirtyKey;
        public readonly Func<bool> isAvailable;
        public readonly Action draw;
        public readonly Action refresh;
        public readonly Action release;
        public readonly Action drawHeader;
        public readonly Action drawToolbar;
        public readonly Action drawCanvas;
        public readonly Action drawInspector;
        public readonly Action drawPreview;
        public readonly Action drawDiagnostics;
        public readonly Action drawFooter;
        public readonly Func<ESWorkbenchActionContext, VisualElement> createView;

        public ESWorkbenchDocumentDefinition(string documentId, string title, string tooltip,
            bool hostsAuthoringViewport, ESWorkbenchDirtyFlags dirtyFlags, Action draw = null,
            Func<bool> isAvailable = null,
            Action refresh = null, Action release = null, string dirtyKey = null,
            Action drawHeader = null, Action drawToolbar = null, Action drawCanvas = null,
            Action drawInspector = null, Action drawPreview = null, Action drawDiagnostics = null,
            Action drawFooter = null, Func<ESWorkbenchActionContext, VisualElement> createView = null)
        {
            if (string.IsNullOrWhiteSpace(documentId)) throw new ArgumentException("documentId 不能为空。", nameof(documentId));
            this.documentId = documentId;
            this.title = title ?? documentId;
            this.tooltip = tooltip ?? string.Empty;
            this.hostsAuthoringViewport = hostsAuthoringViewport;
            this.dirtyFlags = dirtyFlags;
            this.dirtyKey = string.IsNullOrWhiteSpace(dirtyKey) ? documentId : dirtyKey;
            this.draw = draw;
            this.isAvailable = isAvailable;
            this.refresh = refresh;
            this.release = release;
            this.drawHeader = drawHeader;
            this.drawToolbar = drawToolbar;
            this.drawCanvas = drawCanvas;
            this.drawInspector = drawInspector;
            this.drawPreview = drawPreview;
            this.drawDiagnostics = drawDiagnostics;
            this.drawFooter = drawFooter;
            this.createView = createView;
        }
    }

    public sealed class ESWorkbenchAuthoringModeDefinition
    {
        public ESWorkbenchAuthoringModeDefinition(
            string modeId,
            string title,
            string tooltip,
            IEnumerable<string> toolIds = null,
            IEnumerable<ESWorkbenchContentKind> contentKinds = null,
            string defaultToolId = null,
            int priority = 0,
            bool primary = false,
            Func<ESWorkbenchActionContext, bool> isAvailable = null,
            Action<ESWorkbenchActionContext> activate = null,
            Action<ESWorkbenchActionContext> deactivate = null,
            Func<ESWorkbenchActionContext, VisualElement> createInspector = null)
        {
            if (string.IsNullOrWhiteSpace(modeId)) throw new ArgumentException("modeId 不能为空。", nameof(modeId));
            ModeId = modeId.Trim();
            Title = string.IsNullOrWhiteSpace(title) ? ModeId : title.Trim();
            Tooltip = tooltip ?? string.Empty;
            ToolIds = (toolIds ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            ContentKinds = (contentKinds ?? Enumerable.Empty<ESWorkbenchContentKind>()).Distinct().ToArray();
            DefaultToolId = defaultToolId ?? string.Empty;
            Priority = priority;
            Primary = primary;
            IsAvailable = isAvailable;
            Activate = activate;
            Deactivate = deactivate;
            CreateInspector = createInspector;
        }

        public string ModeId { get; }
        public string Title { get; }
        public string Tooltip { get; }
        public IReadOnlyList<string> ToolIds { get; }
        public IReadOnlyList<ESWorkbenchContentKind> ContentKinds { get; }
        public string DefaultToolId { get; }
        public int Priority { get; }
        public bool Primary { get; }
        public Func<ESWorkbenchActionContext, bool> IsAvailable { get; }
        public Action<ESWorkbenchActionContext> Activate { get; }
        public Action<ESWorkbenchActionContext> Deactivate { get; }
        public Func<ESWorkbenchActionContext, VisualElement> CreateInspector { get; }
    }

    /// <summary>
    /// ES UGC 工作台通用底座。底座拥有 UI Toolkit 外壳、2D/3D 视口、对象库、层级、选择、
    /// 上下文 Inspector、命令、快捷键和弹窗生命周期；业务工作台只注册领域内容与写入语义。
    /// </summary>
    public abstract class ESWorkbenchWindowBase<This, TAsset, TModule> : ESSinglePageWindow<This>
        where This : ESWorkbenchWindowBase<This, TAsset, TModule>
        where TAsset : UnityEngine.Object
        where TModule : struct, Enum
    {
        protected TAsset ESWorkbench_Asset { get; private set; }
        protected SerializedObject ESWorkbench_SerializedAsset { get; private set; }
        protected string ESWorkbench_Status { get; private set; } = "请选择资产。";
        protected MessageType ESWorkbench_StatusType { get; private set; } = MessageType.Info;
        protected ESWorkbenchDirtyFlags ESWorkbench_DirtyFlags { get; private set; }
        private readonly List<ESWorkbenchDocumentDefinition> documents = new List<ESWorkbenchDocumentDefinition>();
        private readonly List<ESWorkbenchAuthoringModeDefinition> authoringModes = new List<ESWorkbenchAuthoringModeDefinition>();
        private string selectedWorkbenchDocumentId = string.Empty;
        [SerializeField] private string workbenchInstanceKey;
        [SerializeField] private ESWorkbenchLayoutState workbenchLayout = new ESWorkbenchLayoutState();
        private readonly HashSet<string> dirtyKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, ESWorkbenchAssetRegistrationState> registrationStates = new Dictionary<string, ESWorkbenchAssetRegistrationState>(StringComparer.Ordinal);
        private readonly Dictionary<string, ESWorkbenchAssetRegistrationSlot> contributionSlots = new Dictionary<string, ESWorkbenchAssetRegistrationSlot>(StringComparer.Ordinal);
        private readonly List<ESWorkbenchContributionEntry> contributionEntries = new List<ESWorkbenchContributionEntry>();
        private readonly List<TModule> activeModules = new List<TModule>();
        private readonly List<ESWorkbenchViewportDescriptor> viewports = new List<ESWorkbenchViewportDescriptor>();
        private readonly List<ESWorkbenchObjectDescriptor> objects = new List<ESWorkbenchObjectDescriptor>();
        private readonly List<ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>> objectSources = new List<ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>>();
        private readonly List<ESWorkbenchObjectDescriptor> resolvedObjects = new List<ESWorkbenchObjectDescriptor>();
        private readonly List<ESWorkbenchHierarchyDescriptor> hierarchy = new List<ESWorkbenchHierarchyDescriptor>();
        private readonly List<ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor>> hierarchySources = new List<ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor>>();
        private readonly List<ESWorkbenchHierarchyDescriptor> resolvedHierarchy = new List<ESWorkbenchHierarchyDescriptor>();
        private readonly List<ESWorkbenchAuthoringAdapterDescriptor> authoringAdapters = new List<ESWorkbenchAuthoringAdapterDescriptor>();
        private readonly List<ESWorkbenchInspectorDescriptor> inspectors = new List<ESWorkbenchInspectorDescriptor>();
        private readonly List<ESWorkbenchToolDescriptor> tools = new List<ESWorkbenchToolDescriptor>();
        private readonly List<ESWorkbenchCommandDescriptor> commands = new List<ESWorkbenchCommandDescriptor>();
        private readonly List<ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>> issueSources = new List<ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>>();
        private readonly List<ESWorkbenchIssueDescriptor> resolvedIssues = new List<ESWorkbenchIssueDescriptor>();
        private readonly List<ESWorkbenchBottomPanelDescriptor> bottomPanels = new List<ESWorkbenchBottomPanelDescriptor>();
        private readonly ESWorkbenchSelectionService selection = new ESWorkbenchSelectionService();
        private readonly ESWorkbenchToolStateService toolState = new ESWorkbenchToolStateService();
        private readonly ESWorkbenchAuthoringService authoringService = new ESWorkbenchAuthoringService();
        private ESWorkbenchPreviewScene previewScene;
        private ESWorkbenchContributionSession<TModule> contributionSession;
        private int contributionLoadCount;
        private ESWorkbenchUIToolkitHost toolkitHost;
        private ESWindowActionHosts workbenchActionHosts;
        private ESWorkbenchActionContext actionContext;
        private IVisualElementScheduledItem pendingDataRefresh;
        private bool suppressSelectionPersistence;
        private bool presentationRegistered;
        private ESWorkbenchHostPresentationDescriptor presentation;
        private Vector2 standardContentScroll;
        private bool workbenchHostSessionActive;
        private int workbenchRefreshGeneration;
        private const string AssetGuidPrefix = "ES.Workbench.AssetGuid.";
        private const string DocumentPrefix = "ES.Workbench.Document.";

        protected virtual IESWorkbenchPersistenceAdapter<TAsset> ESWorkbench_PersistenceAdapter => null;
        /// <summary>
        /// 生产工作台必须显式声明稳定身份。不得回退到类型全名，否则重命名窗口会
        /// 静默改变贡献注册、活动记录和恢复快照的权威键。
        /// </summary>
        protected abstract string ESWorkbench_WorkbenchId { get; }
        /// <summary>
        /// Stable navigation and section identities are part of the workbench contract.
        /// Domain windows may override them when they expose multiple navigation roots;
        /// the default keeps one deterministic identity per workbench.
        /// </summary>
        protected virtual string ESWorkbench_NavigatorId => ESWorkbench_WorkbenchId + ".navigator";
        protected virtual string ESWorkbench_SectionId => ESWorkbench_WorkbenchId + ".section";
        /// <summary>
        /// Workbench panes are intentionally flexible at the window level; their
        /// responsive layout policy bounds internal panes instead of imposing a
        /// misleading fixed maxSize on docked Unity windows.
        /// </summary>
        protected virtual string ESWorkbench_LayoutMaxStrategy => "unbounded-flexible";
        protected virtual string ESWorkbench_BrandTitle => "ES 内容工作台";
        protected virtual ESWorkbenchHostPresentationDescriptor ESWorkbench_DefaultPresentation =>
            new ESWorkbenchHostPresentationDescriptor(
                "core.default",
                ESWorkbench_BrandTitle,
                ESWorkbench_AssetLabel);
        protected sealed override bool ESWindow_AnimateOpeningFrame => false;
        protected sealed override bool ESWindow_UseCompactHostChrome => true;
        protected virtual bool ESWorkbench_IncludeDefaultViewports => true;
        protected virtual bool ESWorkbench_IncludeDefaultTools => true;
        /// <summary>工作台级视口手感合同；领域只覆写配置，不复制输入状态机。</summary>
        protected virtual ESWorkbenchViewportFeelSettings ESWorkbench_ViewportFeel =>
            ESWorkbenchViewportFeelSettings.Standard;
        protected virtual void ESWorkbench_RegisterDomainContributions()
        {
        }
        protected virtual void ESWorkbench_BeforeLoadContributions()
        {
        }
        /// <summary>
        /// 工作台把 ES 基础层的系统/全局/窗口动作迁入自己的商业命令区。
        /// 派生领域只向既有宿主追加控件，不得复制半休眠状态机。
        /// </summary>
        protected sealed override void ESWindow_BuildActionHosts(ESWindowActionHosts hosts)
        {
            workbenchActionHosts = hosts;
            ESWorkbench_BuildActionHosts(hosts);
        }

        protected virtual void ESWorkbench_BuildActionHosts(ESWindowActionHosts hosts)
        {
        }
        protected IReadOnlyList<ESWorkbenchContributionEntry> ESWorkbench_ContributionEntries => contributionEntries;
        protected int ESWorkbench_ActiveContributionCount => contributionSession?.ActiveDescriptors.Count ?? 0;
        protected int ESWorkbench_ContributionLoadCount => contributionLoadCount;
        protected IReadOnlyList<TModule> ESWorkbench_ActiveModules => activeModules;
        protected ESWorkbenchSelectionService ESWorkbench_Selection => selection;
        protected ESWorkbenchToolStateService ESWorkbench_ToolState => toolState;
        protected ESWorkbenchAuthoringService ESWorkbench_Authoring => authoringService;
        protected ESWorkbenchActionContext ESWorkbench_Actions => actionContext;
        protected IReadOnlyList<ESWorkbenchViewportDescriptor> ESWorkbench_Viewports => viewports;
        protected IReadOnlyList<ESWorkbenchObjectDescriptor> ESWorkbench_Objects => resolvedObjects;
        protected IReadOnlyList<ESWorkbenchHierarchyDescriptor> ESWorkbench_Hierarchy => resolvedHierarchy;
        protected IReadOnlyList<ESWorkbenchAuthoringAdapterDescriptor> ESWorkbench_AuthoringAdapters => authoringAdapters;
        protected IReadOnlyList<ESWorkbenchToolDescriptor> ESWorkbench_Tools => tools;
        protected IReadOnlyList<ESWorkbenchCommandDescriptor> ESWorkbench_Commands => commands;
        protected IReadOnlyList<ESWorkbenchIssueDescriptor> ESWorkbench_Issues => resolvedIssues;
        protected IReadOnlyList<ESWorkbenchBottomPanelDescriptor> ESWorkbench_BottomPanels => bottomPanels;

        protected void ESWorkbench_RecordTask(string taskId, string status, string message, string artifactPath = null)
        {
            ESWorkbenchPersistentActivityStore.UpsertTask(
                taskId, ESWorkbench_WorkbenchId, status, message, artifactPath);
            toolkitHost?.RefreshPersistentPanels();
        }

        protected void ESWorkbench_RecordLog(string message, MessageType type = MessageType.Info)
        {
            string status = type == MessageType.Error ? "Error"
                : type == MessageType.Warning ? "Warning" : "Info";
            ESWorkbenchPersistentActivityStore.Append(
                ESWorkbench_WorkbenchId, ESWorkbenchActivityChannel.Log, status, message);
            toolkitHost?.RefreshPersistentPanels();
        }

        /// <summary>工作台的默认模块模板；派生类可直接返回新的枚举列表。</summary>
        protected abstract List<TModule> ESWorkbench_DefaultModules { get; }

        /// <summary>在默认模块模板上执行删除、新增和排序；不会修改默认模板本身。</summary>
        protected virtual void ESWorkbench_AdjustModules(List<TModule> modules)
        {
        }

        protected bool ESWorkbench_IsDirty => ESWorkbench_DirtyFlags != ESWorkbenchDirtyFlags.None || dirtyKeys.Count > 0;

        private string ESWorkbench_StateKey(string prefix)
        {
            if (string.IsNullOrEmpty(workbenchInstanceKey)) workbenchInstanceKey = Guid.NewGuid().ToString("N");
            return prefix + GetType().FullName + "." + workbenchInstanceKey;
        }

        protected virtual string ESWorkbench_AssetLabel => "资产";
        protected void ESWorkbench_BindAsset(TAsset asset)
        {
            bool reloadContributions = contributionSession != null;
            if (reloadContributions) ESWorkbench_ReleaseContributions();
            workbenchLayout ??= new ESWorkbenchLayoutState();
            string assetGuid = ResolveAssetGuid(asset);
            bool restorePreviousSelection = !string.IsNullOrEmpty(assetGuid)
                && string.Equals(workbenchLayout.selectedAssetGuid, assetGuid, StringComparison.Ordinal);
            string selectedStableId = restorePreviousSelection ? workbenchLayout.selectedStableId : string.Empty;
            string selectedKind = restorePreviousSelection ? workbenchLayout.selectedKind : string.Empty;
            suppressSelectionPersistence = true;
            try
            {
                registrationStates.Clear();
                selection.Clear();
                ESWorkbench_SerializedAsset?.Dispose();
                ESWorkbench_Asset = asset;
                TAsset editingAsset = ESWorkbench_ResolveEditingAsset(asset);
                ESWorkbench_SerializedAsset = editingAsset == null ? null : new SerializedObject(editingAsset);
                ESWorkbench_SetStatus(asset == null ? "请选择资产。" : "已绑定资产。", MessageType.Info);
                if (asset != null)
                    SessionState.SetString(ESWorkbench_StateKey(AssetGuidPrefix), assetGuid);
                ESWorkbench_OnAssetBound(asset);
                if (reloadContributions)
                    ESWorkbench_LoadContributions();
                workbenchLayout.selectedStableId = selectedStableId;
                workbenchLayout.selectedKind = selectedKind;
                workbenchLayout.selectedAssetGuid = assetGuid;
                RestoreStableSelection();
            }
            finally
            {
                suppressSelectionPersistence = false;
            }
            PersistStableSelection(selection.Current);
            RefreshWorkbench(ESWorkbenchRefreshReason.AssetChanged);
        }

        private static string ResolveAssetGuid(UnityEngine.Object asset)
        {
            if (asset == null) return string.Empty;
            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private void RestoreStableSelection()
        {
            if (string.IsNullOrWhiteSpace(workbenchLayout?.selectedStableId)) return;
            ESWorkbenchHierarchyDescriptor item = resolvedHierarchy.Find(value => value != null
                && value.ItemId == workbenchLayout.selectedStableId
                && (string.IsNullOrEmpty(workbenchLayout.selectedKind) || value.Kind == workbenchLayout.selectedKind));
            if (item != null) selection.Select(item.ToSelection());
        }

        private void PersistStableSelection(ESWorkbenchSelection current)
        {
            if (suppressSelectionPersistence || current == null || current.IsEmpty || workbenchLayout == null) return;
            ESWorkbenchHierarchyDescriptor item = resolvedHierarchy.Find(value => value != null
                && value.ItemId == current.StableId && value.Kind == current.Kind);
            if (item == null) return;
            workbenchLayout.selectedStableId = current.StableId;
            workbenchLayout.selectedKind = current.Kind;
            workbenchLayout.selectedAssetGuid = ResolveAssetGuid(ESWorkbench_Asset);
        }

        protected void ESWorkbench_RestoreBoundAsset()
        {
            string guid = SessionState.GetString(ESWorkbench_StateKey(AssetGuidPrefix), string.Empty);
            if (string.IsNullOrEmpty(guid)) return;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TAsset asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TAsset>(path);
            if (asset != null) ESWorkbench_BindAsset(asset);
            else ESWorkbench_SetStatus("上次绑定资产已不存在或无法恢复：" + guid, MessageType.Warning);
        }

        protected void ESWorkbench_RegisterDocument(ESWorkbenchDocumentDefinition definition)
        {
            if (definition == null) return;
            for (int i = 0; i < documents.Count; i++)
                if (documents[i].documentId == definition.documentId) { documents[i] = definition; return; }
            documents.Add(definition);
        }

        protected void ESWorkbench_RegisterAuthoringMode(ESWorkbenchAuthoringModeDefinition definition)
        {
            if (definition == null) return;
            for (int i = 0; i < authoringModes.Count; i++)
                if (authoringModes[i].ModeId == definition.ModeId) { authoringModes[i] = definition; return; }
            authoringModes.Add(definition);
        }

        protected void ESWorkbench_RegisterViewport(ESWorkbenchViewportDescriptor descriptor)
        {
            if (descriptor != null && !viewports.Exists(value => value.ViewportId == descriptor.ViewportId)) viewports.Add(descriptor);
        }

        protected void ESWorkbench_RegisterObject(ESWorkbenchObjectDescriptor descriptor)
        {
            if (descriptor != null && !objects.Exists(value => value.ObjectId == descriptor.ObjectId)) objects.Add(descriptor);
        }

        protected void ESWorkbench_RegisterObjectSource(ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor> source)
        {
            if (source != null && !objectSources.Exists(value => value.SourceId == source.SourceId)) objectSources.Add(source);
        }

        protected void ESWorkbench_RegisterHierarchy(ESWorkbenchHierarchyDescriptor descriptor)
        {
            if (descriptor != null && !hierarchy.Exists(value => value.ItemId == descriptor.ItemId)) hierarchy.Add(descriptor);
        }

        protected void ESWorkbench_RegisterHierarchySource(ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor> source)
        {
            if (source != null && !hierarchySources.Exists(value => value.SourceId == source.SourceId)) hierarchySources.Add(source);
        }

        protected void ESWorkbench_RegisterAuthoringAdapter(ESWorkbenchAuthoringAdapterDescriptor descriptor)
        {
            if (descriptor != null && !authoringAdapters.Exists(value => value.AdapterId == descriptor.AdapterId))
                authoringAdapters.Add(descriptor);
        }

        protected void ESWorkbench_RegisterInspector(ESWorkbenchInspectorDescriptor descriptor)
        {
            if (descriptor != null && !inspectors.Exists(value => value.InspectorId == descriptor.InspectorId)) inspectors.Add(descriptor);
        }

        protected void ESWorkbench_RegisterTool(ESWorkbenchToolDescriptor descriptor)
        {
            if (descriptor == null || tools.Exists(value => value.ToolId == descriptor.ToolId)) return;
            tools.Add(descriptor);
            toolState.RegisterCapabilities(descriptor.ToolId, descriptor.Capabilities);
        }

        protected void ESWorkbench_RegisterCommand(ESWorkbenchCommandDescriptor descriptor)
        {
            if (descriptor != null && !commands.Exists(value => value.CommandId == descriptor.CommandId)) commands.Add(descriptor);
        }

        protected void ESWorkbench_RegisterIssueSource(ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor> source)
        {
            if (source != null && !issueSources.Exists(value => value.SourceId == source.SourceId)) issueSources.Add(source);
        }

        protected void ESWorkbench_RegisterPresentation(ESWorkbenchHostPresentationDescriptor descriptor)
        {
            if (descriptor == null || presentationRegistered) return;
            presentation = descriptor;
            presentationRegistered = true;
        }

        protected void ESWorkbench_RegisterBottomPanel(ESWorkbenchBottomPanelDescriptor descriptor)
        {
            if (descriptor != null && !bottomPanels.Exists(value => value.PanelId == descriptor.PanelId))
                bottomPanels.Add(descriptor);
        }

        protected IReadOnlyList<ESWorkbenchDocumentDefinition> ESWorkbench_Documents => documents;
        protected IReadOnlyList<ESWorkbenchAuthoringModeDefinition> ESWorkbench_AuthoringModes => authoringModes;
        protected string ESWorkbench_SelectedDocumentId => selectedWorkbenchDocumentId;
        protected string ESWorkbench_SelectedAuthoringModeId => workbenchLayout?.activeAuthoringModeId ?? string.Empty;

        protected void ESWorkbench_SelectAuthoringMode(string modeId)
        {
            toolkitHost?.SelectAuthoringMode(modeId);
        }

        protected void ESWorkbench_SelectDocument(string documentId)
        {
            if (string.IsNullOrEmpty(documentId)) return;
            for (int i = 0; i < documents.Count; i++)
                if (documents[i].documentId == documentId
                    && (documents[i].isAvailable == null || documents[i].isAvailable()))
                {
                    if (selectedWorkbenchDocumentId != documentId)
                    {
                        ESWorkbench_ReleaseCurrentDocument();
                        selectedWorkbenchDocumentId = documentId;
                        SessionState.SetString(ESWorkbench_StateKey(DocumentPrefix), documentId);
                        documents[i].refresh?.Invoke();
                    }
                    return;
                }
        }

        private void ESWorkbench_ReleaseCurrentDocument()
        {
            if (string.IsNullOrEmpty(selectedWorkbenchDocumentId)) return;
            string releasingDocumentId = selectedWorkbenchDocumentId;
            selectedWorkbenchDocumentId = string.Empty;
            ESWorkbenchDocumentDefinition document = documents.FirstOrDefault(
                value => value.documentId == releasingDocumentId);
            if (document?.release == null) return;
            try { document.release(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        protected void ESWorkbench_RestoreSelectedDocument()
        {
            string saved = SessionState.GetString(ESWorkbench_StateKey(DocumentPrefix), string.Empty);
            if (!string.IsNullOrEmpty(saved))
            {
                ESWorkbench_SelectDocument(saved);
                if (!string.IsNullOrEmpty(selectedWorkbenchDocumentId)) return;
            }
            for (int i = 0; i < documents.Count && string.IsNullOrEmpty(selectedWorkbenchDocumentId); i++)
                ESWorkbench_SelectDocument(documents[i].documentId);
        }

        protected void ESWorkbench_MarkDirty(ESWorkbenchDirtyFlags flags)
        {
            ESWorkbench_DirtyFlags |= flags;
            ESWorkbench_OnDirtyStateChanged(string.Empty, flags);
            ESWorkbench_SetStatus("存在未保存变更：" + ESWorkbench_GetDirtySummary(), MessageType.Warning);
            QueueWorkbenchDataRefresh();
        }

        protected void ESWorkbench_MarkDirty(string dirtyKey, ESWorkbenchDirtyFlags lifecycle = ESWorkbenchDirtyFlags.Authoring)
        {
            if (!string.IsNullOrWhiteSpace(dirtyKey)) dirtyKeys.Add(dirtyKey);
            ESWorkbench_DirtyFlags |= lifecycle;
            ESWorkbench_OnDirtyStateChanged(dirtyKey, lifecycle);
            ESWorkbench_SetStatus("存在未保存变更：" + ESWorkbench_GetDirtySummary(), MessageType.Warning);
            QueueWorkbenchDataRefresh();
        }

        private void MarkDirtyFromAuthoring(string dirtyKey, ESWorkbenchDirtyFlags flags)
        {
            if (!string.IsNullOrWhiteSpace(dirtyKey)) dirtyKeys.Add(dirtyKey);
            ESWorkbench_DirtyFlags |= flags;
            ESWorkbench_OnDirtyStateChanged(dirtyKey, flags);
            ESWorkbench_SetStatus("存在未保存变更：" + ESWorkbench_GetDirtySummary(), MessageType.Warning);
        }

        /// <summary>草稿型工作台可在这里持久化恢复快照；正式资产工作台默认无需额外处理。</summary>
        protected virtual void ESWorkbench_OnDirtyStateChanged(string dirtyKey, ESWorkbenchDirtyFlags flags)
        {
        }

        /// <summary>Undo/Redo 已经改变目标对象时，同步显示状态但不再次触发草稿持久化回调。</summary>
        protected void ESWorkbench_SetDirtyStateWithoutNotification(
            bool isDirty,
            string dirtyKey,
            ESWorkbenchDirtyFlags flags)
        {
            if (!isDirty)
            {
                ESWorkbench_ClearDirty();
                return;
            }

            if (!string.IsNullOrWhiteSpace(dirtyKey)) dirtyKeys.Add(dirtyKey);
            ESWorkbench_DirtyFlags |= flags;
        }

        protected virtual void ESWorkbench_OnUndoRedo()
        {
        }

        /// <summary>
        /// 请求宿主按统一生命周期刷新动态集合、层级、Inspector 与视口。
        /// 派生工作台完成领域状态变更后使用此入口，避免直接触碰内部宿主
        /// 或复制刷新顺序；默认使用显式刷新语义。
        /// </summary>
        protected void ESWorkbench_RequestRefresh(
            ESWorkbenchRefreshReason reason = ESWorkbenchRefreshReason.Explicit)
        {
            RefreshWorkbench(reason);
        }

        private void QueueWorkbenchDataRefresh()
        {
            pendingDataRefresh?.Pause();
            pendingDataRefresh = null;
            if (rootVisualElement?.panel == null)
            {
                RefreshWorkbench(ESWorkbenchRefreshReason.DataChanged);
                return;
            }
            int refreshGeneration = workbenchRefreshGeneration;
            pendingDataRefresh = rootVisualElement.schedule.Execute(() =>
            {
                pendingDataRefresh = null;
                if (!workbenchHostSessionActive || refreshGeneration != workbenchRefreshGeneration)
                    return;
                RefreshWorkbench(ESWorkbenchRefreshReason.DataChanged);
            }).StartingIn(80);
        }

        protected void ESWorkbench_ClearDirty(ESWorkbenchDirtyFlags flags = ESWorkbenchDirtyFlags.All)
        {
            ESWorkbench_DirtyFlags &= ~flags;
            if (flags == ESWorkbenchDirtyFlags.All) dirtyKeys.Clear();
        }

        protected string ESWorkbench_GetDirtySummary()
        {
            if (!ESWorkbench_IsDirty) return "无";
            if (dirtyKeys.Count == 0) return ESWorkbench_DirtyFlags.ToString();
            return string.Join("、", dirtyKeys);
        }

        protected void ESWorkbench_MarkSelectedDocumentDirty()
        {
            for (int i = 0; i < documents.Count; i++)
                if (documents[i].documentId == selectedWorkbenchDocumentId)
                {
                    ESWorkbench_MarkDirty(documents[i].dirtyKey, documents[i].dirtyFlags);
                    return;
                }
        }

        protected virtual void ESWorkbench_OnAssetBound(TAsset asset)
        {
        }

        /// <summary>返回当前窗口实际编辑的对象。默认编辑正式资产；需要草稿隔离的领域可返回 HideAndDontSave 草稿。</summary>
        protected virtual TAsset ESWorkbench_ResolveEditingAsset(TAsset asset)
        {
            return asset;
        }

        protected sealed override bool ESWindow_UseVerticalScroll => false;

        protected sealed override void ESWindow_BuildPageContent(
            ESMenuTreePageContext context,
            VisualElement content)
        {
            toolkitHost?.Dispose();
            actionContext = new ESWorkbenchActionContext(
                this,
                selection,
                toolState,
                authoringService,
                ESWorkbench_SetStatus,
                ShowWorkbenchPopup,
                RefreshWorkbench,
                MarkDirtyFromAuthoring);
            authoringService.Bind(actionContext, () => authoringAdapters, ESWorkbench_ValidateMutation);
            if (!string.IsNullOrWhiteSpace(workbenchLayout.activeToolId))
                toolState.Activate(workbenchLayout.activeToolId);
            ResolveDynamicCollections();
            EnsureActiveTool();
            toolkitHost = new ESWorkbenchUIToolkitHost(
                this,
                actionContext,
                ESWorkbench_WorkbenchId,
                ESWorkbench_BrandTitle,
                typeof(TAsset),
                () => ESWorkbench_Asset,
                value => ESWorkbench_BindAsset(value as TAsset),
                () => documents,
                () => authoringModes,
                () => viewports,
                () => resolvedObjects,
                () => resolvedHierarchy,
                () => inspectors,
                () => tools,
                () => commands,
                workbenchLayout,
                CreateDocumentView,
                ESWorkbench_SelectDocument,
                () => selectedWorkbenchDocumentId,
                () => resolvedIssues,
                () => ESWorkbench_IsDirty,
                () => bottomPanels,
                presentation,
                workbenchActionHosts,
                ESWorkbench_ViewportFeel);
            content.style.paddingLeft = 0f;
            content.style.paddingRight = 0f;
            content.style.paddingTop = 0f;
            content.style.paddingBottom = 0f;
            content.Add(toolkitHost.Build());
            toolkitHost.SetStatus(ESWorkbench_Status, ESWorkbench_StatusType);
            Undo.undoRedoPerformed -= OnWorkbenchUndoRedo;
            Undo.undoRedoPerformed += OnWorkbenchUndoRedo;
        }

        /// <summary>
        /// 兼容旧工作台的 IMGUI 总绘制入口。新底座不再直接调用它；派生窗口应逐步把能力注册为
        /// Document、AuthoringMode、Viewport、Object、Hierarchy、Inspector、Tool 或 Command。
        /// </summary>
        protected virtual void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
        }

        private VisualElement CreateDocumentView(ESWorkbenchDocumentDefinition document)
        {
            if (document == null) return null;
            if (document.createView != null) return document.createView(actionContext);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "ESWorkbenchDocumentScroll" };
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1f;
            scroll.style.minWidth = 0f;
            scroll.style.minHeight = 0f;
            var container = new IMGUIContainer(() => DrawDocument(document)) { name = "ESWorkbenchDocument" };
            container.style.flexGrow = 1f;
            container.style.minWidth = 0f;
            container.style.paddingLeft = 10f;
            container.style.paddingRight = 10f;
            container.style.paddingTop = 8f;
            container.style.paddingBottom = 8f;
            scroll.Add(container);
            return scroll;
        }

        private void DrawDocument(ESWorkbenchDocumentDefinition document)
        {
            if (document == null || (document.isAvailable != null && !document.isAvailable())) return;
            try
            {
                ESWorkbench_SerializedAsset?.Update();
                document.drawHeader?.Invoke();
                document.drawToolbar?.Invoke();
                document.drawCanvas?.Invoke();
                document.draw?.Invoke();
                document.drawInspector?.Invoke();
                document.drawPreview?.Invoke();
                document.drawDiagnostics?.Invoke();
                document.drawFooter?.Invoke();
                bool changed = ESWorkbench_SerializedAsset != null && ESWorkbench_SerializedAsset.hasModifiedProperties;
                ESWorkbench_SerializedAsset?.ApplyModifiedProperties();
                if (changed) ESWorkbench_MarkDirty(document.dirtyKey, document.dirtyFlags);
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ESWorkbench_SetStatus("页面绘制失败：" + exception.Message, MessageType.Error);
                EditorGUILayout.HelpBox(
                    "页面绘制失败。\n原因：" + exception.Message
                    + "\n影响：仅当前文档暂停绘制。"
                    + "\n恢复：修复依赖后切换文档或刷新工作台。",
                    MessageType.Error);
            }
        }

        private void ShowWorkbenchPopup(ESWorkbenchPopupRequest request, Rect screenAnchor)
        {
            if (request == null) return;
            if (screenAnchor.width <= 0f || screenAnchor.height <= 0f)
                screenAnchor = new Rect(position.center, Vector2.one);
            ESWorkbenchPopupWindow.Open(this, request, actionContext, screenAnchor);
        }

        private void OnWorkbenchUndoRedo()
        {
            if (!workbenchHostSessionActive)
                return;
            ESWorkbench_SerializedAsset?.UpdateIfRequiredOrScript();
            ESWorkbench_OnUndoRedo();
            RefreshWorkbench(ESWorkbenchRefreshReason.UndoRedo);
        }

        private void RefreshWorkbench(ESWorkbenchRefreshReason reason)
        {
            if (reason == ESWorkbenchRefreshReason.AssetChanged || reason == ESWorkbenchRefreshReason.DataChanged
                || reason == ESWorkbenchRefreshReason.UndoRedo || reason == ESWorkbenchRefreshReason.Explicit
                || reason == ESWorkbenchRefreshReason.Initial)
                ResolveDynamicCollections();
            toolkitHost?.Refresh(reason);
        }

        /// <summary>
        /// 在窗口主线程上实例化当前工作台的贡献。目录只保存稳定描述，真实页面/工具由本次窗口会话注入。
        /// </summary>
        protected void ESWorkbench_LoadContributions()
        {
            ESWorkbench_ReleaseContributions();
            presentation = ESWorkbench_DefaultPresentation ?? ESWorkbenchHostPresentationDescriptor.CreateDefault();
            presentationRegistered = false;
            ESWorkbench_InitializeModules();
            RegisterDefaultAuthoringCapabilities();
            contributionSession = ESWorkbenchContributionRegistry<TModule>.Open(
                ESWorkbench_WorkbenchId,
                activeModules,
                this,
                ESWorkbench_RegisterDocument,
                ESWorkbench_RegisterAuthoringMode,
                slot =>
                {
                    if (!contributionSlots.ContainsKey(slot.slotId)) contributionSlots.Add(slot.slotId, slot);
                },
                entry => contributionEntries.Add(entry),
                ESWorkbench_RegisterViewport,
                ESWorkbench_RegisterObject,
                ESWorkbench_RegisterObjectSource,
                ESWorkbench_RegisterHierarchy,
                ESWorkbench_RegisterHierarchySource,
                ESWorkbench_RegisterAuthoringAdapter,
                ESWorkbench_RegisterInspector,
                ESWorkbench_RegisterTool,
                ESWorkbench_RegisterCommand,
                message => ESWorkbench_SetStatus(message, MessageType.Warning),
                ESWorkbench_RegisterIssueSource,
                ESWorkbench_RegisterPresentation,
                ESWorkbench_RegisterBottomPanel);
            contributionLoadCount++;
            ResolveDynamicCollections();
            EnsureActiveTool();
            toolkitHost?.UpdatePresentation(presentation);
            toolkitHost?.RefreshRegistrations();
            ESWorkbench_RestoreSelectedDocument();
        }

        private void ResolveDynamicCollections()
        {
            ResolveCollection(
                objects,
                objectSources,
                resolvedObjects,
                value => value.ObjectId,
                "对象");
            ResolveCollection(
                hierarchy,
                hierarchySources,
                resolvedHierarchy,
                value => value.ItemId,
                "层级项");
            ResolveCollection(
                Array.Empty<ESWorkbenchIssueDescriptor>(),
                issueSources,
                resolvedIssues,
                value => value.IssueId,
                "问题");
        }

        private void ResolveCollection<T>(
            IReadOnlyList<T> staticItems,
            IReadOnlyList<ESWorkbenchCollectionSource<T>> sources,
            List<T> output,
            Func<T, string> getId,
            string displayKind) where T : class
        {
            output.Clear();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < staticItems.Count; i++)
                AddResolvedItem(staticItems[i], getId, ids, output);
            if (actionContext == null) return;

            IEnumerable<ESWorkbenchCollectionSource<T>> ordered = sources
                .Where(value => value != null)
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.SourceId, StringComparer.Ordinal);
            foreach (ESWorkbenchCollectionSource<T> source in ordered)
            {
                if (source.IsAvailable != null && !source.IsAvailable(actionContext)) continue;
                try
                {
                    IEnumerable<T> values = source.Query(actionContext);
                    if (values == null) continue;
                    foreach (T value in values) AddResolvedItem(value, getId, ids, output);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    ESWorkbench_SetStatus(displayKind + "源解析失败：" + source.SourceId + " · " + exception.Message,
                        MessageType.Error);
                }
            }
        }

        private static void AddResolvedItem<T>(T value, Func<T, string> getId, HashSet<string> ids, List<T> output)
            where T : class
        {
            if (value == null) return;
            string id = getId(value);
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) return;
            output.Add(value);
        }

        private void EnsureActiveTool()
        {
            if (tools.Exists(value => value != null && value.ToolId == toolState.ActiveToolId
                && (value.IsAvailable == null || (actionContext != null && value.IsAvailable(actionContext))))) return;
            ESWorkbenchToolDescriptor first = tools
                .Where(value => value != null && (value.IsAvailable == null
                    || (actionContext != null && value.IsAvailable(actionContext))))
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.ToolId, StringComparer.Ordinal)
                .FirstOrDefault();
            toolState.Activate(first?.ToolId);
        }

        protected void ESWorkbench_InitializeModules()
        {
            activeModules.Clear();
            List<TModule> defaults = ESWorkbench_DefaultModules ?? new List<TModule>();
            activeModules.AddRange(defaults);
            ESWorkbench_AdjustModules(activeModules);
            for (int i = activeModules.Count - 1; i >= 0; i--)
            {
                if (activeModules.IndexOf(activeModules[i]) != i)
                    activeModules.RemoveAt(i);
            }
        }

        private void RegisterDefaultAuthoringCapabilities()
        {
            if (ESWorkbench_IncludeDefaultViewports)
            {
                ESWorkbench_RegisterViewport(new ESWorkbenchViewportDescriptor(
                    "core.canvas-2d", "2D", ESWorkbenchViewportKind.Canvas2D,
                    context => new ESWorkbenchCanvas2DViewport(context), "二维布局与对象编排", priority: 20));
                ESWorkbench_RegisterViewport(new ESWorkbenchViewportDescriptor(
                    "core.preview-3d", "3D", ESWorkbenchViewportKind.Scene3D,
                    context => new ESWorkbenchPreview3DViewport(context), "三维对象预览与编排", priority: 10));
            }

            if (ESWorkbench_IncludeDefaultTools)
            {
                ESWorkbench_RegisterTool(new ESWorkbenchToolDescriptor(
                    "core.select", "选择", context => Tools.current = Tool.View, "选择或查看对象", priority: 100,
                    shortcut: new ESWorkbenchShortcut(KeyCode.Q)));
                ESWorkbench_RegisterTool(new ESWorkbenchToolDescriptor(
                    "core.move", "移动", context => Tools.current = Tool.Move, "移动当前对象", priority: 90,
                    shortcut: new ESWorkbenchShortcut(KeyCode.W)));
                ESWorkbench_RegisterTool(new ESWorkbenchToolDescriptor(
                    "core.rotate", "旋转", context => Tools.current = Tool.Rotate, "旋转当前对象", priority: 80,
                    shortcut: new ESWorkbenchShortcut(KeyCode.E)));
                ESWorkbench_RegisterTool(new ESWorkbenchToolDescriptor(
                    "core.scale", "缩放", context => Tools.current = Tool.Scale, "缩放当前对象", priority: 70,
                    shortcut: new ESWorkbenchShortcut(KeyCode.R)));
            }

            ESWorkbench_RegisterCommand(new ESWorkbenchCommandDescriptor(
                "core.undo", "撤销", _ => Undo.PerformUndo(), "撤销上一步作者操作",
                priority: 120,
                shortcut: new ESWorkbenchShortcut(KeyCode.Z, EventModifiers.Control),
                iconOnly: true,
                role: ESWorkbenchCommandRole.History,
                visibility: ESWorkbenchCommandVisibility.Pinned,
                unityIconName: "d_Animation.PrevKey"));
            ESWorkbench_RegisterCommand(new ESWorkbenchCommandDescriptor(
                "core.redo", "重做", _ => Undo.PerformRedo(), "重做上一步作者操作",
                priority: 110,
                shortcut: new ESWorkbenchShortcut(KeyCode.Y, EventModifiers.Control),
                iconOnly: true,
                role: ESWorkbenchCommandRole.History,
                visibility: ESWorkbenchCommandVisibility.Pinned,
                unityIconName: "d_Animation.NextKey"));
            ESWorkbench_RegisterCommand(new ESWorkbenchCommandDescriptor(
                "core.save", "保存", _ => ESWorkbench_Save(), "保存当前工作台资产",
                priority: 100,
                shortcut: new ESWorkbenchShortcut(KeyCode.S, EventModifiers.Control),
                canExecute: _ => ESWorkbench_Asset != null,
                role: ESWorkbenchCommandRole.Primary,
                visibility: ESWorkbenchCommandVisibility.Pinned,
                unityIconName: "d_SaveAs"));
            ESWorkbench_RegisterCommand(new ESWorkbenchCommandDescriptor(
                "core.locate", "定位", _ => ESWorkbench_Locate(), "在 Project 中定位当前资产",
                priority: 90,
                canExecute: _ => ESWorkbench_Asset != null,
                showInContextMenu: true,
                unityIconName: "d_Project"));
            ESWorkbench_RegisterCommand(new ESWorkbenchCommandDescriptor(
                "core.refresh", "刷新", context => context.Refresh(), "刷新对象、层级、Inspector 与当前视口",
                priority: 80,
                shortcut: new ESWorkbenchShortcut(KeyCode.R, EventModifiers.Control),
                unityIconName: "d_Refresh"));
            ESWorkbench_RegisterCommand(new ESWorkbenchCommandDescriptor(
                "core.duplicate", "复制所选", context =>
                {
                    if (context.Selection.CurrentSet.Count > 1)
                        context.Authoring.TryDuplicateMany(context.Selection.CurrentSet, out _);
                    else
                        context.Authoring.TryDuplicate(context.Selection.Current, out _);
                }, "复制当前作者对象",
                priority: 70,
                shortcut: new ESWorkbenchShortcut(KeyCode.D, EventModifiers.Control),
                canExecute: context => context.Selection.CurrentSet.Count > 1
                    ? context.Selection.CurrentSet.All(context.Authoring.CanDuplicate)
                    : context.Authoring.CanDuplicate(context.Selection.Current),
                showInToolbar: false,
                showInContextMenu: true,
                role: ESWorkbenchCommandRole.Authoring,
                unityIconName: "TreeEditor.Duplicate"));
            ESWorkbench_RegisterCommand(new ESWorkbenchCommandDescriptor(
                "core.delete", "删除所选", context =>
                {
                    if (context.Selection.CurrentSet.Count > 1)
                        context.Authoring.TryDeleteMany(context.Selection.CurrentSet, out _);
                    else
                        context.Authoring.TryDelete(context.Selection.Current, out _);
                }, "删除当前作者对象",
                priority: 60,
                shortcut: new ESWorkbenchShortcut(KeyCode.Delete),
                canExecute: context => context.Selection.CurrentSet.Count > 1
                    ? context.Selection.CurrentSet.All(context.Authoring.CanDelete)
                    : context.Authoring.CanDelete(context.Selection.Current),
                showInToolbar: false,
                showInContextMenu: true,
                role: ESWorkbenchCommandRole.Dangerous,
                unityIconName: "TreeEditor.Trash"));

            if (ESWorkbench_Asset != null)
            {
                UnityEngine.Object editingTarget = ESWorkbench_SerializedAsset?.targetObject ?? ESWorkbench_Asset;
                ESWorkbench_RegisterHierarchy(new ESWorkbenchHierarchyDescriptor(
                    "asset.root", ESWorkbench_Asset.name, unityObject: editingTarget, kind: "asset-root", order: int.MinValue));
                ESWorkbench_RegisterInspector(new ESWorkbenchInspectorDescriptor(
                    "core.unity-object",
                    value => value != null && value.UnityObject != null,
                    (context, value) => CreateSerializedInspector(value.UnityObject),
                    -100));
            }
        }

        private static VisualElement CreateSerializedInspector(UnityEngine.Object target)
        {
            if (target == null) return null;
            var root = new VisualElement { name = "ESWorkbenchSerializedInspector" };
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 6f;
            root.style.paddingBottom = 8f;
            SerializedObject serialized = null;
            bool released = false;
            try
            {
                serialized = new SerializedObject(target);
                SerializedProperty iterator = serialized.GetIterator();
                bool enterChildren = true;
                int visibleFieldCount = 0;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    SerializedProperty property = iterator.Copy();
                    var field = new PropertyField(property);
                    if (property.propertyPath == "m_Script") field.SetEnabled(false);
                    field.style.marginBottom = 3f;
                    root.Add(field);
                    visibleFieldCount++;
                }
                if (visibleFieldCount == 0)
                    root.Add(ESWindowPresentation.CreateEmptyState("没有可编辑属性", "当前对象没有可序列化字段。", null, null));
                root.Bind(serialized);
                root.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    if (released) return;
                    released = true;
                    root.Unbind();
                    serialized.Dispose();
                });
                return root;
            }
            catch (Exception exception)
            {
                if (serialized != null)
                    serialized.Dispose();
                root.Add(ESWindowPresentation.CreateEmptyState(
                    "Inspector 暂时不可用",
                    "目标对象在窗口重建期间失效，已安全跳过本次绘制。",
                    null,
                    null));
                Debug.LogException(new InvalidOperationException(
                    "ES 工作台序列化 Inspector 创建失败。", exception));
                return root;
            }
        }

        protected bool ESWorkbench_IsModuleEnabled(TModule module)
        {
            return activeModules.Contains(module);
        }

        protected string[] ESWorkbench_GetActiveModuleDisplayNames()
        {
            string[] names = new string[activeModules.Count];
            for (int i = 0; i < activeModules.Count; i++) names[i] = ESWorkbench_GetModuleDisplayName(activeModules[i]);
            return names;
        }

        protected virtual string ESWorkbench_GetModuleDisplayName(TModule module) => module.ToString();

        protected void ESWorkbench_ReleaseContributions()
        {
            toolkitHost?.ReleaseContributedContent();
            ESWorkbench_ReleaseCurrentDocument();
            documents.Clear();
            authoringModes.Clear();
            contributionSession?.Dispose();
            contributionSession = null;
            contributionEntries.Clear();
            contributionSlots.Clear();
            viewports.Clear();
            objects.Clear();
            objectSources.Clear();
            resolvedObjects.Clear();
            hierarchy.Clear();
            hierarchySources.Clear();
            resolvedHierarchy.Clear();
            authoringAdapters.Clear();
            inspectors.Clear();
            tools.Clear();
            toolState.ClearRegisteredCapabilities();
            commands.Clear();
            issueSources.Clear();
            resolvedIssues.Clear();
            bottomPanels.Clear();
            presentationRegistered = false;
        }

        protected bool ESWorkbench_IsHierarchyLocked(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || workbenchLayout?.lockedHierarchyIds == null)
                return false;
            var locked = new HashSet<string>(
                workbenchLayout.lockedHierarchyIds.Where(value => !string.IsNullOrWhiteSpace(value)),
                StringComparer.Ordinal);
            string current = itemId;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
            {
                if (locked.Contains(current)) return true;
                ESWorkbenchHierarchyDescriptor descriptor = resolvedHierarchy.FirstOrDefault(
                    value => value != null && string.Equals(value.ItemId, current, StringComparison.Ordinal));
                current = descriptor?.ParentId;
            }
            return false;
        }

        protected virtual string ESWorkbench_ValidateMutation(
            ESWorkbenchMutationKind kind,
            ESWorkbenchSelection target,
            ESWorkbenchObjectDescriptor item)
        {
            if (kind == ESWorkbenchMutationKind.Create || target == null || target.IsEmpty)
                return string.Empty;
            return ESWorkbench_IsHierarchyLocked(target.StableId)
                ? "对象或其父级已锁定，不能执行" + kind + "操作。"
                : string.Empty;
        }

        protected override void ESWindow_OnHostEnable()
        {
            if (workbenchHostSessionActive) return;
            ESWorkbench_ValidateStableIdentity();
            base.ESWindow_OnHostEnable();
            workbenchHostSessionActive = true;
            workbenchRefreshGeneration++;
            try
            {
                workbenchLayout ??= new ESWorkbenchLayoutState();
                selection.Changed -= PersistStableSelection;
                selection.Changed += PersistStableSelection;
                ESWorkbench_RegisterDomainContributions();
                ESWorkbench_BeforeLoadContributions();
                ESWorkbench_LoadContributions();
            }
            catch
            {
                ESWindow_OnHostDisable();
                throw;
            }
        }

        private void ESWorkbench_ValidateStableIdentity()
        {
            string workbenchId = ESWorkbench_WorkbenchId;
            if (string.IsNullOrWhiteSpace(workbenchId))
                throw new InvalidOperationException(
                    GetType().Name + " 必须声明非空的稳定 ESWorkbench_WorkbenchId。" );

            if (string.Equals(workbenchId.Trim(), GetType().FullName, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    GetType().Name + " 不得使用类型全名作为 ESWorkbench_WorkbenchId；请声明稳定产品身份。" );

            if (workbenchId.Any(char.IsWhiteSpace))
                throw new InvalidOperationException(
                    GetType().Name + " 的 ESWorkbench_WorkbenchId 不得包含空白字符。" );
        }

        protected bool ESWorkbench_TryGetContributionSlot(string slotId, out ESWorkbenchAssetRegistrationSlot slot)
        {
            return contributionSlots.TryGetValue(slotId ?? string.Empty, out slot);
        }

        protected override void ESWindow_OnHostDisable()
        {
            if (!workbenchHostSessionActive) return;
            workbenchHostSessionActive = false;
            workbenchRefreshGeneration++;
            try { pendingDataRefresh?.Pause(); }
            catch (Exception exception) { Debug.LogException(exception); }
            pendingDataRefresh = null;
            try { ESWorkbench_ReleaseContributions(); }
            catch (Exception exception) { Debug.LogException(exception); }
            try { toolkitHost?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
            toolkitHost = null;
            try { authoringService.Unbind(); }
            catch (Exception exception) { Debug.LogException(exception); }
            actionContext = null;
            selection.Changed -= PersistStableSelection;
            Undo.undoRedoPerformed -= OnWorkbenchUndoRedo;
            try { ESWorkbench_OnHostCleanup(); }
            catch (Exception exception) { Debug.LogException(exception); }
            try { base.ESWindow_OnHostDisable(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        /// <summary>
        /// 工作台实例键只用于当前 EditorWindow 的恢复状态。窗口真正销毁时清理对应
        /// SessionState，避免每次新建工作台都留下不可达的资产/文档键；OnDisable
        /// 不执行此清理，以保留 Domain Reload 和 UI 重建恢复语义。
        /// </summary>
        protected override void OnDestroy()
        {
            if (previewScene != null)
            {
                try { ESWorkbench_ClosePreviewScene(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            if (!string.IsNullOrEmpty(workbenchInstanceKey))
            {
                string instanceSuffix = GetType().FullName + "." + workbenchInstanceKey;
                SessionState.EraseString(AssetGuidPrefix + instanceSuffix);
                SessionState.EraseString(DocumentPrefix + instanceSuffix);
            }
            base.OnDestroy();
        }

        protected ESWorkbenchAssetRegistrationState ESWorkbench_GetRegistrationState(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId)) throw new ArgumentException("注册槽位 ID 不能为空。", nameof(slotId));
            if (!registrationStates.TryGetValue(slotId, out ESWorkbenchAssetRegistrationState state))
            {
                state = new ESWorkbenchAssetRegistrationState();
                registrationStates.Add(slotId, state);
            }
            return state;
        }

        protected void ESWorkbench_DrawRegistrationSlot(ESWorkbenchAssetRegistrationSlot slot,
            Action<ESContentRegistrationResult> applyBinding)
        {
            ESWorkbenchAssetRegistrationState state = ESWorkbench_GetRegistrationState(slot.slotId);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                UnityEngine.Object previousSource = state.source;
                string previousKey = state.desiredStringKey;
                EditorGUI.BeginChangeCheck();
                state.source = EditorGUILayout.ObjectField(slot.label, state.source, slot.objectType, false);
                state.desiredStringKey = EditorGUILayout.TextField("稳定 StringKey", state.desiredStringKey);
                if (EditorGUI.EndChangeCheck() && (previousSource != state.source || !string.Equals(previousKey, state.desiredStringKey, StringComparison.Ordinal)))
                    state.InvalidatePreview();
                if (!string.IsNullOrWhiteSpace(slot.help)) EditorGUILayout.HelpBox(slot.help, MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("注册预检", ESEditorPresentation.ToolbarButtonStyle))
                    {
                        state.lastResult = ESWorkbenchContentRegistration.Preview(state.source, state.desiredStringKey, slot.libraryPath);
                        ESWorkbench_SetStatus(state.lastResult == null ? "注册预检无结果。" : state.lastResult.message,
                            state.lastResult != null && state.lastResult.success ? MessageType.Info : MessageType.Error);
                    }
                    bool canCommit = state.source != null && state.lastResult != null && state.lastResult.success;
                    using (new EditorGUI.DisabledScope(!canCommit))
                    {
                        if (GUILayout.Button("提交并绑定", ESEditorPresentation.ToolbarButtonStyle))
                        {
                            ESContentRegistrationResult result = ESWorkbenchContentRegistration.Commit(
                                state.source, state.desiredStringKey, slot.libraryPath, state.lastResult);
                            state.lastResult = result;
                            ESWorkbench_SetStatus(result == null ? "注册提交无结果。" : result.message,
                                result != null && result.success ? MessageType.Info : MessageType.Error);
                            if (result != null && result.success)
                            {
                                ESWorkbench_Record(slot.undoName);
                                applyBinding?.Invoke(result);
                                ESWorkbench_MarkDirty(slot.dirtyKey, ESWorkbenchDirtyFlags.Authoring);
                            }
                        }
                    }
                }
                if (state.lastResult != null)
                    EditorGUILayout.HelpBox((state.lastResult.success ? "成功：" : "失败：") + state.lastResult.message,
                        state.lastResult.success ? MessageType.Info : MessageType.Error);
            }
        }

        protected void ESWorkbench_DrawRegistrationSlot(ESWorkbenchAssetRegistrationSlot slot)
        {
            ESWorkbench_DrawRegistrationSlot(slot, result =>
            {
                if (ESWorkbench_SerializedAsset == null || string.IsNullOrWhiteSpace(slot.targetPropertyPath))
                {
                    ESWorkbench_SetStatus("注册成功，但槽位没有可写的目标属性路径。", MessageType.Error);
                    return;
                }
                SerializedProperty target = ESWorkbench_SerializedAsset.FindProperty(slot.targetPropertyPath);
                if (target == null || target.propertyType != SerializedPropertyType.String)
                {
                    ESWorkbench_SetStatus("注册成功，但目标属性不是可写字符串：" + slot.targetPropertyPath, MessageType.Error);
                    return;
                }
                target.stringValue = result.stringKey;
                ESWorkbench_SerializedAsset.ApplyModifiedProperties();
                ESWorkbench_SetStatus("资源已注册并绑定到工作台槽位。", MessageType.Info);
            });
        }

        protected ESWorkbenchPreviewScene ESWorkbench_GetPreviewScene()
        {
            if (previewScene == null) previewScene = new ESWorkbenchPreviewScene();
            return previewScene;
        }

        protected void ESWorkbench_ClosePreviewScene()
        {
            if (previewScene == null) return;
            previewScene.Dispose();
            // Close() retains the scene handle when Unity refuses to close the
            // PreviewScene. Keep the owner alive so the next host-cleanup pass
            // can retry instead of losing the only recovery path.
            if (!previewScene.IsOpen)
                previewScene = null;
        }

        protected void ESWorkbench_SetStatus(string message, MessageType type = MessageType.Info)
        {
            ESWorkbench_Status = string.IsNullOrWhiteSpace(message) ? "无状态信息。" : message;
            ESWorkbench_StatusType = type;
            toolkitHost?.SetStatus(ESWorkbench_Status, type);
        }

        protected void ESWorkbench_Record(string undoName)
        {
            UnityEngine.Object target = ESWorkbench_SerializedAsset?.targetObject ?? ESWorkbench_Asset;
            if (target != null) Undo.RecordObject(target, undoName);
        }

        protected virtual void ESWorkbench_Save()
        {
            ESWorkbench_SerializedAsset?.ApplyModifiedProperties();
            if (ESWorkbench_Asset == null) return;
            if (ESWorkbench_PersistenceAdapter != null)
            {
                if (!ESWorkbench_PersistenceAdapter.TrySave(ESWorkbench_Asset, ESWorkbench_SerializedAsset, out string adapterMessage))
                {
                    ESWorkbench_SetStatus("保存失败：" + adapterMessage, MessageType.Error);
                    return;
                }
                ESWorkbench_ClearDirty();
                ESWorkbench_SetStatus(adapterMessage ?? "资产已保存。", MessageType.Info);
                RefreshWorkbench(ESWorkbenchRefreshReason.DataChanged);
                return;
            }
            EditorUtility.SetDirty(ESWorkbench_Asset);
            AssetDatabase.SaveAssetIfDirty(ESWorkbench_Asset);
            ESWorkbench_ClearDirty();
            ESWorkbench_SetStatus("资产已保存。", MessageType.Info);
            RefreshWorkbench(ESWorkbenchRefreshReason.DataChanged);
        }

        protected ESContentRegistrationResult ESWorkbench_PreviewBake()
        {
            return ESContentRegistrationAuthoring.Execute(new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.Bake,
                commit = false
            });
        }

        protected ESContentRegistrationResult ESWorkbench_CommitBake(string requestId)
        {
            return ESContentRegistrationAuthoring.Execute(new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.Bake,
                requestId = requestId ?? string.Empty,
                commit = true
            });
        }

        protected ESContentRegistrationResult ESWorkbench_QueryBake(string requestId, string runId)
        {
            return ESContentRegistrationAuthoring.Execute(new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.Status,
                requestId = requestId ?? string.Empty,
                runId = runId ?? string.Empty,
                commit = false
            });
        }

        protected virtual void ESWorkbench_OnHostCleanup()
        {
            ESWorkbench_ReleaseContributions();
            ESWorkbench_ClosePreviewScene();
            registrationStates.Clear();
            ESWorkbench_SerializedAsset?.Dispose();
            ESWorkbench_SerializedAsset = null;
            selection.Clear();
        }

        protected void ESWorkbench_Locate()
        {
            if (ESWorkbench_Asset == null) return;
            Selection.activeObject = ESWorkbench_Asset;
            EditorGUIUtility.PingObject(ESWorkbench_Asset);
            ESWorkbench_SetStatus("已定位资产。", MessageType.Info);
        }

        protected bool ESWorkbench_Validate(Func<string> validator)
        {
            if (validator == null) return false;
            string error = validator();
            bool success = string.IsNullOrWhiteSpace(error);
            ESWorkbench_SetStatus(success ? "验证通过。" : error, success ? MessageType.Info : MessageType.Error);
            return success;
        }

        protected void ESWorkbench_DrawStatus(string title = "工作台状态")
        {
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                GUILayout.Label(title, ESEditorPresentation.HeaderStyle);
                ESStatusKind status = ESWorkbench_StatusType == MessageType.Error
                    ? ESStatusKind.Error
                    : ESWorkbench_StatusType == MessageType.Warning
                        ? ESStatusKind.Warning
                        : ESStatusKind.Ready;
                Rect rect = GUILayoutUtility.GetRect(0f, 30f, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                {
                    Color accent = ESEditorPresentation.GetStatusAccent(0, status);
                    EditorGUI.DrawRect(rect, ESEditorPresentation.GetDepthBackground(1));
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), accent);
                    ESEditorPresentation.DrawFrame(rect, ESEditorPresentation.GetStatusFrameColor(0, status));
                }
                GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 8f), ESWorkbench_Status, ESEditorPresentation.MetaStyle);
            }
        }

        protected void ESWorkbench_DrawHero(string title, string subtitle, string phase = null)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 62f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, ESEditorPresentation.GetDepthBackground(0));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), ESEditorPresentation.GetDepthAccent(0));
                ESEditorPresentation.DrawFrame(rect, ESEditorPresentation.GetStatusFrameColor(0, ESStatusKind.None));
            }
            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, Mathf.Max(100f, rect.width - 180f), 24f), title ?? string.Empty, ESEditorPresentation.HeaderStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 34f, Mathf.Max(100f, rect.width - 180f), 18f), subtitle ?? string.Empty, ESEditorPresentation.MetaStyle);
            if (!string.IsNullOrWhiteSpace(phase))
                GUI.Label(new Rect(rect.xMax - 160f, rect.y + 20f, 145f, 22f), phase, ESEditorPresentation.CompactCollectionMetaStyle);
        }

        protected void ESWorkbench_DrawMetric(string label, string value, ESStatusKind status = ESStatusKind.None)
        {
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle, GUILayout.MinWidth(110f), GUILayout.Height(54f)))
            {
                GUILayout.Label(label ?? string.Empty, ESEditorPresentation.MetaStyle);
                Color previous = GUI.color;
                GUI.color = status == ESStatusKind.None ? ESEditorPresentation.SectionTextColor : ESEditorPresentation.GetStatusAccent(0, status);
                GUILayout.Label(value ?? string.Empty, ESEditorPresentation.HeaderStyle);
                GUI.color = previous;
            }
        }

        protected void ESWorkbench_DrawActionButton(string label, string tooltip, Action action, bool enabled = true, bool emphasized = false)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = emphasized ? ESEditorPresentation.SelectionColor : ESEditorPresentation.ToolbarSurfaceColor;
                if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.MinWidth(92f), GUILayout.Height(26f))) action?.Invoke();
                GUI.backgroundColor = previous;
            }
        }

        protected void ESWorkbench_DrawEmptyState(string title, string description, string actionLabel = null, Action action = null)
        {
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                GUILayout.Label(title ?? "暂无内容", ESEditorPresentation.HeaderStyle);
                GUILayout.Label(description ?? string.Empty, ESEditorPresentation.SubtitleStyle);
                if (action != null && !string.IsNullOrWhiteSpace(actionLabel))
                    ESWorkbench_DrawActionButton(actionLabel, description, action, true, true);
            }
        }

        protected void ESWorkbench_DrawSection(string title, string subtitle = null)
        {
            EditorGUILayout.Space(8f);
            GUILayout.Label(title, ESEditorPresentation.HeaderStyle);
            if (!string.IsNullOrWhiteSpace(subtitle))
                GUILayout.Label(subtitle, ESEditorPresentation.MetaStyle);
        }

        protected void ESWorkbench_DrawNavigationButton(string title, bool selected, Action select, string tooltip = null)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = selected ? ESEditorPresentation.SelectionColor : ESEditorPresentation.ToolbarSurfaceColor;
            if (GUILayout.Button(new GUIContent(title, tooltip), ESEditorPresentation.ToolbarButtonStyle, GUILayout.MinWidth(92f), GUILayout.Height(28f))) select?.Invoke();
            GUI.backgroundColor = previous;
        }

        protected void ESWorkbench_DrawAssetHeader(Action create = null)
        {
            using (new EditorGUILayout.HorizontalScope(ESEditorPresentation.ToolbarStyle))
            {
                TAsset selected = (TAsset)EditorGUILayout.ObjectField(ESWorkbench_AssetLabel, ESWorkbench_Asset, typeof(TAsset), false, GUILayout.MinWidth(180f), GUILayout.MaxWidth(420f));
                if (selected != ESWorkbench_Asset) ESWorkbench_BindAsset(selected);
                if (create != null && GUILayout.Button("创建资产", ESEditorPresentation.ToolbarButtonStyle, GUILayout.MinWidth(90f))) create();
                GUILayout.FlexibleSpace();
                GUILayout.Label(ESWorkbench_Status, ESEditorPresentation.MetaStyle, GUILayout.MaxWidth(360f));
            }
        }

        protected bool ESWorkbench_DrawRegisteredDocument()
        {
            for (int i = 0; i < documents.Count; i++)
                if (documents[i].documentId == selectedWorkbenchDocumentId)
                {
                    ESWorkbench_DrawDocumentContent(documents[i]);
                    return true;
                }
            return false;
        }

        private static void ESWorkbench_DrawDocumentContent(ESWorkbenchDocumentDefinition document)
        {
            document.drawHeader?.Invoke();
            document.drawToolbar?.Invoke();
            document.drawCanvas?.Invoke();
            document.draw?.Invoke();
        }

        protected void ESWorkbench_DrawCurrentDocumentInspector()
        {
            for (int i = 0; i < documents.Count; i++)
                if (documents[i].documentId == selectedWorkbenchDocumentId)
                {
                    documents[i].drawInspector?.Invoke();
                    return;
                }
        }

        protected void ESWorkbench_DrawCurrentDocumentPreview()
        {
            for (int i = 0; i < documents.Count; i++)
                if (documents[i].documentId == selectedWorkbenchDocumentId)
                {
                    documents[i].drawPreview?.Invoke();
                    return;
                }
        }

        protected void ESWorkbench_DrawCurrentDocumentDiagnostics()
        {
            for (int i = 0; i < documents.Count; i++)
                if (documents[i].documentId == selectedWorkbenchDocumentId)
                {
                    documents[i].drawDiagnostics?.Invoke();
                    return;
                }
        }

        protected void ESWorkbench_DrawCurrentDocumentFooter()
        {
            for (int i = 0; i < documents.Count; i++)
                if (documents[i].documentId == selectedWorkbenchDocumentId)
                {
                    documents[i].drawFooter?.Invoke();
                    return;
                }
        }

        protected void ESWorkbench_DrawStandardLayout(Action drawInspector, float navigationWidth = 175f, float inspectorWidth = 255f)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width < 1120f ? 150f : navigationWidth)))
                {
                    GUILayout.Label("工作区导航", ESEditorPresentation.HeaderStyle);
                    for (int i = 0; i < ESWorkbench_Documents.Count; i++)
                    {
                        ESWorkbenchDocumentDefinition item = ESWorkbench_Documents[i];
                        if (item.isAvailable != null && !item.isAvailable()) continue;
                        ESWorkbench_DrawNavigationButton(item.title, item.documentId == ESWorkbench_SelectedDocumentId,
                            () => ESWorkbench_SelectDocument(item.documentId), item.tooltip);
                    }
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    standardContentScroll = EditorGUILayout.BeginScrollView(standardContentScroll);
                    ESWorkbench_DrawRegisteredDocument();
                    EditorGUILayout.EndScrollView();
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width < 1120f ? 215f : inspectorWidth)))
                {
                    drawInspector?.Invoke();
                    ESWorkbench_DrawCurrentDocumentInspector();
                    ESWorkbench_DrawCurrentDocumentPreview();
                    ESWorkbench_DrawCurrentDocumentDiagnostics();
                    ESWorkbench_DrawCurrentDocumentFooter();
                }
            }
        }
    }
}
#endif
