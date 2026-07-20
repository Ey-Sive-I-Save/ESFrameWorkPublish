using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEditor;
#endif

namespace ES
{
    public class PagedListDrawSolver<T> : DrawIMGUISolver where T : IDrawIMGUI, new()
    {
        private const int MinItemsPerPage = 1;

        [NonSerialized] public List<T> Target;
        [NonSerialized] public int indexPage = 1;
        [NonSerialized] public int numPage = 1;
        [NonSerialized] public int numItemsPerPage = 5;
        [NonSerialized] public bool hasInit;

        public string title = "分页列表";
        public bool visible = true;
        public bool allowAdd = true;
        public bool allowRemove = true;
        public bool showElementIndex = true;

        public void Init(List<T> target, int numItemsPerPage = 5)
        {
            Target = target;
            this.numItemsPerPage = Mathf.Max(MinItemsPerPage, numItemsPerPage);
            indexPage = Mathf.Max(1, indexPage);
            hasInit = true;
            RefreshPageState();
        }

        public void ResetTarget(List<T> target, int numItemsPerPage = 5)
        {
            Target = target;
            this.numItemsPerPage = Mathf.Max(MinItemsPerPage, numItemsPerPage);
            indexPage = 1;
            hasInit = true;
            RefreshPageState();
        }

        public override void Draw()
        {
#if UNITY_EDITOR
            if (Target == null)
            {
                EditorGUILayout.HelpBox($"{title} 未绑定目标列表。", MessageType.Info);
                return;
            }

            RefreshPageState();
            DrawToolbar();

            visible = SirenixEditorGUI.Foldout(visible, title);
            if (!visible)
            {
                return;
            }

            if (Target.Count == 0)
            {
                EditorGUILayout.HelpBox("列表为空。", MessageType.None);
                return;
            }

            DrawCurrentPageItems();
            base.Draw();
#endif
        }

        private void RefreshPageState()
        {
            numItemsPerPage = Mathf.Max(MinItemsPerPage, numItemsPerPage);
            int count = Target == null ? 0 : Target.Count;
            numPage = Mathf.Max(1, Mathf.CeilToInt(count / (float)numItemsPerPage));
            indexPage = Mathf.Clamp(indexPage <= 0 ? 1 : indexPage, 1, numPage);
        }

#if UNITY_EDITOR
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{indexPage}/{numPage} 页，共 {Target.Count} 个元素");

            SirenixEditorGUI.BeginHorizontalToolbar();

            if (SirenixEditorGUI.ToolbarButton(SdfIconType.ArrowLeft))
            {
                indexPage--;
                RefreshPageState();
            }

            if (SirenixEditorGUI.ToolbarButton(SdfIconType.ArrowRight))
            {
                indexPage++;
                RefreshPageState();
            }

            GUILayout.Space(5);

            if (allowAdd && SirenixEditorGUI.ToolbarButton(SdfIconType.Plus))
            {
                Target.Add(new T());
                RefreshPageState();
                indexPage = numPage;
            }

            SirenixEditorGUI.EndHorizontalToolbar();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCurrentPageItems()
        {
            int start = Mathf.Max(0, (indexPage - 1) * numItemsPerPage);
            int end = Mathf.Min(Target.Count, start + numItemsPerPage);

            for (int i = start; i < end; i++)
            {
                T item = Target[i];
                SirenixEditorGUI.BeginBox();

                EditorGUILayout.BeginHorizontal();
                if (showElementIndex)
                {
                    EditorGUILayout.LabelField($"元素 {i}", GUILayout.Width(80));
                }

                GUILayout.FlexibleSpace();
                bool remove = allowRemove && SirenixEditorGUI.ToolbarButton(SdfIconType.Trash);
                EditorGUILayout.EndHorizontal();

                if (item == null)
                {
                    EditorGUILayout.HelpBox("元素为空。", MessageType.Warning);
                }
                else
                {
                    item.Editor_DrawIMGUI();
                }

                SirenixEditorGUI.EndBox();

                if (remove)
                {
                    Target.RemoveAt(i);
                    RefreshPageState();
                    break;
                }
            }
        }
#endif
    }
}
