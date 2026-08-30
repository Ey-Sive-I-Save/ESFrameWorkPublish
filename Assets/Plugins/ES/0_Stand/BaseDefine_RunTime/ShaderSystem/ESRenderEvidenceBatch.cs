using System;
using System.Collections.Generic;

namespace ES
{
    [Serializable]
    public sealed class ESRenderEvidenceBatch
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string batchId = string.Empty;
        public ESRenderBackendEvidenceReceipt[] receipts = new ESRenderBackendEvidenceReceipt[0];

        public static bool TryCreate(string batchId, ESRenderBackendEvidenceReceipt[] source,
            out ESRenderEvidenceBatch batch, out string reason)
        {
            batch = null;
            if (string.IsNullOrWhiteSpace(batchId)) { reason = "batch-id-required"; return false; }
            if (source == null || source.Length == 0) { reason = "batch-receipts-required"; return false; }
            if (source.Length > 256) { reason = "batch-size-limit-exceeded"; return false; }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null || string.IsNullOrWhiteSpace(source[i].idempotencyKey))
                { reason = "batch-receipt-invalid"; return false; }
                if (source[i].runtimeAcceptance)
                { reason = "batch-runtime-acceptance-forbidden"; return false; }
                if (!keys.Add(source[i].idempotencyKey)) { reason = "batch-idempotency-key-duplicate"; return false; }
            }
            batch = new ESRenderEvidenceBatch { batchId = batchId, receipts = (ESRenderBackendEvidenceReceipt[])source.Clone() };
            reason = string.Empty;
            return true;
        }
    }
}
