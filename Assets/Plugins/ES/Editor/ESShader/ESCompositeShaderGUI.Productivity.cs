using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public sealed partial class ESCompositeShaderGUI
    {
        #region View And Preset Model

        private enum InspectorViewLevel
        {
            Standard = 0,
            Advanced = 1,
            Expert = 2
        }

        private enum PresetValueKind
        {
            Float,
            Color,
            Vector
        }
        private sealed class PresetAssignment
        {
            internal readonly string PropertyName;
            internal readonly PresetValueKind Kind;
            internal readonly Vector4 Value;

            internal PresetAssignment(string propertyName, float value)
            {
                PropertyName = propertyName;
                Kind = PresetValueKind.Float;
                Value = new Vector4(value, 0f, 0f, 0f);
            }

            internal PresetAssignment(string propertyName, Color value)
            {
                PropertyName = propertyName;
                Kind = PresetValueKind.Color;
                Value = value;
            }

            internal PresetAssignment(string propertyName, Vector4 value)
            {
                PropertyName = propertyName;
                Kind = PresetValueKind.Vector;
                Value = value;
            }

            internal bool IsDifferent(Material material)
            {
                if (material == null || !material.HasProperty(PropertyName)) return false;
                switch (Kind)
                {
                    case PresetValueKind.Color:
                        return !Approximately(material.GetColor(PropertyName), (Color)Value);
                    case PresetValueKind.Vector:
                        return !Approximately(material.GetVector(PropertyName), Value);
                    default:
                        return !Mathf.Approximately(material.GetFloat(PropertyName), Value.x);
                }
            }

            internal void Apply(Material material)
            {
                if (material == null || !material.HasProperty(PropertyName)) return;
                switch (Kind)
                {
                    case PresetValueKind.Color:
                        material.SetColor(PropertyName, (Color)Value);
                        break;
                    case PresetValueKind.Vector:
                        material.SetVector(PropertyName, Value);
                        break;
                    default:
                        material.SetFloat(PropertyName, Value.x);
                        break;
                }
            }

            internal string FormatTarget()
            {
                switch (Kind)
                {
                    case PresetValueKind.Color:
                        Color color = Value;
                        return string.Format("RGBA({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##})", color.r, color.g, color.b, color.a);
                    case PresetValueKind.Vector:
                        return string.Format("({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##})", Value.x, Value.y, Value.z, Value.w);
                    default:
                        return Value.x.ToString("0.###");
                }
            }
        }

        private sealed class CompositePreset
        {
            internal readonly string Id;
            internal readonly string Name;
            internal readonly string Description;
            internal readonly string ShaderName;
            internal readonly PresetAssignment[] Assignments;

            internal CompositePreset(string id, string name, string description, string shaderName, params PresetAssignment[] assignments)
            {
                Id = id;
                Name = name;
                Description = description;
                ShaderName = shaderName;
                Assignments = assignments ?? Array.Empty<PresetAssignment>();
            }
        }

        private static readonly string[] ViewModeNames = { "标准", "进阶", "高级" };
        private static readonly GUIContent ClearFilterButtonContent = new GUIContent("清除筛选", "清除当前搜索与快捷分类筛选");
        private static readonly GUIContent SelectPresetDifferencesButtonContent = new GUIContent("全选差异");
        private static readonly GUIContent CancelPresetSelectionButtonContent = new GUIContent("全部取消");
        private static readonly GUIContent ApplyPresetSelectionButtonContent = new GUIContent("应用所选");
        // 显示级别是明确的编辑器元数据，不根据名称片段推断，避免新增属性被意外藏起来。
        // 未列出的属性保持“标准”可见；只有确实需要背景知识或较高成本的入口才提升级别。
        private static readonly HashSet<string> AdvancedViewProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "_EnableSequence", "_SequencePlayback", "_SequenceColumns", "_SequenceRows", "_SequenceFrame", "_SequenceSpeed",
            "_EnablePolarUV", "_PolarCenter", "_PolarRadialScale", "_PolarAngularScale", "_PolarRotationSpeed",
            "_EnableFlowMap", "_FlowMap", "_FlowMapScale", "_FlowMapSpeed", "_FlowMapStrength",
            "_EnableVertexAnimation", "_VertexAnimationDirection", "_VertexAnimationAmplitude", "_VertexAnimationFrequency", "_VertexAnimationSpeed", "_VertexAnimationMask",
            "_EnableChromatic", "_ChromaticOffset", "_ChromaticIntensity", "_ChromaticEdgeOnly", "_ChromaticAngle",
            "_EnablePalette", "_PaletteTex", "_PaletteRow", "_PaletteStrength",
            "_EnableHalftone", "_HalftoneScale", "_HalftoneAngle", "_HalftoneStrength",
            "_TilingMode", "_WorldTilingScale", "_WorldTilingOffset", "_WorldTilingPixelsPerUnit",
            "_ScreenTilingScale", "_ScreenTilingOffset", "_ScreenTilingPixelsPerUnit",
            "_UberNoiseTexture", "_EnableFlame", "_FlameBrightness", "_FlameSmooth", "_FlameRadius", "_FlameSpeed",
            "_FlameNoiseFactor", "_FlameNoiseHeightFactor", "_FlameNoiseScale",
            "_EnableSmoke", "_SmokeAlpha", "_SmokeSmoothness", "_SmokeNoiseScale", "_SmokeNoiseFactor", "_SmokeDarkEdge", "_SmokeVertexSeed",
            "_EnableWind", "_WindDirection", "_WindAmplitude", "_WindFrequency", "_WindSpeed", "_WindAnchor", "_WindGlobalInfluence",
            "_EnableSquish", "_SquishAmount", "_SquishSpeed",
            "_EnableWiggle", "_WiggleAmplitude", "_WiggleFrequency", "_WiggleSpeed",
            "_EnableVibrate", "_VibrateAmplitude", "_VibrateSpeed",
            "_EnableSqueeze", "_SqueezeFade", "_SqueezeScale", "_SqueezePower", "_SqueezeCenter",
            "_EnableSineRotate", "_SineRotateFade", "_SineRotateAngle", "_SineRotateFrequency", "_SineRotatePivot",
            "_EnableSineMove", "_SineMoveFade", "_SineMoveOffset", "_SineMoveFrequency",
            "_EnableSineScale", "_SineScaleFrequency", "_SineScaleFactor",
            "_EnableCustomFade", "_CustomFadeFadeMask", "_CustomFadeSmoothness", "_CustomFadeNoiseScale", "_CustomFadeNoiseFactor", "_CustomFadeAlpha",
            "_EnableFullGlowDissolve", "_FullGlowDissolveFade", "_FullGlowDissolveWidth", "_FullGlowDissolveEdgeColor", "_FullGlowDissolveNoiseScale",
            "_EnableRadialMask", "_RadialMaskCenter", "_RadialMaskRadius", "_RadialMaskSoftness", "_RadialMaskInvert",
            "_EnableFresnelMask", "_FresnelPower", "_FresnelMin", "_FresnelMax", "_FresnelAlphaInfluence", "_FresnelColor", "_FresnelIntensity",
            "_EnableSoftParticles", "_SoftParticleNear", "_SoftParticleFar",
            "_EnableDepthIntersection", "_DepthIntersectionColor", "_DepthIntersectionDistance", "_DepthIntersectionIntensity",
            "_StencilComp", "_Stencil", "_StencilOp", "_StencilReadMask", "_StencilWriteMask", "_ColorMask"
        };

        private static readonly HashSet<string> ExpertViewProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "_EnableVertexStreams", "_VertexStreamUVStrength", "_VertexStreamFrameStrength", "_VertexStreamDissolveStrength", "_VertexStreamEmissionStrength",
            "_EnableBlur", "_BlurRadius", "_BlurIntensity",
            "_EnableSparkle", "_SparkleColor", "_SparkleScale", "_SparkleSpeed", "_SparkleDensity", "_SparkleSharpness", "_SparkleIntensity",
            "_EnableCamouflage", "_CamouflageFade", "_CamouflageBaseColor", "_CamouflageContrast",
            "_CamouflageColorA", "_CamouflageDensityA", "_CamouflageSmoothnessA", "_CamouflageNoiseScaleA",
            "_CamouflageColorB", "_CamouflageDensityB", "_CamouflageSmoothnessB", "_CamouflageNoiseScaleB",
            "_CamouflageAnimationToggle", "_CamouflageDistortionSpeed", "_CamouflageDistortionIntensity", "_CamouflageDistortionScale",
            "_EnableMetal", "_MetalFade", "_MetalColor", "_MetalContrast", "_MetalHighlightColor", "_MetalHighlightDensity",
            "_MetalHighlightContrast", "_MetalNoiseScale", "_MetalNoiseSpeed", "_MetalNoiseDistortionScale",
            "_MetalNoiseDistortionSpeed", "_MetalNoiseDistortion", "_MetalMaskToggle", "_MetalMask",
            "_EnableEnchanted", "_EnchantedFade", "_EnchantedSpeed", "_EnchantedScale", "_EnchantedBrightness",
            "_EnchantedContrast", "_EnchantedReduce", "_EnchantedRainbowToggle", "_EnchantedRainbowSpeed",
            "_EnchantedRainbowDensity", "_EnchantedRainbowSaturation", "_EnchantedLowColor", "_EnchantedHighColor", "_EnchantedLerpToggle",
            "_EnableShifting", "_ShiftingFade", "_ShiftingSpeed", "_ShiftingDensity", "_ShiftingBrightness",
            "_ShiftingContrast", "_ShiftingRainbowToggle", "_ShiftingSaturation", "_ShiftingColorA", "_ShiftingColorB",
            "_EnableHologram", "_HologramColor", "_HologramFrequency", "_HologramLineFrequency", "_HologramGap", "_HologramLineGap", "_HologramSpeed", "_HologramMinAlpha",
            "_HologramFade", "_HologramContrast", "_HologramSpace", "_HologramDistortionOffset",
            "_HologramDistortionSpeed", "_HologramDistortionDensity", "_HologramDistortionScale",
            "_EnableGlitch", "_GlitchAmount", "_GlitchIntensity", "_GlitchSpeed", "_GlitchFade",
            "_GlitchMaskMin", "_GlitchMaskScale", "_GlitchMaskSpeed", "_GlitchHueSpeed",
            "_GlitchBrightness", "_GlitchNoiseScale", "_GlitchNoiseSpeed", "_GlitchDistortion",
            "_GlitchDistortionScale", "_GlitchDistortionSpeed", "_ESNativeStatusContract",
            "_BlendMode", "_ZWriteMode", "_ZTest", "_Cull", "_QueueOffset"
        };

        private static readonly CompositePreset[] BuiltInPresets =
        {
            new CompositePreset(
                "2d.shine", "2D 扫光强调", "用一条可控高光带突出按钮、卡牌和拾取物。", "ES/2D/Composite URP",
                new PresetAssignment("_EnableShine", 1f),
                new PresetAssignment("_ShineColor", new Color(1.8f, 1.5f, 0.65f, 1f)),
                new PresetAssignment("_ShineSpeed", 1.2f),
                new PresetAssignment("_ShineWidth", 0.14f),
                new PresetAssignment("_ShineDirection", new Vector4(0.8660254f, 0.5f, 0f, 0f)),
                new PresetAssignment("_ShineIntensity", 1.35f)),
            new CompositePreset(
                "2d.dissolve", "2D 噪声消散", "以噪声边缘消散图片，进度可继续交给动画或业务代码。", "ES/2D/Composite URP",
                new PresetAssignment("_FadeMode", 3f),
                new PresetAssignment("_FadeProgress", 0.45f),
                new PresetAssignment("_FadeWidth", 0.09f),
                new PresetAssignment("_FadeNoiseFactor", 0.25f),
                new PresetAssignment("_DissolveEdgeColor", new Color(2.2f, 0.18f, 0.02f, 1f))),
            new CompositePreset(
                "2d.shadow-tint", "2D 阴影暗部", "用单次偏移阴影和冷色暗部染色增强精灵层次，不改变亮部主色。", "ES/2D/Composite URP",
                new PresetAssignment("_EnableShadow", 1f),
                new PresetAssignment("_ShadowFade", 0.68f),
                new PresetAssignment("_ShadowOffset", new Vector4(0.08f, -0.08f, 0f, 0f)),
                new PresetAssignment("_ShadowColor", new Color(0.035f, 0.045f, 0.08f, 1f)),
                new PresetAssignment("_EnableBlackTint", 1f),
                new PresetAssignment("_BlackTintFade", 0.32f),
                new PresetAssignment("_BlackTintColor", new Color(0.03f, 0.08f, 0.22f, 1f)),
                new PresetAssignment("_BlackTintPower", 4f)),
            new CompositePreset(
                "2d.camouflage", "2D 林地迷彩", "以静态三色噪声覆盖原图明度，适合潜行状态和环境伪装。", "ES/2D/Composite URP",
                new PresetAssignment("_EnableCamouflage", 1f),
                new PresetAssignment("_CamouflageFade", 0.9f),
                new PresetAssignment("_CamouflageBaseColor", new Color(0.36f, 0.42f, 0.24f, 1f)),
                new PresetAssignment("_CamouflageColorA", new Color(0.18f, 0.24f, 0.12f, 1f)),
                new PresetAssignment("_CamouflageColorB", new Color(0.52f, 0.48f, 0.28f, 1f)),
                new PresetAssignment("_CamouflageDensityA", 0.42f),
                new PresetAssignment("_CamouflageDensityB", 0.56f),
                new PresetAssignment("_CamouflageNoiseScaleA", new Vector4(0.22f, 0.22f, 0f, 0f)),
                new PresetAssignment("_CamouflageNoiseScaleB", new Vector4(0.38f, 0.38f, 0f, 0f))),
            new CompositePreset(
                "2d.metal", "2D 流动金属", "以暖色 HDR 高光和缓慢噪声流动塑造金属表面。", "ES/2D/Composite URP",
                new PresetAssignment("_EnableMetal", 1f),
                new PresetAssignment("_MetalFade", 0.82f),
                new PresetAssignment("_MetalColor", new Color(1.6f, 0.72f, 0.12f, 1f)),
                new PresetAssignment("_MetalContrast", 1.8f),
                new PresetAssignment("_MetalHighlightColor", new Color(3.2f, 2.1f, 0.45f, 1f)),
                new PresetAssignment("_MetalHighlightDensity", 0.72f),
                new PresetAssignment("_MetalHighlightContrast", 2.4f),
                new PresetAssignment("_MetalNoiseSpeed", new Vector4(0.04f, 0.08f, 0f, 0f))),
            new CompositePreset(
                "lit.rim", "Lit 轮廓强调", "保持 URP Lit 光照，并用冷色边缘光强化角色或交互物轮廓。", "ES/3D/Lit Composite URP",
                new PresetAssignment("_EnableRim", 1f),
                new PresetAssignment("_RimColor", new Color(0.05f, 0.55f, 1.7f, 1f)),
                new PresetAssignment("_RimPower", 3.2f),
                new PresetAssignment("_RimIntensity", 1.5f),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "lit.burn", "Lit 燃烧溶解", "以高亮燃烧边缘推进溶解，适合受击、死亡和场景转化。", "ES/3D/Lit Composite URP",
                new PresetAssignment("_DissolveMode", 1f),
                new PresetAssignment("_DissolveProgress", 0.4f),
                new PresetAssignment("_DissolveSoftness", 0.06f),
                new PresetAssignment("_EnableBurn", 1f),
                new PresetAssignment("_BurnProgress", 0.4f),
                new PresetAssignment("_BurnWidth", 0.12f),
                new PresetAssignment("_BurnEdgeColor", new Color(2.5f, 0.2f, 0.01f, 1f)),
                new PresetAssignment("_QualityTier", 2f)),
            new CompositePreset(
                "ui.shine", "UI 扫光反馈", "轻量扫光用于按钮确认、奖励展示和卡牌强调。", "ES/UI/Composite URP",
                new PresetAssignment("_EnableShine", 1f),
                new PresetAssignment("_ShineColor", new Color(1.7f, 1.45f, 0.7f, 1f)),
                new PresetAssignment("_ShineSpeed", 1.1f),
                new PresetAssignment("_ShineWidth", 0.13f),
                new PresetAssignment("_ShineDirection", new Vector4(0.8660254f, 0.5f, 0f, 0f)),
                new PresetAssignment("_ShineIntensity", 1.25f)),
            new CompositePreset(
                "ui.sine-glow", "UI 呼吸辉光", "按统一时间源周期叠加青色辉光，适合可交互提示与冷却完成反馈。", "ES/UI/Composite URP",
                new PresetAssignment("_EnableSineGlow", 1f),
                new PresetAssignment("_SineGlowFade", 0.72f),
                new PresetAssignment("_SineGlowColor", new Color(0.08f, 1.2f, 2.4f, 1f)),
                new PresetAssignment("_SineGlowContrast", 1.15f),
                new PresetAssignment("_SineGlowFrequency", 3.2f),
                new PresetAssignment("_SineGlowMin", 0f),
                new PresetAssignment("_SineGlowMax", 0.65f)),
            new CompositePreset(
                "ui.enchanted", "UI 附魔流光", "双色滚动流光用于稀有卡牌、装备与奖励强调。", "ES/UI/Composite URP",
                new PresetAssignment("_EnableEnchanted", 1f),
                new PresetAssignment("_EnchantedFade", 0.72f),
                new PresetAssignment("_EnchantedSpeed", new Vector4(0.12f, 0.75f, 0f, 0f)),
                new PresetAssignment("_EnchantedScale", new Vector4(0.18f, 0.18f, 0f, 0f)),
                new PresetAssignment("_EnchantedBrightness", 1.25f),
                new PresetAssignment("_EnchantedContrast", 0.72f),
                new PresetAssignment("_EnchantedLowColor", new Color(0.45f, 0.05f, 1.8f, 1f)),
                new PresetAssignment("_EnchantedHighColor", new Color(0.02f, 1.7f, 2.8f, 1f))),
            new CompositePreset(
                "ui.shifting", "UI 彩虹流变", "随明度连续流变的彩虹色，用于限时、传奇或可升级状态。", "ES/UI/Composite URP",
                new PresetAssignment("_EnableShifting", 1f),
                new PresetAssignment("_ShiftingFade", 0.68f),
                new PresetAssignment("_ShiftingSpeed", 0.38f),
                new PresetAssignment("_ShiftingDensity", 1.35f),
                new PresetAssignment("_ShiftingBrightness", 1.1f),
                new PresetAssignment("_ShiftingContrast", 0.65f),
                new PresetAssignment("_ShiftingRainbowToggle", 1f),
                new PresetAssignment("_ShiftingSaturation", 0.82f)),
            new CompositePreset(
                "ui.hologram", "UI 全息故障", "扫描线与轻微故障组合，用于终端、投影和科技感界面。", "ES/UI/Composite URP",
                new PresetAssignment("_EnableHologram", 1f),
                new PresetAssignment("_HologramColor", new Color(0.05f, 0.9f, 1.8f, 1f)),
                new PresetAssignment("_HologramFrequency", 64f),
                new PresetAssignment("_HologramSpeed", 1.25f),
                new PresetAssignment("_EnableGlitch", 1f),
                new PresetAssignment("_GlitchAmount", 0.018f),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "vfx.energy-flow", "能量流动", "沿 UV 持续流动并叠加青色边缘光，适合能量管线与技能轨迹。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_EnableFlow", 1f),
                new PresetAssignment("_FlowSpeed", new Vector4(0f, -0.65f, 0f, 0f)),
                new PresetAssignment("_FlowStrength", 0.85f),
                new PresetAssignment("_EnableRim", 1f),
                new PresetAssignment("_RimColor", new Color(0.05f, 0.75f, 1.8f, 1f)),
                new PresetAssignment("_RimPower", 2.5f),
                new PresetAssignment("_RimIntensity", 1.8f),
                new PresetAssignment("_EmissionColor", new Color(0.02f, 0.35f, 0.8f, 1f)),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "vfx.shockwave", "冲击波", "把纹理转换为极坐标并使用径向遮罩塑造扩散圆环。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_EnablePolarUV", 1f),
                new PresetAssignment("_PolarCenter", new Vector4(0.5f, 0.5f, 0f, 0f)),
                new PresetAssignment("_PolarRadialScale", 1.2f),
                new PresetAssignment("_PolarAngularScale", 1f),
                new PresetAssignment("_EnableRadialMask", 1f),
                new PresetAssignment("_RadialMaskCenter", new Vector4(0.5f, 0.5f, 0f, 0f)),
                new PresetAssignment("_RadialMaskRadius", 0.55f),
                new PresetAssignment("_RadialMaskSoftness", 0.08f),
                new PresetAssignment("_BlendMode", 1f),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "vfx.portal", "传送门", "旋转极坐标、流动和高亮边缘组合，适合门扉与空间裂隙。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_EnablePolarUV", 1f),
                new PresetAssignment("_PolarRotationSpeed", 0.18f),
                new PresetAssignment("_EnableFlow", 1f),
                new PresetAssignment("_FlowSpeed", new Vector4(0.15f, -0.45f, 0f, 0f)),
                new PresetAssignment("_EnableRim", 1f),
                new PresetAssignment("_RimColor", new Color(0.35f, 0.05f, 2.1f, 1f)),
                new PresetAssignment("_RimIntensity", 2.4f),
                new PresetAssignment("_EnableFresnelMask", 1f),
                new PresetAssignment("_FresnelPower", 1.7f),
                new PresetAssignment("_FresnelIntensity", 1.2f),
                new PresetAssignment("_BlendMode", 1f),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "vfx.shield", "护盾边缘", "用菲涅尔与深度交界强调护盾轮廓和接触区域。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_EnableRim", 1f),
                new PresetAssignment("_RimColor", new Color(0.05f, 0.85f, 2.2f, 1f)),
                new PresetAssignment("_RimPower", 3.8f),
                new PresetAssignment("_RimIntensity", 2.1f),
                new PresetAssignment("_EnableFresnelMask", 1f),
                new PresetAssignment("_FresnelPower", 2.8f),
                new PresetAssignment("_FresnelAlphaInfluence", 0.85f),
                new PresetAssignment("_EnableDepthIntersection", 1f),
                new PresetAssignment("_DepthIntersectionColor", new Color(0.1f, 0.9f, 2.4f, 1f)),
                new PresetAssignment("_DepthIntersectionDistance", 0.18f),
                new PresetAssignment("_DepthIntersectionIntensity", 2f),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "vfx.dissolve", "溶解消散", "带高亮边缘的噪声溶解，保留进度给动画或业务代码控制。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_DissolveMode", 2f),
                new PresetAssignment("_DissolveProgress", 0.45f),
                new PresetAssignment("_DissolveWidth", 0.09f),
                new PresetAssignment("_DissolveColor", new Color(2.4f, 0.22f, 0.015f, 1f)),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "vfx.hologram", "全息故障", "高质量扫描线、轻微故障与色差组合，适合投影和数字替身。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_ESNativeStatusContract", 1f),
                new PresetAssignment("_EnableHologram", 1f),
                new PresetAssignment("_HologramColor", new Color(0.05f, 1.1f, 2.2f, 1f)),
                new PresetAssignment("_HologramFade", 0.9f),
                new PresetAssignment("_HologramContrast", 1.2f),
                new PresetAssignment("_HologramSpace", 1f),
                new PresetAssignment("_HologramLineFrequency", 72f),
                new PresetAssignment("_HologramLineGap", 3f),
                new PresetAssignment("_HologramSpeed", 1.4f),
                new PresetAssignment("_HologramMinAlpha", 0.2f),
                new PresetAssignment("_HologramDistortionOffset", 0.18f),
                new PresetAssignment("_HologramDistortionSpeed", 2f),
                new PresetAssignment("_HologramDistortionDensity", 0.5f),
                new PresetAssignment("_HologramDistortionScale", 10f),
                new PresetAssignment("_EnableGlitch", 1f),
                new PresetAssignment("_GlitchFade", 0.65f),
                new PresetAssignment("_GlitchMaskMin", 0.4f),
                new PresetAssignment("_GlitchMaskScale", new Vector4(0f, 0.2f, 0f, 0f)),
                new PresetAssignment("_GlitchMaskSpeed", new Vector4(0f, 4f, 0f, 0f)),
                new PresetAssignment("_GlitchHueSpeed", 0.35f),
                new PresetAssignment("_GlitchBrightness", 2f),
                new PresetAssignment("_GlitchNoiseScale", new Vector4(0f, 3f, 0f, 0f)),
                new PresetAssignment("_GlitchNoiseSpeed", new Vector4(0f, 1f, 0f, 0f)),
                new PresetAssignment("_GlitchDistortion", new Vector4(0.018f, 0f, 0f, 0f)),
                new PresetAssignment("_GlitchDistortionScale", new Vector4(0f, 3f, 0f, 0f)),
                new PresetAssignment("_GlitchDistortionSpeed", new Vector4(0f, 3.5f, 0f, 0f)),
                new PresetAssignment("_EnableChromatic", 1f),
                new PresetAssignment("_ChromaticOffset", 0.0025f),
                new PresetAssignment("_ChromaticIntensity", 0.7f),
                new PresetAssignment("_BlendMode", 1f),
                new PresetAssignment("_QualityTier", 2f))
        };

        private static readonly Dictionary<string, CompositePreset[]> PresetCache = new Dictionary<string, CompositePreset[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string[]> PresetNameCache = new Dictionary<string, string[]>(StringComparer.Ordinal);
        #endregion

        #region View And Preset Workflow

        private static InspectorViewLevel DrawInspectorViewMode(string shaderName)
        {
            string key = "ES.Composite.ViewLevel." + shaderName;
            InspectorViewLevel current = (InspectorViewLevel)Mathf.Clamp(SessionState.GetInt(key, 0), 0, 2);

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("显示范围", ESEditorPresentation.HeaderStyle, GUILayout.Width(72f));
            int next = GUILayout.Toolbar((int)current, ViewModeNames, EditorStyles.miniButton);
            EditorGUILayout.EndHorizontal();
            if (next != (int)current)
            {
                current = (InspectorViewLevel)next;
                SessionState.SetInt(key, next);
            }

            string guidance = current == InspectorViewLevel.Standard
                ? "常用参数"
                : current == InspectorViewLevel.Advanced
                    ? "坐标、遮罩与深度"
                    : "顶点流与渲染状态";
            GUILayout.Label(guidance + " · 仅改变显示，不修改材质", ESEditorPresentation.SubtitleStyle);
            return current;
        }

        private static bool PropertyPassesViewLevel(MaterialProperty property, string filter, InspectorViewLevel level)
        {
            if (property == null) return false;
            // 主动搜索或效果导航等同于用户明确寻找目标，临时越级展示但不改变当前模式。
            if (!string.IsNullOrEmpty(filter)) return true;
            return ResolveInspectorViewLevel(property.name) <= level;
        }

        private static InspectorViewLevel ResolveInspectorViewLevel(string propertyName)
        {
            if (ExpertViewProperties.Contains(propertyName)) return InspectorViewLevel.Expert;
            if (AdvancedViewProperties.Contains(propertyName)) return InspectorViewLevel.Advanced;
            return InspectorViewLevel.Standard;
        }

        private static int GetMaterialPropertyValueSignature(MaterialProperty[] properties)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < properties.Length; i++)
                {
                    MaterialProperty property = properties[i];
                    if (property == null) continue;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(property.name);
                    hash = hash * 31 + (property.hasMixedValue ? 1 : 0);
                    switch (property.type)
                    {
                        case MaterialProperty.PropType.Color:
                            hash = hash * 31 + property.colorValue.GetHashCode();
                            break;
                        case MaterialProperty.PropType.Vector:
                            hash = hash * 31 + property.vectorValue.GetHashCode();
                            break;
                        case MaterialProperty.PropType.Texture:
                            hash = hash * 31 + (property.textureValue == null ? 0 : property.textureValue.GetInstanceID());
                            hash = hash * 31 + property.textureScaleAndOffset.GetHashCode();
                            break;
                        default:
                            hash = hash * 31 + property.floatValue.GetHashCode();
                            break;
                    }
                }
                return hash;
            }
        }

        private static void DrawPresetPanel(MaterialEditor editor, MaterialProperty[] properties, string shaderName)
        {
            DrawSharedPresetPanel(editor, shaderName);
            CompositePreset[] presets = GetPresets(shaderName);
            if (presets.Length == 0) return;

            string selectedKey = "ES.Composite.Preset.Selected." + shaderName;
            int selected = Mathf.Clamp(SessionState.GetInt(selectedKey, 0), 0, presets.Length - 1);
            string[] names = GetPresetNames(shaderName, presets);
            CompositePreset preset = presets[selected];
            string panelKey = "ES.Composite.Preset.Panel." + shaderName;
            bool expanded = SessionState.GetBool(panelKey, false);

            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            bool nextExpanded = EditorGUILayout.Foldout(expanded, "效果预设 · " + preset.Name, true);
            if (nextExpanded != expanded)
            {
                expanded = nextExpanded;
                SessionState.SetBool(panelKey, expanded);
            }
            if (!expanded)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("选择预设", ESEditorPresentation.HeaderStyle, GUILayout.MinWidth(56f), GUILayout.ExpandWidth(false));
            int next = EditorGUILayout.Popup(selected, names);
            EditorGUILayout.EndHorizontal();
            if (next != selected)
            {
                selected = next;
                SessionState.SetInt(selectedKey, selected);
            }

            preset = presets[selected];
            GUILayout.Label(preset.Description, ESEditorPresentation.SubtitleStyle);
            string foldoutKey = "ES.Composite.Preset.Preview." + shaderName;
            bool previewExpanded = SessionState.GetBool(foldoutKey, false);
            bool nextPreviewExpanded = EditorGUILayout.Foldout(previewExpanded, "预览并选择要应用的差异", true);
            if (nextPreviewExpanded != previewExpanded)
            {
                previewExpanded = nextPreviewExpanded;
                SessionState.SetBool(foldoutKey, previewExpanded);
            }

            int differenceCount = 0;
            int selectedCount = 0;
            if (previewExpanded)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (DrawContentSizedButton(SelectPresetDifferencesButtonContent, EditorStyles.miniButton))
                    SetPresetSelections(editor, preset, true, true);
                if (DrawContentSizedButton(CancelPresetSelectionButtonContent, EditorStyles.miniButton))
                    SetPresetSelections(editor, preset, false, false);
                EditorGUILayout.EndHorizontal();

                bool narrowComparison = EditorGUIUtility.currentViewWidth < 330f;
                for (int i = 0; i < preset.Assignments.Length; i++)
                {
                    PresetAssignment assignment = preset.Assignments[i];
                    bool different = IsDifferentForAnyTarget(editor, assignment);
                    if (different) differenceCount++;
                    string selectionKey = GetPresetSelectionKey(shaderName, preset.Id, assignment.PropertyName);
                    bool apply = different && SessionState.GetBool(selectionKey, true);
                    string displayName = GetPresetPropertyDisplayName(properties, assignment.PropertyName);
                    string comparison = FormatCurrentValue(editor, assignment) + "  →  " + assignment.FormatTarget();
                    bool nextApply;
                    if (narrowComparison)
                    {
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal();
                        nextApply = EditorGUILayout.Toggle(apply, GUILayout.Width(18f));
                        GUILayout.Label(displayName);
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Label(different ? comparison : "已一致", ESEditorPresentation.SubtitleStyle);
                        EditorGUILayout.EndVertical();
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        nextApply = EditorGUILayout.Toggle(apply, GUILayout.Width(18f));
                        GUILayout.Label(displayName, GUILayout.Width(Mathf.Clamp(EditorGUIUtility.currentViewWidth * 0.28f, 92f, 180f)));
                        GUILayout.Label(different ? comparison : "已一致", different ? EditorStyles.miniLabel : ESEditorPresentation.MetaStyle);
                        EditorGUILayout.EndHorizontal();
                    }
                    if (nextApply != apply)
                    {
                        apply = nextApply;
                        SessionState.SetBool(selectionKey, apply);
                    }
                    if (apply) selectedCount++;
                }
            }
            else
            {
                for (int i = 0; i < preset.Assignments.Length; i++)
                {
                    PresetAssignment assignment = preset.Assignments[i];
                    bool different = IsDifferentForAnyTarget(editor, assignment);
                    if (different) differenceCount++;
                    if (different && SessionState.GetBool(GetPresetSelectionKey(shaderName, preset.Id, assignment.PropertyName), true)) selectedCount++;
                }
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("差异 " + differenceCount + " 项 · 已选择 " + selectedCount + " 项", ESEditorPresentation.MetaStyle);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(selectedCount == 0))
            {
                if (DrawContentSizedButton(ApplyPresetSelectionButtonContent))
                    ApplyPreset(editor, preset);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static void DrawSharedPresetPanel(MaterialEditor editor, string shaderName)
        {
            string sessionKey = "ES.Composite.SharedPreset." + shaderName;
            string assetGuid = SessionState.GetString(sessionKey, string.Empty);
            ESCompositeShaderPreset preset = string.IsNullOrEmpty(assetGuid)
                ? null
                : AssetDatabase.LoadAssetAtPath<ESCompositeShaderPreset>(AssetDatabase.GUIDToAssetPath(assetGuid));

            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            EditorGUILayout.LabelField("共享预设资产", EditorStyles.boldLabel);
            ESCompositeShaderPreset next = (ESCompositeShaderPreset)EditorGUILayout.ObjectField(
                "预设",
                preset,
                typeof(ESCompositeShaderPreset),
                false);
            if (next != preset)
            {
                preset = next;
                string path = preset == null ? string.Empty : AssetDatabase.GetAssetPath(preset);
                SessionState.SetString(sessionKey, string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path));
            }

            Material source = editor.target as Material;
            bool compatible = preset != null && source != null && preset.IsCompatible(source);
            if (preset != null && !compatible)
                EditorGUILayout.HelpBox("该预设属于 " + preset.ShaderName + "，与当前 Shader 不兼容。", MessageType.Warning);
            else if (preset != null && !string.IsNullOrWhiteSpace(preset.Description))
                GUILayout.Label(preset.Description, ESEditorPresentation.SubtitleStyle);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("新建并捕获"))
                preset = CreateSharedPreset(source, sessionKey);
            using (new EditorGUI.DisabledScope(!compatible))
            {
                if (GUILayout.Button("重新捕获"))
                {
                    Undo.RecordObject(preset, "捕获 ES Composite Shader 预设");
                    preset.CaptureFrom(source);
                    EditorUtility.SetDirty(preset);
                }
                if (GUILayout.Button("应用到所选"))
                {
                    var materials = new List<Material>();
                    for (int i = 0; i < editor.targets.Length; i++)
                    {
                        Material material = editor.targets[i] as Material;
                        if (preset.IsCompatible(material)) materials.Add(material);
                    }
                    Undo.RecordObjects(materials.ToArray(), "应用 ES Composite Shader 共享预设");
                    for (int i = 0; i < materials.Count; i++) preset.ApplyTo(materials[i]);
                    editor.PropertiesChanged();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static ESCompositeShaderPreset CreateSharedPreset(Material source, string sessionKey)
        {
            if (source == null) return null;
            string path = EditorUtility.SaveFilePanelInProject(
                "新建 ES Composite Shader 预设",
                source.name + " Preset",
                "asset",
                "选择共享预设资产的保存位置。");
            if (string.IsNullOrEmpty(path)) return null;

            var preset = ScriptableObject.CreateInstance<ESCompositeShaderPreset>();
            preset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            preset.CaptureFrom(source);
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssetIfDirty(preset);
            SessionState.SetString(sessionKey, AssetDatabase.AssetPathToGUID(path));
            Selection.activeObject = preset;
            return preset;
        }

        private static CompositePreset[] GetPresets(string shaderName)
        {
            if (PresetCache.TryGetValue(shaderName, out CompositePreset[] cached)) return cached;
            var result = new List<CompositePreset>();
            for (int i = 0; i < BuiltInPresets.Length; i++)
                if (string.Equals(BuiltInPresets[i].ShaderName, shaderName, StringComparison.Ordinal))
                    result.Add(BuiltInPresets[i]);
            CompositePreset[] presets = result.ToArray();
            PresetCache[shaderName] = presets;
            return presets;
        }

        private static string[] GetPresetNames(string shaderName, CompositePreset[] presets)
        {
            if (PresetNameCache.TryGetValue(shaderName, out string[] cached)) return cached;
            string[] names = new string[presets.Length];
            for (int i = 0; i < presets.Length; i++) names[i] = presets[i].Name;
            PresetNameCache[shaderName] = names;
            return names;
        }

        private static string GetPresetSelectionKey(string shaderName, string presetId, string propertyName)
        {
            return "ES.Composite.Preset.Apply." + shaderName + "." + presetId + "." + propertyName;
        }

        private static void SetPresetSelections(MaterialEditor editor, CompositePreset preset, bool selected, bool differencesOnly)
        {
            for (int i = 0; i < preset.Assignments.Length; i++)
            {
                PresetAssignment assignment = preset.Assignments[i];
                bool value = selected && (!differencesOnly || IsDifferentForAnyTarget(editor, assignment));
                SessionState.SetBool(GetPresetSelectionKey(preset.ShaderName, preset.Id, assignment.PropertyName), value);
            }
        }

        private static bool IsDifferentForAnyTarget(MaterialEditor editor, PresetAssignment assignment)
        {
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material != null && material.HasProperty(assignment.PropertyName) && assignment.IsDifferent(material)) return true;
            }
            return false;
        }

        private static string FormatCurrentValue(MaterialEditor editor, PresetAssignment assignment)
        {
            Material first = null;
            bool mixed = false;
            string firstValue = null;
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material == null || !material.HasProperty(assignment.PropertyName)) continue;
                string value = FormatMaterialValue(material, assignment);
                if (first == null)
                {
                    first = material;
                    firstValue = value;
                }
                else if (!string.Equals(firstValue, value, StringComparison.Ordinal))
                {
                    mixed = true;
                    break;
                }
            }
            if (first == null) return "不支持";
            return mixed ? "多值" : firstValue;
        }

        private static string FormatMaterialValue(Material material, PresetAssignment assignment)
        {
            switch (assignment.Kind)
            {
                case PresetValueKind.Color:
                    Color color = material.GetColor(assignment.PropertyName);
                    return string.Format("RGBA({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##})", color.r, color.g, color.b, color.a);
                case PresetValueKind.Vector:
                    Vector4 value = material.GetVector(assignment.PropertyName);
                    return string.Format("({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##})", value.x, value.y, value.z, value.w);
                default:
                    return material.GetFloat(assignment.PropertyName).ToString("0.###");
            }
        }

        private static string GetPresetPropertyDisplayName(MaterialProperty[] properties, string propertyName)
        {
            MaterialProperty property = Find(properties, propertyName);
            return property == null ? propertyName : GetDisplayName(property);
        }

        private static void ApplyPreset(MaterialEditor editor, CompositePreset preset)
        {
            var selected = new List<PresetAssignment>();
            for (int i = 0; i < preset.Assignments.Length; i++)
            {
                PresetAssignment assignment = preset.Assignments[i];
                bool different = IsDifferentForAnyTarget(editor, assignment);
                if (different && SessionState.GetBool(GetPresetSelectionKey(preset.ShaderName, preset.Id, assignment.PropertyName), true))
                    selected.Add(assignment);
            }
            if (selected.Count == 0) return;

            Undo.RecordObjects(editor.targets, "应用 ES Shader 预设：" + preset.Name);
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material == null || material.shader == null || material.shader.name != preset.ShaderName) continue;
                for (int p = 0; p < selected.Count; p++) selected[p].Apply(material);
                SyncMaterialKeywords(material);
                EditorUtility.SetDirty(material);
            }
            for (int i = 0; i < selected.Count; i++)
                SessionState.EraseBool(GetPresetSelectionKey(preset.ShaderName, preset.Id, selected[i].PropertyName));
            editor.PropertiesChanged();
        }

        #endregion
    }
}
