using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    [Serializable, TypeRegistryItem("ES状态机")]
    public partial class StateMachine
    {
        [NonSerialized] private Entity hostEntity;
        public Entity HostEntity => hostEntity;

        [TabGroup("SM_View", "⚙ 配置", Order = 0, TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾蓝\")")]
        [BoxGroup("SM_View/⚙ 配置/基础设置", ShowLabel = false)]
        [LabelText("状态机键"), ShowInInspector]
        public string stateMachineKey;

        [TabGroup("SM_View", "⚙ 配置")]
        [BoxGroup("SM_View/⚙ 配置/基础设置", ShowLabel = false)]
        [LabelText("状态机配置"), ShowInInspector]
        [SerializeField]
        private StateMachineConfig config;
        public StateMachineConfig Config { get => config; set => config = value; }

        [TabGroup("SM_View", "▶ 运行时", Order = 1, TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾绿\")")]
        [LabelText("状态实时上下文"), ShowInInspector, ReadOnly]
        [NonSerialized]
        private StateMachineContext stateContext;

        [TabGroup("SM_View", "⚙ 配置")]
        [BoxGroup("SM_View/⚙ 配置/基础设置", ShowLabel = false), LabelText("默认状态键"), ValueDropdown("GetAllStateKeys")]
        public string defaultStateKey;

        [TabGroup("SM_View", "⚙ 配置")]
        [BoxGroup("SM_View/⚙ 配置/层级遮罩", ShowLabel = false), LabelText("覆盖上半身遮罩配置"), AssetsOnly]
        public AvatarMask upperBodyMask;

        [BoxGroup("SM_View/⚙ 配置/层级遮罩", ShowLabel = false), LabelText("覆盖下半身遮罩配置"), AssetsOnly]
        public AvatarMask lowerBodyMask;

        [BoxGroup("SM_View/⚙ 配置/层级遮罩", ShowLabel = false), LabelText("参考姿态动画剪辑(防下陷)"), AssetsOnly, Tooltip("防止空状态时角色下陷到地面以下。建议设置为 1 帧的站立待机动画剪辑。")]
        public AnimationClip referencePoseClip;

        [TabGroup("SM_View", "▶ 运行时")]
        [ShowInInspector, ReadOnly, LabelText("运行状态")]
        public bool isRunning { get; set; }

        [NonSerialized]
        public bool isInitialized = false;

        public StateMachine()
        {
        }

        public IEnumerable<string> GetAllStateKeys()
        {
            return stringToStateMap.Keys;
        }

        [TabGroup("SM_View", "▶ 运行时")]
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

        [NonSerialized]
        public StateGeneralFinalIKDriverPose stateGeneralFinalIKDriverPose;

        [NonSerialized]
        public string stateGeneralFinalIKContributionSummary = "未更新";

        public delegate void StateGeneralFinalIKDriverPosePostProcessDelegate(StateMachine machine, ref StateGeneralFinalIKDriverPose pose, float deltaTime);

        public event StateGeneralFinalIKDriverPosePostProcessDelegate OnStateGeneralFinalIKDriverPosePostProcess;

        [TabGroup("SM_View", "▶ 运行时")]
        [ShowInInspector, ReadOnly, LabelText("支持标记")]
        [NonSerialized]
        public StateSupportFlags currentSupportFlags = StateSupportFlags.Grounded;

        private const StateSupportFlags LocomotionMask = StateSupportFlags.Grounded | StateSupportFlags.Swimming | StateSupportFlags.Flying | StateSupportFlags.Mounted | StateSupportFlags.Climbing;

        [NonSerialized]
        private Dictionary<StateSupportFlags, uint> _disableTransitionMasks;

        [ShowInInspector, TabGroup("SM_View", "📋 状态字典", Order = 2, TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾紫\")"), LabelText("String映射")]
        [SerializeField, SerializeReference]
        private Dictionary<string, StateBase> stringToStateMap = new Dictionary<string, StateBase>();

        [ShowInInspector, TabGroup("SM_View", "📋 状态字典"), LabelText("Int映射")]
        [SerializeField, SerializeReference]
        private Dictionary<int, StateBase> intToStateMap = new Dictionary<int, StateBase>();

        [ShowInInspector, TabGroup("SM_View", "📋 状态字典"), LabelText("状态层级映射")]
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

        public struct StateExitActivationOptions
        {
            public StateLayerType toLayer;
            public bool oneShot;
            public bool forceEnter;
            public bool fallbackToForceEnterOnFail;
            public bool suppressFromFadeOutOverlap;
            public StateBase ignoreInteractionWithState;
            public Func<StateMachineContext, bool> condition;
        }

        public readonly struct StateExitActivationHandle
        {
            public readonly StateBase fromState;
            public readonly bool oneShot;
            public bool IsValid => fromState != null;
            public StateExitActivationHandle(StateBase fromState, bool oneShot)
            {
                this.fromState = fromState;
                this.oneShot = oneShot;
            }
        }

        [NonSerialized]
        private readonly Dictionary<StateBase, StateActivationCache> _activationCache = new Dictionary<StateBase, StateActivationCache>(64);

        private sealed class StateActivationCache
        {
            public int[] versions;
            public StateActivationResult[] results;
            public List<StateBase>[] interruptLists;
#if UNITY_EDITOR
            public List<StateBase>[] mergeLists;
#endif
        }

        [NonSerialized]
        private readonly List<StateBase> _tmpInterruptStates = new List<StateBase>(8);

        [NonSerialized]
        private readonly List<StateBase> _tmpMergeStates = new List<StateBase>(8);

        [NonSerialized]
        private readonly List<StateBase> _statesToDeactivateCache = new List<StateBase>(16);

        [NonSerialized]
        private readonly List<StateBase> _registeredStatesList = new List<StateBase>(256);

        [NonSerialized]
        private readonly List<StateBase> _runningStatesSnapshot = new List<StateBase>(32);

        [NonSerialized]
        private readonly List<string> _temporaryKeysCache = new List<string>(16);

        [NonSerialized]
        private readonly System.Text.StringBuilder _continuousStatsBuilder = new System.Text.StringBuilder(256);

        [NonSerialized]
        private readonly System.Text.StringBuilder _ikContributionBuilder = new System.Text.StringBuilder(1024);

        public enum ActivationEventKind : byte
        {
            Begin = 1,
            Interrupt = 2,
            Success = 3,
            Rollback = 4,
            Failure = 5,
        }

        public enum ActivationFailureKind : byte
        {
            None = 0,
            Validation = 1,
            LayerNotFound = 2,
            Exception = 3,
        }

        [Serializable]
        public struct ActivationEventRecord
        {
            public int txId;
            public int frame;
            public float time;
            public int stateId;
            public StateLayerType requestedLayer;
            public StateLayerType resolvedLayer;
            public StateActivationCode code;
            public ActivationEventKind kind;
            public ActivationFailureKind failureKind;
            public byte interruptCount;
            public ushort runningBefore;
            public ushort runningAfter;
            public ushort layerRunningBefore;
            public ushort layerRunningAfter;
        }

        private const int ActivationEventBufferSize = 128;

        /// <summary>
        /// 激活事件环形缓冲区，容量固定为 <see cref="ActivationEventBufferSize"/> (128)。
        /// 事件写满后，最旧的事件会被覆盖。
        /// </summary>
        [NonSerialized]
        private readonly ActivationEventRecord[] _activationEventRing = new ActivationEventRecord[ActivationEventBufferSize];

        [NonSerialized]
        private int _activationEventWriteIndex;

        [NonSerialized]
        private int _activationEventCount;

        [NonSerialized]
        private int _activationTxIdCounter;

        public int ActivationEventCount => _activationEventCount;

        [NonSerialized]
        private int _dirtyVersion = 0;

        [NonSerialized]
        private StateDirtyReason _lastDirtyReason = StateDirtyReason.Unknown;

        public enum StateDirtyReason
        {
            Unknown = 0,
            Enter = 1,
            Exit = 2,
            Release = 3,
            RuntimeChanged = 5
        }

        [NonSerialized]
        protected bool isDirty = false;

        [NonSerialized]
        private int _nextAutoIntId = 10000;

        [NonSerialized]
        private int _nextAutoStringIdSuffix = 1;

        [NonSerialized]
        private StateChannelMask _cachedChannelMask = StateChannelMask.None;

        [NonSerialized]
        private int _cachedChannelMaskVersion = -1;

        [TabGroup("SM_View", "▶ 运行时")]
        [NonSerialized]
        protected StateLayerRuntime baseLayer;

        [NonSerialized]
        protected StateLayerRuntime mainLayer;

        [NonSerialized]
        protected StateLayerRuntime buffLayer;

        [NonSerialized]
        protected StateLayerRuntime upperBodyLayer;

        [NonSerialized]
        protected StateLayerRuntime lowerBodyLayer;

        /// <summary>
        /// 缓存的层级数组，用于零分配的每帧统一遍历，
        /// 替代原来重复 5 次的代码块，提高指令缓存命中率。
        /// </summary>
        [NonSerialized] private StateLayerRuntime[] _layerArray;

        [TabGroup("SM_View", "▶ 运行时")]
        [LabelText("Selected Layer")]
        [EnumToggleButtons]
        public StateLayerType inspectorSelectedLayer = StateLayerType.Base;

        [TabGroup("SM_View", "▶ 运行时")]
        [ShowInInspector]
        [ShowIf(nameof(inspectorSelectedLayer), StateLayerType.Base)]
        [HideLabel]
        public StateLayerRuntime BaseLayer => baseLayer;

        [TabGroup("SM_View", "▶ 运行时")]
        [ShowInInspector]
        [ShowIf(nameof(inspectorSelectedLayer), StateLayerType.Main)]
        [HideLabel]
        public StateLayerRuntime MainLayer => mainLayer;

        [TabGroup("SM_View", "▶ 运行时")]
        [ShowInInspector]
        [ShowIf(nameof(inspectorSelectedLayer), StateLayerType.Buff)]
        [HideLabel]
        public StateLayerRuntime BuffLayer => buffLayer;

        [TabGroup("SM_View", "▶ 运行时")]
        [ShowIf(nameof(inspectorSelectedLayer), StateLayerType.UpperBody)]
        [HideLabel]
        [ShowInInspector]
        public StateLayerRuntime UpperBodyLayer => upperBodyLayer;

        [TabGroup("SM_View", "▶ 运行时")]
        [ShowInInspector]
        [ShowIf(nameof(inspectorSelectedLayer), StateLayerType.LowerBody)]
        [HideLabel]
        public StateLayerRuntime LowerBodyLayer => lowerBodyLayer;

        public IEnumerable<StateLayerRuntime> LayerRuntimes
        {
            get { return GetAllLayers(); }
        }

        private StateLayerRuntime GetLayerByType(StateLayerType layerType)
        {
            switch (layerType)
            {
                case StateLayerType.Base: return baseLayer;
                case StateLayerType.Main: return mainLayer;
                case StateLayerType.Buff: return buffLayer;
                case StateLayerType.UpperBody: return upperBodyLayer;
                case StateLayerType.LowerBody: return lowerBodyLayer;
                default: return null;
            }
        }

        private IEnumerable<StateLayerRuntime> GetAllLayers()
        {
            yield return baseLayer;
            yield return mainLayer;
            yield return buffLayer;
            yield return upperBodyLayer;
            yield return lowerBodyLayer;
        }

        [NonSerialized]
        public PlayableGraph playableGraph;

        [NonSerialized]
        protected Animator boundAnimator;

        [NonSerialized]
        protected AnimationPlayableOutput animationOutput;

        [NonSerialized]
        internal AnimationLayerMixerPlayable rootMixer;

        [NonSerialized]
        protected bool ownsPlayableGraph = false;

        [NonSerialized]
        internal Transform[] _sharedBoneTransforms;

        public bool IsPlayableGraphValid => playableGraph.IsValid();

        public bool IsPlayableGraphPlaying => playableGraph.IsValid() && playableGraph.IsPlaying();

        public Animator BoundAnimator => boundAnimator;

        [NonSerialized]
        protected Dictionary<string, StateBase> transitionCache = new Dictionary<string, StateBase>();

        private static readonly float[] MixerBiasScoreFactors =
        {
    0.50f,  // Background 
    0.70f,  // Low     
    1.00f,  // Normal
    1.50f,  // High     
    2.50f,  // Critical  
};

        [NonSerialized]
        private bool _isUpdatingMixerInputWeights = false;

        [NonSerialized]
        private Dictionary<string, StateBase> _temporaryStates = new Dictionary<string, StateBase>();

        private List<StateBase> GetRunningStatesList()
        {
            return runningStates.Items;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushActivationEvent(
            int txId,
            StateBase state,
            StateLayerType requestedLayer,
            StateLayerType resolvedLayer,
            StateActivationCode code,
            ActivationEventKind kind,
            ActivationFailureKind failureKind,
            int interruptCount,
            int runningBefore,
            int runningAfter,
            int layerRunningBefore,
            int layerRunningAfter)
        {
            int write = _activationEventWriteIndex;
            _activationEventRing[write] = new ActivationEventRecord
            {
                txId = txId,
                frame = Time.frameCount,
                time = Time.time,
                stateId = state != null ? state.intKey : -1,
                requestedLayer = requestedLayer,
                resolvedLayer = resolvedLayer,
                code = code,
                kind = kind,
                failureKind = failureKind,
                interruptCount = (byte)Mathf.Clamp(interruptCount, 0, byte.MaxValue),
                runningBefore = (ushort)Mathf.Clamp(runningBefore, 0, ushort.MaxValue),
                runningAfter = (ushort)Mathf.Clamp(runningAfter, 0, ushort.MaxValue),
                layerRunningBefore = (ushort)Mathf.Clamp(layerRunningBefore, 0, ushort.MaxValue),
                layerRunningAfter = (ushort)Mathf.Clamp(layerRunningAfter, 0, ushort.MaxValue),
            };
            write++;
            if (write >= ActivationEventBufferSize)
            {
                write = 0;
            }
            _activationEventWriteIndex = write;
            if (_activationEventCount < ActivationEventBufferSize)
            {
                _activationEventCount++;
            }
        }
        /// <summary>
        /// 获取最近的第 <paramref name="latestOffset"/> 个激活事件（0=最新，1=上一个，…127=最旧）。
        /// 如果偏移量超过当前记录数（最多128），返回 false。
        /// 注意：该缓冲区只保留最近 128 条事件，更早的事件已不可访问。
        public bool TryGetActivationEventFromLatest(int latestOffset, out ActivationEventRecord record)
        {
            if ((uint)latestOffset >= (uint)_activationEventCount)
            {
                record = default;
                return false;
            }
            int index = _activationEventWriteIndex - 1 - latestOffset;
            if (index < 0)
            {
                index += ActivationEventBufferSize;
            }
            record = _activationEventRing[index];
            return true;
        }
    }
}