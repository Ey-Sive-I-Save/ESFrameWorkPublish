// ESDevManagementWindow_V2_Part2.cs
// 此文件包含剩余的页面定义，需要合并到 ESDevManagementWindow_V2.cs 的末尾

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;

namespace ES
{
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
            [TableList(ShowIndexLabels = false, AlwaysExpanded = false, IsReadOnly = true,
                       NumberOfItemsPerPage = 15, ShowPaging = true)]
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

                if (filterStatus != "全部" && Enum.TryParse<TaskStatusV2>(filterStatus, out var status))
                    filtered = filtered.Where(t => t.status == status);

                if (filterTag != "全部")
                    filtered = filtered.Where(t => t.tags != null && t.tags.Contains(filterTag));

                if (!string.IsNullOrWhiteSpace(searchText))
                    filtered = filtered.Where(t => 
                        t.taskName.Contains(searchText) || 
                        t.description.Contains(searchText) ||
                        t.assignedTo.Contains(searchText));

                displayTasks = filtered.OrderBy(t => t.priority).ThenBy(t => t.dueDate)
                    .Select(t => new TaskCardView(t, window)).ToList();
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
            [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
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

            [FoldoutGroup("子任务清单", Expanded = true)]
            [HorizontalGroup("子任务清单/Actions")]
            [Button("添加子任务", ButtonHeight = 30), GUIColor(0.3f, 0.9f, 0.5f)]
            public void AddChecklistItem()
            {
                if (selectedTask != null)
                {
                    selectedTask.checklist.Add(new ChecklistItem("新子任务", selectedTask.assignedTo));
                    EditorUtility.SetDirty(data);
                }
            }

            [HorizontalGroup("子任务清单/Actions")]
            [ProgressBar(0, 100, ColorGetter = "GetProgressColor")]
            [ShowInInspector, LabelText("总进度")]
            private int OverallProgress => selectedTask?.GetOverallProgress() ?? 0;

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
            [Button("保存修改", ButtonHeight = 40), GUIColor(0.3f, 0.9f, 0.5f)]
            public void Save()
            {
                if (selectedTask != null)
                {
                    selectedTask.lastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    selectedTask.lastModifiedBy = window.currentUser;
                    selectedTask.version++;
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("成功", "任务已保存", "确定");
                }
            }

            [HorizontalGroup("操作")]
            [Button("标记完成", ButtonHeight = 40), GUIColor(0.2f, 0.9f, 0.3f)]
            public void MarkComplete()
            {
                if (selectedTask != null)
                {
                    selectedTask.status = TaskStatusV2.已完成;
                    Save();
                }
            }

            [HorizontalGroup("操作")]
            [Button("删除", ButtonHeight = 40), GUIColor(0.9f, 0.3f, 0.3f)]
            public void Delete()
            {
                if (selectedTask != null && EditorUtility.DisplayDialog("确认", "确定删除此任务?", "删除", "取消"))
                {
                    data.tasks.Remove(selectedTask);
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    ESDevManagementWindow_V2.MenuItems[PageName_TaskList]?.Select();
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
            [ListDrawerSettings(ShowIndexLabels = false)]
            [HideLabel]
            public List<TimelineItem> timeline = new List<TimelineItem>();

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
                foreach (var log in data.devLogs)
                {
                    if (DateTime.TryParse(log.createTime, out var time) && time >= startDate)
                    {
                        items.Add(new TimelineItem
                        {
                            time = time.ToString("yyyy-MM-dd HH:mm"),
                            type = "日志",
                            title = log.title,
                            user = log.createdBy,
                            icon = "📝"
                        });
                    }
                }

                // 任务活动
                foreach (var task in data.tasks)
                {
                    if (DateTime.TryParse(task.createTime, out var time) && time >= startDate)
                    {
                        items.Add(new TimelineItem
                        {
                            time = time.ToString("yyyy-MM-dd HH:mm"),
                            type = "任务",
                            title = task.taskName,
                            user = task.createdBy,
                            icon = "✓"
                        });
                    }
                }

                timeline = items.OrderByDescending(i => i.time).ToList();
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
            [HorizontalGroup("Item", Width = 0.15f)]
            [LabelText(""), DisplayAsString]
            public string time;

            [HorizontalGroup("Item", Width = 0.05f)]
            [LabelText(""), DisplayAsString]
            public string icon;

            [HorizontalGroup("Item", Width = 0.1f)]
            [LabelText(""), DisplayAsString, GUIColor(0.7f, 0.9f, 1f)]
            public string type;

            [HorizontalGroup("Item", Width = 0.5f)]
            [LabelText(""), DisplayAsString]
            public string title;

            [HorizontalGroup("Item", Width = 0.2f)]
            [LabelText(""), DisplayAsString, GUIColor(0.8f, 0.8f, 0.8f)]
            public string user;
        }

        // ==================== 标签管理 ====================
        [Serializable]
        public class Page_Tags : ESWindowPageBase
        {
            [HideInInspector] public DevManagementDataV2 data;
            [HideInInspector] public ESDevManagementWindow_V2 window;

            [Title("标签管理", titleAlignment: TitleAlignments.Centered, bold: true)]
            [PropertySpace(10)]

            [InfoBox("管理所有标签，用于分类日志和任务", InfoMessageType.Info)]
            [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
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

            [PropertySpace(10)]
            [HorizontalGroup("操作")]
            [Button("添加标签", ButtonHeight = 35), GUIColor(0.3f, 0.9f, 0.5f)]
            public void AddTag()
            {
                if (data != null)
                {
                    data.allTags.Add("新标签");
                    EditorUtility.SetDirty(data);
                }
            }

            [HorizontalGroup("操作")]
            [Button("保存", ButtonHeight = 35), GUIColor(0.4f, 0.7f, 0.9f)]
            public void Save()
            {
                if (data != null)
                {
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("成功", "标签已保存", "确定");
                }
            }
        }
    }
}
