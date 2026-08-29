using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ES
{
    public static class ESCommandPaletteShortcutSettings
    {
        public const string ShortcutId = "ES/Command Palette";
        private const string EnabledKey = "ES.CommandPalette.Shortcut.Enabled";

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
        }

        public static void SetEnabled(bool enabled)
        {
            EditorPrefs.SetBool(EnabledKey, enabled);
            try
            {
                if (enabled)
                {
                    ShortcutManager.instance.ClearShortcutOverride(ShortcutId);
                }
                else
                {
                    ShortcutManager.instance.RebindShortcut(ShortcutId, ShortcutBinding.empty);
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[ES Command Palette] 快捷键状态更新失败：" + exception.Message);
            }
        }

        public static void RestoreDefaultBinding()
        {
            EditorPrefs.SetBool(EnabledKey, true);
            try
            {
                ShortcutManager.instance.ClearShortcutOverride(ShortcutId);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[ES Command Palette] 默认快捷键恢复失败：" + exception.Message);
            }
        }

        public static string FindConflictingShortcutId()
        {
            IShortcutManager manager = ShortcutManager.instance;
            ShortcutBinding paletteBinding = manager.GetShortcutBinding(ShortcutId);
            if (paletteBinding.Equals(ShortcutBinding.empty))
            {
                return string.Empty;
            }

            foreach (string shortcutId in manager.GetAvailableShortcutIds())
            {
                if (string.Equals(shortcutId, ShortcutId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (paletteBinding.Equals(manager.GetShortcutBinding(shortcutId)))
                {
                    return shortcutId;
                }
            }

            return string.Empty;
        }
    }

    public sealed class ESCommandPaletteSearchEngine
    {
        public const int MaximumResults = 50;
        public const long AllocationBudgetBytes = 64 * 1024;
        public const int MaximumQueryCharacters = 1024;

        private static readonly Dictionary<string, string> SearchAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "zcg", "资产管理窗口" },
                { "zcgl", "资产管理窗口" },
                { "zichan", "资产管理窗口" },
                { "asset", "资源" },
                { "res", "资源" },
                { "resource", "资源" },
                { "so", "SO 数据窗口" },
                { "sodata", "SO 数据" },
                { "shuju", "数据" },
                { "global", "全局配置" },
                { "globaldata", "全局配置" },
                { "quanjv", "全局配置" },
                { "ai", "AI 命令" },
                { "cmd", "AI 命令" },
                { "command", "AI 命令" },
                { "rtw", "RuntimeWatch" },
                { "runtimewatch", "RuntimeWatch" },
                { "yunxing", "运行时" },
                { "track", "轨道" },
                { "guidao", "轨道" },
                { "tool", "工具" },
                { "gongju", "工具" },
                { "theme", "主题" },
                { "zhuti", "主题" },
                { "gamecore", "GameCore" },
                { "core", "GameCore" },
                { "pool", "对象池" },
                { "duixiangchi", "对象池" },
                { "prefab", "Prefab" },
                { "changjing", "场景" },
                { "scene", "场景" }
            };

        // Keep only the top-K candidates while scanning. The registry can contain
        // thousands of commands; retaining every match makes a simple query create
        // an avoidable allocation spike before the final 50 results are selected.
        private readonly List<ScoredItem> scoredItems = new List<ScoredItem>(MaximumResults);
        private readonly List<ESCommandPaletteItem> results = new List<ESCommandPaletteItem>(MaximumResults);
        private readonly ReadOnlyCollection<ESCommandPaletteItem> resultsView;

        public ESCommandPaletteSearchEngine()
        {
            resultsView = results.AsReadOnly();
        }

        public IReadOnlyList<ESCommandPaletteItem> Results => resultsView;
        public ESCommandPaletteSearchMetrics LastMetrics { get; private set; }

        public IReadOnlyList<ESCommandPaletteItem> Search(string query, IReadOnlyList<ESCommandPaletteItem> allItems)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            scoredItems.Clear();
            results.Clear();

            string text = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            if (text.Length > MaximumQueryCharacters)
                text = text.Substring(0, MaximumQueryCharacters);
            char prefix = text.Length > 0 && IsPrefix(text[0]) ? text[0] : '\0';
            string term = prefix == '\0' ? text : text.Substring(1).Trim();
            // Tokenize once per query. Splitting inside Score used to allocate a
            // new array for every candidate in the command registry.
            string[] tokens = term.Length > 0
                ? term.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();
            int candidateCount = 0;

            if (allItems != null)
            {
                for (int i = 0; i < allItems.Count; i++)
                {
                    ESCommandPaletteItem item = allItems[i];
                    if (item == null || string.IsNullOrEmpty(item.Title) || !MatchesPrefix(item, prefix))
                    {
                        continue;
                    }

                    candidateCount++;
                    int score = Score(item, term, tokens);
                    if (term.Length > 0 && score <= 0)
                    {
                        continue;
                    }

                    AddTopScoredItem(new ScoredItem(item, score));
                }
            }

            int resultCount = scoredItems.Count;
            for (int i = 0; i < resultCount; i++)
            {
                results.Add(scoredItems[i].Item);
            }

            long finished = Stopwatch.GetTimestamp();
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            LastMetrics = new ESCommandPaletteSearchMetrics(
                (finished - started) * 1000d / Stopwatch.Frequency,
                Math.Max(0, allocatedAfter - allocatedBefore),
                candidateCount,
                results.Count,
                AllocationBudgetBytes);
            return resultsView;
        }

        private void AddTopScoredItem(ScoredItem candidate)
        {
            IComparer<ScoredItem> comparer = ScoredItemComparer.Instance;
            int insertionIndex = scoredItems.BinarySearch(candidate, comparer);
            if (insertionIndex < 0)
                insertionIndex = ~insertionIndex;

            if (insertionIndex >= MaximumResults)
                return;

            scoredItems.Insert(insertionIndex, candidate);
            if (scoredItems.Count > MaximumResults)
                scoredItems.RemoveAt(scoredItems.Count - 1);
        }

        public void Clear()
        {
            scoredItems.Clear();
            results.Clear();
            LastMetrics = default;
        }

        private static bool IsPrefix(char value)
        {
            return value == '@'
                || value == '#'
                || value == '$'
                || value == '★'
                || value == 'r'
                || value == 'G'
                || value == 'g';
        }

        private static bool MatchesPrefix(ESCommandPaletteItem item, char prefix)
        {
            if (prefix == '\0')
            {
                return true;
            }

            if (prefix == '★')
            {
                return ESCommandPaletteRegistry.IsFavorite(item.StableId);
            }

            if (prefix == 'r')
            {
                return ESCommandPaletteRegistry.TryGetRecentRank(item.StableId, out _);
            }

            if (prefix == 'G' || prefix == 'g')
            {
                return item.Prefix != null
                    && item.Prefix.Length == 1
                    && (item.Prefix[0] == 'G' || item.Prefix[0] == 'g');
            }

            return item.Prefix != null && item.Prefix.Length == 1 && item.Prefix[0] == prefix;
        }

        private static int Score(ESCommandPaletteItem item, string term, string[] tokens)
        {
            if (term.Length == 0)
            {
                return 1000 + ScorePopularity(item);
            }

            int total = 0;
            int count = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                int tokenScore = ScoreToken(item, token);
                if (tokenScore <= 0)
                {
                    return 0;
                }

                total += tokenScore;
                count++;
            }

            if (count == 0)
            {
                return 1000 + ScorePopularity(item);
            }

            int score = total / count + (count > 1 ? 60 : 0);
            return score + ScorePopularity(item);
        }

        private static int ScoreToken(ESCommandPaletteItem item, string token)
        {
            if (item == null || string.IsNullOrEmpty(item.Title) || string.IsNullOrEmpty(token))
                return 0;

            if (item.Title.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            {
                return 1000;
            }

            if (item.Title.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 800;
            }

            if (!string.IsNullOrEmpty(item.SearchText)
                && item.SearchText.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 500;
            }

            string aliasTarget = TryResolveAlias(token);
            if (!string.IsNullOrEmpty(aliasTarget)
                && (item.Title.IndexOf(aliasTarget, StringComparison.OrdinalIgnoreCase) >= 0
                    || !string.IsNullOrEmpty(item.SearchText)
                    && item.SearchText.IndexOf(aliasTarget, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return 400;
            }

            return 0;
        }

        private static int ScorePopularity(ESCommandPaletteItem item)
        {
            int score = 0;
            if (ESCommandPaletteRegistry.IsFavorite(item.StableId))
            {
                score += 100;
            }

            if (ESCommandPaletteRegistry.TryGetRecentRank(item.StableId, out int recentRank))
            {
                score += Math.Max(1, 50 - recentRank);
            }

            return score;
        }

        private static string TryResolveAlias(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            return SearchAliases.TryGetValue(token, out string target)
                ? target
                : string.Empty;
        }

        private readonly struct ScoredItem
        {
            public ScoredItem(ESCommandPaletteItem item, int score)
            {
                Item = item;
                Score = score;
            }

            public ESCommandPaletteItem Item { get; }
            public int Score { get; }
        }

        private sealed class ScoredItemComparer : IComparer<ScoredItem>
        {
            public static readonly ScoredItemComparer Instance = new ScoredItemComparer();

            public int Compare(ScoredItem left, ScoredItem right)
            {
                int score = right.Score.CompareTo(left.Score);
                if (score != 0)
                    return score;

                int title = string.Compare(left.Item.Title, right.Item.Title, StringComparison.Ordinal);
                if (title != 0)
                    return title;

                return string.Compare(left.Item.StableId, right.Item.StableId, StringComparison.Ordinal);
            }
        }
    }

    [ES.ESWindowSleepContract(
        ES.ESWindowSleepMode.Transient,
        ES.ESWindowSurfaceKind.Popup,
        "短生命周期命令面板")]
    public sealed class ESCommandPaletteWindow : EditorWindow, IESWindowPresentationShortTitle
    {
        public string ESWindow_PresentationShortTitle => "命令";
        private const double SearchDebounceSeconds = 0.18d;
        private const float RowHeight = 50f;
        private const float MinDetailWidth = 760f;
        private const string LastTabKey = "ES.CommandPalette.LastTab";
        private const int MaximumQueryHistory = 20;
        private const double StyleRetryBackoffSeconds = 0.25d;
        private static readonly Vector2 DefaultSize = new Vector2(800f, 520f);
        private static readonly Vector2 MinimumSize = new Vector2(360f, 320f);
        private static readonly Vector2 MaximumSize = new Vector2(1100f, 760f);

        private static string NormalizeQuery(string value)
        {
            string normalized = value ?? string.Empty;
            return normalized.Length > ESCommandPaletteSearchEngine.MaximumQueryCharacters
                ? normalized.Substring(0, ESCommandPaletteSearchEngine.MaximumQueryCharacters)
                : normalized;
        }

        private readonly ESCommandPaletteSearchEngine searchEngine = new ESCommandPaletteSearchEngine();
        private readonly List<string> queryHistory = new List<string>(24);
        private string query = string.Empty;
        private string draftQuery = string.Empty;
        private string lastQuery = string.Empty;
        private string activeTab = string.Empty;
        private int queryHistoryIndex = -1;
        private Vector2 scroll;
        private Vector2 tabScroll;
        private int selected;
        private double nextSearchAt = double.MaxValue;
        private string feedback = string.Empty;
        private bool focusSearchOnNextLayout;
        private int hoveredIndex = -1;
        private int indexedCount;
        private bool searchTickRegistered;
        private bool shortcutCheckTickRegistered;
        private bool stylesAcquired;
        private bool lifecycleActive;
        private ShortcutConflictState shortcutConflictState = ShortcutConflictState.NoConflict;
        private string shortcutConflictMessage = string.Empty;
        private double nextShortcutConflictCheck = double.MinValue;
        private IReadOnlyList<ESCommandPaletteItem> results = Array.Empty<ESCommandPaletteItem>();

        private static GUIStyle headerTitleStyle;
        private static GUIStyle headerSubtitleStyle;
        private static GUIStyle headerMetaStyle;
        private static GUIStyle searchFieldStyle;
        private static GUIStyle tabStyle;
        private static GUIStyle tabSelectedStyle;
        private static GUIStyle resultStyle;
        private static GUIStyle hoveredResultStyle;
        private static GUIStyle selectedResultStyle;
        private static GUIStyle resultTitleStyle;
        private static GUIStyle resultDescriptionStyle;
        private static GUIStyle categoryHeaderStyle;
        private static GUIStyle prefixBadgeStyle;
        private static GUIStyle categoryBadgeStyle;
        private static GUIStyle actionBadgeStyle;
        private static GUIStyle starStyle;
        private static GUIStyle emptyTitleStyle;
        private static GUIStyle emptyDescriptionStyle;
        private static GUIStyle detailPanelStyle;
        private static GUIStyle detailHeaderStyle;
        private static GUIStyle detailTitleStyle;
        private static GUIStyle detailDescriptionStyle;
        private static GUIStyle detailLabelStyle;
        private static GUIStyle detailValueStyle;
        private static GUIStyle footerStyle;
        private static bool stylesReady;
        private static bool stylesProSkin;
        private static int styleUsers;
        private static int lastThemeGeneration = -1;
        private static int lastSkinGeneration = -1;
        private static readonly List<Texture2D> CreatedTextures = new List<Texture2D>();
        private static bool styleFailureLogged;
        private static double nextStyleRetryAt;

        private enum ShortcutConflictState
        {
            NoConflict,
            Conflict,
            Failed
        }

        private readonly struct ShortcutConflictCheck
        {
            private ShortcutConflictCheck(ShortcutConflictState state, string message)
            {
                State = state;
                Message = message ?? string.Empty;
            }

            public ShortcutConflictState State { get; }
            public string Message { get; }

            public static ShortcutConflictCheck NoConflict()
            {
                return new ShortcutConflictCheck(ShortcutConflictState.NoConflict, string.Empty);
            }

            public static ShortcutConflictCheck Conflict(string conflict)
            {
                return new ShortcutConflictCheck(ShortcutConflictState.Conflict, conflict);
            }

            public static ShortcutConflictCheck Failed(string message)
            {
                return new ShortcutConflictCheck(ShortcutConflictState.Failed, message);
            }
        }

        [MenuItem(MenuItemPathDefine.COMMAND_PALETTE_WINDOW_PATH, false, 0)]
        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "ES 命令面板", false, -1200)]
        public static void OpenMenuWindow()
        {
            OpenWindow();
        }

        [Shortcut(ESCommandPaletteShortcutSettings.ShortcutId, KeyCode.P, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        private static void OpenShortcut()
        {
            if (ESCommandPaletteShortcutSettings.Enabled)
            {
                OpenWindow();
            }
        }

        [MenuItem(MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/ES 命令面板/启用快捷键", false, 100)]
        private static void ToggleShortcut()
        {
            ESCommandPaletteShortcutSettings.SetEnabled(!ESCommandPaletteShortcutSettings.Enabled);
        }

        [MenuItem(MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/ES 命令面板/启用快捷键", true)]
        private static bool ValidateToggleShortcut()
        {
            Menu.SetChecked(
                MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/ES 命令面板/启用快捷键",
                ESCommandPaletteShortcutSettings.Enabled);
            return true;
        }

        [MenuItem(MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/ES 命令面板/恢复默认快捷键", false, 101)]
        private static void RestoreDefaultShortcut()
        {
            ESCommandPaletteShortcutSettings.RestoreDefaultBinding();
        }

        [MenuItem(MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/ES 命令面板/检查快捷键冲突", false, 102)]
        private static void CheckShortcutConflict()
        {
            ShortcutConflictCheck check = RunShortcutConflictCheck();
            if (check.State == ShortcutConflictState.Failed)
            {
                ShowShortcutStatusDialog(
                    "冲突检测失败：" + check.Message,
                    ESDialogTone.Warning);
                return;
            }

            if (check.State == ShortcutConflictState.Conflict)
            {
                ShowShortcutStatusDialog(
                    "发现冲突：" + check.Message + Environment.NewLine + "可在“恢复默认快捷键”中重置。",
                    ESDialogTone.Warning);
                return;
            }

            ShowShortcutStatusDialog("未发现冲突。", ESDialogTone.Success);
        }

        private static void ShowShortcutStatusDialog(string message, ESDialogTone tone)
        {
            ESDialogService.ShowModal(new ESAdvancedDialogRequest
            {
                dialogId = "es.command-palette.shortcut-status",
                title = "ES 命令面板快捷键",
                message = message ?? string.Empty,
                confirmText = "确定",
                cancelText = string.Empty,
                showCancel = false,
                tone = tone,
                minSize = new Vector2(420f, 220f),
                preferredSize = new Vector2(560f, 300f),
                allowMainWorkspaceFallback = true,
                duplicatePolicy = ESDialogDuplicatePolicy.FocusExisting,
            });
        }

        public static void OpenWindow()
        {
            bool alreadyOpen = HasOpenInstances<ESCommandPaletteWindow>();
            if (alreadyOpen)
            {
                ESCommandPaletteRegistry.EnsureInitialized();
            }
            else
            {
                ESCommandPaletteRegistry.Refresh();
            }

            ESCommandPaletteWindow window = GetWindow<ESCommandPaletteWindow>(true, "ES 命令面板", true);
            try
            {
                window.minSize = MinimumSize;
                window.maxSize = MaximumSize;
                window.titleContent = new GUIContent("ES 命令面板");
                window.ShowUtility();
                window.Focus();
            }
            catch (Exception exception)
            {
                if (!alreadyOpen)
                {
                    try { window.Close(); }
                    catch (Exception closeException) { Debug.LogException(closeException); }
                }
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] 显示命令面板失败，已拒绝继续初始化。", exception));
                return;
            }
            if (!alreadyOpen)
            {
                CenterWindowInMainEditor(window);
            }
            if (alreadyOpen)
            {
                window.query = NormalizeQuery(window.query);
            }
            else
            {
                string lastTab = string.Empty;
                try
                {
                    lastTab = SessionState.GetString(LastTabKey, string.Empty);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "[ESCommandPalette] 重开时读取上次标签失败，已回退默认标签。", exception));
                }
                window.activeTab = lastTab;
                window.query = NormalizeQuery(lastTab);
            }
            window.selected = 0;
            window.focusSearchOnNextLayout = true;
            window.ScheduleSearch();
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Open);
        }

        private static void CenterWindowInMainEditor(ESCommandPaletteWindow window)
        {
            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            if (window == null
                || !IsFinite(mainWindow.x) || !IsFinite(mainWindow.y)
                || !IsFinite(mainWindow.width) || !IsFinite(mainWindow.height)
                || mainWindow.width <= 0f || mainWindow.height <= 0f)
            {
                return;
            }
            float width = Mathf.Min(
                DefaultSize.x,
                Mathf.Max(MinimumSize.x, mainWindow.width - 80f));
            float height = Mathf.Min(
                DefaultSize.y,
                Mathf.Max(MinimumSize.y, mainWindow.height - 120f));

            window.position = new Rect(
                window.position.x,
                window.position.y,
                width,
                height);
            window.position = new Rect(
                mainWindow.x + Mathf.Max(0f, (mainWindow.width - width) * 0.5f),
                mainWindow.y + Mathf.Max(0f, (mainWindow.height - height) * 0.5f),
                width,
                height);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static void OpenWithQuery(string initialQuery)
        {
            OpenWindow();
            ESCommandPaletteWindow window = GetWindow<ESCommandPaletteWindow>();
            window.query = NormalizeQuery(initialQuery);
            window.activeTab = window.query;
            window.selected = 0;
            window.ScheduleSearch();
        }

        private void OnEnable()
        {
            lifecycleActive = true;
            nextStyleRetryAt = 0d;
            try
            {
                ESWindowFoundation.BindTransient(this);
                if (!stylesAcquired)
                {
                    AcquireStyles();
                    stylesAcquired = true;
                }
                ESCommandPaletteRegistry.EnsureInitialized();
                focusSearchOnNextLayout = true;
                ScheduleSearch();
            }
            catch (Exception exception)
            {
                lifecycleActive = false;
                UnregisterSearchTick();
                UnregisterShortcutCheckTick();
                if (stylesAcquired)
                {
                    try { ReleaseStyles(); }
                    catch (Exception releaseException) { Debug.LogException(releaseException); }
                    stylesAcquired = false;
                }
                try { ESWindowFoundation.Suspend(this); }
                catch (Exception suspendException) { Debug.LogException(suspendException); }
                nextSearchAt = double.MaxValue;
                searchEngine.Clear();
                results = Array.Empty<ESCommandPaletteItem>();
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] 启动初始化失败，已回滚窗口状态。", exception));
            }
        }

        private void OnDisable()
        {
            lifecycleActive = false;
            try
            {
                SessionState.SetString(LastTabKey, activeTab);
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] 关闭时保存标签失败。", exception));
            }
            try
            {
                ESWindowFoundation.Suspend(this);
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] 关闭挂起协议失败。", exception));
            }
            finally
            {
                UnregisterSearchTick();
                UnregisterShortcutCheckTick();
                if (stylesAcquired)
                {
                    ReleaseStyles();
                    stylesAcquired = false;
                }
                nextSearchAt = double.MaxValue;
                nextStyleRetryAt = 0d;
                searchEngine.Clear();
                results = Array.Empty<ESCommandPaletteItem>();
            }
        }

        private void OnDestroy()
        {
            lifecycleActive = false;
            try
            {
                ESWindowFoundation.Close(this);
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] 销毁关闭协议失败。", exception));
            }
            finally
            {
                UnregisterSearchTick();
                UnregisterShortcutCheckTick();
                if (stylesAcquired)
                {
                    ReleaseStyles();
                    stylesAcquired = false;
                }
                nextSearchAt = double.MaxValue;
                nextStyleRetryAt = 0d;
                searchEngine.Clear();
                results = Array.Empty<ESCommandPaletteItem>();
            }
        }

        private void OnGUI()
        {
            // Unity can deliver one final repaint while a utility window is
            // being disabled. Do not rebuild styles or process stale input in
            // that frame after transient resources have been released.
            if (!lifecycleActive)
                return;
            if (!stylesReady && EditorApplication.timeSinceStartup < nextStyleRetryAt)
                return;
            try
            {
                EnsureStyles();
            }
            catch (Exception exception)
            {
                stylesReady = false;
                nextStyleRetryAt = EditorApplication.timeSinceStartup + StyleRetryBackoffSeconds;
                if (!styleFailureLogged)
                {
                    styleFailureLogged = true;
                    Debug.LogException(new InvalidOperationException(
                        "[ESCommandPalette] 样式构建失败，已跳过本帧绘制。", exception));
                }
                return;
            }
            if (!stylesReady)
                return;
            if (EditorApplication.timeSinceStartup >= nextSearchAt)
            {
                UpdateResultsNow();
            }

            using (new EditorGUILayout.VerticalScope())
            {
                DrawHeader();
                DrawSearchBar();
                DrawCategoryTabs();
                DrawMainArea();
                DrawStatus();
            }

            HandleKeyboard(Event.current);
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("ES 命令面板", headerTitleStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(results.Count + " / " + indexedCount + " 条", headerMetaStyle);
            }

            EditorGUILayout.LabelField("发现 · 打开 · 定位 · 复制", headerSubtitleStyle);
            EditorGUILayout.Space(2f);
        }

        private void DrawSearchBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUIContent searchIcon = EditorGUIUtility.IconContent("Search Icon");
                if (searchIcon != null && searchIcon.image != null)
                {
                    GUILayout.Label(searchIcon, GUILayout.Width(20f));
                }

                GUI.SetNextControlName("ESCommandPaletteSearch");
                string next = NormalizeQuery(EditorGUILayout.TextField(
                    query, searchFieldStyle, GUILayout.MinWidth(120f)));
                if (!string.Equals(next, query, StringComparison.Ordinal))
                {
                    query = next;
                    queryHistoryIndex = -1;
                    SyncActiveTabFromQuery();
                    ScheduleSearch();
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Type);
                }

                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.MinWidth(56f)))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Refresh);
                    ESCommandPaletteRegistry.Refresh();
                    ScheduleSearch();
                }
            }

            if (focusSearchOnNextLayout && Event.current.type == EventType.Layout)
            {
                focusSearchOnNextLayout = false;
                EditorGUI.FocusTextInControl("ESCommandPaletteSearch");
            }
        }

        private void DrawCategoryTabs()
        {
            string[] labels = { "全部", "命令", "场景", "全局配置", "AI 命令", "收藏", "最近" };
            string[] prefixes = { "", "@", "#", "G", "$", "★", "r" };

            tabScroll = EditorGUILayout.BeginScrollView(
                tabScroll,
                GUILayout.Height(30f),
                GUILayout.ExpandWidth(true));
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    bool isSelected = string.Equals(activeTab, prefixes[i], StringComparison.Ordinal);
                    GUIStyle style = isSelected ? tabSelectedStyle : tabStyle;
                    if (GUILayout.Button(labels[i], style, GUILayout.Height(26f), GUILayout.MinWidth(72f)))
                    {
                        if (!isSelected)
                        {
                            ApplyTab(prefixes[i]);
                            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Navigate);
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawMainArea()
        {
            if (results.Count == 0)
            {
                DrawEmptyState();
                return;
            }

            if (position.width >= MinDetailWidth)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    float listWidth = Mathf.Max(360f, position.width * 0.58f);
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(listWidth)))
                    {
                        DrawResultList();
                    }

                    DrawDetailPanel();
                }
            }
            else
            {
                DrawResultList();
            }
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.Space(28f);
            GUIContent icon = EditorGUIUtility.IconContent("d_console.infoicon.sml");
            if (icon != null && icon.image != null)
            {
                GUILayout.Label(icon, GUILayout.Width(32f), GUILayout.Height(32f));
            }
            EditorGUILayout.LabelField("没有匹配的只读命令", emptyTitleStyle);
            EditorGUILayout.LabelField("可尝试 @ 命令、# 场景、$ AI 命令、G 全局配置、★ 收藏或 r 最近。", emptyDescriptionStyle);
            EditorGUILayout.Space(28f);
        }

        private void DrawResultList()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            string lastCategory = null;
            for (int i = 0; i < results.Count; i++)
            {
                ESCommandPaletteItem item = results[i];
                if (item == null)
                {
                    continue;
                }

                if (!string.Equals(lastCategory, item.Category, StringComparison.Ordinal))
                {
                    DrawCategoryHeader(item.Category);
                    lastCategory = item.Category;
                }

                DrawResultRow(item, i);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawCategoryHeader(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                return;
            }

            EditorGUILayout.Space(4f);
            GUILayout.Label(category, categoryHeaderStyle);
        }

        private void DrawResultRow(ESCommandPaletteItem item, int index)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, RowHeight, resultStyle, GUILayout.ExpandWidth(true));
            bool isSelected = index == selected;
            bool isHovered = index == hoveredIndex;
            GUIStyle rowStyle = isSelected ? selectedResultStyle : (isHovered ? hoveredResultStyle : resultStyle);

            if (Event.current.type == EventType.Repaint && rowStyle.normal.background != null)
            {
                GUI.DrawTexture(rect, rowStyle.normal.background);
            }

            const float gap = 8f;
            float prefixWidth = 24f;
            float starWidth = 26f;
            float categoryWidth = 76f;
            float actionWidth = 64f;
            float verticalCenter = rect.y + (rect.height - 18f) * 0.5f;

            Rect prefixRect = new Rect(rect.x + gap, verticalCenter, prefixWidth, 18f);
            Rect starRect = new Rect(rect.xMax - starWidth - gap, rect.y + (rect.height - 22f) * 0.5f, starWidth, 22f);
            Rect actionRect = new Rect(starRect.x - actionWidth - gap, verticalCenter, actionWidth, 18f);
            Rect categoryRect = new Rect(actionRect.x - categoryWidth - gap, verticalCenter, categoryWidth, 18f);
            Rect textRect = new Rect(prefixRect.xMax + gap, rect.y + 5f, categoryRect.x - prefixRect.xMax - gap * 2f, rect.height - 10f);
            Rect titleRect = new Rect(textRect.x, textRect.y, textRect.width, 18f);
            Rect descRect = new Rect(textRect.x, textRect.yMax + 2f, textRect.width, 14f);

            if (Event.current.type == EventType.Repaint)
            {
                bool isFavorite = ESCommandPaletteRegistry.IsFavorite(item.StableId);
                GUI.Label(prefixRect, PrefixGlyph(item.Prefix), prefixBadgeStyle);
                GUI.Label(titleRect, new GUIContent(item.Title, item.Description), resultTitleStyle);
                if (!string.IsNullOrEmpty(item.Description))
                {
                    GUI.Label(descRect, item.Description, resultDescriptionStyle);
                }
                GUI.Label(categoryRect, item.Category, categoryBadgeStyle);
                GUI.Label(actionRect, ActionText(item.ActionKind), actionBadgeStyle);
                GUI.Label(starRect, isFavorite ? "★" : "☆", starStyle);
            }

            Event current = Event.current;
            if (current.type == EventType.MouseMove)
            {
                bool inside = rect.Contains(current.mousePosition);
                if (inside && hoveredIndex != index)
                {
                    hoveredIndex = index;
                    Repaint();
                }
                else if (!inside && hoveredIndex == index)
                {
                    hoveredIndex = -1;
                    Repaint();
                }
            }
            else if (current.type == EventType.MouseDown && rect.Contains(current.mousePosition))
            {
                if (starRect.Contains(current.mousePosition))
                {
                    ESCommandPaletteRegistry.ToggleFavorite(item.StableId);
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Confirm);
                    current.Use();
                    return;
                }

                selected = index;
                if (current.button == 1)
                {
                    ShowContextMenu(item);
                    current.Use();
                    return;
                }

                ExecuteSelected();
                current.Use();
            }
        }

        private void ShowContextMenu(ESCommandPaletteItem item)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(PrimaryActionLabel(item.ActionKind)), false, () => ExecuteItem(item));

            ESCommandPaletteItem selectItem = CreateSelectItem(item);
            if (selectItem != null && item.ActionKind != ESCommandPaletteActionKind.Select)
            {
                menu.AddItem(new GUIContent("定位"), false, () => ExecuteItem(selectItem));
            }

            if (item.ActionKind == ESCommandPaletteActionKind.OpenFile)
            {
                menu.AddItem(new GUIContent("复制文本"), false, () => ExecuteItem(CreateCopyTextItem(item)));
            }

            if (item.ActionKind == ESCommandPaletteActionKind.OpenFile
                || item.ActionKind == ESCommandPaletteActionKind.CopyText)
            {
                menu.AddItem(new GUIContent("复制路径"), false, () => ExecuteItem(CreateCopyPathItem(item)));
            }
            else if (item.ActionKind == ESCommandPaletteActionKind.OpenAsset
                || item.ActionKind == ESCommandPaletteActionKind.Select)
            {
                menu.AddItem(new GUIContent("复制路径"), false,
                    () => CopyTargetFromContextMenu(item.TargetId));
            }

            menu.AddSeparator(string.Empty);
            bool isFavorite = ESCommandPaletteRegistry.IsFavorite(item.StableId);
            menu.AddItem(
                new GUIContent(isFavorite ? "取消收藏" : "收藏"),
                false,
                () => ToggleFavoriteFromContextMenu(item.StableId));
            menu.AddItem(new GUIContent("查看详情"), false, OpenDetailSelected);
            menu.ShowAsContext();
        }

        private void CopyTargetFromContextMenu(string targetId)
        {
            if (this == null || !lifecycleActive || string.IsNullOrEmpty(targetId))
                return;
            if (!TryCopyTargetToClipboard(targetId, out string copyError))
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Warning);
                feedback = "复制路径失败：" + copyError;
                Repaint();
                return;
            }
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Copy);
            feedback = "已复制路径 " + targetId;
            Repaint();
        }

        private static bool TryCopyTargetToClipboard(string targetId, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(targetId))
            {
                error = "目标路径为空";
                return false;
            }

            try
            {
                GUIUtility.systemCopyBuffer = targetId;
                return true;
            }
            catch (Exception exception)
            {
                error = string.IsNullOrEmpty(exception.Message) ? "编辑器剪贴板不可用" : exception.Message;
                return false;
            }
        }

        private void ToggleFavoriteFromContextMenu(string stableId)
        {
            if (this == null || !lifecycleActive || string.IsNullOrEmpty(stableId))
                return;

            ESCommandPaletteRegistry.ToggleFavorite(stableId);
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Confirm);
            Repaint();
        }

        private void DrawDetailPanel()
        {
            using (new EditorGUILayout.VerticalScope(detailPanelStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                GUILayout.Label("详情", detailHeaderStyle);

                if (selected < 0 || selected >= results.Count)
                {
                    GUILayout.Label("选择一条命令查看详情", detailDescriptionStyle);
                    return;
                }

                ESCommandPaletteItem item = results[selected];
                GUILayout.Label(item.Title, detailTitleStyle);
                if (!string.IsNullOrEmpty(item.Description))
                {
                    GUILayout.Label(item.Description, detailDescriptionStyle);
                }

                EditorGUILayout.Space(8f);
                DrawDetailRow("分类", item.Category);
                DrawDetailRow("动作", ActionText(item.ActionKind));
                DrawDetailRow("前缀", string.IsNullOrEmpty(item.Prefix) ? "全部" : item.Prefix);
                DrawDetailRow("目标", item.TargetId);

                EditorGUILayout.Space(12f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("执行", GUILayout.Height(28f), GUILayout.MinWidth(86f)))
                    {
                        ExecuteSelected();
                    }

                    bool isFavorite = ESCommandPaletteRegistry.IsFavorite(item.StableId);
                    if (GUILayout.Button(isFavorite ? "取消收藏" : "收藏", GUILayout.Height(28f), GUILayout.MinWidth(86f)))
                    {
                        ESCommandPaletteRegistry.ToggleFavorite(item.StableId);
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Confirm);
                    }
                }

                EditorGUILayout.Space(8f);
                GUILayout.Label(ShortcutHint(item), footerStyle);
            }
        }

        private void DrawDetailRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, detailLabelStyle, GUILayout.Width(56f));
                GUILayout.Label(string.IsNullOrEmpty(value) ? "-" : value, detailValueStyle);
            }
        }

        private void DrawStatus()
        {
            RefreshShortcutConflictIfNeeded();
            ESCommandPaletteSearchMetrics metrics = searchEngine.LastMetrics;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(
                    string.Format(
                        "{0}/{1}  {2:0.00} ms  GC {3} B",
                        metrics.ResultCount,
                        metrics.CandidateCount,
                        metrics.DurationMilliseconds,
                        metrics.AllocatedBytes),
                    footerStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label("@ 命令 · # 场景 · $ AI 命令 · G 全局配置 · ★ 收藏 · r 最近", footerStyle);
            }

            if (selected >= 0 && selected < results.Count)
            {
                GUILayout.Label(ShortcutHint(results[selected]), footerStyle);
            }
            else
            {
                GUILayout.Label("Enter 执行 · ↑↓ 选择 · Tab 分类 · Alt+↑↓ 历史 · Esc 关闭", footerStyle);
            }

            if (shortcutConflictState == ShortcutConflictState.Failed)
            {
                EditorGUILayout.HelpBox("快捷键冲突检测失败：" + shortcutConflictMessage, MessageType.Error);
            }
            else if (shortcutConflictState == ShortcutConflictState.Conflict)
            {
                EditorGUILayout.HelpBox(
                    "快捷键冲突：" + shortcutConflictMessage + "。可在【ES】/项目配置/编辑器体验/ES 命令面板/恢复默认快捷键。",
                    MessageType.Warning);
            }

            if (!metrics.IsWithinAllocationBudget)
            {
                EditorGUILayout.HelpBox("搜索 GC 超过当前预算", MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(feedback))
            {
                EditorGUILayout.HelpBox(feedback, MessageType.Info);
            }
        }

        private void RefreshShortcutConflictIfNeeded()
        {
            if (EditorApplication.timeSinceStartup >= nextShortcutConflictCheck)
            {
                nextShortcutConflictCheck = EditorApplication.timeSinceStartup + 2d;
                ShortcutConflictCheck check = ESCommandPaletteShortcutSettings.Enabled
                    ? RunShortcutConflictCheck()
                    : ShortcutConflictCheck.NoConflict();
                shortcutConflictState = check.State;
                shortcutConflictMessage = check.Message;
            }

        }

        private void ScheduleShortcutCheckTick()
        {
            if (shortcutCheckTickRegistered)
            {
                return;
            }

            shortcutCheckTickRegistered = true;
            EditorApplication.update += OnShortcutCheckTick;
        }

        private void OnShortcutCheckTick()
        {
            if (!lifecycleActive)
            {
                UnregisterShortcutCheckTick();
                return;
            }
            if (EditorApplication.timeSinceStartup < nextShortcutConflictCheck)
            {
                return;
            }

            UnregisterShortcutCheckTick();
            RefreshShortcutConflictIfNeeded();
            Repaint();
        }

        private void UnregisterShortcutCheckTick()
        {
            if (!shortcutCheckTickRegistered)
            {
                return;
            }

            shortcutCheckTickRegistered = false;
            EditorApplication.update -= OnShortcutCheckTick;
        }

        private static ShortcutConflictCheck RunShortcutConflictCheck()
        {
            try
            {
                string conflict = ESCommandPaletteShortcutSettings.FindConflictingShortcutId();
                return string.IsNullOrEmpty(conflict)
                    ? ShortcutConflictCheck.NoConflict()
                    : ShortcutConflictCheck.Conflict(conflict);
            }
            catch (Exception exception)
            {
                return ShortcutConflictCheck.Failed(exception.Message);
            }
        }

        private void ApplyTab(string prefix)
        {
            activeTab = prefix;
            query = NormalizeQuery(prefix + CurrentSearchTerm());
            selected = 0;
            ScheduleSearch();
        }

        private void SyncActiveTabFromQuery()
        {
            if (string.IsNullOrEmpty(query))
            {
                activeTab = string.Empty;
                return;
            }

            char first = query[0];
            activeTab = "@#$★rGg".IndexOf(first) >= 0 ? first.ToString() : string.Empty;
        }

        private string CurrentSearchTerm()
        {
            if (string.IsNullOrEmpty(query))
            {
                return string.Empty;
            }

            char first = query[0];
            return "@#$★rGg".IndexOf(first) >= 0 ? query.Substring(1).Trim() : query.Trim();
        }

        private static string PrefixGlyph(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return "•";
            }
            return prefix;
        }

        private static string ActionText(ESCommandPaletteActionKind kind)
        {
            switch (kind)
            {
                case ESCommandPaletteActionKind.OpenWindow:
                    return "打开窗口";
                case ESCommandPaletteActionKind.OpenMenu:
                    return "打开菜单";
                case ESCommandPaletteActionKind.OpenFile:
                    return "打开文件";
                case ESCommandPaletteActionKind.OpenAsset:
                    return "打开资产";
                case ESCommandPaletteActionKind.CopyText:
                    return "复制文本";
                case ESCommandPaletteActionKind.CopyPath:
                    return "复制路径";
                case ESCommandPaletteActionKind.Select:
                    return "定位";
                default:
                    return "动作";
            }
        }

        private static string PrimaryActionLabel(ESCommandPaletteActionKind kind)
        {
            switch (kind)
            {
                case ESCommandPaletteActionKind.OpenWindow:
                case ESCommandPaletteActionKind.OpenMenu:
                case ESCommandPaletteActionKind.OpenFile:
                case ESCommandPaletteActionKind.OpenAsset:
                    return "打开";
                case ESCommandPaletteActionKind.CopyText:
                    return "复制文本";
                case ESCommandPaletteActionKind.CopyPath:
                    return "复制路径";
                case ESCommandPaletteActionKind.Select:
                    return "定位";
                default:
                    return "执行";
            }
        }

        private static string ShortcutHint(ESCommandPaletteItem item)
        {
            if (item == null)
            {
                return "Enter 执行 · ↑↓ 选择 · Tab 分类 · Esc 关闭";
            }

            switch (item.ActionKind)
            {
                case ESCommandPaletteActionKind.OpenFile:
                case ESCommandPaletteActionKind.OpenAsset:
                    return "Enter 打开 · Ctrl+Enter 定位 · Ctrl+C 复制 · Esc 关闭";
                case ESCommandPaletteActionKind.OpenWindow:
                case ESCommandPaletteActionKind.OpenMenu:
                    return "Enter 打开 · Esc 关闭";
                case ESCommandPaletteActionKind.CopyText:
                case ESCommandPaletteActionKind.CopyPath:
                    return "Enter 复制 · Ctrl+C 复制 · Esc 关闭";
                case ESCommandPaletteActionKind.Select:
                    return "Enter 定位 · Ctrl+C 复制路径 · Esc 关闭";
                default:
                    return "Enter 执行 · ↑↓ 选择 · Esc 关闭";
            }
        }

        private void HandleKeyboard(Event currentEvent)
        {
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (currentEvent.keyCode == KeyCode.Tab)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Navigate);
                CycleCategory(currentEvent.shift);
                currentEvent.Use();
                return;
            }

            if (currentEvent.keyCode == KeyCode.Escape)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Close);
                Close();
                currentEvent.Use();
                return;
            }

            if (currentEvent.alt && currentEvent.keyCode == KeyCode.UpArrow)
            {
                NavigateQueryHistory(-1);
                currentEvent.Use();
                return;
            }

            if (currentEvent.alt && currentEvent.keyCode == KeyCode.DownArrow)
            {
                NavigateQueryHistory(1);
                currentEvent.Use();
                return;
            }

            if (currentEvent.keyCode == KeyCode.C && currentEvent.control)
            {
                CopySelectedShortcut();
                currentEvent.Use();
                return;
            }

            if (currentEvent.keyCode == KeyCode.DownArrow)
            {
                int next = Mathf.Min(selected + 1, Math.Max(0, results.Count - 1));
                if (next != selected)
                {
                    selected = next;
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Navigate);
                }
                currentEvent.Use();
                Repaint();
            }
            else if (currentEvent.keyCode == KeyCode.UpArrow)
            {
                int next = Mathf.Max(0, selected - 1);
                if (next != selected)
                {
                    selected = next;
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Navigate);
                }
                currentEvent.Use();
                Repaint();
            }
            else if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                if (currentEvent.control)
                {
                    LocateSelected();
                }
                else if (currentEvent.shift)
                {
                    OpenDetailSelected();
                }
                else
                {
                    ExecuteSelected();
                }
                currentEvent.Use();
            }
        }

        private void ExecuteSelected()
        {
            if (this == null || !lifecycleActive
                || selected < 0 || selected >= results.Count)
            {
                return;
            }

            ExecuteItem(results[selected]);
        }

        private void ExecuteItem(ESCommandPaletteItem item)
        {
            // GenericMenu callbacks can outlive this window when the host is
            // closed while the context menu is open. Do not touch window state
            // or dispatch a command from a stale callback.
            if (this == null || !lifecycleActive || item == null)
            {
                return;
            }

            if (ESCommandPaletteRegistry.TryGet(item.StableId, out ESCommandPaletteItem currentItem)
                && currentItem != null)
            {
                item = currentItem;
            }

            RecordQuery(query);
            ESEditorFeedbackSound.SuppressSelectionSound();
            ESCommandPaletteResult result;
            try
            {
                result = ESCommandPaletteExecutors.Execute(item);
            }
            catch (Exception exception)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                feedback = "命令执行异常：" + exception.Message + "（建议：刷新命令面板后重试）";
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] 命令执行器未返回受管结果：" + item.Title,
                    exception));
                if (this != null && lifecycleActive)
                    Repaint();
                return;
            }
            if (result == null)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                feedback = "命令执行器未返回结果（建议：刷新命令面板后重试）";
                Debug.LogError("[ESCommandPalette] 命令执行器返回了 null：" + item.Title);
                if (this != null && lifecycleActive)
                    Repaint();
                return;
            }
            if (this == null || !lifecycleActive)
                return;
            if (result.Success)
            {
                PlaySuccessKind(item.ActionKind);
                if (ESCommandPaletteRegistry.TryGet(item.StableId, out _))
                {
                    ESCommandPaletteRegistry.RecordRecent(item.StableId);
                }
                feedback = "已执行：" + result.Message;
            }
            else
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                feedback = result.Message;
                if (!string.IsNullOrEmpty(result.RecoveryAction))
                {
                    feedback += "（建议：" + result.RecoveryAction + "）";
                }
            }

            if (this != null && lifecycleActive)
                Repaint();
        }

        private static void PlaySuccessKind(ESCommandPaletteActionKind kind)
        {
            switch (kind)
            {
                case ESCommandPaletteActionKind.Select:
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Locate);
                    break;
                case ESCommandPaletteActionKind.CopyText:
                case ESCommandPaletteActionKind.CopyPath:
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Copy);
                    break;
                case ESCommandPaletteActionKind.OpenFile:
                case ESCommandPaletteActionKind.OpenAsset:
                case ESCommandPaletteActionKind.OpenWindow:
                case ESCommandPaletteActionKind.OpenMenu:
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Open);
                    break;
                default:
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Success);
                    break;
            }
        }

        private void CycleCategory(bool reverse)
        {
            string[] prefixes = { "", "@", "#", "G", "$", "★", "r" };
            int index = Array.IndexOf(prefixes, activeTab);
            if (index < 0)
            {
                index = 0;
            }

            index = reverse
                ? (index - 1 + prefixes.Length) % prefixes.Length
                : (index + 1) % prefixes.Length;
            ApplyTab(prefixes[index]);
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Navigate);
        }

        private void NavigateQueryHistory(int direction)
        {
            if (queryHistory.Count == 0)
            {
                return;
            }

            if (direction < 0)
            {
                if (queryHistoryIndex < queryHistory.Count - 1)
                {
                    if (queryHistoryIndex < 0)
                    {
                        draftQuery = query;
                    }
                    queryHistoryIndex++;
                }
                else
                {
                    return;
                }
            }
            else
            {
                if (queryHistoryIndex > 0)
                {
                    queryHistoryIndex--;
                }
                else if (queryHistoryIndex == 0)
                {
                    queryHistoryIndex = -1;
                    query = draftQuery;
                    SyncActiveTabFromQuery();
                    ScheduleSearch();
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Navigate);
                    return;
                }
                else
                {
                    return;
                }
            }

            query = queryHistory[queryHistoryIndex];
            SyncActiveTabFromQuery();
            ScheduleSearch();
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Navigate);
        }

        private void RecordQuery(string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (normalized.Length == 0)
            {
                return;
            }

            queryHistory.Remove(normalized);
            queryHistory.Insert(0, normalized);
            if (queryHistory.Count > MaximumQueryHistory)
            {
                queryHistory.RemoveRange(MaximumQueryHistory, queryHistory.Count - MaximumQueryHistory);
            }

            queryHistoryIndex = -1;
        }

        private void LocateSelected()
        {
            if (this == null || !lifecycleActive
                || selected < 0 || selected >= results.Count)
            {
                return;
            }

            ESCommandPaletteItem selectItem = CreateSelectItem(results[selected]);
            if (selectItem == null)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Warning);
                feedback = "当前命令不支持定位";
                Repaint();
                return;
            }

            ExecuteItem(selectItem);
        }

        private void OpenDetailSelected()
        {
            if (this == null || !lifecycleActive
                || selected < 0 || selected >= results.Count)
            {
                return;
            }

            ESCommandPaletteItem item = results[selected];
            string detail = item.Title;
            if (!string.IsNullOrEmpty(item.Category))
            {
                detail += " | " + item.Category;
            }
            if (!string.IsNullOrEmpty(item.TargetId))
            {
                detail += " | " + item.TargetId;
            }
            if (!string.IsNullOrEmpty(item.Description))
            {
                detail += Environment.NewLine + item.Description;
            }

            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Open);
            feedback = detail;
            Repaint();
        }

        private void CopySelectedShortcut()
        {
            if (this == null || !lifecycleActive
                || selected < 0 || selected >= results.Count)
            {
                return;
            }

            ESCommandPaletteItem item = results[selected];
            if (item.ActionKind == ESCommandPaletteActionKind.CopyText)
            {
                ExecuteItem(CreateCopyTextItem(item));
                return;
            }

            if (item.ActionKind == ESCommandPaletteActionKind.CopyPath)
            {
                ExecuteItem(item);
                return;
            }

            if (item.ActionKind == ESCommandPaletteActionKind.OpenFile)
            {
                ExecuteItem(CreateCopyPathItem(item));
                return;
            }

            if (item.ActionKind == ESCommandPaletteActionKind.OpenAsset
                || item.ActionKind == ESCommandPaletteActionKind.Select)
            {
                if (!TryCopyTargetToClipboard(item.TargetId, out string copyError))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Warning);
                    feedback = "复制路径失败：" + copyError;
                    Repaint();
                    return;
                }
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Copy);
                feedback = "已复制路径 " + item.TargetId;
                Repaint();
                return;
            }

            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Warning);
            feedback = "当前命令不支持复制";
            Repaint();
        }

        private static ESCommandPaletteItem CreateSelectItem(ESCommandPaletteItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.TargetId))
            {
                return null;
            }

            if (item.ActionKind != ESCommandPaletteActionKind.OpenFile
                && item.ActionKind != ESCommandPaletteActionKind.OpenAsset
                && item.ActionKind != ESCommandPaletteActionKind.CopyText
                && item.ActionKind != ESCommandPaletteActionKind.CopyPath
                && item.ActionKind != ESCommandPaletteActionKind.Select)
            {
                return null;
            }

            string selectId = item.ItemId.EndsWith(":select", StringComparison.Ordinal)
                ? item.ItemId
                : item.ItemId + ":select";
            return new ESCommandPaletteItem(
                selectId,
                item.Title + "（定位）",
                item.Description,
                item.Category,
                item.Keywords,
                item.Prefix,
                item.TargetId,
                ESCommandPaletteActionKind.Select);
        }

        private static ESCommandPaletteItem CreateCopyTextItem(ESCommandPaletteItem item)
        {
            if (item == null)
            {
                return null;
            }

            if (item.ActionKind == ESCommandPaletteActionKind.CopyText)
            {
                return item;
            }

            return new ESCommandPaletteItem(
                item.ItemId + ":copy-text",
                item.Title + "（复制文本）",
                item.Description,
                item.Category,
                item.Keywords,
                item.Prefix,
                item.TargetId,
                ESCommandPaletteActionKind.CopyText);
        }

        private static ESCommandPaletteItem CreateCopyPathItem(ESCommandPaletteItem item)
        {
            if (item == null)
            {
                return null;
            }

            if (item.ActionKind == ESCommandPaletteActionKind.CopyPath)
            {
                return item;
            }

            return new ESCommandPaletteItem(
                item.ItemId + ":copy-path",
                item.Title + "（复制路径）",
                item.Description,
                item.Category,
                item.Keywords,
                item.Prefix,
                item.TargetId,
                ESCommandPaletteActionKind.CopyPath);
        }

        private void ScheduleSearch()
        {
            if (!lifecycleActive)
            {
                return;
            }
            nextSearchAt = EditorApplication.timeSinceStartup + SearchDebounceSeconds;
            if (!searchTickRegistered)
            {
                searchTickRegistered = true;
                EditorApplication.update += OnSearchDebounceTick;
            }

            Repaint();
        }

        private void OnSearchDebounceTick()
        {
            if (!lifecycleActive)
            {
                UnregisterSearchTick();
                return;
            }
            if (EditorApplication.timeSinceStartup < nextSearchAt)
            {
                return;
            }

            UnregisterSearchTick();
            UpdateResultsNow();
        }

        private void UnregisterSearchTick()
        {
            if (!searchTickRegistered)
            {
                return;
            }

            searchTickRegistered = false;
            EditorApplication.update -= OnSearchDebounceTick;
        }

        private void UpdateResultsNow()
        {
            UnregisterSearchTick();
            try
            {
                UpdateResults();
            }
            catch (Exception exception)
            {
                searchEngine.Clear();
                results = Array.Empty<ESCommandPaletteItem>();
                selected = 0;
                feedback = "搜索暂时失败：" + exception.Message + "（建议：重新输入或重开命令面板）";
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] 搜索结果更新失败。", exception));
            }
            if (this != null && lifecycleActive)
                Repaint();
        }

        private void UpdateResults()
        {
            nextSearchAt = double.MaxValue;
            string previousSelectedId = selected >= 0 && selected < results.Count
                ? results[selected]?.StableId
                : null;
            results = searchEngine.Search(query, ESCommandPaletteRegistry.AllItems);
            results = OrderResultsForDisplay(results);
            indexedCount = ESCommandPaletteRegistry.ItemCount;
            if (!string.Equals(query, lastQuery, StringComparison.Ordinal))
            {
                lastQuery = query;
                selected = 0;
            }
            else
            {
                int restoredIndex = -1;
                if (!string.IsNullOrEmpty(previousSelectedId))
                {
                    for (int index = 0; index < results.Count; index++)
                    {
                        if (string.Equals(results[index]?.StableId, previousSelectedId, StringComparison.Ordinal))
                        {
                            restoredIndex = index;
                            break;
                        }
                    }
                }
                selected = restoredIndex >= 0
                    ? restoredIndex
                    : Mathf.Clamp(selected, 0, Math.Max(0, results.Count - 1));
            }
            hoveredIndex = -1;
        }

        private static IReadOnlyList<ESCommandPaletteItem> OrderResultsForDisplay(
            IReadOnlyList<ESCommandPaletteItem> source)
        {
            if (source == null || source.Count == 0)
            {
                return source;
            }

            string[] preferredOrder =
            {
                "GlobalData",
                "AICommand",
                "场景",
                "窗口",
                "资源与发布",
                "内容制作",
                "开发与维护",
                "运行时诊断",
                "自动化"
            };
            var ordered = new List<ESCommandPaletteItem>(source.Count);
            var added = new HashSet<ESCommandPaletteItem>();

            for (int i = 0; i < preferredOrder.Length; i++)
            {
                AddCategory(ordered, added, source, preferredOrder[i]);
            }

            var remainingCategories = new List<string>();
            for (int i = 0; i < source.Count; i++)
            {
                ESCommandPaletteItem item = source[i];
                if (item == null || added.Contains(item))
                {
                    continue;
                }

                if (!remainingCategories.Contains(item.Category))
                {
                    remainingCategories.Add(item.Category);
                }
            }

            for (int i = 0; i < remainingCategories.Count; i++)
            {
                AddCategory(ordered, added, source, remainingCategories[i]);
            }

            return ordered;
        }

        private static void AddCategory(
            List<ESCommandPaletteItem> destination,
            HashSet<ESCommandPaletteItem> added,
            IReadOnlyList<ESCommandPaletteItem> source,
            string category)
        {
            for (int i = 0; i < source.Count; i++)
            {
                ESCommandPaletteItem item = source[i];
                if (item != null
                    && string.Equals(item.Category, category, StringComparison.Ordinal)
                    && added.Add(item))
                {
                    destination.Add(item);
                }
            }
        }

        private static void EnsureStyles()
        {
            int themeGeneration = ES.EditorInternal.ESEditorPresentation.ThemeGeneration;
            int skinGeneration = ES.EditorInternal.ESEditorPresentation.SkinGeneration;
            if (stylesReady)
            {
                if (stylesProSkin == EditorGUIUtility.isProSkin
                    && lastThemeGeneration == themeGeneration
                    && lastSkinGeneration == skinGeneration)
                {
                    return;
                }

                DestroyCreatedTextures();
                stylesReady = false;
            }
            else
            {
                // A previous build may have failed after creating only part of
                // the temporary textures. Clear that partial allocation before
                // retrying so repeated repaints cannot accumulate resources.
                DestroyCreatedTextures();
            }

            stylesProSkin = EditorGUIUtility.isProSkin;
            lastThemeGeneration = themeGeneration;
            lastSkinGeneration = skinGeneration;
            bool pro = stylesProSkin;

            Color row = pro ? new Color(0.18f, 0.20f, 0.22f, 0.92f) : new Color(0.97f, 0.97f, 0.97f, 0.94f);
            Color hover = pro ? new Color(0.23f, 0.26f, 0.30f, 0.96f) : new Color(0.90f, 0.93f, 0.96f, 0.96f);
            Color selected = pro ? new Color(0.20f, 0.38f, 0.62f, 0.92f) : new Color(0.26f, 0.52f, 0.86f, 0.22f);
            Color accent = pro ? new Color(0.42f, 0.72f, 0.98f) : new Color(0.10f, 0.35f, 0.70f);
            Color muted = pro ? new Color(0.72f, 0.75f, 0.80f) : new Color(0.36f, 0.39f, 0.44f);
            Color badge = pro ? new Color(0.25f, 0.31f, 0.42f, 0.85f) : new Color(0.88f, 0.91f, 0.96f, 0.92f);
            Color actionBadge = pro ? new Color(0.20f, 0.34f, 0.30f, 0.92f) : new Color(0.84f, 0.94f, 0.90f, 0.95f);

            headerTitleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = pro ? new Color(0.90f, 0.93f, 0.96f) : new Color(0.08f, 0.11f, 0.16f) }
            };
            headerSubtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                normal = { textColor = muted },
                richText = true
            };
            headerMetaStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = accent }
            };
            searchFieldStyle = new GUIStyle(GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField)
            {
                fontSize = 13,
                richText = true
            };

            tabStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 11,
                normal = { textColor = muted },
                padding = new RectOffset(8, 8, 3, 3)
            };
            tabSelectedStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = accent, background = SolidTexture(pro ? new Color(0.22f, 0.36f, 0.58f, 0.95f) : new Color(0.82f, 0.91f, 1.00f, 0.95f)) },
                padding = new RectOffset(8, 8, 3, 3)
            };

            resultStyle = new GUIStyle
            {
                normal = { background = SolidTexture(row) },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(8, 8, 6, 6)
            };
            hoveredResultStyle = new GUIStyle(resultStyle)
            {
                normal = { background = SolidTexture(hover) }
            };
            selectedResultStyle = new GUIStyle(resultStyle)
            {
                normal = { background = SolidTexture(selected) }
            };

            resultTitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = pro ? new Color(0.94f, 0.96f, 0.98f) : new Color(0.08f, 0.12f, 0.18f) }
            };
            resultDescriptionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                richText = true,
                normal = { textColor = muted }
            };
            categoryHeaderStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = accent },
                padding = new RectOffset(2, 0, 4, 2)
            };
            prefixBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = accent }
            };
            categoryBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                normal = { textColor = pro ? new Color(0.80f, 0.86f, 0.94f) : new Color(0.14f, 0.20f, 0.34f), background = SolidTexture(badge) },
                padding = new RectOffset(6, 6, 2, 2)
            };
            actionBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                normal = { textColor = pro ? new Color(0.72f, 0.90f, 0.84f) : new Color(0.08f, 0.35f, 0.25f), background = SolidTexture(actionBadge) },
                padding = new RectOffset(6, 6, 2, 2)
            };
            starStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                padding = new RectOffset(0, 0, 0, 0)
            };

            emptyTitleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = muted }
            };
            emptyDescriptionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = muted }
            };

            detailPanelStyle = new GUIStyle
            {
                normal = { background = SolidTexture(pro ? new Color(0.12f, 0.13f, 0.15f, 0.98f) : new Color(0.91f, 0.92f, 0.94f, 0.98f)) },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(14, 14, 12, 12)
            };
            detailHeaderStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = accent }
            };
            detailTitleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = pro ? new Color(0.94f, 0.96f, 0.98f) : new Color(0.08f, 0.12f, 0.18f) }
            };
            detailDescriptionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = 11,
                normal = { textColor = muted }
            };
            detailLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = muted }
            };
            detailValueStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                normal = { textColor = pro ? new Color(0.84f, 0.88f, 0.94f) : new Color(0.20f, 0.24f, 0.30f) }
            };
            footerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                normal = { textColor = muted }
            };
            // Only publish the ready flag after every style and texture has been
            // created successfully; a mid-build exception must be retried.
            stylesReady = true;
            styleFailureLogged = false;
            nextStyleRetryAt = 0d;
        }

        private static Texture2D SolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, UnityEngine.TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixel(0, 0, color);
                texture.Apply();
                texture.hideFlags = HideFlags.HideAndDontSave;
                CreatedTextures.Add(texture);
                return texture;
            }
            catch (Exception createException)
            {
                if (texture != null)
                {
                    try { DestroyImmediate(texture); }
                    catch (Exception destroyException)
                    {
                        Debug.LogException(new InvalidOperationException(
                            "[ESCommandPalette] 样式纹理创建失败后的清理也失败。", destroyException));
                    }
                }
                throw createException;
            }
        }

        private static void AcquireStyles()
        {
            styleUsers++;
        }

        private static void ReleaseStyles()
        {
            styleUsers = Math.Max(0, styleUsers - 1);
            if (styleUsers != 0)
            {
                return;
            }

            DestroyCreatedTextures();
            stylesReady = false;
        }

        private static void DestroyCreatedTextures()
        {
            for (int i = 0; i < CreatedTextures.Count; i++)
            {
                if (CreatedTextures[i] != null)
                {
                    try
                    {
                        DestroyImmediate(CreatedTextures[i]);
                    }
                    catch (Exception exception)
                    {
                        UnityEngine.Debug.LogException(new InvalidOperationException(
                            "[ES Command Palette] 临时样式纹理释放失败。", exception));
                    }
                }
            }

            CreatedTextures.Clear();
        }
    }
}
