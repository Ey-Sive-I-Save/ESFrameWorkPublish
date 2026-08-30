namespace ES
{
    public sealed class ESRenderEvidenceBatchBudgetAudit
    {
        public int EvaluatedCount { get; private set; }
        public int UnmeasuredCount { get; private set; }
        public int OverrunCount { get; private set; }

        public bool HasMeasuredOverrun { get { return OverrunCount > 0; } }

        public static ESRenderEvidenceBatchBudgetAudit Evaluate(
            ESRenderEvidenceBatch batch,
            ESRenderQualityPolicy policy)
        {
            var result = new ESRenderEvidenceBatchBudgetAudit();
            if (batch == null || batch.receipts == null || !policy.IsValid(out _)) return result;
            foreach (var receipt in batch.receipts)
            {
                if (receipt == null || !receipt.runtimeCaptured || receipt.metricSampleCount <= 0)
                { result.UnmeasuredCount++; continue; }
                result.EvaluatedCount++;
                if (receipt.cpuMilliseconds > policy.TargetFrameMilliseconds
                    || receipt.gpuMilliseconds > policy.TargetFrameMilliseconds)
                    result.OverrunCount++;
            }
            return result;
        }
    }
}
