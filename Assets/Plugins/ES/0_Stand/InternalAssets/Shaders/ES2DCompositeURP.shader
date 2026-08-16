Shader "ES/2D/Composite URP"
{
    Properties
    {
        [PerRendererData] _MainTex ("主纹理", 2D) = "white" {}
        _Color ("基础颜色", Color) = (1,1,1,1)
        _MainTexScaleOffset ("主纹理缩放/偏移", Vector) = (1,1,0,0)
        _VertexColorStrength ("顶点色强度", Range(0,1)) = 1
        [Enum(UV,0,World,1,Screen,2)] _CoordinateMode ("坐标空间", Float) = 0
        [Enum(SceneTime,0,UnscaledTime,1,CustomTime,2)] _TimeMode ("时间来源", Float) = 0
        _CustomTime ("自定义时间", Float) = 0
        _TimeScale ("时间倍率", Range(0,4)) = 1

        [Enum(Off,0,Sequence,1)] _AnimationMode ("序列帧模式", Float) = 0
        _SequenceColumns ("序列帧列数", Float) = 1
        _SequenceRows ("序列帧行数", Float) = 1
        _SequenceFrame ("序列帧当前帧", Float) = 0
        _SequenceSpeed ("序列帧速度", Float) = 0

        [Enum(Off,0,Directional,1,TextureMask,2,NoiseDissolve,3)] _FadeMode ("渐隐模式", Float) = 0
        _FadeProgress ("渐隐进度", Range(0,1)) = 0
        _FadePosition ("渐隐位置", Vector) = (0.5,0.5,0,0)
        _FadeWidth ("渐隐宽度", Range(0.001,1)) = 0.1
        _FadeNoiseFactor ("渐隐噪声影响", Range(0,1)) = 0.2
        [NoScaleOffset] _FadeMask ("渐隐遮罩", 2D) = "white" {}
        [HDR] _DissolveEdgeColor ("溶解边缘颜色", Color) = (1,0.15,0.01,1)
        _DissolveEdgeWidth ("溶解边缘宽度", Range(0.001,1)) = 0.08

        [Toggle] _EnableAddColor ("启用叠加颜色", Float) = 0
        [HDR] _AddColor ("叠加颜色", Color) = (1,0,0,1)
        _AddColorFade ("叠加颜色强度", Range(0,1)) = 1
        [Toggle] _EnableStrongTint ("启用强制染色", Float) = 0
        [HDR] _StrongTint ("强制染色", Color) = (1,1,1,1)
        _StrongTintFade ("强制染色强度", Range(0,1)) = 1
        [Toggle] _EnableAlphaTint ("启用透明染色", Float) = 0
        [HDR] _AlphaTint ("透明染色", Color) = (1,1,1,1)
        _AlphaTintMin ("透明染色最低透明度", Range(0,1)) = 0.02
        [Toggle] _EnableColorReplace ("启用颜色替换", Float) = 0
        _ReplaceFrom ("替换源颜色", Color) = (0,0,0,1)
        [HDR] _ReplaceTo ("替换目标颜色", Color) = (1,1,1,1)
        _ReplaceRange ("替换范围", Range(0,1)) = 0.1
        _ReplaceSoftness ("替换柔和度", Range(0.001,1)) = 0.1

        [Toggle] _EnableBrightness ("启用亮度", Float) = 0
        _Brightness ("亮度", Range(0,4)) = 1
        [Toggle] _EnableContrast ("启用对比度", Float) = 0
        _Contrast ("对比度", Range(0,4)) = 1
        [Toggle] _EnableSaturation ("启用饱和度", Float) = 0
        _Saturation ("饱和度", Range(0,4)) = 1
        [Toggle] _EnableHue ("启用色相偏移", Float) = 0
        _Hue ("色相偏移", Range(-1,1)) = 0
        [Toggle] _EnableNegative ("启用负片", Float) = 0
        _NegativeFade ("负片强度", Range(0,1)) = 1
        [Toggle] _EnableRainbow ("启用彩虹渐变", Float) = 0
        _RainbowSpeed ("彩虹速度", Float) = 1
        _RainbowDensity ("彩虹密度", Float) = 1
        _RainbowBrightness ("彩虹亮度", Range(0,4)) = 1

        [Toggle] _EnableInnerOutline ("启用内描边", Float) = 0
        [HDR] _InnerOutlineColor ("内描边颜色", Color) = (1,0.2,0.05,1)
        _InnerOutlineWidth ("内描边宽度", Range(0,1)) = 0.08
        [Toggle] _EnableOuterOutline ("启用外描边", Float) = 0
        [HDR] _OuterOutlineColor ("外描边颜色", Color) = (0,0,0,1)
        _OuterOutlineWidth ("外描边宽度", Range(0,0.05)) = 0.005
        [Toggle] _EnablePixelOutline ("启用像素描边", Float) = 0
        [HDR] _PixelOutlineColor ("像素描边颜色", Color) = (1,1,1,1)
        _PixelOutlineWidth ("像素描边宽度", Range(0,4)) = 1

        [Toggle] _EnableShine ("启用扫光", Float) = 0
        [HDR] _ShineColor ("扫光颜色", Color) = (1,1,1,1)
        _ShineSpeed ("扫光速度", Float) = 1
        _ShineWidth ("扫光宽度", Range(0.001,1)) = 0.15
        _ShineAngle ("扫光角度", Range(0,360)) = 30
        _ShineIntensity ("扫光强度", Range(0,8)) = 1
        [Toggle] _EnableSparkle ("启用亮晶晶", Float) = 0
        [HDR] _SparkleColor ("亮晶晶颜色", Color) = (1,1,1,1)
        _SparkleScale ("亮晶晶密度", Range(1,128)) = 32
        _SparkleSpeed ("亮晶晶速度", Float) = 2
        _SparkleDensity ("亮晶晶数量", Range(0,1)) = 0.18
        _SparkleSharpness ("亮晶晶锐度", Range(1,16)) = 6
        _SparkleIntensity ("亮晶晶强度", Range(0,8)) = 1
        [Toggle] _EnableFlow ("启用纹理流动", Float) = 0
        _FlowSpeed ("流动速度", Vector) = (0,0,0,0)
        _FlowStrength ("流动强度", Range(0,1)) = 1
        [Toggle] _EnableChromatic ("启用色差", Float) = 0
        _ChromaticOffset ("色差偏移", Range(0,0.02)) = 0.002
        _ChromaticIntensity ("色差强度", Range(0,1)) = 1
        _ChromaticEdgeOnly ("边缘色差", Range(0,1)) = 0.5
        _ChromaticAngle ("色差方向", Range(0,360)) = 0
        [Toggle] _EnableBlur ("启用纹理模糊", Float) = 0
        _BlurRadius ("模糊半径", Range(0,0.02)) = 0.002
        _BlurIntensity ("模糊强度", Range(0,1)) = 0.5
        [Toggle] _EnablePingPongGlow ("启用往返发光", Float) = 0
        [HDR] _GlowFrom ("往返发光起点", Color) = (1,0,0,1)
        [HDR] _GlowTo ("往返发光终点", Color) = (0,0.3,1,1)
        _GlowFrequency ("往返发光频率", Float) = 2
        _GlowIntensity ("往返发光强度", Range(0,8)) = 1

        [Toggle] _EnableDistortion ("启用噪声扰动", Float) = 0
        [NoScaleOffset] _NoiseTex ("噪声纹理", 2D) = "gray" {}
        _NoiseScale ("噪声缩放", Vector) = (1,1,0,0)
        _NoiseSpeed ("噪声速度", Vector) = (0,0,0,0)
        _DistortionStrength ("扰动强度", Range(0,0.2)) = 0.02

        [Toggle] _EnableHologram ("启用全息", Float) = 0
        [HDR] _HologramColor ("全息颜色", Color) = (0.1,0.8,1,1)
        _HologramLineFrequency ("全息线频率", Float) = 80
        _HologramLineGap ("全息线间隔", Range(0,1)) = 0.35
        _HologramSpeed ("全息速度", Float) = 1
        _HologramMinAlpha ("全息最低透明度", Range(0,1)) = 0.2
        [Toggle] _EnableGlitch ("启用故障", Float) = 0
        _GlitchIntensity ("故障强度", Range(0,0.2)) = 0.03
        _GlitchSpeed ("故障速度", Float) = 3

        [Toggle] _EnableFrozen ("启用冰冻", Float) = 0
        [HDR] _FrozenColor ("冰冻颜色", Color) = (0.3,0.8,1,1)
        [HDR] _FrozenHighlight ("冰冻高光", Color) = (1,1,1,1)
        _FrozenDensity ("冰冻雪花密度", Range(0,1)) = 0.35
        _FrozenSpeed ("冰冻流动速度", Float) = 0.2
        [Toggle] _EnableBurn ("启用燃烧", Float) = 0
        [HDR] _BurnEdgeColor ("燃烧边缘颜色", Color) = (1,0.1,0.01,1)
        [HDR] _BurnInsideColor ("燃烧内部颜色", Color) = (0.2,0.02,0,1)
        _BurnProgress ("燃烧进度", Range(0,1)) = 0
        _BurnWidth ("燃烧边缘宽度", Range(0.001,1)) = 0.1
        [Toggle] _EnablePoison ("启用中毒", Float) = 0
        [HDR] _PoisonColor ("中毒颜色", Color) = (0.2,1,0.1,1)
        _PoisonDensity ("中毒密度", Float) = 3
        _PoisonSpeed ("中毒速度", Float) = 1

        [Toggle] _AlphaClip ("启用透明裁剪", Float) = 0
        _Cutoff ("透明裁剪阈值", Range(0,1)) = 0.01
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "ES2DComposite"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex ESVertex
            #pragma fragment ESFragment
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_FadeMask); SAMPLER(sampler_FadeMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float4 _MainTex_TexelSize; half4 _Color; float4 _MainTexScaleOffset; float _VertexColorStrength;
                float _CoordinateMode; float _TimeMode; float _CustomTime; float _TimeScale;
                float _AnimationMode; float _SequenceColumns; float _SequenceRows; float _SequenceFrame; float _SequenceSpeed;
                float _FadeMode; float _FadeProgress; float4 _FadePosition; float _FadeWidth; float _FadeNoiseFactor; half4 _DissolveEdgeColor; float _DissolveEdgeWidth;
                half4 _AddColor; float _AddColorFade; float _EnableAddColor; float _EnableStrongTint; half4 _StrongTint; float _StrongTintFade;
                float _EnableAlphaTint; half4 _AlphaTint; float _AlphaTintMin; float _EnableColorReplace; half4 _ReplaceFrom; half4 _ReplaceTo; float _ReplaceRange; float _ReplaceSoftness;
                float _EnableBrightness; float _Brightness; float _EnableContrast; float _Contrast; float _EnableSaturation; float _Saturation; float _EnableHue; float _Hue; float _EnableNegative; float _NegativeFade;
                float _EnableRainbow; float _RainbowSpeed; float _RainbowDensity; float _RainbowBrightness;
                float _EnableInnerOutline; half4 _InnerOutlineColor; float _InnerOutlineWidth; float _EnableOuterOutline; half4 _OuterOutlineColor; float _OuterOutlineWidth; float _EnablePixelOutline; half4 _PixelOutlineColor; float _PixelOutlineWidth;
                float _EnableShine; half4 _ShineColor; float _ShineSpeed; float _ShineWidth; float _ShineAngle; float _ShineIntensity;
                float _EnableSparkle; half4 _SparkleColor; float _SparkleScale; float _SparkleSpeed; float _SparkleDensity; float _SparkleSharpness; float _SparkleIntensity;
                float _EnableFlow; float4 _FlowSpeed; float _FlowStrength;
                float _EnableChromatic; float _ChromaticOffset; float _ChromaticIntensity; float _ChromaticEdgeOnly; float _ChromaticAngle;
                float _EnableBlur; float _BlurRadius; float _BlurIntensity;
                float _EnablePingPongGlow; half4 _GlowFrom; half4 _GlowTo; float _GlowFrequency; float _GlowIntensity;
                float _EnableDistortion; float4 _NoiseScale; float4 _NoiseSpeed; float _DistortionStrength;
                float _EnableHologram; half4 _HologramColor; float _HologramLineFrequency; float _HologramLineGap; float _HologramSpeed; float _HologramMinAlpha; float _EnableGlitch; float _GlitchIntensity; float _GlitchSpeed;
                float _EnableFrozen; half4 _FrozenColor; half4 _FrozenHighlight; float _FrozenDensity; float _FrozenSpeed; float _EnableBurn; half4 _BurnEdgeColor; half4 _BurnInsideColor; float _BurnProgress; float _BurnWidth; float _EnablePoison; half4 _PoisonColor; float _PoisonDensity; float _PoisonSpeed;
                float _AlphaClip; float _Cutoff;
            CBUFFER_END

            struct ESAttributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct ESVaryings { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float3 positionWS:TEXCOORD1; float4 screenPosition:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float ESRandom(float2 p) { return frac(sin(dot(p, float2(12.9898,78.233))) * 43758.5453); }
            float _ESUnscaledTime; float _ESUnscaledTimeValid;
            float ESGetTime() { float baseTime = _TimeMode > 1.5 ? _CustomTime : (_TimeMode > 0.5 ? (_ESUnscaledTimeValid > 0.5 ? _ESUnscaledTime : _Time.y) : _Time.y); return baseTime * max(0,_TimeScale); }
            float3 ESRgbToHsv(float3 c) { float4 K=float4(0,-1.0/3.0,2.0/3.0,-1); float4 p=lerp(float4(c.bg,K.wz),float4(c.gb,K.xy),step(c.b,c.g)); float4 q=lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r)); float d=q.x-min(q.w,q.y); return float3(abs(q.z+(q.w-q.y)/(6*d+1e-5)),d/(q.x+1e-5),q.x); }
            float3 ESHsvToRgb(float3 c) { float3 p=abs(frac(c.xxx+float3(0,1.0/3.0,2.0/3.0))*6-3); return c.z*lerp(float3(1,1,1),saturate(p-1),c.y); }
            float2 ESSequenceUV(float2 uv,float time) { float cols=max(1,_SequenceColumns),rows=max(1,_SequenceRows); float frame=max(0,floor(_SequenceFrame+(_AnimationMode>0.5?time*_SequenceSpeed:0))); float2 cell=1/float2(cols,rows); return uv*cell+float2(fmod(frame,cols),rows-1-fmod(floor(frame/cols),rows))*cell; }
            float2 ESOutlineUV(float2 uv,float2 offset) { return uv+offset; }
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

            ESVaryings ESVertex(ESAttributes v)
            {
                ESVaryings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o);
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz); o.positionHCS=p.positionCS; o.positionWS=p.positionWS; o.screenPosition=ComputeScreenPos(p.positionCS); o.uv=TRANSFORM_TEX(v.uv,_MainTex); o.uv=o.uv*_MainTexScaleOffset.xy+_MainTexScaleOffset.zw; o.color=lerp(half4(1,1,1,1),v.color,_VertexColorStrength)*_Color; return o;
            }

            half4 ESFragment(ESVaryings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); float time=ESGetTime(); float2 uv=_AnimationMode>0.5?ESSequenceUV(i.uv,time):i.uv; float2 coord=uv;
                if(_CoordinateMode>0.5&&_CoordinateMode<1.5) coord=i.positionWS.xz; else if(_CoordinateMode>1.5) coord=i.screenPosition.xy/max(i.screenPosition.w,1e-4);
                float2 noiseUV=coord*_NoiseScale.xy+_NoiseSpeed.xy*time; half noise=0.5h;
                if(_EnableDistortion>0.5||(_FadeMode>0.5&&(_FadeMode>2.5||_FadeNoiseFactor>0.0001))||_EnableFrozen>0.5||_EnableBurn>0.5||_EnablePoison>0.5)
                    noise=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,noiseUV).r;
                if(_EnableGlitch>0.5) uv+=float2((ESRandom(floor(coord.y*_GlitchSpeed+time*_GlitchSpeed))-0.5)*_GlitchIntensity,0);
                if(_EnableDistortion>0.5) uv+=(noise-0.5)*_DistortionStrength;
                if (_EnableFlow > 0.5) uv += _FlowSpeed.xy * time * _FlowStrength;
                half4 src=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv)*i.color;
                if (_EnableBlur > 0.5) src = lerp(src, ESBlurSample(uv) * i.color, saturate(_BlurIntensity));
                if(_AlphaClip>0.5) clip(src.a-_Cutoff); float alpha=src.a; float3 c=src.rgb;
                if (_EnableChromatic > 0.5)
                {
                    float2 chromaDir = float2(cos(radians(_ChromaticAngle)), sin(radians(_ChromaticAngle)));
                    float2 localCoord = frac(coord);
                    float edgeFactor = saturate(length(localCoord - 0.5) * 2.0);
                    float amount = _ChromaticOffset * lerp(1.0, edgeFactor, _ChromaticEdgeOnly);
                    float3 chroma = c;
                    chroma.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + chromaDir * amount).r * i.color.r;
                    chroma.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - chromaDir * amount).b * i.color.b;
                    c = lerp(c, chroma, saturate(_ChromaticIntensity));
                }

                if(_FadeMode>0.5) { float mask=_FadeMode>2.5?noise:(_FadeMode>1.5?SAMPLE_TEXTURE2D(_FadeMask,sampler_FadeMask,uv).r:saturate(dot(coord-_FadePosition.xy,float2(1,1))+0.5)); mask=lerp(mask,noise,_FadeNoiseFactor); float fade=smoothstep(_FadeProgress-_FadeWidth,_FadeProgress+_FadeWidth,mask); alpha*=(1-fade); if(_FadeMode>2.5) { float edge=1-smoothstep(_FadeProgress,_FadeProgress+_DissolveEdgeWidth,mask); c=lerp(c,_DissolveEdgeColor.rgb,edge); } }
                if(_EnableAddColor>0.5) c+=_AddColor.rgb*_AddColorFade;
                if(_EnableStrongTint>0.5) c=lerp(c,_StrongTint.rgb,_StrongTintFade);
                if(_EnableColorReplace>0.5) { float d=distance(c,_ReplaceFrom.rgb); c=lerp(c,_ReplaceTo.rgb,1-smoothstep(_ReplaceRange,_ReplaceRange+_ReplaceSoftness,d)); }
                if(_EnableBrightness>0.5) c*=_Brightness; if(_EnableContrast>0.5) c=(c-0.5)*_Contrast+0.5;
                if(_EnableSaturation>0.5) { float l=dot(c,float3(0.2126,0.7152,0.0722)); c=lerp(l.xxx,c,_Saturation); }
                if(_EnableHue>0.5) { float3 h=ESRgbToHsv(c); h.x=frac(h.x+_Hue); c=ESHsvToRgb(h); }
                if(_EnableNegative>0.5) c=lerp(c,1-c,_NegativeFade);
                if(_EnableRainbow>0.5) { float3 rainbow=ESHsvToRgb(float3(frac(coord.y*_RainbowDensity+time*_RainbowSpeed),1,1)); c=lerp(c,rainbow*_RainbowBrightness,0.5); }

                if(_EnableInnerOutline>0.5) { half n=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,ESOutlineUV(uv,float2(_InnerOutlineWidth,0))).a; half s=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,ESOutlineUV(uv,float2(-_InnerOutlineWidth,0))).a; float edge=saturate(src.a-min(n,s)); c=lerp(c,_InnerOutlineColor.rgb,edge); }
                if(_EnableOuterOutline>0.5||_EnablePixelOutline>0.5) { float w=_EnablePixelOutline>0.5?_PixelOutlineWidth/1024:_OuterOutlineWidth; half around=0; around=max(around,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+float2(w,0)).a); around=max(around,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-float2(w,0)).a); around=max(around,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+float2(0,w)).a); around=max(around,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-float2(0,w)).a); float edge=saturate(around-src.a); c=lerp(c,_EnablePixelOutline>0.5?_PixelOutlineColor.rgb:_OuterOutlineColor.rgb,edge); alpha=max(alpha,around); }
                if(_EnableShine>0.5) { float2 dir=float2(cos(radians(_ShineAngle)),sin(radians(_ShineAngle))); float shine=1-smoothstep(0,_ShineWidth,abs(frac(dot(coord,dir)+time*_ShineSpeed)-0.5)); c+=_ShineColor.rgb*shine*_ShineIntensity; }
                if(_EnableSparkle>0.5)
                {
                    float2 sparkleCell = floor(coord * max(1.0, _SparkleScale));
                    float sparkleSeed = ESRandom(sparkleCell);
                    float sparkleWave = 0.5 + 0.5 * sin(time * _SparkleSpeed + sparkleSeed * 6.2831853);
                    float2 sparkleLocal = frac(coord * max(1.0, _SparkleScale)) - 0.5;
                    float sparkleRadial = saturate(1.0 - length(sparkleLocal) * 2.0);
                    float sparkleCross = max(saturate(1.0 - abs(sparkleLocal.x) * 8.0), saturate(1.0 - abs(sparkleLocal.y) * 8.0));
                    float sparkleShape = saturate(sparkleRadial * 0.35 + sparkleCross * 0.65);
                    float sparkle = step(1.0 - _SparkleDensity, sparkleSeed) * pow(saturate(sparkleWave * sparkleShape), max(1.0, _SparkleSharpness));
                    c += _SparkleColor.rgb * sparkle * _SparkleIntensity;
                }
                if(_EnablePingPongGlow>0.5) { float wave=0.5+0.5*sin(time*_GlowFrequency); c+=lerp(_GlowFrom.rgb,_GlowTo.rgb,wave)*_GlowIntensity; }
                if(_EnableHologram>0.5) { float scanLine=step(_HologramLineGap,frac(coord.y*_HologramLineFrequency+time*_HologramSpeed)); c=lerp(c,_HologramColor.rgb,0.55); alpha*=max(_HologramMinAlpha,scanLine); }
                if(_EnableFrozen>0.5) { float snow=smoothstep(1-_FrozenDensity,1,noise); c=lerp(c,_FrozenColor.rgb,0.65); c+=_FrozenHighlight.rgb*snow*(0.5+0.5*sin(time*_FrozenSpeed+noise*6)); }
                if(_EnableBurn>0.5) { float burn=smoothstep(_BurnProgress-_BurnWidth,_BurnProgress+_BurnWidth,noise); c=lerp(_BurnInsideColor.rgb,_BurnEdgeColor.rgb,burn); alpha*=step(_BurnProgress-0.02,noise); }
                if(_EnablePoison>0.5) { float poison=0.5+0.5*sin(time*_PoisonSpeed+noise*_PoisonDensity*6); c=lerp(c,_PoisonColor.rgb,saturate(poison*0.45)); }
                if(_EnableAlphaTint>0.5) c=lerp(c,_AlphaTint.rgb,saturate(alpha+_AlphaTintMin));
                return half4(c,alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "ES.EditorInternal.ESCompositeShaderGUI"
}
