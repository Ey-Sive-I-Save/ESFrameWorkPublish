using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public class EditorInitAndUpdater : EditorInvoker_Level0
{
    public static Event etc;


    public override void InitInvoke()
    {
        Debug.Log("Editor SetHeight");
        EditorApplication.update += Update;
       
    }

    private void Update()
    {
        etc = Event.current;
        WindowFocus();
        if (etc != null) DragRectSelect();
         
    }

    private void DragRectSelect()
    {
       
    }

    private static void WindowFocus()
    {
        var windowF = EditorWindow.focusedWindow;
        if (windowF == null) return;
        string window = windowF.titleContent.text;
        switch (window)
        {
            case "项目" or "Project":ESEditorGlobal.windowENV = ESEditorGlobalENV.Project; break;
            case "层级" or "Hierarchy": ESEditorGlobal.windowENV = ESEditorGlobalENV.Hierarchy; break;
            case "检查器" or "Inspector": ESEditorGlobal.windowENV = ESEditorGlobalENV.Inspector; break;
            case "场景" or "Scene": ESEditorGlobal.windowENV = ESEditorGlobalENV.Scene; break;
            case "游戏" or "Game": ESEditorGlobal.windowENV = ESEditorGlobalENV.Game; break;
            case "控制台" or "Console": ESEditorGlobal.windowENV = ESEditorGlobalENV.Console; break;

        }
    }
}
public class EditorInit1 : EditorInvoker_Level1
{
    public override void InitInvoke()
    {
        Debug.Log("Editor SetHeight"+1);
        EditorApplication.update += Update;
    }

    private void Update()
    {
    }

}
public class EditorInit2 : EditorInvoker_Level2
{
    public override void InitInvoke()
    {
        Debug.Log("Editor SetHeight"+2 );
        EditorApplication.update += Update;
    }

    private void Update()
    {
    }

}