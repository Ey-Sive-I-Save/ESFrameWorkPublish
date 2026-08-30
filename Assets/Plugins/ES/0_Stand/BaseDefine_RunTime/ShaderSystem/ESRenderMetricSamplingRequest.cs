using System;

namespace ES
{
    /// <summary>
    /// ES 运行时渲染采样请求。它只定义边界，不启动 Profiler 或读取 Unity 状态。
    /// </summary>
    public readonly struct ESRenderMetricSamplingRequest
    {
        public ESRenderMetricSamplingRequest(
            string platform,
            string scenario,
            int sampleCount)
        {
            Platform = platform ?? string.Empty;
            Scenario = scenario ?? string.Empty;
            SampleCount = Math.Max(0, sampleCount);
        }

        public string Platform { get; }
        public string Scenario { get; }
        public int SampleCount { get; }

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
