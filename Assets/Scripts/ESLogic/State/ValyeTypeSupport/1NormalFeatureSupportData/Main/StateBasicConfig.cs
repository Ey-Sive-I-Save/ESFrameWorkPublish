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
        [InfoBox("优先配置：名称、层级、混合偏置。混合偏置只影响同层 Mixer 的权重分配与最终排序；默认值为“标准”。", InfoMessageType.Info)]
        public string stateName = "新状态";

        [BoxGroup("标识", ShowLabel = true), PropertyOrder(0)]
        [LabelText("状态ID(重复时会被运行时Hash值顶掉)")]
        [Tooltip("用于唯一标识状态；重复ID会被运行时Hash覆盖。建议由资源导出或工具保证唯一性。")]
        public int stateId;

        [BoxGroup("标识", ShowLabel = true), PropertyOrder(0)]
        [LabelText("#状态所属层级")]
        [Tooltip(
            "定义该状态参与哪一条状态层的并行与覆盖计算，这是状态最核心的归属信息之一。" +
            "层级决定了它会和哪些状态互相混合、互相覆盖、互相竞争权重。" +
            "通常应把它理解为“这条状态属于身体哪一层语义管线”，而不是简单的显示分组。" +
            "选错层级会直接导致动画覆盖关系、Mixer 分配、IK 叠加和最终表现异常。" +
            "除非你非常确定该状态应该进入别的层，否则不要随意改动。")]
        public StateLayerType layerType = StateLayerType.Base;

        [BoxGroup("标识", ShowLabel = true), PropertyOrder(0)]
        [LabelText("#同层混合偏置")]
        [Tooltip(
            "用于控制同层多个状态同时参与 Mixer 时，谁更容易拿到更高权重。" +
            "它只影响两件事：1. 同层状态的权重分配；2. 最终排序时的同层偏置。" +
            "它不影响合并规则，不影响打断规则，也不影响跨层关系。" +
            "默认值为“标准”，适合绝大多数状态。" +
            "建议用法：背景 = 几乎只做陪衬；偏低 = 希望参与混合但尽量让位；标准 = 常规默认；偏高 = 希望在同层混合中更主动；关键 = 必须明显压过同层普通状态。" +
            "使用 5 档离散值而不是旧的 0-255，可避免难以理解和难以维护的微调。")]
        public StateMixerBias mixerBias = StateMixerBias.Normal;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("#状态适用环境")]
        [Tooltip(
            "定义该状态默认适用于哪一种角色环境或运动语义，例如地面、游泳、飞行、攀爬。" +
            "状态机在激活、保留、Fallback 和支持标记切换时，都会把它作为重要约束条件。" +
            "如果这里填写了某个明确标记，通常表示这个状态只应该在该环境下成立；如果为无，则表示它更偏向通用状态。" +
            "这个字段本质上是“状态的适用环境声明”，不是临时运行时开关。")]
        public StateSupportFlags stateSupportFlag = StateSupportFlags.Grounded;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("#忽略入场环境")]
        [Tooltip(
            "开启后，状态在尝试进入时不再严格检查当前角色的支持标记是否与 stateSupportFlag 一致。" +
            "适合那些需要跨环境强制打入的状态，例如某些过渡表现、强制演出、特殊控制态。" +
            "关闭时，状态进入会遵守支持标记约束，更安全，也更符合常规语义。" +
            "注意：这只影响“进入时”的检查，不代表进入后所有支持标记逻辑都被完全忽略。")]
        public bool ignoreSupportFlag = false;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("#切换时禁激活")]
        [Tooltip(
            "该字段控制的是“激活判定”，不是“已激活状态的保留判定”。" +
            "开启后，当当前支持标记与本状态的 stateSupportFlag 不匹配，且本次属于支持标记切换场景时，状态会被禁止激活。" +
            "也就是说，它决定的是“切换过程中能不能打进来”。" +
            "关闭后，则允许在某些支持标记切换窗口里继续尝试激活，是否最终允许还会结合其他支持标记转移规则判断。" +
            "适合那些强依赖环境、在错误支持标记下绝不能被激活的状态。" 
            )]
        public bool disableActiveOnSupportFlagSwitching = false;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("#切换后自动失活")]
        [Tooltip(
            "该字段控制的是“已经在运行中的状态”，不是激活判定。" +
            "开启后，当状态机当前支持标记切换到与本状态 stateSupportFlag 不再匹配时，该状态会被自动失活。" +
            "例如一个只适用于 Grounded 的状态，在角色切到 Swimming、Flying、Climbing 后，会被系统主动退出，避免状态残留在错误环境中。" +
            "关闭后，即使支持标记已经不匹配，状态也允许继续保留，适合过渡态、表现态、桥接态，或你明确希望跨支持标记短暂延续的状态。" +
            "默认开启，是为了保持当前框架已有的支持标记切换后清理行为。" +
            "前缀 # 表示该字段必须先阅读 Tooltip 再配置。")]
        public bool deactivateOnSupportFlagSwitching = true;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("#支持重复激活")]
        [Tooltip(
            "定义当该状态已经处于运行中时，是否允许再次请求进入并执行一次“重新开始”。" +
            "开启后，同一个状态重复激活时可以重置自身进入逻辑、重新计算动画或重新触发关键效果。" +
            "关闭时，重复进入请求通常会被视为无效或被忽略，更适合那些一旦运行就应保持连续性的状态。" +
            "这个字段决定的是“同状态重复激活”的行为策略，不是普通的状态切换能力。")]
        public bool supportReStart = false;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("#入场时重设标记")]
        [Tooltip(
            "开启后，状态进入时会主动把状态机当前支持标记重设为本状态声明的 stateSupportFlag。" +
            "适合那些进入后就应该明确切换宿主语义的状态，例如进入游泳态后，整体环境语义应立即变成 Swimming。" +
            "关闭时，状态进入不会主动重写当前支持标记，更适合只消费环境、不定义环境的状态。" +
            "如果你不希望一个状态进入后改写整个状态机的环境语义，就不要开启它。")]
        public bool resetSupportFlagOnEnter = true;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("#退出时移除标记")]
        [Tooltip(
            "开启后，状态退出时会把自己对应的支持标记从当前状态机语义中移除。" +
            "适合那些退出后应明确结束某种环境语义占用的状态。" +
            "关闭时，退出不会主动清理支持标记，通常依赖其他状态进入或外部系统重新设置。" +
            "如果环境语义是由多个系统共同维护的，这个开关就需要谨慎使用，否则容易在退出时把仍然有效的标记一起清掉。")]
        public bool removeSupportFlagOnExit = false;

        [BoxGroup("支持标记", ShowLabel = true), PropertyOrder(1)]
        [LabelText("#允许作为Fallback")]
        [Tooltip(
            "勾选后，该状态允许被系统当作 Fallback 状态使用。" +
            "Fallback 的含义是：当某层没有更合适的可运行状态、资源失配、条件落空，或系统需要一个稳定兜底时，可以自动转入该状态。" +
            "因此它更适合那些安全、稳定、可长时间停留、不会产生副作用的状态。" +
            "不要把强动作、一次性技能、强依赖目标的状态设成 Fallback，否则兜底逻辑会变得危险。")]
        public bool canBeFeedback = false;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(2)]
        [LabelText("#状态自然持续模式")]
        [Tooltip(
            "定义该状态在没有被外部打断时，应以什么规则判定自己何时自然结束。" +
            "无限 = 会一直保持，直到外部明确退出；按动画结束 = 以当前动画播放完毕作为自动退出依据；定时 = 以 timedDuration 指定的固定时长为准。" +
            "这是状态生命周期的主规则之一，会直接影响自动退出、临时状态行为和动画驱动逻辑。" +
            "如果你不确定，常规表现状态优先考虑“按动画结束”，持续控制态再考虑“无限”，强设计时长的状态再用“定时”。")]
        public StateDurationMode durationMode = StateDurationMode.UntilAnimationEnd;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(2)]
        [LabelText("定时时长(秒)"), MinValue(0)]
        [ShowIf("@durationMode == StateDurationMode.Timed")]
        public float timedDuration = 1f;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(2)]
        [LabelText("#启用运行时进度")]
        [Tooltip(
            "开启后，状态会持续计算运行时动画进度，包括 normalizedProgress、totalProgress 和 loopCount。" +
            "所有依赖动画进度的逻辑，例如分段 IK、分段 LookAt、按进度触发的时序行为与后续可能接入的事件机制，都依赖它。" +
            "关闭后可以少做一部分运行时进度计算，但所有依赖动画进度的功能都会失效、退化，或长期停留在初始进度。" +
            "只有当该状态完全不依赖动画进度驱动时才建议关闭。")]
        public bool enableRuntimeProgress = false;

        [BoxGroup("生命周期", ShowLabel = true), PropertyOrder(2)]
        [LabelText("启用Clip时长兜底")]
        [Tooltip("仅用于UntilAnimationEnd模式的兜底计算，默认关闭")]
        public bool enableClipLengthFallback = false;

        [BoxGroup("说明", ShowLabel = true), PropertyOrder(4)]
        [LabelText("内部备注"), TextArea(2, 3)]
        [Tooltip("仅供策划、程序或美术在资产上记录说明，不参与运行时逻辑、状态判断、导出显示名或 UI 展示链路。" +
             "如果你需要给编辑器列表、预设说明或展示层使用，请填写 StateSharedData 里的 description，而不是这里。")]
        [FormerlySerializedAs("description")]
        public string internalNote = "";

        /// <summary>
        /// 验证并修正配置（编辑器与运行时可调用）。
        /// 会：
        /// - 确保 <see cref="timedDuration"/> 非负
        /// </summary>
        public void ValidateAndFix()
        {
            if (timedDuration < 0f) timedDuration = 0f;
        }

        /// <summary>
        /// 运行时初始化 - 执行验证和预备计算
        /// </summary>
        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;

            ValidateAndFix();
            _isRuntimeInitialized = true;
        }
    }
}
