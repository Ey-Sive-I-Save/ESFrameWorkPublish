using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{

    /// <summary>
    /// 状态基础配置 - 标识与生命周期
    /// </summary>
    [Serializable]
    public class StateBasicConfig
    {
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

        [LabelText("状态描述"), TextArea(2, 3)]
        public string description = "";

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
        }
    }


    /// <summary>
    /// 状态阶段配置
    /// </summary>
    [Serializable]
    public class StatePhaseConfig
    {
        [LabelText("返还阶段开始时间(归一化)"), Range(0, 1)]
        [Tooltip("达到此时间点进入返还阶段，可以开始额外容纳其他动作")]
        public float returnStartTime = 0.7f;

        [LabelText("释放阶段开始时间(归一化)"), Range(0, 1)]
        [Tooltip("达到此时间点进入释放阶段，状态不再占据但动画可能未完")]
        public float releaseStartTime = 0.9f;

        [LabelText("返还代价比例"), Range(0, 1)]
        [Tooltip("返还阶段返还多少比例的代价")]
        public float returnCostFraction = 0.5f;
    }

}
