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
        /// 是否需要在“淡出期间（状态已退出逻辑，但Playable仍连接在图中以完成淡出）”继续调用 UpdateWeights。
        /// 默认 false：淡出阶段冻结内部混合比例，只做外部权重衰减。
        /// 典型需要开启的：1D/2D混合树、DirectBlend、Phase4（需要持续跟随参数）。
        /// </summary>
        public virtual bool NeedUpdateWhenFadingOut => false;

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
            // 默认兜底：尽量给一个“合理时长”。
            // 约定：若能拿到当前主导Clip，则返回 clip.length（不依赖 isLooping）。
            // 复杂多阶段/可变结构（Sequential/Phase4等）应 override 给出更准确的总时长/完成信号。
            var clip = GetCurrentClip(runtime);
            if (clip == null) return 0f;
            return clip.length;
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







}