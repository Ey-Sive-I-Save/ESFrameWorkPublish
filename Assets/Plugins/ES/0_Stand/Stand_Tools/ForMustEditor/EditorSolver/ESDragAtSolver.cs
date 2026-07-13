using ES;
using Sirenix.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace ES
{
    /// <summary>
    /// DragAtSolver 专门为拖动资源并放置 提供了便捷的工具
    /// </summary>
    public class ESDragAtSolver : ESEditorSolver
    {
        private float fallbackHeight = 40;
        private bool isDraging = false;
        public Color canReceiveColor = new Color(0.12f, 0.36f, 0.68f, 0.55f);
        public Color isReceivingColor = new Color(0.03f, 0.42f, 0.18f, 0.82f);
        public Color normalColor = new Color(1f, 0.82f, 0.12f, 0.18f);

        public ESDragAtSolver InitSolver(
            Color? normalColor = null,
            Color? canReceiveColor = null,
            Color? isReceivingColor = null)
        {
            if (normalColor.HasValue) this.normalColor = normalColor.Value;
            if (canReceiveColor.HasValue) this.canReceiveColor = canReceiveColor.Value;
            if (isReceivingColor.HasValue) this.isReceivingColor = isReceivingColor.Value;
            return CompleteInitSolver<ESDragAtSolver>();
        }

        [Obsolete("Height is frame context. Prefer Update(out users, area, ev, fallbackHeight) or pass ESAreaSolver.GetAreaRect().")]
        public virtual void SetHeight(int defaultHeight = 40)
        {
            this.fallbackHeight = defaultHeight;
        }
        public virtual bool Update(out UnityEngine.Object[] users, Rect? defaultArea = null, Event ev = null, float? fallbackHeight = null)
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

            Rect area = (defaultArea != null && defaultArea.Value.height > 2) ? defaultArea.Value : orSpace.SetYMax(orSpace.yMin + (fallbackHeight ?? this.fallbackHeight));
            users = DragAndDrop.objectReferences;

            ev ??= Event.current;
            if (ev == null)
                return false;

            if (users != null && users.Length > 0)
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
                        ev.Use();
                        return true;
                    }

                    if (area.Contains(ev.mousePosition))
                    {
                        ev.Use();
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
