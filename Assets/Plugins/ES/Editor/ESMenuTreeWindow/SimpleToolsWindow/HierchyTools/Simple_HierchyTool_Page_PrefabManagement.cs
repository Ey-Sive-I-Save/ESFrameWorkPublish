using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.Callbacks;

namespace ES
{
    #region Prefab信息类
    /// <summary>
    /// Prefab实例信息数据结构
    /// </summary>
    [Serializable]
    public class PrefabInstanceInfo
    {
        [ReadOnly, LabelText("实例对象")]
        public GameObject instance;

        [ReadOnly, LabelText("资产路径")]
        public string prefabPath;

        [ReadOnly, LabelText("已修改")]
        public bool hasModifications;

        [ReadOnly, LabelText("资产丢失")]
        public bool isMissing;

        [ReadOnly, LabelText("变体类型")]
        public bool isVariant;

        [Button("🎯 定位实例", ButtonSizes.Small), HorizontalGroup("Actions")]
        [Tooltip("在Hierarchy中选中并高亮显示此Prefab实例")]
        public void SelectInstance()
        {
            if (instance != null)
            {
                Selection.activeGameObject = instance;
                EditorGUIUtility.PingObject(instance);
            }
        }

        [Button("📁 定位资产", ButtonSizes.Small), HorizontalGroup("Actions")]
        [Tooltip("在Project窗口中定位并高亮显示对应的Prefab资产文件")]
        public void PingAsset()
        {
            if (!string.IsNullOrEmpty(prefabPath) && !isMissing)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            }
        }
    }
    #endregion

    #region Prefab管理工具
    /// <summary>
    /// 商业级Prefab实例批量管理工具
    /// 提供全面的Prefab实例管理、检测、优化功能
    /// 支持批量应用、还原、断开、替换等操作
    /// 所有危险操作均带有确认对话框和Undo支持
    /// </summary>
    [Serializable]
    public class Page_PrefabManagement : ESWindowPageBase
    {
        #region UI配置
        [Title("Prefab实例管理工具", "商业级Prefab实例批量管理解决方案", bold: true, titleAlignment: TitleAlignments.Centered)]

        private string PanelSummary
        {
            get
            {
                int selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
                int modifiedCount = detectedPrefabs.Count(info => info != null && info.hasModifications);
                int missingCount = detectedPrefabs.Count(info => info != null && info.isMissing);
                return $"当前选择: {selectedCount} 个对象 | 已分析 Prefab: {detectedPrefabs.Count} 个 | 已修改: {modifiedCount} | 丢失引用: {missingCount} | 包含子对象: {(includeChildren ? "是" : "否")}";
            }
        }

        [HideInInspector]
        [DisplayAsString(fontSize: 12), HideLabel, GUIColor(0.8f, 0.9f, 1f)]
        public string featureOverview =
            "🔧 批量应用/还原Prefab实例修改到原始资产\n" +
            "🔗 断开Prefab实例连接或替换为其他Prefab\n" +
            "🔍 检测丢失/修改的Prefab实例\n" +
            "🎯 查找和选择场景中的相同类型Prefab实例\n" +
            "🏷️ Prefab变体检测和管理";

        [HideInInspector]
        [DisplayAsString(fontSize: 12), HideLabel, GUIColor(0.9f, 0.9f, 0.8f)]
        public string operationFlow =
            "1️⃣ 在Hierarchy中选择目标对象\n" +
            "2️⃣ 点击'分析选中对象'查看详情\n" +
            "3️⃣ 根据需要执行批量操作\n" +
            "4️⃣ 修改 Prefab 资产或 Override 前请先确认影响范围";

        [HideInInspector]
        [DisplayAsString(fontSize: 12), HideLabel, GUIColor(0.9f, 0.8f, 0.9f)]
        public string usageTips =
            "💡 勾选'包含子对象'可处理嵌套Prefab\n" +
            "💡 操作前建议先分析以了解影响范围\n" +
            "💡 批量操作会显示操作对象数量\n" +
            "💡 所有危险操作都有确认对话框\n" +
            "💡 场景对象操作通常可撤销，Prefab资产写入不承诺完整Undo";

        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private bool foldoutPrefabSettings = true;
        private string prefabSearch = "";
        private int prefabStatusFilterIndex = 0;
        private int prefabSortIndex = 1;
        private int prefabPageIndex = 0;
        private const int prefabPageSize = 12;
        private static readonly string[] PrefabStatusFilterLabels = { "全部", "已修改", "丢失", "变体", "正常" };
        private static readonly string[] PrefabSortLabels = { "对象", "风险", "资产", "路径" };

        [OnInspectorGUI, PropertyOrder(-200)]
        private void DrawResultPanel()
        {
            DrawPrefabWorkbench();
        }

        private void DrawPrefabWorkbench()
        {
            DrawPrefabHeader();
            DrawPrefabContext();
            DrawPrefabQuickSettings();
            DrawPrefabActions();
            DrawPrefabResultPanel();
            DrawPrefabFilters();
            DrawPrefabTable();
        }

        private void DrawPrefabHeader()
        {
            int selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
            int modifiedCount = detectedPrefabs.Count(info => info != null && info.hasModifications);
            int missingCount = detectedPrefabs.Count(info => info != null && info.isMissing);
            int variantCount = detectedPrefabs.Count(info => info != null && info.isVariant);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Prefab 实例审计台", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("用于分析场景或 Prefab Mode 中的实例状态，并安全执行应用、还原、断开、替换等高风险操作。", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetric("当前选择", selectedCount.ToString());
                    DrawMetric("已分析", detectedPrefabs.Count.ToString());
                    DrawMetric("已修改", modifiedCount.ToString());
                    DrawMetric("丢失引用", missingCount.ToString());
                    DrawMetric("变体", variantCount.ToString());
                }
            }
        }

        private void DrawPrefabContext()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("当前上下文", "操作对象来自当前选择；“分析当前上下文”会覆盖当前场景或 Prefab Mode 根对象。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawInfoRow("编辑环境", GetContextLabel());
                DrawInfoRow("选中对象", BuildSelectionSummary());
                DrawInfoRow("替换目标", targetPrefab == null ? "未设置。替换操作前需要指定目标 Prefab 资产。" : $"{targetPrefab.name}  |  {AssetDatabase.GetAssetPath(targetPrefab)}");
                DrawInfoRow("目标策略", includeChildren ? "包含子对象，会折叠为最近 Prefab 实例根，避免重复操作嵌套子节点。" : "只处理当前选中对象。");
            }
        }

        private void DrawPrefabQuickSettings()
        {
            foldoutPrefabSettings = EditorGUILayout.Foldout(foldoutPrefabSettings, "常用设置", true);
            if (!foldoutPrefabSettings)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                includeChildren = GUILayout.Toggle(includeChildren, "包含子对象和嵌套实例", EditorStyles.miniButton, GUILayout.Height(24));
                targetPrefab = (GameObject)EditorGUILayout.ObjectField("替换目标 Prefab", targetPrefab, typeof(GameObject), false);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("使用当前选中资产作为替换目标", EditorStyles.miniButtonLeft, GUILayout.Height(24)))
                        UseSelectionAsTargetPrefab();
                    if (GUILayout.Button("清空替换目标", EditorStyles.miniButtonRight, GUILayout.Height(24)))
                        targetPrefab = null;
                }
            }
        }

        private void DrawPrefabActions()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("执行操作", "先分析，再筛选复核，最后执行高风险操作。应用会写 Prefab 资产；还原/断开/替换会改场景对象。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("分析选中对象", SimpleToolsActionTone.Primary, 32))
                        AnalyzeSelection();
                    if (SimpleToolsPanelUtility.DrawActionButton("分析当前上下文", SimpleToolsActionTone.Primary, 32))
                        AnalyzeCurrentContext();
                    if (SimpleToolsPanelUtility.DrawActionButton("检测丢失引用", SimpleToolsActionTone.Warning, 32))
                        FindMissingPrefabs();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("选择同源 Prefab", SimpleToolsActionTone.Success, 28))
                        SelectSamePrefabs();
                    GUI.enabled = detectedPrefabs.Count > 0;
                    if (SimpleToolsPanelUtility.DrawActionButton("选中筛选结果", SimpleToolsActionTone.Neutral, 28))
                        SelectFilteredResults();
                    if (SimpleToolsPanelUtility.DrawActionButton("导出审计报告", SimpleToolsActionTone.Neutral, 28))
                        ExportPrefabReport();
                    GUI.enabled = true;
                }

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = Selection.gameObjects != null && Selection.gameObjects.Length > 0;
                    if (SimpleToolsPanelUtility.DrawActionButton("应用修改到资产", SimpleToolsActionTone.Success, 28))
                        ApplyAllPrefabs();
                    if (SimpleToolsPanelUtility.DrawActionButton("还原实例修改", SimpleToolsActionTone.Warning, 28))
                        RevertAllPrefabs();
                    if (SimpleToolsPanelUtility.DrawActionButton("断开 Prefab 连接", SimpleToolsActionTone.Danger, 28))
                        UnpackPrefabs();
                    GUI.enabled = targetPrefab != null;
                    if (SimpleToolsPanelUtility.DrawActionButton("替换为目标 Prefab", SimpleToolsActionTone.Warning, 28))
                        ReplacePrefabs();
                    GUI.enabled = true;
                }
            }
        }

        private void DrawPrefabResultPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("审计结果", "这里显示最近一次分析或操作的结果，表格只负责复核具体对象。");
            if (detectedPrefabs.Count == 0 && string.IsNullOrWhiteSpace(lastResultSummary))
            {
                SimpleToolsPanelUtility.DrawEmptyState("还没有分析结果。先选择 Hierarchy 对象后点“分析选中对象”，或直接点“分析当前上下文”扫描当前场景/Prefab Mode。");
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!string.IsNullOrWhiteSpace(lastResultSummary))
                    EditorGUILayout.LabelField(lastResultSummary, EditorStyles.boldLabel);

                if (!string.IsNullOrWhiteSpace(lastResultDetail))
                    EditorGUILayout.TextArea(lastResultDetail, GUILayout.MinHeight(38), GUILayout.MaxHeight(96));

                DrawInfoRow("风险摘要", BuildPrefabRiskSummary(GetFilteredPrefabInfos(false)));
                DrawInfoRow("资产分布", BuildPrefabAssetSummary(GetFilteredPrefabInfos(false), 5));
            }
        }

        private void DrawPrefabFilters()
        {
            if (detectedPrefabs.Count == 0)
                return;

            SimpleToolsPanelUtility.DrawSectionTitle("结果筛选", "搜索会匹配对象路径、Prefab 资产路径和资产名。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", EditorStyles.miniBoldLabel, GUILayout.Width(42));
                    prefabSearch = EditorGUILayout.TextField(prefabSearch);
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                        prefabSearch = string.Empty;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("状态", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                    prefabStatusFilterIndex = GUILayout.Toolbar(prefabStatusFilterIndex, PrefabStatusFilterLabels, EditorStyles.miniButton, GUILayout.Height(22));
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField("排序", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                    prefabSortIndex = GUILayout.Toolbar(prefabSortIndex, PrefabSortLabels, EditorStyles.miniButton, GUILayout.Width(198), GUILayout.Height(22));
                }
            }
        }

        private void DrawPrefabTable()
        {
            if (detectedPrefabs.Count == 0)
                return;

            var rows = GetFilteredPrefabInfos(true);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Prefab 实例列表  ({rows.Count}/{detectedPrefabs.Count})", EditorStyles.boldLabel);
                if (rows.Count == 0)
                {
                    EditorGUILayout.LabelField("当前筛选条件下没有结果。", EditorStyles.wordWrappedMiniLabel);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("对象", EditorStyles.miniBoldLabel, GUILayout.MinWidth(180));
                    EditorGUILayout.LabelField("状态", EditorStyles.miniBoldLabel, GUILayout.Width(104));
                    EditorGUILayout.LabelField("Prefab资产", EditorStyles.miniBoldLabel, GUILayout.MinWidth(180));
                    GUILayout.Space(100);
                }

                int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)rows.Count / prefabPageSize));
                prefabPageIndex = Mathf.Clamp(prefabPageIndex, 0, totalPages - 1);
                int start = prefabPageIndex * prefabPageSize;
                int end = Mathf.Min(start + prefabPageSize, rows.Count);

                for (int i = start; i < end; i++)
                    DrawPrefabRow(rows[i]);

                if (totalPages > 1)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("上一页", EditorStyles.miniButtonLeft, GUILayout.Width(64)) && prefabPageIndex > 0)
                            prefabPageIndex--;
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField($"第 {prefabPageIndex + 1} / {totalPages} 页", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(90));
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("下一页", EditorStyles.miniButtonRight, GUILayout.Width(64)) && prefabPageIndex < totalPages - 1)
                            prefabPageIndex++;
                    }
                }
            }
        }

        private void DrawPrefabRow(PrefabInstanceInfo info)
        {
            if (info == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(GetObjectPath(info.instance), EditorStyles.miniLabel, GUILayout.MinWidth(180));
                EditorGUILayout.LabelField(GetPrefabStatusText(info), EditorStyles.miniLabel, GUILayout.Width(104));
                EditorGUILayout.LabelField(info.prefabPath, EditorStyles.miniLabel, GUILayout.MinWidth(180));
                if (GUILayout.Button("实例", EditorStyles.miniButtonLeft, GUILayout.Width(48)))
                    info.SelectInstance();
                GUI.enabled = !info.isMissing;
                if (GUILayout.Button("资产", EditorStyles.miniButtonMid, GUILayout.Width(48)))
                    info.PingAsset();
                GUI.enabled = true;
                if (GUILayout.Button("复制", EditorStyles.miniButtonRight, GUILayout.Width(48)))
                    EditorGUIUtility.systemCopyBuffer = $"{GetObjectPath(info.instance)} | {GetPrefabStatusText(info)} | {info.prefabPath}";
            }
        }

        private void DrawMetric(string label, string value)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(72)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(value) ? "-" : value, EditorStyles.boldLabel);
            }
        }

        private void DrawInfoRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(78));
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(value) ? "-" : value, EditorStyles.wordWrappedMiniLabel);
            }
        }
        #endregion

        #region 配置参数
        [HideInInspector]
        [LabelText("包含子对象")]
        [Tooltip("启用后，分析和操作将包含选中对象的所有子级对象，包括嵌套的Prefab实例")]
        [InfoBox("勾选后将处理选中对象的所有子级Prefab实例", InfoMessageType.Info)]
        public bool includeChildren = true;

        [HideInInspector]
        [LabelText("替换目标Prefab"), AssetsOnly]
        [Tooltip("选择用于'替换为目标Prefab实例'操作的Prefab资产。替换时会保留原对象的Transform信息")]
        [InfoBox("设置用于'替换为目标Prefab实例'操作的Prefab资产", InfoMessageType.Info)]
        public GameObject targetPrefab;
        #endregion

        #region 统计信息
        [HideInInspector]
        [DisplayAsString(fontSize: 12), HideLabel, GUIColor(0.7f, 1f, 0.7f)]
        [Tooltip("显示当前选中对象的分析结果，包括Prefab实例数量、修改状态等统计信息")]
        public string currentStats = "📌 请先在Hierarchy中选择对象，然后点击'分析选中对象'...";

        [HideInInspector]
        [ListDrawerSettings(IsReadOnly = true, DraggableItems = false, HideAddButton = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
        [LabelText("Prefab实例列表")]
        [Tooltip("列出所有检测到的Prefab实例，包含详细信息和快速操作按钮")]
        public List<PrefabInstanceInfo> detectedPrefabs = new List<PrefabInstanceInfo>();
        #endregion

        #region 分析功能
        /// <summary>
        /// 刷新并分析当前选择的Prefab实例，生成详细统计信息
        /// </summary>
        [Tooltip("分析当前选中的对象，统计Prefab实例数量、修改状态、变体类型等详细信息。结果会显示在下方统计面板中。")]
        public void AnalyzeSelection()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                currentStats = "❌ 未选择任何对象，请在Hierarchy中选择GameObject";
                lastResultSummary = "分析取消: 未选择对象";
                lastResultDetail = "请先在 Hierarchy 中选择一个或多个 GameObject。";
                return;
            }

            AnalyzeObjects(selectedObjects, false, "当前选择");
            Debug.Log($"[Prefab管理] {lastResultSummary}");
        }

        public void AnalyzeCurrentContext()
        {
            detectedPrefabs.Clear();

            var roots = GetCurrentContextRoots();
            if (roots.Count == 0)
            {
                currentStats = "未找到可分析的场景或 Prefab Mode 根对象。";
                lastResultSummary = "分析取消: 没有可分析对象";
                lastResultDetail = currentStats;
                return;
            }

            AnalyzeObjects(roots, true, "当前上下文");
        }

        private void AnalyzeObjects(IEnumerable<GameObject> roots, bool forceIncludeChildren, string label)
        {
            detectedPrefabs.Clear();

            var inputRoots = roots?.Where(obj => obj != null).Distinct().ToList() ?? new List<GameObject>();
            var allObjects = forceIncludeChildren
                ? inputRoots.SelectMany(root => root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject)).Distinct().ToList()
                : SimpleToolsSafetyUtility.CollectTargets(inputRoots.ToArray(), includeChildren).Distinct().ToList();

            int prefabCount = 0;
            int modifiedCount = 0;
            int missingCount = 0;
            int variantCount = 0;

            foreach (var obj in allObjects)
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(obj))
                    continue;

                prefabCount++;
                var info = new PrefabInstanceInfo
                {
                    instance = obj,
                    hasModifications = PrefabUtility.HasPrefabInstanceAnyOverrides(obj, false)
                };

                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(obj);
                if (prefabAsset != null)
                {
                    info.prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
                    info.isVariant = PrefabUtility.IsPartOfVariantPrefab(prefabAsset);
                    if (info.isVariant) variantCount++;
                }
                else
                {
                    info.isMissing = true;
                    info.prefabPath = "Prefab资产丢失";
                    missingCount++;
                }

                if (info.hasModifications) modifiedCount++;
                detectedPrefabs.Add(info);
            }

            detectedPrefabs = detectedPrefabs
                .OrderBy(info => GetObjectPath(info.instance), StringComparer.Ordinal)
                .ToList();

            currentStats = $"分析范围: {label}\n总对象数: {allObjects.Count}\nPrefab实例: {prefabCount}\n已修改实例: {modifiedCount}\n变体实例: {variantCount}\n丢失引用: {missingCount}";
            lastResultSummary = $"分析完成: {label} | 扫描 {allObjects.Count} 个对象 | Prefab {prefabCount} | 修改 {modifiedCount} | 丢失 {missingCount}";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(detectedPrefabs.Select(info => $"{GetObjectPath(info.instance)} | {GetPrefabStatusText(info)} | {info.prefabPath}"), 14);
            prefabPageIndex = 0;
        }
        #endregion

        #region 分析和检测功能

        /// <summary>
        /// 在当前场景中查找所有丢失Prefab引用的对象
        /// </summary>
        [Tooltip("扫描整个场景，查找所有Prefab引用丢失的对象。找到的对象会被自动选中，方便批量处理。")]
        public void FindMissingPrefabs()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            var missingList = new List<GameObject>();

            // 遍历场景中的所有对象
            foreach (var root in rootObjects)
            {
                var allTransforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                    {
                        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                        if (prefabAsset == null)
                        {
                            missingList.Add(t.gameObject);
                        }
                    }
                }
            }

            if (missingList.Count > 0)
            {
                Selection.objects = missingList.ToArray();
                lastResultSummary = $"丢失 Prefab 检测完成: 发现 {missingList.Count} 个";
                lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(missingList.Select(GetObjectPath), 12);
                EditorUtility.DisplayDialog("检测完成",
                    $"⚠️ 发现 {missingList.Count} 个丢失Prefab引用的对象！\n\n已自动选中这些对象，请检查并修复。\n建议删除或重新连接这些对象。",
                    "确定");
                Debug.LogWarning($"[Prefab管理] 发现 {missingList.Count} 个丢失Prefab引用的对象");
            }
            else
            {
                lastResultSummary = "丢失 Prefab 检测完成: 未发现丢失引用";
                lastResultDetail = "当前活动场景没有扫描到丢失 Prefab 引用。";
                EditorUtility.DisplayDialog("检测完成", "✅ 场景中没有丢失的Prefab引用！\n\n所有Prefab实例都正常连接。", "确定");
            }
        }

        /// <summary>
        /// 选择场景中所有与当前选中对象相同类型的Prefab实例
        /// </summary>
        [Tooltip("选择场景中所有与当前选中Prefab实例相同类型的对象。适用于批量修改相同类型的Prefab。")]
        public void SelectSamePrefabs()
        {
            var selected = Selection.activeGameObject;
            if (selected == null || !PrefabUtility.IsPartOfPrefabInstance(selected))
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先选择一个Prefab实例！", "确定");
                return;
            }

            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(selected);
            if (prefabAsset == null)
            {
                EditorUtility.DisplayDialog("错误", "❌ 无法获取Prefab资产！\n该对象的Prefab引用可能已丢失。", "确定");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            var sameTypeList = new List<GameObject>();

            // 遍历场景查找相同类型的Prefab实例
            foreach (var root in rootObjects)
            {
                var allTransforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                    {
                        var asset = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                        if (asset == prefabAsset)
                        {
                            sameTypeList.Add(t.gameObject);
                        }
                    }
                }
            }

            if (sameTypeList.Count > 0)
            {
                Selection.objects = sameTypeList.ToArray();
                lastResultSummary = $"选择相同 Prefab 完成: 选中 {sameTypeList.Count} 个";
                lastResultDetail = $"Prefab路径: {AssetDatabase.GetAssetPath(prefabAsset)}\n\n对象:\n" + SimpleToolsSafetyUtility.JoinPreview(sameTypeList.Select(GetObjectPath), 12);
                EditorUtility.DisplayDialog("选择完成",
                    $"✅ 已选中 {sameTypeList.Count} 个相同的Prefab实例！\n\nPrefab路径: {AssetDatabase.GetAssetPath(prefabAsset)}",
                    "确定");
                Debug.Log($"[Prefab管理] 选择了 {sameTypeList.Count} 个相同的Prefab实例: {prefabAsset.name}");
            }
        }
        #endregion

        private List<GameObject> GetSelectedPrefabInstanceRoots()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
                return new List<GameObject>();

            return SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren)
                .Where(obj => obj != null && PrefabUtility.IsPartOfPrefabInstance(obj))
                .Select(PrefabUtility.GetNearestPrefabInstanceRoot)
                .Where(obj => obj != null)
                .Distinct()
                .OrderBy(GetObjectPath, StringComparer.Ordinal)
                .ToList();
        }

        private List<GameObject> GetSafeReplacementTargets()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
                return new List<GameObject>();

            var targets = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren)
                .Where(obj => obj != null)
                .Distinct()
                .ToList();
            var set = new HashSet<GameObject>(targets);

            return targets
                .Where(obj => !HasAncestorInSet(obj, set))
                .OrderBy(GetObjectPath, StringComparer.Ordinal)
                .ToList();
        }

        private List<GameObject> GetCurrentContextRoots()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null)
                return new List<GameObject> { prefabStage.prefabContentsRoot };

            var scene = SceneManager.GetActiveScene();
            return scene.IsValid() ? scene.GetRootGameObjects().ToList() : new List<GameObject>();
        }

        private string GetContextLabel()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
                return $"Prefab Mode: {prefabStage.assetPath}";

            var scene = SceneManager.GetActiveScene();
            return scene.IsValid() ? $"场景: {scene.name}" : "未知上下文";
        }

        private string BuildSelectionSummary()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return "未选择 Hierarchy 对象。";

            return $"{selected.Length} 个对象  |  " + SimpleToolsSafetyUtility.JoinPreview(selected.Select(GetObjectPath), 4).Replace("\n", "  |  ");
        }

        private void UseSelectionAsTargetPrefab()
        {
            var selectedObject = Selection.activeObject;
            if (selectedObject is GameObject go)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(go))
                {
                    targetPrefab = go;
                    lastResultSummary = "已设置替换目标 Prefab";
                    lastResultDetail = AssetDatabase.GetAssetPath(targetPrefab);
                    return;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    var asset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (asset is GameObject prefabAsset)
                    {
                        targetPrefab = prefabAsset;
                        lastResultSummary = "已从实例设置替换目标 Prefab";
                        lastResultDetail = AssetDatabase.GetAssetPath(targetPrefab);
                        return;
                    }
                }
            }

            lastResultSummary = "设置替换目标失败";
            lastResultDetail = "请选择 Project 中的 Prefab 资产，或选择一个 Prefab 实例。";
        }

        private List<PrefabInstanceInfo> GetFilteredPrefabInfos(bool sorted)
        {
            IEnumerable<PrefabInstanceInfo> query = detectedPrefabs.Where(PassesPrefabFilter);
            if (sorted)
                query = SortPrefabInfos(query);
            return query.ToList();
        }

        private bool PassesPrefabFilter(PrefabInstanceInfo info)
        {
            if (info == null)
                return false;

            switch (prefabStatusFilterIndex)
            {
                case 1:
                    if (!info.hasModifications) return false;
                    break;
                case 2:
                    if (!info.isMissing) return false;
                    break;
                case 3:
                    if (!info.isVariant) return false;
                    break;
                case 4:
                    if (info.hasModifications || info.isMissing) return false;
                    break;
            }

            if (string.IsNullOrWhiteSpace(prefabSearch))
                return true;

            string keyword = prefabSearch.Trim();
            return ContainsIgnoreCase(GetObjectPath(info.instance), keyword) ||
                   ContainsIgnoreCase(info.prefabPath, keyword) ||
                   ContainsIgnoreCase(Path.GetFileNameWithoutExtension(info.prefabPath), keyword) ||
                   ContainsIgnoreCase(GetPrefabStatusText(info), keyword);
        }

        private IEnumerable<PrefabInstanceInfo> SortPrefabInfos(IEnumerable<PrefabInstanceInfo> infos)
        {
            switch (prefabSortIndex)
            {
                case 1:
                    return infos.OrderByDescending(info => GetPrefabRiskScore(info)).ThenBy(info => GetObjectPath(info.instance), StringComparer.Ordinal);
                case 2:
                    return infos.OrderBy(info => info.prefabPath, StringComparer.Ordinal).ThenBy(info => GetObjectPath(info.instance), StringComparer.Ordinal);
                case 3:
                    return infos.OrderBy(info => GetObjectPath(info.instance), StringComparer.Ordinal);
                default:
                    return infos.OrderBy(info => GetObjectPath(info.instance), StringComparer.Ordinal);
            }
        }

        private int GetPrefabRiskScore(PrefabInstanceInfo info)
        {
            if (info == null)
                return 0;

            int score = 0;
            if (info.isMissing) score += 100;
            if (info.hasModifications) score += 30;
            if (info.isVariant) score += 10;
            return score;
        }

        private string GetPrefabStatusText(PrefabInstanceInfo info)
        {
            if (info == null)
                return "未知";

            var states = new List<string>();
            if (info.isMissing) states.Add("丢失");
            if (info.hasModifications) states.Add("修改");
            if (info.isVariant) states.Add("变体");
            return states.Count == 0 ? "正常" : string.Join("/", states);
        }

        private string BuildPrefabRiskSummary(List<PrefabInstanceInfo> infos)
        {
            if (infos == null || infos.Count == 0)
                return "无命中结果。";

            return $"共 {infos.Count} 个 | 修改 {infos.Count(i => i.hasModifications)} | 丢失 {infos.Count(i => i.isMissing)} | 变体 {infos.Count(i => i.isVariant)} | 正常 {infos.Count(i => !i.hasModifications && !i.isMissing)}";
        }

        private string BuildPrefabAssetSummary(List<PrefabInstanceInfo> infos, int limit)
        {
            if (infos == null || infos.Count == 0)
                return "无";

            return string.Join("  |  ", infos
                .Where(info => !string.IsNullOrWhiteSpace(info.prefabPath))
                .GroupBy(info => info.prefabPath)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(limit)
                .Select(g => $"{Path.GetFileNameWithoutExtension(g.Key)} {g.Count()}"));
        }

        private bool ContainsIgnoreCase(string source, string keyword)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SelectFilteredResults()
        {
            var objects = GetFilteredPrefabInfos(true)
                .Select(info => info.instance)
                .Where(obj => obj != null)
                .Cast<UnityEngine.Object>()
                .ToArray();

            if (objects.Length == 0)
            {
                lastResultSummary = "选中筛选结果失败";
                lastResultDetail = "当前筛选条件下没有可选中的实例对象。";
                return;
            }

            Selection.objects = objects;
            EditorGUIUtility.PingObject(objects[0]);
            lastResultSummary = $"已选中筛选结果: {objects.Length} 个";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(objects.OfType<GameObject>().Select(GetObjectPath), 12);
        }

        private void ExportPrefabReport()
        {
            if (detectedPrefabs.Count == 0)
            {
                lastResultSummary = "导出取消: 没有分析结果";
                lastResultDetail = "请先执行一次 Prefab 分析。";
                return;
            }

            string reportPath = EditorUtility.SaveFilePanel("导出 Prefab 审计报告", Application.dataPath, "PrefabAuditReport.txt", "txt");
            if (string.IsNullOrEmpty(reportPath))
                return;

            try
            {
                using (var writer = new StreamWriter(reportPath, false, System.Text.Encoding.UTF8))
                {
                    var rows = GetFilteredPrefabInfos(true);
                    writer.WriteLine("=== ES Prefab 实例审计报告 ===");
                    writer.WriteLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"上下文: {GetContextLabel()}");
                    writer.WriteLine($"包含子对象: {(includeChildren ? "是" : "否")}");
                    writer.WriteLine($"筛选结果: {rows.Count} / {detectedPrefabs.Count}");
                    writer.WriteLine();
                    writer.WriteLine("=== 风险总览 ===");
                    writer.WriteLine(BuildPrefabRiskSummary(rows));
                    writer.WriteLine($"资产分布: {BuildPrefabAssetSummary(rows, 20)}");
                    writer.WriteLine();
                    writer.WriteLine("状态\t对象路径\tPrefab资产路径");

                    foreach (var info in rows)
                        writer.WriteLine($"{GetPrefabStatusText(info)}\t{GetObjectPath(info.instance)}\t{info.prefabPath}");
                }

                EditorUtility.RevealInFinder(reportPath);
                lastResultSummary = "Prefab 审计报告已导出";
                lastResultDetail = reportPath;
            }
            catch (Exception ex)
            {
                lastResultSummary = "导出 Prefab 报告失败";
                lastResultDetail = ex.Message;
                EditorUtility.DisplayDialog("错误", $"导出报告失败：{ex.Message}", "确定");
            }
        }

        private bool HasAncestorInSet(GameObject obj, HashSet<GameObject> set)
        {
            if (obj == null)
                return false;

            var current = obj.transform.parent;
            while (current != null)
            {
                if (set.Contains(current.gameObject))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private string GetObjectPath(GameObject obj)
        {
            if (obj == null)
                return "<丢失对象>";

            var names = new Stack<string>();
            var current = obj.transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }

        #region 基础Prefab操作
        /// <summary>
        /// 批量应用所有选中Prefab实例的更改到资产文件
        /// </summary>
        [Tooltip("将选中Prefab实例的所有修改应用到原始Prefab资产文件。这会影响项目中所有使用该Prefab的地方。")]
        public void ApplyAllPrefabs()
        {
            var prefabTargets = GetSelectedPrefabInstanceRoots();
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先选择GameObject！", "确定");
                return;
            }

            int prefabCount = prefabTargets.Count;
            if (prefabCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "ℹ️ 选中的对象中没有Prefab实例！", "确定");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(prefabTargets.Select(GetObjectPath), 10);
            // 确认操作
            if (!EditorUtility.DisplayDialog("确认应用Prefab实例修改",
                $"⚠️ 确定要应用 {prefabCount} 个Prefab实例的所有更改吗？\n\n" +
                $"实际目标：\n{preview}\n\n" +
                $"此操作将:\n" +
                $"• 覆盖Prefab资产文件\n" +
                $"• 影响所有引用该Prefab的场景\n" +
                $"• 会写入Prefab资产，影响所有引用该Prefab的实例\n\n" +
                $"建议操作前备份重要资产！",
                "应用", "取消"))
            {
                return;
            }

            // 执行应用操作
            int appliedCount = 0;
            var failedMessages = new List<string>();
            foreach (var obj in prefabTargets)
            {
                try
                {
                    PrefabUtility.ApplyPrefabInstance(obj, InteractionMode.UserAction);
                    appliedCount++;
                }
                catch (Exception e)
                {
                    failedMessages.Add($"{GetObjectPath(obj)}: {e.Message}");
                    Debug.LogError($"[Prefab管理] 应用失败: {obj.name} - {e.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            lastResultSummary = $"应用 Prefab 修改完成: 成功 {appliedCount} / {prefabCount} | 失败 {failedMessages.Count}";
            lastResultDetail = BuildResultDetail(prefabTargets, failedMessages);
            EditorUtility.DisplayDialog("操作完成",
                $"成功应用 {appliedCount} / {prefabCount} 个Prefab实例的更改。{BuildFailureDetail(failedMessages)}",
                "确定");
            Debug.Log($"[Prefab管理] 应用完成 - 成功: {appliedCount} / 总数: {prefabCount}");
        }

        /// <summary>
        /// 批量还原所有选中Prefab实例的更改，恢复到资产原始状态
        /// </summary>
        [Tooltip("将选中Prefab实例的所有修改还原到原始Prefab资产的状态。所有未应用的更改将会丢失。")]
        public void RevertAllPrefabs()
        {
            var prefabTargets = GetSelectedPrefabInstanceRoots();
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先选择GameObject！", "确定");
                return;
            }

            int prefabCount = prefabTargets.Count;
            if (prefabCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "ℹ️ 选中的对象中没有Prefab实例！", "确定");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(prefabTargets.Select(GetObjectPath), 10);
            // 确认操作
            if (!EditorUtility.DisplayDialog("确认还原Prefab实例修改",
                $"⚠️ 确定要还原 {prefabCount} 个Prefab实例的所有更改吗？\n\n" +
                $"实际目标：\n{preview}\n\n" +
                $"此操作将:\n" +
                $"• 丢失所有未应用的修改\n" +
                $"• 恢复到Prefab资产原始状态\n" +
                $"• 会丢弃当前实例未应用的Override，请确认后再继续\n\n" +
                $"请确认是否继续！",
                "还原", "取消"))
            {
                return;
            }

            // 执行还原操作
            int revertedCount = 0;
            var failedMessages = new List<string>();
            foreach (var obj in prefabTargets)
            {
                try
                {
                    PrefabUtility.RevertPrefabInstance(obj, InteractionMode.UserAction);
                    revertedCount++;
                }
                catch (Exception e)
                {
                    failedMessages.Add($"{GetObjectPath(obj)}: {e.Message}");
                    Debug.LogError($"[Prefab管理] 还原失败: {obj.name} - {e.Message}");
                }
            }

            MarkScenesDirty(prefabTargets);
            lastResultSummary = $"还原 Prefab 修改完成: 成功 {revertedCount} / {prefabCount} | 失败 {failedMessages.Count}";
            lastResultDetail = BuildResultDetail(prefabTargets, failedMessages);
            EditorUtility.DisplayDialog("操作完成",
                $"成功还原 {revertedCount} / {prefabCount} 个Prefab实例的更改。{BuildFailureDetail(failedMessages)}",
                "确定");
            Debug.Log($"[Prefab管理] 还原完成 - 成功: {revertedCount} / 总数: {prefabCount}");
        }

        /// <summary>
        /// 批量断开Prefab连接，将Prefab实例转换为普通GameObject
        /// </summary>
        [Tooltip("将选中Prefab实例转换为普通GameObject，断开与Prefab资产的连接。转换后无法再接收Prefab更新。")]
        public void UnpackPrefabs()
        {
            var prefabTargets = GetSelectedPrefabInstanceRoots();
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先选择GameObject！", "确定");
                return;
            }

            int prefabCount = prefabTargets.Count;
            if (prefabCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "ℹ️ 选中的对象中没有Prefab实例！", "确定");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(prefabTargets.Select(GetObjectPath), 10);
            // 确认操作
            if (!EditorUtility.DisplayDialog("确认断开Prefab实例连接",
                $"⚠️ 确定要断开 {prefabCount} 个Prefab实例的连接吗？\n\n" +
                $"实际目标：\n{preview}\n\n" +
                $"此操作将:\n" +
                $"• 对象转换为普通GameObject\n" +
                $"• 失去与Prefab资产的关联\n" +
                $"• 无法接收Prefab资产更新\n" +
                $"• 场景对象断开操作通常可通过Undo撤回\n\n" +
                $"请谨慎操作！",
                "断开", "取消"))
            {
                return;
            }

            // 执行断开操作
            int unpackedCount = 0;
            var failedMessages = new List<string>();
            foreach (var obj in prefabTargets)
            {
                try
                {
                    PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.UserAction);
                    unpackedCount++;
                }
                catch (Exception e)
                {
                    failedMessages.Add($"{GetObjectPath(obj)}: {e.Message}");
                    Debug.LogError($"[Prefab管理] 断开失败: {obj.name} - {e.Message}");
                }
            }

            MarkScenesDirty(prefabTargets);
            lastResultSummary = $"断开 Prefab 连接完成: 成功 {unpackedCount} / {prefabCount} | 失败 {failedMessages.Count}";
            lastResultDetail = BuildResultDetail(prefabTargets, failedMessages);
            EditorUtility.DisplayDialog("操作完成",
                $"成功断开 {unpackedCount} / {prefabCount} 个Prefab连接。{BuildFailureDetail(failedMessages)}",
                "确定");
            Debug.Log($"[Prefab管理] 断开完成 - 成功: {unpackedCount} / 总数: {prefabCount}");
        }

        /// <summary>
        /// 批量替换选中对象为指定的目标Prefab
        /// </summary>
        [Tooltip("将选中对象替换为指定的目标Prefab。会保留原对象的Transform信息、名称和层级关系。")]
        public void ReplacePrefabs()
        {
            if (targetPrefab == null)
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先设置目标Prefab！\n\n在'基础设置'中选择要替换的目标Prefab资产。", "确定");
                return;
            }

            var replacementTargets = GetSafeReplacementTargets();
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "❌ 请先选择要替换的GameObject！", "确定");
                return;
            }

            if (replacementTargets.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有可替换的有效对象。", "确定");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(replacementTargets.Select(GetObjectPath), 10);
            // 确认操作
            if (!EditorUtility.DisplayDialog("确认替换为目标Prefab实例",
                $"⚠️ 确定要将 {replacementTargets.Count} 个对象替换为目标Prefab吗？\n\n" +
                $"目标Prefab: {targetPrefab.name}\n" +
                $"路径: {AssetDatabase.GetAssetPath(targetPrefab)}\n\n" +
                $"实际目标：\n{preview}\n\n" +
                $"此操作将:\n" +
                $"• 销毁原对象并创建新Prefab实例\n" +
                $"• 保留Transform信息(位置/旋转/缩放)\n" +
                $"• 保留对象名称和父级关系\n" +
                $"• 新对象创建和旧对象删除通常可通过Undo撤回",
                "替换", "取消"))
            {
                return;
            }

            // 执行替换操作
            int replacedCount = 0;
            var failedMessages = new List<string>();
            var affectedScenes = new HashSet<UnityEngine.SceneManagement.Scene>();
            foreach (var obj in replacementTargets)
            {
                try
                {
                    if (obj.scene.IsValid())
                        affectedScenes.Add(obj.scene);

                    var parent = obj.transform.parent;
                    var position = obj.transform.position;
                    var rotation = obj.transform.rotation;
                    var scale = obj.transform.localScale;
                    var name = obj.name;
                    var siblingIndex = obj.transform.GetSiblingIndex();
                    var tag = obj.tag;
                    var layer = obj.layer;
                    var activeSelf = obj.activeSelf;
                    var staticFlags = GameObjectUtility.GetStaticEditorFlags(obj);

                    // 实例化新Prefab
                    var newObj = PrefabUtility.InstantiatePrefab(targetPrefab) as GameObject;
                    if (newObj == null)
                    {
                        failedMessages.Add($"{GetObjectPath(obj)}: 无法实例化目标Prefab {targetPrefab.name}");
                        Debug.LogError($"[Prefab管理] 替换失败: 无法实例化目标Prefab {targetPrefab.name}");
                        continue;
                    }

                    if (parent == null && obj.scene.IsValid())
                        SceneManager.MoveGameObjectToScene(newObj, obj.scene);

                    newObj.transform.SetParent(parent);
                    newObj.transform.position = position;
                    newObj.transform.rotation = rotation;
                    newObj.transform.localScale = scale;
                    newObj.name = name;
                    newObj.tag = tag;
                    newObj.layer = layer;
                    newObj.SetActive(activeSelf);
                    GameObjectUtility.SetStaticEditorFlags(newObj, staticFlags);
                    newObj.transform.SetSiblingIndex(siblingIndex);

                    // 注册Undo并销毁原对象
                    Undo.RegisterCreatedObjectUndo(newObj, "Replace Prefab");
                    Undo.DestroyObjectImmediate(obj);
                    replacedCount++;
                }
                catch (Exception e)
                {
                    failedMessages.Add($"{GetObjectPath(obj)}: {e.Message}");
                    Debug.LogError($"[Prefab管理] 替换失败: {obj.name} - {e.Message}");
                }
            }

            foreach (var scene in affectedScenes)
                EditorSceneManager.MarkSceneDirty(scene);

            lastResultSummary = $"替换 Prefab 完成: 成功 {replacedCount} / {replacementTargets.Count} | 失败 {failedMessages.Count}";
            lastResultDetail = $"目标Prefab: {targetPrefab.name}\n路径: {AssetDatabase.GetAssetPath(targetPrefab)}\n\n" + BuildResultDetail(replacementTargets, failedMessages);
            EditorUtility.DisplayDialog("操作完成",
                $"成功替换 {replacedCount} / {replacementTargets.Count} 个对象为目标Prefab。\n\n新Prefab: {targetPrefab.name}{BuildFailureDetail(failedMessages)}",
                "确定");
            Debug.Log($"[Prefab管理] 替换完成 - 成功: {replacedCount} / 总数: {replacementTargets.Count}");
        }

        private string BuildFailureDetail(List<string> failedMessages)
        {
            if (failedMessages == null || failedMessages.Count == 0)
                return string.Empty;

            return "\n\n失败项：\n" + SimpleToolsSafetyUtility.JoinPreview(failedMessages, 8);
        }

        private string BuildResultDetail(IEnumerable<GameObject> targets, List<string> failedMessages)
        {
            var sections = new List<string>();
            if (targets != null)
                sections.Add("对象:\n" + SimpleToolsSafetyUtility.JoinPreview(targets.Select(GetObjectPath), 12));

            if (failedMessages != null && failedMessages.Count > 0)
                sections.Add("失败项:\n" + SimpleToolsSafetyUtility.JoinPreview(failedMessages, 8));

            return sections.Count == 0 ? "无详细项" : string.Join("\n\n", sections);
        }

        private void MarkScenesDirty(IEnumerable<GameObject> targets)
        {
            if (targets == null)
                return;

            foreach (var scene in targets
                .Where(obj => obj != null && obj.scene.IsValid())
                .Select(obj => obj.scene)
                .Distinct())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
        #endregion
      }
    #endregion
}

