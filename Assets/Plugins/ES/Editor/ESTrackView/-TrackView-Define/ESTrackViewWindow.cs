using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using ES;
using ES.EditorInternal;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.UIElements.Cursor;

[ESWindowSleepContract(ESWindowSleepMode.Full, ESWindowSurfaceKind.Workspace)]
public class ESTrackViewWindow : OdinEditorWindow, ES.IESWindowPresentationShortTitle
{
    public string ESWindow_PresentationShortTitle => "轨道";
    internal const string SleepOwnerKey = "ES.TrackView.Window";
    private static readonly Vector2 s_MinWindowSize = new Vector2(600f, 420f);
    private const string LastTimelineGuidPrefKey = "ES.TrackView.LastTimelineGuid";
    private const string LastTimelinePathPrefKey = "ES.TrackView.LastTimelineAssetPath";
    private const string LastTimelineSubAssetNamePrefKey = "ES.TrackView.LastTimelineSubAssetName";
    private const string LastTimelineSubAssetLocalFileIdPrefKey = "ES.TrackView.LastTimelineSubAssetLocalFileId";
    private const string PersistedSelectionPrefix = "ES.TrackView.";
    private const string CursorTimeSuffix = ".CursorTime";
    private const string StartScaleSuffix = ".StartScale";
    private const string EndScaleSuffix = ".EndScale";
    private const double TrackAssetRevisionPollSeconds = 0.25d;
    private const double TrackAssetProjectChangeDebounceSeconds = 0.15d;

    public static ESTrackViewWindow window;
    public static ITrackSequence Sequence { get { if (TrackContainer != null) return TrackContainer.Sequence; return null; } }
    public static IEditorTrackSupport_GetSequence TrackContainer;
    private static byte[] s_CopiedClipData;
    private static Type s_CopiedClipType;
    private static float s_CopiedClipStartTime;
    private static readonly List<CopiedClipPayload> s_CopiedClips = new List<CopiedClipPayload>();
    private bool m_AutoValidationScheduled;
    private IVisualElementScheduledItem m_AutoValidationTask;
    private double m_LastAutoValidationRequestTime;
    private bool m_ViewRefreshScheduled;
    private bool m_ApplyTrackPanelLayoutScheduled;
    private IVisualElementScheduledItem m_ApplyTrackPanelLayoutTask;
    private VisualElement m_ProjectionRoot;
    private bool m_PlaybackContextDirty;
    private bool m_PlaybackContextSaveScheduled;
    private IVisualElementScheduledItem m_PlaybackContextSaveTask;
    private IVisualElementScheduledItem m_InitialScaleTask;
    private bool m_ApplyingPlaybackContext;
    private double m_PlaybackContextNextFlushAt;
    private float m_LastSavedCursorTime;
    private float m_LastSavedStartScale;
    private float m_LastSavedEndScale;
    private readonly HashSet<ESEditorTrackClip> m_SelectedClips = new HashSet<ESEditorTrackClip>();
    private readonly List<ITrackClip> m_ValidationErrorClips = new List<ITrackClip>();
    private int m_ValidationErrorCursor = -1;
    private readonly Dictionary<ESEditorTrackClip, float> m_GroupDragStartTimes = new Dictionary<ESEditorTrackClip, float>();
    private ESEditorTrackClip m_GroupDragAnchor;
    private float m_GroupDragAnchorStartTime;
    private bool m_IsApplyingGroupDrag;

    // 播放器实例
    public static EditorTimelinePlayer Player => EditorTimelinePlayer.Instance;

    private sealed class CopiedClipPayload
    {
        public byte[] data;
        public Type clipType;
        public float startTime;
        public int trackIndex;
    }



    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    [SerializeField, HideInInspector]
    private string m_TrackContainerAssetGuid;

    [SerializeField, HideInInspector]
    private string m_TrackContainerAssetPath;

    [SerializeField, HideInInspector]
    private string m_TrackContainerSubAssetName;

    [SerializeField, HideInInspector]
    private long m_TrackContainerSubAssetLocalFileId;

    // 仅保存编辑器投影的选择位置，业务资产仍是唯一数据权威。
    // Track/Clip 稳定 ID 作为 ReloadDomain 恢复的首选线索，索引只用于旧数据回退。
    [SerializeField, HideInInspector]
    private int m_SelectedTrackIndex = -1;

    [SerializeField, HideInInspector]
    private int m_SelectedClipIndex = -1;

    [SerializeField, HideInInspector]
    private string m_SelectedTrackId = string.Empty;

    [SerializeField, HideInInspector]
    private string m_SelectedClipId = string.Empty;

    [SerializeField, HideInInspector]
    private List<string> m_CollapsedTrackIds = new List<string>();

    private const double AutoSaveDelaySeconds = 1.25d;
    private const double InspectorPreviewRebuildIdleSeconds = 0.16d;
    private bool m_AutoSaveScheduled;
    private double m_AutoSaveDueAt;
    private UnityEngine.Object m_AutoSaveTarget;
    private bool m_UndoRefreshScheduled;
    private bool m_PreviewRebuildScheduled;
    private double m_PreviewRebuildDueAt;
    private UnityEngine.Object m_ObservedTrackAsset;
    private int m_ObservedTrackDirtyCount = int.MinValue;
    private Hash128 m_ObservedTrackDependencyHash;
    private Hash128 m_ObservedTrackContentHash;
    private bool m_HasObservedTrackContentHash;
    private string m_ObservedTrackAssetPath = string.Empty;
    private double m_NextTrackAssetRevisionPollAt;
    private double m_TrackAssetProjectChangeDueAt;
    private bool m_TrackAssetProjectChangePending;
    private bool m_TrackAssetExternalRefreshScheduled;
    private bool m_TrackAssetExternalRefreshDirtyChanged;
    private bool m_TrackAssetExternalRefreshDependencyChanged;
    private bool m_TrackAssetConflictPending;
    private string m_TrackAssetConflictReason = string.Empty;
    private int m_LastTrackThemeGeneration = -1;
    private bool m_LastTrackThemeProSkin;
    // 防止重建投影期间窗口被禁用/重载后，旧的延迟任务继续回写新投影。
    private int m_ProjectionGeneration;

    [SerializeField, Range(180f, 420f)]
    private float m_TrackPanelWidth = DefaultTrackPanelWidth;
    #region  加载滞留

    protected override void OnImGUI()
    {
        base.OnImGUI();
        if (window == null)
        {
            window = this;
        }
    }

    protected override void OnDestroy()
    {
        ESWindowFoundation.Close(this);
        EditorApplication.update -= FlushScheduledViewRefresh;
        EditorApplication.update -= FlushAutoSave;
        EditorApplication.update -= PollTrackContainerRevision;
        EditorApplication.projectChanged -= OnTrackProjectChanged;
        EditorApplication.delayCall -= RefreshAfterUndoRedoDelayed;
        EditorApplication.update -= FlushScheduledPreviewRebuild;
        EditorApplication.delayCall -= RefreshAfterExternalAssetChangeDelayed;
        m_PreviewRebuildScheduled = false;
        m_InitialScaleTask?.Pause();
        m_InitialScaleTask = null;
        m_AutoValidationTask?.Pause();
        m_AutoValidationTask = null;
        m_AutoValidationScheduled = false;
        CancelDeferredTrackLayout();
        ForceFlushPlaybackContextSave();
        ShutdownLiveProjection();
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        EditorApplication.quitting -= OnEditorQuitting;
        FlushAutoSaveImmediate();
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        CleanupTrackPreviewPlayer();
        ESTrackViewWindowHelper.CancelPendingSelectionTrackRefresh();
        DetachProjectionVisuals();
        base.OnDestroy();

    }

    // 失焦是常规编辑流程（例如点击浮动 Inspector、切换 Dock 页签或资产）。
    // 先提交正在进行的手势，再把 Dirty 时间轴立即落盘，避免用户以为切换窗口
    // 已经安全保存却仍停留在内存状态。
    private void OnLostFocus()
    {
        EndTransientInteractions(true);
        FlushAutoSaveImmediate();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        m_ProjectionGeneration++;
        window = this;
        minSize = s_MinWindowSize;
        m_IsInspectorDrawerOpen = m_SerializedInspectorDrawerOpen;
        m_InspectorDrawerClosedByUser = m_SerializedInspectorDrawerClosedByUser;
        Selection.selectionChanged -= OnTrackWindowSelectionChanged;
        Selection.selectionChanged += OnTrackWindowSelectionChanged;
        s_CursorDefault = new Cursor
        {
            texture = EditorGUIUtility.Load("Cursors/d_Cursor_Arrow") as Texture2D,
            hotspot = new Vector2(7, 7)
        };

        s_CursorPan = new Cursor
        {
            texture = EditorGUIUtility.Load("Cursors/d_Cursor_Pan") as Texture2D,
            hotspot = new Vector2(12, 12)
        };

        s_CursorSelect = new Cursor
        {
            texture = EditorGUIUtility.Load("Cursors/d_Cursor_Cross") as Texture2D,
            hotspot = new Vector2(7, 7)
        };

        EditorApplication.delayCall -= RefreshPreselectEntityDelayed;
        EditorApplication.delayCall += RefreshPreselectEntityDelayed;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.quitting -= OnEditorQuitting;
        EditorApplication.quitting += OnEditorQuitting;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        EditorApplication.update -= PollTrackContainerRevision;
        EditorApplication.update += PollTrackContainerRevision;
        EditorApplication.projectChanged -= OnTrackProjectChanged;
        EditorApplication.projectChanged += OnTrackProjectChanged;
        m_NextTrackAssetRevisionPollAt = 0d;
        CaptureTrackContainerRevision(true);
    }

    protected override void OnDisable()
    {
        m_ProjectionGeneration++;
        ESWindowFoundation.Suspend(this);
        UnbindNormalHandles();
        EditorApplication.update -= FlushScheduledViewRefresh;
        EditorApplication.update -= FlushAutoSave;
        EditorApplication.update -= PollTrackContainerRevision;
        EditorApplication.projectChanged -= OnTrackProjectChanged;
        EditorApplication.delayCall -= RefreshAfterUndoRedoDelayed;
        EditorApplication.update -= FlushScheduledPreviewRebuild;
        EditorApplication.delayCall -= RefreshAfterExternalAssetChangeDelayed;
        m_TrackAssetExternalRefreshScheduled = false;
        m_PreviewRebuildScheduled = false;
        m_InitialScaleTask?.Pause();
        m_InitialScaleTask = null;
        m_AutoValidationTask?.Pause();
        m_AutoValidationTask = null;
        CancelDeferredTrackLayout();
        ForceFlushPlaybackContextSave();
        ShutdownLiveProjection();
        m_UndoRefreshScheduled = false;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        EditorApplication.quitting -= OnEditorQuitting;
        FlushAutoSaveImmediate();
        m_AutoValidationScheduled = false;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        CleanupTrackPreviewPlayer();
        ESTrackViewWindowHelper.CancelPendingSelectionTrackRefresh();
        DetachProjectionVisuals();
        base.OnDisable();
        Selection.selectionChanged -= OnTrackWindowSelectionChanged;
        EditorApplication.delayCall -= RefreshPreselectEntityDelayed;
        // OnDisable 也会在域重载、布局重建和窗口暂时停用时触发。
        // 浮动 Inspector 是独立用户窗口，不能因为主窗口短暂失活就被悄悄关闭；
        // 真正销毁主窗口时仍由 OnDestroy 收口。
    }

    private void CleanupTrackPreviewPlayer()
    {
        if (window != this)
            return;

        try
        {
            EditorTimelinePlayer.Instance.ActiveSequence = null;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        window = null;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.EnteredPlayMode)
            return;

        try
        {
            EditorTimelinePlayer.Instance.ActiveSequence = null;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
    protected override void Initialize()
    {
        if (window == null)
        {
            window = this;

        }
        base.Initialize();
    }

    #endregion


    #region  标准参数
    // public float showStart=0;
    // public float   TopRuler.style.left = 0;
    //             TopRuler.style.top = 0;
    //             TopRuler.style.width = 1000;
    public static float TotalTime
    {
        get { return _totaltime; }
        set
        {
            if (_totaltime != value)
                _totaltime = value;
        }
    }

    public static float _totaltime = 10;
    private const float MinSequenceTotalTime = 10f;
    private const float SequenceTailPaddingTime = 0.5f;
    public float startScale = 0;
    public float endScale = 1;
    public float pixelPerSecond = 100;
    public float showScale = 1;
    private const float MinHorizontalScaleSpan = 0.1f;
    public static float standPixelPerSecond => ResolveWidth() / Mathf.Max(TotalTime, 0.01f);
    public float StartShow => startScale * TotalTime;
    internal float CursorTime => cursorTime;

    internal float GetTimeAtCanvasLocalX(float localX)
    {
        if (pixelPerSecond <= 0.0001f)
            return Mathf.Max(0f, cursorTime);

        return Mathf.Max(0f, StartShow + Mathf.Max(0f, localX) / pixelPerSecond);
    }

    public float GetVisibleEndTime()
    {
        float panelWidth = rightPanel != null ? rightPanel.layout.width : 0f;
        if (panelWidth <= 0f && m_ContentContainer != null)
            panelWidth = m_ContentContainer.layout.width;

        if (panelWidth <= 0f || pixelPerSecond <= 0.0001f)
            return float.PositiveInfinity;

        return StartShow + panelWidth / pixelPerSecond;
    }

    public const float totalPixel = 800;
    private const float DefaultTrackPanelWidth = 220f;
    private const float MinTrackPanelWidth = 180f;
    private const float MaxTrackPanelWidth = 420f;
    private const float MinTimelineCanvasWidth = 180f;
    public static float LeftTrackPixel => window != null ? window.m_TrackPanelWidth : DefaultTrackPanelWidth;
    public static float dynamicTargetTotalPixel => window != null && window.rightPanel != null
        ? window.rightPanel.resolvedStyle.width
        : totalPixel;

    public static float ResolveWidth()
    {
        return dynamicTargetTotalPixel;
    }
    #endregion




    #region  标准窗口元素


    public ESTrackRuler ruler;
    public MinMaxSlider horSlider;
    public ScrollView verScroll;

    public VisualElement rightPanel;
    public VisualElement leftPanel;
    private VisualElement m_TimelineWorkspace;
    private TextField m_TrackSearchField;

    public ESTrackCreatorToolbar CreatorToolBar;

    public List<ESEditorTrackItem> Items = new List<ESEditorTrackItem>();

    public ESTrackTimerToolbar toolbar;
    public Entity PreselectEntity { get; private set; }
    public Entity RunningEntity { get; private set; }
    public ESEditorTrackItem SelectedTrackItem { get; private set; }
    public ESEditorTrackClip SelectedClip { get; private set; }
    public ESEditorTrackClip FocusedEditingClip { get; private set; }
    public ESEditorTrackClip RenamingClip { get; private set; }
    public ESEditorTrackItem RenamingTrack { get; private set; }
    private ESEditorTrackItem m_DragSortingTrack;
    private VisualElement m_TrackInsertLine;
    private int m_DragTargetIndex = -1;
    private VisualElement m_TrackPanelSplitter;
    private bool m_IsResizingTrackPanel;
    private int m_TrackPanelResizePointerId = -1;
    private VisualElement m_InspectorPanel;
    private VisualElement m_InspectorSummary;
    private VisualElement m_EmptyStateCard;
    private Button m_InspectorToggleButton;
    private Button m_InspectorSeparateButton;
    private Button m_InspectorHeaderOpenButton;
    private Label m_InspectorTitleLabel;
    private Label m_InspectorTargetLabel;
    private Label m_InspectorTypeLabel;
    private Label m_InspectorStatusBadge;
    private Label m_InspectorHintLabel;
    private Label m_InspectorBodyCaption;
    private Button m_InspectorDetailButton;
    private Button m_InspectorSaveButton;
    private ScrollView m_InspectorScrollView;
    private IMGUIContainer m_InspectorGuiContainer;
    private OdinEditor m_EmbeddedInspectorEditor;
    private VisualGUIDrawerSO m_EmbeddedInspectorDrawer;
    private ESEditorTrackItem m_EmbeddedInspectorTrack;
    private ESEditorTrackClip m_EmbeddedInspectorClip;
    private bool m_IsInspectorDrawerOpen;
    private bool m_InspectorDrawerClosedByUser;
    [SerializeField, HideInInspector]
    private bool m_SerializedInspectorDrawerOpen;
    [SerializeField, HideInInspector]
    private bool m_SerializedInspectorDrawerClosedByUser;

    private enum TrackSaveVisualState
    {
        None,
        Saved,
        Dirty,
        Saving,
        Conflict,
        Failed
    }

    private TrackSaveVisualState m_SaveVisualState = TrackSaveVisualState.None;
    private string m_SaveVisualTooltip = string.Empty;
    private string m_SaveChangeSource = "未记录";
    private string m_SaveFailureReason = string.Empty;
    private double m_LastSaveAt;
    private string m_LastSaveTimeText = string.Empty;

    private VisualElement timeCursor;
    private VisualElement m_TimeCursorLine;
    private VisualElement m_TimeCursorHandle;
    private bool isDraggingCursor = false;

    private float cursorTime = 0f; // 当前游标所在的时间（秒）


    #endregion


    #region  右面板参数

    public enum InteractionMode
    {
        None,           // 无交互
        Panning,        // 平移模式
        Zooming,        // 缩放模式
        Selecting       // 选择模式
    }

    // 控制器状态
    private InteractionMode m_CurrentMode = InteractionMode.None;
    private VisualElement m_ContentContainer => rightPanel;

    // 平移相关
    private Vector2 m_PanStartPosition;
    private bool m_IsPanning = false;

    // 缩放相关
    private float m_ZoomSensitivity = 0.1f;

    // 选择相关
    private Rect m_SelectionRect = Rect.zero;
    private VisualElement m_SelectionVisual;
    private bool m_IsSelecting = false;
    private Vector2 m_SelectionStart = Vector2.zero;
    private bool m_SelectionAdditive;

    #endregion


    #region 初始化核心
    [MenuItem(MenuItemPathDefine.TRACK_EDITOR_WINDOW_PATH, false, 0)]
    [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "轨道编辑器", false, -1000)]
    public static void OpenWindow()
    {
        ESWindowCommandRegistry.RecordOpened("track_editor");
        window = GetWindow<ESTrackViewWindow>();
        window.titleContent = new GUIContent("【轨道】编辑器");
        // OnEnable establishes the minimum size for a newly created window.
        // Do not write minSize here: a content refresh may happen while the
        // existing instance is in ES Presentation's 80x80/32x32 sleep state.
    }

    public static void InitNewSequenceAndOpenWindow()
    {
        // This method is also used by selection-follow and asset-refresh paths.
        // Reusing the live instance avoids GetWindow's focus/show side effects,
        // which would otherwise interrupt an active sleep transition.
        if (window == null)
            OpenWindow();
        else
            window.titleContent = new GUIContent("【轨道】编辑器");
        if (TrackContainer != null)
            window.RememberTrackContainer(TrackContainer);
        //简单更新
        if (TrackContainer == null || Sequence == null)
        {
            window.ResetSequenceViewState();
            window.ClearTrackVisuals();
            window.ShowNoSequenceState();
            window?.toolbar?.UpdateEntity(null, null);
            return;
        }

        if (window.toolbar != null)
        {
            window.toolbar.Name.text = "轴：" + TrackContainer.trackName;
            window.toolbar.Name.tooltip = "当前时间轴。修改会自动保存；也可从“更多”菜单立即保存。";
            window.UpdateSaveStatus("已保存", ESTrackViewTheme.StatusReady, "当前时间轴已保存。");
        }
        window.UpdateEmptyStateVisibility();

        window.ResetSequenceViewState();
        window.SyncTotalTimeFromSequence(Sequence, true);
        //开始重建
        if (ESTrackViewWindow.window.leftPanel == null)
            return;

        window.ClearTrackVisuals();

        if (Sequence != null)
        {
            int unsupportedTrackCount;
            int unsupportedClipCount;
            bool futureSchemaBlocked;
            EnsureSequenceStableTrackIdentity(
                Sequence,
                out unsupportedTrackCount,
                out unsupportedClipCount,
                out futureSchemaBlocked);
            if (futureSchemaBlocked)
            {
                window.ShowNotification(new GUIContent("未来版本时间轴：已阻断自动迁移，避免旧编辑器覆盖新版本数据。"));
            }

            if (unsupportedTrackCount > 0 || unsupportedClipCount > 0)
            {
                string compatibilityKey = "ES.TrackView.IdentityCompatibility." + window.PersistedSelectionScope;
                if (!SessionState.GetBool(compatibilityKey, false))
                {
                    SessionState.SetBool(compatibilityKey, true);
                    window.ShowNotification(new GUIContent("兼容模式：部分轨道/片段未接入稳定身份，ReloadDomain 选择恢复将退回索引。"));
                }

                Debug.LogWarning(
                    "[轨道编辑器] 当前时间轴存在未实现稳定身份的轨道/片段，ReloadDomain 选择恢复将退回索引模式。"
                    + " Track=" + unsupportedTrackCount.ToString(CultureInfo.InvariantCulture)
                    + ", Clip=" + unsupportedClipCount.ToString(CultureInfo.InvariantCulture));
            }
            window.UpdatePreselectEntityFromSelection(false);
            if (Sequence != null)
            {
                var seqPlayer = window.BuildSequencePlayer(Sequence, window.PreselectEntity);
                SetActivePreviewPlayerSafely(seqPlayer);  // 关键
            }
            var protectedBasicTrackKeys = new HashSet<string>();
            foreach (var t in Sequence.Tracks)
            {
                bool isProtectedBasicTrack = ESTrackViewIconUtility.TryGetBasicTrackKey(t, out string basicTrackKey) &&
                                             protectedBasicTrackKeys.Add(basicTrackKey);
                var item = new ESEditorTrackItem().InitWithItem(t, isProtectedBasicTrack);
                ESTrackViewWindow.window.leftPanel.Add(item);
                ESTrackViewWindow.window.Items.Add(item);
            }

            window.ApplyTrackPanelLayout(false);
            window.UpdateTimelineContentHeight();

            int projectionGeneration = window.m_ProjectionGeneration;
            ESEditorHandle.AddSimpleHandleTask(() =>
            {
                if (projectionGeneration != window.m_ProjectionGeneration
                    || window == null
                    || window.leftPanel == null)
                    return;
                foreach (var it in window.Items)
                {
                    it.UpdateNodes();
                }

                window.RestoreSerializedSelection();
                window.ApplyPlaybackContext();

            });
        }





    }

    public void CreateGUI()
    {
        ResetEditorProjectionForRebuild();
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;
        // VisualElements objects can contain other VisualElement following a tree hierarchy.
        // Instantiate UXML



        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        m_ProjectionRoot = labelFromUXML;
        root.Add(labelFromUXML);

        //隐藏特殊资源

        BindElements();
        ESWindowFoundation.BindFullSleep(
            this,
            new ESWindowActionHosts(system: toolbar.SystemActionHost));
        ESWindowFoundation.ResolvePendingSleepOwners(SleepOwnerKey, this);
        ApplyTrackViewTheme();
        FindTrackAssets();
        RestoreRememberedPreviewEntity();
        BindNormalHandles();
        BindButtonsHandles();



        m_InitialScaleTask = root.schedule.Execute(() =>
                  {
                      m_InitialScaleTask = null;
                      if (this == null || m_ProjectionRoot == null)
                          return;
                      HandleStartEndScale(startScale, endScale);
                      ApplyStartEndToUISlider(startScale, endScale);
                  }).StartingIn(100);
        CreateTimeCursor();
        window.timeCursor.BringToFront();

    }

    private void ApplyTrackViewTheme()
    {
        VisualElement root = rootVisualElement;
        if (root == null)
            return;

        int appliedGeneration = ES.EditorInternal.ESEditorPresentation.ThemeGeneration;
        bool appliedProSkin = ES.EditorInternal.ESEditorPresentation.IsProSkin;

        root.style.backgroundColor = ESTrackViewTheme.WindowBackground;

        VisualElement topPart = root.Q<VisualElement>("TopPart");
        if (topPart != null)
            topPart.style.backgroundColor = ESTrackViewTheme.ToolbarBackground;

        VisualElement toolbarDivider = root.Q<VisualElement>("ToolbarDivider");
        if (toolbarDivider != null)
        {
            toolbarDivider.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            toolbarDivider.style.borderBottomWidth = 1f;
            toolbarDivider.style.borderBottomColor = ESTrackViewTheme.Divider;
        }

        if (verScroll != null)
            verScroll.style.backgroundColor = ESTrackViewTheme.WindowBackground;
        if (leftPanel != null)
            leftPanel.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
        if (rightPanel != null)
            rightPanel.style.backgroundColor = ESTrackViewTheme.CanvasBackground;
        if (ruler != null)
            ruler.TopRuler.MarkDirtyRepaint();
        if (m_TimeCursorLine != null)
            m_TimeCursorLine.style.backgroundColor = ESTrackViewTheme.PlayheadAccent;
        if (m_TimeCursorHandle != null)
            m_TimeCursorHandle.style.backgroundColor = ESTrackViewTheme.PlayheadHandle;
        if (m_TrackInsertLine != null)
            m_TrackInsertLine.style.backgroundColor = ESTrackViewTheme.TrackInsertAccent;
        ApplyStaticChromeTheme();
        toolbar?.RefreshTheme();
        CreatorToolBar?.RefreshTheme();
        RefreshSaveStatusThemeColor();
        RefreshInspectorSummary();

        if (Items != null)
        {
            for (int i = 0; i < Items.Count; i++)
                Items[i]?.RefreshTheme();
        }

        root.MarkDirtyRepaint();
        m_LastTrackThemeGeneration = appliedGeneration;
        m_LastTrackThemeProSkin = appliedProSkin;
    }

    private void ApplyStaticChromeTheme()
    {
        if (m_TimelineWorkspace != null)
        {
            m_TimelineWorkspace.style.backgroundColor = ESTrackViewTheme.WindowBackground;
            m_TimelineWorkspace.style.borderTopColor = ESTrackViewTheme.Divider;
        }
        if (rightPanel != null)
        {
            rightPanel.style.backgroundColor = ESTrackViewTheme.CanvasBackground;
            rightPanel.style.borderLeftColor = ESTrackViewTheme.Divider;
        }
        if (leftPanel != null)
        {
            leftPanel.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            leftPanel.style.borderRightColor = ESTrackViewTheme.Divider;
        }
        if (m_TrackPanelSplitter != null)
            m_TrackPanelSplitter.style.backgroundColor = m_IsResizingTrackPanel
                ? ESTrackViewTheme.Accent
                : ESTrackViewTheme.Transparent;
        if (m_SelectionVisual != null)
        {
            Color selection = ESTrackViewTheme.SelectionFrame(true);
            m_SelectionVisual.style.backgroundColor = ESTrackViewTheme.SelectionFill(true);
            m_SelectionVisual.style.borderLeftColor = selection;
            m_SelectionVisual.style.borderTopColor = selection;
            m_SelectionVisual.style.borderRightColor = selection;
            m_SelectionVisual.style.borderBottomColor = selection;
        }

        if (m_TrackSearchField != null)
        {
            m_TrackSearchField.style.color = ESTrackViewTheme.Text;
            m_TrackSearchField.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            Label searchLabel = m_TrackSearchField.Q<Label>();
            if (searchLabel != null)
                searchLabel.style.color = ESTrackViewTheme.MutedText;
            VisualElement searchInput = m_TrackSearchField.Q<VisualElement>(className: "unity-text-input");
            if (searchInput != null)
            {
                searchInput.style.color = ESTrackViewTheme.Text;
                searchInput.style.backgroundColor = ESTrackViewTheme.CanvasBackground;
            }
        }

        if (m_EmptyStateCard != null)
        {
            m_EmptyStateCard.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            m_EmptyStateCard.style.borderLeftColor = ESTrackViewTheme.Accent;
            m_EmptyStateCard.style.borderTopColor = ESTrackViewTheme.Divider;
            m_EmptyStateCard.style.borderRightColor = ESTrackViewTheme.Divider;
            m_EmptyStateCard.style.borderBottomColor = ESTrackViewTheme.Divider;
            Label emptyTitle = m_EmptyStateCard.Q<Label>(className: "track-empty-state-title");
            if (emptyTitle != null)
                emptyTitle.style.color = ESTrackViewTheme.Text;
            m_EmptyStateCard.Query<Label>(className: "track-empty-state-description")
                .ForEach(label => label.style.color = ESTrackViewTheme.MutedText);
            m_EmptyStateCard.Query<Label>(className: "track-empty-state-step")
                .ForEach(label => label.style.color = ESTrackViewTheme.Text);
            m_EmptyStateCard.Query<Button>(className: "track-empty-state-action")
                .ForEach(ESTrackViewTheme.ApplyAccentButton);
        }

        if (m_InspectorPanel == null)
            return;

        m_InspectorPanel.style.backgroundColor = ESTrackViewTheme.CanvasBackground;
        m_InspectorPanel.style.borderLeftColor = ESTrackViewTheme.Accent;
        m_InspectorPanel.style.borderTopColor = ESTrackViewTheme.Divider;
        m_InspectorPanel.style.borderBottomColor = ESTrackViewTheme.Divider;
        VisualElement inspectorHeader = m_InspectorPanel.Q<VisualElement>(className: "track-inspector-header");
        if (inspectorHeader != null)
        {
            inspectorHeader.style.backgroundColor = ESTrackViewTheme.ToolbarBackground;
            inspectorHeader.style.borderBottomColor = ESTrackViewTheme.Divider;
        }
        if (m_InspectorSummary != null)
        {
            m_InspectorSummary.style.backgroundColor = ESTrackViewTheme.InspectorSummarySurface;
            m_InspectorSummary.style.borderLeftColor = ESTrackViewTheme.Accent;
            m_InspectorSummary.style.borderBottomColor = ESTrackViewTheme.Divider;
        }
        if (m_InspectorTitleLabel != null)
            m_InspectorTitleLabel.style.color = ESTrackViewTheme.Text;
        if (m_InspectorTargetLabel != null)
            m_InspectorTargetLabel.style.color = ESTrackViewTheme.SelectedText;
        if (m_InspectorTypeLabel != null)
            m_InspectorTypeLabel.style.color = ESTrackViewTheme.MutedText;
        if (m_InspectorHintLabel != null)
            m_InspectorHintLabel.style.color = ESTrackViewTheme.MutedText;
        if (m_InspectorBodyCaption != null)
        {
            m_InspectorBodyCaption.style.color = ESTrackViewTheme.SelectedText;
            m_InspectorBodyCaption.style.backgroundColor = ESTrackViewTheme.SecondarySurface;
            m_InspectorBodyCaption.style.borderBottomColor = ESTrackViewTheme.Divider;
        }

        m_InspectorPanel.Query<Button>(className: "track-inspector-header-button")
            .ForEach(ESTrackViewTheme.ApplyStandardButton);
        ESTrackViewTheme.ApplyStandardButton(m_InspectorDetailButton);
        ESTrackViewTheme.ApplyAccentButton(m_InspectorHeaderOpenButton);
        ESTrackViewTheme.ApplyStandardButton(m_InspectorToggleButton);
        ESTrackViewTheme.ApplyAccentButton(m_InspectorSeparateButton);
        ESTrackViewTheme.ApplyStandardButton(m_InspectorSaveButton);
        if (m_InspectorSaveButton != null)
        {
            m_InspectorSaveButton.style.color = ESTrackViewTheme.SelectedText;
            m_InspectorSaveButton.style.backgroundColor = ESTrackViewTheme.StateBadgeSurface(ESTrackViewTheme.StatusReady);
            m_InspectorSaveButton.style.borderLeftColor = ESTrackViewTheme.StatusReady;
            m_InspectorSaveButton.style.borderTopColor = ESTrackViewTheme.StatusReady;
        }
    }

    private void RefreshSaveStatusThemeColor()
    {
        if (toolbar?.SaveStatusLabel == null)
            return;

        switch (m_SaveVisualState)
        {
            case TrackSaveVisualState.Saved:
                toolbar.SaveStatusLabel.style.color = ESTrackViewTheme.StatusReady;
                break;
            case TrackSaveVisualState.Dirty:
            case TrackSaveVisualState.Saving:
                toolbar.SaveStatusLabel.style.color = ESTrackViewTheme.StatusModified;
                break;
            case TrackSaveVisualState.Conflict:
                toolbar.SaveStatusLabel.style.color = ESTrackViewTheme.StatusWarning;
                break;
            case TrackSaveVisualState.Failed:
                toolbar.SaveStatusLabel.style.color = ESTrackViewTheme.StatusError;
                break;
            default:
                toolbar.SaveStatusLabel.style.color = ESTrackViewTheme.StatusNeutral;
                break;
        }
    }

    private void RefreshTrackViewThemeIfNeeded()
    {
        int generation = ES.EditorInternal.ESEditorPresentation.ThemeGeneration;
        bool proSkin = ES.EditorInternal.ESEditorPresentation.IsProSkin;
        if (generation == m_LastTrackThemeGeneration && proSkin == m_LastTrackThemeProSkin)
            return;

        ApplyTrackViewTheme();
    }


    private void ResetSequenceViewState()
    {
        EndTransientInteractions(true);
        m_TrackSearchField?.SetValueWithoutNotify(string.Empty);

        if (SelectedTrackItem != null)
            SelectedTrackItem.SetSelected(false);
        SelectedTrackItem = null;

        // 重建投影时保留序列化选择索引，供 ReloadDomain/重开窗口后恢复；
        // 用户主动清空选择仍走默认的 ClearClipSelection() 路径。
        ClearClipSelection(false);
        ClearFocusedEditingClip(null);
        RenamingClip = null;
        RenamingTrack = null;

        if (m_EmbeddedInspectorTrack != null || m_EmbeddedInspectorClip != null)
            ClearEmbeddedInspector();

    }

    private void OnEditorQuitting()
    {
        // Unity 退出时不保证仍会经历完整的窗口销毁链；这里直接冲刷当前 Dirty 资产，
        // 作为自动保存的最后一道 P0 安全网。
        EndTransientInteractions(true);
        FlushAutoSaveImmediate();
        ForceFlushPlaybackContextSave();
    }

    private void ClearTrackVisuals()
    {
        if (leftPanel != null)
        {
            List<VisualElement> elements = leftPanel.Children().ToList();
            for (int i = 0; i < elements.Count; i++)
            {
                VisualElement element = elements[i];
                if (!(element is ESEditorTrackItem))
                    continue;

                element.RemoveFromHierarchy();
                element.userData = null;
            }
        }

        Items.Clear();
        UpdateTimelineContentHeight();
        ScheduleViewRefresh();
    }

    private void ShowNoSequenceState()
    {
        if (toolbar == null)
            return;

        toolbar.Name.text = "轴：未选择";
        toolbar.Name.tooltip = "当前没有打开时间轴。点击“切换时间轴”选择已有资产。";
        UpdateSaveStatus("未选择", ESTrackViewTheme.StatusNeutral, toolbar.Name.tooltip);
        UpdateEmptyStateVisibility();
    }

    internal void UpdateSaveStatus(string text, Color color, string tooltip, string source = null)
    {
        m_SaveVisualState = ResolveSaveVisualState(text);
        m_SaveVisualTooltip = tooltip ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(source))
            m_SaveChangeSource = source.Trim();

        if (m_SaveVisualState == TrackSaveVisualState.Saved)
        {
            m_LastSaveAt = EditorApplication.timeSinceStartup;
            m_LastSaveTimeText = DateTime.Now.ToString("HH:mm:ss");
            m_SaveFailureReason = string.Empty;
        }
        else if (m_SaveVisualState == TrackSaveVisualState.Failed)
        {
            m_SaveFailureReason = m_SaveVisualTooltip;
        }

        string statusTooltip = BuildSaveStatusTooltip(tooltip);
        if (toolbar?.SaveStatusLabel == null)
        {
            RefreshInspectorSummary();
            return;
        }

        toolbar.SaveStatusLabel.text = text;
        toolbar.SaveStatusLabel.style.color = color;
        toolbar.SaveStatusLabel.tooltip = statusTooltip;
        RefreshInspectorSummary();
    }

    private string BuildSaveStatusTooltip(string tooltip)
    {
        StringBuilder builder = new StringBuilder(tooltip ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(m_SaveChangeSource))
            builder.Append("\n来源：").Append(m_SaveChangeSource);
        if (m_LastSaveAt > 0d && m_SaveVisualState != TrackSaveVisualState.Failed)
            builder.Append("\n最近保存：").Append(m_LastSaveTimeText);
        if (m_SaveVisualState == TrackSaveVisualState.Failed && !string.IsNullOrWhiteSpace(m_SaveFailureReason))
            builder.Append("\n失败原因：").Append(m_SaveFailureReason);
        return builder.ToString();
    }

    private static TrackSaveVisualState ResolveSaveVisualState(string text)
    {
        if (string.Equals(text, "外部冲突", StringComparison.Ordinal))
            return TrackSaveVisualState.Conflict;
        if (string.Equals(text, "保存失败", StringComparison.Ordinal))
            return TrackSaveVisualState.Failed;
        if (string.Equals(text, "保存中", StringComparison.Ordinal))
            return TrackSaveVisualState.Saving;
        if (string.Equals(text, "待保存", StringComparison.Ordinal)
            || string.Equals(text, "未保存", StringComparison.Ordinal))
            return TrackSaveVisualState.Dirty;
        if (string.Equals(text, "已保存", StringComparison.Ordinal))
            return TrackSaveVisualState.Saved;
        return TrackSaveVisualState.None;
    }

    private void RememberTrackContainer(IEditorTrackSupport_GetSequence container)
    {
        bool containerChanged = !ReferenceEquals(TrackContainer, container);
        string previousGuid = m_TrackContainerAssetGuid;
        string previousSubAssetName = m_TrackContainerSubAssetName;
        long previousSubAssetLocalFileId = m_TrackContainerSubAssetLocalFileId;
        if (containerChanged)
        {
            EndTransientInteractions(true);
        }
        if (containerChanged && TrackContainer != null)
            ForceFlushPlaybackContextSave();

        if (m_AutoSaveTarget != null && !ReferenceEquals(m_AutoSaveTarget, container as UnityEngine.Object))
            FlushAutoSaveImmediate();

        TrackContainer = container;
        m_TrackContainerAssetGuid = string.Empty;
        m_TrackContainerAssetPath = string.Empty;
        m_TrackContainerSubAssetName = string.Empty;
        m_TrackContainerSubAssetLocalFileId = 0;

        if (container is UnityEngine.Object unityObject)
        {
            string assetPath = AssetDatabase.GetAssetPath(unityObject);
            if (!string.IsNullOrEmpty(assetPath))
            {
                m_TrackContainerAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                m_TrackContainerAssetPath = assetPath;
                m_TrackContainerSubAssetName = GetSubAssetName(unityObject, assetPath);
                if (AssetDatabase.IsSubAsset(unityObject)
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        unityObject,
                        out _,
                        out long localFileId))
                    m_TrackContainerSubAssetLocalFileId = localFileId;
            }
        }

        if (!string.IsNullOrEmpty(m_TrackContainerAssetGuid))
        {
            EditorPrefs.SetString(LastTimelineGuidPrefKey, m_TrackContainerAssetGuid);
            EditorPrefs.SetString(LastTimelinePathPrefKey, m_TrackContainerAssetPath);
            EditorPrefs.SetString(LastTimelineSubAssetNamePrefKey, m_TrackContainerSubAssetName);
            EditorPrefs.SetString(
                LastTimelineSubAssetLocalFileIdPrefKey,
                m_TrackContainerSubAssetLocalFileId.ToString(CultureInfo.InvariantCulture));
        }

        bool sameScope = string.Equals(previousGuid, m_TrackContainerAssetGuid, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(previousSubAssetName, m_TrackContainerSubAssetName, StringComparison.Ordinal)
                         && previousSubAssetLocalFileId == m_TrackContainerSubAssetLocalFileId;
        if (containerChanged && !sameScope)
        {
            m_SelectedTrackIndex = -1;
            m_SelectedClipIndex = -1;
            m_SelectedTrackId = string.Empty;
            m_SelectedClipId = string.Empty;
        }

        if (m_SelectedTrackIndex < 0)
            LoadPersistedSelection();
        LoadPlaybackContext();

        if (containerChanged)
            ClearTrackAssetConflict();
        CaptureTrackContainerRevision(true);
    }

    private void OnTrackProjectChanged()
    {
        if (this == null || window != this)
            return;
        if (TrackContainer == null && string.IsNullOrEmpty(m_TrackContainerAssetGuid))
            return;

        m_TrackAssetProjectChangePending = true;
        m_TrackAssetProjectChangeDueAt = EditorApplication.timeSinceStartup
                                         + TrackAssetProjectChangeDebounceSeconds;
    }

    private void PollTrackContainerRevision()
    {
        if (this == null || window != this)
            return;

        RefreshTrackViewThemeIfNeeded();

        double now = EditorApplication.timeSinceStartup;
        bool includeDependencyHash = m_TrackAssetProjectChangePending
                                     && now >= m_TrackAssetProjectChangeDueAt;
        if (!includeDependencyHash && now < m_NextTrackAssetRevisionPollAt)
            return;

        if (includeDependencyHash)
            m_TrackAssetProjectChangePending = false;
        m_NextTrackAssetRevisionPollAt = now + TrackAssetRevisionPollSeconds;
        SynchronizeTrackContainerRevision(includeDependencyHash);
    }

    private void SynchronizeTrackContainerRevision(bool includeDependencyHash)
    {
        if (TrackContainer == null)
        {
            if (includeDependencyHash)
                TryRestoreExternallyChangedTrackContainer();
            else
                CaptureTrackContainerRevision(false);
            return;
        }

        if (TrackContainer is UnityEngine.Object unityTarget && unityTarget == null)
        {
            HandleTrackContainerUnavailable();
            return;
        }

        UnityEngine.Object target = TrackContainer as UnityEngine.Object;
        if (target == null)
        {
            CaptureTrackContainerRevision(false);
            return;
        }

        string currentPath = AssetDatabase.GetAssetPath(target);
        if (string.IsNullOrEmpty(currentPath))
        {
            HandleTrackContainerUnavailable();
            return;
        }

        if (!ReferenceEquals(m_ObservedTrackAsset, target))
        {
            CaptureTrackContainerRevision(true);
            return;
        }

        int dirtyCount = EditorUtility.GetDirtyCount(target);
        Hash128 dependencyHash = includeDependencyHash
            ? GetTrackContainerDependencyHash(currentPath)
            : m_ObservedTrackDependencyHash;
        Hash128 contentHash = default;
        bool hasContentHash = includeDependencyHash
                              && TryGetTrackContainerContentHash(currentPath, out contentHash);
        bool dirtyChanged = dirtyCount != m_ObservedTrackDirtyCount;
        bool dependencyChanged = includeDependencyHash
                                 && dependencyHash != m_ObservedTrackDependencyHash;
        bool contentChanged = hasContentHash
                              && m_HasObservedTrackContentHash
                              && contentHash != m_ObservedTrackContentHash;
        bool pathChanged = !string.Equals(currentPath, m_ObservedTrackAssetPath, StringComparison.Ordinal);
        if (!dirtyChanged && !dependencyChanged && !contentChanged && !pathChanged)
            return;

        bool hasLocalPendingChanges = ReferenceEquals(m_AutoSaveTarget, target)
                                      || m_AutoSaveScheduled
                                      || m_SaveVisualState == TrackSaveVisualState.Dirty;
        m_ObservedTrackDirtyCount = dirtyCount;
        if (includeDependencyHash)
            m_ObservedTrackDependencyHash = dependencyHash;
        if (hasContentHash)
        {
            m_ObservedTrackContentHash = contentHash;
            m_HasObservedTrackContentHash = true;
        }
        if (pathChanged)
            UpdateRememberedTrackAssetPath(currentPath);
        m_ObservedTrackAssetPath = currentPath;

        if (contentChanged && (hasLocalPendingChanges || EditorUtility.IsDirty(target)))
        {
            EnterTrackAssetConflict(
                "检测到磁盘上的时间轴资产已变化，同时窗口仍有本地未保存内容。自动保存已暂停，请检查后手动决定是否覆盖保存。");
        }

        ScheduleExternalAssetRefresh(dirtyChanged, dependencyChanged || pathChanged);
    }

    private void ScheduleExternalAssetRefresh(bool dirtyChanged, bool dependencyChanged)
    {
        m_TrackAssetExternalRefreshDirtyChanged |= dirtyChanged;
        m_TrackAssetExternalRefreshDependencyChanged |= dependencyChanged;
        if (m_TrackAssetExternalRefreshScheduled)
            return;

        m_TrackAssetExternalRefreshScheduled = true;
        EditorApplication.delayCall -= RefreshAfterExternalAssetChangeDelayed;
        EditorApplication.delayCall += RefreshAfterExternalAssetChangeDelayed;
    }

    private void RefreshAfterExternalAssetChangeDelayed()
    {
        EditorApplication.delayCall -= RefreshAfterExternalAssetChangeDelayed;
        m_TrackAssetExternalRefreshScheduled = false;
        bool dirtyChanged = m_TrackAssetExternalRefreshDirtyChanged;
        bool dependencyChanged = m_TrackAssetExternalRefreshDependencyChanged;
        m_TrackAssetExternalRefreshDirtyChanged = false;
        m_TrackAssetExternalRefreshDependencyChanged = false;
        if (this == null || rootVisualElement == null || window != this)
            return;
        if (TrackContainer is UnityEngine.Object unityTarget && unityTarget == null)
        {
            HandleTrackContainerUnavailable();
            return;
        }

        EndTransientInteractions(false);
        SavePersistedSelection();
        RefreshAfterUndoRedoDelayed();
        CaptureTrackContainerRevision(true);

        UnityEngine.Object target = TrackContainer as UnityEngine.Object;
        if (m_TrackAssetConflictPending)
        {
            UpdateSaveStatus("外部冲突", ESTrackViewTheme.StatusWarning,
                m_TrackAssetConflictReason, "外部资产修改");
        }
        else if (target != null && EditorUtility.IsDirty(target))
        {
            ScheduleAutoSave("外部 Inspector 修改");
        }
        else if (dependencyChanged || dirtyChanged)
        {
            UpdateSaveStatus("已保存", ESTrackViewTheme.StatusReady,
                "已同步时间轴资产的外部修改。", "外部资产修改");
        }
    }

    private void TryRestoreExternallyChangedTrackContainer()
    {
        string assetPath = ResolveTrackContainerAssetPath();
        if (string.IsNullOrEmpty(assetPath))
            return;

        UnityEngine.Object restored = LoadTrackContainerAsset(
            assetPath,
            m_TrackContainerSubAssetName,
            m_TrackContainerSubAssetLocalFileId);
        if (!(restored is IEditorTrackSupport_GetSequence support) || support.Sequence == null)
            return;

        RememberTrackContainer(support);
        InitNewSequenceAndOpenWindow();
        CaptureTrackContainerRevision(true);
        UpdateSaveStatus("已保存", ESTrackViewTheme.StatusReady,
            "时间轴资产已恢复并重新载入。", "外部资产恢复");
    }

    private void HandleTrackContainerUnavailable()
    {
        CancelTrackAutoSaveWithoutWriting();
        TrackContainer = null;
        CaptureTrackContainerRevision(false);
        m_TrackAssetProjectChangePending = true;
        m_TrackAssetProjectChangeDueAt = EditorApplication.timeSinceStartup
                                         + TrackAssetProjectChangeDebounceSeconds;
        ResetSequenceViewState();
        ClearTrackVisuals();
        ShowNoSequenceState();
        toolbar?.UpdateEntity(null, null);
        UpdateSaveStatus("保存失败", ESTrackViewTheme.StatusError,
            "当前时间轴资产已被删除或暂时不可用；窗口已停止自动保存，等待资产恢复或重新选择。",
            "外部资产变更");
        ES.EditorInternal.ESEditorPresentation.PulseWindow(this, ES.EditorInternal.ESStatusKind.Error);
    }

    private void CaptureTrackContainerRevision(bool includeDependencyHash)
    {
        UnityEngine.Object target = TrackContainer as UnityEngine.Object;
        bool targetChanged = !ReferenceEquals(m_ObservedTrackAsset, target);
        m_ObservedTrackAsset = target;
        if (target == null)
        {
            m_ObservedTrackDirtyCount = int.MinValue;
            m_ObservedTrackDependencyHash = default;
            m_ObservedTrackContentHash = default;
            m_HasObservedTrackContentHash = false;
            m_ObservedTrackAssetPath = string.Empty;
            return;
        }

        m_ObservedTrackDirtyCount = EditorUtility.GetDirtyCount(target);
        string assetPath = AssetDatabase.GetAssetPath(target);
        bool pathChanged = !string.Equals(assetPath, m_ObservedTrackAssetPath, StringComparison.Ordinal);
        m_ObservedTrackAssetPath = assetPath ?? string.Empty;
        if (includeDependencyHash || targetChanged || pathChanged)
        {
            m_ObservedTrackDependencyHash = GetTrackContainerDependencyHash(assetPath);
            m_HasObservedTrackContentHash = TryGetTrackContainerContentHash(
                assetPath,
                out m_ObservedTrackContentHash);
        }
    }

    private static Hash128 GetTrackContainerDependencyHash(string assetPath)
    {
        return string.IsNullOrEmpty(assetPath)
            ? default
            : AssetDatabase.GetAssetDependencyHash(assetPath);
    }

    private static bool TryGetTrackContainerContentHash(string assetPath, out Hash128 contentHash)
    {
        contentHash = default;
        if (string.IsNullOrEmpty(assetPath))
            return false;

        string normalizedPath = assetPath.Replace('\\', '/');
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        string absolutePath = Path.IsPathRooted(normalizedPath)
            ? Path.GetFullPath(normalizedPath)
            : string.IsNullOrEmpty(projectRoot)
                ? string.Empty
                : Path.GetFullPath(Path.Combine(projectRoot, normalizedPath));
        if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            return false;

        try
        {
            using (FileStream stream = new FileStream(
                       absolutePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(stream);
                contentHash = new Hash128(
                    BitConverter.ToUInt32(digest, 0),
                    BitConverter.ToUInt32(digest, 4),
                    BitConverter.ToUInt32(digest, 8),
                    BitConverter.ToUInt32(digest, 12));
                return true;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private void UpdateRememberedTrackAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return;

        m_TrackContainerAssetPath = assetPath;
        m_TrackContainerAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
        EditorPrefs.SetString(LastTimelineGuidPrefKey, m_TrackContainerAssetGuid ?? string.Empty);
        EditorPrefs.SetString(LastTimelinePathPrefKey, m_TrackContainerAssetPath);
    }

    private void EnterTrackAssetConflict(string reason)
    {
        m_TrackAssetConflictPending = true;
        m_TrackAssetConflictReason = reason ?? string.Empty;
        CancelTrackAutoSaveWithoutWriting();
        UpdateSaveStatus("外部冲突", ESTrackViewTheme.StatusWarning,
            m_TrackAssetConflictReason, "外部资产修改");
        ES.EditorInternal.ESEditorPresentation.PulseWindow(this, ES.EditorInternal.ESStatusKind.Warning);
    }

    private void ClearTrackAssetConflict()
    {
        m_TrackAssetConflictPending = false;
        m_TrackAssetConflictReason = string.Empty;
    }

    private void CancelTrackAutoSaveWithoutWriting()
    {
        EditorApplication.update -= FlushAutoSave;
        m_AutoSaveScheduled = false;
        m_AutoSaveTarget = null;
    }

    internal bool ConfirmManualSaveWhenExternalConflict()
    {
        if (!m_TrackAssetConflictPending)
            return true;

        bool confirmed = EditorUtility.DisplayDialog(
            "时间轴外部修改冲突",
            m_TrackAssetConflictReason
            + "\n\n继续保存会以当前窗口中的数据写入该资产。请确认已经检查外部修改。",
            "确认覆盖保存",
            "取消");
        if (!confirmed)
            return false;

        return true;
    }

    internal void NotifyTrackAssetSaved()
    {
        ClearTrackAssetConflict();
        CaptureTrackContainerRevision(true);
    }

    private void OnUndoRedoPerformed()
    {
        if (this == null || rootVisualElement == null || window != this)
            return;

        CaptureTrackContainerRevision(false);
        if (m_UndoRefreshScheduled)
            return;

        m_UndoRefreshScheduled = true;
        EditorApplication.delayCall -= RefreshAfterUndoRedoDelayed;
        EditorApplication.delayCall += RefreshAfterUndoRedoDelayed;
    }

    private void RefreshAfterUndoRedoDelayed()
    {
        EditorApplication.delayCall -= RefreshAfterUndoRedoDelayed;
        m_UndoRefreshScheduled = false;
        if (this == null || rootVisualElement == null || window != this)
            return;

        ES.EditorInternal.ESEditorPresentation.PulseWindow(this, ES.EditorInternal.ESStatusKind.Modified);

        ITrackItem selectedTrack = SelectedTrackItem != null ? SelectedTrackItem.item : null;
        ITrackClip primaryClip = SelectedClip != null ? SelectedClip.trackClip : null;
        ITrackItem externalTrack = ESTrackItemTemporaryInspectorWindow.UsingWindow?.CurrentInspectorData as ITrackItem;
        ITrackClip externalClip = ESTrackClipTemporaryInspectorWindow.UsingWindow?.CurrentInspectorData as ITrackClip;
        List<ITrackClip> selectedClips = m_SelectedClips
            .Where(clip => clip != null && clip.trackClip != null)
            .Select(clip => clip.trackClip)
            .ToList();
        EditorSequencePlayer activeSequence = EditorTimelinePlayer.Instance.ActiveSequence;
        float keepTime = activeSequence != null ? activeSequence.CurrentTime : cursorTime;
        float keepSpeed = activeSequence != null ? activeSequence.Speed : 1f;

        if (TrackContainer == null || TrackContainer.Sequence == null)
        {
            FindTrackAssets();
            return;
        }

        bool reuseProjection = IsTrackProjectionInSync();
        if (reuseProjection)
        {
            ITrackItem inspectorTrack = m_EmbeddedInspectorTrack != null ? m_EmbeddedInspectorTrack.item : null;
            ITrackClip inspectorClip = m_EmbeddedInspectorClip != null ? m_EmbeddedInspectorClip.trackClip : null;
            bool revealInspector = m_IsInspectorDrawerOpen;

            if (SelectedTrackItem != null)
                SelectedTrackItem.SetSelected(false);
            SelectedTrackItem = null;
            ClearClipSelection();

            for (int i = 0; i < Items.Count; i++)
                Items[i]?.RefreshProjectionAfterUndoRedo();

            SyncTotalTimeFromSequence(Sequence, false);

            if (selectedTrack != null)
            {
                ESEditorTrackItem restoredTrack = Items.FirstOrDefault(item => item != null && item.item == selectedTrack);
                if (restoredTrack != null)
                    SelectTrack(restoredTrack);
            }

            ESEditorTrackClip reusedPrimary = primaryClip != null ? FindEditorClip(primaryClip) : null;
            if (reusedPrimary != null)
                SelectClip(reusedPrimary, false);

            for (int i = 0; i < selectedClips.Count; i++)
            {
                ESEditorTrackClip restored = FindEditorClip(selectedClips[i]);
                if (restored != null && restored != reusedPrimary)
                    SelectClip(restored, true);
            }

            if (inspectorTrack != null)
            {
                ESEditorTrackItem restoredTrack = Items.FirstOrDefault(item => item != null && item.item == inspectorTrack);
                if (restoredTrack != null)
                    SetTrackInspectorTarget(restoredTrack, revealInspector);
            }
            else if (inspectorClip != null)
            {
                ESEditorTrackClip restoredClip = FindEditorClip(inspectorClip);
                if (restoredClip != null)
                    SetClipInspectorTarget(restoredClip, revealInspector);
            }

            RebuildActivePreviewPlayer();
            if (EditorTimelinePlayer.Instance.ActiveSequence != null)
            {
                EditorTimelinePlayer.Instance.ActiveSequence.Speed = keepSpeed;
                EditorTimelinePlayer.Instance.SetTime(keepTime);
            }

            if (TrackContainer is UnityEngine.Object reusedTarget && EditorUtility.IsDirty(reusedTarget))
                ScheduleAutoSave();

            ForceRefreshClipLayoutNow();
            Repaint();
            return;
        }

        InitNewSequenceAndOpenWindow();

        if (selectedTrack != null)
        {
            ESEditorTrackItem restoredTrack = Items.FirstOrDefault(item => item != null && item.item == selectedTrack);
            if (restoredTrack != null)
                SelectTrack(restoredTrack);
        }

        ESEditorTrackClip restoredPrimary = primaryClip != null ? FindEditorClip(primaryClip) : null;
        if (restoredPrimary != null)
            SelectClip(restoredPrimary, false);

        for (int i = 0; i < selectedClips.Count; i++)
        {
            ESEditorTrackClip restored = FindEditorClip(selectedClips[i]);
            if (restored != null && restored != restoredPrimary)
                SelectClip(restored, true);
        }

        if (externalTrack != null)
        {
            ESEditorTrackItem restoredTrack = Items.FirstOrDefault(item => item != null && item.item == externalTrack);
            if (restoredTrack != null)
                EditTrack(restoredTrack, true);
        }

        if (externalClip != null)
        {
            ESEditorTrackClip restoredClip = FindEditorClip(externalClip);
            if (restoredClip != null)
                EditClip(restoredClip, true);
        }

        if (EditorTimelinePlayer.Instance.ActiveSequence != null)
        {
            EditorTimelinePlayer.Instance.ActiveSequence.Speed = keepSpeed;
            EditorTimelinePlayer.Instance.SetTime(keepTime);
        }

        if (TrackContainer is UnityEngine.Object target && EditorUtility.IsDirty(target))
            ScheduleAutoSave();

        ForceRefreshClipLayoutNow();
        Repaint();
    }

    private bool IsTrackProjectionInSync()
    {
        if (Sequence == null || Sequence.Tracks == null || Items == null)
            return false;

        int trackIndex = 0;
        foreach (ITrackItem sourceTrack in Sequence.Tracks)
        {
            if (sourceTrack == null || sourceTrack.Clips == null || trackIndex >= Items.Count)
                return false;

            ESEditorTrackItem editorTrack = Items[trackIndex];
            if (editorTrack == null || editorTrack.item != sourceTrack || editorTrack.TrackClips == null)
                return false;

            int clipIndex = 0;
            foreach (ITrackClip sourceClip in sourceTrack.Clips)
            {
                if (clipIndex >= editorTrack.TrackClips.Count)
                    return false;

                ESEditorTrackClip editorClip = editorTrack.TrackClips[clipIndex];
                if (editorClip == null || editorClip.trackClip != sourceClip)
                    return false;

                clipIndex++;
            }

            if (clipIndex != editorTrack.TrackClips.Count)
                return false;

            trackIndex++;
        }

        return trackIndex == Items.Count;
    }

    private void EndTransientInteractions(bool commitData)
    {
        if (m_DragSortingTrack != null)
            EndTrackSortDrag(false);

        if (m_GroupDragAnchor != null)
        {
            ESEditorTrackClip anchor = m_GroupDragAnchor;
            if (commitData)
                EndClipGroupDrag(anchor);
            else
            {
                m_GroupDragStartTimes.Clear();
                m_GroupDragAnchor = null;
            }
        }

        m_IsApplyingGroupDrag = false;
        if (m_IsResizingTrackPanel)
            EndTrackPanelResize();

        if (m_IsPanning)
            EndPanning();
        else if (m_IsSelecting)
            EndSelection();
        else
            m_CurrentMode = InteractionMode.None;

        ForceEndCursorDrag();
        if (Items != null)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                ESEditorTrackItem item = Items[i];
                if (item == null || item.TrackClips == null)
                    continue;

                for (int j = 0; j < item.TrackClips.Count; j++)
                    item.TrackClips[j]?.CancelPointerInteraction(commitData);
            }
        }
        ForceFlushPlaybackContextSave();
    }

    private bool TryRestoreTrackContainer()
    {
        string assetPath = ResolveTrackContainerAssetPath();
        if (string.IsNullOrEmpty(assetPath))
            return false;

        UnityEngine.Object asset = LoadTrackContainerAsset(
            assetPath,
            m_TrackContainerSubAssetName,
            m_TrackContainerSubAssetLocalFileId);
        if (asset == null)
        {
            if (m_TrackContainerSubAssetLocalFileId != 0)
                Debug.LogWarning(
                    "[ESTrackView] 无法按 LocalFileId 恢复子资产，已避免静默回退主资产。Path="
                    + assetPath + ", LocalFileId=" + m_TrackContainerSubAssetLocalFileId);
            else if (!string.IsNullOrEmpty(m_TrackContainerSubAssetName))
                Debug.LogWarning(
                    "[ESTrackView] 无法按名称恢复旧版子资产，已避免静默回退主资产。Path="
                    + assetPath + ", SubAsset=" + m_TrackContainerSubAssetName);
            return false;
        }
        if (!(asset is IEditorTrackSupport_GetSequence support) || support.Sequence == null)
            return false;

        RememberTrackContainer(support);
        return true;
    }

    private string ResolveTrackContainerAssetPath()
    {
        if (!string.IsNullOrEmpty(m_TrackContainerAssetGuid))
        {
            string resolvedByGuid = AssetDatabase.GUIDToAssetPath(m_TrackContainerAssetGuid);
            if (!string.IsNullOrEmpty(resolvedByGuid))
            {
                string cachedPath = m_TrackContainerAssetPath;
                if (!string.IsNullOrEmpty(cachedPath)
                    && string.Equals(
                        AssetDatabase.AssetPathToGUID(cachedPath),
                        m_TrackContainerAssetGuid,
                        StringComparison.OrdinalIgnoreCase))
                    return cachedPath;
                return resolvedByGuid;
            }
        }

        return m_TrackContainerAssetPath;
    }

    private static UnityEngine.Object LoadTrackContainerAsset(
        string assetPath,
        string subAssetName,
        long subAssetLocalFileId)
    {
        if (subAssetLocalFileId != 0)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] == null || !AssetDatabase.IsSubAsset(assets[i]))
                    continue;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        assets[i],
                        out _,
                        out long localFileId)
                    && localFileId == subAssetLocalFileId)
                    return assets[i];
            }

            return null;
        }

        if (string.IsNullOrEmpty(subAssetName))
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

        UnityEngine.Object[] legacyAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < legacyAssets.Length; i++)
        {
            if (legacyAssets[i] != null && string.Equals(legacyAssets[i].name, subAssetName, StringComparison.Ordinal))
                return legacyAssets[i];
        }

        return null;
    }

    private static string GetSubAssetName(UnityEngine.Object target, string assetPath)
    {
        if (target == null || string.IsNullOrEmpty(assetPath))
            return string.Empty;

        if (ReferenceEquals(AssetDatabase.LoadMainAssetAtPath(assetPath), target))
            return string.Empty;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] != null && ReferenceEquals(assets[i], target))
                return assets[i].name;
        }

        return string.Empty;
    }

    private void FindTrackAssets()
    {
        if (TrackContainer != null && TrackContainer.Sequence != null)
            return;

        TrackContainer = null;
        string editorPrefsGuid = EditorPrefs.GetString(LastTimelineGuidPrefKey, string.Empty);
        string editorPrefsPath = EditorPrefs.GetString(LastTimelinePathPrefKey, string.Empty);
        string editorPrefsSubAssetName = EditorPrefs.GetString(LastTimelineSubAssetNamePrefKey, string.Empty);
        string editorPrefsSubAssetLocalFileIdText =
            EditorPrefs.GetString(LastTimelineSubAssetLocalFileIdPrefKey, string.Empty);
        bool hasEditorPrefsSubAssetLocalFileId = long.TryParse(
            editorPrefsSubAssetLocalFileIdText,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out long editorPrefsSubAssetLocalFileId);

        if (string.IsNullOrEmpty(m_TrackContainerAssetGuid))
        {
            m_TrackContainerAssetGuid = editorPrefsGuid;
            m_TrackContainerAssetPath = editorPrefsPath;
            m_TrackContainerSubAssetName = editorPrefsSubAssetName;
            if (hasEditorPrefsSubAssetLocalFileId)
                m_TrackContainerSubAssetLocalFileId = editorPrefsSubAssetLocalFileId;
        }
        else if (!string.IsNullOrEmpty(editorPrefsGuid)
                 && string.Equals(
                     editorPrefsGuid,
                     m_TrackContainerAssetGuid,
                     StringComparison.OrdinalIgnoreCase))
        {
            m_TrackContainerAssetPath = editorPrefsPath;
            m_TrackContainerSubAssetName = editorPrefsSubAssetName;
            if (hasEditorPrefsSubAssetLocalFileId)
                m_TrackContainerSubAssetLocalFileId = editorPrefsSubAssetLocalFileId;
        }
        if (TryRestoreTrackContainer())
        {
            InitNewSequenceAndOpenWindow();
            return;
        }

        if (Selection.activeObject is IEditorTrackSupport_GetSequence support &&
        support.Sequence != null)
        {
            RememberTrackContainer(support);
            InitNewSequenceAndOpenWindow();
            return;
        }

        ResetSequenceViewState();
        ClearTrackVisuals();
        ShowNoSequenceState();
    }


    public static void TryUpdateTrackSequence(IEditorTrackSupport_GetSequence newSequenceContainer)
    {
        if (newSequenceContainer != TrackContainer)
        {
            if (window != null)
                window.RememberTrackContainer(newSequenceContainer);
            else
                TrackContainer = newSequenceContainer;
            InitNewSequenceAndOpenWindow();

        }
        else
        {
            window?.RememberTrackContainer(newSequenceContainer);
            InitNewSequenceAndOpenWindow();
        }

        if (window != null)
        {
            if (window.timeCursor != null)
                window.timeCursor.BringToFront();

            window.ForceRefreshClipLayoutNow();
            window.Repaint();
        }

    }
    private void BindButtonsHandles()
    {
        CreatorToolBar.CreateButton.RegisterCallback<ClickEvent>(OnCreatorButtonClickLeft);
    }

    private void BindElements()
    {
        ruler = rootVisualElement.Query<ESTrackRuler>();
        horSlider = rootVisualElement.Query<MinMaxSlider>();
        verScroll = rootVisualElement.Query<ScrollView>();
        rightPanel = rootVisualElement.Query<VisualElement>("DownRightPart");
        leftPanel = rootVisualElement.Query("DownLeftPart");
        m_TimelineWorkspace = rootVisualElement.Query<VisualElement>("DownPart");
        m_TrackPanelSplitter = rootVisualElement.Query<VisualElement>("TrackPanelSplitter");
        m_TrackPanelSplitter.style.cursor = new Cursor
        {
            texture = EditorGUIUtility.Load("Cursors/d_ResizeHorizontal") as Texture2D,
            hotspot = new Vector2(8, 8)
        };


        m_SelectionVisual = rootVisualElement.Query("SeletionContent");
        m_SelectionVisual.pickingMode = PickingMode.Ignore;
        m_SelectionVisual.style.display = DisplayStyle.None;

        CreatorToolBar = rootVisualElement.Query<ESTrackCreatorToolbar>();

        toolbar = rootVisualElement.Query<ESTrackTimerToolbar>();
        EnsureTrackSearchField();
        CreateInspectorPanel();
        CreateEmptyStateCard();
        ApplyTrackPanelLayout(false);
        UpdateInspectorLayout();
        RefreshEntityDisplay();
    }

    private void EnsureTrackSearchField()
    {
        if (m_TrackSearchField == null)
        {
            m_TrackSearchField = new TextField("查找")
            {
                tooltip = "输入轨道名称、类型或片段名称；留空恢复显示全部轨道。"
            };
            m_TrackSearchField.RegisterValueChangedCallback(evt => ApplyTrackFilter(evt.newValue));
            m_TrackSearchField.style.height = 28;
            m_TrackSearchField.style.minHeight = 28;
            m_TrackSearchField.style.flexGrow = 1f;
            m_TrackSearchField.style.flexShrink = 1f;
            m_TrackSearchField.style.maxWidth = 420f;
            m_TrackSearchField.style.marginLeft = 5f;
            m_TrackSearchField.style.marginRight = 5f;
            m_TrackSearchField.style.marginTop = 3f;
            m_TrackSearchField.style.marginBottom = 1f;
        }

        if (m_TrackSearchField.parent != null)
            m_TrackSearchField.RemoveFromHierarchy();

        VisualElement searchParent = toolbar != null ? toolbar.parent : rootVisualElement;
        if (searchParent == null)
            searchParent = rootVisualElement;
        if (toolbar != null && toolbar.parent != null)
            searchParent.Insert(searchParent.IndexOf(toolbar) + 1, m_TrackSearchField);
        else
            searchParent.Add(m_TrackSearchField);
    }

    private void ResetEditorProjectionForRebuild()
    {
        ForceFlushPlaybackContextSave();
        CancelPlaybackContextSave();
        CancelDeferredTrackLayout();
        UnbindNormalHandles();
        DetachProjectionVisuals();

        SelectedTrackItem = null;
        SelectedClip = null;
        m_SelectedClips.Clear();
        FocusedEditingClip = null;
        ReleaseEmbeddedInspectorResources();
        m_EmbeddedInspectorTrack = null;
        m_EmbeddedInspectorClip = null;
        Items.Clear();
        m_ValidationErrorClips.Clear();
    }

    private void ShutdownLiveProjection()
    {
        bool hasLiveProjection = m_ProjectionRoot != null
            && m_ProjectionRoot.panel != null
            && rootVisualElement != null
            && rootVisualElement.panel != null
            && ReferenceEquals(m_ProjectionRoot.panel, rootVisualElement.panel);

        if (!hasLiveProjection)
        {
            Items.Clear();
            m_SelectedClips.Clear();
            SelectedTrackItem = null;
            SelectedClip = null;
            FocusedEditingClip = null;
            ReleaseEmbeddedInspectorResources();
            m_EmbeddedInspectorTrack = null;
            m_EmbeddedInspectorClip = null;
            return;
        }

        EndTransientInteractions(true);
        ClearFocusedEditingClip(null);
        ClearEmbeddedInspector();
    }

    private void DetachProjectionVisuals()
    {
        if (m_ProjectionRoot != null)
            m_ProjectionRoot.RemoveFromHierarchy();
        m_ProjectionRoot = null;

        if (m_InspectorPanel != null)
            m_InspectorPanel.RemoveFromHierarchy();
        m_InspectorPanel = null;

        if (m_EmptyStateCard != null)
            m_EmptyStateCard.RemoveFromHierarchy();
        m_EmptyStateCard = null;
    }

    private void CancelDeferredTrackLayout()
    {
        m_ApplyTrackPanelLayoutTask?.Pause();
        m_ApplyTrackPanelLayoutTask = null;
        m_ApplyTrackPanelLayoutScheduled = false;
    }

    private void CreateEmptyStateCard()
    {
        if (m_EmptyStateCard != null)
            return;

        m_EmptyStateCard = new VisualElement { name = "TrackEmptyStateCard" };
        m_EmptyStateCard.AddToClassList("track-empty-state-card");

        Label title = new Label("开始制作技能时间轴");
        title.AddToClassList("track-empty-state-title");
        m_EmptyStateCard.Add(title);

        Label description = new Label("按下面四步完成一次可预览、可保存的技能编排：");
        description.AddToClassList("track-empty-state-description");
        m_EmptyStateCard.Add(description);

        Button createSkillButton = new Button(ESCreateSkillWindow.Open)
        {
            text = "新建技能"
        };
        createSkillButton.AddToClassList("track-empty-state-action");
        createSkillButton.tooltip = "创建独立技能，或放入技能 Group；需要输入键名。";

        Button chooseButton = null;
        chooseButton = new Button(() =>
        {
            toolbar?.OpenTimelineSelectionMenu(chooseButton);
        })
        {
            text = "选择时间轴"
        };
        chooseButton.AddToClassList("track-empty-state-action");
        chooseButton.tooltip = "打开时间轴选择菜单；不会扫描或修改资产。";

        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.alignItems = Align.Center;
        actionsRow.style.marginTop = 6f;
        actionsRow.Add(createSkillButton);
        actionsRow.Add(chooseButton);
        m_EmptyStateCard.Add(actionsRow);

        string[] steps =
        {
            "1. 添加轨道，再添加片段",
            "2. 拖动片段调整时序和时长",
            "3. 在右侧 Inspector 填写业务内容"
        };
        for (int i = 0; i < steps.Length; i++)
        {
            Label step = new Label(steps[i]);
            step.AddToClassList("track-empty-state-step");
            m_EmptyStateCard.Add(step);
        }
        // 空状态属于时间轴工作区，不应覆盖顶部工具栏，也不应随根窗口百分比漂移。
        (m_TimelineWorkspace ?? rootVisualElement).Add(m_EmptyStateCard);
        m_EmptyStateCard.BringToFront();
        UpdateEmptyStateVisibility();
    }

    private void UpdateEmptyStateVisibility()
    {
        if (m_EmptyStateCard == null)
            return;

        bool empty = TrackContainer == null
            || Sequence == null
            || Sequence.Tracks == null
            || !Sequence.Tracks.Any(track => track != null);
        m_EmptyStateCard.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
        if (empty)
        {
            UpdateEmptyStateLayout();
            m_EmptyStateCard.BringToFront();
        }
    }

    private void UpdateEmptyStateLayout()
    {
        if (m_EmptyStateCard == null || m_TimelineWorkspace == null)
            return;

        float workspaceWidth = m_TimelineWorkspace.resolvedStyle.width;
        float workspaceHeight = m_TimelineWorkspace.resolvedStyle.height;
        if (workspaceWidth <= 0f || workspaceHeight <= 0f)
            return;

        float inspectorReserve = 0f;
        if (m_InspectorPanel != null && m_InspectorPanel.resolvedStyle.display == DisplayStyle.Flex)
            inspectorReserve = Mathf.Max(0f, m_InspectorPanel.resolvedStyle.width);

        // 600×420 与高 DPI 下，轨道面板会吃掉大部分横向空间；空状态卡必须
        // 服从实际视口，不得用 300px 固定最小宽度越过画布边界。
        float minCardWidth = workspaceWidth < 720f ? 220f : 300f;
        float left = Mathf.Clamp(m_TrackPanelWidth + 24f, 18f, Mathf.Max(18f, workspaceWidth - minCardWidth - inspectorReserve - 24f));
        float right = Mathf.Clamp(inspectorReserve + 24f, 18f, Mathf.Max(18f, workspaceWidth - left - minCardWidth));
        float available = Mathf.Max(minCardWidth, workspaceWidth - left - right);
        float cardWidth = Mathf.Clamp(available, minCardWidth, 520f);
        if (left + cardWidth > workspaceWidth - right)
            left = Mathf.Max(12f, workspaceWidth - right - cardWidth);

        m_EmptyStateCard.style.left = left;
        m_EmptyStateCard.style.right = StyleKeyword.Auto;
        m_EmptyStateCard.style.width = cardWidth;
        m_EmptyStateCard.style.minWidth = minCardWidth;
        m_EmptyStateCard.style.maxWidth = 520f;
        m_EmptyStateCard.style.top = Mathf.Clamp((workspaceHeight - 240f) * 0.35f, 28f, 120f);
    }

    private void CreateInspectorPanel()
    {
        if (m_InspectorPanel != null)
            return;

        m_InspectorToggleButton = new Button(ToggleInspectorDrawer)
        {
            text = "属性",
            tooltip = "显示或隐藏当前轨道/片段的属性。窗口较窄时将在独立窗口中编辑。"
        };
        m_InspectorToggleButton.AddToClassList("track-inspector-toggle");
        rootVisualElement.Add(m_InspectorToggleButton);

        // 抽屉关闭或窄窗口时仍保留独立编辑器入口，避免“弹出”功能被隐藏在不可见面板中。
        m_InspectorSeparateButton = new Button(OpenCurrentInspectorInSeparateWindow)
        {
            text = "弹出编辑器",
            tooltip = "在独立窗口中编辑当前选中的轨道或片段；也可 Shift + 右键直接弹出。"
        };
        m_InspectorSeparateButton.AddToClassList("track-inspector-separate-toggle");
        rootVisualElement.Add(m_InspectorSeparateButton);

        m_InspectorPanel = new VisualElement { name = "TrackInspectorPanel" };
        m_InspectorPanel.AddToClassList("track-inspector-panel");

        m_InspectorSummary = new VisualElement { name = "TrackInspectorSummary" };
        m_InspectorSummary.AddToClassList("track-inspector-summary");

        VisualElement summaryTitleRow = new VisualElement();
        summaryTitleRow.AddToClassList("track-inspector-summary-title-row");
        summaryTitleRow.style.flexDirection = FlexDirection.Row;
        summaryTitleRow.style.alignItems = Align.Center;
        summaryTitleRow.style.flexWrap = Wrap.NoWrap;
        summaryTitleRow.style.minHeight = 24f;
        m_InspectorTargetLabel = new Label("未选择轨道或片段");
        m_InspectorTargetLabel.AddToClassList("track-inspector-target");
        summaryTitleRow.Add(m_InspectorTargetLabel);
        m_InspectorTypeLabel = new Label("等待选择");
        m_InspectorTypeLabel.AddToClassList("track-inspector-type");
        m_InspectorTypeLabel.style.flexShrink = 1f;
        m_InspectorTypeLabel.style.minWidth = 0f;
        m_InspectorTypeLabel.style.whiteSpace = WhiteSpace.NoWrap;
        m_InspectorTypeLabel.style.overflow = Overflow.Hidden;
        m_InspectorTypeLabel.style.textOverflow = TextOverflow.Ellipsis;
        summaryTitleRow.Add(m_InspectorTypeLabel);
        m_InspectorSummary.Add(summaryTitleRow);

        VisualElement summaryStatusRow = new VisualElement();
        summaryStatusRow.AddToClassList("track-inspector-summary-status-row");
        summaryStatusRow.style.flexDirection = FlexDirection.Row;
        summaryStatusRow.style.alignItems = Align.Center;
        summaryStatusRow.style.flexWrap = Wrap.NoWrap;
        m_InspectorStatusBadge = new Label("未选择");
        m_InspectorStatusBadge.AddToClassList("track-inspector-status-badge");
        summaryStatusRow.Add(m_InspectorStatusBadge);
        m_InspectorHintLabel = new Label("选择轨道或片段后，在此处编辑业务内容。");
        m_InspectorHintLabel.AddToClassList("track-inspector-hint");
        summaryStatusRow.Add(m_InspectorHintLabel);
        m_InspectorDetailButton = new Button(CopyInspectorDetails)
        {
            text = "复制",
            tooltip = "复制当前状态、保存来源和完整校验/失败信息，不受界面截断影响。"
        };
        m_InspectorDetailButton.style.flexGrow = 0f;
        m_InspectorDetailButton.style.flexShrink = 0f;
        m_InspectorDetailButton.style.width = 54f;
        m_InspectorDetailButton.style.maxWidth = 54f;
        m_InspectorDetailButton.AddToClassList("track-inspector-detail-button");
        summaryStatusRow.Add(m_InspectorDetailButton);
        m_InspectorSummary.Add(summaryStatusRow);
        m_InspectorPanel.Add(m_InspectorSummary);

        VisualElement header = new VisualElement();
        header.AddToClassList("track-inspector-header");
        header.style.flexDirection = FlexDirection.Column;
        header.style.alignItems = Align.Stretch;
        header.style.minHeight = 62f;
        header.style.paddingTop = 5f;
        header.style.paddingBottom = 5f;

        VisualElement headerTitleRow = new VisualElement();
        headerTitleRow.style.flexDirection = FlexDirection.Row;
        headerTitleRow.style.alignItems = Align.Center;
        headerTitleRow.style.flexGrow = 1f;
        headerTitleRow.style.minHeight = 24f;
        m_InspectorTitleLabel = new Label("属性检查器");
        m_InspectorTitleLabel.AddToClassList("track-inspector-title");
        headerTitleRow.Add(m_InspectorTitleLabel);
        header.Add(headerTitleRow);

        VisualElement headerActions = new VisualElement();
        headerActions.style.flexDirection = FlexDirection.Row;
        headerActions.style.alignItems = Align.Center;
        headerActions.style.justifyContent = Justify.FlexEnd;
        headerActions.style.minHeight = 24f;
        headerActions.style.flexShrink = 0f;
        headerActions.AddToClassList("track-inspector-header-actions");

        m_InspectorHeaderOpenButton = new Button(OpenCurrentInspectorInSeparateWindow)
        {
            text = "弹出",
            tooltip = "在独立窗口中编辑当前属性。"
        };
        m_InspectorHeaderOpenButton.style.flexGrow = 0f;
        m_InspectorHeaderOpenButton.style.flexShrink = 0f;
        m_InspectorHeaderOpenButton.style.width = 52f;
        m_InspectorHeaderOpenButton.style.maxWidth = 52f;
        m_InspectorHeaderOpenButton.AddToClassList("track-inspector-header-button");
        headerActions.Add(m_InspectorHeaderOpenButton);

        m_InspectorSaveButton = new Button(ESTrackViewWindowHelper.SaveContainerNow)
        {
            text = "保存",
            tooltip = "立即写入当前时间轴；失败时可从这里重试。"
        };
        m_InspectorSaveButton.style.flexGrow = 0f;
        m_InspectorSaveButton.style.flexShrink = 0f;
        m_InspectorSaveButton.style.width = 58f;
        m_InspectorSaveButton.style.maxWidth = 58f;
        m_InspectorSaveButton.AddToClassList("track-inspector-save-button");
        headerActions.Add(m_InspectorSaveButton);

        Button closeButton = new Button(CloseInspectorDrawer)
        {
            text = "关闭",
            tooltip = "隐藏属性检查器。"
        };
        closeButton.style.flexGrow = 0f;
        closeButton.style.flexShrink = 0f;
        closeButton.style.width = 52f;
        closeButton.style.maxWidth = 52f;
        closeButton.AddToClassList("track-inspector-header-button");
        headerActions.Add(closeButton);
        header.Add(headerActions);
        m_InspectorPanel.Add(header);

        m_InspectorBodyCaption = new Label("业务字段");
        m_InspectorBodyCaption.AddToClassList("track-inspector-body-caption");
        m_InspectorBodyCaption.tooltip = "这里编辑当前轨道或片段的业务参数；时间位置和时长可直接在时间轴上调整。";
        m_InspectorPanel.Add(m_InspectorBodyCaption);

        // 内置 Inspector 只保留一个 UI Toolkit 竖向滚动容器。
        // Odin/IMGUI 不再自行创建 ScrollView，避免横向滚动和嵌套滚动互相抢事件。
        m_InspectorScrollView = new ScrollView(ScrollViewMode.Vertical)
        {
            name = "TrackInspectorScrollView"
        };
        m_InspectorScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        m_InspectorScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        m_InspectorScrollView.style.flexGrow = 1f;
        m_InspectorScrollView.style.flexShrink = 1f;
        m_InspectorScrollView.style.overflow = Overflow.Hidden;
        m_InspectorScrollView.contentContainer.style.flexGrow = 1f;
        m_InspectorScrollView.contentContainer.style.paddingLeft = 8f;
        m_InspectorScrollView.contentContainer.style.paddingRight = 10f;
        m_InspectorScrollView.contentContainer.style.paddingTop = 6f;
        m_InspectorScrollView.contentContainer.style.paddingBottom = 10f;

        m_InspectorGuiContainer = new IMGUIContainer(DrawEmbeddedInspector);
        m_InspectorGuiContainer.AddToClassList("track-inspector-content");
        m_InspectorGuiContainer.style.flexGrow = 1f;
        m_InspectorGuiContainer.style.flexShrink = 1f;
        m_InspectorGuiContainer.style.minWidth = 0f;
        m_InspectorGuiContainer.style.width = Length.Percent(100f);
        m_InspectorScrollView.Add(m_InspectorGuiContainer);
        m_InspectorPanel.Add(m_InspectorScrollView);
        rootVisualElement.Add(m_InspectorPanel);
        m_InspectorPanel.BringToFront();
        RefreshInspectorSummary();
    }

    private void RefreshInspectorSummary()
    {
        if (m_InspectorSummary == null || m_InspectorTargetLabel == null)
            return;

        bool hasTrack = m_EmbeddedInspectorTrack != null && m_EmbeddedInspectorTrack.item != null;
        bool hasClip = m_EmbeddedInspectorClip != null && m_EmbeddedInspectorClip.trackClip != null;
        if (!hasTrack && !hasClip)
        {
            m_InspectorTargetLabel.text = "未选择轨道或片段";
            m_InspectorTypeLabel.text = "等待选择";
            SetInspectorSemanticStatus("未选择", ESStatusKind.Empty);
            m_InspectorHintLabel.text = "选择轨道或片段后，在此处编辑业务内容。";
            UpdateInspectorDetailAction(false);
            UpdateInspectorSaveAction(false);
            return;
        }

        object target = hasClip ? m_EmbeddedInspectorClip.trackClip : m_EmbeddedInspectorTrack.item;
        string displayName = hasClip
            ? m_EmbeddedInspectorClip.trackClip.DisplayName
            : m_EmbeddedInspectorTrack.item.DisplayName;
        string kind = hasClip ? "片段" : "轨道";
        m_InspectorTargetLabel.text = string.IsNullOrEmpty(displayName) ? kind : kind + " · " + displayName;
        m_InspectorTypeLabel.text = target != null ? target.GetType()._GetTypeDisplayName() : "未知类型";

        bool dirty = TrackContainer is UnityEngine.Object container && EditorUtility.IsDirty(container);
        bool hasValidation = m_ValidationErrorClips.Count > 0;
        if (m_SaveVisualState == TrackSaveVisualState.Conflict)
        {
            SetInspectorSemanticStatus("外部冲突", ESStatusKind.Warning);
            m_InspectorHintLabel.text = string.IsNullOrEmpty(m_TrackAssetConflictReason)
                ? "检测到磁盘上的外部修改，自动保存已暂停。请检查后手动决定是否覆盖保存。"
                : m_TrackAssetConflictReason;
        }
        else if (m_SaveVisualState == TrackSaveVisualState.Failed)
        {
            SetInspectorSemanticStatus("保存失败", ESStatusKind.Error);
            m_InspectorHintLabel.text = string.IsNullOrEmpty(m_SaveVisualTooltip)
                ? "保存失败，请从“更多”菜单重试立即保存。"
                : m_SaveVisualTooltip;
        }
        else if (m_SaveVisualState == TrackSaveVisualState.Saving)
        {
            SetInspectorSemanticStatus("保存中", ESStatusKind.Modified);
            m_InspectorHintLabel.text = "正在写入当前时间轴，请稍候。来源：" + m_SaveChangeSource;
        }
        else if (dirty || m_SaveVisualState == TrackSaveVisualState.Dirty)
        {
            SetInspectorSemanticStatus("待保存", ESStatusKind.Modified);
            m_InspectorHintLabel.text = "当前修改会自动保存，也可从“更多”菜单立即保存。来源：" + m_SaveChangeSource;
        }
        else if (hasValidation)
        {
            SetInspectorSemanticStatus("有校验问题", ESStatusKind.Warning);
            m_InspectorHintLabel.text = "请先处理时间轴校验问题，再交给预览或运行时。";
        }
        else
        {
            SetInspectorSemanticStatus("已保存", ESStatusKind.Ready);
            m_InspectorHintLabel.text = "可直接修改字段；失焦后会同步预览并自动保存。";
        }

        // 选中目标后始终保留详情入口，避免校验缓存尚未建立时用户找不到完整信息出口。
        UpdateInspectorDetailAction(true);
        UpdateInspectorSaveAction(dirty
                                  || m_SaveVisualState == TrackSaveVisualState.Failed
                                  || m_SaveVisualState == TrackSaveVisualState.Conflict);
    }

    private void UpdateInspectorSaveAction(bool visible)
    {
        if (m_InspectorSaveButton == null)
            return;

        m_InspectorSaveButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        m_InspectorSaveButton.SetEnabled(visible && TrackContainer != null && Sequence != null);
        m_InspectorSaveButton.tooltip = visible
            ? "立即写入当前时间轴；失败时可从这里重试。"
            : "当前时间轴没有待保存修改。";
    }

    private void UpdateInspectorDetailAction(bool visible)
    {
        if (m_InspectorDetailButton == null)
            return;

        m_InspectorDetailButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        m_InspectorDetailButton.SetEnabled(visible);
        m_InspectorDetailButton.tooltip = visible
            ? "复制当前状态、保存来源和完整校验/失败信息，不受界面截断影响。"
            : "当前没有需要展开的状态详情。";
    }

    private void CopyInspectorDetails()
    {
        string details = BuildInspectorDetails();
        if (string.IsNullOrWhiteSpace(details))
            return;

        EditorGUIUtility.systemCopyBuffer = details;
        ShowNotification(new GUIContent("已复制 Inspector 完整详情"));
    }

    private string BuildInspectorDetails()
    {
        StringBuilder builder = new StringBuilder(1024);
        builder.AppendLine("ES TrackWindow Inspector 状态详情");
        builder.Append("资产：").AppendLine((TrackContainer as UnityEngine.Object)?.name ?? "<未选择>");
        builder.Append("目标：").AppendLine(m_InspectorTargetLabel != null ? m_InspectorTargetLabel.text : "<未选择>");
        builder.Append("状态：").AppendLine(m_InspectorStatusBadge != null ? m_InspectorStatusBadge.text : "未知");
        if (!string.IsNullOrWhiteSpace(m_SaveChangeSource))
            builder.Append("修改来源：").AppendLine(m_SaveChangeSource);
        if (!string.IsNullOrWhiteSpace(m_SaveVisualTooltip))
            builder.Append("保存信息：").AppendLine(m_SaveVisualTooltip);
        if (!string.IsNullOrWhiteSpace(m_SaveFailureReason))
            builder.Append("失败原因：").AppendLine(m_SaveFailureReason);

        List<string> warnings = new List<string>(32);
        List<string> infos = new List<string>(16);
        Dictionary<ITrackClip, string> clipWarnings = new Dictionary<ITrackClip, string>();
        ValidateSequence(Sequence, warnings, infos, clipWarnings);
        builder.Append("校验警告数量：").AppendLine(warnings.Count.ToString(CultureInfo.InvariantCulture));
        builder.Append("校验提示数量：").AppendLine(infos.Count.ToString(CultureInfo.InvariantCulture));
        if (warnings.Count > 0)
        {
            builder.AppendLine("校验警告明细：");
            for (int i = 0; i < warnings.Count; i++)
                builder.Append("- ").AppendLine(warnings[i]);
        }
        if (infos.Count > 0)
        {
            builder.AppendLine("校验提示明细：");
            for (int i = 0; i < infos.Count; i++)
                builder.Append("- ").AppendLine(infos[i]);
        }

        return builder.ToString();
    }

    private void SetInspectorStatus(string text, Color foreground, Color background)
    {
        if (m_InspectorStatusBadge == null)
            return;

        m_InspectorStatusBadge.text = text;
        m_InspectorStatusBadge.style.color = foreground;
        m_InspectorStatusBadge.style.backgroundColor = background;
        m_InspectorStatusBadge.style.borderLeftColor = foreground;
        m_InspectorStatusBadge.style.borderTopColor = ESTrackViewTheme.WithAlpha(foreground, 0.58f);
        m_InspectorStatusBadge.style.borderRightColor = ESTrackViewTheme.WithAlpha(foreground, 0.38f);
        m_InspectorStatusBadge.style.borderBottomColor = ESTrackViewTheme.WithAlpha(foreground, 0.38f);
    }

    private void SetInspectorSemanticStatus(string text, ESStatusKind status)
    {
        Color foreground = ESEditorPresentation.GetStatusAccent(0, status);
        Color background = Color.Lerp(
            ESEditorPresentation.GetDepthBackground(1), foreground,
            ESEditorPresentation.IsProSkin ? 0.16f : 0.10f);
        SetInspectorStatus(text, foreground, background);
    }

    private void UpdateInspectorLayout()
    {
        if (m_InspectorPanel == null || m_InspectorToggleButton == null || verScroll == null)
            return;

        // 抽屉必须同时容纳轨道列表、时间轴画布和 Inspector。
        // 由实际最小布局预算推导阈值，避免 720~999px 区间覆盖画布。
        const float inspectorDrawerWidth = 320f;
        const float timelineCanvasSafetyWidth = 480f;
        float dockedInspectorMinimumWidth = MinTrackPanelWidth + timelineCanvasSafetyWidth + inspectorDrawerWidth;
        float drawerInspectorMinimumWidth = dockedInspectorMinimumWidth;
        float width = rootVisualElement.layout.width;
        bool docked = width >= dockedInspectorMinimumWidth;
        bool canUseDrawer = width >= drawerInspectorMinimumWidth;
        float inspectorWidth = Mathf.Clamp(width * 0.3f, 300f, 320f);

        // Inspector 抽屉锚定到真实时间轴视口，而不是假设顶部永远是 48px、底部永远是 20px。
        // 这样工具栏高度、滚动条、字体缩放或高 DPI 变化时不会覆盖时间轴内容。
        Rect viewportRect = verScroll.layout;
        float rootHeight = rootVisualElement.layout.height;
        if (viewportRect.height <= 0f)
            viewportRect = new Rect(0f, 0f, rootVisualElement.layout.width, rootHeight);
        float panelTop = Mathf.Max(0f, viewportRect.y);
        float panelBottom = Mathf.Max(0f, rootHeight - viewportRect.yMax);
        m_InspectorPanel.style.top = panelTop;
        m_InspectorPanel.style.bottom = panelBottom;
        m_InspectorPanel.style.height = Mathf.Max(0f, rootHeight - panelTop - panelBottom);
        m_InspectorPanel.style.position = Position.Absolute;
        m_InspectorPanel.style.left = StyleKeyword.Auto;
        m_InspectorPanel.style.right = 0f;

        bool hasTarget = TryResolveInspectorTarget(out _, out _);
        if (!hasTarget)
        {
            ReleaseEmbeddedInspectorResources();
            m_EmbeddedInspectorTrack = null;
            m_EmbeddedInspectorClip = null;
        }
        toolbar?.UpdateInspectorAction(hasTarget);
        bool hasEmbeddedTarget = m_EmbeddedInspectorDrawer != null && m_EmbeddedInspectorEditor != null;
        if (docked && hasEmbeddedTarget && !m_InspectorDrawerClosedByUser)
        {
            m_IsInspectorDrawerOpen = true;
            m_SerializedInspectorDrawerOpen = true;
        }

        bool showPanel = canUseDrawer && (hasEmbeddedTarget ? m_IsInspectorDrawerOpen : !hasTarget);
        // 非全宽窗口打开抽屉时也必须给时间轴让出空间，不能让 Inspector 覆盖轨道内容。
        verScroll.style.marginRight = showPanel ? inspectorWidth : 0f;
        m_InspectorPanel.style.display = showPanel ? DisplayStyle.Flex : DisplayStyle.None;
        m_InspectorPanel.style.width = inspectorWidth;
        m_InspectorPanel.style.minWidth = inspectorWidth;
        m_InspectorPanel.style.maxWidth = inspectorWidth;
        m_InspectorPanel.style.overflow = Overflow.Hidden;
        m_InspectorToggleButton.style.display = canUseDrawer && !showPanel ? DisplayStyle.Flex : DisplayStyle.None;
        bool showSeparateEntry = !showPanel && hasTarget;
        m_InspectorToggleButton.SetEnabled(hasTarget);
        m_InspectorToggleButton.tooltip = hasTarget
            ? "显示当前轨道/片段的属性检查器。"
            : "请先选择轨道或片段。";
        if (m_InspectorSeparateButton != null)
        {
            m_InspectorSeparateButton.style.display = showSeparateEntry ? DisplayStyle.Flex : DisplayStyle.None;
            m_InspectorSeparateButton.SetEnabled(hasTarget);
            m_InspectorSeparateButton.tooltip = hasTarget
                ? "在独立窗口中编辑当前选中的轨道或片段；也可 Shift + 右键直接弹出。"
                : "请先选择轨道或片段，再弹出独立编辑器。";
            if (showSeparateEntry)
                m_InspectorSeparateButton.BringToFront();
        }
        if (m_InspectorHeaderOpenButton != null)
        {
            m_InspectorHeaderOpenButton.style.display = hasTarget ? DisplayStyle.Flex : DisplayStyle.None;
            m_InspectorHeaderOpenButton.SetEnabled(hasTarget);
        m_InspectorHeaderOpenButton.tooltip = hasTarget
                ? "在独立窗口中编辑当前属性。"
                : "请先选择轨道或片段，再弹出独立编辑器。";
            m_InspectorHeaderOpenButton.text = m_EmbeddedInspectorClip != null ? "弹出片段" : "弹出轨道";
        }
        if (showPanel)
            m_InspectorPanel.BringToFront();

        // 操作入口跟随实际时间轴视口定位，避免固定 top:52 在工具栏/高 DPI 变化时覆盖内容。
        float overlayTop = Mathf.Max(4f, viewportRect.y + 5f);
        m_InspectorToggleButton.style.top = overlayTop;
        if (m_InspectorSeparateButton != null)
            m_InspectorSeparateButton.style.top = overlayTop;
        if (m_InspectorToggleButton != null)
            m_InspectorToggleButton.style.right = 8f;
        if (m_InspectorSeparateButton != null)
            m_InspectorSeparateButton.style.right = 68f;
        UpdateEmptyStateLayout();
    }

    private void ToggleInspectorDrawer()
    {
        if (!CanEmbedInspector())
        {
            OpenCurrentInspectorInSeparateWindow();
            return;
        }

        m_IsInspectorDrawerOpen = !m_IsInspectorDrawerOpen;
        m_InspectorDrawerClosedByUser = !m_IsInspectorDrawerOpen;
        m_SerializedInspectorDrawerOpen = m_IsInspectorDrawerOpen;
        m_SerializedInspectorDrawerClosedByUser = m_InspectorDrawerClosedByUser;
        UpdateInspectorLayout();
    }

    private void CloseInspectorDrawer()
    {
        m_IsInspectorDrawerOpen = false;
        m_InspectorDrawerClosedByUser = true;
        m_SerializedInspectorDrawerOpen = false;
        m_SerializedInspectorDrawerClosedByUser = true;
        UpdateInspectorLayout();
    }

    private void DisposeEmbeddedInspectorEditor()
    {
        OdinEditor editor = m_EmbeddedInspectorEditor;
        m_EmbeddedInspectorEditor = null;
        if (editor == null)
            return;

        try
        {
            UnityEngine.Object.DestroyImmediate(editor);
        }
        catch (Exception exception)
        {
            Debug.LogException(new InvalidOperationException(
                "Track 内嵌 Inspector Editor 释放失败，已断开引用。", exception));
        }
    }

    private void ReleaseEmbeddedInspectorResources()
    {
        DisposeEmbeddedInspectorEditor();
        ESIndependentInspectorAsset.DestroyManagedReferenceAsset(m_EmbeddedInspectorDrawer);
        m_EmbeddedInspectorDrawer = null;
    }

    private void ClearEmbeddedInspector()
    {
        ReleaseEmbeddedInspectorResources();
        m_EmbeddedInspectorTrack = null;
        m_EmbeddedInspectorClip = null;
        if (m_InspectorTitleLabel != null)
        {
            m_InspectorTitleLabel.text = "属性检查器";
            m_InspectorTitleLabel.tooltip = "请选择轨道或片段查看属性。";
        }
        if (m_InspectorBodyCaption != null)
            m_InspectorBodyCaption.text = "业务字段";
        RefreshInspectorSummary();
        m_IsInspectorDrawerOpen = false;
        m_InspectorDrawerClosedByUser = false;
        UpdateInspectorLayout();
        m_InspectorGuiContainer?.MarkDirtyRepaint();
    }

    private void SetEmbeddedInspector(object data, string title, ESEditorTrackItem trackItem, ESEditorTrackClip clip, bool revealDrawer)
    {
        if (data == null)
        {
            ClearEmbeddedInspector();
            return;
        }

        bool targetChanged = m_EmbeddedInspectorTrack != trackItem || m_EmbeddedInspectorClip != clip;
        m_EmbeddedInspectorTrack = trackItem;
        m_EmbeddedInspectorClip = clip;
        // 标题栏只表达面板职责，具体目标放在摘要区，避免“属性检查器/片段名”重复两次。
        m_InspectorTitleLabel.text = "属性检查器";
        m_InspectorTitleLabel.tooltip = string.IsNullOrEmpty(title) ? "属性检查器" : title;
        if (m_InspectorBodyCaption != null)
            m_InspectorBodyCaption.text = m_EmbeddedInspectorClip != null ? "片段业务字段" : "轨道业务字段";
        if (targetChanged || m_EmbeddedInspectorEditor == null)
        {
            ReleaseEmbeddedInspectorResources();
            m_EmbeddedInspectorDrawer = ESIndependentInspectorAsset.CreateManagedReferenceAsset(
                data,
                string.IsNullOrEmpty(title) ? "Track 内嵌检查器" : title + " · 内嵌桥接");
            m_EmbeddedInspectorEditor = m_EmbeddedInspectorDrawer != null
                ? OdinEditor.CreateEditor(m_EmbeddedInspectorDrawer, typeof(OdinEditor)) as OdinEditor
                : null;
            if (m_InspectorScrollView != null)
                m_InspectorScrollView.scrollOffset = Vector2.zero;
        }

        if (revealDrawer)
        {
            m_IsInspectorDrawerOpen = true;
            m_InspectorDrawerClosedByUser = false;
            m_SerializedInspectorDrawerOpen = true;
            m_SerializedInspectorDrawerClosedByUser = false;
        }
        UpdateInspectorLayout();
        RefreshInspectorSummary();
        m_InspectorGuiContainer?.MarkDirtyRepaint();
    }

    private void DrawEmbeddedInspector()
    {
        if (m_EmbeddedInspectorEditor == null)
        {
            ESTrackInspectorVisuals.DrawEmptyState(
                "尚未选择轨道或片段",
                "选择时间轴中的轨道或片段后，在这里编辑业务设置。",
                "当前 Inspector 等待编辑目标。");
            return;
        }

        RecordInspectorUndoBeforeInput(TrackContainer as UnityEngine.Object, "编辑时间轴属性");
        EditorGUI.BeginChangeCheck();
        using (ESTrackInspectorVisuals.BeginBody())
        {
            m_EmbeddedInspectorEditor.DrawDefaultInspector();
        }
        if (EditorGUI.EndChangeCheck())
            ApplyEmbeddedInspectorChanges();
    }

    private static void RecordInspectorUndoBeforeInput(UnityEngine.Object target, string label)
    {
        if (target == null || Event.current == null)
            return;

        EventType type = Event.current.type;
        if (type == EventType.MouseDown
            || type == EventType.DragPerform
            || type == EventType.ExecuteCommand)
        {
            Undo.RecordObject(target, label);
        }
        else if (type == EventType.KeyDown && !EditorGUIUtility.editingTextField)
        {
            // 文本输入由 Odin/SerializedObject 自身合并；逐字符 RecordObject 会让
            // Ctrl+Z 变成几十步，破坏商业编辑器的手感。
            Undo.RecordObject(target, label);
        }
    }

    private void ApplyEmbeddedInspectorChanges()
    {
        object data = m_EmbeddedInspectorClip != null
            ? m_EmbeddedInspectorClip.trackClip
            : m_EmbeddedInspectorTrack != null ? m_EmbeddedInspectorTrack.item : null;
        ApplyAuthoringChange(
            data,
            ESTrackAuthoringChangeFlags.InspectorEdit,
            "内嵌 Inspector 修改");
    }

    internal void ApplyIndependentInspectorChanges(object data)
    {
        ApplyAuthoringChange(
            data,
            ESTrackAuthoringChangeFlags.InspectorEdit,
            "独立 Inspector 修改");
    }

    internal void ApplyAuthoringChange(
        object data,
        ESTrackAuthoringChangeFlags flags,
        string source)
    {
        if ((flags & ESTrackAuthoringChangeFlags.Projection) != 0)
        {
            RefreshAuthoringProjection(data);
            RefreshIndependentInspectorTitle(data);
        }

        if ((flags & ESTrackAuthoringChangeFlags.TimelineDuration) != 0)
            SyncTotalTimeFromCurrentSequence(false);

        if ((flags & ESTrackAuthoringChangeFlags.Save) != 0)
            ESTrackViewWindowHelper.SaveContainerChanges(source);

        if ((flags & ESTrackAuthoringChangeFlags.Preview) != 0)
            SchedulePreviewRebuild();

        if ((flags & ESTrackAuthoringChangeFlags.Inspector) != 0)
        {
            RefreshInspectorSummary();
            m_InspectorGuiContainer?.MarkDirtyRepaint();
        }

        if ((flags & ESTrackAuthoringChangeFlags.Repaint) != 0)
            Repaint();
    }

    private void RefreshIndependentInspectorTitle(object data)
    {
        if (data is ITrackItem track
            && Last_EditorWindowForTrackItem is ESTrackItemTemporaryInspectorWindow trackWindow
            && ReferenceEquals(trackWindow.CurrentInspectorData, track))
        {
            trackWindow.titleContent = new GUIContent("编辑轨道<" + track.DisplayName + ">");
            trackWindow.Repaint();
        }

        if (data is ITrackClip clip
            && Last_EditorWindowForTrackClip is ESTrackClipTemporaryInspectorWindow clipWindow
            && ReferenceEquals(clipWindow.CurrentInspectorData, clip))
        {
            clipWindow.titleContent = new GUIContent("编辑片段<" + clip.DisplayName + ">");
            clipWindow.Repaint();
        }
    }

    private void RefreshAuthoringProjection(object data)
    {
        if (data is ITrackItem trackData)
        {
            ESEditorTrackItem trackItem = Items?.FirstOrDefault(item => item != null && ReferenceEquals(item.item, trackData));
            if (trackItem != null)
                trackItem.RefreshProjectionAfterUndoRedo();
        }

        if (data is ITrackClip clipData)
        {
            ESEditorTrackClip clip = FindEditorClip(clipData);
            if (clip != null)
            {
                clip.SetTimeScaleAndStartShowCache();
                clip.UpdateNodeView();
                clip.RefreshEnabledVisual();
            }
        }
    }

    internal void NotifyIndependentInspectorBound(OdinEditorWindow inspector)
    {
        switch (inspector)
        {
            case ESTrackItemTemporaryInspectorWindow trackWindow:
                Last_EditorWindowForTrackItem = trackWindow;
                break;
            case ESTrackClipTemporaryInspectorWindow clipWindow:
                Last_EditorWindowForTrackClip = clipWindow;
                if (clipWindow.CurrentInspectorData is ITrackClip clipData)
                    SetFocusedEditingClip(FindEditorClip(clipData));
                break;
            case ESTrackSkillDataTemporaryInspectorWindow skillWindow:
                Last_EditorWindowForSkillDataInfo = skillWindow;
                break;
        }
    }

    internal void NotifyIndependentInspectorClosed(OdinEditorWindow inspector)
    {
        switch (inspector)
        {
            case ESTrackItemTemporaryInspectorWindow trackWindow:
                if (ReferenceEquals(Last_EditorWindowForTrackItem, trackWindow))
                    Last_EditorWindowForTrackItem = null;
                break;
            case ESTrackClipTemporaryInspectorWindow clipWindow:
                if (clipWindow.CurrentInspectorData is ITrackClip clipData)
                {
                    ESEditorTrackClip editorClip = FindEditorClip(clipData);
                    if (editorClip != null)
                        ClearFocusedEditingClip(editorClip);
                }
                if (ReferenceEquals(Last_EditorWindowForTrackClip, clipWindow))
                    Last_EditorWindowForTrackClip = null;
                break;
            case ESTrackSkillDataTemporaryInspectorWindow skillWindow:
                if (ReferenceEquals(Last_EditorWindowForSkillDataInfo, skillWindow))
                    Last_EditorWindowForSkillDataInfo = null;
                break;
        }

        m_InspectorGuiContainer?.MarkDirtyRepaint();
        RefreshInspectorSummary();
        Repaint();
    }

    private void BindNormalHandles()
    {
        //MINMAX 的显示范围选定
        horSlider.RegisterValueChangedCallback(HorSliderChange);

        //rightPanel 的快捷操作
        // 1. 鼠标滚轮事件 - 缩放
        rootVisualElement.focusable = true;
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnTrackWindowKeyDown, TrickleDown.TrickleDown);
        rootVisualElement.RegisterCallback<PointerDownEvent>(OnTrackWindowPointerDown, TrickleDown.TrickleDown);
        rootVisualElement.RegisterCallback<PointerMoveEvent>(OnTrackPanelSplitterPointerMove, TrickleDown.TrickleDown);
        rootVisualElement.RegisterCallback<PointerUpEvent>(OnTrackPanelSplitterPointerUp, TrickleDown.TrickleDown);
        rootVisualElement.RegisterCallback<PointerCaptureOutEvent>(OnTrackPanelSplitterPointerCaptureOut, TrickleDown.TrickleDown);
        rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnTimelineGeometryChanged);
        rightPanel.RegisterCallback<WheelEvent>(OnRightPanelWheel, TrickleDown.TrickleDown);
        verScroll.RegisterCallback<WheelEvent>(OnScrollViewWheel, TrickleDown.TrickleDown);

        // 2. 鼠标普通事件 - 平移
        rightPanel.RegisterCallback<MouseDownEvent>(OnRightPanelMouseDown, TrickleDown.NoTrickleDown);
        rightPanel.RegisterCallback<MouseMoveEvent>(OnRightPanelMouseMove, TrickleDown.NoTrickleDown);
        rightPanel.RegisterCallback<MouseUpEvent>(OnRightPanelMouseUp, TrickleDown.NoTrickleDown);
        rightPanel.RegisterCallback<MouseCaptureOutEvent>(OnRightPanelMouseCaptureOut);

        // 3. 右键上下文菜单
        rightPanel.RegisterCallback<ContextClickEvent>(OnContextClick_CompleteMenu);
        m_TrackPanelSplitter.RegisterCallback<PointerDownEvent>(OnTrackPanelSplitterPointerDown);
        m_TrackPanelSplitter.RegisterCallback<PointerEnterEvent>(OnTrackPanelSplitterPointerEnter);
        m_TrackPanelSplitter.RegisterCallback<PointerLeaveEvent>(OnTrackPanelSplitterPointerLeave);

    }

    private void UnbindNormalHandles()
    {
        horSlider?.UnregisterValueChangedCallback(HorSliderChange);
        rootVisualElement?.UnregisterCallback<KeyDownEvent>(OnTrackWindowKeyDown, TrickleDown.TrickleDown);
        rootVisualElement?.UnregisterCallback<PointerDownEvent>(OnTrackWindowPointerDown, TrickleDown.TrickleDown);
        rootVisualElement?.UnregisterCallback<PointerMoveEvent>(OnTrackPanelSplitterPointerMove, TrickleDown.TrickleDown);
        rootVisualElement?.UnregisterCallback<PointerUpEvent>(OnTrackPanelSplitterPointerUp, TrickleDown.TrickleDown);
        rootVisualElement?.UnregisterCallback<PointerCaptureOutEvent>(OnTrackPanelSplitterPointerCaptureOut, TrickleDown.TrickleDown);
        rootVisualElement?.UnregisterCallback<GeometryChangedEvent>(OnTimelineGeometryChanged);
        rightPanel?.UnregisterCallback<WheelEvent>(OnRightPanelWheel, TrickleDown.TrickleDown);
        verScroll?.UnregisterCallback<WheelEvent>(OnScrollViewWheel, TrickleDown.TrickleDown);
        rightPanel?.UnregisterCallback<MouseDownEvent>(OnRightPanelMouseDown, TrickleDown.NoTrickleDown);
        rightPanel?.UnregisterCallback<MouseMoveEvent>(OnRightPanelMouseMove, TrickleDown.NoTrickleDown);
        rightPanel?.UnregisterCallback<MouseUpEvent>(OnRightPanelMouseUp, TrickleDown.NoTrickleDown);
        rightPanel?.UnregisterCallback<MouseCaptureOutEvent>(OnRightPanelMouseCaptureOut);
        rightPanel?.UnregisterCallback<ContextClickEvent>(OnContextClick_CompleteMenu);
        m_TrackPanelSplitter?.UnregisterCallback<PointerDownEvent>(OnTrackPanelSplitterPointerDown);
        m_TrackPanelSplitter?.UnregisterCallback<PointerEnterEvent>(OnTrackPanelSplitterPointerEnter);
        m_TrackPanelSplitter?.UnregisterCallback<PointerLeaveEvent>(OnTrackPanelSplitterPointerLeave);
    }

    private void OnTrackPanelSplitterPointerEnter(PointerEnterEvent evt)
    {
        if (!m_IsResizingTrackPanel && m_TrackPanelSplitter != null)
            m_TrackPanelSplitter.style.backgroundColor = ESTrackViewTheme.SplitterHoverBackground;
    }

    private void OnTrackPanelSplitterPointerLeave(PointerLeaveEvent evt)
    {
        if (!m_IsResizingTrackPanel && m_TrackPanelSplitter != null)
            m_TrackPanelSplitter.style.backgroundColor = ESTrackViewTheme.Transparent;
    }

    private void OnTimelineGeometryChanged(GeometryChangedEvent evt)
    {
        if (Mathf.Abs(evt.newRect.width - evt.oldRect.width) > 0.1f
            || Mathf.Abs(evt.newRect.height - evt.oldRect.height) > 0.1f)
        {
            ApplyTrackPanelLayout(true);
            UpdateInspectorLayout();
        }
    }

    private void OnTrackPanelSplitterPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0)
            return;

        m_IsResizingTrackPanel = true;
        m_TrackPanelResizePointerId = evt.pointerId;
        m_TrackPanelSplitter.style.backgroundColor = ESTrackViewTheme.Accent;
        m_TrackPanelSplitter.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnTrackPanelSplitterPointerMove(PointerMoveEvent evt)
    {
        if (!m_IsResizingTrackPanel || evt.pointerId != m_TrackPanelResizePointerId)
            return;

        float maxWidth = Mathf.Max(MinTrackPanelWidth, rootVisualElement.layout.width - MinTimelineCanvasWidth);
        m_TrackPanelWidth = Mathf.Clamp(rootVisualElement.WorldToLocal(evt.position).x, MinTrackPanelWidth, Mathf.Min(MaxTrackPanelWidth, maxWidth));
        ApplyTrackPanelLayout(true);
        evt.StopPropagation();
    }

    private void OnTrackPanelSplitterPointerUp(PointerUpEvent evt)
    {
        if (!m_IsResizingTrackPanel || evt.pointerId != m_TrackPanelResizePointerId)
            return;

        EndTrackPanelResize();
        evt.StopPropagation();
    }

    private void OnTrackPanelSplitterPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        if (m_IsResizingTrackPanel && evt.pointerId == m_TrackPanelResizePointerId)
            EndTrackPanelResize();
    }

    private void EndTrackPanelResize()
    {
        if (m_TrackPanelSplitter != null && m_TrackPanelResizePointerId >= 0 && m_TrackPanelSplitter.HasPointerCapture(m_TrackPanelResizePointerId))
            m_TrackPanelSplitter.ReleasePointer(m_TrackPanelResizePointerId);

        m_IsResizingTrackPanel = false;
        m_TrackPanelResizePointerId = -1;
        if (m_TrackPanelSplitter != null)
            m_TrackPanelSplitter.style.backgroundColor = ESTrackViewTheme.Transparent;
    }

    internal void ApplyTrackPanelLayout(bool refreshTracks)
    {
        if (leftPanel == null || rightPanel == null || horSlider == null)
            return;

        float availableWidth = rootVisualElement.layout.width;
        if (availableWidth <= 0f)
        {
            if (m_ApplyTrackPanelLayoutScheduled)
                return;
            m_ApplyTrackPanelLayoutScheduled = true;
            m_ApplyTrackPanelLayoutTask?.Pause();
            IVisualElementScheduledItem task = rootVisualElement.schedule.Execute(() =>
            {
                m_ApplyTrackPanelLayoutTask = null;
                m_ApplyTrackPanelLayoutScheduled = false;
                if (rootVisualElement == null || this == null)
                    return;
                ApplyTrackPanelLayout(refreshTracks);
            });
            task.ExecuteLater(0);
            m_ApplyTrackPanelLayoutTask = task;
            return;
        }
        float maxWidth = availableWidth > 0f
            ? Mathf.Max(MinTrackPanelWidth, availableWidth - MinTimelineCanvasWidth)
            : MaxTrackPanelWidth;
        m_TrackPanelWidth = Mathf.Clamp(m_TrackPanelWidth, MinTrackPanelWidth, Mathf.Min(MaxTrackPanelWidth, maxWidth));

        leftPanel.style.width = m_TrackPanelWidth;
        leftPanel.style.minWidth = m_TrackPanelWidth;
        leftPanel.style.maxWidth = m_TrackPanelWidth;
        rightPanel.style.left = m_TrackPanelWidth;
        horSlider.style.left = m_TrackPanelWidth;

        if (m_TrackPanelSplitter != null)
            m_TrackPanelSplitter.style.left = m_TrackPanelWidth;

        float canvasWidth = Mathf.Max(1f, availableWidth - m_TrackPanelWidth);
        ruler?.ApplyTimelineWidth(canvasWidth);
        foreach (ESEditorTrackItem trackItem in Items)
        {
            if (!IsTrackItemAttachedToCurrentWindow(trackItem))
                continue;
            trackItem.ApplyTimelineLayout(m_TrackPanelWidth, canvasWidth);
        }

        UpdateEmptyStateLayout();

        if (refreshTracks)
        {
            MoveTimeCursor(cursorTime);
            ScheduleViewRefresh();
        }
    }

    private static bool IsTrackItemAttachedToCurrentWindow(ESEditorTrackItem trackItem)
    {
        return trackItem != null
               && trackItem.parent != null
               && trackItem.panel != null
               && window != null
               && window.rootVisualElement != null
               && window.rootVisualElement.panel != null
               && ReferenceEquals(trackItem.panel, window.rootVisualElement.panel);
    }

    internal void UpdateTimelineContentHeight()
    {
        if (m_TimelineWorkspace == null)
            return;

        const float headerHeight = 40f;
        const float minimumHeight = 400f;
        float trackHeight = 0f;
        if (Items != null)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                ESEditorTrackItem item = Items[i];
                if (item == null || item.style.display == DisplayStyle.None)
                    continue;
                trackHeight += item.CurrentHeight;
            }
        }

        m_TimelineWorkspace.style.minHeight = Mathf.Max(minimumHeight, headerHeight + trackHeight);
    }

    private void ApplyTrackFilter(string query)
    {
        query = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
        if (Items != null)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                ESEditorTrackItem item = Items[i];
                if (item == null)
                    continue;

                bool visible = string.IsNullOrEmpty(query) || TrackMatchesQuery(item, query);
                item.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        UpdateTimelineContentHeight();
        ApplyTrackPanelLayout(false);
        ScheduleViewRefresh();
    }

    private static bool TrackMatchesQuery(ESEditorTrackItem item, string query)
    {
        if (item == null || item.item == null || string.IsNullOrEmpty(query))
            return item != null;

        if (ContainsIgnoreCase(item.item.DisplayName, query)
            || ContainsIgnoreCase(item.item.GetType().Name, query))
        {
            return true;
        }

        if (item.TrackClips == null)
            return false;

        for (int i = 0; i < item.TrackClips.Count; i++)
        {
            ESEditorTrackClip clip = item.TrackClips[i];
            ITrackClip trackClip = clip != null ? clip.trackClip : null;
            if (trackClip != null
                && (ContainsIgnoreCase(trackClip.DisplayName, query)
                    || ContainsIgnoreCase(trackClip.GetType().Name, query)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrEmpty(source)
               && !string.IsNullOrEmpty(value)
               && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void CreateTimeCursor()
    {
        m_SelectionVisual.RemoveFromHierarchy();
        leftPanel.Add(m_SelectionVisual);

        timeCursor = new VisualElement
        {
            name = "time-cursor",
            style =
        {
            position = Position.Absolute,
            width = 14,               // 透明命中区，方便拖动
            backgroundColor = ESTrackViewTheme.Transparent,
            top = 0,
            bottom = 0,
            // 使用稳定的 Editor 内置光标资源，避免不同 Unity 版本缺少 d_GridLayout 图标时报错。
            cursor = new Cursor { texture = EditorGUIUtility.Load("Cursors/d_Cursor_Cross") as Texture2D, hotspot = new Vector2(7, 7) }
        }
        };

        // 视觉上仍显示一条细线
        m_TimeCursorLine = new VisualElement
        {
            style =
        {
            width = 2,
            backgroundColor = ESTrackViewTheme.PlayheadAccent,
            position = Position.Absolute,
            left = 6,
            top = 0,
            bottom = 0
        }
        };
        timeCursor.Add(m_TimeCursorLine);

        m_TimeCursorHandle = new VisualElement
        {
            name = "time-cursor-handle",
            pickingMode = PickingMode.Ignore,
            style =
        {
            position = Position.Absolute,
            width = 9,
            height = 6,
            left = 2.5f,
            top = 1,
            backgroundColor = ESTrackViewTheme.PlayheadHandle
        }
        };
        ESEditorPresentation.ApplyCornerRadius(
            m_TimeCursorHandle, ESEditorPresentation.ESCornerRadiusToken.Control);
        timeCursor.Add(m_TimeCursorHandle);

        leftPanel.Add(timeCursor);
        timeCursor.BringToFront();

        // 直接绑定鼠标事件
        timeCursor.RegisterCallback<MouseDownEvent>(OnTimeCursorMouseDown);
        timeCursor.RegisterCallback<MouseMoveEvent>(OnTimeCursorMouseMove);
        timeCursor.RegisterCallback<MouseUpEvent>(OnTimeCursorMouseUp);
        timeCursor.RegisterCallback<MouseCaptureOutEvent>(OnTimeCursorMouseCaptureOut);


    }
    #endregion

    #region  播放支持
    /// <summary> 根据一个序列数据，创建并填充采样器的播放器 </summary>
    private EditorSequencePlayer BuildSequencePlayer(ITrackSequence sequence, Entity editorEntity)
    {
        float sequenceDuration = SyncTotalTimeFromSequence(sequence, false);
        if (editorEntity == null)
            editorEntity = EditorRememberedEntityTarget.TrackPreview.ResolveFromSelectionOrMemory();

        if (editorEntity != null)
            EditorRememberedEntityTarget.TrackPreview.Remember(editorEntity);

        var seqPlayer = new EditorSequencePlayer
        {
            Name = "未命名时间轴",
            Duration = sequenceDuration,
            Speed = 1f
        };
        EditorRememberedEntityTarget.TrackPreview.FillPreviewTarget(seqPlayer.PreviewTarget, editorEntity);

        if (sequence == null || sequence.Tracks == null)
            return seqPlayer;

        foreach (var track in sequence.Tracks)
        {
            if (track == null || !track.Enabled)
                continue;

            List<IEditorTimeSampler> trackSamplers = null;
            try
            {
                trackSamplers = track.CreateEditorSamplers(sequence, seqPlayer.PreviewTarget);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ESTrackViewWindow] CreateEditorSamplers failed. Track={track.DisplayName}, Type={track.GetType().Name}");
                Debug.LogException(e);
            }

            if (trackSamplers == null)
                continue;

            for (int i = 0; i < trackSamplers.Count; i++)
            {
                IEditorTimeSampler sampler = trackSamplers[i];
                try
                {
                    seqPlayer.RegisterSampler(sampler);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ESTrackViewWindow] RegisterSampler failed. Track={track.DisplayName}, Sampler={sampler?.GetType().Name ?? "<Null>"}");
                    Debug.LogException(e);
                }
            }
        }

        // 绑定 UI 更新事件
        seqPlayer.OnTimeUpdated += OnSequenceTimeUpdated;
        seqPlayer.Duration = SyncTotalTimeFromSequence(sequence, false);

        return seqPlayer;
    }

    private float SyncTotalTimeFromSequence(ITrackSequence sequence, bool markDirty)
    {
        float duration = CalculateSequenceTotalTime(sequence);
        TotalTime = duration;

        bool durationCacheChanged = false;
        if (sequence is ITrackSequenceDurationCache durationCache && !Mathf.Approximately(durationCache.CachedMaxTime, duration))
        {
            durationCache.CachedMaxTime = duration;
            durationCacheChanged = true;
        }

        if (EditorTimelinePlayer.Instance.ActiveSequence != null)
            EditorTimelinePlayer.Instance.ActiveSequence.Duration = duration;

        if (markDirty && durationCacheChanged)
            ESTrackViewWindowHelper.SaveContainerDisplayChanges("时间轴总时长调整");

        return duration;
    }

    public float SyncTotalTimeFromCurrentSequence(bool markDirty)
    {
        float duration = SyncTotalTimeFromSequence(Sequence, markDirty);
        ScheduleAutoValidateSequenceVisuals();
        return duration;
    }

    private static float CalculateSequenceTotalTime(ITrackSequence sequence)
    {
        float maxEndTime = MinSequenceTotalTime;
        if (sequence != null && sequence.Tracks != null)
        {
            foreach (var track in sequence.Tracks)
            {
                if (track == null || track.Clips == null)
                    continue;

                foreach (var clip in track.Clips)
                {
                    if (clip == null)
                        continue;

                    float endTime = clip.StartTime + Mathf.Max(0f, clip.DurationTime);
                    if (endTime > maxEndTime)
                        maxEndTime = endTime;
                }
            }
        }

        return Mathf.Max(MinSequenceTotalTime, Mathf.Ceil((maxEndTime + SequenceTailPaddingTime) * 10f) / 10f);
    }

    private Entity ResolvePreviewEntity()
    {
        GameObject selectedGameObject = Selection.activeGameObject;
        if (selectedGameObject == null && Selection.activeObject is Component selectedComponent)
            selectedGameObject = selectedComponent.gameObject;

        if (selectedGameObject != null)
        {
            var entity = FindEntityInSelfOrParents(selectedGameObject);
            if (entity != null)
            {
                EditorRememberedEntityTarget.TrackPreview.Remember(entity);
                return entity;
            }
        }

        UnityEngine.Object[] selectedObjects = Selection.objects;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            if (selectedObjects[i] is GameObject gameObject)
            {
                Entity entity = FindEntityInSelfOrParents(gameObject);
                if (entity != null)
                {
                    EditorRememberedEntityTarget.TrackPreview.Remember(entity);
                    return entity;
                }
            }

            if (selectedObjects[i] is Component component)
            {
                Entity entity = FindEntityInSelfOrParents(component.gameObject);
                if (entity != null)
                {
                    EditorRememberedEntityTarget.TrackPreview.Remember(entity);
                    return entity;
                }
            }
        }

        return EditorRememberedEntityTarget.TrackPreview.ResolveOrSceneFallback();
    }

    private static Entity FindEntityInSelfOrParents(GameObject gameObject)
    {
        Transform current = gameObject != null ? gameObject.transform : null;
        while (current != null)
        {
            Entity entity = current.GetComponent<Entity>();
            if (entity != null)
                return entity;

            current = current.parent;
        }

        return null;
    }

    public void UpdatePreselectEntityFromSelection(bool askWhenParentEntity)
    {
        Entity selectedEntity = ResolvePreviewEntity();
        if (selectedEntity == null || selectedEntity == PreselectEntity)
        {
            RefreshEntityDisplay();
            return;
        }

        bool directEntity = IsSelectionDirectEntity(selectedEntity);
        if (directEntity || !askWhenParentEntity || window == null)
        {
            SetPreselectEntity(selectedEntity);
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "更改轨道预览 Entity",
            $"当前选择对象的父级包含 Entity: {selectedEntity.name}\n是否将它设为轨道预览 Entity?",
            "更改",
            "保持当前");

        if (confirm)
            SetPreselectEntity(selectedEntity);
        else
            RefreshEntityDisplay();
    }

    private static bool IsSelectionDirectEntity(Entity entity)
    {
        if (entity == null)
            return false;

        if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Entity>() == entity)
            return true;

        if (Selection.activeObject is Component component && component.GetComponent<Entity>() == entity)
            return true;

        return false;
    }

    public void SetPreselectEntity(Entity entity)
    {
        PreselectEntity = entity;
        if (entity != null)
            EditorRememberedEntityTarget.TrackPreview.Remember(entity);

        RefreshEntityDisplay();
    }

    public void SelectTrack(ESEditorTrackItem trackItem)
    {
        if (SelectedTrackItem == trackItem)
        {
            SetTrackInspectorTarget(trackItem, false);
            rootVisualElement?.Focus();
            return;
        }

        if (SelectedTrackItem != null)
            SelectedTrackItem.SetSelected(false);

        SelectedTrackItem = trackItem;
        if (SelectedTrackItem != null)
            SelectedTrackItem.SetSelected(true);

        m_SelectedTrackIndex = SelectedTrackItem != null && Items != null ? Items.IndexOf(SelectedTrackItem) : -1;
        m_SelectedClipIndex = -1;
        m_SelectedTrackId = GetStableTrackId(SelectedTrackItem != null ? SelectedTrackItem.item : null);
        m_SelectedClipId = string.Empty;
        SavePersistedSelection();

        SetTrackInspectorTarget(SelectedTrackItem, false);
        rootVisualElement?.Focus();
    }

    public void SelectClip(ESEditorTrackClip clip)
    {
        SelectClip(clip, false);
    }

    public void SetFocusedEditingClip(ESEditorTrackClip clip)
    {
        if (FocusedEditingClip == clip)
        {
            FocusedEditingClip?.SetFocusedEditing(true);
            return;
        }

        if (FocusedEditingClip != null)
            FocusedEditingClip.SetFocusedEditing(false);

        FocusedEditingClip = clip;
        if (FocusedEditingClip != null)
            FocusedEditingClip.SetFocusedEditing(true);
    }

    public void ClearFocusedEditingClip(ESEditorTrackClip clip)
    {
        if (clip != null && FocusedEditingClip != clip)
            return;

        if (FocusedEditingClip != null)
            FocusedEditingClip.SetFocusedEditing(false);

        FocusedEditingClip = null;
    }

    public void SelectClip(ESEditorTrackClip clip, bool additive)
    {
        if (SelectedClip == clip)
        {
            if (additive)
            {
                RemoveClipFromSelection(clip);
            }
            else if (m_SelectedClips.Count > 1)
            {
                foreach (ESEditorTrackClip selected in m_SelectedClips)
                {
                    if (selected != null && selected != clip)
                        selected.SetSelected(false);
                }

                m_SelectedClips.Clear();
                m_SelectedClips.Add(clip);
                RefreshClipSelectionVisuals();
            }

            SetClipInspectorTarget(SelectedClip, false);
            rootVisualElement?.Focus();
            return;
        }

        if (!additive)
            ClearClipSelection();

        SelectedClip = clip;
        if (SelectedClip != null)
        {
            m_SelectedClips.Add(SelectedClip);
            ESEditorTrackItem selectedTrack = Items != null
                ? Items.FirstOrDefault(item => item != null && item.TrackClips != null && item.TrackClips.Contains(SelectedClip))
                : null;
            m_SelectedTrackIndex = selectedTrack != null && Items != null ? Items.IndexOf(selectedTrack) : -1;
            m_SelectedClipIndex = selectedTrack != null && selectedTrack.TrackClips != null
                ? selectedTrack.TrackClips.IndexOf(SelectedClip)
                : -1;
            m_SelectedTrackId = selectedTrack != null ? GetStableTrackId(selectedTrack.item) : string.Empty;
            m_SelectedClipId = GetStableClipId(SelectedClip.trackClip);
            SavePersistedSelection();
            RefreshClipSelectionVisuals();
        }

        SetClipInspectorTarget(SelectedClip, false);
        rootVisualElement?.Focus();
    }

    private void SetTrackInspectorTarget(ESEditorTrackItem trackItem, bool revealDrawer)
    {
        if (trackItem == null || trackItem.item == null)
        {
            ClearEmbeddedInspector();
            return;
        }

        SetEmbeddedInspector(trackItem.item, "轨道 · " + trackItem.item.DisplayName, trackItem, null, revealDrawer);
    }

    private void SetClipInspectorTarget(ESEditorTrackClip clip, bool revealDrawer)
    {
        if (clip == null || clip.trackClip == null)
        {
            ClearEmbeddedInspector();
            return;
        }

        SetEmbeddedInspector(clip.trackClip, "片段 · " + clip.trackClip.DisplayName, null, clip, revealDrawer);
    }

    public bool IsClipSelected(ESEditorTrackClip clip)
    {
        return clip != null && m_SelectedClips.Contains(clip);
    }

    public int SelectedClipCount => m_SelectedClips.Count;

    internal void HandleTrackItemRemoved(ESEditorTrackItem removedTrack)
    {
        if (removedTrack == null)
            return;

        if (ReferenceEquals(ESTrackItemTemporaryInspectorWindow.UsingWindow?.CurrentInspectorData, removedTrack.item))
            ESTrackItemTemporaryInspectorWindow.CloseCurrentWindow();
        if (ESTrackClipTemporaryInspectorWindow.UsingWindow?.CurrentInspectorData is ITrackClip inspectedClip
            && removedTrack.TrackClips.Any(clip => clip != null && ReferenceEquals(clip.trackClip, inspectedClip)))
        {
            ESTrackClipTemporaryInspectorWindow.CloseCurrentWindow();
        }

        bool removedInspectorClip = m_EmbeddedInspectorClip != null && removedTrack.TrackClips.Contains(m_EmbeddedInspectorClip);
        bool removedSelectedClip = m_SelectedClips.Any(removedTrack.TrackClips.Contains);
        Items.Remove(removedTrack);
        if (SelectedTrackItem == removedTrack || m_EmbeddedInspectorTrack == removedTrack || removedInspectorClip || removedSelectedClip)
        {
            if (SelectedTrackItem == removedTrack)
            {
                SelectedTrackItem = null;
                m_SelectedTrackIndex = -1;
                m_SelectedTrackId = string.Empty;
            }
            ClearClipSelection();
            ClearEmbeddedInspector();
        }

        if (SelectedTrackItem != null && Items != null)
        {
            m_SelectedTrackIndex = Items.IndexOf(SelectedTrackItem);
            m_SelectedTrackId = GetStableTrackId(SelectedTrackItem.item);
        }

        UpdateTimelineContentHeight();
    }

    internal void HandleTrackClipRemoved(ESEditorTrackClip removedClip)
    {
        if (removedClip == null)
            return;

        if (ReferenceEquals(ESTrackClipTemporaryInspectorWindow.UsingWindow?.CurrentInspectorData, removedClip.trackClip))
            ESTrackClipTemporaryInspectorWindow.CloseCurrentWindow();

        bool wasInspectorTarget = m_EmbeddedInspectorClip == removedClip;
        m_SelectedClips.Remove(removedClip);
        removedClip.SetSelected(false);
        if (SelectedClip == removedClip)
            SelectedClip = m_SelectedClips.FirstOrDefault();

        ESEditorTrackItem selectedTrack = null;
        if (SelectedClip != null && Items != null)
        {
            selectedTrack = Items.FirstOrDefault(item => item != null && item.TrackClips != null && item.TrackClips.Contains(SelectedClip));
            m_SelectedTrackIndex = selectedTrack != null ? Items.IndexOf(selectedTrack) : -1;
            m_SelectedClipIndex = selectedTrack != null && selectedTrack.TrackClips != null ? selectedTrack.TrackClips.IndexOf(SelectedClip) : -1;
        }
        else
        {
            m_SelectedClipIndex = -1;
        }
        m_SelectedTrackId = selectedTrack != null ? GetStableTrackId(selectedTrack.item) : string.Empty;
        m_SelectedClipId = SelectedClip != null ? GetStableClipId(SelectedClip.trackClip) : string.Empty;
        SavePersistedSelection();

        if (wasInspectorTarget)
        {
            if (SelectedClip != null)
                SetClipInspectorTarget(SelectedClip, false);
            else
                ClearEmbeddedInspector();
        }
        else
        {
            RefreshClipSelectionVisuals();
        }
    }

    private void ClearClipSelection(bool clearPersistedSelection = true)
    {
        bool clearClipInspector = m_EmbeddedInspectorClip != null;
        foreach (ESEditorTrackClip selected in m_SelectedClips)
        {
            if (selected != null)
                selected.SetSelected(false);
        }

        m_SelectedClips.Clear();
        SelectedClip = null;

        if (clearPersistedSelection)
        {
            m_SelectedClipIndex = -1;
            m_SelectedClipId = string.Empty;
            SavePersistedSelection();
        }

        if (clearClipInspector)
            ClearEmbeddedInspector();
    }

    private void SavePersistedSelection()
    {
        if (string.IsNullOrEmpty(m_TrackContainerAssetGuid) && string.IsNullOrEmpty(m_TrackContainerAssetPath))
            return;

        string scope = PersistedSelectionScope;
        string trackId = m_SelectedTrackId;
        string clipId = m_SelectedClipId;
        ESEditorTrackItem selectedTrack = null;
        if (m_SelectedTrackIndex >= 0
            && Items != null
            && m_SelectedTrackIndex < Items.Count)
        {
            selectedTrack = Items[m_SelectedTrackIndex];
        }

        if (string.IsNullOrEmpty(trackId) && selectedTrack != null)
        {
            trackId = GetStableTrackId(selectedTrack.item);
            m_SelectedTrackId = trackId;
        }

        if (string.IsNullOrEmpty(clipId)
            && selectedTrack != null
            && selectedTrack.TrackClips != null
            && m_SelectedClipIndex >= 0
            && m_SelectedClipIndex < selectedTrack.TrackClips.Count)
        {
            ESEditorTrackClip clip = selectedTrack.TrackClips[m_SelectedClipIndex];
            clipId = GetStableClipId(clip != null ? clip.trackClip : null);
            m_SelectedClipId = clipId;
        }

        EditorPrefs.SetString(
            PersistedSelectionPrefix + scope + ".TrackIndex",
            m_SelectedTrackIndex.ToString(CultureInfo.InvariantCulture));
        EditorPrefs.SetString(
            PersistedSelectionPrefix + scope + ".ClipIndex",
            m_SelectedClipIndex.ToString(CultureInfo.InvariantCulture));
        EditorPrefs.SetString(
            PersistedSelectionPrefix + scope + ".TrackId",
            trackId ?? string.Empty);
        EditorPrefs.SetString(
            PersistedSelectionPrefix + scope + ".ClipId",
            clipId ?? string.Empty);
    }

    private void LoadPersistedSelection()
    {
        if (string.IsNullOrEmpty(m_TrackContainerAssetGuid) && string.IsNullOrEmpty(m_TrackContainerAssetPath))
            return;

        string scope = PersistedSelectionScope;
        string trackKey = PersistedSelectionPrefix + scope + ".TrackIndex";
        string clipKey = PersistedSelectionPrefix + scope + ".ClipIndex";
        string trackIdKey = PersistedSelectionPrefix + scope + ".TrackId";
        string clipIdKey = PersistedSelectionPrefix + scope + ".ClipId";
        int trackIndex = -1;
        int clipIndex = -1;

        if (int.TryParse(
                EditorPrefs.GetString(trackKey, string.Empty),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out trackIndex))
        {
            m_SelectedTrackIndex = trackIndex;
        }

        if (int.TryParse(
                EditorPrefs.GetString(clipKey, string.Empty),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out clipIndex))
        {
            m_SelectedClipIndex = clipIndex;
        }

        m_SelectedTrackId = EditorPrefs.GetString(trackIdKey, string.Empty);
        m_SelectedClipId = EditorPrefs.GetString(clipIdKey, string.Empty);
    }

    private string PersistedSelectionScope
    {
        get
        {
            string scope = !string.IsNullOrEmpty(m_TrackContainerAssetGuid)
                ? m_TrackContainerAssetGuid
                : m_TrackContainerAssetPath;
            if (m_TrackContainerSubAssetLocalFileId != 0)
                scope += "/" + m_TrackContainerSubAssetLocalFileId.ToString(CultureInfo.InvariantCulture);
            else if (!string.IsNullOrEmpty(m_TrackContainerSubAssetName))
                scope += "/" + m_TrackContainerSubAssetName;
            return string.IsNullOrEmpty(scope) ? "default" : scope;
        }
    }

    private void SchedulePlaybackContextSave()
    {
        if (!HasPersistedPlaybackScope() || m_ApplyingPlaybackContext)
            return;

        m_PlaybackContextDirty = true;
        if (m_PlaybackContextSaveScheduled)
            return;

        m_PlaybackContextSaveScheduled = true;
        m_PlaybackContextNextFlushAt = EditorApplication.timeSinceStartup + 0.35;
        if (rootVisualElement != null && rootVisualElement.panel != null)
        {
            m_PlaybackContextSaveTask?.Pause();
            m_PlaybackContextSaveTask = rootVisualElement.schedule.Execute(FlushPlaybackContextSave);
            m_PlaybackContextSaveTask.ExecuteLater(350);
        }
        else
        {
            EditorApplication.delayCall -= FlushPlaybackContextSave;
            EditorApplication.delayCall += FlushPlaybackContextSave;
        }
    }

    private void LoadPlaybackContext()
    {
        if (!HasPersistedPlaybackScope())
            return;

        CancelPlaybackContextSave();
        string scope = PersistedSelectionScope;
        string prefix = PersistedSelectionPrefix + scope;
        cursorTime = EditorPrefs.GetFloat(prefix + CursorTimeSuffix, 0f);
        startScale = EditorPrefs.GetFloat(prefix + StartScaleSuffix, 0f);
        endScale = EditorPrefs.GetFloat(prefix + EndScaleSuffix, 1f);
        ClampHorizontalScaleRange(startScale, endScale, out startScale, out endScale);
        m_LastSavedCursorTime = cursorTime;
        m_LastSavedStartScale = startScale;
        m_LastSavedEndScale = endScale;
        m_PlaybackContextDirty = false;
    }

    private void ApplyPlaybackContext()
    {
        if (Sequence == null)
            return;

        m_ApplyingPlaybackContext = true;
        try
        {
            float duration = EditorTimelinePlayer.Instance?.ActiveSequence?.Duration ?? TotalTime;
            cursorTime = Mathf.Clamp(cursorTime, 0f, Mathf.Max(0f, duration));
            EditorTimelinePlayer.Instance?.SetTime(cursorTime);
            MoveTimeCursor(cursorTime);

            ClampHorizontalScaleRange(startScale, endScale, out startScale, out endScale);
            showScale = 1f / Mathf.Max(MinHorizontalScaleSpan, Mathf.Abs(startScale - endScale));
            pixelPerSecond = standPixelPerSecond * showScale;
            ApplyStartEndToUISlider(startScale, endScale);
        }
        finally
        {
            m_ApplyingPlaybackContext = false;
        }
    }

    private void FlushPlaybackContextSave()
    {
        m_PlaybackContextSaveTask?.Pause();
        m_PlaybackContextSaveTask = null;
        EditorApplication.delayCall -= FlushPlaybackContextSave;
        m_PlaybackContextSaveScheduled = false;

        if (this == null || rootVisualElement == null || window != this)
        {
            m_PlaybackContextDirty = false;
            return;
        }

        if (!m_PlaybackContextDirty || !HasPersistedPlaybackScope())
        {
            m_PlaybackContextDirty = false;
            m_PlaybackContextSaveScheduled = false;
            return;
        }

        double remaining = m_PlaybackContextNextFlushAt - EditorApplication.timeSinceStartup;
        if (remaining > 0d)
        {
            m_PlaybackContextSaveScheduled = true;
            EditorApplication.delayCall -= FlushPlaybackContextSave;
            EditorApplication.delayCall += FlushPlaybackContextSave;
            return;
        }

        string prefix = PersistedSelectionPrefix + PersistedSelectionScope;
        if (!Mathf.Approximately(cursorTime, m_LastSavedCursorTime))
        {
            EditorPrefs.SetFloat(prefix + CursorTimeSuffix, cursorTime);
            m_LastSavedCursorTime = cursorTime;
        }
        if (!Mathf.Approximately(startScale, m_LastSavedStartScale))
        {
            EditorPrefs.SetFloat(prefix + StartScaleSuffix, startScale);
            m_LastSavedStartScale = startScale;
        }
        if (!Mathf.Approximately(endScale, m_LastSavedEndScale))
        {
            EditorPrefs.SetFloat(prefix + EndScaleSuffix, endScale);
            m_LastSavedEndScale = endScale;
        }
        m_PlaybackContextDirty = false;
        m_PlaybackContextSaveScheduled = false;
    }

    private void ForceFlushPlaybackContextSave()
    {
        m_PlaybackContextNextFlushAt = 0d;
        FlushPlaybackContextSave();
    }

    private void CancelPlaybackContextSave()
    {
        m_PlaybackContextSaveTask?.Pause();
        m_PlaybackContextSaveTask = null;
        EditorApplication.delayCall -= FlushPlaybackContextSave;
        m_PlaybackContextSaveScheduled = false;
    }

    private bool HasPersistedPlaybackScope()
    {
        return !string.IsNullOrEmpty(m_TrackContainerAssetGuid)
               || !string.IsNullOrEmpty(m_TrackContainerAssetPath);
    }

    private void RestoreSerializedSelection()
    {
        if (Items == null || Items.Count == 0)
            return;

        if (m_SelectedTrackIndex < 0)
            LoadPersistedSelection();

        ESEditorTrackItem track = null;
            if (!string.IsNullOrEmpty(m_SelectedTrackId))
            {
                track = FindTrackByStableId(m_SelectedTrackId);
                if (track == null)
                {
                    // 稳定身份暂时不可见时不要清空持久选择。资产恢复、异步重建或
                    // 外部 Inspector 写回后，保留 ID 才能在下一次投影重建中重新定位；
                    // 这里禁止退回索引，避免把选择静默绑定到另一条轨道。
                    m_SelectedTrackIndex = -1;
                    m_SelectedClipIndex = -1;
                    return;
                }

            m_SelectedTrackIndex = Items.IndexOf(track);
        }
        else if (m_SelectedTrackIndex >= 0 && m_SelectedTrackIndex < Items.Count)
        {
            track = Items[m_SelectedTrackIndex];
        }
        else
        {
            m_SelectedTrackIndex = -1;
            m_SelectedClipIndex = -1;
            m_SelectedTrackId = string.Empty;
            m_SelectedClipId = string.Empty;
            return;
        }

        if (track == null || track.item == null)
            return;

        if (!string.IsNullOrEmpty(m_SelectedClipId))
        {
            ESEditorTrackClip clip = FindClipByStableId(track, m_SelectedClipId);
            if (clip != null && clip.trackClip != null)
            {
                SelectClip(clip, false);
                return;
            }
        }
        else if (m_SelectedClipIndex >= 0 && track.TrackClips != null && m_SelectedClipIndex < track.TrackClips.Count)
        {
            ESEditorTrackClip clip = track.TrackClips[m_SelectedClipIndex];
            if (clip != null && clip.trackClip != null)
            {
                SelectClip(clip, false);
                return;
            }
        }

        SelectTrack(track);
    }

    private ESEditorTrackItem FindTrackByStableId(string trackId)
    {
        if (string.IsNullOrEmpty(trackId) || Items == null)
            return null;

        for (int i = 0; i < Items.Count; i++)
        {
            ESEditorTrackItem item = Items[i];
            if (item != null
                && item.item is IStableTrackItem stable
                && string.Equals(stable.TrackId, trackId, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private ESEditorTrackClip FindClipByStableId(ESEditorTrackItem track, string clipId)
    {
        if (track == null || string.IsNullOrEmpty(clipId) || track.TrackClips == null)
            return null;

        for (int i = 0; i < track.TrackClips.Count; i++)
        {
            ESEditorTrackClip clip = track.TrackClips[i];
            if (clip != null
                && clip.trackClip is IStableTrackClip stable
                && string.Equals(stable.ClipId, clipId, StringComparison.Ordinal))
            {
                return clip;
            }
        }

        return null;
    }

    private static string GetStableTrackId(ITrackItem item)
    {
        if (item is IStableTrackItem stable)
        {
            if (stable.TrackSchema <= ESTrackIdentity.CurrentTrackSchema
                && !ESTrackIdentity.IsValidStableId(stable.TrackId))
                stable.EnsureStableTrackIdentity();
            return stable.TrackId;
        }

        return string.Empty;
    }

    internal bool IsTrackCollapsed(ITrackItem track)
    {
        string trackId = GetStableTrackId(track);
        return !string.IsNullOrEmpty(trackId)
            && m_CollapsedTrackIds != null
            && m_CollapsedTrackIds.Contains(trackId);
    }

    internal void SetTrackCollapsedState(ITrackItem track, bool collapsed)
    {
        string trackId = GetStableTrackId(track);
        if (string.IsNullOrEmpty(trackId))
            return;

        if (m_CollapsedTrackIds == null)
            m_CollapsedTrackIds = new List<string>();

        bool changed;
        if (collapsed)
        {
            changed = !m_CollapsedTrackIds.Contains(trackId);
            if (changed)
                m_CollapsedTrackIds.Add(trackId);
        }
        else
        {
            changed = m_CollapsedTrackIds.Remove(trackId);
        }

        if (changed)
            EditorUtility.SetDirty(this);
    }

    private static string GetStableClipId(ITrackClip clip)
    {
        if (clip is IStableTrackClip stable)
        {
            if (stable.ClipSchema <= ESTrackIdentity.CurrentClipSchema
                && !ESTrackIdentity.IsValidStableId(stable.ClipId))
                stable.EnsureStableClipIdentity();
            return stable.ClipId;
        }

        return string.Empty;
    }

    private static void ResetClipIdentityForPaste(ITrackClip clip)
    {
        if (clip is IStableTrackClip stable)
        {
            stable.ClipId = ESTrackIdentity.NewClipId();
            stable.EnsureStableClipIdentity();
        }
    }

    private static bool EnsureSequenceStableTrackIdentity(
        ITrackSequence sequence,
        out int unsupportedTrackCount,
        out int unsupportedClipCount,
        out bool futureSchemaBlocked)
    {
        unsupportedTrackCount = 0;
        unsupportedClipCount = 0;
        futureSchemaBlocked = false;
        if (sequence == null)
            return false;

        if (ESTrackIdentity.HasFutureSchema(
                sequence,
                out int futureTrackCount,
                out int futureClipCount))
        {
            futureSchemaBlocked = true;
            ESTrackIdentity.ValidateSequenceIdentity(
                sequence,
                out _,
                out _,
                out unsupportedTrackCount,
                out unsupportedClipCount);
            Debug.LogError(
                "[轨道编辑器] 当前时间轴包含未来版本 Schema，已阻断自动迁移，避免旧编辑器覆盖新版本资产。"
                + " Track=" + futureTrackCount.ToString(CultureInfo.InvariantCulture)
                + ", Clip=" + futureClipCount.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        bool needsRepair = ESTrackIdentity.ValidateSequenceIdentity(
            sequence,
            out int preTrackIssues,
            out int preClipIssues,
            out unsupportedTrackCount,
            out unsupportedClipCount);
        if (!needsRepair)
            return false;

        UnityEngine.Object target = TrackContainer as UnityEngine.Object;
        if (target == null)
        {
            Debug.LogWarning("[轨道编辑器] 当前时间轴不是 UnityEngine.Object，稳定身份迁移无法持久化，已跳过自动修复。");
            return false;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.RegisterCompleteObjectUndo(target, "迁移 Track/Clip 稳定身份");

        int trackRepairs;
        int clipRepairs;
        bool changed;
        try
        {
            changed = ESTrackIdentity.RepairSequenceIdentity(
                sequence,
                out trackRepairs,
                out clipRepairs,
                out unsupportedTrackCount,
                out unsupportedClipCount);
        }
        catch (Exception e)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogException(e);
            return false;
        }
        if (!changed)
        {
            Undo.CollapseUndoOperations(undoGroup);
            return false;
        }

        int postTrackIssues;
        int postClipIssues;
        bool postClean = !ESTrackIdentity.ValidateSequenceIdentity(
            sequence,
            out postTrackIssues,
            out postClipIssues,
            out _,
            out _);
        bool countsMatch = trackRepairs <= preTrackIssues && clipRepairs <= preClipIssues;
        if (!postClean || !countsMatch)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogError(
                "[轨道编辑器] Track/Clip 稳定身份迁移复核失败，已回滚。"
                + " PreTrack=" + preTrackIssues.ToString(CultureInfo.InvariantCulture)
                + " RepairTrack=" + trackRepairs.ToString(CultureInfo.InvariantCulture)
                + " PostTrack=" + postTrackIssues.ToString(CultureInfo.InvariantCulture)
                + " PreClip=" + preClipIssues.ToString(CultureInfo.InvariantCulture)
                + " RepairClip=" + clipRepairs.ToString(CultureInfo.InvariantCulture)
                + " PostClip=" + postClipIssues.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        Undo.CollapseUndoOperations(undoGroup);
        ESTrackViewWindowHelper.SaveContainerChanges("迁移 Track/Clip 稳定身份");
        return true;
    }

    private void RemoveClipFromSelection(ESEditorTrackClip clip)
    {
        if (clip == null)
            return;

        if (m_SelectedClips.Remove(clip))
            clip.SetSelected(false);

        if (SelectedClip == clip)
            SelectedClip = m_SelectedClips.FirstOrDefault();

        if (SelectedClip != null && Items != null)
        {
            ESEditorTrackItem selectedTrack = Items.FirstOrDefault(item => item != null && item.TrackClips != null && item.TrackClips.Contains(SelectedClip));
            m_SelectedTrackIndex = selectedTrack != null ? Items.IndexOf(selectedTrack) : -1;
            m_SelectedClipIndex = selectedTrack != null && selectedTrack.TrackClips != null ? selectedTrack.TrackClips.IndexOf(SelectedClip) : -1;
            m_SelectedTrackId = selectedTrack != null ? GetStableTrackId(selectedTrack.item) : string.Empty;
            m_SelectedClipId = SelectedClip != null ? GetStableClipId(SelectedClip.trackClip) : string.Empty;
        }
        else
        {
            m_SelectedClipIndex = -1;
            m_SelectedTrackId = string.Empty;
            m_SelectedClipId = string.Empty;
        }

        if (SelectedClip != null)
        {
            RefreshClipSelectionVisuals();
            if (m_EmbeddedInspectorClip == clip)
                SetClipInspectorTarget(SelectedClip, false);
        }
        else if (m_EmbeddedInspectorClip == clip)
        {
            ClearEmbeddedInspector();
        }
        SavePersistedSelection();
    }

    private void RefreshClipSelectionVisuals()
    {
        foreach (ESEditorTrackClip selected in m_SelectedClips)
        {
            if (selected != null)
                selected.SetSelected(true, selected == SelectedClip);
        }
    }

    private void SelectAllClips()
    {
        ClearClipSelection();
        if (Items == null)
            return;

        ESEditorTrackClip lastSelected = null;

        for (int i = 0; i < Items.Count; i++)
        {
            ESEditorTrackItem item = Items[i];
            if (item == null || item.TrackClips == null)
                continue;

            for (int j = 0; j < item.TrackClips.Count; j++)
            {
                ESEditorTrackClip clip = item.TrackClips[j];
                if (clip == null)
                    continue;

                clip.SetSelected(true);
                m_SelectedClips.Add(clip);
                lastSelected = clip;
            }
        }

        SelectedClip = lastSelected;
        m_SelectedTrackIndex = -1;
        m_SelectedClipIndex = -1;
        m_SelectedTrackId = string.Empty;
        m_SelectedClipId = string.Empty;
        SavePersistedSelection();
        RefreshClipSelectionVisuals();
        if (SelectedClip != null)
            SetClipInspectorTarget(SelectedClip, false);
    }

    private void CopySelectedClipsToClipboard()
    {
        ESEditorTrackClip context = SelectedClip != null && m_SelectedClips.Contains(SelectedClip)
            ? SelectedClip
            : m_SelectedClips.FirstOrDefault();

        if (context != null)
            CopyClipToClipboard(context);
    }

    private void PasteFromShortcut()
    {
        if (CanPasteCopiedClipsToOriginalTracks())
        {
            PasteCopiedClipsToOriginalTracks(1f, true);
            return;
        }

        if (SelectedTrackItem != null && CanPasteClipToTrack(SelectedTrackItem))
            PasteClipToTrack(SelectedTrackItem, cursorTime, true);
    }

    private void DeleteSelectedClips()
    {
        if (m_SelectedClips.Count == 0)
            return;

        List<ESEditorTrackClip> clips = m_SelectedClips
            .Where(i => i != null && i.trackClip != null)
            .ToList();
        if (clips.Count == 0)
            return;

        if (TrackContainer is UnityEngine.Object undoTarget)
            Undo.RecordObject(undoTarget, clips.Count > 1 ? "删除选中片段" : "删除片段");

        for (int i = 0; i < clips.Count; i++)
            RemoveClipEditorFromSequence(clips[i]);

        ClearClipSelection();
        ApplyAuthoringChange(
            null,
            ESTrackAuthoringChangeFlags.StructuralEdit,
            clips.Count > 1 ? "删除选中片段" : "删除片段");
    }

    private bool RemoveClipEditorFromSequence(ESEditorTrackClip clip)
    {
        if (clip == null || clip.trackClip == null || Items == null)
            return false;

        for (int i = 0; i < Items.Count; i++)
        {
            ESEditorTrackItem trackItemEditor = Items[i];
            if (trackItemEditor == null || trackItemEditor.item == null)
                continue;

            if (!trackItemEditor.TrackClips.Contains(clip))
                continue;

            trackItemEditor.RemoveClip(clip);
            trackItemEditor.MarkVisibilityCacheDirty();
            return true;
        }

        return false;
    }

    private void AlignSelectedClipsToPlayhead()
    {
        if (m_SelectedClips.Count == 0)
            return;

        List<ESEditorTrackClip> clips = m_SelectedClips
            .Where(i => i != null && i.trackClip != null)
            .OrderBy(i => i.trackClip.StartTime)
            .ToList();
        if (clips.Count == 0)
            return;

        ESEditorTrackClip anchor = SelectedClip != null && clips.Contains(SelectedClip)
            ? SelectedClip
            : clips[0];
        float delta = cursorTime - anchor.trackClip.StartTime;
        if (Mathf.Approximately(delta, 0f))
            return;

        if (TrackContainer is UnityEngine.Object undoTarget)
            Undo.RecordObject(undoTarget, clips.Count > 1 ? "对齐选中片段到播放头" : "对齐片段到播放头");

        for (int i = 0; i < clips.Count; i++)
        {
            ESEditorTrackClip clip = clips[i];
            clip.trackClip.StartTime = Mathf.Max(0f, clip.trackClip.StartTime + delta);
            clip.SetTimeScaleAndStartShowCache();
        }

        RefreshEditedTracksAfterClipChanges();
    }

    public void BeginClipGroupDrag(ESEditorTrackClip anchor)
    {
        m_GroupDragStartTimes.Clear();
        m_GroupDragAnchor = null;
        if (anchor == null || anchor.trackClip == null || !m_SelectedClips.Contains(anchor))
            return;

        if (TrackContainer is UnityEngine.Object undoTarget)
            Undo.RecordObject(undoTarget, m_SelectedClips.Count > 1 ? "批量移动片段" : "移动片段");

        m_GroupDragAnchor = anchor;
        m_GroupDragAnchorStartTime = anchor.StartTime;
        foreach (ESEditorTrackClip clip in m_SelectedClips)
        {
            if (clip != null && clip.trackClip != null)
                m_GroupDragStartTimes[clip] = clip.trackClip.StartTime;
        }
    }

    public void ApplyClipGroupDrag(ESEditorTrackClip anchor, float anchorStartTime)
    {
        if (m_IsApplyingGroupDrag || anchor == null || anchor != m_GroupDragAnchor || m_GroupDragStartTimes.Count <= 1)
            return;

        float deltaTime = anchorStartTime - m_GroupDragAnchorStartTime;
        if (Mathf.Approximately(deltaTime, 0f))
            return;

        m_IsApplyingGroupDrag = true;
        try
        {
            foreach (KeyValuePair<ESEditorTrackClip, float> pair in m_GroupDragStartTimes)
            {
                ESEditorTrackClip clip = pair.Key;
                if (clip == null || clip == anchor || clip.trackClip == null)
                    continue;

                clip.trackClip.StartTime = Mathf.Max(0f, pair.Value + deltaTime);
                clip.SetTimeScaleAndStartShowCache();
            }
        }
        finally
        {
            m_IsApplyingGroupDrag = false;
        }
    }

    public void EndClipGroupDrag(ESEditorTrackClip anchor)
    {
        if (anchor != m_GroupDragAnchor)
            return;

        bool changed = false;
        foreach (KeyValuePair<ESEditorTrackClip, float> pair in m_GroupDragStartTimes)
        {
            if (pair.Key != null && pair.Key.trackClip != null && !Mathf.Approximately(pair.Key.StartTime, pair.Value))
            {
                changed = true;
                break;
            }
        }

        m_GroupDragStartTimes.Clear();
        m_GroupDragAnchor = null;

        if (changed)
            RefreshEditedTracksAfterClipChanges();
    }

    public void BeginClipResize(ESEditorTrackClip clip)
    {
        if (clip == null || clip.trackClip == null)
            return;

        if (TrackContainer is UnityEngine.Object undoTarget)
            Undo.RecordObject(undoTarget, "调整片段长度");
    }

    public void EndClipResize(ESEditorTrackClip clip, float initialDuration)
    {
        if (clip == null || clip.trackClip == null || Mathf.Approximately(clip.Duration, initialDuration))
            return;

        clip.SetTimeScaleAndStartShowCache();
        ApplyAuthoringChange(
            clip.trackClip,
            ESTrackAuthoringChangeFlags.InspectorEdit,
            "调整片段时长");
    }

    private void RefreshEditedTracksAfterClipChanges()
    {
        foreach (ESEditorTrackItem item in Items)
        {
            if (item == null || item.item == null)
                continue;

            item.item.SortClipsByTime();
            item.MarkVisibilityCacheDirty();
            item.UpdateNodeMatchAndForeachUpdate(true);
        }

        ApplyAuthoringChange(
            null,
            ESTrackAuthoringChangeFlags.StructuralEdit,
            "片段时间或排序修改");
    }

    private void NudgeSelectedClips(float delta)
    {
        if (SelectedClip == null || m_SelectedClips.Count == 0)
            return;
        if (!(TrackContainer is UnityEngine.Object undoTarget))
            return;

        Undo.RecordObject(undoTarget, "方向键微调片段");
        bool changed = false;
        foreach (ESEditorTrackClip editorClip in m_SelectedClips)
        {
            ITrackClip clip = editorClip != null ? editorClip.trackClip : null;
            if (clip == null)
                continue;

            float newStart = Mathf.Max(0f, clip.StartTime + delta);
            if (!Mathf.Approximately(newStart, clip.StartTime))
            {
                clip.StartTime = newStart;
                changed = true;
            }
        }

        if (!changed)
            return;

        RefreshEditedTracksAfterClipChanges();
        ForceRefreshClipLayoutNow();
    }

    public void MarkAllTrackVisibilityCachesDirty()
    {
        if (Items == null)
            return;

        for (int i = 0; i < Items.Count; i++)
            Items[i]?.MarkVisibilityCacheDirty();
    }

    public void ForceRefreshClipLayoutNow()
    {
        if (rootVisualElement == null || ruler == null || rightPanel == null || leftPanel == null)
            return;

        MarkAllTrackVisibilityCachesDirty();
        UpdateClipsSimple(ESTrackClipUpdateFlags.All);
        MoveTimeCursor(cursorTime);
    }

    public void SetRenamingClip(ESEditorTrackClip clip)
    {
        if (clip == null)
            return;

        ESEditorTrackClip previousClip = RenamingClip;
        ESEditorTrackItem previousTrack = RenamingTrack;
        if (previousClip != null && previousClip != clip)
            previousClip.CommitRenameBeforeLayoutMutation();
        previousTrack?.CommitRenameBeforeLayoutMutation();

        RenamingClip = clip;
        RenamingTrack = null;
    }

    public void ClearRenamingClip(ESEditorTrackClip clip)
    {
        if (RenamingClip == clip)
            RenamingClip = null;
    }

    public void SetRenamingTrack(ESEditorTrackItem track)
    {
        if (track == null)
            return;

        ESEditorTrackClip previousClip = RenamingClip;
        ESEditorTrackItem previousTrack = RenamingTrack;
        previousClip?.CommitRenameBeforeLayoutMutation();
        if (previousTrack != null && previousTrack != track)
            previousTrack.CommitRenameBeforeLayoutMutation();

        RenamingTrack = track;
        RenamingClip = null;
    }

    public void ClearRenamingTrack(ESEditorTrackItem track)
    {
        if (RenamingTrack == track)
            RenamingTrack = null;
    }

    internal void CommitActiveRenameBeforeLayoutMutation()
    {
        // 先截取引用：提交会通过 ClearRenamingXxx 清理窗口状态。
        // 所有会改变轨道几何结构的操作都从这里收口，避免输入框随折叠、排序等操作悬空。
        ESEditorTrackClip renamingClip = RenamingClip;
        ESEditorTrackItem renamingTrack = RenamingTrack;
        renamingClip?.CommitRenameBeforeLayoutMutation();
        renamingTrack?.CommitRenameBeforeLayoutMutation();
    }

    private void ReselectTrack(ITrackItem trackItem)
    {
        if (trackItem == null)
            return;

        var editorTrack = Items.FirstOrDefault(item => item.item == trackItem);
        if (editorTrack != null)
            SelectTrack(editorTrack);
    }

    private void OnTrackWindowKeyDown(KeyDownEvent evt)
    {
        if (IsEventTargetInsideInspector(evt))
            return;

        if (IsTextInputEventTarget(evt))
            return;

        bool command = evt.ctrlKey || evt.commandKey;
        if (command)
        {
            if (evt.keyCode == KeyCode.C)
            {
                CopySelectedClipsToClipboard();
                evt.PreventDefault();
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.keyCode == KeyCode.V)
            {
                PasteFromShortcut();
                evt.PreventDefault();
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.keyCode == KeyCode.A)
            {
                SelectAllClips();
                evt.PreventDefault();
                evt.StopImmediatePropagation();
                return;
            }
        }

        if (evt.keyCode == KeyCode.Escape)
        {
            ClearClipSelection();
            evt.PreventDefault();
            evt.StopImmediatePropagation();
            return;
        }

        if (evt.keyCode == KeyCode.F2)
        {
            if (SelectedClip != null)
                SelectedClip.BeginRenameFromContext();
            else
                SelectedTrackItem?.BeginRenameFromContext();

            if (SelectedClip != null || SelectedTrackItem != null)
            {
                evt.PreventDefault();
                evt.StopImmediatePropagation();
            }
            return;
        }

        if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
        {
            DeleteSelectedClips();
            evt.PreventDefault();
            evt.StopImmediatePropagation();
            return;
        }

        bool left = evt.keyCode == KeyCode.LeftArrow;
        bool right = evt.keyCode == KeyCode.RightArrow;
        if ((left || right) && SelectedClip != null && !command)
        {
            float delta = evt.shiftKey ? 1f : 0.1f;
            NudgeSelectedClips(left ? -delta : delta);
            evt.PreventDefault();
            evt.StopImmediatePropagation();
            return;
        }

        bool up = evt.keyCode == KeyCode.UpArrow;
        bool down = evt.keyCode == KeyCode.DownArrow;
        if (!up && !down)
            return;

        if (!evt.shiftKey && !evt.altKey)
            return;

        if (SelectedTrackItem == null)
            return;

        ITrackItem movedTrack = SelectedTrackItem.item;
        bool moved = false;
        if (evt.shiftKey)
        {
            moved = ESTrackViewWindowHelper.MoveTrackItemInCurrentSequence(
                SelectedTrackItem,
                up ? TrackMoveCommand.StepUp : TrackMoveCommand.StepDown);
        }
        else if (evt.altKey)
        {
            moved = ESTrackViewWindowHelper.MoveTrackItemInCurrentSequence(
                SelectedTrackItem,
                up ? TrackMoveCommand.ToMovableTop : TrackMoveCommand.ToBottom);
        }

        if (!moved)
            return;

        InitNewSequenceAndOpenWindow();
        ReselectTrack(movedTrack);
        evt.PreventDefault();
        evt.StopImmediatePropagation();
    }

    private void OnTrackWindowPointerDown(PointerDownEvent evt)
    {
        if (RenamingClip != null)
            RenamingClip.CommitRenameIfPointerOutsideRenameField(evt.position);

        if (RenamingTrack != null)
            RenamingTrack.CommitRenameIfPointerOutsideRenameField(evt.position);

        // Inspector 是独立的编辑上下文。根节点的“点击空白清除 Clip”规则不能穿透到这里，
        // 否则点击任意 Odin 字段、滚动条或 Inspector 按钮都会清空目标并销毁面板。
        bool insideInspector = IsEventTargetInsideInspector(evt);
        if (evt.button == 0 && !evt.ctrlKey && !evt.commandKey
            && !insideInspector && !IsEventTargetInsideClip(evt))
            ClearClipSelection();
    }

    private static bool IsTextInputEventTarget(EventBase evt)
    {
        var element = evt.target as VisualElement;
        while (element != null)
        {
            if (element is TextField)
                return true;

            element = element.parent;
        }

        return false;
    }

    public void BeginTrackSortDrag(ESEditorTrackItem trackItem)
    {
        if (trackItem == null || trackItem.IsProtectedBasicTrack)
            return;

        SelectTrack(trackItem);
        m_DragSortingTrack = trackItem;
        m_DragTargetIndex = Items.IndexOf(trackItem);
        EnsureTrackInsertLine();
        trackItem.SetSortDragging(true);
    }

    public void UpdateTrackSortDrag(Vector2 worldMousePosition)
    {
        if (m_DragSortingTrack == null)
            return;

        int targetIndex = ResolveTrackInsertIndex(worldMousePosition);
        m_DragTargetIndex = targetIndex;
        ShowTrackInsertLine(targetIndex);
    }

    public void EndTrackSortDrag(bool commit)
    {
        if (m_DragSortingTrack == null)
            return;

        ESEditorTrackItem draggedTrack = m_DragSortingTrack;
        ITrackItem movedTrack = draggedTrack.item;
        draggedTrack.SetSortDragging(false);
        HideTrackInsertLine();
        m_DragSortingTrack = null;

        if (!commit)
            return;

        bool moved = ESTrackViewWindowHelper.MoveTrackItemToIndexInCurrentSequence(draggedTrack, m_DragTargetIndex);
        if (!moved)
            return;

        InitNewSequenceAndOpenWindow();
        ReselectTrack(movedTrack);
    }

    private int ResolveTrackInsertIndex(Vector2 worldMousePosition)
    {
        if (Items == null || Items.Count == 0)
            return ESTrackViewIconUtility.ProtectedBasicTrackCount;

        float localY = leftPanel.WorldToLocal(worldMousePosition).y;
        for (int i = 0; i < Items.Count; i++)
        {
            ESEditorTrackItem item = Items[i];
            if (item == null)
                continue;

            if (localY < item.layout.center.y)
                return ESTrackViewIconUtility.ClampUserTrackInsertIndex(i, Items.Count);
        }

        return ESTrackViewIconUtility.ClampUserTrackInsertIndex(Items.Count, Items.Count);
    }

    private void EnsureTrackInsertLine()
    {
        if (m_TrackInsertLine != null)
            return;

        m_TrackInsertLine = new VisualElement
        {
            name = "track-sort-insert-line",
            pickingMode = PickingMode.Ignore
        };
        m_TrackInsertLine.style.position = Position.Absolute;
        m_TrackInsertLine.style.left = 0;
        m_TrackInsertLine.style.right = 0;
        m_TrackInsertLine.style.height = 2;
        m_TrackInsertLine.style.backgroundColor = ESTrackViewTheme.TrackInsertAccent;
        m_TrackInsertLine.style.display = DisplayStyle.None;
        leftPanel.Add(m_TrackInsertLine);
        m_TrackInsertLine.BringToFront();
    }

    private void ShowTrackInsertLine(int targetIndex)
    {
        EnsureTrackInsertLine();
        float y;
        if (targetIndex >= Items.Count)
        {
            var last = Items.Count > 0 ? Items[Items.Count - 1] : null;
            y = last != null ? last.layout.yMax : 0f;
        }
        else
        {
            y = Items[targetIndex].layout.y;
        }

        m_TrackInsertLine.style.top = y;
        m_TrackInsertLine.style.display = DisplayStyle.Flex;
        m_TrackInsertLine.BringToFront();
    }

    private void HideTrackInsertLine()
    {
        if (m_TrackInsertLine != null)
            m_TrackInsertLine.style.display = DisplayStyle.None;
    }

    private void OnTrackWindowSelectionChanged()
    {
        if (ESTrackViewWindowHelper.AutoFollowPreviewEntity)
            UpdatePreselectEntityFromSelection(false);
    }

    private void RefreshPreselectEntityDelayed()
    {
        if (this == null)
            return;

        UpdatePreselectEntityFromSelection(false);
    }

    private void RestoreRememberedPreviewEntity()
    {
        if (PreselectEntity != null)
            return;

        Entity remembered = EditorRememberedEntityTarget.TrackPreview.ResolveOrSceneFallback();
        if (remembered != null)
            SetPreselectEntity(remembered);
        else
            RefreshEntityDisplay();
    }

    public void SealRunningEntityForPlay()
    {
        UpdatePreselectEntityFromSelection(false);
        if (PreselectEntity == null)
            PreselectEntity = EditorRememberedEntityTarget.TrackPreview.ResolveOrSceneFallback();

        RunningEntity = PreselectEntity;
        if (RunningEntity == null)
            Debug.LogWarning("[轨道编辑器] 开始预览时使用者为空。请选择带 Entity 的对象，或从实体菜单中选择。");

        var activeSequence = EditorTimelinePlayer.Instance.ActiveSequence;
        float keepTime = activeSequence != null ? activeSequence.CurrentTime : cursorTime;
        float keepSpeed = activeSequence != null ? activeSequence.Speed : 1f;

        if (Sequence != null)
        {
            var rebuiltPlayer = BuildSequencePlayer(Sequence, RunningEntity);
            rebuiltPlayer.Speed = keepSpeed;
            EditorTimelinePlayer.Instance.ActiveSequence = rebuiltPlayer;
            rebuiltPlayer.SetTime(keepTime);
        }
        else if (activeSequence != null && activeSequence.PreviewTarget != null && !activeSequence.PreviewTarget.IsRecycled)
        {
            EditorRememberedEntityTarget.TrackPreview.FillPreviewTarget(activeSequence.PreviewTarget, RunningEntity);
        }

        RefreshEntityDisplay();
    }

    public bool TryStartPreview()
    {
        if (TrackContainer == null || Sequence == null)
        {
            EditorUtility.DisplayDialog("无法开始预览", "当前没有打开时间轴资产。请先选择时间轴，再开始预览。", "确定");
            return false;
        }

        try
        {
            FlushPendingPreviewRebuildNow();
            SealRunningEntityForPlay();
            EditorSequencePlayer player = EditorTimelinePlayer.Instance.ActiveSequence;
            if (player == null)
            {
                EditorUtility.DisplayDialog("无法开始预览", "预览播放器没有成功创建。请检查当前轨道类型和 Console 错误。", "确定");
                return false;
            }

            EditorTimelinePlayer.Instance.Play();
            if (toolbar?.PreviewButton != null)
            {
                toolbar.PreviewButton.tooltip = RunningEntity != null
                    ? "正在预览；当前使用者：" + RunningEntity.name
                    : "正在无使用者上下文预览；需要 Entity 的轨道可能不会产生完整表现。";
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("预览启动失败", "开始预览时发生异常。请查看 Console，并检查轨道预览依赖。", "确定");
            return false;
        }
    }

    private void SchedulePreviewRebuild()
    {
        if (this == null || rootVisualElement == null || Sequence == null)
            return;

        // Inspector 的拖动、滚轮和连续键盘输入会在多个编辑器帧内持续触发变更。
        // 节点外观仍即时更新，但完整 Preview Player 只在输入短暂停顿后重建，
        // 避免连续数值调整时每帧重新编译整条 Sequence。
        m_PreviewRebuildDueAt = EditorApplication.timeSinceStartup + InspectorPreviewRebuildIdleSeconds;
        if (m_PreviewRebuildScheduled)
            return;

        m_PreviewRebuildScheduled = true;
        EditorApplication.update -= FlushScheduledPreviewRebuild;
        EditorApplication.update += FlushScheduledPreviewRebuild;
    }

    private void FlushScheduledPreviewRebuild()
    {
        if (!m_PreviewRebuildScheduled)
            return;
        if (EditorApplication.timeSinceStartup < m_PreviewRebuildDueAt)
            return;

        EditorApplication.update -= FlushScheduledPreviewRebuild;
        m_PreviewRebuildScheduled = false;
        if (this == null || rootVisualElement == null || window != this || Sequence == null)
            return;

        RebuildActivePreviewPlayer();
        ForceRefreshClipLayoutNow();
    }

    private void FlushPendingPreviewRebuildNow()
    {
        if (!m_PreviewRebuildScheduled)
            return;

        EditorApplication.update -= FlushScheduledPreviewRebuild;
        m_PreviewRebuildScheduled = false;
        if (this == null || rootVisualElement == null || window != this || Sequence == null)
            return;

        RebuildActivePreviewPlayer();
        ForceRefreshClipLayoutNow();
    }

    public void RebuildActivePreviewPlayer()
    {
        if (Sequence == null)
            return;

        var activeSequence = EditorTimelinePlayer.Instance.ActiveSequence;
        float keepTime = activeSequence != null ? activeSequence.CurrentTime : cursorTime;
        float keepSpeed = activeSequence != null ? activeSequence.Speed : 1f;
        Entity previewEntity = RunningEntity != null ? RunningEntity : PreselectEntity;

        var rebuiltPlayer = BuildSequencePlayer(Sequence, previewEntity);
        rebuiltPlayer.Speed = keepSpeed;
        SetActivePreviewPlayerSafely(rebuiltPlayer);
        rebuiltPlayer.SetTime(keepTime);
        ScheduleAutoValidateSequenceVisuals();
    }

    private static void SetActivePreviewPlayerSafely(EditorSequencePlayer candidate)
    {
        if (candidate == null)
            return;

        try
        {
            EditorTimelinePlayer.Instance.ActiveSequence = candidate;
            candidate = null;
        }
        finally
        {
            if (candidate != null)
            {
                try
                {
                    candidate.DisposeEditorPreviewTarget();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "Track 预览播放器提交失败，临时预览对象释放也失败。", exception));
                }
            }
        }
    }

    [MenuItem(MenuItemPathDefine.CONTENT_CREATION_PATH + "技能与轨道/临时播放当前技能序列", false, 20)]
    public static void PlayCurrentSequenceAsTemporarySkillStateMenu()
    {
        PlayCurrentSequenceAsTemporarySkillState();
    }

    public static bool PlayCurrentSequenceAsTemporarySkillState(StateLayerType layer = StateLayerType.Main, bool forceEnter = false)
    {
        if (window == null)
            InitNewSequenceAndOpenWindow();

        var currentWindow = window;
        var sequence = Sequence;
        if (currentWindow == null || sequence == null)
        {
            Debug.LogWarning("[ESTrackViewWindow] 临时技能状态播放失败：当前没有打开的轨道序列。");
            return false;
        }

        currentWindow.UpdatePreselectEntityFromSelection(false);
        Entity entity = currentWindow.RunningEntity != null ? currentWindow.RunningEntity : currentWindow.PreselectEntity;
        if (entity == null || entity.stateDomain == null || entity.stateDomain.stateMachine == null)
        {
            Debug.LogWarning("[ESTrackViewWindow] 临时技能状态播放失败：未找到可用 Entity 或 StateMachine。");
            return false;
        }

        string sequenceName = !string.IsNullOrEmpty(sequence.Name) ? sequence.Name : "SkillSequence";
        string tempKey = "TrackPreview_" + sequenceName;
        StateAniDataInfo baseStateInfo = TrackContainer is SkillTrackProcessInfo skillDataInfo ? skillDataInfo.baseStateInfo : null;
        return entity.stateDomain.stateMachine.AddTemporarySkillSequence(tempKey, sequence, baseStateInfo, layer, forceEnter);
    }

    private void RefreshEntityDisplay()
    {
        toolbar?.UpdateEntity(PreselectEntity, RunningEntity);
    }

    public void ShowEntitySelectMenu()
    {
        var menu = new GenericMenu();
        Entity[] entities = UnityEngine.Object.FindObjectsByType<Entity>(FindObjectsSortMode.None);
        Array.Sort(entities, (a, b) => string.CompareOrdinal(GetEntityMenuPath(a), GetEntityMenuPath(b)));

        int addedCount = 0;
        if (entities.Length > 0)
        {
            foreach (var entity in entities)
            {
                if (entity == null || !entity.gameObject.activeInHierarchy)
                    continue;

                Entity captured = entity;
                menu.AddItem(new GUIContent(GetEntityMenuPath(entity)), entity == PreselectEntity, () =>
                {
                    SetPreselectEntity(captured);
                    Selection.activeObject = captured.gameObject;
                });
                addedCount++;
            }
        }

        if (addedCount == 0)
            menu.AddDisabledItem(new GUIContent("没有可用实体"));

        menu.ShowAsContext();
    }

    private static string GetEntityMenuPath(Entity entity)
    {
        if (entity == null)
            return "<None>";

        string sceneName = entity.gameObject.scene.IsValid() ? entity.gameObject.scene.name : "未加载场景";
        return $"{sceneName}/{GetGameObjectPath(entity.gameObject)}";
    }

    private static string GetGameObjectPath(GameObject gameObject)
    {
        if (gameObject == null)
            return "<None>";

        string path = gameObject.name;
        Transform current = gameObject.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
    private void OnSequenceTimeUpdated(float time)
    {
        if (this == null || window != this || toolbar == null)
            return;

        // 1. 更新工具栏上的时间文本
        // 假设你有一个方法能拿到工具栏引用，例如：
        window.toolbar.UpdateTime(time);

        cursorTime = time;
        // 2. 移动时间游标（如果你有实现的话）
        MoveTimeCursor(time);
        SchedulePlaybackContextSave();

        // 3. 高亮当前播放片段
        HighlightActiveClips(time);

        // 如果上述功能还没建好，直接打日志或留空即可
        // Debug.Log("当前时间: " + time);
    }
    private void MoveTimeCursor(float currentTime)
    {
        if (timeCursor == null || toolbar == null) return;

        // 当前显示区域的起始时间
        float startShow = StartShow;   // 或者 window.StartShow
                                       // 当前每像素秒数
        float pixelsPerSec = pixelPerSecond;

        // 计算游标在 rightPanel 内的 x 位置
        float xPos = LeftTrackPixel + (currentTime - startShow) * pixelsPerSec;

        timeCursor.style.left = xPos;

        toolbar.UpdateTime(currentTime);


    }

    private void SetPlayheadTime(float time)
    {
        float duration = EditorTimelinePlayer.Instance.ActiveSequence?.Duration ?? TotalTime;
        float newTime = Mathf.Clamp(time, 0f, Mathf.Max(0f, duration));
        cursorTime = newTime;
        EditorTimelinePlayer.Instance.SetTime(newTime);
        MoveTimeCursor(newTime);
        SchedulePlaybackContextSave();
    }

    private void EnsureTimeVisible(float time)
    {
        if (TotalTime <= 0.0001f)
            return;

        float visibleStart = StartShow;
        float visibleEnd = GetVisibleEndTime();
        if (time >= visibleStart && time <= visibleEnd)
            return;

        float span = Mathf.Clamp(Mathf.Abs(endScale - startScale), MinHorizontalScaleSpan, 1f);
        float centerScale = Mathf.Clamp01(time / TotalTime);
        float newStart = centerScale - span * 0.5f;
        float newEnd = centerScale + span * 0.5f;
        ClampHorizontalScaleRange(newStart, newEnd, out startScale, out endScale);
        showScale = 1 / Mathf.Abs(startScale - endScale);
        pixelPerSecond = standPixelPerSecond * showScale;
        ApplyStartEndToUISlider(startScale, endScale);
        ScheduleViewRefresh();
    }

    private void HighlightActiveClips(float currentTime)
    {
        if (window == null) return;
        foreach (var item in window.Items)
        {

            item.SetCurrentTime(currentTime);
        }
    }

    #region 游标

    // 强制结束拖动的方法


    private void OnTimeCursorMouseDown(MouseDownEvent evt)
    {
        // 仅左键
        if (evt.button != 0) return;
        // 防止事件来源不是游标自身（虽然通常是）
        if (evt.target != timeCursor && !timeCursor.Contains(evt.target as VisualElement)) return;

        isDraggingCursor = true;
        timeCursor.CaptureMouse();                     // 鼠标事件全归游标
        EditorTimelinePlayer.Instance.ActiveSequence?.Pause();
        evt.StopPropagation();
    }

    private void OnTimeCursorMouseMove(MouseMoveEvent evt)
    {
        if (!isDraggingCursor) return;

        // 自愈检查：如果左键没按了，强制结束拖动
        if ((evt.pressedButtons & 1) == 0)
        {
            ForceEndCursorDrag();
            return;
        }

        // 计算新时间
        Vector2 localPos = rightPanel.WorldToLocal(evt.mousePosition);
        float mouseX = localPos.x;
        float startShow = StartShow;
        float pixelsPerSec = pixelPerSecond;
        float newTime = startShow + (mouseX) / pixelsPerSec;
        float totalDuration = EditorTimelinePlayer.Instance.ActiveSequence?.Duration ?? 10f;
        newTime = Mathf.Clamp(newTime, 0f, totalDuration);
        cursorTime = newTime;  // 记录
        EditorTimelinePlayer.Instance.SetTime(newTime);
        MoveTimeCursor(newTime);
        SchedulePlaybackContextSave();
    }

    private void OnTimeCursorMouseUp(MouseUpEvent evt)
    {
        if (!isDraggingCursor || evt.button != 0) return;
        ForceEndCursorDrag();
        evt.StopPropagation();
    }

    private void OnTimeCursorMouseCaptureOut(MouseCaptureOutEvent evt)
    {
        if (isDraggingCursor)
            ForceEndCursorDrag();
    }

    private void ForceEndCursorDrag()
    {
        isDraggingCursor = false;
        if (timeCursor != null && timeCursor.HasMouseCapture())
            timeCursor.ReleaseMouse();
    }


    #endregion
    #endregion


    #region  水平缩放偏移
    void HorSliderChange(ChangeEvent<Vector2> change)
    { //HandleStartEndScale(0, 1);
        HandleStartEndScale(change.newValue.x, change.newValue.y);
    }
    private void HandleStartEndScale(float start, float end)
    {
        ClampHorizontalScaleRange(start, end, out startScale, out endScale);
        showScale = 1 / Mathf.Abs(startScale - endScale);
        pixelPerSecond = standPixelPerSecond * showScale;
        //Debug.Log("更新V2");
        ScheduleViewRefresh();
        SchedulePlaybackContextSave();
    }

    private void HandleVerStartEndScaleAndApply(float start, float end)
    {
        //    verScroll.hi
        float verStart = Mathf.Clamp(start, 0, 0.9f);
        //  float verEnd = Mathf.Clamp(end, start, 1f);
        float verShowScale = 1 / Mathf.Clamp(Mathf.Abs(verStart - end), 0.1f, 10);
        verScroll.scrollOffset = new Vector2(0, end);
        //         Debug.Log( verScroll.scrollOffset+"   "+new Vector2(verStart, end)+"  "+new Vector2(start, end));
        // pixelPerSecond = (standPixelPerSecond * showScale);

    }

    private void ApplyStartEndToUISlider(float start, float end)
    {
        horSlider.SetValueWithoutNotify(new Vector2(startScale, endScale));
    }
    #endregion

    #region 鼠标滚轮缩放
    private void OnScrollViewWheel(WheelEvent evt)
    {
        if (!IsEventFromRightPanel(evt))
            return;

        OnRightPanelWheel(evt);
    }

    private static void ClampHorizontalScaleRange(float start, float end, out float clampedStart, out float clampedEnd)
    {
        float center = (start + end) * 0.5f;
        float span = Mathf.Clamp(Mathf.Abs(end - start), MinHorizontalScaleSpan, 1f);

        clampedStart = center - span * 0.5f;
        clampedEnd = center + span * 0.5f;

        if (clampedStart < 0f)
        {
            clampedEnd -= clampedStart;
            clampedStart = 0f;
        }

        if (clampedEnd > 1f)
        {
            float overflow = clampedEnd - 1f;
            clampedStart -= overflow;
            clampedEnd = 1f;
        }

        clampedStart = Mathf.Clamp01(clampedStart);
        clampedEnd = Mathf.Clamp(clampedEnd, clampedStart + MinHorizontalScaleSpan, 1f);
    }

    public void OnRightPanelWheel(WheelEvent evt)
    {
        if (evt.shiftKey)
        {
            HandleZoomVerScale(evt);
        }
        else
        {
            HandleZoomHorScale(evt);
        }

        evt.PreventDefault();
        evt.StopImmediatePropagation();
    }

    private void HandleZoomHorScale(WheelEvent evt)
    {
        // 计算缩放因子
        float zoomDelta = (evt.delta.y > 0 ? -m_ZoomSensitivity : m_ZoomSensitivity) * 0.35f;
        float nowEdge = Mathf.Clamp(Mathf.Abs(startScale - endScale), 0.1f, 10);
        var tryStart = startScale + zoomDelta * nowEdge;
        var tryEnd = endScale - zoomDelta * nowEdge;
        //   Debug.Log("zoomDelta"+zoomDelta+"?"+evt.delta.y );
        HandleStartEndScale(tryStart, tryEnd);
        ApplyStartEndToUISlider(startScale, endScale);
    }


    private void HandleZoomVerScale(WheelEvent evt)
    {
        // 计算缩放因子
        float zoomDelta = (evt.delta.x > 0 ? -m_ZoomSensitivity : m_ZoomSensitivity) * 250;
        float nowEdge = Mathf.Clamp(Mathf.Abs(verScroll.scrollOffset.x - verScroll.scrollOffset.y), 0.1f, 10);


        // var tryStart = verScroll.scrollOffset.x + zoomDelta * nowEdge;
        var tryEnd = verScroll.scrollOffset.y - zoomDelta;
        //Debug.Log("zoomDelta"+zoomDelta+"?"+evt.delta.y +"" +evt.delta.x);
        HandleVerStartEndScaleAndApply(0, tryEnd);
        // ApplyStartEndToUISlider(tryStart, tryEnd);
    }
    private bool IsEventFromRightPanel(EventBase evt)
    {
        var element = evt.target as VisualElement;
        while (element != null)
        {
            if (element == rightPanel)
                return true;

            element = element.parent;
        }

        return false;
    }
    #endregion
    // private void HandleHorizontalScroll(WheelEvent evt)
    // {
    //     // 水平滚动
    //     float scrollAmount = evt.delta.y * 10f;
    //     m_CurrentPanOffset.x += scrollAmount;

    //     UpdateContentTransform();
    //     OnPanChanged?.Invoke(m_CurrentPanOffset);

    //     Debug.Log($"水平滚动: {scrollAmount:F1}, 偏移: {m_CurrentPanOffset.x:F1}");
    // }

    // private void HandleVerticalScroll(WheelEvent evt)
    // {
    //     // 垂直滚动
    //     float scrollAmount = evt.delta.y * 10f;
    //     m_CurrentPanOffset.y += scrollAmount;

    //     UpdateContentTransform();
    //     OnPanChanged?.Invoke(m_CurrentPanOffset);

    //     Debug.Log($"垂直滚动: {scrollAmount:F1}, 偏移: {m_CurrentPanOffset.y:F1}");
    // }
    // #endregion

    #region 鼠标中键拖拽
    private void OnRightPanelMouseDown(MouseDownEvent evt)
    {
        // 检查是否在 RightPanel 内
        if (!IsMouseInPanel(evt))
            return;

        // 中键：平移模式
        if (evt.button == 2) // 中键
        {
            StartPanning(evt);
            evt.StopPropagation();
        }
        // 左键：选择模式
        else if (evt.button == 0)
        {
            StartSelection(evt);
            evt.StopPropagation();
        }
    }

    private void OnRightPanelMouseMove(MouseMoveEvent evt)
    {
        // 根据当前模式处理
        switch (m_CurrentMode)
        {
            case InteractionMode.Panning:
                HandlePanning(evt);
                break;

            case InteractionMode.Selecting:
                HandleSelection(evt);
                break;
        }
    }

    private void OnRightPanelMouseUp(MouseUpEvent evt)
    {
        // 根据按钮结束对应模式
        if (evt.button == 2 && m_CurrentMode == InteractionMode.Panning)
        {
            EndPanning();
        }
        else if (evt.button == 0 && m_CurrentMode == InteractionMode.Selecting)
        {
            EndSelection();
        }

        UpdateCursor();
    }

    private void OnRightPanelMouseCaptureOut(MouseCaptureOutEvent evt)
    {
        if (m_IsPanning)
            EndPanning();
        else if (m_IsSelecting)
            EndSelection();
    }

    private void StartPanning(MouseDownEvent evt)
    {
        m_CurrentMode = InteractionMode.Panning;
        m_IsPanning = true;
        m_PanStartPosition = evt.mousePosition;

        // 捕获鼠标
        rightPanel.CaptureMouse();

        UpdateCursor();

        // Debug.Log("开始平移");
    }

    private void HandlePanning(MouseMoveEvent evt)
    {
        if (!m_IsPanning || m_CurrentMode != InteractionMode.Panning)
            return;

        // 计算移动距离
        Vector2 delta = evt.mousePosition - m_PanStartPosition;
        m_PanStartPosition = evt.mousePosition;

        // 应用平移
        float nowEdge = 0.01f * Mathf.Clamp(Mathf.Abs(startScale - endScale), 0.1f, 10);

        var offset = -delta.x * nowEdge;
        if (offset > 0)
        {
            var maxOffset = Mathf.Min(offset, 1 - endScale);
            var tryStart = startScale + maxOffset;
            var tryEnd = endScale + maxOffset;
            HandleStartEndScale(tryStart, tryEnd);
            ApplyStartEndToUISlider(tryStart, tryEnd);
        }
        else
        {
            var maxOffset = Mathf.Max(offset, -startScale);
            var tryStart = startScale + maxOffset;
            var tryEnd = endScale + maxOffset;
            HandleStartEndScale(tryStart, tryEnd);
            ApplyStartEndToUISlider(tryStart, tryEnd);
        }


        // 更新内容位置
        //UpdateContentTransform();

        // 触发事件
        // OnPanChanged?.Invoke(m_CurrentPanOffset);

        // Debug.Log($"平移: {delta}, 总偏移: {m_CurrentPanOffset}");
    }

    private void EndPanning()
    {
        m_CurrentMode = InteractionMode.None;
        m_IsPanning = false;

        // 释放鼠标捕获
        if (rightPanel.HasMouseCapture())
        {
            rightPanel.ReleaseMouse();
        }

        UpdateCursor();

        MoveTimeCursor(cursorTime);

        // Debug.Log("结束平移");
    }
    #endregion

    #region 右键菜单

    private void OnContextClick_CompleteMenu(ContextClickEvent evt)
    {
        var menu = new GenericMenu();
        AppendMenuItems_AddTrack(menu);
        if (CanPasteCopiedClipsToOriginalTracks())
        {
            menu.AddItem(new GUIContent("粘贴片段/按原轨道右移 1 秒"), false, () =>
            {
                PasteCopiedClipsToOriginalTracks(1f, true);
            });
        }
        else if (HasCopiedClips())
        {
            menu.AddDisabledItem(new GUIContent("粘贴片段/原轨道不可用"));
        }
        if (SelectedTrackItem != null && CanPasteClipToTrack(SelectedTrackItem))
        {
            ESEditorTrackItem pasteTarget = SelectedTrackItem;
            menu.AddItem(new GUIContent("粘贴片段/粘贴到所选轨道播放头"), false, () =>
            {
                PasteClipToTrack(pasteTarget, cursorTime, true);
            });
        }

        menu.AddSeparator("");
        AppendTrackVisibilityMenu(menu);
        menu.AddSeparator("");
        AppendMenuItems_Refresh(menu);

        menu.ShowAsContext();
        evt.PreventDefault();
        evt.StopImmediatePropagation();
    }

    private void ShowMenu_AddTrack()
    {
        var menu = new GenericMenu();
        AppendMenuItems_AddTrack(menu);
        menu.ShowAsContext();
    }
    [NonSerialized] public OdinEditorWindow Last_EditorWindowForTrackItem;
    [NonSerialized] public OdinEditorWindow Last_EditorWindowForTrackClip;
    [NonSerialized] public OdinEditorWindow Last_EditorWindowForSkillDataInfo;

    public void EditTrack(ESEditorTrackItem trackItem, bool forceSeparateWindow = false)
    {
        if (trackItem == null || trackItem.item == null)
            return;

        if (!forceSeparateWindow && CanEmbedInspector())
        {
            trackItem.UpdateNodeMatchAndForeachUpdate();
            trackItem.UpdateWhenEdit();
            SetTrackInspectorTarget(trackItem, true);
            return;
        }

        ESTrackClipTemporaryInspectorWindow.CloseCurrentWindow();
        Last_EditorWindowForTrackClip = null;
        ESTrackItemTemporaryInspectorWindow.CloseCurrentWindow();
        Last_EditorWindowForTrackItem = null;

        trackItem.UpdateNodeMatchAndForeachUpdate();
        trackItem.UpdateWhenEdit();
        Last_EditorWindowForTrackItem = ESTrackItemTemporaryInspectorWindow.OpenFor(
            trackItem.item,
            TrackContainer as UnityEngine.Object,
            "编辑轨道<" + trackItem.item.DisplayName + ">",
            "轨道项目",
            this);
        Last_EditorWindowForTrackItem?.Focus();
    }

    public void EditClip(ESEditorTrackClip clip, bool forceSeparateWindow = false)
    {
        if (clip == null || clip.trackClip == null)
            return;

        SetFocusedEditingClip(clip);
        if (!forceSeparateWindow && CanEmbedInspector())
        {
            clip.SetTimeScaleAndStartShowCache();
            clip.UpdateNodeView();
            SetClipInspectorTarget(clip, true);
            return;
        }

        ESTrackClipTemporaryInspectorWindow.CloseCurrentWindow();
        Last_EditorWindowForTrackClip = null;
        ESTrackItemTemporaryInspectorWindow.CloseCurrentWindow();
        Last_EditorWindowForTrackItem = null;

        clip.SetTimeScaleAndStartShowCache();
        clip.UpdateNodeView();
        Last_EditorWindowForTrackClip = ESTrackClipTemporaryInspectorWindow.OpenFor(
            clip.trackClip,
            TrackContainer as UnityEngine.Object,
            "编辑片段<" + clip.trackClip.DisplayName + ">",
            "片段",
            this);
        Last_EditorWindowForTrackClip?.Focus();
    }

    private bool CanEmbedInspector()
    {
        if (rootVisualElement == null)
            return false;

        const float inspectorDrawerWidth = 320f;
        const float timelineCanvasSafetyWidth = 480f;
        return rootVisualElement.layout.width >= MinTrackPanelWidth + timelineCanvasSafetyWidth + inspectorDrawerWidth;
    }

    public bool CanOpenCurrentInspectorInSeparateWindow
    {
        get { return TryResolveInspectorTarget(out _, out _); }
    }

    public void OpenCurrentInspectorInSeparateWindow()
    {
        // 先按当前内置 Inspector 的真实绑定目标打开，避免旧的轨道选择状态抢占 Clip。
        if (m_EmbeddedInspectorClip != null && m_EmbeddedInspectorClip.trackClip != null)
        {
            EditClip(m_EmbeddedInspectorClip, true);
            return;
        }

        if (m_EmbeddedInspectorTrack != null && m_EmbeddedInspectorTrack.item != null)
        {
            EditTrack(m_EmbeddedInspectorTrack, true);
            return;
        }

        if (TryResolveInspectorTarget(out ESEditorTrackClip clip, out ESEditorTrackItem track))
        {
            if (clip != null)
                EditClip(clip, true);
            else if (track != null)
                EditTrack(track, true);
        }
    }

    private bool TryResolveInspectorTarget(out ESEditorTrackClip clip, out ESEditorTrackItem track)
    {
        // 当前 Inspector 已绑定的 Clip 优先，其次才取选择状态；否则用户从片段切换时，
        // 顶部“弹出”可能沿用上一轮轨道目标。
        clip = m_EmbeddedInspectorClip ?? SelectedClip ?? FocusedEditingClip;
        if (clip != null && clip.trackClip != null)
        {
            track = null;
            return true;
        }

        if (m_SelectedClips != null)
        {
            foreach (ESEditorTrackClip selectedClip in m_SelectedClips)
            {
                if (selectedClip != null && selectedClip.trackClip != null)
                {
                    track = null;
                    return true;
                }
            }
        }

        track = m_EmbeddedInspectorTrack;
        if (track != null && track.item != null)
            return true;

        track = SelectedTrackItem;
        if (track != null && track.item != null)
            return true;

        clip = null;
        track = null;
        return false;
    }

    public void ShowTrackContextMenu(ESEditorTrackItem trackItem, float contextTime, string contextLabel)
    {
        if (trackItem == null || trackItem.item == null)
            return;

        SelectTrack(trackItem);
        contextTime = Mathf.Max(0f, contextTime);
        contextLabel = string.IsNullOrWhiteSpace(contextLabel) ? "指定时间" : contextLabel.Trim();

        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("编辑轨道/编辑属性"), false, () =>
        {
            EditTrack(trackItem);
        });
        menu.AddItem(new GUIContent("编辑轨道/在独立窗口中编辑"), false, () =>
        {
            EditTrack(trackItem, true);
        });
        menu.AddItem(new GUIContent("编辑轨道/重命名"), false, trackItem.BeginRenameFromContext);

        menu.AddSeparator("");
        string enabledText = trackItem.IsEnabled ? "轨道状态/禁用轨道" : "轨道状态/启用轨道";
        menu.AddItem(new GUIContent(enabledText), false, trackItem.ToggleEnabledFromContext);
        string collapseText = trackItem.IsCollapsed ? "轨道显示/展开轨道" : "轨道显示/折叠轨道";
        menu.AddItem(new GUIContent(collapseText), false, trackItem.ToggleCollapse);
        AppendTrackOrderMenu(menu, trackItem);

        menu.AddSeparator("");
        AppendMenuItems_AddClip(menu, trackItem, contextTime, contextLabel);
        AppendMenuItems_PasteClip(menu, trackItem, contextTime, contextLabel);
        if (trackItem.TrackClips != null && trackItem.TrackClips.Count > 1)
            menu.AddItem(new GUIContent("整理片段/按开始时间排序"), false, () => SortTrackClipsByTime(trackItem));
        else
            menu.AddDisabledItem(new GUIContent("整理片段/按开始时间排序（片段不足）"));

        menu.AddSeparator("");
        AppendMenuItems_AddTrack(menu);
        menu.AddSeparator("");
        if (trackItem.IsProtectedBasicTrack)
        {
            menu.AddDisabledItem(new GUIContent("删除轨道（基础轨道不可删除）"));
        }
        else
        {
            menu.AddItem(new GUIContent("删除轨道"), false, () =>
            {
                if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog(
                    "删除轨道 " + trackItem.item.DisplayName,
                    "确认删除该轨道吗？\n删除会记录为一次可撤销操作。",
                    "删除",
                    "取消"))
                {
                    ESTrackViewWindowHelper.RemoveTrackItemToCurrentSequence(trackItem);
                }
            });
        }

        menu.ShowAsContext();
    }

    public void ShowClipContextMenu(ESEditorTrackClip clip)
    {
        if (clip == null || clip.trackClip == null)
            return;

        if (!IsClipSelected(clip))
            SelectClip(clip, false);

        int selectedCount = Mathf.Max(1, m_SelectedClips.Count);
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("编辑片段/编辑属性"), false, () => EditClip(clip));
        menu.AddItem(new GUIContent("编辑片段/在独立窗口中编辑"), false, () => EditClip(clip, true));
        menu.AddItem(new GUIContent("编辑片段/重命名"), false, clip.BeginRenameFromContext);

        menu.AddSeparator("");
        string enabledText = clip.trackClip.Enabled ? "片段状态/禁用当前片段" : "片段状态/启用当前片段";
        menu.AddItem(new GUIContent(enabledText), false, clip.ToggleEnabled);
        string copyText = selectedCount > 1 ? $"复制选中片段（{selectedCount}）" : "复制片段";
        menu.AddItem(new GUIContent(copyText), false, () => CopyClipToClipboard(clip));
        string alignText = selectedCount > 1 ? $"对齐选中片段到播放头（{selectedCount}）" : "对齐片段到播放头";
        menu.AddItem(new GUIContent(alignText), false, AlignSelectedClipsToPlayhead);

        menu.AddSeparator("");
        string deleteText = selectedCount > 1 ? $"删除选中片段（{selectedCount}）" : "删除片段";
        menu.AddItem(new GUIContent(deleteText), false, () =>
        {
            string message = selectedCount > 1
                ? $"确认删除选中的 {selectedCount} 个片段吗？\n删除会记录为一次可撤销操作。"
                : "确认删除片段“" + clip.trackClip.DisplayName + "”吗？\n删除会记录为一次可撤销操作。";
            if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("删除片段", message, "删除", "取消"))
                DeleteSelectedClips();
        });
        menu.ShowAsContext();
    }

    private void AppendTrackOrderMenu(GenericMenu menu, ESEditorTrackItem trackItem)
    {
        const string root = "轨道顺序/";
        if (trackItem == null || trackItem.item == null || trackItem.IsProtectedBasicTrack)
        {
            menu.AddDisabledItem(new GUIContent(root + "基础轨道不可排序"));
            return;
        }

        if (!(Sequence is ITrackSequenceMutableOrder mutableOrder))
        {
            menu.AddDisabledItem(new GUIContent(root + "当前序列不支持排序"));
            return;
        }

        int index = mutableOrder.IndexOfTrackItem(trackItem.item);
        int firstMovableIndex = ESTrackViewIconUtility.ProtectedBasicTrackCount;
        int lastIndex = mutableOrder.TrackItemCount - 1;
        AddTrackMoveMenuItem(menu, root + "上移一层", trackItem, TrackMoveCommand.StepUp, index > firstMovableIndex);
        AddTrackMoveMenuItem(menu, root + "下移一层", trackItem, TrackMoveCommand.StepDown, index >= firstMovableIndex && index < lastIndex);
        AddTrackMoveMenuItem(menu, root + "移到用户轨道顶部", trackItem, TrackMoveCommand.ToMovableTop, index > firstMovableIndex);
        AddTrackMoveMenuItem(menu, root + "移到最底部", trackItem, TrackMoveCommand.ToBottom, index >= firstMovableIndex && index < lastIndex);
    }

    private void AddTrackMoveMenuItem(
        GenericMenu menu,
        string path,
        ESEditorTrackItem trackItem,
        TrackMoveCommand command,
        bool enabled)
    {
        if (!enabled)
        {
            menu.AddDisabledItem(new GUIContent(path));
            return;
        }

        menu.AddItem(new GUIContent(path), false, () => MoveTrackFromContext(trackItem, command));
    }

    private void MoveTrackFromContext(ESEditorTrackItem trackItem, TrackMoveCommand command)
    {
        if (trackItem == null || trackItem.item == null)
            return;

        ITrackItem movedTrack = trackItem.item;
        if (!ESTrackViewWindowHelper.MoveTrackItemInCurrentSequence(trackItem, command))
            return;

        InitNewSequenceAndOpenWindow();
        ReselectTrack(movedTrack);
    }

    private void AppendTrackVisibilityMenu(GenericMenu menu)
    {
        if (Items == null || Items.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("轨道显示/当前没有轨道"));
            return;
        }

        bool hasExpanded = Items.Any(item => item != null && !item.IsCollapsed);
        bool hasCollapsed = Items.Any(item => item != null && item.IsCollapsed);
        if (hasExpanded)
            menu.AddItem(new GUIContent("轨道显示/折叠全部轨道"), false, () => SetAllTracksCollapsed(true));
        else
            menu.AddDisabledItem(new GUIContent("轨道显示/折叠全部轨道"));

        if (hasCollapsed)
            menu.AddItem(new GUIContent("轨道显示/展开全部轨道"), false, () => SetAllTracksCollapsed(false));
        else
            menu.AddDisabledItem(new GUIContent("轨道显示/展开全部轨道"));
    }

    internal void SetAllTracksCollapsed(bool collapsed)
    {
        if (Items == null)
            return;

        CommitActiveRenameBeforeLayoutMutation();
        for (int i = 0; i < Items.Count; i++)
            Items[i]?.SetCollapsed(collapsed, false);

        UpdateTimelineContentHeight();
        ApplyTrackPanelLayout(false);
        Repaint();
    }


    public void AppendMenuItems_AddTrack(GenericMenu menu)
    {
        if (menu == null)
            return;

        if (TrackContainer == null || Sequence == null)
        {
            menu.AddDisabledItem(new GUIContent("添加轨道/请先选择时间轴资产"));
            return;
        }

        TrackItemType itemType = TrackContainer.trackItemType;
        IReadOnlyList<(string name, Type type)> values = ESTrackViewWindowHelper.GetTrackItemTypes(itemType);
        if (values.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("添加轨道/当前时间轴没有可用轨道类型"));
            return;
        }

        foreach (var entry in values)
        {
            string displayName = !string.IsNullOrWhiteSpace(entry.name)
                ? entry.name.Trim()
                : entry.type != null ? entry.type._GetTypeDisplayName()._KeepAfterByLast("/") : "未知轨道";
            if (!TryValidateTrackItemType(entry.type, out string reason))
            {
                menu.AddDisabledItem(new GUIContent("添加轨道/" + displayName + "（" + reason + "）"));
                continue;
            }

            Type capturedType = entry.type;
            menu.AddItem(new GUIContent("添加轨道/" + displayName), false, () =>
            {
                ESTrackViewWindowHelper.AddNewTrackItemToCurrentSequence(capturedType);
            });
        }
    }

    private static bool TryValidateTrackItemType(Type type, out string reason)
    {
        if (type == null)
        {
            reason = "类型缺失";
            return false;
        }
        if (type.IsAbstract || type.IsInterface)
        {
            reason = "不能实例化";
            return false;
        }
        if (!typeof(ITrackItem).IsAssignableFrom(type))
        {
            reason = "未实现 ITrackItem";
            return false;
        }
        if (type.GetConstructor(Type.EmptyTypes) == null)
        {
            reason = "缺少无参构造";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public void AppendMenuItems_AddClip(
        GenericMenu menu,
        ESEditorTrackItem forItem,
        float insertTime,
        string locationLabel)
    {
        if (menu == null || forItem == null || forItem.item == null)
            return;

        if (TrackContainer == null || Sequence == null)
        {
            menu.AddDisabledItem(new GUIContent("添加片段/当前没有有效时间轴"));
            return;
        }

        IEnumerable<Type> supportedTypes = forItem.item.SupportedClipTypes();
        if (supportedTypes == null)
        {
            menu.AddDisabledItem(new GUIContent("添加片段/当前轨道未声明片段类型"));
            return;
        }

        List<Type> types = supportedTypes.ToList();
        types.Sort((left, right) => string.Compare(
            GetTrackClipMenuDisplayName(left),
            GetTrackClipMenuDisplayName(right),
            StringComparison.Ordinal));
        if (types.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("添加片段/当前轨道未声明片段类型"));
            return;
        }

        insertTime = Mathf.Max(0f, insertTime);
        string location = string.IsNullOrWhiteSpace(locationLabel) ? "指定时间" : locationLabel.Trim();
        string locationPath = location + " " + insertTime.ToString("0.###", CultureInfo.InvariantCulture) + " 秒";
        foreach (Type type in types)
        {
            string displayName = GetTrackClipMenuDisplayName(type);
            if (!TryValidateTrackClipType(type, out string reason))
            {
                menu.AddDisabledItem(new GUIContent(
                    "添加片段/" + locationPath + "/" + displayName + "（" + reason + "）"));
                continue;
            }

            Type capturedType = type;
            float capturedTime = insertTime;
            menu.AddItem(new GUIContent("添加片段/" + locationPath + "/" + displayName), false, () =>
            {
                ITrackClip clip = Activator.CreateInstance(capturedType) as ITrackClip;
                if (clip == null)
                    return;

                clip.StartTime = capturedTime;
                GetStableClipId(clip);
                if (TrackContainer is UnityEngine.Object undoTarget)
                    Undo.RecordObject(undoTarget, "添加轨道片段");

                ESEditorTrackClip clipEditor = forItem.AddClip(clip, false);
                if (clipEditor == null)
                    return;

                SortTrackClipsByTime(forItem, false, false);
                SelectClip(clipEditor);
                ApplyAuthoringChange(
                    null,
                    ESTrackAuthoringChangeFlags.StructuralEdit,
                    "添加片段");
            });
        }
    }

    private static string GetTrackClipMenuDisplayName(Type type)
    {
        if (type == null)
            return "未知片段";

        string displayName = type._GetTypeDisplayName();
        return string.IsNullOrWhiteSpace(displayName) ? type.Name : displayName.Trim();
    }

    private static bool TryValidateTrackClipType(Type type, out string reason)
    {
        if (type == null)
        {
            reason = "类型缺失";
            return false;
        }
        if (type.IsAbstract || type.IsInterface)
        {
            reason = "不能实例化";
            return false;
        }
        if (!typeof(ITrackClip).IsAssignableFrom(type))
        {
            reason = "未实现 ITrackClip";
            return false;
        }
        if (type.GetConstructor(Type.EmptyTypes) == null)
        {
            reason = "缺少无参构造";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void AppendMenuItems_PasteClip(
        GenericMenu menu,
        ESEditorTrackItem forItem,
        float contextTime,
        string contextLabel)
    {
        if (menu == null || forItem == null || forItem.item == null)
            return;

        if (!HasCopiedClips())
        {
            menu.AddDisabledItem(new GUIContent("粘贴片段/剪贴板中没有片段"));
            return;
        }

        if (CanPasteCopiedClipsToOriginalTracks())
        {
            menu.AddItem(new GUIContent("粘贴片段/按原轨道右移 1 秒"), false, () =>
            {
                PasteCopiedClipsToOriginalTracks(1f, true);
            });
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("粘贴片段/原轨道不可用"));
        }

        if (CanPasteClipToTrack(forItem))
        {
            float capturedContextTime = Mathf.Max(0f, contextTime);
            string location = string.IsNullOrWhiteSpace(contextLabel) ? "指定时间" : contextLabel.Trim();
            menu.AddItem(new GUIContent(
                "粘贴片段/粘贴到" + location + " "
                + capturedContextTime.ToString("0.###", CultureInfo.InvariantCulture) + " 秒"), false, () =>
            {
                PasteClipToTrack(forItem, capturedContextTime, true);
            });

            if (!Mathf.Approximately(capturedContextTime, cursorTime))
            {
                menu.AddItem(new GUIContent("粘贴片段/粘贴到播放头"), false, () =>
                {
                    PasteClipToTrack(forItem, cursorTime, true);
                });
            }
        }
    }

    private void CopyClipToClipboard(ESEditorTrackClip contextClip)
    {
        List<ESEditorTrackClip> sourceClips = CollectClipsForCopy(contextClip);
        s_CopiedClips.Clear();

        for (int i = 0; i < sourceClips.Count; i++)
        {
            ESEditorTrackClip editorClip = sourceClips[i];
            if (editorClip == null || editorClip.trackClip == null)
                continue;

            ITrackClip clip = editorClip.trackClip;
            s_CopiedClips.Add(new CopiedClipPayload
            {
                data = Sirenix.Serialization.SerializationUtility.SerializeValue(clip, Sirenix.Serialization.DataFormat.Binary),
                clipType = clip.GetType(),
                startTime = clip.StartTime,
                trackIndex = GetTrackIndexForEditorClip(editorClip)
            });
        }

        if (s_CopiedClips.Count > 0)
        {
            CopiedClipPayload first = s_CopiedClips[0];
            s_CopiedClipData = first.data;
            s_CopiedClipType = first.clipType;
            s_CopiedClipStartTime = first.startTime;
        }

        Debug.Log($"[轨道编辑器] 已复制片段：{s_CopiedClips.Count}");
    }

    private List<ESEditorTrackClip> CollectClipsForCopy(ESEditorTrackClip contextClip)
    {
        List<ESEditorTrackClip> sourceClips = new List<ESEditorTrackClip>();
        if (contextClip == null)
            return sourceClips;

        if (m_SelectedClips.Contains(contextClip) && m_SelectedClips.Count > 0)
            sourceClips.AddRange(m_SelectedClips.Where(i => i != null && i.trackClip != null));
        else if (contextClip.trackClip != null)
            sourceClips.Add(contextClip);

        sourceClips.Sort((a, b) =>
        {
            int aTrack = GetTrackIndexForEditorClip(a);
            int bTrack = GetTrackIndexForEditorClip(b);
            int trackCompare = aTrack.CompareTo(bTrack);
            if (trackCompare != 0)
                return trackCompare;

            float aStart = a != null && a.trackClip != null ? a.trackClip.StartTime : float.MaxValue;
            float bStart = b != null && b.trackClip != null ? b.trackClip.StartTime : float.MaxValue;
            return aStart.CompareTo(bStart);
        });

        return sourceClips;
    }

    private int GetTrackIndexForEditorClip(ESEditorTrackClip editorClip)
    {
        if (editorClip == null || Items == null)
            return -1;

        for (int i = 0; i < Items.Count; i++)
        {
            ESEditorTrackItem item = Items[i];
            if (item != null && item.TrackClips != null && item.TrackClips.Contains(editorClip))
                return i;
        }

        return -1;
    }

    private static void CopyClipToClipboard(ITrackClip clip)
    {
        if (clip == null)
            return;

        s_CopiedClips.Clear();
        s_CopiedClipType = clip.GetType();
        s_CopiedClipStartTime = clip.StartTime;
        s_CopiedClipData = Sirenix.Serialization.SerializationUtility.SerializeValue(clip, Sirenix.Serialization.DataFormat.Binary);
        s_CopiedClips.Add(new CopiedClipPayload
        {
            data = s_CopiedClipData,
            clipType = s_CopiedClipType,
            startTime = s_CopiedClipStartTime,
            trackIndex = -1
        });
        Debug.Log($"[轨道编辑器] 已复制片段：{clip.DisplayName} ({s_CopiedClipType.Name})");
    }

    private static bool HasCopiedClips()
    {
        return s_CopiedClips.Count > 0 || (s_CopiedClipData != null && s_CopiedClipType != null);
    }

    private bool CanPasteClipToTrack(ESEditorTrackItem forItem)
    {
        if (forItem == null || forItem.item == null || s_CopiedClipData == null || s_CopiedClipType == null)
            return false;

        return CanPasteClipTypeToTrack(forItem, s_CopiedClipType);
    }

    private static bool CanPasteClipTypeToTrack(ESEditorTrackItem forItem, Type clipType)
    {
        if (forItem == null || forItem.item == null || clipType == null)
            return false;

        foreach (Type type in forItem.item.SupportedClipTypes())
        {
            if (type != null && type.IsAssignableFrom(clipType))
                return true;
        }

        return false;
    }

    private static bool IsEventTargetInsideClip(EventBase evt)
    {
        var element = evt.target as VisualElement;
        while (element != null)
        {
            if (element is ESEditorTrackClip)
                return true;

            element = element.parent;
        }

        return false;
    }

    private bool IsEventTargetInsideInspector(EventBase evt)
    {
        if (m_InspectorPanel == null || evt == null)
            return false;

        VisualElement element = evt.target as VisualElement;
        while (element != null)
        {
            if (ReferenceEquals(element, m_InspectorPanel))
                return true;
            element = element.parent;
        }

        return false;
    }

    private bool CanPasteCopiedClipsToOriginalTracks()
    {
        if (!HasCopiedClips())
            return false;

        for (int i = 0; i < s_CopiedClips.Count; i++)
        {
            CopiedClipPayload payload = s_CopiedClips[i];
            if (payload == null || payload.data == null || payload.clipType == null)
                continue;

            ESEditorTrackItem targetTrack = GetTrackItemByIndex(payload.trackIndex);
            if (CanPasteClipTypeToTrack(targetTrack, payload.clipType))
                return true;
        }

        return false;
    }

    private ESEditorTrackItem GetTrackItemByIndex(int index)
    {
        if (Items == null || index < 0 || index >= Items.Count)
            return null;

        return Items[index];
    }

    private void PasteCopiedClipsToOriginalTracks(float timeOffset, bool recordUndo)
    {
        if (!HasCopiedClips())
            return;

        if (recordUndo && TrackContainer is UnityEngine.Object undoTarget)
            Undo.RecordObject(undoTarget, "粘贴多轨片段");

        List<ESEditorTrackItem> changedTracks = new List<ESEditorTrackItem>();
        List<ESEditorTrackClip> pastedClips = new List<ESEditorTrackClip>();
        int skippedCount = 0;

        for (int i = 0; i < s_CopiedClips.Count; i++)
        {
            CopiedClipPayload payload = s_CopiedClips[i];
            if (payload == null || payload.data == null || payload.clipType == null)
            {
                skippedCount++;
                continue;
            }

            ESEditorTrackItem targetTrack = GetTrackItemByIndex(payload.trackIndex);
            if (!CanPasteClipTypeToTrack(targetTrack, payload.clipType))
            {
                skippedCount++;
                continue;
            }

            ITrackClip clip = Sirenix.Serialization.SerializationUtility.DeserializeValue<ITrackClip>(payload.data, Sirenix.Serialization.DataFormat.Binary);
            if (clip == null)
            {
                skippedCount++;
                continue;
            }

            ResetClipIdentityForPaste(clip);
            clip.StartTime = Mathf.Max(0f, payload.startTime + timeOffset);
            ESEditorTrackClip editorClip = targetTrack.AddClip(clip, false);
            if (editorClip == null)
            {
                skippedCount++;
                continue;
            }

            editorClip.SetTimeScaleAndStartShowCache();
            pastedClips.Add(editorClip);
            if (!changedTracks.Contains(targetTrack))
                changedTracks.Add(targetTrack);
        }

        if (pastedClips.Count == 0)
        {
            if (skippedCount > 0)
                Debug.LogWarning($"[轨道编辑器] 粘贴失败，跳过片段：{skippedCount}");
            return;
        }

        ClearClipSelection();
        for (int i = 0; i < pastedClips.Count; i++)
        {
            ESEditorTrackClip pastedClip = pastedClips[i];
            if (pastedClip == null)
                continue;

            pastedClip.SetSelected(true);
            m_SelectedClips.Add(pastedClip);
            SelectedClip = pastedClip;
        }
        RefreshClipSelectionVisuals();
        if (SelectedClip != null)
            SetClipInspectorTarget(SelectedClip, false);

        for (int i = 0; i < changedTracks.Count; i++)
        {
            ESEditorTrackItem track = changedTracks[i];
            if (track == null || track.item == null)
                continue;

            track.item.SortClipsByTime();
            track.MarkVisibilityCacheDirty();
            track.UpdateNodeMatchAndForeachUpdate(true);
        }

        ApplyAuthoringChange(
            null,
            ESTrackAuthoringChangeFlags.StructuralEdit,
            "粘贴片段");

        if (skippedCount > 0)
            Debug.LogWarning($"[轨道编辑器] 已粘贴片段：{pastedClips.Count}，跳过：{skippedCount}");
    }

    private void PasteClipToTrack(ESEditorTrackItem forItem, float startTime, bool recordUndo)
    {
        if (!CanPasteClipToTrack(forItem))
            return;

        ITrackClip clip = Sirenix.Serialization.SerializationUtility.DeserializeValue<ITrackClip>(s_CopiedClipData, Sirenix.Serialization.DataFormat.Binary);
        if (clip == null)
            return;

        ResetClipIdentityForPaste(clip);
        clip.StartTime = Mathf.Max(0f, startTime);
        if (recordUndo && TrackContainer is UnityEngine.Object undoTarget)
            Undo.RecordObject(undoTarget, "粘贴轨道片段");

        ESEditorTrackClip editorClip = forItem.AddClip(clip, false);
        if (editorClip != null)
        {
            editorClip.SetTimeScaleAndStartShowCache();
            SelectClip(editorClip);
        }

        SortTrackClipsByTime(forItem, false, false);
        ApplyAuthoringChange(
            null,
            ESTrackAuthoringChangeFlags.StructuralEdit,
            "粘贴片段");
    }

    private void SortTrackClipsByTime(
        ESEditorTrackItem trackItem,
        bool recordUndo = true,
        bool notifyChanges = true)
    {
        if (trackItem == null || trackItem.item == null)
            return;

        UnityEngine.Object undoTarget = TrackContainer as UnityEngine.Object;
        if (recordUndo && undoTarget != null)
            Undo.RecordObject(undoTarget, "按开始时间排序片段");

        bool changed = trackItem.item.SortClipsByTime();
        if (!changed)
            return;

        trackItem.MarkVisibilityCacheDirty();
        trackItem.UpdateNodeMatchAndForeachUpdate(true);
        if (notifyChanges)
        {
            ApplyAuthoringChange(
                null,
                ESTrackAuthoringChangeFlags.StructuralEdit,
                "按开始时间排序片段");
        }
    }


    public void AppendMenuItems_Refresh(GenericMenu menu)
    {
        if (menu == null)
            return;

        menu.AddItem(new GUIContent("维护/重建全部轨道视图"), false, () =>
        {
            InitNewSequenceAndOpenWindow();
        });

        menu.AddItem(new GUIContent("维护/刷新片段节点"), false, () =>
        {
            SyncTotalTimeFromCurrentSequence(true);
            UpdateClipsSimple(ESTrackClipUpdateFlags.All);
        });

        menu.AddItem(new GUIContent("校验/检查当前技能序列"), false, ValidateCurrentSequenceAndReport);
        if (m_ValidationErrorClips.Count > 0)
        {
            menu.AddItem(new GUIContent("校验/定位首个错误"), false, LocateFirstValidationError);
            menu.AddItem(new GUIContent("校验/定位下一个错误"), false, LocateNextValidationError);
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("校验/定位首个错误（暂无错误）"));
            menu.AddDisabledItem(new GUIContent("校验/定位下一个错误（暂无错误）"));
        }
    }

    private void ValidateCurrentSequenceAndReport()
    {
        List<string> warnings = new List<string>(32);
        List<string> infos = new List<string>(16);
        Dictionary<ITrackClip, string> clipWarnings = new Dictionary<ITrackClip, string>();
        ValidateSequence(Sequence, warnings, infos, clipWarnings);
        ApplyClipValidationWarnings(clipWarnings);
        RebuildValidationErrorList(clipWarnings);

        string sequenceName = Sequence != null ? Sequence.Name : "<空序列>";
        if (warnings.Count == 0)
        {
            Debug.Log($"[技能序列校验] {sequenceName} 未发现明显风险。Info={infos.Count}");
            EditorUtility.DisplayDialog("技能序列校验", $"未发现明显风险。\n提示项：{infos.Count}", "确定");
            return;
        }

        StringBuilder builder = new StringBuilder(1024);
        builder.AppendLine($"技能序列：{sequenceName}");
        builder.AppendLine($"警告数量：{warnings.Count}");
        builder.AppendLine();
        int displayCount = Mathf.Min(warnings.Count, 12);
        for (int i = 0; i < displayCount; i++)
            builder.AppendLine(warnings[i]);

        if (warnings.Count > displayCount)
            builder.AppendLine($"... 还有 {warnings.Count - displayCount} 条，详见 Console。");

        for (int i = 0; i < warnings.Count; i++)
            Debug.LogWarning($"[技能序列校验] {warnings[i]}");

        EditorUtility.DisplayDialog("技能序列校验", builder.ToString(), "确定");
    }

    private void ApplyClipValidationWarnings(Dictionary<ITrackClip, string> clipWarnings)
    {
        if (Items == null)
            return;

        foreach (ESEditorTrackItem item in Items)
        {
            if (item == null || item.TrackClips == null)
                continue;

            foreach (ESEditorTrackClip clip in item.TrackClips)
            {
                if (clip == null || clip.trackClip == null)
                    continue;

                clipWarnings.TryGetValue(clip.trackClip, out string warning);
                clip.SetValidationWarning(warning);
            }
        }
    }

    private void RebuildValidationErrorList(Dictionary<ITrackClip, string> clipWarnings)
    {
        m_ValidationErrorClips.Clear();
        m_ValidationErrorCursor = -1;
        if (clipWarnings == null || clipWarnings.Count == 0 || Items == null)
            return;

        for (int i = 0; i < Items.Count; i++)
        {
            ESEditorTrackItem item = Items[i];
            if (item == null || item.TrackClips == null)
                continue;

            List<ESEditorTrackClip> orderedClips = item.TrackClips
                .Where(clip => clip != null && clip.trackClip != null && clipWarnings.ContainsKey(clip.trackClip))
                .OrderBy(clip => clip.trackClip.StartTime)
                .ToList();

            for (int j = 0; j < orderedClips.Count; j++)
                m_ValidationErrorClips.Add(orderedClips[j].trackClip);
        }
    }

    private void EnsureValidationErrorList()
    {
        if (m_ValidationErrorClips.Count > 0)
            return;

        List<string> warnings = new List<string>(32);
        List<string> infos = new List<string>(16);
        Dictionary<ITrackClip, string> clipWarnings = new Dictionary<ITrackClip, string>();
        ValidateSequence(Sequence, warnings, infos, clipWarnings);
        ApplyClipValidationWarnings(clipWarnings);
        RebuildValidationErrorList(clipWarnings);
    }

    private void LocateFirstValidationError()
    {
        EnsureValidationErrorList();
        if (m_ValidationErrorClips.Count == 0)
        {
            Debug.Log("[技能序列校验] 当前没有可定位的片段错误。");
            return;
        }

        m_ValidationErrorCursor = 0;
        LocateValidationErrorAt(m_ValidationErrorCursor);
    }

    private void LocateNextValidationError()
    {
        EnsureValidationErrorList();
        if (m_ValidationErrorClips.Count == 0)
        {
            Debug.Log("[技能序列校验] 当前没有可定位的片段错误。");
            return;
        }

        m_ValidationErrorCursor = (m_ValidationErrorCursor + 1 + m_ValidationErrorClips.Count) % m_ValidationErrorClips.Count;
        LocateValidationErrorAt(m_ValidationErrorCursor);
    }

    private void LocateValidationErrorAt(int index)
    {
        if (index < 0 || index >= m_ValidationErrorClips.Count)
            return;

        ITrackClip targetClip = m_ValidationErrorClips[index];
        ESEditorTrackClip editorClip = FindEditorClip(targetClip);
        if (editorClip == null)
        {
            m_ValidationErrorClips.RemoveAt(index);
            m_ValidationErrorCursor = -1;
            Debug.LogWarning("[技能序列校验] 错误片段节点已失效，已刷新错误列表。");
            return;
        }

        SelectClip(editorClip, false);
        SetPlayheadTime(editorClip.trackClip.StartTime);
        EnsureTimeVisible(editorClip.trackClip.StartTime);
        Debug.Log($"[技能序列校验] 定位错误 {index + 1}/{m_ValidationErrorClips.Count}: {editorClip.trackClip.DisplayName}");
    }

    private ESEditorTrackClip FindEditorClip(ITrackClip clip)
    {
        if (clip == null || Items == null)
            return null;

        for (int i = 0; i < Items.Count; i++)
        {
            ESEditorTrackItem item = Items[i];
            if (item == null)
                continue;

            if (item.TryGetEditorClip(clip, out ESEditorTrackClip editorClip))
                return editorClip;
        }

        return null;
    }

    private void ScheduleAutoValidateSequenceVisuals()
    {
        if (rootVisualElement == null)
            return;

        m_LastAutoValidationRequestTime = EditorApplication.timeSinceStartup;
        if (m_AutoValidationScheduled)
            return;

        m_AutoValidationScheduled = true;
        int generation = m_ProjectionGeneration;
        Action validateWhenIdle = null;
        validateWhenIdle = () =>
        {
            m_AutoValidationTask = null;
            if (this == null || rootVisualElement == null || generation != m_ProjectionGeneration)
            {
                m_AutoValidationScheduled = false;
                return;
            }

            const double validationIdleDelay = 0.25d;
            double remainingDelay = validationIdleDelay - (EditorApplication.timeSinceStartup - m_LastAutoValidationRequestTime);
            if (remainingDelay > 0d)
            {
                m_AutoValidationTask = rootVisualElement.schedule.Execute(validateWhenIdle);
                m_AutoValidationTask.ExecuteLater(Mathf.CeilToInt((float)(remainingDelay * 1000d)));
                return;
            }

            m_AutoValidationScheduled = false;
            AutoValidateSequenceVisuals();
        };
        m_AutoValidationTask = rootVisualElement.schedule.Execute(validateWhenIdle);
        m_AutoValidationTask.ExecuteLater(250);
    }

    private void AutoValidateSequenceVisuals()
    {
        if (Sequence == null)
            return;

        List<string> warnings = new List<string>(16);
        List<string> infos = new List<string>(8);
        Dictionary<ITrackClip, string> clipWarnings = new Dictionary<ITrackClip, string>();
        ValidateSequence(Sequence, warnings, infos, clipWarnings);
        ApplyClipValidationWarnings(clipWarnings);
        RebuildValidationErrorList(clipWarnings);
    }

    private static void AddClipWarning(List<string> warnings, Dictionary<ITrackClip, string> clipWarnings, ITrackClip clip, string message)
    {
        warnings.Add(message);
        if (clip == null || clipWarnings == null)
            return;

        if (clipWarnings.TryGetValue(clip, out string existing) && !string.IsNullOrEmpty(existing))
            clipWarnings[clip] = existing + "\n" + message;
        else
            clipWarnings[clip] = message;
    }

    internal static void ValidateSequence(ITrackSequence sequence, List<string> warnings, List<string> infos, Dictionary<ITrackClip, string> clipWarnings)
    {
        if (warnings == null || infos == null)
            return;

        if (sequence == null)
        {
            warnings.Add("当前没有绑定技能序列。");
            return;
        }

        if (sequence.Tracks == null)
        {
            warnings.Add("序列 Tracks 为空。");
            return;
        }

        int trackIndex = 0;
        bool hasEnabledAnimationTrack = false;
        bool hasEnabledAnimationClip = false;
        foreach (ITrackItem track in sequence.Tracks)
        {
            string trackName = track != null ? track.DisplayName : "<空轨道>";
            if (track == null)
            {
                warnings.Add($"轨道[{trackIndex}] 为空。");
                trackIndex++;
                continue;
            }

            if (!track.Enabled)
            {
                infos.Add($"轨道[{trackIndex}] {trackName} 已禁用。");
                trackIndex++;
                continue;
            }

            if (track is SkillTrackItem_Animation)
                hasEnabledAnimationTrack = true;

            List<ITrackClip> enabledClips = new List<ITrackClip>(8);
            int clipIndex = 0;
            if (track.Clips == null)
            {
                warnings.Add($"轨道[{trackIndex}] {trackName} 的片段列表为空。");
                trackIndex++;
                continue;
            }

            foreach (ITrackClip clip in track.Clips)
            {
                string clipName = clip != null ? clip.DisplayName : "<空片段>";
                if (clip == null)
                {
                    warnings.Add($"轨道[{trackIndex}] {trackName} / 片段[{clipIndex}] 为空。");
                    clipIndex++;
                    continue;
                }

                if (!clip.Enabled)
                {
                    infos.Add($"轨道[{trackIndex}] {trackName} / 片段[{clipIndex}] {clipName} 已禁用。");
                    clipIndex++;
                    continue;
                }

                if (clip.StartTime < 0f)
                    AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / {clipName} 开始时间小于 0。");

                if (clip.DurationTime <= 0.0001f)
                    AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / {clipName} 持续时间过短或为 0。");

                ValidateTypedClip(trackIndex, trackName, clipIndex, clip, warnings, clipWarnings);

                if (clip is SkillTrackClip_Animation)
                    hasEnabledAnimationClip = true;

                enabledClips.Add(clip);
                clipIndex++;
            }

            trackIndex++;
        }

        if (hasEnabledAnimationTrack && !hasEnabledAnimationClip)
            warnings.Add("存在启用的动画轨道，但没有任何启用且有效的动画片段；运行时会只依赖基础 Idle/状态动画。");
    }

    private static void ValidateTypedClip(int trackIndex, string trackName, int clipIndex, ITrackClip clip, List<string> warnings, Dictionary<ITrackClip, string> clipWarnings)
    {
        string clipName = clip.DisplayName;
        if (clip is SkillTrackClip_Animation animationClip)
        {
            if (animationClip.AnimationClipName == null)
            {
                AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / 片段[{clipIndex}] {clipName} 未指定 AnimationClip。");
            }
            else
            {
                float clipLength = animationClip.AnimationClipName.length;
                if (animationClip.clipStartOffset >= clipLength)
                    AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / {clipName} 裁剪起点({animationClip.clipStartOffset:F2}s)超出动画长度({clipLength:F2}s)。");

                if (animationClip.playbackSpeed <= 0.0001f)
                    AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / {clipName} 播放速度不能小于等于 0。");

                float availableLength = Mathf.Max(0f, clipLength - Mathf.Max(0f, animationClip.clipStartOffset));
                float requiredSourceTime = animationClip.DurationTime * Mathf.Max(0.01f, animationClip.playbackSpeed);
                if (!animationClip.loopClip && requiredSourceTime > availableLength + 0.02f)
                    AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / {clipName} 非循环采样会在片段结束前停到动画末帧；可缩短持续时间、降低速度或开启循环。");
            }
        }
        else if (clip is SkillTrackClip_Audio audioClip)
        {
            bool hasCue = audioClip.cue != null && audioClip.cue.IsConfigured;
            if (!hasCue && audioClip.LegacyAudioClip == null)
                AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / 片段[{clipIndex}] {clipName} 未指定 Cue。");
            else if (!hasCue && audioClip.stopOnClipExit && audioClip.DurationTime + 0.05f < audioClip.LegacyAudioClip.length)
                AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / {clipName} 开启了离开片段停止音效，但片段时长({audioClip.DurationTime:F2}s)短于音频({audioClip.LegacyAudioClip.length:F2}s)，可能被截断。");
        }
        else if (clip is SkillTrackClip_Operation operationClip)
        {
            if (operationClip.op == null)
                AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / 片段[{clipIndex}] {clipName} 未配置 Operation。");
            if (!operationClip.conditionValue)
                AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / {clipName} 启用条件为 false，该 Operation 不会执行。");
        }
    }

    #endregion
    #region 辅助方法


    private static Cursor s_CursorDefault;
    private static Cursor s_CursorPan;
    private static Cursor s_CursorSelect;
    /// <summary>
    /// 根据当前交互模式更新 TrackView 右侧面板的光标
    /// </summary>
    private void UpdateCursor()
    {
        // 防御性检查：防止在面板尚未初始化时访问
        if (rightPanel == null)
            return;

        // 根据当前交互模式切换光标
        switch (m_CurrentMode)
        {
            // 平移模式（拖拽画布 / 时间轴）
            case InteractionMode.Panning:
                // 使用 Unity Editor 内置 Pan 光标（抓手）
                rightPanel.style.cursor = s_CursorPan;
                break;

            // 选择模式（框选 Clip / 区域）
            case InteractionMode.Selecting:
                // 使用 Unity Editor 内置 Cross 光标（十字准星）
                rightPanel.style.cursor = s_CursorSelect;
                break;

            // 默认 / 空闲状态
            default:
                // 使用系统默认箭头光标
                rightPanel.style.cursor = s_CursorDefault;
                break;
        }
    }

    private bool IsMouseInPanel(MouseEventBase<MouseDownEvent> evt)
    {
        // 检查鼠标是否在 RightPanel 内
        var localPos = rightPanel.WorldToLocal(evt.mousePosition);
        var rect = new Rect(0, 0, rightPanel.layout.width, rightPanel.layout.height);
        return rect.Contains(localPos);
    }
    #endregion

    #region  操作框选矩形
    private void StartSelection(MouseDownEvent evt)
    {
        m_CurrentMode = InteractionMode.Selecting;
        m_IsSelecting = true;
        m_SelectionAdditive = evt.ctrlKey || evt.commandKey;

        // 记录开始位置
        m_SelectionStart = rightPanel.WorldToLocal(evt.mousePosition);
        m_SelectionRect = new Rect(m_SelectionStart, Vector2.zero);

        // 显示选择框
        m_SelectionVisual.style.display = DisplayStyle.Flex;
        UpdateSelectionVisual();

        // 捕获鼠标
        rightPanel.CaptureMouse();

        UpdateCursor();

        // Debug.Log($"开始选择: {m_SelectionStart}");
    }

    private void HandleSelection(MouseMoveEvent evt)
    {
        if (!m_IsSelecting || m_CurrentMode != InteractionMode.Selecting)
            return;

        // 计算当前鼠标位置
        Vector2 currentPos = rightPanel.WorldToLocal(evt.mousePosition);

        // 更新选择矩形
        Vector2 min = Vector2.Min(m_SelectionStart, currentPos);
        Vector2 max = Vector2.Max(m_SelectionStart, currentPos);
        m_SelectionRect = new Rect(min, max - min);

        // 更新视觉
        UpdateSelectionVisual();

    }

    private void EndSelection()
    {
        m_CurrentMode = InteractionMode.None;
        m_IsSelecting = false;

        // 隐藏选择框
        m_SelectionVisual.style.display = DisplayStyle.None;

        // 框选必须落到真实 Clip 选择状态，不能只显示一块装饰矩形。
        CheckElementsInSelection(m_SelectionAdditive);
        m_SelectionAdditive = false;

        // 释放鼠标捕获
        if (rightPanel.HasMouseCapture())
        {
            rightPanel.ReleaseMouse();
        }

        // 触发选择事件
        // OnSelectionChanged?.Invoke(m_SelectionRect);

        UpdateCursor();

        // Debug.Log($"结束选择: {m_SelectionRect}");
    }

    private void CheckElementsInSelection(bool additive)
    {
        if (leftPanel == null || Items == null || Items.Count == 0)
            return;

        // m_SelectionRect 以时间轴画布为坐标；选择框视觉元素挂在 leftPanel，
        // 因此先转换到同一坐标系再与每个 Clip 的 worldBound 做交集判断。
        Rect selectionInLeftPanel = m_SelectionRect;
        selectionInLeftPanel.position += new Vector2(LeftTrackPixel, 0f);
        bool hasArea = selectionInLeftPanel.width > 2f && selectionInLeftPanel.height > 2f;
        if (!hasArea)
            return;

        if (!additive)
            ClearClipSelection();

        ESEditorTrackClip primary = null;
        for (int i = 0; i < Items.Count; i++)
        {
            ESEditorTrackItem trackItem = Items[i];
            if (trackItem == null || trackItem.TrackClips == null)
                continue;

            for (int j = 0; j < trackItem.TrackClips.Count; j++)
            {
                ESEditorTrackClip clip = trackItem.TrackClips[j];
                if (clip == null || clip.trackClip == null || clip.panel == null)
                    continue;

                Rect clipRect = WorldRectToLocal(leftPanel, clip.worldBound);
                if (!selectionInLeftPanel.Overlaps(clipRect, true))
                    continue;

                if (!m_SelectedClips.Contains(clip))
                {
                    m_SelectedClips.Add(clip);
                    clip.SetSelected(true);
                }

                primary = clip;
            }
        }

        if (primary != null)
        {
            SelectedClip = primary;
            RefreshClipSelectionVisuals();
            SetClipInspectorTarget(primary, false);
        }
    }

    private static Rect WorldRectToLocal(VisualElement parent, Rect worldRect)
    {
        Vector2 min = parent.WorldToLocal(worldRect.min);
        Vector2 max = parent.WorldToLocal(worldRect.max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void UpdateSelectionVisual()
    {
        m_SelectionVisual.style.left = LeftTrackPixel + m_SelectionRect.x;
        m_SelectionVisual.style.top = m_SelectionRect.y;
        m_SelectionVisual.style.width = m_SelectionRect.width;
        m_SelectionVisual.style.height = m_SelectionRect.height;
    }

    #endregion

    #region  常规按钮
    private void OnCreatorButtonClickLeft(ClickEvent click)
    {
        if (click.button == 0)
            ShowMenu_AddTrack();
    }



    #endregion



    private void UpdateClipsSimple(ESTrackClipUpdateFlags flags = ESTrackClipUpdateFlags.Layout)
    {
        if (ruler == null || ruler.TopRuler == null || Items == null)
            return;

        ruler.TopRuler.MarkDirtyRepaint();
        float visibleStart = StartShow;
        float visibleEnd = GetVisibleEndTime();
        foreach (var i in Items)
        {
            if (i != null)
                i.UpdateNodes(visibleStart, visibleEnd, flags);
        }
    }

    private void ScheduleViewRefresh()
    {
        if (m_ViewRefreshScheduled)
            return;

        m_ViewRefreshScheduled = true;
        EditorApplication.update -= FlushScheduledViewRefresh;
        EditorApplication.update += FlushScheduledViewRefresh;
    }

    internal void ScheduleAutoSave(string source = null)
    {
        if (!(TrackContainer is UnityEngine.Object target))
            return;

        CaptureTrackContainerRevision(false);
        if (m_TrackAssetConflictPending)
        {
            m_AutoSaveTarget = target;
            m_AutoSaveScheduled = false;
            EditorApplication.update -= FlushAutoSave;
            UpdateSaveStatus("外部冲突", ESTrackViewTheme.StatusWarning,
                m_TrackAssetConflictReason, string.IsNullOrWhiteSpace(source) ? "时间轴编辑" : source);
            return;
        }

        m_AutoSaveTarget = target;
        if (!string.IsNullOrWhiteSpace(source))
            m_SaveChangeSource = source.Trim();
        m_AutoSaveDueAt = EditorApplication.timeSinceStartup + AutoSaveDelaySeconds;
        UpdateSaveStatus("待保存", ESTrackViewTheme.StatusModified, "当前时间轴有未落盘修改，将在约 1.25 秒后自动保存。", m_SaveChangeSource);
        if (m_AutoSaveScheduled)
            return;

        m_AutoSaveScheduled = true;
        EditorApplication.update -= FlushAutoSave;
        EditorApplication.update += FlushAutoSave;
    }

    private void FlushAutoSave()
    {
        if (!m_AutoSaveScheduled)
            return;

        if (this == null || rootVisualElement == null || window != this)
        {
            CancelTrackAutoSaveWithoutWriting();
            return;
        }

        if (m_TrackAssetConflictPending)
        {
            CancelTrackAutoSaveWithoutWriting();
            UpdateSaveStatus("外部冲突", ESTrackViewTheme.StatusWarning,
                m_TrackAssetConflictReason, "自动保存已暂停");
            return;
        }

        if (EditorApplication.timeSinceStartup < m_AutoSaveDueAt)
            return;

        EditorApplication.update -= FlushAutoSave;
        m_AutoSaveScheduled = false;
        UnityEngine.Object target = m_AutoSaveTarget;
        m_AutoSaveTarget = null;
        UpdateSaveStatus("保存中", ESTrackViewTheme.StatusModified, "正在自动保存当前时间轴。", m_SaveChangeSource);
        TrySaveAutoSaveTarget(target, "当前时间轴已自动保存。");
    }

    internal void FlushAutoSaveImmediate()
    {
        if (m_TrackAssetConflictPending)
        {
            CancelTrackAutoSaveWithoutWriting();
            UpdateSaveStatus("外部冲突", ESTrackViewTheme.StatusWarning,
                m_TrackAssetConflictReason, "立即保存已暂停");
            return;
        }

        UnityEngine.Object target = m_AutoSaveTarget;
        if (target == null && TrackContainer is UnityEngine.Object dirtyTarget && EditorUtility.IsDirty(dirtyTarget))
            target = dirtyTarget;

        if (!m_AutoSaveScheduled && m_AutoSaveTarget == null && target == null)
            return;

        EditorApplication.update -= FlushAutoSave;
        m_AutoSaveScheduled = false;
        m_AutoSaveTarget = null;
        UpdateSaveStatus("保存中", ESTrackViewTheme.StatusModified, "正在立即保存当前时间轴。", m_SaveChangeSource);
        TrySaveAutoSaveTarget(target, "当前时间轴已保存。");
    }

    private bool TrySaveAutoSaveTarget(UnityEngine.Object target, string successTooltip)
    {
        if (target == null)
        {
            UpdateSaveStatus("保存失败", ESTrackViewTheme.StatusError, "自动保存没有找到有效的时间轴资产，请重新选择资产后重试。", "自动保存");
            return false;
        }

        try
        {
            // OnDisable/OnDestroy can flush immediately before the next project-change poll.
            // Re-check the on-disk revision at the write boundary so a just-arrived external
            // edit cannot be silently overwritten by the pending local autosave.
            if (ReferenceEquals(target, TrackContainer as UnityEngine.Object))
            {
                SynchronizeTrackContainerRevision(includeDependencyHash: true);
                if (m_TrackAssetConflictPending)
                {
                    CancelTrackAutoSaveWithoutWriting();
                    UpdateSaveStatus("外部冲突", ESTrackViewTheme.StatusWarning,
                        m_TrackAssetConflictReason, "保存前校验已暂停");
                    return false;
                }
            }

            AssetDatabase.SaveAssetIfDirty(target);
            if (EditorUtility.IsDirty(target))
            {
                UpdateSaveStatus("保存失败", ESTrackViewTheme.StatusError, "时间轴仍有未保存修改，请从“更多”菜单重试立即保存。");
                ES.EditorInternal.ESEditorPresentation.PulseWindow(this, ES.EditorInternal.ESStatusKind.Error);
                return false;
            }

            UpdateSaveStatus("已保存", ESTrackViewTheme.StatusReady, successTooltip);
            ClearTrackAssetConflict();
            CaptureTrackContainerRevision(true);
            ES.EditorInternal.ESEditorPresentation.PulseWindow(this, ES.EditorInternal.ESStatusKind.Modified);
            return true;
        }
        catch (Exception e)
        {
            UpdateSaveStatus("保存失败", ESTrackViewTheme.StatusError, "保存时间轴时发生异常，请查看 Console 后重试。");
            ES.EditorInternal.ESEditorPresentation.PulseWindow(this, ES.EditorInternal.ESStatusKind.Error);
            Debug.LogException(e, target);
            return false;
        }
    }

    private void FlushScheduledViewRefresh()
    {
        EditorApplication.update -= FlushScheduledViewRefresh;
        if (!m_ViewRefreshScheduled)
            return;

        m_ViewRefreshScheduled = false;
        if (this == null || rootVisualElement == null || window != this)
            return;

        UpdateClipsSimple();
        MoveTimeCursor(cursorTime);
    }
}


