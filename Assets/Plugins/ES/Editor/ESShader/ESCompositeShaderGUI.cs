using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// ES Composite 材质 Inspector。
    /// 设计基线参考 SSU：按 Shader 属性声明顺序处理，使用状态机驱动分类、开关和隐藏，
    /// 同时保留 ES 的中文帮助、PropertyBlock 示例和 ESEditorPresentation 视觉体系。
    /// </summary>
    public sealed partial class ESCompositeShaderGUI : ShaderGUI
    {
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

        private static readonly Dictionary<Shader, Material> Defaults = new Dictionary<Shader, Material>();
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

        static ESCompositeShaderGUI()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseDefaults;
            EditorApplication.quitting += ReleaseDefaults;
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor == null || properties == null) return;
            DrawStatus(materialEditor, properties);
            Material material = materialEditor.target as Material;
            string shaderName = material != null && material.shader != null ? material.shader.name : string.Empty;
            InspectorViewLevel viewLevel = DrawInspectorViewMode(shaderName);
            DrawPresetPanel(materialEditor, properties, shaderName);
            DrawEnvironmentDiagnostics(materialEditor, properties, shaderName);
            string effectFilter = DrawEffectNavigator(shaderName, properties);
            int propertySignatureBeforeDraw = GetMaterialPropertyValueSignature(properties);
            DrawPropertyStream(materialEditor, properties, shaderName, effectFilter, viewLevel);
            if (propertySignatureBeforeDraw != GetMaterialPropertyValueSignature(properties))
                SyncKeywords(materialEditor);
        }

        public override void ValidateMaterial(Material material)
        {
            if (material != null)
                SyncMaterialKeywords(material);
        }

        private static void DrawStatus(MaterialEditor editor, MaterialProperty[] properties)
        {
            int enabled = 0, effectCount = 0, mixedCount = 0;
            Material target = editor.target as Material;
            string shaderName = target != null && target.shader != null ? target.shader.name : "未知 Shader";
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty p = properties[i];
                if (IsAlwaysHidden(p)) continue;
                if (IsStatusFeatureToggle(p.name))
                {
                    effectCount++;
                    if (p.hasMixedValue)
                    {
                        mixedCount++;
                        continue;
                    }
                    if (p.floatValue > 0.5f)
                        enabled++;
                }
            }
            MaterialProperty quality = Find(properties, "_QualityTier");
            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            GUILayout.Label(GetShaderDisplayName(shaderName), ESEditorPresentation.HeaderStyle);
            string mixedText = mixedCount > 0 ? "  ·  混合 " + mixedCount : string.Empty;
            string summary = "启用 " + enabled + "/" + effectCount + mixedText;
            if (quality != null)
                summary += "  ·  质量 " + (quality.hasMixedValue ? "混合" : QualityName(quality.floatValue));
            GUILayout.Label(summary, ESEditorPresentation.SubtitleStyle);

            if (quality != null && !quality.hasMixedValue && shaderName != "ES/3D/VFX Composite URP")
            {
                int requiredQuality = GetRequiredQuality(properties, shaderName);
                int currentQuality = Mathf.Clamp(Mathf.RoundToInt(quality.floatValue), 0, 2);
                if (currentQuality < requiredQuality)
                    EditorGUILayout.HelpBox("已启用效果至少需要“" + QualityName(requiredQuality) + "”质量，当前不会完整生效。", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static string QualityName(float value)
        {
            switch (Mathf.Clamp(Mathf.RoundToInt(value), 0, 2))
            {
                case 0: return "基础";
                case 2: return "高质量";
                default: return "标准";
            }
        }

        private static string GetShaderDisplayName(string shaderName)
        {
            switch (shaderName)
            {
                case "ES/2D/Composite URP": return "ES 2D 综合材质";
                case "ES/3D/Lit Composite URP": return "ES 3D 光照材质";
                case "ES/3D/VFX Composite URP": return "ES 3D 特效材质";
                case "ES/UI/Composite URP": return "ES UI 综合材质";
                default: return "ES Composite 材质";
            }
        }

        private static string DrawEffectNavigator(string shaderName, MaterialProperty[] properties)
        {
            string searchKey = "ES.Composite.Navigator.Search." + shaderName;
            string routeKey = "ES.Composite.Navigator.Route." + shaderName;
            string search = SessionState.GetString(searchKey, string.Empty);
            string selectedRoute = SessionState.GetString(routeKey, string.Empty);

            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("效果导航", ESEditorPresentation.HeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("显示全部", EditorStyles.miniButton, GUILayout.Width(64f)))
            {
                search = string.Empty;
                selectedRoute = string.Empty;
                SessionState.SetString(searchKey, search);
                SessionState.SetString(routeKey, selectedRoute);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            string nextSearch = EditorGUILayout.TextField(SearchLabel, search, EditorStyles.toolbarSearchField);
            if (!string.Equals(nextSearch, search, StringComparison.Ordinal))
            {
                search = nextSearch;
                selectedRoute = string.Empty;
                SessionState.SetString(searchKey, search);
                SessionState.SetString(routeKey, selectedRoute);
            }
            EditorGUILayout.EndHorizontal();

            EffectRoute[] routes = RoutesForShader(shaderName, properties);
            if (routes.Length > 0)
            {
                string[] routeTitles = GetRouteTitles(shaderName, routes);
                int selectedIndex = -1;
                for (int i = 0; i < routes.Length; i++)
                {
                    if (string.Equals(selectedRoute, routes[i].Key, StringComparison.Ordinal)) selectedIndex = i;
                }
                float inspectorWidth = EditorGUIUtility.currentViewWidth;
                int columns = inspectorWidth < 330f ? 2 : inspectorWidth < 520f ? 3 : 4;
                int nextIndex = GUILayout.SelectionGrid(selectedIndex, routeTitles, columns, EditorStyles.toolbarButton);
                if (nextIndex >= 0 && nextIndex < routes.Length && nextIndex != selectedIndex)
                {
                    selectedRoute = routes[nextIndex].Key;
                    search = string.Empty;
                    SessionState.SetString(searchKey, search);
                    SessionState.SetString(routeKey, selectedRoute);
                }
            }

            EffectRoute selected = FindRoute(selectedRoute);
            if (selected != null)
            {
                GUILayout.Label("正在查看：" + selected.Title, ESEditorPresentation.SubtitleStyle);
            }
            else if (!string.IsNullOrWhiteSpace(search))
            {
                GUILayout.Label("正在匹配：" + search.Trim(), ESEditorPresentation.SubtitleStyle);
                bool found = false;
                for (int i = 0; i < properties.Length; i++)
                {
                    if (PropertyMatchesFilter(properties[i], search.Trim(), shaderName))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) EditorGUILayout.HelpBox("没有找到匹配的效果或属性名。可以试试：溶解、扫光、描边、全息、故障、颜色。", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
            return string.IsNullOrWhiteSpace(search) && selected == null ? string.Empty : (!string.IsNullOrWhiteSpace(search) ? search.Trim() : "@" + selected.Key);
        }

        private static EffectRoute[] RoutesForShader(string shaderName, MaterialProperty[] properties)
        {
            int signature = GetPropertySignature(properties);
            RouteCacheEntry entry;
            if (RouteCache.TryGetValue(shaderName, out entry) && entry.PropertySignature == signature)
                return entry.Routes;

            var result = new List<EffectRoute>();
            for (int i = 0; i < EffectRoutes.Length; i++)
            {
                EffectRoute route = EffectRoutes[i];
                for (int p = 0; p < properties.Length; p++)
                {
                    if (!IsAlwaysHidden(properties[p]) && PropertyMatches(properties[p], route, shaderName))
                    {
                        result.Add(route);
                        break;
                    }
                }
            }
            EffectRoute[] routes = result.ToArray();
            string[] titles = new string[routes.Length];
            for (int i = 0; i < routes.Length; i++) titles[i] = routes[i].Title;
            RouteCache[shaderName] = new RouteCacheEntry(signature, routes, titles);
            return routes;
        }

        private static string[] GetRouteTitles(string shaderName, EffectRoute[] routes)
        {
            RouteCacheEntry entry;
            if (RouteCache.TryGetValue(shaderName, out entry) && ReferenceEquals(entry.Routes, routes))
                return entry.Titles;

            string[] titles = new string[routes.Length];
            for (int i = 0; i < routes.Length; i++) titles[i] = routes[i].Title;
            return titles;
        }

        private static int GetPropertySignature(MaterialProperty[] properties)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < properties.Length; i++)
                {
                    MaterialProperty property = properties[i];
                    hash = hash * 31 + (property == null ? 0 : StringComparer.Ordinal.GetHashCode(property.name));
                    hash = hash * 31 + (property == null ? 0 : (int)property.flags);
                }
                return hash;
            }
        }

        private static EffectRoute FindRoute(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < EffectRoutes.Length; i++)
                if (EffectRoutes[i].Key == key) return EffectRoutes[i];
            return null;
        }

        private static bool PropertyMatches(MaterialProperty property, EffectRoute route, string shaderName)
        {
            if (property == null || route == null) return false;
            if (route.Key == "animation" && shaderName != "ES/2D/Composite URP") return false;
            string routeController = ResolveRouteController(route.Key);
            if (!string.IsNullOrEmpty(routeController))
                return property.name == routeController
                    || string.Equals(ResolveController(property.name, shaderName), routeController, StringComparison.Ordinal);
            if (ResolveCategory(shaderName, property.name) == route.Category) return true;
            for (int i = 0; i < route.Aliases.Length; i++)
                if (property.name.IndexOf(route.Aliases[i], StringComparison.OrdinalIgnoreCase) >= 0
                    || GetDisplayName(property).IndexOf(route.Aliases[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static string ResolveRouteController(string routeKey)
        {
            switch (routeKey)
            {
                case "shine": return "_EnableShine";
                case "sparkle": return "_EnableSparkle";
                case "flow": return "_EnableFlow";
                case "flow-map": return "_EnableFlowMap";
                case "vertex-animation": return "_EnableVertexAnimation";
                case "sequence": return "_EnableSequence";
                case "polar-uv": return "_EnablePolarUV";
                case "vertex-streams": return "_EnableVertexStreams";
                case "soft-particles": return "_EnableSoftParticles";
                case "depth-intersection": return "_EnableDepthIntersection";
                case "radial-mask": return "_EnableRadialMask";
                case "fresnel-mask": return "_EnableFresnelMask";
                case "chromatic": return "_EnableChromatic";
                case "blur": return "_EnableBlur";
                case "rim": return "_EnableRim";
                case "hologram": return "_EnableHologram";
                case "glitch": return "_EnableGlitch";
                case "emission": return "_UseEmission";
                default: return null;
            }
        }

        private static bool PropertyMatchesFilter(MaterialProperty property, string filter, string shaderName)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            if (filter.StartsWith("@", StringComparison.Ordinal))
            {
                EffectRoute route = FindRoute(filter.Substring(1));
                return PropertyMatches(property, route, shaderName);
            }
            return property.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                || GetDisplayName(property).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void DrawPropertyStream(MaterialEditor editor, MaterialProperty[] properties, string shaderName, string filter, InspectorViewLevel viewLevel)
        {
            // 先确定稳定的分类顺序，再在分类内部保持 Shader 声明顺序。
            // 这样同一分类只会出现一次，不会因为属性交错而重复生成折叠页签。
            string[] categoryOrder = ResolveCategoryOrder(shaderName);
            for (int c = 0; c < categoryOrder.Length; c++)
            {
                string category = categoryOrder[c];
                if (!HasVisibleCategory(properties, category, shaderName, filter, viewLevel)) continue;
                if (!BeginCategoryCard(shaderName, category, !string.IsNullOrEmpty(filter))) continue;
                DrawCategoryProperties(editor, properties, category, shaderName, filter, viewLevel);
                EditorGUILayout.EndVertical();
            }
        }

        private static bool HasVisibleCategory(MaterialProperty[] properties, string category, string shaderName, string filter, InspectorViewLevel viewLevel)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty property = properties[i];
                if (ResolveCategory(shaderName, property.name) == category
                    && !IsAlwaysHidden(property)
                    && PropertyPassesFilter(property, properties, filter, shaderName)
                    && PropertyPassesViewLevel(property, filter, viewLevel)) return true;
            }
            return false;
        }

        private static void DrawCategoryProperties(MaterialEditor editor, MaterialProperty[] properties, string category, string shaderName, string filter, InspectorViewLevel viewLevel)
        {
            string activeGroup = null;
            bool groupOpen = false;
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty property = properties[i];
                if (ResolveCategory(shaderName, property.name) != category || IsAlwaysHidden(property)) continue;

                if (!PropertyPassesFilter(property, properties, filter, shaderName)) continue;
                if (!PropertyPassesViewLevel(property, filter, viewLevel)) continue;

                if (IsEffectToggle(property.name))
                {
                    CloseEffectGroup(ref groupOpen, ref activeGroup);
                    bool expanded = DrawEffectCardHeader(editor, property, GetDisplayName(property), shaderName, !string.IsNullOrEmpty(filter));
                    if ((property.hasMixedValue || property.floatValue > 0.5f) && expanded)
                    {
                        activeGroup = property.name;
                        groupOpen = true;
                    }
                    else
                    {
                        EditorGUILayout.EndVertical();
                    }
                    continue;
                }

                if (IsModeFeature(property.name))
                {
                    CloseEffectGroup(ref groupOpen, ref activeGroup);
                    BeginModeFeatureCard(property);
                    DrawProperty(editor, property, GetDisplayName(property));
                    activeGroup = property.name;
                    groupOpen = true;
                    continue;
                }

                if (groupOpen && !string.Equals(ResolveController(property.name, shaderName), activeGroup, StringComparison.Ordinal))
                    CloseEffectGroup(ref groupOpen, ref activeGroup);
                if (string.IsNullOrEmpty(filter) && IsCollapsedEffectDependency(property, shaderName)) continue;
                if (!IsVisible(property, properties, shaderName)) continue;
                DrawProperty(editor, property, GetDisplayName(property));
            }
            CloseEffectGroup(ref groupOpen, ref activeGroup);
        }

        private static bool IsModeFeature(string propertyName)
        {
            return propertyName == "_AnimationMode" || propertyName == "_FadeMode" || propertyName == "_DissolveMode";
        }

        private static void BeginModeFeatureCard(MaterialProperty property)
        {
            bool active = property.hasMixedValue || property.floatValue > 0.5f;
            Color accent = GetEffectAccent(property.name);
            Color previousBackground = GUI.backgroundColor;
            Rect cardRect;
            try
            {
                GUI.backgroundColor = Color.Lerp(Color.white, accent, active ? 0.24f : 0.08f);
                cardRect = EditorGUILayout.BeginVertical("Helpbox");
            }
            finally
            {
                GUI.backgroundColor = previousBackground;
            }
            DrawEffectCardBorder(cardRect, accent, active && !property.hasMixedValue, property.hasMixedValue);
        }

        private static bool DrawEffectCardHeader(MaterialEditor editor, MaterialProperty property, string displayName, string shaderName, bool forceExpanded)
        {
            bool mixed = property.hasMixedValue;
            bool enabled = !mixed && property.floatValue > 0.5f;
            string key = GetEffectSessionKey(shaderName, property.name);
            bool expanded = forceExpanded || mixed || SessionState.GetBool(key, true);
            string title = GetFeaturePurposeTitle(displayName);
            Color accent = GetEffectAccent(property.name);

            Color previousBackground = GUI.backgroundColor;
            try
            {
                Color frameAccent = mixed ? new Color(0.92f, 0.66f, 0.20f, 1f) : accent;
                GUI.backgroundColor = Color.Lerp(Color.white, frameAccent, enabled || mixed ? 0.34f : 0.12f);
                Rect groupRect = EditorGUILayout.BeginVertical("Helpbox");
                DrawEffectCardBorder(groupRect, accent, enabled, mixed);
            }
            finally
            {
                GUI.backgroundColor = previousBackground;
            }

            bool stackedHeader = EditorGUIUtility.currentViewWidth < 260f;
            Rect headerRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(stackedHeader ? 50f : 30f),
                GUILayout.ExpandWidth(true));
            DrawEffectHeaderBackground(headerRect, accent, enabled, mixed);

            const float gap = 3f;
            float right = headerRect.xMax - 5f
                - ESEditorPresentation.GetInspectorRightGutter(headerRect.width);
            float controlY = headerRect.y + (stackedHeader ? 26f : 5f);
            Rect arrowRect = new Rect(right - 22f, controlY, 22f, 20f);
            right = arrowRect.x - gap;
            Rect codeRect = new Rect(right - 28f, controlY, 28f, 20f);
            right = codeRect.x - gap;
            Rect toggleRect = new Rect(right - 18f, controlY + 1f, 18f, 18f);
            right = toggleRect.x - gap;
            bool showStatus = !stackedHeader;
            Rect statusRect = showStatus
                ? new Rect(right - 48f, headerRect.y + 6f, 48f, 18f)
                : Rect.zero;
            if (showStatus)
                right = statusRect.x - gap;
            float titleX = headerRect.x + 11f;
            float titleRight = stackedHeader ? headerRect.xMax - 8f : right;
            Rect titleRect = new Rect(titleX, headerRect.y + 3f, Mathf.Max(0f, titleRight - titleX), 22f);

            bool headerClicked = GUI.Button(titleRect, title, ESEditorPresentation.HeaderStyle);
            if (showStatus)
                DrawEffectStatus(statusRect, accent, enabled, mixed);
            ESCompositeCodingHelper.DrawCompactBooleanProperty(
                editor,
                property,
                displayName,
                toggleRect,
                codeRect);
            bool arrowClicked = false;
            using (new EditorGUI.DisabledScope(!enabled && !mixed))
            {
                string arrow = expanded ? "▼" : "▶";
                arrowClicked = GUI.Button(arrowRect, arrow, EditorStyles.miniButton);
            }
            if ((headerClicked || arrowClicked) && (enabled || mixed))
            {
                expanded = !expanded;
                SessionState.SetBool(key, expanded);
            }

            if ((enabled || mixed) && expanded)
            {
                EditorGUILayout.Space(2f);
                string description = GetEffectDescription(shaderName, property.name);
                if (!string.IsNullOrEmpty(description))
                    EditorGUILayout.LabelField(description, ESEditorPresentation.SubtitleStyle, GUILayout.ExpandWidth(true));
            }
            return expanded;
        }

        private static string GetEffectDescription(string shaderName, string propertyName)
        {
            EffectDescriptions.TryGetValue(propertyName, out string description);
            if (string.IsNullOrEmpty(description))
                description = PropertyHint(propertyName, shaderName);

            int minimumQuality = GetMinimumQualityTier(shaderName, propertyName);
            if (minimumQuality <= 0)
                return description;

            string qualityText = "需要“" + QualityName(minimumQuality) + "”质量档。";
            return string.IsNullOrEmpty(description) ? qualityText : description + " " + qualityText;
        }

        private static Color GetEffectAccent(string propertyName)
        {
            if (EffectAccentOverrides.TryGetValue(propertyName, out Color accent))
                return accent;

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < propertyName.Length; i++)
                    hash = (hash ^ propertyName[i]) * 16777619u;
                return EffectAccentPalette[(int)(hash % (uint)EffectAccentPalette.Length)];
            }
        }

        private static void DrawEffectHeaderBackground(Rect rect, Color accent, bool enabled, bool mixed)
        {
            Color stateAccent = mixed ? new Color(0.92f, 0.66f, 0.20f, 1f) : accent;
            Color neutral = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.14f, 0.18f, 1f)
                : new Color(0.82f, 0.84f, 0.88f, 1f);
            float strength = mixed ? 0.48f : enabled ? 0.52f : 0.14f;
            EditorGUI.DrawRect(rect, Color.Lerp(neutral, stateAccent, strength));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 5f, rect.height), stateAccent);
            EditorGUI.DrawRect(new Rect(rect.x + 5f, rect.y, rect.width - 5f, 1f), Color.Lerp(stateAccent, Color.white, 0.18f));
        }

        private static void DrawEffectCardBorder(Rect rect, Color accent, bool enabled, bool mixed)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return;

            Color border = mixed ? new Color(0.92f, 0.66f, 0.20f, 1f) : accent;
            border.a = mixed || enabled ? 0.95f : 0.48f;

            const float thickness = 1f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), border);
        }

        private static void DrawEffectStatus(Rect rect, Color accent, bool enabled, bool mixed)
        {
            string status = mixed ? "混合" : enabled ? "已启用" : "未启用";
            Color statusColor = mixed
                ? new Color(0.78f, 0.50f, 0.12f, 0.95f)
                : enabled
                    ? Color.Lerp(new Color(0.10f, 0.12f, 0.16f, 0.96f), accent, 0.62f)
                    : new Color(0.30f, 0.32f, 0.36f, 0.82f);
            EditorGUI.DrawRect(rect, statusColor);
            GUI.Label(rect, status, ESEditorPresentation.MetaStyle);
        }

        private static string GetFeaturePurposeTitle(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "未命名功能";
            if (FeaturePurposeTitles.TryGetValue(displayName, out string title)) return title;
            title = displayName.StartsWith("启用", StringComparison.Ordinal) || displayName.StartsWith("使用", StringComparison.Ordinal)
                ? displayName.Substring(2).Trim()
                : displayName;
            FeaturePurposeTitles[displayName] = title;
            return title;
        }

        private static bool IsEffectToggle(string name)
        {
            return IsToggle(name);
        }

        private static string GetEffectSessionKey(string shaderName, string propertyName)
        {
            return "ES.Composite.Effect." + shaderName + "." + propertyName;
        }

        private static bool IsCollapsedEffectDependency(MaterialProperty property, string shaderName)
        {
            if (property == null) return true;
            string controller = ResolveController(property.name, shaderName);
            if (string.IsNullOrEmpty(controller) || !IsEffectToggle(controller)) return false;
            bool expanded = SessionState.GetBool(GetEffectSessionKey(shaderName, controller), true);
            return !expanded;
        }

        private static bool PropertyPassesFilter(MaterialProperty property, MaterialProperty[] all, string filter, string shaderName)
        {
            if (PropertyMatchesFilter(property, filter, shaderName)) return true;
            if (string.IsNullOrEmpty(filter) || !IsEnableProperty(property.name)) return false;

            for (int i = 0; i < all.Length; i++)
            {
                MaterialProperty dependent = all[i];
                if (string.Equals(ResolveController(dependent.name, shaderName), property.name, StringComparison.Ordinal)
                    && PropertyMatchesFilter(dependent, filter, shaderName))
                    return true;
            }
            return false;
        }

        private static void CloseEffectGroup(ref bool groupOpen, ref string activeGroup)
        {
            if (!groupOpen) return;
            EditorGUILayout.EndVertical();
            groupOpen = false;
            activeGroup = null;
        }

        private static bool IsEnableProperty(string name)
        {
            return IsToggle(name) || IsModeFeature(name);
        }

        private static string GetDisplayName(MaterialProperty property)
        {
            return Labels.TryGetValue(property.name, out string label) ? label : property.displayName;
        }

        private static bool BeginCategoryCard(string shaderName, string title, bool forceExpanded)
        {
            EditorGUILayout.Space(5f);
            string key = GetCategorySessionKey(shaderName, title);
            bool expanded = forceExpanded || SessionState.GetBool(key, true);

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.24f, 0.48f, 0.78f, 0.72f)
                : new Color(0.52f, 0.70f, 0.94f, 0.82f);
            EditorGUILayout.BeginVertical("Helpbox");
            GUI.backgroundColor = previousBackground;

            EditorGUILayout.BeginHorizontal();
            bool headerClicked = GUILayout.Button(title, ESEditorPresentation.HeaderStyle, GUILayout.Height(22f), GUILayout.ExpandWidth(true));
            bool arrowClicked = GUILayout.Button(expanded ? "▼" : "▶", EditorStyles.miniButton, GUILayout.Width(22f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
            if (headerClicked || arrowClicked)
            {
                expanded = !expanded;
                SessionState.SetBool(key, expanded);
            }

            if (!expanded)
                EditorGUILayout.EndVertical();
            return expanded;
        }

        private static string GetCategorySessionKey(string shaderName, string title)
        {
            Dictionary<string, string> keys;
            if (!CategorySessionKeys.TryGetValue(shaderName, out keys))
            {
                keys = new Dictionary<string, string>(StringComparer.Ordinal);
                CategorySessionKeys[shaderName] = keys;
            }

            string key;
            if (!keys.TryGetValue(title, out key))
            {
                key = "ES.Composite.Category." + shaderName + "." + title;
                keys[title] = key;
            }
            return key;
        }

        private static void DrawProperty(MaterialEditor editor, MaterialProperty property, string displayName)
        {
            bool showReset = !IsToggle(property.name);
            string hint = PropertyHint(property.name, (editor.target as Material)?.shader?.name);
            bool resetRequested = ESCompositeCodingHelper.DrawProperty(
                editor,
                property,
                displayName,
                showReset,
                !showReset || !IsDefault(property, editor),
                hint);
            if (resetRequested) Reset(property, editor);
        }

        private static bool IsVisible(MaterialProperty property, MaterialProperty[] all, string shaderName)
        {
            string controller = ResolveController(property.name, shaderName);
            if (!string.IsNullOrEmpty(controller))
            {
                MaterialProperty toggle = Find(all, controller);
                if (toggle != null && !toggle.hasMixedValue && toggle.floatValue < 0.5f) return false;
            }
            if ((property.name.IndexOf("Dissolve", StringComparison.Ordinal) >= 0 && property.name != "_DissolveMode") || property.name == "_FadeProgress" || property.name == "_FadePosition" || property.name == "_FadeWidth" || property.name == "_FadeMask")
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
        private static bool IsToggle(string name) { return name.StartsWith("_Enable", StringComparison.Ordinal) || name.StartsWith("_Use", StringComparison.Ordinal) || name == "_AlphaClip" || name == "_ReceiveShadows" || name.EndsWith("Toggle", StringComparison.Ordinal); }
        private static bool IsStatusFeatureToggle(string name) { return name.StartsWith("_Enable", StringComparison.Ordinal) || name.StartsWith("_Use", StringComparison.Ordinal) || name == "_AlphaClip"; }
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
                if (name == "_CoordinateMode" || name == "_TimeMode" || name == "_CustomTime" || name == "_TimeScale" || name == "_MainTexScaleOffset" || name == "_AnimationMode" || name.StartsWith("_Sequence", StringComparison.Ordinal)) return "时间与坐标";
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
                if (name == "_AlphaClip" || name == "_Cutoff" || name.StartsWith("_Stencil", StringComparison.Ordinal) || name == "_ColorMask" || name == "_UseUIAlphaClip") return "遮罩与输出";
                return "动态表现";
            }

            if (name == "_BaseMap" || name == "_BaseColor" || name == "_UseNormalMap" || name == "_NormalMap" || name == "_NormalScale" || name == "_Metallic" || name == "_Smoothness") return "基础材质";
            if (name == "_TimeMode" || name == "_CustomTime" || name == "_TimeScale" || name == "_MainTexScaleOffset" || name.StartsWith("_VertexAnimation", StringComparison.Ordinal) || name == "_EnableVertexAnimation") return "时间与形变";
            if (name == "_Occlusion" || name == "_UseOcclusionMap" || name == "_OcclusionMap" || name == "_UseEmission" || name == "_EmissionColor" || name == "_EmissionMap" || name == "_ReceiveShadows") return "光照输入";
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

        private static void SyncKeywords(MaterialEditor editor)
        {
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material != null && SyncMaterialKeywords(material))
                    EditorUtility.SetDirty(material);
            }
        }

        private static bool SyncMaterialKeywords(Material material)
        {
            bool changed = false;
            string shaderName = material.shader != null ? material.shader.name : string.Empty;
            if (shaderName == "ES/2D/Composite URP")
                changed |= DisableLegacyKeywords(material, Legacy2DKeywords);
            else if (shaderName == "ES/3D/Lit Composite URP")
                changed |= DisableLegacyKeywords(material, LegacyLitKeywords);

            if (material.HasProperty("_QualityTier"))
            {
                int tier = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_QualityTier")), 0, 2);
                changed |= SetKeyword(material, "_ES_QUALITY_STANDARD", tier == 1);
                changed |= SetKeyword(material, "_ES_QUALITY_HIGH", tier >= 2);
            }

            if (material.HasProperty("_ReceiveShadows"))
            {
                bool receiveShadows = material.GetFloat("_ReceiveShadows") > 0.5f;
                changed |= SetKeyword(material, "_RECEIVE_SHADOWS_OFF", !receiveShadows);
            }

            if (material.HasProperty("_UseUIAlphaClip"))
                changed |= SetKeyword(material, "UNITY_UI_ALPHACLIP", material.GetFloat("_UseUIAlphaClip") > 0.5f);

            if (material.shader != null
                && material.shader.name == "ES/3D/VFX Composite URP"
                && material.HasProperty("_BlendMode"))
            {
                int blendMode = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_BlendMode")), 0, 3);
                float sourceBlend;
                float destinationBlend;
                switch (blendMode)
                {
                    case 1:
                        sourceBlend = (float)UnityEngine.Rendering.BlendMode.SrcAlpha;
                        destinationBlend = (float)UnityEngine.Rendering.BlendMode.One;
                        break;
                    case 2:
                        sourceBlend = (float)UnityEngine.Rendering.BlendMode.One;
                        destinationBlend = (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                        break;
                    case 3:
                        sourceBlend = (float)UnityEngine.Rendering.BlendMode.DstColor;
                        destinationBlend = (float)UnityEngine.Rendering.BlendMode.Zero;
                        break;
                    default:
                        sourceBlend = (float)UnityEngine.Rendering.BlendMode.SrcAlpha;
                        destinationBlend = (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                        break;
                }

                changed |= SetMaterialFloat(material, "_SrcBlend", sourceBlend);
                changed |= SetMaterialFloat(material, "_DstBlend", destinationBlend);
                changed |= SetMaterialFloat(material, "_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
                string renderType = material.GetTag("RenderType", false, string.Empty);
                if (!string.Equals(renderType, "Transparent", StringComparison.Ordinal))
                {
                    material.SetOverrideTag("RenderType", "Transparent");
                    changed = true;
                }

                int queueOffset = material.HasProperty("_QueueOffset")
                    ? Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_QueueOffset")), -50, 50)
                    : 0;
                int renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + queueOffset;
                if (material.renderQueue != renderQueue)
                {
                    material.renderQueue = renderQueue;
                    changed = true;
                }
            }
            return changed;
        }

        private static bool DisableLegacyKeywords(Material material, string[] keywords)
        {
            bool changed = false;
            for (int i = 0; i < keywords.Length; i++)
                changed |= SetKeyword(material, keywords[i], false);
            return changed;
        }

        private static bool SetMaterialFloat(Material material, string propertyName, float value)
        {
            if (!material.HasProperty(propertyName) || Mathf.Approximately(material.GetFloat(propertyName), value))
                return false;
            material.SetFloat(propertyName, value);
            return true;
        }

        private static bool SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled && !material.IsKeywordEnabled(keyword))
            {
                material.EnableKeyword(keyword);
                return true;
            }
            if (!enabled && material.IsKeywordEnabled(keyword))
            {
                material.DisableKeyword(keyword);
                return true;
            }
            return false;
        }

        private static Material GetDefault(MaterialEditor editor)
        {
            Material source = editor.target as Material; if (source == null || source.shader == null) return null;
            if (!Defaults.TryGetValue(source.shader, out Material value) || value == null)
            {
                value = new Material(source.shader) { hideFlags = HideFlags.HideAndDontSave };
                Defaults[source.shader] = value;
            }
            return value;
        }

        private static void ReleaseDefaults()
        {
            foreach (KeyValuePair<Shader, Material> pair in Defaults)
            {
                if (pair.Value != null) UnityEngine.Object.DestroyImmediate(pair.Value);
            }
            Defaults.Clear();
            RouteCache.Clear();
            CategorySessionKeys.Clear();
            FeaturePurposeTitles.Clear();
            PresetCache.Clear();
            PresetNameCache.Clear();
            CachedSelectedParticleRenderers.Clear();
            DiagnosticParticleRenderers.Clear();
            CachedTargetMaterials.Clear();
            CachedParticleRendererIds.Clear();
            CachedHierarchyParticleRenderers.Clear();
            CachedRendererMaterials.Clear();
            CachedActiveParticleStreams.Clear();
            CachedDepthCameras.Clear();
            DiagnosticWarnings.Clear();
            particleConfigurationTarget = null;
            cachedParticleSelectionSignature = int.MinValue;
            cachedParticleSelectionTime = 0d;
        }

        private static bool IsDefault(MaterialProperty property, MaterialEditor editor)
        {
            if (property.hasMixedValue) return false;
            Material material = GetDefault(editor); if (material == null) return true;
            switch (property.type)
            {
                case MaterialProperty.PropType.Color: return property.colorValue == material.GetColor(property.name);
                case MaterialProperty.PropType.Vector: return property.vectorValue == material.GetVector(property.name);
                case MaterialProperty.PropType.Texture: return property.textureValue == material.GetTexture(property.name);
                default: return Mathf.Approximately(property.floatValue, material.GetFloat(property.name));
            }
        }

        private static void Reset(MaterialProperty property, MaterialEditor editor)
        {
            Material material = GetDefault(editor); if (material == null) return;
            Undo.RecordObjects(editor.targets, "重置 ES Composite 属性");
            switch (property.type)
            {
                case MaterialProperty.PropType.Color: property.colorValue = material.GetColor(property.name); break;
                case MaterialProperty.PropType.Vector: property.vectorValue = material.GetVector(property.name); break;
                case MaterialProperty.PropType.Texture: property.textureValue = material.GetTexture(property.name); break;
                default: property.floatValue = material.GetFloat(property.name); break;
            }
            for (int i = 0; i < editor.targets.Length; i++)
                if (editor.targets[i] != null) EditorUtility.SetDirty(editor.targets[i]);
        }
    }
}
