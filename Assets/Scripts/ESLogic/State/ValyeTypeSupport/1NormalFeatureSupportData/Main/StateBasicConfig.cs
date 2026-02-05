using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
namespace ES
{

    /// <summary>
    /// 状态基础配置 - 标识与生命周期
    /// </summary>
    [Serializable]
    public class StateBasicConfig : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;
        [HorizontalGroup("Identity", Width = 0.4f)]
        [VerticalGroup("Identity/Left")]
        [LabelText("状态ID(重复时会被运行时Hash值顶掉)")]
        [Tooltip("用于唯一标识状态；重复ID会被运行时Hash覆盖。建议由资源导出或工具保证唯一性。")]
        public int stateId;

        [VerticalGroup("Identity/Left")]
        [LabelText("状态标识名称")]
        public string stateName = "新状态";

        [VerticalGroup("Identity/Right")]
        [LabelText("默认优先级(仅作为最后判据)")]
        [Tooltip("优先级仅在所有其它判定无法决断时作为最终比较依据；建议范围0-255。")]
        [Range(0, 255)]
        public byte priority = 50;

        [VerticalGroup("Identity/Right")]
        [LabelText("所属流水线(重要！)")]
        public StatePipelineType pipelineType = StatePipelineType.Basic;

        [VerticalGroup("Identity/Right")]
        [LabelText("可作为Fallback状态")]
        [Tooltip("勾选后该状态可被用作Fallback状态（如资源无匹配或失效时的兜底流转）。")]
        public bool canBeFeedback = false;

        [VerticalGroup("Identity/Right")]
        [LabelText("Fallback支持标记")]
        [Tooltip("当canBeFeedback=true时，指定该Fallback状态对应的支持标记。")]
        [ShowIf("canBeFeedback")]
        [FormerlySerializedAs("fallbackChannelIndex")]
        public StateSupportFlags fallbackSupportFlag = StateSupportFlags.Grounded;

        [LabelText("状态描述"), TextArea(2, 3)]
        public string description = "";

        [BoxGroup("支持标记")]
        [LabelText("忽略SupportFlags")]
        [Tooltip("启用后该状态不会受SupportFlags限制（如Buff/特效状态）。")]
        public bool ignoreSupportFlags = false;

        [BoxGroup("支持标记")]
        [LabelText("进入前必须满足")]
        [Tooltip("进入该状态前必须满足的支持标记（位标记）。")]
        public StateSupportFlags requiredSupportFlags = StateSupportFlags.None;

        [BoxGroup("支持标记")]
        [LabelText("进入后自动设置")]
        [Tooltip("进入该状态后自动设置的支持标记（位标记）。当前不会自动应用，供外部系统使用。")]
        public StateSupportFlags setSupportFlagsOnEnter = StateSupportFlags.None;

        [BoxGroup("支持标记")]
        [LabelText("进入后自动清除")]
        [Tooltip("进入该状态后自动清除的支持标记（位标记）。当前不会自动应用，供外部系统使用。")]
        public StateSupportFlags clearSupportFlagsOnEnter = StateSupportFlags.None;

        [BoxGroup("动画混合配置")]
        [LabelText("使用直接混合（无淡入淡出）")]
        [Tooltip("启用后动画立即切换到目标权重，不进行平滑过渡。适用于表情、UI反馈等需要即时响应的动画")]
        public bool useDirectBlend = false;

        [BoxGroup("动画混合配置")]
        [LabelText("Avatar Mask（可选）"), AssetsOnly]
        [Tooltip("指定Avatar Mask来控制动画影响的骨骼范围。\n常用场景：\n- 上半身动作：攻击/换弹仅影响上半身\n- 下半身动作：移动/跳跃仅影响下半身\n- 左手/右手分离控制")]
        public AvatarMask avatarMask = null;

        [BoxGroup("主状态判据")]
        [LabelText("主状态判据类型")]
        [Tooltip("主状态判据：直接权重优先 / 依赖代价计算 / 动态运行时评估。")]
        public MainStateCriterionType mainStateCriterion = MainStateCriterionType.DirectWeight;

        [BoxGroup("主状态判据")]
        [LabelText("直接权重(推荐1,范围0-5)")]
        [Tooltip("主状态判据为“直接权重”时使用；用于初始化阶段预备主状态判据计算与保留。")]
        [Range(0, 5)]
        public byte directMainWeight = 1;

        [BoxGroup("主状态判据")]
        [LabelText("预备主状态判据值(只读)"), ReadOnly]
        [Tooltip("初始化阶段基于主状态判据类型计算出的预备值（用于享元缓存）。")]
        public float preparedMainCriterionValue = 0f;

        [BoxGroup("生命周期配置")]
        [HorizontalGroup("生命周期配置/Duration")]
        [LabelText("持续时间模式")]
        [Tooltip("无限：永久持续 | 按动画结束：跟随动画长度 | 定时：指定固定时长")]
        public StateDurationMode durationMode = StateDurationMode.UntilAnimationEnd;

        [HorizontalGroup("生命周期配置/Duration")]
        [LabelText("定时时长(秒)"), MinValue(0)]
        [ShowIf("@durationMode == StateDurationMode.Timed")]
        public float timedDuration = 1f;

        [BoxGroup("生命周期配置")]
        [LabelText("运行时阶段配置")]
        [InlineProperty, HideLabel]
        public StatePhaseConfig phaseConfig = new StatePhaseConfig();

        /// <summary>
        /// 验证并修正配置（编辑器与运行时可调用）。
        /// 会：
        /// - 确保 <see cref="timedDuration"/> 非负
        /// - 保证 <see cref="phaseConfig.returnStartTime"/> <= <see cref="phaseConfig.releaseStartTime"/>
        /// - 将 <see cref="priority"/> 限制在 [0,255]
        /// </summary>
        public void ValidateAndFix()
        {
            if (timedDuration < 0f) timedDuration = 0f;

            // clamp priority
            if (priority < 0) priority = 0;
            if (priority > 255) priority = 255;

            // clamp direct main weight
            if (directMainWeight > 5) directMainWeight = 5;

            // ensure phase ordering
            if (phaseConfig != null)
            {
                if (phaseConfig.returnStartTime < 0f) phaseConfig.returnStartTime = 0f;
                if (phaseConfig.returnStartTime > 1f) phaseConfig.returnStartTime = 1f;
                if (phaseConfig.releaseStartTime < 0f) phaseConfig.releaseStartTime = 0f;
                if (phaseConfig.releaseStartTime > 1f) phaseConfig.releaseStartTime = 1f;

                if (phaseConfig.releaseStartTime < phaseConfig.returnStartTime)
                {
                    // 保持 release >= return，若不满足则将 release 调整为 return
                    phaseConfig.releaseStartTime = phaseConfig.returnStartTime;
                }

                if (phaseConfig.returnCostFraction < 0f) phaseConfig.returnCostFraction = 0f;
                if (phaseConfig.returnCostFraction > 1f) phaseConfig.returnCostFraction = 1f;
            }

            // ensure prepared value is non-negative
            if (preparedMainCriterionValue < 0f) preparedMainCriterionValue = 0f;
        }

        /// <summary>
        /// 依据主状态判据类型预备主判据值（用于享元缓存）。
        /// </summary>
        /// <param name="costData">状态代价数据（仅 CostBased 使用）</param>
        public void PrepareMainCriterionValue(StateCostData costData)
        {
            switch (mainStateCriterion)
            {
                case MainStateCriterionType.DirectWeight:
                    preparedMainCriterionValue = directMainWeight;
                    break;
                case MainStateCriterionType.CostBased:
                    if (costData != null && costData.enableCostCalculation)
                    {
                        preparedMainCriterionValue = costData.GetTotalCost();
                    }
                    else
                    {
                        preparedMainCriterionValue = 0f;
                    }
                    break;
                case MainStateCriterionType.Dynamic:
                default:
                    preparedMainCriterionValue = 0f;
                    break;
            }

            if (preparedMainCriterionValue < 0f) preparedMainCriterionValue = 0f;
        }

        /// <summary>
        /// 运行时初始化 - 执行验证和预备计算
        /// </summary>
        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            
            ValidateAndFix();
            phaseConfig?.InitializeRuntime();
            
            _isRuntimeInitialized = true;
        }
    }


    /// <summary>
    /// 状态阶段配置
    /// </summary>
    [Serializable]
    public class StatePhaseConfig : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;
        [LabelText("返还阶段开始时间(归一化)"), Range(0, 1)]
        [Tooltip("达到此时间点进入返还阶段，可以开始额外容纳其他动作")]
        public float returnStartTime = 0.7f;

        [LabelText("释放阶段开始时间(归一化)"), Range(0, 1)]
        [Tooltip("达到此时间点进入释放阶段，状态不再占据但动画可能未完")]
        public float releaseStartTime = 0.9f;

        [LabelText("返还代价比例"), Range(0, 1)]
        [Tooltip("返还阶段返还多少比例的代价")]
        public float returnCostFraction = 0.5f;

        /// <summary>
        /// 运行时初始化
        /// </summary>
        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            _isRuntimeInitialized = true;
        }
    }

    /// <summary>
    /// 主状态判据类型
    /// </summary>
    public enum MainStateCriterionType
    {
        /// <summary>
        /// 直接设置权重（推荐，0-5）
        /// </summary>
        [InspectorName("直接权重")]
        DirectWeight = 0,

        /// <summary>
        /// 依赖代价计算（静态/可预估）
        /// </summary>
        [InspectorName("依赖代价计算")]
        CostBased = 1,

        /// <summary>
        /// 动态运行时评估
        /// </summary>
        [InspectorName("动态评估")]
        Dynamic = 2
    }

}
