using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    // 注意：StateDefaultParameter枚举已移至 0EnumSupport/StateDefaultParameter.cs
    // 注意：StateParameter结构体已移至 1NormalFeatureSupportData/StateParameter.cs
    
    /// <summary>
    /// 动画混合器类型（用于StateBase识别与扩展能力）
    /// </summary>
    public enum StateAnimationMixerKind
    {
        [InspectorName("未知")]
        Unknown = 0,
        [InspectorName("简易·单Clip播放")]
        SimpleClip = 1,
        [InspectorName("简易·1D混合树")]
        BlendTree1D = 2,
        [InspectorName("2D混合树(方向型)")]
        BlendTree2D_Directional = 3,
        [InspectorName("2D混合树(笛卡尔型)")]
        BlendTree2D_Cartesian = 4,
        [InspectorName("直接权重混合")]
        DirectBlend = 5,
        [InspectorName("简易·序列Clip(前-主-后)")]
        SequentialClipMixer = 6,
        [InspectorName("混合器包装器")]
        MixerWrapper = 7,
        [InspectorName("高级·序列状态播放器")]
        SequentialStates = 8,
        [InspectorName("高级·阶段播放器(四阶段)")]
        Phase4 = 9
    }
    
    /// <summary>
    /// 动画Clip计算器基类 - 零GC高性能抽象
    /// 配置数据(享元),运行时数据分离
    /// 设计原则:
    /// 1. Calculator不可变，作为共享配置数据
    /// 2. Runtime可变，每个状态独占一个
    /// 3. Runtime绑定Calculator后不变，但Clip可被override(索引保持)
    /// 4. 支持多层级Mixer嵌套，可接入其他Mixer
    /// 5. 参数获取通过StateContext，支持枚举+字符串方式
    /// </summary>
    [Serializable]
    public abstract class StateAnimationMixCalculator
    {
        /// <summary>
        /// 计算器类型标识（供StateBase/Runtime扩展使用）
        /// </summary>
        [ShowInInspector, LabelText("混合器类型"), ReadOnly]
        public virtual StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.Unknown;

        /// <summary>
        /// 初始化Calculator - 预计算享元数据（仅执行一次）
        /// 在状态注册时自动调用，用于排序、三角化等一次性计算
        /// 子类可重写以实现自定义初始化逻辑
        /// </summary>
        public virtual void InitializeCalculator()
        {
            // 默认实现：无操作
            // 子类重写以实现具体初始化逻辑（如排序、三角化等）
        }
        
        /// <summary>
        /// 创建运行时数据
        /// Runtime与Calculator绑定后不变，保证索引稳定性
        /// ★ 优化：使用对象池回收，避免GC
        /// </summary>
        public virtual AnimationCalculatorRuntime CreateRuntimeData()
        {
            var runtime = AnimationCalculatorRuntime.Pool.GetInPool();
            runtime.BoundCalculatorKind = CalculatorKind;
            return runtime;
        }

        /// <summary>
        /// 初始化运行时Playable - 连接到PlayableGraph或父Mixer（带统一防重复保护）
        /// 支持多层级：可以连接到主Graph或父级Mixer
        /// ★ 优化：设置BoundCalculatorKind和outputPlayable跟踪
        /// </summary>
        /// <param name="runtime">运行时数据(绑定后不变)</param>
        /// <param name="graph">PlayableGraph引用</param>
        /// <param name="output">输出Playable(可被父Mixer连接)</param>
        /// <returns>是否初始化成功</returns>
        public bool InitializeRuntime(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
        {
            // 统一防重复初始化检查（所有计算器统一处理）
            if (runtime.IsInitialized)
            {
                if (StateMachineDebugSettings.Instance.logRuntimeInit)
                    StateMachineDebugSettings.Instance.LogWarning($"[{GetType().Name}] Runtime已初始化，跳过重复初始化");
                return true; // 已初始化视为成功
            }
            
            // ★ 绑定计算器类型标识
            runtime.BoundCalculatorKind = CalculatorKind;
            
            // 调用子类具体实现
            bool success = InitializeRuntimeInternal(runtime, graph, ref output);
            
            // 统一标记初始化完成
            if (success)
            {
                runtime.IsInitialized = true;
                // ★ 记录输出Playable引用（用于外部查询和IK连接）
                if (output.IsValid())
                    runtime.outputPlayable = output;
                    
                if (StateMachineDebugSettings.Instance.logRuntimeInit)
                    StateMachineDebugSettings.Instance.LogRuntimeInit($"[{GetType().Name}] Runtime初始化完成 | Kind={CalculatorKind}");
            }
            else
            {
                if (StateMachineDebugSettings.Instance.alwaysLogErrors)
                    StateMachineDebugSettings.Instance.LogError($"[{GetType().Name}] Runtime初始化失败");
            }
            
            return success;
        }
        
        /// <summary>
        /// 子类实现具体的运行时初始化逻辑
        /// 无需检查IsInitialized或设置标记，由基类统一处理
        /// 注意：IK绑定需要在此方法中创建对应的IK Playable节点
        /// </summary>
        protected abstract bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output);

        /// <summary>
        /// 零GC更新权重 - 每帧调用
        /// 通过StateContext获取参数，支持枚举+字符串方式
        /// 建议：常用参数使用枚举，动态参数使用字符串
        /// </summary>
        /// <param name="runtime">运行时数据</param>
        /// <param name="context">状态上下文(用于读取参数，in修饰避免复制)</param>
        /// <param name="deltaTime">帧时间</param>
        public abstract void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime);

        /// <summary>
        /// 状态首帧立即更新 — 内部混合权重直接到位（无平滑过渡）。
        /// 外部管线权重(FadeIn/FadeOut)由状态机独立管理，不受此影响。
        /// 
        /// 原理：临时关闭 useSmoothing 并传入极大 deltaTime，
        /// 使所有 SmoothDamp 在一次调用内完全收敛到目标值，
        /// 随后恢复平滑设置供后续帧正常过渡。
        /// </summary>
        public virtual void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runtime == null) throw new ArgumentNullException(nameof(runtime), $"[{GetType().Name}] ImmediateUpdate: runtime 不能为空");
#endif

            // 保存 & 关闭内部权重平滑
            bool prevSmoothing = runtime.useSmoothing;
            runtime.useSmoothing = false;

            // 极大 deltaTime → 所有 SmoothDamp（输入平滑 / Direct权重平滑）一帧收敛
            UpdateWeights(runtime, context, 99f);

            // 恢复平滑设置
            runtime.useSmoothing = prevSmoothing;
        }

        /// <summary>
        /// 获取当前播放的主Clip(用于调试)
        /// </summary>
        public abstract AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime);
        
        /// <summary>
        /// 运行时覆盖Clip - 索引不变，仅替换内容
        /// 用于动态替换动画(如换装/武器切换)
        /// </summary>
        /// <param name="runtime">运行时数据</param>
        /// <param name="clipIndex">Clip索引(必须在有效范围内)</param>
        /// <param name="newClip">新的AnimationClip</param>
        /// <returns>是否替换成功</returns>
        public virtual bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
        {
            // 默认实现：不支持覆盖
            StateMachineDebugSettings.Instance.LogWarning($"[{GetType().Name}] 不支持Clip覆盖");
            return false;
        }

        /// <summary>
        /// 获取标准动画时长（不经历外部缩放速度）
        /// 用于计算归一化进度和循环次数
        /// </summary>
        /// <param name="runtime">运行时数据</param>
        /// <returns>标准动画时长（秒），0表示无法获取</returns>
        public virtual float GetStandardDuration(AnimationCalculatorRuntime runtime)
        {
            // 默认实现：返回0（子类需要override）
            return 0f;
        }

#if UNITY_EDITOR
        [OnInspectorGUI]
        private void DrawUsageHelpButton()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("?", GUILayout.Width(22f), GUILayout.Height(18f)))
            {
                string title = $"{GetCalculatorDisplayName()} 使用说明";
                EditorUtility.DisplayDialog(title, GetUsageHelp(), "确定");
            }
            GUILayout.EndHorizontal();
        }
#endif

        /// <summary>
        /// 子类可覆盖：提供简短中文使用说明
        /// </summary>
        protected virtual string GetUsageHelp()
        {
            return "暂无说明。";
        }

        /// <summary>
        /// 弹窗标题显示名
        /// </summary>
        protected virtual string GetCalculatorDisplayName()
        {
            return CalculatorKind.ToString();
        }
    }

    // ==================== 单Clip计算器 ====================

    /// <summary>
    /// 简单Clip - 直接播放单个AnimationClip
    /// 支持运行时Clip覆盖，可接入任意Mixer
    /// </summary>
    [Serializable, TypeRegistryItem("简易·单Clip播放")]
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

        // ==================== 1D混合树 ====================

        /// <summary>
        /// 1D混合树 - 单参数线性混合
        /// 典型应用: Idle → Walk → Run → Sprint
        /// 性能: O(log n)查找 + O(1)插值
        /// 支持多层级嵌套，Mixer可连接到父Mixer
        /// </summary>
        [Serializable, TypeRegistryItem("简易·1D混合树")]
        public class StateAnimationMixCalculatorForBlendTree1D : StateAnimationMixCalculator
        {
            [Serializable]
            public struct ClipSampleForBlend1D
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

        // ==================== 2D混合树基类 ====================

        /// <summary>
        /// 2D混合树基类
        /// 支持多层级嵌套，Mixer可连接到父Mixer
        /// </summary>
        [Serializable]
        public abstract class StateAnimationMixCalculatorForBlendTree2D : StateAnimationMixCalculator
        {
            [Serializable]
            public struct ClipSample2D
            {
                public AnimationClip clip;
                public Vector2 position;  // 2D空间位置
            }

            public StateParameter parameterX = "DirectionX";
            public StateParameter parameterY = "DirectionY";
            public ClipSample2D[] samples = new ClipSample2D[0];

            [Range(0f, 1f)]
            public float smoothTime = 0.1f;

            protected AnimationCalculatorRuntime.Triangle[] _sharedTriangles;  // 享元三角形数据
            
            /// <summary>
            /// Debug设置（可独立配置，默认使用全局设置）
            /// </summary>
            protected StateMachineDebugSettings debugSettings => StateMachineDebugSettings.Instance;

#if UNITY_EDITOR
            /// <summary>
            /// 编辑器初始化标准采样组 - 为2D混合树提供标准8方向+中心采样点
            /// </summary>
            [Button("初始化标准采样(8方向+中心)"), PropertyOrder(-1)]
            private void InitializeStandardSamples()
            {
                if (samples != null && samples.Length > 0)
                {
                    if (!UnityEditor.EditorUtility.DisplayDialog(
                        "确认初始化",
                        $"当前已有{samples.Length}个采样点，是否覆盖为标准配置？\n标准配置：8方向+中心共17个采样点（内外两圈+中心）",
                        "确认覆盖", "取消"))
                    {
                        return;
                    }
                }

                var sampleList = new System.Collections.Generic.List<ClipSample2D>();
                
                // 中心点
                sampleList.Add(new ClipSample2D { position = Vector2.zero, clip = null });
                
                // 外圈8个方向（半径1）
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f * Mathf.Deg2Rad;
                    sampleList.Add(new ClipSample2D 
                    { 
                        position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 
                        clip = null 
                    });
                }
                
                // 内圈8个方向（半径0.5）
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f * Mathf.Deg2Rad;
                    sampleList.Add(new ClipSample2D 
                    { 
                        position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f, 
                        clip = null 
                    });
                }

                samples = sampleList.ToArray();

                debugSettings.LogRuntimeInit($"[BlendTree2D] 已初始化{samples.Length}个标准采样点（8方向外圈+8方向内圈+中心）");
            }
#endif

            /// <summary>
            /// 计算器初始化 - 仅执行一次，所有Runtime共享（预计算三角化）
            /// </summary>
            public override void InitializeCalculator()
            {
                // 预计算三角化(一次性计算，享元数据)
                ComputeDelaunayTriangulation();
            }

            protected override bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
            {
                if (samples == null || samples.Length == 0)
                {
                    debugSettings.LogError("[BlendTree2D] 采样点为空");
                    return false;
                }

                if (samples.Length == 1 && samples[0].clip != null)
                {
                    runtime.singlePlayable = AnimationClipPlayable.Create(graph, samples[0].clip);
                    output = runtime.singlePlayable;
                    return true;
                }

                if (samples.Length < 3)
                {
                    debugSettings.LogError("[BlendTree2D] 至少需要3个采样点");
                    return false;
                }

                // 创建Mixer(可被父Mixer连接)
                runtime.mixer = AnimationMixerPlayable.Create(graph, samples.Length);

                // 创建所有Clip的Playable(索引固定)
                runtime.playables = new AnimationClipPlayable[samples.Length];
                
                // 初始化权重缓存系统（与1D一致）
                runtime.weightCache = new float[samples.Length];
                runtime.weightTargetCache = new float[samples.Length];
                runtime.weightVelocityCache = new float[samples.Length];
                runtime.useSmoothing = smoothTime > 0.001f;
                
                // 找到中心点（最接近原点的点）
                int centerIndex = 0;
                float minDist = float.MaxValue;
                for (int i = 0; i < samples.Length; i++)
                {
                    float dist = samples[i].position.sqrMagnitude;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        centerIndex = i;
                    }
                }
                
                for (int i = 0; i < samples.Length; i++)
                {
                    if (samples[i].clip != null)
                    {
                        runtime.playables[i] = AnimationClipPlayable.Create(graph, samples[i].clip);
                        graph.Connect(runtime.playables[i], 0, runtime.mixer, i);
                        
                        // 初始化时给中心点100%权重（默认播放Idle）
                        float initialWeight = (i == centerIndex) ? 1f : 0f;
                        runtime.mixer.SetInputWeight(i, initialWeight);
                        runtime.weightCache[i] = initialWeight;
                        runtime.weightTargetCache[i] = initialWeight;
                    }
                }

                // 三角形数据从享元复制到Runtime(共享引用，不复制数组)
                runtime.triangles = _sharedTriangles;

                // 输出Mixer供父级连接(支持多层级)
                output = runtime.mixer;
                
                debugSettings.LogRuntimeInit($"[BlendTree2D] Runtime初始化完成: {samples.Length}个采样点, 中心点索引={centerIndex}, 三角形数量={_sharedTriangles?.Length ?? 0}");
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
            {
               // Debug.Log($"[BlendTree2D] 更新动画权重: Runtime={runtime}, DeltaTime={deltaTime}");
                if (!runtime.mixer.IsValid() || samples.Length == 0){
                    debugSettings.LogWarning("[BlendTree2D] Mixer无效或采样点为空，跳过权重更新");
                    return;
                }
                   

                // 通过context获取参数（直接传StateParameter，零GC）
                float paramX = context.GetFloat(parameterX, 0f);
                float paramY = context.GetFloat(parameterY, 0f);
                    
                Vector2 rawInput = new Vector2(paramX, paramY);

                // 平滑输入(零GC)
                Vector2 input = smoothTime > 0.001f
                    ? Vector2.SmoothDamp(runtime.lastInput2D, rawInput, ref runtime.inputVelocity2D, smoothTime, float.MaxValue, deltaTime)
                    : rawInput;
                runtime.lastInput2D = input;
                // 计算权重（带平滑过渡）
                CalculateWeights2D(runtime, input, deltaTime);
            }

            protected abstract void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input, float deltaTime);

            /// <summary>
            /// 预计算Delaunay三角化 - 一次性计算，享元数据
            /// </summary>
            protected abstract void ComputeDelaunayTriangulation();

            /// <summary>
            /// BlendTree2D 首帧立即更新 — 跳过输入SmoothDamp，
            /// 关闭 useSmoothing 后调用 CalculateWeights2D 使内部权重直接到位，
            /// 同时重置 inputVelocity2D 避免残留速度影响后续帧。
            /// </summary>
            public override void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
            {
                if (runtime == null || !runtime.mixer.IsValid() || samples.Length == 0) return;

                // 直接读取参数，跳过输入平滑
                float paramX = context.GetFloat(parameterX, 0f);
                float paramY = context.GetFloat(parameterY, 0f);
                Vector2 rawInput = new Vector2(paramX, paramY);

                // 立即设置 lastInput2D（后续帧从此值开始平滑）
                runtime.lastInput2D = rawInput;
                runtime.inputVelocity2D = Vector2.zero;

                // 关闭权重平滑，直接计算到位
                bool prevSmoothing = runtime.useSmoothing;
                runtime.useSmoothing = false;
                CalculateWeights2D(runtime, rawInput, 0f);
                runtime.useSmoothing = prevSmoothing;

                // 重置权重速度缓存
                if (runtime.weightVelocityCache != null)
                {
                    for (int i = 0; i < runtime.weightVelocityCache.Length; i++)
                        runtime.weightVelocityCache[i] = 0f;
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
                    debugSettings.LogError($"[BlendTree2D] 索引越界: {clipIndex} (有效范围: 0-{samples.Length - 1})");
                    return false;
                }
                
                if (runtime.playables == null || !runtime.playables[clipIndex].IsValid())
                {
                    debugSettings.LogError($"[BlendTree2D] Playable[{clipIndex}]无效");
                    return false;
                }
                
                if (newClip == null)
                {
                    debugSettings.LogError("[BlendTree2D] 新Clip为null");
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
                
                return true;
            }

        public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
        {
            if (samples == null || samples.Length == 0)
                return 0f;

            int maxWeightIndex = 0;
            float maxWeight = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float weight = runtime.weightCache != null && i < runtime.weightCache.Length
                    ? runtime.weightCache[i]
                    : runtime.mixer.GetInputWeight(i);

                if (weight > maxWeight)
                {
                    maxWeight = weight;
                    maxWeightIndex = i;
                }
            }

            var clip = samples[maxWeightIndex].clip;
            return clip != null ? clip.length : 0f;
        }
        }

        // ==================== 2D自由方向混合 ====================

        /// <summary>
        /// 2D自由方向混合 - 适用于移动动画
        /// 典型应用: 8方向移动(前后左右+4个斜向)
        /// 性能: O(n)三角形查找 + O(1)重心坐标
        /// </summary>
        [Serializable, TypeRegistryItem("2D混合树(方向型)")]
        public class StateAnimationMixCalculatorForBlendTree2DFreeformDirectional : StateAnimationMixCalculatorForBlendTree2D
        {
            public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.BlendTree2D_Directional;

            protected override string GetUsageHelp()
            {
                 return "适用：方向移动（8向/多向）。\n" +
                        "必填：采样点（二维位置 + 动画片段），以及 2 个用于驱动的 float 参数（横向/纵向）。\n" +
                        "建议：中心 Idle 在 (0,0)，外圈为跑，内圈为走。";
            }

            protected override string GetCalculatorDisplayName()
            {
                return "2D混合树(方向型)";
            }
            // 8方向 Walk + 8方向 Run + 中心Idle 的环形缓存
            [NonSerialized] private bool _ringsPrepared;
            [NonSerialized] private int _centerIndex = -1;
            [NonSerialized] private int[] _innerRing = Array.Empty<int>();
            [NonSerialized] private int[] _outerRing = Array.Empty<int>();
            [NonSerialized] private float[] _innerAngles = Array.Empty<float>();
            [NonSerialized] private float[] _outerAngles = Array.Empty<float>();
            [NonSerialized] private float _innerRadius;
            [NonSerialized] private float _outerRadius;

            /// <summary>
            /// 重写初始化：预计算中心点、内外环、角度缓存
            /// </summary>
            public override void InitializeCalculator()
            {
                PrepareRings();
            }

            protected override void ComputeDelaunayTriangulation()
            {
                // 本算法不使用三角化，保留空实现，避免上层逻辑依赖
                _sharedTriangles = null;
            }

            protected override void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input, float deltaTime)
            {
                if (samples == null || samples.Length == 0)
                {
                    debugSettings.LogWarning("【混合树】samples为空，无法计算权重");
                    return;
                }

                if (!_ringsPrepared)
                    PrepareRings();

                if (_centerIndex < 0 || _innerRing.Length < 2)
                {
                    int fallback = FindNearestSample(input);
                    for (int i = 0; i < samples.Length; i++)
                        runtime.weightTargetCache[i] = 0f;
                    runtime.weightTargetCache[fallback] = 1f;
                    ApplyWeights(runtime, deltaTime);
                    return;
                }

                // 计算输入角度与半径
                float inputMagnitude = input.magnitude;
                float angle = Mathf.Atan2(input.y, input.x);
                if (angle < 0f) angle += Mathf.PI * 2f;

                // 限制输入到外圈半径范围内（保证稳定）
                if (_outerRadius > 0.001f && inputMagnitude > _outerRadius)
                {
                    inputMagnitude = _outerRadius;
                }

                // 清空目标权重
                for (int i = 0; i < samples.Length; i++)
                    runtime.weightTargetCache[i] = 0f;

                // 中心权重（Idle）
                float centerRadius = _innerRing.Length >= 2 ? _innerRadius : _outerRadius;
                float centerWeight = 0f;
                if (centerRadius > 0.001f)
                {
                    centerWeight = Mathf.Clamp01(1f - (inputMagnitude / centerRadius));
                }
                runtime.weightTargetCache[_centerIndex] = centerWeight;

                // 方向权重（内环/外环）
                float radialT = 1f;
                if (_innerRing.Length >= 2 && _outerRadius > _innerRadius + 0.0001f)
                {
                    radialT = Mathf.Clamp01((inputMagnitude - _innerRadius) / (_outerRadius - _innerRadius));
                }

                // 内环权重（存在时参与混合）
                if (_innerRing.Length >= 2)
                {
                    GetDirectionalPair(_innerRing, _innerAngles, angle, out int iA, out int iB, out float t);
                    float innerScale = (1f - centerWeight) * (1f - radialT);
                    runtime.weightTargetCache[iA] += (1f - t) * innerScale;
                    runtime.weightTargetCache[iB] += t * innerScale;
                }

                // 外环权重
                if (_outerRing.Length >= 2)
                {
                    GetDirectionalPair(_outerRing, _outerAngles, angle, out int oA, out int oB, out float t);
                    float outerScale = (1f - centerWeight) * radialT;
                    runtime.weightTargetCache[oA] += (1f - t) * outerScale;
                    runtime.weightTargetCache[oB] += t * outerScale;
                }

                NormalizeWeights(runtime);
                ApplyWeights(runtime, deltaTime);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsPointInTriangle(Vector2 p, AnimationCalculatorRuntime.Triangle triangle)
            {
                // 使用重心坐标法判断点是否在三角形内
                Vector3 bary = CalculateBarycentricCoordinates(p, triangle.v0, triangle.v1, triangle.v2);
                
                // 如果三个重心坐标都非负（带容差），则点在三角形内
                return bary.x >= -0.001f && bary.y >= -0.001f && bary.z >= -0.001f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Vector3 CalculateBarycentricCoordinates(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                // 使用叉积法计算重心坐标（更稳定）
                Vector2 v0 = c - a;
                Vector2 v1 = b - a;
                Vector2 v2 = p - a;

                float d00 = Vector2.Dot(v0, v0);
                float d01 = Vector2.Dot(v0, v1);
                float d11 = Vector2.Dot(v1, v1);
                float d20 = Vector2.Dot(v2, v0);
                float d21 = Vector2.Dot(v2, v1);

                float denom = d00 * d11 - d01 * d01;
                
                // 防止除零
                if (Mathf.Abs(denom) < 0.0001f)
                {
                    // 三角形退化，返回最近顶点
                    float distA = Vector2.Distance(p, a);
                    float distB = Vector2.Distance(p, b);
                    float distC = Vector2.Distance(p, c);
                    
                    if (distA <= distB && distA <= distC)
                        return new Vector3(1, 0, 0);
                    else if (distB <= distC)
                        return new Vector3(0, 1, 0);
                    else
                        return new Vector3(0, 0, 1);
                }

                float invDenom = 1f / denom;
                float w = (d11 * d20 - d01 * d21) * invDenom;
                float v = (d00 * d21 - d01 * d20) * invDenom;
                float u = 1f - v - w;

                return new Vector3(u, v, w);
            }

            private int FindNearestSample(Vector2 input)
            {
                float minDist = float.MaxValue;
                int nearest = 0;

                for (int i = 0; i < samples.Length; i++)
                {
                    float dist = Vector2.SqrMagnitude(input - samples[i].position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = i;
                    }
                }

                return nearest;
            }

            private void PrepareRings()
            {
                _ringsPrepared = true;
                _centerIndex = -1;
                _innerRing = Array.Empty<int>();
                _outerRing = Array.Empty<int>();
                _innerAngles = Array.Empty<float>();
                _outerAngles = Array.Empty<float>();
                _innerRadius = 0f;
                _outerRadius = 0f;

                if (samples == null || samples.Length < 3)
                {
                    debugSettings.LogWarning("【混合树】samples不足，无法初始化环形混合");
                    return;
                }

                // 找中心点（最近原点）
                float minDist = float.MaxValue;
                for (int i = 0; i < samples.Length; i++)
                {
                    float d = samples[i].position.sqrMagnitude;
                    if (d < minDist)
                    {
                        minDist = d;
                        _centerIndex = i;
                    }
                }

                if (_centerIndex < 0)
                    return;

                // 采集非中心点并按半径排序
                var others = new List<int>(samples.Length - 1);
                for (int i = 0; i < samples.Length; i++)
                {
                    if (i != _centerIndex)
                        others.Add(i);
                }

                others.Sort((a, b) =>
                {
                    float da = samples[a].position.sqrMagnitude;
                    float db = samples[b].position.sqrMagnitude;
                    return da.CompareTo(db);
                });

                if (others.Count < 2)
                    return;

                // 动态分割内/外环：寻找半径最大间隙
                float maxGap = 0f;
                int splitIndex = -1;
                float maxRadius = Mathf.Sqrt(samples[others[others.Count - 1]].position.sqrMagnitude);
                for (int i = 1; i < others.Count; i++)
                {
                    float r0 = samples[others[i - 1]].position.magnitude;
                    float r1 = samples[others[i]].position.magnitude;
                    float gap = r1 - r0;
                    if (gap > maxGap)
                    {
                        maxGap = gap;
                        splitIndex = i;
                    }
                }

                // 若间隙不足，则视为单环（仅外环）
                float gapThreshold = Mathf.Max(0.05f, maxRadius * 0.15f);
                if (splitIndex <= 0 || splitIndex >= others.Count || maxGap < gapThreshold)
                {
                    _outerRing = others.ToArray();
                    SortByAngle(_outerRing);
                    _outerAngles = BuildAngles(_outerRing);
                    _outerRadius = AverageRadius(_outerRing);
                    _innerRing = Array.Empty<int>();
                    _innerAngles = Array.Empty<float>();
                    _innerRadius = 0f;
                }
                else
                {
                    _innerRing = others.GetRange(0, splitIndex).ToArray();
                    _outerRing = others.GetRange(splitIndex, others.Count - splitIndex).ToArray();

                    // 角度排序
                    SortByAngle(_innerRing);
                    SortByAngle(_outerRing);

                    _innerAngles = BuildAngles(_innerRing);
                    _outerAngles = BuildAngles(_outerRing);
                    _innerRadius = AverageRadius(_innerRing);
                    _outerRadius = AverageRadius(_outerRing);

                    // 若内环不足2点，降级为单外环
                    if (_innerRing.Length < 2)
                    {
                        _outerRing = others.ToArray();
                        SortByAngle(_outerRing);
                        _outerAngles = BuildAngles(_outerRing);
                        _outerRadius = AverageRadius(_outerRing);
                        _innerRing = Array.Empty<int>();
                        _innerAngles = Array.Empty<float>();
                        _innerRadius = 0f;
                    }
                }

                debugSettings.LogRuntimeInit($"[BlendTree2D-Directional] 环形初始化完成: center={_centerIndex}, inner={_innerRing.Length}, outer={_outerRing.Length}, innerR={_innerRadius:F2}, outerR={_outerRadius:F2}, gap={maxGap:F2}");
            }

            private void SortByAngle(int[] indices)
            {
                Array.Sort(indices, (a, b) =>
                {
                    float angleA = Mathf.Atan2(samples[a].position.y, samples[a].position.x);
                    float angleB = Mathf.Atan2(samples[b].position.y, samples[b].position.x);
                    if (angleA < 0f) angleA += Mathf.PI * 2f;
                    if (angleB < 0f) angleB += Mathf.PI * 2f;
                    return angleA.CompareTo(angleB);
                });
            }

            private float[] BuildAngles(int[] indices)
            {
                var angles = new float[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                {
                    float a = Mathf.Atan2(samples[indices[i]].position.y, samples[indices[i]].position.x);
                    if (a < 0f) a += Mathf.PI * 2f;
                    angles[i] = a;
                }
                return angles;
            }

            private float AverageRadius(int[] indices)
            {
                if (indices.Length == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < indices.Length; i++)
                    sum += samples[indices[i]].position.magnitude;
                return sum / indices.Length;
            }

            private void GetDirectionalPair(int[] ring, float[] angles, float angle, out int iA, out int iB, out float t)
            {
                iA = ring[0];
                iB = ring[0];
                t = 0f;

                if (ring.Length == 1)
                {
                    iA = ring[0];
                    iB = ring[0];
                    t = 0f;
                    return;
                }

                // 找到角度区间
                int idx = 0;
                for (int i = 0; i < angles.Length; i++)
                {
                    if (angles[i] >= angle)
                    {
                        idx = i;
                        break;
                    }
                    if (i == angles.Length - 1)
                        idx = 0;
                }

                int prev = (idx - 1 + ring.Length) % ring.Length;
                int next = idx;

                float a0 = angles[prev];
                float a1 = angles[next];
                if (a1 < a0) a1 += Mathf.PI * 2f;
                float a = angle;
                if (a < a0) a += Mathf.PI * 2f;

                float span = a1 - a0;
                t = span > 0.0001f ? Mathf.Clamp01((a - a0) / span) : 0f;

                iA = ring[prev];
                iB = ring[next];
            }

            private void NormalizeWeights(AnimationCalculatorRuntime runtime)
            {
                float sum = 0f;
                for (int i = 0; i < samples.Length; i++)
                    sum += runtime.weightTargetCache[i];

                if (sum > 0.0001f)
                {
                    float inv = 1f / sum;
                    for (int i = 0; i < samples.Length; i++)
                        runtime.weightTargetCache[i] *= inv;
                }
            }

            private void ApplyWeights(AnimationCalculatorRuntime runtime, float deltaTime)
            {
                float smoothingSpeed = smoothTime > 0.001f ? smoothTime * 0.5f : 0.001f;
                for (int i = 0; i < samples.Length; i++)
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
        }

        // ==================== 2D笛卡尔混合 ====================

        /// <summary>
        /// 2D笛卡尔混合 - 适用于瞄准偏移
        /// 典型应用: Aim Offset (Yaw/Pitch独立混合)
        /// 性能: O(1)查找最近4个点 + O(1)双线性插值
        /// </summary>
        [Serializable, TypeRegistryItem("2D混合树(笛卡尔型)")]
        public class StateAnimationMixCalculatorForBlendTree2DFreeformCartesian : StateAnimationMixCalculatorForBlendTree2D
        {
            public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.BlendTree2D_Cartesian;

            [NonSerialized] private bool _gridPrepared;
            [NonSerialized] private float[] _gridX;
            [NonSerialized] private float[] _gridY;
            [NonSerialized] private int[,] _gridIndex;

            protected override string GetUsageHelp()
            {
                 return "适用：瞄准偏移（Yaw/Pitch）或视角偏移。\n" +
                        "必填：采样点（二维位置 + 动画片段），以及 2 个用于驱动的 float 参数（横向/纵向）。\n" +
                        "建议：采样点构成规则网格，便于插值。";
            }

            protected override string GetCalculatorDisplayName()
            {
                return "2D混合树(笛卡尔型)";
            }

            public override void InitializeCalculator()
            {
                base.InitializeCalculator();
                PrepareGridCache();
            }

            protected override void ComputeDelaunayTriangulation()
            {
                // 笛卡尔模式使用网格结构,不需要三角化
                _sharedTriangles = null;
            }

            protected override void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input, float deltaTime)
            {
                // 先计算目标权重，全部置零
                for (int i = 0; i < samples.Length; i++)
                    runtime.weightTargetCache[i] = 0f;

                // 找到最近的4个点(形成矩形)
                if (!_gridPrepared)
                    PrepareGridCache();

                bool found = TryGetCellIndices(input, out int i0, out int i1, out int i2, out int i3);
                if (!found)
                {
                    FindClosestGridPoints(input, out i0, out i1, out i2, out i3);
                }

                if (i0 < 0)
                {
                    // 找最近的单点
                    int nearest = FindNearestSample(input);
                    runtime.weightTargetCache[nearest] = 1f;
                }
                else
                {
                    // 双线性插值
                    Vector2 p0 = samples[i0].position;
                    Vector2 p1 = samples[i1].position;
                    Vector2 p2 = samples[i2].position;
                    Vector2 p3 = samples[i3].position;

                    // 计算插值权重
                    float tx = (input.x - p0.x) / (p1.x - p0.x);
                    float ty = (input.y - p0.y) / (p2.y - p0.y);
                    tx = Mathf.Clamp01(tx);
                    ty = Mathf.Clamp01(ty);

                    runtime.weightTargetCache[i0] = (1 - tx) * (1 - ty);
                    runtime.weightTargetCache[i1] = tx * (1 - ty);
                    runtime.weightTargetCache[i2] = (1 - tx) * ty;
                    runtime.weightTargetCache[i3] = tx * ty;
                }

                // 平滑过渡到目标权重
                float smoothingSpeed = smoothTime > 0.001f ? smoothTime * 0.5f : 0.001f;
                for (int i = 0; i < samples.Length; i++)
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

            private void PrepareGridCache()
            {
                _gridPrepared = true;
                _gridX = null;
                _gridY = null;
                _gridIndex = null;

                if (samples == null || samples.Length < 4)
                    return;

                var xList = new List<float>();
                var yList = new List<float>();
                for (int i = 0; i < samples.Length; i++)
                {
                    float x = samples[i].position.x;
                    float y = samples[i].position.y;
                    if (!xList.Contains(x)) xList.Add(x);
                    if (!yList.Contains(y)) yList.Add(y);
                }

                xList.Sort();
                yList.Sort();

                _gridX = xList.ToArray();
                _gridY = yList.ToArray();
                _gridIndex = new int[_gridX.Length, _gridY.Length];

                for (int xi = 0; xi < _gridX.Length; xi++)
                    for (int yi = 0; yi < _gridY.Length; yi++)
                        _gridIndex[xi, yi] = -1;

                for (int i = 0; i < samples.Length; i++)
                {
                    int xi = Array.IndexOf(_gridX, samples[i].position.x);
                    int yi = Array.IndexOf(_gridY, samples[i].position.y);
                    if (xi >= 0 && yi >= 0)
                        _gridIndex[xi, yi] = i;
                }
            }

            private bool TryGetCellIndices(Vector2 input, out int i0, out int i1, out int i2, out int i3)
            {
                i0 = i1 = i2 = i3 = -1;
                if (_gridX == null || _gridY == null || _gridX.Length < 2 || _gridY.Length < 2)
                    return false;

                int x0 = FindGridIndex(_gridX, input.x);
                int y0 = FindGridIndex(_gridY, input.y);

                if (x0 < 0 || y0 < 0 || x0 >= _gridX.Length - 1 || y0 >= _gridY.Length - 1)
                    return false;

                int x1 = x0 + 1;
                int y1 = y0 + 1;

                i0 = _gridIndex[x0, y0];
                i1 = _gridIndex[x1, y0];
                i2 = _gridIndex[x0, y1];
                i3 = _gridIndex[x1, y1];

                return i0 >= 0 && i1 >= 0 && i2 >= 0 && i3 >= 0;
            }

            private int FindGridIndex(float[] arr, float value)
            {
                for (int i = 0; i < arr.Length - 1; i++)
                {
                    if (value >= arr[i] && value <= arr[i + 1])
                        return i;
                }
                return -1;
            }

            private void FindClosestGridPoints(Vector2 input, out int i0, out int i1, out int i2, out int i3)
            {
                // 简化实现:找最近的4个点
                i0 = i1 = i2 = i3 = -1;

                float minDist0 = float.MaxValue;
                float minDist1 = float.MaxValue;
                float minDist2 = float.MaxValue;
                float minDist3 = float.MaxValue;

                for (int i = 0; i < samples.Length; i++)
                {
                    float dist = Vector2.SqrMagnitude(input - samples[i].position);

                    if (dist < minDist0)
                    {
                        minDist3 = minDist2;
                        i3 = i2;
                        minDist2 = minDist1;
                        i2 = i1;
                        minDist1 = minDist0;
                        i1 = i0;
                        minDist0 = dist;
                        i0 = i;
                    }
                    else if (dist < minDist1)
                    {
                        minDist3 = minDist2;
                        i3 = i2;
                        minDist2 = minDist1;
                        i2 = i1;
                        minDist1 = dist;
                        i1 = i;
                    }
                    else if (dist < minDist2)
                    {
                        minDist3 = minDist2;
                        i3 = i2;
                        minDist2 = dist;
                        i2 = i;
                    }
                    else if (dist < minDist3)
                    {
                        minDist3 = dist;
                        i3 = i;
                    }
                }
            }

            private int FindNearestSample(Vector2 input)
            {
                float minDist = float.MaxValue;
                int nearest = 0;

                for (int i = 0; i < samples.Length; i++)
                {
                    float dist = Vector2.SqrMagnitude(input - samples[i].position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = i;
                    }
                }

                return nearest;
            }
        }

        // ==================== Direct混合 ====================

        /// <summary>
        /// Direct混合 - 每个Clip独立控制权重
        /// 典型应用: 面部表情、手指动画、多层BlendShape
        /// 性能: O(n)权重更新
        /// 支持多层级嵌套，Mixer可连接到父Mixer
        /// </summary>
        [Serializable, TypeRegistryItem("直接权重混合")]
        public class StateAnimationMixCalculatorForDirectBlend : StateAnimationMixCalculator
        {
            [Serializable]
            public struct DirectClip
            {
                public AnimationClip clip;
                public string weightParameter;  // 权重参数名
                public float defaultWeight;     // 默认权重
            }

            [Serializable]
            public struct WeightEvent
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
        public class SequentialClipMixer : StateAnimationMixCalculator
        {
            [BoxGroup("前置动画(Entry)")]
            [LabelText("前置Clip"), Tooltip("可选，在主动画前播放（如攻击预备、技能前摇）")]
            public AnimationClip entryClip;

            [BoxGroup("前置动画(Entry)")]
            [LabelText("前置速度"), Range(0.1f, 5f), ShowIf("@entryClip != null")]
            public float entrySpeed = 1f;

            [BoxGroup("主动画(Main)"), HideLabel]
            [InlineProperty]
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
            public struct MainClipConfig
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

        // ==================== 混合器包装器（支持嵌套） ====================

        /// <summary>
        /// 混合器包装器 - 支持Calculator嵌套
        /// 用于实现上下半身分离、多层混合等高级功能
        /// 性能: 一层嵌套几乎无影响，推荐深度≤2层
        /// </summary>
        [Serializable, TypeRegistryItem("混合器包装器")]
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