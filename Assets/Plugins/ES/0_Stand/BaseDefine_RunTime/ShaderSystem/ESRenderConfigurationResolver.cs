using System;

namespace ES
{
    public readonly struct ESRenderResolvedConfiguration
    {
        internal ESRenderResolvedConfiguration(
            ESRenderSceneIntentId sceneIntent,
            ESRenderPlatformId platform,
            ESRenderStylePreset style,
            ESRenderFeatureRecipe featureRecipe,
            ESRenderMaterialRecipe materialRecipe,
            ESRenderLightingRecipe lightingRecipe,
            ESRenderEffectsRecipe effectsRecipe,
            ESRenderQualityProfileId qualityProfile,
            bool styleFallback,
            bool qualityDowngraded,
            int featureBudget,
            bool volumetricsEnabled,
            ESRenderContentTypeId contentType,
            float transparencyBudgetScale,
            float particleBudgetScale)
        {
            SceneIntent = sceneIntent;
            Platform = platform;
            Style = style;
            FeatureRecipe = featureRecipe;
            MaterialRecipe = materialRecipe;
            LightingRecipe = lightingRecipe;
            EffectsRecipe = effectsRecipe;
            QualityProfile = qualityProfile;
            StyleFallback = styleFallback;
            QualityDowngraded = qualityDowngraded;
            FeatureBudget = featureBudget;
            VolumetricsEnabled = volumetricsEnabled;
            ContentType = contentType;
            TransparencyBudgetScale = transparencyBudgetScale;
            ParticleBudgetScale = particleBudgetScale;
        }

        public ESRenderSceneIntentId SceneIntent { get; }
        public ESRenderPlatformId Platform { get; }
        public ESRenderStylePreset Style { get; }
        public ESRenderFeatureRecipe FeatureRecipe { get; }
        public ESRenderMaterialRecipe MaterialRecipe { get; }
        public ESRenderLightingRecipe LightingRecipe { get; }
        public ESRenderEffectsRecipe EffectsRecipe { get; }
        public ESRenderQualityProfileId QualityProfile { get; }
        public bool StyleFallback { get; }
        public bool QualityDowngraded { get; }
        public int FeatureBudget { get; }
        public bool VolumetricsEnabled { get; }
        public ESRenderContentTypeId ContentType { get; }
        public float TransparencyBudgetScale { get; }
        public float ParticleBudgetScale { get; }
    }

    /// <summary>
    /// 将场景意图、平台能力和风格/Feature 配方合并为一个可审计结果。
    /// 不访问 Unity API，不写入 GameManager 或 RendererData。
    /// </summary>
    public static class ESRenderConfigurationResolver
    {
        public static bool TryResolve(
            ESRenderSceneIntent sceneIntent,
            ESRenderPlatformProfile platform,
            out ESRenderResolvedConfiguration configuration,
            out string reason)
        {
            return TryResolve(sceneIntent, platform, ESRenderContentTypeId.RolePlaying, out configuration, out reason);
        }

        public static bool TryResolve(
            ESRenderSceneIntent sceneIntent,
            ESRenderPlatformProfile platform,
            ESRenderContentTypeId contentType,
            out ESRenderResolvedConfiguration configuration,
            out string reason)
        {
            configuration = default(ESRenderResolvedConfiguration);
            ESRenderStylePreset preset;
            bool styleFallback;
            if (!sceneIntent.TryResolvePreset(out preset, out styleFallback))
            {
                reason = "scene-style-unavailable";
                return false;
            }

            ESRenderQualityProfileId requestedQuality = preset.QualityProfile;
            ESRenderQualityProfileId effectiveQuality = platform.ClampQuality(requestedQuality);
            bool qualityDowngraded = effectiveQuality != requestedQuality;
            if (qualityDowngraded && !ESRenderStyleCatalog.TryGetFirstForQuality(effectiveQuality, out preset))
            {
                reason = "platform-quality-fallback-unavailable";
                return false;
            }

            ESRenderFeatureRecipe recipe = ESRenderFeatureRecipe.Resolve(preset.Style);
            ESRenderMaterialRecipe material = ESRenderMaterialRecipe.Resolve(preset.Style);
            ESRenderLightingRecipe lighting = ESRenderLightingRecipe.Resolve(preset.Style, effectiveQuality);
            ESRenderEffectsRecipe effects = ESRenderEffectsRecipe.Resolve(preset.Style);
            ESRenderContentTypeProfile contentProfile;
            if (!ESRenderContentTypeCatalog.TryGet(contentType, out contentProfile))
            {
                reason = "content-type-profile-unavailable";
                return false;
            }
            effects = effects.WithBudgetScale(contentProfile.TransparencyBudgetScale, contentProfile.ParticleBudgetScale);
            if (!material.IsValid(out reason) || !lighting.IsValid(out reason) || !effects.IsValid(out reason))
                return false;
            int featureBudget = Math.Max(0, (int)Math.Round(recipe.FeatureBudget * platform.FeatureBudgetScale));
            bool volumetrics = recipe.AllowVolumetrics && platform.FeatureBudgetScale >= 0.75f;
            configuration = new ESRenderResolvedConfiguration(
                sceneIntent.Intent,
                platform.Platform,
                preset,
                recipe,
                material,
                lighting,
                effects,
                effectiveQuality,
                styleFallback || qualityDowngraded,
                qualityDowngraded,
                featureBudget,
                volumetrics,
                contentType,
                contentProfile.TransparencyBudgetScale,
                contentProfile.ParticleBudgetScale);
            reason = string.Empty;
            return true;
        }
    }
}
