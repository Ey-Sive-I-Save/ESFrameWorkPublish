using System;

namespace ES
{
    public enum ESRenderContentTypeId : byte
    {
        Action = 0,
        RolePlaying = 1,
        Strategy = 2,
        Horror = 3,
        Cozy = 4,
        Racing = 5,
        Simulation = 6,
        Stylized = 7
    }

    /// <summary>
    /// 内容类型到 ES 风格、场景意图与预算倍率的纯数据适配。
    /// </summary>
    public readonly struct ESRenderContentTypeProfile
    {
        public ESRenderContentTypeProfile(
            ESRenderContentTypeId contentType,
            ESRenderVisualStyleId preferredStyle,
            ESRenderSceneIntentId defaultIntent,
            float transparencyBudgetScale,
            float particleBudgetScale,
            bool prioritizeReadability)
        {
            ContentType = contentType;
            PreferredStyle = preferredStyle;
            DefaultIntent = defaultIntent;
            TransparencyBudgetScale = Clamp(transparencyBudgetScale, 0.25f, 2f, 1f);
            ParticleBudgetScale = Clamp(particleBudgetScale, 0.25f, 2f, 1f);
            PrioritizeReadability = prioritizeReadability;
        }

        public ESRenderContentTypeId ContentType { get; }
        public ESRenderVisualStyleId PreferredStyle { get; }
        public ESRenderSceneIntentId DefaultIntent { get; }
        public float TransparencyBudgetScale { get; }
        public float ParticleBudgetScale { get; }
        public bool PrioritizeReadability { get; }

        public static ESRenderContentTypeProfile Resolve(ESRenderContentTypeId contentType)
        {
            switch (contentType)
            {
                case ESRenderContentTypeId.Action: return new ESRenderContentTypeProfile(contentType, ESRenderVisualStyleId.StylizedToon, ESRenderSceneIntentId.Combat, 0.75f, 0.8f, true);
                case ESRenderContentTypeId.RolePlaying: return new ESRenderContentTypeProfile(contentType, ESRenderVisualStyleId.NaturalPbr, ESRenderSceneIntentId.Exploration, 1f, 1f, true);
                case ESRenderContentTypeId.Strategy: return new ESRenderContentTypeProfile(contentType, ESRenderVisualStyleId.TacticalRealism, ESRenderSceneIntentId.Exploration, 0.8f, 0.65f, true);
                case ESRenderContentTypeId.Horror: return new ESRenderContentTypeProfile(contentType, ESRenderVisualStyleId.HorrorGrit, ESRenderSceneIntentId.Cinematic, 0.9f, 0.75f, true);
                case ESRenderContentTypeId.Cozy: return new ESRenderContentTypeProfile(contentType, ESRenderVisualStyleId.CozyPastel, ESRenderSceneIntentId.Social, 0.7f, 0.7f, true);
                case ESRenderContentTypeId.Racing: return new ESRenderContentTypeProfile(contentType, ESRenderVisualStyleId.NeonSciFi, ESRenderSceneIntentId.Combat, 0.85f, 0.9f, false);
                case ESRenderContentTypeId.Simulation: return new ESRenderContentTypeProfile(contentType, ESRenderVisualStyleId.NaturalPbr, ESRenderSceneIntentId.Exploration, 0.65f, 0.6f, true);
                case ESRenderContentTypeId.Stylized: return new ESRenderContentTypeProfile(contentType, ESRenderVisualStyleId.RetroPixel, ESRenderSceneIntentId.Menu, 0.5f, 0.5f, true);
                default: throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unknown ES render content type.");
            }
        }

        private static float Clamp(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return Math.Max(min, Math.Min(max, value));
        }
    }

    public static class ESRenderContentTypeCatalog
    {
        public const int Count = 8;

        public static bool TryGet(ESRenderContentTypeId contentType, out ESRenderContentTypeProfile profile)
        {
            try { profile = ESRenderContentTypeProfile.Resolve(contentType); return true; }
            catch (ArgumentOutOfRangeException) { profile = default(ESRenderContentTypeProfile); return false; }
        }

        public static bool ValidateBuiltIn(out string reason)
        {
            for (int i = 0; i < Count; i++)
            {
                ESRenderContentTypeProfile profile = ESRenderContentTypeProfile.Resolve((ESRenderContentTypeId)i);
                if (profile.ContentType != (ESRenderContentTypeId)i || profile.TransparencyBudgetScale <= 0f || profile.ParticleBudgetScale <= 0f)
                {
                    reason = "content-type-profile-invalid-" + i;
                    return false;
                }
            }
            reason = string.Empty;
            return true;
        }
    }

    public readonly struct ESRenderSceneTemplateDescriptor
    {
        public ESRenderSceneTemplateDescriptor(
            ESRenderContentTypeId contentType,
            ESRenderVisualStyleId style,
            ESRenderSceneIntentId intent,
            ESRenderTemplateResourceBinding resources)
        {
            ContentType = contentType;
            Style = style;
            Intent = intent;
            Resources = resources;
        }

        public ESRenderContentTypeId ContentType { get; }
        public ESRenderVisualStyleId Style { get; }
        public ESRenderSceneIntentId Intent { get; }
        public ESRenderTemplateResourceBinding Resources { get; }
    }

    public static class ESRenderSceneTemplateCatalog
    {
        public static bool TryResolve(
            ESRenderContentTypeId contentType,
            out ESRenderSceneTemplateDescriptor descriptor,
            out string reason)
        {
            ESRenderContentTypeProfile profile;
            if (!ESRenderContentTypeCatalog.TryGet(contentType, out profile))
            {
                descriptor = default(ESRenderSceneTemplateDescriptor);
                reason = "content-type-profile-not-found";
                return false;
            }

            ESRenderStylePreset stylePreset;
            if (!ESRenderStyleCatalog.TryGet(profile.PreferredStyle, out stylePreset))
            {
                descriptor = default(ESRenderSceneTemplateDescriptor);
                reason = "scene-template-style-not-found";
                return false;
            }

            ESRenderTemplateResourceBinding resources;
            if (!ESRenderTemplateResourceMap.TryGet(profile.PreferredStyle, stylePreset.QualityProfile, out resources, out reason))
            {
                descriptor = default(ESRenderSceneTemplateDescriptor);
                return false;
            }

            descriptor = new ESRenderSceneTemplateDescriptor(contentType, profile.PreferredStyle, profile.DefaultIntent, resources);
            reason = string.Empty;
            return true;
        }
    }

    public static class ESRenderSceneTemplatePlanFactory
    {
        public static bool TryCreate(
            ESRenderContentTypeId contentType,
            ESRenderPlatformId platform,
            out ESRenderSceneTemplateDescriptor descriptor,
            out ESRenderTemplatePlan plan,
            out string reason)
        {
            if (!ESRenderSceneTemplateCatalog.TryResolve(contentType, out descriptor, out reason))
            {
                plan = default(ESRenderTemplatePlan);
                return false;
            }

            if (!ESRenderTemplatePlan.TryCreate(
                descriptor.Style,
                descriptor.Intent,
                platform,
                contentType,
                out plan,
                out reason))
                return false;

            return true;
        }
    }
}
