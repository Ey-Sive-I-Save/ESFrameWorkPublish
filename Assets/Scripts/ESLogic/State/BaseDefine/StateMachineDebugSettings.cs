using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 状态机全局调试设置 - 一键控制所有Debug日志输出
    /// 支持编辑器配置：在Project面板右键 Create → ES → StateMachine Debug Settings
    /// </summary>
    [CreateAssetMenu(fileName = "StateMachineDebugSettings", menuName = "ES/StateMachine Debug Settings", order = 100)]
    public class StateMachineDebugSettings : ESEditorGlobalSo<StateMachineDebugSettings>
    {
        [TitleGroup("调试开关", "控制状态机各模块的Debug日志输出", BoldTitle = true, Indent = false)]
        [LabelText("启用全局Debug"), Tooltip("总开关，关闭后所有Debug日志都不输出")]
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
        [LabelText("Runtime初始化日志"), Tooltip("记录Runtime创建和初始化")]
        public bool logRuntimeInit = true;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("FallBack日志"), Tooltip("记录FallBack系统激活")]
        public bool logFallback = true;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("Dirty机制日志"), Tooltip("记录Dirty标记和降级过程")]
        public bool logDirtySystem = false;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("淡入淡出日志"), Tooltip("记录动画淡入淡出过程")]
        public bool logFadeEffects = true;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/详细控制")]
        [LabelText("错误和警告"), Tooltip("始终输出错误和警告（即使enableDebug=false）")]
        public bool alwaysLogErrors = true;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/性能监控")]
        [LabelText("性能统计"), Tooltip("输出每帧性能统计数据")]
        public bool logPerformanceStats = false;

        [ShowIf("enableDebug")]
        [BoxGroup("调试开关/性能监控")]
        [LabelText("权重详细信息"), Tooltip("输出每个动画的详细权重值")]
        public bool logWeightDetails = false;


        /// <summary>
        /// 条件日志 - 状态切换
        /// </summary>
        public void LogStateTransition(string message)
        {
            if (enableDebug && logStateTransitions)
                Debug.Log($"[StateMachine] {message}");
        }

        /// <summary>
        /// 条件日志 - 动画混合
        /// </summary>
        public void LogAnimationBlend(string message)
        {
            if (enableDebug && logAnimationBlending)
                Debug.Log($"[Animation] {message}");
        }

        /// <summary>
        /// 条件日志 - 三角化
        /// </summary>
        public void LogTriangulation(string message)
        {
            if (enableDebug && logTriangulation)
                Debug.Log($"[Triangulation] {message}");
        }

        /// <summary>
        /// 条件日志 - Runtime初始化
        /// </summary>
        public void LogRuntimeInit(string message)
        {
            if (enableDebug && logRuntimeInit)
                Debug.Log($"[Runtime] {message}");
        }

        /// <summary>
        /// 条件日志 - FallBack
        /// </summary>
        public void LogFallback(string message)
        {
            if (enableDebug && logFallback)
                Debug.Log($"[FallBack] {message}");
        }

        /// <summary>
        /// 条件日志 - Dirty系统
        /// </summary>
        public void LogDirty(string message)
        {
            if (enableDebug && logDirtySystem)
                Debug.Log($"[Dirty] {message}");
        }

        /// <summary>
        /// 条件日志 - 淡入淡出效果
        /// </summary>
        public void LogFade(string message)
        {
            if (enableDebug && logFadeEffects)
                Debug.Log($"[Fade] {message}");
        }

        /// <summary>
        /// 条件日志 - 性能统计
        /// </summary>
        public void LogPerformance(string message)
        {
            if (enableDebug && logPerformanceStats)
                Debug.Log($"[Performance] {message}");
        }

        /// <summary>
        /// 条件日志 - 权重详细
        /// </summary>
        public void LogWeightDetail(string message)
        {
            if (enableDebug && logWeightDetails)
                Debug.Log($"[Weight] {message}");
        }

        /// <summary>
        /// 警告日志 - 根据alwaysLogErrors设置
        /// </summary>
        public void LogWarning(string message)
        {
            if (enableDebug || alwaysLogErrors)
                Debug.LogWarning($"[StateMachine Warning] {message}");
        }

        /// <summary>
        /// 错误日志 - 根据alwaysLogErrors设置
        /// </summary>
        public void LogError(string message)
        {
            if (enableDebug || alwaysLogErrors)
                Debug.LogError($"[StateMachine Error] {message}");
        }
    }
}
