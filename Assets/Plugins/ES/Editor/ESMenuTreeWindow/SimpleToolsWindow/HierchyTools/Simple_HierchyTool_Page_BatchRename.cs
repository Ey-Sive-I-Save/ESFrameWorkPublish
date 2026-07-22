using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ES
{
    #region 批量重命名工具
    [Serializable]
    public class Page_BatchRename : ESWindowPageBase
    {
        [Title("批量重命名工具", "批量重命名选中的 GameObject", bold: true, titleAlignment: TitleAlignments.Centered)]
        [HideInInspector]
        public string readMe = "选择层级中的 GameObject，设置重命名规则，刷新预览后执行。";

        public enum RenameMode
        {
            [LabelText("前缀模式")] Prefix,
            [LabelText("后缀模式")] Suffix,
            [LabelText("替换模式")] Replace,
            [LabelText("编号模式")] Number
        }

        public enum RenameSortMode
        {
            [LabelText("当前选区顺序")] SelectionOrder,
            [LabelText("Hierarchy 路径")] HierarchyPath,
            [LabelText("对象名称")] Name
        }

        [Serializable]
        private class RenamePreviewRecord
        {
            [TableColumnWidth(48, false), LabelText("执行")]
            public bool Selected = true;

            [ReadOnly, TableColumnWidth(150, false), LabelText("对象")]
            public GameObject Object;

            [ReadOnly, TableColumnWidth(260, false), LabelText("路径")]
            public string Path;

            [ReadOnly, TableColumnWidth(140, false), LabelText("原名")]
            public string OriginalName;

            [ReadOnly, TableColumnWidth(160, false), LabelText("新名")]
            public string NewName;

            [ReadOnly, TableColumnWidth(70, false), LabelText("状态")]
            public string State;

            [ReadOnly, TableColumnWidth(150, false), LabelText("提示")]
            public string Note;

            [Button("定位", ButtonSizes.Small), TableColumnWidth(48, false)]
            private void Ping()
            {
                if (Object == null)
                    return;

                Selection.activeGameObject = Object;
                EditorGUIUtility.PingObject(Object);
            }
        }

        [Serializable]
        private class RenameRuleSnapshot
        {
            public string Label;
            public string SavedAt;
            public RenameMode Mode;
            public RenameSortMode SortMode;
            public bool ProtectPrefabAssets;
            public string PrefixText;
            public string SuffixText;
            public string FindText;
            public string ReplaceText;
            public bool ReplaceCaseSensitive;
            public string BaseName;
            public string NumberSeparator;
            public int StartNumber;
            public int NumberDigits;
        }

        [Serializable]
        private class RenameRuleHistoryStore
        {
            public List<RenameRuleSnapshot> Items = new List<RenameRuleSnapshot>();
        }

        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel, PropertyOrder(-10)]
        private string PanelSummary
        {
            get
            {
                int selected = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
                int checkedCount = renamePlan.Count(item => item.Selected && item.State == "会改");
                int conflictCount = renamePlan.Count(item => item.Note.Contains("冲突"));
                return $"当前选择: {selected} 个对象 | 模式: {renameMode} | 预览: {renamePlan.Count} | 勾选会改: {checkedCount} | 冲突改名: {conflictCount}";
            }
        }

        [FoldoutGroup("1. 命名规则", Expanded = true), InfoBox("@RenameModeInfo", InfoMessageType.Info)]
        [FoldoutGroup("1. 命名规则"), LabelText("重命名模式")]
        public RenameMode renameMode = RenameMode.Prefix;

        [FoldoutGroup("1. 命名规则"), LabelText("排序方式")]
        public RenameSortMode sortMode = RenameSortMode.HierarchyPath;

        [FoldoutGroup("1. 命名规则"), LabelText("保护 Prefab 资产")]
        [InfoBox("开启后跳过 Project 窗口里选中的 Prefab 资产，只处理场景对象和 Prefab Mode 中的对象。", InfoMessageType.Info)]
        public bool protectPrefabAssets = true;

        [FoldoutGroup("1. 命名规则"), LabelText("前缀文本"), ShowIf("renameMode", RenameMode.Prefix)]
        public string prefixText = "New_";

        [FoldoutGroup("1. 命名规则"), LabelText("后缀文本"), ShowIf("renameMode", RenameMode.Suffix)]
        public string suffixText = "_Copy";

        [FoldoutGroup("1. 命名规则"), LabelText("查找文本"), ShowIf("renameMode", RenameMode.Replace)]
        public string findText = "Old";

        [FoldoutGroup("1. 命名规则"), LabelText("替换文本"), ShowIf("renameMode", RenameMode.Replace)]
        public string replaceText = "New";

        [FoldoutGroup("1. 命名规则"), LabelText("区分大小写"), ShowIf("renameMode", RenameMode.Replace)]
        public bool replaceCaseSensitive = true;

        [FoldoutGroup("1. 命名规则"), LabelText("基础名称"), ShowIf("renameMode", RenameMode.Number)]
        public string baseName = "Object";

        [FoldoutGroup("1. 命名规则"), LabelText("编号分隔符"), ShowIf("renameMode", RenameMode.Number)]
        public string numberSeparator = "_";

        [FoldoutGroup("1. 命名规则"), LabelText("起始编号"), ShowIf("renameMode", RenameMode.Number)]
        public int startNumber = 1;

        [FoldoutGroup("1. 命名规则"), LabelText("编号位数"), Range(1, 8), ShowIf("renameMode", RenameMode.Number)]
        public int numberDigits = 3;

        [FoldoutGroup("2. 详细预览", Expanded = false), HorizontalGroup("2. 详细预览/Filters"), LabelText("搜索"), LabelWidth(40)]
        public string previewSearch = "";

        [FoldoutGroup("2. 详细预览"), HorizontalGroup("2. 详细预览/Filters"), LabelText("只看会改"), LabelWidth(70)]
        public bool onlyShowChanged = false;

        [FoldoutGroup("2. 详细预览"), HorizontalGroup("2. 详细预览/Filters"), LabelText("只执行勾选"), LabelWidth(85)]
        public bool executeCheckedOnly = true;

        [FoldoutGroup("2. 详细预览"), HorizontalGroup("2. 详细预览/Page"), LabelText("每页数量"), LabelWidth(60)]
        public int previewPageSize = SimpleToolsPanelUtility.DefaultPageSize;

        [FoldoutGroup("2. 详细预览"), ShowInInspector, ReadOnly, DisplayAsString, HideLabel]
        private string PreviewSummary => BuildPreviewSummary();

        [FoldoutGroup("2. 详细预览"), OnInspectorGUI]
        private void DrawPreviewPager()
        {
            previewPageSize = Mathf.Clamp(previewPageSize, 10, 200);
            int filteredCount = GetFilteredRenameQuery().Count();
            SimpleToolsPanelUtility.DrawPager(ref previewPageIndex, filteredCount, previewPageSize);
        }

        [FoldoutGroup("2. 详细预览"), ShowInInspector, TableList(IsReadOnly = false, AlwaysExpanded = true), LabelText("重命名计划")]
        private List<RenamePreviewRecord> FilteredRenamePlan => GetFilteredRenamePlan();

        private readonly List<RenamePreviewRecord> renamePlan = new List<RenamePreviewRecord>();
        private static readonly List<RenameRuleSnapshot> renameRuleHistory = new List<RenameRuleSnapshot>();
        private const string RenameRuleHistoryPrefsKey = "ES.SimpleTools.BatchRename.RuleHistory";
        private const int MaxRenameRuleHistory = 8;
        private static bool renameRuleHistoryLoaded;
        private string previewSignature = "";
        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private int previewPageIndex;

        private string RenameModeInfo
        {
            get
            {
                switch (renameMode)
                {
                    case RenameMode.Prefix:
                        return "前缀模式：在现有名称前添加指定前缀。例如 Cube -> New_Cube。";
                    case RenameMode.Suffix:
                        return "后缀模式：在现有名称后添加指定后缀。例如 Cube -> Cube_Copy。";
                    case RenameMode.Replace:
                        return "替换模式：把名称中匹配的文本替换为新文本。空查找文本不会执行。";
                    case RenameMode.Number:
                        return "编号模式：按当前排序生成 Base_001、Base_002 这类连续名称。";
                    default:
                        return string.Empty;
                }
            }
        }

        [OnInspectorGUI, PropertyOrder(-200)]
        private void DrawRenameWorkbench()
        {
            int selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
            SimpleToolsPanelUtility.DrawToolHeader(
                "批量重命名工作台",
                "选中对象，填一个命名规则，然后直接执行；需要逐项复核时再打开详细预览。",
                SimpleToolsMaturity.Upgrading,
                "会直接修改场景对象名称。预览和执行共用同一份计划，支持冲突自动改名、勾选执行、Undo 和场景 Dirty。");
            SimpleToolsPanelUtility.DrawLargeListGuard(selectedCount, "选中对象");
            SimpleToolsPanelUtility.DrawSummary(
                "选中对象: " + selectedCount,
                "示例: " + BuildQuickExampleName(),
                "排序: " + sortMode,
                "Prefab资产保护: " + (protectPrefabAssets ? "开" : "关"),
                "计划项: " + renamePlan.Count,
                "会修改: " + renamePlan.Count(item => item.State == "会改"),
                "勾选执行: " + renamePlan.Count(item => item.Selected && item.State == "会改"));
            DrawRenameActionPanel();
            DrawRenameHistoryPanel();
            SimpleToolsPanelUtility.DrawResultSummary("最近重命名结果", lastResultSummary, lastResultDetail);
        }

        private void DrawRenameActionPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("2. 执行", "常规流程：选对象 -> 填规则 -> 直接执行。大批量或担心冲突时先看详细预览。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("直接执行当前规则", SimpleToolsActionTone.Warning, 34, GUILayout.Width(150)))
                        BatchRename();
                    if (SimpleToolsPanelUtility.DrawActionButton("查看详细预览", SimpleToolsActionTone.Primary, 34, GUILayout.Width(120)))
                        RefreshRenamePreview();
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("全选预览", SimpleToolsActionTone.Neutral, 24, GUILayout.Width(76)))
                        SetPlanSelection(true, changedOnly: false);
                    if (SimpleToolsPanelUtility.DrawActionButton("只选会改", SimpleToolsActionTone.Neutral, 24, GUILayout.Width(76)))
                        SetPlanSelection(true, changedOnly: true);
                    if (SimpleToolsPanelUtility.DrawActionButton("清空选择", SimpleToolsActionTone.Neutral, 24, GUILayout.Width(76)))
                        SetPlanSelection(false, changedOnly: false);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawRenameHistoryPanel()
        {
            EnsureRenameRuleHistoryLoaded();
            SimpleToolsPanelUtility.DrawSectionTitle("3. 历史方案", "执行成功后会自动记录当前规则；点击恢复即可复用上次作业配置。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (renameRuleHistory.Count == 0)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("还没有历史方案。完成一次重命名后，这里会出现可恢复的规则。");
                    return;
                }

                for (int i = 0; i < renameRuleHistory.Count; i++)
                {
                    var snapshot = renameRuleHistory[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(snapshot.Label, EditorStyles.miniLabel, GUILayout.MinWidth(220));
                        EditorGUILayout.LabelField(snapshot.SavedAt, EditorStyles.miniLabel, GUILayout.Width(120));
                        if (GUILayout.Button("恢复", EditorStyles.miniButton, GUILayout.Width(48)))
                        {
                            RestoreRuleSnapshot(snapshot);
                            lastResultSummary = "已恢复重命名历史方案";
                            lastResultDetail = BuildSnapshotDetail(snapshot);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("保存当前规则", EditorStyles.miniButton, GUILayout.Width(92)))
                        AddRenameRuleHistory(CaptureCurrentRuleSnapshot("手动保存"));
                    if (GUILayout.Button("清空历史", EditorStyles.miniButton, GUILayout.Width(76)) &&
                        EditorUtility.DisplayDialog("清空重命名历史", "确定清空最近重命名方案吗？", "清空", "取消"))
                    {
                        renameRuleHistory.Clear();
                        SaveRenameRuleHistory();
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private string BuildQuickExampleName()
        {
            string original = "Cube";
            try
            {
                return original + " -> " + BuildRawName(original, 0);
            }
            catch
            {
                return original + " -> <规则不完整>";
            }
        }

        public void RefreshRenamePreview()
        {
            if (!ValidateRenameInput(showDialog: true))
                return;

            var selectedObjects = GetSortedSelection();
            BuildRenamePlan(selectedObjects);
            previewPageIndex = 0;
            previewSignature = BuildPreviewSignature(selectedObjects);
            lastResultSummary = $"预览完成: 目标 {renamePlan.Count} 个 | 会修改 {renamePlan.Count(item => item.State == "会改")} 个";
            lastResultDetail = BuildPlanReport(renamePlan.Where(item => item.State == "会改"), 16);
        }

        public void BatchRename()
        {
            if (!ValidateRenameInput(showDialog: true))
                return;

            var selectedObjects = GetSortedSelection();
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("需要选择对象", "请先在 Hierarchy 中选择要重命名的 GameObject。", "知道了");
                return;
            }

            string currentSignature = BuildPreviewSignature(selectedObjects);
            if (renamePlan.Count == 0 || previewSignature != currentSignature)
            {
                BuildRenamePlan(selectedObjects);
                previewSignature = currentSignature;
                if (executeCheckedOnly)
                    SetPlanSelection(true, changedOnly: true);
            }

            var targets = renamePlan
                .Where(item => item.Object != null && item.State == "会改" && (!executeCheckedOnly || item.Selected))
                .ToList();

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("没有需要修改的对象", "当前计划里没有勾选的可修改项。", "知道了");
                return;
            }

            string preview = BuildPlanReport(targets, 12);
            if (!SimpleToolsPanelUtility.ConfirmHeavyOperation(
                "确认批量重命名",
                targets.Count,
                $"重命名 {targets.Count} / {renamePlan.Count} 个对象。\n\n{preview}",
                "会直接修改场景对象名称。支持 Ctrl+Z 撤销，但仍建议先确认选区、排序和命名规则。"))
                return;

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Batch Rename");
            int changedCount = 0;
            var changedObjects = new List<GameObject>();

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var record = targets[i];
                    if (record.Object == null || record.Object.name == record.NewName)
                        continue;

                    Undo.RecordObject(record.Object, "Rename Object");
                    record.Object.name = record.NewName;
                    EditorUtility.SetDirty(record.Object);
                    changedObjects.Add(record.Object);
                    changedCount++;

                    if (i % 20 == 0)
                        EditorUtility.DisplayProgressBar("批量重命名", $"正在重命名: {i + 1}/{targets.Count}", (float)i / Mathf.Max(1, targets.Count));
                }

                MarkScenesDirty(changedObjects);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(group);
            }

            lastResultSummary = $"重命名完成: 修改 {changedCount} / {targets.Count} 个对象 | 模式: {renameMode}";
            lastResultDetail = BuildPlanReport(targets, 24);
            AddRenameRuleHistory(CaptureCurrentRuleSnapshot("执行"));
            var refreshedSelection = GetSortedSelection();
            BuildRenamePlan(refreshedSelection);
            previewSignature = BuildPreviewSignature(refreshedSelection);
            EditorUtility.DisplayDialog("重命名完成", $"已修改 {changedCount} 个对象名称。", "完成");
        }

        private RenameRuleSnapshot CaptureCurrentRuleSnapshot(string source)
        {
            return new RenameRuleSnapshot
            {
                Label = BuildRuleLabel(source),
                SavedAt = DateTime.Now.ToString("MM-dd HH:mm"),
                Mode = renameMode,
                SortMode = sortMode,
                ProtectPrefabAssets = protectPrefabAssets,
                PrefixText = prefixText,
                SuffixText = suffixText,
                FindText = findText,
                ReplaceText = replaceText,
                ReplaceCaseSensitive = replaceCaseSensitive,
                BaseName = baseName,
                NumberSeparator = numberSeparator,
                StartNumber = startNumber,
                NumberDigits = numberDigits
            };
        }

        private string BuildRuleLabel(string source)
        {
            switch (renameMode)
            {
                case RenameMode.Prefix:
                    return $"{source}: 加前缀 {prefixText}";
                case RenameMode.Suffix:
                    return $"{source}: 加后缀 {suffixText}";
                case RenameMode.Replace:
                    return $"{source}: 替换 {findText} -> {replaceText}";
                case RenameMode.Number:
                    return $"{source}: 编号 {baseName}{numberSeparator}{startNumber.ToString($"D{numberDigits}")}";
                default:
                    return source + ": 未知规则";
            }
        }

        private void RestoreRuleSnapshot(RenameRuleSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            renameMode = snapshot.Mode;
            sortMode = snapshot.SortMode;
            protectPrefabAssets = snapshot.ProtectPrefabAssets;
            prefixText = snapshot.PrefixText;
            suffixText = snapshot.SuffixText;
            findText = snapshot.FindText;
            replaceText = snapshot.ReplaceText;
            replaceCaseSensitive = snapshot.ReplaceCaseSensitive;
            baseName = snapshot.BaseName;
            numberSeparator = snapshot.NumberSeparator;
            startNumber = snapshot.StartNumber;
            numberDigits = Mathf.Clamp(snapshot.NumberDigits, 1, 8);
            previewPageSize = Mathf.Clamp(previewPageSize, 10, 200);

            RefreshRenamePreview();
        }

        private string BuildSnapshotDetail(RenameRuleSnapshot snapshot)
        {
            if (snapshot == null)
                return string.Empty;

            return $"方案: {snapshot.Label}\n模式: {snapshot.Mode}\n排序: {snapshot.SortMode}\n保存: {snapshot.SavedAt}";
        }

        private void AddRenameRuleHistory(RenameRuleSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            EnsureRenameRuleHistoryLoaded();
            string signature = BuildSnapshotSignature(snapshot);
            renameRuleHistory.RemoveAll(item => BuildSnapshotSignature(item) == signature);
            renameRuleHistory.Insert(0, snapshot);
            while (renameRuleHistory.Count > MaxRenameRuleHistory)
                renameRuleHistory.RemoveAt(renameRuleHistory.Count - 1);
            SaveRenameRuleHistory();
        }

        private static string BuildSnapshotSignature(RenameRuleSnapshot snapshot)
        {
            if (snapshot == null)
                return string.Empty;

            return $"{snapshot.Mode}|{snapshot.SortMode}|{snapshot.ProtectPrefabAssets}|{snapshot.PrefixText}|{snapshot.SuffixText}|{snapshot.FindText}|{snapshot.ReplaceText}|{snapshot.ReplaceCaseSensitive}|{snapshot.BaseName}|{snapshot.NumberSeparator}|{snapshot.StartNumber}|{snapshot.NumberDigits}";
        }

        private static void EnsureRenameRuleHistoryLoaded()
        {
            if (renameRuleHistoryLoaded)
                return;

            renameRuleHistoryLoaded = true;
            renameRuleHistory.Clear();
            string json = EditorPrefs.GetString(RenameRuleHistoryPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                var store = JsonUtility.FromJson<RenameRuleHistoryStore>(json);
                if (store?.Items != null)
                    renameRuleHistory.AddRange(store.Items.Where(item => item != null).Take(MaxRenameRuleHistory));
            }
            catch
            {
                renameRuleHistory.Clear();
            }
        }

        private static void SaveRenameRuleHistory()
        {
            var store = new RenameRuleHistoryStore
            {
                Items = renameRuleHistory.Take(MaxRenameRuleHistory).ToList()
            };
            EditorPrefs.SetString(RenameRuleHistoryPrefsKey, JsonUtility.ToJson(store));
        }

        private bool ValidateRenameInput(bool showDialog)
        {
            numberDigits = Mathf.Clamp(numberDigits, 1, 8);
            previewPageSize = Mathf.Clamp(previewPageSize, 10, 200);

            if (renameMode == RenameMode.Replace && string.IsNullOrEmpty(findText))
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("规则不完整", "替换模式下请输入要查找的文本。", "知道了");
                return false;
            }

            if (renameMode == RenameMode.Number && numberDigits < 1)
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("规则不完整", "编号位数必须大于 0。", "知道了");
                return false;
            }

            return true;
        }

        private GameObject[] GetSortedSelection()
        {
            var selectedObjects = (Selection.gameObjects ?? Array.Empty<GameObject>())
                .Where(obj => obj != null && (!protectPrefabAssets || !PrefabUtility.IsPartOfPrefabAsset(obj)))
                .ToArray();
            switch (sortMode)
            {
                case RenameSortMode.HierarchyPath:
                    return selectedObjects.OrderBy(GetHierarchySortPath, StringComparer.OrdinalIgnoreCase).ToArray();
                case RenameSortMode.Name:
                    return selectedObjects.OrderBy(obj => obj != null ? obj.name : string.Empty, StringComparer.OrdinalIgnoreCase).ToArray();
                default:
                    return selectedObjects;
            }
        }

        private void BuildRenamePlan(GameObject[] selectedObjects)
        {
            renamePlan.Clear();
            if (selectedObjects == null || selectedObjects.Length == 0)
                return;

            var selectionSet = new HashSet<GameObject>(selectedObjects.Where(obj => obj != null));
            var usedNames = BuildSiblingNameSet(selectedObjects, selectionSet);

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                var obj = selectedObjects[i];
                if (obj == null)
                    continue;

                string rawName = BuildRawName(obj.name, i);
                string parentKey = BuildParentKey(obj);
                string finalName = rawName;
                bool conflictResolved = false;

                if (usedNames.Contains(parentKey + finalName))
                {
                    conflictResolved = true;
                    int suffix = 1;
                    string candidate = finalName + "(" + suffix + ")";
                    while (usedNames.Contains(parentKey + candidate))
                    {
                        suffix++;
                        candidate = finalName + "(" + suffix + ")";
                    }
                    finalName = candidate;
                }

                usedNames.Add(parentKey + finalName);

                bool changed = obj.name != finalName;
                renamePlan.Add(new RenamePreviewRecord
                {
                    Selected = changed,
                    Object = obj,
                    Path = SimpleToolsSafetyUtility.GetHierarchyPath(obj),
                    OriginalName = obj.name,
                    NewName = finalName,
                    State = changed ? "会改" : "不变",
                    Note = conflictResolved ? "同级冲突，已自动改名" : (changed ? "" : "规则计算后无变化")
                });
            }
        }

        private HashSet<string> BuildSiblingNameSet(GameObject[] selectedObjects, HashSet<GameObject> selectionSet)
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in selectedObjects)
            {
                if (obj == null)
                    continue;

                var parent = obj.transform.parent;
                if (parent != null)
                {
                    for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
                    {
                        var sibling = parent.GetChild(childIndex).gameObject;
                        if (!selectionSet.Contains(sibling))
                            usedNames.Add(parent.GetInstanceID() + "|" + sibling.name);
                    }
                }
                else if (obj.scene.IsValid())
                {
                    foreach (var root in obj.scene.GetRootGameObjects())
                    {
                        if (!selectionSet.Contains(root))
                            usedNames.Add("root:" + obj.scene.handle + "|" + root.name);
                    }
                }
            }

            return usedNames;
        }

        private string BuildRawName(string originalName, int index)
        {
            string safeReplaceText = replaceText ?? string.Empty;
            string safePrefixText = prefixText ?? string.Empty;
            string safeSuffixText = suffixText ?? string.Empty;
            string safeBaseName = string.IsNullOrEmpty(baseName) ? "Object" : baseName;
            string safeSeparator = numberSeparator ?? string.Empty;
            originalName = originalName ?? string.Empty;

            switch (renameMode)
            {
                case RenameMode.Prefix:
                    return safePrefixText + originalName;
                case RenameMode.Suffix:
                    return originalName + safeSuffixText;
                case RenameMode.Replace:
                    return replaceCaseSensitive
                        ? originalName.Replace(findText, safeReplaceText)
                        : Regex.Replace(originalName, Regex.Escape(findText), _ => safeReplaceText, RegexOptions.IgnoreCase);
                case RenameMode.Number:
                    return safeBaseName + safeSeparator + (startNumber + index).ToString($"D{numberDigits}");
                default:
                    return originalName;
            }
        }

        private List<RenamePreviewRecord> GetFilteredRenamePlan()
        {
            int totalPages;
            var filtered = GetFilteredRenameQuery().ToList();
            return SimpleToolsPanelUtility.PageItems(filtered, ref previewPageIndex, previewPageSize, out totalPages);
        }

        private IEnumerable<RenamePreviewRecord> GetFilteredRenameQuery()
        {
            IEnumerable<RenamePreviewRecord> query = renamePlan;
            if (onlyShowChanged)
                query = query.Where(item => item.State == "会改");

            if (!string.IsNullOrWhiteSpace(previewSearch))
            {
                string keyword = previewSearch.Trim();
                query = query.Where(item =>
                    ContainsIgnoreCase(item.Path, keyword) ||
                    ContainsIgnoreCase(item.OriginalName, keyword) ||
                    ContainsIgnoreCase(item.NewName, keyword) ||
                    ContainsIgnoreCase(item.State, keyword) ||
                    ContainsIgnoreCase(item.Note, keyword));
            }

            return query;
        }

        private void SetPlanSelection(bool selected, bool changedOnly)
        {
            foreach (var item in renamePlan)
            {
                if (!changedOnly || item.State == "会改")
                    item.Selected = selected;
            }
        }

        private string BuildPreviewSummary()
        {
            if (renamePlan.Count == 0)
                return "还没有预览。选择对象并点击“刷新预览”。";

            int changed = renamePlan.Count(item => item.State == "会改");
            int checkedChanged = renamePlan.Count(item => item.Selected && item.State == "会改");
            int conflicts = renamePlan.Count(item => item.Note.Contains("冲突"));
            int filteredCount = GetFilteredRenameQuery().Count();
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(filteredCount / (float)Mathf.Max(1, previewPageSize)));
            previewPageIndex = Mathf.Clamp(previewPageIndex, 0, totalPages - 1);
            return $"预览 {renamePlan.Count} | 会修改 {changed} | 勾选执行 {checkedChanged} | 自动处理冲突 {conflicts} | 筛选后 {filteredCount} | 第 {previewPageIndex + 1}/{totalPages} 页";
        }

        private string BuildPlanReport(IEnumerable<RenamePreviewRecord> records, int limit)
        {
            return SimpleToolsSafetyUtility.JoinPreview(
                records?.Where(item => item != null).Select(item => $"{item.Path}: {item.OriginalName} -> {item.NewName}{(string.IsNullOrWhiteSpace(item.Note) ? "" : " | " + item.Note)}"),
                limit);
        }

        private string BuildPreviewSignature(GameObject[] selectedObjects)
        {
            string selection = string.Join("|", (selectedObjects ?? Array.Empty<GameObject>()).Where(obj => obj != null).Select(obj => obj.GetInstanceID()));
            return $"{selection}|{renameMode}|{sortMode}|{protectPrefabAssets}|{prefixText}|{suffixText}|{findText}|{replaceText}|{replaceCaseSensitive}|{baseName}|{numberSeparator}|{startNumber}|{numberDigits}";
        }

        private static string BuildParentKey(GameObject obj)
        {
            if (obj == null)
                return "null|";

            return obj.transform.parent != null ? obj.transform.parent.GetInstanceID() + "|" : "root:" + obj.scene.handle + "|";
        }

        private static string GetHierarchySortPath(GameObject obj)
        {
            return obj == null ? string.Empty : SimpleToolsSafetyUtility.GetHierarchyPath(obj);
        }

        private static bool ContainsIgnoreCase(string source, string keyword)
        {
            return !string.IsNullOrEmpty(source) &&
                   !string.IsNullOrEmpty(keyword) &&
                   source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
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
