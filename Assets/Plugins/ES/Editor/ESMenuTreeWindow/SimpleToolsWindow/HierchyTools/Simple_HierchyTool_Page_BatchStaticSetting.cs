using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Animations;

// 抑制私有字段未使用警告
#pragma warning disable CS0414
// 抑制无法访问的代码警告（提前return）
#pragma warning disable CS0162

namespace ES
{

    #region 批量静态设置工具
    [Serializable]
    public class Page_BatchStaticSetting : ESWindowPageBase
    {
        [Title("批量静态设置工具", "批量设置GameObject的静态标记", bold: true, titleAlignment: TitleAlignments.Centered)]

        public enum StaticApplyMode
        {
            [LabelText("覆盖为当前勾选")] Override,
            [LabelText("只添加勾选项")] AddOnly,
            [LabelText("只移除勾选项")] RemoveOnly,
        }

        [Serializable]
        private class StaticPreviewRecord
        {
            [TableColumnWidth(55, false), LabelText("处理")]
            public bool Selected = true;

            [ReadOnly, TableColumnWidth(170, false), LabelText("对象")]
            public GameObject Object;

            [ReadOnly, TableColumnWidth(260, false), LabelText("路径")]
            public string Path;

            [ReadOnly, TableColumnWidth(160, false), LabelText("当前")]
            public string CurrentFlags;

            [ReadOnly, TableColumnWidth(160, false), LabelText("目标")]
            public string TargetFlags;

            [ReadOnly, TableColumnWidth(70, false), LabelText("变化")]
            public string ChangeState;

            [ReadOnly, TableColumnWidth(90, false), LabelText("Prefab")]
            public string PrefabState;

            [ReadOnly, TableColumnWidth(150, false), LabelText("提示")]
            public string Note;

            [Button("定位", ButtonSizes.Small), TableColumnWidth(48, false)]
            private void Ping()
            {
                if (Object == null) return;
                Selection.activeGameObject = Object;
                EditorGUIUtility.PingObject(Object);
            }
        }

        [FoldoutGroup("说明与风险"), InfoBox("用途：批量维护 Unity GameObject 的 Static Flags。\n" +
                 "这些标记会被 Unity 的静态批处理、光照烘焙、反射探针、遮挡剔除和旧导航流程读取。\n" +
                 "适合处理运行时不会移动、不会缩放、不会频繁启停的场景物体，例如建筑、地面、墙体、岩石、固定装饰。\n" +
                 "不适合角色、可移动平台、门、机关、运行时生成/回收对象，以及会在运行时改变 Transform 或 Renderer 状态的对象。", InfoMessageType.Info)]

        [FoldoutGroup("说明与风险"), InfoBox("风险：Static Flags 不是普通分类标签，错误标记会导致烘焙结果、遮挡剔除、合批和运行时表现不符合预期。\n" +
                 "• 覆盖模式会把未勾选的静态标记清掉。\n" +
                 "• 清除所有静态标记会移除目标对象参与烘焙、遮挡剔除和批处理的资格。\n" +
                 "• Prefab 实例会记录 Override；Project 中的 Prefab 资产默认会被保护过滤。\n" +
                 "建议先刷新预览，只勾选确认要处理的对象，再执行。", InfoMessageType.Warning)]

        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel, PropertyOrder(-10)]
        private string PanelSummary
        {
            get
            {
                int selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
                var targetInfo = CollectStaticTargets();
                return $"当前选择: {selectedCount} 个对象 | 实际目标: {targetInfo.Targets.Count} 个 | 过滤: {targetInfo.FilteredCount} 个 | 模式: {applyMode}";
            }
        }

        [InfoBox("标记含义：\n" +
                 "• 批处理静态：允许 Unity 对静止 Renderer 做静态批处理，通常用于降低 Draw Call，但会增加静态批处理相关内存占用。\n" +
                 "• 贡献全局光照：对象参与光照贴图/全局光照烘焙；错误开启会影响烘焙时间和光照结果。\n" +
                 "• 反射探针静态：对象参与反射探针烘焙和静态反射采样。\n" +
                 "• 遮挡剔除静态：对象可作为遮挡物；被遮挡物静态：对象可被遮挡剔除系统隐藏。\n" +
                 "• 导航静态(旧)：旧 Unity 导航静态标记，新项目通常由 NavMesh 工作流单独管理。", InfoMessageType.Info)]
        [LabelText("应用模式"), Space(5)]
        public StaticApplyMode applyMode = StaticApplyMode.Override;

        [LabelText("包含子对象"), Space(5)]
        public bool includeChildren = true;

        [LabelText("包含未激活对象")]
        public bool includeInactive = true;

        [LabelText("保护Prefab资产")]
        [InfoBox("开启后跳过 Project 窗口里选中的 Prefab 资产，只处理场景对象和 Prefab Mode 中的对象。")]
        public bool protectPrefabAssets = true;

        private static readonly Color EnabledColor = new Color(0.6f, 0.9f, 0.6f);
        private static readonly Color DisabledColor = new Color(0.8f, 0.8f, 0.8f);

        [Tooltip("参与全局光照和光照贴图烘焙。")]
        [LabelText("贡献全局光照"), GUIColor("@contributeGI ? EnabledColor : DisabledColor")]
        public bool contributeGI = false;

        [Tooltip("作为遮挡剔除系统中的静态遮挡物。")]
        [LabelText("遮挡剔除静态"), GUIColor("@occluderStatic ? EnabledColor : DisabledColor")]
        public bool occluderStatic = false;

        [Tooltip("作为遮挡剔除系统中的可被遮挡静态物。")]
        [LabelText("被遮挡物静态"), GUIColor("@occludeeStatic ? EnabledColor : DisabledColor")]
        public bool occludeeStatic = false;

        [Tooltip("允许 Unity 静态批处理减少渲染批次。")]
        [LabelText("批处理静态"), GUIColor("@batchingStatic ? EnabledColor : DisabledColor")]
        public bool batchingStatic = false;

        [Tooltip("兼容旧 Unity 导航静态标记。新项目通常由 NavMesh 工作流单独管理。")]
        [LabelText("导航静态(旧)"), GUIColor("@navigationStatic ? EnabledColor : DisabledColor")]
        public bool navigationStatic = false;

        [Tooltip("参与反射探针烘焙和静态反射采样。")]
        [LabelText("反射探针静态"), GUIColor("@reflectionProbeStatic ? EnabledColor : DisabledColor")]
        public bool reflectionProbeStatic = false;

        [HorizontalGroup("Presets")]
        [Tooltip("读取当前有效目标中第一个对象的 Static Flags，并同步到上方开关。不会修改场景。")]
        [Button("读取首个对象", ButtonHeight = 24)]
        private void LoadFlagsFromFirstSelected()
        {
            var targetInfo = CollectStaticTargets();
            var first = targetInfo.Targets.FirstOrDefault();
            if (first == null)
            {
                EditorUtility.DisplayDialog("没有可读取对象", "当前没有可读取的有效对象。", "知道了");
                return;
            }

            ApplyFlagsToToggles(GameObjectUtility.GetStaticEditorFlags(first));
            lastResultSummary = $"已读取: {GetObjectLabel(first)}";
            lastResultDetail = DescribeFlags(GameObjectUtility.GetStaticEditorFlags(first));
        }

        [HorizontalGroup("Presets")]
        [Tooltip("勾选批处理静态、被遮挡物静态、反射探针静态。适合不会移动的普通场景装饰和建筑件。")]
        [Button("减少DrawCall", ButtonHeight = 24)]
        private void PresetRenderingStatic()
        {
            contributeGI = false;
            occluderStatic = false;
            occludeeStatic = true;
            batchingStatic = true;
            navigationStatic = false;
            reflectionProbeStatic = true;
        }

        [HorizontalGroup("Presets")]
        [Tooltip("勾选贡献全局光照、批处理静态、被遮挡物静态、反射探针静态。适合需要参与光照贴图和反射烘焙的静态场景物体。")]
        [Button("准备光照烘焙", ButtonHeight = 24)]
        private void PresetLightBakeStatic()
        {
            contributeGI = true;
            occluderStatic = false;
            occludeeStatic = true;
            batchingStatic = true;
            navigationStatic = false;
            reflectionProbeStatic = true;
        }

        [HorizontalGroup("Presets")]
        [Tooltip("勾选遮挡剔除静态和被遮挡物静态。适合墙体、建筑块、室内结构等遮挡关系明确的物体。")]
        [Button("准备遮挡剔除", ButtonHeight = 24)]
        private void PresetOcclusionStatic()
        {
            contributeGI = false;
            occluderStatic = true;
            occludeeStatic = true;
            batchingStatic = false;
            navigationStatic = false;
            reflectionProbeStatic = false;
        }

        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private string lastAuditSummary = "";
        private string previewSearch = "";
        private bool onlyShowWillChange = false;
        private bool usePreviewSelection = true;
        private string previewSignature = "";
        private List<StaticPreviewRecord> previewRecords = new List<StaticPreviewRecord>();

        [OnInspectorGUI, PropertyOrder(-200)]
        private void DrawResultPanel()
        {
            var targetInfo = CollectStaticTargets();
            SimpleToolsPanelUtility.DrawToolHeader(
                "静态标记批处理工作台",
                "用于批量设置 GameObject Static Flags，适合布景、烘焙、遮挡剔除和静态批处理前的整理。",
                SimpleToolsMaturity.Upgrading,
                "Static Flags 会影响渲染、烘焙和遮挡结果。建议先刷新预览，只处理勾选项；Prefab 资产默认保护，场景实例会记录 Override。");
            SimpleToolsPanelUtility.DrawSummary(
                "选中对象: " + ((Selection.gameObjects != null) ? Selection.gameObjects.Length : 0),
                "实际目标: " + targetInfo.Targets.Count,
                "过滤: " + targetInfo.FilteredCount,
                "模式: " + applyMode,
                "预览: " + previewRecords.Count);
            DrawStaticActionPanel();
            SimpleToolsPanelUtility.DrawResultSummary("最近静态标记操作", lastResultSummary, lastResultDetail);
        }

        private void DrawStaticActionPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("预览与执行", "刷新预览后可以勾选目标；执行时会检查预览是否仍匹配当前选区。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("刷新预览", SimpleToolsActionTone.Primary, 30, GUILayout.MinWidth(96)))
                        RefreshStaticPreview();
                    if (SimpleToolsPanelUtility.DrawActionButton("全选", SimpleToolsActionTone.Neutral, 30, GUILayout.Width(64)))
                        SelectAllPreviewRecords();
                    if (SimpleToolsPanelUtility.DrawActionButton("只选变化", SimpleToolsActionTone.Neutral, 30, GUILayout.Width(82)))
                        SelectChangedPreviewRecords();
                    if (SimpleToolsPanelUtility.DrawActionButton("清空选择", SimpleToolsActionTone.Neutral, 30, GUILayout.Width(82)))
                        ClearPreviewSelection();
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("应用静态设置", SimpleToolsActionTone.Warning, 34, GUILayout.MinWidth(120)))
                        ApplyStaticSettings();
                    if (SimpleToolsPanelUtility.DrawActionButton("清除所有静态", SimpleToolsActionTone.Danger, 34, GUILayout.MinWidth(120)))
                        ClearAllStaticFlags();
                    if (SimpleToolsPanelUtility.DrawActionButton("重置勾选", SimpleToolsActionTone.Neutral, 34, GUILayout.Width(90)))
                        ResetStaticSettings();
                    GUILayout.FlexibleSpace();
                }
            }
        }

        [HorizontalGroup("PreviewFilters")]
        [LabelText("预览搜索"), LabelWidth(70)]
        public string PreviewSearch
        {
            get => previewSearch;
            set => previewSearch = value ?? "";
        }

        [HorizontalGroup("PreviewFilters")]
        [LabelText("只看变化"), LabelWidth(70)]
        public bool OnlyShowWillChange
        {
            get => onlyShowWillChange;
            set => onlyShowWillChange = value;
        }

        [HorizontalGroup("PreviewFilters")]
        [LabelText("只处理勾选"), LabelWidth(80)]
        [InfoBox("开启后，应用/清除只处理预览表中勾选的对象；没有刷新预览时会处理当前有效目标。", InfoMessageType.None)]
        public bool UsePreviewSelection
        {
            get => usePreviewSelection;
            set => usePreviewSelection = value;
        }

        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel]
        private string PreviewSummary => BuildPreviewSummary();

        [ShowInInspector, TableList(IsReadOnly = false, AlwaysExpanded = true), LabelText("静态标记预览")]
        private List<StaticPreviewRecord> FilteredPreviewRecords => GetFilteredPreviewRecords();

        public void RefreshStaticPreview()
        {
            var targetInfo = CollectStaticTargets();
            RebuildPreviewRecords(targetInfo.Targets);
            previewSignature = BuildPreviewSignature(targetInfo.Targets);
            lastAuditSummary = BuildStaticAudit(targetInfo.Targets, targetInfo.FilteredCount);
            lastResultSummary = $"预览完成: 目标 {targetInfo.Targets.Count} 个 | 过滤 {targetInfo.FilteredCount} 个";
            lastResultDetail = lastAuditSummary;
        }

        private void SelectAllPreviewRecords()
        {
            foreach (var record in previewRecords)
                record.Selected = true;
        }

        private void SelectChangedPreviewRecords()
        {
            foreach (var record in previewRecords)
                record.Selected = record.ChangeState == "会变";
        }

        private void ClearPreviewSelection()
        {
            foreach (var record in previewRecords)
                record.Selected = false;
        }

        public void ApplyStaticSettings()
        {
            var targetInfo = CollectStaticTargets();
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("需要选择对象", "先在层级窗口选中要处理的 GameObject。", "知道了");
                return;
            }

            if (!ValidatePreviewFresh(targetInfo.Targets))
                return;

            var allObjects = ResolveOperationTargets(targetInfo.Targets);
            if (allObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("没有可处理对象", "当前没有可处理对象。请检查 Prefab 资产保护、预览勾选项、子对象和未激活对象设置。", "知道了");
                return;
            }

            StaticEditorFlags selectedFlags = ComposeSelectedFlags();
            if (applyMode != StaticApplyMode.Override && selectedFlags == 0)
            {
                EditorUtility.DisplayDialog("没有勾选标记", "添加/移除模式下，请至少勾选一个要处理的 Static Flag。", "知道了");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(allObjects.Select(GetObjectLabel), 10);
            string modeHint = GetApplyModeHint(selectedFlags);
            if (!EditorUtility.DisplayDialog("确认应用静态标记",
                $"用途: {GetPurposeHint(selectedFlags)}\n风险: {GetRiskHint(selectedFlags)}\n\n将处理 {allObjects.Count} 个对象的 Static Flags。\n过滤对象: {targetInfo.FilteredCount} 个\n模式: {modeHint}\n\n{preview}\n\n支持 Ctrl+Z 撤销。继续吗？",
                "开始应用", "取消"))
                return;

            Undo.RecordObjects(allObjects.ToArray(), "Batch Static Setting");

            int changedCount = 0;
            var changedObjects = new List<GameObject>();
            var beforeFlags = CaptureStaticFlags(allObjects);
            foreach (var obj in allObjects)
            {
                StaticEditorFlags oldFlags = GameObjectUtility.GetStaticEditorFlags(obj);
                StaticEditorFlags flags = ApplyStaticFlagMode(oldFlags, selectedFlags);

                if (oldFlags != flags)
                {
                    GameObjectUtility.SetStaticEditorFlags(obj, flags);
                    FinalizeStaticObject(obj);
                    changedCount++;
                    changedObjects.Add(obj);
                }
            }

            MarkScenesDirty(changedObjects);
            lastResultSummary = $"应用完成: 检查 {allObjects.Count} 个对象 | 实际修改 {changedCount} 个";
            lastResultDetail = BuildChangeReport(changedObjects, beforeFlags);
            EditorUtility.DisplayDialog("静态标记已应用", $"检查 {allObjects.Count} 个对象，实际修改 {changedCount} 个。", "完成");
        }

        public void ResetStaticSettings()
        {
            contributeGI = false;
            occluderStatic = false;
            occludeeStatic = false;
            batchingStatic = false;
            navigationStatic = false;
            reflectionProbeStatic = false;
        }

        public void ClearAllStaticFlags()
        {
            var targetInfo = CollectStaticTargets();
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            if (!ValidatePreviewFresh(targetInfo.Targets))
                return;

            var allObjects = ResolveOperationTargets(targetInfo.Targets);
            if (allObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("无需清除", "当前选区没有可处理对象。", "知道了");
                return;
            }

            var targets = allObjects.Where(obj => GameObjectUtility.GetStaticEditorFlags(obj) != 0).ToList();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("无需清除", "当前选区没有带静态标记的对象。", "知道了");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(targets.Select(GetObjectLabel), 10);
            if (!EditorUtility.DisplayDialog("确认清除静态标记",
                $"将清除 {targets.Count} 个对象的所有 Static Flags。\n风险: 目标对象将不再作为静态批处理、光照烘焙、反射探针、遮挡剔除或旧导航静态对象参与对应流程。\n过滤对象: {targetInfo.FilteredCount} 个\n\n{preview}\n\n支持 Ctrl+Z 撤销。继续吗？",
                "开始清除", "取消"))
                return;

            Undo.RecordObjects(targets.ToArray(), "Clear Static Flags");

            int changedCount = 0;
            var beforeFlags = CaptureStaticFlags(targets);
            foreach (var obj in targets)
            {
                if (GameObjectUtility.GetStaticEditorFlags(obj) != 0)
                {
                    GameObjectUtility.SetStaticEditorFlags(obj, 0);
                    FinalizeStaticObject(obj);
                    changedCount++;
                }
            }

            MarkScenesDirty(targets);
            lastResultSummary = $"清除完成: 检查 {allObjects.Count} 个对象 | 实际清除 {changedCount} 个";
            lastResultDetail = BuildChangeReport(targets, beforeFlags);
            EditorUtility.DisplayDialog("静态标记已清除", $"检查 {allObjects.Count} 个对象，实际清除 {changedCount} 个。", "完成");
        }

        private struct StaticTargetInfo
        {
            public List<GameObject> Targets;
            public int FilteredCount;
        }

        private StaticTargetInfo CollectStaticTargets()
        {
            var rawSelection = Selection.gameObjects ?? Array.Empty<GameObject>();
            var collected = SimpleToolsSafetyUtility.CollectTargets(rawSelection, includeChildren, includeInactive);
            var filtered = new List<GameObject>();

            foreach (var obj in collected)
            {
                if (obj == null)
                    continue;

                if (protectPrefabAssets && PrefabUtility.IsPartOfPrefabAsset(obj))
                    continue;

                filtered.Add(obj);
            }

            return new StaticTargetInfo
            {
                Targets = filtered,
                FilteredCount = Mathf.Max(0, collected.Count - filtered.Count)
            };
        }

        private List<GameObject> ResolveOperationTargets(List<GameObject> currentTargets)
        {
            if (currentTargets == null || currentTargets.Count == 0)
                return new List<GameObject>();

            if (!usePreviewSelection || previewRecords.Count == 0)
                return currentTargets;

            var currentSet = new HashSet<GameObject>(currentTargets);
            return previewRecords
                .Where(record => record != null && record.Selected && record.Object != null && currentSet.Contains(record.Object))
                .Select(record => record.Object)
                .Distinct()
                .ToList();
        }

        private bool ValidatePreviewFresh(List<GameObject> currentTargets)
        {
            if (!usePreviewSelection || previewRecords.Count == 0)
                return true;

            string currentSignature = BuildPreviewSignature(currentTargets);
            if (previewSignature == currentSignature)
                return true;

            EditorUtility.DisplayDialog("预览已过期",
                "当前选区或静态标记设置已经变化，预览表不再可信。\n\n请点击“刷新选区预览”后再执行。",
                "知道了");
            return false;
        }

        private string BuildPreviewSignature(List<GameObject> targets)
        {
            string targetIds = string.Join(",", (targets ?? new List<GameObject>())
                .Where(obj => obj != null)
                .Select(obj => obj.GetInstanceID())
                .OrderBy(id => id));

            return $"{applyMode}|{ComposeSelectedFlags()}|{includeChildren}|{includeInactive}|{protectPrefabAssets}|{targetIds}";
        }

        private void RebuildPreviewRecords(List<GameObject> targets)
        {
            previewRecords.Clear();
            StaticEditorFlags selectedFlags = ComposeSelectedFlags();

            foreach (var obj in targets ?? new List<GameObject>())
            {
                if (obj == null)
                    continue;

                StaticEditorFlags current = GameObjectUtility.GetStaticEditorFlags(obj);
                StaticEditorFlags target = ApplyStaticFlagMode(current, selectedFlags);
                bool willChange = current != target;
                bool prefabInstance = PrefabUtility.IsPartOfPrefabInstance(obj);
                bool prefabAsset = PrefabUtility.IsPartOfPrefabAsset(obj);

                var notes = new List<string>();
                if (prefabInstance) notes.Add("会写入Prefab Override");
                if (prefabAsset) notes.Add("Prefab资产");
                if (applyMode == StaticApplyMode.Override && selectedFlags == 0 && current != 0) notes.Add("覆盖后会清空");

                previewRecords.Add(new StaticPreviewRecord
                {
                    Selected = true,
                    Object = obj,
                    Path = GetObjectLabel(obj),
                    CurrentFlags = DescribeFlags(current),
                    TargetFlags = DescribeFlags(target),
                    ChangeState = willChange ? "会变" : "不变",
                    PrefabState = prefabInstance ? "实例" : prefabAsset ? "资产" : "场景",
                    Note = notes.Count == 0 ? "可处理" : string.Join("；", notes)
                });
            }
        }

        private List<StaticPreviewRecord> GetFilteredPreviewRecords()
        {
            IEnumerable<StaticPreviewRecord> query = previewRecords;
            if (onlyShowWillChange)
                query = query.Where(record => record.ChangeState == "会变");

            if (!string.IsNullOrWhiteSpace(previewSearch))
            {
                string keyword = previewSearch.Trim();
                query = query.Where(record =>
                    ContainsIgnoreCase(record.Object != null ? record.Object.name : "", keyword) ||
                    ContainsIgnoreCase(record.Path, keyword) ||
                    ContainsIgnoreCase(record.CurrentFlags, keyword) ||
                    ContainsIgnoreCase(record.TargetFlags, keyword) ||
                    ContainsIgnoreCase(record.PrefabState, keyword) ||
                    ContainsIgnoreCase(record.Note, keyword));
            }

            return query.ToList();
        }

        private string BuildPreviewSummary()
        {
            if (previewRecords.Count == 0)
                return "尚未生成预览。点击“刷新选区预览”查看目标和变化。";

            int selected = previewRecords.Count(record => record.Selected);
            int willChange = previewRecords.Count(record => record.ChangeState == "会变");
            int prefab = previewRecords.Count(record => record.PrefabState == "实例");
            return $"预览 {previewRecords.Count} | 勾选 {selected} | 会变化 {willChange} | Prefab实例 {prefab} | 当前显示 {GetFilteredPreviewRecords().Count}";
        }

        private bool ContainsIgnoreCase(string source, string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return true;
            return (source ?? "").IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private StaticEditorFlags ComposeSelectedFlags()
        {
            StaticEditorFlags flags = 0;
            if (contributeGI) flags |= StaticEditorFlags.ContributeGI;
            if (occluderStatic) flags |= StaticEditorFlags.OccluderStatic;
            if (occludeeStatic) flags |= StaticEditorFlags.OccludeeStatic;
            if (batchingStatic) flags |= StaticEditorFlags.BatchingStatic;
#pragma warning disable CS0618
            if (navigationStatic) flags |= StaticEditorFlags.NavigationStatic;
#pragma warning restore CS0618
            if (reflectionProbeStatic) flags |= StaticEditorFlags.ReflectionProbeStatic;
            return flags;
        }

        private StaticEditorFlags ApplyStaticFlagMode(StaticEditorFlags oldFlags, StaticEditorFlags selectedFlags)
        {
            switch (applyMode)
            {
                case StaticApplyMode.AddOnly:
                    return oldFlags | selectedFlags;
                case StaticApplyMode.RemoveOnly:
                    return oldFlags & ~selectedFlags;
                case StaticApplyMode.Override:
                default:
                    return selectedFlags;
            }
        }

        private void ApplyFlagsToToggles(StaticEditorFlags flags)
        {
            contributeGI = (flags & StaticEditorFlags.ContributeGI) != 0;
            occluderStatic = (flags & StaticEditorFlags.OccluderStatic) != 0;
            occludeeStatic = (flags & StaticEditorFlags.OccludeeStatic) != 0;
            batchingStatic = (flags & StaticEditorFlags.BatchingStatic) != 0;
#pragma warning disable CS0618
            navigationStatic = (flags & StaticEditorFlags.NavigationStatic) != 0;
#pragma warning restore CS0618
            reflectionProbeStatic = (flags & StaticEditorFlags.ReflectionProbeStatic) != 0;
        }

        private Dictionary<GameObject, StaticEditorFlags> CaptureStaticFlags(IEnumerable<GameObject> objects)
        {
            var result = new Dictionary<GameObject, StaticEditorFlags>();
            if (objects == null)
                return result;

            foreach (var obj in objects)
            {
                if (obj != null)
                    result[obj] = GameObjectUtility.GetStaticEditorFlags(obj);
            }
            return result;
        }

        private string BuildStaticAudit(List<GameObject> targets, int filteredCount)
        {
            if (targets == null || targets.Count == 0)
                return filteredCount > 0 ? $"没有可处理对象。已过滤 {filteredCount} 个。" : "没有可处理对象。";

            int staticCount = targets.Count(obj => GameObjectUtility.GetStaticEditorFlags(obj) != 0);
            int prefabInstanceCount = targets.Count(PrefabUtility.IsPartOfPrefabInstance);
            var grouped = targets
                .GroupBy(obj => GameObjectUtility.GetStaticEditorFlags(obj))
                .OrderByDescending(group => group.Count())
                .Take(8)
                .Select(group => $"{DescribeFlags(group.Key)}: {group.Count()}");

            return $"目标 {targets.Count} | 已有静态标记 {staticCount} | Prefab实例 {prefabInstanceCount} | 过滤 {filteredCount}\n\n分布:\n" +
                   string.Join("\n", grouped);
        }

        private string BuildChangeReport(List<GameObject> changedObjects, Dictionary<GameObject, StaticEditorFlags> beforeFlags)
        {
            if (changedObjects == null || changedObjects.Count == 0)
                return "无对象发生变化。";

            var lines = new List<string>();
            foreach (var obj in changedObjects.Take(12))
            {
                StaticEditorFlags before = beforeFlags != null && beforeFlags.TryGetValue(obj, out var captured) ? captured : 0;
                StaticEditorFlags after = GameObjectUtility.GetStaticEditorFlags(obj);
                lines.Add($"{GetObjectLabel(obj)} | {DescribeFlags(before)} -> {DescribeFlags(after)}");
            }

            return $"变更对象: {changedObjects.Count}\n" + string.Join("\n", lines) +
                   (changedObjects.Count > 12 ? "\n..." : "");
        }

        private string GetApplyModeHint(StaticEditorFlags selectedFlags)
        {
            string selected = DescribeFlags(selectedFlags);
            switch (applyMode)
            {
                case StaticApplyMode.AddOnly:
                    return $"只添加 [{selected}]";
                case StaticApplyMode.RemoveOnly:
                    return $"只移除 [{selected}]";
                case StaticApplyMode.Override:
                default:
                    return $"覆盖为 [{selected}]";
            }
        }

        private string GetPurposeHint(StaticEditorFlags selectedFlags)
        {
            if (applyMode == StaticApplyMode.RemoveOnly)
                return "从目标对象上移除指定 Static Flags，常用于修正被误标为静态的动态对象。";

            if (selectedFlags == 0)
                return "目标 Static Flags 为空，覆盖后等同于让对象退出 Unity 相关静态流程。";

            var purposes = new List<string>();
            if ((selectedFlags & StaticEditorFlags.BatchingStatic) != 0)
                purposes.Add("允许静态批处理系统处理 Renderer");
            if ((selectedFlags & StaticEditorFlags.ContributeGI) != 0)
                purposes.Add("参与光照贴图/全局光照烘焙");
            if ((selectedFlags & StaticEditorFlags.ReflectionProbeStatic) != 0)
                purposes.Add("参与反射探针烘焙");
            if ((selectedFlags & StaticEditorFlags.OccluderStatic) != 0 || (selectedFlags & StaticEditorFlags.OccludeeStatic) != 0)
                purposes.Add("参与遮挡剔除数据构建");
#pragma warning disable CS0618
            if ((selectedFlags & StaticEditorFlags.NavigationStatic) != 0)
                purposes.Add("兼容旧版导航静态标记");
#pragma warning restore CS0618

            return purposes.Count == 0 ? "设置 Unity Static Flags。" : string.Join("；", purposes);
        }

        private string GetRiskHint(StaticEditorFlags selectedFlags)
        {
            if (applyMode == StaticApplyMode.Override)
            {
                if (selectedFlags == 0)
                    return "覆盖为空会清掉所有已管理的 Static Flags。";

                return "覆盖模式会移除未勾选的已管理 Static Flags；如果对象已有其他静态用途，请先看预览。";
            }

            if (applyMode == StaticApplyMode.RemoveOnly)
                return "只移除勾选项，不会影响未勾选的 Static Flags；但被移除的系统将不再读取这些对象。";

            return "只添加勾选项，不会清掉现有 Static Flags；但误加到动态对象仍可能造成烘焙或剔除结果错误。";
        }

        private string DescribeFlags(StaticEditorFlags flags)
        {
            if (flags == 0)
                return "无";

            var names = new List<string>();
            if ((flags & StaticEditorFlags.ContributeGI) != 0) names.Add("GI");
            if ((flags & StaticEditorFlags.OccluderStatic) != 0) names.Add("遮挡");
            if ((flags & StaticEditorFlags.OccludeeStatic) != 0) names.Add("被遮挡");
            if ((flags & StaticEditorFlags.BatchingStatic) != 0) names.Add("批处理");
#pragma warning disable CS0618
            if ((flags & StaticEditorFlags.NavigationStatic) != 0) names.Add("导航");
#pragma warning restore CS0618
            if ((flags & StaticEditorFlags.ReflectionProbeStatic) != 0) names.Add("反射");

            var knownFlags = GetKnownManagedFlags();
            var otherFlags = flags & ~knownFlags;
            if (otherFlags != 0)
                names.Add("其他:" + otherFlags);

            return string.Join("/", names);
        }

        private StaticEditorFlags GetKnownManagedFlags()
        {
            StaticEditorFlags flags = StaticEditorFlags.ContributeGI |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic |
                                      StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.ReflectionProbeStatic;
#pragma warning disable CS0618
            flags |= StaticEditorFlags.NavigationStatic;
#pragma warning restore CS0618
            return flags;
        }

        private string GetObjectLabel(GameObject obj)
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

        private void FinalizeStaticObject(GameObject obj)
        {
            if (obj == null)
                return;

            EditorUtility.SetDirty(obj);
            if (PrefabUtility.IsPartOfPrefabInstance(obj))
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
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
    }
    #endregion

}
