using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    /// <summary>
    /// 状态机基类 - 专为Entity提供的高性能并行状态管理系统。
    /// 设计思路参考UE状态机，支持流水线、并行状态、动画混合等高级特性。
    /// 核心逻辑默认稳定可用，扩展点通过回调/配置开放，避免子类侵入式重写。
    /// </summary>
    [Serializable, TypeRegistryItem("ES状态机")]
    public class StateMachine
    {
        #region 核心引用与宿主（核心/不建议改）

        /// <summary>
        /// 宿主Entity - 状态机所属的实体对象
        /// </summary>
        [NonSerialized]
        public Entity hostEntity;

        /// <summary>
        /// 状态机唯一标识键
        /// </summary>
        [LabelText("状态机键"), ShowInInspector]
        public string stateMachineKey;

        /// <summary>
        /// 状态机配置（可拖入，编辑器下空则使用全局Instance）
        /// </summary>
        [LabelText("状态机配置")]
        public StateMachineConfig config;

        /// <summary>
        /// 状态上下文 - 统一管理运行时数据、参数、标记等（整合了原StateMachineContext）
        /// </summary>
        [LabelText("状态上下文"), ShowInInspector]
        [NonSerialized]
        public StateMachineContext stateContext;

        /// <summary>
        /// 是否持续输出统计信息（用于调试）
        /// </summary>
        [LabelText("持续输出统计"), Tooltip("每帧在Console输出状态机统计信息")]
        [NonSerialized]
        public bool enableContinuousStats = false;

#if UNITY_EDITOR
        [OnInspectorInit]
        private void EditorInitConfig()
        {
            if (config == null)
            {
                config = StateMachineConfig.Instance;
            }
        }
#endif

        #endregion

        #region 扩展回调与策略（可修改）

        /// <summary>
        /// 自定义激活测试（若返回非默认值将覆盖内置逻辑）
        /// </summary>
        public Func<StateBase, StatePipelineType, StateActivationResult> CustomActivationTest;

        /// <summary>
        /// 自定义合并判定
        /// </summary>
        public Func<StateBase, StateBase, bool> CanMergeEvaluator;

        /// <summary>
        /// 状态进入回调
        /// </summary>
        public Action<StateBase, StatePipelineType> OnStateEntered;

        /// <summary>
        /// 状态退出回调
        /// </summary>
        public Action<StateBase, StatePipelineType> OnStateExited;

        /// <summary>
        /// 流水线初始化回调
        /// </summary>
        public Action<StatePipelineRuntime> OnPipelineInitialized;

        /// <summary>
        /// 自定义退出测试
        /// </summary>
        public Func<StateBase, StatePipelineType, StateExitResult> CustomExitTest;

        /// <summary>
        /// 动画事件回调（当状态触发动画事件时）
        /// </summary>
        public Action<StateBase, string, string> OnAnimationEvent;

        /// <summary>
        /// 自定义通道占用计算
        /// </summary>
        public Func<IEnumerable<StateBase>, StateChannelMask> CustomChannelMaskEvaluator;

        /// <summary>
        /// 自定义代价计算
        /// </summary>
        public Func<IEnumerable<StateBase>, StateCostSummary> CustomCostEvaluator;

        /// <summary>
        /// 自定义主状态评分（用于 Dynamic 判据）
        /// </summary>
        public Func<StateBase, float> CustomMainStateScore;

        #endregion

        #region 生命周期状态（核心/不建议改）

        /// <summary>
        /// 状态机是否正在运行
        /// </summary>
        [ShowInInspector, ReadOnly, LabelText("运行状态")]
        public bool isRunning { get; protected set; }

        /// <summary>
        /// 状态机是否已初始化
        /// </summary>
        [NonSerialized]
        protected bool isInitialized = false;

        #endregion

        #region 流水线与并行状态管理（核心/谨慎改）

        /// <summary>
        /// 所有运行中的状态集合 - 支持多状态并行
        /// </summary>
        [ShowInInspector, ReadOnly, LabelText("当前运行状态")]
        [NonSerialized]
        public HashSet<StateBase> runningStates = new HashSet<StateBase>();

        /// <summary>
        /// 当前实体支持标记（与实体KCC一致）
        /// </summary>
        [ShowInInspector, ReadOnly, LabelText("SupportFlags")]
        [NonSerialized]
        public StateSupportFlags currentSupportFlags = StateSupportFlags.Grounded;

        private const StateSupportFlags LocomotionMask = StateSupportFlags.Grounded | StateSupportFlags.Swimming | StateSupportFlags.Flying;

        public void SetSupportFlags(StateSupportFlags flags)
        {
            var beforeFlags = currentSupportFlags;
            currentSupportFlags = flags;
            if (beforeFlags != currentSupportFlags)
            {
                MarkSupportFlagsDirty();
            }
        }

        public void SetLocomotionSupportFlags(StateSupportFlags locomotionFlags)
        {
            var beforeFlags = currentSupportFlags;
            currentSupportFlags = (currentSupportFlags & ~LocomotionMask) | (locomotionFlags & LocomotionMask);
            if (beforeFlags != currentSupportFlags)
            {
                MarkSupportFlagsDirty();
            }
        }

        private void MarkSupportFlagsDirty()
        {
            basicPipeline.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            mainPipeline.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            buffPipeline.MarkDirty(PipelineDirtyFlags.FallbackCheck);
        }

        [NonSerialized]
        private int[] _supportFlagToRelationIndex;

        [NonSerialized]
        private ulong[] _disableTransitionMasks;

        /// <summary>
        /// String键到状态的映射
        /// </summary>
        [ShowInInspector, FoldoutGroup("状态字典"), LabelText("String映射")]
        [SerializeReference]
        public Dictionary<string, StateBase> stringToStateMap = new Dictionary<string, StateBase>();

        /// <summary>
        /// Int键到状态的映射
        /// </summary>
        [ShowInInspector, FoldoutGroup("状态字典"), LabelText("Int映射")]
        [SerializeReference]
        public Dictionary<int, StateBase> intToStateMap = new Dictionary<int, StateBase>();

        /// <summary>
        /// 状态归属流水线映射
        /// </summary>
        [ShowInInspector, FoldoutGroup("状态字典"), LabelText("状态管线映射")]
        [NonSerialized]
        public Dictionary<StateBase, StatePipelineType> statePipelineMap = new Dictionary<StateBase, StatePipelineType>();

        [NonSerialized]
        private readonly List<StateBase> _tmpStateBuffer = new List<StateBase>(16);

        [NonSerialized]
        private readonly List<StateBase> _tmpInterruptStates = new List<StateBase>(8);

        [NonSerialized]
        private readonly List<StateBase> _tmpMergeStates = new List<StateBase>(8);

        [NonSerialized]
        private readonly List<StateBase> _statesToDeactivateCache = new List<StateBase>(16);

        [NonSerialized]
        private readonly Dictionary<StateBase, StateActivationCache> _activationCache = new Dictionary<StateBase, StateActivationCache>(64);

        [NonSerialized]
        private readonly List<StateBase> _registeredStatesList = new List<StateBase>(256);

        [NonSerialized]
        private readonly List<StateBase> _cachedRunningStatesList = new List<StateBase>(32);

        [NonSerialized]
        private readonly List<string> _temporaryKeysCache = new List<string>(16);

        [NonSerialized]
        private readonly System.Text.StringBuilder _continuousStatsBuilder = new System.Text.StringBuilder(256);

        [NonSerialized]
        private int _cachedRunningStatesVersion = -1;

        [NonSerialized]
        private int _dirtyVersion = 0;

        [NonSerialized]
        private StateDirtyReason _lastDirtyReason = StateDirtyReason.Unknown;

        /// <summary>
        /// 自动分配ID的起始值（避免与预设ID冲突）
        /// </summary>
        [NonSerialized]
        private int _nextAutoIntId = 10000;

        [NonSerialized]
        private int _nextAutoStringIdSuffix = 1;

        [NonSerialized]
        private StateChannelMask _cachedChannelMask = StateChannelMask.None;

        [NonSerialized]
        private int _cachedChannelMaskVersion = -1;

        [NonSerialized]
        private StateCostSummary _cachedCostSummary;

        [NonSerialized]
        private int _cachedCostVersion = -1;

        /// <summary>
        /// 默认状态键 - 状态机启动时进入的状态
        /// </summary>
        [LabelText("默认状态键"), ValueDropdown("GetAllStateKeys")]
        public string defaultStateKey;

        #endregion

        #region 流水线声明与管理（核心/谨慎改）

        /// <summary>
        /// 基础流水线 - 基础状态层
        /// </summary>
        [ShowInInspector, LabelText("基础流水线")]
        protected StatePipelineRuntime basicPipeline;

        /// <summary>
        /// 主流水线 - 主要动作层
        /// </summary>
        [ShowInInspector, LabelText("主流水线")]
        protected StatePipelineRuntime mainPipeline;

        /// <summary>
        /// Buff流水线 - 增益/减益效果层
        /// </summary>
        [ShowInInspector, LabelText("Buff流水线")]
        protected StatePipelineRuntime buffPipeline;

        /// <summary>
        /// 流水线混合模式 - 控制Main线和Basic线如何混合
        /// </summary>
        [TitleGroup("流水线混合设置", Order = 1)]
        [LabelText("混合模式"), InfoBox("Override: Main覆盖Basic（推荐）\nAdditive: 权重叠加\nMultiplicative: Main调制Basic")]
        [EnumToggleButtons]
        public PipelineBlendMode pipelineBlendMode = PipelineBlendMode.Override;

        /// <summary>
        /// 通过枚举获取对应的流水线
        /// </summary>
        private StatePipelineRuntime GetPipelineByType(StatePipelineType pipelineType)
        {
            switch (pipelineType)
            {
                case StatePipelineType.Basic:
                    return basicPipeline;
                case StatePipelineType.Main:
                    return mainPipeline;
                case StatePipelineType.Buff:
                    return buffPipeline;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 设置流水线引用
        /// </summary>
        private void SetPipelineByType(StatePipelineType pipelineType, StatePipelineRuntime pipeline)
        {
            switch (pipelineType)
            {
                case StatePipelineType.Basic:
                    basicPipeline = pipeline;
                    break;
                case StatePipelineType.Main:
                    mainPipeline = pipeline;
                    break;
                case StatePipelineType.Buff:
                    buffPipeline = pipeline;
                    break;
            }
        }

        /// <summary>
        /// 获取所有流水线（用于遍历）
        /// 注意：basicPipeline和mainPipeline必须不为null，否则视为崩溃
        /// </summary>
        private IEnumerable<StatePipelineRuntime> GetAllPipelines()
        {
            yield return basicPipeline;
            yield return mainPipeline;
            yield return buffPipeline;
        }

        #endregion

        #region Playable动画系统（核心/谨慎改）

        /// <summary>
        /// PlayableGraph引用 - 用于动画播放
        /// </summary>
        [NonSerialized]
        public PlayableGraph playableGraph;

        /// <summary>
        /// 绑定的Animator
        /// </summary>
        [NonSerialized]
        protected Animator boundAnimator;

        /// <summary>
        /// Animator输出
        /// </summary>
        [NonSerialized]
        protected AnimationPlayableOutput animationOutput;

        /// <summary>
        /// 根动画混合器 - 支持多层动画混合
        /// </summary>
        [NonSerialized]
        internal AnimationMixerPlayable rootMixer;

        /// <summary>
        /// 是否拥有PlayableGraph所有权
        /// </summary>
        [NonSerialized]
        protected bool ownsPlayableGraph = false;

        public bool IsPlayableGraphValid => playableGraph.IsValid();

        public bool IsPlayableGraphPlaying => playableGraph.IsValid() && playableGraph.IsPlaying();

        public Animator BoundAnimator => boundAnimator;

        #endregion

        #region 性能优化相关（核心/谨慎改）

        /// <summary>
        /// 状态转换缓存 - 避免频繁的字典查找
        /// </summary>
        [NonSerialized]
        protected Dictionary<string, StateBase> transitionCache = new Dictionary<string, StateBase>();

        /// <summary>
        /// 脏标记 - 标识是否需要更新
        /// </summary>
        [NonSerialized]
        protected bool isDirty = false;

        private sealed class StateActivationCache
        {
            public int[] versions;
            public StateActivationResult[] results;
            public List<StateBase>[] interruptLists;
            public List<StateBase>[] mergeLists;
        }

        public enum StateDirtyReason
        {
            Unknown = 0,
            Enter = 1,
            Exit = 2,
            Release = 3,
            CostChanged = 4,
            RuntimeChanged = 5
        }

        [Serializable]
        public struct StateCostSummary
        {
            public float motionCost;
            public float agilityCost;
            public float targetCost;
            public float weightedMotion;
            public float weightedAgility;
            public float weightedTarget;
            public float totalWeighted;

            public void Clear()
            {
                motionCost = 0f;
                agilityCost = 0f;
                targetCost = 0f;
                weightedMotion = 0f;
                weightedAgility = 0f;
                weightedTarget = 0f;
                totalWeighted = 0f;
            }
        }

        #endregion

        #region 初始化与销毁（核心/谨慎改）

        /// <summary>
        /// 初始化状态机
        /// </summary>
        /// <param name="entity">宿主Entity</param>
        /// <param name="graph">PlayableGraph，如果为default则自动创建</param>
        /// <param name="root">外部RootMixer（可选）</param>
        public void Initialize(Entity entity, PlayableGraph graph = default, AnimationMixerPlayable root = default)
        {
            if (isInitialized) return;

            hostEntity = entity;

            // 初始化StateContext（整合了原StateMachineContext和动画参数）
            stateContext = new StateMachineContext();
            stateContext.contextID = Guid.NewGuid().ToString();
            stateContext.creationTime = Time.time;
            stateContext.lastUpdateTime = Time.time;

            // 初始化流水线
            InitializePipelines(graph, root);

            // 初始化SupportFlags禁用跳转缓存（超高频查询用）
            InitializeSupportFlagsTransitionCache();

            // 初始化所有状态（注意：状态初始化依赖流水线已创建，所以必须在InitializePipelines之后）
            foreach (var kvp in stringToStateMap)
            {
                InitializeState(kvp.Value);
            }

            // 标记所有流水线需要FallBack检查
            basicPipeline.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            mainPipeline.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            buffPipeline.MarkDirty(PipelineDirtyFlags.FallbackCheck);

            isInitialized = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTransitionDisabledFast(StateSupportFlags fromFlag, StateSupportFlags toFlag)
        {
            if (_disableTransitionMasks == null || _supportFlagToRelationIndex == null) return false;
            fromFlag = NormalizeSingleSupportFlag(fromFlag);
            toFlag = NormalizeSingleSupportFlag(toFlag);
            if (fromFlag == StateSupportFlags.None || toFlag == StateSupportFlags.None) return false;

            int fromBit = GetFlagBitIndex(fromFlag);
            int toBit = GetFlagBitIndex(toFlag);
            if ((uint)fromBit >= 8 || (uint)toBit >= 8) return false;

            int fromIndex = _supportFlagToRelationIndex[fromBit];
            int toIndex = _supportFlagToRelationIndex[toBit];
            if (fromIndex < 0 || toIndex < 0) return false;

            return ((_disableTransitionMasks[fromBit] >> toIndex) & 1UL) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTransitionDisabledMaskFast(StateSupportFlags fromFlag, ulong targetMask)
        {
            if (_disableTransitionMasks == null) return false;
            fromFlag = NormalizeSingleSupportFlag(fromFlag);
            if (fromFlag == StateSupportFlags.None) return false;
            int fromBit = GetFlagBitIndex(fromFlag);
            if ((uint)fromBit >= 8) return false;
            return (_disableTransitionMasks[fromBit] & targetMask) != 0UL;
        }

        public ulong BuildTransitionMask(params StateSupportFlags[] flags)
        {
            if (_supportFlagToRelationIndex == null || flags == null || flags.Length == 0) return 0UL;
            ulong mask = 0UL;
            for (int i = 0; i < flags.Length; i++)
            {
                var flag = NormalizeSingleSupportFlag(flags[i]);
                if (flag == StateSupportFlags.None) continue;
                int bit = GetFlagBitIndex(flag);
                if ((uint)bit >= 8) continue;
                int index = _supportFlagToRelationIndex[bit];
                if (index >= 0 && index < 64)
                {
                    mask |= 1UL << index;
                }
            }

            return mask;
        }

        private void InitializeSupportFlagsTransitionCache()
        {
            if (config == null)
            {
                config = StateMachineConfig.Instance;
            }

            var map = config != null ? config.disableTransitionPermissionMap : null;
            if (map == null)
            {
                _supportFlagToRelationIndex = null;
                _disableTransitionMasks = null;
                return;
            }

            _supportFlagToRelationIndex = new int[8];
            for (int i = 0; i < _supportFlagToRelationIndex.Length; i++)
            {
                _supportFlagToRelationIndex[i] = -1;
            }

            _disableTransitionMasks = new ulong[8];
            Array.Clear(_disableTransitionMasks, 0, _disableTransitionMasks.Length);

            var values = (StateSupportFlags[])Enum.GetValues(typeof(StateSupportFlags));
            for (int i = 0; i < values.Length; i++)
            {
                var flag = values[i];
                if (flag == StateSupportFlags.None) continue;
                int bitIndex = GetFlagBitIndex(flag);
                if ((uint)bitIndex >= 8) continue;

                if (map.TryGetIndex(flag, out var relationIndex))
                {
                    _supportFlagToRelationIndex[bitIndex] = relationIndex;
                }

                if (map.TryGetMaskFast(flag, out var mask))
                {
                    _disableTransitionMasks[bitIndex] = mask;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetFlagBitIndex(StateSupportFlags flag)
        {
            byte value = (byte)flag;
            if (value == 0) return -1;
            int index = 0;
            while ((value & 1) == 0)
            {
                value >>= 1;
                index++;
            }
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static StateSupportFlags NormalizeSingleSupportFlag(StateSupportFlags flag)
        {
            if (flag == StateSupportFlags.None) return StateSupportFlags.None;
            byte value = (byte)flag;
            byte lowest = (byte)(value & (byte)(-(sbyte)value));
            return (StateSupportFlags)lowest;
        }

        /// <summary>
        /// 初始化状态机并绑定Animator
        /// </summary>
        public void Initialize(Entity entity, Animator animator, PlayableGraph graph = default, AnimationMixerPlayable root = default)
        {
            Initialize(entity, graph, root);
            BindToAnimator(animator);
            playableGraph.Stop();
            playableGraph.Play();
        }

        /// <summary>
        /// 初始化流水线系统
        /// </summary>
        private void InitializePipelines(PlayableGraph hanldegraph, AnimationMixerPlayable root)
        {
            // Playable初始化
            if (hanldegraph.IsValid())
            {
                playableGraph = hanldegraph;
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                ownsPlayableGraph = false;
            }
            else
            {
                playableGraph = PlayableGraph.Create($"StateMachine_{stateMachineKey}");
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                ownsPlayableGraph = true;
            }

            int pipelineCount = (int)StatePipelineType.Count;

            // 创建/绑定根Mixer
            if (playableGraph.IsValid())
            {
                if (root.IsValid())
                {
                    rootMixer = root;
                    if (rootMixer.GetInputCount() < pipelineCount)
                    {
                        rootMixer.SetInputCount(pipelineCount);
                    }
                }
                else
                {
                    rootMixer = AnimationMixerPlayable.Create(playableGraph, pipelineCount);
                }
            }

            // 使用封装方法直接装填所有流水线
            InitializeAllPipelines();
            InitializePipelineWeights();
        }

        /// <summary>
        /// 初始化单个流水线
        /// </summary>
        private StatePipelineRuntime InitializeSinglePipeline(StatePipelineType pipelineType)
        {
            Debug.Log($"[StateMachine] 开始初始化流水线: {pipelineType}");
            var pipeline = new StatePipelineRuntime(pipelineType, this);
            SetPipelineByType(pipelineType, pipeline);

            // 如果有PlayableGraph,为流水线创建Mixer并接入Root
            if (playableGraph.IsValid())
            {
                pipeline.mixer = AnimationMixerPlayable.Create(playableGraph, 0);
                pipeline.rootInputIndex = (int)pipelineType;
                playableGraph.Connect(pipeline.mixer, 0, rootMixer, pipeline.rootInputIndex);
                rootMixer.SetInputWeight(pipeline.rootInputIndex, pipeline.weight);
                Debug.Log($"[StateMachine] ✓ {pipelineType}流水线Mixer创建成功 | Valid:{pipeline.mixer.IsValid()} | RootIndex:{pipeline.rootInputIndex}");
            }
            else
            {
                Debug.LogWarning($"[StateMachine] ✗ {pipelineType}流水线Mixer创建失败 - PlayableGraph无效");
            }

            OnPipelineInitialized?.Invoke(pipeline);
            return pipeline;
        }

        /// <summary>
        /// 初始化所有流水线 - 直接装填枚举
        /// </summary>
        private void InitializeAllPipelines()
        {
            // 直接装填每个枚举值
            basicPipeline = InitializeSinglePipeline(StatePipelineType.Basic);
            mainPipeline = InitializeSinglePipeline(StatePipelineType.Main);
            buffPipeline = InitializeSinglePipeline(StatePipelineType.Buff);
        }

        /// <summary>
        /// 初始化单个状态
        /// </summary>
        public void InitializeState(StateBase state)
        {
            state.host = this;
            state.Initialize(this);
        }

        /// <summary>
        /// 绑定PlayableGraph到Animator
        /// </summary>
        public bool BindToAnimator(Animator animator)
        {
            if (!playableGraph.IsValid())
            {
                playableGraph = PlayableGraph.Create($"StateMachine_{stateMachineKey}");
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                ownsPlayableGraph = true;
            }
            else
            {
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            }

            if (!rootMixer.IsValid())
            {
                int pipelineCount = (int)StatePipelineType.Count;
                rootMixer = AnimationMixerPlayable.Create(playableGraph, pipelineCount);
            }

            boundAnimator = animator;

            if (!animationOutput.IsOutputValid())
            {
                animationOutput = AnimationPlayableOutput.Create(playableGraph, "StateMachine", animator);
            }
            else
            {
                animationOutput.SetTarget(animator);
            }

            animationOutput.SetSourcePlayable(rootMixer);
            // ★ 确保Output权重为1.0，否则动画不会输出
            animationOutput.SetWeight(1.0f);

            InitializePipelineWeights();

            Debug.Log($"[StateMachine] Animator绑定成功: {animator.gameObject.name}");
            return true;
        }

        /// <summary>
        /// 标记Dirty（用于缓存失效）
        /// </summary>
        public void MarkDirty(StateDirtyReason reason = StateDirtyReason.Unknown)
        {
            _dirtyVersion++;
            isDirty = true;
            _lastDirtyReason = reason;
        }

        /// <summary>
        /// 清除Dirty标记（仅影响外部查询，不影响版本号）
        /// </summary>
        public void ClearDirty()
        {
            isDirty = false;
        }

        private List<StateBase> GetCachedRunningStates()
        {
            if (_cachedRunningStatesVersion == _dirtyVersion)
            {
                return _cachedRunningStatesList;
            }

            _cachedRunningStatesList.Clear();
            for (int i = 0; i < _registeredStatesList.Count; i++)
            {
                var state = _registeredStatesList[i];
                if (runningStates.Contains(state))
                {
                    _cachedRunningStatesList.Add(state);
                }
            }
            _cachedRunningStatesVersion = _dirtyVersion;
            return _cachedRunningStatesList;
        }

        private StateBase GetFirstRunningState(StatePipelineRuntime pipeline)
        {
            var runningStates = GetCachedRunningStates();
            for (int i = 0; i < runningStates.Count; i++)
            {
                var state = runningStates[i];
                if (pipeline.runningStates.Contains(state))
                {
                    return state;
                }
            }

            return null;
        }

        private static int CompareStateDeterministic(StateBase a, StateBase b)
        {
            if (ReferenceEquals(a, b)) return 0;

            int aId = a.stateSharedData.basicConfig.stateId;
            int bId = b.stateSharedData.basicConfig.stateId;
            int idCompare = aId.CompareTo(bId);
            if (idCompare != 0) return idCompare;

            string aKey = a.strKey ?? string.Empty;
            string bKey = b.strKey ?? string.Empty;
            int keyCompare = string.CompareOrdinal(aKey, bKey);
            if (keyCompare != 0) return keyCompare;

            return 0;
        }

        private StatePipelineType ResolvePipelineForState(StateBase targetState, StatePipelineType pipeline)
        {
            if (pipeline != StatePipelineType.NotClear)
            {
                return pipeline;
            }

            if (statePipelineMap.TryGetValue(targetState, out var mappedPipeline))
            {
                return mappedPipeline;
            }

            return targetState.stateSharedData.basicConfig.pipelineType;
        }

        private StateActivationCache GetOrCreateActivationCache(StateBase targetState)
        {
            if (!_activationCache.TryGetValue(targetState, out var cache) || cache == null)
            {
                cache = new StateActivationCache();
                _activationCache[targetState] = cache;
            }

            int pipelineCount = (int)StatePipelineType.Count;
            if (cache.versions == null || cache.versions.Length != pipelineCount)
            {
                cache.versions = new int[pipelineCount];
                cache.results = new StateActivationResult[pipelineCount];
                cache.interruptLists = new List<StateBase>[pipelineCount];
                cache.mergeLists = new List<StateBase>[pipelineCount];

                for (int i = 0; i < pipelineCount; i++)
                {
                    cache.versions[i] = -1;
                    cache.interruptLists[i] = new List<StateBase>(4);
                    cache.mergeLists[i] = new List<StateBase>(4);
                }
            }

            return cache;
        }

        public StateChannelMask GetTotalChannelMask()
        {
            if (_cachedChannelMaskVersion != _dirtyVersion || isDirty)
            {
                _cachedChannelMask = EvaluateChannelMask();
                _cachedChannelMaskVersion = _dirtyVersion;
            }

            return _cachedChannelMask;
        }

        public StateCostSummary GetTotalCostSummary()
        {
            if (_cachedCostVersion != _dirtyVersion || isDirty)
            {
                _cachedCostSummary = EvaluateCostSummary();
                _cachedCostVersion = _dirtyVersion;
            }

            return _cachedCostSummary;
        }

        private StateChannelMask EvaluateChannelMask()
        {
            if (CustomChannelMaskEvaluator != null)
            {
                return CustomChannelMaskEvaluator(runningStates);
            }

            StateChannelMask mask = StateChannelMask.None;
            foreach (var state in runningStates)
            {
                var mergeData = state.stateSharedData.mergeData;
                mask |= mergeData.stateChannelMask;
            }

            return mask;
        }

        private StateCostSummary EvaluateCostSummary()
        {
            if (CustomCostEvaluator != null)
            {
                return CustomCostEvaluator(runningStates);
            }

            StateCostSummary sum = new StateCostSummary();
            foreach (var state in runningStates)
            {
                var costData = state.stateSharedData.costData;

                sum.motionCost += costData.motionCost;
                sum.agilityCost += costData.agilityCost;
                sum.targetCost += costData.targetCost;
                sum.weightedMotion += costData.GetWeightedMotion();
                sum.weightedAgility += costData.GetWeightedAgility();
                sum.weightedTarget += costData.GetWeightedTarget();
            }

            sum.totalWeighted = sum.weightedMotion + sum.weightedAgility + sum.weightedTarget;
            return sum;
        }

        private float GetMainStateScore(StateBase state)
        {
            var sharedData = state.stateSharedData;
            var basic = sharedData.basicConfig;

            switch (basic.mainStateCriterion)
            {
                case MainStateCriterionType.DirectWeight:
                    return basic.directMainWeight;
                case MainStateCriterionType.CostBased:
                    {
                        var cost = sharedData.costData;
                        return cost.GetTotalCost();
                    }
                case MainStateCriterionType.Dynamic:
                default:
                    {
                        if (CustomMainStateScore != null)
                            return CustomMainStateScore(state);

                        var cost = sharedData.costData;
                        return cost.GetTotalCost();
                    }
            }
        }

        /// <summary>
        /// 通知代价层变化
        /// </summary>
        public void NotifyCostChanged()
        {
            MarkDirty(StateDirtyReason.CostChanged);
        }

        /// <summary>
        /// 销毁状态机，释放资源
        /// </summary>
        public void Dispose()
        {
            // 停用所有运行中的状态
            _tmpStateBuffer.Clear();
            _tmpStateBuffer.AddRange(runningStates);
            foreach (var state in _tmpStateBuffer)
            {
                state.OnStateExit();
            }

            runningStates.Clear();

            // 清理流水线
            foreach (var pipeline in GetAllPipelines())
            {
                pipeline.runningStates.Clear();

                // 清理Playable槽位映射
                pipeline.stateToSlotMap.Clear();
                pipeline.freeSlots.Clear();

                pipeline.mixer.Destroy();
            }

            basicPipeline = null;
            mainPipeline = null;
            buffPipeline = null;

            // 清理Playable资源
            if (ownsPlayableGraph && playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }

            animationOutput = default;
            boundAnimator = null;

            // 清理映射
            stringToStateMap.Clear();
            intToStateMap.Clear();
            transitionCache.Clear();
            statePipelineMap.Clear();
            _activationCache.Clear();

            // 清理上下文
            stateContext.Clear();

            isRunning = false;
            isInitialized = false;
        }

        #endregion

        #region 状态机生命周期（核心/谨慎改）

        /// <summary>
        /// 启动状态机
        /// </summary>
        public void StartStateMachine()
        {
            if (isRunning) return;
            if (!isInitialized)
            {
                Debug.LogWarning($"状态机 {stateMachineKey} 未初始化，无法启动");
                return;
            }

            isRunning = true;

            // 播放PlayableGraph
            if (playableGraph.IsValid())
            {
                playableGraph.Play();
            }

            // 进入默认状态
            if (!string.IsNullOrEmpty(defaultStateKey))
            {
                TryActivateState(defaultStateKey, StatePipelineType.Basic);
            }
        }

        /// <summary>
        /// 停止状态机
        /// </summary>
        public void StopStateMachine()
        {
            if (!isRunning) return;

            // 停止所有流水线
            foreach (var pipeline in GetAllPipelines())
            {
                DeactivatePipeline(pipeline.pipelineType);
            }

            // 停止PlayableGraph
            if (playableGraph.IsValid())
            {
                playableGraph.Stop();
            }

            isRunning = false;
        }

        /// <summary>
        /// 更新状态机 - 每帧调用
        /// 注意：Animator需要设置为：
        /// 1. Update Mode = Normal (允许脚本控制)
        /// 2. Culling Mode = Always Animate (即使不可见也更新)
        /// 3. 不要勾选 Apply Root Motion（除非需要根运动）
        /// 
        /// Dirty机制：根据各流水线的Dirty等级执行不同任务
        /// - Dirty >= 1: 执行FallBack自动激活检查
        /// - Dirty >= 2: 执行中等优先级任务（预留）
        /// - Dirty >= 3: 执行高优先级任务（预留）
        /// </summary>
        public void UpdateStateMachine()
        {
            if (!isRunning) return;

            float deltaTime = Time.deltaTime;

            // SupportFlags由StateMachine统一维护，无需同步

            // 更新上下文时间
            stateContext.lastUpdateTime = Time.time;

            // 更新所有运行中的状态
            var statesToDeactivate = _statesToDeactivateCache; // 收集需要自动退出的状态
            statesToDeactivate.Clear();
            foreach (var state in runningStates)
            {
                if (state.baseStatus == StateBaseStatus.Running)
                {
                    state.OnStateUpdate();

                    // ★ 更新动画权重 - 2D混合树等需要通过StateContext获取参数
                    state.UpdateAnimationWeights(stateContext, deltaTime);

                    // ★ 检查是否应该自动退出（按持续时间模式）
                    if (state.ShouldAutoExit(Time.time))
                    {
                        statesToDeactivate.Add(state);
                    }
                }
            }

            // 自动退出已完成的状态
            foreach (var state in statesToDeactivate)
            {
                // 查找状态所在流水线并停用
                foreach (var pipeline in GetAllPipelines())
                {
                    if (pipeline.runningStates.Contains(state))
                    {
                        TryDeactivateState(state.strKey);
                        break;
                    }
                }
            }

            // ★ 更新淡入淡出效果
            UpdateFades(deltaTime);

            // 更新三个流水线的MainState
            UpdatePipelineMainState(basicPipeline);
            UpdatePipelineMainState(mainPipeline);
            UpdatePipelineMainState(buffPipeline);

            // 更新流水线Dirty自动标记（高等级降级到1，保持最低Dirty用于持续检查FallBack）
            basicPipeline.UpdateDirtyDecay();
            mainPipeline.UpdateDirtyDecay();
            buffPipeline.UpdateDirtyDecay();

            // 根据Dirty等级处理不同任务（包括FallBack自动激活）
            ProcessDirtyTasks(basicPipeline, StatePipelineType.Basic);
            ProcessDirtyTasks(mainPipeline, StatePipelineType.Main);
            ProcessDirtyTasks(buffPipeline, StatePipelineType.Buff);

            // ★ 应用流水线混合模式（Main与Basic的混合策略）
            ApplyPipelineBlendMode();

            // Manual模式下需要手动Evaluate推进图
            if (playableGraph.IsValid())
            {
                if (!playableGraph.IsPlaying())
                {
                    playableGraph.Play();
                }
                StateMachineDebugSettings.Instance.LogPerformance(
                    $"[StateMachine] 手动评估PlayableGraph，DeltaTime: {deltaTime:F4}" +
                    playableGraph.GetTimeUpdateMode() +
                    playableGraph.IsPlaying() +
                    playableGraph.IsValid());
                playableGraph.Evaluate(deltaTime);
            }

            // 持续输出统计信息（可选）
            if (enableContinuousStats)
            {
                OutputContinuousStats();
            }

        }



        /// <summary>
        /// 应用流水线混合模式 - 控制Main线和Basic线的混合权重
        /// </summary>
        private void ApplyPipelineBlendMode()
        {
            float basicWeight = basicPipeline.weight;
            float mainWeight = mainPipeline.weight;

            // 计算Main线的实际激活度（有运行状态则视为激活）
            float mainActivation = (mainPipeline.runningStates.Count > 0) ? mainWeight : 0f;

            switch (pipelineBlendMode)
            {
                case PipelineBlendMode.Override:
                    // 覆盖模式：Main激活时完全覆盖Basic
                    if (mainActivation > 0.001f)
                    {
                        rootMixer.SetInputWeight(basicPipeline.rootInputIndex, 0f);
                        rootMixer.SetInputWeight(mainPipeline.rootInputIndex, 1f);
                    }
                    else
                    {
                        rootMixer.SetInputWeight(basicPipeline.rootInputIndex, 1f);
                        rootMixer.SetInputWeight(mainPipeline.rootInputIndex, 0f);
                    }
                    break;

                case PipelineBlendMode.Additive:
                    // 叠加模式：直接使用各自的权重（默认行为）
                    rootMixer.SetInputWeight(basicPipeline.rootInputIndex, basicWeight);
                    rootMixer.SetInputWeight(mainPipeline.rootInputIndex, mainWeight);
                    break;

                case PipelineBlendMode.Multiplicative:
                    // 乘法模式：Basic权重被Main激活度调制
                    float modulatedBasicWeight = basicWeight * (1f - mainActivation);
                    rootMixer.SetInputWeight(basicPipeline.rootInputIndex, modulatedBasicWeight);
                    rootMixer.SetInputWeight(mainPipeline.rootInputIndex, mainWeight);
                    break;
            }

            // Buff线始终使用自身权重（不受混合模式影响）
            rootMixer.SetInputWeight(buffPipeline.rootInputIndex, buffPipeline.weight);
        }

        /// <summary>
        /// 初始化所有流水线权重到RootMixer
        /// </summary>
        private void InitializePipelineWeights()
        {
            if (!rootMixer.IsValid()) return;

            basicPipeline.UpdatePipelineMixer();
            mainPipeline.UpdatePipelineMixer();
            buffPipeline.UpdatePipelineMixer();
        }

        #region 淡入淡出系统（核心/谨慎改）

        /// <summary>
        /// 应用淡入效果到新激活的状态
        /// </summary>
        private void ApplyFadeIn(StateBase state, StatePipelineRuntime pipeline)
        {
            if (!state.stateSharedData.enableFadeInOut) return;
            if (state.stateSharedData.basicConfig.useDirectBlend) return;

            float fadeInDuration = GetScaledFadeDuration(state.stateSharedData.fadeInDuration, state.stateSharedData);
            if (fadeInDuration <= 0f || !pipeline.stateToSlotMap.ContainsKey(state))
                return;

            // 初始化淡入：权重从0开始
            int slotIndex = pipeline.stateToSlotMap[state];
            pipeline.mixer.SetInputWeight(slotIndex, 0f);

            // 记录淡入数据（需要在StatePipelineRuntime中添加字段）
            if (!pipeline.fadeInStates.TryGetValue(state, out var fadeData))
            {
                fadeData = StateFadeData.Pool.GetInPool();
                fadeData.elapsedTime = 0f;
                fadeData.duration = fadeInDuration;
                fadeData.slotIndex = slotIndex;
                fadeData.startWeight = 1f;
                pipeline.fadeInStates[state] = fadeData;

                StateMachineDebugSettings.Instance.LogFade(
                    $"[淡入] 状态 {state.strKey} 开始淡入，时长 {fadeInDuration:F2}秒");
            }
        }

        /// <summary>
        /// 应用淡出效果到即将停用的状态
        /// </summary>
        private void ApplyFadeOut(StateBase state, StatePipelineRuntime pipeline)
        {
            if (!state.stateSharedData.enableFadeInOut) return;
            if (state.stateSharedData.basicConfig.useDirectBlend) return;

            float fadeOutDuration = GetScaledFadeDuration(state.stateSharedData.fadeOutDuration, state.stateSharedData);
            if (fadeOutDuration <= 0f || !pipeline.stateToSlotMap.ContainsKey(state))
                return;

            // 记录淡出数据
            int slotIndex = pipeline.stateToSlotMap[state];
            float currentWeight = pipeline.mixer.GetInputWeight(slotIndex);

            if (!pipeline.fadeOutStates.TryGetValue(state, out var fadeData))
            {
                fadeData = StateFadeData.Pool.GetInPool();
                fadeData.elapsedTime = 0f;
                fadeData.duration = fadeOutDuration;
                fadeData.slotIndex = slotIndex;
                fadeData.startWeight = currentWeight;
                pipeline.fadeOutStates[state] = fadeData;

                state.OnFadeOutStarted();
                StateMachineDebugSettings.Instance.LogFade(
                    $"[淡出] 状态 {state.strKey} 开始淡出，时长 {fadeOutDuration:F2}秒，起始权重 {currentWeight:F2}");
            }
        }

        /// <summary>
        /// 更新所有流水线的淡入淡出效果
        /// </summary>
        private void UpdateFades(float deltaTime)
        {
            UpdatePipelineFades(basicPipeline, deltaTime);
            UpdatePipelineFades(mainPipeline, deltaTime);
            UpdatePipelineFades(buffPipeline, deltaTime);
        }

        /// <summary>
        /// 更新单个流水线的淡入淡出效果
        /// </summary>
        private void UpdatePipelineFades(StatePipelineRuntime pipeline, float deltaTime)
        {
            // 更新淡入状态
            var fadeInToRemove = pipeline.fadeInToRemoveCache;
            fadeInToRemove.Clear();
            foreach (var kvp in pipeline.fadeInStates)
            {
                var state = kvp.Key;
                var fadeData = kvp.Value;

                fadeData.elapsedTime += deltaTime;
                float t = Mathf.Clamp01(fadeData.elapsedTime / fadeData.duration);
                float eased = EvaluateFadeCurve(state, t, isFadeIn: true);
                float weight = Mathf.Lerp(0f, 1f, eased);

                pipeline.mixer.SetInputWeight(fadeData.slotIndex, weight);

                if (t >= 1f)
                {
                    fadeInToRemove.Add(state);
                    state.OnFadeInComplete();
                    StateMachineDebugSettings.Instance.LogFade(
                        $"[淡入完成] 状态 {state.strKey}");
                }
            }

            // 移除已完成的淡入状态
            foreach (var state in fadeInToRemove)
            {
                if (pipeline.fadeInStates.TryGetValue(state, out var fadeData))
                {
                    fadeData.TryAutoPushedToPool();
                }
                pipeline.fadeInStates.Remove(state);
            }

            // 更新淡出状态
            var fadeOutToRemove = pipeline.fadeOutToRemoveCache;
            fadeOutToRemove.Clear();
            foreach (var kvp in pipeline.fadeOutStates)
            {
                var state = kvp.Key;
                var fadeData = kvp.Value;

                fadeData.elapsedTime += deltaTime;
                float t = Mathf.Clamp01(fadeData.elapsedTime / fadeData.duration);
                float eased = EvaluateFadeCurve(state, t, isFadeIn: false);
                float weight = Mathf.Lerp(fadeData.startWeight, 0f, eased);

                pipeline.mixer.SetInputWeight(fadeData.slotIndex, weight);

                if (t >= 1f)
                {
                    fadeOutToRemove.Add(state);
                    HotUnplugStateFromPlayable(state, pipeline);
                    StateMachineDebugSettings.Instance.LogFade(
                        $"[淡出完成] 状态 {state.strKey}");
                }
            }

            // 移除已完成的淡出状态
            foreach (var state in fadeOutToRemove)
            {
                if (pipeline.fadeOutStates.TryGetValue(state, out var fadeData))
                {
                    fadeData.TryAutoPushedToPool();
                }
                pipeline.fadeOutStates.Remove(state);
            }
        }

        private float GetScaledFadeDuration(float baseDuration, StateSharedData sharedData)
        {
            if (baseDuration <= 0f) return baseDuration;

            float scale = Mathf.Max(0.01f, sharedData.fadeSpeedMultiplier);
            if (sharedData.fadeFollowTimeScale)
                scale *= Mathf.Max(0.01f, Time.timeScale);

            return baseDuration / scale;
        }

        private float EvaluateFadeCurve(StateBase state, float t, bool isFadeIn)
        {
            if (!state.stateSharedData.useAdvancedFadeCurve) return t;

            var curve = isFadeIn ? state.stateSharedData.fadeInCurve : state.stateSharedData.fadeOutCurve;
            if (curve == null || curve.length == 0)
            {
                return t;
            }
            return Mathf.Clamp01(curve.Evaluate(t));
        }

        #endregion

        /// <summary>
        /// 输出持续统计信息 - 简洁版，不干扰游戏运行
        /// </summary>
        private void OutputContinuousStats()
        {
            var sb = _continuousStatsBuilder;
            sb.Clear();
            sb.Append($"[Stats] 运行:{runningStates.Count} |");

            foreach (var pipeline in GetAllPipelines())
            {
                if (pipeline.runningStates.Count > 0)
                {
                    sb.Append($" {pipeline.pipelineType}:{pipeline.runningStates.Count}");
                }
            }

            if (runningStates.Count > 0)
            {
                sb.Append(" | 状态:");
                foreach (var state in runningStates)
                {
                    sb.Append($" [{state.strKey}]");
                }
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 根据流水线的Dirty标记处理不同任务
        /// </summary>
        private void ProcessDirtyTasks(StatePipelineRuntime pipelineData, StatePipelineType pipeline)
        {
            if (!pipelineData.IsDirty) return;

            if (pipelineData.HasDirtyFlag(PipelineDirtyFlags.HighPriority))
            {
                // 可在此添加高优先级任务
            }

            if (pipelineData.HasDirtyFlag(PipelineDirtyFlags.MediumPriority))
            {
                // 可在此添加中等优先级任务
            }

            if (pipelineData.HasDirtyFlag(PipelineDirtyFlags.HotPlug))
            {
                // 热插拔相关任务（预留）
                pipelineData.ClearDirty(PipelineDirtyFlags.HotPlug);
            }

            if (pipelineData.HasDirtyFlag(PipelineDirtyFlags.FallbackCheck))
            {
                // 如果流水线空闲，尝试激活FallBack状态
                if (pipelineData.runningStates.Count == 0)
                {
                    // Debug.Log($"[FallBack-Activate] ⚠ [{pipeline}] 流水线已空，检查FallBack配置...");
                    // Debug.Log($"[FallBack-Activate]   DefaultSupportFlag={pipelineData.DefaultSupportFlag}");

                    // 使用支持标记FallBack系统
                    int fallbackStateId = pipelineData.GetFallBack(currentSupportFlags); // 使用当前SupportFlags

                    if (fallbackStateId >= 0)
                    {
                        // Debug.Log($"[FallBack-Activate] 🔍 查找FallBack状态: StateID={fallbackStateId}");
                        var fallbackState = GetStateByInt(fallbackStateId);

                        bool activated = TryActivateState(fallbackState, pipeline);
                        if (activated)
                        {
                            pipelineData.ClearDirty(PipelineDirtyFlags.FallbackCheck);
                        }
                        else
                        {
                            //Debug.LogWarning($"[FallBack-Activate] ✗ 未找到FallBack状态(ID={fallbackStateId})，流水线将保持空闲");
                        }
                    }
                    else
                    {
                        // Debug.Log($"[FallBack-Activate] ⊘ [{pipeline}] 未配置FallBack状态(StateID={fallbackStateId})，流水线保持空闲");
                    }
                }
                else
                {
                    // Debug.Log($"[FallBack-Activate] [{pipeline}] 流水线仍有{pipelineData.runningStates.Count}个运行状态，无需FallBack");
                    // 流水线非空时也清除FallBack标记
                    pipelineData.ClearDirty(PipelineDirtyFlags.FallbackCheck);
                }
            }
        }

        #endregion

        #region 状态注册与管理（核心/谨慎改）

        /// <summary>
        /// 从StateAniDataInfo注册状态 - 完整封装创建和注册流程
        /// </summary>
        /// <param name="info">状态数据Info</param>
        /// <param name="allowOverride">是否允许覆盖已存在的状态</param>
        /// <returns>是否注册成功</returns>
        public bool RegisterStateFromInfo(StateAniDataInfo info, bool allowOverride = false)
        {
            return RegisterStateFromInfo(info, null, allowOverride);
        }

        /// <summary>
        /// 从StateAniDataInfo注册状态 - 支持自定义String键
        /// </summary>
        /// <param name="info">状态数据Info</param>
        /// <param name="customStringKey">自定义String键（null则使用info中的stateName）</param>
        /// <param name="allowOverride">是否允许覆盖已存在的状态</param>
        /// <returns>是否注册成功</returns>
        public bool RegisterStateFromInfo(StateAniDataInfo info, string customStringKey, bool allowOverride = false)
        {
            return RegisterStateFromInfo(info, customStringKey, null, allowOverride);
        }

        /// <summary>
        /// 从StateAniDataInfo注册状态 - 支持自定义String和Int键
        /// </summary>
        /// <param name="info">状态数据Info</param>
        /// <param name="customStringKey">自定义String键（null则使用info中的stateName）</param>
        /// <param name="customIntKey">自定义Int键（null则使用info中的stateId）</param>
        /// <param name="allowOverride">是否允许覆盖已存在的状态</param>
        /// <returns>是否注册成功</returns>
        public bool RegisterStateFromInfo(StateAniDataInfo info, string customStringKey, int? customIntKey, bool allowOverride = false)
        {
            try
            {
                // 1. 确保Runtime初始化（不重复）
                info.InitializeRuntime();
                StateMachineDebugSettings.Instance.LogRuntimeInit($"✓ Info初始化完成: {info.sharedData.basicConfig.stateName}");

                // 2. 创建StateBase实例
                var state = CreateStateFromInfo(info);
                // 3. 应用自定义键（如果提供）
                string finalStringKey = customStringKey ?? info.sharedData.basicConfig.stateName;
                int finalIntKey = customIntKey ?? info.sharedData.basicConfig.stateId;

                // 4. 获取流水线类型
                var pipelineType = info.sharedData.basicConfig.pipelineType;

                // 5. 注册状态（使用自定义键或原始键）
                bool registered;
                if (customStringKey != null || customIntKey.HasValue)
                {
                    // 使用了自定义键，直接注册
                    registered = RegisterStateCore(finalStringKey, finalIntKey, state, pipelineType);
                    if (!registered && !allowOverride)
                    {
                        // 键冲突时自动处理
                        registered = RegisterState(state, pipelineType, allowOverride);
                    }
                }
                else
                {
                    // 使用原始键，自动处理冲突
                    registered = RegisterState(state, pipelineType, allowOverride);
                }

                if (registered)
                {
                    StateMachineDebugSettings.Instance.LogStateTransition(
                        $"✓ 注册状态: [{pipelineType}] {state.strKey} (ID:{state.intKey})");
                }
                else
                {
                    if (StateMachineDebugSettings.Instance.alwaysLogErrors)
                        Debug.LogWarning($"[StateMachine] 注册状态失败: {info.sharedData.basicConfig.stateName}");
                }

                return registered;
            }
            catch (Exception e)
            {
                if (StateMachineDebugSettings.Instance.alwaysLogErrors)
                    Debug.LogError($"[StateMachine] 注册状态异常: {info.sharedData.basicConfig.stateName}\n{e}");
                return false;
            }
        }

        /// <summary>
        /// 从StateAniDataInfo创建StateBase实例
        /// </summary>
        private StateBase CreateStateFromInfo(StateAniDataInfo info)
        {
            var state = StateBase.Pool.GetInPool();
            state.stateSharedData = info.sharedData;
            state.stateVariableData = new StateVariableData();
            return state;
        }

        /// <summary>
        /// 注册状态（自动从SharedData获取配置）- 智能处理键冲突
        /// </summary>
        private bool RegisterState(StateBase state, StatePipelineType pipeline, bool allowOverride = false)
        {
            var config = state.stateSharedData.basicConfig;
            string originalName = string.IsNullOrEmpty(config.stateName) ? "AutoState" : config.stateName;
            int originalId = config.stateId;

            // 处理String键冲突
            string finalName = originalName;
            if (!allowOverride)
            {
                int attempt = 0;
                while (stringToStateMap.ContainsKey(finalName))
                {
                    finalName = $"{originalName}_r{++attempt}";
                    if (StateMachineDebugSettings.Instance.alwaysLogErrors)
                        Debug.LogError($"[StateMachine] ⚠️ String键冲突! '{originalName}' → '{finalName}'");
                }
            }

            // 处理Int键冲突（传-1触发自动分配）
            if (!allowOverride && originalId > 0 && intToStateMap.ContainsKey(originalId))
            {
                config.stateId = -1; // 触发自动分配
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"⚠️ Int键冲突! ID={originalId} 已占用，自动重新分配");
            }

            // 自动分配IntKey
            int finalId = GenerateUniqueIntKey(state);

            // 执行注册
            return RegisterStateCore(finalName, finalId, state, pipeline);
        }

        /// <summary>
        /// 注册新状态（String键）- 智能处理键冲突
        /// </summary>
        public bool RegisterState(string stateKey, StateBase state, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (string.IsNullOrEmpty(stateKey))
            {
                Debug.LogError("[StateMachine] 状态键不能为空");
                return false;
            }

            // String键重复时自动添加后缀（_r1, _r2...）
            string finalStateKey = stateKey;
            int renameAttempt = 0;
            while (stringToStateMap.ContainsKey(finalStateKey))
            {
                renameAttempt++;
                finalStateKey = $"{stateKey}_r{renameAttempt}";
                Debug.LogError($"[StateMachine] ⚠️ String键冲突! '{stateKey}'已存在，自动重命名为'{finalStateKey}'");
            }

            // 自动分配IntKey（从SharedData获取或自动生成）
            int autoIntKey = GenerateUniqueIntKey(state);
            // Int键冲突时会自动跳过到下一个可用ID（GenerateUniqueIntKey内部已处理）

            return RegisterStateCore(finalStateKey, autoIntKey, state, pipeline);
        }

        /// <summary>
        /// 注册StateSharedData - 快速注册接口（支持自定义键）
        /// </summary>
        /// <param name="sharedData">状态共享数据</param>
        /// <param name="customStringKey">自定义String键（null则使用sharedData中的stateName）</param>
        /// <param name="customIntKey">自定义Int键（null则使用sharedData中的stateId）</param>
        /// <param name="allowOverride">是否允许覆盖</param>
        /// <returns>是否注册成功</returns>
        public bool RegisterStateFromSharedData(StateSharedData sharedData, string customStringKey = null, int? customIntKey = null, bool allowOverride = false)
        {
            if (sharedData == null)
            {
                if (StateMachineDebugSettings.Instance.alwaysLogErrors)
                    Debug.LogError("[StateMachine] StateSharedData为空");
                return false;
            }

            // 确保初始化
            if (!sharedData.IsRuntimeInitialized)
            {
                sharedData.InitializeRuntime();
            }

            // 创建StateBase（对象池）
            var state = StateBase.Pool.GetInPool();
            state.stateSharedData = sharedData;
            state.stateVariableData = new StateVariableData();

            // 应用自定义键或使用默认键
            string finalStringKey = customStringKey ?? sharedData.basicConfig.stateName;
            int finalIntKey = customIntKey ?? sharedData.basicConfig.stateId;
            var pipelineType = sharedData.basicConfig.pipelineType;

            // 注册
            bool registered;
            if (customStringKey != null || customIntKey.HasValue)
            {
                registered = RegisterStateCore(finalStringKey, finalIntKey, state, pipelineType);
                if (!registered && !allowOverride)
                {
                    registered = RegisterState(state, pipelineType, allowOverride);
                }
            }
            else
            {
                registered = RegisterState(state, pipelineType, allowOverride);
            }

            if (registered)
            {
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"✓ 注册SharedData状态: [{pipelineType}] {state.strKey} (ID:{state.intKey})");
            }

            return registered;
        }

        /// <summary>
        /// 注册新状态（Int键）
        /// </summary>
        public bool RegisterState(int stateKey, StateBase state, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (state == null)
            {
                Debug.LogError($"状态实例不能为空: {stateKey}");
                return false;
            }
            if (intToStateMap.ContainsKey(stateKey))
            {
                Debug.LogWarning($"状态ID {stateKey} 已存在，跳过注册");
                return false;
            }

            // 自动生成StringKey（从SharedData获取或自动生成）
            string autoStrKey = GenerateUniqueStringKey(state);
            if (stringToStateMap.ContainsKey(autoStrKey))
            {
                Debug.LogWarning($"自动生成的StringKey {autoStrKey} 已存在，跳过注册");
                return false;
            }

            return RegisterStateCore(autoStrKey, stateKey, state, pipeline);
        }

        /// <summary>
        /// 同时注册String和Int键
        /// </summary>
        public bool RegisterState(string stringKey, int intKey, StateBase state, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (string.IsNullOrEmpty(stringKey))
            {
                Debug.LogError("状态键不能为空");
                return false;
            }

            if (state == null)
            {
                Debug.LogError($"状态实例不能为空: {stringKey}");
                return false;
            }

            if (stringToStateMap.ContainsKey(stringKey))
            {
                Debug.LogWarning($"状态 {stringKey} 已存在，跳过注册");
                return false;
            }

            if (intToStateMap.ContainsKey(intKey))
            {
                Debug.LogWarning($"状态ID {intKey} 已存在，跳过注册");
                return false;
            }

            return RegisterStateCore(stringKey, intKey, state, pipeline);
        }

        /// <summary>
        /// 注销状态（String键）
        /// </summary>
        public bool UnregisterState(string stateKey)
        {
            if (!stringToStateMap.TryGetValue(stateKey, out var state))
            {
                return false;
            }

            return UnregisterStateCore(state);
        }

        /// <summary>
        /// 生成唯一的IntKey - 智能处理冲突
        /// </summary>
        private int GenerateUniqueIntKey(StateBase state)
        {
            // 优先从SharedData.basicConfig.stateId获取
            if (state?.stateSharedData?.basicConfig != null)
            {
                int configId = state.stateSharedData.basicConfig.stateId;

                // 如果配置ID为-1，表示需要自动分配
                if (configId == -1)
                {
                    StateMachineDebugSettings.Instance.LogStateTransition(
                        $"状态'{state.stateSharedData.basicConfig.stateName}' ID=-1，触发自动分配");
                }
                else if (configId > 0 && !intToStateMap.ContainsKey(configId))
                {
                    return configId;
                }
                else if (configId > 0 && intToStateMap.ContainsKey(configId))
                {
                    if (StateMachineDebugSettings.Instance.alwaysLogErrors)
                        Debug.LogWarning($"[StateMachine] ⚠️ IntKey冲突! ID={configId} 已被'{intToStateMap[configId].strKey}'占用");
                }
            }

            // 自动分配新ID（从10000开始避免冲突）
            while (intToStateMap.ContainsKey(_nextAutoIntId))
            {
                _nextAutoIntId++;
            }
            int newId = _nextAutoIntId++;
            StateMachineDebugSettings.Instance.LogStateTransition($"✓ 自动分配IntKey: {newId}");
            return newId;
        }

        /// <summary>
        /// 生成唯一的StringKey
        /// </summary>
        private string GenerateUniqueStringKey(StateBase state)
        {
            // 优先从SharedData.basicConfig.stateName获取
            if (state?.stateSharedData?.basicConfig != null)
            {
                string configName = state.stateSharedData.basicConfig.stateName;
                if (!string.IsNullOrEmpty(configName) && !stringToStateMap.ContainsKey(configName))
                {
                    return configName;
                }
            }

            // 自动分配
            string baseName = "State";
            string candidateName;
            do
            {
                candidateName = $"{baseName}_{_nextAutoStringIdSuffix++}";
            }
            while (stringToStateMap.ContainsKey(candidateName));

            return candidateName;
        }

        /// <summary>
        /// 检查并设置Fallback状态
        /// </summary>
        private void CheckAndSetFallbackState(StateBase state, StatePipelineType pipeline)
        {
            if (state?.stateSharedData?.basicConfig == null) return;

            // 检查是否可以作为Fallback状态
            if (state.stateSharedData.basicConfig.canBeFeedback)
            {
                // 获取Fallback支持标记
                var fallbackFlag = state.stateSharedData.basicConfig.fallbackSupportFlag;

                // 获取目标流水线运行时
                var pipelineRuntime = GetPipelineByType(pipeline);
                if (pipelineRuntime != null)
                {
                    pipelineRuntime.SetFallBack(state.intKey, fallbackFlag);
                    Debug.Log($"[FallBack-Register] ✓ [{pipeline}] Flag={fallbackFlag} <- State '{state.strKey}' (ID:{state.intKey})");
                }
            }
        }

        /// <summary>
        /// 注册状态核心逻辑（私有，供三个RegisterState重载调用）
        /// </summary>
        private bool RegisterStateCore(string stringKey, int intKey, StateBase state, StatePipelineType pipeline)
        {
            // 同时注册到两个字典
            stringToStateMap[stringKey] = state;
            intToStateMap[intKey] = state;
            state.strKey = stringKey;
            state.intKey = intKey;
            state.host = this;
            statePipelineMap[state] = pipeline;
            if (!_registeredStatesList.Contains(state))
            {
                _registeredStatesList.Add(state);
            }

            // 检查并设置Fallback状态
            CheckAndSetFallbackState(state, pipeline);

            // 如果状态有动画，初始化Calculator（享元数据预计算）
            if (state.stateSharedData.hasAnimation && state.stateSharedData.animationConfig?.calculator != null)
            {
                try
                {
                    state.stateSharedData.animationConfig.calculator.InitializeCalculator();
                    StateMachineDebugSettings.Instance.LogRuntimeInit(
                        $"✓ Calculator初始化: {stringKey} - {state.stateSharedData.animationConfig.calculator.GetType().Name}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[StateMachine] Calculator初始化失败: {stringKey}\n{e}");
                }
            }

            MarkDirty(StateDirtyReason.RuntimeChanged);

            if (isInitialized)
            {
                InitializeState(state);
            }

            Debug.Log($"[StateMachine] 注册状态: {stringKey} (IntKey:{intKey}, Pipeline:{pipeline})");
            return true;
        }

        /// <summary>
        /// 注销状态核心逻辑（私有，供UnregisterState重载调用）
        /// </summary>
        private bool UnregisterStateCore(StateBase state)
        {
            if (state == null) return false;
            // 如果状态正在运行，先停用
            if (runningStates.Contains(state))
            {
                TryDeactivateState(state.strKey);
            }

            // 同时从两个字典移除
            if (!string.IsNullOrEmpty(state.strKey))
            {
                stringToStateMap.Remove(state.strKey);
            }
            if (state.intKey != -1)
            {
                intToStateMap.Remove(state.intKey);
            }

            transitionCache.Remove(state.strKey);
            statePipelineMap.Remove(state);
            _activationCache.Remove(state);
            _registeredStatesList.Remove(state);
            MarkDirty(StateDirtyReason.Release);
            state.TryAutoPushedToPool();
            return true;
        }

        /// <summary>
        /// 注销状态（Int键）
        /// </summary>
        public bool UnregisterState(int stateKey)
        {
            if (!intToStateMap.TryGetValue(stateKey, out var state))
            {
                return false;
            }

            return UnregisterStateCore(state);
        }

        /// <summary>
        /// 获取状态（通过String键）
        /// </summary>
        public StateBase GetStateByString(string stateKey)
        {
            if (string.IsNullOrEmpty(stateKey)) return null;

            // 优先从缓存获取
            if (transitionCache.TryGetValue(stateKey, out var cachedState))
            {
                return cachedState;
            }

            // 从字典获取并缓存
            if (stringToStateMap.TryGetValue(stateKey, out var state))
            {
                transitionCache[stateKey] = state;
                return state;
            }

            return null;
        }

        /// <summary>
        /// 获取状态（通过Int键）
        /// </summary>
        public StateBase GetStateByInt(int stateKey)
        {
            return intToStateMap.TryGetValue(stateKey, out var state) ? state : null;
        }

        /// <summary>
        /// 检查状态是否存在（String键）
        /// </summary>
        public bool HasState(string stateKey)
        {
            return stringToStateMap.ContainsKey(stateKey);
        }

        /// <summary>
        /// 检查状态是否存在（Int键）
        /// </summary>
        public bool HasState(int stateKey)
        {
            return intToStateMap.ContainsKey(stateKey);
        }

        /// <summary>
        /// 设置Fallback状态（按支持标记）
        /// </summary>
        public void SetFallbackState(StatePipelineType pipelineType, int stateId, StateSupportFlags supportFlag = StateSupportFlags.None)
        {
            var pipeline = GetPipelineByType(pipelineType);
            if (pipeline != null)
            {
                pipeline.SetFallBack(stateId, supportFlag);
            }
        }

        /// <summary>
        /// 获取流水线
        /// </summary>
        public StatePipelineRuntime GetPipeline(StatePipelineType pipelineType)
        {
            return GetPipelineByType(pipelineType);
        }

        /// <summary>
        /// 设置流水线权重
        /// </summary>
        public void SetPipelineWeight(StatePipelineType pipelineType, float weight)
        {
            var pipeline = GetPipelineByType(pipelineType);
            if (pipeline != null)
            {
                pipeline.weight = Mathf.Clamp01(weight);
                // 更新Playable权重
                pipeline.UpdatePipelineMixer();
            }
        }

        #endregion

        #region 临时动画热拔插（可修改）

        /// <summary>
        /// 临时动画状态跟踪
        /// </summary>
        [NonSerialized]
        private Dictionary<string, StateBase> _temporaryStates = new Dictionary<string, StateBase>();

#if UNITY_EDITOR
        // === 编辑器测试字段 ===
        [FoldoutGroup("临时动画测试", expanded: false)]
        [LabelText("测试键"), Tooltip("临时状态的唯一标识")]
        public string testTempKey = "TestAnim";

        [FoldoutGroup("临时动画测试")]
        [LabelText("测试Clip"), AssetsOnly]
        public AnimationClip testClip;

        [FoldoutGroup("临时动画测试")]
        [LabelText("目标管线")]
        public StatePipelineType testPipeline = StatePipelineType.Main;

        [FoldoutGroup("临时动画测试")]
        [LabelText("播放速度"), Range(0.1f, 3f)]
        public float testSpeed = 1.0f;

        [FoldoutGroup("临时动画测试")]
        [LabelText("循环播放"), Tooltip("勾选后动画循环播放，不勾选则播放一次后自动退出")]
        public bool testLoopable = false;

        [FoldoutGroup("临时动画测试")]
        [Button("添加临时动画", ButtonSizes.Medium), GUIColor(0.4f, 0.8f, 1f)]
        private void EditorAddTemporaryAnimation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("请在运行时测试！");
                return;
            }

            if (testClip == null)
            {
                Debug.LogError("请先指定Clip！");
                return;
            }

            AddTemporaryAnimation(testTempKey, testClip, testPipeline, testSpeed, testLoopable);
        }

        [FoldoutGroup("临时动画测试")]
        [Button("移除临时动画", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.4f)]
        private void EditorRemoveTemporaryAnimation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("请在运行时测试！");
                return;
            }

            RemoveTemporaryAnimation(testTempKey);
        }
#endif

        /// <summary>
        /// 添加临时动画 - 快速热拔插（自动注册+激活）
        /// </summary>
        /// <param name="tempKey">临时状态键（唯一标识）</param>
        /// <param name="clip">动画Clip</param>
        /// <param name="pipeline">目标流水线</param>
        /// <param name="speed">播放速度</param>
        /// <param name="loopable">是否循环播放（false=播放一次后自动退出，true=持续循环）</param>
        /// <returns>是否添加成功</returns>
        public bool AddTemporaryAnimation(string tempKey, AnimationClip clip, StatePipelineType pipeline = StatePipelineType.Main, float speed = 1.0f, bool loopable = false)
        {
            if (string.IsNullOrEmpty(tempKey))
            {
                Debug.LogError("[TempAnim] 临时状态键不能为空");
                return false;
            }

            if (clip == null)
            {
                Debug.LogError("[TempAnim] AnimationClip不能为空");
                return false;
            }

            // 检查是否已存在
            if (_temporaryStates.ContainsKey(tempKey))
            {
                Debug.LogWarning($"[TempAnim] 临时状态 {tempKey} 已存在，先移除旧的");
                RemoveTemporaryAnimation(tempKey);
            }

            // 创建临时StateBase
            var tempState = StateBase.Pool.GetInPool();
            tempState.strKey = $"__temp_{tempKey}";
            tempState.intKey = -1;

            // 创建SharedData
            tempState.stateSharedData = new StateSharedData();
            tempState.stateSharedData.hasAnimation = true;

            // 创建BasicConfig（根据loopable配置播放模式）
            tempState.stateSharedData.basicConfig = new StateBasicConfig();
            tempState.stateSharedData.basicConfig.stateName = tempKey;
            tempState.stateSharedData.basicConfig.durationMode = loopable
                ? StateDurationMode.Infinite  // 循环播放
                : StateDurationMode.UntilAnimationEnd; // 播放一次后自动退出
            tempState.stateSharedData.basicConfig.pipelineType = pipeline;

            // 创建AnimationConfig
            tempState.stateSharedData.animationConfig = new StateAnimationConfigData();
            var calculator = new StateAnimationMixCalculatorForSimpleClip
            {
                clip = clip,
                speed = speed
            };
            tempState.stateSharedData.animationConfig.calculator = calculator;

            // 初始化SharedData
            tempState.stateSharedData.InitializeRuntime();

            // 注册状态
            if (!RegisterState(tempState.strKey, tempState, pipeline))
            {
                Debug.LogError($"[TempAnim] 注册临时状态失败: {tempKey}");
                return false;
            }

            // 激活状态
            if (!TryActivateState(tempState, pipeline))
            {
                Debug.LogError($"[TempAnim] 激活临时状态失败: {tempKey}");
                UnregisterState(tempState.strKey);
                return false;
            }

            // 记录到临时状态集合
            _temporaryStates[tempKey] = tempState;
            Debug.Log($"[TempAnim] ✓ 添加临时动画: {tempKey} | Clip:{clip.name} | Pipeline:{pipeline}");
            return true;
        }

        /// <summary>
        /// 移除临时动画
        /// </summary>
        /// <param name="tempKey">临时状态键</param>
        /// <returns>是否移除成功</returns>
        public bool RemoveTemporaryAnimation(string tempKey)
        {
            if (!_temporaryStates.TryGetValue(tempKey, out var tempState))
            {
                Debug.LogWarning($"[TempAnim] 临时状态 {tempKey} 不存在");
                return false;
            }

            // 停用并注销状态
            if (runningStates.Contains(tempState))
            {
                TryDeactivateState(tempState.strKey);
            }
            UnregisterState(tempState.strKey);

            // 从临时集合移除
            _temporaryStates.Remove(tempKey);
            Debug.Log($"[TempAnim] ✓ 移除临时动画: {tempKey}");
            return true;
        }

        /// <summary>
        /// 一键清除所有临时动画
        /// </summary>
        public void ClearAllTemporaryAnimations()
        {
            if (_temporaryStates.Count == 0)
            {
                Debug.Log("[TempAnim] 没有临时动画需要清除");
                return;
            }

            Debug.Log($"[TempAnim] 开始清除 {_temporaryStates.Count} 个临时动画");

            // 复制键列表避免迭代时修改字典
            var keys = _temporaryKeysCache;
            keys.Clear();
            foreach (var key in _temporaryStates.Keys)
            {
                keys.Add(key);
            }
            for (int i = 0; i < keys.Count; i++)
            {
                RemoveTemporaryAnimation(keys[i]);
            }

            _temporaryStates.Clear();
            Debug.Log("[TempAnim] ✓ 所有临时动画已清除");
        }

        /// <summary>
        /// 检查临时动画是否存在
        /// </summary>
        public bool HasTemporaryAnimation(string tempKey)
        {
            return _temporaryStates.ContainsKey(tempKey);
        }

        /// <summary>
        /// 获取临时动画数量
        /// </summary>
        public int GetTemporaryAnimationCount()
        {
            return _temporaryStates.Count;
        }

        /// <summary>
        /// 广播动画事件
        /// 由StateBase调用，通知外部监听者
        /// </summary>
        /// <param name="state">触发事件的状态</param>
        /// <param name="eventName">事件名称</param>
        /// <param name="eventParam">事件参数</param>
        public void BroadcastAnimationEvent(StateBase state, string eventName, string eventParam)
        {
            // 调用回调
            OnAnimationEvent?.Invoke(state, eventName, eventParam);

            // 也可以通过Entity广播
            if (hostEntity != null)
            {
                // 假设Entity有事件系统
                // hostEntity.BroadcastEvent(eventName, eventParam);
            }

            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[StateMachine] 广播动画事件: {eventName} | State: {state?.strKey} | Param: {eventParam}");
        }

        #endregion

        #region 状态激活测试与执行（核心/谨慎改）

        /// <summary>
        /// 测试状态能否激活（不执行）
        /// </summary>
        // TODO: [用户修改] 合并测试核心方法 - 需要优化合并判断逻辑
        // 修改点：
        // 1. 完善 CheckStateMergeCompatibility 的判断规则
        // 2. 考虑优先级、代价、通道占用等因素
        // 3. 添加自定义合并策略支持
        public StateActivationResult TestStateActivation(StateBase targetState, StatePipelineType pipeline = StatePipelineType.NotClear)
        {
            if (targetState == null)
            {
                return StateActivationResult.Failure(StateFailureReasons.StateIsNull);
            }

            pipeline = ResolvePipelineForState(targetState, pipeline);

            if (!isRunning)
            {
                return StateActivationResult.Failure(StateFailureReasons.MachineNotRunning);
            }

            var basicConfig = targetState.stateSharedData?.basicConfig;
            if (basicConfig != null && !basicConfig.ignoreSupportFlags)
            {
                var requiredFlags = basicConfig.requiredSupportFlags;
                if ((currentSupportFlags & requiredFlags) != requiredFlags)
                {
                    return StateActivationResult.Failure(StateFailureReasons.SupportFlagsNotSatisfied);
                }
            }

            if (CustomActivationTest != null)
            {
                return CustomActivationTest(targetState, pipeline);
            }

            int pipelineCount = (int)StatePipelineType.Count;
            int pipelineIndex = (int)pipeline;
            if (pipelineIndex < 0 || pipelineIndex >= pipelineCount)
            {
                return StateActivationResult.Failure(StateFailureReasons.InvalidPipelineIndex);
            }

            var cache = GetOrCreateActivationCache(targetState);
            if (cache != null && cache.versions[pipelineIndex] == _dirtyVersion)
            {
                return cache.results[pipelineIndex];
            }

            // 检查该状态是否已在运行
            if (runningStates.Contains(targetState))
            {
                var failure = StateActivationResult.Failure(StateFailureReasons.StateAlreadyRunning);
                if (cache != null)
                {
                    cache.results[pipelineIndex] = failure;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return failure;
            }

            // 获取目标流水线
            var targetPipeline = GetPipelineByType(pipeline);
            if (targetPipeline == null)
            {
                var failure = StateActivationResult.Failure(StateFailureReasons.PipelineNotFound);
                if (cache != null)
                {
                    cache.results[pipelineIndex] = failure;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return failure;
            }

            if (!targetPipeline.isEnabled)
            {
                var failure = StateActivationResult.Failure(StateFailureReasons.PipelineDisabled);
                if (cache != null)
                {
                    cache.results[pipelineIndex] = failure;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return failure;
            }

            // 获取该流水线中当前运行的状态
            var pipelineStates = targetPipeline.runningStates;

            var allRunningStates = GetCachedRunningStates();
            if (allRunningStates.Count == 0)
            {
                var success = StateActivationResult.Success(pipeline, false);
                if (cache != null)
                {
                    cache.results[pipelineIndex] = success;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return success;
            }

            // 检查合并和冲突（复用列表，减少GC）
            var interruptList = cache != null ? cache.interruptLists[pipelineIndex] : _tmpInterruptStates;
            var mergeList = cache != null ? cache.mergeLists[pipelineIndex] : _tmpMergeStates;
            interruptList.Clear();
            mergeList.Clear();

            foreach (var existingState in allRunningStates)
            {
                if (existingState == null || targetState == null)
                {
                    continue;
                }

                var mergeResult = CheckStateMergeCompatibility(existingState, targetState);
                bool isInTargetPipeline = pipelineStates.Contains(existingState);
                if (!isInTargetPipeline)
                {
                    if (mergeResult == StateMergeResult.MergeComplete)
                    {
                        continue;
                    }

                    var failure = StateActivationResult.Failure($"跨流水线冲突：{targetState.strKey} 与 {existingState.strKey} 不可并行");
                    if (cache != null)
                    {
                        cache.results[pipelineIndex] = failure;
                        cache.versions[pipelineIndex] = _dirtyVersion;
                    }
                    return failure;
                }

                switch (mergeResult)
                {
                    case StateMergeResult.MergeComplete:
                        mergeList.Add(existingState);
                        break;
                    case StateMergeResult.HitAndReplace:
                    case StateMergeResult.TryWeakInterrupt:
                        interruptList.Add(existingState);
                        break;
                    case StateMergeResult.MergeFail:
                    default:
                        {
                            var failure = StateActivationResult.Failure($"合并失败：{targetState.strKey} 与 {existingState.strKey} 冲突");
                            if (cache != null)
                            {
                                cache.results[pipelineIndex] = failure;
                                cache.versions[pipelineIndex] = _dirtyVersion;
                            }
                            return failure;
                        }
                }
            }

            // 如果可以合并
            if (mergeList.Count > 0 && interruptList.Count == 0)
            {
                var success = new StateActivationResult
                {
                    canActivate = true,
                    requiresInterruption = false,
                    canMerge = true,
                    mergeDirectly = true,
                    statesToInterrupt = interruptList,
                    statesToMergeWith = mergeList,
                    interruptCount = 0,
                    mergeCount = mergeList.Count,
                    failureReason = string.Empty,
                    targetPipeline = pipeline
                };
                if (cache != null)
                {
                    cache.results[pipelineIndex] = success;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return success;
            }

            // 如果需要打断
            if (interruptList.Count > 0)
            {
                var success = new StateActivationResult
                {
                    canActivate = true,
                    requiresInterruption = true,
                    canMerge = false,
                    mergeDirectly = false,
                    statesToInterrupt = interruptList,
                    statesToMergeWith = mergeList,
                    interruptCount = interruptList.Count,
                    mergeCount = 0,
                    failureReason = string.Empty,
                    targetPipeline = pipeline
                };
                if (cache != null)
                {
                    cache.results[pipelineIndex] = success;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return success;
            }

            var defaultSuccess = new StateActivationResult
            {
                canActivate = true,
                requiresInterruption = false,
                canMerge = false,
                mergeDirectly = false,
                statesToInterrupt = interruptList,
                statesToMergeWith = mergeList,
                interruptCount = 0,
                mergeCount = 0,
                failureReason = string.Empty,
                targetPipeline = pipeline
            };
            if (cache != null)
            {
                cache.results[pipelineIndex] = defaultSuccess;
                cache.versions[pipelineIndex] = _dirtyVersion;
            }
            return defaultSuccess;
        }

        /// <summary>
        /// 更新流水线的MainState - 选择总代价最高的状态
        /// </summary>
        private void UpdatePipelineMainState(StatePipelineRuntime pipeline)
        {
            if (pipeline == null || pipeline.runningStates.Count == 0)
            {
                if (pipeline != null) pipeline.mainState = null;
                return;
            }

            StateBase bestState = null;
            float bestScore = float.MinValue;
            byte bestPriority = 0;

            foreach (var state in pipeline.runningStates)
            {
                var basic = state?.stateSharedData?.basicConfig;
                if (basic == null) continue;

                float score = GetMainStateScore(state);
                byte priority = basic.priority;

                if (bestState == null)
                {
                    bestState = state;
                    bestScore = score;
                    bestPriority = priority;
                    continue;
                }

                if (score > bestScore)
                {
                    bestState = state;
                    bestScore = score;
                    bestPriority = priority;
                    continue;
                }

                if (Mathf.Approximately(score, bestScore))
                {
                    if (priority > bestPriority)
                    {
                        bestState = state;
                        bestScore = score;
                        bestPriority = priority;
                        continue;
                    }

                    if (priority == bestPriority && CompareStateDeterministic(state, bestState) < 0)
                    {
                        bestState = state;
                        bestScore = score;
                        bestPriority = priority;
                    }
                }
            }

            // 如果没有有效评分的状态，选择确定性的第一个
            pipeline.mainState = bestState ?? GetFirstRunningState(pipeline);
        }

        /// <summary>
        /// 检查两个状态是否可以合并
        /// </summary>
        /// TODO: [用户修改] 合并兼容性检查 - 需要实现详细的合并规则
        /// 修改点：
        /// 1. 检查 StateMergeData.stateChannelMask 是否冲突
        /// 2. 检查 exclusiveTags 是否互斥
        /// 3. 检查优先级和代价是否允许合并
        /// 4. 考虑自定义合并策略（CanMergeEvaluator）
        private StateMergeResult CheckStateMergeCompatibility(StateBase existing, StateBase incoming)
        {
            if (CanMergeEvaluator != null)
            {
                return CanMergeEvaluator(existing, incoming) ? StateMergeResult.MergeComplete : StateMergeResult.HitAndReplace;
            }

            if (existing == null || incoming == null || existing == incoming)
                return StateMergeResult.MergeFail;

            var leftShared = existing.stateSharedData;
            var rightShared = incoming.stateSharedData;
            var leftMerge = leftShared?.mergeData;
            var rightMerge = rightShared?.mergeData;

            // 无条件规则优先
            var unconditional = ResolveUnconditionalRule(leftShared?.basicConfig, leftMerge?.asLeftRule, incoming)
                              ?? ResolveUnconditionalRule(rightShared?.basicConfig, rightMerge?.asRightRule, existing);
            if (unconditional.HasValue)
                return unconditional.Value;

            // 若缺少合并配置，默认允许合并
            if (leftMerge == null || rightMerge == null)
                return StateMergeResult.MergeComplete;

            // 通道冲突检查
            bool channelOverlap = (leftMerge.stateChannelMask & rightMerge.stateChannelMask) != StateChannelMask.None;
            if (channelOverlap)
            {
                // 若通道总代价允许共存，优先合并
                if (CanMergeByChannelOverlap(GetPipelineByType(rightShared?.basicConfig?.pipelineType ?? StatePipelineType.Basic), incoming))
                    return StateMergeResult.MergeComplete;

                // 层级规则
                var leftLevel = GetStayLevelValue(leftMerge.stayLevel);
                var rightLevel = GetStayLevelValue(rightMerge.stayLevel);

                if (rightMerge.asRightRule != null && rightMerge.asRightRule.hitByLayerOption == StateHitByLayerOption.Never)
                    return StateMergeResult.MergeFail;

                if (rightMerge.asRightRule != null && rightMerge.asRightRule.hitByLayerOption == StateHitByLayerOption.OnlyLayerCrush)
                {
                    return rightLevel > leftLevel ? StateMergeResult.HitAndReplace : StateMergeResult.MergeFail;
                }

                // SameLevelTest: 层级优先，层级相同则走优先级规则
                if (rightLevel > leftLevel)
                    return StateMergeResult.HitAndReplace;

                if (Mathf.Approximately(rightLevel, leftLevel))
                {
                    byte rightPriority = rightShared?.basicConfig?.priority ?? (byte)0;
                    byte leftPriority = leftShared?.basicConfig?.priority ?? (byte)0;
                    bool equalEffective = rightMerge.asRightRule != null && rightMerge.asRightRule.EqualIsEffectial;

                    if (rightPriority > leftPriority || (equalEffective && rightPriority == leftPriority))
                        return StateMergeResult.HitAndReplace;
                }

                return StateMergeResult.MergeFail;
            }

            // 无通道冲突，允许合并
            return StateMergeResult.MergeComplete;
        }

        private StateMergeResult? ResolveUnconditionalRule(StateBasicConfig selfBasic, NormalMergeRule rule, StateBase other)
        {
            if (rule == null || !rule.enableUnconditionalRule || rule.unconditionalRule == null || other == null)
                return null;

            foreach (var item in rule.unconditionalRule)
            {
                if (item == null) continue;

                bool nameMatch = !string.IsNullOrEmpty(item.stateName) && item.stateName == other.strKey;
                bool idMatch = item.stateID >= 0 && other.intKey == item.stateID;

                if (!nameMatch && !idMatch)
                    continue;

                switch (item.matchBackType)
                {
                    case UnconditionalMatchBackType.Accept:
                        return StateMergeResult.MergeComplete;
                    case UnconditionalMatchBackType.Reject:
                        return StateMergeResult.MergeFail;
                    case UnconditionalMatchBackType.Replace:
                        return StateMergeResult.HitAndReplace;
                }
            }

            return null;
        }

        private static float GetStayLevelValue(StateStayLevel level)
        {
            return (float)level;
        }

        /// <summary>
        /// 检查流水线中的状态是否可以与新状态合并
        /// 基于Channel重合度：如果总代价不超过1，则可以合并
        /// </summary>
        /// TODO: [用户修改] 基于通道的合并检查 - 需要调整代价阈值和合并规则
        /// 修改点：
        /// 1. totalOverlapCost <= 1.0f 的阈值是否合理？
        /// 2. 是否需要考虑不同通道的权重？
        /// 3. 是否需要支持动态调整阈值？
        private bool CanMergeByChannelOverlap(StatePipelineRuntime pipeline, StateBase incomingState)
        {
            if (pipeline == null || incomingState?.stateSharedData == null) return false;

            var incomingMergeData = incomingState.stateSharedData.mergeData;
            var incomingCostData = incomingState.stateSharedData.costData;
            if (incomingMergeData == null || incomingCostData == null) return false;

            float totalOverlapCost = 0f;

            // 遍历流水线中的所有状态
            foreach (var existingState in pipeline.runningStates)
            {
                if (existingState?.stateSharedData == null) continue;

                var existingMergeData = existingState.stateSharedData.mergeData;
                var existingCostData = existingState.stateSharedData.costData;
                if (existingMergeData == null || existingCostData == null) continue;

                // 检查Channel是否有重合
                StateChannelMask overlap = existingMergeData.stateChannelMask & incomingMergeData.stateChannelMask;
                if (overlap != StateChannelMask.None)
                {
                    // 有重合，累加代价
                    if (existingCostData.enableCostCalculation)
                    {
                        totalOverlapCost += existingCostData.GetTotalCost();
                    }
                }
            }

            // 加上新状态的代价
            if (incomingCostData.enableCostCalculation)
            {
                totalOverlapCost += incomingCostData.GetTotalCost();
            }

            // 如果总代价不超过1，则可以合并
            return totalOverlapCost <= 1.0f;
        }

        /// <summary>
        /// 执行状态激活（根据测试结果）
        /// </summary>
        /// TODO: [用户修改] 执行激活逻辑 - 需要验证合并执行流程
        /// 修改点：
        /// 1. 验证 result.canMerge 和 result.mergeDirectly 的处理逻辑
        /// 2. 确认合并时的权重分配和动画混合
        /// 3. 确认打断和合并的执行顺序
        /// 4. 添加合并失败的回滚机制
        public bool ExecuteStateActivation(StateBase targetState, StateActivationResult result)
        {
            Debug.Log($"[StateMachine] === 开始执行状态激活 ===");
            Debug.Log($"[StateMachine]   状态: {targetState?.strKey} (ID:{targetState?.intKey})");
            Debug.Log($"[StateMachine]   目标管线: {result.targetPipeline}");

            var basicConfig = targetState?.stateSharedData?.basicConfig;

            if (!result.canActivate)
            {
                Debug.LogWarning($"[StateMachine] ✗ 状态激活失败: {result.failureReason}");
                return false;
            }

            var pipeline = GetPipelineByType(result.targetPipeline);
            if (pipeline == null)
            {
                Debug.LogError($"[StateMachine] ✗ 获取流水线失败: {result.targetPipeline}");
                return false;
            }

            Debug.Log($"[StateMachine]   流水线状态: Mixer有效={pipeline.mixer.IsValid()}, 运行状态数={pipeline.runningStates.Count}");

            // 执行打断
            if (result.requiresInterruption && result.statesToInterrupt != null)
            {
                Debug.Log($"[StateMachine]   打断 {result.statesToInterrupt.Count} 个状态");
                foreach (var stateToInterrupt in result.statesToInterrupt)
                {
                    TruelyDeactivateState(stateToInterrupt, result.targetPipeline);
                }
            }

            // 激活目标状态
            targetState.OnStateEnter();
            runningStates.Add(targetState);
            pipeline.runningStates.Add(targetState);
            Debug.Log($"[StateMachine]   ✓ 状态已添加到运行集合");

            // 如果状态有动画，热插拔到Playable图
            HotPlugStateToPlayable(targetState, pipeline);

            // ★ 应用淡入逻辑（如果启用）
            ApplyFadeIn(targetState, pipeline);

            // 重新评估MainState
            UpdatePipelineMainState(pipeline);


            OnStateEntered?.Invoke(targetState, result.targetPipeline);
            MarkDirty(StateDirtyReason.Enter);

            Debug.Log($"[StateMachine] === 状态激活完成 ===");
            return true;
        }

        /// <summary>
        /// 尝试激活状态（通过键）
        /// </summary>
        public bool TryActivateState(string stateKey, StatePipelineType pipeline = StatePipelineType.NotClear)
        {
            var state = GetStateByString(stateKey);
            if (state == null)
            {
                Debug.LogWarning($"状态 {stateKey} 不存在");
                return false;
            }

            return TryActivateState(state, pipeline);
        }

        /// <summary>
        /// 尝试激活状态（通过Int键）
        /// </summary>
        public bool TryActivateState(int stateKey, StatePipelineType pipeline = StatePipelineType.NotClear)
        {
            var state = GetStateByInt(stateKey);
            if (state == null)
            {
                Debug.LogWarning($"状态ID {stateKey} 不存在");
                return false;
            }

            return TryActivateState(state, pipeline);
        }

        /// <summary>
        /// 尝试激活状态（通过实例 + 指定流水线）
        /// </summary>
        public bool TryActivateState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.NotClear)
        {
            if (targetState == null) return false;

            pipeline = ResolvePipelineForState(targetState, pipeline);
            var result = TestStateActivation(targetState, pipeline);
            return ExecuteStateActivation(targetState, result);
        }

        /// <summary>
        /// 尝试激活状态（通过实例，使用注册时的默认流水线）
        /// </summary>
        public bool TryActivateState(StateBase targetState)
        {
            if (targetState == null) return false;

            return TryActivateState(targetState, StatePipelineType.NotClear);
        }

        /// <summary>
        /// 停用状态（内部方法）
        /// </summary>
        private void TruelyDeactivateState(StateBase state, StatePipelineType pipeline)
        {
            if (state == null) return;

            // ★ 应用淡出逻辑（如果启用）
            var pipelineData = GetPipelineByType(pipeline);
            if (pipelineData != null)
            {
                ApplyFadeOut(state, pipelineData);
            }

            // 若启用淡出，则由淡出完成时统一卸载
            bool useDirectBlend = state.stateSharedData.basicConfig?.useDirectBlend == true;
            if (pipelineData != null && (!state.stateSharedData.enableFadeInOut || state.stateSharedData.fadeOutDuration <= 0f || useDirectBlend))
            {
                HotUnplugStateFromPlayable(state, pipelineData);
            }

            state.OnStateExit();
            runningStates.Remove(state);

            if (pipelineData != null)
            {
                pipelineData.runningStates.Remove(state);

                // 重新评估MainState
                UpdatePipelineMainState(pipelineData);

                // 标记FallBack检查
                pipelineData.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            }

            OnStateExited?.Invoke(state, pipeline);
            MarkDirty(StateDirtyReason.Exit);
        }

        /// <summary>
        /// 尝试停用状态（通过String键）
        /// </summary>
        public bool TryDeactivateState(string stateKey)
        {
            var state = GetStateByString(stateKey);
            if (state == null || !runningStates.Contains(state))
            {
                return false;
            }

            // 查找该状态所在的流水线 - 零GC优化
            foreach (var pipeline in GetAllPipelines())
            {
                if (pipeline.runningStates.Contains(state))
                {
                    TruelyDeactivateState(state, pipeline.pipelineType);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试停用状态（通过Int键）
        /// </summary>
        public bool TryDeactivateState(int stateKey)
        {
            var state = GetStateByInt(stateKey);
            if (state == null || !runningStates.Contains(state))
            {
                return false;
            }

            // 查找该状态所在的流水线 - 零GC优化
            foreach (var pipeline in GetAllPipelines())
            {
                if (pipeline.runningStates.Contains(state))
                {
                    TruelyDeactivateState(state, pipeline.pipelineType);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 进入验证测试（不执行）
        /// </summary>
        public StateActivationResult TestEnterState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.NotClear)
        {
            return TestStateActivation(targetState, pipeline);
        }

        /// <summary>
        /// 测试进入（验证后执行进入）
        /// </summary>
        public bool TryEnterState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.NotClear)
        {
            return TryActivateState(targetState, pipeline);
        }

        /// <summary>
        /// 强制进入（不做验证）
        /// </summary>
        public bool ForceEnterState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.NotClear)
        {
            if (targetState == null) return false;

            pipeline = ResolvePipelineForState(targetState, pipeline);
            var pipelineData = GetPipelineByType(pipeline);
            if (pipelineData == null)
            {
                return false;
            }

            _tmpStateBuffer.Clear();
            _tmpStateBuffer.AddRange(pipelineData.runningStates);
            foreach (var state in _tmpStateBuffer)
            {
                TruelyDeactivateState(state, pipeline);
            }

            targetState.OnStateEnter();
            runningStates.Add(targetState);
            pipelineData.runningStates.Add(targetState);

            // 重新评估MainState
            UpdatePipelineMainState(pipelineData);

            OnStateEntered?.Invoke(targetState, pipeline);
            return true;
        }

        /// <summary>
        /// 退出验证测试（不执行）
        /// </summary>
        public StateExitResult TestExitState(StateBase targetState)
        {
            if (targetState == null)
            {
                return StateExitResult.Failure("目标状态为空", StatePipelineType.Basic);
            }

            if (!runningStates.Contains(targetState))
            {
                return StateExitResult.Failure("状态未在运行中", StatePipelineType.Basic);
            }

            if (!statePipelineMap.TryGetValue(targetState, out var pipeline))
            {
                pipeline = StatePipelineType.Basic;
            }

            if (CustomExitTest != null)
            {
                return CustomExitTest(targetState, pipeline);
            }

            return StateExitResult.Success(pipeline);
        }

        /// <summary>
        /// 测试退出（验证后执行退出）
        /// </summary>
        public bool TryExitState(StateBase targetState)
        {
            var result = TestExitState(targetState);
            if (!result.canExit)
            {
                return false;
            }

            TruelyDeactivateState(targetState, result.pipeline);
            return true;
        }

        /// <summary>
        /// 强制退出（不做验证）
        /// </summary>
        public void ForceExitState(StateBase targetState)
        {
            if (targetState == null) return;

            if (statePipelineMap.TryGetValue(targetState, out var pipeline))
            {
                TruelyDeactivateState(targetState, pipeline);
                return;
            }

            // 查找该状态所在的流水线 - 零GC优化
            foreach (var pipelineRuntime in GetAllPipelines())
            {
                if (pipelineRuntime.runningStates.Contains(targetState))
                {
                    TruelyDeactivateState(targetState, pipelineRuntime.pipelineType);
                    return;
                }
            }
        }

        /// <summary>
        /// 停用流水线中的所有状态
        /// </summary>
        public void DeactivatePipeline(StatePipelineType pipeline)
        {
            var pipelineData = GetPipelineByType(pipeline);
            if (pipelineData == null)
            {
                return;
            }

            _tmpStateBuffer.Clear();
            _tmpStateBuffer.AddRange(pipelineData.runningStates);
            foreach (var state in _tmpStateBuffer)
            {
                TruelyDeactivateState(state, pipeline);
            }
        }

        #endregion

        #region Playable动画管理（核心/谨慎改）

        /// <summary>
        /// 热插拔状态到Playable图（运行时动态添加）- 高性能版本
        /// </summary>
        internal void HotPlugStateToPlayable(StateBase state, StatePipelineRuntime pipeline)
        {
            Debug.Log($"[HotPlug] === 开始热插拔状态到Playable ===");
            Debug.Log($"[HotPlug]   状态: {state?.strKey} | 流水线: {pipeline?.pipelineType}");

            if (state == null || pipeline == null)
            {
                Debug.LogWarning($"[HotPlug] ✗ 状态或流水线为空 - State:{state != null}, Pipeline:{pipeline != null}");
                return;
            }

            // 检查状态是否有动画
            if (state.stateSharedData?.hasAnimation != true)
            {
                Debug.Log($"[HotPlug]   状态无动画，跳过热插拔");
                return;
            }

            // 检查是否已经插入过
            if (pipeline.stateToSlotMap.ContainsKey(state))
            {
                Debug.Log($"[HotPlug]   状态已在槽位映射中，跳过");
                return; // 已存在，跳过
            }

            // 确保PlayableGraph和流水线Mixer有效
            Debug.Log($"[HotPlug]   检查Playable有效性:");
            Debug.Log($"[HotPlug]     PlayableGraph有效: {playableGraph.IsValid()}");
            Debug.Log($"[HotPlug]     Pipeline.mixer有效: {pipeline.mixer.IsValid()}");

            if (!playableGraph.IsValid() || !pipeline.mixer.IsValid())
            {
                Debug.LogError($"[HotPlug] ✗✗✗ 无法插入状态动画：PlayableGraph({playableGraph.IsValid()})或Mixer({pipeline.mixer.IsValid()})无效 ✗✗✗");
                Debug.LogError($"[HotPlug]   这是问题所在！流水线: {pipeline.pipelineType}");
                Debug.LogError($"[HotPlug]   StateMachine初始化状态: {isInitialized}");
                Debug.LogError($"[HotPlug]   StateMachine运行状态: {isRunning}");
                return;
            }

            // 获取状态的动画配置
            var animConfig = state.stateSharedData.animationConfig;
            if (animConfig == null)
            {
                Debug.LogWarning($"状态 {state.strKey} 标记了hasAnimation=true，但没有animationConfig");
                return;
            }

            // 创建Playable节点
            var statePlayable = CreateStatePlayable(state, animConfig);
            if (!statePlayable.IsValid())
            {
                Debug.LogWarning($"无法为状态 {state.strKey} 创建有效的Playable节点");
                return;
            }

            int inputIndex;

            // 优先从空闲槽位池获取
            if (pipeline.freeSlots.Count > 0)
            {
                inputIndex = pipeline.freeSlots.Pop();

                // 断开旧连接（如果有）
                if (pipeline.mixer.GetInput(inputIndex).IsValid())
                {
                    playableGraph.Disconnect(pipeline.mixer, inputIndex);
                }
            }
            else
            {
                // 检查是否达到最大槽位限制
                int currentCount = pipeline.mixer.GetInputCount();
                if (currentCount >= pipeline.maxPlayableSlots)
                {
                    Debug.LogWarning($"流水线 {pipeline.pipelineType} 已达到最大Playable槽位限制 {pipeline.maxPlayableSlots}，无法添加新状态");
                    statePlayable.Destroy();
                    return;
                }

                // 分配新槽位
                inputIndex = currentCount;
                pipeline.mixer.SetInputCount(inputIndex + 1);
            }
            Debug.Log($"[HotPlug]   插入状态Playable到Mixer槽位 {inputIndex}");
            // 连接到流水线Mixer
            playableGraph.Connect(statePlayable, 0, pipeline.mixer, inputIndex);
            pipeline.mixer.SetInputWeight(inputIndex, 1.0f);

            // 记录映射
            pipeline.stateToSlotMap[state] = inputIndex;
            Debug.Log($"[HotPlug]   状态 {state.strKey} 映射到槽位 {inputIndex}");

            // 标记Dirty（热插拔）
            pipeline.MarkDirty(PipelineDirtyFlags.HotPlug);
        }

        /// <summary>
        /// 从Playable图中卸载状态（运行时动态移除）- 高性能版本
        /// </summary>
        internal void HotUnplugStateFromPlayable(StateBase state, StatePipelineRuntime pipeline)
        {
            if (state == null || pipeline == null) return;

            // 只有有动画的状态才需要卸载
            if (state.stateSharedData?.hasAnimation != true)
            {
                return;
            }

            // 查找状态对应的槽位
            if (!pipeline.stateToSlotMap.TryGetValue(state, out int slotIndex))
            {
                return; // 未找到，可能未插入过
            }

            // 确保Mixer有效
            if (!pipeline.mixer.IsValid())
            {
                return;
            }

            // 断开连接
            var inputPlayable = pipeline.mixer.GetInput(slotIndex);
            if (inputPlayable.IsValid())
            {
                playableGraph.Disconnect(pipeline.mixer, slotIndex);
            }

            // 清除权重
            pipeline.mixer.SetInputWeight(slotIndex, 0f);

            // 移除映射
            pipeline.stateToSlotMap.Remove(state);

            // 将槽位回收到池中
            pipeline.freeSlots.Push(slotIndex);

            // 标记Dirty（热拔插）
            pipeline.MarkDirty(PipelineDirtyFlags.HotPlug);

            // 让StateBase销毁自己的Playable资源（包括嵌套的Mixer等）
            state.DestroyPlayable();
        }

        /// <summary>
        /// 为状态创建Playable节点 - 委托给StateBase处理
        /// StateBase会使用其SharedData中的混合计算器生成Playable
        /// </summary>
        protected virtual Playable CreateStatePlayable(StateBase state, StateAnimationConfigData animConfig)
        {
            if (state == null) return Playable.Null;

            // 委托给StateBase创建Playable
            if (state.CreatePlayable(playableGraph, out Playable output))
            {
                var mask = state.stateSharedData?.basicConfig?.avatarMask;
                if (mask != null && output.IsValid())
                {
                    var layerMixer = AnimationLayerMixerPlayable.Create(playableGraph, 1);
                    playableGraph.Connect(output, 0, layerMixer, 0);
                    layerMixer.SetInputWeight(0, 1f);
                    layerMixer.SetLayerMaskFromAvatarMask(0, mask);
                    output = layerMixer;
                }

                Debug.Log($"[StateMachine] ✓ 状态 {state.strKey} Playable创建成功 | Valid:{output.IsValid()}");
                return output;
            }

            Debug.LogWarning($"[StateMachine] ✗ 状态 {state.strKey} Playable创建失败");
            return Playable.Null;
        }

        /// <summary>
        /// 为状态创建AnimationClipPlayable
        /// </summary>
        protected virtual AnimationClipPlayable CreateClipPlayable(AnimationClip clip)
        {
            if (!playableGraph.IsValid() || clip == null)
            {
                return default;
            }

            return AnimationClipPlayable.Create(playableGraph, clip);
        }

        /// <summary>
        /// 为状态创建AnimationMixerPlayable
        /// </summary>
        protected virtual AnimationMixerPlayable CreateMixerPlayable(int inputCount)
        {
            if (!playableGraph.IsValid())
            {
                return default;
            }

            return AnimationMixerPlayable.Create(playableGraph, inputCount);
        }

        #endregion

        #region 工具方法（可修改）

        /// <summary>
        /// 获取所有状态键（用于编辑器下拉框）
        /// </summary>
        protected IEnumerable<string> GetAllStateKeys()
        {
            return stringToStateMap.Keys;
        }

        /// <summary>
        /// 检查是否完全空闲（所有流水线都没有运行状态）
        /// </summary>
        public bool IsIdle()
        {
            return runningStates.Count == 0;
        }

        /// <summary>
        /// 检查特定流水线是否空闲
        /// </summary>
        public bool IsPipelineIdle(StatePipelineType pipelineType)
        {
            var pipeline = GetPipelineByType(pipelineType);
            return pipeline != null && !pipeline.HasActiveStates;
        }

        /// <summary>
        /// 获取所有运行中的状态数量
        /// </summary>
        public int GetRunningStateCount()
        {
            return runningStates.Count;
        }

        /// <summary>
        /// 获取特定流水线中运行的状态数量
        /// </summary>
        public int GetPipelineStateCount(StatePipelineType pipelineType)
        {
            var pipeline = GetPipelineByType(pipelineType);
            return pipeline != null ? pipeline.runningStates.Count : 0;
        }

        /// <summary>
        /// 获取状态当前权重（用于IK/外部系统）
        /// </summary>
        public float GetStateWeight(StateBase state)
        {
            if (state == null) return 0f;
            if (statePipelineMap.TryGetValue(state, out var pipelineType))
            {
                var pipeline = GetPipelineByType(pipelineType);
                return pipeline != null ? pipeline.GetStateWeight(state) : 0f;
            }
            return 0f;
        }

        /// <summary>
        /// 获取状态权重（String键）
        /// </summary>
        public float GetStateWeight(string stateKey)
        {
            var state = GetStateByString(stateKey);
            return GetStateWeight(state);
        }

        /// <summary>
        /// 获取状态权重（Int键）
        /// </summary>
        public float GetStateWeight(int stateId)
        {
            var state = GetStateByInt(stateId);
            return GetStateWeight(state);
        }

        /// <summary>
        /// 获取RootMixer的测试输出信息 - 用于调试动画输出链路
        /// </summary>
        public string GetRootMixerDebugInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("========== RootMixer调试信息 ==========");

            // PlayableGraph状态
            sb.AppendLine($"PlayableGraph有效: {playableGraph.IsValid()}");
            if (playableGraph.IsValid())
            {
                sb.AppendLine($"PlayableGraph运行中: {playableGraph.IsPlaying()}");
                sb.AppendLine($"PlayableGraph名称: {playableGraph.GetEditorName()}");
            }

            // RootMixer状态
            sb.AppendLine($"\nRootMixer有效: {rootMixer.IsValid()}");
            if (rootMixer.IsValid())
            {
                int inputCount = rootMixer.GetInputCount();
                sb.AppendLine($"RootMixer输入数: {inputCount}");

                // 遍历所有输入槽位
                for (int i = 0; i < inputCount; i++)
                {
                    var input = rootMixer.GetInput(i);
                    float weight = rootMixer.GetInputWeight(i);
                    StatePipelineType pipelineType = (StatePipelineType)i;

                    sb.AppendLine($"\n  槽位[{i}] - {pipelineType}:");
                    sb.AppendLine($"    输入有效: {input.IsValid()}");
                    sb.AppendLine($"    权重: {weight:F3}");

                    if (input.IsValid())
                    {
                        // 如果是Mixer，显示其子输入
                        if (input.IsPlayableOfType<AnimationMixerPlayable>())
                        {
                            var mixer = (AnimationMixerPlayable)input;
                            int subInputCount = mixer.GetInputCount();
                            sb.AppendLine($"    子输入数: {subInputCount}");

                            var pipeline = GetPipelineByType(pipelineType);
                            if (pipeline != null)
                            {
                                sb.AppendLine($"    运行状态数: {pipeline.runningStates.Count}");
                            }
                        }
                    }
                }
            }

            // Animator输出
            sb.AppendLine($"\nAnimator绑定: {boundAnimator != null}");
            if (boundAnimator != null)
            {
                sb.AppendLine($"Animator启用: {boundAnimator.enabled}");
                sb.AppendLine($"Animator路径: {boundAnimator.gameObject.name}");
            }

            sb.AppendLine($"\nOutput有效: {animationOutput.IsOutputValid()}");
            if (animationOutput.IsOutputValid())
            {
                var sourcePlayable = animationOutput.GetSourcePlayable();
                sb.AppendLine($"Output源Playable有效: {sourcePlayable.IsValid()}");
                sb.AppendLine($"Output权重: {animationOutput.GetWeight():F3}");
            }

            sb.AppendLine("========================================");
            return sb.ToString();
        }

        #endregion

        #region 调试支持（可修改）

#if UNITY_EDITOR
        /// <summary>
        /// 获取状态机调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"========== 状态机调试信息 ==========");
            sb.AppendLine($"状态机ID: {stateMachineKey}");
            sb.AppendLine($"运行状态: {(isRunning ? "运行中" : "已停止")}");
            sb.AppendLine($"宿主Entity: 无");
            sb.AppendLine($"\n========== 上下文信息 ==========");
            sb.AppendLine($"上下文ID: {stateContext?.contextID}");
            sb.AppendLine($"创建时间: {stateContext?.creationTime}");
            sb.AppendLine($"最后更新: {stateContext?.lastUpdateTime}");
            sb.AppendLine($"\n========== 状态统计 ==========");
            sb.AppendLine($"注册状态数(String): {stringToStateMap.Count}");
            sb.AppendLine($"注册状态数(Int): {intToStateMap.Count}");
            sb.AppendLine($"运行中状态总数: {runningStates.Count}");

            sb.AppendLine($"\n========== 流水线状态 ==========");
            foreach (var pipeline in GetAllPipelines())
            {
                sb.AppendLine($"- {pipeline.pipelineType}: {pipeline.runningStates.Count}个状态 | 权重:{pipeline.weight:F2} | {(pipeline.isEnabled ? "启用" : "禁用")}");
                foreach (var state in pipeline.runningStates)
                {
                    sb.AppendLine($"  └─ {state.strKey}");
                }
            }

            return sb.ToString();
        }

        [Button("输出调试信息", ButtonSizes.Large), PropertyOrder(-1)]
        private void DebugPrint()
        {
            Debug.Log(GetDebugInfo());
        }

        [Button("输出所有状态", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugPrintAllStates()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("========== 所有注册状态 ==========");
            foreach (var kvp in stringToStateMap)
            {
                sb.AppendLine($"[{kvp.Key}] -> {kvp.Value.GetType().Name} (运行:{runningStates.Contains(kvp.Value)})");
            }
            Debug.Log(sb.ToString());
        }

        [Button("测试RootMixer输出", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugPrintRootMixer()
        {
            Debug.Log(GetRootMixerDebugInfo());
        }

        [Button("切换持续统计输出", ButtonSizes.Medium), PropertyOrder(-1)]
        [GUIColor("@enableContinuousStats ? new Color(0.4f, 1f, 0.4f) : new Color(0.7f, 0.7f, 0.7f)")]
        private void ToggleContinuousStats()
        {
            enableContinuousStats = !enableContinuousStats;
            Debug.Log($"[StateMachine] 持续统计输出: {(enableContinuousStats ? "开启" : "关闭")}");
        }

        [Button("打印临时动画列表", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugPrintTemporaryAnimations()
        {
            if (_temporaryStates.Count == 0)
            {
                Debug.Log("[TempAnim] 无临时动画");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"========== 临时动画列表 ({_temporaryStates.Count}个) ==========");
            foreach (var kvp in _temporaryStates)
            {
                var state = kvp.Value;
                bool isRunning = runningStates.Contains(state);
                var clip = state.stateSharedData?.animationConfig?.calculator as StateAnimationMixCalculatorForSimpleClip;
                string clipName = clip?.clip?.name ?? "未知";
                sb.AppendLine($"[{kvp.Key}] Clip:{clipName} | 运行:{isRunning}");
            }
            Debug.Log(sb.ToString());
        }

        [Button("一键清除临时动画", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugClearTemporaryAnimations()
        {
            ClearAllTemporaryAnimations();
        }
#endif

        #endregion

        #region StateContext便捷访问（可修改）

        /// <summary>
        /// 设置Float参数 - 用于动画混合（如2D混合的X/Y输入）
        /// </summary>
        public void SetFloat(StateParameter parameter, float value)
        {
            stateContext?.SetFloat(parameter, value);
        }

        /// <summary>
        /// 设置Float参数 - 字符串重载
        /// </summary>
        public void SetFloat(string paramName, float value)
        {
            stateContext?.SetFloat(paramName, value);
        }

        /// <summary>
        /// 获取Float参数
        /// </summary>
        public float GetFloat(StateParameter parameter, float defaultValue = 0f)
        {
            return stateContext?.GetFloat(parameter, defaultValue) ?? defaultValue;
        }

        /// <summary>
        /// 获取Float参数 - 字符串重载
        /// </summary>
        public float GetFloat(string paramName, float defaultValue = 0f)
        {
            return stateContext?.GetFloat(paramName, defaultValue) ?? defaultValue;
        }

        #endregion
    }
}
