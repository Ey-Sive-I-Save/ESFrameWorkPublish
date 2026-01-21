using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

namespace ES.VMCP
{
    /// <summary>
    /// 增强的场景记忆组件 - 基于ESVMCPMemoryItem的运行时记忆
    /// </summary>
    [AddComponentMenu("ES/VMCP/增强记忆组件")]
    public class ESVMCPMemoryEnhanced : MonoBehaviour
    {
        [Title("增强记忆系统")]
        [InfoBox("支持多种引用方式的智能记忆系统，自动管理GameObject、Asset、Component等")]

        [ShowInInspector, ReadOnly]
        [DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.Foldout)]
        [LabelText("记忆条目")]
        private Dictionary<string, ESVMCPMemoryItem> memoryItems = new Dictionary<string, ESVMCPMemoryItem>();

        [ShowInInspector, ReadOnly]
        [ListDrawerSettings(ShowFoldout = true)]
        [LabelText("操作历史")]
        private List<string> operationHistory = new List<string>();

        [Title("统计信息")]
        [ShowInInspector, ReadOnly]
        public int TotalMemoryItems => memoryItems.Count;

        [ShowInInspector, ReadOnly]
        public int GameObjectMemory => memoryItems.Count(m => m.Value.ItemType == ESVMCPMemoryItemType.GameObject);

        [ShowInInspector, ReadOnly]
        public int AssetMemory => memoryItems.Count(m => m.Value.ItemType == ESVMCPMemoryItemType.Asset);

        [ShowInInspector, ReadOnly]
        public int ComponentMemory => memoryItems.Count(m => m.Value.ItemType == ESVMCPMemoryItemType.Component);

        [Title("设置")]
        [LabelText("最大历史记录")]
        public int MaxHistory = 100;

        [LabelText("自动清理")]
        public bool AutoCleanup = true;

        [LabelText("清理间隔(秒)")]
        public float CleanupInterval = 60f;

        private float lastCleanupTime;

        #region 保存记忆

        /// <summary>
        /// 保存GameObject到记忆
        /// </summary>
        public void SaveGameObject(string key, GameObject go, bool persistent = false)
        {
            if (string.IsNullOrEmpty(key) || go == null) return;

            var item = ESVMCPMemoryItem.FromGameObject(key, go);
            if (item != null)
            {
                memoryItems[key] = item;
                AddHistory($"SaveGO: {key} -> {go.name}");

                if (persistent)
                {
                    SyncToPersistentMemory(item);
                }
            }
        }

        /// <summary>
        /// 保存资产到记忆（默认持久化）
        /// </summary>
        public void SaveAsset(string key, UnityEngine.Object asset)
        {
            if (string.IsNullOrEmpty(key) || asset == null) return;

            var item = ESVMCPMemoryItem.FromAsset(key, asset);
            if (item != null)
            {
                memoryItems[key] = item;
                AddHistory($"SaveAsset: {key} -> {asset.name}");

                // 资产默认持久化
                SyncToPersistentMemory(item);
            }
        }

        /// <summary>
        /// 保存组件到记忆
        /// </summary>
        public void SaveComponent(string key, Component component, bool persistent = false)
        {
            if (string.IsNullOrEmpty(key) || component == null) return;

            var item = ESVMCPMemoryItem.FromComponent(key, component);
            if (item != null)
            {
                memoryItems[key] = item;
                AddHistory($"SaveComponent: {key} -> {component.GetType().Name}");

                if (persistent)
                {
                    SyncToPersistentMemory(item);
                }
            }
        }

        /// <summary>
        /// 保存基础类型到记忆
        /// </summary>
        public void SavePrimitive(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return;

            var item = ESVMCPMemoryItem.FromPrimitive(key, value);
            memoryItems[key] = item;
            AddHistory($"SavePrimitive: {key} -> {value}");
        }

        /// <summary>
        /// 通用保存方法（自动判断类型）
        /// </summary>
        public void Save(string key, object value, bool persistent = false)
        {
            if (value == null)
            {
                SavePrimitive(key, null);
                return;
            }

            if (value is GameObject go)
            {
                SaveGameObject(key, go, persistent);
            }
            else if (value is Component component)
            {
                SaveComponent(key, component, persistent);
            }
            else if (value is UnityEngine.Object asset)
            {
                SaveAsset(key, asset);
            }
            else
            {
                SavePrimitive(key, value);
            }
        }

        #endregion

        #region 获取记忆

        /// <summary>
        /// 获取记忆项
        /// </summary>
        public ESVMCPMemoryItem GetMemoryItem(string key)
        {
            if (memoryItems.TryGetValue(key, out var item))
            {
                return item;
            }

            // 尝试从持久记忆加载
            var persistentItem = LoadFromPersistentMemory(key);
            if (persistentItem != null)
            {
                memoryItems[key] = persistentItem;
                return persistentItem;
            }

            return null;
        }

        /// <summary>
        /// 获取GameObject
        /// </summary>
        public GameObject GetGameObject(string key)
        {
            var item = GetMemoryItem(key);
            return item?.ResolveAsGameObject();
        }

        /// <summary>
        /// 获取资产
        /// </summary>
        public UnityEngine.Object GetAsset(string key)
        {
            var item = GetMemoryItem(key);
            return item?.ResolveAsAsset();
        }

        /// <summary>
        /// 获取资产（泛型）
        /// </summary>
        public T GetAsset<T>(string key) where T : UnityEngine.Object
        {
            var asset = GetAsset(key);
            return asset as T;
        }

        /// <summary>
        /// 获取组件
        /// </summary>
        public new Component GetComponent(string key)
        {
            var item = GetMemoryItem(key);
            return item?.ResolveAsComponent();
        }

        /// <summary>
        /// 获取组件（泛型）
        /// </summary>
        public T GetComponent<T>(string key) where T : Component
        {
            var component = GetComponent(key);
            return component as T;
        }

        /// <summary>
        /// 获取基础类型
        /// </summary>
        public T GetPrimitive<T>(string key, T defaultValue = default)
        {
            var item = GetMemoryItem(key);
            if (item != null && item.ItemType == ESVMCPMemoryItemType.Primitive)
            {
                return item.ResolveAsPrimitive<T>();
            }
            return defaultValue;
        }

        /// <summary>
        /// 通用获取方法 - 返回解析结果
        /// </summary>
        public ResolveResult Get(string key)
        {
            var item = GetMemoryItem(key);
            return item?.Resolve() ?? ResolveResult.Fail(ResolveResultType.NotFound, $"Memory item not found: {key}", ESVMCPMemoryItemType.Primitive);
        }

        #endregion

        #region 记忆管理

        /// <summary>
        /// 检查记忆是否存在
        /// </summary>
        public bool Has(string key)
        {
            return memoryItems.ContainsKey(key) || HasInPersistentMemory(key);
        }

        /// <summary>
        /// 删除记忆
        /// </summary>
        public void Remove(string key)
        {
            if (memoryItems.Remove(key))
            {
                AddHistory($"Remove: {key}");
            }
        }

        /// <summary>
        /// 清除所有记忆
        /// </summary>
        public void Clear()
        {
            int count = memoryItems.Count;
            memoryItems.Clear();
            AddHistory($"ClearAll: {count} items");
        }

        /// <summary>
        /// 获取所有记忆键
        /// </summary>
        public List<string> GetAllKeys()
        {
            return new List<string>(memoryItems.Keys);
        }

        /// <summary>
        /// 获取按类型过滤的键
        /// </summary>
        public List<string> GetKeysByType(ESVMCPMemoryItemType type)
        {
            return memoryItems
                .Where(kvp => kvp.Value.ItemType == type)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        #endregion

        #region 持久化同步

        /// <summary>
        /// 同步到持久记忆
        /// </summary>
        private void SyncToPersistentMemory(ESVMCPMemoryItem item)
        {
            try
            {
                var persistentMemory = ESVMCPConfig.Instance.GetPersistentMemory();
                if (persistentMemory != null)
                {
                    persistentMemory.SaveMemoryItem(item);
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(persistentMemory);
#endif
                    AddHistory($"Persist: {item.Key}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ESVMCP Memory] 持久化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从持久记忆加载
        /// </summary>
        private ESVMCPMemoryItem LoadFromPersistentMemory(string key)
        {
            try
            {
                var persistentMemory = ESVMCPConfig.Instance.GetPersistentMemory();
                return persistentMemory?.GetMemoryItem(key);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ESVMCP Memory] 从持久记忆加载失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查持久记忆中是否存在
        /// </summary>
        private bool HasInPersistentMemory(string key)
        {
            try
            {
                var persistentMemory = ESVMCPConfig.Instance.GetPersistentMemory();
                return persistentMemory != null && persistentMemory.Has(key);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 自动清理

        private void Update()
        {
            if (!AutoCleanup) return;

            if (Time.time - lastCleanupTime > CleanupInterval)
            {
                PerformCleanup();
                lastCleanupTime = Time.time;
            }
        }

        /// <summary>
        /// 执行清理
        /// </summary>
        [Button("执行清理", ButtonSizes.Medium)]
        private void PerformCleanup()
        {
            // 清理无效引用
            List<string> invalidKeys = new List<string>();
            foreach (var kvp in memoryItems)
            {
                if (!kvp.Value.IsValid())
                {
                    invalidKeys.Add(kvp.Key);
                }
            }

            foreach (var key in invalidKeys)
            {
                memoryItems.Remove(key);
                AddHistory($"CleanInvalid: {key}");
            }

            // 清理历史记录
            while (operationHistory.Count > MaxHistory)
            {
                operationHistory.RemoveAt(0);
            }

            if (invalidKeys.Count > 0)
            {
                Debug.Log($"[ESVMCP Memory] 清理了 {invalidKeys.Count} 个无效记忆");
            }
        }

        #endregion

        #region 辅助方法

        private void AddHistory(string operation)
        {
            operationHistory.Add($"[{DateTime.Now:HH:mm:ss}] {operation}");
            if (operationHistory.Count > MaxHistory)
            {
                operationHistory.RemoveAt(0);
            }
        }

        [Button("导出记忆报告", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void ExportReport()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ESVMCP Enhanced Memory Report ===");
            sb.AppendLine($"Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine("[Statistics]");
            sb.AppendLine($"Total Items: {TotalMemoryItems}");
            sb.AppendLine($"GameObjects: {GameObjectMemory}");
            sb.AppendLine($"Assets: {AssetMemory}");
            sb.AppendLine($"Components: {ComponentMemory}");
            sb.AppendLine();

            sb.AppendLine("[Memory Items]");
            foreach (var kvp in memoryItems.OrderBy(k => k.Value.ItemType))
            {
                var item = kvp.Value;
                sb.AppendLine($"- [{item.ItemType}] {item.Key}");
                sb.AppendLine($"  访问: {item.AccessCount}次, 重要性: {item.ImportanceScore}");
                sb.AppendLine($"  最后访问: {item.LastAccessTime}");
                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
            GUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log("[ESVMCP Memory] 报告已复制到剪贴板");
        }

        #endregion
    }
}
