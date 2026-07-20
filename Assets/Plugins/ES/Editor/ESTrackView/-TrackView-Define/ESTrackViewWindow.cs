using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using ES;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.UIElements.Cursor;

// 抑制私有字段未使用警告
#pragma warning disable CS0414
// 抑制无法访问的代码警告（提前return）
#pragma warning disable CS0162

public class ESTrackViewWindow : OdinEditorWindow
{
    public static ESTrackViewWindow window;
    public static ITrackSequence Sequence { get { if (TrackContainer != null) return TrackContainer.Sequence; return null; } }
    public static IEditorTrackSupport_GetSequence TrackContainer;
    private static byte[] s_CopiedClipData;
    private static Type s_CopiedClipType;
    private static float s_CopiedClipStartTime;
    private static readonly List<CopiedClipPayload> s_CopiedClips = new List<CopiedClipPayload>();
    private bool m_AutoValidationScheduled;
    private bool m_ViewRefreshScheduled;
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

    [SerializeField]
    public VisualGUIDrawerSO drawerSOForTrackItem;

    [SerializeField]
    public VisualGUIDrawerSO drawerSOForTrackClip;
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
        EditorApplication.update -= FlushScheduledViewRefresh;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        CleanupTrackPreviewPlayer();
        base.OnDestroy();

    }
    protected override void OnEnable()
    {
        base.OnEnable();
        window = this;
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
    }
    protected override void OnDisable()
    {
        EditorApplication.update -= FlushScheduledViewRefresh;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        CleanupTrackPreviewPlayer();
        ClearFocusedEditingClip(null);
        base.OnDisable();
        Selection.selectionChanged -= OnTrackWindowSelectionChanged;
        EditorApplication.delayCall -= RefreshPreselectEntityDelayed;
        if (Last_EditorWindowForTrackItem != null)
        {
            Last_EditorWindowForTrackItem.Close();
        }
        if (Last_EditorWindowForTrackClip != null)
        {
            Last_EditorWindowForTrackClip.Close();
        }
        if (Last_EditorWindowForSkillDataInfo != null)
        {
            Last_EditorWindowForSkillDataInfo.Close();
        }
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
            {
                _totaltime = value;
                TrackClipBase.defaultEndTime = _totaltime;
            }
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
    public const float LeftTrackPixel = 200;
    public static float dynamicTargetTotalPixel { get { return window.horSlider.resolvedStyle.width; } }

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

    private VisualElement timeCursor;
    private bool isDraggingCursor = false;
    private float dragOffsetX = 0; // 鼠标点击点相对于游标中心的偏移(视情况可省略)

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
    private float m_PanSensitivity = 1.0f;
    private bool m_IsPanning = false;

    // 缩放相关
    private float m_ZoomLevel = 1.0f;
    private float m_MinZoom = 0.1f;
    private float m_MaxZoom = 10.0f;
    private float m_ZoomSensitivity = 0.1f;
    private Vector2 m_ZoomCenter = Vector2.zero;

    // 选择相关
    private Rect m_SelectionRect = Rect.zero;
    private VisualElement m_SelectionVisual;
    private bool m_IsSelecting = false;
    private Vector2 m_SelectionStart = Vector2.zero;

    #endregion


    #region 初始化核心
    [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "轨道编辑器", false, -1000)]
    public static void OpenWindow()
    {
        window = GetWindow<ESTrackViewWindow>();
        window.titleContent = new GUIContent("【轨道】编辑器");
    }
    public static void InitNewSequenceAndOpenWindow()
    {
        OpenWindow();
        //简单更新
        if (TrackContainer == null || Sequence == null)
        {
            window?.toolbar?.UpdateEntity(null, null);
            return;
        }

        if (window.toolbar != null)
        {
            window.toolbar.Name.text = "轴：" + TrackContainer.trackName;
        }

        window.ClearFocusedEditingClip(null);
        window.SyncTotalTimeFromSequence(Sequence, true);
        //开始重建
        if (ESTrackViewWindow.window.leftPanel == null)
            return;

        var elements = ESTrackViewWindow.window.leftPanel.Children().ToList();

        // 移除并销毁每个元素
        foreach (var element in elements)
        {
            if (element == null) return;
            if (element is ESEditorTrackItem item)
            {
                element.RemoveFromHierarchy();

                // 3. 清除引用
                element.userData = null;
            }
            // 2. 移除元素

        }
        window.Items.Clear();

        if (Sequence != null)
        {
            window.UpdatePreselectEntityFromSelection(false);
            if (Sequence != null)
            {
                var seqPlayer = window.BuildSequencePlayer(Sequence, window.PreselectEntity);
                EditorTimelinePlayer.Instance.ActiveSequence = seqPlayer;  // 关键
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

            ESEditorHandle.AddSimpleHandleTask(() =>
            {
                foreach (var it in ESTrackViewWindow.window.Items)
                {
                    it.UpdateNodes();
                }


            });
        }





    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;
        // VisualElements objects can contain other VisualElement following a tree hierarchy.
        // Instantiate UXML



        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);

        //隐藏特殊资源

        drawerSOForTrackItem.hideFlags = HideFlags.None;


        BindElements();
        FindTrackAssets();
        BindNormalHandles();
        BindButtonsHandles();



        root.schedule.Execute(() =>
                  {
                      HandleStartEndScale(startScale, endScale);
                      ApplyStartEndToUISlider(startScale, endScale);
                  }).StartingIn(100);
        CreateTimeCursor();
        window.timeCursor.BringToFront();

    }


    private void FindTrackAssets()
    {
        if (TrackContainer != null && TrackContainer.Sequence != null)
            return;

        if (Selection.activeObject is IEditorTrackSupport_GetSequence support &&
        support.Sequence != null)
        {
            TrackContainer = support;
            InitNewSequenceAndOpenWindow();
            return;
        }

        var allAssets = ESDesignUtility.SafeEditor.FindAllSOAssets<IEditorTrackSupport_GetSequence>();
        if (allAssets.Count > 0)
        {
            foreach (var a in allAssets)
            {
                if (a.Sequence != null)
                {
                    TryUpdateTrackSequence(a);

                    break;
                }
            }
        }
    }


    public static void TryUpdateTrackSequence(IEditorTrackSupport_GetSequence newSequenceContainer)
    {
        if (newSequenceContainer != TrackContainer)
        {
            TrackContainer = newSequenceContainer;
            InitNewSequenceAndOpenWindow();

        }
        else
        {
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
        //rightPanel.RegisterCallback<ClickEvent>(OnCreatorButtonClickRight);
        //leftPanel.RegisterCallback<ClickEvent>(OnCreatorButtonClickRight);
    }

    private void BindElements()
    {
        ruler = rootVisualElement.Query<ESTrackRuler>();
        horSlider = rootVisualElement.Query<MinMaxSlider>();
        verScroll = rootVisualElement.Query<ScrollView>();
        rightPanel = rootVisualElement.Query<VisualElement>("DownRightPart");
        leftPanel = rootVisualElement.Query("DownLeftPart");

        leftPanel.style.width = LeftTrackPixel;
        leftPanel.style.minWidth = LeftTrackPixel;
        leftPanel.style.maxWidth = LeftTrackPixel;

        horSlider.style.left = LeftTrackPixel;
        rightPanel.style.left = LeftTrackPixel;
        ruler.style.left = 0;


        m_SelectionVisual = rootVisualElement.Query("SeletionContent");

        CreatorToolBar = rootVisualElement.Query<ESTrackCreatorToolbar>();

        toolbar = rootVisualElement.Query<ESTrackTimerToolbar>();
        RefreshEntityDisplay();
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
        rightPanel.RegisterCallback<WheelEvent>(OnRightPanelWheel, TrickleDown.TrickleDown);
        verScroll.RegisterCallback<WheelEvent>(OnScrollViewWheel, TrickleDown.TrickleDown);

        // 2. 鼠标普通事件 - 平移
        rightPanel.RegisterCallback<MouseDownEvent>(OnRightPanelMouseDown, TrickleDown.NoTrickleDown);
        rightPanel.RegisterCallback<MouseMoveEvent>(OnRightPanelMouseMove, TrickleDown.NoTrickleDown);
        rightPanel.RegisterCallback<MouseUpEvent>(OnRightPanelMouseUp, TrickleDown.NoTrickleDown);

        // 3. 鼠标离开事件
        rightPanel.RegisterCallback<MouseLeaveEvent>(OnMouseLeave, TrickleDown.TrickleDown);

        // 4. 右键上下文菜单
        rightPanel.RegisterCallback<ContextClickEvent>(OnContextClick_CompleteMenu, TrickleDown.TrickleDown);

    }

    private void CreateTimeCursor()
    {
        timeCursor = new VisualElement
        {
            name = "time-cursor",
            style =
        {
            position = Position.Absolute,
            width = 14,               // 透明命中区，方便拖动
            backgroundColor = Color.clear,
            top = 0,
            bottom = 0,
            // 使用稳定的 Editor 内置光标资源，避免不同 Unity 版本缺少 d_GridLayout 图标时报错。
            cursor = new Cursor { texture = EditorGUIUtility.Load("Cursors/d_Cursor_Cross") as Texture2D, hotspot = new Vector2(7, 7) }
        }
        };

        // 视觉上仍显示一条细线
        var innerLine = new VisualElement
        {
            style =
        {
            width = 2,
            backgroundColor = new Color(1f, 0.12f, 0.16f, 0.92f),
            position = Position.Absolute,
            left = 6,
            top = 0,
            bottom = 0
        }
        };
        timeCursor.Add(innerLine);

        var handle = new VisualElement
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
            borderTopLeftRadius = 3,
            borderTopRightRadius = 3,
            borderBottomLeftRadius = 2,
            borderBottomRightRadius = 2,
            backgroundColor = new Color(0.92f, 0.08f, 0.13f, 0.72f)
        }
        };
        timeCursor.Add(handle);

        leftPanel.Add(timeCursor);
        timeCursor.BringToFront();

        // 直接绑定鼠标事件
        timeCursor.RegisterCallback<MouseDownEvent>(OnTimeCursorMouseDown);
        timeCursor.RegisterCallback<MouseMoveEvent>(OnTimeCursorMouseMove);
        timeCursor.RegisterCallback<MouseUpEvent>(OnTimeCursorMouseUp);


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

        if (sequence is ITrackSequenceDurationCache durationCache)
            durationCache.CachedMaxTime = duration;

        if (EditorTimelinePlayer.Instance.ActiveSequence != null)
            EditorTimelinePlayer.Instance.ActiveSequence.Duration = duration;

        if (markDirty)
            ESTrackViewWindowHelper.SaveContainerDisplayChanges();

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
            rootVisualElement?.Focus();
            return;
        }

        if (SelectedTrackItem != null)
            SelectedTrackItem.SetSelected(false);

        SelectedTrackItem = trackItem;
        if (SelectedTrackItem != null)
            SelectedTrackItem.SetSelected(true);

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

            rootVisualElement?.Focus();
            return;
        }

        if (!additive)
            ClearClipSelection();

        SelectedClip = clip;
        if (SelectedClip != null)
        {
            m_SelectedClips.Add(SelectedClip);
            RefreshClipSelectionVisuals();
        }

        rootVisualElement?.Focus();
    }

    public bool IsClipSelected(ESEditorTrackClip clip)
    {
        return clip != null && m_SelectedClips.Contains(clip);
    }

    public int SelectedClipCount => m_SelectedClips.Count;

    private void ClearClipSelection()
    {
        foreach (ESEditorTrackClip selected in m_SelectedClips)
        {
            if (selected != null)
                selected.SetSelected(false);
        }

        m_SelectedClips.Clear();
        SelectedClip = null;
    }

    private void RemoveClipFromSelection(ESEditorTrackClip clip)
    {
        if (clip == null)
            return;

        if (m_SelectedClips.Remove(clip))
            clip.SetSelected(false);

        if (SelectedClip == clip)
            SelectedClip = m_SelectedClips.FirstOrDefault();

        if (SelectedClip != null)
            RefreshClipSelectionVisuals();
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

        for (int i = 0; i < Items.Count; i++)
        {
            ESEditorTrackItem item = Items[i];
            if (item == null || item.TrackClips == null)
                continue;

            for (int j = 0; j < item.TrackClips.Count; j++)
                SelectClip(item.TrackClips[j], true);
        }
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
        ESDesignUtility.SafeEditor.Wrap_SetDirty(TrackContainer as UnityEngine.Object);
        SkillSequenceRuntimeCache.NotifySequenceChanged(Sequence);
        SyncTotalTimeFromCurrentSequence(true);
        RebuildActivePreviewPlayer();
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

            if (!trackItemEditor.item.TryRemoveTrackClip(clip.trackClip))
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
        if (anchor == null || !m_SelectedClips.Contains(anchor) || m_SelectedClips.Count <= 1)
            return;

        if (TrackContainer is UnityEngine.Object undoTarget)
            Undo.RecordObject(undoTarget, "批量移动片段");

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

        m_GroupDragStartTimes.Clear();
        m_GroupDragAnchor = null;

        RefreshEditedTracksAfterClipChanges();
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

        ESDesignUtility.SafeEditor.Wrap_SetDirty(TrackContainer as UnityEngine.Object);
        SkillSequenceRuntimeCache.NotifySequenceChanged(Sequence);
        SyncTotalTimeFromCurrentSequence(true);
        RebuildActivePreviewPlayer();
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
        UpdateClipsSimple();
        MoveTimeCursor(cursorTime);
    }

    public void SetRenamingClip(ESEditorTrackClip clip)
    {
        RenamingClip = clip;
    }

    public void ClearRenamingClip(ESEditorTrackClip clip)
    {
        if (RenamingClip == clip)
            RenamingClip = null;
    }

    public void SetRenamingTrack(ESEditorTrackItem track)
    {
        RenamingTrack = track;
    }

    public void ClearRenamingTrack(ESEditorTrackItem track)
    {
        if (RenamingTrack == track)
            RenamingTrack = null;
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

        if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
        {
            DeleteSelectedClips();
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

        if (evt.button == 0 && !evt.ctrlKey && !evt.commandKey && !IsEventTargetInsideClip(evt))
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
        int targetIndex = Items.Count;
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            float middleY = item.layout.y + item.layout.height * 0.5f;
            if (localY < middleY)
            {
                targetIndex = i;
                break;
            }
        }

        return ESTrackViewIconUtility.ClampUserTrackInsertIndex(targetIndex, Items.Count);
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
        m_TrackInsertLine.style.backgroundColor = new Color(0.82f, 0.72f, 0.36f, 1f);
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
        UpdatePreselectEntityFromSelection(true);
    }

    private void RefreshPreselectEntityDelayed()
    {
        if (this == null)
            return;

        UpdatePreselectEntityFromSelection(false);
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
        EditorTimelinePlayer.Instance.ActiveSequence = rebuiltPlayer;
        rebuiltPlayer.SetTime(keepTime);
        ScheduleAutoValidateSequenceVisuals();
    }

    [MenuItem(MenuItemPathDefine.GAMEPLAY_BUILDING_PATH + "技能轨道/临时播放当前技能序列", false, 20)]
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
        Entity[] entities = FindObjectsOfType<Entity>();
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
        // 1. 更新工具栏上的时间文本
        // 假设你有一个方法能拿到工具栏引用，例如：
        window.toolbar.UpdateTime(time);

        cursorTime = time;
        // 2. 移动时间游标（如果你有实现的话）
        MoveTimeCursor(time);

        // 3. 高亮当前播放片段
        HighlightActiveClips(time);

        // 如果上述功能还没建好，直接打日志或留空即可
        // Debug.Log("当前时间: " + time);
    }
    private void MoveTimeCursor(float currentTime)
    {
        if (timeCursor == null) return;

        // 当前显示区域的起始时间
        float startShow = StartShow;   // 或者 window.StartShow
                                       // 当前每像素秒数
        float pixelsPerSec = pixelPerSecond;

        // 计算游标在 rightPanel 内的 x 位置
        // 注意：剪辑块起始偏移为 LeftTrackPixel，因此游标也需要加上这个偏移
        float xPos = ESTrackViewWindow.LeftTrackPixel + (currentTime - startShow) * pixelsPerSec;

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
    }

    private void OnTimeCursorMouseUp(MouseUpEvent evt)
    {
        if (!isDraggingCursor || evt.button != 0) return;
        ForceEndCursorDrag();
        evt.StopPropagation();
    }

    private void ForceEndCursorDrag()
    {
        isDraggingCursor = false;
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
        return;
        // 检查控制键状态
        if (evt.ctrlKey)
        {
            // Ctrl+滚轮：缩放

        }
        else if (evt.shiftKey)
        {
            // Shift+滚轮：水平滚动
            //  HandleHorizontalScroll(evt);
            evt.StopPropagation();
        }
        else
        {
            // 普通滚轮：垂直滚动
            //   HandleVerticalScroll(evt);
            evt.StopPropagation();
        }
    }

    private void HandleZoomHorScale(WheelEvent evt)
    {
        // 计算缩放中心（鼠标位置）
        Vector2 localMousePos = m_ContentContainer.WorldToLocal(evt.mousePosition);
        m_ZoomCenter = localMousePos;

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
        // 计算缩放中心（鼠标位置）
        Vector2 localMousePos = m_ContentContainer.WorldToLocal(evt.mousePosition);
        m_ZoomCenter = localMousePos;

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

    #region  鼠标离开和右键空
    private void OnMouseLeave(MouseLeaveEvent evt)
    {
        return;
        // 鼠标离开时结束当前操作
        if (m_CurrentMode == InteractionMode.Panning)
        {
            EndPanning();
        }
        else if (m_CurrentMode == InteractionMode.Selecting)
        {
            EndSelection();
        }
    }

    private void OnContextClick_CompleteMenu(ContextClickEvent evt)
    {
        // 显示上下文菜单
        var menu = new GenericMenu();

        AppendMenuItems_Refresh(menu);

        menu.AddSeparator("");

        AppendMenuItems_AddTrack(menu);

        menu.AddItem(new GUIContent("视图设置"), false, () =>
        {
            // ShowViewSettings();
        });

        menu.ShowAsContext();
    }
    private void ShowMenu_AddTrack()
    {
        // 显示上下文菜单
        var menu = new GenericMenu();
        AppendMenuItems_Refresh(menu);
        AppendMenuItems_AddTrack(menu);

        menu.AddSeparator("");

        menu.ShowAsContext();
    }
    private void ShowMenu_CompleteAndAddTrack()
    {
        // 显示上下文菜单
        var menu = new GenericMenu();

        AppendMenuItems_AddTrack(menu);

        menu.AddSeparator("");

        menu.ShowAsContext();
    }
    public OdinEditorWindow Last_EditorWindowForTrackItem;
    public OdinEditorWindow Last_EditorWindowForTrackClip;
    public OdinEditorWindow Last_EditorWindowForSkillDataInfo;

    public void ShowMenu_SelectTrackAndAddTrack(ESEditorTrackItem trackItem)
    {
        if (trackItem == null || trackItem.item == null)
            return;

        var menu = new GenericMenu();
        AppendMenuItems_Refresh(menu);
        AppendMenuItems_AddClip(menu, trackItem);
        AppendMenuItems_PasteClip(menu, trackItem);
        menu.AddItem(new GUIContent("【整理片段】/按开始时间排序"), false, () => SortTrackClipsByTime(trackItem));
        menu.AddSeparator("");

        menu.AddItem(new GUIContent("编辑轨道项目"), false, () =>
        {
            drawerSOForTrackItem.drawerData = trackItem.item;
            if (Last_EditorWindowForTrackItem != null)
                Last_EditorWindowForTrackItem.Close();

            trackItem.UpdateNodeMatchAndForeachUpdate();
            trackItem.UpdateWhenEdit();
            Last_EditorWindowForTrackItem = ESTrackItemTemporaryInspectorWindow.OpenFor(
                drawerSOForTrackItem,
                "编辑轨道<" + trackItem.item.DisplayName + ">",
                "轨道项目",
                () =>
            {
                drawerSOForTrackItem.drawerData = null;
                ESTrackViewWindowHelper.SaveContainerChanges();
                trackItem.UpdateNodeMatchAndForeachUpdate();
                trackItem.UpdateWhenEdit();
            });
        });
        menu.AddSeparator("");

        if (trackItem.IsProtectedBasicTrack)
        {
            menu.AddDisabledItem(new GUIContent("【危险操作】/基础轨道不可删除"));
            menu.AddDisabledItem(new GUIContent("【危险操作】/基础轨道不可参与轨道排序"));
        }
        else
        {
            menu.AddItem(new GUIContent("【危险操作】/删除轨道项目"), false, () =>
            {
                if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog(
                    "删除轨道" + trackItem.item.DisplayName,
                    "确认删除该轨道吗？\n虽然记录了可撤销但是不能保证完整恢复",
                    "删除",
                    "取消"))
                {
                    Undo.RecordObjects(new UnityEngine.Object[] { ESTrackViewWindow.TrackContainer as UnityEngine.Object, this as UnityEngine.Object }, "Remove Track Item");
                    ESTrackViewWindowHelper.RemoveTrackItemToCurrentSequence(trackItem);
                }
            });
        }

        menu.AddSeparator("");
        AppendMenuItems_AddTrack(menu);
        menu.ShowAsContext();
    }
    public void ShowMenu_SelectClip(ESEditorTrackClip clip)
    {
        // 显示上下文菜单
        var menu = new GenericMenu();

        AppendMenuItems_Refresh(menu);

        menu.AddItem(new GUIContent("编辑片段"), false, () =>
       {

           ESTrackViewWindowHelper.EditClip(clip);


           // EditorGUIUtility.ShowObjectPicker<VisualGUIDrawerSO>(drawerSO, false, "", 0);
        });
        if (clip != null && clip.trackClip != null)
        {
            if (!IsClipSelected(clip))
                SelectClip(clip, false);

            string enabledText = clip.trackClip.Enabled ? "禁用片段" : "启用片段";
            menu.AddItem(new GUIContent(enabledText), false, clip.ToggleEnabled);
            int selectedCount = m_SelectedClips.Count;
            string copyText = selectedCount > 1 ? $"复制选中片段 ({selectedCount})" : "复制片段";
            menu.AddItem(new GUIContent(copyText), false, () => CopyClipToClipboard(clip));
            menu.AddItem(new GUIContent("【更多功能】/对齐播放头"), false, AlignSelectedClipsToPlayhead);
        }
        menu.AddSeparator("");

        menu.AddItem(new GUIContent("【危险操作】/删除片段"), false, () =>
        {
            if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("删除片段" + clip.trackClip.DisplayName, "确认删除该片段吗？\n虽然记录了可撤销但是不能保证完整恢复", "删除", "取消"))
            {
                Undo.RecordObjects(new UnityEngine.Object[] { ESTrackViewWindow.TrackContainer as UnityEngine.Object, this as UnityEngine.Object }, "Remove Track Item 2");
                ESTrackViewWindowHelper.RemoveTrackClipToCurrentSequence(clip);
            }//Undo.RecordObject(ESTrackViewWindow.TrackContainer as UnityEngine.Object, "Remove Track Item");
        });
        menu.AddSeparator("");

        menu.AddSeparator("");

        menu.ShowAsContext();
    }


    public void AppendMenuItems_AddTrack(GenericMenu GenericMenu)
    {
        if (ESTrackViewWindow.TrackContainer != null && ESTrackViewWindow.Sequence != null)
        {
            var type = ESTrackViewWindow.TrackContainer.trackItemType;
            if (ESTrackViewWindowHelper.AllTrackItemTypes.TryGetValue(type, out var values))
            {

                foreach (var i in values)
                {
                    if (i.type != null && typeof(ITrackItem).IsAssignableFrom(i.type) && i.type.GetConstructor(Type.EmptyTypes) != null)
                    {
                        GenericMenu.AddItem(new GUIContent("【添加轨道】/" + i.name), false, () =>
                        {
                            ESTrackViewWindowHelper.AddNewTrackItemToCurrentSequence(i.type);
                        });
                    }
                }

            }
        }
    }

    public void AppendMenuItems_AddClip(GenericMenu GenericMenu, ESEditorTrackItem forItem)
    {
        if (ESTrackViewWindow.TrackContainer != null && ESTrackViewWindow.Sequence != null)
        {
            if (forItem.item == null) return;
            var types = forItem.item.SupportedClipTypes();
            foreach (var type in types)
            {
                if (type != null && typeof(ITrackClip).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null)
                {
                    GenericMenu.AddItem(new GUIContent("【添加片段】/" + type._GetTypeDisplayName()._KeepAfterByLast("/")), false, () =>
                    {
                        var clip = Activator.CreateInstance(type) as ITrackClip;

                        if (clip != null)
                        {
                            var clipEditor = forItem.AddClip(clip, false);
                            // 将绝对位置转换为相对于targetArea的本地位置
                            clipEditor.style.left = forItem.recordLocalClipsMousePos.x;
                            // Debug.Log("添加片段 位置" + forItem.recordLocalClipsMousePos.x);
                            clipEditor.MatchTimeFromDynamicPos();
                            SortTrackClipsByTime(forItem);
                            SyncTotalTimeFromCurrentSequence(true);
                        }
                    });
                }
            }
        }
    }

    private void AppendMenuItems_PasteClip(GenericMenu menu, ESEditorTrackItem forItem)
    {
        if (menu == null || forItem == null || forItem.item == null)
            return;

        if (!HasCopiedClips())
        {
            menu.AddDisabledItem(new GUIContent("【粘贴片段】/没有可粘贴片段"));
            return;
        }

        if (CanPasteCopiedClipsToOriginalTracks())
        {
            menu.AddItem(new GUIContent("【粘贴片段】/按原轨道右移1秒"), false, () =>
            {
                PasteCopiedClipsToOriginalTracks(1f, true);
            });
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("【粘贴片段】/原轨道不可用"));
        }

        if (CanPasteClipToTrack(forItem))
        {
            menu.AddItem(new GUIContent("【粘贴片段】/粘贴到当前轨道播放头"), false, () =>
            {
                PasteClipToTrack(forItem, cursorTime, true);
            });
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
            SelectClip(pastedClips[i], true);

        for (int i = 0; i < changedTracks.Count; i++)
        {
            ESEditorTrackItem track = changedTracks[i];
            if (track == null || track.item == null)
                continue;

            track.item.SortClipsByTime();
            track.MarkVisibilityCacheDirty();
            track.UpdateNodeMatchAndForeachUpdate(true);
        }

        ESDesignUtility.SafeEditor.Wrap_SetDirty(TrackContainer as UnityEngine.Object);
        SkillSequenceRuntimeCache.NotifySequenceChanged(Sequence);
        SyncTotalTimeFromCurrentSequence(true);
        RebuildActivePreviewPlayer();

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

        clip.StartTime = Mathf.Max(0f, startTime);
        if (recordUndo && TrackContainer is UnityEngine.Object undoTarget)
            Undo.RecordObject(undoTarget, "粘贴轨道片段");

        ESEditorTrackClip editorClip = forItem.AddClip(clip, false);
        if (editorClip != null)
        {
            editorClip.SetTimeScaleAndStartShowCache();
            SelectClip(editorClip);
        }

        SortTrackClipsByTime(forItem);
        ESDesignUtility.SafeEditor.Wrap_SetDirty(TrackContainer as UnityEngine.Object);
        SkillSequenceRuntimeCache.NotifySequenceChanged(Sequence);
        SyncTotalTimeFromCurrentSequence(true);
        RebuildActivePreviewPlayer();
    }

    private void SortTrackClipsByTime(ESEditorTrackItem trackItem)
    {
        if (trackItem == null || trackItem.item == null)
            return;

        UnityEngine.Object undoTarget = TrackContainer as UnityEngine.Object;
        if (undoTarget != null)
            Undo.RecordObject(undoTarget, "按开始时间排序片段");

        bool changed = trackItem.item.SortClipsByTime();
        if (!changed)
            return;

        trackItem.MarkVisibilityCacheDirty();
        trackItem.UpdateNodeMatchAndForeachUpdate(true);
        ESDesignUtility.SafeEditor.Wrap_SetDirty(TrackContainer as UnityEngine.Object);
        SkillSequenceRuntimeCache.NotifySequenceChanged(Sequence);
        SyncTotalTimeFromCurrentSequence(true);
        RebuildActivePreviewPlayer();
    }


    public void AppendMenuItems_Refresh(GenericMenu GenericMenu)
    {
        GenericMenu.AddItem(new GUIContent("【刷新】/重建全部轨道视图"), false, () =>
        {
            InitNewSequenceAndOpenWindow();
        });

        GenericMenu.AddItem(new GUIContent("【刷新】/刷新片段节点"), false, () =>
        {
            SyncTotalTimeFromCurrentSequence(true);
            UpdateClipsSimple();
        });

        GenericMenu.AddItem(new GUIContent("【校验】/检查当前技能序列"), false, ValidateCurrentSequenceAndReport);
        GenericMenu.AddItem(new GUIContent("【更多功能】/定位首个错误"), false, LocateFirstValidationError);
        GenericMenu.AddItem(new GUIContent("【更多功能】/定位下一个错误"), false, LocateNextValidationError);

        GenericMenu.AddSeparator("");

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
            if (item == null || item.TrackClips == null)
                continue;

            for (int j = 0; j < item.TrackClips.Count; j++)
            {
                ESEditorTrackClip editorClip = item.TrackClips[j];
                if (editorClip != null && ReferenceEquals(editorClip.trackClip, clip))
                    return editorClip;
            }
        }

        return null;
    }

    private void ScheduleAutoValidateSequenceVisuals()
    {
        if (m_AutoValidationScheduled || rootVisualElement == null)
            return;

        m_AutoValidationScheduled = true;
        rootVisualElement.schedule.Execute(() =>
        {
            m_AutoValidationScheduled = false;
            AutoValidateSequenceVisuals();
        }).ExecuteLater(80);
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

    private static void ValidateSequence(ITrackSequence sequence, List<string> warnings, List<string> infos, Dictionary<ITrackClip, string> clipWarnings)
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
            if (audioClip.audioClip == null)
                AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / 片段[{clipIndex}] {clipName} 未指定 AudioClip。");
            else if (audioClip.stopOnClipExit && audioClip.DurationTime + 0.05f < audioClip.audioClip.length)
                AddClipWarning(warnings, clipWarnings, clip, $"轨道[{trackIndex}] {trackName} / {clipName} 开启了离开片段停止音效，但片段时长({audioClip.DurationTime:F2}s)短于音频({audioClip.audioClip.length:F2}s)，可能被截断。");
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

        // 检查哪些元素在选择框内
        // CheckElementsInSelection();
    }

    private void EndSelection()
    {
        m_CurrentMode = InteractionMode.None;
        m_IsSelecting = false;

        // 隐藏选择框
        m_SelectionVisual.style.display = DisplayStyle.None;

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

    private void UpdateSelectionVisual()
    {
        m_SelectionVisual.style.left = m_SelectionRect.x;
        m_SelectionVisual.style.top = m_SelectionRect.y;
        m_SelectionVisual.style.width = m_SelectionRect.width;
        m_SelectionVisual.style.height = m_SelectionRect.height;
    }

    #endregion

    #region  常规按钮
    private void OnCreatorButtonClickRight(ClickEvent click)
    {
        if (click.button == 1)
            HandleCreatorOpeartion();
    }
    private void OnCreatorButtonClickLeft(ClickEvent click)
    {
        if (click.button == 0)
            ShowMenu_AddTrack();
    }

    private void HandleCreatorOpeartion()
    {
        ShowMenu_CompleteAndAddTrack();
    }



    #endregion



    private void UpdateClipsSimple()
    {
        if (ruler == null || ruler.TopRuler == null || Items == null)
            return;

        ruler.TopRuler.MarkDirtyRepaint();
        float visibleStart = StartShow;
        float visibleEnd = GetVisibleEndTime();
        foreach (var i in Items)
        {
            if (i != null)
                i.UpdateNodes(visibleStart, visibleEnd);
        }
        ScheduleAutoValidateSequenceVisuals();
    }

    private void ScheduleViewRefresh()
    {
        if (m_ViewRefreshScheduled)
            return;

        m_ViewRefreshScheduled = true;
        EditorApplication.update -= FlushScheduledViewRefresh;
        EditorApplication.update += FlushScheduledViewRefresh;
    }

    private void FlushScheduledViewRefresh()
    {
        EditorApplication.update -= FlushScheduledViewRefresh;
        if (!m_ViewRefreshScheduled)
            return;

        m_ViewRefreshScheduled = false;
        if (this == null || rootVisualElement == null)
            return;

        UpdateClipsSimple();
        MoveTimeCursor(cursorTime);
    }
}


public enum TrackMoveCommand
{
    StepUp,
    StepDown,
    ToMovableTop,
    ToBottom
}

public class ESTrackViewWindowHelper : EditorInvoker_Level0

{
    public static Dictionary<TrackItemType, List<(string name, Type type)>> AllTrackItemTypes = new Dictionary<TrackItemType, List<(string name, Type type)>>();
    private static IEditorTrackSupport_GetSequence s_PendingSelectionTrackContainer;
    private static bool s_SelectionTrackRefreshScheduled;

    public override void InitInvoke()
    {
        Selection.selectionChanged -= ForTrackWindowSelection;
        Selection.selectionChanged += ForTrackWindowSelection;
    }

    private static void ForTrackWindowSelection()
    {
        ESTrackViewWindow.window?.UpdatePreselectEntityFromSelection(true);

        if (Selection.activeObject is IEditorTrackSupport_GetSequence SupportSequence)
        {
            Debug.Log("已经选中了技能序列" + Selection.activeObject.name);
            if (SupportSequence.Sequence != null)
                ScheduleOpenAndRefreshTrackWindow(SupportSequence);
        }
    }

    private static void ScheduleOpenAndRefreshTrackWindow(IEditorTrackSupport_GetSequence supportSequence)
    {
        if (supportSequence == null || supportSequence.Sequence == null)
            return;

        s_PendingSelectionTrackContainer = supportSequence;
        if (s_SelectionTrackRefreshScheduled)
            return;

        s_SelectionTrackRefreshScheduled = true;
        EditorApplication.delayCall += FlushOpenAndRefreshTrackWindow;
    }

    public static void CancelPendingSelectionTrackRefresh()
    {
        if (s_SelectionTrackRefreshScheduled)
            EditorApplication.delayCall -= FlushOpenAndRefreshTrackWindow;

        s_PendingSelectionTrackContainer = null;
        s_SelectionTrackRefreshScheduled = false;
    }

    private static void FlushOpenAndRefreshTrackWindow()
    {
        EditorApplication.delayCall -= FlushOpenAndRefreshTrackWindow;
        s_SelectionTrackRefreshScheduled = false;

        IEditorTrackSupport_GetSequence supportSequence = s_PendingSelectionTrackContainer;
        s_PendingSelectionTrackContainer = null;
        if (supportSequence == null || supportSequence.Sequence == null)
            return;

        ESTrackViewWindow.TryUpdateTrackSequence(supportSequence);
        if (ESTrackViewWindow.window != null)
        {
            ESTrackViewWindow.window.ForceRefreshClipLayoutNow();
            ESTrackViewWindow.window.Repaint();
        }
    }

    public static void AddNewTrackItemToCurrentSequence(Type itemType)
    {
        if (ESTrackViewWindow.Sequence != null && ESTrackViewWindow.window != null)
        {
            if (itemType != null && typeof(ITrackItem).IsAssignableFrom(itemType) && itemType.GetConstructor(Type.EmptyTypes) != null)
            {
                var newItem = Activator.CreateInstance(itemType) as ITrackItem;
                if (newItem != null)
                {

                    if (ESTrackViewWindow.Sequence.TryAddTrackItem(newItem))
                    {
                        var item = new ESEditorTrackItem().InitWithItem(newItem);
                        ESTrackViewWindow.window.leftPanel.Add(item);
                        ESTrackViewWindow.window.Items.Add(item);
                        ESDesignUtility.SafeEditor.Wrap_SetDirty(ESTrackViewWindow.TrackContainer as UnityEngine.Object);
                        SkillSequenceRuntimeCache.NotifySequenceChanged(ESTrackViewWindow.Sequence);
                    }
                }
            }
        }
    }

    public static bool MoveTrackItemInCurrentSequence(ESEditorTrackItem editorTrack, TrackMoveCommand command)
    {
        if (editorTrack == null || editorTrack.item == null)
            return false;

        if (editorTrack.IsProtectedBasicTrack)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道不可参与排序。");
            return false;
        }

        IList tracks = GetMutableTrackList(ESTrackViewWindow.Sequence);
        if (tracks == null)
        {
            Debug.LogWarning("[轨道编辑器] 当前序列不支持轨道排序。");
            return false;
        }

        int oldIndex = tracks.IndexOf(editorTrack.item);
        int minIndex = ESTrackViewIconUtility.ProtectedBasicTrackCount;
        if (oldIndex < minIndex)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道区域不可参与排序。");
            return false;
        }

        int newIndex = oldIndex;
        switch (command)
        {
            case TrackMoveCommand.StepUp:
                newIndex = oldIndex - 1;
                break;
            case TrackMoveCommand.StepDown:
                newIndex = oldIndex + 1;
                break;
            case TrackMoveCommand.ToMovableTop:
                newIndex = minIndex;
                break;
            case TrackMoveCommand.ToBottom:
                newIndex = tracks.Count - 1;
                break;
        }

        newIndex = ESTrackViewIconUtility.ClampUserTrackInsertIndex(newIndex, tracks.Count - 1);
        if (newIndex == oldIndex)
            return false;

        UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
        if (undoTarget != null)
            Undo.RecordObject(undoTarget, "调整轨道顺序");

        return MoveTrackItemToFinalIndexInCurrentSequence(editorTrack, newIndex);
    }

    public static bool MoveTrackItemToFinalIndexInCurrentSequence(ESEditorTrackItem editorTrack, int targetFinalIndex)
    {
        if (editorTrack == null || editorTrack.item == null)
            return false;

        if (editorTrack.IsProtectedBasicTrack)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道不可参与排序。");
            return false;
        }

        IList tracks = GetMutableTrackList(ESTrackViewWindow.Sequence);
        if (tracks == null)
        {
            Debug.LogWarning("[轨道编辑器] 当前序列不支持轨道排序。");
            return false;
        }

        int oldIndex = tracks.IndexOf(editorTrack.item);
        int minIndex = ESTrackViewIconUtility.ProtectedBasicTrackCount;
        if (oldIndex < minIndex)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道区域不可参与排序。");
            return false;
        }

        int finalIndex = Mathf.Clamp(targetFinalIndex, minIndex, tracks.Count - 1);
        if (finalIndex == oldIndex)
            return false;

        UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
        if (undoTarget != null)
            Undo.RecordObject(undoTarget, "调整轨道顺序");

        object item = tracks[oldIndex];
        tracks.RemoveAt(oldIndex);
        tracks.Insert(finalIndex, item);

        ESDesignUtility.SafeEditor.Wrap_SetDirty(ESTrackViewWindow.TrackContainer as UnityEngine.Object);
        SkillSequenceRuntimeCache.NotifySequenceChanged(ESTrackViewWindow.Sequence);
        return true;
    }

    public static bool MoveTrackItemToIndexInCurrentSequence(ESEditorTrackItem editorTrack, int targetIndex)
    {
        if (editorTrack == null || editorTrack.item == null)
            return false;

        if (editorTrack.IsProtectedBasicTrack)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道不可参与排序。");
            return false;
        }

        IList tracks = GetMutableTrackList(ESTrackViewWindow.Sequence);
        if (tracks == null)
        {
            Debug.LogWarning("[轨道编辑器] 当前序列不支持轨道排序。");
            return false;
        }

        int oldIndex = tracks.IndexOf(editorTrack.item);
        int minIndex = ESTrackViewIconUtility.ProtectedBasicTrackCount;
        if (oldIndex < minIndex)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道区域不可参与排序。");
            return false;
        }

        int insertIndex = ESTrackViewIconUtility.ClampUserTrackInsertIndex(targetIndex, tracks.Count);
        if (insertIndex > oldIndex)
            insertIndex--;

        insertIndex = Mathf.Clamp(insertIndex, minIndex, tracks.Count - 1);
        if (insertIndex == oldIndex)
            return false;

        UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
        if (undoTarget != null)
            Undo.RecordObject(undoTarget, "调整轨道顺序");

        object item = tracks[oldIndex];
        tracks.RemoveAt(oldIndex);
        tracks.Insert(insertIndex, item);

        ESDesignUtility.SafeEditor.Wrap_SetDirty(ESTrackViewWindow.TrackContainer as UnityEngine.Object);
        SkillSequenceRuntimeCache.NotifySequenceChanged(ESTrackViewWindow.Sequence);
        return true;
    }

    private static IList GetMutableTrackList(ITrackSequence sequence)
    {
        if (sequence == null)
            return null;

        Type type = sequence.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField("tracks_", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(sequence) as IList;

            type = type.BaseType;
        }

        return null;
    }

    public static void RemoveTrackItemToCurrentSequence(ESEditorTrackItem ediTrack)
    {
        if (ediTrack == null || ediTrack.IsProtectedBasicTrack)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道不可删除。");
            return;
        }

        if (ESTrackViewWindow.Sequence != null && ESTrackViewWindow.window != null)
        {
            var item = ediTrack.item;
            if (item != null && typeof(ITrackItem).IsAssignableFrom(item.GetType()))
            {


                if (ESTrackViewWindow.Sequence.TryRemoveTrackItem(item))
                {
                    ESTrackViewWindow.window.leftPanel.Remove(ediTrack);
                    ESDesignUtility.SafeEditor.Wrap_SetDirty(ESTrackViewWindow.TrackContainer as UnityEngine.Object);
                    SkillSequenceRuntimeCache.NotifySequenceChanged(ESTrackViewWindow.Sequence);
                }

            }
        }
    }

    public static void RemoveTrackClipToCurrentSequence(ESEditorTrackClip clip)
    {
        if (ESTrackViewWindow.Sequence != null && ESTrackViewWindow.window != null)
        {
            var item = clip.trackClip;
            if (item != null && typeof(ITrackClip).IsAssignableFrom(item.GetType()))
            {

                foreach (var trackItemEditor in ESTrackViewWindow.window.Items)
                {
                    if (trackItemEditor.item.TryRemoveTrackClip(clip.trackClip))
                    {
                        trackItemEditor.RemoveClip(clip);
                        ESDesignUtility.SafeEditor.Wrap_SetDirty(ESTrackViewWindow.TrackContainer as UnityEngine.Object);
                        SkillSequenceRuntimeCache.NotifySequenceChanged(ESTrackViewWindow.Sequence);
                        ESTrackViewWindow.window.SyncTotalTimeFromCurrentSequence(false);
                        break;
                    }
                }
            }
        }
    }

    public static void EditClip(ESEditorTrackClip clip)
    {
        if (clip == null || clip.trackClip == null || ESTrackViewWindow.window == null)
            return;

        ESTrackViewWindow trackWindow = ESTrackViewWindow.window;
        if (ESTrackViewWindow.window.Last_EditorWindowForTrackClip != null &&
            ESTrackViewWindow.window.drawerSOForTrackClip != null &&
            ReferenceEquals(ESTrackViewWindow.window.drawerSOForTrackClip.drawerData, clip.trackClip))
        {
            trackWindow.SetFocusedEditingClip(clip);
            ESTrackViewWindow.window.Last_EditorWindowForTrackClip.Focus();
            return;
        }

        trackWindow.SetFocusedEditingClip(clip);
        if (ESTrackViewWindow.window.Last_EditorWindowForTrackClip != null)
        {
            ESTrackViewWindow.window.Last_EditorWindowForTrackClip.Close();
        }
        ESTrackViewWindow.window.drawerSOForTrackClip.drawerData = clip.trackClip;
        clip.SetTimeScaleAndStartShowCache();
        clip.UpdateNodeView();
        ESTrackViewWindow.window.Last_EditorWindowForTrackClip = ESTrackClipTemporaryInspectorWindow.OpenFor(
            ESTrackViewWindow.window.drawerSOForTrackClip,
            "编辑片段<" + clip.trackClip.DisplayName,
            "片段",
            () =>
        {
            clip.SetTimeScaleAndStartShowCache();
            clip.UpdateNodeView();
            if (trackWindow != null)
            {
                trackWindow.ClearFocusedEditingClip(clip);
                if (trackWindow.drawerSOForTrackClip != null && ReferenceEquals(trackWindow.drawerSOForTrackClip.drawerData, clip.trackClip))
                    trackWindow.drawerSOForTrackClip.drawerData = null;
            }
            ESTrackViewWindowHelper.SaveContainerChanges();
        });
    }

    public static void SaveContainerChanges()
    {
        if (ESTrackViewWindow.TrackContainer != null)
        {
            Undo.RecordObject(ESTrackViewWindow.TrackContainer as UnityEngine.Object, "Save Track Container Changes");
            ESDesignUtility.SafeEditor.Wrap_SetDirty(ESTrackViewWindow.TrackContainer as UnityEngine.Object);
            SkillSequenceRuntimeCache.NotifySequenceChanged(ESTrackViewWindow.Sequence);
        }
    }

    public static void SaveContainerDisplayChanges()
    {
        if (ESTrackViewWindow.TrackContainer != null)
        {
            ESDesignUtility.SafeEditor.Wrap_SetDirty(ESTrackViewWindow.TrackContainer as UnityEngine.Object);
        }
    }
}

#region  编辑器注册器

public class ESEditorTrackItemRegister : EditorRegister_FOR_ClassAttribute<CreateTrackItemAttribute>
{
    public override void Handle(CreateTrackItemAttribute attribute, Type type)
    {
        // Debug.Log("收集-"+attribute.menuName+"-"+type);
        if (ESTrackViewWindowHelper.AllTrackItemTypes.TryGetValue(attribute.itemType, out var list))
        {
            list.Add((attribute.menuName, type));
        }
        else
        {
            ESTrackViewWindowHelper.AllTrackItemTypes.Add(attribute.itemType, new List<(string name, Type type)> { (attribute.menuName, type) });
        }
    }
}


#endregion

// 恢复警告
#pragma warning restore CS0414
#pragma warning restore CS0162










