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
                ToolbarExtender.LeftToolbarGUI.Remove(OnCmdAgentToolbarGUI);
                ToolbarExtender.LeftToolbarGUI.Add(OnQuickSelectionToolbarGUI);
                ToolbarExtender.LeftToolbarGUI.Add(OnAssetQuickAccessToolbarGUI);
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
                    scenesCached = true;
                }
                catch (Exception ex)
                {
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
                    if (string.IsNullOrWhiteSpace(recentPath) || !File.Exists(recentPath))
                        continue;

                    entries.Add(CreateSceneEntry(recentPath, "最近打开", activeScenePath));
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

                foreach (string scenePath in cachedAllScenes.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
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
                            Selection.activeObject = ESSceneGlobalData.Instance;
                            EditorGUIUtility.PingObject(ESSceneGlobalData.Instance);
                        }
                    });

                    menu.AddSeparator("");

                    menu.AddItem(new GUIContent("刷新场景缓存"), false, () =>
                    {
                        scenesCached = false;
                        CacheScenes();
                        Debug.Log("场景缓存已刷新");
                    });

                    menu.ShowAsContext();
                }
            }

            /// <summary>
            /// 资产快捷访问工具栏GUI
            /// </summary>
            static void OnCmdAgentToolbarGUI()
            {
                if (GUILayout.Button(new GUIContent("Agent", "打开【ES】Cmd Agent：后台 CMD/Codex 中转，并按配置自动恢复最近会话"), EditorStyles.toolbarButton, GUILayout.Width(56)))
                    ESCmdAgentWindow.OpenAndResume();
            }
            static void OnAssetQuickAccessToolbarGUI()
            {
                if (EditorGUILayout.DropdownButton(
                    new GUIContent("资产快捷访问", EditorGUIUtility.IconContent("d__Popup").image),
                    FocusType.Passive,
                    EditorStyles.toolbarDropDown, GUILayout.Width(120)))
                {
                    ShowAssetQuickAccessMenu(GUILayoutUtility.GetLastRect());
                }
            }

            /// <summary>
            /// 显示资产快捷访问菜单
            /// </summary>
            private static void ShowAssetQuickAccessMenu(Rect anchorRect)
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
                                Texture2D icon = asset.Asset != null ? AssetPreview.GetMiniThumbnail(asset.Asset) : null;
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

            static void OnQuickSelectionToolbarGUI()
            {
                // 创建下拉菜单按钮
                if (EditorGUILayout.DropdownButton(
                    new GUIContent("快速定位", EditorGUIUtility.IconContent("d__Popup").image),
                    FocusType.Passive,
                    EditorStyles.toolbarDropDown, GUILayout.Width(100)))
                {
                    Rect anchorRect = GUILayoutUtility.GetLastRect();
                    var entries = new List<ESSearchDropdown.Entry>();

                    entries.Add(ESSearchDropdown.Entry.Item("玩家对象", () =>
                    {
                        var player = GameObject.FindGameObjectWithTag("Player");
                        if (player != null)
                        {
                            Selection.activeGameObject = player;
                            EditorGUIUtility.PingObject(player);
                        }
                        else
                        {
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
                                entries.Add(ESSearchDropdown.Entry.Item(k, () =>
                                { 
                                    Selection.activeObject = v; 
                                    EditorGUIUtility.PingObject(v); 
                                }, "全局定位资产", AssetPreview.GetMiniThumbnail(v),
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

                if (!System.IO.File.Exists(scenePath))
                {
                    Debug.LogError($"场景文件不存在: {scenePath}");
                    return;
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
                        UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
                        if (activeScene.isDirty)
                        {
                            bool saved = EditorSceneManager.SaveScene(activeScene);
                            Debug.Log($"自动保存场景 {activeScene.name} {(saved ? "成功" : "失败")}");
                        }
                    }

                    // Ping资产
                    var sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenePath);
                    if (sceneAsset != null)
                    {
                        EditorGUIUtility.PingObject(sceneAsset);
                    }

                    // 打开场景
                    OpenSceneMode mode = additiveMode ? OpenSceneMode.Additive : OpenSceneMode.Single;
                    EditorSceneManager.OpenScene(scenePath, mode);
                    RecordRecentScene(scenePath);
                    Debug.Log($"已{(additiveMode ? "叠加" : "")}打开场景: {Path.GetFileNameWithoutExtension(scenePath)}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"打开场景失败: {ex.Message}\n{ex.StackTrace}");
                    EditorUtility.DisplayDialog("错误", $"打开场景失败:\n{ex.Message}", "确定");
                }
            }

            /// <summary>
            /// Ping资产
            /// </summary>
            private static void PingAsset(UnityEngine.Object asset)
            {
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
                else
                {
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
                    ESSceneGlobalData.Instance.AutoSaveBeforeSwitch = !ESSceneGlobalData.Instance.AutoSaveBeforeSwitch;
                    EditorUtility.SetDirty(ESSceneGlobalData.Instance);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"自动保存: {(ESSceneGlobalData.Instance.AutoSaveBeforeSwitch ? "开启" : "关闭")}");
                }
                else
                {
                    bool current = EditorPrefs.GetBool("ES_AutoSaveBeforeSwitch", true);
                    EditorPrefs.SetBool("ES_AutoSaveBeforeSwitch", !current);
                    Debug.Log($"自动保存: {(!current ? "开启" : "关闭")}");
                }
            }

            /// <summary>
            /// 切换叠加模式
            /// </summary>
            private static void ToggleAdditiveMode()
            {
                if (ESSceneGlobalData.Instance != null)
                {
                    ESSceneGlobalData.Instance.UseAdditiveMode = !ESSceneGlobalData.Instance.UseAdditiveMode;
                    EditorUtility.SetDirty(ESSceneGlobalData.Instance);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"叠加模式: {(ESSceneGlobalData.Instance.UseAdditiveMode ? "开启" : "关闭")}");
                }
                else
                {
                    bool current = EditorPrefs.GetBool("ES_UseAdditiveMode", false);
                    EditorPrefs.SetBool("ES_UseAdditiveMode", !current);
                    Debug.Log($"叠加模式: {(!current ? "开启" : "关闭")}");
                }
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
                        EditorUtility.DisplayDialog("错误", "当前场景未保存，无法添加！", "确定");
                        return;
                    }

                    if (ESSceneGlobalData.Instance != null)
                    {
                        ESSceneGlobalData.Instance.AddScene(activeScene.path);
                        EditorUtility.DisplayDialog("成功", $"已添加场景: {activeScene.name}", "确定");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "顶级工具栏管理面板数据未找到！", "确定");
                    }
                }
                catch (Exception ex)
                {
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
                        EditorUtility.DisplayDialog("成功", $"已添加资产: {name}", "确定");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "顶级工具栏管理面板数据未找到！", "确定");
                    }
                }
                catch (Exception ex)
                {
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
                    // 使用GetWindow方法获取或创建SimpleToolsWindow实例
                    SimpleToolsWindow simpleToolsWindow = SimpleToolsWindow.GetWindow<SimpleToolsWindow>();
                    simpleToolsWindow.Focus();
                    SimpleToolsWindow.UsingWindow = simpleToolsWindow;

                    // 延迟执行，确保窗口和菜单树完全初始化
                    QueueSelectTopToolbarPage();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"打开顶级工具栏管理面板失败: {ex.Message}");
                }
            }

            /// <summary>
            /// 选择顶部工具栏页面
            /// </summary>
            private static void QueueSelectTopToolbarPage()
            {
                EditorApplication.delayCall -= SelectTopToolbarPage;
                EditorApplication.delayCall += SelectTopToolbarPage;
            }

            private static IReadOnlyList<string> GetRecentScenePaths()
            {
                string value = EditorPrefs.GetString(RecentScenesPrefsKey, string.Empty);
                if (string.IsNullOrWhiteSpace(value))
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

            private static void SelectTopToolbarPage()
            {
                try
                {
                    if (SimpleToolsWindow.UsingWindow == null)
                    {
                        return;
                    }

                    // 如果MenuTree为null，尝试强制重建
                    if (SimpleToolsWindow.UsingWindow.MenuTree == null)
                    {
                        SimpleToolsWindow.UsingWindow.ForceMenuTreeRebuild();

                        // 再次延迟检查
                        QueueSelectTopToolbarPage();
                        return;
                    }

                    if (SimpleToolsWindow.MenuItems == null || SimpleToolsWindow.MenuItems.Count == 0)
                    {
                        // 再次延迟重试
                        QueueSelectTopToolbarPage();
                        return;
                    }

                    string menuPath = "【ES集成工具集】/顶部工具栏";

                    if (SimpleToolsWindow.MenuItems.TryGetValue(menuPath, out var menuItem))
                    {
                        SimpleToolsWindow.UsingWindow.MenuTree.Selection.Clear();
                        SimpleToolsWindow.UsingWindow.MenuTree.Selection.Add(menuItem);
                        SimpleToolsWindow.UsingWindow.Repaint();
                    }
                    else
                    {
                        // 最后一次尝试：强制刷新窗口
                        SimpleToolsWindow.UsingWindow.ESWindow_RefreshWindow();
                        EditorApplication.delayCall -= SelectTopToolbarPageAfterRefresh;
                        EditorApplication.delayCall += SelectTopToolbarPageAfterRefresh;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"选择顶部工具栏页面失败: {ex.Message}");
                }
            }

            private static void SelectTopToolbarPageAfterRefresh()
            {
                const string menuPath = "【ES集成工具集】/顶部工具栏";
                if (SimpleToolsWindow.UsingWindow == null || SimpleToolsWindow.UsingWindow.MenuTree == null || SimpleToolsWindow.MenuItems == null)
                    return;

                if (!SimpleToolsWindow.MenuItems.TryGetValue(menuPath, out var item))
                    return;

                SimpleToolsWindow.UsingWindow.MenuTree.Selection.Clear();
                SimpleToolsWindow.UsingWindow.MenuTree.Selection.Add(item);
                SimpleToolsWindow.UsingWindow.Repaint();
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

