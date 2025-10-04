using ES;
using Sirenix.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace ES
{
    /// <summary>
    /// DragAtSolver 专门为拖动资源并放置 提供了便捷的工具
    /// </summary>
    public class ESDragAtSolver
    {
        private float defaultHeight = 40;
        private bool isDraging = false;
        public static Color canReceiveColor = new Color(0, 0, 0, 0.25f);
        public static Color isReceivingColor = new Color(0, 0, 0, 0.8f);
        public Color normalColor = new Color(1, 1, 0, 0.25f);
        public virtual void SetHeight(int defaultHeight = 40)
        {
            this.defaultHeight = defaultHeight;
        }
        public virtual bool Update(out UnityEngine.Object[] users, Rect? defaultArea = null, Event ev = null)
        {
#if UNITY_EDITOR
            //刷新绘制区域
            EditorGUILayout.Space(0);
            //获取所在原始空间
            Rect orSpace = GUILayoutUtility.GetLastRect();

            if (defaultArea != null)
            {
                orSpace = defaultArea.Value;
            }

            Rect area = (defaultArea != null && defaultArea.Value.height > 2) ? defaultArea.Value : orSpace.SetYMax(orSpace.yMin + defaultHeight);
            users = DragAndDrop.objectReferences;

            ev ??= Event.current;
            if (users.Length > 0)
            {
                if (ev.type == EventType.DragExited || ev.type == EventType.MouseUp)
                {
                    isDraging = false;
                }
                if (!isDraging && ev.type == EventType.DragUpdated)
                {
                    isDraging = true;
                }
                if (isDraging)
                {
                    if (area.Contains(ev.mousePosition))
                    {
                        EditorGUI.DrawRect(area, isReceivingColor);
                    }
                    else
                    {
                        EditorGUI.DrawRect(area, canReceiveColor);
                    }
                }
                if (ev.type == EventType.DragUpdated || ev.type == EventType.DragPerform)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                    if (ev.type == EventType.DragPerform && area.Contains(ev.mousePosition))
                    {
                        DragAndDrop.AcceptDrag();
                        users = DragAndDrop.objectReferences;
                        return true;
                    }
                }
            }
            else
            {
                isDraging = false;
                EditorGUI.DrawRect(area, normalColor);
            }

#endif
            users = null;
            return false;

        }
    }
}
