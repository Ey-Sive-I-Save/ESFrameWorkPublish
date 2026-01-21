using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Sirenix.OdinInspector;

namespace ES.VMCP
{
    /// <summary>
    /// ESVMCP批量执行器 - 批量处理Input文件夹中的JSON文件
    /// </summary>
    public class ESVMCPBatchExecutor
    {
        private ESVMCPConfig config;
        private ESVMCPMemoryEnhanced sceneMemory;
        private ESVMCPMemoryAssetEnhanced persistentMemory;

        public ESVMCPBatchExecutor(ESVMCPConfig config, ESVMCPMemoryEnhanced sceneMemory = null, ESVMCPMemoryAssetEnhanced persistentMemory = null)
        {
            this.config = config;
            this.sceneMemory = sceneMemory;
            this.persistentMemory = persistentMemory;
        }

        /// <summary>
        /// 执行Input文件夹中的所有JSON文件
        /// </summary>
        public ESVMCPBatchReport ExecuteAllInputFiles()
        {
            var batchReport = new ESVMCPBatchReport
            {
                StartTime = DateTime.Now,
                InputFolder = config.InputFolder
            };

            try
            {
                // 1. 获取所有JSON文件
                var jsonFiles = GetJsonFilesFromInput();
                batchReport.TotalFiles = jsonFiles.Count;

                if (jsonFiles.Count == 0)
                {
                    batchReport.Success = true;
                    batchReport.Message = "Input文件夹中没有找到JSON文件";
                    batchReport.EndTime = DateTime.Now;
                    return batchReport;
                }

                // 2. 解析并排序JSON文档
                var documents = ParseAndSortDocuments(jsonFiles, batchReport);
                if (documents.Count == 0)
                {
                    batchReport.Success = false;
                    batchReport.Message = "没有有效的JSON文档可以执行";
                    batchReport.EndTime = DateTime.Now;
                    return batchReport;
                }

                // 3. 逐个执行文档
                var executor = new ESVMCPCommandExecutor(config, sceneMemory);
                foreach (var docInfo in documents)
                {
                    if (!docInfo.Document.Enabled)
                    {
                        batchReport.AddSkippedFile(docInfo.FilePath, "文件被禁用");
                        continue;
                    }

                    try
                    {
                        Debug.Log($"[ESVMCP] 执行文件: {Path.GetFileName(docInfo.FilePath)} (顺序: {docInfo.Document.Order})");
                        var report = executor.ExecuteJsonFile(docInfo.FilePath);

                        batchReport.AddExecutionReport(report);

                        if (report.Success)
                        {
                            // 移动到Archive文件夹
                            MoveToArchive(docInfo.FilePath);
                            Debug.Log($"[ESVMCP] 文件执行成功，已移动到Archive: {Path.GetFileName(docInfo.FilePath)}");
                        }
                        else
                        {
                            Debug.LogError($"[ESVMCP] 文件执行失败: {Path.GetFileName(docInfo.FilePath)} - {report.ErrorMessage}");

                            // 输出失败命令详情
                            if (report.FailedCommands > 0)
                            {
                                Debug.LogError($"[ESVMCP] 失败命令详情 ({report.FailedCommands}/{report.TotalCommands}):");
                                for (int i = 0; i < report.CommandResults.Count; i++)
                                {
                                    var cmdResult = report.CommandResults[i];
                                    if (!cmdResult.Success)
                                    {
                                        Debug.LogError($"  命令 #{i + 1} [{cmdResult.CommandType}] ID:{cmdResult.CommandId} - {cmdResult.Message}");
                                    }
                                }
                            }

                            if (config.StopOnError)
                            {
                                batchReport.Success = false;
                                batchReport.Message = $"执行被错误终止: {report.ErrorMessage}";
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        batchReport.AddFailedFile(docInfo.FilePath, ex.Message);
                        Debug.LogError($"[ESVMCP] 执行文件异常: {Path.GetFileName(docInfo.FilePath)} - {ex.Message}");

                        if (config.StopOnError)
                        {
                            batchReport.Success = false;
                            batchReport.Message = $"执行被异常终止: {ex.Message}";
                            break;
                        }
                    }
                }

                batchReport.Success = batchReport.FailedFiles == 0;
                batchReport.Message = batchReport.Success ?
                    $"批量执行完成: {batchReport.SuccessfulFiles}/{batchReport.TotalFiles} 个文件成功" :
                    $"批量执行失败: {batchReport.FailedFiles}/{batchReport.TotalFiles} 个文件失败";

            }
            catch (Exception ex)
            {
                batchReport.Success = false;
                batchReport.Message = $"批量执行异常: {ex.Message}";
                Debug.LogError($"[ESVMCP] 批量执行异常: {ex.Message}\n{ex.StackTrace}");
            }

            batchReport.EndTime = DateTime.Now;
            return batchReport;
        }

        /// <summary>
        /// 获取Input文件夹中的所有JSON文件
        /// </summary>
        private List<string> GetJsonFilesFromInput()
        {
            if (!Directory.Exists(config.InputFolder))
            {
                Debug.LogWarning($"[ESVMCP] Input文件夹不存在: {config.InputFolder}");
                return new List<string>();
            }

            return Directory.GetFiles(config.InputFolder, "*.json")
                .OrderBy(f => Path.GetFileName(f))
                .ToList();
        }

        /// <summary>
        /// 解析并排序JSON文档
        /// </summary>
        private List<DocumentInfo> ParseAndSortDocuments(List<string> jsonFiles, ESVMCPBatchReport batchReport)
        {
            var documents = new List<DocumentInfo>();

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    string jsonContent = File.ReadAllText(filePath);
                    var document = JsonConvert.DeserializeObject<ESVMCPJsonDocument>(jsonContent);

                    if (document == null)
                    {
                        batchReport.AddInvalidFile(filePath, "JSON解析失败");
                        continue;
                    }

                    if (string.IsNullOrEmpty(document.CommandId))
                    {
                        batchReport.AddInvalidFile(filePath, "缺少commandId");
                        continue;
                    }

                    documents.Add(new DocumentInfo
                    {
                        FilePath = filePath,
                        Document = document
                    });
                }
                catch (Exception ex)
                {
                    batchReport.AddInvalidFile(filePath, $"解析异常: {ex.Message}");
                }
            }

            // 按顺序排序，相同顺序则按文件名排序
            return documents
                .OrderBy(d => d.Document.Order)
                .ThenBy(d => Path.GetFileName(d.FilePath))
                .ToList();
        }

        /// <summary>
        /// 移动文件和对应的.meta文件到指定文件夹
        /// </summary>
        private void MoveFileWithMeta(string sourceFilePath, string targetDirectory)
        {
            try
            {
                string fileName = Path.GetFileName(sourceFilePath);
                string targetPath = Path.Combine(targetDirectory, fileName);

                // 确保目标文件夹存在
                Directory.CreateDirectory(targetDirectory);

                // 检查是否存在对应的.meta文件
                string metaFilePath = sourceFilePath + ".meta";
                string metaTargetPath = targetPath + ".meta";

                // 如果目标文件已存在，添加时间戳
                if (File.Exists(targetPath))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string extension = Path.GetExtension(fileName);
                    string newFileName = $"{fileNameWithoutExt}_{timestamp}{extension}";
                    targetPath = Path.Combine(targetDirectory, newFileName);
                    metaTargetPath = targetPath + ".meta";
                }

                // 移动JSON文件
                File.Move(sourceFilePath, targetPath);
                Debug.Log($"[ESVMCP] 文件已移动: {Path.GetFileName(targetPath)}");

                // 如果存在.meta文件，也一起移动
                if (File.Exists(metaFilePath))
                {
                    File.Move(metaFilePath, metaTargetPath);
                    Debug.Log($"[ESVMCP] .meta文件已同时移动: {Path.GetFileName(metaTargetPath)}");
                }
                else
                {
                    Debug.LogWarning($"[ESVMCP] 未找到对应的.meta文件: {fileName}.meta");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ESVMCP] 移动文件失败: {Path.GetFileName(sourceFilePath)} - {ex.Message}");
            }
        }

        /// <summary>
        /// 移动文件到Archive文件夹
        /// </summary>
        private void MoveToArchive(string filePath)
        {
            MoveFileWithMeta(filePath, config.ArchiveFolder);
        }

        /// <summary>
        /// 文档信息
        /// </summary>
        private class DocumentInfo
        {
            public string FilePath { get; set; }
            public ESVMCPJsonDocument Document { get; set; }
        }
    }

    /// <summary>
    /// 批量执行报告
    /// </summary>
    public class ESVMCPBatchReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string InputFolder { get; set; }

        public int TotalFiles { get; set; }
        public int SuccessfulFiles => ExecutionReports.Count(r => r.Success);
        public int FailedFiles => FailedFileInfos.Count;
        public int SkippedFiles => SkippedFileInfos.Count;
        public int InvalidFiles => InvalidFileInfos.Count;

        public List<ESVMCPExecutionReport> ExecutionReports { get; } = new List<ESVMCPExecutionReport>();
        public List<FileInfo> FailedFileInfos { get; } = new List<FileInfo>();
        public List<FileInfo> SkippedFileInfos { get; } = new List<FileInfo>();
        public List<FileInfo> InvalidFileInfos { get; } = new List<FileInfo>();

        public TimeSpan Duration => EndTime - StartTime;

        public void AddExecutionReport(ESVMCPExecutionReport report)
        {
            ExecutionReports.Add(report);
        }

        public void AddFailedFile(string filePath, string reason)
        {
            FailedFileInfos.Add(new FileInfo { FilePath = filePath, Reason = reason });
        }

        public void AddSkippedFile(string filePath, string reason)
        {
            SkippedFileInfos.Add(new FileInfo { FilePath = filePath, Reason = reason });
        }

        public void AddInvalidFile(string filePath, string reason)
        {
            InvalidFileInfos.Add(new FileInfo { FilePath = filePath, Reason = reason });
        }

        public string GenerateReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ESVMCP 批量执行报告 ===");
            sb.AppendLine($"开始时间: {StartTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"结束时间: {EndTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"总耗时: {Duration.TotalSeconds:F2}秒");
            sb.AppendLine($"Input文件夹: {InputFolder}");
            sb.AppendLine();
            sb.AppendLine($"文件统计:");
            sb.AppendLine($"  总文件数: {TotalFiles}");
            sb.AppendLine($"  成功执行: {SuccessfulFiles}");
            sb.AppendLine($"  执行失败: {FailedFiles}");
            sb.AppendLine($"  被跳过: {SkippedFiles}");
            sb.AppendLine($"  无效文件: {InvalidFiles}");
            sb.AppendLine();
            sb.AppendLine($"状态: {(Success ? "成功" : "失败")}");
            sb.AppendLine($"消息: {Message}");

            if (FailedFileInfos.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("失败的文件:");
                foreach (var info in FailedFileInfos)
                {
                    sb.AppendLine($"  - {Path.GetFileName(info.FilePath)}: {info.Reason}");
                }
            }

            if (SkippedFileInfos.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("跳过的文件:");
                foreach (var info in SkippedFileInfos)
                {
                    sb.AppendLine($"  - {Path.GetFileName(info.FilePath)}: {info.Reason}");
                }
            }

            if (InvalidFileInfos.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("无效的文件:");
                foreach (var info in InvalidFileInfos)
                {
                    sb.AppendLine($"  - {Path.GetFileName(info.FilePath)}: {info.Reason}");
                }
            }

            return sb.ToString();
        }

        public class FileInfo
        {
            public string FilePath { get; set; }
            public string Reason { get; set; }
        }
    }
}