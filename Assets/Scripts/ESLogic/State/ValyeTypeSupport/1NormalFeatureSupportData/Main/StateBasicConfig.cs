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
        [BoxGroup("标识", ShowLabel = true), PropertyOrder(0)]
        [LabelText("状态名称")]
        [InfoBox("优先配置：名称、层级、优先级", InfoMessageType.Info)]
        public string stateName = "新状态";

        [BoxGroup("标识", ShowLabel = true), PropertyOrder(0)]
        [LabelText("状态ID(重复时会被运行时Hash值顶掉)")]
        [Tooltip("用于唯一标识状态；重复ID会被运行时Hash覆盖。建议由资源导出或工具保证唯一性。")]
        public int stateId;

        [BoxGroup("标识", ShowLabel = true), PropertyOrder(0)]
        [LabelText("所属层级(重要！)")]
        public StateLayerType layerType = StateLayerType.Base;

        [BoxGroup("标识", ShowLabel = true), PropertyOrder(0)]
        [LabelText("默认优先级(仅作为最后判据)")]
        [Tooltip("优先级仅在所有其它判定无法决断时作为最终比较依据；建议范围0-255。")]
        [Range(0, 255)]
        public byte priority = 50;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("状态所处于的支持标记(无代表通用)")]
        public StateSupportFlags stateSupportFlag = StateSupportFlags.Grounded;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("忽略进入支持标记")]
        public bool ignoreSupportFlag = false;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("是否必须不允许激活于状态支持标记切换")]
        public bool disableActiveOnSupportFlagSwitching = false;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("支持ReStart")]
        public bool supportReStart = false;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("进入时重设支持标记")]
        public bool resetSupportFlagOnEnter = true;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("退出时移除支持标记")]
        public bool removeSupportFlagOnExit = false;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("可作为Fallback状态")]
        [Tooltip("勾选后该状态可被用作Fallback状态（如资源无匹配或失效时的兜底流转）。")]
        public bool canBeFeedback = false;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(2)]
        [LabelText("持续时间模式")]
        [Tooltip("无限：永久持续 | 按动画结束：跟随动画长度 | 定时：指定固定时长")]
        public StateDurationMode durationMode = StateDurationMode.UntilAnimationEnd;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(2)]
        [LabelText("定时时长(秒)"), MinValue(0)]
        [ShowIf("@durationMode == StateDurationMode.Timed")]
        public float timedDuration = 1f;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(2)]
        [LabelText("启用进度追踪")]
        [Tooltip("仅在需要阶段/事件/进度时开启，默认关闭以降低开销")]
        public bool enableProgressTracking = false;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(2)]
        [LabelText("启用Clip时长兜底")]
        [Tooltip("仅用于UntilAnimationEnd模式的兜底计算，默认关闭")]
        public bool enableClipLengthFallback = false;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(2)]
        [LabelText("运行时阶段配置")]
        [InlineProperty, HideLabel]
        public StatePhaseConfig phaseConfig = new StatePhaseConfig();

        [BoxGroup("混合", ShowLabel = true), PropertyOrder(3)]
        [LabelText("使用直接混合（无淡入淡出）")]
        [Tooltip("启用后动画立即切换到目标权重，不进行平滑过渡。适用于表情、UI反馈等需要即时响应的动画")]
        public bool useDirectBlend = false;

        [BoxGroup("混合", ShowLabel = true), PropertyOrder(3)]
        [LabelText("Avatar Mask（可选）"), AssetsOnly]
        [Tooltip("指定Avatar Mask来控制动画影响的骨骼范围。\n常用场景：\n- 上半身动作：攻击/换弹仅影响上半身\n- 下半身动作：移动/跳跃仅影响下半身\n- 左手/右手分离控制")]
        public AvatarMask avatarMask = null;

        [BoxGroup("说明", ShowLabel = true), PropertyOrder(4)]
        [LabelText("状态描述"), TextArea(2, 3)]
        public string description = "";

        /// <summary>
        /// 验证并修正配置（编辑器与运行时可调用）。
        /// 会：
        /// - 确保 <see cref="timedDuration"/> 非负
        /// - 保证 <see cref="phaseConfig.mainStartTime"/> <= <see cref="phaseConfig.waitStartTime"/>
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
                if (phaseConfig.mainStartTime < 0f) phaseConfig.mainStartTime = 0f;
                if (phaseConfig.mainStartTime > 1f) phaseConfig.mainStartTime = 1f;
                if (phaseConfig.waitStartTime < 0f) phaseConfig.waitStartTime = 0f;
                if (phaseConfig.waitStartTime > 1f) phaseConfig.waitStartTime = 1f;

                if (phaseConfig.waitStartTime < phaseConfig.mainStartTime)
                {
                    // 保持 release >= return，若不满足则将 release 调整为 return
                    phaseConfig.waitStartTime = phaseConfig.mainStartTime;
                }

            }
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
        [LabelText("启用阶段")]
        public bool enablePhase = false;

        [LabelText("启用按时间自动切换")]
        public bool enableAutoPhaseByTime = false;

        [LabelText("静默阶段覆盖")]
        [Tooltip("启用后忽略Pre/Wait/Released覆盖，仅使用Main(默认)配置")]
        public bool mutePhaseOverride = false;

        [LabelText("Main阶段开始时间(归一化)"), Range(0, 1)]
        [Tooltip("达到此时间点进入Main阶段")]
        [FormerlySerializedAs("returnStartTime")]
        public float mainStartTime = 0.7f;

        [LabelText("Wait阶段开始时间(归一化)"), Range(0, 1)]
        [Tooltip("达到此时间点进入Wait阶段（回收/衔接）")]
        [FormerlySerializedAs("releaseStartTime")]
        public float waitStartTime = 0.9f;

        [Title("Main阶段IK")]
        [LabelText("覆盖Main阶段IK目标权重")]
        [Tooltip("启用后：当状态运行时阶段进入 Main 时，强制把IK总目标权重设置为 mainIKTargetWeight（0=不影响/关闭IK，1=完全启用IK）。\n不启用时：Main阶段沿用默认IK目标权重，或由Pre/Wait/Released阶段覆盖决定。")]
        public bool overrideMainIK = false;

        [ShowIf("overrideMainIK"), Range(0f, 1f)]
        [Tooltip("Main阶段的IK总目标权重（会被平滑过渡到该值）。")]
        public float mainIKTargetWeight = 1f;

        [Title("阶段覆盖")]
        [LabelText("Pre阶段覆盖")]
        public StatePhaseOverrideConfig preOverride = new StatePhaseOverrideConfig();

        [LabelText("Wait阶段覆盖")]
        public StatePhaseOverrideConfig waitOverride = new StatePhaseOverrideConfig();

        [LabelText("Released阶段覆盖")]
        public StatePhaseOverrideConfig releasedOverride = new StatePhaseOverrideConfig();

        // 代价相关参数已移除

        /// <summary>
        /// 运行时初始化
        /// </summary>
        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            preOverride?.InitializeRuntime();
            waitOverride?.InitializeRuntime();
            releasedOverride?.InitializeRuntime();
            _isRuntimeInitialized = true;
        }

        public StateRuntimePhase EvaluatePhase(float normalizedProgress)
        {
            if (normalizedProgress < mainStartTime)
                return StateRuntimePhase.Pre;
            if (normalizedProgress < waitStartTime)
                return StateRuntimePhase.Main;
            return StateRuntimePhase.Wait;
        }
    }

    [Serializable]
    public class StatePhaseOverrideConfig : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;

        [LabelText("启用覆盖")]
        public bool enable = false;

        [LabelText("覆盖代价")]
        public bool overrideCost = false;

        [ShowIf("overrideCost")]
        [InlineProperty, HideLabel]
        public StateCostData costData = new StateCostData();

        [LabelText("覆盖冲突规则")]
        public bool overrideMerge = false;

        [ShowIf("overrideMerge")]
        [InlineProperty, HideLabel]
        public StateMergeData mergeData = new StateMergeData();

        [LabelText("覆盖优先级")]
        public bool overridePriority = false;

        [ShowIf("overridePriority"), Range(0, 255)]
        public byte priority = 50;

        [LabelText("覆盖IK目标权重")]
        public bool overrideIK = false;

        [ShowIf("overrideIK"), Range(0f, 1f)]
        public float ikTargetWeight = 1f;

        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            costData?.InitializeRuntime();
            mergeData?.InitializeRuntime();
            _isRuntimeInitialized = true;
        }
    }
}
