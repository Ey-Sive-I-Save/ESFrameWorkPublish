using UnityEngine;
using UnityEngine.Rendering;

namespace ES
{
    /// <summary>
    /// ES/2D/Composite URP 的稳定强枚举参数入口。
    /// 数值必须与 Shader 属性中的 Enum 顺序保持一致；业务层不直接散落字符串。
    /// </summary>
    public enum ES2DCompositeAnimationMode
    {
        无 = 0,
        序列帧 = 1
    }

    public enum ES2DCompositeFadeMode
    {
        无 = 0,
        渐隐 = 1,
        遮罩 = 2,
        溶解 = 3
    }

    public enum ES2DCompositeCoordinateMode
    {
        UV = 0,
        世界空间 = 1,
        屏幕空间 = 2
    }

    public enum ES2DCompositeTimeMode
    {
        场景时间 = 0,
        非缩放时间 = 1,
        自定义时间 = 2
    }

    public enum ESCompositeTimeMode
    {
        场景时间 = 0,
        非缩放时间 = 1,
        自定义时间 = 2
    }

    public static class ESCompositeURPProperties
    {
        private const string QualityStandardKeyword = "_ES_QUALITY_STANDARD";
        private const string QualityHighKeyword = "_ES_QUALITY_HIGH";
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");
        public static readonly int UnscaledTime = Shader.PropertyToID("_ESUnscaledTime");
        public static readonly int UnscaledTimeValid = Shader.PropertyToID("_ESUnscaledTimeValid");

        public static void SetTime(MaterialPropertyBlock block, ESCompositeTimeMode mode, float timeScale = 1f, float customTime = 0f)
        {
            if (block == null) return;
            block.SetFloat(TimeMode, (float)mode);
            block.SetFloat(TimeScale, Mathf.Max(0f, timeScale));
            block.SetFloat(CustomTime, customTime);
        }

        public static void SetMainTextureTransform(MaterialPropertyBlock block, Vector2 scale, Vector2 offset)
        {
            if (block == null) return;
            block.SetVector(MainTexScaleOffset, new Vector4(scale.x, scale.y, offset.x, offset.y));
        }

        public static void SetUnscaledTime(float unscaledTime)
        {
            Shader.SetGlobalFloat(UnscaledTime, unscaledTime);
            Shader.SetGlobalFloat(UnscaledTimeValid, 1f);
        }

        internal static void ApplyQuality(Material material, int propertyId, ESCompositeQualityTier quality)
        {
            if (material == null) return;
            int tier = Mathf.Clamp((int)quality, 0, 2);
            material.SetFloat(propertyId, tier);
            SetKeyword(material, QualityStandardKeyword, tier == 1);
            SetKeyword(material, QualityHighKeyword, tier == 2);
        }

        internal static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (material == null) return;
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }
    }

    public enum ES3DCompositeDissolveMode
    {
        无 = 0,
        噪声溶解 = 1,
        距离溶解 = 2
    }

    public enum ES3DVFXDissolveMode
    {
        无 = 0,
        溶解 = 1,
        溶解加边缘光 = 2
    }

    public enum ES3DVFXSequencePlaybackMode
    {
        手动帧 = 0,
        时间播放 = 1,
        顶点流帧号 = 2
    }

    public enum ES3DVFXBlendMode
    {
        透明混合 = 0,
        叠加 = 1,
        预乘透明 = 2,
        正片叠底 = 3
    }

    public enum ES3DVFXDepthWriteMode
    {
        关闭 = 0,
        开启 = 1
    }

    public enum ES3DVFXDepthTestMode
    {
        禁用 = 0,
        从不 = 1,
        小于 = 2,
        等于 = 3,
        小于等于 = 4,
        大于 = 5,
        不等于 = 6,
        大于等于 = 7,
        始终 = 8
    }

    public enum ES3DVFXCullMode
    {
        双面 = 0,
        剔除正面 = 1,
        剔除背面 = 2
    }

    public enum ESUICompositeEffectMode
    {
        无 = 0,
        全息 = 1,
        故障 = 2,
        全息与故障 = 3
    }

    public enum ESCompositeQualityTier
    {
        基础 = 0,
        标准 = 1,
        高质量 = 2
    }

    public enum ESCompositeVertexColorMask
    {
        无 = 0,
        红色通道 = 1,
        绿色通道 = 2,
        蓝色通道 = 3,
        透明通道 = 4
    }

    public static class ES2DCompositeURPProperties
    {
        public const string ShaderName = "ES/2D/Composite URP";
        public const string Lit3DShaderName = "ES/3D/Lit Composite URP";
        public const string Vfx3DShaderName = "ES/3D/VFX Composite URP";
        public static readonly int AnimationMode = Shader.PropertyToID("_AnimationMode");
        public static readonly int FadeMode = Shader.PropertyToID("_FadeMode");
        public static readonly int FadeProgress = Shader.PropertyToID("_FadeProgress");
        public static readonly int CoordinateMode = Shader.PropertyToID("_CoordinateMode");
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");
        public static readonly int SequenceColumns = Shader.PropertyToID("_SequenceColumns");
        public static readonly int SequenceRows = Shader.PropertyToID("_SequenceRows");
        public static readonly int SequenceFrame = Shader.PropertyToID("_SequenceFrame");
        public static readonly int SequenceSpeed = Shader.PropertyToID("_SequenceSpeed");
        public static readonly int GlowIntensity = Shader.PropertyToID("_GlowIntensity");
        public static readonly int ShineEnabled = Shader.PropertyToID("_EnableShine");
        public static readonly int ShineColor = Shader.PropertyToID("_ShineColor");
        public static readonly int ShineSpeed = Shader.PropertyToID("_ShineSpeed");
        public static readonly int ShineWidth = Shader.PropertyToID("_ShineWidth");
        public static readonly int ShineAngle = Shader.PropertyToID("_ShineAngle");
        public static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
        public static readonly int SparkleEnabled = Shader.PropertyToID("_EnableSparkle");
        public static readonly int SparkleColor = Shader.PropertyToID("_SparkleColor");
        public static readonly int SparkleScale = Shader.PropertyToID("_SparkleScale");
        public static readonly int SparkleSpeed = Shader.PropertyToID("_SparkleSpeed");
        public static readonly int SparkleDensity = Shader.PropertyToID("_SparkleDensity");
        public static readonly int SparkleSharpness = Shader.PropertyToID("_SparkleSharpness");
        public static readonly int SparkleIntensity = Shader.PropertyToID("_SparkleIntensity");
        public static readonly int FlowEnabled = Shader.PropertyToID("_EnableFlow");
        public static readonly int FlowSpeed = Shader.PropertyToID("_FlowSpeed");
        public static readonly int FlowStrength = Shader.PropertyToID("_FlowStrength");
        public static readonly int ChromaticEnabled = Shader.PropertyToID("_EnableChromatic");
        public static readonly int ChromaticOffset = Shader.PropertyToID("_ChromaticOffset");
        public static readonly int ChromaticIntensity = Shader.PropertyToID("_ChromaticIntensity");
        public static readonly int ChromaticEdgeOnly = Shader.PropertyToID("_ChromaticEdgeOnly");
        public static readonly int ChromaticAngle = Shader.PropertyToID("_ChromaticAngle");
        public static readonly int BlurEnabled = Shader.PropertyToID("_EnableBlur");
        public static readonly int BlurRadius = Shader.PropertyToID("_BlurRadius");
        public static readonly int BlurIntensity = Shader.PropertyToID("_BlurIntensity");

        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int VertexColorStrength = Shader.PropertyToID("_VertexColorStrength");
        public static readonly int DistortionStrength = Shader.PropertyToID("_DistortionStrength");
        public static readonly int HologramEnabled = Shader.PropertyToID("_EnableHologram");
        public static readonly int GlitchEnabled = Shader.PropertyToID("_EnableGlitch");
        public static readonly int BurnEnabled = Shader.PropertyToID("_EnableBurn");
        public static readonly int PoisonEnabled = Shader.PropertyToID("_EnablePoison");
        public static readonly int FrozenEnabled = Shader.PropertyToID("_EnableFrozen");

        public static void SetMotionEffects(MaterialPropertyBlock block, bool sparkle, bool flow, bool chromatic, Vector2 flowSpeed, float flowStrength, float sparkleIntensity, float chromaticOffset, float chromaticIntensity)
        {
            if (block == null) return;
            block.SetFloat(SparkleEnabled, sparkle ? 1f : 0f);
            block.SetFloat(FlowEnabled, flow ? 1f : 0f);
            block.SetVector(FlowSpeed, new Vector4(flowSpeed.x, flowSpeed.y, 0f, 0f));
            block.SetFloat(FlowStrength, Mathf.Clamp01(flowStrength));
            block.SetFloat(SparkleIntensity, Mathf.Max(0f, sparkleIntensity));
            block.SetFloat(ChromaticEnabled, chromatic ? 1f : 0f);
            block.SetFloat(ChromaticOffset, Mathf.Max(0f, chromaticOffset));
            block.SetFloat(ChromaticIntensity, Mathf.Clamp01(chromaticIntensity));
        }

        public static void SetModes(MaterialPropertyBlock block,
            ES2DCompositeAnimationMode animationMode,
            ES2DCompositeFadeMode fadeMode,
            ES2DCompositeCoordinateMode coordinateMode,
            ES2DCompositeTimeMode timeMode)
        {
            if (block == null) return;
            block.SetFloat(AnimationMode, (float)animationMode);
            block.SetFloat(FadeMode, (float)fadeMode);
            block.SetFloat(CoordinateMode, (float)coordinateMode);
            block.SetFloat(TimeMode, (float)timeMode);
        }

        public static void SetFade(MaterialPropertyBlock block, float progress, float width)
        {
            if (block == null) return;
            block.SetFloat(FadeProgress, progress);
            block.SetFloat(Shader.PropertyToID("_FadeWidth"), width);
        }

        public static void SetEffectToggles(MaterialPropertyBlock block, bool hologram, bool glitch, bool frozen, bool burn, bool poison)
        {
            if (block == null) return;
            block.SetFloat(HologramEnabled, hologram ? 1f : 0f);
            block.SetFloat(GlitchEnabled, glitch ? 1f : 0f);
            block.SetFloat(FrozenEnabled, frozen ? 1f : 0f);
            block.SetFloat(BurnEnabled, burn ? 1f : 0f);
            block.SetFloat(PoisonEnabled, poison ? 1f : 0f);
        }
    }

    public static class ES3DLitCompositeURPProperties
    {
        public const string ShaderName = "ES/3D/Lit Composite URP";
        public static readonly int DissolveMode = Shader.PropertyToID("_DissolveMode");
        public static readonly int DissolveProgress = Shader.PropertyToID("_DissolveProgress");
        public static readonly int RimIntensity = Shader.PropertyToID("_RimIntensity");
        public static readonly int ShineEnabled = Shader.PropertyToID("_EnableShine");
        public static readonly int ShineColor = Shader.PropertyToID("_ShineColor");
        public static readonly int ShineSpeed = Shader.PropertyToID("_ShineSpeed");
        public static readonly int ShineWidth = Shader.PropertyToID("_ShineWidth");
        public static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
        public static readonly int SparkleEnabled = Shader.PropertyToID("_EnableSparkle");
        public static readonly int ShineDirection = Shader.PropertyToID("_ShineDirection");
        public static readonly int SparkleColor = Shader.PropertyToID("_SparkleColor");
        public static readonly int SparkleScale = Shader.PropertyToID("_SparkleScale");
        public static readonly int SparkleSpeed = Shader.PropertyToID("_SparkleSpeed");
        public static readonly int SparkleDensity = Shader.PropertyToID("_SparkleDensity");
        public static readonly int SparkleSharpness = Shader.PropertyToID("_SparkleSharpness");
        public static readonly int SparkleIntensity = Shader.PropertyToID("_SparkleIntensity");
        public static readonly int FlowEnabled = Shader.PropertyToID("_EnableFlow");
        public static readonly int FlowSpeed = Shader.PropertyToID("_FlowSpeed");
        public static readonly int FlowStrength = Shader.PropertyToID("_FlowStrength");
        public static readonly int FlowMapEnabled = Shader.PropertyToID("_EnableFlowMap");
        public static readonly int FlowMap = Shader.PropertyToID("_FlowMap");
        public static readonly int FlowMapScale = Shader.PropertyToID("_FlowMapScale");
        public static readonly int FlowMapSpeed = Shader.PropertyToID("_FlowMapSpeed");
        public static readonly int FlowMapStrength = Shader.PropertyToID("_FlowMapStrength");
        public static readonly int VertexAnimationEnabled = Shader.PropertyToID("_EnableVertexAnimation");
        public static readonly int VertexAnimationDirection = Shader.PropertyToID("_VertexAnimationDirection");
        public static readonly int VertexAnimationAmplitude = Shader.PropertyToID("_VertexAnimationAmplitude");
        public static readonly int VertexAnimationFrequency = Shader.PropertyToID("_VertexAnimationFrequency");
        public static readonly int VertexAnimationSpeed = Shader.PropertyToID("_VertexAnimationSpeed");
        public static readonly int VertexAnimationMask = Shader.PropertyToID("_VertexAnimationMask");
        public static readonly int ChromaticEnabled = Shader.PropertyToID("_EnableChromatic");
        public static readonly int ChromaticOffset = Shader.PropertyToID("_ChromaticOffset");
        public static readonly int ChromaticIntensity = Shader.PropertyToID("_ChromaticIntensity");
        public static readonly int ChromaticEdgeOnly = Shader.PropertyToID("_ChromaticEdgeOnly");
        public static readonly int ChromaticAngle = Shader.PropertyToID("_ChromaticAngle");
        public static readonly int BlurEnabled = Shader.PropertyToID("_EnableBlur");
        public static readonly int BlurRadius = Shader.PropertyToID("_BlurRadius");
        public static readonly int BlurIntensity = Shader.PropertyToID("_BlurIntensity");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int NormalMapEnabled = Shader.PropertyToID("_UseNormalMap");
        public static readonly int Metallic = Shader.PropertyToID("_Metallic");
        public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        public static readonly int Occlusion = Shader.PropertyToID("_Occlusion");
        public static readonly int OcclusionMapEnabled = Shader.PropertyToID("_UseOcclusionMap");
        public static readonly int EmissionEnabled = Shader.PropertyToID("_UseEmission");
        public static readonly int RimEnabled = Shader.PropertyToID("_EnableRim");
        public static readonly int BurnEnabled = Shader.PropertyToID("_EnableBurn");
        public static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        public static readonly int ReceiveShadows = Shader.PropertyToID("_ReceiveShadows");
        public static readonly int QualityTier = Shader.PropertyToID("_QualityTier");
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");

        public static void SetDissolve(MaterialPropertyBlock block, ES3DCompositeDissolveMode mode, float progress)
        {
            if (block == null) return;
            block.SetFloat(DissolveMode, (float)mode);
            block.SetFloat(DissolveProgress, progress);
        }

        public static void SetEffects(MaterialPropertyBlock block, bool rim, bool burn, float rimIntensity, float shineIntensity)
        {
            if (block == null) return;
            block.SetFloat(RimEnabled, rim ? 1f : 0f);
            block.SetFloat(BurnEnabled, burn ? 1f : 0f);
            block.SetFloat(RimIntensity, rimIntensity);
            block.SetFloat(ShineIntensity, shineIntensity);
        }

        public static void SetMotionEffects(MaterialPropertyBlock block, bool sparkle, bool flow, bool chromatic, Vector2 flowSpeed, float flowStrength, float sparkleIntensity, float chromaticOffset, float chromaticIntensity)
        {
            if (block == null) return;
            block.SetFloat(SparkleEnabled, sparkle ? 1f : 0f);
            block.SetFloat(FlowEnabled, flow ? 1f : 0f);
            block.SetVector(FlowSpeed, new Vector4(flowSpeed.x, flowSpeed.y, 0f, 0f));
            block.SetFloat(FlowStrength, Mathf.Clamp01(flowStrength));
            block.SetFloat(SparkleIntensity, Mathf.Max(0f, sparkleIntensity));
            block.SetFloat(ChromaticEnabled, chromatic ? 1f : 0f);
            block.SetFloat(ChromaticOffset, Mathf.Max(0f, chromaticOffset));
            block.SetFloat(ChromaticIntensity, Mathf.Clamp01(chromaticIntensity));
        }

        public static void SetFlowMap(MaterialPropertyBlock block, bool enabled, Texture texture, Vector2 scale, Vector2 offset, Vector2 speed, float strength)
        {
            if (block == null) return;
            block.SetFloat(FlowMapEnabled, enabled ? 1f : 0f);
            block.SetTexture(FlowMap, texture);
            block.SetVector(FlowMapScale, new Vector4(scale.x, scale.y, offset.x, offset.y));
            block.SetVector(FlowMapSpeed, new Vector4(speed.x, speed.y, 0f, 0f));
            block.SetFloat(FlowMapStrength, Mathf.Max(0f, strength));
        }

        public static void SetVertexAnimation(MaterialPropertyBlock block, bool enabled, Vector3 localDirection, float amplitude, float frequency, float speed, ESCompositeVertexColorMask mask)
        {
            if (block == null) return;
            block.SetFloat(VertexAnimationEnabled, enabled ? 1f : 0f);
            block.SetVector(VertexAnimationDirection, new Vector4(localDirection.x, localDirection.y, localDirection.z, 0f));
            block.SetFloat(VertexAnimationAmplitude, Mathf.Max(0f, amplitude));
            block.SetFloat(VertexAnimationFrequency, Mathf.Max(0f, frequency));
            block.SetFloat(VertexAnimationSpeed, speed);
            block.SetFloat(VertexAnimationMask, (float)mask);
        }

        public static void SetSurfaceFeatures(MaterialPropertyBlock block, bool normalMap, bool occlusionMap, bool emission)
        {
            if (block == null) return;
            block.SetFloat(NormalMapEnabled, normalMap ? 1f : 0f);
            block.SetFloat(OcclusionMapEnabled, occlusionMap ? 1f : 0f);
            block.SetFloat(EmissionEnabled, emission ? 1f : 0f);
        }

        public static void SetQuality(Material material, ESCompositeQualityTier quality)
        {
            ESCompositeURPProperties.ApplyQuality(material, QualityTier, quality);
        }

        public static void SetReceiveShadows(Material material, bool receiveShadows)
        {
            if (material == null) return;
            material.SetFloat(ReceiveShadows, receiveShadows ? 1f : 0f);
            ESCompositeURPProperties.SetKeyword(material, "_RECEIVE_SHADOWS_OFF", !receiveShadows);
        }
    }

    public static class ES3DVFXCompositeURPProperties
    {
        public const string ShaderName = "ES/3D/VFX Composite URP";
        private const int TransparentQueue = (int)RenderQueue.Transparent;
        public static readonly int SequenceEnabled = Shader.PropertyToID("_EnableSequence");
        public static readonly int SequencePlayback = Shader.PropertyToID("_SequencePlayback");
        public static readonly int SequenceColumns = Shader.PropertyToID("_SequenceColumns");
        public static readonly int SequenceRows = Shader.PropertyToID("_SequenceRows");
        public static readonly int SequenceFrame = Shader.PropertyToID("_SequenceFrame");
        public static readonly int SequenceSpeed = Shader.PropertyToID("_SequenceSpeed");
        public static readonly int PolarUVEnabled = Shader.PropertyToID("_EnablePolarUV");
        public static readonly int PolarCenter = Shader.PropertyToID("_PolarCenter");
        public static readonly int PolarRadialScale = Shader.PropertyToID("_PolarRadialScale");
        public static readonly int PolarAngularScale = Shader.PropertyToID("_PolarAngularScale");
        public static readonly int PolarRotationSpeed = Shader.PropertyToID("_PolarRotationSpeed");
        public static readonly int VertexStreamsEnabled = Shader.PropertyToID("_EnableVertexStreams");
        public static readonly int VertexStreamUVStrength = Shader.PropertyToID("_VertexStreamUVStrength");
        public static readonly int VertexStreamFrameStrength = Shader.PropertyToID("_VertexStreamFrameStrength");
        public static readonly int VertexStreamDissolveStrength = Shader.PropertyToID("_VertexStreamDissolveStrength");
        public static readonly int VertexStreamEmissionStrength = Shader.PropertyToID("_VertexStreamEmissionStrength");
        public static readonly int DissolveMode = Shader.PropertyToID("_DissolveMode");
        public static readonly int DissolveProgress = Shader.PropertyToID("_DissolveProgress");
        public static readonly int HologramEnabled = Shader.PropertyToID("_EnableHologram");
        public static readonly int GlitchEnabled = Shader.PropertyToID("_EnableGlitch");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int VertexColorStrength = Shader.PropertyToID("_VertexColorStrength");
        public static readonly int Distortion = Shader.PropertyToID("_Distortion");
        public static readonly int SparkleEnabled = Shader.PropertyToID("_EnableSparkle");
        public static readonly int ShineEnabled = Shader.PropertyToID("_EnableShine");
        public static readonly int ShineColor = Shader.PropertyToID("_ShineColor");
        public static readonly int ShineSpeed = Shader.PropertyToID("_ShineSpeed");
        public static readonly int ShineWidth = Shader.PropertyToID("_ShineWidth");
        public static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
        public static readonly int SparkleColor = Shader.PropertyToID("_SparkleColor");
        public static readonly int SparkleScale = Shader.PropertyToID("_SparkleScale");
        public static readonly int SparkleSpeed = Shader.PropertyToID("_SparkleSpeed");
        public static readonly int SparkleDensity = Shader.PropertyToID("_SparkleDensity");
        public static readonly int SparkleSharpness = Shader.PropertyToID("_SparkleSharpness");
        public static readonly int SparkleIntensity = Shader.PropertyToID("_SparkleIntensity");
        public static readonly int FlowEnabled = Shader.PropertyToID("_EnableFlow");
        public static readonly int FlowSpeed = Shader.PropertyToID("_FlowSpeed");
        public static readonly int FlowStrength = Shader.PropertyToID("_FlowStrength");
        public static readonly int FlowMapEnabled = Shader.PropertyToID("_EnableFlowMap");
        public static readonly int FlowMap = Shader.PropertyToID("_FlowMap");
        public static readonly int FlowMapScale = Shader.PropertyToID("_FlowMapScale");
        public static readonly int FlowMapSpeed = Shader.PropertyToID("_FlowMapSpeed");
        public static readonly int FlowMapStrength = Shader.PropertyToID("_FlowMapStrength");
        public static readonly int VertexAnimationEnabled = Shader.PropertyToID("_EnableVertexAnimation");
        public static readonly int VertexAnimationDirection = Shader.PropertyToID("_VertexAnimationDirection");
        public static readonly int VertexAnimationAmplitude = Shader.PropertyToID("_VertexAnimationAmplitude");
        public static readonly int VertexAnimationFrequency = Shader.PropertyToID("_VertexAnimationFrequency");
        public static readonly int VertexAnimationSpeed = Shader.PropertyToID("_VertexAnimationSpeed");
        public static readonly int VertexAnimationMask = Shader.PropertyToID("_VertexAnimationMask");
        public static readonly int SoftParticlesEnabled = Shader.PropertyToID("_EnableSoftParticles");
        public static readonly int SoftParticleNear = Shader.PropertyToID("_SoftParticleNear");
        public static readonly int SoftParticleFar = Shader.PropertyToID("_SoftParticleFar");
        public static readonly int RadialMaskEnabled = Shader.PropertyToID("_EnableRadialMask");
        public static readonly int RadialMaskCenter = Shader.PropertyToID("_RadialMaskCenter");
        public static readonly int RadialMaskRadius = Shader.PropertyToID("_RadialMaskRadius");
        public static readonly int RadialMaskSoftness = Shader.PropertyToID("_RadialMaskSoftness");
        public static readonly int RadialMaskInvert = Shader.PropertyToID("_RadialMaskInvert");
        public static readonly int FresnelMaskEnabled = Shader.PropertyToID("_EnableFresnelMask");
        public static readonly int FresnelPower = Shader.PropertyToID("_FresnelPower");
        public static readonly int FresnelMin = Shader.PropertyToID("_FresnelMin");
        public static readonly int FresnelMax = Shader.PropertyToID("_FresnelMax");
        public static readonly int FresnelAlphaInfluence = Shader.PropertyToID("_FresnelAlphaInfluence");
        public static readonly int FresnelColor = Shader.PropertyToID("_FresnelColor");
        public static readonly int FresnelIntensity = Shader.PropertyToID("_FresnelIntensity");
        public static readonly int DepthIntersectionEnabled = Shader.PropertyToID("_EnableDepthIntersection");
        public static readonly int DepthIntersectionColor = Shader.PropertyToID("_DepthIntersectionColor");
        public static readonly int DepthIntersectionDistance = Shader.PropertyToID("_DepthIntersectionDistance");
        public static readonly int DepthIntersectionIntensity = Shader.PropertyToID("_DepthIntersectionIntensity");
        public static readonly int ChromaticEnabled = Shader.PropertyToID("_EnableChromatic");
        public static readonly int ChromaticOffset = Shader.PropertyToID("_ChromaticOffset");
        public static readonly int ChromaticIntensity = Shader.PropertyToID("_ChromaticIntensity");
        public static readonly int ChromaticEdgeOnly = Shader.PropertyToID("_ChromaticEdgeOnly");
        public static readonly int ChromaticAngle = Shader.PropertyToID("_ChromaticAngle");
        public static readonly int BlurEnabled = Shader.PropertyToID("_EnableBlur");
        public static readonly int BlurRadius = Shader.PropertyToID("_BlurRadius");
        public static readonly int BlurIntensity = Shader.PropertyToID("_BlurIntensity");
        public static readonly int QualityTier = Shader.PropertyToID("_QualityTier");
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");
        public static readonly int BlendMode = Shader.PropertyToID("_BlendMode");
        public static readonly int ZWriteMode = Shader.PropertyToID("_ZWriteMode");
        public static readonly int ZTest = Shader.PropertyToID("_ZTest");
        public static readonly int Cull = Shader.PropertyToID("_Cull");
        public static readonly int QueueOffset = Shader.PropertyToID("_QueueOffset");
        public static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        public static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        public static readonly int BlendOp = Shader.PropertyToID("_BlendOp");

        public static void SetSequence(MaterialPropertyBlock block, bool enabled, ES3DVFXSequencePlaybackMode playback, int columns, int rows, float frame, float speed)
        {
            if (block == null) return;
            block.SetFloat(SequenceEnabled, enabled ? 1f : 0f);
            block.SetFloat(SequencePlayback, (float)playback);
            block.SetFloat(SequenceColumns, Mathf.Max(1, columns));
            block.SetFloat(SequenceRows, Mathf.Max(1, rows));
            block.SetFloat(SequenceFrame, frame);
            block.SetFloat(SequenceSpeed, speed);
        }

        public static void SetPolarUV(MaterialPropertyBlock block, bool enabled, Vector2 center, float radialScale, float angularScale, float rotationSpeed)
        {
            if (block == null) return;
            block.SetFloat(PolarUVEnabled, enabled ? 1f : 0f);
            block.SetVector(PolarCenter, new Vector4(center.x, center.y, 0f, 0f));
            block.SetFloat(PolarRadialScale, radialScale);
            block.SetFloat(PolarAngularScale, angularScale);
            block.SetFloat(PolarRotationSpeed, rotationSpeed);
        }

        public static void SetVertexStreamControls(MaterialPropertyBlock block, bool enabled, float uvStrength, float frameStrength, float dissolveStrength, float emissionStrength)
        {
            if (block == null) return;
            block.SetFloat(VertexStreamsEnabled, enabled ? 1f : 0f);
            block.SetFloat(VertexStreamUVStrength, Mathf.Clamp01(uvStrength));
            block.SetFloat(VertexStreamFrameStrength, Mathf.Clamp01(frameStrength));
            block.SetFloat(VertexStreamDissolveStrength, Mathf.Clamp01(dissolveStrength));
            block.SetFloat(VertexStreamEmissionStrength, Mathf.Max(0f, emissionStrength));
        }

        public static void SetRadialMask(MaterialPropertyBlock block, bool enabled, Vector2 center, float radius, float softness, bool invert)
        {
            if (block == null) return;
            block.SetFloat(RadialMaskEnabled, enabled ? 1f : 0f);
            block.SetVector(RadialMaskCenter, new Vector4(center.x, center.y, 0f, 0f));
            block.SetFloat(RadialMaskRadius, Mathf.Max(0f, radius));
            block.SetFloat(RadialMaskSoftness, Mathf.Max(0.001f, softness));
            block.SetFloat(RadialMaskInvert, invert ? 1f : 0f);
        }

        public static void SetFresnelMask(MaterialPropertyBlock block, bool enabled, float power, Vector2 remap, float alphaInfluence, Color color, float intensity)
        {
            if (block == null) return;
            float minimum = Mathf.Clamp01(Mathf.Min(remap.x, remap.y));
            float maximum = Mathf.Clamp01(Mathf.Max(remap.x, remap.y));
            block.SetFloat(FresnelMaskEnabled, enabled ? 1f : 0f);
            block.SetFloat(FresnelPower, Mathf.Max(0.1f, power));
            block.SetFloat(FresnelMin, minimum);
            block.SetFloat(FresnelMax, Mathf.Max(minimum + 0.0001f, maximum));
            block.SetFloat(FresnelAlphaInfluence, Mathf.Clamp01(alphaInfluence));
            block.SetColor(FresnelColor, color);
            block.SetFloat(FresnelIntensity, Mathf.Max(0f, intensity));
        }

        public static void SetDepthInteraction(MaterialPropertyBlock block, bool softParticles, float nearDistance, float farDistance, bool intersection, Color intersectionColor, float intersectionDistance, float intersectionIntensity)
        {
            if (block == null) return;
            SetSoftParticles(block, softParticles, nearDistance, farDistance);
            block.SetFloat(DepthIntersectionEnabled, intersection ? 1f : 0f);
            block.SetColor(DepthIntersectionColor, intersectionColor);
            block.SetFloat(DepthIntersectionDistance, Mathf.Max(0.001f, intersectionDistance));
            block.SetFloat(DepthIntersectionIntensity, Mathf.Max(0f, intersectionIntensity));
        }

        public static void SetMotionEffects(MaterialPropertyBlock block, bool sparkle, bool flow, bool chromatic, Vector2 flowSpeed, float flowStrength, float sparkleIntensity, float chromaticOffset, float chromaticIntensity)
        {
            if (block == null) return;
            block.SetFloat(SparkleEnabled, sparkle ? 1f : 0f);
            block.SetFloat(FlowEnabled, flow ? 1f : 0f);
            block.SetVector(FlowSpeed, new Vector4(flowSpeed.x, flowSpeed.y, 0f, 0f));
            block.SetFloat(FlowStrength, Mathf.Clamp01(flowStrength));
            block.SetFloat(SparkleIntensity, Mathf.Max(0f, sparkleIntensity));
            block.SetFloat(ChromaticEnabled, chromatic ? 1f : 0f);
            block.SetFloat(ChromaticOffset, Mathf.Max(0f, chromaticOffset));
            block.SetFloat(ChromaticIntensity, Mathf.Clamp01(chromaticIntensity));
        }

        public static void SetFlowMap(MaterialPropertyBlock block, bool enabled, Texture texture, Vector2 scale, Vector2 offset, Vector2 speed, float strength)
        {
            if (block == null) return;
            block.SetFloat(FlowMapEnabled, enabled ? 1f : 0f);
            block.SetTexture(FlowMap, texture);
            block.SetVector(FlowMapScale, new Vector4(scale.x, scale.y, offset.x, offset.y));
            block.SetVector(FlowMapSpeed, new Vector4(speed.x, speed.y, 0f, 0f));
            block.SetFloat(FlowMapStrength, Mathf.Max(0f, strength));
        }

        public static void SetVertexAnimation(MaterialPropertyBlock block, bool enabled, Vector3 localDirection, float amplitude, float frequency, float speed, ESCompositeVertexColorMask mask)
        {
            if (block == null) return;
            block.SetFloat(VertexAnimationEnabled, enabled ? 1f : 0f);
            block.SetVector(VertexAnimationDirection, new Vector4(localDirection.x, localDirection.y, localDirection.z, 0f));
            block.SetFloat(VertexAnimationAmplitude, Mathf.Max(0f, amplitude));
            block.SetFloat(VertexAnimationFrequency, Mathf.Max(0f, frequency));
            block.SetFloat(VertexAnimationSpeed, speed);
            block.SetFloat(VertexAnimationMask, (float)mask);
        }

        public static void SetSoftParticles(MaterialPropertyBlock block, bool enabled, float nearDistance, float farDistance)
        {
            if (block == null) return;
            float near = Mathf.Max(0f, nearDistance);
            block.SetFloat(SoftParticlesEnabled, enabled ? 1f : 0f);
            block.SetFloat(SoftParticleNear, near);
            block.SetFloat(SoftParticleFar, Mathf.Max(near + 0.001f, farDistance));
        }

        public static void SetDissolve(MaterialPropertyBlock block, ES3DVFXDissolveMode mode, float progress)
        {
            if (block == null) return;
            block.SetFloat(DissolveMode, (float)mode);
            block.SetFloat(DissolveProgress, progress);
        }

        public static void SetFlags(MaterialPropertyBlock block, bool hologram, bool glitch)
        {
            if (block == null) return;
            block.SetFloat(HologramEnabled, hologram ? 1f : 0f);
            block.SetFloat(GlitchEnabled, glitch ? 1f : 0f);
        }

        public static void SetQuality(Material material, ESCompositeQualityTier quality)
        {
            ESCompositeURPProperties.ApplyQuality(material, QualityTier, quality);
        }

        public static void SetRenderState(
            Material material,
            ES3DVFXBlendMode blendMode,
            ES3DVFXDepthWriteMode depthWrite,
            ES3DVFXDepthTestMode depthTest,
            ES3DVFXCullMode cullMode,
            int queueOffset = 0)
        {
            if (material == null) return;
            SetBlendMode(material, blendMode);
            SetDepthWrite(material, depthWrite);
            SetDepthTest(material, depthTest);
            SetCullMode(material, cullMode);
            SetQueueOffset(material, queueOffset);
        }

        public static void SetBlendMode(Material material, ES3DVFXBlendMode blendMode)
        {
            if (material == null) return;
            switch (blendMode)
            {
                case ES3DVFXBlendMode.叠加:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.One);
                    break;
                case ES3DVFXBlendMode.预乘透明:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
                case ES3DVFXBlendMode.正片叠底:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.DstColor);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.Zero);
                    break;
                default:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
            }
            material.SetFloat(BlendMode, (float)blendMode);
            material.SetFloat(BlendOp, (float)UnityEngine.Rendering.BlendOp.Add);
            material.SetOverrideTag("RenderType", "Transparent");
        }

        public static void SetDepthWrite(Material material, ES3DVFXDepthWriteMode depthWrite)
        {
            if (material == null) return;
            material.SetFloat(ZWriteMode, (float)depthWrite);
        }

        public static void SetDepthTest(Material material, ES3DVFXDepthTestMode depthTest)
        {
            if (material == null) return;
            material.SetFloat(ZTest, (float)depthTest);
        }

        public static void SetCullMode(Material material, ES3DVFXCullMode cullMode)
        {
            if (material == null) return;
            material.SetFloat(Cull, (float)cullMode);
        }

        public static void SetQueueOffset(Material material, int queueOffset)
        {
            if (material == null) return;
            int offset = Mathf.Clamp(queueOffset, -50, 50);
            material.SetFloat(QueueOffset, offset);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = TransparentQueue + offset;
        }
    }

    public static class ESUICompositeURPProperties
    {
        public const string ShaderName = "ES/UI/Composite URP";
        private const string UIAlphaClipKeyword = "UNITY_UI_ALPHACLIP";
        public static readonly int HologramEnabled = Shader.PropertyToID("_EnableHologram");
        public static readonly int GlitchEnabled = Shader.PropertyToID("_EnableGlitch");
        public static readonly int GlitchSpeed = Shader.PropertyToID("_GlitchSpeed");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int VertexColorStrength = Shader.PropertyToID("_VertexColorStrength");
        public static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        public static readonly int UseUIAlphaClip = Shader.PropertyToID("_UseUIAlphaClip");
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");
        public static readonly int SparkleEnabled = Shader.PropertyToID("_EnableSparkle");
        public static readonly int ShineEnabled = Shader.PropertyToID("_EnableShine");
        public static readonly int ShineColor = Shader.PropertyToID("_ShineColor");
        public static readonly int ShineSpeed = Shader.PropertyToID("_ShineSpeed");
        public static readonly int ShineWidth = Shader.PropertyToID("_ShineWidth");
        public static readonly int ShineAngle = Shader.PropertyToID("_ShineAngle");
        public static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
        public static readonly int SparkleColor = Shader.PropertyToID("_SparkleColor");
        public static readonly int SparkleScale = Shader.PropertyToID("_SparkleScale");
        public static readonly int SparkleSpeed = Shader.PropertyToID("_SparkleSpeed");
        public static readonly int SparkleDensity = Shader.PropertyToID("_SparkleDensity");
        public static readonly int SparkleSharpness = Shader.PropertyToID("_SparkleSharpness");
        public static readonly int SparkleIntensity = Shader.PropertyToID("_SparkleIntensity");
        public static readonly int FlowEnabled = Shader.PropertyToID("_EnableFlow");
        public static readonly int FlowSpeed = Shader.PropertyToID("_FlowSpeed");
        public static readonly int FlowStrength = Shader.PropertyToID("_FlowStrength");
        public static readonly int ChromaticEnabled = Shader.PropertyToID("_EnableChromatic");
        public static readonly int ChromaticOffset = Shader.PropertyToID("_ChromaticOffset");
        public static readonly int ChromaticIntensity = Shader.PropertyToID("_ChromaticIntensity");
        public static readonly int ChromaticEdgeOnly = Shader.PropertyToID("_ChromaticEdgeOnly");
        public static readonly int ChromaticAngle = Shader.PropertyToID("_ChromaticAngle");
        public static readonly int BlurEnabled = Shader.PropertyToID("_EnableBlur");
        public static readonly int BlurRadius = Shader.PropertyToID("_BlurRadius");
        public static readonly int BlurIntensity = Shader.PropertyToID("_BlurIntensity");

        public static void SetMotionEffects(MaterialPropertyBlock block, bool sparkle, bool flow, bool chromatic, Vector2 flowSpeed, float flowStrength, float sparkleIntensity, float chromaticOffset, float chromaticIntensity)
        {
            if (block == null) return;
            block.SetFloat(SparkleEnabled, sparkle ? 1f : 0f);
            block.SetFloat(FlowEnabled, flow ? 1f : 0f);
            block.SetVector(FlowSpeed, new Vector4(flowSpeed.x, flowSpeed.y, 0f, 0f));
            block.SetFloat(FlowStrength, Mathf.Clamp01(flowStrength));
            block.SetFloat(SparkleIntensity, Mathf.Max(0f, sparkleIntensity));
            block.SetFloat(ChromaticEnabled, chromatic ? 1f : 0f);
            block.SetFloat(ChromaticOffset, Mathf.Max(0f, chromaticOffset));
            block.SetFloat(ChromaticIntensity, Mathf.Clamp01(chromaticIntensity));
        }

        public static void SetEffects(MaterialPropertyBlock block, ESUICompositeEffectMode mode)
        {
            if (block == null) return;
            block.SetFloat(HologramEnabled, mode == ESUICompositeEffectMode.全息 || mode == ESUICompositeEffectMode.全息与故障 ? 1f : 0f);
            block.SetFloat(GlitchEnabled, mode == ESUICompositeEffectMode.故障 || mode == ESUICompositeEffectMode.全息与故障 ? 1f : 0f);
        }

        /// <summary>
        /// 设置 Unity UI 的固定阈值透明裁剪。
        /// 该能力由本地 Shader Keyword 控制，必须作用于当前 Graphic 缓存的独立 Material，不能使用 MaterialPropertyBlock。
        /// </summary>
        public static void SetUIAlphaClip(Material material, bool enabled)
        {
            if (material == null) return;
            material.SetFloat(UseUIAlphaClip, enabled ? 1f : 0f);
            ESCompositeURPProperties.SetKeyword(material, UIAlphaClipKeyword, enabled);
        }
    }

    internal static class ESCompositeShaderTimeDriver
    {
        private const string DriverObjectName = "ES Composite Shader Time Driver";
        private static GameObject driverObject;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (driverObject != null) return;
            driverObject = new GameObject(DriverObjectName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(driverObject);
            driverObject.AddComponent<DriverBehaviour>();
        }

        private sealed class DriverBehaviour : MonoBehaviour
        {
            private void OnEnable()
            {
                Publish();
            }

            private void Update()
            {
                Publish();
            }

            private static void Publish()
            {
                ESCompositeURPProperties.SetUnscaledTime(Time.unscaledTime);
            }
        }
    }
}
