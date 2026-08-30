using System;

namespace ES
{
    /// <summary>
    /// 多个 ES URP 回归报告的确定性聚合。任何未证明或超预算报告都会保留在总体状态中。
    /// </summary>
    [Serializable]
    public sealed class ESRenderEvidenceAggregateReport
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string aggregateId = string.Empty;
        public string overallStatus = string.Empty;
        public int reportCount;
        public int measuredCount;
        public int unmeasuredCount;
        public int overrunCount;
        public int driftedReportCount;
        public ESRenderEvidenceReport[] reports = new ESRenderEvidenceReport[0];

        private ESRenderEvidenceAggregateReport() { }

        public static bool TryCreate(
            string aggregateId,
            ESRenderEvidenceReport[] source,
            out ESRenderEvidenceAggregateReport aggregate,
            out string reason)
        {
            aggregate = null;
            if (string.IsNullOrWhiteSpace(aggregateId)) { reason = "aggregate-id-required"; return false; }
            if (source == null || source.Length == 0) { reason = "aggregate-reports-required"; return false; }

            bool hasUnproven = false;
            bool hasOverrun = false;
            bool hasDrift = false;
            int measured = 0;
            int unmeasured = 0;
            int overruns = 0;
            int drifted = 0;
            var copy = new ESRenderEvidenceReport[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                ESRenderEvidenceReport report = source[i];
                if (report == null || report.schemaVersion != ESRenderEvidenceReport.CurrentSchemaVersion)
                { reason = "aggregate-report-invalid"; return false; }
                copy[i] = report;
                measured += report.evaluatedCount;
                unmeasured += report.unmeasuredCount;
                overruns += report.overrunCount;
                if (report.unmeasuredCount > 0) hasUnproven = true;
                if (report.overrunCount > 0) hasOverrun = true;
                if (report.changedCount > 0) { hasDrift = true; drifted++; }
            }

            aggregate = new ESRenderEvidenceAggregateReport
            {
                aggregateId = aggregateId,
                overallStatus = hasUnproven
                    ? (hasDrift ? ESRenderEvidenceBatchDecisionStatus.DriftedAndUnproven.ToString()
                        : (hasOverrun ? ESRenderEvidenceBatchDecisionStatus.BudgetOverrunAndUnproven.ToString()
                            : ESRenderEvidenceBatchDecisionStatus.Unproven.ToString()))
                    : (hasDrift ? ESRenderEvidenceBatchDecisionStatus.Drifted.ToString()
                        : (hasOverrun ? ESRenderEvidenceBatchDecisionStatus.BudgetOverrun.ToString()
                            : ESRenderEvidenceBatchDecisionStatus.Stable.ToString())),
                reportCount = copy.Length,
                measuredCount = measured,
                unmeasuredCount = unmeasured,
                overrunCount = overruns,
                driftedReportCount = drifted,
                reports = copy
            };
            reason = string.Empty;
            return true;
        }
    }
}
