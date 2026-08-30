using System;

namespace ES
{
    public enum ESRenderBackendApplyGateStatus
    {
        Denied = 0,
        Ready = 1
    }

    /// <summary>
    /// Unity Writer 前的最小授权与漂移门禁。
    /// 门禁只产生可重放判定，不执行 Apply、Rollback 或任何 Unity 状态写入。
    /// </summary>
    public readonly struct ESRenderBackendApplyGate
    {
        private ESRenderBackendApplyGate(
            ESRenderBackendApplyGateStatus status,
            string reason,
            string idempotencyKey,
            ESRenderBackendSnapshot baseline,
            ESRenderQualityProfileId targetProfile)
        {
            Status = status;
            Reason = reason ?? string.Empty;
            IdempotencyKey = idempotencyKey ?? string.Empty;
            Baseline = baseline;
            TargetProfile = targetProfile;
        }

        public ESRenderBackendApplyGateStatus Status { get; }
        public string Reason { get; }
        public string IdempotencyKey { get; }
        public ESRenderBackendSnapshot Baseline { get; }
        public ESRenderQualityProfileId TargetProfile { get; }
        public bool IsReady => Status == ESRenderBackendApplyGateStatus.Ready;

        public bool MatchesIdempotencyKey(string idempotencyKey)
        {
            return IsReady
                && !string.IsNullOrWhiteSpace(idempotencyKey)
                && string.Equals(IdempotencyKey, idempotencyKey, StringComparison.Ordinal);
        }

        public bool MatchesPlan(ESRenderBackendChangePlan plan)
        {
            return IsReady
                && TargetProfile == plan.TargetPolicy.Profile
                && SameSnapshot(Baseline, plan.Before);
        }

        public static bool TryAuthorize(
            ESRenderBackendChangePlan plan,
            ESRenderBackendSnapshot observed,
            bool userDirected,
            string idempotencyKey,
            out ESRenderBackendApplyGate gate,
            out string reason)
        {
            if (!userDirected)
                return Deny("user-directed-apply-required", out gate, out reason);
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Deny("non-empty-idempotency-key-required", out gate, out reason);
            if (!plan.IsDryRun)
                return Deny("dry-run-plan-required", out gate, out reason);
            if (!SameSnapshot(plan.Before, observed))
                return Deny("observed-backend-snapshot-drifted", out gate, out reason);

            gate = new ESRenderBackendApplyGate(
                ESRenderBackendApplyGateStatus.Ready,
                "ready-for-separately-authorized-unity-writer",
                idempotencyKey,
                plan.Before,
                plan.TargetPolicy.Profile);
            reason = string.Empty;
            return true;
        }

        private static bool Deny(
            string denial,
            out ESRenderBackendApplyGate gate,
            out string reason)
        {
            gate = new ESRenderBackendApplyGate(
                ESRenderBackendApplyGateStatus.Denied,
                denial,
                string.Empty,
                default(ESRenderBackendSnapshot),
                default(ESRenderQualityProfileId));
            reason = denial;
            return false;
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
            if (!left.HasValue)
                return true;
            ESRenderLightingRecipe a = left.Value;
            ESRenderLightingRecipe b = right.Value;
            return a.Style == b.Style
                && a.ShadowMode == b.ShadowMode
                && a.AdditionalLightsPerObject == b.AdditionalLightsPerObject
                && a.ShadowDistance == b.ShadowDistance
                && a.CascadeCount == b.CascadeCount
                && a.SoftShadows == b.SoftShadows
                && a.ReflectionProbes == b.ReflectionProbes
                && a.MainLightIntensity == b.MainLightIntensity
                && a.ShadowStrength == b.ShadowStrength
                && a.ShadowBias == b.ShadowBias
                && a.ShadowNormalBias == b.ShadowNormalBias
                && a.ContactShadows == b.ContactShadows
                && a.AmbientIntensity == b.AmbientIntensity
                && a.UseColorTemperature == b.UseColorTemperature
                && a.MainLightTemperatureKelvin == b.MainLightTemperatureKelvin
                && a.AmbientColor.Red == b.AmbientColor.Red
                && a.AmbientColor.Green == b.AmbientColor.Green
                && a.AmbientColor.Blue == b.AmbientColor.Blue;
        }
    }
}
