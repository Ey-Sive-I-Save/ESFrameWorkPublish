namespace ES
{
    /// <summary>
    /// 失败原因常量（零GC优化：避免字符串重复分配）
    /// </summary>
    public static class StateFailureReasons
    {
        public const string StateIsNull = "目标状态为空";
        public const string MachineNotRunning = "状态机未运行";
        public const string StateAlreadyRunning = "状态已在运行中";
        public const string PipelineNotFound = "流水线不存在";
        public const string PipelineDisabled = "流水线未启用";
        public const string InvalidPipelineIndex = "流水线索引非法";
        public const string SupportFlagsNotSatisfied = "支持标记条件未满足";
    }
}
