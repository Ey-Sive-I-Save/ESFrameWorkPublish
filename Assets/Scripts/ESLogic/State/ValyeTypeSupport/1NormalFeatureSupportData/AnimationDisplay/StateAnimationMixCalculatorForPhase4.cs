using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 阶段播放器（严格四阶段）
    /// - 固定 4 个阶段：Pre → Main → Wait → Released
    /// - 默认按时间推进（最大时长）
    /// - 支持切换条件（触发参数/阈值比较）
    /// - 阶段切换时强制同步 StateRuntimePhase
    /// </summary>
    [Serializable, TypeRegistryItem("高级·阶段播放器(四阶段)")]
    public class StateAnimationMixCalculatorForPhase4 : StateAnimationMixCalculator
    {
        public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.Phase4;

        private const int PhaseCount = 4;
        private const float WeightEpsilon = 0.0001f;
        private const float DefaultTriggerThreshold = 0.5f;

        public enum PhaseTransitionTarget
        {
            Next = 0,
            Pre = 1,
            Main = 2,
            Wait = 3,
            Released = 4,
            Terminate = 5,
        }

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

        [Serializable]
        public struct Phase4Config
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
            [Tooltip("该阶段最短持续时间（秒）。在此之前不会发生任何切换（切换条件/播完切换/自动切换/额外切换）。")]
            [Range(0f, 5f)]
            public float minDuration;

            [LabelText("最大时长")]
            [Tooltip("该阶段最长持续时间（秒）。>0 时到达会【强制】切换到【切换目标】；0 表示无限制。")]
            [Range(0f, 10f)]
            public float maxDuration;

            [LabelText("自动切换")]
            [Tooltip("启用后：当本阶段持续时间达到【最小时长】时，在下一次更新会立刻切换到【切换目标】。\n特点：不需要任何参数/条件，也不会等待到【最大时长】。\n说明：【最大时长】（>0）始终是硬上限，到点一定会强制切换（可作为兜底）。\n用法：如果希望“只在到达最大时长时才切换”，请关闭本开关，仅设置最大时长。")]
            public bool autoTransition;

            [LabelText("播完自动切换")]
            [Tooltip("启用后：当本阶段持续时间达到【最小时长】并且输出动画播放到末尾时，切换到【切换目标】。\n注意：循环动画或无固定长度的输出可能无法判断“末尾”，此时不会触发；可用【最大时长】作为硬上限兜底。")]
            public bool autoTransitionOnAnimationEnd;

            [LabelText("触发参数")]
            [HideInInspector]
            [Tooltip("(兼容旧字段) 旧版“触发参数”。已统一迁移到【切换条件】中")]
            public StateParameter transitionTrigger;

            [LabelText("触发阈值")]
            [HideInInspector]
            [Tooltip("(兼容旧字段) 旧版“触发阈值”。已统一迁移到【切换条件】中")]
            public float transitionTriggerThreshold;

            [LabelText("切换条件（可选）")]
            [Tooltip("当本阶段持续时间达到【最小时长】后，若条件成立则切换到【切换目标】。仅保留这一套手动切换规则")]
            public PhaseTransitionCondition transitionCondition;

            [LabelText("切换目标")]
            [Tooltip("当发生切换（到达最大时长/自动切换/播完切换/切换条件）时，将切到哪个阶段。Terminate=直接终止（标记完成，不切换）")]
            public PhaseTransitionTarget transitionTarget;

            [LabelText("额外切换（可选）")]
            [Tooltip("用于自由跳转/往返（典型：Wait→Main）。当本阶段持续时间达到【最小时长】后，若条件成立则切换到【额外切换目标】")]
            public PhaseTransitionCondition extraTransitionCondition;

            [LabelText("额外切换目标")]
            public PhaseTransitionTarget extraTransitionTarget;
        }

        [Title("四阶段配置")]
        [LabelText("Pre")]
        public Phase4Config pre = new Phase4Config
        {
            phaseName = "Pre",
            minDuration = 0f,
            maxDuration = 0.1f,
            autoTransition = false,
            autoTransitionOnAnimationEnd = false,
        };

        [LabelText("Main")]
        public Phase4Config main = new Phase4Config
        {
            phaseName = "Main",
            minDuration = 0f,
            maxDuration = 0f,
            autoTransition = false,
            autoTransitionOnAnimationEnd = false,
        };

        [LabelText("Wait")]
        public Phase4Config wait = new Phase4Config
        {
            phaseName = "Wait",
            minDuration = 0f,
            maxDuration = 0f,
            autoTransition = false,
            autoTransitionOnAnimationEnd = false,
            transitionTarget = PhaseTransitionTarget.Next,
            extraTransitionTarget = PhaseTransitionTarget.Main,
        };

        [LabelText("Released")]
        public Phase4Config released = new Phase4Config
        {
            phaseName = "Released",
            minDuration = 0f,
            maxDuration = 0.2f,
            autoTransition = false,
            autoTransitionOnAnimationEnd = true,
        };

        [Title("过渡")]
        [LabelText("过渡时间")]
        [Tooltip("阶段切换时的 CrossFade 时间")]
        [Range(0f, 0.5f)]
        public float transitionDuration = 0.1f;

        [LabelText("混合平滑时间")]
        [Tooltip("同阶段内主次Clip混合的平滑时间")]
        [Range(0f, 0.5f)]
        public float blendSmoothTime = 0.05f;

        private bool _isCalculatorInitialized;

#if UNITY_EDITOR
        [Button("初始化默认四阶段数据"), PropertyOrder(-1)]
        private void InitializeDefaultPhase4()
        {
            pre = new Phase4Config
            {
                phaseName = "Pre",
                minDuration = 0f,
                maxDuration = 0.1f,
                autoTransition = false,
                autoTransitionOnAnimationEnd = false,
            };

            main = new Phase4Config
            {
                phaseName = "Main",
                minDuration = 0f,
                maxDuration = 0f,
                autoTransition = false,
                autoTransitionOnAnimationEnd = false,
            };

            wait = new Phase4Config
            {
                phaseName = "Wait",
                minDuration = 0f,
                maxDuration = 0f,
                autoTransition = false,
                autoTransitionOnAnimationEnd = false,
                transitionTarget = PhaseTransitionTarget.Next,
                extraTransitionTarget = PhaseTransitionTarget.Main,
            };

            released = new Phase4Config
            {
                phaseName = "Released",
                minDuration = 0f,
                maxDuration = 0.2f,
                autoTransition = false,
                autoTransitionOnAnimationEnd = true,
            };

            transitionDuration = 0.05f;
            blendSmoothTime = 0.05f;
            Debug.Log("[Phase4] 已初始化默认四阶段数据");
        }
#endif

        protected override string GetUsageHelp()
        {
            return "适用：严格四阶段动作（Pre/Main/Wait/Released）并要求与运行时阶段一一对应。\n" +
                   "每个阶段可配置：主Clip 或 子计算器；可选次Clip混合。\n" +
                   "切换：最大时长为硬上限；达到最小时长后可自动切换/播完切换/切换条件。";
        }

        protected override string GetCalculatorDisplayName()
        {
            return "高级·阶段播放器(四阶段)";
        }

        public override void InitializeCalculator()
        {
            if (_isCalculatorInitialized) return;

            ValidatePhase(ref pre, "Pre");
            ValidatePhase(ref main, "Main");
            ValidatePhase(ref wait, "Wait");
            ValidatePhase(ref released, "Released");

            _isCalculatorInitialized = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidatePhase(ref Phase4Config p, string name)
        {
            if (p.minDuration < 0f) p.minDuration = 0f;

            // 默认目标：Next（保证新字段加入后旧资源行为不变）
            if ((int)p.transitionTarget == 0)
            {
                // Next 是 0，本行只是让意图更清晰（不修改）
            }

            // 统一切换规则：迁移旧 trigger(+threshold) 到 transitionCondition，保证旧资源无需改数据
            if (!p.transitionCondition.enable && HasTrigger(p.transitionTrigger))
            {
                p.transitionCondition.enable = true;
                p.transitionCondition.parameter = p.transitionTrigger;
                p.transitionCondition.compare = PhaseTransitionCompare.Greater;
                p.transitionCondition.threshold = p.transitionTriggerThreshold > 0f ? p.transitionTriggerThreshold : DefaultTriggerThreshold;

                p.transitionTrigger = StateDefaultFloatParameter.None;
                p.transitionTriggerThreshold = 0f;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (p.phaseCalculator == null && p.primaryClip == null && p.secondaryClip == null)
            {
                Debug.LogWarning($"[Phase4] {name} 无可用输出");
            }

            if (p.phaseCalculator != null && (p.primaryClip != null || p.secondaryClip != null))
            {
                Debug.LogWarning($"[Phase4] {name} 同时设置了阶段计算器和动画片段：动画片段与其混合配置将被忽略");
            }
#endif

            if (p.phaseCalculator != null)
            {
                p.phaseCalculator.InitializeCalculator();
            }
        }

        protected override bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
        {
            InitializeCalculator();

            runtime.mixer = AnimationMixerPlayable.Create(graph, PhaseCount);

            runtime.phaseRuntimes = new AnimationCalculatorRuntime[PhaseCount];
            runtime.phaseOutputs = new Playable[PhaseCount];
            runtime.phaseMixers = new AnimationMixerPlayable[PhaseCount];
            runtime.phasePrimaryPlayables = new AnimationClipPlayable[PhaseCount];
            runtime.phaseSecondaryPlayables = new AnimationClipPlayable[PhaseCount];
            runtime.phaseUsesCalculator = new bool[PhaseCount];
            runtime.phaseBlendWeights = new float[PhaseCount];
            runtime.phaseBlendVelocities = new float[PhaseCount];

            InitPhaseRuntime(runtime, graph, 0, pre);
            InitPhaseRuntime(runtime, graph, 1, main);
            InitPhaseRuntime(runtime, graph, 2, wait);
            InitPhaseRuntime(runtime, graph, 3, released);

            for (int i = 0; i < PhaseCount; i++)
            {
                runtime.mixer.SetInputWeight(i, 0f);
            }

            // 起始阶段：Pre
            runtime.sequencePhaseIndex = 0;
            runtime.sequencePhaseTime = 0f;
            runtime.sequenceTotalTime = 0f;
            runtime.sequenceCompleted = false;
            runtime.sequencePrevPhase = -1;
            runtime.sequenceTransitionTime = 0f;
            runtime.sequenceInTransition = false;

            runtime.useSmoothing = blendSmoothTime > 0.001f;

            // 首帧权重到位
            runtime.mixer.SetInputWeight(0, 1f);
            runtime.sequenceLastAppliedPhaseIndex = 0;
            runtime.sequenceLastAppliedPrevPhaseIndex = -1;
            runtime.sequenceLastAppliedPhaseWeight = 1f;
            runtime.sequenceLastAppliedPrevWeight = 0f;

            // ★ 严格一一对应：锁定运行时阶段
            SyncOwnerRuntimePhase(runtime, 0);

            output = runtime.mixer;
            return true;
        }

        private static void InitPhaseRuntime(AnimationCalculatorRuntime runtime, PlayableGraph graph, int index, Phase4Config phase)
        {
            if (phase.phaseCalculator != null)
            {
                runtime.phaseUsesCalculator[index] = true;
                runtime.phaseRuntimes[index] = phase.phaseCalculator.CreateRuntimeData();

                // 继承 owner（便于子计算器需要时做低频写回）
                if (runtime.ownerState != null && runtime.phaseRuntimes[index] != null)
                {
                    runtime.phaseRuntimes[index].ownerState = runtime.ownerState;
                }

                Playable phaseOutput = Playable.Null;
                bool ok = phase.phaseCalculator.InitializeRuntime(runtime.phaseRuntimes[index], graph, ref phaseOutput);
                if (ok && phaseOutput.IsValid())
                {
                    runtime.phaseOutputs[index] = phaseOutput;
                    graph.Connect(phaseOutput, 0, runtime.mixer, index);
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                else
                {
                    Debug.LogWarning($"[Phase4] 阶段{index} 子计算器初始化失败");
                }
#endif

                return;
            }

            runtime.phaseUsesCalculator[index] = false;

            if (phase.primaryClip != null)
            {
                if (phase.enableSecondaryClipBlend && phase.secondaryClip != null)
                {
                    runtime.phaseMixers[index] = AnimationMixerPlayable.Create(graph, 2);

                    runtime.phasePrimaryPlayables[index] = AnimationClipPlayable.Create(graph, phase.primaryClip);
                    graph.Connect(runtime.phasePrimaryPlayables[index], 0, runtime.phaseMixers[index], 0);

                    runtime.phaseSecondaryPlayables[index] = AnimationClipPlayable.Create(graph, phase.secondaryClip);
                    graph.Connect(runtime.phaseSecondaryPlayables[index], 0, runtime.phaseMixers[index], 1);

                    runtime.phaseMixers[index].SetInputWeight(0, 1f);
                    runtime.phaseMixers[index].SetInputWeight(1, 0f);

                    graph.Connect(runtime.phaseMixers[index], 0, runtime.mixer, index);
                }
                else
                {
                    runtime.phasePrimaryPlayables[index] = AnimationClipPlayable.Create(graph, phase.primaryClip);
                    graph.Connect(runtime.phasePrimaryPlayables[index], 0, runtime.mixer, index);
                }
            }
            else if (phase.secondaryClip != null)
            {
                runtime.phaseSecondaryPlayables[index] = AnimationClipPlayable.Create(graph, phase.secondaryClip);
                graph.Connect(runtime.phaseSecondaryPlayables[index], 0, runtime.mixer, index);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.LogWarning($"[Phase4] 阶段{index} 无可用输出");
            }
#endif
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

            if (lastCurrent != currentPhase && lastCurrent != prevPhaseIndex && lastCurrent >= 0)
            {
                mixer.SetInputWeight(lastCurrent, 0f);
            }
            if (lastPrev != prevPhaseIndex && lastPrev != currentPhase && lastPrev >= 0)
            {
                mixer.SetInputWeight(lastPrev, 0f);
            }

            if (currentPhase >= 0)
            {
                if (lastCurrent != currentPhase || Mathf.Abs(runtime.sequenceLastAppliedPhaseWeight - currentWeight) > WeightEpsilon)
                {
                    mixer.SetInputWeight(currentPhase, currentWeight);
                    runtime.sequenceLastAppliedPhaseWeight = currentWeight;
                }
            }

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
            if (runtime == null || !runtime.mixer.IsValid()) return;

            // 保障：若外部已将 State 运行时阶段锁到 Released，则阶段播放器必须播放 Released
            var owner = runtime.ownerState;
            if (owner != null && owner.RuntimePhase == StateRuntimePhase.Released && runtime.sequencePhaseIndex != 3)
            {
                ForceSwitchToPhase(runtime, 3);
            }

            int currentPhase = Mathf.Clamp(runtime.sequencePhaseIndex, 0, PhaseCount - 1);
            float phaseTime = runtime.sequencePhaseTime + deltaTime;
            runtime.sequencePhaseTime = phaseTime;
            runtime.sequenceTotalTime += deltaTime;

            int targetPhase = GetTransitionTarget(runtime, currentPhase, phaseTime, context);

            if (targetPhase != currentPhase)
            {
                int prevPhase = currentPhase;
                runtime.sequencePrevPhase = prevPhase;
                runtime.sequenceInTransition = transitionDuration > 0.001f;
                runtime.sequenceTransitionTime = 0f;

                runtime.sequencePhaseIndex = targetPhase;
                runtime.sequencePhaseTime = 0f;
                currentPhase = targetPhase;

                SyncOwnerRuntimePhase(runtime, targetPhase);
            }

            UpdatePhaseOutput(runtime, currentPhase, GetPhaseConfig(currentPhase), context, deltaTime);
            if (runtime.sequenceInTransition && runtime.sequencePrevPhase >= 0)
            {
                int prevIndex = runtime.sequencePrevPhase;
                UpdatePhaseOutput(runtime, prevIndex, GetPhaseConfig(prevIndex), context, deltaTime);
            }

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

            // Released：可选将“播完/到时”作为完成信号（用于 UntilAnimationEnd 等语义）
            if (!runtime.sequenceCompleted && currentPhase == 3)
            {
                var r = released;
                float releasedTime = runtime.sequencePhaseTime;
                if (r.maxDuration > 0f && releasedTime >= r.maxDuration)
                {
                    runtime.sequenceCompleted = true;
                }
                else if (r.autoTransitionOnAnimationEnd && IsPhaseAnimationEnded(runtime, 3, r))
                {
                    runtime.sequenceCompleted = true;
                }
            }
        }

        public override void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
        {
            if (runtime == null || !runtime.mixer.IsValid()) return;

            var owner = runtime.ownerState;
            if (owner != null && owner.RuntimePhase == StateRuntimePhase.Released && runtime.sequencePhaseIndex != 3)
            {
                ForceSwitchToPhase(runtime, 3);
            }

            int currentPhase = Mathf.Clamp(runtime.sequencePhaseIndex, 0, PhaseCount - 1);

            for (int i = 0; i < PhaseCount; i++)
            {
                runtime.mixer.SetInputWeight(i, i == currentPhase ? 1f : 0f);
            }

            // 若当前阶段是子计算器，也执行一次立即更新
            var phase = GetPhaseConfig(currentPhase);
            if (phase.phaseCalculator != null && runtime.phaseRuntimes != null && runtime.phaseRuntimes[currentPhase] != null)
            {
                phase.phaseCalculator.ImmediateUpdate(runtime.phaseRuntimes[currentPhase], context);
            }
        }

        public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
        {
            if (runtime == null) return null;

            int currentPhase = Mathf.Clamp(runtime.sequencePhaseIndex, 0, PhaseCount - 1);
            var phase = GetPhaseConfig(currentPhase);

            if (phase.phaseCalculator != null && runtime.phaseRuntimes != null)
            {
                var pr = runtime.phaseRuntimes[currentPhase];
                if (pr != null)
                    return phase.phaseCalculator.GetCurrentClip(pr);
            }

            return phase.primaryClip;
        }

        public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
        {
            if (runtime == null) return false;
            if (newClip == null) return false;

            int phaseIndex = clipIndex / 2;
            int localIndex = clipIndex % 2;
            if (phaseIndex < 0 || phaseIndex >= PhaseCount) return false;

            if (runtime.phaseUsesCalculator != null && runtime.phaseUsesCalculator[phaseIndex])
                return false;

            if (localIndex == 0)
            {
                if (runtime.phasePrimaryPlayables == null || !runtime.phasePrimaryPlayables[phaseIndex].IsValid())
                    return false;

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

                return true;
            }

            if (runtime.phaseSecondaryPlayables == null || !runtime.phaseSecondaryPlayables[phaseIndex].IsValid())
                return false;

            if (runtime.phaseMixers == null || !runtime.phaseMixers[phaseIndex].IsValid())
                return false;

            var g2 = runtime.phaseSecondaryPlayables[phaseIndex].GetGraph();
            var os2 = runtime.phaseSecondaryPlayables[phaseIndex].GetSpeed();
            var ot2 = runtime.phaseSecondaryPlayables[phaseIndex].GetTime();

            runtime.phaseMixers[phaseIndex].DisconnectInput(1);
            runtime.phaseSecondaryPlayables[phaseIndex].Destroy();

            runtime.phaseSecondaryPlayables[phaseIndex] = AnimationClipPlayable.Create(g2, newClip);
            runtime.phaseSecondaryPlayables[phaseIndex].SetSpeed(os2);
            runtime.phaseSecondaryPlayables[phaseIndex].SetTime(ot2);
            g2.Connect(runtime.phaseSecondaryPlayables[phaseIndex], 0, runtime.phaseMixers[phaseIndex], 1);

            return true;
        }

        public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
        {
            // Phase4：如果任一阶段 maxDuration<=0 则无法给出标准总时长（返回0）
            float a = pre.maxDuration;
            float b = main.maxDuration;
            float c = wait.maxDuration;
            float d = released.maxDuration;
            if (a <= 0f || b <= 0f || c <= 0f || d <= 0f) return 0f;
            return a + b + c + d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Phase4Config GetPhaseConfig(int index)
        {
            switch (index)
            {
                case 0: return pre;
                case 1: return main;
                case 2: return wait;
                default: return released;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetTransitionTarget(AnimationCalculatorRuntime runtime, int phaseIndex, float phaseTime, in StateMachineContext context)
        {
            if (phaseIndex < 0) return 0;
            if (phaseIndex >= 3) return 3;

            if (runtime != null && runtime.sequenceCompleted)
                return phaseIndex;

            var phase = GetPhaseConfig(phaseIndex);

            if (phaseTime >= phase.minDuration && phase.extraTransitionCondition.Check(context))
            {
                int t = ResolveTarget(runtime, phaseIndex, phase.extraTransitionTarget);
                if (t != phaseIndex) return t;
            }

            if (phase.maxDuration > 0f && phaseTime >= phase.maxDuration)
                return ResolveTarget(runtime, phaseIndex, phase.transitionTarget);

            if (phase.autoTransition && phaseTime >= phase.minDuration)
                return ResolveTarget(runtime, phaseIndex, phase.transitionTarget);

            if (phaseTime < phase.minDuration)
                return phaseIndex;

            if (phase.autoTransitionOnAnimationEnd && IsPhaseAnimationEnded(runtime, phaseIndex, phase))
                return ResolveTarget(runtime, phaseIndex, phase.transitionTarget);

            if (phase.transitionCondition.Check(context))
                return ResolveTarget(runtime, phaseIndex, phase.transitionTarget);

            return phaseIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveTarget(AnimationCalculatorRuntime runtime, int fromPhase, PhaseTransitionTarget target)
        {
            switch (target)
            {
                case PhaseTransitionTarget.Pre: return 0;
                case PhaseTransitionTarget.Main: return 1;
                case PhaseTransitionTarget.Wait: return 2;
                case PhaseTransitionTarget.Released: return 3;
                case PhaseTransitionTarget.Terminate:
                    if (runtime != null) runtime.sequenceCompleted = true;
                    return fromPhase;
                default:
                    return Mathf.Min(fromPhase + 1, 3);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ForceSwitchToPhase(AnimationCalculatorRuntime runtime, int targetPhase)
        {
            int current = Mathf.Clamp(runtime.sequencePhaseIndex, 0, PhaseCount - 1);
            targetPhase = Mathf.Clamp(targetPhase, 0, PhaseCount - 1);
            if (current == targetPhase) return;

            runtime.sequencePrevPhase = current;
            runtime.sequenceInTransition = transitionDuration > 0.001f;
            runtime.sequenceTransitionTime = 0f;

            runtime.sequencePhaseIndex = targetPhase;
            runtime.sequencePhaseTime = 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPlayableEnded(Playable playable)
        {
            if (!playable.IsValid()) return false;

            double duration = playable.GetDuration();
            if (duration <= 0d || double.IsInfinity(duration) || double.IsNaN(duration))
                return false;

            return playable.GetTime() >= duration - 0.0001d;
        }

        private static bool IsPhaseAnimationEnded(AnimationCalculatorRuntime runtime, int phaseIndex, Phase4Config phase)
        {
            if (runtime == null || phaseIndex < 0) return false;

            if (runtime.phaseUsesCalculator != null && phaseIndex < runtime.phaseUsesCalculator.Length && runtime.phaseUsesCalculator[phaseIndex])
            {
                if (runtime.phaseRuntimes != null && phaseIndex < runtime.phaseRuntimes.Length)
                {
                    var pr = runtime.phaseRuntimes[phaseIndex];
                    if (pr != null && IsPlayableEnded(pr.outputPlayable))
                        return true;
                }

                return false;
            }

            if (runtime.phaseMixers != null && phaseIndex < runtime.phaseMixers.Length && runtime.phaseMixers[phaseIndex].IsValid())
            {
                bool primaryEnded = runtime.phasePrimaryPlayables != null && phaseIndex < runtime.phasePrimaryPlayables.Length
                    ? IsPlayableEnded(runtime.phasePrimaryPlayables[phaseIndex])
                    : false;

                bool secondaryEnded = runtime.phaseSecondaryPlayables != null && phaseIndex < runtime.phaseSecondaryPlayables.Length
                    ? IsPlayableEnded(runtime.phaseSecondaryPlayables[phaseIndex])
                    : false;

                if (phase.primaryClip == null) primaryEnded = true;
                if (phase.secondaryClip == null) secondaryEnded = true;

                return primaryEnded && secondaryEnded;
            }

            if (runtime.phasePrimaryPlayables != null && phaseIndex < runtime.phasePrimaryPlayables.Length && runtime.phasePrimaryPlayables[phaseIndex].IsValid())
                return IsPlayableEnded(runtime.phasePrimaryPlayables[phaseIndex]);

            if (runtime.phaseSecondaryPlayables != null && phaseIndex < runtime.phaseSecondaryPlayables.Length && runtime.phaseSecondaryPlayables[phaseIndex].IsValid())
                return IsPlayableEnded(runtime.phaseSecondaryPlayables[phaseIndex]);

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasTrigger(StateParameter trigger)
        {
            return trigger.EnumValue != StateDefaultFloatParameter.None ||
                   !string.IsNullOrEmpty(trigger.StringValue);
        }

        private void UpdatePhaseOutput(
            AnimationCalculatorRuntime runtime,
            int phaseIndex,
            Phase4Config phase,
            in StateMachineContext context,
            float deltaTime)
        {
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
                float smooth = blendSmoothTime > 0.001f ? blendSmoothTime : 0.001f;
                runtime.phaseBlendWeights[phaseIndex] = Mathf.SmoothDamp(
                    runtime.phaseBlendWeights[phaseIndex],
                    targetBlend,
                    ref runtime.phaseBlendVelocities[phaseIndex],
                    smooth,
                    float.MaxValue,
                    deltaTime);
            }
            else
            {
                runtime.phaseBlendWeights[phaseIndex] = targetBlend;
            }

            float w0 = 1f - runtime.phaseBlendWeights[phaseIndex];
            float w1 = runtime.phaseBlendWeights[phaseIndex];

            runtime.phaseMixers[phaseIndex].SetInputWeight(0, w0);
            runtime.phaseMixers[phaseIndex].SetInputWeight(1, w1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SyncOwnerRuntimePhase(AnimationCalculatorRuntime runtime, int phaseIndex)
        {
            var owner = runtime != null ? runtime.ownerState : null;
            if (owner == null) return;

            StateRuntimePhase phase;
            switch (phaseIndex)
            {
                case 0: phase = StateRuntimePhase.Pre; break;
                case 1: phase = StateRuntimePhase.Main; break;
                case 2: phase = StateRuntimePhase.Wait; break;
                default: phase = StateRuntimePhase.Released; break;
            }

            owner.SetRuntimePhaseFromCalculator(phase, true);
        }
    }
}
