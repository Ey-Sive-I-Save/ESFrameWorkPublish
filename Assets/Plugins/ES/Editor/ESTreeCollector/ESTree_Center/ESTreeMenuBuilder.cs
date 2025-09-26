using ES;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
public enum ESTreeCollectorName
{
    [InspectorName("快捷创建")] ESCreate,

}
public class ESTreeMenuBuilder : EditorInvoker_Level1
{
    //每次初始化进行装载
    public static Dictionary<ESTreeCollectorName, ESTreeCollector> Collectors = new Dictionary<ESTreeCollectorName, ESTreeCollector>();
    public override void InitInvoke()
    {
        //开始初始化
    }
    [UnityEditor.MenuItem("Tools/A #Z")]
    public static void TEST()
    {
        ShowTree(ESTreeCollectorName.ESCreate);
    }

    public static void ShowTree(ESTreeCollectorName name)
    {
        ESTreeMenuShower.OpenWith(name);
    }
}

public class ESTreeMenuShower : OdinEditorWindow
{
    public static OdinMenuTree menuTree;
    public static ESTreeCollectorName Last;
    public static ESTreeCollector collector;
    private static bool init = false;
    public static ESTreeMenuShower UsingWindow;
    public static ESTreeMenuShower OpenWith(ESTreeCollectorName tree)
    {

        if (UsingWindow != null)
        {
            if (tree != Last)
            {
                menuTree = null;
                Last = tree;
                UsingWindow.Build(tree);
            }
        }
        if (UsingWindow == null)
        {
            UsingWindow = GetWindow<ESTreeMenuShower>();
            Last = tree;
            UsingWindow.Build(tree);
        }
        if (UsingWindow != null)
        {
            UsingWindow.minSize = new Vector2() { x = 200, y = 400 };
            UsingWindow.maxSize = new Vector2() { x = 200, y = 500 };
            UsingWindow.ShowPopup();
            UsingWindow.position = new Rect(ESEditorGlobal.ActionMousePos + Vector2.up * 50, UsingWindow.minSize);
        }
        return UsingWindow;
    }
    public void Build(ESTreeCollectorName tree)
    {
        // DateTime date = DateTime.Now;
        collector = ESTreeMenuBuilder.Collectors[Last];
        if (collector == null) { Debug.LogWarning("未提供合适的类型" + Last); Close(); return; }
        menuTree = new OdinMenuTree();

        /* Texture2D blackTexture = new Texture2D(1, 1);
         blackTexture.SetPixel(0, 0, Color.black);
         blackTexture.Apply();
         menuTree.DefaultMenuStyle.DefaultLabelStyle.normal.background = blackTexture;*/

        init = true;

        var lister = collector.Items.OrderBy((item) => item.Order + (item.ParentName == null ? -100 : 100));

        foreach (var i in lister)
        {
            var iname = i.GetName();
            menuTree.Add(iname, i.Select());
            var item = menuTree.GetMenuItem(iname);
            //Debug.Log("SSSS");
            item.OnDrawItem += (OdinMenuItem it) =>
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    if (it.Rect.Contains(Event.current.mousePosition) && Event.current.button == 0)
                    {
                        i.Click();

                    }
                }
            };
        }

        //Debug.Log("构建树" + (DateTime.Now - date));
    }
    private const int _MAX_Tools_Num = 4; 
    protected override void DrawEditors()
    {
        if (menuTree == null || !init)
        {
            Build(Last);
        }
        if (menuTree == null) return;
        //EditorGUI.DrawRect(this.position,Color.black);
        //标题
        string titleName = Test1GlobalDara.Instance.Title;

        SirenixEditorGUI.Title(titleName+ Last._GetInspectorName(), "", TextAlignment.Center, true);
        //工具栏
        #region 工具栏
        GUILayout.Box("", GUILayout.Height(8), GUILayout.ExpandWidth(true));
        int remain = _MAX_Tools_Num;
        SirenixEditorGUI.BeginHorizontalToolbar();
        foreach (var i in collector.Tools)
        {
            i.Draw(collector);
            GUILayout.Space(5);
            remain--;
            if (remain <= 0 || i.NextLine)
            {
                SirenixEditorGUI.EndHorizontalToolbar();
                GUILayout.Space(5);
                SirenixEditorGUI.BeginHorizontalToolbar();
                remain = _MAX_Tools_Num;
            }
        }
        SirenixEditorGUI.EndHorizontalToolbar();
        GUILayout.Box("", GUILayout.Height(8), GUILayout.ExpandWidth(true));
        #endregion

        //窗口

        menuTree.DrawMenuTree();

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            Close();
        }

    }

}
