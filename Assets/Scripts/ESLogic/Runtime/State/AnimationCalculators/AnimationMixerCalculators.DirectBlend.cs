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
      // ==================== Direct混合 ====================

        /// <summary>
        /// Direct混合 - 每个Clip独立控制权重
        /// 典型应用: 面部表情、手指动画、多层BlendShape
        /// 性能: O(n)权重更新
        /// 支持多层级嵌套，Mixer可连接到父Mixer
        /// </summary>
        [Serializable, TypeRegistryItem("直接权重混合")]
        // 对应枚举：DirectBlend
        public class StateAnimationMixCalculatorForDirectBlend : StateAnimationMixCalculator
        {
            [Serializable]
            public class DirectClip
            {
                public AnimationClip clip;
                public string weightParameter;  // 权重参数名
                public float defaultWeight;     // 默认权重
            }

            [Serializable]
            public class WeightEvent
            {
                [LabelText("事件名")]
                public string eventName;

                [LabelText("关联Clip索引")]
                public int clipIndex;

                [LabelText("触发阈值"), Range(0f, 1f)]
                public float threshold;

                [LabelText("上升触发"), Tooltip("权重从低到高穿越阈值时触发")]
                public bool triggerOnRising;

                [LabelText("下降触发"), Tooltip("权重从高到低穿越阈值时触发")]
                public bool triggerOnFalling;
            }

            public DirectClip[] clips = new DirectClip[0];
            public bool autoNormalize = false;  // 是否自动归一化权重

            [BoxGroup("事件回调")]
            [LabelText("权重事件")]
            public WeightEvent[] weightEvents = new WeightEvent[0];

            [BoxGroup("事件回调")]
            [LabelText("事件参数名前缀"), Tooltip("触发事件时写入运行时参数的名称前缀")]
            public string eventParamPrefix = "OnWeight_";

            public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.DirectBlend;

            public override bool NeedUpdateWhenFadingOut => true;

            protected override string GetUsageHelp()
            {
                 return "适用：表情/手指/局部动作叠加。\n" +
                        "必填：若干叠加层（动画片段 + 权重驱动参数）。\n" +
                        "可选：权重归一化、权重事件（阈值触发）。";
            }

            protected override string GetCalculatorDisplayName()
            {
                return "直接权重混合";
            }

            [Range(0f, 1f)]
            public float smoothTime = 0.05f;
            
            private bool _isCalculatorInitialized;  // 标记Calculator是否已初始化（享元数据）

#if UNITY_EDITOR
            /// <summary>
            /// 编辑器初始化标准采样组 - 为Direct混合提供标准插槽
            /// </summary>
            [Button("初始化标准采样(4个插槽)"), PropertyOrder(-1)]
            private void InitializeStandardSamples()
            {
                if (clips != null && clips.Length > 0)
                {
                    if (!UnityEditor.EditorUtility.DisplayDialog(
                        "确认初始化",
                        $"当前已有{clips.Length}个Clip，是否覆盖为标准配置？\n标准配置：4个独立控制插槽",
                        "确认覆盖", "取消"))
                    {
                        return;
                    }
                }

                clips = new DirectClip[4]
                {
                    new DirectClip { clip = null, weightParameter = "Weight0", defaultWeight = 0f },
                    new DirectClip { clip = null, weightParameter = "Weight1", defaultWeight = 0f },
                    new DirectClip { clip = null, weightParameter = "Weight2", defaultWeight = 0f },
                    new DirectClip { clip = null, weightParameter = "Weight3", defaultWeight = 0f }
                };
                
                _isCalculatorInitialized = false; // 重置初始化标记

                StateMachineDebugSettings.Instance.LogRuntimeInit("[DirectBlend] 已初始化4个标准插槽");
            }
#endif

            /// <summary>
            /// 计算器初始化 - 仅执行一次，所有Runtime共享
            /// DirectBlend无需预计算（已经是直接映射），但保持接口一致性
            /// </summary>
            public override void InitializeCalculator()
            {
                if (_isCalculatorInitialized || clips == null || clips.Length == 0)
                    return;

                // DirectBlend无需预计算，但可以在此进行验证
                // 例如：检查参数名是否重复、默认权重是否合法等
                _isCalculatorInitialized = true;
            }

            protected override bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
            {
                // 确保Calculator已初始化
                InitializeCalculator();
                
                if (clips == null || clips.Length == 0)
                {
                    StateMachineDebugSettings.Instance.LogError("[DirectBlend] Clip列表为空");
                    return false;
                }

                // 创建Mixer(可被父Mixer连接)
                runtime.mixer = AnimationMixerPlayable.Create(graph, clips.Length);

                // 创建所有Clip的Playable(索引固定)
                runtime.playables = new AnimationClipPlayable[clips.Length];
                runtime.currentWeights = new float[clips.Length];
                runtime.targetWeights = new float[clips.Length];
                runtime.weightVelocities = new float[clips.Length];
                runtime.weightTargetCache = new float[clips.Length]; // 作为事件的上帧权重缓存

                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i].clip != null)
                    {
                        runtime.playables[i] = AnimationClipPlayable.Create(graph, clips[i].clip);
                        graph.Connect(runtime.playables[i], 0, runtime.mixer, i);
                        runtime.mixer.SetInputWeight(i, clips[i].defaultWeight);
                        runtime.currentWeights[i] = clips[i].defaultWeight;
                        runtime.weightTargetCache[i] = clips[i].defaultWeight;
                    }
                }

                // 输出Mixer供父级连接(支持多层级)
                output = runtime.mixer;
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
            {
                if (!runtime.mixer.IsValid())
                    return;

                // 缓存上帧权重（用于事件触发判断）
                if (runtime.weightTargetCache != null && runtime.weightTargetCache.Length == runtime.currentWeights.Length)
                {
                    for (int i = 0; i < runtime.currentWeights.Length; i++)
                        runtime.weightTargetCache[i] = runtime.currentWeights[i];
                }

                // 通过context读取所有参数 - 零GC
                for (int i = 0; i < clips.Length; i++)
                {
                    string paramName = clips[i].weightParameter;
                    runtime.targetWeights[i] = string.IsNullOrEmpty(paramName)
                        ? clips[i].defaultWeight
                        : context.GetFloat(paramName, clips[i].defaultWeight);
                }

                // 归一化(可选)
                if (autoNormalize)
                {
                    float sum = 0f;
                    for (int i = 0; i < runtime.targetWeights.Length; i++)
                        sum += runtime.targetWeights[i];

                    if (sum > 0.001f)
                    {
                        for (int i = 0; i < runtime.targetWeights.Length; i++)
                            runtime.targetWeights[i] /= sum;
                    }
                }

                // 平滑过渡 - 零GC
                for (int i = 0; i < clips.Length; i++)
                {
                    if (smoothTime > 0.001f)
                    {
                        runtime.currentWeights[i] = Mathf.SmoothDamp(
                            runtime.currentWeights[i],
                            runtime.targetWeights[i],
                            ref runtime.weightVelocities[i],
                            smoothTime,
                            float.MaxValue,
                            deltaTime
                        );
                    }
                    else
                    {
                        runtime.currentWeights[i] = runtime.targetWeights[i];
                    }

                    runtime.mixer.SetInputWeight(i, runtime.currentWeights[i]);
                }

                // 触发权重事件（基于上/下穿越阈值）
                if (weightEvents != null && weightEvents.Length > 0)
                {
                    for (int i = 0; i < weightEvents.Length; i++)
                    {
                        var evt = weightEvents[i];
                        if (evt.clipIndex < 0 || evt.clipIndex >= runtime.currentWeights.Length)
                            continue;

                        float current = runtime.currentWeights[evt.clipIndex];
                        float previous = runtime.weightTargetCache != null && evt.clipIndex < runtime.weightTargetCache.Length
                            ? runtime.weightTargetCache[evt.clipIndex]
                            : current;

                        bool rising = evt.triggerOnRising && previous < evt.threshold && current >= evt.threshold;
                        bool falling = evt.triggerOnFalling && previous > evt.threshold && current <= evt.threshold;

                        if (rising || falling)
                        {
                            string paramName = string.IsNullOrEmpty(evt.eventName)
                                ? eventParamPrefix + evt.clipIndex
                                : eventParamPrefix + evt.eventName;

                            context.SetTrigger(paramName);
                        }
                    }
                }
            }

            /// <summary>
            /// DirectBlend 首帧立即更新 — 跳过权重SmoothDamp，
            /// 直接从 Context 读取目标权重并立即应用到 Mixer，
            /// 同时重置 weightVelocities 避免残留速度影响后续帧。
            /// </summary>
            public override void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
            {
                if (runtime == null || !runtime.mixer.IsValid()) return;

                // 读取目标权重
                for (int i = 0; i < clips.Length; i++)
                {
                    string paramName = clips[i].weightParameter;
                    runtime.targetWeights[i] = string.IsNullOrEmpty(paramName)
                        ? clips[i].defaultWeight
                        : context.GetFloat(paramName, clips[i].defaultWeight);
                }

                // 归一化(如果启用)
                if (autoNormalize)
                {
                    float sum = 0f;
                    for (int i = 0; i < runtime.targetWeights.Length; i++)
                        sum += runtime.targetWeights[i];
                    if (sum > 0.001f)
                    {
                        for (int i = 0; i < runtime.targetWeights.Length; i++)
                            runtime.targetWeights[i] /= sum;
                    }
                }

                // 权重直接到位，跳过SmoothDamp
                for (int i = 0; i < clips.Length; i++)
                {
                    runtime.currentWeights[i] = runtime.targetWeights[i];
                    runtime.weightVelocities[i] = 0f;
                    runtime.mixer.SetInputWeight(i, runtime.currentWeights[i]);
                }

                // 同步weightTargetCache（避免首帧事件误触发）
                if (runtime.weightTargetCache != null)
                {
                    for (int i = 0; i < runtime.currentWeights.Length && i < runtime.weightTargetCache.Length; i++)
                        runtime.weightTargetCache[i] = runtime.currentWeights[i];
                }
            }

            public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
            {
                // 返回权重最高的Clip
                if (clips.Length == 0 || runtime.currentWeights == null || runtime.playables == null)
                    return null;

                int maxIndex = 0;
                float maxWeight = runtime.currentWeights[0];

                for (int i = 1; i < runtime.currentWeights.Length; i++)
                {
                    if (runtime.currentWeights[i] > maxWeight)
                    {
                        maxWeight = runtime.currentWeights[i];
                        maxIndex = i;
                    }
                }

                return runtime.playables[maxIndex].GetAnimationClip();
            }
            
            /// <summary>
            /// 运行时覆盖Clip - 索引固定，仅替换内容
            /// </summary>
            public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
            {
                if (clipIndex < 0 || clipIndex >= clips.Length)
                {
                    StateMachineDebugSettings.Instance.LogError($"[DirectBlend] 索引越界: {clipIndex} (有效范围: 0-{clips.Length - 1})");
                    return false;
                }
                
                if (runtime.playables == null || !runtime.playables[clipIndex].IsValid())
                {
                    StateMachineDebugSettings.Instance.LogError($"[DirectBlend] Playable[{clipIndex}]无效");
                    return false;
                }
                
                if (newClip == null)
                {
                    StateMachineDebugSettings.Instance.LogError("[DirectBlend] 新Clip为null");
                    return false;
                }
                
                // 零GC替换：保持索引和连接，仅替换Playable
                var graph = runtime.playables[clipIndex].GetGraph();
                var oldSpeed = runtime.playables[clipIndex].GetSpeed();
                var oldTime = runtime.playables[clipIndex].GetTime();
                var currentWeight = runtime.mixer.GetInputWeight(clipIndex);
                
                // 断开连接
                runtime.mixer.DisconnectInput(clipIndex);
                runtime.playables[clipIndex].Destroy();
                
                // 创建新Playable并重新连接
                runtime.playables[clipIndex] = AnimationClipPlayable.Create(graph, newClip);
                runtime.playables[clipIndex].SetSpeed(oldSpeed);
                runtime.playables[clipIndex].SetTime(oldTime);
                graph.Connect(runtime.playables[clipIndex], 0, runtime.mixer, clipIndex);
                runtime.mixer.SetInputWeight(clipIndex, currentWeight);
                
                clips[clipIndex].clip = newClip;
                return true;
            }

            public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
            {
                // DirectBlend: 返回当前权重最大的Clip的长度
                if (runtime.currentWeights == null || runtime.currentWeights.Length == 0)
                    return 0f;

                int maxWeightIndex = 0;
                float maxWeight = 0f;
                for (int i = 0; i < runtime.currentWeights.Length; i++)
                {
                    if (runtime.currentWeights[i] > maxWeight)
                    {
                        maxWeight = runtime.currentWeights[i];
                        maxWeightIndex = i;
                    }
                }

                if (maxWeightIndex < clips.Length && clips[maxWeightIndex].clip != null)
                {
                    return clips[maxWeightIndex].clip.length;
                }
                return 0f;
            }
        }

}
