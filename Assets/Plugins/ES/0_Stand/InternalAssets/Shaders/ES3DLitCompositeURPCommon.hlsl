#ifndef ES_3D_LIT_COMPOSITE_URP_COMMON_INCLUDED
#define ES_3D_LIT_COMPOSITE_URP_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "ESCompositeColorTransform.hlsl"
#include "ESCompositeRecolor.hlsl"
#include "ESCompositeGenerated.hlsl"
#include "ESCompositeSampling.hlsl"
#include "ESCompositeESNativeEffects.hlsl"
#include "ESCompositeFade.hlsl"

// No resource-mask keyword means the dynamic MPB-safe variant. Optimized materials
// select exactly one mask keyword, where bits are UV=1, Fade=2, Surface=4, Layers=8.
#if defined(_ES_LIT_RESOURCE_MASK_0) || defined(_ES_LIT_RESOURCE_MASK_1) || defined(_ES_LIT_RESOURCE_MASK_2) || defined(_ES_LIT_RESOURCE_MASK_3) || defined(_ES_LIT_RESOURCE_MASK_4) || defined(_ES_LIT_RESOURCE_MASK_5) || defined(_ES_LIT_RESOURCE_MASK_6) || defined(_ES_LIT_RESOURCE_MASK_7) || defined(_ES_LIT_RESOURCE_MASK_8) || defined(_ES_LIT_RESOURCE_MASK_9) || defined(_ES_LIT_RESOURCE_MASK_10) || defined(_ES_LIT_RESOURCE_MASK_11) || defined(_ES_LIT_RESOURCE_MASK_12) || defined(_ES_LIT_RESOURCE_MASK_13) || defined(_ES_LIT_RESOURCE_MASK_14) || defined(_ES_LIT_RESOURCE_MASK_15)
    #define ES_LIT_RESOURCE_PROFILE_OPTIMIZED 1
#endif

#if !defined(ES_LIT_RESOURCE_PROFILE_OPTIMIZED) || defined(_ES_LIT_RESOURCE_MASK_1) || defined(_ES_LIT_RESOURCE_MASK_3) || defined(_ES_LIT_RESOURCE_MASK_5) || defined(_ES_LIT_RESOURCE_MASK_7) || defined(_ES_LIT_RESOURCE_MASK_9) || defined(_ES_LIT_RESOURCE_MASK_11) || defined(_ES_LIT_RESOURCE_MASK_13) || defined(_ES_LIT_RESOURCE_MASK_15)
    #define ES_LIT_COMPILE_UV_RESOURCES 1
#endif
#if !defined(ES_LIT_RESOURCE_PROFILE_OPTIMIZED) || defined(_ES_LIT_RESOURCE_MASK_2) || defined(_ES_LIT_RESOURCE_MASK_3) || defined(_ES_LIT_RESOURCE_MASK_6) || defined(_ES_LIT_RESOURCE_MASK_7) || defined(_ES_LIT_RESOURCE_MASK_10) || defined(_ES_LIT_RESOURCE_MASK_11) || defined(_ES_LIT_RESOURCE_MASK_14) || defined(_ES_LIT_RESOURCE_MASK_15)
    #define ES_LIT_COMPILE_FADE_RESOURCES 1
#endif
#if !defined(ES_LIT_RESOURCE_PROFILE_OPTIMIZED) || defined(_ES_LIT_RESOURCE_MASK_4) || defined(_ES_LIT_RESOURCE_MASK_5) || defined(_ES_LIT_RESOURCE_MASK_6) || defined(_ES_LIT_RESOURCE_MASK_7) || defined(_ES_LIT_RESOURCE_MASK_12) || defined(_ES_LIT_RESOURCE_MASK_13) || defined(_ES_LIT_RESOURCE_MASK_14) || defined(_ES_LIT_RESOURCE_MASK_15)
    #define ES_LIT_COMPILE_SURFACE_RESOURCES 1
#endif
#if !defined(ES_LIT_RESOURCE_PROFILE_OPTIMIZED) || defined(_ES_LIT_RESOURCE_MASK_8) || defined(_ES_LIT_RESOURCE_MASK_9) || defined(_ES_LIT_RESOURCE_MASK_10) || defined(_ES_LIT_RESOURCE_MASK_11) || defined(_ES_LIT_RESOURCE_MASK_12) || defined(_ES_LIT_RESOURCE_MASK_13) || defined(_ES_LIT_RESOURCE_MASK_14) || defined(_ES_LIT_RESOURCE_MASK_15)
    #define ES_LIT_COMPILE_LAYER_RESOURCES 1
#endif

// Texture Resources
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
TEXTURE2D(_MetallicMap); SAMPLER(sampler_MetallicMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
#if defined(ES_LIT_COMPILE_FADE_RESOURCES) || defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
#endif
#if defined(ES_LIT_COMPILE_UV_RESOURCES)
TEXTURE2D(_FlowMap); SAMPLER(sampler_FlowMap);
#endif
#if defined(ES_LIT_COMPILE_UV_RESOURCES) || defined(ES_LIT_COMPILE_FADE_RESOURCES) || defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
SAMPLER(sampler_UberNoiseTexture);
#endif
#if defined(ES_LIT_COMPILE_FADE_RESOURCES) || defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
TEXTURE2D(_UberNoiseTexture);
#endif
#if !defined(ES_LIT_COMPILE_FADE_RESOURCES) && !defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    #define _UberNoiseTexture _BaseMap
    #define sampler_UberNoiseTexture sampler_BaseMap
#endif
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
TEXTURE2D(_AddColorMask);
TEXTURE2D(_StrongTintMask);
TEXTURE2D(_RecolorRGBMask); SAMPLER(sampler_RecolorRGBMask);
TEXTURE2D(_RecolorRGBYCPMask); SAMPLER(sampler_RecolorRGBYCPMask);
TEXTURE2D(_AddHueMask); SAMPLER(sampler_AddHueMask);
TEXTURE2D(_SineGlowMask); SAMPLER(sampler_SineGlowMask);
TEXTURE2D(_MetalMask); SAMPLER(sampler_MetalMask);
TEXTURE2D(_ShineMask);
#define sampler_ShineMask sampler_BaseMap
TEXTURE2D(_InnerOutlineTintTexture);
TEXTURE2D(_OuterOutlineTintTexture);
TEXTURE2D(_PixelOutlineTintTexture);
#endif
#if !defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    #define _ShineMask _BaseMap
    #define sampler_ShineMask sampler_BaseMap
#endif
// Layer textures reuse the base sampler state so the Lit pass stays within the SM3.0 sampler budget.
#if defined(ES_LIT_COMPILE_LAYER_RESOURCES)
TEXTURE2D(_TextureLayer1Texture);
TEXTURE2D(_TextureLayer2Texture);
#endif
// Auxiliary UV/fade textures reuse existing samplers to keep SM3.0 passes below the sampler limit.
#if defined(ES_LIT_COMPILE_UV_RESOURCES)
TEXTURE2D(_UVDistortNoiseTex);
TEXTURE2D(_UVDistortMask);
#endif
#if defined(ES_LIT_COMPILE_FADE_RESOURCES)
TEXTURE2D(_FadeNoiseTex);
TEXTURE2D(_FadeMask);
TEXTURE2D(_CustomFadeFadeMask);
#endif
// Shared And Per-Material State
float3 _LightDirection;
float3 _LightPosition;
float _ESUnscaledTime;
float _ESUnscaledTimeValid;
float4 _ESCompositeGlobalWind;
float _ESCompositeGlobalWindValid;

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _MetallicMap_ST;
half4 _BaseColor;
float4 _MainTexScaleOffset;
float _TimeMode;
float _CustomTime;
float _TimeScale;
float _EnableTimeFPS;
float _TimeFPS;
float _EnableTimeFrequency;
float _TimeFrequency;
float _TimeRange;
float _EnableUVTransform;
float4 _UVPivot;
float4 _UVScale;
float4 _UVOffset;
float _UVRotation;
float _UVRotationSpeed;
float _EnableUVDistort;
float4 _UVDistortFrequency;
float4 _UVDistortSpeed;
float _UVDistortAmount;
float4 _UVDistortFrom;
float4 _UVDistortTo;
float _UVDistortFade;
float _UVDistortMaskToggle;
float _UVDistortMaskChannel;
float _TilingMode;
float4 _WorldTilingScale;
float4 _WorldTilingOffset;
float _WorldTilingPixelsPerUnit;
float4 _ScreenTilingScale;
float4 _ScreenTilingOffset;
float _ScreenTilingPixelsPerUnit;
float _EnableSmoothPixelArt;
float _SmoothPixelStrength;
float _EnablePixelate;
float _PixelateCells;
float _PixelateStrength;
float _EnableCheckerboard;
float _CheckerboardDarken;
float _CheckerboardTiling;
float _EnableFlame;
float _FlameBrightness;
float _FlameSmooth;
float _FlameRadius;
float4 _FlameCenter;
float4 _FlameDirection;
float4 _FlameSpeed;
float _FlameNoiseFactor;
float _FlameNoiseHeightFactor;
float4 _FlameNoiseScale;
float _EnableSmoke;
float _SmokeAlpha;
float _SmokeSmoothness;
float _SmokeNoiseScale;
float4 _SmokeSpeed;
float _SmokeNoiseFactor;
float _SmokeDarkEdge;
float _SmokeVertexSeed;
float _EnableHalftone;
float _HalftoneScale;
float _HalftoneAngle;
float _HalftoneStrength;
float4 _HalftonePosition;
float _HalftoneFade;
float _HalftoneFadeWidth;
float _HalftoneInvert;
float _HalftoneAlphaPattern;
float _EnableSharpen;
float _SharpenAmount;
float _SharpenRadius;
float _SharpenThreshold;
float _SharpenFade;
float _EnableTextureLayer1;
float _TextureLayer1Fade;
half4 _TextureLayer1Color;
float4 _TextureLayer1Scale;
float4 _TextureLayer1Offset;
float _TextureLayer1ScrollToggle;
float4 _TextureLayer1ScrollSpeed;
float _TextureLayer1SheetToggle;
float _TextureLayer1Columns;
float _TextureLayer1Rows;
float _TextureLayer1Speed;
float _TextureLayer1StartFrame;
float _TextureLayer1EdgeClip;
float _TextureLayer1ContrastToggle;
float _TextureLayer1Contrast;
float _EnableTextureLayer2;
float _TextureLayer2Fade;
half4 _TextureLayer2Color;
float4 _TextureLayer2Scale;
float4 _TextureLayer2Offset;
float _TextureLayer2ScrollToggle;
float4 _TextureLayer2ScrollSpeed;
float _TextureLayer2SheetToggle;
float _TextureLayer2Columns;
float _TextureLayer2Rows;
float _TextureLayer2Speed;
float _TextureLayer2StartFrame;
float _TextureLayer2EdgeClip;
float _TextureLayer2ContrastToggle;
float _TextureLayer2Contrast;
float _EnableInnerOutline;
float _InnerOutlineFade;
half4 _InnerOutlineColor;
float _InnerOutlineWidth;
float _InnerOutlineDistortionToggle;
float4 _InnerOutlineDistortionIntensity;
float4 _InnerOutlineNoiseScale;
float4 _InnerOutlineNoiseSpeed;
float _InnerOutlineTextureToggle;
float4 _InnerOutlineTextureSpeed;
float _InnerOutlineOutlineOnlyToggle;
float _EnableOuterOutline;
float _OuterOutlineFade;
half4 _OuterOutlineColor;
float _OuterOutlineWidth;
float _OuterOutlineDistortionToggle;
float4 _OuterOutlineDistortionIntensity;
float4 _OuterOutlineNoiseScale;
float4 _OuterOutlineNoiseSpeed;
float _OuterOutlineTextureToggle;
float4 _OuterOutlineTextureSpeed;
float _OuterOutlineOutlineOnlyToggle;
float _EnablePixelOutline;
float _PixelOutlineFade;
half4 _PixelOutlineColor;
float _PixelOutlineWidth;
float _PixelOutlineTextureToggle;
float4 _PixelOutlineTextureSpeed;
float _PixelOutlineOutlineOnlyToggle;
float _EnableShadow;
float _ShadowFade;
float4 _ShadowOffset;
half4 _ShadowColor;
float _EnableFullGlowDissolve;
float _FullGlowDissolveFade;
float _FullGlowDissolveWidth;
half4 _FullGlowDissolveEdgeColor;
float4 _FullGlowDissolveNoiseScale;
float _EnableHologram;
float _HologramFade;
half4 _HologramColor;
float _HologramContrast;
float _HologramSpace;
float4 _HologramDirection;
float _HologramLineFrequency;
float _HologramLineGap;
float _HologramSpeed;
float _HologramMinAlpha;
float _HologramDistortionOffset;
float4 _HologramDistortionDirection;
float _HologramDistortionSpeed;
float _HologramDistortionDensity;
float _HologramDistortionScale;
float _EnableGlitch;
float _GlitchFade;
float _GlitchIntensity;
float _GlitchSpeed;
float4 _GlitchScanDirection;
float _GlitchMaskMin;
float4 _GlitchMaskScale;
float4 _GlitchMaskSpeed;
float _GlitchHueSpeed;
float _GlitchBrightness;
float4 _GlitchNoiseScale;
float4 _GlitchNoiseSpeed;
float4 _GlitchDistortion;
float4 _GlitchDistortionScale;
float4 _GlitchDistortionSpeed;
float _EnableFullDistortion;
float _FullDistortionFade;
float4 _FullDistortionDistortion;
float4 _FullDistortionNoiseScale;
float _EnableSqueeze;
float _SqueezeFade;
float4 _SqueezeScale;
float _SqueezePower;
float4 _SqueezeCenter;
float _EnableSineRotate;
float _SineRotateFade;
float _SineRotateAngle;
float _SineRotateFrequency;
float4 _SineRotatePivot;
float _EnableVertexAnimation;
float4 _VertexAnimationDirection;
float _VertexAnimationAmplitude;
float _VertexAnimationFrequency;
float _VertexAnimationSpeed;
float _VertexAnimationMask;
float _EnableWind;
float4 _WindDirection;
float _WindAmplitude;
float _WindFrequency;
float _WindSpeed;
float _WindAnchor;
float4 _WindAnchorDirection;
float _WindGlobalInfluence;
float _EnableSquish;
float _SquishAmount;
float4 _SquishDirection;
float _SquishSpeed;
float _SquishFade;
float _ESInteractiveWindRotation;
float _ESInteractiveWindHeight;
float _ESInteractiveSquish;
float _ESWindPhaseOffset;
float _EnableWiggle;
float _WiggleAmplitude;
float _WiggleFrequency;
float4 _WiggleDirection;
float _WiggleSpeed;
float _EnableVibrate;
float _VibrateAmplitude;
float4 _VibrateDirection;
float _VibrateSpeed;
float _EnableSineMove;
float _SineMoveFade;
float4 _SineMoveOffset;
float4 _SineMoveFrequency;
float _EnableSineScale;
float _SineScaleFrequency;
float4 _SineScaleFactor;
float _FadeMode;
float _FadeProgress;
float4 _FadePosition;
float _FadeRotation;
float _FadeWidth;
float _FadeInvert;
float _FadeNoiseFactor;
float4 _FadeNoiseScale;
float4 _FadeNoiseSpeed;
float _FadeDistortionStrength;
float _DissolveEdgeIntensity;
float _EnableFullAlphaDissolve;
float _FullAlphaDissolveFade;
float _FullAlphaDissolveWidth;
float4 _FullAlphaDissolveNoiseScale;
float _EnableSourceAlphaDissolve;
float _SourceAlphaDissolveFade;
float4 _SourceAlphaDissolvePosition;
float _SourceAlphaDissolveWidth;
float4 _SourceAlphaDissolveNoiseScale;
float _SourceAlphaDissolveNoiseFactor;
float _SourceAlphaDissolveInvert;
float _EnableSourceGlowDissolve;
float _SourceGlowDissolveFade;
float4 _SourceGlowDissolvePosition;
float _SourceGlowDissolveWidth;
half4 _SourceGlowDissolveEdgeColor;
float4 _SourceGlowDissolveNoiseScale;
float _SourceGlowDissolveNoiseFactor;
float _SourceGlowDissolveInvert;
float _EnableDirectionalAlphaFade;
float _DirectionalAlphaFadeFade;
float _DirectionalAlphaFadeRotation;
float _DirectionalAlphaFadeWidth;
float4 _DirectionalAlphaFadeNoiseScale;
float _DirectionalAlphaFadeNoiseFactor;
float _DirectionalAlphaFadeInvert;
float _EnableDirectionalGlowFade;
float _DirectionalGlowFadeFade;
float _DirectionalGlowFadeRotation;
half4 _DirectionalGlowFadeEdgeColor;
float _DirectionalGlowFadeWidth;
float4 _DirectionalGlowFadeNoiseScale;
float _DirectionalGlowFadeNoiseFactor;
float _DirectionalGlowFadeInvert;
float _EnableDirectionalDistortion;
float _DirectionalDistortionFade;
float _DirectionalDistortionRotation;
float _DirectionalDistortionWidth;
float4 _DirectionalDistortionNoiseScale;
float _DirectionalDistortionNoiseFactor;
float4 _DirectionalDistortionDistortion;
float _DirectionalDistortionRandomDirection;
float4 _DirectionalDistortionDistortionScale;
float _DirectionalDistortionInvert;
float _EnableCustomFade;
float4 _CustomFadeFadeMask_ST;
float _CustomFadeSmoothness;
float4 _CustomFadeNoiseScale;
float _CustomFadeNoiseFactor;
float _CustomFadeAlpha;
float _UseNormalMap;
float _NormalScale;
float _UseMetallicMap;
float _SmoothnessMapChannel;
float _Metallic;
float _Smoothness;
float _Occlusion;
float _UseOcclusionMap;
float _UseEmission;
float _EmissionUseAlpha;
half4 _EmissionColor;
float _DissolveMode;
float _DissolveProgress;
float _DissolveSoftness;
half4 _DissolveEdgeColor;
float _DissolveEdgeWidth;
float _EnableAddColor;
half4 _AddColor;
float _AddColorFade;
float _AddColorContrastToggle;
float _AddColorContrast;
float _AddColorMaskToggle;
float4 _AddColorMask_ST;
float _EnableStrongTint;
half4 _StrongTint;
float _StrongTintFade;
float _StrongTintContrastToggle;
float _StrongTintContrast;
float _StrongTintMaskToggle;
float4 _StrongTintMask_ST;
float _EnableAlphaTint;
half4 _AlphaTint;
float _AlphaTintMin;
float _AlphaTintFade;
float _EnableColorReplace;
half4 _ReplaceFrom;
half4 _ReplaceTo;
float _ReplaceRange;
float _ReplaceSoftness;
float _ReplaceContrast;
float _ReplaceFade;
float _EnableRecolorRGB;
half4 _RecolorRed;
half4 _RecolorGreen;
half4 _RecolorBlue;
float _RecolorRGBStrength;
float _RecolorRGBMaskToggle;
float _RecolorRGBMaskChannel;
float _EnableRecolorRGBYCP;
half4 _RecolorRGBYCPRed;
half4 _RecolorRGBYCPGreen;
half4 _RecolorRGBYCPBlue;
half4 _RecolorRGBYCPYellow;
half4 _RecolorRGBYCPCyan;
half4 _RecolorRGBYCPPurple;
float _RecolorRGBYCPStrength;
float _RecolorRGBYCPMaskToggle;
float _RecolorRGBYCPMaskChannel;
float _EnableBrightness;
float _Brightness;
float _EnableContrast;
float _Contrast;
float _EnableSaturation;
float _Saturation;
float _EnableHue;
float _Hue;
float _EnableSplitToning;
half4 _SplitToneShadows;
half4 _SplitToneHighlights;
float _SplitToneBalance;
float _SplitToneStrength;
float _SplitToneContrast;
float _SplitToneShift;
float _EnableBlackTint;
float _BlackTintFade;
half4 _BlackTintColor;
float _BlackTintPower;
float _EnableInkSpread;
float _InkSpreadFade;
half4 _InkSpreadColor;
float _InkSpreadContrast;
float _InkSpreadDistance;
float4 _InkSpreadPosition;
float _InkSpreadWidth;
float4 _InkSpreadNoiseScale;
float _InkSpreadNoiseFactor;
float _EnableShiftHue;
float _ShiftHueSpeed;
float _EnableAddHue;
float _AddHueFade;
float _AddHueSpeed;
float _AddHueBrightness;
float _AddHueSaturation;
float _AddHueContrast;
float _AddHueMaskToggle;
float4 _AddHueMask_ST;
float _EnableSineGlow;
float _SineGlowFade;
half4 _SineGlowColor;
float _SineGlowContrast;
float _SineGlowFrequency;
float _SineGlowMin;
float _SineGlowMax;
float _SineGlowMaskToggle;
float4 _SineGlowMask_ST;
float _EnableNegative;
float _NegativeFade;
float _EnableRainbow;
float _RainbowSpeed;
float _RainbowDensity;
float4 _RainbowDirection;
float _RainbowBrightness;
float _RainbowFade;
float _RainbowSaturation;
float _RainbowContrast;
float4 _RainbowCenter;
float4 _RainbowNoiseScale;
float _RainbowNoiseFactor;
float _EnablePingPongGlow;
half4 _GlowFrom;
half4 _GlowTo;
float _GlowFrequency;
float _GlowIntensity;
float _GlowContrast;
float _GlowFade;
float _EnableCamouflage;
float _CamouflageFade;
half4 _CamouflageBaseColor;
float _CamouflageContrast;
half4 _CamouflageColorA;
float _CamouflageDensityA;
float _CamouflageSmoothnessA;
float4 _CamouflageNoiseScaleA;
half4 _CamouflageColorB;
float _CamouflageDensityB;
float _CamouflageSmoothnessB;
float4 _CamouflageNoiseScaleB;
float _CamouflageAnimationToggle;
float4 _CamouflageDistortionSpeed;
float4 _CamouflageDistortionIntensity;
float4 _CamouflageDistortionScale;
float _EnableMetal;
float _MetalFade;
half4 _MetalColor;
float _MetalContrast;
half4 _MetalHighlightColor;
float _MetalHighlightDensity;
float _MetalHighlightContrast;
float4 _MetalNoiseScale;
float4 _MetalNoiseSpeed;
float4 _MetalNoiseDistortionScale;
float4 _MetalNoiseDistortionSpeed;
float4 _MetalNoiseDistortion;
float _MetalMaskToggle;
float _EnableFrozen;
float _ESNativeStatusContract;
float _FrozenFade;
half4 _FrozenTint;
float _FrozenContrast;
half4 _FrozenSnowColor;
float _FrozenSnowContrast;
float _FrozenSnowDensity;
float4 _FrozenSnowScale;
half4 _FrozenHighlightColor;
float _FrozenHighlightContrast;
float _FrozenHighlightDensity;
float4 _FrozenHighlightSpeed;
float4 _FrozenHighlightScale;
float4 _FrozenHighlightDistortion;
float4 _FrozenHighlightDistortionSpeed;
float4 _FrozenHighlightDistortionScale;
half4 _FrozenColor;
half4 _FrozenHighlight;
float _FrozenDensity;
float _FrozenSpeed;
float _EnablePoison;
float _PoisonFade;
float _PoisonRecolorFactor;
float _PoisonShiftSpeed;
float _PoisonNoiseBrightness;
float4 _PoisonNoiseScale;
float4 _PoisonNoiseSpeed;
half4 _PoisonColor;
float _PoisonDensity;
float _PoisonSpeed;
float _EnableEnchanted;
float _EnchantedFade;
float4 _EnchantedSpeed;
float4 _EnchantedScale;
float _EnchantedBrightness;
float _EnchantedContrast;
float _EnchantedReduce;
float _EnchantedRainbowToggle;
float _EnchantedRainbowSpeed;
float _EnchantedRainbowDensity;
float _EnchantedRainbowSaturation;
half4 _EnchantedLowColor;
half4 _EnchantedHighColor;
float _EnchantedLerpToggle;
float _EnableShifting;
float _ShiftingFade;
float _ShiftingSpeed;
float _ShiftingDensity;
float _ShiftingBrightness;
float _ShiftingContrast;
float _ShiftingRainbowToggle;
float _ShiftingSaturation;
half4 _ShiftingColorA;
half4 _ShiftingColorB;
float _EnableRim;
half4 _RimColor;
float _RimPower;
float _RimIntensity;
float _EnableShine;
float _ShineFade;
float _ShineSaturation;
float _ShineContrast;
float _ShineRotation;
float _ShineSmooth;
float _ShineFrequency;
float _ShineMaskToggle;
float4 _ShineMask_ST;
half4 _ShineColor;
float _ShineSpeed;
float _ShineWidth;
float _ShineIntensity;
float _ShineSpace;
float4 _ShineDirection;
float _EnableSparkle;
half4 _SparkleColor;
float _SparkleScale;
float _SparkleSpeed;
float _SparkleDensity;
float _SparkleSharpness;
float _SparkleIntensity;
float _EnableFlow;
float4 _FlowSpeed;
float _FlowStrength;
float _EnableFlowMap;
float4 _FlowMapScale;
float4 _FlowMapSpeed;
float _FlowMapStrength;
float _EnableChromatic;
float _ChromaticOffset;
float _ChromaticIntensity;
float _ChromaticEdgeOnly;
float _ChromaticAngle;
float _EnableBlur;
float _BlurRadius;
float _BlurIntensity;
float _EnableBurn;
float _BurnFade;
float4 _BurnPosition;
float _BurnRadius;
float4 _BurnEdgeNoiseScale;
float _BurnEdgeNoiseFactor;
float _BurnInsideContrast;
half4 _BurnInsideColor;
half4 _BurnInsideNoiseColor;
float _BurnInsideNoiseFactor;
float4 _BurnInsideNoiseScale;
float _BurnSwirlFactor;
float4 _BurnSwirlNoiseScale;
half4 _BurnEdgeColor;
float _BurnProgress;
float _BurnWidth;
float _AlphaClip;
float _Cutoff;
float4 _NoiseScale;
float4 _NoiseSpeed;
CBUFFER_END

#include "ESCompositeESNativeFadeStack.hlsl"
#include "ESCompositeESNativeStatusEffects.hlsl"
#include "ESCompositeESNativeStylizedEffects.hlsl"

// Time, UV And Vertex Deformation
float ESCompositeTime()
{
    float baseTime = _TimeMode > 1.5 ? _CustomTime : (_TimeMode > 0.5 ? (_ESUnscaledTimeValid > 0.5 ? _ESUnscaledTime : _Time.y) : _Time.y);
    float timeValue = baseTime * _TimeScale;
    if (_EnableTimeFPS > 0.5)
    {
        float fps = max(abs(_TimeFPS), 0.01);
        timeValue = floor(timeValue * fps) / fps;
    }
    if (_EnableTimeFrequency > 0.5)
        timeValue = sin(timeValue * _TimeFrequency) * _TimeRange + 100.0;
    return timeValue;
}

float ESGetTime()
{
    return ESCompositeTime();
}

float ESResolveLitShineCoordinate(
    float2 surfaceUV,
    float3 positionWS,
    float fallbackDegrees,
    float exactCompatibility)
{
    if (_ShineSpace > 1.5)
        return ESCompositeShineCoordinate3D(
            positionWS,
            _ShineDirection.xyz,
            float3(0.0, 1.0, 0.0));
    if (_ShineSpace > 0.5)
        return ESCompositeShineCoordinate2D(
            surfaceUV,
            _ShineDirection.xy,
            fallbackDegrees);

    if (exactCompatibility > 0.5 && length(_ShineDirection.xyz) <= 0.0001)
        return ESCompositeShineCoordinate2D(
            surfaceUV,
            float2(0.0, 0.0),
            fallbackDegrees);
    return ESCompositeShineCoordinate3D(
        positionWS,
        _ShineDirection.xyz,
        float3(0.0, 1.0, 0.0));
}

#include "ESCompositeSpriteVertexMotion.hlsl"

#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
#include "ES3DLitCompositeESNativeSurface.hlsl"
#endif

float ESVertexAnimationMask(float4 vertexColor)
{
    if (_VertexAnimationMask < 0.5) return 1.0;
    if (_VertexAnimationMask < 1.5) return vertexColor.r;
    if (_VertexAnimationMask < 2.5) return vertexColor.g;
    if (_VertexAnimationMask < 3.5) return vertexColor.b;
    return vertexColor.a;
}

float3 ESApplyVertexAnimation(float3 positionOS, float4 vertexColor, float2 localUV)
{
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_EnableVertexAnimation > 0.5 && abs(_VertexAnimationAmplitude) > 0.00001)
    {
        float directionLength = length(_VertexAnimationDirection.xyz);
        float3 directionOS = directionLength > 0.0001
            ? _VertexAnimationDirection.xyz / directionLength
            : float3(0.0, 1.0, 0.0);
        float phase = dot(positionOS, directionOS) * _VertexAnimationFrequency + ESCompositeTime() * _VertexAnimationSpeed;
        positionOS += directionOS * sin(phase) * _VertexAnimationAmplitude * saturate(ESVertexAnimationMask(vertexColor));
    }
    if (_EnableWind > 0.5 || _EnableSquish > 0.5 || _EnableWiggle > 0.5
        || abs(_ESInteractiveWindRotation) > 0.0001 || abs(_ESInteractiveSquish) > 0.0001
        || _EnableVibrate > 0.5 || _EnableSineMove > 0.5 || _EnableSineScale > 0.5)
        positionOS = ESApplyVertexMotion(positionOS, localUV);
#endif
    return positionOS;
}

float ESLitRandom(float2 value)
{
    return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
}

float ESLitHologramCoordinate(float2 uv, float3 positionWS)
{
    return ESCompositeResolveESNativeHologramCoordinate(uv, positionWS);
}

float2 ESLitApplyHologramUV(float2 uv, float3 positionWS)
{
    float timeValue = ESCompositeTime() * _HologramDistortionSpeed;
    float coordinate = ESLitHologramCoordinate(uv, positionWS);
    float band = floor((coordinate + timeValue) * max(abs(_HologramDistortionScale), 0.01));
    float active = step(1.0 - saturate(_HologramDistortionDensity), ESLitRandom(float2(band, floor(timeValue))));
    float offset = (ESLitRandom(float2(band + 19.0, floor(timeValue) + 7.0)) * 2.0 - 1.0)
        * _HologramDistortionOffset * active * saturate(_HologramFade);
    float2 distortionDirection = _HologramDistortionDirection.xy;
    float distortionDirectionLength = length(distortionDirection);
    distortionDirection = distortionDirectionLength > 0.0001
        ? distortionDirection / distortionDirectionLength
        : float2(1.0, 0.0);
    uv += distortionDirection * offset;
    return uv;
}

float ESLitGlitchMask(float2 uv)
{
    float timeValue = ESCompositeTime();
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    float2 maskUV = uv * _GlitchMaskScale.xy + timeValue * _GlitchMaskSpeed.xy;
    float maskNoise = SAMPLE_TEXTURE2D(_UberNoiseTexture, sampler_UberNoiseTexture, maskUV).r;
    return max(saturate(_GlitchMaskMin), maskNoise);
#else
    float scanCoordinate = ESCompositeDirectionalCoordinate2D(
        frac(uv),
        _GlitchScanDirection.xy,
        float2(0.0, 1.0));
    float row = floor(scanCoordinate * 128.0);
    float timeCell = floor(timeValue * max(abs(_GlitchSpeed), 0.01));
    return max(saturate(_GlitchMaskMin), ESLitRandom(float2(row, timeCell + 17.0)));
#endif
}

float2 ESLitApplyGlitchUV(float2 uv)
{
    float timeValue = ESCompositeTime();
    float active = ESLitGlitchMask(uv) * saturate(_GlitchFade);
    float2 direction = _GlitchDistortion.xy;
    float directionLength = length(direction);
    direction = directionLength > 0.0001 ? direction / directionLength : float2(1.0, 0.0);
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    float2 noiseUV = uv * _GlitchDistortionScale.xy + timeValue * _GlitchDistortionSpeed.xy;
    float distortionNoise = SAMPLE_TEXTURE2D(_UberNoiseTexture, sampler_UberNoiseTexture, noiseUV).r * 2.0 - 1.0;
#else
    float scanCoordinate = ESCompositeDirectionalCoordinate2D(
        frac(uv),
        _GlitchScanDirection.xy,
        float2(0.0, 1.0));
    float distortionNoise = ESLitRandom(float2(
        floor(scanCoordinate * 128.0),
        floor(timeValue * max(abs(_GlitchSpeed), 0.01)))) * 2.0 - 1.0;
#endif
    uv += direction * distortionNoise * _GlitchIntensity * active;
    return uv;
}

half3 ESLitApplyGlitchColor(half3 color, float2 uv)
{
    float active = ESLitGlitchMask(uv) * saturate(_GlitchFade);
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    float2 noiseUV = uv * _GlitchNoiseScale.xy + ESCompositeTime() * _GlitchNoiseSpeed.xy;
    active *= SAMPLE_TEXTURE2D(_UberNoiseTexture, sampler_UberNoiseTexture, noiseUV).r;
#endif
    half3 shifted = ESCompositeApplyShiftHue(color, ESCompositeTime(), _GlitchHueSpeed)
        * max((half)_GlitchBrightness, 0.0h);
    return lerp(color, shifted, saturate((half)active));
}

float2 ESResolveLitUV(
    float2 uv,
    float3 positionWS,
    float4 positionCS,
    float allowScreenSpaceTiling)
{
    uv = TRANSFORM_TEX(uv, _BaseMap);
    uv = uv * _MainTexScaleOffset.xy + _MainTexScaleOffset.zw;
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_TilingMode > 0.5 && _TilingMode < 1.5)
    {
        uv = ESCompositeResolveTilingUV(
            uv,
            positionWS.xz,
            0.0,
            _ScreenParams.xy,
            _TilingMode,
            _WorldTilingScale.xy,
            _WorldTilingOffset.xy,
            _WorldTilingPixelsPerUnit,
            _ScreenTilingScale.xy,
            _ScreenTilingOffset.xy,
            _ScreenTilingPixelsPerUnit);
    }
    else if (_TilingMode > 1.5 && allowScreenSpaceTiling > 0.5)
    {
        float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
        uv = ESCompositeResolveTilingUV(
            uv,
            positionWS.xz,
            screenUV,
            _ScreenParams.xy,
            _TilingMode,
            _WorldTilingScale.xy,
            _WorldTilingOffset.xy,
            _WorldTilingPixelsPerUnit,
            _ScreenTilingScale.xy,
            _ScreenTilingOffset.xy,
            _ScreenTilingPixelsPerUnit);
    }
    if (_EnableUVTransform > 0.5)
        uv = ESCompositeTransformUV(
            uv,
            _UVPivot.xy,
            _UVScale.xy,
            _UVOffset.xy,
            _UVRotation + _UVRotationSpeed * ESCompositeTime());
#if defined(ES_LIT_COMPILE_UV_RESOURCES)
    if (_EnableUVDistort > 0.5)
    {
        uv = ESCompositeUVDistort(
            uv,
            _UVDistortFrequency.xy,
            _UVDistortSpeed.xy,
            _UVDistortAmount,
            ESCompositeTime());
        float2 noiseUV = uv * _UVDistortFrequency.xy + _UVDistortSpeed.xy * ESCompositeTime();
        float noise = SAMPLE_TEXTURE2D(_UVDistortNoiseTex, sampler_UberNoiseTexture, noiseUV).r;
        float mask = 1.0;
        if (_UVDistortMaskToggle > 0.5)
            mask = ESCompositeSelectChannel(
                SAMPLE_TEXTURE2D(_UVDistortMask, sampler_BaseMap, uv),
                _UVDistortMaskChannel);
        uv = ESCompositeUVDistortNoise(
            uv,
            noise,
            _UVDistortFrom.xy,
            _UVDistortTo.xy,
            _UVDistortFade,
            mask);
    }
#endif
    if (_EnableSqueeze > 0.5)
        uv = ESCompositeApplySqueezeUV(
            uv,
            _SqueezeCenter.xy,
            _SqueezePower,
            _SqueezeScale.xy,
            _SqueezeFade);
    if (_EnableSineRotate > 0.5)
        uv = ESCompositeApplySineRotateUV(
            uv,
            _SineRotatePivot.xy,
            ESCompositeTime(),
            _SineRotateFrequency,
            _SineRotateAngle,
            _SineRotateFade);
#endif
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    if (_EnableFullDistortion > 0.5)
    {
        float fullNoiseX = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
            _UberNoiseTexture,
            sampler_UberNoiseTexture,
            uv * _FullDistortionNoiseScale.xy).r);
        float fullNoiseY = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
            _UberNoiseTexture,
            sampler_UberNoiseTexture,
            (uv + 0.321) * _FullDistortionNoiseScale.xy).r);
        uv += (1.0 - saturate(_FullDistortionFade))
            * (float2(fullNoiseX, fullNoiseY) - 0.5)
            * _FullDistortionDistortion.xy;
    }
#endif
#if defined(ES_LIT_COMPILE_FADE_RESOURCES)
    uv = ESCompositeApplyESNativeDirectionalDistortionUV(uv, frac(uv));
#endif
    return uv;
}

float2 ESBaseUV(float2 uv)
{
    return TRANSFORM_TEX(uv, _BaseMap) * _MainTexScaleOffset.xy + _MainTexScaleOffset.zw;
}

float2 ESApplyFlowMap(float2 uv)
{
    if (_EnableFlow > 0.5)
        uv += _FlowSpeed.xy * ESCompositeTime() * _FlowStrength;
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
#if defined(ES_LIT_COMPILE_UV_RESOURCES)
    if (_EnableFlowMap > 0.5 && abs(_FlowMapStrength) > 0.00001)
    {
        float2 flowUV = uv * _FlowMapScale.xy + _FlowMapScale.zw + _FlowMapSpeed.xy * ESCompositeTime();
        float2 direction = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, flowUV).rg * 2.0 - 1.0;
        uv += direction * _FlowMapStrength;
    }
#endif
#if defined(ES_LIT_COMPILE_FADE_RESOURCES)
    if (_FadeMode > 4.5 && _FadeMode < 5.5 && abs(_FadeDistortionStrength) > 0.00001)
    {
        float2 coordinate = frac(uv);
        float2 noiseUV = coordinate * _FadeNoiseScale.xy + _FadeNoiseSpeed.xy * ESCompositeTime();
        float noise = 0.5;
        if ((_FadeMode > 2.5 && _FadeMode < 5.5) || _FadeNoiseFactor > 0.0001)
            noise = SAMPLE_TEXTURE2D(_FadeNoiseTex, sampler_UberNoiseTexture, noiseUV).r;
        float mask = ESCompositeDirectionalFadeMask(coordinate, _FadePosition.xy, _FadeRotation);
        mask = ESCompositeApplyFadeNoise(mask, noise, _FadeNoiseFactor);
        float visibility;
        float edge;
        ESCompositeEvaluateFade(mask, _FadeProgress, _FadeWidth, _DissolveEdgeWidth, _FadeInvert, visibility, edge);
        float2 direction = ESCompositeFadeDirection(_FadeRotation);
        uv += float2(-direction.y, direction.x) * (noise * 2.0 - 1.0) * edge * _FadeDistortionStrength;
    }
#endif
#endif
    return uv;
}

float2 ESApplyLitStylizedAndPixelUV(
    float2 uv,
    float3 positionWS,
    out float2 stylizedCoordinate,
    out float hologramCoordinate)
{
    stylizedCoordinate = uv;
    hologramCoordinate = 0.0;
#if defined(_ES_QUALITY_HIGH)
    if (_ESNativeStatusContract > 0.5)
    {
        if (_EnableHologram > 0.5)
        {
            hologramCoordinate = ESCompositeResolveESNativeHologramCoordinate(
                stylizedCoordinate,
                positionWS);
            uv = ESCompositeApplyESNativeHologramUV(
                uv,
                hologramCoordinate,
                _BaseMap_TexelSize.z,
                ESCompositeTime());
        }
        if (_EnableGlitch > 0.5)
            uv = ESCompositeApplyESNativeGlitchUV(
                uv,
                stylizedCoordinate,
                ESCompositeTime());
    }
    else
    {
        if (_EnableHologram > 0.5)
            uv = ESLitApplyHologramUV(uv, positionWS);
        if (_EnableGlitch > 0.5)
            uv = ESLitApplyGlitchUV(uv);
    }
#endif
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_EnableSmoothPixelArt > 0.5)
        uv = ESCompositeSmoothPixelUV(uv, _BaseMap_TexelSize.zw, _SmoothPixelStrength);
    if (_EnablePixelate > 0.5)
    {
        float cellsX = max(2.0, _PixelateCells);
        float aspect = _BaseMap_TexelSize.z / max(_BaseMap_TexelSize.w, 1.0);
        float2 cells = float2(cellsX, max(2.0, cellsX / max(aspect, 0.0001)));
        float2 snapped = (floor(uv * cells) + 0.5) / cells;
        uv = lerp(uv, snapped, saturate(_PixelateStrength));
    }
#endif
    return uv;
}

float2 ESResolveLitSurfaceUV(
    float2 uv,
    float3 positionWS,
    float4 positionCS,
    out float2 stylizedCoordinate,
    out float hologramCoordinate)
{
    uv = ESResolveLitUV(uv, positionWS, positionCS, 1.0);
    uv = ESApplyFlowMap(uv);
    return ESApplyLitStylizedAndPixelUV(
        uv,
        positionWS,
        stylizedCoordinate,
        hologramCoordinate);
}

float2 ESResolveLitShadowSurfaceUV(
    float2 uv,
    float3 positionWS,
    float4 positionCS,
    out float2 stylizedCoordinate,
    out float hologramCoordinate)
{
    uv = ESResolveLitUV(uv, positionWS, positionCS, 0.0);
    uv = ESApplyFlowMap(uv);
    return ESApplyLitStylizedAndPixelUV(
        uv,
        positionWS,
        stylizedCoordinate,
        hologramCoordinate);
}

float ESApplyLitFade(float2 uv, float vertexAlpha, out float edge)
{
    edge = 0.0;
    float visibility = 1.0;
#if defined(ES_LIT_COMPILE_FADE_RESOURCES)
    if (_FadeMode > 0.5)
    {
        float2 coordinate = frac(uv);
        float2 noiseUV = coordinate * _FadeNoiseScale.xy + _FadeNoiseSpeed.xy * ESCompositeTime();
        float noise = SAMPLE_TEXTURE2D(_FadeNoiseTex, sampler_UberNoiseTexture, noiseUV).r;
        float mask = 1.0;
        if (_FadeMode < 1.5 || (_FadeMode > 3.5 && _FadeMode < 5.5))
            mask = ESCompositeDirectionalFadeMask(coordinate, _FadePosition.xy, _FadeRotation);
        else if (_FadeMode < 2.5)
            mask = SAMPLE_TEXTURE2D(_FadeMask, sampler_BaseMap, coordinate).r;
        else if (_FadeMode < 5.5)
            mask = noise;
        else
            mask = ESCompositeSourceFadeMask(coordinate, _FadePosition.xy);
        if (!(_FadeMode > 2.5 && _FadeMode < 3.5))
            mask = ESCompositeApplyFadeNoise(mask, noise, _FadeNoiseFactor);
        ESCompositeEvaluateFade(mask, _FadeProgress, _FadeWidth, _DissolveEdgeWidth, _FadeInvert, visibility, edge);
    }
    if (_EnableCustomFade > 0.5)
    {
        float2 customMaskUV = uv * _CustomFadeFadeMask_ST.xy + _CustomFadeFadeMask_ST.zw;
        float customMask = SAMPLE_TEXTURE2D(_CustomFadeFadeMask, sampler_BaseMap, customMaskUV).r;
        float customNoise = SAMPLE_TEXTURE2D(_UberNoiseTexture, sampler_UberNoiseTexture, uv * _CustomFadeNoiseScale.xy).r;
        visibility *= ESCompositeCustomFadeVisibility(
            vertexAlpha,
            customMask,
            ESCompositePerceptualNoise(customNoise),
            _CustomFadeSmoothness,
            _CustomFadeNoiseFactor,
            _CustomFadeAlpha);
    }
#endif
    return visibility;
}

// Vertex Contracts
struct ES3DLitAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ES3DLitVaryings
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float4 tangentWS : TEXCOORD3;
    #ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight : TEXCOORD4;
    #else
    half fogFactor : TEXCOORD4;
    #endif
    float4 shadowCoord : TEXCOORD5;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);
    #if defined(DYNAMICLIGHTMAP_ON)
    float2 dynamicLightmapUV : TEXCOORD7;
    #endif
    float4 vertexColor : TEXCOORD8;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Surface Sampling And Dissolve
float ESNoise(float3 positionWS)
{
#if defined(ES_LIT_COMPILE_FADE_RESOURCES) || defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    float2 uv = positionWS.xz * _NoiseScale.xy + positionWS.y * _NoiseScale.zw + _NoiseSpeed.xy * ESCompositeTime();
    return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r;
#else
    return 1.0;
#endif
}

half4 ESBlurBaseSample(float2 uv, half4 center)
{
    float2 delta = _BaseMap_TexelSize.xy * (_BlurRadius * 512.0);
    half4 result = center * 0.4h;
    result += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(delta.x, 0)) * 0.15h;
    result += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - float2(delta.x, 0)) * 0.15h;
    result += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(0, delta.y)) * 0.15h;
    result += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - float2(0, delta.y)) * 0.15h;
    return result;
}

half4 ESLitSharpenSample(float2 uv, half4 center)
{
    float2 delta = _BaseMap_TexelSize.xy * (_SharpenRadius * 512.0);
    half4 axis = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(delta.x, 0.0));
    axis += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - float2(delta.x, 0.0));
    axis += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(0.0, delta.y));
    axis += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - float2(0.0, delta.y));
    return ESCompositeSharpen5(center, axis * 0.25h, _SharpenAmount, _SharpenThreshold);
}

float ESLitSmokeMask(float2 uv, float4 vertexColor)
{
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    float seed = _SmokeVertexSeed > 0.5 ? vertexColor.r * 5.0 : 0.0;
    float2 noiseUV = (
        uv + ESCompositeTime() * _SmokeSpeed.xy + seed)
        * max(_SmokeNoiseScale, 0.01);
    float noise = ESCompositePerceptualNoise(
        SAMPLE_TEXTURE2D(_UberNoiseTexture, sampler_UberNoiseTexture, noiseUV).r);
    return ESCompositeSmokeMask(
        noise,
        frac(uv),
        vertexColor.a,
        _SmokeNoiseFactor,
        _SmokeSmoothness);
#else
    return 1.0;
#endif
}

float ESLitFlameMask(float2 uv)
{
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    float2 noiseUV = (uv + ESCompositeTime() * _FlameSpeed.xy) * _FlameNoiseScale.xy;
    float noise = ESCompositePerceptualNoise(
        SAMPLE_TEXTURE2D(_UberNoiseTexture, sampler_UberNoiseTexture, noiseUV).r);
    return ESCompositeFlameMask(
        noise,
        frac(uv),
        _FlameCenter.xy,
        _FlameDirection.xy,
        _FlameRadius,
        _FlameSmooth,
        _FlameNoiseFactor,
        _FlameNoiseHeightFactor);
#else
    return 1.0;
#endif
}

half ESLitHalftoneVisibility(float2 uv)
{
    float radialFade = max(
        (_HalftoneFade - distance(_HalftonePosition.xy, uv))
        / max(abs(_HalftoneFadeWidth), 0.01),
        0.0001);
    float2 ssuCell = (frac(abs(uv) * max(4.0, _HalftoneScale)) * 2.0 - 1.0) / radialFade;
    float ssuDistance = length(ssuCell);
    float ssuAA = max(fwidth(ssuDistance), 0.0001);
    half visibility = (half)saturate((1.0 - ssuDistance) / ssuAA);
    return _HalftoneInvert > 0.5 ? 1.0h - visibility : visibility;
}

half3 ESLitApplyHalftone(half3 color, float2 uv, out half visibility)
{
    float angle = radians(_HalftoneAngle);
    float2 directionX = float2(cos(angle), -sin(angle));
    float2 directionY = float2(sin(angle), cos(angle));
    float2 rotated = float2(dot(uv, directionX), dot(uv, directionY));
    float2 cell = frac(rotated * max(4.0, _HalftoneScale)) - 0.5;
    float luminance = saturate(dot(color, half3(0.2126h, 0.7152h, 0.0722h)));
    float radius = sqrt(1.0 - luminance) * 0.5;
    float distanceToCenter = length(cell);
    float antialias = max(fwidth(distanceToCenter), 0.001);
    float ink = 1.0 - smoothstep(radius - antialias, radius + antialias, distanceToCenter);
    visibility = ESLitHalftoneVisibility(uv);
    return color * lerp(1.0h, 1.0h - (half)ink, saturate((half)_HalftoneStrength));
}

float2 ESLitLayerUV(
    float2 baseUV,
    float4 scale,
    float4 offset,
    float4 scrollSpeed,
    float scrollToggle,
    float sheetToggle,
    float columns,
    float rows,
    float speed,
    float startFrame,
    float edgeClip)
{
    float2 uv = baseUV * scale.xy - offset.xy;
    if (scrollToggle > 0.5)
        uv -= scrollSpeed.xy * ESCompositeTime();
    if (sheetToggle > 0.5)
    {
        float2 sheet = max(float2(1.0, 1.0), round(float2(columns, rows)));
        float frameCount = max(1.0, sheet.x * sheet.y);
        float frame = round(startFrame + ESCompositeTime() * speed);
        frame -= floor(frame / frameCount) * frameCount;
        float2 tile = float2(fmod(frame, sheet.x), floor(frame / sheet.x));
        float2 local = frac(uv);
        float edge = saturate(edgeClip);
        local = lerp(edge.xx, (1.0 - edge).xx, local);
        uv = (local + tile) / sheet;
    }
    return uv;
}

half3 ESLitApplyLayerContrast(half3 layerColor, half3 baseColor, float enabled, float contrast)
{
    if (enabled > 0.5)
    {
        float luminance = dot(baseColor, half3(0.2126h, 0.7152h, 0.0722h));
        layerColor *= pow(max(luminance, 0.001), max(contrast, 0.01));
    }
    return layerColor;
}

half3 ESLitApplyTextureLayers(half3 baseColor, float2 uv)
{
    half3 color = baseColor;
#if defined(ES_LIT_COMPILE_LAYER_RESOURCES)
    if (_EnableTextureLayer1 > 0.5)
    {
        float2 layerUV = ESLitLayerUV(
            uv, _TextureLayer1Scale, _TextureLayer1Offset, _TextureLayer1ScrollSpeed,
            _TextureLayer1ScrollToggle, _TextureLayer1SheetToggle,
            _TextureLayer1Columns, _TextureLayer1Rows, _TextureLayer1Speed,
            _TextureLayer1StartFrame, _TextureLayer1EdgeClip);
        half4 layerSample = SAMPLE_TEXTURE2D(_TextureLayer1Texture, sampler_BaseMap, layerUV);
        half3 layerColor = ESLitApplyLayerContrast(
            layerSample.rgb * _TextureLayer1Color.rgb,
            color,
            _TextureLayer1ContrastToggle,
            _TextureLayer1Contrast);
        color = lerp(color, layerColor, saturate(layerSample.a * _TextureLayer1Fade));
    }
    if (_EnableTextureLayer2 > 0.5)
    {
        float2 layerUV = ESLitLayerUV(
            uv, _TextureLayer2Scale, _TextureLayer2Offset, _TextureLayer2ScrollSpeed,
            _TextureLayer2ScrollToggle, _TextureLayer2SheetToggle,
            _TextureLayer2Columns, _TextureLayer2Rows, _TextureLayer2Speed,
            _TextureLayer2StartFrame, _TextureLayer2EdgeClip);
        half4 layerSample = SAMPLE_TEXTURE2D(_TextureLayer2Texture, sampler_BaseMap, layerUV);
        half3 layerColor = ESLitApplyLayerContrast(
            layerSample.rgb * _TextureLayer2Color.rgb,
            color,
            _TextureLayer2ContrastToggle,
            _TextureLayer2Contrast);
        color = lerp(color, layerColor, saturate(layerSample.a * _TextureLayer2Fade));
    }
#endif
    return color;
}

half ESLitBaseAlpha(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
}

float2 ESLitOutlineWidth()
{
    if (_EnablePixelOutline > 0.5)
        return _BaseMap_TexelSize.xy * max(_PixelOutlineWidth, 0.0);
    return max(_OuterOutlineWidth, 0.0).xx;
}

float2 ESLitOutlineDistortedUV(
    float2 uv,
    float enabled,
    float2 intensity,
    float2 scale,
    float2 speed)
{
    if (enabled < 0.5) return uv;
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    float2 noiseUV = uv * scale + ESCompositeTime() * speed;
    float noise = SAMPLE_TEXTURE2D(_UberNoiseTexture, sampler_UberNoiseTexture, noiseUV).r * 2.0 - 1.0;
    return uv + noise * intensity;
#else
    float2 cell = floor((uv * scale + ESCompositeTime() * speed) * 64.0);
    float2 noise = float2(
        ESLitRandom(cell + 13.0),
        ESLitRandom(cell.yx + 37.0)) * 2.0 - 1.0;
    return uv + noise * intensity;
#endif
}

half ESLitMaxNeighbourAlpha(float2 uv, float2 width)
{
    half value = 0.0h;
    value = max(value, ESLitBaseAlpha(uv + float2(width.x, 0.0)));
    value = max(value, ESLitBaseAlpha(uv - float2(width.x, 0.0)));
    value = max(value, ESLitBaseAlpha(uv + float2(0.0, width.y)));
    value = max(value, ESLitBaseAlpha(uv - float2(0.0, width.y)));
    value = max(value, ESLitBaseAlpha(uv + width));
    value = max(value, ESLitBaseAlpha(uv - width));
    value = max(value, ESLitBaseAlpha(uv + float2(width.x, -width.y)));
    value = max(value, ESLitBaseAlpha(uv + float2(-width.x, width.y)));
    return value;
}

half ESLitMinNeighbourAlpha(float2 uv, float2 width)
{
    half value = 1.0h;
    value = min(value, ESLitBaseAlpha(uv + float2(width.x, 0.0)));
    value = min(value, ESLitBaseAlpha(uv - float2(width.x, 0.0)));
    value = min(value, ESLitBaseAlpha(uv + float2(0.0, width.y)));
    value = min(value, ESLitBaseAlpha(uv - float2(0.0, width.y)));
    value = min(value, ESLitBaseAlpha(uv + width));
    value = min(value, ESLitBaseAlpha(uv - width));
    value = min(value, ESLitBaseAlpha(uv + float2(width.x, -width.y)));
    value = min(value, ESLitBaseAlpha(uv + float2(-width.x, width.y)));
    return value;
}

half ESLitAroundAlpha(float2 uv)
{
    float2 width = ESLitOutlineWidth();
    if (_EnablePixelOutline < 0.5)
        uv = ESLitOutlineDistortedUV(
            uv,
            _OuterOutlineDistortionToggle,
            _OuterOutlineDistortionIntensity.xy,
            _OuterOutlineNoiseScale.xy,
            _OuterOutlineNoiseSpeed.xy);
    return ESLitMaxNeighbourAlpha(uv, width);
}

half ESLitInnerOutlineEdge(float2 uv, half sourceAlpha)
{
    uv = ESLitOutlineDistortedUV(
        uv,
        _InnerOutlineDistortionToggle,
        _InnerOutlineDistortionIntensity.xy,
        _InnerOutlineNoiseScale.xy,
        _InnerOutlineNoiseSpeed.xy);
    half inner = ESLitMinNeighbourAlpha(uv, max(_InnerOutlineWidth, 0.0).xx);
    return saturate(sourceAlpha - inner) * saturate((half)_InnerOutlineFade);
}

half ESLitOuterOutlineEdge(float2 uv, half sourceAlpha)
{
    half fade = _EnablePixelOutline > 0.5 ? (half)_PixelOutlineFade : (half)_OuterOutlineFade;
    return saturate(ESLitAroundAlpha(uv) - sourceAlpha) * saturate(fade);
}

half3 ESLitOutlineTint(float2 uv, bool pixel)
{
    half3 tint = pixel ? _PixelOutlineColor.rgb : _OuterOutlineColor.rgb;
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    if (pixel && _PixelOutlineTextureToggle > 0.5)
        tint *= SAMPLE_TEXTURE2D(
            _PixelOutlineTintTexture,
            sampler_BaseMap,
            uv + ESCompositeTime() * _PixelOutlineTextureSpeed.xy).rgb;
    else if (!pixel && _OuterOutlineTextureToggle > 0.5)
        tint *= SAMPLE_TEXTURE2D(
            _OuterOutlineTintTexture,
            sampler_BaseMap,
            uv + ESCompositeTime() * _OuterOutlineTextureSpeed.xy).rgb;
#endif
    return tint;
}

half ESLitESNativeMinNeighbourAlpha8(float2 uv, float2 width)
{
    const float diagonal = 0.705;
    half value = 1.0h;
    value = min(value, ESLitBaseAlpha(uv + float2(0.0, -width.y)));
    value = min(value, ESLitBaseAlpha(uv + float2(0.0, width.y)));
    value = min(value, ESLitBaseAlpha(uv + float2(-width.x, 0.0)));
    value = min(value, ESLitBaseAlpha(uv + float2(width.x, 0.0)));
    value = min(value, ESLitBaseAlpha(uv + width * diagonal));
    value = min(value, ESLitBaseAlpha(uv + float2(-width.x, width.y) * diagonal));
    value = min(value, ESLitBaseAlpha(uv + float2(width.x, -width.y) * diagonal));
    value = min(value, ESLitBaseAlpha(uv - width * diagonal));
    return value;
}

half ESLitESNativeMaxNeighbourAlpha8(float2 uv, float2 width)
{
    const float diagonal = 0.705;
    half value = 0.0h;
    value = max(value, ESLitBaseAlpha(uv + float2(0.0, -width.y)));
    value = max(value, ESLitBaseAlpha(uv + float2(0.0, width.y)));
    value = max(value, ESLitBaseAlpha(uv + float2(-width.x, 0.0)));
    value = max(value, ESLitBaseAlpha(uv + float2(width.x, 0.0)));
    value = max(value, ESLitBaseAlpha(uv + width * diagonal));
    value = max(value, ESLitBaseAlpha(uv + float2(-width.x, width.y) * diagonal));
    value = max(value, ESLitBaseAlpha(uv + float2(width.x, -width.y) * diagonal));
    value = max(value, ESLitBaseAlpha(uv - width * diagonal));
    return value;
}

half ESLitESNativeMaxNeighbourAlpha4(float2 uv, float2 width)
{
    half value = 0.0h;
    value = max(value, ESLitBaseAlpha(uv + float2(0.0, -width.y)));
    value = max(value, ESLitBaseAlpha(uv + float2(0.0, width.y)));
    value = max(value, ESLitBaseAlpha(uv + float2(-width.x, 0.0)));
    value = max(value, ESLitBaseAlpha(uv + float2(width.x, 0.0)));
    return value;
}

void ESLitApplyESNativeOutlines(float2 uv, inout half4 color)
{
    float timeValue = ESCompositeTime();
    if (_EnableInnerOutline > 0.5)
    {
        float2 distortedUV = ESCompositeESNativeOutlineDistortedUV(
            uv,
            _InnerOutlineDistortionToggle,
            _InnerOutlineDistortionIntensity.xy,
            _InnerOutlineNoiseScale.xy,
            _InnerOutlineNoiseSpeed.xy,
            timeValue);
        half3 tint = _InnerOutlineColor.rgb;
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
        if (_InnerOutlineTextureToggle > 0.5)
            tint *= SAMPLE_TEXTURE2D(
                _InnerOutlineTintTexture,
                sampler_BaseMap,
                uv + timeValue * _InnerOutlineTextureSpeed.xy).rgb;
#endif
        color = ESCompositeApplyESNativeInnerOutline(
            color,
            ESLitESNativeMinNeighbourAlpha8(
                distortedUV,
                max(_InnerOutlineWidth, 0.0) * 100.0 * _BaseMap_TexelSize.xy),
            tint,
            _InnerOutlineFade,
            _InnerOutlineOutlineOnlyToggle);
    }
    if (_EnableOuterOutline > 0.5)
    {
        float2 distortedUV = ESCompositeESNativeOutlineDistortedUV(
            uv,
            _OuterOutlineDistortionToggle,
            _OuterOutlineDistortionIntensity.xy,
            _OuterOutlineNoiseScale.xy,
            _OuterOutlineNoiseSpeed.xy,
            timeValue);
        half3 tint = _OuterOutlineColor.rgb;
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
        if (_OuterOutlineTextureToggle > 0.5)
            tint *= SAMPLE_TEXTURE2D(
                _OuterOutlineTintTexture,
                sampler_BaseMap,
                uv + timeValue * _OuterOutlineTextureSpeed.xy).rgb;
#endif
        color = ESCompositeApplyESNativeOuterOutline(
            color,
            ESLitESNativeMaxNeighbourAlpha8(
                distortedUV,
                max(_OuterOutlineWidth, 0.0) * 100.0 * _BaseMap_TexelSize.xy),
            tint,
            _OuterOutlineFade,
            _OuterOutlineOutlineOnlyToggle);
    }
    if (_EnablePixelOutline > 0.5)
    {
        half3 tint = _PixelOutlineColor.rgb;
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
        if (_PixelOutlineTextureToggle > 0.5)
            tint *= SAMPLE_TEXTURE2D(
                _PixelOutlineTintTexture,
                sampler_BaseMap,
                uv + timeValue * _PixelOutlineTextureSpeed.xy).rgb;
#endif
        color = ESCompositeApplyESNativeOuterOutline(
            color,
            ESLitESNativeMaxNeighbourAlpha4(
                uv,
                max(_PixelOutlineWidth, 0.0) * _BaseMap_TexelSize.xy),
            tint,
            _PixelOutlineFade,
            _PixelOutlineOutlineOnlyToggle);
    }
}

half ESLitApplyESNativeOutlineAlpha(float2 uv, half alpha)
{
    float timeValue = ESCompositeTime();
    half4 probe = half4(0.0h, 0.0h, 0.0h, alpha);
    if (_EnableInnerOutline > 0.5)
    {
        float2 distortedUV = ESCompositeESNativeOutlineDistortedUV(
            uv,
            _InnerOutlineDistortionToggle,
            _InnerOutlineDistortionIntensity.xy,
            _InnerOutlineNoiseScale.xy,
            _InnerOutlineNoiseSpeed.xy,
            timeValue);
        probe = ESCompositeApplyESNativeInnerOutline(
            probe,
            ESLitESNativeMinNeighbourAlpha8(
                distortedUV,
                max(_InnerOutlineWidth, 0.0) * 100.0 * _BaseMap_TexelSize.xy),
            0.0h,
            _InnerOutlineFade,
            _InnerOutlineOutlineOnlyToggle);
    }
    if (_EnableOuterOutline > 0.5)
    {
        float2 distortedUV = ESCompositeESNativeOutlineDistortedUV(
            uv,
            _OuterOutlineDistortionToggle,
            _OuterOutlineDistortionIntensity.xy,
            _OuterOutlineNoiseScale.xy,
            _OuterOutlineNoiseSpeed.xy,
            timeValue);
        probe = ESCompositeApplyESNativeOuterOutline(
            probe,
            ESLitESNativeMaxNeighbourAlpha8(
                distortedUV,
                max(_OuterOutlineWidth, 0.0) * 100.0 * _BaseMap_TexelSize.xy),
            0.0h,
            _OuterOutlineFade,
            _OuterOutlineOutlineOnlyToggle);
    }
    if (_EnablePixelOutline > 0.5)
    {
        probe = ESCompositeApplyESNativeOuterOutline(
            probe,
            ESLitESNativeMaxNeighbourAlpha4(
                uv,
                max(_PixelOutlineWidth, 0.0) * _BaseMap_TexelSize.xy),
            0.0h,
            _PixelOutlineFade,
            _PixelOutlineOutlineOnlyToggle);
    }
    return probe.a;
}

void ESLitApplyOutlines(float2 uv, half sourceAlpha, inout half4 color)
{
    if (_ESNativeStatusContract > 0.5)
    {
        ESLitApplyESNativeOutlines(uv, color);
        return;
    }
    if (_EnableInnerOutline > 0.5)
    {
        half edge = ESLitInnerOutlineEdge(uv, sourceAlpha);
        half3 tint = _InnerOutlineColor.rgb;
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
        if (_InnerOutlineTextureToggle > 0.5)
            tint *= SAMPLE_TEXTURE2D(
                _InnerOutlineTintTexture,
                sampler_BaseMap,
                uv + ESCompositeTime() * _InnerOutlineTextureSpeed.xy).rgb;
#endif
        color.rgb = lerp(color.rgb, tint, edge);
        if (_InnerOutlineOutlineOnlyToggle > 0.5)
            color.a = edge;
    }
    if (_EnableOuterOutline > 0.5 || _EnablePixelOutline > 0.5)
    {
        bool pixel = _EnablePixelOutline > 0.5;
        half edge = ESLitOuterOutlineEdge(uv, sourceAlpha);
        half3 outlineColor = ESLitOutlineTint(uv, pixel);
        color.rgb = lerp(color.rgb, outlineColor, edge);
        bool outlineOnly = pixel
            ? _PixelOutlineOutlineOnlyToggle > 0.5
            : _OuterOutlineOutlineOnlyToggle > 0.5;
        color.a = outlineOnly ? edge : max(color.a, edge);
    }
}

half ESLitFullGlowVisibility(float2 uv)
{
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    float noise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
        _UberNoiseTexture,
        sampler_UberNoiseTexture,
        frac(uv) * _FullGlowDissolveNoiseScale.xy).r);
    half visibility;
    ESCompositeApplyFullGlowDissolve(
        half4(0.0h, 0.0h, 0.0h, 1.0h),
        noise,
        _FullGlowDissolveFade,
        _FullGlowDissolveWidth,
        _FullGlowDissolveEdgeColor.rgb,
        visibility);
    return visibility;
#else
    return 1.0h;
#endif
}

half ESLitHologramVisibility(float2 uv, float3 positionWS)
{
    float coordinate = ESLitHologramCoordinate(uv, positionWS);
    float phase = frac((coordinate + ESCompositeTime() * _HologramSpeed) * _HologramLineFrequency);
    float antialias = max(fwidth(phase), 0.0005);
    float scanLine = smoothstep(
        saturate(_HologramLineGap) - antialias,
        saturate(_HologramLineGap) + antialias,
        phase);
    scanLine = pow(saturate(scanLine), max(_HologramContrast, 0.01));
    half visibility = max(saturate((half)_HologramMinAlpha), (half)scanLine);
    return lerp(1.0h, visibility, saturate((half)_HologramFade));
}

half ESLitShadowAlpha(float2 uv)
{
    float2 offset = _ShadowOffset.xy * 100.0 * _BaseMap_TexelSize.xy;
    return ESLitBaseAlpha(uv - offset) * saturate((half)_ShadowFade);
}

float ESDissolveSource(float3 positionWS)
{
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_DissolveMode < 0.5) return 1.0;
    if (_DissolveMode > 1.5) return saturate(distance(positionWS, _WorldSpaceCameraPos) * 0.02);
    return ESNoise(positionWS);
#else
    return 1.0;
#endif
}

float ESDissolveAlpha(float3 positionWS, out float edge)
{
    edge = 0.0;
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_DissolveMode < 0.5) return 1.0;
    float source = ESDissolveSource(positionWS);
    float alpha = smoothstep(_DissolveProgress - _DissolveSoftness, _DissolveProgress + _DissolveSoftness, source);
    edge = 1.0 - smoothstep(_DissolveProgress, _DissolveProgress + _DissolveEdgeWidth, source);
    return alpha;
#else
    return 1.0;
#endif
}

float ESComputeLitAlpha(
    float2 uv,
    float2 stylizedCoordinate,
    float hologramCoordinate,
    float3 positionWS,
    float4 vertexColor,
    out float edge)
{
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
#if defined(_ES_QUALITY_HIGH)
    half sourceAlpha = alpha;
    if (_ESNativeStatusContract > 0.5)
    {
        if (_EnableInnerOutline > 0.5 || _EnableOuterOutline > 0.5 || _EnablePixelOutline > 0.5)
            alpha = ESLitApplyESNativeOutlineAlpha(uv, alpha);
    }
    else
    {
        if (_EnableInnerOutline > 0.5 && _InnerOutlineOutlineOnlyToggle > 0.5)
            alpha = ESLitInnerOutlineEdge(uv, sourceAlpha);
        if (_EnableOuterOutline > 0.5 || _EnablePixelOutline > 0.5)
        {
            half outlineEdge = ESLitOuterOutlineEdge(uv, sourceAlpha);
            bool outlineOnly = _EnablePixelOutline > 0.5
                ? _PixelOutlineOutlineOnlyToggle > 0.5
                : _OuterOutlineOutlineOnlyToggle > 0.5;
            alpha = outlineOnly ? outlineEdge : max(alpha, outlineEdge);
        }
    }
#endif
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    if (_EnableSmoke > 0.5)
        alpha *= ESLitSmokeMask(uv, vertexColor) * saturate(_SmokeAlpha);
    if (_EnableFlame > 0.5)
        alpha *= ESLitFlameMask(uv);
#endif
#endif
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_EnableHalftone > 0.5 && _HalftoneAlphaPattern > 0.5)
        alpha *= ESLitHalftoneVisibility(uv);
#endif
#if defined(_ES_QUALITY_HIGH)
    if (_EnableHologram > 0.5)
    {
        if (_ESNativeStatusContract > 0.5)
            alpha = ESCompositeApplyESNativeHologramColor(
                half4(0.0h, 0.0h, 0.0h, alpha),
                hologramCoordinate,
                ESCompositeTime()).a;
        else
            alpha *= ESLitHologramVisibility(uv, positionWS);
    }
#endif
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    half fullGlowVisibility = 1.0h;
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    if (_EnableFullGlowDissolve > 0.5)
    {
        fullGlowVisibility = ESLitFullGlowVisibility(uv);
        alpha *= fullGlowVisibility;
    }
    if (_EnableShadow > 0.5)
    {
        half shadowAlpha = ESLitShadowAlpha(uv) * fullGlowVisibility;
        alpha = alpha + shadowAlpha * (1.0h - alpha);
    }
#else
    if (_EnableShadow > 0.5)
    {
        half shadowAlpha = ESLitShadowAlpha(uv);
        alpha = alpha + shadowAlpha * (1.0h - alpha);
    }
#endif
#endif
    float dissolveEdge;
    alpha *= ESDissolveAlpha(positionWS, dissolveEdge);
    float fadeEdge;
    alpha *= ESApplyLitFade(uv, vertexColor.a, fadeEdge);
#if defined(ES_LIT_COMPILE_FADE_RESOURCES)
    half3 unusedFadeColor = 0.0h;
    float ssuFadeVisibility;
    unusedFadeColor = ESCompositeApplyESNativeFadeStackColor(
        unusedFadeColor,
        frac(uv),
        ssuFadeVisibility);
    alpha *= ssuFadeVisibility;
#endif
    edge = max(dissolveEdge, fadeEdge);
    return alpha;
}

void ESInitializeSurface(
    float2 uv,
    float2 stylizedCoordinate,
    float hologramCoordinate,
    float3 positionWS,
    float4 vertexColor,
    out SurfaceData surfaceData,
    out float dissolveAlpha,
    out float dissolveEdge)
{
    surfaceData = (SurfaceData)0;
    half4 baseTextureSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    half4 baseSample = baseTextureSample * _BaseColor;
    half sourceCoverage = baseSample.a;
#if defined(_ES_QUALITY_HIGH)
    if (_EnableSharpen > 0.5 && _SharpenFade > 0.0001 && _SharpenAmount > 0.0001)
    {
        half4 sharpened = ESLitSharpenSample(uv, baseTextureSample) * _BaseColor;
        baseSample = lerp(baseSample, sharpened, saturate(_SharpenFade));
    }
#endif
    if (_EnableBlur > 0.5 && _BlurIntensity > 0.0001 && _BlurRadius > 0.0001)
        baseSample = lerp(
            baseSample,
            ESBlurBaseSample(uv, baseTextureSample) * _BaseColor,
            saturate(_BlurIntensity));
    // Sampling filters are color treatments. Coverage remains governed by the
    // source texture and the shared alpha stack used by Depth/Shadow passes.
    baseSample.a = sourceCoverage;
    if (_EnableChromatic > 0.5
        && _ChromaticIntensity > 0.0001
        && abs(_ChromaticOffset) > 0.000001)
    {
        float2 chromaDir = float2(cos(radians(_ChromaticAngle)), sin(radians(_ChromaticAngle)));
        float2 localCoord = frac(uv);
        float edgeFactor = saturate(length(localCoord - 0.5) * 2.0);
        float amount = _ChromaticOffset * lerp(1.0, edgeFactor, _ChromaticEdgeOnly);
        half3 chroma = baseSample.rgb;
        chroma.r = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + chromaDir * amount).r * _BaseColor.r;
        chroma.b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - chromaDir * amount).b * _BaseColor.b;
        baseSample.rgb = lerp(baseSample.rgb, chroma, saturate(_ChromaticIntensity));
    }
#if defined(_ES_QUALITY_HIGH)
    if (_ESNativeStatusContract <= 0.5 && _EnableGlitch > 0.5)
        baseSample.rgb = ESLitApplyGlitchColor(baseSample.rgb, uv);
#endif
#if defined(_ES_QUALITY_HIGH)
    if (_EnableInnerOutline > 0.5 || _EnableOuterOutline > 0.5 || _EnablePixelOutline > 0.5)
        ESLitApplyOutlines(uv, baseSample.a, baseSample);
#endif
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    if (_EnableSmoke > 0.5)
        baseSample = ESCompositeApplySmoke(
            baseSample,
            ESLitSmokeMask(uv, vertexColor),
            _SmokeAlpha,
            _SmokeDarkEdge);
    if (_EnableCheckerboard > 0.5)
        baseSample.rgb = ESCompositeApplyCheckerboard(
            baseSample.rgb,
            positionWS.xz,
            _CheckerboardTiling,
            _CheckerboardDarken);
    if (_EnableFlame > 0.5)
    {
        float flameMask = ESLitFlameMask(uv);
        baseSample.rgb *= flameMask * _FlameBrightness;
        baseSample.a *= flameMask;
    }
#endif
#endif
    float fadeEdge;
    dissolveAlpha = ESDissolveAlpha(positionWS, dissolveEdge)
        * ESApplyLitFade(uv, vertexColor.a, fadeEdge);
    dissolveEdge = max(dissolveEdge, fadeEdge);
    half3 ssuEmission = 0.0h;
#if defined(ES_LIT_COMPILE_FADE_RESOURCES)
    half3 beforeFadeStack = baseSample.rgb;
    float ssuFadeVisibility;
    baseSample.rgb = ESCompositeApplyESNativeFadeStackColor(
        baseSample.rgb,
        frac(uv),
        ssuFadeVisibility);
    dissolveAlpha *= ssuFadeVisibility;
    ssuEmission += max(baseSample.rgb - beforeFadeStack, 0.0h);
#endif
#if defined(_ES_QUALITY_HIGH)
    if (_ESNativeStatusContract > 0.5)
    {
        if (_EnableHologram > 0.5)
            baseSample = ESCompositeApplyESNativeHologramColor(
                baseSample,
                hologramCoordinate,
                ESCompositeTime());
        if (_EnableGlitch > 0.5)
            baseSample.rgb = ESCompositeApplyESNativeGlitchColor(
                baseSample.rgb,
                stylizedCoordinate,
                ESCompositeTime());
    }
#endif
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    ESApplyLitESNativeSurfaceEffects(
        uv,
        positionWS,
        baseSample.a * dissolveAlpha,
        baseSample.rgb,
        ssuEmission);
#endif
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
#if defined(ES_LIT_COMPILE_LAYER_RESOURCES)
    baseSample.rgb = ESLitApplyTextureLayers(baseSample.rgb, uv);
#endif
    if (_EnableHalftone > 0.5)
    {
        half halftoneVisibility;
        baseSample.rgb = ESLitApplyHalftone(baseSample.rgb, uv, halftoneVisibility);
        baseSample.a *= lerp(1.0h, halftoneVisibility, step(0.5, _HalftoneAlphaPattern));
    }
#endif
#if defined(_ES_QUALITY_HIGH)
    if (_ESNativeStatusContract <= 0.5 && _EnableHologram > 0.5)
    {
        half hologramVisibility = ESLitHologramVisibility(uv, positionWS);
        baseSample.rgb = lerp(baseSample.rgb, _HologramColor.rgb, saturate((half)_HologramFade) * 0.55h);
        baseSample.a *= hologramVisibility;
    }
#endif
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    half fullGlowVisibility = 1.0h;
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    if (_EnableFullGlowDissolve > 0.5)
    {
        float fullGlowNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
            _UberNoiseTexture,
            sampler_UberNoiseTexture,
            frac(uv) * _FullGlowDissolveNoiseScale.xy).r);
        half3 beforeGlow = baseSample.rgb;
        baseSample = ESCompositeApplyFullGlowDissolve(
            baseSample,
            fullGlowNoise,
            _FullGlowDissolveFade,
            _FullGlowDissolveWidth,
            _FullGlowDissolveEdgeColor.rgb,
            fullGlowVisibility);
        ssuEmission += max(baseSample.rgb - beforeGlow, 0.0h);
    }
#endif
    if (_EnableShadow > 0.5)
    {
        half shadowAlpha = ESLitShadowAlpha(uv) * fullGlowVisibility;
        baseSample = ESCompositeApplySpriteShadow(baseSample, _ShadowColor.rgb, shadowAlpha);
    }
#endif
    surfaceData.albedo = baseSample.rgb;
    half4 metallicSample = half4(1.0h, 1.0h, 1.0h, 1.0h);
    if (_UseMetallicMap > 0.5)
    {
        float2 metallicUV = uv * _MetallicMap_ST.xy + _MetallicMap_ST.zw;
        metallicSample = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, metallicUV);
    }
    surfaceData.metallic = saturate(_Metallic * metallicSample.r);
    surfaceData.specular = half3(0.5, 0.5, 0.5);
    half smoothnessSample = lerp(metallicSample.r, metallicSample.a, step(0.5, _SmoothnessMapChannel));
    surfaceData.smoothness = saturate(_Smoothness * smoothnessSample);
    surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
    if (_UseNormalMap > 0.5)
        surfaceData.normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalScale);
    half occlusion = _UseOcclusionMap > 0.5 ? SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g : 1.0h;
    surfaceData.occlusion = lerp(1.0h, occlusion, saturate(_Occlusion));
    surfaceData.emission = ssuEmission;
    if (_UseEmission > 0.5)
    {
        half4 emissionSample = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv);
        surfaceData.emission += emissionSample.rgb * _EmissionColor.rgb
            * lerp(1.0h, emissionSample.a, step(0.5, _EmissionUseAlpha));
    }
    surfaceData.alpha = saturate(baseSample.a * dissolveAlpha);
    surfaceData.clearCoatMask = 0.0;
    surfaceData.clearCoatSmoothness = 0.0;
#if defined(_ES_QUALITY_HIGH)
#if defined(ES_LIT_COMPILE_SURFACE_RESOURCES)
    if (_ESNativeStatusContract <= 0.5 && _EnableBurn > 0.5)
        surfaceData.emission += _BurnEdgeColor.rgb * (1.0 - smoothstep(_BurnProgress, _BurnProgress + _BurnWidth, ESNoise(positionWS)));
#endif
#endif
}

// Forward Lit Pass
ES3DLitVaryings ES3DLitVertex(ES3DLitAttributes input)
{
    ES3DLitVaryings output = (ES3DLitVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionOS = ESApplyVertexAnimation(input.positionOS.xyz, input.color, input.uv);
    VertexPositionInputs positionInput = GetVertexPositionInputs(positionOS);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = positionInput.positionCS;
    output.positionWS = positionInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.tangentWS = half4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = input.uv;
    output.vertexColor = input.color;
    half fog = ComputeFogFactor(positionInput.positionCS.z);
    #ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fog, VertexLighting(positionInput.positionWS, normalInput.normalWS));
    #else
    output.fogFactor = fog;
    #endif
    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(output.normalWS, output.vertexSH);
    #if defined(DYNAMICLIGHTMAP_ON)
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
    #endif
    output.shadowCoord = GetShadowCoord(positionInput);
    return output;
}

half4 ES3DLitFragment(ES3DLitVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData surfaceData;
    float dissolveAlpha;
    float dissolveEdge;
    float2 stylizedCoordinate;
    float hologramCoordinate;
    float2 surfaceUV = ESResolveLitSurfaceUV(
        input.uv,
        input.positionWS,
        input.positionCS,
        stylizedCoordinate,
        hologramCoordinate);
    ESInitializeSurface(
        surfaceUV,
        stylizedCoordinate,
        hologramCoordinate,
        input.positionWS,
        input.vertexColor,
        surfaceData,
        dissolveAlpha,
        dissolveEdge);
    if (_AlphaClip > 0.5) clip(surfaceData.alpha - _Cutoff);

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(surfaceData.normalTS,
        half3x3(input.tangentWS.xyz, cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w, input.normalWS)));
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_RECEIVE_SHADOWS_OFF)
    inputData.shadowCoord = 0;
#else
    inputData.shadowCoord = input.shadowCoord;
#endif
    #ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = input.fogFactorAndVertexLight.x;
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    #else
    inputData.fogCoord = input.fogFactor;
    #endif
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    #else
    inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
    #endif
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);

    half4 result = UniversalFragmentPBR(inputData, surfaceData);
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_DissolveMode > 0.5 || (_FadeMode > 3.5 && _FadeMode < 4.5) || _FadeMode > 6.5)
        result.rgb += _DissolveEdgeColor.rgb * dissolveEdge * _DissolveEdgeIntensity;
    if (_EnableRim > 0.5) result.rgb += _RimColor.rgb * pow(1.0 - saturate(dot(inputData.normalWS, inputData.viewDirectionWS)), _RimPower) * _RimIntensity;
#endif
#if defined(_ES_QUALITY_HIGH)
    if (_ESNativeStatusContract <= 0.5 && _EnableShine > 0.5)
    {
        float shineCoordinate = ESResolveLitShineCoordinate(
            surfaceUV,
            input.positionWS,
            90.0,
            0.0);
        float shine = 1.0 - smoothstep(0.0, _ShineWidth, abs(frac(shineCoordinate + ESCompositeTime() * _ShineSpeed) - 0.5));
        result.rgb += _ShineColor.rgb * shine * _ShineIntensity;
    }
    if (_EnableSparkle > 0.5)
    {
        float2 sparkleCell = floor(surfaceUV * max(1.0, _SparkleScale));
        float sparkleSeed = frac(sin(dot(sparkleCell, float2(12.9898, 78.233))) * 43758.5453);
        float sparkleWave = 0.5 + 0.5 * sin(ESCompositeTime() * _SparkleSpeed + sparkleSeed * 6.2831853);
        float2 sparkleLocal = frac(surfaceUV * max(1.0, _SparkleScale)) - 0.5;
        float sparkleRadial = saturate(1.0 - length(sparkleLocal) * 2.0);
        float sparkleCross = max(saturate(1.0 - abs(sparkleLocal.x) * 8.0), saturate(1.0 - abs(sparkleLocal.y) * 8.0));
        float sparkleShape = saturate(sparkleRadial * 0.35 + sparkleCross * 0.65);
        float sparkle = step(1.0 - _SparkleDensity, sparkleSeed)
            * pow(saturate(sparkleWave * sparkleShape), max(1.0, _SparkleSharpness));
        result.rgb += _SparkleColor.rgb * sparkle * _SparkleIntensity;
    }
#endif
    result.rgb = MixFog(result.rgb, inputData.fogCoord);
    result.a = surfaceData.alpha;
    return result;
}

// Shadow Pass
struct ES3DShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float4 vertexColor : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
ES3DShadowVaryings ES3DShadowVertex(ES3DLitAttributes input)
{
    ES3DShadowVaryings output = (ES3DShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionOS = ESApplyVertexAnimation(input.positionOS.xyz, input.color, input.uv);
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
    float3 lightDirectionWS = _LightDirection;
    #endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    output.uv = input.uv;
    output.positionWS = positionWS;
    output.vertexColor = input.color;
    return output;
}

half4 ES3DShadowFragment(ES3DShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float2 stylizedCoordinate;
    float hologramCoordinate;
    float2 surfaceUV = ESResolveLitShadowSurfaceUV(
        input.uv,
        input.positionWS,
        input.positionCS,
        stylizedCoordinate,
        hologramCoordinate);
    float edge;
    half alpha = ESComputeLitAlpha(
        surfaceUV,
        stylizedCoordinate,
        hologramCoordinate,
        input.positionWS,
        input.vertexColor,
        edge);
    if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
    return 0;
}

// Depth Passes
struct ES3DDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float4 tangentWS : TEXCOORD3;
    float4 vertexColor : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
ES3DDepthVaryings ES3DDepthVertex(ES3DLitAttributes input)
{
    ES3DDepthVaryings output = (ES3DDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionOS = ESApplyVertexAnimation(input.positionOS.xyz, input.color, input.uv);
    output.positionCS = TransformObjectToHClip(positionOS);
    output.uv = input.uv;
    output.positionWS = TransformObjectToWorld(positionOS);
    output.vertexColor = input.color;
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.normalWS = normalInput.normalWS;
    output.tangentWS = half4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    return output;
}
half4 ES3DDepthFragment(ES3DDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float2 stylizedCoordinate;
    float hologramCoordinate;
    float2 surfaceUV = ESResolveLitSurfaceUV(
        input.uv,
        input.positionWS,
        input.positionCS,
        stylizedCoordinate,
        hologramCoordinate);
    float edge;
    half alpha = ESComputeLitAlpha(
        surfaceUV,
        stylizedCoordinate,
        hologramCoordinate,
        input.positionWS,
        input.vertexColor,
        edge);
    if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
    return 0;
}

#endif
