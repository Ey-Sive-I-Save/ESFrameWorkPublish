using ES;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class ESGraphViewWindow : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    public ESGraphView_Part_FlowChart Part_FlowChart;
    public ESGraphView_Part_InspectorView Part_Inspector;


    public INodeContainer Container { get { return m_NodeContainer; } set { if (value != m_NodeContainer) {  m_NodeContainer = value; OnTargetContainerChanged(); } } }
    private INodeContainer m_NodeContainer;
    [MenuItem("Tools/ES工具/图编辑器")]
    public static ESGraphViewWindow ShowWindow()
    {
        ESGraphViewWindow wnd = GetWindow<ESGraphViewWindow>();
        wnd.titleContent = new GUIContent("ESGraphViewWindow");
        return wnd;
    }
    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;
        // VisualElements objects can contain other VisualElement following a tree hierarchy.
        
        // Instantiate UXML
        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);
        #region 查询绑定

       
        Part_FlowChart = root.Q<ESGraphView_Part_FlowChart>();
        Part_Inspector = root.Q<ESGraphView_Part_InspectorView>();

        #endregion

        #region 委托注册
        Part_FlowChart.OnSelectNodeViewer = OnNodeSelectionChanged;
       /* Part_FlowChart.userSeletionGo = userSeletionGo;*/
        Part_FlowChart.window = this;
        Selection.selectionChanged += () => {
        
            if(Selection.activeObject is INodeContainer container)
            {
                Container = container;
            }
        };
        if (Selection.activeObject is INodeContainer container)
        {
            Container = container;
        }
        #endregion

        #region 初始化
        Part_FlowChart.ResetNodeViewers();
        #endregion
    }

    void OnNodeSelectionChanged(BaseNodeViewer nodeView)
    {
        //进行检查器更新
        Part_Inspector.UpdateSelectionNode(nodeView);
    }
    void OnTargetContainerChanged()
    {
        if (m_NodeContainer != null)
        {
            Part_Inspector.SetContainerGlobal(m_NodeContainer);
        }
    }

    public class ERS : EditorInvoker_Level50
    {
        public override void InitInvoke()
        {
            Selection.selectionChanged += () => {

                if (Selection.activeObject is INodeContainer container)
                {
                    var w= ESGraphViewWindow.ShowWindow();
                    w.Container = container;
                }
            };
        }
    }

}
