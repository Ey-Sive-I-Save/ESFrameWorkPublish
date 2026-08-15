Shader "ES/UI/Composite URP"
{
    Properties
    {
        [PerRendererData] _MainTex ("主纹理", 2D) = "white" {}
        _Color ("颜色", Color) = (1,1,1,1)
        _MainTexScaleOffset ("主纹理缩放/偏移", Vector) = (1,1,0,0)
        [Enum(场景时间,0,非缩放时间,1,自定义时间,2)] _TimeMode ("时间来源", Float) = 0
        _CustomTime ("自定义时间", Float) = 0
        _TimeScale ("时间倍率", Range(0,4)) = 1
        _VertexColorStrength ("顶点色强度", Range(0,1)) = 1
        [Toggle] _EnableHologram ("启用全息", Float) = 0
        [HDR] _HologramColor ("全息颜色", Color) = (0.1,0.8,1,1)
        _HologramFrequency ("全息线频率", Float) = 60
        _HologramSpeed ("全息速度", Float) = 1
        [Toggle] _EnableGlitch ("启用故障", Float) = 0
        _GlitchAmount ("故障强度", Range(0,0.2)) = 0.02
        _GlitchSpeed ("故障速度", Float) = 3
        _AlphaClip ("透明裁剪", Float) = 0
        _Cutoff ("裁剪阈值", Range(0,1)) = 0.01
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _TextureSampleAdd ("Texture Sample Add", Color) = (0,0,0,0)
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("启用 UI 透明裁剪", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "UIForward"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ESUIVertex
            #pragma fragment ESUIFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UnityUI.cginc"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; half4 _Color; float4 _MainTexScaleOffset; float _TimeMode; float _CustomTime; float _TimeScale; float _VertexColorStrength; float _EnableHologram; half4 _HologramColor; float _HologramFrequency; float _HologramSpeed; float _EnableGlitch; float _GlitchAmount; float _GlitchSpeed; float _AlphaClip; float _Cutoff; half4 _TextureSampleAdd; float4 _ClipRect;
            CBUFFER_END
            float _ESUnscaledTime; float _ESUnscaledTimeValid;
            float ESGetTime() { float baseTime = _TimeMode > 1.5 ? _CustomTime : (_TimeMode > 0.5 ? (_ESUnscaledTimeValid > 0.5 ? _ESUnscaledTime : _Time.y) : _Time.y); return baseTime * max(0,_TimeScale); }
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float4 worldPosition:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V ESUIVertex(A input) { V output=(V)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output); output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.worldPosition=input.positionOS; output.uv=input.uv*_MainTexScaleOffset.xy+_MainTexScaleOffset.zw; output.color=lerp(half4(1,1,1,1),input.color,_VertexColorStrength)*_Color; return output; }
            half4 ESUIFragment(V input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.uv;
                if(_EnableGlitch>0.5) uv.x+=(frac(sin(dot(floor(uv*100+ESGetTime()*_GlitchSpeed),float2(12.9898,78.233)))*43758.5453)-0.5)*_GlitchAmount;
                half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv)+_TextureSampleAdd;
                color*=input.color;
                if(_EnableHologram>0.5) color.rgb=lerp(color.rgb,_HologramColor.rgb,0.5+0.5*sin(uv.y*_HologramFrequency+ESGetTime()*_HologramSpeed));
                #ifdef UNITY_UI_CLIP_RECT
                color*=UnityGet2DClipping(input.worldPosition.xy,_ClipRect);
                #endif
                #if defined(UNITY_UI_ALPHACLIP)
                clip(color.a-0.001);
                #endif
                if(_AlphaClip>0.5) clip(color.a-_Cutoff);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "ES.EditorInternal.ESCompositeShaderGUI"
}
