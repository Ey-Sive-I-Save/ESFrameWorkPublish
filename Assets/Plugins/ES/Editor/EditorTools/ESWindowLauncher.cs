using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
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

        public string SearchText => string.Join(" ", DisplayName, Category, Keywords, MenuPath);
    }

    /// <summary>
    /// ES 工具启动器注册表：统一提供搜索、收藏、最近使用和固定排序数据。
    /// 数据只保存在 EditorPrefs，不进入运行时程序集和项目资源。
    /// </summary>
    public static class ESWindowCommandRegistry
    {
        private const string PreferencePrefix = "ES.WindowLauncher.";
        private static readonly Dictionary<string, ESWindowCommand> Commands = new Dictionary<string, ESWindowCommand>(StringComparer.Ordinal);
        private static readonly List<string> Favorites = new List<string>();
        private static readonly List<string> Recent = new List<string>();
        private static readonly List<string> FavoriteOrder = new List<string>();

        static ESWindowCommandRegistry()
        {
            LoadLists();
            RegisterBuiltIns();
        }

        // 由 AssemblyStream 的初始化器显式触发静态构造，避免把普通窗口注册挂到 Unity 全局域重载入口。
        internal static void EnsureInitialized()
        {
        }

        public static IReadOnlyList<ESWindowCommand> All => Commands.Values.OrderBy(c => c.Category).ThenBy(c => c.DisplayName).ToList();
        public static IReadOnlyList<string> FavoriteIds => Favorites;
        public static IReadOnlyList<string> RecentIds => Recent;

        public static void Register(ESWindowCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Id) || string.IsNullOrWhiteSpace(command.DisplayName))
                return;
            Commands[command.Id] = command;
            if (!FavoriteOrder.Contains(command.Id)) FavoriteOrder.Add(command.Id);
        }

        public static bool IsFavorite(string id) => Favorites.Contains(id);

        public static void ToggleFavorite(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!Favorites.Remove(id)) Favorites.Add(id);
            if (!FavoriteOrder.Contains(id)) FavoriteOrder.Add(id);
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
            while (Recent.Count > 12) Recent.RemoveAt(Recent.Count - 1);
            SaveLists();
        }

        private static void RegisterBuiltIns()
        {
            Register(new ESWindowCommand { Id = "asset_window", DisplayName = "资产管理窗口", Category = "资源与发布", MenuPath = MenuItemPathDefine.RESOURCE_WINDOW_PATH, Keywords = "Library Consumer Catalog AB 资源" });
            Register(new ESWindowCommand { Id = "so_data_window", DisplayName = "SO 数据窗口", Category = "内容制作", MenuPath = MenuItemPathDefine.CONTENT_CREATION_PATH + "数据与配置/SO 数据窗口", Keywords = "ScriptableObject 数据 配置" });
            Register(new ESWindowCommand { Id = "simple_tools", DisplayName = "简单工具集", Category = "开发与维护", MenuPath = MenuItemPathDefine.DEVELOPMENT_MAINTENANCE_PATH + "综合工具/简单工具集", Keywords = "批处理 工具" });
            Register(new ESWindowCommand { Id = "runtime_watch", DisplayName = "RuntimeWatch", Category = "运行时诊断", MenuPath = MenuItemPathDefine.RUNTIME_DIAGNOSTICS_PATH + "RuntimeWatch/打开运行时观察", Shortcut = "Ctrl+Shift+W", Keywords = "运行时 观察 监控 调试" });
            Register(new ESWindowCommand { Id = "es_presentation_boundary", DisplayName = "ES 多态边界测试", Category = "示例与测试", MenuPath = MenuItemPathDefine.TEST_TOOLS_PATH + "ES 编辑器扩展/创建多态边界测试层级", Keywords = "ESEditorSection SerializeReference 多目标 嵌套 数组 Profiler" });
            Register(new ESWindowCommand { Id = "track_editor", DisplayName = "轨道编辑器", Category = "内容制作", MenuPath = MenuItemPathDefine.CONTENT_CREATION_PATH + "技能与轨道/轨道编辑器", Keywords = "技能 Timeline Clip" });
            Register(new ESWindowCommand { Id = "stable_graph_v2", DisplayName = "稳定图编辑器 V2", Category = "常用窗口", MenuPath = MenuItemPathDefine.QUICK_WINDOWS_PATH + "稳定图编辑器 V2", Keywords = "Graph 流程 行为树 AICommand Agent Skill" });
            Register(new ESWindowCommand { Id = "font_workbench", DisplayName = "字体资产工作台", Category = "内容制作", MenuPath = MenuItemPathDefine.CONTENT_CREATION_PATH + "UI 与字体/字体资产工作台", Keywords = "TMP 字符集 Fallback" });
            Register(new ESWindowCommand { Id = "cmd_agent", DisplayName = "Cmd Agent", Category = "开发与维护", MenuPath = MenuItemPathDefine.DEVELOPMENT_MAINTENANCE_PATH + "自动化/Cmd Agent（CMD 中转与架构师）", Keywords = "命令 AI Codex 自动化" });
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

        private static List<string> ReadList(string suffix)
        {
            string raw = EditorPrefs.GetString(Key(suffix), string.Empty);
            return raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToList();
        }

        private static void SaveLists()
        {
            EditorPrefs.SetString(Key("favorites"), string.Join("\n", Favorites));
            EditorPrefs.SetString(Key("recent"), string.Join("\n", Recent));
            EditorPrefs.SetString(Key("favoriteOrder"), string.Join("\n", FavoriteOrder));
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
    public sealed class ESWindowLauncher : EditorWindow
    {
        private string search = string.Empty;
        private Vector2 scroll;
        private bool showFavorites = true;
        private bool showRecent = true;

        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "工具启动器 %#e", false, -1100)]
        public static void OpenWindow()
        {
            var window = GetWindow<ESWindowLauncher>(false, "ES 工具启动器", true);
            window.minSize = new Vector2(620f, 420f);
            window.Show();
            window.Focus();
        }

        private void OnGUI()
        {
            ES.EditorInternal.ESEditorPresentation.BindWindow(this);
            DrawToolbar();
            DrawSections();
        }

        private void OnDestroy()
        {
            ES.EditorInternal.ESEditorPresentation.UnbindWindow(this);
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
            showFavorites = EditorGUILayout.Foldout(showFavorites, "★ 收藏窗口", true, EditorStyles.foldoutHeader);
            if (showFavorites)
            {
                foreach (ESWindowCommand command in Filter(ESWindowCommandRegistry.GetFavoriteCommands())) DrawCommand(command, true);
                if (!ESWindowCommandRegistry.GetFavoriteCommands().Any()) EditorGUILayout.HelpBox("暂无收藏。点击窗口右侧的星标即可固定。", MessageType.Info);
            }
            showRecent = EditorGUILayout.Foldout(showRecent, "最近使用", true, EditorStyles.foldoutHeader);
            if (showRecent)
                foreach (ESWindowCommand command in Filter(ESWindowCommandRegistry.GetRecentCommands())) DrawCommand(command, false);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("全部窗口", EditorStyles.boldLabel);
            foreach (var group in Filter(ESWindowCommandRegistry.All).GroupBy(c => c.Category))
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.miniBoldLabel);
                foreach (ESWindowCommand command in group) DrawCommand(command, false);
            }
            EditorGUILayout.EndScrollView();
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
                foreach (var group in Filter(ESWindowCommandRegistry.All).GroupBy(c => c.Category))
                    foreach (ESWindowCommand command in group)
                        entries.Add(ESSearchDropdown.Entry.Item(
                            command.DisplayName,
                            () => ESWindowCommandRegistry.Open(command),
                            group.Key,
                            command.Icon,
                            tooltip: command.MenuPath,
                            badge: command.Shortcut,
                            selected: ESWindowCommandRegistry.IsFavorite(command.Id)));
                if (entries.Count == 0) entries.Add(ESSearchDropdown.Entry.Disabled("没有匹配的 ES 窗口"));
                return entries;
            }, minimumWindowSize: new Vector2(560f, 360f));
        }
    }
}
