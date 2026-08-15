Shader "ES/3D/Lit Composite URP"
{
    Properties
    {
        [MainTexture] _BaseMap ("基础颜色纹理", 2D) = "white" {}
        [MainColor] _BaseColor ("基础颜色", Color) = (1,1,1,1)
        _MainTexScaleOffset ("主纹理缩放/偏移", Vector) = (1,1,0,0)
        [Enum(场景时间,0,非缩放时间,1,自定义时间,2)] _TimeMode ("时间来源", Float) = 0
        _CustomTime ("自定义时间", Float) = 0
        _TimeScale ("时间倍率", Range(0,4)) = 1
        [Toggle] _UseNormalMap ("启用法线纹理", Float) = 0
        [Normal] _NormalMap ("法线纹理", 2D) = "bump" {}
        _NormalScale ("法线强度", Range(0,2)) = 1
        _Metallic ("金属度", Range(0,1)) = 0
        _Smoothness ("光滑度", Range(0,1)) = 0.5
        [Toggle] _UseOcclusionMap ("使用环境遮挡纹理", Float) = 0
        _OcclusionMap ("环境遮挡纹理", 2D) = "white" {}
        _Occlusion ("环境遮挡强度", Range(0,1)) = 1
        [Toggle] _UseEmission ("启用自发光", Float) = 0
        [HDR] _EmissionColor ("自发光颜色", Color) = (0,0,0,1)
        _EmissionMap ("自发光纹理", 2D) = "black" {}
        [Enum(关闭,0,噪声溶解,1,距离溶解,2)] _DissolveMode ("溶解模式", Float) = 0
        [NoScaleOffset] _NoiseTex ("噪声纹理", 2D) = "gray" {}
        _NoiseScale ("噪声缩放", Vector) = (1,1,1,1)
        _NoiseSpeed ("噪声速度", Vector) = (0,0,0,0)
        _DissolveProgress ("溶解进度", Range(0,1)) = 0
        _DissolveSoftness ("溶解柔和度", Range(0.001,1)) = 0.08
        [HDR] _DissolveEdgeColor ("溶解边缘颜色", Color) = (1,0.1,0.01,1)
        _DissolveEdgeWidth ("溶解边缘宽度", Range(0.001,1)) = 0.08
        [Toggle] _EnableRim ("启用边缘光", Float) = 0
        [HDR] _RimColor ("边缘光颜色", Color) = (0.1,0.6,1,1)
        _RimPower ("边缘光幂次", Range(0.1,8)) = 3
        _RimIntensity ("边缘光强度", Range(0,8)) = 1
        [Toggle] _EnableShine ("启用扫光", Float) = 0
        [HDR] _ShineColor ("扫光颜色", Color) = (1,1,1,1)
        _ShineSpeed ("扫光速度", Float) = 1
        _ShineWidth ("扫光宽度", Range(0.001,1)) = 0.15
        _ShineIntensity ("扫光强度", Range(0,8)) = 1
        [Toggle] _EnableBurn ("启用燃烧边缘", Float) = 0
        [HDR] _BurnEdgeColor ("燃烧边缘颜色", Color) = (1,0.05,0,1)
        _BurnProgress ("燃烧进度", Range(0,1)) = 0
        _BurnWidth ("燃烧边缘宽度", Range(0.001,1)) = 0.1
        [Toggle] _AlphaClip ("启用透明裁剪", Float) = 0
        _Cutoff ("透明裁剪阈值", Range(0,1)) = 0.5
        [Toggle] _ReceiveShadows ("接收阴影", Float) = 1
        [Enum(基础,0,标准,1,高质量,2)] _QualityTier ("效果质量档位", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }
        LOD 300
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ES3DLitVertex
            #pragma fragment ES3DLitFragment
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "ES3DLitCompositeURPCommon.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ES3DShadowVertex
            #pragma fragment ES3DShadowFragment
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "ES3DLitCompositeURPCommon.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ES3DDepthVertex
            #pragma fragment ES3DDepthFragment
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma multi_compile_instancing
            #include "ES3DLitCompositeURPCommon.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ES3DDepthVertex
            #pragma fragment ES3DDepthNormalsFragment
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "ES3DLitCompositeURPCommon.hlsl"
            half4 ES3DDepthNormalsFragment(ES3DDepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                float edge;
                alpha *= ESDissolveAlpha(input.positionWS, edge);
                if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
                float3 normalWS = input.normalWS;
                if (_UseNormalMap > 0.5)
                {
                    float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                    float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalScale);
                    normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS));
                }
                normalWS = NormalizeNormalPerPixel(normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remappedOctNormalWS), 0);
#else
                return half4(normalWS, 0);
#endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ES3DMetaVertex
            #pragma fragment ES3DMetaFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"
            #include "ES3DLitCompositeURPCommon.hlsl"
            struct ES3DMetaAttributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 lightmapUV:TEXCOORD1; float2 dynamicLightmapUV:TEXCOORD2; };
            struct ES3DMetaVaryings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            ES3DMetaVaryings ES3DMetaVertex(ES3DMetaAttributes input)
            { ES3DMetaVaryings output; output.positionCS=UnityMetaVertexPosition(input.positionOS.xyz, input.lightmapUV, input.dynamicLightmapUV, unity_LightmapST, unity_DynamicLightmapST); output.uv=TRANSFORM_TEX(input.uv,_BaseMap); output.uv=output.uv*_MainTexScaleOffset.xy+_MainTexScaleOffset.zw; return output; }
            half4 ES3DMetaFragment(ES3DMetaVaryings input):SV_Target
            { half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv)*_BaseColor; MetaInput meta=(MetaInput)0; meta.Albedo=albedo.rgb; meta.Emission=_UseEmission>0.5?SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,input.uv).rgb*_EmissionColor.rgb:0; return UnityMetaFragment(meta); }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "ES.EditorInternal.ESCompositeShaderGUI"
}
