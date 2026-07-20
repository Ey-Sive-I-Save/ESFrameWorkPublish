using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public abstract class ESTrackTemporaryInspectorWindow<TWindow> : ESMenuTreeWindowAB<TWindow>
        where TWindow : ESTrackTemporaryInspectorWindow<TWindow>
    {
        private UnityEngine.Object inspectedObject;
        private string windowTitle = "临时编辑";
        private string pageName = "编辑";
        private Action closeAction;
        private bool closeActionInvoked;

        public static TWindow OpenFor(UnityEngine.Object target, string title, string page, Action onClose)
        {
            TWindow window = GetWindow<TWindow>();
            UsingWindow = window;
            window.inspectedObject = target;
            window.windowTitle = string.IsNullOrEmpty(title) ? "临时编辑" : title;
            window.pageName = string.IsNullOrEmpty(page) ? "编辑" : page;
            window.closeAction = onClose;
            window.closeActionInvoked = false;
            window.titleContent = window.ESWindow_GetWindowGUIContent();
            window.minSize = new Vector2(420f, 520f);
            window.maxSize = new Vector2(1200f, 1600f);
            window.MenuWidth = 120f;
            window.Show();
            window.ForceMenuTreeRebuild();
            return window;
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent(windowTitle, "ES 轨道编辑器临时检查器");
        }

        protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
            base.ES_OnBuildMenuTree(tree);
            RegisterAndAddPage(tree, pageName, new Page_ESTrackTemporaryInspector(inspectedObject), SdfIconType.Pencil);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            InvokeCloseActionOnce();
        }

        private void InvokeCloseActionOnce()
        {
            if (closeActionInvoked)
                return;

            closeActionInvoked = true;
            Action action = closeAction;
            closeAction = null;
            action?.Invoke();
        }

        [Serializable]
        private sealed class Page_ESTrackTemporaryInspector : ESWindowPageBase
        {
            [NonSerialized] private readonly UnityEngine.Object target;
            [NonSerialized] private OdinEditor editor;

            public Page_ESTrackTemporaryInspector(UnityEngine.Object target)
            {
                this.target = target;
            }

            [Sirenix.OdinInspector.OnInspectorGUI]
            private void DrawInspector()
            {
                if (target == null)
                {
                    EditorGUILayout.HelpBox("没有可编辑对象。", MessageType.Warning);
                    return;
                }

                editor ??= OdinEditor.CreateEditor(target, typeof(OdinEditor)) as OdinEditor;
                if (editor != null)
                    editor.DrawDefaultInspector();
            }

            public override void OnPageDisable()
            {
                if (editor != null)
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                    editor = null;
                }
            }
        }
    }

    public sealed class ESTrackItemTemporaryInspectorWindow : ESTrackTemporaryInspectorWindow<ESTrackItemTemporaryInspectorWindow>
    {
    }

    public sealed class ESTrackClipTemporaryInspectorWindow : ESTrackTemporaryInspectorWindow<ESTrackClipTemporaryInspectorWindow>
    {
    }

    public sealed class ESTrackSkillDataTemporaryInspectorWindow : ESTrackTemporaryInspectorWindow<ESTrackSkillDataTemporaryInspectorWindow>
    {
    }
}
