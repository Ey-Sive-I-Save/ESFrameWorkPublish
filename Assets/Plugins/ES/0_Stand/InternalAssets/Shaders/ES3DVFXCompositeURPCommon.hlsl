#ifndef ES_3D_VFX_COMPOSITE_URP_COMMON_INCLUDED
#define ES_3D_VFX_COMPOSITE_URP_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "ESCompositeColorTransform.hlsl"
#include "ESCompositeGenerated.hlsl"

// Texture Resources
TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
TEXTURE2D(_FlowMap); SAMPLER(sampler_FlowMap);

// Per-Material State
CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float4 _MainTex_TexelSize;
    half4 _Color;
    float4 _MainTexScaleOffset;
    float _TimeMode;
    float _CustomTime;
    float _TimeScale;
    float _EnableTimeFPS;
    float _TimeFPS;
    float _EnableTimeFrequency;
    float _TimeFrequency;
    float _TimeRange;
    float _VertexColorStrength;

    float _EnableSequence;
    float _SequencePlayback;
    float _SequenceColumns;
    float _SequenceRows;
    float _SequenceFrame;
    float _SequenceSpeed;

    float _EnablePolarUV;
    float4 _PolarCenter;
    float _PolarRadialScale;
    float _PolarAngularScale;
    float _PolarRotationSpeed;

    float _EnableVertexStreams;
    float _VertexStreamUVStrength;
    float _VertexStreamFrameStrength;
    float _VertexStreamDissolveStrength;
    float _VertexStreamEmissionStrength;

    float _EnableVertexAnimation;
    float4 _VertexAnimationDirection;
    float _VertexAnimationAmplitude;
    float _VertexAnimationFrequency;
    float _VertexAnimationSpeed;
    float _VertexAnimationMask;

    float4 _NoiseScale;
    float4 _NoiseSpeed;
    float _Distortion;
    float4 _DistortionDirection;
    float _EnableFlow;
    float4 _FlowSpeed;
    float _FlowStrength;
    float _EnableFlowMap;
    float4 _FlowMapScale;
    float4 _FlowMapSpeed;
    float _FlowMapStrength;

    float _EnableShine;
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
    float _EnableChromatic;
    float _ChromaticOffset;
    float _ChromaticIntensity;
    float _ChromaticEdgeOnly;
    float _ChromaticAngle;
    float _EnableBlur;
    float _BlurRadius;
    float _BlurIntensity;

    float _EnableRadialMask;
    float4 _RadialMaskCenter;
    float _RadialMaskRadius;
    float _RadialMaskSoftness;
    float _RadialMaskInvert;

    float _DissolveMode;
    float _DissolveProgress;
    float _DissolveWidth;
    half4 _DissolveColor;
    float _EnableHologram;
    half4 _HologramColor;
    float _HologramFrequency;
    float _HologramGap;
    float _HologramSpeed;
    float _HologramMinAlpha;
    float _HologramFade;
    float _HologramContrast;
    float _HologramSpace;
    float4 _HologramDirection;
    float _HologramLineFrequency;
    float _HologramLineGap;
    float _HologramDistortionOffset;
    float4 _HologramDistortionDirection;
    float _HologramDistortionSpeed;
    float _HologramDistortionDensity;
    float _HologramDistortionScale;
    float _EnableRim;
    half4 _RimColor;
    float _RimPower;
    float _RimIntensity;

    float _EnableFresnelMask;
    float _FresnelPower;
    float _FresnelMin;
    float _FresnelMax;
    float _FresnelAlphaInfluence;
    half4 _FresnelColor;
    float _FresnelIntensity;

    float _EnableGlitch;
    float _GlitchAmount;
    float _GlitchSpeed;
    float4 _GlitchScanDirection;
    float _GlitchFade;
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
    float _SSUStatusContract;
    half4 _EmissionColor;
    float _EnableSoftParticles;
    float _SoftParticleNear;
    float _SoftParticleFar;
    float _EnableDepthIntersection;
    half4 _DepthIntersectionColor;
    float _DepthIntersectionDistance;
    float _DepthIntersectionIntensity;
    float _AlphaClip;
    float _Cutoff;
    float _BlendMode;
CBUFFER_END

// VFX reuses its authored noise resource for the shared SSU contract so the
// exact path adds no sampler and remains compatible with particle materials.
#define _UberNoiseTexture _NoiseTex
#define sampler_UberNoiseTexture sampler_NoiseTex
#include "ESCompositeSSUStylizedEffects.hlsl"

float _ESUnscaledTime;
float _ESUnscaledTimeValid;

// Vertex Contracts
struct ESAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float4 customData1 : TEXCOORD1;
    float4 customData2 : TEXCOORD2;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ESVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 customData1 : TEXCOORD1;
    float customData2 : TEXCOORD2;
    float3 positionWS : TEXCOORD3;
    half3 normalWS : TEXCOORD4;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Time, UV And Sampling Helpers
float ESGetTime()
{
    float baseTime = _TimeMode > 1.5
        ? _CustomTime
        : (_TimeMode > 0.5 ? (_ESUnscaledTimeValid > 0.5 ? _ESUnscaledTime : _Time.y) : _Time.y);
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

float ESRandom(float2 value)
{
    return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
}

float ESVertexAnimationMask(float4 vertexColor)
{
    if (_VertexAnimationMask < 0.5) return 1.0;
    if (_VertexAnimationMask < 1.5) return vertexColor.r;
    if (_VertexAnimationMask < 2.5) return vertexColor.g;
    if (_VertexAnimationMask < 3.5) return vertexColor.b;
    return vertexColor.a;
}

float3 ESApplyVertexAnimation(float3 positionOS, float4 vertexColor)
{
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_EnableVertexAnimation > 0.5 && abs(_VertexAnimationAmplitude) > 0.00001)
    {
        float directionLength = length(_VertexAnimationDirection.xyz);
        float3 directionOS = directionLength > 0.0001
            ? _VertexAnimationDirection.xyz / directionLength
            : float3(0.0, 1.0, 0.0);
        float phase = dot(positionOS, directionOS) * _VertexAnimationFrequency + ESGetTime() * _VertexAnimationSpeed;
        positionOS += directionOS * sin(phase) * _VertexAnimationAmplitude * saturate(ESVertexAnimationMask(vertexColor));
    }
#endif
    return positionOS;
}

float2 ESApplyPolarUV(float2 uv, float timeValue)
{
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_EnablePolarUV > 0.5)
    {
        float2 delta = uv - _PolarCenter.xy;
        float radial = length(delta) * _PolarRadialScale;
        float angular = atan2(delta.y, delta.x) * 0.15915494309 + 0.5;
        angular = angular * _PolarAngularScale + timeValue * _PolarRotationSpeed;
        return float2(angular, radial);
    }
#endif
    return uv;
}

float2 ESApplySequenceUV(float2 uv, float timeValue, float vertexFrame, out float4 atlasBounds)
{
    atlasBounds = float4(0.0, 0.0, 1.0, 1.0);
    if (_EnableSequence < 0.5) return uv;
    float columns = max(1.0, floor(_SequenceColumns + 0.5));
    float rows = max(1.0, floor(_SequenceRows + 0.5));
    float frame = _SequenceFrame;
    if (_SequencePlayback > 0.5 && _SequencePlayback < 1.5)
        frame += timeValue * _SequenceSpeed;
    else if (_SequencePlayback > 1.5)
        frame += vertexFrame;
    float totalFrames = columns * rows;
    frame = floor(frame - floor(frame / totalFrames) * totalFrames);
    float2 cellSize = rcp(float2(columns, rows));
    float2 cell = float2(frame - floor(frame / columns) * columns, rows - 1.0 - floor(frame / columns));
    float2 cellOrigin = cell * cellSize;
    atlasBounds = float4(cellOrigin, cellOrigin + cellSize);
    return frac(uv) * cellSize + cellOrigin;
}

float2 ESWrapSequenceUV(float2 uv, float4 atlasBounds)
{
    if (_EnableSequence < 0.5) return uv;
    float2 cellSize = max(atlasBounds.zw - atlasBounds.xy, _MainTex_TexelSize.xy);
    return atlasBounds.xy + frac((uv - atlasBounds.xy) / cellSize) * cellSize;
}

float2 ESSequenceLocalCoordinate(float2 uv, float4 atlasBounds)
{
    if (_EnableSequence < 0.5) return uv;
    float2 cellSize = max(atlasBounds.zw - atlasBounds.xy, _MainTex_TexelSize.xy);
    return (uv - atlasBounds.xy) / cellSize;
}

float2 ESClampSequenceSample(float2 uv, float4 atlasBounds)
{
    if (_EnableSequence < 0.5) return uv;
    float2 inset = min(_MainTex_TexelSize.xy * 0.5, (atlasBounds.zw - atlasBounds.xy) * 0.25);
    return clamp(uv, atlasBounds.xy + inset, atlasBounds.zw - inset);
}

float2 ESApplyFlowMap(float2 uv, float timeValue)
{
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_EnableFlowMap > 0.5 && abs(_FlowMapStrength) > 0.00001)
    {
        float2 flowUV = uv * _FlowMapScale.xy + _FlowMapScale.zw + _FlowMapSpeed.xy * timeValue;
        float2 direction = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, flowUV).rg * 2.0 - 1.0;
        uv += direction * _FlowMapStrength;
    }
#endif
    return uv;
}

float ESCalculateDepthGap(ESVaryings input)
{
    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
    float rawDepth = SampleSceneDepth(screenUV);
    float sceneEyeDepth = unity_OrthoParams.w == 0.0
        ? LinearEyeDepth(rawDepth, _ZBufferParams)
        : LinearDepthToEyeDepth(rawDepth);
    float fragmentEyeDepth = -TransformWorldToView(input.positionWS).z;
    return sceneEyeDepth - fragmentEyeDepth;
}

half4 ESBlurSample(float2 uv, float4 atlasBounds, half4 center)
{
    float2 delta = _MainTex_TexelSize.xy * (_BlurRadius * 512.0);
    half4 result = center * 0.4h;
    result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv + float2(delta.x, 0.0), atlasBounds)) * 0.15h;
    result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv - float2(delta.x, 0.0), atlasBounds)) * 0.15h;
    result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv + float2(0.0, delta.y), atlasBounds)) * 0.15h;
    result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv - float2(0.0, delta.y), atlasBounds)) * 0.15h;
    return result;
}

// Vertex Stage
ESVaryings ES3DVFXVertex(ESAttributes input)
{
    ESVaryings output = (ESVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionOS = ESApplyVertexAnimation(input.positionOS.xyz, input.color);
    VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = GetVertexNormalInputs(input.normalOS).normalWS;
    output.uv = TRANSFORM_TEX(input.uv, _MainTex);
    output.uv = output.uv * _MainTexScaleOffset.xy + _MainTexScaleOffset.zw;
    output.customData1 = input.customData1;
    output.customData2 = input.customData2.x;
    output.color = lerp(half4(1,1,1,1), input.color, _VertexColorStrength) * _Color;
    return output;
}

// Fragment Stage
half4 ES3DVFXFragment(ESVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float timeValue = ESGetTime();
    float streamEnabled = step(0.5, _EnableVertexStreams);
    float4 customData1 = input.customData1 * streamEnabled;
    float customData2 = input.customData2 * streamEnabled;
    float2 maskUV = input.uv;
    float2 uv = input.uv + customData1.xy * _VertexStreamUVStrength;
    uv = ESApplyPolarUV(uv, timeValue);
    float4 atlasBounds;
    uv = ESApplySequenceUV(uv, timeValue, customData1.z * _VertexStreamFrameStrength, atlasBounds);

    float noise = 0.5;
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (abs(_Distortion) > 0.00001 || _DissolveMode > 0.5)
    {
        float2 noiseUV = input.positionWS.xz * _NoiseScale.xy + _NoiseScale.zw + _NoiseSpeed.xy * timeValue;
        noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
        uv += (noise - 0.5) * _Distortion * _DistortionDirection.xy;
    }
#endif

    if (_EnableFlow > 0.5)
        uv += _FlowSpeed.xy * timeValue * _FlowStrength;
    uv = ESApplyFlowMap(uv, timeValue);

    float2 stylizedCoordinate = ESSequenceLocalCoordinate(uv, atlasBounds);
    float hologramCoordinate = 0.0;
#if defined(_ES_QUALITY_HIGH)
    if (_SSUStatusContract > 0.5)
    {
        if (_EnableHologram > 0.5)
        {
            hologramCoordinate = ESCompositeResolveSSUHologramCoordinate(
                stylizedCoordinate,
                input.positionWS);
            uv = ESCompositeApplySSUHologramUV(
                uv,
                hologramCoordinate,
                _MainTex_TexelSize.z,
                timeValue);
        }
        if (_EnableGlitch > 0.5)
            uv = ESCompositeApplySSUGlitchUV(uv, stylizedCoordinate, timeValue);
    }
    else if (_EnableGlitch > 0.5)
    {
        float glitchScanCoordinate = ESCompositeDirectionalCoordinate3D(
            input.positionWS,
            _GlitchScanDirection.xyz,
            float3(0.0, 1.0, 0.0));
        float glitch = ESRandom(float2(
            floor(glitchScanCoordinate * _GlitchSpeed + timeValue),
            0.0)) - 0.5;
        float2 glitchDirection = _GlitchDistortion.xy;
        float glitchDirectionLength = length(glitchDirection);
        glitchDirection = glitchDirectionLength > 0.0001
            ? glitchDirection / glitchDirectionLength
            : float2(1.0, 0.0);
        uv += glitchDirection * glitch * _GlitchAmount;
    }
#endif
    uv = ESWrapSequenceUV(uv, atlasBounds);

    half4 sourceTexture = SAMPLE_TEXTURE2D(
        _MainTex,
        sampler_MainTex,
        ESClampSequenceSample(uv, atlasBounds));
    half4 source = sourceTexture * input.color;
#if defined(_ES_QUALITY_HIGH)
    if (_EnableBlur > 0.5 && _BlurIntensity > 0.0001 && _BlurRadius > 0.0001)
        source = lerp(
            source,
            ESBlurSample(uv, atlasBounds, sourceTexture) * input.color,
            saturate(_BlurIntensity));
#endif

    float alpha = source.a;
    float3 color = source.rgb;

#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_EnableChromatic > 0.5
        && _ChromaticIntensity > 0.0001
        && abs(_ChromaticOffset) > 0.000001)
    {
        float2 chromaticDirection = float2(cos(radians(_ChromaticAngle)), sin(radians(_ChromaticAngle)));
        float edgeFactor = saturate(length(frac(stylizedCoordinate) - 0.5) * 2.0);
        float amount = _ChromaticOffset * lerp(1.0, edgeFactor, _ChromaticEdgeOnly);
        float3 chromaticColor = color;
        chromaticColor.r = SAMPLE_TEXTURE2D(
            _MainTex,
            sampler_MainTex,
            ESClampSequenceSample(uv + chromaticDirection * amount, atlasBounds)).r * input.color.r;
        chromaticColor.b = SAMPLE_TEXTURE2D(
            _MainTex,
            sampler_MainTex,
            ESClampSequenceSample(uv - chromaticDirection * amount, atlasBounds)).b * input.color.b;
        color = lerp(color, chromaticColor, saturate(_ChromaticIntensity));
    }
#endif

#if defined(_ES_QUALITY_HIGH)
    if (_SSUStatusContract > 0.5)
    {
        if (_EnableHologram > 0.5)
        {
            half4 hologramSource = ESCompositeApplySSUHologramColor(
                half4(color, alpha),
                hologramCoordinate,
                timeValue);
            color = hologramSource.rgb;
            alpha = hologramSource.a;
        }
        if (_EnableGlitch > 0.5)
            color = ESCompositeApplySSUGlitchColor(color, stylizedCoordinate, timeValue);
    }
#endif

    if (_EnableRadialMask > 0.5)
    {
        float radialDistance = length(maskUV - _RadialMaskCenter.xy);
        float radialMask = 1.0 - smoothstep(_RadialMaskRadius, _RadialMaskRadius + max(_RadialMaskSoftness, 0.0001), radialDistance);
        radialMask = lerp(radialMask, 1.0 - radialMask, step(0.5, _RadialMaskInvert));
        alpha *= radialMask;
    }

#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    float dissolveProgress = saturate(_DissolveProgress + customData1.w * _VertexStreamDissolveStrength);
    if (_DissolveMode > 0.5)
    {
        float dissolve = smoothstep(dissolveProgress - _DissolveWidth, dissolveProgress + _DissolveWidth, noise);
        alpha *= dissolve;
        if (_DissolveMode > 1.5)
        {
            float edge = 1.0 - smoothstep(dissolveProgress, dissolveProgress + _DissolveWidth, noise);
            color = lerp(color, _DissolveColor.rgb, edge);
        }
    }

    if (_EnableRim > 0.5 || _EnableFresnelMask > 0.5)
    {
        float3 normalWS = SafeNormalize(input.normalWS);
        float3 viewDirectionWS = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
        float fresnelBase = 1.0 - saturate(dot(normalWS, viewDirectionWS));
        if (_EnableRim > 0.5)
            color += _RimColor.rgb * pow(fresnelBase, max(0.1, _RimPower)) * _RimIntensity;
        if (_EnableFresnelMask > 0.5)
        {
            float fresnel = pow(fresnelBase, max(0.1, _FresnelPower));
            fresnel = smoothstep(min(_FresnelMin, _FresnelMax - 0.0001), max(_FresnelMax, _FresnelMin + 0.0001), fresnel);
            alpha *= lerp(1.0, fresnel, saturate(_FresnelAlphaInfluence));
            color += _FresnelColor.rgb * fresnel * _FresnelIntensity;
        }
    }

    if (_EnableShine > 0.5)
    {
        float shineCoordinate = _ShineSpace > 0.5 && _ShineSpace < 1.5
            ? ESCompositeShineCoordinate2D(
                stylizedCoordinate,
                _ShineDirection.xy,
                90.0)
            : ESCompositeShineCoordinate3D(
                input.positionWS,
                _ShineDirection.xyz,
                float3(0.0, 1.0, 0.0));
        float shine = 1.0 - smoothstep(0.0, _ShineWidth, abs(frac(shineCoordinate + timeValue * _ShineSpeed) - 0.5));
        color += _ShineColor.rgb * shine * _ShineIntensity;
    }

    if (_EnableSoftParticles > 0.5 || _EnableDepthIntersection > 0.5)
    {
        float depthGap = ESCalculateDepthGap(input);
        if (_EnableSoftParticles > 0.5)
        {
            float nearDistance = min(_SoftParticleNear, _SoftParticleFar - 0.0001);
            float fadeDistance = max(_SoftParticleFar - nearDistance, 0.0001);
            alpha *= saturate((depthGap - nearDistance) / fadeDistance);
        }
        if (_EnableDepthIntersection > 0.5)
        {
            float intersection = 1.0 - smoothstep(0.0, max(_DepthIntersectionDistance, 0.0001), max(depthGap, 0.0));
            color += _DepthIntersectionColor.rgb * intersection * _DepthIntersectionIntensity;
        }
    }
#endif

#if defined(_ES_QUALITY_HIGH)
    if (_EnableSparkle > 0.5)
    {
        float sparkleScale = max(1.0, _SparkleScale);
        float2 sparkleCell = floor(stylizedCoordinate * sparkleScale);
        float sparkleSeed = ESRandom(sparkleCell);
        float sparkleWave = 0.5 + 0.5 * sin(timeValue * _SparkleSpeed + sparkleSeed * 6.2831853);
        float2 sparkleLocal = frac(stylizedCoordinate * sparkleScale) - 0.5;
        float sparkleRadial = saturate(1.0 - length(sparkleLocal) * 2.0);
        float sparkleCross = max(saturate(1.0 - abs(sparkleLocal.x) * 8.0), saturate(1.0 - abs(sparkleLocal.y) * 8.0));
        float sparkleShape = saturate(sparkleRadial * 0.35 + sparkleCross * 0.65);
        float sparkle = step(1.0 - _SparkleDensity, sparkleSeed) * pow(saturate(sparkleWave * sparkleShape), max(1.0, _SparkleSharpness));
        color += _SparkleColor.rgb * sparkle * _SparkleIntensity;
    }
    if (_SSUStatusContract <= 0.5 && _EnableHologram > 0.5)
    {
        float2 legacyLocalDirection = _HologramDirection.xy;
        float legacyLocalDirectionLength = length(legacyLocalDirection);
        legacyLocalDirection = legacyLocalDirectionLength > 0.0001
            ? legacyLocalDirection / legacyLocalDirectionLength
            : float2(0.0, 1.0);
        float3 legacyWorldDirection = _HologramDirection.xyz;
        float legacyWorldDirectionLength = length(legacyWorldDirection);
        legacyWorldDirection = legacyWorldDirectionLength > 0.0001
            ? legacyWorldDirection / legacyWorldDirectionLength
            : float3(0.0, 1.0, 0.0);
        float legacyHologramCoordinate = _HologramSpace < 0.5
            ? dot(stylizedCoordinate, legacyLocalDirection)
            : dot(input.positionWS, legacyWorldDirection);
        float hologramLine = step(
            _HologramGap,
            frac(legacyHologramCoordinate * _HologramFrequency + timeValue * _HologramSpeed));
        color = lerp(color, _HologramColor.rgb, saturate((half)_HologramFade) * 0.6h);
        alpha *= lerp(1.0, max(_HologramMinAlpha, hologramLine), saturate(_HologramFade));
    }
#endif

    float emissionMultiplier = 1.0 + customData2 * _VertexStreamEmissionStrength;
    color += _EmissionColor.rgb * max(0.0, emissionMultiplier);
    if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
    if (_BlendMode > 1.5 && _BlendMode < 2.5) color *= alpha;
    return half4(color, alpha);
}

#endif
