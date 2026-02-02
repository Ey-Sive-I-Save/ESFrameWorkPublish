using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    #region 辅助结构定义

    /// <summary>
    /// 状态激活测试结果 - 描述状态能否激活以及需要执行的操作
    /// </summary>
    [Serializable]
    public struct StateActivationResult
    {
        /// <summary>
        /// 是否可以激活
        /// </summary>
        public bool canActivate;

        /// <summary>
        /// 是否需要打断当前状态
        /// </summary>
        public bool requiresInterruption;

        /// <summary>
        /// 需要打断的状态列表
        /// </summary>
        public List<StateBase> statesToInterrupt;

        /// <summary>
        /// 是否可以直接合并（多个状态并行）
        /// </summary>
        public bool canMerge;

        /// <summary>
        /// 是否可以直接合并（无需打断）
        /// </summary>
        public bool mergeDirectly;

        /// <summary>
        /// 可以合并的状态列表
        /// </summary>
        public List<StateBase> statesToMergeWith;

        /// <summary>
        /// 打断数量（便于快速判断）
        /// </summary>
        public int interruptCount;

        /// <summary>
        /// 合并数量（便于快速判断）
        /// </summary>
        public int mergeCount;

        /// <summary>
        /// 失败原因
        /// </summary>
        public string failureReason;

        /// <summary>
        /// 目标流水线
        /// </summary>
        public StatePipelineType targetPipeline;

        /// <summary>
        /// 注意：statesToInterrupt / statesToMergeWith 为内部复用列表引用，不建议外部长期持有。
        /// </summary>

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static StateActivationResult Success(StatePipelineType pipeline, bool merge = false)
        {
            return new StateActivationResult
            {
                canActivate = true,
                requiresInterruption = false,
                canMerge = merge,
                mergeDirectly = merge,
                statesToInterrupt = new List<StateBase>(),
                statesToMergeWith = new List<StateBase>(),
                interruptCount = 0,
                mergeCount = 0,
                failureReason = string.Empty,
                targetPipeline = pipeline
            };
        }

        /// <summary>
        /// 创建打断结果
        /// </summary>
        public static StateActivationResult Interrupt(StatePipelineType pipeline, List<StateBase> toInterrupt)
        {
            return new StateActivationResult
            {
                canActivate = true,
                requiresInterruption = true,
                canMerge = false,
                mergeDirectly = false,
                statesToInterrupt = toInterrupt,
                statesToMergeWith = new List<StateBase>(),
                interruptCount = toInterrupt?.Count ?? 0,
                mergeCount = 0,
                failureReason = string.Empty,
                targetPipeline = pipeline
            };
        }

        /// <summary>
        /// 创建合并结果
        /// </summary>
        public static StateActivationResult Merge(StatePipelineType pipeline, List<StateBase> mergeWith)
        {
            return new StateActivationResult
            {
                canActivate = true,
                requiresInterruption = false,
                canMerge = true,
                mergeDirectly = true,
                statesToInterrupt = new List<StateBase>(),
                statesToMergeWith = mergeWith,
                interruptCount = 0,
                mergeCount = mergeWith?.Count ?? 0,
                failureReason = string.Empty,
                targetPipeline = pipeline
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static StateActivationResult Failure(string reason)
        {
            return new StateActivationResult
            {
                canActivate = false,
                requiresInterruption = false,
                canMerge = false,
                mergeDirectly = false,
                statesToInterrupt = new List<StateBase>(),
                statesToMergeWith = new List<StateBase>(),
                interruptCount = 0,
                mergeCount = 0,
                failureReason = reason,
                targetPipeline = StatePipelineType.Basic
            };
        }
    }

    /// <summary>
    /// 流水线数据 - 管理单个流水线中的状态
    /// </summary>
    [Serializable]
    public class StatePipelineRuntime
    {
        [LabelText("流水线类型")]
        public StatePipelineType pipelineType;

        [LabelText("主状态"), ShowInInspector, ReadOnly]
        [NonSerialized]
        public StateBase mainState;

        [LabelText("当前运行状态集合"), ShowInInspector, ReadOnly]
        [NonSerialized]
        public HashSet<StateBase> runningStates = new HashSet<StateBase>();

        [LabelText("流水线权重"), Range(0f, 1f)]
        public float weight = 1f;

        [LabelText("是否启用")]
        public bool isEnabled = true;

        [LabelText("优先级"), Tooltip("数值越大优先级越高")]
        public int priority = 0;

        /// <summary>
        /// Playable混合器 - 该流水线的动画混合器
        /// </summary>
        [NonSerialized]
        public AnimationMixerPlayable mixer;

        /// <summary>
        /// 该流水线在RootMixer中的输入索引
        /// </summary>
        [NonSerialized]
        public int rootInputIndex = -1;

        /// <summary>
        /// 流水线是否有活动状态
        /// </summary>
        public bool HasActiveStates => runningStates.Count > 0;

        public StatePipelineRuntime(StatePipelineType type)
        {
            pipelineType = type;
            runningStates = new HashSet<StateBase>();
        }
    }

    /// <summary>
    /// 状态机上下文 - 存储状态机运行时的核心数据
    /// </summary>
    [Serializable]
    public class StateMachineContext
    {
        [LabelText("上下文ID")]
        public string contextID;

        [LabelText("创建时间")]
        public float creationTime;

        [LabelText("最后更新时间")]
        public float lastUpdateTime;

        [LabelText("共享数据"), ShowInInspector]
        [NonSerialized]
        public Dictionary<string, object> sharedData = new Dictionary<string, object>();

        [LabelText("临时标记"), ShowInInspector]
        [NonSerialized]
        public HashSet<string> runtimeFlags = new HashSet<string>();

        /// <summary>
        /// 设置共享数据
        /// </summary>
        public void SetData<T>(string key, T value)
        {
            sharedData[key] = value;
        }

        /// <summary>
        /// 获取共享数据
        /// </summary>
        public T GetData<T>(string key, T defaultValue = default)
        {
            if (sharedData.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 添加标记
        /// </summary>
        public void AddFlag(string flag)
        {
            runtimeFlags.Add(flag);
        }

        /// <summary>
        /// 移除标记
        /// </summary>
        public void RemoveFlag(string flag)
        {
            runtimeFlags.Remove(flag);
        }

        /// <summary>
        /// 检查标记
        /// </summary>
        public bool HasFlag(string flag)
        {
            return runtimeFlags.Contains(flag);
        }

        /// <summary>
        /// 清空上下文
        /// </summary>
        public void Clear()
        {
            sharedData.Clear();
            runtimeFlags.Clear();
        }
    }

    /// <summary>
    /// 状态退出测试结果
    /// </summary>
    [Serializable]
    public struct StateExitResult
    {
        public bool canExit;
        public string failureReason;
        public StatePipelineType pipeline;

        public static StateExitResult Success(StatePipelineType pipelineType)
        {
            return new StateExitResult { canExit = true, failureReason = string.Empty, pipeline = pipelineType };
        }

        public static StateExitResult Failure(string reason, StatePipelineType pipelineType)
        {
            return new StateExitResult { canExit = false, failureReason = reason, pipeline = pipelineType };
        }
    }

    #endregion

    /// <summary>
    /// 状态机基类 - 专为Entity提供的高性能并行状态管理系统
    /// 设计思路参考UE的状态机，支持流水线、并行状态、动画混合等高级特性
    /// 功能完备，无需子类重写核心逻辑
    /// </summary>
    [Serializable]
    public class StateMachine
    {
        #region 核心引用与宿主

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
        /// 状态机核心上下文 - 存储运行时数据
        /// </summary>
        [LabelText("核心上下文"), ShowInInspector]
        [NonSerialized]
        public StateMachineContext context;

        #endregion

        #region 扩展回调与策略

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
        /// 自定义通道占用计算
        /// </summary>
        public Func<IEnumerable<StateBase>, StateChannelMask> CustomChannelMaskEvaluator;

        /// <summary>
        /// 自定义代价计算
        /// </summary>
        public Func<IEnumerable<StateBase>, StateCostSummary> CustomCostEvaluator;

        #endregion

        #region 生命周期状态

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

        #region 流水线与并行状态管理

        /// <summary>
        /// 流水线字典 - 按流水线类型组织状态
        /// </summary>
        [SerializeReference, LabelText("流水线系统")]
        protected Dictionary<StatePipelineType, StatePipelineRuntime> pipelines = new Dictionary<StatePipelineType, StatePipelineRuntime>();

        /// <summary>
        /// 所有运行中的状态集合 - 支持多状态并行
        /// </summary>
        [ShowInInspector, ReadOnly, LabelText("当前运行状态")]
        [NonSerialized]
        public HashSet<StateBase> runningStates = new HashSet<StateBase>();

        /// <summary>
        /// String键到状态的映射
        /// </summary>
        [SerializeReference, LabelText("String映射")]
        protected Dictionary<string, StateBase> stringToStateMap = new Dictionary<string, StateBase>();

        /// <summary>
        /// Int键到状态的映射
        /// </summary>
        [SerializeReference, LabelText("Int映射")]
        protected Dictionary<int, StateBase> intToStateMap = new Dictionary<int, StateBase>();

        /// <summary>
        /// 状态归属流水线映射
        /// </summary>
        [NonSerialized]
        protected Dictionary<StateBase, StatePipelineType> statePipelineMap = new Dictionary<StateBase, StatePipelineType>();

        [NonSerialized]
        private readonly List<StateBase> _tmpStateBuffer = new List<StateBase>(16);

        [NonSerialized]
        private readonly List<StateBase> _tmpInterruptStates = new List<StateBase>(8);

        [NonSerialized]
        private readonly List<StateBase> _tmpMergeStates = new List<StateBase>(8);

        [NonSerialized]
        private readonly Dictionary<StateBase, StateActivationCache> _activationCache = new Dictionary<StateBase, StateActivationCache>(64);

        [NonSerialized]
        private int _dirtyVersion = 0;

        [NonSerialized]
        private StateDirtyReason _lastDirtyReason = StateDirtyReason.Unknown;

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

        #region Playable动画系统

        /// <summary>
        /// PlayableGraph引用 - 用于动画播放
        /// </summary>
        [NonSerialized]
        protected PlayableGraph playableGraph;

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
        protected AnimationMixerPlayable rootMixer;

        /// <summary>
        /// 是否拥有PlayableGraph所有权
        /// </summary>
        [NonSerialized]
        protected bool ownsPlayableGraph = false;

        public bool IsPlayableGraphValid => playableGraph.IsValid();

        public bool IsPlayableGraphPlaying => playableGraph.IsValid() && playableGraph.IsPlaying();

        public Animator BoundAnimator => boundAnimator;

        #endregion

        #region 性能优化相关

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

        #region 初始化与销毁

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

            // 初始化上下文
            context = new StateMachineContext
            {
                contextID = Guid.NewGuid().ToString(),
                creationTime = Time.time,
                lastUpdateTime = Time.time
            };

            // 初始化流水线
            InitializePipelines(graph, root);

            // 初始化所有状态
            foreach (var kvp in stringToStateMap)
            {
                InitializeState(kvp.Value);
            }

            isInitialized = true;
        }

        /// <summary>
        /// 初始化状态机并绑定Animator
        /// </summary>
        public void Initialize(Entity entity, Animator animator, PlayableGraph graph = default, AnimationMixerPlayable root = default)
        {
            Initialize(entity, graph, root);
            BindToAnimator(animator);
        }

        /// <summary>
        /// 初始化流水线系统
        /// </summary>
        private void InitializePipelines(PlayableGraph graph, AnimationMixerPlayable root)
        {
            // Playable初始化
            if (graph.IsValid())
            {
                playableGraph = graph;
                ownsPlayableGraph = false;
            }
            else
            {
                playableGraph = PlayableGraph.Create($"StateMachine_{stateMachineKey}");
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
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

            // 为每个流水线类型创建实例
            for (int i = 0; i < pipelineCount; i++)
            {
                var pipelineType = (StatePipelineType)i;
                var pipeline = new StatePipelineRuntime(pipelineType);
                pipelines[pipelineType] = pipeline;

                // 如果有PlayableGraph，为流水线创建Mixer并接入Root
                if (playableGraph.IsValid())
                {
                    pipeline.mixer = AnimationMixerPlayable.Create(playableGraph, 0);
                    pipeline.rootInputIndex = i;
                    playableGraph.Connect(pipeline.mixer, 0, rootMixer, i);
                    rootMixer.SetInputWeight(i, pipeline.weight);
                }

                OnPipelineInitialized?.Invoke(pipeline);
            }
        }

        /// <summary>
        /// 初始化单个状态
        /// </summary>
        public void InitializeState(StateBase state)
        {
            if (state == null) return;
            state.host = this;
            state.Initialize(this);
        }

        /// <summary>
        /// 绑定PlayableGraph到Animator
        /// </summary>
        public bool BindToAnimator(Animator animator)
        {
            if (animator == null)
            {
                Debug.LogError("绑定Animator失败：Animator为空");
                return false;
            }

            if (!playableGraph.IsValid())
            {
                playableGraph = PlayableGraph.Create($"StateMachine_{stateMachineKey}");
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                ownsPlayableGraph = true;
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

        private StateActivationCache GetOrCreateActivationCache(StateBase targetState)
        {
            if (targetState == null) return null;

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
                var mergeData = state?.stateSharedData?.mergeData;
                if (mergeData != null)
                {
                    mask |= mergeData.stateChannelMask;
                }
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
                var costData = state?.stateSharedData?.costData;
                if (costData == null || !costData.enableCostCalculation)
                {
                    continue;
                }

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
            foreach (var pipeline in pipelines.Values)
            {
                pipeline.runningStates.Clear();
                if (pipeline.mixer.IsValid())
                {
                    pipeline.mixer.Destroy();
                }
            }
            pipelines.Clear();

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
            context?.Clear();

            isRunning = false;
            isInitialized = false;
        }

        #endregion

        #region 状态机生命周期

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
            foreach (var pipelineType in pipelines.Keys)
            {
                DeactivatePipeline(pipelineType);
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
        /// </summary>
        public void UpdateStateMachine()
        {
            if (!isRunning) return;

            // 更新上下文时间
            context.lastUpdateTime = Time.time;

            // 更新所有运行中的状态
            foreach (var state in runningStates)
            {
                if (state != null && state.baseStatus == StateBaseStatus.Running)
                {
                    state.OnStateUpdate();
                }
            }
        }

        #endregion

        #region 状态注册与管理

        /// <summary>
        /// 注册新状态（String键）
        /// </summary>
        public bool RegisterState(string stateKey, StateBase state, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (string.IsNullOrEmpty(stateKey))
            {
                Debug.LogError("状态键不能为空");
                return false;
            }

            if (state == null)
            {
                Debug.LogError($"状态实例不能为空: {stateKey}");
                return false;
            }

            if (stringToStateMap.ContainsKey(stateKey))
            {
                Debug.LogWarning($"状态 {stateKey} 已存在，跳过注册");
                return false;
            }

            // 注册状态
            stringToStateMap[stateKey] = state;
            state.strKey = stateKey;
            state.host = this;
            statePipelineMap[state] = pipeline;

            MarkDirty(StateDirtyReason.RuntimeChanged);

            if (isInitialized)
            {
                InitializeState(state);
            }

            return true;
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

            // 注册状态
            intToStateMap[stateKey] = state;
            state.host = this;
            statePipelineMap[state] = pipeline;

            MarkDirty(StateDirtyReason.RuntimeChanged);

            if (isInitialized)
            {
                InitializeState(state);
            }

            return true;
        }

        /// <summary>
        /// 同时注册String和Int键
        /// </summary>
        public bool RegisterState(string stringKey, int intKey, StateBase state, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            bool success = RegisterState(stringKey, state, pipeline);
            if (success)
            {
                intToStateMap[intKey] = state;
                statePipelineMap[state] = pipeline;
                MarkDirty(StateDirtyReason.RuntimeChanged);
            }
            return success;
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

            // 如果状态正在运行，先停用
            if (runningStates.Contains(state))
            {
                TryDeactivateState(stateKey);
            }

            stringToStateMap.Remove(stateKey);
            transitionCache.Remove(stateKey);
            statePipelineMap.Remove(state);
            _activationCache.Remove(state);
            MarkDirty(StateDirtyReason.Release);
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

            if (runningStates.Contains(state))
            {
                TryDeactivateState(stateKey);
            }

            intToStateMap.Remove(stateKey);
            statePipelineMap.Remove(state);
            _activationCache.Remove(state);
            MarkDirty(StateDirtyReason.Release);
            return true;
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
        /// 获取流水线
        /// </summary>
        public StatePipelineRuntime GetPipeline(StatePipelineType pipelineType)
        {
            return pipelines.TryGetValue(pipelineType, out var pipeline) ? pipeline : null;
        }

        /// <summary>
        /// 设置流水线权重
        /// </summary>
        public void SetPipelineWeight(StatePipelineType pipelineType, float weight)
        {
            if (pipelines.TryGetValue(pipelineType, out var pipeline))
            {
                pipeline.weight = Mathf.Clamp01(weight);
                // 更新Playable权重
                if (rootMixer.IsValid())
                {
                    int index = (int)pipelineType;
                    if (index < rootMixer.GetInputCount())
                    {
                        rootMixer.SetInputWeight(index, pipeline.weight);
                    }
                }
            }
        }

        #endregion

        #region 状态激活测试与执行

        /// <summary>
        /// 测试状态能否激活（不执行）
        /// </summary>
        public StateActivationResult TestStateActivation(StateBase targetState, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (targetState == null)
            {
                return StateActivationResult.Failure("目标状态为空");
            }

            if (!isRunning)
            {
                return StateActivationResult.Failure("状态机未运行");
            }

            if (CustomActivationTest != null)
            {
                return CustomActivationTest(targetState, pipeline);
            }

            int pipelineCount = (int)StatePipelineType.Count;
            int pipelineIndex = (int)pipeline;
            if (pipelineIndex < 0 || pipelineIndex >= pipelineCount)
            {
                return StateActivationResult.Failure($"流水线索引非法: {pipeline}");
            }

            var cache = GetOrCreateActivationCache(targetState);
            if (cache != null && cache.versions[pipelineIndex] == _dirtyVersion)
            {
                return cache.results[pipelineIndex];
            }

            // 检查该状态是否已在运行
            if (runningStates.Contains(targetState))
            {
                var failure = StateActivationResult.Failure("状态已在运行中");
                if (cache != null)
                {
                    cache.results[pipelineIndex] = failure;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return failure;
            }

            // 获取目标流水线
            if (!pipelines.TryGetValue(pipeline, out var targetPipeline))
            {
                var failure = StateActivationResult.Failure($"流水线 {pipeline} 不存在");
                if (cache != null)
                {
                    cache.results[pipelineIndex] = failure;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return failure;
            }

            if (!targetPipeline.isEnabled)
            {
                var failure = StateActivationResult.Failure($"流水线 {pipeline} 未启用");
                if (cache != null)
                {
                    cache.results[pipelineIndex] = failure;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return failure;
            }

            // 获取该流水线中当前运行的状态
            var pipelineStates = targetPipeline.runningStates;

            // 如果流水线为空，直接成功
            if (pipelineStates.Count == 0)
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

            foreach (var existingState in pipelineStates)
            {
                // 这里可以添加自定义的冲突/合并逻辑
                // 示例：检查StateSharedData的mergeData
                if (existingState.stateSharedData != null && targetState.stateSharedData != null)
                {
                    // 简化的合并判断逻辑
                    bool canMerge = CheckStateMergeCompatibility(existingState, targetState);
                    if (canMerge)
                    {
                        mergeList.Add(existingState);
                    }
                    else
                    {
                        interruptList.Add(existingState);
                    }
                }
                else
                {
                    // 默认：打断
                    interruptList.Add(existingState);
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
        /// 检查两个状态是否可以合并
        /// </summary>
        private bool CheckStateMergeCompatibility(StateBase existing, StateBase incoming)
        {
            if (CanMergeEvaluator != null)
            {
                return CanMergeEvaluator(existing, incoming);
            }

            // 默认简单实现：不同状态可以合并
            return existing != incoming;
        }

        /// <summary>
        /// 执行状态激活（根据测试结果）
        /// </summary>
        public bool ExecuteStateActivation(StateBase targetState, StateActivationResult result)
        {
            if (!result.canActivate)
            {
                Debug.LogWarning($"状态激活失败: {result.failureReason}");
                return false;
            }

            if (!pipelines.TryGetValue(result.targetPipeline, out var pipeline))
            {
                return false;
            }

            // 执行打断
            if (result.requiresInterruption && result.statesToInterrupt != null)
            {
                foreach (var stateToInterrupt in result.statesToInterrupt)
                {
                    DeactivateState(stateToInterrupt, result.targetPipeline);
                }
            }

            // 激活目标状态
            targetState.OnStateEnter();
            runningStates.Add(targetState);
            pipeline.runningStates.Add(targetState);

            if (pipeline.mainState == null || result.requiresInterruption)
            {
                pipeline.mainState = targetState;
            }

            OnStateEntered?.Invoke(targetState, result.targetPipeline);
            MarkDirty(StateDirtyReason.Enter);

            return true;
        }

        /// <summary>
        /// 尝试激活状态（通过键）
        /// </summary>
        public bool TryActivateState(string stateKey, StatePipelineType pipeline = StatePipelineType.Basic)
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
        public bool TryActivateState(int stateKey, StatePipelineType pipeline = StatePipelineType.Basic)
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
        public bool TryActivateState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (targetState == null) return false;

            var result = TestStateActivation(targetState, pipeline);
            return ExecuteStateActivation(targetState, result);
        }

        /// <summary>
        /// 尝试激活状态（通过实例，使用注册时的默认流水线）
        /// </summary>
        public bool TryActivateState(StateBase targetState)
        {
            if (targetState == null) return false;

            if (statePipelineMap.TryGetValue(targetState, out var pipeline))
            {
                return TryActivateState(targetState, pipeline);
            }

            return TryActivateState(targetState, StatePipelineType.Basic);
        }

        /// <summary>
        /// 停用状态（内部方法）
        /// </summary>
        private void DeactivateState(StateBase state, StatePipelineType pipeline)
        {
            if (state == null) return;

            state.OnStateExit();
            runningStates.Remove(state);

            if (pipelines.TryGetValue(pipeline, out var pipelineData))
            {
                pipelineData.runningStates.Remove(state);
                if (pipelineData.mainState == state)
                {
                    pipelineData.mainState = pipelineData.runningStates.FirstOrDefault();
                }
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

            // 查找该状态所在的流水线
            foreach (var kvp in pipelines)
            {
                if (kvp.Value.runningStates.Contains(state))
                {
                    DeactivateState(state, kvp.Key);
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

            foreach (var kvp in pipelines)
            {
                if (kvp.Value.runningStates.Contains(state))
                {
                    DeactivateState(state, kvp.Key);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 进入验证测试（不执行）
        /// </summary>
        public StateActivationResult TestEnterState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            return TestStateActivation(targetState, pipeline);
        }

        /// <summary>
        /// 测试进入（验证后执行进入）
        /// </summary>
        public bool TryEnterState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            return TryActivateState(targetState, pipeline);
        }

        /// <summary>
        /// 强制进入（不做验证）
        /// </summary>
        public bool ForceEnterState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (targetState == null) return false;

            if (!pipelines.TryGetValue(pipeline, out var pipelineData))
            {
                return false;
            }

            _tmpStateBuffer.Clear();
            _tmpStateBuffer.AddRange(pipelineData.runningStates);
            foreach (var state in _tmpStateBuffer)
            {
                DeactivateState(state, pipeline);
            }

            targetState.OnStateEnter();
            runningStates.Add(targetState);
            pipelineData.runningStates.Add(targetState);
            pipelineData.mainState = targetState;

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

            DeactivateState(targetState, result.pipeline);
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
                DeactivateState(targetState, pipeline);
                return;
            }

            foreach (var kvp in pipelines)
            {
                if (kvp.Value.runningStates.Contains(targetState))
                {
                    DeactivateState(targetState, kvp.Key);
                    return;
                }
            }
        }

        /// <summary>
        /// 停用流水线中的所有状态
        /// </summary>
        public void DeactivatePipeline(StatePipelineType pipeline)
        {
            if (!pipelines.TryGetValue(pipeline, out var pipelineData))
            {
                return;
            }

            _tmpStateBuffer.Clear();
            _tmpStateBuffer.AddRange(pipelineData.runningStates);
            foreach (var state in _tmpStateBuffer)
            {
                DeactivateState(state, pipeline);
            }
        }

        #endregion

        #region Playable动画管理

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

        #region 工具方法

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
            return pipelines.TryGetValue(pipelineType, out var pipeline) && !pipeline.HasActiveStates;
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
            return pipelines.TryGetValue(pipelineType, out var pipeline) ? pipeline.runningStates.Count : 0;
        }

        #endregion

        #region 调试支持

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
            sb.AppendLine($"上下文ID: {context?.contextID}");
            sb.AppendLine($"创建时间: {context?.creationTime}");
            sb.AppendLine($"最后更新: {context?.lastUpdateTime}");
            sb.AppendLine($"\n========== 状态统计 ==========");
            sb.AppendLine($"注册状态数(String): {stringToStateMap.Count}");
            sb.AppendLine($"注册状态数(Int): {intToStateMap.Count}");
            sb.AppendLine($"运行中状态总数: {runningStates.Count}");
            
            sb.AppendLine($"\n========== 流水线状态 ==========");
            foreach (var kvp in pipelines)
            {
                var pipeline = kvp.Value;
                sb.AppendLine($"- {kvp.Key}: {pipeline.runningStates.Count}个状态 | 权重:{pipeline.weight:F2} | {(pipeline.isEnabled ? "启用" : "禁用")}");
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
#endif

        #endregion
    }
}
//     #region 最原始定义
//     //原始定义
//     public abstract class BaseOriginalStateMachine : IESHosting<BaseOriginalStateMachine>, IStateMachine, IESModule,IESOriginalModule<BaseOriginalStateMachine>
//     {
//         #region 杂货
//         public static IState NullState = new ESNanoStateMachine_StringKey() { key_ = "空状态" };
//         public static IState NullState_Micro = new BaseESMicroStateOverrideRunTimeLogic_StringKey() { key = "空状态" };
//         [DisplayAsString(FontSize = 25), HideLabel, PropertyOrder(-1)]
//         public string Des_ => "我是一个状态机噢耶";//随便描述一下而已
//         public bool CheckThisStateCanUpdating
//         {
//             get
//             {
//                 if (AsThis != null && AsThis != this)
//                 {
//                     if (!AsThis.CheckThisStateCanUpdating) return false;
//                 }
//                 if (GetHost != null)
//                 {
//                     if (GetHost is IStateMachine machine)
//                     {
//                         if (!machine.CheckThisStateCanUpdating)
//                         {
//                             return false;
//                         }
//                         else if (machine is ESMicroStateMachine_StringKey mS && (mS.SelfRunningStates?.Contains(this) ?? false)) return true;
//                         return false;
//                     }
//                 }
//                 return true;
//             }
//         }

//         //默认进入状态
//         [NonSerialized] public IState StartWithState;
//         [LabelText("设置默认状态的键(这里设置没用)"),ShowInInspector] public object defaultStateKey = null;
//         //Host获取哈
//         protected bool OnSubmitHostingAsNormal(BaseOriginalStateMachine hosting)
//         {
//             host = hosting;
//             return true;
//         }
//         //获取初始状态
//         protected IState GetStartWith()
//         {
//             if (StartWithState != null && StartWithState != NullState)
//             {
//                 return StartWithState;
//             }
//             else
//             {
//                 return GetDefaultState();
//             }
//         }
//         public IState GetDefaultState()
//         {
//             if (defaultStateKey == null) return null;
//             return GetStateByKey(defaultStateKey);
//         }
//         public bool IsIdle()
//         {
//             return !IsStateNotNull(_SelfRunningState);
//         }
//         public IState AsThis
//         {
//             get => thisState;
//             set { /*Debug.LogWarning("修改状态机自状态是危险的，但是允许");*/ thisState = value; }
//         }

//         [LabelText("状态机自基准状态"), SerializeReference, FoldoutGroup("子状态")] public IState thisState = NullState_Micro;//基准状态不准是纳米的，起码是微型的罢

//         #endregion

//         #region 父子关系与保持的运行
//         //基准状态
//         //获得根状态机--非常好的东西
//         public BaseOriginalStateMachine Root { get { if (GetHost is BaseOriginalStateMachine machine) return machine.Root; else return this; } }

//         public IState SelfRunningMainState { get => _SelfRunningState; set => _SelfRunningState = value; }
//         [SerializeReference, LabelText("当前运行状态"), FoldoutGroup("子状态")] public IState _SelfRunningState = null;
//         //总状态机(冲突合并需要的)--只有标准状态机需要
//         /*[SerializeReference, LabelText("总状态机运行中状态")]
//         public HashSet<IESNanoState> MainRunningStates;*/

//         //自己掌控运行--至少是微型状态机才有
//         /**/
//         #endregion

//         #region 生命周期_状态专属的
//         [ShowInInspector, ReadOnly, LabelText("状态进入 是/否"), FoldoutGroup("是否状况收集")]
//         public bool IsRunning { get; set; }
//         public void OnStateEnter()
//         {
//             if (IsRunning) return;
//             IsRunning = true;//状态更新
//             /*MainRunningStates = Root.MainRunningStates;//使用统一的根处引用*/
//             AsThis?.OnStateEnter();//自基准状态
//             _Expand_PreparedHappenDesign();//扩展Prepare时间
//             /*_SelfRunningStates?.Update();//自己层级下的运行中状态--子状态机也是状态哈--这个更新就是删除多余的的加入新的
//              */
//             if (host is ESMicroStateMachine_StringKey parent)//您是微型状态机
//             {
//                 parent._SelfRunningState = this;
//                 parent._SelfRunningStates.TryAdd(this);//如果自己是子，那么也要加入到父级的运行状态中
//             }
//             OnStateEnter();//状态机没啥好额外准备的罢
//         }
//         //进入时--都不是必要功能
//         protected virtual void OnStateEnter()
//         {
//             IState state = GetStartWith();
//             if (state != null)
//             {
//                 bool b = TryActiveState(state);
//                 if (b == false) OnStateExit();
//                 StartWithState = null;
//             }
//         }

//         //退出时,状态退出
//         public virtual void OnStateExit()
//         {
//             if (!IsRunning) return;
//             IsRunning = false;//状态更新

//             AsThis?.OnStateExit();//自基准状态


//             _Expand_ExitHappenDesign();//扩展Exit时机

//             if (host is ESMicroStateMachine_StringKey parent)
//             {
//                 if (parent._SelfRunningState == this)
//                 {
//                     parent._SelfRunningState = null;
//                 }
//                 parent._SelfRunningStates.TryRemove(this);//如果自己是子，那么也要从父级的运行状态中移除
//             }
//             _SelfRunningState = NullState;
//         }
//         //更新时--状态更新
//         public void OnStateUpdate()
//         {
//             AsThis?.OnStateUpdate();
//             _Expand_UpdateHappenDesign();//扩展更新Update的时机
//             /* _SelfRunningStates?.Update();
//              if (_SelfRunningStates != null)
//              {
//                  foreach (var i in _SelfRunningStates.valuesNow_)
//                  {
//                      if (i != null)
//                      {
//                          if(i is IESMicroState ESMicro)
//                          {
//                              if (ESMicro.RunningStatus != EnumStateRunningStatus.StateUpdate) continue;
//                          }
//                          else if(!i.HasPrepared)
//                          {
//                              continue;
//                          }
//                          i.OnStateUpdate();
//                      }
//                  }
//              }*/
//         }
//         #endregion

//         #region 生命周期_作为模块的
//         protected override void OnEnable()
//         {
//             base.OnEnable();
//             if (GetHost is BaseOriginalStateMachine machine) { }//如果有父状态机--》受父级更新控制
//             else OnStateEnter();//如果没有父级或者父级不是状态机--》自己就完成控制了
//         }

//         protected override void OnDisable()
//         {
//             base.OnDisable();
//             if (GetHost is BaseOriginalStateMachine machine) { }//如果有父状态机--》受父级更新控制
//             else OnStateExit();//如果没有父级或者父级不是状态机--》自己就完成控制了
//         }
//         protected override void Update()
//         {
//             base.Update();
//             if (GetHost is BaseOriginalStateMachine machine) { Debug.Log(2); }//如果有父状态机--》受父级更新控制
//             else OnStateUpdate();//如果没有父级或者父级不是状态机--》自己就完成控制了
//         }
//         #endregion

//         #region 对基准状态的包装
//         public object key_;
//         public void SetKey(object key)
//         {
//             AsThis?.SetKey(key);
//             key_ = key;
//         }
//         public object GetKey()
//         {
//             if (!IsStateNotNull(AsThis)) return key_;
//             return AsThis.GetKey();
//         }
//         public bool IsStateNotNull(IState state)
//         {
//             if (state == null || state == NullState || state == NullState_Micro) return false;
//             return true;
//         }

//         public EnumStateRunningStatus RunningStatus => AsThis?.RunningStatus??(IsRunning?EnumStateRunningStatus.StateUpdate:EnumStateRunningStatus.StateExit);
//         IStateSharedData IState.SharedData
//         {
//             get => AsThis?.SharedData; set { if (AsThis != null) AsThis.SharedData = value; }
//         }
//         IStateVariableData IState.VariableData
//         {
//             get => AsThis?.VariableData; set { if (AsThis != null) AsThis.VariableData = value; }
//         }
//         #endregion

//         #region 状态切换支持
//         public abstract bool TryActiveState(IState use);//通用
//         public abstract bool TryInActiveState(IState use);//通用
//         public abstract bool TryActiveStateByKey(object key_);//通用
//         public abstract string[] KeysWithLayer(string atFirst="");
//         public abstract void RegisterNewState_Original(object key, IState aState);
//         public abstract bool TryInActiveState(object key_);
//         public abstract bool TryInActiveStateByKey(string key_);
//         public abstract IState GetStateByKey(object o);
//         public abstract IState GetStateByKey(string o);
//         #endregion

//         #region 设计层扩展重写状态机逻辑
//         protected virtual void _Expand_PreparedHappenDesign()
//         {
//             host._SelfRunningState = this;
//         }
//         protected virtual void _Expand_UpdateHappenDesign()
//         {
//             if (_SelfRunningState != null)
//             {
//                 _SelfRunningState.OnStateUpdate();
//             }
//             else
//             {
//                 if (IsIdle())
//                 {
//                     var default_ = GetDefaultState();
//                     if (default_ != null)
//                     {
//                        bool b= TryActiveState(default_);
//                         if (b == false) OnStateExit();
//                     }
//                 }
//             }
//         }
//         protected virtual void _Expand_ExitHappenDesign()
//         {

//             if (_SelfRunningState != null)
//             {
//                 _RightlyExitTheState(_SelfRunningState);
//             }
//         }
//         #endregion

//         #region 测试方法
//         [Button("初始化"), FoldoutGroup("状态机测试按钮"), Tooltip("定义初始化的状态")]
//         public void WithEnterState(IState state)
//         {
//             if (state != null)
//                 StartWithState = state;
//         }


//         #endregion

//         #region 注册状态


//         #endregion

//         #region 状态切换辅助等
//         protected void _RightlyPreparedTheState(IState use)
//         {
//             if (use != null)
//             {
//                 use.OnStateEnter();
//                 _SelfRunningState = use;
//             }
//         }
//         protected void _RightlyExitTheState(IState use)
//         {
//             if (use != null)
//             {
//                 if (_SelfRunningState == use) { _SelfRunningState = null; };
//                 use.OnStateExit();
//             }
//         }

//         bool IESOriginalModule<BaseOriginalStateMachine>.OnSubmitHosting(BaseOriginalStateMachine host)
//         {
//             this.host = host;
//             return true;
//         }
//         #endregion
//     }
//     #endregion

//     #region 纳米级别状态机--空的-泛型键-状态机-只能是纳米了--给键哈
//     public abstract class BaseESNanoStateMachine<Key_> : BaseOriginalStateMachine
//     {
//         #region 杂货与基准定义
//         #endregion

//         #region 字典表
//         public override string[] KeysWithLayer(string atFirst)
//         {
//             List<string> all = new List<string>();

//             //加键
//             foreach (var i in allStates.Keys)
//             {
//                 all.Add(atFirst + i);
//             };
//             //遍历
//             foreach (var i in allStates.Values)
//             {
//                 if (i is BaseOriginalStateMachine machine)
//                 {
//                     all.AddRange(machine.KeysWithLayer(atFirst + i.GetKey()));
//                 }
//             }
//             return all.ToArray();
//         }
//         [SerializeReference, LabelText("全部状态字典")]
//         public Dictionary<Key_, IState> allStates = new Dictionary<Key_, IState>();
//         public Dictionary<Key_, IState> AllStates => allStates;
//         public override IState GetStateByKey(object o)
//         {
//             if(o is Key_ thekey&&allStates.ContainsKey(thekey))
//             {
//                 return allStates[thekey];
//             }
//             return null;
//         }
//         public override IState GetStateByKey(string s)
//         {
//             if (s is Key_ thekey && allStates.ContainsKey(thekey))
//             {
//                 return allStates[thekey];
//             }
//             var use = ESDesignUtility.Matcher.SystemObjectToT<Key_>(s);
//             if (use != null && allStates.ContainsKey(use))
//             {
//                 return allStates[use];
//             }
//             return null;
//         }
//         #endregion

//         #region 定义

//         #endregion
//         /* [GUIColor("@OLDESDesignUtilityOLD.ColorSelector.Color_03"),LabelText("当前状态")]*/
//         /* public IESMicroState CurrentState =>currentFirstState?? NullState;*/


//         [Button("测试切换状态"), Tooltip("该方法建议用于运行时，不然得话会立刻调用状态的Enter，请切换使用WithEnterState")]
//         public void TryActiveStateByKey(Key_ key, IState ifNULL = null)
//         {
//             if (allStates == null)
//             {
//                 allStates = new Dictionary<Key_, IState>();
//                 if (ifNULL != null)
//                 {
//                     RegisterNewState_Original(key, ifNULL);
//                     OnlyESNanoPrivate_SwitchStateToFromByKey(key);
//                     //新建 没啥
//                 }
//                 return;
//             }
//             if (allStates.ContainsKey(key))
//             {
//                 TryActiveState(allStates[key]);
//             }
//             else
//             {
//                 if (ifNULL != null)
//                 {
//                     RegisterNewState_Original(key, ifNULL);
//                     TryActiveState(ifNULL);
//                 }
//             }
//         }
//         public override bool TryActiveStateByKey(object key_)
//         {
//             if(key_ is Key_ key_1)
//             {
//                 return TryActiveStateByKey(key_1);
//             }
//             return false;
//         }
//         protected void OnlyESNanoPrivate_SwitchStateToFromByKey(Key_ to, Key_ from = default)
//         {
//             if (AllStates.ContainsKey(to))
//             {
//                 OnlyESNanoPrivate_SwitchStateToFrom(AllStates[to]);
//             }
//         }
//         protected void OnlyESNanoPrivate_SwitchStateToFrom(IState to, IState from = default)
//         {
//             if (AllStates.Values.Contains(to))
//             {
//                 IState willUse = to;
//                 if (SelfRunningMainState == willUse) return;//同一个？无意义

//                 _RightlyExitTheState(SelfRunningMainState);//过去的真退了

//                 _RightlyPreparedTheState(to);//我真来了

//                 if (SelfRunningMainState == null)
//                 {
//                     Debug.LogError("状态为空！键是" + to.GetKey());
//                 }
//             }
//         }
//         public override bool TryActiveState(IState use)
//         {
//             if (!this.IsRunning&&host is BaseOriginalStateMachine originalStateMachine)
//             {
//                 this.WithEnterState(use);
//                 originalStateMachine.TryActiveState(this);
//             }
//             /* if (RootMainRunningStates == null)
//              {
//                  RootMainRunningStates = new Dictionary<Key_, IESMicroState>();
//                  if (ESMicro != null)
//                  {
//                      RegisterNewState_Original(ESMicro, ESMicro);
//                      OnlyESNanoPrivate_SwitchStateToFrom(key);
//                  }
//                  return;
//              }*/
//             if (allStates.Values.Contains(use))
//             {
//                 OnlyESNanoPrivate_SwitchStateToFrom(use);
//                 return true;
//             }
//             else
//             {
//                 Debug.LogError("暂时不支持活动为注册的状态");
//                 return false;
//             }

//         }
//         public bool TryActiveStateByKey(Key_ key_)
//         {
//             if (allStates.ContainsKey(key_))
//             {
//                 return TryActiveState(allStates[key_]);
//             }
//             return false;
//         }
//         public override void RegisterNewState_Original(object key, IState logic)
//         {
//             RegesterNewState(ESDesignUtility.Matcher.SystemObjectToT<Key_>(key), logic);
//         }

//         [Button("初始化"), Tooltip("定义初始化的状态")]
//         public void WithEnterStateByKey(Key_ key)
//         {
//             if (key != null && allStates.ContainsKey(key))
//                 StartWithState = allStates[key];
//             else Debug.LogError("状态机没注册这个状态");
//         }
//         protected override void OnStateEnter()
//         {
//             IState state = GetStartWith();
//             if (state != null)
//             {
//                 bool b = TryActiveState(state);
//                 if (b == false) OnStateExit();
//                 StartWithState = null;
//             }
//             base.OnStateEnter();
//         }
//         public void RegesterNewState(Key_ key, IState logic)
//         {
//             if (allStates.ContainsKey(key))
//             {
//                 Debug.LogError("重复注册状态?键是" + key);
//             }
//             else
//             {
//                 allStates.Add(key, logic);
//                 logic.SetKey(key);

//                 if (logic is IESOriginalModule<BaseOriginalStateMachine> logic1)
//                 {
//                     logic1.OnSubmitHosting(this);
//                     Debug.Log("注册成功？" + logic.GetKey());
//                 }
//                 else if(logic is BaseOriginalStateMachine machine)
//                 {
//                     machine.TrySubmitHosting(this,false);
//                 }
//                 /*else if(logic is BaseOriginalStateMachine standMachine)
//                 {
//                     standMachine.TrySubmitHosting(this);
//                 }*/
//                 else
//                 {
//                     Debug.Log("啥也不是？");
//                 }

//             }
//         }
//         public override bool TryInActiveState(object key_)
//         {
//             return TryInActiveStateByKey(key_.ToString());
//         }

//         public override bool TryInActiveStateByKey(string s)
//         {
//             Debug.Log("尝试关闭状态"+s);
//             if (s is Key_ thekey && allStates.ContainsKey(thekey))
//             {
//                 TryInActiveState(allStates[thekey]);
//             }
//             var use = ESDesignUtility.Matcher.SystemObjectToT<Key_>(s);
//             if (use != null && allStates.ContainsKey(use))
//             {
//                 TryInActiveState(allStates[use]);
//             }
//             return false;
//         }

//         public override bool TryInActiveState(IState use)
//         {
//             if (allStates.ContainsValue(use))
//             {
//                 _RightlyExitTheState(use);
//             }
//             return false;
//         }



//         //默认的单播放

//         /*  private void OnlyESNanoPrivate_SwitchStateToFrom(IESNanoState to, IESNanoState from = default)
//           {
//               if (allStates.Values.Contains(to))
//               {
//                   IESNanoState willUse = to;
//                   if (_SelfRunningState == willUse) return;//同一个？无意义

//                   _RightlyExitTheState(_SelfRunningState);//过去的真退了

//                   _RightlyPreparedTheState(to);//我真来了

//                   if (_SelfRunningState == null)
//                   {
//                       Debug.LogError("状态为空！键是" + to.GetKey());
//                   }
//               }
//           }*/
//         protected override void OnSubmitHosting(BaseOriginalStateMachine host)
//         {
//             this.host = host;
//             base.OnSubmitHosting(host);
//         }
//     }
//     #endregion

//  /*   #region 微型级别状态机 : 支持局部并行和级别处理了
//     [Serializable, TypeRegistryItem("微型状态机(字符串)")]
//     public class BaseESMicroStateMachine_StringKey2 : BaseESNanoStateMachine<string>
//     {
//         public override void TryRemoveModuleAsNormal(IESNanoState key_)
//         {

//         }
//     }
//     #endregion*/

//     #region 标准状态机支持并行和数据配置

//     [Serializable, TypeRegistryItem("字符串键标准并行状态机")]
//     public class BaseESStandardStateMachine2 : ESMicroStateMachine_StringKey, IESModule
//     {
//         #region 
//         [NonSerialized] public HashSet<IState> RootMainRunningStates;//根部的运行
//         #endregion
//         public override bool TryActiveState(IState use)
//         {
//             if (use is IState stand)
//             {
//                 //空状态：直接使用
//                 if (RootMainRunningStates.Count == 0) return base.TryActiveState(stand);
//                 //已经包含-就取消
//                 if (RootMainRunningStates.Contains(stand))
//                 {
//                     Debug.LogWarning("尝试使用已经存在的状态，有何用意");
//                     return false;
//                 }

//                 Debug.Log("-----------《《《《合并测试开始------来自" + stand.GetKey().ToString());
//                 //单状态，简易判断
//                 if (RootMainRunningStates.Count == 1)
//                 {
//                     IState state = RootMainRunningStates.First();
//                     {
//                         //state的共享数据有的不是标准的哈/
//                         //标准情形
//                         Debug.Log(stand.SharedData);
//                         if (state.SharedData is StateSharedData left && stand.SharedData is StateSharedData right)
//                         {
//                             string leftKey = state.GetKey().ToString();
//                             string rightKey = stand.GetKey().ToString();
//                             var back = StateSharedData.HandleMerge(left.MergePart_, right.MergePart_, leftKey, rightKey);
//                             if (back == HandleMergeBack.HitAndReplace)
//                             {
//                                 state.OnStateExit();
//                                 stand.OnStateEnter();
//                                 _SelfRunningState = stand;
//                                 Debug.Log("单-合并--打断  原有的  " + leftKey + " 被 新的  " + rightKey + "  打断!");
//                             }
//                             else if (back == HandleMergeBack.MergeComplete)
//                             {
//                                 stand.OnStateEnter();
//                                 Debug.Log("单-合并--成功  原有的  " + leftKey + " 和  新的  " + rightKey + "  合并!");
//                             }
//                             else //合并失败
//                             {
//                                 Debug.Log("单-合并--失败  原有的  " + leftKey + " 阻止了  新的  " + rightKey + "  !");
//                             }
//                         }
//                         else //有的不是标准状态
//                         {
//                             base.TryActiveState(stand);
//                         }
//                     }
//                 }
//                 else  //多项目
//                 {
//                     if (stand.SharedData is StateSharedData right)
//                     {
//                         string rightKey = stand.GetKey().ToString();
//                         List<IState> hit = new List<IState>();
//                         List<string> merge = new List<string>();
//                         foreach (var i in RootMainRunningStates)
//                         {
//                             if (i.SharedData is StateSharedData left)
//                             {
//                                 string leftKey = i.GetKey().ToString();
//                                 var back = StateSharedData.HandleMerge(left.MergePart_, right.MergePart_, leftKey, rightKey);
//                                 if (back == HandleMergeBack.HitAndReplace)
//                                 {
//                                     hit.Add(i);

//                                     //打断一个捏
//                                 }
//                                 else if (back == HandleMergeBack.MergeComplete)
//                                 {
//                                     //正常的
//                                     merge.Add(leftKey);

//                                 }
//                                 else //合并失败
//                                 {
//                                     Debug.LogWarning("多-合并--失败" + leftKey + " 阻止了 " + rightKey + "的本次合并测，无事发生试!");
//                                     return false;
//                                 }
//                             }
//                         }
//                         //成功合并了
//                         Debug.Log("---√多-合并--完全成功！来自" + rightKey + "以下是细则：");
//                         stand.OnStateEnter();
//                         foreach (var i in merge)
//                         {
//                             Debug.Log("     --合并细则  本次合并-合并了了" + i);
//                         }
//                         foreach (var i in hit)
//                         {
//                             Debug.Log("     --合并细则  本次合并-打断了" + i.GetKey());
//                             i.OnStateExit();
//                         }
//                     }
//                     else //不是标准状态滚
//                     {
//                         base.TryActiveState(stand);
//                     }
//                 }
//                 return false;
//             }
//             return false;
//         }
//     }
//     #endregion


//     #region 可用状态机--String锁定了
//     [Serializable,TypeRegistryItem("纳米状态机(String)")]
//     public class ESNanoStateMachine_StringKey : BaseOriginalStateMachine
//     {
//         #region 字典表

//         [SerializeReference, LabelText("全部状态字典"), FoldoutGroup("子状态")]
//         public Dictionary<string, IState> allStates = new Dictionary<string, IState>();
//         public override string[] KeysWithLayer(string atFirst)
//         {
//             List<string> all = new List<string>();

//             //加键
//             foreach (var i in allStates.Keys) {
//                 all.Add(atFirst+i);
//             };
//             //遍历
//             foreach (var i in allStates.Values)
//             {
//                 if (i is BaseOriginalStateMachine machine)
//                 {
//                     all.AddRange(machine.KeysWithLayer(atFirst + machine.GetKey()+'/'));
//                 }
//             }
//             return all.ToArray();
//         }
//         public override IState GetStateByKey(object o)
//         {
//             string s = o.ToString();
//             if (allStates.ContainsKey(s))
//             {
//                 return allStates[s];
//             }
//             return null;
//         }
//         public override IState GetStateByKey(string s)
//         {
//             if (s != null && allStates.ContainsKey(s))
//             {
//                 return allStates[s];
//             }
//             return null;
//         }
//         #endregion

//         #region 切换状态
//         protected void OnlyESNanoPrivate_SwitchStateToFromByKey(string to, string from = default)
//         {
//             if (allStates.ContainsKey(to))
//             {
//                 OnlyESNanoPrivate_SwitchStateToFrom(allStates[to]);
//             }
//         }
//         protected void OnlyESNanoPrivate_SwitchStateToFrom(IState to, IState from = default)
//         {
//             if (allStates.Values.Contains(to))
//             {
//                 IState willUse = to;
//                 if (SelfRunningMainState == willUse) return;//同一个？无意义

//                 _RightlyExitTheState(SelfRunningMainState);//过去的真退了

//                 _RightlyPreparedTheState(to);//我真来了

//                 if (SelfRunningMainState == null)
//                 {
//                     Debug.LogError("状态为空！键是" + to.GetKey());
//                 }
//             }
//         }

//         public bool TryActiveStateByKeyWithLayer(string layerKey)
//         {
//             if (layerKey.Contains('/')||layerKey.Contains('?')||layerKey.Contains(".."))
//             {
//                 string[] layers = layerKey.Split('/');
//                 BaseOriginalStateMachine TheMachine = this;
//                 IState TheState=null;
//                 Queue<IState> TheStates=null;
//                 bool endBack = true;

//                 for(int index = 0; index < layers.Length; index++)
//                 {
//                     string i = layers[index];
//                     Debug.Log("by" + i+index);
//                     if (i == "..")//回退一级
//                     {
//                         Debug.Log("回退");
//                         var parent = TheMachine.GetHost as BaseOriginalStateMachine;
//                         if (parent != null)
//                         {
//                             TheMachine = parent;
//                             continue;
//                         }
//                     }
//                     var aState = i;
//                     TheStates = null;
//                     if (i.Contains('?'))
//                     {
//                         TheStates = new Queue<IState>();
//                         string[] Addstates = i.Split('?');
//                         foreach (var a in Addstates)
//                         {
//                             var state = TheMachine.GetStateByKey(a);
//                             if (state != null)
//                                 TheStates.Enqueue(state);
//                         }
//                         aState = Addstates[0];
//                     }

//                     TheState = TheMachine.GetStateByKey(aState);

//                     if (TheState != null)
//                     {
//                         if (index == layers.Length - 1) { Debug.Log("last" + index+ TheState.GetKey()); break; }
//                         if (TheState is BaseOriginalStateMachine nextMachine)
//                         {
//                             TheMachine = nextMachine;
//                             Debug.Log("next"+index);
//                             continue;
//                         }
//                         else
//                         {
//                             Debug.Log("state" + index);
//                             break;
//                         }
//                     }
//                     else
//                     {
//                         Debug.Log("null" + index);
//                         endBack = false;
//                         break;
//                     }
//                 }
//                /* foreach(var i in layers)
//                 {

//                 }*/
//                 if (TheStates != null&&TheStates.Count>0)
//                 {
//                     while (TheStates.Count > 0)
//                     {
//                         var use = TheStates.Dequeue();
//                         if (TheMachine.TryActiveState(use)) return true;
//                     }
//                 }
//                 else if (TheState != null)
//                 {
//                    return TheMachine.TryActiveState(TheState);
//                 }
//                 return endBack;
//             }
//             else
//             {
//                return TryActiveStateByKey(layerKey);
//             }
//         }
//         public bool TryInActiveStateByKeyWithLayer(string layerKey)
//         {
//             if (layerKey.Contains('/') || layerKey.Contains('?') || layerKey.Contains(".."))
//             {
//                 string[] layers = layerKey.Split('/');
//                 BaseOriginalStateMachine TheMachine = this;
//                 IState TheState = null;
//                 Queue<IState> TheStates = null;
//                 bool endBack = true;

//                 for (int index = 0; index < layers.Length; index++)
//                 {
//                     string i = layers[index];
//                     Debug.Log("by" + i + index);
//                     if (i == "..")//回退一级
//                     {
//                         Debug.Log("回退");
//                         var parent = TheMachine.GetHost as BaseOriginalStateMachine;
//                         if (parent != null)
//                         {
//                             TheMachine = parent;
//                             continue;
//                         }
//                     }
//                     var aState = i;
//                     TheStates = null;
//                     if (i.Contains('?'))
//                     {
//                         TheStates = new Queue<IState>();
//                         string[] Addstates = i.Split('?');
//                         foreach (var a in Addstates)
//                         {
//                             var state = TheMachine.GetStateByKey(a);
//                             if (state != null)
//                                 TheStates.Enqueue(state);
//                         }
//                         aState = Addstates[0];
//                     }

//                     TheState = TheMachine.GetStateByKey(aState);

//                     if (TheState != null)
//                     {
//                         if (index == layers.Length - 1) break;
//                         if (TheState is BaseOriginalStateMachine nextMachine)
//                         {
//                             TheMachine = nextMachine;
//                             Debug.Log("next" + index);
//                             continue;
//                         }
//                         else
//                         {
//                             Debug.Log("state" + index);
//                             break;
//                         }
//                     }
//                     else
//                     {
//                         Debug.Log("null" + index);
//                         endBack = false;
//                         break;
//                     }
//                 }
//                 /* foreach(var i in layers)
//                  {

//                  }*/
//                 if (TheStates != null && TheStates.Count > 0)
//                 {
//                     while (TheStates.Count > 0)
//                     {
//                         var use = TheStates.Dequeue();
//                         if (TheMachine.TryInActiveState(use)) return true;
//                     }
//                 }
//                 else if (TheState != null)
//                 {
//                     return TheMachine.TryInActiveState(TheState);
//                 }
//                 return endBack;
//             }
//             else
//             {
//                 return TryInActiveStateByKey(layerKey);
//             }
//         }
//         public override bool TryInActiveState(IState use)
//         {
//             if (allStates.ContainsValue(use))
//             {
//                 _RightlyExitTheState(use);
//             }
//             return false;
//         }
//         public bool TryActiveStateByKey(string key_)
//         {
//             if (allStates.ContainsKey(key_))
//             {

//                 return TryActiveState(allStates[key_]);
//             }
//             return false;
//         }

//         public override bool TryActiveStateByKey(object key_)
//         {
//             return TryActiveStateByKey(key_.ToString());
//         }

//         public override bool TryActiveState(IState use)
//         {
//             if (use.IsRunning) return true;
//             if (!this.IsRunning && host is BaseOriginalStateMachine originalStateMachine)
//             { 
//                 this.WithEnterState(use);
//                 return originalStateMachine.TryActiveState(this);
//             }
//             if (allStates.Values.Contains(use))
//             {
//                 OnlyESNanoPrivate_SwitchStateToFrom(use);
//                 return true;
//             }
//             else
//             {
//                 if (use.GetKey().ToString() == "玩家格挡")
//                 {
//                     Debug.Log("RESIST-SI");
//                 }
//                 Debug.LogError("暂时不支持活动为注册的状态");
//                 return false;
//             }
//         }
//         #endregion

//         #region 注册注销
//         public override void RegisterNewState_Original(object key, IState aState)
//         {
//             RegisterNewState(key.ToString(), aState);
//         }
//         public void RegisterNewState(string key, IState logic)
//         {
//             if (allStates.ContainsKey(key))
//             {
//                // Debug.LogError("重复注册状态?键是" + key);
//             }
//             else
//             {
//                 allStates.Add(key, logic);
//                 logic.SetKey(key);
//                 if (StartWithState == null || StartWithState == NullState)
//                 {
//                     //新的
//                     StartWithState = logic;
//                 }
//                 if (logic is IESOriginalModule<BaseOriginalStateMachine> logic1)
//                 {
//                     logic1.OnSubmitHosting(this);
//                     //Debug.Log("注册状态成功？" + logic.GetKey());
//                 }
//                 else if (logic is BaseOriginalStateMachine machine)
//                 {
//                     machine.TrySubmitHosting(this, false);
//                     //Debug.Log("注册状态机成功？" + logic.GetKey());
//                 }
//                 else
//                 {
//                     Debug.Log("啥也不是？");
//                 }

//             }
//         }


//         #endregion

//         #region 设计层
//         protected override void _Expand_PreparedHappenDesign()
//         {
//             base._Expand_PreparedHappenDesign();
//         }

//         public override bool TryInActiveState(object key_)
//         {
//            return TryInActiveStateByKey(key_.ToString());
//         }

//         public override bool TryInActiveStateByKey(string key_)
//         {
//             Debug.Log("尝试关闭" + key_);
//             var it = GetStateByKey(key_);
//             if (it == null) return false;
//             TryInActiveState(it);
//             return false;
//         }

//         #endregion
//     }
//     //微型状态机定义
//     [Serializable, TypeRegistryItem("微型状态机(String)")]
//     public class ESMicroStateMachine_StringKey : ESNanoStateMachine_StringKey, IStateMachine
//     {
//         #region 当前并行
//         [LabelText("自己的运行中状态"),FoldoutGroup("子状态")]
//         public SafeUpdateList_EasyQueue_SeriNot_Dirty<IState> _SelfRunningStates = new SafeUpdateList_EasyQueue_SeriNot_Dirty<IState>();
//         public IEnumerable<IState> SelfRunningStates => _SelfRunningStates.valuesNow_;
//         #endregion

//         #region 设计层
//         protected override void _Expand_PreparedHappenDesign()
//         {
//             _SelfRunningStates?.Update();
//             // base._Expand_PreparedHappenDesign();
//         }
//         protected override void _Expand_UpdateHappenDesign()
//         {
//             if (_SelfRunningStates != null)
//             {
//                 _SelfRunningStates.Update();
//                 int count = _SelfRunningStates.valuesNow_.Count;
//                 if (count == 0)
//                 {
//                     if (IsIdle())
//                     {
//                         var default_ = GetDefaultState();
//                         if (default_ != null)
//                         {
//                             bool b = TryActiveState(default_);
//                             if (b == false)
//                             {
//                                 if (GetHost is not BaseOriginalStateMachine machine) return;//最高级您
//                                 OnStateExit();
//                             }
//                         }
//                         else
//                         {
//                             if (GetHost is not BaseOriginalStateMachine machine) return;//最高级您
//                             OnStateExit();
//                         }
//                     }
//                 }
//                 else
//                 {

//                     for (int iq = 0; iq < count; iq++)
//                     {
//                         var i = _SelfRunningStates.valuesNow_[iq];
//                         /*                    if (i is IESMicroState ESMicro)
//                                             {
//                                                 if (ESMicro.RunningStatus != EnumStateRunningStatus.StateUpdate) continue;
//                                             }
//                                             else if (!i.HasPrepared)
//                                             {
//                                                 continue;
//                                             }*/
//                         if (i.RunningStatus == EnumStateRunningStatus.StateUpdate)
//                             i.OnStateUpdate();
//                     }
//                 }

//             }





//             //base._Expand_UpdateHappenDesign();不需要您
//         }
//         protected override void _Expand_ExitHappenDesign()
//         {
//             _SelfRunningStates?.Update();
//             if (_SelfRunningStates != null)
//             {
//                 foreach (var i in _SelfRunningStates.valuesNow_)
//                 {
//                     if (i != null)
//                     {
//                         _RightlyExitTheState(i);
//                     }
//                 }
//             }
//             //base._Expand_ExitHappenDesign();
//         }
//         #endregion

//         #region Active重写
//         public override bool TryActiveState(IState use)
//         {
//             if (use is IState ESMicro)
//             {
//                 //空状态：直接使用
//                 if (SelfRunningStates.Count() == 0) return base.TryActiveState(ESMicro);
//                 //已经包含-就取消
//                 if (SelfRunningStates.Contains(ESMicro))
//                 {
//                     Debug.LogWarning("尝试使用已经存在的状态，有何用意");
//                     return false;
//                 }

//                 Debug.Log("-----------《《《《合并测试开始------来自" + ESMicro.GetKey().ToString());
//                 //单状态，简易判断
//                 if (SelfRunningStates.Count() == 1)
//                 {
//                     IState state = SelfRunningStates.First();
//                     {
//                         //state的共享数据有的不是标准的哈/
//                         //标准情形
//                         if (state.SharedData is IStateSharedData left && ESMicro.SharedData is IStateSharedData right)
//                         {
//                             string leftKey = state.GetKey().ToString();
//                             string rightKey = ESMicro.GetKey().ToString();
//                             var back = StateSharedData.HandleMerge(left, right, leftKey, rightKey);
//                             if (back == HandleMergeBack.HitAndReplace)
//                             {
//                                 state.OnStateExit();
//                                 ESMicro.OnStateEnter();
//                                 _SelfRunningState = ESMicro;
//                                 Debug.Log("单-合并--打断  原有的  " + leftKey + " 被 新的  " + rightKey + "  打断!");
//                             }
//                             else if (back ==HandleMergeBack.MergeComplete)
//                             {
//                                 ESMicro.OnStateEnter();
//                                 Debug.Log("单-合并--成功  原有的  " + leftKey + " 和  新的  " + rightKey + "  合并!");
//                             }
//                             else //合并失败
//                             {
//                                 Debug.Log("单-合并--失败  原有的  " + leftKey + " 阻止了  新的  " + rightKey + "  !");
//                             }
//                         }
//                         else //有的不是标准状态
//                         {
//                             base.TryActiveState(ESMicro);
//                             Debug.Log("不具有");
//                         }
//                     }
//                 }
//                 else  //多项目
//                 {
//                     if (ESMicro.SharedData is IStateSharedData right)
//                     {
//                         string rightKey = ESMicro.GetKey().ToString();
//                         List<IState> hit = new List<IState>();
//                         List<string> merge = new List<string>();
//                         foreach (var i in SelfRunningStates)
//                         {
//                             if (i.SharedData is IStateSharedData left)
//                             {
//                                 string leftKey = i.GetKey().ToString();
//                                 var back = StateSharedData.HandleMerge(left, right, leftKey, rightKey);
//                                 if (back == HandleMergeBack.HitAndReplace)
//                                 {
//                                     hit.Add(i);

//                                     //打断一个捏
//                                 }
//                                 else if (back == HandleMergeBack.MergeComplete)
//                                 {
//                                     //正常的
//                                     merge.Add(leftKey);

//                                 }
//                                 else //合并失败
//                                 {
//                                     Debug.LogWarning("多-合并--失败" + leftKey + " 阻止了 " + rightKey + "的本次合并测，无事发生试!");
//                                     return false;
//                                 }
//                             }
//                         }
//                         //成功合并了
//                         Debug.Log("---√多-合并--完全成功！来自" + rightKey + "以下是细则：");
//                         ESMicro.OnStateEnter();
//                         foreach (var i in merge)
//                         {
//                             Debug.Log("     --合并细则  本次合并-合并了了" + i);
//                         }
//                         foreach (var i in hit)
//                         {
//                             Debug.Log("     --合并细则  本次合并-打断了" + i.GetKey());
//                             i.OnStateExit();
//                         }
//                     }
//                     else //不是标准状态滚
//                     {
//                         base.TryActiveState(ESMicro);
//                     }
//                 }
//                 return false;
//             }
//             return base.TryActiveState(use);
//         }

//         #endregion
//     }

//     //标准状态机
//     [Serializable, TypeRegistryItem("标准状态机(String)")]
//     public class ESStandardStateMachine_StringKey : ESMicroStateMachine_StringKey, IStateMachine
//     {
//         #region 总状态机
//         [OdinSerialize, LabelText("总状态机运行中状态"),ShowInInspector, FoldoutGroup("子状态")]
//         public HashSet<IState> MainRunningStates=new HashSet<IState>();
//         public HashSet<IState> RootAllRunningStates => MainRunningStates;
//         #endregion

//         #region 设计层
//         protected override void _Expand_PreparedHappenDesign()
//         {
//             if (MainRunningStates ==null&&Root == this)
//             {
//                 MainRunningStates = new HashSet<IState>();
//             }
//             if (Root is ESStandardStateMachine_StringKey standMachine)
//             {
//                 MainRunningStates = standMachine.MainRunningStates;
//             }
//             base._Expand_PreparedHappenDesign();
//         }
//         protected override void OnEnable()
//         {
//             base.OnEnable();
//             if (Root is ESStandardStateMachine_StringKey standMachine)
//             {
//                 MainRunningStates = standMachine.MainRunningStates;
//             }
//         }
//       /*  protected override void _Expand_UpdateHappenDesign()
//         {
//             base._Expand_UpdateHappenDesign();//照常更新
//         }*/
//         protected override void _Expand_ExitHappenDesign()
//         {
//             _SelfRunningStates?.Update();
//             base._Expand_ExitHappenDesign();//照常更新
//         }
//         #endregion

//         #region Active重写
//         public override bool TryActiveState(IState use)
//         {

//             if (use is IState stand)
//             {
//                 //空状态：直接使用
//                 if (RootAllRunningStates.Count == 0) { return base.TryActiveState(stand); }
//                 //已经包含-就取消
//                 if (RootAllRunningStates.Contains(stand)||SelfRunningStates.Contains(stand))
//                 {
//                     return stand.IsRunning;
//                 }

//                 //Debug.Log("-----------《《《《合并测试开始------来自" + stand.GetKey().ToString());
//                 //单状态，简易判断
//                 if (RootAllRunningStates.Count == 1)
//                 {
//                     IState state = RootAllRunningStates.First();
//                     {
//                         //state的共享数据有的不是标准的哈/
//                         //标准情形
//                       //  Debug.Log("单-合并--测试");
//                         if (state.SharedData is StateSharedData left && stand.SharedData is StateSharedData right)
//                         {
//                             string leftKey = state.GetKey().ToString();
//                             string rightKey = stand.GetKey().ToString();
//                             var back = StateSharedData.HandleMerge(left.MergePart_, right.MergePart_, leftKey, rightKey);
//                             if (back == HandleMergeBack.HitAndReplace)
//                             {
//                                 state.OnStateExit();
//                                 stand.OnStateEnter();
//                                 _SelfRunningState = stand;
//                                 Debug.Log("单-合并--打断  原有的  " + leftKey + " 被 新的  " + rightKey + "  打断!");
//                                 return true;
//                             }
//                             else if (back == HandleMergeBack.MergeComplete)
//                             {
//                                 stand.OnStateEnter();
//                                 Debug.Log("单-合并--成功  原有的  " + leftKey + " 和  新的  " + rightKey + "  合并!");
//                                 return true;
//                             }
//                             else //合并失败
//                             {
//                                 Debug.Log("单-合并--失败  原有的  " + leftKey + " 阻止了  新的  " + rightKey + "  !");
//                                 return false;
//                             }
//                         }
//                         else //有的不是标准状态
//                         {
//                             base.TryActiveState(stand);
//                         }
//                     }
//                 }
//                 else  //多项目
//                 {
//                     if (stand.SharedData is StateSharedData right)
//                     {
//                         string rightKey = stand.GetKey().ToString();
//                         List<IState> hit = new List<IState>();
//                         List<string> merge = new List<string>();
//                         foreach (var i in RootAllRunningStates)
//                         {
//                             if (i.SharedData is StateSharedData left)
//                             {
//                                 string leftKey = i.GetKey().ToString();
//                                 var back = StateSharedData.HandleMerge(left.MergePart_, right.MergePart_, leftKey, rightKey);
//                                 if (back == HandleMergeBack.HitAndReplace)
//                                 {
//                                     hit.Add(i);

//                                     //打断一个捏
//                                 }
//                                 else if (back == HandleMergeBack.MergeComplete)
//                                 {
//                                     //正常的
//                                     merge.Add(leftKey);

//                                 }
//                                 else //合并失败
//                                 {
//                                    // Debug.LogWarning("多-合并--失败" + leftKey + " 阻止了 " + rightKey + "的本次合并测，无事发生试!");
//                                     return false;
//                                 }
//                             }
//                         }
//                         //成功合并了
//                       //  Debug.Log("---√多-合并--完全成功！来自" + rightKey + "以下是细则：");
//                         stand.OnStateEnter();
//                         foreach (var i in merge)
//                         {
//                           //  Debug.Log("     --合并细则  本次合并-合并了了" + i);
//                         }
//                         foreach (var i in hit)
//                         {
//                         //    Debug.Log("     --合并细则  本次合并-打断了" + i.GetKey());
//                             i.OnStateExit();
//                         }
//                         return true;
//                     }
//                     else //不是标准状态滚
//                     {
//                         base.TryActiveState(stand);
//                     }
//                 }
//                 return false;
//             }
//             return base.TryActiveState(use);
//         }

//         #endregion

//         #region 测试方法

//         [Button("输出全部状态"), FoldoutGroup("状态机测试按钮")]
//         public void Test_OutPutAllStateRunning(string befo = "状态机：")
//         {
//             string all = befo + "现在运行的有：";
//             foreach (var i in MainRunningStates)
//             {
//                 all += i.GetKey() + " , ";
//             }
//             /*Debug.LogWarning(all);*/
//         }
//         #endregion
//     }

//     #endregion


// }
