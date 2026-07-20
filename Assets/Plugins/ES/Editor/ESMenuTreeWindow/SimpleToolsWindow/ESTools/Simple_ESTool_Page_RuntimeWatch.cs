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

namespace ES
{
    [Serializable]
    public class Page_RuntimeWatch : ESWindowPageBase
    {
        private readonly Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> ownerFoldouts = new Dictionary<string, bool>();
        private readonly List<WatchEntry> entries = new List<WatchEntry>();
        private static readonly Dictionary<Type, PropertyInfo> ModulesEnumerablePropertyCache = new Dictionary<Type, PropertyInfo>();
        private static readonly Dictionary<string, Delegate> OdinBoolExpressionCache = new Dictionary<string, Delegate>();
        private static readonly HashSet<string> FailedOdinBoolExpressions = new HashSet<string>();
        private readonly Dictionary<string, string> inlineInputDrafts = new Dictionary<string, string>();
        private bool defaultFoldoutExpanded = true;

        [Title("运行时观察", "收集场景中带 ESRuntimeWatch 标记的字段、属性和方法", bold: true, titleAlignment: TitleAlignments.Centered)]
        [InfoBox("给 MonoBehaviour、Module 或其嵌套数据添加 [ESRuntimeWatch(\"分组\", \"显示名\")]。面板只在编辑器读取当前场景实例，支持分类、搜索、Odin ShowIf、属性 getter、方法调用和常规值设定；点击按钮会直接生效。")]
        [HorizontalGroup("Toolbar", Width = 0.42f)]
        [ShowInInspector, LabelText("搜索")]
        private string searchText = "";

        [HorizontalGroup("SearchFilters", Width = 135)]
        [ShowInInspector, LabelText("分类"), ValueDropdown(nameof(GetRuntimeWatchCategoryOptions))]
        private string selectedCategoryFilter = "全部";

        [HorizontalGroup("SearchFilters", Width = 165)]
        [ShowInInspector, LabelText("游戏对象"), ValueDropdown(nameof(GetRuntimeWatchObjectOptions))]
        private string selectedObjectFilter = "全部";

        [HorizontalGroup("SearchFilters", Width = 145)]
        [ShowInInspector, LabelText("脚本"), ValueDropdown(nameof(GetRuntimeWatchScriptOptions))]
        private string selectedScriptFilter = "全部";

        [HorizontalGroup("Toolbar", Width = 135)]
        [ShowInInspector, LabelText("自动刷新")]
        private bool autoRefresh = true;

        [HorizontalGroup("Toolbar", Width = 160)]
        [ShowInInspector, LabelText("刷新间隔")]
        [MinValue(0.1f)]
        private float refreshInterval = 0.25f;

        [HorizontalGroup("Toolbar", Width = 145)]
        [ShowInInspector, LabelText("编辑器扫描")]
        private bool refreshInEditMode = false;

        [HorizontalGroup("Scope", Width = 165)]
        [ShowInInspector, LabelText("只看选中对象")]
        private bool onlySelectedGameObject = false;

        [HorizontalGroup("Scope", Width = 165)]
        [ShowInInspector, LabelText("包含子对象")]
        private bool includeSelectedChildren = true;

        [HorizontalGroup("Filters", Width = 160)]
        [ShowInInspector, LabelText("启用Tag过滤")]
        private bool enableTagFilter = true;

        [HorizontalGroup("Filters", Width = 165)]
        [ShowInInspector, LabelText("启用ShowIf")]
        private bool enableShowIfFilter = true;

        [HorizontalGroup("Filters", Width = 190)]
        [ShowInInspector, LabelText("允许GetMoudle"), Tooltip("允许 RuntimeWatch 通过 Core.GetMoudle<T>() 补取模块。此开关可能创建缺失模块，默认关闭。")]
        private bool allowGetMoudleFallback = false;

        [HorizontalGroup("Filters", Width = 135)]
        [ShowInInspector, LabelText("紧凑模式")]
        private bool compactView = false;

        private double nextRefreshTime;
        private Vector2 scroll;
        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private string chainReport = "";
        private int lastScannedOwnerTypeCount;
        private int lastFoundOwnerCount;
        private int lastCandidateEntryCount;
        private int lastNoFilterCandidateCount;
        private int lastTagFilteredCount;
        private int lastShowIfFilteredCount;
        private int lastContextMissingCount;
        private int lastDuplicateSkippedCount;

        [HorizontalGroup("Toolbar", Width = 110)]
        [Button("立即刷新", ButtonHeight = 22), GUIColor(0.35f, 0.65f, 1f)]
        public void RefreshNow()
        {
            CollectEntries(recordResult: true);
        }

        [HorizontalGroup("DebugButtons", Width = 140)]
        [Button("生成链路报告", ButtonHeight = 22), GUIColor(0.75f, 0.85f, 1f)]
        public void BuildChainReport()
        {
            chainReport = BuildRegistryChainReport();
        }

        [HorizontalGroup("DebugButtons", Width = 140)]
        [Button("复制链路报告", ButtonHeight = 22)]
        public void CopyChainReport()
        {
            if (string.IsNullOrEmpty(chainReport))
                chainReport = BuildRegistryChainReport();

            EditorGUIUtility.systemCopyBuffer = chainReport;
            lastResultSummary = "已复制 RuntimeWatch 链路报告";
            lastResultDetail = "报告已写入系统剪贴板。";
        }

        [HorizontalGroup("DebugButtons", Width = 120)]
        [Button("清空报告", ButtonHeight = 22)]
        public void ClearChainReport()
        {
            chainReport = "";
        }

        [HorizontalGroup("DebugButtons", Width = 110)]
        [Button("全部展开", ButtonHeight = 22), GUIColor(0.35f, 0.65f, 0.45f)]
        public void ExpandAllFoldouts()
        {
            defaultFoldoutExpanded = true;
            groupFoldouts.Clear();
            ownerFoldouts.Clear();
            lastResultSummary = "已展开所有 RuntimeWatch 分组";
            lastResultDetail = "当前已清空折叠缓存，新条目将默认展开。";
        }

        [HorizontalGroup("DebugButtons", Width = 110)]
        [Button("全部收起", ButtonHeight = 22), GUIColor(0.62f, 0.48f, 0.24f)]
        public void CollapseAllFoldouts()
        {
            defaultFoldoutExpanded = false;
            groupFoldouts.Clear();
            ownerFoldouts.Clear();
            lastResultSummary = "已收起所有 RuntimeWatch 分组";
            lastResultDetail = "当前已清空折叠缓存，新条目将默认收起。";
        }

        [HorizontalGroup("DebugButtons", Width = 120)]
        [Button("重置折叠", ButtonHeight = 22)]
        public void ResetFoldouts()
        {
            defaultFoldoutExpanded = true;
            groupFoldouts.Clear();
            ownerFoldouts.Clear();
        }

        [FoldoutGroup("链路诊断", Expanded = false)]
        [ShowInInspector, ReadOnly, HideLabel, MultiLineProperty(28)]
        private string ChainReportView => string.IsNullOrEmpty(chainReport) ? "点击“生成链路报告”查看底层完整链路。" : chainReport;

        [OnInspectorGUI]
        private void DrawRuntimeWatch()
        {
            bool shouldAutoRefresh = autoRefresh && (EditorApplication.isPlaying || refreshInEditMode);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play Mode 后显示运行时观察数据。", MessageType.Info);
                if (GUILayout.Button("编辑器下扫描一次"))
                    CollectEntries(recordResult: true);
            }

            if (shouldAutoRefresh && EditorApplication.timeSinceStartup >= nextRefreshTime)
            {
                CollectEntries(recordResult: false);
                nextRefreshTime = EditorApplication.timeSinceStartup + refreshInterval;
            }

            SimpleToolsPanelUtility.DrawSummary(
                $"观察项: {entries.Count}",
                $"注册成员: {ESRuntimeWatchRegistry.RegisteredMemberCount} / 字段 {ESRuntimeWatchRegistry.RegisteredFieldCount} / 属性 {ESRuntimeWatchRegistry.RegisteredPropertyCount} / 方法 {ESRuntimeWatchRegistry.RegisteredMethodCount}",
                $"展开链路: {ESRuntimeWatchRegistry.Entries.Count}",
                $"Owner类型: {ESRuntimeWatchRegistry.OwnerTypes.Count}",
                $"场景Owner: {lastFoundOwnerCount}",
                $"候选项: {lastCandidateEntryCount}",
                $"无条件候选: {lastNoFilterCandidateCount}",
                $"Tag过滤: {lastTagFilteredCount}",
                $"ShowIf过滤: {lastShowIfFilteredCount}",
                $"上下文缺失: {lastContextMissingCount}",
                $"范围: {BuildScopeLabel()}",
                $"过滤器: Tag {(enableTagFilter ? "开" : "关")} / ShowIf {(enableShowIfFilter ? "开" : "关")}",
                $"GetMoudle: {(allowGetMoudleFallback ? "开" : "关")}",
                $"路径图: {(ESRuntimeWatchRegistry.IsFieldGraphBuilt ? ESRuntimeWatchRegistry.FieldGraphTargetTypeCount + "类/" + ESRuntimeWatchRegistry.FieldGraphEdgeCount + "边" : "未构建")}",
                $"路径截断: {ESRuntimeWatchRegistry.SchemeLimitHitCount}",
                $"拒绝Owner: {ESRuntimeWatchRegistry.RejectedNonMonoOwnerCount}",
                $"拒绝链路: {ESRuntimeWatchRegistry.RejectedInvalidPathCount}",
                $"自动刷新: {(shouldAutoRefresh ? "开" : "关")}");

            if (!compactView)
                SimpleToolsPanelUtility.DrawResultSummary("最近运行时观察刷新", lastResultSummary, lastResultDetail);

            if (entries.Count == 0)
            {
                string emptyMessage = EditorApplication.isPlaying
                    ? "当前没有找到可显示的观察项。请确认场景对象存在、字段/属性/方法已添加 ESRuntimeWatch 标记，并且搜索条件没有过滤掉结果。"
                    : "编辑器下只显示注册信息。进入 Play Mode 后会读取场景实例；也可以点“编辑器下扫描一次”检查当前打开场景。";
                EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(defaultFoldoutExpanded ? "默认展开" : "默认收起", EditorStyles.miniButtonLeft, GUILayout.Width(80)))
                    defaultFoldoutExpanded = !defaultFoldoutExpanded;
                if (GUILayout.Button("清理折叠缓存", EditorStyles.miniButtonRight, GUILayout.Width(110)))
                {
                    groupFoldouts.Clear();
                    ownerFoldouts.Clear();
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawEntries();
            EditorGUILayout.EndScrollView();

            if (shouldAutoRefresh && SimpleToolsWindow.UsingWindow != null)
                SimpleToolsWindow.UsingWindow.Repaint();
        }

        private void DrawEntries()
        {
            string activeGroup = null;
            string activeOwnerKey = null;
            bool groupVisible = false;
            bool ownerVisible = false;

            foreach (WatchEntry entry in entries)
            {
                if (!MatchesSearch(entry))
                    continue;

                if (activeGroup != entry.Group)
                {
                    activeGroup = entry.Group;
                    if (!groupFoldouts.ContainsKey(activeGroup))
                        groupFoldouts[activeGroup] = defaultFoldoutExpanded;

                    EditorGUILayout.Space(4);
                    groupFoldouts[activeGroup] = EditorGUILayout.Foldout(groupFoldouts[activeGroup], activeGroup, true);
                    groupVisible = groupFoldouts[activeGroup];
                    activeOwnerKey = null;
                }

                if (!groupVisible)
                    continue;

                string ownerFoldoutKey = entry.Group + "|" + entry.OwnerKey;
                if (activeOwnerKey != ownerFoldoutKey)
                {
                    activeOwnerKey = ownerFoldoutKey;
                    if (!ownerFoldouts.ContainsKey(activeOwnerKey))
                        ownerFoldouts[activeOwnerKey] = defaultFoldoutExpanded;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(14);
                        ownerFoldouts[activeOwnerKey] = EditorGUILayout.Foldout(ownerFoldouts[activeOwnerKey], entry.OwnerName, true);
                        if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(48)))
                        {
                            UnityEngine.Object target = entry.SceneObject != null ? entry.SceneObject : entry.Owner;
                            Selection.activeObject = target;
                            EditorGUIUtility.PingObject(target);
                            lastResultSummary = $"已定位观察对象: {entry.OwnerName}";
                            lastResultDetail = $"分组: {entry.Group}\n字段: {entry.Label}\n路径: {entry.MemberPath}\n类型: {entry.OwnerTypeName}";
                        }
                        if (GUILayout.Button("复制", EditorStyles.miniButton, GUILayout.Width(48)))
                        {
                            EditorGUIUtility.systemCopyBuffer = $"{entry.OwnerName}\n{entry.MemberPath}\n{entry.ReadValue()}";
                            lastResultSummary = $"已复制观察项: {entry.OwnerName}";
                            lastResultDetail = $"{entry.Group}\n{entry.Label}\n{entry.MemberPath}";
                        }
                    }

                    ownerVisible = ownerFoldouts[activeOwnerKey];
                }

                if (!ownerVisible)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(28);
                    DrawMemberKindBadge(entry);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(entry.Label, EditorStyles.label, GUILayout.MinWidth(120));
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.SelectableLabel(BuildCompactValue(entry.ReadValue()), GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(compactView ? 100 : 150));
                        }

                        GUILayout.Label(entry.MemberSummary, RuntimeWatchPathStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    }
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(compactView ? 180 : 240)))
                    {
                        bool inlineHandled = DrawInlineControl(entry);
                        if (!inlineHandled && entry.HasManualAction)
                            DrawRuntimeWatchActionButton(entry, compactView ? 96 : 128);
                    }
                }
            }
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

        private static string BuildCompactValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            const int maxLength = 40;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 1) + "…";
        }

        private static GUIStyle runtimeWatchPathStyle;
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

        private void DrawRuntimeWatchActionButton(WatchEntry entry, int width)
        {
            if (!GUILayout.Button(entry.ActionButtonLabel, EditorStyles.miniButton, GUILayout.Width(width)))
                return;

            string invokeResult = entry.InvokeManualAction();
            RequestDeferredRefresh();
            lastResultSummary = $"已执行: {entry.Label}";
            lastResultDetail = invokeResult;
        }

        private void CollectEntries(bool recordResult)
        {
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

                        entries.Add(WatchEntry.FromRegistryEntry(behaviour, registeredEntry, allowGetMoudleFallback));
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
                        if (!TryEvaluateSimpleBoolExpression(context, expression, out _))
                        UnityEngine.Debug.LogWarning($"[RuntimeWatch] Odin ShowIf 表达式解析失败，尝试简单布尔兜底: {expression}\nContext: {context.GetType().Name}\n{error}");
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
                        UnityEngine.Debug.LogWarning($"[RuntimeWatch] Odin ShowIf 表达式执行失败: {expression}\nContext: {context.GetType().Name}\n{ex.Message}");
                        return false;
                    }
                }
            }

            if (TryEvaluateSimpleBoolExpression(context, expression, out bool simpleResult))
            {
                if (!simpleResult)
                    failReason = "SimpleFallbackFalse";
                return simpleResult;
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

        private static bool TryEvaluateSimpleBoolExpression(object context, string expression, out bool result)
        {
            result = false;
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

            object value = ReadSimpleMemberPath(context, path);
            return TryConvertToBool(value, invert, out result);
        }

        private static object ReadSimpleMemberPath(object context, string memberPath)
        {
            object current = context;
            string[] members = memberPath.Split('.');
            for (int i = 0; i < members.Length; i++)
            {
                if (current == null)
                    return null;

                string member = members[i];
                if (string.IsNullOrWhiteSpace(member))
                    return null;

                Type currentType = current.GetType();
                FieldInfo field = currentType.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    current = field.GetValue(current);
                    continue;
                }

                PropertyInfo property = currentType.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    current = property.GetValue(current);
                    continue;
                }

                return null;
            }

            return current;
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

        private IEnumerable<string> GetRuntimeWatchCategoryOptions()
        {
            string[] common =
            {
                "全部",
                "无分类",
                "临时",
                "调试",
                "战斗",
                "AI",
                "角色",
                "场景",
                "资源",
                "性能",
                "网络",
                "存档",
                "输入",
                "UI"
            };

            return common
                .Concat(entries.Select(entry => string.IsNullOrWhiteSpace(entry.Category) ? "无分类" : entry.Category))
                .Distinct()
                .OrderBy(value => value == "全部" ? "" : value);
        }

        private IEnumerable<string> GetRuntimeWatchObjectOptions()
        {
            return new[] { "全部" }
                .Concat(entries.Select(entry => entry.GameObjectName).Where(value => !string.IsNullOrWhiteSpace(value)))
                .Distinct()
                .OrderBy(value => value == "全部" ? "" : value);
        }

        private IEnumerable<string> GetRuntimeWatchScriptOptions()
        {
            return new[] { "全部" }
                .Concat(entries.Select(entry => entry.ScriptTypeName).Where(value => !string.IsNullOrWhiteSpace(value)))
                .Distinct()
                .OrderBy(value => value == "全部" ? "" : value);
        }

        private static bool ContainsIgnoreCase(string value, string search)
        {
            return !string.IsNullOrEmpty(value)
                   && !string.IsNullOrEmpty(search)
                   && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool DrawInlineControl(WatchEntry entry)
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
                    if (GUILayout.Button(entry.ActionButtonLabel, EditorStyles.miniButton, GUILayout.Width(compactView ? 96 : 128)))
                    {
                        if (TryConvertTextToType(draft, parameters[0].ParameterType, out object parsedValue, out string parseError))
                        {
                            string invokeResult = InvokeMethodWithArguments(entry, new object[] { parsedValue });
                                RequestDeferredRefresh();
                                lastResultSummary = $"已调用: {entry.Label}";
                                lastResultDetail = invokeResult;
                        }
                        else
                        {
                            lastResultSummary = $"方法参数解析失败: {entry.Label}";
                            lastResultDetail = parseError;
                        }
                    }

                    return true;
                }
            }

            if (TryGetEditableValueType(memberInfo, out Type editableType))
            {
                string key = BuildInlineDraftKey(entry, "field");
                string seed = GetCurrentValueDraft(entry, editableType);
                string draft = GetOrCreateDraft(key, seed);
                DrawEditableValueField(editableType, ref draft);
                inlineInputDrafts[key] = draft;
                if (GUILayout.Button("设值", EditorStyles.miniButton, GUILayout.Width(compactView ? 58 : 64)))
                {
                    if (TryConvertTextToType(draft, editableType, out object parsedValue, out string parseError))
                    {
                        string setResult = TrySetEntryValue(entry, parsedValue, out string writeMessage) ? writeMessage : BuildWriteFallback(writeMessage);
                            RequestDeferredRefresh();
                            lastResultSummary = $"已设值: {entry.Label}";
                            lastResultDetail = setResult;
                    }
                    else
                    {
                        lastResultSummary = $"字段值解析失败: {entry.Label}";
                        lastResultDetail = parseError;
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
                CollectEntries(false);
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

        private static string GetCurrentValueDraft(WatchEntry entry, Type valueType)
        {
            try
            {
                object value = entry != null ? entry.ReadRawValue() : null;
                if (value == null)
                    return string.Empty;

                if (valueType != null && valueType.IsEnum)
                    return value.ToString();

                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string BuildWriteFallback(string message)
        {
            return string.IsNullOrWhiteSpace(message) ? "写回失败" : message;
        }

        private static bool TrySetEntryValue(WatchEntry entry, object value, out string message)
        {
            message = null;
            if (entry == null || entry.MemberInfo == null)
            {
                message = "条目无效";
                return false;
            }

            object context = WatchEntry.ResolveEntryContext(entry.Owner as MonoBehaviour, entry.RegistryEntry, true);
            if (context == null)
            {
                message = "找不到写回上下文";
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

        private static string InvokeMethodWithArguments(WatchEntry entry, object[] args)
        {
            if (entry == null || !(entry.MemberInfo is MethodInfo methodInfo))
                return "方法无效";

            object context = WatchEntry.ResolveEntryContext(entry.Owner as MonoBehaviour, entry.RegistryEntry, true);
            if (context == null)
                return "找不到执行上下文";

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
                    value = Enum.Parse(targetType, text, true);
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

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

        private string BuildRegistryChainReport()
        {
            IReadOnlyList<ESRuntimeWatchRegistry.Entry> registryEntries = ESRuntimeWatchRegistry.Entries;
            var groups = new Dictionary<string, List<string>>
            {
                { "Core链路 / Module字段", new List<string>() },
                { "Core链路 / Module方法", new List<string>() },
                { "Core链路 / Domain或Core嵌套字段", new List<string>() },
                { "Core链路 / Domain或Core嵌套方法", new List<string>() },
                { "普通Mono / 直接字段", new List<string>() },
                { "普通Mono / 直接方法", new List<string>() },
                { "普通Mono / 嵌套字段", new List<string>() },
                { "普通Mono / 嵌套方法", new List<string>() },
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
            public string MemberKindLabel => BuildMemberKindLabel(MemberInfo);
            public string MemberSummary => BuildMemberSummary(this);
            public Color MemberKindColor => BuildMemberKindColor(MemberInfo);
            public bool HasManualAction;
            public string ActionButtonLabel;
            private Func<string> readValue;
            private Func<string> manualAction;

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
                return new WatchEntry
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
                    return new Color(0.32f, 0.58f, 0.88f);

                if (memberInfo is PropertyInfo)
                    return new Color(0.30f, 0.68f, 0.48f);

                if (memberInfo is MethodInfo)
                    return new Color(0.82f, 0.55f, 0.24f);

                return new Color(0.55f, 0.55f, 0.55f);
            }

            private static string BuildMemberSummary(WatchEntry entry)
            {
                if (entry == null || entry.MemberInfo == null)
                    return "<成员丢失>";

                string name = entry.MemberInfo.Name;

                if (entry.MemberInfo is MethodInfo method)
                {
                    string parameters = string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name + " " + parameter.Name));
                    string returnType = method.ReturnType != null ? method.ReturnType.Name : "void";
                    return $"{name}({parameters}) : {returnType}";
                }

                if (entry.MemberInfo is FieldInfo field)
                    return $"{name} : {field.FieldType.Name}";

                if (entry.MemberInfo is PropertyInfo property)
                    return $"{name} : {property.PropertyType.Name}";

                return name;
            }

            private static object ReadEntryValue(MonoBehaviour owner, ESRuntimeWatchRegistry.Entry entry, bool allowGetMoudleFallback)
            {
                object current = ResolveEntryContext(owner, entry, allowGetMoudleFallback);

                if (current == null)
                    return "<空引用: " + entry.MemberPath + ">";

                if (entry.MemberInfo == null || entry.MemberInfo.DeclaringType == null || !entry.MemberInfo.DeclaringType.IsInstanceOfType(current))
                    return "<类型不匹配: " + entry.MemberPath + ">";

                if (entry.RequiresManualInvoke)
                    return "<点击按钮执行>";

                return ReadMemberValue(current, entry.MemberInfo);
            }

            public object ReadRawValue()
            {
                if (Owner == null || RegistryEntry.MemberInfo == null)
                    return null;

                object current = ResolveEntryContext(Owner as MonoBehaviour, RegistryEntry, true);
                if (current == null)
                    return null;

                if (RegistryEntry.MemberInfo is FieldInfo field)
                    return field.GetValue(current);

                if (RegistryEntry.MemberInfo is PropertyInfo property && property.CanRead && property.GetIndexParameters().Length == 0)
                    return property.GetValue(current);

                if (RegistryEntry.MemberInfo is MethodInfo method && method.GetParameters().Length == 0 && method.ReturnType != typeof(void))
                    return method.Invoke(current, null);

                return null;
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
                    return "<不是方法: " + entry.MemberPath + ">";

                if (method.GetParameters().Length > 0)
                    return "<方法需要参数: " + entry.MemberPath + ">";

                if (method.DeclaringType == null || !method.DeclaringType.IsInstanceOfType(current))
                    return "<类型不匹配: " + entry.MemberPath + ">";

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
                    UnityEngine.Debug.LogWarning($"[RuntimeWatch] GetMoudle<{moduleType.Name}> fallback failed: {inner.GetType().Name}: {inner.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[RuntimeWatch] GetMoudle<{moduleType.Name}> fallback failed: {ex.Message}");
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

            public string ReadValue()
            {
                return readValue == null ? "" : readValue();
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
