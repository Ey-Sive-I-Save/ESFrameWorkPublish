using System;

namespace ES
{
    /// <summary>
    /// 透明、粒子、Decal、后处理与 Shader Variant 的纯数据预算配方。
    /// </summary>
    public readonly struct ESRenderEffectsRecipe
    {
        public ESRenderEffectsRecipe(
            ESRenderVisualStyleId style,
            int transparentBudget,
            int particleBudget,
            int decalBudget,
            bool bloom,
            bool toneMapping,
            bool colorAdjustments,
            bool depthOfField,
            bool vignette,
            int shaderVariantBudget)
        {
            Style = style;
            TransparentBudget = Math.Max(0, transparentBudget);
            ParticleBudget = Math.Max(0, particleBudget);
            DecalBudget = Math.Max(0, decalBudget);
            Bloom = bloom;
            ToneMapping = toneMapping;
            ColorAdjustments = colorAdjustments;
            DepthOfField = depthOfField;
            Vignette = vignette;
            ShaderVariantBudget = Math.Max(0, shaderVariantBudget);
        }

        public ESRenderVisualStyleId Style { get; }
        public int TransparentBudget { get; }
        public int ParticleBudget { get; }
        public int DecalBudget { get; }
        public bool Bloom { get; }
        public bool ToneMapping { get; }
        public bool ColorAdjustments { get; }
        public bool DepthOfField { get; }
        public bool Vignette { get; }
        public int ShaderVariantBudget { get; }

        public ESRenderEffectsRecipe WithBudgetScale(float transparentScale, float particleScale)
        {
            float safeTransparent = ClampScale(transparentScale);
            float safeParticle = ClampScale(particleScale);
            return new ESRenderEffectsRecipe(
                Style,
                (int)Math.Round(TransparentBudget * safeTransparent),
                (int)Math.Round(ParticleBudget * safeParticle),
                DecalBudget,
                Bloom,
                ToneMapping,
                ColorAdjustments,
                DepthOfField,
                Vignette,
                ShaderVariantBudget);
        }

        public bool IsValid(out string reason)
        {
            if (!Enum.IsDefined(typeof(ESRenderVisualStyleId), Style))
            {
                reason = "effects-style-unknown";
                return false;
            }

            if (ParticleBudget > TransparentBudget * 4)
            {
                reason = "particle-budget-exceeds-transparent-capacity";
                return false;
            }

            if (Style == ESRenderVisualStyleId.MobileFlat &&
                (DecalBudget > 0 || DepthOfField || Bloom || ShaderVariantBudget > 8))
            {
                reason = "mobile-flat-effects-too-expensive";
                return false;
            }

            if (Style == ESRenderVisualStyleId.NeonSciFi && !Bloom)
            {
                reason = "neon-effects-require-bloom";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static ESRenderEffectsRecipe Resolve(ESRenderVisualStyleId style)
        {
            switch (style)
            {
                case ESRenderVisualStyleId.NaturalPbr:
                    return new ESRenderEffectsRecipe(style, 48, 128, 32, true, true, true, false, true, 16);
                case ESRenderVisualStyleId.StylizedToon:
                    return new ESRenderEffectsRecipe(style, 32, 96, 8, false, true, true, false, false, 16);
                case ESRenderVisualStyleId.NoirContrast:
                    return new ESRenderEffectsRecipe(style, 48, 96, 24, false, true, true, true, true, 24);
                case ESRenderVisualStyleId.NeonSciFi:
                    return new ESRenderEffectsRecipe(style, 64, 192, 48, true, true, true, false, true, 32);
                case ESRenderVisualStyleId.FantasyAtmosphere:
                    return new ESRenderEffectsRecipe(style, 96, 320, 64, true, true, true, true, true, 32);
                case ESRenderVisualStyleId.MobileFlat:
                    return new ESRenderEffectsRecipe(style, 16, 48, 0, false, false, true, false, false, 8);
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown ES render style for effects recipe.");
            }
        }

        private static float ClampScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            return Math.Max(0.25f, Math.Min(2f, value));
        }
    }
}
