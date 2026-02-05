using System;

namespace ES
{
    /// <summary>
    /// 状态退出测试结果
    /// </summary>
    [Serializable]
    public struct StateExitResult
    {
        /// <summary>
        /// 是否允许退出
        /// </summary>
        public bool canExit;

        /// <summary>
        /// 失败原因（canExit 为 false 时有效）
        /// </summary>
        public string failureReason;

        /// <summary>
        /// 目标流水线
        /// </summary>
        public StatePipelineType pipeline;

        /// <summary>
        /// 构造成功结果
        /// </summary>
        /// <param name="pipelineType">目标流水线</param>
        /// <returns>允许退出的结果</returns>
        public static StateExitResult Success(StatePipelineType pipelineType)
        {
            return new StateExitResult { canExit = true, failureReason = string.Empty, pipeline = pipelineType };
        }

        /// <summary>
        /// 构造失败结果
        /// </summary>
        /// <param name="reason">失败原因</param>
        /// <param name="pipelineType">目标流水线</param>
        /// <returns>不允许退出的结果</returns>
        public static StateExitResult Failure(string reason, StatePipelineType pipelineType)
        {
            return new StateExitResult { canExit = false, failureReason = reason, pipeline = pipelineType };
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 示例：分析常见退出情况（仅编辑器使用，不参与运行时）
    /// </summary>
    internal static class StateExitResultSample
    {
        /// <summary>
        /// 常见退出情况分析（样例）
        /// </summary>
        /// <remarks>
        /// 仅示例说明，不会被运行时调用。
        /// </remarks>
        public static void AnalyzeCommonExitCases()
        {
            // 1) 目标状态为空
            var caseNull = StateExitResult.Failure("目标状态为空", StatePipelineType.Basic);

            // 2) 状态未在运行中
            var caseNotRunning = StateExitResult.Failure("状态未在运行中", StatePipelineType.Basic);

            // 3) 自定义退出测试拒绝
            var caseCustomDenied = StateExitResult.Failure("自定义退出测试未通过", StatePipelineType.Main);

            // 4) 正常允许退出
            var caseAllowed = StateExitResult.Success(StatePipelineType.Basic);

            // 这里仅为示例，避免运行时输出；如需查看可在编辑器调用并自行打印。
            _ = caseNull;
            _ = caseNotRunning;
            _ = caseCustomDenied;
            _ = caseAllowed;
        }
    }
#endif
}
