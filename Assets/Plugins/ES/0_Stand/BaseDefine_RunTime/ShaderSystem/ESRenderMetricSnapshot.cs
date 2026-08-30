using System;

namespace ES
{
    /// <summary>
    /// ES 渲染指标的不可变输入快照。数值由调用方提供；本类型不采集、不推断运行时数据。
    /// </summary>
    public readonly struct ESRenderMetricSnapshot
    {
        public ESRenderMetricSnapshot(
            string platform,
            string scenario,
            int sampleCount,
            int drawCalls,
            int setPassCalls,
            float cpuMilliseconds,
            float gpuMilliseconds,
            int gcAllocBytes,
            long residentMemoryBytes,
            bool runtimeCaptured)
        {
            Platform = platform ?? string.Empty;
            Scenario = scenario ?? string.Empty;
            SampleCount = Math.Max(0, sampleCount);
            DrawCalls = Math.Max(0, drawCalls);
            SetPassCalls = Math.Max(0, setPassCalls);
            CpuMilliseconds = Math.Max(0f, cpuMilliseconds);
            GpuMilliseconds = Math.Max(0f, gpuMilliseconds);
            GcAllocBytes = Math.Max(0, gcAllocBytes);
            ResidentMemoryBytes = Math.Max(0L, residentMemoryBytes);
            RuntimeCaptured = runtimeCaptured;
        }

        public string Platform { get; }
        public string Scenario { get; }
        public int SampleCount { get; }
        public int DrawCalls { get; }
        public int SetPassCalls { get; }
        public float CpuMilliseconds { get; }
        public float GpuMilliseconds { get; }
        public int GcAllocBytes { get; }
        public long ResidentMemoryBytes { get; }
        public bool RuntimeCaptured { get; }

        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(Platform)) { reason = "platform-required"; return false; }
            if (string.IsNullOrWhiteSpace(Scenario)) { reason = "scenario-required"; return false; }
            if (SampleCount <= 0) { reason = "sample-count-required"; return false; }
            reason = string.Empty;
            return true;
        }
    }
}
