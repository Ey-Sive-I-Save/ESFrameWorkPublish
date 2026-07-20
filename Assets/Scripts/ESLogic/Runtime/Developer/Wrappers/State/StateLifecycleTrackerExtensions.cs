using System.Runtime.CompilerServices;

namespace ES
{
    /// <summary>
    /// StateLifecycleTracker 的可选扩展能力。
    /// </summary>
    public static class StateLifecycleTrackerExtensions
    {
        /// <summary>
        /// 将生命周期活跃标记同步到绑定状态真实运行态。
        /// 返回 <c>true</c> 表示同步过程中活跃标记发生了变化。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SyncFromBoundState(this StateLifecycleTracker tracker)
        {
            if (tracker == null)
                return false;

            var state = tracker.BoundState;
            bool nextActive = state != null && state.baseStatus == StateBaseStatus.Running;

            if (nextActive)
                return tracker.TryEnter(true);

            if (!tracker.IsActive)
                return false;

            tracker.Release();
            return true;
        }
    }
}
