using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    [Flags]
    public enum StateActivationCode : byte
    {
        [InspectorName("失败")]
        Fail = 0,
        [InspectorName("成功")]
        Success = 1 << 0,
        [InspectorName("有打断")]
        HasInterrupt = 1 << 1,
        [InspectorName("有合并")]
        HasMerge = 1 << 2,
        [InspectorName("重启")]
        Restart = 1 << 3
    }

    /// <summary>
    /// 状态激活测试结果 - 描述状态能否激活以及需要执行的操作
    /// </summary>
    [Serializable]
    public struct StateActivationResult
    {
        /// <summary>
        /// 激活码
        /// </summary>
        public StateActivationCode code;

        /// <summary>
        /// 失败原因
        /// </summary>
        public string failureReason;

        /// <summary>
        /// 运行时打断列表（仅当code包含HasInterrupt时有效）
        /// </summary>
        public List<StateBase> statesToInterrupt;

        /// <summary>
        /// 运行时打断数量
        /// </summary>
        public int interruptCount;

        public bool CanActivate => (code & StateActivationCode.Success) != 0;

        public bool RequiresInterruption => (code & StateActivationCode.HasInterrupt) != 0;

        public bool CanMerge => (code & StateActivationCode.HasMerge) != 0;

        public bool IsRestart => (code & StateActivationCode.Restart) != 0;

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器调试用：合并状态列表
        /// </summary>
        public List<StateBase> debugMergeStates;

        /// <summary>
        /// 编辑器调试用：合并数量
        /// </summary>
        public int debugMergeCount;
#endif
        /// <summary>
        /// 注意：调试列表为内部复用引用，仅用于编辑器观察。
        /// </summary>

        // 共享空List，避免重复分配(零GC优化)
        private static readonly List<StateBase> _sharedEmptyList = new List<StateBase>(0);

        // 共享结果（零GC优化，适用于不可变结果）
        public static readonly StateActivationResult SuccessNoMerge = new StateActivationResult
        {
            code = StateActivationCode.Success,
            failureReason = string.Empty,
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };

        public static readonly StateActivationResult SuccessMerge = new StateActivationResult
        {
            code = StateActivationCode.Success | StateActivationCode.HasMerge,
            failureReason = string.Empty,
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };

        public static readonly StateActivationResult SuccessInterrupt = new StateActivationResult
        {
            code = StateActivationCode.Success | StateActivationCode.HasInterrupt,
            failureReason = string.Empty,
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };

        public static readonly StateActivationResult SuccessRestart = new StateActivationResult
        {
            code = StateActivationCode.Success | StateActivationCode.Restart,
            failureReason = string.Empty,
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
    #if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
    #endif
        };

        public static readonly StateActivationResult FailureStateIsNull = new StateActivationResult
        {
            code = StateActivationCode.Fail,
            failureReason = StateFailureReasons.StateIsNull,
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };

        public static readonly StateActivationResult FailureMachineNotRunning = new StateActivationResult
        {
            code = StateActivationCode.Fail,
            failureReason = StateFailureReasons.MachineNotRunning,
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };

        public static readonly StateActivationResult FailureStateAlreadyRunning = new StateActivationResult
        {
            code = StateActivationCode.Fail,
            failureReason = StateFailureReasons.StateAlreadyRunning,
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };

        public static readonly StateActivationResult FailurePipelineNotFound = new StateActivationResult
        {
            code = StateActivationCode.Fail,
            failureReason = StateFailureReasons.PipelineNotFound,
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };

        public static readonly StateActivationResult FailurePipelineDisabled = new StateActivationResult
        {
            code = StateActivationCode.Fail,
            failureReason = StateFailureReasons.PipelineDisabled,
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };

        public static readonly StateActivationResult FailureInvalidPipelineIndex = new StateActivationResult
        {
            code = StateActivationCode.Fail,
            failureReason = StateFailureReasons.InvalidPipelineIndex,
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };

        public static readonly StateActivationResult FailureSupportFlagsNotSatisfied = new StateActivationResult
        {
            code = StateActivationCode.Fail,
            failureReason = StateFailureReasons.SupportFlagsNotSatisfied,
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };

        public static readonly StateActivationResult FailureCrossPipelineConflict = new StateActivationResult
        {
            code = StateActivationCode.Fail,
            failureReason = "跨流水线冲突",
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };

        public static readonly StateActivationResult FailureMergeConflict = new StateActivationResult
        {
            code = StateActivationCode.Fail,
            failureReason = "合并冲突",
            statesToInterrupt = _sharedEmptyList,
            interruptCount = 0
#if UNITY_EDITOR
            , debugMergeStates = _sharedEmptyList,
            debugMergeCount = 0
#endif
        };
    }
}
