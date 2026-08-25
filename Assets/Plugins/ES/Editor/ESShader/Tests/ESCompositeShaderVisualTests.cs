using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ES;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ES.Tests
{
    public sealed partial class ESCompositeShaderVisualTests
    {
        private const int PreviewSize = 256;
        private const int BackgroundColorTolerance = 12;
        private const float MinimumVisibleCoverage = 0.2f;
        private const float MinimumEffectDifference = 0.008f;
        private const float MinimumRepresentativeEffectDifference = 0.004f;
        private const float MinimumCoordinateDifference = 0.004f;
        private const float MaximumDisabledEffectDifference = 0.0005f;
        private static readonly Color PreviewBackground = Color.clear;

        private static readonly (string Key, string ShaderName)[] ShaderFamilies =
        {
            ("2D", "ES/2D/Composite URP"),
            ("UI", "ES/UI/Composite URP"),
            ("Lit", "ES/3D/Lit Composite URP"),
            ("VFX", "ES/3D/VFX Composite URP")
        };

        private static readonly string[] SharedRepresentativeEffects =
        {
            "Flow",
            "UVTransform",
            "FullDistortion",
            "Pixelate",
            "Chromatic",
            "Blur",
            "Negative",
            "Checkerboard",
            "FullAlphaDissolve",
            "OuterOutline",
            "TextureLayer1",
            "RecolorRGB",
            "SineMove"
        };

        private static readonly string[] VfxRepresentativeEffects =
        {
            "Flow",
            "Sequence",
            "PolarUV",
            "Chromatic",
            "Blur",
            "RadialMask",
            "Shine",
            "Sparkle",
            "VertexAnimation",
            "Dissolve"
        };

        [Test]
        public void ShaderFamilies_RenderDistinctHologramGlitchAndCombinedResults()
        {
            RequireGraphicsDevice();
            string outputDirectory = PrepareOutputDirectory();
            Texture2D baseTexture = CreateBaseTexture();
            Texture2D noiseTexture = CreateNoiseTexture();
            var report = new StringBuilder();
            report.AppendLine("ES Composite Shader visual evidence");
            report.AppendLine("GraphicsDevice=" + SystemInfo.graphicsDeviceType);

            try
            {
                foreach ((string key, string shaderName) in ShaderFamilies)
                {
                    Shader shader = Shader.Find(shaderName);
                    Assert.That(shader, Is.Not.Null, shaderName + " must be imported before visual validation.");

                    Material baseline = CreatePreparedMaterial(shader, baseTexture, noiseTexture);
                    Material hologram = new Material(baseline) { hideFlags = HideFlags.HideAndDontSave };
                    Material glitch = new Material(baseline) { hideFlags = HideFlags.HideAndDontSave };
                    Material combined = new Material(baseline) { hideFlags = HideFlags.HideAndDontSave };
                    Texture2D baselineImage = null;
                    Texture2D hologramImage = null;
                    Texture2D glitchImage = null;
                    Texture2D combinedImage = null;

                    try
                    {
                        ConfigureExactEffects(hologram, true, false, 0f);
                        ConfigureExactEffects(glitch, false, true, 0f);
                        ConfigureExactEffects(combined, true, true, 0f);

                        baselineImage = RenderMaterial(baseline, 0f, true);
                        hologramImage = RenderMaterial(hologram, 0f, true);
                        glitchImage = RenderMaterial(glitch, 0f, true);
                        combinedImage = RenderMaterial(combined, 0f, true);

                        float coverage = VisibleCoverage(baselineImage);
                        float hologramDifference = MeanAbsoluteDifference(baselineImage, hologramImage);
                        float glitchDifference = MeanAbsoluteDifference(baselineImage, glitchImage);
                        float combinedVsHologram = MeanAbsoluteDifference(hologramImage, combinedImage);
                        float combinedVsGlitch = MeanAbsoluteDifference(glitchImage, combinedImage);

                        SaveImage(outputDirectory, key + "_00_Baseline.png", baselineImage);
                        SaveImage(outputDirectory, key + "_01_Hologram.png", hologramImage);
                        SaveImage(outputDirectory, key + "_02_Glitch.png", glitchImage);
                        SaveImage(outputDirectory, key + "_03_Combined.png", combinedImage);

                        report.AppendLine(string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "{0}: coverage={1:F6}, hologram={2:F6}, glitch={3:F6}, combinedVsHologram={4:F6}, combinedVsGlitch={5:F6}",
                            key,
                            coverage,
                            hologramDifference,
                            glitchDifference,
                            combinedVsHologram,
                            combinedVsGlitch));

                        Assert.That(coverage, Is.GreaterThan(MinimumVisibleCoverage), key + " baseline must render visible pixels.");
                        Assert.That(hologramDifference, Is.GreaterThan(MinimumEffectDifference), key + " hologram must change rendered pixels.");
                        Assert.That(glitchDifference, Is.GreaterThan(MinimumEffectDifference), key + " glitch must change rendered pixels.");
                        Assert.That(combinedVsHologram, Is.GreaterThan(MinimumEffectDifference), key + " combined result must retain an active glitch contribution.");
                        Assert.That(combinedVsGlitch, Is.GreaterThan(MinimumEffectDifference), key + " combined result must retain an active hologram contribution.");
                    }
                    finally
                    {
                        DestroyImmediateSafe(baselineImage);
                        DestroyImmediateSafe(hologramImage);
                        DestroyImmediateSafe(glitchImage);
                        DestroyImmediateSafe(combinedImage);
                        DestroyImmediateSafe(baseline);
                        DestroyImmediateSafe(hologram);
                        DestroyImmediateSafe(glitch);
                        DestroyImmediateSafe(combined);
                    }
                }
            }
            finally
            {
                File.WriteAllText(
                    Path.Combine(outputDirectory, "visual-report.txt"),
                    report.ToString(),
                    new UTF8Encoding(false));
                DestroyImmediateSafe(baseTexture);
                DestroyImmediateSafe(noiseTexture);
            }
        }

        [TestCase("2D", "ES/2D/Composite URP")]
        [TestCase("UI", "ES/UI/Composite URP")]
        public void SpriteAndUI_HologramSpaceAndProjectionAffectRealPixels(
            string key,
            string shaderName)
        {
            RequireGraphicsDevice();
            string outputDirectory = PrepareOutputDirectory();
            Texture2D baseTexture = CreateBaseTexture();
            Texture2D noiseTexture = CreateNoiseTexture();
            Shader shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null);

            Material localMaterial = CreatePreparedMaterial(shader, baseTexture, noiseTexture);
            Material worldMaterial = new Material(localMaterial) { hideFlags = HideFlags.HideAndDontSave };
            ConfigureExactEffects(localMaterial, true, false, 0f);
            ConfigureExactEffects(worldMaterial, true, false, 1f);

            Texture2D localLow = null;
            Texture2D localHigh = null;
            Texture2D worldLow = null;
            Texture2D worldHigh = null;
            Texture2D worldPerspective = null;
            try
            {
                localLow = RenderMaterial(localMaterial, 0f, true);
                localHigh = RenderMaterial(localMaterial, 2.25f, true);
                worldLow = RenderMaterial(worldMaterial, 0f, true);
                worldHigh = RenderMaterial(worldMaterial, 2.25f, true);
                worldPerspective = RenderMaterial(worldMaterial, 2.25f, false);

                float localHeightDifference = MeanAbsoluteDifference(localLow, localHigh);
                float worldHeightDifference = MeanAbsoluteDifference(worldLow, worldHigh);
                float projectionDifference = MeanAbsoluteDifference(worldHigh, worldPerspective);

                TestContext.WriteLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}: localHeight={1:F6}, worldHeight={2:F6}, projection={3:F6}",
                    key,
                    localHeightDifference,
                    worldHeightDifference,
                    projectionDifference));

                SaveImage(outputDirectory, key + "_10_Local_Low.png", localLow);
                SaveImage(outputDirectory, key + "_11_Local_High.png", localHigh);
                SaveImage(outputDirectory, key + "_12_World_Low.png", worldLow);
                SaveImage(outputDirectory, key + "_13_World_High.png", worldHigh);
                SaveImage(outputDirectory, key + "_14_World_Perspective.png", worldPerspective);

                Assert.That(localHeightDifference, Is.LessThan(0.002f), key + " LocalUV hologram must not drift with world height.");
                Assert.That(worldHeightDifference, Is.GreaterThan(MinimumCoordinateDifference), key + " WorldHeight hologram must react to object world height.");
                Assert.That(projectionDifference, Is.GreaterThan(0.002f), key + " world hologram must exercise the orthographic/perspective branch.");
            }
            finally
            {
                DestroyImmediateSafe(localLow);
                DestroyImmediateSafe(localHigh);
                DestroyImmediateSafe(worldLow);
                DestroyImmediateSafe(worldHigh);
                DestroyImmediateSafe(worldPerspective);
                DestroyImmediateSafe(localMaterial);
                DestroyImmediateSafe(worldMaterial);
                DestroyImmediateSafe(baseTexture);
                DestroyImmediateSafe(noiseTexture);
            }
        }

        [TestCase("2D", "ES/2D/Composite URP")]
        [TestCase("UI", "ES/UI/Composite URP")]
        [TestCase("Lit", "ES/3D/Lit Composite URP")]
        [TestCase("VFX", "ES/3D/VFX Composite URP")]
        public void ShaderFamilies_RepresentativeEffectsRespectParentsAndChangePixels(
            string key,
            string shaderName)
        {
            RequireGraphicsDevice();
            string outputDirectory = PrepareOutputDirectory();
            Texture2D baseTexture = CreateBaseTexture();
            Texture2D noiseTexture = CreateNoiseTexture();
            Shader shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null);

            Material baseline = CreatePreparedMaterial(shader, baseTexture, noiseTexture);
            Texture2D baselineImage = null;
            var report = new StringBuilder();
            var failures = new List<string>();
            string[] effects = key == "VFX"
                ? VfxRepresentativeEffects
                : SharedRepresentativeEffects;

            report.AppendLine(key + " representative effect evidence");
            report.AppendLine("GraphicsDevice=" + SystemInfo.graphicsDeviceType);
            try
            {
                baselineImage = RenderMaterial(baseline, 0f, true);
                foreach (string effect in effects)
                {
                    Material prerequisite = null;
                    Texture2D prerequisiteImage = null;
                    Material reference = baseline;
                    Texture2D referenceImage = baselineImage;
                    if (key == "Lit" && effect == "FullAlphaDissolve")
                    {
                        prerequisite = new Material(baseline) { hideFlags = HideFlags.HideAndDontSave };
                        ES3DLitCompositeURPProperties.SetSurfaceMode(
                            prerequisite,
                            ES3DLitSurfaceMode.透明混合);
                        prerequisiteImage = RenderMaterial(prerequisite, 0f, true);
                        reference = prerequisite;
                        referenceImage = prerequisiteImage;
                    }

                    Material disabled = new Material(reference) { hideFlags = HideFlags.HideAndDontSave };
                    Material enabled = new Material(reference) { hideFlags = HideFlags.HideAndDontSave };
                    Texture2D disabledImage = null;
                    Texture2D enabledImage = null;
                    try
                    {
                        ConfigureRepresentativeEffect(disabled, effect, false, noiseTexture);
                        ConfigureRepresentativeEffect(enabled, effect, true, noiseTexture);
                        disabledImage = RenderMaterial(disabled, 0f, true);
                        enabledImage = RenderMaterial(enabled, 0f, true);

                        float disabledDifference = MeanAbsoluteDifference(referenceImage, disabledImage);
                        float enabledDifference = MeanAbsoluteDifference(disabledImage, enabledImage);
                        report.AppendLine(string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "{0}: disabled={1:F6}, enabled={2:F6}",
                            effect,
                            disabledDifference,
                            enabledDifference));
                        SaveImage(outputDirectory, key + "_20_" + effect + ".png", enabledImage);

                        if (disabledDifference > MaximumDisabledEffectDifference)
                        {
                            failures.Add(string.Format(
                                System.Globalization.CultureInfo.InvariantCulture,
                                "{0}/{1}: child parameters changed pixels while the parent was disabled ({2:F6}).",
                                key,
                                effect,
                                disabledDifference));
                        }
                        if (enabledDifference <= MinimumRepresentativeEffectDifference)
                        {
                            failures.Add(string.Format(
                                System.Globalization.CultureInfo.InvariantCulture,
                                "{0}/{1}: enabling the parent did not materially change pixels ({2:F6}).",
                                key,
                                effect,
                                enabledDifference));
                        }
                    }
                    catch (Exception exception)
                    {
                        failures.Add(key + "/" + effect + ": " + exception.GetType().Name + ": " + exception.Message);
                        report.AppendLine(effect + ": ERROR " + exception);
                    }
                    finally
                    {
                        DestroyImmediateSafe(disabledImage);
                        DestroyImmediateSafe(enabledImage);
                        DestroyImmediateSafe(disabled);
                        DestroyImmediateSafe(enabled);
                        DestroyImmediateSafe(prerequisiteImage);
                        DestroyImmediateSafe(prerequisite);
                    }
                }
            }
            finally
            {
                File.WriteAllText(
                    Path.Combine(outputDirectory, key + "-representative-effects-report.txt"),
                    report.ToString(),
                    new UTF8Encoding(false));
                DestroyImmediateSafe(baselineImage);
                DestroyImmediateSafe(baseline);
                DestroyImmediateSafe(baseTexture);
                DestroyImmediateSafe(noiseTexture);
            }

            if (failures.Count > 0)
                Assert.Fail(string.Join(Environment.NewLine, failures));
        }

        private static Material CreatePreparedMaterial(
            Shader shader,
            Texture2D baseTexture,
            Texture2D noiseTexture)
        {
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            SetTextureIfPresent(material, "_MainTex", baseTexture);
            SetTextureIfPresent(material, "_BaseMap", baseTexture);
            SetTextureIfPresent(material, "_UberNoiseTexture", noiseTexture);
            SetTextureIfPresent(material, "_NoiseTex", noiseTexture);
            SetTextureIfPresent(material, "_FlowMap", noiseTexture);
            SetColorIfPresent(material, "_Color", Color.white);
            SetColorIfPresent(material, "_BaseColor", Color.white);
            SetColorIfPresent(material, "_RendererColor", Color.white);
            SetFloatIfPresent(material, "_TimeMode", 2f);
            SetFloatIfPresent(material, "_CustomTime", 0.37f);
            SetFloatIfPresent(material, "_TimeScale", 1f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_UseUIAlphaClip", 0f);
            SetFloatIfPresent(material, "_ColorMask", 15f);
            SetVectorIfPresent(material, "_SpriteUVRect", new Vector4(0f, 0f, 1f, 1f));
            SetFloatIfPresent(material, "_SpriteUVTransformValid", 0f);

            switch (shader.name)
            {
                case "ES/2D/Composite URP":
                    Assert.That(ES2DCompositeURPProperties.PrepareMaterialForDynamicESNative(material), Is.True);
                    break;
                case "ES/UI/Composite URP":
                    Assert.That(ESUICompositeURPProperties.PrepareMaterialForDynamicESNative(material), Is.True);
                    break;
                case "ES/3D/Lit Composite URP":
                    Assert.That(ES3DLitCompositeURPProperties.PrepareMaterialForDynamicESNative(material), Is.True);
                    SetFloatIfPresent(material, "_Metallic", 0f);
                    SetFloatIfPresent(material, "_Smoothness", 0.25f);
                    break;
                case "ES/3D/VFX Composite URP":
                    Assert.That(ES3DVFXCompositeURPProperties.PrepareMaterialForDynamicESNative(material), Is.True);
                    break;
                default:
                    Assert.Fail("Unsupported visual validation shader: " + shader.name);
                    break;
            }

            ConfigureExactEffects(material, false, false, 0f);
            return material;
        }

        private static void ConfigureExactEffects(
            Material material,
            bool hologram,
            bool glitch,
            float hologramSpace)
        {
            SetFloatIfPresent(material, "_ESNativeStatusContract", 1f);
            SetFloatIfPresent(material, "_EnableHologram", hologram ? 1f : 0f);
            SetFloatIfPresent(material, "_HologramFade", hologram ? 1f : 0f);
            SetColorIfPresent(material, "_HologramColor", new Color(0.08f, 0.9f, 1.8f, 1f));
            SetFloatIfPresent(material, "_HologramContrast", 1.35f);
            SetFloatIfPresent(material, "_HologramSpace", hologramSpace);
            SetFloatIfPresent(material, "_HologramLineFrequency", 22f);
            SetFloatIfPresent(material, "_HologramLineGap", 0.7f);
            SetFloatIfPresent(material, "_HologramSpeed", 0.8f);
            SetFloatIfPresent(material, "_HologramMinAlpha", 0.18f);
            SetFloatIfPresent(material, "_HologramDistortionOffset", 0.09f);
            SetFloatIfPresent(material, "_HologramDistortionSpeed", 1.4f);
            SetFloatIfPresent(material, "_HologramDistortionDensity", 1.7f);
            SetFloatIfPresent(material, "_HologramDistortionScale", 7f);

            SetFloatIfPresent(material, "_EnableGlitch", glitch ? 1f : 0f);
            SetFloatIfPresent(material, "_GlitchFade", glitch ? 1f : 0f);
            SetFloatIfPresent(material, "_GlitchMaskMin", 0.82f);
            SetVectorIfPresent(material, "_GlitchMaskScale", new Vector4(2f, 5f, 0f, 0f));
            SetVectorIfPresent(material, "_GlitchMaskSpeed", new Vector4(0.3f, 1.2f, 0f, 0f));
            SetFloatIfPresent(material, "_GlitchHueSpeed", 0.65f);
            SetFloatIfPresent(material, "_GlitchBrightness", 4.5f);
            SetVectorIfPresent(material, "_GlitchNoiseScale", new Vector4(1.5f, 4f, 0f, 0f));
            SetVectorIfPresent(material, "_GlitchNoiseSpeed", new Vector4(0.6f, 0.25f, 0f, 0f));
            SetVectorIfPresent(material, "_GlitchDistortion", new Vector4(0.18f, 0.04f, 0f, 0f));
            SetVectorIfPresent(material, "_GlitchDistortionScale", new Vector4(2f, 6f, 0f, 0f));
            SetVectorIfPresent(material, "_GlitchDistortionSpeed", new Vector4(1.1f, 0.4f, 0f, 0f));
        }

        private static void ConfigureRepresentativeEffect(
            Material material,
            string effect,
            bool enabled,
            Texture2D noiseTexture)
        {
            float toggle = enabled ? 1f : 0f;
            switch (effect)
            {
                case "Flow":
                    SetFloatIfPresent(material, "_EnableFlow", toggle);
                    SetVectorIfPresent(material, "_FlowSpeed", new Vector4(0.55f, -0.37f, 0f, 0f));
                    SetFloatIfPresent(material, "_FlowStrength", 1f);
                    break;
                case "UVTransform":
                    SetFloatIfPresent(material, "_EnableUVTransform", toggle);
                    SetVectorIfPresent(material, "_UVPivot", new Vector4(0.5f, 0.5f, 0f, 0f));
                    SetVectorIfPresent(material, "_UVScale", new Vector4(1.7f, 0.65f, 0f, 0f));
                    SetVectorIfPresent(material, "_UVOffset", new Vector4(0.13f, -0.08f, 0f, 0f));
                    SetFloatIfPresent(material, "_UVRotation", 27f);
                    break;
                case "FullDistortion":
                    SetFloatIfPresent(material, "_EnableFullDistortion", toggle);
                    SetFloatIfPresent(material, "_FullDistortionFade", 0f);
                    SetVectorIfPresent(material, "_FullDistortionDistortion", new Vector4(0.18f, 0.14f, 0f, 0f));
                    SetVectorIfPresent(material, "_FullDistortionNoiseScale", new Vector4(2.3f, 3.7f, 0f, 0f));
                    break;
                case "Pixelate":
                    SetFloatIfPresent(material, "_EnablePixelate", toggle);
                    SetFloatIfPresent(material, "_PixelateCells", 4f);
                    SetFloatIfPresent(material, "_PixelateStrength", 1f);
                    break;
                case "Chromatic":
                    SetFloatIfPresent(material, "_EnableChromatic", toggle);
                    SetFloatIfPresent(material, "_ChromaticOffset", 0.02f);
                    SetFloatIfPresent(material, "_ChromaticIntensity", 1f);
                    SetFloatIfPresent(material, "_ChromaticEdgeOnly", 0f);
                    SetFloatIfPresent(material, "_ChromaticAngle", 33f);
                    break;
                case "Blur":
                    SetFloatIfPresent(material, "_EnableBlur", toggle);
                    SetFloatIfPresent(material, "_BlurRadius", 0.02f);
                    SetFloatIfPresent(material, "_BlurIntensity", 1f);
                    SetFloatIfPresent(material, "_BlurMode", 1f);
                    break;
                case "Negative":
                    SetFloatIfPresent(material, "_EnableNegative", toggle);
                    SetFloatIfPresent(material, "_NegativeFade", 1f);
                    break;
                case "Checkerboard":
                    SetFloatIfPresent(material, "_EnableCheckerboard", toggle);
                    SetFloatIfPresent(material, "_CheckerboardDarken", 0.05f);
                    SetFloatIfPresent(material, "_CheckerboardTiling", 8f);
                    break;
                case "FullAlphaDissolve":
                    SetFloatIfPresent(material, "_EnableFullAlphaDissolve", toggle);
                    SetFloatIfPresent(material, "_FullAlphaDissolveFade", 0.65f);
                    SetFloatIfPresent(material, "_FullAlphaDissolveWidth", 0.12f);
                    SetVectorIfPresent(material, "_FullAlphaDissolveNoiseScale", new Vector4(4f, 3f, 0f, 0f));
                    break;
                case "OuterOutline":
                    SetFloatIfPresent(material, "_EnableOuterOutline", toggle);
                    SetColorIfPresent(material, "_OuterOutlineColor", new Color(1.8f, 0.05f, 1.2f, 1f));
                    SetFloatIfPresent(material, "_OuterOutlineWidth", 0.02f);
                    SetFloatIfPresent(material, "_OuterOutlineFade", 1f);
                    break;
                case "TextureLayer1":
                    SetFloatIfPresent(material, "_EnableTextureLayer1", toggle);
                    SetTextureIfPresent(material, "_TextureLayer1Texture", noiseTexture);
                    SetColorIfPresent(material, "_TextureLayer1Color", new Color(0.1f, 1.5f, 1.8f, 1f));
                    SetFloatIfPresent(material, "_TextureLayer1Fade", 0.9f);
                    SetVectorIfPresent(material, "_TextureLayer1Scale", new Vector4(2f, 3f, 0f, 0f));
                    SetVectorIfPresent(material, "_TextureLayer1Offset", new Vector4(0.17f, 0.09f, 0f, 0f));
                    break;
                case "RecolorRGB":
                    SetFloatIfPresent(material, "_EnableRecolorRGB", toggle);
                    SetColorIfPresent(material, "_RecolorRed", new Color(0f, 1.4f, 1.4f, 1f));
                    SetColorIfPresent(material, "_RecolorGreen", new Color(1.4f, 0f, 1.4f, 1f));
                    SetColorIfPresent(material, "_RecolorBlue", new Color(1.4f, 1.4f, 0f, 1f));
                    SetFloatIfPresent(material, "_RecolorRGBStrength", 1f);
                    SetFloatIfPresent(material, "_RecolorRGBMaskToggle", 0f);
                    break;
                case "SineMove":
                    SetFloatIfPresent(material, "_EnableSineMove", toggle);
                    SetFloatIfPresent(material, "_SineMoveFade", 1f);
                    SetVectorIfPresent(material, "_SineMoveOffset", new Vector4(0.2f, 0.35f, 0f, 0f));
                    SetVectorIfPresent(material, "_SineMoveFrequency", new Vector4(2.2f, 3.4f, 0f, 0f));
                    break;
                case "Sequence":
                    SetFloatIfPresent(material, "_EnableSequence", toggle);
                    SetFloatIfPresent(material, "_SequencePlayback", 0f);
                    SetFloatIfPresent(material, "_SequenceColumns", 2f);
                    SetFloatIfPresent(material, "_SequenceRows", 2f);
                    SetFloatIfPresent(material, "_SequenceFrame", 3f);
                    break;
                case "PolarUV":
                    SetFloatIfPresent(material, "_EnablePolarUV", toggle);
                    SetVectorIfPresent(material, "_PolarCenter", new Vector4(0.5f, 0.5f, 0f, 0f));
                    SetFloatIfPresent(material, "_PolarRadialScale", 1.7f);
                    SetFloatIfPresent(material, "_PolarAngularScale", 0.65f);
                    SetFloatIfPresent(material, "_PolarRotationSpeed", 0.8f);
                    break;
                case "RadialMask":
                    SetFloatIfPresent(material, "_EnableRadialMask", toggle);
                    SetVectorIfPresent(material, "_RadialMaskCenter", new Vector4(0.5f, 0.5f, 0f, 0f));
                    SetFloatIfPresent(material, "_RadialMaskRadius", 0.34f);
                    SetFloatIfPresent(material, "_RadialMaskSoftness", 0.04f);
                    break;
                case "Shine":
                    SetFloatIfPresent(material, "_EnableShine", toggle);
                    SetColorIfPresent(material, "_ShineColor", new Color(1.8f, 0.4f, 0.05f, 1f));
                    SetFloatIfPresent(material, "_ShineSpeed", 1.3f);
                    SetFloatIfPresent(material, "_ShineWidth", 0.35f);
                    SetFloatIfPresent(material, "_ShineIntensity", 5f);
                    break;
                case "Sparkle":
                    SetFloatIfPresent(material, "_EnableSparkle", toggle);
                    SetColorIfPresent(material, "_SparkleColor", new Color(2f, 0.2f, 1.8f, 1f));
                    SetFloatIfPresent(material, "_SparkleScale", 8f);
                    SetFloatIfPresent(material, "_SparkleSpeed", 1.7f);
                    SetFloatIfPresent(material, "_SparkleDensity", 0.9f);
                    SetFloatIfPresent(material, "_SparkleSharpness", 2f);
                    SetFloatIfPresent(material, "_SparkleIntensity", 8f);
                    break;
                case "VertexAnimation":
                    SetFloatIfPresent(material, "_EnableVertexAnimation", toggle);
                    SetVectorIfPresent(material, "_VertexAnimationDirection", new Vector4(0.35f, 1f, 0f, 0f));
                    SetFloatIfPresent(material, "_VertexAnimationAmplitude", 0.35f);
                    SetFloatIfPresent(material, "_VertexAnimationFrequency", 4f);
                    SetFloatIfPresent(material, "_VertexAnimationSpeed", 2f);
                    SetFloatIfPresent(material, "_VertexAnimationMask", 0f);
                    break;
                case "Dissolve":
                    SetFloatIfPresent(material, "_DissolveMode", enabled ? 2f : 0f);
                    SetFloatIfPresent(material, "_DissolveProgress", 0.58f);
                    SetFloatIfPresent(material, "_DissolveWidth", 0.08f);
                    SetColorIfPresent(material, "_DissolveColor", new Color(2f, 0.08f, 0.02f, 1f));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect, "Unknown representative effect.");
            }
        }

        private static Texture2D RenderMaterial(Material material, float worldHeight, bool orthographic)
        {
            PreviewRenderUtility utility = null;
            Mesh mesh = null;
            Texture2D preview = null;
            try
            {
                utility = new PreviewRenderUtility();
                utility.camera.orthographic = orthographic;
                utility.camera.orthographicSize = 0.55f;
                utility.camera.fieldOfView = 30f;
                utility.camera.transform.position = new Vector3(0f, worldHeight, -2.05f);
                utility.camera.transform.rotation = Quaternion.identity;
                utility.camera.nearClipPlane = 0.01f;
                utility.camera.farClipPlane = 10f;
                utility.camera.clearFlags = CameraClearFlags.Color;
                utility.camera.backgroundColor = PreviewBackground;
                utility.lights[0].intensity = 1.25f;
                utility.lights[0].transform.rotation = Quaternion.Euler(30f, 30f, 0f);
                utility.lights[1].intensity = 0.55f;

                mesh = CreateQuad();
                utility.BeginStaticPreview(new Rect(0f, 0f, PreviewSize, PreviewSize));
                utility.DrawMesh(
                    mesh,
                    Matrix4x4.TRS(new Vector3(0f, worldHeight, 0f), Quaternion.identity, Vector3.one),
                    material,
                    0);
                utility.camera.Render();
                preview = utility.EndStaticPreview();
                Assert.That(preview, Is.Not.Null, material.shader.name + " preview render returned null.");
                return CloneTexture(preview);
            }
            finally
            {
                DestroyImmediateSafe(preview);
                DestroyImmediateSafe(mesh);
                utility?.Cleanup();
            }
        }

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "ES Composite Visual Test Quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.tangents = new[]
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D CreateBaseTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, UnityEngine.TextureFormat.RGBA32, false, false)
            {
                name = "ES Composite Visual Base",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float v = y / (float)(size - 1);
                    float checker = ((x / 8 + y / 8) & 1) == 0 ? 0.22f : 0.82f;
                    float alpha = Mathf.SmoothStep(0.04f, 0.12f, 0.5f - Vector2.Distance(new Vector2(u, v), Vector2.one * 0.5f));
                    pixels[y * size + x] = new Color(
                        Mathf.Lerp(0.08f, 1f, u) * checker,
                        Mathf.Lerp(0.95f, 0.12f, v) * checker,
                        Mathf.Lerp(0.25f, 0.9f, 1f - u) * checker,
                        alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateNoiseTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, UnityEngine.TextureFormat.RGBA32, false, true)
            {
                name = "ES Composite Visual Noise",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            uint state = 0x9E3779B9u;
            for (int i = 0; i < pixels.Length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                float value = (state & 0x00FFFFFFu) / 16777215f;
                pixels[i] = new Color(value, value, value, 1f);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CloneTexture(Texture2D source)
        {
            var clone = new Texture2D(source.width, source.height, UnityEngine.TextureFormat.RGBA32, false, false)
            {
                name = source.name + " Copy",
                hideFlags = HideFlags.HideAndDontSave
            };
            clone.SetPixels32(source.GetPixels32());
            clone.Apply(false, false);
            return clone;
        }

        private static float VisibleCoverage(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            Color32 background = PreviewBackground;
            int visible = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (Math.Abs(pixel.r - background.r) > BackgroundColorTolerance ||
                    Math.Abs(pixel.g - background.g) > BackgroundColorTolerance ||
                    Math.Abs(pixel.b - background.b) > BackgroundColorTolerance)
                {
                    visible++;
                }
            }
            return visible / (float)pixels.Length;
        }

        private static float MeanAbsoluteDifference(Texture2D a, Texture2D b)
        {
            Color32[] first = a.GetPixels32();
            Color32[] second = b.GetPixels32();
            Assert.That(second.Length, Is.EqualTo(first.Length));
            double total = 0d;
            for (int i = 0; i < first.Length; i++)
            {
                total += Math.Abs(first[i].r - second[i].r);
                total += Math.Abs(first[i].g - second[i].g);
                total += Math.Abs(first[i].b - second[i].b);
                total += Math.Abs(first[i].a - second[i].a);
            }
            return (float)(total / (first.Length * 4d * 255d));
        }

        private static string PrepareOutputDirectory()
        {
            string path = Path.GetFullPath(Path.Combine("Library", "ESCompositeShaderVisualEvidence"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void SaveImage(string directory, string fileName, Texture2D texture)
        {
            File.WriteAllBytes(Path.Combine(directory, fileName), texture.EncodeToPNG());
        }

        private static void RequireGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("ES Composite visual tests require a real graphics device.");
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName)) material.SetFloat(propertyName, value);
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName)) material.SetColor(propertyName, value);
        }

        private static void SetVectorIfPresent(Material material, string propertyName, Vector4 value)
        {
            if (material.HasProperty(propertyName)) material.SetVector(propertyName, value);
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture value)
        {
            if (material.HasProperty(propertyName)) material.SetTexture(propertyName, value);
        }

        private static void DestroyImmediateSafe(UnityEngine.Object value)
        {
            if (value != null) UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
