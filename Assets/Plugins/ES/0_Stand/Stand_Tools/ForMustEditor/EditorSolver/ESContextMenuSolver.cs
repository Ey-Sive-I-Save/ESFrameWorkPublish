using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    public class ESContextMenuSolver : ESEditorSolver
    {
#if UNITY_EDITOR
        private readonly GenericMenu menu = new GenericMenu();
#endif

        public ESContextMenuSolver InitSolver()
        {
            return CompleteInitSolver<ESContextMenuSolver>();
        }

        public ESContextMenuSolver Add(string path, Action onClick, bool enabled = true, bool selected = false)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(path)) return this;
            if (enabled)
                menu.AddItem(new GUIContent(path), selected, () => onClick?.Invoke());
            else
                menu.AddDisabledItem(new GUIContent(path), selected);
#endif
            return this;
        }

        public ESContextMenuSolver Separator(string path = "")
        {
#if UNITY_EDITOR
            menu.AddSeparator(path);
#endif
            return this;
        }

        public void Show()
        {
#if UNITY_EDITOR
            menu.ShowAsContext();
#endif
        }

        public void ShowAt(Rect area)
        {
#if UNITY_EDITOR
            menu.DropDown(area);
#endif
        }

        public static bool HandleContextClick(Rect area, Action<ESContextMenuSolver> buildMenu)
        {
#if UNITY_EDITOR
            var ev = Event.current;
            if (ev == null || ev.type != EventType.ContextClick || !area.Contains(ev.mousePosition))
                return false;

            var solver = new ESContextMenuSolver();
            buildMenu?.Invoke(solver);
            solver.Show();
            ev.Use();
            return true;
#else
            return false;
#endif
        }
    }
}
