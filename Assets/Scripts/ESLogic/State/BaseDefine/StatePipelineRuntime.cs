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
    public class StateLayerRuntime
    {
        /// <summary>
        /// 所属状态机引用
        /// </summary>
        [NonSerialized]
        public StateMachine stateMachine;

        [LabelText("层级类型")]
        public StateLayerType layerType;

        [LabelText("AvatarMask")]
        public AvatarMask avatarMask;

        [LabelText("混合模式")]
        public StateLayerBlendMode blendMode = StateLayerBlendMode.Override;

        [LabelText("允许状态Mask覆盖")]
        public bool allowStateMaskOverride = false;


        [LabelText("空转反馈状态"), ShowInInspector]
        public StateBase feedbackState;

        [LabelText("当前运行状态集合"), ShowInInspector, ReadOnly]
        [NonSerialized]
        public SwapBackSet<StateBase> runningStates = new SwapBackSet<StateBase>(16);

        [LabelText("层级权重"), Range(0f, 1f)]
        public float weight = 1f;

        [NonSerialized]
        internal float lastAppliedRootMixerWeight = float.NaN;

        [LabelText("是否启用")]
        public bool isEnabled = true;

        [LabelText("优先级"), Tooltip("数值越大优先级越高")]
        public byte priority = 0;

        /// <summary>
        /// Playable混合器 - 该层级的动画混合器
        /// </summary>
        [NonSerialized]
        public AnimationMixerPlayable mixer;

        /// <summary>
        /// ★ 参考姿态Playable - 防止bind pose导致角色下陷
        /// 始终占据mixer的slot 0，权重自动填充空缺
        /// </summary>
        [NonSerialized]
        public AnimationClipPlayable referencePosePlayable;

        /// <summary>
        /// 参考姿态是否已初始化
        /// </summary>
        [NonSerialized]
        public bool hasReferencePose;

        /// <summary>
        /// 参考姿态权重归一化标记。
        /// 当上一轮因为 intendedWeightSum &gt; 1 而对状态权重做了归一化缩放时，置为 true。
        /// 用于在 intendedWeightSum 回落到 &lt;= 1 时，仅在必要时恢复原始权重，避免每帧重复写回所有 state 权重。
        /// </summary>
        [NonSerialized]
        public bool referencePoseWeightsNormalized;

        /// <summary>
        /// 该层级在RootMixer中的输入索引
        /// </summary>
        [NonSerialized]
        public int rootInputIndex = -1;

        /// <summary>
        /// Playable槽位池 - 记录空闲的输入索引（用于复用）
        /// </summary>
        [NonSerialized]
        public Stack<int> freeSlots = new Stack<int>(64);

        /// <summary>
        /// 状态到输入索引的映射 - 用于快速查找和卸载
        /// </summary>
        [NonSerialized, ShowInInspector]
        public Dictionary<StateBase, int> stateToSlotMap = new Dictionary<StateBase, int>(64);

        // ===== 连接状态缓存（性能关键：替代每次 dirty 的 Dictionary 枚举） =====
        // 维护时机：HotPlug/HotUnplug/强制清理（低频），读取时机：UpdateMixerInputWeights（可能每帧/每 dirty）。
        [NonSerialized]
        internal readonly List<StateBase> connectedStates = new List<StateBase>(64);

        [NonSerialized]
        internal readonly List<int> connectedSlots = new List<int>(64);

        [NonSerialized]
        private readonly Dictionary<StateBase, int> _connectedIndexMap = new Dictionary<StateBase, int>(64);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InternalOnStateConnected(StateBase state, int slotIndex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (_connectedIndexMap.ContainsKey(state)) throw new InvalidOperationException($"State 已在 connectedStates 中: {state.strKey}");
#endif
            int idx = connectedStates.Count;
            connectedStates.Add(state);
            connectedSlots.Add(slotIndex);
            _connectedIndexMap[state] = idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InternalOnStateDisconnected(StateBase state)
        {
            if (state == null) return;
            if (!_connectedIndexMap.TryGetValue(state, out int idx)) return;

            int last = connectedStates.Count - 1;
            var lastState = connectedStates[last];
            var lastSlot = connectedSlots[last];

            connectedStates[idx] = lastState;
            connectedSlots[idx] = lastSlot;
            connectedStates.RemoveAt(last);
            connectedSlots.RemoveAt(last);

            _connectedIndexMap.Remove(state);
            if (idx != last)
            {
                _connectedIndexMap[lastState] = idx;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InternalClearConnectedStates()
        {
            connectedStates.Clear();
            connectedSlots.Clear();
            _connectedIndexMap.Clear();
        }

        /// <summary>
        /// 正在淡入的状态字典 - 用于淡入效果更新
        /// </summary>
        [NonSerialized]
        public Dictionary<StateBase, StateFadeData> fadeInStates = new Dictionary<StateBase, StateFadeData>();

        /// <summary>
        /// 正在淡出的状态字典 - 用于淡出效果更新
        /// </summary>
        [NonSerialized]
        public Dictionary<StateBase, StateFadeData> fadeOutStates = new Dictionary<StateBase, StateFadeData>();

        /// <summary>
        /// 淡入移除缓存（零GC）
        /// </summary>
        [NonSerialized]
        public List<StateBase> fadeInToRemoveCache = new List<StateBase>(8);

        /// <summary>
        /// 淡出移除缓存（零GC）
        /// </summary>
        [NonSerialized]
        public List<StateBase> fadeOutToRemoveCache = new List<StateBase>(8);

        /// <summary>
        /// 槽位映射排序缓存（零GC）
        /// </summary>
        [NonSerialized]
        private readonly List<KeyValuePair<StateBase, int>> _stateToSlotListCache = new List<KeyValuePair<StateBase, int>>(64);

        /// <summary>
        /// 最大预分配槽位数 - 避免无限增长
        /// </summary>
        [LabelText("最大Playable槽位")]
        public int maxPlayableSlots = 32;

        // ===== FallBack支持标记系统（每个层级独立配置）=====
        [LabelText("Grounded FallBack")]
        public int FallBackForGrounded = -1;
        [LabelText("Crouched FallBack")]
        public int FallBackForCrouched = -1;
        [LabelText("Prone FallBack")]
        public int FallBackForProne = -1;
        [LabelText("Swimming FallBack")]
        public int FallBackForSwimming = -1;
        [LabelText("Flying FallBack")]
        public int FallBackForFlying = -1;
        [LabelText("Mounted FallBack")]
        public int FallBackForMounted = -1;
        [LabelText("Climbing FallBack")]
        public int FallBackForClimbing = -1;
        [LabelText("SpecialInteraction FallBack")]
        public int FallBackForSpecialInteraction = -1;
        [LabelText("Observer FallBack")]
        public int FallBackForObserver = -1;
        [LabelText("Dead FallBack")]
        public int FallBackForDead = -1;
        [LabelText("Transition FallBack")]
        public int FallBackForTransition = -1;

        // ===== Dirty机制（层级级别的脏标记）=====
        /// <summary>
        /// 层级Dirty标记（独立运作，不再使用等级制）
        /// </summary>
        [LabelText("Dirty标记"), ShowInInspector, ReadOnly]
        [NonSerialized]
        public PipelineDirtyFlags dirtyFlags = PipelineDirtyFlags.None;

        /// <summary>
        /// 上次Dirty时间（用于自动衰减）
        /// </summary>
        [NonSerialized]
        private float lastDirtyTime = 0f;

        /// <summary>
        /// 是否为Dirty状态
        /// </summary>
        public bool IsDirty => dirtyFlags != PipelineDirtyFlags.None;

        /// <summary>
        /// 层级是否有活动状态
        /// </summary>
        public bool HasActiveStates => runningStates.Count > 0;

        // ===== Odin Inspector 实时调试显示 =====

    #if UNITY_EDITOR
        [NonSerialized]
        private bool _debugEnableMixerSlotWeights;

        [NonSerialized]
        private bool _debugMixerSlotWeightsDirty = true;

        [ShowInInspector, FoldoutGroup("Mixer权重调试"), LabelText("启用槽位权重列表(耗时)")]
        private bool DebugEnableMixerSlotWeights
        {
            get => _debugEnableMixerSlotWeights;
            set
            {
                if (_debugEnableMixerSlotWeights == value) return;
                _debugEnableMixerSlotWeights = value;
                _debugMixerSlotWeightsDirty = true;
            }
        }

        [NonSerialized]
        private readonly List<string> _debugMixerSlotWeightsCache = new List<string>(64);

        [NonSerialized]
        private StateBase[] _debugSlotToState;

        [NonSerialized]
        private bool[] _debugSlotFadingIn;

        [NonSerialized]
        private bool[] _debugSlotFadingOut;
    #endif

        /// <summary>
        /// 实时显示Mixer内所有槽位的权重，用于调试动画混合
        /// 显示格式：期望权重 → 实际Mixer权重，便于观察归一化效果
        /// </summary>
        [ShowInInspector, ReadOnly, FoldoutGroup("Mixer权重调试")]
        [LabelText("槽位权重列表")]
        private List<string> DebugMixerSlotWeights
        {
            get
            {
#if UNITY_EDITOR
                if (!_debugEnableMixerSlotWeights)
                {
                    _debugMixerSlotWeightsCache.Clear();
                    _debugMixerSlotWeightsCache.Add("(未启用) 勾选“启用槽位权重列表(耗时)”以显示");
                    return _debugMixerSlotWeightsCache;
                }

                if (!mixer.IsValid())
                {
                    _debugMixerSlotWeightsCache.Clear();
                    _debugMixerSlotWeightsCache.Add("Mixer无效");
                    _debugMixerSlotWeightsDirty = false;
                    return _debugMixerSlotWeightsCache;
                }

                int inputCount = mixer.GetInputCount();
                if (inputCount <= 0)
                {
                    _debugMixerSlotWeightsCache.Clear();
                    _debugMixerSlotWeightsCache.Add("(无输入槽位)");
                    _debugMixerSlotWeightsDirty = false;
                    return _debugMixerSlotWeightsCache;
                }

                // Inspector 重绘频率很高：仅在权重/槽位可能变化时重建字符串列表，避免每次重绘都产生 GC。
                bool hasActiveFades = fadeInStates.Count > 0 || fadeOutStates.Count > 0;
                bool hasRelevantDirty = (dirtyFlags & (PipelineDirtyFlags.MixerWeights | PipelineDirtyFlags.HotPlug)) != 0;
                if (!_debugMixerSlotWeightsDirty && !hasActiveFades && !hasRelevantDirty)
                {
                    return _debugMixerSlotWeightsCache;
                }

                _debugMixerSlotWeightsCache.Clear();

                if (_debugSlotToState == null || _debugSlotToState.Length < inputCount)
                {
                    _debugSlotToState = new StateBase[inputCount];
                    _debugSlotFadingIn = new bool[inputCount];
                    _debugSlotFadingOut = new bool[inputCount];
                }

                for (int i = 0; i < inputCount; i++)
                {
                    _debugSlotToState[i] = null;
                    _debugSlotFadingIn[i] = false;
                    _debugSlotFadingOut[i] = false;
                }

                foreach (var kvp in stateToSlotMap)
                {
                    int slotIndex = kvp.Value;
                    if ((uint)slotIndex >= (uint)inputCount) continue;

                    var state = kvp.Key;
                    _debugSlotToState[slotIndex] = state;
                    _debugSlotFadingIn[slotIndex] = state != null && fadeInStates.ContainsKey(state);
                    _debugSlotFadingOut[slotIndex] = state != null && fadeOutStates.ContainsKey(state);
                }

                for (int slot = 0; slot < inputCount; slot++)
                {
                    float mixerW = mixer.GetInputWeight(slot);
                    var state = _debugSlotToState[slot];

                    if (slot > 0 && state == null && mixerW <= 0f)
                    {
                        continue;
                    }

                    string stateName;
                    float intendedW;

                    if (slot == 0 && hasReferencePose)
                    {
                        stateName = "[参考姿态]";
                        intendedW = mixerW;
                    }
                    else if (state != null)
                    {
                        stateName = state.strKey;
                        intendedW = state.PlayableWeight;
                    }
                    else
                    {
                        stateName = "(空)";
                        intendedW = 0f;
                    }

                    string fadeTag = _debugSlotFadingIn[slot] ? " [淡入中]" : _debugSlotFadingOut[slot] ? " [淡出中]" : "";

                    bool isNormalized = slot > 0 && state != null && !Mathf.Approximately(intendedW, mixerW);
                    string weightStr = isNormalized ? $"{intendedW:F4}→{mixerW:F4}" : $"{mixerW:F4}";
                    string normTag = isNormalized ? " [已归一化]" : "";
                    _debugMixerSlotWeightsCache.Add($"Slot[{slot}] {weightStr} ← {stateName}{fadeTag}{normTag}");
                }

                _debugMixerSlotWeightsDirty = false;
                return _debugMixerSlotWeightsCache;
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// 期望权重总和（从状态对象读取，不受归一化影响）
        /// </summary>
        [ShowInInspector, ReadOnly, FoldoutGroup("Mixer权重调试")]
        [ShowIf("DebugEnableMixerSlotWeights")]
        [LabelText("期望权重总和"), PropertyOrder(1)]
        private float DebugIntendedWeightSum
        {
            get
            {
                // Inspector 重绘频繁：未启用时不做遍历。
#if UNITY_EDITOR
                if (!_debugEnableMixerSlotWeights) return 0f;
#endif
                float sum = 0f;
                foreach (var kvp in stateToSlotMap)
                    sum += kvp.Key.PlayableWeight;
                return sum;
            }
        }

        /// <summary>
        /// Mixer实际权重总和（不含参考姿态）
        /// </summary>
        [ShowInInspector, ReadOnly, FoldoutGroup("Mixer权重调试")]
        [ShowIf("DebugEnableMixerSlotWeights")]
        [LabelText("Mixer实际权重总和"), PropertyOrder(2)]
        private float DebugMixerActiveWeightSum
        {
            get
            {
#if UNITY_EDITOR
                if (!_debugEnableMixerSlotWeights) return 0f;
#endif
                if (!mixer.IsValid()) return 0f;
                int count = mixer.GetInputCount();
                float sum = 0f;
                int start = hasReferencePose ? 1 : 0;
                for (int i = start; i < count; i++)
                    sum += mixer.GetInputWeight(i);
                return sum;
            }
        }

        /// <summary>
        /// 参考姿态当前权重
        /// </summary>
        [ShowInInspector, ReadOnly, FoldoutGroup("Mixer权重调试")]
        [ShowIf("DebugEnableMixerSlotWeights")]
        [LabelText("参考姿态权重"), PropertyOrder(3)]
        private float DebugReferencePoseWeight
        {
            get
            {
#if UNITY_EDITOR
                if (!_debugEnableMixerSlotWeights) return -1f;
#endif
                if (!mixer.IsValid() || !hasReferencePose) return -1f;
                return mixer.GetInputWeight(0);
            }
        }

        /// <summary>
        /// Mixer所有输入的权重总和（含参考姿态），应始终≈1.0
        /// </summary>
        [ShowInInspector, ReadOnly, FoldoutGroup("Mixer权重调试")]
        [ShowIf("DebugEnableMixerSlotWeights")]
        [LabelText("总权重(含参考姿态)"), PropertyOrder(4)]
        private float DebugTotalWeight
        {
            get
            {
#if UNITY_EDITOR
                if (!_debugEnableMixerSlotWeights) return 0f;
#endif
                if (!mixer.IsValid()) return 0f;
                int count = mixer.GetInputCount();
                float sum = 0f;
                for (int i = 0; i < count; i++)
                    sum += mixer.GetInputWeight(i);
                return sum;
            }
        }

        /// <summary>
        /// 正在淡入/淡出的状态数量
        /// </summary>
        [ShowInInspector, ReadOnly, FoldoutGroup("Mixer权重调试")]
        [ShowIf("DebugEnableMixerSlotWeights")]
        [LabelText("淡入/淡出状态数"), PropertyOrder(5)]
        private string DebugFadeCount
        {
            get => $"淡入:{fadeInStates.Count}  淡出:{fadeOutStates.Count}";
        }

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
            if (machine == null) throw new InvalidOperationException("StateLayerRuntime.stateMachine 不能为空（应由构造函数注入）");
#endif
            return machine;
        }

        /// <summary>
        /// 更新层级权重到根部Mixer
        /// </summary>
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

        /// <summary>
        /// 获取指定状态在本层级Mixer中的当前权重
        /// </summary>
        public float GetStateWeight(StateBase state)
        {
            if (state == null) return 0f;
            return state.PlayableWeight;
        }

        /// <summary>
        /// 激活状态并加载到Mixer
        /// </summary>
        public bool ActivateState(StateBase state)
        {
            if (state == null) return false;
            var machine = GetStateMachineOrNull();
            if (machine == null) return false;

            if (runningStates.Contains(state)) return false;

            // 激活状态
            runningStates.Add(state);
            machine.InternalAddRunningState(state);

            // 热插拔到Playable图
            if (machine.IsPlayableGraphValid && mixer.IsValid())
            {
                machine.HotPlugStateToPlayable(state, this);
            }

            return true;
        }

        /// <summary>
        /// 停用状态并从 Mixer卸载
        /// </summary>
        public bool DeactivateState(StateBase state)
        {
            if (state == null) return false;
            var machine = GetStateMachineOrNull();
            if (machine == null) return false;

            if (!runningStates.Contains(state)) return false;

            // 热拔插从Playable图
            if (machine.IsPlayableGraphValid && mixer.IsValid())
            {
                machine.HotUnplugStateFromPlayable(state, this);
            }

            // 停用状态
            runningStates.Remove(state);
            machine.InternalRemoveRunningState(state);

            return true;
        }

        /// <summary>
        /// 获取指定支持标记的FallBack状态ID
        /// 当层级运行状态为空时，返回此状态ID作为默认回退状态
        /// </summary>
        /// <param name="supportFlag">支持标记（传 None 使用 StateMachine 当前SupportFlags）</param>
        /// <returns>FallBack状态ID，-1表示无FallBack</returns>
        public int GetFallBack(StateSupportFlags supportFlag = StateSupportFlags.None)
        {
            var originalFlag = supportFlag;
            if (supportFlag == StateSupportFlags.None)
            {
                var machine = GetStateMachineOrNull();
                supportFlag = machine != null ? machine.currentSupportFlags : StateSupportFlags.Grounded;
            }

            supportFlag = NormalizeSingleFlag(supportFlag);

            int result;
            switch (supportFlag)
            {
                case StateSupportFlags.Grounded: result = FallBackForGrounded; break;
                case StateSupportFlags.Crouched: result = FallBackForCrouched; break;
                case StateSupportFlags.Prone: result = FallBackForProne; break;
                case StateSupportFlags.Swimming: result = FallBackForSwimming; break;
                case StateSupportFlags.Flying: result = FallBackForFlying; break;
                case StateSupportFlags.Mounted: result = FallBackForMounted; break;
                case StateSupportFlags.Climbing: result = FallBackForClimbing; break;
                case StateSupportFlags.SpecialInteraction: result = FallBackForSpecialInteraction; break;
                case StateSupportFlags.Observer: result = FallBackForObserver; break;
                case StateSupportFlags.Dead: result = FallBackForDead; break;
                case StateSupportFlags.Transition: result = FallBackForTransition; break;
                default:
#if STATEMACHINEDEBUG
                        {
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null && dbg.IsFallbackEnabled)
                            {
                                dbg.LogFallback($"[FallBack-Get] ⚠ [{layerType}] SupportFlag无效({supportFlag})，回退到Grounded");
                            }
                        }
#endif
                        result = ResolveFallBackByFlag(StateSupportFlags.Grounded);
                    break;
            }

#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsFallbackEnabled)
                {
                    dbg.LogFallback($"[FallBack-Get] [{layerType}] GetFallBack(原始Flag={originalFlag}, 实际Flag={supportFlag}) -> StateID={result}");
                }
            }
#endif
            return result;
        }

        /// <summary>
        /// 设置指定支持标记的FallBack状态ID
        /// </summary>
        /// <param name="stateID">状态ID，-1表示无FallBack</param>
        /// <param name="supportFlag">支持标记（传 None 使用 StateMachine 当前SupportFlags）</param>
        public void SetFallBack(int stateID, StateSupportFlags supportFlag = StateSupportFlags.None)
        {
            var originalFlag = supportFlag;
            if (supportFlag == StateSupportFlags.None)
            {
                var machine = GetStateMachineOrNull();
                supportFlag = machine != null ? machine.currentSupportFlags : StateSupportFlags.Grounded;
            }

            supportFlag = NormalizeSingleFlag(supportFlag);

#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsFallbackEnabled)
                {
                    dbg.LogFallback($"[FallBack-Set] [{layerType}] SetFallBack(StateID={stateID}, 原始Flag={originalFlag}, 实际Flag={supportFlag})");
                }
            }
#endif

            switch (supportFlag)
            {
                case StateSupportFlags.Grounded: FallBackForGrounded = stateID; break;
                case StateSupportFlags.Crouched: FallBackForCrouched = stateID; break;
                case StateSupportFlags.Prone: FallBackForProne = stateID; break;
                case StateSupportFlags.Swimming: FallBackForSwimming = stateID; break;
                case StateSupportFlags.Flying: FallBackForFlying = stateID; break;
                case StateSupportFlags.Mounted: FallBackForMounted = stateID; break;
                case StateSupportFlags.Climbing: FallBackForClimbing = stateID; break;
                case StateSupportFlags.SpecialInteraction: FallBackForSpecialInteraction = stateID; break;
                case StateSupportFlags.Observer: FallBackForObserver = stateID; break;
                case StateSupportFlags.Dead: FallBackForDead = stateID; break;
                case StateSupportFlags.Transition: FallBackForTransition = stateID; break;
                default:
#if STATEMACHINEDEBUG
                    {
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null)
                        {
                            dbg.LogError($"[FallBack-Set] ✗ [{layerType}] 无效的SupportFlag: {supportFlag}");
                        }
                    }
#endif
                    break;
            }
        }

        /// <summary>
        /// 检查指定支持标记是否配置了FallBack状态
        /// </summary>
        public bool HasFallBack(StateSupportFlags supportFlag = StateSupportFlags.None)
        {
            return GetFallBack(supportFlag) >= 0;
        }

        private static StateSupportFlags NormalizeSingleFlag(StateSupportFlags flag)
        {
            if (flag == StateSupportFlags.None) return StateSupportFlags.None;

            ushort value = (ushort)flag;
            ushort lowest = (ushort)(value & (ushort)(-(short)value));
            return (StateSupportFlags)lowest;
        }

        private int ResolveFallBackByFlag(StateSupportFlags flag)
        {
            switch (flag)
            {
                case StateSupportFlags.Grounded: return FallBackForGrounded;
                case StateSupportFlags.Crouched: return FallBackForCrouched;
                case StateSupportFlags.Prone: return FallBackForProne;
                case StateSupportFlags.Swimming: return FallBackForSwimming;
                case StateSupportFlags.Flying: return FallBackForFlying;
                case StateSupportFlags.Mounted: return FallBackForMounted;
                case StateSupportFlags.Climbing: return FallBackForClimbing;
                case StateSupportFlags.SpecialInteraction: return FallBackForSpecialInteraction;
                case StateSupportFlags.Observer: return FallBackForObserver;
                case StateSupportFlags.Dead: return FallBackForDead;
                case StateSupportFlags.Transition: return FallBackForTransition;
                default: return FallBackForGrounded;
            }
        }

        /// <summary>
        /// 标记Dirty状态（独立Flag）
        /// </summary>
        public void MarkDirty(PipelineDirtyFlags flags)
        {
            if (flags == PipelineDirtyFlags.None) return;

            // 性能：MixerWeights 在同一帧内可能被多个 State 反复标记。
            // 该标记不参与 Dirty 衰减计时（仅 Medium/High 需要 lastDirtyTime），
            // 因此当目标位已存在时可以直接返回，避免重复读取 Time.time。
            var decayFlags = PipelineDirtyFlags.MediumPriority | PipelineDirtyFlags.HighPriority;
            bool touchesDecayTimer = (flags & decayFlags) != 0;

            var merged = dirtyFlags | flags;
            if (merged == dirtyFlags && !touchesDecayTimer)
            {
                return;
            }

            dirtyFlags = merged;
            if (touchesDecayTimer)
            {
                lastDirtyTime = Time.time;
            }

#if UNITY_EDITOR
            if ((flags & (PipelineDirtyFlags.MixerWeights | PipelineDirtyFlags.HotPlug)) != 0)
            {
                _debugMixerSlotWeightsDirty = true;
            }
#endif
#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsDirtyEnabled)
                {
                    dbg.LogDirty($"[Layer-Dirty] [{layerType}] Dirty添加: {flags} -> {dirtyFlags}");
                }
            }
#endif
        }

        /// <summary>
        /// 清除Dirty标记（不传入则清空全部）
        /// </summary>
        public void ClearDirty(PipelineDirtyFlags flags = PipelineDirtyFlags.None)
        {
            if (flags == PipelineDirtyFlags.None)
            {
                if (dirtyFlags == PipelineDirtyFlags.None) return;
#if STATEMACHINEDEBUG
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsDirtyEnabled)
                    {
                        dbg.LogDirty($"[Layer-Dirty] [{layerType}] Dirty已清空 (旧={dirtyFlags})");
                    }
                }
#endif
                dirtyFlags = PipelineDirtyFlags.None;
                return;
            }

            if ((dirtyFlags & flags) != 0)
            {
                dirtyFlags &= ~flags;
#if STATEMACHINEDEBUG
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsDirtyEnabled)
                    {
                        dbg.LogDirty($"[Layer-Dirty] [{layerType}] Dirty移除: {flags} -> {dirtyFlags}");
                    }
                }
#endif
            }
        }

        /// <summary>
        /// 是否包含指定Dirty标记
        /// </summary>
        public bool HasDirtyFlag(PipelineDirtyFlags flags)
        {
            return (dirtyFlags & flags) != 0;
        }

        /// <summary>
        /// 更新Dirty自动衰减（仅对中/高优先级降级为FallBack检查）
        /// </summary>
        public void UpdateDirtyDecay()
        {
            if (dirtyFlags == PipelineDirtyFlags.None) return;

            var decayFlags = PipelineDirtyFlags.MediumPriority | PipelineDirtyFlags.HighPriority;
            if ((dirtyFlags & decayFlags) == 0) return;

            float elapsed = Time.time - lastDirtyTime;
            if (elapsed >= 1.0f)
            {
                dirtyFlags &= ~decayFlags;
                dirtyFlags |= PipelineDirtyFlags.FallbackCheck;
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsDirtyEnabled)
                {
                    dbg.LogDirty($"[Layer-Dirty] [{layerType}] Dirty衰减 -> {dirtyFlags}");
                }
#endif
            }
        }

        /// <summary>
        /// 获取Mixer连接详细信息
        /// </summary>
        public string GetMixerConnectionInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"╔═══════════════════════════════════════════════════════════════╗");
            sb.AppendLine($"║  [{layerType}] 层级连接信息");
            sb.AppendLine($"╚═══════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            // 基本信息
            sb.AppendLine($"┌─ 基本配置 ────────────────────────────────────────────────┐");
            sb.AppendLine($"│  层级类型: {layerType}");
            sb.AppendLine($"│  权重: {weight:F3} | 启用: {(isEnabled ? "✓" : "✗")} | 优先级: {priority}");
            sb.AppendLine($"│  SupportFlag: Grounded={FallBackForGrounded}, Crouched={FallBackForCrouched}, Prone={FallBackForProne}, Swimming={FallBackForSwimming}, Flying={FallBackForFlying}, Mounted={FallBackForMounted}, Climbing={FallBackForClimbing}, SpecialInteraction={FallBackForSpecialInteraction}, Observer={FallBackForObserver}, Dead={FallBackForDead}, Transition={FallBackForTransition}");
            sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // RootMixer连接状态
            sb.AppendLine($"┌─ RootMixer连接 ───────────────────────────────────────────┐");
            if (stateMachine != null && stateMachine.rootMixer.IsValid())
            {
                var rootMixer = stateMachine.rootMixer;
                bool isConnected = rootInputIndex >= 0 && rootInputIndex < rootMixer.GetInputCount();
                sb.AppendLine($"│  状态: {(isConnected ? "✓ 已连接" : "✗ 未连接")}");
                sb.AppendLine($"│  索引: {rootInputIndex}");
                if (isConnected)
                {
                    float actualWeight = rootMixer.GetInputWeight(rootInputIndex);
                    sb.AppendLine($"│  实际权重: {actualWeight:F3} {(Mathf.Approximately(actualWeight, weight) ? "" : $"(配置: {weight:F3})")}");
                    var input = rootMixer.GetInput(rootInputIndex);
                    sb.AppendLine($"│  输入Playable: {(input.IsValid() ? $"有效 ({input.GetPlayableType().Name})" : "无效")}");
                }
            }
            else
            {
                sb.AppendLine($"│  状态: ✗ StateMachine或RootMixer无效");
            }
            sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // Mixer自身信息
            sb.AppendLine($"┌─ 层级Mixer ─────────────────────────────────────────────┐");
            if (mixer.IsValid())
            {
                sb.AppendLine($"│  状态: ✓ 有效");
                sb.AppendLine($"│  输入槽位数: {mixer.GetInputCount()} / {maxPlayableSlots} (最大)");
                sb.AppendLine($"│  已使用槽位: {stateToSlotMap.Count}");
                sb.AppendLine($"│  空闲槽位数: {freeSlots.Count}");
            }
            else
            {
                sb.AppendLine($"│  状态: ✗ 无效");
            }
            sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // 运行状态列表
            sb.AppendLine($"┌─ 运行状态 ({runningStates.Count}) ─────────────────────────────────────────┐");
            if (runningStates.Count > 0)
            {
                int index = 0;
                foreach (var state in runningStates)
                {
                    string prefix = index == runningStates.Count - 1 ? "└─" : "├─";
                    string stateInfo = $"{state.strKey} (ID:{state.intKey})";

                    // 检查是否有动画连接
                    bool hasAnimation;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (state == null) throw new InvalidOperationException($"[{layerType}] runningStates 包含 null StateBase（不允许）");
                    if (state.stateSharedData == null) throw new InvalidOperationException($"[{layerType}] State '{state.strKey}' 的 stateSharedData 为空（不允许）");
                    hasAnimation = state.stateSharedData.hasAnimation;
#else
                    if (state == null || state.stateSharedData == null)
                    {
                        hasAnimation = false;
                    }
                    else
                    {
                        hasAnimation = state.stateSharedData.hasAnimation;
                    }
#endif
                    string animStatus = hasAnimation ? "有动画" : "无动画";

                    // 检查槽位映射
                    string slotInfo = "";
                    if (hasAnimation && stateToSlotMap.TryGetValue(state, out int slot))
                    {
                        if (mixer.IsValid() && slot < mixer.GetInputCount())
                        {
                            float slotWeight = mixer.GetInputWeight(slot);
                            var slotInput = mixer.GetInput(slot);
                            slotInfo = $"→ Slot[{slot}] 权重:{slotWeight:F3} {(slotInput.IsValid() ? "✓" : "✗")}";
                        }
                        else
                        {
                            slotInfo = $"→ Slot[{slot}] ⚠无效";
                        }
                    }
                    else if (hasAnimation)
                    {
                        slotInfo = "→ ⚠未映射";
                    }

                    sb.AppendLine($"│ {prefix} {stateInfo} [{animStatus}] {slotInfo}");
                    index++;
                }
            }
            else
            {
                sb.AppendLine($"│  (空闲)");
            }
            sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // 槽位详细映射
            if (stateToSlotMap.Count > 0)
            {
                sb.AppendLine($"┌─ 槽位映射详情 ({stateToSlotMap.Count}) ──────────────────────────────────┐");
                var slotList = _stateToSlotListCache;
                slotList.Clear();
                foreach (var kvp in stateToSlotMap)
                {
                    slotList.Add(kvp);
                }
                slotList.Sort((a, b) => a.Value.CompareTo(b.Value));
                for (int i = 0; i < slotList.Count; i++)
                {
                    var kvp = slotList[i];
                    var state = kvp.Key;
                    var slot = kvp.Value;
                    string slotStatus = "✓";
                    string weightInfo = "";

                    if (mixer.IsValid() && slot < mixer.GetInputCount())
                    {
                        var input = mixer.GetInput(slot);
                        if (!input.IsValid())
                        {
                            slotStatus = "✗ 断开";
                        }
                        else
                        {
                            float w = mixer.GetInputWeight(slot);
                            weightInfo = $"权重:{w:F3}";
                        }
                    }
                    else
                    {
                        slotStatus = "⚠ 越界";
                    }

                    sb.AppendLine($"│  Slot[{slot,2}] {slotStatus} ← {state.strKey,-20} {weightInfo}");
                }
                sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
                sb.AppendLine();
            }

            // 空闲槽位池
            if (freeSlots.Count > 0)
            {
                sb.AppendLine($"┌─ 空闲槽位池 ({freeSlots.Count}) ──────────────────────────────────────┐");
                var freeArray = freeSlots.ToArray();
                sb.Append($"│  ");
                for (int i = 0; i < freeArray.Length; i++)
                {
                    sb.Append($"[{freeArray[i]}]");
                    if ((i + 1) % 10 == 0 && i < freeArray.Length - 1)
                    {
                        sb.AppendLine();
                        sb.Append($"│  ");
                    }
                    else if (i < freeArray.Length - 1)
                    {
                        sb.Append(" ");
                    }
                }
                sb.AppendLine();
                sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
            }

            return sb.ToString();
        }

#if UNITY_EDITOR
        [Button("输出Mixer连接信息", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugPrintMixerConnection()
        {
#if STATEMACHINEDEBUG
            UnityEngine.Debug.Log(GetMixerConnectionInfo());
#endif
        }
#endif
    }
}
