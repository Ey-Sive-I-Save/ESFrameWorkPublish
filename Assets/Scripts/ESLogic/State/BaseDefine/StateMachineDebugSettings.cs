using System;
using Sirenix.OdinInspector;
using UnityEngine;

// ============================================================================
// 文件：StateMachineDebugSettings.cs
// 作用：状态机全局调试设置（ScriptableObject），用于集中控制各模块调试日志输出。
//
// Public（本文件定义的对外成员，按出现顺序；“先功能、后成员”，便于扫读）：
//
// - 【全局实例访问】public static StateMachineDebugSettings Instance { get; set; }
//   用途：获取/设置全局调试配置（编辑器资源优先；运行时无资源时返回兜底实例）。
//
// - 【总开关】public bool enableDebug
//   用途：总开关（关闭后大多数调试日志不输出）。
//
// - 【状态切换日志】public bool logStateTransitions
//   用途：控制进入/退出/切换相关日志输出。
// - 【动画混合日志】public bool logAnimationBlending
//   用途：控制动画权重计算与混合过程日志输出。
// - 【三角化日志】public bool logTriangulation
//   用途：控制 2D 混合树三角化过程日志输出。
// - 【运行时初始化日志】public bool logRuntimeInit
//   用途：控制 Runtime 创建/初始化相关日志输出。
// - 【回退日志】public bool logFallback
//   用途：控制回退系统激活相关日志输出。
// - 【脏标记机制日志】public bool logDirtySystem
//   用途：控制 Dirty 标记与降级流程日志输出。
// - 【淡入淡出日志】public bool logFadeEffects
//   用途：控制淡入淡出过程日志输出。
//
// - 【错误/警告始终输出】public bool alwaysLogErrors
//   用途：即使关闭总开关，也允许错误与警告照常输出。
//
// - 【性能统计】public bool logPerformanceStats
//   用途：输出每帧性能统计数据（建议谨慎开启）。
// - 【权重明细】public bool logWeightDetails
//   用途：输出每个动画的权重明细（用于定位混合问题）。
//
// - 【快捷判断】public bool Is*Enabled { get; }
//   用途：各模块开关的组合判断（总开关 + 子开关）。
// - 【条件日志】public void Log*(string message)
//   用途：按对应开关输出日志。
// - 【强制输出】public void LogWarning/LogError(string message)
//   用途：输出警告/错误（受 alwaysLogErrors 影响）。
//
// Private/Internal：运行时兜底实例缓存（避免未创建资源时 STATEMACHINEDEBUG 下空引用）。
// ============================================================================

namespace ES
{
    /// <summary>
    /// 状态机全局调试设置 - 一键控制所有Debug日志输出
    /// 支持编辑器配置：在Project面板右键 Create → ES → 状态机调试设置
    /// </summary>
    [CreateAssetMenu(fileName = "StateMachineDebugSettings", menuName = "ES/状态机调试设置", order = 100)]
    public class StateMachineDebugSettings : ESEditorGlobalSo<StateMachineDebugSettings>
    {
        private static StateMachineDebugSettings _runtimeFallbackInstance;

        /// <summary>
        /// 安全的全局实例访问：
        /// - 编辑器/有资源时：返回已配置的全局 SO。
        /// - 运行时未创建资源时：返回一个临时实例（默认完全静默），避免 STATEMACHINEDEBUG 下空引用崩溃。
        /// </summary>
        public static new StateMachineDebugSettings Instance
        {
            get
            {
                var instance = ESEditorGlobalSo<StateMachineDebugSettings>.Instance;
                if (instance != null) return instance;

                if (_runtimeFallbackInstance == null)
                {
                    _runtimeFallbackInstance = CreateInstance<StateMachineDebugSettings>();
                    _runtimeFallbackInstance.hideFlags = HideFlags.HideAndDontSave;
                    _runtimeFallbackInstance.enableDebug = false;
                    _runtimeFallbackInstance.alwaysLogErrors = true;
                }

                return _runtimeFallbackInstance;
            }
            set => ESEditorGlobalSo<StateMachineDebugSettings>.Instance = value;
        }

        [TitleGroup("调试开关", "控制状态机各模块的调试日志输出", BoldTitle = true, Indent = false)]
        [LabelText("启用全局调试"), Tooltip("总开关，关闭后所有调试日志都不输出")]
        public bool enableDebug = false;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("状态切换日志"), Tooltip("记录状态进入/退出事件")]
        public bool logStateTransitions = true;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("动画混合日志"), Tooltip("记录动画权重计算和混合过程")]
        public bool logAnimationBlending = false;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("三角化日志"), Tooltip("记录2D混合树三角化过程")]
        public bool logTriangulation = true;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("运行时初始化日志"), Tooltip("记录运行时创建和初始化")]
        public bool logRuntimeInit = true;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("回退日志"), Tooltip("记录回退系统激活")]
        public bool logFallback = true;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("脏标记机制日志"), Tooltip("记录脏标记和降级过程")]
        public bool logDirtySystem = false;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("淡入淡出日志"), Tooltip("记录动画淡入淡出过程")]
        public bool logFadeEffects = true;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("错误和警告"), Tooltip("始终输出错误和警告（即使关闭全局调试）")]
        public bool alwaysLogErrors = true;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/性能监控")]
        [LabelText("性能统计"), Tooltip("输出每帧性能统计数据")]
        public bool logPerformanceStats = false;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/性能监控")]
        [LabelText("权重详细信息"), Tooltip("输出每个动画的详细权重值")]
        public bool logWeightDetails = false;


        public bool IsStateTransitionEnabled => enableDebug && logStateTransitions;
        public bool IsAnimationBlendEnabled => enableDebug && logAnimationBlending;
        public bool IsTriangulationEnabled => enableDebug && logTriangulation;
        public bool IsRuntimeInitEnabled => enableDebug && logRuntimeInit;
        public bool IsFallbackEnabled => enableDebug && logFallback;
        public bool IsDirtyEnabled => enableDebug && logDirtySystem;
        public bool IsFadeEnabled => enableDebug && logFadeEffects;
        public bool IsPerformanceEnabled => enableDebug && logPerformanceStats;
        public bool IsWeightDetailEnabled => enableDebug && logWeightDetails;


        /// <summary>
        /// 条件日志 - 状态切换
        /// </summary>
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public void LogStateTransition(string message)
        {
            if (enableDebug && logStateTransitions)
                Debug.Log($"[StateMachine] {message}");
        }

        /// <summary>
        /// 条件日志 - 动画混合
        /// </summary>
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public void LogAnimationBlend(string message)
        {
            if (enableDebug && logAnimationBlending)
                Debug.Log($"[Animation] {message}");
        }

        /// <summary>
        /// 条件日志 - 三角化
        /// </summary>
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public void LogTriangulation(string message)
        {
            if (enableDebug && logTriangulation)
                Debug.Log($"[Triangulation] {message}");
        }

        /// <summary>
        /// 条件日志 - Runtime初始化
        /// </summary>
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public void LogRuntimeInit(string message)
        {
            if (enableDebug && logRuntimeInit)
                Debug.Log($"[Runtime] {message}");
        }

        /// <summary>
        /// 条件日志 - FallBack
        /// </summary>
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public void LogFallback(string message)
        {
            if (enableDebug && logFallback)
                Debug.Log($"[FallBack] {message}");
        }

        /// <summary>
        /// 条件日志 - Dirty系统
        /// </summary>
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public void LogDirty(string message)
        {
            if (enableDebug && logDirtySystem)
                Debug.Log($"[Dirty] {message}");
        }

        /// <summary>
        /// 条件日志 - 淡入淡出效果
        /// </summary>
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public void LogFade(string message)
        {
            if (enableDebug && logFadeEffects)
                Debug.Log($"[Fade] {message}");
        }

        /// <summary>
        /// 条件日志 - 性能统计
        /// </summary>
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public void LogPerformance(string message)
        {
            if (enableDebug && logPerformanceStats)
                Debug.Log($"[Performance] {message}");
        }

        /// <summary>
        /// 条件日志 - 权重详细
        /// </summary>
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public void LogWeightDetail(string message)
        {
            if (enableDebug && logWeightDetails)
                Debug.Log($"[Weight] {message}");
        }

        /// <summary>
        /// 警告日志 - 根据alwaysLogErrors设置
        /// </summary>
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public void LogWarning(string message)
        {
            if (enableDebug || alwaysLogErrors)
                Debug.LogWarning($"[StateMachine Warning] {message}");
        }

        /// <summary>
        /// 错误日志 - 根据alwaysLogErrors设置
        /// </summary>
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public void LogError(string message)
        {
            if (enableDebug || alwaysLogErrors)
                Debug.LogError($"[StateMachine Error] {message}");
        }
    }
}
