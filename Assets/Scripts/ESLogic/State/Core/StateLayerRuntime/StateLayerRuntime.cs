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

        [LabelText("层级类型")] public StateLayerType layerType;
        [LabelText("AvatarMask")] public AvatarMask avatarMask;
        [LabelText("混合模式")] public StateLayerBlendMode blendMode = StateLayerBlendMode.Override;
        [LabelText("允许状态Mask覆盖")] public bool allowStateMaskOverride;
        [LabelText("空转反馈状态"), ShowInInspector] public StateBase feedbackState;
        [LabelText("当前运行状态集合"), ShowInInspector, ReadOnly] [NonSerialized] public SwapBackSet<StateBase> runningStates = new SwapBackSet<StateBase>(16);
        [LabelText("层级权重"), Range(0f, 1f)] public float weight = 1f;
        [NonSerialized] internal float lastAppliedRootMixerWeight = float.NaN;
        [LabelText("是否启用")] public bool isEnabled = true;
        [LabelText("优先级"), Tooltip("数值越大优先级越高")] public byte priority;

        [NonSerialized] public AnimationMixerPlayable mixer;
        [NonSerialized] public AnimationClipPlayable referencePosePlayable;
        [NonSerialized] public bool hasReferencePose;
        [NonSerialized] public bool referencePoseWeightsNormalized;
        [NonSerialized] public int rootInputIndex = -1;

        [NonSerialized] public Stack<int> freeSlots = new Stack<int>(64);
        [NonSerialized, ShowInInspector] public Dictionary<StateBase, int> stateToSlotMap = new Dictionary<StateBase, int>(64);

        [NonSerialized] public Dictionary<StateBase, StateFadeData> fadeInStates = new Dictionary<StateBase, StateFadeData>();
        [NonSerialized] public Dictionary<StateBase, StateFadeData> fadeOutStates = new Dictionary<StateBase, StateFadeData>();
        [NonSerialized] public List<StateBase> fadeInToRemoveCache = new List<StateBase>(8);
        [NonSerialized] public List<StateBase> fadeOutToRemoveCache = new List<StateBase>(8);
        [NonSerialized] private readonly List<KeyValuePair<StateBase, int>> _stateToSlotListCache = new List<KeyValuePair<StateBase, int>>(64);

        [LabelText("最大Playable槽位")] public int maxPlayableSlots = 32;

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