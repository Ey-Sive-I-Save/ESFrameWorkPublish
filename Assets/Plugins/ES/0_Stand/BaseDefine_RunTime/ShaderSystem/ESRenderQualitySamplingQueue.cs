using System;
using System.Collections.Generic;

namespace ES
{
    public enum ESRenderQualitySamplingQueueStatus
    {
        Ready = 0,
        InProgress = 1,
        Completed = 2
    }

    /// <summary>
    /// ES 多质量档采样编排器。只管理顺序和状态，不执行质量切换或 Profiler 采集。
    /// </summary>
    public sealed class ESRenderQualitySamplingQueue
    {
        private readonly ESRenderQualityProfileId[] profiles;
        private int cursor;
        private ESRenderQualitySamplingQueueStatus status;

        private ESRenderQualitySamplingQueue(ESRenderQualityProfileId[] profiles)
        {
            this.profiles = profiles;
            status = ESRenderQualitySamplingQueueStatus.Ready;
        }

        public ESRenderQualitySamplingQueueStatus Status => status;
        public int Count => profiles.Length;
        public int CompletedCount => cursor;
        public bool HasNext => cursor < profiles.Length;

        public static bool TryCreate(
            IReadOnlyList<ESRenderQualityProfileId> source,
            out ESRenderQualitySamplingQueue queue,
            out string reason)
        {
            queue = null;
            if (source == null || source.Count == 0)
            {
                reason = "sampling-queue-profiles-required";
                return false;
            }
            var unique = new HashSet<ESRenderQualityProfileId>();
            var copy = new ESRenderQualityProfileId[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                if (!Enum.IsDefined(typeof(ESRenderQualityProfileId), source[i]))
                {
                    reason = "sampling-queue-profile-unsupported";
                    return false;
                }
                if (!unique.Add(source[i]))
                {
                    reason = "sampling-queue-profile-duplicate";
                    return false;
                }
                copy[i] = source[i];
            }
            queue = new ESRenderQualitySamplingQueue(copy);
            reason = string.Empty;
            return true;
        }

        public bool TryBeginNext(out ESRenderQualityProfileId profile, out string reason)
        {
            profile = default(ESRenderQualityProfileId);
            if (status == ESRenderQualitySamplingQueueStatus.InProgress)
            {
                reason = "sampling-queue-item-in-progress";
                return false;
            }
            if (!HasNext)
            {
                status = ESRenderQualitySamplingQueueStatus.Completed;
                reason = "sampling-queue-completed";
                return false;
            }
            profile = profiles[cursor];
            status = ESRenderQualitySamplingQueueStatus.InProgress;
            reason = string.Empty;
            return true;
        }

        public bool TryCompleteCurrent(out string reason)
        {
            if (status != ESRenderQualitySamplingQueueStatus.InProgress)
            {
                reason = "sampling-queue-no-item-in-progress";
                return false;
            }
            cursor++;
            status = HasNext
                ? ESRenderQualitySamplingQueueStatus.Ready
                : ESRenderQualitySamplingQueueStatus.Completed;
            reason = string.Empty;
            return true;
        }
    }
}
