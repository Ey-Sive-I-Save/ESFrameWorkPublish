using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public enum ESWindowCommandScope
    {
        Core,
        Peripheral
    }

    /// <summary>ES 编辑器窗口命令描述。MenuPath 使用 Unity 可执行菜单路径。</summary>
    public sealed class ESWindowCommand
    {
        public string Id;
        public string DisplayName;
        public string Category;
        public string MenuPath;
        public string Shortcut;
        public string Keywords;
        public Texture2D Icon;
        public Action Open;
        public ESWindowCommandScope Scope;

        public string SearchText => string.Join(" ", DisplayName, Category, Keywords, MenuPath, ScopeDisplayName);
        public string ScopeDisplayName => Scope == ESWindowCommandScope.Core ? "核心窗口" : "零散窗口";
    }

    /// <summary>
    /// ES 工具启动器注册表：统一提供搜索、收藏、最近使用和固定排序数据。
    /// 数据只保存在 EditorPrefs，不进入运行时程序集和项目资源。
    /// </summary>
    public static class ESWindowCommandRegistry
    {
        private const string PreferencePrefix = "ES.WindowLauncher.";
        private const int MaxFavoriteCount = 32;
        private const int MaxRecentCount = 12;
        private static readonly Dictionary<string, ESWindowCommand> Commands = new Dictionary<string, ESWindowCommand>(StringComparer.Ordinal);
        private static readonly List<ESWindowCommand> SortedCommands = new List<ESWindowCommand>();
        private static readonly List<string> Favorites = new List<string>();
        private static readonly List<string> Recent = new List<string>();
        private static readonly List<string> FavoriteOrder = new List<string>();

        static ESWindowCommandRegistry()
        {
            LoadLists();
            RegisterBuiltIns();
            PrunePersistedLists();
        }

        // 由 AssemblyStream 的初始化器显式触发静态构造，避免把普通窗口注册挂到 Unity 全局域重载入口。
        internal static void EnsureInitialized()
        {
        }

        public static IReadOnlyList<ESWindowCommand> All => SortedCommands;
        public static IReadOnlyList<string> FavoriteIds => Favorites;
        public static IReadOnlyList<string> RecentIds => Recent;

        public static IEnumerable<ESWindowCommand> GetCommands(ESWindowCommandScope scope)
        {
            for (int i = 0; i < SortedCommands.Count; i++)
            {
                ESWindowCommand command = SortedCommands[i];
                if (command.Scope == scope)
                    yield return command;
            }
        }

        public static void Register(ESWindowCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Id) || string.IsNullOrWhiteSpace(command.DisplayName))
                return;
            Commands[command.Id] = command;
            RebuildSortedCommands();
        }

        private static void RebuildSortedCommands()
        {
            SortedCommands.Clear();
            SortedCommands.AddRange(Commands.Values);
            SortedCommands.Sort((left, right) =>
            {
                int category = string.Compare(
                    left?.Category, right?.Category, StringComparison.Ordinal);
                return category != 0
                    ? category
                    : string.Compare(left?.DisplayName, right?.DisplayName, StringComparison.Ordinal);
            });
        }

        public static bool IsFavorite(string id) => Favorites.Contains(id);

        public static void ToggleFavorite(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!Favorites.Remove(id)) Favorites.Add(id);
            if (!FavoriteOrder.Contains(id)) FavoriteOrder.Add(id);
            while (Favorites.Count > MaxFavoriteCount)
            {
                string removed = Favorites[0];
                Favorites.RemoveAt(0);
                FavoriteOrder.Remove(removed);
            }
            SaveLists();
        }

        public static void MoveFavorite(string id, int delta)
        {
            int index = FavoriteOrder.IndexOf(id);
            if (index < 0) return;
            int target = Mathf.Clamp(index + delta, 0, FavoriteOrder.Count - 1);
            if (target == index) return;
            FavoriteOrder.RemoveAt(index);
            FavoriteOrder.Insert(target, id);
            SaveLists();
        }

        public static IEnumerable<ESWindowCommand> GetFavoriteCommands()
        {
            foreach (string id in FavoriteOrder)
                if (Favorites.Contains(id) && Commands.TryGetValue(id, out ESWindowCommand command))
                    yield return command;
        }

        public static IEnumerable<ESWindowCommand> GetRecentCommands()
        {
            foreach (string id in Recent)
                if (Commands.TryGetValue(id, out ESWindowCommand command))
                    yield return command;
        }

        public static void Open(ESWindowCommand command)
        {
            if (command == null) return;
            RecordOpened(command.Id);
            if (command.Open != null) command.Open();
            else if (!string.IsNullOrEmpty(command.MenuPath)) EditorApplication.ExecuteMenuItem(command.MenuPath);
        }

        public static void RecordOpened(string id)
        {
            if (string.IsNullOrEmpty(id) || !Commands.ContainsKey(id)) return;
            if (Recent.Count > 0 && Recent[0] == id) return;
            Recent.Remove(id);
            Recent.Insert(0, id);
            while (Recent.Count > MaxRecentCount) Recent.RemoveAt(Recent.Count - 1);
            SaveLists();
        }

        private static void RegisterBuiltIns()
        {
            RegisterCoreBuiltIns();
            RegisterPeripheralBuiltIns();
        }

        private static void RegisterCoreBuiltIns()
        {
            Register(new ESWindowCommand { Id = "asset_window", DisplayName = "资产管理窗口", Category = "资源与发布", MenuPath = MenuItemPathDefine.RESOURCE_WINDOW_PATH, Keywords = "Library Consumer Catalog AB 资源" });
            Register(new ESWindowCommand { Id = "so_data_window", DisplayName = "SO 数据窗口", Category = "内容制作", MenuPath = MenuItemPathDefine.SO_DATA_WINDOW_PATH, Keywords = "ScriptableObject 数据 配置" });
            Register(new ESWindowCommand { Id = "simple_tools", DisplayName = "简单工具集", Category = "自动化与开发", MenuPath = MenuItemPathDefine.SIMPLE_TOOLS_WINDOW_PATH, Keywords = "批处理 工具" });
            Register(new ESWindowCommand { Id = "runtime_watch", DisplayName = "RuntimeWatch", Category = "验证与诊断", MenuPath = MenuItemPathDefine.RUNTIME_WATCH_WINDOW_PATH, Shortcut = "Ctrl+Shift+W", Keywords = "运行时 观察 监控 调试" });
            Register(new ESWindowCommand { Id = "track_editor", DisplayName = "轨道编辑器", Category = "内容制作", MenuPath = MenuItemPathDefine.TRACK_EDITOR_WINDOW_PATH, Keywords = "技能 Timeline Clip" });
            Register(new ESWindowCommand { Id = "stable_graph_v2", DisplayName = "稳定图编辑器 V2", Category = "内容制作", MenuPath = MenuItemPathDefine.STABLE_GRAPH_WINDOW_PATH, Keywords = "Graph 流程 行为树 AICommand Agent Skill" });
            Register(new ESWindowCommand { Id = "font_workbench", DisplayName = "字体资产工具", Category = "内容制作", MenuPath = MenuItemPathDefine.FONT_WORKBENCH_WINDOW_PATH, Keywords = "TMP 字符集 Fallback" });
            Register(new ESWindowCommand { Id = "cmd_agent", DisplayName = "Agent 控制台", Category = "自动化与开发", MenuPath = MenuItemPathDefine.AGENT_WORKBENCH_WINDOW_PATH, Keywords = "命令 AI Codex 自动化 Agent" });
            Register(new ESWindowCommand { Id = "command_palette", DisplayName = "ES 命令面板", Category = "自动化与开发", MenuPath = MenuItemPathDefine.COMMAND_PALETTE_WINDOW_PATH, Keywords = "Command Palette 快速命令 搜索" });
        }

        // 只收录可长期交互的窗口。Dialog、Popup、Picker、测试演示和带副作用的一次性动作不得进入此表。
        private static void RegisterPeripheralBuiltIns()
        {
            RegisterPeripheral("asset_package_bake", "资产包分离窗口", "资源与发布", MenuItemPathDefine.RESOURCE_DELIVERY_PATH + "构建与发布/资产包分离窗口", "Package Bake 资产包 烘焙 构建");
            RegisterPeripheral("resource_collection", "资源收集工作流", "资源与发布", MenuItemPathDefine.RESOURCE_DELIVERY_PATH + "资源收集/资源收集工作流", "Asset ResourcePlan ConfigKey 收集 检查");
            RegisterPeripheral("resource_runtime_monitor", "资源运行时监视器", "验证与诊断", MenuItemPathDefine.VALIDATION_RUNTIME_MONITORING_PATH + "资源系统/打开资源运行时监视器", "Scope Registry Provider Cache 资源 监视");
            RegisterPeripheral("automation_center", "自动化中心", "自动化与开发", MenuItemPathDefine.AUTOMATION_CENTER_PATH + "打开自动化中心", "Automation Task Preset 自动任务");
            RegisterPeripheral("developer_cockpit", "开发者驾驶舱", "验证与诊断", MenuItemPathDefine.VALIDATION_DIAGNOSTICS_PATH + "开发者驾驶舱/打开开发者驾驶舱", "Developer Cockpit Trace Observation 诊断");
            RegisterPeripheral("editor_theme", "编辑器主题", "项目配置", MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/打开主题设置", "Presentation 颜色 字体 动效 皮肤");
            RegisterPeripheral("editor_health", "ES 编辑器健康检查", "验证与诊断", MenuItemPathDefine.VALIDATION_EDITOR_HEALTH_PATH + "打开 ES 编辑器健康检查", "Presentation 缓存 Drawer 健康 只读");
            RegisterPeripheral("feedback_sound_scheme", "编辑器音效方案", "项目配置", MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/反馈音效/切换音效方案...", "Sound Audio Feedback 音效 方案");
            RegisterPeripheral("installer", "安装管理器", "自动化与开发", MenuItemPathDefine.INSTALL_DEPENDENCY_PATH + "打开安装管理器", "Installer Dependency 依赖 安装");
            RegisterPeripheral("ui_risk_audit", "UI 风险体检", "验证与诊断", MenuItemPathDefine.VALIDATION_STATIC_AUDIT_PATH + "打开 UI 风险体检", "UI UGUI Canvas Layout 性能 风险 审计");
            RegisterPeripheral("entity_stat_monitor", "Entity 属性监视器", "验证与诊断", MenuItemPathDefine.STAT_RUNTIME_PANEL_PATH, "Entity Stat Modifier 属性 监视");
            RegisterPeripheral("entity_interaction_monitor", "交互运行时面板", "验证与诊断", MenuItemPathDefine.INTERACTION_RUNTIME_PANEL_PATH, "Entity Interaction IK 交互 监视");
            RegisterPeripheral("dynamic_atlas_monitor", "动态图集监视器", "验证与诊断", MenuItemPathDefine.VALIDATION_RUNTIME_MONITORING_PATH + "动态图集/打开动态图集监视器", "Dynamic Atlas GPU 页面 上传 监视");
            RegisterPeripheral("camera_track_preview", "轨道相机预览", "内容制作", MenuItemPathDefine.CONTENT_CREATION_PATH + "相机/打开轨道相机预览", "Camera TrackView 相机 轨道 预览");
        }

        private static void RegisterPeripheral(
            string id,
            string displayName,
            string category,
            string menuPath,
            string keywords)
        {
            Register(new ESWindowCommand
            {
                Id = id,
                DisplayName = displayName,
                Category = category,
                MenuPath = menuPath,
                Keywords = keywords,
                Scope = ESWindowCommandScope.Peripheral
            });
        }

        private static string Key(string suffix) => PreferencePrefix + ProjectHash() + "." + suffix;

        private static string ProjectHash()
        {
            unchecked
            {
                int hash = 17;
                string value = Application.dataPath ?? string.Empty;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return hash.ToString("X8");
            }
        }

        private static void LoadLists()
        {
            Favorites.Clear(); Favorites.AddRange(ReadList("favorites"));
            Recent.Clear(); Recent.AddRange(ReadList("recent"));
            FavoriteOrder.Clear(); FavoriteOrder.AddRange(ReadList("favoriteOrder"));
        }

        private static void PrunePersistedLists()
        {
            bool changed = false;
            changed |= Favorites.RemoveAll(id => !Commands.ContainsKey(id)) > 0;
            changed |= Recent.RemoveAll(id => !Commands.ContainsKey(id)) > 0;
            changed |= FavoriteOrder.RemoveAll(id => !Commands.ContainsKey(id)) > 0;
            changed |= FavoriteOrder.RemoveAll(id => !Favorites.Contains(id)) > 0;
            for (int i = 0; i < Favorites.Count; i++)
            {
                if (FavoriteOrder.Contains(Favorites[i])) continue;
                FavoriteOrder.Add(Favorites[i]);
                changed = true;
            }
            while (Favorites.Count > MaxFavoriteCount)
            {
                string removed = Favorites[0];
                Favorites.RemoveAt(0);
                FavoriteOrder.Remove(removed);
                changed = true;
            }
            while (Recent.Count > MaxRecentCount)
            {
                Recent.RemoveAt(Recent.Count - 1);
                changed = true;
            }
            while (FavoriteOrder.Count > MaxFavoriteCount)
            {
                FavoriteOrder.RemoveAt(FavoriteOrder.Count - 1);
                changed = true;
            }
            if (changed) SaveLists();
        }

        private static List<string> ReadList(string suffix)
        {
            try
            {
                string raw = EditorPrefs.GetString(Key(suffix), string.Empty);
                return raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToList();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("读取 ES 工具启动器偏好失败，已使用空列表：" + exception.Message);
                return new List<string>();
            }
        }

        private static void SaveLists()
        {
            try
            {
                EditorPrefs.SetString(Key("favorites"), string.Join("\n", Favorites));
                EditorPrefs.SetString(Key("recent"), string.Join("\n", Recent));
                EditorPrefs.SetString(Key("favoriteOrder"), string.Join("\n", FavoriteOrder));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("保存 ES 工具启动器偏好失败：" + exception.Message);
            }
        }
    }

    public sealed class ESWindowCommandRegistryInitializer : EditorInvoker_Level1
    {
        public override void InitInvoke()
        {
            ESWindowCommandRegistry.EnsureInitialized();
        }
    }

    /// <summary>ES 工具启动器。常用窗口菜单只负责打开它，动态内容在此窗口内维护。</summary>
    public sealed class ESWindowLauncher : ESSinglePageIMGUIWindow<ESWindowLauncher>
    {
        private string search = string.Empty;
        private Vector2 scroll;
        private bool showFavorites = true;
        private bool showRecent = true;
        private bool showCore = true;
        private bool showPeripheral;

        [MenuItem(MenuItemPathDefine.WINDOW_LAUNCHER_PATH, false, 0)]
        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "工具启动器 %#e", false, -1100)]
        private static void OpenFromMenu()
        {
            OpenWindow();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES 工具启动器", "搜索、收藏并打开常用 ES 功能窗口");
        }
        public override string ESWindow_PresentationShortTitle => "启动器";

        protected override string ESWindow_Subtitle => "常用功能统一入口";
        protected override Vector2 ESWindow_MinSize => new Vector2(620f, 420f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(860f, 620f);
        protected override string ESWindow_PageStableId => "es.window-launcher";
        protected override string ESWindow_PageTitle => "窗口与工具";
        protected override string ESWindow_PageKeywords => "窗口 工具 搜索 收藏 最近使用";

        protected override void ESWindow_OnHostEnable()
        {
            base.ESWindow_OnHostEnable();
            maxSize = new Vector2(1400f, 1000f);
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            DrawToolbar();
            DrawSections();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUI.SetNextControlName("ESWindowLauncherSearch");
                search = EditorGUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField, GUILayout.MinWidth(280f));
                if (GUILayout.Button("快速搜索", EditorStyles.toolbarButton, GUILayout.Width(82f))) OpenSearchDropdown();
                if (GUILayout.Button("清除", EditorStyles.toolbarButton, GUILayout.Width(46f))) search = string.Empty;
            }
            if (Event.current.type == EventType.Layout && GUI.GetNameOfFocusedControl() == "")
                EditorGUI.FocusTextInControl("ESWindowLauncherSearch");
        }

        private void DrawSections()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (!string.IsNullOrWhiteSpace(search))
            {
                DrawSearchResults();
                EditorGUILayout.EndScrollView();
                return;
            }

            showFavorites = EditorGUILayout.Foldout(showFavorites, "★ 收藏窗口", true, EditorStyles.foldoutHeader);
            if (showFavorites)
            {
                bool hasFavorite = false;
                foreach (ESWindowCommand command in Filter(ESWindowCommandRegistry.GetFavoriteCommands()))
                {
                    hasFavorite = true;
                    DrawCommand(command, true);
                }
                if (!hasFavorite)
                    EditorGUILayout.HelpBox("暂无收藏。点击窗口右侧的星标即可固定。", MessageType.Info);
            }
            showRecent = EditorGUILayout.Foldout(showRecent, "最近使用", true, EditorStyles.foldoutHeader);
            if (showRecent)
                foreach (ESWindowCommand command in Filter(ESWindowCommandRegistry.GetRecentCommands())) DrawCommand(command, false);

            EditorGUILayout.Space(6f);
            showCore = EditorGUILayout.Foldout(showCore, "核心窗口", true, EditorStyles.foldoutHeader);
            if (showCore)
                DrawCommandGroups(Filter(ESWindowCommandRegistry.GetCommands(ESWindowCommandScope.Core)));

            showPeripheral = EditorGUILayout.Foldout(
                showPeripheral,
                "零散窗口目录",
                true,
                EditorStyles.foldoutHeader);
            if (showPeripheral)
            {
                EditorGUILayout.HelpBox(
                    "收集低频但可长期交互的 ES 面板；收藏、最近使用和搜索仍与核心窗口共用。",
                    MessageType.None);
                DrawCommandGroups(Filter(ESWindowCommandRegistry.GetCommands(ESWindowCommandScope.Peripheral)));
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSearchResults()
        {
            ESWindowCommand[] matches = Filter(ESWindowCommandRegistry.All).ToArray();
            EditorGUILayout.LabelField($"搜索结果  {matches.Length}", EditorStyles.boldLabel);
            if (matches.Length == 0)
            {
                EditorGUILayout.HelpBox("没有匹配的 ES 窗口。", MessageType.Info);
                return;
            }

            foreach (var scope in matches.GroupBy(c => c.Scope))
            {
                EditorGUILayout.LabelField(
                    scope.Key == ESWindowCommandScope.Core ? "核心窗口" : "零散窗口目录",
                    EditorStyles.miniBoldLabel);
                DrawCommandGroups(scope);
            }
        }

        private void DrawCommandGroups(IEnumerable<ESWindowCommand> commands)
        {
            foreach (var group in commands.GroupBy(c => c.Category))
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.miniBoldLabel);
                foreach (ESWindowCommand command in group)
                    DrawCommand(command, false);
            }
        }

        private IEnumerable<ESWindowCommand> Filter(IEnumerable<ESWindowCommand> source)
        {
            string term = search?.Trim();
            return string.IsNullOrEmpty(term) ? source : source.Where(c => c.SearchText.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void DrawCommand(ESWindowCommand command, bool showOrder)
        {
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                string label = command.DisplayName + (string.IsNullOrEmpty(command.Shortcut) ? string.Empty : "  [" + command.Shortcut + "]");
                if (GUILayout.Button(new GUIContent(label, command.Category), EditorStyles.label)) ESWindowCommandRegistry.Open(command);
                if (GUILayout.Button(ESWindowCommandRegistry.IsFavorite(command.Id) ? "★" : "☆", GUILayout.Width(28f))) ESWindowCommandRegistry.ToggleFavorite(command.Id);
                if (showOrder)
                {
                    if (GUILayout.Button("↑", GUILayout.Width(24f))) ESWindowCommandRegistry.MoveFavorite(command.Id, -1);
                    if (GUILayout.Button("↓", GUILayout.Width(24f))) ESWindowCommandRegistry.MoveFavorite(command.Id, 1);
                }
            }
        }

        private void OpenSearchDropdown()
        {
            Rect anchor = new Rect(position.width * 0.5f - 120f, 42f, 240f, 20f);
            ESSearchDropdown.Open(anchor, "搜索 ES 窗口", () =>
            {
                var entries = new List<ESSearchDropdown.Entry>();
                foreach (var scope in Filter(ESWindowCommandRegistry.All).GroupBy(c => c.Scope))
                foreach (var group in scope.GroupBy(c => c.Category))
                    foreach (ESWindowCommand command in group)
                        entries.Add(ESSearchDropdown.Entry.Item(
                            command.DisplayName,
                            () => ESWindowCommandRegistry.Open(command),
                            command.ScopeDisplayName + " / " + group.Key,
                            command.Icon,
                            tooltip: command.MenuPath,
                            badge: command.Shortcut,
                            selected: ESWindowCommandRegistry.IsFavorite(command.Id)));
                if (entries.Count == 0) entries.Add(ESSearchDropdown.Entry.Disabled("没有匹配的 ES 窗口"));
                return entries;
            }, minimumWindowSize: new Vector2(560f, 360f), hostWindow: this);
        }
    }
}
