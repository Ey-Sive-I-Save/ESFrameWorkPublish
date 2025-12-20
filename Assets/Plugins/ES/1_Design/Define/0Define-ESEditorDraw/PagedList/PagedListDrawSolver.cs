using ES;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEditor;
using UnityEngine;


namespace ES
{
    public class PagedListDrawSolver<T> : DrawIMGUISolver where T : IDrawIMGUI, new()
    {
        [NonSerialized]
        public List<T> Target;

        [NonSerialized]
        public int indexPage;

        [NonSerialized]
        public int numPage;

        [NonSerialized]
        public int numItemsPerPage;

        [NonSerialized]
        public bool hasInit = false;

        private bool visible = false;
        public void Init(List<T> target, int numItemsPerPage = 5)
        {
            if (hasInit) return;
            this.Target = target;
            this.numItemsPerPage = numItemsPerPage;
            hasInit = true;
        }

        public override void Draw()
        {
            #if UNITY_EDITOR
            if (Target == null) 

            return;

            if (Target.Count == 0)
            {
                numPage = 1;
                indexPage = 1;
            }
            else
            {
                numPage = Mathf.CeilToInt(((float)Target.Count / numItemsPerPage));
                indexPage = Mathf.Clamp(indexPage, 1, numPage);
            }

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"{indexPage}/{numPage}页，共{Target.Count}元素");

            SirenixEditorGUI.BeginHorizontalToolbar();
            //左
            if (SirenixEditorGUI.ToolbarButton(Sirenix.OdinInspector.SdfIconType.Arrow90degLeft))
            {
                indexPage--;
            }
            GUILayout.Space(5);
            //右
            if (SirenixEditorGUI.ToolbarButton(Sirenix.OdinInspector.SdfIconType.Arrow90degRight))
            {
                indexPage++;
            }
            GUILayout.Space(5);
            //加
            if (SirenixEditorGUI.ToolbarButton(Sirenix.OdinInspector.SdfIconType.Plus))
            {
                Target.Add(new T());
            }
            indexPage = Mathf.Clamp(indexPage, 1, numPage);
            SirenixEditorGUI.EndHorizontalToolbar();
            EditorGUILayout.EndHorizontal();
            visible = SirenixEditorGUI.Foldout(visible, "折叠列表页");
            if (visible && Target.Count > 0)
            {
                int start = Mathf.Max(0, (indexPage - 1) * numItemsPerPage);
                for (int i = start; i < Target.Count && i < (start + numItemsPerPage); i++)
                {
                    var item = Target[i];
                    if (item != null)
                    {
                        SirenixEditorGUI.BeginBox();
                        item.Editor_DrawIMGUI();
                        SirenixEditorGUI.EndBox();
                    }
                }

            }
            base.Draw();
            #endif
        }
                    
    }
}
