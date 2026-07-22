using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;

namespace ES.Obsolete{
    /// <summary>
    /// ES开发管理窗口 V2.0 - Notion风格
    /// 功能完善的团队协作开发管理系统
    /// </summary>
    public partial class ESDevManagementWindow_V2 : ESMenuTreeWindowAB<ESDevManagementWindow_V2>
    {
        [MenuItem(MenuItemPathDefine.EDITOR_TOOLS_PATH + "【日志】开发管理窗口 ", false, 1)]
        private static void OpenDevWindow()
        {
            OpenWindow();
        }

        private static new void OpenWindow()
        {
            UsingWindow = GetWindow<ESDevManagementWindow_V2>();
            UsingWindow.titleContent = new GUIContent("ES开发管理 V2", "Notion风格协作工具");
            UsingWindow.minSize = new Vector2(1200, 700);
            UsingWindow.MenuWidth = 250;
            UsingWindow.Show();
        }

        #region 常量定义
        private const string DataSavePath = "Assets/Plugins/ES/Obsolete/Assets_ES_Legacy/DevManagement/DevManagementDataV2.asset";
        private const string EditorPrefKey = "ESDevManagement_V2_DataGUID";
        private const string CurrentUserKey = "ESDevManagement_V2_CurrentUser";

        // 页面名称
        private const string PageName_Dashboard = "仪表板";
        private const string PageName_DevLogList = "开发日志/列表视图";
        private const string PageName_DevLogCreate = "开发日志/新建";
        private const string PageName_DevLogDetail = "开发日志/详情";
        private const string PageName_TaskBoard = "任务看板/看板视图";
        private const string PageName_TaskList = "任务看板/列表视图";
        private const string PageName_TaskCalendar = "任务看板/日历视图";
        private const string PageName_TaskCreate = "任务看板/新建";
        private const string PageName_TaskDetail = "任务看板/详情";
        private const string PageName_Timeline = "时间线";
        private const string PageName_Tags = "标签管理";
        private const string PageName_Settings = "设置";
        #endregion

        #region 页面实例
        [NonSerialized] private Page_Dashboard pageDashboard;
        [NonSerialized] private Page_DevLogList pageDevLogList;
        [NonSerialized] private Page_CreateDevLog pageCreateDevLog;
        [NonSerialized] private Page_DevLogDetail pageDevLogDetail;
        [NonSerialized] private Page_TaskBoard pageTaskBoard;
        [NonSerialized] private Page_TaskList pageTaskList;
        [NonSerialized] private Page_TaskCalendar pageTaskCalendar;
        [NonSerialized] private Page_CreateTask pageCreateTask;
        [NonSerialized] private Page_TaskDetail pageTaskDetail;
        [NonSerialized] private Page_Timeline pageTimeline;
        [NonSerialized] private Page_Tags pageTags;
        [NonSerialized] private Page_Settings pageSettings;
        #endregion

        #region 数据
        private DevManagementDataV2 dataAsset;
        private string currentUser = Environment.UserName;

        // 公开数据访问，供子页面使用
        public DevManagementDataV2 DataAsset => dataAsset;
        #endregion

        protected override void OnImGUI()
        {
            if (UsingWindow == null)
            {
                UsingWindow = this;
                ES_LoadData();
            }

            // 确保数据始终有效
            if (dataAsset == null)
            {
                ES_LoadData();
            }

            base.OnImGUI();
        }

        public override void ES_LoadData()
        {
            if (EditorPrefs.HasKey(CurrentUserKey))
                currentUser = EditorPrefs.GetString(CurrentUserKey);
            else
            {
                currentUser = Environment.UserName;
                EditorPrefs.SetString(CurrentUserKey, currentUser);
            }

            if (EditorPrefs.HasKey(EditorPrefKey))
            {
                string guid = EditorPrefs.GetString(EditorPrefKey);
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    dataAsset = AssetDatabase.LoadAssetAtPath<DevManagementDataV2>(path);
            }

            if (dataAsset == null)
            {
                dataAsset = AssetDatabase.LoadAssetAtPath<DevManagementDataV2>(DataSavePath);

                if (dataAsset == null)
                {
                    dataAsset = ScriptableObject.CreateInstance<DevManagementDataV2>();
                    dataAsset.InitializeDefault();

                    string dir = System.IO.Path.GetDirectoryName(DataSavePath);
                    ESDesignUtility.SafeEditor.Quick_System_CreateDirectory(dir);

                    AssetDatabase.CreateAsset(dataAsset, DataSavePath);
                    AssetDatabase.SaveAssets();
                }

                string assetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(dataAsset));
                EditorPrefs.SetString(EditorPrefKey, assetGuid);
            }
        }

        public override void ES_SaveData()
        {
            if (dataAsset != null)
            {
                dataAsset.lastModifiedBy = currentUser;
                dataAsset.lastModifiedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                EditorUtility.SetDirty(dataAsset);
                AssetDatabase.SaveAssets();
            }
        }

        protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
            base.ES_OnBuildMenuTree(tree);

            tree.Config.DrawSearchToolbar = true;
            tree.DefaultMenuStyle.IconSize = 22;
            tree.DefaultMenuStyle.Height = 30;

            if (dataAsset == null) ES_LoadData();

            BuildMenuPages(tree);
        }

        private void BuildMenuPages(OdinMenuTree tree)
        {
            // 仪表板
            QuickBuildRootMenu(tree, PageName_Dashboard, ref pageDashboard, SdfIconType.GridFill);
            pageDashboard.data = dataAsset;
            pageDashboard.window = this;

            // 开发日志
            QuickBuildRootMenu(tree, PageName_DevLogList, ref pageDevLogList, SdfIconType.JournalText);
            pageDevLogList.data = dataAsset;
            pageDevLogList.window = this;
            pageDevLogList.RefreshList();

            pageCreateDevLog = new Page_CreateDevLog { data = dataAsset, window = this, currentUser = currentUser };
            MenuItems[PageName_DevLogCreate] = tree.Add(PageName_DevLogCreate, pageCreateDevLog, SdfIconType.JournalPlus).Last();
            
            pageDevLogDetail = new Page_DevLogDetail { data = dataAsset, window = this };
            MenuItems[PageName_DevLogDetail] = tree.Add(PageName_DevLogDetail, pageDevLogDetail, SdfIconType.FileText).Last();

            // 任务看板
            QuickBuildRootMenu(tree, PageName_TaskBoard, ref pageTaskBoard, SdfIconType.KanbanFill);
            pageTaskBoard.data = dataAsset;
            pageTaskBoard.window = this;
            pageTaskBoard.RefreshBoard();

            QuickBuildRootMenu(tree, PageName_TaskList, ref pageTaskList, SdfIconType.ListTask);
            pageTaskList.data = dataAsset;
            pageTaskList.window = this;
            pageTaskList.RefreshList();

            pageCreateTask = new Page_CreateTask { data = dataAsset, window = this, currentUser = currentUser };
            MenuItems[PageName_TaskCreate] = tree.Add(PageName_TaskCreate, pageCreateTask, SdfIconType.PlusSquareFill).Last();
            
            pageTaskDetail = new Page_TaskDetail { data = dataAsset, window = this };
            MenuItems[PageName_TaskDetail] = tree.Add(PageName_TaskDetail, pageTaskDetail, SdfIconType.CardText).Last();

            // 任务日历视图
            QuickBuildRootMenu(tree, PageName_TaskCalendar, ref pageTaskCalendar, SdfIconType.Calendar);
            pageTaskCalendar.data = dataAsset;
            pageTaskCalendar.window = this;

            // 时间线
            QuickBuildRootMenu(tree, PageName_Timeline, ref pageTimeline, SdfIconType.ClockHistory);
            pageTimeline.data = dataAsset;
            pageTimeline.window = this;

            // 标签管理
            QuickBuildRootMenu(tree, PageName_Tags, ref pageTags, SdfIconType.TagsFill);
            pageTags.data = dataAsset;
            pageTags.window = this;

            // 设置
            QuickBuildRootMenu(tree, PageName_Settings, ref pageSettings, SdfIconType.GearFill);
            pageSettings.data = dataAsset;
            pageSettings.window = this;
            pageSettings.currentUser = currentUser;

            tree.EnumerateTree().ForEach(item =>
            {
                if (item.Value == null)
                    item.Toggled = item.GetParentMenuItemsRecursive(false).Count() == 0;
            });
        }

        // 选择日志详情（通过 ID 重新定位，避免引用不一致）
        public void SelectLogDetail(DevLogEntryV2 log)
        {
            if (pageDevLogDetail != null && log != null)
            {
                // 尽量从数据资产中找到同 ID 的正式实例，防止传入的是拷贝
                var targetLog = dataAsset?.devLogs?.FirstOrDefault(l => l.id == log.id) ?? log;

                DevManagementSoundManager.PlayClickSound();
                pageDevLogDetail.selectedLog = targetLog;
                MenuItems[PageName_DevLogDetail]?.Select(true);
                Repaint();
            }
        }

        // 选择任务详情
        public void SelectTaskDetail(TaskEntryV2 task)
        {
            if (pageTaskDetail != null && task != null)
            {
                DevManagementSoundManager.PlayClickSound();
                pageTaskDetail.selectedTask = task;
                MenuItems[PageName_TaskDetail]?.Select();
                Repaint();
            }
        }

        #region 页面类定义

        // ==================== 仪表板 ====================
        [Serializable]
        public class Page_Dashboard : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [Title("开发协作仪表板", titleAlignment: TitleAlignments.Centered, bold: true)]
            [PropertySpace(SpaceAfter = 15)]

            // 统计卡片
            [FoldoutGroup("数据概览", Expanded = true)]
            [HorizontalGroup("数据概览/Row1", Width = 0.25f)]
            [BoxGroup("数据概览/Row1/日志"), HideLabel]
            [ShowInInspector, DisplayAsString(false)]
            [GUIColor(0.4f, 0.8f, 1f)]
            private string LogStats => $"📝 日志\n总计: {data?.devLogs?.Count ?? 0}  本周: {GetThisWeekLogs()}";

            [HorizontalGroup("数据概览/Row1", Width = 0.25f)]
            [BoxGroup("数据概览/Row1/任务"), HideLabel]
            [ShowInInspector, DisplayAsString(false)]
            [GUIColor(0.4f, 1f, 0.6f)]
            private string TaskStats => $"✓ 任务\n总计: {data?.tasks?.Count ?? 0}  活跃: {GetActiveTasks()}";

            [HorizontalGroup("数据概览/Row1", Width = 0.25f)]
            [BoxGroup("数据概览/Row1/完成率"), HideLabel]
            [ProgressBar(0, 100, ColorGetter = "GetProgressColor")]
            [ShowInInspector]
            [LabelText("完成率")]
            private double CompletionRate
            {
                get
                {
                    var total = data?.tasks?.Count ?? 0;
                    if (total == 0) return 0;
                    var completed = data.tasks.Count(t => t.status == TaskStatusV2.已完成);
                    return Math.Round((completed * 100.0) / total, 1);
                }
            }

            [HorizontalGroup("数据概览/Row1", Width = 0.25f)]
            [BoxGroup("数据概览/Row1/统计"), HideLabel]
            [ShowInInspector, DisplayAsString(false)]
            [GUIColor(1f, 0.8f, 0.4f)]
            private string GeneralStats => $"📊 统计\n标签: {data?.allTags?.Count ?? 0}  用户: {GetUserCount()}"; private int GetUserCount()
            {
                if (data == null) return 0;
                var users = new HashSet<string>();
                data.devLogs?.ForEach(l => users.Add(l.createdBy));
                data.tasks?.ForEach(t => users.Add(t.createdBy));
                return users.Count;
            }

            private int GetThisWeekLogs()
            {
                if (data?.devLogs == null) return 0;
                var weekStart = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek);
                return data.devLogs.Count(l => DateTime.TryParse(l.createTime, out var date) && date >= weekStart);
            }

            private int GetActiveTasks()
            {
                return data?.tasks?.Count(t => t.status == TaskStatusV2.进行中 || t.status == TaskStatusV2.开始) ?? 0;
            }

            private Color GetProgressColor(double value)
            {
                if (value >= 80) return new Color(0.2f, 0.8f, 0.3f);
                if (value >= 50) return new Color(0.9f, 0.7f, 0.2f);
                return new Color(0.9f, 0.3f, 0.3f);
            }

            // 最近活动时间线
            [FoldoutGroup("最近活动", Expanded = true)]
            [ShowInInspector, HideLabel, DisplayAsString(false)]
            [MultiLineProperty(12)]
            private string RecentTimeline
            {
                get
                {
                    if (data == null) return "📭 暂无活动记录";

                    var activities = new List<(DateTime time, string text, string type)>();

                    if (data.devLogs != null)
                    {
                        foreach (var log in data.devLogs.Take(8))
                        {
                            if (DateTime.TryParse(log.createTime, out var time))
                            {
                                var icon = log.priority == Priority.紧急 ? "🔴" : "📝";
                                activities.Add((time, $"{icon} {log.title} - {log.createdBy} [{log.type}]", "log"));
                            }
                        }
                    }

                    if (data.tasks != null)
                    {
                        foreach (var task in data.tasks.Take(8))
                        {
                            if (DateTime.TryParse(task.createTime, out var time))
                            {
                                var icon = task.status == TaskStatusV2.已完成 ? "✅" : "📌";
                                activities.Add((time, $"{icon} {task.taskName} - {task.assignedTo} [{task.priority}]", "task"));
                            }
                        }
                    }

                    if (activities.Count == 0) return "📭 暂无活动记录";

                    var sorted = activities.OrderByDescending(a => a.time).Take(12);
                    return string.Join("\n", sorted.Select(a => $"{a.time:MM-dd HH:mm}  {a.text}"));
                }
            }

            // 快速操作
            [FoldoutGroup("快速操作", Expanded = true)]
            [HorizontalGroup("快速操作/Buttons")]
            [Button("新建日志", ButtonHeight = 40), GUIColor(0.3f, 0.7f, 0.9f)]
            public void QuickCreateLog()
            {
                DevManagementSoundManager.PlayClickSound();
                ESDevManagementWindow_V2.MenuItems[PageName_DevLogCreate]?.Select();
            }

            [HorizontalGroup("快速操作/Buttons")]
            [Button("新建任务", ButtonHeight = 40), GUIColor(0.3f, 0.9f, 0.5f)]
            public void QuickCreateTask()
            {
                DevManagementSoundManager.PlayClickSound();
                ESDevManagementWindow_V2.MenuItems[PageName_TaskCreate]?.Select();
            }

            [HorizontalGroup("快速操作/Buttons")]
            [Button("查看时间线", ButtonHeight = 40), GUIColor(0.7f, 0.5f, 0.9f)]
            public void ViewTimeline()
            {
                DevManagementSoundManager.PlayClickSound();
                ESDevManagementWindow_V2.MenuItems[PageName_Timeline]?.Select();
            }

            [HorizontalGroup("快速操作/Buttons")]
            [Button("刷新", ButtonHeight = 40), GUIColor(0.6f, 0.6f, 0.6f)]
            public void Refresh()
            {
                DevManagementSoundManager.PlayClickSound();
                window?.ForceMenuTreeRebuild();
                window?.Repaint();
            }
        }

        // ==================== 开发日志列表 ====================
        [Serializable]
        public class Page_DevLogList : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [Title("开发日志", titleAlignment: TitleAlignments.Centered, bold: true)]
            [PropertySpace(10)]

            // 筛选栏
            [HorizontalGroup("筛选", Width = 0.3f)]
            [LabelText("类型"), LabelWidth(40)]
            [ValueDropdown("GetTypes")]
            public string filterType = "全部";

            [HorizontalGroup("筛选", Width = 0.3f)]
            [LabelText("标签"), LabelWidth(40)]
            [ValueDropdown("GetTags")]
            public string filterTag = "全部";

            [HorizontalGroup("筛选", Width = 0.4f)]
            [LabelText("搜索"), LabelWidth(40)]
            public string searchText = "";

            [HorizontalGroup("筛选")]
            [Button("筛选", ButtonHeight = 25), GUIColor(0.4f, 0.7f, 0.9f)]
            public void ApplyFilter()
            {
                RefreshList();
            }

            private IEnumerable<string> GetTypes()
            {
                var types = new List<string> { "全部" };
                types.AddRange(Enum.GetNames(typeof(DevLogType)));
                return types;
            }

            private IEnumerable<string> GetTags()
            {
                var tags = new List<string> { "全部" };
                if (data?.allTags != null) tags.AddRange(data.allTags);
                return tags;
            }

            [PropertySpace(5)]
            [ListDrawerSettings(ShowIndexLabels = false, IsReadOnly = true,
                       NumberOfItemsPerPage = 10, ShowPaging = true, ShowFoldout = false)]
            [HideLabel]
            [OnCollectionChanged("OnLogSelected")]
            public List<DevLogCardView> displayLogs = new List<DevLogCardView>();

            public void RefreshList()
            {
                if (data?.devLogs == null)
                {
                    displayLogs = new List<DevLogCardView>();
                    return;
                }

                var filtered = data.devLogs.AsEnumerable();

                // 类型筛选
                if (filterType != "全部" && Enum.TryParse<DevLogType>(filterType, out var type))
                    filtered = filtered.Where(l => l.type == type);

                // 标签筛选
                if (filterTag != "全部")
                    filtered = filtered.Where(l => l.tags != null && l.tags.Contains(filterTag));

                // 搜索功能增强：支持多字段搜索
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var searchLower = searchText.ToLower();
                    filtered = filtered.Where(l =>
                        l.title.ToLower().Contains(searchLower) ||
                        l.content.ToLower().Contains(searchLower) ||
                        l.createdBy.ToLower().Contains(searchLower) ||
                        (l.changeDescription != null && l.changeDescription.ToLower().Contains(searchLower)));
                }

                // 排序：按优先级和时间
                displayLogs = filtered
                    .OrderBy(l => l.priority)
                    .ThenByDescending(l => l.createTime)
                    .Select(l => new DevLogCardView(l, window))
                    .ToList();
            }

            public override ESWindowPageBase ES_Refresh()
            {
                RefreshList();
                return base.ES_Refresh();
            }

            private void OnLogSelected()
            {
                // 可以添加选中回调
            }

            [PropertySpace(10)]
            [HorizontalGroup("操作")]
            [Button("刷新", ButtonHeight = 35), GUIColor(0.4f, 0.7f, 0.9f)]
            public void Refresh()
            {
                RefreshList();
                window?.Repaint();
            }
        }

        // ==================== 日志卡片视图 ====================
        [Serializable]
        public class DevLogCardView
        {
            [HideInInspector] public DevLogEntryV2 log;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [BoxGroup("日志卡片",showLabel:false), HideLabel]
            [HorizontalGroup("日志卡片/Header", Width = 0.65f)]
            [VerticalGroup("日志卡片/Header/Left")]
            // 使用 DisplayAsString 以标签形式显示，并用高亮颜色突出标题
            [LabelText(""), ShowInInspector, DisplayAsString]
            [GUIColor(0.1f, 0.9f, 1f)]
            public string Title { get { return GetIcon() + " " + log.title; } set { } }

            [VerticalGroup("日志卡片/Header/Left")]
            [LabelText(""), DisplayAsString(false)]
            [GUIColor(0.7f, 0.7f, 0.7f)]
            public string Info => $"{log.type} | {log.createdBy} | {log.createTime.Substring(0, Math.Min(10, log.createTime.Length))}";

            [VerticalGroup("日志卡片/Header/Left")]
            [LabelText("内容摘要"), DisplayAsString(false)]
            [GUIColor(0.8f, 0.8f, 0.8f)]
            public string ContentPreview => GetContentPreview();

            [HorizontalGroup("日志卡片/Header", Width = 0.35f)]
            [VerticalGroup("日志卡片/Header/Right"), ShowInInspector]
            [LabelText("优先级"), DisplayAsString(false)]
            [GUIColor("GetPriorityColor")]
            public string PriorityText => GetPriorityIcon() + log.priority.ToString();

            [VerticalGroup("日志卡片/Header/Right")]
            [LabelText("标签"), DisplayAsString(false)]
            [GUIColor(0.6f, 0.8f, 1f)]
            public string Tags => GetTagsText();

            [VerticalGroup("日志卡片/Header/Right")]
            [LabelText("版本"), DisplayAsString(false)]
            [GUIColor(0.7f, 0.9f, 0.7f)]
            public string Version => $"v{log.version}";

            [BoxGroup("日志卡片")]
            [HorizontalGroup("日志卡片/Actions")]
            [Button("🔍 查看完整内容", ButtonHeight = 30), GUIColor(0.3f, 0.8f, 0.9f)]
            public void ViewDetail()
            {
                DevManagementSoundManager.PlayClickSound();
                window?.SelectLogDetail(log);
            }

            [HorizontalGroup("日志卡片/Actions")]
            [Button("⚙️ 快速编辑", ButtonHeight = 30), GUIColor(0.4f, 0.9f, 0.5f)]
            public void QuickEdit()
            {
                DevManagementSoundManager.PlayClickSound();
                DevLogEditWindow.ShowWindow(log, window.DataAsset, window);
            }

            [HorizontalGroup("日志卡片/Actions")]
            [Button("📋 复制", ButtonHeight = 30), GUIColor(0.6f, 0.7f, 0.9f)]
            public void Duplicate()
            {
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var newLog = new DevLogEntryV2
                {
                    id = Guid.NewGuid().ToString(),
                    title = log.title + " (复制)",
                    content = log.content,
                    type = log.type,
                    priority = log.priority,
                    tags = new List<string>(log.tags ?? new List<string>()),
                    changeDescription = log.changeDescription,
                    linkedTaskIds = new List<string>(log.linkedTaskIds ?? new List<string>()),
                    createTime = now,
                    lastModified = now,
                    createdBy = window.currentUser,
                    lastModifiedBy = window.currentUser,
                    version = 1
                };
                window.DataAsset.devLogs.Add(newLog);
                EditorUtility.SetDirty(window.DataAsset);
                AssetDatabase.SaveAssets();
                window.ForceMenuTreeRebuild();
                DevManagementSoundManager.PlayCreateSound();
                EditorUtility.DisplayDialog("成功", "日志已复制！", "确定");
            }

            private string GetContentPreview()
            {
                if (string.IsNullOrEmpty(log.content)) return "无内容";
                var preview = log.content.Replace("\n", " ").Replace("\r", "");
                return preview.Length > 80 ? preview.Substring(0, 80) + "..." : preview;
            }

            private string GetTagsText()
            {
                if (log.tags == null || log.tags.Count == 0) return "无标签";
                return string.Join(", ", log.tags.Take(3)) + (log.tags.Count > 3 ? "..." : "");
            }

            private string GetIcon()
            {
                return log.type switch
                {
                    DevLogType.功能开发 => "🔧",
                    DevLogType.Bug修复 => "🐞",
                    DevLogType.性能优化 => "⚡",
                    DevLogType.重构 => "🔄",
                    DevLogType.文档更新 => "📝",
                    DevLogType.测试 => "🧪",
                    DevLogType.部署 => "🚀",
                    DevLogType.会议记录 => "💼",
                    _ => "📝"
                };
            }

            private string GetPriorityIcon()
            {
                return log.priority switch
                {
                    Priority.紧急 => "🔴 ",
                    Priority.高 => "🟠 ",
                    Priority.中 => "🟡 ",
                    Priority.低 => "⚪ ",
                    _ => ""
                };
            }

            private Color GetPriorityColor()
            {
                return log.priority switch
                {
                    Priority.紧急 => new Color(0.9f, 0.2f, 0.2f),
                    Priority.高 => new Color(0.9f, 0.6f, 0.2f),
                    Priority.中 => new Color(0.6f, 0.8f, 0.3f),
                    Priority.低 => new Color(0.5f, 0.5f, 0.5f),
                    _ => new Color(0.7f, 0.7f, 0.7f)
                };
            }

            public DevLogCardView(DevLogEntryV2 log, ESDevManagementWindow_V2 window)
            {
                this.log = log;
                this.window = window;
            }
        }

        // ==================== 日志详情页 ====================
        [Serializable]
        public class Page_DevLogDetail : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;
            [HideInInspector] public DevLogEntryV2 selectedLog;

            [ShowIf("HasLog")]
            [Title("@GetTitle", titleAlignment: TitleAlignments.Left, bold: true)]
            [PropertySpace(10)]

            private string GetTitle() => selectedLog != null ? $"📝 {selectedLog.title}" : "未选择日志";
            private bool HasLog() => selectedLog != null;

            [ShowIf("HasLog")]
            [BoxGroup("基本信息")]
            [LabelText("类型"), ReadOnly]
            [ShowInInspector]
            private DevLogType Type => selectedLog?.type ?? DevLogType.功能开发;

            [ShowIf("HasLog")]
            [BoxGroup("基本信息")]
            [LabelText("优先级"), ReadOnly]
            [ShowInInspector]
            private Priority Priority => selectedLog?.priority ?? Priority.中;

            [ShowIf("HasLog")]
            [BoxGroup("基本信息")]
            [LabelText("创建人"), ReadOnly, DisplayAsString]
            private string CreatedBy => selectedLog?.createdBy ?? "";

            [ShowIf("HasLog")]
            [BoxGroup("基本信息")]
            [LabelText("创建时间"), ReadOnly, DisplayAsString]
            private string CreateTime => selectedLog?.createTime ?? "";

            [ShowIf("HasLog")]
            [BoxGroup("内容")]
            [LabelText("正文"), ReadOnly]
            [MultiLineProperty(10)]
            [ShowInInspector]
            private string Content => selectedLog?.content ?? "";

            [ShowIf("HasLog")]
            [BoxGroup("内容")]
            [LabelText("变更描述"), ReadOnly]
            [MultiLineProperty(5)]
            [ShowInInspector]
            private string ChangeDesc => selectedLog?.changeDescription ?? "";

            [ShowIf("HasLog")]
            [BoxGroup("标签")]
            [LabelText("标签列表"), ReadOnly]
            [ShowInInspector]
            private List<string> Tags => selectedLog?.tags ?? new List<string>();

            [ShowIf("HasLog")]
            [BoxGroup("关联")]
            [LabelText("关联任务"), ReadOnly]
            [ShowInInspector]
            private List<string> LinkedTasks => selectedLog?.linkedTaskIds ?? new List<string>();

            [ShowIf("HasLog")]
            [PropertySpace(15)]
            [HorizontalGroup("操作")]
            [Button("⚙️ 编辑", ButtonHeight = 38), GUIColor(0.3f, 0.9f, 0.5f)]
            public void Edit()
            {
                if (selectedLog != null)
                {
                    // 创建编辑窗口
                    var editWindow = DevLogEditWindow.ShowWindow(selectedLog, data, window);
                }
            }

            [HorizontalGroup("操作")]
            [Button("📋 复制", ButtonHeight = 38), GUIColor(0.5f, 0.7f, 0.9f)]
            public void Duplicate()
            {
                if (selectedLog != null && data != null)
                {
                    DevManagementSoundManager.PlayCreateSound();
                    var newLog = new DevLogEntryV2
                    {
                        id = Guid.NewGuid().ToString(),
                        title = selectedLog.title + " (复制)",
                        content = selectedLog.content,
                        type = selectedLog.type,
                        priority = selectedLog.priority,
                        tags = new List<string>(selectedLog.tags),
                        changeDescription = selectedLog.changeDescription,
                        linkedTaskIds = new List<string>(selectedLog.linkedTaskIds),
                        createTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        lastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        createdBy = window.currentUser,
                        lastModifiedBy = window.currentUser,
                        version = 1
                    };
                    data.devLogs.Add(newLog);
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("成功", "日志已复制！", "确定");
                }
            }

            [HorizontalGroup("操作")]
            [Button("🗑️ 删除", ButtonHeight = 38), GUIColor(0.9f, 0.3f, 0.3f)]
            public void Delete()
            {
                if (selectedLog != null && EditorUtility.DisplayDialog("确认", "确定删除此日志?", "删除", "取消"))
                {
                    DevManagementSoundManager.PlayDeleteSound();
                    data.devLogs.Remove(selectedLog);
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    ESDevManagementWindow_V2.MenuItems[PageName_DevLogList]?.Select();
                    EditorUtility.DisplayDialog("成功", "日志已删除", "确定");
                }
            }

            [ShowIf("@!HasLog()")]
            [InfoBox("请从列表中选择一个日志查看详情", InfoMessageType.Info)]
            [Button("返回列表", ButtonHeight = 40), GUIColor(0.4f, 0.7f, 0.9f)]
            public void BackToList()
            {
                ESDevManagementWindow_V2.MenuItems[PageName_DevLogList]?.Select();
            }
        }

        // ==================== 新建日志 ====================
        [Serializable]
        public class Page_CreateDevLog : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;
            [HideInInspector] public string currentUser;

            [Title("新建开发日志", titleAlignment: TitleAlignments.Centered, bold: true)]
            [PropertySpace(10)]

            [BoxGroup("基本信息")]
            [LabelText("标题"), Required]
            public string title = "";

            [BoxGroup("基本信息")]
            [HorizontalGroup("基本信息/Row1")]
            [LabelText("类型"), LabelWidth(60)]
            [ValueDropdown("GetTypes")]
            public DevLogType type = DevLogType.功能开发;

            [HorizontalGroup("基本信息/Row1")]
            [LabelText("优先级"), LabelWidth(60)]
            [ValueDropdown("GetPriorities")]
            public Priority priority = Priority.中;

            [BoxGroup("内容")]
            [LabelText("正文"), TextArea(10, 20), Required]
            public string content = "";

            [BoxGroup("内容")]
            [LabelText("变更描述"), TextArea(4, 10)]
            public string changeDescription = "";

            [BoxGroup("分类")]
            [LabelText("标签")]
            [ValueDropdown("GetAllTags")]
            public List<string> tags = new List<string>();

            [BoxGroup("关联")]
            [LabelText("关联任务ID (可选)")]
            public List<string> linkedTaskIds = new List<string>();

            private IEnumerable<DevLogType> GetTypes() => Enum.GetValues(typeof(DevLogType)).Cast<DevLogType>();
            private IEnumerable<Priority> GetPriorities() => Enum.GetValues(typeof(Priority)).Cast<Priority>();
            private IEnumerable<string> GetAllTags() => data?.allTags ?? new List<string>();

            [PropertySpace(15)]
            [HorizontalGroup("操作")]
            [Button("创建", ButtonHeight = 45), GUIColor(0.2f, 0.9f, 0.4f)]
            public void Create()
            {
                if (string.IsNullOrWhiteSpace(title))
                {
                    EditorUtility.DisplayDialog("错误", "标题不能为空", "确定");
                    return;
                }

                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var log = new DevLogEntryV2
                {
                    id = Guid.NewGuid().ToString(),
                    title = title,
                    content = content,
                    type = type,
                    priority = priority,
                    tags = new List<string>(tags),
                    changeDescription = changeDescription,
                    linkedTaskIds = new List<string>(linkedTaskIds),
                    createTime = now,
                    lastModified = now,
                    createdBy = currentUser,
                    lastModifiedBy = currentUser,
                    version = 1
                };

                data.devLogs.Add(log);

                // 更新标签库
                foreach (var tag in tags)
                {
                    if (!data.allTags.Contains(tag))
                        data.allTags.Add(tag);
                }

                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();

                DevManagementSoundManager.PlayCreateSound();
                EditorUtility.DisplayDialog("成功", "日志创建成功！", "确定");
                ClearForm();
            }

            [HorizontalGroup("操作")]
            [Button("清空", ButtonHeight = 45), GUIColor(0.7f, 0.7f, 0.7f)]
            public void ClearForm()
            {
                title = "";
                content = "";
                changeDescription = "";
                tags.Clear();
                linkedTaskIds.Clear();
                type = DevLogType.功能开发;
                priority = Priority.中;
            }
        }

        // ==================== 任务看板视图 ====================
        [Serializable]
        public class Page_TaskBoard : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [Title("任务看板", "Kanban风格", titleAlignment: TitleAlignments.Centered, bold: true)]
            [PropertySpace(10)]

            [FoldoutGroup("开始", Expanded = true)]
            [TableList(ShowIndexLabels = false, AlwaysExpanded = true, IsReadOnly = true)]
            [HideLabel]
            public List<TaskCardView> todoTasks = new List<TaskCardView>();

            [FoldoutGroup("进行中", Expanded = true)]
            [TableList(ShowIndexLabels = false, AlwaysExpanded = true, IsReadOnly = true)]
            [HideLabel]
            public List<TaskCardView> inProgressTasks = new List<TaskCardView>();

            [FoldoutGroup("评估中", Expanded = true)]
            [TableList(ShowIndexLabels = false, AlwaysExpanded = true, IsReadOnly = true)]
            [HideLabel]
            public List<TaskCardView> reviewTasks = new List<TaskCardView>();

            [FoldoutGroup("已完成", Expanded = false)]
            [TableList(ShowIndexLabels = false, AlwaysExpanded = true, IsReadOnly = true)]
            [HideLabel]
            public List<TaskCardView> doneTasks = new List<TaskCardView>();

            public void RefreshBoard()
            {
                if (data?.tasks == null) return;

                todoTasks = data.tasks.Where(t => t.status == TaskStatusV2.开始)
                    .Select(t => new TaskCardView(t, window)).ToList();

                inProgressTasks = data.tasks.Where(t => t.status == TaskStatusV2.进行中)
                    .Select(t => new TaskCardView(t, window)).ToList();

                reviewTasks = data.tasks.Where(t => t.status == TaskStatusV2.评估中)
                    .Select(t => new TaskCardView(t, window)).ToList();

                doneTasks = data.tasks.Where(t => t.status == TaskStatusV2.已完成)
                    .Select(t => new TaskCardView(t, window)).ToList();
            }

            public override ESWindowPageBase ES_Refresh()
            {
                RefreshBoard();
                return base.ES_Refresh();
            }

            [PropertySpace(10)]
            [Button("刷新看板", ButtonHeight = 35), GUIColor(0.4f, 0.7f, 0.9f)]
            public void Refresh()
            {
                RefreshBoard();
                window?.Repaint();
            }
        }

        // ==================== 任务卡片视图 ====================
        [Serializable]
        public class TaskCardView
        {
            [HideInInspector] public TaskEntryV2 task;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [FoldoutGroup("任务", Expanded = true)]
            [HorizontalGroup("任务/Header", Width = 0.6f)]
            [VerticalGroup("任务/Header/Left")]
            [LabelText(""), DisplayAsString(false)]
            [GUIColor(0.9f, 1f, 0.95f)]
            public string TaskName => GetStatusIcon() + " " + task.taskName;

            [VerticalGroup("任务/Header/Left")]
            [LabelText(""), DisplayAsString(false)]
            [GUIColor(0.7f, 0.7f, 0.7f)]
            public string Info => $"{task.assignedTo} | {task.startDate} ~ {task.dueDate}";

            [VerticalGroup("任务/Header/Left")]
            [LabelText("描述"), DisplayAsString(false)]
            [GUIColor(0.8f, 0.8f, 0.8f)]
            public string Description => GetDescriptionPreview();

            [HorizontalGroup("任务/Header", Width = 0.4f)]
            [VerticalGroup("任务/Header/Right")]
            [LabelText("状态"), DisplayAsString(false)]
            [GUIColor("GetStatusColor")]
            public string Status => task.status.ToString();

            [VerticalGroup("任务/Header/Right")]
            [LabelText("优先级"), DisplayAsString(false)]
            [GUIColor("GetPriorityColor")]
            public Priority Priority => task.priority;

            [VerticalGroup("任务/Header/Right")]
            [ProgressBar(0, 100, ColorGetter = "GetProgressColor")]
            [LabelText("进度")]
            public int Progress => task.GetOverallProgress();

            [FoldoutGroup("任务", Expanded = true)]
            [HorizontalGroup("任务/Actions")]
            [Button("🔍 查看详情", ButtonHeight = 30), GUIColor(0.3f, 0.8f, 0.9f)]
            public void ViewDetail()
            {
                window?.SelectTaskDetail(task);
            }

            [HorizontalGroup("任务/Actions")]
            [Button("✅ 快速完成", ButtonHeight = 30), GUIColor(0.3f, 0.9f, 0.4f)]
            public void QuickComplete()
            {
                task.status = TaskStatusV2.已完成;
                foreach (var item in task.checklist)
                {
                    if (!item.isCompleted)
                    {
                        item.isCompleted = true;
                        item.completedTime = DateTime.Now.ToString("MM-dd HH:mm");
                    }
                }
                EditorUtility.SetDirty(window.DataAsset);
                AssetDatabase.SaveAssets();
                window.ForceMenuTreeRebuild();
                EditorUtility.DisplayDialog("成功", "任务已标记为完成！", "确定");
            }

            [HorizontalGroup("任务/Actions")]
            [Button("📋 复制", ButtonHeight = 30), GUIColor(0.6f, 0.7f, 0.9f)]
            public void Duplicate()
            {
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var newTask = new TaskEntryV2
                {
                    id = Guid.NewGuid().ToString(),
                    taskName = task.taskName + " (复制)",
                    description = task.description,
                    startDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    dueDate = DateTime.Now.AddDays(7).ToString("yyyy-MM-dd"),
                    assignedTo = task.assignedTo,
                    status = TaskStatusV2.开始,
                    priority = task.priority,
                    checklist = task.checklist.Select(c => new ChecklistItem(c.content, c.assignedTo)).ToList(),
                    tags = new List<string>(task.tags ?? new List<string>()),
                    linkedLogIds = new List<string>(),
                    createTime = now,
                    lastModified = now,
                    createdBy = window.currentUser,
                    lastModifiedBy = window.currentUser,
                    version = 1
                };
                window.DataAsset.tasks.Add(newTask);
                EditorUtility.SetDirty(window.DataAsset);
                AssetDatabase.SaveAssets();
                window.ForceMenuTreeRebuild();
                EditorUtility.DisplayDialog("成功", "任务已复制！", "确定");
            }

            private string GetDescriptionPreview()
            {
                if (string.IsNullOrEmpty(task.description)) return "无描述";
                var preview = task.description.Replace("\n", " ").Replace("\r", "");
                return preview.Length > 60 ? preview.Substring(0, 60) + "..." : preview;
            }

            private string GetStatusIcon()
            {
                return task.status switch
                {
                    TaskStatusV2.已完成 => "✅",
                    TaskStatusV2.进行中 => "🔄",
                    TaskStatusV2.评估中 => "🔍",
                    TaskStatusV2.开始 => "🎯",
                    TaskStatusV2.暂停 => "⏸️",
                    TaskStatusV2.已取消 => "❌",
                    _ => "📌"
                };
            }

            private string GetPriorityIcon()
            {
                return task.priority switch
                {
                    Priority.紧急 => "🔴 ",
                    Priority.高 => "🟠 ",
                    Priority.中 => "🟡 ",
                    Priority.低 => "⚪ ",
                    _ => ""
                };
            }

            private Color GetStatusColor()
            {
                return task.status switch
                {
                    TaskStatusV2.已完成 => new Color(0.3f, 0.9f, 0.4f),
                    TaskStatusV2.进行中 => new Color(0.4f, 0.7f, 0.9f),
                    TaskStatusV2.评估中 => new Color(0.9f, 0.7f, 0.3f),
                    TaskStatusV2.暂停 => new Color(0.6f, 0.6f, 0.6f),
                    TaskStatusV2.已取消 => new Color(0.9f, 0.4f, 0.4f),
                    _ => new Color(0.7f, 0.9f, 0.5f)
                };
            }

            private Color GetPriorityColor()
            {
                return task.priority switch
                {
                    Priority.紧急 => new Color(0.9f, 0.2f, 0.2f),
                    Priority.高 => new Color(0.9f, 0.6f, 0.2f),
                    Priority.中 => new Color(0.6f, 0.8f, 0.3f),
                    Priority.低 => new Color(0.5f, 0.5f, 0.5f),
                    _ => new Color(0.7f, 0.7f, 0.7f)
                };
            }

            private Color GetProgressColor(int value)
            {
                if (value >= 80) return new Color(0.2f, 0.9f, 0.3f);
                if (value >= 50) return new Color(0.9f, 0.7f, 0.2f);
                return new Color(0.9f, 0.3f, 0.3f);
            }

            public TaskCardView(TaskEntryV2 task, ESDevManagementWindow_V2 window)
            {
                this.task = task;
                this.window = window;
            }
        }

        // ==================== 设置页面 ====================
        [Serializable]
        public class Page_Settings : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;
            [HideInInspector] public string currentUser;

            [Title("系统设置", titleAlignment: TitleAlignments.Centered, bold: true)]
            [PropertySpace(10)]

            [FoldoutGroup("用户设置", Expanded = true)]
            [LabelText("当前用户")]
            [ShowInInspector, DisplayAsString]
            [GUIColor(0.5f, 0.8f, 1f)]
            private string CurrentUser => $"👤 {currentUser}";

            [FoldoutGroup("用户设置", Expanded = true)]
            [LabelText("更改用户名")]
            public string newUserName = "";

            [FoldoutGroup("用户设置", Expanded = true)]
            [Button("💾 保存用户名", ButtonHeight = 32), GUIColor(0.3f, 0.8f, 0.5f)]
            public void SaveUserName()
            {
                if (!string.IsNullOrWhiteSpace(newUserName))
                {
                    currentUser = newUserName;
                    EditorPrefs.SetString(CurrentUserKey, currentUser);
                    DevManagementSoundManager.PlaySaveSound();
                    EditorUtility.DisplayDialog("成功", $"当前用户已更新为: {currentUser}", "确定");
                    newUserName = "";
                }
            }

            [FoldoutGroup("界面设置", Expanded = true)]
            [LabelText("🔊 启用音效")]
            [ToggleLeft]
            [OnValueChanged("OnSoundEnabledChanged")]
            public bool soundEnabled = true;

            [FoldoutGroup("界面设置", Expanded = true)]
            [Button("🔊 测试音效", ButtonHeight = 32), GUIColor(0.4f, 0.7f, 0.9f)]
            public void TestSound()
            {
                DevManagementSoundManager.PlayClickSound();
            }

            private void OnSoundEnabledChanged()
            {
                DevManagementSoundManager.SetSoundEnabled(soundEnabled);
            }

            public override ESWindowPageBase ES_Refresh()
            {
                soundEnabled = DevManagementSoundManager.IsSoundEnabled();
                return base.ES_Refresh();
            }

            [FoldoutGroup("数据信息", Expanded = true)]
            [ShowInInspector, ReadOnly, LabelText("数据路径"), DisplayAsString]
            private string DataPath => AssetDatabase.GetAssetPath(data);

            [FoldoutGroup("数据信息", Expanded = true)]
            [ShowInInspector, ReadOnly, LabelText("最后修改"), DisplayAsString]
            [GUIColor(0.7f, 0.9f, 0.7f)]
            private string LastModified => $"👤 {data?.lastModifiedBy} ⏰ {data?.lastModifiedTime}";

            [FoldoutGroup("数据信息", Expanded = true)]
            [ShowInInspector, ReadOnly, LabelText("数据统计"), DisplayAsString]
            [MultiLineProperty(4)]
            private string DataStats
            {
                get
                {
                    if (data == null) return "无数据";
                    return $"📝 日志总数: {data.devLogs?.Count ?? 0}\n" +
                           $"✓ 任务总数: {data.tasks?.Count ?? 0}\n" +
                           $"🏷️ 标签总数: {data.allTags?.Count ?? 0}\n" +
                           $"📊 数据版本: v{data.dataVersion}";
                }
            }

            [FoldoutGroup("数据操作", Expanded = true)]
            [HorizontalGroup("数据操作/Buttons")]
            [Button("💾 保存数据", ButtonHeight = 42), GUIColor(0.3f, 0.9f, 0.3f)]
            public void SaveData()
            {
                window?.ES_SaveData();
                DevManagementSoundManager.PlaySaveSound();
                EditorUtility.DisplayDialog("成功", "数据已保存！", "确定");
            }

            [HorizontalGroup("数据操作/Buttons")]
            [Button("🔄 重新加载", ButtonHeight = 42), GUIColor(0.5f, 0.7f, 0.9f)]
            public void ReloadData()
            {
                window?.ES_LoadData();
                window?.ForceMenuTreeRebuild();
                window?.Repaint();
                EditorUtility.DisplayDialog("成功", "数据已重新加载！", "确定");
            }

            [HorizontalGroup("数据操作/Buttons")]
            [Button("📌 定位文件", ButtonHeight = 42), GUIColor(0.9f, 0.7f, 0.3f)]
            public void PingAsset()
            {
                if (data != null)
                {
                    Selection.activeObject = data;
                    EditorGUIUtility.PingObject(data);
                }
            }

            [FoldoutGroup("导出功能", Expanded = true)]
            [InfoBox("导出数据为可读格式，便于分享和归档", InfoMessageType.Info)]
            [HorizontalGroup("导出功能/Buttons")]
            [Button("📄 导出为Markdown", ButtonHeight = 40), GUIColor(0.4f, 0.8f, 0.9f)]
            public void ExportToMarkdown()
            {
                if (data == null) return;

                var path = EditorUtility.SaveFilePanel("导出为Markdown", "", "DevReport.md", "md");
                if (string.IsNullOrEmpty(path)) return;

                var md = new System.Text.StringBuilder();
                md.AppendLine("# 开发管理报告");
                md.AppendLine($"\n生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                md.AppendLine($"\n生成人: {currentUser}\n");

                md.AppendLine("## 📊 数据统计");
                md.AppendLine($"- 日志总数: {data.devLogs?.Count ?? 0}");
                md.AppendLine($"- 任务总数: {data.tasks?.Count ?? 0}");
                md.AppendLine($"- 已完成任务: {data.tasks?.Count(t => t.status == TaskStatusV2.已完成) ?? 0}");
                md.AppendLine($"- 标签总数: {data.allTags?.Count ?? 0}\n");

                md.AppendLine("## 📝 开发日志");
                if (data.devLogs != null)
                {
                    foreach (var log in data.devLogs.OrderByDescending(l => l.createTime))
                    {
                        md.AppendLine($"\n### {log.title}");
                        md.AppendLine($"- **类型**: {log.type}");
                        md.AppendLine($"- **优先级**: {log.priority}");
                        md.AppendLine($"- **创建人**: {log.createdBy}");
                        md.AppendLine($"- **创建时间**: {log.createTime}");
                        if (log.tags != null && log.tags.Count > 0)
                            md.AppendLine($"- **标签**: {string.Join(", ", log.tags)}");
                        md.AppendLine($"\n{log.content}\n");
                    }
                }

                md.AppendLine("\n## ✅ 任务列表");
                if (data.tasks != null)
                {
                    foreach (var task in data.tasks.OrderBy(t => t.priority))
                    {
                        md.AppendLine($"\n### {task.taskName}");
                        md.AppendLine($"- **状态**: {task.status}");
                        md.AppendLine($"- **优先级**: {task.priority}");
                        md.AppendLine($"- **负责人**: {task.assignedTo}");
                        md.AppendLine($"- **开始日期**: {task.startDate}");
                        md.AppendLine($"- **截止日期**: {task.dueDate}");
                        md.AppendLine($"- **进度**: {task.GetOverallProgress()}%");
                        md.AppendLine($"\n{task.description}\n");

                        if (task.checklist != null && task.checklist.Count > 0)
                        {
                            md.AppendLine("**子任务清单**:");
                            foreach (var item in task.checklist)
                            {
                                var check = item.isCompleted ? "x" : " ";
                                md.AppendLine($"- [{check}] {item.content} ({item.assignedTo})");
                            }
                        }
                    }
                }

                System.IO.File.WriteAllText(path, md.ToString());
                EditorUtility.DisplayDialog("成功", $"报告已导出到\n{path}", "确定");
            }

            [HorizontalGroup("导出功能/Buttons")]
            [Button("📊 导出统计JSON", ButtonHeight = 40), GUIColor(0.7f, 0.8f, 0.5f)]
            public void ExportStatsJSON()
            {
                if (data == null) return;

                var path = EditorUtility.SaveFilePanel("导出统计", "", "DevStats.json", "json");
                if (string.IsNullOrEmpty(path)) return;

                var stats = new
                {
                    generatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    generatedBy = currentUser,
                    totalLogs = data.devLogs?.Count ?? 0,
                    totalTasks = data.tasks?.Count ?? 0,
                    completedTasks = data.tasks?.Count(t => t.status == TaskStatusV2.已完成) ?? 0,
                    totalTags = data.allTags?.Count ?? 0,
                    logsByType = data.devLogs?.GroupBy(l => l.type).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                    tasksByStatus = data.tasks?.GroupBy(t => t.status).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                    tasksByPriority = data.tasks?.GroupBy(t => t.priority).ToDictionary(g => g.Key.ToString(), g => g.Count())
                };

                var json = JsonUtility.ToJson(stats, true);
                System.IO.File.WriteAllText(path, json);
                EditorUtility.DisplayDialog("成功", $"统计数据已导出到\n{path}", "确定");
            }

            [FoldoutGroup("危险操作", Expanded = false)]
            [InfoBox("以下操作不可恢复，请谨慎操作！", InfoMessageType.Warning)]
            [HorizontalGroup("危险操作/Buttons")]
            [Button("🗑️ 清空所有日志", ButtonHeight = 40), GUIColor(0.9f, 0.5f, 0.2f)]
            public void ClearAllLogs()
            {
                if (EditorUtility.DisplayDialog("警告", "确定要清空所有日志吗？此操作不可恢复！", "清空", "取消"))
                {
                    data.devLogs.Clear();
                    SaveData();
                    window?.ForceMenuTreeRebuild();
                    EditorUtility.DisplayDialog("完成", "所有日志已清空！", "确定");
                }
            }

            [HorizontalGroup("危险操作/Buttons")]
            [Button("🗑️ 清空所有任务", ButtonHeight = 40), GUIColor(0.9f, 0.5f, 0.2f)]
            public void ClearAllTasks()
            {
                if (EditorUtility.DisplayDialog("警告", "确定要清空所有任务吗？此操作不可恢复！", "清空", "取消"))
                {
                    data.tasks.Clear();
                    SaveData();
                    window?.ForceMenuTreeRebuild();
                    EditorUtility.DisplayDialog("完成", "所有任务已清空！", "确定");
                }
            }

            [HorizontalGroup("危险操作/Buttons")]
            [Button("⚠️ 清空所有数据", ButtonHeight = 40), GUIColor(0.9f, 0.2f, 0.2f)]
            public void ClearAllData()
            {
                if (EditorUtility.DisplayDialog("严重警告", "确定要清空所有数据吗？此操作不可恢复！\n\n包括：所有日志、所有任务、所有标签", "确认清空", "取消"))
                {
                    data.devLogs.Clear();
                    data.tasks.Clear();
                    data.allTags.Clear();
                    SaveData();
                    window?.ForceMenuTreeRebuild();
                    EditorUtility.DisplayDialog("完成", "所有数据已清空！", "确定");
                }
            }
        }

        // ==================== 日志编辑窗口 ====================
        public class DevLogEditWindow : EditorWindow
        {
            private DevLogEntryV2 log;
            private DevManagementDataV2 data;
            private ESDevManagementWindow_V2 mainWindow;
            private Vector2 scrollPos;

            public static DevLogEditWindow ShowWindow(DevLogEntryV2 log, DevManagementDataV2 data, ESDevManagementWindow_V2 mainWindow)
            {
                var window = GetWindow<DevLogEditWindow>("编辑日志");
                window.minSize = new Vector2(600, 700);
                window.log = log;
                window.data = data;
                window.mainWindow = mainWindow;
                window.Show();
                return window;
            }

            private void OnGUI()
            {
                if (log == null)
                {
                    EditorGUILayout.HelpBox("日志数据丢失", MessageType.Error);
                    return;
                }

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                GUILayout.Space(10);

                // 标题
                EditorGUILayout.LabelField("编辑日志", EditorStyles.boldLabel);
                GUILayout.Space(5);

                // 标题
                log.title = EditorGUILayout.TextField("标题", log.title);

                GUILayout.Space(10);

                // 类型和优先级
                EditorGUILayout.BeginHorizontal();
                log.type = (DevLogType)EditorGUILayout.EnumPopup("类型", log.type);
                log.priority = (Priority)EditorGUILayout.EnumPopup("优先级", log.priority);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);

                // 内容
                EditorGUILayout.LabelField("内容");
                log.content = EditorGUILayout.TextArea(log.content, GUILayout.Height(200));

                GUILayout.Space(10);

                // 变更描述
                EditorGUILayout.LabelField("变更描述");
                log.changeDescription = EditorGUILayout.TextArea(log.changeDescription ?? "", GUILayout.Height(100));

                GUILayout.Space(10);

                // 标签
                EditorGUILayout.LabelField("标签");
                if (log.tags == null) log.tags = new List<string>();

                for (int i = 0; i < log.tags.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    log.tags[i] = EditorGUILayout.TextField(log.tags[i]);
                    if (GUILayout.Button("×", GUILayout.Width(30)))
                    {
                        log.tags.RemoveAt(i);
                        i--;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("+ 添加标签"))
                {
                    log.tags.Add("");
                }

                GUILayout.Space(20);

                // 操作按钮
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("💾 保存", GUILayout.Height(40)))
                {
                    SaveLog();
                }

                GUI.backgroundColor = Color.gray;
                if (GUILayout.Button("取消", GUILayout.Height(40)))
                {
                    Close();
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);

                EditorGUILayout.EndScrollView();
            }

            private void SaveLog()
            {
                log.lastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                log.lastModifiedBy = mainWindow.currentUser;
                log.version++;

                // 更新标签库
                foreach (var tag in log.tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag) && !data.allTags.Contains(tag))
                        data.allTags.Add(tag);
                }

                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();

                DevManagementSoundManager.PlaySaveSound();
                EditorUtility.DisplayDialog("成功", "日志已保存！", "确定");
                mainWindow?.ForceMenuTreeRebuild();
                mainWindow?.Repaint();
                Close();
            }
        }

        // 继续下一部分...
        #endregion
    }
}
