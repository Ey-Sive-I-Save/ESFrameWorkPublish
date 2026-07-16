using UnityEngine;

namespace ES
{
    /// <summary>
    /// Framework-level run state used by lightweight executable units.
    /// Keep this protocol generic; do not bind it to Command, Input, UI, or any single feature.
    /// </summary>
    public enum ESRunState : byte
    {
        [InspectorName("无")]
        None = 0,

        [InspectorName("运行中")]
        Running = 1,

        [InspectorName("成功")]
        Succeeded = 2,

        [InspectorName("失败")]
        Failed = 3,

        [InspectorName("已取消")]
        Canceled = 4,

        [InspectorName("已跳过")]
        Skipped = 5
    }

    public static class ESRunStateUtility
    {
        public static bool IsDone(ESRunState state)
        {
            return state == ESRunState.Succeeded
                   || state == ESRunState.Failed
                   || state == ESRunState.Canceled
                   || state == ESRunState.Skipped;
        }

        public static bool IsSuccessLike(ESRunState state)
        {
            return state == ESRunState.Succeeded || state == ESRunState.Skipped;
        }

        public static ESRunState FromTryResult(ESTryResult result)
        {
            if ((result & ESTryResult.Succeed) != 0)
                return ESRunState.Succeeded;

            if ((result & ESTryResult.Fail) != 0)
                return ESRunState.Failed;

            if ((result & ESTryResult.Trying) != 0)
                return ESRunState.Running;

            if ((result & ESTryResult.Repeat) != 0)
                return ESRunState.Skipped;

            return ESRunState.None;
        }
    }
}
