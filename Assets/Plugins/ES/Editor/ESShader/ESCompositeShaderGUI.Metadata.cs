using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public sealed partial class ESCompositeShaderGUI
    {
        #region Inspector Metadata

        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "_MainTex", "主纹理" }, { "_BaseMap", "基础颜色纹理" }, { "_Color", "颜色" }, { "_BaseColor", "基础颜色" },
            { "_NormalMap", "法线纹理" }, { "_NormalScale", "法线强度" }, { "_Metallic", "金属度" }, { "_Smoothness", "光滑度" },
            { "_UseNormalMap", "启用法线纹理" }, { "_OcclusionMap", "环境遮挡纹理" }, { "_Occlusion", "环境遮挡强度" },
            { "_UseEmission", "启用自发光" }, { "_EmissionMap", "自发光纹理" },
            { "_EmissionColor", "自发光颜色" }, { "_NoiseTex", "噪声纹理" }, { "_NoiseScale", "噪声缩放" }, { "_NoiseSpeed", "噪声速度" },
            { "_Distortion", "扰动强度" }, { "_DistortionStrength", "扰动强度" }, { "_DissolveMode", "溶解模式" },
            { "_DissolveProgress", "溶解进度" }, { "_DissolveSoftness", "溶解柔和度" }, { "_DissolveWidth", "溶解边缘宽度" },
            { "_DissolveEdgeColor", "溶解边缘颜色" }, { "_DissolveColor", "溶解颜色" }, { "_EnableRim", "启用边缘光" },
            { "_RimColor", "边缘光颜色" }, { "_RimPower", "边缘光幂次" }, { "_RimIntensity", "边缘光强度" },
            { "_EnableShine", "启用扫光" }, { "_ShineColor", "扫光颜色" }, { "_ShineSpeed", "扫光速度" }, { "_ShineWidth", "扫光宽度" },
            { "_ShineAngle", "扫光角度" }, { "_ShineDirection", "扫光方向" }, { "_ShineIntensity", "扫光强度" },
            { "_EnableSparkle", "启用亮晶晶" }, { "_SparkleColor", "亮晶晶颜色" }, { "_SparkleScale", "亮晶晶密度" }, { "_SparkleSpeed", "亮晶晶速度" },
            { "_SparkleDensity", "亮晶晶数量" }, { "_SparkleSharpness", "亮晶晶锐度" }, { "_SparkleIntensity", "亮晶晶强度" },
            { "_EnableFlow", "启用纹理流动" }, { "_FlowSpeed", "流动速度" }, { "_FlowStrength", "流动强度" },
            { "_EnableFlowMap", "启用流向贴图" }, { "_FlowMap", "流向贴图" }, { "_FlowMapScale", "流向贴图缩放/偏移" },
            { "_FlowMapSpeed", "流向贴图速度" }, { "_FlowMapStrength", "流向贴图强度" },
            { "_EnableVertexAnimation", "启用顶点动画" }, { "_VertexAnimationDirection", "顶点动画局部方向" },
            { "_VertexAnimationAmplitude", "顶点动画幅度" }, { "_VertexAnimationFrequency", "顶点动画频率" },
            { "_VertexAnimationSpeed", "顶点动画速度" }, { "_VertexAnimationMask", "顶点色动画遮罩" },
            { "_EnableSoftParticles", "启用软粒子" }, { "_SoftParticleNear", "软粒子起始距离" }, { "_SoftParticleFar", "软粒子结束距离" },
            { "_EnableChromatic", "启用色差" }, { "_ChromaticOffset", "色差偏移" }, { "_ChromaticIntensity", "色差强度" }, { "_ChromaticEdgeOnly", "边缘色差" }, { "_ChromaticAngle", "色差方向" },
            { "_EnableBlur", "启用纹理模糊" }, { "_BlurRadius", "模糊半径" }, { "_BlurIntensity", "模糊强度" },
            { "_EnableHologram", "启用全息" },
            { "_HologramColor", "全息颜色" }, { "_HologramFrequency", "全息线频率" }, { "_HologramLineFrequency", "全息线频率" },
            { "_HologramGap", "全息线间隔" }, { "_HologramLineGap", "全息线间隔" }, { "_HologramSpeed", "全息速度" },
            { "_HologramMinAlpha", "全息最低透明度" }, { "_EnableGlitch", "启用故障" }, { "_GlitchAmount", "故障强度" },
            { "_GlitchIntensity", "故障强度" }, { "_GlitchSpeed", "故障速度" }, { "_QualityTier", "效果质量档位" },
            { "_ReceiveShadows", "接收阴影" }, { "_AlphaClip", "启用透明裁剪" }, { "_Cutoff", "裁剪阈值" },
            { "_VertexColorStrength", "顶点色影响" }, { "_CoordinateMode", "坐标模式" }, { "_TimeMode", "时间来源" },
            { "_CustomTime", "自定义时间" }, { "_TimeScale", "时间倍率" }, { "_MainTexScaleOffset", "主纹理缩放/偏移" }, { "_AnimationMode", "动画模式" }, { "_SequenceColumns", "序列帧列数" },
            { "_SequenceRows", "序列帧行数" }, { "_SequenceFrame", "序列帧帧号" }, { "_SequenceSpeed", "序列帧速度" },
            { "_EnableSequence", "启用序列帧" }, { "_SequencePlayback", "序列帧播放方式" },
            { "_EnablePolarUV", "启用极坐标 UV" }, { "_PolarCenter", "极坐标中心" }, { "_PolarRadialScale", "径向缩放" },
            { "_PolarAngularScale", "角向缩放" }, { "_PolarRotationSpeed", "旋转速度" },
            { "_EnableVertexStreams", "启用粒子顶点流" }, { "_VertexStreamUVStrength", "Custom1 XY · UV 偏移" },
            { "_VertexStreamFrameStrength", "Custom1 Z · 帧号偏移" }, { "_VertexStreamDissolveStrength", "Custom1 W · 溶解增量" },
            { "_VertexStreamEmissionStrength", "Custom2 X · 自发光增量" },
            { "_EnableRadialMask", "启用径向遮罩" }, { "_RadialMaskCenter", "径向遮罩中心" },
            { "_RadialMaskRadius", "径向遮罩半径" }, { "_RadialMaskSoftness", "径向遮罩柔和度" }, { "_RadialMaskInvert", "反转径向遮罩" },
            { "_EnableFresnelMask", "启用菲涅尔遮罩" }, { "_FresnelPower", "菲涅尔幂次" }, { "_FresnelMin", "菲涅尔起点" },
            { "_FresnelMax", "菲涅尔终点" }, { "_FresnelAlphaInfluence", "透明度影响" }, { "_FresnelColor", "菲涅尔颜色" },
            { "_FresnelIntensity", "菲涅尔发光强度" }, { "_EnableDepthIntersection", "启用深度交界发光" },
            { "_DepthIntersectionColor", "深度交界颜色" }, { "_DepthIntersectionDistance", "深度交界距离" },
            { "_DepthIntersectionIntensity", "深度交界强度" }, { "_BlendMode", "混合模式" }, { "_ZWriteMode", "深度写入" },
            { "_ZTest", "深度测试" }, { "_Cull", "剔除模式" }, { "_QueueOffset", "渲染队列偏移" },
            { "_FadeMode", "渐隐模式" }, { "_FadeProgress", "渐隐进度" }, { "_FadePosition", "渐隐位置" }, { "_FadeWidth", "渐隐宽度" },
            { "_FadeNoiseFactor", "渐隐噪声影响" }, { "_FadeMask", "渐隐遮罩" }, { "_EnableAddColor", "启用叠加颜色" }, { "_AddColor", "叠加颜色" },
            { "_AddColorFade", "叠加颜色强度" }, { "_EnableStrongTint", "启用强制染色" }, { "_StrongTint", "强制染色" }, { "_StrongTintFade", "强制染色强度" },
            { "_EnableAlphaTint", "启用透明染色" }, { "_AlphaTint", "透明染色" }, { "_AlphaTintMin", "透明染色下限" },
            { "_EnableColorReplace", "启用颜色替换" }, { "_ReplaceFrom", "替换源颜色" }, { "_ReplaceTo", "替换目标颜色" },
            { "_ReplaceRange", "替换范围" }, { "_ReplaceSoftness", "替换柔和度" }, { "_EnableBrightness", "启用亮度" }, { "_Brightness", "亮度" },
            { "_EnableContrast", "启用对比度" }, { "_Contrast", "对比度" }, { "_EnableSaturation", "启用饱和度" }, { "_Saturation", "饱和度" },
            { "_EnableHue", "启用色相偏移" }, { "_Hue", "色相偏移" }, { "_EnableNegative", "启用负片" }, { "_NegativeFade", "负片强度" },
            { "_EnableRainbow", "启用彩虹渐变" }, { "_RainbowSpeed", "彩虹速度" }, { "_RainbowDensity", "彩虹密度" }, { "_RainbowBrightness", "彩虹亮度" },
            { "_EnableInnerOutline", "启用内描边" }, { "_InnerOutlineColor", "内描边颜色" }, { "_InnerOutlineWidth", "内描边宽度" },
            { "_EnableOuterOutline", "启用外描边" }, { "_OuterOutlineColor", "外描边颜色" }, { "_OuterOutlineWidth", "外描边宽度" },
            { "_EnablePixelOutline", "启用像素描边" }, { "_PixelOutlineColor", "像素描边颜色" }, { "_PixelOutlineWidth", "像素描边宽度" },
            { "_EnablePingPongGlow", "启用往返发光" }, { "_GlowFrom", "发光起点颜色" }, { "_GlowTo", "发光终点颜色" },
            { "_GlowFrequency", "发光频率" }, { "_GlowIntensity", "发光强度" }, { "_EnableDistortion", "启用噪声扰动" },
            { "_EnableFrozen", "启用冰冻" }, { "_FrozenColor", "冰冻颜色" }, { "_FrozenHighlight", "冰冻高光" },
            { "_FrozenDensity", "冰冻雪花密度" }, { "_FrozenSpeed", "冰冻流动速度" }, { "_EnableBurn", "启用燃烧" },
            { "_BurnEdgeColor", "燃烧边缘颜色" }, { "_BurnInsideColor", "燃烧内部颜色" }, { "_BurnProgress", "燃烧进度" }, { "_BurnWidth", "燃烧边缘宽度" },
            { "_EnablePoison", "启用中毒" }, { "_PoisonColor", "中毒颜色" }, { "_PoisonDensity", "中毒密度" }, { "_PoisonSpeed", "中毒速度" },
            { "_UseOcclusionMap", "使用环境遮挡纹理" }, { "_StencilComp", "Stencil 比较方式" },
            { "_Stencil", "Stencil ID" }, { "_StencilOp", "Stencil 操作" }, { "_StencilReadMask", "Stencil 读取掩码" },
            { "_StencilWriteMask", "Stencil 写入掩码" }, { "_ColorMask", "颜色写入掩码" }, { "_UseUIAlphaClip", "启用 UI 透明裁剪" }
        };

        private static readonly string[] TwoDCategoryOrder =
        {
            "基础输入", "时间与坐标", "遮罩与溶解", "色彩调整", "轮廓", "动态表现", "状态表现", "输出控制"
        };
        private static readonly string[] LitCategoryOrder =
        {
            "基础材质", "时间与形变", "光照输入", "遮罩与溶解", "动态表现", "输出与质量"
        };
        private static readonly string[] VfxCategoryOrder =
        {
            "基础输入", "时间与坐标", "粒子输入", "形变与流动", "遮罩与溶解", "动态表现", "深度交互", "输出与质量", "渲染状态"
        };
        private static readonly string[] UiCategoryOrder =
        {
            "基础输入", "时间与坐标", "动态表现", "遮罩与输出"
        };
        private static readonly Dictionary<string, Color> EffectAccentOverrides = new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            { "_UseNormalMap", new Color(0.20f, 0.72f, 0.92f, 1f) },
            { "_UseOcclusionMap", new Color(0.54f, 0.70f, 0.30f, 1f) },
            { "_UseEmission", new Color(1.00f, 0.62f, 0.18f, 1f) },
            { "_EnableDistortion", new Color(0.14f, 0.76f, 0.72f, 1f) },
            { "_EnableInnerOutline", new Color(0.58f, 0.48f, 0.96f, 1f) },
            { "_EnableOuterOutline", new Color(0.28f, 0.56f, 0.96f, 1f) },
            { "_EnablePixelOutline", new Color(0.86f, 0.36f, 0.88f, 1f) },
            { "_EnableShine", new Color(1.00f, 0.78f, 0.20f, 1f) },
            { "_EnableSparkle", new Color(1.00f, 0.92f, 0.42f, 1f) },
            { "_EnableFlow", new Color(0.20f, 0.82f, 0.72f, 1f) },
            { "_EnableFlowMap", new Color(0.10f, 0.68f, 0.88f, 1f) },
            { "_EnableVertexAnimation", new Color(0.46f, 0.78f, 0.30f, 1f) },
            { "_EnableSoftParticles", new Color(0.42f, 0.66f, 0.88f, 1f) },
            { "_EnableSequence", new Color(0.30f, 0.72f, 0.96f, 1f) },
            { "_EnablePolarUV", new Color(0.24f, 0.78f, 0.86f, 1f) },
            { "_EnableVertexStreams", new Color(0.44f, 0.82f, 0.42f, 1f) },
            { "_EnableRadialMask", new Color(0.82f, 0.48f, 0.88f, 1f) },
            { "_EnableFresnelMask", new Color(0.28f, 0.70f, 1.00f, 1f) },
            { "_EnableDepthIntersection", new Color(0.18f, 0.84f, 0.92f, 1f) },
            { "_EnableChromatic", new Color(0.78f, 0.42f, 1.00f, 1f) },
            { "_EnableBlur", new Color(0.48f, 0.70f, 0.94f, 1f) },
            { "_EnablePingPongGlow", new Color(1.00f, 0.48f, 0.22f, 1f) },
            { "_EnableHologram", new Color(0.18f, 0.82f, 0.96f, 1f) },
            { "_EnableGlitch", new Color(0.96f, 0.28f, 0.66f, 1f) },
            { "_EnableRim", new Color(0.28f, 0.64f, 1.00f, 1f) },
            { "_EnableFrozen", new Color(0.42f, 0.82f, 1.00f, 1f) },
            { "_EnableBurn", new Color(1.00f, 0.30f, 0.12f, 1f) },
            { "_EnablePoison", new Color(0.38f, 0.82f, 0.28f, 1f) },
            { "_EnableRainbow", new Color(0.74f, 0.42f, 1.00f, 1f) },
            { "_AlphaClip", new Color(0.94f, 0.34f, 0.28f, 1f) },
            { "_UseUIAlphaClip", new Color(0.94f, 0.34f, 0.28f, 1f) },
            { "_ReceiveShadows", new Color(0.36f, 0.54f, 0.76f, 1f) }
        };
        private static readonly Color[] EffectAccentPalette =
        {
            new Color(0.30f, 0.66f, 0.96f, 1f),
            new Color(0.66f, 0.46f, 0.94f, 1f),
            new Color(0.92f, 0.38f, 0.62f, 1f),
            new Color(0.96f, 0.58f, 0.22f, 1f),
            new Color(0.44f, 0.78f, 0.32f, 1f),
            new Color(0.16f, 0.76f, 0.72f, 1f)
        };
        private static readonly string[] Legacy2DKeywords =
        {
            "_ENABLEBURN_ON", "_ENABLEDISTORTION_ON", "_ENABLEGLITCH_ON", "_ENABLEHOLOGRAM_ON", "_ENABLESHINE_ON"
        };
        private static readonly string[] LegacyLitKeywords = { "_NORMALMAP", "_RECEIVESHADOWS_ON" };

        private sealed class EffectRoute
        {
            internal readonly string Key;
            internal readonly string Title;
            internal readonly string Category;
            internal readonly string[] Aliases;

            internal EffectRoute(string key, string title, string category, params string[] aliases)
            {
                Key = key;
                Title = title;
                Category = category;
                Aliases = aliases ?? Array.Empty<string>();
            }
        }

        private sealed class RouteCacheEntry
        {
            internal readonly int PropertySignature;
            internal readonly EffectRoute[] Routes;
            internal readonly string[] Titles;

            internal RouteCacheEntry(int propertySignature, EffectRoute[] routes, string[] titles)
            {
                PropertySignature = propertySignature;
                Routes = routes;
                Titles = titles;
            }
        }

        private static readonly EffectRoute[] EffectRoutes =
        {
            new EffectRoute("base", "基础材质", "主材质", "基础", "主纹理", "颜色", "Base", "Main"),
            new EffectRoute("animation", "动画/坐标", "坐标与动画", "动画", "序列帧", "坐标", "Animation", "Sequence"),
            new EffectRoute("time", "时间/倍率", "时间与坐标", "时间", "倍率", "非缩放", "自定义", "Time", "Scale"),
            new EffectRoute("uv", "主纹理缩放", "时间与坐标", "主纹理", "UV", "缩放", "偏移", "Tiling", "Offset"),
            new EffectRoute("sequence", "序列帧", "序列帧", "序列帧", "翻页", "Flipbook", "Sequence"),
            new EffectRoute("polar-uv", "极坐标 UV", "坐标变换", "极坐标", "径向坐标", "Polar"),
            new EffectRoute("vertex-streams", "粒子顶点流", "粒子顶点流", "Custom1", "Custom2", "顶点流", "Vertex Stream"),
            new EffectRoute("noise", "噪声/扰动", "噪声与扰动", "噪声", "扰动", "Noise", "Distortion"),
            new EffectRoute("dissolve", "溶解/渐隐", "渐隐与溶解", "溶解", "渐隐", "Dissolve", "Fade"),
            new EffectRoute("outline", "描边", "描边", "描边", "轮廓", "Outline"),
            new EffectRoute("shine", "扫光", "动态效果", "扫光", "高光带", "Shine"),
            new EffectRoute("sparkle", "亮晶晶", "动态效果", "亮晶晶", "闪点", "Sparkle"),
            new EffectRoute("flow", "纹理流动", "动态效果", "流动", "Flow"),
            new EffectRoute("flow-map", "流向贴图", "动态效果", "流向贴图", "Flow Map", "FlowMap"),
            new EffectRoute("vertex-animation", "顶点动画", "动态效果", "顶点动画", "形变", "Vertex Animation"),
            new EffectRoute("soft-particles", "深度融合", "深度交互", "软粒子", "深度融合", "Soft Particles"),
            new EffectRoute("depth-intersection", "深度交界发光", "深度交互", "深度交界", "接触光", "Intersection"),
            new EffectRoute("radial-mask", "径向遮罩", "遮罩", "径向遮罩", "圆形遮罩", "Radial Mask"),
            new EffectRoute("fresnel-mask", "菲涅尔遮罩", "遮罩", "菲涅尔遮罩", "视角遮罩", "Fresnel Mask"),
            new EffectRoute("chromatic", "色差", "动态效果", "色差", "色散", "Chromatic"),
            new EffectRoute("blur", "纹理模糊", "动态效果", "模糊", "柔化", "Blur"),
            new EffectRoute("rim", "边缘光", "表现效果", "边缘光", "轮廓光", "Rim"),
            new EffectRoute("hologram", "全息", "动态效果", "全息", "扫描线", "Hologram"),
            new EffectRoute("glitch", "故障", "动态效果", "故障", "抖动", "Glitch"),
            new EffectRoute("emission", "自发光", "自发光", "自发光", "发光", "Emission"),
            new EffectRoute("render-state", "混合/深度/剔除", "渲染状态", "混合", "深度", "剔除", "队列", "Blend", "ZWrite", "Cull"),
            new EffectRoute("color", "颜色处理", "颜色处理", "颜色", "染色", "亮度", "对比度", "饱和度", "色相", "Color", "Tint"),
            new EffectRoute("state", "冰冻/燃烧/中毒", "状态效果", "冰冻", "燃烧", "中毒", "Frozen", "Burn", "Poison"),
            new EffectRoute("output", "裁剪/阴影", "输出与质量", "裁剪", "阴影", "质量", "Alpha", "Shadow", "Quality")
        };
        private static readonly Dictionary<string, RouteCacheEntry> RouteCache = new Dictionary<string, RouteCacheEntry>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Dictionary<string, string>> CategorySessionKeys = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> FeaturePurposeTitles = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> EffectDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "_UseNormalMap", "使用法线纹理改变光照法线；关闭时跳过法线采样。" },
            { "_UseOcclusionMap", "使用环境遮挡纹理压低间接光；关闭时遮挡值固定为 1。" },
            { "_UseEmission", "叠加自发光颜色和纹理；适合能量、霓虹和受击反馈。" },
            { "_EnableDistortion", "用噪声驱动 UV 扰动；启用后会读取噪声纹理。" },
            { "_EnableInnerOutline", "在原图形内部生成轮廓线；适合角色描边和选中反馈。" },
            { "_EnableOuterOutline", "在精灵透明留白内生成外轮廓；源纹理需要预留足够边距。" },
            { "_EnablePixelOutline", "使用像素宽度生成硬边轮廓；适合像素风和强调边缘。" },
            { "_EnableShine", "沿表面移动高光带；速度、宽度和颜色可独立调整。" },
            { "_EnableSparkle", "按坐标生成程序化闪点；密度、速度、锐度和强度可独立控制。" },
            { "_EnableFlow", "按时间推进主纹理 UV；关闭时不会增加流动偏移计算。" },
            { "_EnableFlowMap", "用流向贴图的 RG 通道扭曲主纹理 UV；关闭时不读取流向贴图。" },
            { "_EnableVertexAnimation", "在局部空间执行正弦顶点位移；Lit 的主画面、阴影和深度使用同一形变。" },
            { "_EnableSoftParticles", "按相机深度柔化 VFX 与场景交界；要求 URP 开启 Depth Texture。" },
            { "_EnableSequence", "按行列网格播放主纹理序列帧；支持手动、时间和 Custom1.z 帧号。" },
            { "_EnablePolarUV", "把 UV 转换为角度/半径坐标；适合旋涡、冲击波和环形流动。" },
            { "_EnableVertexStreams", "读取 ParticleSystem Custom1/Custom2 顶点流，为单粒子提供 UV、帧号、溶解和自发光增量。" },
            { "_EnableRadialMask", "使用独立中心、半径和柔边裁切透明度；基础档即可执行。" },
            { "_EnableFresnelMask", "按视角边缘同时控制透明度和加色。" },
            { "_EnableDepthIntersection", "复用软粒子的同一次场景深度读取，在特效与几何交界处叠加发光。" },
            { "_EnableChromatic", "分离主纹理 RGB 通道形成轻量色差；启用后额外读取红、蓝通道样本。" },
            { "_EnableBlur", "对材质自身纹理执行轻量五点模糊；不读取屏幕背景，适合图标、VFX 卡片和软化贴图。" },
            { "_EnablePingPongGlow", "在两个颜色之间往返发光；适合循环提示和呼吸效果。" },
            { "_EnableHologram", "叠加扫描线和最低透明度控制，形成全息显示效果。" },
            { "_EnableGlitch", "按时间和坐标产生故障抖动。" },
            { "_EnableRim", "按视线与表面法线夹角增加轮廓光。" },
            { "_EnableFrozen", "叠加冰冻颜色与冰晶高光；需要噪声纹理参与。" },
            { "_EnableBurn", "按噪声推进燃烧边缘；通常与溶解或裁剪一起使用。" },
            { "_EnablePoison", "叠加周期性中毒染色；适合状态提示而非基础材质。" },
            { "_EnableAddColor", "在原始颜色上叠加一层可控颜色。" },
            { "_EnableStrongTint", "用指定颜色覆盖主要视觉色调。" },
            { "_EnableAlphaTint", "在保持主体颜色的同时调整透明度色调。" },
            { "_EnableColorReplace", "按颜色距离把指定颜色替换为目标颜色。" },
            { "_EnableBrightness", "调整输出亮度倍率。" },
            { "_EnableContrast", "调整颜色相对中性灰的对比度。" },
            { "_EnableSaturation", "调整颜色鲜艳程度。" },
            { "_EnableHue", "旋转颜色色相。" },
            { "_EnableNegative", "将颜色向负片效果偏移。" },
            { "_EnableRainbow", "按坐标和时间叠加彩虹渐变。" },
            { "_EnableAlphaClip", "按裁剪阈值丢弃低透明度像素。" },
            { "_AlphaClip", "按 Cutoff 阈值丢弃低透明度像素。" },
            { "_ReceiveShadows", "控制 Lit 材质是否接收主光源阴影；修改会同步 Shader Keyword。" }
        };
        private static readonly GUIContent SearchLabel = new GUIContent("查找效果");
        #endregion

        #region Property Classification

        private static bool IsVisible(MaterialProperty property, MaterialProperty[] all, string shaderName)
        {
            string controller = ResolveController(property.name, shaderName);
            if (!string.IsNullOrEmpty(controller))
            {
                MaterialProperty toggle = Find(all, controller);
                if (toggle != null && !toggle.hasMixedValue && toggle.floatValue < 0.5f) return false;
            }
            if ((property.name.IndexOf("Dissolve", StringComparison.Ordinal) >= 0 && property.name != "_DissolveMode")
                || property.name == "_FadeProgress"
                || property.name == "_FadePosition"
                || property.name == "_FadeWidth"
                || property.name == "_FadeMask")
            {
                MaterialProperty mode = Find(all, "_DissolveMode") ?? Find(all, "_FadeMode");
                if (mode != null && !mode.hasMixedValue && mode.floatValue < 0.5f) return false;
            }
            if (property.name == "_SequenceColumns" || property.name == "_SequenceRows" || property.name == "_SequenceFrame" || property.name == "_SequenceSpeed")
            {
                MaterialProperty mode = Find(all, "_AnimationMode");
                if (mode != null && !mode.hasMixedValue && mode.floatValue < 0.5f) return false;
            }
            if (property.name == "_CustomTime")
            {
                MaterialProperty mode = Find(all, "_TimeMode");
                if (mode != null && !mode.hasMixedValue && Mathf.RoundToInt(mode.floatValue) != 2) return false;
            }
            return true;
        }

        private static string ResolveController(string name, string shaderName)
        {
            if (name.StartsWith("_Enable", StringComparison.Ordinal)) return null;
            if (shaderName == "ES/3D/VFX Composite URP" && name.StartsWith("_Sequence", StringComparison.Ordinal)) return "_EnableSequence";
            if (name.StartsWith("_Polar", StringComparison.Ordinal)) return "_EnablePolarUV";
            if (name.StartsWith("_VertexStream", StringComparison.Ordinal)) return "_EnableVertexStreams";
            if (name.StartsWith("_RadialMask", StringComparison.Ordinal)) return "_EnableRadialMask";
            if (name.StartsWith("_Fresnel", StringComparison.Ordinal)) return "_EnableFresnelMask";
            if (name.StartsWith("_DepthIntersection", StringComparison.Ordinal)) return "_EnableDepthIntersection";
            if (name.StartsWith("_AddColor", StringComparison.Ordinal)) return "_EnableAddColor";
            if (name.StartsWith("_StrongTint", StringComparison.Ordinal)) return "_EnableStrongTint";
            if (name.StartsWith("_AlphaTint", StringComparison.Ordinal)) return "_EnableAlphaTint";
            if (name.StartsWith("_Replace", StringComparison.Ordinal)) return "_EnableColorReplace";
            if (name == "_Brightness") return "_EnableBrightness";
            if (name == "_Contrast") return "_EnableContrast";
            if (name == "_Saturation") return "_EnableSaturation";
            if (name == "_Hue") return "_EnableHue";
            if (name == "_NegativeFade") return "_EnableNegative";
            if (name.StartsWith("_Rainbow", StringComparison.Ordinal)) return "_EnableRainbow";
            if (name.StartsWith("_InnerOutline", StringComparison.Ordinal)) return "_EnableInnerOutline";
            if (name.StartsWith("_OuterOutline", StringComparison.Ordinal)) return "_EnableOuterOutline";
            if (name.StartsWith("_PixelOutline", StringComparison.Ordinal)) return "_EnablePixelOutline";
            if (name.StartsWith("_Glow", StringComparison.Ordinal)) return "_EnablePingPongGlow";
            if (name.StartsWith("_Sparkle", StringComparison.Ordinal)) return "_EnableSparkle";
            if (name.StartsWith("_FlowMap", StringComparison.Ordinal)) return "_EnableFlowMap";
            if (name.StartsWith("_Flow", StringComparison.Ordinal)) return "_EnableFlow";
            if (name.StartsWith("_VertexAnimation", StringComparison.Ordinal)) return "_EnableVertexAnimation";
            if (name.StartsWith("_SoftParticle", StringComparison.Ordinal)) return "_EnableSoftParticles";
            if (name.StartsWith("_Chromatic", StringComparison.Ordinal)) return "_EnableChromatic";
            if (name.StartsWith("_Blur", StringComparison.Ordinal)) return "_EnableBlur";
            if (name == "_NormalMap" || name == "_NormalScale") return "_UseNormalMap";
            if (name == "_EmissionMap" || name == "_EmissionColor") return "_UseEmission";
            if (name == "_NoiseTex" || name == "_NoiseScale" || name == "_NoiseSpeed" || name == "_DistortionStrength") return "_EnableDistortion";
            if (name == "_Cutoff") return "_AlphaClip";
            if (name == "_OcclusionMap" || name == "_Occlusion") return "_UseOcclusionMap";
            if (name.StartsWith("_Sequence", StringComparison.Ordinal)) return "_AnimationMode";
            if (shaderName == "ES/2D/Composite URP"
                && ((name.StartsWith("_Fade", StringComparison.Ordinal) && name != "_FadeMode") || name == "_DissolveEdgeColor")) return "_FadeMode";
            if (shaderName != "ES/2D/Composite URP"
                && name.StartsWith("_Dissolve", StringComparison.Ordinal) && name != "_DissolveMode") return "_DissolveMode";
            if (name.StartsWith("_Frozen", StringComparison.Ordinal)) return "_EnableFrozen";
            if (name.StartsWith("_Burn", StringComparison.Ordinal)) return "_EnableBurn";
            if (name.StartsWith("_Poison", StringComparison.Ordinal)) return "_EnablePoison";
            if (name.StartsWith("_Hologram", StringComparison.Ordinal)) return "_EnableHologram";
            if (name.StartsWith("_Glitch", StringComparison.Ordinal)) return "_EnableGlitch";
            if (name.StartsWith("_Shine", StringComparison.Ordinal)) return "_EnableShine";
            if (name.StartsWith("_Rim", StringComparison.Ordinal)) return "_EnableRim";
            return null;
        }

        private static bool IsAlwaysHidden(MaterialProperty property)
        {
            if (property == null) return true;
            string name = property.name;
            return (property.flags & MaterialProperty.PropFlags.HideInInspector) != 0
                || name == "_texcoord"
                || name == "_AlphaTex"
                || name.StartsWith("unity_", StringComparison.Ordinal);
        }
        private static bool IsToggle(string name)
        {
            return name.StartsWith("_Enable", StringComparison.Ordinal)
                || name.StartsWith("_Use", StringComparison.Ordinal)
                || name == "_AlphaClip"
                || name == "_ReceiveShadows"
                || name.EndsWith("Toggle", StringComparison.Ordinal);
        }

        private static bool IsStatusFeatureToggle(string name)
        {
            return name.StartsWith("_Enable", StringComparison.Ordinal)
                || name.StartsWith("_Use", StringComparison.Ordinal)
                || name == "_AlphaClip";
        }
        private static string[] ResolveCategoryOrder(string shaderName)
        {
            if (shaderName == "ES/2D/Composite URP") return TwoDCategoryOrder;
            if (shaderName == "ES/3D/VFX Composite URP") return VfxCategoryOrder;
            if (shaderName == "ES/UI/Composite URP") return UiCategoryOrder;
            return LitCategoryOrder;
        }

        private static string ResolveCategory(string shaderName, string name)
        {
            if (shaderName == "ES/2D/Composite URP")
            {
                if (name == "_MainTex" || name == "_Color" || name == "_VertexColorStrength") return "基础输入";
                if (name == "_CoordinateMode"
                    || name == "_TimeMode"
                    || name == "_CustomTime"
                    || name == "_TimeScale"
                    || name == "_MainTexScaleOffset"
                    || name == "_AnimationMode"
                    || name.StartsWith("_Sequence", StringComparison.Ordinal)) return "时间与坐标";
                if (name.StartsWith("_Fade", StringComparison.Ordinal) || name.StartsWith("_Dissolve", StringComparison.Ordinal)) return "遮罩与溶解";
                if (name.IndexOf("Outline", StringComparison.Ordinal) >= 0) return "轮廓";
                if (name.StartsWith("_EnableFrozen", StringComparison.Ordinal) || name.StartsWith("_Frozen", StringComparison.Ordinal)
                    || name.StartsWith("_EnableBurn", StringComparison.Ordinal) || name.StartsWith("_Burn", StringComparison.Ordinal)
                    || name.StartsWith("_EnablePoison", StringComparison.Ordinal) || name.StartsWith("_Poison", StringComparison.Ordinal)) return "状态表现";
                if (name == "_AlphaClip" || name == "_Cutoff") return "输出控制";
                if (name.StartsWith("_EnableShine", StringComparison.Ordinal) || name.StartsWith("_Shine", StringComparison.Ordinal)
                    || name.StartsWith("_EnablePingPongGlow", StringComparison.Ordinal) || name.StartsWith("_Glow", StringComparison.Ordinal)
                    || name.StartsWith("_EnableSparkle", StringComparison.Ordinal) || name.StartsWith("_Sparkle", StringComparison.Ordinal)
                    || name.StartsWith("_EnableFlow", StringComparison.Ordinal) || name.StartsWith("_Flow", StringComparison.Ordinal)
                    || name.StartsWith("_EnableChromatic", StringComparison.Ordinal) || name.StartsWith("_Chromatic", StringComparison.Ordinal)
                    || name.StartsWith("_EnableBlur", StringComparison.Ordinal) || name.StartsWith("_Blur", StringComparison.Ordinal)
                    || name.StartsWith("_EnableDistortion", StringComparison.Ordinal) || name.StartsWith("_Noise", StringComparison.Ordinal)
                    || name.StartsWith("_Distortion", StringComparison.Ordinal) || name.StartsWith("_EnableHologram", StringComparison.Ordinal)
                    || name.StartsWith("_Hologram", StringComparison.Ordinal) || name.StartsWith("_EnableGlitch", StringComparison.Ordinal)
                    || name.StartsWith("_Glitch", StringComparison.Ordinal)) return "动态表现";
                return "色彩调整";
            }

            if (shaderName == "ES/3D/VFX Composite URP")
            {
                if (name == "_MainTex" || name == "_Color" || name == "_VertexColorStrength") return "基础输入";
                if (name == "_TimeMode" || name == "_CustomTime" || name == "_TimeScale" || name == "_MainTexScaleOffset") return "时间与坐标";
                if (name == "_EnableSequence" || name.StartsWith("_Sequence", StringComparison.Ordinal)
                    || name == "_EnableVertexStreams" || name.StartsWith("_VertexStream", StringComparison.Ordinal)) return "粒子输入";
                if (name == "_EnablePolarUV" || name.StartsWith("_Polar", StringComparison.Ordinal)
                    || name.StartsWith("_Noise", StringComparison.Ordinal) || name == "_Distortion"
                    || name.StartsWith("_EnableFlow", StringComparison.Ordinal) || name.StartsWith("_Flow", StringComparison.Ordinal)
                    || name.StartsWith("_EnableFlowMap", StringComparison.Ordinal) || name.StartsWith("_FlowMap", StringComparison.Ordinal)
                    || name.StartsWith("_EnableVertexAnimation", StringComparison.Ordinal) || name.StartsWith("_VertexAnimation", StringComparison.Ordinal)) return "形变与流动";
                if (name.StartsWith("_EnableShine", StringComparison.Ordinal) || name.StartsWith("_Shine", StringComparison.Ordinal)
                    || name.StartsWith("_EnableSparkle", StringComparison.Ordinal) || name.StartsWith("_Sparkle", StringComparison.Ordinal)
                    || name.StartsWith("_EnableChromatic", StringComparison.Ordinal) || name.StartsWith("_Chromatic", StringComparison.Ordinal)
                    || name.StartsWith("_EnableBlur", StringComparison.Ordinal) || name.StartsWith("_Blur", StringComparison.Ordinal)
                    || name.StartsWith("_Hologram", StringComparison.Ordinal) || name == "_EnableHologram"
                    || name.StartsWith("_Rim", StringComparison.Ordinal) || name == "_EnableRim"
                    || name.StartsWith("_Glitch", StringComparison.Ordinal) || name == "_EnableGlitch"
                    || name == "_EmissionColor") return "动态表现";
                if (name == "_EnableRadialMask" || name.StartsWith("_RadialMask", StringComparison.Ordinal)
                    || name == "_EnableFresnelMask" || name.StartsWith("_Fresnel", StringComparison.Ordinal)
                    || name.StartsWith("_Dissolve", StringComparison.Ordinal)) return "遮罩与溶解";
                if (name == "_EnableSoftParticles" || name.StartsWith("_SoftParticle", StringComparison.Ordinal)
                    || name == "_EnableDepthIntersection" || name.StartsWith("_DepthIntersection", StringComparison.Ordinal)) return "深度交互";
                if (name == "_AlphaClip" || name == "_Cutoff" || name == "_QualityTier") return "输出与质量";
                if (name == "_BlendMode" || name == "_ZWriteMode" || name == "_ZTest" || name == "_Cull" || name == "_QueueOffset") return "渲染状态";
                return "基础输入";
            }

            if (shaderName == "ES/UI/Composite URP")
            {
                if (name == "_MainTex" || name == "_Color" || name == "_VertexColorStrength") return "基础输入";
                if (name == "_TimeMode" || name == "_CustomTime" || name == "_TimeScale" || name == "_MainTexScaleOffset") return "时间与坐标";
                if (name == "_AlphaClip"
                    || name == "_Cutoff"
                    || name.StartsWith("_Stencil", StringComparison.Ordinal)
                    || name == "_ColorMask"
                    || name == "_UseUIAlphaClip") return "遮罩与输出";
                return "动态表现";
            }

            if (name == "_BaseMap"
                || name == "_BaseColor"
                || name == "_UseNormalMap"
                || name == "_NormalMap"
                || name == "_NormalScale"
                || name == "_Metallic"
                || name == "_Smoothness") return "基础材质";
            if (name == "_TimeMode"
                || name == "_CustomTime"
                || name == "_TimeScale"
                || name == "_MainTexScaleOffset"
                || name.StartsWith("_VertexAnimation", StringComparison.Ordinal)
                || name == "_EnableVertexAnimation") return "时间与形变";
            if (name == "_Occlusion"
                || name == "_UseOcclusionMap"
                || name == "_OcclusionMap"
                || name == "_UseEmission"
                || name == "_EmissionColor"
                || name == "_EmissionMap"
                || name == "_ReceiveShadows") return "光照输入";
            if (name.StartsWith("_Dissolve", StringComparison.Ordinal) || name.StartsWith("_Noise", StringComparison.Ordinal)) return "遮罩与溶解";
            if (name == "_AlphaClip" || name == "_Cutoff" || name == "_QualityTier") return "输出与质量";
            return "动态表现";
        }
        private static string PropertyHint(string name, string shaderName)
        {
            if (name == "_MainTex" && shaderName == "ES/UI/Composite URP") return "由 RawImage.texture 或 Image.sprite 提供；CanvasRenderer 会覆盖材质中的主纹理。";
            if (name == "_QualityTier") return "基础/标准/高质量会同步控制 ES 关键词。";
            if (name == "_EnableVertexAnimation") return shaderName == "ES/3D/Lit Composite URP" ? "Lit 会同步主画面、阴影和深度顶点位置。" : null;
            if (name == "_EnableFlowMap") return "RG 通道以 0.5 为静止方向。";
            if (name == "_EnableSoftParticles") return "URP Asset 或 Camera 必须提供 Depth Texture。";
            if (name == "_EnableDepthIntersection") return "与软粒子共用场景深度采样，URP 必须提供 Depth Texture。";
            if (name == "_EnableChromatic") return "会增加两次主纹理采样。";
            if (name == "_EnableVertexStreams") return "ParticleSystem Renderer 顶点流需提供 Custom1.xyzw 和 Custom2.x；各通道仅作为增量。";
            if (name == "_BlendMode" || name == "_ZWriteMode" || name == "_ZTest" || name == "_Cull" || name == "_QueueOffset") return "材质级渲染状态；MaterialPropertyBlock 无法覆盖。";
            if (name == "_TimeMode") return "场景时间受 Time.timeScale 影响；非缩放时间由 ES 运行时驱动；自定义时间由调用方写入。";
            if (name == "_CustomTime") return "选择自定义时间后生效；建议通过 MaterialPropertyBlock 或 ESCompositeURPProperties.SetTime 写入。";
            if (name == "_TimeScale") return "统一乘在当前时间源上；各效果自身的速度参数仍独立生效。";
            if (name == "_MainTexScaleOffset") return "X/Y 为缩放，Z/W 为偏移；支持 MaterialPropertyBlock 对单个对象覆盖。";
            if (name == "_ReceiveShadows") return "关闭后同步 _RECEIVE_SHADOWS_OFF，材质不再应用主光实时阴影。";
            if (name == "_UseUIAlphaClip") return "材质关键词；必须写入 UI 独立材质，MaterialPropertyBlock 无法切换。";
            if (name == "_UseNormalMap") return "关闭时跳过法线纹理采样；开启后才显示纹理和强度。";
            if (name == "_UseEmission") return "关闭时跳过自发光纹理采样；开启后才显示颜色和纹理。";
            if (name == "_NormalMap") return "纹理导入类型应为 Normal map。";
            if (name == "_NoiseTex") return "建议使用 Repeat 包裹和线性过滤。";
            if (name == "_AlphaClip") return "透明度低于阈值的像素会被丢弃。";
            int minimumQuality = GetMinimumQualityTier(shaderName, name);
            if (minimumQuality > 0) return "需要“" + QualityName(minimumQuality) + "”质量档。";
            return null;
        }

        private static int GetMinimumQualityTier(string shaderName, string propertyName)
        {
            if (shaderName == "ES/3D/Lit Composite URP")
            {
                if (propertyName == "_EnableShine" || propertyName == "_EnableSparkle" || propertyName == "_EnableBurn") return 2;
                if (propertyName == "_EnableVertexAnimation" || propertyName == "_EnableFlowMap"
                    || propertyName == "_DissolveMode" || propertyName == "_EnableRim") return 1;
                return 0;
            }

            if (shaderName == "ES/3D/VFX Composite URP")
            {
                if (propertyName == "_EnableHologram" || propertyName == "_EnableGlitch"
                    || propertyName == "_EnableBlur" || propertyName == "_EnableSparkle") return 2;
                if (propertyName == "_EnableVertexAnimation" || propertyName == "_EnablePolarUV"
                    || propertyName == "_EnableFlowMap" || propertyName == "_EnableChromatic"
                    || propertyName == "_DissolveMode" || propertyName == "_EnableRim"
                    || propertyName == "_EnableFresnelMask" || propertyName == "_EnableShine"
                    || propertyName == "_EnableSoftParticles" || propertyName == "_EnableDepthIntersection") return 1;
            }
            return 0;
        }

        private static MaterialProperty Find(MaterialProperty[] properties, string name)
        {
            for (int i = 0; i < properties.Length; i++) if (properties[i].name == name) return properties[i];
            return null;
        }

        #endregion
    }
}
