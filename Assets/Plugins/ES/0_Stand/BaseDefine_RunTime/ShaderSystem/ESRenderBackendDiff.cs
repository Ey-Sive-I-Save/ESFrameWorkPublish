using System;

namespace ES
{
    [Flags]
    public enum ESRenderBackendDifference
    {
        None = 0,
        QualityName = 1 << 0,
        PipelineMissing = 1 << 1,
        SrpBatcherDisabled = 1 << 2,
        TargetProfileUnmapped = 1 << 3,
        LightingStateMissing = 1 << 4,
        LightingTargetMismatch = 1 << 5
    }

    /// <summary>
    /// 对当前 Unity 后端快照与 ES 目标策略进行只读差异评估。
    /// 差异结果用于 dry-run 展示，不代表可以自动写入或证明性能结果。
    /// </summary>
    public readonly struct ESRenderBackendDiff
    {
        public ESRenderBackendDiff(ESRenderBackendDifference differences)
        {
            Differences = differences;
        }

        public ESRenderBackendDifference Differences { get; }
        public bool HasDifferences => Differences != ESRenderBackendDifference.None;
        public bool RequiresUnityWriter => HasDifferences;

        public static ESRenderBackendDiff Evaluate(
            ESRenderBackendSnapshot snapshot,
            ESRenderQualityPolicy targetPolicy)
        {
            ESRenderBackendDifference differences = ESRenderBackendDifference.None;
            string profileName = ExpectedQualityName(targetPolicy.Profile);
            if (!string.Equals(snapshot.QualityName, profileName, StringComparison.OrdinalIgnoreCase))
                differences |= ESRenderBackendDifference.QualityName;
            if (string.IsNullOrEmpty(snapshot.PipelineName))
                differences |= ESRenderBackendDifference.PipelineMissing;
            if (targetPolicy.Profile == ESRenderQualityProfileId.HighFidelity
                && !snapshot.IsUrpLikePipeline)
                differences |= ESRenderBackendDifference.TargetProfileUnmapped;
            return new ESRenderBackendDiff(differences);
        }

        public static ESRenderBackendDiff Evaluate(
            ESRenderBackendSnapshot snapshot,
            ESRenderQualityPolicy targetPolicy,
            ESRenderLightingRecipe targetLighting)
        {
            ESRenderBackendDifference differences = Evaluate(snapshot, targetPolicy).Differences;
            if (!snapshot.LightingRecipe.HasValue)
                differences |= ESRenderBackendDifference.LightingStateMissing;
            else if (!SameLighting(snapshot.LightingRecipe.Value, targetLighting))
                differences |= ESRenderBackendDifference.LightingTargetMismatch;
            return new ESRenderBackendDiff(differences);
        }

        private static bool SameLighting(ESRenderLightingRecipe left, ESRenderLightingRecipe right)
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
