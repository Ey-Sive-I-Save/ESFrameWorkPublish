using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace ES
{
 
    [Serializable, TypeRegistryItem("标准状态共享数据")]
    public class StateSharedData : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;
        [TabGroup("核心", "基础配置")]
        [InlineProperty, HideLabel]
        public StateBasicConfig basicConfig = new StateBasicConfig();
        
        [TabGroup("核心", "动画配置")]
        [LabelText("动画配置")]
        public StateAnimationConfigData animationConfig = new StateAnimationConfigData();

        [TabGroup("核心", "动画配置")]
        [LabelText("是否有动画")]
        [Tooltip("如果为false，则不会被加入Playable图")]
        public bool hasAnimation = false;

        [TabGroup("核心", "动画配置")]
        [BoxGroup("核心/动画配置/淡入淡出配置")]
        [LabelText("启用淡入淡出"), ToggleLeft]
        public bool enableFadeInOut = true;

        [BoxGroup("核心/动画配置/淡入淡出配置")]
        [LabelText("淡入时间(秒)"), ShowIf("enableFadeInOut"), Range(0f, 2f)]
        public float fadeInDuration = 0.2f;

        [BoxGroup("核心/动画配置/淡入淡出配置")]
        [LabelText("淡出时间(秒)"), ShowIf("enableFadeInOut"), Range(0f, 2f)]
        public float fadeOutDuration = 0.15f;

        [TabGroup("核心", "元数据")]
        [BoxGroup("核心/元数据/标签系统")]
        [LabelText("状态标签"), Tooltip("用于快速分类和查询（如Attack、Movement、Skill）")]
        public List<string> stateTags = new List<string>();

        [BoxGroup("核心/元数据/标签系统")]
        [LabelText("状态分组"), Tooltip("用于UI分组显示和批量管理")]
        public string stateGroup = "Default";

        [BoxGroup("核心/元数据/显示信息")]
        [LabelText("显示名称"), Tooltip("用于UI显示的友好名称")]
        public string displayName = "";

        [BoxGroup("核心/元数据/显示信息")]
        [LabelText("描述"), TextArea(2, 4), Tooltip("状态功能描述")]
        public string description = "";

        [BoxGroup("核心/元数据/显示信息")]
        [LabelText("图标"), PreviewField(50), Tooltip("状态图标（用于UI）")]
        public Sprite icon;

        [TabGroup("核心", "性能配置")]
        [BoxGroup("核心/性能配置/优化选项")]
        [LabelText("启用性能统计"), Tooltip("收集该状态的性能数据（轻微开销）")]
        public bool enablePerformanceTracking = false;

        [BoxGroup("核心/性能配置/优化选项")]
        [LabelText("预加载优先级"), Range(0, 10), Tooltip("数值越高越优先预加载（0=不预加载）")]
        public int preloadPriority = 0;

        [BoxGroup("核心/性能配置/优化选项")]
        [LabelText("常驻内存"), Tooltip("是否保持在内存中不卸载（高频状态建议开启）")]
        public bool keepInMemory = false;

        [TabGroup("核心", "调试辅助")]
        [BoxGroup("核心/调试辅助/可视化")]
        [LabelText("Gizmo颜色"), Tooltip("Scene视图中的调试颜色")]
        public Color debugGizmoColor = Color.white;

        [BoxGroup("核心/调试辅助/可视化")]
        [LabelText("显示调试信息"), Tooltip("是否在Game视图显示该状态的调试信息")]
        public bool showDebugInfo = false;

        [BoxGroup("核心/调试辅助/测试")]
        [LabelText("单元测试数据"), TextArea(3, 6), Tooltip("用于自动化测试的JSON数据")]
        public string testData = "";

        [TabGroup("切换", "冲突合并配置")]
        [InlineProperty, HideLabel]
        public StateMergeData mergeData = new StateMergeData();

        [TabGroup("切换", "代价配置")]
        [InlineProperty, HideLabel]
        public StateCostData costData = new StateCostData();

        /// <summary>
        /// 运行时初始化 - 递归初始化所有子成员
        /// </summary>
        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            
            // 递归初始化所有成员
            basicConfig?.InitializeRuntime();
            animationConfig?.InitializeRuntime();
            mergeData?.InitializeRuntime();
            costData?.InitializeRuntime();
            
            // 预备主状态判据值（依赖costData）
            basicConfig?.PrepareMainCriterionValue(costData);
            
            _isRuntimeInitialized = true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器手动初始化接口
        /// </summary>
        [Button("初始化预备数据"), PropertyOrder(-1)]
        private void EditorInitialize()
        {
            _isRuntimeInitialized = false; // 重置标记以允许重新初始化
            InitializeRuntime();
            Debug.Log($"[StateSharedData] 预备主状态判据值: {basicConfig.preparedMainCriterionValue}");
        }
#endif

        /// <summary>
        /// 检查状态是否包含指定标签
        /// </summary>
        public bool HasTag(string tag)
        {
            return stateTags != null && stateTags.Contains(tag);
        }

        /// <summary>
        /// 添加标签（运行时）
        /// </summary>
        public void AddTag(string tag)
        {
            if (stateTags == null)
                stateTags = new List<string>();
            if (!stateTags.Contains(tag))
                stateTags.Add(tag);
        }

        /// <summary>
        /// 获取显示名称（如果未设置则返回状态Key）
        /// </summary>
        public string GetDisplayName(string fallbackKey = "")
        {
            return !string.IsNullOrEmpty(displayName) ? displayName : fallbackKey;
        }


    }

    [Serializable, TypeRegistryItem("标准状态运行状态")]
    //目前还没有特殊的玩意
    public class StateVariableData : IDeepClone<StateVariableData>
    {
        [LabelText("状态开始时间")] public float hasEnterTime;
        public void DeepCloneFrom(StateVariableData t)
        {
            if (t is StateVariableData svd)
            {
                hasEnterTime = svd.hasEnterTime;
            }
        }
    }

}
