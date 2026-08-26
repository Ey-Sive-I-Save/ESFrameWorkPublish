using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Static menu registrar for explicit project actions on the shared ES editor theme. This is
    /// not an EditorWindow lifecycle owner: the EditorWindow references below are transient reads
    /// of the focused window for menu commands only. It must not receive window binding, sleep,
    /// reload, or close callbacks. Opening an inspector never creates this asset implicitly; a
    /// missing asset simply falls back to the in-memory default palette.
    /// </summary>
    internal static class ESGlobalEditorThemeMenu
    {
        private const string ThemeFolder = "Assets/ESNormalAssets/Data/GlobalData/EditorTheme";
        private const string ThemeAssetPath = ThemeFolder + "/ESGlobalEditorTheme.asset";
        private const string CreateMenuPath
            = MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/主题/创建 ES 默认编辑器主题";
        private const string SelectMenuPath
            = MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/主题/定位 ES 编辑器主题";
        private const string RestoreMenuPath
            = MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/主题/恢复 ES 默认编辑器主题";
        private const string SemiSleepMenuPath
            = MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/窗口半休眠";
        private const string FocusModeMenuPath
            = MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/聚焦当前 ES 窗口";
        private const string SaveWorkspaceMenuPath
            = MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/工作区/保存当前工作区";
        private const string RestoreWorkspaceMenuPath
            = MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/工作区/恢复当前工作区";

        [MenuItem(SemiSleepMenuPath, false, 40)]
        private static void ToggleWindowSemiSleep()
        {
            ESEditorPresentation.SetSemiSleepEnabled(!ESEditorPresentation.SemiSleepEnabled);
            Menu.SetChecked(SemiSleepMenuPath, ESEditorPresentation.SemiSleepEnabled);
        }

        [MenuItem(SemiSleepMenuPath, true)]
        private static bool ValidateWindowSemiSleep()
        {
            Menu.SetChecked(SemiSleepMenuPath, ESEditorPresentation.SemiSleepEnabled);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem(FocusModeMenuPath, false, 41)]
        private static void ToggleFocusMode()
        {
            EditorWindow focused = EditorWindow.focusedWindow;
            if (focused == null)
                return;
            bool enabled = !ESEditorPresentation.IsFocusMode(focused);
            ESEditorPresentation.SetFocusMode(focused, enabled);
            Menu.SetChecked(FocusModeMenuPath, enabled);
        }

        [MenuItem(FocusModeMenuPath, true)]
        private static bool ValidateFocusMode()
        {
            EditorWindow focused = EditorWindow.focusedWindow;
            bool valid = ESEditorPresentation.IsWindowBound(focused);
            Menu.SetChecked(FocusModeMenuPath, valid && ESEditorPresentation.IsFocusMode(focused));
            return valid && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem(SaveWorkspaceMenuPath, false, 50)]
        private static void SaveWorkspace()
        {
            ESEditorPresentation.SaveWorkspaceSnapshot("default");
            Debug.Log("[ES] 已保存当前工作区快照（当前 Editor 会话）。");
        }

        [MenuItem(SaveWorkspaceMenuPath, true)]
        private static bool ValidateSaveWorkspace()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem(RestoreWorkspaceMenuPath, false, 51)]
        private static void RestoreWorkspace()
        {
            int restored = ESEditorPresentation.RestoreWorkspaceSnapshot("default");
            if (restored == 0)
                Debug.Log("[ES] 当前会话没有可恢复的 ES 工作区快照。");
            else
                Debug.Log("[ES] 已恢复 " + restored + " 个仍打开的 ES 窗口状态。");
        }

        [MenuItem(RestoreWorkspaceMenuPath, true)]
        private static bool ValidateRestoreWorkspace()
        {
            return ESEditorPresentation.HasWorkspaceSnapshot("default")
                && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem(CreateMenuPath, false, 30)]
        private static void CreateDefaultTheme()
        {
            ESGlobalEditorTheme theme = EnsureDefaultTheme();

            ESGlobalEditorTheme.Instance = theme;
            ESEditorPresentation.InvalidateTheme();
            Selection.activeObject = theme;
            EditorGUIUtility.PingObject(theme);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        [MenuItem(CreateMenuPath, true)]
        private static bool ValidateCreateDefaultTheme()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem(SelectMenuPath, false, 31)]
        private static void SelectTheme()
        {
            ESGlobalEditorTheme theme = LoadTheme();
            if (theme == null)
            {
                EditorUtility.DisplayDialog(
                    "没有找到 ES 编辑器主题",
                    "请先执行“创建 ES 默认编辑器主题”。未创建时会继续使用内存默认色板。",
                    "知道了");
                return;
            }

            Selection.activeObject = theme;
            EditorGUIUtility.PingObject(theme);
        }

        [MenuItem(SelectMenuPath, true)]
        private static bool ValidateSelectTheme()
        {
            return LoadTheme() != null;
        }

        [MenuItem(RestoreMenuPath, false, 32)]
        private static void RestoreDefaultTheme()
        {
            ESGlobalEditorTheme theme = LoadTheme();
            if (theme == null)
            {
                CreateDefaultTheme();
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "恢复 ES 默认编辑器主题",
                    "将覆盖当前项目主题色板和密度设置。此操作可通过 Undo 撤销。",
                    "恢复默认",
                    "取消"))
                return;

            Undo.RecordObject(theme, "恢复 ES 默认编辑器主题");
            theme.RestoreDefault();
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssetIfDirty(theme);
            ESEditorPresentation.InvalidateTheme();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        [MenuItem(RestoreMenuPath, true)]
        private static bool ValidateRestoreDefaultTheme()
        {
            return LoadTheme() != null && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        internal static ESGlobalEditorTheme EnsureDefaultTheme()
        {
            ESGlobalEditorTheme theme = LoadTheme();
            if (theme != null)
                return theme;

            EnsureFolder();
            theme = ScriptableObject.CreateInstance<ESGlobalEditorTheme>();
            theme.name = "ESGlobalEditorTheme";
            theme.RestoreDefault();
            theme.HasConfirm = true;
            try
            {
                AssetDatabase.CreateAsset(theme, ThemeAssetPath);
            }
            catch
            {
                if (theme != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(theme)))
                    UnityEngine.Object.DestroyImmediate(theme);
                throw;
            }
            AssetDatabase.SaveAssetIfDirty(theme);
            AssetDatabase.Refresh();
            Debug.Log("[ES] 已创建默认编辑器主题：" + ThemeAssetPath, theme);
            return theme;
        }

        internal static ESGlobalEditorTheme LoadTheme()
        {
            ESGlobalEditorTheme theme = AssetDatabase.LoadAssetAtPath<ESGlobalEditorTheme>(ThemeAssetPath);
            if (theme != null)
                return theme;

            string[] guids = AssetDatabase.FindAssets("t:ESGlobalEditorTheme");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                theme = AssetDatabase.LoadAssetAtPath<ESGlobalEditorTheme>(path);
                if (theme != null)
                    return theme;
            }

            return null;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ESNormalAssets"))
                AssetDatabase.CreateFolder("Assets", "ESNormalAssets");
            if (!AssetDatabase.IsValidFolder("Assets/ESNormalAssets/Data"))
                AssetDatabase.CreateFolder("Assets/ESNormalAssets", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/ESNormalAssets/Data/GlobalData"))
                AssetDatabase.CreateFolder("Assets/ESNormalAssets/Data", "GlobalData");
            if (!AssetDatabase.IsValidFolder(ThemeFolder))
                AssetDatabase.CreateFolder("Assets/ESNormalAssets/Data/GlobalData", "EditorTheme");
        }
    }

    [CustomEditor(typeof(ESGlobalEditorTheme))]
    internal sealed class ESGlobalEditorThemeInspector : OdinEditor
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            Undo.undoRedoPerformed -= HandleUndoRedo;
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        protected override void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
                ESGlobalEditorThemeChangeBridge.NotifyThemeChanged(
                    target as ESGlobalEditorTheme);
        }

        private void HandleUndoRedo()
        {
            serializedObject?.UpdateIfRequiredOrScript();
            ESGlobalEditorThemeChangeBridge.NotifyThemeChanged(
                target as ESGlobalEditorTheme);
            Repaint();
        }
    }

    internal static class ESGlobalEditorThemeChangeBridge
    {
        internal static void NotifyThemeChanged(ESGlobalEditorTheme changedTheme)
        {
            if (changedTheme == null)
                return;

            ESEditorPresentation.InvalidateTheme();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
