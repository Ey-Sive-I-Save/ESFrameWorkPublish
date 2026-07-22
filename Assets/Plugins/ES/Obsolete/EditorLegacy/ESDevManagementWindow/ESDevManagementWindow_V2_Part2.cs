// ESDevManagementWindow_V2_Part2.cs
// 此文件包含剩余的页面定义，需要合并到 ESDevManagementWindow_V2.cs 的末尾

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;

namespace ES.Obsolete{
    // ==================== 任务列表视图 ====================
    public partial class ESDevManagementWindow_V2
    {
        [Serializable]
        public class Page_TaskList : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [Title("任务列表", titleAlignment: TitleAlignments.Centered, bold: true)]
            [PropertySpace(10)]

            [HorizontalGroup("筛选", Width = 0.25f)]
            [LabelText("状态"), LabelWidth(40)]
            [ValueDropdown("GetStatuses")]
            public string filterStatus = "全部";

            [HorizontalGroup("筛选", Width = 0.25f)]
            [LabelText("标签"), LabelWidth(40)]
            [ValueDropdown("GetTags")]
            public string filterTag = "全部";

            [HorizontalGroup("筛选", Width = 0.3f)]
            [LabelText("搜索"), LabelWidth(40)]
            public string searchText = "";

            [HorizontalGroup("筛选", Width = 0.2f)]
            [Button("筛选", ButtonHeight = 25), GUIColor(0.4f, 0.7f, 0.9f)]
            public void ApplyFilter()
            {
                RefreshList();
            }

            private IEnumerable<string> GetStatuses()
            {
                var statuses = new List<string> { "全部" };
                statuses.AddRange(Enum.GetNames(typeof(TaskStatusV2)));
                return statuses;
            }

            private IEnumerable<string> GetTags()
            {
                var tags = new List<string> { "全部" };
                if (data?.allTags != null) tags.AddRange(data.allTags);
                return tags;
            }

            [PropertySpace(5)]
            [ListDrawerSettings(ShowIndexLabels = false, DefaultExpandedState = true, IsReadOnly = true,
                       NumberOfItemsPerPage = 10, ShowPaging = true)]
            [HideLabel]
            public List<TaskCardView> displayTasks = new List<TaskCardView>();

            public void RefreshList()
            {
                if (data?.tasks == null)
                {
                    displayTasks = new List<TaskCardView>();
                    return;
                }

                var filtered = data.tasks.AsEnumerable();

                // 状态筛选
                if (filterStatus != "全部" && Enum.TryParse<TaskStatusV2>(filterStatus, out var status))
                    filtered = filtered.Where(t => t.status == status);

                // 标签筛选
                if (filterTag != "全部")
                    filtered = filtered.Where(t => t.tags != null && t.tags.Contains(filterTag));

                // 增强搜索：支持多字段
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var searchLower = searchText.ToLower();
                    filtered = filtered.Where(t => 
                        t.taskName.ToLower().Contains(searchLower) || 
                        t.description.ToLower().Contains(searchLower) ||
                        t.assignedTo.ToLower().Contains(searchLower) ||
                        t.createdBy.ToLower().Contains(searchLower));
                }

                // 排序优化：优先级 -> 状态 -> 截止日期
                displayTasks = filtered
                    .OrderBy(t => t.priority)
                    .ThenBy(t => t.status)
                    .ThenBy(t => t.dueDate)
                    .Select(t => new TaskCardView(t, window))
                    .ToList();
            }

            public override ESWindowPageBase ES_Refresh()
            {
                RefreshList();
                return base.ES_Refresh();
            }

            [PropertySpace(10)]
            [Button("刷新", ButtonHeight = 35), GUIColor(0.4f, 0.7f, 0.9f)]
            public void Refresh()
            {
                RefreshList();
                window?.Repaint();
            }
        }

        // ==================== 任务详情页 ====================
        [Serializable]
        public class Page_TaskDetail : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;
            [HideInInspector] public TaskEntryV2 selectedTask;

            [ShowIf("HasTask")]
            [Title("@GetTitle", titleAlignment: TitleAlignments.Left, bold: true)]
            [PropertySpace(10)]

            private string GetTitle() => selectedTask != null ? $"✓ {selectedTask.taskName}" : "未选择任务";
            private bool HasTask() => selectedTask != null;

            // 基本信息
            [ShowIf("HasTask")]
            [FoldoutGroup("基本信息", Expanded = true)]
            [HorizontalGroup("基本信息/Row1")]
            [LabelText("状态"), LabelWidth(60)]
            [ShowInInspector]
            private TaskStatusV2 Status
            {
                get => selectedTask?.status ?? TaskStatusV2.开始;
                set
                {
                    if (selectedTask != null)
                    {
                        selectedTask.status = value;
                        EditorUtility.SetDirty(data);
                    }
                }
            }

            [HorizontalGroup("基本信息/Row1")]
            [LabelText("优先级"), LabelWidth(60)]
            [ShowInInspector]
            private Priority Priority
            {
                get => selectedTask?.priority ?? Priority.中;
                set
                {
                    if (selectedTask != null)
                    {
                        selectedTask.priority = value;
                        EditorUtility.SetDirty(data);
                    }
                }
            }

            [HorizontalGroup("基本信息/Row1")]
            [LabelText("负责人"), LabelWidth(60)]
            [ShowInInspector]
            private string AssignedTo
            {
                get => selectedTask?.assignedTo ?? "";
                set
                {
                    if (selectedTask != null)
                    {
                        selectedTask.assignedTo = value;
                        EditorUtility.SetDirty(data);
                    }
                }
            }

            [FoldoutGroup("基本信息", Expanded = true)]
            [HorizontalGroup("基本信息/Row2")]
            [LabelText("开始日期"), LabelWidth(60), ReadOnly, DisplayAsString]
            private string StartDate => selectedTask?.startDate ?? "";

            [HorizontalGroup("基本信息/Row2")]
            [LabelText("截止日期"), LabelWidth(60), ReadOnly, DisplayAsString]
            private string DueDate => selectedTask?.dueDate ?? "";

            [HorizontalGroup("基本信息/Row2")]
            [LabelText("创建人"), LabelWidth(60), ReadOnly, DisplayAsString]
            private string CreatedBy => selectedTask?.createdBy ?? "";

            // 描述
            [ShowIf("HasTask")]
            [FoldoutGroup("任务描述", Expanded = true)]
            [LabelText(""), MultiLineProperty(8), ReadOnly]
            [ShowInInspector]
            private string Description => selectedTask?.description ?? "";

            // 子任务清单
            [ShowIf("HasTask")]
            [FoldoutGroup("子任务清单", Expanded = true)]
            [InfoBox("勾选完成子任务，自动计算总进度", InfoMessageType.Info)]
            [HorizontalGroup("子任务清单/Progress")]
            [LabelText("总进度"), LabelWidth(60)]
            [ProgressBar(0, 100, ColorGetter = "GetProgressColor")]
            [ShowInInspector]
            private int OverallProgress => selectedTask?.GetOverallProgress() ?? 0;

            [HorizontalGroup("子任务清单/Progress")]
            [LabelText(""), DisplayAsString]
            [ShowInInspector]
            private string ProgressText => selectedTask != null ? $"{selectedTask.checklist.Count(c => c.isCompleted)}/{selectedTask.checklist.Count} 已完成" : "0/0";

            [FoldoutGroup("子任务清单", Expanded = true)]
            [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true, OnTitleBarGUI = "DrawChecklistToolbar")]
            [ShowInInspector, HideLabel]
            private List<ChecklistItem> Checklist
            {
                get => selectedTask?.checklist ?? new List<ChecklistItem>();
                set
                {
                    if (selectedTask != null)
                    {
                        selectedTask.checklist = value;
                        EditorUtility.SetDirty(data);
                    }
                }
            }

            private void DrawChecklistToolbar()
            {
                if (GUILayout.Button("➕ 添加", GUILayout.Width(60)))
                {
                    AddChecklistItem();
                }
                if (GUILayout.Button("✅ 全选", GUILayout.Width(60)))
                {
                    if (selectedTask != null)
                    {
                        foreach (var item in selectedTask.checklist)
                        {
                            if (!item.isCompleted)
                            {
                                item.isCompleted = true;
                                item.completedTime = DateTime.Now.ToString("MM-dd HH:mm");
                            }
                        }
                        EditorUtility.SetDirty(data);
                    }
                }
                if (GUILayout.Button("❌ 清空", GUILayout.Width(60)))
                {
                    if (selectedTask != null && EditorUtility.DisplayDialog("确认", "确定清空所有子任务？", "清空", "取消"))
                    {
                        selectedTask.checklist.Clear();
                        EditorUtility.SetDirty(data);
                    }
                }
            }

            public void AddChecklistItem()
            {
                if (selectedTask != null)
                {
                    selectedTask.checklist.Add(new ChecklistItem("新子任务", selectedTask.assignedTo));
                    EditorUtility.SetDirty(data);
                }
            }

            private Color GetProgressColor(int value)
            {
                if (value >= 80) return new Color(0.2f, 0.9f, 0.3f);
                if (value >= 50) return new Color(0.9f, 0.7f, 0.2f);
                return new Color(0.9f, 0.3f, 0.3f);
            }

            // 标签
            [ShowIf("HasTask")]
            [FoldoutGroup("标签", Expanded = true)]
            [ValueDropdown("@data.allTags")]
            [ShowInInspector, HideLabel]
            private List<string> Tags
            {
                get => selectedTask?.tags ?? new List<string>();
                set
                {
                    if (selectedTask != null)
                    {
                        selectedTask.tags = value;
                        EditorUtility.SetDirty(data);
                    }
                }
            }

            // 关联
            [ShowIf("HasTask")]
            [FoldoutGroup("关联日志", Expanded = false)]
            [ShowInInspector, HideLabel, ReadOnly]
            private List<string> LinkedLogs => selectedTask?.linkedLogIds ?? new List<string>();

            // 操作按钮
            [ShowIf("HasTask")]
            [PropertySpace(15)]
            [HorizontalGroup("操作")]
            [Button("💾 保存修改", ButtonHeight = 42), GUIColor(0.3f, 0.9f, 0.5f)]
            public void Save()
            {
                if (selectedTask != null)
                {
                    selectedTask.lastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    selectedTask.lastModifiedBy = window.currentUser;
                    selectedTask.version++;
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    DevManagementSoundManager.PlaySaveSound();
                    window?.ForceMenuTreeRebuild();
                    EditorUtility.DisplayDialog("成功", "任务已保存！", "确定");
                }
            }

            [HorizontalGroup("操作")]
            [Button("✅ 标记完成", ButtonHeight = 42), GUIColor(0.2f, 0.9f, 0.3f)]
            public void MarkComplete()
            {
                if (selectedTask != null)
                {
                    selectedTask.status = TaskStatusV2.已完成;
                    // 自动完成所有子任务
                    foreach (var item in selectedTask.checklist)
                    {
                        if (!item.isCompleted)
                        {
                            item.isCompleted = true;
                            item.completedTime = DateTime.Now.ToString("MM-dd HH:mm");
                        }
                    }
                    DevManagementSoundManager.PlayCompleteSound();
                    Save();
                }
            }

            [HorizontalGroup("操作")]
            [Button("📋 复制", ButtonHeight = 42), GUIColor(0.5f, 0.7f, 0.9f)]
            public void Duplicate()
            {
                if (selectedTask != null && data != null)
                {
                    DevManagementSoundManager.PlayCreateSound();
                    var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var newTask = new TaskEntryV2
                    {
                        id = Guid.NewGuid().ToString(),
                        taskName = selectedTask.taskName + " (复制)",
                        description = selectedTask.description,
                        startDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        dueDate = DateTime.Now.AddDays(7).ToString("yyyy-MM-dd"),
                        assignedTo = selectedTask.assignedTo,
                        status = TaskStatusV2.开始,
                        priority = selectedTask.priority,
                        checklist = selectedTask.checklist.Select(c => new ChecklistItem(c.content, c.assignedTo)).ToList(),
                        tags = new List<string>(selectedTask.tags),
                        linkedLogIds = new List<string>(),
                        createTime = now,
                        lastModified = now,
                        createdBy = window.currentUser,
                        lastModifiedBy = window.currentUser,
                        version = 1
                    };
                    data.tasks.Add(newTask);
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("成功", "任务已复制！", "确定");
                }
            }

            [HorizontalGroup("操作")]
            [Button("🗑️ 删除", ButtonHeight = 42), GUIColor(0.9f, 0.3f, 0.3f)]
            public void Delete()
            {
                if (selectedTask != null && EditorUtility.DisplayDialog("确认", "确定删除此任务?", "删除", "取消"))
                {
                    DevManagementSoundManager.PlayDeleteSound();
                    data.tasks.Remove(selectedTask);
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    ESDevManagementWindow_V2.MenuItems[PageName_TaskList]?.Select();
                    EditorUtility.DisplayDialog("成功", "任务已删除", "确定");
                }
            }

            [ShowIf("@!HasTask()")]
            [InfoBox("请从列表中选择一个任务查看详情", InfoMessageType.Info)]
            [Button("返回列表", ButtonHeight = 40), GUIColor(0.4f, 0.7f, 0.9f)]
            public void BackToList()
            {
                ESDevManagementWindow_V2.MenuItems[PageName_TaskList]?.Select();
            }
        }

        // ==================== 新建任务 ====================
        [Serializable]
        public class Page_CreateTask : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;
            [HideInInspector] public string currentUser;

            [Title("新建任务", titleAlignment: TitleAlignments.Centered, bold: true)]
            [PropertySpace(10)]

            [BoxGroup("基本信息")]
            [LabelText("任务名称"), Required]
            public string taskName = "";

            [BoxGroup("基本信息")]
            [LabelText("任务描述"), TextArea(6, 15), Required]
            public string description = "";

            [BoxGroup("时间安排")]
            [HorizontalGroup("时间安排/Dates")]
            [LabelText("开始日期"), LabelWidth(80)]
            public string startDate = DateTime.Now.ToString("yyyy-MM-dd");

            [HorizontalGroup("时间安排/Dates")]
            [LabelText("截止日期"), LabelWidth(80)]
            public string dueDate = DateTime.Now.AddDays(7).ToString("yyyy-MM-dd");

            [BoxGroup("分配")]
            [HorizontalGroup("分配/Row1")]
            [LabelText("负责人"), LabelWidth(60)]
            public string assignedTo = "";

            [HorizontalGroup("分配/Row1")]
            [LabelText("状态"), LabelWidth(60)]
            [ValueDropdown("GetStatuses")]
            public TaskStatusV2 status = TaskStatusV2.开始;

            [HorizontalGroup("分配/Row1")]
            [LabelText("优先级"), LabelWidth(60)]
            [ValueDropdown("GetPriorities")]
            public Priority priority = Priority.中;

            [BoxGroup("子任务清单")]
            [InfoBox("添加子任务，便于细化工作和追踪进度", InfoMessageType.Info)]
            [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
            public List<ChecklistItem> checklist = new List<ChecklistItem>();

            [BoxGroup("子任务清单")]
            [Button("快速添加子任务", ButtonHeight = 30), GUIColor(0.4f, 0.8f, 0.9f)]
            public void QuickAddItem()
            {
                checklist.Add(new ChecklistItem("", assignedTo));
            }

            [BoxGroup("分类")]
            [LabelText("标签")]
            [ValueDropdown("@data.allTags")]
            public List<string> tags = new List<string>();

            [BoxGroup("关联")]
            [LabelText("关联日志ID")]
            public List<string> linkedLogIds = new List<string>();

            private IEnumerable<TaskStatusV2> GetStatuses() => Enum.GetValues(typeof(TaskStatusV2)).Cast<TaskStatusV2>();
            private IEnumerable<Priority> GetPriorities() => Enum.GetValues(typeof(Priority)).Cast<Priority>();

            [PropertySpace(15)]
            [HorizontalGroup("操作")]
            [Button("创建任务", ButtonHeight = 45), GUIColor(0.2f, 0.9f, 0.4f)]
            public void Create()
            {
                if (string.IsNullOrWhiteSpace(taskName))
                {
                    EditorUtility.DisplayDialog("错误", "任务名称不能为空", "确定");
                    return;
                }

                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var task = new TaskEntryV2
                {
                    id = Guid.NewGuid().ToString(),
                    taskName = taskName,
                    description = description,
                    startDate = startDate,
                    dueDate = dueDate,
                    assignedTo = string.IsNullOrWhiteSpace(assignedTo) ? currentUser : assignedTo,
                    status = status,
                    priority = priority,
                    checklist = new List<ChecklistItem>(checklist),
                    tags = new List<string>(tags),
                    linkedLogIds = new List<string>(linkedLogIds),
                    createTime = now,
                    lastModified = now,
                    createdBy = currentUser,
                    lastModifiedBy = currentUser,
                    version = 1
                };

                data.tasks.Add(task);

                // 更新标签库
                foreach (var tag in tags)
                {
                    if (!data.allTags.Contains(tag))
                        data.allTags.Add(tag);
                }

                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();

                DevManagementSoundManager.PlayCreateSound();
                EditorUtility.DisplayDialog("成功", "任务创建成功！", "确定");
                ClearForm();
            }

            [HorizontalGroup("操作")]
            [Button("清空", ButtonHeight = 45), GUIColor(0.7f, 0.7f, 0.7f)]
            public void ClearForm()
            {
                taskName = "";
                description = "";
                assignedTo = "";
                checklist.Clear();
                tags.Clear();
                linkedLogIds.Clear();
                status = TaskStatusV2.开始;
                priority = Priority.中;
                startDate = DateTime.Now.ToString("yyyy-MM-dd");
                dueDate = DateTime.Now.AddDays(7).ToString("yyyy-MM-dd");
            }
        }

        // ==================== 时间线 ====================
        [Serializable]
        public class Page_Timeline : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [Title("活动时间线", titleAlignment: TitleAlignments.Centered, bold: true)]
            [PropertySpace(10)]

            [HorizontalGroup("筛选")]
            [LabelText("时间范围"), LabelWidth(80)]
            [ValueDropdown("GetTimeRanges")]
            public string timeRange = "本周";

            [HorizontalGroup("筛选")]
            [Button("刷新", ButtonHeight = 25), GUIColor(0.4f, 0.7f, 0.9f)]
            public void Refresh()
            {
                BuildTimeline();
                window?.Repaint();
            }

            private IEnumerable<string> GetTimeRanges()
            {
                return new[] { "今天", "本周", "本月", "全部" };
            }

            [PropertySpace(10)]
            [ListDrawerSettings(ShowIndexLabels = false, OnTitleBarGUI = "DrawTimelineToolbar")]
            [HideLabel]
            public List<TimelineItem> timeline = new List<TimelineItem>();

            private void DrawTimelineToolbar()
            {
                if (GUILayout.Button("🔄 刷新", GUILayout.Width(80)))
                {
                    Refresh();
                }
                if (GUILayout.Button("📥 导出", GUILayout.Width(80)))
                {
                    ExportTimeline();
                }
            }

            private void ExportTimeline()
            {
                var path = EditorUtility.SaveFilePanel("导出时间线", "", "timeline.txt", "txt");
                if (!string.IsNullOrEmpty(path))
                {
                    var lines = timeline.Select(t => $"{t.time} | {t.type} | {t.title} | {t.user}");
                    System.IO.File.WriteAllLines(path, lines);
                    EditorUtility.DisplayDialog("成功", $"时间线已导出到\n{path}", "确定");
                }
            }

            public override ESWindowPageBase ES_Refresh()
            {
                BuildTimeline();
                return base.ES_Refresh();
            }

            private void BuildTimeline()
            {
                if (data == null) return;

                var items = new List<TimelineItem>();
                DateTime startDate = GetStartDate();

                // 日志活动
                if (data.devLogs != null)
                {
                    foreach (var log in data.devLogs)
                    {
                        if (DateTime.TryParse(log.createTime, out var time) && time >= startDate)
                        {
                            items.Add(new TimelineItem
                            {
                                time = time.ToString("yyyy-MM-dd HH:mm"),
                                type = "📝 日志",
                                title = $"{log.title} [{log.type}]",
                                user = log.createdBy,
                                icon = GetLogIcon(log.priority),
                                relatedId = log.id,
                                itemType = "log",
                                window = window
                            });
                        }
                    }
                }

                // 任务活动
                if (data.tasks != null)
                {
                    foreach (var task in data.tasks)
                    {
                        if (DateTime.TryParse(task.createTime, out var time) && time >= startDate)
                        {
                            items.Add(new TimelineItem
                            {
                                time = time.ToString("yyyy-MM-dd HH:mm"),
                                type = "✓ 任务",
                                title = $"{task.taskName} [{task.status}]",
                                user = task.createdBy,
                                icon = GetTaskIcon(task.status),
                                relatedId = task.id,
                                itemType = "task",
                                window = window
                            });
                        }
                    }
                }

                timeline = items.OrderByDescending(i => i.time).ToList();
            }

            private string GetLogIcon(Priority priority)
            {
                return priority switch
                {
                    Priority.紧急 => "🔴",
                    Priority.高 => "🟠",
                    Priority.中 => "🟡",
                    Priority.低 => "⚪",
                    _ => "📝"
                };
            }

            private string GetTaskIcon(TaskStatusV2 status)
            {
                return status switch
                {
                    TaskStatusV2.已完成 => "✅",
                    TaskStatusV2.进行中 => "🔄",
                    TaskStatusV2.评估中 => "🔍",
                    TaskStatusV2.开始 => "🎯",
                    _ => "📌"
                };
            }

            private DateTime GetStartDate()
            {
                var now = DateTime.Now;
                return timeRange switch
                {
                    "今天" => now.Date,
                    "本周" => now.AddDays(-(int)now.DayOfWeek),
                    "本月" => new DateTime(now.Year, now.Month, 1),
                    _ => DateTime.MinValue
                };
            }
        }

        [Serializable]
        public class TimelineItem
        {
            [HideInInspector] public string relatedId;
            [HideInInspector] public string itemType;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [HorizontalGroup("Item", Width = 0.15f)]
            [LabelText(""), DisplayAsString]
            [GUIColor(0.7f, 0.9f, 1f)]
            public string time;

            [HorizontalGroup("Item", Width = 0.05f)]
            [LabelText(""), DisplayAsString]
            public string icon;

            [HorizontalGroup("Item", Width = 0.1f)]
            [LabelText(""), DisplayAsString, GUIColor(0.7f, 0.9f, 1f)]
            public string type;

            [HorizontalGroup("Item", Width = 0.45f)]
            [LabelText(""), DisplayAsString]
            public string title;

            [HorizontalGroup("Item", Width = 0.15f)]
            [LabelText(""), DisplayAsString, GUIColor(0.8f, 0.8f, 0.8f)]
            public string user;

            [HorizontalGroup("Item", Width = 0.1f)]
            [Button("🔍 详情", ButtonHeight = 22), GUIColor(0.5f, 0.8f, 1f)]
            public void ViewDetail()
            {
                if (window == null || string.IsNullOrEmpty(relatedId)) return;

                if (itemType == "log")
                {
                    var log = window.dataAsset.devLogs.FirstOrDefault(l => l.id == relatedId);
                    if (log != null) window.SelectLogDetail(log);
                }
                else if (itemType == "task")
                {
                    var task = window.dataAsset.tasks.FirstOrDefault(t => t.id == relatedId);
                    if (task != null) window.SelectTaskDetail(task);
                }
            }
        }

        // ==================== 标签管理 ====================
        [Serializable]
        public class Page_Tags : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [Title("标签管理", titleAlignment: TitleAlignments.Centered, bold: true)]
            [PropertySpace(10)]

            [FoldoutGroup("标签统计", Expanded = true)]
            [ShowInInspector, DisplayAsString, HideLabel]
            [MultiLineProperty(5)]
            private string TagStats
            {
                get
                {
                    if (data == null) return "无数据";
                    var stats = new System.Text.StringBuilder();
                    stats.AppendLine($"🏷️ 标签总数: {data.allTags.Count}");
                    stats.AppendLine($"📝 使用中的标签: {GetUsedTagsCount()}");
                    stats.AppendLine($"📈 最常用: {GetMostUsedTag()}");
                    stats.AppendLine($"📊 平均每项标签数: {GetAverageTagsPerItem():F1}");
                    return stats.ToString();
                }
            }

            private int GetUsedTagsCount()
            {
                if (data == null) return 0;
                var usedTags = new HashSet<string>();
                data.devLogs?.ForEach(l => l.tags?.ForEach(t => usedTags.Add(t)));
                data.tasks?.ForEach(t => t.tags?.ForEach(tag => usedTags.Add(tag)));
                return usedTags.Count;
            }

            private string GetMostUsedTag()
            {
                if (data == null) return "-";
                var tagCounts = new Dictionary<string, int>();
                
                data.devLogs?.ForEach(l => l.tags?.ForEach(t => 
                {
                    if (!tagCounts.ContainsKey(t)) tagCounts[t] = 0;
                    tagCounts[t]++;
                }));
                
                data.tasks?.ForEach(t => t.tags?.ForEach(tag => 
                {
                    if (!tagCounts.ContainsKey(tag)) tagCounts[tag] = 0;
                    tagCounts[tag]++;
                }));
                
                if (tagCounts.Count == 0) return "-";
                var most = tagCounts.OrderByDescending(kv => kv.Value).First();
                return $"{most.Key} ({most.Value}次)";
            }

            private double GetAverageTagsPerItem()
            {
                if (data == null) return 0;
                var totalItems = (data.devLogs?.Count ?? 0) + (data.tasks?.Count ?? 0);
                if (totalItems == 0) return 0;
                
                var totalTags = 0;
                data.devLogs?.ForEach(l => totalTags += l.tags?.Count ?? 0);
                data.tasks?.ForEach(t => totalTags += t.tags?.Count ?? 0);
                
                return (double)totalTags / totalItems;
            }

            [InfoBox("管理所有标签，用于分类日志和任务", InfoMessageType.Info)]
            [FoldoutGroup("标签列表", Expanded = true)]
            [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true, OnTitleBarGUI = "DrawTagsToolbar")]
            [ShowInInspector, HideLabel]
            private List<string> AllTags
            {
                get => data?.allTags ?? new List<string>();
                set
                {
                    if (data != null)
                    {
                        data.allTags = value;
                        EditorUtility.SetDirty(data);
                    }
                }
            }

            private void DrawTagsToolbar()
            {
                if (GUILayout.Button("➕ 添加", GUILayout.Width(60)))
                {
                    AddTag();
                }
                if (GUILayout.Button("🧼 清理", GUILayout.Width(60)))
                {
                    CleanUnusedTags();
                }
            }

            [PropertySpace(10)]
            [HorizontalGroup("操作")]
            [Button("➕ 添加标签", ButtonHeight = 35), GUIColor(0.3f, 0.9f, 0.5f)]
            public void AddTag()
            {
                if (data != null)
                {
                    data.allTags.Add("新标签");
                    EditorUtility.SetDirty(data);
                }
            }

            [HorizontalGroup("操作")]
            [Button("🧼 清理未使用标签", ButtonHeight = 35), GUIColor(0.9f, 0.7f, 0.3f)]
            public void CleanUnusedTags()
            {
                if (data == null) return;
                
                var usedTags = new HashSet<string>();
                data.devLogs?.ForEach(l => l.tags?.ForEach(t => usedTags.Add(t)));
                data.tasks?.ForEach(t => t.tags?.ForEach(tag => usedTags.Add(tag)));
                
                var before = data.allTags.Count;
                data.allTags = data.allTags.Where(t => usedTags.Contains(t)).ToList();
                var removed = before - data.allTags.Count;
                
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                DevManagementSoundManager.PlayDeleteSound();
                EditorUtility.DisplayDialog("成功", $"已清理 {removed} 个未使用标签", "确定");
            }

            [HorizontalGroup("操作")]
            [Button("💾 保存", ButtonHeight = 35), GUIColor(0.4f, 0.7f, 0.9f)]
            public void Save()
            {
                if (data != null)
                {
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    DevManagementSoundManager.PlaySaveSound();
                    EditorUtility.DisplayDialog("成功", "标签已保存", "确定");
                }
            }
        }

        // ==================== 任务日历视图 ====================
        [Serializable]
        public class Page_TaskCalendar : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [Title("任务日历视图", "📅 按日期查看任务", titleAlignment: TitleAlignments.Centered, bold: true)]
            [PropertySpace(10)]

            [FoldoutGroup("日历导航", Expanded = true)]
            [HorizontalGroup("日历导航/Nav")]
            [Button("◀ 上一周", ButtonHeight = 30), GUIColor(0.6f, 0.7f, 0.9f)]
            public void PreviousWeek()
            {
                DevManagementSoundManager.PlayClickSound();
                currentWeekStart = currentWeekStart.AddDays(-7);
                RefreshCalendar();
            }

            [HorizontalGroup("日历导航/Nav")]
            [ShowInInspector, DisplayAsString, LabelText("当前周")]
            [GUIColor(0.7f, 0.9f, 1f)]
            private string CurrentWeek => $"{currentWeekStart:yyyy-MM-dd} ~ {currentWeekStart.AddDays(6):yyyy-MM-dd}";

            [HorizontalGroup("日历导航/Nav")]
            [Button("今天", ButtonHeight = 30), GUIColor(0.4f, 0.9f, 0.5f)]
            public void GoToToday()
            {
                DevManagementSoundManager.PlayClickSound();
                var today = DateTime.Now;
                currentWeekStart = today.AddDays(-(int)today.DayOfWeek);
                RefreshCalendar();
            }

            [HorizontalGroup("日历导航/Nav")]
            [Button("下一周 ▶", ButtonHeight = 30), GUIColor(0.6f, 0.7f, 0.9f)]
            public void NextWeek()
            {
                DevManagementSoundManager.PlayClickSound();
                currentWeekStart = currentWeekStart.AddDays(7);
                RefreshCalendar();
            }

            private DateTime currentWeekStart = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek);

            [FoldoutGroup("本周任务", Expanded = true)]
            [ListDrawerSettings(ShowIndexLabels = false, DefaultExpandedState = true)]
            [HideLabel]
            public List<DailyTaskGroup> weeklyTasks = new List<DailyTaskGroup>();

            public override ESWindowPageBase ES_Refresh()
            {
                RefreshCalendar();
                return base.ES_Refresh();
            }

            public void RefreshCalendar()
            {
                weeklyTasks = new List<DailyTaskGroup>();
                
                if (data?.tasks == null) return;

                for (int i = 0; i < 7; i++)
                {
                    var date = currentWeekStart.AddDays(i);
                    var dateStr = date.ToString("yyyy-MM-dd");
                    
                    var tasksForDay = data.tasks.Where(t => 
                        (t.startDate == dateStr || t.dueDate == dateStr) ||
                        (DateTime.TryParse(t.startDate, out var start) && DateTime.TryParse(t.dueDate, out var end) &&
                         date >= start && date <= end))
                        .OrderBy(t => t.priority)
                        .ToList();

                    weeklyTasks.Add(new DailyTaskGroup
                    {
                        date = date,
                        tasks = tasksForDay,
                        window = window
                    });
                }
            }

            [PropertySpace(10)]
            [Button("🔄 刷新日历", ButtonHeight = 35), GUIColor(0.4f, 0.7f, 0.9f)]
            public void Refresh()
            {
                RefreshCalendar();
                window?.Repaint();
            }
        }

        [Serializable]
        public class DailyTaskGroup
        {
            [HideInInspector] public DateTime date;
            [HideInInspector] public List<TaskEntryV2> tasks;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [FoldoutGroup("日期", Expanded = true)]
            [ShowInInspector, DisplayAsString, HideLabel]
            [GUIColor("GetDateColor")]
            public string DateHeader => GetDateString();

            [FoldoutGroup("日期", Expanded = true)]
            [ShowInInspector, DisplayAsString, HideLabel]
            [GUIColor(0.8f, 0.8f, 0.8f)]
            public string TaskCount => $"📌 任务数: {tasks?.Count ?? 0}";

            [FoldoutGroup("日期", Expanded = true)]
            [ListDrawerSettings(ShowIndexLabels = true, DefaultExpandedState = false)]
            [ShowInInspector, HideLabel]
            private List<SimplifiedTaskView> TaskList
            {
                get
                {
                    if (tasks == null || tasks.Count == 0) return new List<SimplifiedTaskView>();
                    return tasks.Select(t => new SimplifiedTaskView { task = t, window = window }).ToList();
                }
            }

            private string GetDateString()
            {
                var dayOfWeek = date.DayOfWeek switch
                {
                    DayOfWeek.Sunday => "周日",
                    DayOfWeek.Monday => "周一",
                    DayOfWeek.Tuesday => "周二",
                    DayOfWeek.Wednesday => "周三",
                    DayOfWeek.Thursday => "周四",
                    DayOfWeek.Friday => "周五",
                    DayOfWeek.Saturday => "周六",
                    _ => ""
                };

                var isToday = date.Date == DateTime.Now.Date;
                var prefix = isToday ? "📍 今天 " : "";

                return $"{prefix}{date:MM月dd日} {dayOfWeek}";
            }

            private Color GetDateColor()
            {
                if (date.Date == DateTime.Now.Date)
                    return new Color(0.3f, 0.9f, 0.5f); // 今天 - 绿色
                if (date.Date < DateTime.Now.Date)
                    return new Color(0.7f, 0.7f, 0.7f); // 过去 - 灰色
                return new Color(0.7f, 0.9f, 1f); // 未来 - 蓝色
            }
        }

        [Serializable]
        public class SimplifiedTaskView
        {
            [HideInInspector] public TaskEntryV2 task;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [HorizontalGroup("Task", Width = 0.5f)]
            [DisplayAsString, LabelText("")]
            [GUIColor("GetStatusColor")]
            public string TaskInfo => $"{GetIcon()} {task.taskName}";

            [HorizontalGroup("Task", Width = 0.2f)]
            [DisplayAsString, LabelText("")]
            [GUIColor("GetPriorityColor")]
            public Priority Priority => task.priority;

            [HorizontalGroup("Task", Width = 0.15f)]
            [DisplayAsString, LabelText("")]
            public string Assignee => task.assignedTo;

            [HorizontalGroup("Task", Width = 0.15f)]
            [Button("查看", ButtonHeight = 25), GUIColor(0.5f, 0.8f, 0.9f)]
            public void View()
            {
                DevManagementSoundManager.PlayClickSound();
                window?.SelectTaskDetail(task);
            }

            private string GetIcon()
            {
                return task.status switch
                {
                    TaskStatusV2.已完成 => "✅",
                    TaskStatusV2.进行中 => "🔄",
                    TaskStatusV2.评估中 => "🔍",
                    _ => "📌"
                };
            }

            private Color GetStatusColor()
            {
                return task.status switch
                {
                    TaskStatusV2.已完成 => new Color(0.3f, 0.9f, 0.4f),
                    TaskStatusV2.进行中 => new Color(0.4f, 0.7f, 0.9f),
                    _ => new Color(0.9f, 0.9f, 0.9f)
                };
            }

            private Color GetPriorityColor()
            {
                return task.priority switch
                {
                    Priority.紧急 => new Color(0.9f, 0.2f, 0.2f),
                    Priority.高 => new Color(0.9f, 0.6f, 0.2f),
                    Priority.中 => new Color(0.6f, 0.8f, 0.3f),
                    _ => new Color(0.7f, 0.7f, 0.7f)
                };
            }
        }
    }
}
