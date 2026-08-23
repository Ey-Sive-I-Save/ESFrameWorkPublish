using System;
using UnityEditor;
using UnityEngine;

namespace ES.TestAssets.Editor
{
    internal static partial class ESCompositeShaderTestAssetsBuilder
    {
        private static GeneratedMaterials CreateMaterials(GeneratedTextures textures)
        {
            var materials = new GeneratedMaterials();
            Create2DMaterials(textures, materials);
            CreateUIMaterials(textures, materials);
            CreateLitMaterials(textures, materials);
            CreateVfxMaterials(textures, materials);
            CreateProductionMaterials(textures, materials);
            CreateEnvironmentMaterials(textures, materials);
            return materials;
        }

        private static void Create2DMaterials(GeneratedTextures t, GeneratedMaterials output)
        {
            AddCase(output, "2d.base", "01_2D/01_Base_原始图标.mat", Shader2D, t, ESCompositeQualityTier.基础, false, null);
            AddCase(output, "2d.shine_horizontal", "01_2D/02_Shine_水平扫光.mat", Shader2D, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableShine", 1f); Set(m, "_ShineColor", Hdr(2.4f, 1.8f, 0.55f));
                Set(m, "_ShineDirection", new Vector4(1f, 0f, 0f, 0f)); Set(m, "_ShineSpeed", 0.75f);
                Set(m, "_ShineWidth", 0.12f); Set(m, "_ShineIntensity", 1.4f);
            });
            AddCase(output, "2d.shine_diagonal", "01_2D/03_Shine_右上斜向.mat", Shader2D, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableShine", 1f); Set(m, "_ShineColor", Hdr(0.4f, 1.8f, 2.8f));
                Set(m, "_ShineDirection", new Vector4(0.72f, 0.69f, 0f, 0f)); Set(m, "_ShineSpeed", 0.9f);
                Set(m, "_ShineWidth", 0.16f); Set(m, "_ShineIntensity", 1.25f);
            });
            AddCase(output, "2d.dissolve_directional", "01_2D/04_Dissolve_方向发光.mat", Shader2D, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_FadeMode", 4f); Set(m, "_FadeProgress", 0.52f); Set(m, "_FadeRotation", 32f);
                Set(m, "_FadeWidth", 0.11f); Set(m, "_FadeNoiseFactor", 0.3f);
                Set(m, "_DissolveEdgeColor", Hdr(3.2f, 0.28f, 0.03f)); Set(m, "_DissolveEdgeIntensity", 2.2f);
            });
            AddCase(output, "2d.dissolve_radial", "01_2D/05_Dissolve_源点扩散.mat", Shader2D, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_FadeMode", 7f); Set(m, "_FadeProgress", 0.58f); Set(m, "_FadePosition", new Vector4(0.5f, 0.5f, 0f, 0f));
                Set(m, "_FadeWidth", 0.12f); Set(m, "_FadeNoiseFactor", 0.2f);
                Set(m, "_DissolveEdgeColor", Hdr(0.1f, 2.1f, 3.6f));
            });
            AddCase(output, "2d.pixel_outline", "01_2D/06_Outline_像素描边.mat", Shader2D, t, ESCompositeQualityTier.标准, true, m =>
            {
                Set(m, "_EnablePixelOutline", 1f); Set(m, "_PixelOutlineColor", Hdr(1.8f, 0.15f, 0.05f));
                Set(m, "_PixelOutlineWidth", 2f); Set(m, "_PixelOutlineFade", 1f);
            });
            AddCase(output, "2d.hologram_local", "01_2D/07_Hologram_局部UV.mat", Shader2D, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableHologram", 1f); Set(m, "_HologramColor", Hdr(0.05f, 1.4f, 2.9f));
                Set(m, "_HologramSpace", 0f); Set(m, "_HologramLineFrequency", 70f); Set(m, "_HologramSpeed", 1.1f);
            });
            AddCase(output, "2d.glitch", "01_2D/08_Glitch_横向故障.mat", Shader2D, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableGlitch", 1f); Set(m, "_GlitchFade", 0.72f);
                Set(m, "_GlitchScanDirection", new Vector4(0f, 1f, 0f, 0f));
                Set(m, "_GlitchDistortion", new Vector4(0.045f, 0f, 0f, 0f)); Set(m, "_GlitchBrightness", 2.2f);
            });
            AddCase(output, "2d.frozen", "01_2D/09_Status_冰冻.mat", Shader2D, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableFrozen", 1f); Set(m, "_FrozenFade", 0.9f); Set(m, "_FrozenColor", Hdr(0.12f, 1.1f, 2.4f));
            });
            AddCase(output, "2d.burn", "01_2D/10_Status_燃烧.mat", Shader2D, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableBurn", 1f); Set(m, "_BurnFade", 0.82f); Set(m, "_BurnEdgeColor", Hdr(4f, 0.35f, 0.02f));
                Set(m, "_BurnInsideColor", new Color(0.18f, 0.015f, 0.005f, 1f));
            });
            AddCase(output, "2d.poison", "01_2D/11_Status_中毒.mat", Shader2D, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnablePoison", 1f); Set(m, "_PoisonFade", 0.86f); Set(m, "_PoisonColor", Hdr(0.25f, 2.2f, 0.08f));
            });
            AddCase(output, "2d.camouflage", "01_2D/12_Style_迷彩.mat", Shader2D, t, ESCompositeQualityTier.高质量, true, m => ConfigureCamouflage(m));
            AddCase(output, "2d.metal", "01_2D/13_Style_流动金属.mat", Shader2D, t, ESCompositeQualityTier.高质量, true, m => ConfigureMetal(m));
            AddCase(output, "2d.enchanted", "01_2D/14_Style_附魔流光.mat", Shader2D, t, ESCompositeQualityTier.高质量, true, m => ConfigureEnchanted(m));
            AddCase(output, "2d.motion_squish", "01_2D/15_Motion_挤压与摆动.mat", Shader2D, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableSquish", 1f); Set(m, "_SquishAmount", 0.16f); Set(m, "_SquishDirection", new Vector4(0f, 1f, 0f, 0f));
                Set(m, "_SquishSpeed", 2f); Set(m, "_EnableWiggle", 1f); Set(m, "_WiggleAmplitude", 0.035f);
                Set(m, "_WiggleDirection", new Vector4(1f, 0f, 0f, 0f)); Set(m, "_WiggleSpeed", 2.4f);
            });
            AddCase(output, "2d.distortion_chromatic", "01_2D/16_Distortion_色差扰动.mat", Shader2D, t, ESCompositeQualityTier.高质量, false, m =>
            {
                Set(m, "_EnableUVDistort", 1f); Set(m, "_UVDistortAmount", 0.045f); Set(m, "_UVDistortSpeed", new Vector4(0.12f, 0.18f, 0f, 0f));
                Set(m, "_EnableChromatic", 1f); Set(m, "_ChromaticOffset", 0.006f); Set(m, "_ChromaticIntensity", 0.85f);
            });
        }

        private static void CreateUIMaterials(GeneratedTextures t, GeneratedMaterials output)
        {
            AddCase(output, "ui.base", "02_UI/01_Base_普通卡片.mat", ShaderUI, t, ESCompositeQualityTier.基础, false, null);
            AddCase(output, "ui.shine_button", "02_UI/02_Shine_按钮反馈.mat", ShaderUI, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableShine", 1f); Set(m, "_ShineDirection", new Vector4(0.8f, 0.6f, 0f, 0f));
                Set(m, "_ShineColor", Hdr(2.4f, 1.8f, 0.6f)); Set(m, "_ShineSpeed", 0.85f); Set(m, "_ShineWidth", 0.14f);
            });
            AddCase(output, "ui.hologram", "02_UI/03_Hologram_科技面板.mat", ShaderUI, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableHologram", 1f); Set(m, "_HologramColor", Hdr(0.04f, 1.2f, 2.7f)); Set(m, "_HologramSpace", 0f);
                Set(m, "_HologramLineFrequency", 64f); Set(m, "_HologramSpeed", 1.3f);
            });
            AddCase(output, "ui.glitch", "02_UI/04_Glitch_警告面板.mat", ShaderUI, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableGlitch", 1f); Set(m, "_GlitchFade", 0.7f); Set(m, "_GlitchBrightness", 2f);
                Set(m, "_GlitchDistortion", new Vector4(0.04f, 0f, 0f, 0f));
            });
            AddCase(output, "ui.enchanted", "02_UI/05_Enchanted_稀有卡牌.mat", ShaderUI, t, ESCompositeQualityTier.高质量, true, m => ConfigureEnchanted(m));
            AddCase(output, "ui.shifting", "02_UI/06_Shifting_传奇品质.mat", ShaderUI, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableShifting", 1f); Set(m, "_ShiftingFade", 0.78f); Set(m, "_ShiftingSpeed", 0.38f);
                Set(m, "_ShiftingDensity", 1.5f); Set(m, "_ShiftingRainbowToggle", 1f); Set(m, "_ShiftingSaturation", 0.88f);
            });
            AddCase(output, "ui.sine_glow", "02_UI/07_SineGlow_可交互提示.mat", ShaderUI, t, ESCompositeQualityTier.标准, true, m =>
            {
                Set(m, "_EnableSineGlow", 1f); Set(m, "_SineGlowFade", 0.8f); Set(m, "_SineGlowColor", Hdr(0.05f, 1.5f, 3f));
                Set(m, "_SineGlowFrequency", 3.2f); Set(m, "_SineGlowMax", 0.75f);
            });
            AddCase(output, "ui.pixelate", "02_UI/08_Pixelate_冷却锁定.mat", ShaderUI, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnablePixelate", 1f); Set(m, "_PixelateCells", 22f); Set(m, "_PixelateStrength", 1f);
                Set(m, "_EnableSaturation", 1f); Set(m, "_Saturation", 0.15f);
            });
            AddCase(output, "ui.dissolve", "02_UI/09_Dissolve_面板揭示.mat", ShaderUI, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_FadeMode", 4f); Set(m, "_FadeProgress", 0.54f); Set(m, "_FadeRotation", 90f);
                Set(m, "_DissolveEdgeColor", Hdr(0.2f, 2.4f, 2.8f)); Set(m, "_FadeWidth", 0.1f);
            });
            AddCase(output, "ui.recolor_outline", "02_UI/10_Recolor_主题换肤描边.mat", ShaderUI, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableRecolorRGB", 1f); Set(m, "_RecolorRed", Hdr(0.2f, 1.2f, 2.2f));
                Set(m, "_RecolorGreen", Hdr(1.8f, 0.25f, 0.75f)); Set(m, "_RecolorBlue", Hdr(2.1f, 1.5f, 0.2f));
                Set(m, "_EnablePixelOutline", 1f); Set(m, "_PixelOutlineColor", Color.white); Set(m, "_PixelOutlineWidth", 2f);
            });
        }

        private static void CreateLitMaterials(GeneratedTextures t, GeneratedMaterials output)
        {
            AddCase(output, "lit.base", "03_3D_Lit/01_Base_标准受光.mat", ShaderLit, t, ESCompositeQualityTier.基础, false, null);
            AddCase(output, "lit.rim", "03_3D_Lit/02_Rim_角色边缘光.mat", ShaderLit, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableRim", 1f); Set(m, "_RimColor", Hdr(0.08f, 1.2f, 2.8f)); Set(m, "_RimPower", 2.2f); Set(m, "_RimIntensity", 1.4f);
            });
            AddCase(output, "lit.shine", "03_3D_Lit/03_Shine_拾取高亮.mat", ShaderLit, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableShine", 1f); Set(m, "_ShineDirection", new Vector4(0.7f, 0.7f, 0f, 0f));
                Set(m, "_ShineColor", Hdr(2.5f, 1.7f, 0.45f)); Set(m, "_ShineIntensity", 1.4f);
            });
            AddCase(output, "lit.dissolve", "03_3D_Lit/04_Dissolve_生成消散.mat", ShaderLit, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_DissolveMode", 1f); Set(m, "_DissolveProgress", 0.48f); Set(m, "_DissolveSoftness", 0.06f);
                Set(m, "_DissolveEdgeColor", Hdr(3.2f, 0.28f, 0.02f)); Set(m, "_DissolveEdgeWidth", 0.09f);
            });
            AddCase(output, "lit.hologram", "03_3D_Lit/05_Hologram_投影替身.mat", ShaderLit, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableHologram", 1f); Set(m, "_HologramColor", Hdr(0.04f, 1.3f, 2.8f)); Set(m, "_HologramSpace", 1f);
                Set(m, "_HologramLineFrequency", 72f); Set(m, "_HologramSpeed", 1.2f);
            });
            AddCase(output, "lit.glitch", "03_3D_Lit/06_Glitch_受损投影.mat", ShaderLit, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableGlitch", 1f); Set(m, "_GlitchFade", 0.68f); Set(m, "_GlitchBrightness", 2.1f);
                Set(m, "_GlitchDistortion", new Vector4(0.04f, 0f, 0f, 0f));
            });
            AddCase(output, "lit.frozen", "03_3D_Lit/07_Status_冰冻角色.mat", ShaderLit, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableFrozen", 1f); Set(m, "_FrozenFade", 0.9f); Set(m, "_FrozenColor", Hdr(0.08f, 1.1f, 2.4f));
            });
            AddCase(output, "lit.burn", "03_3D_Lit/08_Status_燃烧角色.mat", ShaderLit, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableBurn", 1f); Set(m, "_BurnFade", 0.84f); Set(m, "_BurnEdgeColor", Hdr(3.8f, 0.32f, 0.015f));
                Set(m, "_BurnInsideColor", new Color(0.15f, 0.01f, 0.005f, 1f));
            });
            AddCase(output, "lit.camouflage", "03_3D_Lit/09_Style_潜行迷彩.mat", ShaderLit, t, ESCompositeQualityTier.高质量, true, m => ConfigureCamouflage(m));
            AddCase(output, "lit.metal_enchanted", "03_3D_Lit/10_Style_附魔金属.mat", ShaderLit, t, ESCompositeQualityTier.高质量, true, m =>
            {
                ConfigureMetal(m); ConfigureEnchanted(m); Set(m, "_EnchantedFade", 0.48f);
            });
        }

        private static void CreateVfxMaterials(GeneratedTextures t, GeneratedMaterials output)
        {
            AddCase(output, "vfx.base", "04_3D_VFX/00_Base_无效果.mat", ShaderVfx, t, ESCompositeQualityTier.基础, false, null);
            AddCase(output, "vfx.sequence", "04_3D_VFX/01_Sequence_4x4爆发.mat", ShaderVfx, t, ESCompositeQualityTier.基础, false, m =>
            {
                Set(m, "_MainTex", t.Sequence); Set(m, "_EnableSequence", 1f); Set(m, "_SequencePlayback", 1f);
                Set(m, "_SequenceColumns", 4f); Set(m, "_SequenceRows", 4f); Set(m, "_SequenceSpeed", 8f);
            });
            AddCase(output, "vfx.polar", "04_3D_VFX/02_PolarUV_旋涡门户.mat", ShaderVfx, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnablePolarUV", 1f); Set(m, "_PolarCenter", new Vector4(0.5f, 0.5f, 0f, 0f));
                Set(m, "_PolarRadialScale", 1.4f); Set(m, "_PolarAngularScale", 2.2f); Set(m, "_PolarRotationSpeed", 0.45f);
            });
            AddCase(output, "vfx.vertex_animation", "04_3D_VFX/03_Vertex_动画与Custom流.mat", ShaderVfx, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableVertexStreams", 1f); Set(m, "_VertexStreamUVStrength", 1f);
                Set(m, "_VertexStreamFrameStrength", 1f); Set(m, "_VertexStreamDissolveStrength", 1f); Set(m, "_VertexStreamEmissionStrength", 1f);
                Set(m, "_EnableVertexAnimation", 1f); Set(m, "_VertexAnimationDirection", new Vector4(1f, 0.2f, 0f, 0f));
                Set(m, "_VertexAnimationAmplitude", 0.18f); Set(m, "_VertexAnimationFrequency", 3f); Set(m, "_VertexAnimationSpeed", 1.6f);
            });
            AddCase(output, "vfx.flow", "04_3D_VFX/04_Flow_能量流.mat", ShaderVfx, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableFlow", 1f); Set(m, "_FlowSpeed", new Vector4(0.8f, 0.1f, 0f, 0f)); Set(m, "_FlowStrength", 1.2f);
                Set(m, "_EnableFlowMap", 1f); Set(m, "_FlowMap", t.Flow); Set(m, "_FlowMapStrength", 0.75f);
            });
            AddCase(output, "vfx.shine", "04_3D_VFX/05_Shine_技能轨迹.mat", ShaderVfx, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableShine", 1f); Set(m, "_ShineDirection", new Vector4(1f, 0f, 0f, 0f));
                Set(m, "_ShineColor", Hdr(0.3f, 1.6f, 3.2f)); Set(m, "_ShineSpeed", 1.2f); Set(m, "_ShineWidth", 0.12f);
            });
            AddCase(output, "vfx.radial", "04_3D_VFX/06_RadialMask_范围预警.mat", ShaderVfx, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableRadialMask", 1f); Set(m, "_RadialMaskCenter", new Vector4(0.5f, 0.5f, 0f, 0f));
                Set(m, "_RadialMaskRadius", 0.42f); Set(m, "_RadialMaskSoftness", 0.08f);
            });
            AddCase(output, "vfx.dissolve", "04_3D_VFX/07_Dissolve_传送门消散.mat", ShaderVfx, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_DissolveMode", 2f); Set(m, "_DissolveProgress", 0.52f); Set(m, "_DissolveWidth", 0.09f);
                Set(m, "_DissolveColor", Hdr(2.8f, 0.24f, 0.03f));
            });
            AddCase(output, "vfx.hologram", "04_3D_VFX/08_Hologram_数字替身.mat", ShaderVfx, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableHologram", 1f); Set(m, "_HologramColor", Hdr(0.04f, 1.4f, 3f));
                Set(m, "_HologramDirection", new Vector4(0f, 1f, 0f, 0f)); Set(m, "_HologramLineFrequency", 72f);
            });
            AddCase(output, "vfx.glitch", "04_3D_VFX/09_Glitch_信号中断.mat", ShaderVfx, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableGlitch", 1f); Set(m, "_GlitchScanDirection", new Vector4(0f, 1f, 0f, 0f));
                Set(m, "_GlitchFade", 0.72f); Set(m, "_GlitchDistortion", new Vector4(0.05f, 0f, 0f, 0f));
            });
            AddCase(output, "vfx.depth", "04_3D_VFX/10_Depth_接触高亮.mat", ShaderVfx, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableDepthIntersection", 1f); Set(m, "_DepthIntersectionColor", Hdr(0.1f, 1.8f, 3.2f));
                Set(m, "_DepthIntersectionDistance", 0.7f); Set(m, "_DepthIntersectionIntensity", 2f);
                Set(m, "_EnableSoftParticles", 1f); Set(m, "_SoftParticleNear", 0f); Set(m, "_SoftParticleFar", 1.2f);
            });
        }

        private static void CreateProductionMaterials(GeneratedTextures t, GeneratedMaterials output)
        {
            AddCase(output, "prod.basic", "05_ProductionRecipes/01_Quality_Basic.mat", Shader2D, t, ESCompositeQualityTier.基础, false, m => ConfigureQualityShowcase(m));
            AddCase(output, "prod.standard", "05_ProductionRecipes/02_Quality_Standard.mat", Shader2D, t, ESCompositeQualityTier.标准, false, m => ConfigureQualityShowcase(m));
            AddCase(output, "prod.high", "05_ProductionRecipes/03_Quality_HighExact.mat", Shader2D, t, ESCompositeQualityTier.高质量, true, m => ConfigureQualityShowcase(m));
            AddCase(output, "prod.dir_right", "05_ProductionRecipes/04_Direction_水平向右.mat", Shader2D, t, ESCompositeQualityTier.标准, false, m => ConfigureDirectionalShine(m, new Vector4(1f, 0f, 0f, 0f), Hdr(2.6f, 0.5f, 0.1f)));
            AddCase(output, "prod.dir_up", "05_ProductionRecipes/05_Direction_垂直向上.mat", Shader2D, t, ESCompositeQualityTier.标准, false, m => ConfigureDirectionalShine(m, new Vector4(0f, 1f, 0f, 0f), Hdr(0.1f, 2.2f, 1.1f)));
            AddCase(output, "prod.dir_diag", "05_ProductionRecipes/06_Direction_斜向右上.mat", Shader2D, t, ESCompositeQualityTier.标准, false, m => ConfigureDirectionalShine(m, new Vector4(0.707f, 0.707f, 0f, 0f), Hdr(0.2f, 1.2f, 3f)));
            AddCase(output, "prod.order_color_uv", "05_ProductionRecipes/07_Order_UV后颜色.mat", Shader2D, t, ESCompositeQualityTier.高质量, false, m =>
            {
                Set(m, "_EnableUVDistort", 1f); Set(m, "_UVDistortAmount", 0.035f); Set(m, "_UVDistortSpeed", new Vector4(0.15f, 0.08f, 0f, 0f));
                Set(m, "_EnableSplitToning", 1f); Set(m, "_SplitToneShadows", new Color(0.08f, 0.2f, 0.65f, 1f));
                Set(m, "_SplitToneHighlights", Hdr(1.9f, 0.38f, 0.08f)); Set(m, "_SplitToneStrength", 0.85f);
            });
            AddCase(output, "prod.order_fade_status", "05_ProductionRecipes/08_Order_溶解后状态.mat", Shader2D, t, ESCompositeQualityTier.高质量, true, m =>
            {
                Set(m, "_EnableFrozen", 1f); Set(m, "_FrozenFade", 0.8f); Set(m, "_FadeMode", 4f);
                Set(m, "_FadeProgress", 0.5f); Set(m, "_FadeRotation", -35f); Set(m, "_DissolveEdgeColor", Hdr(2.8f, 0.35f, 0.02f));
            });
            AddCase(output, "prod.mpb_a", "05_ProductionRecipes/09_MPB_共享材质A.mat", Shader2D, t, ESCompositeQualityTier.标准, false, m => ConfigureDirectionalShine(m, new Vector4(1f, 0f, 0f, 0f), Hdr(0.15f, 1.7f, 3f)));
            AddCase(output, "prod.mpb_b", "05_ProductionRecipes/10_MPB_共享材质B.mat", ShaderLit, t, ESCompositeQualityTier.标准, false, m =>
            {
                Set(m, "_EnableRim", 1f); Set(m, "_RimColor", Hdr(1.8f, 0.15f, 0.8f)); Set(m, "_RimPower", 2f); Set(m, "_RimIntensity", 1.2f);
            });
        }

        private static void CreateEnvironmentMaterials(GeneratedTextures t, GeneratedMaterials output)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("找不到 URP Lit，无法创建独立测试环境材质。");

            Material dark = CreateOrResetMaterial(MaterialRoot + "/90_Environment/DarkFloor.mat", shader.name);
            if (dark.HasProperty("_BaseColor")) dark.SetColor("_BaseColor", new Color(0.055f, 0.065f, 0.085f, 1f));
            if (dark.HasProperty("_Smoothness")) dark.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(dark); output.Add("env.dark", dark);

            Material neutral = CreateOrResetMaterial(MaterialRoot + "/90_Environment/NeutralReference.mat", shader.name);
            if (neutral.HasProperty("_BaseColor")) neutral.SetColor("_BaseColor", new Color(0.32f, 0.34f, 0.38f, 1f));
            if (neutral.HasProperty("_Smoothness")) neutral.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(neutral); output.Add("env.neutral", neutral);
        }

        private static void AddCase(
            GeneratedMaterials output,
            string id,
            string relativePath,
            string shaderName,
            GeneratedTextures textures,
            ESCompositeQualityTier quality,
            bool exactContract,
            Action<Material> configure)
        {
            Material material = CreateOrResetMaterial(MaterialRoot + "/" + relativePath, shaderName);
            ConfigureBaseMaterial(material, textures, quality, exactContract);
            configure?.Invoke(material);
            EditorUtility.SetDirty(material);
            output.Add(id, material);
        }

        private static void ConfigureBaseMaterial(
            Material material,
            GeneratedTextures textures,
            ESCompositeQualityTier quality,
            bool exactContract)
        {
            SetIfPresent(material, "_MainTex", textures.Icon);
            SetIfPresent(material, "_BaseMap", textures.Icon);
            SetIfPresent(material, "_NoiseTex", textures.Noise);
            SetIfPresent(material, "_FadeNoiseTex", textures.Noise);
            SetIfPresent(material, "_UVDistortNoiseTex", textures.Noise);
            SetIfPresent(material, "_UberNoiseTexture", textures.Noise);
            SetIfPresent(material, "_FlowMap", textures.Flow);

            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Hdr(0.12f, 0.28f, 0.55f));

            switch (material.shader.name)
            {
                case Shader2D:
                    ES2DCompositeURPProperties.SetQuality(material, quality);
                    if (exactContract) ES2DCompositeURPProperties.SetSSUExactContract(material, true, textures.Noise);
                    break;
                case ShaderUI:
                    ESUICompositeURPProperties.SetQuality(material, quality);
                    if (exactContract) ESUICompositeURPProperties.SetSSUExactContract(material, true, textures.Noise);
                    break;
                case ShaderLit:
                    ES3DLitCompositeURPProperties.SetQuality(material, quality);
                    if (exactContract) ES3DLitCompositeURPProperties.SetSSUExactContract(material, true, textures.Noise);
                    break;
                case ShaderVfx:
                    ES3DVFXCompositeURPProperties.SetQuality(material, quality);
                    if (exactContract) ES3DVFXCompositeURPProperties.SetSSUExactContract(material, true);
                    break;
                default:
                    throw new InvalidOperationException("未注册的 Composite Shader：" + material.shader.name);
            }
        }

        private static void ConfigureCamouflage(Material m)
        {
            Set(m, "_EnableCamouflage", 1f); Set(m, "_CamouflageFade", 0.9f);
            Set(m, "_CamouflageBaseColor", new Color(0.34f, 0.42f, 0.22f, 1f));
            Set(m, "_CamouflageColorA", new Color(0.12f, 0.2f, 0.08f, 1f)); Set(m, "_CamouflageDensityA", 0.44f);
            Set(m, "_CamouflageColorB", new Color(0.58f, 0.5f, 0.22f, 1f)); Set(m, "_CamouflageDensityB", 0.56f);
        }

        private static void ConfigureMetal(Material m)
        {
            Set(m, "_EnableMetal", 1f); Set(m, "_MetalFade", 0.86f); Set(m, "_MetalColor", Hdr(1.4f, 0.56f, 0.08f));
            Set(m, "_MetalHighlightColor", Hdr(3.2f, 2f, 0.4f)); Set(m, "_MetalHighlightDensity", 0.7f);
            Set(m, "_MetalNoiseSpeed", new Vector4(0.04f, 0.09f, 0f, 0f));
        }

        private static void ConfigureEnchanted(Material m)
        {
            Set(m, "_EnableEnchanted", 1f); Set(m, "_EnchantedFade", 0.75f); Set(m, "_EnchantedSpeed", new Vector4(0.15f, 0.75f, 0f, 0f));
            Set(m, "_EnchantedScale", new Vector4(0.18f, 0.18f, 0f, 0f)); Set(m, "_EnchantedBrightness", 1.3f);
            Set(m, "_EnchantedLowColor", Hdr(0.55f, 0.05f, 1.9f)); Set(m, "_EnchantedHighColor", Hdr(0.02f, 1.7f, 2.9f));
        }

        private static void ConfigureQualityShowcase(Material m)
        {
            Set(m, "_EnableHologram", 1f); Set(m, "_HologramColor", Hdr(0.04f, 1.25f, 2.8f)); Set(m, "_HologramSpace", 0f);
            Set(m, "_EnableGlitch", 1f); Set(m, "_GlitchFade", 0.55f); Set(m, "_EnablePixelOutline", 1f);
            Set(m, "_PixelOutlineColor", Color.white); Set(m, "_PixelOutlineWidth", 2f);
        }

        private static void ConfigureDirectionalShine(Material m, Vector4 direction, Color color)
        {
            Set(m, "_EnableShine", 1f); Set(m, "_ShineDirection", direction); Set(m, "_ShineColor", color);
            Set(m, "_ShineSpeed", 0.8f); Set(m, "_ShineWidth", 0.13f); Set(m, "_ShineIntensity", 1.35f);
        }
    }
}
