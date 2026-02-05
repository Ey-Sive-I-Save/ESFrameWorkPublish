using System;
using System.Collections.Generic;

namespace ES
{
    /// <summary>
    /// 状态激活测试结果 - 描述状态能否激活以及需要执行的操作
    /// </summary>
    [Serializable]
    public struct StateActivationResult
    {
        // 共享空List，避免重复分配(零GC优化)
        private static readonly List<StateBase> _sharedEmptyList = new List<StateBase>(0);

        /// <summary>
        /// 是否可以激活
        /// </summary>
        public bool canActivate;

        /// <summary>
        /// 是否需要打断当前状态
        /// </summary>
        public bool requiresInterruption;

        /// <summary>
        /// 需要打断的状态列表
        /// </summary>
        public List<StateBase> statesToInterrupt;

        /// <summary>
        /// 是否可以直接合并（多个状态并行）
        /// </summary>
        public bool canMerge;

        /// <summary>
        /// 是否可以直接合并（无需打断）
        /// </summary>
        public bool mergeDirectly;

        /// <summary>
        /// 可以合并的状态列表
        /// </summary>
        public List<StateBase> statesToMergeWith;

        /// <summary>
        /// 打断数量（便于快速判断）
        /// </summary>
        public int interruptCount;

        /// <summary>
        /// 合并数量（便于快速判断）
        /// </summary>
        public int mergeCount;

        /// <summary>
        /// 失败原因
        /// </summary>
        public string failureReason;

        /// <summary>
        /// 目标流水线
        /// </summary>
        public StatePipelineType targetPipeline;

        /// <summary>
        /// 注意：statesToInterrupt / statesToMergeWith 为内部复用列表引用，不建议外部长期持有。
        /// </summary>

        /// <summary>
        /// 创建成功结果 (零GC优化：使用共享空List)
        /// </summary>
        public static StateActivationResult Success(StatePipelineType pipeline, bool merge = false)
        {
            return new StateActivationResult
            {
                canActivate = true,
                requiresInterruption = false,
                canMerge = merge,
                mergeDirectly = merge,
                statesToInterrupt = _sharedEmptyList,
                statesToMergeWith = _sharedEmptyList,
                interruptCount = 0,
                mergeCount = 0,
                failureReason = string.Empty,
                targetPipeline = pipeline
            };
        }

        /// <summary>
        /// 创建打断结果 (零GC优化)
        /// </summary>
        public static StateActivationResult Interrupt(StatePipelineType pipeline, List<StateBase> toInterrupt)
        {
            return new StateActivationResult
            {
                canActivate = true,
                requiresInterruption = true,
                canMerge = false,
                mergeDirectly = false,
                statesToInterrupt = toInterrupt,
                statesToMergeWith = _sharedEmptyList,
                interruptCount = toInterrupt?.Count ?? 0,
                mergeCount = 0,
                failureReason = string.Empty,
                targetPipeline = pipeline
            };
        }

        /// <summary>
        /// 创建合并结果 (零GC优化)
        /// </summary>
        public static StateActivationResult Merge(StatePipelineType pipeline, List<StateBase> mergeWith)
        {
            return new StateActivationResult
            {
                canActivate = true,
                requiresInterruption = false,
                canMerge = true,
                mergeDirectly = true,
                statesToInterrupt = _sharedEmptyList,
                statesToMergeWith = mergeWith,
                interruptCount = 0,
                mergeCount = mergeWith?.Count ?? 0,
                failureReason = string.Empty,
                targetPipeline = pipeline
            };
        }

        /// <summary>
        /// 创建失败结果 (零GC优化)
        /// </summary>
        public static StateActivationResult Failure(string reason)
        {
            return new StateActivationResult
            {
                canActivate = false,
                requiresInterruption = false,
                canMerge = false,
                mergeDirectly = false,
                statesToInterrupt = _sharedEmptyList,
                statesToMergeWith = _sharedEmptyList,
                interruptCount = 0,
                mergeCount = 0,
                failureReason = reason,
                targetPipeline = StatePipelineType.Basic
            };
        }
    }
}
