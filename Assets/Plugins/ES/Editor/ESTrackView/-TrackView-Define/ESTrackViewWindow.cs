using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using ES;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// 抑制私有字段未使用警告
#pragma warning disable CS0414
// 抑制无法访问的代码警告（提前return）
#pragma warning disable CS0162

public class ESTrackViewWindow : OdinEditorWindow
{
    public static ESTrackViewWindow window;
    public static ITrackSequence Sequence { get { if (TrackContainer != null) return TrackContainer.Sequence; return null; } }
    public static IEditorTrackSupport_GetSequence TrackContainer;

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
    protected override void OnDisable()
    {
        base.OnDisable();
        if (Last_EditorWindowForTrackItem != null)
        {
            Last_EditorWindowForTrackItem.Close();
        }
        if (Last_EditorWindowForTrackClip != null)
        {
            Last_EditorWindowForTrackClip.Close();
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

    [MenuItem(MenuItemPathDefine.EDITOR_TOOLS_PATH + "多轨编辑器", false, 2)]
    public static void OpenWindow()
    {
        window = GetWindow<ESTrackViewWindow>();
        window.titleContent = new GUIContent("轨道编辑器");
    }
    public static void InitNewSequenceAndOpenWindow()
    {
        OpenWindow();
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
            foreach (var t in Sequence.Tracks)
            {
                var item = new ESEditorTrackItem().InitWithItem(t);
                ESTrackViewWindow.window.leftPanel.Add(item);
                ESTrackViewWindow.window.Items.Add(item);
            }
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


    }

    private void FindTrackAssets()
    {
        if (Sequence != null) InitNewSequenceAndOpenWindow();
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

    public void ShowMenu_SelectTrackAndAddTrack(ESEditorTrackItem trackItem)
    {
        // 显示上下文菜单
        var menu = new GenericMenu();

        AppendMenuItems_Refresh(menu);



        AppendMenuItems_AddClip(menu, trackItem);

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("【轨道】编辑轨道"), false, () =>
       {
           drawerSOForTrackItem.drawerData = trackItem.item;
           if (Last_EditorWindowForTrackItem != null)
           {
               Last_EditorWindowForTrackItem.Close();
           }
           trackItem.UpdateNodeMatch();
           Last_EditorWindowForTrackItem = InspectObject(drawerSOForTrackItem);
           // Debug.Log("开始编辑轨道" + trackItem.item.GetType() + trackItem.item.DisplayName);
           Last_EditorWindowForTrackItem.titleContent = new GUIContent("编辑轨道" + "<" + trackItem.item.DisplayName);

           Last_EditorWindowForTrackItem.OnClose += () =>
           {
               drawerSOForTrackItem.drawerData = null;
               ESTrackViewWindowHelper.SaveContainerChanges();
               trackItem.UpdateNodeMatch();
           };



           // EditorGUIUtility.ShowObjectPicker<VisualGUIDrawerSO>(drawerSO, false, "", 0);
       });
        menu.AddSeparator("");

        menu.AddItem(new GUIContent("【轨道】删除轨道"), false, () =>
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

    private void UpdateCursor()
    {
       // switch (m_CurrentMode)
        {
            // case InteractionMode.Panning:
            //     rightPanel.style.cursor = LoadCursor("PanCursor");
            //     break;

            // case InteractionMode.Selecting:
            //     rightPanel.style.cursor = LoadCursor("SelectCursor");
            //     break;

            // default:
            //     rightPanel.style.cursor = Texture2D.whiteTexture;
            //     break;
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
            i.UpdateNodesPos();
        }
    }
}


public class ESTrackViewWindowHelper : EditorInvoker_Level0

{
    public static Dictionary<TrackItemType, List<(string name, Type type)>> AllTrackItemTypes = new Dictionary<TrackItemType, List<(string name, Type type)>>();

    public override void InitInvoke()
    {
        Selection.selectionChanged += ForTrackWindowSelection;
    }

    private static void ForTrackWindowSelection()
    {
        if (Selection.activeObject is IEditorTrackSupport_GetSequence SupportSequence)
        {
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
        ESTrackViewWindow.window.Last_EditorWindowForTrackClip = ESTrackViewWindow.InspectObject(ESTrackViewWindow.window.drawerSOForTrackClip);
        Debug.Log("开始编辑片段" + clip.trackClip.GetType() + clip.trackClip.DisplayName);
        ESTrackViewWindow.window.Last_EditorWindowForTrackClip.titleContent = new GUIContent("编辑片段<" + clip.trackClip.DisplayName);

        ESTrackViewWindow.window.Last_EditorWindowForTrackClip.OnClose += () =>
        {
            clip.SetTimeScaleAndStartShowCache();
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










