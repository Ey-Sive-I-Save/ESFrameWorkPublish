using System;

namespace ES
{
    public enum ESRenderBackendPlanStatus
    {
        Invalid = 0,
        NoChange = 1,
        DryRunReady = 2
    }

    /// <summary>
    /// ES 渲染后端的不可变 dry-run 计划。
    /// 当前阶段只生成计划和恢复基线，不写入 Unity；真正 Apply/Rollback 必须由受控后端适配器完成。
    /// </summary>
    public readonly struct ESRenderBackendChangePlan
    {
        private ESRenderBackendChangePlan(
            ESRenderBackendPlanStatus status,
            ESRenderBackendSnapshot before,
            ESRenderQualityPolicy targetPolicy,
            ESRenderLightingRecipe? targetLighting,
            string reason)
        {
            Status = status;
            Before = before;
            TargetPolicy = targetPolicy;
            TargetLighting = targetLighting;
            Reason = reason ?? string.Empty;
        }

        public ESRenderBackendPlanStatus Status { get; }
        public ESRenderBackendSnapshot Before { get; }
        public ESRenderQualityPolicy TargetPolicy { get; }
        public ESRenderLightingRecipe? TargetLighting { get; }
        public string Reason { get; }
        public bool IsDryRun => Status == ESRenderBackendPlanStatus.DryRunReady;
        public bool RequiresUnityWriter => IsDryRun;
        public bool HasRollbackBaseline => IsDryRun;

        public static bool TryCreateDryRun(
            ESRenderBackendSnapshot before,
            ESRenderQualityPolicy targetPolicy,
            out ESRenderBackendChangePlan plan,
            out string reason)
        {
            if (!targetPolicy.IsValid(out reason))
            {
                plan = new ESRenderBackendChangePlan(
                    ESRenderBackendPlanStatus.Invalid,
                    before,
                    targetPolicy,
                    null,
                    reason);
                return false;
            }

            if (string.Equals(
                before.QualityName,
                ExpectedQualityName(targetPolicy.Profile),
                StringComparison.OrdinalIgnoreCase))
            {
                plan = new ESRenderBackendChangePlan(
                    ESRenderBackendPlanStatus.NoChange,
                    before,
                    targetPolicy,
                    null,
                    string.Empty);
                reason = string.Empty;
                return true;
            }

            plan = new ESRenderBackendChangePlan(
                ESRenderBackendPlanStatus.DryRunReady,
                before,
                targetPolicy,
                null,
                "dry-run-only; unity-writer-required-for-apply-and-rollback");
            reason = string.Empty;
            return true;
        }

        public static bool TryCreateDryRun(
            ESRenderBackendSnapshot before,
            ESRenderQualityPolicy targetPolicy,
            ESRenderLightingRecipe targetLighting,
            out ESRenderBackendChangePlan plan,
            out string reason)
        {
            if (!targetPolicy.IsValid(out reason))
            {
                plan = new ESRenderBackendChangePlan(ESRenderBackendPlanStatus.Invalid, before, targetPolicy, targetLighting, reason);
                return false;
            }
            if (!targetLighting.IsValid(out reason))
            {
                plan = new ESRenderBackendChangePlan(ESRenderBackendPlanStatus.Invalid, before, targetPolicy, targetLighting, reason);
                return false;
            }
            if (!before.LightingRecipe.HasValue)
            {
                plan = new ESRenderBackendChangePlan(
                    ESRenderBackendPlanStatus.Invalid,
                    before,
                    targetPolicy,
                    targetLighting,
                    "lighting-baseline-required");
                reason = "lighting-baseline-required";
                return false;
            }
            plan = new ESRenderBackendChangePlan(
                ESRenderBackendPlanStatus.DryRunReady,
                before,
                targetPolicy,
                targetLighting,
                "dry-run-only; quality-and-lighting-target-validated");
            reason = string.Empty;
            return true;
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
