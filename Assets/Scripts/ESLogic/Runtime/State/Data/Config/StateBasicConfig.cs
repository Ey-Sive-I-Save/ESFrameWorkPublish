using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace ES
{
    /// <summary>
    /// 状态基础配置：只放身份、层级、支持环境和生命周期。
    /// 展示名/描述请放在 StateSharedData，避免运行身份和编辑器展示混在一起。
    /// </summary>
    [Serializable]
    public class StateBasicConfig : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;

        [BoxGroup("身份", ShowLabel = true), PropertyOrder(0)]
        [LabelText("状态名")]
        [InfoBox("优先配置状态名、层级和同层混合偏置。状态名是运行时默认 StringKey，不是 UI 展示名。", InfoMessageType.Info)]
        public string stateName = "新状态";

        [BoxGroup("身份", ShowLabel = true), PropertyOrder(0)]
        [LabelText("状态ID")]
        [Tooltip("用于唯一标识状态。重复 ID 可能被运行时 Hash 逻辑覆盖，建议由工具或导出流程保证唯一。")]
        public int stateId;

        [BoxGroup("身份", ShowLabel = true), PropertyOrder(0)]
        [LabelText("状态层级")]
        [Tooltip("决定状态进入哪一条状态层参与并行、覆盖和混合。它不是显示分组，选错会直接影响动画混合、IK 叠加和状态竞争。")]
        public StateLayerType layerType = StateLayerType.Base;

        [BoxGroup("身份", ShowLabel = true), PropertyOrder(0)]
        [LabelText("同层混合偏置")]
        [Tooltip("控制同层多个状态混合时谁更容易获得权重。默认用“标准”；不要把它当成打断优先级。")]
        public StateMixerBias mixerBias = StateMixerBias.Normal;

        [BoxGroup("支持环境", ShowLabel = true), PropertyOrder(1)]
        [LabelText("适用环境")]
        [Tooltip("声明该状态默认适用于哪种角色环境或运动语义，例如地面、游泳、飞行、攀爬。用于激活、保留、Fallback 和环境切换判断。")]
        public StateSupportFlags stateSupportFlag = StateSupportFlags.Grounded;

        [BoxGroup("支持环境", ShowLabel = true), PropertyOrder(1)]
        [LabelText("忽略入场环境检查")]
        [Tooltip("开启后，状态尝试进入时不严格检查当前环境是否匹配适用环境。适合强制演出或跨环境过渡状态。")]
        public bool ignoreSupportFlag = false;

        [BoxGroup("支持环境", ShowLabel = true), PropertyOrder(1)]
        [LabelText("环境切换时禁止进入")]
        [Tooltip("开启后，当当前环境和本状态适用环境不匹配，并且本次属于环境切换场景时，禁止该状态被激活。")]
        public bool disableActiveOnSupportFlagSwitching = false;

        [BoxGroup("支持环境", ShowLabel = true), PropertyOrder(1)]
        [LabelText("环境切换后自动退出")]
        [Tooltip("开启后，如果状态运行中环境切换到不再匹配本状态适用环境，该状态会自动退出。")]
        public bool deactivateOnSupportFlagSwitching = true;

        [BoxGroup("激活策略", ShowLabel = true), PropertyOrder(2)]
        [LabelText("允许重复进入")]
        [Tooltip("开启后，同一个状态已经运行时仍可再次请求进入，并触发重新开始逻辑。适合攻击、受击、短技能等状态。")]
        public bool supportReStart = false;

        [BoxGroup("激活策略", ShowLabel = true), PropertyOrder(2)]
        [LabelText("入场时重设环境")]
        [Tooltip("开启后，状态进入时会把状态机当前环境重设为本状态声明的适用环境。只有明确需要改写整体环境语义时再开启。")]
        public bool resetSupportFlagOnEnter = true;

        [BoxGroup("激活策略", ShowLabel = true), PropertyOrder(2)]
        [LabelText("退出时移除环境")]
        [Tooltip("开启后，状态退出时会从当前状态机环境语义中移除本状态声明的适用环境。多系统共同维护环境时需谨慎。")]
        public bool removeSupportFlagOnExit = false;

        [BoxGroup("激活策略", ShowLabel = true), PropertyOrder(2)]
        [LabelText("允许作为兜底状态")]
        [Tooltip("允许系统在没有更合适状态时把该状态作为 Fallback。适合安全、稳定、可长时间停留的状态。")]
        public bool canBeFeedback = false;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(3)]
        [LabelText("自然结束模式")]
        [Tooltip("决定状态在没有外部打断时如何自然结束：无限、按动画结束、或固定时长。")]
        public StateDurationMode durationMode = StateDurationMode.UntilAnimationEnd;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(3)]
        [LabelText("固定时长"), MinValue(0), SuffixLabel("秒", Overlay = true)]
        [ShowIf("@durationMode == StateDurationMode.Timed")]
        public float timedDuration = 1f;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(3)]
        [LabelText("计算运行进度")]
        [Tooltip("开启后计算 normalizedProgress、totalProgress 和 loopCount。分段 IK、按进度触发的时序逻辑会依赖它。")]
        public bool enableRuntimeProgress = false;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(3)]
        [LabelText("动画时长兜底")]
        [Tooltip("仅用于“按动画结束”模式下的兜底时长计算。默认关闭。")]
        public bool enableClipLengthFallback = false;

        [BoxGroup("说明", ShowLabel = true), PropertyOrder(4)]
        [LabelText("内部备注"), TextArea(2, 3)]
        [Tooltip("仅供策划、程序或美术在资产上记录，不参与运行时逻辑。展示名和公开描述请填写 StateSharedData 的 displayName / description。")]
        [FormerlySerializedAs("description")]
        public string internalNote = "";

        public void ValidateAndFix()
        {
            if (timedDuration < 0f) timedDuration = 0f;
        }

        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;

            ValidateAndFix();
            _isRuntimeInitialized = true;
        }
    }
}
