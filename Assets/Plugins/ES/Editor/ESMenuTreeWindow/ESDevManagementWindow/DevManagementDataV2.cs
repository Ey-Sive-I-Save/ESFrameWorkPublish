using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 开发管理数据 V2 - 增强版
    /// </summary>
    [CreateAssetMenu(fileName = "DevManagementDataV2", menuName = "ES/开发管理数据 V2")]
    public class DevManagementDataV2 : ScriptableObject
    {
        [Title("元数据")]
        [LabelText("最后修改人"), ReadOnly]
        public string lastModifiedBy = "";

        [LabelText("最后修改时间"), ReadOnly]
        public string lastModifiedTime = "";

        [LabelText("数据版本"), ReadOnly]
        public int dataVersion = 1;

        [Title("数据")]
        [LabelText("开发日志")]
        [ListDrawerSettings(ListElementLabelName = "title")]
        public List<DevLogEntryV2> devLogs = new List<DevLogEntryV2>();

        [LabelText("任务列表")]
        [ListDrawerSettings(ListElementLabelName = "taskName")]
        public List<TaskEntryV2> tasks = new List<TaskEntryV2>();

        [LabelText("标签库")]
        public List<string> allTags = new List<string>();

        public void InitializeDefault()
        {
            devLogs = new List<DevLogEntryV2>();
            tasks = new List<TaskEntryV2>();
            allTags = new List<string> { "Bug", "Feature", "优化", "文档", "测试" };
            lastModifiedBy = Environment.UserName;
            lastModifiedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            dataVersion = 1;
        }
    }

    // ==================== 开发日志 V2 ====================
    [Serializable]
    public class DevLogEntryV2
    {
        [HideInInspector]
        public string id;

        [TableColumnWidth(250)]
        [LabelText("标题")]
        public string title;

        [TableColumnWidth(80)]
        [LabelText("类型")]
        public DevLogType type;

        [TableColumnWidth(60)]
        [LabelText("优先级")]
        public Priority priority;

        [HideInTables]
        [TextArea(10, 20)]
        [LabelText("内容")]
        public string content;

        [HideInTables]
        [TextArea(4, 10)]
        [LabelText("变更描述")]
        public string changeDescription;

        [TableColumnWidth(150)]
        [LabelText("标签")]
        public List<string> tags = new List<string>();

        [HideInTables]
        [LabelText("关联任务ID")]
        public List<string> linkedTaskIds = new List<string>();

        [TableColumnWidth(120)]
        [LabelText("创建时间"), ReadOnly]
        public string createTime;

        [TableColumnWidth(120)]
        [LabelText("修改时间"), ReadOnly]
        public string lastModified;

        [TableColumnWidth(80)]
        [LabelText("创建人"), ReadOnly]
        public string createdBy;

        [TableColumnWidth(80)]
        [LabelText("修改人"), ReadOnly]
        public string lastModifiedBy;

        [HideInTables]
        [LabelText("版本"), ReadOnly]
        public int version = 1;
    }

    // ==================== 任务 V2 ====================
    [Serializable]
    public class TaskEntryV2
    {
        [HideInInspector]
        public string id;

        [TableColumnWidth(200)]
        [LabelText("任务名称")]
        public string taskName;

        [HideInTables]
        [TextArea(5, 15)]
        [LabelText("任务描述")]
        public string description;

        [TableColumnWidth(100)]
        [LabelText("开始日期")]
        public string startDate;

        [TableColumnWidth(100)]
        [LabelText("截止日期")]
        public string dueDate;

        [TableColumnWidth(80)]
        [LabelText("负责人")]
        public string assignedTo;

        [TableColumnWidth(80)]
        [LabelText("状态")]
        public TaskStatusV2 status;

        [TableColumnWidth(60)]
        [LabelText("优先级")]
        public Priority priority;

        [HideInTables]
        [LabelText("子任务清单")]
        [ListDrawerSettings(ShowIndexLabels = true)]
        public List<ChecklistItem> checklist = new List<ChecklistItem>();

        [TableColumnWidth(80)]
        [ProgressBar(0, 100, ColorGetter = "GetProgressColor")]
        [LabelText("总进度 %")]
        [ShowInInspector, ReadOnly]
        public int overallProgress => GetOverallProgress();

        [HideInTables]
        [LabelText("标签")]
        public List<string> tags = new List<string>();

        [HideInTables]
        [LabelText("关联日志ID")]
        public List<string> linkedLogIds = new List<string>();

        [TableColumnWidth(120)]
        [LabelText("创建时间"), ReadOnly]
        public string createTime;

        [TableColumnWidth(120)]
        [LabelText("修改时间"), ReadOnly]
        public string lastModified;

        [TableColumnWidth(80)]
        [LabelText("创建人"), ReadOnly]
        public string createdBy;

        [TableColumnWidth(80)]
        [LabelText("修改人"), ReadOnly]
        public string lastModifiedBy;

        [HideInTables]
        [LabelText("版本"), ReadOnly]
        public int version = 1;

        /// <summary>
        /// 计算总进度（基于子任务完成情况）
        /// </summary>
        public int GetOverallProgress()
        {
            if (checklist == null || checklist.Count == 0)
                return status == TaskStatusV2.已完成 ? 100 : 0;

            int completed = checklist.Count(c => c.isCompleted);
            return (int)Math.Round((completed * 100.0) / checklist.Count);
        }

        /// <summary>
        /// 获取未完成的子任务数
        /// </summary>
        public int GetPendingCount()
        {
            return checklist?.Count(c => !c.isCompleted) ?? 0;
        }

        private Color GetProgressColor(int value)
        {
            if (value >= 80) return new Color(0.2f, 0.9f, 0.3f);
            if (value >= 50) return new Color(0.9f, 0.7f, 0.2f);
            return new Color(0.9f, 0.3f, 0.3f);
        }
    }

    // ==================== 子任务清单项 ====================
    [Serializable]
    public class ChecklistItem
    {
        [HorizontalGroup("Item", Width = 0.05f)]
        [LabelText(""), LabelWidth(5)]
        [ToggleLeft]
        public bool isCompleted;

        [HorizontalGroup("Item", Width = 0.7f)]
        [LabelText(""), LabelWidth(5)]
        [HideIf("isCompleted")]
        public string content;

        [HorizontalGroup("Item", Width = 0.7f)]
        [LabelText(""), LabelWidth(5)]
        [ShowIf("isCompleted")]
        [GUIColor(0.6f, 0.8f, 0.6f)]
        [DisplayAsString]
        public string CompletedContent => $"✓ {content}";

        [HorizontalGroup("Item", Width = 0.15f)]
        [LabelText(""), LabelWidth(5)]
        public string assignedTo;

        [HorizontalGroup("Item", Width = 0.1f)]
        [LabelText(""), LabelWidth(5), DisplayAsString]
        [ShowIf("isCompleted")]
        public string completedTime;

        public ChecklistItem()
        {
            content = "";
            isCompleted = false;
            assignedTo = "";
            completedTime = "";
        }

        public ChecklistItem(string content, string assignedTo = "")
        {
            this.content = content;
            this.isCompleted = false;
            this.assignedTo = assignedTo;
            this.completedTime = "";
        }

        public void ToggleComplete()
        {
            isCompleted = !isCompleted;
            if (isCompleted)
                completedTime = DateTime.Now.ToString("MM-dd HH:mm");
            else
                completedTime = "";
        }
    }

    // ==================== 枚举定义 ====================
    [Serializable]
    public enum TaskStatusV2
    {
        开始,
        进行中,
        评估中,
        已完成,
        已取消,
        暂停
    }

    [Serializable]
    public enum DevLogType
    {
        功能开发,
        Bug修复,
        性能优化,
        重构,
        文档更新,
        测试,
        部署,
        会议记录,
        其他
    }

    [Serializable]
    public enum Priority
    {
        低 = 3,
        中 = 2,
        高 = 1,
        紧急 = 0
    }
}
