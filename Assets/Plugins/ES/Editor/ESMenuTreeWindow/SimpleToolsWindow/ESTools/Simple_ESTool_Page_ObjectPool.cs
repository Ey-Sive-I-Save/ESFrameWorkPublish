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
        [DisplayAsString(Overflow = true, FontSize = 13), HideLabel]
        public string PoolCoreInfo = "";

        [Title("对象池<集成>工具", "ES对象池运行时数据汇总管理", bold: true, titleAlignment: TitleAlignments.Centered)]
        [InfoBox("汇总 Poolable-Define.cs 支持的对象池运行时统计。用于查看池数量、创建量、活跃量、回收量和丢弃量。", InfoMessageType.Info)]
        [DisplayAsString(fontSize: 12), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "支持 IPoolable / IPoolableAuto / Pool<T> / ESSimplePool<T> / ESSimplePoolSingleton<T> / PoolStatistics。";

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
        private string lastResultSummary = "";
        private string lastResultDetail = "";

        [OnInspectorGUI]
        public void DrawThisWindow()
        {
            // 显示对象池使用情况面板
            DrawPoolUsagePanel();
        }

        private void DrawPoolUsagePanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("对象池使用情况", "查看运行时池数量、活跃量、回收量和异常丢弃。");
            SimpleToolsPanelUtility.DrawResultSummary("最近对象池分析", lastResultSummary, lastResultDetail);

            // 使用状况查询按钮
            if (SimpleToolsPanelUtility.DrawActionButton("分析池使用情况", SimpleToolsActionTone.Primary, 26))
            {
                AnalyzePoolUsage();
                showUsageAnalysis = true;
            }

            // 显示分析结果
            if (showUsageAnalysis)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    SimpleToolsPanelUtility.DrawSummary(
                        $"平均实时利用率: {avgRealTimeUtilization:P2}",
                        $"平均总利用率: {avgTotalUtilization:P2}",
                        $"平均丢弃: {avgDiscarded:F2}");

                    DrawPoolAnalysisList("高频/扩容偏高", highFreqPools, new Color(1f, 0.58f, 0.18f));
                    DrawPoolAnalysisList("低频/容量偏空", lowFreqPools, new Color(0.35f, 0.75f, 1f));
                }

                if (SimpleToolsPanelUtility.DrawActionButton("收起分析", SimpleToolsActionTone.Neutral, 22))
                    showUsageAnalysis = false;
            }

            var globalGroup = PoolStatistics.GlobalStatisticsGroup;
            if (globalGroup == null)
            {
                SimpleToolsPanelUtility.DrawEmptyState("全局统计组还没有初始化。进入 Play Mode 或触发对象池创建后，这里会显示运行时统计。");
                return;
            }

            var allStats = CollectValidStats(globalGroup);
            int totalPools = allStats.Count;
            int totalCreated = allStats.Sum(stat => stat.TotalCreated);
            int totalActive = allStats.Sum(stat => stat.CurrentActive);
            int totalPooled = allStats.Sum(stat => stat.CurrentPooled);
            int totalDiscarded = allStats.Sum(stat => stat.DiscardedCount);
            SimpleToolsPanelUtility.DrawSummary(
                $"总池数: {totalPools}",
                $"总创建: {totalCreated}",
                $"活跃: {totalActive}",
                $"池中: {totalPooled}",
                $"丢弃: {totalDiscarded}");

            if (totalPools == 0)
            {
                SimpleToolsPanelUtility.DrawEmptyState("当前没有对象池统计。先运行会创建对象池的功能，再回来查看池中、活跃、峰值和丢弃情况。");
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
                                    ContainsIgnoreCase(groupKey, searchText);
                bool poolMatches = false;
                if (!groupMatches)
                {
                    foreach (var stat in groupList)
                    {
                        if (stat != null && ContainsIgnoreCase(stat.PoolDisplayName, searchText))
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
                                !ContainsIgnoreCase(stat.PoolDisplayName, searchText))
                            {
                                continue;
                            }

                            EditorGUILayout.LabelField(
                                $"{stat.PoolDisplayName}    创建 {stat.TotalCreated} | 获取 {stat.TotalGets} | 回收 {stat.TotalReturns} | 池中 {stat.CurrentPooled} | 活跃 {stat.CurrentActive} | 峰值 {stat.PeakActive} | 丢弃 {stat.DiscardedCount}",
                                EditorStyles.miniLabel);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 分析池使用状况
        /// </summary>
        private void AnalyzePoolUsage()
        {
            var globalGroup = PoolStatistics.GlobalStatisticsGroup;
            if (globalGroup == null)
            {
                lastResultSummary = "对象池统计未初始化";
                lastResultDetail = "进入 Play Mode 或触发对象池创建后再刷新。";
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
                lastResultSummary = "对象池分析完成: 没有有效统计";
                lastResultDetail = "当前统计组存在，但没有有效 PoolStatistics。";
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

            lastResultSummary = $"对象池分析完成: 池 {allStats.Count} 个 | 高频 {highFreqPools.Count} | 低频 {lowFreqPools.Count}";
            lastResultDetail =
                $"平均实时利用率: {avgRealTimeUtilization:P2}\n平均总利用率: {avgTotalUtilization:P2}\n平均丢弃: {avgDiscarded:F2}\n\n" +
                "高频/扩容偏高:\n" + BuildPoolPreview(highFreqPools) +
                "\n\n低频/容量偏空:\n" + BuildPoolPreview(lowFreqPools);
        }

        private static List<PoolStatistics> CollectValidStats(JumpSafeKeyGroup<string, PoolStatistics> globalGroup)
        {
            List<PoolStatistics> stats = new List<PoolStatistics>();
            foreach (var kvp in globalGroup.Groups)
            {
                var list = kvp.Value;
                if (list != null)
                    stats.AddRange(list.Where(s => s != null && s.IsValid));
            }

            return stats;
        }

        private static void DrawPoolAnalysisList(string title, List<PoolStatistics> pools, Color color)
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = color;
            EditorGUILayout.LabelField(title, style);

            if (pools == null || pools.Count == 0)
            {
                EditorGUILayout.LabelField("暂无明显项。", EditorStyles.miniLabel);
                return;
            }

            foreach (var stat in pools)
            {
                if (stat == null)
                    continue;

                float realTimeUtil = stat.CurrentPooled > 0 ? (float)stat.CurrentActive / stat.CurrentPooled : 0f;
                float totalUtil = stat.TotalCreated > 0 ? (float)stat.TotalGets / stat.TotalCreated : 0f;
                EditorGUILayout.LabelField(
                    $"{stat.PoolDisplayName} ({stat.GroupName})  实时 {realTimeUtil:P0} | 总 {totalUtil:P0} | 池中 {stat.CurrentPooled} | 丢弃 {stat.DiscardedCount}",
                    style);
            }
        }

        private static bool ContainsIgnoreCase(string value, string search)
        {
            return !string.IsNullOrEmpty(value)
                   && !string.IsNullOrEmpty(search)
                   && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildPoolPreview(IEnumerable<PoolStatistics> pools)
        {
            return SimpleToolsSafetyUtility.JoinPreview(
                pools?.Select(stat => stat == null ? null : $"{stat.PoolDisplayName} ({stat.GroupName}) | 池中 {stat.CurrentPooled} | 活跃 {stat.CurrentActive} | 丢弃 {stat.DiscardedCount}"),
                8);
        }
    }
    #endregion
}
