using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;

namespace ES
{
    /// <summary>
    /// ES开发管理窗口 V2.0 - Notion风格
    /// 功能完善的团队协作开发管理系统
    /// </summary>
    public partial class ESDevManagementWindow_V2 : ESMenuTreeWindowAB<ESDevManagementWindow_V2>
    {
        [MenuItem("Tools/ES工具/ES开发管理 V2")]
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
        private const string DataSavePath = "Assets/ES/DevManagement/DevManagementDataV2.asset";
        private const string EditorPrefKey = "ESDevManagement_V2_DataGUID";
        private const string CurrentUserKey = "ESDevManagement_V2_CurrentUser";
        
        // 页面名称
        private const string PageName_Dashboard = "仪表板";
        private const string PageName_DevLogList = "开发日志/列表视图";
        private const string PageName_DevLogCreate = "开发日志/新建";
        private const string PageName_DevLogDetail = "开发日志/详情";
        private const string PageName_TaskBoard = "任务看板/看板视图";
        private const string PageName_TaskList = "任务看板/列表视图";
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
        [NonSerialized] private Page_CreateTask pageCreateTask;
        [NonSerialized] private Page_TaskDetail pageTaskDetail;
        [NonSerialized] private Page_Timeline pageTimeline;
        [NonSerialized] private Page_Tags pageTags;
        [NonSerialized] private Page_Settings pageSettings;
        #endregion

        #region 数据
        private DevManagementDataV2 dataAsset;
        private string currentUser = Environment.UserName;
        #endregion

        protected override void OnImGUI()
        {
            if (UsingWindow == null)
            {
                UsingWindow = this;
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
            MenuItems[PageName_DevLogCreate] = tree.Add(PageName_DevLogCreate, pageCreateDevLog, SdfIconType.JournalPlus).First();
            
            pageDevLogDetail = new Page_DevLogDetail { data = dataAsset, window = this };
            MenuItems[PageName_DevLogDetail] = tree.Add(PageName_DevLogDetail, pageDevLogDetail, SdfIconType.FileText).First();

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
            MenuItems[PageName_TaskCreate] = tree.Add(PageName_TaskCreate, pageCreateTask, SdfIconType.PlusSquareFill).First();
            
            pageTaskDetail = new Page_TaskDetail { data = dataAsset, window = this };
            MenuItems[PageName_TaskDetail] = tree.Add(PageName_TaskDetail, pageTaskDetail, SdfIconType.CardText).First();

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

        // 选择日志详情
        public void SelectLogDetail(DevLogEntryV2 log)
        {
            if (pageDevLogDetail != null)
            {
                pageDevLogDetail.selectedLog = log;
                MenuItems[PageName_DevLogDetail]?.Select();
            }
        }

        // 选择任务详情
        public void SelectTaskDetail(TaskEntryV2 task)
        {
            if (pageTaskDetail != null)
            {
                pageTaskDetail.selectedTask = task;
                MenuItems[PageName_TaskDetail]?.Select();
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
            private string LogStats => $"总计: {data?.devLogs?.Count ?? 0}\n本周: {GetThisWeekLogs()}";

            [HorizontalGroup("数据概览/Row1", Width = 0.25f)]
            [BoxGroup("数据概览/Row1/任务"), HideLabel]
            [ShowInInspector, DisplayAsString(false)]
            private string TaskStats => $"总计: {data?.tasks?.Count ?? 0}\n活跃: {GetActiveTasks()}";

            [HorizontalGroup("数据概览/Row1", Width = 0.25f)]
            [BoxGroup("数据概览/Row1/完成率"), HideLabel]
            [ProgressBar(0, 100, ColorGetter = "GetProgressColor")]
            [ShowInInspector]
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
            [BoxGroup("数据概览/Row1/标签"), HideLabel]
            [ShowInInspector, DisplayAsString(false)]
            private string TagStats => $"标签: {data?.allTags?.Count ?? 0}";

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
            [MultiLineProperty(10)]
            private string RecentTimeline
            {
                get
                {
                    if (data == null) return "暂无活动";
                    
                    var activities = new List<(DateTime time, string text)>();
                    
                    foreach (var log in data.devLogs.Take(5))
                    {
                        if (DateTime.TryParse(log.createTime, out var time))
                            activities.Add((time, $"📝 {log.title} - {log.createdBy}"));
                    }
                    
                    foreach (var task in data.tasks.Take(5))
                    {
                        if (DateTime.TryParse(task.createTime, out var time))
                            activities.Add((time, $"✅ {task.taskName} - {task.assignedTo}"));
                    }
                    
                    return string.Join("\n", activities.OrderByDescending(a => a.time).Take(10).Select(a => $"{a.time:MM-dd HH:mm} {a.text}"));
                }
            }

            // 快速操作
            [FoldoutGroup("快速操作", Expanded = true)]
            [HorizontalGroup("快速操作/Buttons")]
            [Button("新建日志", ButtonHeight = 40), GUIColor(0.3f, 0.7f, 0.9f)]
            public void QuickCreateLog()
            {
                ESDevManagementWindow_V2.MenuItems[PageName_DevLogCreate]?.Select();
            }

            [HorizontalGroup("快速操作/Buttons")]
            [Button("新建任务", ButtonHeight = 40), GUIColor(0.3f, 0.9f, 0.5f)]
            public void QuickCreateTask()
            {
                ESDevManagementWindow_V2.MenuItems[PageName_TaskCreate]?.Select();
            }

            [HorizontalGroup("快速操作/Buttons")]
            [Button("查看时间线", ButtonHeight = 40), GUIColor(0.7f, 0.5f, 0.9f)]
            public void ViewTimeline()
            {
                ESDevManagementWindow_V2.MenuItems[PageName_Timeline]?.Select();
            }

            [HorizontalGroup("快速操作/Buttons")]
            [Button("刷新", ButtonHeight = 40), GUIColor(0.6f, 0.6f, 0.6f)]
            public void Refresh()
            {
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
            [TableList(ShowIndexLabels = false, AlwaysExpanded = false, IsReadOnly = true,
                       NumberOfItemsPerPage = 15, ShowPaging = true)]
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

                if (filterType != "全部" && Enum.TryParse<DevLogType>(filterType, out var type))
                    filtered = filtered.Where(l => l.type == type);

                if (filterTag != "全部")
                    filtered = filtered.Where(l => l.tags != null && l.tags.Contains(filterTag));

                if (!string.IsNullOrWhiteSpace(searchText))
                    filtered = filtered.Where(l => l.title.Contains(searchText) || l.content.Contains(searchText));

                displayLogs = filtered.OrderByDescending(l => l.createTime)
                    .Select(l => new DevLogCardView(l, window)).ToList();
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

            [HorizontalGroup("Main", Width = 0.6f)]
            [VerticalGroup("Main/Info")]
            [LabelText("标题"), DisplayAsString, GUIColor(0.8f, 0.9f, 1f)]
            public string Title => $"📝 {log.title}";

            [VerticalGroup("Main/Info")]
            [LabelText("信息"), DisplayAsString]
            [GUIColor(0.7f, 0.7f, 0.7f)]
            public string Info => $"{log.type} | {log.createdBy} | {log.createTime}";

            [HorizontalGroup("Main", Width = 0.2f)]
            [LabelText("优先级"), DisplayAsString]
            [GUIColor("GetPriorityColor")]
            public string PriorityText => log.priority.ToString();

            [HorizontalGroup("Main", Width = 0.2f)]
            [Button("查看详情", ButtonHeight = 25), GUIColor(0.3f, 0.8f, 0.9f)]
            public void ViewDetail()
            {
                window?.SelectLogDetail(log);
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
            [Button("编辑", ButtonHeight = 35), GUIColor(0.3f, 0.9f, 0.5f)]
            public void Edit()
            {
                // TODO: 打开编辑界面
                EditorUtility.DisplayDialog("提示", "编辑功能开发中...", "确定");
            }

            [HorizontalGroup("操作")]
            [Button("删除", ButtonHeight = 35), GUIColor(0.9f, 0.3f, 0.3f)]
            public void Delete()
            {
                if (selectedLog != null && EditorUtility.DisplayDialog("确认", "确定删除此日志?", "删除", "取消"))
                {
                    data.devLogs.Remove(selectedLog);
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    ESDevManagementWindow_V2.MenuItems[PageName_DevLogList]?.Select();
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

            [HorizontalGroup("Main", Width = 0.5f)]
            [LabelText("任务"), DisplayAsString, GUIColor(0.8f, 0.9f, 1f)]
            public string TaskName => $"✓ {task.taskName}";

            [HorizontalGroup("Main", Width = 0.15f)]
            [LabelText("负责人"), DisplayAsString]
            public string Assignee => task.assignedTo;

            [HorizontalGroup("Main", Width = 0.15f)]
            [ProgressBar(0, 100, ColorGetter = "GetProgressColor")]
            [LabelText("进度")]
            public int Progress => task.GetOverallProgress();

            [HorizontalGroup("Main", Width = 0.2f)]
            [Button("详情", ButtonHeight = 25), GUIColor(0.3f, 0.8f, 0.9f)]
            public void ViewDetail()
            {
                window?.SelectTaskDetail(task);
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
            private string CurrentUser => currentUser;

            [FoldoutGroup("用户设置", Expanded = true)]
            [LabelText("更改用户名")]
            public string newUserName = "";

            [FoldoutGroup("用户设置", Expanded = true)]
            [Button("保存用户名", ButtonHeight = 30), GUIColor(0.3f, 0.8f, 0.5f)]
            public void SaveUserName()
            {
                if (!string.IsNullOrWhiteSpace(newUserName))
                {
                    currentUser = newUserName;
                    EditorPrefs.SetString(CurrentUserKey, currentUser);
                    EditorUtility.DisplayDialog("成功", $"当前用户已更新为: {currentUser}", "确定");
                }
            }

            [FoldoutGroup("数据信息", Expanded = true)]
            [ShowInInspector, ReadOnly, LabelText("数据路径")]
            private string DataPath => AssetDatabase.GetAssetPath(data);

            [FoldoutGroup("数据信息", Expanded = true)]
            [ShowInInspector, ReadOnly, LabelText("最后修改")]
            private string LastModified => $"{data?.lastModifiedBy} @ {data?.lastModifiedTime}";

            [FoldoutGroup("数据操作", Expanded = true)]
            [HorizontalGroup("数据操作/Buttons")]
            [Button("保存数据", ButtonHeight = 40), GUIColor(0.3f, 0.9f, 0.3f)]
            public void SaveData()
            {
                window?.ES_SaveData();
                EditorUtility.DisplayDialog("成功", "数据已保存！", "确定");
            }

            [HorizontalGroup("数据操作/Buttons")]
            [Button("重新加载", ButtonHeight = 40), GUIColor(0.5f, 0.7f, 0.9f)]
            public void ReloadData()
            {
                window?.ES_LoadData();
                window?.ForceMenuTreeRebuild();
                window?.Repaint();
                EditorUtility.DisplayDialog("成功", "数据已重新加载！", "确定");
            }

            [HorizontalGroup("数据操作/Buttons")]
            [Button("定位文件", ButtonHeight = 40), GUIColor(0.9f, 0.7f, 0.3f)]
            public void PingAsset()
            {
                if (data != null)
                {
                    Selection.activeObject = data;
                    EditorGUIUtility.PingObject(data);
                }
            }

            [FoldoutGroup("危险操作", Expanded = false)]
            [InfoBox("以下操作不可恢复，请谨慎操作！", InfoMessageType.Warning)]
            [Button("清空所有数据", ButtonHeight = 40), GUIColor(0.9f, 0.2f, 0.2f)]
            public void ClearAllData()
            {
                if (EditorUtility.DisplayDialog("警告", "确定要清空所有数据吗？此操作不可恢复！", "清空", "取消"))
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

        // 继续下一部分...
        #endregion
    }
}
