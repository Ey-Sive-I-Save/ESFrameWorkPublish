using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    /// <summary>
    /// ES集成工具集-顶部工具栏管理器
    /// </summary>
    [Serializable]
    public class Page_TopToolbar : ESWindowPageBase
    {
        // 分页设置
        [HideInInspector] public int assetPageIndex = 0;
        [HideInInspector] public int scenePageIndex = 0;
        [HideInInspector] public int selectedTab = 0;
        [HideInInspector] public const int ItemsPerPage = 10;

        [Title("顶部工具栏管理器", "ES场景快捷访问与管理工具", bold: true, titleAlignment: TitleAlignments.Centered)]
        [InfoBox("管理常用场景和资产快捷入口；配置写入 ESSceneGlobalData，支持 Undo。", InfoMessageType.Info)]

        [FoldoutGroup("功能列表", expanded: false)]
        [DisplayAsString(fontSize: 12), HideLabel]
        public string readMe = "功能列表:\n" +
            "● 自定义场景快捷方式\n" +
            "● 场景分组管理\n" +
            "● 资产快捷访问\n" +
            "● 资产分组管理\n" +
            "● 资产分组编辑\n" +
            "● 文件夹快捷访问支持\n" +
            "● Tab分页（场景/资产分离）\n" +
            "● 场景列表分页显示\n" +
            "● Build Settings场景同步\n" +
            "● 批量操作支持";

        private string lastResultSummary = "";
        private string lastResultDetail = "";

        [OnInspectorGUI]
        public void DrawThisWindow()
        {
            var dataInstance = ESSceneGlobalData.Instance;
            if (dataInstance == null)
            {
                EditorGUILayout.HelpBox("场景管理器数据未找到！请创建 ESSceneGlobalData 资产。", MessageType.Error);
                return;
            }

            // 使用TabGroup分离场景和资产管理
            EditorGUILayout.BeginVertical();
            selectedTab = GUILayout.Toolbar(selectedTab, new string[] { "场景管理", "资产管理" });
            DrawSummaryAndResult(dataInstance);

            // 撤销/重做按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("↶ 撤销 (Ctrl+Z)", GUILayout.Width(120), GUILayout.Height(25)))
            {
                Undo.PerformUndo();
                // 刷新窗口显示
                if (SimpleToolsWindow.UsingWindow != null)
                {
                    SimpleToolsWindow.UsingWindow.Repaint();
                }
            }
            if (GUILayout.Button("↷ 重做 (Ctrl+Y)", GUILayout.Width(120), GUILayout.Height(25)))
            {
                Undo.PerformRedo();
                // 刷新窗口显示
                if (SimpleToolsWindow.UsingWindow != null)
                {
                    SimpleToolsWindow.UsingWindow.Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (selectedTab == 0)
            {
                DrawSceneManagementSection();
            }
            else
            {
                DrawAssetManagementSection();
            }

            EditorGUILayout.Space(20);
            DrawQuickActionsSection();
            EditorGUILayout.EndVertical();
        }

        private void DrawSummaryAndResult(ESSceneGlobalData data)
        {
            int sceneCount = data.GetEnabledScenes().Count;
            int assetCount = data.GetEnabledAssets().Count;
            SimpleToolsPanelUtility.DrawSummary(
                $"场景快捷: {sceneCount} 个",
                $"资产快捷: {assetCount} 个",
                $"打开方式: {(data.UseAdditiveMode ? "叠加打开" : "单场景打开")}",
                $"切换前保存: {(data.AutoSaveBeforeSwitch ? "自动保存" : "手动确认")}");
            SimpleToolsPanelUtility.DrawResultSummary("最近场景/资产操作", lastResultSummary, lastResultDetail);
        }

        private void DrawSceneManagementSection()
        {
            EditorGUILayout.LabelField("场景管理", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 快速添加区域
            EditorGUILayout.LabelField("快速添加场景", EditorStyles.miniBoldLabel);

            // 拖拽区域 - 简化实现
            var dropRect = EditorGUILayout.GetControlRect(GUILayout.Height(40));
            GUI.Box(dropRect, "拖拽场景文件到此处添加", EditorStyles.helpBox);

            // 处理拖拽 - 只在必要时处理
            if (Event.current != null && dropRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    var scenePaths = new List<string>();
                    foreach (var draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is SceneAsset sceneAsset)
                        {
                            string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
                            if (!string.IsNullOrEmpty(scenePath) &&
                                !ESSceneGlobalData.Instance.CustomScenes.Exists(s => s.ScenePath == scenePath) &&
                                !scenePaths.Contains(scenePath))
                            {
                                scenePaths.Add(scenePath);
                            }
                        }
                    }

                    if (scenePaths.Count > 0)
                    {
                        string preview = SimpleToolsSafetyUtility.JoinPreview(scenePaths, 8);
                        if (EditorUtility.DisplayDialog("确认添加场景",
                            $"将添加 {scenePaths.Count} 个场景到快捷列表。\n\n{preview}\n\n支持 Ctrl+Z 撤销。继续吗？",
                            "添加", "取消"))
                        {
                            Undo.RegisterCompleteObjectUndo(ESSceneGlobalData.Instance, "拖拽添加快捷场景");
                            foreach (string scenePath in scenePaths)
                            {
                                ESSceneGlobalData.Instance.AddScene(scenePath);
                                Debug.Log($"已添加场景: {scenePath}");
                            }

                            EditorUtility.SetDirty(ESSceneGlobalData.Instance);
                            AssetDatabase.SaveAssets();
                            lastResultSummary = $"拖拽添加场景完成: 新增 {scenePaths.Count} 个";
                            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(scenePaths, 12);
                        }
                    }
                    Event.current.Use();
                }
            }

            // 快捷按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ 添加当前场景", GUILayout.Height(25)))
            {
                AddCurrentScene();
            }
            if (GUILayout.Button("🔄 从Build Settings同步", GUILayout.Height(25)))
            {
                SyncFromBuildSettings();
            }
            if (GUILayout.Button("📂 打开Project窗口", GUILayout.Height(25)))
            {
                EditorApplication.ExecuteMenuItem("Window/General/Project");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 自定义场景列表
            var data = ESSceneGlobalData.Instance;
            var allScenes = data.GetEnabledScenes();

            // 分页逻辑
            int totalScenes = allScenes.Count;
            int totalPages = Mathf.CeilToInt((float)totalScenes / ItemsPerPage);
            if (scenePageIndex >= totalPages) scenePageIndex = Mathf.Max(0, totalPages - 1);

            var scenes = allScenes.Skip(scenePageIndex * ItemsPerPage).Take(ItemsPerPage).ToList();

            // 分页控件
            if (totalPages > 1)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("◀", GUILayout.Width(30)))
                {
                    scenePageIndex = Mathf.Max(0, scenePageIndex - 1);
                }
                EditorGUILayout.LabelField($"页 {scenePageIndex + 1}/{totalPages} ({totalScenes} 个场景)", EditorStyles.centeredGreyMiniLabel);
                if (GUILayout.Button("▶", GUILayout.Width(30)))
                {
                    scenePageIndex = Mathf.Min(totalPages - 1, scenePageIndex + 1);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);
            }

            if (scenes.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无场景，请先添加场景", MessageType.Info);
            }
            else
            {
                // 按分组显示 - 简化布局
                var groups = scenes
                    .Where(s => !string.IsNullOrEmpty(s.Group))
                    .GroupBy(s => s.Group)
                    .OrderBy(g => g.Key);

                foreach (var group in groups)
                {
                    EditorGUILayout.LabelField($"【{group.Key}】 ({group.Count()}个场景)", EditorStyles.boldLabel);

                    foreach (var scene in group)
                    {
                        DrawSceneItem(scene);
                    }

                    EditorGUILayout.Space(5);
                }

                // 处理未分组的场景
                var ungroupedScenes = scenes.Where(s => string.IsNullOrEmpty(s.Group)).ToList();
                if (ungroupedScenes.Count > 0)
                {
                    EditorGUILayout.LabelField($"【未分组】 ({ungroupedScenes.Count}个场景)", EditorStyles.boldLabel);

                    foreach (var scene in ungroupedScenes)
                    {
                        DrawSceneItem(scene);
                    }

                    EditorGUILayout.Space(5);
                }
            }
        }

        /// <summary>
        /// 绘制单个场景项
        /// </summary>
        private void DrawSceneItem(SceneQuickAccessData scene)
        {
            EditorGUILayout.BeginHorizontal();

            // 颜色标记
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = scene.Color;

            // 确保显示名称不为空
            string displayName = string.IsNullOrEmpty(scene.DisplayName) ?
                System.IO.Path.GetFileNameWithoutExtension(scene.ScenePath) : scene.DisplayName;

            if (GUILayout.Button(displayName, GUILayout.Height(26), GUILayout.MinWidth(200)))
            {
                OpenScene(scene);
            }

            GUI.backgroundColor = originalColor;

            // Ping按钮
            if (GUILayout.Button("定位", GUILayout.Width(44), GUILayout.Height(26)))
            {
                PingSceneAsset(scene);
            }

            // 编辑按钮
            if (GUILayout.Button("编辑", GUILayout.Width(44), GUILayout.Height(26)))
            {
                EditSceneProperties(scene);
            }

            // 移除按钮
            if (GUILayout.Button("移除", GUILayout.Width(44), GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("确认", $"确定要移除场景 '{displayName}' 吗？", "确定", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(ESSceneGlobalData.Instance, "移除快捷场景");
                    ESSceneGlobalData.Instance.RemoveScene(scene.ScenePath);
                    EditorUtility.SetDirty(ESSceneGlobalData.Instance);
                    AssetDatabase.SaveAssets();
                    lastResultSummary = $"移除快捷场景完成: {displayName}";
                    lastResultDetail = scene.ScenePath;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (ESSceneGlobalData.Instance.ShowScenePath && !string.IsNullOrEmpty(scene.ScenePath))
            {
                EditorGUILayout.LabelField(scene.ScenePath, EditorStyles.miniLabel);
            }

            // 描述
            if (!string.IsNullOrEmpty(scene.Description))
            {
                EditorGUILayout.LabelField(scene.Description, EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(3);
        }

        /// <summary>
        /// 绘制资产管理区域
        /// </summary>
        private void DrawAssetManagementSection()
        {
            EditorGUILayout.LabelField("资产快捷访问", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);

            // 自定义资产列表
            var data = ESSceneGlobalData.Instance;
            var assets = data.GetEnabledAssets();

            // 按分组显示 - 完整分组功能
            var groups = assets
                .Where(a => !string.IsNullOrEmpty(a.Group))
                .GroupBy(a => a.Group)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                EditorGUILayout.LabelField($"【{group.Key}】 ({group.Count()}个资产)", EditorStyles.boldLabel);

                foreach (var asset in group)
                {
                    DrawAssetItem(asset);
                }

                EditorGUILayout.Space(5);
            }

            // 处理未分组的资产
            var ungroupedAssets = assets.Where(a => string.IsNullOrEmpty(a.Group)).ToList();
            if (ungroupedAssets.Count > 0)
            {
                EditorGUILayout.LabelField($"【未分组】 ({ungroupedAssets.Count}个资产)", EditorStyles.boldLabel);

                foreach (var asset in ungroupedAssets)
                {
                    DrawAssetItem(asset);
                }

                EditorGUILayout.Space(5);
            }

            // 添加资产按钮
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ 添加当前选中资产", GUILayout.Height(25)))
            {
                AddSelectedAsset();
            }
            if (GUILayout.Button("➕ 添加当前选中文件夹", GUILayout.Height(25)))
            {
                AddSelectedFolder();
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制单个资产项
        /// </summary>
        private void DrawAssetItem(AssetQuickAccessData asset)
        {
            EditorGUILayout.BeginHorizontal();

            // 检查是否是文件夹
            bool isFolder = asset.Asset != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(asset.Asset));
            string displayName = isFolder ? $"📁 {asset.DisplayName}" : asset.DisplayName;

            // 资产名称按钮
            if (GUILayout.Button(displayName, GUILayout.Height(25), GUILayout.MinWidth(200)))
            {
                PingAsset(asset);
            }

            // 编辑分组按钮
            if (GUILayout.Button("分组", GUILayout.Width(40), GUILayout.Height(25)))
            {
                EditAssetProperties(asset);
            }

            // 移除按钮
            if (GUILayout.Button("移除", GUILayout.Width(36), GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("确认", $"确定要移除资产 '{asset.DisplayName}' 吗？", "确定", "取消"))
                {
                    Undo.RegisterCompleteObjectUndo(ESSceneGlobalData.Instance, "移除快捷资产");
                    ESSceneGlobalData.Instance.RemoveAsset(asset.Asset);
                    EditorUtility.SetDirty(ESSceneGlobalData.Instance);
                    AssetDatabase.SaveAssets();
                    lastResultSummary = $"移除快捷资产完成: {asset.DisplayName}";
                    lastResultDetail = AssetDatabase.GetAssetPath(asset.Asset);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制快捷操作区域
        /// </summary>
        private void DrawQuickActionsSection()
        {
            EditorGUILayout.LabelField("快捷操作", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("打开数据配置", GUILayout.Height(30)))
            {
                Selection.activeObject = ESSceneGlobalData.Instance;
                EditorGUIUtility.PingObject(ESSceneGlobalData.Instance);
                lastResultSummary = "已定位数据配置";
                lastResultDetail = AssetDatabase.GetAssetPath(ESSceneGlobalData.Instance);
            }

            if (GUILayout.Button("刷新缓存", GUILayout.Height(30)))
            {
                AssetDatabase.Refresh();
                lastResultSummary = "AssetDatabase 刷新完成";
                lastResultDetail = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Debug.Log("缓存已刷新");
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 打开场景
        /// </summary>
        private void OpenScene(SceneQuickAccessData scene)
        {
            if (scene == null || scene.SceneAsset == null)
            {
                EditorUtility.DisplayDialog("场景资产无效", "这个快捷场景已经丢失或不是有效的场景资产。", "知道了");
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(scene.ScenePath) || !scene.ScenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    EditorUtility.DisplayDialog("场景路径无效", $"场景路径无效：\n{scene.ScenePath}", "知道了");
                    return;
                }

                OpenSceneMode mode = ESSceneGlobalData.Instance.UseAdditiveMode ? OpenSceneMode.Additive : OpenSceneMode.Single;

                // 自动保存当前场景
                if (ESSceneGlobalData.Instance.AutoSaveBeforeSwitch)
                {
                    Scene activeScene = EditorSceneManager.GetActiveScene();
                    if (activeScene.isDirty)
                    {
                        bool saved = EditorSceneManager.SaveScene(activeScene);
                        Debug.Log($"自动保存场景 {activeScene.name} {(saved ? "成功" : "失败")}");
                    }
                }
                else if (mode == OpenSceneMode.Single && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("[场景管理] 用户取消保存确认，已中止打开场景。");
                    return;
                }

                // 打开场景
                EditorSceneManager.OpenScene(scene.ScenePath, mode);
                lastResultSummary = $"打开场景完成: {scene.DisplayName}";
                lastResultDetail = $"路径: {scene.ScenePath}\n模式: {mode}";
                Debug.Log($"已打开场景: {scene.DisplayName}");
            }
            catch (Exception ex)
            {
                lastResultSummary = $"打开场景失败: {scene.DisplayName}";
                lastResultDetail = ex.Message;
                EditorUtility.DisplayDialog("打开场景失败", ex.Message, "知道了");
                Debug.LogError($"打开场景失败: {ex.Message}");
            }
        }

        /// <summary>
        /// Ping场景资产
        /// </summary>
        private void PingSceneAsset(SceneQuickAccessData scene)
        {
            if (scene?.SceneAsset != null)
            {
                Selection.activeObject = scene.SceneAsset;
                EditorGUIUtility.PingObject(scene.SceneAsset);
                lastResultSummary = $"已定位场景资产: {scene.DisplayName}";
                lastResultDetail = scene.ScenePath;
            }
        }

        /// <summary>
        /// Ping资产
        /// </summary>
        private void PingAsset(AssetQuickAccessData asset)
        {
            if (asset?.Asset != null)
            {
                Selection.activeObject = asset.Asset;
                EditorGUIUtility.PingObject(asset.Asset);

                // 如果是文件夹，额外打开Project窗口
                string assetPath = AssetDatabase.GetAssetPath(asset.Asset);
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    EditorApplication.ExecuteMenuItem("Window/General/Project");
                }
                lastResultSummary = $"已定位快捷资产: {asset.DisplayName}";
                lastResultDetail = assetPath;
            }
        }

        /// <summary>
        /// 添加当前场景
        /// </summary>
        private void AddCurrentScene()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.path))
            {
                EditorUtility.DisplayDialog("错误", "当前场景未保存，无法添加！", "确定");
                return;
            }

            if (ESSceneGlobalData.Instance.CustomScenes.Exists(s => string.Equals(s.ScenePath, activeScene.path, StringComparison.OrdinalIgnoreCase)))
            {
                EditorUtility.DisplayDialog("场景已存在", "当前场景已经在快捷场景列表里。", "知道了");
                return;
            }

            Undo.RegisterCompleteObjectUndo(ESSceneGlobalData.Instance, "添加当前场景");
            ESSceneGlobalData.Instance.AddScene(activeScene.path);
            EditorUtility.SetDirty(ESSceneGlobalData.Instance);
            AssetDatabase.SaveAssets();
            lastResultSummary = $"添加当前场景完成: {activeScene.name}";
            lastResultDetail = activeScene.path;
        }

        /// <summary>
        /// 从Build Settings同步
        /// </summary>
        private void SyncFromBuildSettings()
        {
            var buildScenes = EditorBuildSettings.scenes;
            var scenesToAdd = new List<string>();

            foreach (var buildScene in buildScenes)
            {
                if (buildScene.enabled && !string.IsNullOrEmpty(buildScene.path))
                {
                    // 检查是否已存在
                    if (!ESSceneGlobalData.Instance.CustomScenes.Exists(s => s.ScenePath == buildScene.path))
                    {
                        scenesToAdd.Add(buildScene.path);
                    }
                }
            }

            if (scenesToAdd.Count == 0)
            {
                EditorUtility.DisplayDialog("同步完成", "Build Settings 中没有需要新增的场景。", "确定");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(scenesToAdd, 10);
            if (!EditorUtility.DisplayDialog("确认同步Build Settings",
                $"将向场景快捷数据中新增 {scenesToAdd.Count} 个场景。\n\n{preview}\n\n会修改 ESSceneGlobalData，支持 Ctrl+Z 撤销。继续吗？",
                "开始同步", "取消"))
                return;

            Undo.RegisterCompleteObjectUndo(ESSceneGlobalData.Instance, "从Build Settings同步场景");
            foreach (var scenePath in scenesToAdd)
            {
                ESSceneGlobalData.Instance.AddScene(scenePath);
                EditorUtility.SetDirty(ESSceneGlobalData.Instance);
            }

            if (scenesToAdd.Count > 0)
                AssetDatabase.SaveAssets();

            lastResultSummary = $"Build Settings 同步完成: 新增 {scenesToAdd.Count} 个场景";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(scenesToAdd, 12);
            EditorUtility.DisplayDialog("同步完成", $"从Build Settings同步了 {scenesToAdd.Count} 个场景。", "确定");
        }

        /// <summary>
        /// 添加选中的资产
        /// </summary>
        private void AddSelectedAsset()
        {
            if (Selection.activeObject == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择一个资产！", "确定");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("不能添加场景对象", "请在 Project 窗口选择项目资产。Hierarchy 中的场景对象不能作为持久快捷资产保存。", "知道了");
                return;
            }

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                EditorUtility.DisplayDialog("请选择添加文件夹", "当前选择的是文件夹，请使用“添加当前选中文件夹”按钮。", "知道了");
                return;
            }

            if (ESSceneGlobalData.Instance.CustomAssets.Exists(a => a.Asset == Selection.activeObject))
            {
                EditorUtility.DisplayDialog("资产已存在", "当前资产已经在快捷资产列表里。", "知道了");
                return;
            }

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

            Undo.RegisterCompleteObjectUndo(ESSceneGlobalData.Instance, "添加快捷资产");
            ESSceneGlobalData.Instance.AddAsset(name, Selection.activeObject, group);
            EditorUtility.SetDirty(ESSceneGlobalData.Instance);
            AssetDatabase.SaveAssets();
            lastResultSummary = $"添加快捷资产完成: {name}";
            lastResultDetail = $"分组: {group}\n路径: {assetPath}";
        }

        private void AddSelectedFolder()
        {
            if (Selection.activeObject == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择一个文件夹！", "确定");
                return;
            }

            // 检查是否是文件夹
            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!AssetDatabase.IsValidFolder(assetPath))
            {
                EditorUtility.DisplayDialog("错误", "请选择一个文件夹！", "确定");
                return;
            }

            if (ESSceneGlobalData.Instance.CustomAssets.Exists(a => a.Asset == Selection.activeObject))
            {
                EditorUtility.DisplayDialog("文件夹已存在", "当前文件夹已经在快捷资产列表里。", "知道了");
                return;
            }

            string name = Selection.activeObject.name;
            string group = "文件夹";

            Undo.RegisterCompleteObjectUndo(ESSceneGlobalData.Instance, "添加快捷文件夹");
            ESSceneGlobalData.Instance.AddAsset($"📁 {name}", Selection.activeObject, group);
            EditorUtility.SetDirty(ESSceneGlobalData.Instance);
            AssetDatabase.SaveAssets();
            lastResultSummary = $"添加快捷文件夹完成: {name}";
            lastResultDetail = assetPath;
        }

        /// <summary>
        /// 编辑场景属性
        /// </summary>
        private void EditSceneProperties(SceneQuickAccessData scene)
        {
           
            // 简单的属性编辑对话框
            int result = EditorUtility.DisplayDialogComplex(
                "编辑场景属性",
                $"当前场景: {scene.DisplayName}\n分组: {scene.Group}",
                "重命名",
                "更改分组",
                "取消"
            );

            if (result == 0) // 重命名
            {
                string newDisplayName = EditorInputDialog.Show("重命名场景", "输入新名称:", scene.DisplayName);
                if (!string.IsNullOrEmpty(newDisplayName) && newDisplayName != scene.DisplayName)
                {
                    // 记录撤销操作
                    Undo.RegisterCompleteObjectUndo(ESSceneGlobalData.Instance, "重命名场景");
                    Undo.SetCurrentGroupName("场景管理操作");

                    scene.DisplayName = newDisplayName;
                    EditorUtility.SetDirty(ESSceneGlobalData.Instance);
                    AssetDatabase.SaveAssets();
                    lastResultSummary = $"重命名快捷场景完成: {newDisplayName}";
                    lastResultDetail = scene.ScenePath;
                }
            }
            else if (result == 1) // 更改分组
            {
                var groups = ESSceneGlobalData.Instance.SceneGroups;
                if (groups.Count > 0)
                {
                    GenericMenu menu = new GenericMenu();
                    foreach (var group in groups)
                    {
                        menu.AddItem(new GUIContent(group), scene.Group == group, () =>
                        {
                            // 记录撤销操作
                            Undo.RegisterCompleteObjectUndo(ESSceneGlobalData.Instance, "更改场景分组");
                            Undo.SetCurrentGroupName("场景管理操作");

                            scene.Group = group;
                            EditorUtility.SetDirty(ESSceneGlobalData.Instance);
                            AssetDatabase.SaveAssets();
                            lastResultSummary = $"修改场景分组完成: {scene.DisplayName}";
                            lastResultDetail = $"分组: {group}\n路径: {scene.ScenePath}";
                        });
                    }
                    menu.ShowAsContext();
                }
            }
        }

        /// <summary>
        /// 编辑资产属性
        /// </summary>
        private void EditAssetProperties(AssetQuickAccessData asset)
        {
            // 简单的属性编辑对话框
            int result = EditorUtility.DisplayDialogComplex(
                "编辑资产属性",
                $"当前资产: {asset.DisplayName}\n分组: {asset.Group}",
                "重命名",
                "更改分组",
                "取消"
            );

            if (result == 0) // 重命名
            {
                string newDisplayName = EditorInputDialog.Show("重命名资产", "输入新名称:", asset.DisplayName);
                if (!string.IsNullOrEmpty(newDisplayName) && newDisplayName != asset.DisplayName)
                {
                    // 记录撤销操作
                    Undo.RegisterCompleteObjectUndo(ESSceneGlobalData.Instance, "重命名资产");
                    Undo.SetCurrentGroupName("资产管理操作");

                    asset.DisplayName = newDisplayName;
                    EditorUtility.SetDirty(ESSceneGlobalData.Instance);
                    AssetDatabase.SaveAssets();
                    lastResultSummary = $"重命名快捷资产完成: {newDisplayName}";
                    lastResultDetail = AssetDatabase.GetAssetPath(asset.Asset);
                }
            }
            else if (result == 1) // 更改分组
            {
                var groups = ESSceneGlobalData.Instance.AssetGroups;
                if (groups.Count > 0)
                {
                    GenericMenu menu = new GenericMenu();
                    foreach (var group in groups)
                    {
                        menu.AddItem(new GUIContent(group), asset.Group == group, () =>
                        {
                            // 记录撤销操作
                            Undo.RegisterCompleteObjectUndo(ESSceneGlobalData.Instance, "更改资产分组");
                            Undo.SetCurrentGroupName("资产管理操作");

                            asset.Group = group;
                            EditorUtility.SetDirty(ESSceneGlobalData.Instance);
                            AssetDatabase.SaveAssets();
                            lastResultSummary = $"修改资产分组完成: {asset.DisplayName}";
                            lastResultDetail = $"分组: {group}\n路径: {AssetDatabase.GetAssetPath(asset.Asset)}";
                        });
                    }
                    menu.ShowAsContext();
                }
            }
        }
    }
}
