using System.Collections.Generic;
using ES;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ES.Tests
{
    // 真实宿主覆盖：Canvas 裁剪、Renderer MPB 与 ParticleSystem 顶点流。
    public sealed partial class ESCompositeShaderVisualTests
    {
        private enum CanvasClipMode
        {
            None,
            RectMask,
            StencilMask
        }

        [Test]
        public void UIWorldSpaceCanvas_RectAndStencilMasksClipAfterCompositeEffects()
        {
            RequireGraphicsDevice();
            string outputDirectory = PrepareOutputDirectory();
            Texture2D baseTexture = CreateBaseTexture();
            Texture2D noiseTexture = CreateNoiseTexture();
            Sprite sprite = null;
            Material material = null;
            Texture2D unmasked = null;
            Texture2D rectMasked = null;
            Texture2D stencilMasked = null;

            try
            {
                Shader shader = Shader.Find("ES/UI/Composite URP");
                Assert.That(shader, Is.Not.Null);
                material = CreatePreparedMaterial(shader, baseTexture, noiseTexture);
                ConfigureExactEffects(material, true, true, 0f);
                sprite = Sprite.Create(
                    baseTexture,
                    new Rect(0f, 0f, baseTexture.width, baseTexture.height),
                    new Vector2(0.5f, 0.5f),
                    64f);
                sprite.hideFlags = HideFlags.HideAndDontSave;

                unmasked = RenderWorldSpaceCanvas(material, sprite, CanvasClipMode.None);
                rectMasked = RenderWorldSpaceCanvas(material, sprite, CanvasClipMode.RectMask);
                stencilMasked = RenderWorldSpaceCanvas(material, sprite, CanvasClipMode.StencilMask);

                float unmaskedCoverage = VisibleCoverage(unmasked);
                float rectCoverage = VisibleCoverage(rectMasked);
                float stencilCoverage = VisibleCoverage(stencilMasked);
                float rectDifference = MeanAbsoluteDifference(unmasked, rectMasked);
                float stencilDifference = MeanAbsoluteDifference(unmasked, stencilMasked);

                TestContext.WriteLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "UI Canvas: unmasked={0:F6}, rect={1:F6}, stencil={2:F6}, rectDiff={3:F6}, stencilDiff={4:F6}",
                    unmaskedCoverage,
                    rectCoverage,
                    stencilCoverage,
                    rectDifference,
                    stencilDifference));
                SaveImage(outputDirectory, "UI_30_Canvas_Unmasked.png", unmasked);
                SaveImage(outputDirectory, "UI_31_Canvas_RectMask.png", rectMasked);
                SaveImage(outputDirectory, "UI_32_Canvas_StencilMask.png", stencilMasked);

                Assert.That(unmaskedCoverage, Is.GreaterThan(0.35f), "World-space Canvas baseline must be visible.");
                Assert.That(rectCoverage, Is.GreaterThan(0.04f).And.LessThan(unmaskedCoverage * 0.55f),
                    "RectMask2D must retain a visible center while clipping the composite effect outside its rect.");
                Assert.That(stencilCoverage, Is.GreaterThan(0.04f).And.LessThan(unmaskedCoverage * 0.55f),
                    "Stencil Mask must retain a visible center while clipping the composite effect outside its mask.");
                Assert.That(rectDifference, Is.GreaterThan(0.02f));
                Assert.That(stencilDifference, Is.GreaterThan(0.02f));
            }
            finally
            {
                DestroyImmediateSafe(unmasked);
                DestroyImmediateSafe(rectMasked);
                DestroyImmediateSafe(stencilMasked);
                DestroyImmediateSafe(material);
                DestroyImmediateSafe(sprite);
                DestroyImmediateSafe(baseTexture);
                DestroyImmediateSafe(noiseTexture);
            }
        }

        [Test]
        public void UIRealTMPFontAsset_AdapterRendersStylingAndCompositeEffectsThenRestoresSource()
        {
            RequireGraphicsDevice();
            string outputDirectory = PrepareOutputDirectory();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Assert.That(font, Is.Not.Null, "The real project TMP font asset is required for host validation.");
            Assert.That(font.material, Is.Not.Null);

            Material baselineSource = new Material(font.material) { hideFlags = HideFlags.HideAndDontSave };
            Material styledSource = new Material(font.material) { hideFlags = HideFlags.HideAndDontSave };
            Texture2D baseline = null;
            Texture2D styled = null;
            Texture2D composite = null;

            try
            {
                ConfigureTMPSourceMaterial(baselineSource, false);
                ConfigureTMPSourceMaterial(styledSource, true);

                baseline = RenderWorldSpaceTMP(font, baselineSource, false, out bool baselineRestored);
                styled = RenderWorldSpaceTMP(font, styledSource, false, out bool styledRestored);
                composite = RenderWorldSpaceTMP(font, styledSource, true, out bool compositeRestored);

                float baselineCoverage = VisibleCoverage(baseline);
                float styledDifference = MeanAbsoluteDifference(baseline, styled);
                float compositeDifference = MeanAbsoluteDifference(styled, composite);
                TestContext.WriteLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "UI TMP host: coverage={0:F6}, styling={1:F6}, composite={2:F6}, restored={3}/{4}/{5}",
                    baselineCoverage,
                    styledDifference,
                    compositeDifference,
                    baselineRestored,
                    styledRestored,
                    compositeRestored));
                SaveImage(outputDirectory, "UI_60_TMP_Baseline.png", baseline);
                SaveImage(outputDirectory, "UI_61_TMP_OutlineUnderlay.png", styled);
                SaveImage(outputDirectory, "UI_62_TMP_CompositeHologram.png", composite);

                Assert.That(baselineCoverage, Is.GreaterThan(0.025f), "Real TMP glyphs must render visible pixels.");
                Assert.That(styledDifference, Is.GreaterThan(MinimumRepresentativeEffectDifference),
                    "TMP outline and underlay must materially change real glyph pixels.");
                Assert.That(compositeDifference, Is.GreaterThan(MinimumRepresentativeEffectDifference),
                    "ES exact hologram must remain active on adapted TMP glyphs.");
                Assert.That(baselineRestored && styledRestored && compositeRestored, Is.True,
                    "TMP adapter must restore the exact source font material after release.");
            }
            finally
            {
                DestroyImmediateSafe(baseline);
                DestroyImmediateSafe(styled);
                DestroyImmediateSafe(composite);
                DestroyImmediateSafe(baselineSource);
                DestroyImmediateSafe(styledSource);
            }
        }

        [Test]
        public void UISDFCanvasImage_FaceOutlineAndGlowChangeRealPixelsAndRespectParent()
        {
            RequireGraphicsDevice();
            string outputDirectory = PrepareOutputDirectory();
            Texture2D sdfTexture = CreateSDFTexture();
            Texture2D noiseTexture = CreateNoiseTexture();
            Sprite sprite = null;
            Material rawMaterial = null;
            Material disabledMaterial = null;
            Material faceMaterial = null;
            Material styledMaterial = null;
            Texture2D raw = null;
            Texture2D disabled = null;
            Texture2D face = null;
            Texture2D styled = null;

            try
            {
                Shader shader = Shader.Find("ES/UI/Composite URP");
                Assert.That(shader, Is.Not.Null);
                rawMaterial = CreatePreparedMaterial(shader, sdfTexture, noiseTexture);
                disabledMaterial = new Material(rawMaterial) { hideFlags = HideFlags.HideAndDontSave };
                faceMaterial = new Material(rawMaterial) { hideFlags = HideFlags.HideAndDontSave };
                styledMaterial = new Material(rawMaterial) { hideFlags = HideFlags.HideAndDontSave };
                sprite = Sprite.Create(
                    sdfTexture,
                    new Rect(0f, 0f, sdfTexture.width, sdfTexture.height),
                    new Vector2(0.5f, 0.5f),
                    64f);
                sprite.hideFlags = HideFlags.HideAndDontSave;

                ConfigureSDFMaterial(disabledMaterial, false, true);
                ConfigureSDFMaterial(faceMaterial, true, false);
                ConfigureSDFMaterial(styledMaterial, true, true);

                raw = RenderWorldSpaceCanvas(rawMaterial, sprite, CanvasClipMode.None);
                disabled = RenderWorldSpaceCanvas(disabledMaterial, sprite, CanvasClipMode.None);
                face = RenderWorldSpaceCanvas(faceMaterial, sprite, CanvasClipMode.None);
                styled = RenderWorldSpaceCanvas(styledMaterial, sprite, CanvasClipMode.None);

                float disabledDifference = MeanAbsoluteDifference(raw, disabled);
                float faceDifference = MeanAbsoluteDifference(raw, face);
                float styledDifference = MeanAbsoluteDifference(face, styled);
                TestContext.WriteLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "UI SDF: disabled={0:F6}, face={1:F6}, outlineGlow={2:F6}",
                    disabledDifference,
                    faceDifference,
                    styledDifference));
                SaveImage(outputDirectory, "UI_64_SDF_Raw.png", raw);
                SaveImage(outputDirectory, "UI_65_SDF_ParentDisabled.png", disabled);
                SaveImage(outputDirectory, "UI_66_SDF_Face.png", face);
                SaveImage(outputDirectory, "UI_67_SDF_OutlineGlow.png", styled);

                Assert.That(VisibleCoverage(raw), Is.GreaterThan(0.1f));
                Assert.That(disabledDifference, Is.LessThanOrEqualTo(MaximumDisabledEffectDifference),
                    "SDF child parameters must not leak while the SDF parent is disabled.");
                Assert.That(faceDifference, Is.GreaterThan(MinimumRepresentativeEffectDifference),
                    "Enabling SDF must materially resolve the distance-field face.");
                Assert.That(styledDifference, Is.GreaterThan(MinimumRepresentativeEffectDifference),
                    "SDF outline and glow must materially change resolved face pixels.");
            }
            finally
            {
                DestroyImmediateSafe(raw);
                DestroyImmediateSafe(disabled);
                DestroyImmediateSafe(face);
                DestroyImmediateSafe(styled);
                DestroyImmediateSafe(rawMaterial);
                DestroyImmediateSafe(disabledMaterial);
                DestroyImmediateSafe(faceMaterial);
                DestroyImmediateSafe(styledMaterial);
                DestroyImmediateSafe(sprite);
                DestroyImmediateSafe(sdfTexture);
                DestroyImmediateSafe(noiseTexture);
            }
        }

        [TestCase("2D", "ES/2D/Composite URP")]
        [TestCase("UI", "ES/UI/Composite URP")]
        [TestCase("Lit", "ES/3D/Lit Composite URP")]
        [TestCase("VFX", "ES/3D/VFX Composite URP")]
        public void RendererPropertyBlock_PreparedExactContractChangesRealPixels(
            string key,
            string shaderName)
        {
            RequireGraphicsDevice();
            string outputDirectory = PrepareOutputDirectory();
            Texture2D baseTexture = CreateBaseTexture();
            Texture2D noiseTexture = CreateNoiseTexture();
            Shader shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null);

            Material material = CreatePreparedMaterial(shader, baseTexture, noiseTexture);
            var disabledBlock = new MaterialPropertyBlock();
            var enabledBlock = new MaterialPropertyBlock();
            Texture2D disabledImage = null;
            Texture2D enabledImage = null;

            try
            {
                SetBasicQuality(material, shaderName);
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_HIGH"), Is.False,
                    key + " precondition must start outside the high-quality variant.");

                ConfigureExactEffects(disabledBlock, false, false, 0f);
                ConfigureExactEffects(enabledBlock, true, false, 0f);
                Assert.That(TryPrepareExactContract(material, enabledBlock, shaderName), Is.True);
                Assert.That(material.IsKeywordEnabled("_ES_QUALITY_HIGH"), Is.True,
                    key + " MPB preparation must select the high-quality shader variant.");

                disabledImage = RenderMeshRenderer(material, disabledBlock, key == "Lit");
                enabledImage = RenderMeshRenderer(material, enabledBlock, key == "Lit");
                float difference = MeanAbsoluteDifference(disabledImage, enabledImage);
                float coverage = VisibleCoverage(disabledImage);

                TestContext.WriteLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0} Renderer MPB: coverage={1:F6}, difference={2:F6}",
                    key,
                    coverage,
                    difference));
                SaveImage(outputDirectory, key + "_40_Renderer_MPB_Disabled.png", disabledImage);
                SaveImage(outputDirectory, key + "_41_Renderer_MPB_Hologram.png", enabledImage);

                Assert.That(coverage, Is.GreaterThan(MinimumVisibleCoverage));
                Assert.That(difference, Is.GreaterThan(MinimumEffectDifference),
                    key + " Renderer must consume the prepared MaterialPropertyBlock exact contract.");
            }
            finally
            {
                DestroyImmediateSafe(disabledImage);
                DestroyImmediateSafe(enabledImage);
                DestroyImmediateSafe(material);
                DestroyImmediateSafe(baseTexture);
                DestroyImmediateSafe(noiseTexture);
            }
        }

        [Test]
        public void VFXParticleSystem_CustomStreamsDriveAtlasUvDissolveAndEmission()
        {
            RequireGraphicsDevice();
            string outputDirectory = PrepareOutputDirectory();
            Texture2D atlasTexture = CreateParticleAtlasTexture();
            Texture2D noiseTexture = CreateNoiseTexture();
            Shader shader = Shader.Find("ES/3D/VFX Composite URP");
            Assert.That(shader, Is.Not.Null);
            Material material = CreatePreparedMaterial(shader, atlasTexture, noiseTexture);
            Material streamsDisabledMaterial = null;
            Texture2D baseline = null;
            Texture2D uvOffset = null;
            Texture2D atlasFrame = null;
            Texture2D dissolved = null;
            Texture2D emissive = null;
            Texture2D streamsDisabled = null;

            try
            {
                SetFloatIfPresent(material, "_EnableVertexStreams", 1f);
                SetFloatIfPresent(material, "_VertexStreamUVStrength", 1f);
                SetFloatIfPresent(material, "_VertexStreamFrameStrength", 1f);
                SetFloatIfPresent(material, "_VertexStreamDissolveStrength", 1f);
                SetFloatIfPresent(material, "_VertexStreamEmissionStrength", 2f);
                SetFloatIfPresent(material, "_EnableSequence", 1f);
                SetFloatIfPresent(material, "_SequencePlayback", 2f);
                SetFloatIfPresent(material, "_SequenceColumns", 2f);
                SetFloatIfPresent(material, "_SequenceRows", 2f);
                SetFloatIfPresent(material, "_SequenceFrame", 0f);
                SetFloatIfPresent(material, "_DissolveMode", 2f);
                SetFloatIfPresent(material, "_DissolveProgress", 0.15f);
                SetFloatIfPresent(material, "_DissolveWidth", 0.08f);
                SetColorIfPresent(material, "_DissolveColor", new Color(2f, 0.08f, 0.02f, 1f));
                SetColorIfPresent(material, "_EmissionColor", new Color(0.08f, 0.2f, 0.7f, 1f));

                baseline = RenderParticleSystem(material, Vector4.zero, Vector4.zero);
                uvOffset = RenderParticleSystem(material, new Vector4(0.18f, -0.12f, 0f, 0f), Vector4.zero);
                atlasFrame = RenderParticleSystem(material, new Vector4(0f, 0f, 3f, 0f), Vector4.zero);
                dissolved = RenderParticleSystem(material, new Vector4(0f, 0f, 0f, 0.55f), Vector4.zero);
                emissive = RenderParticleSystem(material, Vector4.zero, new Vector4(2f, 0f, 0f, 0f));
                streamsDisabledMaterial = new Material(material) { hideFlags = HideFlags.HideAndDontSave };
                SetFloatIfPresent(streamsDisabledMaterial, "_EnableVertexStreams", 0f);
                streamsDisabled = RenderParticleSystem(
                    streamsDisabledMaterial,
                    new Vector4(0.18f, -0.12f, 3f, 0.55f),
                    new Vector4(2f, 0f, 0f, 0f));

                float uvDifference = MeanAbsoluteDifference(baseline, uvOffset);
                float frameDifference = MeanAbsoluteDifference(baseline, atlasFrame);
                float dissolveDifference = MeanAbsoluteDifference(baseline, dissolved);
                float emissionDifference = MeanAbsoluteDifference(baseline, emissive);
                float disabledDifference = MeanAbsoluteDifference(baseline, streamsDisabled);

                TestContext.WriteLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "VFX Particle streams: uv={0:F6}, frame={1:F6}, dissolve={2:F6}, emission={3:F6}, disabled={4:F6}",
                    uvDifference,
                    frameDifference,
                    dissolveDifference,
                    emissionDifference,
                    disabledDifference));
                SaveImage(outputDirectory, "VFX_50_Particle_Baseline.png", baseline);
                SaveImage(outputDirectory, "VFX_51_Particle_Custom1UV.png", uvOffset);
                SaveImage(outputDirectory, "VFX_52_Particle_Custom1Frame.png", atlasFrame);
                SaveImage(outputDirectory, "VFX_53_Particle_Custom1Dissolve.png", dissolved);
                SaveImage(outputDirectory, "VFX_54_Particle_Custom2Emission.png", emissive);
                SaveImage(outputDirectory, "VFX_55_Particle_StreamsDisabled.png", streamsDisabled);

                Assert.That(VisibleCoverage(baseline), Is.GreaterThan(0.08f));
                Assert.That(uvDifference, Is.GreaterThan(MinimumRepresentativeEffectDifference));
                Assert.That(frameDifference, Is.GreaterThan(MinimumRepresentativeEffectDifference));
                Assert.That(dissolveDifference, Is.GreaterThan(MinimumRepresentativeEffectDifference));
                Assert.That(emissionDifference, Is.GreaterThan(MinimumRepresentativeEffectDifference));
                Assert.That(disabledDifference, Is.LessThanOrEqualTo(MaximumDisabledEffectDifference),
                    "Custom1/Custom2 values must not leak while the vertex-stream parent is disabled.");
            }
            finally
            {
                DestroyImmediateSafe(baseline);
                DestroyImmediateSafe(uvOffset);
                DestroyImmediateSafe(atlasFrame);
                DestroyImmediateSafe(dissolved);
                DestroyImmediateSafe(emissive);
                DestroyImmediateSafe(streamsDisabled);
                DestroyImmediateSafe(streamsDisabledMaterial);
                DestroyImmediateSafe(material);
                DestroyImmediateSafe(atlasTexture);
                DestroyImmediateSafe(noiseTexture);
            }
        }

        [Test]
        public void VFXDepthTexture_SoftParticlesAndIntersectionChangeRealPixelsAndRespectParents()
        {
            RequireGraphicsDevice();
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>(),
                "The depth interaction visual test requires the active URP pipeline.");
            string outputDirectory = PrepareOutputDirectory();
            Texture2D baseTexture = CreateBaseTexture();
            Texture2D noiseTexture = CreateNoiseTexture();
            Shader shader = Shader.Find("ES/3D/VFX Composite URP");
            Assert.That(shader, Is.Not.Null);
            Material baselineMaterial = CreatePreparedMaterial(shader, baseTexture, noiseTexture);
            Material disabledMaterial = new Material(baselineMaterial) { hideFlags = HideFlags.HideAndDontSave };
            Material softParticleMaterial = new Material(baselineMaterial) { hideFlags = HideFlags.HideAndDontSave };
            Material intersectionMaterial = new Material(baselineMaterial) { hideFlags = HideFlags.HideAndDontSave };
            Texture2D baseline = null;
            Texture2D disabled = null;
            Texture2D softParticles = null;
            Texture2D intersection = null;

            try
            {
                SetFloatIfPresent(disabledMaterial, "_SoftParticleNear", 4.5f);
                SetFloatIfPresent(disabledMaterial, "_SoftParticleFar", 5f);
                SetColorIfPresent(disabledMaterial, "_DepthIntersectionColor", new Color(4f, 0f, 4f, 1f));
                SetFloatIfPresent(disabledMaterial, "_DepthIntersectionDistance", 4f);
                SetFloatIfPresent(disabledMaterial, "_DepthIntersectionIntensity", 8f);

                SetFloatIfPresent(softParticleMaterial, "_EnableSoftParticles", 1f);
                SetFloatIfPresent(softParticleMaterial, "_SoftParticleNear", 0f);
                SetFloatIfPresent(softParticleMaterial, "_SoftParticleFar", 0.5f);

                SetFloatIfPresent(intersectionMaterial, "_EnableDepthIntersection", 1f);
                SetColorIfPresent(intersectionMaterial, "_DepthIntersectionColor", new Color(0.02f, 1.4f, 2.5f, 1f));
                SetFloatIfPresent(intersectionMaterial, "_DepthIntersectionDistance", 0.35f);
                SetFloatIfPresent(intersectionMaterial, "_DepthIntersectionIntensity", 3f);

                baseline = RenderVFXDepthScene(baselineMaterial);
                disabled = RenderVFXDepthScene(disabledMaterial);
                softParticles = RenderVFXDepthScene(softParticleMaterial);
                intersection = RenderVFXDepthScene(intersectionMaterial);

                float disabledDifference = MeanAbsoluteDifference(baseline, disabled);
                float softDifference = MeanAbsoluteDifference(baseline, softParticles);
                float intersectionDifference = MeanAbsoluteDifference(baseline, intersection);
                TestContext.WriteLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "VFX depth: disabled={0:F6}, soft={1:F6}, intersection={2:F6}",
                    disabledDifference,
                    softDifference,
                    intersectionDifference));
                SaveImage(outputDirectory, "VFX_70_Depth_Baseline.png", baseline);
                SaveImage(outputDirectory, "VFX_71_Depth_ParentsDisabled.png", disabled);
                SaveImage(outputDirectory, "VFX_72_Depth_SoftParticles.png", softParticles);
                SaveImage(outputDirectory, "VFX_73_Depth_Intersection.png", intersection);

                Assert.That(VisibleCoverage(baseline), Is.GreaterThan(0.2f));
                Assert.That(disabledDifference, Is.LessThanOrEqualTo(MaximumDisabledEffectDifference),
                    "Depth child parameters must not leak while both depth parents are disabled.");
                Assert.That(softDifference, Is.GreaterThan(MinimumRepresentativeEffectDifference),
                    "Soft particles must fade real pixels against an opaque depth surface.");
                Assert.That(intersectionDifference, Is.GreaterThan(MinimumRepresentativeEffectDifference),
                    "Depth intersection must add visible color near an opaque depth surface.");
            }
            finally
            {
                DestroyImmediateSafe(baseline);
                DestroyImmediateSafe(disabled);
                DestroyImmediateSafe(softParticles);
                DestroyImmediateSafe(intersection);
                DestroyImmediateSafe(baselineMaterial);
                DestroyImmediateSafe(disabledMaterial);
                DestroyImmediateSafe(softParticleMaterial);
                DestroyImmediateSafe(intersectionMaterial);
                DestroyImmediateSafe(baseTexture);
                DestroyImmediateSafe(noiseTexture);
            }
        }

        private static void ConfigureExactEffects(
            MaterialPropertyBlock block,
            bool hologram,
            bool glitch,
            float hologramSpace)
        {
            block.SetFloat(Shader.PropertyToID("_SSUStatusContract"), 1f);
            block.SetFloat(Shader.PropertyToID("_EnableHologram"), hologram ? 1f : 0f);
            block.SetFloat(Shader.PropertyToID("_HologramFade"), hologram ? 1f : 0f);
            block.SetColor(Shader.PropertyToID("_HologramColor"), new Color(0.08f, 0.9f, 1.8f, 1f));
            block.SetFloat(Shader.PropertyToID("_HologramContrast"), 1.35f);
            block.SetFloat(Shader.PropertyToID("_HologramSpace"), hologramSpace);
            block.SetFloat(Shader.PropertyToID("_HologramLineFrequency"), 22f);
            block.SetFloat(Shader.PropertyToID("_HologramLineGap"), 0.7f);
            block.SetFloat(Shader.PropertyToID("_HologramSpeed"), 0.8f);
            block.SetFloat(Shader.PropertyToID("_HologramMinAlpha"), 0.18f);
            block.SetFloat(Shader.PropertyToID("_HologramDistortionOffset"), 0.09f);
            block.SetFloat(Shader.PropertyToID("_HologramDistortionSpeed"), 1.4f);
            block.SetFloat(Shader.PropertyToID("_HologramDistortionDensity"), 1.7f);
            block.SetFloat(Shader.PropertyToID("_HologramDistortionScale"), 7f);

            block.SetFloat(Shader.PropertyToID("_EnableGlitch"), glitch ? 1f : 0f);
            block.SetFloat(Shader.PropertyToID("_GlitchFade"), glitch ? 1f : 0f);
            block.SetFloat(Shader.PropertyToID("_GlitchMaskMin"), 0.82f);
            block.SetVector(Shader.PropertyToID("_GlitchMaskScale"), new Vector4(2f, 5f, 0f, 0f));
            block.SetVector(Shader.PropertyToID("_GlitchMaskSpeed"), new Vector4(0.3f, 1.2f, 0f, 0f));
            block.SetFloat(Shader.PropertyToID("_GlitchHueSpeed"), 0.65f);
            block.SetFloat(Shader.PropertyToID("_GlitchBrightness"), 4.5f);
            block.SetVector(Shader.PropertyToID("_GlitchNoiseScale"), new Vector4(1.5f, 4f, 0f, 0f));
            block.SetVector(Shader.PropertyToID("_GlitchNoiseSpeed"), new Vector4(0.6f, 0.25f, 0f, 0f));
            block.SetVector(Shader.PropertyToID("_GlitchDistortion"), new Vector4(0.18f, 0.04f, 0f, 0f));
            block.SetVector(Shader.PropertyToID("_GlitchDistortionScale"), new Vector4(2f, 6f, 0f, 0f));
            block.SetVector(Shader.PropertyToID("_GlitchDistortionSpeed"), new Vector4(1.1f, 0.4f, 0f, 0f));
        }

        private static void SetBasicQuality(Material material, string shaderName)
        {
            switch (shaderName)
            {
                case "ES/2D/Composite URP":
                    ES2DCompositeURPProperties.SetQuality(material, ESCompositeQualityTier.基础);
                    break;
                case "ES/UI/Composite URP":
                    ESUICompositeURPProperties.SetQuality(material, ESCompositeQualityTier.基础);
                    break;
                case "ES/3D/Lit Composite URP":
                    ES3DLitCompositeURPProperties.SetQuality(material, ESCompositeQualityTier.基础);
                    break;
                case "ES/3D/VFX Composite URP":
                    ES3DVFXCompositeURPProperties.SetQuality(material, ESCompositeQualityTier.基础);
                    break;
                default:
                    Assert.Fail("Unsupported MPB visual validation shader: " + shaderName);
                    break;
            }
        }

        private static bool TryPrepareExactContract(
            Material material,
            MaterialPropertyBlock block,
            string shaderName)
        {
            switch (shaderName)
            {
                case "ES/2D/Composite URP":
                    return ES2DCompositeURPProperties.TrySetSSUExactContract(material, block, true);
                case "ES/UI/Composite URP":
                    return ESUICompositeURPProperties.TrySetSSUExactContract(material, block, true);
                case "ES/3D/Lit Composite URP":
                    return ES3DLitCompositeURPProperties.TrySetSSUExactContract(material, block, true);
                case "ES/3D/VFX Composite URP":
                    return ES3DVFXCompositeURPProperties.TrySetSSUExactContract(material, block, true);
                default:
                    Assert.Fail("Unsupported MPB visual validation shader: " + shaderName);
                    return false;
            }
        }

        private static Texture2D RenderWorldSpaceCanvas(
            Material material,
            Sprite sprite,
            CanvasClipMode clipMode)
        {
            GameObject cameraObject = null;
            GameObject canvasObject = null;
            RenderTexture target = null;
            try
            {
                cameraObject = new GameObject("ES Composite Canvas Camera", typeof(Camera));
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                Camera camera = cameraObject.GetComponent<Camera>();
                ConfigureCaptureCamera(camera, 1.28f, 31);

                target = CreateCaptureTarget("ES Composite Canvas Target");
                camera.targetTexture = target;

                canvasObject = new GameObject("ES Composite Canvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.hideFlags = HideFlags.HideAndDontSave;
                canvasObject.layer = 31;
                RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(256f, 256f);
                canvasRect.localScale = Vector3.one * 0.01f;
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvas.sortingOrder = 100;

                Transform parent = canvasObject.transform;
                if (clipMode != CanvasClipMode.None)
                {
                    var clipObject = new GameObject("Clip", typeof(RectTransform));
                    clipObject.hideFlags = HideFlags.HideAndDontSave;
                    clipObject.layer = 31;
                    clipObject.transform.SetParent(canvasObject.transform, false);
                    clipObject.GetComponent<RectTransform>().sizeDelta = new Vector2(108f, 108f);
                    parent = clipObject.transform;

                    if (clipMode == CanvasClipMode.RectMask)
                    {
                        clipObject.AddComponent<RectMask2D>();
                    }
                    else
                    {
                        Image maskImage = clipObject.AddComponent<Image>();
                        maskImage.sprite = sprite;
                        maskImage.color = Color.white;
                        Mask mask = clipObject.AddComponent<Mask>();
                        mask.showMaskGraphic = false;
                    }
                }

                var imageObject = new GameObject("Composite Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imageObject.hideFlags = HideFlags.HideAndDontSave;
                imageObject.layer = 31;
                imageObject.transform.SetParent(parent, false);
                imageObject.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 220f);
                Image image = imageObject.GetComponent<Image>();
                image.sprite = sprite;
                image.material = material;
                image.color = Color.white;

                Canvas.ForceUpdateCanvases();
                image.SetVerticesDirty();
                image.SetMaterialDirty();
                Canvas.ForceUpdateCanvases();
                if (clipMode == CanvasClipMode.StencilMask)
                {
                    Material renderingMaterial = image.materialForRendering;
                    Assert.That(renderingMaterial, Is.Not.Null);
                    Assert.That(renderingMaterial.GetFloat("_Stencil"), Is.GreaterThan(0f),
                        "MaskableGraphic must provide a non-zero stencil reference to the ES UI shader.");
                }

                camera.Render();
                return ReadRenderTexture(target, "ES Composite Canvas Readback");
            }
            finally
            {
                DestroyImmediateSafe(canvasObject);
                DestroyImmediateSafe(cameraObject);
                ReleaseCaptureTarget(target);
            }
        }

        private static Texture2D RenderWorldSpaceTMP(
            TMP_FontAsset font,
            Material sourceMaterial,
            bool enableCompositeEffects,
            out bool sourceRestored)
        {
            GameObject cameraObject = null;
            GameObject canvasObject = null;
            RenderTexture target = null;
            sourceRestored = false;
            try
            {
                cameraObject = new GameObject("ES Composite TMP Camera", typeof(Camera));
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                Camera camera = cameraObject.GetComponent<Camera>();
                ConfigureCaptureCamera(camera, 1.28f, 31);
                target = CreateCaptureTarget("ES Composite TMP Target");
                camera.targetTexture = target;

                canvasObject = new GameObject("ES Composite TMP Canvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.hideFlags = HideFlags.HideAndDontSave;
                canvasObject.layer = 31;
                RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(256f, 256f);
                canvasRect.localScale = Vector3.one * 0.01f;
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvas.sortingOrder = 100;

                var textObject = new GameObject(
                    "ES Composite TMP Text",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                textObject.hideFlags = HideFlags.HideAndDontSave;
                textObject.layer = 31;
                textObject.transform.SetParent(canvasObject.transform, false);
                textObject.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 170f);
                TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
                text.font = font;
                text.fontSharedMaterial = sourceMaterial;
                text.text = "ES";
                text.fontSize = 118f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.enableWordWrapping = false;
                text.raycastTarget = false;

                ESCompositeTMPMaterialAdapter adapter = textObject.AddComponent<ESCompositeTMPMaterialAdapter>();
                adapter.Configure(text, sourceMaterial);
                Material runtimeMaterial = adapter.Acquire();
                Assert.That(runtimeMaterial, Is.Not.Null);
                Assert.That(runtimeMaterial.shader.name, Is.EqualTo("ES/UI/Composite URP"));
                Assert.That(runtimeMaterial.GetFloat("_EnableTMPCompatibility"), Is.EqualTo(1f));
                Assert.That(ESUICompositeURPProperties.PrepareMaterialForDynamicSSU(runtimeMaterial), Is.True);
                ConfigureExactEffects(runtimeMaterial, enableCompositeEffects, false, 0f);

                text.ForceMeshUpdate(true, true);
                text.SetVerticesDirty();
                text.SetMaterialDirty();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                Texture2D result = ReadRenderTexture(target, "ES Composite TMP Readback");

                adapter.Release();
                sourceRestored = text.fontSharedMaterial == sourceMaterial && !adapter.HasInstance;
                return result;
            }
            finally
            {
                DestroyImmediateSafe(canvasObject);
                DestroyImmediateSafe(cameraObject);
                ReleaseCaptureTarget(target);
            }
        }

        private static void ConfigureTMPSourceMaterial(Material material, bool styled)
        {
            material.SetColor("_FaceColor", Color.white);
            material.SetFloat("_FaceDilate", 0f);
            material.SetFloat("_OutlineWidth", styled ? 0.24f : 0f);
            material.SetFloat("_OutlineSoftness", styled ? 0.08f : 0f);
            material.SetColor("_OutlineColor", new Color(1.5f, 0.08f, 0.75f, 1f));
            material.SetFloat("_UnderlayOffsetX", 0.18f);
            material.SetFloat("_UnderlayOffsetY", -0.18f);
            material.SetFloat("_UnderlayDilate", 0.08f);
            material.SetFloat("_UnderlaySoftness", 0.12f);
            material.SetColor("_UnderlayColor", new Color(0.02f, 0.65f, 1.4f, 0.9f));
            if (styled)
                material.EnableKeyword("UNDERLAY_ON");
            else
                material.DisableKeyword("UNDERLAY_ON");
        }

        private static void ConfigureSDFMaterial(Material material, bool enabled, bool styled)
        {
            SetFloatIfPresent(material, "_EnableSDF", enabled ? 1f : 0f);
            SetFloatIfPresent(material, "_SDFThreshold", 0.5f);
            SetFloatIfPresent(material, "_SDFSoftness", 0.65f);
            SetFloatIfPresent(material, "_SDFOutlineWidth", styled ? 0.14f : 0f);
            SetFloatIfPresent(material, "_SDFOutlineSoftness", styled ? 1.2f : 0.65f);
            SetColorIfPresent(material, "_SDFOutlineColor", new Color(1.8f, 0.08f, 0.55f, 1f));
            SetFloatIfPresent(material, "_SDFGlowWidth", styled ? 0.18f : 0f);
            SetColorIfPresent(material, "_SDFGlowColor", new Color(0.02f, 0.8f, 2.2f, 0.9f));
        }

        private static Texture2D RenderMeshRenderer(
            Material material,
            MaterialPropertyBlock block,
            bool addDirectionalLight)
        {
            GameObject cameraObject = null;
            GameObject rendererObject = null;
            GameObject lightObject = null;
            RenderTexture target = null;
            Mesh mesh = null;
            try
            {
                cameraObject = new GameObject("ES Composite Renderer Camera", typeof(Camera));
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                Camera camera = cameraObject.GetComponent<Camera>();
                ConfigureCaptureCamera(camera, 0.55f, 30);
                target = CreateCaptureTarget("ES Composite Renderer Target");
                camera.targetTexture = target;

                rendererObject = new GameObject("ES Composite Renderer", typeof(MeshFilter), typeof(MeshRenderer));
                rendererObject.hideFlags = HideFlags.HideAndDontSave;
                rendererObject.layer = 30;
                mesh = CreateQuad();
                rendererObject.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = rendererObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.SetPropertyBlock(block);

                if (addDirectionalLight)
                {
                    lightObject = new GameObject("ES Composite Renderer Light", typeof(Light));
                    lightObject.hideFlags = HideFlags.HideAndDontSave;
                    lightObject.transform.rotation = Quaternion.Euler(25f, 30f, 0f);
                    Light light = lightObject.GetComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1.5f;
                    light.cullingMask = 1 << 30;
                }

                camera.Render();
                return ReadRenderTexture(target, "ES Composite Renderer Readback");
            }
            finally
            {
                DestroyImmediateSafe(lightObject);
                DestroyImmediateSafe(rendererObject);
                DestroyImmediateSafe(cameraObject);
                DestroyImmediateSafe(mesh);
                ReleaseCaptureTarget(target);
            }
        }

        private static Texture2D RenderParticleSystem(
            Material material,
            Vector4 custom1,
            Vector4 custom2)
        {
            GameObject cameraObject = null;
            GameObject particleObject = null;
            RenderTexture target = null;
            try
            {
                cameraObject = new GameObject("ES Composite Particle Camera", typeof(Camera));
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                Camera camera = cameraObject.GetComponent<Camera>();
                ConfigureCaptureCamera(camera, 0.65f, 29);
                target = CreateCaptureTarget("ES Composite Particle Target");
                camera.targetTexture = target;

                particleObject = new GameObject("ES Composite Particle", typeof(ParticleSystem));
                particleObject.hideFlags = HideFlags.HideAndDontSave;
                particleObject.layer = 29;
                ParticleSystem particleSystem = particleObject.GetComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particleSystem.main;
                main.loop = false;
                main.playOnAwake = false;
                main.maxParticles = 1;
                main.startLifetime = 100f;
                main.startSpeed = 0f;
                main.startSize = 1f;
                main.startColor = Color.white;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.enabled = false;
                ParticleSystem.CustomDataModule customData = particleSystem.customData;
                customData.enabled = true;
                customData.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
                customData.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 4);
                customData.SetMode(ParticleSystemCustomData.Custom2, ParticleSystemCustomDataMode.Vector);
                customData.SetVectorComponentCount(ParticleSystemCustomData.Custom2, 1);

                ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                var streams = new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position,
                    ParticleSystemVertexStream.Normal,
                    ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV,
                    ParticleSystemVertexStream.Custom1XYZW,
                    ParticleSystemVertexStream.Custom2X
                };
                renderer.SetActiveVertexStreams(streams);
                var activeStreams = new List<ParticleSystemVertexStream>();
                renderer.GetActiveVertexStreams(activeStreams);
                CollectionAssert.AreEqual(streams, activeStreams,
                    "ParticleSystemRenderer must preserve the exact ES Custom1/Custom2 stream contract.");

                var emit = new ParticleSystem.EmitParams
                {
                    position = Vector3.zero,
                    startLifetime = 100f,
                    startSize = 1f,
                    startColor = Color.white
                };
                particleSystem.Emit(emit, 1);
                particleSystem.SetCustomParticleData(
                    new List<Vector4> { custom1 },
                    ParticleSystemCustomData.Custom1);
                particleSystem.SetCustomParticleData(
                    new List<Vector4> { custom2 },
                    ParticleSystemCustomData.Custom2);
                particleSystem.Pause(true);

                camera.Render();
                return ReadRenderTexture(target, "ES Composite Particle Readback");
            }
            finally
            {
                DestroyImmediateSafe(particleObject);
                DestroyImmediateSafe(cameraObject);
                ReleaseCaptureTarget(target);
            }
        }

        private static Texture2D RenderVFXDepthScene(Material vfxMaterial)
        {
            GameObject cameraObject = null;
            GameObject opaqueObject = null;
            GameObject vfxObject = null;
            Material opaqueMaterial = null;
            RenderTexture target = null;
            Mesh mesh = null;
            try
            {
                cameraObject = new GameObject("ES Composite VFX Depth Camera", typeof(Camera));
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                Camera camera = cameraObject.GetComponent<Camera>();
                ConfigureCaptureCamera(camera, 0.65f, 28);
                UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                cameraData.requiresDepthTexture = true;
                cameraData.requiresColorTexture = false;
                cameraData.renderPostProcessing = false;
                target = CreateCaptureTarget("ES Composite VFX Depth Target");
                camera.targetTexture = target;

                Shader opaqueShader = Shader.Find("Universal Render Pipeline/Unlit");
                Assert.That(opaqueShader, Is.Not.Null);
                opaqueMaterial = new Material(opaqueShader) { hideFlags = HideFlags.HideAndDontSave };
                SetColorIfPresent(opaqueMaterial, "_BaseColor", new Color(0.025f, 0.04f, 0.075f, 1f));
                SetFloatIfPresent(opaqueMaterial, "_Surface", 0f);
                SetFloatIfPresent(opaqueMaterial, "_ZWrite", 1f);
                SetFloatIfPresent(opaqueMaterial, "_Cull", 0f);
                opaqueMaterial.renderQueue = (int)RenderQueue.Geometry;

                mesh = CreateQuad();
                opaqueObject = new GameObject("ES Composite Opaque Depth Surface", typeof(MeshFilter), typeof(MeshRenderer));
                opaqueObject.hideFlags = HideFlags.HideAndDontSave;
                opaqueObject.layer = 28;
                opaqueObject.transform.position = Vector3.zero;
                opaqueObject.transform.localScale = Vector3.one * 1.18f;
                opaqueObject.GetComponent<MeshFilter>().sharedMesh = mesh;
                opaqueObject.GetComponent<MeshRenderer>().sharedMaterial = opaqueMaterial;

                vfxObject = new GameObject("ES Composite VFX Depth Quad", typeof(MeshFilter), typeof(MeshRenderer));
                vfxObject.hideFlags = HideFlags.HideAndDontSave;
                vfxObject.layer = 28;
                vfxObject.transform.position = new Vector3(0f, 0f, -0.08f);
                vfxObject.GetComponent<MeshFilter>().sharedMesh = mesh;
                vfxObject.GetComponent<MeshRenderer>().sharedMaterial = vfxMaterial;

                camera.Render();
                return ReadRenderTexture(target, "ES Composite VFX Depth Readback");
            }
            finally
            {
                DestroyImmediateSafe(vfxObject);
                DestroyImmediateSafe(opaqueObject);
                DestroyImmediateSafe(cameraObject);
                DestroyImmediateSafe(opaqueMaterial);
                DestroyImmediateSafe(mesh);
                ReleaseCaptureTarget(target);
            }
        }

        private static void ConfigureCaptureCamera(Camera camera, float orthographicSize, int layer)
        {
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.transform.position = new Vector3(0f, 0f, -2.05f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = PreviewBackground;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            camera.cullingMask = 1 << layer;
        }

        private static RenderTexture CreateCaptureTarget(string name)
        {
            var target = new RenderTexture(PreviewSize, PreviewSize, 24, RenderTextureFormat.ARGB32)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();
            return target;
        }

        private static void ReleaseCaptureTarget(RenderTexture target)
        {
            if (target == null) return;
            target.Release();
            DestroyImmediateSafe(target);
        }

        private static Texture2D ReadRenderTexture(RenderTexture target, string name)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                var texture = new Texture2D(
                    target.width,
                    target.height,
                    UnityEngine.TextureFormat.RGBA32,
                    false,
                    false)
                {
                    name = name,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0, false);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static Texture2D CreateParticleAtlasTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, UnityEngine.TextureFormat.RGBA32, false, false)
            {
                name = "ES Composite Particle Atlas",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            var pixels = new Color[size * size];
            Color[] quadrantColors =
            {
                new Color(1f, 0.08f, 0.04f, 1f),
                new Color(0.04f, 1f, 0.12f, 1f),
                new Color(0.05f, 0.18f, 1f, 1f),
                new Color(1f, 0.75f, 0.04f, 1f)
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int column = x < size / 2 ? 0 : 1;
                    int rowFromTop = y >= size / 2 ? 0 : 1;
                    int quadrant = rowFromTop * 2 + column;
                    float localX = (x % (size / 2)) / (float)(size / 2 - 1);
                    float localY = (y % (size / 2)) / (float)(size / 2 - 1);
                    float alpha = Mathf.SmoothStep(
                        0.03f,
                        0.16f,
                        0.5f - Vector2.Distance(new Vector2(localX, localY), Vector2.one * 0.5f));
                    float pattern = ((x / 4 + y / 4) & 1) == 0 ? 0.55f : 1f;
                    Color color = quadrantColors[quadrant] * pattern;
                    color.a = alpha;
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateSDFTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, UnityEngine.TextureFormat.RGBA32, false, true)
            {
                name = "ES Composite SDF Circle",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 uv = new Vector2(x, y) / (size - 1f);
                    float signedDistance = 0.29f - Vector2.Distance(uv, Vector2.one * 0.5f);
                    float distanceValue = Mathf.Clamp01(0.5f + signedDistance * 3.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, distanceValue);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
