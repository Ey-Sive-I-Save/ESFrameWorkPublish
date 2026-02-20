using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Debug = UnityEngine.Debug;

// ============================================================================
// 文件：StateMachine.cs
// 作用：状态机主实现（注册/运行/更新/切换/层级管理/PlayableGraph 驱动/临时动画等）。
//
// Public（本文件对外可直接使用的成员；按模块分组，“先功能、后成员”，便于扫读）：
//
// 【基础信息】
// - 宿主实体：public Entity HostEntity { get; }
// - 唯一标识：public string stateMachineKey
// - 配置访问：public StateMachineConfig Config
// - 运行状态：public bool isRunning { get; protected set; }
// - 查询某状态是否运行：public bool IsStateRunning(StateBase state)
//
// 【IK 汇总输出】
// - 最终 IK 姿态：public StateIKPose finalIKPose
// - IK 后处理扩展点：public delegate FinalIKPosePostProcessDelegate(...) / public event OnFinalIKPosePostProcess
//
// 【运行时约束】
// - 支持标记：public StateSupportFlags currentSupportFlags
// - 设置支持标记：public void SetSupportFlags(StateSupportFlags flags)
//
// 【注册与查询】
// - 注册数量：public int RegisteredStateCount { get; }
// - 枚举注册状态：public IEnumerable<KeyValuePair<string, StateBase>> EnumerateRegisteredStatesByKey()
// - 查询状态所属层级：public bool TryGetStateLayerType(StateBase state, out StateLayerType layerType)
// - 按键获取/判断：public GetStateByString/GetStateByInt/HasState(...)
//
// 【默认/层级资源】
// - 默认状态键：public string defaultStateKey
// - 上/下半身遮罩：public AvatarMask upperBodyMask / public AvatarMask lowerBodyMask
// - 参考姿态动画：public AnimationClip referencePoseClip
//
// 【PlayableGraph】
// - 图对象：public PlayableGraph playableGraph
// - 图状态：public bool IsPlayableGraphValid / public bool IsPlayableGraphPlaying
// - 绑定 Animator：public Animator BoundAnimator { get; }
//
// 【初始化/生命周期】
// - 初始化：public void Initialize(...)
// - 初始化并绑定 Animator：public void Initialize(..., Animator animator, ...)
// - 释放：public void Dispose()
// - 启动/停止：public void StartStateMachine() / public void StopStateMachine()
// - 每帧更新：public void UpdateStateMachine()
//
// 【注册接口】
// - 从 Info 注册：public StateBase RegisterStateFromInfo(...)
// - 注册/反注册：public bool RegisterState(...)
// - 从 SharedData 注册：public bool RegisterStateFromSharedData(...)
// - 反注册：public bool UnregisterState(...)
//
// 【回退与层级控制】
// - 设置回退状态：public void SetFallbackState(...)
// - 获取层级运行时：public StateLayerRuntime GetLayer(StateLayerType layerType)
// - 设置层级权重：public void SetLayerWeight(StateLayerType layerType, float weight)
// - 层级空闲/统计：public IsIdle/IsLayerIdle/GetRunningStateCount/GetLayerStateCount
//
// 【临时动画（运行时插拔）】
// - 添加/移除/清空：public AddTemporaryAnimation/RemoveTemporaryAnimation/ClearAllTemporaryAnimations
// - 查询：public HasTemporaryAnimation/GetTemporaryAnimationCount
//
// 【事件广播】
// - 动画事件广播：public void BroadcastAnimationEvent(StateBase state, string eventName, string eventParam)
//
// 【状态切换】
// - 评估：public TestStateActivation/TestEnterState/TestExitState
// - 执行：public ExecuteStateActivation/TryActivateState/TryDeactivateState/TryEnterState/TryExitState
// - 强制：public ForceEnterState/ForceExitState
// - 关闭层级：public void DeactivateLayer(StateLayerType layer)
//
// 【权重/调试/阶段】
// - 权重查询：public float GetStateWeight(...)
// - RootMixer 调试信息：public string GetRootMixerDebugInfo()
// - 外部控制状态阶段：public void SetStateRuntimePhase(...) / public void ClearStateRuntimePhaseOverride(...)
//
// 【上下文参数】
// - 速度/攀爬输入：public void SetMotionSpeedXZ/SetAvgSpeedXZ/SetClimbInput
// - 参数读写：public void SetFloat(...) / public float GetFloat(...)
//
// Private/Internal：各类缓存、热路径更新、字典/层级运行时、混合/打断/合并规则、Dirty 降级流程等。
// ============================================================================

namespace ES
{
    /// <summary>
    /// 状态机基类 - 专为Entity提供的高性能并行状态管理系统。
    /// 设计思路参考UE状态机，支持层级、并行状态、动画混合等高级特性。
    /// 核心逻辑默认稳定可用，扩展点通过回调/配置开放，避免子类侵入式重写。
    /// </summary>
    [Serializable, TypeRegistryItem("ES状态机")]
    public partial class StateMachine
    {
        
        #region 基础字段（序列化/上下文）

        /// <summary>
        /// 宿主Entity - 状态机所属的实体对象
        /// </summary>
        [NonSerialized]
        private Entity hostEntity;

        public Entity HostEntity => hostEntity;

        /// <summary>
        /// 状态机唯一标识键
        /// </summary>
        [TitleGroup("状态机设置", Order = 0)]
        [BoxGroup("状态机设置/基础", ShowLabel = false)]
        [LabelText("状态机键"), ShowInInspector]
        public string stateMachineKey;



        [TitleGroup("状态机设置", Order = 0)]
        [BoxGroup("状态机设置/基础", ShowLabel = false)]
        [LabelText("状态机配置"), ShowInInspector]
        [SerializeField]
        private StateMachineConfig config;

        public StateMachineConfig Config
        {
            get => config;
            set => config = value;
        }

        /// <summary>
        /// 状态上下文 - 统一管理运行时数据、参数、标记等（整合了原StateMachineContext）
        /// </summary>
        [LabelText("状态实时上下文"), ShowInInspector]
        [ReadOnly]
        [NonSerialized]
        private StateMachineContext stateContext;


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


        #region 运行时状态（核心/不建议改）


        [ShowInInspector, ReadOnly, LabelText("当前运行状态")]
        [NonSerialized]
        private SwapBackSet<StateBase> runningStates = new SwapBackSet<StateBase>(32);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InternalAddRunningState(StateBase state)
        {
            if (state == null) return;
            runningStates.Add(state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InternalRemoveRunningState(StateBase state)
        {
            if (state == null) return;
            runningStates.Remove(state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsStateRunning(StateBase state)
        {
            return state != null && runningStates.Contains(state);
        }

        #region FinalIK 输出（BipedIK）

        /// <summary>
        /// 由 StateMachine 每帧聚合得到的最终 IK Pose（供 LateUpdate 驱动 FinalIK 使用）。
        /// </summary>
        [NonSerialized]
        public StateIKPose finalIKPose;

        /// <summary>
        /// FinalIK Pose 后处理回调：允许外部模块在不侵入状态系统的前提下，
        /// 对 finalIKPose 做“最后一公里”的增量修正（例如：走台阶脚贴合、轻量LookAt等）。
        /// 不订阅时完全不影响原有逻辑与性能。
        /// </summary>
        public delegate void FinalIKPosePostProcessDelegate(StateMachine machine, ref StateIKPose pose, float deltaTime);

        public event FinalIKPosePostProcessDelegate OnFinalIKPosePostProcess;

        #endregion


        #region SupportFlags（运行时约束）




        [ShowInInspector, ReadOnly, LabelText("支持标记")]
        [NonSerialized]
        public StateSupportFlags currentSupportFlags = StateSupportFlags.Grounded;

        private const StateSupportFlags LocomotionMask = StateSupportFlags.Grounded | StateSupportFlags.Swimming | StateSupportFlags.Flying | StateSupportFlags.Mounted | StateSupportFlags.Climbing;

        public void SetSupportFlags(StateSupportFlags flags)
        {
#if STATEMACHINEDEBUG
            var dbg = StateMachineDebugSettings.Instance;
            if (dbg != null && dbg.IsStateTransitionEnabled)
            {
                dbg.LogStateTransition($"设置支持标记: {flags}");
            }
#endif
            var beforeFlags = currentSupportFlags;
            currentSupportFlags = NormalizeSingleSupportFlag(flags);
            if (beforeFlags != currentSupportFlags)
            {
                RemoveUnsupportedRunningStates(currentSupportFlags);
                MarkSupportFlagsDirty();
            }
        }



        private void MarkSupportFlagsDirty()
        {
            foreach (var layer in layerRuntimes)
            {
                layer.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            }
            MarkDirty(StateDirtyReason.RuntimeChanged);
        }

        private void RemoveUnsupportedRunningStates(StateSupportFlags newFlag)
        {
            if (newFlag == StateSupportFlags.None)
            {
                return;
            }

            var allRunningStates = GetRunningStatesSnapshot();
            for (int i = 0; i < allRunningStates.Count; i++)
            {
                var state = allRunningStates[i];
                if (state == null) continue;

                var sharedData = state.stateSharedData;
                if (sharedData == null) continue;

                var basicConfig = sharedData.basicConfig;
                if (basicConfig == null) continue;

                if (basicConfig.ignoreSupportFlag)
                {
                    continue;
                }

                var stateFlag = basicConfig.stateSupportFlag;
                if (stateFlag == StateSupportFlags.None)
                {
                    continue;
                }

                if ((stateFlag & newFlag) == 0)
                {
                    if (stateLayerMap.TryGetValue(state, out var layerType))
                    {
                        TruelyDeactivateState(state, layerType);
                    }
                }
            }
        }

        [NonSerialized]
        private Dictionary<StateSupportFlags, uint> _disableTransitionMasks;

        #endregion

        #endregion

        #region 存储容器（核心/谨慎改）
        /// <summary>
        /// String键到状态的映射
        /// </summary>
        [ShowInInspector, FoldoutGroup("状态字典"), LabelText("String映射")]
        [SerializeField, SerializeReference]
        private Dictionary<string, StateBase> stringToStateMap = new Dictionary<string, StateBase>();

        /// <summary>
        /// Int键到状态的映射
        /// </summary>
        [ShowInInspector, FoldoutGroup("状态字典"), LabelText("Int映射")]
        [SerializeField, SerializeReference]
        private Dictionary<int, StateBase> intToStateMap = new Dictionary<int, StateBase>();

        /// <summary>
        /// 状态归属层级映射
        /// </summary>
        [ShowInInspector, FoldoutGroup("状态字典"), LabelText("状态层级映射")]
        [NonSerialized]
        private Dictionary<StateBase, StateLayerType> stateLayerMap = new Dictionary<StateBase, StateLayerType>();

        public int RegisteredStateCount => stringToStateMap != null ? stringToStateMap.Count : 0;

        public IEnumerable<KeyValuePair<string, StateBase>> EnumerateRegisteredStatesByKey()
        {
            if (stringToStateMap == null) yield break;
            foreach (var kvp in stringToStateMap)
            {
                yield return kvp;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStateLayerType(StateBase state, out StateLayerType layerType)
        {
            if (state == null)
            {
                layerType = default;
                return false;
            }

            return stateLayerMap.TryGetValue(state, out layerType);
        }

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
        private readonly List<StateBase> _runningStatesSnapshot = new List<StateBase>(32);

        [NonSerialized]
        private readonly List<string> _temporaryKeysCache = new List<string>(16);

        [NonSerialized]
        private readonly System.Text.StringBuilder _continuousStatsBuilder = new System.Text.StringBuilder(256);

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


        /// <summary>
        /// 默认状态键 - 状态机启动时进入的状态
        /// </summary>
        [BoxGroup("状态机设置/基础", ShowLabel = false)]
        [LabelText("默认状态键"), ValueDropdown("GetAllStateKeys")]
        public string defaultStateKey;
        #endregion


        #region 层级声明与管理（核心/谨慎改）

        /// <summary>
        /// 固定层级遮罩配置（按规范使用）
        /// </summary>
        [TitleGroup("层级设置", Order = 1)]
        [BoxGroup("层级设置/动作遮罩", ShowLabel = false)]
        [LabelText("上半身遮罩"), AssetsOnly]
        public AvatarMask upperBodyMask;

        [BoxGroup("层级设置/动作遮罩", ShowLabel = false)]
        [LabelText("下半身遮罩"), AssetsOnly]
        public AvatarMask lowerBodyMask;

        /// <summary>
        /// ★ 参考姿态Clip - 防止bind pose导致角色下陷。
        /// 当无活跃状态或状态权重之和 < 1.0时，此Clip会自动填充剩余权重，
        /// 保证 AnimationMixerPlayable 的输入权重总和始终=1.0。
        /// 建议使用1帧的站立idle动画。
        /// </summary>
        [BoxGroup("层级设置/动作遮罩", ShowLabel = false)]
        [LabelText("参考姿态动画剪辑(防下陷)"), AssetsOnly]
        [Tooltip("防止空状态时角色下陷到地面以下。建议设置为 1 帧的站立待机动画剪辑。")]
        public AnimationClip referencePoseClip;

        /// <summary>
        /// 层级运行时数据
        /// </summary>
        [ShowInInspector, LabelText("运行层级")]
        [NonSerialized]
        protected List<StateLayerRuntime> layerRuntimes = new List<StateLayerRuntime>();

        [NonSerialized]
        private Dictionary<StateLayerType, StateLayerRuntime> _layerRuntimeMap = new Dictionary<StateLayerType, StateLayerRuntime>();


        /// <summary>
        /// 通过枚举获取对应的层级
        /// </summary>
        private StateLayerRuntime GetLayerByType(StateLayerType layerType)
        {
            if (_layerRuntimeMap.TryGetValue(layerType, out var runtime))
            {
                return runtime;
            }

            return null;
        }

        /// <summary>
        /// 获取所有层级（用于遍历）
        /// </summary>
        private IEnumerable<StateLayerRuntime> GetAllLayers()
        {
            return layerRuntimes;
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
        internal AnimationLayerMixerPlayable rootMixer;

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
#if UNITY_EDITOR
            public List<StateBase>[] mergeLists;
#endif
        }

        public enum StateDirtyReason
        {
            Unknown = 0,
            Enter = 1,
            Exit = 2,
            Release = 3,
            RuntimeChanged = 5
        }

        #endregion

        #region 初始化与销毁（核心/谨慎改）

        /// <summary>
        /// 初始化状态机
        /// </summary>
        /// <param name="entity">宿主Entity</param>
        /// <param name="graph">PlayableGraph，如果为default则自动创建</param>
        /// <param name="root">外部RootMixer（可选）</param>
        public void Initialize(Entity entity, PlayableGraph graph = default, AnimationLayerMixerPlayable root = default)
        {
            if (isInitialized) return;

            hostEntity = entity;

            // 初始化StateContext（整合了原StateMachineContext和动画参数）
            stateContext = new StateMachineContext();
            stateContext.contextID = Guid.NewGuid().ToString();
            stateContext.creationTime = Time.time;
            stateContext.lastUpdateTime = Time.time;

            // 初始化层级
            InitializePipelines(graph, root);

            // 初始化SupportFlags禁用跳转缓存（超高频查询用）
            InitializeSupportFlagsTransitionCache();

            // 初始化所有状态（注意：状态初始化依赖层级已创建，所以必须在InitializePipelines之后）
            foreach (var kvp in stringToStateMap)
            {
                InitializeState(kvp.Value);
            }

            // 标记所有层级需要FallBack检查
            foreach (var layer in layerRuntimes)
            {
                layer.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            }

            isInitialized = true;
        }

        //是否直接禁用跳转
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTransitionDisabledFast(StateSupportFlags fromFlag, StateSupportFlags toFlag)
        {
            // fromFlag 是当前支持标记（通常单一）
            // toFlag 作为“目标层级掩码”直接按位判断

            if (fromFlag == StateSupportFlags.None || toFlag == StateSupportFlags.None) return false;
            return _disableTransitionMasks.TryGetValue(fromFlag, out var mask)
                && (mask & (uint)toFlag) != 0u;
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
                _disableTransitionMasks = null;
                return;
            }

            if (_disableTransitionMasks == null)
            {
                _disableTransitionMasks = new Dictionary<StateSupportFlags, uint>(8);
            }
            else
            {
                _disableTransitionMasks.Clear();
            }

            var relations = map.Relations;
            if (relations == null)
            {
                return;
            }

            for (int i = 0; i < relations.Count; i++)
            {
                var entry = relations[i];
                var fromFlag = entry.key;
                if (fromFlag == StateSupportFlags.None) continue;

                uint mask = 0u;
                var related = entry.relatedKeys;
                if (related != null)
                {
                    for (int r = 0; r < related.Count; r++)
                    {
                        var relatedFlag = related[r];
                        if (relatedFlag == StateSupportFlags.None) continue;
                        mask |= (uint)relatedFlag;
                    }
                }

                _disableTransitionMasks[fromFlag] = mask;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static StateSupportFlags NormalizeSingleSupportFlag(StateSupportFlags flag)
        {
            if (flag == StateSupportFlags.None) return StateSupportFlags.None;
            uint value = (ushort)flag;
            uint lowest = value & (~value + 1u);
            return (StateSupportFlags)(ushort)lowest;
        }

        /// <summary>
        /// 初始化状态机并绑定Animator
        /// </summary>
        public void Initialize(Entity entity, Animator animator, PlayableGraph graph = default, AnimationLayerMixerPlayable root = default)
        {
            Initialize(entity, graph, root);
            BindToAnimator(animator);
            playableGraph.Stop();
            playableGraph.Play();
        }

        /// <summary>
        /// 初始化层级系统
        /// </summary>
        private void InitializePipelines(PlayableGraph hanldegraph, AnimationLayerMixerPlayable root)
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

            int layerCount = (int)StateLayerType.Count;

            // 创建/绑定根Mixer
            if (playableGraph.IsValid())
            {
                if (root.IsValid())
                {
                    rootMixer = root;
                    if (rootMixer.GetInputCount() < layerCount)
                    {
                        rootMixer.SetInputCount(layerCount);
                    }
                }
                else
                {
                    rootMixer = AnimationLayerMixerPlayable.Create(playableGraph, layerCount);
                }
            }

            InitializeAllLayers();
            InitializeLayerWeights();
        }

        /// <summary>
        /// 初始化单个层级
        /// </summary>
        private StateLayerRuntime InitializeSingleLayer(StateLayerType layerType)
        {
#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsRuntimeInitEnabled)
                {
                    dbg.LogRuntimeInit($"开始初始化层级: {layerType}");
                }
            }
#endif
            var layer = new StateLayerRuntime(layerType, this);

            layer.avatarMask = ResolveLayerMask(layerType);
            layer.blendMode = StateLayerBlendMode.Override;
            layer.weight = GetDefaultLayerWeight(layerType);
            layer.priority = 0;
            layer.allowStateMaskOverride = false;

            // 如果有PlayableGraph,为层级创建Mixer并接入Root
            if (playableGraph.IsValid())
            {
                // ★ 仅Base层使用参考姿态Clip，slot 0 预留给参考姿态
                bool useReferencePose = (referencePoseClip != null && layerType == StateLayerType.Base);
                int initialSlotCount = useReferencePose ? 1 : 0;
                layer.mixer = AnimationMixerPlayable.Create(playableGraph, initialSlotCount);
                layer.rootInputIndex = (int)layerType;
                playableGraph.Connect(layer.mixer, 0, rootMixer, layer.rootInputIndex);
                rootMixer.SetInputWeight(layer.rootInputIndex, layer.weight);
                layer.lastAppliedRootMixerWeight = layer.weight;

                // ★ 创建参考姿态Playable（防止bind pose下陷）—— 仅Base层
                if (useReferencePose)
                {
                    layer.referencePosePlayable = AnimationClipPlayable.Create(playableGraph, referencePoseClip);
                    layer.referencePosePlayable.SetSpeed(0); // 不需要播放，只需要第0帧姿态
                    layer.referencePosePlayable.SetTime(0);
                    playableGraph.Connect(layer.referencePosePlayable, 0, layer.mixer, 0);
                    layer.mixer.SetInputWeight(0, 1f); // 初始权重=1（无其他状态时全部用参考姿态）
                    layer.hasReferencePose = true;
#if STATEMACHINEDEBUG
                    {
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null && dbg.IsRuntimeInitEnabled)
                        {
                            dbg.LogRuntimeInit($"✓ Base层参考姿态已创建 | Clip={referencePoseClip.name}");
                        }
                    }
#endif
                }

                if (layer.avatarMask != null)
                {
                    rootMixer.SetLayerMaskFromAvatarMask((uint)layer.rootInputIndex, layer.avatarMask);
                }

                rootMixer.SetLayerAdditive((uint)layer.rootInputIndex, layer.blendMode == StateLayerBlendMode.Additive);

#if STATEMACHINEDEBUG
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsRuntimeInitEnabled)
                    {
                        dbg.LogRuntimeInit($"✓ {layerType}层级Mixer创建成功 | Valid:{layer.mixer.IsValid()} | RootIndex:{layer.rootInputIndex}");
                    }
                }
#endif
            }
            else
            {
#if STATEMACHINEDEBUG
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null)
                    {
                        dbg.LogWarning($"✗ {layerType}层级Mixer创建失败 - PlayableGraph无效");
                    }
                }
#endif
            }

            return layer;
        }

        /// <summary>
        /// 初始化所有层级 - 直接装填枚举
        /// </summary>
        private void InitializeAllLayers()
        {
            layerRuntimes.Clear();
            _layerRuntimeMap.Clear();

            int layerCount = (int)StateLayerType.Count;
            for (int i = 0; i < layerCount; i++)
            {
                var layerType = (StateLayerType)i;
                var runtime = InitializeSingleLayer(layerType);
                layerRuntimes.Add(runtime);
                _layerRuntimeMap[layerType] = runtime;
            }
        }

        private static float GetDefaultLayerWeight(StateLayerType layerType)
        {
            switch (layerType)
            {
                case StateLayerType.Base:
                case StateLayerType.Main:
                case StateLayerType.UpperBody:
                case StateLayerType.LowerBody:
                    return 1f;
                case StateLayerType.Buff:
                    return 0.5f;
                default:
                    return 0f;
            }
        }

        private AvatarMask ResolveLayerMask(StateLayerType layerType)
        {
            switch (layerType)
            {
                case StateLayerType.UpperBody:
                    return upperBodyMask;
                case StateLayerType.LowerBody:
                    return lowerBodyMask;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 初始化单个状态
        /// </summary>
        public void InitializeState(StateBase state)
        {
            state.host = this;
            state.BindHostMachine(this);
        }

        /// <summary>
        /// 绑定PlayableGraph到Animator
        /// </summary>
        public bool BindToAnimator(Animator animator)
        {
            if (animator == null)
            {
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                    dbg.LogWarning($"BindToAnimator 失败：Animator 为 null | StateMachineKey={stateMachineKey}");
                }
#endif
                return false;
            }

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
                int layerCount = (int)StateLayerType.Count;
                rootMixer = AnimationLayerMixerPlayable.Create(playableGraph, layerCount);
            }

            boundAnimator = animator;

            // ★ 深度集成：自动挂载 FinalIK 驱动（如果用户有添加 BipedIK 组件则会被驱动；没有则零成本跳过）
            var driver = animator.GetComponent<StateFinalIKDriver>();
            if (driver == null)
            {
                driver = animator.gameObject.AddComponent<StateFinalIKDriver>();
            }
            driver.Bind(this, animator);

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

            InitializeLayerWeights();

#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsRuntimeInitEnabled)
                {
                    dbg.LogRuntimeInit($"Animator绑定成功: {animator.gameObject.name}");
                }
            }
#endif
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

        private List<StateBase> GetRunningStatesList()
        {
            return runningStates.Items;
        }

        /// <summary>
        /// 获取 RunningStates 的稳定快照（复用缓存，零GC）。
        /// 当遍历过程中可能触发状态切换（Enter/Exit）导致 runningStates 变更时，必须使用快照。
        /// </summary>
        private List<StateBase> GetRunningStatesSnapshot()
        {
            var src = runningStates.Items;
            var dst = _runningStatesSnapshot;
            dst.Clear();

            for (int i = 0; i < src.Count; i++)
            {
                var state = src[i];
                if (state != null)
                {
                    dst.Add(state);
                }
            }

            return dst;
        }

        private StateBase GetFirstRunningState(StateLayerRuntime layer)
        {
            var runningStates = GetRunningStatesList();
            for (int i = 0; i < runningStates.Count; i++)
            {
                var state = runningStates[i];
                if (stateLayerMap.TryGetValue(state, out var layerType) && layerType == layer.layerType)
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

        private StateLayerType ResolveLayerForState(StateBase targetState, StateLayerType layer)
        {
            if (layer != StateLayerType.NotClear)
            {
                return layer;
            }

            return targetState.stateSharedData.basicConfig.layerType;
        }

        private StateActivationCache GetOrCreateActivationCache(StateBase targetState)
        {
            if (!_activationCache.TryGetValue(targetState, out var cache) || cache == null)
            {
                cache = new StateActivationCache();
                _activationCache[targetState] = cache;
            }

            int layerCount = (int)StateLayerType.Count;
            if (cache.versions == null || cache.versions.Length != layerCount)
            {
                cache.versions = new int[layerCount];
                cache.results = new StateActivationResult[layerCount];
                cache.interruptLists = new List<StateBase>[layerCount];
#if UNITY_EDITOR
                cache.mergeLists = new List<StateBase>[layerCount];
#endif

                for (int i = 0; i < layerCount; i++)
                {
                    cache.versions[i] = -1;
                    cache.interruptLists[i] = new List<StateBase>(4);
#if UNITY_EDITOR
                    cache.mergeLists[i] = new List<StateBase>(4);
#endif
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

        private StateChannelMask EvaluateChannelMask()
        {
            StateChannelMask mask = StateChannelMask.None;
            var allRunningStates = GetRunningStatesList();
            for (int i = 0; i < allRunningStates.Count; i++)
            {
                var resolved = allRunningStates[i].ResolvedConfig;
                mask |= resolved.channelMask;
            }

            return mask;
        }



        /// <summary>
        /// 销毁状态机，释放资源
        /// ★ 修复：增加_temporaryStates清理、状态Playable销毁、IK/MatchTarget资源释放
        /// </summary>
        public void Dispose()
        {
            // 解绑 FinalIK 驱动：避免 driver 继续引用已 Dispose 的 StateMachine
            if (boundAnimator != null)
            {
                var driver = boundAnimator.GetComponent<StateFinalIKDriver>();
                if (driver != null)
                {
                    driver.Unbind();
                }
            }

            // 停用所有运行中的状态
            var allRunningStates = GetRunningStatesSnapshot();
            for (int i = 0; i < allRunningStates.Count; i++)
            {
                var state = allRunningStates[i];
                if (state == null) continue;
                state.OnStateExit();
                // ★ 修复：销毁每个状态的Playable资源（之前漏掉导致Playable泄漏）
                state.DestroyPlayable();
            }

            runningStates.Clear();

            // ★ 修复：清理临时状态（之前完全漏掉）
            if (_temporaryStates.Count > 0)
            {
                foreach (var kvp in _temporaryStates)
                {
                    if (kvp.Value != null)
                    {
                        kvp.Value.OnStateExit();
                        kvp.Value.DestroyPlayable();
                        kvp.Value.TryAutoPushedToPool();
                    }
                }
                _temporaryStates.Clear();
            }

            // 清理层级
            foreach (var layer in GetAllLayers())
            {
                // ★ 修复：确保层级内运行状态的Playable也被清理
                foreach (var state in layer.runningStates)
                {
                    state.DestroyPlayable();
                }
                layer.runningStates.Clear();

                // 清理Playable槽位映射
                layer.stateToSlotMap.Clear();
                layer.InternalClearConnectedStates();
                layer.freeSlots.Clear();

                // ★ 清理淡出状态池
                foreach (var kvp in layer.fadeOutStates)
                {
                    kvp.Value?.TryAutoPushedToPool();
                }
                layer.fadeOutStates.Clear();

                foreach (var kvp in layer.fadeInStates)
                {
                    kvp.Value?.TryAutoPushedToPool();
                }
                layer.fadeInStates.Clear();

                if (layer.mixer.IsValid())
                    layer.mixer.Destroy();
            }

            layerRuntimes.Clear();
            _layerRuntimeMap.Clear();

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
            stateLayerMap.Clear();
            _activationCache.Clear();

            // 清理上下文
            if (stateContext != null)
            {
                stateContext.Clear();
            }

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
#if STATEMACHINEDEBUG
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    dbg?.LogWarning($"状态机 {stateMachineKey} 未初始化，无法启动");
                }
#endif
                return;
            }

            if (boundAnimator == null)
            {
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                    dbg.LogWarning($"状态机 {stateMachineKey} 未绑定Animator，无法启动（请先调用 BindToAnimator）");
                }
#endif
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
                TryActivateState(defaultStateKey, StateLayerType.NotClear);
            }
        }

        /// <summary>
        /// 停止状态机
        /// </summary>
        public void StopStateMachine()
        {
            if (!isRunning) return;

            // 停止所有层级
            foreach (var layer in GetAllLayers())
            {
                DeactivateLayer(layer.layerType);

                // ★ 可靠性收口：Stop 之后不会再 UpdateFades，因此必须强制清理残留 fade 数据并卸载仍连接的 Playable。
                // 否则：fadeOut 未完成的状态会一直残留在 stateToSlotMap 上，导致重启后 HotPlug 跳过/槽位耗尽等隐性问题。
                ForceClearLayerFadesAndResidualPlayables(layer);
            }

            // 停止PlayableGraph
            if (playableGraph.IsValid())
            {
                playableGraph.Stop();
            }

            isRunning = false;
        }

        /// <summary>
        /// ★ Stop/强制清理用：
        /// - 清空该层的 fadeIn/fadeOut 数据（回收池对象）
        /// - 卸载仍连接在 mixer 上的残留状态 Playable（通常来自未完成的 fadeOut）
        ///
        /// 注意：不触发 OnStateExit / 事件回调；逻辑退出应由上层调用 TruelyDeactivateState 处理。
        /// </summary>
        private void ForceClearLayerFadesAndResidualPlayables(StateLayerRuntime layer)
        {
            if (layer == null) return;

            // 1) 清理 fade 数据（回收对象池）
            if (layer.fadeInStates.Count > 0)
            {
                foreach (var kvp in layer.fadeInStates)
                {
                    kvp.Value?.TryAutoPushedToPool();
                }
                layer.fadeInStates.Clear();
            }

            if (layer.fadeOutStates.Count > 0)
            {
                foreach (var kvp in layer.fadeOutStates)
                {
                    kvp.Value?.TryAutoPushedToPool();
                }
                layer.fadeOutStates.Clear();
            }

            // 2) 卸载仍连接的残留状态（只要还在 stateToSlotMap，就说明 playable 还挂在 mixer 上）
            if (layer.stateToSlotMap.Count > 0)
            {
                var buffer = _statesToDeactivateCache;
                buffer.Clear();
                foreach (var kvp in layer.stateToSlotMap)
                {
                    if (kvp.Key != null) buffer.Add(kvp.Key);
                }

                for (int i = 0; i < buffer.Count; i++)
                {
                    HotUnplugStateFromPlayable(buffer[i], layer);
                }

                buffer.Clear();
            }
        }

        /// <summary>
        /// 更新状态机 - 每帧调用
        /// 注意：Animator需要设置为：
        /// 1. Update Mode = Normal (允许脚本控制)
        /// 2. Culling Mode = Always Animate (即使不可见也更新)
        /// 3. 不要勾选 Apply Root Motion（除非需要根运动）
        /// 
        /// Dirty机制：根据各层级的Dirty等级执行不同任务
        /// - Dirty >= 1: 执行FallBack自动激活检查
        /// - Dirty >= 2: 执行中等优先级任务（预留）
        /// - Dirty >= 3: 执行高优先级任务（预留）
        /// </summary>
        public void UpdateStateMachine()
        {
            if (!isRunning) return;

            if (stateContext == null)
            {
#if STATEMACHINEDEBUG
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                    dbg.LogWarning($"UpdateStateMachine 被调用但 stateContext 为 null | StateMachineKey={stateMachineKey}");
                }
#endif
                return;
            }

            float deltaTime = Time.deltaTime;

            // SupportFlags由StateMachine统一维护，无需同步

            // 更新上下文时间
            stateContext.lastUpdateTime = Time.time;

            // 更新所有运行中的状态
            var statesToDeactivate = _statesToDeactivateCache; // 收集需要自动退出的状态
            statesToDeactivate.Clear();
            var allRunningStates = GetRunningStatesSnapshot();
            for (int i = 0; i < allRunningStates.Count; i++)
            {
                var state = allRunningStates[i];
                if (state == null) continue;
                if (state.baseStatus != StateBaseStatus.Running) continue;

                state.OnStateUpdate();

                // 统一推进进入时长：不应依赖动画更新函数是否被调用。
                state.hasEnterTime += deltaTime;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (state.stateSharedData == null) throw new InvalidOperationException("RunningState 的 stateSharedData 不能为空（应在注册/初始化期保证）");
#endif
                if (state._hasAnimationCached)
                {
                    state.UpdateAnimationRuntime(stateContext, deltaTime);
                    state.ProcessMatchTarget(boundAnimator);
                }

                // ★ 检查是否应该自动退出（按持续时间模式）
                if (state.ShouldAutoExit(Time.time))
                {
                    statesToDeactivate.Add(state);
                }
            }

            // 自动退出已完成的状态
            foreach (var state in statesToDeactivate)
            {
                // 使用缓存的层级映射直接停用
                if (stateLayerMap.TryGetValue(state, out var layerType))
                {
                    TruelyDeactivateState(state, layerType);
                }
            }

            // ★ 更新淡入淡出效果
            UpdateFades(deltaTime);

            // ★ 统一批处理写入 Mixer 输入权重（含参考姿态填充/归一化）
            UpdateMixerInputWeights();


            // 更新层级Dirty自动标记（高等级降级到1，保持最低Dirty用于持续检查FallBack）
            foreach (var layer in layerRuntimes)
            {
                layer.UpdateDirtyDecay();
            }

            // 根据Dirty等级处理不同任务（包括FallBack自动激活）
            foreach (var layer in layerRuntimes)
            {
                ProcessDirtyTasks(layer, layer.layerType);
            }

            // 同步层级权重到RootMixer
            UpdateLayerWeights();

            // ★ FinalIK Pose 聚合：每帧从所有 Running State 的 runtime.ik 生成最终 Pose（LateUpdate 驱动输出）
            UpdateFinalIKPoseCache(deltaTime);

            // Manual模式下需要手动Evaluate推进图
            if (playableGraph.IsValid())
            {
                if (!playableGraph.IsPlaying())
                {
                    playableGraph.Play();
                }
#if STATEMACHINEDEBUG
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsPerformanceEnabled)
                    {
                        dbg.LogPerformance(
                            $"[StateMachine] 手动评估PlayableGraph，DeltaTime: {deltaTime:F4}" +
                            playableGraph.GetTimeUpdateMode() +
                            playableGraph.IsPlaying() +
                            playableGraph.IsValid());
                    }
                }
#endif
                playableGraph.Evaluate(deltaTime);
            }

#if UNITY_EDITOR
#if STATEMACHINEDEBUG
            // 持续输出统计信息（可选）
            if (enableContinuousStats)
            {
                OutputContinuousStats();
            }
#endif
#endif

        }

        private void UpdateFinalIKPoseCache(float deltaTime)
        {
            finalIKPose.Reset();

            // 临时容器：按层级做 override（上层优先），同层多状态做权重混合
            StateIKPose upper = default;
            StateIKPose lower = default;
            StateIKPose buff = default;
            StateIKPose main = default;
            StateIKPose @base = default;
            upper.Reset();
            lower.Reset();
            buff.Reset();
            main.Reset();
            @base.Reset();

            var allRunningStates = GetRunningStatesList();
            for (int i = 0; i < allRunningStates.Count; i++)
            {
                var state = allRunningStates[i];
                if (state == null || state.baseStatus != StateBaseStatus.Running) continue;

                var runtime = state.AnimationRuntime;
                if (runtime == null || !runtime.ik.enabled) continue;
                if (runtime.ik.weight < 0.001f) continue;

                float master = runtime.ik.weight * state.PlayableWeight;
                if (master < 0.001f) continue;

                if (!stateLayerMap.TryGetValue(state, out var layerType))
                {
                    layerType = state.stateSharedData != null ? state.stateSharedData.basicConfig.layerType : StateLayerType.Main;
                }

                ref var target = ref GetLayerPoseRef(layerType, ref upper, ref lower, ref buff, ref main, ref @base);
                AccumulateFromRuntime(ref target, state, master);
            }

            // 按层级 override 合成：UpperBody > LowerBody > Buff > Main > Base
            ComposeOverride(ref finalIKPose, ref upper);
            ComposeOverride(ref finalIKPose, ref lower);
            ComposeOverride(ref finalIKPose, ref buff);
            ComposeOverride(ref finalIKPose, ref main);
            ComposeOverride(ref finalIKPose, ref @base);

            // 输出权重 clamp
            ClampPoseWeights(ref finalIKPose);

            // 外部后处理（可选）：例如脚贴合台阶等
            var cb = OnFinalIKPosePostProcess;
            if (cb != null)
            {
                cb(this, ref finalIKPose, deltaTime);
                ClampPoseWeights(ref finalIKPose);
            }
        }

        private static ref StateIKPose GetLayerPoseRef(
            StateLayerType layerType,
            ref StateIKPose upper,
            ref StateIKPose lower,
            ref StateIKPose buff,
            ref StateIKPose main,
            ref StateIKPose @base)
        {
            switch (layerType)
            {
                case StateLayerType.UpperBody: return ref upper;
                case StateLayerType.LowerBody: return ref lower;
                case StateLayerType.Buff: return ref buff;
                case StateLayerType.Base: return ref @base;
                case StateLayerType.Main:
                default: return ref main;
            }
        }

        private static void AccumulateFromRuntime(ref StateIKPose pose, StateBase state, float master)
        {
            ref var ik = ref state.AnimationRuntime.ik;

            AccumulateGoal(ref pose.leftHand, ik.leftHandWeight * master, ik.leftHandPosition, ik.leftHandRotation, ik.leftHandHintPosition);
            AccumulateGoal(ref pose.rightHand, ik.rightHandWeight * master, ik.rightHandPosition, ik.rightHandRotation, ik.rightHandHintPosition);
            AccumulateGoal(ref pose.leftFoot, ik.leftFootWeight * master, ik.leftFootPosition, ik.leftFootRotation, ik.leftFootHintPosition);
            AccumulateGoal(ref pose.rightFoot, ik.rightFootWeight * master, ik.rightFootPosition, ik.rightFootRotation, ik.rightFootHintPosition);

            float lookW = ik.lookAtWeight * master;
            if (lookW > 0.001f)
            {
                AccumulateLookAt(ref pose, lookW, ik.lookAtPosition, state);
            }
        }

        private static void AccumulateGoal(ref IKGoalPose goal, float w, Vector3 pos, Quaternion rot, Vector3 hintPos)
        {
            if (w <= 0.001f) return;

            if (goal.weight <= 0.001f)
            {
                goal.weight = w;
                goal.position = pos;
                goal.rotation = rot;
                goal.hintPosition = hintPos;
                return;
            }

            float newW = goal.weight + w;
            float t = w / newW;
            goal.position = Vector3.Lerp(goal.position, pos, t);
            goal.rotation = Quaternion.Slerp(goal.rotation, rot, t);

            if (hintPos != Vector3.zero)
            {
                goal.hintPosition = (goal.hintPosition == Vector3.zero) ? hintPos : Vector3.Lerp(goal.hintPosition, hintPos, t);
            }

            goal.weight = newW;
        }

        private static void AccumulateLookAt(ref StateIKPose pose, float w, Vector3 lookAtPos, StateBase state)
        {
            if (pose.lookAtWeight <= 0.001f)
            {
                pose.lookAtWeight = w;
                pose.lookAtPosition = lookAtPos;
                ApplyLookAtConfig(ref pose, state);
                return;
            }

            float newW = pose.lookAtWeight + w;
            float t = w / newW;
            pose.lookAtPosition = Vector3.Lerp(pose.lookAtPosition, lookAtPos, t);
            pose.lookAtWeight = newW;
        }

        private static void ApplyLookAtConfig(ref StateIKPose pose, StateBase state)
        {
            var animConfig = state.stateSharedData != null ? state.stateSharedData.animationConfig : null;
            if (animConfig != null && animConfig.enableIK && animConfig.ikLookAt.enabled)
            {
                var cfg = animConfig.ikLookAt;
                pose.lookAtBodyWeight = cfg.bodyWeight;
                pose.lookAtHeadWeight = cfg.headWeight;
                pose.lookAtEyesWeight = cfg.eyesWeight;
                pose.lookAtClampWeight = cfg.clampWeight;
            }
            else
            {
                pose.lookAtBodyWeight = 0.5f;
                pose.lookAtHeadWeight = 1f;
                pose.lookAtEyesWeight = 1f;
                pose.lookAtClampWeight = 0.5f;
            }
        }

        private static void ComposeOverride(ref StateIKPose dst, ref StateIKPose src)
        {
            if (dst.leftHand.weight <= 0.001f && src.leftHand.weight > 0.001f) dst.leftHand = src.leftHand;
            if (dst.rightHand.weight <= 0.001f && src.rightHand.weight > 0.001f) dst.rightHand = src.rightHand;
            if (dst.leftFoot.weight <= 0.001f && src.leftFoot.weight > 0.001f) dst.leftFoot = src.leftFoot;
            if (dst.rightFoot.weight <= 0.001f && src.rightFoot.weight > 0.001f) dst.rightFoot = src.rightFoot;

            if (dst.lookAtWeight <= 0.001f && src.lookAtWeight > 0.001f)
            {
                dst.lookAtWeight = src.lookAtWeight;
                dst.lookAtPosition = src.lookAtPosition;
                dst.lookAtBodyWeight = src.lookAtBodyWeight;
                dst.lookAtHeadWeight = src.lookAtHeadWeight;
                dst.lookAtEyesWeight = src.lookAtEyesWeight;
                dst.lookAtClampWeight = src.lookAtClampWeight;
            }
        }

        private static void ClampPoseWeights(ref StateIKPose pose)
        {
            pose.leftHand.weight = Mathf.Clamp01(pose.leftHand.weight);
            pose.rightHand.weight = Mathf.Clamp01(pose.rightHand.weight);
            pose.leftFoot.weight = Mathf.Clamp01(pose.leftFoot.weight);
            pose.rightFoot.weight = Mathf.Clamp01(pose.rightFoot.weight);
            pose.lookAtWeight = Mathf.Clamp01(pose.lookAtWeight);
        }



        /// <summary>
        /// 同步所有层级权重到RootMixer
        /// </summary>
        private void UpdateLayerWeights()
        {
            if (!rootMixer.IsValid()) return;

            var mixer = rootMixer;
            for (int i = 0; i < layerRuntimes.Count; i++)
            {
                var layer = layerRuntimes[i];
                int index = layer.rootInputIndex;
                if (index >= 0)
                {
                    if (Mathf.Abs(layer.lastAppliedRootMixerWeight - layer.weight) > 0.0001f)
                    {
                        mixer.SetInputWeight(index, layer.weight);
                        layer.lastAppliedRootMixerWeight = layer.weight;
                    }
                }
            }
        }

        /// <summary>
        /// 初始化所有层级权重到RootMixer
        /// </summary>
        private void InitializeLayerWeights()
        {
            UpdateLayerWeights();
        }

        #region 淡入淡出系统（核心/谨慎改）

        /// <summary>
        /// 应用淡入效果到新激活的状态（仅表现层，不影响逻辑）
        /// </summary>
        private void ApplyFadeIn(StateBase state, StateLayerRuntime layer)
        {
            if (!state.stateSharedData.enableFadeInOut) return;
            if (state.stateSharedData.basicConfig.useDirectBlend) return;

            float fadeInDuration = GetScaledFadeDuration(state.stateSharedData.fadeInDuration, state.stateSharedData);
            if (fadeInDuration <= 0f || !layer.stateToSlotMap.ContainsKey(state))
                return;

            // 初始化淡入：权重从0开始
            int slotIndex = layer.stateToSlotMap[state];
            state.SetPlayableWeightAssumeBound(0f);

            // 记录淡入数据（需要在StateLayerRuntime中添加字段）
            if (!layer.fadeInStates.TryGetValue(state, out var fadeData))
            {
                fadeData = StateFadeData.Pool.GetInPool();
                fadeData.elapsedTime = 0f;
                fadeData.duration = fadeInDuration;
                fadeData.slotIndex = slotIndex;
                fadeData.startWeight = 1f;
                layer.fadeInStates[state] = fadeData;

                StateMachineDebugSettings.Instance.LogFade(
                    $"[淡入] 状态 {state.strKey} 开始淡入，时长 {fadeInDuration:F2}秒");
            }
        }

        /// <summary>
        /// 应用淡出效果到即将停用的状态（仅表现层，不影响逻辑）
        /// 注意：触发淡出时，状态逻辑已执行退出。
        /// </summary>
        private void ApplyFadeOut(StateBase state, StateLayerRuntime layer)
        {
            if (!state.stateSharedData.enableFadeInOut) return;
            if (state.stateSharedData.basicConfig.useDirectBlend) return;

            float fadeOutDuration = GetScaledFadeDuration(state.stateSharedData.fadeOutDuration, state.stateSharedData);
            if (fadeOutDuration <= 0f || !layer.stateToSlotMap.ContainsKey(state))
                return;

            // 记录淡出数据
            int slotIndex = layer.stateToSlotMap[state];
            float currentWeight = state.PlayableWeight;

            if (!layer.fadeOutStates.TryGetValue(state, out var fadeData))
            {
                fadeData = StateFadeData.Pool.GetInPool();
                fadeData.elapsedTime = 0f;
                fadeData.duration = fadeOutDuration;
                fadeData.slotIndex = slotIndex;
                fadeData.startWeight = currentWeight;
                layer.fadeOutStates[state] = fadeData;

                StateMachineDebugSettings.Instance.LogFade(
                    $"[淡出] 状态 {state.strKey} 开始淡出，时长 {fadeOutDuration:F2}秒，起始权重 {currentWeight:F2}");
            }
        }

        /// <summary>
        /// 更新所有层级的淡入淡出效果
        /// </summary>
        private void UpdateFades(float deltaTime)
        {
            foreach (var layer in layerRuntimes)
            {
                UpdateLayerFades(layer, deltaTime);
            }
        }

        /// <summary>
        /// ★ 批处理写入每层 Mixer 的输入权重（零GC）。
        /// - 所有状态的外部权重写入统一集中在这里，避免 StateBase 每次 SetPlayableWeight 都调用 SetInputWeight。
        /// - Base 层含参考姿态：额外做“参考姿态填充/归一化”，确保输入权重和≈1，避免 bind pose 混入下陷。
        /// </summary>
        private void UpdateMixerInputWeights()
        {
            for (int layerIdx = 0; layerIdx < layerRuntimes.Count; layerIdx++)
            {
                var layer = layerRuntimes[layerIdx];
                if (!layer.mixer.IsValid()) continue;
                if (!layer.HasDirtyFlag(PipelineDirtyFlags.MixerWeights)) continue;

                // 非参考姿态层：只需要把 state.PlayableWeight 写回 mixer 一次即可
                if (!layer.hasReferencePose)
                {
                    var states = layer.connectedStates;
                    var slots = layer.connectedSlots;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (states.Count != slots.Count) throw new InvalidOperationException($"connectedStates/connectedSlots 计数不一致: {layer.layerType}");
#endif
                    for (int i = 0; i < states.Count; i++)
                    {
                        var state = states[i];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (state == null) throw new InvalidOperationException($"connectedStates 存在 null: {layer.layerType}");
#endif
                        layer.mixer.SetInputWeight(slots[i], state.PlayableWeight);
                    }

                    layer.ClearDirty(PipelineDirtyFlags.MixerWeights);
                    continue;
                }

                int inputCount = layer.mixer.GetInputCount();
                if (inputCount <= 1)
                {
                    // 只有参考姿态自己，权重=1
                    layer.mixer.SetInputWeight(0, 1f);
                    layer.referencePoseWeightsNormalized = false;
                    layer.ClearDirty(PipelineDirtyFlags.MixerWeights);
                    continue;
                }

                // ★ 核心：从状态对象读取"期望权重"（state.PlayableWeight），
                //   而非从mixer读取——这样不受上帧归一化的污染
                float intendedWeightSum = 0f;
                {
                    var states = layer.connectedStates;
                    for (int i = 0; i < states.Count; i++)
                    {
                        intendedWeightSum += states[i].PlayableWeight;
                    }
                }

                if (intendedWeightSum <= 1f)
                {
                    // ★ 权重不足1.0：参考姿态自动填充剩余，防止bind pose下陷
                    // 注意：外部权重写入已延后到这里，因此每次 dirty 都需要把状态权重写回 mixer。
                    {
                        var states = layer.connectedStates;
                        var slots = layer.connectedSlots;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (states.Count != slots.Count) throw new InvalidOperationException($"connectedStates/connectedSlots 计数不一致: {layer.layerType}");
#endif
                        for (int i = 0; i < states.Count; i++)
                        {
                            layer.mixer.SetInputWeight(slots[i], states[i].PlayableWeight);
                        }
                    }
                    layer.mixer.SetInputWeight(0, 1f - intendedWeightSum);
                    layer.referencePoseWeightsNormalized = false;
                }
                else
                {
                    // ★ 权重超过1.0：归一化所有状态权重，防止角色浮起
                    // 参考姿态权重=0（活跃状态已完全覆盖）
                    // 因为每帧都从state.PlayableWeight重新读取，不会永久破坏权重
                    // 当其他状态淡出后sum回落<=1.0，权重自动恢复为原始值
                    layer.mixer.SetInputWeight(0, 0f);
                    float invSum = 1f / intendedWeightSum;
                    {
                        var states = layer.connectedStates;
                        var slots = layer.connectedSlots;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (states.Count != slots.Count) throw new InvalidOperationException($"connectedStates/connectedSlots 计数不一致: {layer.layerType}");
#endif
                        for (int i = 0; i < states.Count; i++)
                        {
                            layer.mixer.SetInputWeight(slots[i], states[i].PlayableWeight * invSum);
                        }
                    }

                    layer.referencePoseWeightsNormalized = true;
                }

                layer.ClearDirty(PipelineDirtyFlags.MixerWeights);
            }
        }

        /// <summary>
        /// 更新单个层级的淡入淡出效果
        /// </summary>
        private void UpdateLayerFades(StateLayerRuntime layer, float deltaTime)
        {
            if (layer.fadeInStates.Count == 0 && layer.fadeOutStates.Count == 0)
                return;

            // 更新淡入状态
            var fadeInToRemove = layer.fadeInToRemoveCache;
            fadeInToRemove.Clear();
            foreach (var kvp in layer.fadeInStates)
            {
                var state = kvp.Key;
                var fadeData = kvp.Value;

                fadeData.elapsedTime += deltaTime;
                float t = Mathf.Clamp01(fadeData.elapsedTime / fadeData.duration);
                float eased = EvaluateFadeCurve(state, t, isFadeIn: true);
                float weight = Mathf.Lerp(0f, 1f, eased);
                state.SetPlayableWeightAssumeBound(weight);

                if (t >= 1f)
                {
                    fadeInToRemove.Add(state);
#if STATEMACHINEDEBUG
                    StateMachineDebugSettings.Instance.LogFade(
                        $"[淡入完成] 状态 {state.strKey}");
#endif
                }
            }

            // 移除已完成的淡入状态
            foreach (var state in fadeInToRemove)
            {
                if (layer.fadeInStates.TryGetValue(state, out var fadeData))
                {
                    fadeData.TryAutoPushedToPool();
                }
                layer.fadeInStates.Remove(state);
            }

            // 更新淡出状态
            var fadeOutToRemove = layer.fadeOutToRemoveCache;
            fadeOutToRemove.Clear();
            foreach (var kvp in layer.fadeOutStates)
            {
                var state = kvp.Key;
                var fadeData = kvp.Value;

                fadeData.elapsedTime += deltaTime;
                float t = Mathf.Clamp01(fadeData.elapsedTime / fadeData.duration);
                float eased = EvaluateFadeCurve(state, t, isFadeIn: false);
                float weight = Mathf.Lerp(fadeData.startWeight, 0f, eased);
                state.SetPlayableWeightAssumeBound(weight);

                if (t >= 1f)
                {
                    fadeOutToRemove.Add(state);
                    HotUnplugStateFromPlayable(state, layer);
#if STATEMACHINEDEBUG
                    StateMachineDebugSettings.Instance.LogFade(
                        $"[淡出完成] 状态 {state.strKey}");
#endif
                }
            }

            // 移除已完成的淡出状态
            foreach (var state in fadeOutToRemove)
            {
                if (layer.fadeOutStates.TryGetValue(state, out var fadeData))
                {
                    fadeData.TryAutoPushedToPool();
                }
                layer.fadeOutStates.Remove(state);
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

        /// <summary>
        /// ★ 清理状态的残留fade数据（防止快速重入时旧fadeOut覆盖新fadeIn）
        /// 场景：状态A退出(开始fadeOut 1→0) → 立即重新进入A → 旧fadeOut仍在字典中
        /// → 下一帧fadeOut把权重压回0 → 玩家看到"没有动画"
        /// 
        /// 必须在 HotPlugStateToPlayable 之前调用
        /// 因为旧的 stateToSlotMap 映射还存在会导致 HotPlug 判断为"已存在"而跳过
        /// </summary>
        private void CancelStaleFadeData(StateBase state, StateLayerRuntime layer)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (layer == null) throw new ArgumentNullException(nameof(layer));
#endif

            // 清理残留的fadeOut（最关键！）
            // fadeOut期间：状态的旧Playable仍然连接在mixer上，stateToSlotMap映射也还在
            // 必须先卸载旧Playable，否则HotPlugStateToPlayable会跳过
            if (layer.fadeOutStates.TryGetValue(state, out var fadeOutData))
            {
                // 卸载旧的Playable连接（这会清理stateToSlotMap和销毁旧Playable）
                HotUnplugStateFromPlayable(state, layer);

                fadeOutData.TryAutoPushedToPool();
                layer.fadeOutStates.Remove(state);
#if STATEMACHINEDEBUG
                StateMachineDebugSettings.Instance.LogFade(
                    $"[Fade修复] 取消状态 {state.strKey} 的残留fadeOut（快速重入）");
#endif
            }

            // 清理残留的fadeIn（边界情况：fadeIn未完成就退出再进入）
            if (layer.fadeInStates.TryGetValue(state, out var fadeInData))
            {
                fadeInData.TryAutoPushedToPool();
                layer.fadeInStates.Remove(state);
#if STATEMACHINEDEBUG
                StateMachineDebugSettings.Instance.LogFade(
                    $"[Fade修复] 取消状态 {state.strKey} 的残留fadeIn（快速重入）");
#endif
            }
        }

        #endregion

        /// <summary>
        /// 根据层级的Dirty标记处理不同任务
        /// </summary>
        private void ProcessDirtyTasks(StateLayerRuntime layerData, StateLayerType layer)
        {
            if (!layerData.IsDirty) return;

            if (layerData.HasDirtyFlag(PipelineDirtyFlags.HighPriority))
            {
                // 可在此添加高优先级任务
            }

            if (layerData.HasDirtyFlag(PipelineDirtyFlags.MediumPriority))
            {
                // 可在此添加中等优先级任务
            }

            if (layerData.HasDirtyFlag(PipelineDirtyFlags.HotPlug))
            {
                // 热插拔相关任务（预留）
                layerData.ClearDirty(PipelineDirtyFlags.HotPlug);
            }

            if (layerData.HasDirtyFlag(PipelineDirtyFlags.FallbackCheck))
            {
                // 如果层级空闲，尝试激活FallBack状态
                if (layerData.runningStates.Count == 0)
                {
                    // Debug.Log($"[FallBack-Activate] ⚠ [{pipeline}] 层级已空，检查FallBack配置...");
                    // Debug.Log($"[FallBack-Activate]   DefaultSupportFlag={pipelineData.DefaultSupportFlag}");

                    // 使用支持标记FallBack系统
                    int fallbackStateId = layerData.GetFallBack(currentSupportFlags); // 使用当前SupportFlags

                    if (fallbackStateId >= 0)
                    {
                        // Debug.Log($"[FallBack-Activate] 🔍 查找FallBack状态: StateID={fallbackStateId}");
                        var fallbackState = GetStateByInt(fallbackStateId);

                        bool activated = TryActivateState(fallbackState, layer);
                        if (activated)
                        {
                            layerData.ClearDirty(PipelineDirtyFlags.FallbackCheck);
                        }
                        else
                        {
                            //Debug.LogWarning($"[FallBack-Activate] ✗ 未找到FallBack状态(ID={fallbackStateId})，层级将保持空闲");
                        }
                    }
                    else
                    {
                        // Debug.Log($"[FallBack-Activate] ⊘ [{pipeline}] 未配置FallBack状态(StateID={fallbackStateId})，层级保持空闲");
                    }
                }
                else
                {
                    // Debug.Log($"[FallBack-Activate] [{layer}] 层级仍有{layerData.runningStates.Count}个运行状态，无需FallBack");
                    // 层级非空时也清除FallBack标记
                    layerData.ClearDirty(PipelineDirtyFlags.FallbackCheck);
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
        /// <returns>成功返回 StateBase，失败返回 null</returns>
        public StateBase RegisterStateFromInfo(StateAniDataInfo info, bool allowOverride = false)
        {
            return RegisterStateFromInfo(info, null, allowOverride);
        }

        /// <summary>
        /// 从StateAniDataInfo注册状态 - 支持自定义String键
        /// </summary>
        /// <param name="info">状态数据Info</param>
        /// <param name="customStringKey">自定义String键（null则使用info中的stateName）</param>
        /// <param name="allowOverride">是否允许覆盖已存在的状态</param>
        /// <returns>成功返回 StateBase，失败返回 null</returns>
        public StateBase RegisterStateFromInfo(StateAniDataInfo info, string customStringKey, bool allowOverride = false)
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
        /// <returns>成功返回 StateBase，失败返回 null</returns>
        public StateBase RegisterStateFromInfo(StateAniDataInfo info, string customStringKey, int? customIntKey, bool allowOverride = false)
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

                // 4. 获取层级类型
                var layerType = info.sharedData.basicConfig.layerType;

                // 5. 注册状态（使用自定义键或原始键）
                bool registered;
                if (customStringKey != null || customIntKey.HasValue)
                {
                    // 使用了自定义键，直接注册
                    registered = RegisterStateCore(finalStringKey, finalIntKey, state, layerType);
                    if (!registered && !allowOverride)
                    {
                        // 键冲突时自动处理
                        registered = RegisterState(state, layerType, allowOverride);
                    }
                }
                else
                {
                    // 使用原始键，自动处理冲突
                    registered = RegisterState(state, layerType, allowOverride);
                }

                if (registered)
                {
                    StateMachineDebugSettings.Instance.LogStateTransition(
                        $"✓ 注册状态: [{layerType}] {state.strKey} (ID:{state.intKey})");
                    return state;
                }
                else
                {
                    StateMachineDebugSettings.Instance.LogWarning($"注册状态失败: {info.sharedData.basicConfig.stateName}");
                }
                return null;
            }
            catch (Exception e)
            {
                StateMachineDebugSettings.Instance.LogError($"注册状态异常: {info.sharedData.basicConfig.stateName}\n{e}");
                return null;
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
        private bool RegisterState(StateBase state, StateLayerType layer, bool allowOverride = false)
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
                    StateMachineDebugSettings.Instance.LogStateTransition(
                        $"⚠️ String键冲突: '{originalName}' → '{finalName}'");
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
            return RegisterStateCore(finalName, finalId, state, layer);
        }

        /// <summary>
        /// 注册新状态（String键）- 智能处理键冲突
        /// </summary>
        public bool RegisterState(string stateKey, StateBase state, StateLayerType layer = StateLayerType.NotClear)
        {
            if (string.IsNullOrEmpty(stateKey))
            {
                StateMachineDebugSettings.Instance.LogError("状态键不能为空");
                return false;
            }
            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogError("状态实例不能为空");
                return false;
            }

            layer = ResolveLayerForState(state, layer);

            // String键重复时自动添加后缀（_r1, _r2...）
            string finalStateKey = stateKey;
            int renameAttempt = 0;
            while (stringToStateMap.ContainsKey(finalStateKey))
            {
                renameAttempt++;
                finalStateKey = $"{stateKey}_r{renameAttempt}";
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"⚠️ String键冲突: '{stateKey}'已存在，自动重命名为'{finalStateKey}'");
            }

            // 自动分配IntKey（从SharedData获取或自动生成）
            int autoIntKey = GenerateUniqueIntKey(state);
            // Int键冲突时会自动跳过到下一个可用ID（GenerateUniqueIntKey内部已处理）

            return RegisterStateCore(finalStateKey, autoIntKey, state, layer);
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
                StateMachineDebugSettings.Instance.LogError("StateSharedData为空");
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
            var layerType = sharedData.basicConfig.layerType;

            // 注册
            bool registered;
            if (customStringKey != null || customIntKey.HasValue)
            {
                registered = RegisterStateCore(finalStringKey, finalIntKey, state, layerType);
                if (!registered && !allowOverride)
                {
                    registered = RegisterState(state, layerType, allowOverride);
                }
            }
            else
            {
                registered = RegisterState(state, layerType, allowOverride);
            }

            if (registered)
            {
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"✓ 注册SharedData状态: [{layerType}] {state.strKey} (ID:{state.intKey})");
            }

            return registered;
        }

        /// <summary>
        /// 注册新状态（Int键）
        /// </summary>
        public bool RegisterState(int stateKey, StateBase state, StateLayerType layer = StateLayerType.NotClear)
        {
            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogError($"状态实例不能为空: {stateKey}");
                return false;
            }

            layer = ResolveLayerForState(state, layer);
            if (intToStateMap.ContainsKey(stateKey))
            {
                StateMachineDebugSettings.Instance.LogWarning($"状态ID {stateKey} 已存在，跳过注册");
                return false;
            }

            // 自动生成StringKey（从SharedData获取或自动生成）
            string autoStrKey = GenerateUniqueStringKey(state);
            if (stringToStateMap.ContainsKey(autoStrKey))
            {
                StateMachineDebugSettings.Instance.LogWarning($"自动生成的StringKey {autoStrKey} 已存在，跳过注册");
                return false;
            }

            return RegisterStateCore(autoStrKey, stateKey, state, layer);
        }

        /// <summary>
        /// 同时注册String和Int键
        /// </summary>
        public bool RegisterState(string stringKey, int intKey, StateBase state, StateLayerType layer = StateLayerType.NotClear)
        {
            if (string.IsNullOrEmpty(stringKey))
            {
                StateMachineDebugSettings.Instance.LogError("状态键不能为空");
                return false;
            }

            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogError($"状态实例不能为空: {stringKey}");
                return false;
            }

            layer = ResolveLayerForState(state, layer);

            if (stringToStateMap.ContainsKey(stringKey))
            {
                StateMachineDebugSettings.Instance.LogWarning($"状态 {stringKey} 已存在，跳过注册");
                return false;
            }

            if (intToStateMap.ContainsKey(intKey))
            {
                StateMachineDebugSettings.Instance.LogWarning($"状态ID {intKey} 已存在，跳过注册");
                return false;
            }

            return RegisterStateCore(stringKey, intKey, state, layer);
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
            // 约定：注册期传入的 state 必须具备完整 sharedData/basicConfig
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.stateSharedData == null) throw new InvalidOperationException("State.stateSharedData 不能为空（注册期不应出现空共享数据）");
            if (state.stateSharedData.basicConfig == null) throw new InvalidOperationException("State.stateSharedData.basicConfig 不能为空（注册期不应出现空基础配置）");
#endif

            var sharedData = state.stateSharedData;
            var basicConfig = sharedData.basicConfig;

            // 优先从SharedData.basicConfig.stateId获取
            int configId = basicConfig.stateId;

            // 如果配置ID为-1，表示需要自动分配
            if (configId == -1)
            {
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"状态'{basicConfig.stateName}' ID=-1，触发自动分配");
            }
            else if (configId > 0 && !intToStateMap.ContainsKey(configId))
            {
                return configId;
            }
            else if (configId > 0 && intToStateMap.ContainsKey(configId))
            {
                StateMachineDebugSettings.Instance.LogWarning(
                    $"⚠️ IntKey冲突! ID={configId} 已被'{intToStateMap[configId].strKey}'占用");
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.stateSharedData == null) throw new InvalidOperationException("State.stateSharedData 不能为空（注册期不应出现空共享数据）");
            if (state.stateSharedData.basicConfig == null) throw new InvalidOperationException("State.stateSharedData.basicConfig 不能为空（注册期不应出现空基础配置）");
#endif

            // 优先从SharedData.basicConfig.stateName获取
            string configName = state.stateSharedData.basicConfig.stateName;
            if (!string.IsNullOrEmpty(configName) && !stringToStateMap.ContainsKey(configName))
            {
                return configName;
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
        private void CheckAndSetFallbackState(StateBase state, StateLayerType layerType)
        {
            // 约定：调用方（RegisterStateCore）已保证 state/sharedData/basicConfig 非空
            var sharedData = state.stateSharedData;
            var basicConfig = sharedData.basicConfig;

            // 检查是否可以作为Fallback状态
            if (basicConfig.canBeFeedback)
            {
                // 获取Fallback支持标记
                var fallbackFlag = basicConfig.stateSupportFlag;

                // 获取目标层级运行时
                var layerRuntime = GetLayerByType(layerType);
                if (layerRuntime != null)
                {
                    layerRuntime.SetFallBack(state.intKey, fallbackFlag);
                    StateMachineDebugSettings.Instance.LogFallback(
                        $"[FallBack-Register] ✓ [{layerType}] Flag={fallbackFlag} <- State '{state.strKey}' (ID:{state.intKey})");
                }
            }
        }

        /// <summary>
        /// 注册状态核心逻辑（私有，供三个RegisterState重载调用）
        /// </summary>
        private bool RegisterStateCore(string stringKey, int intKey, StateBase state, StateLayerType layer)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.stateSharedData == null) throw new InvalidOperationException("RegisterStateCore: stateSharedData 不能为空");
            if (state.stateSharedData.basicConfig == null) throw new InvalidOperationException("RegisterStateCore: basicConfig 不能为空");
#else
            if (state == null || state.stateSharedData == null || state.stateSharedData.basicConfig == null) return false;
#endif

            var sharedData = state.stateSharedData;

            // 同时注册到两个字典
            stringToStateMap[stringKey] = state;
            intToStateMap[intKey] = state;
            state.strKey = stringKey;
            state.intKey = intKey;
            state.host = this;
            stateLayerMap[state] = layer;

            // ★ 低频路径缓存：Initialize/Update 热路径依赖这些缓存字段（减少每帧判空与链式取值）
            state.InternalRefreshSharedDataCache();
            if (!_registeredStatesList.Contains(state))
            {
                _registeredStatesList.Add(state);
            }

            // 检查并设置Fallback状态
            CheckAndSetFallbackState(state, layer);

            // 如果状态有动画，初始化Calculator（享元数据预计算）
            if (sharedData.hasAnimation)
            {
                var animationConfig = sharedData.animationConfig;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (animationConfig == null) throw new InvalidOperationException($"RegisterStateCore: hasAnimation=true 但 animationConfig 为空 | State={stringKey}");
#endif
                // ★ 优先使用 State 缓存的“已解析 Calculator”（支持外部覆盖，不污染 sharedData）
                var calculator = state._calculatorCached;
                if (calculator == null && animationConfig != null)
                {
                    calculator = animationConfig.calculator;
                }

                if (calculator != null)
                {
                    try
                    {
                        calculator.InitializeCalculator();
                        StateMachineDebugSettings.Instance.LogRuntimeInit(
                            $"✓ Calculator初始化: {stringKey} - {calculator.GetType().Name}");
                    }
                    catch (System.Exception e)
                    {
                        StateMachineDebugSettings.Instance.LogError($"Calculator初始化失败: {stringKey}\n{e}");
                    }
                }
            }

            MarkDirty(StateDirtyReason.RuntimeChanged);

            if (isInitialized)
            {
                InitializeState(state);
            }

#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsRuntimeInitEnabled)
                {
                    dbg.LogRuntimeInit($"注册状态: {stringKey} (IntKey:{intKey}, Layer:{layer})");
                }
            }
#endif
            return true;
        }

        /// <summary>
        /// 注销状态核心逻辑（私有，供UnregisterState重载调用）
        /// </summary>
        private bool UnregisterStateCore(StateBase state)
        {
            if (state == null) return false;
            // 如果状态正在运行，先停用
            if (state.baseStatus == StateBaseStatus.Running)
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
            stateLayerMap.Remove(state);
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
        public void SetFallbackState(StateLayerType layerType, int stateId, StateSupportFlags supportFlag = StateSupportFlags.None)
        {
            var layer = GetLayerByType(layerType);
            if (layer != null)
            {
                layer.SetFallBack(stateId, supportFlag);
            }
        }

        /// <summary>
        /// 获取层级
        /// </summary>
        public StateLayerRuntime GetLayer(StateLayerType layerType)
        {
            return GetLayerByType(layerType);
        }

        /// <summary>
        /// 设置层级权重
        /// </summary>
        public void SetLayerWeight(StateLayerType layerType, float weight)
        {
            var layer = GetLayerByType(layerType);
            if (layer != null)
            {
                layer.weight = Mathf.Clamp01(weight);
                // 更新Playable权重
                layer.UpdateLayerMixer();
            }
        }

        #endregion

        #region 临时动画热拔插（可修改）

        /// <summary>
        /// 临时动画状态跟踪
        /// </summary>
        [NonSerialized]
        private Dictionary<string, StateBase> _temporaryStates = new Dictionary<string, StateBase>();

        /// <summary>
        /// 添加临时动画 - 快速热拔插（自动注册+激活）
        /// </summary>
        /// <param name="tempKey">临时状态键（唯一标识）</param>
        /// <param name="clip">动画Clip</param>
        /// <param name="layer">目标层级</param>
        /// <param name="speed">播放速度</param>
        /// <param name="loopable">是否循环播放（false=播放一次后自动退出，true=持续循环）</param>
        /// <returns>是否添加成功</returns>
        public bool AddTemporaryAnimation(string tempKey, AnimationClip clip, StateLayerType layer = StateLayerType.Main, float speed = 1.0f, bool loopable = false)
        {
            if (string.IsNullOrEmpty(tempKey))
            {
                StateMachineDebugSettings.Instance.LogError("[TempAnim] 临时状态键不能为空");
                return false;
            }

            if (clip == null)
            {
                StateMachineDebugSettings.Instance.LogError("[TempAnim] AnimationClip不能为空");
                return false;
            }

            // 检查是否已存在
            if (_temporaryStates.ContainsKey(tempKey))
            {
                StateMachineDebugSettings.Instance.LogWarning($"[TempAnim] 临时状态 {tempKey} 已存在，先移除旧的");
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
            tempState.stateSharedData.basicConfig.layerType = layer;

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
            if (!RegisterState(tempState.strKey, tempState, layer))
            {
                StateMachineDebugSettings.Instance.LogError($"[TempAnim] 注册临时状态失败: {tempKey}");
                return false;
            }

            // 激活状态
            if (!TryActivateState(tempState, layer))
            {
                StateMachineDebugSettings.Instance.LogError($"[TempAnim] 激活临时状态失败: {tempKey}");
                UnregisterState(tempState.strKey);
                return false;
            }

            // 记录到临时状态集合
            _temporaryStates[tempKey] = tempState;
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[TempAnim] ✓ 添加临时动画: {tempKey} | Clip:{clip.name} | Layer:{layer}");
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
                StateMachineDebugSettings.Instance.LogWarning($"[TempAnim] 临时状态 {tempKey} 不存在");
                return false;
            }

            // 停用并注销状态
            if (tempState.baseStatus == StateBaseStatus.Running)
            {
                TryDeactivateState(tempState.strKey);
            }
            UnregisterState(tempState.strKey);

            // 从临时集合移除
            _temporaryStates.Remove(tempKey);
            StateMachineDebugSettings.Instance.LogStateTransition($"[TempAnim] ✓ 移除临时动画: {tempKey}");
            return true;
        }

        /// <summary>
        /// 一键清除所有临时动画
        /// </summary>
        public void ClearAllTemporaryAnimations()
        {
            if (_temporaryStates.Count == 0)
            {
                StateMachineDebugSettings.Instance.LogStateTransition("[TempAnim] 没有临时动画需要清除");
                return;
            }

            StateMachineDebugSettings.Instance.LogStateTransition($"[TempAnim] 开始清除 {_temporaryStates.Count} 个临时动画");

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
            StateMachineDebugSettings.Instance.LogStateTransition("[TempAnim] ✓ 所有临时动画已清除");
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
            // 预留：通过Entity广播
            if (hostEntity != null)
            {
                // 假设Entity有事件系统
                // hostEntity.BroadcastEvent(eventName, eventParam);
            }

#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[StateMachine] 广播动画事件: {eventName} | State: {state?.strKey} | Param: {eventParam}");
#endif
#endif
        }

        #endregion

        #region 状态激活测试与执行（核心/谨慎改）

        /// <summary>
        /// 测试状态能否激活（不执行）
        /// </summary>
        // 修改点：
        // 1. 完善 CheckStateMergeCompatibility 的判断规则
        // 2. 考虑优先级、代价、通道占用等因素
        // 3. 添加自定义合并策略支持
        public StateActivationResult TestStateActivation(StateBase targetState, StateLayerType layer = StateLayerType.NotClear)
        {
#if STATEMACHINEDEBUG && UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition($"[TestStateActivation] Begin | State={(targetState != null ? targetState.strKey : "<null>")} | Layer={layer} | Running={isRunning} | DirtyVersion={_dirtyVersion}");
                }
            }
#endif

            //状态为空，直接失败
            if (targetState == null)
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                StateMachineDebugSettings.Instance?.LogWarning("[TestStateActivation] Fail: targetState is null");
#endif
                return StateActivationResult.FailureStateIsNull;
            }

            var basicConfig = targetState.stateSharedData.basicConfig;
            //不清晰就是用默认配置的层级
            if (layer == StateLayerType.NotClear)
            {
                layer = basicConfig.layerType;
            }
#if STATEMACHINEDEBUG && UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition($"[TestStateActivation] ResolveLayer -> {layer}");
                }
            }
#endif
            //忽略Ignore则跳过支持标记检查
            if (!basicConfig.ignoreSupportFlag)
            {
                var targetFlag = basicConfig.stateSupportFlag;
                //为NULL则通用支持，跳过即可
                if (targetFlag != StateSupportFlags.None)
                {
                    var supportFlags = currentSupportFlags;
                    if ((supportFlags & targetFlag) == StateSupportFlags.None)
                    {
                        //如果禁用在切换时激活则直接失败了，如果不禁用去看一下是否在禁用切换中
                        if (basicConfig.disableActiveOnSupportFlagSwitching || IsTransitionDisabledFast(supportFlags, targetFlag))
                        {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                            StateMachineDebugSettings.Instance?.LogWarning(
                                $"[TestStateActivation] Fail: SupportFlags not satisfied | Current={supportFlags} Target={targetFlag} DisableOnSwitch={basicConfig.disableActiveOnSupportFlagSwitching}");
#endif
                            return StateActivationResult.FailureSupportFlagsNotSatisfied;
                        }
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        {
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null && dbg.IsStateTransitionEnabled)
                            {
                                dbg.LogStateTransition($"[TestStateActivation] SupportFlags mismatch but not blocked | Current={supportFlags} Target={targetFlag}");
                            }
                        }
#endif
                    }
                }
            }
            #region 缓存与已激活查询
            int layerIndex = (int)layer;
            var cache = GetOrCreateActivationCache(targetState);
            if (cache != null && cache.versions[layerIndex] == _dirtyVersion)
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition($"[TestStateActivation] Cache hit | LayerIndex={layerIndex}");
                    }
                }
#endif
                return cache.results[layerIndex];
            }

            // 检查该状态是否已在运行
            if (targetState.baseStatus == StateBaseStatus.Running)
            {
                var failure = basicConfig.supportReStart
                    ? StateActivationResult.SuccessRestart
                    : StateActivationResult.FailureStateAlreadyRunning;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition($"[TestStateActivation] State already running | Restart={basicConfig.supportReStart}");
                    }
                }
#endif
                if (cache != null)
                {
                    cache.results[layerIndex] = failure;
                    cache.versions[layerIndex] = _dirtyVersion;
                }
                return failure;
            }

            // 获取目标层级
            var targetLayer = GetLayerByType(layer);
            if (targetLayer == null)
            {
                var failure = StateActivationResult.FailurePipelineNotFound;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                StateMachineDebugSettings.Instance?.LogWarning($"[TestStateActivation] Fail: Layer not found | {layer}");
#endif
                if (cache != null)
                {
                    cache.results[layerIndex] = failure;
                    cache.versions[layerIndex] = _dirtyVersion;
                }
                return failure;
            }

            if (!targetLayer.isEnabled)
            {
                var failure = StateActivationResult.FailurePipelineDisabled;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                StateMachineDebugSettings.Instance?.LogWarning($"[TestStateActivation] Fail: Layer disabled | {layer}");
#endif
                if (cache != null)
                {
                    cache.results[layerIndex] = failure;
                    cache.versions[layerIndex] = _dirtyVersion;
                }
                return failure;
            }
            #endregion

            var allRunningStates = GetRunningStatesList();
            //空状态机直接激活即可
            if (allRunningStates.Count == 0)
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition("[TestStateActivation] No running states -> SuccessNoMerge");
                    }
                }
#endif
                var success = StateActivationResult.SuccessNoMerge;
                if (cache != null)
                {
                    cache.results[layerIndex] = success;
                    cache.versions[layerIndex] = _dirtyVersion;
                }
                return success;
            }

            int totalMotionCost = 0;
            int totalAgilityCost = 0;
            int totalTargetCost = 0;

            targetState.EnsureResolvedRuntimeConfig();
            var incomingResolved = targetState.ResolvedConfig;
            if (incomingResolved.enableCostCalculation)
            {
                totalMotionCost += incomingResolved.costForMotion;
                totalAgilityCost += incomingResolved.costForAgility;
                totalTargetCost += incomingResolved.costForTarget;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition(
                            $"[TestStateActivation] IncomingCost | M/A/T={incomingResolved.costForMotion}/{incomingResolved.costForAgility}/{incomingResolved.costForTarget} " +
                            $"TotalNow M/A/T={totalMotionCost}/{totalAgilityCost}/{totalTargetCost}");
                    }
                }
#endif
            }

            bool needsInterrupt = false;
            bool canMerge = false;
            // 检查合并和冲突（运行时复用列表）
            var interruptList = cache.interruptLists[layerIndex];
            interruptList.Clear();
#if UNITY_EDITOR
            var mergeList = cache.mergeLists[layerIndex];
            mergeList.Clear();
#endif       
            #region 遍历合并测试
            foreach (var existingState in allRunningStates)
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition($"[TestStateActivation] MergeCheck: {existingState?.strKey} vs {targetState.strKey}");
                    }
                }
#endif
                var mergeResult = CheckStateMergeCompatibility(existingState, targetState,
                    ref totalMotionCost, ref totalAgilityCost, ref totalTargetCost);

                switch (mergeResult)
                {
                    //已经失败则直接返回
                    case StateMergeResult.MergeFail:
                        var failure = StateActivationResult.FailureMergeConflict;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        StateMachineDebugSettings.Instance?.LogWarning($"[TestStateActivation] Fail: MergeConflict with {existingState?.strKey}");
#endif
                        if (cache != null)
                        {
                            cache.results[layerIndex] = failure;
                            cache.versions[layerIndex] = _dirtyVersion;
                        }
                        return failure;
                    case StateMergeResult.MergeComplete:
                        canMerge = true;
#if UNITY_EDITOR
                        mergeList.Add(existingState);
#endif
                        break;
                    case StateMergeResult.HitAndReplace:
                    case StateMergeResult.TryWeakInterrupt:
                        needsInterrupt = true;
                        interruptList.Add(existingState);
                        break;

                    default:
                        {
                            var failureDefault = StateActivationResult.FailureMergeConflict;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                            StateMachineDebugSettings.Instance?.LogWarning($"[TestStateActivation] Fail: Unexpected merge result with {existingState?.strKey}");
#endif
                            if (cache != null)
                            {
                                cache.results[layerIndex] = failureDefault;
                                cache.versions[layerIndex] = _dirtyVersion;
                            }
                            return failureDefault;
                        }
                }
            }

            StateActivationCode code = StateActivationCode.Success;
            if (needsInterrupt)
            {
                code |= StateActivationCode.HasInterrupt;
            }
            if (canMerge)
            {
                code |= StateActivationCode.HasMerge;
            }
            #endregion

            var defaultSuccess = new StateActivationResult
            {
                code = code,
                failureReason = string.Empty,
                statesToInterrupt = interruptList,
                interruptCount = interruptList.Count
#if UNITY_EDITOR
                ,
                debugMergeStates = mergeList,
                debugMergeCount = mergeList.Count
#endif
            };
#if STATEMACHINEDEBUG && UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition($"[TestStateActivation] Success | Code={code} | Interrupts={interruptList.Count} | Merges={(canMerge ? mergeList.Count : 0)}");
                }
            }
#endif
            if (cache != null)
            {
                cache.results[layerIndex] = defaultSuccess;
                cache.versions[layerIndex] = _dirtyVersion;
            }
            return defaultSuccess;
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
        private StateMergeResult CheckStateMergeCompatibility(StateBase existing, StateBase incoming,
            ref int totalMotionCost, ref int totalAgilityCost, ref int totalTargetCost)
        {
#if STATEMACHINEDEBUG && UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition(
                        $"[MergeCheck] Begin | Existing={existing?.strKey} (ID:{existing?.intKey}) " +
                        $"Incoming={incoming?.strKey} (ID:{incoming?.intKey}) | " +
                        $"CostsBefore: M{totalMotionCost}/A{totalAgilityCost}/T{totalTargetCost}");
                }
            }
#endif
            if (existing == incoming)
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                StateMachineDebugSettings.Instance?.LogWarning("[MergeCheck] Fail: existing == incoming");
#endif
                return StateMergeResult.MergeFail;
            }

            existing.EnsureResolvedRuntimeConfig();
            incoming.EnsureResolvedRuntimeConfig();

            var leftResolved = existing.ResolvedConfig;
            var rightResolved = incoming.ResolvedConfig;

            NormalMergeRule leftRule = leftResolved.asLeftRule ?? existing.stateSharedData?.mergeData?.asLeftRule;
            NormalMergeRule rightRule = rightResolved.asRightRule ?? incoming.stateSharedData?.mergeData?.asRightRule;

#if STATEMACHINEDEBUG && UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition(
                        $"[MergeCheck] ChannelMask L={leftResolved.channelMask} R={rightResolved.channelMask} | " +
                        $"StayLevel L={leftResolved.stayLevel} R={rightResolved.stayLevel} | " +
                        $"CostEnabled={leftResolved.enableCostCalculation} " +
                        $"Cost(M/A/T)={leftResolved.costForMotion}/{leftResolved.costForAgility}/{leftResolved.costForTarget}");
                    dbg.LogStateTransition(
                        $"[MergeCheck] LeftRule: Unconditional={leftRule.enableUnconditionalRule} " +
                        $"HitByLayer={leftRule.hitByLayerOption} Priority={leftRule.EffectialPripority} EqualIsEffectial={leftRule.EqualIsEffectial_}");
                    dbg.LogStateTransition(
                        $"[MergeCheck] RightRule: Unconditional={rightRule.enableUnconditionalRule} " +
                        $"HitByLayer={rightRule.hitByLayerOption} Priority={rightRule.EffectialPripority} EqualIsEffectial={rightRule.EqualIsEffectial_}");
                }
            }
#endif

            #region 优先检查无条件规则
            //左边承接右边的无条件规则

            int leftRuleCount = leftRule.unconditionalRule.Count;
            if (leftRuleCount > 0 && leftRule.enableUnconditionalRule)
            {
                var list = leftRule.unconditionalRule;
                for (int i = 0; i < leftRuleCount; i++)
                {
                    var item = list[i];
                    if (item.stateName != null && item.stateName.Length > 0 && item.stateName == incoming.strKey)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        {
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null && dbg.IsStateTransitionEnabled)
                            {
                                dbg.LogStateTransition($"[MergeCheck] Unconditional(L->R) Hit by Name: {item.stateName} => {item.matchBackType}");
                            }
                        }
#endif
                        return item.matchBackType;
                    }

                    if (item.stateID >= 0 && incoming.intKey == item.stateID)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        {
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null && dbg.IsStateTransitionEnabled)
                            {
                                dbg.LogStateTransition($"[MergeCheck] Unconditional(L->R) Hit by ID: {item.stateID} => {item.matchBackType}");
                            }
                        }
#endif
                        return item.matchBackType;
                    }
                }
            }
            //右边抓取左边的无条件规则

            int rightRuleCount = rightRule.unconditionalRule.Count;
            if (rightRuleCount > 0 && rightRule.enableUnconditionalRule)
            {
                var list = rightRule.unconditionalRule;
                for (int i = 0; i < rightRuleCount; i++)
                {
                    var item = list[i];
                    if (item.stateName != null && item.stateName.Length > 0 && item.stateName == existing.strKey)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        {
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null && dbg.IsStateTransitionEnabled)
                            {
                                dbg.LogStateTransition($"[MergeCheck] Unconditional(R->L) Hit by Name: {item.stateName} => {item.matchBackType}");
                            }
                        }
#endif
                        return item.matchBackType;
                    }

                    if (item.stateID >= 0 && existing.intKey == item.stateID)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        {
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null && dbg.IsStateTransitionEnabled)
                            {
                                dbg.LogStateTransition($"[MergeCheck] Unconditional(R->L) Hit by ID: {item.stateID} => {item.matchBackType}");
                            }
                        }
#endif
                        return item.matchBackType;
                    }
                }
            }
            #endregion

            bool onlyInterruptTest = false;
            // 通道冲突检查
            bool channelOverlap = (leftResolved.channelMask & rightResolved.channelMask) != StateChannelMask.None;


            //发生通道重叠
            if (channelOverlap)
            {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition("[MergeCheck] Channel overlap detected");
                    }
                }
#endif
                if (!leftResolved.enableCostCalculation)
                {
                    onlyInterruptTest = true;
                }
                else
                {
                    const int costLimit = 100;
                    int nextMotionCost = totalMotionCost + leftResolved.costForMotion;
                    int nextAgilityCost = totalAgilityCost + leftResolved.costForAgility;
                    int nextTargetCost = totalTargetCost + leftResolved.costForTarget;

                    bool overMotion = nextMotionCost > costLimit;
                    bool overAgility = nextAgilityCost > costLimit;
                    bool overTarget = nextTargetCost > costLimit;

                    onlyInterruptTest = overMotion || overAgility || overTarget;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                    {
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null && dbg.IsStateTransitionEnabled)
                        {
                            dbg.LogStateTransition(
                                $"[MergeCheck] CostCalc | Limit={costLimit} " +
                                $"Next(M/A/T)={nextMotionCost}/{nextAgilityCost}/{nextTargetCost} " +
                                $"Over(M/A/T)={overMotion}/{overAgility}/{overTarget} " +
                                $"OnlyInterrupt={onlyInterruptTest}");
                        }
                    }
#endif
                }

                if (onlyInterruptTest)
                {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                    {
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null && dbg.IsStateTransitionEnabled)
                        {
                            dbg.LogStateTransition(
                                $"[MergeCheck] Only interrupt test | CurrentCosts M{totalMotionCost}/A{totalAgilityCost}/T{totalTargetCost}");
                        }
                    }
#endif
                    // 仅打断测试逻辑
                    //右边不允许打断，左边不允许被打断，都会提前终止
                    if (rightRule.hitByLayerOption == StateHitByLayerOption.Never)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        StateMachineDebugSettings.Instance?.LogWarning("[MergeCheck] Fail: Right hitByLayer=Never");
#endif
                        return StateMergeResult.MergeFail;
                    }
                    if (leftRule.hitByLayerOption == StateHitByLayerOption.Never)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        StateMachineDebugSettings.Instance?.LogWarning("[MergeCheck] Fail: Left hitByLayer=Never");
#endif
                        return StateMergeResult.MergeFail;
                    }
                    var levelOverlap = leftResolved.stayLevel & rightResolved.stayLevel;
                    if (levelOverlap == StateStayLevel.Rubbish)
                    {
                        if (leftRule.hitByLayerOption == StateHitByLayerOption.SameLevelTest
                            && rightRule.hitByLayerOption == StateHitByLayerOption.SameLevelTest)
                        {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                            StateMachineDebugSettings.Instance?.LogWarning("[MergeCheck] Fail: SameLevelTest + Rubbish overlap");
#endif
                            return StateMergeResult.MergeFail;
                        }
                        else if (rightResolved.stayLevel > leftResolved.stayLevel)
                        {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                            {
                                var dbg = StateMachineDebugSettings.Instance;
                                if (dbg != null && dbg.IsStateTransitionEnabled)
                                {
                                    dbg.LogStateTransition("[MergeCheck] HitAndReplace: Right stayLevel higher");
                                }
                            }
#endif
                            return StateMergeResult.HitAndReplace;
                        }
                    }


                    byte rightPriority = rightRule.EffectialPripority;
                    byte leftPriority = leftRule.EffectialPripority;

                    if (rightRule.EqualIsEffectial_ && leftRule.EqualIsEffectial_)
                    {
                        if (rightPriority < leftPriority)
                        {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                            {
                                var dbg = StateMachineDebugSettings.Instance;
                                if (dbg != null && dbg.IsStateTransitionEnabled)
                                {
                                    dbg.LogStateTransition("[MergeCheck] Fail: Right priority lower (EqualIsEffectial)");
                                }
                            }
#endif
                            return StateMergeResult.MergeFail;
                        }
                        else return StateMergeResult.HitAndReplace;
                    }
                    else if (rightPriority < leftPriority)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        {
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null && dbg.IsStateTransitionEnabled)
                            {
                                dbg.LogStateTransition("[MergeCheck] Fail: Right priority lower");
                            }
                        }
#endif
                        return StateMergeResult.MergeFail;
                    }
                    else if (rightPriority > leftPriority)
                    {
#if STATEMACHINEDEBUG && UNITY_EDITOR
                        {
                            var dbg = StateMachineDebugSettings.Instance;
                            if (dbg != null && dbg.IsStateTransitionEnabled)
                            {
                                dbg.LogStateTransition("[MergeCheck] HitAndReplace: Right priority higher");
                            }
                        }
#endif
                        return StateMergeResult.HitAndReplace;
                    }
                    // 无法决定打断方向，合并失败
#if STATEMACHINEDEBUG && UNITY_EDITOR
                    StateMachineDebugSettings.Instance?.LogWarning("[MergeCheck] Fail: Unable to decide interrupt direction");
#endif
                    return StateMergeResult.MergeFail;
                }

                // 代价符合合并要求，允许合并
                if (leftResolved.enableCostCalculation)
                {
                    // 代价加上
                    totalMotionCost += leftResolved.costForMotion;
                    totalAgilityCost += leftResolved.costForAgility;
                    totalTargetCost += leftResolved.costForTarget;
#if STATEMACHINEDEBUG && UNITY_EDITOR
                    {
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null && dbg.IsStateTransitionEnabled)
                        {
                            dbg.LogStateTransition(
                                $"[MergeCheck] MergeComplete by cost | CostsAfter M{totalMotionCost}/A{totalAgilityCost}/T{totalTargetCost}");
                        }
                    }
#endif
                }
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition("[MergeCheck] MergeComplete (channel overlap allowed)");
                    }
                }
#endif
                return StateMergeResult.MergeComplete;
            }
            else
            {
                // 无通道冲突，允许直接合并
#if STATEMACHINEDEBUG && UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsStateTransitionEnabled)
                    {
                        dbg.LogStateTransition("[MergeCheck] MergeComplete (no channel overlap)");
                    }
                }
#endif
                return StateMergeResult.MergeComplete;
            }
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
                    case StateMergeResult.MergeComplete:
                        return StateMergeResult.MergeComplete;
                    case StateMergeResult.MergeFail:
                        return StateMergeResult.MergeFail;
                    case StateMergeResult.HitAndReplace:
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
        /// 执行状态激活（根据测试结果）
        /// </summary>
        /// TODO: [用户修改] 执行激活逻辑 - 需要验证合并执行流程
        /// 修改点：
        /// 1. 验证 result.decision 的处理逻辑
        /// 2. 确认合并时的权重分配和动画混合
        /// 3. 确认打断和合并的执行顺序
        /// 4. 添加合并失败的回滚机制
        public bool ExecuteStateActivation(StateBase targetState, StateLayerType layer, in StateActivationResult result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (targetState == null) throw new ArgumentNullException(nameof(targetState));
            if (targetState.stateSharedData == null) throw new InvalidOperationException("ExecuteStateActivation: targetState.stateSharedData 不能为空（状态必须先完成注册/初始化）");
            if (targetState.stateSharedData.basicConfig == null) throw new InvalidOperationException("ExecuteStateActivation: targetState.stateSharedData.basicConfig 不能为空（状态必须先完成注册/初始化）");
#else
            if (targetState == null || targetState.stateSharedData == null || targetState.stateSharedData.basicConfig == null) return false;
#endif

#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition("=== 开始执行状态激活 ===");
                    dbg.LogStateTransition($"状态: {targetState.strKey} (ID:{targetState.intKey})");
                    dbg.LogStateTransition($"目标层级: {layer}");
                }
            }
#endif
#endif

            var sharedData = targetState.stateSharedData;
            var basicConfig = sharedData.basicConfig;

            if ((result.code & StateActivationCode.Success) == 0)
            {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    dbg?.LogWarning($"状态激活失败: {result.failureReason}");
                }
#endif
#endif
                return false;
            }

            var layerRuntime = GetLayerByType(layer);
            if (layerRuntime == null)
            {
                StateMachineDebugSettings.Instance.LogError($"获取层级失败: {layer}");
                return false;
            }

            // Restart：若目标状态已运行，先停用再重新进入
            if ((result.code & StateActivationCode.Restart) != 0 && targetState.baseStatus == StateBaseStatus.Running)
            {
                TruelyDeactivateState(targetState, layer);
            }

#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition($"层级状态: Mixer有效={layerRuntime.mixer.IsValid()}, 运行状态数={layerRuntime.runningStates.Count}");
                }
            }
#endif
#endif

            // 执行打断
            if ((result.code & StateActivationCode.HasInterrupt) != 0)
            {
                var interruptStates = result.statesToInterrupt;
                if (interruptStates != null && result.interruptCount > 0)
                {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                    {
                        var dbg = StateMachineDebugSettings.Instance;
                        if (dbg != null && dbg.IsStateTransitionEnabled)
                        {
                            dbg.LogStateTransition($"打断 {interruptStates.Count} 个状态");
                        }
                    }
#endif
#endif
                    for (int i = 0; i < interruptStates.Count; i++)
                    {
                        TruelyDeactivateState(interruptStates[i], layer);
                    }
                }
            }

            // 激活目标状态
            if (basicConfig.resetSupportFlagOnEnter)
            {
                var enterFlag = basicConfig.stateSupportFlag;
                if (enterFlag != StateSupportFlags.None)
                {
                    SetSupportFlags(enterFlag);
                }
            }
            targetState.OnStateEnter();
            runningStates.Add(targetState);
            layerRuntime.runningStates.Add(targetState);
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition("状态已添加到运行集合");
                }
            }
#endif
#endif

            // ★ 关键修复：激活前清理该状态的残留fadeOut/fadeIn数据
            // 快速重入场景：状态A退出(开始fadeOut) → 立即重新进入 → 旧fadeOut残留会把权重压回0
            // 必须在HotPlugStateToPlayable之前调用，因为旧的stateToSlotMap映射还存在会导致HotPlug跳过
            CancelStaleFadeData(targetState, layerRuntime);

            // 如果状态有动画，热插拔到Playable图
            HotPlugStateToPlayable(targetState, layerRuntime);

            // ★ 应用淡入逻辑（如果启用）
            ApplyFadeIn(targetState, layerRuntime);

            // ★ 首帧立即更新：计算器内部权重直接到位（跳过平滑），外部淡入不受影响
            // 解决状态进入后第一帧计算器内部权重全为 0 导致的视觉跳变
            if (targetState.stateSharedData.hasAnimation)
            {
                targetState.ImmediateUpdateAnimationRuntime(stateContext);
            }

            MarkDirty(StateDirtyReason.Enter);

#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition("=== 状态激活完成 ===");
                }
            }
#endif
#endif
            return true;
        }

        /// <summary>
        /// 尝试激活状态（通过键）
        /// </summary>
        public bool TryActivateState(string stateKey, StateLayerType layer = StateLayerType.NotClear)
        {
            var state = GetStateByString(stateKey);
            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogWarning($"状态 {stateKey} 不存在");
                return false;
            }

            return TryActivateState(state, layer);
        }

        /// <summary>
        /// 尝试激活状态（通过Int键）
        /// </summary>
        public bool TryActivateState(int stateKey, StateLayerType layer = StateLayerType.NotClear)
        {
            var state = GetStateByInt(stateKey);
            if (state == null)
            {
                StateMachineDebugSettings.Instance.LogWarning($"状态ID {stateKey} 不存在");
                return false;
            }

            return TryActivateState(state, layer);
        }

        /// <summary>
        /// 尝试激活状态（通过实例 + 指定层级）
        /// </summary>
        public bool TryActivateState(StateBase targetState, StateLayerType layer = StateLayerType.NotClear)
        {
#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsStateTransitionEnabled)
                {
                    dbg.LogStateTransition($"尝试激活状态: {targetState?.strKey} | Layer: {layer}");
                }
            }
#endif
            if (targetState == null) return false;
            layer = ResolveLayerForState(targetState, layer);
            var result = TestStateActivation(targetState, layer);
            return ExecuteStateActivation(targetState, layer, result);
        }

        /// <summary>
        /// 尝试激活状态（通过实例，使用注册时的默认层级）
        /// </summary>
        public bool TryActivateState(StateBase targetState)
        {
            return TryActivateState(targetState, StateLayerType.NotClear);
        }

        /// <summary>
        /// 停用状态（内部方法）
        /// </summary>
        private void TruelyDeactivateState(StateBase state, StateLayerType layer)
        {
            if (state == null) return;

            // ★ 应用淡出逻辑（如果启用）
            var layerRuntime = GetLayerByType(layer);
            if (layerRuntime != null)
            {
                // ★ 可靠性收口：退出时取消未完成的淡入，避免 OnFadeInComplete 在已退出状态上触发。
                if (layerRuntime.fadeInStates != null && layerRuntime.fadeInStates.TryGetValue(state, out var fadeInData))
                {
                    fadeInData.TryAutoPushedToPool();
                    layerRuntime.fadeInStates.Remove(state);
                }

                // 仅触发一次淡出时长判断，后续不做持续判断。
                ApplyFadeOut(state, layerRuntime);
            }

            // 若不启用淡出，则立即卸载
            bool useDirectBlend = state.stateSharedData?.basicConfig?.useDirectBlend == true;
            if (layerRuntime != null && (!state.stateSharedData.enableFadeInOut || state.stateSharedData.fadeOutDuration <= 0f || useDirectBlend))
            {
                HotUnplugStateFromPlayable(state, layerRuntime);

                if (layerRuntime.fadeOutStates.TryGetValue(state, out var fadeData))
                {
                    fadeData.TryAutoPushedToPool();
                    layerRuntime.fadeOutStates.Remove(state);
                }
            }

            // 逻辑层已退出，之后仅保留表现层淡出。
            state.OnStateExit();

            var exitBasicConfig = state.stateSharedData?.basicConfig;
            if (exitBasicConfig != null && exitBasicConfig.removeSupportFlagOnExit)
            {
                var exitFlag = exitBasicConfig.stateSupportFlag;
                if (exitFlag != StateSupportFlags.None && currentSupportFlags == exitFlag)
                {
                    SetSupportFlags(StateSupportFlags.None);
                }
            }

            runningStates.Remove(state);

            if (layerRuntime != null)
            {
                layerRuntime.runningStates.Remove(state);


                // 标记FallBack检查
                layerRuntime.MarkDirty(PipelineDirtyFlags.FallbackCheck);
            }

            MarkDirty(StateDirtyReason.Exit);
        }

        /// <summary>
        /// 尝试停用状态（通过String键）
        /// </summary>
        public bool TryDeactivateState(string stateKey)
        {
            var state = GetStateByString(stateKey);
            if (state == null || state.baseStatus != StateBaseStatus.Running)
            {
                return false;
            }

            if (stateLayerMap.TryGetValue(state, out var layerType))
            {
                TruelyDeactivateState(state, layerType);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 尝试停用状态（通过Int键）
        /// </summary>
        public bool TryDeactivateState(int stateKey)
        {
            var state = GetStateByInt(stateKey);
            if (state == null || state.baseStatus != StateBaseStatus.Running)
            {
                return false;
            }

            if (stateLayerMap.TryGetValue(state, out var layerType))
            {
                TruelyDeactivateState(state, layerType);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 进入验证测试（不执行）
        /// </summary>
        public StateActivationResult TestEnterState(StateBase targetState, StateLayerType layer = StateLayerType.NotClear)
        {
            return TestStateActivation(targetState, layer);
        }

        /// <summary>
        /// 测试进入（验证后执行进入）
        /// </summary>
        public bool TryEnterState(StateBase targetState, StateLayerType layer = StateLayerType.NotClear)
        {
            return TryActivateState(targetState, layer);
        }

        /// <summary>
        /// 强制进入（不做验证）
        /// ★ 修复：原实现缺少HotPlugStateToPlayable和ApplyFadeIn，导致强制进入的状态无动画
        /// </summary>
        public bool ForceEnterState(StateBase targetState, StateLayerType layer = StateLayerType.NotClear)
        {
            if (targetState == null) return false;

            layer = ResolveLayerForState(targetState, layer);
            var layerData = GetLayerByType(layer);
            if (layerData == null)
            {
                return false;
            }

            while (layerData.runningStates.Count > 0)
            {
                var state = layerData.runningStates.Items[0];
                TruelyDeactivateState(state, layer);
            }

            targetState.OnStateEnter();
            runningStates.Add(targetState);
            layerData.runningStates.Add(targetState);

            // ★ 关键修复：激活前清理该状态的残留fadeOut/fadeIn数据
            CancelStaleFadeData(targetState, layerData);

            // ★ 修复：热插拔状态的Playable到图（之前漏掉导致无动画播放）
            HotPlugStateToPlayable(targetState, layerData);

            // ★ 修复：应用淡入逻辑（之前漏掉导致无淡入效果）
            ApplyFadeIn(targetState, layerData);

            // ★ 首帧立即更新：计算器内部权重直接到位（跳过平滑），外部淡入不受影响
            if (targetState.stateSharedData.hasAnimation)
            {
                targetState.ImmediateUpdateAnimationRuntime(stateContext);
            }

            MarkDirty(StateDirtyReason.Enter);
            return true;
        }

        /// <summary>
        /// 退出验证测试（不执行）
        /// </summary>
        public StateExitResult TestExitState(StateBase targetState)
        {
            if (targetState == null)
            {
                return StateExitResult.Failure("目标状态为空", StateLayerType.NotClear);
            }

            if (targetState.baseStatus != StateBaseStatus.Running)
            {
                return StateExitResult.Failure("状态未在运行中", StateLayerType.NotClear);
            }

            if (!stateLayerMap.TryGetValue(targetState, out var layer))
            {
                layer = StateLayerType.NotClear;
            }

            layer = ResolveLayerForState(targetState, layer);

            return StateExitResult.Success(layer);
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

            TruelyDeactivateState(targetState, result.layer);
            return true;
        }

        /// <summary>
        /// 强制退出（不做验证）
        /// </summary>
        public void ForceExitState(StateBase targetState)
        {
            if (targetState == null) return;

            if (stateLayerMap.TryGetValue(targetState, out var layer))
            {
                TruelyDeactivateState(targetState, layer);
            }
        }

        /// <summary>
        /// 停用层级中的所有状态
        /// </summary>
        public void DeactivateLayer(StateLayerType layer)
        {
            var layerData = GetLayerByType(layer);
            if (layerData == null)
            {
                return;
            }

            while (layerData.runningStates.Count > 0)
            {
                var state = layerData.runningStates.Items[0];
                TruelyDeactivateState(state, layer);
            }
        }

        #endregion

        #region Playable动画管理（核心/谨慎改）

        /// <summary>
        /// 热插拔状态到Playable图（运行时动态添加）- 高性能版本
        /// </summary>
        internal void HotPlugStateToPlayable(StateBase state, StateLayerRuntime layer)
        {
#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsAnimationBlendEnabled)
                {
                    dbg.LogAnimationBlend("[HotPlug] === 开始热插拔状态到Playable ===");
                    dbg.LogAnimationBlend($"[HotPlug] 状态: {state?.strKey} | 层级: {layer?.layerType}");
                }
            }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (layer == null) throw new ArgumentNullException(nameof(layer));
            if (state.stateSharedData == null) throw new InvalidOperationException("HotPlugStateToPlayable: state.stateSharedData 不能为空");
#else
            if (state == null || layer == null || state.stateSharedData == null) return;
#endif

            var sharedData = state.stateSharedData;

            // 检查状态是否有动画
            if (!sharedData.hasAnimation)
            {
#if STATEMACHINEDEBUG
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsAnimationBlendEnabled)
                    {
                        dbg.LogAnimationBlend("[HotPlug] 状态无动画，跳过热插拔");
                    }
                }
#endif
                return;
            }

            // 连接/槽位映射变化，需要下一帧更新参考姿态填充与归一化
            layer.MarkDirty(PipelineDirtyFlags.MixerWeights);

            // 检查是否已经插入过
            if (layer.stateToSlotMap.ContainsKey(state))
            {
#if STATEMACHINEDEBUG
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsAnimationBlendEnabled)
                    {
                        dbg.LogAnimationBlend("[HotPlug] 状态已在槽位映射中，跳过");
                    }
                }
#endif
                return; // 已存在，跳过
            }

            // 确保PlayableGraph和层级Mixer有效
            if (!playableGraph.IsValid() || !layer.mixer.IsValid())
            {
                StateMachineDebugSettings.Instance.LogError(
                    $"[HotPlug] 无法插入状态动画：PlayableGraph({playableGraph.IsValid()})或Mixer({layer.mixer.IsValid()})无效 | 层级:{layer.layerType} | 初始化:{isInitialized} | 运行:{isRunning}");
                return;
            }

            // 获取状态的动画配置
            var animConfig = sharedData.animationConfig;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (animConfig == null) throw new InvalidOperationException($"HotPlugStateToPlayable: hasAnimation=true 但 animationConfig 为空 | State={state.strKey}");
#else
            if (animConfig == null)
            {
                StateMachineDebugSettings.Instance.LogWarning($"状态 {state.strKey} 标记了hasAnimation=true，但没有animationConfig");
                return;
            }
#endif

            // 创建Playable节点
            var statePlayable = CreateStatePlayable(state, animConfig, layer);
            if (!statePlayable.IsValid())
            {
                StateMachineDebugSettings.Instance.LogWarning($"无法为状态 {state.strKey} 创建有效的Playable节点");
                return;
            }

            int inputIndex;

            // 优先从空闲槽位池获取
            if (layer.freeSlots.Count > 0)
            {
                inputIndex = layer.freeSlots.Pop();

                // ★ 保护：slot 0 是参考姿态保留位，不可分配给状态
                if (inputIndex == 0 && layer.hasReferencePose)
                {
                    // 放弃slot 0，重新分配
                    int currentCount = layer.mixer.GetInputCount();
                    inputIndex = currentCount;
                    layer.mixer.SetInputCount(inputIndex + 1);
                }
                // 断开旧连接（如果有）
                else if (layer.mixer.GetInput(inputIndex).IsValid())
                {
                    playableGraph.Disconnect(layer.mixer, inputIndex);
                }
            }
            else
            {
                // 检查是否达到最大槽位限制
                int currentCount = layer.mixer.GetInputCount();
                if (currentCount >= layer.maxPlayableSlots)
                {
                    StateMachineDebugSettings.Instance.LogWarning($"层级 {layer.layerType} 已达到最大Playable槽位限制 {layer.maxPlayableSlots}，无法添加新状态");
                    statePlayable.Destroy();
                    return;
                }

                // 分配新槽位
                inputIndex = currentCount;
                layer.mixer.SetInputCount(inputIndex + 1);
            }
#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsAnimationBlendEnabled)
                {
                    dbg.LogAnimationBlend($"[HotPlug] 插入状态Playable到Mixer槽位 {inputIndex}");
                }
            }
#endif
            // 连接到层级Mixer
            playableGraph.Connect(statePlayable, 0, layer.mixer, inputIndex);

            // 记录映射
            layer.stateToSlotMap[state] = inputIndex;
            state.BindLayerSlot(layer, inputIndex);
            layer.InternalOnStateConnected(state, inputIndex);
#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsAnimationBlendEnabled)
                {
                    dbg.LogAnimationBlend($"[HotPlug] 状态 {state.strKey} 映射到槽位 {inputIndex}");
                }
            }
#endif

            // 标记Dirty（热插拔）
            layer.MarkDirty(PipelineDirtyFlags.HotPlug);
        }

        /// <summary>
        /// 从Playable图中卸载状态（运行时动态移除）- 高性能版本
        /// </summary>
        internal void HotUnplugStateFromPlayable(StateBase state, StateLayerRuntime layer)
        {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (layer == null) throw new ArgumentNullException(nameof(layer));
            if (state.stateSharedData == null) throw new InvalidOperationException("HotUnplugStateFromPlayable: state.stateSharedData 不能为空");
#else
            if (state == null || layer == null || state.stateSharedData == null) return;
#endif

            // 卸载连接/槽位变化，需要更新参考姿态填充与归一化
            layer.MarkDirty(PipelineDirtyFlags.MixerWeights);

            var sharedData = state.stateSharedData;

            // 只有有动画的状态才需要卸载
            if (!sharedData.hasAnimation)
            {
                return;
            }

            // 查找状态对应的槽位
            if (!layer.stateToSlotMap.TryGetValue(state, out int slotIndex))
            {
                return; // 未找到，可能未插入过
            }

            // 确保Mixer有效
            if (!layer.mixer.IsValid())
            {
                return;
            }

            // 断开连接
            var inputPlayable = layer.mixer.GetInput(slotIndex);
            if (inputPlayable.IsValid())
            {
                playableGraph.Disconnect(layer.mixer, slotIndex);
            }

            // 清除权重
            layer.mixer.SetInputWeight(slotIndex, 0f);

            // 移除映射
            layer.stateToSlotMap.Remove(state);
            layer.InternalOnStateDisconnected(state);
            state.ClearLayerSlot();

            // 将槽位回收到池中（★ slot 0 是参考姿态保留位，不回收）
            if (!(slotIndex == 0 && layer.hasReferencePose))
            {
                layer.freeSlots.Push(slotIndex);
            }

            // 标记Dirty（热拔插）
            layer.MarkDirty(PipelineDirtyFlags.HotPlug);

            // 让StateBase销毁自己的Playable资源（包括嵌套的Mixer等）
            state.DestroyPlayable();
        }


        /// <summary>
        /// 为状态创建Playable节点 - 委托给StateBase处理
        /// StateBase会使用其SharedData中的混合计算器生成Playable
        /// </summary>
        protected virtual Playable CreateStatePlayable(StateBase state, StateAnimationConfigData animConfig, StateLayerRuntime layer)
        {
            if (state == null) return Playable.Null;

            // 委托给StateBase创建Playable
            if (state.CreatePlayable(playableGraph, out Playable output))
            {
                var mask = state.stateSharedData?.basicConfig?.avatarMask;
                if (mask != null && output.IsValid() && (layer == null || layer.allowStateMaskOverride || layer.avatarMask == null))
                {
                    var layerMixer = AnimationLayerMixerPlayable.Create(playableGraph, 1);
                    playableGraph.Connect(output, 0, layerMixer, 0);
                    layerMixer.SetInputWeight(0, 1f);
                    layerMixer.SetLayerMaskFromAvatarMask(0, mask);
                    output = layerMixer;
                }

#if STATEMACHINEDEBUG
                {
                    var dbg = StateMachineDebugSettings.Instance;
                    if (dbg != null && dbg.IsAnimationBlendEnabled)
                    {
                        dbg.LogAnimationBlend($"状态 {state.strKey} Playable创建成功 | Valid:{output.IsValid()}");
                    }
                }
#endif
                return output;
            }

#if STATEMACHINEDEBUG
            {
                var dbg = StateMachineDebugSettings.Instance;
                dbg?.LogWarning($"状态 {state.strKey} Playable创建失败");
            }
#endif
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
        /// 检查是否完全空闲（所有层级都没有运行状态）
        /// </summary>
        public bool IsIdle()
        {
            return runningStates.Count == 0;
        }

        /// <summary>
        /// 检查特定层级是否空闲
        /// </summary>
        public bool IsLayerIdle(StateLayerType layerType)
        {
            var layer = GetLayerByType(layerType);
            return layer != null && !layer.HasActiveStates;
        }

        /// <summary>
        /// 获取所有运行中的状态数量
        /// </summary>
        public int GetRunningStateCount()
        {
            return runningStates.Count;
        }

        /// <summary>
        /// 获取特定层级中运行的状态数量
        /// </summary>
        public int GetLayerStateCount(StateLayerType layerType)
        {
            var layer = GetLayerByType(layerType);
            return layer != null ? layer.runningStates.Count : 0;
        }

        /// <summary>
        /// 获取状态当前权重（用于IK/外部系统）
        /// </summary>
        public float GetStateWeight(StateBase state)
        {
            if (state == null) return 0f;
            if (stateLayerMap.TryGetValue(state, out var layerType))
            {
                var layer = GetLayerByType(layerType);
                return layer != null ? layer.GetStateWeight(state) : 0f;
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
                    StateLayerType layerType = (StateLayerType)i;

                    sb.AppendLine($"\n  槽位[{i}] - {layerType}:");
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

                            var layer = GetLayerByType(layerType);
                            if (layer != null)
                            {
                                sb.AppendLine($"    运行状态数: {layer.runningStates.Count}");
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

        #region RuntimePhase控制（可修改）

        public void SetStateRuntimePhase(StateBase state, StateRuntimePhase phase, bool lockPhase = true)
        {
            state?.SetRuntimePhase(phase, lockPhase);
        }

        public void ClearStateRuntimePhaseOverride(StateBase state)
        {
            state?.ClearRuntimePhaseOverride();
        }

        #endregion

        #region StateContext便捷访问（可修改）

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMotionSpeedXZ(float localSpeedX, float localSpeedZ)
        {
            var ctx = stateContext;
            if (ctx == null) return;
            ctx.SpeedX = localSpeedX;
            ctx.SpeedZ = localSpeedZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAvgSpeedXZ(float avgSpeedX, float avgSpeedZ)
        {
            var ctx = stateContext;
            if (ctx == null) return;
            ctx.AvgSpeedX = avgSpeedX;
            ctx.AvgSpeedZ = avgSpeedZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetClimbInput(float horizontal, float vertical)
        {
            var ctx = stateContext;
            if (ctx == null) return;
            ctx.ClimbHorizontal = horizontal;
            ctx.ClimbVertical = vertical;
        }

        /// <summary>
        /// 设置默认枚举Float参数（直接字段路径）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat(StateDefaultFloatParameter parameter, float value)
        {
            stateContext?.SetDefaultFloat(parameter, value);
        }

        /// <summary>
        /// 获取默认枚举Float参数（直接字段路径）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetFloat(StateDefaultFloatParameter parameter, float defaultValue = 0f)
        {
            return stateContext?.GetDefaultFloat(parameter, defaultValue) ?? defaultValue;
        }

        /// <summary>
        /// 设置Float参数 - 用于动画混合（如2D混合的X/Y输入）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat(StateParameter parameter, float value)
        {
            stateContext?.SetFloat(parameter, value);
        }

        /// <summary>
        /// 设置Float参数 - 字符串重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFloat(string paramName, float value)
        {
            stateContext?.SetFloat(paramName, value);
        }

        /// <summary>
        /// 获取Float参数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetFloat(StateParameter parameter, float defaultValue = 0f)
        {
            return stateContext?.GetFloat(parameter, defaultValue) ?? defaultValue;
        }

        /// <summary>
        /// 获取Float参数 - 字符串重载
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetFloat(string paramName, float defaultValue = 0f)
        {
            return stateContext?.GetFloat(paramName, defaultValue) ?? defaultValue;
        }

        #endregion
    }
}
