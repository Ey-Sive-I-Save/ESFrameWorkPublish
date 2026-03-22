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
    // ==================== 1D混合树 ====================

        /// <summary>
        /// 1D混合树 - 单参数线性混合
        /// 典型应用: Idle → Walk → Run → Sprint
        /// 性能: O(log n)查找 + O(1)插值
        /// 支持多层级嵌套，Mixer可连接到父Mixer
        /// </summary>
        [Serializable, TypeRegistryItem("简易·1D混合树")]
        // 对应枚举：BlendTree1D
        public class StateAnimationMixCalculatorForBlendTree1D : StateAnimationMixCalculator
        {
            [Serializable]
            public class ClipSampleForBlend1D
            {
                public AnimationClip clip;
                public float threshold;  // 阈值位置
            }

            public StateParameter parameterFloat = StateDefaultFloatParameter.Speed;
            public ClipSampleForBlend1D[] samples = new ClipSampleForBlend1D[0];

            [BoxGroup("曲线阈值")]
            [LabelText("启用曲线阈值"), Tooltip("将输入值通过曲线映射到阈值空间")]
            public bool useCustomThresholdCurve = false;

            [BoxGroup("曲线阈值")]
            [LabelText("输入最小值")]
            public float inputMin = 0f;

            [BoxGroup("曲线阈值")]
            [LabelText("输入最大值")]
            public float inputMax = 1f;

            [BoxGroup("曲线阈值")]
            [LabelText("阈值曲线"), Tooltip("X=输入(0-1), Y=输出(0-1)")]
            public AnimationCurve thresholdCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.BlendTree1D;

            public override bool NeedUpdateWhenFadingOut => true;

            protected override string GetUsageHelp()
            {
                 return "适用：速度驱动的线性混合（Idle→Walk→Run→Sprint）。\n" +
                        "必填：若干采样点（动画片段 + 阈值），以及 1 个用于驱动的 float 参数。\n" +
                        "可选：输入平滑、输入映射曲线。\n" +
                        "建议：阈值严格递增。";
            }

            protected override string GetCalculatorDisplayName()
            {
                return "简易·1D混合树";
            }

            [Range(0f, 1f)]
            public float smoothTime = 0.1f;

#if UNITY_EDITOR
            /// <summary>
            /// 编辑器初始化标准采样组 - 为1D混合树提供标准阈值采样点
            /// </summary>
            [Button("初始化标准采样(Idle-Walk-Run)"), PropertyOrder(-1)]
            private void InitializeStandardSamples()
            {
                if (samples != null && samples.Length > 0)
                {
                    if (!UnityEditor.EditorUtility.DisplayDialog(
                        "确认初始化",
                        "当前已有采样点，是否覆盖为标准配置？\n标准配置：Idle(0) → Walk(0.5) → Run(1.0)",
                        "确认覆盖", "取消"))
                    {
                        return;
                    }
                }

                samples = new ClipSampleForBlend1D[3]
                {
                    new ClipSampleForBlend1D { threshold = 0f, clip = null },
                    new ClipSampleForBlend1D { threshold = 0.5f, clip = null },
                    new ClipSampleForBlend1D { threshold = 1.0f, clip = null }
                };
                
                
                StateMachineDebugSettings.Instance.LogRuntimeInit("[BlendTree1D] 已初始化3个标准采样点");
            }
#endif

            /// <summary>
            /// 计算器初始化 - 仅执行一次，所有Runtime共享
            /// </summary>
            public override void InitializeCalculator()
            {
                if (samples == null || samples.Length == 0)
                    return;

                // 按阈值排序(享元数据，一次性计算)
                Array.Sort(samples, (a, b) => a.threshold.CompareTo(b.threshold));
            }

            protected override bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
            {
                if (samples == null || samples.Length == 0)
                {
                    StateMachineDebugSettings.Instance.LogError("[BlendTree1D] 采样点为空");
                    return false;
                }

                if (samples.Length == 1 && samples[0].clip != null)
                {
                    runtime.singlePlayable = AnimationClipPlayable.Create(graph, samples[0].clip);
                    output = runtime.singlePlayable;
                    return true;
                }

                // 创建Mixer(可被父Mixer连接)
                runtime.mixer = AnimationMixerPlayable.Create(graph, samples.Length);

                // 创建所有Clip的Playable(索引固定)
                runtime.playables = new AnimationClipPlayable[samples.Length];
                
                // 初始化权重缓存系统
                runtime.weightCache = new float[samples.Length];
                runtime.weightTargetCache = new float[samples.Length];
                runtime.weightVelocityCache = new float[samples.Length];
                runtime.useSmoothing = smoothTime > 0.001f;
                
                for (int i = 0; i < samples.Length; i++)
                {
                    if (samples[i].clip != null)
                    {
                        runtime.playables[i] = AnimationClipPlayable.Create(graph, samples[i].clip);
                        graph.Connect(runtime.playables[i], 0, runtime.mixer, i);
                        runtime.mixer.SetInputWeight(i, 0f);
                        runtime.weightCache[i] = 0f;
                    }
                }

                // 输出Mixer供父级连接(支持多层级)
                output = runtime.mixer;
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
            {
                if (!runtime.mixer.IsValid() || samples.Length == 0)
                    return;

                // 通过context获取参数（直接传StateParameter，零GC）
                float rawInput = context.GetFloat(parameterFloat, 0f);

                // 自定义曲线阈值映射
                float mappedInput = useCustomThresholdCurve
                    ? MapInputByCurve(rawInput)
                    : rawInput;

                // 平滑输入(零GC)
                float input = smoothTime > 0.001f
                    ? Mathf.SmoothDamp(runtime.lastInput, mappedInput, ref runtime.inputVelocity, smoothTime, float.MaxValue, deltaTime)
                    : mappedInput;
                runtime.lastInput = input;

                // 零GC计算权重（使用缓存和平滑）
                CalculateWeights1D(runtime, input, deltaTime);
            }

            private void CalculateWeights1D(AnimationCalculatorRuntime runtime, float input, float deltaTime)
            {
                int count = samples.Length;

                // 计算目标权重（不直接应用，先缓存）
                for (int i = 0; i < count; i++)
                    runtime.weightTargetCache[i] = 0f;

                // 边界情况
                if (input <= samples[0].threshold)
                {
                    runtime.weightTargetCache[0] = 1f;
                }
                else if (input >= samples[count - 1].threshold)
                {
                    runtime.weightTargetCache[count - 1] = 1f;
                }
                else
                {
                    // 二分查找 O(log n)
                    int rightIndex = BinarySearchRight(input);
                    int leftIndex = rightIndex - 1;

                    // 线性插值
                    float leftThreshold = samples[leftIndex].threshold;
                    float rightThreshold = samples[rightIndex].threshold;
                    float t = (input - leftThreshold) / (rightThreshold - leftThreshold);

                    // 设置权重(仅2个Clip)
                    runtime.weightTargetCache[leftIndex] = 1f - t;
                    runtime.weightTargetCache[rightIndex] = t;
                }

                // 平滑过渡到目标权重（避免僵硬跳变）
                float smoothingSpeed = smoothTime > 0.001f ? smoothTime * 0.5f : 0.001f;
                for (int i = 0; i < count; i++)
                {
                    if (runtime.useSmoothing)
                    {
                        runtime.weightCache[i] = Mathf.SmoothDamp(
                            runtime.weightCache[i],
                            runtime.weightTargetCache[i],
                            ref runtime.weightVelocityCache[i],
                            smoothingSpeed,
                            float.MaxValue,
                            deltaTime
                        );
                    }
                    else
                    {
                        runtime.weightCache[i] = runtime.weightTargetCache[i];
                    }
                    runtime.mixer.SetInputWeight(i, runtime.weightCache[i]);
                }
            }

            private float MapInputByCurve(float rawInput)
            {
                float min = inputMin;
                float max = inputMax;
                if (Mathf.Abs(max - min) < 0.0001f)
                    return rawInput;

                float t = Mathf.InverseLerp(min, max, rawInput);
                float curveT = thresholdCurve != null ? thresholdCurve.Evaluate(t) : t;

                // 映射到阈值范围
                float minThreshold = samples.Length > 0 ? samples[0].threshold : 0f;
                float maxThreshold = samples.Length > 0 ? samples[samples.Length - 1].threshold : 1f;
                return Mathf.Lerp(minThreshold, maxThreshold, Mathf.Clamp01(curveT));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private int BinarySearchRight(float value)
            {
                int left = 0;
                int right = samples.Length - 1;

                while (left < right)
                {
                    int mid = (left + right) / 2;
                    if (samples[mid].threshold < value)
                        left = mid + 1;
                    else
                        right = mid;
                }

                return right;
            }

            /// <summary>
            /// BlendTree1D 首帧立即更新 — 跳过输入SmoothDamp和权重SmoothDamp，
            /// 直接根据 Context 参数计算并应用最终权重。
            /// 同时重置 inputVelocity 和 weightVelocityCache 避免残留速度影响后续帧。
            /// </summary>
            public override void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
            {
                if (runtime == null || !runtime.mixer.IsValid() || samples.Length == 0) return;

                // 直接读取参数，跳过输入平滑
                float rawInput = context.GetFloat(parameterFloat, 0f);
                float mappedInput = useCustomThresholdCurve ? MapInputByCurve(rawInput) : rawInput;

                // 立即设置 lastInput（后续帧从此值开始平滑）
                runtime.lastInput = mappedInput;
                runtime.inputVelocity = 0f;

                // 直接计算目标权重
                int count = samples.Length;
                for (int i = 0; i < count; i++)
                    runtime.weightTargetCache[i] = 0f;

                if (mappedInput <= samples[0].threshold)
                {
                    runtime.weightTargetCache[0] = 1f;
                }
                else if (mappedInput >= samples[count - 1].threshold)
                {
                    runtime.weightTargetCache[count - 1] = 1f;
                }
                else
                {
                    int rightIndex = BinarySearchRight(mappedInput);
                    int leftIndex = rightIndex - 1;
                    float leftThreshold = samples[leftIndex].threshold;
                    float rightThreshold = samples[rightIndex].threshold;
                    float t = (mappedInput - leftThreshold) / (rightThreshold - leftThreshold);
                    runtime.weightTargetCache[leftIndex] = 1f - t;
                    runtime.weightTargetCache[rightIndex] = t;
                }

                // 权重直接到位，跳过SmoothDamp
                for (int i = 0; i < count; i++)
                {
                    runtime.weightCache[i] = runtime.weightTargetCache[i];
                    runtime.weightVelocityCache[i] = 0f;
                    runtime.mixer.SetInputWeight(i, runtime.weightCache[i]);
                }
            }

            public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
            {
                if (runtime.singlePlayable.IsValid())
                {
                    return runtime.singlePlayable.GetAnimationClip();
                }

                // 返回权重最大的Clip
                if (samples.Length == 0 || runtime.playables == null)
                    return null;
                    
                int maxWeightIndex = 0;
                float maxWeight = runtime.mixer.GetInputWeight(0);
                
                for (int i = 1; i < samples.Length; i++)
                {
                    float weight = runtime.mixer.GetInputWeight(i);
                    if (weight > maxWeight)
                    {
                        maxWeight = weight;
                        maxWeightIndex = i;
                    }
                }
                
                return runtime.playables[maxWeightIndex].GetAnimationClip();
            }
            
            /// <summary>
            /// 运行时覆盖Clip - 索引固定，仅替换内容
            /// </summary>
            public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
            {
                if (samples.Length == 1 && clipIndex == 0 && runtime.singlePlayable.IsValid())
                {
                    var singleGraph = runtime.singlePlayable.GetGraph();
                    var singleOldSpeed = runtime.singlePlayable.GetSpeed();
                    var singleOldTime = runtime.singlePlayable.GetTime();

                    runtime.singlePlayable.Destroy();
                    runtime.singlePlayable = AnimationClipPlayable.Create(singleGraph, newClip);
                    runtime.singlePlayable.SetSpeed(singleOldSpeed);
                    runtime.singlePlayable.SetTime(singleOldTime);

                    samples[0].clip = newClip;
                    return true;
                }

                if (clipIndex < 0 || clipIndex >= samples.Length)
                {
                    StateMachineDebugSettings.Instance.LogError($"[BlendTree1D] 索引越界: {clipIndex} (有效范围: 0-{samples.Length - 1})");
                    return false;
                }
                
                if (runtime.playables == null || !runtime.playables[clipIndex].IsValid())
                {
                    StateMachineDebugSettings.Instance.LogError($"[BlendTree1D] Playable[{clipIndex}]无效");
                    return false;
                }
                
                if (newClip == null)
                {
                    StateMachineDebugSettings.Instance.LogError("[BlendTree1D] 新Clip为null");
                    return false;
                }
                
                // 零GC替换：保持索引和连接，仅替换Playable
                var graph = runtime.playables[clipIndex].GetGraph();
                var oldSpeed = runtime.playables[clipIndex].GetSpeed();
                var oldTime = runtime.playables[clipIndex].GetTime();
                
                // 断开连接
                runtime.mixer.DisconnectInput(clipIndex);
                runtime.playables[clipIndex].Destroy();
                
                // 创建新Playable并重新连接
                runtime.playables[clipIndex] = AnimationClipPlayable.Create(graph, newClip);
                runtime.playables[clipIndex].SetSpeed(oldSpeed);
                runtime.playables[clipIndex].SetTime(oldTime);
                graph.Connect(runtime.playables[clipIndex], 0, runtime.mixer, clipIndex);
                
                samples[clipIndex].clip = newClip;
                return true;
            }

            public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
            {
                if (runtime.singlePlayable.IsValid())
                {
                    var clip = runtime.singlePlayable.GetAnimationClip();
                    return clip != null ? clip.length : 0f;
                }

                // BlendTree1D: 返回当前权重最大的Clip的长度
                if (runtime.weightCache == null || runtime.weightCache.Length == 0)
                    return 0f;

                int maxWeightIndex = 0;
                float maxWeight = 0f;
                for (int i = 0; i < runtime.weightCache.Length; i++)
                {
                    if (runtime.weightCache[i] > maxWeight)
                    {
                        maxWeight = runtime.weightCache[i];
                        maxWeightIndex = i;
                    }
                }

                if (maxWeightIndex < samples.Length && samples[maxWeightIndex].clip != null)
                {
                    return samples[maxWeightIndex].clip.length;
                }
                return 0f;
            }
        }
}
