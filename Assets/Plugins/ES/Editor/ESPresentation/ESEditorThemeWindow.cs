using System.Collections.Generic;
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
        private static readonly Dictionary<string, GUIContent> PropertyLabels =
            new Dictionary<string, GUIContent>
            {
                { "presetId", new GUIContent("主题预设", "项目共享的主题预设稳定标识。") },
                { "density", new GUIContent("界面密度", "控制 ES 窗口的间距和控件密度。") },
                { "showSectionSubtitle", new GUIContent("显示分区副标题", "显示用于解释业务分区的辅助文字。") },
                { "useCustomPalette", new GUIContent("启用自定义色板", "关闭时使用 ES 内置安全色板。") },
                { "enableGlobalEditorShell", new GUIContent("启用 ES 全局外观", "覆盖 Unity 公开回调允许安全接入的主要 Editor Shell 表面。") },
                { "enableDeepEditorSkin", new GUIContent("Unity 全局深度皮肤（实验）", "为安全内容容器应用 ES 纯色表面，并染色已有控件背景；不填充窗口根节点，不遮挡原生内容，进入 PlayMode 自动停用。") },
                { "enableMotion", new GUIContent("启用编辑器动效", "启用局部反馈动画，不影响编辑数据。") },
                { "motionIntensity", new GUIContent("动效强度", "建议保持 0.65～0.85。") },
                { "darkAccentStart", new GUIContent("深色强调起始色") },
                { "darkAccentEnd", new GUIContent("深色强调结束色") },
                { "darkWarning", new GUIContent("深色警告色") },
                { "darkError", new GUIContent("深色错误色") },
                { "lightAccentStart", new GUIContent("浅色强调起始色") },
                { "lightAccentEnd", new GUIContent("浅色强调结束色") },
                { "lightWarning", new GUIContent("浅色警告色") },
                { "lightError", new GUIContent("浅色错误色") }
            };
        private Vector2 scroll;
        private ESGlobalEditorTheme theme;
        private SerializedObject serializedTheme;
        private double previewFeedbackStartedAt;
        private string deepSkinStatus = string.Empty;
        private MessageType deepSkinStatusType = MessageType.Info;
        private bool pendingDeepSkinEnabled;
        private bool pendingDeepSkinRefresh;

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
            ESEditorPresentation.BindWindow(this);
            previewFeedbackStartedAt = EditorApplication.timeSinceStartup;
            RefreshTheme();
            RefreshDeepSkinStatus();
        }

        private void OnDestroy()
        {
            EditorApplication.delayCall -= CompleteDeepSkinAction;
            ESEditorPresentation.UnbindWindow(this);
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= CompleteDeepSkinAction;
            ESEditorPresentation.UnbindWindow(this);
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
                DrawDeepSkinControls();
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
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), ESEditorPresentation.LogicSteelBlue);
                EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.yMax - 1f, rect.width - 3f, 1f), ESEditorPresentation.LogicGold);
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

                if (ESEditorPresentation.MotionEnabled
                    && ESEditorPresentation.EvaluatePulse(previewFeedbackStartedAt, 1.20f) > 0f)
                    Repaint();
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawPreviewRow(Rect rect, ESStatusKind status, string text)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color accent = ESEditorPresentation.GetStatusAccent(0, status);
                EditorGUI.DrawRect(rect, ESEditorPresentation.GetDepthBackground(0));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), accent);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), ESEditorPresentation.GetStatusFrameColor(0, status));
                ESEditorPresentation.DrawFeedbackSweep(rect, accent, previewFeedbackStartedAt, 1.20f, 0.16f);
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
                DrawProperty("enableGlobalEditorShell");
                DrawProperty("enableDeepEditorSkin");
                DrawProperty("enableMotion");
                DrawProperty("motionIntensity");

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
                previewFeedbackStartedAt = EditorApplication.timeSinceStartup;
                ESEditorPresentation.PulseWindow(this, ESStatusKind.Modified);
                ESEditorPresentation.InvalidateTheme();
                RepaintAllESViews();
                RefreshDeepSkinStatus();
            }
        }

        private void DrawProperty(string propertyName)
        {
            SerializedProperty property = serializedTheme.FindProperty(propertyName);
            if (property != null)
            {
                if (PropertyLabels.TryGetValue(propertyName, out GUIContent label))
                    EditorGUILayout.PropertyField(property, label, true);
                else
                    EditorGUILayout.PropertyField(property, true);
            }
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
                    ESEditorPresentation.PulseWindow(this, ESStatusKind.Modified);
                    ESEditorPresentation.InvalidateTheme();
                    RepaintAllESViews();
                }
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }

        }

        private void DrawDeepSkinControls()
        {
            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            try
            {
                GUILayout.Label("Unity 全局深度皮肤", ESEditorPresentation.HeaderStyle);
                GUILayout.Label(
                    "纯色只用于安全内容容器；窗口根节点与透明绘制层保持原样。不扫描资产、不常驻轮询，PlayMode 自动停用。",
                    ESEditorPresentation.SubtitleStyle);
                EditorGUILayout.HelpBox(deepSkinStatus, deepSkinStatusType);

                bool enabled = theme != null && theme.enableDeepEditorSkin;
                EditorGUILayout.BeginHorizontal();
                try
                {
                    string applyLabel = enabled && ESGlobalEditorSkinExperiment.IsApplied
                        ? "刷新全局窗口覆盖"
                        : "启用并应用全局皮肤";
                    if (GUILayout.Button(applyLabel, GUILayout.Height(28f)))
                        ApplyOrRefreshDeepSkin();

                    using (new EditorGUI.DisabledScope(!enabled && !ESGlobalEditorSkinExperiment.IsApplied))
                    {
                        if (GUILayout.Button("停用并恢复原生样式", GUILayout.Height(28f)))
                            DisableDeepSkin();
                    }
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private void ApplyOrRefreshDeepSkin()
        {
            SetDeepSkinEnabled(true);
            deepSkinStatus = ESGlobalEditorSkinExperiment.IsApplied
                ? "正在刷新已打开窗口的 ES 全局皮肤覆盖……"
                : "正在应用 ES 全局皮肤……";
            deepSkinStatusType = MessageType.Info;
            QueueDeepSkinAction(true, ESGlobalEditorSkinExperiment.IsApplied);
        }

        private void DisableDeepSkin()
        {
            SetDeepSkinEnabled(false);
            deepSkinStatus = "正在恢复 Unity 原生样式……";
            deepSkinStatusType = MessageType.Info;
            QueueDeepSkinAction(false, false);
        }

        private void QueueDeepSkinAction(bool enabled, bool forceRefresh)
        {
            pendingDeepSkinEnabled = enabled;
            pendingDeepSkinRefresh = forceRefresh;
            EditorApplication.delayCall -= CompleteDeepSkinAction;
            EditorApplication.delayCall += CompleteDeepSkinAction;
            Repaint();
        }

        private void CompleteDeepSkinAction()
        {
            EditorApplication.delayCall -= CompleteDeepSkinAction;
            if (this == null)
                return;

            bool success = true;
            string message;
            if (!pendingDeepSkinEnabled)
            {
                ESGlobalEditorSkinExperiment.Restore();
                message = "已恢复 Unity 原生样式。项目开关已关闭，域重载后不会自动重新应用。";
            }
            else if (pendingDeepSkinRefresh && ESGlobalEditorSkinExperiment.IsApplied)
            {
                success = ESGlobalEditorSkinExperiment.Refresh(out message);
            }
            else
            {
                success = ESGlobalEditorSkinExperiment.TryApply(out message);
            }

            deepSkinStatus = message;
            deepSkinStatusType = success ? MessageType.Info : MessageType.Error;
            ESEditorPresentation.PulseWindow(this, success ? ESStatusKind.Modified : ESStatusKind.Error);
            RepaintAllESViews();
        }

        private void SetDeepSkinEnabled(bool enabled)
        {
            if (theme == null)
                return;
            if (serializedTheme == null || serializedTheme.targetObject != theme)
                serializedTheme = new SerializedObject(theme);
            serializedTheme.Update();
            SerializedProperty property = serializedTheme.FindProperty("enableDeepEditorSkin");
            if (property == null || property.boolValue == enabled)
                return;
            Undo.RecordObject(theme, enabled ? "启用 ES 全局深度皮肤" : "停用 ES 全局深度皮肤");
            property.boolValue = enabled;
            serializedTheme.ApplyModifiedProperties();
            EditorUtility.SetDirty(theme);
            ESEditorPresentation.InvalidateTheme();
        }

        private void RefreshDeepSkinStatus()
        {
            if (ESGlobalEditorSkinExperiment.IsApplied)
            {
                deepSkinStatus = "已生效：当前覆盖 " + ESGlobalEditorSkinExperiment.StyledWindowCount
                    + " 个 UI Toolkit 窗口，并已统一常用 IMGUI 控件。";
                deepSkinStatusType = MessageType.Info;
                return;
            }

            if (theme != null && theme.enableDeepEditorSkin)
            {
                deepSkinStatus = "项目已启用全局深度皮肤，正在等待 Editor 样式初始化；可点击下方按钮立即应用或刷新。";
                deepSkinStatusType = MessageType.Warning;
                return;
            }

            deepSkinStatus = "当前使用 Unity 原生样式。启用后可随时恢复，且不会写入场景或业务资产。";
            deepSkinStatusType = MessageType.Info;
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
