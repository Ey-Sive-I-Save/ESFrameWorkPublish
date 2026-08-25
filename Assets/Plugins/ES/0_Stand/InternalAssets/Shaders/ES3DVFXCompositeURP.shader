Shader "ES/3D/VFX Composite URP"
{
    Properties
    {
        [HideInInspector] _ESMaterialVersion ("ES Material Version", Float) = 1
        // Base Input
        [MainTexture] _MainTex ("主纹理", 2D) = "white" {}
        [MainColor] _Color ("基础颜色", Color) = (1,1,1,1)
        _VertexColorStrength ("顶点色强度", Range(0,1)) = 1

        // Time And Coordinates
        _MainTexScaleOffset ("主纹理缩放/偏移", Vector) = (1,1,0,0)
        [Enum(SceneTime,0,UnscaledTime,1,CustomTime,2)] _TimeMode ("时间来源", Float) = 0
        _CustomTime ("自定义时间", Float) = 0
        _TimeScale ("时间倍率", Range(-4,4)) = 1
        [Toggle] _EnableTimeFPS ("启用时间帧率量化", Float) = 0
        _TimeFPS ("时间帧率", Range(0.01,240)) = 5
        [Toggle] _EnableTimeFrequency ("启用周期时间", Float) = 0
        _TimeFrequency ("时间周期频率", Float) = 2
        _TimeRange ("时间周期范围", Float) = 0.5

        // Particle Input - Sequence
        [Toggle] _EnableSequence ("启用序列帧", Float) = 0
        [Enum(Manual,0,Time,1,VertexStream,2)] _SequencePlayback ("序列帧播放方式", Float) = 1
        _SequenceColumns ("序列帧列数", Float) = 1
        _SequenceRows ("序列帧行数", Float) = 1
        _SequenceFrame ("序列帧起始帧", Float) = 0
        _SequenceSpeed ("序列帧速度", Float) = 12

        // Deformation And Flow - Polar UV
        [Toggle] _EnablePolarUV ("启用极坐标 UV", Float) = 0
        _PolarCenter ("极坐标中心", Vector) = (0.5,0.5,0,0)
        _PolarRadialScale ("极坐标径向缩放", Float) = 1
        _PolarAngularScale ("极坐标角向缩放", Float) = 1
        _PolarRotationSpeed ("极坐标旋转速度", Float) = 0

        // Particle Input - Vertex Streams
        [Toggle] _EnableVertexStreams ("启用粒子顶点流", Float) = 0
        _VertexStreamUVStrength ("Custom1 XY · UV 偏移", Range(0,1)) = 1
        _VertexStreamFrameStrength ("Custom1 Z · 帧号偏移", Range(0,1)) = 1
        _VertexStreamDissolveStrength ("Custom1 W · 溶解增量", Range(0,1)) = 1
        _VertexStreamEmissionStrength ("Custom2 X · 自发光增量", Range(0,8)) = 1

        // Deformation And Flow
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
        _DistortionDirection ("扰动方向与轴强度", Vector) = (1,1,0,0)

        [Toggle] _EnableFlow ("启用纹理流动", Float) = 0
        _FlowSpeed ("流动速度", Vector) = (0,0,0,0)
        _FlowStrength ("流动强度", Range(0,1)) = 1
        [Toggle] _EnableFlowMap ("启用流向贴图", Float) = 0
        [NoScaleOffset] _FlowMap ("流向贴图", 2D) = "gray" {}
        _FlowMapScale ("流向贴图缩放/偏移", Vector) = (1,1,0,0)
        _FlowMapSpeed ("流向贴图速度", Vector) = (0,0,0,0)
        _FlowMapStrength ("流向贴图强度", Range(0,0.2)) = 0.03

        // Dynamic Effects
        [Toggle] _EnableShine ("启用扫光", Float) = 0
        [HDR] _ShineColor ("扫光颜色", Color) = (1,1,1,1)
        _ShineSpeed ("扫光速度", Float) = 1
        _ShineWidth ("扫光宽度", Range(0.001,1)) = 0.15
        _ShineIntensity ("扫光强度", Range(0,8)) = 1
        [Enum(CompatibleDefault,0,LocalUV,1,WorldProjection,2)] _ShineSpace ("扫光空间", Float) = 0
        _ShineDirection ("扫光方向", Vector) = (0,1,0,0)
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

        // Masks And Dissolve
        [Toggle] _EnableRadialMask ("启用径向遮罩", Float) = 0
        _RadialMaskCenter ("径向遮罩中心", Vector) = (0.5,0.5,0,0)
        _RadialMaskRadius ("径向遮罩半径", Range(0,1.5)) = 0.5
        _RadialMaskSoftness ("径向遮罩柔和度", Range(0.001,1)) = 0.1
        [Toggle] _RadialMaskInvert ("反转径向遮罩", Float) = 0

        [Enum(Off,0,Dissolve,1,EdgeDissolve,2)] _DissolveMode ("溶解模式", Float) = 0
        _DissolveProgress ("溶解进度", Range(0,1)) = 0
        _DissolveWidth ("溶解边缘宽度", Range(0.001,1)) = 0.1
        [HDR] _DissolveColor ("溶解边缘颜色", Color) = (1,0.1,0.01,1)

        // Dynamic Effects - Hologram
        [Toggle] _EnableHologram ("启用全息", Float) = 0
        [HDR] _HologramColor ("全息颜色", Color) = (0.1,0.8,1,1)
        _HologramFrequency ("全息线频率", Float) = 60
        _HologramGap ("全息线间隔", Range(0,1)) = 0.35
        _HologramSpeed ("全息速度", Float) = 1
        _HologramMinAlpha ("全息最低透明度", Range(0,1)) = 0.2
        _HologramFade ("ESNative 全息淡入", Range(0,1)) = 1
        _HologramContrast ("ESNative 全息对比度", Float) = 1
        [Enum(LocalUV,0,WorldProjection,1)] _HologramSpace ("全息扫描空间", Float) = 1
        _HologramDirection ("全息扫描方向", Vector) = (0,1,0,0)
        _HologramLineFrequency ("ESNative 全息线频率", Float) = 60
        _HologramLineGap ("ESNative 全息线间隔", Float) = 0.35
        _HologramDistortionOffset ("ESNative 全息扰动偏移", Float) = 0.5
        _HologramDistortionDirection ("全息扰动方向", Vector) = (1,0,0,0)
        _HologramDistortionSpeed ("ESNative 全息扰动速度", Float) = 2
        _HologramDistortionDensity ("ESNative 全息扰动密度", Float) = 0.5
        _HologramDistortionScale ("ESNative 全息扰动缩放", Float) = 10

        // Dynamic Effects - Rim
        [Toggle] _EnableRim ("启用边缘光", Float) = 0
        [HDR] _RimColor ("边缘光颜色", Color) = (0.1,0.6,1,1)
        _RimPower ("边缘光幂次", Range(0.1,8)) = 3
        _RimIntensity ("边缘光强度", Range(0,8)) = 1
        // Masks And Dissolve - Fresnel
        [Toggle] _EnableFresnelMask ("启用菲涅尔遮罩", Float) = 0
        _FresnelPower ("菲涅尔幂次", Range(0.1,8)) = 2
        _FresnelMin ("菲涅尔起点", Range(0,1)) = 0
        _FresnelMax ("菲涅尔终点", Range(0,1)) = 1
        _FresnelAlphaInfluence ("菲涅尔透明度影响", Range(0,1)) = 1
        [HDR] _FresnelColor ("菲涅尔颜色", Color) = (0.1,0.6,1,1)
        _FresnelIntensity ("菲涅尔发光强度", Range(0,8)) = 0

        // Dynamic Effects - Glitch And Emission
        [Toggle] _EnableGlitch ("启用故障", Float) = 0
        _GlitchAmount ("故障偏移", Range(0,0.2)) = 0.02
        _GlitchSpeed ("故障速度", Float) = 3
        _GlitchScanDirection ("故障条带方向", Vector) = (0,1,0,0)
        _GlitchFade ("ESNative 故障淡入", Range(0,1)) = 1
        _GlitchMaskMin ("ESNative 故障遮罩下限", Range(0,1)) = 0.4
        _GlitchMaskScale ("ESNative 故障遮罩缩放", Vector) = (0,0.2,0,0)
        _GlitchMaskSpeed ("ESNative 故障遮罩速度", Vector) = (0,4,0,0)
        _GlitchHueSpeed ("ESNative 故障色相速度", Float) = 0.5
        _GlitchBrightness ("ESNative 故障亮度", Float) = 2
        _GlitchNoiseScale ("ESNative 故障噪声缩放", Vector) = (0,3,0,0)
        _GlitchNoiseSpeed ("ESNative 故障噪声速度", Vector) = (0,1,0,0)
        _GlitchDistortion ("ESNative 故障位移", Vector) = (0.1,0,0,0)
        _GlitchDistortionScale ("ESNative 故障位移缩放", Vector) = (0,3,0,0)
        _GlitchDistortionSpeed ("ESNative 故障位移速度", Vector) = (0,1,0,0)
        [Toggle] _ESNativeStatusContract ("使用 ESNative 精确全息/故障合同", Float) = 0
        [HDR] _EmissionColor ("自发光颜色", Color) = (0,0,0,1)

        // Depth Interaction
        [Toggle] _EnableSoftParticles ("启用软粒子", Float) = 0
        _SoftParticleNear ("软粒子起始距离", Range(0,5)) = 0
        _SoftParticleFar ("软粒子结束距离", Range(0.001,10)) = 1
        [Toggle] _EnableDepthIntersection ("启用深度交界发光", Float) = 0
        [HDR] _DepthIntersectionColor ("深度交界颜色", Color) = (0.2,0.8,1,1)
        _DepthIntersectionDistance ("深度交界距离", Range(0.001,5)) = 0.25
        _DepthIntersectionIntensity ("深度交界强度", Range(0,8)) = 1

        // Output And Quality
        [Toggle] _AlphaClip ("启用透明裁剪", Float) = 0
        _Cutoff ("透明裁剪阈值", Range(0,1)) = 0.01
        [Enum(Basic,0,Standard,1,High,2)] _QualityTier ("效果质量档位", Float) = 0

        // Render State
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

            #include "ES3DVFXCompositeURPCommon.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "ES.EditorInternal.ESCompositeShaderGUI"
}
