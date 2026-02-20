using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

// ============================================================================
// 文件：StateBase.Core.cs 220
// 作用：StateBase 的核心部分（Host 绑定、共享数据缓存、生命周期、运行时阶段、ResolvedConfig）。
//
// Public（本文件对外可直接使用的成员，按出现顺序；“先功能、后成员”，便于扫读）：
//
// - 【宿主状态机引用】public StateMachine host
//   用途：状态所属的状态机（绑定后使用）。
// - 【绑定宿主】public virtual void BindHostMachine(StateMachine machine)
//   用途：绑定宿主并刷新共享配置缓存（让 Update 热路径只读缓存字段）。
//
// - 【共享配置】public StateSharedData stateSharedData
//   用途：状态共享配置（注册期写入；运行期通常只读）。
// - 【可变数据】public StateVariableData stateVariableData
//   用途：状态运行期可变数据（由业务/外部系统驱动）。
//
// - 【动画运行时访问】public AnimationCalculatorRuntime AnimationRuntime { get; }
//   用途：只读暴露动画 Runtime（供 IK/MatchTarget 等高级操作使用）。
//
// - 【状态键】public string strKey / public int intKey
//   用途：状态在状态机内的键（用于注册/查找/调试）。
//
// - 【生命周期状态】public StateBaseStatus baseStatus { get; }
//   用途：查询状态当前生命周期（Never/Running/Exited）。
// - 【运行时阶段】public StateRuntimePhase RuntimePhase { get; }
//   用途：查询状态当前阶段（Pre/Wait/Main/Released）。
//
// - 【合成配置容器】public class ResolvedRuntimeConfig
//   用途：承载“阶段覆盖后”的最终配置（成本/合并/优先级/IK 等）。
// - 【合成配置访问】public ResolvedRuntimeConfig ResolvedConfig { get; }
//   用途：获取合成后的配置（内部带脏标记，避免重复合成）。
//
// - 【设置阶段】public void SetRuntimePhase(...) / public void SetRuntimePhaseFromCalculator(...)
//   用途：外部/Calculator 设置阶段（可锁定）。
// - 【清除阶段锁定】public void ClearRuntimePhaseOverride()
//   用途：恢复阶段由内部驱动。
//
// - 【生命周期入口】public void OnStateEnter() / OnStateUpdate() / OnStateExit()
//   用途：由 StateMachine 驱动调用（业务侧通常重写 *Logic 扩展）。
//
// - 【动画事件广播】public static void BroadcastAnimationEvent(...)
//   用途：将动画事件转发给宿主状态机进行广播。
//
// Private/Internal（框架内部实现）：共享配置缓存刷新、运行时阶段切换、ResolvedConfig 合成与脏标记。
// ============================================================================

namespace ES
{
    [Serializable, TypeRegistryItem("状态基类")]
    public partial class StateBase : IPoolableAuto
    {
        #region 基础属性（Host/Runtime）

        [NonSerialized]
        public StateMachine host;

        [NonSerialized]
        private StateLayerRuntime _layerRuntime;

        [NonSerialized]
        private int _pipelineSlotIndex = -1;

        /// <summary>
        /// 初始化状态（绑定宿主StateMachine）
        /// </summary>
        public virtual void BindHostMachine(StateMachine machine)
        {
            host = machine;

            // Initialize 后建立强不变式：把共享配置引用/flags 缓存在字段里，Update 热路径只读字段。
            // 容错：若标记 hasAnimation 但缺失 animationConfig/calculator，则降级为无动画，避免每帧判空与潜在NRE。
            InternalRefreshSharedDataCache();
        }

        #endregion

        #region 共享数据与运行时数据

        [LabelText("共享数据", SdfIconType.Calendar2Date), FoldoutGroup("基础属性"), NonSerialized/*不让自动序列化*/]
        public StateSharedData stateSharedData = null;

        [LabelText("自变化数据", SdfIconType.Calendar3Range), FoldoutGroup("基础属性")]
        public StateVariableData stateVariableData;

        [NonSerialized] internal StateBasicConfig _basicConfigCached;
        [NonSerialized] private StatePhaseConfig _phaseConfigCached;
        [NonSerialized] internal StateAnimationConfigData _animConfigCached;
        [NonSerialized] internal StateAnimationMixCalculator _calculatorCached;
        [NonSerialized] internal bool _hasAnimationCached;
        [NonSerialized] internal bool _hasAnimationMarkerCached;
        [NonSerialized] private bool _needsProgressTrackingCached;
        [NonSerialized] private bool _autoPhaseByTimeCached;
        [NonSerialized] private StateDurationMode _durationModeCached;
        [NonSerialized] private bool _enableIKCached;
        [NonSerialized] private bool _enableMatchTargetCached;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InternalRefreshSharedDataCache()
        {
            var shared = stateSharedData;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (shared == null) throw new InvalidOperationException("StateBase.stateSharedData 不能为空（状态必须先完成注册/初始化）");
            if (shared.basicConfig == null) throw new InvalidOperationException("StateBase.stateSharedData.basicConfig 不能为空（状态必须先完成注册/初始化）");
#endif

            if (shared == null)
            {
                _basicConfigCached = null;
                _phaseConfigCached = null;
                _animConfigCached = null;
                _calculatorCached = null;
                _hasAnimationCached = false;
                _hasAnimationMarkerCached = false;
                _needsProgressTrackingCached = false;
                _autoPhaseByTimeCached = false;
                _durationModeCached = StateDurationMode.Timed;
                _enableIKCached = false;
                _enableMatchTargetCached = false;
                return;
            }

            var basic = shared.basicConfig;
            _basicConfigCached = basic;
            _durationModeCached = basic != null ? basic.durationMode : StateDurationMode.Timed;

            var phase = basic != null ? basic.phaseConfig : null;
            _phaseConfigCached = phase;

            bool needsProgress = basic != null && basic.enableProgressTracking;
            if (phase != null && phase.enablePhase && phase.enableAutoPhaseByTime)
            {
                needsProgress = true;
            }
            _needsProgressTrackingCached = needsProgress;

            var animConfig = shared.animationConfig;
            var sharedCalculator = animConfig != null ? animConfig.calculator : null;
            // ★ 运行时覆盖：如果外部已直接设置 _calculatorCached，则不再被 sharedData 覆盖。
            // 这让“无需修改 StateSharedData，直接替换本 State 的 calculator”成为可能。
            var calculator = _calculatorCached ?? sharedCalculator;

            _animConfigCached = animConfig;
            _calculatorCached = calculator;

            bool hasAnim = shared.hasAnimation && animConfig != null && calculator != null;
            _hasAnimationCached = hasAnim;

            // 保留原始标记（用于进度/阶段等逻辑 gating，保持旧语义）
            _hasAnimationMarkerCached = shared.hasAnimation;

            // ★ AutoPhaseByTime 热路径开关：只在真正启用阶段自动评估时为 true。
            // 目的：不启用阶段的状态，在进度更新时不会额外访问 phaseConfig / EvaluatePhase。
            _autoPhaseByTimeCached = _hasAnimationMarkerCached && phase != null && phase.enablePhase && phase.enableAutoPhaseByTime;

            _enableIKCached = hasAnim && animConfig.enableIK;
            _enableMatchTargetCached = hasAnim && animConfig.enableMatchTarget;

            // IK 权重曲线依赖 normalizedProgress：若启用曲线，即便用户未显式勾选 enableProgressTracking，也需要更新进度。
            if (_enableIKCached && animConfig != null && animConfig.useIKTargetWeightCurve)
            {
                _needsProgressTrackingCached = true;
            }
        }

        /// <summary>
        /// 对外入口：当外部修改了 stateSharedData（尤其是 animationConfig / calculator）后，
        /// 调用此方法让缓存字段立即生效。
        /// </summary>
        public void RefreshSharedDataCache()
        {
            InternalRefreshSharedDataCache();
        }

        /// <summary>
        /// 运行时替换本 State 使用的动画 Calculator（不需要修改 StateSharedData）。
        /// - 一旦设置，后续 InternalRefreshSharedDataCache 不会再用 sharedData 覆盖它（除非你把它设回 null）。
        /// - 典型用途：临时动画/能力注入/共享 calculator 复用。
        ///
        /// 注意：如果已经创建并初始化过 Playable/Runtime，切换 calculator 可能导致 runtime 结构不匹配。
        /// 这种情况下建议 destroyPlayableIfInitialized=true 先销毁旧 runtime。
        /// </summary>
        public void SetAnimationCalculator(
            StateAnimationMixCalculator calculator,
            bool initializeCalculator = false,
            bool destroyPlayableIfInitialized = false)
        {
            if (destroyPlayableIfInitialized && _animationRuntime != null)
            {
                DestroyPlayable();
            }

            _calculatorCached = calculator;

            if (initializeCalculator && calculator != null)
            {
                calculator.InitializeCalculator();
            }

            // 刷新 gating/缓存字段（hasAnim/IK/MatchTarget 等）
            var shared = stateSharedData;
            if (shared != null && shared.basicConfig != null)
            {
                InternalRefreshSharedDataCache();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private StateSharedData GetSharedDataOrNull()
        {
            var shared = stateSharedData;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (shared == null) throw new InvalidOperationException("StateBase.stateSharedData 不能为空（状态必须先完成注册/初始化）");
            if (shared.basicConfig == null) throw new InvalidOperationException("StateBase.stateSharedData.basicConfig 不能为空（状态必须先完成注册/初始化）");
#endif
            return shared;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal string GetStateNameSafe()
        {
            var basic = _basicConfigCached;
            if (basic != null) return basic.stateName;

            var shared = stateSharedData;
            basic = shared != null ? shared.basicConfig : null;
            return basic != null ? basic.stateName : "NULL";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetStateIdSafe()
        {
            var basic = _basicConfigCached;
            if (basic != null) return basic.stateId;

            var shared = stateSharedData;
            basic = shared != null ? shared.basicConfig : null;
            return basic != null ? basic.stateId : -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal StateAnimationConfigData GetAnimConfigCachedOrShared(StateSharedData shared)
        {
            return _animConfigCached ?? (shared != null ? shared.animationConfig : null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal StateAnimationConfigData GetAnimConfigCachedOrSharedOrNull()
        {
            return GetAnimConfigCachedOrShared(GetSharedDataOrNull());
        }

        /// <summary>
        /// 运行时动画数据 - 由Calculator创建并管理，零GC
        /// </summary>
        [NonSerialized]
        private AnimationCalculatorRuntime _animationRuntime;

        /// <summary>
        /// 获取动画Runtime（只读访问，用于外部IK/MatchTarget等高级操作）
        /// </summary>
        public AnimationCalculatorRuntime AnimationRuntime => _animationRuntime;

        #endregion

        #region 标识键（Key）

        public string strKey;
        public int intKey;

        #endregion

        #region 生命周期与运行时阶段

        public StateBaseStatus baseStatus { get; private set; } = StateBaseStatus.Never;

        private StateRuntimePhase _runtimePhase = StateRuntimePhase.Pre;
        public StateRuntimePhase RuntimePhase => _runtimePhase;

#if UNITY_EDITOR
    [ShowInInspector, ReadOnly, FoldoutGroup("运行时阶段"), PropertyOrder(-1)]
        private StateRuntimePhase DebugRuntimePhase => _runtimePhase;

    [ShowInInspector, ReadOnly, FoldoutGroup("运行时阶段"), PropertyOrder(-1)]
        private float DebugMainStartTime
        {
            get
            {
                var shared = stateSharedData;
                var basic = shared != null ? shared.basicConfig : null;
                var phase = basic != null ? basic.phaseConfig : null;
                return phase != null ? phase.mainStartTime : 0f;
            }
        }

        [ShowInInspector, ReadOnly, FoldoutGroup("运行时阶段"), PropertyOrder(-1)]
        private float DebugWaitStartTime
        {
            get
            {
                var shared = stateSharedData;
                var basic = shared != null ? shared.basicConfig : null;
                var phase = basic != null ? basic.phaseConfig : null;
                return phase != null ? phase.waitStartTime : 0f;
            }
        }
#endif

        [NonSerialized]
        private bool _runtimePhaseManual = false;

        public class ResolvedRuntimeConfig
        {
            public byte costForMotion;
            public byte costForAgility;
            public byte costForTarget;
            public bool enableCostCalculation;
            public StateChannelMask channelMask;
            public StateStayLevel stayLevel;
            public NormalMergeRule asLeftRule;
            public NormalMergeRule asRightRule;
            public byte priority;
            public bool ikOverrideEnabled;
            public float ikTargetWeight;
        }

        private ResolvedRuntimeConfig _resolvedRuntimeConfig;
        public ResolvedRuntimeConfig ResolvedConfig => _resolvedRuntimeConfig ?? (_resolvedRuntimeConfig = new ResolvedRuntimeConfig());

        /// <summary>
        /// ResolvedRuntimeConfig 的脏标记（性能关键）：
        /// - true：当前 ResolvedConfig 已过期，需要重新合成（会读取 sharedData/basic/phaseOverride 等）。
        /// - false：当前 ResolvedConfig 与运行时输入一致，无需重复计算。
        /// 
        /// 设计目的：
        /// - 将“昂贵且容易散落”的配置合成逻辑收敛到 <see cref="RefreshResolvedRuntimeConfig"/>，
        ///   其它地方只负责在输入变化时把该标记置为 true。
        /// - 避免每帧/每次访问都做 phaseOverride/merge/cost 等链式判定，从而稳定热路径耗时。
        /// 
        /// 置脏时机（典型）：
        /// - 运行时阶段变化：<see cref="SwitchRuntimePhase"/>。
        /// - 状态进入：<see cref="OnStateEnter"/>（需要重新应用阶段覆盖与 IK 权重）。
        /// - 任何影响 ResolvedConfig 结果的输入变化点（例如：sharedData 变更、关键 runtime 参数变更）也应置脏。
        /// 
        /// 清理时机：
        /// - <see cref="RefreshResolvedRuntimeConfig"/> 完成合成后置为 false。
        /// - 当 sharedData 为 null 时：清空 _resolvedRuntimeConfig，并将该标记置为 false，避免后续重复清空。
        /// </summary>
        private bool _resolvedRuntimeDirty = true;

        public void SetRuntimePhase(StateRuntimePhase phase, bool lockPhase = true)
        {
            SwitchRuntimePhase(phase, lockPhase);
        }

        public void SetRuntimePhaseFromCalculator(StateRuntimePhase phase, bool lockPhase = true)
        {
            SwitchRuntimePhase(phase, lockPhase);
        }

        public void ClearRuntimePhaseOverride()
        {
            _runtimePhaseManual = false;
        }

        private void SwitchRuntimePhase(StateRuntimePhase phase, bool lockPhase)
        {
            if (phase != _runtimePhase)
            {
                var previous = _runtimePhase;
                _runtimePhase = phase;
                _resolvedRuntimeDirty = true;
                RefreshResolvedRuntimeConfig();
            }
            _runtimePhaseManual = lockPhase;
        }

        private void RefreshResolvedRuntimeConfig()
        {
            var shared = stateSharedData;
            if (shared == null)
            {
                _resolvedRuntimeConfig = null;
                _resolvedRuntimeDirty = false;
                return;
            }

            if (!_resolvedRuntimeDirty && _resolvedRuntimeConfig != null)
            {
                return;
            }

            if (_resolvedRuntimeConfig == null)
            {
                _resolvedRuntimeConfig = new ResolvedRuntimeConfig();
            }

            var basic = shared.basicConfig;
            var costData = shared.costData;
            var mergeData = shared.mergeData;
            byte priority = basic != null ? basic.priority : (byte)0;
            bool ikOverrideEnabled = false;
            float ikTargetWeight = _ikDefaultTargetWeight;

            var phaseConfig = basic != null ? basic.phaseConfig : null;
            if (phaseConfig != null && phaseConfig.enablePhase)
            {
                StatePhaseOverrideConfig overrideConfig = null;
                switch (_runtimePhase)
                {
                    case StateRuntimePhase.Pre:
                        overrideConfig = phaseConfig.preOverride;
                        break;
                    case StateRuntimePhase.Wait:
                        overrideConfig = phaseConfig.waitOverride;
                        break;
                    case StateRuntimePhase.Released:
                        overrideConfig = phaseConfig.releasedOverride;
                        break;
                }

                if (!phaseConfig.mutePhaseOverride && overrideConfig != null && overrideConfig.enable)
                {
                    if (overrideConfig.overrideCost && overrideConfig.costData != null)
                        costData = overrideConfig.costData;
                    if (overrideConfig.overrideMerge && overrideConfig.mergeData != null)
                        mergeData = overrideConfig.mergeData;
                    if (overrideConfig.overridePriority)
                        priority = overrideConfig.priority;
                    if (overrideConfig.overrideIK)
                    {
                        ikOverrideEnabled = true;
                        ikTargetWeight = overrideConfig.ikTargetWeight;
                    }
                }

                if (_runtimePhase == StateRuntimePhase.Main && phaseConfig.overrideMainIK)
                {
                    ikOverrideEnabled = true;
                    ikTargetWeight = phaseConfig.mainIKTargetWeight;
                }
            }

            _resolvedRuntimeConfig.costForMotion = costData != null ? costData.costForMotion : (byte)0;
            _resolvedRuntimeConfig.costForAgility = costData != null ? costData.costForAgility : (byte)0;
            _resolvedRuntimeConfig.costForTarget = costData != null ? costData.costForTarget : (byte)0;
            _resolvedRuntimeConfig.enableCostCalculation = costData != null && costData.enableCostCalculation;

            if (mergeData != null)
            {
                _resolvedRuntimeConfig.channelMask = mergeData.stateChannelMask;
                _resolvedRuntimeConfig.stayLevel = mergeData.stayLevel;
                _resolvedRuntimeConfig.asLeftRule = mergeData.asLeftRule;
                _resolvedRuntimeConfig.asRightRule = mergeData.asRightRule;
            }
            else
            {
                _resolvedRuntimeConfig.channelMask = default;
                _resolvedRuntimeConfig.stayLevel = default;
                _resolvedRuntimeConfig.asLeftRule = null;
                _resolvedRuntimeConfig.asRightRule = null;
            }

            _resolvedRuntimeConfig.priority = priority;
            _resolvedRuntimeConfig.ikOverrideEnabled = ikOverrideEnabled;
            _resolvedRuntimeConfig.ikTargetWeight = ikTargetWeight;

            if (_animationRuntime != null && _animationRuntime.ik.enabled)
            {
                SetIKTargetWeight(ikTargetWeight);
            }

            _resolvedRuntimeDirty = false;
        }

        internal void EnsureResolvedRuntimeConfig()
        {
            RefreshResolvedRuntimeConfig();
        }

        public void OnStateEnter()
        {
            if (baseStatus == StateBaseStatus.Running) return;
            baseStatus = StateBaseStatus.Running;
            SwitchRuntimePhase(StateRuntimePhase.Pre, false);
            activationTime = Time.time; // 记录激活时间

            SetPlayableWeight(1f);

            // 重置运行时数据
            hasEnterTime = 0f;
            normalizedProgress = 0f;
            totalProgress = 0f;
            loopCount = 0;
            _lastNormalizedProgress = 0f; // 重置事件检测
            _shouldAutoExitFromAnimation = false; // 重置动画完毕标志

            // 重置动画事件触发标记
            ResetAnimationEventTriggers();

            // ★ 自动应用IK/MatchTarget配置（从Inspector配置到Runtime）
            // 约定：只有有动画Runtime时才会走配置应用，避免在内部函数里到处写 runtime 判空。
            if (_animationRuntime != null)
            {
                // 防御性：避免复用 runtime 时遗留的“序列已完成”标志导致 UntilAnimationEnd 立刻自动退出。
                _animationRuntime.sequenceCompleted = false;
                ApplyIKConfigOnEnter(_animationRuntime);
                ApplyMatchTargetConfigOnEnter(_animationRuntime);
            }

            if (_animationRuntime != null && _animationRuntime.ik.enabled)
            {
                _ikDefaultTargetWeight = _animationRuntime.ik.targetWeight;
            }

            _resolvedRuntimeDirty = true;
            RefreshResolvedRuntimeConfig();

            OnStateEnterLogic();
        }

        public void OnStateUpdate()
        {
            OnStateUpdateLogic();
        }

        public void OnStateExit()
        {
            if (baseStatus != StateBaseStatus.Running) return;
            baseStatus = StateBaseStatus.Exited;
            SwitchRuntimePhase(StateRuntimePhase.Released, true);

            // ★ 状态退出时自动清理IK
            var animConfig = GetAnimConfigCachedOrSharedOrNull();
            if (animConfig != null && animConfig.disableIKOnExit)
            {
                DisableIK();
            }
            // ★ 状态退出时自动清理MatchTarget
            if (_matchTargetActive)
            {
                CancelMatchTarget();
            }

            //这里需要编写释放逻辑
            OnStateExitLogic();
        }

        #endregion

        #region 应用层重写入口（业务扩展）

        protected virtual void OnStateEnterLogic()
        {
            //默认的进入执行逻辑
        }
        protected virtual void OnStateUpdateLogic()
        {
            //默认的更新执行逻辑
        }
        protected virtual void OnStateExitLogic()
        {
            //默认的退出执行逻辑
        }

        #endregion
    }

  
}
