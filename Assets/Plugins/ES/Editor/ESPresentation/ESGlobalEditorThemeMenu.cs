using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Explicit project actions for the shared ES editor theme. Opening an inspector never creates
    /// this asset implicitly; a missing asset simply falls back to the in-memory default palette.
    /// </summary>
    internal static class ESGlobalEditorThemeMenu
    {
        private const string ThemeFolder = "Assets/ESNormalAssets/Data/GlobalData/EditorTheme";
        private const string ThemeAssetPath = ThemeFolder + "/ESGlobalEditorTheme.asset";
        private const string CreateMenuPath
            = MenuItemPathDefine.PROJECT_SETTINGS_PATH + "全局配置/创建 ES 默认编辑器主题";
        private const string SelectMenuPath
            = MenuItemPathDefine.PROJECT_SETTINGS_PATH + "全局配置/定位 ES 编辑器主题";
        private const string RestoreMenuPath
            = MenuItemPathDefine.PROJECT_SETTINGS_PATH + "全局配置/恢复 ES 默认编辑器主题";

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
            AssetDatabase.SaveAssets();
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
            AssetDatabase.CreateAsset(theme, ThemeAssetPath);
            AssetDatabase.SaveAssets();
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
}
