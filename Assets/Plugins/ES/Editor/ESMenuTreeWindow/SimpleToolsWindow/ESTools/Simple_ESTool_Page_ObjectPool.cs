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


namespace ES
{
    #region ES集成工具 - 对象池工具
    [Serializable]
    public class Page_ObjectPool : ESWindowPageBase
    {
        public enum ObjectPoolToolTab
        {
            运行时统计,
            PrefabPrewarmDataInfo审计,
            GameManager接入,
            PlayMode池组状态
        }

        [DisplayAsString(Overflow = true, FontSize = 13), HideLabel]
        public string PoolCoreInfo = "";

        [Title("对象池集成工具", "ES 对象池运行时数据汇总管理", bold: true, titleAlignment: TitleAlignments.Centered)]
        [InfoBox("汇总 Poolable-Define.cs 支持的对象池运行时统计。用于查看池数量、创建量、活跃量、回收量和丢弃量。", InfoMessageType.Info)]
        [DisplayAsString(fontSize: 12), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "支持 IPoolable / IPoolableAuto / Pool<T> / ESSimplePool<T> / ESSimplePoolSingleton<T> / PoolStatistics。";

        // 折叠状态字典
        private SafeDictionary<string, bool> foldouts = new SafeDictionary<string, bool>(() => true);

        // 搜索文本
        private string searchText = "";
        private string prewarmSearchText = "";
        private string poolGroupSearchText = "";

        [EnumToggleButtons, HideLabel]
        public ObjectPoolToolTab currentTab = ObjectPoolToolTab.运行时统计;

        [LabelText("目标预热数据")]
        public PrefabPrewarmDataInfo targetPrewarmData;

        // 使用状况查询结果
        private bool showUsageAnalysis = false;
        private List<PoolStatistics> highFreqPools = new List<PoolStatistics>();
        private List<PoolStatistics> lowFreqPools = new List<PoolStatistics>();
        private float avgRealTimeUtilization;
        private float avgTotalUtilization;
        private float avgDiscarded;
        private string lastResultSummary = "";
        private string lastResultDetail = "";

        [OnInspectorGUI, PropertyOrder(100)]
        public void DrawThisWindow()
        {
            SimpleToolsPanelUtility.DrawToolHeader(
                "对象池与预热配置",
                "对象池与预热配置",
                SimpleToolsMaturity.Upgrading,
                "PrefabPrewarmDataInfo 是 ESSO/SoDataInfo 资产；工具只扫描这种配置资产，不再把当前 Selection 伪装成池化入口。");

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

        private void DrawPoolUsagePanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("对象池使用情况", "查看运行时池数量、活跃量、回收量和异常丢弃。");

            DrawPoolActionPanel();
            SimpleToolsPanelUtility.DrawResultSummary("最近对象池分析", lastResultSummary, lastResultDetail);

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
                $"平均总利用率: {avgTotalUtilization:P2}",
                $"平均总利用率: {avgTotalUtilization:P2}",
                $"活跃: {totalActive}",
                $"池中: {totalPooled}",
                $"丢弃: {totalDiscarded}");

            if (totalPools == 0)
            {
                SimpleToolsPanelUtility.DrawEmptyState("全局统计组还没有初始化。进入 Play Mode 或触发对象池创建后，这里会显示运行时统计。");
                return;
            }

            bool hasLargeGroup = globalGroup.Groups.Any(kvp => kvp.Value != null && kvp.Value.Count() >= 3);

            // 如果有大组或有折叠功能，显示搜索栏。
            if (hasLargeGroup || foldouts.Count > 0)
            {
                searchText = EditorGUILayout.TextField("搜索 (组或池名)", searchText);
            }

            // 遍历所有分组。
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

                            // 如果有搜索文本且不匹配组名，检查池项。
                            if (!string.IsNullOrEmpty(searchText) && !groupMatches &&
                                !ContainsIgnoreCase(stat.PoolDisplayName, searchText))
                            {
                                continue;
                            }

                            EditorGUILayout.LabelField(
                                $"池中: {totalPooled}",
                                EditorStyles.miniLabel);
                        }
                    }
                }
            }
        }

        private void DrawPoolActionPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
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
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
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
                    SimpleToolsPanelUtility.DrawEmptyState("当前项目没有找到 PrefabPrewarmDataInfo。可通过 SO 数据窗口创建“Prefab预热配置”，再回到这里维护条目。");
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

            SimpleToolsPanelUtility.DrawSectionTitle("Prefab 预热数据", "PrefabPrewarmDataInfo 是 ESSO/SoDataInfo 配置资产，也是 ESGameObjectPoolModule 的预热配置入口。这里扫描的是配置资产，不扫描 Prefab。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SimpleToolsPanelUtility.DrawSummary(
                    $"GameManager: {GetManagerName(manager)}",
                    $"对象池模块: {GetPoolStateText(pool)}",
                    $"配置资产: {infos.Count}",
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
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
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
            var infos = ESEditorSO.GetGroupOfType<PrefabPrewarmDataInfo>() ?? new List<PrefabPrewarmDataInfo>(0);
            var result = new List<PrefabPrewarmDataInfo>(infos.Count);
            foreach (var info in infos)
            {
                if (info == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(info);
                if (!string.IsNullOrWhiteSpace(prewarmSearchText))
                {
                    string keyword = prewarmSearchText.Trim();
                    if (!ContainsIgnoreCase(info.name, keyword) &&
                        !ContainsIgnoreCase(info.KeyName, keyword) &&
                        !ContainsIgnoreCase(path, keyword))
                        continue;
                }

                result.Add(info);
            }

            return result
                .Distinct()
                .OrderBy(info => AssetDatabase.GetAssetPath(info), StringComparer.OrdinalIgnoreCase)
                .ToList();
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
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
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
