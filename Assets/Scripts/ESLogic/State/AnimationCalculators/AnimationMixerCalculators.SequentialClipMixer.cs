using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sirenix.OdinInspector;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
        // ==================== 序列混合器（前中后三段式） ====================

        /// <summary>
        /// 序列混合器 - 在主动画前后自动插入过渡Clip
        /// 典型应用场景：
        /// 1. 攻击动画：预备(准备姿态) → 主体(挥砍) → 收尾(收刀)
        /// 2. 技能释放：前摇(读条) → 施法(特效) → 后摇(硬直)
        /// 3. 开门动画：推门(Entry) → 穿过(Main) → 关门(Exit)
        /// 4. 换弹动画：拆卸(Entry) → 装填(Main) → 上膛(Exit)
        /// 
        /// 优势：
        /// - 自动管理三段式时间轴，无需手动计算时间点
        /// - 支持独立速度缩放（前摇1x，主体2x，后摇0.5x）
        /// - 支持可选前后Clip（Entry/Exit可为null）
        /// - 零GC实现，运行时无分配
        /// </summary>
        [Serializable, TypeRegistryItem("简易·序列Clip(前-主-后)")]
        // 对应枚举：SequentialClipMixer
        public class SequentialClipMixer : StateAnimationMixCalculator
        {
            [BoxGroup("前置动画(Entry)")]
            [LabelText("前置Clip"), Tooltip("可选，在主动画前播放（如攻击预备、技能前摇）")]
            public AnimationClip entryClip;

            [BoxGroup("前置动画(Entry)")]
            [LabelText("前置速度"), Range(0.1f, 5f), ShowIf("@entryClip != null")]
            public float entrySpeed = 1f;

            [BoxGroup("主动画(Main)"), HideLabel]
            public MainClipConfig mainConfig = new MainClipConfig();

            [BoxGroup("后置动画(Exit)")]
            [LabelText("后置Clip"), Tooltip("可选，在主动画后播放（如攻击收尾、技能后摇）")]
            public AnimationClip exitClip;

            [BoxGroup("后置动画(Exit)")]
            [LabelText("后置速度"), Range(0.1f, 5f), ShowIf("@exitClip != null")]
            public float exitSpeed = 1f;

            [BoxGroup("高级选项")]
            [LabelText("循环主动画"), Tooltip("是否循环播放主动画（前后Clip只播放一次）")]
            public bool loopMainClip = false;

            [BoxGroup("高级选项")]
            [LabelText("允许提前退出"), Tooltip("是否允许在主动画阶段提前退出（跳过后置Clip）")]
            public bool allowEarlyExit = true;

            public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.SequentialClipMixer;

            protected override string GetUsageHelp()
            {
                 return "适用：前摇-主体-后摇（攻击/施法/开门）。\n" +
                        "必填：主体动画。\n" +
                        "可选：前摇/后摇、播放速度、允许提前结束（跳过后摇）。";
            }

            protected override string GetCalculatorDisplayName()
            {
                return "简易·序列Clip(前-主-后)";
            }
            
            private bool _isCalculatorInitialized;  // 标记Calculator是否已初始化（享元数据）

            [Serializable]
            public class MainClipConfig
            {
                [LabelText("主Clip"), Tooltip("必须，核心动画")]
                public AnimationClip clip;

                [LabelText("主速度"), Range(0.1f, 5f)]
                public float speed;

                public MainClipConfig(AnimationClip clip = null, float speed = 1f)
                {
                    this.clip = clip;
                    this.speed = speed;
                }
            }

            /// <summary>
            /// 计算器初始化 - 仅执行一次，所有Runtime共享
            /// SequentialClipMixer无需预计算（序列固定），但保持接口一致性
            /// </summary>
            public override void InitializeCalculator()
            {
                if (_isCalculatorInitialized)
                    return;

                // 验证主Clip必须存在
                if (mainConfig.clip == null)
                {
                    StateMachineDebugSettings.Instance.LogWarning("[SequentialMixer] 主Clip未设置，请在Inspector中配置");
                }

                _isCalculatorInitialized = true;
            }

            public override AnimationCalculatorRuntime CreateRuntimeData()
            {
                var runtime = base.CreateRuntimeData();
                runtime.sequencePhase = 0; // 0=Entry, 1=Main, 2=Exit
                runtime.phaseStartTime = 0f;
                return runtime;
            }

            protected override bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
            {
                // 确保Calculator已初始化
                InitializeCalculator();
                
                if (mainConfig.clip == null)
                {
                    StateMachineDebugSettings.Instance.LogError("[SequentialMixer] 主Clip不能为null");
                    return false;
                }

                // 创建Mixer（3个输入：Entry、Main、Exit）
                runtime.mixer = AnimationMixerPlayable.Create(graph, 3);
                runtime.playables = new AnimationClipPlayable[3];
                
                // ★ 关键修复：分配weightCache，便于外部在需要时“从缓存回写内部Mixer权重”（不再乘 totalWeight）
                // 没有weightCache时，若外部系统修改过内部mixer权重，将难以恢复到计算器期望的内部权重。
                runtime.weightCache = new float[3];

                // 初始化阶段：Entry或Main
                runtime.sequencePhase = (entryClip != null) ? 0 : 1;
                runtime.phaseStartTime = 0f;

                // 创建Entry Playable（可选）
                if (entryClip != null)
                {
                    runtime.playables[0] = AnimationClipPlayable.Create(graph, entryClip);
                    runtime.playables[0].SetSpeed(entrySpeed);
                    // ★ 不使用SetDuration，避免PlayableGraph将Playable标记为Done导致冻结
                    // ★ 非当前阶段暂停（speed=0），防止时间提前推进
                    if (runtime.sequencePhase != 0)
                        runtime.playables[0].Pause();
                    graph.Connect(runtime.playables[0], 0, runtime.mixer, 0);
                }

                // 创建Main Playable（必须）
                runtime.playables[1] = AnimationClipPlayable.Create(graph, mainConfig.clip);
                runtime.playables[1].SetSpeed(mainConfig.speed);
                // ★ 非当前阶段暂停
                if (runtime.sequencePhase != 1)
                    runtime.playables[1].Pause();
                graph.Connect(runtime.playables[1], 0, runtime.mixer, 1);

                // 创建Exit Playable（可选）
                if (exitClip != null)
                {
                    runtime.playables[2] = AnimationClipPlayable.Create(graph, exitClip);
                    runtime.playables[2].SetSpeed(exitSpeed);
                    // ★ Exit始终先暂停（稍后激活时恢复）
                    runtime.playables[2].Pause();
                    graph.Connect(runtime.playables[2], 0, runtime.mixer, 2);
                }

                UpdatePhaseWeights(runtime);

                output = runtime.mixer;

                StateMachineDebugSettings.Instance.LogRuntimeInit(
                    $"[SequentialMixer] 初始化完成: Entry={entryClip?.name ?? "None"}, Main={mainConfig.clip.name}, Exit={exitClip?.name ?? "None"}, StartPhase={runtime.sequencePhase}");
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
            {
                if (!runtime.mixer.IsValid())
                    return;

                runtime.phaseStartTime += deltaTime;

                // 检查当前阶段是否完成，自动切换到下一阶段
                bool phaseCompleted = false;
                switch (runtime.sequencePhase)
                {
                    case 0: // Entry阶段
                        if (entryClip != null && runtime.playables[0].IsValid())
                        {
                            double duration = entryClip.length / entrySpeed;
                            if (runtime.phaseStartTime >= duration)
                            {
                                phaseCompleted = true;
                            }
                        }
                        else
                        {
                            phaseCompleted = true; // Entry不存在，立即进入Main
                        }
                        break;

                    case 1: // Main阶段
                        if (!loopMainClip && mainConfig.clip != null)
                        {
                            double duration = mainConfig.clip.length / mainConfig.speed;
                            if (runtime.phaseStartTime >= duration)
                            {
                                phaseCompleted = true;
                            }
                        }
                        // 循环模式下Main永不完成（除非外部强制退出）
                        break;

                    case 2: // Exit阶段
                        if (exitClip != null && runtime.playables[2].IsValid())
                        {
                            double duration = exitClip.length / exitSpeed;
                            if (runtime.phaseStartTime >= duration)
                            {
                                // Exit完成，整个序列结束
                                runtime.sequenceCompleted = true;
                            }
                        }
                        else
                        {
                            runtime.sequenceCompleted = true; // Exit不存在，序列结束
                        }
                        break;
                }

                // 阶段切换
                if (phaseCompleted)
                {
                    // ★ 暂停当前阶段的Playable（停止时间推进）
                    if (runtime.sequencePhase < 3 && runtime.playables[runtime.sequencePhase].IsValid())
                    {
                        runtime.playables[runtime.sequencePhase].Pause();
                    }

                    runtime.sequencePhase++;
                    runtime.phaseStartTime = 0f;

                    // ★ 恢复新阶段的Playable：重置时间 → 恢复播放
                    if (runtime.sequencePhase < 3 && runtime.playables[runtime.sequencePhase].IsValid())
                    {
                        runtime.playables[runtime.sequencePhase].SetTime(0);
                        runtime.playables[runtime.sequencePhase].Play();
                    }

#if STATEMACHINEDEBUG
                    StateMachineDebugSettings.Instance.LogAnimationBlend(
                        $"[SequentialMixer] 阶段切换: Phase {runtime.sequencePhase - 1} → {runtime.sequencePhase}");
#endif
                }

                // ★ 关键修复：每帧都更新阶段权重（不仅仅是切换时）
                // 外部系统/权重回写可能随时修改mixer权重
                // 必须每帧重新写入正确的内部权重（0/1）
                UpdatePhaseWeights(runtime);
            }

            /// <summary>
            /// 更新三个阶段的权重（只有当前阶段权重为1）
            /// ★ 关键修复：将内部权重(0/1)写入weightCache，便于在外部修改后从缓存恢复
            /// 之前直接写mixer且不维护weightCache，外部改动后可能无法回到计算器期望的内部权重
            /// </summary>
            private void UpdatePhaseWeights(AnimationCalculatorRuntime runtime)
            {
                for (int i = 0; i < 3; i++)
                {
                    float internalWeight = (i == runtime.sequencePhase) ? 1f : 0f;
                    runtime.weightCache[i] = internalWeight;
                    runtime.mixer.SetInputWeight(i, internalWeight);
                }
            }

            /// <summary>
            /// 序列型计算器的首帧立即更新 — 仅刷新当前阶段权重，不推进时间/阶段。
            /// </summary>
            public override void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
            {
                if (runtime == null || !runtime.mixer.IsValid()) return;
                UpdatePhaseWeights(runtime);
            }

            public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
            {
                switch (runtime.sequencePhase)
                {
                    case 0: return entryClip;
                    case 1: return mainConfig.clip;
                    case 2: return exitClip;
                    default: return null;
                }
            }

            public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
            {
                float totalDuration = 0f;

                // Entry时长
                if (entryClip != null)
                    totalDuration += entryClip.length / entrySpeed;

                // Main时长（循环模式返回无限）
                if (loopMainClip)
                    return float.PositiveInfinity;
                if (mainConfig.clip != null)
                    totalDuration += mainConfig.clip.length / mainConfig.speed;

                // Exit时长
                if (exitClip != null)
                    totalDuration += exitClip.length / exitSpeed;

                return totalDuration > 0f ? totalDuration : 0f;
            }

            public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
            {
                if (clipIndex < 0 || clipIndex >= 3)
                {
                    StateMachineDebugSettings.Instance.LogError($"[SequentialMixer] 索引越界: {clipIndex} (0=Entry, 1=Main, 2=Exit)");
                    return false;
                }

                if (clipIndex == 1 && newClip == null)
                {
                    StateMachineDebugSettings.Instance.LogError("[SequentialMixer] Main Clip不能为null");
                    return false;
                }

                if (!runtime.playables[clipIndex].IsValid())
                {
                    StateMachineDebugSettings.Instance.LogWarning($"[SequentialMixer] Playable[{clipIndex}]无效，可能该阶段原本为空");
                    return false;
                }

                // 替换Playable
                var graph = runtime.playables[clipIndex].GetGraph();
                var oldSpeed = runtime.playables[clipIndex].GetSpeed();
                var oldTime = runtime.playables[clipIndex].GetTime();

                runtime.mixer.DisconnectInput(clipIndex);
                runtime.playables[clipIndex].Destroy();

                if (newClip != null)
                {
                    runtime.playables[clipIndex] = AnimationClipPlayable.Create(graph, newClip);
                    runtime.playables[clipIndex].SetSpeed(oldSpeed);
                    runtime.playables[clipIndex].SetTime(oldTime);
                    graph.Connect(runtime.playables[clipIndex], 0, runtime.mixer, clipIndex);
                }

                // 更新配置
                switch (clipIndex)
                {
                    case 0: entryClip = newClip; break;
                    case 1: mainConfig.clip = newClip; break;
                    case 2: exitClip = newClip; break;
                }

                return true;
            }

            /// <summary>
            /// 强制进入Exit阶段（提前退出）
            /// </summary>
            public void ForceExit(AnimationCalculatorRuntime runtime)
            {
                if (!allowEarlyExit || runtime.sequencePhase >= 2)
                    return;

                // ★ 暂停当前阶段
                if (runtime.playables[runtime.sequencePhase].IsValid())
                {
                    runtime.playables[runtime.sequencePhase].Pause();
                }

                runtime.sequencePhase = 2;
                runtime.phaseStartTime = 0f;

                // ★ 恢复Exit阶段：重置时间 → 恢复播放
                if (runtime.playables[2].IsValid())
                {
                    runtime.playables[2].SetTime(0);
                    runtime.playables[2].Play();
                }
                UpdatePhaseWeights(runtime);

#if STATEMACHINEDEBUG
                StateMachineDebugSettings.Instance.LogAnimationBlend("[SequentialMixer] 强制提前退出到Exit阶段");
#endif
            }
        }

}
