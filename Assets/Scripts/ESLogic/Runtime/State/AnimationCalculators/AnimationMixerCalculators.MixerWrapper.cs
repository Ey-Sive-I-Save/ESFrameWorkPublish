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
     // ==================== 混合器包装器（支持嵌套） ====================

        /// <summary>
        /// 混合器包装器 - 支持Calculator嵌套
        /// 用于实现上下半身分离、多层混合等高级功能
        /// 性能: 一层嵌套几乎无影响，推荐深度≤2层
        /// </summary>
        [Serializable, TypeRegistryItem("混合器包装器")]
        // 对应枚举：MixerWrapper
        public class MixerCalculator : StateAnimationMixCalculator
        {
            [SerializeReference, LabelText("子计算器")]
            public StateAnimationMixCalculator childCalculator;

            [LabelText("权重缩放"), Range(0f, 1f), Tooltip("对子Calculator的输出权重进行缩放")]
            public float weightScale = 1f;

            public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.MixerWrapper;

            protected override string GetUsageHelp()
            {
                 return "适用：组合多层混合（上半身/下半身分离）。\n" +
                        "必填：子计算器。\n" +
                        "可选：权重缩放（整体缩放）。";
            }

            protected override string GetCalculatorDisplayName()
            {
                return "混合器包装器";
            }

            /// <summary>
            /// 计算器初始化 - 仅执行一次，所有Runtime共享
            /// MixerCalculator需要递归初始化子Calculator（享元数据一次性计算）
            /// </summary>
            public override void InitializeCalculator()
            {
                // 递归初始化子Calculator
                if (childCalculator != null)
                {
                    childCalculator.InitializeCalculator();
                }
            }

            public override AnimationCalculatorRuntime CreateRuntimeData()
            {
                // 创建运行时数据，包含子Calculator的Runtime
                var runtime = new AnimationCalculatorRuntime();
                if (childCalculator != null)
                {
                    runtime.childRuntime = childCalculator.CreateRuntimeData();
                }
                return runtime;
            }

            protected override bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
            {
                if (childCalculator == null)
                {
                    StateMachineDebugSettings.Instance.LogError("[MixerCalculator] 子计算器为null");
                    return false;
                }

                // 初始化子Calculator，将其输出作为我们的输出
                bool success = childCalculator.InitializeRuntime(runtime.childRuntime, graph, ref output);
                
                if (success)
                {
                    StateMachineDebugSettings.Instance.LogRuntimeInit($"[MixerCalculator] 嵌套初始化成功: {childCalculator.GetType().Name}");
                }
                
                return success;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
            {
                if (childCalculator != null && runtime.childRuntime != null)
                {
                    // 递归更新子Calculator
                    childCalculator.UpdateWeights(runtime.childRuntime, context, deltaTime);
                }
            }

            /// <summary>
            /// 嵌套计算器的首帧立即更新 — 递归委托给子计算器。
            /// </summary>
            public override void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
            {
                if (childCalculator != null && runtime.childRuntime != null)
                {
                    childCalculator.ImmediateUpdate(runtime.childRuntime, context);
                }
            }

            public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
            {
                if (childCalculator != null && runtime.childRuntime != null)
                {
                    return childCalculator.GetCurrentClip(runtime.childRuntime);
                }
                return null;
            }

            public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
            {
                if (childCalculator != null && runtime.childRuntime != null)
                {
                    return childCalculator.GetStandardDuration(runtime.childRuntime);
                }
                return 0f;
            }

            public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
            {
                if (childCalculator != null && runtime.childRuntime != null)
                {
                    return childCalculator.OverrideClip(runtime.childRuntime, clipIndex, newClip);
                }
                return false;
            }
        }

}
