using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ESMENU : PopupWindowContent
{
    float f;
    bool toggle1 = true;
    bool toggle2 = true;
    bool toggle3 = true;
    OdinMenuTree tree;
    string usingItem;
    
    public override void OnGUI(Rect rect)
    {
        Debug.Log("SS" + this.editorWindow);
        if (tree == null)
        {
            tree = new OdinMenuTree();

            tree.Add("一级菜单", new GUIContent("AHAHAHAH"));
            var style = OdinMenuStyle.TreeViewStyle;

            tree.Config.DefaultMenuStyle = style;
            OdinMenuItem item = tree.GetMenuItem("一级菜单");

            tree.Add("一级菜单2", new GUIContent("AHAHAHAH"));
            // var style = OdinMenuStyle.TreeViewStyle;

            // tree.Config.DefaultMenuStyle = style;
            OdinMenuItem item2 = tree.GetMenuItem("一级菜单2");

            item.OnRightClick += (f) => { var menu = new ESMENU(); PopupWindow.Show(new Rect(Event.current.mousePosition, new Vector2(100, 100)), menu); };
            tree.Add("一级菜单/二级菜单", null);
        }

        tree.DrawSearchToolbar();
        tree.DrawMenuTree();

    }
}
public class ESMenuPro : OdinEditorWindow
{

    //public override bool DrawUnityEditorPreview { get => false; set { } }
    [UnityEditor.MenuItem("Tools/ESMenuPro")]
    public static void Open()
    {

        var w = GetWindow<ESMenuPro>();
        // GenericMenu


    }
    public override bool DrawUnityEditorPreview { get => false; set { } }
    OdinMenuTree tree;
    protected override void DrawEditors()
    {
        if (tree == null)
        {
            tree = new OdinMenuTree();

            tree.Add("SSS", new GUIContent("AHAHAHAH"));
            var style = OdinMenuStyle.TreeViewStyle;

            tree.Config.DefaultMenuStyle = style;
            OdinMenuItem item = tree.GetMenuItem("SSS");

            item.OnRightClick += (f) => { var menu = new ESMENU(); PopupWindow.Show(new Rect(Event.current.mousePosition, new Vector2(100, 100)), menu); };
            tree.Add("SSS/HAHAHA", null);
        }
        SirenixEditorGUI.BeginToolbarBox(GUILayout.Height(75));

        SirenixEditorGUI.BeginIndentedHorizontal();
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");

        };
        SirenixEditorGUI.EndIndentedHorizontal();
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");

        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");

        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        if (SirenixEditorGUI.ToolbarButton(EditorIcons.AlertTriangle, false))
        {
            Debug.Log("SS");
        };
        SirenixEditorGUI.EndToolbarBox();
        if (Event.current.type== EventType.KeyDown)
        {
            if(Event.current.keyCode== KeyCode.Escape)
            {
                Close();
               
            }
        }
        tree.DrawMenuTree();

        //EditorGUI.DrawRect(new Rect(Event.current.mousePosition, new Vector2(100, 100)), Color.yellow);
        // base.DrawEditors();
    }
}
