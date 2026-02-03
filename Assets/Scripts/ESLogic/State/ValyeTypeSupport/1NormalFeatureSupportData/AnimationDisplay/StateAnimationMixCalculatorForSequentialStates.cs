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
    [Serializable, TypeRegistryItem("序列状态播放器")]
    public class StateAnimationMixCalculatorForSequentialStates : StateAnimationMixCalculator
    {
        /// <summary>
        /// 顺序阶段定义
        /// </summary>
        [Serializable]
        public struct SequentialPhase
        {
            [LabelText("阶段名称")]
            public string phaseName;
            
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
        public void InitializeCalculator()
        {
            if (_isCalculatorInitialized || phases == null || phases.Length == 0)
                return;

            // 验证配置
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].primaryClip == null)
                {
                    Debug.LogWarning($"[SequentialStates] 阶段{i} ({phases[i].phaseName}) 缺少主Clip");
                }
                if (phases[i].minDuration < 0f)
                {
                    Debug.LogError($"[SequentialStates] 阶段{i} ({phases[i].phaseName}) minDuration不能为负");
                    phases[i].minDuration = 0f;
                }
            }

            _isCalculatorInitialized = true;
        }

        public override bool InitializeRuntime(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
        {
            InitializeCalculator();

            if (phases == null || phases.Length == 0)
            {
                Debug.LogError("[SequentialStates] 阶段列表为空");
                return false;
            }

            // 创建Mixer（每个阶段占2个输入：主Clip + 次Clip）
            int totalInputs = phases.Length * 2;
            runtime.mixer = AnimationMixerPlayable.Create(graph, totalInputs);

            // 创建所有Playables
            runtime.playables = new AnimationClipPlayable[totalInputs];
            runtime.weightCache = new float[totalInputs];
            runtime.weightTargetCache = new float[totalInputs];
            runtime.weightVelocityCache = new float[totalInputs];

            for (int i = 0; i < phases.Length; i++)
            {
                int primaryIndex = i * 2;
                int secondaryIndex = i * 2 + 1;

                // 主Clip
                if (phases[i].primaryClip != null)
                {
                    runtime.playables[primaryIndex] = AnimationClipPlayable.Create(graph, phases[i].primaryClip);
                    graph.Connect(runtime.playables[primaryIndex], 0, runtime.mixer, primaryIndex);
                }

                // 次Clip（可选）
                if (phases[i].secondaryClip != null)
                {
                    runtime.playables[secondaryIndex] = AnimationClipPlayable.Create(graph, phases[i].secondaryClip);
                    graph.Connect(runtime.playables[secondaryIndex], 0, runtime.mixer, secondaryIndex);
                }

                // 初始权重全为0
                runtime.mixer.SetInputWeight(primaryIndex, 0f);
                runtime.mixer.SetInputWeight(secondaryIndex, 0f);
            }

            // 初始化顺序状态机状态
            runtime.currentWeights = new float[3];  // [0]=当前阶段索引, [1]=阶段时间, [2]=总时间
            runtime.currentWeights[0] = 0f;  // 从第一个阶段开始
            runtime.currentWeights[1] = 0f;
            runtime.currentWeights[2] = 0f;

            runtime.useSmoothing = blendSmoothTime > 0.001f;

            output = runtime.mixer;
            runtime.IsInitialized = true;
            return true;
        }

        public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
        {
            if (!runtime.mixer.IsValid() || phases.Length == 0)
                return;

            int currentPhase = (int)runtime.currentWeights[0];
            float phaseTime = runtime.currentWeights[1];
            float totalTime = runtime.currentWeights[2];

            // 更新时间
            phaseTime += deltaTime;
            totalTime += deltaTime;
            runtime.currentWeights[1] = phaseTime;
            runtime.currentWeights[2] = totalTime;

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
                else if (phaseTime >= phase.minDuration && 
                         phase.transitionTrigger.EnumValue != StateDefaultFloatParameter.None)
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
                runtime.currentWeights[0] = currentPhase;
                runtime.currentWeights[1] = 0f;  // 重置阶段时间
            }

            // 更新当前阶段的权重
            if (currentPhase < phases.Length)
            {
                var phase = phases[currentPhase];
                int primaryIndex = currentPhase * 2;
                int secondaryIndex = currentPhase * 2 + 1;

                // 获取混合参数
                float blendValue = context.GetFloat(phase.blendParameter, 0f);
                blendValue = Mathf.Clamp01(blendValue);

                // 计算主次Clip权重
                float primaryWeight = 1f - blendValue;
                float secondaryWeight = blendValue;

                // 计算阶段淡入淡出（基于transitionDuration）
                float phaseFade = 1f;
                if (transitionDuration > 0.001f && phaseTime < transitionDuration)
                {
                    phaseFade = phaseTime / transitionDuration;
                }

                // 设置目标权重
                for (int i = 0; i < runtime.weightTargetCache.Length; i++)
                    runtime.weightTargetCache[i] = 0f;

                runtime.weightTargetCache[primaryIndex] = primaryWeight * phaseFade;
                if (phase.secondaryClip != null)
                {
                    runtime.weightTargetCache[secondaryIndex] = secondaryWeight * phaseFade;
                }

                // 平滑过渡
                float smoothSpeed = blendSmoothTime > 0.001f ? blendSmoothTime : 0.001f;
                for (int i = 0; i < runtime.weightTargetCache.Length; i++)
                {
                    if (runtime.useSmoothing)
                    {
                        runtime.weightCache[i] = Mathf.SmoothDamp(
                            runtime.weightCache[i],
                            runtime.weightTargetCache[i],
                            ref runtime.weightVelocityCache[i],
                            smoothSpeed,
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
        }

        public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
        {
            if (phases.Length == 0 || runtime.currentWeights == null)
                return null;

            int currentPhase = (int)runtime.currentWeights[0];
            if (currentPhase >= 0 && currentPhase < phases.Length)
            {
                return phases[currentPhase].primaryClip;
            }

            return null;
        }

        public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
        {
            if (clipIndex < 0 || clipIndex >= runtime.playables.Length)
            {
                Debug.LogError($"[SequentialStates] 索引越界: {clipIndex}");
                return false;
            }

            if (!runtime.playables[clipIndex].IsValid() || newClip == null)
            {
                Debug.LogError($"[SequentialStates] Playable[{clipIndex}]无效或新Clip为null");
                return false;
            }

            // 零GC替换
            var graph = runtime.playables[clipIndex].GetGraph();
            var oldSpeed = runtime.playables[clipIndex].GetSpeed();
            var oldTime = runtime.playables[clipIndex].GetTime();
            var currentWeight = runtime.mixer.GetInputWeight(clipIndex);

            runtime.mixer.DisconnectInput(clipIndex);
            runtime.playables[clipIndex].Destroy();

            runtime.playables[clipIndex] = AnimationClipPlayable.Create(graph, newClip);
            runtime.playables[clipIndex].SetSpeed(oldSpeed);
            runtime.playables[clipIndex].SetTime(oldTime);
            graph.Connect(runtime.playables[clipIndex], 0, runtime.mixer, clipIndex);
            runtime.mixer.SetInputWeight(clipIndex, currentWeight);

            return true;
        }

        /// <summary>
        /// 重置到第一个阶段（外部调用）
        /// </summary>
        public void ResetSequence(AnimationCalculatorRuntime runtime)
        {
            if (runtime.currentWeights != null && runtime.currentWeights.Length >= 3)
            {
                runtime.currentWeights[0] = 0f;  // 阶段索引
                runtime.currentWeights[1] = 0f;  // 阶段时间
                runtime.currentWeights[2] = 0f;  // 总时间
            }
        }

        /// <summary>
        /// 强制跳转到指定阶段（外部调用）
        /// </summary>
        public void JumpToPhase(AnimationCalculatorRuntime runtime, int phaseIndex)
        {
            if (phaseIndex >= 0 && phaseIndex < phases.Length && runtime.currentWeights != null)
            {
                runtime.currentWeights[0] = phaseIndex;
                runtime.currentWeights[1] = 0f;  // 重置阶段时间
            }
        }
    }
}
