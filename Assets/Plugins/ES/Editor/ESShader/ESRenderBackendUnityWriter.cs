using System;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Unity Editor 质量档 Writer。只有已通过的 ApplyGate 和匹配幂等键才能进入。
    /// 本类型不自行取得授权；调用方必须先构造 ESRenderBackendApplyGate。
    /// </summary>
    public static class ESRenderBackendUnityWriter
    {
        public static bool TryPreview(
            ESRenderQualityPolicy targetPolicy,
            out ESRenderBackendSnapshot current,
            out ESRenderBackendDiff diff,
            out string reason)
        {
            if (!ESRenderBackendSnapshot.TryCapture(out current, out reason))
            {
                diff = default(ESRenderBackendDiff);
                return false;
            }

            diff = ESRenderBackendDiff.Evaluate(current, targetPolicy);
            reason = diff.HasDifferences ? "dry-run-differences-present" : "already-at-target";
            return true;
        }

        public static bool TryApply(
            ESRenderBackendChangePlan plan,
            ESRenderBackendApplyGate gate,
            string idempotencyKey,
            out ESRenderBackendReceipt receipt,
            out string reason)
        {
            return TryApply(plan, gate, idempotencyKey, null, out receipt, out reason);
        }

        /// <summary>
        /// 使用宿主注入的真实灯光目标执行完整 Apply；目标必须同时支持 Apply 与 Capture。
        /// </summary>
        public static bool TryApply(
            ESRenderBackendChangePlan plan,
            ESRenderBackendApplyGate gate,
            string idempotencyKey,
            IESRenderLightingTarget lightingTarget,
            out ESRenderBackendReceipt receipt,
            out string reason)
        {
            receipt = default(ESRenderBackendReceipt);
            if (!plan.IsDryRun)
            {
                reason = "dry-run-plan-required-for-unity-writer";
                return false;
            }
            if (!gate.MatchesIdempotencyKey(idempotencyKey))
            {
                reason = "apply-gate-idempotency-key-mismatch";
                return false;
            }
            if (!gate.MatchesPlan(plan))
            {
                reason = "apply-gate-plan-mismatch";
                return false;
            }
            if (plan.TargetLighting.HasValue && lightingTarget == null)
            {
                reason = "lighting-target-requires-lighting-writer";
                return false;
            }
            ESRenderBackendSnapshot current;
            bool captured;
            if (lightingTarget == null || !plan.TargetLighting.HasValue)
                captured = ESRenderBackendSnapshot.TryCapture(out current, out reason);
            else
                captured = ESRenderBackendSnapshot.TryCapture(lightingTarget, out current, out reason);
            if (!captured
                || !SameSnapshot(plan.Before, current))
            {
                reason = "backend-snapshot-drifted-before-unity-write";
                return false;
            }

            ESRenderBackendApplySession session;
            if (!ESRenderBackendApplySession.TryCreate(
                plan, gate, idempotencyKey, out session, out reason))
                return false;

            string lightingApplyReason = string.Empty;
            receipt = session.Execute(
                () =>
                {
                    if (!TrySetQuality(plan.TargetPolicy.Profile))
                        return false;
                    if (!plan.TargetLighting.HasValue
                        || lightingTarget.TryApply(plan.TargetLighting.Value, out lightingApplyReason))
                        return true;

                    // Compensate a partial transaction: never leave quality changed
                    // when the injected lighting target rejects the same operation.
                    TrySetQualityIndex(plan.Before.QualityIndex);
                    if (plan.Before.LightingRecipe.HasValue)
                        lightingTarget.TryApply(plan.Before.LightingRecipe.Value, out lightingApplyReason);
                    return false;
                },
                () => CaptureOrThrow(plan.TargetLighting.HasValue ? lightingTarget : null));
            reason = receipt.Reason;
            return receipt.Status == ESRenderBackendReceiptStatus.Verified;
        }

        public static bool TryRollback(
            ESRenderBackendChangePlan plan,
            ESRenderBackendApplyGate gate,
            string idempotencyKey,
            out ESRenderBackendReceipt receipt,
            out string reason)
        {
            return TryRollback(plan, gate, idempotencyKey, null, out receipt, out reason);
        }

        public static bool TryRollback(
            ESRenderBackendChangePlan plan,
            ESRenderBackendApplyGate gate,
            string idempotencyKey,
            IESRenderLightingTarget lightingTarget,
            out ESRenderBackendReceipt receipt,
            out string reason)
        {
            receipt = default(ESRenderBackendReceipt);
            if (!plan.HasRollbackBaseline)
            {
                reason = "rollback-baseline-missing";
                return false;
            }
            if (!gate.MatchesIdempotencyKey(idempotencyKey))
            {
                reason = "rollback-gate-idempotency-key-mismatch";
                return false;
            }
            if (!gate.MatchesPlan(plan))
            {
                reason = "rollback-gate-plan-mismatch";
                return false;
            }
            if (plan.TargetLighting.HasValue && lightingTarget == null)
            {
                reason = "lighting-target-requires-lighting-writer";
                return false;
            }

            try
            {
                ESRenderBackendSnapshot current;
                bool captured = lightingTarget == null
                    ? ESRenderBackendSnapshot.TryCapture(out current, out reason)
                    : ESRenderBackendSnapshot.TryCapture(lightingTarget, out current, out reason);
                if (!captured)
                {
                    reason = "backend-snapshot-capture-failed-before-rollback";
                    return false;
                }
                if (!ESRenderBackendReceipt.EvaluateApply(plan, current, true).IsVerified)
                {
                    reason = "backend-snapshot-is-not-the-applied-target";
                    return false;
                }
                if (lightingTarget != null
                    && plan.Before.LightingRecipe.HasValue
                    && !lightingTarget.TryApply(plan.Before.LightingRecipe.Value, out reason))
                {
                    receipt = ESRenderBackendReceipt.CreateFailure(reason);
                    return false;
                }
                if (!TrySetQualityIndex(plan.Before.QualityIndex))
                {
                    receipt = ESRenderBackendReceipt.CreateFailure("unity-rollback-quality-write-failed");
                    return false;
                }
                receipt = ESRenderBackendReceipt.EvaluateRollback(
                    plan, CaptureOrThrow(plan.TargetLighting.HasValue ? lightingTarget : null), true);
            }
            catch (Exception exception)
            {
                receipt = ESRenderBackendReceipt.CreateFailure(
                    "unity-rollback-threw-" + exception.GetType().Name);
            }

            reason = receipt.Reason;
            return receipt.Status == ESRenderBackendReceiptStatus.RolledBack;
        }

        private static bool TrySetQuality(ESRenderQualityProfileId profile)
        {
            string expectedName = ExpectedQualityName(profile);
            string[] names = QualitySettings.names;
            if (names == null)
                return false;
            for (int index = 0; index < names.Length; index++)
            {
                if (string.Equals(names[index], expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return TrySetQualityIndex(index);
                }
            }
            return false;
        }

        private static bool TrySetQualityIndex(int qualityIndex)
        {
            string[] names = QualitySettings.names;
            if (names == null || qualityIndex < 0 || qualityIndex >= names.Length)
                return false;
            QualitySettings.SetQualityLevel(qualityIndex, true);
            return QualitySettings.GetQualityLevel() == qualityIndex;
        }

        private static ESRenderBackendSnapshot CaptureOrThrow(IESRenderLightingTarget lightingTarget = null)
        {
            ESRenderBackendSnapshot snapshot;
            string reason;
            bool captured;
            if (lightingTarget == null)
                captured = ESRenderBackendSnapshot.TryCapture(out snapshot, out reason);
            else
                captured = ESRenderBackendSnapshot.TryCapture(lightingTarget, out snapshot, out reason);
            if (captured)
                return snapshot;
            throw new InvalidOperationException(reason);
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
