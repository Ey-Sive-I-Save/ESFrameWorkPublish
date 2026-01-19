using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

namespace ES.VMCP
{
    /// <summary>
    /// 持久记忆资产 - 跨场景记忆
    /// </summary>
    [CreateAssetMenu(fileName = "ESVMCPMemoryAsset", menuName = "ES/VMCP/记忆资产")]
    public class ESVMCPMemoryAsset : ScriptableObject
    {
        [Title("持久记忆")]
        [InfoBox("跨场景和会话的持久化记忆数据")]

        [ShowInInspector]
        [DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.Foldout)]
        [LabelText("记忆数据")]
        public Dictionary<string, string> Memory = new Dictionary<string, string>();

        [ShowInInspector]
        [ListDrawerSettings(ShowFoldout = true)]
        [LabelText("操作记录")]
        public List<MemoryRecord> Records = new List<MemoryRecord>();

        [Title("统计")]
        [ShowInInspector, ReadOnly]
        public int TotalCommands => totalCommandsExecuted;

        [ShowInInspector, ReadOnly]
        public string LastSession => lastSessionTime;

        [ShowInInspector, ReadOnly]
        public int MemoryCount => Memory.Count;

        [SerializeField]
        private int totalCommandsExecuted = 0;

        [SerializeField]
        private string lastSessionTime = "";

        [Title("设置")]
        [LabelText("最大记录数")]
        public int MaxRecords = 1000;

        [LabelText("自动清理旧记录")]
        public bool AutoCleanupOldRecords = true;

        [LabelText("记录保留天数")]
        public int RetentionDays = 30;

        /// <summary>
        /// 保存记忆
        /// </summary>
        public void SaveMemory(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[ESVMCP MemoryAsset] 记忆键不能为空");
                return;
            }

            Memory[key] = value != null ? value.ToString() : "";
            
            AddRecord(new MemoryRecord
            {
                Type = MemoryRecordType.Save,
                Key = key,
                Value = value?.ToString(),
                Timestamp = DateTime.Now
            });

            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// 获取记忆
        /// </summary>
        public object GetMemory(string key)
        {
            if (Memory.TryGetValue(key, out string value))
            {
                AddRecord(new MemoryRecord
                {
                    Type = MemoryRecordType.Load,
                    Key = key,
                    Timestamp = DateTime.Now
                });
                return value;
            }
            return null;
        }

        /// <summary>
        /// 检查记忆是否存在
        /// </summary>
        public bool HasMemory(string key)
        {
            return Memory.ContainsKey(key);
        }

        /// <summary>
        /// 删除记忆
        /// </summary>
        public void RemoveMemory(string key)
        {
            if (Memory.Remove(key))
            {
                AddRecord(new MemoryRecord
                {
                    Type = MemoryRecordType.Remove,
                    Key = key,
                    Timestamp = DateTime.Now
                });
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// 添加记录
        /// </summary>
        public void AddRecord(MemoryRecord record)
        {
            Records.Add(record);
            
            if (AutoCleanupOldRecords)
            {
                CleanupOldRecords();
            }

            if (Records.Count > MaxRecords)
            {
                Records.RemoveAt(0);
            }
        }

        /// <summary>
        /// 记录命令执行
        /// </summary>
        public void RecordCommandExecution(string commandType, bool success)
        {
            totalCommandsExecuted++;
            lastSessionTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            AddRecord(new MemoryRecord
            {
                Type = MemoryRecordType.Command,
                Key = commandType,
                Value = success ? "Success" : "Failed",
                Timestamp = DateTime.Now
            });

            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// 清理旧记录
        /// </summary>
        private void CleanupOldRecords()
        {
            DateTime cutoffDate = DateTime.Now.AddDays(-RetentionDays);
            Records.RemoveAll(r => r.Timestamp < cutoffDate);
        }

        /// <summary>
        /// 导出记忆为JSON
        /// </summary>
        public string ExportToJson()
        {
            var exportData = new
            {
                exportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                totalCommands = totalCommandsExecuted,
                lastSession = lastSessionTime,
                memoryCount = Memory.Count,
                memory = Memory,
                recentRecords = Records.GetRange(Mathf.Max(0, Records.Count - 50), Mathf.Min(50, Records.Count))
                    .Select(r => new
                    {
                        type = r.Type.ToString(),
                        key = r.Key,
                        value = r.Value,
                        timestamp = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToList()
            };

            return Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>
        /// 获取记忆数量
        /// </summary>
        public int GetMemoryCount()
        {
            return Memory.Count;
        }

        /// <summary>
        /// 获取所有记忆数据
        /// </summary>
        public Dictionary<string, string> GetMemoryData()
        {
            return new Dictionary<string, string>(Memory);
        }

        /// <summary>
        /// 清空记忆
        /// </summary>
        public void ClearMemory()
        {
            int count = Memory.Count;
            Memory.Clear();
            Records.Clear();
            totalCommandsExecuted = 0;
            lastSessionTime = "";
            
            AddRecord(new MemoryRecord
            {
                Type = MemoryRecordType.Remove,
                Key = "ALL_MEMORY",
                Value = $"Cleared {count} entries",
                Timestamp = DateTime.Now
            });
            
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// 批量保存记忆
        /// </summary>
        public void SaveMemoryBatch(Dictionary<string, object> memoryData)
        {
            foreach (var kvp in memoryData)
            {
                Memory[kvp.Key] = kvp.Value?.ToString();
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [Button("导出记忆到文件", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void ExportToFile()
        {
            string content = ExportToText();
            string filename = $"PersistentMemory_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string path = System.IO.Path.Combine(Application.dataPath, "..", "Data", "ESVMCP", "Memory", filename);
            
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, content);
            
            Debug.Log($"[ESVMCP MemoryAsset] 记忆已导出到: {path}");
            UnityEditor.EditorUtility.RevealInFinder(path);
        }

        /// <summary>
        /// 导出为文本格式
        /// </summary>
        public string ExportToText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ESVMCP Persistent Memory Export ===");
            sb.AppendLine($"Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Asset Name: {this.name}");
            sb.AppendLine();

            sb.AppendLine("[Memory Data]");
            foreach (var kvp in Memory)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();

            sb.AppendLine("[Statistics]");
            sb.AppendLine($"- Total Commands Executed: {totalCommandsExecuted}");
            sb.AppendLine($"- Last Session Time: {lastSessionTime}");
            sb.AppendLine();

            sb.AppendLine("[Recent Records]");
            int startIndex = Mathf.Max(0, Records.Count - 20);
            for (int i = startIndex; i < Records.Count; i++)
            {
                var record = Records[i];
                sb.AppendLine($"{record.Timestamp:yyyy-MM-dd HH:mm:ss} [{record.Type}] {record.Key}: {record.Value}");
            }

            return sb.ToString();
        }

        [Button("清空所有记忆", ButtonSizes.Medium)]
        [GUIColor(1f, 0.4f, 0.4f)]
        private void ClearAllMemory()
        {
            int count = Memory.Count;
            Memory.Clear();
            Records.Clear();
            totalCommandsExecuted = 0;
            lastSessionTime = "";
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ESVMCP MemoryAsset] 已清空 {count} 条记忆");
        }

        [Button("清理旧记录", ButtonSizes.Medium)]
        private void ManualCleanup()
        {
            int beforeCount = Records.Count;
            CleanupOldRecords();
            int removedCount = beforeCount - Records.Count;
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ESVMCP MemoryAsset] 已清理 {removedCount} 条旧记录");
        }
    }

    /// <summary>
    /// 记忆记录
    /// </summary>
    [Serializable]
    public class MemoryRecord
    {
        [LabelText("类型")]
        public MemoryRecordType Type;

        [LabelText("键")]
        public string Key;

        [LabelText("值")]
        public string Value;

        [LabelText("时间戳")]
        public DateTime Timestamp;
    }

    /// <summary>
    /// 记录类型
    /// </summary>
    public enum MemoryRecordType
    {
        Save,       // 保存
        Load,       // 加载
        Remove,     // 删除
        Command,    // 命令执行
        Export      // 导出
    }
}
