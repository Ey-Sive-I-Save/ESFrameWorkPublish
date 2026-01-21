using UnityEngine;
using UnityEditor;
using System.IO;
using ES.VMCP;

namespace ES.VMCP.Editor
{
    /// <summary>
    /// ESVMCP编辑器工具
    /// </summary>
    public static class ESVMCPEditorTools
    {
        public const string MenuRoot = "Tools/【ESVMCP】/";
        private const string DefaultDataFolderRoot = "Assets/ES/ESVMCP/RunningData";
        private const string DefaultRootFolder = "Assets/ES/ESVMCP";

        /// <summary>
        /// 获取数据文件夹根路径
        /// </summary>
        private static string DataFolderRoot
        {
            get
            {
                var config = GetConfig();
                return config != null ? config.BaseFolder : DefaultDataFolderRoot;
            }
        }

        /// <summary>
        /// 获取资源文件夹路径
        /// </summary>
        /// <remarks>
        /// 查找全局资产：ESVMCP的资源文件夹路径，从配置中获取或使用默认路径
        /// </remarks>
        private static string ResourcesPath => GetConfig()?.ResourcesFolder ?? "Assets/ES/ESVMCP/Resources";

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        /// <remarks>
        /// 查找全局资产：ESVMCPConfig.asset 文件的路径，用于加载配置
        /// </remarks>
        private static string ConfigAssetPath => System.IO.Path.Combine(ResourcesPath, "ESVMCPConfig.asset");

        /// <summary>
        /// 获取记忆资产路径
        /// </summary>
        /// <remarks>
        /// 查找全局资产：ESVMCPMemoryAsset.asset 文件的路径，用于记忆系统
        /// </remarks>
        private static string MemoryAssetPath => System.IO.Path.Combine(ResourcesPath, "ESVMCPMemoryAsset.asset");

        /// <summary>
        /// 获取配置实例
        /// </summary>
        /// <remarks>
        /// 查找全局资产：从默认路径加载 ESVMCPConfig.asset 配置资产
        /// </remarks>
        private static ESVMCPConfig GetConfig()
        {
            return ESVMCPConfig.Instance;
        }

        [MenuItem(MenuRoot + "【环境识别】", priority = 2)]
        public static void ShowEnvironmentSummary()
        {
            // 使用新的场景分析器
        var sceneMemory = Object.FindObjectOfType<ESVMCPMemoryEnhanced>();
        string content = ESVMCPSceneAnalyzer.GenerateEnvironmentInfo(sceneMemory);
            EditorGUIUtility.systemCopyBuffer = content;

            // 显示复杂面板
            EnvironmentAnalysisCompleteWindow.ShowWindow(content.Length);

            Debug.Log($"[ESVMCP] 环境识别完成，{content.Length}字符已复制到剪贴板");
        }

        /// <summary>
        /// 递归统计场景中所有GameObject数量
        /// </summary>
        private static int CountAllGameObjects(GameObject[] roots)
        {
            int count = 0;
            foreach (var root in roots)
            {
                count += CountChildren(root) + 1; // +1 for root itself
            }
            return count;
        }

        /// <summary>
        /// 递归统计子对象数量（包括自身）
        /// </summary>
        private static int CountChildren(GameObject obj)
        {
            int count = 0;
            foreach (Transform child in obj.transform)
            {
                count += 1 + CountChildren(child.gameObject);
            }
            return count;
        }

        [MenuItem(MenuRoot + "系统/【一键安装】", priority = 200)]
        public static void CompleteSetup()
        {
            Debug.Log("=== ESVMCP 完整安装开始 ===");

            // 1. 创建文件夹结构
            CreateFolderStructure();

            // 2. 创建配置资产
            var config = CreateConfigAsset();

            // 3. 创建记忆资产
            CreateMemoryAsset();

            // 4. 在当前场景添加记忆组件
            AddMemoryComponentToScene();

            // 5. 使用配置创建Data文件夹
            if (config != null)
            {
                config.EnsureFoldersExist();
            }

            Debug.Log("=== ESVMCP 完整安装完成！===");
            EditorUtility.DisplayDialog("ESVMCP安装", 
                "ESVMCP系统安装完成！\n\n" +
                "已创建：\n" +
                "- 文件夹结构\n" +
                "- 配置资产\n" +
                "- 记忆资产\n" +
                "- 场景记忆组件\n\n" +
                "请在场景中查看ESVMCPMemoryEnhanced组件", 
                "确定");
        }

        [MenuItem(MenuRoot + "导出记忆", priority = 20)]
        public static void ExportCurrentMemory()
        {
            var config = ESVMCPConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到ESVMCPConfig配置", "确定");
                return;
            }

            // 获取场景记忆
        var sceneMemory = Object.FindObjectOfType<ESVMCPMemoryEnhanced>();
        if (sceneMemory == null)
        {
            EditorUtility.DisplayDialog("提示", "场景中未找到ESVMCPMemoryEnhanced组件，请先运行一键安装", "确定");
            return;
        }

        // 获取持久记忆
        var persistentMemory = ESVMCPConfig.Instance.GetPersistentMemory();
            // 导出到文件
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"memory_export_{timestamp}.txt";
            string exportPath = Path.Combine(config.MemoryFolder, fileName);

            try
            {
                Directory.CreateDirectory(config.MemoryFolder);
                string content = GenerateMemoryReport(sceneMemory, persistentMemory);
                File.WriteAllText(exportPath, content);

                EditorUtility.DisplayDialog("导出成功", 
                    $"记忆数据已导出到：\n{fileName}\n\n包含场景记忆和持久记忆的完整状态。", 
                    "确定");

                Debug.Log($"[ESVMCP] 记忆数据已导出: {exportPath}");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("导出失败", $"导出记忆数据时出错：\n{ex.Message}", "确定");
                Debug.LogError($"[ESVMCP] 导出记忆失败: {ex.Message}");
            }
        }

        [MenuItem(MenuRoot + "系统/【查看状态】", priority = 201)]
        public static void ShowSystemStatus()
        {
            var config = ESVMCPConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到ESVMCPConfig配置", "确定");
                return;
            }

            // 收集状态信息
            var statusReport = new System.Text.StringBuilder();
            statusReport.AppendLine("=== ESVMCP 系统状态 ===");
            statusReport.AppendLine();

            // 场景状态
            var sceneMemory = Object.FindObjectOfType<ESVMCPMemoryEnhanced>();
            statusReport.AppendLine("📊 场景记忆:");
            if (sceneMemory != null)
            {
                statusReport.AppendLine($"  - 记忆条目: {sceneMemory.TotalMemoryItems}");
                statusReport.AppendLine($"  - GameObject记忆: {sceneMemory.GameObjectMemory}");
                statusReport.AppendLine($"  - 资产记忆: {sceneMemory.AssetMemory}");
                statusReport.AppendLine($"  - 组件记忆: {sceneMemory.ComponentMemory}");
            }
            else
            {
                statusReport.AppendLine("  - 未找到场景记忆组件");
            }
            statusReport.AppendLine();

            // 持久记忆状态
            var persistentMemory = ESVMCPConfig.Instance.GetPersistentMemory();
            statusReport.AppendLine("💾 持久记忆:");
            if (persistentMemory != null)
            {
                statusReport.AppendLine($"  - 记忆条目: {persistentMemory.TotalItems}");
            }
            else
            {
                statusReport.AppendLine("  - 未找到持久记忆资产");
            }
            statusReport.AppendLine();

            // 文件夹状态
            statusReport.AppendLine("📁 文件夹状态:");
            string[] folders = { config.InputFolder, config.ArchiveFolder, config.MemoryFolder, config.LogFolder };
            string[] folderNames = { "Input", "Archive", "Memory", "Logs" };

            for (int i = 0; i < folders.Length; i++)
            {
                bool exists = Directory.Exists(folders[i]);
                int fileCount = exists ? Directory.GetFiles(folders[i], "*.json").Length : 0;
                statusReport.AppendLine($"  - {folderNames[i]}: {(exists ? "✓" : "✗")} ({fileCount} 个文件)");
            }
            statusReport.AppendLine();

            // 配置状态
            statusReport.AppendLine("⚙️ 配置状态:");
            statusReport.AppendLine($"  - 基础文件夹: {config.BaseFolder}");
            statusReport.AppendLine($"  - 自动执行: {(config.AutoExecute ? "开启" : "关闭")}");
            statusReport.AppendLine($"  - 遇错停止: {(config.StopOnError ? "开启" : "关闭")}");
            statusReport.AppendLine($"  - 启用记忆: {(config.EnableMemory ? "开启" : "关闭")}");

            // 显示状态窗口
            EditorUtility.DisplayDialog("ESVMCP 系统状态", statusReport.ToString(), "确定");
        }

        [MenuItem(MenuRoot + "【AI指导】", priority = 1)]
        public static void GetAIGuidance()
        {
            var config = ESVMCPConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到ESVMCPConfig配置", "确定");
                return;
            }

            // 读取AI指导文档
            string guidancePath = config.AIGuidanceDocumentPath;

            if (!File.Exists(guidancePath))
            {
                EditorUtility.DisplayDialog("错误", $"未找到AI指导文档：\n{guidancePath}", "确定");
                return;
            }

            try
            {
                string content = File.ReadAllText(guidancePath);
                
                // 追加当前实际的文件夹路径信息
                var pathInfo = new System.Text.StringBuilder();
                pathInfo.AppendLine("\n\n---");
                pathInfo.AppendLine("## 📂 当前项目实际路径信息");
                pathInfo.AppendLine("\n**重要**: 以下是当前Unity项目的实际文件夹路径，请使用这些路径而非文档中的示例路径。\n");
                pathInfo.AppendLine("### 数据文件夹");
                pathInfo.AppendLine($"- **⚠️ Input文件夹 (重要！位于RunningData中)**: `{config.InputFolder}`");
                pathInfo.AppendLine($"- **Archive文件夹**: `{config.ArchiveFolder}`");
                pathInfo.AppendLine($"- **Memory文件夹**: `{config.MemoryFolder}`");
                pathInfo.AppendLine($"- **Logs文件夹**: `{config.LogFolder}`");
                pathInfo.AppendLine("\n### 配置文件");
                pathInfo.AppendLine($"- **配置资产路径**: `{ConfigAssetPath}`");
                pathInfo.AppendLine($"- **记忆资产路径**: `{MemoryAssetPath}`");
                pathInfo.AppendLine($"- **AI指导文档路径**: `{guidancePath}`");
                pathInfo.AppendLine("\n### 资源文件夹");
                pathInfo.AppendLine($"- **Resources路径**: `{config.ResourcesFolder}`");
                pathInfo.AppendLine($"- **根文件夹**: `{config.RootFolder}`");
                pathInfo.AppendLine($"- **基础文件夹**: `{config.BaseFolder}`");
                pathInfo.AppendLine("\n### 系统配置");
                pathInfo.AppendLine($"- **自动执行**: {(config.AutoExecute ? "开启" : "关闭")}");
                pathInfo.AppendLine($"- **遇错停止**: {(config.StopOnError ? "开启" : "关闭")}");
                pathInfo.AppendLine($"- **启用记忆**: {(config.EnableMemory ? "开启" : "关闭")}");
                pathInfo.AppendLine($"- **环境详细等级**: {config.DefaultDetailLevel}");
                pathInfo.AppendLine("\n**生成时间**: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                pathInfo.AppendLine("\n---\n");
                
                // 合并内容
                string fullContent = content + pathInfo.ToString();
                EditorGUIUtility.systemCopyBuffer = fullContent;

                // 显示AI指导完成窗口
                AIGuidanceCompleteWindow.ShowWindow(guidancePath, fullContent.Length);

                Debug.Log($"[ESVMCP] AI指导文档已复制到剪贴板 ({fullContent.Length} 字符，包含当前项目路径信息)");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("读取失败", $"读取AI指导文档时出错：\n{ex.Message}", "确定");
                Debug.LogError($"[ESVMCP] 读取AI指导文档失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成记忆报告
        /// </summary>
        private static string GenerateMemoryReport(ESVMCPMemoryEnhanced sceneMemory, ESVMCPMemoryAssetEnhanced persistentMemory)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== ESVMCP 记忆导出报告 ===");
            report.AppendLine($"导出时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            // 场景记忆
            report.AppendLine("📊 场景记忆 (MonoBehaviour):");
            if (sceneMemory != null)
            {
                report.AppendLine($"记忆条目数量: {sceneMemory.TotalMemoryItems}");
                report.AppendLine($"GameObject记忆: {sceneMemory.GameObjectMemory}");
                report.AppendLine($"资产记忆: {sceneMemory.AssetMemory}");
                report.AppendLine($"组件记忆: {sceneMemory.ComponentMemory}");
                report.AppendLine();

                if (sceneMemory.TotalMemoryItems > 0)
                {
                    report.AppendLine("记忆内容:");
                    var allKeys = sceneMemory.GetAllKeys();
                    foreach (var key in allKeys)
                    {
                        var item = sceneMemory.GetMemoryItem(key);
                        if (item != null)
                        {
                            report.AppendLine($"  {key}: {item.Resolve()} [{item.ItemType}]");
                        }
                    }
                }

                if (sceneMemory.GameObjectMemory > 0)
                {
                    report.AppendLine();
                    report.AppendLine("GameObject引用:");
                    var goKeys = sceneMemory.GetKeysByType(ESVMCPMemoryItemType.GameObject);
                    foreach (var key in goKeys)
                    {
                        var go = sceneMemory.GetGameObject(key);
                        string status = go != null ? $"{go.name} (Active: {go.activeSelf})" : "已销毁";
                        report.AppendLine($"  {key}: {status}");
                    }
                }
            }
            else
            {
                report.AppendLine("未找到场景记忆组件");
            }
            report.AppendLine();

            // 持久记忆
            report.AppendLine("💾 持久记忆 (ScriptableObject):");
            if (persistentMemory != null)
            {
                report.AppendLine($"记忆条目数量: {persistentMemory.TotalItems}");

                if (persistentMemory.TotalItems > 0)
                {
                    report.AppendLine();
                    report.AppendLine("记忆内容:");
                    var allKeys = persistentMemory.GetAllKeys();
                    foreach (var key in allKeys)
                    {
                        var item = persistentMemory.GetMemoryItem(key);
                        if (item != null)
                        {
                            report.AppendLine($"  {key}: {item.Resolve()} [{item.ItemType}]");
                        }
                    }
                }
            }
            else
            {
                report.AppendLine("未找到持久记忆资产");
            }

            return report.ToString();
        }

        [MenuItem(MenuRoot + "创建文件夹结构", priority = 21)]
        public static void CreateFolderStructure()
        {
            Debug.Log("[ESVMCP] 开始创建文件夹结构...");

            // Data文件夹
            string projectRoot = Path.Combine(Application.dataPath, "..");
            CreateFolder(Path.Combine(projectRoot, DataFolderRoot));
            CreateFolder(Path.Combine(projectRoot, DataFolderRoot, "Input"));
            CreateFolder(Path.Combine(projectRoot, DataFolderRoot, "Archive"));
            CreateFolder(Path.Combine(projectRoot, DataFolderRoot, "Memory"));
            CreateFolder(Path.Combine(projectRoot, DataFolderRoot, "Logs"));

            // Assets文件夹
            string rootFolder = DefaultRootFolder;
            CreateFolder(Path.Combine(rootFolder, "Core"));
            CreateFolder(Path.Combine(rootFolder, "Commands"));
            CreateFolder(Path.Combine(rootFolder, "Commands/GameObject"));
            CreateFolder(Path.Combine(rootFolder, "Commands/Component"));
            CreateFolder(Path.Combine(rootFolder, "Commands/Scene"));
            CreateFolder(Path.Combine(rootFolder, "Commands/Asset"));
            CreateFolder(Path.Combine(rootFolder, "Commands/Memory"));
            CreateFolder(Path.Combine(rootFolder, "Commands/Custom"));
            CreateFolder(Path.Combine(rootFolder, "Memory"));
            CreateFolder(Path.Combine(rootFolder, "Json"));
            CreateFolder(Path.Combine(rootFolder, "Editor"));
            CreateFolder(Path.Combine(rootFolder, "Resources"));
            CreateFolder(Path.Combine(rootFolder, "Examples"));

            AssetDatabase.Refresh();
            Debug.Log("[ESVMCP] 文件夹结构创建完成！");
        } 

        [MenuItem(MenuRoot + "创建配置资产", priority = 22)]
        public static ESVMCPConfig CreateConfigAsset()
        {
            Debug.Log("[ESVMCP] 创建配置资产...");

            // 确保Resources文件夹存在
            if (!Directory.Exists(ResourcesPath))
            {
                Directory.CreateDirectory(ResourcesPath);
                AssetDatabase.Refresh();
            }

            // 检查是否已存在 - 查找全局资产：检查配置资产是否已存在
            ESVMCPConfig config = ESVMCPConfig.Instance;
            if (config != null)
            {
                Debug.Log("[ESVMCP] 配置资产已存在");
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
                return config;
            }

            // 创建新配置
            config = ScriptableObject.CreateInstance<ESVMCPConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log($"[ESVMCP] 配置资产创建完成: {ConfigAssetPath}");
            return config;
        }

        [MenuItem(MenuRoot + "创建记忆资产", priority = 23)]
        public static ESVMCPMemoryAssetEnhanced CreateMemoryAsset()
        {
            Debug.Log("[ESVMCP] 创建记忆资产...");

            // 确保Resources文件夹存在
            if (!Directory.Exists(ResourcesPath))
            {
                Directory.CreateDirectory(ResourcesPath);
                AssetDatabase.Refresh();
            }

            // 检查是否已存在 - 查找全局资产：检查记忆资产是否已存在
            ESVMCPMemoryAssetEnhanced memoryAsset = AssetDatabase.LoadAssetAtPath<ESVMCPMemoryAssetEnhanced>(MemoryAssetPath);
            if (memoryAsset != null)
            {
                Debug.Log("[ESVMCP] 记忆资产已存在");
                Selection.activeObject = memoryAsset;
                EditorGUIUtility.PingObject(memoryAsset);
                return memoryAsset;
            }

            // 创建新记忆资产
            memoryAsset = ScriptableObject.CreateInstance<ESVMCPMemoryAssetEnhanced>();
            AssetDatabase.CreateAsset(memoryAsset, MemoryAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = memoryAsset;
            EditorGUIUtility.PingObject(memoryAsset);

            Debug.Log($"[ESVMCP] 记忆资产创建完成: {MemoryAssetPath}");
            return memoryAsset;
        }

        [MenuItem(MenuRoot + "在场景中添加记忆组件", priority = 24)]
        public static void AddMemoryComponentToScene()
        {
            Debug.Log("[ESVMCP] 在场景中添加记忆组件...");

            // 查找是否已存在
            ESVMCPMemoryEnhanced existingMemory = Object.FindObjectOfType<ESVMCPMemoryEnhanced>();
            if (existingMemory != null)
            {
                Debug.Log("[ESVMCP] 场景中已存在记忆组件");
                Selection.activeGameObject = existingMemory.gameObject;
                EditorGUIUtility.PingObject(existingMemory.gameObject);
                return;
            }

            // 创建新GameObject并添加组件
            GameObject memoryObj = new GameObject("ESVMCP_Memory");
            ESVMCPMemoryEnhanced memory = memoryObj.AddComponent<ESVMCPMemoryEnhanced>();

            // 设置标签
            memoryObj.tag = "EditorOnly";

            Selection.activeGameObject = memoryObj;
            EditorGUIUtility.PingObject(memoryObj);

            Debug.Log("[ESVMCP] 记忆组件已添加到场景");
        }

        [MenuItem(MenuRoot + "系统/打开文件夹/打开Input文件夹", priority = 210)]
        public static void OpenInputFolder()
        {
            string path = Path.Combine(Application.dataPath, "..", DataFolderRoot, "Input");
            OpenFolder(path);
        }

        [MenuItem(MenuRoot + "系统/打开文件夹/打开Archive文件夹", priority = 211)]
        public static void OpenArchiveFolder()
        {
            string path = Path.Combine(Application.dataPath, "..", DataFolderRoot, "Archive");
            OpenFolder(path);
        }

        [MenuItem(MenuRoot + "系统/打开文件夹/打开Memory文件夹", priority = 212)]
        public static void OpenMemoryFolder()
        {
            string path = Path.Combine(Application.dataPath, "..", DataFolderRoot, "Memory");
            OpenFolder(path);
        }

        [MenuItem(MenuRoot + "系统/打开文件夹/打开Logs文件夹", priority = 213)]
        public static void OpenLogsFolder()
        {
            string path = Path.Combine(Application.dataPath, "..", DataFolderRoot, "Logs");
            OpenFolder(path);
        }

        [MenuItem(MenuRoot + "系统/资产/选择配置资产", priority = 220)]
        public static void SelectConfigAsset()
        {
            // 查找全局资产：加载并选择配置资产
            ESVMCPConfig config = ESVMCPConfig.Instance;
            if (config != null)
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
            else
            {
                Debug.LogWarning("[ESVMCP] 配置资产不存在，请先创建");
            }
        }

        [MenuItem(MenuRoot + "系统/资产/选择记忆资产", priority = 221)]
        public static void SelectMemoryAsset()
        {
            // 查找全局资产：加载并选择记忆资产
            ESVMCPMemoryAssetEnhanced memoryAsset = AssetDatabase.LoadAssetAtPath<ESVMCPMemoryAssetEnhanced>(MemoryAssetPath);
            if (memoryAsset != null)
            {
                Selection.activeObject = memoryAsset;
                EditorGUIUtility.PingObject(memoryAsset);
            }
            else
            {
                Debug.LogWarning("[ESVMCP] 记忆资产不存在，请先创建");
            }
        }

        [MenuItem(MenuRoot + "创建示例JSON", priority = 25)]
        public static void CreateExampleJson()
        {
            string exampleJson = @"{
  ""commandId"": ""example_001"",
  ""timestamp"": """ + System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + @""",
  ""description"": ""示例命令"",
  ""commands"": [
    {
      ""type"": ""CreateGameObject"",
      ""id"": ""obj1"",
      ""parameters"": {
        ""name"": ""ExampleObject"",
        ""position"": [0, 1, 0]
      }
    }
  ],
  ""memory"": {
    ""save"": {
      ""example_object_id"": ""{{obj1.gameObjectId}}""
    }
  }
}";

            string filename = $"example_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            string path = Path.Combine(Application.dataPath, "..", DataFolderRoot, "Input", filename);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, exampleJson);

            Debug.Log($"[ESVMCP] 示例JSON已创建: {path}");
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "帮助/打开README", priority = 51)]
        public static void OpenReadme()
        {
            string readmePath = Path.Combine(DefaultRootFolder, "README.md");
            // 查找全局资产：加载README文档资产
            var readme = AssetDatabase.LoadAssetAtPath<TextAsset>(readmePath);
            if (readme != null)
            {
                Selection.activeObject = readme;
                EditorGUIUtility.PingObject(readme);
            }
            else
            {
                Debug.LogWarning("[ESVMCP] README文件不存在");
            }
        }

        [MenuItem(MenuRoot + "帮助/打开实现指南", priority = 52)]
        public static void OpenImplementationGuide()
        {
            string guidePath = Path.Combine(DefaultRootFolder, "IMPLEMENTATION_GUIDE.md");
            // 查找全局资产：加载实现指南文档资产
            var guide = AssetDatabase.LoadAssetAtPath<TextAsset>(guidePath);
            if (guide != null)
            {
                Selection.activeObject = guide;
                EditorGUIUtility.PingObject(guide);
            }
            else
            {
                Debug.LogWarning("[ESVMCP] 实现指南文件不存在");
            }
        }

        // 辅助方法
        private static void CreateFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Debug.Log($"[ESVMCP] 创建文件夹: {path}");
            }
        }

        private static void OpenFolder(string path)
        {
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start(path);
            }
            else
            {
                Directory.CreateDirectory(path);
                Debug.Log($"[ESVMCP] 创建并打开文件夹: {path}");
                System.Diagnostics.Process.Start(path);
            }
        }
    }

    /// <summary>
    /// 环境识别完成提示窗口
    /// </summary>
    public class EnvironmentAnalysisCompleteWindow : EditorWindow
    {
        private int contentLength;
        private bool dontShowAgain;
        private const string DONT_SHOW_KEY = "ESVMCP_EnvironmentAnalysis_DontShow";

        public static void ShowWindow(int contentLength)
        {
            // 检查是否设置了不再提醒
            if (EditorPrefs.GetBool(DONT_SHOW_KEY, false))
            {
                Debug.Log($"[ESVMCP] 环境识别完成，{contentLength}字符已复制到剪贴板 (不再提醒)");
                return;
            }

            var window = GetWindow<EnvironmentAnalysisCompleteWindow>("环境识别完成", true);
            window.contentLength = contentLength;
            window.dontShowAgain = false;
            window.minSize = new Vector2(450, 300);
            window.Show();
        }

        private void OnGUI()
        {
            // 标题
            EditorGUILayout.LabelField("🎉 环境识别完成", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 信息内容
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("✅ 环境信息已复制到剪贴板！", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("📊 包含内容：", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• 智能分析的场景状态", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• 记忆系统数据", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• 对象层级结构", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• 组件统计信息", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 字数统计
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"📝 总字数: {contentLength}", EditorStyles.boldLabel);
            if (contentLength > 10000)
            {
                EditorGUILayout.LabelField("(大量数据)", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.yellow } });
            }
            else if (contentLength > 5000)
            {
                EditorGUILayout.LabelField("(中等数据)", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.green } });
            }
            else
            {
                EditorGUILayout.LabelField("(少量数据)", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.cyan } });
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 不再提醒选项
            dontShowAgain = EditorGUILayout.ToggleLeft("不再显示此提示", dontShowAgain);

            EditorGUILayout.Space(10);

            // 按钮区域
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("确定", GUILayout.Height(25)))
            {
                if (dontShowAgain)
                {
                    EditorPrefs.SetBool(DONT_SHOW_KEY, true);
                }
                Close();
            }

            if (GUILayout.Button("查看剪贴板内容", GUILayout.Height(25), GUILayout.Width(120)))
            {
                // 打开一个临时窗口显示剪贴板内容
                var previewWindow = GetWindow<EnvironmentDataPreviewWindow>("剪贴板内容预览", true);
                previewWindow.content = EditorGUIUtility.systemCopyBuffer;
                previewWindow.minSize = new Vector2(700, 500);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 底部提示
            EditorGUILayout.LabelField("💡 提示：您可以直接将剪贴板内容粘贴给AI使用", 
                new GUIStyle(EditorStyles.miniLabel) { wordWrap = true });
        }
    }

    /// <summary>
    /// 环境数据预览窗口
    /// </summary>
    public class EnvironmentDataPreviewWindow : EditorWindow
    {
        public string content = "";
        private Vector2 scrollPosition;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("📋 剪贴板内容预览", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"总字符数: {content.Length}", EditorStyles.miniLabel);
            if (GUILayout.Button("复制到剪贴板", GUILayout.Width(100)))
            {
                EditorGUIUtility.systemCopyBuffer = content;
                ShowNotification(new GUIContent("已复制到剪贴板"));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, "box");
            EditorGUILayout.TextArea(content, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            if (GUILayout.Button("关闭", GUILayout.Height(25)))
            {
                Close();
            }
        }
    }

    /// <summary>
    /// AI指导完成提示窗口
    /// </summary>
    public class AIGuidanceCompleteWindow : EditorWindow
    {
        private string guidancePath;
        private int contentLength;
        private bool autoClose = true;
        private double startTime;
        private const string AUTO_CLOSE_KEY = "ESVMCP_AIGuidance_AutoClose";

        public static void ShowWindow(string path, int length)
        {
            var window = GetWindow<AIGuidanceCompleteWindow>("AI指导已复制", true);
            window.guidancePath = path;
            window.contentLength = length;
            window.autoClose = EditorPrefs.GetBool(AUTO_CLOSE_KEY, true);
            window.startTime = EditorApplication.timeSinceStartup;
            window.minSize = new Vector2(400, 250);
            window.Show();
        }

        private void OnGUI()
        {
            // 检查是否需要自动关闭 - 在所有GUI代码之前
            if (autoClose)
            {
                double elapsedTime = EditorApplication.timeSinceStartup - startTime;
                if (elapsedTime >= 3.0)
                {
                    EditorPrefs.SetBool(AUTO_CLOSE_KEY, autoClose);
                    Close();
                    GUIUtility.ExitGUI(); // 确保立即退出GUI处理
                    return;
                }
                Repaint(); // 重新绘制以更新倒计时
            }

            // 标题
            EditorGUILayout.LabelField("🤖 AI指导已复制", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 信息内容
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("✅ AI交互指导文档已复制到剪贴板！", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("📄 文档位置：", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(guidancePath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("📝 文档字数：" + contentLength, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 自动关闭选项
            EditorGUILayout.BeginHorizontal();
            autoClose = EditorGUILayout.ToggleLeft("3秒后自动关闭", autoClose);
            if (autoClose)
            {
                double elapsedTime = EditorApplication.timeSinceStartup - startTime;
                double remainingTime = System.Math.Max(0.0, 3.0 - elapsedTime);
                EditorGUILayout.LabelField($"({Mathf.CeilToInt((float)remainingTime)}秒)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 按钮区域
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("立即关闭", GUILayout.Height(25)))
            {
                EditorPrefs.SetBool(AUTO_CLOSE_KEY, autoClose);
                Close();
                GUIUtility.ExitGUI(); // 确保立即退出GUI处理
            }

            if (GUILayout.Button("查看文档内容", GUILayout.Height(25), GUILayout.Width(100)))
            {
                // 打开文档预览窗口
                var previewWindow = GetWindow<AIDocumentPreviewWindow>("AI指导文档预览", true);
                previewWindow.content = EditorGUIUtility.systemCopyBuffer;
                previewWindow.minSize = new Vector2(600, 400);
            }

            if (GUILayout.Button("打开文档位置", GUILayout.Height(25), GUILayout.Width(100)))
            {
                EditorUtility.RevealInFinder(guidancePath);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 底部提示
            EditorGUILayout.LabelField("💡 提示：您现在可以直接将剪贴板内容粘贴给AI使用",
                new GUIStyle(EditorStyles.miniLabel) { wordWrap = true });
        }
    }

    /// <summary>
    /// AI文档预览窗口
    /// </summary>
    public class AIDocumentPreviewWindow : EditorWindow
    {
        public string content = "";
        private Vector2 scrollPosition;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("📄 AI指导文档内容", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"总字符数: {content.Length}", EditorStyles.miniLabel);
            if (GUILayout.Button("复制全部", GUILayout.Width(80)))
            {
                EditorGUIUtility.systemCopyBuffer = content;
                ShowNotification(new GUIContent("已复制到剪贴板"));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, "box");
            EditorGUILayout.TextArea(content, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("关闭", GUILayout.Height(25)))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
