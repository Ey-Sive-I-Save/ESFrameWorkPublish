using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using ES;
using ES.ES;
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

    // 播放器实例
    public static EditorTimelinePlayer Player => EditorTimelinePlayer.Instance;



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
    }
    protected override void OnDisable()
    {
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
    public float startScale = 0;
    public float endScale = 1;
    public float pixelPerSecond = 100;
    public float showScale = 1;
    public static float standPixelPerSecond => (ResolveWidth()) / (TotalTime + 0.5f);
    public float StartShow => startScale * TotalTime;

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
    [MenuItem(MenuItemPathDefine.EDITOR_TOOLS_PATH + "【轨道】编辑器", false, 2)]
    public static void OpenWindow()
    {
        window = GetWindow<ESTrackViewWindow>();
        window.titleContent = new GUIContent("【轨道】编辑器");
    }
    public static void InitNewSequenceAndOpenWindow()
    {
        OpenWindow();
        //简单更新
        if (window.toolbar != null)
        {
            window.toolbar.Name.text = "轴：" + TrackContainer.trackName;
        }
        //开始重建
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
                EditorTimelinePlayer.Instance.ActiveSequence = seqPlayer;  // ★ 关键
            }
            foreach (var t in Sequence.Tracks)
            {
                var item = new ESEditorTrackItem().InitWithItem(t);
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
        if (Selection.activeObject is IEditorTrackSupport_GetSequence support &&
        support.Sequence != null) { InitNewSequenceAndOpenWindow(); return; }

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

        if (window.timeCursor != null) window.timeCursor.BringToFront();

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
        //MINMAX 的 显示范围选定
        horSlider.RegisterValueChangedCallback(HorSliderChange);

        //rightPanel 的快捷操作
        // 1. 鼠标滚轮事件 - 缩放
        rightPanel.RegisterCallback<WheelEvent>(OnRightPanelWheel, TrickleDown.NoTrickleDown);

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
            width = 6,                // 加宽便于拖动，视觉可用 border 控制
            backgroundColor = Color.red,
            top = 0,
            bottom = 0,
            // 设置光标样式
            cursor = new Cursor { texture = EditorGUIUtility.IconContent("d_GridLayout").image as Texture2D, hotspot = Vector2.zero }
        }
        };

        // 视觉上仍显示一条细线
        var innerLine = new VisualElement
        {
            style =
        {
            width = 2,
            backgroundColor = Color.red,
            position = Position.Absolute,
            // 水平居中
            left = 2,
            top = 0,
            bottom = 0
        }
        };
        timeCursor.Add(innerLine);

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
        var seqPlayer = new EditorSequencePlayer
        {
            Name = "名字-未定义",
            Duration = 100,
            Speed = 1f
        };
        seqPlayer.PreviewTarget.SetEntity(editorEntity);

        if (sequence == null || sequence.Tracks == null)
            return seqPlayer;

        foreach (var track in sequence.Tracks)
        {
            if (track == null)
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

        return seqPlayer;
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
                return entity;
        }

        UnityEngine.Object[] selectedObjects = Selection.objects;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            if (selectedObjects[i] is GameObject gameObject)
            {
                Entity entity = FindEntityInSelfOrParents(gameObject);
                if (entity != null)
                    return entity;
            }

            if (selectedObjects[i] is Component component)
            {
                Entity entity = FindEntityInSelfOrParents(component.gameObject);
                if (entity != null)
                    return entity;
            }
        }

        return null;
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
        RefreshEntityDisplay();
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
        RunningEntity = PreselectEntity;
        if (RunningEntity == null)
            Debug.LogWarning("[ESTrackViewWindow] UserEntity is null when starting EditorPlay. Select a GameObject or Component with Entity in self or parents, or choose one from the Entity menu.");

        var activeSequence = EditorTimelinePlayer.Instance.ActiveSequence;
        float keepTime = activeSequence != null ? activeSequence.CurrentTime : cursorTime;
        float keepSpeed = activeSequence != null ? activeSequence.Speed : 1f;
        float keepDuration = activeSequence != null ? activeSequence.Duration : 100f;

        if (Sequence != null)
        {
            var rebuiltPlayer = BuildSequencePlayer(Sequence, RunningEntity);
            rebuiltPlayer.Speed = keepSpeed;
            rebuiltPlayer.Duration = keepDuration;
            EditorTimelinePlayer.Instance.ActiveSequence = rebuiltPlayer;
            rebuiltPlayer.SetTime(keepTime);
        }
        else if (activeSequence != null && activeSequence.PreviewTarget != null && !activeSequence.PreviewTarget.IsRecycled)
        {
            activeSequence.PreviewTarget.SetEntity(RunningEntity);
        }

        RefreshEntityDisplay();
    }

    [MenuItem(MenuItemPathDefine.EDITOR_TOOLS_PATH + "轨道/临时播放当前技能序列", false, 30)]
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
        StateAniDataInfo baseStateInfo = TrackContainer is SKillDataInfo skillDataInfo ? skillDataInfo.baseStateInfo : null;
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
            menu.AddDisabledItem(new GUIContent("No active Entity"));

        menu.ShowAsContext();
    }

    private static string GetEntityMenuPath(Entity entity)
    {
        if (entity == null)
            return "<None>";

        string sceneName = entity.gameObject.scene.IsValid() ? entity.gameObject.scene.name : "NoScene";
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
        startScale = Mathf.Clamp(start, 0, 0.9f);
        endScale = Mathf.Clamp(end, start, 1f);
        showScale = 1 / Mathf.Clamp(Mathf.Abs(startScale - endScale), 0.1f, 10);
        pixelPerSecond = standPixelPerSecond * showScale;
        //Debug.Log("更新V2");
        UpdateClipsSimple();
        MoveTimeCursor(cursorTime);   // 保持时间点，位置自动适配
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
        horSlider.value = new Vector2(start, end);
    }
    #endregion

    #region 鼠标滚轮缩放
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

        evt.StopPropagation();
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
        ApplyStartEndToUISlider(tryStart, tryEnd);
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
        // 显示上下文菜单
        var menu = new GenericMenu();

        AppendMenuItems_Refresh(menu);



        AppendMenuItems_AddClip(menu, trackItem);

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("【编辑】编辑轨道"), false, () =>
       {
           drawerSOForTrackItem.drawerData = trackItem.item;
           if (Last_EditorWindowForTrackItem != null)
           {
               Last_EditorWindowForTrackItem.Close();
           }
           trackItem.UpdateNodeMatchAndForeachUpdate();
           trackItem.UpdateWhenEdit();
           Last_EditorWindowForTrackItem = InspectObject(drawerSOForTrackItem);
           // Debug.Log("开始编辑轨道" + trackItem.item.GetType() + trackItem.item.DisplayName);
           Last_EditorWindowForTrackItem.titleContent = new GUIContent("编辑轨道" + "<" + trackItem.item.DisplayName);

           Last_EditorWindowForTrackItem.OnClose += () =>
           {
               drawerSOForTrackItem.drawerData = null;
               ESTrackViewWindowHelper.SaveContainerChanges();
               trackItem.UpdateNodeMatchAndForeachUpdate();
               trackItem.UpdateWhenEdit();
           };



           // EditorGUIUtility.ShowObjectPicker<VisualGUIDrawerSO>(drawerSO, false, "", 0);
       });
        menu.AddSeparator("");

        menu.AddItem(new GUIContent("【❌️删除】删除轨道"), false, () =>
        {
            if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("删除轨道" + trackItem.item.DisplayName, "确认删除该轨道吗？\n虽然记录了可撤销但是不能保证完整恢复", "删除", "取消"))
            {
                Undo.RecordObjects(new UnityEngine.Object[] { ESTrackViewWindow.TrackContainer as UnityEngine.Object, this as UnityEngine.Object }, "Remove Track Item 2");
                ESTrackViewWindowHelper.RemoveTrackItemToCurrentSequence(trackItem);
            }//Undo.RecordObject(ESTrackViewWindow.TrackContainer as UnityEngine.Object, "Remove Track Item");
        });


        menu.AddSeparator("");

        AppendMenuItems_AddTrack(menu);



        menu.ShowAsContext();
    }

    public void ShowMenu_SelectClip(ESEditorTrackClip clip)
    {
        // 显示上下文菜单
        var menu = new GenericMenu();

        AppendMenuItems_Refresh(menu);

        menu.AddItem(new GUIContent("【片段】编辑片段"), false, () =>
       {

           ESTrackViewWindowHelper.EditClip(clip);


           // EditorGUIUtility.ShowObjectPicker<VisualGUIDrawerSO>(drawerSO, false, "", 0);
       });
        menu.AddSeparator("");

        menu.AddItem(new GUIContent("【片段】删除片段"), false, () =>
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
                        GenericMenu.AddItem(new GUIContent("【添加轨道】" + i.name), false, () =>
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
            var types = forItem.item.SupprtedClipTypes();
            foreach (var type in types)
            {
                if (type != null && typeof(ITrackClip).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null)
                {
                    GenericMenu.AddItem(new GUIContent("【添加片段】" + type._GetTypeDisplayName()._KeepAfterByLast("/")), false, () =>
                    {
                        var clip = Activator.CreateInstance(type) as ITrackClip;

                        if (clip != null)
                        {
                            var clipEditor = forItem.AddClip(clip, false);
                            // 将绝对位置转换为相对于targetArea的本地位置
                            clipEditor.style.left = forItem.recordLocalClipsMousePos.x;
                            // Debug.Log("添加片段 位置" + forItem.recordLocalClipsMousePos.x);
                            clipEditor.MatchTimeFromDynamicPos();
                        }
                    });
                }
            }
        }
    }


    public void AppendMenuItems_Refresh(GenericMenu GenericMenu)
    {
        GenericMenu.AddItem(new GUIContent("重置全部轨道"), false, () =>
        {
            InitNewSequenceAndOpenWindow();
        });

        GenericMenu.AddItem(new GUIContent("节点刷新"), false, () =>
        {
            UpdateClipsSimple();
        });

        GenericMenu.AddSeparator("");

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
                // 使用 Unity Editor 内置的 Pan 光标（抓手）
                rightPanel.style.cursor = s_CursorPan;
                break;

            // 选择模式（框选 Clip / 区域）
            case InteractionMode.Selecting:
                // 使用 Unity Editor 内置的 Cross 光标（十字准星）
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

        ruler.TopRuler.MarkDirtyRepaint();
        var items = rootVisualElement.Query<ESEditorTrackItem>().ToList();
        foreach (var i in items)
        {
            i.UpdateNodes();
        }
    }
}


public class ESTrackViewWindowHelper : EditorInvoker_Level0

{
    public static Dictionary<TrackItemType, List<(string name, Type type)>> AllTrackItemTypes = new Dictionary<TrackItemType, List<(string name, Type type)>>();

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
            var se = SupportSequence.Sequence;
            if (se != null)
            {
                if (ESTrackViewWindow.Sequence != se)
                {
                    ESTrackViewWindow.TryUpdateTrackSequence(SupportSequence);
                }
            }
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
    public static void RemoveTrackItemToCurrentSequence(ESEditorTrackItem ediTrack)
    {
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
                        break;
                    }
                }
            }
        }
    }

    public static void EditClip(ESEditorTrackClip clip)
    {
        ESTrackViewWindow.window.drawerSOForTrackClip.drawerData = clip.trackClip;
        if (ESTrackViewWindow.window.Last_EditorWindowForTrackClip != null)
        {
            ESTrackViewWindow.window.Last_EditorWindowForTrackClip.Close();
        }
        clip.SetTimeScaleAndStartShowCache();
        clip.UpdateNodeView();
        ESTrackViewWindow.window.Last_EditorWindowForTrackClip = ESTrackViewWindow.InspectObject(ESTrackViewWindow.window.drawerSOForTrackClip);
        Debug.Log("开始编辑片段" + clip.trackClip.GetType() + clip.trackClip.DisplayName);
        ESTrackViewWindow.window.Last_EditorWindowForTrackClip.titleContent = new GUIContent("编辑片段<" + clip.trackClip.DisplayName);

        ESTrackViewWindow.window.Last_EditorWindowForTrackClip.OnClose += () =>
        {
            clip.SetTimeScaleAndStartShowCache();
            clip.UpdateNodeView();
            ESTrackViewWindow.window.drawerSOForTrackItem.drawerData = null;
            ESTrackViewWindowHelper.SaveContainerChanges();
        };
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










