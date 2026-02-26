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

        public override bool NeedUpdateWhenFadingOut => true;

        private const int PhaseCount = 4;
        private const float WeightEpsilon = 0.0001f;
        private const float DefaultTriggerThreshold = 0.5f;

        #region Runtime Debug（不依赖编译宏 / 临时固定开启）

        // 临时调试：固定开启，不暴露 Inspector（你现在测试用）。
        // 如需关闭：把下面常量改为 false。
        private const bool DebugAutoTransitionOnAnimationEnd = true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldLogAutoEndThisFrame()
        {
            // 最暴力模式：不节流，每次判定都输出，方便你直接查“为何不生效”。
            return DebugAutoTransitionOnAnimationEnd;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetLogicalDurationSeconds(Playable playable, out double durationSeconds)
        {
            durationSeconds = 0d;

            if (!playable.IsValid()) return false;

            // 口径收敛：只对 AnimationClipPlayable 用 clip.length。
            var playableType = playable.GetPlayableType();
            if (playableType != typeof(AnimationClipPlayable)) return false;

            var cp = (AnimationClipPlayable)playable;
            var clip = cp.GetAnimationClip();
            if (clip == null) return false;

            durationSeconds = clip.length;
            return durationSeconds > 0.0001d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPlayableEnded(Playable playable)
        {
            if (!playable.IsValid()) return false;

            // 口径收敛：只对 AnimationClipPlayable 用 clip.length 判定。
            // Mixer/ScriptPlayable 等几乎拿不到可靠“时长/播完”语义，这里直接视为不可判定（返回 false），
            // 请用 maxDuration / 标准时长机制兜底。
            if (TryGetLogicalDurationSeconds(playable, out double len))
            {
                return playable.GetTime() >= len - 0.0001d;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetPlayableDebugInfo(Playable p)
        {
            if (!p.IsValid()) return "Invalid";
            var type = p.GetPlayableType();
            string typeName = type != null ? type.Name : "Unknown";
            double t = p.GetTime();
            double s = p.GetSpeed();

            // duration 打印：只对 ClipPlayable 有意义（clip.length）。其它节点输出 ?
            string durStr = "?";
            if (TryGetLogicalDurationSeconds(p, out double len))
                durStr = len.ToString("F3");

            return $"{typeName} t={t:F3} dur={durStr} speed={s:F2} in={p.GetInputCount()}";
        }

        private bool IsPhaseAnimationEndedWithRuntimeDebug(AnimationCalculatorRuntime runtime, int phaseIndex, Phase4Config phase)
        {
            bool doLog = ShouldLogAutoEndThisFrame();
            var owner = runtime != null ? runtime.ownerState : null;
            string ownerName = owner != null ? owner.strKey : "(无Owner)";

            if (runtime == null)
            {
                if (doLog) Debug.Log($"[Phase4][AutoEnd] runtime=null | phase={phaseIndex} {phase.phaseName} | owner={ownerName}");
                return false;
            }

            // 子计算器：只能判断 outputPlayable 是否可判定结束。
            if (runtime.phaseUsesCalculator != null && phaseIndex >= 0 && phaseIndex < runtime.phaseUsesCalculator.Length && runtime.phaseUsesCalculator[phaseIndex])
            {
                Playable outP = Playable.Null;
                if (runtime.phaseRuntimes != null && phaseIndex < runtime.phaseRuntimes.Length)
                {
                    var pr = runtime.phaseRuntimes[phaseIndex];
                    outP = pr != null ? pr.outputPlayable : Playable.Null;
                }

                bool ended = IsPlayableEnded(outP);
                if (doLog)
                {
                    Debug.Log(
                        $"[Phase4][AutoEnd] 子计算器输出判定 | owner={ownerName} | phase={phaseIndex} {phase.phaseName} | " +
                        $"output=({GetPlayableDebugInfo(outP)}) | ended={ended} | " +
                        $"提示：非Clip输出若无有效输入，无法判定播完（用 maxDuration 兜底）");
                }
                return ended;
            }

            // 主次混合：分别判断 primary/secondary。
            if (runtime.phaseMixers != null && phaseIndex >= 0 && phaseIndex < runtime.phaseMixers.Length && runtime.phaseMixers[phaseIndex].IsValid())
            {
                var p0 = runtime.phasePrimaryPlayables != null && phaseIndex < runtime.phasePrimaryPlayables.Length
                    ? (Playable)runtime.phasePrimaryPlayables[phaseIndex]
                    : Playable.Null;
                var p1 = runtime.phaseSecondaryPlayables != null && phaseIndex < runtime.phaseSecondaryPlayables.Length
                    ? (Playable)runtime.phaseSecondaryPlayables[phaseIndex]
                    : Playable.Null;

                bool primaryEnded = phase.primaryClip == null ? true : IsPlayableEnded(p0);
                bool secondaryEnded = phase.secondaryClip == null ? true : IsPlayableEnded(p1);
                bool ended = primaryEnded && secondaryEnded;

                if (doLog)
                {
                    Debug.Log(
                        $"[Phase4][AutoEnd] 主次Clip判定 | owner={ownerName} | phase={phaseIndex} {phase.phaseName} | " +
                        $"primary=({GetPlayableDebugInfo(p0)}) clip={(phase.primaryClip != null ? phase.primaryClip.name : "None")} ended={primaryEnded} | " +
                        $"secondary=({GetPlayableDebugInfo(p1)}) clip={(phase.secondaryClip != null ? phase.secondaryClip.name : "None")} ended={secondaryEnded} | " +
                        $"result={ended}");
                }
                return ended;
            }

            // 单Clip：哪个有效用哪个。
            if (runtime.phasePrimaryPlayables != null && phaseIndex >= 0 && phaseIndex < runtime.phasePrimaryPlayables.Length && runtime.phasePrimaryPlayables[phaseIndex].IsValid())
            {
                var p = (Playable)runtime.phasePrimaryPlayables[phaseIndex];
                bool ended = IsPlayableEnded(p);
                if (doLog)
                {
                    Debug.Log($"[Phase4][AutoEnd] 单Clip判定(primary) | owner={ownerName} | phase={phaseIndex} {phase.phaseName} | {GetPlayableDebugInfo(p)} | ended={ended}");
                }
                return ended;
            }

            if (runtime.phaseSecondaryPlayables != null && phaseIndex >= 0 && phaseIndex < runtime.phaseSecondaryPlayables.Length && runtime.phaseSecondaryPlayables[phaseIndex].IsValid())
            {
                var p = (Playable)runtime.phaseSecondaryPlayables[phaseIndex];
                bool ended = IsPlayableEnded(p);
                if (doLog)
                {
                    Debug.Log($"[Phase4][AutoEnd] 单Clip判定(secondary) | owner={ownerName} | phase={phaseIndex} {phase.phaseName} | {GetPlayableDebugInfo(p)} | ended={ended}");
                }
                return ended;
            }

            if (doLog)
            {
                Debug.Log($"[Phase4][AutoEnd] 无可判定Playable | owner={ownerName} | phase={phaseIndex} {phase.phaseName} | ended=false");
            }
            return false;
        }

        #endregion

        public enum PhaseTransitionTarget
        {
            [InspectorName("下一个阶段")]
            Next = 0,
            [InspectorName("Pre（预备）")]
            Pre = 1,
            [InspectorName("Main（主阶段）")]
            Main = 2,
            [InspectorName("Wait（等待）")]
            Wait = 3,
            [InspectorName("Released（释放）")]
            Released = 4,
            [InspectorName("Terminate（终止/不切换）")]
            Terminate = 5,
        }

        public enum PhaseTransitionCompare
        {
            [InspectorName("大于")]
            Greater,
            [InspectorName("小于")]
            Less
        }

        [Serializable]
        public class PhaseTransitionCondition
        {
            [LabelText("启用条件")]
            public bool enable;

            [LabelText("参数")]
            [Tooltip("用于比较的 float 参数")]
            public StateParameter parameter;

            [LabelText("比较")]
            public PhaseTransitionCompare compare;

            [LabelText("包含等于(=)")]
            [Tooltip("默认不包含等号：Greater=大于(>)，Less=小于(<)。勾选后变为 >= / <=")]
            public bool includeEqual;

            [LabelText("阈值")]
            public float threshold;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Check(in StateMachineContext context)
            {
                if (!enable) return false;
                float diff = context.GetFloat(parameter, 0f) - threshold;

                if (compare == PhaseTransitionCompare.Greater)
                    return includeEqual ? (diff >= 0f) : (diff > 0f);

                return includeEqual ? (diff <= 0f) : (diff < 0f);
            }
        }

        [Serializable]
        public class Phase4Config
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

        [LabelText("阶段跳转权重过渡倍率")]
        [Tooltip("仅影响阶段切换时权重CrossFade的推进速度：1=按【过渡时间】推进；2=更快（相当于过渡时间减半）。\n不会影响动画播放速度，也不会影响阶段切换条件判断。")]
        [Range(0.1f, 10f)]
        public float transitionWeightSpeedMultiplier = 1f;

        [LabelText("混合平滑时间")]
        [Tooltip("同阶段内主次Clip混合的平滑时间")]
        [Range(0f, 0.5f)]
        public float blendSmoothTime = 0.05f;

        private bool _isCalculatorInitialized;

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        [NonSerialized]
        private bool _debugLogPhase4;

        [NonSerialized]
        private float _debugLastBlendLogged;

        [NonSerialized]
        private int _debugLastPhaseOutputLogFrame;
    #endif

    #if UNITY_EDITOR
        [ShowInInspector, FoldoutGroup("调试"), LabelText("临时Debug输出(勾选后会Log)")]
        private bool DebugLogPhase4
        {
            get => _debugLogPhase4;
            set
            {
            _debugLogPhase4 = value;
            _debugLastBlendLogged = float.NaN;
            }
        }
    #endif

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
            runtime.mixer.SetTime(0d);

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

            // ★ 关键修复：非当前阶段不应后台推进时间，否则强制切到 Released 时可能已播完。
            RestartPhasePlayables(runtime, 0);
            UpdatePhasePlayablesSpeed(runtime, currentPhase: 0, prevPhaseIndex: -1);

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
                    phaseOutput.SetTime(0d);
                    runtime.phaseOutputs[index] = phaseOutput;
                    graph.Connect(phaseOutput, 0, runtime.mixer, index);

                    // 双保险：部分子计算器会把 outputPlayable 指向内部根节点，这里也重置一次。
                    var pr = runtime.phaseRuntimes[index];
                    if (pr != null && pr.outputPlayable.IsValid())
                        pr.outputPlayable.SetTime(0d);
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
                    runtime.phaseMixers[index].SetTime(0d);

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
                    runtime.phasePrimaryPlayables[index].SetTime(0d);
                    graph.Connect(runtime.phasePrimaryPlayables[index], 0, runtime.mixer, index);
                }
            }
            else if (phase.secondaryClip != null)
            {
                runtime.phaseSecondaryPlayables[index] = AnimationClipPlayable.Create(graph, phase.secondaryClip);
                runtime.phaseSecondaryPlayables[index].SetTime(0d);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RestartPhasePlayables(AnimationCalculatorRuntime runtime, int phaseIndex)
        {
            if (runtime == null || phaseIndex < 0 || phaseIndex >= PhaseCount) return;

            // 子计算器：尽量重置输出Playable的时间。
            if (runtime.phaseUsesCalculator != null && phaseIndex < runtime.phaseUsesCalculator.Length && runtime.phaseUsesCalculator[phaseIndex])
            {
                if (runtime.phaseRuntimes != null && phaseIndex < runtime.phaseRuntimes.Length)
                {
                    var pr = runtime.phaseRuntimes[phaseIndex];
                    if (pr != null && pr.outputPlayable.IsValid())
                    {
                        pr.outputPlayable.SetTime(0d);
                    }
                }

                if (runtime.phaseOutputs != null && phaseIndex < runtime.phaseOutputs.Length && runtime.phaseOutputs[phaseIndex].IsValid())
                {
                    runtime.phaseOutputs[phaseIndex].SetTime(0d);
                }
                return;
            }

            if (runtime.phasePrimaryPlayables != null && phaseIndex < runtime.phasePrimaryPlayables.Length && runtime.phasePrimaryPlayables[phaseIndex].IsValid())
            {
                runtime.phasePrimaryPlayables[phaseIndex].SetTime(0d);
            }

            if (runtime.phaseSecondaryPlayables != null && phaseIndex < runtime.phaseSecondaryPlayables.Length && runtime.phaseSecondaryPlayables[phaseIndex].IsValid())
            {
                runtime.phaseSecondaryPlayables[phaseIndex].SetTime(0d);
            }

            if (runtime.phaseMixers != null && phaseIndex < runtime.phaseMixers.Length && runtime.phaseMixers[phaseIndex].IsValid())
            {
                runtime.phaseMixers[phaseIndex].SetTime(0d);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetPhasePlayablesSpeed(AnimationCalculatorRuntime runtime, int phaseIndex, double speed)
        {
            if (runtime == null || phaseIndex < 0 || phaseIndex >= PhaseCount) return;

            // 子计算器：尽量设置输出Playable速度。
            if (runtime.phaseUsesCalculator != null && phaseIndex < runtime.phaseUsesCalculator.Length && runtime.phaseUsesCalculator[phaseIndex])
            {
                if (runtime.phaseRuntimes != null && phaseIndex < runtime.phaseRuntimes.Length)
                {
                    var pr = runtime.phaseRuntimes[phaseIndex];
                    if (pr != null && pr.outputPlayable.IsValid())
                    {
                        pr.outputPlayable.SetSpeed(speed);
                    }
                }
                return;
            }

            if (runtime.phasePrimaryPlayables != null && phaseIndex < runtime.phasePrimaryPlayables.Length && runtime.phasePrimaryPlayables[phaseIndex].IsValid())
            {
                runtime.phasePrimaryPlayables[phaseIndex].SetSpeed(speed);
            }

            if (runtime.phaseSecondaryPlayables != null && phaseIndex < runtime.phaseSecondaryPlayables.Length && runtime.phaseSecondaryPlayables[phaseIndex].IsValid())
            {
                runtime.phaseSecondaryPlayables[phaseIndex].SetSpeed(speed);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdatePhasePlayablesSpeed(AnimationCalculatorRuntime runtime, int currentPhase, int prevPhaseIndex)
        {
            // Phase4 的语义：非当前(及过渡上一阶段)不应在后台推进，否则退出/切阶段时可能“已经播完”，看起来没效果。
            for (int i = 0; i < PhaseCount; i++)
            {
                bool active = (i == currentPhase) || (prevPhaseIndex >= 0 && i == prevPhaseIndex);
                SetPhasePlayablesSpeed(runtime, i, active ? 1d : 0d);
            }
        }

        public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
        {
            
            if (runtime == null || !runtime.mixer.IsValid()) return;
         
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var ownerState = runtime.ownerState;
            string ownerName = ownerState != null ? ownerState.strKey : "(无Owner)";
#endif

            // 保障：若外部已将 State 运行时阶段锁到 Released，则阶段播放器必须播放 Released
            var owner = runtime.ownerState;
            if (owner != null && owner.RuntimePhase == StateRuntimePhase.Released && runtime.sequencePhaseIndex != 3)
            {
                // 退出强制释放：不做内部CrossFade，直接切到 Released=1。
                ForceSwitchToPhase(runtime, 3, immediate: true);
            }

            int currentPhase = Mathf.Clamp(runtime.sequencePhaseIndex, 0, PhaseCount - 1);
            float phaseTime = runtime.sequencePhaseTime + deltaTime;
            runtime.sequencePhaseTime = phaseTime;
            runtime.sequenceTotalTime += deltaTime;

            int targetPhase = GetTransitionTarget(runtime, currentPhase, phaseTime, context);
           
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugLogPhase4 && targetPhase != currentPhase)
            {
                Debug.Log($"[四阶段-调试] {ownerName} 阶段切换 {currentPhase}->{targetPhase} 阶段时间={phaseTime:F3} 总时间={runtime.sequenceTotalTime:F3}");
            }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            {
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null && dbg.IsAnimationBlendEnabled)
                {
                    // 只在你们明确开启调试时才输出，避免刷屏。
                    // 这里不强依赖 _debugLogPhase4（Release 下该字段可能不存在），用全局开关做统一 gating。
                    dbg.LogAnimationBlend($"[Phase4] Phase={currentPhase} PhaseTime={phaseTime:F3} TotalTime={runtime.sequenceTotalTime:F3} Target={targetPhase}");
                }
            }
#endif
            if (targetPhase != currentPhase)
            {
                int prevPhase = currentPhase;
                runtime.sequencePrevPhase = prevPhase;
                runtime.sequenceInTransition = transitionDuration > 0.001f;
                runtime.sequenceTransitionTime = 0f;

                runtime.sequencePhaseIndex = targetPhase;
                runtime.sequencePhaseTime = 0f;
                currentPhase = targetPhase;

                // 切到新阶段：从头开始播，避免“已在后台播完”。
                RestartPhasePlayables(runtime, targetPhase);

                SyncOwnerRuntimePhase(runtime, targetPhase);

                if (DebugAutoTransitionOnAnimationEnd && ShouldLogAutoEndThisFrame())
                {
                    var owner2 = runtime.ownerState;
                    string ownerName2 = owner2 != null ? owner2.strKey : "(无Owner)";
                    string rp = owner2 != null ? owner2.RuntimePhase.ToString() : "(无OwnerPhase)";
                    Debug.Log($"[Phase4][PhaseSwitch] owner={ownerName2} {prevPhase}->{targetPhase} | RuntimePhaseNow={rp}");
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float prevBlendBefore = 0f;
            bool checkBlend = false;
            if (_debugLogPhase4 && runtime.phaseBlendWeights != null && currentPhase >= 0 && currentPhase < runtime.phaseBlendWeights.Length)
            {
                var cfg = GetPhaseConfig(currentPhase);
                checkBlend = cfg.enableSecondaryClipBlend && cfg.secondaryClip != null;
                if (checkBlend)
                    prevBlendBefore = runtime.phaseBlendWeights[currentPhase];
            }
#endif

            UpdatePhaseOutput(runtime, currentPhase, GetPhaseConfig(currentPhase), context, deltaTime);
            if (runtime.sequenceInTransition && runtime.sequencePrevPhase >= 0)
            {
                int prevIndex = runtime.sequencePrevPhase;
                UpdatePhaseOutput(runtime, prevIndex, GetPhaseConfig(prevIndex), context, deltaTime);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugLogPhase4 && checkBlend && runtime.phaseBlendWeights != null && currentPhase >= 0 && currentPhase < runtime.phaseBlendWeights.Length)
            {
                float blend = runtime.phaseBlendWeights[currentPhase];
                // 避免每帧刷屏：仅当权重有明显变化或首次开启时输出
                if (float.IsNaN(_debugLastBlendLogged) || Mathf.Abs(blend - _debugLastBlendLogged) >= 0.05f || Mathf.Abs(blend - prevBlendBefore) >= 0.05f)
                {
                    _debugLastBlendLogged = blend;

                    var cfg = GetPhaseConfig(currentPhase);
                    string pName = cfg.primaryClip != null ? cfg.primaryClip.name : "None";
                    string sName = cfg.secondaryClip != null ? cfg.secondaryClip.name : "None";
                    float w0 = 1f - blend;
                    float w1 = blend;
                    Debug.Log($"[四阶段-调试] {ownerName} 主次混合 阶段={currentPhase} 主:{pName}({w0:F2}) 次:{sName}({w1:F2})");
                }
            }
#endif

            float currentWeight = 1f;
            float prevWeight = 0f;
            int prevPhaseIndex = -1;

            if (runtime.sequenceInTransition)
            {
                // 兼容：Unity 对“新增字段”的旧资源默认值通常是 0（而非代码初始化的 1）。
                // 语义约定：倍率默认 1；用户通过 Inspector 可调到 [0.1, 10]。
                float speedMul = transitionWeightSpeedMultiplier;
                if (!(speedMul > 0f)) speedMul = 1f;
                runtime.sequenceTransitionTime += deltaTime * speedMul;
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

            // 每帧收口：只推进当前阶段（以及过渡上一阶段）。
            UpdatePhasePlayablesSpeed(runtime, currentPhase, prevPhaseIndex);

            ApplyMixerWeightsOptimized(runtime, currentPhase, currentWeight, prevPhaseIndex, prevWeight);

            // Released：可选将“播完/到时”作为完成信号（用于 UntilAnimationEnd 等语义）
            if (!runtime.sequenceCompleted && currentPhase == 3)
            {
                var r = released;
                float releasedTime = runtime.sequencePhaseTime;
                if (r.maxDuration > 0f && releasedTime >= r.maxDuration)
                {
                    runtime.sequenceCompleted = true;

                    if (DebugAutoTransitionOnAnimationEnd && ShouldLogAutoEndThisFrame())
                    {
                        var owner2 = runtime.ownerState;
                        string ownerName2 = owner2 != null ? owner2.strKey : "(无Owner)";
                        Debug.Log($"[Phase4][ReleasedComplete] 达到 maxDuration 完成 | owner={ownerName2} | releasedTime={releasedTime:F3} >= maxDuration={r.maxDuration:F3}");
                    }
                }
                else if (r.autoTransitionOnAnimationEnd)
                {
                    bool ended = IsPhaseAnimationEndedWithRuntimeDebug(runtime, 3, r);

                    if (ended)
                    {
                        runtime.sequenceCompleted = true;

                        if (DebugAutoTransitionOnAnimationEnd && ShouldLogAutoEndThisFrame())
                        {
                            var owner2 = runtime.ownerState;
                            string ownerName2 = owner2 != null ? owner2.strKey : "(无Owner)";
                            Debug.Log($"[Phase4][ReleasedComplete] 播完完成(autoTransitionOnAnimationEnd) | owner={ownerName2} | releasedTime={releasedTime:F3}");
                        }
                    }
                    else
                    {
                        if (DebugAutoTransitionOnAnimationEnd && ShouldLogAutoEndThisFrame())
                        {
                            var owner2 = runtime.ownerState;
                            string ownerName2 = owner2 != null ? owner2.strKey : "(无Owner)";
                            Debug.Log($"[Phase4][ReleasedComplete] 未完成：未判定播完 | owner={ownerName2} | releasedTime={releasedTime:F3} | autoTransitionOnAnimationEnd=true");
                        }
                    }
                }
            }
        }

        public override void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
        {
            if (runtime == null || !runtime.mixer.IsValid()) return;

            var owner = runtime.ownerState;
            if (owner != null && owner.RuntimePhase == StateRuntimePhase.Released && runtime.sequencePhaseIndex != 3)
            {
                ForceSwitchToPhase(runtime, 3, immediate: true);
            }

            int currentPhase = Mathf.Clamp(runtime.sequencePhaseIndex, 0, PhaseCount - 1);

            RestartPhasePlayables(runtime, currentPhase);
            UpdatePhasePlayablesSpeed(runtime, currentPhase, prevPhaseIndex: -1);

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
            // Phase4：阶段可能是“无限制(maxDuration=0)”或由子计算器驱动。
            // 这里提供一个“标准/估算时长”，用于进度/回退逻辑。
            // 约定：
            // - 优先使用 maxDuration（硬上限）
            // - 否则：若配置了阶段计算器，则用子计算器的标准时长
            // - 否则：用主/次Clip的长度（取较大）
            // - 若阶段输出是循环/不可估算，则该阶段返回0
            float a = GetPhaseEstimatedDuration(runtime, 0, pre);
            float b = GetPhaseEstimatedDuration(runtime, 1, main);
            float c = GetPhaseEstimatedDuration(runtime, 2, wait);
            float d = GetPhaseEstimatedDuration(runtime, 3, released);

            // 全部可估算：返回总和
            if (a > 0.001f && b > 0.001f && c > 0.001f && d > 0.001f)
                return a + b + c + d;

            // 至少给出“主动画”的合理时长（符合：有主动画就用主动画时长）
            if (b > 0.001f)
                return b;

            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetPhaseEstimatedDuration(AnimationCalculatorRuntime runtime, int phaseIndex, Phase4Config phase)
        {
            if (phase.maxDuration > 0.001f)
                return phase.maxDuration;

            // 子计算器：优先使用其标准时长（并保护循环输出）
            if (runtime != null && runtime.phaseUsesCalculator != null && phaseIndex >= 0 && phaseIndex < runtime.phaseUsesCalculator.Length && runtime.phaseUsesCalculator[phaseIndex])
            {
                if (runtime.phaseRuntimes != null && phaseIndex < runtime.phaseRuntimes.Length)
                {
                    var pr = runtime.phaseRuntimes[phaseIndex];
                    if (pr != null && phase.phaseCalculator != null)
                    {
                        float d = phase.phaseCalculator.GetStandardDuration(pr);
                        if (d > 0.001f && !float.IsInfinity(d) && !float.IsNaN(d))
                            return d;
                    }
                }

                return 0f;
            }

            // 直连Clip：用主/次长度（只依赖 clip.length，不使用 isLooping）
            float p = phase.primaryClip != null ? phase.primaryClip.length : 0f;
            float s = phase.secondaryClip != null ? phase.secondaryClip.length : 0f;
            return Mathf.Max(p, s);
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
            {
                if (DebugAutoTransitionOnAnimationEnd && ShouldLogAutoEndThisFrame())
                {
                    var owner = runtime.ownerState;
                    string ownerName = owner != null ? owner.strKey : "(无Owner)";
                    var phase0 = GetPhaseConfig(phaseIndex);
                    Debug.Log(
                        $"[Phase4][NoSwitch] sequenceCompleted=true | owner={ownerName} | phase={phaseIndex} {phase0.phaseName} | " +
                        $"phaseTime={phaseTime:F3} -> stay");
                }
                return phaseIndex;
            }

            var phase = GetPhaseConfig(phaseIndex);

            bool doLog = DebugAutoTransitionOnAnimationEnd && ShouldLogAutoEndThisFrame();
            var ownerForLog = runtime != null ? runtime.ownerState : null;
            string ownerNameForLog = ownerForLog != null ? ownerForLog.strKey : "(无Owner)";

            if (phaseTime >= phase.minDuration && phase.extraTransitionCondition.Check(context))
            {
                int t = ResolveTarget(runtime, phaseIndex, phase.extraTransitionTarget);
                if (t != phaseIndex)
                {
                    if (doLog)
                    {
                        float v = phase.extraTransitionCondition.enable
                            ? context.GetFloat(phase.extraTransitionCondition.parameter, 0f)
                            : 0f;
                        Debug.Log(
                            $"[Phase4][SwitchReason] extraTransition | owner={ownerNameForLog} | phase={phaseIndex} {phase.phaseName} -> {t} | " +
                            $"phaseTime={phaseTime:F3} min={phase.minDuration:F3} | " +
                            $"extraCond(enable={phase.extraTransitionCondition.enable} v={v:F3} {phase.extraTransitionCondition.compare} thr={phase.extraTransitionCondition.threshold:F3}) | " +
                            $"extraTarget={phase.extraTransitionTarget} -> resolved={t} | sequenceCompleted={(runtime != null && runtime.sequenceCompleted)}");
                    }
                    return t;
                }

                if (doLog)
                {
                    float v = phase.extraTransitionCondition.enable
                        ? context.GetFloat(phase.extraTransitionCondition.parameter, 0f)
                        : 0f;
                    Debug.Log(
                        $"[Phase4][NoSwitch] extraTransition 条件成立但目标=当前 | owner={ownerNameForLog} | phase={phaseIndex} {phase.phaseName} | " +
                        $"phaseTime={phaseTime:F3} min={phase.minDuration:F3} | " +
                        $"extraCond(enable={phase.extraTransitionCondition.enable} v={v:F3} {phase.extraTransitionCondition.compare} thr={phase.extraTransitionCondition.threshold:F3}) | " +
                        $"extraTarget={phase.extraTransitionTarget} -> resolved={t}");
                }
            }

            if (phase.maxDuration > 0f && phaseTime >= phase.maxDuration)
            {
                int t = ResolveTarget(runtime, phaseIndex, phase.transitionTarget);
                if (t != phaseIndex)
                {
                    if (doLog)
                    {
                        Debug.Log(
                            $"[Phase4][SwitchReason] maxDuration | owner={ownerNameForLog} | phase={phaseIndex} {phase.phaseName} -> {t} | " +
                            $"phaseTime={phaseTime:F3} >= max={phase.maxDuration:F3} | transitionTarget={phase.transitionTarget} -> resolved={t} | " +
                            $"sequenceCompleted={(runtime != null && runtime.sequenceCompleted)}");
                    }
                    return t;
                }
                if (doLog)
                {
                    Debug.Log(
                        $"[Phase4][NoSwitch] 达到 maxDuration 但目标=当前 | owner={ownerNameForLog} | phase={phaseIndex} {phase.phaseName} | " +
                        $"phaseTime={phaseTime:F3} >= max={phase.maxDuration:F3} | transitionTarget={phase.transitionTarget} -> resolved={t} | " +
                        $"sequenceCompleted={(runtime != null && runtime.sequenceCompleted)}");
                }
                return phaseIndex;
            }

            if (phase.autoTransition && phaseTime >= phase.minDuration)
            {
                int t = ResolveTarget(runtime, phaseIndex, phase.transitionTarget);
                if (t != phaseIndex)
                {
                    if (doLog)
                    {
                        Debug.Log(
                            $"[Phase4][SwitchReason] autoTransition | owner={ownerNameForLog} | phase={phaseIndex} {phase.phaseName} -> {t} | " +
                            $"phaseTime={phaseTime:F3} min={phase.minDuration:F3} | transitionTarget={phase.transitionTarget} -> resolved={t} | " +
                            $"sequenceCompleted={(runtime != null && runtime.sequenceCompleted)}");
                    }
                    return t;
                }
                if (doLog)
                {
                    Debug.Log(
                        $"[Phase4][NoSwitch] autoTransition=true 但目标=当前 | owner={ownerNameForLog} | phase={phaseIndex} {phase.phaseName} | " +
                        $"phaseTime={phaseTime:F3} min={phase.minDuration:F3} | transitionTarget={phase.transitionTarget} -> resolved={t} | " +
                        $"sequenceCompleted={(runtime != null && runtime.sequenceCompleted)}");
                }
                return phaseIndex;
            }

            if (phaseTime < phase.minDuration)
            {
                if (DebugAutoTransitionOnAnimationEnd && phase.autoTransitionOnAnimationEnd && ShouldLogAutoEndThisFrame())
                {
                    var owner = runtime != null ? runtime.ownerState : null;
                    string ownerName = owner != null ? owner.strKey : "(无Owner)";
                    Debug.Log($"[Phase4][AutoEnd] 被 minDuration 阻挡 | owner={ownerName} | phase={phaseIndex} {phase.phaseName} | phaseTime={phaseTime:F3} < minDuration={phase.minDuration:F3}");
                }
                return phaseIndex;
            }

            if (phase.autoTransitionOnAnimationEnd)
            {
                bool ended = IsPhaseAnimationEndedWithRuntimeDebug(runtime, phaseIndex, phase);

                if (ended)
                {
                    int resolved = ResolveTarget(runtime, phaseIndex, phase.transitionTarget);
                    if (DebugAutoTransitionOnAnimationEnd && ShouldLogAutoEndThisFrame())
                    {
                        bool completed = runtime != null && runtime.sequenceCompleted;
                        Debug.Log(
                            $"[Phase4][AutoEnd] ended=true | owner={ownerNameForLog} | phase={phaseIndex} {phase.phaseName} | " +
                            $"transitionTarget={phase.transitionTarget} -> resolved={resolved} | sequenceCompleted={completed}");
                    }

                    if (resolved == phaseIndex)
                    {
                        if (doLog)
                        {
                            Debug.Log(
                                $"[Phase4][NoSwitch] ended=true 但 resolved=当前（通常是 Terminate）| owner={ownerNameForLog} | " +
                                $"phase={phaseIndex} {phase.phaseName} | transitionTarget={phase.transitionTarget} -> resolved={resolved} | " +
                                $"sequenceCompleted={(runtime != null && runtime.sequenceCompleted)}");
                        }
                        return phaseIndex;
                    }

                    if (doLog)
                    {
                        Debug.Log(
                            $"[Phase4][SwitchReason] autoEnd(ended) | owner={ownerNameForLog} | phase={phaseIndex} {phase.phaseName} -> {resolved} | " +
                            $"phaseTime={phaseTime:F3} min={phase.minDuration:F3} | transitionTarget={phase.transitionTarget} -> resolved={resolved} | " +
                            $"sequenceCompleted={(runtime != null && runtime.sequenceCompleted)}");
                    }

                    return resolved;
                }
            }

            if (phase.transitionCondition.Check(context))
            {
                int t = ResolveTarget(runtime, phaseIndex, phase.transitionTarget);
                if (t != phaseIndex)
                {
                    if (doLog)
                    {
                        float v = phase.transitionCondition.enable
                            ? context.GetFloat(phase.transitionCondition.parameter, 0f)
                            : 0f;
                        Debug.Log(
                            $"[Phase4][SwitchReason] transitionCondition | owner={ownerNameForLog} | phase={phaseIndex} {phase.phaseName} -> {t} | " +
                            $"cond(enable={phase.transitionCondition.enable} v={v:F3} {phase.transitionCondition.compare} thr={phase.transitionCondition.threshold:F3}) | " +
                            $"transitionTarget={phase.transitionTarget} -> resolved={t} | phaseTime={phaseTime:F3} min={phase.minDuration:F3} | " +
                            $"sequenceCompleted={(runtime != null && runtime.sequenceCompleted)}");
                    }
                    return t;
                }
                if (doLog)
                {
                    float v = phase.transitionCondition.enable
                        ? context.GetFloat(phase.transitionCondition.parameter, 0f)
                        : 0f;
                    Debug.Log(
                        $"[Phase4][NoSwitch] transitionCondition 成立但目标=当前 | owner={ownerNameForLog} | phase={phaseIndex} {phase.phaseName} | " +
                        $"cond(enable={phase.transitionCondition.enable} v={v:F3} {phase.transitionCondition.compare} thr={phase.transitionCondition.threshold:F3}) | " +
                        $"transitionTarget={phase.transitionTarget} -> resolved={t} | sequenceCompleted={(runtime != null && runtime.sequenceCompleted)}");
                }
                return phaseIndex;
            }

            if (doLog)
            {
                bool extraEnable = phase.extraTransitionCondition.enable;
                float extraV = extraEnable ? context.GetFloat(phase.extraTransitionCondition.parameter, 0f) : 0f;
                bool extraOk = phaseTime >= phase.minDuration && phase.extraTransitionCondition.Check(context);

                bool transEnable = phase.transitionCondition.enable;
                float transV = transEnable ? context.GetFloat(phase.transitionCondition.parameter, 0f) : 0f;
                bool transOk = phase.transitionCondition.Check(context);

                Debug.Log(
                    $"[Phase4][NoSwitch] 所有规则未触发 | owner={ownerNameForLog} | phase={phaseIndex} {phase.phaseName} | " +
                    $"phaseTime={phaseTime:F3} min={phase.minDuration:F3} max={phase.maxDuration:F3} | " +
                    $"extraCond(enable={extraEnable} ok={extraOk} v={extraV:F3}) extraTarget={phase.extraTransitionTarget} | " +
                    $"autoTransition={phase.autoTransition} | autoEnd={phase.autoTransitionOnAnimationEnd} | " +
                    $"transitionCond(enable={transEnable} ok={transOk} v={transV:F3}) transitionTarget={phase.transitionTarget}");
            }

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
            ForceSwitchToPhase(runtime, targetPhase, immediate: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ForceSwitchToPhase(AnimationCalculatorRuntime runtime, int targetPhase, bool immediate)
        {
            int current = Mathf.Clamp(runtime.sequencePhaseIndex, 0, PhaseCount - 1);
            targetPhase = Mathf.Clamp(targetPhase, 0, PhaseCount - 1);
            if (current == targetPhase) return;

            if (immediate)
            {
                runtime.sequencePrevPhase = -1;
                runtime.sequenceInTransition = false;
                runtime.sequenceTransitionTime = 0f;
            }
            else
            {
                runtime.sequencePrevPhase = current;
                runtime.sequenceInTransition = transitionDuration > 0.001f;
                runtime.sequenceTransitionTime = 0f;
            }

            runtime.sequencePhaseIndex = targetPhase;
            runtime.sequencePhaseTime = 0f;

            // 强制切阶段也必须从头开始播（典型：退出锁到 Released）。
            RestartPhasePlayables(runtime, targetPhase);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var dbg = StateMachineDebugSettings.Instance;
            bool allowThisFrameLog = false;
            if (dbg != null && dbg.IsAnimationBlendEnabled && _debugLogPhase4)
            {
                int frame = Time.frameCount;
                if (_debugLastPhaseOutputLogFrame != frame)
                {
                    _debugLastPhaseOutputLogFrame = frame;
                    allowThisFrameLog = true;
                }
            }
#endif

            if (runtime.phaseUsesCalculator != null && runtime.phaseUsesCalculator[phaseIndex])
            {
                var phaseRuntime = runtime.phaseRuntimes[phaseIndex];
                if (phase.phaseCalculator != null && phaseRuntime != null)
                {
                    phase.phaseCalculator.UpdateWeights(phaseRuntime, context, deltaTime);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (allowThisFrameLog)
                    {
                        dbg?.LogAnimationBlend($"[Phase4] 阶段输出更新(子计算器) | 阶段={phaseIndex} {phase.phaseName} | 计算器={phase.phaseCalculator.GetType().Name} | dt={deltaTime:F3}");
                    }
#endif
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                else if (allowThisFrameLog)
                {
                    string calcName = phase.phaseCalculator != null ? phase.phaseCalculator.GetType().Name : "(空)";
                    string rt = phaseRuntime != null ? "有效" : "空";
                    dbg?.LogAnimationBlend($"[Phase4] 阶段输出更新被跳过(子计算器) | 阶段={phaseIndex} {phase.phaseName} | 计算器={calcName} | Runtime={rt}");
                }
#endif
                return;
            }

            if (!phase.enableSecondaryClipBlend || phase.secondaryClip == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (allowThisFrameLog)
                {
                    if (phase.enableSecondaryClipBlend && phase.secondaryClip == null)
                    {
                        dbg?.LogAnimationBlend($"[Phase4] 阶段未能启用主次混合：次Clip为空 | 阶段={phaseIndex} {phase.phaseName}");
                    }
                    else
                    {
                        // 你问的“只有一个动画（无次Clip/未启用主次混合）走哪条路径”：就是这里直接返回，不会走主次混合更新。
                        dbg?.LogAnimationBlend($"[Phase4] 阶段输出更新(单Clip/无混合) | 阶段={phaseIndex} {phase.phaseName} | enableSecondaryClipBlend={phase.enableSecondaryClipBlend}");
                    }
                }
#endif
                return;
            }

            if (runtime.phaseMixers == null || !runtime.phaseMixers[phaseIndex].IsValid())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (allowThisFrameLog)
                {
                    dbg?.LogAnimationBlend($"[Phase4] 阶段主次混合被跳过：phaseMixers无效 | 阶段={phaseIndex} {phase.phaseName}");
                }
#endif
                return;
            }

            float targetBlend = Mathf.Clamp01(context.GetFloat(phase.blendParameter, 0f));

            if (runtime.phaseBlendWeights == null || runtime.phaseBlendVelocities == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (allowThisFrameLog)
                {
                    dbg?.LogAnimationBlend($"[Phase4] 阶段主次混合被跳过：权重缓存为空 | 阶段={phaseIndex} {phase.phaseName}");
                }
#endif
                return;
            }

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (allowThisFrameLog)
            {
                string pName = phase.primaryClip != null ? phase.primaryClip.name : "None";
                string sName = phase.secondaryClip != null ? phase.secondaryClip.name : "None";
                dbg?.LogAnimationBlend(
                    $"[Phase4] 阶段输出更新(主次混合) | 阶段={phaseIndex} {phase.phaseName} | " +
                    $"目标={targetBlend:F2} 实际={runtime.phaseBlendWeights[phaseIndex]:F2} | 主={pName}({w0:F2}) 次={sName}({w1:F2})");
            }
#endif
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
