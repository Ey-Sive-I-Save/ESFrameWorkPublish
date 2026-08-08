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

        public static TWindow OpenFor(
            UnityEngine.Object target,
            string title,
            string page,
            Action onClose,
            UnityEngine.Object undoTarget = null,
            Action onChanged = null)
        {
            // 每次“弹出”都创建一个明确的浮动实例。GetWindow<T>() 会复用已有停靠页签，
            // 再调用 ShowUtility() 在 Unity/Odin 不同版本上可能无法可靠脱离 DockArea。
            // 先关闭旧实例，保持同一类检查器只有一个，避免多个窗口同时写同一份 drawerData。
            if (UsingWindow != null)
            {
                TWindow previous = UsingWindow;
                UsingWindow = null;
                previous.Close();
            }

            TWindow window = CreateInstance<TWindow>();
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
            window.m_UndoTarget = undoTarget;
            window.m_OnChanged = onChanged;
            // 独立编辑器必须是真正的浮动窗口，并立即取得焦点；仅调用 Show() 时
            // Unity 可能把它复用成停靠页签，用户会误以为“弹出编辑器”失效。
            window.ShowUtility();
            window.Focus();
            window.ForceMenuTreeRebuild();
            window.Repaint();
            return window;
        }

        [NonSerialized] private UnityEngine.Object m_UndoTarget;
        [NonSerialized] private Action m_OnChanged;

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent(windowTitle, "ES 轨道编辑器临时检查器");
        }

        protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
            base.ES_OnBuildMenuTree(tree);
            RegisterAndAddPage(tree, pageName, new Page_ESTrackTemporaryInspector(inspectedObject, m_UndoTarget, m_OnChanged), SdfIconType.Pencil);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            InvokeCloseActionOnce();
            if (ReferenceEquals(UsingWindow, this))
                UsingWindow = null;
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
            [NonSerialized] private readonly UnityEngine.Object undoTarget;
            [NonSerialized] private readonly Action onChanged;
            [NonSerialized] private OdinEditor editor;

            public Page_ESTrackTemporaryInspector(UnityEngine.Object target, UnityEngine.Object undoTarget, Action onChanged)
            {
                this.target = target;
                this.undoTarget = undoTarget;
                this.onChanged = onChanged;
            }

            [Sirenix.OdinInspector.OnInspectorGUI]
            private void DrawInspector()
            {
                if (target == null)
                {
                    EditorGUILayout.HelpBox("没有可编辑对象。", MessageType.Warning);
                    return;
                }

                DrawESInspectorSummary(target);

                editor ??= OdinEditor.CreateEditor(target, typeof(OdinEditor)) as OdinEditor;
                if (editor != null)
                {
                    UnityEngine.Object editTarget = undoTarget != null ? undoTarget : target;
                    RecordUndoBeforeInspectorInput(editTarget, "编辑时间轴属性");
                    EditorGUI.BeginChangeCheck();
                    editor.DrawDefaultInspector();
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (editTarget != null)
                            EditorUtility.SetDirty(editTarget);
                        onChanged?.Invoke();
                    }
                }
            }

            private static void RecordUndoBeforeInspectorInput(UnityEngine.Object target, string label)
            {
                if (target == null || Event.current == null)
                    return;

                EventType type = Event.current.type;
                if (type == EventType.MouseDown
                    || type == EventType.DragPerform
                    || type == EventType.ExecuteCommand)
                {
                    Undo.RecordObject(target, label);
                }
                else if (type == EventType.KeyDown && !EditorGUIUtility.editingTextField)
                {
                    // 文本输入由 Odin/SerializedObject 合并；逐字符记录会让撤销栈膨胀，
                    // 也会让一次业务编辑无法通过一次 Ctrl+Z 回滚。
                    Undo.RecordObject(target, label);
                }
            }

            private static void DrawESInspectorSummary(UnityEngine.Object target)
            {
                VisualGUIDrawerSO drawer = target as VisualGUIDrawerSO;
                object data = drawer != null ? drawer.drawerData : null;
                string typeName = data != null ? data.GetType()._GetTypeDisplayName() : "未绑定业务对象";
                string displayName = data is ITrackItem trackItem
                    ? trackItem.DisplayName
                    : data is ITrackClip trackClip
                        ? trackClip.DisplayName
                        : typeName;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("ES 属性检查器", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("目标", string.IsNullOrEmpty(displayName) ? "未命名" : displayName);
                EditorGUILayout.LabelField("类型", typeName);
                EditorGUILayout.LabelField("状态", "编辑中 · 修改将同步回当前时间轴");
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
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
