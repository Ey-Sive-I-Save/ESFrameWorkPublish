// ============================================================================
// 文件：StateBase.Debug.cs
// 作用：StateBase 的编辑器/开发期调试输出（仅用于诊断，不影响正式运行时逻辑）。
//
// Public（本文件定义的对外成员；按模块分组，“先功能、后成员”，便于扫读）：
//
// 【调试摘要】
// - 获取状态摘要：public string GetDebugSummary()
//
// Private/Internal：无（仅拼装调试摘要）。
// ============================================================================

namespace ES
{
    public partial class StateBase
    {
        #region 调试与诊断（Editor/Development）

#if UNITY_EDITOR
        /// <summary>
        /// 获取状态的调试摘要（Editor Only）
        /// </summary>
        public string GetDebugSummary()
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append($"[State] {GetStateNameSafe()}");
            sb.Append($" | Status={baseStatus} Phase={_runtimePhase}");
            sb.Append($" | Weight={_playableWeight:F2}");
            sb.Append($" | Time={hasEnterTime:F2}s Progress={normalizedProgress:F2} Loop={loopCount}");
            if (_ikActive && _animationRuntime != null)
                sb.Append($" | IK={_animationRuntime.ik.weight:F2}");
            if (_matchTargetActive)
                sb.Append(" | MT=Active");
            if (_shouldAutoExitFromAnimation)
                sb.Append(" | AutoExit=Pending");
            if (_animationRuntime != null)
                sb.Append($" | Mem={_animationRuntime.GetMemoryFootprint()}B");
            return sb.ToString();
        }
#endif

        #endregion
    }
}
