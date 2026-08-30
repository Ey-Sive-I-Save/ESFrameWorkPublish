using System;

namespace ES
{
    public enum ESRenderBackendReceiptStatus
    {
        NotRun = 0,
        AppliedUnverified = 1,
        Verified = 2,
        Failed = 3,
        RollbackRequired = 4,
        RolledBack = 5
    }

    /// <summary>
    /// Apply 后的结果判定。调用方必须提供真实的变更后快照；本类型不执行 Unity 写入或回滚。
    /// </summary>
    public readonly struct ESRenderBackendReceipt
    {
        private ESRenderBackendReceipt(ESRenderBackendReceiptStatus status, string reason)
        {
            Status = status;
            Reason = reason ?? string.Empty;
        }

        public ESRenderBackendReceiptStatus Status { get; }
        public string Reason { get; }
        public bool IsVerified => Status == ESRenderBackendReceiptStatus.Verified;

        public static ESRenderBackendReceipt CreateFailure(string reason)
        {
            return new ESRenderBackendReceipt(ESRenderBackendReceiptStatus.Failed, reason);
        }

        public static ESRenderBackendReceipt EvaluateApply(
            ESRenderBackendChangePlan plan,
            ESRenderBackendSnapshot after,
            bool writerReportedSuccess)
        {
            if (!plan.IsDryRun)
                return new ESRenderBackendReceipt(
                    ESRenderBackendReceiptStatus.Failed,
                    "dry-run-plan-required-for-apply-receipt");

            if (!writerReportedSuccess)
                return new ESRenderBackendReceipt(
                    ESRenderBackendReceiptStatus.Failed,
                    "unity-writer-reported-failure");

            string expectedQualityName = ExpectedQualityName(plan.TargetPolicy.Profile);
            if (!string.Equals(after.QualityName, expectedQualityName, StringComparison.OrdinalIgnoreCase))
                return new ESRenderBackendReceipt(
                    ESRenderBackendReceiptStatus.RollbackRequired,
                    "post-apply-quality-state-does-not-match-target");

            if (plan.TargetLighting.HasValue)
            {
                if (!after.LightingRecipe.HasValue)
                    return new ESRenderBackendReceipt(
                        ESRenderBackendReceiptStatus.RollbackRequired,
                        "post-apply-lighting-state-missing");
                if (!SameLighting(plan.TargetLighting.Value, after.LightingRecipe.Value))
                    return new ESRenderBackendReceipt(
                        ESRenderBackendReceiptStatus.RollbackRequired,
                        "post-apply-lighting-state-does-not-match-target");
            }

            if (string.IsNullOrEmpty(after.PipelineName))
                return new ESRenderBackendReceipt(
                    ESRenderBackendReceiptStatus.RollbackRequired,
                    "post-apply-render-pipeline-is-missing");

            return new ESRenderBackendReceipt(
                ESRenderBackendReceiptStatus.Verified,
                "post-apply-snapshot-matches-target-quality");
        }

        /// <summary>
        /// 只有真实恢复后快照与 Apply 前基线逐字段一致时，才确认回滚完成。
        /// </summary>
        public static ESRenderBackendReceipt EvaluateRollback(
            ESRenderBackendChangePlan plan,
            ESRenderBackendSnapshot restored,
            bool writerReportedSuccess)
        {
            if (!plan.HasRollbackBaseline)
                return new ESRenderBackendReceipt(
                    ESRenderBackendReceiptStatus.Failed,
                    "rollback-baseline-missing");
            if (!writerReportedSuccess)
                return new ESRenderBackendReceipt(
                    ESRenderBackendReceiptStatus.Failed,
                    "unity-writer-reported-rollback-failure");
            if (!SameSnapshot(plan.Before, restored))
                return new ESRenderBackendReceipt(
                    ESRenderBackendReceiptStatus.RollbackRequired,
                    "post-rollback-snapshot-does-not-match-baseline");

            return new ESRenderBackendReceipt(
                ESRenderBackendReceiptStatus.RolledBack,
                "post-rollback-snapshot-matches-baseline");
        }

        private static bool SameSnapshot(
            ESRenderBackendSnapshot left,
            ESRenderBackendSnapshot right)
        {
            return left.QualityIndex == right.QualityIndex
                && string.Equals(left.QualityName, right.QualityName, StringComparison.Ordinal)
                && string.Equals(left.PipelineName, right.PipelineName, StringComparison.Ordinal)
                && left.SrpBatcherEnabled == right.SrpBatcherEnabled
                && string.Equals(left.QualityNamesFingerprint, right.QualityNamesFingerprint, StringComparison.Ordinal)
                && SameOptionalLighting(left.LightingRecipe, right.LightingRecipe);
        }

        private static bool SameOptionalLighting(
            ESRenderLightingRecipe? left,
            ESRenderLightingRecipe? right)
        {
            if (left.HasValue != right.HasValue)
                return false;
            return !left.HasValue || SameLighting(left.Value, right.Value);
        }

        private static bool SameLighting(
            ESRenderLightingRecipe left,
            ESRenderLightingRecipe right)
        {
            return left.Style == right.Style
                && left.ShadowMode == right.ShadowMode
                && left.AdditionalLightsPerObject == right.AdditionalLightsPerObject
                && left.ShadowDistance == right.ShadowDistance
                && left.CascadeCount == right.CascadeCount
                && left.SoftShadows == right.SoftShadows
                && left.ReflectionProbes == right.ReflectionProbes
                && left.MainLightIntensity == right.MainLightIntensity
                && left.ShadowStrength == right.ShadowStrength
                && left.ShadowBias == right.ShadowBias
                && left.ShadowNormalBias == right.ShadowNormalBias
                && left.ContactShadows == right.ContactShadows
                && left.AmbientIntensity == right.AmbientIntensity
                && left.UseColorTemperature == right.UseColorTemperature
                && left.MainLightTemperatureKelvin == right.MainLightTemperatureKelvin
                && left.AmbientColor.Red == right.AmbientColor.Red
                && left.AmbientColor.Green == right.AmbientColor.Green
                && left.AmbientColor.Blue == right.AmbientColor.Blue;
        }

        private static string ExpectedQualityName(ESRenderQualityProfileId profile)
        {
            switch (profile)
            {
                case ESRenderQualityProfileId.HighFidelity:
                case ESRenderQualityProfileId.CinematicShowcase:
                    return "High Fidelity";
                case ESRenderQualityProfileId.Balanced:
                case ESRenderQualityProfileId.CombatReadability:
                    return "Balanced";
                case ESRenderQualityProfileId.MobileStable:
                case ESRenderQualityProfileId.Performant:
                    return "Performant";
                default:
                    return string.Empty;
            }
        }
    }
}
