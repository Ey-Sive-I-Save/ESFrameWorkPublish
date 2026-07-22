using UnityEngine;
using UnityEditor;
using System.IO;
using ES.VMCP.Editor;

namespace ES.Obsolete.VMCP
{
    /// <summary>
    /// ESVMCP测试工具 - 在Unity编辑器中测试JSON命令
    /// </summary>
    public class ESVMCPTestTool : EditorWindow
    {
        private string jsonFilePath = "";
        private string executionLog = "";
        private Vector2 scrollPosition;
        private ESVMCPExecutionReport lastReport;
        private ESVMCPBatchReport lastBatchReport;

        [MenuItem(ESVMCPEditorTools.MenuRoot + MenuItemPathDefine.VMCP_SYSTEM_MANAGEMENT + "/测试工具", priority = 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<ESVMCPTestTool>("ESVMCP 测试工具");
            window.minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            GUILayout.Label("ESVMCP JSON 命令测试工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 文件选择
            EditorGUILayout.BeginHorizontal();
            jsonFilePath = EditorGUILayout.TextField("JSON文件:", jsonFilePath);
            if (GUILayout.Button("浏览...", GUILayout.Width(80)))
            {
                string selectedPath = EditorUtility.OpenFilePanel("选择JSON文件", "", "json");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    jsonFilePath = selectedPath;
                }
            }
            EditorGUILayout.EndHorizontal();

            // 快速选择示例
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("示例:", GUILayout.Width(60));
            if (GUILayout.Button("基础场景"))
            {
                jsonFilePath = Path.Combine(Application.dataPath, "ES/ESVMCP/Examples/example_basic_scene.json");
            }
            if (GUILayout.Button("使用记忆"))
            {
                jsonFilePath = Path.Combine(Application.dataPath, "ES/ESVMCP/Examples/example_using_memory.json");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 执行按钮
            GUI.enabled = !string.IsNullOrEmpty(jsonFilePath) && File.Exists(jsonFilePath);
            if (GUILayout.Button("执行 JSON 命令", GUILayout.Height(30)))
            {
                ExecuteJson();
            }
            GUI.enabled = true;

            EditorGUILayout.Space();

            // 批量执行按钮
            var config = ESVMCPConfig.Instance;
            bool hasInputFiles = config != null && Directory.Exists(config.InputFolder) &&
                Directory.GetFiles(config.InputFolder, "*.json").Length > 0;

            GUI.enabled = hasInputFiles;
            if (GUILayout.Button("🔄 批量执行 Input 文件夹", GUILayout.Height(30)))
            {
                ExecuteBatch();
            }
            GUI.enabled = true;

            if (!hasInputFiles && config != null)
            {
                EditorGUILayout.HelpBox($"Input文件夹为空或不存在: {config.InputFolder}", MessageType.Info);
            }

            EditorGUILayout.Space();

            // 报告显示
            if (lastBatchReport != null)
            {
                EditorGUILayout.LabelField("批量执行报告:", EditorStyles.boldLabel);

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"状态: {(lastBatchReport.Success ? "✓ 成功" : "✗ 失败")}");
                EditorGUILayout.LabelField($"文件统计: {lastBatchReport.SuccessfulFiles}/{lastBatchReport.TotalFiles} 成功");
                EditorGUILayout.LabelField($"耗时: {lastBatchReport.Duration.TotalSeconds:F2} 秒");
                if (!string.IsNullOrEmpty(lastBatchReport.Message))
                {
                    EditorGUILayout.HelpBox(lastBatchReport.Message, lastBatchReport.Success ? MessageType.Info : MessageType.Error);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
            }
            else if (lastReport != null)
            {
                EditorGUILayout.LabelField("执行报告:", EditorStyles.boldLabel);

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"状态: {(lastReport.Success ? "✓ 成功" : "✗ 失败")}");
                EditorGUILayout.LabelField($"命令统计: {lastReport.SuccessfulCommands}/{lastReport.TotalCommands} 成功");
                EditorGUILayout.LabelField($"耗时: {lastReport.Duration.TotalSeconds:F2} 秒");
                if (!string.IsNullOrEmpty(lastReport.ErrorMessage))
                {
                    EditorGUILayout.HelpBox(lastReport.ErrorMessage, MessageType.Error);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
            }

            // 日志显示
            EditorGUILayout.LabelField("执行日志:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, "box");
            EditorGUILayout.TextArea(executionLog, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // 底部按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("清空日志"))
            {
                executionLog = "";
                lastReport = null;
                lastBatchReport = null;
            }
            if (GUILayout.Button("复制完整报告") && (lastReport != null || lastBatchReport != null))
            {
                string reportText = lastBatchReport != null ? lastBatchReport.GenerateReport() : lastReport.GenerateReport();
                EditorGUIUtility.systemCopyBuffer = reportText;
                Debug.Log("[ESVMCP] 报告已复制到剪贴板");
            }
            if (GUILayout.Button("打开配置"))
            {
                Selection.activeObject = ESVMCPConfig.Instance;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ExecuteJson()
        {
            try
            {
                // 获取配置和记忆系统
                var config = ESVMCPConfig.Instance;
                if (config == null)
                {
                    executionLog = "错误: 未找到ESVMCPConfig配置\n";
                    return;
                }

                // 查找场景中的记忆组件
                var sceneMemory = FindObjectOfType<ESVMCPMemoryEnhanced>();
                if (sceneMemory == null)
                {
                    Debug.LogWarning("[ESVMCP] 场景中未找到ESVMCPMemoryEnhanced组件，将创建临时实例");
                    GameObject memoryGo = new GameObject("ESVMCP_Memory_Temp");
                    sceneMemory = memoryGo.AddComponent<ESVMCPMemoryEnhanced>();
                }

                // 加载持久记忆
                var persistentMemory = ESVMCPConfig.Instance.GetPersistentMemory();

                // 创建执行器
                var executor = new ESVMCPCommandExecutor(config, sceneMemory);

                // 执行JSON文件
                executionLog = $"开始执行: {Path.GetFileName(jsonFilePath)}\n";
                executionLog += $"时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
                executionLog += "====================\n\n";

                lastReport = executor.ExecuteJsonFile(jsonFilePath);

                // 显示报告
                executionLog += lastReport.GenerateReport();

                // 在控制台输出
                if (lastReport.Success)
                {
                    Debug.Log($"[ESVMCP] 执行成功: {lastReport.SuccessfulCommands}/{lastReport.TotalCommands} 命令");
                }
                else
                {
                    Debug.LogError($"[ESVMCP] 执行失败: {lastReport.ErrorMessage}");
                }

                // 标记场景为已修改
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
                );
            }
            catch (System.Exception e)
            {
                executionLog += $"\n\n异常: {e.Message}\n{e.StackTrace}";
                Debug.LogError($"[ESVMCP] 执行异常: {e.Message}");
            }
        }

        private void ExecuteBatch()
        {
            try
            {
                // 获取配置和记忆系统
                var config = ESVMCPConfig.Instance;
                if (config == null)
                {
                    executionLog = "错误: 未找到ESVMCPConfig配置\n";
                    return;
                }

                // 查找场景中的记忆组件
                var sceneMemory = FindObjectOfType<ESVMCPMemoryEnhanced>();
                if (sceneMemory == null)
                {
                    Debug.LogWarning("[ESVMCP] 场景中未找到ESVMCPMemoryEnhanced组件，将创建临时实例");
                    GameObject memoryGo = new GameObject("ESVMCP_Memory_Temp");
                    sceneMemory = memoryGo.AddComponent<ESVMCPMemoryEnhanced>();
                }

                // 加载持久记忆
                var persistentMemory = ESVMCPConfig.Instance.GetPersistentMemory();

                // 创建批量执行器
                var batchExecutor = new ESVMCPBatchExecutor(config, sceneMemory, persistentMemory);

                // 执行批量处理
                executionLog = $"开始批量执行 Input 文件夹: {config.InputFolder}\n";
                executionLog += $"时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
                executionLog += "====================\n\n";

                lastBatchReport = batchExecutor.ExecuteAllInputFiles();

                // 显示报告
                executionLog += lastBatchReport.GenerateReport();

                // 在控制台输出
                if (lastBatchReport.Success)
                {
                    Debug.Log($"[ESVMCP] 批量执行成功: {lastBatchReport.SuccessfulFiles}/{lastBatchReport.TotalFiles} 个文件");
                }
                else
                {
                    Debug.LogError($"[ESVMCP] 批量执行失败: {lastBatchReport.Message}");
                }

                // 标记场景为已修改
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
                );
            }
            catch (System.Exception e)
            {
                executionLog += $"\n\n异常: {e.Message}\n{e.StackTrace}";
                Debug.LogError($"[ESVMCP] 批量执行异常: {e.Message}");
            }
        }
    }

    /// <summary>
    /// ESVMCP快速执行菜单
    /// </summary>
    public static class ESVMCPQuickExecute
    {
        private static bool isBatchExecuting = false;


        [MenuItem(ESVMCPEditorTools.MenuRoot + MenuItemPathDefine.VMCP_ASSET_MANAGEMENT + "/记忆系统示例", priority = 21)]
        public static void ExecuteMemoryExample()
        {
            string path = Path.Combine(Application.dataPath, "ES/ESVMCP/Examples/example_using_memory.json");
            ExecuteJsonFile(path);
        }

        [MenuItem(ESVMCPEditorTools.MenuRoot + "【批量执行】", priority = 3)]
        public static void ExecuteBatchFromMenu()
        {
            ExecuteBatchFiles();
        }

        [MenuItem(ESVMCPEditorTools.MenuRoot + "【清空Input文件夹】", priority = 14)]
        public static void ClearInputFolder()
        {
            var config = ESVMCPConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到ESVMCPConfig配置", "确定");
                return;
            }

            if (!Directory.Exists(config.InputFolder))
            {
                EditorUtility.DisplayDialog("提示", "Input文件夹不存在", "确定");
                return;
            }

            var jsonFiles = Directory.GetFiles(config.InputFolder, "*.json");
            if (jsonFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "Input文件夹已经是空的", "确定");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog("确认清空",
                $"即将删除 {jsonFiles.Length} 个JSON文件\n\n这些文件将被永久删除，无法恢复\n\n确定继续吗？",
                "确定", "取消");

            if (!confirm)
                return;

            try
            {
                int deletedCount = 0;
                foreach (var file in jsonFiles)
                {
                    File.Delete(file);
                    deletedCount++;
                }

                EditorUtility.DisplayDialog("清空完成",
                    $"已删除 {deletedCount} 个JSON文件\n\nInput文件夹已清空",
                    "确定");

                Debug.Log($"[ESVMCP] 已清空Input文件夹，删除了 {deletedCount} 个文件");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("清空失败", $"删除文件时出错：\n{e.Message}", "确定");
                Debug.LogError($"[ESVMCP] 清空Input文件夹失败: {e.Message}");
            }
        }

        [MenuItem(ESVMCPEditorTools.MenuRoot + "【单独执行JSON】", priority = 15)]
        public static void ExecuteSingleJsonFromMenu()
        {
            var config = ESVMCPConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到ESVMCPConfig配置", "确定");
                return;
            }

            if (!Directory.Exists(config.InputFolder))
            {
                EditorUtility.DisplayDialog("错误", $"Input文件夹不存在: {config.InputFolder}", "确定");
                return;
            }

            var jsonFiles = Directory.GetFiles(config.InputFolder, "*.json");
            if (jsonFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "Input文件夹中没有JSON文件", "确定");
                return;
            }

            // 创建选择菜单
            var menu = new GenericMenu();

            foreach (var file in jsonFiles)
            {
                string fileName = Path.GetFileName(file);
                menu.AddItem(new GUIContent(fileName), false, () => ExecuteJsonFile(file));
            }

            menu.ShowAsContext();
        }

        private static void ExecuteJsonFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[ESVMCP] 文件不存在: {path}");
                return;
            }

            var config = ESVMCPConfig.Instance;
            var sceneMemory = Object.FindObjectOfType<ESVMCPMemoryEnhanced>();
            var persistentMemory = ESVMCPConfig.Instance.GetPersistentMemory();

            if (sceneMemory == null)
            {
                GameObject memoryGo = new GameObject("ESVMCP_Memory_Temp");
                sceneMemory = memoryGo.AddComponent<ESVMCPMemoryEnhanced>();
            }

            var executor = new ESVMCPCommandExecutor(config, sceneMemory);
            var report = executor.ExecuteJsonFile(path);

            Debug.Log($"[ESVMCP] {Path.GetFileName(path)}\n{report.GenerateReport()}");

            if (report.Success)
            {
                EditorUtility.DisplayDialog("执行成功", 
                    $"成功执行 {report.SuccessfulCommands}/{report.TotalCommands} 条命令\n耗时: {report.Duration.TotalSeconds:F2}秒", 
                    "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("执行失败", 
                    $"失败: {report.ErrorMessage}\n成功: {report.SuccessfulCommands}/{report.TotalCommands}", 
                    "确定");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
            );
        }

        private static void ExecuteBatchFiles()
        {
            if (isBatchExecuting)
            {
                EditorUtility.DisplayDialog("提示", "批量执行正在进行中，请等待完成", "确定");
                return;
            }

            var config = ESVMCPConfig.Instance;
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到ESVMCPConfig配置", "确定");
                return;
            }

            if (!Directory.Exists(config.InputFolder))
            {
                EditorUtility.DisplayDialog("错误", $"Input文件夹不存在: {config.InputFolder}", "确定");
                return;
            }

            var jsonFiles = Directory.GetFiles(config.InputFolder, "*.json");
            if (jsonFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "Input文件夹中没有JSON文件", "确定");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog("批量执行确认",
                $"即将执行 {jsonFiles.Length} 个JSON文件\n\n文件将按顺序执行，成功后移动到Archive文件夹\n\n确定继续吗？",
                "确定", "取消");

            if (!confirm)
                return;

            isBatchExecuting = true;

            try
            {
                var sceneMemory = Object.FindObjectOfType<ESVMCPMemoryEnhanced>();
                var persistentMemory = ESVMCPConfig.Instance.GetPersistentMemory();

                if (sceneMemory == null)
                {
                    GameObject memoryGo = new GameObject("ESVMCP_Memory_Temp");
                    sceneMemory = memoryGo.AddComponent<ESVMCPMemoryEnhanced>();
                }

                var batchExecutor = new ESVMCPBatchExecutor(config, sceneMemory, persistentMemory);
                var batchReport = batchExecutor.ExecuteAllInputFiles();

                // 生成详细报告
                string detailedReport = $"批量执行完成 - {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
                detailedReport += $"总文件数: {batchReport.TotalFiles}\n";
                detailedReport += $"成功文件数: {batchReport.SuccessfulFiles}\n";
                detailedReport += $"失败文件数: {batchReport.FailedFiles}\n";
                detailedReport += $"跳过文件数: {batchReport.SkippedFiles}\n";
                detailedReport += $"无效文件数: {batchReport.InvalidFiles}\n";
                detailedReport += $"总耗时: {batchReport.Duration.TotalSeconds:F2}秒\n\n";

                if (batchReport.ExecutionReports != null && batchReport.ExecutionReports.Count > 0)
                {
                    detailedReport += "详细执行结果:\n";
                    foreach (var executionReport in batchReport.ExecutionReports)
                    {
                        string status = executionReport.Success ? "✓" : "✗";
                        detailedReport += $"{status} {Path.GetFileName(executionReport.SourceFile)}\n";
                        if (!executionReport.Success && !string.IsNullOrEmpty(executionReport.ErrorMessage))
                        {
                            detailedReport += $"  错误: {executionReport.ErrorMessage}\n";
                        }
                        detailedReport += $"  耗时: {executionReport.Duration.TotalSeconds:F2}秒\n";
                        detailedReport += $"  命令: {executionReport.SuccessfulCommands}/{executionReport.TotalCommands} 成功\n\n";
                    }
                }

                if (batchReport.FailedFileInfos.Count > 0)
                {
                    detailedReport += "失败的文件:\n";
                    foreach (var failedFile in batchReport.FailedFileInfos)
                    {
                        detailedReport += $"✗ {Path.GetFileName(failedFile.FilePath)}: {failedFile.Reason}\n";
                    }
                    detailedReport += "\n";
                }

                if (batchReport.SkippedFileInfos.Count > 0)
                {
                    detailedReport += "跳过的文件:\n";
                    foreach (var skippedFile in batchReport.SkippedFileInfos)
                    {
                        detailedReport += $"⚠ {Path.GetFileName(skippedFile.FilePath)}: {skippedFile.Reason}\n";
                    }
                    detailedReport += "\n";
                }

                if (batchReport.InvalidFileInfos.Count > 0)
                {
                    detailedReport += "无效的文件:\n";
                    foreach (var invalidFile in batchReport.InvalidFileInfos)
                    {
                        detailedReport += $"❌ {Path.GetFileName(invalidFile.FilePath)}: {invalidFile.Reason}\n";
                    }
                    detailedReport += "\n";
                }

                Debug.Log($"[ESVMCP] 批量执行完成\n{detailedReport}");

                if (batchReport.Success)
                {
                    EditorUtility.DisplayDialog("批量执行成功",
                        $"成功执行 {batchReport.SuccessfulFiles}/{batchReport.TotalFiles} 个文件\n耗时: {batchReport.Duration.TotalSeconds:F2}秒\n\n详细结果请查看控制台日志",
                        "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("批量执行完成",
                        $"{batchReport.Message}\n\n成功: {batchReport.SuccessfulFiles}/{batchReport.TotalFiles}\n\n详细结果请查看控制台日志",
                        "确定");
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
                );
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ESVMCP] 批量执行异常: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("批量执行异常", $"执行过程中发生异常: {e.Message}", "确定");
            }
            finally
            {
                isBatchExecuting = false;
            }
        }
    }
}
