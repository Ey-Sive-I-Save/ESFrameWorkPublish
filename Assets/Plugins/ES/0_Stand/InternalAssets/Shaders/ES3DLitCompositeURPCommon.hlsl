#ifndef ES_3D_LIT_COMPOSITE_URP_COMMON_INCLUDED
#define ES_3D_LIT_COMPOSITE_URP_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// Texture Resources
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
TEXTURE2D(_FlowMap); SAMPLER(sampler_FlowMap);
// Shared And Per-Material State
float3 _LightDirection;
float3 _LightPosition;
float _ESUnscaledTime;
float _ESUnscaledTimeValid;

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
half4 _BaseColor;
float4 _MainTexScaleOffset;
float _TimeMode;
float _CustomTime;
float _TimeScale;
float _EnableVertexAnimation;
float4 _VertexAnimationDirection;
float _VertexAnimationAmplitude;
float _VertexAnimationFrequency;
float _VertexAnimationSpeed;
float _VertexAnimationMask;
float _UseNormalMap;
float _NormalScale;
float _Metallic;
float _Smoothness;
float _Occlusion;
float _UseOcclusionMap;
float _UseEmission;
half4 _EmissionColor;
float _DissolveMode;
float _DissolveProgress;
float _DissolveSoftness;
half4 _DissolveEdgeColor;
float _DissolveEdgeWidth;
float _EnableRim;
half4 _RimColor;
float _RimPower;
float _RimIntensity;
float _EnableShine;
half4 _ShineColor;
float _ShineSpeed;
float _ShineWidth;
float _ShineIntensity;
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
half4 _BurnEdgeColor;
float _BurnProgress;
float _BurnWidth;
float _AlphaClip;
float _Cutoff;
float4 _NoiseScale;
float4 _NoiseSpeed;
CBUFFER_END

// Time, UV And Vertex Deformation
float ESCompositeTime()
{
    float baseTime = _TimeMode > 1.5 ? _CustomTime : (_TimeMode > 0.5 ? (_ESUnscaledTimeValid > 0.5 ? _ESUnscaledTime : _Time.y) : _Time.y);
    return baseTime * max(0, _TimeScale);
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
        float phase = dot(positionOS, directionOS) * _VertexAnimationFrequency + ESCompositeTime() * _VertexAnimationSpeed;
        positionOS += directionOS * sin(phase) * _VertexAnimationAmplitude * saturate(ESVertexAnimationMask(vertexColor));
    }
#endif
    return positionOS;
}

float2 ESBaseUV(float2 uv)
{
    uv = TRANSFORM_TEX(uv, _BaseMap);
    uv = uv * _MainTexScaleOffset.xy + _MainTexScaleOffset.zw;
    if (_EnableFlow > 0.5)
        uv += _FlowSpeed.xy * ESCompositeTime() * _FlowStrength;
    return uv;
}

float2 ESApplyFlowMap(float2 uv)
{
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
    if (_EnableFlowMap > 0.5 && abs(_FlowMapStrength) > 0.00001)
    {
        float2 flowUV = uv * _FlowMapScale.xy + _FlowMapScale.zw + _FlowMapSpeed.xy * ESCompositeTime();
        float2 direction = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, flowUV).rg * 2.0 - 1.0;
        uv += direction * _FlowMapStrength;
    }
#endif
    return uv;
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
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Surface Sampling And Dissolve
float ESNoise(float3 positionWS)
{
    float2 uv = positionWS.xz * _NoiseScale.xy + positionWS.y * _NoiseScale.zw + _NoiseSpeed.xy * ESCompositeTime();
    return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r;
}

half4 ESBlurBaseSample(float2 uv)
{
    float2 delta = _BaseMap_TexelSize.xy * (_BlurRadius * 512.0);
    half4 result = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * 0.4h;
    result += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(delta.x, 0)) * 0.15h;
    result += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - float2(delta.x, 0)) * 0.15h;
    result += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(0, delta.y)) * 0.15h;
    result += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - float2(0, delta.y)) * 0.15h;
    return result;
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

void ESInitializeSurface(float2 uv, float3 positionWS, out SurfaceData surfaceData, out float dissolveAlpha, out float dissolveEdge)
{
    surfaceData = (SurfaceData)0;
    half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
    if (_EnableBlur > 0.5)
        baseSample = lerp(baseSample, ESBlurBaseSample(uv) * _BaseColor, saturate(_BlurIntensity));
    if (_EnableChromatic > 0.5)
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
    dissolveAlpha = ESDissolveAlpha(positionWS, dissolveEdge);
    surfaceData.albedo = baseSample.rgb;
    surfaceData.metallic = saturate(_Metallic);
    surfaceData.specular = half3(0.5, 0.5, 0.5);
    surfaceData.smoothness = saturate(_Smoothness);
    surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
    if (_UseNormalMap > 0.5)
        surfaceData.normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalScale);
    half occlusion = _UseOcclusionMap > 0.5 ? SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g : 1.0h;
    surfaceData.occlusion = lerp(1.0h, occlusion, saturate(_Occlusion));
    surfaceData.emission = 0.0h;
    if (_UseEmission > 0.5)
        surfaceData.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
    surfaceData.alpha = saturate(baseSample.a * dissolveAlpha);
    surfaceData.clearCoatMask = 0.0;
    surfaceData.clearCoatSmoothness = 0.0;
#if defined(_ES_QUALITY_HIGH)
    if (_EnableBurn > 0.5)
        surfaceData.emission += _BurnEdgeColor.rgb * (1.0 - smoothstep(_BurnProgress, _BurnProgress + _BurnWidth, ESNoise(positionWS)));
#endif
}

// Forward Lit Pass
ES3DLitVaryings ES3DLitVertex(ES3DLitAttributes input)
{
    ES3DLitVaryings output = (ES3DLitVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionOS = ESApplyVertexAnimation(input.positionOS.xyz, input.color);
    VertexPositionInputs positionInput = GetVertexPositionInputs(positionOS);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = positionInput.positionCS;
    output.positionWS = positionInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.tangentWS = half4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = ESBaseUV(input.uv);
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
    float2 surfaceUV = ESApplyFlowMap(input.uv);
    ESInitializeSurface(surfaceUV, input.positionWS, surfaceData, dissolveAlpha, dissolveEdge);
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
    if (_DissolveMode > 0.5) result.rgb += _DissolveEdgeColor.rgb * dissolveEdge;
    if (_EnableRim > 0.5) result.rgb += _RimColor.rgb * pow(1.0 - saturate(dot(inputData.normalWS, inputData.viewDirectionWS)), _RimPower) * _RimIntensity;
#endif
#if defined(_ES_QUALITY_HIGH)
    if (_EnableShine > 0.5)
    {
        float shineDirectionLength = length(_ShineDirection.xyz);
        float3 shineDirection = shineDirectionLength > 0.0001 ? (_ShineDirection.xyz / shineDirectionLength) : float3(0, 1, 0);
        float shineCoordinate = dot(input.positionWS, shineDirection);
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
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
ES3DShadowVaryings ES3DShadowVertex(ES3DLitAttributes input)
{
    ES3DShadowVaryings output = (ES3DShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionOS = ESApplyVertexAnimation(input.positionOS.xyz, input.color);
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
    output.uv = ESBaseUV(input.uv);
    output.positionWS = positionWS;
    return output;
}

half4 ES3DShadowFragment(ES3DShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float2 surfaceUV = ESApplyFlowMap(input.uv);
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, surfaceUV).a * _BaseColor.a;
    float edge;
    alpha *= ESDissolveAlpha(input.positionWS, edge);
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
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
ES3DDepthVaryings ES3DDepthVertex(ES3DLitAttributes input)
{
    ES3DDepthVaryings output = (ES3DDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionOS = ESApplyVertexAnimation(input.positionOS.xyz, input.color);
    output.positionCS = TransformObjectToHClip(positionOS);
    output.uv = ESBaseUV(input.uv);
    output.positionWS = TransformObjectToWorld(positionOS);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.normalWS = normalInput.normalWS;
    output.tangentWS = half4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    return output;
}
half4 ES3DDepthFragment(ES3DDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float2 surfaceUV = ESApplyFlowMap(input.uv);
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, surfaceUV).a * _BaseColor.a;
    float edge;
    alpha *= ESDissolveAlpha(input.positionWS, edge);
    if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
    return 0;
}

#endif
