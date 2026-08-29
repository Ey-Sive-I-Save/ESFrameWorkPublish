using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;


namespace ES
{
    public class ESEditorToolBar
    {

        public static class CustomToolbarMenu
        {
            // 缓存场景路径以提高性能
            private static List<string> cachedBuildScenes = new List<string>();
            private static List<string> cachedAllScenes = new List<string>();
            private static bool scenesCached = false;
            private const string RecentScenesPrefsKey = "ES_Toolbar_RecentScenes";
        private const int MaxRecentSceneCount = 8;
        private const int MaxRecentPreferenceCharacters = 64 * 1024;
            private const string RecentAssetsPrefsKey = "ES_Toolbar_RecentAssets";
            private const int MaxRecentAssetCount = 10;

            /// <summary>
            /// 由程序集流安装工具栏回调。重复调用时先卸载再安装，避免重复绘制。
            /// </summary>
            internal static void Install()
            {
                ToolbarExtender.Initialize();

                // 注册到右边工具栏
                ToolbarExtender.RightToolbarGUI.Remove(OnSceneSelectorToolbarGUI);
                ToolbarExtender.RightToolbarGUI.Remove(OnCustomSceneToolbarGUI);
                ToolbarExtender.RightToolbarGUI.Remove(OnSceneSelectorSettingsToolbarGUI);
                ToolbarExtender.RightToolbarGUI.Add(OnSceneSelectorToolbarGUI);
                ToolbarExtender.RightToolbarGUI.Add(OnCustomSceneToolbarGUI);
                ToolbarExtender.RightToolbarGUI.Add(OnSceneSelectorSettingsToolbarGUI);
                //左边
                ToolbarExtender.LeftToolbarGUI.Remove(OnQuickSelectionToolbarGUI);
                ToolbarExtender.LeftToolbarGUI.Remove(OnAssetQuickAccessToolbarGUI);
                ToolbarExtender.LeftToolbarGUI.Remove(OnESQuickToolbarGUI);
                ToolbarExtender.LeftToolbarGUI.Remove(OnCmdAgentToolbarGUI);
                ToolbarExtender.LeftToolbarGUI.Add(OnQuickSelectionToolbarGUI);
                ToolbarExtender.LeftToolbarGUI.Add(OnAssetQuickAccessToolbarGUI);
                ToolbarExtender.LeftToolbarGUI.Add(OnESQuickToolbarGUI);
                ToolbarExtender.LeftToolbarGUI.Add(OnCmdAgentToolbarGUI);
            }

            private static void CacheScenes()
            {
                try
                {
                    // 缓存构建场景
                    cachedBuildScenes.Clear();
                    var buildScenes = EditorBuildSettings.scenes;
                    foreach (var scene in buildScenes)
                    {
                        if (!string.IsNullOrEmpty(scene.path))
                        {
                            cachedBuildScenes.Add(scene.path);
                        }
                    }

                    // 缓存所有场景
                    cachedAllScenes.Clear();
                    string[] guids = AssetDatabase.FindAssets("t:Scene");
                    foreach (string guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(path))
                        {
                            cachedAllScenes.Add(path);
                        }
                    }
                    cachedAllScenes.Sort(StringComparer.OrdinalIgnoreCase);
                    scenesCached = true;
                }
                catch (Exception ex)
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                    Debug.LogError($"缓存场景失败: {ex.Message}");
                    scenesCached = false;
                }
            }

            static void OnSceneSelectorToolbarGUI()
            {
                // 创建下拉菜单按钮
                if (EditorGUILayout.DropdownButton(
                    new GUIContent("场景跳转", EditorGUIUtility.IconContent("d__Popup").image),
                    FocusType.Passive,
                    EditorStyles.toolbarDropDown))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                    ShowBuildScenesMenu(GUILayoutUtility.GetLastRect());
                }
            }

            /// <summary>
            /// 显示Build Settings场景菜单
            /// </summary>
            private static void ShowBuildScenesMenu(Rect anchorRect)
            {
                EnsureScenesCached();
                var entries = new List<ESSearchDropdown.Entry>();
                string activeScenePath = EditorSceneManager.GetActiveScene().path;

                foreach (string recentPath in GetRecentScenePaths())
                {
                    string canonicalRecentPath = ResolveCanonicalScenePath(recentPath);
                    if (string.IsNullOrEmpty(canonicalRecentPath))
                        continue;

                    entries.Add(CreateSceneEntry(canonicalRecentPath, "最近打开", activeScenePath));
                }

                if (cachedBuildScenes.Count == 0)
                {
                    entries.Add(ESSearchDropdown.Entry.Disabled("无 Build 场景", "Build Settings"));
                }
                else
                {
                    foreach (string scenePath in cachedBuildScenes)
                        entries.Add(CreateSceneEntry(scenePath, "Build Settings", activeScenePath));
                }

                if (ESSceneGlobalData.Instance != null)
                {
                    foreach (var scene in ESSceneGlobalData.Instance.GetEnabledScenes())
                    {
                        if (scene == null || string.IsNullOrWhiteSpace(scene.ScenePath))
                            continue;

                        string group = string.IsNullOrWhiteSpace(scene.Group)
                            ? "自定义场景"
                            : "自定义场景/" + scene.Group;
                        string scenePath = scene.ScenePath;
                        entries.Add(ESSearchDropdown.Entry.Item(
                            scene.DisplayName,
                            () => OpenScene(scenePath, GetAdditiveMode()),
                            group,
                            EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D,
                            subtitle: scenePath,
                            badge: string.Equals(scenePath, activeScenePath, StringComparison.OrdinalIgnoreCase) ? "当前" : "自定义",
                            selected: string.Equals(scenePath, activeScenePath, StringComparison.OrdinalIgnoreCase)));
                    }
                }

                foreach (string scenePath in cachedAllScenes)
                {
                    string folder = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
                    string group = string.IsNullOrWhiteSpace(folder)
                        ? "项目全部场景"
                        : "项目全部场景/" + folder;
                    entries.Add(CreateSceneEntry(scenePath, group, activeScenePath));
                }

                entries.Add(ESSearchDropdown.Entry.Separator());
                entries.Add(ESSearchDropdown.Entry.Item("打开顶级工具栏管理面板", OpenSceneManagerWindow, "操作"));

                string mode = GetAdditiveMode() ? "叠加打开" : "单场景打开";
                ESSearchDropdown.Open(
                    anchorRect,
                    "场景跳转 · " + mode,
                    entries,
                    minimumWindowSize: new Vector2(720f, 440f));
            }

            private static void EnsureScenesCached()
            {
                if (!scenesCached)
                    CacheScenes();
            }

            private static ESSearchDropdown.Entry CreateSceneEntry(string scenePath, string groupPath, string activeScenePath)
            {
                string displayName = Path.GetFileNameWithoutExtension(scenePath);
                bool isActive = string.Equals(scenePath, activeScenePath, StringComparison.OrdinalIgnoreCase);
                string normalizedGroup = groupPath ?? "项目全部场景";
                string badge = normalizedGroup.StartsWith("最近打开", StringComparison.Ordinal) ? "最近"
                    : normalizedGroup.StartsWith("Build Settings", StringComparison.Ordinal) ? "Build"
                    : "项目";
                return ESSearchDropdown.Entry.Item(
                    displayName,
                    () => OpenScene(scenePath, GetAdditiveMode()),
                    normalizedGroup,
                    EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D,
                    subtitle: scenePath,
                    badge: isActive ? "当前" : badge,
                    selected: isActive);
            }

            /// <summary>
            /// 自定义场景工具栏GUI
            /// </summary>
            static void OnCustomSceneToolbarGUI()
            {
                if (EditorGUILayout.DropdownButton(
                    new GUIContent("自定义场景", EditorGUIUtility.IconContent("d__Popup").image),
                    FocusType.Passive,
                    EditorStyles.toolbarDropDown))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                    ShowCustomScenesMenu(GUILayoutUtility.GetLastRect());
                }
            }

            /// <summary>
            /// 显示自定义场景菜单
            /// </summary>
            private static void ShowCustomScenesMenu(Rect anchorRect)
            {
                var entries = new List<ESSearchDropdown.Entry>();

                if (ESSceneGlobalData.Instance == null)
                {
                    entries.Add(ESSearchDropdown.Entry.Disabled("顶级工具栏管理面板数据未找到"));
                    entries.Add(ESSearchDropdown.Entry.Separator());
                    entries.Add(ESSearchDropdown.Entry.Item("打开顶级工具栏管理面板", OpenSceneManagerWindow, "操作"));
                }
                else
                {
                    var customScenes = ESSceneGlobalData.Instance.GetEnabledScenes();

                    if (customScenes.Count == 0)
                    {
                        entries.Add(ESSearchDropdown.Entry.Disabled("无自定义场景"));
                    }
                    else
                    {
                        // 按分组显示
                        var groups = customScenes.GroupBy(s => s.Group).OrderBy(g => g.Key);

                        foreach (var group in groups)
                        {
                            foreach (var scene in group)
                            {
                                bool isActive = string.Equals(scene.ScenePath, EditorSceneManager.GetActiveScene().path, StringComparison.OrdinalIgnoreCase);
                                entries.Add(ESSearchDropdown.Entry.Item(
                                    scene.DisplayName,
                                    () => OpenScene(scene.ScenePath, GetAdditiveMode()),
                                    string.IsNullOrWhiteSpace(group.Key) ? "未分组" : group.Key,
                                    EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D,
                                    subtitle: scene.ScenePath,
                                    badge: isActive ? "当前" : "自定义",
                                    selected: isActive));
                            }
                        }
                    }

                    entries.Add(ESSearchDropdown.Entry.Separator());
                    entries.Add(ESSearchDropdown.Entry.Item("添加当前场景", () =>
                    {
                        AddCurrentSceneToCustom();
                    }, "操作"));
                    entries.Add(ESSearchDropdown.Entry.Item("打开顶级工具栏管理面板", OpenSceneManagerWindow, "操作"));
                }

                ESSearchDropdown.Open(anchorRect, "打开自定义场景", entries);
            }

            static void OnSceneSelectorSettingsToolbarGUI()
            {
                // 创建下拉菜单按钮
                if (EditorGUILayout.DropdownButton(
                    new GUIContent("场景设置", EditorGUIUtility.IconContent("_Popup").image),
                    FocusType.Passive,
                    EditorStyles.toolbarDropDown))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                    var menu = new GenericMenu();

                    // 从数据中读取设置
                    bool autoSave = ESSceneGlobalData.Instance != null ? 
                        ESSceneGlobalData.Instance.AutoSaveBeforeSwitch : 
                        EditorPrefs.GetBool("ES_AutoSaveBeforeSwitch", true);
                    
                    bool additiveMode = ESSceneGlobalData.Instance != null ? 
                        ESSceneGlobalData.Instance.UseAdditiveMode : 
                        EditorPrefs.GetBool("ES_UseAdditiveMode", false);

                    menu.AddItem(new GUIContent("自动保存当前场景"), autoSave, () =>
                    {
                        ToggleAutoSave();
                    });

                    menu.AddItem(new GUIContent("使用叠加场景模式"), additiveMode, () =>
                    {
                        ToggleAdditiveMode();
                    });

                    menu.AddSeparator("");

                    menu.AddItem(new GUIContent("打开顶级工具栏管理面板"), false, () =>
                    {
                        OpenSceneManagerWindow();
                    });

                    menu.AddItem(new GUIContent("打开数据配置"), false, () =>
                    {
                        if (ESSceneGlobalData.Instance != null)
                        {
                            ESEditorFeedbackSound.SuppressSelectionSound();
                            Selection.activeObject = ESSceneGlobalData.Instance;
                            EditorGUIUtility.PingObject(ESSceneGlobalData.Instance);
                            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Locate);
                        }
                    });

                    menu.AddSeparator("");

                    menu.AddItem(new GUIContent("刷新场景缓存"), false, () =>
                    {
                        scenesCached = false;
                        CacheScenes();
                        ESEditorFeedbackSound.Play(
                            scenesCached
                                ? ESEditorFeedbackSoundKind.Refresh
                                : ESEditorFeedbackSoundKind.Error);
                        Debug.Log("场景缓存已刷新");
                    });

                    menu.ShowAsContext();
                }
            }

            /// <summary>
            /// 资产快捷访问工具栏GUI
            /// </summary>
            static void OnESQuickToolbarGUI()
            {
                if (EditorGUILayout.DropdownButton(
                    new GUIContent("ES工具", EditorGUIUtility.IconContent("d__Popup").image),
                    FocusType.Passive,
                    EditorStyles.toolbarDropDown,
                    GUILayout.Width(72)))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                    ShowESQuickMenu(GUILayoutUtility.GetLastRect());
                }
            }

            private static void ShowESQuickMenu(Rect anchorRect)
            {
                var entries = new List<ESSearchDropdown.Entry>();
                entries.Add(ESSearchDropdown.Entry.Item(
                    "打开命令面板",
                    () =>
                    {
                        ESCommandPaletteWindow.OpenWindow();
                    },
                    "命令"));
                entries.Add(ESSearchDropdown.Entry.Item(
                    "GlobalData",
                    () =>
                    {
                        ESCommandPaletteWindow.OpenWithQuery("G");
                    },
                    "命令"));
                entries.Add(ESSearchDropdown.Entry.Item(
                    "AICommand",
                    () =>
                    {
                        ESCommandPaletteWindow.OpenWithQuery("$");
                    },
                    "命令"));
                entries.Add(ESSearchDropdown.Entry.Separator());

                IReadOnlyList<ESWindowDescriptor> windows = ESWindowRegistry.All;
                if (windows != null)
                {
                    for (int i = 0; i < windows.Count; i++)
                    {
                        ESWindowDescriptor window = windows[i];
                        if (window == null || string.IsNullOrWhiteSpace(window.MenuPath))
                            continue;

                        entries.Add(ESSearchDropdown.Entry.Item(
                            window.Title,
                            () => ExecuteRegisteredWindow(window),
                            "窗口",
                            EditorGUIUtility.IconContent("UnityEditor.SceneHierarchyWindow").image as Texture2D,
                            subtitle: window.MenuPath,
                            badge: "ES窗口"));
                    }
                }

                entries.Add(ESSearchDropdown.Entry.Separator());
                entries.Add(ESSearchDropdown.Entry.Item(
                    "打开顶级工具栏管理面板",
                    OpenSceneManagerWindow,
                    "操作"));
                ESSearchDropdown.Open(anchorRect, "ES 工具", entries);
            }

            private static void ExecuteRegisteredWindow(ESWindowDescriptor window)
            {
                if (window == null || string.IsNullOrWhiteSpace(window.WindowId))
                    return;

                var item = new ESCommandPaletteItem(
                    window.WindowId,
                    window.Title,
                    "打开 ES 窗口",
                    window.Category,
                    window.Keywords,
                    "@",
                    window.WindowId,
                    ESCommandPaletteActionKind.OpenWindow);
                ESCommandPaletteResult result = ESCommandPaletteExecutors.Execute(item);
                if (!result.Success)
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                    Debug.LogWarning("[ESEditorToolBar] 窗口打开失败：" + result.Message);
                }
                else
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Open);
                }
            }

            static void OnCmdAgentToolbarGUI()
            {
                if (GUILayout.Button(new GUIContent("Agent", "打开【ES】Cmd Agent：后台 CMD/Codex 中转，并按配置自动恢复最近会话"), EditorStyles.toolbarButton, GUILayout.Width(56)))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Open);
                    ESCmdAgentWindow.OpenAndResume();
                }
            }
            static void OnAssetQuickAccessToolbarGUI()
            {
                if (EditorGUILayout.DropdownButton(
                    new GUIContent("资产快捷访问", EditorGUIUtility.IconContent("d__Popup").image),
                    FocusType.Passive,
                    EditorStyles.toolbarDropDown, GUILayout.Width(120)))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                    ShowAssetQuickAccessMenu(GUILayoutUtility.GetLastRect());
                }
            }

            /// <summary>
            /// 显示资产快捷访问菜单
            /// </summary>
            private static void ShowAssetQuickAccessMenu(Rect anchorRect)
            {
                var entries = new List<ESSearchDropdown.Entry>();
                int globalDataCount = AddGlobalDataEntries(entries);
                if (globalDataCount == 0)
                {
                    entries.Add(ESSearchDropdown.Entry.Disabled(
                        "未发现可访问的 GlobalData",
                        "GlobalData",
                        "可刷新命令面板索引后重试"));
                }

                entries.Add(ESSearchDropdown.Entry.Item(
                    "在命令面板中查看全部 GlobalData",
                    () => ESCommandPaletteWindow.OpenWithQuery("G"),
                    "GlobalData/操作",
                    EditorGUIUtility.IconContent("Search Icon").image as Texture2D,
                    subtitle: globalDataCount + " 个可访问资产",
                    badge: "查看全部"));
                entries.Add(ESSearchDropdown.Entry.Separator());

                int recentCount = AddRecentAssetEntries(entries, "最近资产");
                if (recentCount > 0)
                {
                    entries.Add(ESSearchDropdown.Entry.Separator());
                }

                if (ESSceneGlobalData.Instance == null)
                {
                    entries.Add(ESSearchDropdown.Entry.Disabled("顶级工具栏管理面板数据未找到"));
                    entries.Add(ESSearchDropdown.Entry.Separator());
                    entries.Add(ESSearchDropdown.Entry.Item("打开顶级工具栏管理面板", OpenSceneManagerWindow, "操作"));
                }
                else
                {
                    var customAssets = ESSceneGlobalData.Instance.GetEnabledAssets();

                    if (customAssets.Count == 0)
                    {
                        entries.Add(ESSearchDropdown.Entry.Disabled("无自定义资产"));
                    }
                    else
                    {
                        // 按分组显示
                        var groups = customAssets.GroupBy(a => a.Group).OrderBy(g => g.Key);

                        foreach (var group in groups)
                        {
                            foreach (var asset in group)
                            {
                                Texture2D icon = asset.Asset != null ? GetAssetTypeIcon(asset.Asset) : null;
                                string assetPath = asset.Asset != null ? AssetDatabase.GetAssetPath(asset.Asset) : string.Empty;
                                entries.Add(ESSearchDropdown.Entry.Item(
                                    asset.DisplayName,
                                    () => PingAsset(asset.Asset),
                                    string.IsNullOrWhiteSpace(group.Key) ? "未分组" : group.Key,
                                    icon,
                                    subtitle: asset.Asset != null ? asset.Asset.GetType().Name + " · " + assetPath : "资产已丢失",
                                    badge: asset.Asset != null ? "快捷资产" : "缺失",
                                    selected: Selection.activeObject == asset.Asset));
                            }
                        }
                    }

                    entries.Add(ESSearchDropdown.Entry.Separator());
                    entries.Add(ESSearchDropdown.Entry.Item("添加当前选中资产", () =>
                    {
                        AddCurrentAssetToCustom();
                    }, "操作"));
                    entries.Add(ESSearchDropdown.Entry.Item("打开顶级工具栏管理面板", OpenSceneManagerWindow, "操作"));
                }

                ESSearchDropdown.Open(anchorRect, "快速访问资产", entries);
            }

            private static int AddGlobalDataEntries(List<ESSearchDropdown.Entry> entries)
            {
                IReadOnlyList<ESCommandPaletteItem> globalDataItems = GetGlobalDataQuickAccessItems();
                string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                Texture2D icon = EditorGUIUtility.IconContent("ScriptableObject Icon").image as Texture2D;

                for (int i = 0; i < globalDataItems.Count; i++)
                {
                    ESCommandPaletteItem item = globalDataItems[i];
                    string folderName = Path.GetFileName(Path.GetDirectoryName(item.TargetId));
                    string groupPath = string.IsNullOrWhiteSpace(folderName)
                        ? "GlobalData"
                        : "GlobalData/" + folderName;

                    entries.Add(ESSearchDropdown.Entry.Item(
                        item.Title,
                        () => ExecuteGlobalDataItem(item),
                        groupPath,
                        icon,
                        subtitle: item.TargetId,
                        tooltip: item.Description,
                        keywords: item.Keywords,
                        badge: "GlobalData",
                        selected: string.Equals(selectedPath, item.TargetId, StringComparison.Ordinal)));
                }

                return globalDataItems.Count;
            }

            internal static IReadOnlyList<ESCommandPaletteItem> GetGlobalDataQuickAccessItems()
            {
                IReadOnlyList<ESCommandPaletteItem> allItems = ESCommandPaletteRegistry.AllItems;
                var result = new List<ESCommandPaletteItem>();
                for (int i = 0; i < allItems.Count; i++)
                {
                    ESCommandPaletteItem item = allItems[i];
                    if (item == null
                        || item.ActionKind != ESCommandPaletteActionKind.OpenAsset
                        || !string.Equals(item.Category, "GlobalData", StringComparison.Ordinal)
                        || !ESCommandPalettePathPolicy.IsRegisteredGlobalData(item.TargetId))
                    {
                        continue;
                    }

                    result.Add(item);
                }

                result.Sort((left, right) =>
                {
                    int titleOrder = string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
                    return titleOrder != 0
                        ? titleOrder
                        : string.Compare(left.TargetId, right.TargetId, StringComparison.Ordinal);
                });
                return result;
            }

            private static void ExecuteGlobalDataItem(ESCommandPaletteItem item)
            {
                if (item == null)
                    return;

                ESEditorFeedbackSound.SuppressSelectionSound();
                ESCommandPaletteResult result = ESCommandPaletteExecutors.Execute(item);
                if (result.Success)
                {
                    ESCommandPaletteRegistry.RecordRecent(item.StableId);
                    RecordRecentAsset(item.TargetId);
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Open);
                    return;
                }

                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                string recovery = string.IsNullOrWhiteSpace(result.RecoveryAction)
                    ? string.Empty
                    : "\n\n恢复建议：" + result.RecoveryAction;
                Debug.LogWarning("[ESEditorToolBar] GlobalData 打开失败：" + result.Message + recovery);
                EditorUtility.DisplayDialog(
                    "GlobalData 打开失败",
                    result.Message + recovery,
                    "确定");
            }

            private static void AddCurrentSelectionEntry(List<ESSearchDropdown.Entry> entries)
            {
                UnityEngine.Object selected = Selection.activeObject;
                if (selected == null)
                    return;

                string path = AssetDatabase.GetAssetPath(selected);
                string subtitle = selected.GetType().Name;
                if (!string.IsNullOrEmpty(path))
                    subtitle += " · " + path;

                entries.Add(ESSearchDropdown.Entry.Item(
                    selected.name,
                    () => PingAsset(selected),
                    "当前选中",
                    GetAssetTypeIcon(selected),
                    subtitle: subtitle,
                    badge: "选中",
                    selected: true));
            }

            private static int AddRecentAssetEntries(List<ESSearchDropdown.Entry> entries, string group)
            {
                int count = 0;
                IReadOnlyList<string> paths = GetRecentAssetPaths();
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                        continue;

                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset == null)
                        continue;

                    entries.Add(ESSearchDropdown.Entry.Item(
                        asset.name,
                        () => PingAsset(asset),
                        group,
                        GetAssetTypeIcon(asset),
                        subtitle: path,
                        badge: "最近"));
                    count++;
                }

                return count;
            }

            private static void AddCommonRoleEntries(List<ESSearchDropdown.Entry> entries)
            {
                string[] templatePaths =
                {
                    "Assets/ESNormalAssets/CharacterTemplates/ES基础角色模板.prefab",
                    "Assets/ESNormalAssets/CharacterTemplates/ES通用角色完整架构.prefab",
                    "Assets/ESNormalAssets/CharacterVariants/大黑塔.prefab"
                };
                AddFixedAssetEntries(entries, "常用角色", templatePaths);

                AddTypeAssetEntries(entries, "ActorDataInfo", "DataInfo", 8);
                AddTypeAssetEntries(entries, "MonsterDataInfo", "DataInfo", 8);
                AddTypeAssetEntries(entries, "NpcDataInfo", "DataInfo", 8);
                AddTypeAssetEntries(entries, "ItemDataInfo", "DataInfo", 8);
            }

            private static void AddFixedAssetEntries(
                List<ESSearchDropdown.Entry> entries,
                string group,
                string[] paths)
            {
                if (paths == null)
                    return;

                for (int i = 0; i < paths.Length; i++)
                {
                    string path = paths[i];
                    if (string.IsNullOrWhiteSpace(path)
                        || !path.Replace('\\', '/').StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string normalizedPath = path.Replace('\\', '/');
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalizedPath);
                    if (asset == null)
                        continue;

                    entries.Add(ESSearchDropdown.Entry.Item(
                        asset.name,
                        () => PingAsset(asset),
                        group,
                        GetAssetTypeIcon(asset),
                        subtitle: normalizedPath,
                        badge: "常用"));
                }
            }

            private static void AddTypeAssetEntries(
                List<ESSearchDropdown.Entry> entries,
                string typeName,
                string group,
                int maximum)
            {
                if (string.IsNullOrWhiteSpace(typeName) || maximum <= 0)
                    return;

                string[] guids = AssetDatabase.FindAssets(
                    "t:" + typeName,
                    new[] { "Assets/ESNormalAssets/Data" });
                int added = 0;
                for (int i = 0; i < guids.Length && added < maximum; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset == null)
                        continue;

                    entries.Add(ESSearchDropdown.Entry.Item(
                        asset.name,
                        () => PingAsset(asset),
                        group,
                        GetAssetTypeIcon(asset),
                        subtitle: path,
                        badge: typeName));
                    added++;
                }
            }

            private static Texture2D GetAssetTypeIcon(UnityEngine.Object asset)
            {
                if (asset is GameObject)
                    return EditorGUIUtility.IconContent("GameObject Icon").image as Texture2D;
                if (asset is ScriptableObject)
                    return EditorGUIUtility.IconContent("ScriptableObject Icon").image as Texture2D;
                if (asset is Material)
                    return EditorGUIUtility.IconContent("Material Icon").image as Texture2D;
                if (asset is Texture)
                    return EditorGUIUtility.IconContent("Texture Icon").image as Texture2D;
                if (asset is SceneAsset)
                    return EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D;
                return EditorGUIUtility.IconContent("DefaultAsset Icon").image as Texture2D;
            }

            static void OnQuickSelectionToolbarGUI()
            {
                // 创建下拉菜单按钮
                if (EditorGUILayout.DropdownButton(
                    new GUIContent("快速定位", EditorGUIUtility.IconContent("d__Popup").image),
                    FocusType.Passive,
                    EditorStyles.toolbarDropDown, GUILayout.Width(100)))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                    Rect anchorRect = GUILayoutUtility.GetLastRect();
                    var entries = new List<ESSearchDropdown.Entry>();

                    AddCurrentSelectionEntry(entries);
                    int recentCount = AddRecentAssetEntries(entries, "最近资产");
                    if (recentCount > 0)
                    {
                        entries.Add(ESSearchDropdown.Entry.Separator());
                    }
                    AddCommonRoleEntries(entries);
                    if (entries.Count > 0)
                    {
                        entries.Add(ESSearchDropdown.Entry.Separator());
                    }

                    entries.Add(ESSearchDropdown.Entry.Item("玩家对象", () =>
                    {
                        var player = GameObject.FindGameObjectWithTag("Player");
                        if (player != null)
                        {
                            ESEditorFeedbackSound.SuppressSelectionSound();
                            Selection.activeGameObject = player;
                            EditorGUIUtility.PingObject(player);
                            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Locate);
                        }
                        else
                        {
                            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Warning);
                            Debug.LogWarning("未找到带有 'Player' 标签的对象");
                        }
                    }, "场景对象", EditorGUIUtility.IconContent("GameObject Icon").image as Texture2D,
                        subtitle: "查找 Tag=Player 的当前场景对象", badge: "GameObject"));

                    entries.Add(ESSearchDropdown.Entry.Separator());

                    // 添加ESGlobalEditorLocation中的资产
                    if (ESGlobalEditorLocation.Instance != null && ESGlobalEditorLocation.Instance.Assets.Count > 0)
                    {
                        foreach (var (k, v) in ESGlobalEditorLocation.Instance.Assets)
                        {
                            if (v != null)
                            {
                                entries.Add(ESSearchDropdown.Entry.Item(k, () => PingAsset(v), "全局定位资产", GetAssetTypeIcon(v),
                                    subtitle: v.GetType().Name + " · " + AssetDatabase.GetAssetPath(v),
                                    selected: Selection.activeObject == v));
                            }
                        }
                    }
                    else
                    {
                        entries.Add(ESSearchDropdown.Entry.Disabled("无快速定位资产"));
                    }

                    ESSearchDropdown.Open(anchorRect, "快速定位", entries);
                }
            }

            #region 辅助方法

            /// <summary>
            /// 打开场景（带错误处理）
            /// </summary>
            private static void OpenScene(string scenePath, bool additiveMode)
            {
                if (string.IsNullOrEmpty(scenePath))
                {
                    Debug.LogError("场景路径无效！");
                    return;
                }

                string canonicalScenePath = ResolveCanonicalScenePath(scenePath);
                if (string.IsNullOrEmpty(canonicalScenePath))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                    Debug.LogError($"场景资产无效或路径已过期: {scenePath}");
                    return;
                }

                scenePath = canonicalScenePath;

                UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
                if (!additiveMode
                    && string.Equals(activeScene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    PingSceneAsset(scenePath);
                    RecordRecentScene(scenePath);
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Locate);
                    return;
                }

                if (additiveMode)
                {
                    UnityEngine.SceneManagement.Scene loadedScene = EditorSceneManager.GetSceneByPath(scenePath);
                    if (loadedScene.IsValid() && loadedScene.isLoaded)
                    {
                        if (EditorSceneManager.SetActiveScene(loadedScene))
                        {
                            PingSceneAsset(scenePath);
                            RecordRecentScene(scenePath);
                            ESEditorFeedbackSoundHook.NotifySceneTransition(scenePath);
                            return;
                        }

                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                        Debug.LogWarning("[ESEditorToolBar] 已加载场景激活失败：" + scenePath);
                        return;
                    }
                }

                try
                {
                    Debug.Log($"正在打开场景: {scenePath}");

                    // 自动保存当前场景
                    bool autoSave = ESSceneGlobalData.Instance != null ? 
                        ESSceneGlobalData.Instance.AutoSaveBeforeSwitch : 
                        EditorPrefs.GetBool("ES_AutoSaveBeforeSwitch", true);

                    if (autoSave)
                    {
                        if (activeScene.isDirty)
                        {
                            bool saved = EditorSceneManager.SaveScene(activeScene);
                            Debug.Log($"自动保存场景 {activeScene.name} {(saved ? "成功" : "失败")}");
                            if (!saved)
                            {
                                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                                Debug.LogError("[ESEditorToolBar] 自动保存失败，已取消场景切换：" + scenePath);
                                return;
                            }
                        }
                    }

                    // Ping资产
                    PingSceneAsset(scenePath);

                    // 打开场景
                    OpenSceneMode mode = additiveMode ? OpenSceneMode.Additive : OpenSceneMode.Single;
                    EditorSceneManager.OpenScene(scenePath, mode);
                    RecordRecentScene(scenePath);
                    ESEditorFeedbackSoundHook.NotifySceneTransition(scenePath);
                    Debug.Log($"已{(additiveMode ? "叠加" : "")}打开场景: {Path.GetFileNameWithoutExtension(scenePath)}");
                }
                catch (Exception ex)
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                    Debug.LogError($"打开场景失败: {ex.Message}\n{ex.StackTrace}");
                    EditorUtility.DisplayDialog("错误", $"打开场景失败:\n{ex.Message}", "确定");
                }
            }

            private static string ResolveCanonicalScenePath(string candidatePath)
            {
                string normalized = candidatePath.Replace('\\', '/').Trim();
                if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                    || !normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    return null;

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(normalized);
                if (sceneAsset == null)
                    return null;

                string canonicalPath = AssetDatabase.GetAssetPath(sceneAsset);
                return string.IsNullOrEmpty(canonicalPath)
                    ? null
                    : canonicalPath.Replace('\\', '/');
            }

            private static void PingSceneAsset(string scenePath)
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenePath);
                if (sceneAsset != null)
                {
                    EditorGUIUtility.PingObject(sceneAsset);
                }
            }

            /// <summary>
            /// Ping资产
            /// </summary>
            private static void PingAsset(UnityEngine.Object asset)
            {
                if (asset != null)
                {
                    string path = AssetDatabase.GetAssetPath(asset);
                    if (!string.IsNullOrEmpty(path))
                    {
                        RecordRecentAsset(path);
                    }
                    ESEditorFeedbackSound.SuppressSelectionSound();
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Locate);
                }
                else
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Warning);
                    Debug.LogWarning("资产对象无效！");
                }
            }

            /// <summary>
            /// 获取叠加模式设置
            /// </summary>
            private static bool GetAdditiveMode()
            {
                if (ESSceneGlobalData.Instance != null)
                {
                    return ESSceneGlobalData.Instance.UseAdditiveMode;
                }
                return EditorPrefs.GetBool("ES_UseAdditiveMode", false);
            }

            /// <summary>
            /// 切换自动保存
            /// </summary>
            private static void ToggleAutoSave()
            {
                if (ESSceneGlobalData.Instance != null)
                {
                    Undo.RecordObject(ESSceneGlobalData.Instance, "切换场景自动保存");
                    ESSceneGlobalData.Instance.AutoSaveBeforeSwitch = !ESSceneGlobalData.Instance.AutoSaveBeforeSwitch;
                    EditorUtility.SetDirty(ESSceneGlobalData.Instance);
                    AssetDatabase.SaveAssetIfDirty(ESSceneGlobalData.Instance);
                    Debug.Log($"自动保存: {(ESSceneGlobalData.Instance.AutoSaveBeforeSwitch ? "开启" : "关闭")}");
                }
                else
                {
                    bool current = EditorPrefs.GetBool("ES_AutoSaveBeforeSwitch", true);
                    EditorPrefs.SetBool("ES_AutoSaveBeforeSwitch", !current);
                    Debug.Log($"自动保存: {(!current ? "开启" : "关闭")}");
                }

                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Confirm);
            }

            /// <summary>
            /// 切换叠加模式
            /// </summary>
            private static void ToggleAdditiveMode()
            {
                if (ESSceneGlobalData.Instance != null)
                {
                    Undo.RecordObject(ESSceneGlobalData.Instance, "切换场景叠加模式");
                    ESSceneGlobalData.Instance.UseAdditiveMode = !ESSceneGlobalData.Instance.UseAdditiveMode;
                    EditorUtility.SetDirty(ESSceneGlobalData.Instance);
                    AssetDatabase.SaveAssetIfDirty(ESSceneGlobalData.Instance);
                    Debug.Log($"叠加模式: {(ESSceneGlobalData.Instance.UseAdditiveMode ? "开启" : "关闭")}");
                }
                else
                {
                    bool current = EditorPrefs.GetBool("ES_UseAdditiveMode", false);
                    EditorPrefs.SetBool("ES_UseAdditiveMode", !current);
                    Debug.Log($"叠加模式: {(!current ? "开启" : "关闭")}");
                }

                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Confirm);
            }

            /// <summary>
            /// 添加当前场景到自定义列表
            /// </summary>
            private static void AddCurrentSceneToCustom()
            {
                try
                {
                    UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
                    if (string.IsNullOrEmpty(activeScene.path))
                    {
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                        EditorUtility.DisplayDialog("错误", "当前场景未保存，无法添加！", "确定");
                        return;
                    }

                    if (ESSceneGlobalData.Instance != null)
                    {
                        ESSceneGlobalData.Instance.AddScene(activeScene.path);
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Confirm);
                        EditorUtility.DisplayDialog("成功", $"已添加场景: {activeScene.name}", "确定");
                    }
                    else
                    {
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                        EditorUtility.DisplayDialog("错误", "顶级工具栏管理面板数据未找到！", "确定");
                    }
                }
                catch (Exception ex)
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                    Debug.LogError($"添加场景失败: {ex.Message}");
                    EditorUtility.DisplayDialog("错误", $"添加场景失败:\n{ex.Message}", "确定");
                }
            }

            /// <summary>
            /// 添加当前资产到自定义列表
            /// </summary>
            private static void AddCurrentAssetToCustom()
            {
                try
                {
                    if (Selection.activeObject == null)
                    {
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                        EditorUtility.DisplayDialog("错误", "请先选择一个资产！", "确定");
                        return;
                    }

                    if (ESSceneGlobalData.Instance != null)
                    {
                        string name = Selection.activeObject.name;
                        string group = "默认";

                        // 根据资产类型自动分组
                        if (Selection.activeObject is MonoScript)
                            group = "脚本";
                        else if (Selection.activeObject is GameObject)
                            group = "预制体";
                        else if (Selection.activeObject is Material)
                            group = "材质";
                        else if (Selection.activeObject is ScriptableObject)
                            group = "配置";

                        ESSceneGlobalData.Instance.AddAsset(name, Selection.activeObject, group);
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Confirm);
                        EditorUtility.DisplayDialog("成功", $"已添加资产: {name}", "确定");
                    }
                    else
                    {
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                        EditorUtility.DisplayDialog("错误", "顶级工具栏管理面板数据未找到！", "确定");
                    }
                }
                catch (Exception ex)
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                    Debug.LogError($"添加资产失败: {ex.Message}");
                    EditorUtility.DisplayDialog("错误", $"添加资产失败:\n{ex.Message}", "确定");
                }
            }

            /// <summary>
            /// 打开顶级工具栏管理面板
            /// </summary>
            private static void OpenSceneManagerWindow()
            {
                try
                {
                    SimpleToolsWindow.OpenWindow(SimpleToolsWindow.PageId_TopToolbar);
                }
                catch (Exception ex)
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                    Debug.LogError($"打开顶级工具栏管理面板失败: {ex.Message}");
                }
            }

            private static IReadOnlyList<string> GetRecentScenePaths()
            {
                string value = EditorPrefs.GetString(RecentScenesPrefsKey, string.Empty);
                if (string.IsNullOrWhiteSpace(value) || value.Length > MaxRecentPreferenceCharacters)
                    return Array.Empty<string>();

                return value.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaxRecentSceneCount)
                    .ToArray();
            }

            private static void RecordRecentScene(string scenePath)
            {
                if (string.IsNullOrWhiteSpace(scenePath))
                    return;

                var recent = new List<string> { scenePath };
                recent.AddRange(GetRecentScenePaths().Where(path =>
                    !string.Equals(path, scenePath, StringComparison.OrdinalIgnoreCase)));
                EditorPrefs.SetString(
                    RecentScenesPrefsKey,
                    string.Join("\n", recent.Take(MaxRecentSceneCount)));
            }

            private static IReadOnlyList<string> GetRecentAssetPaths()
            {
                string value = EditorPrefs.GetString(RecentAssetsPrefsKey, string.Empty);
                if (string.IsNullOrWhiteSpace(value) || value.Length > MaxRecentPreferenceCharacters)
                    return Array.Empty<string>();

                return value.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaxRecentAssetCount)
                    .ToArray();
            }

            private static void RecordRecentAsset(string assetPath)
            {
                if (string.IsNullOrWhiteSpace(assetPath)
                    || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
                    return;

                var recent = new List<string> { assetPath };
                recent.AddRange(GetRecentAssetPaths().Where(path =>
                    !string.Equals(path, assetPath, StringComparison.OrdinalIgnoreCase)));
                EditorPrefs.SetString(
                    RecentAssetsPrefsKey,
                    string.Join("\n", recent.Take(MaxRecentAssetCount)));
            }

            #endregion

        }

        /// <summary>
        /// ES 工具栏的唯一自动入口：通过程序集流完成轻量回调安装。
        /// 场景资产扫描改为用户打开“场景跳转”菜单时按需执行。
        /// </summary>
        public sealed class ESEditorToolBarAssemblyStreamInitializer : EditorInvoker_Level2
        {
        public override void InitInvoke()
        {
            CustomToolbarMenu.Install();
            ES.EditorInternal.ESEditorPresentation.InstallGlobalEditorAdapters();
        }
        }

        #region 自主扩展
        public enum ESEditorQuickSelectGroup
        {
            [ESMessage("【文件夹】")] Dir,
            [ESMessage("【资产】")] AssetObject,
            [ESMessage("【管理器】")] Manager,
            [ESMessage("【场景特殊物体】")] SceneGameObjectObject,

        }
        public abstract class ESEditorExpand_QuickSelect
        {
            public abstract ESEditorQuickSelectGroup GetGroup { get; }
            public abstract string MenuName { get; }
            public abstract Func<UnityEngine.Object> GetPingUnityObject();
            public static UnityEngine.Object Helper_GetFromTag(string tag)
            {
                return GameObject.FindGameObjectWithTag(tag);
            }
            public static UnityEngine.Object[] Helper_Get_S_FromTag(string tag)
            {
                return GameObject.FindGameObjectsWithTag(tag);
            }
            public static UnityEngine.Object Helper_GetFromCompo<T>() where T : Component
            {
                T t = UnityEngine.Object.FindAnyObjectByType<T>();
                if (t != null)
                {
                    return t.gameObject;
                }
                return null;
            }
            public static UnityEngine.Object[] Helper_Get_S_FromCompo<T>() where T : Component
            {
                var ts = UnityEngine.Object.FindObjectsByType<T>(sortMode: FindObjectsSortMode.None);
                return ts.Select((n) => n.gameObject).ToArray();
            }
            public static ScriptableObject Helper_GetSO<T>() where T : ScriptableObject
            {
                T t = UnityEngine.Object.FindAnyObjectByType<T>();
                return t;
            }
            public static ScriptableObject[] Helper_Get_S_SO<T>() where T : ScriptableObject
            {
                var ts = UnityEngine.Object.FindObjectsByType<T>(sortMode: FindObjectsSortMode.None);
                return ts;
            }

            public static UnityEngine.Object Helper_Asset_GetFromNameAndParent(string name, params string[] withparent)
            {
                string[] guids = AssetDatabase.FindAssets(name);
                foreach (var i in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(i);
                    var use = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    bool Cancel = false;
                    if (use != null)
                    {
                        if (withparent != null && withparent.Length != 0)
                        {
                            foreach (var p in withparent)
                            {
                                if (!path.Contains(p))
                                {
                                    Cancel = true;
                                    break;
                                }
                            }
                        }
                        else
                        {

                        }
                    }
                    else
                    {
                        continue;
                    }
                    if (Cancel) continue;
                    return use;
                }
                return name != "ESFramework" ? Helper_Asset_GetFromNameAndParent("ESFramework") : null;
            }

            public static string Name_ESFramework = "ESFramework";
        }
        #region 演示
        public class ESEditorExpand_QuickSelect_EntityDir : ESEditorExpand_QuickSelect
        {
            public override ESEditorQuickSelectGroup GetGroup => ESEditorQuickSelectGroup.Dir;
            public override string MenuName => "实体定义文件夹";
            public override Func<UnityEngine.Object> GetPingUnityObject()
            {
                return () => Helper_Asset_GetFromNameAndParent("Entity", Name_ESFramework);
            }
        }


        public class ESEditorExpand_QuickSelect_LinkDir : ESEditorExpand_QuickSelect
        {
            public override ESEditorQuickSelectGroup GetGroup => ESEditorQuickSelectGroup.Dir;
            public override string MenuName => "Link定义文件夹";
            public override Func<UnityEngine.Object> GetPingUnityObject()
            {
                return () => Helper_Asset_GetFromNameAndParent("Link", "Assets/Scripts/ESFramework/Interface_Abstract_Extension_Design/Link");
            }
        }

        public class ESEditorExpand_QuickSelect_SceneCamera : ESEditorExpand_QuickSelect
        {
            public override ESEditorQuickSelectGroup GetGroup => ESEditorQuickSelectGroup.SceneGameObjectObject;
            public override string MenuName => "主相机";
            public override Func<UnityEngine.Object> GetPingUnityObject()
            {
                return () => Helper_GetFromTag("MainCamera");
            }
        }
        #endregion


        #endregion
    }
}

