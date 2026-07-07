using ES;
using NUnit.Framework;
using Sirenix.Reflection.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public enum ESEditorGlobalENV
{
    [InspectorName("场景")]Scene,
    [InspectorName("项目(资产)")]Project,
    [InspectorName("层级")]Hierarchy,
    [InspectorName("检查器")] Inspector,
    [InspectorName("控制台")] Console,
    [InspectorName("游戏")] Game,

}
public enum ESEditorAction
{
    None,
    DragRectSelect,
}
public class ESEditorGlobal : EditorInvoker_Level0
{
    public static ESEditorGlobalENV windowENV;
    public static ESEditorAction action;
    public static Vector2 RunningMousePos;
    public static Vector2 actionPos1;
    public static Vector2 actionPos2;
    public static Vector2 ActionMousePos { get { 
            if (_actionMousePos == default) { 
                _actionMousePos = new Vector2(1000, 500); } 
            return _actionMousePos; }
        set {_actionMousePos = value; } }
    private static Vector2 _actionMousePos = default;
    public override void InitInvoke()
    {
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyGUI;
        
        EditorApplication.projectWindowItemOnGUI += ProjectGUI;
        
        
    }
    private static HashSet<int> Contains = new HashSet<int>();
    public static Rect SelectHiRect;
    Dictionary<Transform, float> YS = new Dictionary<Transform, float>();
    private void HierarchyGUI(int inID, Rect rect)
    {
        GameObject go = Resources.InstanceIDToObject(inID) as GameObject;
        if (go != null)
        {
            if(go.transform.root == go.transform)
            {

            }
            else if(YS.TryGetValue(go.transform.parent,out var py))
            {
                Rect hor = rect;
                hor.width = 22;
                hor.y += hor.height / 2;
                hor.height = 2;
                hor.x -= 22;

                Rect ver = hor;
                ver.width = 2;
                ver.height =(rect.y - py);
                ver.y -=(rect.y - py);
                EditorGUI.DrawRect(hor, Color.white._WithAlpha(0.5f));
                EditorGUI.DrawRect(ver, Color.white._WithAlpha(0.5f));
            }
            YS[go.transform] = rect.y;
        }
        
        Event etc = Event.current;
        if (rect.Contains(Event.current.mousePosition)&& Event.current.type == EventType.MouseDown)
        {
            ActionMousePos = EditorGUIUtility.GUIToScreenPoint(rect.position);
        }

        
        if (ESEditorGlobal.action == ESEditorAction.DragRectSelect)
        {

            EditorGUI.DrawRect(SelectHiRect, Color.blue._WithAlpha(0.1f));
            if (SelectHiRect.Overlaps(rect))
            {
                Debug.Log("ADD"+rect+"  "+ SelectHiRect);
                Contains.Add(inID);
            }
            else
            {
                Debug.Log("Remove" + rect+"  " + SelectHiRect);
                Contains.Remove(inID);
            }
            if (etc.type == EventType.MouseUp && etc.button == 2)
            {
               
                ESEditorGlobal.action = ESEditorAction.None;
                var ids = Contains;
                var gs = new List<UnityEngine.Object>(10);
                foreach(var i in ids)
                {
                    UnityEngine.Object foundObj = Resources.InstanceIDToObject(i);
                    if (foundObj != null)
                    {
                        gs.Add(foundObj);
                    }
                }
                Selection.objects = gs.ToArray();
                etc.Use();
            }
            else if (etc.type == EventType.MouseDrag && etc.button == 2)
            {
                actionPos2 = Event.current.mousePosition;
                float xMin = Mathf.Min(actionPos1.x,actionPos2.x);
                float yMin = Mathf.Min(actionPos1.y, actionPos2.y);
                float w = Mathf.Abs(actionPos1.x-actionPos2.x);
                float h = Mathf.Abs(actionPos1.y - actionPos2.y);
                SelectHiRect = new Rect(xMin,yMin, w,h);
               
                //Debug.Log("Update"+actionPos2);
                //EditorGUI.DrawRect(new Rect(actionPos1, actionPos2 - actionPos1), Color.blue._WithAlpha(0.5f));


            }
        }
        if (ESEditorGlobal.action == ESEditorAction.None)
        {
            if (etc.type == EventType.MouseDown && etc.button == 2)
            {
                actionPos1=actionPos2 = Event.current.mousePosition;
                ESEditorGlobal.action = ESEditorAction.DragRectSelect;
                Contains.Clear();
                etc.Use();
            }
        }
    }
    private void ProjectGUI(string guid, Rect rect)
    {
        if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
        {
            ActionMousePos = EditorGUIUtility.GUIToScreenPoint(rect.position);
        }
    }
}
