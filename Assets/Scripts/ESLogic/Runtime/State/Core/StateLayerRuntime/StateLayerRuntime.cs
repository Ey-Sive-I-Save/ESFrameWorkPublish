using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    /// <summary>
    /// 层级运行时数据 - 管理单个层级中的状态
    /// </summary>
    public partial class StateLayerRuntime
    {
        [NonSerialized] public StateMachine stateMachine;

        // ---- 初始化配置（只读）----
        [BoxGroup("基础信息", ShowLabel = true)]
        [LabelText("层级类型"), ReadOnly]
        public StateLayerType layerType;

        [BoxGroup("基础信息")]
        [LabelText("AvatarMask"), ReadOnly]
        public AvatarMask avatarMask;

        [BoxGroup("基础信息")]
        [LabelText("混合模式"), ReadOnly]
        public StateLayerBlendMode blendMode = StateLayerBlendMode.Override;

        [BoxGroup("基础信息")]
        [LabelText("允许状态遮罩覆盖"), ReadOnly]
        public bool allowStateMaskOverride;

        [BoxGroup("基础信息")]
        [LabelText("最大动画槽位数"), ReadOnly]
        public int maxPlayableSlots = 32;

        // ---- 运行时权重/开关（由 StateMachine 控制，只读）----
        [BoxGroup("运行状态", ShowLabel = true)]
        [LabelText("层级权重"), Range(0f, 1f), ReadOnly]
        public float weight = 1f;

        [BoxGroup("运行状态")]
        [LabelText("是否启用"), ReadOnly]
        public bool isEnabled = true;

        // ---- 运行时状态（只读调试）----
        [BoxGroup("运行状态")]
        [LabelText("回退状态"), ShowInInspector, ReadOnly]
        public StateBase feedbackState;

        [FoldoutGroup("高级运行数据", expanded: false)]
        [LabelText("当前运行状态集合"), ShowInInspector, ReadOnly]
        [NonSerialized] public SwapBackSet<StateBase> runningStates = new SwapBackSet<StateBase>(16);

        [FoldoutGroup("高级运行数据", expanded: false)]
        [LabelText("动画槽位映射"), ShowInInspector, ReadOnly]
        [NonSerialized] public Dictionary<StateBase, int> stateToSlotMap = new Dictionary<StateBase, int>(64);

        // ---- 内部 Playable / 淡入淡出（隐藏）----
        [NonSerialized] public AnimationMixerPlayable mixer;
        [NonSerialized] public AnimationClipPlayable referencePosePlayable;
        [NonSerialized] public bool hasReferencePose;
        [NonSerialized] public bool referencePoseWeightsNormalized;
        [NonSerialized] public int rootInputIndex = -1;
        [NonSerialized] public Stack<int> freeSlots = new Stack<int>(64);
        [NonSerialized] public Dictionary<StateBase, StateFadeData> fadeInStates = new Dictionary<StateBase, StateFadeData>();
        [NonSerialized] public Dictionary<StateBase, StateFadeData> fadeOutStates = new Dictionary<StateBase, StateFadeData>();
        [NonSerialized] public List<StateBase> fadeInToRemoveCache = new List<StateBase>(8);
        [NonSerialized] public List<StateBase> fadeOutToRemoveCache = new List<StateBase>(8);
        [NonSerialized] private readonly List<KeyValuePair<StateBase, int>> _stateToSlotListCache = new List<KeyValuePair<StateBase, int>>(64);
        /// <summary>上一次写入 RootMixer 的层级权重（用于阈值比较）</summary>
        [NonSerialized] internal float lastAppliedRootMixerWeight = float.NaN;
        public StateLayerRuntime(StateLayerType type, StateMachine machine)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (machine == null) throw new ArgumentNullException(nameof(machine));
#endif
            layerType = type;
            stateMachine = machine;
            runningStates = new SwapBackSet<StateBase>(16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private StateMachine GetStateMachineOrNull()
        {
            var machine = stateMachine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (machine == null) throw new InvalidOperationException("StateLayerRuntime.stateMachine 不能为空");
#endif
            return machine;
        }

        public void UpdateLayerMixer()
        {
            var machine = GetStateMachineOrNull();
            if (machine == null || !machine.IsPlayableGraphValid) return;

            var rootMixer = machine.rootMixer;
            if (!rootMixer.IsValid()) return;

            if (rootInputIndex >= 0 && rootInputIndex < rootMixer.GetInputCount())
            {
                if (Mathf.Abs(lastAppliedRootMixerWeight - weight) > 0.0001f)
                {
                    rootMixer.SetInputWeight(rootInputIndex, weight);
                    lastAppliedRootMixerWeight = weight;
                }
            }
        }

        public float GetStateWeight(StateBase state) => state != null ? state.PlayableWeight : 0f;

        public bool ActivateState(StateBase state)
        {
            if (state == null) return false;
            var machine = GetStateMachineOrNull();
            if (machine == null) return false;
            if (runningStates.Contains(state)) return false;

            runningStates.Add(state);
            machine.InternalAddRunningState(state);

            if (machine.IsPlayableGraphValid && mixer.IsValid())
                machine.HotPlugStateToPlayable(state, this);

            return true;
        }

        public bool DeactivateState(StateBase state)
        {
            if (state == null) return false;
            var machine = GetStateMachineOrNull();
            if (machine == null) return false;
            if (!runningStates.Contains(state)) return false;

            if (machine.IsPlayableGraphValid && mixer.IsValid())
                machine.HotUnplugStateFromPlayable(state, this);

            runningStates.Remove(state);
            machine.InternalRemoveRunningState(state);
            return true;
        }

        public bool HasActiveStates => runningStates.Count > 0;
    }
}

