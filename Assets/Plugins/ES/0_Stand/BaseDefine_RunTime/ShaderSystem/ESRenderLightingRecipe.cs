using System;

namespace ES
{
    /// <summary>与 Unity Color 解耦的线性环境光颜色值。</summary>
    public readonly struct ESRenderRgbColor
    {
        public ESRenderRgbColor(float red, float green, float blue)
        {
            Red = Clamp(red);
            Green = Clamp(green);
            Blue = Clamp(blue);
        }

        public float Red { get; }
        public float Green { get; }
        public float Blue { get; }
        public static ESRenderRgbColor White => new ESRenderRgbColor(1f, 1f, 1f);

        private static float Clamp(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            return Math.Max(0f, Math.Min(1f, value));
        }
    }

    public enum ESRenderShadowMode
    {
        Disabled = 0,
        BakedOnly = 1,
        Realtime = 2,
        Mixed = 3
    }

    /// <summary>
    /// 光照与阴影的纯数据配方；具体 Light/URP Asset 写入仍由受管后端执行。
    /// </summary>
    public readonly struct ESRenderLightingRecipe
    {
        public ESRenderLightingRecipe(
            ESRenderVisualStyleId style,
            ESRenderShadowMode shadowMode,
            int additionalLightsPerObject,
            float shadowDistance,
            int cascadeCount,
            bool softShadows,
            bool reflectionProbes)
            : this(style, shadowMode, additionalLightsPerObject, shadowDistance, cascadeCount,
                softShadows, reflectionProbes, 1f,
                shadowMode == ESRenderShadowMode.Disabled ? 0f : 0.85f,
                0.05f, 0.4f, false, 1f, false, 6500f)
        {
        }

        public ESRenderLightingRecipe(
            ESRenderVisualStyleId style,
            ESRenderShadowMode shadowMode,
            int additionalLightsPerObject,
            float shadowDistance,
            int cascadeCount,
            bool softShadows,
            bool reflectionProbes,
            float mainLightIntensity,
            float shadowStrength,
            float shadowBias,
            float shadowNormalBias,
            bool contactShadows)
            : this(style, shadowMode, additionalLightsPerObject, shadowDistance, cascadeCount,
                softShadows, reflectionProbes, mainLightIntensity, shadowStrength,
                shadowBias, shadowNormalBias, contactShadows, 1f, false, 6500f)
        {
        }

        public ESRenderLightingRecipe(
            ESRenderVisualStyleId style,
            ESRenderShadowMode shadowMode,
            int additionalLightsPerObject,
            float shadowDistance,
            int cascadeCount,
            bool softShadows,
            bool reflectionProbes,
            float mainLightIntensity,
            float shadowStrength,
            float shadowBias,
            float shadowNormalBias,
            bool contactShadows,
            float ambientIntensity,
            bool useColorTemperature,
            float colorTemperatureKelvin)
            : this(style, shadowMode, additionalLightsPerObject, shadowDistance, cascadeCount,
                softShadows, reflectionProbes, mainLightIntensity, shadowStrength,
                shadowBias, shadowNormalBias, contactShadows, ambientIntensity,
                useColorTemperature, colorTemperatureKelvin, ESRenderRgbColor.White)
        {
        }

        public ESRenderLightingRecipe(
            ESRenderVisualStyleId style,
            ESRenderShadowMode shadowMode,
            int additionalLightsPerObject,
            float shadowDistance,
            int cascadeCount,
            bool softShadows,
            bool reflectionProbes,
            float mainLightIntensity,
            float shadowStrength,
            float shadowBias,
            float shadowNormalBias,
            bool contactShadows,
            float ambientIntensity,
            bool useColorTemperature,
            float colorTemperatureKelvin,
            ESRenderRgbColor ambientColor)
        {
            Style = style;
            ShadowMode = shadowMode;
            AdditionalLightsPerObject = Math.Max(0, Math.Min(8, additionalLightsPerObject));
            ShadowDistance = ClampFinite(shadowDistance, 0f, 500f, 0f);
            CascadeCount = Math.Max(0, Math.Min(4, cascadeCount));
            SoftShadows = softShadows;
            ReflectionProbes = reflectionProbes;
            MainLightIntensity = ClampFinite(mainLightIntensity, 0f, 8f, 1f);
            ShadowStrength = ClampFinite(shadowStrength, 0f, 1f, 0.85f);
            ShadowBias = ClampFinite(shadowBias, 0f, 2f, 0.05f);
            ShadowNormalBias = ClampFinite(shadowNormalBias, 0f, 3f, 0.4f);
            ContactShadows = contactShadows;
            AmbientIntensity = ClampFinite(ambientIntensity, 0f, 8f, 1f);
            UseColorTemperature = useColorTemperature;
            MainLightTemperatureKelvin = ClampFinite(colorTemperatureKelvin, 1000f, 20000f, 6500f);
            AmbientColor = ambientColor;
        }

        public ESRenderVisualStyleId Style { get; }
        public ESRenderShadowMode ShadowMode { get; }
        public int AdditionalLightsPerObject { get; }
        public float ShadowDistance { get; }
        public int CascadeCount { get; }
        public bool SoftShadows { get; }
        public bool ReflectionProbes { get; }
        public float MainLightIntensity { get; }
        public float ShadowStrength { get; }
        public float ShadowBias { get; }
        public float ShadowNormalBias { get; }
        public bool ContactShadows { get; }
        public float AmbientIntensity { get; }
        public bool UseColorTemperature { get; }
        public float MainLightTemperatureKelvin { get; }
        public ESRenderRgbColor AmbientColor { get; }
        public bool HasShadows => ShadowMode != ESRenderShadowMode.Disabled;
        public bool UsesRealtimeShadows => ShadowMode == ESRenderShadowMode.Realtime || ShadowMode == ESRenderShadowMode.Mixed;
        /// <summary>
        /// URP 主光实时阴影开关。BakedOnly 只依赖烘焙光照，不应打开实时阴影通道。
        /// </summary>
        public bool MainLightShadowsEnabled => UsesRealtimeShadows;
        public bool AdditionalLightShadowsEnabled => UsesRealtimeShadows && AdditionalLightsPerObject > 0;
        public int EstimatedShadowPassBudget => UsesRealtimeShadows
            ? CascadeCount + (AdditionalLightShadowsEnabled ? AdditionalLightsPerObject : 0)
            : 0;

        /// <summary>
        /// 业务侧推荐入口：使用命名参数表达调优意图，避免长位置参数误配。
        /// </summary>
        public static ESRenderLightingRecipe Create(
            ESRenderVisualStyleId style,
            ESRenderShadowMode shadowMode,
            int additionalLightsPerObject,
            float shadowDistance,
            int cascadeCount,
            bool softShadows,
            bool reflectionProbes,
            float mainLightIntensity = 1f,
            float shadowStrength = 0.85f,
            float shadowBias = 0.05f,
            float shadowNormalBias = 0.4f,
            bool contactShadows = false,
            float ambientIntensity = 1f,
            bool useColorTemperature = false,
            float colorTemperatureKelvin = 6500f,
            ESRenderRgbColor? ambientColor = null)
        {
            return new ESRenderLightingRecipe(
                style, shadowMode, additionalLightsPerObject, shadowDistance, cascadeCount,
                softShadows, reflectionProbes, mainLightIntensity, shadowStrength,
                shadowBias, shadowNormalBias, contactShadows, ambientIntensity,
                useColorTemperature, colorTemperatureKelvin,
                ambientColor ?? ESRenderRgbColor.White);
        }

        /// <summary>
        /// 业务/工具链推荐入口：创建并立即验证配方，避免把非法配置推迟到后端阶段才发现。
        /// </summary>
        public static bool TryCreate(
            ESRenderVisualStyleId style,
            ESRenderShadowMode shadowMode,
            int additionalLightsPerObject,
            float shadowDistance,
            int cascadeCount,
            bool softShadows,
            bool reflectionProbes,
            out ESRenderLightingRecipe recipe,
            out string reason,
            float mainLightIntensity = 1f,
            float shadowStrength = 0.85f,
            float shadowBias = 0.05f,
            float shadowNormalBias = 0.4f,
            bool contactShadows = false,
            float ambientIntensity = 1f,
            bool useColorTemperature = false,
            float colorTemperatureKelvin = 6500f,
            ESRenderRgbColor? ambientColor = null)
        {
            recipe = Create(
                style, shadowMode, additionalLightsPerObject, shadowDistance, cascadeCount,
                softShadows, reflectionProbes, mainLightIntensity, shadowStrength,
                shadowBias, shadowNormalBias, contactShadows, ambientIntensity,
                useColorTemperature, colorTemperatureKelvin, ambientColor);

            return recipe.IsValid(out reason);
        }

        public bool IsValid(out string reason)
        {
            if (!Enum.IsDefined(typeof(ESRenderVisualStyleId), Style))
            {
                reason = "lighting-style-unknown";
                return false;
            }

            if (!Enum.IsDefined(typeof(ESRenderShadowMode), ShadowMode))
            {
                reason = "lighting-shadow-mode-unknown";
                return false;
            }

            if (MainLightIntensity <= 0f)
            {
                reason = "main-light-must-be-positive";
                return false;
            }

            if (ShadowMode == ESRenderShadowMode.Disabled && (ShadowDistance > 0f || CascadeCount > 0))
            {
                reason = "disabled-shadows-cannot-have-budget";
                return false;
            }

            if (ShadowMode == ESRenderShadowMode.Disabled && (ShadowStrength > 0f || SoftShadows || ContactShadows))
            {
                reason = "disabled-shadows-cannot-have-shadow-features";
                return false;
            }

            if (ShadowMode != ESRenderShadowMode.Disabled && CascadeCount == 0)
            {
                reason = "enabled-shadows-require-cascades";
                return false;
            }

            if (CascadeCount == 3)
            {
                reason = "cascade-count-must-be-0-1-2-or-4";
                return false;
            }

            if (Style == ESRenderVisualStyleId.MobileFlat
                && (AdditionalLightsPerObject > 0 || SoftShadows || ReflectionProbes))
            {
                reason = "mobile-flat-disallows-expensive-lighting";
                return false;
            }

            if (Style == ESRenderVisualStyleId.MobileFlat && ContactShadows)
            {
                reason = "mobile-flat-disallows-contact-shadows";
                return false;
            }

            if (ContactShadows && ShadowMode == ESRenderShadowMode.BakedOnly)
            {
                reason = "contact-shadows-require-realtime-shadowing";
                return false;
            }

            if (SoftShadows && !UsesRealtimeShadows)
            {
                reason = "soft-shadows-require-realtime-shadowing";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static ESRenderLightingRecipe Resolve(ESRenderVisualStyleId style)
        {
            switch (style)
            {
                case ESRenderVisualStyleId.NaturalPbr:
                    return new ESRenderLightingRecipe(style, ESRenderShadowMode.Mixed, 2, 80f, 2, true, true);
                case ESRenderVisualStyleId.StylizedToon:
                    return new ESRenderLightingRecipe(style, ESRenderShadowMode.Realtime, 1, 50f, 1, false, false);
                case ESRenderVisualStyleId.NoirContrast:
                    return new ESRenderLightingRecipe(style, ESRenderShadowMode.Realtime, 2, 90f, 2, true, true);
                case ESRenderVisualStyleId.NeonSciFi:
                    return new ESRenderLightingRecipe(style, ESRenderShadowMode.Realtime, 4, 100f, 2, true, true);
                case ESRenderVisualStyleId.FantasyAtmosphere:
                    return new ESRenderLightingRecipe(style, ESRenderShadowMode.Mixed, 4, 150f, 4, true, true);
                case ESRenderVisualStyleId.MobileFlat:
                    return new ESRenderLightingRecipe(style, ESRenderShadowMode.BakedOnly, 0, 35f, 1, false, false);
                case ESRenderVisualStyleId.RetroPixel:
                    return new ESRenderLightingRecipe(style, ESRenderShadowMode.BakedOnly, 0, 40f, 1, false, false);
                case ESRenderVisualStyleId.HorrorGrit:
                    return new ESRenderLightingRecipe(style, ESRenderShadowMode.Realtime, 1, 60f, 2, false, false);
                case ESRenderVisualStyleId.CozyPastel:
                    return new ESRenderLightingRecipe(style, ESRenderShadowMode.Mixed, 1, 70f, 2, true, true);
                case ESRenderVisualStyleId.TacticalRealism:
                    return new ESRenderLightingRecipe(style, ESRenderShadowMode.Mixed, 2, 90f, 2, true, true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown ES render style for lighting recipe.");
            }
        }

        public static bool TryResolve(
            ESRenderVisualStyleId style,
            out ESRenderLightingRecipe recipe,
            out string reason)
        {
            recipe = default(ESRenderLightingRecipe);
            if (!Enum.IsDefined(typeof(ESRenderVisualStyleId), style))
            {
                reason = "lighting-style-unknown";
                return false;
            }
            recipe = Resolve(style);
            return recipe.IsValid(out reason);
        }

        public static ESRenderLightingRecipe Resolve(
            ESRenderVisualStyleId style,
            ESRenderQualityProfileId qualityProfile)
        {
            return Resolve(Resolve(style), qualityProfile);
        }

        /// <summary>
        /// 将质量预算投影到已编写的配方；只裁剪预算，不覆盖强度、环境光或色温等审美参数。
        /// </summary>
        public static ESRenderLightingRecipe Resolve(
            ESRenderLightingRecipe baseRecipe,
            ESRenderQualityProfileId qualityProfile)
        {
            ESRenderVisualStyleId style = baseRecipe.Style;
            switch (qualityProfile)
            {
                case ESRenderQualityProfileId.Performant:
                case ESRenderQualityProfileId.MobileStable:
                    return Create(
                        style,
                        baseRecipe.ShadowMode == ESRenderShadowMode.BakedOnly
                            ? ESRenderShadowMode.BakedOnly
                            : ESRenderShadowMode.Disabled,
                        0,
                        baseRecipe.ShadowMode == ESRenderShadowMode.BakedOnly
                            ? Math.Min(35f, baseRecipe.ShadowDistance)
                            : 0f,
                        baseRecipe.ShadowMode == ESRenderShadowMode.BakedOnly ? 1 : 0,
                        false,
                        false,
                        baseRecipe.MainLightIntensity,
                        baseRecipe.ShadowMode == ESRenderShadowMode.Disabled ? 0f : 0.75f,
                        baseRecipe.ShadowBias,
                        baseRecipe.ShadowNormalBias,
                        false,
                        baseRecipe.AmbientIntensity,
                        baseRecipe.UseColorTemperature,
                        baseRecipe.MainLightTemperatureKelvin,
                        baseRecipe.AmbientColor);
                case ESRenderQualityProfileId.CombatReadability:
                    return Create(
                        style, baseRecipe.ShadowMode, Math.Min(2, baseRecipe.AdditionalLightsPerObject),
                        Math.Min(80f, baseRecipe.ShadowDistance), Math.Min(2, baseRecipe.CascadeCount),
                        false, baseRecipe.ReflectionProbes, baseRecipe.MainLightIntensity,
                        baseRecipe.ShadowStrength, baseRecipe.ShadowBias, baseRecipe.ShadowNormalBias, false,
                        baseRecipe.AmbientIntensity, baseRecipe.UseColorTemperature,
                        baseRecipe.MainLightTemperatureKelvin,
                        baseRecipe.AmbientColor);
                case ESRenderQualityProfileId.Balanced:
                case ESRenderQualityProfileId.HighFidelity:
                case ESRenderQualityProfileId.CinematicShowcase:
                    return baseRecipe;
                default:
                    throw new ArgumentOutOfRangeException(nameof(qualityProfile), qualityProfile,
                        "Unknown ES quality profile for lighting recipe.");
            }
        }

        public static bool TryResolve(
            ESRenderVisualStyleId style,
            ESRenderQualityProfileId qualityProfile,
            out ESRenderLightingRecipe recipe,
            out string reason)
        {
            recipe = default(ESRenderLightingRecipe);
            if (!TryResolve(style, out ESRenderLightingRecipe baseRecipe, out reason))
                return false;
            if (!Enum.IsDefined(typeof(ESRenderQualityProfileId), qualityProfile))
            {
                reason = "lighting-quality-profile-unknown";
                return false;
            }
            recipe = Resolve(style, qualityProfile);
            return recipe.IsValid(out reason);
        }

        /// <summary>
        /// 对已编写配方执行质量投影并返回可诊断结果，避免业务层依赖异常控制流。
        /// </summary>
        public static bool TryResolve(
            ESRenderLightingRecipe authoredRecipe,
            ESRenderQualityProfileId qualityProfile,
            out ESRenderLightingRecipe recipe,
            out string reason)
        {
            recipe = default(ESRenderLightingRecipe);
            if (!authoredRecipe.IsValid(out reason))
                return false;
            if (!Enum.IsDefined(typeof(ESRenderQualityProfileId), qualityProfile))
            {
                reason = "lighting-quality-profile-unknown";
                return false;
            }
            recipe = Resolve(authoredRecipe, qualityProfile);
            return recipe.IsValid(out reason);
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return fallback;
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
