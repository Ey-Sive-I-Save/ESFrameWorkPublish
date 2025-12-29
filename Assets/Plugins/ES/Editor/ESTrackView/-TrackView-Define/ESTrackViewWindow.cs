using System;
using System.Collections.Generic;
using DG.Tweening;
using ES;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class ESTrackViewWindow : OdinEditorWindow
{
    public static ESTrackViewWindow window;
    public static ITrackSequence Sequence;

    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;
    #region  标准参数
    // public float showStart=0;
    // public float   TopRuler.style.left = 0;
    //             TopRuler.style.top = 0;
    //             TopRuler.style.width = 1000;
    public float totalTime=10;
    public float startScale=0;
    public float endScale=1;
    public float pixelPerSecond=100;
    public float showScale=1;
    public const int standPixelPerSecond=100;
    public float StartShow=>startScale*totalTime;
    #endregion
    #region  标准窗口元素
    public ESTrackRuler ruler;
    public MinMaxSlider horSlider;
    
    public VisualElement rightPanel;
    public VisualElement leftPanel;

    public ESTrackCreatorToolbar CreatorToolBar;

    public List<ESEditorTrackItem> Items=new List<ESEditorTrackItem>();


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
  private VisualElement m_ContentContainer=>rightPanel;
    
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

    [MenuItem("Tools/ES工具/多轨编辑器")]
    public static void ShowExample()
    {
        window = GetWindow<ESTrackViewWindow>();
        window.titleContent = new GUIContent("轨道编辑器");
    }
    public static void InitNewSequence()
    {
        ShowExample();
        window = GetWindow<ESTrackViewWindow>();
        
    }
    
    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;
        // VisualElements objects can contain other VisualElement following a tree hierarchy.
        // Instantiate UXML
        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);

        BindElements();
        BindNormalHandles();
        BindButtonsHandles();
    }

    private void BindButtonsHandles()
    {
       CreatorToolBar.CreateButton.RegisterCallback<ClickEvent>(OnCreatorButtonClick);
       rightPanel.RegisterCallback<ClickEvent>(OnCreatorButtonClick);
       leftPanel.RegisterCallback<ClickEvent>(OnCreatorButtonClick);
    }

    private void BindElements()
    {
        ruler=rootVisualElement.Query<ESTrackRuler>();
        horSlider=rootVisualElement.Query<MinMaxSlider>();
        rightPanel=rootVisualElement.Query<VisualElement>("DownRightPart");
        leftPanel=rootVisualElement.Query("DownLeftPart");
        m_SelectionVisual=rootVisualElement.Query("SeletionContent");

        CreatorToolBar=rootVisualElement.Query<ESTrackCreatorToolbar>();
    }
    
    private void BindNormalHandles()
    {
       //MINMAX 的 显示范围选定
       horSlider.RegisterValueChangedCallback(HorSliderChange);
       
       //rightPanel 的快捷操作
          // 1. 鼠标滚轮事件 - 缩放
        rightPanel.RegisterCallback<WheelEvent>(OnRightPanelWheel, TrickleDown.TrickleDown);
        
        // 2. 鼠标中键事件 - 平移
        rightPanel.RegisterCallback<MouseDownEvent>(OnRightPanelMouseDown, TrickleDown.TrickleDown);
        rightPanel.RegisterCallback<MouseMoveEvent>(OnRightPanelMouseMove, TrickleDown.TrickleDown);
        rightPanel.RegisterCallback<MouseUpEvent>(OnRightPanelMouseUp, TrickleDown.TrickleDown);
        
        // 3. 鼠标离开事件
        rightPanel.RegisterCallback<MouseLeaveEvent>(OnMouseLeave, TrickleDown.TrickleDown);
        
        // 4. 右键上下文菜单
        rightPanel.RegisterCallback<ContextClickEvent>(OnContextClick, TrickleDown.TrickleDown);
   
    }
     
     #region  水平缩放偏移
       void HorSliderChange(ChangeEvent<Vector2> change)
        {
            HandleStartEndScale(change.newValue.x,change.newValue.y);
        }
        private void HandleStartEndScale(float start,float end)
    {
           startScale=Mathf.Clamp(start,0,0.9f);
            endScale=Mathf.Clamp(end,start,1f);
            showScale=1/Mathf.Clamp(Mathf.Abs(startScale-endScale),0.1f,10);

            pixelPerSecond=(standPixelPerSecond*showScale);
            Debug.Log("更新V2");
            UpdateNodesPos();
    }
    private void ApplyStartEndToUISlider(float start,float end)
    {
        horSlider.value=new Vector2(start,end);
    }
    #endregion
    
    #region 鼠标滚轮缩放
    private void OnRightPanelWheel(WheelEvent evt)
    {       HandleZoom(evt);
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
    
    private void HandleZoom(WheelEvent evt)
    {
        // 计算缩放中心（鼠标位置）
        Vector2 localMousePos = m_ContentContainer.WorldToLocal(evt.mousePosition);
        m_ZoomCenter = localMousePos;
        
        // 计算缩放因子
        float zoomDelta = evt.delta.y > 0 ? -m_ZoomSensitivity : m_ZoomSensitivity;
        float nowEdge=Mathf.Clamp(Mathf.Abs(startScale-endScale),0.1f,10);
        var tryStart= startScale+zoomDelta*nowEdge;
         var tryEnd= endScale-zoomDelta*nowEdge;
        HandleStartEndScale(tryStart,tryEnd);
        ApplyStartEndToUISlider(tryStart,tryEnd);
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
        
        Debug.Log("开始平移");
    }
    
    private void HandlePanning(MouseMoveEvent evt)
    {
        if (!m_IsPanning || m_CurrentMode != InteractionMode.Panning)
            return;
        
        // 计算移动距离
        Vector2 delta = evt.mousePosition - m_PanStartPosition;
        m_PanStartPosition = evt.mousePosition;
        
        // 应用平移
         float nowEdge=0.01f*Mathf.Clamp(Mathf.Abs(startScale-endScale),0.1f,10);

         var offset=-delta.x*nowEdge;
        if (offset > 0)
        {
            var maxOffset=Mathf.Min(offset,1-endScale);
            var tryStart=startScale+maxOffset;
             var tryEnd= endScale+maxOffset;
               HandleStartEndScale(tryStart,tryEnd);
        ApplyStartEndToUISlider(tryStart,tryEnd);
        }
        else
        {
             var maxOffset=Mathf.Max(offset,-startScale);
             var tryStart=startScale+maxOffset;
             var tryEnd= endScale+maxOffset;
               HandleStartEndScale(tryStart,tryEnd);
        ApplyStartEndToUISlider(tryStart,tryEnd);
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
        
        Debug.Log("结束平移");
    }
    #endregion
    
    #region  鼠标离开和右键空
    private void OnMouseLeave(MouseLeaveEvent evt)
    {
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
    
    private void OnContextClick(ContextClickEvent evt)
    {
        // 显示上下文菜单
        var menu = new GenericMenu();
        
        menu.AddItem(new GUIContent("重置视图"), false, () =>
        {
           // ResetView();
        });
        
        menu.AddItem(new GUIContent("适合所有内容"), false, () =>
        {
          //  FitToContent();
        });
        
        menu.AddSeparator("");
        
        menu.AddItem(new GUIContent("视图设置"), false, () =>
        {
           // ShowViewSettings();
        });
        
        menu.ShowAsContext();
    }
      #endregion
     #region 辅助方法
    
    private void UpdateCursor()
    {
        switch (m_CurrentMode)
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
        
        Debug.Log($"开始选择: {m_SelectionStart}");
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
        
        Debug.Log($"结束选择: {m_SelectionRect}");
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
    private void OnCreatorButtonClick(ClickEvent click)
    {
        HandleCreatorOpeartion();
    }

    private void HandleCreatorOpeartion()
    {
        var menu = new GenericMenu();
        
        menu.AddDisabledItem(new GUIContent("总操作"),false);

        menu.AddItem(new GUIContent("重置视图"), false, () =>
        {
           // ResetView();
        });
        
        menu.AddItem(new GUIContent("适合所有内容"), false, () =>
        {
          //  FitToContent();
        });
        
        menu.AddSeparator("");
        
        menu.AddItem(new GUIContent("视图设置"), false, () =>
        {
           // ShowViewSettings();
        });
        
        menu.ShowAsContext();
    }
     


     #endregion
    
    
    
    private void UpdateNodesPos()
    {
        
        ruler.TopRuler.MarkDirtyRepaint();
        var items=rootVisualElement.Query<ESEditorTrackItem>().ToList();
        foreach(var i in items)
        {
            i.UpdateNodesPos();
        }
    }
}

public class ESTrackViewWindowHelper : EditorInvoker_Level0
{
    public override void InitInvoke()
    {
        Selection.selectionChanged += ForTrackWindowSelection;
    }

    private static void ForTrackWindowSelection()
    {
        if (Selection.activeObject is EditorTrackSupport_GetSequence SupportSequence)
        {
            var se = SupportSequence.Sequence;
            if (se != null)
            {
                if (ESTrackViewWindow.Sequence != se)
                {
                    ESTrackViewWindow.Sequence = se;
                    ESTrackViewWindow.InitNewSequence();
                }
            }
        }
    }
}