using System.IO;
using ES;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ES.Tests
{
    public sealed class ESCompositeShaderContractTests
    {
        private const string ShaderRoot = "Assets/Plugins/ES/0_Stand/InternalAssets/Shaders/";
        private const string EditorShaderRoot = "Assets/Plugins/ES/Editor/ESShader/";

        [TestCase("ES/2D/Composite URP", "ES2DComposite", "Universal2D")]
        [TestCase("ES/3D/Lit Composite URP", "ForwardLit", "UniversalForwardOnly")]
        [TestCase("ES/3D/VFX Composite URP", "ForwardUnlit", "UniversalForward")]
        [TestCase("ES/UI/Composite URP", "UIForward", "SRPDefaultUnlit")]
        public void CompositeShader_HasExpectedPrimaryPass(
            string shaderName,
            string passName,
            string lightMode)
        {
            Shader shader = RequireShader(shaderName);

            AssertSourcePassLightMode(shader, passName, lightMode);
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/3D/Lit Composite URP")]
        [TestCase("ES/3D/VFX Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void CompositeShader_HasNoReportedImportErrors(string shaderName)
        {
            Shader shader = RequireShader(shaderName);

            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False,
                shaderName + " has a Unity shader import error.");
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/3D/Lit Composite URP")]
        [TestCase("ES/3D/VFX Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void CompositeShader_ExposesMaterialSchemaVersion(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(shader, "_ESMaterialVersion");
        }

        [TestCase("ES/2D/Composite URP", "ES2DCompositeURP.shader")]
        [TestCase("ES/3D/Lit Composite URP", "ES3DLitCompositeURPCommon.hlsl")]
        [TestCase("ES/3D/VFX Composite URP", "ES3DVFXCompositeURPCommon.hlsl")]
        [TestCase("ES/UI/Composite URP", "ESUICompositeURP.shader")]
        public void CompositeShader_ExposesOrderedTimeModifierContract(string shaderName, string implementationFile)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_TimeMode",
                "_CustomTime",
                "_TimeScale",
                "_EnableTimeFPS",
                "_TimeFPS",
                "_EnableTimeFrequency",
                "_TimeFrequency",
                "_TimeRange");

            string source = File.ReadAllText(Path.GetFullPath(ShaderRoot + implementationFile));
            int scale = source.IndexOf("baseTime * _TimeScale", System.StringComparison.Ordinal);
            int fps = source.IndexOf("if (_EnableTimeFPS > 0.5)", scale, System.StringComparison.Ordinal);
            int frequency = source.IndexOf("if (_EnableTimeFrequency > 0.5)", fps, System.StringComparison.Ordinal);
            Assert.That(scale, Is.GreaterThanOrEqualTo(0));
            Assert.That(fps, Is.GreaterThan(scale));
            Assert.That(frequency, Is.GreaterThan(fps));
        }

        [Test]
        public void CompositeTimeParameters_SupportReversePlaybackAndClampModifiers()
        {
            var block = new MaterialPropertyBlock();

            ESCompositeURPProperties.SetTime(block, ESCompositeTimeMode.自定义时间, -2.5f, 12f);
            ESCompositeURPProperties.SetTimeModifiers(block, true, -500f, true, -3f, -0.75f);

            Assert.That(block.GetFloat(ESCompositeURPProperties.TimeMode), Is.EqualTo((float)ESCompositeTimeMode.自定义时间));
            Assert.That(block.GetFloat(ESCompositeURPProperties.TimeScale), Is.EqualTo(-2.5f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.CustomTime), Is.EqualTo(12f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.TimeFPSEnabled), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.TimeFPS), Is.EqualTo(240f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.TimeFrequencyEnabled), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.TimeFrequency), Is.EqualTo(-3f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.TimeRange), Is.EqualTo(-0.75f));
            Assert.That(ES2DCompositeURPProperties.TimeFPSEnabled, Is.EqualTo(ESCompositeURPProperties.TimeFPSEnabled));
            Assert.That(ES3DLitCompositeURPProperties.TimeFrequencyEnabled, Is.EqualTo(ESCompositeURPProperties.TimeFrequencyEnabled));
            Assert.That(ES3DVFXCompositeURPProperties.TimeFPS, Is.EqualTo(ESCompositeURPProperties.TimeFPS));
            Assert.That(ESUICompositeURPProperties.TimeRange, Is.EqualTo(ESCompositeURPProperties.TimeRange));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteShaders_ExposeAtlasLocalUvContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_SpriteUVRect",
                "_SpriteUVTransformX",
                "_SpriteUVTransformY",
                "_SpriteUVTransformValid");
            Assert.That(ReadShaderSource(shader), Does.Contain("ESAtlasToLocalUV"));
            Assert.That(ReadShaderSource(shader), Does.Contain("ESSpritePixelSize"));
        }

        [Test]
        public void TwoDShader_ImplementsPixelSnapAndSceneSelectionContracts()
        {
            Shader shader = RequireShader("ES/2D/Composite URP");

            AssertProperties(shader, "PixelSnap");
            AssertPasses(shader, "ScenePickingPass", "SceneSelectionPass");
            AssertSourcePassLightMode(shader, "ScenePickingPass", "Picking");
            AssertSourcePassLightMode(shader, "SceneSelectionPass", "SceneSelectionPass");
            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("ESApplyPixelSnap(positionInputs.positionCS)"));
            Assert.That(source, Does.Contain("return unity_SelectionID;"));
            Assert.That(source, Does.Contain("float4 _SelectionID;"));
            Assert.That(source, Does.Contain("_ObjectId"));
        }

        [Test]
        public void UIShader_UsesSoftRectMaskForTmpAndUgui()
        {
            Shader shader = RequireShader("ES/UI/Composite URP");

            AssertProperties(shader, "_MaskSoftnessX", "_MaskSoftnessY", "_UIMaskSoftnessX", "_UIMaskSoftnessY");
            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("half2 maskSoftness = _EnableTMPCompatibility > 0.5"));
            Assert.That(source, Does.Contain("half2(_UIMaskSoftnessX, _UIMaskSoftnessY)"));
            Assert.That(source, Does.Contain("float2 uiScale = _EnableTMPCompatibility > 0.5"));
            Assert.That(source, Does.Contain(": float2(1, 1);"));
            Assert.That(source, Does.Contain("color *= mask.x * mask.y"));
            Assert.That(source, Does.Not.Contain("ESGet2DClipping"));
        }

        [Test]
        public void LitShader_ExposesCompleteDepthAndBakePassContract()
        {
            Shader shader = RequireShader("ES/3D/Lit Composite URP");

            AssertPasses(shader, "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals", "GBuffer", "Meta", "ScenePickingPass", "SceneSelectionPass");
            AssertSourcePassLightMode(shader, "ScenePickingPass", "Picking");
            AssertSourcePassLightMode(shader, "SceneSelectionPass", "SceneSelectionPass");
            AssertSourcePassLightMode(shader, "GBuffer", "UniversalGBuffer");
            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("unity_SelectionID"));
            Assert.That(source, Does.Contain("_ObjectId"));
            Assert.That(source, Does.Contain("BRDFDataToGbuffer"));
        }

        [Test]
        public void LitShader_ExposesMetallicMapContract()
        {
            Shader shader = RequireShader("ES/3D/Lit Composite URP");

            AssertProperties(
                shader,
                "_UseMetallicMap",
                "_MetallicMap",
                "_SmoothnessMapChannel",
                "_Metallic",
                "_Smoothness",
                "_UseEmission",
                "_EmissionMap",
                "_EmissionUseAlpha");
            string commonSource = System.IO.File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            Assert.That(commonSource, Does.Contain("metallicSample.r"));
            Assert.That(commonSource, Does.Contain("metallicSample.a"));
            Assert.That(commonSource, Does.Contain("step(0.5, _SmoothnessMapChannel)"));
            Assert.That(commonSource, Does.Contain("step(0.5, _EmissionUseAlpha)"));
        }

        [Test]
        public void LitShader_ExposesSSUSurfaceColorAndStatusContract()
        {
            Shader shader = RequireShader("ES/3D/Lit Composite URP");

            AssertProperties(
                shader,
                "_EnableAddColor",
                "_EnableStrongTint",
                "_EnableAlphaTint",
                "_EnableColorReplace",
                "_EnableRecolorRGB",
                "_EnableRecolorRGBYCP",
                "_EnableBrightness",
                "_EnableContrast",
                "_EnableSaturation",
                "_EnableHue",
                "_EnableSplitToning",
                "_EnableBlackTint",
                "_EnableInkSpread",
                "_EnableShiftHue",
                "_EnableAddHue",
                "_EnableSineGlow",
                "_EnableCamouflage",
                "_EnableMetal",
                "_EnableFrozen",
                "_EnablePoison",
                "_EnableEnchanted",
                "_EnableShifting",
                "_EnableNegative",
                "_EnableRainbow",
                "_EnablePingPongGlow",
                "_UberNoiseTexture",
                "_RecolorRGBMask",
                "_RecolorRGBYCPMask",
                "_AddHueMask",
                "_SineGlowMask",
                "_MetalMask");
        }

        [Test]
        public void LitShader_ExposesUnifiedUvFadeAndVertexMotionContract()
        {
            Shader shader = RequireShader("ES/3D/Lit Composite URP");
            AssertProperties(
                shader,
                "_EnableUVTransform",
                "_EnableUVDistort",
                "_TilingMode",
                "_EnableSqueeze",
                "_EnableSineRotate",
                "_EnableWind",
                "_EnableSquish",
                "_EnableWiggle",
                "_EnableVibrate",
                "_EnableSineMove",
                "_EnableSineScale",
                "_FadeMode",
                "_FadeProgress",
                "_FadeMask",
                "_DissolveEdgeIntensity",
                "_EnableCustomFade");

            string source = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            Assert.That(source, Does.Contain("float2 ESResolveLitUV"));
            Assert.That(source, Does.Contain("float ESComputeLitAlpha"));
            Assert.That(source, Does.Contain("ESApplyVertexMotion"));
            Assert.That(source, Does.Contain("ESCompositeEvaluateFade"));
            Assert.That(source, Does.Contain("ESCompositeResolveTilingUV"));
            Assert.That(source, Does.Contain("ESCompositeApplySqueezeUV"));
            Assert.That(source, Does.Contain("ESCompositeApplySineRotateUV"));
        }

        [Test]
        public void LitShader_ExposesGeneratedStylizationAndKeepsAlphaPassesInContract()
        {
            Shader shader = RequireShader("ES/3D/Lit Composite URP");
            AssertProperties(
                shader,
                "_EnableSmoothPixelArt", "_SmoothPixelStrength",
                "_EnablePixelate", "_PixelateCells", "_PixelateStrength",
                "_EnableCheckerboard", "_CheckerboardDarken", "_CheckerboardTiling",
                "_EnableFlame", "_FlameBrightness", "_FlameSmooth", "_FlameRadius",
                "_FlameSpeed", "_FlameNoiseFactor", "_FlameNoiseHeightFactor", "_FlameNoiseScale",
                "_EnableSmoke", "_SmokeAlpha", "_SmokeSmoothness", "_SmokeNoiseScale",
                "_SmokeNoiseFactor", "_SmokeDarkEdge", "_SmokeVertexSeed",
                "_EnableHalftone", "_HalftoneScale", "_HalftoneAngle", "_HalftoneStrength",
                "_HalftonePosition", "_HalftoneFade", "_HalftoneFadeWidth", "_HalftoneInvert", "_HalftoneAlphaPattern",
                "_EnableSharpen", "_SharpenAmount", "_SharpenRadius", "_SharpenThreshold", "_SharpenFade");

            string source = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            Assert.That(source, Does.Contain("#include \"ESCompositeSampling.hlsl\""));
            Assert.That(source, Does.Contain("ESLitSmokeMask"));
            Assert.That(source, Does.Contain("ESLitFlameMask"));
            Assert.That(source, Does.Contain("ESLitApplyHalftone"));
            Assert.That(source, Does.Contain("ESLitSharpenSample"));
            Assert.That(source, Does.Contain("alpha *= ESLitSmokeMask"));
            Assert.That(source, Does.Contain("alpha *= ESLitFlameMask"));
            Assert.That(source, Does.Contain("half ESLitHalftoneVisibility(float2 uv)"));
            Assert.That(source, Does.Contain("alpha *= ESLitHalftoneVisibility(uv);"));
            Assert.That(source, Does.Contain("baseSample.a = sourceCoverage;"));
        }

        [Test]
        public void LitShader_ColorSamplingFiltersDoNotChangeSharedCoverage()
        {
            string source = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            int sourceCoverage = source.IndexOf("half sourceCoverage = baseSample.a;", System.StringComparison.Ordinal);
            int sharpen = source.IndexOf("if (_EnableSharpen > 0.5", sourceCoverage, System.StringComparison.Ordinal);
            int blur = source.IndexOf("if (_EnableBlur > 0.5", sharpen, System.StringComparison.Ordinal);
            int restoreCoverage = source.IndexOf("baseSample.a = sourceCoverage;", blur, System.StringComparison.Ordinal);
            int chromatic = source.IndexOf("if (_EnableChromatic > 0.5", restoreCoverage, System.StringComparison.Ordinal);

            Assert.That(sourceCoverage, Is.GreaterThanOrEqualTo(0));
            Assert.That(sharpen, Is.GreaterThan(sourceCoverage));
            Assert.That(blur, Is.GreaterThan(sharpen));
            Assert.That(restoreCoverage, Is.GreaterThan(blur));
            Assert.That(chromatic, Is.GreaterThan(restoreCoverage));
        }

        [TestCase("ES2DCompositeURP.shader", "ESBlurSample(float2 uv, half4 center)", "ESBlurSample(uv, sampledSource)")]
        [TestCase("ESUICompositeURP.shader", "ESBlurSample(float2 uv, half4 center)", "ESBlurSample(uv, sampledColor)")]
        [TestCase("ES3DVFXCompositeURPCommon.hlsl", "ESBlurSample(float2 uv, float4 atlasBounds, half4 center)", "ESBlurSample(uv, atlasBounds, sourceTexture)")]
        public void CompositeShaders_ReuseCenterSampleAndGuardExpensiveFilters(
            string fileName,
            string blurSignature,
            string blurCall)
        {
            string source = File.ReadAllText(ShaderRoot + fileName);
            Assert.That(source, Does.Contain(blurSignature));
            Assert.That(source, Does.Contain("half4 result = center * 0.4h;"));
            Assert.That(source, Does.Contain(blurCall));
            Assert.That(source, Does.Contain("_BlurIntensity > 0.0001"));
            Assert.That(source, Does.Contain("_BlurRadius > 0.0001"));
            Assert.That(source, Does.Contain("_ChromaticIntensity > 0.0001"));
            Assert.That(source, Does.Contain("abs(_ChromaticOffset) > 0.000001"));
        }

        [TestCase("ES2DCompositeURP.shader")]
        [TestCase("ESUICompositeURP.shader")]
        public void SpriteShaders_GuardSharpenBeforeNeighbourSampling(string fileName)
        {
            string source = File.ReadAllText(ShaderRoot + fileName);
            Assert.That(source, Does.Contain(
                "if (_EnableSharpen > 0.5 && _SharpenFade > 0.0001 && _SharpenAmount > 0.0001)"));
            Assert.That(source, Does.Contain("ESSharpenSample(uv, sampled"));
        }

        [Test]
        public void LitShader_ReusesBaseSampleForSharpenAndBlur()
        {
            string source = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            Assert.That(source, Does.Contain("half4 baseTextureSample = SAMPLE_TEXTURE2D"));
            Assert.That(source, Does.Contain("ESLitSharpenSample(uv, baseTextureSample)"));
            Assert.That(source, Does.Contain("ESBlurBaseSample(uv, baseTextureSample)"));
            Assert.That(source, Does.Not.Contain("ESLitSharpenSample(\n            uv,\n            SAMPLE_TEXTURE2D"));
        }

        [Test]
        public void LitShader_ExposesRemainingSSUEffectsAndKeepsSharedPassContracts()
        {
            Shader shader = RequireShader("ES/3D/Lit Composite URP");
            AssertProperties(
                shader,
                "_EnableTextureLayer1", "_TextureLayer1Texture", "_TextureLayer1Fade",
                "_TextureLayer1Color", "_TextureLayer1Scale", "_TextureLayer1Offset",
                "_TextureLayer1ScrollToggle", "_TextureLayer1ScrollSpeed",
                "_TextureLayer1SheetToggle", "_TextureLayer1Columns", "_TextureLayer1Rows",
                "_TextureLayer1Speed", "_TextureLayer1StartFrame", "_TextureLayer1EdgeClip",
                "_TextureLayer1ContrastToggle", "_TextureLayer1Contrast",
                "_EnableTextureLayer2", "_TextureLayer2Texture", "_TextureLayer2Fade",
                "_EnableInnerOutline", "_InnerOutlineFade", "_InnerOutlineColor", "_InnerOutlineWidth",
                "_InnerOutlineDistortionToggle", "_InnerOutlineDistortionIntensity",
                "_InnerOutlineNoiseScale", "_InnerOutlineNoiseSpeed", "_InnerOutlineTextureToggle",
                "_InnerOutlineTintTexture", "_InnerOutlineTextureSpeed", "_InnerOutlineOutlineOnlyToggle",
                "_EnableOuterOutline", "_OuterOutlineFade", "_OuterOutlineColor", "_OuterOutlineWidth",
                "_OuterOutlineDistortionToggle", "_OuterOutlineDistortionIntensity",
                "_OuterOutlineNoiseScale", "_OuterOutlineNoiseSpeed", "_OuterOutlineTextureToggle",
                "_OuterOutlineTintTexture", "_OuterOutlineTextureSpeed", "_OuterOutlineOutlineOnlyToggle",
                "_EnablePixelOutline", "_PixelOutlineFade", "_PixelOutlineColor", "_PixelOutlineWidth",
                "_PixelOutlineTextureToggle", "_PixelOutlineTintTexture", "_PixelOutlineTextureSpeed",
                "_PixelOutlineOutlineOnlyToggle",
                "_EnableShadow", "_ShadowFade", "_ShadowOffset", "_ShadowColor",
                "_EnableFullGlowDissolve", "_FullGlowDissolveFade", "_FullGlowDissolveWidth",
                "_FullGlowDissolveEdgeColor", "_FullGlowDissolveNoiseScale",
                "_EnableHologram", "_HologramFade", "_HologramColor", "_HologramContrast",
                "_HologramSpace", "_HologramDirection", "_HologramLineFrequency", "_HologramLineGap", "_HologramSpeed",
                "_HologramMinAlpha", "_HologramDistortionOffset", "_HologramDistortionDirection", "_HologramDistortionSpeed",
                "_HologramDistortionDensity", "_HologramDistortionScale",
                "_EnableGlitch", "_GlitchFade", "_GlitchIntensity", "_GlitchSpeed",
                "_GlitchScanDirection",
                "_GlitchMaskMin", "_GlitchMaskScale", "_GlitchMaskSpeed", "_GlitchHueSpeed",
                "_GlitchBrightness", "_GlitchNoiseScale", "_GlitchNoiseSpeed", "_GlitchDistortion",
                "_GlitchDistortionScale", "_GlitchDistortionSpeed");

            string commonSource = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            Assert.That(commonSource, Does.Contain("TEXTURE2D(_TextureLayer1Texture);"));
            Assert.That(commonSource, Does.Contain("TEXTURE2D(_TextureLayer2Texture);"));
            Assert.That(commonSource, Does.Not.Contain("SAMPLER(sampler_TextureLayer1Texture)"));
            Assert.That(commonSource, Does.Not.Contain("SAMPLER(sampler_TextureLayer2Texture)"));
            Assert.That(commonSource, Does.Contain("uv = ESLitApplyGlitchUV(uv);"));
            Assert.That(commonSource, Does.Contain("float2(width.x, -width.y)"));
            Assert.That(commonSource, Does.Contain("float2(-width.x, width.y)"));
            Assert.That(commonSource, Does.Contain("half outlineEdge = ESLitOuterOutlineEdge(uv, sourceAlpha);"));
            Assert.That(commonSource, Does.Contain("alpha = outlineOnly ? outlineEdge : max(alpha, outlineEdge);"));
            Assert.That(commonSource, Does.Contain("alpha *= ESLitHologramVisibility(uv, positionWS);"));
            Assert.That(commonSource, Does.Contain("positionWS.y / orthographicHeight"));
            Assert.That(commonSource, Does.Contain("return max(saturate(_GlitchMaskMin), maskNoise);"));
            Assert.That(commonSource, Does.Contain("float2 noiseUV = uv * scale + ESCompositeTime() * speed;"));
            Assert.That(commonSource, Does.Contain("return uv + noise * intensity;"));
            Assert.That(commonSource, Does.Contain("alpha *= fullGlowVisibility;"));
            Assert.That(commonSource, Does.Contain("half shadowAlpha = ESLitShadowAlpha(uv) * fullGlowVisibility;"));
            Assert.That(commonSource, Does.Contain("baseSample.rgb = ESLitApplyTextureLayers(baseSample.rgb, uv);"));
            Assert.That(commonSource, Does.Contain("ESLitApplyOutlines(uv, baseSample.a, baseSample);"));
            Assert.That(commonSource, Does.Contain("uv = ESLitApplyHologramUV(uv, positionWS);"));
        }

        [Test]
        public void LitSSUSurfaceEffects_ShareRuntimePassesWhileMetaRemainsStable()
        {
            string shaderSource = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURP.shader");
            string commonSource = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            string effectSource = File.ReadAllText(ShaderRoot + "ES3DLitCompositeSSUSurface.hlsl");
            string[] parentSwitches =
            {
                "_EnableAddColor", "_EnableStrongTint", "_EnableAlphaTint", "_EnableColorReplace",
                "_EnableRecolorRGB", "_EnableRecolorRGBYCP", "_EnableBrightness", "_EnableContrast",
                "_EnableSaturation", "_EnableHue", "_EnableSplitToning", "_EnableBlackTint",
                "_EnableInkSpread", "_EnableShiftHue", "_EnableAddHue", "_EnableSineGlow",
                "_EnableCamouflage", "_EnableMetal",
                "_EnableEnchanted", "_EnableShifting", "_EnableNegative",
                "_EnablePingPongGlow"
            };

            Assert.That(commonSource, Does.Contain("#include \"ES3DLitCompositeSSUSurface.hlsl\""));
            Assert.That(commonSource, Does.Contain("ESApplyLitSSUSurfaceEffects(uv, baseSample.a * dissolveAlpha"));
            Assert.That(shaderSource, Does.Not.Contain("ESApplyLitSSUSurfaceEffects(surfaceUV, albedo.a"));
            Assert.That(shaderSource, Does.Contain("float2 surfaceUV = ESBaseUV(input.uv);"));
            Assert.That(shaderSource, Does.Contain("time-driven composite effects remain runtime-only"));
            for (int i = 0; i < parentSwitches.Length; i++)
                Assert.That(effectSource, Does.Contain("if (" + parentSwitches[i] + " > 0.5)"),
                    parentSwitches[i] + " does not gate its Lit surface path.");

            Assert.That(commonSource, Does.Contain("#include \"ESCompositeSSUStatusEffects.hlsl\""));
            Assert.That(commonSource, Does.Contain("#include \"ESCompositeSSUStylizedEffects.hlsl\""));
            Assert.That(commonSource, Does.Contain("ESLitSSUMinNeighbourAlpha8"));
            Assert.That(commonSource, Does.Contain("ESLitSSUMaxNeighbourAlpha8"));
            Assert.That(commonSource, Does.Contain("ESLitSSUMaxNeighbourAlpha4"));
            Assert.That(commonSource, Does.Contain("ESLitApplySSUOutlines"));
            Assert.That(commonSource, Does.Contain("ESLitApplySSUOutlineAlpha"));
            Assert.That(commonSource, Does.Contain("ESCompositeApplySSUHologramUV"));
            Assert.That(commonSource, Does.Contain("ESCompositeApplySSUGlitchUV"));
            Assert.That(commonSource, Does.Contain("ESCompositeApplySSUHologramColor"));
            Assert.That(commonSource, Does.Contain("ESCompositeApplySSUGlitchColor"));
            Assert.That(effectSource, Does.Contain("if (_SSUStatusContract > 0.5)"));
            Assert.That(effectSource, Does.Contain("ESCompositeApplySSUStatusEffects(color, uv, uv, timeValue)"));
            Assert.That(effectSource, Does.Contain("_SSUStatusContract <= 0.5 && (_EnableFrozen > 0.5 || _EnablePoison > 0.5)"));
            Assert.That(effectSource, Does.Contain("emission += max(withAddHue - color"));
            Assert.That(effectSource, Does.Contain("emission += max(withSineGlow - color"));
            Assert.That(effectSource, Does.Contain("emission += lerp(_GlowFrom.rgb, _GlowTo.rgb"));
            Assert.That(effectSource, Does.Not.Contain("alpha *="));
            Assert.That(effectSource, Does.Not.Contain("clip("));
        }

        [Test]
        public void LitParameters_SetMetallicMapClampsScalarInputs()
        {
            var block = new MaterialPropertyBlock();

            ES3DLitCompositeURPProperties.SetMetallicMap(block, true, null, 2f, -1f);

            Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.MetallicMapEnabled), Is.Zero);
            Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.Metallic), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.Smoothness), Is.Zero);

            Texture2D texture = new Texture2D(1, 1);
            try
            {
                ES3DLitCompositeURPProperties.SetMetallicMap(block, true, texture, 0.25f, 0.75f);
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.MetallicMapEnabled), Is.EqualTo(1f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.Metallic), Is.EqualTo(0.25f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.Smoothness), Is.EqualTo(0.75f));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void LitParameters_PreserveSSUFloatInputsAndShareIds()
        {
            var block = new MaterialPropertyBlock();
            var texture = new Texture2D(1, 1);
            try
            {
                ES3DLitCompositeURPProperties.SetOutlines(
                    block,
                    true, Color.red, 3f,
                    true, Color.green, 2f,
                    true, Color.blue, 9f);
                ES3DLitCompositeURPProperties.SetInnerOutline(
                    block, true, Color.red, 3f, -1f, true,
                    new Vector2(2f, -2f), new Vector2(4096f, -4096f), new Vector2(256f, -256f),
                    true, texture, new Vector2(256f, -256f), true);
                ES3DLitCompositeURPProperties.SetOuterOutline(
                    block, true, Color.green, 2f, 2f, true,
                    Vector2.zero, Vector2.one, Vector2.zero,
                    true, texture, Vector2.zero, true);
                ES3DLitCompositeURPProperties.SetPixelOutline(
                    block, true, Color.blue, 9f, -1f, true, texture, Vector2.zero, true);
                ES3DLitCompositeURPProperties.SetHologram(
                    block, true, Color.cyan, 4096f, -1f, 256f, 2f,
                    -1f, 20f, ES3DLitHologramSpace.世界高度, 2f, 256f, -1f, -4096f);
                ES3DLitCompositeURPProperties.SetGlitch(
                    block, true, 1f, -256f, 2f, -1f,
                    new Vector2(4096f, -4096f), new Vector2(256f, -256f), 256f, 16f,
                    Vector2.one, Vector2.zero, new Vector2(2f, -2f), Vector2.one, Vector2.zero);

                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.InnerOutlineWidth), Is.EqualTo(3f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.InnerOutlineFade), Is.Zero);
                Assert.That(block.GetVector(ES3DLitCompositeURPProperties.InnerOutlineDistortionIntensity),
                    Is.EqualTo(new Vector4(2f, -2f, 0f, 0f)));
                Assert.That(block.GetTexture(ES3DLitCompositeURPProperties.InnerOutlineTintTexture), Is.SameAs(texture));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.InnerOutlineOnly), Is.EqualTo(1f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.OuterOutlineWidth), Is.EqualTo(2f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.OuterOutlineFade), Is.EqualTo(1f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.PixelOutlineWidth), Is.EqualTo(9f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.PixelOutlineFade), Is.Zero);
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.HologramLineFrequency), Is.EqualTo(4096f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.HologramLineGap), Is.EqualTo(-1f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.HologramSpeed), Is.EqualTo(256f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.HologramMinAlpha), Is.EqualTo(1f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.HologramFade), Is.Zero);
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.HologramContrast), Is.EqualTo(20f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.HologramSpace), Is.EqualTo(1f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.HologramDistortionScale), Is.EqualTo(-4096f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.GlitchIntensity), Is.EqualTo(1f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.GlitchSpeed), Is.EqualTo(-256f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.GlitchFade), Is.EqualTo(1f));
                Assert.That(block.GetFloat(ES3DLitCompositeURPProperties.GlitchBrightness), Is.EqualTo(16f));
                Assert.That(block.GetVector(ES3DLitCompositeURPProperties.GlitchDistortion),
                    Is.EqualTo(new Vector4(2f, -2f, 0f, 0f)));
                Assert.That(ES3DLitCompositeURPProperties.SSUExactContract,
                    Is.EqualTo(ESCompositeURPProperties.SSUStatusContract));
                Assert.That(ES3DLitCompositeURPProperties.TextureLayer1Enabled,
                    Is.EqualTo(ESCompositeURPProperties.TextureLayer1Enabled));
                Assert.That(ES3DLitCompositeURPProperties.SpriteShadowEnabled,
                    Is.EqualTo(ESCompositeURPProperties.SpriteShadowEnabled));
                Assert.That(ES3DLitCompositeURPProperties.FullGlowDissolveEnabled,
                    Is.EqualTo(ESCompositeURPProperties.FullGlowDissolveEnabled));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void LitShader_SurfaceModesSynchronizeQueueBlendAndOpaquePasses()
        {
            Material material = CreateTestMaterial(RequireShader("ES/3D/Lit Composite URP"));
            try
            {
                AssertProperties(material.shader, "_Surface", "_AlphaClip", "_Cull", "_QueueOffset", "_SrcBlend", "_DstBlend", "_ZWrite");

                ES3DLitCompositeURPProperties.SetSurfaceMode(
                    material,
                    ES3DLitSurfaceMode.透明裁剪,
                    7,
                    UnityEngine.Rendering.CullMode.Off);
                Assert.That(material.GetFloat(ES3DLitCompositeURPProperties.Surface), Is.Zero);
                Assert.That(material.GetFloat(ES3DLitCompositeURPProperties.AlphaClip), Is.EqualTo(1f));
                Assert.That(material.GetFloat(ES3DLitCompositeURPProperties.Cull), Is.Zero);
                Assert.That(material.GetTag("RenderType", false), Is.EqualTo("TransparentCutout"));
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.AlphaTest + 7));
                Assert.That(material.GetShaderPassEnabled("GBuffer"), Is.True);
                Assert.That(material.GetShaderPassEnabled("ShadowCaster"), Is.True);

                ES3DLitCompositeURPProperties.SetSurfaceMode(material, ES3DLitSurfaceMode.透明混合, 100);
                Assert.That(material.GetFloat(ES3DLitCompositeURPProperties.Surface), Is.EqualTo(1f));
                Assert.That(material.GetFloat(ES3DLitCompositeURPProperties.AlphaClip), Is.Zero);
                Assert.That(material.GetFloat(ES3DLitCompositeURPProperties.SrcBlend),
                    Is.EqualTo((float)UnityEngine.Rendering.BlendMode.SrcAlpha));
                Assert.That(material.GetFloat(ES3DLitCompositeURPProperties.DstBlend),
                    Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                Assert.That(material.GetFloat(ES3DLitCompositeURPProperties.ZWrite), Is.Zero);
                Assert.That(material.GetTag("RenderType", false), Is.EqualTo("Transparent"));
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent + 50));
                Assert.That(material.GetShaderPassEnabled("GBuffer"), Is.False);
                Assert.That(material.GetShaderPassEnabled("ShadowCaster"), Is.False);
                Assert.That(material.GetShaderPassEnabled("DepthOnly"), Is.False);
                Assert.That(material.GetShaderPassEnabled("DepthNormals"), Is.False);
                Assert.That(material.GetShaderPassEnabled("Meta"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void TwoDShader_ExposesRenderer2DAndForwardContracts()
        {
            Shader shader = RequireShader("ES/2D/Composite URP");

            AssertPasses(shader, "ES2DComposite", "NormalsRendering", "ES2DForwardFallback");
            AssertSourcePassLightMode(shader, "ES2DComposite", "Universal2D");
            AssertSourcePassLightMode(shader, "NormalsRendering", "NormalsRendering");
            AssertSourcePassLightMode(shader, "ES2DForwardFallback", "UniversalForward");

            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("CombinedShapeLightShared"));
            Assert.That(source, Does.Contain("USE_SHAPE_LIGHT_TYPE_0"));
            Assert.That(source, Does.Contain("NormalsRenderingShared"));
        }

        [Test]
        public void TwoDShader_ExposesSpriteRendererCompatibilityProperties()
        {
            Shader shader = RequireShader("ES/2D/Composite URP");

            AssertProperties(
                shader,
                "_RendererColor",
                "_Flip",
                "_AlphaTex",
                "_EnableExternalAlpha",
                "_MaskTex",
                "_NormalMap");
        }

        [Test]
        public void QualityKeywords_AreLocalAndMutuallyExclusive()
        {
            AssertTwoDQualityContract("ES2DCompositeURP.shader");
            AssertQualityContract("ES3DLitCompositeURP.shader");
            AssertQualityContract("ES3DVFXCompositeURP.shader");
            AssertQualityContract("ESUICompositeURP.shader");
        }

        [Test]
        public void LitResourceProfile_UsesOneBoundedMaskSetAcrossEveryPass()
        {
            Shader shader = RequireShader("ES/3D/Lit Composite URP");
            AssertProperties(shader, "_ResourceProfile");

            string shaderSource = ReadShaderSource(shader);
            string commonSource = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            const string maskSet = "#pragma shader_feature_local _ _ES_LIT_RESOURCE_MASK_0";
            Assert.That(
                System.Text.RegularExpressions.Regex.Matches(shaderSource, maskSet).Count,
                Is.EqualTo(8),
                "Every Lit pass must compile the same mutually exclusive resource-mask set.");
            Assert.That(shaderSource, Does.Contain("_ES_LIT_RESOURCE_MASK_15"));
            Assert.That(commonSource, Does.Contain("#define ES_LIT_COMPILE_UV_RESOURCES 1"));
            Assert.That(commonSource, Does.Contain("#define ES_LIT_COMPILE_FADE_RESOURCES 1"));
            Assert.That(commonSource, Does.Contain("#define ES_LIT_COMPILE_SURFACE_RESOURCES 1"));
            Assert.That(commonSource, Does.Contain("#define ES_LIT_COMPILE_LAYER_RESOURCES 1"));
            Assert.That(commonSource, Does.Contain("#if defined(ES_LIT_COMPILE_LAYER_RESOURCES)\nTEXTURE2D(_TextureLayer1Texture);"));
            Assert.That(commonSource, Does.Contain("#if defined(ES_LIT_COMPILE_UV_RESOURCES)\nTEXTURE2D(_UVDistortNoiseTex);"));
            Assert.That(commonSource, Does.Contain("TEXTURE2D(_InnerOutlineTintTexture);"));
            Assert.That(commonSource, Does.Contain("TEXTURE2D(_OuterOutlineTintTexture);"));
            Assert.That(commonSource, Does.Contain("TEXTURE2D(_PixelOutlineTintTexture);"));
        }

        [Test]
        public void LitResourceProfile_DefaultIsDynamicAndOptimizedMaskTracksMaterialFeatures()
        {
            Material material = CreateTestMaterial(RequireShader("ES/3D/Lit Composite URP"));
            try
            {
                Assert.That(material.GetFloat("_ResourceProfile"), Is.Zero);
                ES3DLitCompositeURPProperties.RefreshResourceProfile(material);
                AssertResourceMask(material, -1);

                ES3DLitCompositeURPProperties.SetResourceProfile(material, ES3DLitResourceProfile.材质优化);
                AssertResourceMask(material, 0);

                material.SetFloat("_SSUStatusContract", 1f);
                ES3DLitCompositeURPProperties.RefreshResourceProfile(material);
                AssertResourceMask(material, 4);
                material.SetFloat("_SSUStatusContract", 0f);

                material.SetFloat("_EnableUVDistort", 1f);
                ES3DLitCompositeURPProperties.RefreshResourceProfile(material);
                AssertResourceMask(material, 1);

                material.SetFloat("_FadeMode", 1f);
                material.SetFloat("_EnableAddColor", 1f);
                material.SetFloat("_EnableTextureLayer1", 1f);
                ES3DLitCompositeURPProperties.RefreshResourceProfile(material);
                AssertResourceMask(material, 15);

                ES3DLitCompositeURPProperties.SetResourceProfile(material, ES3DLitResourceProfile.动态完整);
                AssertResourceMask(material, -1);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [TestCase("ES/2D/Composite URP", "ES2DCompositeURP.shader", 5)]
        [TestCase("ES/UI/Composite URP", "ESUICompositeURP.shader", 1)]
        public void SpriteResourceProfile_UsesOneBoundedMaskSetAcrossEveryPass(
            string shaderName,
            string fileName,
            int expectedPassCount)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(shader, "_ResourceProfile");

            string source = File.ReadAllText(ShaderRoot + fileName);
            const string maskSet = "#pragma shader_feature_local _ _ES_SPRITE_RESOURCE_MASK_0";
            Assert.That(
                System.Text.RegularExpressions.Regex.Matches(source, maskSet).Count,
                Is.EqualTo(expectedPassCount));
            Assert.That(source, Does.Contain("_ES_SPRITE_RESOURCE_MASK_15"));
            Assert.That(source, Does.Contain("#define ES_SPRITE_COMPILE_UV_RESOURCES 1"));
            Assert.That(source, Does.Contain("#define ES_SPRITE_COMPILE_FADE_RESOURCES 1"));
            Assert.That(source, Does.Contain("#define ES_SPRITE_COMPILE_SURFACE_RESOURCES 1"));
            Assert.That(source, Does.Contain("#define ES_SPRITE_COMPILE_LAYER_RESOURCES 1"));
            Assert.That(source, Does.Contain("#if defined(ES_SPRITE_COMPILE_LAYER_RESOURCES)"));
            Assert.That(source, Does.Contain("#define _TextureLayer1Texture _MainTex"));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteResourceProfile_DefaultIsDynamicAndOptimizedMaskTracksMaterialFeatures(string shaderName)
        {
            Material material = CreateTestMaterial(RequireShader(shaderName));
            try
            {
                Assert.That(material.GetFloat("_ResourceProfile"), Is.Zero);
                RefreshSpriteResourceProfile(material, shaderName);
                AssertSpriteResourceMask(material, -1);

                SetSpriteResourceProfile(material, shaderName, ESSpriteCompositeResourceProfile.材质优化);
                AssertSpriteResourceMask(material, 0);

                material.SetFloat("_SSUStatusContract", 1f);
                RefreshSpriteResourceProfile(material, shaderName);
                AssertSpriteResourceMask(material, 4);
                material.SetFloat("_SSUStatusContract", 0f);

                material.SetFloat("_EnableUVDistort", 1f);
                RefreshSpriteResourceProfile(material, shaderName);
                AssertSpriteResourceMask(material, 1);

                material.SetFloat("_FadeMode", 1f);
                material.SetFloat("_EnableMetal", 1f);
                material.SetFloat("_EnableTextureLayer1", 1f);
                RefreshSpriteResourceProfile(material, shaderName);
                AssertSpriteResourceMask(material, 15);

                SetSpriteResourceProfile(material, shaderName, ESSpriteCompositeResourceProfile.动态完整);
                AssertSpriteResourceMask(material, -1);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        [TestCase("ES/3D/Lit Composite URP")]
        [TestCase("ES/3D/VFX Composite URP")]
        public void DynamicSSUPreparation_SelectsHighQualityBeforeWritingPropertyBlock(string shaderName)
        {
            Material material = CreateTestMaterial(RequireShader(shaderName));
            var block = new MaterialPropertyBlock();
            try
            {
                bool prepared;
                if (shaderName == ES2DCompositeURPProperties.ShaderName)
                {
                    ES2DCompositeURPProperties.SetResourceProfile(material, ESSpriteCompositeResourceProfile.材质优化);
                    prepared = ES2DCompositeURPProperties.TrySetSSUExactContract(material, block, true);
                    AssertSpriteResourceMask(material, -1);
                    Assert.That(material.IsKeywordEnabled("_ES_QUALITY_BASIC"), Is.False);
                    Assert.That(material.IsKeywordEnabled("_ES_QUALITY_STANDARD"), Is.False);
                }
                else if (shaderName == ESUICompositeURPProperties.ShaderName)
                {
                    ESUICompositeURPProperties.SetResourceProfile(material, ESSpriteCompositeResourceProfile.材质优化);
                    prepared = ESUICompositeURPProperties.TrySetSSUExactContract(material, block, true);
                    AssertSpriteResourceMask(material, -1);
                    Assert.That(material.IsKeywordEnabled("_ES_QUALITY_HIGH"), Is.True);
                }
                else if (shaderName == ES3DLitCompositeURPProperties.ShaderName)
                {
                    ES3DLitCompositeURPProperties.SetResourceProfile(material, ES3DLitResourceProfile.材质优化);
                    prepared = ES3DLitCompositeURPProperties.TrySetSSUExactContract(material, block, true);
                    AssertResourceMask(material, -1);
                    Assert.That(material.IsKeywordEnabled("_ES_QUALITY_HIGH"), Is.True);
                }
                else
                {
                    prepared = ES3DVFXCompositeURPProperties.TrySetSSUExactContract(material, block, true);
                    Assert.That(material.IsKeywordEnabled("_ES_QUALITY_HIGH"), Is.True);
                }

                Assert.That(prepared, Is.True);
                Assert.That(material.GetFloat("_QualityTier"), Is.EqualTo(2f));
                Assert.That(block.GetFloat(ESCompositeURPProperties.SSUExactContract), Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void LitCodingHelper_RoutesAdvancedSurfacePropertiesToTypedIds()
        {
            string source = File.ReadAllText(EditorShaderRoot + "ESCompositeCodingHelper.Generation.cs");
            Assert.That(source, Does.Contain("bool litAdvancedSurfaceProperty = shader.name == \"ES/3D/Lit Composite URP\""));
            Assert.That(source, Does.Contain("return \"ES3DLitCompositeURPProperties.\" + ToPascal(propertyName);"));
            Assert.That(source, Does.Contain("case \"InnerOutlineDistortionToggle\": return \"InnerOutlineDistortionEnabled\";"));
            Assert.That(source, Does.Contain("case \"OuterOutlineTextureToggle\": return \"OuterOutlineTextureEnabled\";"));
            Assert.That(source, Does.Contain("case \"PixelOutlineOutlineOnlyToggle\": return \"PixelOutlineOnly\";"));
        }

        [Test]
        public void CompositeInspector_UsesWeightedTextureSampleBudgetInsteadOfEffectCount()
        {
            string source = File.ReadAllText(EditorShaderRoot + "ESCompositeShaderGUI.Diagnostics.cs");
            Assert.That(source, Does.Contain("DrawTextureSampleBudgetDiagnostics(editor, shaderName);"));
            Assert.That(source, Does.Contain("EstimateAdditionalTextureSamples"));
            Assert.That(source, Does.Contain("EstimateBaseTextureSamples"));
            Assert.That(source, Does.Contain("EstimateFadeSamples"));
            Assert.That(source, Does.Contain("EstimateSurfaceEffectSamples"));
            Assert.That(source, Does.Contain("EstimateStatusSamples"));
            Assert.That(source, Does.Contain("EstimateOutlineSamples"));
            Assert.That(source, Does.Contain("不是 GPU Profiler 实测"));
            Assert.That(source, Does.Not.Contain("if (enabledCost >= 6) heavyCombinationCount++;"));
            Assert.That(source, Does.Contain("DrawLitResourceDiagnostics(editor);"));
            Assert.That(source, Does.Contain("material.GetFloat(\"_ResourceProfile\") < 0.5f"));
            Assert.That(source, Does.Contain("目标 GLES3/Vulkan 设备"));
            Assert.That(source, Does.Contain("for (int i = 0; i < editor.targets.Length; i++)"));
        }

        [Test]
        public void TextureSampleBudget_WeightsBlurModeAndNoOpControls()
        {
            System.Reflection.MethodInfo estimate = typeof(ES.EditorInternal.ESCompositeShaderGUI).GetMethod(
                "EstimateAdditionalTextureSamples",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(estimate, Is.Not.Null);

            Material material = CreateTestMaterial(RequireShader("ES/2D/Composite URP"));
            try
            {
                material.SetFloat("_QualityTier", 2f);
                material.SetFloat("_EnableBlur", 1f);
                material.SetFloat("_BlurIntensity", 1f);
                material.SetFloat("_BlurRadius", 0.01f);
                material.SetFloat("_BlurMode", 0f);
                Assert.That(
                    (int)estimate.Invoke(null, new object[] { material, material.shader.name }),
                    Is.EqualTo(4));

                material.SetFloat("_BlurMode", 1f);
                Assert.That(
                    (int)estimate.Invoke(null, new object[] { material, material.shader.name }),
                    Is.EqualTo(8));

                material.SetFloat("_EnableChromatic", 1f);
                material.SetFloat("_ChromaticIntensity", 1f);
                material.SetFloat("_ChromaticOffset", 0.01f);
                Assert.That(
                    (int)estimate.Invoke(null, new object[] { material, material.shader.name }),
                    Is.EqualTo(10));

                material.SetFloat("_BlurIntensity", 0f);
                Assert.That(
                    (int)estimate.Invoke(null, new object[] { material, material.shader.name }),
                    Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void TextureSampleBudget_CountsFadeModesAndDirectionalDistortion()
        {
            System.Reflection.MethodInfo estimate = typeof(ES.EditorInternal.ESCompositeShaderGUI).GetMethod(
                "EstimateAdditionalTextureSamples",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Material material = CreateTestMaterial(RequireShader("ES/2D/Composite URP"));
            try
            {
                material.SetFloat("_EnableDirectionalDistortion", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(3));

                material.SetFloat("_EnableDirectionalDistortion", 0f);
                material.SetFloat("_FadeMode", 2f);
                material.SetFloat("_FadeNoiseFactor", 0f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(1));

                material.SetFloat("_FadeNoiseFactor", 0.2f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void TextureSampleBudget_CountsPatternAndMaterialEffects()
        {
            System.Reflection.MethodInfo estimate = typeof(ES.EditorInternal.ESCompositeShaderGUI).GetMethod(
                "EstimateAdditionalTextureSamples",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Material material = CreateTestMaterial(RequireShader("ES/2D/Composite URP"));
            try
            {
                material.SetFloat("_EnableCamouflage", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(2));
                material.SetFloat("_CamouflageAnimationToggle", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(3));

                material.SetFloat("_EnableCamouflage", 0f);
                material.SetFloat("_EnableMetal", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(2));
                material.SetFloat("_MetalMaskToggle", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(3));

                material.SetFloat("_EnableMetal", 0f);
                material.SetFloat("_MetalMaskToggle", 0f);
                material.SetFloat("_EnableEnchanted", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void TextureSampleBudget_DistinguishesExactAndSharedLegacyStatusNoise()
        {
            System.Reflection.MethodInfo estimate = typeof(ES.EditorInternal.ESCompositeShaderGUI).GetMethod(
                "EstimateAdditionalTextureSamples",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Material material = CreateTestMaterial(RequireShader("ES/2D/Composite URP"));
            try
            {
                material.SetFloat("_SSUStatusContract", 1f);
                material.SetFloat("_EnableFrozen", 1f);
                material.SetFloat("_EnableBurn", 1f);
                material.SetFloat("_EnableRainbow", 1f);
                material.SetFloat("_EnablePoison", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(8));

                material.SetFloat("_SSUStatusContract", 0f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(1));
                material.SetFloat("_EnableDistortion", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void TextureSampleBudget_ModelsExactAndLegacyOutlineFamilies()
        {
            System.Reflection.MethodInfo estimate = typeof(ES.EditorInternal.ESCompositeShaderGUI).GetMethod(
                "EstimateAdditionalTextureSamples",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Material material = CreateTestMaterial(RequireShader("ES/2D/Composite URP"));
            try
            {
                material.SetFloat("_QualityTier", 2f);
                material.SetFloat("_SSUStatusContract", 1f);
                material.SetFloat("_EnableInnerOutline", 1f);
                material.SetFloat("_InnerOutlineFade", 1f);
                material.SetFloat("_InnerOutlineDistortionToggle", 1f);
                material.SetFloat("_InnerOutlineTextureToggle", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(10));

                material.SetFloat("_QualityTier", 0f);
                material.SetFloat("_SSUStatusContract", 0f);
                material.SetFloat("_EnableOuterOutline", 1f);
                material.SetFloat("_OuterOutlineFade", 1f);
                material.SetFloat("_EnablePixelOutline", 1f);
                material.SetFloat("_PixelOutlineFade", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void TextureSampleBudget_CountsUIStandardExactHologramAndGlitch()
        {
            System.Reflection.MethodInfo estimate = typeof(ES.EditorInternal.ESCompositeShaderGUI).GetMethod(
                "EstimateAdditionalTextureSamples",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Material material = CreateTestMaterial(RequireShader("ES/UI/Composite URP"));
            try
            {
                material.SetFloat("_QualityTier", 1f);
                material.SetFloat("_SSUStatusContract", 1f);
                material.SetFloat("_EnableHologram", 1f);
                material.SetFloat("_EnableGlitch", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void TextureSampleBudget_UsesEightNeighbourLegacyLitPixelOutline()
        {
            System.Reflection.MethodInfo estimate = typeof(ES.EditorInternal.ESCompositeShaderGUI).GetMethod(
                "EstimateAdditionalTextureSamples",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Material material = CreateTestMaterial(RequireShader("ES/3D/Lit Composite URP"));
            try
            {
                material.SetFloat("_QualityTier", 2f);
                material.SetFloat("_EnablePixelOutline", 1f);
                material.SetFloat("_PixelOutlineFade", 1f);
                material.SetFloat("_SSUStatusContract", 0f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(8));

                material.SetFloat("_SSUStatusContract", 1f);
                Assert.That((int)estimate.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void TextureSampleBudget_SeparatesUniversal2DBaseAndEtc1AlphaReads()
        {
            System.Reflection.MethodInfo estimateBase = typeof(ES.EditorInternal.ESCompositeShaderGUI).GetMethod(
                "EstimateBaseTextureSamples",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Material material = CreateTestMaterial(RequireShader("ES/2D/Composite URP"));
            try
            {
                Assert.That((int)estimateBase.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(2));
                material.EnableKeyword("ETC1_EXTERNAL_ALPHA");
                Assert.That((int)estimateBase.Invoke(null, new object[] { material, material.shader.name }), Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ShaderSamplingHotPaths_AvoidLegacyStatusAndFadeStackDuplicateReads()
        {
            string spriteSource = File.ReadAllText(ShaderRoot + "ES2DCompositeURP.shader");
            Assert.That(spriteSource, Does.Contain("|| (_SSUStatusContract <= 0.5"));
            Assert.That(spriteSource, Does.Contain(
                "&& (_EnableFrozen > 0.5 || _EnableBurn > 0.5 || _EnablePoison > 0.5))"));

            string litSource = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            Assert.That(CountOccurrences(litSource, "ESCompositeApplySSUFadeStackColor("), Is.EqualTo(2));
            Assert.That(litSource, Does.Contain("dissolveAlpha *= ssuFadeVisibility;"));
        }

        [Test]
        public void TextureSampleBudget_CountsCombinedVfxDepthReadOnce()
        {
            System.Reflection.MethodInfo estimate = typeof(ES.EditorInternal.ESCompositeShaderGUI).GetMethod(
                "EstimateAdditionalTextureSamples",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Material material = CreateTestMaterial(RequireShader("ES/3D/VFX Composite URP"));
            try
            {
                material.SetFloat("_QualityTier", 2f);
                material.SetFloat("_Distortion", 0f);
                material.SetFloat("_EnableSoftParticles", 1f);
                material.SetFloat("_EnableDepthIntersection", 1f);
                Assert.That(
                    (int)estimate.Invoke(null, new object[] { material, material.shader.name }),
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void TwoDQuality_DefaultsToHighWithoutKeywordAndSupportsExplicitTiers()
        {
            Material material = CreateTestMaterial(RequireShader("ES/2D/Composite URP"));

            try
            {
                Assert.That(material.GetFloat("_QualityTier"), Is.EqualTo(2f));
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_BASIC"), Is.False);
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_STANDARD"), Is.False);

                ES2DCompositeURPProperties.SetQuality(material, ESCompositeQualityTier.基础);
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_BASIC"), Is.True);
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_STANDARD"), Is.False);

                ES2DCompositeURPProperties.SetQuality(material, ESCompositeQualityTier.标准);
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_BASIC"), Is.False);
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_STANDARD"), Is.True);

                ES2DCompositeURPProperties.SetQuality(material, ESCompositeQualityTier.高质量);
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_BASIC"), Is.False);
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_STANDARD"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteShaders_ExposeStylizationContract(string shaderName)
        {
            AssertProperties(
                RequireShader(shaderName),
                "_EnablePixelate",
                "_PixelateCells",
                "_PixelateStrength",
                "_EnablePalette",
                "_PaletteTex",
                "_PaletteRow",
                "_PaletteStrength",
                "_EnableHalftone",
                "_HalftoneScale",
                "_HalftoneAngle",
                "_HalftoneStrength",
                "_HalftonePosition",
                "_HalftoneFade",
                "_HalftoneFadeWidth",
                "_HalftoneInvert",
                "_HalftoneAlphaPattern",
                "_QualityTier");
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/3D/Lit Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void CompositeShaders_ExposeCompleteSsuTintContract(string shaderName)
        {
            AssertProperties(
                RequireShader(shaderName),
                "_EnableAddColor", "_AddColor", "_AddColorFade",
                "_AddColorContrastToggle", "_AddColorContrast", "_AddColorMaskToggle", "_AddColorMask",
                "_EnableStrongTint", "_StrongTint", "_StrongTintFade",
                "_StrongTintContrastToggle", "_StrongTintContrast", "_StrongTintMaskToggle", "_StrongTintMask");

            string source = shaderName == "ES/3D/Lit Composite URP"
                ? File.ReadAllText(ShaderRoot + "ES3DLitCompositeSSUSurface.hlsl")
                : ReadShaderSource(RequireShader(shaderName));
            Assert.That(source, Does.Contain("_AddColorContrastToggle"));
            Assert.That(source, Does.Contain("_AddColorMaskToggle"));
            Assert.That(source, Does.Contain("_StrongTintContrastToggle"));
            Assert.That(source, Does.Contain("_StrongTintMaskToggle"));
            if (shaderName == "ES/3D/Lit Composite URP")
            {
                Assert.That(source, Does.Contain("sampler_BaseMap"));
                Assert.That(source, Does.Not.Contain("SAMPLER(sampler_AddColorMask)"));
                Assert.That(source, Does.Not.Contain("SAMPLER(sampler_StrongTintMask)"));
            }
            else
            {
                Assert.That(source, Does.Contain("#define sampler_AddColorMask sampler_MainTex"));
                Assert.That(source, Does.Contain("#define sampler_StrongTintMask sampler_MainTex"));
            }
        }

        [TestCase("ES/2D/Composite URP", "ES2DCompositeURP.shader")]
        [TestCase("ES/3D/Lit Composite URP", "ES3DLitCompositeURPCommon.hlsl")]
        [TestCase("ES/UI/Composite URP", "ESUICompositeURP.shader")]
        public void CompositeShaders_ExposeDualNoiseFullDistortionContract(string shaderName, string implementationFile)
        {
            AssertProperties(
                RequireShader(shaderName),
                "_EnableFullDistortion",
                "_FullDistortionFade",
                "_FullDistortionDistortion",
                "_FullDistortionNoiseScale");

            string source = File.ReadAllText(ShaderRoot + implementationFile);
            Assert.That(source, Does.Contain("float fullNoiseX"));
            Assert.That(source, Does.Contain("float fullNoiseY"));
            Assert.That(source, Does.Contain("(uv + 0.321)").Or.Contain("(coordinate + 0.321)").Or.Contain("(fadeCoordinate + 0.321)"));
            Assert.That(source, Does.Contain("* _FullDistortionDistortion.xy"));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteShaders_ExposeSharedUVAndSplitToneContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_EnableUVTransform",
                "_UVPivot",
                "_UVScale",
                "_UVOffset",
                "_UVRotation",
                "_UVRotationSpeed",
                "_EnableUVDistort",
                "_UVDistortFrequency",
                "_UVDistortSpeed",
                "_UVDistortAmount",
                "_UVDistortNoiseTex",
                "_UVDistortFrom",
                "_UVDistortTo",
                "_UVDistortFade",
                "_UVDistortMaskToggle",
                "_UVDistortMask",
                "_UVDistortMaskChannel",
                "_EnableSplitToning",
                "_SplitToneShadows",
                "_SplitToneHighlights",
                "_SplitToneBalance",
                "_SplitToneStrength");
            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("ESCompositeColorTransform.hlsl"));
            Assert.That(source, Does.Contain("ESCompositeSplitTone"));
        }

        [Test]
        public void SharedTransformParameters_ClampUnsafeValues()
        {
            var block = new MaterialPropertyBlock();
            ESCompositeURPProperties.SetUVTransform(
                block,
                true,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -2f),
                Vector2.one,
                540f,
                true,
                new Vector2(0f, -3f),
                new Vector2(1f, -1f),
                1f,
                45f);
            ESCompositeURPProperties.SetSplitToning(
                block,
                true,
                Color.black,
                Color.white,
                2f,
                -1f);

            Vector4 scale = block.GetVector(ESCompositeURPProperties.UVScale);
            Vector4 frequency = block.GetVector(ESCompositeURPProperties.UVDistortFrequency);
            Assert.That(scale.x, Is.EqualTo(0.0001f).Within(0.000001f));
            Assert.That(scale.y, Is.EqualTo(-2f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.UVRotation), Is.EqualTo(-180f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.UVRotationSpeed), Is.EqualTo(45f));
            Assert.That(frequency.x, Is.EqualTo(0.001f).Within(0.000001f));
            Assert.That(frequency.y, Is.EqualTo(3f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.UVDistortAmount), Is.EqualTo(0.2f));
            Assert.That(block.GetVector(ESCompositeURPProperties.UVDistortFrom).x, Is.EqualTo(-0.02f));
            Assert.That(block.GetVector(ESCompositeURPProperties.UVDistortTo).x, Is.EqualTo(0.02f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.UVDistortFade), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.UVDistortMaskEnabled), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.SplitToneBalance), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SplitToneStrength), Is.Zero);
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteShaders_ExposeSSUColorAndShadowContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_EnableShadow", "_ShadowFade", "_ShadowOffset", "_ShadowColor",
                "_EnableBlackTint", "_BlackTintFade", "_BlackTintColor", "_BlackTintPower",
                "_EnableInkSpread", "_InkSpreadFade", "_InkSpreadColor", "_InkSpreadContrast",
                "_InkSpreadDistance", "_InkSpreadPosition", "_InkSpreadWidth",
                "_InkSpreadNoiseScale", "_InkSpreadNoiseFactor",
                "_EnableShiftHue", "_ShiftHueSpeed",
                "_EnableAddHue", "_AddHueFade", "_AddHueSpeed", "_AddHueBrightness",
                "_AddHueSaturation", "_AddHueContrast", "_AddHueMaskToggle", "_AddHueMask",
                "_EnableSineGlow", "_SineGlowFade", "_SineGlowColor", "_SineGlowContrast",
                "_SineGlowFrequency", "_SineGlowMin", "_SineGlowMax",
                "_SineGlowMaskToggle", "_SineGlowMask");

            string source = ReadShaderSource(shader);
            int blackTint = source.IndexOf("if (_EnableBlackTint > 0.5)", System.StringComparison.Ordinal);
            int inkSpread = source.IndexOf("if (_EnableInkSpread > 0.5)", System.StringComparison.Ordinal);
            int shiftHue = source.IndexOf("if (_EnableShiftHue > 0.5)", System.StringComparison.Ordinal);
            int addHue = source.IndexOf("if (_EnableAddHue > 0.5)", System.StringComparison.Ordinal);
            int sineGlow = source.IndexOf("if (_EnableSineGlow > 0.5)", System.StringComparison.Ordinal);
            int spriteShadow = source.IndexOf("if (_EnableShadow > 0.5)", System.StringComparison.Ordinal);

            Assert.That(blackTint, Is.GreaterThanOrEqualTo(0));
            Assert.That(inkSpread, Is.GreaterThan(blackTint));
            Assert.That(shiftHue, Is.GreaterThan(inkSpread));
            Assert.That(addHue, Is.GreaterThan(shiftHue));
            Assert.That(sineGlow, Is.GreaterThan(addHue));
            Assert.That(spriteShadow, Is.GreaterThan(sineGlow));
            Assert.That(source.IndexOf("_UberNoiseTexture,", inkSpread, System.StringComparison.Ordinal),
                Is.InRange(inkSpread, shiftHue - 1));
            Assert.That(source.IndexOf("_AddHueMask,", addHue, System.StringComparison.Ordinal),
                Is.InRange(addHue, sineGlow - 1));
            Assert.That(source.IndexOf("_SineGlowMask,", sineGlow, System.StringComparison.Ordinal),
                Is.InRange(sineGlow, spriteShadow - 1));
            Assert.That(source.IndexOf("ESCompositeApplySpriteShadow", spriteShadow, System.StringComparison.Ordinal),
                Is.GreaterThan(spriteShadow));
            Assert.That(source.IndexOf("fadeVisibility", spriteShadow, System.StringComparison.Ordinal),
                Is.GreaterThan(spriteShadow));
            if (shaderName == "ES/UI/Composite URP")
                Assert.That(source, Does.Contain("ESResolveUIShadowAlpha(shadowSourceAlpha, input.sdfData)"));
        }

        [Test]
        public void SSUColorAndShadowParameters_ClampValuesAndShareIds()
        {
            var block = new MaterialPropertyBlock();
            ESCompositeURPProperties.SetSpriteShadow(block, true, new Vector2(99f, -99f), Color.black, 2f);
            ESCompositeURPProperties.SetBlackTint(block, true, Color.blue, 0f, -1f);
            ESCompositeURPProperties.SetInkSpread(
                block, true, null, Color.yellow, 0f, 2f, 99f, Vector2.one, 0f, Vector2.zero, 99f);
            ESCompositeURPProperties.SetShiftHue(block, true, -99f);
            ESCompositeURPProperties.SetAddHue(
                block, true, 2f, 99f, 99f, -1f, 0f, null, Vector2.zero, Vector2.one);
            ESCompositeURPProperties.SetSineGlow(
                block, true, Color.cyan, 0f, -99f, -99f, 99f, 2f, null, Vector2.zero, Vector2.one);

            Vector4 shadowOffset = block.GetVector(ESCompositeURPProperties.SpriteShadowOffset);
            Assert.That(shadowOffset.x, Is.EqualTo(32f));
            Assert.That(shadowOffset.y, Is.EqualTo(-32f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SpriteShadowFade), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.BlackTintPower), Is.EqualTo(0.001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.BlackTintFade), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.InkSpreadContrast), Is.EqualTo(0.001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.InkSpreadFade), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.InkSpreadDistance), Is.EqualTo(32f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.InkSpreadWidth), Is.EqualTo(0.001f));
            Assert.That(block.GetVector(ESCompositeURPProperties.InkSpreadNoiseScale).x, Is.EqualTo(0.001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.InkSpreadNoiseFactor), Is.EqualTo(4f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.ShiftHueSpeed), Is.EqualTo(-32f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.AddHueSpeed), Is.EqualTo(32f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.AddHueBrightness), Is.EqualTo(16f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.AddHueSaturation), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.AddHueContrast), Is.EqualTo(0.001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.AddHueMaskEnabled), Is.Zero);
            Assert.That(block.GetVector(ESCompositeURPProperties.AddHueMaskScaleOffset).x, Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SineGlowContrast), Is.EqualTo(0.001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SineGlowFrequency), Is.EqualTo(-32f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SineGlowMin), Is.EqualTo(-8f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SineGlowMax), Is.EqualTo(8f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SineGlowFade), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SineGlowMaskEnabled), Is.Zero);
            Assert.That(ES2DCompositeURPProperties.SpriteShadowEnabled,
                Is.EqualTo(ESCompositeURPProperties.SpriteShadowEnabled));
            Assert.That(ESUICompositeURPProperties.BlackTintEnabled,
                Is.EqualTo(ESCompositeURPProperties.BlackTintEnabled));
            Assert.That(ES2DCompositeURPProperties.AddHueMaskScaleOffset,
                Is.EqualTo(ESUICompositeURPProperties.AddHueMaskScaleOffset));
            Assert.That(ESUICompositeURPProperties.SineGlowEnabled,
                Is.EqualTo(ESCompositeURPProperties.SineGlowEnabled));
        }

        [Test]
        public void SSUColorTransform_UsesCompatibleLuminanceAndSineWave()
        {
            string source = File.ReadAllText(ShaderRoot + "ESCompositeColorTransform.hlsl");
            Assert.That(source, Does.Contain("(color.r * 2.0 + color.g * 3.0 + color.b) / 6.0"));
            Assert.That(source, Does.Contain("(sin(timeValue * frequency) + 1.0) * (maximum - minimum) + minimum"));
            Assert.That(source, Does.Contain("pow(luminance, max(contrast, 0.001))"));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        [TestCase("ES/3D/Lit Composite URP")]
        public void CompositeShaders_ExposeCompleteSSUStatusEffectContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_SSUStatusContract",
                "_FrozenFade", "_FrozenTint", "_FrozenContrast", "_FrozenSnowColor",
                "_FrozenSnowContrast", "_FrozenSnowDensity", "_FrozenSnowScale",
                "_FrozenHighlightColor", "_FrozenHighlightContrast", "_FrozenHighlightDensity",
                "_FrozenHighlightSpeed", "_FrozenHighlightScale", "_FrozenHighlightDistortion",
                "_FrozenHighlightDistortionSpeed", "_FrozenHighlightDistortionScale",
                "_BurnFade", "_BurnPosition", "_BurnRadius", "_BurnEdgeColor", "_BurnWidth",
                "_BurnEdgeNoiseScale", "_BurnEdgeNoiseFactor", "_BurnInsideColor", "_BurnInsideContrast",
                "_BurnInsideNoiseColor", "_BurnInsideNoiseFactor", "_BurnInsideNoiseScale",
                "_BurnSwirlFactor", "_BurnSwirlNoiseScale",
                "_RainbowFade", "_RainbowBrightness", "_RainbowSaturation", "_RainbowContrast",
                "_RainbowSpeed", "_RainbowDensity", "_RainbowCenter", "_RainbowNoiseScale", "_RainbowNoiseFactor",
                "_ShineFade", "_ShineColor", "_ShineSaturation", "_ShineContrast", "_ShineWidth",
                "_ShineSpeed", "_ShineRotation", "_ShineSmooth", "_ShineFrequency", "_ShineMaskToggle", "_ShineMask",
                "_PoisonFade", "_PoisonColor", "_PoisonDensity", "_PoisonRecolorFactor",
                "_PoisonShiftSpeed", "_PoisonNoiseBrightness", "_PoisonNoiseScale", "_PoisonNoiseSpeed");
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        [TestCase("ES/3D/Lit Composite URP")]
        public void CompositeShaders_ExposeSSUExactStylizedContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_SSUStatusContract",
                "_EnableInnerOutline", "_InnerOutlineFade", "_InnerOutlineColor", "_InnerOutlineWidth",
                "_InnerOutlineDistortionToggle", "_InnerOutlineDistortionIntensity",
                "_InnerOutlineNoiseScale", "_InnerOutlineNoiseSpeed", "_InnerOutlineTextureToggle",
                "_InnerOutlineTintTexture", "_InnerOutlineTextureSpeed", "_InnerOutlineOutlineOnlyToggle",
                "_EnableOuterOutline", "_OuterOutlineFade", "_OuterOutlineColor", "_OuterOutlineWidth",
                "_OuterOutlineDistortionToggle", "_OuterOutlineDistortionIntensity",
                "_OuterOutlineNoiseScale", "_OuterOutlineNoiseSpeed", "_OuterOutlineTextureToggle",
                "_OuterOutlineTintTexture", "_OuterOutlineTextureSpeed", "_OuterOutlineOutlineOnlyToggle",
                "_EnablePixelOutline", "_PixelOutlineFade", "_PixelOutlineColor", "_PixelOutlineWidth",
                "_PixelOutlineTextureToggle", "_PixelOutlineTintTexture", "_PixelOutlineTextureSpeed",
                "_PixelOutlineOutlineOnlyToggle",
                "_EnableHologram", "_HologramFade", "_HologramColor", "_HologramContrast",
                "_HologramSpace", "_HologramDirection", "_HologramLineFrequency", "_HologramLineGap", "_HologramSpeed",
                "_HologramMinAlpha", "_HologramDistortionOffset", "_HologramDistortionDirection", "_HologramDistortionSpeed",
                "_HologramDistortionDensity", "_HologramDistortionScale",
                "_EnableGlitch", "_GlitchFade", "_GlitchMaskMin", "_GlitchMaskScale",
                "_GlitchScanDirection",
                "_GlitchMaskSpeed", "_GlitchHueSpeed", "_GlitchBrightness",
                "_GlitchNoiseScale", "_GlitchNoiseSpeed", "_GlitchDistortion",
                "_GlitchDistortionScale", "_GlitchDistortionSpeed");

            string source = ReadShaderSource(shader);
            string stylizedSource = shaderName == "ES/3D/Lit Composite URP"
                ? File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl")
                : source;
            Assert.That(stylizedSource, Does.Contain("#include \"ESCompositeSSUStylizedEffects.hlsl\""));
            Assert.That(stylizedSource, Does.Contain("ESCompositeApplySSUHologramUV"));
            Assert.That(stylizedSource, Does.Contain("ESCompositeApplySSUGlitchUV"));
            Assert.That(stylizedSource, Does.Contain("ESCompositeApplySSUHologramColor"));
            Assert.That(stylizedSource, Does.Contain("ESCompositeApplySSUGlitchColor"));
            if (shaderName == "ES/3D/Lit Composite URP")
            {
                Assert.That(stylizedSource, Does.Contain("ESLitSSUMinNeighbourAlpha8"));
                Assert.That(stylizedSource, Does.Contain("ESLitSSUMaxNeighbourAlpha8"));
                Assert.That(stylizedSource, Does.Contain("ESLitSSUMaxNeighbourAlpha4"));
            }
            else
            {
                Assert.That(source, Does.Contain("ESMinOutlineAlpha8"));
                Assert.That(source, Does.Contain("ESMaxOutlineAlpha8"));
                Assert.That(source, Does.Contain("ESMaxOutlineAlpha4"));
                Assert.That(source, Does.Contain("ESApplySSUExactOutlines"));
                Assert.That(source, Does.Match(
                    @"ESCompositeApplySSUHologramUV\([\s\S]{0,200}_MainTex_TexelSize\.z,"));
            }
        }

        [Test]
        public void VFXShader_UsesSharedExactStylizedContractInsideAtlasBoundaries()
        {
            Shader shader = RequireShader("ES/3D/VFX Composite URP");
            AssertProperties(
                shader,
                "_SSUStatusContract",
                "_EnableHologram", "_HologramFade", "_HologramColor", "_HologramContrast",
                "_HologramSpace", "_HologramLineFrequency", "_HologramLineGap", "_HologramSpeed",
                "_HologramMinAlpha", "_HologramDistortionOffset", "_HologramDistortionSpeed",
                "_HologramDistortionDensity", "_HologramDistortionScale",
                "_EnableGlitch", "_GlitchFade", "_GlitchMaskMin", "_GlitchMaskScale",
                "_GlitchMaskSpeed", "_GlitchHueSpeed", "_GlitchBrightness",
                "_GlitchNoiseScale", "_GlitchNoiseSpeed", "_GlitchDistortion",
                "_GlitchDistortionScale", "_GlitchDistortionSpeed");

            string source = File.ReadAllText(ShaderRoot + "ES3DVFXCompositeURPCommon.hlsl");
            int fragment = source.IndexOf("half4 ES3DVFXFragment", System.StringComparison.Ordinal);
            int customData = source.IndexOf("float4 customData1 = input.customData1", fragment, System.StringComparison.Ordinal);
            int sequence = source.IndexOf("uv = ESApplySequenceUV", customData, System.StringComparison.Ordinal);
            int flow = source.IndexOf("uv = ESApplyFlowMap(uv, timeValue);", sequence, System.StringComparison.Ordinal);
            int coordinate = source.IndexOf("float2 stylizedCoordinate = ESSequenceLocalCoordinate", flow, System.StringComparison.Ordinal);
            int hologramUV = source.IndexOf("ESCompositeApplySSUHologramUV", coordinate, System.StringComparison.Ordinal);
            int glitchUV = source.IndexOf("ESCompositeApplySSUGlitchUV", hologramUV, System.StringComparison.Ordinal);
            int atlasWrap = source.IndexOf("uv = ESWrapSequenceUV(uv, atlasBounds);", glitchUV, System.StringComparison.Ordinal);
            int sample = source.IndexOf("half4 source = SAMPLE_TEXTURE2D", atlasWrap, System.StringComparison.Ordinal);
            int chromatic = source.IndexOf("if (_EnableChromatic > 0.5", sample, System.StringComparison.Ordinal);
            int hologramColor = source.IndexOf("ESCompositeApplySSUHologramColor", chromatic, System.StringComparison.Ordinal);
            int glitchColor = source.IndexOf("ESCompositeApplySSUGlitchColor", hologramColor, System.StringComparison.Ordinal);
            int radialMask = source.IndexOf("if (_EnableRadialMask > 0.5)", glitchColor, System.StringComparison.Ordinal);
            int dissolve = source.IndexOf("float dissolveProgress =", radialMask, System.StringComparison.Ordinal);
            int rim = source.IndexOf("if (_EnableRim > 0.5 || _EnableFresnelMask > 0.5)", dissolve, System.StringComparison.Ordinal);
            int shine = source.IndexOf("if (_EnableShine > 0.5)", rim, System.StringComparison.Ordinal);
            int depth = source.IndexOf("if (_EnableSoftParticles > 0.5 || _EnableDepthIntersection > 0.5)", shine, System.StringComparison.Ordinal);
            int sparkle = source.IndexOf("if (_EnableSparkle > 0.5)", depth, System.StringComparison.Ordinal);
            int emission = source.IndexOf("float emissionMultiplier =", sparkle, System.StringComparison.Ordinal);
            int alphaClip = source.IndexOf("if (_AlphaClip > 0.5)", emission, System.StringComparison.Ordinal);

            Assert.That(source, Does.Contain("#include \"ESCompositeSSUStylizedEffects.hlsl\""));
            Assert.That(source, Does.Contain("#define _UberNoiseTexture _NoiseTex"));
            Assert.That(customData, Is.GreaterThan(fragment));
            Assert.That(sequence, Is.GreaterThan(customData));
            Assert.That(flow, Is.GreaterThan(sequence));
            Assert.That(coordinate, Is.GreaterThan(flow));
            Assert.That(hologramUV, Is.GreaterThan(coordinate));
            Assert.That(glitchUV, Is.GreaterThan(hologramUV));
            Assert.That(atlasWrap, Is.GreaterThan(glitchUV));
            Assert.That(sample, Is.GreaterThan(atlasWrap));
            Assert.That(chromatic, Is.GreaterThan(sample));
            Assert.That(hologramColor, Is.GreaterThan(chromatic));
            Assert.That(glitchColor, Is.GreaterThan(hologramColor));
            Assert.That(radialMask, Is.GreaterThan(glitchColor));
            Assert.That(dissolve, Is.GreaterThan(radialMask));
            Assert.That(rim, Is.GreaterThan(dissolve));
            Assert.That(shine, Is.GreaterThan(rim));
            Assert.That(depth, Is.GreaterThan(shine));
            Assert.That(sparkle, Is.GreaterThan(depth));
            Assert.That(emission, Is.GreaterThan(sparkle));
            Assert.That(alphaClip, Is.GreaterThan(emission));
            Assert.That(source, Does.Contain("_SSUStatusContract <= 0.5 && _EnableHologram > 0.5"));
        }

        [Test]
        public void VFXShader_SeparatesLegacyAndExactAuthoringContracts()
        {
            string shaderSource = File.ReadAllText(ShaderRoot + "ES3DVFXCompositeURP.shader");
            string commonSource = File.ReadAllText(ShaderRoot + "ES3DVFXCompositeURPCommon.hlsl");
            string metadataSource = File.ReadAllText(EditorShaderRoot + "ESCompositeShaderGUI.Metadata.cs");
            string productivitySource = File.ReadAllText(EditorShaderRoot + "ESCompositeShaderGUI.Productivity.cs");
            var block = new MaterialPropertyBlock();
            var exactBlock = new MaterialPropertyBlock();

            ES3DVFXCompositeURPProperties.SetHologram(
                block, true, Color.cyan, -10f, 2f, 256f, 2f);
            ES3DVFXCompositeURPProperties.SetGlitch(block, true, 2f, -256f);
            ES3DVFXCompositeURPProperties.SetSSUExactHologram(
                exactBlock, true, Color.magenta, 72f, 0.4f, 3f, 0.6f,
                0.8f, 2f, ES3DLitHologramSpace.局部UV,
                0.25f, 4f, 0.75f, 12f);
            ES3DVFXCompositeURPProperties.SetSSUExactGlitch(
                exactBlock, true, 2f, -1f,
                new Vector2(2f, 3f), new Vector2(4f, 5f),
                6f, 7f,
                new Vector2(8f, 9f), new Vector2(10f, 11f),
                new Vector2(12f, 13f), new Vector2(14f, 15f), new Vector2(16f, 17f));

            Assert.That(shaderSource, Does.Contain("[Toggle] _SSUStatusContract"));
            Assert.That(shaderSource, Does.Not.Contain("[HideInInspector] _HologramFade"));
            Assert.That(shaderSource, Does.Not.Contain("_GlitchIntensity (\"SSU 故障强度\""));
            Assert.That(commonSource, Does.Not.Contain("float _GlitchIntensity;"));
            Assert.That(metadataSource, Does.Contain("bool vfx = shaderName == \"ES/3D/VFX Composite URP\";"));
            Assert.That(metadataSource, Does.Contain("bool stylizedContractShader = spriteOrUI || vfx;"));
            Assert.That(metadataSource, Does.Contain("bool keepActiveShineProperty = property.name == \"_ShineDirection\""));
            Assert.That(metadataSource, Does.Contain("vfx && property.name == \"_ShineIntensity\""));
            Assert.That(metadataSource, Does.Contain("&& !keepActiveShineProperty) return false;"));
            Assert.That(metadataSource, Does.Contain("vfx && property.name == \"_HologramMinAlpha\""));
            int presetStart = productivitySource.IndexOf("\"vfx.hologram\"", System.StringComparison.Ordinal);
            int presetEnd = productivitySource.IndexOf(
                "private static readonly Dictionary<string, CompositePreset[]>",
                presetStart,
                System.StringComparison.Ordinal);
            string vfxPreset = productivitySource.Substring(presetStart, presetEnd - presetStart);
            Assert.That(vfxPreset, Does.Contain("new PresetAssignment(\"_SSUStatusContract\", 1f)"));
            Assert.That(vfxPreset, Does.Contain("new PresetAssignment(\"_HologramLineFrequency\", 72f)"));
            Assert.That(vfxPreset, Does.Contain("new PresetAssignment(\"_GlitchDistortion\""));
            Assert.That(vfxPreset, Does.Not.Contain("new PresetAssignment(\"_HologramFrequency\""));
            Assert.That(vfxPreset, Does.Not.Contain("new PresetAssignment(\"_GlitchAmount\""));
            Assert.That(vfxPreset, Does.Not.Contain("new PresetAssignment(\"_GlitchSpeed\""));

            Assert.That(block.GetFloat(ES3DVFXCompositeURPProperties.HologramFrequency), Is.EqualTo(0.01f));
            Assert.That(block.GetFloat(ES3DVFXCompositeURPProperties.HologramGap), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ES3DVFXCompositeURPProperties.HologramSpeed), Is.EqualTo(128f));
            Assert.That(block.GetFloat(ES3DVFXCompositeURPProperties.HologramMinAlpha), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ES3DVFXCompositeURPProperties.GlitchAmount), Is.EqualTo(0.2f));
            Assert.That(block.GetFloat(ES3DVFXCompositeURPProperties.GlitchSpeed), Is.EqualTo(-128f));
            Assert.That(exactBlock.GetFloat(ES3DVFXCompositeURPProperties.HologramLineFrequency), Is.EqualTo(72f));
            Assert.That(exactBlock.GetFloat(ES3DVFXCompositeURPProperties.HologramSpace), Is.Zero);
            Assert.That(exactBlock.GetFloat(ES3DVFXCompositeURPProperties.HologramDistortionScale), Is.EqualTo(12f));
            Assert.That(exactBlock.GetFloat(ES3DVFXCompositeURPProperties.GlitchFade), Is.EqualTo(1f));
            Assert.That(exactBlock.GetFloat(ES3DVFXCompositeURPProperties.GlitchMaskMin), Is.Zero);
            Assert.That(exactBlock.GetVector(ES3DVFXCompositeURPProperties.GlitchDistortion),
                Is.EqualTo(new Vector4(12f, 13f, 0f, 0f)));
        }

        [TestCase("ES2DCompositeURP.shader", "half4 ESComputeCompositeColor", "float2 stylizedCoordinate = uv;", "half4 sampledSource = ESSampleMainTexture(uv);")]
        [TestCase("ESUICompositeURP.shader", "half4 ESUIFragment", "float2 ssuStylizedCoordinate = uv;", "half4 sampledColor = ESSampleUITexture(uv);")]
        public void SpriteAndUIShaders_ApplyFadeDistortionBeforeStylizedSampling(
            string fileName,
            string fragmentToken,
            string coordinateToken,
            string sampleToken)
        {
            string source = File.ReadAllText(ShaderRoot + fileName);
            int fragment = source.IndexOf(fragmentToken, System.StringComparison.Ordinal);
            int flow = source.IndexOf("if (_EnableFlow > 0.5) uv +=", fragment, System.StringComparison.Ordinal);
            int fadeDistortion = source.IndexOf("uv += perpendicular *", flow, System.StringComparison.Ordinal);
            int coordinate = source.IndexOf(coordinateToken, fadeDistortion, System.StringComparison.Ordinal);
            int hologram = source.IndexOf("ESCompositeApplySSUHologramUV", coordinate, System.StringComparison.Ordinal);
            int glitch = source.IndexOf("ESCompositeApplySSUGlitchUV", hologram, System.StringComparison.Ordinal);
            int pixelate = source.IndexOf("if (_EnablePixelate > 0.5)", glitch, System.StringComparison.Ordinal);
            int sample = source.IndexOf(sampleToken, pixelate, System.StringComparison.Ordinal);
            int chromatic = source.IndexOf("if (_EnableChromatic > 0.5", sample, System.StringComparison.Ordinal);

            Assert.That(fragment, Is.GreaterThanOrEqualTo(0));
            Assert.That(flow, Is.GreaterThan(fragment));
            Assert.That(fadeDistortion, Is.GreaterThan(flow));
            Assert.That(coordinate, Is.GreaterThan(fadeDistortion));
            Assert.That(hologram, Is.GreaterThan(coordinate));
            Assert.That(glitch, Is.GreaterThan(hologram));
            Assert.That(pixelate, Is.GreaterThan(glitch));
            Assert.That(sample, Is.GreaterThan(pixelate));
            Assert.That(chromatic, Is.GreaterThan(sample));
        }

        [TestCase("ES/2D/Composite URP", "ES2DCompositeURP.shader", "ESCompositeShineCoordinate2D", "_ShineDirection.xy")]
        [TestCase("ES/UI/Composite URP", "ESUICompositeURP.shader", "ESCompositeShineCoordinate2D", "_ShineDirection.xy")]
        [TestCase("ES/3D/Lit Composite URP", "ES3DLitCompositeURPCommon.hlsl", "ESCompositeShineCoordinate3D", "_ShineDirection.xyz")]
        [TestCase("ES/3D/VFX Composite URP", "ES3DVFXCompositeURPCommon.hlsl", "ESCompositeShineCoordinate3D", "_ShineDirection.xyz")]
        public void ExistingShineEffects_ExposeAndReadExplicitDirection(
            string shaderName,
            string executionFile,
            string normalizationToken,
            string projectionToken)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(shader, "_EnableShine", "_ShineSpace", "_ShineDirection", "_ShineSpeed", "_ShineWidth", "_ShineIntensity");

            string source = File.ReadAllText(ShaderRoot + executionFile);
            Assert.That(source, Does.Contain("_ShineSpace"));
            Assert.That(source, Does.Contain(normalizationToken));
            Assert.That(source, Does.Contain(projectionToken));
        }

        [TestCase("ES/2D/Composite URP", "ES2DCompositeURP.shader", "_DistortionStrength * _DistortionDirection.xy")]
        [TestCase("ES/3D/VFX Composite URP", "ES3DVFXCompositeURPCommon.hlsl", "_Distortion * _DistortionDirection.xy")]
        public void BasicNoiseDistortion_ExposesAndReadsAxisDirection(
            string shaderName,
            string executionFile,
            string executionToken)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(shader, "_DistortionDirection");

            string source = File.ReadAllText(ShaderRoot + executionFile);
            Assert.That(source, Does.Contain(executionToken));
        }

        [Test]
        public void BasicNoiseDistortion_RuntimeApisWriteDirectionAndRestoreCompatibilityDefault()
        {
            var spriteBlock = new MaterialPropertyBlock();
            var vfxBlock = new MaterialPropertyBlock();

            ES2DCompositeURPProperties.SetDistortion(
                spriteBlock, true, null, Vector2.one, Vector2.zero, 0.1f, new Vector2(2f, -3f));
            ES3DVFXCompositeURPProperties.SetNoiseDistortion(
                vfxBlock, null, Vector2.one, Vector2.zero, 0.1f, new Vector2(-2f, 3f));

            Assert.That(spriteBlock.GetVector(ES2DCompositeURPProperties.DistortionDirection),
                Is.EqualTo(new Vector4(2f, -3f, 0f, 0f)));
            Assert.That(vfxBlock.GetVector(ES3DVFXCompositeURPProperties.DistortionDirection),
                Is.EqualTo(new Vector4(-2f, 3f, 0f, 0f)));

            ES2DCompositeURPProperties.SetDistortion(
                spriteBlock, true, null, Vector2.one, Vector2.zero, 0.1f);
            ES3DVFXCompositeURPProperties.SetNoiseDistortion(
                vfxBlock, null, Vector2.one, Vector2.zero, 0.1f);

            Assert.That(spriteBlock.GetVector(ES2DCompositeURPProperties.DistortionDirection),
                Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
            Assert.That(vfxBlock.GetVector(ES3DVFXCompositeURPProperties.DistortionDirection),
                Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
        }

        [Test]
        public void VfxSequenceLocalEffects_DoNotDependOnAtlasFramePlacement()
        {
            string source = File.ReadAllText(ShaderRoot + "ES3DVFXCompositeURPCommon.hlsl");
            int localCoordinate = source.IndexOf(
                "float2 stylizedCoordinate = ESSequenceLocalCoordinate(uv, atlasBounds);",
                System.StringComparison.Ordinal);
            int chromaticEdge = source.IndexOf(
                "length(frac(stylizedCoordinate) - 0.5)",
                localCoordinate,
                System.StringComparison.Ordinal);
            int shine = source.IndexOf(
                "ESCompositeShineCoordinate2D(\n                stylizedCoordinate,",
                chromaticEdge,
                System.StringComparison.Ordinal);
            int sparkleCell = source.IndexOf(
                "floor(stylizedCoordinate * sparkleScale)",
                shine,
                System.StringComparison.Ordinal);
            int sparkleShape = source.IndexOf(
                "frac(stylizedCoordinate * sparkleScale)",
                sparkleCell,
                System.StringComparison.Ordinal);

            Assert.That(localCoordinate, Is.GreaterThanOrEqualTo(0));
            Assert.That(chromaticEdge, Is.GreaterThan(localCoordinate));
            Assert.That(shine, Is.GreaterThan(chromaticEdge));
            Assert.That(sparkleCell, Is.GreaterThan(shine));
            Assert.That(sparkleShape, Is.GreaterThan(sparkleCell));
        }

        [Test]
        public void VfxLegacyHologram_RespectsConfiguredProjectionSpace()
        {
            string source = File.ReadAllText(ShaderRoot + "ES3DVFXCompositeURPCommon.hlsl");
            int legacyBranch = source.IndexOf(
                "if (_SSUStatusContract <= 0.5 && _EnableHologram > 0.5)",
                System.StringComparison.Ordinal);
            int spaceSelection = source.IndexOf(
                "float legacyHologramCoordinate = _HologramSpace < 0.5",
                legacyBranch,
                System.StringComparison.Ordinal);
            int localProjection = source.IndexOf(
                "dot(stylizedCoordinate, legacyLocalDirection)",
                spaceSelection,
                System.StringComparison.Ordinal);
            int worldProjection = source.IndexOf(
                "dot(input.positionWS, legacyWorldDirection)",
                localProjection,
                System.StringComparison.Ordinal);

            Assert.That(legacyBranch, Is.GreaterThanOrEqualTo(0));
            Assert.That(spaceSelection, Is.GreaterThan(legacyBranch));
            Assert.That(localProjection, Is.GreaterThan(spaceSelection));
            Assert.That(worldProjection, Is.GreaterThan(localProjection));
        }

        [TestCase("ES/2D/Composite URP", "ES2DCompositeURP.shader")]
        [TestCase("ES/UI/Composite URP", "ESUICompositeURP.shader")]
        [TestCase("ES/3D/Lit Composite URP", "ES3DLitCompositeURPCommon.hlsl")]
        public void GeneratedDirectionalEffects_ExposeAndReadCompleteParameters(
            string shaderName,
            string executionFile)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_EnableFlame", "_FlameCenter", "_FlameDirection",
                "_EnableSmoke", "_SmokeSpeed",
                "_EnableSquish", "_SquishDirection",
                "_EnableVibrate", "_VibrateDirection");

            string source = File.ReadAllText(ShaderRoot + executionFile);
            Assert.That(source, Does.Contain("_FlameCenter"));
            Assert.That(source, Does.Contain("_FlameDirection"));
            Assert.That(source, Does.Contain("_SmokeSpeed"));
            Assert.That(source, Does.Contain("_SquishDirection"));
            Assert.That(source, Does.Contain("_VibrateDirection"));
        }

        [TestCase("ES2DCompositeURP.shader")]
        [TestCase("ESUICompositeURP.shader")]
        [TestCase("ES3DLitCompositeURP.shader")]
        public void GeneratedDirectionalEffects_DefaultsPreserveLegacyVisuals(string shaderFile)
        {
            string source = File.ReadAllText(ShaderRoot + shaderFile);
            Assert.That(source, Does.Contain("_FlameCenter (\"火焰中心\", Vector) = (0.5,0.4,0,0)"));
            Assert.That(source, Does.Contain("_FlameDirection (\"火焰方向\", Vector) = (0,1,0,0)"));
            Assert.That(source, Does.Contain("_SmokeSpeed (\"烟雾流动速度\", Vector) = (0,0,0,0)"));
            Assert.That(source, Does.Contain("_SquishDirection (\"挤压方向\", Vector) = (1,0,0,0)"));
            Assert.That(source, Does.Contain("_VibrateDirection (\"震动主方向\", Vector) = (1,0,0,0)"));
            Assert.That(source, Does.Contain("_ShineSpace (\"扫光空间\", Float) = 0"));
        }

        [TestCase("ES2DCompositeURP.shader")]
        [TestCase("ESUICompositeURP.shader")]
        [TestCase("ES3DLitCompositeURP.shader")]
        [TestCase("ES3DVFXCompositeURP.shader")]
        public void ShineSpace_DefaultsToCompatibleForEveryFamily(string shaderFile)
        {
            string source = File.ReadAllText(ShaderRoot + shaderFile);
            Assert.That(source, Does.Contain("_ShineSpace (\"扫光空间\", Float) = 0"));
        }

        [TestCase("ES/2D/Composite URP", "ES2DCompositeURP.shader")]
        [TestCase("ES/UI/Composite URP", "ESUICompositeURP.shader")]
        [TestCase("ES/3D/Lit Composite URP", "ES3DLitCompositeURPCommon.hlsl")]
        [TestCase("ES/3D/VFX Composite URP", "ES3DVFXCompositeURPCommon.hlsl")]
        public void HologramAndLegacyGlitch_ExposeAndReadExplicitDirections(
            string shaderName,
            string executionFile)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_EnableHologram", "_HologramDirection", "_HologramDistortionDirection",
                "_EnableGlitch", "_GlitchScanDirection", "_GlitchDistortion");

            string sharedSource = File.ReadAllText(ShaderRoot + "ESCompositeSSUStylizedEffects.hlsl");
            Assert.That(sharedSource, Does.Contain("_HologramDirection.xy"));
            Assert.That(sharedSource, Does.Contain("_HologramDirection.xyz"));
            Assert.That(sharedSource, Does.Contain("_HologramDistortionDirection.xy"));

            string source = File.ReadAllText(ShaderRoot + executionFile);
            Assert.That(source, Does.Contain("_GlitchDistortion.xy"));
            Assert.That(source, Does.Contain("_GlitchScanDirection"));
            Assert.That(source, Does.Contain("hologramCoordinate"));
        }

        [Test]
        public void SSUStylizedEffects_ClampRuntimeInputsBeforeNonlinearMath()
        {
            string source = File.ReadAllText(ShaderRoot + "ESCompositeSSUStylizedEffects.hlsl");

            Assert.That(source, Does.Contain("float safeTextureWidth = max(abs(textureWidth), 1.0);"));
            Assert.That(source, Does.Contain("float lineGap = clamp(_HologramLineGap, 0.001, 8.0);"));
            Assert.That(source, Does.Contain("float minimumAlpha = saturate(_HologramMinAlpha);"));
            Assert.That(source, Does.Contain("return saturate(max(maskNoise, saturate(_GlitchMaskMin)) * saturate(_GlitchFade));"));
            Assert.That(source, Does.Contain("clamp(_GlitchBrightness, 0.0, 16.0)"));
        }

        [TestCase("ES/2D/Composite URP", "ES2DCompositeURP.shader", "ESCompositeDirectionalCoordinate2D")]
        [TestCase("ES/UI/Composite URP", "ESUICompositeURP.shader", "ESCompositeDirectionalCoordinate2D")]
        [TestCase("ES/3D/Lit Composite URP", "ES3DLitCompositeURPCommon.hlsl", "ESCompositeDirectionalCoordinate2D")]
        [TestCase("ES/3D/VFX Composite URP", "ES3DVFXCompositeURPCommon.hlsl", "ESCompositeDirectionalCoordinate3D")]
        public void LegacyGlitch_UsesExplicitScanDirection(
            string shaderName,
            string executionFile,
            string projectionToken)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(shader, "_EnableGlitch", "_GlitchScanDirection");

            string source = File.ReadAllText(ShaderRoot + executionFile);
            Assert.That(source, Does.Contain("_GlitchScanDirection"));
            Assert.That(source, Does.Contain(projectionToken));
        }

        [TestCase("ES/2D/Composite URP", "ES2DCompositeURP.shader")]
        [TestCase("ES/UI/Composite URP", "ESUICompositeURP.shader")]
        [TestCase("ES/3D/Lit Composite URP", "ES3DLitCompositeSSUSurface.hlsl")]
        public void LegacyLinearRainbow_UsesExplicitBandDirection(
            string shaderName,
            string executionFile)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(shader, "_EnableRainbow", "_RainbowDirection");

            string source = File.ReadAllText(ShaderRoot + executionFile);
            Assert.That(source, Does.Contain("_RainbowDirection.xy"));
            Assert.That(source, Does.Contain("ESCompositeDirectionalCoordinate2D"));
        }

        [Test]
        public void TwoDShader_RestoredColorControls_AreDeclaredInUnityPerMaterial()
        {
            string source = File.ReadAllText(ShaderRoot + "ES2DCompositeURP.shader");
            int cbuffer = source.IndexOf("CBUFFER_START(UnityPerMaterial)", System.StringComparison.Ordinal);
            int cbufferEnd = source.IndexOf("CBUFFER_END", cbuffer, System.StringComparison.Ordinal);
            string materialConstants = source.Substring(cbuffer, cbufferEnd - cbuffer);

            Assert.That(materialConstants, Does.Contain("float _AlphaTintFade;"));
            Assert.That(materialConstants, Does.Contain("float _ReplaceContrast;"));
            Assert.That(materialConstants, Does.Contain("float _ReplaceFade;"));
            Assert.That(materialConstants, Does.Contain("float _SplitToneContrast;"));
            Assert.That(materialConstants, Does.Contain("float _SplitToneShift;"));
        }

        [TestCase("ES2DCompositeURP.shader", "half4 ESComputeCompositeColor", "alpha *= fadeVisibility;", "if (_SSUStatusContract <= 0.5 && _EnableShine > 0.5)")]
        [TestCase("ESUICompositeURP.shader", "half4 ESUIFragment", "color.a *= fadeVisibility;", "if (_SSUStatusContract <= 0.5 && _EnableShine > 0.5)")]
        public void SpriteAndUIShine_RunsAfterCoverageAndBeforeSparkle(
            string fileName,
            string fragmentToken,
            string coverageToken,
            string shineToken)
        {
            string source = File.ReadAllText(ShaderRoot + fileName);
            int fragment = source.IndexOf(fragmentToken, System.StringComparison.Ordinal);
            int coverage = source.IndexOf(coverageToken, fragment, System.StringComparison.Ordinal);
            int shine = source.IndexOf(shineToken, coverage, System.StringComparison.Ordinal);
            int sparkle = source.IndexOf("if (_EnableSparkle > 0.5)", shine, System.StringComparison.Ordinal);

            Assert.That(coverage, Is.GreaterThan(fragment));
            Assert.That(shine, Is.GreaterThan(coverage));
            Assert.That(sparkle, Is.GreaterThan(shine));
        }

        [TestCase("ES2DCompositeURP.shader", "half4 ESComputeCompositeColor")]
        [TestCase("ESUICompositeURP.shader", "half4 ESUIFragment")]
        [TestCase("ES3DLitCompositeURPCommon.hlsl", "void ESInitializeSurface")]
        public void GeneratedEffects_PreserveSmokeCheckerboardFlameOrder(
            string fileName,
            string pipelineToken)
        {
            string source = File.ReadAllText(ShaderRoot + fileName);
            int pipeline = source.IndexOf(pipelineToken, System.StringComparison.Ordinal);
            int smoke = source.IndexOf("if (_EnableSmoke > 0.5)", pipeline, System.StringComparison.Ordinal);
            int checkerboard = source.IndexOf("if (_EnableCheckerboard > 0.5)", smoke, System.StringComparison.Ordinal);
            int flame = source.IndexOf("if (_EnableFlame > 0.5)", checkerboard, System.StringComparison.Ordinal);

            Assert.That(pipeline, Is.GreaterThanOrEqualTo(0));
            Assert.That(smoke, Is.GreaterThan(pipeline));
            Assert.That(checkerboard, Is.GreaterThan(smoke));
            Assert.That(flame, Is.GreaterThan(checkerboard));
        }

        [Test]
        public void LitShine_RunsAfterLightingAndRimBeforeSparkle()
        {
            string source = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            int fragment = source.IndexOf("half4 ESForwardFragment", System.StringComparison.Ordinal);
            int lighting = source.IndexOf("UniversalFragmentPBR", fragment, System.StringComparison.Ordinal);
            int rim = source.IndexOf("if (_EnableRim > 0.5)", lighting, System.StringComparison.Ordinal);
            int shine = source.IndexOf("if (_SSUStatusContract <= 0.5 && _EnableShine > 0.5)", rim, System.StringComparison.Ordinal);
            int sparkle = source.IndexOf("if (_EnableSparkle > 0.5)", shine, System.StringComparison.Ordinal);

            Assert.That(lighting, Is.GreaterThan(fragment));
            Assert.That(rim, Is.GreaterThan(lighting));
            Assert.That(shine, Is.GreaterThan(rim));
            Assert.That(sparkle, Is.GreaterThan(shine));
        }

        [Test]
        public void LitShine_UsesUnifiedSpaceResolverInForwardGBufferAndExactPaths()
        {
            string common = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            string shader = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURP.shader");
            string exact = File.ReadAllText(ShaderRoot + "ES3DLitCompositeSSUSurface.hlsl");

            Assert.That(common, Does.Contain("float ESResolveLitShineCoordinate("));
            Assert.That(common, Does.Contain("float shineCoordinate = ESResolveLitShineCoordinate("));
            Assert.That(shader, Does.Contain("float shineCoordinate = ESResolveLitShineCoordinate("));
            Assert.That(exact, Does.Contain("float exactShineCoordinate = ESResolveLitShineCoordinate("));
        }

        [Test]
        public void ShineRuntimeApis_WriteDirectionForEveryShaderFamily()
        {
            var spriteBlock = new MaterialPropertyBlock();
            var uiBlock = new MaterialPropertyBlock();
            var litBlock = new MaterialPropertyBlock();
            var vfxBlock = new MaterialPropertyBlock();

            ES2DCompositeURPProperties.SetShine(
                spriteBlock, true, Color.white, 2f, 0.2f, 3f, new Vector2(3f, 4f), 75f);
            ESUICompositeURPProperties.SetShine(
                uiBlock, true, Color.white, 2f, 0.2f, 3f, new Vector2(-2f, 5f), 95f);
            ES3DLitCompositeURPProperties.SetShine(
                litBlock, true, Color.white, 2f, 0.2f, 3f, new Vector3(1f, 2f, 3f));
            ES3DVFXCompositeURPProperties.SetShine(
                vfxBlock, true, Color.white, 2f, 0.2f, 3f, new Vector3(-1f, 4f, 2f));

            Assert.That(spriteBlock.GetVector(ES2DCompositeURPProperties.ShineDirection), Is.EqualTo(new Vector4(3f, 4f, 0f, 0f)));
            Assert.That(spriteBlock.GetFloat(ES2DCompositeURPProperties.ShineAngle), Is.EqualTo(Mathf.Atan2(4f, 3f) * Mathf.Rad2Deg).Within(0.001f));
            Assert.That(spriteBlock.GetFloat(ESCompositeURPProperties.ShineRotation), Is.EqualTo(Mathf.Atan2(4f, 3f) * Mathf.Rad2Deg).Within(0.001f));
            Assert.That(uiBlock.GetVector(ESUICompositeURPProperties.ShineDirection), Is.EqualTo(new Vector4(-2f, 5f, 0f, 0f)));
            Assert.That(uiBlock.GetFloat(ESUICompositeURPProperties.ShineAngle), Is.EqualTo(Mathf.Repeat(Mathf.Atan2(5f, -2f) * Mathf.Rad2Deg, 360f)).Within(0.001f));
            Assert.That(litBlock.GetVector(ES3DLitCompositeURPProperties.ShineDirection), Is.EqualTo(new Vector4(1f, 2f, 3f, 0f)));
            Assert.That(vfxBlock.GetVector(ES3DVFXCompositeURPProperties.ShineDirection), Is.EqualTo(new Vector4(-1f, 4f, 2f, 0f)));
        }

        [Test]
        public void DirectionalEffectRuntimeApis_WriteSpaceDirectionCenterAndSpeed()
        {
            var spriteBlock = new MaterialPropertyBlock();
            var uiBlock = new MaterialPropertyBlock();
            var litBlock = new MaterialPropertyBlock();
            var vfxBlock = new MaterialPropertyBlock();

            ES2DCompositeURPProperties.SetShine(
                spriteBlock, true, Color.white, 2f, 0.2f, 3f,
                Vector2.up, ESCompositeProjectionSpace.世界投影);
            ESUICompositeURPProperties.SetShine(
                uiBlock, true, Color.white, 2f, 0.2f, 3f,
                Vector2.right, ESCompositeProjectionSpace.局部UV);
            ES3DLitCompositeURPProperties.SetShine(
                litBlock, true, Color.white, 2f, 0.2f, 3f,
                Vector3.forward, ESCompositeProjectionSpace.局部UV);
            ES3DVFXCompositeURPProperties.SetShine(
                vfxBlock, true, Color.white, 2f, 0.2f, 3f,
                Vector3.up, ESCompositeProjectionSpace.世界投影);

            ES2DCompositeURPProperties.SetFlame(
                spriteBlock, true, null, 1f, 2f, 0.4f, Vector2.up, 1f, 1f,
                Vector2.one, new Vector2(3f, 4f), new Vector2(0.2f, 0.7f));
            ESUICompositeURPProperties.SetSmoke(
                uiBlock, true, null, 0.8f, 1f, 2f, 0.5f, 0.2f, false,
                new Vector2(-0.5f, 1.25f));
            ES3DLitCompositeURPProperties.SetSquish(
                litBlock, true, 0.2f, 3f, new Vector2(0f, 2f));
            ESUICompositeURPProperties.SetVibrate(
                uiBlock, true, 0.1f, 4f, new Vector2(-3f, 4f));

            Assert.That(spriteBlock.GetFloat(ES2DCompositeURPProperties.ShineSpace), Is.EqualTo(2f));
            Assert.That(uiBlock.GetFloat(ESUICompositeURPProperties.ShineSpace), Is.EqualTo(1f));
            Assert.That(litBlock.GetFloat(ES3DLitCompositeURPProperties.ShineSpace), Is.EqualTo(1f));
            Assert.That(vfxBlock.GetFloat(ES3DVFXCompositeURPProperties.ShineSpace), Is.EqualTo(2f));
            Assert.That(spriteBlock.GetVector(ES2DCompositeURPProperties.FlameDirection),
                Is.EqualTo(new Vector4(0.6f, 0.8f, 0f, 0f)));
            Assert.That(spriteBlock.GetVector(ES2DCompositeURPProperties.FlameCenter),
                Is.EqualTo(new Vector4(0.2f, 0.7f, 0f, 0f)));
            Assert.That(uiBlock.GetVector(ESUICompositeURPProperties.SmokeSpeed),
                Is.EqualTo(new Vector4(-0.5f, 1.25f, 0f, 0f)));
            Assert.That(litBlock.GetVector(ES3DLitCompositeURPProperties.SquishDirection),
                Is.EqualTo(new Vector4(0f, 1f, 0f, 0f)));
            Assert.That(uiBlock.GetVector(ESUICompositeURPProperties.VibrateDirection),
                Is.EqualTo(new Vector4(-0.6f, 0.8f, 0f, 0f)));

            ES2DCompositeURPProperties.SetShine(
                spriteBlock, true, Color.white, 1f, 0.1f, 1f, Vector2.right);
            ES2DCompositeURPProperties.SetFlame(
                spriteBlock, true, null, 1f, 1f, 0.4f, Vector2.up, 1f, 1f, Vector2.one);
            ESUICompositeURPProperties.SetSmoke(
                uiBlock, true, null, 1f, 1f, 1f, 1f, 0f, false);
            ES3DLitCompositeURPProperties.SetSquish(litBlock, true, 0.2f, 1f);
            ESUICompositeURPProperties.SetVibrate(uiBlock, true, 0.1f, 1f);

            Assert.That(spriteBlock.GetFloat(ES2DCompositeURPProperties.ShineSpace), Is.Zero);
            Assert.That(spriteBlock.GetVector(ES2DCompositeURPProperties.FlameDirection),
                Is.EqualTo(new Vector4(0f, 1f, 0f, 0f)));
            Assert.That(spriteBlock.GetVector(ES2DCompositeURPProperties.FlameCenter),
                Is.EqualTo(new Vector4(0.5f, 0.4f, 0f, 0f)));
            Assert.That(uiBlock.GetVector(ESUICompositeURPProperties.SmokeSpeed), Is.EqualTo(Vector4.zero));
            Assert.That(litBlock.GetVector(ES3DLitCompositeURPProperties.SquishDirection),
                Is.EqualTo(new Vector4(1f, 0f, 0f, 0f)));
            Assert.That(uiBlock.GetVector(ESUICompositeURPProperties.VibrateDirection),
                Is.EqualTo(new Vector4(1f, 0f, 0f, 0f)));
        }

        [Test]
        public void HologramRuntimeApis_WriteDirectionsForEveryShaderFamily()
        {
            var spriteBlock = new MaterialPropertyBlock();
            var uiBlock = new MaterialPropertyBlock();
            var litBlock = new MaterialPropertyBlock();
            var vfxBlock = new MaterialPropertyBlock();

            ES2DCompositeURPProperties.SetHologram(
                spriteBlock, true, Color.cyan, 80f, 0.3f, 2f, 0.2f,
                1f, 2f, ES3DLitHologramSpace.局部UV, 0.1f, 2f, 0.5f, 10f,
                new Vector3(2f, 3f, 0f), new Vector2(-1f, 4f));
            ESUICompositeURPProperties.SetHologram(
                uiBlock, true, Color.cyan, 70f, 0.4f, 3f, 0.3f,
                1f, 2f, ES3DLitHologramSpace.局部UV, 0.2f, 3f, 0.6f, 11f,
                new Vector3(-2f, 5f, 0f), new Vector2(3f, 1f));
            ES3DLitCompositeURPProperties.SetHologram(
                litBlock, true, Color.cyan, 60f, 0.5f, 4f, 0.4f,
                1f, 2f, ES3DLitHologramSpace.世界投影, 0.3f, 4f, 0.7f, 12f,
                new Vector3(1f, 2f, 3f), new Vector2(2f, -1f));
            ES3DVFXCompositeURPProperties.SetSSUExactHologram(
                vfxBlock, true, Color.cyan, 50f, 0.6f, 5f, 0.5f,
                1f, 2f, ES3DLitHologramSpace.世界投影, 0.4f, 5f, 0.8f, 13f,
                new Vector3(-1f, 4f, 2f), new Vector2(-3f, 2f));

            Assert.That(spriteBlock.GetVector(ES2DCompositeURPProperties.HologramDirection), Is.EqualTo(new Vector4(2f, 3f, 0f, 0f)));
            Assert.That(spriteBlock.GetVector(ES2DCompositeURPProperties.HologramDistortionDirection), Is.EqualTo(new Vector4(-1f, 4f, 0f, 0f)));
            Assert.That(uiBlock.GetVector(ESUICompositeURPProperties.HologramDirection), Is.EqualTo(new Vector4(-2f, 5f, 0f, 0f)));
            Assert.That(litBlock.GetVector(ES3DLitCompositeURPProperties.HologramDirection), Is.EqualTo(new Vector4(1f, 2f, 3f, 0f)));
            Assert.That(vfxBlock.GetVector(ES3DVFXCompositeURPProperties.HologramDirection), Is.EqualTo(new Vector4(-1f, 4f, 2f, 0f)));
            Assert.That(vfxBlock.GetVector(ES3DVFXCompositeURPProperties.HologramDistortionDirection), Is.EqualTo(new Vector4(-3f, 2f, 0f, 0f)));
        }

        [Test]
        public void ComposableFadeRuntimeApis_WriteEveryShaderParameterGroup()
        {
            var block = new MaterialPropertyBlock();

            ESCompositeURPProperties.SetFullAlphaDissolve(block, true, 0.2f, 0.3f, new Vector2(1f, 2f));
            ESCompositeURPProperties.SetSourceAlphaDissolve(
                block, true, 0.4f, new Vector2(0.2f, 0.7f), 0.5f,
                new Vector2(3f, 4f), 0.6f, true);
            ESCompositeURPProperties.SetSourceGlowDissolve(
                block, true, 0.7f, new Vector2(0.3f, 0.8f), 0.2f, Color.cyan,
                new Vector2(5f, 6f), 0.4f, false);
            ESCompositeURPProperties.SetDirectionalAlphaFade(
                block, true, 0.1f, 45f, 0.25f, new Vector2(7f, 8f), 0.3f, true);
            ESCompositeURPProperties.SetDirectionalGlowFade(
                block, true, 0.2f, 90f, 0.35f, Color.magenta,
                new Vector2(9f, 10f), 0.5f, false);
            ESCompositeURPProperties.SetDirectionalDistortion(
                block, true, 0.3f, 135f, 0.45f, new Vector2(11f, 12f), 0.7f,
                new Vector2(0.1f, -0.2f), 0.8f, new Vector2(13f, 14f), true);

            Assert.That(block.GetFloat(ESCompositeURPProperties.FullAlphaDissolveEnabled), Is.EqualTo(1f));
            Assert.That(block.GetVector(ESCompositeURPProperties.SourceAlphaDissolvePosition), Is.EqualTo(new Vector4(0.2f, 0.7f, 0f, 0f)));
            Assert.That(block.GetColor(ESCompositeURPProperties.SourceGlowDissolveEdgeColor), Is.EqualTo(Color.cyan));
            Assert.That(block.GetFloat(ESCompositeURPProperties.DirectionalAlphaFadeRotation), Is.EqualTo(45f));
            Assert.That(block.GetColor(ESCompositeURPProperties.DirectionalGlowFadeEdgeColor), Is.EqualTo(Color.magenta));
            Assert.That(block.GetVector(ESCompositeURPProperties.DirectionalDistortionAmount), Is.EqualTo(new Vector4(0.1f, -0.2f, 0f, 0f)));
            Assert.That(block.GetFloat(ESCompositeURPProperties.DirectionalDistortionInvert), Is.EqualTo(1f));
        }

        [Test]
        public void RestoredColorControlApis_WriteCompleteParameterGroups()
        {
            var block = new MaterialPropertyBlock();

            ESCompositeURPProperties.SetAlphaTint(
                block, true, Color.cyan, 0.25f, 0.75f);
            ESCompositeURPProperties.SetColorReplace(
                block, true, Color.red, Color.blue, 0.2f, 0.3f, 2f, 0.6f);
            ESCompositeURPProperties.SetSplitToning(
                block, true, Color.black, Color.white, 0.4f, 0.8f, 3f, -0.2f);
            ESCompositeURPProperties.SetPingPongGlow(
                block, true, Color.green, Color.magenta, 4f, 5f, 2.5f, 0.7f);

            Assert.That(block.GetFloat(ESCompositeURPProperties.AlphaTintFade), Is.EqualTo(0.75f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.ReplaceContrast), Is.EqualTo(2f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.ReplaceFade), Is.EqualTo(0.6f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SplitToneContrast), Is.EqualTo(3f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SplitToneShift), Is.EqualTo(-0.2f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.GlowContrast), Is.EqualTo(2.5f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.GlowFade), Is.EqualTo(0.7f));
        }

        [TestCase("ES2DCompositeURP.shader")]
        [TestCase("ESUICompositeURP.shader")]
        [TestCase("ES3DLitCompositeURP.shader")]
        [TestCase("ES3DVFXCompositeURP.shader")]
        public void EveryAuthoringProperty_HasRuntimePropertyIdOrExplicitHostOwnership(string fileName)
        {
            string shaderSource = File.ReadAllText(ShaderRoot + fileName);
            string parameterSource = File.ReadAllText(
                "Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESCompositeShaderParameters.cs");
            var hostOwned = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
            {
                "_ESMaterialVersion"
            };

            if (fileName == "ES2DCompositeURP.shader")
            {
                hostOwned.UnionWith(new[]
                {
                    "_RendererColor", "_Flip", "_AlphaTex", "_EnableExternalAlpha"
                });
            }
            else if (fileName == "ESUICompositeURP.shader")
            {
                hostOwned.UnionWith(new[]
                {
                    "_ScaleX", "_ScaleY", "_PerspectiveFilter", "_VertexOffsetX", "_VertexOffsetY",
                    "_MaskSoftnessX", "_MaskSoftnessY", "_UIMaskSoftnessX", "_UIMaskSoftnessY",
                    "_ShaderFlags", "_ClipRect", "_CullMode", "_StencilComp", "_Stencil",
                    "_StencilOp", "_StencilWriteMask", "_StencilReadMask", "_ColorMask", "_TextureSampleAdd"
                });
            }

            System.Text.RegularExpressions.Match properties =
                System.Text.RegularExpressions.Regex.Match(
                    shaderSource,
                    @"Properties\s*\{(?<body>[\s\S]*?)\n\s*\}\s*\n\s*SubShader");
            Assert.That(properties.Success, Is.True, fileName + " has no parseable Properties block.");

            System.Text.RegularExpressions.MatchCollection names =
                System.Text.RegularExpressions.Regex.Matches(
                    properties.Groups["body"].Value,
                    @"(?m)^\s*(?:\[[^\r\n]*\]\s*)*(?<name>_[A-Za-z0-9_]+)\s*\(");
            for (int i = 0; i < names.Count; i++)
            {
                string propertyName = names[i].Groups["name"].Value;
                if (hostOwned.Contains(propertyName)) continue;
                Assert.That(
                    parameterSource,
                    Does.Contain("Shader.PropertyToID(\"" + propertyName + "\")"),
                    fileName + " authoring property lacks a runtime property ID: " + propertyName);
            }
        }

        [TestCase("ES2DCompositeURP.shader", null)]
        [TestCase("ESUICompositeURP.shader", null)]
        [TestCase("ES3DLitCompositeURP.shader", "ES3DLitComposite*.hlsl")]
        [TestCase("ES3DVFXCompositeURP.shader", "ES3DVFXComposite*.hlsl")]
        public void EveryAuthoringProperty_IsConsumedByShaderOrExplicitHostState(
            string shaderFileName,
            string familyExecutionPattern)
        {
            string shaderSource = File.ReadAllText(ShaderRoot + shaderFileName);
            System.Text.RegularExpressions.Match properties =
                System.Text.RegularExpressions.Regex.Match(
                    shaderSource,
                    @"Properties\s*\{(?<body>[\s\S]*?)\n\s*\}\s*\n\s*SubShader");
            Assert.That(properties.Success, Is.True, shaderFileName + " has no parseable Properties block.");

            var hostOrEditorState = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
            {
                "_ESMaterialVersion", "_QualityTier", "_ResourceProfile", "_QueueOffset"
            };
            if (shaderFileName == "ES2DCompositeURP.shader")
                hostOrEditorState.Add("_Flip");
            else if (shaderFileName == "ESUICompositeURP.shader")
            {
                hostOrEditorState.Add("_ShaderFlags");
                hostOrEditorState.Add("_UseUIAlphaClip");
            }
            else if (shaderFileName == "ES3DLitCompositeURP.shader")
            {
                hostOrEditorState.Add("_Surface");
                hostOrEditorState.Add("_ReceiveShadows");
            }

            string executionSource = shaderSource.Substring(properties.Index + properties.Length);
            string[] sharedExecutionFiles = Directory.GetFiles(ShaderRoot, "ESComposite*.hlsl");
            for (int i = 0; i < sharedExecutionFiles.Length; i++)
                executionSource += File.ReadAllText(sharedExecutionFiles[i]);
            if (!string.IsNullOrEmpty(familyExecutionPattern))
            {
                string[] familyExecutionFiles = Directory.GetFiles(ShaderRoot, familyExecutionPattern);
                for (int i = 0; i < familyExecutionFiles.Length; i++)
                    executionSource += File.ReadAllText(familyExecutionFiles[i]);
            }

            System.Text.RegularExpressions.MatchCollection names =
                System.Text.RegularExpressions.Regex.Matches(
                    properties.Groups["body"].Value,
                    @"(?m)^\s*(?:\[[^\r\n]*\]\s*)*(?<name>_[A-Za-z0-9_]+)\s*\(");
            for (int i = 0; i < names.Count; i++)
            {
                string propertyName = names[i].Groups["name"].Value;
                if (hostOrEditorState.Contains(propertyName)) continue;
                Assert.That(
                    System.Text.RegularExpressions.Regex.IsMatch(
                        executionSource,
                        @"(?<![A-Za-z0-9_])" + System.Text.RegularExpressions.Regex.Escape(propertyName)
                            + @"(?![A-Za-z0-9_])"),
                    Is.True,
                    shaderFileName + " authoring property is never consumed: " + propertyName);
            }
        }

        [TestCase("ES2DCompositeURP.shader", null)]
        [TestCase("ESUICompositeURP.shader", null)]
        [TestCase("ES3DLitCompositeURP.shader", "ES3DLitComposite*.hlsl")]
        [TestCase("ES3DVFXCompositeURP.shader", "ES3DVFXComposite*.hlsl")]
        public void EveryEffectToggle_HasNonDeclarationShaderReference(
            string shaderFileName,
            string familyExecutionPattern)
        {
            string shaderSource = File.ReadAllText(ShaderRoot + shaderFileName);
            System.Text.RegularExpressions.Match properties =
                System.Text.RegularExpressions.Regex.Match(
                    shaderSource,
                    @"Properties\s*\{(?<body>[\s\S]*?)\n\s*\}\s*\n\s*SubShader");
            Assert.That(properties.Success, Is.True, shaderFileName + " has no parseable Properties block.");

            string executionSource = shaderSource.Substring(properties.Index + properties.Length);
            string[] sharedExecutionFiles = Directory.GetFiles(ShaderRoot, "ESComposite*.hlsl");
            for (int i = 0; i < sharedExecutionFiles.Length; i++)
                executionSource += File.ReadAllText(sharedExecutionFiles[i]);
            if (!string.IsNullOrEmpty(familyExecutionPattern))
            {
                string[] familyExecutionFiles = Directory.GetFiles(ShaderRoot, familyExecutionPattern);
                for (int i = 0; i < familyExecutionFiles.Length; i++)
                    executionSource += File.ReadAllText(familyExecutionFiles[i]);
            }

            executionSource = System.Text.RegularExpressions.Regex.Replace(
                executionSource,
                @"(?m)^\s*(?:half|half[234]|float|float[234]|int|uint)\s+_Enable[A-Za-z0-9_]+\s*;\s*$",
                string.Empty);
            System.Text.RegularExpressions.MatchCollection toggles =
                System.Text.RegularExpressions.Regex.Matches(
                    properties.Groups["body"].Value,
                    @"(?m)^\s*(?:\[[^\r\n]*\]\s*)*(?<name>_Enable[A-Za-z0-9_]+)\s*\(");

            for (int i = 0; i < toggles.Count; i++)
            {
                string toggleName = toggles[i].Groups["name"].Value;
                Assert.That(
                    System.Text.RegularExpressions.Regex.IsMatch(
                        executionSource,
                        @"(?<![A-Za-z0-9_])" + System.Text.RegularExpressions.Regex.Escape(toggleName)
                        + @"(?![A-Za-z0-9_])"),
                    Is.True,
                    shaderFileName + " effect toggle has no non-declaration shader reference: "
                    + toggleName);
            }
        }

        [Test]
        public void FamilyRuntimeApis_WritePreviouslyUnreachableCoreParameters()
        {
            var spriteBlock = new MaterialPropertyBlock();
            var litBlock = new MaterialPropertyBlock();
            var vfxBlock = new MaterialPropertyBlock();

            ES2DCompositeURPProperties.SetNormalMap(spriteBlock, Texture2D.normalTexture, 2f);
            ES2DCompositeURPProperties.SetDistortion(
                spriteBlock, true, Texture2D.grayTexture,
                new Vector2(2f, 3f), new Vector2(4f, 5f), 0.25f);
            ES2DCompositeURPProperties.SetAlphaClip(spriteBlock, true, 0.4f);

            ES3DLitCompositeURPProperties.SetNormalMap(litBlock, true, Texture2D.normalTexture, 1.5f);
            ES3DLitCompositeURPProperties.SetOcclusionMap(litBlock, true, Texture2D.whiteTexture, 0.75f);
            ES3DLitCompositeURPProperties.SetEmission(litBlock, true, Color.cyan, Texture2D.whiteTexture, true);
            ES3DLitCompositeURPProperties.SetRim(litBlock, true, Color.magenta, 4f, 2f);
            ES3DLitCompositeURPProperties.SetDissolve(
                litBlock, ES3DCompositeDissolveMode.噪声溶解, 0.3f, 0.15f,
                Texture2D.grayTexture, new Vector4(6f, 7f, 8f, 9f), new Vector4(10f, 11f, 12f, 13f));

            ES3DVFXCompositeURPProperties.SetNoiseDistortion(
                vfxBlock, Texture2D.grayTexture,
                new Vector2(10f, 11f), new Vector2(12f, 13f), 0.2f);
            ES3DVFXCompositeURPProperties.SetDissolve(
                vfxBlock, ES3DVFXDissolveMode.溶解加边缘光, 0.6f, 0.1f, Color.yellow,
                Texture2D.grayTexture, new Vector2(14f, 15f), new Vector2(16f, 17f));
            ES3DVFXCompositeURPProperties.SetRim(vfxBlock, true, Color.red, 3f, 4f);
            ES3DVFXCompositeURPProperties.SetEmission(vfxBlock, Color.green);
            ES3DVFXCompositeURPProperties.SetAlphaClip(vfxBlock, true, 0.35f);

            Assert.That(spriteBlock.GetTexture(ES2DCompositeURPProperties.NormalMap), Is.EqualTo(Texture2D.normalTexture));
            Assert.That(spriteBlock.GetFloat(ES2DCompositeURPProperties.DistortionStrength), Is.EqualTo(0.25f));
            Assert.That(spriteBlock.GetFloat(ES2DCompositeURPProperties.Cutoff), Is.EqualTo(0.4f));
            Assert.That(litBlock.GetTexture(ES3DLitCompositeURPProperties.OcclusionMap), Is.EqualTo(Texture2D.whiteTexture));
            Assert.That(litBlock.GetColor(ES3DLitCompositeURPProperties.EmissionColor), Is.EqualTo(Color.cyan));
            Assert.That(litBlock.GetFloat(ES3DLitCompositeURPProperties.DissolveSoftness), Is.EqualTo(0.15f));
            Assert.That(litBlock.GetVector(ES3DLitCompositeURPProperties.NoiseScale), Is.EqualTo(new Vector4(6f, 7f, 8f, 9f)));
            Assert.That(vfxBlock.GetFloat(ES3DVFXCompositeURPProperties.Distortion), Is.EqualTo(0.2f));
            Assert.That(vfxBlock.GetColor(ES3DVFXCompositeURPProperties.DissolveColor), Is.EqualTo(Color.yellow));
            Assert.That(vfxBlock.GetColor(ES3DVFXCompositeURPProperties.EmissionColor), Is.EqualTo(Color.green));
            Assert.That(vfxBlock.GetFloat(ES3DVFXCompositeURPProperties.Cutoff), Is.EqualTo(0.35f));
        }

        [Test]
        public void LitShader_ResolvesFlowAndFadeBeforeStylizedAndPixelUv()
        {
            string source = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            int resolver = source.IndexOf("float2 ESResolveLitSurfaceUV(", System.StringComparison.Ordinal);
            int baseUv = source.IndexOf("uv = ESResolveLitUV", resolver, System.StringComparison.Ordinal);
            int flowAndFade = source.IndexOf("uv = ESApplyFlowMap(uv);", baseUv, System.StringComparison.Ordinal);
            int stylized = source.IndexOf("return ESApplyLitStylizedAndPixelUV(", flowAndFade, System.StringComparison.Ordinal);
            int stylizedFunction = source.IndexOf("float2 ESApplyLitStylizedAndPixelUV(", System.StringComparison.Ordinal);
            int hologram = source.IndexOf("ESCompositeApplySSUHologramUV", stylizedFunction, System.StringComparison.Ordinal);
            int glitch = source.IndexOf("ESCompositeApplySSUGlitchUV", hologram, System.StringComparison.Ordinal);
            int smoothPixel = source.IndexOf("ESCompositeSmoothPixelUV", glitch, System.StringComparison.Ordinal);
            int pixelate = source.IndexOf("if (_EnablePixelate > 0.5)", smoothPixel, System.StringComparison.Ordinal);

            Assert.That(baseUv, Is.GreaterThan(resolver));
            Assert.That(flowAndFade, Is.GreaterThan(baseUv));
            Assert.That(stylized, Is.GreaterThan(flowAndFade));
            Assert.That(hologram, Is.GreaterThan(stylizedFunction));
            Assert.That(glitch, Is.GreaterThan(hologram));
            Assert.That(smoothPixel, Is.GreaterThan(glitch));
            Assert.That(pixelate, Is.GreaterThan(smoothPixel));
        }

        [Test]
        public void SSUExactStylizedParameters_PreserveFloatValuesAcrossSpriteApis()
        {
            var block = new MaterialPropertyBlock();
            ES2DCompositeURPProperties.SetSSUExactContract(block, true);
            ES2DCompositeURPProperties.SetHologram(
                block, true, Color.cyan, 4096f, 3f, 256f, 0.25f,
                0.75f, 12f, ES3DLitHologramSpace.世界高度,
                2f, 256f, -0.5f, -4096f);
            ESUICompositeURPProperties.SetGlitch(
                block, true, 2f, -256f, 0.5f, 0.25f,
                new Vector2(4096f, -4096f), new Vector2(256f, -256f),
                256f, 16f, new Vector2(3f, 4f), new Vector2(5f, 6f),
                new Vector2(2f, -2f), new Vector2(7f, 8f), new Vector2(9f, 10f));
            ESUICompositeURPProperties.SetPixelOutline(
                block, true, Color.white, 9f, 0.5f, false, null, new Vector2(256f, -256f), false);

            Assert.That(block.GetFloat(ESCompositeURPProperties.SSUExactContract), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.HologramLineGap), Is.EqualTo(3f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.HologramContrast), Is.EqualTo(12f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.HologramDistortionScale), Is.EqualTo(-4096f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.GlitchBrightness), Is.EqualTo(16f));
            Assert.That(block.GetVector(ESCompositeURPProperties.GlitchDistortion),
                Is.EqualTo(new Vector4(2f, -2f, 0f, 0f)));
            Assert.That(block.GetFloat(ESCompositeURPProperties.PixelOutlineWidth), Is.EqualTo(9f));
            Assert.That(ES2DCompositeURPProperties.HologramContrast,
                Is.EqualTo(ESUICompositeURPProperties.HologramContrast));
            Assert.That(ES2DCompositeURPProperties.PixelOutlineFade,
                Is.EqualTo(ESUICompositeURPProperties.PixelOutlineFade));
        }

        [Test]
        public void LegacyStylizedSetters_KeepCompatibilityClamps()
        {
            var block = new MaterialPropertyBlock();
            ESCompositeURPProperties.SetOutlines(
                block,
                true, Color.red, 3f,
                true, Color.green, 2f,
                true, Color.blue, 9f);
            ESCompositeURPProperties.SetHologram(
                block, true, Color.cyan, 4096f, 3f, 256f, 2f);

            Assert.That(block.GetFloat(ESCompositeURPProperties.InnerOutlineWidth), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.OuterOutlineWidth), Is.EqualTo(0.05f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.PixelOutlineWidth), Is.EqualTo(4f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.HologramLineFrequency), Is.EqualTo(2048f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.HologramLineGap), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.HologramSpeed), Is.EqualTo(128f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.HologramMinAlpha), Is.EqualTo(1f));
        }

        [Test]
        public void SSUExactStylizedSharedFunctions_MatchCommercialFormulaShape()
        {
            string source = File.ReadAllText(ShaderRoot + "ESCompositeSSUStylizedEffects.hlsl");
            Assert.That(source, Does.Contain("worldHeight / orthographicHeight"));
            Assert.That(source, Does.Contain("frac(localCoordinate).y"));
            Assert.That(source, Does.Contain(
                "return (color.r * 2.0 + color.g * 3.0 + color.b) / 6.0;"));
            Assert.That(source, Does.Contain(
                "pow(abs(sin(scanHeight * _HologramLineFrequency)), _HologramLineGap)"));
            Assert.That(source, Does.Contain("return max(maskNoise, _GlitchMaskMin) * _GlitchFade;"));
            Assert.That(source, Does.Contain("half3 firstTint = lerp(source.rgb, tint, colorWeight);"));
            Assert.That(source, Does.Contain("result.rgb = lerp(firstTint, tint, colorWeight);"));
            Assert.That(source, Does.Not.Contain("saturate((half)ESCompositeSSUGlitchFade"));
        }

        [Test]
        public void SSUStatusEffects_PreserveSourceExecutionOrder()
        {
            string source = File.ReadAllText(ShaderRoot + "ESCompositeSSUStatusEffects.hlsl");
            int frozen = source.IndexOf("ESCompositeApplySSUFrozen(color", System.StringComparison.Ordinal);
            int burn = source.IndexOf("ESCompositeApplySSUBurn(color", System.StringComparison.Ordinal);
            int rainbow = source.IndexOf("ESCompositeApplySSURainbow(color", System.StringComparison.Ordinal);
            int shine = source.IndexOf("ESCompositeApplySSUShine(color", System.StringComparison.Ordinal);
            int poison = source.IndexOf("ESCompositeApplySSUPoison(color", System.StringComparison.Ordinal);

            Assert.That(frozen, Is.GreaterThanOrEqualTo(0));
            Assert.That(burn, Is.GreaterThan(frozen));
            Assert.That(rainbow, Is.GreaterThan(burn));
            Assert.That(shine, Is.GreaterThan(rainbow));
            Assert.That(poison, Is.GreaterThan(shine));
            Assert.That(source, Does.Contain("maskUV * _ShineMask_ST.xy + _ShineMask_ST.zw"));

            string spriteSource = File.ReadAllText(ShaderRoot + "ES2DCompositeURP.shader");
            string litSource = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            Assert.That(spriteSource, Does.Contain("#define sampler_ShineMask sampler_MainTex"));
            Assert.That(litSource, Does.Contain("#define sampler_ShineMask sampler_BaseMap"));
            Assert.That(spriteSource, Does.Not.Contain("SAMPLER(sampler_ShineMask)"));
            Assert.That(litSource, Does.Not.Contain("SAMPLER(sampler_ShineMask)"));
        }

        [TestCase("ES2DCompositeURP.shader")]
        [TestCase("ESUICompositeURP.shader")]
        [TestCase("ES3DLitCompositeURP.shader")]
        public void RestoredSSUControls_AreDeclaredAndApplied(string fileName)
        {
            Shader shader = RequireShader(
                fileName == "ES2DCompositeURP.shader" ? "ES/2D/Composite URP"
                : fileName == "ESUICompositeURP.shader" ? "ES/UI/Composite URP"
                : "ES/3D/Lit Composite URP");
            AssertProperties(
                shader,
                "_AlphaTintFade",
                "_ReplaceContrast", "_ReplaceFade",
                "_SplitToneContrast", "_SplitToneShift",
                "_GlowContrast", "_GlowFade");

            string source = File.ReadAllText(ShaderRoot + fileName);
            Assert.That(source, Does.Contain("_AlphaTintFade"));
            Assert.That(source, Does.Contain("_ReplaceContrast"));
            Assert.That(source, Does.Contain("_ReplaceFade"));
            Assert.That(source, Does.Contain("_SplitToneContrast"));
            Assert.That(source, Does.Contain("_SplitToneShift"));
            Assert.That(source, Does.Contain("_GlowContrast"));
            Assert.That(source, Does.Contain("_GlowFade"));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteShaders_ExposeExtendedSSUEffectContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_EnableSqueeze", "_SqueezeFade", "_SqueezeScale", "_SqueezePower", "_SqueezeCenter",
                "_EnableSineRotate", "_SineRotateFade", "_SineRotateAngle", "_SineRotateFrequency", "_SineRotatePivot",
                "_EnableSineMove", "_SineMoveFade", "_SineMoveOffset", "_SineMoveFrequency",
                "_EnableSineScale", "_SineScaleFrequency", "_SineScaleFactor",
                "_EnableCustomFade", "_CustomFadeFadeMask", "_CustomFadeSmoothness", "_CustomFadeNoiseScale",
                "_CustomFadeNoiseFactor", "_CustomFadeAlpha",
                "_EnableFullGlowDissolve", "_FullGlowDissolveFade", "_FullGlowDissolveWidth",
                "_FullGlowDissolveEdgeColor", "_FullGlowDissolveNoiseScale",
                "_EnableCamouflage", "_CamouflageFade", "_CamouflageBaseColor", "_CamouflageContrast",
                "_CamouflageColorA", "_CamouflageDensityA", "_CamouflageSmoothnessA", "_CamouflageNoiseScaleA",
                "_CamouflageColorB", "_CamouflageDensityB", "_CamouflageSmoothnessB", "_CamouflageNoiseScaleB",
                "_CamouflageAnimationToggle", "_CamouflageDistortionSpeed", "_CamouflageDistortionIntensity",
                "_CamouflageDistortionScale",
                "_EnableMetal", "_MetalFade", "_MetalColor", "_MetalContrast", "_MetalHighlightColor",
                "_MetalHighlightDensity", "_MetalHighlightContrast", "_MetalNoiseScale", "_MetalNoiseSpeed",
                "_MetalNoiseDistortionScale", "_MetalNoiseDistortionSpeed", "_MetalNoiseDistortion",
                "_MetalMaskToggle", "_MetalMask",
                "_EnableEnchanted", "_EnchantedFade", "_EnchantedSpeed", "_EnchantedScale",
                "_EnchantedBrightness", "_EnchantedContrast", "_EnchantedReduce", "_EnchantedRainbowToggle",
                "_EnchantedRainbowSpeed", "_EnchantedRainbowDensity", "_EnchantedRainbowSaturation",
                "_EnchantedLowColor", "_EnchantedHighColor", "_EnchantedLerpToggle",
                "_EnableShifting", "_ShiftingFade", "_ShiftingSpeed", "_ShiftingDensity",
                "_ShiftingBrightness", "_ShiftingContrast", "_ShiftingRainbowToggle", "_ShiftingSaturation",
                "_ShiftingColorA", "_ShiftingColorB");

            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("#include \"ESCompositeSSUEffects.hlsl\""));
            AssertSourceTokenWithinEffect(source, "_EnableCustomFade", "_CustomFadeFadeMask", "_EnableSmoke");
            AssertSourceTokenWithinEffect(source, "_EnableCustomFade", "_UberNoiseTexture", "_EnableSmoke");
            AssertSourceTokenWithinEffect(source, "_EnableCamouflage", "_UberNoiseTexture", "_EnableMetal");
            AssertSourceTokenWithinEffect(source, "_EnableMetal", "_MetalMask", "_EnableEnchanted");
            AssertSourceTokenWithinEffect(source, "_EnableEnchanted", "_UberNoiseTexture", "_EnableShifting");
            AssertSourceTokenWithinEffect(source, "_EnableFullGlowDissolve", "_UberNoiseTexture", "_EnableShadow");

            int shadow = source.IndexOf("if (_EnableShadow > 0.5)", System.StringComparison.Ordinal);
            Assert.That(shadow, Is.GreaterThanOrEqualTo(0));
            Assert.That(source.IndexOf("fadeVisibility", shadow, System.StringComparison.Ordinal), Is.GreaterThan(shadow));
            Assert.That(source.IndexOf("customFadeVisibility", shadow, System.StringComparison.Ordinal), Is.GreaterThan(shadow));
            Assert.That(source.IndexOf("fullGlowVisibility", shadow, System.StringComparison.Ordinal), Is.GreaterThan(shadow));

            if (shaderName == "ES/2D/Composite URP")
                Assert.That(source, Does.Contain(
                    "source.a = processedSource.a * _Color.a * _RendererColor.a * customFadeVisibility;"));
            else
                Assert.That(source, Does.Contain("color.a = untintedAlpha * _Color.a * customFadeVisibility;"));
        }

        [Test]
        public void ExtendedSSUVertexMotion_UsesOriginalPositionAndParentSwitches()
        {
            string source = File.ReadAllText(ShaderRoot + "ESCompositeSpriteVertexMotion.hlsl");
            Assert.That(source, Does.Contain("float2 basePosition = positionOS.xy;"));
            Assert.That(source, Does.Contain("if (_EnableSineMove > 0.5)"));
            Assert.That(source, Does.Contain("* moveOffset * saturate(_SineMoveFade)"));
            Assert.That(source, Does.Contain("if (_EnableSineScale > 0.5)"));
            Assert.That(source, Does.Contain("positionOS.xy += basePosition * wave * factor;"));
            Assert.That(source, Does.Contain("clamp(_SineMoveFrequency.xy, -32.0, 32.0)"));
            Assert.That(source, Does.Contain("clamp(_SineScaleFactor.xy, -4.0, 4.0)"));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/3D/Lit Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void InteractiveVertexMotion_ExposesPerRendererChannels(string shaderName)
        {
            AssertProperties(
                RequireShader(shaderName),
                "_SquishFade",
                "_ESInteractiveWindRotation",
                "_ESInteractiveWindHeight",
                "_ESInteractiveSquish",
                "_ESWindPhaseOffset");

            string source = File.ReadAllText(ShaderRoot + "ESCompositeSpriteVertexMotion.hlsl");
            Assert.That(source, Does.Contain("saturate(_SquishFade)"));
            Assert.That(source, Does.Contain("if (abs(_ESInteractiveWindRotation) > 0.0001)"));
            Assert.That(source, Does.Contain("if (abs(_ESInteractiveSquish) > 0.0001)"));
            Assert.That(source, Does.Contain("+ _ESWindPhaseOffset"));
        }

        [Test]
        public void ExtendedSSUEffects_ClampUnsafeMathAndNormalizeLoopPhase()
        {
            string source = File.ReadAllText(ShaderRoot + "ESCompositeSSUEffects.hlsl");
            Assert.That(source, Does.Contain("clamp(power, 0.001, 8.0)"));
            Assert.That(source, Does.Contain("clamp(contrast, 0.001, 8.0)"));
            Assert.That(source, Does.Contain("lerp(source, effectColor, saturate(weight))"));
            Assert.That(source, Does.Contain("float phase = frac("));
            Assert.That(source, Does.Contain("max(visibility - (half)step(noise, innerThreshold), 0.0h)"));
        }

        [Test]
        public void ExtendedSSUParameters_ClampValuesAndShareSpriteIds()
        {
            var block = new MaterialPropertyBlock();
            ESCompositeURPProperties.SetSqueeze(block, true, Vector2.one, new Vector2(99f, -99f), 0f, 2f);
            ESCompositeURPProperties.SetSineRotate(block, true, Vector2.zero, 999f, -99f, -1f);
            ESCompositeURPProperties.SetSineMove(block, true, new Vector2(99f, -99f), new Vector2(99f, -99f), 2f);
            ESCompositeURPProperties.SetSineScale(block, true, new Vector2(99f, -99f), 99f);
            ESCompositeURPProperties.SetCustomFade(block, true, null, 0f, Vector2.zero, 2f, -1f);
            ESCompositeURPProperties.SetFullGlowDissolve(block, true, 2f, 0f, Color.red, Vector2.zero);
            ESCompositeURPProperties.SetCamouflage(
                block, true, Color.white, Color.red, 2f, 0f, Vector2.zero,
                Color.blue, -1f, 0f, Vector2.zero, 0f, 2f, true,
                new Vector2(99f, -99f), new Vector2(99f, -99f), Vector2.zero);
            ESCompositeURPProperties.SetMetal(
                block, true, Color.white, 0f, Color.yellow, 2f, 0f,
                Vector2.zero, new Vector2(99f, -99f), Vector2.zero,
                new Vector2(99f, -99f), new Vector2(99f, -99f), 2f, null);
            ESCompositeURPProperties.SetEnchanted(
                block, true, new Vector2(99f, -99f), Vector2.zero, 99f, 0f, 99f,
                Color.red, Color.blue, true, 99f, -99f, 2f, true, 2f);
            ESCompositeURPProperties.SetShifting(
                block, true, 99f, -99f, 99f, 0f, 2f, Color.red, Color.blue, true, 2f);

            Assert.That(block.GetVector(ESCompositeURPProperties.SqueezeScale).x, Is.EqualTo(8f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SqueezePower), Is.EqualTo(0.001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SqueezeFade), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SineRotateAngle), Is.EqualTo(720f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SineRotateFrequency), Is.EqualTo(-32f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SineRotateFade), Is.Zero);
            Assert.That(block.GetVector(ESCompositeURPProperties.SineMoveOffset).x, Is.EqualTo(8f));
            Assert.That(block.GetVector(ESCompositeURPProperties.SineScaleFactor).y, Is.EqualTo(-4f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.CustomFadeSmoothness), Is.EqualTo(0.001f));
            Assert.That(block.GetVector(ESCompositeURPProperties.CustomFadeNoiseScale).x, Is.EqualTo(0.001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.CustomFadeNoiseFactor), Is.EqualTo(0.5f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.CustomFadeAlpha), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.FullGlowDissolveFade), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FullGlowDissolveWidth), Is.EqualTo(0.001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.CamouflageDensityA), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.CamouflageDensityB), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.CamouflageContrast), Is.EqualTo(0.001f));
            Assert.That(block.GetVector(ESCompositeURPProperties.CamouflageDistortionIntensity).x, Is.EqualTo(4f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.MetalHighlightDensity), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.MetalMaskEnabled), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.EnchantedBrightness), Is.EqualTo(16f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.EnchantedReduce), Is.EqualTo(2f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.EnchantedRainbowSaturation), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.ShiftingSpeed), Is.EqualTo(32f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.ShiftingDensity), Is.EqualTo(-32f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.ShiftingContrast), Is.EqualTo(0.001f));
            Assert.That(ES2DCompositeURPProperties.SqueezeEnabled, Is.EqualTo(ESCompositeURPProperties.SqueezeEnabled));
            Assert.That(ESUICompositeURPProperties.CustomFadeMask, Is.EqualTo(ESCompositeURPProperties.CustomFadeMask));
            Assert.That(ES2DCompositeURPProperties.CamouflageAnimationEnabled,
                Is.EqualTo(ESUICompositeURPProperties.CamouflageAnimationEnabled));
            Assert.That(ESUICompositeURPProperties.MetalMaskEnabled, Is.EqualTo(ESCompositeURPProperties.MetalMaskEnabled));
            Assert.That(ES2DCompositeURPProperties.EnchantedLerpEnabled,
                Is.EqualTo(ESCompositeURPProperties.EnchantedLerpEnabled));
            Assert.That(ESUICompositeURPProperties.ShiftingRainbowEnabled,
                Is.EqualTo(ESCompositeURPProperties.ShiftingRainbowEnabled));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteShaders_ExposeSharedFadeFamilyContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_FadeMode",
                "_FadeProgress",
                "_FadePosition",
                "_FadeRotation",
                "_FadeWidth",
                "_FadeInvert",
                "_FadeNoiseFactor",
                "_FadeNoiseScale",
                "_FadeNoiseSpeed",
                "_FadeNoiseTex",
                "_FadeMask",
                "_DissolveEdgeColor",
                "_DissolveEdgeWidth",
                "_DissolveEdgeIntensity",
                "_FadeDistortionStrength");
            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("ESCompositeFade.hlsl"));
            Assert.That(source, Does.Contain("ESCompositeEvaluateFade"));
            Assert.That(source, Does.Contain("ESCompositeSourceFadeMask"));
            Assert.That((int)ESCompositeFadeMode.源点发光溶解, Is.EqualTo(7));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/3D/Lit Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void CompositeShader_FadeModeAvoidsOverLimitShaderLabEnumDrawer(string shaderName)
        {
            string source = ReadShaderSource(RequireShader(shaderName));

            Assert.That(source, Does.Contain("_FadeMode (\"渐隐模式\", Float) = 0"));
            Assert.That(source, Does.Not.Contain("[Enum(Off,0,DirectionalAlpha,1,TextureMask,2,FullAlphaDissolve,3,DirectionalGlow,4,DirectionalDistortion,5,SourceAlphaDissolve,6,SourceGlowDissolve,7)]"));
        }

        [Test]
        public void CompositeShaderInspector_FadeModeUsesAllEightCustomOptions()
        {
            string source = File.ReadAllText(Path.GetFullPath(EditorShaderRoot + "ESCompositeCodingHelper.cs"));
            string[] expectedOptions =
            {
                "方向透明渐隐",
                "纹理遮罩",
                "全局透明溶解",
                "方向发光渐隐",
                "方向扰动",
                "源点透明溶解",
                "源点发光溶解"
            };

            Assert.That(source, Does.Contain("private static readonly string[] FadeModeOptions"));
            Assert.That(source, Does.Contain("propertyName == \"_FadeMode\""));
            for (int i = 0; i < expectedOptions.Length; i++)
                Assert.That(source, Does.Contain("\"" + expectedOptions[i] + "\""));
        }

        [Test]
        public void SharedFadeParameters_ClampAndRejectMissingCustomMask()
        {
            var block = new MaterialPropertyBlock();
            ESCompositeURPProperties.SetFade(
                block,
                ESCompositeFadeMode.纹理遮罩,
                0.5f,
                0.1f,
                Vector2.one,
                0f,
                false,
                0f,
                Vector2.one,
                Vector2.zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.FadeMode), Is.Zero);

            ESCompositeURPProperties.SetFade(
                block,
                ESCompositeFadeMode.方向发光渐隐,
                -1f,
                2f,
                new Vector2(0.25f, 0.75f),
                -30f,
                true,
                2f,
                Vector2.zero,
                new Vector2(1f, -1f),
                edgeWidth: 0f,
                edgeIntensity: 20f,
                distortionStrength: 1f);

            Assert.That(block.GetFloat(ESCompositeURPProperties.FadeMode), Is.EqualTo(4f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FadeProgress), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.FadeWidth), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FadeRotation), Is.EqualTo(330f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FadeInvert), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FadeNoiseFactor), Is.EqualTo(1f));
            Assert.That(block.GetVector(ESCompositeURPProperties.FadeNoiseScale).x, Is.EqualTo(0.001f).Within(0.000001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FadeEdgeWidth), Is.EqualTo(0.001f).Within(0.000001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FadeEdgeIntensity), Is.EqualTo(8f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FadeDistortionStrength), Is.EqualTo(0.2f));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteShaders_ExposeSharedSamplingContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_EnableBlur",
                "_BlurRadius",
                "_BlurIntensity",
                "_BlurMode",
                "_EnableSharpen",
                "_SharpenAmount",
                "_SharpenRadius",
                "_SharpenThreshold",
                "_SharpenFade");
            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("ESCompositeSampling.hlsl"));
            Assert.That(source, Does.Contain("ESCompositeGaussian3x3"));
            Assert.That(source, Does.Contain("ESCompositeSharpen5"));
        }

        [Test]
        public void SharedSamplingParameters_ClampValues()
        {
            var block = new MaterialPropertyBlock();
            ESCompositeURPProperties.SetSampling(
                block,
                true,
                true,
                -1f,
                2f,
                true,
                8f,
                1f,
                -1f,
                -1f);

            Assert.That(block.GetFloat(ESCompositeURPProperties.BlurEnabled), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.BlurMode), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.BlurRadius), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.BlurIntensity), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SharpenEnabled), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SharpenAmount), Is.EqualTo(4f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SharpenRadius), Is.EqualTo(0.02f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SharpenThreshold), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.SharpenFade), Is.Zero);
            Assert.That(ES2DCompositeURPProperties.BlurMode, Is.EqualTo(ESCompositeURPProperties.BlurMode));
            Assert.That(ESUICompositeURPProperties.SharpenEnabled, Is.EqualTo(ESCompositeURPProperties.SharpenEnabled));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteShaders_ExposeGeneratedStylizationContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_TilingMode",
                "_WorldTilingScale",
                "_WorldTilingOffset",
                "_WorldTilingPixelsPerUnit",
                "_ScreenTilingScale",
                "_ScreenTilingOffset",
                "_ScreenTilingPixelsPerUnit",
                "_EnableSmoothPixelArt",
                "_SmoothPixelStrength",
                "_EnableCheckerboard",
                "_CheckerboardDarken",
                "_CheckerboardTiling",
                "_UberNoiseTexture",
                "_EnableFlame",
                "_FlameBrightness",
                "_FlameSmooth",
                "_FlameRadius",
                "_FlameSpeed",
                "_FlameNoiseFactor",
                "_FlameNoiseHeightFactor",
                "_FlameNoiseScale",
                "_EnableSmoke",
                "_SmokeAlpha",
                "_SmokeSmoothness",
                "_SmokeNoiseScale",
                "_SmokeNoiseFactor",
                "_SmokeDarkEdge",
                "_SmokeVertexSeed");
            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("ESCompositeGenerated.hlsl"));
            Assert.That(source, Does.Contain("ESCompositeResolveTilingUV"));
            Assert.That(source, Does.Contain("ESCompositeSmoothPixelUV"));
            Assert.That(source, Does.Contain("ESCompositeApplyCheckerboard"));
            Assert.That(source, Does.Contain("ESCompositeSmokeMask"));
            Assert.That(source, Does.Contain("ESCompositeFlameMask"));
        }

        [Test]
        public void GeneratedStylizationParameters_ClampValuesAndShareIds()
        {
            var block = new MaterialPropertyBlock();
            ESCompositeURPProperties.SetTiling(
                block,
                (ESCompositeTilingMode)99,
                Vector2.zero,
                Vector2.one,
                -1f,
                Vector2.zero,
                -Vector2.one,
                4096f);
            ESCompositeURPProperties.SetGeneratedStylization(block, true, 2f, true, -1f, 999f);

            Assert.That(block.GetFloat(ESCompositeURPProperties.TilingMode), Is.EqualTo(2f));
            Assert.That(block.GetVector(ESCompositeURPProperties.WorldTilingScale).x, Is.EqualTo(0.0001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.WorldTilingPixelsPerUnit), Is.EqualTo(0.01f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.ScreenTilingPixelsPerUnit), Is.EqualTo(2048f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SmoothPixelArtEnabled), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SmoothPixelStrength), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.CheckerboardDarken), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.CheckerboardTiling), Is.EqualTo(64f));
            Assert.That(ES2DCompositeURPProperties.TilingMode, Is.EqualTo(ESCompositeURPProperties.TilingMode));
            Assert.That(ESUICompositeURPProperties.CheckerboardEnabled, Is.EqualTo(ESCompositeURPProperties.CheckerboardEnabled));
        }

        [Test]
        public void LitRasterStylizationParameters_ClampValuesAndShareIds()
        {
            var block = new MaterialPropertyBlock();
            ESCompositeURPProperties.SetRasterStylization(
                block, true, 9999f, -1f, true, -1f, 999f, 2f);

            Assert.That(block.GetFloat(ESCompositeURPProperties.PixelateEnabled), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.PixelateCells), Is.EqualTo(512f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.PixelateStrength), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.HalftoneEnabled), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.HalftoneScale), Is.EqualTo(4f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.HalftoneAngle), Is.EqualTo(180f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.HalftoneStrength), Is.EqualTo(1f));
            Assert.That(ES3DLitCompositeURPProperties.PixelateCells, Is.EqualTo(ESCompositeURPProperties.PixelateCells));
            Assert.That(ES3DLitCompositeURPProperties.HalftoneStrength, Is.EqualTo(ESCompositeURPProperties.HalftoneStrength));
        }

        [Test]
        public void GeneratedFlameAndSmokeParameters_ClampValues()
        {
            var block = new MaterialPropertyBlock();
            ESCompositeURPProperties.SetFlame(
                block, true, null, 99f, -1f, 0f, Vector2.one,
                99f, -1f, Vector2.zero);
            ESCompositeURPProperties.SetSmoke(
                block, true, null, 2f, 9f, -1f, -1f, 9f, true);

            Assert.That(block.GetFloat(ESCompositeURPProperties.FlameEnabled), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FlameBrightness), Is.EqualTo(16f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FlameSmooth), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.FlameRadius), Is.EqualTo(0.01f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FlameNoiseFactor), Is.EqualTo(8f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.FlameNoiseHeightFactor), Is.Zero);
            Assert.That(block.GetVector(ESCompositeURPProperties.FlameNoiseScale).x, Is.EqualTo(0.001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SmokeAlpha), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SmokeSmoothness), Is.EqualTo(4f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SmokeNoiseScale), Is.EqualTo(0.01f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SmokeNoiseFactor), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.SmokeDarkEdge), Is.EqualTo(1.5f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SmokeVertexSeed), Is.EqualTo(1f));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteShaders_ExposeRGBAndRGBYCPRecolorContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_EnableRecolorRGB",
                "_RecolorRed",
                "_RecolorGreen",
                "_RecolorBlue",
                "_RecolorRGBStrength",
                "_RecolorRGBMaskToggle",
                "_RecolorRGBMask",
                "_RecolorRGBMaskChannel",
                "_EnableRecolorRGBYCP",
                "_RecolorRGBYCPRed",
                "_RecolorRGBYCPGreen",
                "_RecolorRGBYCPBlue",
                "_RecolorRGBYCPYellow",
                "_RecolorRGBYCPCyan",
                "_RecolorRGBYCPPurple",
                "_RecolorRGBYCPStrength",
                "_RecolorRGBYCPMaskToggle",
                "_RecolorRGBYCPMask",
                "_RecolorRGBYCPMaskChannel");
            Assert.That(ReadShaderSource(shader), Does.Contain("ESCompositeRecolorRGBYCP"));
        }

        [Test]
        public void RecolorParameters_ClampStrengthAndDisableMissingMasks()
        {
            var block = new MaterialPropertyBlock();
            ESCompositeURPProperties.SetRecolorRGB(
                block,
                true,
                Color.red,
                Color.green,
                Color.blue,
                2f,
                null,
                ESCompositeTextureChannel.蓝色);
            ESCompositeURPProperties.SetRecolorRGBYCP(
                block,
                true,
                Color.red,
                Color.green,
                Color.blue,
                Color.yellow,
                Color.cyan,
                Color.magenta,
                -1f);

            Assert.That(block.GetFloat(ESCompositeURPProperties.RecolorRGBEnabled), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.RecolorRGBStrength), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.RecolorRGBMaskEnabled), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.RecolorRGBMaskChannel), Is.EqualTo(2f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.RecolorRGBYCPEnabled), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.RecolorRGBYCPStrength), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.RecolorRGBYCPMaskEnabled), Is.Zero);
        }

        [Test]
        public void TwoDShader_ExposesAndAppliesMaterialBlendModes()
        {
            Material material = CreateTestMaterial(RequireShader("ES/2D/Composite URP"));
            try
            {
                AssertProperties(material.shader, "_BlendMode", "_SrcBlend", "_DstBlend", "_BlendOp");
                ES2DCompositeURPProperties.SetBlendMode(material, ES2DCompositeBlendMode.正片叠底);
                Assert.That(material.GetFloat(ES2DCompositeURPProperties.BlendMode), Is.EqualTo(3f));
                Assert.That(material.GetFloat(ES2DCompositeURPProperties.SrcBlend),
                    Is.EqualTo((float)UnityEngine.Rendering.BlendMode.DstColor));
                Assert.That(material.GetFloat(ES2DCompositeURPProperties.DstBlend),
                    Is.EqualTo((float)UnityEngine.Rendering.BlendMode.Zero));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteShaders_ExposeTextureLayerContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_EnableTextureLayer1",
                "_TextureLayer1Texture",
                "_TextureLayer1Fade",
                "_TextureLayer1Color",
                "_TextureLayer1Scale",
                "_TextureLayer1Offset",
                "_TextureLayer1ScrollToggle",
                "_TextureLayer1ScrollSpeed",
                "_TextureLayer1SheetToggle",
                "_TextureLayer1Columns",
                "_TextureLayer1Rows",
                "_TextureLayer1Speed",
                "_TextureLayer1StartFrame",
                "_TextureLayer1EdgeClip",
                "_TextureLayer1ContrastToggle",
                "_TextureLayer1Contrast",
                "_EnableTextureLayer2",
                "_TextureLayer2Texture");
            Assert.That(ReadShaderSource(shader), Does.Contain("ESApplyTextureLayers"));
        }

        [Test]
        public void TextureLayerParameters_RequireTextureAndClampSheetValues()
        {
            var block = new MaterialPropertyBlock();
            ESCompositeURPProperties.SetTextureLayer(
                block,
                ESCompositeTextureLayer.层一,
                true,
                null,
                Color.white,
                Vector2.one,
                Vector2.zero,
                true,
                Vector2.one,
                true,
                0,
                100,
                12f,
                -2,
                1f,
                true,
                8f,
                2f);

            Assert.That(block.GetFloat(ESCompositeURPProperties.TextureLayer1Enabled), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.TextureLayer1Columns), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.TextureLayer1Rows), Is.EqualTo(64f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.TextureLayer1StartFrame), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.TextureLayer1EdgeClip), Is.EqualTo(0.49f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.TextureLayer1Contrast), Is.EqualTo(4f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.TextureLayer1Fade), Is.EqualTo(1f));
        }

        [TestCase("ES/2D/Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void SpriteShaders_ExposeVertexMotionContract(string shaderName)
        {
            Shader shader = RequireShader(shaderName);
            AssertProperties(
                shader,
                "_EnableWind",
                "_WindDirection",
                "_WindAmplitude",
                "_WindFrequency",
                "_WindSpeed",
                "_WindAnchor",
                "_WindAnchorDirection",
                "_WindGlobalInfluence",
                "_EnableSquish",
                "_SquishAmount",
                "_SquishSpeed",
                "_EnableWiggle",
                "_WiggleAmplitude",
                "_WiggleFrequency",
                "_WiggleDirection",
                "_WiggleSpeed",
                "_EnableVibrate",
                "_VibrateAmplitude",
                "_VibrateSpeed");
            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("ESApplyVertexMotion"));
            Assert.That(source, Does.Contain("_ESCompositeGlobalWind"));
        }

        [Test]
        public void VertexMotionParameters_NormalizeAndClampPropertyBlockValues()
        {
            var block = new MaterialPropertyBlock();

            ESCompositeURPProperties.SetWind(block, true, Vector2.zero, -2f, -3f, -4f, 2f, -1f);
            ESCompositeURPProperties.SetSquish(block, true, 2f, -1f);
            ESCompositeURPProperties.SetWiggle(block, true, 90f, -2f, -3f);
            ESCompositeURPProperties.SetVibrate(block, true, -5f, -6f);

            Vector4 direction = block.GetVector(ESCompositeURPProperties.WindDirection);
            Assert.That(direction.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(direction.y, Is.Zero.Within(0.0001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.WindAmplitude), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.WindAnchor), Is.EqualTo(1f));
            Assert.That(block.GetVector(ESCompositeURPProperties.WindAnchorDirection), Is.EqualTo(new Vector4(0f, 1f, 0f, 0f)));
            Assert.That(block.GetFloat(ESCompositeURPProperties.WindGlobalInfluence), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.SquishAmount), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.SquishSpeed), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.WiggleAmplitude), Is.EqualTo(45f).Within(0.0001f));
            Assert.That(block.GetFloat(ESCompositeURPProperties.WiggleFrequency), Is.Zero);
            Assert.That(block.GetVector(ESCompositeURPProperties.WiggleDirection), Is.EqualTo(new Vector4(0f, 1f, 0f, 0f)));
            Assert.That(block.GetFloat(ESCompositeURPProperties.VibrateAmplitude), Is.Zero);
            Assert.That(block.GetFloat(ESCompositeURPProperties.VibrateSpeed), Is.Zero);
        }

        [Test]
        public void DirectionalVertexAndGlitchApis_NormalizeAndRestoreCompatibilityDefaults()
        {
            var block = new MaterialPropertyBlock();

            ESCompositeURPProperties.SetWind(
                block, true, Vector2.right, 1f, 2f, 3f, 0.25f, 0.5f, new Vector2(-3f, 4f));
            ESCompositeURPProperties.SetWiggle(
                block, true, 10f, 2f, 3f, new Vector2(4f, 3f));
            ESCompositeURPProperties.SetGlitchScanDirection(block, new Vector3(0f, 0f, -2f));
            ESCompositeURPProperties.SetRainbow(block, true, 2f, 3f, 4f, new Vector2(-4f, 3f));

            Assert.That(block.GetVector(ESCompositeURPProperties.WindAnchorDirection),
                Is.EqualTo(new Vector4(-0.6f, 0.8f, 0f, 0f)));
            Assert.That(block.GetVector(ESCompositeURPProperties.WiggleDirection),
                Is.EqualTo(new Vector4(0.8f, 0.6f, 0f, 0f)));
            Assert.That(block.GetVector(ESCompositeURPProperties.GlitchScanDirection),
                Is.EqualTo(new Vector4(0f, 0f, -1f, 0f)));
            Assert.That(block.GetVector(ESCompositeURPProperties.RainbowDirection),
                Is.EqualTo(new Vector4(-0.8f, 0.6f, 0f, 0f)));

            ESCompositeURPProperties.SetWind(block, true, Vector2.right, 1f, 2f, 3f);
            ESCompositeURPProperties.SetWiggle(block, true, 10f, 2f, 3f);
            ESCompositeURPProperties.SetGlitch(block, true, 0.1f, 2f);
            ESCompositeURPProperties.SetRainbow(block, true, 2f, 3f, 4f);

            Assert.That(block.GetVector(ESCompositeURPProperties.WindAnchorDirection),
                Is.EqualTo(new Vector4(0f, 1f, 0f, 0f)));
            Assert.That(block.GetVector(ESCompositeURPProperties.WiggleDirection),
                Is.EqualTo(new Vector4(0f, 1f, 0f, 0f)));
            Assert.That(block.GetVector(ESCompositeURPProperties.GlitchScanDirection),
                Is.EqualTo(new Vector4(0f, 1f, 0f, 0f)));
            Assert.That(block.GetVector(ESCompositeURPProperties.RainbowDirection),
                Is.EqualTo(new Vector4(0f, 1f, 0f, 0f)));
        }

        [Test]
        public void GlobalWind_HighestPriorityPublishesAndFallsBackOnDisable()
        {
            var lowObject = new GameObject("ES Global Wind Low");
            var highObject = new GameObject("ES Global Wind High");
            ESCompositeGlobalWind low = lowObject.AddComponent<ESCompositeGlobalWind>();
            ESCompositeGlobalWind high = highObject.AddComponent<ESCompositeGlobalWind>();

            try
            {
                low.Configure(Vector2.right, 1.5f, 2f, int.MaxValue - 2);
                high.Configure(Vector2.up, 3f, 4f, int.MaxValue - 1);
                Assert.That(ESCompositeGlobalWind.ActiveWind, Is.SameAs(high));
                Vector4 published = Shader.GetGlobalVector(ESCompositeURPProperties.GlobalWind);
                Assert.That(published.x, Is.Zero.Within(0.0001f));
                Assert.That(published.y, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(published.z, Is.EqualTo(3f).Within(0.0001f));
                Assert.That(published.w, Is.EqualTo(4f).Within(0.0001f));
                Assert.That(Shader.GetGlobalFloat(ESCompositeURPProperties.GlobalWindValid), Is.EqualTo(1f));

                high.enabled = false;
                Assert.That(ESCompositeGlobalWind.ActiveWind, Is.SameAs(low));
                published = Shader.GetGlobalVector(ESCompositeURPProperties.GlobalWind);
                Assert.That(published.x, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(published.y, Is.Zero.Within(0.0001f));
                Assert.That(published.z, Is.EqualTo(1.5f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(highObject);
                Object.DestroyImmediate(lowObject);
            }
        }

        [Test]
        public void VertexMotionBounds_ExpandsWithoutCompoundingAndRestoresOnDisable()
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Renderer renderer = gameObject.GetComponent<Renderer>();
            Bounds original = renderer.localBounds;
            ESCompositeVertexMotionBounds bounds = gameObject.AddComponent<ESCompositeVertexMotionBounds>();

            try
            {
                var padding = new Vector3(0.2f, 0.3f, 0.4f);
                bounds.Configure(padding);
                Vector3 expectedSize = original.size + padding * 2f;
                Assert.That(renderer.localBounds.size.x, Is.EqualTo(expectedSize.x).Within(0.0001f));
                Assert.That(renderer.localBounds.size.y, Is.EqualTo(expectedSize.y).Within(0.0001f));
                Assert.That(renderer.localBounds.size.z, Is.EqualTo(expectedSize.z).Within(0.0001f));

                bounds.RefreshBounds();
                Assert.That(renderer.localBounds.size.x, Is.EqualTo(expectedSize.x).Within(0.0001f),
                    "Repeated refresh must start from the renderer source bounds, not the previous expansion.");

                bounds.enabled = false;
                Assert.That(renderer.localBounds.size.x, Is.EqualTo(original.size.x).Within(0.0001f));
                Assert.That(renderer.localBounds.size.y, Is.EqualTo(original.size.y).Within(0.0001f));
                Assert.That(renderer.localBounds.size.z, Is.EqualTo(original.size.z).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase("ES/3D/Lit Composite URP")]
        [TestCase("ES/3D/VFX Composite URP")]
        [TestCase("ES/UI/Composite URP")]
        public void QualityDefaults_ToBasicWithoutKeywords(string shaderName)
        {
            Material material = CreateTestMaterial(RequireShader(shaderName));

            try
            {
                Assert.That(material.GetFloat("_QualityTier"), Is.Zero);
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_STANDARD"), Is.False);
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_HIGH"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void UIShader_ExposesSdfQualityAndBlendContract()
        {
            Shader shader = RequireShader("ES/UI/Composite URP");

            AssertProperties(
                shader,
                "_EnableSDF",
                "_SDFThreshold",
                "_SDFSoftness",
                "_SDFOutlineWidth",
                "_SDFOutlineSoftness",
                "_SDFOutlineColor",
                "_SDFGlowWidth",
                "_SDFGlowColor",
                "_QualityTier",
                "_BlendMode",
                "_SrcBlend",
                "_DstBlend");
        }

        [Test]
        public void UIShader_ExposesTextMeshProCompatibilityContract()
        {
            Shader shader = RequireShader("ES/UI/Composite URP");
            AssertProperties(
                shader,
                "_EnableTMPCompatibility",
                "_FaceColor",
                "_FaceDilate",
                "_OutlineColor",
                "_OutlineWidth",
                "_OutlineSoftness",
                "_EnableUnderlay",
                "_UnderlayColor",
                "_UnderlayOffsetX",
                "_UnderlayOffsetY",
                "_UnderlayDilate",
                "_UnderlaySoftness",
                "_WeightNormal",
                "_WeightBold",
                "_ScaleRatioA",
                "_ScaleRatioB",
                "_ScaleRatioC",
                "_GradientScale",
                "_Sharpness",
                "_TextureWidth",
                "_TextureHeight",
                "_MaskSoftnessX",
                "_MaskSoftnessY",
                "_ShaderFlags",
                "_ClipRect",
                "_CullMode");
            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("ESApplyTMP"));
            Assert.That(source, Does.Contain("UNDERLAY_ON"));
            Assert.That(source, Does.Contain("TEXCOORD1"));
        }

        [Test]
        public void TMPAdapter_CopiesFontMaterialContractIntoManagedCompositeInstance()
        {
            Shader tmpShader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            Assert.That(tmpShader, Is.Not.Null, "TMP mobile distance-field shader is required for this contract test.");
            var source = new Material(tmpShader);
            Material converted = null;

            try
            {
                Color faceColor = new Color(0.2f, 0.4f, 0.8f, 0.75f);
                source.SetColor("_FaceColor", faceColor);
                source.SetFloat("_OutlineWidth", 0.25f);
                source.SetFloat("_GradientScale", 7f);
                source.EnableKeyword("UNDERLAY_ON");
                source.EnableKeyword("UNITY_UI_CLIP_RECT");

                converted = ESCompositeTMPMaterialAdapter.CreateRuntimeMaterial(source);

                Assert.That(converted, Is.Not.Null);
                Assert.That(converted.shader.name, Is.EqualTo("ES/UI/Composite URP"));
                Assert.That(converted.GetFloat("_EnableTMPCompatibility"), Is.EqualTo(1f));
                Assert.That(converted.GetColor("_FaceColor"), Is.EqualTo(faceColor));
                Assert.That(converted.GetFloat("_OutlineWidth"), Is.EqualTo(0.25f));
                Assert.That(converted.GetFloat("_GradientScale"), Is.EqualTo(7f));
                Assert.That(converted.IsKeywordEnabled("OUTLINE_ON"), Is.True);
                Assert.That(converted.IsKeywordEnabled("UNDERLAY_ON"), Is.True);
                Assert.That(converted.IsKeywordEnabled("UNITY_UI_CLIP_RECT"), Is.True);
            }
            finally
            {
                if (converted != null) Object.DestroyImmediate(converted);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void UIParameters_SetTMPClampsCompatibilityValues()
        {
            var block = new MaterialPropertyBlock();
            ESUICompositeURPProperties.SetTMP(
                block, true, Color.white, 2f, Color.black, 2f, -1f, true, Color.black,
                new Vector2(2f, -2f), -2f, 2f, -1f, 1f, -1f, 0f, 0f, -2f, 2f,
                new Vector2(0f, 0f));

            Assert.That(block.GetFloat(ESUICompositeURPProperties.TMPCompatibility), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESUICompositeURPProperties.TMPFaceDilate), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESUICompositeURPProperties.TMPOutlineWidth), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESUICompositeURPProperties.TMPOutlineSoftness), Is.Zero);
            Assert.That(block.GetFloat(ESUICompositeURPProperties.TMPUnderlayOffsetX), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESUICompositeURPProperties.TMPUnderlayOffsetY), Is.EqualTo(-1f));
            Assert.That(block.GetFloat(ESUICompositeURPProperties.TMPTextureWidth), Is.EqualTo(1f));
            Assert.That(block.GetFloat(ESUICompositeURPProperties.TMPTextureHeight), Is.EqualTo(1f));
        }

        [Test]
        public void UIShader_ExposesCanvasStencilContract()
        {
            Shader shader = RequireShader("ES/UI/Composite URP");
            Material material = CreateTestMaterial(shader);

            string[] properties =
            {
                "_StencilComp",
                "_Stencil",
                "_StencilOp",
                "_StencilWriteMask",
                "_StencilReadMask",
                "_ColorMask",
                "_UseUIAlphaClip"
            };

            try
            {
                for (int i = 0; i < properties.Length; i++)
                    Assert.That(material.HasProperty(properties[i]), Is.True,
                        "UI shader is missing Canvas property " + properties[i] + ".");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void UIShader_ExposesCompleteSSUGuiColorOutlineGlowAndStatusContract()
        {
            Shader shader = RequireShader("ES/UI/Composite URP");
            AssertProperties(
                shader,
                "_EnableAddColor", "_AddColor", "_AddColorFade",
                "_EnableStrongTint", "_StrongTint", "_StrongTintFade",
                "_EnableAlphaTint", "_AlphaTint", "_AlphaTintMin",
                "_EnableColorReplace", "_ReplaceFrom", "_ReplaceTo", "_ReplaceRange", "_ReplaceSoftness",
                "_EnableBrightness", "_Brightness",
                "_EnableContrast", "_Contrast",
                "_EnableSaturation", "_Saturation",
                "_EnableHue", "_Hue",
                "_EnableNegative", "_NegativeFade",
                "_EnableRainbow", "_RainbowSpeed", "_RainbowDensity", "_RainbowBrightness",
                "_EnableInnerOutline", "_InnerOutlineColor", "_InnerOutlineWidth",
                "_EnableOuterOutline", "_OuterOutlineColor", "_OuterOutlineWidth",
                "_EnablePixelOutline", "_PixelOutlineColor", "_PixelOutlineWidth",
                "_EnablePingPongGlow", "_GlowFrom", "_GlowTo", "_GlowFrequency", "_GlowIntensity",
                "_EnableFrozen", "_FrozenColor", "_FrozenHighlight", "_FrozenDensity", "_FrozenSpeed",
                "_EnableBurn", "_BurnEdgeColor", "_BurnInsideColor", "_BurnProgress", "_BurnWidth",
                "_EnablePoison", "_PoisonColor", "_PoisonDensity", "_PoisonSpeed",
                "_SSUStatusContract",
                "_FrozenFade", "_FrozenTint", "_FrozenContrast", "_FrozenSnowColor",
                "_FrozenSnowContrast", "_FrozenSnowDensity", "_FrozenSnowScale",
                "_FrozenHighlightColor", "_FrozenHighlightContrast", "_FrozenHighlightDensity",
                "_FrozenHighlightSpeed", "_FrozenHighlightScale", "_FrozenHighlightDistortion",
                "_FrozenHighlightDistortionSpeed", "_FrozenHighlightDistortionScale",
                "_BurnFade", "_BurnPosition", "_BurnRadius", "_BurnEdgeNoiseScale",
                "_BurnEdgeNoiseFactor", "_BurnInsideContrast", "_BurnInsideNoiseColor",
                "_BurnInsideNoiseFactor", "_BurnInsideNoiseScale", "_BurnSwirlFactor", "_BurnSwirlNoiseScale",
                "_RainbowFade", "_RainbowSaturation", "_RainbowContrast", "_RainbowCenter",
                "_RainbowNoiseScale", "_RainbowNoiseFactor",
                "_ShineFade", "_ShineSaturation", "_ShineContrast", "_ShineRotation",
                "_ShineSmooth", "_ShineFrequency", "_ShineMaskToggle", "_ShineMask",
                "_PoisonFade", "_PoisonRecolorFactor", "_PoisonShiftSpeed",
                "_PoisonNoiseBrightness", "_PoisonNoiseScale", "_PoisonNoiseSpeed");

            string source = ReadShaderSource(shader);
            Assert.That(source, Does.Contain("if (_EnableTMPCompatibility < 0.5 && _EnableSDF < 0.5)"));
            Assert.That(source, Does.Contain("ESSampleUITexture(uv + float2(_InnerOutlineWidth, 0.0)).a"));
            Assert.That(source, Does.Contain("rcp(ESSpritePixelSize()) * _PixelOutlineWidth"));
            Assert.That(source, Does.Not.Contain("ESSampleMainTexture"));

            const string statusGate = "if (_SSUStatusContract <= 0.5";
            int statusGateIndex = source.IndexOf(statusGate);
            Assert.That(statusGateIndex, Is.GreaterThanOrEqualTo(0));
            int statusNoiseIndex = source.IndexOf("float statusNoise", statusGateIndex);
            Assert.That(statusNoiseIndex, Is.GreaterThan(statusGateIndex));
            int canvasClipIndex = source.IndexOf("#ifdef UNITY_UI_CLIP_RECT", statusNoiseIndex);
            Assert.That(canvasClipIndex, Is.GreaterThan(statusNoiseIndex));
            Assert.That(source.IndexOf("_UberNoiseTexture", statusGateIndex), Is.LessThan(canvasClipIndex));

            Assert.That(ESUICompositeURPProperties.AddColorEnabled,
                Is.EqualTo(ESCompositeURPProperties.AddColorEnabled));
            Assert.That(ESUICompositeURPProperties.PixelOutlineWidth,
                Is.EqualTo(ESCompositeURPProperties.PixelOutlineWidth));
            Assert.That(ESUICompositeURPProperties.BurnProgress,
                Is.EqualTo(ESCompositeURPProperties.BurnProgress));
            Assert.That(ESUICompositeURPProperties.PoisonSpeed,
                Is.EqualTo(ESCompositeURPProperties.PoisonSpeed));
        }

        [Test]
        public void SharedPreset_CapturesAndRestoresVisibleMaterialProperties()
        {
            Shader shader = RequireShader("ES/2D/Composite URP");
            Shader otherShader = RequireShader("ES/UI/Composite URP");
            var material = new Material(shader);
            var incompatible = new Material(otherShader);
            var texture = new Texture2D(4, 4);
            var preset = ScriptableObject.CreateInstance<ES.EditorInternal.ESCompositeShaderPreset>();

            try
            {
                material.SetFloat("_EnableBrightness", 1f);
                material.SetFloat("_Brightness", 1.75f);
                material.SetTexture("_NoiseTex", texture);
                material.SetTextureScale("_NoiseTex", new Vector2(2f, 3f));
                material.SetTextureOffset("_NoiseTex", new Vector2(0.1f, 0.2f));

                Assert.That(preset.CaptureFrom(material), Is.GreaterThan(0));
                Assert.That(preset.IsCompatible(material), Is.True);
                Assert.That(preset.IsCompatible(incompatible), Is.False);
                Assert.That(preset.ContainsProperty("_Brightness"), Is.True);
                Assert.That(preset.ContainsProperty("_NoiseTex"), Is.True);
                Assert.That(preset.ContainsProperty("_MainTex"), Is.False,
                    "PerRendererData texture must not be frozen into a shared preset.");
                Assert.That(preset.ContainsProperty("_SpriteUVRect"), Is.False,
                    "Hidden per-sprite state must not be frozen into a shared preset.");

                material.SetFloat("_Brightness", 0.25f);
                material.SetTexture("_NoiseTex", null);
                Assert.That(preset.ApplyTo(material), Is.GreaterThan(0));
                Assert.That(material.GetFloat("_Brightness"), Is.EqualTo(1.75f).Within(0.0001f));
                Assert.That(material.GetTexture("_NoiseTex"), Is.SameAs(texture));
                Assert.That(material.GetTextureScale("_NoiseTex"), Is.EqualTo(new Vector2(2f, 3f)));
                Assert.That(material.GetTextureOffset("_NoiseTex"), Is.EqualTo(new Vector2(0.1f, 0.2f)));
                Assert.That(preset.ApplyTo(incompatible), Is.Zero);

                var serializedPreset = new SerializedObject(preset);
                serializedPreset.FindProperty("materialSchemaVersion").intValue =
                    ES.EditorInternal.ESCompositeMaterialMigration.CurrentVersion + 1;
                serializedPreset.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(preset.IsCompatible(material), Is.False,
                    "A preset authored by a future schema must not be applied silently.");
            }
            finally
            {
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(incompatible);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void MaterialInstance_AcquireAndRelease_RestoresRendererMaterial()
        {
            Shader shader = RequireShader("ES/2D/Composite URP");
            var source = new Material(shader);
            var gameObject = new GameObject("ES Material Instance Test");
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = source;
            ESCompositeMaterialInstance instance = gameObject.AddComponent<ESCompositeMaterialInstance>();
            instance.Configure(renderer);

            try
            {
                Material runtime = instance.Acquire();
                Assert.That(runtime, Is.Not.Null);
                Assert.That(runtime, Is.Not.SameAs(source));
                Assert.That(renderer.sharedMaterial, Is.SameAs(runtime));

                instance.Release();
                Assert.That(renderer.sharedMaterial, Is.SameAs(source));
                Assert.That(instance.RuntimeMaterial, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void InteractiveShaderComponents_PreserveUnrelatedPropertyBlockState()
        {
            const int sentinelValue = 37;
            int sentinel = Shader.PropertyToID("_ESContractSentinel");
            var gameObjects = new GameObject[3];

            try
            {
                for (int i = 0; i < gameObjects.Length; i++)
                {
                    gameObjects[i] = new GameObject("ES Interactive MPB Test " + i);
                    SpriteRenderer renderer = gameObjects[i].AddComponent<SpriteRenderer>();
                    var initialBlock = new MaterialPropertyBlock();
                    initialBlock.SetFloat(sentinel, sentinelValue);
                    renderer.SetPropertyBlock(initialBlock);

                    Behaviour component;
                    int ownedProperty;
                    if (i == 0)
                    {
                        component = gameObjects[i].AddComponent<ESCompositeInteractiveWind2D>();
                        ownedProperty = ESCompositeURPProperties.InteractiveWindRotation;
                    }
                    else if (i == 1)
                    {
                        component = gameObjects[i].AddComponent<ESCompositeInteractiveSquish2D>();
                        ownedProperty = ESCompositeURPProperties.InteractiveSquish;
                    }
                    else
                    {
                        gameObjects[i].transform.position = new Vector3(4.25f, 0f, 0f);
                        var windParallax = gameObjects[i].AddComponent<ESCompositeWindParallax>();
                        windParallax.Refresh();
                        component = windParallax;
                        ownedProperty = ESCompositeURPProperties.WindPhaseOffset;
                    }

                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    Assert.That(block.GetFloat(sentinel), Is.EqualTo(sentinelValue));
                    if (i == 2)
                        Assert.That(block.GetFloat(ownedProperty), Is.EqualTo(4.25f).Within(0.0001f));

                    // EditMode does not synchronously dispatch the runtime-only callback.
                    // Invoke the same cleanup entry explicitly to validate MPB ownership.
                    component.GetType()
                        .GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        ?.Invoke(component, null);
                    component.enabled = false;
                    block.Clear();
                    renderer.GetPropertyBlock(block);
                    Assert.That(block.GetFloat(sentinel), Is.EqualTo(sentinelValue));
                    Assert.That(block.GetFloat(ownedProperty), Is.Zero.Within(0.0001f));
                }
            }
            finally
            {
                for (int i = 0; i < gameObjects.Length; i++)
                    if (gameObjects[i] != null)
                        Object.DestroyImmediate(gameObjects[i]);
            }
        }

        [Test]
        public void InteractiveShaderComponents_DoNotInstantiateMaterialsOrClearPropertyBlocks()
        {
            const string runtimeRoot = "Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/";
            string[] files =
            {
                "ESCompositeInteractiveWind2D.cs",
                "ESCompositeInteractiveSquish2D.cs",
                "ESCompositeWindParallax.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                string source = File.ReadAllText(runtimeRoot + files[i]);
                Assert.That(source, Does.Contain("GetPropertyBlock(propertyBlock)"));
                Assert.That(source, Does.Contain("SetPropertyBlock(propertyBlock)"));
                Assert.That(source, Does.Not.Contain("SetPropertyBlock(null)"));
                Assert.That(source, Does.Not.Contain("new Material("));
                Assert.That(source, Does.Not.Contain(".material"));
            }

            string parallaxSource = File.ReadAllText(runtimeRoot + "ESCompositeWindParallax.cs");
            Assert.That(parallaxSource, Does.Contain("transform.position.x * phaseScale"));
        }

        [Test]
        public void ShaderFader_DefaultTrack_WritesRendererPropertyBlock()
        {
            Shader shader = RequireShader("ES/2D/Composite URP");
            var source = new Material(shader);
            var gameObject = new GameObject("ES Shader Fader Test");
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = source;
            ESCompositeShaderFader fader = gameObject.AddComponent<ESCompositeShaderFader>();

            try
            {
                fader.RefreshTargets();
                fader.SetProgress(0.7f);
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat(Shader.PropertyToID("_FadeProgress")), Is.EqualTo(0.7f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void ShaderFader_MultiMaterialRenderer_WritesOnlyCompositeSlots()
        {
            Shader compositeShader = RequireShader("ES/3D/Lit Composite URP");
            Shader otherShader = Shader.Find("Sprites/Default");
            Assert.That(otherShader, Is.Not.Null);
            var composite = new Material(compositeShader);
            var other = new Material(otherShader);
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Renderer renderer = gameObject.GetComponent<Renderer>();
            renderer.sharedMaterials = new[] { other, composite };
            ESCompositeShaderFader fader = gameObject.AddComponent<ESCompositeShaderFader>();

            try
            {
                fader.RefreshTargets();
                fader.SetProgress(0.65f);
                int fadeProgress = Shader.PropertyToID("_FadeProgress");
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, 0);
                Assert.That(block.GetFloat(fadeProgress), Is.Zero);
                block.Clear();
                renderer.GetPropertyBlock(block, 1);
                Assert.That(block.GetFloat(fadeProgress), Is.EqualTo(0.65f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(other);
                Object.DestroyImmediate(composite);
            }
        }

        [Test]
        public void ShaderFaderEditor_CopiesMaterialValuesToTrackEndpoints()
        {
            Shader shader = RequireShader("ES/2D/Composite URP");
            var material = new Material(shader);
            var gameObject = new GameObject("ES Shader Fader Editor Test");
            ESCompositeShaderFader fader = gameObject.AddComponent<ESCompositeShaderFader>();

            try
            {
                material.SetFloat("_FadeProgress", 0.2f);
                Assert.That(
                    ES.EditorInternal.ESCompositeShaderFaderEditor.CopyMaterialToTracks(fader, material, true),
                    Is.EqualTo(1));
                material.SetFloat("_FadeProgress", 0.8f);
                Assert.That(
                    ES.EditorInternal.ESCompositeShaderFaderEditor.CopyMaterialToTracks(fader, material, false),
                    Is.EqualTo(1));

                var serializedFader = new SerializedObject(fader);
                SerializedProperty track = serializedFader.FindProperty("tracks").GetArrayElementAtIndex(0);
                Assert.That(
                    track.FindPropertyRelative("valueType").enumValueIndex,
                    Is.EqualTo((int)ESCompositeShaderFadeValueType.Float));
                Assert.That(track.FindPropertyRelative("fromFloat").floatValue, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(track.FindPropertyRelative("toFloat").floatValue, Is.EqualTo(0.8f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ShaderFader_RefreshTargets_AddsInstancesOnlyToCompositeGraphics()
        {
            Shader compositeShader = RequireShader("ES/UI/Composite URP");
            Shader defaultShader = Shader.Find("UI/Default");
            Assert.That(defaultShader, Is.Not.Null);
            var composite = new Material(compositeShader);
            var other = new Material(defaultShader);
            var root = new GameObject("ES Shader Fader UI Root");
            var child = new GameObject("Graphic", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(root.transform);
            Image image = child.GetComponent<Image>();
            ESCompositeShaderFader fader = root.AddComponent<ESCompositeShaderFader>();

            try
            {
                image.material = other;
                fader.RefreshTargets();
                Assert.That(child.GetComponent<ESCompositeMaterialInstance>(), Is.Null);

                image.material = composite;
                fader.RefreshTargets();
                Assert.That(child.GetComponent<ESCompositeMaterialInstance>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(other);
                Object.DestroyImmediate(composite);
            }
        }

        [Test]
        public void SpriteUvDriver_WritesSpriteSubRectWithoutChangingSharedMaterial()
        {
            Shader shader = RequireShader("ES/2D/Composite URP");
            var source = new Material(shader);
            var texture = new Texture2D(8, 8);
            Sprite sprite = Sprite.Create(texture, new Rect(2, 1, 4, 3), new Vector2(0.5f, 0.5f));
            var gameObject = new GameObject("ES Sprite UV Driver Test");
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = source;
            renderer.sprite = sprite;
            ESCompositeSpriteUVDriver driver = gameObject.AddComponent<ESCompositeSpriteUVDriver>();

            try
            {
                driver.UpdateNow();
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Vector4 rect = block.GetVector(Shader.PropertyToID("_SpriteUVRect"));
                Vector4 transformX = block.GetVector(Shader.PropertyToID("_SpriteUVTransformX"));
                Vector4 transformY = block.GetVector(Shader.PropertyToID("_SpriteUVTransformY"));
                Assert.That(rect.x, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(rect.y, Is.EqualTo(0.125f).Within(0.0001f));
                Assert.That(rect.z, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(rect.w, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(transformX.x, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(transformX.y, Is.Zero.Within(0.0001f));
                Assert.That(transformX.z, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(transformY.x, Is.Zero.Within(0.0001f));
                Assert.That(transformY.y, Is.EqualTo(0.375f).Within(0.0001f));
                Assert.That(transformY.z, Is.EqualTo(0.125f).Within(0.0001f));
                Assert.That(block.GetFloat(Shader.PropertyToID("_SpriteUVTransformValid")), Is.EqualTo(1f));
                Assert.That(renderer.sharedMaterial, Is.SameAs(source));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SpriteUvDriver_ImageCreatesInstanceOnlyForCompositeMaterial()
        {
            Shader compositeShader = RequireShader("ES/UI/Composite URP");
            Shader otherShader = Shader.Find("UI/Default");
            Assert.That(otherShader, Is.Not.Null);
            var composite = new Material(compositeShader);
            var other = new Material(otherShader);
            var texture = new Texture2D(8, 8);
            Sprite sprite = Sprite.Create(texture, new Rect(1, 2, 4, 4), new Vector2(0.5f, 0.5f));
            var gameObject = new GameObject("ES UI Sprite UV Driver Test", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Image image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            ESCompositeSpriteUVDriver driver = gameObject.AddComponent<ESCompositeSpriteUVDriver>();

            try
            {
                image.material = other;
                driver.UpdateNow();
                Assert.That(gameObject.GetComponent<ESCompositeMaterialInstance>(), Is.Null);

                image.material = composite;
                driver.UpdateNow();
                ESCompositeMaterialInstance instance = gameObject.GetComponent<ESCompositeMaterialInstance>();
                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.RuntimeMaterial, Is.Not.Null);
                Assert.That(
                    instance.RuntimeMaterial.GetFloat(Shader.PropertyToID("_SpriteUVTransformValid")),
                    Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(other);
                Object.DestroyImmediate(composite);
            }
        }

        [Test]
        public void MaterialMigration_StampsBaselineVersionAndRejectsFutureVersion()
        {
            Shader shader = RequireShader("ES/2D/Composite URP");
            var material = new Material(shader);

            try
            {
                Assert.That(ES.EditorInternal.ESCompositeMaterialMigration.GetStoredVersion(material), Is.EqualTo(0));
                Assert.That(ES.EditorInternal.ESCompositeMaterialMigration.Migrate(material, false), Is.True);
                Assert.That(
                    ES.EditorInternal.ESCompositeMaterialMigration.GetStoredVersion(material),
                    Is.EqualTo(ES.EditorInternal.ESCompositeMaterialMigration.CurrentVersion));

                material.SetOverrideTag(
                    ES.EditorInternal.ESCompositeMaterialMigration.VersionTagName,
                    (ES.EditorInternal.ESCompositeMaterialMigration.CurrentVersion + 1).ToString());
                Assert.That(ES.EditorInternal.ESCompositeMaterialMigration.Migrate(material, false), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [TestCase("Sprite Shaders Ultimate/Standard SSU", "ES/2D/Composite URP", ES.EditorInternal.ESCompositeSSUBlendMode.Alpha)]
        [TestCase("Sprite Shaders Ultimate/Additive SSU", "ES/2D/Composite URP", ES.EditorInternal.ESCompositeSSUBlendMode.Additive)]
        [TestCase("Sprite Shaders Ultimate/Multiplicative SSU", "ES/2D/Composite URP", ES.EditorInternal.ESCompositeSSUBlendMode.Multiply)]
        [TestCase("Sprite Shaders Ultimate/2D Lit URP SSU", "ES/2D/Composite URP", ES.EditorInternal.ESCompositeSSUBlendMode.Alpha)]
        [TestCase("Sprite Shaders Ultimate/GUI SSU", "ES/UI/Composite URP", ES.EditorInternal.ESCompositeSSUBlendMode.Alpha)]
        [TestCase("Sprite Shaders Ultimate/Additive GUI SSU", "ES/UI/Composite URP", ES.EditorInternal.ESCompositeSSUBlendMode.Additive)]
        [TestCase("Sprite Shaders Ultimate/3D Lit URP SSU", "ES/3D/Lit Composite URP", ES.EditorInternal.ESCompositeSSUBlendMode.Alpha)]
        [TestCase("Sprite Shaders Ultimate/3D Lit Cutout URP SSU", "ES/3D/Lit Composite URP", ES.EditorInternal.ESCompositeSSUBlendMode.Alpha)]
        [TestCase("Sprite Shaders Ultimate/3D Lit BuiltIn SSU", "ES/3D/Lit Composite URP", ES.EditorInternal.ESCompositeSSUBlendMode.Alpha)]
        [TestCase("Sprite Shaders Ultimate/3D Lit Cutout BuiltIn SSU", "ES/3D/Lit Composite URP", ES.EditorInternal.ESCompositeSSUBlendMode.Alpha)]
        public void SSUMigration_ResolvesSupportedShaderFamilies(
            string sourceShader,
            string expectedTarget,
            ES.EditorInternal.ESCompositeSSUBlendMode expectedBlend)
        {
            bool resolved = ES.EditorInternal.ESCompositeSSUMaterialMigration.TryResolveSourceShader(
                sourceShader,
                out string target,
                out ES.EditorInternal.ESCompositeSSUBlendMode blend);

            Assert.That(resolved, Is.True);
            Assert.That(target, Is.EqualTo(expectedTarget));
            Assert.That(blend, Is.EqualTo(expectedBlend));
        }

        [TestCase("Sprite Shaders Ultimate/Unknown SSU")]
        [TestCase("UI/Default")]
        public void SSUMigration_DoesNotRouteUnsupportedShaderFamilies(string sourceShader)
        {
            Assert.That(
                ES.EditorInternal.ESCompositeSSUMaterialMigration.TryResolveSourceShader(
                    sourceShader,
                    out _,
                    out _),
                Is.False);
        }

        [TestCase("Sprite Shaders Ultimate/Standard SSU", "ES/2D/Composite URP")]
        [TestCase("Sprite Shaders Ultimate/GUI SSU", "ES/UI/Composite URP")]
        public void SSUMigration_MapsCompleteTimeContractForSpriteAndUi(string sourceShaderName, string targetShaderName)
        {
            Material source = CreateTestMaterial(RequireShader(sourceShaderName));
            Material migrated = null;
            try
            {
                source.SetFloat("_ToggleCustomTime", 1f);
                source.SetFloat("_ToggleUnscaledTime", 1f);
                source.SetFloat("_TimeValue", 7.5f);
                source.SetFloat("_ToggleTimeSpeed", 1f);
                source.SetFloat("_TimeSpeed", -2.25f);
                source.SetFloat("_ToggleTimeFPS", 1f);
                source.SetFloat("_TimeFPS", -500f);
                source.SetFloat("_ToggleTimeFrequency", 1f);
                source.SetFloat("_TimeFrequency", -3.5f);
                source.SetFloat("_TimeRange", -0.6f);

                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out ES.EditorInternal.ESCompositeSSUMigrationReport report);

                Assert.That(migrated, Is.Not.Null);
                Assert.That(report.TargetShaderName, Is.EqualTo(targetShaderName));
                Assert.That(migrated.GetFloat("_TimeMode"), Is.EqualTo(1f), "SSU applies Unscaled Time after Custom Time.");
                Assert.That(migrated.GetFloat("_CustomTime"), Is.EqualTo(7.5f));
                Assert.That(migrated.GetFloat("_TimeScale"), Is.EqualTo(-2.25f));
                Assert.That(migrated.GetFloat("_EnableTimeFPS"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_TimeFPS"), Is.EqualTo(240f));
                Assert.That(migrated.GetFloat("_EnableTimeFrequency"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_TimeFrequency"), Is.EqualTo(-3.5f));
                Assert.That(migrated.GetFloat("_TimeRange"), Is.EqualTo(-0.6f));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(source);
            }
        }

        [TestCase("Sprite Shaders Ultimate/Standard SSU", "ES/2D/Composite URP")]
        [TestCase("Sprite Shaders Ultimate/GUI SSU", "ES/UI/Composite URP")]
        public void SSUMigration_SpriteAndUiPreserveExactStylizedParameters(
            string sourceShaderName,
            string targetShaderName)
        {
            Material source = CreateTestMaterial(RequireShader(sourceShaderName));
            Material migrated = null;
            var tintTexture = new Texture2D(2, 2);
            try
            {
                Color hologramTint = new Color(0.25f, 1.5f, 2f, 1f);
                source.SetFloat("_EnableHologram", 1f);
                source.SetColor("_HologramTint", hologramTint);
                source.SetFloat("_HologramLineGap", 3f);
                source.SetFloat("_HologramLineSpeed", 0.75f);
                source.SetFloat("_HologramContrast", 2.5f);
                source.SetFloat("_EnableGlitch", 1f);
                source.SetFloat("_GlitchFade", 0.6f);
                source.SetFloat("_GlitchBrightness", 4.5f);
                source.SetVector("_GlitchDistortion", new Vector4(0.1f, -0.2f, 0f, 0f));
                source.SetFloat("_EnableOuterOutline", 1f);
                source.SetFloat("_OuterOutlineWidth", 0.125f);
                source.SetFloat("_OuterOutlineFade", 0.7f);
                source.SetFloat("_OuterOutlineTextureToggle", 1f);
                source.SetTexture("_OuterOutlineTintTexture", tintTexture);
                source.SetFloat("_OuterOutlineOutlineOnlyToggle", 1f);

                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out ES.EditorInternal.ESCompositeSSUMigrationReport report);

                Assert.That(migrated, Is.Not.Null);
                Assert.That(report.TargetShaderName, Is.EqualTo(targetShaderName));
                Assert.That(migrated.GetFloat("_SSUStatusContract"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_QualityTier"), Is.EqualTo(2f));
                Assert.That(migrated.GetColor("_HologramColor"), Is.EqualTo(hologramTint));
                Assert.That(migrated.GetFloat("_HologramLineGap"), Is.EqualTo(3f));
                Assert.That(migrated.GetFloat("_HologramSpeed"), Is.EqualTo(0.75f));
                Assert.That(migrated.GetFloat("_HologramContrast"), Is.EqualTo(2.5f));
                Assert.That(migrated.GetFloat("_HologramSpace"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_GlitchFade"), Is.EqualTo(0.6f));
                Assert.That(migrated.GetFloat("_GlitchBrightness"), Is.EqualTo(4.5f));
                Assert.That(migrated.GetVector("_GlitchDistortion"),
                    Is.EqualTo(new Vector4(0.1f, -0.2f, 0f, 0f)));
                Assert.That(migrated.GetFloat("_OuterOutlineWidth"), Is.EqualTo(0.125f));
                Assert.That(migrated.GetFloat("_OuterOutlineFade"), Is.EqualTo(0.7f));
                Assert.That(migrated.GetTexture("_OuterOutlineTintTexture"), Is.SameAs(tintTexture));
                Assert.That(migrated.GetFloat("_OuterOutlineOutlineOnlyToggle"), Is.EqualTo(1f));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(tintTexture);
                Object.DestroyImmediate(source);
            }
        }

        [TestCase("Sprite Shaders Ultimate/Standard SSU", "ES/2D/Composite URP")]
        [TestCase("Sprite Shaders Ultimate/GUI SSU", "ES/UI/Composite URP")]
        [TestCase("Sprite Shaders Ultimate/3D Lit URP SSU", "ES/3D/Lit Composite URP")]
        public void SSUMigration_ShineUsesExplicitLocalSpaceWithoutDirectionOverride(
            string sourceShaderName,
            string targetShaderName)
        {
            Material source = CreateTestMaterial(RequireShader(sourceShaderName));
            Material migrated = null;
            try
            {
                source.SetFloat("_EnableShine", 1f);
                source.SetFloat("_ShineRotation", 47f);

                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out ES.EditorInternal.ESCompositeSSUMigrationReport report);

                Assert.That(migrated, Is.Not.Null);
                Assert.That(report.TargetShaderName, Is.EqualTo(targetShaderName));
                Assert.That(migrated.GetFloat("_ShineSpace"), Is.EqualTo((float)ESCompositeProjectionSpace.局部UV));
                Assert.That(migrated.GetVector("_ShineDirection"), Is.EqualTo(Vector4.zero));
                Assert.That(migrated.GetFloat("_ShineRotation"), Is.EqualTo(47f));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SSUMigration_LitMapsPbrTimeAndAliasedEffects()
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/3D Lit URP SSU"));
            Material migrated = null;
            var baseTexture = new Texture2D(8, 4);
            try
            {
                var emission = new Color(3f, 0.4f, 0.2f, 1f);
                var strongTint = new Color(0.2f, 0.7f, 1.5f, 1f);
                var replaceTo = new Color(1.8f, 0.1f, 0.3f, 1f);
                source.SetTexture("_MainTex", baseTexture);
                source.SetTextureScale("_MainTex", new Vector2(2f, 3f));
                source.SetTextureOffset("_MainTex", new Vector2(0.25f, 0.5f));
                source.SetFloat("_NormalIntensity", 1.4f);
                source.SetFloat("_MetallicMapToggle", 1f);
                source.SetTextureScale("_MetallicMap", new Vector2(4f, 5f));
                source.SetTextureOffset("_MetallicMap", new Vector2(0.1f, 0.2f));
                source.SetFloat("_EmissionToggle", 1f);
                source.SetColor("_EmissionTint", emission);
                source.SetFloat("_ToggleCustomTime", 1f);
                source.SetFloat("_TimeValue", 12.5f);
                source.SetFloat("_ToggleTimeSpeed", 1f);
                source.SetFloat("_TimeSpeed", 2.25f);
                source.SetFloat("_EnableStrongTint", 1f);
                source.SetColor("_StrongTintTint", strongTint);
                source.SetFloat("_EnableColorReplace", 1f);
                source.SetColor("_ColorReplaceToColor", replaceTo);
                source.SetFloat("_ColorReplaceRange", 0.35f);
                source.SetFloat("_EnableDirectionalAlphaFade", 1f);
                source.SetFloat("_DirectionalAlphaFadeFade", 0.25f);
                source.SetFloat("_DirectionalAlphaFadeWidth", 0.12f);
                source.SetFloat("_EnableUVScale", 1f);
                source.SetVector("_UVScaleScale", new Vector4(2f, 0.75f, 0f, 0f));

                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out ES.EditorInternal.ESCompositeSSUMigrationReport report);

                Assert.That(migrated, Is.Not.Null);
                Assert.That(report.TargetShaderName, Is.EqualTo("ES/3D/Lit Composite URP"));
                Assert.That(migrated.GetTexture("_BaseMap"), Is.SameAs(baseTexture));
                Assert.That(migrated.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(2f, 3f)));
                Assert.That(migrated.GetTextureOffset("_BaseMap"), Is.EqualTo(new Vector2(0.25f, 0.5f)));
                Assert.That(migrated.GetFloat("_NormalScale"), Is.EqualTo(1.4f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_UseMetallicMap"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_SmoothnessMapChannel"), Is.Zero);
                Assert.That(migrated.GetTextureScale("_MetallicMap"), Is.EqualTo(Vector2.one));
                Assert.That(migrated.GetTextureOffset("_MetallicMap"), Is.EqualTo(Vector2.zero));
                Assert.That(migrated.GetFloat("_UseEmission"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_EmissionUseAlpha"), Is.EqualTo(1f));
                Assert.That(migrated.GetColor("_EmissionColor"), Is.EqualTo(emission));
                Assert.That(migrated.GetFloat("_TimeMode"), Is.EqualTo(2f));
                Assert.That(migrated.GetFloat("_CustomTime"), Is.EqualTo(12.5f));
                Assert.That(migrated.GetFloat("_TimeScale"), Is.EqualTo(2.25f));
                Assert.That(migrated.GetFloat("_QualityTier"), Is.EqualTo(2f));
                Assert.That(migrated.GetColor("_StrongTint"), Is.EqualTo(strongTint));
                Assert.That(migrated.GetColor("_ReplaceTo"), Is.EqualTo(replaceTo));
                Assert.That(migrated.GetFloat("_ReplaceRange"), Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_FadeMode"), Is.Zero);
                Assert.That(migrated.GetFloat("_EnableDirectionalAlphaFade"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_DirectionalAlphaFadeFade"), Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_DirectionalAlphaFadeWidth"), Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_EnableUVTransform"), Is.EqualTo(1f));
                Assert.That(migrated.GetVector("_UVScale"), Is.EqualTo(new Vector4(2f, 0.75f, 0f, 0f)));
                Assert.That(migrated.GetFloat("_Surface"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_AlphaClip"), Is.Zero);
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(baseTexture);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SSUMigration_PreservesComposableFadeStackWithoutLegacyCollapse()
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/Standard SSU"));
            Material migrated = null;
            try
            {
                string[] enables =
                {
                    "_EnableFullAlphaDissolve",
                    "_EnableSourceAlphaDissolve",
                    "_EnableSourceGlowDissolve",
                    "_EnableDirectionalAlphaFade",
                    "_EnableDirectionalGlowFade",
                    "_EnableDirectionalDistortion"
                };
                for (int i = 0; i < enables.Length; i++)
                    source.SetFloat(enables[i], 1f);

                source.SetFloat("_FullAlphaDissolveFade", 0.31f);
                source.SetVector("_SourceAlphaDissolvePosition", new Vector4(0.2f, 0.7f, 0f, 0f));
                source.SetColor("_SourceGlowDissolveEdgeColor", new Color(4f, 2f, 1f, 0f));
                source.SetFloat("_DirectionalAlphaFadeRotation", 123f);
                source.SetFloat("_DirectionalGlowFadeNoiseFactor", 0.37f);
                source.SetVector("_DirectionalDistortionDistortion", new Vector4(0.08f, -0.12f, 0f, 0f));

                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out ES.EditorInternal.ESCompositeSSUMigrationReport report);

                Assert.That(migrated, Is.Not.Null);
                Assert.That(report.Issues, Has.None.Matches<ES.EditorInternal.ESCompositeSSUMigrationIssue>(
                    issue => issue.Severity == ES.EditorInternal.ESCompositeSSUMigrationSeverity.Error));
                Assert.That(report.Issues, Has.Some.Matches<ES.EditorInternal.ESCompositeSSUMigrationIssue>(
                    issue => issue.Message.Contains("6 个可叠加 SSU Fade")));
                Assert.That(migrated.GetFloat("_FadeMode"), Is.Zero);
                for (int i = 0; i < enables.Length; i++)
                    Assert.That(migrated.GetFloat(enables[i]), Is.EqualTo(1f), enables[i]);
                Assert.That(migrated.GetFloat("_FullAlphaDissolveFade"), Is.EqualTo(0.31f).Within(0.0001f));
                Assert.That(migrated.GetVector("_SourceAlphaDissolvePosition"), Is.EqualTo(new Vector4(0.2f, 0.7f, 0f, 0f)));
                Assert.That(migrated.GetColor("_SourceGlowDissolveEdgeColor"), Is.EqualTo(new Color(4f, 2f, 1f, 0f)));
                Assert.That(migrated.GetFloat("_DirectionalAlphaFadeRotation"), Is.EqualTo(123f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_DirectionalGlowFadeNoiseFactor"), Is.EqualTo(0.37f).Within(0.0001f));
                Assert.That(migrated.GetVector("_DirectionalDistortionDistortion"), Is.EqualTo(new Vector4(0.08f, -0.12f, 0f, 0f)));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(source);
            }
        }

        [TestCase("ES2DCompositeURP.shader")]
        [TestCase("ESUICompositeURP.shader")]
        [TestCase("ES3DLitCompositeURP.shader")]
        public void CompositeShaders_ExposeComposableSSUFadeStack(string shaderName)
        {
            string source = File.ReadAllText(ShaderRoot + shaderName);
            string executionSource = shaderName == "ES3DLitCompositeURP.shader"
                ? File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl")
                : source;
            string[] properties =
            {
                "_EnableFullAlphaDissolve",
                "_EnableSourceAlphaDissolve",
                "_EnableSourceGlowDissolve",
                "_EnableDirectionalAlphaFade",
                "_EnableDirectionalGlowFade",
                "_EnableDirectionalDistortion",
                "_DirectionalDistortionRandomDirection",
                "_DirectionalDistortionDistortionScale"
            };

            for (int i = 0; i < properties.Length; i++)
                Assert.That(source, Does.Contain(properties[i]), properties[i]);
            Assert.That(executionSource, Does.Contain("#include \"ESCompositeSSUFadeStack.hlsl\""));
        }

        [Test]
        public void SSUFadeStack_PreservesSourceOrderAndDirectionalDistortionPhases()
        {
            string source = File.ReadAllText(ShaderRoot + "ESCompositeSSUFadeStack.hlsl");
            int fullAlpha = source.IndexOf("if (_EnableFullAlphaDissolve", System.StringComparison.Ordinal);
            int sourceAlpha = source.IndexOf("if (_EnableSourceAlphaDissolve", System.StringComparison.Ordinal);
            int sourceGlow = source.IndexOf("if (_EnableSourceGlowDissolve", System.StringComparison.Ordinal);
            int directionalAlpha = source.IndexOf("if (_EnableDirectionalAlphaFade", System.StringComparison.Ordinal);
            int directionalGlow = source.IndexOf("if (_EnableDirectionalGlowFade", System.StringComparison.Ordinal);

            Assert.That(fullAlpha, Is.GreaterThan(0));
            Assert.That(sourceAlpha, Is.GreaterThan(fullAlpha));
            Assert.That(sourceGlow, Is.GreaterThan(sourceAlpha));
            Assert.That(directionalAlpha, Is.GreaterThan(sourceGlow));
            Assert.That(directionalGlow, Is.GreaterThan(directionalAlpha));
            Assert.That(source, Does.Contain("ESCompositeApplySSUDirectionalDistortionUV"));
            Assert.That(source, Does.Contain("visibility *= ESCompositeSSUResolveInvert"));

            string litCommon = File.ReadAllText(ShaderRoot + "ES3DLitCompositeURPCommon.hlsl");
            Assert.That(litCommon, Does.Contain("uv = ESCompositeApplySSUDirectionalDistortionUV(uv, frac(uv));"));
            Assert.That(litCommon, Does.Contain("visibility *= ssuFadeVisibility;"));
            Assert.That(litCommon, Does.Contain("baseSample.rgb = ESCompositeApplySSUFadeStackColor("));
        }

        [TestCase("ES/2D/Composite URP", "_ES_SPRITE_RESOURCE_MASK_2")]
        [TestCase("ES/UI/Composite URP", "_ES_SPRITE_RESOURCE_MASK_2")]
        [TestCase("ES/3D/Lit Composite URP", "_ES_LIT_RESOURCE_MASK_2")]
        public void ComposableSSUFadeStack_RequestsFadeResourceBit(
            string shaderName,
            string expectedKeyword)
        {
            Material material = CreateTestMaterial(RequireShader(shaderName));
            try
            {
                material.SetFloat("_ResourceProfile", 1f);
                material.SetFloat("_EnableDirectionalGlowFade", 1f);
                if (shaderName == "ES/2D/Composite URP")
                    ES2DCompositeURPProperties.RefreshResourceProfile(material);
                else if (shaderName == "ES/UI/Composite URP")
                    ESUICompositeURPProperties.RefreshResourceProfile(material);
                else
                    ES3DLitCompositeURPProperties.RefreshResourceProfile(material);

                Assert.That(material.IsKeywordEnabled(expectedKeyword), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SSUMigration_LitMapsCompleteHalftoneAlphaContract()
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/3D Lit URP SSU"));
            Material migrated = null;
            var texture = new Texture2D(200, 100);
            try
            {
                source.SetTexture("_MainTex", texture);
                source.SetFloat("_EnableSharpen", 1f);
                source.SetFloat("_SharpenFactor", 8f);
                source.SetFloat("_SharpenOffset", 2f);
                source.SetFloat("_SharpenFade", 0.75f);
                source.SetFloat("_EnablePixelate", 1f);
                source.SetFloat("_PixelatePixelDensity", 16f);
                source.SetFloat("_PixelatePixelsPerUnit", 100f);
                source.SetFloat("_PixelateFade", 0.5f);
                source.SetFloat("_EnableHalftone", 1f);
                source.SetFloat("_HalftoneTiling", 3f);
                source.SetFloat("_HalftoneFade", 0.25f);
                source.SetVector("_HalftonePosition", new Vector4(0.2f, 0.7f, 0f, 0f));
                source.SetFloat("_HalftoneFadeWidth", -0.5f);
                source.SetFloat("_HalftoneInvert", 1f);

                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out ES.EditorInternal.ESCompositeSSUMigrationReport report);

                Assert.That(migrated.GetFloat("_SharpenAmount"), Is.EqualTo(4f));
                Assert.That(migrated.GetFloat("_SharpenRadius"), Is.EqualTo(2f / 512f).Within(0.000001f));
                Assert.That(migrated.GetFloat("_SharpenFade"), Is.EqualTo(0.75f));
                Assert.That(migrated.GetFloat("_PixelateCells"), Is.EqualTo(64f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_PixelateStrength"), Is.EqualTo(0.5f));
                Assert.That(migrated.GetFloat("_HalftoneScale"), Is.EqualTo(4f));
                Assert.That(migrated.GetFloat("_HalftoneStrength"), Is.Zero);
                Assert.That(migrated.GetVector("_HalftonePosition"), Is.EqualTo(new Vector4(0.2f, 0.7f, 0f, 0f)));
                Assert.That(migrated.GetFloat("_HalftoneFade"), Is.EqualTo(0.25f));
                Assert.That(migrated.GetFloat("_HalftoneFadeWidth"), Is.EqualTo(0.5f));
                Assert.That(migrated.GetFloat("_HalftoneInvert"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_HalftoneAlphaPattern"), Is.EqualTo(1f));
                Assert.That(report.Issues, Has.Some.Matches<ES.EditorInternal.ESCompositeSSUMigrationIssue>(
                    issue => issue.Message.Contains("透明点阵") && issue.Severity == ES.EditorInternal.ESCompositeSSUMigrationSeverity.Info));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SSUMigration_LitMapsRemainingEffectsAndReportsAdvancedLosses()
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/3D Lit URP SSU"));
            Material migrated = null;
            var layerTexture = new Texture2D(4, 4);
            try
            {
                var hologramTint = new Color(0.2f, 1.5f, 2.5f, 1f);
                source.SetFloat("_EnableTextureLayer1", 1f);
                source.SetTexture("_TextureLayer1Texture", layerTexture);
                source.SetFloat("_TextureLayer1Fade", 0.65f);
                source.SetFloat("_EnableHologram", 1f);
                source.SetColor("_HologramTint", hologramTint);
                source.SetFloat("_HologramFade", 0.4f);
                source.SetFloat("_HologramContrast", 2.5f);
                source.SetFloat("_HologramLineSpeed", 0.75f);
                source.SetFloat("_HologramLineGap", 3f);
                source.SetFloat("_HologramDistortionOffset", 0.2f);
                source.SetFloat("_HologramDistortionDensity", 0.7f);
                source.SetFloat("_EnableGlitch", 1f);
                source.SetFloat("_GlitchFade", 0.5f);
                source.SetFloat("_GlitchMaskMin", 0.3f);
                source.SetVector("_GlitchDistortion", new Vector4(0.1f, 0.05f, 0f, 0f));
                source.SetVector("_GlitchMaskSpeed", new Vector4(0f, 4f, 0f, 0f));
                source.SetVector("_GlitchNoiseSpeed", new Vector4(0f, 2f, 0f, 0f));
                source.SetVector("_GlitchDistortionSpeed", new Vector4(0f, 3f, 0f, 0f));
                source.SetFloat("_EnableFullDistortion", 1f);
                source.SetFloat("_FullDistortionFade", 0.25f);
                source.SetVector("_FullDistortionDistortion", new Vector4(0.2f, 0.1f, 0f, 0f));
                source.SetVector("_FullDistortionNoiseScale", new Vector4(3f, 2f, 0f, 0f));
                source.SetFloat("_EnableOuterOutline", 1f);
                source.SetFloat("_OuterOutlineFade", 0.6f);
                source.SetFloat("_OuterOutlineTextureToggle", 1f);
                source.SetTexture("_OuterOutlineTintTexture", layerTexture);
                source.SetFloat("_OuterOutlineOutlineOnlyToggle", 1f);

                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out ES.EditorInternal.ESCompositeSSUMigrationReport report);

                Assert.That(migrated, Is.Not.Null);
                Assert.That(migrated.GetFloat("_EnableTextureLayer1"), Is.EqualTo(1f));
                Assert.That(migrated.GetTexture("_TextureLayer1Texture"), Is.SameAs(layerTexture));
                Assert.That(migrated.GetFloat("_TextureLayer1Fade"), Is.EqualTo(0.65f).Within(0.0001f));
                Assert.That(migrated.GetColor("_HologramColor"), Is.EqualTo(hologramTint));
                Assert.That(migrated.GetFloat("_HologramFade"), Is.EqualTo(0.4f));
                Assert.That(migrated.GetFloat("_HologramContrast"), Is.EqualTo(2.5f));
                Assert.That(migrated.GetFloat("_HologramSpace"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_HologramSpeed"), Is.EqualTo(0.75f));
                Assert.That(migrated.GetFloat("_HologramLineGap"), Is.EqualTo(3f));
                Assert.That(migrated.GetFloat("_HologramDistortionOffset"), Is.EqualTo(0.2f));
                Assert.That(migrated.GetFloat("_HologramDistortionDensity"), Is.EqualTo(0.7f));
                Assert.That(migrated.GetFloat("_GlitchFade"), Is.EqualTo(0.5f));
                Assert.That(migrated.GetFloat("_GlitchMaskMin"), Is.EqualTo(0.3f));
                Assert.That(migrated.GetVector("_GlitchDistortion"), Is.EqualTo(new Vector4(0.1f, 0.05f, 0f, 0f)));
                Assert.That(migrated.GetFloat("_GlitchIntensity"), Is.EqualTo(new Vector2(0.1f, 0.05f).magnitude).Within(0.0001f));
                Assert.That(migrated.GetFloat("_GlitchSpeed"), Is.EqualTo(4f));
                Assert.That(migrated.GetFloat("_OuterOutlineFade"), Is.EqualTo(0.6f));
                Assert.That(migrated.GetTexture("_OuterOutlineTintTexture"), Is.SameAs(layerTexture));
                Assert.That(migrated.GetFloat("_OuterOutlineOutlineOnlyToggle"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_EnableFullDistortion"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_FullDistortionFade"), Is.EqualTo(0.25f));
                Assert.That(migrated.GetVector("_FullDistortionDistortion"), Is.EqualTo(new Vector4(0.2f, 0.1f, 0f, 0f)));
                Assert.That(migrated.GetVector("_FullDistortionNoiseScale"), Is.EqualTo(new Vector4(3f, 2f, 0f, 0f)));
                Assert.That(migrated.GetFloat("_EnableUVDistort"), Is.Zero);
                Assert.That(migrated.GetFloat("_SSUStatusContract"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_QualityTier"), Is.EqualTo(2f));
                Assert.That(report.Issues, Has.Some.Matches<ES.EditorInternal.ESCompositeSSUMigrationIssue>(
                    issue => issue.Message.Contains("Hologram") && issue.Message.Contains("透视相机")));
                Assert.That(report.Issues, Has.Some.Matches<ES.EditorInternal.ESCompositeSSUMigrationIssue>(
                    issue => issue.Message.Contains("Glitch") && issue.Message.Contains("精确合同")));
                Assert.That(report.Issues, Has.Some.Matches<ES.EditorInternal.ESCompositeSSUMigrationIssue>(
                    issue => issue.Message.Contains("Full Distortion") && issue.Message.Contains("两次独立噪声")));
                Assert.That(report.Issues, Has.Some.Matches<ES.EditorInternal.ESCompositeSSUMigrationIssue>(
                    issue => issue.Message.Contains("Outline")
                        && issue.Message.Contains("网格覆盖范围")));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(layerTexture);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SSUMigration_LitCutoutMapsSurfaceAndCutoff()
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/3D Lit Cutout URP SSU"));
            Material migrated = null;
            try
            {
                source.SetFloat("_AlphaClip", 0.37f);
                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out _);

                Assert.That(migrated, Is.Not.Null);
                Assert.That(migrated.GetFloat("_Surface"), Is.Zero);
                Assert.That(migrated.GetFloat("_AlphaClip"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_Cutoff"), Is.EqualTo(0.37f).Within(0.0001f));
                Assert.That(migrated.GetTag("RenderType", false), Is.EqualTo("TransparentCutout"));
                Assert.That(migrated.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.AlphaTest));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SSUMigration_BuiltInLitRequiresExplicitLossyOptIn()
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/3D Lit BuiltIn SSU"));
            try
            {
                ES.EditorInternal.ESCompositeSSUMigrationReport report =
                    ES.EditorInternal.ESCompositeSSUMaterialMigration.Analyze(source);

                Assert.That(report.TargetShaderName, Is.EqualTo("ES/3D/Lit Composite URP"));
                Assert.That(report.HasErrors, Is.False);
                Assert.That(report.HasWarnings, Is.True);
                Assert.That(report.CanMigrate(false), Is.False);
                Assert.That(report.CanMigrate(true), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SSUMigration_FullDistortionDoesNotRequireLossyOptIn()
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/Standard SSU"));
            try
            {
                source.SetFloat("_EnableFullDistortion", 1f);
                ES.EditorInternal.ESCompositeSSUMigrationReport report =
                    ES.EditorInternal.ESCompositeSSUMaterialMigration.Analyze(source);

                Assert.That(report.HasErrors, Is.False);
                Assert.That(report.Issues, Has.None.Matches<ES.EditorInternal.ESCompositeSSUMigrationIssue>(
                    issue => issue.Message.Contains("Full Distortion")
                        && issue.Severity == ES.EditorInternal.ESCompositeSSUMigrationSeverity.Warning));
                Assert.That(report.CanMigrate(false), Is.True);
                Assert.That(report.CanMigrate(true), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SSUMigration_BurnPreservesCompleteStatusContractWithoutLossyOptIn()
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/Standard SSU"));
            Material migrated = null;
            try
            {
                source.SetFloat("_EnableBurn", 1f);
                source.SetFloat("_BurnFade", 0.63f);
                source.SetFloat("_BurnRadius", 2.75f);
                source.SetVector("_BurnPosition", new Vector4(0.25f, 0.75f, 0f, 0f));
                ES.EditorInternal.ESCompositeSSUMigrationReport report =
                    ES.EditorInternal.ESCompositeSSUMaterialMigration.Analyze(source);

                Assert.That(report.HasErrors, Is.False);
                Assert.That(report.HasWarnings, Is.False);
                Assert.That(report.PartiallyCompatibleEffectCount, Is.Zero);
                Assert.That(report.CanMigrate(false), Is.True);

                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    false,
                    out _);
                Assert.That(migrated, Is.Not.Null);
                Assert.That(migrated.GetFloat("_SSUStatusContract"), Is.EqualTo(1f));
                Assert.That(migrated.GetFloat("_BurnFade"), Is.EqualTo(0.63f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_BurnRadius"), Is.EqualTo(2.75f).Within(0.0001f));
                Assert.That(migrated.GetVector("_BurnPosition"), Is.EqualTo(new Vector4(0.25f, 0.75f, 0f, 0f)));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(source);
            }
        }

        [TestCase("_EnableFrozen")]
        [TestCase("_EnableRainbow")]
        [TestCase("_EnableShine")]
        [TestCase("_EnablePoison")]
        public void SSUMigration_OtherStatusEffectsHaveCompleteParameterSchemas(string enabledProperty)
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/Standard SSU"));
            try
            {
                source.SetFloat(enabledProperty, 1f);
                ES.EditorInternal.ESCompositeSSUMigrationReport report =
                    ES.EditorInternal.ESCompositeSSUMaterialMigration.Analyze(source);

                Assert.That(report.HasErrors, Is.False);
                Assert.That(report.HasWarnings, Is.False);
                Assert.That(report.PartiallyCompatibleEffectCount, Is.Zero);
                Assert.That(report.CanMigrate(false), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [TestCase("Sprite Shaders Ultimate/Standard SSU", ES.EditorInternal.ESCompositeSSUTargetMode.TwoD)]
        [TestCase("Sprite Shaders Ultimate/GUI SSU", ES.EditorInternal.ESCompositeSSUTargetMode.UI)]
        [TestCase("Sprite Shaders Ultimate/3D Lit URP SSU", ES.EditorInternal.ESCompositeSSUTargetMode.Lit)]
        public void SSUMigration_RestoresRenamedColorAndFilterControls(
            string sourceShaderName,
            ES.EditorInternal.ESCompositeSSUTargetMode targetMode)
        {
            Material source = CreateTestMaterial(RequireShader(sourceShaderName));
            Material migrated = null;
            try
            {
                source.SetFloat("_EnableAlphaTint", 1f);
                source.SetColor("_AlphaTintColor", Color.cyan);
                source.SetFloat("_AlphaTintMinAlpha", 0.21f);
                source.SetFloat("_AlphaTintFade", 0.73f);
                source.SetFloat("_EnableColorReplace", 1f);
                source.SetColor("_ColorReplaceFromColor", Color.red);
                source.SetColor("_ColorReplaceToColor", Color.blue);
                source.SetFloat("_ColorReplaceContrast", 2.4f);
                source.SetFloat("_ColorReplaceFade", 0.62f);
                source.SetFloat("_EnableSplitToning", 1f);
                source.SetFloat("_SplitToningContrast", 3.1f);
                source.SetFloat("_SplitToningShift", -0.35f);
                source.SetFloat("_EnablePingPongGlow", 1f);
                source.SetFloat("_PingPongGlowContrast", 1.8f);
                source.SetFloat("_PingPongGlowFade", 0.54f);
                source.SetFloat("_EnableSharpen", 1f);
                source.SetFloat("_SharpenFactor", 2.2f);
                source.SetFloat("_SharpenOffset", 3f);

                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    targetMode,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out ES.EditorInternal.ESCompositeSSUMigrationReport report);

                Assert.That(migrated, Is.Not.Null);
                Assert.That(migrated.GetColor("_AlphaTint"), Is.EqualTo(Color.cyan));
                Assert.That(migrated.GetFloat("_AlphaTintMin"), Is.EqualTo(0.21f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_AlphaTintFade"), Is.EqualTo(0.73f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_ReplaceContrast"), Is.EqualTo(2.4f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_ReplaceFade"), Is.EqualTo(0.62f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_SplitToneContrast"), Is.EqualTo(3.1f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_SplitToneShift"), Is.EqualTo(-0.35f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_GlowContrast"), Is.EqualTo(1.8f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_GlowFade"), Is.EqualTo(0.54f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_SharpenAmount"), Is.EqualTo(2.2f).Within(0.0001f));
                Assert.That(migrated.GetFloat("_SharpenRadius"), Is.EqualTo(3f / 512f).Within(0.0001f));
                Assert.That(report.Issues, Has.Some.Matches<ES.EditorInternal.ESCompositeSSUMigrationIssue>(
                    issue => issue.Severity == ES.EditorInternal.ESCompositeSSUMigrationSeverity.Info
                        && issue.Message.Contains("执行顺序遵循 ES Composite 管线")));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SSUMigration_ClampsEnabledDirectRangeProperties()
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/Standard SSU"));
            Material migrated = null;
            try
            {
                source.SetFloat("_EnableNegative", 1f);
                source.SetFloat("_NegativeFade", 4f);
                ES.EditorInternal.ESCompositeSSUMigrationReport preview =
                    ES.EditorInternal.ESCompositeSSUMaterialMigration.Analyze(source);

                Assert.That(preview.ClampedPropertyCount, Is.GreaterThan(0));
                Assert.That(preview.CanMigrate(false), Is.False);
                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out _);
                Assert.That(migrated.GetFloat("_NegativeFade"), Is.EqualTo(1f));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SSUMigration_ConvertsWorldTilingForNonSquareTextures()
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/Standard SSU"));
            Material migrated = null;
            var texture = new Texture2D(200, 100);
            try
            {
                source.SetTexture("_MainTex", texture);
                source.SetFloat("_EnableWorldTiling", 1f);
                source.SetFloat("_WorldTilingPixelsPerUnit", 100f);
                source.SetVector("_WorldTilingScale", new Vector4(1f, 1f, 0f, 0f));
                source.SetVector("_WorldTilingOffset", new Vector4(0.5f, 0.25f, 0f, 0f));
                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out _);

                Assert.That(migrated.GetFloat("_WorldTilingPixelsPerUnit"), Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(migrated.GetVector("_WorldTilingScale").y, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(migrated.GetVector("_WorldTilingOffset").x, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(migrated.GetVector("_WorldTilingOffset").y, Is.EqualTo(0.25f).Within(0.0001f));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SSUMigration_RejectsCompositeAndOrdinaryMaterials()
        {
            Material composite = CreateTestMaterial(RequireShader("ES/2D/Composite URP"));
            Material ordinary = CreateTestMaterial(RequireShader("UI/Default"));
            try
            {
                ES.EditorInternal.ESCompositeSSUMigrationReport compositeReport =
                    ES.EditorInternal.ESCompositeSSUMaterialMigration.Analyze(composite);
                ES.EditorInternal.ESCompositeSSUMigrationReport ordinaryReport =
                    ES.EditorInternal.ESCompositeSSUMaterialMigration.Analyze(
                        ordinary,
                        ES.EditorInternal.ESCompositeSSUTargetMode.TwoD,
                        ES.EditorInternal.ESCompositeSSUBlendMode.Alpha);

                Assert.That(compositeReport.HasErrors, Is.True);
                Assert.That(ordinaryReport.HasErrors, Is.True);
                Assert.That(ordinaryReport.CanMigrate(true), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(ordinary);
                Object.DestroyImmediate(composite);
            }
        }

        [Test]
        public void SSUMigration_CreatesIndependentMaterialWithoutMutatingSource()
        {
            Material source = CreateTestMaterial(RequireShader("Sprite Shaders Ultimate/Standard SSU"));
            Material migrated = null;
            try
            {
                source.name = "SSU Migration Source";
                source.SetFloat("_EnableFullDistortion", 1f);
                source.SetFloat("_FullDistortionFade", 0.25f);
                Shader sourceShader = source.shader;
                int sourceQueue = source.renderQueue;

                migrated = ES.EditorInternal.ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                    source,
                    ES.EditorInternal.ESCompositeSSUTargetMode.Auto,
                    ES.EditorInternal.ESCompositeSSUBlendMode.Auto,
                    true,
                    out ES.EditorInternal.ESCompositeSSUMigrationReport report);

                Assert.That(migrated, Is.Not.Null);
                Assert.That(report.CanMigrate(true), Is.True);
                Assert.That(migrated, Is.Not.SameAs(source));
                Assert.That(migrated.shader.name, Is.EqualTo("ES/2D/Composite URP"));
                Assert.That(source.shader, Is.SameAs(sourceShader));
                Assert.That(source.renderQueue, Is.EqualTo(sourceQueue));
                Assert.That(source.GetFloat("_EnableFullDistortion"), Is.EqualTo(1f));
                Assert.That(source.GetFloat("_FullDistortionFade"), Is.EqualTo(0.25f));
            }
            finally
            {
                if (migrated != null) Object.DestroyImmediate(migrated);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SSUMigration_SourceKeepsSnapshotAndOutputSafetyContracts()
        {
            const string editorRoot = "Assets/Plugins/ES/Editor/ESShader/";
            string migrationSource = File.ReadAllText(editorRoot + "ESCompositeSSUMaterialMigration.cs");
            string windowSource = File.ReadAllText(editorRoot + "ESCompositeSSUMigrationWindow.cs");

            Assert.That(migrationSource, Does.Contain("m_SavedProperties.m_Floats"));
            Assert.That(migrationSource, Does.Contain("m_SavedProperties.m_Ints"));
            Assert.That(migrationSource, Does.Contain("m_SavedProperties.m_Colors"));
            Assert.That(migrationSource, Does.Contain("m_SavedProperties.m_TexEnvs"));
            Assert.That(migrationSource, Does.Not.Contain("source.SetFloat"));
            Assert.That(migrationSource, Does.Not.Contain("source.shader ="));
            Assert.That(windowSource, Does.Contain("AssetDatabase.GenerateUniqueAssetPath"));
            Assert.That(windowSource, Does.Contain("TryNormalizeAssetFolder"));
            Assert.That(windowSource, Does.Contain("AssetDatabase.FindAssets(\"t:Material\""));
            Assert.That(windowSource, Does.Contain("AssetDatabase.SaveAssetIfDirty"));
            Assert.That(windowSource, Does.Not.Contain("AssetDatabase.SaveAssets()"));
            Assert.That(migrationSource, Does.Contain("ESCompositeInteractiveWind2D"));
            Assert.That(migrationSource, Does.Contain("ESCompositeWindParallax"));
        }

        [Test]
        public void BakeOutputSize_StacksFramesInRequestedDirection()
        {
            Assert.That(
                ES.EditorInternal.ESCompositeShaderBakeWindow.CalculateOutputSize(64, 32, 4, true),
                Is.EqualTo(new Vector2Int(256, 32)));
            Assert.That(
                ES.EditorInternal.ESCompositeShaderBakeWindow.CalculateOutputSize(64, 32, 4, false),
                Is.EqualTo(new Vector2Int(64, 128)));
        }

        private static Shader RequireShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, "Shader.Find failed for " + shaderName + ".");
            return shader;
        }

        private static void AssertPasses(Shader shader, params string[] passNames)
        {
            string source = ReadShaderSource(shader);

            for (int i = 0; i < passNames.Length; i++)
                Assert.That(source, Does.Match(
                    "(?m)^\\s*Name\\s+\\\"" + RegexEscape(passNames[i]) + "\\\"\\s*$"),
                    shader.name + " is missing pass " + passNames[i] + ".");
        }

        private static void AssertProperties(Shader shader, params string[] propertyNames)
        {
            Material material = CreateTestMaterial(shader);

            try
            {
                for (int i = 0; i < propertyNames.Length; i++)
                    Assert.That(material.HasProperty(propertyNames[i]), Is.True,
                        shader.name + " is missing property " + propertyNames[i] + ".");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        private static void AssertSourcePassLightMode(Shader shader, string passName, string lightMode)
        {
            Assert.That(ReadShaderSource(shader), Does.Match(
                "(?s)Name\\s+\\\"" + RegexEscape(passName)
                + "\\\"(?:(?!HLSLPROGRAM).)*?Tags\\s*\\{[^}]*\\\"LightMode\\\"?\\s*=\\s*\\\""
                + RegexEscape(lightMode) + "\\\""),
                shader.name + " does not route " + passName + " through " + lightMode + ".");
        }

        private static void AssertSourceTokenWithinEffect(
            string source,
            string effectProperty,
            string token,
            string nextEffectProperty)
        {
            int effectStart = source.IndexOf("if (" + effectProperty + " > 0.5)", System.StringComparison.Ordinal);
            int nextEffect = source.IndexOf("if (" + nextEffectProperty + " > 0.5)", effectStart + 1, System.StringComparison.Ordinal);
            int tokenIndex = source.IndexOf(token, effectStart, System.StringComparison.Ordinal);
            Assert.That(effectStart, Is.GreaterThanOrEqualTo(0), effectProperty + " branch is missing.");
            Assert.That(nextEffect, Is.GreaterThan(effectStart), nextEffectProperty + " must follow " + effectProperty + ".");
            Assert.That(tokenIndex, Is.InRange(effectStart, nextEffect - 1), token + " escaped " + effectProperty + " branch.");
        }

        private static Material CreateTestMaterial(Shader shader)
        {
            return new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static void AssertResourceMask(Material material, int expectedMask)
        {
            int enabledCount = 0;
            for (int i = 0; i < 16; i++)
            {
                bool enabled = material.IsKeywordEnabled("_ES_LIT_RESOURCE_MASK_" + i);
                if (enabled) enabledCount++;
                Assert.That(enabled, Is.EqualTo(i == expectedMask), "Unexpected Lit resource mask keyword " + i + ".");
            }
            Assert.That(enabledCount, Is.EqualTo(expectedMask < 0 ? 0 : 1));
        }

        private static void SetSpriteResourceProfile(
            Material material,
            string shaderName,
            ESSpriteCompositeResourceProfile profile)
        {
            if (shaderName == ES2DCompositeURPProperties.ShaderName)
                ES2DCompositeURPProperties.SetResourceProfile(material, profile);
            else
                ESUICompositeURPProperties.SetResourceProfile(material, profile);
        }

        private static void RefreshSpriteResourceProfile(Material material, string shaderName)
        {
            if (shaderName == ES2DCompositeURPProperties.ShaderName)
                ES2DCompositeURPProperties.RefreshResourceProfile(material);
            else
                ESUICompositeURPProperties.RefreshResourceProfile(material);
        }

        private static void AssertSpriteResourceMask(Material material, int expectedMask)
        {
            int enabledCount = 0;
            for (int i = 0; i < 16; i++)
            {
                bool enabled = material.IsKeywordEnabled("_ES_SPRITE_RESOURCE_MASK_" + i);
                if (enabled) enabledCount++;
                Assert.That(enabled, Is.EqualTo(i == expectedMask),
                    "Unexpected Sprite resource mask keyword " + i + ".");
            }
            Assert.That(enabledCount, Is.EqualTo(expectedMask < 0 ? 0 : 1));
        }

        private static void AssertQualityContract(string fileName)
        {
            string source = File.ReadAllText(Path.GetFullPath(ShaderRoot + fileName));
            const string contract = "#pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH";

            Assert.That(source, Does.Contain(contract),
                fileName + " must keep quality tiers in one local keyword set.");
        }

        private static void AssertTwoDQualityContract(string fileName)
        {
            string source = File.ReadAllText(Path.GetFullPath(ShaderRoot + fileName));
            const string contract = "#pragma shader_feature_local _ _ES_QUALITY_BASIC _ES_QUALITY_STANDARD";

            Assert.That(source, Does.Contain(contract),
                fileName + " must keep legacy-compatible high quality as the no-keyword variant.");
        }

        private static string ReadShaderSource(Shader shader)
        {
            string assetPath = AssetDatabase.GetAssetPath(shader);
            Assert.That(assetPath, Is.Not.Empty, shader.name + " has no project asset path.");
            return File.ReadAllText(Path.GetFullPath(assetPath));
        }

        private static string RegexEscape(string value)
        {
            return System.Text.RegularExpressions.Regex.Escape(value ?? string.Empty);
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(token, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count;
        }

        [Test]
        public void CompositeShaderDialogsUseEsDialogAndCodeHelpOpensUpperLeft()
        {
            string editorRoot = Path.GetFullPath(EditorShaderRoot);
            string[] sources = Directory.GetFiles(editorRoot, "*.cs", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < sources.Length; i++)
            {
                string source = File.ReadAllText(sources[i]);
                Assert.That(source, Does.Not.Contain("EditorUtility.DisplayDialog("),
                    Path.GetFileName(sources[i]) + " must use ESDialog.");
                Assert.That(source, Does.Not.Contain("EditorUtility.DisplayDialogComplex("),
                    Path.GetFileName(sources[i]) + " must use ESDialog.");
            }

            string dialogSource = File.ReadAllText(
                Path.GetFullPath("Assets/Plugins/ES/0_Stand/BaseDefine_Law/ESDialog.cs"));
            Assert.That(dialogSource, Does.Contain("public static void InfoModal("));
            Assert.That(dialogSource, Does.Contain("public static bool ConfirmModal("));

            string codingSource = File.ReadAllText(
                Path.Combine(editorRoot, "ESCompositeCodingHelper.cs"));
            Assert.That(codingSource, Does.Contain("codeButtonRect.xMin"));
            Assert.That(codingSource, Does.Contain("CalculateCodeDialogTopLeft"));
            Assert.That(codingSource, Does.Contain("buttonScreenTopLeft.x - dialogSize.x - 14f"));
            Assert.That(codingSource, Does.Contain("buttonScreenTopLeft.y - 14f"));
        }

    }
}
