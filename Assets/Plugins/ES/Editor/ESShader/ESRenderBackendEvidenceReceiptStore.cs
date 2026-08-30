using System;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Evidence Receipt 的确定性 JSON 适配器。只负责校验和转换，不读写文件。
    /// </summary>
    public static class ESRenderBackendEvidenceReceiptStore
    {
        public static bool TryCreateWithResourceSnapshot(
            ESRenderBackendChangePlan plan,
            ESRenderBackendReceipt backendReceipt,
            string idempotencyKey,
            ESRenderBackendResourceSnapshot resourceSnapshot,
            out ESRenderBackendEvidenceReceipt receipt,
            out string reason)
        {
            receipt = null;
            if (resourceSnapshot == null)
            {
                reason = "resource-snapshot-required";
                return false;
            }

            try
            {
                receipt = ESRenderBackendEvidenceReceipt.Create(
                    plan,
                    backendReceipt,
                    idempotencyKey,
                    resourceSnapshot.pipelineAssetName,
                    resourceSnapshot.rendererDataTypeName,
                    resourceSnapshot.rendererDataName);
                BindCompatibility(receipt, resourceSnapshot);
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = "evidence-receipt-create-failed-" + exception.GetType().Name;
                return false;
            }
        }

        public static bool TryCreateWithResourceAndVolumeSnapshot(
            ESRenderBackendChangePlan plan,
            ESRenderBackendReceipt backendReceipt,
            string idempotencyKey,
            ESRenderBackendResourceSnapshot resourceSnapshot,
            ESRenderVolumeResourceSnapshot volumeSnapshot,
            out ESRenderBackendEvidenceReceipt receipt,
            out string reason)
        {
            receipt = null;
            if (resourceSnapshot == null || volumeSnapshot == null)
            {
                reason = "resource-and-volume-snapshots-required";
                return false;
            }

            try
            {
                receipt = ESRenderBackendEvidenceReceipt.Create(
                    plan,
                    backendReceipt,
                    idempotencyKey,
                    resourceSnapshot.pipelineAssetName,
                    resourceSnapshot.rendererDataTypeName,
                    resourceSnapshot.rendererDataName,
                    volumeSnapshot.profileAssetCount,
                    volumeSnapshot.profileGuidFingerprint,
                    volumeSnapshot.profileNameFingerprint);
                BindCompatibility(receipt, resourceSnapshot);
                BindVolume(receipt, volumeSnapshot);
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = "evidence-receipt-create-failed-" + exception.GetType().Name;
                return false;
            }
        }

        public static bool TryCreateWithAllResourceSnapshots(
            ESRenderBackendChangePlan plan,
            ESRenderBackendReceipt backendReceipt,
            string idempotencyKey,
            ESRenderBackendResourceSnapshot resourceSnapshot,
            ESRenderVolumeResourceSnapshot volumeSnapshot,
            ESRenderShaderResourceSnapshot shaderSnapshot,
            out ESRenderBackendEvidenceReceipt receipt,
            out string reason)
        {
            receipt = null;
            if (resourceSnapshot == null || volumeSnapshot == null || shaderSnapshot == null)
            {
                reason = "resource-volume-and-shader-snapshots-required";
                return false;
            }

            try
            {
                receipt = ESRenderBackendEvidenceReceipt.Create(
                    plan,
                    backendReceipt,
                    idempotencyKey,
                    resourceSnapshot.pipelineAssetName,
                    resourceSnapshot.rendererDataTypeName,
                    resourceSnapshot.rendererDataName,
                    volumeSnapshot.profileAssetCount,
                    volumeSnapshot.profileGuidFingerprint,
                    volumeSnapshot.profileNameFingerprint,
                    shaderSnapshot.shaderAssetCount,
                    shaderSnapshot.keywordSpaceShaderCount,
                    shaderSnapshot.shaderGuidFingerprint,
                    shaderSnapshot.keywordFingerprint);
                BindCompatibility(receipt, resourceSnapshot);
                BindVolume(receipt, volumeSnapshot);
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = "evidence-receipt-create-failed-" + exception.GetType().Name;
                return false;
            }
        }

        public static bool TryCreateWithAllResourceAndMetricsSnapshots(
            ESRenderBackendChangePlan plan,
            ESRenderBackendReceipt backendReceipt,
            string idempotencyKey,
            ESRenderBackendResourceSnapshot resourceSnapshot,
            ESRenderVolumeResourceSnapshot volumeSnapshot,
            ESRenderShaderResourceSnapshot shaderSnapshot,
            ESRenderMetricSnapshot metricSnapshot,
            out ESRenderBackendEvidenceReceipt receipt,
            out string reason)
        {
            if (!metricSnapshot.IsValid(out reason)) { receipt = null; return false; }
            if (!TryCreateWithAllResourceSnapshots(plan, backendReceipt, idempotencyKey,
                resourceSnapshot, volumeSnapshot, shaderSnapshot, out receipt, out reason)) return false;
            receipt.metricPlatform = metricSnapshot.Platform;
            receipt.metricScenario = metricSnapshot.Scenario;
            receipt.metricSampleCount = metricSnapshot.SampleCount;
            receipt.drawCalls = metricSnapshot.DrawCalls;
            receipt.setPassCalls = metricSnapshot.SetPassCalls;
            receipt.cpuMilliseconds = metricSnapshot.CpuMilliseconds;
            receipt.gpuMilliseconds = metricSnapshot.GpuMilliseconds;
            receipt.gcAllocBytes = metricSnapshot.GcAllocBytes;
            receipt.residentMemoryBytes = metricSnapshot.ResidentMemoryBytes;
            receipt.runtimeCaptured = metricSnapshot.RuntimeCaptured;
            return true;
        }

        public static bool TrySerialize(
            ESRenderBackendEvidenceReceipt receipt,
            out string json,
            out string reason)
        {
            json = string.Empty;
            if (!IsValid(receipt, out reason))
                return false;
            json = JsonUtility.ToJson(receipt, true);
            reason = string.Empty;
            return true;
        }

        public static bool TryDeserialize(
            string json,
            out ESRenderBackendEvidenceReceipt receipt,
            out string reason)
        {
            receipt = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                reason = "receipt-json-required";
                return false;
            }

            try
            {
                receipt = JsonUtility.FromJson<ESRenderBackendEvidenceReceipt>(json);
            }
            catch (Exception exception)
            {
                reason = "receipt-json-invalid-" + exception.GetType().Name;
                return false;
            }

            if (!IsValid(receipt, out reason))
            {
                receipt = null;
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public static bool TrySerializeBatch(ESRenderEvidenceBatch batch, out string json, out string reason)
        {
            json = string.Empty;
            if (batch == null || batch.schemaVersion != ESRenderEvidenceBatch.CurrentSchemaVersion)
            { reason = "batch-schema-invalid"; return false; }
            ESRenderEvidenceBatch validated;
            if (!ESRenderEvidenceBatch.TryCreate(batch.batchId, batch.receipts, out validated, out reason)) return false;
            json = JsonUtility.ToJson(validated, true);
            reason = string.Empty;
            return true;
        }

        public static bool TrySerializeReport(
            ESRenderEvidenceReport report,
            out string json,
            out string reason)
        {
            json = string.Empty;
            if (report == null || report.schemaVersion != ESRenderEvidenceReport.CurrentSchemaVersion)
            {
                reason = "report-schema-invalid";
                return false;
            }
            if (string.IsNullOrWhiteSpace(report.reportId) || report.batch == null)
            {
                reason = "report-identity-and-batch-required";
                return false;
            }
            json = JsonUtility.ToJson(report, true);
            reason = string.Empty;
            return true;
        }

        public static bool TrySerializeAggregateReport(
            ESRenderEvidenceAggregateReport aggregate,
            out string json,
            out string reason)
        {
            json = string.Empty;
            if (aggregate == null || aggregate.schemaVersion != ESRenderEvidenceAggregateReport.CurrentSchemaVersion)
            {
                reason = "aggregate-report-schema-invalid";
                return false;
            }
            if (string.IsNullOrWhiteSpace(aggregate.aggregateId)
                || aggregate.reports == null || aggregate.reports.Length == 0)
            {
                reason = "aggregate-report-identity-and-reports-required";
                return false;
            }
            json = JsonUtility.ToJson(aggregate, true);
            reason = string.Empty;
            return true;
        }

        public static bool TryValidateReportOutputPath(
            string projectRoot,
            string candidatePath,
            out string normalizedPath,
            out string reason)
        {
            return ESRenderEvidencePathPolicy.TryValidate(
                projectRoot, candidatePath, out normalizedPath, out reason);
        }

        public static bool TryDeserializeBatch(string json, out ESRenderEvidenceBatch batch, out string reason)
        {
            batch = null;
            if (string.IsNullOrWhiteSpace(json)) { reason = "batch-json-required"; return false; }
            try { batch = JsonUtility.FromJson<ESRenderEvidenceBatch>(json); }
            catch (Exception exception) { reason = "batch-json-invalid-" + exception.GetType().Name; return false; }
            ESRenderEvidenceBatch validated;
            if (batch == null || !ESRenderEvidenceBatch.TryCreate(batch.batchId, batch.receipts, out validated, out reason))
            { batch = null; return false; }
            batch = validated;
            reason = string.Empty;
            return true;
        }

        private static bool IsValid(
            ESRenderBackendEvidenceReceipt receipt,
            out string reason)
        {
            if (receipt == null)
            {
                reason = "receipt-required";
                return false;
            }
            if (receipt.schemaVersion != ESRenderBackendEvidenceReceipt.CurrentSchemaVersion)
            {
                reason = "receipt-schema-version-unsupported";
                return false;
            }
            if (string.IsNullOrWhiteSpace(receipt.idempotencyKey))
            {
                reason = "receipt-idempotency-key-required";
                return false;
            }
            if (!string.Equals(receipt.operation, "apply", StringComparison.Ordinal)
                && !string.Equals(receipt.operation, "rollback", StringComparison.Ordinal))
            {
                reason = "receipt-operation-unsupported";
                return false;
            }
            if (!Enum.TryParse(
                    receipt.receiptStatus,
                    ignoreCase: false,
                    out ESRenderBackendReceiptStatus parsedStatus)
                || !Enum.IsDefined(typeof(ESRenderBackendReceiptStatus), parsedStatus))
            {
                reason = "receipt-status-unsupported";
                return false;
            }
            if (receipt.runtimeAcceptance)
            {
                reason = "runtime-acceptance-cannot-be-serialized-in-static-receipt";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static void BindCompatibility(
            ESRenderBackendEvidenceReceipt receipt,
            ESRenderBackendResourceSnapshot resourceSnapshot)
        {
            receipt.compatibilityStatus = resourceSnapshot.compatibilityStatus ?? string.Empty;
            receipt.compatibilityReason = resourceSnapshot.compatibilityReason ?? string.Empty;
            receipt.unityVersion = resourceSnapshot.unityVersion ?? string.Empty;
            receipt.urpPackageVersion = resourceSnapshot.urpPackageVersion ?? string.Empty;
            receipt.rendererFeatureCount = resourceSnapshot.rendererFeatureCount;
            receipt.rendererFeatureTypeFingerprint = resourceSnapshot.rendererFeatureTypeFingerprint ?? string.Empty;
        }

        private static void BindVolume(
            ESRenderBackendEvidenceReceipt receipt,
            ESRenderVolumeResourceSnapshot volumeSnapshot)
        {
            receipt.volumeComponentCount = volumeSnapshot.componentCount;
            receipt.volumeComponentTypeFingerprint = volumeSnapshot.componentTypeFingerprint ?? string.Empty;
        }
    }
}
