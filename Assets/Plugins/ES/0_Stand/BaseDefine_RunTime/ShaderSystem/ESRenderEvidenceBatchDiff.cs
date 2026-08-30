using System;
using System.Collections.Generic;

namespace ES
{
    public sealed class ESRenderEvidenceBatchDiff
    {
        public int AddedCount { get; private set; }
        public int RemovedCount { get; private set; }
        public int ChangedCount { get; private set; }
        public string[] ChangedIdempotencyKeys { get; private set; }
        public bool HasChanges { get { return AddedCount > 0 || RemovedCount > 0 || ChangedCount > 0; } }
        public bool IsIdentical { get { return !HasChanges; } }

        public static ESRenderEvidenceBatchDiff Compare(ESRenderEvidenceBatch baseline, ESRenderEvidenceBatch candidate)
        {
            var result = new ESRenderEvidenceBatchDiff { ChangedIdempotencyKeys = new string[0] };
            var before = ToMap(baseline);
            var after = ToMap(candidate);
            var changed = new List<string>();
            foreach (var pair in after)
            {
                ESRenderBackendEvidenceReceipt oldReceipt;
                if (!before.TryGetValue(pair.Key, out oldReceipt)) { result.AddedCount++; continue; }
                if (!Equivalent(oldReceipt, pair.Value)) changed.Add(pair.Key);
            }
            foreach (var pair in before)
                if (!after.ContainsKey(pair.Key)) result.RemovedCount++;
            result.ChangedCount = changed.Count;
            result.ChangedIdempotencyKeys = changed.ToArray();
            return result;
        }

        private static Dictionary<string, ESRenderBackendEvidenceReceipt> ToMap(ESRenderEvidenceBatch batch)
        {
            var map = new Dictionary<string, ESRenderBackendEvidenceReceipt>(StringComparer.Ordinal);
            if (batch == null || batch.receipts == null) return map;
            foreach (var receipt in batch.receipts)
                if (receipt != null && !string.IsNullOrWhiteSpace(receipt.idempotencyKey)) map[receipt.idempotencyKey] = receipt;
            return map;
        }

        private static bool Equivalent(ESRenderBackendEvidenceReceipt a, ESRenderBackendEvidenceReceipt b)
        {
            return a.receiptStatus == b.receiptStatus
                && a.qualityProfile == b.qualityProfile
                && a.compatibilityStatus == b.compatibilityStatus
                && a.pipelineAssetName == b.pipelineAssetName
                && a.rendererDataName == b.rendererDataName
                && a.rendererFeatureCount == b.rendererFeatureCount
                && a.rendererFeatureTypeFingerprint == b.rendererFeatureTypeFingerprint
                && a.volumeProfileAssetCount == b.volumeProfileAssetCount
                && a.volumeComponentCount == b.volumeComponentCount
                && a.volumeComponentTypeFingerprint == b.volumeComponentTypeFingerprint
                && a.shaderAssetCount == b.shaderAssetCount
                && a.drawCalls == b.drawCalls
                && a.setPassCalls == b.setPassCalls
                && Math.Abs(a.cpuMilliseconds - b.cpuMilliseconds) < 0.0001f
                && Math.Abs(a.gpuMilliseconds - b.gpuMilliseconds) < 0.0001f
                && a.gcAllocBytes == b.gcAllocBytes
                && a.residentMemoryBytes == b.residentMemoryBytes;
        }
    }
}
