using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor.Expressions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ES
{
    [Serializable]
    [ESSimpleToolsLayout]
    public class Page_RuntimeWatch : ESWindowPageBase
    {
        private readonly Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> ownerFoldouts = new Dictionary<string, bool>();
        private readonly List<WatchEntry> entries = new List<WatchEntry>();
        // 后台受控采样写 entries；IMGUI 只在 Layout 边界切换 renderEntries，
        // 防止采样更新恰好落在 Layout/Repaint 之间时改变控件树数量。
        private readonly List<WatchEntry> renderEntries = new List<WatchEntry>();
        private readonly List<RuntimeWatchEvidenceRecorder.RecordedEvent> renderEvidenceEvents =
            new List<RuntimeWatchEvidenceRecorder.RecordedEvent>(64);
        private int entriesVersion;
        private int renderEntriesVersion = -1;
        private bool renderEvidenceIsRecording;
        private bool renderEvidenceHasEvidence;
        private int renderEvidenceEventCount;
        private string renderEvidenceScopeLabel = string.Empty;
        private static readonly Dictionary<Type, PropertyInfo> ModulesEnumerablePropertyCache = new Dictionary<Type, PropertyInfo>();
        private static readonly Dictionary<string, Delegate> OdinBoolExpressionCache = new Dictionary<string, Delegate>();
        private static readonly HashSet<string> FailedOdinBoolExpressions = new HashSet<string>();
        private static readonly HashSet<string> LoggedRuntimeWatchWarnings = new HashSet<string>();
        private readonly Dictionary<string, string> inlineInputDrafts = new Dictionary<string, string>();
        private readonly HashSet<string> manuallyEditedInlineDraftKeys = new HashSet<string>();
        private readonly Dictionary<string, WatchSampleState> sampleStates = new Dictionary<string, WatchSampleState>();
        private readonly HashSet<string> pinnedEntryKeys = new HashSet<string>(StringComparer.Ordinal);
        // 下拉选项随受控采样更新；OnGUI 只读取列表，避免每次 Layout/Repaint 重新 LINQ 分组和分配。
        private readonly List<string> categoryFilterOptions = new List<string> { "全部" };
        private readonly List<string> scriptFilterOptions = new List<string> { "全部" };
        private readonly List<string> objectFilterOptions = new List<string> { "全部" };
        // 证据只存在当前编辑器会话：显式开始、固定容量、不会写入场景或资产。
        [NonSerialized]
        private readonly RuntimeWatchEvidenceRecorder evidenceRecorder = new RuntimeWatchEvidenceRecorder();
        private bool defaultFoldoutExpanded = true;
        private bool pinnedEntriesLoaded;
        [NonSerialized]
        private bool showEvidenceReplay = true;
        [NonSerialized]
        private bool restoreAutoRefreshAfterRecording;
        [NonSerialized]
        private bool autoRefreshBeforeRecording;
        [NonSerialized]
        private bool autoRefreshPausedBeforeRecording;
        private const string PinnedEntriesPrefsKey = "ES_RuntimeWatch_PinnedEntries";

        private string searchText = "";

        private string selectedCategoryFilter = "全部";

        private string selectedObjectFilter = "全部";

        private string selectedScriptFilter = "全部";

        private bool autoRefresh = true;

        private float refreshInterval = 0.25f;

        private bool refreshInEditMode = false;

        private bool onlySelectedGameObject = false;

        private bool includeSelectedChildren = true;

        private bool enableTagFilter = true;

        private bool enableShowIfFilter = true;

        private bool allowGetMoudleFallback = false;

        private bool compactView = false;

        private double nextRefreshTime;
        private Vector2 scroll;
        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private string lastActionEntryKey = "";
        private string lastActionFeedback = "";
        private bool lastActionFailed;
        private bool lastActionFeedbackVisible;
        private string chainReport = "";
        private int lastScannedOwnerTypeCount;
        private int lastFoundOwnerCount;
        private int lastCandidateEntryCount;
        private int lastNoFilterCandidateCount;
        private int lastTagFilteredCount;
        private int lastShowIfFilteredCount;
        private int lastContextMissingCount;
        private int lastDuplicateSkippedCount;
        private double lastScanDurationMs;
        private bool showAdvancedFilters;
        private bool showDiagnostics;
        private bool autoRefreshPaused;
        private bool confirmMethodActions = true;
        private int currentPage;
        private int pageSize = 100;
        private RuntimeWatchViewFilter viewFilter = RuntimeWatchViewFilter.All;
        // 观察值只能由受控刷新路径更新，绝不能在 Layout/Repaint 中重复执行 Getter。
        private bool snapshotRequiresRefresh = true;
        private bool foregroundRefreshPending;
        private int sampledSelectionInstanceId;
        private double lastRefreshAt;
        private string lastRefreshReason = "尚未刷新";
        private int lastReadFailureCount;
        private int lastSlowGetterCount;
        private int lastManualActionCount;
        private const double ChangeHighlightSeconds = 0.5d;
        private const double SlowGetterThresholdMs = 2d;

        private enum RuntimeWatchViewFilter
        {
            [InspectorName("全部")]
            All,
            [InspectorName("最近变化")]
            Changed,
            [InspectorName("读取失败")]
            ReadFailures,
            [InspectorName("慢读取")]
            SlowGetters,
            [InspectorName("可执行项")]
            Actions,
            [InspectorName("已收藏")]
            Pinned
        }

        private enum RuntimeWatchDropdownFilter
        {
            Category,
            Script,
            SceneObject
        }

        public void RefreshNow()
        {
            CollectEntries(recordResult: true, refreshReason: "手动刷新");
            nextRefreshTime = EditorApplication.timeSinceStartup + refreshInterval;
        }

        /// <summary>
        /// The SimpleTools host calls this only when RuntimeWatch becomes frontmost.
        /// It schedules an immediate automatic sample without allowing background tabs
        /// or ordinary OnGUI repaints to trigger collection.
        /// </summary>
        public void RequestForegroundRefresh()
        {
            if (!ShouldAutoRefresh())
                return;

            foregroundRefreshPending = true;
            nextRefreshTime = 0d;
        }

        public bool TryAutoRefreshFromEditorTick()
        {
            if (evidenceRecorder.IsRecording && !EditorApplication.isPlaying)
            {
                StopEvidenceRecording("已随 Play Mode 结束冻结");
                return true;
            }

            if (!ShouldAutoRefresh() || EditorApplication.timeSinceStartup < nextRefreshTime)
                return false;

            string refreshReason = foregroundRefreshPending ? "前台恢复" : "自动刷新";
            foregroundRefreshPending = false;
            CollectEntries(recordResult: false, refreshReason: refreshReason);
            nextRefreshTime = EditorApplication.timeSinceStartup + refreshInterval;
            return true;
        }

        public void BuildChainReport()
        {
            chainReport = BuildRegistryChainReport();
        }

        public void CopyChainReport()
        {
            if (string.IsNullOrEmpty(chainReport))
                chainReport = BuildRegistryChainReport();

            EditorGUIUtility.systemCopyBuffer = chainReport;
            lastResultSummary = "已复制 RuntimeWatch 链路报告";
            lastResultDetail = "报告已写入系统剪贴板。";
        }

        public void ClearChainReport()
        {
            chainReport = "";
        }

        public void ExpandAllFoldouts()
        {
            defaultFoldoutExpanded = true;
            groupFoldouts.Clear();
            ownerFoldouts.Clear();
            lastResultSummary = "已展开所有 RuntimeWatch 分组";
            lastResultDetail = "当前已清空折叠缓存，新条目将默认展开。";
        }

        public void CollapseAllFoldouts()
        {
            defaultFoldoutExpanded = false;
            groupFoldouts.Clear();
            ownerFoldouts.Clear();
            lastResultSummary = "已收起所有 RuntimeWatch 分组";
            lastResultDetail = "当前已清空折叠缓存，新条目将默认收起。";
        }

        public void ResetFoldouts()
        {
            defaultFoldoutExpanded = true;
            groupFoldouts.Clear();
            ownerFoldouts.Clear();
        }

        private string ChainReportView => string.IsNullOrEmpty(chainReport) ? "点击“生成链路报告”查看底层完整链路。" : chainReport;

        // Render results after the actual filters and controls, never before them.
        [OnInspectorGUI, PropertyOrder(100)]
        private void DrawRuntimeWatch()
        {
            EnsurePinnedEntriesLoaded();
            PrepareRenderEntries();
            DetectSelectedScopeChange();

            // 自动刷新由 SimpleToolsWindow 的 EditorApplication.update 宿主驱动。
            // OnGUI 只消费当前缓存，避免 Layout/Repaint 或重绘触发扫描。
            SimpleToolsPanelUtility.DrawToolHeader(
                "RuntimeWatch 运行时观察",
                "确认范围 → 刷新快照 → 观察变化或显式执行操作。界面永远只显示最近一次受控采样缓存。",
                SimpleToolsMaturity.Upgrading,
                "自动刷新只在 RuntimeWatch 前台执行；读取不会写入场景或资产，方法与设值必须由用户明确触发。");
            SimpleToolsPanelUtility.DrawSummary(
                $"范围: {BuildScopeLabel()}",
                $"快照: {BuildSnapshotStatusLabel()}",
                $"观察: 已收集 {renderEntries.Count} 项 · 可操作 {lastManualActionCount}",
                $"状态: 失败 {lastReadFailureCount} · 慢读取 {lastSlowGetterCount}");

            DrawPrimaryToolbar();
            DrawSecondaryFilters();
            int visibleCount = CountVisibleEntries();

            if (!EditorApplication.isPlaying && !refreshInEditMode)
                EditorGUILayout.HelpBox("当前处于 Edit Mode：自动刷新会等待 Play Mode。仍可点击“刷新快照”执行一次明确的场景检查。", MessageType.Info);

            if (renderEntries.Count == 0)
            {
                string emptyMessage = EditorApplication.isPlaying
                    ? "当前没有找到观察项。请检查 ESRuntimeWatch 标记、场景实例和高级过滤条件。"
                    : "尚未扫描到观察项。点击“刷新快照”扫描当前场景。";
                EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
            }

            DrawContentToolbar(visibleCount);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawEntries(visibleCount);
            EditorGUILayout.EndScrollView();
            DrawEvidenceReplay();
            DrawDiagnosticsPanel();

        }

        private bool ShouldAutoRefresh()
        {
            return autoRefresh
                && !autoRefreshPaused
                && (EditorApplication.isPlaying || refreshInEditMode);
        }

        private void DrawPrimaryToolbar()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("1 · 选择范围并刷新", "范围、Tag 和 ShowIf 会影响下一次采样；搜索、分类、脚本和对象筛选只作用于当前快照。");
            bool narrow = EditorGUIUtility.currentViewWidth < 720f;
            if (narrow)
            {
                searchText = EditorGUILayout.TextField(searchText, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawRefreshSnapshotButton();
                    DrawWatchQuickJumpButton();
                    GUILayout.FlexibleSpace();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawGameObjectQuickJumpButton();
                    DrawEvidenceButton();
                    GUILayout.FlexibleSpace();
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    searchText = EditorGUILayout.TextField(searchText, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField);
                    DrawQuickJumpActions();
                }
            }

            if (narrow)
            {
                float viewWidth = EditorGUIUtility.currentViewWidth;
                if (viewWidth < 340f)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        selectedCategoryFilter = DrawRuntimeWatchFilterDropdown(
                            RuntimeWatchDropdownFilter.Category,
                            "分类",
                            selectedCategoryFilter,
                            categoryFilterOptions,
                            Mathf.Max(120f, viewWidth - 6f));
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        selectedScriptFilter = DrawRuntimeWatchFilterDropdown(
                            RuntimeWatchDropdownFilter.Script,
                            "脚本",
                            selectedScriptFilter,
                            scriptFilterOptions,
                            Mathf.Max(120f, viewWidth - 6f));
                    }
                }
                else
                {
                    float halfWidth = Mathf.Max(132f, (viewWidth - 8f) * 0.5f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        selectedCategoryFilter = DrawRuntimeWatchFilterDropdown(
                            RuntimeWatchDropdownFilter.Category,
                            "分类",
                            selectedCategoryFilter,
                            categoryFilterOptions,
                            halfWidth);
                        selectedScriptFilter = DrawRuntimeWatchFilterDropdown(
                            RuntimeWatchDropdownFilter.Script,
                            "脚本",
                            selectedScriptFilter,
                            scriptFilterOptions,
                            halfWidth);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    selectedObjectFilter = DrawRuntimeWatchFilterDropdown(
                        RuntimeWatchDropdownFilter.SceneObject,
                        "对象",
                        selectedObjectFilter,
                        objectFilterOptions,
                        Mathf.Max(120f, viewWidth - 6f));
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    selectedCategoryFilter = DrawRuntimeWatchFilterDropdown(
                        RuntimeWatchDropdownFilter.Category,
                        "分类",
                        selectedCategoryFilter,
                        categoryFilterOptions,
                        180f);
                    selectedScriptFilter = DrawRuntimeWatchFilterDropdown(
                        RuntimeWatchDropdownFilter.Script,
                        "脚本",
                        selectedScriptFilter,
                        scriptFilterOptions,
                        220f);
                    selectedObjectFilter = DrawRuntimeWatchFilterDropdown(
                        RuntimeWatchDropdownFilter.SceneObject,
                        "对象",
                        selectedObjectFilter,
                        objectFilterOptions,
                        280f);
                }
            }

            if (narrow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawScopeSelector();
                    if (GUILayout.Button(new GUIContent("重置筛选", "恢复全部文本、分类、脚本、对象和结果视图。若范围或采样规则发生变化，会提示你刷新快照。"), EditorStyles.miniButton, GUILayout.Width(72)))
                        ClearWatchFilters();
                    GUILayout.FlexibleSpace();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(BuildRuntimeModeStatusLabel(), EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawScopeSelector();
                    if (GUILayout.Button(new GUIContent("重置筛选", "恢复全部文本、分类、脚本、对象和结果视图。若范围或采样规则发生变化，会提示你刷新快照。"), EditorStyles.miniButton, GUILayout.Width(72)))
                        ClearWatchFilters();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(BuildRuntimeModeStatusLabel(), EditorStyles.miniBoldLabel);
                }
            }
        }

        private void DrawQuickJumpActions()
        {
            DrawRefreshSnapshotButton();
            DrawWatchQuickJumpButton();
            DrawGameObjectQuickJumpButton();
            DrawEvidenceButton();
        }

        private void DrawRefreshSnapshotButton()
        {
            string refreshLabel = snapshotRequiresRefresh ? "刷新快照 ●" : "刷新快照";
            if (SimpleToolsPanelUtility.DrawActionButton(
                    refreshLabel,
                    "立即按当前范围和采样规则读取一次。不会写入场景或资产。",
                    SimpleToolsActionTone.Primary,
                    22,
                    GUILayout.Width(82)))
                RefreshNow();
        }

        private void DrawWatchQuickJumpButton()
        {
            if (GUILayout.Button(new GUIContent("观察项 ▼", "按 RuntimeWatch 分类快速跳转。"), EditorStyles.miniButton, GUILayout.Width(78)))
                ShowQuickJumpMenu(GUILayoutUtility.GetLastRect());
        }

        private void DrawGameObjectQuickJumpButton()
        {
            if (GUILayout.Button(new GUIContent("GameObject ▼", "按场景对象快速跳转。"), EditorStyles.miniButton, GUILayout.Width(96)))
                ShowGameObjectJumpMenu(GUILayoutUtility.GetLastRect());
        }

        private void DrawScopeSelector()
        {
            string scopeLabel;
            if (!onlySelectedGameObject)
            {
                scopeLabel = "范围 · 当前场景";
            }
            else
            {
                GameObject selected = Selection.activeGameObject;
                string selectedName = selected != null ? selected.name : "未选择对象";
                scopeLabel = includeSelectedChildren
                    ? "范围 · " + selectedName + " + 子层级"
                    : "范围 · " + selectedName;
            }

            if (!GUILayout.Button(
                    new GUIContent(scopeLabel + " ▼", "选择本次采样范围。切换范围是明确操作，会立即刷新快照。"),
                    EditorStyles.miniButton,
                    GUILayout.Width(Mathf.Min(220f, Mathf.Max(132f, EditorGUIUtility.currentViewWidth * 0.36f)))))
                return;

            Rect anchor = GUILayoutUtility.GetLastRect();
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("当前场景"), !onlySelectedGameObject, () => SetObservationScope(false, true));

            bool hasSelectedGameObject = Selection.activeGameObject != null;
            if (hasSelectedGameObject)
            {
                menu.AddItem(
                    new GUIContent("当前选中对象"),
                    onlySelectedGameObject && !includeSelectedChildren,
                    () => SetObservationScope(true, false));
                menu.AddItem(
                    new GUIContent("当前选中对象及子层级"),
                    onlySelectedGameObject && includeSelectedChildren,
                    () => SetObservationScope(true, true));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("当前选中对象（请先在 Hierarchy 选择对象）"));
                menu.AddDisabledItem(new GUIContent("当前选中对象及子层级（请先在 Hierarchy 选择对象）"));
            }

            menu.DropDown(anchor);
        }

        private void SetObservationScope(bool useSelectedObject, bool includeChildren)
        {
            onlySelectedGameObject = useSelectedObject;
            includeSelectedChildren = includeChildren;
            currentPage = 0;
            // 用户明确选择了采样范围，因此立即更新，而不是让 UI 短暂显示旧范围的数据。
            RefreshNow();
        }

        private string GetAutoRefreshStatusLabel()
        {
            if (!autoRefresh)
                return "手动刷新";

            if (autoRefreshPaused)
                return "自动刷新已暂停";

            if (!EditorApplication.isPlaying && !refreshInEditMode)
                return "自动刷新等待 Play";

            return "自动刷新 " + refreshInterval.ToString("0.##", CultureInfo.InvariantCulture) + "s";
        }

        private string BuildRuntimeModeStatusLabel()
        {
            string mode = EditorApplication.isPlaying ? "● Play" : "○ Edit";
            return mode + " · " + GetAutoRefreshStatusLabel();
        }

        private void DrawEvidenceButton()
        {
            string actionLabel = evidenceRecorder.IsRecording
                ? $"■ {evidenceRecorder.ElapsedSeconds:0}s"
                : "● 录制";
            if (!evidenceRecorder.IsRecording && !EditorApplication.isPlaying)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    SimpleToolsPanelUtility.DrawCompactButton(
                        "Play 后录制",
                        "证据录制只在 Play Mode 中可用，避免编辑状态产生后台采样。",
                        82,
                        22);
                }
                return;
            }

            if (SimpleToolsPanelUtility.DrawCompactButton(
                    actionLabel,
                    "显式记录 RuntimeWatch 字段/属性的变化；不会执行方法，也不会写入场景或资产。",
                    68,
                    22))
            {
                if (evidenceRecorder.IsRecording)
                {
                    StopEvidenceRecording("用户停止");
                }
                else if (!EditorApplication.isPlaying)
                {
                    StartEvidenceRecording();
                }
                else
                {
                    if (!evidenceRecorder.HasEvidence
                        || EditorUtility.DisplayDialog("开始新的运行证据录制", "当前会清除本次内存回放，并建立新的状态基线。场景和资产不会被修改。", "开始新录制", "保留当前回放"))
                    {
                        StartEvidenceRecording();
                    }
                }
            }
        }

        private void StartEvidenceRecording()
        {
            if (!EditorApplication.isPlaying)
            {
                lastResultSummary = "证据录制需要在 Play Mode 中开始";
                lastResultDetail = "进入 Play Mode 后点击“开始证据录制”。不会自动开始，也不会扫描或写入资产。";
                return;
            }

            // 建立一份明确的初始基线；之后只记录变化，不保留每帧完整快照。
            CollectEntries(recordResult: false, refreshReason: "录制基线");
            autoRefreshBeforeRecording = autoRefresh;
            autoRefreshPausedBeforeRecording = autoRefreshPaused;
            restoreAutoRefreshAfterRecording = true;
            evidenceRecorder.Start(EditorApplication.timeSinceStartup, BuildScopeLabel());
            CaptureEvidenceSample();
            autoRefresh = true;
            autoRefreshPaused = false;
            nextRefreshTime = EditorApplication.timeSinceStartup + refreshInterval;
            lastResultSummary = "已开始运行证据录制";
            lastResultDetail = "已建立当前观察项基线。对象销毁或状态变化后，可在下方回放前后值。";
        }

        private void StopEvidenceRecording(string reason)
        {
            if (!evidenceRecorder.IsRecording)
                return;

            evidenceRecorder.Stop(EditorApplication.timeSinceStartup, reason);
            if (restoreAutoRefreshAfterRecording)
            {
                autoRefresh = autoRefreshBeforeRecording;
                autoRefreshPaused = autoRefreshPausedBeforeRecording;
                restoreAutoRefreshAfterRecording = false;
            }
            showEvidenceReplay = true;
            lastResultSummary = "运行证据已冻结";
            lastResultDetail = evidenceRecorder.EventCount == 0
                ? "录制期间没有观察到值变化。"
                : $"已保留 {evidenceRecorder.EventCount} 条变化。它们仅存在当前编辑器会话。";
        }

        private void DrawEvidenceReplay()
        {
            if (!renderEvidenceIsRecording && !renderEvidenceHasEvidence)
                return;

            SimpleToolsPanelUtility.DrawSectionTitle("4 · 运行证据", "仅记录录制期间发生变化的字段与属性；方法绝不因录制而自动执行。证据只保留当前编辑器会话。");
            showEvidenceReplay = EditorGUILayout.Foldout(
                showEvidenceReplay,
                renderEvidenceIsRecording
                    ? $"运行证据 · 录制中（{renderEvidenceEventCount} 条变化）"
                    : $"运行证据回放（{renderEvidenceEventCount} 条变化）",
                true);
            if (!showEvidenceReplay)
                return;

            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                string description = renderEvidenceIsRecording
                    ? "只追加发生变化的字段/属性；方法永不自动执行。"
                    : "按最新变化优先显示。对象已销毁时仍保留它销毁前的最后状态。";
                EditorGUILayout.LabelField("范围：" + renderEvidenceScopeLabel + " · " + description, EditorStyles.miniLabel);

                bool narrow = EditorGUIUtility.currentViewWidth < 720f;
                for (int i = 0; i < renderEvidenceEvents.Count; i++)
                {
                    RuntimeWatchEvidenceRecorder.RecordedEvent recordedEvent = renderEvidenceEvents[i];
                    if (recordedEvent == null)
                        continue;

                    if (narrow)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label($"{recordedEvent.ElapsedSeconds,6:0.000}s", EditorStyles.miniLabel, GUILayout.Width(58));
                            GUILayout.Label(recordedEvent.OwnerName, EditorStyles.miniBoldLabel);
                            using (new EditorGUI.DisabledScope(recordedEvent.Owner == null))
                            {
                                if (GUILayout.Button(new GUIContent("◎", "定位对象"), EditorStyles.miniButton, GUILayout.Width(28)))
                                {
                                    Selection.activeObject = recordedEvent.Owner;
                                    EditorGUIUtility.PingObject(recordedEvent.Owner);
                                }
                            }
                        }
                        EditorGUILayout.LabelField(recordedEvent.Label, EditorStyles.miniLabel);
                        EditorGUILayout.SelectableLabel(
                            recordedEvent.Before + "  →  " + recordedEvent.After,
                            EditorStyles.miniLabel,
                            GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    }
                    else
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label($"{recordedEvent.ElapsedSeconds,6:0.000}s", EditorStyles.miniLabel, GUILayout.Width(58));
                            GUILayout.Label(recordedEvent.OwnerName, EditorStyles.miniBoldLabel, GUILayout.Width(150));
                            GUILayout.Label(recordedEvent.Label, EditorStyles.miniLabel, GUILayout.Width(112));
                            EditorGUILayout.SelectableLabel(
                                recordedEvent.Before + "  →  " + recordedEvent.After,
                                EditorStyles.miniLabel,
                                GUILayout.Height(EditorGUIUtility.singleLineHeight));
                            using (new EditorGUI.DisabledScope(recordedEvent.Owner == null))
                            {
                                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(42)))
                                {
                                    Selection.activeObject = recordedEvent.Owner;
                                    EditorGUIUtility.PingObject(recordedEvent.Owner);
                                }
                            }
                        }
                    }
                }

                if (renderEvidenceEventCount > renderEvidenceEvents.Count)
                    EditorGUILayout.LabelField($"为保持回放紧凑，仅显示最新 {renderEvidenceEvents.Count} 条；本次共 {renderEvidenceEventCount} 条。", EditorStyles.miniLabel);

                if (!renderEvidenceIsRecording
                    && GUILayout.Button("清除本次回放", EditorStyles.miniButton, GUILayout.Width(98)))
                {
                    evidenceRecorder.Clear();
                    lastResultSummary = "已清空本次运行证据";
                    lastResultDetail = "仅清除内存中的回放记录，不会修改场景或资产。";
                }
            }
        }

        private void DrawSecondaryFilters()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("2 · 刷新与显示", "自动刷新只在前台 RuntimeWatch 执行；高级采样规则变更后会明确标记“需要刷新”。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                bool narrow = EditorGUIUtility.currentViewWidth < 720f;
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool previousAutoRefresh = autoRefresh;
                    autoRefresh = GUILayout.Toggle(autoRefresh, "自动刷新", EditorStyles.miniButton, GUILayout.Width(78));
                    if (autoRefresh && !previousAutoRefresh && !autoRefreshPaused)
                        nextRefreshTime = 0d;

                    string pauseLabel = autoRefreshPaused ? "继续刷新" : "暂停刷新";
                    using (new EditorGUI.DisabledScope(!autoRefresh))
                    {
                        if (GUILayout.Button(pauseLabel, EditorStyles.miniButton, GUILayout.Width(78)))
                            autoRefreshPaused = !autoRefreshPaused;
                    }

                    DrawRefreshIntervalSelector();
                    if (!narrow)
                    {
                        GUILayout.FlexibleSpace();
                        showAdvancedFilters = EditorGUILayout.Foldout(showAdvancedFilters, "高级采样与操作规则", true);
                    }
                }

                if (narrow)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        showAdvancedFilters = EditorGUILayout.Foldout(showAdvancedFilters, "高级采样与操作规则", true);
                        GUILayout.FlexibleSpace();
                    }
                }

                if (!showAdvancedFilters)
                    return;

                EditorGUILayout.Space(2);
                bool previousTagFilter = enableTagFilter;
                bool previousShowIfFilter = enableShowIfFilter;
                bool previousGetModuleFallback = allowGetMoudleFallback;
                bool previousEditModeRefresh = refreshInEditMode;

                using (new EditorGUILayout.HorizontalScope())
                {
                    enableTagFilter = GUILayout.Toggle(enableTagFilter, "Tag 过滤", EditorStyles.miniButton, GUILayout.Width(82));
                    enableShowIfFilter = GUILayout.Toggle(enableShowIfFilter, "ShowIf", EditorStyles.miniButton, GUILayout.Width(72));
                    allowGetMoudleFallback = GUILayout.Toggle(allowGetMoudleFallback, "允许 GetMoudle", EditorStyles.miniButton, GUILayout.Width(116));
                    if (!narrow)
                    {
                        refreshInEditMode = GUILayout.Toggle(refreshInEditMode, "Edit Mode 自动扫描", EditorStyles.miniButton, GUILayout.Width(116));
                        compactView = GUILayout.Toggle(compactView, "紧凑条目", EditorStyles.miniButton, GUILayout.Width(82));
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (narrow)
                    {
                        refreshInEditMode = GUILayout.Toggle(refreshInEditMode, "Edit Mode 自动扫描", EditorStyles.miniButton, GUILayout.Width(116));
                        compactView = GUILayout.Toggle(compactView, "紧凑条目", EditorStyles.miniButton, GUILayout.Width(82));
                    }

                    confirmMethodActions = GUILayout.Toggle(confirmMethodActions, "方法二次确认", EditorStyles.miniButton, GUILayout.Width(106));
                    GUILayout.FlexibleSpace();
                }

                if (previousTagFilter != enableTagFilter
                    || previousShowIfFilter != enableShowIfFilter
                    || previousGetModuleFallback != allowGetMoudleFallback)
                    MarkSnapshotStale();

                if (previousEditModeRefresh != refreshInEditMode && refreshInEditMode && autoRefresh && !autoRefreshPaused)
                    nextRefreshTime = 0d;

                if (allowGetMoudleFallback)
                    EditorGUILayout.HelpBox("允许 GetMoudle 可能创建缺失模块，仅建议诊断时临时启用；修改后请刷新快照。", MessageType.Warning);
            }
        }

        private void DrawRefreshIntervalSelector()
        {
            string label = "间隔 " + refreshInterval.ToString("0.##", CultureInfo.InvariantCulture) + "s ▼";
            if (!GUILayout.Button(new GUIContent(label, "设置前台自动刷新的最小间隔。间隔越短，Getter 采样成本越高。"), EditorStyles.miniButton, GUILayout.Width(88)))
                return;

            Rect anchor = GUILayoutUtility.GetLastRect();
            var menu = new GenericMenu();
            AddRefreshIntervalMenuItem(menu, 0.10f, "0.10 秒 · 高频");
            AddRefreshIntervalMenuItem(menu, 0.25f, "0.25 秒 · 推荐");
            AddRefreshIntervalMenuItem(menu, 0.50f, "0.50 秒");
            AddRefreshIntervalMenuItem(menu, 1.00f, "1.00 秒 · 低频");
            menu.DropDown(anchor);
        }

        private void AddRefreshIntervalMenuItem(GenericMenu menu, float value, string label)
        {
            bool selected = Mathf.Approximately(refreshInterval, value);
            menu.AddItem(new GUIContent(label), selected, () =>
            {
                refreshInterval = value;
                if (autoRefresh && !autoRefreshPaused)
                    nextRefreshTime = 0d;
            });
        }

        private void DrawContentToolbar(int visibleCount)
        {
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(visibleCount / (float)Mathf.Max(1, pageSize)));
            currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
            SimpleToolsPanelUtility.DrawSectionTitle("3 · 观察结果与操作", "按分类 → 对象 → 成员阅读。字段/属性的设值和方法执行均为明确操作；折叠只保存当前编辑器会话。");
            bool narrow = EditorGUIUtility.currentViewWidth < 720f;
            using (new EditorGUILayout.VerticalScope(EditorStyles.toolbar))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawViewFilterSelector();
                    if (GUILayout.Button("全部展开", EditorStyles.toolbarButton, GUILayout.Width(72)))
                        ExpandAllFoldouts();

                    if (!narrow)
                    {
                        if (GUILayout.Button("全部收起", EditorStyles.toolbarButton, GUILayout.Width(72)))
                            CollapseAllFoldouts();
                        if (GUILayout.Button("重置折叠", EditorStyles.toolbarButton, GUILayout.Width(72)))
                            ResetFoldouts();
                        GUILayout.FlexibleSpace();
                        DrawPagingControls(visibleCount, pageCount);
                    }
                }

                if (narrow)
                {
                    bool veryNarrow = EditorGUIUtility.currentViewWidth < 400f;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("全部收起", EditorStyles.toolbarButton, GUILayout.Width(72)))
                            CollapseAllFoldouts();
                        if (GUILayout.Button("重置折叠", EditorStyles.toolbarButton, GUILayout.Width(72)))
                            ResetFoldouts();
                        if (!veryNarrow)
                        {
                            GUILayout.FlexibleSpace();
                            DrawPagingControls(visibleCount, pageCount);
                        }
                    }

                    if (veryNarrow)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            DrawPagingControls(visibleCount, pageCount);
                            GUILayout.FlexibleSpace();
                        }
                    }
                }
            }
        }

        private void DrawViewFilterSelector()
        {
            string label = "显示 · " + GetViewFilterDisplayName(viewFilter) + " ▼";
            if (!GUILayout.Button(new GUIContent(label, "仅改变当前快照的显示方式，不会触发采样。"), EditorStyles.toolbarDropDown, GUILayout.Width(112)))
                return;

            Rect anchor = GUILayoutUtility.GetLastRect();
            var menu = new GenericMenu();
            Array filters = Enum.GetValues(typeof(RuntimeWatchViewFilter));
            for (int i = 0; i < filters.Length; i++)
            {
                RuntimeWatchViewFilter filter = (RuntimeWatchViewFilter)filters.GetValue(i);
                RuntimeWatchViewFilter captured = filter;
                menu.AddItem(
                    new GUIContent(GetViewFilterDisplayName(captured)),
                    viewFilter == captured,
                    () =>
                    {
                        viewFilter = captured;
                        currentPage = 0;
                    });
            }

            menu.DropDown(anchor);
        }

        private static string GetViewFilterDisplayName(RuntimeWatchViewFilter filter)
        {
            switch (filter)
            {
                case RuntimeWatchViewFilter.Changed:
                    return "最近变化";
                case RuntimeWatchViewFilter.ReadFailures:
                    return "读取失败";
                case RuntimeWatchViewFilter.SlowGetters:
                    return "慢读取";
                case RuntimeWatchViewFilter.Actions:
                    return "可操作项";
                case RuntimeWatchViewFilter.Pinned:
                    return "已收藏";
                default:
                    return "全部";
            }
        }

        private void DrawPagingControls(int visibleCount, int pageCount)
        {
            bool compactPager = EditorGUIUtility.currentViewWidth < 480f;
            if (compactPager)
                GUILayout.Label($"{visibleCount}/{renderEntries.Count}", EditorStyles.miniLabel, GUILayout.Width(42));
            else
            {
                GUILayout.Label($"显示 {visibleCount} / 收集 {renderEntries.Count}", EditorStyles.miniLabel);
                GUILayout.Space(8);
            }
            if (GUILayout.Button("‹", EditorStyles.toolbarButton, GUILayout.Width(24)))
                currentPage = Mathf.Max(0, currentPage - 1);
            GUILayout.Label($"{currentPage + 1}/{pageCount}", EditorStyles.miniLabel, GUILayout.Width(compactPager ? 36 : 48));
            if (GUILayout.Button("›", EditorStyles.toolbarButton, GUILayout.Width(24)))
                currentPage = Mathf.Min(pageCount - 1, currentPage + 1);
            pageSize = EditorGUILayout.IntPopup(pageSize, new[] { "50", "100", "200", "500" }, new[] { 50, 100, 200, 500 }, EditorStyles.toolbarPopup, GUILayout.Width(compactPager ? 50 : 58));
        }

        private void DrawDiagnosticsPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("5 · 诊断与链路报告", "仅在需要解释筛选、注册、上下文或异常时展开；不会干扰日常观察操作。");
            showDiagnostics = EditorGUILayout.Foldout(showDiagnostics, "诊断与链路报告", true);
            if (!showDiagnostics)
                return;

            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                EditorGUILayout.LabelField(
                    $"Tag过滤 {lastTagFilteredCount} · ShowIf过滤 {lastShowIfFilteredCount} · 上下文缺失 {lastContextMissingCount} · 重复跳过 {lastDuplicateSkippedCount}",
                    EditorStyles.miniLabel);
                if (!string.IsNullOrWhiteSpace(lastResultSummary))
                    EditorGUILayout.HelpBox(lastResultSummary + (string.IsNullOrWhiteSpace(lastResultDetail) ? string.Empty : "\n" + lastResultDetail), MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("生成链路报告", EditorStyles.miniButton, GUILayout.Width(112)))
                        BuildChainReport();
                    if (GUILayout.Button("复制报告", EditorStyles.miniButton, GUILayout.Width(82)))
                        CopyChainReport();
                    if (GUILayout.Button("清空报告", EditorStyles.miniButton, GUILayout.Width(82)))
                        ClearChainReport();
                }

                if (!string.IsNullOrWhiteSpace(chainReport))
                    EditorGUILayout.TextArea(chainReport, GUILayout.MinHeight(180));
            }
        }

        private string DrawRuntimeWatchFilterDropdown(
            RuntimeWatchDropdownFilter filter,
            string label,
            string current,
            IReadOnlyList<string> options,
            float width)
        {
            bool currentIsValid = false;
            if (options != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    if (string.Equals(options[i], current, StringComparison.Ordinal))
                    {
                        currentIsValid = true;
                        break;
                    }
                }
            }

            string normalizedCurrent = currentIsValid ? current : "全部";
            const float LabelWidth = 36f;
            GUILayout.Label(label, GUILayout.Width(LabelWidth));
            Rect anchorRect = GUILayoutUtility.GetRect(
                new GUIContent(normalizedCurrent + " ▼"),
                EditorStyles.miniButton,
                GUILayout.Width(Mathf.Max(84f, width - LabelWidth)),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUI.Button(anchorRect, new GUIContent(normalizedCurrent + " ▼", "搜索并选择" + label + "筛选"), EditorStyles.miniButton))
                ShowRuntimeWatchFilterMenu(anchorRect, filter, label, normalizedCurrent, options);

            return normalizedCurrent;
        }

        private void ShowRuntimeWatchFilterMenu(
            Rect anchorRect,
            RuntimeWatchDropdownFilter filter,
            string label,
            string current,
            IReadOnlyList<string> options)
        {
            int optionCount = options != null ? options.Count : 0;
            var entries = new List<ESSearchDropdown.Entry>(optionCount);
            for (int i = 0; i < optionCount; i++)
            {
                string option = options[i];
                string captured = option;
                int matchCount = CountRuntimeWatchFilterMatches(filter, captured);
                entries.Add(ESSearchDropdown.Entry.Item(
                    captured,
                    () => ApplyRuntimeWatchFilter(filter, captured),
                    subtitle: matchCount + " 个观察项",
                    keywords: captured,
                    badge: string.Equals(captured, current, StringComparison.Ordinal) ? "当前" : null,
                    selected: string.Equals(captured, current, StringComparison.Ordinal)));
            }

            ESSearchDropdown.Open(
                anchorRect,
                "选择" + label + "筛选",
                entries,
                minimumWindowSize: new Vector2(320f, 300f),
                hostWindow: SimpleToolsWindow.UsingWindow);
        }

        private int CountRuntimeWatchFilterMatches(RuntimeWatchDropdownFilter filter, string value)
        {
            return entries.Count(entry => MatchesRuntimeWatchFilter(entry, filter, value));
        }

        private static bool MatchesRuntimeWatchFilter(WatchEntry entry, RuntimeWatchDropdownFilter filter, string value)
        {
            if (entry == null || IsAllFilter(value))
                return entry != null;

            string entryValue;
            switch (filter)
            {
                case RuntimeWatchDropdownFilter.Category:
                    entryValue = string.IsNullOrWhiteSpace(entry.Category)
                        ? ESRuntimeWatchAttribute.CategoryNone
                        : entry.Category;
                    break;
                case RuntimeWatchDropdownFilter.Script:
                    entryValue = entry.ScriptTypeName;
                    break;
                default:
                    entryValue = entry.GameObjectName;
                    break;
            }

            return string.Equals(entryValue, value, StringComparison.Ordinal);
        }

        private void ApplyRuntimeWatchFilter(RuntimeWatchDropdownFilter filter, string value)
        {
            string selectedValue = string.IsNullOrWhiteSpace(value) ? "全部" : value;
            switch (filter)
            {
                case RuntimeWatchDropdownFilter.Category:
                    selectedCategoryFilter = selectedValue;
                    break;
                case RuntimeWatchDropdownFilter.Script:
                    selectedScriptFilter = selectedValue;
                    break;
                default:
                    selectedObjectFilter = selectedValue;
                    break;
            }

            currentPage = 0;
            scroll.y = 0f;
            lastResultSummary = "RuntimeWatch " + GetRuntimeWatchFilterDisplayName(filter) + "筛选：" + selectedValue;
            lastResultDetail = "当前命中 " + CountVisibleEntries() + " 个观察项。";
            SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private static string GetRuntimeWatchFilterDisplayName(RuntimeWatchDropdownFilter filter)
        {
            switch (filter)
            {
                case RuntimeWatchDropdownFilter.Category:
                    return "分类";
                case RuntimeWatchDropdownFilter.Script:
                    return "脚本";
                default:
                    return "对象";
            }
        }

        private void DrawEntries(int visibleCount)
        {
            string activeGroup = null;
            string activeOwnerKey = null;
            bool groupVisible = false;
            bool ownerVisible = false;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(visibleCount / (float)Mathf.Max(1, pageSize)));
            currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
            int firstVisibleIndex = currentPage * pageSize;
            int lastVisibleIndex = firstVisibleIndex + pageSize;
            int filteredIndex = 0;

            foreach (WatchEntry entry in renderEntries)
            {
                if (!MatchesSearch(entry))
                    continue;

                int currentFilteredIndex = filteredIndex++;
                if (currentFilteredIndex < firstVisibleIndex || currentFilteredIndex >= lastVisibleIndex)
                    continue;

                if (activeGroup != entry.Group)
                {
                    activeGroup = entry.Group;
                    if (!groupFoldouts.ContainsKey(activeGroup))
                        groupFoldouts[activeGroup] = defaultFoldoutExpanded;

                    EditorGUILayout.Space(8);
                    groupFoldouts[activeGroup] = DrawRuntimeWatchGroupHeader(
                        groupFoldouts[activeGroup],
                        "分类 · " + activeGroup);
                    groupVisible = groupFoldouts[activeGroup];
                    activeOwnerKey = null;
                    ownerVisible = false;
                }

                if (!groupVisible)
                    continue;

                string ownerFoldoutKey = entry.Group + "|" + entry.OwnerKey;
                if (activeOwnerKey != ownerFoldoutKey)
                {
                    activeOwnerKey = ownerFoldoutKey;
                    if (!ownerFoldouts.ContainsKey(activeOwnerKey))
                        ownerFoldouts[activeOwnerKey] = defaultFoldoutExpanded;

                    ownerFoldouts[activeOwnerKey] = DrawRuntimeWatchOwnerHeader(
                        entry,
                        ownerFoldouts[activeOwnerKey]);

                    ownerVisible = ownerFoldouts[activeOwnerKey];
                }

                if (!ownerVisible)
                    continue;

                DrawRuntimeWatchEntryDivider(28f);
                DrawRuntimeWatchEntry(entry);
            }
        }

        private bool DrawRuntimeWatchGroupHeader(bool expanded, string label)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true));
            DrawRuntimeWatchHeaderBackground(rect, new Color(0.22f, 0.58f, 0.86f, 0.78f));
            Rect foldoutRect = new Rect(rect.x + 9f, rect.y + 3f, Mathf.Max(40f, rect.width - 16f), rect.height - 4f);
            return EditorGUI.Foldout(foldoutRect, expanded, label, true, RuntimeWatchGroupHeaderStyle);
        }

        private bool DrawRuntimeWatchOwnerHeader(WatchEntry entry, bool expanded)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            bool narrow = rect.width < 460f;
            float actionWidth = narrow ? 28f : 44f;
            const float indent = 14f;
            DrawRuntimeWatchHeaderBackground(rect, new Color(0.56f, 0.68f, 0.78f, 0.52f));

            Rect copyRect = new Rect(rect.xMax - actionWidth - 4f, rect.y + 2f, actionWidth, rect.height - 4f);
            Rect locateRect = new Rect(copyRect.x - actionWidth - 3f, rect.y + 2f, actionWidth, rect.height - 4f);
            Rect foldoutRect = new Rect(
                rect.x + indent + 7f,
                rect.y + 2f,
                Mathf.Max(40f, locateRect.x - rect.x - indent - 12f),
                rect.height - 3f);
            bool nextExpanded = EditorGUI.Foldout(foldoutRect, expanded, entry.OwnerName, true, RuntimeWatchOwnerHeaderStyle);

            GUIContent locateContent = narrow
                ? new GUIContent("◎", "定位此观察对象")
                : new GUIContent("定位", "定位此观察对象");
            if (GUI.Button(locateRect, locateContent, EditorStyles.miniButton))
            {
                UnityEngine.Object target = entry.SceneObject != null ? entry.SceneObject : entry.Owner;
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
                lastResultSummary = $"已定位观察对象: {entry.OwnerName}";
                lastResultDetail = $"分组: {entry.Group}\n字段: {entry.Label}\n路径: {entry.MemberPath}\n类型: {entry.OwnerTypeName}";
            }

            GUIContent copyContent = narrow
                ? new GUIContent("⧉", "复制当前观察项")
                : new GUIContent("复制", "复制当前观察项");
            if (GUI.Button(copyRect, copyContent, EditorStyles.miniButton))
            {
                EditorGUIUtility.systemCopyBuffer = $"{entry.OwnerName}\n{entry.MemberPath}\n{entry.GetCachedValue()}";
                lastResultSummary = $"已复制观察项: {entry.Label}";
                lastResultDetail = $"{entry.OwnerName}\n{entry.Group}\n{entry.MemberPath}";
            }

            return nextExpanded;
        }

        private void DrawRuntimeWatchEntry(WatchEntry entry)
        {
            bool narrow = EditorGUIUtility.currentViewWidth < 760f;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(28);
                DrawPinButton(entry);
                DrawMemberKindBadge(entry);
                DrawCapabilityBadge(entry);
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawMemberLabel(entry, narrow ? 92f : 120f);
                        GUILayout.FlexibleSpace();
                        string value = entry.GetCachedValue();
                        Color previousContentColor = GUI.contentColor;
                        if (entry.LastReadFailed)
                            GUI.contentColor = new Color(1f, 0.42f, 0.42f);
                        else if (entry.IsRecentlyChanged)
                            GUI.contentColor = new Color(1f, 0.82f, 0.28f);
                        EditorGUILayout.SelectableLabel(
                            BuildCompactValue(value),
                            GUILayout.Height(EditorGUIUtility.singleLineHeight),
                            GUILayout.Width(narrow || compactView ? 96 : 150));
                        GUI.contentColor = previousContentColor;
                    }

                    DrawMemberSummary(entry);
                }

                if (!narrow && entry.IsActionable)
                    DrawRuntimeWatchEntryAction(entry, false);
            }

            if (narrow && entry.IsActionable)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(28);
                    DrawRuntimeWatchEntryAction(entry, true);
                }
            }

            DrawEntryOperationFeedback(entry);
        }

        private void DrawEntryOperationFeedback(WatchEntry entry)
        {
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.StateKey)
                || !string.Equals(entry.StateKey, lastActionEntryKey, StringComparison.Ordinal)
                || !lastActionFeedbackVisible
                || string.IsNullOrWhiteSpace(lastActionFeedback))
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(28);
                Color previous = GUI.contentColor;
                GUI.contentColor = lastActionFailed
                    ? new Color(1f, 0.42f, 0.42f)
                    : new Color(0.44f, 0.82f, 0.58f);
                GUILayout.Label(
                    new GUIContent("最近操作 · " + BuildCompactOperationFeedback(lastActionFeedback), lastActionFeedback),
                    EditorStyles.miniLabel);
                GUI.contentColor = previous;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("详情", "在底部“诊断与链路报告”中查看完整操作反馈。"), EditorStyles.miniButton, GUILayout.Width(42)))
                    RequestDiagnosticsDisplay();
            }
        }

        private static string BuildCompactOperationFeedback(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            const int maxLength = 96;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 1) + "…";
        }

        private void DrawRuntimeWatchEntryAction(WatchEntry entry, bool fullWidth)
        {
            using (new EditorGUILayout.VerticalScope(fullWidth ? GUILayout.ExpandWidth(true) : GUILayout.Width(compactView ? 180f : 240f)))
            {
                bool inlineHandled = DrawInlineControl(entry, fullWidth);
                if (!inlineHandled && entry.HasManualAction)
                    DrawRuntimeWatchActionButton(entry, fullWidth ? 0 : (compactView ? 96 : 128), fullWidth);
            }
        }

        private static void DrawRuntimeWatchEntryDivider(float indent)
        {
            Rect divider = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint)
                return;

            divider.xMin += Mathf.Min(indent, Mathf.Max(0f, divider.width - 8f));
            EditorGUI.DrawRect(
                divider,
                EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.07f)
                    : new Color(0f, 0f, 0f, 0.08f));
        }

        private static void DrawRuntimeWatchHeaderBackground(Rect rect, Color accent)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            Color surface = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.21f, 0.25f, 0.78f)
                : new Color(0.78f, 0.82f, 0.87f, 0.7f);
            EditorGUI.DrawRect(rect, surface);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), accent);
            EditorGUI.DrawRect(
                new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
                EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.11f)
                    : new Color(0f, 0f, 0f, 0.12f));
        }

        private static void DrawMemberKindBadge(WatchEntry entry)
        {
            if (entry == null)
                return;

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = entry.MemberKindColor;
            GUILayout.Label(entry.MemberKindLabel, EditorStyles.miniButton, GUILayout.Width(60));
            GUI.backgroundColor = previous;
        }

        private static void DrawMemberLabel(WatchEntry entry, float minimumWidth)
        {
            if (entry == null)
                return;

            Color previous = GUI.contentColor;
            GUI.contentColor = entry.MemberKindColor;
            GUILayout.Label(entry.Label, RuntimeWatchMemberNameStyle, GUILayout.MinWidth(minimumWidth));
            GUI.contentColor = previous;
        }

        private static void DrawMemberSummary(WatchEntry entry)
        {
            if (entry == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrEmpty(entry.MemberPathPrefix))
                    GUILayout.Label(entry.MemberPathPrefix, RuntimeWatchPathStyle);

                Color previous = GUI.contentColor;
                GUI.contentColor = entry.MemberKindColor;
                GUILayout.Label(entry.MemberCodeName, RuntimeWatchMemberNameStyle);
                GUI.contentColor = previous;

                string sampleState;
                if (entry.LastReadFailed)
                    sampleState = " · 读取失败";
                else if (entry.LastReadDurationMs >= SlowGetterThresholdMs)
                    sampleState = " · 慢读取 " + entry.LastReadDurationMs.ToString("0.00", CultureInfo.InvariantCulture) + "ms";
                else if (entry.IsRecentlyChanged)
                    sampleState = " · 最近变化";
                else if (entry.HasManualAction)
                    sampleState = " · 手动执行";
                else
                    sampleState = string.Empty;

                if (entry.LastReadFailed)
                    GUI.contentColor = new Color(1f, 0.42f, 0.42f);
                else if (entry.LastReadDurationMs >= SlowGetterThresholdMs)
                    GUI.contentColor = new Color(1f, 0.73f, 0.25f);
                GUILayout.Label(entry.MemberTypeSuffix + sampleState, RuntimeWatchPathStyle);
                GUI.contentColor = previous;
            }
        }

        private void DrawPinButton(WatchEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.StateKey))
                return;

            bool pinned = pinnedEntryKeys.Contains(entry.StateKey);
            Color previous = GUI.contentColor;
            if (pinned)
                GUI.contentColor = new Color(1f, 0.76f, 0.22f);
            if (GUILayout.Button(pinned ? "★" : "☆", EditorStyles.miniButton, GUILayout.Width(24)))
            {
                if (pinned)
                    pinnedEntryKeys.Remove(entry.StateKey);
                else
                    pinnedEntryKeys.Add(entry.StateKey);
                SavePinnedEntries();
            }
            GUI.contentColor = previous;
        }

        private void EnsurePinnedEntriesLoaded()
        {
            if (pinnedEntriesLoaded)
                return;

            pinnedEntriesLoaded = true;
            string saved = EditorPrefs.GetString(PinnedEntriesPrefsKey, string.Empty);
            foreach (string key in saved.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                pinnedEntryKeys.Add(key);
        }

        private void SavePinnedEntries()
        {
            EditorPrefs.SetString(PinnedEntriesPrefsKey, string.Join("\n", pinnedEntryKeys.OrderBy(key => key, StringComparer.Ordinal)));
        }

        private static void DrawCapabilityBadge(WatchEntry entry)
        {
            if (entry == null)
                return;

            string label = null;
            Color color = Color.gray;
            if (entry.MemberInfo is MethodInfo)
            {
                label = "执行";
                color = new Color(0.88f, 0.42f, 0.28f);
            }
            else if (TryGetEditableValueType(entry.MemberInfo, out _))
            {
                label = "可写";
                color = new Color(0.85f, 0.68f, 0.22f);
            }

            if (label == null)
                return;

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(label, EditorStyles.miniButton, GUILayout.Width(42));
            GUI.backgroundColor = previous;
        }

        private static string BuildCompactValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            const int maxLength = 40;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 1) + "…";
        }

        private int CountVisibleEntries()
        {
            int count = 0;
            for (int i = 0; i < renderEntries.Count; i++)
            {
                if (MatchesSearch(renderEntries[i]))
                    count++;
            }

            return count;
        }

        private void PrepareRenderEntries()
        {
            if (Event.current == null || Event.current.type != EventType.Layout)
                return;

            if (renderEntriesVersion != entriesVersion)
            {
                renderEntries.Clear();
                renderEntries.AddRange(entries);
                renderEntriesVersion = entriesVersion;
            }

            renderEvidenceIsRecording = evidenceRecorder.IsRecording;
            renderEvidenceHasEvidence = evidenceRecorder.HasEvidence;
            renderEvidenceEventCount = evidenceRecorder.EventCount;
            renderEvidenceScopeLabel = evidenceRecorder.ScopeLabel;
            renderEvidenceEvents.Clear();
            int shownCount = Math.Min(renderEvidenceEventCount, 64);
            for (int i = 0; i < shownCount; i++)
            {
                RuntimeWatchEvidenceRecorder.RecordedEvent recordedEvent = evidenceRecorder.GetNewestEvent(i);
                if (recordedEvent != null)
                    renderEvidenceEvents.Add(recordedEvent);
            }
        }

        private void DetectSelectedScopeChange()
        {
            if (!onlySelectedGameObject)
                return;

            if (sampledSelectionInstanceId != GetCurrentSelectionInstanceId())
                MarkSnapshotStale();
        }

        private static int GetCurrentSelectionInstanceId()
        {
            GameObject selected = Selection.activeGameObject;
            return selected != null ? selected.GetInstanceID() : 0;
        }

        private void MarkSnapshotStale()
        {
            snapshotRequiresRefresh = true;
        }

        private string BuildSnapshotStatusLabel()
        {
            if (snapshotRequiresRefresh)
                return "范围或规则已变 · 请刷新";

            if (lastRefreshAt <= 0d)
                return "尚未获取";

            double elapsed = Math.Max(0d, EditorApplication.timeSinceStartup - lastRefreshAt);
            return lastRefreshReason + " · " + elapsed.ToString("0.0", CultureInfo.InvariantCulture)
                   + "s 前 · " + lastScanDurationMs.ToString("0.0", CultureInfo.InvariantCulture) + "ms";
        }

        private void ClearWatchFilters()
        {
            searchText = string.Empty;
            selectedCategoryFilter = "全部";
            selectedObjectFilter = "全部";
            selectedScriptFilter = "全部";
            onlySelectedGameObject = false;
            includeSelectedChildren = true;
            enableTagFilter = true;
            enableShowIfFilter = true;
            allowGetMoudleFallback = false;
            viewFilter = RuntimeWatchViewFilter.All;
            currentPage = 0;
            MarkSnapshotStale();
            lastResultSummary = "已清除 RuntimeWatch 筛选";
            lastResultDetail = "已恢复全部范围、分类、对象、脚本、文本和结果视图。采样规则已还原，请刷新快照。";
        }

        private void ShowQuickJumpMenu(Rect anchorRect)
        {
            var candidates = entries
                .Where(entry => entry != null)
                .OrderBy(entry => entry.Group, StringComparer.Ordinal)
                .ThenBy(entry => entry.OwnerName, StringComparer.Ordinal)
                .ThenBy(entry => entry.Label, StringComparer.Ordinal)
                .ToList();
            var options = new List<ESSearchDropdown.Entry>(candidates.Count);

            if (candidates.Count == 0)
            {
                options.Add(ESSearchDropdown.Entry.Disabled("当前没有已收集的观察项"));
            }
            else
            {
                foreach (WatchEntry candidate in candidates)
                {
                    WatchEntry captured = candidate;
                    string group = string.IsNullOrWhiteSpace(candidate.Group) ? "未分组" : candidate.Group;
                    string category = string.IsNullOrWhiteSpace(candidate.Category) ? "未分类" : candidate.Category;
                    Texture2D icon = candidate.SceneObject != null
                        ? EditorGUIUtility.ObjectContent(candidate.SceneObject, candidate.SceneObject.GetType()).image as Texture2D
                        : null;
                    options.Add(ESSearchDropdown.Entry.Item(
                        candidate.Label,
                        () => FocusWatchEntry(captured),
                        group + "/" + category,
                        icon,
                        subtitle: candidate.OwnerName + " · " + candidate.ScriptTypeName + " · " + candidate.MemberPath,
                        tooltip: candidate.GameObjectName,
                        badge: candidate.LastReadFailed ? "读取失败" : candidate.MemberKindLabel,
                        selected: string.Equals(searchText, candidate.MemberPath, StringComparison.Ordinal)));
                }
            }

            ESSearchDropdown.Open(
                anchorRect,
                "快速跳转 RuntimeWatch 观察项",
                options,
                minimumWindowSize: new Vector2(480f, 400f),
                hostWindow: SimpleToolsWindow.UsingWindow);
        }

        private void FocusWatchEntry(WatchEntry entry)
        {
            if (entry == null)
                return;

            selectedCategoryFilter = string.IsNullOrWhiteSpace(entry.Category) ? "全部" : entry.Category;
            selectedObjectFilter = string.IsNullOrWhiteSpace(entry.GameObjectName) ? "全部" : entry.GameObjectName;
            selectedScriptFilter = string.IsNullOrWhiteSpace(entry.ScriptTypeName) ? "全部" : entry.ScriptTypeName;
            onlySelectedGameObject = false;
            searchText = entry.MemberPath ?? entry.Label ?? string.Empty;
            string group = entry.Group ?? "Default";
            string ownerKey = group + "|" + entry.OwnerKey;
            groupFoldouts[group] = true;
            ownerFoldouts[ownerKey] = true;

            UnityEngine.Object target = entry.SceneObject != null ? entry.SceneObject : entry.Owner;
            if (target != null)
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }

            lastResultSummary = "已跳转到 RuntimeWatch 观察项: " + entry.Label;
            lastResultDetail = $"对象: {entry.OwnerName}\n分组: {entry.Group}\n路径: {entry.MemberPath}";
        }

        private void ShowGameObjectJumpMenu(Rect anchorRect)
        {
            var objectGroups = entries
                .Where(entry => entry != null && entry.SceneObject != null)
                .GroupBy(entry => entry.SceneObject.GetInstanceID())
                .Select(group => new
                {
                    SceneObject = group.First().SceneObject,
                    GameObjectPath = group.First().GameObjectName,
                    SceneName = group.First().SceneObject.scene.IsValid()
                        ? group.First().SceneObject.scene.name
                        : "未加载场景",
                    EntryCount = group.Count(),
                    ScriptCount = group.Select(entry => entry.ScriptTypeName).Distinct().Count()
                })
                .OrderBy(item => item.SceneName, StringComparer.Ordinal)
                .ThenBy(item => item.GameObjectPath, StringComparer.Ordinal)
                .ToList();
            var options = new List<ESSearchDropdown.Entry>(objectGroups.Count);

            if (objectGroups.Count == 0)
            {
                options.Add(ESSearchDropdown.Entry.Disabled("当前没有包含 RuntimeWatch 的 GameObject"));
            }
            else
            {
                foreach (var item in objectGroups)
                {
                    GameObject capturedObject = item.SceneObject;
                    string capturedPath = item.GameObjectPath;
                    Texture2D icon = EditorGUIUtility.ObjectContent(capturedObject, typeof(GameObject)).image as Texture2D;
                    options.Add(ESSearchDropdown.Entry.Item(
                        item.GameObjectPath,
                        () => FocusWatchGameObject(capturedObject, capturedPath),
                        item.SceneName,
                        icon,
                        subtitle: item.EntryCount + " 个观察项 · " + item.ScriptCount + " 个脚本",
                        badge: capturedObject.activeInHierarchy ? "活动" : "未激活",
                        selected: Selection.activeGameObject == capturedObject));
                }
            }

            ESSearchDropdown.Open(
                anchorRect,
                "按 GameObject 跳转 RuntimeWatch",
                options,
                minimumWindowSize: new Vector2(460f, 390f),
                hostWindow: SimpleToolsWindow.UsingWindow);
        }

        private void FocusWatchGameObject(GameObject sceneObject, string gameObjectPath)
        {
            if (sceneObject == null)
                return;

            searchText = string.Empty;
            selectedCategoryFilter = "全部";
            selectedObjectFilter = string.IsNullOrWhiteSpace(gameObjectPath) ? "全部" : gameObjectPath;
            selectedScriptFilter = "全部";
            onlySelectedGameObject = false;

            foreach (WatchEntry entry in entries)
            {
                if (entry == null || entry.SceneObject != sceneObject)
                    continue;

                string group = entry.Group ?? "Default";
                groupFoldouts[group] = true;
                ownerFoldouts[group + "|" + entry.OwnerKey] = true;
            }

            Selection.activeGameObject = sceneObject;
            EditorGUIUtility.PingObject(sceneObject);
            int count = entries.Count(entry => entry != null && entry.SceneObject == sceneObject);
            lastResultSummary = "已跳转到 RuntimeWatch GameObject: " + sceneObject.name;
            lastResultDetail = $"路径: {gameObjectPath}\n观察项: {count}";
        }

        private static GUIStyle runtimeWatchPathStyle;
        private static GUIStyle runtimeWatchMemberNameStyle;
        private static GUIStyle runtimeWatchGroupHeaderStyle;
        private static GUIStyle runtimeWatchOwnerHeaderStyle;
        private static GUIStyle RuntimeWatchPathStyle
        {
            get
            {
                if (runtimeWatchPathStyle == null)
                {
                    runtimeWatchPathStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        clipping = TextClipping.Clip
                    };
                }

                return runtimeWatchPathStyle;
            }
        }

        private static GUIStyle RuntimeWatchMemberNameStyle
        {
            get
            {
                if (runtimeWatchMemberNameStyle == null)
                {
                    runtimeWatchMemberNameStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                    {
                        clipping = TextClipping.Clip
                    };
                }

                return runtimeWatchMemberNameStyle;
            }
        }

        private static GUIStyle RuntimeWatchGroupHeaderStyle
        {
            get
            {
                if (runtimeWatchGroupHeaderStyle == null)
                {
                    runtimeWatchGroupHeaderStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold,
                        fontSize = 12,
                        clipping = TextClipping.Clip,
                        alignment = TextAnchor.MiddleLeft
                    };
                }

                return runtimeWatchGroupHeaderStyle;
            }
        }

        private static GUIStyle RuntimeWatchOwnerHeaderStyle
        {
            get
            {
                if (runtimeWatchOwnerHeaderStyle == null)
                {
                    runtimeWatchOwnerHeaderStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold,
                        clipping = TextClipping.Clip,
                        alignment = TextAnchor.MiddleLeft
                    };
                }

                return runtimeWatchOwnerHeaderStyle;
            }
        }

        private void DrawRuntimeWatchActionButton(WatchEntry entry, int width, bool expandWidth)
        {
            bool invoked = expandWidth
                ? GUILayout.Button(entry.ActionButtonLabel, EditorStyles.miniButton, GUILayout.ExpandWidth(true))
                : GUILayout.Button(entry.ActionButtonLabel, EditorStyles.miniButton, GUILayout.Width(width));
            if (!invoked)
                return;

            if (!ConfirmMethodAction(entry))
                return;

            string invokeResult = entry.InvokeManualAction();
            RequestDeferredRefresh();
            SetEntryOperationResult(
                entry,
                $"已执行: {entry.Label}",
                invokeResult,
                invokeResult.StartsWith("执行失败", StringComparison.Ordinal));
        }

        private void SetEntryOperationResult(WatchEntry entry, string summary, string detail, bool failed)
        {
            lastActionEntryKey = entry != null ? entry.StateKey ?? string.Empty : string.Empty;
            lastActionFeedback = string.IsNullOrWhiteSpace(detail) ? summary : detail;
            lastActionFailed = failed;
            // 鼠标事件发生在已有 Layout 之后时，不能立即增减 GUILayout 行。
            // 延后一帧显示反馈，确保新的控制树先经历完整 Layout，再进入 Repaint。
            lastActionFeedbackVisible = false;
            EditorApplication.delayCall -= ShowDeferredActionFeedback;
            EditorApplication.delayCall += ShowDeferredActionFeedback;
            lastResultSummary = summary ?? string.Empty;
            lastResultDetail = detail ?? string.Empty;
        }

        private void ShowDeferredActionFeedback()
        {
            lastActionFeedbackVisible = true;
            SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private void RequestDiagnosticsDisplay()
        {
            EditorApplication.delayCall -= ShowDeferredDiagnostics;
            EditorApplication.delayCall += ShowDeferredDiagnostics;
        }

        private void ShowDeferredDiagnostics()
        {
            showDiagnostics = true;
            SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private bool ConfirmMethodAction(WatchEntry entry)
        {
            if (!confirmMethodActions || entry == null || !(entry.MemberInfo is MethodInfo))
                return true;

            return EditorUtility.DisplayDialog(
                "执行 RuntimeWatch 方法",
                $"对象：{entry.OwnerName}\n方法：{entry.MemberPath}\n\n该操作可能修改运行时状态，确定继续？",
                "执行",
                "取消");
        }

        private void CollectEntries(bool recordResult, string refreshReason = null)
        {
            var scanStopwatch = Stopwatch.StartNew();
            entries.Clear();
            IReadOnlyList<ESRuntimeWatchRegistry.Entry> registeredEntries = ESRuntimeWatchRegistry.Entries;
            IReadOnlyList<Type> ownerTypes = ESRuntimeWatchRegistry.OwnerTypes;
            HashSet<string> addedKeys = new HashSet<string>();
            List<string> ownerDiagnostics = recordResult ? new List<string>() : null;
            List<string> filterDiagnostics = recordResult ? new List<string>() : null;
            lastScannedOwnerTypeCount = ownerTypes.Count;
            lastFoundOwnerCount = 0;
            lastCandidateEntryCount = 0;
            lastNoFilterCandidateCount = 0;
            lastTagFilteredCount = 0;
            lastShowIfFilteredCount = 0;
            lastContextMissingCount = 0;
            lastDuplicateSkippedCount = 0;

            for (int typeIndex = 0; typeIndex < ownerTypes.Count; typeIndex++)
            {
                Type ownerType = ownerTypes[typeIndex];
                UnityEngine.Object[] owners = FindOwners(ownerType);
                IReadOnlyList<ESRuntimeWatchRegistry.Entry> ownerEntries = ESRuntimeWatchRegistry.GetEntriesForOwnerType(ownerType);
                int noFilterEntryCount = CountNoFilterEntries(ownerEntries);
                lastFoundOwnerCount += owners.Length;
                lastCandidateEntryCount += owners.Length * ownerEntries.Count;
                lastNoFilterCandidateCount += owners.Length * noFilterEntryCount;
                if (recordResult)
                    ownerDiagnostics.Add($"{ownerType.FullName}: 实例 {owners.Length} 个 / 注册项 {ownerEntries.Count} 个 / 无条件 {noFilterEntryCount} 个");

                for (int ownerIndex = 0; ownerIndex < owners.Length; ownerIndex++)
                {
                    MonoBehaviour behaviour = owners[ownerIndex] as MonoBehaviour;
                    if (behaviour == null)
                        continue;

                    Type behaviourType = behaviour.GetType();
                    for (int entryIndex = 0; entryIndex < ownerEntries.Count; entryIndex++)
                    {
                        ESRuntimeWatchRegistry.Entry registeredEntry = ownerEntries[entryIndex];
                        if (!registeredEntry.OwnerType.IsAssignableFrom(behaviourType))
                            continue;

                        if (enableTagFilter && !PassesRootTagFilter(behaviour, registeredEntry))
                        {
                            lastTagFilteredCount++;
                            if (recordResult && filterDiagnostics.Count < 12)
                                filterDiagnostics.Add($"Tag: {behaviour.name} | {registeredEntry.MemberPath} | 需要 {registeredEntry.RequiredTag}");
                            continue;
                        }

                        if (enableShowIfFilter && !PassesOdinShowIf(behaviour, registeredEntry, allowGetMoudleFallback, out string showIfFailReason))
                        {
                            lastShowIfFilteredCount++;
                            if (showIfFailReason == "ContextMissing")
                                lastContextMissingCount++;
                            if (recordResult && filterDiagnostics.Count < 12)
                                filterDiagnostics.Add($"ShowIf: {behaviour.name} | {registeredEntry.MemberPath} | {registeredEntry.ShowIf} | {showIfFailReason}");
                            continue;
                        }

                        string key = behaviour.GetInstanceID() + "|" + registeredEntry.EntryKey;
                        if (!addedKeys.Add(key))
                        {
                            lastDuplicateSkippedCount++;
                            continue;
                        }

                        WatchEntry watchEntry = WatchEntry.FromRegistryEntry(behaviour, registeredEntry, allowGetMoudleFallback);
                        if (!sampleStates.TryGetValue(key, out WatchSampleState sampleState))
                        {
                            sampleState = new WatchSampleState();
                            sampleStates.Add(key, sampleState);
                        }
                        watchEntry.SampleState = sampleState;
                        watchEntry.StateKey = BuildPersistentWatchKey(behaviour, registeredEntry);
                        // 采样只发生在手动刷新、前台自动刷新、录制基线或显式操作后的延迟刷新。
                        // 绘制阶段只读取 SampleState 缓存，避免 Layout/Repaint 反复执行属性和 Getter。
                        watchEntry.RefreshSample();
                        entries.Add(watchEntry);
                    }
                }
            }

            entries.Sort((a, b) =>
            {
                int group = string.Compare(a.Group, b.Group, StringComparison.Ordinal);
                if (group != 0) return group;
                int owner = string.Compare(a.OwnerName, b.OwnerName, StringComparison.Ordinal);
                return owner != 0 ? owner : string.Compare(a.Label, b.Label, StringComparison.Ordinal);
            });
            RebuildFilterOptions();
            entriesVersion++;

            if (sampleStates.Count > addedKeys.Count + 32)
            {
                foreach (string staleKey in sampleStates.Keys.Where(key => !addedKeys.Contains(key)).ToArray())
                    sampleStates.Remove(staleKey);
            }

            lastReadFailureCount = entries.Count(entry => entry.LastReadFailed);
            lastSlowGetterCount = entries.Count(entry => entry.LastReadDurationMs >= SlowGetterThresholdMs);
            lastManualActionCount = entries.Count(entry => entry.IsActionable);
            snapshotRequiresRefresh = false;
            sampledSelectionInstanceId = GetCurrentSelectionInstanceId();
            lastRefreshAt = EditorApplication.timeSinceStartup;
            lastRefreshReason = string.IsNullOrWhiteSpace(refreshReason) ? "刷新" : refreshReason;

            scanStopwatch.Stop();
            lastScanDurationMs = scanStopwatch.Elapsed.TotalMilliseconds;

            CaptureEvidenceSample();

            if (recordResult)
            {
                lastResultSummary = $"运行时观察刷新完成: 观察项 {entries.Count} 个 | 展开注册项 {registeredEntries.Count} 个 | Owner类型 {lastScannedOwnerTypeCount} 个 | 当前场景Owner {lastFoundOwnerCount} 个";
                string foundPreview = SimpleToolsSafetyUtility.JoinPreview(entries.Select(entry => $"{entry.Group} | {entry.OwnerName} | {entry.Label}"), 12);
                string ownerPreview = SimpleToolsSafetyUtility.JoinPreview(ownerDiagnostics, 16);
                string filterPreview = SimpleToolsSafetyUtility.JoinPreview(filterDiagnostics, 12);
                lastResultDetail =
                    $"当前场景: {SceneManager.GetActiveScene().name}\n" +
                    $"候选项: {lastCandidateEntryCount}\n" +
                    $"无条件候选: {lastNoFilterCandidateCount}\n" +
                    $"Tag过滤: {lastTagFilteredCount}\n" +
                    $"ShowIf过滤: {lastShowIfFilteredCount}\n" +
                    $"上下文缺失: {lastContextMissingCount}\n" +
                    $"范围: {BuildScopeLabel()}\n" +
                    $"过滤器: Tag {(enableTagFilter ? "启用" : "忽略")} / ShowIf {(enableShowIfFilter ? "启用" : "忽略")} / GetMoudle {(allowGetMoudleFallback ? "允许" : "禁止")}\n" +
                    $"重复跳过: {lastDuplicateSkippedCount}\n\n" +
                    $"Owner类型:\n{ESRuntimeWatchRegistry.OwnerTypeSummary}\n\n" +
                    $"Owner扫描:\n{ownerPreview}\n\n" +
                    $"过滤样例:\n{filterPreview}\n\n" +
                    $"显示项:\n{foundPreview}";
            }
        }

        private void CaptureEvidenceSample()
        {
            if (!evidenceRecorder.IsRecording)
                return;

            evidenceRecorder.Sample(entries, EditorApplication.timeSinceStartup);
        }

        private static int CountNoFilterEntries(IReadOnlyList<ESRuntimeWatchRegistry.Entry> ownerEntries)
        {
            if (ownerEntries == null)
                return 0;

            int count = 0;
            for (int i = 0; i < ownerEntries.Count; i++)
            {
                ESRuntimeWatchRegistry.Entry entry = ownerEntries[i];
                if (string.IsNullOrWhiteSpace(entry.RequiredTag) && string.IsNullOrWhiteSpace(entry.ShowIf))
                    count++;
            }

            return count;
        }

        private string BuildScopeLabel()
        {
            if (!onlySelectedGameObject)
                return "当前场景";

            GameObject selected = Selection.activeGameObject;
            if (selected == null)
                return "选中对象:<空>";

            return includeSelectedChildren
                ? "选中对象+" + selected.name
                : "选中对象:" + selected.name;
        }

        private static bool PassesRootTagFilter(MonoBehaviour owner, ESRuntimeWatchRegistry.Entry entry)
        {
            string requiredTag = entry.RequiredTag;
            if (string.IsNullOrWhiteSpace(requiredTag))
                return true;

            if (owner == null || owner.transform == null)
                return false;

            GameObject root = owner.transform.root != null ? owner.transform.root.gameObject : owner.gameObject;
            try
            {
                return root != null && root.CompareTag(requiredTag);
            }
            catch
            {
                return root != null && string.Equals(root.tag, requiredTag, StringComparison.Ordinal);
            }
        }

        private static bool PassesOdinShowIf(MonoBehaviour owner, ESRuntimeWatchRegistry.Entry entry, bool allowGetMoudleFallback, out string failReason)
        {
            failReason = null;
            string expression = entry.ShowIf;
            if (string.IsNullOrWhiteSpace(expression))
                return true;

            object context = WatchEntry.ResolveEntryContext(owner, entry, allowGetMoudleFallback);
            if (context == null)
            {
                failReason = "ContextMissing";
                return false;
            }

            // The common ShowIf forms (@this.flag, flag, !flag, and nested member paths)
            // do not need Odin's expression compiler. Evaluate them first so their value is
            // re-read on every RuntimeWatch tick and no cached delegate can hold stale state.
            if (TryEvaluateSimpleBoolExpression(context, expression, out bool simpleResult, out bool simpleGetterFailed))
            {
                if (!simpleResult)
                    failReason = "SimpleFalse";
                return simpleResult;
            }

            if (simpleGetterFailed)
            {
                failReason = "ShowIfGetterFailed";
                return false;
            }

            string cacheKey = context.GetType().AssemblyQualifiedName + "|" + expression;
            if (!FailedOdinBoolExpressions.Contains(cacheKey))
            {
                if (!OdinBoolExpressionCache.TryGetValue(cacheKey, out var func))
                {
                    string error = null;
                    string odinExpression = NormalizeOdinBoolExpression(expression);
                    try
                    {
                        func = ExpressionUtility.ParseExpression(odinExpression, false, context.GetType(), out error, false);
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        func = null;
                    }
                    if (func == null)
                    {
                        FailedOdinBoolExpressions.Add(cacheKey);
                        LogRuntimeWatchWarningOnce("ShowIfParse|" + cacheKey, $"[RuntimeWatch] Odin ShowIf 表达式解析失败，简单布尔表达式也无法处理: {expression}\nContext: {context.GetType().Name}\n{error}");
                    }
                    else
                    {
                        OdinBoolExpressionCache[cacheKey] = func;
                    }
                }

                if (func != null)
                {
                    try
                    {
                        object rawResult = func.DynamicInvoke(context);
                        bool result = rawResult is bool boolResult && boolResult;
                        if (!result)
                            failReason = "OdinFalse";
                        return result;
                    }
                    catch (Exception ex)
                    {
                        failReason = "OdinInvokeFailed";
                        LogRuntimeWatchWarningOnce("ShowIfInvoke|" + cacheKey, $"[RuntimeWatch] Odin ShowIf 表达式执行失败: {expression}\nContext: {context.GetType().Name}\n{ex.Message}");
                        return false;
                    }
                }
            }

            if (TryEvaluateSimpleBoolExpression(context, expression, out bool fallbackSimpleResult, out bool fallbackGetterFailed))
            {
                if (!fallbackSimpleResult)
                    failReason = "SimpleFallbackFalse";
                return fallbackSimpleResult;
            }

            if (fallbackGetterFailed)
            {
                failReason = "ShowIfGetterFailed";
                return false;
            }

            failReason = FailedOdinBoolExpressions.Contains(cacheKey) ? "OdinParseFailed" : "OdinUnknownFailed";
            return false;
        }

        private static string NormalizeOdinBoolExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return expression;

            string normalized = expression.Trim();
            normalized = normalized.Replace("@this.", string.Empty);
            if (normalized == "@this")
                normalized = "this";

            return normalized;
        }

        private static bool TryEvaluateSimpleBoolExpression(object context, string expression, out bool result, out bool getterFailed)
        {
            result = false;
            getterFailed = false;
            if (context == null || string.IsNullOrWhiteSpace(expression))
                return false;

            string path = expression.Trim();
            bool invert = false;
            while (path.StartsWith("!", StringComparison.Ordinal))
            {
                invert = !invert;
                path = path.Substring(1).TrimStart();
            }

            if (path.StartsWith("@this.", StringComparison.Ordinal))
                path = path.Substring("@this.".Length);
            else if (path.StartsWith("this.", StringComparison.Ordinal))
                path = path.Substring("this.".Length);
            else if (path == "@this" || path == "this")
                return TryConvertToBool(context, invert, out result);
            else if (path.StartsWith("@", StringComparison.Ordinal))
                return false;

            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (!TryReadSimpleMemberPath(context, path, out object value, out getterFailed))
                return false;

            return TryConvertToBool(value, invert, out result);
        }

        private static bool TryReadSimpleMemberPath(object context, string memberPath, out object value, out bool getterFailed)
        {
            value = null;
            getterFailed = false;
            object current = context;
            string[] members = memberPath.Split('.');
            for (int i = 0; i < members.Length; i++)
            {
                if (current == null)
                    return false;

                string member = members[i];
                if (string.IsNullOrWhiteSpace(member))
                    return false;

                Type currentType = current.GetType();
                FieldInfo field = currentType.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    try
                    {
                        current = field.GetValue(current);
                    }
                    catch
                    {
                        getterFailed = true;
                        return false;
                    }
                    continue;
                }

                PropertyInfo property = currentType.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        current = property.GetValue(current);
                    }
                    catch
                    {
                        getterFailed = true;
                        return false;
                    }
                    continue;
                }

                return false;
            }

            value = current;
            return true;
        }

        private static bool TryConvertToBool(object value, bool invert, out bool result)
        {
            result = false;
            if (value == null)
                return false;

            if (value is bool boolValue)
            {
                result = invert ? !boolValue : boolValue;
                return true;
            }

            if (value is string stringValue && bool.TryParse(stringValue, out bool parsedBool))
            {
                result = invert ? !parsedBool : parsedBool;
                return true;
            }

            return false;
        }

        private UnityEngine.Object[] FindOwners(Type ownerType)
        {
            if (ownerType == null || !typeof(Component).IsAssignableFrom(ownerType))
                return Array.Empty<UnityEngine.Object>();

            if (onlySelectedGameObject)
                return FindOwnersInSelectedGameObject(ownerType);

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return Array.Empty<UnityEngine.Object>();

            var result = new List<UnityEngine.Object>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;

                foreach (var component in root.GetComponentsInChildren(ownerType, true))
                {
                    if (component != null
                        && component.gameObject.scene == scene
                        && (component.hideFlags & HideFlags.HideInHierarchy) == 0)
                    {
                        result.Add(component);
                    }
                }
            }
            return result.ToArray();
        }

        private UnityEngine.Object[] FindOwnersInSelectedGameObject(Type ownerType)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
                return Array.Empty<UnityEngine.Object>();

            if (!includeSelectedChildren)
            {
                Component component = selected.GetComponent(ownerType);
                return component != null ? new UnityEngine.Object[] { component } : Array.Empty<UnityEngine.Object>();
            }

            return selected.GetComponentsInChildren(ownerType, true)
                .Where(component => component != null && (component.hideFlags & HideFlags.HideInHierarchy) == 0)
                .Cast<UnityEngine.Object>()
                .ToArray();
        }

        private bool MatchesSearch(WatchEntry entry)
        {
            if (!MatchesDropdownFilters(entry))
                return false;

            if (!MatchesViewFilter(entry))
                return false;

            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            return ContainsIgnoreCase(entry.Group, searchText)
                   || ContainsIgnoreCase(entry.Category, searchText)
                   || ContainsIgnoreCase(entry.OwnerName, searchText)
                   || ContainsIgnoreCase(entry.Label, searchText)
                   || ContainsIgnoreCase(entry.MemberPath, searchText)
                   || ContainsIgnoreCase(entry.OwnerTypeName, searchText)
                   || ContainsIgnoreCase(entry.ScriptTypeName, searchText)
                   || ContainsIgnoreCase(entry.GameObjectName, searchText)
                   || ContainsIgnoreCase(entry.ActionButtonLabel, searchText);
        }

        private bool MatchesViewFilter(WatchEntry entry)
        {
            if (entry == null)
                return false;

            switch (viewFilter)
            {
                case RuntimeWatchViewFilter.Changed:
                    return entry.IsRecentlyChanged;
                case RuntimeWatchViewFilter.ReadFailures:
                    return entry.LastReadFailed;
                case RuntimeWatchViewFilter.SlowGetters:
                    return entry.LastReadDurationMs >= SlowGetterThresholdMs;
                case RuntimeWatchViewFilter.Actions:
                    return entry.IsActionable;
                case RuntimeWatchViewFilter.Pinned:
                    return !string.IsNullOrWhiteSpace(entry.StateKey) && pinnedEntryKeys.Contains(entry.StateKey);
                default:
                    return true;
            }
        }

        private static string BuildPersistentWatchKey(MonoBehaviour owner, ESRuntimeWatchRegistry.Entry entry)
        {
            if (owner == null)
                return string.Empty;

            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(owner);
            return globalId + "|" + entry.EntryKey;
        }

        private bool MatchesDropdownFilters(WatchEntry entry)
        {
            if (entry == null)
                return false;

            if (!IsAllFilter(selectedCategoryFilter) && !string.Equals(entry.Category, selectedCategoryFilter, StringComparison.Ordinal))
                return false;

            if (!IsAllFilter(selectedObjectFilter) && !string.Equals(entry.GameObjectName, selectedObjectFilter, StringComparison.Ordinal))
                return false;

            if (!IsAllFilter(selectedScriptFilter) && !string.Equals(entry.ScriptTypeName, selectedScriptFilter, StringComparison.Ordinal))
                return false;

            return true;
        }

        private static bool IsAllFilter(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value == "全部";
        }

        private void RebuildFilterOptions()
        {
            categoryFilterOptions.Clear();
            scriptFilterOptions.Clear();
            objectFilterOptions.Clear();
            categoryFilterOptions.Add("全部");
            scriptFilterOptions.Add("全部");
            objectFilterOptions.Add("全部");

            for (int i = 0; i < entries.Count; i++)
            {
                WatchEntry entry = entries[i];
                if (entry == null)
                    continue;

                AddFilterOption(
                    categoryFilterOptions,
                    string.IsNullOrWhiteSpace(entry.Category) ? ESRuntimeWatchAttribute.CategoryNone : entry.Category);
                AddFilterOption(scriptFilterOptions, entry.ScriptTypeName);
                AddFilterOption(objectFilterOptions, entry.GameObjectName);
            }

            SortFilterOptions(categoryFilterOptions);
            SortFilterOptions(scriptFilterOptions);
            SortFilterOptions(objectFilterOptions);
        }

        private static void AddFilterOption(List<string> destination, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || destination.Contains(value))
                return;

            destination.Add(value);
        }

        private static void SortFilterOptions(List<string> options)
        {
            if (options == null || options.Count <= 2)
                return;

            options.Sort(1, options.Count - 1, StringComparer.Ordinal);
        }

        private static bool ContainsIgnoreCase(string value, string search)
        {
            return !string.IsNullOrEmpty(value)
                   && !string.IsNullOrEmpty(search)
                   && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool DrawInlineControl(WatchEntry entry, bool fullWidth)
        {
            MemberInfo memberInfo = entry?.MemberInfo;
            if (memberInfo == null)
                return false;

            if (memberInfo is MethodInfo methodInfo)
            {
                ParameterInfo[] parameters = methodInfo.GetParameters();
                if (parameters.Length == 1)
                {
                    string key = BuildInlineDraftKey(entry, "method");
                    string seed = GetDefaultDraftText(methodInfo, parameters[0].ParameterType);
                    string draft = GetOrCreateDraft(key, seed);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(parameters[0].ParameterType.Name, GUILayout.Width(compactView ? 44 : 58));
                        DrawEditableValueField(parameters[0].ParameterType, ref draft);
                    }
                    inlineInputDrafts[key] = draft;
                    bool invoke = fullWidth
                        ? GUILayout.Button(entry.ActionButtonLabel, EditorStyles.miniButton, GUILayout.ExpandWidth(true))
                        : GUILayout.Button(entry.ActionButtonLabel, EditorStyles.miniButton, GUILayout.Width(compactView ? 96 : 128));
                    if (invoke)
                    {
                        if (!ConfirmMethodAction(entry))
                            return true;

                        if (TryConvertTextToType(draft, parameters[0].ParameterType, out object parsedValue, out string parseError))
                        {
                            string invokeResult = InvokeMethodWithArguments(entry, new object[] { parsedValue }, allowGetMoudleFallback);
                            RequestDeferredRefresh();
                            SetEntryOperationResult(
                                entry,
                                $"已调用: {entry.Label}",
                                invokeResult,
                                invokeResult.StartsWith("执行失败", StringComparison.Ordinal));
                        }
                        else
                        {
                            SetEntryOperationResult(entry, $"方法参数解析失败: {entry.Label}", parseError, true);
                        }
                    }

                    return true;
                }
            }

            if (TryGetEditableValueType(memberInfo, out Type editableType))
            {
                string key = BuildInlineDraftKey(entry, "field");
                string draft = GetOrCreateLiveValueDraft(entry, key, editableType, allowGetMoudleFallback);
                string draftBeforeEdit = draft;
                DrawEditableValueField(editableType, ref draft);
                inlineInputDrafts[key] = draft;
                if (!string.Equals(draftBeforeEdit, draft, StringComparison.Ordinal))
                    manuallyEditedInlineDraftKeys.Add(key);
                if (GUILayout.Button("设值", EditorStyles.miniButton, GUILayout.Width(compactView ? 58 : 64)))
                {
                    if (TryConvertTextToType(draft, editableType, out object parsedValue, out string parseError))
                    {
                        bool written = TrySetEntryValue(entry, parsedValue, allowGetMoudleFallback, out string writeMessage);
                        if (written)
                        {
                            inlineInputDrafts[key] = FormatValueDraft(parsedValue, editableType);
                            manuallyEditedInlineDraftKeys.Remove(key);
                        }
                        string setResult = written ? writeMessage : BuildWriteFallback(writeMessage);
                        RequestDeferredRefresh();
                        SetEntryOperationResult(
                            entry,
                            written ? $"已设值: {entry.Label}" : $"设值失败: {entry.Label}",
                            setResult,
                            !written);
                    }
                    else
                    {
                        SetEntryOperationResult(entry, $"字段值解析失败: {entry.Label}", parseError, true);
                    }
                }

                return true;
            }

            return false;
        }

        private void RequestDeferredRefresh()
        {
            EditorApplication.delayCall += () =>
            {
                CollectEntries(false, "操作后刷新");
                if (SimpleToolsWindow.UsingWindow != null)
                    SimpleToolsWindow.UsingWindow.Repaint();
            };
        }

        private string GetOrCreateDraft(string key, string seed)
        {
            if (string.IsNullOrEmpty(key))
                return seed ?? string.Empty;

            if (!inlineInputDrafts.TryGetValue(key, out string draft))
            {
                draft = seed ?? string.Empty;
                inlineInputDrafts[key] = draft;
            }

            return draft;
        }

        private string GetOrCreateLiveValueDraft(WatchEntry entry, string key, Type valueType, bool allowGetMoudleFallback)
        {
            string seed = GetCurrentValueDraft(entry, valueType, allowGetMoudleFallback);
            if (string.IsNullOrEmpty(key))
                return seed;

            if (!inlineInputDrafts.ContainsKey(key) || !manuallyEditedInlineDraftKeys.Contains(key))
                inlineInputDrafts[key] = seed;

            return inlineInputDrafts[key];
        }

        private static string BuildInlineDraftKey(WatchEntry entry, string suffix)
        {
            return (entry?.OwnerKey ?? "<no-owner>") + "|" + (entry?.MemberPath ?? "<no-member>") + "|" + suffix;
        }

        private static bool TryGetEditableValueType(MemberInfo memberInfo, out Type valueType)
        {
            valueType = null;
            if (memberInfo is FieldInfo fieldInfo)
            {
                valueType = fieldInfo.FieldType;
                return IsSupportedInlineValueType(valueType);
            }

            if (memberInfo is PropertyInfo propertyInfo && propertyInfo.CanWrite && propertyInfo.GetIndexParameters().Length == 0)
            {
                valueType = propertyInfo.PropertyType;
                return IsSupportedInlineValueType(valueType);
            }

            return false;
        }

        private static bool IsSupportedInlineValueType(Type type)
        {
            if (type == null)
                return false;

            return type == typeof(string)
                   || type == typeof(bool)
                   || type.IsEnum
                   || type == typeof(int)
                   || type == typeof(float)
                   || type == typeof(double)
                   || type == typeof(long)
                   || type == typeof(short)
                   || type == typeof(byte)
                   || type == typeof(uint)
                   || type == typeof(ulong)
                   || type == typeof(ushort)
                   || type == typeof(sbyte);
        }

        private static string GetCurrentValueDraft(WatchEntry entry, Type valueType, bool allowGetMoudleFallback)
        {
            // 输入控件同样处于 IMGUI 重绘热路径。使用最近一次受控采样，
            // 不能为了填充输入框在 Layout/Repaint 再次读取属性或调用 Getter。
            return entry != null && entry.SampleState != null && entry.SampleState.HasValue
                ? entry.GetCachedValue()
                : string.Empty;
        }

        private static string FormatValueDraft(object value, Type valueType)
        {
            if (value == null)
                return string.Empty;

            return valueType != null && valueType.IsEnum
                ? value.ToString()
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string BuildWriteFallback(string message)
        {
            return string.IsNullOrWhiteSpace(message) ? "写回失败" : message;
        }

        private static bool TrySetEntryValue(WatchEntry entry, object value, bool allowGetMoudleFallback, out string message)
        {
            message = null;
            if (entry == null || entry.MemberInfo == null)
            {
                message = "条目无效";
                return false;
            }

            object context = WatchEntry.ResolveEntryContext(entry.Owner as MonoBehaviour, entry.RegistryEntry, allowGetMoudleFallback);
            if (context == null)
            {
                message = "条目无效";
                return false;
            }

            try
            {
                if (entry.MemberInfo is FieldInfo fieldInfo)
                {
                    if (entry.Owner is UnityEngine.Object unityObject)
                        Undo.RecordObject(unityObject, "RuntimeWatch Set Field");
                    fieldInfo.SetValue(context, value);
                    if (entry.Owner is UnityEngine.Object dirtyObject)
                    {
                        EditorUtility.SetDirty(dirtyObject);
                        if (dirtyObject is MonoBehaviour behaviour)
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(behaviour.gameObject.scene);
                    }
                    message = "字段已写回: " + entry.MemberPath;
                    return true;
                }

                if (entry.MemberInfo is PropertyInfo propertyInfo && propertyInfo.CanWrite)
                {
                    if (entry.Owner is UnityEngine.Object unityObject)
                        Undo.RecordObject(unityObject, "RuntimeWatch Set Property");
                    propertyInfo.SetValue(context, value);
                    if (entry.Owner is UnityEngine.Object dirtyObject)
                    {
                        EditorUtility.SetDirty(dirtyObject);
                        if (dirtyObject is MonoBehaviour behaviour)
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(behaviour.gameObject.scene);
                    }
                    message = "属性已写回: " + entry.MemberPath;
                    return true;
                }
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                message = "写回失败: " + inner.Message;
                return false;
            }
            catch (Exception ex)
            {
                message = "写回失败: " + ex.Message;
                return false;
            }

            message = "当前成员不支持写回";
            return false;
        }

        private static string InvokeMethodWithArguments(WatchEntry entry, object[] args, bool allowGetMoudleFallback)
        {
            if (entry == null || !(entry.MemberInfo is MethodInfo methodInfo))
                return "方法无效";

            object context = WatchEntry.ResolveEntryContext(entry.Owner as MonoBehaviour, entry.RegistryEntry, allowGetMoudleFallback);
            if (context == null)
                return "方法无效";

            try
            {
                object result = methodInfo.Invoke(context, args);
                return methodInfo.ReturnType == typeof(void) ? "执行完成" : "执行完成: " + (result == null ? "null" : result.ToString());
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                return "执行失败: " + inner.Message;
            }
            catch (Exception ex)
            {
                return "执行失败: " + ex.Message;
            }
        }

        private static string GetDefaultDraftText(MethodInfo methodInfo, Type parameterType)
        {
            if (methodInfo == null)
                return string.Empty;

            ParameterInfo[] parameters = methodInfo.GetParameters();
            if (parameters.Length == 1 && parameterType != null)
            {
                object defaultValue = parameters[0].DefaultValue;
                if (defaultValue != null && defaultValue != DBNull.Value)
                    return Convert.ToString(defaultValue, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            if (parameterType == typeof(string))
                return string.Empty;

            return string.Empty;
        }

        private static void DrawEditableValueField(Type valueType, ref string draft)
        {
            if (valueType == typeof(bool))
            {
                bool parsed = false;
                bool.TryParse(draft, out parsed);
                parsed = EditorGUILayout.ToggleLeft(parsed ? "开启" : "关闭", parsed, GUILayout.Width(74));
                draft = parsed ? "true" : "false";
                return;
            }

            if (valueType.IsEnum)
            {
                string[] names = Enum.GetNames(valueType);
                int index = Array.IndexOf(names, draft);
                if (index < 0)
                    index = 0;
                index = EditorGUILayout.Popup(index, names, GUILayout.Width(Mathf.Min(150, Mathf.Max(86, 10 * names.Max(name => name.Length)))));
                draft = names.Length > 0 ? names[Mathf.Clamp(index, 0, names.Length - 1)] : draft;
                return;
            }

            draft = EditorGUILayout.TextField(draft, GUILayout.Width(96));
        }

        private static bool TryConvertTextToType(string text, Type targetType, out object value, out string error)
        {
            value = null;
            error = null;

            if (targetType == null)
            {
                error = "目标类型为空";
                return false;
            }

            if (targetType == typeof(string))
            {
                value = text ?? string.Empty;
                return true;
            }

            if (targetType == typeof(bool))
            {
                if (bool.TryParse(text, out bool boolValue))
                {
                    value = boolValue;
                    return true;
                }

                error = "bool 只能输入 true 或 false";
                return false;
            }

            if (targetType.IsEnum)
            {
                try
                {
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long enumNumber))
                    {
                        if (!IsValidEnumNumber(targetType, enumNumber))
                        {
                            error = "枚举值不在定义范围内";
                            return false;
                        }
                    }

                    value = Enum.Parse(targetType, text, true);
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            if (targetType == typeof(byte)) return TryParseInteger<byte>(text, byte.MinValue, byte.MaxValue, v => (byte)v, out value, out error);
            if (targetType == typeof(sbyte)) return TryParseInteger<sbyte>(text, sbyte.MinValue, sbyte.MaxValue, v => (sbyte)v, out value, out error);
            if (targetType == typeof(short)) return TryParseInteger<short>(text, short.MinValue, short.MaxValue, v => (short)v, out value, out error);
            if (targetType == typeof(ushort)) return TryParseInteger<ushort>(text, ushort.MinValue, ushort.MaxValue, v => (ushort)v, out value, out error);
            if (targetType == typeof(int)) return TryParseInteger<int>(text, int.MinValue, int.MaxValue, v => (int)v, out value, out error);
            if (targetType == typeof(uint)) return TryParseUnsignedInteger<uint>(text, uint.MinValue, uint.MaxValue, v => (uint)v, out value, out error);
            if (targetType == typeof(long)) return TryParseInteger<long>(text, long.MinValue, long.MaxValue, v => v, out value, out error);
            if (targetType == typeof(ulong)) return TryParseUnsignedInteger<ulong>(text, ulong.MinValue, ulong.MaxValue, v => v, out value, out error);
            if (targetType == typeof(float)) return TryParseFloat(text, out value, out error);
            if (targetType == typeof(double)) return TryParseDouble(text, out value, out error);

            try
            {
                value = Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryParseInteger<T>(string text, long min, long max, Func<long, T> convert, out object value, out string error)
        {
            value = null;
            error = null;
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                error = "请输入整数";
                return false;
            }

            if (parsed < min || parsed > max)
            {
                error = $"数值超出范围 [{min}, {max}]";
                return false;
            }

            value = convert(parsed);
            return true;
        }

        private static bool TryParseUnsignedInteger<T>(string text, ulong min, ulong max, Func<ulong, T> convert, out object value, out string error)
        {
            value = null;
            error = null;
            if (!ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong parsed))
            {
                error = "请输入整数";
                return false;
            }

            if (parsed < min || parsed > max)
            {
                error = $"数值超出范围 [{min}, {max}]";
                return false;
            }

            value = convert(parsed);
            return true;
        }

        private static bool IsValidEnumNumber(Type enumType, long value)
        {
            if (Enum.IsDefined(enumType, Enum.ToObject(enumType, value)))
                return true;

            bool isFlags = enumType.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0;
            if (!isFlags || value < 0)
                return false;

            ulong mask = 0;
            foreach (var defined in Enum.GetValues(enumType))
                mask |= Convert.ToUInt64(defined, CultureInfo.InvariantCulture);

            ulong unsignedValue = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            return (unsignedValue & ~mask) == 0;
        }

        private static bool TryParseFloat(string text, out object value, out string error)
        {
            value = null;
            error = null;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) || float.IsNaN(parsed) || float.IsInfinity(parsed))
            {
                error = "请输入有限 float 数值";
                return false;
            }

            value = parsed;
            return true;
        }

        private static bool TryParseDouble(string text, out object value, out string error)
        {
            value = null;
            error = null;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) || double.IsNaN(parsed) || double.IsInfinity(parsed))
            {
                error = "请输入有限 double 数值";
                return false;
            }

            value = parsed;
            return true;
        }

        private static void LogRuntimeWatchWarningOnce(string key, string message)
        {
            if (string.IsNullOrWhiteSpace(key) || !LoggedRuntimeWatchWarnings.Add(key))
                return;

            UnityEngine.Debug.LogWarning(message);
        }

        private string BuildRegistryChainReport()
        {
            IReadOnlyList<ESRuntimeWatchRegistry.Entry> registryEntries = ESRuntimeWatchRegistry.Entries;
            var groups = new Dictionary<string, List<string>>
            {
                { "Core链路 / Module字段", new List<string>() },
                { "Core链路 / Module属性", new List<string>() },
                { "Core链路 / Module方法", new List<string>() },
                { "Core链路 / Domain或Core嵌套字段", new List<string>() },
                { "Core链路 / Domain或Core嵌套属性", new List<string>() },
                { "Core链路 / Domain或Core嵌套方法", new List<string>() },
                { "普通Mono / 嵌套字段", new List<string>() },
                { "普通Mono / 嵌套属性", new List<string>() },
                { "普通Mono / 嵌套方法", new List<string>() },
                { "普通Mono / 直接字段", new List<string>() },
                { "普通Mono / 直接属性", new List<string>() },
                { "普通Mono / 直接方法", new List<string>() },
                { "异常或待确认链路", new List<string>() }
            };

            for (int i = 0; i < registryEntries.Count; i++)
            {
                ESRuntimeWatchRegistry.Entry entry = registryEntries[i];
                string category = ClassifyEntry(entry);
                if (!groups.TryGetValue(category, out var lines))
                {
                    lines = groups["异常或待确认链路"];
                }

                lines.Add(BuildEntryChainLine(i, entry));
            }

            var sb = new StringBuilder(4096);
            sb.AppendLine("ESRuntimeWatch 底层完整链路报告");
            sb.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("注册成员: " + ESRuntimeWatchRegistry.RegisteredMemberCount);
            sb.AppendLine("注册字段: " + ESRuntimeWatchRegistry.RegisteredFieldCount);
            sb.AppendLine("注册属性: " + ESRuntimeWatchRegistry.RegisteredPropertyCount);
            sb.AppendLine("注册方法: " + ESRuntimeWatchRegistry.RegisteredMethodCount);
            sb.AppendLine("展开Entry: " + registryEntries.Count);
            sb.AppendLine("Owner类型: " + ESRuntimeWatchRegistry.OwnerTypes.Count);
            sb.AppendLine("路径图: " + (ESRuntimeWatchRegistry.IsFieldGraphBuilt ? ESRuntimeWatchRegistry.FieldGraphTargetTypeCount + "类/" + ESRuntimeWatchRegistry.FieldGraphEdgeCount + "边" : "未构建"));
            sb.AppendLine("路径截断: " + ESRuntimeWatchRegistry.SchemeLimitHitCount);
            sb.AppendLine("非Mono Owner拒绝: " + ESRuntimeWatchRegistry.RejectedNonMonoOwnerCount);
            sb.AppendLine("无效链路拒绝: " + ESRuntimeWatchRegistry.RejectedInvalidPathCount);
            sb.AppendLine();
            sb.AppendLine("Owner Type Info:");
            sb.AppendLine(ESRuntimeWatchRegistry.OwnerTypeSummary);
            sb.AppendLine();

            foreach (var pair in groups)
            {
                sb.AppendLine("================================================================================");
                sb.AppendLine(pair.Key + "  Count=" + pair.Value.Count);
                sb.AppendLine("================================================================================");
                if (pair.Value.Count == 0)
                {
                    sb.AppendLine("<无>");
                }
                else
                {
                    for (int i = 0; i < pair.Value.Count; i++)
                        sb.AppendLine(pair.Value[i]);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string ClassifyEntry(ESRuntimeWatchRegistry.Entry entry)
        {
            if (entry.OwnerType == null || !typeof(MonoBehaviour).IsAssignableFrom(entry.OwnerType))
                return "异常或待确认链路";

            bool coreLike = IsCoreLikeType(entry.OwnerType) || PathContainsCoreDomainShape(entry.OwnerPath);
            string memberKind = BuildReportMemberKindLabel(entry.MemberInfo);

            if (entry.Kind == ESRuntimeWatchRegistry.RuntimeWatchEntryKind.Module)
                return "Core链路 / Module" + memberKind;

            if (coreLike)
                return "Core链路 / Domain或Core嵌套" + memberKind;

            return entry.IsNested ? "普通Mono / 嵌套" + memberKind : "普通Mono / 直接" + memberKind;
        }

        private static bool IsCoreLikeType(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (current.Name == "Core")
                    return true;
            }

            return type.GetField("ModuleTables", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null
                   || type.GetMethods(BindingFlags.Instance | BindingFlags.Public).Any(method => method.Name == "GetMoudle" && method.IsGenericMethodDefinition);
        }

        private static string BuildReportMemberKindLabel(MemberInfo memberInfo)
        {
            if (memberInfo is PropertyInfo)
                return "属性";

            if (memberInfo is MethodInfo)
                return "方法";

            return "字段";
        }

        private static bool PathContainsCoreDomainShape(FieldInfo[] path)
        {
            if (path == null)
                return false;

            for (int i = 0; i < path.Length; i++)
            {
                FieldInfo field = path[i];
                if (field == null)
                    continue;

                Type fieldType = field.FieldType;
                if (fieldType == null)
                    continue;

                if (fieldType.Name.Contains("Domain")
                    || fieldType.GetProperty("Core_Base", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null
                    || fieldType.GetProperty("ModulesIEnumable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildEntryChainLine(int index, ESRuntimeWatchRegistry.Entry entry)
        {
            var sb = new StringBuilder(512);
            sb.Append("#").Append(index + 1).Append(' ');
            sb.Append(entry.Kind).Append(" | ");
            sb.Append("Owner=").Append(ESRuntimeWatchRegistry.BuildTypeInfo(entry.OwnerType)).Append(" | ");
            sb.Append("OwnerPath=").Append(BuildFieldPathInfo(entry.OwnerPath)).Append(" | ");
            if (entry.Kind == ESRuntimeWatchRegistry.RuntimeWatchEntryKind.Module)
            {
                sb.Append("Module=").Append(ESRuntimeWatchRegistry.BuildTypeInfo(entry.ModuleType)).Append(" | ");
                sb.Append("ModulePath=").Append(BuildFieldPathInfo(entry.ModulePath)).Append(" | ");
            }

            sb.Append("Member=").Append(BuildMemberInfo(entry.MemberInfo)).Append(" | ");
            sb.Append("MemberPath=").Append(entry.MemberPath).Append(" | ");
            sb.Append("Display=").Append(entry.DisplayName).Append(" | ");
            if (entry.IsMethod)
                sb.Append("ManualInvoke=").Append(entry.RequiresManualInvoke ? "Yes" : "No").Append(" | ");
            sb.Append("Group=").Append(entry.Attribute != null ? entry.Attribute.Group : "<null>").Append(" | ");
            sb.Append("Category=").Append(entry.Attribute != null ? entry.Attribute.Category : "<null>").Append(" | ");
            sb.Append("Label=").Append(entry.Attribute != null ? entry.Attribute.Label : "<null>").Append(" | ");
            sb.Append("Tag=").Append(string.IsNullOrWhiteSpace(entry.RequiredTag) ? "<无>" : entry.RequiredTag).Append(" | ");
            sb.Append("ShowIf=").Append(string.IsNullOrWhiteSpace(entry.ShowIf) ? "<无>" : entry.ShowIf).Append(" | ");
            sb.Append("Key=").Append(entry.EntryKey);
            return sb.ToString();
        }

        private static string BuildFieldPathInfo(FieldInfo[] path)
        {
            if (path == null || path.Length == 0)
                return "<无>";

            return string.Join(" -> ", path.Select(BuildFieldInfo));
        }

        private static string BuildFieldInfo(FieldInfo field)
        {
            if (field == null)
                return "<null>";

            string declaring = field.DeclaringType != null ? field.DeclaringType.Name : "<no declaring>";
            string fieldType = field.FieldType != null ? field.FieldType.Name : "<no type>";
            return declaring + "." + field.Name + ":" + fieldType;
        }

        private static string BuildMemberInfo(MemberInfo member)
        {
            if (member == null)
                return "<null>";

            string declaring = member.DeclaringType != null ? member.DeclaringType.Name : "<no declaring>";
            if (member is FieldInfo field)
                return "Field " + declaring + "." + field.Name + ":" + (field.FieldType != null ? field.FieldType.Name : "<no type>");

            if (member is PropertyInfo property)
                return "Property " + declaring + "." + property.Name + ":" + (property.PropertyType != null ? property.PropertyType.Name : "<no type>");

            if (member is MethodInfo method)
            {
                string returnType = method.ReturnType != null ? method.ReturnType.Name : "<no return>";
                string buttonLabel = ESRuntimeWatchRegistry.TryGetButtonLabel(method);
                return "Method " + declaring + "." + method.Name + "():" + returnType
                       + (string.IsNullOrWhiteSpace(buttonLabel) ? "" : " Button=" + buttonLabel);
            }

            return member.MemberType + " " + declaring + "." + member.Name;
        }

        /// <summary>
        /// RuntimeWatch 的录制层只保存“变化事实”，而不是逐帧值快照。
        /// 它刻意不落盘、不会执行方法，并用固定容量防止编辑器会话长期占用内存。
        /// </summary>
        private sealed class RuntimeWatchEvidenceRecorder
        {
            private const int MaxTrackedSignals = 2048;
            private const int MaxRecordedEvents = 512;
            private const int MaxRecordedValueLength = 240;

            private readonly Dictionary<string, TrackedSignal> signals = new Dictionary<string, TrackedSignal>(StringComparer.Ordinal);
            private readonly HashSet<string> destroyedOwnerKeys = new HashSet<string>(StringComparer.Ordinal);
            private readonly RecordedEvent[] events = new RecordedEvent[MaxRecordedEvents];
            private int eventStart;
            private int eventCount;
            private double startedAt;
            private double stoppedAt;
            private string scopeLabel = string.Empty;
            private string stopReason = string.Empty;

            public bool IsRecording { get; private set; }
            public bool HasEvidence => IsRecording || eventCount > 0 || stoppedAt > 0d;
            public int EventCount => eventCount;
            public string StopReason => string.IsNullOrWhiteSpace(stopReason) ? "尚未冻结" : stopReason;
            public string ScopeLabel => string.IsNullOrWhiteSpace(scopeLabel) ? "当前场景" : scopeLabel;
            public double ElapsedSeconds => IsRecording
                ? Math.Max(0d, EditorApplication.timeSinceStartup - startedAt)
                : Math.Max(0d, stoppedAt - startedAt);

            public void Start(double now, string scope)
            {
                Clear();
                IsRecording = true;
                startedAt = now;
                scopeLabel = string.IsNullOrWhiteSpace(scope) ? "当前场景" : scope;
                stopReason = string.Empty;
            }

            public void Stop(double now, string reason)
            {
                if (!IsRecording)
                    return;

                IsRecording = false;
                stoppedAt = now;
                stopReason = string.IsNullOrWhiteSpace(reason) ? "已停止" : reason;
            }

            public void Clear()
            {
                signals.Clear();
                destroyedOwnerKeys.Clear();
                Array.Clear(events, 0, events.Length);
                eventStart = 0;
                eventCount = 0;
                startedAt = 0d;
                stoppedAt = 0d;
                scopeLabel = string.Empty;
                stopReason = string.Empty;
                IsRecording = false;
            }

            public void Sample(IReadOnlyList<WatchEntry> sourceEntries, double now)
            {
                if (!IsRecording || sourceEntries == null)
                    return;

                foreach (TrackedSignal signal in signals.Values)
                    signal.SeenThisSample = false;

                for (int i = 0; i < sourceEntries.Count; i++)
                {
                    WatchEntry entry = sourceEntries[i];
                    if (!CanRecord(entry))
                        continue;

                    string key = BuildSignalKey(entry);
                    if (!signals.TryGetValue(key, out TrackedSignal signal))
                    {
                        if (signals.Count >= MaxTrackedSignals)
                            continue;

                        signal = new TrackedSignal(entry);
                        signals.Add(key, signal);
                    }

                    signal.RefreshMetadata(entry);
                    signal.SeenThisSample = true;
                    // CollectEntries 已在受控刷新时采样；录制只消费同一份缓存，
                    // 不允许为了记录证据再次执行 Getter。
                    string currentValue = NormalizeValue(entry.GetCachedValue());
                    if (signal.HasValue && !string.Equals(signal.LastValue, currentValue, StringComparison.Ordinal))
                    {
                        AddEvent(new RecordedEvent(
                            now - startedAt,
                            entry.Owner,
                            entry.OwnerName,
                            entry.Group,
                            entry.Label,
                            entry.MemberPath,
                            signal.LastValue,
                            currentValue));
                    }

                    signal.LastValue = currentValue;
                    signal.HasValue = true;
                }

                // CollectEntries 不会再返回已销毁的 Mono，因此在这里保留一次明确的终止事实。
                foreach (TrackedSignal signal in signals.Values)
                {
                    if (signal.SeenThisSample || signal.Owner != null)
                        continue;

                    signal.SeenThisSample = true;
                    if (!destroyedOwnerKeys.Add(signal.OwnerKey))
                        continue;

                    AddEvent(new RecordedEvent(
                        now - startedAt,
                        null,
                        signal.OwnerName,
                        signal.Group,
                        "对象已销毁",
                        signal.MemberPath,
                        signal.HasValue ? signal.Label + " = " + signal.LastValue : "最后状态未知",
                        "<对象已销毁>"));
                }
            }

            public RecordedEvent GetNewestEvent(int newestIndex)
            {
                if (newestIndex < 0 || newestIndex >= eventCount)
                    return null;

                int index = eventStart + eventCount - 1 - newestIndex;
                if (index >= MaxRecordedEvents)
                    index -= MaxRecordedEvents;
                return events[index];
            }

            private static bool CanRecord(WatchEntry entry)
            {
                // 方法在 RuntimeWatch 中可能代表显式操作，录制过程绝不自动调用它们。
                return entry != null
                       && entry.Owner != null
                       && !entry.HasManualAction
                       && !(entry.MemberInfo is MethodInfo);
            }

            private static string BuildSignalKey(WatchEntry entry)
            {
                if (!string.IsNullOrWhiteSpace(entry.StateKey))
                    return entry.StateKey;

                return entry.OwnerKey + "|" + entry.MemberPath;
            }

            private static string NormalizeValue(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return "<空>";

                if (value.Length <= MaxRecordedValueLength)
                    return value;

                return value.Substring(0, MaxRecordedValueLength) + "…";
            }

            private void AddEvent(RecordedEvent recordedEvent)
            {
                if (recordedEvent == null)
                    return;

                int writeIndex;
                if (eventCount < MaxRecordedEvents)
                {
                    writeIndex = (eventStart + eventCount) % MaxRecordedEvents;
                    eventCount++;
                }
                else
                {
                    writeIndex = eventStart;
                    eventStart = (eventStart + 1) % MaxRecordedEvents;
                }

                events[writeIndex] = recordedEvent;
            }

            private sealed class TrackedSignal
            {
                public UnityEngine.Object Owner;
                public string OwnerKey;
                public string OwnerName;
                public string Group;
                public string Label;
                public string MemberPath;
                public string LastValue;
                public bool HasValue;
                public bool SeenThisSample;

                public TrackedSignal(WatchEntry entry)
                {
                    RefreshMetadata(entry);
                }

                public void RefreshMetadata(WatchEntry entry)
                {
                    Owner = entry.Owner;
                    OwnerKey = entry.OwnerKey ?? string.Empty;
                    OwnerName = entry.OwnerName ?? "<对象>";
                    Group = entry.Group ?? "Default";
                    Label = entry.Label ?? entry.MemberPath ?? "<成员>";
                    MemberPath = entry.MemberPath ?? string.Empty;
                }
            }

            public sealed class RecordedEvent
            {
                public readonly double ElapsedSeconds;
                public readonly UnityEngine.Object Owner;
                public readonly string OwnerName;
                public readonly string Group;
                public readonly string Label;
                public readonly string MemberPath;
                public readonly string Before;
                public readonly string After;

                public RecordedEvent(
                    double elapsedSeconds,
                    UnityEngine.Object owner,
                    string ownerName,
                    string group,
                    string label,
                    string memberPath,
                    string before,
                    string after)
                {
                    ElapsedSeconds = Math.Max(0d, elapsedSeconds);
                    Owner = owner;
                    OwnerName = ownerName ?? "<对象>";
                    Group = group ?? "Default";
                    Label = label ?? "<成员>";
                    MemberPath = memberPath ?? string.Empty;
                    Before = before ?? "<空>";
                    After = after ?? "<空>";
                }
            }
        }

        private sealed class WatchSampleState
        {
            public bool HasValue;
            public string LastValue;
            public double ChangedUntil;
            public double LastReadDurationMs;
            public bool LastReadFailed;
        }

        private class WatchEntry
        {
            public UnityEngine.Object Owner;
            public GameObject SceneObject;
            public string OwnerName;
            public string OwnerKey;
            public string OwnerTypeName;
            public string Group;
            public string Category;
            public string Label;
            public string MemberPath;
            public string GameObjectName;
            public string ScriptTypeName;
            public ESRuntimeWatchRegistry.Entry RegistryEntry;
            public MemberInfo MemberInfo => RegistryEntry.MemberInfo;
            // 元数据仅随 CollectEntries 创建条目时计算；IMGUI 重绘只读取这些缓存字段。
            public string MemberKindLabel;
            public string MemberPathPrefix;
            public string MemberCodeName;
            public string MemberTypeSuffix;
            public Color MemberKindColor;
            public bool HasManualAction;
            public bool IsActionable;
            public string ActionButtonLabel;
            public string StateKey;
            public WatchSampleState SampleState;
            private Func<string> readValue;
            private Func<string> manualAction;

            public bool IsRecentlyChanged => SampleState != null
                && SampleState.ChangedUntil > EditorApplication.timeSinceStartup;
            public bool LastReadFailed => SampleState != null && SampleState.LastReadFailed;
            public double LastReadDurationMs => SampleState != null ? SampleState.LastReadDurationMs : 0d;

            public static WatchEntry FromRegistryEntry(MonoBehaviour owner, ESRuntimeWatchRegistry.Entry entry, bool allowGetMoudleFallback)
            {
                string displayLabel = entry.DisplayName;
                if (string.IsNullOrWhiteSpace(displayLabel))
                    displayLabel = entry.MemberPath;

                bool hasManualAction = entry.RequiresManualInvoke;
                string actionLabel = entry.ActionLabel;
                if (string.IsNullOrWhiteSpace(actionLabel))
                    actionLabel = displayLabel;
                if (string.IsNullOrWhiteSpace(actionLabel))
                    actionLabel = "执行";

                return Create(
                    owner,
                    entry.MemberPath,
                    displayLabel,
                    ESRuntimeWatchRegistry.BuildTypeInfo(entry.OwnerType),
                    entry.Attribute,
                    entry,
                    () => ReadEntryValue(owner, entry, allowGetMoudleFallback),
                    hasManualAction,
                    actionLabel,
                    () => InvokeEntryMethod(owner, entry, allowGetMoudleFallback));
            }

            private static WatchEntry Create(MonoBehaviour owner, string memberPath, string displayLabel, string ownerTypeName, ESRuntimeWatchAttribute attribute, ESRuntimeWatchRegistry.Entry registryEntry, Func<object> getter, bool hasManualAction, string actionLabel, Func<object> manualInvoker)
            {
                var watchEntry = new WatchEntry
                {
                    Owner = owner,
                    SceneObject = owner != null ? owner.gameObject : null,
                    OwnerName = BuildOwnerPath(owner),
                    OwnerKey = owner.GetInstanceID() + "|" + ownerTypeName,
                    OwnerTypeName = ownerTypeName,
                    Group = string.IsNullOrEmpty(attribute.Group) ? "Default" : attribute.Group,
                    Category = attribute == null || string.IsNullOrWhiteSpace(attribute.Category) ? "无分类" : attribute.Category,
                    Label = string.IsNullOrEmpty(displayLabel) ? memberPath : displayLabel,
                    MemberPath = memberPath,
                    GameObjectName = BuildGameObjectPath(owner),
                    ScriptTypeName = owner != null ? owner.GetType().Name : "<空脚本>",
                    RegistryEntry = registryEntry,
                    HasManualAction = hasManualAction,
                    ActionButtonLabel = string.IsNullOrEmpty(actionLabel) ? "执行" : actionLabel,
                    readValue = () =>
                    {
                        try
                        {
                            object value = getter();
                            return value == null ? "null" : value.ToString();
                        }
                        catch (Exception e)
                        {
                            return "<读取失败: " + e.Message + ">";
                        }
                    },
                    manualAction = () =>
                    {
                        try
                        {
                            object value = manualInvoker();
                            return value == null ? "执行完成: null" : "执行完成: " + value;
                        }
                        catch (Exception e)
                        {
                            return "执行失败: " + e.Message;
                        }
                    }
                };

                watchEntry.MemberKindLabel = BuildMemberKindLabel(watchEntry.MemberInfo);
                watchEntry.MemberPathPrefix = BuildMemberPathPrefix(watchEntry);
                watchEntry.MemberCodeName = watchEntry.MemberInfo != null ? watchEntry.MemberInfo.Name : "<成员丢失>";
                watchEntry.MemberTypeSuffix = BuildMemberTypeSuffix(watchEntry.MemberInfo);
                watchEntry.MemberKindColor = BuildMemberKindColor(watchEntry.MemberInfo);
                watchEntry.IsActionable = watchEntry.HasManualAction
                                          || TryGetEditableValueType(watchEntry.MemberInfo, out _);
                return watchEntry;
            }

            private static string BuildMemberKindLabel(MemberInfo memberInfo)
            {
                if (memberInfo is FieldInfo)
                    return "字段";

                if (memberInfo is PropertyInfo)
                    return "属性";

                if (memberInfo is MethodInfo)
                    return "方法";

                return "成员";
            }

            private static Color BuildMemberKindColor(MemberInfo memberInfo)
            {
                if (memberInfo is FieldInfo)
                    return new Color(0.25f, 0.72f, 1f);

                if (memberInfo is PropertyInfo)
                    return new Color(0.30f, 0.88f, 0.52f);

                if (memberInfo is MethodInfo)
                    return new Color(1f, 0.64f, 0.22f);

                return new Color(0.55f, 0.55f, 0.55f);
            }

            private static string BuildMemberPathPrefix(WatchEntry entry)
            {
                if (entry == null || entry.MemberInfo == null)
                    return string.Empty;

                string memberPath = entry.MemberPath ?? string.Empty;
                string memberName = entry.MemberInfo.Name;
                return memberPath.EndsWith(memberName, StringComparison.Ordinal)
                    ? memberPath.Substring(0, memberPath.Length - memberName.Length)
                    : string.Empty;
            }

            private static string BuildMemberTypeSuffix(MemberInfo memberInfo)
            {
                if (memberInfo is MethodInfo method)
                {
                    string parameters = string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name + " " + parameter.Name));
                    string returnType = method.ReturnType != null ? method.ReturnType.Name : "void";
                    return $"({parameters}) : {returnType}";
                }

                if (memberInfo is FieldInfo field)
                    return " : " + field.FieldType.Name;

                if (memberInfo is PropertyInfo property)
                    return " : " + property.PropertyType.Name;

                return string.Empty;
            }

            private static object ReadEntryValue(MonoBehaviour owner, ESRuntimeWatchRegistry.Entry entry, bool allowGetMoudleFallback)
            {
                object current = ResolveEntryContext(owner, entry, allowGetMoudleFallback);

                if (current == null)
                    return "<空引用: " + entry.MemberPath + ">";

                if (entry.MemberInfo == null || entry.MemberInfo.DeclaringType == null || !entry.MemberInfo.DeclaringType.IsInstanceOfType(current))
                    return "<空引用: " + entry.MemberPath + ">";

                if (entry.RequiresManualInvoke)
                    return "<点击按钮执行>";

                return ReadMemberValue(current, entry.MemberInfo);
            }

            private static object ReadMemberValue(object owner, MemberInfo memberInfo)
            {
                try
                {
                    if (memberInfo is FieldInfo field)
                        return field.GetValue(owner);

                    if (memberInfo is PropertyInfo property)
                    {
                        if (!property.CanRead || property.GetIndexParameters().Length > 0)
                            return "<属性不可读>";

                        return property.GetValue(owner);
                    }

                    if (memberInfo is MethodInfo method)
                    {
                        if (method.GetParameters().Length > 0)
                            return "<方法需要参数>";

                        object result = method.Invoke(owner, null);
                        return method.ReturnType == typeof(void) ? "<void>" : result;
                    }
                }
                catch (TargetInvocationException ex)
                {
                    Exception inner = ex.InnerException ?? ex;
                    return "<读取失败: " + inner.Message + ">";
                }
                catch (Exception ex)
                {
                    return "<读取失败: " + ex.Message + ">";
                }

                return "<不支持的成员>";
            }

            private static object InvokeEntryMethod(MonoBehaviour owner, ESRuntimeWatchRegistry.Entry entry, bool allowGetMoudleFallback)
            {
                object current = ResolveEntryContext(owner, entry, allowGetMoudleFallback);
                if (current == null)
                    return "<空引用: " + entry.MemberPath + ">";

                if (!(entry.MemberInfo is MethodInfo method))
                    return "<空引用: " + entry.MemberPath + ">";

                if (method.GetParameters().Length > 0)
                    return "<不是方法: " + entry.MemberPath + ">";

                if (method.DeclaringType == null || !method.DeclaringType.IsInstanceOfType(current))
                    return "<方法需要参数: " + entry.MemberPath + ">";

                try
                {
                    object result = method.Invoke(current, null);
                    return method.ReturnType == typeof(void) ? "<void>" : result;
                }
                catch (TargetInvocationException ex)
                {
                    Exception inner = ex.InnerException ?? ex;
                    return "<执行失败: " + inner.Message + ">";
                }
            }

            public static object ResolveEntryContext(MonoBehaviour owner, ESRuntimeWatchRegistry.Entry entry, bool allowGetMoudleFallback)
            {
                object current = owner;
                if (entry.OwnerPath != null)
                {
                    for (int i = 0; i < entry.OwnerPath.Length; i++)
                    {
                        if (current == null)
                            return null;

                        var pathField = entry.OwnerPath[i];
                        if (!pathField.DeclaringType.IsInstanceOfType(current))
                            return null;

                        current = pathField.GetValue(current);
                    }
                }

                if (entry.Kind == ESRuntimeWatchRegistry.RuntimeWatchEntryKind.Module)
                {
                    Type moduleType = entry.ModuleType ?? (entry.MemberInfo != null ? entry.MemberInfo.DeclaringType : null);
                    current = ResolveModuleInstance(current, moduleType, allowGetMoudleFallback);
                    if (entry.ModulePath != null)
                    {
                        for (int i = 0; i < entry.ModulePath.Length; i++)
                        {
                            if (current == null)
                                return null;

                            FieldInfo modulePathField = entry.ModulePath[i];
                            if (!modulePathField.DeclaringType.IsInstanceOfType(current))
                                return null;

                            current = modulePathField.GetValue(current);
                        }
                    }
                }

                return current;
            }

            private static object ResolveModuleInstance(object hostOrModule, Type moduleType, bool allowGetMoudleFallback)
            {
                if (hostOrModule == null || moduleType == null)
                    return null;

                if (moduleType.IsInstanceOfType(hostOrModule))
                    return hostOrModule;

                object core = ResolveCoreObject(hostOrModule);
                object moduleFromCore = ResolveModuleFromCoreTable(core, moduleType);
                if (moduleFromCore != null)
                    return moduleFromCore;

                object moduleFromEnumeration = ResolveModuleFromEnumerable(hostOrModule, moduleType);
                if (moduleFromEnumeration != null)
                    return moduleFromEnumeration;

                if (allowGetMoudleFallback)
                {
                    object moduleFromGetter = ResolveModuleByGetMoudle(core, moduleType);
                    if (moduleFromGetter != null)
                        return moduleFromGetter;
                }

                return null;
            }

            private static object ResolveModuleFromEnumerable(object hostOrModule, Type moduleType)
            {
                if (!(hostOrModule is IESHosting))
                    return null;

                PropertyInfo modulesProperty = GetModulesEnumerableProperty(hostOrModule.GetType());
                if (modulesProperty == null)
                    return null;

                IEnumerable modules = modulesProperty.GetValue(hostOrModule) as IEnumerable;
                if (modules == null)
                    return null;

                foreach (object module in modules)
                {
                    if (module != null && moduleType.IsInstanceOfType(module))
                        return module;
                }

                return null;
            }

            private static object ResolveModuleByGetMoudle(object core, Type moduleType)
            {
                if (core == null || moduleType == null)
                    return null;

                MethodInfo genericGetter = core.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method =>
                        method.Name == "GetMoudle"
                        && method.IsGenericMethodDefinition
                        && method.GetGenericArguments().Length == 1
                        && method.GetParameters().Length == 0);

                if (genericGetter == null)
                    return null;

                try
                {
                    return genericGetter.MakeGenericMethod(moduleType).Invoke(core, null);
                }
                catch (TargetInvocationException ex)
                {
                    Exception inner = ex.InnerException ?? ex;
                    LogRuntimeWatchWarningOnce("GetMoudle|" + core.GetType().FullName + "|" + moduleType.FullName,
                        $"[RuntimeWatch] GetMoudle<{moduleType.Name}> fallback failed: {inner.GetType().Name}: {inner.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    LogRuntimeWatchWarningOnce("GetMoudle|" + core.GetType().FullName + "|" + moduleType.FullName,
                        $"[RuntimeWatch] GetMoudle<{moduleType.Name}> fallback failed: {ex.Message}");
                    return null;
                }
            }

            private static object ResolveCoreObject(object hostOrModule)
            {
                if (hostOrModule == null)
                    return null;

                if (hostOrModule is MonoBehaviour)
                    return hostOrModule;

                PropertyInfo coreBaseProperty = hostOrModule.GetType().GetProperty("Core_Base", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object coreBase = coreBaseProperty != null && coreBaseProperty.GetIndexParameters().Length == 0
                    ? coreBaseProperty.GetValue(hostOrModule)
                    : null;
                if (coreBase is MonoBehaviour)
                    return coreBase;

                PropertyInfo coreObjectProperty = hostOrModule.GetType().GetProperty("Core_Object", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object coreObject = coreObjectProperty != null && coreObjectProperty.GetIndexParameters().Length == 0
                    ? coreObjectProperty.GetValue(hostOrModule)
                    : null;
                if (coreObject is MonoBehaviour)
                    return coreObject;

                return null;
            }

            private static object ResolveModuleFromCoreTable(object core, Type moduleType)
            {
                if (core == null || moduleType == null)
                    return null;

                FieldInfo moduleTablesField = core.GetType().GetField("ModuleTables", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object tableObject = moduleTablesField != null ? moduleTablesField.GetValue(core) : null;
                if (!(tableObject is IDictionary moduleTable))
                    return null;

                if (moduleTable.Contains(moduleType))
                {
                    object exact = moduleTable[moduleType];
                    if (exact != null)
                        return exact;
                }

                foreach (DictionaryEntry pair in moduleTable)
                {
                    Type keyType = pair.Key as Type;
                    object module = pair.Value;
                    if (module == null)
                        continue;

                    if ((keyType != null && moduleType.IsAssignableFrom(keyType)) || moduleType.IsInstanceOfType(module))
                        return module;
                }

                return null;
            }

            private static PropertyInfo GetModulesEnumerableProperty(Type hostType)
            {
                if (hostType == null)
                    return null;

                if (ModulesEnumerablePropertyCache.TryGetValue(hostType, out var cached))
                    return cached;

                PropertyInfo property = hostType.GetProperty("ModulesIEnumable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                ModulesEnumerablePropertyCache[hostType] = property;
                return property;
            }

            private static string BuildPathPrefix(ESRuntimeWatchRegistry.Entry entry, int length)
            {
                if (entry.MemberInfo == null)
                    return "<null>";

                if (entry.OwnerPath == null || entry.OwnerPath.Length == 0)
                    return entry.MemberInfo.Name;

                length = Mathf.Clamp(length, 0, entry.OwnerPath.Length);
                if (length == 0)
                    return entry.OwnerPath[0].Name;

                return string.Join(".", entry.OwnerPath.Take(length).Select(field => field.Name));
            }

            public string GetCachedValue()
            {
                if (SampleState == null || !SampleState.HasValue)
                    return "<等待刷新>";

                return SampleState.LastValue ?? string.Empty;
            }

            public string RefreshSample()
            {
                var stopwatch = Stopwatch.StartNew();
                string value = readValue == null ? "" : readValue();
                stopwatch.Stop();

                if (SampleState != null)
                {
                    SampleState.LastReadDurationMs = stopwatch.Elapsed.TotalMilliseconds;
                    SampleState.LastReadFailed = !string.IsNullOrEmpty(value)
                        && value.StartsWith("<读取失败", StringComparison.Ordinal);
                    if (SampleState.HasValue && !string.Equals(SampleState.LastValue, value, StringComparison.Ordinal))
                        SampleState.ChangedUntil = EditorApplication.timeSinceStartup + ChangeHighlightSeconds;
                    SampleState.LastValue = value;
                    SampleState.HasValue = true;
                }

                return value;
            }

            public string InvokeManualAction()
            {
                return manualAction == null ? "没有可执行动作。" : manualAction();
            }

            private static string BuildOwnerPath(MonoBehaviour owner)
            {
                if (owner == null)
                    return "<空对象>";

                string path = BuildGameObjectPath(owner);
                return path + " (" + owner.GetType().Name + ")";
            }

            private static string BuildGameObjectPath(MonoBehaviour owner)
            {
                if (owner == null || owner.transform == null)
                    return "<空对象>";

                Transform current = owner.transform;
                string path = current.name;
                while (current.parent != null)
                {
                    current = current.parent;
                    path = current.name + "/" + path;
                }

                return path;
            }
        }
    }
}
