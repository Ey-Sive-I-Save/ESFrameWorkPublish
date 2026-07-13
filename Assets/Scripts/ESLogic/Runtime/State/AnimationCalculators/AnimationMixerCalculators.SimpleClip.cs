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
      // ==================== 单Clip计算器 ====================

    /// <summary>
    /// 简单Clip - 直接播放单个AnimationClip
    /// 支持运行时Clip覆盖，可接入任意Mixer
    /// </summary>
    [Serializable, TypeRegistryItem("简易·单Clip播放")]
    // 对应枚举：SimpleClip
    public class StateAnimationMixCalculatorForSimpleClip : StateAnimationMixCalculator
    {
        public AnimationClip clip;

        public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.SimpleClip;

        protected override string GetUsageHelp()
        {
             return "适用：单段动作（待机/受击/起跳/落地）。\n" +
                    "必填：动画片段。\n" +
                    "可选：播放速度。\n" +
                    "特点：性能最优，逻辑最简单。";
        }

        protected override string GetCalculatorDisplayName()
        {
            return "简易·单Clip播放";
        }

            [Range(0f, 3f)]
            public float speed = 1f;
            
            private bool _isCalculatorInitialized;  // 标记Calculator是否已初始化（享元数据）

            /// <summary>
            /// 计算器初始化 - 仅执行一次，所有Runtime共享
            /// SimpleClip无需预计算（只有一个Clip），但保持接口一致性
            /// </summary>
            public override void InitializeCalculator()
            {
                if (_isCalculatorInitialized)
                    return;

                // SimpleClip无需预计算，仅做基础验证
                if (clip == null)
                {
                    StateMachineDebugSettings.Instance.LogWarning("[SimpleClip] Clip未设置，请在Inspector中配置");
                }
                
                _isCalculatorInitialized = true;
            }

            protected override bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
            {
                if (clip == null)
                {
                    StateMachineDebugSettings.Instance.LogError("[SimpleClip] Clip未设置");
                    return false;
                }

                // 创建Playable(可被任意Mixer连接)
                runtime.singlePlayable = AnimationClipPlayable.Create(graph, clip);
                runtime.singlePlayable.SetSpeed(speed);

                // 单Clip直连输出，避免额外Mixer
                output = runtime.singlePlayable;
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
            {
                // 单Clip无需更新权重，但可通过context支持动态速度
                // 示例: runtime.singlePlayable.SetSpeed(context.GetFloat("Speed", speed));
                // 单Clip权重由外部State权重控制
            }

            /// <summary>
            /// SimpleClip 首帧立即更新 — 单Clip无内部混合，无需操作。
            /// </summary>
            public override void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
            {
                // 单Clip权重完全由外部管线(FadeIn)控制，无内部混合需要对齐
            }

            public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
            {
                // 如果Clip被覆盖，返回运行时的Clip
                if (runtime.singlePlayable.IsValid())
                {
                    return runtime.singlePlayable.GetAnimationClip();
                }
                return clip;
            }
            
            /// <summary>
            /// 运行时覆盖Clip - 索引0固定
            /// </summary>
            public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
            {
                if (clipIndex != 0)
                {
                    StateMachineDebugSettings.Instance.LogError($"[SimpleClip] 索引无效: {clipIndex}，仅支持索引0");
                    return false;
                }
                
                if (!runtime.singlePlayable.IsValid())
                {
                    StateMachineDebugSettings.Instance.LogError("[SimpleClip] Runtime未初始化");
                    return false;
                }
                
                if (newClip == null)
                {
                    StateMachineDebugSettings.Instance.LogError("[SimpleClip] 新Clip为null");
                    return false;
                }
                
                // 零GC替换：销毁旧Playable，创建新Playable
                var graph = runtime.singlePlayable.GetGraph();
                var oldSpeed = runtime.singlePlayable.GetSpeed();
                var oldTime = runtime.singlePlayable.GetTime();
                
                runtime.singlePlayable.Destroy();
                runtime.singlePlayable = AnimationClipPlayable.Create(graph, newClip);
                runtime.singlePlayable.SetSpeed(oldSpeed);
                runtime.singlePlayable.SetTime(oldTime);
                
                return true;
            }

            public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
            {
                // SimpleClip: 返回Clip原始长度（不考虑speed缩放）
                var currentClip = GetCurrentClip(runtime);
                return currentClip != null ? currentClip.length : 0f;
            }
        }

}
