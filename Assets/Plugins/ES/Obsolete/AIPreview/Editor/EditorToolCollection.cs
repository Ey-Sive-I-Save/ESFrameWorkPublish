using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace ES.Obsolete.Preview.Editor
{
    /// <summary>
    /// 通用工具脚本集合
    /// 
    /// **功能列表**：
    /// - Hierarchy右键快捷菜单（快速创建Module/Hosting）
    /// - Scene视图绘制工具（Link消息流可视化）
    /// - 性能分析器（Module更新耗时）
    /// - 资源完整性检查器（ResLibrary验证）
    /// - 批量重命名工具
    /// </summary>
    
    #region Hierarchy Shortcuts
    
    public static class HierarchyShortcuts
    {
        /// <summary>
        /// 在Hierarchy右键菜单添加"Create ES Module"
        /// </summary>
        [MenuItem("GameObject/ES Framework/Create Module Hosting", false, 0)]
        private static void CreateModuleHosting()
        {
            GameObject go = new GameObject("ModuleHosting");
            
            // 添加示例Hosting组件
            var hosting = go.AddComponent<ExampleHosting>();
            
            // 选中新对象
            Selection.activeGameObject = go;
            
            // 标记场景为脏
            EditorUtility.SetDirty(go);
            
            Debug.Log("Created Module Hosting GameObject");
        }
        
        [MenuItem("GameObject/ES Framework/Create UI Panel", false, 1)]
        private static void CreateUIPanel()
        {
            GameObject go = new GameObject("UIPanel");
            
            // 添加RectTransform
            go.AddComponent<RectTransform>();
            
            // 添加Canvas组件（如果需要）
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            go.AddComponent<UnityEngine.UI.CanvasScaler>();
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            Selection.activeGameObject = go;
            Debug.Log("Created UI Panel GameObject");
        }
    }
    
    /// <summary>
    /// 示例Hosting组件（实际应替换为真实类型）
    /// </summary>
    public class ExampleHosting : MonoBehaviour
    {
        // 占位实现
    }
    
    #endregion
    
    #region Scene View Drawing
    
    /// <summary>
    /// Scene视图调试绘制工具
    /// </summary>
    public class SceneViewDebugDrawer
    {
        private static bool enableLinkVisualization = false;
        private static bool enableModuleBounds = false;
        private static bool registered;
        
        [MenuItem("ES/Obsolete/Toggle Scene View Debug Drawer")]
        private static void ToggleSceneViewDebugDrawer()
        {
            if (registered)
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                registered = false;
                Debug.Log("[Obsolete AIPreview] Scene View Debug Drawer disabled.");
                return;
            }

            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            registered = true;
            Debug.Log("[Obsolete AIPreview] Scene View Debug Drawer enabled.");
        }
        
        private static void OnSceneGUI(SceneView sceneView)
        {
            Handles.BeginGUI();
            {
                GUILayout.BeginArea(new Rect(10, 10, 250, 150));
                {
                    GUILayout.BeginVertical("box");
                    {
                        GUILayout.Label("ES Debug Tools", EditorStyles.boldLabel);
                        
                        enableLinkVisualization = GUILayout.Toggle(enableLinkVisualization, "Show Link Messages");
                        enableModuleBounds = GUILayout.Toggle(enableModuleBounds, "Show Module Bounds");
                        
                        if (GUILayout.Button("Clear Console"))
                        {
                            ClearConsole();
                        }
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndArea();
            }
            Handles.EndGUI();
            
            // 绘制调试信息
            if (enableLinkVisualization)
            {
                DrawLinkVisualization();
            }
            
            if (enableModuleBounds)
            {
                DrawModuleBounds();
            }
        }
        
        private static void DrawLinkVisualization()
        {
            // TODO: 连接Link系统，显示消息流向
            Handles.color = Color.cyan;
            
            // 示例：绘制箭头表示消息流
            var allObjects = GameObject.FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name.Contains("Module"))
                {
                    Handles.Label(obj.transform.position + Vector3.up * 2, $"📡 {obj.name}");
                }
            }
        }
        
        private static void DrawModuleBounds()
        {
            Handles.color = Color.green;
            
            var allModules = GameObject.FindObjectsOfType<MonoBehaviour>();
            foreach (var module in allModules)
            {
                // 检查是否是ES Module
                if (module.GetType().GetInterface("IESModule") != null)
                {
                    var bounds = new Bounds(module.transform.position, Vector3.one * 2);
                    Handles.DrawWireCube(bounds.center, bounds.size);
                }
            }
        }
        
        private static void ClearConsole()
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(SceneView));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
        }
    }
    
    #endregion
    
    #region Performance Profiler
    
    /// <summary>
    /// Module性能分析器窗口
    /// </summary>
    public class ModulePerformanceProfiler : EditorWindow
    {
        [MenuItem("ES/Tools/Module Performance Profiler")]
        public static void ShowWindow()
        {
            GetWindow<ModulePerformanceProfiler>("Module Profiler");
        }
        
        private Vector2 scrollPos;
        private Dictionary<string, ModuleProfile> profiles = new();
        private bool isRecording = false;
        
        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (GUILayout.Button(isRecording ? "⏸ Stop" : "▶ Start", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    isRecording = !isRecording;
                    if (isRecording)
                    {
                        StartRecording();
                    }
                }
                
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    profiles.Clear();
                }
                
                GUILayout.FlexibleSpace();
                
                GUILayout.Label($"Recording: {isRecording}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
            
            // 显示性能数据
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            {
                EditorGUILayout.LabelField("Module Performance", EditorStyles.boldLabel);
                
                foreach (var kvp in profiles)
                {
                    DrawModuleProfile(kvp.Key, kvp.Value);
                }
            }
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawModuleProfile(string moduleName, ModuleProfile profile)
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField(moduleName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Avg Update Time: {profile.avgUpdateTime:F2}ms");
                EditorGUILayout.LabelField($"Max Update Time: {profile.maxUpdateTime:F2}ms");
                EditorGUILayout.LabelField($"Call Count: {profile.callCount}");
                
                // 性能条
                Rect rect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(rect, profile.avgUpdateTime / 16.67f, $"{profile.avgUpdateTime:F2}ms / 16.67ms");
            }
            EditorGUILayout.EndVertical();
        }
        
        private void StartRecording()
        {
            profiles.Clear();
            
            // TODO: 连接真实的Module系统
            EditorApplication.update += OnEditorUpdate;
        }
        
        private void OnEditorUpdate()
        {
            if (!isRecording)
            {
                EditorApplication.update -= OnEditorUpdate;
                return;
            }
            
            // 模拟数据采集
            // TODO: 实际应通过反射或事件获取Module性能数据
            if (profiles.Count < 5)
            {
                profiles[$"Module_{profiles.Count}"] = new ModuleProfile
                {
                    avgUpdateTime = Random.Range(0.1f, 5f),
                    maxUpdateTime = Random.Range(5f, 15f),
                    callCount = Random.Range(100, 1000)
                };
            }
            
            Repaint();
        }
        
        private class ModuleProfile
        {
            public float avgUpdateTime;
            public float maxUpdateTime;
            public int callCount;
        }
    }
    
    #endregion
    
    #region Asset Integrity Checker
    
    /// <summary>
    /// 资源完整性检查器
    /// </summary>
    public class AssetIntegrityChecker : EditorWindow
    {
        [MenuItem("ES/Tools/Asset Integrity Checker")]
        public static void ShowWindow()
        {
            GetWindow<AssetIntegrityChecker>("Asset Checker");
        }
        
        private Vector2 scrollPos;
        private List<string> issues = new();
        
        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (GUILayout.Button("🔍 Check Assets", EditorStyles.toolbarButton))
                {
                    CheckAllAssets();
                }
                
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                {
                    issues.Clear();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            {
                if (issues.Count == 0)
                {
                    EditorGUILayout.HelpBox("No issues found. Click 'Check Assets' to scan.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox($"Found {issues.Count} issues:", MessageType.Warning);
                    
                    foreach (var issue in issues)
                    {
                        EditorGUILayout.LabelField("• " + issue, EditorStyles.wordWrappedLabel);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
        
        private void CheckAllAssets()
        {
            issues.Clear();
            
            // 1. 检查缺失的脚本引用
            CheckMissingScripts();
            
            // 2. 检查空的Prefab引用
            CheckEmptyPrefabs();
            
            // 3. 检查ResLibrary完整性
            CheckResLibraries();
            
            Debug.Log($"Asset check complete. Found {issues.Count} issues.");
        }
        
        private void CheckMissingScripts()
        {
            var allPrefabs = AssetDatabase.FindAssets("t:Prefab");
            
            foreach (var guid in allPrefabs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                var components = prefab.GetComponentsInChildren<Component>(true);
                foreach (var component in components)
                {
                    if (component == null)
                    {
                        issues.Add($"Missing script in: {path}");
                    }
                }
            }
        }
        
        private void CheckEmptyPrefabs()
        {
            var allPrefabs = AssetDatabase.FindAssets("t:Prefab");
            
            foreach (var guid in allPrefabs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab.transform.childCount == 0 && prefab.GetComponents<Component>().Length == 1)
                {
                    issues.Add($"Empty prefab: {path}");
                }
            }
        }
        
        private void CheckResLibraries()
        {
            // TODO: 扫描所有ResLibrary并检查缺失的Asset引用
            issues.Add("ResLibrary check not yet implemented");
        }
    }
    
    #endregion
    
    #region Batch Rename Tool
    
    /// <summary>
    /// 批量重命名工具
    /// </summary>
    public class BatchRenameTool : EditorWindow
    {
        [MenuItem("ES/Tools/Batch Rename")]
        public static void ShowWindow()
        {
            GetWindow<BatchRenameTool>("Batch Rename");
        }
        
        private string searchPattern = "";
        private string replacePattern = "";
        private string prefix = "";
        private string suffix = "";
        private bool addNumbering = false;
        
        private List<Object> selectedAssets = new();
        
        void OnGUI()
        {
            GUILayout.Label("Batch Rename Tool", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            // 选择资源
            EditorGUILayout.LabelField("Selected Assets:", $"{selectedAssets.Count}");
            
            if (GUILayout.Button("Add Selected Assets"))
            {
                selectedAssets.AddRange(Selection.objects);
            }
            
            if (GUILayout.Button("Clear Selection"))
            {
                selectedAssets.Clear();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rename Options", EditorStyles.boldLabel);
            
            // 搜索替换
            searchPattern = EditorGUILayout.TextField("Search:", searchPattern);
            replacePattern = EditorGUILayout.TextField("Replace With:", replacePattern);
            
            EditorGUILayout.Space();
            
            // 前缀/后缀
            prefix = EditorGUILayout.TextField("Add Prefix:", prefix);
            suffix = EditorGUILayout.TextField("Add Suffix:", suffix);
            
            addNumbering = EditorGUILayout.Toggle("Add Numbering", addNumbering);
            
            EditorGUILayout.Space();
            
            // 预览
            if (selectedAssets.Count > 0)
            {
                EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);
                string preview = GetPreviewName(selectedAssets[0].name, 1);
                EditorGUILayout.HelpBox($"Example: {selectedAssets[0].name} → {preview}", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            
            // 执行
            if (GUILayout.Button("Apply Rename", GUILayout.Height(30)))
            {
                ApplyRename();
            }
        }
        
        private string GetPreviewName(string originalName, int index)
        {
            string newName = originalName;
            
            // 搜索替换
            if (!string.IsNullOrEmpty(searchPattern))
            {
                newName = newName.Replace(searchPattern, replacePattern);
            }
            
            // 前缀
            if (!string.IsNullOrEmpty(prefix))
            {
                newName = prefix + newName;
            }
            
            // 后缀
            if (!string.IsNullOrEmpty(suffix))
            {
                newName = newName + suffix;
            }
            
            // 编号
            if (addNumbering)
            {
                newName = $"{newName}_{index:D3}";
            }
            
            return newName;
        }
        
        private void ApplyRename()
        {
            if (selectedAssets.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No assets selected!", "OK");
                return;
            }
            
            if (!EditorUtility.DisplayDialog("Confirm", 
                $"Rename {selectedAssets.Count} assets?", "Yes", "Cancel"))
            {
                return;
            }
            
            for (int i = 0; i < selectedAssets.Count; i++)
            {
                var asset = selectedAssets[i];
                string assetPath = AssetDatabase.GetAssetPath(asset);
                string newName = GetPreviewName(asset.name, i + 1);
                
                AssetDatabase.RenameAsset(assetPath, newName);
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"Renamed {selectedAssets.Count} assets.");
            selectedAssets.Clear();
        }
    }
    
    #endregion
    
    #region Quick Actions
    
    /// <summary>
    /// 快速操作菜单
    /// </summary>
    public static class QuickActions
    {
        [MenuItem("ES/Quick Actions/Open Persistent Data Path")]
        private static void OpenPersistentDataPath()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
        
        [MenuItem("ES/Quick Actions/Clear PlayerPrefs")]
        private static void ClearPlayerPrefs()
        {
            if (EditorUtility.DisplayDialog("Confirm", "Clear all PlayerPrefs?", "Yes", "Cancel"))
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                Debug.Log("PlayerPrefs cleared.");
            }
        }
        
        [MenuItem("ES/Quick Actions/Take Screenshot")]
        private static void TakeScreenshot()
        {
            string filename = $"Screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            string path = $"{Application.dataPath}/../Screenshots/{filename}";
            
            System.IO.Directory.CreateDirectory($"{Application.dataPath}/../Screenshots");
            ScreenCapture.CaptureScreenshot(path);
            
            Debug.Log($"Screenshot saved: {path}");
        }
    }
    
    #endregion
}
