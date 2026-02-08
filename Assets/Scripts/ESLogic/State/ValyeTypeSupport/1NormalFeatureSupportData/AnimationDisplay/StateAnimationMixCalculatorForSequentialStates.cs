using System;
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

        protected override string GetUsageHelp()
        {
             return "适用：固定顺序多阶段动作（跳跃/连招/翻滚）。\n" +
                 "必填：phases(phaseName+primaryClip 或 phaseCalculator)。\n" +
                 "可选：secondaryClip/触发参数/自动切换/过渡时长。";
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

            [SerializeReference, LabelText("阶段计算器"), Tooltip("可选：使用完整计算器作为该阶段输出")]
            public StateAnimationMixCalculator phaseCalculator;
            
            [LabelText("主Clip")]
            [Tooltip("该阶段的主要动画")]
            public AnimationClip primaryClip;
            
            [LabelText("次Clip（可选）")]
            [Tooltip("可选的次要动画，用于混合")]
            public AnimationClip secondaryClip;
            
            [LabelText("混合参数")]
            [Tooltip("控制主次Clip混合的参数（0=主，1=次）")]
            public StateParameter blendParameter;
            
            [LabelText("最小时长")]
            [Tooltip("该阶段最短持续时间（秒）")]
            [Range(0f, 5f)]
            public float minDuration;
            
            [LabelText("最大时长")]
            [Tooltip("该阶段最长持续时间（秒），0表示无限制")]
            [Range(0f, 10f)]
            public float maxDuration;
            
            [LabelText("自动切换")]
            [Tooltip("时间到达minDuration后自动进入下一阶段")]
            public bool autoTransition;
            
            [LabelText("切换参数")]
            [Tooltip("手动切换到下一阶段的触发参数")]
            public StateParameter transitionTrigger;
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
                if (phases[i].phaseCalculator == null && phases[i].primaryClip == null)
                {
                    Debug.LogWarning($"[SequentialStates] 阶段{i} ({phases[i].phaseName}) 缺少主Clip");
                }
                if (phases[i].phaseCalculator != null)
                {
                    phases[i].phaseCalculator.InitializeCalculator();
                }
                if (phases[i].minDuration < 0f)
                {
                    Debug.LogError($"[SequentialStates] 阶段{i} ({phases[i].phaseName}) minDuration不能为负");
                    phases[i].minDuration = 0f;
                }
            }

            _isCalculatorInitialized = true;
        }

        protected override bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
        {
            InitializeCalculator();

            if (phases == null || phases.Length == 0)
            {
                Debug.LogError("[SequentialStates] 阶段列表为空");
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
                        Debug.LogWarning($"[SequentialStates] 阶段{i} ({phase.phaseName}) 子计算器初始化失败");
                    }
                }
                else
                {
                    runtime.phaseUsesCalculator[i] = false;
                    if (phase.secondaryClip != null)
                    {
                        runtime.phaseMixers[i] = AnimationMixerPlayable.Create(graph, 2);

                        if (phase.primaryClip != null)
                        {
                            runtime.phasePrimaryPlayables[i] = AnimationClipPlayable.Create(graph, phase.primaryClip);
                            graph.Connect(runtime.phasePrimaryPlayables[i], 0, runtime.phaseMixers[i], 0);
                        }

                        runtime.phaseSecondaryPlayables[i] = AnimationClipPlayable.Create(graph, phase.secondaryClip);
                        graph.Connect(runtime.phaseSecondaryPlayables[i], 0, runtime.phaseMixers[i], 1);

                        runtime.phaseMixers[i].SetInputWeight(0, 1f);
                        runtime.phaseMixers[i].SetInputWeight(1, 0f);

                        graph.Connect(runtime.phaseMixers[i], 0, runtime.mixer, i);
                    }
                    else if (phase.primaryClip != null)
                    {
                        runtime.phasePrimaryPlayables[i] = AnimationClipPlayable.Create(graph, phase.primaryClip);
                        graph.Connect(runtime.phasePrimaryPlayables[i], 0, runtime.mixer, i);
                    }
                    else
                    {
                        Debug.LogWarning($"[SequentialStates] 阶段{i} ({phase.phaseName}) 无可用输出");
                    }
                }

                runtime.mixer.SetInputWeight(i, 0f);
            }

            // ★ 修复：使用专用字段代替currentWeights hack（之前currentWeights[0-2]
            //   存储阶段数据与DirectBlend冲突）
            runtime.sequencePhaseIndex = 0;  // 从第一个阶段开始
            runtime.sequencePhaseTime = 0f;
            runtime.sequenceTotalTime = 0f;

            runtime.useSmoothing = blendSmoothTime > 0.001f;

            output = runtime.mixer;
            return true;
        }

        public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
        {
            if (!runtime.mixer.IsValid() || phases.Length == 0)
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
                // 检查手动触发
                else if (phaseTime >= phase.minDuration && HasTransitionTrigger(phase))
                {
                    float triggerValue = context.GetFloat(phase.transitionTrigger, 0f);
                    if (triggerValue > 0.5f)  // 触发阈值
                    {
                        shouldTransition = true;
                    }
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

            for (int i = 0; i < phases.Length; i++)
            {
                float weight = 0f;
                if (i == currentPhase)
                    weight = currentWeight;
                else if (i == prevPhaseIndex)
                    weight = prevWeight;

                runtime.mixer.SetInputWeight(i, weight * runtime.totalWeight);
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
            runtime.sequencePrevPhase = -1;
            runtime.sequenceTransitionTime = 0f;
            runtime.sequenceInTransition = false;
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
        }

        private bool HasTransitionTrigger(SequentialPhase phase)
        {
            return phase.transitionTrigger.EnumValue != StateDefaultFloatParameter.None ||
                   !string.IsNullOrEmpty(phase.transitionTrigger.StringValue);
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

            if (phase.secondaryClip == null)
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
