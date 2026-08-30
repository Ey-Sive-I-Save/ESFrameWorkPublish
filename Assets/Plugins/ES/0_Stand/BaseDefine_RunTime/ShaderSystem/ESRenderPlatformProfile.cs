using System;

namespace ES
{
    public enum ESRenderPlatformId
    {
        Desktop = 0,
        Console = 1,
        Mobile = 2,
        WebGl = 3
    }

    /// <summary>
    /// 平台能力约束。只描述可验证的上限与预算，不读取 Application/SystemInfo，也不执行降级。
    /// </summary>
    public readonly struct ESRenderPlatformProfile
    {
        public ESRenderPlatformProfile(
            ESRenderPlatformId platform,
            ESRenderQualityProfileId maximumQuality,
            float featureBudgetScale,
            float shaderVariantBudgetScale,
            bool dynamicResolutionAllowed)
        {
            Platform = platform;
            MaximumQuality = maximumQuality;
            FeatureBudgetScale = ClampFinite(featureBudgetScale, 0.25f, 2f, 1f);
            ShaderVariantBudgetScale = ClampFinite(shaderVariantBudgetScale, 0.25f, 2f, 1f);
            DynamicResolutionAllowed = dynamicResolutionAllowed;
        }

        public ESRenderPlatformId Platform { get; }
        public ESRenderQualityProfileId MaximumQuality { get; }
        public float FeatureBudgetScale { get; }
        public float ShaderVariantBudgetScale { get; }
        public bool DynamicResolutionAllowed { get; }

        public bool Allows(ESRenderQualityProfileId requested)
        {
            return QualityRank(requested) <= QualityRank(MaximumQuality);
        }

        public ESRenderQualityProfileId ClampQuality(ESRenderQualityProfileId requested)
        {
            if (Allows(requested))
                return requested;

            switch (QualityRank(MaximumQuality))
            {
                case 0: return MaximumQuality;
                case 1: return ESRenderQualityProfileId.Balanced;
                default: return ESRenderQualityProfileId.HighFidelity;
            }
        }

        public static ESRenderPlatformProfile Resolve(ESRenderPlatformId platform)
        {
            switch (platform)
            {
                case ESRenderPlatformId.Desktop:
                    return new ESRenderPlatformProfile(platform, ESRenderQualityProfileId.HighFidelity, 1f, 1f, true);
                case ESRenderPlatformId.Console:
                    return new ESRenderPlatformProfile(platform, ESRenderQualityProfileId.HighFidelity, 0.9f, 0.9f, true);
                case ESRenderPlatformId.Mobile:
                    return new ESRenderPlatformProfile(platform, ESRenderQualityProfileId.MobileStable, 0.5f, 0.5f, true);
                case ESRenderPlatformId.WebGl:
                    return new ESRenderPlatformProfile(platform, ESRenderQualityProfileId.Balanced, 0.65f, 0.6f, false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown ES render platform.");
            }
        }

        private static int QualityRank(ESRenderQualityProfileId profile)
        {
            switch (profile)
            {
                case ESRenderQualityProfileId.Performant:
                case ESRenderQualityProfileId.MobileStable:
                    return 0;
                case ESRenderQualityProfileId.Balanced:
                case ESRenderQualityProfileId.CombatReadability:
                    return 1;
                case ESRenderQualityProfileId.HighFidelity:
                case ESRenderQualityProfileId.CinematicShowcase:
                    return 2;
                default:
                    return -1;
            }
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return fallback;
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
