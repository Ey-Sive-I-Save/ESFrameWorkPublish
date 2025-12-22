using System;
using System.Collections.Generic;
using DG.Tweening;
using ES;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class ESTrackViewWindow : EditorWindow
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
    public List<ESEditorTrackItem> Items=new List<ESEditorTrackItem>();


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
    }


    private void BindElements()
    {
        ruler=rootVisualElement.Query<ESTrackRuler>();
        horSlider=rootVisualElement.Query<MinMaxSlider>();
    }
    
    private void BindNormalHandles()
    {
       horSlider.RegisterValueChangedCallback(Change);
     
    }
       void Change(ChangeEvent<Vector2> change)
        {
            startScale=change.newValue.x;
            endScale=change.newValue.y;
            showScale=1/Mathf.Clamp(Mathf.Abs(startScale-endScale),0.1f,10);

            pixelPerSecond=(standPixelPerSecond*showScale);
            Debug.Log("更新V2");
            UpdateNodesPos();
        }

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