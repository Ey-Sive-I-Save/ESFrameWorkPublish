Shader "ES/UI/Composite URP"
{
    Properties
    {
        // Base Input
        [PerRendererData] _MainTex ("主纹理", 2D) = "white" {}
        _Color ("颜色", Color) = (1,1,1,1)
        _VertexColorStrength ("顶点色强度", Range(0,1)) = 1

        // Time And Coordinates
        _MainTexScaleOffset ("主纹理缩放/偏移", Vector) = (1,1,0,0)
        [Enum(SceneTime,0,UnscaledTime,1,CustomTime,2)] _TimeMode ("时间来源", Float) = 0
        _CustomTime ("自定义时间", Float) = 0
        _TimeScale ("时间倍率", Range(0,4)) = 1
        // Dynamic Effects
        [Toggle] _EnableFlow ("启用纹理流动", Float) = 0
        _FlowSpeed ("流动速度", Vector) = (0,0,0,0)
        _FlowStrength ("流动强度", Range(0,1)) = 1
        [Toggle] _EnableShine ("启用扫光", Float) = 0
        [HDR] _ShineColor ("扫光颜色", Color) = (1,1,1,1)
        _ShineSpeed ("扫光速度", Float) = 1
        _ShineWidth ("扫光宽度", Range(0.001,1)) = 0.15
        _ShineAngle ("扫光角度", Range(0,360)) = 30
        _ShineIntensity ("扫光强度", Range(0,8)) = 1
        [Toggle] _EnableSparkle ("启用亮晶晶", Float) = 0
        [HDR] _SparkleColor ("亮晶晶颜色", Color) = (1,1,1,1)
        _SparkleScale ("亮晶晶密度", Range(1,128)) = 24
        _SparkleSpeed ("亮晶晶速度", Float) = 2
        _SparkleDensity ("亮晶晶数量", Range(0,1)) = 0.16
        _SparkleSharpness ("亮晶晶锐度", Range(1,16)) = 6
        _SparkleIntensity ("亮晶晶强度", Range(0,8)) = 1
        [Toggle] _EnableChromatic ("启用色差", Float) = 0
        _ChromaticOffset ("色差偏移", Range(0,0.02)) = 0.002
        _ChromaticIntensity ("色差强度", Range(0,1)) = 1
        _ChromaticEdgeOnly ("边缘色差", Range(0,1)) = 0.5
        _ChromaticAngle ("色差方向", Range(0,360)) = 0
        [Toggle] _EnableBlur ("启用纹理模糊", Float) = 0
        _BlurRadius ("模糊半径", Range(0,0.02)) = 0.002
        _BlurIntensity ("模糊强度", Range(0,1)) = 0.45
        [Toggle] _EnableHologram ("启用全息", Float) = 0
        [HDR] _HologramColor ("全息颜色", Color) = (0.1,0.8,1,1)
        _HologramFrequency ("全息线频率", Float) = 60
        _HologramSpeed ("全息速度", Float) = 1
        [Toggle] _EnableGlitch ("启用故障", Float) = 0
        _GlitchAmount ("故障强度", Range(0,0.2)) = 0.02
        _GlitchSpeed ("故障速度", Float) = 3
        // Masks And Output
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

            // Texture Resources
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            // Per-Material State
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _Color;
                float4 _MainTexScaleOffset;
                float _TimeMode;
                float _CustomTime;
                float _TimeScale;
                float _VertexColorStrength;
                float _EnableFlow;
                float4 _FlowSpeed;
                float _FlowStrength;
                float _EnableShine;
                half4 _ShineColor;
                float _ShineSpeed;
                float _ShineWidth;
                float _ShineAngle;
                float _ShineIntensity;
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
                float _EnableHologram;
                half4 _HologramColor;
                float _HologramFrequency;
                float _HologramSpeed;
                float _EnableGlitch;
                float _GlitchAmount;
                float _GlitchSpeed;
                float _AlphaClip;
                float _Cutoff;
                half4 _TextureSampleAdd;
                float4 _ClipRect;
            CBUFFER_END

            float _ESUnscaledTime;
            float _ESUnscaledTimeValid;

            // Time And Sampling Helpers
            float ESGetTime()
            {
                float baseTime = _TimeMode > 1.5
                    ? _CustomTime
                    : (_TimeMode > 0.5 ? (_ESUnscaledTimeValid > 0.5 ? _ESUnscaledTime : _Time.y) : _Time.y);
                return baseTime * max(0, _TimeScale);
            }

            float ESGet2DClipping(float2 position, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, position) * step(position, clipRect.zw);
                return inside.x * inside.y;
            }

            half4 ESBlurSample(float2 uv)
            {
                float2 delta = _MainTex_TexelSize.xy * (_BlurRadius * 512.0);
                half4 result = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * 0.4h;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(delta.x, 0)) * 0.15h;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(delta.x, 0)) * 0.15h;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, delta.y)) * 0.15h;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(0, delta.y)) * 0.15h;
                return result;
            }

            // Vertex Contracts
            struct ESUIAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ESUIVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Vertex Stage
            ESUIVaryings ESUIVertex(ESUIAttributes input)
            {
                ESUIVaryings output = (ESUIVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPosition = input.positionOS;
                output.uv = input.uv * _MainTexScaleOffset.xy + _MainTexScaleOffset.zw;
                output.color = lerp(half4(1, 1, 1, 1), input.color, _VertexColorStrength) * _Color;
                return output;
            }

            // Fragment Stage
            half4 ESUIFragment(ESUIVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                if (_EnableFlow > 0.5) uv += _FlowSpeed.xy * ESGetTime() * _FlowStrength;
                if (_EnableGlitch > 0.5)
                {
                    float2 glitchCell = floor(uv * 100 + ESGetTime() * _GlitchSpeed);
                    float glitch = frac(sin(dot(glitchCell, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
                    uv.x += glitch * _GlitchAmount;
                }

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) + _TextureSampleAdd;
                if (_EnableBlur > 0.5) color = lerp(color, ESBlurSample(uv) + _TextureSampleAdd, saturate(_BlurIntensity));
                color *= input.color;
                if (_EnableChromatic > 0.5)
                {
                    float2 chromaDir = float2(cos(radians(_ChromaticAngle)), sin(radians(_ChromaticAngle)));
                    float2 localCoord = frac(uv);
                    float edgeFactor = saturate(length(localCoord - 0.5) * 2.0);
                    float amount = _ChromaticOffset * lerp(1.0, edgeFactor, _ChromaticEdgeOnly);
                    half3 chroma = color.rgb;
                    chroma.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + chromaDir * amount).r * input.color.r;
                    chroma.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - chromaDir * amount).b * input.color.b;
                    color.rgb = lerp(color.rgb, chroma, saturate(_ChromaticIntensity));
                }
                if (_EnableShine > 0.5)
                {
                    float2 shineDir = float2(cos(radians(_ShineAngle)), sin(radians(_ShineAngle)));
                    float shine = 1.0 - smoothstep(0.0, _ShineWidth, abs(frac(dot(uv, shineDir) + ESGetTime() * _ShineSpeed) - 0.5));
                    color.rgb += _ShineColor.rgb * shine * _ShineIntensity;
                }
                if (_EnableSparkle > 0.5)
                {
                    float2 sparkleCell = floor(uv * max(1.0, _SparkleScale));
                    float sparkleSeed = frac(sin(dot(sparkleCell, float2(12.9898, 78.233))) * 43758.5453);
                    float sparkleWave = 0.5 + 0.5 * sin(ESGetTime() * _SparkleSpeed + sparkleSeed * 6.2831853);
                    float2 sparkleLocal = frac(uv * max(1.0, _SparkleScale)) - 0.5;
                    float sparkleRadial = saturate(1.0 - length(sparkleLocal) * 2.0);
                    float sparkleCross = max(saturate(1.0 - abs(sparkleLocal.x) * 8.0), saturate(1.0 - abs(sparkleLocal.y) * 8.0));
                    float sparkleShape = saturate(sparkleRadial * 0.35 + sparkleCross * 0.65);
                    float sparkle = step(1.0 - _SparkleDensity, sparkleSeed)
                        * pow(saturate(sparkleWave * sparkleShape), max(1.0, _SparkleSharpness));
                    color.rgb += _SparkleColor.rgb * sparkle * _SparkleIntensity;
                }
                if (_EnableHologram > 0.5)
                {
                    float hologram = 0.5 + 0.5 * sin(uv.y * _HologramFrequency + ESGetTime() * _HologramSpeed);
                    color.rgb = lerp(color.rgb, _HologramColor.rgb, hologram);
                }
            #ifdef UNITY_UI_CLIP_RECT
                color *= ESGet2DClipping(input.worldPosition.xy, _ClipRect);
            #endif
            #if defined(UNITY_UI_ALPHACLIP)
                clip(color.a - 0.001);
            #endif
                if (_AlphaClip > 0.5) clip(color.a - _Cutoff);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "ES.EditorInternal.ESCompositeShaderGUI"
}
