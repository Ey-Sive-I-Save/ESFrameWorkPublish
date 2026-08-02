using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Project theme workbench. It edits the shared ESGlobalEditorTheme asset directly and keeps
    /// a small live preview beside the real configuration fields.
    /// </summary>
    public sealed class ESEditorThemeWindow : EditorWindow
    {
        private const string WindowMenuPath = MenuItemPathDefine.PROJECT_SETTINGS_PATH + "编辑器主题/主题工作台";
        private Vector2 scroll;
        private ESGlobalEditorTheme theme;
        private SerializedObject serializedTheme;

        [MenuItem(WindowMenuPath, false, 20)]
        private static void Open()
        {
            var window = GetWindow<ESEditorThemeWindow>();
            window.titleContent = new GUIContent("ES 编辑器主题");
            window.minSize = new Vector2(440f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshTheme();
        }

        private void OnFocus()
        {
            if (theme == null)
                RefreshTheme();
        }

        private void OnGUI()
        {
            DrawTitle();
            if (theme == null)
            {
                DrawMissingTheme();
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            try
            {
                DrawPreview();
                EditorGUILayout.Space(8f);
                DrawConfiguration();
                EditorGUILayout.Space(8f);
                DrawActions();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawTitle()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 52f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, ESEditorPresentation.GetDepthBackground(0));
                ESEditorPresentation.DrawFrame(rect, ESEditorPresentation.GetDepthAccent(0));
            }

            GUI.Label(
                new Rect(rect.x + 12f, rect.y + 7f, rect.width - 24f, 22f),
                "ES 编辑器主题",
                ESEditorPresentation.HeaderStyle);
            GUI.Label(
                new Rect(rect.x + 12f, rect.y + 29f, rect.width - 24f, 17f),
                "项目共享 GlobalData · ESOnlyEditorSO · 不写入场景或业务资产",
                ESEditorPresentation.MetaStyle);
        }

        private void DrawMissingTheme()
        {
            EditorGUILayout.Space(12f);
            EditorGUILayout.HelpBox(
                "当前项目没有 ES 编辑器主题资产。编辑器仍会使用内存默认色板；点击下方按钮才会创建 GlobalData。",
                MessageType.Info);
            EditorGUILayout.Space(4f);
            if (GUILayout.Button("创建 ES 默认编辑器主题", GUILayout.Height(30f)))
            {
                theme = ESGlobalEditorThemeMenu.EnsureDefaultTheme();
                ESGlobalEditorTheme.Instance = theme;
                ESEditorPresentation.InvalidateTheme();
                RefreshTheme();
                RepaintAllESViews();
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            try
            {
                GUILayout.Label("实时预览", ESEditorPresentation.HeaderStyle);
                GUILayout.Label("强调色、状态色与密度会同步到已接入 ES Presentation 的界面。", ESEditorPresentation.SubtitleStyle);

                Rect readyRect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));
                Rect warningRect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));
                Rect errorRect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));
                DrawPreviewRow(readyRect, ESStatusKind.Ready, "已就绪 · 当前配置可直接编辑");
                DrawPreviewRow(warningRect, ESStatusKind.Warning, "需要关注 · 请确认当前配置");
                DrawPreviewRow(errorRect, ESStatusKind.Error, "无法继续 · 请修复缺失数据");
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawPreviewRow(Rect rect, ESStatusKind status, string text)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, ESEditorPresentation.GetDepthBackground(0));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), ESEditorPresentation.GetStatusAccent(0, status));
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), ESEditorPresentation.GetStatusFrameColor(0, status));
            }

            ESFieldRow.DrawStatus(
                new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height),
                status,
                text,
                ESEditorPresentation.MetaStyle);
        }

        private void DrawConfiguration()
        {
            if (serializedTheme == null || serializedTheme.targetObject != theme)
                serializedTheme = new SerializedObject(theme);

            serializedTheme.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            try
            {
                GUILayout.Label("共享主题配置", ESEditorPresentation.HeaderStyle);
                DrawProperty("presetId");
                DrawProperty("density");
                DrawProperty("showSectionSubtitle");
                DrawProperty("useCustomPalette");

                EditorGUILayout.Space(5f);
                GUILayout.Label("深色皮肤", ESEditorPresentation.SubtitleStyle);
                DrawProperty("darkAccentStart");
                DrawProperty("darkAccentEnd");
                DrawProperty("darkWarning");
                DrawProperty("darkError");

                EditorGUILayout.Space(5f);
                GUILayout.Label("浅色皮肤", ESEditorPresentation.SubtitleStyle);
                DrawProperty("lightAccentStart");
                DrawProperty("lightAccentEnd");
                DrawProperty("lightWarning");
                DrawProperty("lightError");
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedTheme.ApplyModifiedProperties();
                EditorUtility.SetDirty(theme);
                ESEditorPresentation.InvalidateTheme();
                RepaintAllESViews();
            }
        }

        private void DrawProperty(string propertyName)
        {
            SerializedProperty property = serializedTheme.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            try
            {
                if (GUILayout.Button("定位主题资产", GUILayout.Height(26f)))
                {
                    Selection.activeObject = theme;
                    EditorGUIUtility.PingObject(theme);
                }

                if (GUILayout.Button("恢复 ES 默认", GUILayout.Height(26f)))
                {
                    if (!EditorUtility.DisplayDialog(
                            "恢复 ES 默认编辑器主题",
                            "将覆盖当前主题色板和密度。可使用 Ctrl+Z 撤销。",
                            "恢复默认",
                            "取消"))
                        return;

                    Undo.RecordObject(theme, "恢复 ES 默认编辑器主题");
                    theme.RestoreDefault();
                    EditorUtility.SetDirty(theme);
                    ESEditorPresentation.InvalidateTheme();
                    RepaintAllESViews();
                }
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        private void RefreshTheme()
        {
            theme = ESGlobalEditorThemeMenu.LoadTheme();
            serializedTheme = theme == null ? null : new SerializedObject(theme);
        }

        private void RepaintAllESViews()
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            Repaint();
        }
    }
}
