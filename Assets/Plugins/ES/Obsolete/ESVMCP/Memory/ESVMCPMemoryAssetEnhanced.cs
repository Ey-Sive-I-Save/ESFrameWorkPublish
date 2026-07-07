using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

namespace ES.VMCP
{
    /// <summary>
    /// 增强的持久记忆资产 - 支持多种引用方式和智能衰减
    /// </summary>
    [CreateAssetMenu(fileName = "ESVMCPMemoryAssetEnhanced", menuName = MenuItemPathDefine.VMCP_ASSET_CREATION_PATH + "增强记忆资产")]
    public class ESVMCPMemoryAssetEnhanced : ScriptableObject
    {
        [Title("持久记忆系统")]
        [InfoBox("跨场景和会话的持久化记忆，支持智能衰减和多种引用方式")]

        [SerializeField]
        [ListDrawerSettings(ShowFoldout = true, ShowPaging = true, NumberOfItemsPerPage = 20)]
        [LabelText("记忆条目列表")]
        private List<ESVMCPMemoryItem> memoryItemsList = new List<ESVMCPMemoryItem>();

        [Title("统计信息")]
        [ShowInInspector, ReadOnly]
        public int TotalItems => memoryItemsList.Count;

        [ShowInInspector, ReadOnly]
        public int GameObjectItems => memoryItemsList.Count(m => m.ItemType == ESVMCPMemoryItemType.GameObject);

        [ShowInInspector, ReadOnly]
        public int AssetItems => memoryItemsList.Count(m => m.ItemType == ESVMCPMemoryItemType.Asset);

        [ShowInInspector, ReadOnly]
        public int ComponentItems => memoryItemsList.Count(m => m.ItemType == ESVMCPMemoryItemType.Component);

        [ShowInInspector, ReadOnly]
        public int PrimitiveItems => memoryItemsList.Count(m => m.ItemType == ESVMCPMemoryItemType.Primitive);

        [Title("衰减设置")]
        [LabelText("启用自动衰减")]
        [Tooltip("定期清理低分记忆项")]
        public bool EnableDecay = true;

        [LabelText("衰减阈值")]
        [Tooltip("低于此分数的记忆将被清理")]
        [Range(0, 50)]
        public float DecayThreshold = 20f;

        [LabelText("最大记忆数")]
        [Tooltip("超过此数量时触发衰减")]
        public int MaxMemoryItems = 1000;

        [LabelText("保留天数")]
        [Tooltip("未访问超过此天数的记忆将被清理")]
        public int RetentionDays = 30;

        #region 保存与获取

        /// <summary>
        /// 保存记忆项
        /// </summary>
        public void SaveMemoryItem(ESVMCPMemoryItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Key)) return;

            // 查找是否已存在
            var existingIndex = memoryItemsList.FindIndex(m => m.Key == item.Key);
            if (existingIndex >= 0)
            {
                // 更新现有项
                memoryItemsList[existingIndex] = item;
            }
            else
            {
                // 添加新项
                memoryItemsList.Add(item);
            }

            // 检查是否需要衰减
            if (EnableDecay && memoryItemsList.Count > MaxMemoryItems)
            {
                PerformDecay();
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 获取记忆项
        /// </summary>
        public ESVMCPMemoryItem GetMemoryItem(string key)
        {
            return memoryItemsList.FirstOrDefault(m => m.Key == key);
        }

        /// <summary>
        /// 检查是否存在
        /// </summary>
        public bool Has(string key)
        {
            return memoryItemsList.Any(m => m.Key == key);
        }

        /// <summary>
        /// 删除记忆项
        /// </summary>
        public bool Remove(string key)
        {
            int removedCount = memoryItemsList.RemoveAll(m => m.Key == key);
            if (removedCount > 0)
            {
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清除所有记忆
        /// </summary>
        public void Clear()
        {
            memoryItemsList.Clear();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 获取所有键
        /// </summary>
        public List<string> GetAllKeys()
        {
            return memoryItemsList.Select(m => m.Key).ToList();
        }

        /// <summary>
        /// 按类型获取项
        /// </summary>
        public List<ESVMCPMemoryItem> GetItemsByType(ESVMCPMemoryItemType type)
        {
            return memoryItemsList.Where(m => m.ItemType == type).ToList();
        }

        #endregion

        #region 智能衰减

        /// <summary>
        /// 执行记忆衰减
        /// </summary>
        [Button("执行智能衰减", ButtonSizes.Large)]
        [GUIColor(1f, 0.7f, 0.3f)]
        public void PerformDecay()
        {
            List<ESVMCPMemoryItem> toRemove = new List<ESVMCPMemoryItem>();

            foreach (var item in memoryItemsList)
            {
                float decayScore = item.GetDecayScore();

                // 条件1：衰减分数过低
                if (decayScore < DecayThreshold)
                {
                    toRemove.Add(item);
                    continue;
                }

                // 条件2：超过保留天数未访问
                if (DateTime.TryParse(item.LastAccessTime, out DateTime lastAccess))
                {
                    if ((DateTime.Now - lastAccess).TotalDays > RetentionDays)
                    {
                        toRemove.Add(item);
                        continue;
                    }
                }

                // 条件3：引用已失效
                if (!item.IsValid())
                {
                    toRemove.Add(item);
                }
            }

            // 移除低分记忆
            foreach (var item in toRemove)
            {
                memoryItemsList.Remove(item);
            }

            // 如果还是太多，移除最低分的
            if (memoryItemsList.Count > MaxMemoryItems)
            {
                int excessCount = memoryItemsList.Count - MaxMemoryItems;
                var lowestScoreItems = memoryItemsList
                    .OrderBy(m => m.GetDecayScore())
                    .Take(excessCount)
                    .ToList();

                foreach (var item in lowestScoreItems)
                {
                    memoryItemsList.Remove(item);
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif

            Debug.Log($"[ESVMCP Memory] 衰减完成: 移除了 {toRemove.Count} 个记忆项");
        }

        /// <summary>
        /// 清理无效引用
        /// </summary>
        [Button("清理无效引用", ButtonSizes.Medium)]
        [GUIColor(1f, 0.5f, 0.5f)]
        public void CleanInvalidReferences()
        {
            int before = memoryItemsList.Count;
            memoryItemsList.RemoveAll(m => !m.IsValid());
            int removed = before - memoryItemsList.Count;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif

            Debug.Log($"[ESVMCP Memory] 清理了 {removed} 个无效引用");
        }

        #endregion

        #region 导出与统计

        /// <summary>
        /// 导出为JSON
        /// </summary>
        [Button("导出JSON报告", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        public void ExportToJson()
        {
            var exportData = new
            {
                exportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                totalItems = TotalItems,
                statistics = new
                {
                    gameObjects = GameObjectItems,
                    assets = AssetItems,
                    components = ComponentItems,
                    primitives = PrimitiveItems
                },
                items = memoryItemsList.Select(m => new
                {
                    key = m.Key,
                    type = m.ItemType.ToString(),
                    accessCount = m.AccessCount,
                    importanceScore = m.ImportanceScore,
                    decayScore = m.GetDecayScore(),
                    lastAccess = m.LastAccessTime,
                    assetPath = m.AssetPath,
                    assetName = m.AssetName,
                    gameObjectName = m.GameObjectName
                }).ToList()
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
            GUIUtility.systemCopyBuffer = json;
            Debug.Log("[ESVMCP Memory] JSON报告已复制到剪贴板");
            Debug.Log(json);
        }

        /// <summary>
        /// 生成统计报告
        /// </summary>
        [Button("生成统计报告", ButtonSizes.Medium)]
        [GUIColor(0.6f, 1f, 0.6f)]
        public void GenerateStatisticsReport()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ESVMCP Persistent Memory Statistics ===");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine("[Summary]");
            sb.AppendLine($"Total Items: {TotalItems}");
            sb.AppendLine($"- GameObjects: {GameObjectItems}");
            sb.AppendLine($"- Assets: {AssetItems}");
            sb.AppendLine($"- Components: {ComponentItems}");
            sb.AppendLine($"- Primitives: {PrimitiveItems}");
            sb.AppendLine();

            // 按重要性分组
            var highImportance = memoryItemsList.Count(m => m.ImportanceScore >= 70);
            var mediumImportance = memoryItemsList.Count(m => m.ImportanceScore >= 40 && m.ImportanceScore < 70);
            var lowImportance = memoryItemsList.Count(m => m.ImportanceScore < 40);

            sb.AppendLine("[Importance Distribution]");
            sb.AppendLine($"High (70+): {highImportance}");
            sb.AppendLine($"Medium (40-69): {mediumImportance}");
            sb.AppendLine($"Low (<40): {lowImportance}");
            sb.AppendLine();

            // 最常访问的记忆
            sb.AppendLine("[Most Accessed (Top 10)]");
            var topAccessed = memoryItemsList
                .OrderByDescending(m => m.AccessCount)
                .Take(10);

            foreach (var item in topAccessed)
            {
                sb.AppendLine($"- {item.Key} ({item.AccessCount} times, {item.ItemType})");
            }
            sb.AppendLine();

            // 衰减风险
            sb.AppendLine("[Decay Risk]");
            var atRisk = memoryItemsList.Count(m => m.GetDecayScore() < DecayThreshold);
            sb.AppendLine($"Items at risk of decay: {atRisk}");
            sb.AppendLine();

            Debug.Log(sb.ToString());
            GUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log("[ESVMCP Memory] 统计报告已复制到剪贴板");
        }

        /// <summary>
        /// 显示衰减分数分布
        /// </summary>
        [Button("显示衰减分数分布", ButtonSizes.Medium)]
        public void ShowDecayScoreDistribution()
        {
            var distribution = new Dictionary<string, int>
            {
                ["很高 (80+)"] = memoryItemsList.Count(m => m.GetDecayScore() >= 80),
                ["高 (60-79)"] = memoryItemsList.Count(m => m.GetDecayScore() >= 60 && m.GetDecayScore() < 80),
                ["中等 (40-59)"] = memoryItemsList.Count(m => m.GetDecayScore() >= 40 && m.GetDecayScore() < 60),
                ["低 (20-39)"] = memoryItemsList.Count(m => m.GetDecayScore() >= 20 && m.GetDecayScore() < 40),
                ["很低 (<20)"] = memoryItemsList.Count(m => m.GetDecayScore() < 20)
            };

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Decay Score Distribution ===");
            foreach (var kvp in distribution)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value} items");
            }

            Debug.Log(sb.ToString());
        }

        #endregion

        #region 兼容性方法（用于旧代码）

        /// <summary>
        /// 保存简单值（兼容旧API）
        /// </summary>
        public void SaveMemory(string key, object value)
        {
            var item = ESVMCPMemoryItem.FromPrimitive(key, value);
            SaveMemoryItem(item);
        }

        /// <summary>
        /// 获取简单值（兼容旧API） - 返回解析结果
        /// </summary>
        public ResolveResult GetMemory(string key)
        {
            var item = GetMemoryItem(key);
            return item?.Resolve() ?? ResolveResult.Fail(ResolveResultType.NotFound, $"Memory item not found: {key}", ESVMCPMemoryItemType.Primitive);
        }

        #endregion
    }
}
