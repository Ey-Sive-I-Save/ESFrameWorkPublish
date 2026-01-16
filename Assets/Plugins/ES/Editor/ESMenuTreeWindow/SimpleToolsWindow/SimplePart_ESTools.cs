using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;


namespace ES
{
    #region ES集成工具集-对象池工具
    [Serializable]
    public class Page_ObjectPool : ESWindowPageBase
    {
        [DisplayAsString(Overflow = true, FontSize = 25), HideLabel]
        public string PoolCoreInfo = "";

        [Title("对象池<集成>工具", "ES对象池运行时数据汇总管理", bold: true, titleAlignment: TitleAlignments.Centered)]
        [InfoBox("本页集成了Poolable-Define.cs支持的对象池功能，包括池化对象接口、对象池基类、简单对象池、单例对象池等。\n\n可用于高性能、主线程无锁的对象池管理。", InfoMessageType.Info)]
        [DisplayAsString(fontSize: 20), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "IPoolable: 可池化对象接口\nIPoolableAuto: 支持自动回池接口"
             +
            "Pool<T>: 对象池基类（主线程无锁）\n" +
            "ESSimplePool<T>: 简单对象池实现\n" +
            "ESSimplePoolSingleton<T>: 单例对象池\n" +
            "PoolStatistics: 池统计信息（仅编辑器）";

        // 折叠状态字典
        private SafeDictionary<string, bool> foldouts = new SafeDictionary<string, bool>(() => true);

        // 搜索文本
        private string searchText = "";

        // 使用状况查询结果
        private bool showUsageAnalysis = false;
        private List<PoolStatistics> highFreqPools = new List<PoolStatistics>();
        private List<PoolStatistics> lowFreqPools = new List<PoolStatistics>();
        private float avgRealTimeUtilization;
        private float avgTotalUtilization;
        private float avgDiscarded;

        [OnInspectorGUI]
        public void DrawThisWindow()
        {
            // 显示对象池使用情况面板
            DrawPoolUsagePanel();
        }

        private void DrawPoolUsagePanel()
        {
            EditorGUILayout.LabelField("对象池使用情况", EditorStyles.boldLabel);

            // 使用状况查询按钮
            if (GUILayout.Button("使用状况查询", GUILayout.Height(30)))
            {
                AnalyzePoolUsage();
                showUsageAnalysis = true;
            }

            // 显示分析结果
            if (showUsageAnalysis)
            {
                EditorGUILayout.LabelField("分析结果", EditorStyles.boldLabel);
                Vector2 scrollPos = Vector2.zero; // 滚动位置
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));

                // 总计信息
                EditorGUILayout.LabelField($"总池数: {highFreqPools.Count + lowFreqPools.Count}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"平均实时利用率: {avgRealTimeUtilization:P2}");
                EditorGUILayout.LabelField($"平均总利用率: {avgTotalUtilization:P2}");
                EditorGUILayout.LabelField($"平均扩容次数: {avgDiscarded:F2}");
                EditorGUILayout.Space();

                // 高频池 - 橙色
                GUIStyle highFreqStyle = new GUIStyle(EditorStyles.label);
                highFreqStyle.normal.textColor = new Color(1f, 0.5f, 0f); // 橙色
                EditorGUILayout.LabelField("高频使用池 (Top 5):", highFreqStyle);
                foreach (var stat in highFreqPools)
                {
                    float realTimeUtil = stat.CurrentPooled > 0 ? (float)stat.CurrentActive / stat.CurrentPooled : 0f;
                    float totalUtil = stat.TotalCreated > 0 ? (float)stat.TotalGets / stat.TotalCreated : 0f;
                    EditorGUILayout.LabelField($"- {stat.PoolDisplayName} (组: {stat.GroupName})", highFreqStyle);
                    EditorGUILayout.LabelField($"  实时利用率: {realTimeUtil:P2}, 总利用率: {totalUtil:P2}, 扩容: {stat.DiscardedCount}", highFreqStyle);
                }
                EditorGUILayout.Space();

                // 低频池 - 青色
                GUIStyle lowFreqStyle = new GUIStyle(EditorStyles.label);
                lowFreqStyle.normal.textColor = new Color(0f, 0.5f, 1f); // 青色
                EditorGUILayout.LabelField("低频使用池 (Top 5):", lowFreqStyle);
                foreach (var stat in lowFreqPools)
                {
                    float realTimeUtil = stat.CurrentPooled > 0 ? (float)stat.CurrentActive / stat.CurrentPooled : 0f;
                    float totalUtil = stat.TotalCreated > 0 ? (float)stat.TotalGets / stat.TotalCreated : 0f;
                    EditorGUILayout.LabelField($"- {stat.PoolDisplayName} (组: {stat.GroupName})", lowFreqStyle);
                    EditorGUILayout.LabelField($"  实时利用率: {realTimeUtil:P2}, 总利用率: {totalUtil:P2}, 池中: {stat.CurrentPooled}", lowFreqStyle);
                }

                EditorGUILayout.EndScrollView();
                if (GUILayout.Button("关闭分析"))
                {
                    showUsageAnalysis = false;
                }
            }

            var globalGroup = PoolStatistics.GlobalStatisticsGroup;
            if (globalGroup == null)
            {
                EditorGUILayout.HelpBox("全局统计组未初始化", MessageType.Info);
                return;
            }

            // 检查是否有组包含3个或更多池
            bool hasLargeGroup = globalGroup.Groups.Any(kvp => kvp.Value != null && kvp.Value.Count() >= 3);

            // 如果有大组或有折叠功能，显示搜索框
            if (hasLargeGroup || foldouts.Count > 0)
            {
                searchText = EditorGUILayout.TextField("搜索 (组或池名)", searchText);
            }

            // 遍历所有分组
            foreach (var groupKey in globalGroup.Groups.Keys)
            {
                var groupList = globalGroup.GetGroupDirectly(groupKey);
                if (groupList == null || groupList.Count() == 0) continue;

                // 过滤：如果有搜索文本，检查组名或池名是否匹配
                bool groupMatches = string.IsNullOrEmpty(searchText) ||
                                    groupKey.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                bool poolMatches = false;
                if (!groupMatches)
                {
                    foreach (var stat in groupList)
                    {
                        if (stat != null && stat.PoolDisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            poolMatches = true;
                            break;
                        }
                    }
                }
                if (!groupMatches && !poolMatches) continue;

                // 分组折叠
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    foldouts[groupKey] = EditorGUILayout.Foldout(foldouts[groupKey], $"分组: {groupKey} ({groupList.Count()} 个池)", true);

                    if (foldouts[groupKey])
                    {
                        // 显示每个池的统计信息
                        for (int i = 0; i < groupList.Count(); i++)
                        {
                            var stat = groupList.ValuesNow[i];
                            if (stat == null || !stat.IsValid) continue;

                            // 如果有搜索文本且不匹配组名，检查池名
                            if (!string.IsNullOrEmpty(searchText) && !groupMatches &&
                                stat.PoolDisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                continue;
                            }

                            EditorGUILayout.LabelField($"池: {stat.PoolDisplayName}", EditorStyles.miniBoldLabel);
                            EditorGUILayout.LabelField($"创建: {stat.TotalCreated} | 获取: {stat.TotalGets} | 回收: {stat.TotalReturns}");
                            EditorGUILayout.LabelField($"池中: {stat.CurrentPooled} | 活跃: {stat.CurrentActive} | 峰值: {stat.PeakActive} | 丢弃: {stat.DiscardedCount}");

                            // 分隔线
                            EditorGUILayout.Space();
                        }
                    }
                }
            }

            // 总计信息
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("总计", EditorStyles.boldLabel);
            int totalPools = 0;
            int totalCreated = 0;
            int totalActive = 0;
            foreach (var kvp in globalGroup.Groups)
            {
                var list = kvp.Value;
                if (list != null)
                {
                    totalPools += list.Count();
                    foreach (var stat in list)
                    {
                        if (stat != null)
                        {
                            totalCreated += stat.TotalCreated;
                            totalActive += stat.CurrentActive;
                        }
                    }
                }
            }
            EditorGUILayout.LabelField($"总池数: {totalPools} | 总创建: {totalCreated} | 总活跃: {totalActive}");
        }

        /// <summary>
        /// 分析池使用状况
        /// </summary>
        private void AnalyzePoolUsage()
        {
            var globalGroup = PoolStatistics.GlobalStatisticsGroup;
            if (globalGroup == null)
            {
                highFreqPools.Clear();
                lowFreqPools.Clear();
                return;
            }

            List<PoolStatistics> allStats = new List<PoolStatistics>();
            foreach (var kvp in globalGroup.Groups)
            {
                var list = kvp.Value;
                if (list != null)
                {
                    allStats.AddRange(list.Where(s => s != null && s.IsValid));
                }
            }

            if (allStats.Count == 0)
            {
                highFreqPools.Clear();
                lowFreqPools.Clear();
                return;
            }

            // 计算均值
            avgRealTimeUtilization = allStats.Average(s => s.CurrentPooled > 0 ? (float)s.CurrentActive / s.CurrentPooled : 0f);
            avgTotalUtilization = allStats.Average(s => s.TotalCreated > 0 ? (float)s.TotalGets / s.TotalCreated : 0f);
            avgDiscarded = (float)allStats.Average(s => s.DiscardedCount);

            // 高频使用：实时利用率高、总利用率高、丢弃多（扩容）
            highFreqPools = allStats.Where(s =>
                (s.CurrentPooled > 0 && (float)s.CurrentActive / s.CurrentPooled > avgRealTimeUtilization * 1.5f) ||
                (s.TotalCreated > 0 && (float)s.TotalGets / s.TotalCreated > avgTotalUtilization * 1.5f) ||
                s.DiscardedCount > avgDiscarded * 1.5f
            ).OrderByDescending(s => s.TotalGets).Take(5).ToList();

            // 低频使用：实时利用率低、总利用率低、容量大但使用少
            lowFreqPools = allStats.Where(s =>
                (s.CurrentPooled > 0 && (float)s.CurrentActive / s.CurrentPooled < avgRealTimeUtilization * 0.5f && s.CurrentPooled > 10) ||
                (s.TotalCreated > 0 && (float)s.TotalGets / s.TotalCreated < avgTotalUtilization * 0.5f && s.TotalCreated > 5) ||
                (s.CurrentPooled > s.CurrentActive * 2 && s.CurrentPooled > 10)
            ).OrderBy(s => s.TotalGets).Take(5).ToList();
        }
    }
    #endregion
}
