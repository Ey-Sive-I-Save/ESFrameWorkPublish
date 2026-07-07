using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ES.VMCP
{
    /// <summary>
    /// ESVMCP命令执行器 - 负责执行JSON指令文件
    /// </summary>
    public class ESVMCPCommandExecutor
    {
        private ESVMCPConfig config;
        private ESVMCPMemoryEnhanced sceneMemory;
        private ESVMCPMemoryAssetEnhanced persistentMemory;

        public ESVMCPCommandExecutor(ESVMCPConfig config, ESVMCPMemoryEnhanced sceneMemory = null)
        {
            this.config = config;
            this.sceneMemory = sceneMemory;
            // 从全局配置获取持久记忆
            this.persistentMemory = config.GetPersistentMemory();
        }

        /// <summary>
        /// 执行JSON指令文件
        /// </summary>
        public ESVMCPExecutionReport ExecuteJsonFile(string jsonFilePath)
        {
            var report = new ESVMCPExecutionReport
            {
                SourceFile = jsonFilePath,
                StartTime = DateTime.Now
            };

            try
            {
                // 读取JSON文件
                string jsonContent = System.IO.File.ReadAllText(jsonFilePath);
                
                // 解析JSON文档
                var jsonDoc = JsonConvert.DeserializeObject<ESVMCPJsonDocument>(jsonContent);
                if (jsonDoc == null)
                {
                    report.Success = false;
                    report.ErrorMessage = "JSON文档解析失败";
                    return report;
                }

                report.CommandId = jsonDoc.CommandId;
                report.Description = jsonDoc.Description;

                // 处理memory.load（从记忆加载数据）
                if (jsonDoc.Memory.Load.Count > 0)
                {
                    LoadMemories(jsonDoc.Memory.Load, report);
                }

                // 创建执行上下文
                var context = new ESVMCPExecutionContext
                {
                    Config = config,
                    SceneMemory = sceneMemory,
                    PersistentMemory = persistentMemory,
                    TotalCommands = jsonDoc.Commands.Count
                };

                // 执行命令列表
                if (jsonDoc.Commands.Count > 0)
                {
                    for (int i = 0; i < jsonDoc.Commands.Count; i++)
                    {
                        context.CurrentCommandIndex = i;
                        var command = jsonDoc.Commands[i];

                        if (command == null)
                        {
                            report.AddCommandResult(null, false, $"命令 #{i+1} 解析失败");
                            continue;
                        }

                        // 验证命令
                        var validation = command.Validate();
                        if (!validation.IsValid)
                        {
                            string errors = string.Join("; ", validation.Errors);
                            report.AddCommandResult(command, false, $"验证失败: {errors}");
                            
                            if (config.StopOnError)
                            {
                                report.ErrorMessage = $"命令验证失败，停止执行: {errors}";
                                break;
                            }
                            continue;
                        }

                        // 执行命令
                        try
                        {
                            var result = command.Execute(context);
                            
                            // 保存命令结果到上下文
                            if (!string.IsNullOrEmpty(command.Id))
                            {
                                context.CommandResults[command.Id] = result;
                            }

                            // 命令执行后的记忆保存（多态调用）
                            command.TrySaveToMemory(result, context);

                            // 记录结果
                            report.AddCommandResult(command, result.Success, result.Message, result.OutputData);

                            // 如果失败且配置为停止，则中断
                            if (!result.Success && config.StopOnError)
                            {
                                report.ErrorMessage = $"命令执行失败，停止执行: {result.Message}";
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            report.AddCommandResult(command, false, $"异常: {ex.Message}");
                            Debug.LogError($"[ESVMCP] 命令执行异常: {ex.Message}\n{ex.StackTrace}");
                            
                            if (config.StopOnError)
                            {
                                report.ErrorMessage = $"命令执行异常，停止执行: {ex.Message}";
                                break;
                            }
                        }
                    }
                }

                // 设置执行成功标志
                report.Success = report.FailedCommands == 0;
                report.EndTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.ErrorMessage = $"执行异常: {ex.Message}";
                report.EndTime = DateTime.Now;
                Debug.LogError($"[ESVMCP] 执行JSON文件失败: {ex.Message}\n{ex.StackTrace}");
            }

            return report;
        }

        /// <summary>
        /// 从记忆加载数据到上下文（优先短期，再长期）
        /// </summary>
        private void LoadMemories(List<string> keysToLoad, ESVMCPExecutionReport report)
        {
            // 注意：此方法主要用于预检查记忆键是否存在
            // 实际的数据访问通过命令中的 {{memory.xxx}} 变量引用完成
            foreach (var key in keysToLoad)
            {
                bool exists = false;
                
                // 检查场景记忆（短期）
                if (sceneMemory != null && sceneMemory.Has(key))
                {
                    exists = true;
                    var item = sceneMemory.GetMemoryItem(key);
                    report.AddLog($"✓ 短期记忆可用: {key} = {item?.Resolve()}");
                }
                // 检查持久记忆（长期）
                else if (persistentMemory != null && persistentMemory.Has(key))
                {
                    exists = true;
                    var item = persistentMemory.GetMemoryItem(key);
                    report.AddLog($"✓ 长期记忆可用: {key} = {item?.Resolve()}");
                }
                
                if (!exists)
                {
                    report.AddLog($"⚠ 记忆键不存在: '{key}' (将在后续命令中创建)");
                }
            }
        }
    }

    /// <summary>
    /// JSON文档结构（顶层）
    /// </summary>
    public class ESVMCPJsonDocument
    {
        [JsonProperty("commandId")]
        public string CommandId { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("order")]
        public int Order { get; set; } = 0;

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("memory")]
        public ESVMCPMemorySection Memory { get; set; } = new ESVMCPMemorySection();

        [JsonProperty("commands")]
        public List<ESVMCPCommandBase> Commands { get; set; } = new List<ESVMCPCommandBase>();
    }

    /// <summary>
    /// JSON文档中的memory部分
    /// </summary>
    public class ESVMCPMemorySection
    {
        [JsonProperty("load")]
        public List<string> Load { get; set; } = new List<string>();

        [JsonProperty("save")]
        public Dictionary<string, object> Save { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 执行报告
    /// </summary>
    public class ESVMCPExecutionReport
    {
        public string SourceFile { get; set; }
        public string CommandId { get; set; }
        public string Description { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;

        public int TotalCommands => CommandResults.Count;
        public int SuccessfulCommands => CommandResults.Count(r => r.Success);
        public int FailedCommands => CommandResults.Count(r => !r.Success);

        public List<CommandResultRecord> CommandResults { get; set; } = new List<CommandResultRecord>();
        public List<string> Logs { get; set; } = new List<string>();

        public void AddCommandResult(ESVMCPCommandBase command, bool success, string message, Dictionary<string, object> outputData = null)
        {
            CommandResults.Add(new CommandResultRecord
            {
                CommandType = command?.Type ?? "Unknown",
                CommandId = command?.Id ?? "",
                Success = success,
                Message = message,
                OutputData = outputData
            });
        }

        public void AddLog(string log)
        {
            Logs.Add($"[{DateTime.Now:HH:mm:ss}] {log}");
        }

        public string GenerateReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== ESVMCP 执行报告 ===");
            report.AppendLine($"文件: {SourceFile}");
            report.AppendLine($"命令ID: {CommandId}");
            report.AppendLine($"描述: {Description}");
            report.AppendLine($"执行时间: {StartTime:yyyy-MM-dd HH:mm:ss} - {EndTime:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"耗时: {Duration.TotalSeconds:F2}秒");
            report.AppendLine($"状态: {(Success ? "✓ 成功" : "✗ 失败")}");
            report.AppendLine($"命令统计: {SuccessfulCommands}/{TotalCommands} 成功, {FailedCommands} 失败");
            
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                report.AppendLine($"错误: {ErrorMessage}");
            }

            report.AppendLine("\n=== 命令执行详情 ===");
            for (int i = 0; i < CommandResults.Count; i++)
            {
                var result = CommandResults[i];
                string status = result.Success ? "✓" : "✗";
                report.AppendLine($"{i + 1}. [{status}] {result.CommandType} (ID: {result.CommandId})");
                report.AppendLine($"   {result.Message}");
            }

            // 如果执行失败，专门输出所有失败的命令详情
            if (!Success && FailedCommands > 0)
            {
                report.AppendLine("\n=== ❌ 失败命令详情 ===");
                report.AppendLine($"共 {FailedCommands} 个命令执行失败:");
                report.AppendLine();

                for (int i = 0; i < CommandResults.Count; i++)
                {
                    var result = CommandResults[i];
                    if (!result.Success)
                    {
                        report.AppendLine($"🔴 失败命令 #{i + 1}: {result.CommandType}");
                        if (!string.IsNullOrEmpty(result.CommandId))
                        {
                            report.AppendLine($"   命令ID: {result.CommandId}");
                        }
                        report.AppendLine($"   错误信息: {result.Message}");

                        // 如果有输出数据，也显示出来
                        if (result.OutputData != null && result.OutputData.Count > 0)
                        {
                            report.AppendLine("   输出数据:");
                            foreach (var kvp in result.OutputData)
                            {
                                report.AppendLine($"     {kvp.Key}: {kvp.Value}");
                            }
                        }
                        report.AppendLine();
                    }
                }
            }

            if (Logs.Count > 0)
            {
                report.AppendLine("\n=== 日志 ===");
                foreach (var log in Logs)
                {
                    report.AppendLine(log);
                }
            }

            return report.ToString();
        }
    }

    /// <summary>
    /// 命令执行结果记录
    /// </summary>
    public class CommandResultRecord
    {
        public string CommandType { get; set; }
        public string CommandId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> OutputData { get; set; }
    }
}
