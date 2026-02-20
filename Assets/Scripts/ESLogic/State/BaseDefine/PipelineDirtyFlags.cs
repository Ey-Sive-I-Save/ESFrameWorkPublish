using System;

namespace ES
{
    /// <summary>
    /// 层级Dirty标记（独立运作，位标记组合，低开销）。
    /// </summary>
    [Flags]
    public enum PipelineDirtyFlags : byte
    {
        /// <summary>
        /// 无标记
        /// </summary>
        None = 0,

        /// <summary>
        /// 回退状态检查
        /// </summary>
        FallbackCheck = 1 << 0,

        /// <summary>
        /// 中优先级任务
        /// </summary>
        MediumPriority = 1 << 1,

        /// <summary>
        /// 高优先级任务
        /// </summary>
        HighPriority = 1 << 2,

        /// <summary>
        /// 热插拔相关任务
        /// </summary>
        HotPlug = 1 << 3,

        /// <summary>
        /// Mixer输入权重发生变化（需要更新参考姿态填充/归一化）
        /// </summary>
        MixerWeights = 1 << 4
    }
}
