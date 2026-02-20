using System.Runtime.CompilerServices;

// ============================================================================
// 文件：StateMachineDebug.cs
// 作用：状态机调试日志门面（仅编辑器/STATEMACHINEDEBUG 下生效；非编辑器下为空实现）。
//
// Public（本文件定义的对外成员；按模块分组，“先功能、后成员”，便于扫读）：
//
// 【条件日志（STATEMACHINEDEBUG）】
// - 普通日志：public static void Log(string message)
// - 警告日志：public static void LogWarning(string message)
// - 错误日志：public static void LogError(string message)
//
// Private/Internal：无（仅转发到 StateMachineDebugSettings）。
// ============================================================================

namespace ES
{
#if UNITY_EDITOR
    public static class StateMachineDebug
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public static void Log(string message)
        {
            StateMachineDebugSettings.Instance.LogStateTransition(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public static void LogWarning(string message)
        {
            StateMachineDebugSettings.Instance.LogWarning(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public static void LogError(string message)
        {
            StateMachineDebugSettings.Instance.LogError(message);
        }
    }
#else
    public static class StateMachineDebug
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public static void Log(string message) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public static void LogWarning(string message) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.Conditional("STATEMACHINEDEBUG")]
        public static void LogError(string message) { }
    }
#endif
}
