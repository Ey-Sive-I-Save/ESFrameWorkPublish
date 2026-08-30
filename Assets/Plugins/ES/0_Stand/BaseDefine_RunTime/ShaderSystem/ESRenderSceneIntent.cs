using System;

namespace ES
{
    public enum ESRenderSceneIntentId
    {
        Combat = 0,
        Exploration = 1,
        Menu = 2,
        Cinematic = 3,
        Social = 4,
        PhotoMode = 5
    }

    /// <summary>
    /// 场景语义到视觉模板的纯数据映射。它不持有 Unity 对象，也不执行后端写入。
    /// </summary>
    public readonly struct ESRenderSceneIntent
    {
        public ESRenderSceneIntent(
            ESRenderSceneIntentId intent,
            ESRenderVisualStyleId preferredStyle,
            ESRenderQualityProfileId fallbackQuality,
            bool allowPostProcessing,
            bool preserveReadability,
            float transparencyBudgetScale)
        {
            Intent = intent;
            PreferredStyle = preferredStyle;
            FallbackQuality = fallbackQuality;
            AllowPostProcessing = allowPostProcessing;
            PreserveReadability = preserveReadability;
            TransparencyBudgetScale = ClampFinite(transparencyBudgetScale, 0.25f, 2f, 1f);
        }

        public ESRenderSceneIntentId Intent { get; }
        public ESRenderVisualStyleId PreferredStyle { get; }
        public ESRenderQualityProfileId FallbackQuality { get; }
        public bool AllowPostProcessing { get; }
        public bool PreserveReadability { get; }
        public float TransparencyBudgetScale { get; }

        public bool TryResolvePreset(out ESRenderStylePreset preset, out bool usedFallback)
        {
            if (ESRenderStyleCatalog.TryGet(PreferredStyle, out preset))
            {
                usedFallback = false;
                return true;
            }

            usedFallback = true;
            return ESRenderStyleCatalog.TryGetFirstForQuality(FallbackQuality, out preset);
        }

        public static ESRenderSceneIntent Resolve(ESRenderSceneIntentId intent)
        {
            switch (intent)
            {
                case ESRenderSceneIntentId.Combat:
                    return new ESRenderSceneIntent(intent, ESRenderVisualStyleId.StylizedToon, ESRenderQualityProfileId.CombatReadability, false, true, 0.75f);
                case ESRenderSceneIntentId.Exploration:
                    return new ESRenderSceneIntent(intent, ESRenderVisualStyleId.NaturalPbr, ESRenderQualityProfileId.Balanced, true, true, 1f);
                case ESRenderSceneIntentId.Menu:
                    return new ESRenderSceneIntent(intent, ESRenderVisualStyleId.MobileFlat, ESRenderQualityProfileId.MobileStable, true, true, 0.5f);
                case ESRenderSceneIntentId.Cinematic:
                    return new ESRenderSceneIntent(intent, ESRenderVisualStyleId.FantasyAtmosphere, ESRenderQualityProfileId.CinematicShowcase, true, true, 1.5f);
                case ESRenderSceneIntentId.Social:
                    return new ESRenderSceneIntent(intent, ESRenderVisualStyleId.NeonSciFi, ESRenderQualityProfileId.HighFidelity, true, true, 1.25f);
                case ESRenderSceneIntentId.PhotoMode:
                    return new ESRenderSceneIntent(intent, ESRenderVisualStyleId.NoirContrast, ESRenderQualityProfileId.HighFidelity, true, false, 1.5f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown ES render scene intent.");
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
