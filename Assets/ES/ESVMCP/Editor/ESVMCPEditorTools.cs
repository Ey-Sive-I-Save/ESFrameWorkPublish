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
        private const string MenuRoot = "Tools/ESVMCP/";
        private const string DefaultDataFolderRoot = "Data/ESVMCP";
        private const string ResourcesPath = "Assets/ES/ESVMCP/Resources";
        private const string ConfigAssetPath = "Assets/ES/ESVMCP/Resources/ESVMCPConfig.asset";
        private const string MemoryAssetPath = "Assets/ES/ESVMCP/Resources/ESVMCPMemoryAsset.asset";

        /// <summary>
        /// 获取数据文件夹根路径
        /// </summary>
        private static string DataFolderRoot
        {
            get
            {
                var config = AssetDatabase.LoadAssetAtPath<ESVMCPConfig>(ConfigAssetPath);
                return config != null ? config.BaseFolder : DefaultDataFolderRoot;
            }
        }

        [MenuItem(MenuRoot + "【一键安装】", priority = 1)]
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
                "请在场景中查看ESVMCPMemory组件", 
                "确定");
        }

        [MenuItem(MenuRoot + "【导出记忆】", priority = 2)]
        public static void ExportCurrentMemory()
        {
            var config = ESVMCPConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到ESVMCPConfig配置", "确定");
                return;
            }

            // 获取场景记忆
            var sceneMemory = Object.FindObjectOfType<ESVMCPMemory>();
            if (sceneMemory == null)
            {
                EditorUtility.DisplayDialog("提示", "场景中未找到ESVMCPMemory组件，请先运行一键安装", "确定");
                return;
            }

            // 获取持久记忆
            var persistentMemory = Resources.Load<ESVMCPMemoryAsset>("ESVMCPMemory");

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

        [MenuItem(MenuRoot + "【查看状态】", priority = 3)]
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
            var sceneMemory = Object.FindObjectOfType<ESVMCPMemory>();
            statusReport.AppendLine("📊 场景记忆:");
            if (sceneMemory != null)
            {
                statusReport.AppendLine($"  - 记忆条目: {sceneMemory.MemoryCount}");
                statusReport.AppendLine($"  - 操作历史: {sceneMemory.HistoryCount}");
                statusReport.AppendLine($"  - GameObject引用: {sceneMemory.ReferenceCount}");
            }
            else
            {
                statusReport.AppendLine("  - 未找到场景记忆组件");
            }
            statusReport.AppendLine();

            // 持久记忆状态
            var persistentMemory = Resources.Load<ESVMCPMemoryAsset>("ESVMCPMemory");
            statusReport.AppendLine("💾 持久记忆:");
            if (persistentMemory != null)
            {
                statusReport.AppendLine($"  - 记忆条目: {persistentMemory.GetMemoryCount()}");
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

        [MenuItem(MenuRoot + "【AI指导】", priority = 4)]
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
            if (string.IsNullOrEmpty(guidancePath))
            {
                guidancePath = "Assets/ES/ESVMCP/AI_INTERACTION_GUIDE.md";
            }

            if (!File.Exists(guidancePath))
            {
                EditorUtility.DisplayDialog("错误", $"未找到AI指导文档：\n{guidancePath}", "确定");
                return;
            }

            try
            {
                string content = File.ReadAllText(guidancePath);
                EditorGUIUtility.systemCopyBuffer = content;

                EditorUtility.DisplayDialog("AI指导已复制", 
                    $"AI交互指导文档已复制到剪贴板！\n\n文档位置：{guidancePath}\n\n您现在可以直接粘贴给AI使用。", 
                    "确定");

                Debug.Log($"[ESVMCP] AI指导文档已复制到剪贴板 ({content.Length} 字符)");
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
        private static string GenerateMemoryReport(ESVMCPMemory sceneMemory, ESVMCPMemoryAsset persistentMemory)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== ESVMCP 记忆导出报告 ===");
            report.AppendLine($"导出时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            // 场景记忆
            report.AppendLine("📊 场景记忆 (MonoBehaviour):");
            if (sceneMemory != null)
            {
                report.AppendLine($"记忆条目数量: {sceneMemory.MemoryCount}");
                report.AppendLine($"操作历史数量: {sceneMemory.HistoryCount}");
                report.AppendLine($"GameObject引用数量: {sceneMemory.ReferenceCount}");
                report.AppendLine();

                if (sceneMemory.MemoryCount > 0)
                {
                    report.AppendLine("记忆内容:");
                    foreach (var kvp in sceneMemory.GetMemoryData())
                    {
                        report.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }

                if (sceneMemory.ReferenceCount > 0)
                {
                    report.AppendLine();
                    report.AppendLine("GameObject引用:");
                    foreach (var kvp in sceneMemory.GetGameObjectReferences())
                    {
                        string status = kvp.Value != null ? "有效" : "已销毁";
                        report.AppendLine($"  {kvp.Key}: {status}");
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
                report.AppendLine($"记忆条目数量: {persistentMemory.GetMemoryCount()}");

                if (persistentMemory.GetMemoryCount() > 0)
                {
                    report.AppendLine();
                    report.AppendLine("记忆内容:");
                    foreach (var kvp in persistentMemory.GetMemoryData())
                    {
                        report.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
            }
            else
            {
                report.AppendLine("未找到持久记忆资产");
            }

            return report.ToString();
        }

        [MenuItem(MenuRoot + "创建/创建文件夹结构", priority = 11)]
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
            CreateFolder("Assets/ES/ESVMCP/Core");
            CreateFolder("Assets/ES/ESVMCP/Commands");
            CreateFolder("Assets/ES/ESVMCP/Commands/GameObject");
            CreateFolder("Assets/ES/ESVMCP/Commands/Component");
            CreateFolder("Assets/ES/ESVMCP/Commands/Scene");
            CreateFolder("Assets/ES/ESVMCP/Commands/Asset");
            CreateFolder("Assets/ES/ESVMCP/Commands/Memory");
            CreateFolder("Assets/ES/ESVMCP/Commands/Custom");
            CreateFolder("Assets/ES/ESVMCP/Memory");
            CreateFolder("Assets/ES/ESVMCP/Json");
            CreateFolder("Assets/ES/ESVMCP/Editor");
            CreateFolder("Assets/ES/ESVMCP/Resources");
            CreateFolder("Assets/ES/ESVMCP/Examples");

            AssetDatabase.Refresh();
            Debug.Log("[ESVMCP] 文件夹结构创建完成！");
        } 

        [MenuItem(MenuRoot + "创建/创建配置资产", priority = 12)]
        public static ESVMCPConfig CreateConfigAsset()
        {
            Debug.Log("[ESVMCP] 创建配置资产...");

            // 确保Resources文件夹存在
            if (!Directory.Exists(ResourcesPath))
            {
                Directory.CreateDirectory(ResourcesPath);
                AssetDatabase.Refresh();
            }

            // 检查是否已存在
            ESVMCPConfig config = AssetDatabase.LoadAssetAtPath<ESVMCPConfig>(ConfigAssetPath);
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

        [MenuItem(MenuRoot + "创建/创建记忆资产", priority = 13)]
        public static ESVMCPMemoryAsset CreateMemoryAsset()
        {
            Debug.Log("[ESVMCP] 创建记忆资产...");

            // 确保Resources文件夹存在
            if (!Directory.Exists(ResourcesPath))
            {
                Directory.CreateDirectory(ResourcesPath);
                AssetDatabase.Refresh();
            }

            // 检查是否已存在
            ESVMCPMemoryAsset memoryAsset = AssetDatabase.LoadAssetAtPath<ESVMCPMemoryAsset>(MemoryAssetPath);
            if (memoryAsset != null)
            {
                Debug.Log("[ESVMCP] 记忆资产已存在");
                Selection.activeObject = memoryAsset;
                EditorGUIUtility.PingObject(memoryAsset);
                return memoryAsset;
            }

            // 创建新记忆资产
            memoryAsset = ScriptableObject.CreateInstance<ESVMCPMemoryAsset>();
            AssetDatabase.CreateAsset(memoryAsset, MemoryAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = memoryAsset;
            EditorGUIUtility.PingObject(memoryAsset);

            Debug.Log($"[ESVMCP] 记忆资产创建完成: {MemoryAssetPath}");
            return memoryAsset;
        }

        [MenuItem(MenuRoot + "创建/在场景中添加记忆组件", priority = 14)]
        public static void AddMemoryComponentToScene()
        {
            Debug.Log("[ESVMCP] 在场景中添加记忆组件...");

            // 查找是否已存在
            ESVMCPMemory existingMemory = Object.FindObjectOfType<ESVMCPMemory>();
            if (existingMemory != null)
            {
                Debug.Log("[ESVMCP] 场景中已存在记忆组件");
                Selection.activeGameObject = existingMemory.gameObject;
                EditorGUIUtility.PingObject(existingMemory.gameObject);
                return;
            }

            // 创建新GameObject并添加组件
            GameObject memoryObj = new GameObject("ESVMCP_Memory");
            ESVMCPMemory memory = memoryObj.AddComponent<ESVMCPMemory>();

            // 设置标签
            memoryObj.tag = "EditorOnly";

            Selection.activeGameObject = memoryObj;
            EditorGUIUtility.PingObject(memoryObj);

            Debug.Log("[ESVMCP] 记忆组件已添加到场景");
        }

        [MenuItem(MenuRoot + "打开文件夹/打开Input文件夹", priority = 21)]
        public static void OpenInputFolder()
        {
            string path = Path.Combine(Application.dataPath, "..", DataFolderRoot, "Input");
            OpenFolder(path);
        }

        [MenuItem(MenuRoot + "打开文件夹/打开Archive文件夹", priority = 22)]
        public static void OpenArchiveFolder()
        {
            string path = Path.Combine(Application.dataPath, "..", DataFolderRoot, "Archive");
            OpenFolder(path);
        }

        [MenuItem(MenuRoot + "打开文件夹/打开Memory文件夹", priority = 23)]
        public static void OpenMemoryFolder()
        {
            string path = Path.Combine(Application.dataPath, "..", DataFolderRoot, "Memory");
            OpenFolder(path);
        }

        [MenuItem(MenuRoot + "打开文件夹/打开Logs文件夹", priority = 24)]
        public static void OpenLogsFolder()
        {
            string path = Path.Combine(Application.dataPath, "..", DataFolderRoot, "Logs");
            OpenFolder(path);
        }

        [MenuItem(MenuRoot + "资产/选择配置资产", priority = 31)]
        public static void SelectConfigAsset()
        {
            ESVMCPConfig config = AssetDatabase.LoadAssetAtPath<ESVMCPConfig>(ConfigAssetPath);
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

        [MenuItem(MenuRoot + "资产/选择记忆资产", priority = 32)]
        public static void SelectMemoryAsset()
        {
            ESVMCPMemoryAsset memoryAsset = AssetDatabase.LoadAssetAtPath<ESVMCPMemoryAsset>(MemoryAssetPath);
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

        [MenuItem(MenuRoot + "工具/创建示例JSON", priority = 42)]
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
            string readmePath = "Assets/ES/ESVMCP/README.md";
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
            string guidePath = "Assets/ES/ESVMCP/IMPLEMENTATION_GUIDE.md";
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
}
