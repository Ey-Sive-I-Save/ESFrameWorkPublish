using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ES.EditorInternal;

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
        [NonSerialized] private Page_ESTrackTemporaryInspector inspectorPage;
        [NonSerialized] private VisualElement shellRoot;

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
            // 独立 Inspector 不需要左侧菜单树；保留菜单占位会挤压字段并诱发横向滚动。
            window.MenuWidth = 0f;
            window.m_UndoTarget = undoTarget;
            window.m_OnChanged = onChanged;
            // 独立编辑器必须是真正的浮动窗口，并立即取得焦点；仅调用 Show() 时
            // Unity 可能把它复用成停靠页签，用户会误以为“弹出编辑器”失效。
            window.ShowUtility();
            window.Focus();
            window.ForceMenuTreeRebuild();
            window.BuildGraphStyleShell();
            window.Repaint();
            return window;
        }

        [NonSerialized] private UnityEngine.Object m_UndoTarget;
        [NonSerialized] private Action m_OnChanged;

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent(windowTitle, "ES 轨道编辑器临时检查器");
        }

        // 滚动由 UI Toolkit ScrollView 独立管理，避免 Odin 外层滚动与业务字段滚动叠加。
        public override bool UseScrollView => false;

        protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
            base.ES_OnBuildMenuTree(tree);
            inspectorPage = new Page_ESTrackTemporaryInspector(inspectedObject, m_UndoTarget, m_OnChanged);
            RegisterAndAddPage(tree, pageName, inspectorPage, SdfIconType.Pencil);
        }

        protected override void DrawEditors()
        {
            // Odin 只作为下面 IMGUIContainer 的字段引擎；禁止它再次绘制整页 Inspector。
        }

        private void CreateGUI()
        {
            BuildGraphStyleShell();
        }

        private void BuildGraphStyleShell()
        {
            if (rootVisualElement == null)
                return;
            if (inspectorPage == null)
            {
                rootVisualElement.schedule.Execute(BuildGraphStyleShell);
                return;
            }

            if (shellRoot != null)
                shellRoot.RemoveFromHierarchy();

            shellRoot = new VisualElement { name = "ESTrackInspectorShell" };
            shellRoot.style.flexGrow = 1f;
            shellRoot.style.flexDirection = FlexDirection.Column;
            shellRoot.style.backgroundColor = ESEditorPresentation.GetDepthBackground(3);
            shellRoot.style.borderLeftWidth = 1f;
            shellRoot.style.borderLeftColor = ESEditorPresentation.DividerColor;

            VisualElement header = new VisualElement { name = "ESTrackInspectorShellHeader" };
            header.style.paddingLeft = 13f;
            header.style.paddingRight = 12f;
            header.style.paddingTop = 9f;
            header.style.paddingBottom = 9f;
            header.style.minHeight = 67f;
            header.style.backgroundColor = ESEditorPresentation.GetDepthBackground(1);
            header.style.borderLeftWidth = 4f;
            header.style.borderLeftColor = ESEditorPresentation.GetDepthAccent(0);
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = ESEditorPresentation.DividerColor;
            var context = new Label(pageName);
            context.style.fontSize = 9f;
            context.style.unityFontStyleAndWeight = FontStyle.Bold;
            context.style.color = ESEditorPresentation.SectionMutedTextColor;
            var title = new Label(windowTitle);
            title.style.fontSize = 14f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = ESEditorPresentation.SectionSelectedTextColor;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            var subtitle = new Label("Odin 业务字段桥接 · Graph 风格编辑器");
            subtitle.style.fontSize = 10f;
            subtitle.style.color = ESEditorPresentation.EmptyTextColor;
            header.Add(context);
            header.Add(title);
            header.Add(subtitle);
            shellRoot.Add(header);

            var details = new ScrollView(ScrollViewMode.Vertical);
            details.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            details.verticalScrollerVisibility = ScrollerVisibility.Auto;
            details.style.flexGrow = 1f;
            details.style.flexShrink = 1f;
            details.style.overflow = Overflow.Hidden;
            details.style.paddingLeft = 7f;
            details.style.paddingRight = 7f;
            details.style.paddingTop = 5f;
            details.style.paddingBottom = 7f;
            var body = new IMGUIContainer(inspectorPage.DrawInspectorContents);
            body.style.flexGrow = 1f;
            body.style.flexShrink = 1f;
            body.style.minWidth = 0f;
            body.style.width = Length.Percent(100f);
            body.style.marginLeft = 2f;
            body.style.marginRight = 2f;
            details.Add(body);
            shellRoot.Add(details);
            rootVisualElement.Add(shellRoot);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            InvokeCloseActionOnce();
            if (ReferenceEquals(UsingWindow, this))
                UsingWindow = null;
            shellRoot = null;
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

            public void DrawInspectorContents()
            {
                if (target == null)
                {
                    ESTrackInspectorVisuals.DrawEmptyState(
                        "尚未选择轨道或片段",
                        "选择时间轴中的轨道或片段后，在这里编辑业务设置。",
                        "当前 Inspector 等待编辑目标。");
                    return;
                }

                DrawESInspectorSummary(target);

                editor ??= OdinEditor.CreateEditor(target, typeof(OdinEditor)) as OdinEditor;
                if (editor != null)
                {
                    UnityEngine.Object editTarget = undoTarget != null ? undoTarget : target;
                    RecordUndoBeforeInspectorInput(editTarget, "编辑时间轴属性");
                    EditorGUI.BeginChangeCheck();
                    using (ESTrackInspectorVisuals.BeginBody())
                    {
                        editor.DrawDefaultInspector();
                    }
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
                ESTrackInspectorVisuals.DrawSummary(target);
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

    /// <summary>
    /// Track Inspector 的 IMGUI 视觉适配层。
    /// GraphView 使用 UI Toolkit，但两者必须共享同一组 ES presentation token，
    /// 且不能通过全局 GUI 颜色污染其他 Unity 窗口。
    /// </summary>
    internal static class ESTrackInspectorVisuals
    {
        private static Color InspectorTextColor
        {
            get { return EditorInternal.ESEditorPresentation.IsProSkin
                ? new Color(0.88f, 0.91f, 0.96f, 1f)
                : EditorInternal.ESEditorPresentation.SectionTextColor; }
        }

        private static Color InspectorMutedTextColor
        {
            get { return EditorInternal.ESEditorPresentation.IsProSkin
                ? new Color(0.66f, 0.71f, 0.79f, 1f)
                : EditorInternal.ESEditorPresentation.SectionMutedTextColor; }
        }

        private static Color InspectorSelectedTextColor
        {
            get { return EditorInternal.ESEditorPresentation.IsProSkin
                ? new Color(0.44f, 0.74f, 1f, 1f)
                : EditorInternal.ESEditorPresentation.SectionSelectedTextColor; }
        }

        internal sealed class BodyScope : IDisposable
        {
            private readonly GUISkin previousSkin;
            private readonly float previousLabelWidth;
            private readonly float previousFieldWidth;
            private readonly bool previousWideMode;
            private readonly int previousIndentLevel;
            private readonly Color previousGuiColor;
            private readonly Color previousContentColor;
            private readonly Color previousBackgroundColor;
            private readonly GUISkin localSkin;
            private bool disposed;

            private static GUISkin cachedSkin;
            private static int cachedSkinGeneration = -1;
            private static readonly List<Texture2D> cachedTextures = new List<Texture2D>(8);

            internal BodyScope()
            {
                previousSkin = GUI.skin;
                previousLabelWidth = EditorGUIUtility.labelWidth;
                previousFieldWidth = EditorGUIUtility.fieldWidth;
                previousWideMode = EditorGUIUtility.wideMode;
                previousIndentLevel = EditorGUI.indentLevel;
                previousGuiColor = GUI.color;
                previousContentColor = GUI.contentColor;
                previousBackgroundColor = GUI.backgroundColor;

                EditorGUIUtility.wideMode = false;
                EditorGUIUtility.labelWidth = Mathf.Clamp(
                    EditorGUIUtility.currentViewWidth * 0.22f, 78f, 102f);
                EditorGUIUtility.fieldWidth = 64f;
                EditorGUI.indentLevel = 0;
                GUI.color = Color.white;
                GUI.contentColor = InspectorTextColor;
                localSkin = GetCachedGraphSkin(previousSkin);
                GUI.skin = localSkin;
                EditorGUILayout.BeginVertical(EditorInternal.ESEditorPresentation.SurfaceStyle);
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                EditorGUILayout.EndVertical();
                GUI.skin = previousSkin;
                GUI.color = previousGuiColor;
                GUI.contentColor = previousContentColor;
                GUI.backgroundColor = previousBackgroundColor;
                EditorGUI.indentLevel = previousIndentLevel;
                EditorGUIUtility.labelWidth = previousLabelWidth;
                EditorGUIUtility.fieldWidth = previousFieldWidth;
                EditorGUIUtility.wideMode = previousWideMode;
            }

            private static GUISkin GetCachedGraphSkin(GUISkin source)
            {
                int generation = EditorInternal.ESEditorPresentation.SkinGeneration;
                if (cachedSkin != null && cachedSkinGeneration == generation)
                    return cachedSkin;

                DestroyCachedGraphSkin();
                cachedSkin = BuildGraphSkin(source);
                cachedSkinGeneration = generation;
                return cachedSkin;
            }

            private static GUISkin BuildGraphSkin(GUISkin source)
            {
                GUISkin skin = ScriptableObject.CreateInstance<GUISkin>();
                skin.hideFlags = HideFlags.HideAndDontSave;

                Color text = InspectorTextColor;
                Color muted = InspectorMutedTextColor;
                Color selected = InspectorSelectedTextColor;
                if (EditorInternal.ESEditorPresentation.IsProSkin)
                {
                    // Graph 的深色面板需要“深底 + 高对比字”；原 token 的正文灰度对 Odin 字段过低。
                    text = new Color(0.88f, 0.91f, 0.96f, 1f);
                    muted = new Color(0.66f, 0.71f, 0.79f, 1f);
                    selected = new Color(0.44f, 0.74f, 1f, 1f);
                }
                Color surface = Darken(EditorInternal.ESEditorPresentation.GetDepthBackground(2), 0.18f);
                Color input = Darken(EditorInternal.ESEditorPresentation.GetDepthBackground(3), 0.12f);
                Color accent = EditorInternal.ESEditorPresentation.GetDepthAccent(0);

                skin.label = MakeStyle(source != null ? source.label : null, text, Color.clear);
                skin.box = MakeStyle(source != null ? source.box : null, text, surface);
                skin.button = MakeStyle(source != null ? source.button : null, selected, input);
                skin.textField = MakeStyle(source != null ? source.textField : null, text, input);
                skin.textArea = MakeStyle(source != null ? source.textArea : null, text, input);
                skin.toggle = MakeStyle(source != null ? source.toggle : null, text, Color.clear);
                skin.horizontalSlider = MakeStyle(source != null ? source.horizontalSlider : null, muted, input);
                skin.horizontalSliderThumb = MakeStyle(source != null ? source.horizontalSliderThumb : null, selected, accent);
                skin.verticalSlider = MakeStyle(source != null ? source.verticalSlider : null, muted, input);
                skin.verticalSliderThumb = MakeStyle(source != null ? source.verticalSliderThumb : null, selected, accent);

                skin.customStyles = new[]
                {
                    MakeNamedStyle("Label", source != null ? source.GetStyle("Label") : null, text, Color.clear),
                    MakeNamedStyle("BoldLabel", source != null ? source.GetStyle("BoldLabel") : null, selected, Color.clear),
                    MakeNamedStyle("MiniLabel", source != null ? source.GetStyle("MiniLabel") : null, muted, Color.clear),
                    MakeNamedStyle("Foldout", source != null ? source.GetStyle("Foldout") : null, selected, Color.clear),
                    MakeStyle(source != null ? source.GetStyle("Toolbar") : null, text, surface),
                    MakeStyle(source != null ? source.GetStyle("ToolbarButton") : null, selected, input),
                    MakeStyle(source != null ? source.GetStyle("HelpBox") : null, muted, surface),
                    MakeStyle(source != null ? source.GetStyle("ObjectField") : null, text, input),
                };
                return skin;
            }

            private static Color Darken(Color color, float amount)
            {
                if (!EditorInternal.ESEditorPresentation.IsProSkin)
                    return color;
                float factor = Mathf.Clamp01(1f - amount);
                return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
            }

            private static GUIStyle MakeNamedStyle(string name, GUIStyle source, Color textColor, Color backgroundColor)
            {
                GUIStyle style = MakeStyle(source, textColor, backgroundColor);
                style.name = name;
                return style;
            }

            private static GUIStyle MakeStyle(GUIStyle source, Color textColor, Color backgroundColor)
            {
                GUIStyle style = source != null ? new GUIStyle(source) : new GUIStyle();
                style.normal.textColor = textColor;
                style.onNormal.textColor = textColor;
                style.hover.textColor = textColor;
                style.onHover.textColor = textColor;
                style.active.textColor = Color.white;
                style.onActive.textColor = Color.white;
                style.focused.textColor = textColor;
                style.onFocused.textColor = textColor;
                if (backgroundColor.a > 0f)
                {
                    Texture2D texture = MakeTexture(backgroundColor);
                    style.normal.background = texture;
                    style.hover.background = texture;
                    style.active.background = MakeTexture(Color.Lerp(backgroundColor, EditorInternal.ESEditorPresentation.GetDepthAccent(0), 0.22f));
                    style.focused.background = texture;
                    style.onNormal.background = texture;
                    style.onHover.background = texture;
                    style.onActive.background = style.active.background;
                    style.onFocused.background = texture;
                }
                return style;
            }

            private static Texture2D MakeTexture(Color color)
            {
                Texture2D texture = new Texture2D(1, 1)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "ESTrackInspectorLocalSkin"
                };
                texture.SetPixel(0, 0, color);
                texture.Apply(false, true);
                cachedTextures.Add(texture);
                return texture;
            }

            private static void DestroyCachedGraphSkin()
            {
                for (int i = 0; i < cachedTextures.Count; i++)
                {
                    if (cachedTextures[i] != null)
                        UnityEngine.Object.DestroyImmediate(cachedTextures[i]);
                }
                cachedTextures.Clear();
                if (cachedSkin != null)
                    UnityEngine.Object.DestroyImmediate(cachedSkin);
                cachedSkin = null;
                cachedSkinGeneration = -1;
            }
        }

        public static BodyScope BeginBody()
        {
            return new BodyScope();
        }

        public static void DrawSummary(UnityEngine.Object target)
        {
            VisualGUIDrawerSO drawer = target as VisualGUIDrawerSO;
            object data = drawer != null ? drawer.drawerData : null;
            string typeName = data != null
                ? data.GetType()._GetTypeDisplayName()
                : "未绑定业务对象";
            bool isClip = data is ITrackClip;
            string context = isClip ? "片段属性" : "轨道属性";
            string displayName = data is ITrackItem trackItem
                ? trackItem.DisplayName
                : data is ITrackClip trackClip
                    ? trackClip.DisplayName
                    : typeName;

            Color previousContentColor = GUI.contentColor;
            Color previousBackgroundColor = GUI.backgroundColor;
            Rect cardRect;
            using (new EditorGUILayout.VerticalScope(EditorInternal.ESEditorPresentation.SurfaceStyle))
            {
                EditorGUILayout.LabelField(context, EditorInternal.ESEditorPresentation.MetaStyle);
                GUI.contentColor = InspectorSelectedTextColor;
                EditorGUILayout.LabelField(
                    string.IsNullOrEmpty(displayName) ? "未命名" : displayName,
                    EditorInternal.ESEditorPresentation.HeaderStyle);
                GUI.contentColor = InspectorMutedTextColor;
                EditorGUILayout.LabelField(typeName, EditorInternal.ESEditorPresentation.SubtitleStyle);
                GUI.contentColor = InspectorTextColor;
                EditorGUILayout.LabelField("状态", "编辑中 · 修改将同步回当前时间轴");
            }

            cardRect = GUILayoutUtility.GetLastRect();
            GUI.contentColor = previousContentColor;
            GUI.backgroundColor = previousBackgroundColor;
            DrawCardFrame(cardRect, EditorInternal.ESEditorPresentation.GetDepthAccent(0));
            EditorGUILayout.Space(4f);
        }

        public static void DrawEmptyState(string title, string description, string notice)
        {
            Color previousContentColor = GUI.contentColor;
            Color previousBackgroundColor = GUI.backgroundColor;
            Rect cardRect;
            using (new EditorGUILayout.VerticalScope(EditorInternal.ESEditorPresentation.SurfaceStyle))
            {
                EditorGUILayout.LabelField("轨道属性", EditorInternal.ESEditorPresentation.MetaStyle);
                GUI.contentColor = InspectorSelectedTextColor;
                EditorGUILayout.LabelField(title, EditorInternal.ESEditorPresentation.HeaderStyle);
                GUI.contentColor = InspectorMutedTextColor;
                EditorGUILayout.LabelField(description, EditorInternal.ESEditorPresentation.SubtitleStyle);
                EditorGUILayout.Space(5f);
                Color accent = EditorInternal.ESEditorPresentation.GetDepthAccent(0);
                Color noticeBackground = Color.Lerp(
                    EditorInternal.ESEditorPresentation.GetDepthBackground(2), accent,
                    EditorInternal.ESEditorPresentation.IsProSkin ? 0.13f : 0.08f);
                Color oldBackground = GUI.backgroundColor;
                GUI.backgroundColor = noticeBackground;
                EditorGUILayout.HelpBox(notice, MessageType.Info);
                GUI.backgroundColor = oldBackground;
            }

            cardRect = GUILayoutUtility.GetLastRect();
            GUI.contentColor = previousContentColor;
            GUI.backgroundColor = previousBackgroundColor;
            DrawCardFrame(cardRect, EditorInternal.ESEditorPresentation.GetDepthAccent(0));
        }

        private static void DrawCardFrame(Rect rect, Color accent)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;
            EditorInternal.ESEditorPresentation.DrawFrame(
                rect, EditorInternal.ESEditorPresentation.DividerColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), accent);
        }
    }
}
