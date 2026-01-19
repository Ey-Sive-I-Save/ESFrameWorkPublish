using UnityEngine;
using System;
using Sirenix.OdinInspector;

namespace ES.VMCP
{
    /// <summary>
    /// ESVMCP配置文件
    /// </summary>
    [CreateAssetMenu(fileName = "ESVMCPConfig", menuName = "ES/VMCP/配置文件")]
    public class ESVMCPConfig : ESEditorGlobalSo<ESVMCPConfig>
    {
        [Title("文件路径配置")]
        [FolderPath]
        [LabelText("基础文件夹")]
        [InfoBox("ESVMCP数据存储的基础文件夹，所有子文件夹将自动创建在此文件夹下")]
        public string BaseFolder = "Assets/ES/ESVMCP/RunningData";

        [ReadOnly]
        [LabelText("输入文件夹")]
        [InfoBox("放置待执行的JSON命令文件")]
        public string InputFolder => System.IO.Path.Combine(BaseFolder, "Input");

        [ReadOnly]
        [LabelText("归档文件夹")]
        [InfoBox("执行完成后的JSON文件归档位置")]
        public string ArchiveFolder => System.IO.Path.Combine(BaseFolder, "Archive");

        [ReadOnly]
        [LabelText("记忆导出文件夹")]
        [InfoBox("导出的记忆文件存储位置")]
        public string MemoryFolder => System.IO.Path.Combine(BaseFolder, "Memory");

        [ReadOnly]
        [LabelText("日志文件夹")]
        [InfoBox("日志文件存储位置")]
        public string LogFolder => System.IO.Path.Combine(BaseFolder, "Logs");

        [Title("监视器设置")]
        [LabelText("检查间隔(秒)")]
        [Range(0.1f, 10f)]
        [InfoBox("监视输入文件夹的时间间隔")]
        public float CheckInterval = 1.0f;

        [LabelText("自动执行")]
        [InfoBox("检测到新文件时自动执行")]
        public bool AutoExecute = true;

        [LabelText("执行前验证")]
        [InfoBox("执行命令前进行格式和参数验证")]
        public bool ValidateBeforeExecute = true;

        [Title("执行设置")]
        [LabelText("遇错停止")]
        [InfoBox("遇到错误时停止执行后续命令")]
        public bool StopOnError = false;

        [LabelText("单文件最大命令数")]
        [Range(1, 1000)]
        [InfoBox("单个JSON文件允许的最大命令数量")]
        public int MaxCommandsPerFile = 100;

        [LabelText("命令延迟(秒)")]
        [Range(0f, 5f)]
        [InfoBox("命令之间的执行延迟")]
        public float CommandDelay = 0.1f;

        [Title("记忆系统")]
        [LabelText("启用记忆")]
        [InfoBox("启用记忆系统功能")]
        public bool EnableMemory = true;

        [LabelText("最大记忆条目")]
        [Range(100, 10000)]
        [InfoBox("记忆系统保存的最大条目数")]
        public int MaxMemoryEntries = 1000;

        [LabelText("自动导出记忆")]
        [InfoBox("执行命令后自动导出记忆到文本文件")]
        public bool AutoExportMemory = true;

        [LabelText("记忆保留天数")]
        [Range(1, 90)]
        [InfoBox("自动清理多少天前的记忆")]
        public int MemoryRetentionDays = 30;

        [Title("日志设置")]
        [LabelText("启用详细日志")]
        [InfoBox("记录详细的执行日志")]
        public bool EnableVerboseLogging = true;

        [Title("安全设置")]
        [LabelText("启用命令白名单")]
        [InfoBox("只允许白名单中的命令执行")]
        public bool EnableCommandWhitelist = false;

        [LabelText("危险操作警告")]
        [InfoBox("执行危险操作前显示警告")]
        public bool WarnDangerousOperations = true;

        [Title("AI集成")]
        [LabelText("AI指导文档路径")]
        [InfoBox("AI交互指导文档的路径，用于【AI指导】功能")]
        [FilePath]
        public string AIGuidanceDocumentPath = "Assets/ES/ESVMCP/AI_INTERACTION_GUIDE.md";

        /// <summary>
        /// 获取完整路径
        /// </summary>
        public string GetFullPath(string relativePath)
        {
            return System.IO.Path.Combine(Application.dataPath, "..", relativePath);
        }

        /// <summary>
        /// 确保文件夹存在
        /// </summary>
        public void EnsureFoldersExist()
        {
            EnsureFolderExists(InputFolder);
            EnsureFolderExists(ArchiveFolder);
            EnsureFolderExists(MemoryFolder);
            EnsureFolderExists(LogFolder);
        }

        private void EnsureFolderExists(string folder)
        {
            string fullPath = GetFullPath(folder);
            if (!System.IO.Directory.Exists(fullPath))
            {
                System.IO.Directory.CreateDirectory(fullPath);
                Debug.Log($"[ESVMCP] 创建文件夹: {fullPath}");
            }
        }

        [Button("创建默认文件夹", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void CreateDefaultFolders()
        {
            EnsureFoldersExist();
            Debug.Log("[ESVMCP] 默认文件夹创建完成！");
        }

        [Button("打开输入文件夹", ButtonSizes.Medium)]
        private void OpenInputFolder()
        {
            string fullPath = GetFullPath(InputFolder);
            if (System.IO.Directory.Exists(fullPath))
            {
                System.Diagnostics.Process.Start(fullPath);
            }
            else
            {
                Debug.LogWarning($"[ESVMCP] 文件夹不存在: {fullPath}");
            }
        }

        [Button("打开归档文件夹", ButtonSizes.Medium)]
        private void OpenArchiveFolder()
        {
            string fullPath = GetFullPath(ArchiveFolder);
            if (System.IO.Directory.Exists(fullPath))
            {
                System.Diagnostics.Process.Start(fullPath);
            }
            else
            {
                Debug.LogWarning($"[ESVMCP] 文件夹不存在: {fullPath}");
            }
        }
    }
}
