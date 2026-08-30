using System;
using System.Collections.Generic;
using System.Linq;

namespace ES
{
    public sealed class ESRenderEvidenceScenarioSummary
    {
        public string Scenario { get; private set; }
        public string Platform { get; private set; }
        public string QualityProfile { get; private set; }
        public int ReceiptCount { get; private set; }
        public int MeasuredCount { get; private set; }
        public int UnmeasuredCount { get; private set; }
        public int OverrunCount { get; private set; }

        internal ESRenderEvidenceScenarioSummary(string qualityProfile, string platform, string scenario) { QualityProfile = qualityProfile; Platform = platform; Scenario = scenario; }

        public static ESRenderEvidenceScenarioSummary[] Build(
            ESRenderEvidenceBatch batch, ESRenderQualityPolicy policy)
        {
            var map = new Dictionary<string, ESRenderEvidenceScenarioSummary>(StringComparer.Ordinal);
            if (batch == null || batch.receipts == null) return new ESRenderEvidenceScenarioSummary[0];
            foreach (var receipt in batch.receipts)
            {
                string scenario = receipt == null ? string.Empty : receipt.metricScenario ?? string.Empty;
                string platform = receipt == null ? string.Empty : receipt.metricPlatform ?? string.Empty;
                string qualityProfile = receipt == null ? string.Empty : receipt.qualityProfile ?? string.Empty;
                if (string.IsNullOrWhiteSpace(scenario)) continue;
                string key = qualityProfile + "\u001E" + platform + "\u001E" + scenario;
                ESRenderEvidenceScenarioSummary summary;
                if (!map.TryGetValue(key, out summary))
                    map.Add(key, summary = new ESRenderEvidenceScenarioSummary(qualityProfile, platform, scenario));
                summary.ReceiptCount++;
                if (receipt.runtimeCaptured && receipt.metricSampleCount > 0)
                {
                    summary.MeasuredCount++;
                    if (receipt.cpuMilliseconds > policy.TargetFrameMilliseconds
                        || receipt.gpuMilliseconds > policy.TargetFrameMilliseconds) summary.OverrunCount++;
                }
                else summary.UnmeasuredCount++;
            }
            return map.Values.OrderBy(value => value.QualityProfile, StringComparer.Ordinal)
                .ThenBy(value => value.Platform, StringComparer.Ordinal)
                .ThenBy(value => value.Scenario, StringComparer.Ordinal).ToArray();
        }
    }
}
