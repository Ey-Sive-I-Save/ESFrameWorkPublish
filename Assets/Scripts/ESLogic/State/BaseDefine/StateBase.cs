using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace ES
{

    [Serializable, TypeRegistryItem("状态基类")]
    public class StateBase : IPoolableAuto
    {
        #region 对象池支持

        /// <summary>
        /// StateBase 对象池
        /// 容量: 500个对象
        /// 预热: 10个初始对象
        /// </summary>
        public static readonly ESSimplePool<StateBase> Pool = new ESSimplePool<StateBase>(
            factoryMethod: () => new StateBase(),
            resetMethod: (obj) => obj.OnResetAsPoolable(),
            initCount: 10,
            maxCount: -1,
            poolDisplayName: "StateBase Pool"
        );

        /// <summary>
        /// 对象回收标记
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 重置对象状态 —— 彻底清理所有运行时数据
        /// </summary>
        public void OnResetAsPoolable()
        {
            host = null;
            _layerRuntime = null;
            _pipelineSlotIndex = -1;
            activationTime = 0f;
            hasEnterTime = 0f;
            normalizedProgress = 0f;
            totalProgress = 0f;
            loopCount = 0;
            _lastNormalizedProgress = 0f;
            baseStatus = StateBaseStatus.Never;
            stateRuntimePhase = StateRuntimePhase.Pre;
            _runtimePhaseManual = false;
            strKey = null;
            intKey = -1;
            stateSharedData = null;
            stateVariableData = null;
            _shouldAutoExitFromAnimation = false;
            _playableWeight = 1f;
            OnRuntimePhaseChanged = null; // ★ 清理委托引用防止内存泄漏

            // IK/MatchTarget状态
            _ikActive = false;
            _matchTargetActive = false;

            DestroyPlayable();
        }

        /// <summary>
        /// 尝试回收到对象池
        /// </summary>
        public void TryAutoPushedToPool()
        {
            if (!IsRecycled)
            {
                // ★ 不在这里设置 IsRecycled = true
                // PushToPool 内部流程：检查IsRecycled → resetMethod → 设置IsRecycled=true → 入栈
                // 如果提前设置，PushToPool会误判为"已回收"而拒绝入池
                Pool.PushToPool(this);
            }
        }

        #endregion

        #region  基础属性

        [NonSerialized]
        public StateMachine host;

        [NonSerialized]
        private StateLayerRuntime _layerRuntime;

        [NonSerialized]
        private int _pipelineSlotIndex = -1;

        /// <summary>
        /// 初始化状态（绑定宿主StateMachine）
        /// </summary>
        public virtual void Initialize(StateMachine machine)
        {
            host = machine;
        }

        /// <summary>
        /// 状态激活时间（用于追踪持续时长）
        /// </summary>
        [NonSerialized]
        public float activationTime = 0f;

        #region 运行时数据（保证可用）
        
        /// <summary>
        /// 已经进入时间（从进入状态过去的时间）
        /// </summary>
        [NonSerialized]
        public float hasEnterTime = 0f;

        /// <summary>
        /// 归一化进度（适用于循环动画）[0-1]
        /// </summary>
        [NonSerialized]
        public float normalizedProgress = 0f;

        /// <summary>
        /// 总体进度（比如5.5代表已经循环了5次半）
        /// </summary>
        [NonSerialized]
        public float totalProgress = 0f;

        /// <summary>
        /// 循环次数（完成的循环次数）
        /// </summary>
        [NonSerialized]
        public int loopCount = 0;

        /// <summary>
        /// 上一帧的归一化进度（用于事件触发检测）
        /// </summary>
        [NonSerialized]
        private float _lastNormalizedProgress = 0f;
        #endregion

        [LabelText("共享数据", SdfIconType.Calendar2Date), FoldoutGroup("基础属性"), NonSerialized/*不让自动序列化*/] public StateSharedData stateSharedData = null;
        [LabelText("自变化数据", SdfIconType.Calendar3Range), FoldoutGroup("基础属性")] public StateVariableData stateVariableData;

        /// <summary>
        /// 运行时动画数据 - 由Calculator创建并管理，零GC
        /// </summary>
        [NonSerialized]
        private AnimationCalculatorRuntime _animationRuntime;

        /// <summary>
        /// 获取动画Runtime（只读访问，用于外部IK/MatchTarget等高级操作）
        /// </summary>
        public AnimationCalculatorRuntime AnimationRuntime => _animationRuntime;

        /// <summary>
        /// 动画控制的自动退出标志（AnimationClip反向控制）
        /// </summary>
        [NonSerialized]
        private bool _shouldAutoExitFromAnimation = false;

        /// <summary>
        /// IK是否已激活（避免重复设置）
        /// </summary>
        [NonSerialized]
        private bool _ikActive = false;

        /// <summary>
        /// MatchTarget是否已激活
        /// </summary>
        [NonSerialized]
        private bool _matchTargetActive = false;

        #endregion

        #region 增强回调系统

        /// <summary>
        /// 状态进度回调（每帧调用）
        /// 子类可重写实现基于进度的逻辑
        /// </summary>
        protected virtual void OnProgressUpdate(float normalizedProgress, float totalProgress)
        {
            // 默认不实现，子类重写
        }

        /// <summary>
        /// 循环完成回调
        /// 子类可重写实现循环触发逻辑
        /// </summary>
        protected virtual void OnLoopCompleted(int loopCount)
        {
            // 默认不实现，子类重写
        }

        /// <summary>
        /// 动画事件回调
        /// 当动画播放到指定时间点时触发
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="eventParam">事件参数</param>
        protected virtual void OnAnimationEvent(string eventName, string eventParam)
        {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[AnimEvent] State:{stateSharedData?.basicConfig.stateName} | Event:{eventName} | Param:{eventParam}");
#endif
#endif
            
            // 可以通知StateMachine广播事件
            host?.BroadcastAnimationEvent(this, eventName, eventParam);
        }

        /// <summary>
        /// 淡入完成回调
        /// 由StateMachine调用
        /// </summary>
        public virtual void OnFadeInComplete()
        {
            // 默认不实现，子类重写
        }

        /// <summary>
        /// 淡出开始回调
        /// 由StateMachine调用
        /// </summary>
        public virtual void OnFadeOutStarted()
        {
            // 默认不实现，子类重写
        }

        #endregion

        #region 键


        public string strKey;
        public int intKey;
        #endregion

        #region 权重

        [NonSerialized]
        private float _playableWeight = 1f;

        public float PlayableWeight => _playableWeight;

        internal void SetPlayableWeight(float weight)
        {
            _playableWeight = Mathf.Clamp01(weight);
            if (_animationRuntime != null)
            {
                _animationRuntime.totalWeight = _playableWeight;
                ApplyTotalWeightToRuntime(_animationRuntime);
                ApplyExternalWeightToPipeline();
            }
        }

        internal void BindLayerSlot(StateLayerRuntime layer, int slotIndex)
        {
            _layerRuntime = layer;
            _pipelineSlotIndex = slotIndex;
            ApplyExternalWeightToPipeline();
        }

        internal void ClearLayerSlot()
        {
            _layerRuntime = null;
            _pipelineSlotIndex = -1;
        }

        private bool UsesExternalWeight()
        {
            return _animationRuntime != null && !_animationRuntime.HasInternalWeightMixer();
        }

        internal bool ShouldUseExternalPipelineWeight()
        {
            return UsesExternalWeight();
        }

        private void ApplyExternalWeightToPipeline()
        {
            if (!UsesExternalWeight()) return;
            if (_layerRuntime == null || !_layerRuntime.mixer.IsValid()) return;
            if (_pipelineSlotIndex < 0 || _pipelineSlotIndex >= _layerRuntime.mixer.GetInputCount()) return;

            _layerRuntime.mixer.SetInputWeight(_pipelineSlotIndex, _playableWeight);
        }

        private static void ApplyTotalWeightToRuntime(AnimationCalculatorRuntime runtime)
        {
            if (runtime == null || !runtime.mixer.IsValid()) return;

            int inputCount = runtime.mixer.GetInputCount();
            if (inputCount <= 0) return;

            float totalWeight = runtime.totalWeight;

            if (runtime.weightCache != null && runtime.weightCache.Length >= inputCount)
            {
                for (int i = 0; i < inputCount; i++)
                {
                    runtime.mixer.SetInputWeight(i, runtime.weightCache[i] * totalWeight);
                }
            }
            else if (runtime.currentWeights != null && runtime.currentWeights.Length >= inputCount)
            {
                for (int i = 0; i < inputCount; i++)
                {
                    runtime.mixer.SetInputWeight(i, runtime.currentWeights[i] * totalWeight);
                }
            }
            else
            {
                float scale = runtime.lastAppliedTotalWeight > 0f
                    ? totalWeight / runtime.lastAppliedTotalWeight
                    : totalWeight;

                for (int i = 0; i < inputCount; i++)
                {
                    float currentWeight = runtime.mixer.GetInputWeight(i);
                    runtime.mixer.SetInputWeight(i, currentWeight * scale);
                }
            }

            runtime.lastAppliedTotalWeight = totalWeight;
        }

        #endregion

        #region 状态生命周期
        public StateBaseStatus baseStatus { get; private set; } = StateBaseStatus.Never;
        public StateRuntimePhase stateRuntimePhase = StateRuntimePhase.Pre;
        [NonSerialized]
        private bool _runtimePhaseManual = false;

        public Action<StateBase, StateRuntimePhase, StateRuntimePhase> OnRuntimePhaseChanged;

      
        public void SetRuntimePhase(StateRuntimePhase phase, bool lockPhase = true)
        {
            if (phase != stateRuntimePhase)
            {
                var previous = stateRuntimePhase;
                stateRuntimePhase = phase;
                OnRuntimePhaseChanged?.Invoke(this, previous, phase);
            }
            _runtimePhaseManual = lockPhase;
        }

        public void SetRuntimePhaseFromCalculator(StateRuntimePhase phase, bool lockPhase = true)
        {
            SetRuntimePhase(phase, lockPhase);
        }

        public void ClearRuntimePhaseOverride()
        {
            _runtimePhaseManual = false;
        }
        public void OnStateEnter()
        {
            if (baseStatus == StateBaseStatus.Running) return;
            baseStatus = StateBaseStatus.Running;
            if (stateRuntimePhase != StateRuntimePhase.Pre)
            {
                var previous = stateRuntimePhase;
                stateRuntimePhase = StateRuntimePhase.Pre;
                OnRuntimePhaseChanged?.Invoke(this, previous, stateRuntimePhase);
            }
            _runtimePhaseManual = false;
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

            // ★ 自动应用IK配置（从Inspector配置到Runtime）
            ApplyIKConfigOnEnter();
            // ★ 自动应用MatchTarget配置
            ApplyMatchTargetConfigOnEnter();
            
            OnStateEnterLogic();
        }

        private void MarkAutoExitFromAnimation(string reason)
        {
            if (_shouldAutoExitFromAnimation) return;
            _shouldAutoExitFromAnimation = true;
    #if STATEMACHINEDEBUG
    #if UNITY_EDITOR
            Debug.Log($"[StateBase] AutoExitFlag=TRUE | State={stateSharedData?.basicConfig?.stateName} | Reason={reason}\n{Environment.StackTrace}");
    #endif
    #endif
        }

        public void OnStateUpdate()
        {
            OnStateUpdateLogic();
        }

        public void OnStateExit()
        {
            if (baseStatus != StateBaseStatus.Running) return;
            baseStatus = StateBaseStatus.Exited;
            if (stateRuntimePhase != StateRuntimePhase.Released)
            {
                var previous = stateRuntimePhase;
                stateRuntimePhase = StateRuntimePhase.Released;
                OnRuntimePhaseChanged?.Invoke(this, previous, stateRuntimePhase);
            }

            // ★ 状态退出时自动清理IK
            if (stateSharedData?.animationConfig != null && stateSharedData.animationConfig.disableIKOnExit)
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

        #region 应用层用户自己重写逻辑
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

        #region Playable创建与管理

        /// <summary>
        /// 当前动画计算器类型（用于扩展能力判断）
        /// </summary>
        public StateAnimationMixerKind CurrentCalculatorKind
            => stateSharedData.animationConfig.calculator.CalculatorKind;

        /// <summary>
        /// 创建状态的Playable节点 - 零GC高性能实现
        /// 使用StateSharedData中的混合计算器生成Playable
        /// </summary>
        /// <param name="graph">PlayableGraph引用</param>
        /// <param name="output">输出Playable引用</param>
        /// <returns>是否创建成功</returns>
        public virtual bool CreatePlayable(PlayableGraph graph, out Playable output)
        {
            output = Playable.Null;

            // 验证数据有效性
            var calculator = stateSharedData.animationConfig.calculator;
            if (calculator == null)
                return false;

            // 创建运行时数据（仅创建一次）
            if (_animationRuntime == null)
            {
                _animationRuntime = calculator.CreateRuntimeData();
            }

            // 委托给Calculator初始化Playable
            Playable tempOutput = Playable.Null;
            bool success = calculator.InitializeRuntime(
                _animationRuntime,
                graph,
                ref tempOutput
            );

            if (success && tempOutput.IsValid())
            {
                output = tempOutput;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 更新动画权重 - 每帧调用（可选）
        /// ★ 修复：移除了重复的UpdateRuntimeProgress调用（这是导致计算器失效的关键Bug）
        /// </summary>
        /// <param name="context">状态上下文</param>
        /// <param name="deltaTime">帧时间</param>
        public virtual void UpdateAnimationWeights(StateMachineContext context, float deltaTime)
        {
            // 更新运行时数据
            var shared = stateSharedData;
            bool hasAnimation = shared.hasAnimation;
            if (!hasAnimation)
                return;

            // ★ 累加进入时间（无论是否需要进度追踪都要累加）
            hasEnterTime += deltaTime;

            var basic = shared.basicConfig;
            bool needsProgress = basic.enableProgressTracking;
            var phaseConfig = basic.phaseConfig;
            if (phaseConfig.enablePhase && phaseConfig.enableAutoPhaseByTime)
                needsProgress = true;

            // ★ 修复：进度更新只调用一次（之前被调用了两次导致2x速度推进！）
            if (needsProgress)
            {
                UpdateRuntimeProgress(deltaTime);
            }

            var runtime = _animationRuntime;
            var calculator = shared.animationConfig.calculator;
            if (runtime != null && runtime.IsInitialized && calculator != null)
            {
                runtime.totalWeight = _playableWeight;
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                StateMachineDebugSettings.Instance.LogAnimationBlend(
                    $"State={shared.basicConfig.stateName} 进行权重更新");
#endif
#endif
                calculator.UpdateWeights(runtime, context, deltaTime);
                
                // ★ AnimationClip反向控制：检测动画是否播放完毕（仅针对UntilAnimationEnd模式）
                if (shared.basicConfig.durationMode == StateDurationMode.UntilAnimationEnd)
                {
                    // ★ 关键修复：SequentialClipMixer有自己的完成标志，优先桥接
                    // CheckAnimationCompletion对SequentialClipMixer无效（所有Playable的Duration=infinity）
                    if (runtime.sequenceCompleted)
                    {
                        MarkAutoExitFromAnimation("SequenceComplete");
                    }
                    else
                    {
                        CheckAnimationCompletion();
                    }
                }

                // ★ 更新IK权重平滑过渡
                if (runtime.ik.enabled)
                {
                    float ikSmooth = shared.animationConfig.ikSmoothTime;
                    runtime.UpdateIKWeight(ikSmooth, deltaTime);

                    // ★ 动态IK目标追踪：如果配置了Transform引用，每帧同步位置
                    if (shared.animationConfig.HasDynamicIKTargets())
                    {
                        shared.animationConfig.UpdateIKTargetsFromConfig(runtime);
                    }
                }

                // ★ 更新MatchTarget进度检查
                if (runtime.matchTarget.active && !runtime.matchTarget.completed)
                {
                    if (normalizedProgress > runtime.matchTarget.endTime)
                    {
                        runtime.CompleteMatchTarget();
                        OnMatchTargetCompleted();
                    }
                }
            }
        }

        /// <summary>
        /// 更新运行时进度数据
        /// </summary>
        private void UpdateRuntimeProgress(float deltaTime)
        {
            // 获取标准动画时长（不经历外部缩放速度）
            float standardDuration = GetStandardAnimationDuration();
            
            int previousLoopCount = loopCount;  // 记录上一次的循环次数
            
            if (standardDuration > 0.001f)
            {
                // 计算总体进度
                totalProgress = hasEnterTime / standardDuration;
                
                // 计算归一化进度 [0-1]
                normalizedProgress = totalProgress % 1.0f;
                
                // 计算循环次数
                loopCount = Mathf.FloorToInt(totalProgress);
            }
            else
            {
                // 无法获取时长，使用简单递增
                normalizedProgress = Mathf.Repeat(hasEnterTime, 1.0f);
                totalProgress = hasEnterTime;
            }
            
            // 调用进度回调
            OnProgressUpdate(normalizedProgress, totalProgress);

            // 自动阶段评估（无手动锁定时）
            UpdateAutoRuntimePhase();
            
            // 检测循环完成
            if (loopCount > previousLoopCount)
            {
                OnLoopCompleted(loopCount);
            }
            
            // 检测动画事件触发
            CheckAnimationEventTriggers();
        }

        private void UpdateAutoRuntimePhase()
        {
            if (_runtimePhaseManual)
                return;

            var shared = stateSharedData;
            if (!shared.hasAnimation)
                return;

            var phaseConfig = shared.basicConfig.phaseConfig;
            if (!phaseConfig.enablePhase || !phaseConfig.enableAutoPhaseByTime)
                return;

            var nextPhase = phaseConfig.EvaluatePhase(normalizedProgress);
            if (nextPhase != stateRuntimePhase)
            {
                var previous = stateRuntimePhase;
                stateRuntimePhase = nextPhase;
                OnRuntimePhaseChanged?.Invoke(this, previous, nextPhase);
            }
        }

        /// <summary>
        /// 检测并触发动画事件
        /// </summary>
        private void CheckAnimationEventTriggers()
        {
            // 获取动画配置中的事件列表
            var triggerEvents = GetAnimationTriggerEvents();
            if (triggerEvents == null || triggerEvents.Count == 0)
                return;

            foreach (var evt in triggerEvents)
            {
                // 检测是否穿过触发点
                bool crossedTriggerPoint = false;
                
                // 情况1：正常前进，穿过触发点
                if (_lastNormalizedProgress < evt.normalizedTime && 
                    normalizedProgress >= evt.normalizedTime)
                {
                    crossedTriggerPoint = true;
                }
                
                // 情况2：循环回绕（从1回到0）
                if (_lastNormalizedProgress > normalizedProgress)
                {
                    // 新循环开始，重置触发标记
                    evt.ResetTrigger();
                    
                    // 检查是否在新循环中穿过触发点
                    if (evt.normalizedTime < normalizedProgress)
                    {
                        crossedTriggerPoint = true;
                    }
                }
                
                // 触发事件
                if (crossedTriggerPoint)
                {
                    if (!evt.triggerOnce || !evt.hasTriggered)
                    {
                        OnAnimationEvent(evt.eventName, evt.eventParam);
                        evt.hasTriggered = true;
                    }
                }
            }
            
            _lastNormalizedProgress = normalizedProgress;
        }

        /// <summary>
        /// 获取动画触发事件列表
        /// </summary>
        private List<TriggerEventAt> GetAnimationTriggerEvents()
        {
            // 暂时返回null，后续集成AnimationClipConfig时实现
            // TODO: 从animationConfig.calculator中获取triggerEvents
            return null;
        }

        /// <summary>
        /// 重置动画事件触发标记
        /// </summary>
        private void ResetAnimationEventTriggers()
        {
            var triggerEvents = GetAnimationTriggerEvents();
            if (triggerEvents != null)
            {
                foreach (var evt in triggerEvents)
                {
                    evt.ResetTrigger();
                }
            }
            _lastNormalizedProgress = 0f;
        }

        /// <summary>
        /// 获取标准动画时长（不经历外部缩放速度）
        /// </summary>
        public float GetStandardAnimationDuration()
        {
            if (_animationRuntime?.IsInitialized == true && stateSharedData?.animationConfig?.calculator != null)
            {
                return stateSharedData.animationConfig.calculator.GetStandardDuration(_animationRuntime);
            }
            return 0f;
        }

        /// <summary>
        /// 检测动画播放完毕状态（AnimationClip反向控制）
        /// 当动画播放进度>=1.0且为非循环模式时，标记应该退出
        /// </summary>
        private void CheckAnimationCompletion()
        {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            StateMachineDebugSettings.Instance.LogAnimationBlend(
                $"检测动画完毕: State={stateSharedData.basicConfig.stateName} | RuntimeInit={_animationRuntime != null && _animationRuntime.IsInitialized}");
#endif
#endif
            if (_animationRuntime == null || !_animationRuntime.IsInitialized)
                return;

            // 检查单个Playable（SimpleClip）
            if (_animationRuntime.singlePlayable.IsValid())
            {
                Playable playable = _animationRuntime.singlePlayable;
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                StateMachineDebugSettings.Instance.LogAnimationBlend(
                    $"单Playable检测: Type={playable.GetPlayableType().Name} | Time={playable.GetTime():F3} | Duration={playable.GetDuration():F3}");
#endif
#endif
                if (playable.GetPlayableType() == typeof(AnimationClipPlayable))
                {
                    var clipPlayable = (AnimationClipPlayable)playable;
                    var animationClip = GetAnimationClipFromPlayable(clipPlayable);
                    
                    if (animationClip != null && !animationClip.isLooping)
                    {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                        StateMachineDebugSettings.Instance.LogAnimationBlend(
                            $"单Clip: {animationClip.name} | Loop={animationClip.isLooping}");
#endif
#endif
                        double currentTime = playable.GetTime();
                        double duration = playable.GetDuration();
                        if (double.IsInfinity(duration) || duration <= 0.001)
                        {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                            StateMachineDebugSettings.Instance.LogWarning($"[StateBase] 单Clip时长异常: {duration}");
#endif
#endif
                            return;
                        }
                        
                        // 播放进度>=1.0，标记为应该退出
                        if (duration > 0.001 && currentTime >= duration - 0.016) // 0.016 = 1帧容错（60fps）
                        {
                            MarkAutoExitFromAnimation("SingleClipComplete");
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                            StateMachineDebugSettings.Instance.LogAnimationBlend("单Clip播放完毕 -> 标记退出");
#endif
#endif
                        }
                    }
                    else
                    {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                        StateMachineDebugSettings.Instance.LogAnimationBlend("单Clip为空或循环播放，跳过完成检测");
#endif
#endif
                    }
                }
                else
                {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                    StateMachineDebugSettings.Instance.LogAnimationBlend("单Playable非AnimationClipPlayable，跳过完成检测");
#endif
#endif
                }
            }
            // 检查Mixer中的Playables（BlendTree/DirectBlend）
            else if (_animationRuntime.playables != null)
            {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                StateMachineDebugSettings.Instance.LogAnimationBlend(
                    $"多Playable检测: Count={_animationRuntime.playables.Length}");
#endif
#endif
                // 多动画混合的情况：只有当所有非循环动画都播放完毕才退出
                bool hasNonLoopingClip = false;
                bool allNonLoopingCompleted = true;

                for (int i = 0; i < _animationRuntime.playables.Length; i++)
                {
                    Playable playable = _animationRuntime.playables[i];
                    if (!playable.IsValid()) continue;

                    if (playable.GetPlayableType() == typeof(AnimationClipPlayable))
                    {
                        var clipPlayable = (AnimationClipPlayable)playable;
                        var animationClip = GetAnimationClipFromPlayable(clipPlayable);
                        
                        if (animationClip != null && !animationClip.isLooping)
                        {
                            hasNonLoopingClip = true;
                            
                            double currentTime = playable.GetTime();
                            double duration = playable.GetDuration();
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                            StateMachineDebugSettings.Instance.LogAnimationBlend(
                                $"子Clip[{i}]: {animationClip.name} | Time={currentTime:F3} | Duration={duration:F3} | Loop={animationClip.isLooping}");
#endif
#endif
                            if (double.IsInfinity(duration) || duration <= 0.001)
                            {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                                StateMachineDebugSettings.Instance.LogWarning($"[StateBase] 子Clip时长异常: {duration}");
#endif
#endif
                                continue;
                            }
                            
                            if (duration > 0.001 && currentTime < duration - 0.016)
                            {
                                allNonLoopingCompleted = false;
                                break;
                            }
                        }
                        else
                        {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                            StateMachineDebugSettings.Instance.LogAnimationBlend(
                                $"子Clip[{i}]为空或循环播放，跳过完成检测");
#endif
#endif
                        }
                    }
                    else
                    {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                        StateMachineDebugSettings.Instance.LogAnimationBlend(
                            $"子Playable[{i}]非AnimationClipPlayable: {playable.GetPlayableType().Name}");
#endif
#endif
                    }
                }

                if (hasNonLoopingClip && allNonLoopingCompleted)
                {
                    MarkAutoExitFromAnimation("AllNonLoopingClipsComplete");
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                    StateMachineDebugSettings.Instance.LogAnimationBlend("多Clip全部播放完毕 -> 标记退出");
#endif
#endif
                }
            }
            else
            {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                StateMachineDebugSettings.Instance.LogWarning("[StateBase] 无可检测Playable（singlePlayable无效且playables为空）");
#endif
#endif
            }
        }

        /// <summary>
        /// 从AnimationClipPlayable获取AnimationClip
        /// TODO: 当前使用简单实现，后续可优化为缓存或更高效的方式
        /// </summary>
        private AnimationClip GetAnimationClipFromPlayable(AnimationClipPlayable clipPlayable)
        {
            // 当前简单实现：直接调用GetAnimationClip()
            return clipPlayable.GetAnimationClip();
        }

        /// <summary>
        /// 销毁Playable - 状态退出时调用（零GC）
        /// ★ 改进：支持Runtime回收到对象池
        /// </summary>
        public virtual void DestroyPlayable()
        {
            if (_animationRuntime != null)
            {
                // 使用Runtime的Cleanup方法统一清理所有Playable资源
                _animationRuntime.Cleanup();
                // ★ 回收到对象池而非丢弃
                _animationRuntime.TryAutoPushedToPool();
                _animationRuntime = null;
            }
            _ikActive = false;
            _matchTargetActive = false;
        }

        /// <summary>
        /// 获取当前主动画Clip（调试用）
        /// </summary>
        public virtual AnimationClip GetCurrentClip()
        {
            if (_animationRuntime != null && _animationRuntime.IsInitialized && stateSharedData.animationConfig.calculator != null)
            {
                return stateSharedData.animationConfig.calculator.GetCurrentClip(_animationRuntime);
            }
            return null;
        }

        /// <summary>
        /// 检查状态是否应该自动退出（根据持续时间模式）
        /// ★ 优化：减少冗余的属性访问和类型转换
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <returns>是否应该退出</returns>
        public virtual bool ShouldAutoExit(float currentTime)
        {
            var config = stateSharedData.basicConfig;
            float elapsedTime = currentTime - activationTime;

            switch (config.durationMode)
            {
                case StateDurationMode.Infinite:
                    return false; // 无限持续，不自动退出

                case StateDurationMode.Timed:
                    if (elapsedTime >= config.timedDuration)
                    {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                        StateMachineDebugSettings.Instance.LogStateTransition(
                            $"[StateBase] ShouldAutoExit=TRUE (Timed) | State={stateSharedData.basicConfig.stateName} | Elapsed={elapsedTime:F3} | Duration={config.timedDuration:F3}");
#endif
#endif
                        return true;
                    }
                    return false;

                case StateDurationMode.UntilAnimationEnd:
                    // ★ 检查动画是否播放完毕（优先使用AnimationClip反向控制的标志）
                    if (_shouldAutoExitFromAnimation)
                    {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                        StateMachineDebugSettings.Instance.LogStateTransition(
                            $"[StateBase] ShouldAutoExit=TRUE (AnimFlag) | State={stateSharedData.basicConfig.stateName}");
#endif
#endif
                        return true;
                    }
                    
                    if (!config.enableClipLengthFallback)
                        return false;

                    // 备用逻辑：通过Clip长度计算
                    return CheckClipLengthFallbackExit(elapsedTime);

                default:
                    return false;
            }
        }

        /// <summary>
        /// ★ 抽取的Clip长度回退退出逻辑（降低ShouldAutoExit复杂度）
        /// ★ 修复：SequentialClipMixer使用GetStandardDuration（总时长），而非当前阶段Clip长度
        ///   之前GetCurrentClip返回当前阶段的Clip，导致用总逝去时间 vs 单阶段时长比较 → 提前退出
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckClipLengthFallbackExit(float elapsedTime)
        {
            if (_animationRuntime == null || !_animationRuntime.IsInitialized)
                return false;

            var calculator = stateSharedData.animationConfig.calculator;
            if (calculator == null)
                return false;

            // ★ 优先使用GetStandardDuration（对SequentialClipMixer返回Entry+Main+Exit总时长）
            float standardDuration = calculator.GetStandardDuration(_animationRuntime);
            if (standardDuration > 0.001f && !float.IsInfinity(standardDuration) && !float.IsPositiveInfinity(standardDuration))
            {
                if (elapsedTime >= standardDuration)
                {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                    StateMachineDebugSettings.Instance.LogStateTransition(
                        $"[StateBase] ShouldAutoExit=TRUE (StandardDuration) | State={stateSharedData.basicConfig.stateName} | Elapsed={elapsedTime:F3} | Duration={standardDuration:F3}");
#endif
#endif
                    return true;
                }
                return false;
            }

            // 回退：单Clip模式
            var clip = calculator.GetCurrentClip(_animationRuntime);
            if (clip == null)
                return false;

            // 获取动画速度
            float speed = 1.0f;
            if (calculator is StateAnimationMixCalculatorForSimpleClip simpleCalc)
            {
                speed = simpleCalc.speed;
            }

            float animDuration = clip.length / Mathf.Max(0.01f, speed);
            if (elapsedTime >= animDuration)
            {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"[StateBase] ShouldAutoExit=TRUE (ClipLength) | State={stateSharedData.basicConfig.stateName} | Clip={clip.name} | Elapsed={elapsedTime:F3} | AnimDuration={animDuration:F3} | Speed={speed:F3}");
#endif
#endif
                return true;
            }
            return false;
        }

        #endregion

        #region IK/MatchTarget 配置自动应用

        /// <summary>
        /// 状态进入时自动从 StateAnimationConfigData 应用IK配置到Runtime
        /// 仅在 enableIK=true 且 ikSourceMode != CodeOnly 时生效
        /// </summary>
        private void ApplyIKConfigOnEnter()
        {
            if (_animationRuntime == null || stateSharedData?.animationConfig == null) return;
            var animConfig = stateSharedData.animationConfig;
            if (!animConfig.enableIK) return;
            if (animConfig.ikSourceMode == IKSourceMode.CodeOnly) return;

            // 从Inspector配置数据写入Runtime
            Transform rootTransform = host?.BoundAnimator?.transform;
            animConfig.ApplyIKConfigToRuntime(_animationRuntime, rootTransform);
            _ikActive = true;
        }

        /// <summary>
        /// 状态进入时自动从 StateAnimationConfigData 应用MatchTarget配置到Runtime
        /// 仅在 enableMatchTarget=true 且 autoActivateMatchTarget=true 时生效
        /// </summary>
        private void ApplyMatchTargetConfigOnEnter()
        {
            if (_animationRuntime == null || stateSharedData?.animationConfig == null) return;
            var animConfig = stateSharedData.animationConfig;
            if (!animConfig.enableMatchTarget || !animConfig.autoActivateMatchTarget) return;

            animConfig.ApplyMatchTargetConfigToRuntime(_animationRuntime);
            _matchTargetActive = true;
        }

        #endregion

        #region IK支持（商业级特性）

        /// <summary>
        /// 启用IK并设置目标（由外部系统调用）
        /// </summary>
        /// <param name="goal">IK目标（左/右手/脚）</param>
        /// <param name="position">目标位置</param>
        /// <param name="rotation">目标旋转</param>
        /// <param name="weight">权重 [0-1]</param>
        public void SetIKGoal(AvatarIKGoal goal, Vector3 position, Quaternion rotation, float weight)
        {
            if (_animationRuntime == null) return;
            _ikActive = true;
            _animationRuntime.SetIKGoal(goal, position, rotation, weight);
        }

        /// <summary>
        /// 设置IK提示位置（肘/膝方向引导）
        /// </summary>
        public void SetIKHintPosition(AvatarIKHint hint, Vector3 position)
        {
            if (_animationRuntime == null) return;
            _animationRuntime.SetIKHintPosition(hint, position);
        }

        /// <summary>
        /// 设置注视目标
        /// </summary>
        public void SetLookAtTarget(Vector3 position, float weight)
        {
            if (_animationRuntime == null) return;
            _ikActive = true;
            _animationRuntime.SetLookAtTarget(position, weight);
        }

        /// <summary>
        /// 设置IK总目标权重（平滑过渡）
        /// </summary>
        public void SetIKTargetWeight(float weight)
        {
            if (_animationRuntime == null) return;
            _animationRuntime.ik.targetWeight = Mathf.Clamp01(weight);
        }

        /// <summary>
        /// 禁用IK
        /// </summary>
        public void DisableIK()
        {
            _ikActive = false;
            if (_animationRuntime != null)
            {
                _animationRuntime.ik.targetWeight = 0f;
                // IK权重会在UpdateAnimationWeights中平滑过渡到0
            }
        }

        /// <summary>
        /// IK是否处于活跃状态
        /// </summary>
        public bool IsIKActive => _ikActive && _animationRuntime != null && _animationRuntime.ik.enabled;

        /// <summary>
        /// IK处理回调（子类可重写实现自定义IK逻辑）
        /// 在OnAnimatorIK中由StateMachine转发调用
        /// </summary>
        /// <param name="animator">Animator组件</param>
        /// <param name="layerIndex">动画层索引</param>
        protected virtual void OnStateAnimatorIK(Animator animator, int layerIndex)
        {
            if (_animationRuntime == null || !_animationRuntime.ik.enabled) return;
            if (_animationRuntime.ik.weight < 0.001f) return;

            ref var ik = ref _animationRuntime.ik;

            // 注视IK（支持高级权重配置）
            if (ik.lookAtWeight > 0.001f)
            {
                animator.SetLookAtPosition(ik.lookAtPosition);

                var animConfig = stateSharedData?.animationConfig;
                if (animConfig != null && animConfig.enableIK && animConfig.ikLookAt.enabled)
                {
                    // 使用配置的细分权重
                    var lookCfg = animConfig.ikLookAt;
                    animator.SetLookAtWeight(
                        ik.lookAtWeight * ik.weight,
                        lookCfg.bodyWeight,
                        lookCfg.headWeight,
                        lookCfg.eyesWeight,
                        lookCfg.clampWeight
                    );
                }
                else
                {
                    animator.SetLookAtWeight(ik.lookAtWeight * ik.weight);
                }
            }

            // 四肢IK
            ApplyLimbIK(animator, AvatarIKGoal.LeftHand, ik.leftHandPosition, ik.leftHandRotation,
                ik.leftHandWeight * ik.weight, ik.leftHandHintPosition, AvatarIKHint.LeftElbow);
            ApplyLimbIK(animator, AvatarIKGoal.RightHand, ik.rightHandPosition, ik.rightHandRotation,
                ik.rightHandWeight * ik.weight, ik.rightHandHintPosition, AvatarIKHint.RightElbow);
            ApplyLimbIK(animator, AvatarIKGoal.LeftFoot, ik.leftFootPosition, ik.leftFootRotation,
                ik.leftFootWeight * ik.weight, ik.leftFootHintPosition, AvatarIKHint.LeftKnee);
            ApplyLimbIK(animator, AvatarIKGoal.RightFoot, ik.rightFootPosition, ik.rightFootRotation,
                ik.rightFootWeight * ik.weight, ik.rightFootHintPosition, AvatarIKHint.RightKnee);
        }

        /// <summary>
        /// 应用单个肢体IK
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyLimbIK(Animator animator, AvatarIKGoal goal, Vector3 pos, Quaternion rot,
            float weight, Vector3 hintPos, AvatarIKHint hint)
        {
            if (weight < 0.001f) return;
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotationWeight(goal, weight);
            animator.SetIKPosition(goal, pos);
            animator.SetIKRotation(goal, rot);
            if (hintPos != Vector3.zero)
            {
                animator.SetIKHintPositionWeight(hint, weight);
                animator.SetIKHintPosition(hint, hintPos);
            }
        }

        /// <summary>
        /// 由StateMachine在OnAnimatorIK时调用（内部接口）
        /// </summary>
        internal void ProcessAnimatorIK(Animator animator, int layerIndex)
        {
            OnStateAnimatorIK(animator, layerIndex);
        }

        #endregion

        #region MatchTarget支持（商业级特性）

        /// <summary>
        /// 启动MatchTarget（根动作对齐到目标位置）
        /// 用于攀爬、跳跃落地等需要精确对齐的场景
        /// </summary>
        /// <param name="targetPos">目标位置</param>
        /// <param name="targetRot">目标旋转</param>
        /// <param name="bodyPart">身体部位</param>
        /// <param name="startNormTime">开始归一化时间 [0-1]</param>
        /// <param name="endNormTime">结束归一化时间 [0-1]</param>
        /// <param name="posWeight">位置权重 (XYZ分量)</param>
        /// <param name="rotWeight">旋转权重 [0-1]</param>
        public void StartMatchTarget(Vector3 targetPos, Quaternion targetRot, AvatarTarget bodyPart,
            float startNormTime, float endNormTime, Vector3 posWeight, float rotWeight = 1f)
        {
            if (_animationRuntime == null)
            {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                Debug.LogWarning($"[StateBase] StartMatchTarget failed: runtime is null | State={stateSharedData?.basicConfig?.stateName}");
#endif
#endif
                return;
            }

            _matchTargetActive = true;
            _animationRuntime.StartMatchTarget(targetPos, targetRot, bodyPart, startNormTime, endNormTime, posWeight, rotWeight);
        }

        /// <summary>
        /// 取消MatchTarget
        /// </summary>
        public void CancelMatchTarget()
        {
            _matchTargetActive = false;
            if (_animationRuntime != null)
            {
                _animationRuntime.ResetMatchTargetData();
            }
        }

        /// <summary>
        /// MatchTarget是否处于活跃状态
        /// </summary>
        public bool IsMatchTargetActive => _matchTargetActive && _animationRuntime != null && _animationRuntime.matchTarget.active;

        /// <summary>
        /// MatchTarget完成回调（子类可重写）
        /// </summary>
        protected virtual void OnMatchTargetCompleted()
        {
            _matchTargetActive = false;
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[StateBase] MatchTarget完成 | State={stateSharedData?.basicConfig?.stateName}");
#endif
#endif
        }

        /// <summary>
        /// 由StateMachine在Update中调用处理MatchTarget（内部接口）
        /// </summary>
        internal void ProcessMatchTarget(Animator animator)
        {
            if (_animationRuntime == null || !_animationRuntime.matchTarget.active || _animationRuntime.matchTarget.completed)
                return;

            if (!_animationRuntime.IsMatchTargetInRange(normalizedProgress))
                return;

            ref var mt = ref _animationRuntime.matchTarget;

            // ★ 使用Animator.MatchTarget进行实际的根动作对齐
            if (animator != null && !animator.isMatchingTarget)
            {
                var matchRange = new MatchTargetWeightMask(mt.positionWeight, mt.rotationWeight);
                animator.MatchTarget(
                    mt.position,
                    mt.rotation,
                    mt.bodyPart,
                    matchRange,
                    mt.startTime,
                    mt.endTime
                );
            }
        }

        #endregion

        #region 调试与诊断

#if UNITY_EDITOR
        /// <summary>
        /// 获取状态的调试摘要（Editor Only）
        /// </summary>
        public string GetDebugSummary()
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append($"[State] {stateSharedData?.basicConfig?.stateName ?? "NULL"}");
            sb.Append($" | Status={baseStatus} Phase={stateRuntimePhase}");
            sb.Append($" | Weight={_playableWeight:F2}");
            sb.Append($" | Time={hasEnterTime:F2}s Progress={normalizedProgress:F2} Loop={loopCount}");
            if (_ikActive && _animationRuntime != null)
                sb.Append($" | IK={_animationRuntime.ik.weight:F2}");
            if (_matchTargetActive)
                sb.Append(" | MT=Active");
            if (_shouldAutoExitFromAnimation)
                sb.Append(" | AutoExit=Pending");
            if (_animationRuntime != null)
                sb.Append($" | Mem={_animationRuntime.GetMemoryFootprint()}B");
            return sb.ToString();
        }
#endif

        #endregion

    }

    public static class StateMachineAnimationEventExtensions
    {
        public static void BroadcastAnimationEvent(this StateMachine host, StateBase state, string eventName, string eventParam)
        {
            if (host == null)
            {
                return;
            }

            host.OnAnimationEvent?.Invoke(state, eventName, eventParam);

            if (host.hostEntity != null)
            {
                // 预留：实体广播
            }

#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[StateMachine] 广播动画事件: {eventName} | State: {state?.strKey} | Param: {eventParam}");
#endif
#endif
        }
    }
}





