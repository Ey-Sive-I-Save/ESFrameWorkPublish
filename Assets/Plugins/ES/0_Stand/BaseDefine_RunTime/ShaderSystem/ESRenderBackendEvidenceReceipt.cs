using System;

namespace ES
{
    /// <summary>
    /// 渲染后端一次操作的结构化静态证据。它不代表 Profiler、视觉或发布验收。
    /// </summary>
    [Serializable]
    public sealed class ESRenderBackendEvidenceReceipt
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string operation = string.Empty;
        public string qualityProfile = string.Empty;
        public string idempotencyKey = string.Empty;
        public string planStatus = string.Empty;
        public string receiptStatus = string.Empty;
        public string reason = string.Empty;
        public string pipelineAssetName = string.Empty;
        public string rendererDataTypeName = string.Empty;
        public string rendererDataName = string.Empty;
        public int volumeProfileAssetCount;
        public string volumeProfileGuidFingerprint = string.Empty;
        public string volumeProfileNameFingerprint = string.Empty;
        public int shaderAssetCount;
        public int keywordSpaceShaderCount;
        public string shaderGuidFingerprint = string.Empty;
        public string keywordFingerprint = string.Empty;
        public string compatibilityStatus = string.Empty;
        public string compatibilityReason = string.Empty;
        public string unityVersion = string.Empty;
        public string urpPackageVersion = string.Empty;
        public int rendererFeatureCount;
        public string rendererFeatureTypeFingerprint = string.Empty;
        public int volumeComponentCount;
        public string volumeComponentTypeFingerprint = string.Empty;
        public string metricPlatform = string.Empty;
        public string metricScenario = string.Empty;
        public int metricSampleCount;
        public int drawCalls;
        public int setPassCalls;
        public float cpuMilliseconds;
        public float gpuMilliseconds;
        public int gcAllocBytes;
        public long residentMemoryBytes;
        public bool runtimeCaptured;
        public bool backendStateVerified;
        public bool runtimeAcceptance;

        private ESRenderBackendEvidenceReceipt() { }

        public static ESRenderBackendEvidenceReceipt Create(
            ESRenderBackendChangePlan plan,
            ESRenderBackendReceipt receipt,
            string idempotencyKey)
        {
            return Create(plan, receipt, idempotencyKey, string.Empty, string.Empty, string.Empty);
        }

        public static ESRenderBackendEvidenceReceipt Create(
            ESRenderBackendChangePlan plan,
            ESRenderBackendReceipt receipt,
            string idempotencyKey,
            string pipelineAssetName,
            string rendererDataTypeName,
            string rendererDataName)
        {
            return Create(
                plan, receipt, idempotencyKey,
                pipelineAssetName, rendererDataTypeName, rendererDataName,
                0, string.Empty, string.Empty);
        }

        public static ESRenderBackendEvidenceReceipt Create(
            ESRenderBackendChangePlan plan,
            ESRenderBackendReceipt receipt,
            string idempotencyKey,
            string pipelineAssetName,
            string rendererDataTypeName,
            string rendererDataName,
            int volumeProfileAssetCount,
            string volumeProfileGuidFingerprint,
            string volumeProfileNameFingerprint)
        {
            return Create(
                plan, receipt, idempotencyKey,
                pipelineAssetName, rendererDataTypeName, rendererDataName,
                volumeProfileAssetCount, volumeProfileGuidFingerprint, volumeProfileNameFingerprint,
                0, 0, string.Empty, string.Empty);
        }

        public static ESRenderBackendEvidenceReceipt Create(
            ESRenderBackendChangePlan plan,
            ESRenderBackendReceipt receipt,
            string idempotencyKey,
            string pipelineAssetName,
            string rendererDataTypeName,
            string rendererDataName,
            int volumeProfileAssetCount,
            string volumeProfileGuidFingerprint,
            string volumeProfileNameFingerprint,
            int shaderAssetCount,
            int keywordSpaceShaderCount,
            string shaderGuidFingerprint,
            string keywordFingerprint)
        {
            if (plan.Status == ESRenderBackendPlanStatus.Invalid)
                throw new ArgumentException("Invalid render backend plan.", nameof(plan));
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ArgumentException("A non-empty idempotency key is required.", nameof(idempotencyKey));

            return new ESRenderBackendEvidenceReceipt
            {
                operation = receipt.Status == ESRenderBackendReceiptStatus.RolledBack
                    ? "rollback"
                    : "apply",
                qualityProfile = plan.TargetPolicy.Profile.ToString(),
                idempotencyKey = idempotencyKey,
                planStatus = plan.Status.ToString(),
                receiptStatus = receipt.Status.ToString(),
                reason = receipt.Reason ?? string.Empty,
                pipelineAssetName = pipelineAssetName ?? string.Empty,
                rendererDataTypeName = rendererDataTypeName ?? string.Empty,
                rendererDataName = rendererDataName ?? string.Empty,
                volumeProfileAssetCount = Math.Max(0, volumeProfileAssetCount),
                volumeProfileGuidFingerprint = volumeProfileGuidFingerprint ?? string.Empty,
                volumeProfileNameFingerprint = volumeProfileNameFingerprint ?? string.Empty,
                shaderAssetCount = Math.Max(0, shaderAssetCount),
                keywordSpaceShaderCount = Math.Max(0, keywordSpaceShaderCount),
                shaderGuidFingerprint = shaderGuidFingerprint ?? string.Empty,
                keywordFingerprint = keywordFingerprint ?? string.Empty,
                backendStateVerified = receipt.IsVerified
                    || receipt.Status == ESRenderBackendReceiptStatus.RolledBack,
                runtimeAcceptance = false
            };
        }
    }
}
