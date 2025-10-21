using ES;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class ESTrackViewWindow : EditorWindow
{
    public static ESTrackViewWindow window;
    public static INodeContainer SContainer;

    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    [MenuItem("Window/UI Toolkit/ESTrackViewWindow")]
    public static void ShowExample()
    {
        window = GetWindow<ESTrackViewWindow>();
        window.titleContent = new GUIContent("轨道编辑器");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // VisualElements objects can contain other VisualElement following a tree hierarchy.
        

        // Instantiate UXML
        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);
    }
}
