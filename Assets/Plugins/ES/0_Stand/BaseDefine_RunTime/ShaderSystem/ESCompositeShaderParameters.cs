using UnityEngine;

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
        public static readonly int SequenceFrame = Shader.PropertyToID("_SequenceFrame");
        public static readonly int SequenceSpeed = Shader.PropertyToID("_SequenceSpeed");
        public static readonly int GlowIntensity = Shader.PropertyToID("_GlowIntensity");
        public static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");

        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int VertexColorStrength = Shader.PropertyToID("_VertexColorStrength");
        public static readonly int DistortionStrength = Shader.PropertyToID("_DistortionStrength");
        public static readonly int HologramEnabled = Shader.PropertyToID("_EnableHologram");
        public static readonly int GlitchEnabled = Shader.PropertyToID("_EnableGlitch");
        public static readonly int BurnEnabled = Shader.PropertyToID("_EnableBurn");
        public static readonly int PoisonEnabled = Shader.PropertyToID("_EnablePoison");
        public static readonly int FrozenEnabled = Shader.PropertyToID("_EnableFrozen");

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
        public static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
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
        public static readonly int DissolveMode = Shader.PropertyToID("_DissolveMode");
        public static readonly int DissolveProgress = Shader.PropertyToID("_DissolveProgress");
        public static readonly int HologramEnabled = Shader.PropertyToID("_EnableHologram");
        public static readonly int GlitchEnabled = Shader.PropertyToID("_EnableGlitch");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int VertexColorStrength = Shader.PropertyToID("_VertexColorStrength");
        public static readonly int Distortion = Shader.PropertyToID("_Distortion");
        public static readonly int QualityTier = Shader.PropertyToID("_QualityTier");
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");

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
    }

    public static class ESUICompositeURPProperties
    {
        public const string ShaderName = "ES/UI/Composite URP";
        public static readonly int HologramEnabled = Shader.PropertyToID("_EnableHologram");
        public static readonly int GlitchEnabled = Shader.PropertyToID("_EnableGlitch");
        public static readonly int GlitchSpeed = Shader.PropertyToID("_GlitchSpeed");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int VertexColorStrength = Shader.PropertyToID("_VertexColorStrength");
        public static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");

        public static void SetEffects(MaterialPropertyBlock block, ESUICompositeEffectMode mode)
        {
            if (block == null) return;
            block.SetFloat(HologramEnabled, mode == ESUICompositeEffectMode.全息 || mode == ESUICompositeEffectMode.全息与故障 ? 1f : 0f);
            block.SetFloat(GlitchEnabled, mode == ESUICompositeEffectMode.故障 || mode == ESUICompositeEffectMode.全息与故障 ? 1f : 0f);
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
