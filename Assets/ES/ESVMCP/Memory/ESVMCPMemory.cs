using UnityEngine;
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace ES.VMCP
{
    /// <summary>
    /// 场景记忆组件 - 运行时记忆
    /// </summary>
    [AddComponentMenu("ES/VMCP/记忆组件")]
    public class ESVMCPMemory : MonoBehaviour
    {
        [Title("场景记忆")]
        [InfoBox("存储当前场景的运行时记忆数据")]
        
        [ShowInInspector, ReadOnly]
        [DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.Foldout)]
        private Dictionary<string, object> memory = new Dictionary<string, object>();

        [ShowInInspector, ReadOnly]
        [ListDrawerSettings(ShowFoldout = true)]
        [LabelText("操作历史")]
        private List<string> operationHistory = new List<string>();

        [ShowInInspector, ReadOnly]
        [LabelText("GameObject引用")]
        [DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.Foldout)]
        private Dictionary<string, GameObject> gameObjectReferences = new Dictionary<string, GameObject>();

        [Title("统计信息")]
        [ShowInInspector, ReadOnly]
        public int MemoryCount => memory.Count;

        [ShowInInspector, ReadOnly]
        public int HistoryCount => operationHistory.Count;

        [ShowInInspector, ReadOnly]
        public int ReferenceCount => gameObjectReferences.Count;

        [Title("设置")]
        [LabelText("最大历史记录")]
        public int MaxHistory = 100;

        [LabelText("自动清理")]
        public bool AutoCleanup = true;

        /// <summary>
        /// 保存记忆
        /// </summary>
        public void SaveMemory(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[ESVMCP Memory] 记忆键不能为空");
                return;
            }

            memory[key] = value;
            AddHistory($"Save: {key} = {value}");

            if (AutoCleanup)
            {
                CleanupHistory();
            }
        }

        /// <summary>
        /// 获取记忆
        /// </summary>
        public object GetMemory(string key)
        {
            if (memory.TryGetValue(key, out object value))
            {
                AddHistory($"Load: {key}");
                return value;
            }
            return null;
        }

        /// <summary>
        /// 获取泛型记忆
        /// </summary>
        public T GetMemory<T>(string key, T defaultValue = default)
        {
            object value = GetMemory(key);
            if (value != null)
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    Debug.LogWarning($"[ESVMCP Memory] 类型转换失败: {key}");
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// 检查记忆是否存在
        /// </summary>
        public bool HasMemory(string key)
        {
            return memory.ContainsKey(key);
        }

        /// <summary>
        /// 删除记忆
        /// </summary>
        public void RemoveMemory(string key)
        {
            if (memory.Remove(key))
            {
                AddHistory($"Remove: {key}");
            }
        }

        /// <summary>
        /// 清除所有记忆
        /// </summary>
        public void ClearMemory()
        {
            int count = memory.Count;
            memory.Clear();
            AddHistory($"Clear All: {count} entries");
        }

        /// <summary>
        /// 获取记忆数量
        /// </summary>
        public int GetMemoryCount()
        {
            return memory.Count;
        }

        /// <summary>
        /// 获取所有记忆数据
        /// </summary>
        public Dictionary<string, object> GetMemoryData()
        {
            return new Dictionary<string, object>(memory);
        }

        /// <summary>
        /// 获取所有GameObject引用
        /// </summary>
        public Dictionary<string, GameObject> GetGameObjectReferences()
        {
            return new Dictionary<string, GameObject>(gameObjectReferences);
        }

        /// <summary>
        /// 保存GameObject引用
        /// </summary>
        public void SaveGameObjectReference(string key, GameObject go)
        {
            if (go != null)
            {
                gameObjectReferences[key] = go;
                SaveMemory(key + "_instanceId", go.GetInstanceID());
                AddHistory($"SaveRef: {key} -> {go.name}");
            }
        }

        /// <summary>
        /// 获取GameObject引用
        /// </summary>
        public GameObject GetGameObjectReference(string key)
        {
            if (gameObjectReferences.TryGetValue(key, out GameObject go))
            {
                if (go != null)
                {
                    return go;
                }
                else
                {
                    gameObjectReferences.Remove(key);
                    Debug.LogWarning($"[ESVMCP Memory] GameObject引用已失效: {key}");
                }
            }
            return null;
        }

        /// <summary>
        /// 添加操作历史
        /// </summary>
        public void AddHistory(string operation)
        {
            operationHistory.Add($"[{DateTime.Now:HH:mm:ss}] {operation}");
            if (operationHistory.Count > MaxHistory)
            {
                operationHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// 清理历史记录
        /// </summary>
        private void CleanupHistory()
        {
            while (operationHistory.Count > MaxHistory)
            {
                operationHistory.RemoveAt(0);
            }

            // 清理失效的GameObject引用
            List<string> keysToRemove = new List<string>();
            foreach (var kvp in gameObjectReferences)
            {
                if (kvp.Value == null)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (string key in keysToRemove)
            {
                gameObjectReferences.Remove(key);
            }
        }

        /// <summary>
        /// 导出记忆为文本
        /// </summary>
        public string ExportToText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ESVMCP Scene Memory Export ===");
            sb.AppendLine($"Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Scene: {gameObject.scene.name}");
            sb.AppendLine();

            sb.AppendLine("[Memory Data]");
            foreach (var kvp in memory)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();

            sb.AppendLine("[GameObject References]");
            foreach (var kvp in gameObjectReferences)
            {
                if (kvp.Value != null)
                {
                    sb.AppendLine($"- {kvp.Key}: {kvp.Value.name} (Active: {kvp.Value.activeSelf})");
                }
            }
            sb.AppendLine();

            sb.AppendLine("[Recent Operations]");
            int startIndex = Mathf.Max(0, operationHistory.Count - 20);
            for (int i = startIndex; i < operationHistory.Count; i++)
            {
                sb.AppendLine(operationHistory[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 导出为JSON格式
        /// </summary>
        public string ExportToJson()
        {
            var exportData = new Dictionary<string, object>
            {
                ["exportTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["scene"] = gameObject.scene.name,
                ["memory"] = memory,
                ["operationHistory"] = operationHistory,
                ["gameObjectReferences"] = new Dictionary<string, string>()
            };

            var refDict = (Dictionary<string, string>)exportData["gameObjectReferences"];
            foreach (var kvp in gameObjectReferences)
            {
                if (kvp.Value != null)
                {
                    refDict[kvp.Key] = kvp.Value.name;
                }
            }

            return JsonUtility.ToJson(exportData, true);
        }

        [Button("导出记忆到文本", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void ExportToTextFile()
        {
            string content = ExportToText();
            string filename = $"SceneMemory_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string path = System.IO.Path.Combine(Application.dataPath, "..", "Data", "ESVMCP", "Memory", filename);
            
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, content);
            
            Debug.Log($"[ESVMCP Memory] 记忆已导出到: {path}");
        }

        [Button("清空所有记忆", ButtonSizes.Medium)]
        [GUIColor(1f, 0.4f, 0.4f)]
        private void ClearAllMemory()
        {
            ClearMemory();
            gameObjectReferences.Clear();
            operationHistory.Clear();
            Debug.Log("[ESVMCP Memory] 所有记忆已清空");
        }

        private void OnDestroy()
        {
            // 组件销毁时可以选择导出记忆
            if (AutoCleanup)
            {
                Debug.Log($"[ESVMCP Memory] 场景记忆已清理: {memory.Count} 条");
            }
        }
    }
}
