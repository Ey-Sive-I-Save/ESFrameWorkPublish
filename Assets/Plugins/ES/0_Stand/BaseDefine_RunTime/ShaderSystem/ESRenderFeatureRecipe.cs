using System;

namespace ES
{
    /// <summary>
    /// 可组合的 URP Renderer/Volume 策略片段。它描述意图，不直接操作 RendererData 或 VolumeProfile。
    /// </summary>
    public readonly struct ESRenderFeatureRecipe
    {
        public ESRenderFeatureRecipe(
            ESRenderVisualStyleId style,
            bool enableSsao,
            bool enableBloom,
            bool enableDecals,
            bool enableScreenSpaceShadows,
            bool allowVolumetrics,
            int shadowCascades,
            int featureBudget)
        {
            Style = style;
            EnableSsao = enableSsao;
            EnableBloom = enableBloom;
            EnableDecals = enableDecals;
            EnableScreenSpaceShadows = enableScreenSpaceShadows;
            AllowVolumetrics = allowVolumetrics;
            ShadowCascades = Math.Max(0, Math.Min(4, shadowCascades));
            FeatureBudget = Math.Max(0, featureBudget);
        }

        public ESRenderVisualStyleId Style { get; }
        public bool EnableSsao { get; }
        public bool EnableBloom { get; }
        public bool EnableDecals { get; }
        public bool EnableScreenSpaceShadows { get; }
        public bool AllowVolumetrics { get; }
        public int ShadowCascades { get; }
        public int FeatureBudget { get; }

        public bool IsValid(out string reason)
        {
            if (!Enum.IsDefined(typeof(ESRenderVisualStyleId), Style))
            {
                reason = "recipe-style-unknown";
                return false;
            }

            if (!EnableSsao && EnableScreenSpaceShadows)
            {
                reason = "screen-space-shadows-require-ssao-budget";
                return false;
            }

            if (Style == ESRenderVisualStyleId.MobileFlat && (AllowVolumetrics || EnableDecals))
            {
                reason = "mobile-flat-disallows-heavy-features";
                return false;
            }

            if (Style == ESRenderVisualStyleId.NeonSciFi && !EnableBloom)
            {
                reason = "neon-style-requires-bloom";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static ESRenderFeatureRecipe Resolve(ESRenderVisualStyleId style)
        {
            switch (style)
            {
                case ESRenderVisualStyleId.NaturalPbr:
                    return new ESRenderFeatureRecipe(style, true, true, true, true, false, 2, 4);
                case ESRenderVisualStyleId.StylizedToon:
                    return new ESRenderFeatureRecipe(style, true, false, false, false, false, 1, 2);
                case ESRenderVisualStyleId.NoirContrast:
                    return new ESRenderFeatureRecipe(style, true, false, true, true, false, 2, 3);
                case ESRenderVisualStyleId.NeonSciFi:
                    return new ESRenderFeatureRecipe(style, true, true, true, true, true, 2, 5);
                case ESRenderVisualStyleId.FantasyAtmosphere:
                    return new ESRenderFeatureRecipe(style, true, true, true, true, true, 4, 6);
                case ESRenderVisualStyleId.MobileFlat:
                    return new ESRenderFeatureRecipe(style, false, false, false, false, false, 0, 1);
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown ES render style for feature recipe.");
            }
        }
    }
}
