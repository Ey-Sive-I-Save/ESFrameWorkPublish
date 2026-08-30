using System;

namespace ES
{
    /// <summary>
    /// ES 渲染指标采样会话。调用方负责从 Unity Profiler/FrameTiming 等来源读取一帧，
    /// 本会话负责样本边界、最大值聚合和一次性完成，避免把未完成数据写入 EvidenceReceipt。
    /// </summary>
    public sealed class ESRenderMetricCaptureSession
    {
        private readonly ESRenderMetricSamplingRequest request;
        private int sampleCount;
        private int drawCalls;
        private int setPassCalls;
        private float cpuMilliseconds;
        private float gpuMilliseconds;
        private int gcAllocBytes;
        private long residentMemoryBytes;
        private bool completed;

        private ESRenderMetricCaptureSession(ESRenderMetricSamplingRequest request)
        {
            this.request = request;
        }

        public int CapturedSampleCount => sampleCount;
        public bool IsCompleted => completed;

        public static bool TryCreate(
            ESRenderMetricSamplingRequest request,
            out ESRenderMetricCaptureSession session,
            out string reason)
        {
            if (!request.IsValid(out reason))
            {
                session = null;
                return false;
            }

            session = new ESRenderMetricCaptureSession(request);
            reason = string.Empty;
            return true;
        }

        public bool TryAddSample(
            int drawCalls,
            int setPassCalls,
            float cpuMilliseconds,
            float gpuMilliseconds,
            int gcAllocBytes,
            long residentMemoryBytes,
            out string reason)
        {
            if (completed)
            {
                reason = "capture-session-already-completed";
                return false;
            }
            if (sampleCount >= request.SampleCount)
            {
                reason = "capture-session-sample-limit-reached";
                return false;
            }
            if (float.IsNaN(cpuMilliseconds) || float.IsInfinity(cpuMilliseconds)
                || float.IsNaN(gpuMilliseconds) || float.IsInfinity(gpuMilliseconds))
            {
                reason = "metric-time-must-be-finite";
                return false;
            }

            sampleCount++;
            this.drawCalls = Math.Max(this.drawCalls, Math.Max(0, drawCalls));
            this.setPassCalls = Math.Max(this.setPassCalls, Math.Max(0, setPassCalls));
            this.cpuMilliseconds = Math.Max(this.cpuMilliseconds, Math.Max(0f, cpuMilliseconds));
            this.gpuMilliseconds = Math.Max(this.gpuMilliseconds, Math.Max(0f, gpuMilliseconds));
            this.gcAllocBytes = Math.Max(this.gcAllocBytes, Math.Max(0, gcAllocBytes));
            this.residentMemoryBytes = Math.Max(this.residentMemoryBytes, Math.Max(0L, residentMemoryBytes));
            reason = string.Empty;
            return true;
        }

        public bool TryComplete(out ESRenderMetricSnapshot snapshot, out string reason)
        {
            snapshot = default(ESRenderMetricSnapshot);
            if (completed)
            {
                reason = "capture-session-already-completed";
                return false;
            }
            if (sampleCount != request.SampleCount)
            {
                reason = "capture-session-requires-exact-sample-count";
                return false;
            }

            completed = true;
            snapshot = new ESRenderMetricSnapshot(
                request.Platform,
                request.Scenario,
                sampleCount,
                drawCalls,
                setPassCalls,
                cpuMilliseconds,
                gpuMilliseconds,
                gcAllocBytes,
                residentMemoryBytes,
                true);
            reason = string.Empty;
            return true;
        }
    }
}
