Shader "ES/3D/VFX Composite URP"
{
    Properties
    {
        [MainTexture] _MainTex ("主纹理", 2D) = "white" {}
        [MainColor] _Color ("基础颜色", Color) = (1,1,1,1)
        _MainTexScaleOffset ("主纹理缩放/偏移", Vector) = (1,1,0,0)
        [Enum(场景时间,0,非缩放时间,1,自定义时间,2)] _TimeMode ("时间来源", Float) = 0
        _CustomTime ("自定义时间", Float) = 0
        _TimeScale ("时间倍率", Range(0,4)) = 1
        _VertexColorStrength ("顶点色强度", Range(0,1)) = 1
        [NoScaleOffset] _NoiseTex ("噪声纹理", 2D) = "gray" {}
        _NoiseScale ("噪声缩放", Vector) = (1,1,0,0)
        _NoiseSpeed ("噪声速度", Vector) = (0,0,0,0)
        _Distortion ("扰动强度", Range(0,0.2)) = 0
        [Enum(关闭,0,溶解,1,溶解带边缘,2)] _DissolveMode ("溶解模式", Float) = 0
        _DissolveProgress ("溶解进度", Range(0,1)) = 0
        _DissolveWidth ("溶解边缘宽度", Range(0.001,1)) = 0.1
        [HDR] _DissolveColor ("溶解边缘颜色", Color) = (1,0.1,0.01,1)
        [Toggle] _EnableHologram ("启用全息", Float) = 0
        [HDR] _HologramColor ("全息颜色", Color) = (0.1,0.8,1,1)
        _HologramFrequency ("全息线频率", Float) = 60
        _HologramGap ("全息线间隔", Range(0,1)) = 0.35
        _HologramSpeed ("全息速度", Float) = 1
        _HologramMinAlpha ("全息最低透明度", Range(0,1)) = 0.2
        [Toggle] _EnableRim ("启用边缘光", Float) = 0
        [HDR] _RimColor ("边缘光颜色", Color) = (0.1,0.6,1,1)
        _RimPower ("边缘光幂次", Range(0.1,8)) = 3
        _RimIntensity ("边缘光强度", Range(0,8)) = 1
        [Toggle] _EnableGlitch ("启用故障", Float) = 0
        _GlitchAmount ("故障偏移", Range(0,0.2)) = 0.02
        _GlitchSpeed ("故障速度", Float) = 3
        [HDR] _EmissionColor ("自发光颜色", Color) = (0,0,0,1)
        [Toggle] _AlphaClip ("启用透明裁剪", Float) = 0
        _Cutoff ("透明裁剪阈值", Range(0,1)) = 0.01
        [Enum(基础,0,标准,1,高质量,2)] _QualityTier ("效果质量档位", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ES3DVFXVertex
            #pragma fragment ES3DVFXFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; half4 _Color; float4 _MainTexScaleOffset; float _TimeMode; float _CustomTime; float _TimeScale; float _VertexColorStrength; float4 _NoiseScale; float4 _NoiseSpeed; float _Distortion; float _DissolveMode; float _DissolveProgress; float _DissolveWidth; half4 _DissolveColor; float _EnableHologram; half4 _HologramColor; float _HologramFrequency; float _HologramGap; float _HologramSpeed; float _HologramMinAlpha; float _EnableRim; half4 _RimColor; float _RimPower; float _RimIntensity; float _EnableGlitch; float _GlitchAmount; float _GlitchSpeed; half4 _EmissionColor; float _AlphaClip; float _Cutoff;
            CBUFFER_END
            float _ESUnscaledTime; float _ESUnscaledTimeValid;
            float ESGetTime() { float baseTime = _TimeMode > 1.5 ? _CustomTime : (_TimeMode > 0.5 ? (_ESUnscaledTimeValid > 0.5 ? _ESUnscaledTime : _Time.y) : _Time.y); return baseTime * max(0,_TimeScale); }
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; half3 normalWS:TEXCOORD2; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float random2(float2 p) { return frac(sin(dot(p,float2(12.9898,78.233))) * 43758.5453); }
            V ES3DVFXVertex(A input) { V output=(V)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz); output.positionCS=p.positionCS; output.positionWS=p.positionWS; output.normalWS=GetVertexNormalInputs(input.normalOS).normalWS; output.uv=TRANSFORM_TEX(input.uv,_MainTex); output.uv=output.uv*_MainTexScaleOffset.xy+_MainTexScaleOffset.zw; output.color=lerp(half4(1,1,1,1),input.color,_VertexColorStrength)*_Color; return output; }
            half4 ES3DVFXFragment(V input):SV_Target { UNITY_SETUP_INSTANCE_ID(input); float t=ESGetTime(); float2 uv=input.uv; float n=0.5;
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
                if(abs(_Distortion)>0.00001||_DissolveMode>0.5)
                {
                    n=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,input.positionWS.xz*_NoiseScale.xy+_NoiseSpeed.xy*t).r;
                    uv+=(n-0.5)*_Distortion;
                }
#endif
#if defined(_ES_QUALITY_HIGH)
                if(_EnableGlitch>0.5) uv.x+=(random2(float2(floor(input.positionWS.y*_GlitchSpeed+t),0))-0.5)*_GlitchAmount;
#endif
                half4 src=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv)*input.color; float alpha=src.a; float3 c=src.rgb;
#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
                if(_DissolveMode>0.5){float d=smoothstep(_DissolveProgress-_DissolveWidth,_DissolveProgress+_DissolveWidth,n); alpha*=d; if(_DissolveMode>1.5)c=lerp(c,_DissolveColor.rgb,1-smoothstep(_DissolveProgress,_DissolveProgress+_DissolveWidth,n));}
                if(_EnableRim>0.5)c+=_RimColor.rgb*pow(1-saturate(dot(normalize(input.normalWS),normalize(_WorldSpaceCameraPos-input.positionWS))),_RimPower)*_RimIntensity;
#endif
#if defined(_ES_QUALITY_HIGH)
                if(_EnableHologram>0.5){float line=step(_HologramGap,frac(input.positionWS.y*_HologramFrequency+t*_HologramSpeed)); c=lerp(c,_HologramColor.rgb,0.6); alpha*=max(_HologramMinAlpha,line);}
#endif
                c+=_EmissionColor.rgb; if(_AlphaClip>0.5)clip(alpha-_Cutoff); return half4(c,alpha); }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "ES.EditorInternal.ESCompositeShaderGUI"
}
