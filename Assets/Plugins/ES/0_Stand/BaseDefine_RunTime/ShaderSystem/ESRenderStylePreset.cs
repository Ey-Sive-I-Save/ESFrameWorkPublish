using System;

namespace ES
{
    /// <summary>
    /// ES 的可复用视觉风格模板。它只描述可投影的数值与质量意图，
    /// 不依赖 MonoBehaviour、Renderer 或 Unity Editor；真正的 URP 写入仍由受管后端负责。
    /// </summary>
    public enum ESRenderVisualStyleId
    {
        NaturalPbr = 0,
        StylizedToon = 1,
        NoirContrast = 2,
        NeonSciFi = 3,
        FantasyAtmosphere = 4,
        MobileFlat = 5,
        RetroPixel = 6,
        HorrorGrit = 7,
        CozyPastel = 8,
        TacticalRealism = 9
    }

    public readonly struct ESRenderStylePreset
    {
        public ESRenderStylePreset(
            ESRenderVisualStyleId style,
            ESRenderQualityProfileId qualityProfile,
            float saturation,
            float contrast,
            float exposure,
            float bloomIntensity,
            float shadowSoftness,
            bool preserveSilhouette)
        {
            Style = style;
            QualityProfile = qualityProfile;
            Saturation = ClampFinite(saturation, 0f, 2f, 1f);
            Contrast = ClampFinite(contrast, 0.5f, 2f, 1f);
            Exposure = ClampFinite(exposure, -2f, 2f, 0f);
            BloomIntensity = ClampFinite(bloomIntensity, 0f, 2f, 0f);
            ShadowSoftness = ClampFinite(shadowSoftness, 0f, 1f, 0.5f);
            PreserveSilhouette = preserveSilhouette;
        }

        public ESRenderVisualStyleId Style { get; }
        public ESRenderQualityProfileId QualityProfile { get; }
        public float Saturation { get; }
        public float Contrast { get; }
        public float Exposure { get; }
        public float BloomIntensity { get; }
        public float ShadowSoftness { get; }
        public bool PreserveSilhouette { get; }

        public bool IsValid(out string reason)
        {
            if (!Enum.IsDefined(typeof(ESRenderVisualStyleId), Style))
            {
                reason = "style-unknown";
                return false;
            }

            ESRenderQualityPolicy policy = ESRenderQualityPolicy.Resolve(QualityProfile);
            if (!policy.IsValid(out reason))
                return false;

            if (PreserveSilhouette && Contrast < 0.75f)
            {
                reason = "silhouette-contrast-too-low";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static ESRenderStylePreset Resolve(ESRenderVisualStyleId style)
        {
            switch (style)
            {
                case ESRenderVisualStyleId.NaturalPbr:
                    return new ESRenderStylePreset(style, ESRenderQualityProfileId.Balanced, 1.0f, 1.0f, 0f, 0.15f, 0.55f, true);
                case ESRenderVisualStyleId.StylizedToon:
                    return new ESRenderStylePreset(style, ESRenderQualityProfileId.CombatReadability, 1.15f, 1.1f, 0.05f, 0.1f, 0.35f, true);
                case ESRenderVisualStyleId.NoirContrast:
                    return new ESRenderStylePreset(style, ESRenderQualityProfileId.HighFidelity, 0.2f, 1.35f, -0.1f, 0.05f, 0.2f, true);
                case ESRenderVisualStyleId.NeonSciFi:
                    return new ESRenderStylePreset(style, ESRenderQualityProfileId.HighFidelity, 1.35f, 1.15f, 0.1f, 0.65f, 0.45f, true);
                case ESRenderVisualStyleId.FantasyAtmosphere:
                    return new ESRenderStylePreset(style, ESRenderQualityProfileId.CinematicShowcase, 1.2f, 1.05f, 0.15f, 0.4f, 0.65f, true);
                case ESRenderVisualStyleId.MobileFlat:
                    return new ESRenderStylePreset(style, ESRenderQualityProfileId.MobileStable, 1.05f, 1.0f, 0f, 0f, 0.5f, true);
                case ESRenderVisualStyleId.RetroPixel:
                    return new ESRenderStylePreset(style, ESRenderQualityProfileId.Performant, 1.1f, 1.2f, 0f, 0f, 0.25f, true);
                case ESRenderVisualStyleId.HorrorGrit:
                    return new ESRenderStylePreset(style, ESRenderQualityProfileId.HighFidelity, 0.65f, 1.3f, -0.25f, 0.08f, 0.15f, true);
                case ESRenderVisualStyleId.CozyPastel:
                    return new ESRenderStylePreset(style, ESRenderQualityProfileId.Balanced, 1.25f, 0.9f, 0.2f, 0.2f, 0.7f, true);
                case ESRenderVisualStyleId.TacticalRealism:
                    return new ESRenderStylePreset(style, ESRenderQualityProfileId.CombatReadability, 0.9f, 1.15f, -0.05f, 0.05f, 0.4f, true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown ES render visual style.");
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
