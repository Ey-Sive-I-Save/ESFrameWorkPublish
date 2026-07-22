using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ES
{
    #region 材质批量替换工具
    [Serializable]
    public class Page_MaterialReplacement : ESWindowPageBase
    {
        private const int PreviewDialogLimit = 14;
        private const int ReportExportLimit = 200;

        [Title("材质批量替换工具", "先扫描预览，再按预览结果安全替换场景对象上的材质引用。", bold: true, titleAlignment: TitleAlignments.Centered)]
        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "适合批量修正 Renderer、ParticleSystemRenderer 和脚本序列化字段里的材质引用。执行前会显示命中项，执行后支持 Ctrl+Z 撤销。";

        public enum TargetScope
        {
            [LabelText("只处理当前选中")]
            SelectedOnly,

            [LabelText("选中对象和子物体")]
            SelectedWithChildren,

            [LabelText("当前场景全部对象")]
            ActiveScene
        }

        [Flags]
        public enum ComponentType
        {
            [LabelText("不处理")]
            None = 0,

            [LabelText("Renderer")]
            Renderer = 1 << 0,

            [LabelText("ParticleSystemRenderer")]
            ParticleSystemRenderer = 1 << 1,

            [LabelText("脚本序列化字段")]
            MonoBehaviour = 1 << 2,

            [LabelText("全部")]
            All = Renderer | ParticleSystemRenderer | MonoBehaviour
        }

        public enum ReplacementMode
        {
            [LabelText("指定材质")]
            ReplaceSpecific,

            [LabelText("全部非空材质")]
            ReplaceAll,

            [LabelText("名称包含")]
            MatchByName,

            [LabelText("正则匹配")]
            MatchByRegex,

            [LabelText("Shader 匹配")]
            MatchByShader
        }

        [HideInInspector]
        [LabelText("目标范围"), EnumToggleButtons]
        public TargetScope targetScope = TargetScope.SelectedWithChildren;

        [HideInInspector]
        [LabelText("处理组件"), EnumToggleButtons]
        public ComponentType componentTypes = ComponentType.Renderer | ComponentType.ParticleSystemRenderer;

        [HideInInspector]
        [LabelText("包含未激活对象")]
        public bool includeInactive = true;

        [HideInInspector]
        [LabelText("扫描脚本字段"), GUIColor(0.95f, 0.78f, 0.35f)]
        [ShowIf("@(this.componentTypes & ES.Page_MaterialReplacement.ComponentType.MonoBehaviour) != 0")]
        public bool scanSerializedScriptFields = false;

        [HideInInspector]
        [LabelText("跳过隐藏对象")]
        public bool skipHideFlagsObjects = true;

        [HideInInspector]
        [LabelText("替换模式"), EnumToggleButtons]
        public ReplacementMode replacementMode = ReplacementMode.ReplaceSpecific;

        [HideInInspector]
        [LabelText("源材质"), AssetsOnly]
        [ShowIf("replacementMode", ReplacementMode.ReplaceSpecific)]
        public Material sourceMaterial;

        [HideInInspector]
        [LabelText("名称关键字")]
        [ShowIf("replacementMode", ReplacementMode.MatchByName)]
        public string matchName = "";

        [HideInInspector]
        [LabelText("忽略大小写")]
        [ShowIf("@this.replacementMode == ES.Page_MaterialReplacement.ReplacementMode.MatchByName || this.replacementMode == ES.Page_MaterialReplacement.ReplacementMode.MatchByRegex")]
        public bool ignoreCase = true;

        [HideInInspector]
        [LabelText("正则表达式")]
        [ShowIf("replacementMode", ReplacementMode.MatchByRegex)]
        public string regexPattern = "";

        [HideInInspector]
        [LabelText("源 Shader")]
        [ShowIf("replacementMode", ReplacementMode.MatchByShader)]
        public Shader sourceShader;

        [HideInInspector]
        [LabelText("空材质也填充")]
        public bool setDefaultWhenNull = false;

        [HideInInspector]
        [LabelText("目标材质"), AssetsOnly, Required]
        public Material targetMaterial;

        [HideInInspector]
        [LabelText("允许替换成相同材质")]
        public bool allowSameMaterial = false;

        private string PanelSummary
        {
            get
            {
                int targetCount = CollectTargets().Count;
                int enabledPreview = replacementPreview.Count(item => item.Enabled);
                return $"目标对象: {targetCount} | 预览命中: {replacementPreview.Count} | 已勾选: {enabledPreview} | 模式: {GetModeName(replacementMode)} | 组件: {componentTypes}";
            }
        }

        public void RefreshReplacementPreview()
        {
            if (!TryValidateScanSettings(out var message))
            {
                replacementPreview.Clear();
                lastResultSummary = "预览失败";
                lastResultDetail = message;
                EditorUtility.DisplayDialog("设置不完整", message, "知道了");
                return;
            }

            replacementPreview = BuildReplacementPreview(out var scanWarnings);
            previewSettingsSignature = BuildSettingsSignature();
            lastResultSummary = $"预览完成: 命中 {replacementPreview.Count} 处材质引用";
            lastResultDetail = BuildScanReport(replacementPreview.Count, replacementPreview, scanWarnings);

            string preview = SimpleToolsSafetyUtility.JoinPreview(replacementPreview.Select(item => item.ToOneLine()), PreviewDialogLimit);
            EditorUtility.DisplayDialog("预览已刷新", $"预计可替换 {replacementPreview.Count} 处材质引用。\n\n{preview}", "完成");
        }

        public void EnableAllPreviewItems()
        {
            foreach (var item in replacementPreview)
                item.Enabled = true;
        }

        public void DisableAllPreviewItems()
        {
            foreach (var item in replacementPreview)
                item.Enabled = false;
        }

        [HideInInspector]
        public List<MaterialReferenceRecord> replacementPreview = new List<MaterialReferenceRecord>();

        public void QueryMaterialUsage()
        {
            if (!TryValidateScanSettings(out var message, requireTargetMaterial: false))
            {
                usedMaterials.Clear();
                EditorUtility.DisplayDialog("无法查询", message, "知道了");
                return;
            }

            usedMaterials = BuildUsageList(out var warnings);
            lastResultSummary = $"查询完成: 找到 {usedMaterials.Count} 处材质引用";
            lastResultDetail = BuildUsageReport(usedMaterials.Count, usedMaterials, warnings);
            EditorUtility.DisplayDialog("查询完成", $"找到 {usedMaterials.Count} 处材质引用。", "完成");
        }

        [HideInInspector]
        public List<MaterialUsage> usedMaterials = new List<MaterialUsage>();

        [HideInInspector]
        [LabelText("启用资产批处理")]
        public bool enablePrefabAssetBatch = false;

        [HideInInspector]
        [LabelText("优先处理选中 Prefab 资产")]
        public bool useSelectedPrefabAssets = true;

        [HideInInspector]
        [FolderPath(AbsolutePath = false)]
        [LabelText("Prefab 搜索文件夹")]
        [ShowIf("enablePrefabAssetBatch")]
        [HideIf("useSelectedPrefabAssets")]
        public string prefabAssetFolder = "Assets";

        [HideInInspector]
        public List<PrefabAssetBatchRecord> prefabAssetPreview = new List<PrefabAssetBatchRecord>();

        public void RefreshPrefabAssetPreview()
        {
            if (!TryValidateScanSettings(out var message))
            {
                prefabAssetPreview.Clear();
                EditorUtility.DisplayDialog("设置不完整", message, "知道了");
                return;
            }

            if (!TryValidatePrefabAssetBatchSettings(out message))
            {
                prefabAssetPreview.Clear();
                EditorUtility.DisplayDialog("Prefab 批处理设置不完整", message, "知道了");
                return;
            }

            prefabAssetPreview = BuildPrefabAssetPreview(out var warnings);
            previewSettingsSignature = BuildSettingsSignature();
            lastResultSummary = $"Prefab 资产预览完成: 命中 {prefabAssetPreview.Count} 个 prefab 资产";
            lastResultDetail = BuildPrefabBatchReport(prefabAssetPreview, warnings);
            EditorUtility.DisplayDialog("Prefab 资产预览已刷新", $"命中 {prefabAssetPreview.Count} 个 prefab 资产。", "完成");
        }

        public void ReplacePrefabAssets()
        {
            if (!TryValidateScanSettings(out var message))
            {
                EditorUtility.DisplayDialog("设置不完整", message, "知道了");
                return;
            }

            if (!TryValidatePrefabAssetBatchSettings(out message))
            {
                EditorUtility.DisplayDialog("Prefab 批处理设置不完整", message, "知道了");
                return;
            }

            if (prefabAssetPreview.Count == 0 || previewSettingsSignature != BuildSettingsSignature())
            {
                prefabAssetPreview = BuildPrefabAssetPreview(out _);
                previewSettingsSignature = BuildSettingsSignature();
            }

            var enabledEntries = prefabAssetPreview.Where(item => item.Enabled && item.MatchedCount > 0).ToList();
            if (enabledEntries.Count == 0)
            {
                EditorUtility.DisplayDialog("没有可替换的 Prefab", "当前预览里没有已勾选且有命中的 Prefab 资产。", "知道了");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(enabledEntries.Select(item => item.ToOneLine()), PreviewDialogLimit);
            if (!EditorUtility.DisplayDialog("确认批量替换 Prefab 资产",
                $"将处理 {enabledEntries.Count} 个 Prefab 资产。\n\n{preview}\n\n会直接保存 Prefab 资产文件，继续吗？",
                "开始替换", "取消"))
                return;

            ExecutePrefabAssetReplacement(enabledEntries);
        }

        public void ReplaceMaterials()
        {
            if (!TryValidateScanSettings(out var message))
            {
                EditorUtility.DisplayDialog("设置不完整", message, "知道了");
                return;
            }

            if (replacementPreview.Count == 0 || previewSettingsSignature != BuildSettingsSignature())
            {
                replacementPreview = BuildReplacementPreview(out _);
                previewSettingsSignature = BuildSettingsSignature();
            }

            var enabledRecords = replacementPreview.Where(item => item.Enabled && item.CanWrite).ToList();
            if (enabledRecords.Count == 0)
            {
                EditorUtility.DisplayDialog("没有可替换项", "当前预览里没有已勾选且可写的材质引用。请先刷新预览，或检查过滤条件。", "知道了");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(enabledRecords.Select(item => item.ToOneLine()), PreviewDialogLimit);
            if (!EditorUtility.DisplayDialog("确认批量替换材质",
                $"将替换 {enabledRecords.Count} 处材质引用。\n\n{preview}\n\n会修改场景对象或 Prefab 实例上的组件引用，支持 Ctrl+Z 撤销。继续吗？",
                "开始替换", "取消"))
                return;

            ExecuteReplacement(enabledRecords);
        }

        [OnInspectorGUI, PropertyOrder(-200)]
        private void DrawResultPanel()
        {
            DrawMaterialWorkbench();
        }

        private void DrawMaterialWorkbench()
        {
            DrawMaterialHeader();
            DrawMaterialModeTabs();
            DrawMaterialScopePanel();
            DrawMaterialRulePanel();
            DrawMaterialTargetPanel();

            if (materialToolMode == 0)
            {
                DrawScenePreviewActions();
                DrawReplacementInsightPanel();
                DrawReplacementFilters();
                DrawReplacementTable();
            }
            else
            {
                DrawPrefabAssetModePanel();
                DrawPrefabAssetInsightPanel();
                DrawPrefabAssetTable();
            }

            DrawMaterialReportPanel();
        }

        private void DrawMaterialHeader()
        {
            int targetCount = CollectTargets().Count;
            int enabledPreview = replacementPreview.Count(item => item.Enabled);
            int writablePreview = replacementPreview.Count(item => item.Enabled && item.CanWrite);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("材质引用审计与替换台", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("先扫描材质引用，再按预览结果执行替换。场景实例和 Prefab 资产分开处理，避免资产级修改混入场景操作。", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetric("目标对象", targetCount.ToString());
                    DrawMetric("预览命中", replacementPreview.Count.ToString());
                    DrawMetric("已勾选", enabledPreview.ToString());
                    DrawMetric("可写", writablePreview.ToString());
                    DrawMetric("资产命中", prefabAssetPreview.Count.ToString());
                }
            }
        }

        private void DrawMaterialModeTabs()
        {
            materialToolMode = GUILayout.Toolbar(materialToolMode, MaterialToolModeLabels, GUILayout.Height(28));
        }

        private void DrawMaterialScopePanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("扫描范围", "选择扫描场景对象、当前选择，或在资产模式中扫描 Prefab 文件。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("目标范围", EditorStyles.miniBoldLabel, GUILayout.Width(58));
                    targetScope = (TargetScope)GUILayout.Toolbar((int)targetScope, TargetScopeLabels, EditorStyles.miniButton, GUILayout.Height(24));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    includeInactive = GUILayout.Toggle(includeInactive, "包含未激活", EditorStyles.miniButtonLeft, GUILayout.Height(22));
                    skipHideFlagsObjects = GUILayout.Toggle(skipHideFlagsObjects, "跳过隐藏对象", EditorStyles.miniButtonMid, GUILayout.Height(22));
                    scanSerializedScriptFields = GUILayout.Toggle(scanSerializedScriptFields, "扫描脚本材质字段", EditorStyles.miniButtonRight, GUILayout.Height(22));
                }

                componentTypes = DrawComponentMask(componentTypes);
                DrawInfoRow("当前来源", GetEditingSourceLabel());
                DrawInfoRow("当前选择", Selection.gameObjects == null || Selection.gameObjects.Length == 0 ? "未选择对象" : $"{Selection.gameObjects.Length} 个对象");
            }
        }

        private void DrawMaterialRulePanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("匹配规则", "定义哪些材质引用会进入替换预览。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("模式", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                    replacementMode = (ReplacementMode)GUILayout.Toolbar((int)replacementMode, ReplacementModeLabels, EditorStyles.miniButton, GUILayout.Height(24));
                }

                switch (replacementMode)
                {
                    case ReplacementMode.ReplaceSpecific:
                        sourceMaterial = (Material)EditorGUILayout.ObjectField("源材质", sourceMaterial, typeof(Material), false);
                        break;
                    case ReplacementMode.MatchByName:
                        matchName = EditorGUILayout.TextField("名称包含", matchName);
                        ignoreCase = EditorGUILayout.Toggle("忽略大小写", ignoreCase);
                        break;
                    case ReplacementMode.MatchByRegex:
                        regexPattern = EditorGUILayout.TextField("正则表达式", regexPattern);
                        ignoreCase = EditorGUILayout.Toggle("忽略大小写", ignoreCase);
                        break;
                    case ReplacementMode.MatchByShader:
                        sourceShader = (Shader)EditorGUILayout.ObjectField("源 Shader", sourceShader, typeof(Shader), false);
                        break;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    setDefaultWhenNull = GUILayout.Toggle(setDefaultWhenNull, "空材质也填充", EditorStyles.miniButtonLeft, GUILayout.Height(22));
                    allowSameMaterial = GUILayout.Toggle(allowSameMaterial, "允许替换成相同材质", EditorStyles.miniButtonRight, GUILayout.Height(22));
                }
            }
        }

        private void DrawMaterialTargetPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("替换目标", "所有命中的引用会被替换为这个目标材质。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                targetMaterial = (Material)EditorGUILayout.ObjectField("目标材质", targetMaterial, typeof(Material), false);
                DrawInfoRow("目标 Shader", targetMaterial != null && targetMaterial.shader != null ? targetMaterial.shader.name : "未设置");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("使用当前选中材质作为源", EditorStyles.miniButtonLeft, GUILayout.Height(24)))
                        UseSelectionAsSourceMaterial();
                    if (GUILayout.Button("使用当前选中材质作为目标", EditorStyles.miniButtonMid, GUILayout.Height(24)))
                        UseSelectionAsTargetMaterial();
                    if (GUILayout.Button("交换源和目标", EditorStyles.miniButtonRight, GUILayout.Height(24)))
                        SwapSourceAndTargetMaterial();
                }
            }
        }

        private void DrawScenePreviewActions()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("预览与执行", "场景模式只处理场景或 Prefab Mode 中的实例引用，执行前会检查预览签名。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (IsPreviewStale(replacementPreview.Count))
                    SimpleToolsPanelUtility.DrawWarning("设置已变化，当前预览可能不是最新。执行时会自动重建预览，建议先手动刷新确认。");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("刷新替换预览", SimpleToolsActionTone.Primary, 32))
                        RefreshReplacementPreview();
                    if (SimpleToolsPanelUtility.DrawActionButton("查询材质使用", SimpleToolsActionTone.Neutral, 32))
                        QueryMaterialUsage();
                    if (SimpleToolsPanelUtility.DrawActionButton("执行勾选替换", SimpleToolsActionTone.Warning, 32))
                        ReplaceMaterials();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("全选当前预览", EditorStyles.miniButtonLeft, GUILayout.Height(24)))
                        EnableFilteredPreviewItems(true);
                    if (GUILayout.Button("取消当前预览", EditorStyles.miniButtonMid, GUILayout.Height(24)))
                        EnableFilteredPreviewItems(false);
                    if (GUILayout.Button("选中命中对象", EditorStyles.miniButtonRight, GUILayout.Height(24)))
                        SelectFilteredPreviewObjects();
                }
            }
        }

        private void DrawReplacementInsightPanel()
        {
            var rows = GetFilteredPreviewRecords(false);
            if (replacementPreview.Count == 0 && usedMaterials.Count == 0)
                return;

            SimpleToolsPanelUtility.DrawSectionTitle("预览统计", "用于快速判断命中是否集中在某些材质、Shader、组件或 Prefab 实例。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawInfoRow("材质分布", BuildMaterialSummary(rows, 6));
                DrawInfoRow("Shader分布", BuildShaderSummary(rows, 6));
                DrawInfoRow("组件分布", BuildComponentSummary(rows));
                DrawInfoRow("来源分布", BuildSourceSummary(rows));
            }
        }

        private void DrawReplacementFilters()
        {
            if (replacementPreview.Count == 0 && usedMaterials.Count == 0)
                return;

            SimpleToolsPanelUtility.DrawSectionTitle("结果筛选", "搜索对象路径、材质名、Shader、组件类型、槽位和来源。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", EditorStyles.miniBoldLabel, GUILayout.Width(42));
                    materialSearch = EditorGUILayout.TextField(materialSearch);
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                        materialSearch = string.Empty;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("可写", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                    materialWritableFilter = GUILayout.Toolbar(materialWritableFilter, WritableFilterLabels, EditorStyles.miniButton, GUILayout.Width(118), GUILayout.Height(22));
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField("来源", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                    materialSourceFilter = GUILayout.Toolbar(materialSourceFilter, SourceFilterLabels, EditorStyles.miniButton, GUILayout.Width(160), GUILayout.Height(22));
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField("排序", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                    materialSortIndex = GUILayout.Toolbar(materialSortIndex, MaterialSortLabels, EditorStyles.miniButton, GUILayout.Width(210), GUILayout.Height(22));
                }
            }
        }

        private void DrawReplacementTable()
        {
            var rows = GetFilteredPreviewRecords(true);
            if (replacementPreview.Count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"替换预览表  ({rows.Count}/{replacementPreview.Count})", EditorStyles.boldLabel);
                if (rows.Count == 0)
                {
                    EditorGUILayout.LabelField("当前筛选条件下没有预览项。", EditorStyles.wordWrappedMiniLabel);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("选", EditorStyles.miniBoldLabel, GUILayout.Width(28));
                    EditorGUILayout.LabelField("对象路径", EditorStyles.miniBoldLabel, GUILayout.MinWidth(170));
                    EditorGUILayout.LabelField("组件/槽位", EditorStyles.miniBoldLabel, GUILayout.Width(150));
                    EditorGUILayout.LabelField("当前 -> 目标", EditorStyles.miniBoldLabel, GUILayout.Width(180));
                    EditorGUILayout.LabelField("状态", EditorStyles.miniBoldLabel, GUILayout.Width(92));
                    GUILayout.Space(96);
                }

                int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)rows.Count / materialPageSize));
                materialPageIndex = Mathf.Clamp(materialPageIndex, 0, totalPages - 1);
                int start = materialPageIndex * materialPageSize;
                int end = Mathf.Min(start + materialPageSize, rows.Count);
                for (int i = start; i < end; i++)
                    DrawReplacementRow(rows[i]);

                DrawPager(ref materialPageIndex, totalPages);
            }
        }

        private void DrawReplacementRow(MaterialReferenceRecord record)
        {
            if (record == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                record.Enabled = EditorGUILayout.Toggle(record.Enabled, GUILayout.Width(28));
                EditorGUILayout.LabelField(record.TargetPath, EditorStyles.miniLabel, GUILayout.MinWidth(170));
                EditorGUILayout.LabelField($"{record.ComponentType}.{record.Location}", EditorStyles.miniLabel, GUILayout.Width(150));
                EditorGUILayout.LabelField($"{GetMaterialName(record.CurrentMaterial)} -> {GetMaterialName(record.TargetMaterial)}", EditorStyles.miniLabel, GUILayout.Width(180));
                EditorGUILayout.LabelField($"{(record.CanWrite ? "可写" : "不可写")} | {record.Source}", EditorStyles.miniLabel, GUILayout.Width(92));
                if (GUILayout.Button("对象", EditorStyles.miniButtonLeft, GUILayout.Width(44)))
                    record.PingObject();
                if (GUILayout.Button("当前", EditorStyles.miniButtonMid, GUILayout.Width(44)))
                    record.PingCurrentMaterial();
                if (GUILayout.Button("目标", EditorStyles.miniButtonRight, GUILayout.Width(44)))
                    record.PingTargetMaterial();
            }
        }

        private void DrawPrefabAssetModePanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("Prefab 资产模式", "资产模式会 LoadPrefabContents 并保存 Prefab 文件，适合批量修正资源库中的预制件。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                enablePrefabAssetBatch = true;
                useSelectedPrefabAssets = GUILayout.Toggle(useSelectedPrefabAssets, "优先处理 Project 中选中的 Prefab 资产", EditorStyles.miniButton, GUILayout.Height(24));
                if (!useSelectedPrefabAssets)
                    prefabAssetFolder = EditorGUILayout.TextField("Prefab 搜索文件夹", prefabAssetFolder);

                if (IsPreviewStale(prefabAssetPreview.Count))
                    SimpleToolsPanelUtility.DrawWarning("设置已变化，当前 Prefab 资产预览可能不是最新。执行前建议刷新。");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("扫描 Prefab 资产", SimpleToolsActionTone.Primary, 32))
                        RefreshPrefabAssetPreview();
                    if (SimpleToolsPanelUtility.DrawActionButton("执行资产替换", SimpleToolsActionTone.Warning, 32))
                        ReplacePrefabAssets();
                    if (SimpleToolsPanelUtility.DrawActionButton("导出报告", SimpleToolsActionTone.Neutral, 32))
                        ExportLastReportTxt();
                }
            }
        }

        private void DrawPrefabAssetInsightPanel()
        {
            if (prefabAssetPreview.Count == 0)
                return;

            var rows = GetFilteredPrefabAssetRecords(false);
            SimpleToolsPanelUtility.DrawSectionTitle("资产预览统计", "按 Prefab 文件统计命中和可写引用。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawInfoRow("命中资产", $"{rows.Count}/{prefabAssetPreview.Count}");
                DrawInfoRow("引用统计", $"命中 {rows.Sum(r => r.MatchedCount)} | 可写 {rows.Sum(r => r.WritableCount)}");
                DrawInfoRow("组件分布", BuildPrefabAssetComponentSummary(rows));
            }
        }

        private void DrawPrefabAssetTable()
        {
            if (prefabAssetPreview.Count == 0)
                return;

            var rows = GetFilteredPrefabAssetRecords(true);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Prefab 资产预览表  ({rows.Count}/{prefabAssetPreview.Count})", EditorStyles.boldLabel);
                if (rows.Count == 0)
                {
                    EditorGUILayout.LabelField("当前筛选条件下没有 Prefab 资产。", EditorStyles.wordWrappedMiniLabel);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("选", EditorStyles.miniBoldLabel, GUILayout.Width(28));
                    EditorGUILayout.LabelField("Prefab路径", EditorStyles.miniBoldLabel, GUILayout.MinWidth(240));
                    EditorGUILayout.LabelField("命中", EditorStyles.miniBoldLabel, GUILayout.Width(52));
                    EditorGUILayout.LabelField("可写", EditorStyles.miniBoldLabel, GUILayout.Width(52));
                    EditorGUILayout.LabelField("组件分布", EditorStyles.miniBoldLabel, GUILayout.Width(160));
                    GUILayout.Space(52);
                }

                int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)rows.Count / materialPageSize));
                materialPrefabPageIndex = Mathf.Clamp(materialPrefabPageIndex, 0, totalPages - 1);
                int start = materialPrefabPageIndex * materialPageSize;
                int end = Mathf.Min(start + materialPageSize, rows.Count);
                for (int i = start; i < end; i++)
                {
                    var record = rows[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        record.Enabled = EditorGUILayout.Toggle(record.Enabled, GUILayout.Width(28));
                        EditorGUILayout.LabelField(record.AssetPath, EditorStyles.miniLabel, GUILayout.MinWidth(240));
                        EditorGUILayout.LabelField(record.MatchedCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(52));
                        EditorGUILayout.LabelField(record.WritableCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(52));
                        EditorGUILayout.LabelField(record.ComponentSummary, EditorStyles.miniLabel, GUILayout.Width(160));
                        if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(48)))
                            record.PingAsset();
                    }
                }

                DrawPager(ref materialPrefabPageIndex, totalPages);
            }
        }

        private void DrawMaterialReportPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("报告", "复制或导出最近一次扫描、预览或执行结果。");
            if (string.IsNullOrWhiteSpace(lastResultSummary) && string.IsNullOrWhiteSpace(lastResultDetail))
            {
                SimpleToolsPanelUtility.DrawEmptyState("还没有报告。刷新预览、查询使用或执行替换后，这里会显示结果。");
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(lastResultSummary, EditorStyles.boldLabel);
                if (!string.IsNullOrWhiteSpace(lastResultDetail))
                    EditorGUILayout.TextArea(lastResultDetail, GUILayout.MinHeight(46), GUILayout.MaxHeight(120));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("复制报告", EditorStyles.miniButtonLeft, GUILayout.Height(24)))
                        CopyLastReport();
                    if (GUILayout.Button("导出 TXT", EditorStyles.miniButtonRight, GUILayout.Height(24)))
                        ExportLastReportTxt();
                }
            }
        }

        public void CopyLastReport()
        {
            if (string.IsNullOrWhiteSpace(lastResultSummary) && string.IsNullOrWhiteSpace(lastResultDetail))
            {
                EditorUtility.DisplayDialog("没有报告", "还没有可复制的扫描或执行结果。", "知道了");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = string.IsNullOrWhiteSpace(lastResultDetail)
                ? lastResultSummary
                : lastResultSummary + "\n\n" + lastResultDetail;
        }

        public void ExportLastReportTxt()
        {
            if (string.IsNullOrWhiteSpace(lastResultSummary) && string.IsNullOrWhiteSpace(lastResultDetail))
            {
                EditorUtility.DisplayDialog("没有报告", "还没有可导出的扫描或执行结果。", "知道了");
                return;
            }

            const string reportFolder = "Assets/_ESToolReports";
            if (!SimpleToolsSafetyUtility.EnsureAssetFolder(reportFolder, out var error))
            {
                EditorUtility.DisplayDialog("导出失败", error, "知道了");
                return;
            }

            string fileName = $"MaterialReplacement_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string assetPath = $"{reportFolder}/{fileName}";
            string fullPath = SimpleToolsSafetyUtility.AssetPathToFullPath(assetPath);
            File.WriteAllText(fullPath, string.IsNullOrWhiteSpace(lastResultDetail)
                ? lastResultSummary
                : lastResultSummary + Environment.NewLine + Environment.NewLine + lastResultDetail);
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath));
            EditorUtility.DisplayDialog("导出完成", $"报告已导出到 {assetPath}", "完成");
        }

        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private string previewSettingsSignature = "";
        private int materialToolMode = 0;
        private int materialWritableFilter = 0;
        private int materialSourceFilter = 0;
        private int materialSortIndex = 0;
        private int materialPageIndex = 0;
        private int materialPrefabPageIndex = 0;
        private string materialSearch = "";
        private const int materialPageSize = 12;
        private static readonly string[] MaterialToolModeLabels = { "场景/Prefab Mode", "Prefab 资产" };
        private static readonly string[] TargetScopeLabels = { "只选中", "选中+子物体", "当前场景" };
        private static readonly string[] ReplacementModeLabels = { "指定材质", "全部非空", "名称包含", "正则", "Shader" };
        private static readonly string[] WritableFilterLabels = { "全部", "可写", "不可写" };
        private static readonly string[] SourceFilterLabels = { "全部", "Scene", "PrefabMode", "PrefabAsset" };
        private static readonly string[] MaterialSortLabels = { "对象", "材质", "Shader", "组件", "风险" };

        [Serializable]
        public class MaterialReferenceRecord
        {
            [LabelText("启用"), TableColumnWidth(42, Resizable = false)]
            public bool Enabled = true;

            [ReadOnly, LabelText("来源"), TableColumnWidth(76)]
            public string Source;

            [ReadOnly, LabelText("对象"), TableColumnWidth(110)]
            public GameObject TargetObject;

            [ReadOnly, LabelText("对象路径"), TableColumnWidth(220)]
            public string TargetPath;

            [ReadOnly, LabelText("组件"), TableColumnWidth(120)]
            public Component TargetComponent;

            [ReadOnly, LabelText("组件类型"), TableColumnWidth(130)]
            public string ComponentType;

            [ReadOnly, LabelText("位置"), TableColumnWidth(180)]
            public string Location;

            [ReadOnly, LabelText("当前材质"), TableColumnWidth(130)]
            public Material CurrentMaterial;

            [ReadOnly, LabelText("目标材质"), TableColumnWidth(130)]
            public Material TargetMaterial;

            [ReadOnly, LabelText("可写"), TableColumnWidth(44, Resizable = false)]
            public bool CanWrite;

            [ReadOnly, LabelText("风险"), TableColumnWidth(130)]
            public string Risk;

            [NonSerialized]
            internal string AssetPath;

            [NonSerialized]
            private MaterialReferenceAccessor accessor;

            internal MaterialReferenceAccessor Accessor
            {
                get => accessor;
                set => accessor = value;
            }

            [HorizontalGroup("Actions")]
            [Button("Ping对象", ButtonHeight = 24), GUIColor(0.4f, 0.75f, 0.45f)]
            public void PingObject()
            {
                if (TargetObject == null)
                    return;

                Selection.activeGameObject = TargetObject;
                EditorGUIUtility.PingObject(TargetObject);
            }

            [HorizontalGroup("Actions")]
            [Button("Ping当前材质", ButtonHeight = 24), GUIColor(0.35f, 0.58f, 0.9f)]
            public void PingCurrentMaterial()
            {
                if (CurrentMaterial == null)
                    return;

                Selection.activeObject = CurrentMaterial;
                EditorGUIUtility.PingObject(CurrentMaterial);
            }

            [HorizontalGroup("Actions")]
            [Button("Ping目标材质", ButtonHeight = 24), GUIColor(0.75f, 0.55f, 0.25f)]
            public void PingTargetMaterial()
            {
                if (TargetMaterial == null)
                    return;

                Selection.activeObject = TargetMaterial;
                EditorGUIUtility.PingObject(TargetMaterial);
            }

            public string ToOneLine()
            {
                string current = CurrentMaterial != null ? CurrentMaterial.name : "<空>";
                string target = TargetMaterial != null ? TargetMaterial.name : "<未设置>";
                return $"{TargetPath} | {ComponentType}.{Location} | {current} -> {target}";
            }
        }

        [Serializable]
        public class MaterialUsage : IEquatable<MaterialUsage>
        {
            [ReadOnly, LabelText("对象"), TableColumnWidth(120)]
            public GameObject targetObject;

            [ReadOnly, LabelText("对象路径"), TableColumnWidth(240)]
            public string targetPath;

            [ReadOnly, LabelText("组件"), TableColumnWidth(130)]
            public Component component;

            [ReadOnly, LabelText("组件类型"), TableColumnWidth(130)]
            public string componentType;

            [ReadOnly, LabelText("位置"), TableColumnWidth(180)]
            public string location;

            [ReadOnly, LabelText("材质"), TableColumnWidth(150)]
            public Material material;

            [HorizontalGroup("Actions")]
            [Button("Ping对象", ButtonHeight = 24), GUIColor(0.4f, 0.75f, 0.45f)]
            public void FocusObject()
            {
                if (targetObject == null)
                    return;

                Selection.activeGameObject = targetObject;
                EditorGUIUtility.PingObject(targetObject);
            }

            [HorizontalGroup("Actions")]
            [Button("Ping材质", ButtonHeight = 24), GUIColor(0.35f, 0.58f, 0.9f)]
            public void FocusMaterial()
            {
                if (material == null)
                    return;

                Selection.activeObject = material;
                EditorGUIUtility.PingObject(material);
            }

            public bool Equals(MaterialUsage other)
            {
                if (other == null)
                    return false;

                return targetObject == other.targetObject &&
                       component == other.component &&
                       location == other.location &&
                       material == other.material;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as MaterialUsage);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (targetObject != null ? targetObject.GetHashCode() : 0);
                    hash = hash * 31 + (component != null ? component.GetHashCode() : 0);
                    hash = hash * 31 + (location != null ? location.GetHashCode() : 0);
                    hash = hash * 31 + (material != null ? material.GetHashCode() : 0);
                    return hash;
                }
            }
        }

        [Serializable]
        public class PrefabAssetBatchRecord
        {
            [LabelText("启用"), TableColumnWidth(42, Resizable = false)]
            public bool Enabled = true;

            [ReadOnly, LabelText("Prefab"), TableColumnWidth(150)]
            public UnityEngine.Object Asset;

            [ReadOnly, LabelText("路径"), TableColumnWidth(260)]
            public string AssetPath;

            [ReadOnly, LabelText("命中"), TableColumnWidth(54, Resizable = false)]
            public int MatchedCount;

            [ReadOnly, LabelText("可写"), TableColumnWidth(54, Resizable = false)]
            public int WritableCount;

            [ReadOnly, LabelText("组件分布"), TableColumnWidth(220)]
            public string ComponentSummary;

            [ReadOnly, LabelText("预览"), TableColumnWidth(320)]
            public string Preview;

            [NonSerialized]
            internal List<MaterialReferenceRecord> Records = new List<MaterialReferenceRecord>();

            [Button("Ping", ButtonHeight = 22), TableColumnWidth(48, Resizable = false)]
            public void PingAsset()
            {
                if (Asset == null && !string.IsNullOrEmpty(AssetPath))
                    Asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetPath);

                if (Asset == null)
                    return;

                Selection.activeObject = Asset;
                EditorGUIUtility.PingObject(Asset);
            }

            public string ToOneLine()
            {
                return $"{AssetPath} | 命中 {MatchedCount} | 可写 {WritableCount}";
            }
        }

        internal abstract class MaterialReferenceAccessor
        {
            public abstract Component Component { get; }
            public abstract string ComponentType { get; }
            public abstract string Location { get; }
            public abstract bool CanWrite { get; }
            public abstract Material Read();
            public abstract bool Write(Material material, out string error);
        }

        private sealed class RendererMaterialAccessor : MaterialReferenceAccessor
        {
            private readonly Renderer renderer;
            private readonly int slotIndex;

            public RendererMaterialAccessor(Renderer renderer, int slotIndex)
            {
                this.renderer = renderer;
                this.slotIndex = slotIndex;
            }

            public override Component Component => renderer;
            public override string ComponentType => renderer != null ? renderer.GetType().Name : "Renderer";
            public override string Location => $"sharedMaterials[{slotIndex}]";
            public override bool CanWrite => renderer != null && slotIndex >= 0 && slotIndex < renderer.sharedMaterials.Length;

            public override Material Read()
            {
                if (!CanWrite)
                    return null;

                return renderer.sharedMaterials[slotIndex];
            }

            public override bool Write(Material material, out string error)
            {
                error = null;
                if (!CanWrite)
                {
                    error = "Renderer 材质槽位已经失效。";
                    return false;
                }

                Undo.RecordObject(renderer, "批量替换材质");
                var materials = renderer.sharedMaterials;
                materials[slotIndex] = material;
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                return true;
            }
        }

        private sealed class SerializedMaterialAccessor : MaterialReferenceAccessor
        {
            private readonly MonoBehaviour mono;
            private readonly string propertyPath;

            public SerializedMaterialAccessor(MonoBehaviour mono, string propertyPath)
            {
                this.mono = mono;
                this.propertyPath = propertyPath;
            }

            public override Component Component => mono;
            public override string ComponentType => mono != null ? mono.GetType().Name : "MonoBehaviour";
            public override string Location => propertyPath;
            public override bool CanWrite => mono != null && FindProperty() != null;

            public override Material Read()
            {
                var property = FindProperty();
                return property != null ? property.objectReferenceValue as Material : null;
            }

            public override bool Write(Material material, out string error)
            {
                error = null;
                if (mono == null)
                {
                    error = "脚本组件已经丢失。";
                    return false;
                }

                var serializedObject = new SerializedObject(mono);
                serializedObject.Update();
                var property = serializedObject.FindProperty(propertyPath);
                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    error = "序列化字段路径已经失效。";
                    return false;
                }

                Undo.RecordObject(mono, "批量替换脚本材质字段");
                property.objectReferenceValue = material;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(mono);
                PrefabUtility.RecordPrefabInstancePropertyModifications(mono);
                return true;
            }

            private SerializedProperty FindProperty()
            {
                if (mono == null)
                    return null;

                var serializedObject = new SerializedObject(mono);
                serializedObject.Update();
                return serializedObject.FindProperty(propertyPath);
            }
        }

        private List<MaterialReferenceRecord> BuildReplacementPreview(out List<string> warnings)
        {
            warnings = new List<string>();
            var records = ScanMaterialReferences(warnings)
                .Where(item => ShouldReplaceMaterial(item.CurrentMaterial))
                .Where(item => allowSameMaterial || item.CurrentMaterial != targetMaterial)
                .ToList();

            foreach (var record in records)
            {
                record.TargetMaterial = targetMaterial;
                record.Risk = BuildRiskLabel(record);
            }

            return records;
        }

        private List<MaterialUsage> BuildUsageList(out List<string> warnings)
        {
            warnings = new List<string>();
            return ScanMaterialReferences(warnings)
                .Where(item => item.CurrentMaterial != null)
                .Select(item => new MaterialUsage
                {
                    targetObject = item.TargetObject,
                    targetPath = item.TargetPath,
                    component = item.TargetComponent,
                    componentType = item.ComponentType,
                    location = item.Location,
                    material = item.CurrentMaterial
                })
                .Distinct()
                .ToList();
        }

        private List<MaterialReferenceRecord> ScanMaterialReferences(List<string> warnings)
        {
            return ScanMaterialReferences(CollectTargets(), warnings, GetEditingSourceLabel());
        }

        private List<MaterialReferenceRecord> ScanMaterialReferences(IEnumerable<GameObject> targets, List<string> warnings, string sourceLabel, string assetPath = null)
        {
            var records = new List<MaterialReferenceRecord>();
            foreach (var obj in targets)
            {
                if (obj == null)
                    continue;

                if (skipHideFlagsObjects && obj.hideFlags != HideFlags.None)
                    continue;

                if ((componentTypes & ComponentType.Renderer) != 0)
                {
                    foreach (var renderer in obj.GetComponents<Renderer>())
                        AddRendererRecords(records, obj, renderer, includeParticleRenderers: false, sourceLabel, assetPath);
                }

                if ((componentTypes & ComponentType.ParticleSystemRenderer) != 0)
                {
                    foreach (var particleRenderer in obj.GetComponents<ParticleSystemRenderer>())
                        AddRendererRecords(records, obj, particleRenderer, includeParticleRenderers: true, sourceLabel, assetPath);
                }

                if ((componentTypes & ComponentType.MonoBehaviour) != 0 && scanSerializedScriptFields)
                    AddScriptRecords(records, obj, warnings, sourceLabel, assetPath);
            }

            return records;
        }

        private void AddRendererRecords(List<MaterialReferenceRecord> records, GameObject obj, Renderer renderer, bool includeParticleRenderers, string sourceLabel, string assetPath)
        {
            if (renderer == null)
                return;

            if (!includeParticleRenderers && renderer is ParticleSystemRenderer)
                return;

            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var accessor = new RendererMaterialAccessor(renderer, i);
                records.Add(CreateRecord(obj, accessor, sourceLabel, assetPath));
            }
        }

        private void AddScriptRecords(List<MaterialReferenceRecord> records, GameObject obj, List<string> warnings, string sourceLabel, string assetPath)
        {
            foreach (var mono in obj.GetComponents<MonoBehaviour>())
            {
                if (mono == null)
                {
                    warnings.Add($"{GetTransformPath(obj)} 存在丢失脚本，已跳过。");
                    continue;
                }

                try
                {
                    var serializedObject = new SerializedObject(mono);
                    serializedObject.Update();
                    var iterator = serializedObject.GetIterator();
                    bool enterChildren = true;
                    while (iterator.NextVisible(enterChildren))
                    {
                        enterChildren = true;
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                            continue;

                        if (!IsMaterialReferenceProperty(iterator))
                            continue;

                        var accessor = new SerializedMaterialAccessor(mono, iterator.propertyPath);
                        records.Add(CreateRecord(obj, accessor, sourceLabel, assetPath));
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"{GetTransformPath(obj)} / {mono.GetType().Name}: 扫描脚本字段失败，{ex.Message}");
                }
            }
        }

        private MaterialReferenceRecord CreateRecord(GameObject obj, MaterialReferenceAccessor accessor, string sourceLabel, string assetPath)
        {
            return new MaterialReferenceRecord
            {
                Enabled = true,
                Source = sourceLabel,
                TargetObject = obj,
                TargetPath = GetTransformPath(obj),
                TargetComponent = accessor.Component,
                ComponentType = accessor.ComponentType,
                Location = accessor.Location,
                CurrentMaterial = accessor.Read(),
                TargetMaterial = targetMaterial,
                CanWrite = accessor.CanWrite,
                Risk = "普通",
                AssetPath = assetPath,
                Accessor = accessor
            };
        }

        private void ExecuteReplacement(List<MaterialReferenceRecord> records)
        {
            int replaced = 0;
            var changedObjects = new HashSet<GameObject>();
            var changedComponents = new HashSet<Component>();
            var failedMessages = new List<string>();

            Undo.SetCurrentGroupName("批量替换材质");
            int undoGroup = Undo.GetCurrentGroup();
            try
            {
                foreach (var record in records)
                {
                    if (record == null || record.Accessor == null)
                    {
                        failedMessages.Add("存在失效预览项，已跳过。");
                        continue;
                    }

                    var current = record.Accessor.Read();
                    if (!ShouldReplaceMaterial(current) || (!allowSameMaterial && current == targetMaterial))
                        continue;

                    if (record.Accessor.Write(targetMaterial, out var error))
                    {
                        replaced++;
                        record.CurrentMaterial = targetMaterial;
                        if (record.TargetObject != null)
                            changedObjects.Add(record.TargetObject);
                        if (record.TargetComponent != null)
                            changedComponents.Add(record.TargetComponent);
                    }
                    else
                    {
                        failedMessages.Add($"{record.TargetPath} | {record.ComponentType}.{record.Location}: {error}");
                    }
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            MarkScenesDirty(changedObjects);
            lastResultSummary = $"替换完成: 已执行勾选可写项 {records.Count} 个 | 实际替换 {replaced} 处 | 影响 {changedObjects.Count} 个对象 | 失败 {failedMessages.Count} 项";
            lastResultDetail = BuildExecutionReport(changedObjects, failedMessages);

            string failed = failedMessages.Count > 0 ? "\n\n失败项:\n" + SimpleToolsSafetyUtility.JoinPreview(failedMessages, 8) : string.Empty;
            EditorUtility.DisplayDialog("材质替换完成", $"已替换 {replaced} 处材质引用，影响 {changedObjects.Count} 个对象。{failed}", "完成");
        }

        private List<PrefabAssetBatchRecord> BuildPrefabAssetPreview(out List<string> warnings)
        {
            warnings = new List<string>();
            var result = new List<PrefabAssetBatchRecord>();
            var prefabPaths = FindPrefabAssetPaths(warnings);

            foreach (var prefabPath in prefabPaths)
            {
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(prefabPath);
                    var targets = CollectTargetsFromRoots(new[] { root });
                    var records = ScanMaterialReferences(targets, warnings, "PrefabAsset", prefabPath)
                        .Where(item => ShouldReplaceMaterial(item.CurrentMaterial))
                        .Where(item => allowSameMaterial || item.CurrentMaterial != targetMaterial)
                        .ToList();

                    if (records.Count == 0)
                        continue;

                    result.Add(new PrefabAssetBatchRecord
                    {
                        Enabled = true,
                        Asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath),
                        AssetPath = prefabPath,
                        MatchedCount = records.Count,
                        WritableCount = records.Count(item => item.CanWrite),
                        ComponentSummary = BuildComponentSummary(records),
                        Preview = BuildRecordPreview(records, 4)
                    });
                }
                catch (Exception ex)
                {
                    warnings.Add($"{prefabPath}: 扫描失败，{ex.Message}");
                }
                finally
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return result;
        }

        private void ExecutePrefabAssetReplacement(List<PrefabAssetBatchRecord> entries)
        {
            int changedAssetCount = 0;
            int replacedReferenceCount = 0;
            int skippedReferenceCount = 0;
            var failedMessages = new List<string>();
            var scanWarnings = new List<string>();
            var changedAssetPaths = new List<string>();

            foreach (var entry in entries)
            {
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(entry.AssetPath);
                    var targets = CollectTargetsFromRoots(new[] { root });
                    var records = ScanMaterialReferences(targets, scanWarnings, "PrefabAsset", entry.AssetPath)
                        .Where(item => ShouldReplaceMaterial(item.CurrentMaterial))
                        .Where(item => allowSameMaterial || item.CurrentMaterial != targetMaterial)
                        .ToList();

                    int changedThisAsset = 0;
                    foreach (var record in records)
                    {
                        if (record == null || record.Accessor == null || !record.CanWrite)
                        {
                            skippedReferenceCount++;
                            continue;
                        }

                        var current = record.Accessor.Read();
                        if (!ShouldReplaceMaterial(current) || (!allowSameMaterial && current == targetMaterial))
                        {
                            skippedReferenceCount++;
                            continue;
                        }

                        if (record.Accessor.Write(targetMaterial, out var error))
                        {
                            changedThisAsset++;
                            replacedReferenceCount++;
                        }
                        else
                        {
                            failedMessages.Add($"{entry.AssetPath} | {record.TargetPath} | {record.ComponentType}.{record.Location}: {error}");
                        }
                    }

                    if (changedThisAsset > 0)
                    {
                        var saved = PrefabUtility.SaveAsPrefabAsset(root, entry.AssetPath);
                        if (saved == null)
                        {
                            failedMessages.Add($"{entry.AssetPath}: 保存 Prefab 资产失败。");
                            continue;
                        }

                        changedAssetCount++;
                        changedAssetPaths.Add(entry.AssetPath);
                    }
                }
                catch (Exception ex)
                {
                    failedMessages.Add($"{entry.AssetPath}: 替换失败，{ex.Message}");
                }
                finally
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            lastResultSummary = $"Prefab 资产替换完成: 已执行勾选资产 {entries.Count} 个 | 修改 {changedAssetCount} 个资产 | 替换 {replacedReferenceCount} 处引用 | 跳过 {skippedReferenceCount} 处 | 失败 {failedMessages.Count} 项";
            lastResultDetail = BuildPrefabExecutionReport(changedAssetPaths, failedMessages, scanWarnings);
            EditorUtility.DisplayDialog("Prefab 资产替换完成",
                $"已修改 {changedAssetCount} 个 Prefab 资产，替换 {replacedReferenceCount} 处材质引用。",
                "完成");
        }

        private List<GameObject> CollectTargets()
        {
            switch (targetScope)
            {
                case TargetScope.ActiveScene:
                    return GetActiveEditingScene()
                        .GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Transform>(includeInactive))
                        .Where(t => t != null)
                        .Select(t => t.gameObject)
                        .Distinct()
                        .ToList();

                case TargetScope.SelectedOnly:
                    return SimpleToolsSafetyUtility.CollectTargets(Selection.gameObjects, includeChildren: false, includeInactive: includeInactive);

                default:
                    return SimpleToolsSafetyUtility.CollectTargets(Selection.gameObjects, includeChildren: true, includeInactive: includeInactive);
            }
        }

        private UnityEngine.SceneManagement.Scene GetActiveEditingScene()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.scene.IsValid())
                return prefabStage.scene;

            return UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        }

        private string GetEditingSourceLabel()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.scene.IsValid())
                return "PrefabMode";

            return "Scene";
        }

        private List<GameObject> CollectTargetsFromRoots(IEnumerable<GameObject> roots)
        {
            var result = new List<GameObject>();
            var unique = new HashSet<GameObject>();
            if (roots == null)
                return result;

            foreach (var root in roots)
            {
                if (root == null)
                    continue;

                foreach (var transform in root.GetComponentsInChildren<Transform>(includeInactive))
                {
                    if (transform != null && transform.gameObject != null && unique.Add(transform.gameObject))
                        result.Add(transform.gameObject);
                }
            }

            return result;
        }

        private bool TryValidateScanSettings(out string message, bool requireTargetMaterial = true)
        {
            if (componentTypes == ComponentType.None)
            {
                message = "请至少选择一种要处理的组件类型。";
                return false;
            }

            if (requireTargetMaterial && targetMaterial == null)
            {
                message = "请先设置目标材质。";
                return false;
            }

            if (targetScope != TargetScope.ActiveScene && (Selection.gameObjects == null || Selection.gameObjects.Length == 0))
            {
                message = "请先在 Hierarchy 里选择要处理的 GameObject，或把目标范围切到当前场景全部对象。";
                return false;
            }

            if (replacementMode == ReplacementMode.ReplaceSpecific && sourceMaterial == null && !setDefaultWhenNull)
            {
                message = "指定材质模式下需要设置源材质。如果只是想填充空材质，请打开“空材质也填充”。";
                return false;
            }

            if (replacementMode == ReplacementMode.MatchByName && string.IsNullOrWhiteSpace(matchName))
            {
                message = "名称包含模式下，关键字不能为空。";
                return false;
            }

            if (replacementMode == ReplacementMode.MatchByRegex)
            {
                if (string.IsNullOrWhiteSpace(regexPattern))
                {
                    message = "正则匹配模式下，表达式不能为空。";
                    return false;
                }

                try
                {
                    _ = CreateRegex();
                }
                catch (Exception ex)
                {
                    message = "正则表达式无效: " + ex.Message;
                    return false;
                }
            }

            if (replacementMode == ReplacementMode.MatchByShader && sourceShader == null)
            {
                message = "Shader 匹配模式下需要设置源 Shader。";
                return false;
            }

            message = null;
            return true;
        }

        private bool ShouldReplaceMaterial(Material material)
        {
            if (material == null)
                return setDefaultWhenNull;

            switch (replacementMode)
            {
                case ReplacementMode.ReplaceSpecific:
                    return material == sourceMaterial;

                case ReplacementMode.ReplaceAll:
                    return true;

                case ReplacementMode.MatchByName:
                    return material.name.IndexOf(matchName, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) >= 0;

                case ReplacementMode.MatchByRegex:
                    return CreateRegex().IsMatch(material.name);

                case ReplacementMode.MatchByShader:
                    return material.shader == sourceShader;

                default:
                    return false;
            }
        }

        private bool IsMaterialReferenceProperty(SerializedProperty property)
        {
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                return false;

            if (property.objectReferenceValue is Material)
                return true;

            return setDefaultWhenNull &&
                   property.objectReferenceValue == null &&
                   property.type.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Regex CreateRegex()
        {
            var options = RegexOptions.CultureInvariant;
            if (ignoreCase)
                options |= RegexOptions.IgnoreCase;

            return new Regex(regexPattern, options, TimeSpan.FromMilliseconds(120));
        }

        private string BuildSettingsSignature()
        {
            return string.Join("|",
                targetScope,
                componentTypes,
                includeInactive,
                scanSerializedScriptFields,
                skipHideFlagsObjects,
                replacementMode,
                sourceMaterial != null ? sourceMaterial.GetInstanceID().ToString() : "null",
                matchName ?? string.Empty,
                ignoreCase,
                regexPattern ?? string.Empty,
                sourceShader != null ? sourceShader.GetInstanceID().ToString() : "null",
                setDefaultWhenNull,
                targetMaterial != null ? targetMaterial.GetInstanceID().ToString() : "null",
                allowSameMaterial);
        }

        private ComponentType DrawComponentMask(ComponentType value)
        {
            bool renderer = (value & ComponentType.Renderer) != 0;
            bool particle = (value & ComponentType.ParticleSystemRenderer) != 0;
            bool script = (value & ComponentType.MonoBehaviour) != 0;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("组件", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                renderer = GUILayout.Toggle(renderer, "Renderer", EditorStyles.miniButtonLeft, GUILayout.Height(22));
                particle = GUILayout.Toggle(particle, "ParticleRenderer", EditorStyles.miniButtonMid, GUILayout.Height(22));
                script = GUILayout.Toggle(script, "脚本字段", EditorStyles.miniButtonRight, GUILayout.Height(22));
            }

            var result = ComponentType.None;
            if (renderer) result |= ComponentType.Renderer;
            if (particle) result |= ComponentType.ParticleSystemRenderer;
            if (script) result |= ComponentType.MonoBehaviour;
            return result;
        }

        private bool IsPreviewStale(int count)
        {
            return count > 0 && previewSettingsSignature != BuildSettingsSignature();
        }

        private List<MaterialReferenceRecord> GetFilteredPreviewRecords(bool sorted)
        {
            IEnumerable<MaterialReferenceRecord> rows = replacementPreview.Where(PassesPreviewFilter);
            if (sorted)
                rows = SortPreviewRecords(rows);
            return rows.ToList();
        }

        private List<PrefabAssetBatchRecord> GetFilteredPrefabAssetRecords(bool sorted)
        {
            IEnumerable<PrefabAssetBatchRecord> rows = prefabAssetPreview.Where(PassesPrefabAssetFilter);
            if (sorted)
                rows = rows.OrderByDescending(r => r.WritableCount).ThenBy(r => r.AssetPath, StringComparer.Ordinal);
            return rows.ToList();
        }

        private bool PassesPreviewFilter(MaterialReferenceRecord record)
        {
            if (record == null)
                return false;

            if (materialWritableFilter == 1 && !record.CanWrite)
                return false;
            if (materialWritableFilter == 2 && record.CanWrite)
                return false;

            if (materialSourceFilter > 0)
            {
                string expected = SourceFilterLabels[materialSourceFilter];
                if (!string.Equals(record.Source, expected, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (string.IsNullOrWhiteSpace(materialSearch))
                return true;

            string keyword = materialSearch.Trim();
            return ContainsIgnoreCase(record.TargetPath, keyword) ||
                   ContainsIgnoreCase(record.ComponentType, keyword) ||
                   ContainsIgnoreCase(record.Location, keyword) ||
                   ContainsIgnoreCase(record.Source, keyword) ||
                   ContainsIgnoreCase(record.Risk, keyword) ||
                   ContainsIgnoreCase(GetMaterialName(record.CurrentMaterial), keyword) ||
                   ContainsIgnoreCase(GetMaterialName(record.TargetMaterial), keyword) ||
                   ContainsIgnoreCase(GetShaderName(record.CurrentMaterial), keyword) ||
                   ContainsIgnoreCase(GetShaderName(record.TargetMaterial), keyword) ||
                   ContainsIgnoreCase(record.AssetPath, keyword);
        }

        private bool PassesPrefabAssetFilter(PrefabAssetBatchRecord record)
        {
            if (record == null)
                return false;

            if (materialWritableFilter == 1 && record.WritableCount <= 0)
                return false;
            if (materialWritableFilter == 2 && record.WritableCount > 0)
                return false;

            if (string.IsNullOrWhiteSpace(materialSearch))
                return true;

            string keyword = materialSearch.Trim();
            return ContainsIgnoreCase(record.AssetPath, keyword) ||
                   ContainsIgnoreCase(record.ComponentSummary, keyword) ||
                   ContainsIgnoreCase(record.Preview, keyword);
        }

        private IEnumerable<MaterialReferenceRecord> SortPreviewRecords(IEnumerable<MaterialReferenceRecord> records)
        {
            switch (materialSortIndex)
            {
                case 1:
                    return records.OrderBy(r => GetMaterialName(r.CurrentMaterial), StringComparer.Ordinal).ThenBy(r => r.TargetPath, StringComparer.Ordinal);
                case 2:
                    return records.OrderBy(r => GetShaderName(r.CurrentMaterial), StringComparer.Ordinal).ThenBy(r => r.TargetPath, StringComparer.Ordinal);
                case 3:
                    return records.OrderBy(r => r.ComponentType, StringComparer.Ordinal).ThenBy(r => r.TargetPath, StringComparer.Ordinal);
                case 4:
                    return records.OrderByDescending(GetRecordRiskScore).ThenBy(r => r.TargetPath, StringComparer.Ordinal);
                default:
                    return records.OrderBy(r => r.TargetPath, StringComparer.Ordinal);
            }
        }

        private int GetRecordRiskScore(MaterialReferenceRecord record)
        {
            if (record == null)
                return 0;

            int score = 0;
            if (!record.CanWrite) score += 100;
            if (record.TargetComponent is MonoBehaviour) score += 40;
            if (record.TargetObject != null && PrefabUtility.IsPartOfPrefabInstance(record.TargetObject)) score += 25;
            if (record.CurrentMaterial == null) score += 10;
            return score;
        }

        private void EnableFilteredPreviewItems(bool enabled)
        {
            foreach (var record in GetFilteredPreviewRecords(false))
                record.Enabled = enabled;
        }

        private void SelectFilteredPreviewObjects()
        {
            var objects = GetFilteredPreviewRecords(true)
                .Select(r => r.TargetObject)
                .Where(obj => obj != null)
                .Distinct()
                .Cast<UnityEngine.Object>()
                .ToArray();

            if (objects.Length == 0)
            {
                lastResultSummary = "选中失败";
                lastResultDetail = "当前筛选条件下没有可选中的对象。";
                return;
            }

            Selection.objects = objects;
            EditorGUIUtility.PingObject(objects[0]);
            lastResultSummary = $"已选中命中对象: {objects.Length} 个";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(objects.OfType<GameObject>().Select(GetTransformPath), 12);
        }

        private void UseSelectionAsSourceMaterial()
        {
            if (Selection.activeObject is Material material)
            {
                sourceMaterial = material;
                replacementMode = ReplacementMode.ReplaceSpecific;
                lastResultSummary = "已设置源材质";
                lastResultDetail = material.name;
            }
        }

        private void UseSelectionAsTargetMaterial()
        {
            if (Selection.activeObject is Material material)
            {
                targetMaterial = material;
                lastResultSummary = "已设置目标材质";
                lastResultDetail = material.name;
            }
        }

        private void SwapSourceAndTargetMaterial()
        {
            var oldSource = sourceMaterial;
            sourceMaterial = targetMaterial;
            targetMaterial = oldSource;
            replacementMode = ReplacementMode.ReplaceSpecific;
            lastResultSummary = "已交换源材质和目标材质";
            lastResultDetail = $"源: {GetMaterialName(sourceMaterial)} | 目标: {GetMaterialName(targetMaterial)}";
        }

        private string BuildMaterialSummary(IEnumerable<MaterialReferenceRecord> records, int limit)
        {
            var list = records?.Where(r => r != null).ToList() ?? new List<MaterialReferenceRecord>();
            if (list.Count == 0)
                return "无";

            return string.Join("  |  ", list
                .GroupBy(r => GetMaterialName(r.CurrentMaterial))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(limit)
                .Select(g => $"{g.Key} {g.Count()}"));
        }

        private string BuildShaderSummary(IEnumerable<MaterialReferenceRecord> records, int limit)
        {
            var list = records?.Where(r => r != null).ToList() ?? new List<MaterialReferenceRecord>();
            if (list.Count == 0)
                return "无";

            return string.Join("  |  ", list
                .GroupBy(r => GetShaderName(r.CurrentMaterial))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(limit)
                .Select(g => $"{g.Key} {g.Count()}"));
        }

        private string BuildSourceSummary(IEnumerable<MaterialReferenceRecord> records)
        {
            var list = records?.Where(r => r != null).ToList() ?? new List<MaterialReferenceRecord>();
            if (list.Count == 0)
                return "无";

            return string.Join("  |  ", list.GroupBy(r => r.Source).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"));
        }

        private string BuildPrefabAssetComponentSummary(IEnumerable<PrefabAssetBatchRecord> records)
        {
            var list = records?.Where(r => r != null).ToList() ?? new List<PrefabAssetBatchRecord>();
            if (list.Count == 0)
                return "无";

            var totals = list
                .SelectMany(record => SplitSummary(record.ComponentSummary))
                .GroupBy(pair => pair.Key)
                .OrderByDescending(g => g.Sum(x => x.Value))
                .Select(g => $"{g.Key}:{g.Sum(x => x.Value)}");

            return string.Join("  |  ", totals);
        }

        private string GetMaterialName(Material material)
        {
            return material != null ? material.name : "<空>";
        }

        private string GetShaderName(Material material)
        {
            return material != null && material.shader != null ? material.shader.name : "<无Shader>";
        }

        private bool ContainsIgnoreCase(string source, string keyword)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
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
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(72));
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(value) ? "-" : value, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawPager(ref int pageIndex, int totalPages)
        {
            if (totalPages <= 1)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("上一页", EditorStyles.miniButtonLeft, GUILayout.Width(64)) && pageIndex > 0)
                    pageIndex--;
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"第 {pageIndex + 1} / {totalPages} 页", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(90));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("下一页", EditorStyles.miniButtonRight, GUILayout.Width(64)) && pageIndex < totalPages - 1)
                    pageIndex++;
            }
        }

        private string BuildRiskLabel(MaterialReferenceRecord record)
        {
            var risks = new List<string>();
            if (!record.CanWrite)
                risks.Add("不可写");

            if (record.TargetObject != null && PrefabUtility.IsPartOfPrefabInstance(record.TargetObject))
                risks.Add("Prefab实例覆盖");

            if (record.CurrentMaterial == null)
                risks.Add("空材质填充");

            if (record.TargetComponent is MonoBehaviour)
                risks.Add("脚本字段");

            return risks.Count == 0 ? "普通" : string.Join(" / ", risks);
        }

        private string BuildScanReport(int hitCount, IEnumerable<MaterialReferenceRecord> statsSource, List<string> warnings)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"扫描范围: {GetScopeName(targetScope)}");
            builder.AppendLine($"处理组件: {componentTypes}");
            builder.AppendLine($"匹配模式: {GetModeName(replacementMode)}");
            builder.AppendLine($"命中数量: {hitCount}");

            var previewStats = BuildRecordStats(statsSource);
            if (!string.IsNullOrWhiteSpace(previewStats))
            {
                builder.AppendLine(previewStats);
            }

            if (warnings != null && warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("扫描警告:");
                builder.Append(SimpleToolsSafetyUtility.JoinPreview(warnings, 12));
            }

            return builder.ToString();
        }

        private string BuildUsageReport(int hitCount, IEnumerable<MaterialUsage> usageSource, List<string> warnings)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"扫描范围: {GetScopeName(targetScope)}");
            builder.AppendLine($"处理组件: {componentTypes}");
            builder.AppendLine($"命中数量: {hitCount}");

            var list = usageSource?.Where(item => item != null).ToList() ?? new List<MaterialUsage>();
            if (list.Count > 0)
            {
                builder.AppendLine($"组件分布: {string.Join(", ", list.GroupBy(item => item.componentType).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}:{g.Count()}"))}");
            }

            if (warnings != null && warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("扫描警告:");
                builder.AppendLine(SimpleToolsSafetyUtility.JoinPreview(warnings, 12));
            }

            return builder.ToString();
        }

        private string BuildExecutionReport(IEnumerable<GameObject> changedObjects, List<string> failedMessages)
        {
            var builder = new StringBuilder();
            builder.AppendLine(BuildRecordStats(replacementPreview));
            builder.AppendLine();
            var changedList = changedObjects
                .Where(obj => obj != null)
                .Select(GetTransformPath)
                .OrderBy(path => path)
                .ToList();

            builder.AppendLine("修改对象:");
            builder.AppendLine(SimpleToolsSafetyUtility.JoinPreview(changedList, 18));

            if (failedMessages != null && failedMessages.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("失败项:");
                builder.AppendLine(SimpleToolsSafetyUtility.JoinPreview(failedMessages, 12));
            }

            return builder.ToString();
        }

        private string BuildRecordStats(IEnumerable<MaterialReferenceRecord> records)
        {
            var list = records?.Where(item => item != null).ToList() ?? new List<MaterialReferenceRecord>();
            if (list.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine($"来源分布: {string.Join(", ", list.GroupBy(item => item.Source).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}:{g.Count()}"))}");
            builder.AppendLine($"可写项: {list.Count(item => item.CanWrite)} | 不可写项: {list.Count(item => !item.CanWrite)} | Prefab实例: {list.Count(item => item.TargetObject != null && PrefabUtility.IsPartOfPrefabInstance(item.TargetObject))}");
            builder.AppendLine($"组件分布: {string.Join(", ", list.GroupBy(item => item.ComponentType).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}:{g.Count()}"))}");
            return builder.ToString().TrimEnd();
        }

        private string BuildPrefabBatchReport(IEnumerable<PrefabAssetBatchRecord> entries, List<string> warnings)
        {
            var list = entries?.ToList() ?? new List<PrefabAssetBatchRecord>();
            var builder = new StringBuilder();
            builder.AppendLine($"Prefab 资产: {list.Count}");
            builder.AppendLine($"命中引用: {list.Sum(item => item.MatchedCount)}");
            builder.AppendLine($"可写引用: {list.Sum(item => item.WritableCount)}");

            if (list.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("组件分布:");
                foreach (var group in list
                    .SelectMany(item => SplitSummary(item.ComponentSummary))
                    .GroupBy(item => item.Key)
                    .OrderByDescending(g => g.Sum(x => x.Value)))
                {
                    builder.AppendLine($"{group.Key}: {group.Sum(x => x.Value)}");
                }

                builder.AppendLine();
                builder.AppendLine("命中示例:");
                builder.AppendLine(SimpleToolsSafetyUtility.JoinPreview(list.Select(item => item.ToOneLine()), 12));
            }

            if (warnings != null && warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("扫描警告:");
                builder.AppendLine(SimpleToolsSafetyUtility.JoinPreview(warnings, 12));
            }

            return builder.ToString();
        }

        private string BuildPrefabExecutionReport(IEnumerable<string> changedAssetPaths, List<string> failedMessages, List<string> scanWarnings)
        {
            var builder = new StringBuilder();
            builder.AppendLine("修改的 Prefab 资产:");
            builder.AppendLine(SimpleToolsSafetyUtility.JoinPreview(changedAssetPaths, ReportExportLimit));

            if (scanWarnings != null && scanWarnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("扫描警告:");
                builder.AppendLine(SimpleToolsSafetyUtility.JoinPreview(scanWarnings, 20));
            }

            if (failedMessages != null && failedMessages.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("失败项:");
                builder.AppendLine(SimpleToolsSafetyUtility.JoinPreview(failedMessages, 20));
            }

            return builder.ToString();
        }

        private static IEnumerable<KeyValuePair<string, int>> SplitSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
                yield break;

            var parts = summary.Split(new[] { ',', '，', '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                var pair = trimmed.Split(':');
                if (pair.Length == 2 && int.TryParse(pair[1].Trim(), out var value))
                    yield return new KeyValuePair<string, int>(pair[0].Trim(), value);
            }
        }

        private string BuildComponentSummary(IEnumerable<MaterialReferenceRecord> records)
        {
            var summary = records
                .GroupBy(item => item.ComponentType)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Key}:{group.Count()}");

            return string.Join(", ", summary);
        }

        private string BuildRecordPreview(IEnumerable<MaterialReferenceRecord> records, int limit)
        {
            return SimpleToolsSafetyUtility.JoinPreview(records.Select(item =>
            {
                string current = item.CurrentMaterial != null ? item.CurrentMaterial.name : "<空>";
                string target = item.TargetMaterial != null ? item.TargetMaterial.name : "<未设置>";
                return $"{item.TargetPath} | {item.ComponentType}.{item.Location} | {current} -> {target}";
            }), limit);
        }

        private List<string> FindPrefabAssetPaths(List<string> warnings = null)
        {
            var result = new List<string>();

            if (useSelectedPrefabAssets)
            {
                var selectedPaths = Selection.objects
                    .Select(AssetDatabase.GetAssetPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToList();

                if (selectedPaths.Count == 0)
                    warnings?.Add("当前没有选中的 Prefab 资产，已回退到文件夹搜索。");
                else
                    result.AddRange(selectedPaths);
            }

            if (!useSelectedPrefabAssets || result.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(prefabAssetFolder) || !AssetDatabase.IsValidFolder(prefabAssetFolder))
                {
                    warnings?.Add("Prefab 搜索文件夹无效。");
                    return result;
                }

                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabAssetFolder });
                result.AddRange(guids.Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)));
            }

            return result.Distinct().ToList();
        }

        private bool TryValidatePrefabAssetBatchSettings(out string message)
        {
            if (!enablePrefabAssetBatch)
            {
                message = "请先打开“启用资产批处理”。";
                return false;
            }

            if (useSelectedPrefabAssets)
            {
                bool hasPrefabSelection = Selection.objects != null && Selection.objects.Any(obj =>
                {
                    var path = AssetDatabase.GetAssetPath(obj);
                    return !string.IsNullOrWhiteSpace(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
                });

                if (!hasPrefabSelection)
                {
                    message = "当前没有选中的 Prefab 资产。请选中 Project 里的 prefab，或者关闭“优先处理选中 Prefab 资产”改为文件夹扫描。";
                    return false;
                }
            }
            else if (string.IsNullOrWhiteSpace(prefabAssetFolder) || !AssetDatabase.IsValidFolder(prefabAssetFolder))
            {
                message = "Prefab 搜索文件夹无效。";
                return false;
            }

            message = null;
            return true;
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

        private string GetTransformPath(GameObject obj)
        {
            if (obj == null)
                return "<对象丢失>";

            var names = new Stack<string>();
            var current = obj.transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static string GetScopeName(TargetScope scope)
        {
            switch (scope)
            {
                case TargetScope.SelectedOnly:
                    return "只处理当前选中";
                case TargetScope.ActiveScene:
                    return "当前场景全部对象";
                default:
                    return "选中对象和子物体";
            }
        }

        private static string GetModeName(ReplacementMode mode)
        {
            switch (mode)
            {
                case ReplacementMode.ReplaceSpecific:
                    return "指定材质";
                case ReplacementMode.ReplaceAll:
                    return "全部非空材质";
                case ReplacementMode.MatchByName:
                    return "名称包含";
                case ReplacementMode.MatchByRegex:
                    return "正则匹配";
                case ReplacementMode.MatchByShader:
                    return "Shader 匹配";
                default:
                    return mode.ToString();
            }
        }
    }
    #endregion
}
