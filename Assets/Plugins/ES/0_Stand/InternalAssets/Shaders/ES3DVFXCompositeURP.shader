Shader "ES/3D/VFX Composite URP"
{
    Properties
    {
        [MainTexture] _MainTex ("主纹理", 2D) = "white" {}
        [MainColor] _Color ("基础颜色", Color) = (1,1,1,1)
        _MainTexScaleOffset ("主纹理缩放/偏移", Vector) = (1,1,0,0)
        [Enum(SceneTime,0,UnscaledTime,1,CustomTime,2)] _TimeMode ("时间来源", Float) = 0
        _CustomTime ("自定义时间", Float) = 0
        _TimeScale ("时间倍率", Range(0,4)) = 1
        _VertexColorStrength ("顶点色强度", Range(0,1)) = 1

        [Toggle] _EnableSequence ("启用序列帧", Float) = 0
        [Enum(Manual,0,Time,1,VertexStream,2)] _SequencePlayback ("序列帧播放方式", Float) = 1
        _SequenceColumns ("序列帧列数", Float) = 1
        _SequenceRows ("序列帧行数", Float) = 1
        _SequenceFrame ("序列帧起始帧", Float) = 0
        _SequenceSpeed ("序列帧速度", Float) = 12

        [Toggle] _EnablePolarUV ("启用极坐标 UV", Float) = 0
        _PolarCenter ("极坐标中心", Vector) = (0.5,0.5,0,0)
        _PolarRadialScale ("极坐标径向缩放", Float) = 1
        _PolarAngularScale ("极坐标角向缩放", Float) = 1
        _PolarRotationSpeed ("极坐标旋转速度", Float) = 0

        [Toggle] _EnableVertexStreams ("启用粒子顶点流", Float) = 0
        _VertexStreamUVStrength ("Custom1 XY · UV 偏移", Range(0,1)) = 1
        _VertexStreamFrameStrength ("Custom1 Z · 帧号偏移", Range(0,1)) = 1
        _VertexStreamDissolveStrength ("Custom1 W · 溶解增量", Range(0,1)) = 1
        _VertexStreamEmissionStrength ("Custom2 X · 自发光增量", Range(0,8)) = 1

        [Toggle] _EnableVertexAnimation ("启用顶点动画", Float) = 0
        _VertexAnimationDirection ("顶点动画局部方向", Vector) = (0,1,0,0)
        _VertexAnimationAmplitude ("顶点动画幅度", Range(0,2)) = 0.1
        _VertexAnimationFrequency ("顶点动画频率", Range(0,20)) = 2
        _VertexAnimationSpeed ("顶点动画速度", Float) = 1
        [Enum(None,0,Red,1,Green,2,Blue,3,Alpha,4)] _VertexAnimationMask ("顶点色动画遮罩", Float) = 0

        [NoScaleOffset] _NoiseTex ("噪声纹理", 2D) = "gray" {}
        _NoiseScale ("噪声缩放", Vector) = (1,1,0,0)
        _NoiseSpeed ("噪声速度", Vector) = (0,0,0,0)
        _Distortion ("扰动强度", Range(0,0.2)) = 0

        [Toggle] _EnableFlow ("启用纹理流动", Float) = 0
        _FlowSpeed ("流动速度", Vector) = (0,0,0,0)
        _FlowStrength ("流动强度", Range(0,1)) = 1
        [Toggle] _EnableFlowMap ("启用流向贴图", Float) = 0
        [NoScaleOffset] _FlowMap ("流向贴图", 2D) = "gray" {}
        _FlowMapScale ("流向贴图缩放/偏移", Vector) = (1,1,0,0)
        _FlowMapSpeed ("流向贴图速度", Vector) = (0,0,0,0)
        _FlowMapStrength ("流向贴图强度", Range(0,0.2)) = 0.03

        [Toggle] _EnableShine ("启用扫光", Float) = 0
        [HDR] _ShineColor ("扫光颜色", Color) = (1,1,1,1)
        _ShineSpeed ("扫光速度", Float) = 1
        _ShineWidth ("扫光宽度", Range(0.001,1)) = 0.15
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

        [Toggle] _EnableRadialMask ("启用径向遮罩", Float) = 0
        _RadialMaskCenter ("径向遮罩中心", Vector) = (0.5,0.5,0,0)
        _RadialMaskRadius ("径向遮罩半径", Range(0,1.5)) = 0.5
        _RadialMaskSoftness ("径向遮罩柔和度", Range(0.001,1)) = 0.1
        [Toggle] _RadialMaskInvert ("反转径向遮罩", Float) = 0

        [Enum(Off,0,Dissolve,1,EdgeDissolve,2)] _DissolveMode ("溶解模式", Float) = 0
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
        [Toggle] _EnableFresnelMask ("启用菲涅尔遮罩", Float) = 0
        _FresnelPower ("菲涅尔幂次", Range(0.1,8)) = 2
        _FresnelMin ("菲涅尔起点", Range(0,1)) = 0
        _FresnelMax ("菲涅尔终点", Range(0,1)) = 1
        _FresnelAlphaInfluence ("菲涅尔透明度影响", Range(0,1)) = 1
        [HDR] _FresnelColor ("菲涅尔颜色", Color) = (0.1,0.6,1,1)
        _FresnelIntensity ("菲涅尔发光强度", Range(0,8)) = 0

        [Toggle] _EnableGlitch ("启用故障", Float) = 0
        _GlitchAmount ("故障偏移", Range(0,0.2)) = 0.02
        _GlitchSpeed ("故障速度", Float) = 3
        [HDR] _EmissionColor ("自发光颜色", Color) = (0,0,0,1)

        [Toggle] _EnableSoftParticles ("启用软粒子", Float) = 0
        _SoftParticleNear ("软粒子起始距离", Range(0,5)) = 0
        _SoftParticleFar ("软粒子结束距离", Range(0.001,10)) = 1
        [Toggle] _EnableDepthIntersection ("启用深度交界发光", Float) = 0
        [HDR] _DepthIntersectionColor ("深度交界颜色", Color) = (0.2,0.8,1,1)
        _DepthIntersectionDistance ("深度交界距离", Range(0.001,5)) = 0.25
        _DepthIntersectionIntensity ("深度交界强度", Range(0,8)) = 1

        [Toggle] _AlphaClip ("启用透明裁剪", Float) = 0
        _Cutoff ("透明裁剪阈值", Range(0,1)) = 0.01
        [Enum(Basic,0,Standard,1,High,2)] _QualityTier ("效果质量档位", Float) = 1

        [Enum(Alpha,0,Additive,1,Premultiply,2,Multiply,3)] _BlendMode ("混合模式", Float) = 0
        [Enum(Off,0,On,1)] _ZWriteMode ("深度写入", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("深度测试", Float) = 4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("剔除模式", Float) = 0
        _QueueOffset ("渲染队列偏移", Range(-50,50)) = 0
        [HideInInspector] _SrcBlend ("源混合因子", Float) = 5
        [HideInInspector] _DstBlend ("目标混合因子", Float) = 10
        [HideInInspector] _BlendOp ("混合操作", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        BlendOp [_BlendOp]
        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]
        ZWrite [_ZWriteMode]
        ZTest [_ZTest]

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_FlowMap); SAMPLER(sampler_FlowMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _Color;
                float4 _MainTexScaleOffset;
                float _TimeMode;
                float _CustomTime;
                float _TimeScale;
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

            float _ESUnscaledTime;
            float _ESUnscaledTimeValid;

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

            float ESGetTime()
            {
                float baseTime = _TimeMode > 1.5
                    ? _CustomTime
                    : (_TimeMode > 0.5 ? (_ESUnscaledTimeValid > 0.5 ? _ESUnscaledTime : _Time.y) : _Time.y);
                return baseTime * max(0.0, _TimeScale);
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

            half4 ESBlurSample(float2 uv, float4 atlasBounds)
            {
                float2 delta = _MainTex_TexelSize.xy * (_BlurRadius * 512.0);
                half4 result = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv, atlasBounds)) * 0.4h;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv + float2(delta.x, 0.0), atlasBounds)) * 0.15h;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv - float2(delta.x, 0.0), atlasBounds)) * 0.15h;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv + float2(0.0, delta.y), atlasBounds)) * 0.15h;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv - float2(0.0, delta.y), atlasBounds)) * 0.15h;
                return result;
            }

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
                    uv += (noise - 0.5) * _Distortion;
                }
            #endif

            #if defined(_ES_QUALITY_HIGH)
                if (_EnableGlitch > 0.5)
                    uv.x += (ESRandom(float2(floor(input.positionWS.y * _GlitchSpeed + timeValue), 0.0)) - 0.5) * _GlitchAmount;
            #endif

                if (_EnableFlow > 0.5)
                    uv += _FlowSpeed.xy * timeValue * _FlowStrength;
                uv = ESApplyFlowMap(uv, timeValue);
                uv = ESWrapSequenceUV(uv, atlasBounds);

                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv, atlasBounds)) * input.color;
            #if defined(_ES_QUALITY_HIGH)
                if (_EnableBlur > 0.5)
                    source = lerp(source, ESBlurSample(uv, atlasBounds) * input.color, saturate(_BlurIntensity));
            #endif

                float alpha = source.a;
                float3 color = source.rgb;

            #if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
                if (_EnableChromatic > 0.5)
                {
                    float2 chromaticDirection = float2(cos(radians(_ChromaticAngle)), sin(radians(_ChromaticAngle)));
                    float edgeFactor = saturate(length(frac(uv) - 0.5) * 2.0);
                    float amount = _ChromaticOffset * lerp(1.0, edgeFactor, _ChromaticEdgeOnly);
                    float3 chromaticColor = color;
                    chromaticColor.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv + chromaticDirection * amount, atlasBounds)).r * input.color.r;
                    chromaticColor.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESClampSequenceSample(uv - chromaticDirection * amount, atlasBounds)).b * input.color.b;
                    color = lerp(color, chromaticColor, saturate(_ChromaticIntensity));
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
                    float shine = 1.0 - smoothstep(0.0, _ShineWidth, abs(frac(input.positionWS.y + timeValue * _ShineSpeed) - 0.5));
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
                    float2 sparkleCell = floor(uv * sparkleScale);
                    float sparkleSeed = ESRandom(sparkleCell);
                    float sparkleWave = 0.5 + 0.5 * sin(timeValue * _SparkleSpeed + sparkleSeed * 6.2831853);
                    float2 sparkleLocal = frac(uv * sparkleScale) - 0.5;
                    float sparkleRadial = saturate(1.0 - length(sparkleLocal) * 2.0);
                    float sparkleCross = max(saturate(1.0 - abs(sparkleLocal.x) * 8.0), saturate(1.0 - abs(sparkleLocal.y) * 8.0));
                    float sparkleShape = saturate(sparkleRadial * 0.35 + sparkleCross * 0.65);
                    float sparkle = step(1.0 - _SparkleDensity, sparkleSeed) * pow(saturate(sparkleWave * sparkleShape), max(1.0, _SparkleSharpness));
                    color += _SparkleColor.rgb * sparkle * _SparkleIntensity;
                }
                if (_EnableHologram > 0.5)
                {
                    float line = step(_HologramGap, frac(input.positionWS.y * _HologramFrequency + timeValue * _HologramSpeed));
                    color = lerp(color, _HologramColor.rgb, 0.6);
                    alpha *= max(_HologramMinAlpha, line);
                }
            #endif

                float emissionMultiplier = 1.0 + customData2 * _VertexStreamEmissionStrength;
                color += _EmissionColor.rgb * max(0.0, emissionMultiplier);
                if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
                if (_BlendMode > 1.5 && _BlendMode < 2.5) color *= alpha;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "ES.EditorInternal.ESCompositeShaderGUI"
}
