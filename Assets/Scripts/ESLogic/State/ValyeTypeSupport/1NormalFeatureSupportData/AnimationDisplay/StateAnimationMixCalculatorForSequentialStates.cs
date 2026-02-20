using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 顺序状态机计算器 - 支持固定顺序的状态转换
    /// 
    /// 典型应用：
    /// - 跳跃序列：起跳 → 上升 → 下落 → 落地
    /// - 攻击连招：轻击1 → 轻击2 → 重击
    /// - 翻滚序列：准备 → 翻滚 → 恢复
    /// 
    /// 设计特点：
    /// 1. 每个阶段支持2个Clip混合（如上升阶段可以有"快速上升"和"慢速上升"）
    /// 2. 自动时序控制（基于时间或参数触发）
    /// 3. 支持循环模式和单次模式
    /// 4. 零GC设计，使用享元模式
    /// 
    /// 使用场景：
    /// - Jump: 起跳(0.1s) → 上升(变长) → 下落(变长) → 落地(0.2s)
    /// - Attack: 前摇(0.15s) → 攻击(0.3s) → 后摇(0.25s)
    /// - Dodge: 启动(0.05s) → 无敌(0.4s) → 恢复(0.15s)
    /// </summary>
    [Serializable, TypeRegistryItem("高级·序列状态播放器")]
    public class StateAnimationMixCalculatorForSequentialStates : StateAnimationMixCalculator
    {
        public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.SequentialStates;

        private const float WeightEpsilon = 0.0001f;
        private const float DefaultTriggerThreshold = 0.5f;

        public enum PhaseTransitionCompare
        {
            Greater,
            Less
        }

        [Serializable]
        public struct PhaseTransitionCondition
        {
            [LabelText("启用条件")]
            public bool enable;

            [LabelText("参数")]
            [Tooltip("用于比较的 float 参数")]
            public StateParameter parameter;

            [LabelText("比较")]
            public PhaseTransitionCompare compare;

            [LabelText("阈值")]
            public float threshold;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Check(in StateMachineContext context)
            {
                if (!enable) return false;
                float v = context.GetFloat(parameter, 0f);
                return compare == PhaseTransitionCompare.Greater ? (v >= threshold) : (v <= threshold);
            }
        }

        protected override string GetUsageHelp()
        {
            return "适用：固定顺序多阶段动作（跳跃/连招/翻滚）。\n" +
                   "必填：阶段列表（阶段名 + 主Clip 或 子计算器）。\n" +
                   "可选：主/次Clip混合、切换条件、播完切换、自动切换、过渡时间。";
        }

        protected override string GetCalculatorDisplayName()
        {
            return "高级·序列状态播放器";
        }
        /// <summary>
        /// 顺序阶段定义
        /// </summary>
        [Serializable]
        public struct SequentialPhase
        {
            [LabelText("阶段名称")]
            public string phaseName;

            [SerializeReference, LabelText("阶段计算器"), Tooltip("可选：用一个完整的混合/播放方案作为本阶段输出。\n设置后：本阶段将以该方案的输出为准；本阶段下方的主/次动画与其混合设置不再生效。")]
            public StateAnimationMixCalculator phaseCalculator;
            
            [LabelText("主Clip")]
            [Tooltip("本阶段的主要动画（未设置【阶段计算器】时生效）")]
            [ShowIf("@phaseCalculator == null")]
            public AnimationClip primaryClip;

            [LabelText("启用次Clip混合")]
            [Tooltip("仅在未设置【阶段计算器】且同时配置主/次动画时生效：启用后，使用【混合参数】在主/次动画之间混合。\n关闭时不会创建 2 输入混合器，也不会更新混合权重。")]
            [ShowIf("@phaseCalculator == null")]
            public bool enableSecondaryClipBlend;
            
            [LabelText("次Clip（可选）")]
            [Tooltip("可选的次要动画（仅在未设置【阶段计算器】且启用主次混合时生效）")]
            [ShowIf("@phaseCalculator == null && enableSecondaryClipBlend")]
            public AnimationClip secondaryClip;
            
            [LabelText("混合参数")]
            [Tooltip("控制主次Clip混合的参数（0=主，1=次）")]
            [ShowIf("@phaseCalculator == null && enableSecondaryClipBlend")]
            public StateParameter blendParameter;
            
            [LabelText("最小时长")]
            [Tooltip("该阶段最短持续时间（秒）。在此之前不会发生：切换条件/播完切换/自动切换")]
            [Range(0f, 5f)]
            public float minDuration;
            
            [LabelText("最大时长")]
            [Tooltip("该阶段最长持续时间（秒）。>0 时达到会强制切到下一阶段；0 表示无限制")]
            [Range(0f, 10f)]
            public float maxDuration;
            
            [LabelText("自动切换")]
            [Tooltip("启用后：当本阶段持续时间达到【最小时长】时，在下一次更新会立刻进入下一阶段。\n特点：不需要任何参数/条件，也不会等待到【最大时长】。\n说明：【最大时长】（>0）始终是硬上限，到点一定会强制进入下一阶段（可作为兜底）。")]
            public bool autoTransition;

            [LabelText("播完自动切换")]
            [Tooltip("启用后：当本阶段持续时间达到【最小时长】并且输出动画播放到末尾时，自动进入下一阶段。\n注意：循环动画或无固定长度的输出可能无法判断“末尾”，此时不会触发；可用【最大时长】作为硬上限兜底。")]
            public bool autoTransitionOnAnimationEnd;
            
            [LabelText("切换参数")]
            [HideInInspector]
            [Tooltip("(兼容旧字段) 旧版“触发参数”。已统一迁移到【切换条件】中")]
            public StateParameter transitionTrigger;

            [LabelText("触发阈值")]
            [HideInInspector]
            [Tooltip("(兼容旧字段) 旧版“触发阈值”。已统一迁移到【切换条件】中")]
            public float transitionTriggerThreshold;

            [LabelText("切换条件（可选）")]
            [Tooltip("当本阶段持续时间达到【最小时长】后，若条件成立则进入下一阶段。仅保留这一套手动切换规则")]
            public PhaseTransitionCondition transitionCondition;
        }
        
        // ==================== 配置数据（享元） ====================

        [LabelText("阶段列表")]
        [ListDrawerSettings(ShowFoldout = true, NumberOfItemsPerPage = 5)]
        public SequentialPhase[] phases = new SequentialPhase[0];
        
        [LabelText("循环模式")]
        [Tooltip("到达最后一个阶段后是否循环回第一个")]
        public bool loopMode = false;
        
        [LabelText("过渡时间")]
        [Tooltip("阶段切换时的CrossFade时间")]
        [Range(0f, 0.5f)]
        public float transitionDuration = 0.1f;
        
        [LabelText("混合平滑时间")]
        [Tooltip("同阶段内主次Clip混合的平滑时间")]
        [Range(0f, 0.5f)]
        public float blendSmoothTime = 0.05f;
        
        private bool _isCalculatorInitialized;

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器快速初始化：跳跃序列
        /// </summary>
        [Button("初始化跳跃序列（4阶段）"), PropertyOrder(-1)]
        private void InitializeJumpSequence()
        {
            if (phases != null && phases.Length > 0)
            {
                if (!UnityEditor.EditorUtility.DisplayDialog(
                    "确认初始化",
                    "当前已有阶段，是否覆盖为跳跃序列？\n序列：起跳 → 上升 → 下落 → 落地",
                    "确认覆盖", "取消"))
                {
                    return;
                }
            }

            phases = new SequentialPhase[4]
            {
                new SequentialPhase
                {
                    phaseName = "起跳 (JumpStart)",
                    primaryClip = null,
                    secondaryClip = null,
                    blendParameter = "JumpPower",
                    minDuration = 0.1f,
                    maxDuration = 0.15f,
                    autoTransition = true,
                    transitionTrigger = StateDefaultFloatParameter.None
                },
                new SequentialPhase
                {
                    phaseName = "上升 (Rising)",
                    primaryClip = null,
                    secondaryClip = null,
                    blendParameter = "VerticalSpeed",
                    minDuration = 0.2f,
                    maxDuration = 2f,
                    autoTransition = false,
                    transitionTrigger = "VelocityNegative"  // 速度变负时切换
                },
                new SequentialPhase
                {
                    phaseName = "下落 (Falling)",
                    primaryClip = null,
                    secondaryClip = null,
                    blendParameter = "FallSpeed",
                    minDuration = 0.15f,
                    maxDuration = 5f,
                    autoTransition = false,
                    transitionTrigger = "OnGrounded"  // 接地时切换
                },
                new SequentialPhase
                {
                    phaseName = "落地 (Landing)",
                    primaryClip = null,
                    secondaryClip = null,
                    blendParameter = "ImpactForce",
                    minDuration = 0.2f,
                    maxDuration = 0.3f,
                    autoTransition = true,
                    transitionTrigger = StateDefaultFloatParameter.None
                }
            };
            
            loopMode = false;
            transitionDuration = 0.05f;
            blendSmoothTime = 0.05f;

            Debug.Log("[SequentialStates] 已初始化跳跃序列（4阶段）");
        }
        
        /// <summary>
        /// 编辑器快速初始化：攻击连招
        /// </summary>
        [Button("初始化攻击连招（3阶段）"), PropertyOrder(-1)]
        private void InitializeAttackCombo()
        {
            if (phases != null && phases.Length > 0)
            {
                if (!UnityEditor.EditorUtility.DisplayDialog(
                    "确认初始化",
                    "当前已有阶段，是否覆盖为攻击连招？\n序列：前摇 → 攻击 → 后摇",
                    "确认覆盖", "取消"))
                {
                    return;
                }
            }

            phases = new SequentialPhase[3]
            {
                new SequentialPhase
                {
                    phaseName = "前摇 (Windup)",
                    primaryClip = null,
                    secondaryClip = null,
                    blendParameter = "AttackCharge",
                    minDuration = 0.15f,
                    maxDuration = 0.2f,
                    autoTransition = true,
                    transitionTrigger = StateDefaultFloatParameter.None
                },
                new SequentialPhase
                {
                    phaseName = "攻击 (Strike)",
                    primaryClip = null,
                    secondaryClip = null,
                    blendParameter = "AttackPower",
                    minDuration = 0.3f,
                    maxDuration = 0.4f,
                    autoTransition = true,
                    transitionTrigger = StateDefaultFloatParameter.None
                },
                new SequentialPhase
                {
                    phaseName = "后摇 (Recovery)",
                    primaryClip = null,
                    secondaryClip = null,
                    blendParameter = "RecoverySpeed",
                    minDuration = 0.25f,
                    maxDuration = 0.35f,
                    autoTransition = true,
                    transitionTrigger = StateDefaultFloatParameter.None
                }
            };
            
            loopMode = false;
            transitionDuration = 0.05f;
            blendSmoothTime = 0.1f;

            Debug.Log("[SequentialStates] 已初始化攻击连招（3阶段）");
        }
#endif

        /// <summary>
        /// 计算器初始化
        /// </summary>
        public override void InitializeCalculator()
        {
            if (_isCalculatorInitialized || phases == null || phases.Length == 0)
                return;

            // 验证配置
            for (int i = 0; i < phases.Length; i++)
            {
                var p = phases[i];

                // 统一切换规则：迁移旧 trigger(+threshold) 到 transitionCondition
                if (!p.transitionCondition.enable && HasTransitionTrigger(p))
                {
                    p.transitionCondition.enable = true;
                    p.transitionCondition.parameter = p.transitionTrigger;
                    p.transitionCondition.compare = PhaseTransitionCompare.Greater;
                    p.transitionCondition.threshold = p.transitionTriggerThreshold > 0f ? p.transitionTriggerThreshold : DefaultTriggerThreshold;

                    p.transitionTrigger = StateDefaultFloatParameter.None;
                    p.transitionTriggerThreshold = 0f;
                }

                if (p.phaseCalculator == null && p.primaryClip == null && p.secondaryClip == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[SequentialStates] 阶段{i} ({p.phaseName}) 缺少主Clip");
#endif
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (p.phaseCalculator != null && (p.primaryClip != null || p.secondaryClip != null))
                {
                    Debug.LogWarning($"[SequentialStates] 阶段{i} ({p.phaseName}) 同时设置了阶段计算器和动画片段：动画片段与其混合配置将被忽略");
                }
#endif
                if (p.phaseCalculator != null)
                {
                    p.phaseCalculator.InitializeCalculator();
                }
                if (p.minDuration < 0f)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError($"[SequentialStates] 阶段{i} ({p.phaseName}) minDuration不能为负");
#endif
                    p.minDuration = 0f;
                }

                phases[i] = p;
            }

            _isCalculatorInitialized = true;
        }

        protected override bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
        {
            InitializeCalculator();

            if (phases == null || phases.Length == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SequentialStates] 阶段列表为空");
#endif
                return false;
            }

            runtime.mixer = AnimationMixerPlayable.Create(graph, phases.Length);

            // 初始化阶段数据
            runtime.phaseRuntimes = new AnimationCalculatorRuntime[phases.Length];
            runtime.phaseOutputs = new Playable[phases.Length];
            runtime.phaseMixers = new AnimationMixerPlayable[phases.Length];
            runtime.phasePrimaryPlayables = new AnimationClipPlayable[phases.Length];
            runtime.phaseSecondaryPlayables = new AnimationClipPlayable[phases.Length];
            runtime.phaseUsesCalculator = new bool[phases.Length];
            runtime.phaseBlendWeights = new float[phases.Length];
            runtime.phaseBlendVelocities = new float[phases.Length];

            for (int i = 0; i < phases.Length; i++)
            {
                var phase = phases[i];

                if (phase.phaseCalculator != null)
                {
                    runtime.phaseUsesCalculator[i] = true;
                    runtime.phaseRuntimes[i] = phase.phaseCalculator.CreateRuntimeData();

                    Playable phaseOutput = Playable.Null;
                    bool ok = phase.phaseCalculator.InitializeRuntime(runtime.phaseRuntimes[i], graph, ref phaseOutput);
                    if (ok && phaseOutput.IsValid())
                    {
                        runtime.phaseOutputs[i] = phaseOutput;
                        graph.Connect(phaseOutput, 0, runtime.mixer, i);
                    }
                    else
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogWarning($"[SequentialStates] 阶段{i} ({phase.phaseName}) 子计算器初始化失败");
#endif
                    }
                }
                else
                {
                    runtime.phaseUsesCalculator[i] = false;

                    // 规则：
                    // - primaryClip存在：可选 secondaryClip 做本地2输入混合
                    // - primaryClip为空但 secondaryClip存在：将 secondaryClip 视为唯一输出（避免空输出）
                    if (phase.primaryClip != null)
                    {
                        if (phase.enableSecondaryClipBlend && phase.secondaryClip != null)
                        {
                            runtime.phaseMixers[i] = AnimationMixerPlayable.Create(graph, 2);

                            runtime.phasePrimaryPlayables[i] = AnimationClipPlayable.Create(graph, phase.primaryClip);
                            graph.Connect(runtime.phasePrimaryPlayables[i], 0, runtime.phaseMixers[i], 0);

                            runtime.phaseSecondaryPlayables[i] = AnimationClipPlayable.Create(graph, phase.secondaryClip);
                            graph.Connect(runtime.phaseSecondaryPlayables[i], 0, runtime.phaseMixers[i], 1);

                            runtime.phaseMixers[i].SetInputWeight(0, 1f);
                            runtime.phaseMixers[i].SetInputWeight(1, 0f);

                            graph.Connect(runtime.phaseMixers[i], 0, runtime.mixer, i);
                        }
                        else
                        {
                            runtime.phasePrimaryPlayables[i] = AnimationClipPlayable.Create(graph, phase.primaryClip);
                            graph.Connect(runtime.phasePrimaryPlayables[i], 0, runtime.mixer, i);
                        }
                    }
                    else if (phase.secondaryClip != null)
                    {
                        runtime.phaseSecondaryPlayables[i] = AnimationClipPlayable.Create(graph, phase.secondaryClip);
                        graph.Connect(runtime.phaseSecondaryPlayables[i], 0, runtime.mixer, i);
                    }
                    else
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogWarning($"[SequentialStates] 阶段{i} ({phase.phaseName}) 无可用输出");
#endif
                    }
                }

                runtime.mixer.SetInputWeight(i, 0f);
            }

            // ★ 修复：使用专用字段代替currentWeights hack（之前currentWeights[0-2]
            //   存储阶段数据与DirectBlend冲突）
            runtime.sequencePhaseIndex = 0;  // 从第一个阶段开始
            runtime.sequencePhaseTime = 0f;
            runtime.sequenceTotalTime = 0f;
            runtime.sequenceCompleted = false;

            runtime.useSmoothing = blendSmoothTime > 0.001f;

            // 商业级稳健性：初始化时就把第0阶段权重置为1，避免某些路径缺少ImmediateUpdate导致首帧空输出。
            runtime.mixer.SetInputWeight(0, 1f);
            runtime.sequenceLastAppliedPhaseIndex = 0;
            runtime.sequenceLastAppliedPrevPhaseIndex = -1;
            runtime.sequenceLastAppliedPhaseWeight = 1f;
            runtime.sequenceLastAppliedPrevWeight = 0f;

            output = runtime.mixer;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyMixerWeightsOptimized(
            AnimationCalculatorRuntime runtime,
            int currentPhase,
            float currentWeight,
            int prevPhaseIndex,
            float prevWeight)
        {
            var mixer = runtime.mixer;

            int lastCurrent = runtime.sequenceLastAppliedPhaseIndex;
            int lastPrev = runtime.sequenceLastAppliedPrevPhaseIndex;

            // 清零不再使用的索引（注意：当 oldCurrent 变成 newPrev 时，不能清零）
            if (lastCurrent != currentPhase && lastCurrent != prevPhaseIndex && lastCurrent >= 0)
            {
                mixer.SetInputWeight(lastCurrent, 0f);
            }
            if (lastPrev != prevPhaseIndex && lastPrev != currentPhase && lastPrev >= 0)
            {
                mixer.SetInputWeight(lastPrev, 0f);
            }

            // 当前阶段
            if (currentPhase >= 0)
            {
                if (lastCurrent != currentPhase || Mathf.Abs(runtime.sequenceLastAppliedPhaseWeight - currentWeight) > WeightEpsilon)
                {
                    mixer.SetInputWeight(currentPhase, currentWeight);
                    runtime.sequenceLastAppliedPhaseWeight = currentWeight;
                }
            }

            // 上一阶段（过渡用）
            if (prevPhaseIndex >= 0)
            {
                if (lastPrev != prevPhaseIndex || Mathf.Abs(runtime.sequenceLastAppliedPrevWeight - prevWeight) > WeightEpsilon)
                {
                    mixer.SetInputWeight(prevPhaseIndex, prevWeight);
                    runtime.sequenceLastAppliedPrevWeight = prevWeight;
                }
            }
            else
            {
                runtime.sequenceLastAppliedPrevWeight = 0f;
            }

            runtime.sequenceLastAppliedPhaseIndex = currentPhase;
            runtime.sequenceLastAppliedPrevPhaseIndex = prevPhaseIndex;
        }

        public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
        {
            if (runtime == null || !runtime.mixer.IsValid() || phases == null || phases.Length == 0)
                return;

            // ★ 修复：使用专用字段代替currentWeights hack
            int currentPhase = runtime.sequencePhaseIndex;
            float phaseTime = runtime.sequencePhaseTime;
            float totalTime = runtime.sequenceTotalTime;

            // 更新时间
            phaseTime += deltaTime;
            totalTime += deltaTime;
            runtime.sequencePhaseTime = phaseTime;
            runtime.sequenceTotalTime = totalTime;

            // 检查是否需要切换阶段
            bool shouldTransition = false;
            if (currentPhase < phases.Length)
            { 
                var phase = phases[currentPhase];

                // 检查最大时长
                if (phase.maxDuration > 0f && phaseTime >= phase.maxDuration)
                {
                    shouldTransition = true;
                }
                // 检查自动切换
                else if (phase.autoTransition && phaseTime >= phase.minDuration)
                {
                    shouldTransition = true;
                }
                // 检查阈值条件
                else if (phaseTime >= phase.minDuration && phase.transitionCondition.Check(context))
                {
                    shouldTransition = true;
                }
                // 检查：动画播放一遍结束自动切换
                else if (phase.autoTransitionOnAnimationEnd && phaseTime >= phase.minDuration && IsPhaseAnimationEnded(runtime, currentPhase, phase))
                {
                    shouldTransition = true;
                }
            }

            // 执行阶段切换
            if (shouldTransition)
            {
                int prevPhase = currentPhase;
                currentPhase++;
                if (currentPhase >= phases.Length)
                {
                    if (loopMode)
                    {
                        currentPhase = 0;  // 循环回第一个阶段
                    }
                    else
                    {
                        // 非循环：达到末尾后，标记序列完成。
                        // 注意：这里的“完成”语义依赖 phase.maxDuration/autoTransition/trigger 的配置。
                        runtime.sequenceCompleted = true;
                        currentPhase = phases.Length - 1;  // 停留在最后一个阶段
                    }
                }
                if (currentPhase != prevPhase)
                {
                    runtime.sequencePrevPhase = prevPhase;
                    runtime.sequenceInTransition = transitionDuration > 0.001f;
                    runtime.sequenceTransitionTime = 0f;
                }
                runtime.sequencePhaseIndex = currentPhase;
                runtime.sequencePhaseTime = 0f;  // 重置阶段时间
            }

            // 更新阶段内部输出（当前与过渡中的上一个阶段）
            UpdatePhaseOutput(runtime, currentPhase, context, deltaTime);
            if (runtime.sequenceInTransition && runtime.sequencePrevPhase >= 0)
            {
                UpdatePhaseOutput(runtime, runtime.sequencePrevPhase, context, deltaTime);
            }

            // 计算阶段权重（支持过渡）
            float currentWeight = 1f;
            float prevWeight = 0f;
            int prevPhaseIndex = -1;

            if (runtime.sequenceInTransition)
            {
                runtime.sequenceTransitionTime += deltaTime;
                float t = transitionDuration > 0.001f
                    ? Mathf.Clamp01(runtime.sequenceTransitionTime / transitionDuration)
                    : 1f;

                currentWeight = t;
                prevWeight = 1f - t;
                prevPhaseIndex = runtime.sequencePrevPhase;

                if (t >= 1f)
                {
                    runtime.sequenceInTransition = false;
                    runtime.sequencePrevPhase = -1;
                    prevPhaseIndex = -1;
                    currentWeight = 1f;
                    prevWeight = 0f;
                }
            }

            ApplyMixerWeightsOptimized(runtime, currentPhase, currentWeight, prevPhaseIndex, prevWeight);
        }

        /// <summary>
        /// 序列型计算器的首帧立即更新 — 仅刷新当前阶段权重到位，不推进时间/不切换阶段。
        /// </summary>
        public override void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
        {
            if (runtime == null || !runtime.mixer.IsValid() || phases.Length == 0) return;

            // 不推进时间，只根据当前阶段索引设置权重
            int currentPhase = runtime.sequencePhaseIndex;
            for (int i = 0; i < phases.Length; i++)
            {
                float weight = (i == currentPhase) ? 1f : 0f;
                runtime.mixer.SetInputWeight(i, weight);
            }

            // 如果当前阶段有子计算器，对其也执行一次立即更新
            if (currentPhase >= 0 && currentPhase < phases.Length)
            {
                var phase = phases[currentPhase];
                if (phase.phaseCalculator != null && runtime.phaseRuntimes != null
                    && currentPhase < runtime.phaseRuntimes.Length && runtime.phaseRuntimes[currentPhase] != null)
                {
                    phase.phaseCalculator.ImmediateUpdate(runtime.phaseRuntimes[currentPhase], context);
                }
            }
        }

        public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
        {
            if (phases.Length == 0)
                return null;

            // ★ 修复：使用专用字段
            int currentPhase = runtime.sequencePhaseIndex;
            if (currentPhase >= 0 && currentPhase < phases.Length)
            {
                if (phases[currentPhase].phaseCalculator != null && runtime.phaseRuntimes != null)
                {
                    var phaseRuntime = runtime.phaseRuntimes[currentPhase];
                    if (phaseRuntime != null)
                        return phases[currentPhase].phaseCalculator.GetCurrentClip(phaseRuntime);
                }
                return phases[currentPhase].primaryClip;
            }

            return null;
        }

        public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
        {
            if (phases == null || phases.Length == 0)
                return false;

            int phaseIndex = clipIndex / 2;
            int localIndex = clipIndex % 2;

            if (phaseIndex < 0 || phaseIndex >= phases.Length)
            {
                Debug.LogError($"[SequentialStates] 索引越界: {clipIndex}");
                return false;
            }

            if (runtime.phaseUsesCalculator != null && runtime.phaseUsesCalculator[phaseIndex])
            {
                Debug.LogWarning($"[SequentialStates] 阶段{phaseIndex}使用子计算器，无法通过索引覆盖Clip");
                return false;
            }

            if (newClip == null)
            {
                Debug.LogError("[SequentialStates] 新Clip为null");
                return false;
            }

            if (localIndex == 0)
            {
                if (runtime.phasePrimaryPlayables == null || !runtime.phasePrimaryPlayables[phaseIndex].IsValid())
                {
                    Debug.LogError($"[SequentialStates] 主Clip Playable无效: phase={phaseIndex}");
                    return false;
                }

                var graph = runtime.phasePrimaryPlayables[phaseIndex].GetGraph();
                var oldSpeed = runtime.phasePrimaryPlayables[phaseIndex].GetSpeed();
                var oldTime = runtime.phasePrimaryPlayables[phaseIndex].GetTime();

                if (runtime.phaseMixers != null && runtime.phaseMixers[phaseIndex].IsValid())
                {
                    runtime.phaseMixers[phaseIndex].DisconnectInput(0);
                    runtime.phasePrimaryPlayables[phaseIndex].Destroy();

                    runtime.phasePrimaryPlayables[phaseIndex] = AnimationClipPlayable.Create(graph, newClip);
                    runtime.phasePrimaryPlayables[phaseIndex].SetSpeed(oldSpeed);
                    runtime.phasePrimaryPlayables[phaseIndex].SetTime(oldTime);
                    graph.Connect(runtime.phasePrimaryPlayables[phaseIndex], 0, runtime.phaseMixers[phaseIndex], 0);
                }
                else
                {
                    runtime.mixer.DisconnectInput(phaseIndex);
                    runtime.phasePrimaryPlayables[phaseIndex].Destroy();

                    runtime.phasePrimaryPlayables[phaseIndex] = AnimationClipPlayable.Create(graph, newClip);
                    runtime.phasePrimaryPlayables[phaseIndex].SetSpeed(oldSpeed);
                    runtime.phasePrimaryPlayables[phaseIndex].SetTime(oldTime);
                    graph.Connect(runtime.phasePrimaryPlayables[phaseIndex], 0, runtime.mixer, phaseIndex);
                }
            }
            else
            {
                if (runtime.phaseSecondaryPlayables == null || !runtime.phaseSecondaryPlayables[phaseIndex].IsValid())
                {
                    Debug.LogError($"[SequentialStates] 次Clip Playable无效: phase={phaseIndex}");
                    return false;
                }

                if (runtime.phaseMixers == null || !runtime.phaseMixers[phaseIndex].IsValid())
                {
                    Debug.LogError($"[SequentialStates] 阶段{phaseIndex}不存在次Clip Mixer");
                    return false;
                }

                var graph = runtime.phaseSecondaryPlayables[phaseIndex].GetGraph();
                var oldSpeed = runtime.phaseSecondaryPlayables[phaseIndex].GetSpeed();
                var oldTime = runtime.phaseSecondaryPlayables[phaseIndex].GetTime();

                runtime.phaseMixers[phaseIndex].DisconnectInput(1);
                runtime.phaseSecondaryPlayables[phaseIndex].Destroy();

                runtime.phaseSecondaryPlayables[phaseIndex] = AnimationClipPlayable.Create(graph, newClip);
                runtime.phaseSecondaryPlayables[phaseIndex].SetSpeed(oldSpeed);
                runtime.phaseSecondaryPlayables[phaseIndex].SetTime(oldTime);
                graph.Connect(runtime.phaseSecondaryPlayables[phaseIndex], 0, runtime.phaseMixers[phaseIndex], 1);
            }

            return true;
        }

        /// <summary>
        /// 重置到第一个阶段（外部调用）
        /// ★ 修复：使用专用字段
        /// </summary>
        public void ResetSequence(AnimationCalculatorRuntime runtime)
        {
            runtime.sequencePhaseIndex = 0;
            runtime.sequencePhaseTime = 0f;
            runtime.sequenceTotalTime = 0f;
            runtime.sequenceCompleted = false;
            runtime.sequencePrevPhase = -1;
            runtime.sequenceTransitionTime = 0f;
            runtime.sequenceInTransition = false;

            // 强制下一帧重新写入mixer权重
            runtime.sequenceLastAppliedPhaseIndex = -1;
            runtime.sequenceLastAppliedPrevPhaseIndex = -1;
            runtime.sequenceLastAppliedPhaseWeight = -1f;
            runtime.sequenceLastAppliedPrevWeight = -1f;
        }

        public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
        {
            // SequentialStates：阶段可能是变长的（maxDuration=0 表示无限制），因此这里只能给一个“标准/上限时长”。
            // 约定：
            // - loopMode: 无限
            // - 任意阶段 maxDuration<=0: 无法给出标准时长（返回0）
            // - 否则：sum(maxDuration)
            if (loopMode) return float.PositiveInfinity;
            if (phases == null || phases.Length == 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < phases.Length; i++)
            {
                float max = phases[i].maxDuration;
                if (max <= 0f) return 0f;
                sum += max;
            }
            return sum > 0f ? sum : 0f;
        }

        /// <summary>
        /// 强制跳转到指定阶段（外部调用）
        /// ★ 修复：使用专用字段
        /// </summary>
        public void JumpToPhase(AnimationCalculatorRuntime runtime, int phaseIndex)
        {
            if (phaseIndex >= 0 && phaseIndex < phases.Length)
            {
                runtime.sequencePhaseIndex = phaseIndex;
                runtime.sequencePhaseTime = 0f;
            }
            runtime.sequencePrevPhase = -1;
            runtime.sequenceTransitionTime = 0f;
            runtime.sequenceInTransition = false;

            // 强制下一帧重新写入mixer权重
            runtime.sequenceLastAppliedPhaseIndex = -1;
            runtime.sequenceLastAppliedPrevPhaseIndex = -1;
            runtime.sequenceLastAppliedPhaseWeight = -1f;
            runtime.sequenceLastAppliedPrevWeight = -1f;
        }

        private bool HasTransitionTrigger(SequentialPhase phase)
        {
            return phase.transitionTrigger.EnumValue != StateDefaultFloatParameter.None ||
                   !string.IsNullOrEmpty(phase.transitionTrigger.StringValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPlayableEnded(Playable playable)
        {
            if (!playable.IsValid()) return false;

            double duration = playable.GetDuration();
            if (duration <= 0d || double.IsInfinity(duration) || double.IsNaN(duration))
                return false;

            // Playable 时间精度用 double，这里给一个很小的容差
            return playable.GetTime() >= duration - 0.0001d;
        }

        private static bool IsPhaseAnimationEnded(AnimationCalculatorRuntime runtime, int phaseIndex, SequentialPhase phase)
        {
            if (runtime == null || phaseIndex < 0) return false;

            // 子计算器：优先看其输出 Playable 是否有可用 duration
            if (runtime.phaseUsesCalculator != null && phaseIndex < runtime.phaseUsesCalculator.Length && runtime.phaseUsesCalculator[phaseIndex])
            {
                if (runtime.phaseRuntimes != null && phaseIndex < runtime.phaseRuntimes.Length)
                {
                    var pr = runtime.phaseRuntimes[phaseIndex];
                    if (pr != null && IsPlayableEnded(pr.outputPlayable))
                        return true;
                }

                // duration 不可得时，保守：不自动切换
                return false;
            }

            // Clip 模式
            if (runtime.phaseMixers != null && phaseIndex < runtime.phaseMixers.Length && runtime.phaseMixers[phaseIndex].IsValid())
            {
                // 两段混合：等两段都播完再切（避免提前截断）
                bool primaryEnded = runtime.phasePrimaryPlayables != null && phaseIndex < runtime.phasePrimaryPlayables.Length
                    ? IsPlayableEnded(runtime.phasePrimaryPlayables[phaseIndex])
                    : false;

                bool secondaryEnded = runtime.phaseSecondaryPlayables != null && phaseIndex < runtime.phaseSecondaryPlayables.Length
                    ? IsPlayableEnded(runtime.phaseSecondaryPlayables[phaseIndex])
                    : false;

                // 若某一侧 clip 本身不存在，IsPlayableEnded 会返回 false；这里用配置兜底
                if (phase.primaryClip == null) primaryEnded = true;
                if (phase.secondaryClip == null) secondaryEnded = true;

                return primaryEnded && secondaryEnded;
            }

            // 单 Clip 直连
            if (runtime.phasePrimaryPlayables != null && phaseIndex < runtime.phasePrimaryPlayables.Length && runtime.phasePrimaryPlayables[phaseIndex].IsValid())
                return IsPlayableEnded(runtime.phasePrimaryPlayables[phaseIndex]);

            // secondary-only 直连
            if (runtime.phaseSecondaryPlayables != null && phaseIndex < runtime.phaseSecondaryPlayables.Length && runtime.phaseSecondaryPlayables[phaseIndex].IsValid())
                return IsPlayableEnded(runtime.phaseSecondaryPlayables[phaseIndex]);

            // 没有可用输出：不自动切换
            return false;
        }

        private void UpdatePhaseOutput(AnimationCalculatorRuntime runtime, int phaseIndex, in StateMachineContext context, float deltaTime)
        {
            if (phaseIndex < 0 || phaseIndex >= phases.Length)
                return;

            var phase = phases[phaseIndex];

            if (runtime.phaseUsesCalculator != null && runtime.phaseUsesCalculator[phaseIndex])
            {
                var phaseRuntime = runtime.phaseRuntimes[phaseIndex];
                if (phase.phaseCalculator != null && phaseRuntime != null)
                {
                    phase.phaseCalculator.UpdateWeights(phaseRuntime, context, deltaTime);
                }
                return;
            }

            if (!phase.enableSecondaryClipBlend || phase.secondaryClip == null)
                return;

            if (runtime.phaseMixers == null || !runtime.phaseMixers[phaseIndex].IsValid())
                return;

            float targetBlend = Mathf.Clamp01(context.GetFloat(phase.blendParameter, 0f));

            if (runtime.phaseBlendWeights == null || runtime.phaseBlendVelocities == null)
                return;

            if (runtime.useSmoothing)
            {
                float smoothSpeed = blendSmoothTime > 0.001f ? blendSmoothTime : 0.001f;
                runtime.phaseBlendWeights[phaseIndex] = Mathf.SmoothDamp(
                    runtime.phaseBlendWeights[phaseIndex],
                    targetBlend,
                    ref runtime.phaseBlendVelocities[phaseIndex],
                    smoothSpeed,
                    float.MaxValue,
                    deltaTime
                );
            }
            else
            {
                runtime.phaseBlendWeights[phaseIndex] = targetBlend;
            }

            float primaryWeight = 1f - runtime.phaseBlendWeights[phaseIndex];
            float secondaryWeight = runtime.phaseBlendWeights[phaseIndex];

            runtime.phaseMixers[phaseIndex].SetInputWeight(0, primaryWeight);
            runtime.phaseMixers[phaseIndex].SetInputWeight(1, secondaryWeight);
        }
    }
}
