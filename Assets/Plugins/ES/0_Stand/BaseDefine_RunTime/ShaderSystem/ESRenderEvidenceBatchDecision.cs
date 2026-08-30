namespace ES
{
    public enum ESRenderEvidenceBatchDecisionStatus
    {
        Stable = 0,
        Drifted = 1,
        BudgetOverrun = 2,
        Unproven = 3,
        DriftedAndUnproven = 4,
        BudgetOverrunAndUnproven = 5
    }

    public sealed class ESRenderEvidenceBatchDecision
    {
        public ESRenderEvidenceBatchDiff Diff { get; private set; }
        public ESRenderEvidenceBatchBudgetAudit BudgetAudit { get; private set; }
        public ESRenderEvidenceBatchDecisionStatus Status { get; private set; }

        public static ESRenderEvidenceBatchDecision Evaluate(
            ESRenderEvidenceBatch baseline,
            ESRenderEvidenceBatch candidate,
            ESRenderQualityPolicy policy)
        {
            var diff = ESRenderEvidenceBatchDiff.Compare(baseline, candidate);
            var budget = ESRenderEvidenceBatchBudgetAudit.Evaluate(candidate, policy);
            bool drifted = diff.HasChanges;
            bool overrun = budget.HasMeasuredOverrun;
            bool unproven = budget.UnmeasuredCount > 0;
            ESRenderEvidenceBatchDecisionStatus status = unproven
                ? (drifted ? ESRenderEvidenceBatchDecisionStatus.DriftedAndUnproven
                    : (overrun ? ESRenderEvidenceBatchDecisionStatus.BudgetOverrunAndUnproven
                        : ESRenderEvidenceBatchDecisionStatus.Unproven))
                : (drifted ? ESRenderEvidenceBatchDecisionStatus.Drifted
                    : (overrun ? ESRenderEvidenceBatchDecisionStatus.BudgetOverrun
                        : ESRenderEvidenceBatchDecisionStatus.Stable));
            return new ESRenderEvidenceBatchDecision { Diff = diff, BudgetAudit = budget, Status = status };
        }
    }
}
