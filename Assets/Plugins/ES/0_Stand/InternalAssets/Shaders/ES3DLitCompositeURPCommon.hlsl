#ifndef ES_3D_LIT_COMPOSITE_URP_COMMON_INCLUDED
#define ES_3D_LIT_COMPOSITE_URP_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
float3 _LightDirection;
float3 _LightPosition;
float _ESUnscaledTime;
float _ESUnscaledTimeValid;

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
float4 _MainTexScaleOffset;
float _TimeMode;
float _CustomTime;
float _TimeScale;
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
float _EnableBurn;
half4 _BurnEdgeColor;
float _BurnProgress;
float _BurnWidth;
float _AlphaClip;
float _Cutoff;
float4 _NoiseScale;
float4 _NoiseSpeed;
CBUFFER_END

float ESCompositeTime()
{
    float baseTime = _TimeMode > 1.5 ? _CustomTime : (_TimeMode > 0.5 ? (_ESUnscaledTimeValid > 0.5 ? _ESUnscaledTime : _Time.y) : _Time.y);
    return baseTime * max(0, _TimeScale);
}

struct ES3DLitAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
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

float ESNoise(float3 positionWS)
{
    float2 uv = positionWS.xz * _NoiseScale.xy + positionWS.y * _NoiseScale.zw + _NoiseSpeed.xy * ESCompositeTime();
    return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r;
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

ES3DLitVaryings ES3DLitVertex(ES3DLitAttributes input)
{
    ES3DLitVaryings output = (ES3DLitVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs positionInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = positionInput.positionCS;
    output.positionWS = positionInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.tangentWS = half4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.uv = output.uv * _MainTexScaleOffset.xy + _MainTexScaleOffset.zw;
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
    ESInitializeSurface(input.uv, input.positionWS, surfaceData, dissolveAlpha, dissolveEdge);
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
        float shine = 1.0 - smoothstep(0.0, _ShineWidth, abs(frac(input.positionWS.y + ESCompositeTime() * _ShineSpeed) - 0.5));
        result.rgb += _ShineColor.rgb * shine * _ShineIntensity;
    }
#endif
    result.rgb = MixFog(result.rgb, inputData.fogCoord);
    result.a = surfaceData.alpha;
    return result;
}

struct ES3DShadowVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
ES3DShadowVaryings ES3DShadowVertex(ES3DLitAttributes input)
{
    ES3DShadowVaryings output = (ES3DShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
    float3 lightDirectionWS = _LightDirection;
    #endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.uv = output.uv * _MainTexScaleOffset.xy + _MainTexScaleOffset.zw;
    output.positionWS = positionWS;
    return output;
}

half4 ES3DShadowFragment(ES3DShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
    float edge;
    alpha *= ESDissolveAlpha(input.positionWS, edge);
    if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
    return 0;
}

struct ES3DDepthVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; float4 tangentWS : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
ES3DDepthVaryings ES3DDepthVertex(ES3DLitAttributes input)
{
    ES3DDepthVaryings output = (ES3DDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.uv = output.uv * _MainTexScaleOffset.xy + _MainTexScaleOffset.zw;
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.normalWS = normalInput.normalWS;
    output.tangentWS = half4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    return output;
}
half4 ES3DDepthFragment(ES3DDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
    float edge;
    alpha *= ESDissolveAlpha(input.positionWS, edge);
    if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
    return 0;
}

#endif
