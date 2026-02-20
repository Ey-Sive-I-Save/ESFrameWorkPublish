using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

// ============================================================================
// 文件：StateBase.Animation.cs 220 通过
// 作用：StateBase 的动画/Playable 相关实现（权重、Calculator、自动退出等）。
//
// Public（本文件定义的对外成员；按模块分组，“先功能、后成员”，便于扫读）：
//
// 【权重与计算器】
// - 查询当前输出权重：public float PlayableWeight { get; }
// - 查询计算器类型：public StateAnimationMixerKind CurrentCalculatorKind { get; }
//
// 【Playable 生命周期】
// - 创建 Playable：public bool CreatePlayable(PlayableGraph graph, out Playable output)
// - 销毁并释放：public void DestroyPlayable()
//
// 【每帧权重刷新】
// - 立即刷新（强制同步/调试）：public void ImmediateUpdateAnimationRuntime(StateMachineContext context)
// - 每帧刷新（热路径）：public void UpdateAnimationRuntime(StateMachineContext context, float deltaTime)
//
// 【调试与自动退出】
// - 获取当前主要 Clip：public AnimationClip GetCurrentClip()
// - 判断是否自动退出：public bool ShouldAutoExit(float currentTime)
//
// Private/Internal：PlayableGraph/Runtime 缓存、权重应用细节、自动退出判定的内部状态。
// ============================================================================

namespace ES
{
    public partial class StateBase
    {
        #region 权重与外部管线

        [NonSerialized]
        private float _playableWeight = 1f;

        public float PlayableWeight => _playableWeight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetPlayableWeight(float weight)
        {
            // 约定：调用方应传入 [0,1]。
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (weight < 0f || weight > 1f) throw new ArgumentOutOfRangeException(nameof(weight), weight, "SetPlayableWeight: weight 必须在 [0,1] 范围内");
#endif

            _playableWeight = weight;
            if (_animationRuntime != null)
            {
                _animationRuntime.totalWeight = weight;
                // ★ 修复：不再用totalWeight缩放内部Mixer权重（内部Mixer始终保持归一化=1.0）
                // 淮入/淮出影响统一通过层级Mixer的权重控制
                // ReapplyInternalMixerWeightsFromCache(_animationRuntime); // 已废弃
            }
            // ★ 权重写入统一由 StateMachine 每层批处理执行（减少 SetInputWeight 次数）
            if (_layerRuntime != null)
            {
                _layerRuntime.MarkDirty(PipelineDirtyFlags.MixerWeights);
            }
        }

        /// <summary>
        /// 热路径：假定 State 已被 HotPlug 并绑定到层级（_layerRuntime 非空，且有动画时 _animationRuntime 非空）。
        /// 用于淡入/淡出每帧更新，尽量避免反复判空。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetPlayableWeightAssumeBound(float weight)
        {
            // 约定：调用方应传入 [0,1]。
            _playableWeight = weight;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (weight < 0f || weight > 1f) throw new ArgumentOutOfRangeException(nameof(weight), weight, "SetPlayableWeightAssumeBound: weight 必须在 [0,1] 范围内");
            if (_layerRuntime == null) throw new InvalidOperationException("SetPlayableWeightAssumeBound: _layerRuntime 为空（调用方应保证已绑定层级）");
            if (_animationRuntime == null) throw new InvalidOperationException("SetPlayableWeightAssumeBound: _animationRuntime 为空（调用方应保证已创建Playable/Runtime）");
#endif

            // Fade 热路径只会用于已创建 Playable 的动画状态：这里直接写入，避免每帧判空/分支。
            _animationRuntime.totalWeight = weight;

            _layerRuntime.MarkDirty(PipelineDirtyFlags.MixerWeights);
        }

        internal void BindLayerSlot(StateLayerRuntime layer, int slotIndex)
        {
            _layerRuntime = layer;
            _pipelineSlotIndex = slotIndex;
            WritePlayableWeightToLayerMixerSlot();
        }

        internal void ClearLayerSlot()
        {
            _layerRuntime = null;
            _pipelineSlotIndex = -1;
        }
        private void WritePlayableWeightToLayerMixerSlot()
        {
            // ★ 所有状态类型都通过层级Mixer控制淮入淮出权重
            if (_layerRuntime == null || !_layerRuntime.mixer.IsValid()) return;
            if (_pipelineSlotIndex < 0 || _pipelineSlotIndex >= _layerRuntime.mixer.GetInputCount()) return;

            _layerRuntime.mixer.SetInputWeight(_pipelineSlotIndex, _playableWeight);
            _layerRuntime.MarkDirty(PipelineDirtyFlags.MixerWeights);
        }

        /// <summary>
        /// ★ 重新应用内部Mixer权重（不再缩放 totalWeight）
        /// 内部Mixer权重始终保持归一化(sum=1.0)，避免 bind pose 混入。
        /// 淮入淮出的影响统一由层级Mixer权重控制 + 归一化保证。
        /// </summary>
        private static void ReapplyInternalMixerWeightsFromCache(AnimationCalculatorRuntime runtime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runtime == null) throw new ArgumentNullException(nameof(runtime), "ReapplyInternalMixerWeightsFromCache: runtime 不能为空");
            if (!runtime.mixer.IsValid()) throw new InvalidOperationException("ReapplyInternalMixerWeightsFromCache: runtime.mixer 无效（调用方应保证已初始化Mixer）");
#endif

            int inputCount = runtime.mixer.GetInputCount();
            if (inputCount <= 0) return;

            // ★ 直接应用缓存的内部权重（不乘以 totalWeight）
            if (runtime.weightCache != null && runtime.weightCache.Length >= inputCount)
            {
                for (int i = 0; i < inputCount; i++)
                {
                    runtime.mixer.SetInputWeight(i, runtime.weightCache[i]);
                }
            }
            else if (runtime.currentWeights != null && runtime.currentWeights.Length >= inputCount)
            {
                for (int i = 0; i < inputCount; i++)
                {
                    runtime.mixer.SetInputWeight(i, runtime.currentWeights[i]);
                }
            }
            // 如果没有缓存，不做任何操作（计算器下一帧会设置正确值）

            runtime.lastAppliedTotalWeight = runtime.totalWeight;
        }

        #endregion

        #region Playable 创建与销毁

        /// <summary>
        /// 动画控制的自动退出标志（AnimationClip反向控制）
        /// </summary>
        [NonSerialized]
        private bool _shouldAutoExitFromAnimation = false;

        /// <summary>
        /// 当前动画计算器类型（用于扩展能力判断）
        /// </summary>
        public StateAnimationMixerKind CurrentCalculatorKind
            => _calculatorCached != null ? _calculatorCached.CalculatorKind : StateAnimationMixerKind.Unknown;

        /// <summary>
        /// 创建状态的Playable节点 - 零GC高性能实现
        /// 使用StateSharedData中的混合计算器生成Playable
        /// </summary>
        /// <param name="graph">PlayableGraph引用</param>
        /// <param name="output">输出Playable引用</param>
        /// <returns>是否创建成功</returns>
        public bool CreatePlayable(PlayableGraph graph, out Playable output)
        {
            output = Playable.Null;

            // Fast path：已初始化时直接复用 Runtime 输出。
            // 说明：StateAnimationMixCalculator.InitializeRuntime 对重复初始化会 return true 但不回填 output。
            // 因此这里必须显式从 runtime 获取 outputPlayable，既更快也更稳。
            var runtime = _animationRuntime;
            if (runtime != null && runtime.IsInitialized)
            {
                if (_calculatorCached == null)
                {
                    // 仅在必要时补齐缓存，避免影响后续 UpdateAnimationWeights 等路径。
                    ResolveCalculatorForPlayableCreation();
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_calculatorCached != null && runtime.BoundCalculatorKind != _calculatorCached.CalculatorKind)
                {
                    throw new InvalidOperationException(
                        $"CreatePlayable: runtime 已初始化，但 BoundCalculatorKind={runtime.BoundCalculatorKind} 与当前 calculator.kind={_calculatorCached.CalculatorKind} 不一致。" +
                        " 这通常表示运行时切换了 Calculator，但未先 DestroyPlayable() 重建运行时结构。"
                    );
                }
#endif

                output = runtime.GetOutputPlayable();
                return output.IsValid();
            }

            var calculator = ResolveCalculatorForPlayableCreation();
            if (calculator == null) return false;

            runtime = GetOrCreateAnimationRuntime(calculator);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runtime == null) throw new InvalidOperationException("CreatePlayable: runtime 为空（CreateRuntimeData 返回了 null）");
#else
            if (runtime == null) return false;
#endif

            // GetOrCreate 后仍可能已初始化（例如外部提前初始化或复用对象池）。
            if (runtime.IsInitialized)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_calculatorCached != null && runtime.BoundCalculatorKind != _calculatorCached.CalculatorKind)
                {
                    throw new InvalidOperationException(
                        $"CreatePlayable: runtime 已初始化，但 BoundCalculatorKind={runtime.BoundCalculatorKind} 与当前 calculator.kind={_calculatorCached.CalculatorKind} 不一致。" +
                        " 这通常表示运行时切换了 Calculator，但未先 DestroyPlayable() 重建运行时结构。"
                    );
                }
#endif
                output = runtime.GetOutputPlayable();
                return output.IsValid();
            }

            Playable created = Playable.Null;
            if (!TryInitializePlayableRuntime(calculator, runtime, graph, ref created))
                return false;

            // 初始化成功但未显式回填 created 时，回退从 runtime 获取。
            if (!created.IsValid())
            {
                created = runtime.GetOutputPlayable();
                if (!created.IsValid())
                    return false;
            }
            output = created;
            return output.IsValid();
        }

        /// <summary>
        /// 解析用于创建 Playable 的 Calculator。
        /// 默认优先使用缓存字段，其次从 stateSharedData.animationConfig 中回退获取，并回写缓存。
        /// </summary>
        protected StateAnimationMixCalculator ResolveCalculatorForPlayableCreation()
        {
            var calculator = _calculatorCached;
            if (calculator != null) return calculator;

            var shared = stateSharedData;
            var animConfig = shared != null ? shared.animationConfig : null;
            calculator = animConfig != null ? animConfig.calculator : null;
            if (calculator != null)
            {
                // 只要解析到了，就回写缓存，避免后续重复回退查找。
                _calculatorCached = calculator;
            }
            return calculator;
        }

        /// <summary>
        /// 创建/获取动画 Runtime。
        /// 默认每个 StateBase 生命周期只创建一次。
        /// </summary>
        protected AnimationCalculatorRuntime GetOrCreateAnimationRuntime(StateAnimationMixCalculator calculator)
        {
            if (_animationRuntime == null)
            {
                _animationRuntime = calculator.CreateRuntimeData();
            }

            // 绑定 owner（用于计算器在低频事件时同步运行时阶段等信息）
            if (_animationRuntime != null)
            {
                _animationRuntime.ownerState = this;
            }
            return _animationRuntime;
        }

        /// <summary>
        /// 委托 Calculator 初始化 Runtime，并产出可连接到图的 Playable。
        /// </summary>
        protected bool TryInitializePlayableRuntime(
            StateAnimationMixCalculator calculator,
            AnimationCalculatorRuntime runtime,
            PlayableGraph graph,
            ref Playable created)
        {
            return calculator.InitializeRuntime(runtime, graph, ref created);
        }

        /// <summary>
        /// 状态进入后立即执行一次动画权重更新 — 内部混合权重直接到位（无平滑过渡）。
        /// 不推进 hasEnterTime / 进度追踪，仅让计算器内部权重（如 BlendTree 各 Clip 的混合比例）
        /// 根据当前 Context 参数直接对齐，避免首帧空白或权重归零的视觉跳变。
        /// 外部管线权重（FadeIn）由状态机独立驱动，不受此影响。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ImmediateUpdateAnimationWeights(StateMachineContext context)
            => ImmediateUpdateAnimationRuntime(context);

        public void ImmediateUpdateAnimationRuntime(StateMachineContext context)
        {
            if (!_hasAnimationCached) return;

            var runtime = _animationRuntime;
            var calculator = _calculatorCached;
            if (runtime == null || !runtime.IsInitialized || calculator == null) return;

            // 同步当前管线权重到 runtime（FadeIn 可能已将其设为 0）
            runtime.totalWeight = _playableWeight;

            // 立即更新 — 内部权重无过渡
            calculator.ImmediateUpdate(runtime, context);
        }

        private void MarkAutoExitFromAnimation(string reason)
        {
            if (_shouldAutoExitFromAnimation) return;
            _shouldAutoExitFromAnimation = true;
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            var dbg = StateMachineDebugSettings.Instance;
            if (dbg != null && dbg.IsStateTransitionEnabled)
            {
                var shared = stateSharedData;
                var stateName = GetStateNameSafe();
                dbg.LogStateTransition($"[StateBase] AutoExitFlag=TRUE | State={stateName} | Reason={reason}\n{Environment.StackTrace}");
            }
#endif
#endif
        }

        public void UpdateAnimationRuntime(StateMachineContext context, float deltaTime)
        {
            // 更新运行时数据
            if (!_hasAnimationCached) return;

            var runtime = _animationRuntime;
            if (runtime == null || !runtime.IsInitialized) return;

            var calculator = _calculatorCached;
            if (calculator == null) return;

            // ★ 修复：进度更新只调用一次（之前被调用了两次导致2x速度推进！）
            if (_needsProgressTrackingCached)
            {
                UpdateRuntimeProgress(deltaTime);
            }

            runtime.totalWeight = _playableWeight;
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            StateMachineDebugSettings.Instance.LogAnimationBlend(
                $"State={_basicConfigCached != null ? _basicConfigCached.stateName : \"NULL\"} 进行权重更新");
#endif
#endif
            calculator.UpdateWeights(runtime, context, deltaTime);

            // ★ AnimationClip反向控制：检测动画是否播放完毕（仅针对UntilAnimationEnd模式）
            if (_durationModeCached == StateDurationMode.UntilAnimationEnd)
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

            // normalizedProgress 可能涉及除法/分支：按需且最多计算一次。
            float np = 0f;
            bool hasNp = false;

            // ★ 更新IK权重平滑过渡
            if (runtime.ik.enabled)
            {
                var animConfig = _animConfigCached;
                var resolved = _resolvedRuntimeConfig;
                float target;
                if (!_ikActive)
                {
                    // IK被显式禁用：仍然要把targetWeight推向0，才能让ik.weight平滑衰减。
                    target = 0f;
                }
                else
                {
                    // ★ 深度集成：ResolvedConfig的IK目标权重与配置曲线相乘（更自然的进出手/收招权重）
                    // ResolvedConfig.ikTargetWeight 已包含：默认值 + Phase覆盖 + Main阶段覆盖
                    target = resolved != null ? resolved.ikTargetWeight : runtime.ik.targetWeight;
                    if (_enableIKCached && animConfig != null)
                    {
                        if (!hasNp) { np = normalizedProgress; hasNp = true; }
                        target *= animConfig.EvaluateIKTargetWeightMultiplier(np);
                    }
                }

                SetIKTargetWeight(target);

                float ikSmooth = animConfig != null ? animConfig.ikSmoothTime : 0f;
                runtime.UpdateIKWeight(ikSmooth, deltaTime);

                // ★ 动态IK目标追踪：如果配置了Transform引用，每帧同步位置
                if (_ikActive && _enableIKCached && animConfig != null && animConfig.HasDynamicIKTargets())
                {
                    animConfig.UpdateIKTargetsFromConfig(runtime);
                }
            }

            // ★ 更新MatchTarget进度检查
            if (runtime.matchTarget.active && !runtime.matchTarget.completed)
            {
                if (!hasNp) { np = normalizedProgress; hasNp = true; }
                if (np > runtime.matchTarget.endTime)
                {
                    runtime.CompleteMatchTarget();
                    OnMatchTargetCompleted();
                }
            }
        }

        /// <summary>
        /// 检测动画播放完毕状态（AnimationClip反向控制）
        /// 当动画播放进度>=1.0且为非循环模式时，标记应该退出
        /// </summary>
        private void CheckAnimationCompletion()
        {
            var runtime = _animationRuntime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runtime == null) throw new InvalidOperationException("CheckAnimationCompletion: runtime 为空（调用方应保证有动画且已初始化）");
            if (!runtime.IsInitialized) throw new InvalidOperationException("CheckAnimationCompletion: runtime 未初始化（调用方应保证有动画且已初始化）");
#else
            if (runtime == null || !runtime.IsInitialized) return;
#endif

            // 统一容错：按 60fps 估算 1 帧余量
            const double kFrameToleranceSeconds = 1.0 / 60.0;

            // 情况1：SimpleClip（最准确，直接用 Playable 的 time/duration 判定）
            if (runtime.singlePlayable.IsValid())
            {
                Playable playable = runtime.singlePlayable;
                if (playable.GetPlayableType() == typeof(AnimationClipPlayable))
                {
                    var clipPlayable = (AnimationClipPlayable)playable;
                    var clip = clipPlayable.GetAnimationClip();
                    if (clip != null && !clip.isLooping)
                    {
                        double duration = playable.GetDuration();
                        if (!double.IsInfinity(duration) && duration > 0.001)
                        {
                            if (playable.GetTime() >= duration - kFrameToleranceSeconds)
                            {
                                MarkAutoExitFromAnimation("SingleClipComplete");
                            }
                        }
                    }
                }
                return;
            }

            // 情况2：其它混合结构（强制限定）：依赖 Calculator 提供标准时长
            var calculator = _calculatorCached;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (calculator == null)
                throw new InvalidOperationException("CheckAnimationCompletion: calculator 为空（应在注册/创建期保证缓存有效）");
#else
            if (calculator == null) return;
#endif

            float standardDuration = calculator.GetStandardDuration(runtime);
            if (standardDuration > 0.001f && !float.IsInfinity(standardDuration))
            {
                // 保护：循环Clip不应触发 UntilAnimationEnd 的自动退出。
                // 对于混合器：优先用 GetCurrentClip 判断“当前主导Clip”是否循环。
                var currentClip = calculator.GetCurrentClip(runtime);
                if (currentClip != null && currentClip.isLooping)
                {
                    return;
                }

                // 优先用输出Playable的时间（更接近图内真实推进），拿不到再回退到 hasEnterTime。
                double t;
                var output = runtime.GetOutputPlayable();
                if (output.IsValid()) t = output.GetTime();
                else t = hasEnterTime;

                if (t >= standardDuration - kFrameToleranceSeconds)
                {
                    MarkAutoExitFromAnimation("StandardDurationComplete");
                }
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 强制限定：复杂混合器若未提供标准时长，会导致 UntilAnimationEnd 无法可靠退出。
            throw new InvalidOperationException(
                $"CheckAnimationCompletion: 当前Calculator={calculator.GetType().Name} 未提供有效标准时长（GetStandardDuration={standardDuration}）。" +
                " UntilAnimationEnd 仅支持 SimpleClip / Sequential(由sequenceCompleted驱动) / 或提供标准时长的Calculator。"
            );
#else
            // Release：拿不到标准时长时，不做自动退出判定。
            return;
#endif
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
        public void DestroyPlayable()
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
        public AnimationClip GetCurrentClip()
        {
            var runtime = _animationRuntime;
            if (runtime == null || !runtime.IsInitialized) return null;

            var calculator = _calculatorCached;
            if (calculator == null) return null;

            return calculator.GetCurrentClip(runtime);
        }

        /// <summary>
        /// 检查状态是否应该自动退出（根据持续时间模式）
        /// ★ 优化：减少冗余的属性访问和类型转换
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <returns>是否应该退出</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldAutoExit(float currentTime)
        {
            var config = _basicConfigCached;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (config == null) throw new InvalidOperationException("StateBase.basicConfig 缓存为空（应在注册/初始化期保证）");
#else
            if (config == null) return false;
#endif

            // Hot path：按需计算 elapsedTime（Infinite / UntilAnimationEnd(无fallback) 不需要减法）。
            var mode = _durationModeCached;

            if (mode == StateDurationMode.Infinite)
                return false;

            if (mode == StateDurationMode.Timed)
            {
                float elapsedTime = currentTime - activationTime;
                if (elapsedTime >= config.timedDuration)
                {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                    StateMachineDebugSettings.Instance.LogStateTransition(
                        $"[StateBase] ShouldAutoExit=TRUE (Timed) | State={config.stateName} | Elapsed={elapsedTime:F3} | Duration={config.timedDuration:F3}");
#endif
#endif
                    return true;
                }
                return false;
            }

            if (mode == StateDurationMode.UntilAnimationEnd)
            {
                // ★ 检查动画是否播放完毕（优先使用AnimationClip反向控制的标志）
                if (_shouldAutoExitFromAnimation)
                {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                    StateMachineDebugSettings.Instance.LogStateTransition(
                        $"[StateBase] ShouldAutoExit=TRUE (AnimFlag) | State={config.stateName}");
#endif
#endif
                    return true;
                }

                if (!config.enableClipLengthFallback)
                    return false;

                // 备用逻辑：通过Clip长度计算
                float elapsedTime = currentTime - activationTime;
                return CheckClipLengthFallbackExit(elapsedTime);
            }

            return false;
        }

        /// <summary>
        /// ★ 抽取的Clip长度回退退出逻辑（降低ShouldAutoExit复杂度）
        /// ★ 修复：SequentialClipMixer使用GetStandardDuration（总时长），而非当前阶段Clip长度
        ///   之前GetCurrentClip返回当前阶段的Clip，导致用总逝去时间 vs 单阶段时长比较 → 提前退出
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckClipLengthFallbackExit(float elapsedTime)
        {
            var runtime = _animationRuntime;
            if (runtime == null || !runtime.IsInitialized) return false;

            var calculator = _calculatorCached;
            if (calculator == null) return false;

            // ★ 优先使用GetStandardDuration（对SequentialClipMixer返回Entry+Main+Exit总时长）
            float standardDuration = calculator.GetStandardDuration(runtime);
            if (standardDuration > 0.001f && !float.IsInfinity(standardDuration))
            {
                if (elapsedTime >= standardDuration)
                {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                    var cfg = _basicConfigCached;
                    StateMachineDebugSettings.Instance.LogStateTransition(
                        $"[StateBase] ShouldAutoExit=TRUE (StandardDuration) | State={(cfg != null ? cfg.stateName : \"NULL\")} | Elapsed={elapsedTime:F3} | Duration={standardDuration:F3}");
#endif
#endif
                    return true;
                }
                return false;
            }

            // 回退：单Clip模式
            var clip = calculator.GetCurrentClip(runtime);
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
                var cfg = _basicConfigCached;
                StateMachineDebugSettings.Instance.LogStateTransition(
                    $"[StateBase] ShouldAutoExit=TRUE (ClipLength) | State={(cfg != null ? cfg.stateName : \"NULL\")} | Clip={clip.name} | Elapsed={elapsedTime:F3} | AnimDuration={animDuration:F3} | Speed={speed:F3}");
#endif
#endif
                return true;
            }
            return false;
        }

        #endregion
    }
}
