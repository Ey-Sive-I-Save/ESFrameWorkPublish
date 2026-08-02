using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using ES.EditorInternal;


namespace ES
{
    #region ES集成工具 - 对象池工具
    [Serializable]
    [ESSimpleToolsLayout]
    public class Page_ObjectPool : ESWindowPageBase
    {
        public enum ObjectPoolToolTab
        {
            运行时统计,
            PrefabPrewarmDataInfo审计,
            GameManager接入,
            PlayMode池组状态
        }

        [HideInInspector]
        public string PoolCoreInfo = "";

        [HideInInspector]
        public string readMe = "支持 IPoolable / IPoolableAuto / Pool<T> / ESSimplePool<T> / ESSimplePoolSingleton<T> / PoolStatistics。";

        // 折叠状态字典
        private SafeDictionary<string, bool> foldouts = new SafeDictionary<string, bool>(() => true);

        // 搜索文本
        private string searchText = "";
        private string prewarmSearchText = "";
        private string poolGroupSearchText = "";
        private readonly List<PrefabPrewarmDataInfo> prewarmDataCache = new List<PrefabPrewarmDataInfo>(16);
        private bool prewarmDataCacheReady;

        [HideInInspector]
        public ObjectPoolToolTab currentTab = ObjectPoolToolTab.运行时统计;

        [HideInInspector]
        public PrefabPrewarmDataInfo targetPrewarmData;

        private static readonly ESEditorSectionNavigatorItem[] PoolSections =
        {
            new ESEditorSectionNavigatorItem("runtime", "运行时统计", "查看通用对象池统计；不写入配置。"),
            new ESEditorSectionNavigatorItem("prewarm", "预热配置审计", "审计 PrefabPrewarmDataInfo 配置资产，不扫描 Project Prefab。"),
            new ESEditorSectionNavigatorItem("integration", "GameManager 接入", "把已存在的预热配置接入当前场景的 ESGameManager。"),
            new ESEditorSectionNavigatorItem("groups", "池组状态", "Play Mode 下只读查看当前已创建池组。")
        };

        // 使用状况查询结果
        private bool showUsageAnalysis = false;
        private List<PoolStatistics> highFreqPools = new List<PoolStatistics>();
        private List<PoolStatistics> lowFreqPools = new List<PoolStatistics>();
        private float avgRealTimeUtilization;
        private float avgTotalUtilization;
        private float avgDiscarded;
        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private readonly List<PoolGroupRenderSnapshot> poolGroupRenderSnapshots = new List<PoolGroupRenderSnapshot>(16);
        private int poolGroupRenderSnapshotCount;
        private int poolGroupRenderSignature;
        private bool poolGroupRenderSnapshotValid;
        private bool poolGroupSearchVisible;
        private string poolGroupSnapshotSearchText = string.Empty;
        private bool poolUsageSnapshotHasGlobalGroup;
        private int poolUsageSnapshotTotalPools;
        private int poolUsageSnapshotTotalActive;
        private int poolUsageSnapshotTotalPooled;
        private int poolUsageSnapshotTotalDiscarded;

        [OnInspectorGUI, PropertyOrder(100)]
        public void DrawThisWindow()
        {
            SimpleToolsPanelUtility.DrawToolHeader(
                "对象池与预热配置",
                "查看运行时池数据，审计预热配置，并把明确的配置资产接入当前场景 GameManager。",
                SimpleToolsMaturity.Upgrading,
                "PrefabPrewarmDataInfo 是 ESSO/SoDataInfo 资产；工具只扫描这种配置资产，不再把当前 Selection 伪装成池化入口。");

            DrawPoolSectionNavigator();

            switch (currentTab)
            {
                case ObjectPoolToolTab.PrefabPrewarmDataInfo审计:
                    DrawPrewarmDataPanel();
                    break;
                case ObjectPoolToolTab.GameManager接入:
                    DrawGameManagerPoolPanel();
                    break;
                case ObjectPoolToolTab.PlayMode池组状态:
                    DrawPlayModePoolGroupsPanel();
                    break;
                default:
                    DrawPoolUsagePanel();
                    break;
            }
        }

        private void DrawPoolSectionNavigator()
        {
            string nextId = ESEditorSectionNavigatorIMGUI.Draw(
                "SimpleTools.ObjectPool",
                GetPoolSectionId(currentTab),
                PoolSections);
            currentTab = GetPoolTab(nextId);
        }

        private static string GetPoolSectionId(ObjectPoolToolTab tab)
        {
            switch (tab)
            {
                case ObjectPoolToolTab.PrefabPrewarmDataInfo审计:
                    return "prewarm";
                case ObjectPoolToolTab.GameManager接入:
                    return "integration";
                case ObjectPoolToolTab.PlayMode池组状态:
                    return "groups";
                default:
                    return "runtime";
            }
        }

        private static ObjectPoolToolTab GetPoolTab(string sectionId)
        {
            switch (sectionId)
            {
                case "prewarm":
                    return ObjectPoolToolTab.PrefabPrewarmDataInfo审计;
                case "integration":
                    return ObjectPoolToolTab.GameManager接入;
                case "groups":
                    return ObjectPoolToolTab.PlayMode池组状态;
                default:
                    return ObjectPoolToolTab.运行时统计;
            }
        }

        private void DrawPoolUsagePanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("对象池使用情况", "查看运行时池数量、活跃量、回收量和异常丢弃。");

            DrawPoolActionPanel();
            SimpleToolsPanelUtility.DrawResultSummary("最近对象池分析", lastResultSummary, lastResultDetail);

            // 运行时池集合可能在 Layout 与 Repaint 之间改变。整个统计区都只能
            // 消费 Layout 时建立的快照，不能只冻结下半部分的分组行。
            EnsurePoolUsageRenderSnapshot();

            // 显示分析结果
            if (showUsageAnalysis)
            {
                using (SimpleToolsPanelUtility.BeginContentSection())
                {
                    SimpleToolsPanelUtility.DrawSummary(
                        $"平均实时利用率: {avgRealTimeUtilization:P2}",
                        $"平均总利用率: {avgTotalUtilization:P2}",
                        $"平均丢弃: {avgDiscarded:F2}");

                    DrawPoolAnalysisList("高频/扩容偏高", highFreqPools, new Color(1f, 0.58f, 0.18f));
                    DrawPoolAnalysisList("低频/容量偏空", lowFreqPools, new Color(0.35f, 0.75f, 1f));
                }

            }
            if (!poolUsageSnapshotHasGlobalGroup)
            {
                SimpleToolsPanelUtility.DrawEmptyState("全局统计组还没有初始化。进入 Play Mode 或触发对象池创建后，这里会显示运行时统计。");
                return;
            }

            SimpleToolsPanelUtility.DrawSummary(
                $"池数量: {poolUsageSnapshotTotalPools}",
                $"平均总利用率: {avgTotalUtilization:P2}",
                $"活跃: {poolUsageSnapshotTotalActive}",
                $"池中: {poolUsageSnapshotTotalPooled}",
                $"丢弃: {poolUsageSnapshotTotalDiscarded}");

            if (poolUsageSnapshotTotalPools == 0)
            {
                SimpleToolsPanelUtility.DrawEmptyState("全局统计组还没有初始化。进入 Play Mode 或触发对象池创建后，这里会显示运行时统计。");
                return;
            }

            // 搜索框是否出现也使用同一份 Layout 快照，避免运行时池组变化导致
            // Layout/Repaint 生成不同数量的 GUILayout 控件。
            if (poolGroupSearchVisible)
            {
                searchText = EditorGUILayout.TextField("搜索 (组或池名)", searchText);
            }

            for (int snapshotIndex = 0; snapshotIndex < poolGroupRenderSnapshotCount; snapshotIndex++)
            {
                PoolGroupRenderSnapshot groupSnapshot = poolGroupRenderSnapshots[snapshotIndex];
                // 分组折叠
                using (SimpleToolsPanelUtility.BeginContentSection())
                {
                    bool nextExpanded = EditorGUILayout.Foldout(
                        groupSnapshot.Expanded,
                        $"分组: {groupSnapshot.GroupKey} ({groupSnapshot.PoolCount} 个池)",
                        true);

                    if (nextExpanded != groupSnapshot.Expanded)
                    {
                        // 本次事件仍按 Layout 快照绘制；折叠状态在下一次 Layout
                        // 重建后才影响子行数量，避免 MouseUp/Repaint 控件树失配。
                        foldouts[groupSnapshot.GroupKey] = nextExpanded;
                        poolGroupRenderSnapshotValid = false;
                        SimpleToolsWindow.UsingWindow?.Repaint();
                    }

                    if (groupSnapshot.Expanded)
                    {
                        for (int i = 0; i < groupSnapshot.PoolLines.Count; i++)
                            EditorGUILayout.LabelField(groupSnapshot.PoolLines[i], EditorStyles.miniLabel);
                    }
                }
            }
        }

        private void EnsurePoolUsageRenderSnapshot()
        {
            bool isLayout = Event.current == null || Event.current.type == EventType.Layout;
            if (!isLayout)
                return;

            JumpSafeKeyGroup<string, PoolStatistics> globalGroup = PoolStatistics.GlobalStatisticsGroup;
            bool hasLargeGroup = globalGroup != null && globalGroup.Groups.Any(kvp => kvp.Value != null && kvp.Value.Count() >= 3);
            int currentSignature = CalculatePoolGroupRenderSignature(globalGroup, hasLargeGroup);
            if (poolGroupRenderSnapshotValid && currentSignature == poolGroupRenderSignature)
                return;

            poolUsageSnapshotHasGlobalGroup = globalGroup != null;
            poolUsageSnapshotTotalPools = 0;
            poolUsageSnapshotTotalActive = 0;
            poolUsageSnapshotTotalPooled = 0;
            poolUsageSnapshotTotalDiscarded = 0;
            poolGroupSearchVisible = hasLargeGroup || foldouts.Count > 0;
            poolGroupSnapshotSearchText = searchText ?? string.Empty;

            if (globalGroup == null)
            {
                poolGroupRenderSnapshotCount = 0;
                poolGroupRenderSignature = currentSignature;
                poolGroupRenderSnapshotValid = true;
                return;
            }

            List<PoolStatistics> allStats = CollectValidStats(globalGroup);
            poolUsageSnapshotTotalPools = allStats.Count;
            for (int i = 0; i < allStats.Count; i++)
            {
                PoolStatistics stat = allStats[i];
                poolUsageSnapshotTotalActive += stat.CurrentActive;
                poolUsageSnapshotTotalPooled += stat.CurrentPooled;
                poolUsageSnapshotTotalDiscarded += stat.DiscardedCount;
            }

            RebuildPoolGroupRenderSnapshot(globalGroup);
            poolGroupRenderSignature = currentSignature;
            poolGroupRenderSnapshotValid = true;
        }

        private void RebuildPoolGroupRenderSnapshot(JumpSafeKeyGroup<string, PoolStatistics> globalGroup)
        {
            int snapshotWriteIndex = 0;

            foreach (var groupKey in globalGroup.Groups.Keys.ToList())
            {
                var groupList = globalGroup.GetGroupDirectly(groupKey);
                if (groupList == null || groupList.Count() == 0)
                    continue;

                bool groupMatches = string.IsNullOrEmpty(poolGroupSnapshotSearchText) ||
                                    ContainsIgnoreCase(groupKey, poolGroupSnapshotSearchText);
                PoolGroupRenderSnapshot snapshot = GetReusablePoolGroupSnapshot(snapshotWriteIndex);
                snapshot.PoolLines.Clear();
                int validCount = 0;
                for (int i = 0; i < groupList.Count(); i++)
                {
                    var stat = groupList.ValuesNow[i];
                    if (stat == null || !stat.IsValid)
                        continue;

                    validCount++;
                    if (!groupMatches && !ContainsIgnoreCase(stat.PoolDisplayName, poolGroupSnapshotSearchText))
                        continue;

                    snapshot.PoolLines.Add($"{stat.PoolDisplayName} · 池中 {stat.CurrentPooled} · 活跃 {stat.CurrentActive} · 丢弃 {stat.DiscardedCount}");
                }

                if (validCount == 0 || (!groupMatches && snapshot.PoolLines.Count == 0))
                    continue;

                snapshot.GroupKey = groupKey;
                snapshot.PoolCount = validCount;
                snapshot.Expanded = foldouts[groupKey];
                snapshotWriteIndex++;
            }

            poolGroupRenderSnapshotCount = snapshotWriteIndex;
        }

        private int CalculatePoolGroupRenderSignature(JumpSafeKeyGroup<string, PoolStatistics> globalGroup, bool hasLargeGroup)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (hasLargeGroup ? 1 : 0);
                hash = hash * 31 + (searchText == null ? 0 : searchText.GetHashCode());
                hash = hash * 31 + foldouts.Count;
                if (globalGroup == null)
                    return hash;

                foreach (var pair in globalGroup.Groups)
                {
                    hash = hash * 31 + (pair.Key == null ? 0 : pair.Key.GetHashCode());
                    var groupList = pair.Value;
                    if (groupList == null)
                        continue;

                    for (int i = 0; i < groupList.Count(); i++)
                    {
                        PoolStatistics stat = groupList.ValuesNow[i];
                        if (stat == null)
                        {
                            hash = hash * 31;
                            continue;
                        }

                        hash = hash * 31 + (stat.IsValid ? 1 : 0);
                        hash = hash * 31 + (stat.PoolDisplayName == null ? 0 : stat.PoolDisplayName.GetHashCode());
                        hash = hash * 31 + stat.CurrentPooled;
                        hash = hash * 31 + stat.CurrentActive;
                        hash = hash * 31 + stat.DiscardedCount;
                    }
                }

                return hash;
            }
        }

        private PoolGroupRenderSnapshot GetReusablePoolGroupSnapshot(int index)
        {
            while (poolGroupRenderSnapshots.Count <= index)
                poolGroupRenderSnapshots.Add(new PoolGroupRenderSnapshot());

            return poolGroupRenderSnapshots[index];
        }

        private sealed class PoolGroupRenderSnapshot
        {
            public string GroupKey;
            public int PoolCount;
            public bool Expanded;
            public readonly List<string> PoolLines = new List<string>(8);

            public PoolGroupRenderSnapshot()
            {
            }
        }

        private void DrawPoolActionPanel()
        {
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("分析池使用情况", SimpleToolsActionTone.Primary, 26, GUILayout.Width(120)))
                    {
                        AnalyzePoolUsage();
                        showUsageAnalysis = true;
                    }

                    if (SimpleToolsPanelUtility.DrawActionButton(showUsageAnalysis ? "收起分析" : "展开分析", SimpleToolsActionTone.Neutral, 26, GUILayout.Width(88)))
                        showUsageAnalysis = !showUsageAnalysis;

                    if (SimpleToolsPanelUtility.DrawActionButton("复制分析", SimpleToolsActionTone.Neutral, 26, GUILayout.Width(76)))
                        EditorGUIUtility.systemCopyBuffer = lastResultSummary + "\n" + lastResultDetail;

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField("判断口径：高频扩容偏高通常说明池容量偏小或创建压力高；低频/容量偏空通常说明预热过量。", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawPrewarmDataPanel()
        {
            var infos = FindPrewarmDataInfos();
            SimpleToolsPanelUtility.DrawSectionTitle("Prefab 预热数据", "PrefabPrewarmDataInfo 是 ESSO/SoDataInfo 配置资产，也是 ESGameObjectPoolModule 的预热配置入口。这里扫描的是配置资产，不扫描 Prefab。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton(
                            "刷新配置事实",
                            "显式查询 ESSO/SoDataInfo 中的 PrefabPrewarmDataInfo；页面打开和重绘不会自动扫描。",
                            SimpleToolsActionTone.Primary,
                            24,
                            GUILayout.Width(104)))
                    {
                        RefreshPrewarmDataCache();
                    }
                    EditorGUILayout.LabelField(
                        prewarmDataCacheReady ? $"缓存 {prewarmDataCache.Count} 个配置" : "尚未刷新配置事实",
                        EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                    prewarmSearchText = EditorGUILayout.TextField(prewarmSearchText);
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                        prewarmSearchText = string.Empty;
                }

                int entryCount = infos.Sum(info => info != null && info.entries != null ? info.entries.Count : 0);
                int enabledEntryCount = infos.Sum(info => info != null && info.entries != null ? info.entries.Count(entry => entry != null && entry.enabled) : 0);
                int missingPrefabCount = infos.Sum(info => info != null && info.entries != null ? info.entries.Count(IsMissingPrefabKey) : 0);
                int invalidCount = infos.Sum(CountInvalidPrewarmEntries);
                int duplicateKeyCount = infos.Sum(CountDuplicatePrewarmKeys);
                SimpleToolsPanelUtility.DrawSummary(
                    $"配置资产: {infos.Count}",
                    $"条目: {entryCount}",
                    $"启用: {enabledEntryCount}",
                    $"Prefab丢失: {missingPrefabCount}",
                    $"异常条目: {invalidCount}",
                    $"重复Key: {duplicateKeyCount}");

                if (infos.Count == 0)
                {
                    SimpleToolsPanelUtility.DrawEmptyState(
                        prewarmDataCacheReady
                            ? "当前项目没有找到 PrefabPrewarmDataInfo。可通过 SO 数据窗口创建“Prefab预热配置”，再回到这里维护条目。"
                            : "尚未刷新配置事实。点击上方“刷新配置事实”后，工具才会查询已有 PrefabPrewarmDataInfo；不会扫描 Project Prefab。 ");
                    return;
                }

                foreach (var info in infos)
                    DrawPrewarmInfoRow(info);
            }
        }

        private void DrawGameManagerPoolPanel()
        {
            var infos = FindPrewarmDataInfos();
            ESGameManager manager = ResolveSceneGameManager();
            ESGameObjectPoolModule pool = ResolvePoolModule(manager, false);
            int sourceCount = pool != null && pool.prewarmSources != null ? pool.prewarmSources.Count : 0;
            bool targetLinked = pool != null && targetPrewarmData != null && pool.prewarmSources != null && pool.prewarmSources.Contains(targetPrewarmData);

            SimpleToolsPanelUtility.DrawSectionTitle("GameManager 接入", "把已有 PrefabPrewarmDataInfo 配置资产接入当前场景的 ESGameManager；编辑模式只改配置关系，不实例化池对象。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                SimpleToolsPanelUtility.DrawSummary(
                    $"GameManager: {GetManagerName(manager)}",
                    $"对象池模块: {GetPoolStateText(pool)}",
                    $"配置资产: {(prewarmDataCacheReady ? infos.Count.ToString() : "未刷新")}",
                    $"目标已接入: {SimpleToolsSafetyUtility.YesNo(targetLinked)}",
                    $"运行状态: {GetRuntimeStateText()}");

                EditorGUILayout.HelpBox("建议流程：先在“预热数据”页选择已有 PrefabPrewarmDataInfo 配置资产，再到这里把该配置接入 GameManager。编辑模式不会实例化对象池；进入 Play Mode 后由模块按场景和 Space 条件加载。", MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("目标预热数据", EditorStyles.miniBoldLabel, GUILayout.Width(82));
                    targetPrewarmData = (PrefabPrewarmDataInfo)EditorGUILayout.ObjectField(targetPrewarmData, typeof(PrefabPrewarmDataInfo), false);
                    if (targetPrewarmData != null && GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        Selection.activeObject = targetPrewarmData;
                        EditorGUIUtility.PingObject(targetPrewarmData);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("定位 GameManager", SimpleToolsActionTone.Neutral, 28, GUILayout.Width(110)))
                        PingGameManager(manager);

                    if (SimpleToolsPanelUtility.DrawActionButton("获取/创建对象池模块", SimpleToolsActionTone.Primary, 28, GUILayout.Width(140)))
                        CreatePoolModuleForManager(manager);

                    if (SimpleToolsPanelUtility.DrawActionButton("接入目标预热数据", SimpleToolsActionTone.Warning, 28, GUILayout.Width(130)))
                        LinkTargetPrewarmToGameManager(manager);

                    if (SimpleToolsPanelUtility.DrawActionButton("移除目标接入", SimpleToolsActionTone.Danger, 28, GUILayout.Width(110)))
                        UnlinkTargetPrewarmFromGameManager(manager, false);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = Application.isPlaying;
                    if (SimpleToolsPanelUtility.DrawActionButton("运行时加载目标", SimpleToolsActionTone.Success, 28, GUILayout.Width(120)))
                        LoadTargetPrewarmNow(manager);

                    if (SimpleToolsPanelUtility.DrawActionButton("刷新当前场景预热", SimpleToolsActionTone.Success, 28, GUILayout.Width(130)))
                        RefreshCurrentScenePrewarm(manager);

                    if (SimpleToolsPanelUtility.DrawActionButton("运行时卸载目标", SimpleToolsActionTone.Danger, 28, GUILayout.Width(120)))
                        UnlinkTargetPrewarmFromGameManager(manager, true);
                    GUI.enabled = true;
                    GUILayout.FlexibleSpace();
                }

                DrawGameManagerPrewarmSourceList(pool);
                SimpleToolsPanelUtility.DrawResultSummary("最近 GameManager 接入结果", lastResultSummary, lastResultDetail);
            }
        }

        private void DrawPlayModePoolGroupsPanel()
        {
            ESGameManager manager = ResolveSceneGameManager();
            ESGameObjectPoolModule pool = ResolvePoolModule(manager, false);
            int sourceCount = pool != null && pool.prewarmSources != null ? pool.prewarmSources.Count : 0;
            int missingPrefabCount = FindPrewarmDataInfos().Sum(CountMissingPrefabKeys);
            List<ESGameObjectPoolStats> stats = Application.isPlaying && pool != null
                ? CollectPoolGroupStats(pool)
                : new List<ESGameObjectPoolStats>(0);

            SimpleToolsPanelUtility.DrawSectionTitle("PlayMode 池组状态", "只读查看 ESGameObjectPoolModule 当前已经创建的池组。此页不创建、不预热、不回收对象。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                SimpleToolsPanelUtility.DrawSummary(
                    $"运行状态: {GetRuntimeStateText()}",
                    $"GameManager: {GetManagerName(manager)}",
                    $"对象池模块: {GetPoolStateText(pool)}",
                    $"预热配置数: {sourceCount}",
                    $"活跃: {stats.Sum(item => item.activeCount)}",
                    $"池中: {stats.Sum(item => item.inactiveCount)}",
                    $"Prefab丢失: {missingPrefabCount}");

                if (!Application.isPlaying)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("当前项目没有找到 PrefabPrewarmDataInfo。可通过 SO 数据窗口创建“Prefab预热配置”，再回到这里维护条目。");
                    return;
                }

                if (pool == null)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("当前项目没有找到 PrefabPrewarmDataInfo。可通过 SO 数据窗口创建“Prefab预热配置”，再回到这里维护条目。");
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                    poolGroupSearchText = EditorGUILayout.TextField(poolGroupSearchText);
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                        poolGroupSearchText = string.Empty;
                    if (GUILayout.Button("复制报告", EditorStyles.miniButton, GUILayout.Width(72)))
                        EditorGUIUtility.systemCopyBuffer = BuildPoolGroupStatusReport(stats);
                }

                if (stats.Count == 0)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("当前项目没有找到 PrefabPrewarmDataInfo。可通过 SO 数据窗口创建“Prefab预热配置”，再回到这里维护条目。");
                    return;
                }

                DrawPoolGroupStatsTable(stats);
            }
        }

        private static ESGameManager ResolveSceneGameManager()
        {
            if (ESGameManager.Instance != null)
                return ESGameManager.Instance;

#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<ESGameManager>(FindObjectsInactive.Include);
#else
            ESGameManager[] managers = Resources.FindObjectsOfTypeAll<ESGameManager>();
            return managers.FirstOrDefault(item => item != null && item.gameObject != null && item.gameObject.scene.IsValid());
#endif
        }

        private static ESGameObjectPoolModule ResolvePoolModule(ESGameManager manager, bool createIfMissing)
        {
            if (manager == null)
                return null;

            if (manager.ModuleTables != null && manager.ModuleTables.TryGetValue(typeof(ESGameObjectPoolModule), out IModule module))
                return module as ESGameObjectPoolModule;

            if (ESGameManager.PoolModule != null && ESGameManager.PoolModule.Core_Object == manager)
                return ESGameManager.PoolModule;

            ESGameObjectPoolModule serializedPool = manager.flowDomain != null ? manager.flowDomain.FindMyModule<ESGameObjectPoolModule>() : null;
            if (serializedPool != null)
            {
                serializedPool._SetDomainCreateRelationshipOnly(manager.flowDomain);
                return serializedPool;
            }

            return createIfMissing ? manager.GetMoudle<ESGameObjectPoolModule>() : null;
        }

        private static void PingGameManager(ESGameManager manager)
        {
            if (manager == null)
            {
                EditorUtility.DisplayDialog("没有找到 GameManager", "当前场景里没有 ESGameManager。请先放置或打开带 ESGameManager 的场景。", "知道了");
                return;
            }

            Selection.activeObject = manager.gameObject;
            EditorGUIUtility.PingObject(manager.gameObject);
        }

        private void CreatePoolModuleForManager(ESGameManager manager)
        {
            if (manager == null)
            {
                EditorUtility.DisplayDialog("没有找到 GameManager", "当前场景里没有 ESGameManager，不能创建对象池模块。", "知道了");
                return;
            }

            if (!EditorUtility.DisplayDialog("创建对象池模块", $"将在 {manager.name} 上获取或创建 ESGameObjectPoolModule。该操作会修改 GameManager 的模块配置。", "继续", "取消"))
                return;

            Undo.RecordObject(manager, "Create GameManager Pool Module");
            ESGameObjectPoolModule pool = ResolvePoolModule(manager, true);
            manager.flowDomain?.MyModules?.ApplyBuffers();
            MarkGameManagerDirty(manager);
            lastResultSummary = pool != null ? "对象池模块已就绪" : "对象池模块创建失败";
            lastResultDetail = pool != null
                ? $"GameManager: {manager.name}\n模块: {pool.GetType().Name}\n注意：编辑模式只完成配置关系，运行时池对象会在 Play Mode 加载。"
                : "没有找到匹配的 Domain，或模块注册被项目逻辑拒绝。";
        }

        private void LinkTargetPrewarmToGameManager(ESGameManager manager)
        {
            if (!ValidateTargetPrewarmAndManager(manager, out ESGameObjectPoolModule pool))
                return;

            Undo.RecordObject(manager, "Link Prefab Prewarm To GameManager");
            pool.prewarmSources ??= new List<PrefabPrewarmDataInfo>(8);
            bool exists = pool.prewarmSources.Contains(targetPrewarmData);
            if (!exists)
                pool.prewarmSources.Add(targetPrewarmData);

            MarkGameManagerDirty(manager);
            lastResultSummary = exists ? "目标预热数据已经接入" : "目标预热数据已接入 GameManager";
            lastResultDetail = BuildGameManagerPrewarmDetail(manager, pool);
        }

        private void LoadTargetPrewarmNow(ESGameManager manager)
        {
            if (!ValidateTargetPrewarmAndManager(manager, out ESGameObjectPoolModule pool))
                return;

            pool.RegisterPrewarmSource(targetPrewarmData, true);
            lastResultSummary = "已在运行时加载目标预热数据";
            lastResultDetail = BuildGameManagerPrewarmDetail(manager, pool);
        }

        private void RefreshCurrentScenePrewarm(ESGameManager manager)
        {
            ESGameObjectPoolModule pool = ResolvePoolModule(manager, false);
            if (manager == null || pool == null)
            {
                EditorUtility.DisplayDialog("对象池模块未就绪", "请先让当前场景存在 ESGameManager 和 ESGameObjectPoolModule。", "知道了");
                return;
            }

            pool.RefreshPrewarmForCurrentScene();
            lastResultSummary = "已刷新当前场景预热";
            lastResultDetail = BuildGameManagerPrewarmDetail(manager, pool);
        }

        private void UnlinkTargetPrewarmFromGameManager(ESGameManager manager, bool unloadImmediately)
        {
            if (!ValidateTargetPrewarmAndManager(manager, out ESGameObjectPoolModule pool))
                return;

            string action = unloadImmediately ? "卸载并移除" : "移除";
            if (!EditorUtility.DisplayDialog($"{action}目标预热数据", $"将从 {manager.name} 的对象池模块中{action}：\n{targetPrewarmData.KeyName ?? targetPrewarmData.name}", "继续", "取消"))
                return;

            if (Application.isPlaying && unloadImmediately)
            {
                pool.RemovePrewarmSource(targetPrewarmData, true);
            }
            else
            {
                Undo.RecordObject(manager, "Unlink Prefab Prewarm From GameManager");
                pool.prewarmSources?.Remove(targetPrewarmData);
                MarkGameManagerDirty(manager);
            }

            lastResultSummary = $"已{action}目标预热数据";
            lastResultDetail = BuildGameManagerPrewarmDetail(manager, pool);
        }

        private bool ValidateTargetPrewarmAndManager(ESGameManager manager, out ESGameObjectPoolModule pool)
        {
            pool = ResolvePoolModule(manager, false);
            if (targetPrewarmData == null)
            {
                EditorUtility.DisplayDialog("需要目标预热数据", "请先选择一个 PrefabPrewarmDataInfo。", "知道了");
                return false;
            }

            if (manager == null)
            {
                EditorUtility.DisplayDialog("没有找到 GameManager", "当前场景里没有 ESGameManager。", "知道了");
                return false;
            }

            if (pool == null)
            {
                EditorUtility.DisplayDialog("对象池模块未创建", "请先点击“获取/创建对象池模块”，再接入预热数据。", "知道了");
                return false;
            }

            return true;
        }

        private static void MarkGameManagerDirty(ESGameManager manager)
        {
            if (manager == null)
                return;

            EditorUtility.SetDirty(manager);
            if (manager.gameObject != null && manager.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }

        private void DrawGameManagerPrewarmSourceList(ESGameObjectPoolModule pool)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("GameManager 当前预热配置", EditorStyles.boldLabel);
            if (pool == null || pool.prewarmSources == null || pool.prewarmSources.Count == 0)
            {
                SimpleToolsPanelUtility.DrawEmptyState("对象池模块还没有接入任何 PrefabPrewarmDataInfo。先选择目标预热数据，再点击“接入目标预热数据”。");
                return;
            }

            for (int i = 0; i < pool.prewarmSources.Count; i++)
            {
                PrefabPrewarmDataInfo info = pool.prewarmSources[i];
                string path = info != null ? AssetDatabase.GetAssetPath(info) : "<丢失>";
                int entryCount = info != null && info.entries != null ? info.entries.Count : 0;
                int enabledCount = info != null && info.entries != null ? info.entries.Count(entry => entry != null && entry.enabled) : 0;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"#{i + 1}", EditorStyles.miniLabel, GUILayout.Width(30));
                    EditorGUILayout.LabelField(info != null ? (info.KeyName ?? info.name) : "<丢失>", EditorStyles.miniLabel, GUILayout.Width(180));
                    EditorGUILayout.LabelField($"条目 {entryCount} | 启用 {enabledCount}", EditorStyles.miniLabel, GUILayout.Width(110));
                    EditorGUILayout.LabelField(path, EditorStyles.miniLabel, GUILayout.MinWidth(220));
                    if (info != null && GUILayout.Button("设为目标", EditorStyles.miniButton, GUILayout.Width(64)))
                        targetPrewarmData = info;
                    if (info != null && GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        Selection.activeObject = info;
                        EditorGUIUtility.PingObject(info);
                    }
                }
            }
        }

        private void DrawPoolGroupStatsTable(List<ESGameObjectPoolStats> stats)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Key", EditorStyles.miniBoldLabel, GUILayout.MinWidth(220));
                EditorGUILayout.LabelField("活跃", EditorStyles.miniBoldLabel, GUILayout.Width(44));
                EditorGUILayout.LabelField("池中", EditorStyles.miniBoldLabel, GUILayout.Width(44));
                EditorGUILayout.LabelField("总量", EditorStyles.miniBoldLabel, GUILayout.Width(44));
                EditorGUILayout.LabelField("创建", EditorStyles.miniBoldLabel, GUILayout.Width(48));
                EditorGUILayout.LabelField("借出", EditorStyles.miniBoldLabel, GUILayout.Width(48));
                EditorGUILayout.LabelField("归还", EditorStyles.miniBoldLabel, GUILayout.Width(48));
                EditorGUILayout.LabelField("Miss", EditorStyles.miniBoldLabel, GUILayout.Width(48));
                EditorGUILayout.LabelField("修补", EditorStyles.miniBoldLabel, GUILayout.Width(48));
                EditorGUILayout.LabelField("溢出销毁", EditorStyles.miniBoldLabel, GUILayout.Width(68));
                EditorGUILayout.LabelField("预热源", EditorStyles.miniBoldLabel, GUILayout.Width(56));
            }

            int shown = 0;
            foreach (var stat in stats.OrderByDescending(item => item.activeCount).ThenBy(item => item.key, StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(poolGroupSearchText) &&
                    !ContainsIgnoreCase(stat.key, poolGroupSearchText))
                    continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(stat.key ?? "<无Key>", EditorStyles.miniLabel, GUILayout.MinWidth(220));
                    EditorGUILayout.LabelField(stat.activeCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(44));
                    EditorGUILayout.LabelField(stat.inactiveCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(44));
                    EditorGUILayout.LabelField(stat.totalCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(44));
                    EditorGUILayout.LabelField(stat.createdCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(48));
                    EditorGUILayout.LabelField(stat.rentCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(48));
                    EditorGUILayout.LabelField(stat.returnCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(48));
                    EditorGUILayout.LabelField(stat.missCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(48));
                    EditorGUILayout.LabelField(stat.repairCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(48));
                    EditorGUILayout.LabelField(stat.overflowDestroyCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(68));
                    EditorGUILayout.LabelField(stat.prewarmSourceCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(56));
                }

                shown++;
                if (shown >= 120)
                {
                    EditorGUILayout.HelpBox("池组超过 120 条，已截断显示。请用搜索缩小范围，复制报告仍包含当前收集到的全部池组。", MessageType.Info);
                    break;
                }
            }

            if (shown == 0)
                SimpleToolsPanelUtility.DrawEmptyState("对象池模块还没有接入任何 PrefabPrewarmDataInfo。先选择目标预热数据，再点击“接入目标预热数据”。");
        }

        private static List<ESGameObjectPoolStats> CollectPoolGroupStats(ESGameObjectPoolModule pool)
        {
            List<ESGameObjectPoolStats> result = new List<ESGameObjectPoolStats>(32);
            if (pool == null)
                return result;

            foreach (string key in CollectPoolGroupKeys(pool))
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                if (pool.TryGetStats(key, out ESGameObjectPoolStats stats))
                    result.Add(stats);
            }

            return result;
        }

        private static IEnumerable<string> CollectPoolGroupKeys(ESGameObjectPoolModule pool)
        {
            if (pool == null)
                yield break;

            FieldInfo field = typeof(ESGameObjectPoolModule).GetField("groupsByKey", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(pool) is not IDictionary dictionary)
                yield break;

            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is string key)
                    yield return key;
            }
        }

        private static string BuildPoolGroupStatusReport(List<ESGameObjectPoolStats> stats)
        {
            if (stats == null || stats.Count == 0)
                return "当前没有运行时池组。";

            return string.Join("\n", stats
                .OrderByDescending(item => item.activeCount)
                .ThenBy(item => item.key, StringComparer.OrdinalIgnoreCase)
                .Select(item =>
                    $"{item.key} | 活跃 {item.activeCount} | 池中 {item.inactiveCount} | 总量 {item.totalCount} | 创建 {item.createdCount} | 借出 {item.rentCount} | 归还 {item.returnCount} | Miss {item.missCount} | 修补 {item.repairCount} | 溢出销毁 {item.overflowDestroyCount} | 预热源 {item.prewarmSourceCount}"));
        }

        private static string BuildGameManagerPrewarmDetail(ESGameManager manager, ESGameObjectPoolModule pool)
        {
            if (manager == null || pool == null)
                return "GameManager 或 ESGameObjectPoolModule 不存在。";

            IEnumerable<string> lines = pool.prewarmSources == null
                ? Enumerable.Empty<string>()
                : pool.prewarmSources.Select(info => info == null
                    ? "<丢失>"
                    : $"{info.KeyName ?? info.name} | 条目 {(info.entries != null ? info.entries.Count : 0)} | {AssetDatabase.GetAssetPath(info)}");

            return $"GameManager: {manager.name}\n当前 Space: {pool.currentSpaceName}\n自动 Start 预热: {pool.loadPrewarmOnStart}\n监听场景加载: {pool.autoLoadOnSceneLoaded}\n配置列表:\n{SimpleToolsSafetyUtility.JoinPreview(lines, 16)}";
        }

        private List<PrefabPrewarmDataInfo> FindPrewarmDataInfos()
        {
            if (!prewarmDataCacheReady)
                return new List<PrefabPrewarmDataInfo>(0);

            var result = new List<PrefabPrewarmDataInfo>(prewarmDataCache.Count);
            string keyword = prewarmSearchText == null ? string.Empty : prewarmSearchText.Trim();
            foreach (var info in prewarmDataCache)
            {
                if (info == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(info);
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    if (!ContainsIgnoreCase(info.name, keyword) &&
                        !ContainsIgnoreCase(info.KeyName, keyword) &&
                        !ContainsIgnoreCase(path, keyword))
                        continue;
                }

                result.Add(info);
            }

            return result;
        }

        private void RefreshPrewarmDataCache()
        {
            var infos = ESEditorSO.GetGroupOfType<PrefabPrewarmDataInfo>() ?? new List<PrefabPrewarmDataInfo>(0);
            prewarmDataCache.Clear();
            prewarmDataCache.AddRange(
                infos.Where(info => info != null)
                    .Distinct()
                    .OrderBy(info => AssetDatabase.GetAssetPath(info), StringComparer.OrdinalIgnoreCase));
            prewarmDataCacheReady = true;
            lastResultSummary = $"预热配置事实刷新完成: {prewarmDataCache.Count} 个配置资产";
            lastResultDetail = "查询来源：ESSO/SoDataInfo；未扫描 Project Prefab。";
        }

        private void DrawPrewarmInfoRow(PrefabPrewarmDataInfo info)
        {
            if (info == null)
                return;

            string path = AssetDatabase.GetAssetPath(info);
            int count = info.entries != null ? info.entries.Count : 0;
            int enabled = info.entries != null ? info.entries.Count(entry => entry != null && entry.enabled) : 0;
            int missing = info.entries != null ? info.entries.Count(IsMissingPrefabKey) : 0;
            int invalid = CountInvalidPrewarmEntries(info);
            int duplicate = CountDuplicatePrewarmKeys(info);
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(info.KeyName ?? info.name, EditorStyles.boldLabel, GUILayout.Width(180));
                    EditorGUILayout.LabelField($"条目 {count} | 启用 {enabled} | 丢失 {missing} | 异常 {invalid} | 重复 {duplicate}", EditorStyles.miniLabel, GUILayout.Width(250));
                    EditorGUILayout.LabelField(path, EditorStyles.miniLabel, GUILayout.MinWidth(200));
                    if (GUILayout.Button("选为目标", EditorStyles.miniButton, GUILayout.Width(68)))
                        targetPrewarmData = info;
                    if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        Selection.activeObject = info;
                        EditorGUIUtility.PingObject(info);
                    }
                }

                if (missing > 0)
                    EditorGUILayout.HelpBox("存在 Prefab 丢失条目，运行时预热会跳过这些项。", MessageType.Warning);
                if (invalid > 0)
                    EditorGUILayout.HelpBox("存在 Key 为空、预热数量小于等于 0、或启用项 Prefab 为空的异常条目。", MessageType.Warning);
                if (duplicate > 0)
                    EditorGUILayout.HelpBox("存在重复 Key。运行时同 Key 池组会合并或覆盖预期，请明确配置。", MessageType.Warning);
            }
        }

        private static int CountInvalidPrewarmEntries(PrefabPrewarmDataInfo info)
        {
            if (info == null || info.entries == null)
                return 0;

            int count = 0;
            foreach (var entry in info.entries)
            {
                if (entry == null)
                {
                    count++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.key) || entry.prewarmCount <= 0 || (entry.enabled && IsMissingPrefabKey(entry)))
                    count++;
            }

            return count;
        }

        private static int CountMissingPrefabKeys(PrefabPrewarmDataInfo info)
        {
            if (info == null || info.entries == null)
                return 0;

            return info.entries.Count(IsMissingPrefabKey);
        }

        private static bool IsMissingPrefabKey(PrefabPrewarmEntry entry)
        {
            return entry == null || entry.prefabKey == null ||
                   (entry.prefabKey.EnumKeyInt == 0 && string.IsNullOrWhiteSpace(entry.prefabKey.StringKey));
        }

        private static int CountDuplicatePrewarmKeys(PrefabPrewarmDataInfo info)
        {
            if (info == null || info.entries == null)
                return 0;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            int duplicates = 0;
            foreach (var entry in info.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    continue;

                if (!seen.Add(entry.key))
                    duplicates++;
            }

            return duplicates;
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

            // 计算均值。
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

        private static string GetManagerName(ESGameManager manager)
        {
            return manager != null ? manager.name : "未找到";
        }

        private static string GetPoolStateText(ESGameObjectPoolModule pool)
        {
            return pool != null ? "已存在" : "未创建";
        }

        private static string GetRuntimeStateText()
        {
            return Application.isPlaying ? "Play Mode" : "编辑模式";
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
