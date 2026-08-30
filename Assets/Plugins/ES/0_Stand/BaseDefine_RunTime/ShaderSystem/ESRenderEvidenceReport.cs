using System;

namespace ES
{
    /// <summary>
    /// ES URP 渲染回归报告。聚合候选批次、基线差异、预算审计和场景摘要。
    /// 报告只表达证据状态，不把静态或未测量数据升级为运行验收。
    /// </summary>
    [Serializable]
    public sealed class ESRenderEvidenceReport
    {
        // candidate-only: this report never declares task completion or runtime acceptance.
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string reportId = string.Empty;
        public string decisionStatus = string.Empty;
        public int addedCount;
        public int removedCount;
        public int changedCount;
        public int evaluatedCount;
        public int unmeasuredCount;
        public int overrunCount;
        public ESRenderEvidenceScenarioSummary[] scenarioSummaries = new ESRenderEvidenceScenarioSummary[0];
        public ESRenderEvidenceBatch batch;

        public ESRenderEvidenceReport() { }

        public static bool TryCreate(
            string reportId,
            ESRenderEvidenceBatch baseline,
            ESRenderEvidenceBatch candidate,
            ESRenderQualityPolicy policy,
            out ESRenderEvidenceReport report,
            out string reason)
        {
            report = null;
            if (string.IsNullOrWhiteSpace(reportId)) { reason = "report-id-required"; return false; }
            if (candidate == null || candidate.receipts == null || candidate.receipts.Length == 0)
            { reason = "report-candidate-batch-required"; return false; }
            if (!policy.IsValid(out reason)) return false;

            ESRenderEvidenceBatchDecision decision = ESRenderEvidenceBatchDecision.Evaluate(baseline, candidate, policy);
            ESRenderEvidenceBatchBudgetAudit audit = decision.BudgetAudit;
            ESRenderEvidenceBatchDiff diff = decision.Diff;
            report = new ESRenderEvidenceReport
            {
                reportId = reportId,
                decisionStatus = decision.Status.ToString(),
                addedCount = diff.AddedCount,
                removedCount = diff.RemovedCount,
                changedCount = diff.ChangedCount,
                evaluatedCount = audit.EvaluatedCount,
                unmeasuredCount = audit.UnmeasuredCount,
                overrunCount = audit.OverrunCount,
                scenarioSummaries = ESRenderEvidenceScenarioSummary.Build(candidate, policy),
                batch = candidate
            };
            reason = string.Empty;
            return true;
        }
    }
}
