#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    [Serializable]
    public sealed class ESWorkbenchLayoutState
    {
        public float leftPaneWidth = 245f;
        public float inspectorPaneWidth = 310f;
        public string activeLeftTab = "objects";
        public string activeDocument = "viewport";
        public string activeViewportId = string.Empty;
        public string activeToolId = string.Empty;
        public string selectedStableId = string.Empty;
        public string selectedKind = string.Empty;
        public string selectedAssetGuid = string.Empty;
        public bool leftPaneVisible = true;
        public bool inspectorPaneVisible = true;
        public string compactSidePane = "inspector";
        public bool responsiveLayoutInitialized;
        public bool hierarchyExpansionInitialized;
        public bool bottomDrawerExpanded = true;
        public float bottomDrawerHeight = 210f;
        public string activeBottomTab = "problems";
        public List<string> expandedHierarchyIds = new List<string>();
        public List<string> hiddenHierarchyIds = new List<string>();
        public List<string> lockedHierarchyIds = new List<string>();
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
    }

    public enum ESWorkbenchViewportKind : byte
    {
        Canvas2D,
        Scene3D,
        Custom
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
        private string activeToolId = string.Empty;

        public string ActiveToolId => activeToolId;
        public event Action<string> Changed;

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
            ESWorkbenchShortcut? shortcut = null)
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
        }

        public string ToolId { get; }
        public string DisplayName { get; }
        public string Tooltip { get; }
        public Texture Icon { get; }
        public int Priority { get; }
        public Action<ESWorkbenchActionContext> Activate { get; }
        public Func<ESWorkbenchActionContext, bool> IsAvailable { get; }
        public ESWorkbenchShortcut? Shortcut { get; }
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
            bool iconOnly = false)
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
            string badge = null)
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
        public ESWorkbenchSelection ToSelection() => new ESWorkbenchSelection(ObjectId, "palette-object", Source, Payload ?? this);
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

    public sealed class ESWorkbenchViewportContext
    {
        internal ESWorkbenchViewportContext(
            EditorWindow window,
            ESWorkbenchActionContext actions,
            string viewportId,
            ESWorkbenchViewportLayoutState layout,
            Func<IReadOnlyList<ESWorkbenchHierarchyDescriptor>> getHierarchy = null,
            Func<string, bool> isHierarchyVisible = null,
            Func<string, bool> isHierarchyLocked = null)
        {
            Window = window;
            Actions = actions;
            ViewportId = viewportId ?? string.Empty;
            Layout = layout ?? new ESWorkbenchViewportLayoutState { viewportId = ViewportId };
            GetHierarchy = getHierarchy;
            IsHierarchyVisible = isHierarchyVisible ?? (_ => true);
            IsHierarchyLocked = isHierarchyLocked ?? (_ => false);
        }

        public EditorWindow Window { get; }
        public ESWorkbenchActionContext Actions { get; }
        public string ViewportId { get; }
        public ESWorkbenchViewportLayoutState Layout { get; }
        public Func<IReadOnlyList<ESWorkbenchHierarchyDescriptor>> GetHierarchy { get; }
        public Func<string, bool> IsHierarchyVisible { get; }
        public Func<string, bool> IsHierarchyLocked { get; }
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

    public interface IESWorkbenchFrameableViewport
    {
        void FrameAll();
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
