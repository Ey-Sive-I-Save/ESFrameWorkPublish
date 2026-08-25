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
            { "_NormalMap", "法线纹理" }, { "_NormalScale", "法线强度" }, { "_MaskTex", "2D 光照遮罩" },
            { "_UseMetallicMap", "使用金属度纹理" }, { "_MetallicMap", "金属度/光滑度纹理 (R/A)" },
            { "_SmoothnessMapChannel", "光滑度纹理通道" }, { "_Metallic", "金属度" }, { "_Smoothness", "光滑度" },
            { "_UseNormalMap", "启用法线纹理" }, { "_OcclusionMap", "环境遮挡纹理" }, { "_Occlusion", "环境遮挡强度" },
            { "_UseEmission", "启用自发光" }, { "_EmissionMap", "自发光纹理" }, { "_EmissionUseAlpha", "自发光乘纹理 Alpha" },
            { "_EmissionColor", "自发光颜色" }, { "_NoiseTex", "噪声纹理" }, { "_NoiseScale", "噪声缩放" }, { "_NoiseSpeed", "噪声速度" },
            { "_Distortion", "扰动强度" }, { "_DistortionStrength", "扰动强度" }, { "_DistortionDirection", "扰动方向与轴强度" }, { "_DissolveMode", "溶解模式" },
            { "_DissolveProgress", "溶解进度" }, { "_DissolveSoftness", "溶解柔和度" }, { "_DissolveWidth", "溶解边缘宽度" },
            { "_DissolveEdgeColor", "溶解边缘颜色" }, { "_DissolveColor", "溶解颜色" }, { "_EnableRim", "启用边缘光" },
            { "_RimColor", "边缘光颜色" }, { "_RimPower", "边缘光幂次" }, { "_RimIntensity", "边缘光强度" },
            { "_EnableShine", "启用扫光" }, { "_ShineColor", "扫光颜色" }, { "_ShineSpeed", "扫光速度" }, { "_ShineWidth", "扫光宽度" },
            { "_ShineAngle", "扫光角度" }, { "_ShineSpace", "扫光投影空间" }, { "_ShineDirection", "扫光方向" }, { "_ShineIntensity", "扫光强度" },
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
            { "_EnableBlur", "启用纹理模糊" }, { "_BlurRadius", "模糊半径" }, { "_BlurIntensity", "模糊强度" }, { "_BlurMode", "模糊核" },
            { "_EnableSharpen", "启用纹理锐化" }, { "_SharpenAmount", "锐化强度" }, { "_SharpenRadius", "锐化半径" },
            { "_SharpenThreshold", "锐化阈值" }, { "_SharpenFade", "锐化混合" },
            { "_EnableHologram", "启用全息" },
            { "_HologramColor", "全息颜色" }, { "_HologramFrequency", "全息线频率" }, { "_HologramLineFrequency", "全息线频率" },
            { "_HologramGap", "全息线间隔" }, { "_HologramLineGap", "全息线间隔" }, { "_HologramSpeed", "全息速度" },
            { "_HologramMinAlpha", "全息最低透明度" }, { "_HologramFade", "全息淡入" }, { "_HologramContrast", "全息对比度" },
            { "_HologramSpace", "全息扫描空间" }, { "_HologramDirection", "全息扫描方向" },
            { "_HologramDistortionOffset", "全息扰动偏移" }, { "_HologramDistortionDirection", "全息扰动方向" },
            { "_HologramDistortionSpeed", "全息扰动速度" }, { "_HologramDistortionDensity", "全息扰动密度" },
            { "_HologramDistortionScale", "全息扰动缩放" }, { "_EnableGlitch", "启用故障" }, { "_GlitchAmount", "故障强度" },
            { "_GlitchIntensity", "故障强度" }, { "_GlitchSpeed", "故障速度" }, { "_GlitchScanDirection", "故障条带方向" }, { "_GlitchFade", "故障淡入" },
            { "_GlitchMaskMin", "故障遮罩下限" }, { "_GlitchMaskScale", "故障遮罩缩放" }, { "_GlitchMaskSpeed", "故障遮罩速度" },
            { "_GlitchHueSpeed", "故障色相速度" }, { "_GlitchBrightness", "故障亮度" },
            { "_GlitchNoiseScale", "故障噪声缩放" }, { "_GlitchNoiseSpeed", "故障噪声速度" },
            { "_GlitchDistortion", "故障位移方向" }, { "_GlitchDistortionScale", "故障位移噪声缩放" },
            { "_GlitchDistortionSpeed", "故障位移噪声速度" }, { "_QualityTier", "效果质量档位" },
            { "_ResourceProfile", "资源编译配置" },
            { "_ReceiveShadows", "接收阴影" }, { "_Surface", "表面类型" }, { "_AlphaClip", "启用透明裁剪" }, { "_Cutoff", "裁剪阈值" },
            { "_VertexColorStrength", "顶点色影响" }, { "_CoordinateMode", "坐标模式" }, { "_TimeMode", "时间来源" },
            { "_CustomTime", "自定义时间" }, { "_TimeScale", "时间倍率" },
            { "_EnableTimeFPS", "启用时间帧率量化" }, { "_TimeFPS", "时间帧率" },
            { "_EnableTimeFrequency", "启用周期时间" }, { "_TimeFrequency", "时间周期频率" }, { "_TimeRange", "时间周期范围" },
            { "_MainTexScaleOffset", "主纹理缩放/偏移" }, { "_AnimationMode", "动画模式" }, { "_SequenceColumns", "序列帧列数" },
            { "_EnableUVTransform", "启用 UV 变换" }, { "_UVPivot", "UV 变换中心" }, { "_UVScale", "UV 缩放" }, { "_UVOffset", "UV 偏移" },
            { "_UVRotation", "UV 旋转" }, { "_UVRotationSpeed", "UV 旋转速度" }, { "_EnableUVDistort", "启用 UV 扰动" }, { "_UVDistortFrequency", "UV 扰动频率" },
            { "_UVDistortSpeed", "UV 扰动速度" }, { "_UVDistortAmount", "UV 扰动强度" }, { "_UVDistortNoiseTex", "UV 扰动噪声纹理" },
            { "_UVDistortFrom", "UV 扰动起始偏移" }, { "_UVDistortTo", "UV 扰动目标偏移" }, { "_UVDistortFade", "UV 扰动淡入" },
            { "_UVDistortMaskToggle", "使用 UV 扰动遮罩" }, { "_UVDistortMask", "UV 扰动遮罩" }, { "_UVDistortMaskChannel", "UV 扰动遮罩通道" },
            { "_EnableWind", "启用风摆" }, { "_WindDirection", "风摆方向" }, { "_WindAmplitude", "风摆幅度" },
            { "_WindFrequency", "风摆频率" }, { "_WindSpeed", "风摆速度" }, { "_WindAnchor", "风摆固定边界" }, { "_WindAnchorDirection", "风摆锚定方向" }, { "_WindGlobalInfluence", "全局风影响" },
            { "_EnableSquish", "启用挤压" }, { "_SquishAmount", "挤压幅度" }, { "_SquishSpeed", "挤压速度" }, { "_SquishDirection", "挤压方向" }, { "_SquishFade", "挤压强度" },
            { "_EnableWiggle", "启用摇摆" }, { "_WiggleAmplitude", "摇摆角度" }, { "_WiggleFrequency", "摇摆相位频率" }, { "_WiggleDirection", "摇摆相位方向" }, { "_WiggleSpeed", "摇摆速度" },
            { "_EnableVibrate", "启用震动" }, { "_VibrateAmplitude", "震动幅度" }, { "_VibrateSpeed", "震动速度" }, { "_VibrateDirection", "震动主方向" },
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
            { "_FadeMode", "渐隐模式" }, { "_FadeProgress", "渐隐进度" }, { "_FadePosition", "渐隐位置" }, { "_FadeRotation", "渐隐方向" },
            { "_FadeWidth", "渐隐宽度" }, { "_FadeInvert", "反转渐隐" }, { "_FadeNoiseFactor", "渐隐噪声影响" },
            { "_FadeNoiseScale", "渐隐噪声缩放" }, { "_FadeNoiseSpeed", "渐隐噪声速度" }, { "_FadeNoiseTex", "渐隐噪声纹理" },
            { "_FadeMask", "渐隐遮罩" }, { "_FadeDistortionStrength", "方向扰动强度" },
            { "_DissolveEdgeWidth", "溶解边缘宽度" }, { "_DissolveEdgeIntensity", "溶解边缘强度" }, { "_EnableAddColor", "启用叠加颜色" }, { "_AddColor", "叠加颜色" },
            { "_AddColorFade", "叠加颜色强度" }, { "_AddColorContrastToggle", "叠加颜色使用对比度" }, { "_AddColorContrast", "叠加颜色对比度" },
            { "_AddColorMaskToggle", "叠加颜色使用遮罩" }, { "_AddColorMask", "叠加颜色遮罩" },
            { "_EnableStrongTint", "启用强制染色" }, { "_StrongTint", "强制染色" }, { "_StrongTintFade", "强制染色强度" },
            { "_StrongTintContrastToggle", "强制染色使用对比度" }, { "_StrongTintContrast", "强制染色对比度" },
            { "_StrongTintMaskToggle", "强制染色使用遮罩" }, { "_StrongTintMask", "强制染色遮罩" },
            { "_EnableAlphaTint", "启用透明染色" }, { "_AlphaTint", "透明染色" }, { "_AlphaTintMin", "透明染色下限" }, { "_AlphaTintFade", "透明染色强度" },
            { "_EnableColorReplace", "启用颜色替换" }, { "_ReplaceFrom", "替换源颜色" }, { "_ReplaceTo", "替换目标颜色" },
            { "_ReplaceRange", "替换范围" }, { "_ReplaceSoftness", "替换柔和度" }, { "_ReplaceContrast", "替换亮度对比" }, { "_ReplaceFade", "替换强度" }, { "_EnableBrightness", "启用亮度" }, { "_Brightness", "亮度" },
            { "_EnableRecolorRGB", "启用 RGB 重映色" }, { "_RecolorRed", "红色通道目标色" }, { "_RecolorGreen", "绿色通道目标色" },
            { "_RecolorBlue", "蓝色通道目标色" }, { "_RecolorRGBStrength", "RGB 重映色强度" },
            { "_RecolorRGBMaskToggle", "使用 RGB 重映色遮罩" }, { "_RecolorRGBMask", "RGB 重映色遮罩" }, { "_RecolorRGBMaskChannel", "RGB 遮罩通道" },
            { "_EnableRecolorRGBYCP", "启用 RGBYCP 重映色" }, { "_RecolorRGBYCPRed", "RGBYCP 红色目标色" },
            { "_RecolorRGBYCPGreen", "RGBYCP 绿色目标色" }, { "_RecolorRGBYCPBlue", "RGBYCP 蓝色目标色" },
            { "_RecolorRGBYCPYellow", "RGBYCP 黄色目标色" }, { "_RecolorRGBYCPCyan", "RGBYCP 青色目标色" },
            { "_RecolorRGBYCPPurple", "RGBYCP 紫色目标色" }, { "_RecolorRGBYCPStrength", "RGBYCP 重映色强度" },
            { "_RecolorRGBYCPMaskToggle", "使用 RGBYCP 重映色遮罩" }, { "_RecolorRGBYCPMask", "RGBYCP 重映色遮罩" },
            { "_RecolorRGBYCPMaskChannel", "RGBYCP 遮罩通道" },
            { "_TilingMode", "主纹理平铺空间" }, { "_WorldTilingScale", "世界平铺缩放" }, { "_WorldTilingOffset", "世界平铺偏移" },
            { "_WorldTilingPixelsPerUnit", "世界平铺每单位重复数" }, { "_ScreenTilingScale", "屏幕平铺缩放" },
            { "_ScreenTilingOffset", "屏幕平铺偏移" }, { "_ScreenTilingPixelsPerUnit", "屏幕平铺像素尺寸" },
            { "_EnableSmoothPixelArt", "启用平滑像素画" }, { "_SmoothPixelStrength", "平滑像素画强度" },
            { "_EnableCheckerboard", "启用棋盘格" }, { "_CheckerboardDarken", "暗格保留亮度" }, { "_CheckerboardTiling", "棋盘格密度" },
            { "_UberNoiseTexture", "Generated 效果噪声" },
            { "_EnableFlame", "启用火焰" }, { "_FlameBrightness", "火焰亮度" }, { "_FlameSmooth", "火焰柔和度" },
            { "_FlameRadius", "火焰半径" }, { "_FlameSpeed", "火焰速度" }, { "_FlameNoiseFactor", "火焰噪声影响" },
            { "_FlameNoiseHeightFactor", "火焰高度影响" }, { "_FlameNoiseScale", "火焰噪声缩放" },
            { "_FlameCenter", "火焰中心" }, { "_FlameDirection", "火焰方向" },
            { "_EnableSmoke", "启用烟雾" }, { "_SmokeAlpha", "烟雾透明度" }, { "_SmokeSmoothness", "烟雾柔和度" },
            { "_SmokeNoiseScale", "烟雾噪声缩放" }, { "_SmokeNoiseFactor", "烟雾噪声影响" }, { "_SmokeDarkEdge", "烟雾暗边" },
            { "_SmokeVertexSeed", "烟雾使用顶点色种子" }, { "_SmokeSpeed", "烟雾流动速度" },
            { "_EnablePixelate", "启用像素化" }, { "_PixelateCells", "横向像素格数" }, { "_PixelateStrength", "像素化强度" },
            { "_EnablePalette", "启用调色板映射" }, { "_PaletteTex", "调色板纹理" }, { "_PaletteRow", "调色板采样行" }, { "_PaletteStrength", "调色板强度" },
            { "_EnableHalftone", "启用半色调" }, { "_HalftoneScale", "半色调密度" }, { "_HalftoneAngle", "半色调角度" }, { "_HalftoneStrength", "半色调强度" },
            { "_HalftonePosition", "半色调中心" }, { "_HalftoneFade", "半色调扩散" }, { "_HalftoneFadeWidth", "半色调扩散宽度" },
            { "_HalftoneInvert", "反转半色调" }, { "_HalftoneAlphaPattern", "使用 ESNative 透明点阵" },
            { "_EnableFullDistortion", "启用 ESNative 全局扰动" }, { "_FullDistortionFade", "全局扰动淡出" },
            { "_FullDistortionDistortion", "全局扰动方向强度" }, { "_FullDistortionNoiseScale", "全局扰动噪声缩放" },
            { "_EnableTextureLayer1", "启用纹理层 1" }, { "_TextureLayer1Texture", "纹理层 1 贴图" }, { "_TextureLayer1Color", "纹理层 1 颜色" },
            { "_EnableTextureLayer2", "启用纹理层 2" }, { "_TextureLayer2Texture", "纹理层 2 贴图" }, { "_TextureLayer2Color", "纹理层 2 颜色" },
            { "_EnableContrast", "启用对比度" }, { "_Contrast", "对比度" }, { "_EnableSaturation", "启用饱和度" }, { "_Saturation", "饱和度" },
            { "_EnableHue", "启用色相偏移" }, { "_Hue", "色相偏移" }, { "_EnableNegative", "启用负片" }, { "_NegativeFade", "负片强度" },
            { "_EnableSplitToning", "启用分离色调" }, { "_SplitToneShadows", "阴影色调" }, { "_SplitToneHighlights", "高光色调" },
            { "_SplitToneBalance", "分离色调平衡" }, { "_SplitToneStrength", "分离色调强度" }, { "_SplitToneContrast", "分离色调对比" }, { "_SplitToneShift", "分离色调亮度偏移" },
            { "_EnableBlackTint", "启用暗部染色" }, { "_BlackTintFade", "暗部染色强度" },
            { "_BlackTintColor", "暗部染色颜色" }, { "_BlackTintPower", "暗部染色幂次" },
            { "_EnableInkSpread", "启用墨水扩散" }, { "_InkSpreadFade", "墨水扩散强度" },
            { "_InkSpreadColor", "墨水扩散颜色" }, { "_InkSpreadContrast", "墨水扩散对比度" },
            { "_InkSpreadDistance", "墨水扩散距离" }, { "_InkSpreadPosition", "墨水扩散中心" },
            { "_InkSpreadWidth", "墨水扩散宽度" }, { "_InkSpreadNoiseScale", "墨水噪声缩放" },
            { "_InkSpreadNoiseFactor", "墨水噪声影响" },
            { "_EnableShiftHue", "启用动态色相偏移" }, { "_ShiftHueSpeed", "动态色相速度" },
            { "_EnableAddHue", "启用动态色相叠加" }, { "_AddHueFade", "动态色相叠加强度" },
            { "_AddHueSpeed", "动态色相叠加速度" }, { "_AddHueBrightness", "动态色相叠加亮度" },
            { "_AddHueSaturation", "动态色相叠加饱和度" }, { "_AddHueContrast", "动态色相叠加对比度" },
            { "_AddHueMaskToggle", "使用动态色相叠加遮罩" }, { "_AddHueMask", "动态色相叠加遮罩" },
            { "_EnableSineGlow", "启用正弦辉光" }, { "_SineGlowFade", "正弦辉光强度" },
            { "_SineGlowColor", "正弦辉光颜色" }, { "_SineGlowContrast", "正弦辉光对比度" },
            { "_SineGlowFrequency", "正弦辉光频率" }, { "_SineGlowMin", "正弦辉光下限" },
            { "_SineGlowMax", "正弦辉光上限" }, { "_SineGlowMaskToggle", "使用正弦辉光遮罩" },
            { "_SineGlowMask", "正弦辉光遮罩" },
            { "_EnableShadow", "启用精灵阴影" }, { "_ShadowFade", "精灵阴影强度" },
            { "_ShadowOffset", "精灵阴影偏移" }, { "_ShadowColor", "精灵阴影颜色" },
            { "_EnableRainbow", "启用彩虹渐变" }, { "_RainbowSpeed", "彩虹速度" }, { "_RainbowDensity", "彩虹密度" }, { "_RainbowDirection", "彩虹色带方向" }, { "_RainbowBrightness", "彩虹亮度" },
            { "_EnableInnerOutline", "启用内描边" }, { "_InnerOutlineFade", "内描边淡入" }, { "_InnerOutlineColor", "内描边颜色" }, { "_InnerOutlineWidth", "内描边宽度" },
            { "_InnerOutlineDistortionToggle", "内描边扰动" }, { "_InnerOutlineDistortionIntensity", "内描边扰动强度" },
            { "_InnerOutlineNoiseScale", "内描边噪声缩放" }, { "_InnerOutlineNoiseSpeed", "内描边噪声速度" },
            { "_InnerOutlineTextureToggle", "内描边纹理着色" }, { "_InnerOutlineTintTexture", "内描边纹理" },
            { "_InnerOutlineTextureSpeed", "内描边纹理速度" }, { "_InnerOutlineOutlineOnlyToggle", "仅显示内描边" },
            { "_EnableOuterOutline", "启用外描边" }, { "_OuterOutlineFade", "外描边淡入" }, { "_OuterOutlineColor", "外描边颜色" }, { "_OuterOutlineWidth", "外描边宽度" },
            { "_OuterOutlineDistortionToggle", "外描边扰动" }, { "_OuterOutlineDistortionIntensity", "外描边扰动强度" },
            { "_OuterOutlineNoiseScale", "外描边噪声缩放" }, { "_OuterOutlineNoiseSpeed", "外描边噪声速度" },
            { "_OuterOutlineTextureToggle", "外描边纹理着色" }, { "_OuterOutlineTintTexture", "外描边纹理" },
            { "_OuterOutlineTextureSpeed", "外描边纹理速度" }, { "_OuterOutlineOutlineOnlyToggle", "仅显示外描边" },
            { "_EnablePixelOutline", "启用像素描边" }, { "_PixelOutlineFade", "像素描边淡入" }, { "_PixelOutlineColor", "像素描边颜色" }, { "_PixelOutlineWidth", "像素描边宽度" },
            { "_PixelOutlineTextureToggle", "像素描边纹理着色" }, { "_PixelOutlineTintTexture", "像素描边纹理" },
            { "_PixelOutlineTextureSpeed", "像素描边纹理速度" }, { "_PixelOutlineOutlineOnlyToggle", "仅显示像素描边" },
            { "_EnablePingPongGlow", "启用往返发光" }, { "_GlowFrom", "发光起点颜色" }, { "_GlowTo", "发光终点颜色" },
            { "_GlowFrequency", "发光频率" }, { "_GlowIntensity", "发光强度" }, { "_GlowContrast", "发光亮度对比" }, { "_GlowFade", "发光淡入" }, { "_EnableDistortion", "启用噪声扰动" },
            { "_EnableFrozen", "启用冰冻" }, { "_FrozenColor", "冰冻颜色" }, { "_FrozenHighlight", "冰冻高光" },
            { "_FrozenDensity", "冰冻雪花密度" }, { "_FrozenSpeed", "冰冻流动速度" }, { "_EnableBurn", "启用燃烧" },
            { "_BurnEdgeColor", "燃烧边缘颜色" }, { "_BurnInsideColor", "燃烧内部颜色" }, { "_BurnProgress", "燃烧进度" }, { "_BurnWidth", "燃烧边缘宽度" },
            { "_EnablePoison", "启用中毒" }, { "_PoisonColor", "中毒颜色" }, { "_PoisonDensity", "中毒密度" }, { "_PoisonSpeed", "中毒速度" },
            { "_ESNativeStatusContract", "使用 ESNative 精确效果合同" },
            { "_FrozenFade", "冰冻强度" }, { "_FrozenTint", "冰冻色调" }, { "_FrozenContrast", "冰冻对比度" },
            { "_FrozenSnowColor", "冰冻雪花颜色" }, { "_FrozenSnowContrast", "冰冻雪花对比度" },
            { "_FrozenSnowDensity", "冰冻雪花密度" }, { "_FrozenSnowScale", "冰冻雪花缩放" },
            { "_FrozenHighlightColor", "冰冻高光颜色" }, { "_FrozenHighlightContrast", "冰冻高光对比度" },
            { "_FrozenHighlightDensity", "冰冻高光密度" }, { "_FrozenHighlightSpeed", "冰冻高光速度" },
            { "_FrozenHighlightScale", "冰冻高光缩放" }, { "_FrozenHighlightDistortion", "冰冻高光扰动" },
            { "_FrozenHighlightDistortionSpeed", "冰冻高光扰动速度" }, { "_FrozenHighlightDistortionScale", "冰冻高光扰动缩放" },
            { "_BurnFade", "燃烧强度" }, { "_BurnPosition", "燃烧中心" }, { "_BurnRadius", "燃烧半径" },
            { "_BurnEdgeNoiseScale", "燃烧边缘噪声缩放" }, { "_BurnEdgeNoiseFactor", "燃烧边缘噪声影响" },
            { "_BurnInsideContrast", "燃烧内部对比度" }, { "_BurnInsideNoiseColor", "燃烧内部噪声颜色" },
            { "_BurnInsideNoiseFactor", "燃烧内部噪声影响" }, { "_BurnInsideNoiseScale", "燃烧内部噪声缩放" },
            { "_BurnSwirlFactor", "燃烧旋涡强度" }, { "_BurnSwirlNoiseScale", "燃烧旋涡噪声缩放" },
            { "_RainbowFade", "彩虹强度" }, { "_RainbowSaturation", "彩虹饱和度" }, { "_RainbowContrast", "彩虹对比度" },
            { "_RainbowCenter", "彩虹中心" }, { "_RainbowNoiseScale", "彩虹噪声缩放" }, { "_RainbowNoiseFactor", "彩虹噪声影响" },
            { "_ShineFade", "扫光淡入" }, { "_ShineSaturation", "扫光饱和度" }, { "_ShineContrast", "扫光对比度" },
            { "_ShineRotation", "扫光旋转（方向为零时）" }, { "_ShineSmooth", "扫光平滑度" }, { "_ShineFrequency", "扫光频率" },
            { "_ShineMaskToggle", "使用扫光遮罩" }, { "_ShineMask", "扫光遮罩" },
            { "_PoisonFade", "中毒强度" }, { "_PoisonRecolorFactor", "中毒重着色比例" },
            { "_PoisonShiftSpeed", "中毒条纹速度" }, { "_PoisonNoiseBrightness", "中毒噪声亮度" },
            { "_PoisonNoiseScale", "中毒噪声缩放" }, { "_PoisonNoiseSpeed", "中毒噪声速度" },
            { "_UseOcclusionMap", "使用环境遮挡纹理" }, { "_StencilComp", "Stencil 比较方式" },
            { "_Stencil", "Stencil ID" }, { "_StencilOp", "Stencil 操作" }, { "_StencilReadMask", "Stencil 读取掩码" },
            { "_StencilWriteMask", "Stencil 写入掩码" }, { "_ColorMask", "颜色写入掩码" }, { "_UseUIAlphaClip", "启用 UI 透明裁剪" },
            { "_EnableSDF", "启用 SDF 字体" }, { "_SDFThreshold", "SDF 字面阈值" }, { "_SDFSoftness", "SDF 边缘柔和度" },
            { "_SDFOutlineWidth", "SDF 描边宽度" }, { "_SDFOutlineSoftness", "SDF 描边柔和度" }, { "_SDFOutlineColor", "SDF 描边颜色" },
            { "_SDFGlowWidth", "SDF 辉光宽度" }, { "_SDFGlowColor", "SDF 辉光颜色" },
            { "_EnableTMPCompatibility", "启用 TMP 材质合同" }, { "_FaceColor", "TMP 字面颜色" },
            { "_FaceDilate", "TMP 字面扩张" }, { "_OutlineColor", "TMP 描边颜色" }, { "_OutlineWidth", "TMP 描边宽度" },
            { "_OutlineSoftness", "TMP 描边柔和度" }, { "_EnableUnderlay", "启用 TMP 底衬" },
            { "_UnderlayColor", "TMP 底衬颜色" }, { "_UnderlayOffsetX", "TMP 底衬 X 偏移" },
            { "_UnderlayOffsetY", "TMP 底衬 Y 偏移" }, { "_UnderlayDilate", "TMP 底衬扩张" },
            { "_UnderlaySoftness", "TMP 底衬柔和度" }, { "_WeightNormal", "TMP 常规字重" }, { "_WeightBold", "TMP 粗体字重" }
        };

        private static readonly string[] TwoDCategoryOrder =
        {
            "基础输入", "时间与坐标", "顶点形变", "遮罩与溶解", "色彩调整", "风格化", "轮廓", "动态表现", "状态表现", "输出控制"
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
            "基础输入", "SDF 字体", "色彩调整", "时间与坐标", "顶点形变", "风格化", "轮廓", "动态表现", "遮罩与输出", "渲染状态"
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
            { "_EnableBlackTint", new Color(0.34f, 0.42f, 0.72f, 1f) },
            { "_EnableInkSpread", new Color(0.88f, 0.58f, 0.14f, 1f) },
            { "_EnableShiftHue", new Color(0.30f, 0.78f, 0.70f, 1f) },
            { "_EnableAddHue", new Color(0.96f, 0.46f, 0.44f, 1f) },
            { "_EnableSineGlow", new Color(0.18f, 0.78f, 0.96f, 1f) },
            { "_EnableShadow", new Color(0.38f, 0.44f, 0.54f, 1f) },
            { "_EnableCamouflage", new Color(0.42f, 0.62f, 0.34f, 1f) },
            { "_EnableMetal", new Color(0.90f, 0.68f, 0.24f, 1f) },
            { "_EnableEnchanted", new Color(0.32f, 0.82f, 0.84f, 1f) },
            { "_EnableShifting", new Color(0.92f, 0.40f, 0.56f, 1f) },
            { "_EnableFullGlowDissolve", new Color(1.00f, 0.32f, 0.20f, 1f) },
            { "_EnableCustomFade", new Color(0.64f, 0.50f, 0.88f, 1f) },
            { "_EnableSqueeze", new Color(0.42f, 0.76f, 0.48f, 1f) },
            { "_EnableSineRotate", new Color(0.28f, 0.70f, 0.88f, 1f) },
            { "_EnableSineMove", new Color(0.32f, 0.78f, 0.64f, 1f) },
            { "_EnableSineScale", new Color(0.58f, 0.72f, 0.30f, 1f) },
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
            new EffectRoute("black-tint", "暗部染色", "颜色处理", "暗部", "黑色染色", "Black Tint"),
            new EffectRoute("ink-spread", "墨水扩散", "动态效果", "墨水", "扩散", "Ink Spread"),
            new EffectRoute("shift-hue", "动态色相偏移", "颜色处理", "动态色相", "色相偏移", "Shift Hue"),
            new EffectRoute("add-hue", "动态色相叠加", "颜色处理", "色相叠加", "Add Hue"),
            new EffectRoute("sine-glow", "正弦辉光", "动态效果", "呼吸辉光", "正弦发光", "Sine Glow"),
            new EffectRoute("sprite-shadow", "精灵阴影", "描边", "投影", "偏移阴影", "Sprite Shadow"),
            new EffectRoute("camouflage", "迷彩", "风格化", "伪装", "斑块", "Camouflage"),
            new EffectRoute("metal", "金属着色", "风格化", "金属", "高光噪声", "Metal"),
            new EffectRoute("enchanted", "附魔流光", "动态效果", "附魔", "魔法流光", "Enchanted"),
            new EffectRoute("shifting", "明度流变", "动态效果", "流变", "颜色流动", "Shifting"),
            new EffectRoute("full-glow-dissolve", "全局辉光溶解", "渐隐与溶解", "全辉光", "Glow Dissolve", "Full Glow Dissolve"),
            new EffectRoute("custom-fade", "自定义渐隐", "渐隐与溶解", "顶点透明渐隐", "Custom Fade"),
            new EffectRoute("squeeze", "径向挤压", "坐标变换", "挤压", "Squeeze"),
            new EffectRoute("sine-rotate", "正弦旋转", "坐标变换", "周期旋转", "Sine Rotate"),
            new EffectRoute("sine-move", "正弦移动", "顶点形变", "周期移动", "Sine Move"),
            new EffectRoute("sine-scale", "正弦缩放", "顶点形变", "呼吸缩放", "Sine Scale"),
            new EffectRoute("state", "冰冻/燃烧/中毒", "状态效果", "冰冻", "燃烧", "中毒", "Frozen", "Burn", "Poison"),
            new EffectRoute("output", "裁剪/阴影", "输出与质量", "裁剪", "阴影", "质量", "Alpha", "Shadow", "Quality")
        };
        private static readonly Dictionary<string, RouteCacheEntry> RouteCache = new Dictionary<string, RouteCacheEntry>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Dictionary<string, string>> CategorySessionKeys = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> FeaturePurposeTitles = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> EffectDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "_UseNormalMap", "使用法线纹理改变光照法线；关闭时跳过法线采样。" },
            { "_UseMetallicMap", "使用纹理 R 通道调制金属度、A 通道调制光滑度；关闭时保持标量工作流。" },
            { "_UseOcclusionMap", "使用环境遮挡纹理压低间接光；关闭时遮挡值固定为 1。" },
            { "_UseEmission", "叠加自发光颜色和纹理；适合能量、霓虹和受击反馈。" },
            { "_EnableDistortion", "用噪声驱动 UV 扰动；启用后会读取噪声纹理。" },
            { "_EnableInnerOutline", "在原图形内部以八方向邻域生成轮廓，可选噪声扰动、纹理着色和仅描边输出。" },
            { "_EnableOuterOutline", "在透明留白内以八方向邻域生成外轮廓，可独立扰动和着色；源网格需要预留足够边距。" },
            { "_EnablePixelOutline", "按基础纹理像素尺寸生成四方向硬边轮廓，可独立纹理着色和仅描边输出。" },
            { "_EnableShine", "沿指定方向移动高光带；可选择兼容默认、局部 UV 或世界投影空间，速度、宽度和颜色可独立调整。" },
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
            { "_EnableSmoothPixelArt", "使用屏幕导数重建像素边缘；不增加纹理采样，适合低分辨率像素素材的缩放显示。" },
            { "_EnableCheckerboard", "按连续世界坐标生成交错暗格；可用于棋盘、网格阴影和程序化材质底纹。" },
            { "_EnableFlame", "用局部 UV、可配置中心与方向、径向遮罩和滚动噪声生成火焰；适合空白白图或粒子卡片。" },
            { "_EnableSmoke", "按局部 UV、顶点色和可配置流速的噪声生成烟雾轮廓，同时控制暗边与透明度。" },
            { "_EnablePingPongGlow", "在两个颜色之间往返发光；适合循环提示和呼吸效果。" },
            { "_EnableBlackTint", "只在原图暗部叠加 HDR 颜色，幂次越高影响越集中在最暗区域。" },
            { "_EnableInkSpread", "从指定中心按距离和共享噪声推进墨水颜色，距离参数可由动画驱动。" },
            { "_EnableShiftHue", "按当前时间源持续旋转原图色相，保留原饱和度和明度。" },
            { "_EnableAddHue", "按时间生成高饱和色并依据原图亮度叠加，可选独立遮罩。" },
            { "_EnableSineGlow", "按正弦波周期叠加 HDR 辉光，可选彩色遮罩限制区域。" },
            { "_EnableShadow", "采样偏移后的主纹理 Alpha，在原图透明区域后方合成单次精灵阴影。" },
            { "_EnableCamouflage", "使用两层共享噪声和三种颜色重建迷彩图案，可选时间扰动。" },
            { "_EnableMetal", "使用双层噪声生成移动高光，并按原图明度映射 HDR 金属颜色。" },
            { "_EnableEnchanted", "叠加或替换双向滚动噪声流光，可在双色与彩虹模式间切换。" },
            { "_EnableShifting", "把原图明度与时间组合为循环相位，再映射到双色或彩虹色带。" },
            { "_EnableFullGlowDissolve", "按共享噪声执行硬溶解并在阈值边界叠加完整 HDR 辉光。" },
            { "_EnableCustomFade", "以顶点 Alpha、遮罩和共享噪声计算非线性透明度，接管普通顶点 Alpha 乘法。" },
            { "_EnableSqueeze", "在 UV 空间按到中心的幂次距离执行径向挤压。" },
            { "_EnableSineRotate", "围绕指定 UV 中心按 ESNative 兼容角度公式周期旋转。" },
            { "_EnableSineMove", "在顶点阶段按 X/Y 独立频率执行周期位移。" },
            { "_EnableSineScale", "在顶点阶段从原始顶点位置执行单向正弦缩放。" },
            { "_EnableSquish", "沿可配置局部方向执行保面积挤压；交互挤压复用同一方向。" },
            { "_EnableVibrate", "沿可配置主方向及其垂直方向组合周期位移，形成二维震动。" },
            { "_EnableHologram", "在局部 UV 或世界高度空间生成扫描线，并支持对比度、透明度和分段扰动。" },
            { "_EnableGlitch", "以独立遮罩、颜色噪声和位移噪声产生方向可控的故障效果。" },
            { "_ESNativeStatusContract", "启用后按 ESNative 精确参数与共享公式执行状态、全息、故障和描边效果；关闭时保持 ES 原生轻量公式。" },
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
        private static readonly HashSet<string> ESNativeStatusContractProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "_FrozenFade", "_FrozenTint", "_FrozenContrast", "_FrozenSnowColor",
            "_FrozenSnowContrast", "_FrozenSnowDensity", "_FrozenSnowScale",
            "_FrozenHighlightColor", "_FrozenHighlightContrast", "_FrozenHighlightDensity",
            "_FrozenHighlightSpeed", "_FrozenHighlightScale", "_FrozenHighlightDistortion",
            "_FrozenHighlightDistortionSpeed", "_FrozenHighlightDistortionScale",
            "_BurnFade", "_BurnPosition", "_BurnRadius", "_BurnEdgeNoiseScale",
            "_BurnEdgeNoiseFactor", "_BurnInsideColor", "_BurnInsideContrast", "_BurnInsideNoiseColor",
            "_BurnInsideNoiseFactor", "_BurnInsideNoiseScale", "_BurnSwirlFactor", "_BurnSwirlNoiseScale",
            "_RainbowFade", "_RainbowSaturation", "_RainbowContrast", "_RainbowCenter",
            "_RainbowNoiseScale", "_RainbowNoiseFactor",
            "_ShineFade", "_ShineSaturation", "_ShineContrast", "_ShineRotation",
            "_ShineSmooth", "_ShineFrequency", "_ShineMaskToggle", "_ShineMask",
            "_PoisonFade", "_PoisonRecolorFactor", "_PoisonShiftSpeed",
            "_PoisonNoiseBrightness", "_PoisonNoiseScale", "_PoisonNoiseSpeed"
        };
        private static readonly HashSet<string> LegacyStatusProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "_FrozenColor", "_FrozenHighlight", "_FrozenDensity", "_FrozenSpeed",
            "_BurnProgress", "_ShineAngle", "_ShineIntensity", "_ShineDirection", "_PoisonSpeed"
        };
        private static readonly HashSet<string> ESNativeStylizedContractProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "_InnerOutlineFade", "_InnerOutlineDistortionToggle", "_InnerOutlineDistortionIntensity",
            "_InnerOutlineNoiseScale", "_InnerOutlineNoiseSpeed", "_InnerOutlineTextureToggle",
            "_InnerOutlineTintTexture", "_InnerOutlineTextureSpeed", "_InnerOutlineOutlineOnlyToggle",
            "_OuterOutlineFade", "_OuterOutlineDistortionToggle", "_OuterOutlineDistortionIntensity",
            "_OuterOutlineNoiseScale", "_OuterOutlineNoiseSpeed", "_OuterOutlineTextureToggle",
            "_OuterOutlineTintTexture", "_OuterOutlineTextureSpeed", "_OuterOutlineOutlineOnlyToggle",
            "_PixelOutlineFade", "_PixelOutlineTextureToggle", "_PixelOutlineTintTexture",
            "_PixelOutlineTextureSpeed", "_PixelOutlineOutlineOnlyToggle",
            "_HologramFade", "_HologramContrast", "_HologramSpace", "_HologramLineFrequency",
            "_HologramLineGap", "_HologramMinAlpha", "_HologramDistortionOffset",
            "_HologramDistortionDirection", "_HologramDistortionSpeed",
            "_HologramDistortionDensity", "_HologramDistortionScale",
            "_GlitchFade", "_GlitchMaskMin", "_GlitchMaskScale", "_GlitchMaskSpeed",
            "_GlitchHueSpeed", "_GlitchBrightness", "_GlitchNoiseScale", "_GlitchNoiseSpeed",
            "_GlitchDistortion", "_GlitchDistortionScale", "_GlitchDistortionSpeed"
        };
        private static readonly HashSet<string> LegacyStylizedProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "_HologramFrequency", "_GlitchAmount", "_GlitchIntensity", "_GlitchSpeed"
        };
        #endregion

        #region Property Classification

        private static bool IsVisible(MaterialProperty property, MaterialProperty[] all, string shaderName)
        {
            MaterialProperty statusContract = Find(all, "_ESNativeStatusContract");
            if (statusContract != null && !statusContract.hasMixedValue)
            {
                bool useESNativeStatusContract = statusContract.floatValue > 0.5f;
                bool spriteOrUI = shaderName == "ES/2D/Composite URP"
                    || shaderName == "ES/UI/Composite URP";
                bool vfx = shaderName == "ES/3D/VFX Composite URP";
                bool keepActiveShineProperty = property.name == "_ShineDirection"
                    || (vfx && property.name == "_ShineIntensity");
                bool isContractOnly = ESNativeStatusContractProperties.Contains(property.name)
                    && (property.name != "_BurnInsideColor" || shaderName == "ES/3D/Lit Composite URP");
                if (isContractOnly && !useESNativeStatusContract) return false;
                if (LegacyStatusProperties.Contains(property.name)
                    && useESNativeStatusContract
                    && !keepActiveShineProperty) return false;
                bool stylizedContractShader = spriteOrUI || vfx;
                bool sharedHologramProperty = property.name == "_HologramFade"
                    || (shaderName == "ES/2D/Composite URP"
                        && (property.name == "_HologramLineFrequency"
                            || property.name == "_HologramLineGap"
                            || property.name == "_HologramMinAlpha"))
                    || (vfx && property.name == "_HologramMinAlpha");
                bool sharedGlitchProperty = property.name == "_GlitchDistortion";
                if (stylizedContractShader
                    && ESNativeStylizedContractProperties.Contains(property.name)
                    && !sharedHologramProperty
                    && !sharedGlitchProperty
                    && !useESNativeStatusContract) return false;
                if (stylizedContractShader
                    && LegacyStylizedProperties.Contains(property.name)
                    && useESNativeStatusContract) return false;
            }
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
            if (property.name.StartsWith("_Fade", StringComparison.Ordinal)
                || property.name.StartsWith("_DissolveEdge", StringComparison.Ordinal))
            {
                MaterialProperty mode = Find(all, "_FadeMode");
                if (mode != null && !mode.hasMixedValue)
                {
                    int fadeMode = Mathf.RoundToInt(mode.floatValue);
                    if (property.name == "_FadeMask" && fadeMode != 2) return false;
                    if (property.name == "_FadePosition"
                        && fadeMode != 1 && fadeMode != 4 && fadeMode != 5 && fadeMode != 6 && fadeMode != 7) return false;
                    if (property.name == "_FadeRotation"
                        && fadeMode != 1 && fadeMode != 4 && fadeMode != 5) return false;
                    if (property.name == "_FadeDistortionStrength" && fadeMode != 5) return false;
                    if (property.name.StartsWith("_DissolveEdge", StringComparison.Ordinal)
                        && fadeMode != 4 && fadeMode != 7) return false;
                }
            }
            if (property.name == "_UVDistortMask" || property.name == "_UVDistortMaskChannel")
            {
                MaterialProperty toggle = Find(all, "_UVDistortMaskToggle");
                if (toggle != null && !toggle.hasMixedValue && toggle.floatValue < 0.5f) return false;
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
            if (property.name.StartsWith("_WorldTiling", StringComparison.Ordinal))
            {
                MaterialProperty mode = Find(all, "_TilingMode");
                if (mode != null && !mode.hasMixedValue && Mathf.RoundToInt(mode.floatValue) != 1) return false;
            }
            if (property.name.StartsWith("_ScreenTiling", StringComparison.Ordinal))
            {
                MaterialProperty mode = Find(all, "_TilingMode");
                if (mode != null && !mode.hasMixedValue && Mathf.RoundToInt(mode.floatValue) != 2) return false;
            }
            if (property.name == "_UberNoiseTexture")
            {
                MaterialProperty flame = Find(all, "_EnableFlame");
                MaterialProperty smoke = Find(all, "_EnableSmoke");
                MaterialProperty contract = Find(all, "_ESNativeStatusContract");
                bool flameInactive = flame == null || (!flame.hasMixedValue && flame.floatValue < 0.5f);
                bool smokeInactive = smoke == null || (!smoke.hasMixedValue && smoke.floatValue < 0.5f);
                bool contractInactive = contract == null || (!contract.hasMixedValue && contract.floatValue < 0.5f);
                if (flameInactive && smokeInactive && contractInactive) return false;
            }
            return true;
        }

        private static string ResolveController(string name, string shaderName)
        {
            if (name.StartsWith("_Enable", StringComparison.Ordinal)) return null;
            if (name == "_TimeFPS") return "_EnableTimeFPS";
            if (name == "_TimeFrequency" || name == "_TimeRange") return "_EnableTimeFrequency";
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
            if (name.StartsWith("_RecolorRGBYCP", StringComparison.Ordinal)) return "_EnableRecolorRGBYCP";
            if (name == "_RecolorRed" || name == "_RecolorGreen" || name == "_RecolorBlue"
                || name.StartsWith("_RecolorRGB", StringComparison.Ordinal)) return "_EnableRecolorRGB";
            if (name == "_Brightness") return "_EnableBrightness";
            if (name == "_Contrast") return "_EnableContrast";
            if (name == "_Saturation") return "_EnableSaturation";
            if (name == "_Hue") return "_EnableHue";
            if (name.StartsWith("_SplitTone", StringComparison.Ordinal)) return "_EnableSplitToning";
            if (name.StartsWith("_BlackTint", StringComparison.Ordinal)) return "_EnableBlackTint";
            if (name.StartsWith("_InkSpread", StringComparison.Ordinal)) return "_EnableInkSpread";
            if (name.StartsWith("_ShiftHue", StringComparison.Ordinal)) return "_EnableShiftHue";
            if (name.StartsWith("_AddHue", StringComparison.Ordinal)) return "_EnableAddHue";
            if (name.StartsWith("_SineGlow", StringComparison.Ordinal)) return "_EnableSineGlow";
            if (name.StartsWith("_Squeeze", StringComparison.Ordinal)) return "_EnableSqueeze";
            if (name.StartsWith("_SineRotate", StringComparison.Ordinal)) return "_EnableSineRotate";
            if (name.StartsWith("_SineMove", StringComparison.Ordinal)) return "_EnableSineMove";
            if (name.StartsWith("_SineScale", StringComparison.Ordinal)) return "_EnableSineScale";
            if (name.StartsWith("_CustomFade", StringComparison.Ordinal)) return "_EnableCustomFade";
            if (name.StartsWith("_FullAlphaDissolve", StringComparison.Ordinal)) return "_EnableFullAlphaDissolve";
            if (name.StartsWith("_SourceAlphaDissolve", StringComparison.Ordinal)) return "_EnableSourceAlphaDissolve";
            if (name.StartsWith("_SourceGlowDissolve", StringComparison.Ordinal)) return "_EnableSourceGlowDissolve";
            if (name.StartsWith("_DirectionalAlphaFade", StringComparison.Ordinal)) return "_EnableDirectionalAlphaFade";
            if (name.StartsWith("_DirectionalGlowFade", StringComparison.Ordinal)) return "_EnableDirectionalGlowFade";
            if (name.StartsWith("_DirectionalDistortion", StringComparison.Ordinal)) return "_EnableDirectionalDistortion";
            if (name.StartsWith("_FullGlowDissolve", StringComparison.Ordinal)) return "_EnableFullGlowDissolve";
            if (name.StartsWith("_FullDistortion", StringComparison.Ordinal)) return "_EnableFullDistortion";
            if (name.StartsWith("_Camouflage", StringComparison.Ordinal)) return "_EnableCamouflage";
            if (name.StartsWith("_Metal", StringComparison.Ordinal)) return "_EnableMetal";
            if (name.StartsWith("_Enchanted", StringComparison.Ordinal)) return "_EnableEnchanted";
            if (name.StartsWith("_Shifting", StringComparison.Ordinal)) return "_EnableShifting";
            if ((shaderName == "ES/2D/Composite URP" || shaderName == "ES/UI/Composite URP")
                && name.StartsWith("_Shadow", StringComparison.Ordinal)) return "_EnableShadow";
            if (name.StartsWith("_UVDistort", StringComparison.Ordinal)) return "_EnableUVDistort";
            if (name.StartsWith("_UV", StringComparison.Ordinal)) return "_EnableUVTransform";
            if (name == "_NegativeFade") return "_EnableNegative";
            if (name.StartsWith("_Rainbow", StringComparison.Ordinal)) return "_EnableRainbow";
            if (name.StartsWith("_Wind", StringComparison.Ordinal)) return "_EnableWind";
            if (name.StartsWith("_Squish", StringComparison.Ordinal)) return "_EnableSquish";
            if (name.StartsWith("_Wiggle", StringComparison.Ordinal)) return "_EnableWiggle";
            if (name.StartsWith("_Vibrate", StringComparison.Ordinal)) return "_EnableVibrate";
            if (name.StartsWith("_Pixelate", StringComparison.Ordinal)) return "_EnablePixelate";
            if (name.StartsWith("_WorldTiling", StringComparison.Ordinal)) return "_TilingMode";
            if (name.StartsWith("_ScreenTiling", StringComparison.Ordinal)) return "_TilingMode";
            if (name == "_SmoothPixelStrength") return "_EnableSmoothPixelArt";
            if (name.StartsWith("_Checkerboard", StringComparison.Ordinal)) return "_EnableCheckerboard";
            if (name.StartsWith("_Flame", StringComparison.Ordinal)) return "_EnableFlame";
            if (name.StartsWith("_Smoke", StringComparison.Ordinal)) return "_EnableSmoke";
            if (name.StartsWith("_Palette", StringComparison.Ordinal)) return "_EnablePalette";
            if (name.StartsWith("_Halftone", StringComparison.Ordinal)) return "_EnableHalftone";
            if (name == "_FaceColor" || name == "_FaceDilate" || name == "_OutlineColor" || name == "_OutlineWidth"
                || name == "_OutlineSoftness" || name.StartsWith("_Underlay", StringComparison.Ordinal)
                || name == "_WeightNormal" || name == "_WeightBold" || name.StartsWith("_ScaleRatio", StringComparison.Ordinal)
                || name == "_GradientScale" || name == "_Sharpness" || name.StartsWith("_TextureWidth", StringComparison.Ordinal)
                || name.StartsWith("_TextureHeight", StringComparison.Ordinal) || name.StartsWith("_ScaleX", StringComparison.Ordinal)
                || name.StartsWith("_ScaleY", StringComparison.Ordinal) || name.StartsWith("_PerspectiveFilter", StringComparison.Ordinal)
                || name.StartsWith("_VertexOffset", StringComparison.Ordinal)) return "_EnableTMPCompatibility";
            if (name.StartsWith("_TextureLayer1", StringComparison.Ordinal)) return "_EnableTextureLayer1";
            if (name.StartsWith("_TextureLayer2", StringComparison.Ordinal)) return "_EnableTextureLayer2";
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
            if (name.StartsWith("_Sharpen", StringComparison.Ordinal)) return "_EnableSharpen";
            if (name == "_NormalMap" || name == "_NormalScale") return "_UseNormalMap";
            if (name == "_MetallicMap" || name == "_SmoothnessMapChannel") return "_UseMetallicMap";
            if (name == "_EmissionMap" || name == "_EmissionColor" || name == "_EmissionUseAlpha") return "_UseEmission";
            if (name == "_NoiseTex" || name == "_NoiseScale" || name == "_NoiseSpeed" || name == "_DistortionStrength"
                || (name == "_DistortionDirection" && shaderName == "ES/2D/Composite URP")) return "_EnableDistortion";
            if (name == "_Cutoff") return "_AlphaClip";
            if (name == "_OcclusionMap" || name == "_Occlusion") return "_UseOcclusionMap";
            if (name.StartsWith("_Sequence", StringComparison.Ordinal)) return "_AnimationMode";
            if ((shaderName == "ES/2D/Composite URP" || shaderName == "ES/UI/Composite URP")
                && ((name.StartsWith("_Fade", StringComparison.Ordinal) && name != "_FadeMode")
                    || name.StartsWith("_DissolveEdge", StringComparison.Ordinal))) return "_FadeMode";
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
            return ((property.flags & MaterialProperty.PropFlags.HideInInspector) != 0
                    && !ESNativeStatusContractProperties.Contains(name))
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
                if (name == "_MainTex" || name == "_Color" || name == "_VertexColorStrength"
                    || name == "_MaskTex" || name == "_NormalMap" || name == "_NormalScale") return "基础输入";
                if (name == "_CoordinateMode"
                    || name == "_TimeMode"
                    || name == "_CustomTime"
                    || name == "_TimeScale"
                    || name == "_EnableTimeFPS" || name == "_TimeFPS"
                    || name == "_EnableTimeFrequency" || name == "_TimeFrequency" || name == "_TimeRange"
                    || name == "_MainTexScaleOffset"
                    || name == "_TilingMode"
                    || name.StartsWith("_WorldTiling", StringComparison.Ordinal)
                    || name.StartsWith("_ScreenTiling", StringComparison.Ordinal)
                    || name.StartsWith("_UV", StringComparison.Ordinal)
                    || name == "_AnimationMode"
                    || name.StartsWith("_Sequence", StringComparison.Ordinal)
                    || name == "_EnableSqueeze" || name.StartsWith("_Squeeze", StringComparison.Ordinal)
                    || name == "_EnableSineRotate" || name.StartsWith("_SineRotate", StringComparison.Ordinal)) return "时间与坐标";
                if (name.StartsWith("_Wind", StringComparison.Ordinal) || name == "_EnableWind"
                    || name.StartsWith("_Squish", StringComparison.Ordinal) || name == "_EnableSquish"
                    || name.StartsWith("_Wiggle", StringComparison.Ordinal) || name == "_EnableWiggle"
                    || name.StartsWith("_Vibrate", StringComparison.Ordinal) || name == "_EnableVibrate"
                    || name == "_EnableSineMove" || name.StartsWith("_SineMove", StringComparison.Ordinal)
                    || name == "_EnableSineScale" || name.StartsWith("_SineScale", StringComparison.Ordinal)) return "顶点形变";
                if (name.StartsWith("_Fade", StringComparison.Ordinal) || name.StartsWith("_Dissolve", StringComparison.Ordinal)
                    || name == "_EnableCustomFade" || name.StartsWith("_CustomFade", StringComparison.Ordinal)
                    || name == "_EnableFullGlowDissolve" || name.StartsWith("_FullGlowDissolve", StringComparison.Ordinal)) return "遮罩与溶解";
                if (name.StartsWith("_Pixelate", StringComparison.Ordinal) || name == "_EnablePixelate"
                    || name == "_EnableSmoothPixelArt" || name.StartsWith("_SmoothPixel", StringComparison.Ordinal)
                    || name == "_EnableCheckerboard" || name.StartsWith("_Checkerboard", StringComparison.Ordinal)
                    || name == "_EnableFlame" || name.StartsWith("_Flame", StringComparison.Ordinal)
                    || name == "_EnableSmoke" || name.StartsWith("_Smoke", StringComparison.Ordinal)
                    || name == "_EnableCamouflage" || name.StartsWith("_Camouflage", StringComparison.Ordinal)
                    || name == "_EnableMetal" || name.StartsWith("_Metal", StringComparison.Ordinal)
                    || name == "_UberNoiseTexture"
                    || name.StartsWith("_Palette", StringComparison.Ordinal) || name == "_EnablePalette"
                    || name.StartsWith("_Halftone", StringComparison.Ordinal) || name == "_EnableHalftone"
                    || name.StartsWith("_TextureLayer1", StringComparison.Ordinal) || name.StartsWith("_TextureLayer2", StringComparison.Ordinal)) return "风格化";
                if (name.IndexOf("Outline", StringComparison.Ordinal) >= 0) return "轮廓";
                if (name == "_EnableShadow" || name.StartsWith("_Shadow", StringComparison.Ordinal)) return "轮廓";
                if (name.StartsWith("_EnableFrozen", StringComparison.Ordinal) || name.StartsWith("_Frozen", StringComparison.Ordinal)
                    || name.StartsWith("_EnableBurn", StringComparison.Ordinal) || name.StartsWith("_Burn", StringComparison.Ordinal)
                    || name.StartsWith("_EnablePoison", StringComparison.Ordinal) || name.StartsWith("_Poison", StringComparison.Ordinal)) return "状态表现";
                if (name == "_AlphaClip" || name == "_Cutoff" || name == "_QualityTier" || name == "_BlendMode") return "输出控制";
                if (name.StartsWith("_EnableShine", StringComparison.Ordinal) || name.StartsWith("_Shine", StringComparison.Ordinal)
                    || name.StartsWith("_EnablePingPongGlow", StringComparison.Ordinal) || name.StartsWith("_Glow", StringComparison.Ordinal)
                    || name == "_EnableInkSpread" || name.StartsWith("_InkSpread", StringComparison.Ordinal)
                    || name == "_EnableShiftHue" || name.StartsWith("_ShiftHue", StringComparison.Ordinal)
                    || name == "_EnableAddHue" || name.StartsWith("_AddHue", StringComparison.Ordinal)
                    || name == "_EnableSineGlow" || name.StartsWith("_SineGlow", StringComparison.Ordinal)
                    || name == "_EnableEnchanted" || name.StartsWith("_Enchanted", StringComparison.Ordinal)
                    || name == "_EnableShifting" || name.StartsWith("_Shifting", StringComparison.Ordinal)
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
                if (name == "_TimeMode" || name == "_CustomTime" || name == "_TimeScale"
                    || name == "_EnableTimeFPS" || name == "_TimeFPS"
                    || name == "_EnableTimeFrequency" || name == "_TimeFrequency" || name == "_TimeRange"
                    || name == "_MainTexScaleOffset") return "时间与坐标";
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
                    || name == "_ESNativeStatusContract"
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
                if (name == "_EnableSDF" || name.StartsWith("_SDF", StringComparison.Ordinal)) return "SDF 字体";
                if (name == "_EnableTMPCompatibility" || name == "_FaceColor" || name == "_FaceDilate"
                    || name == "_OutlineColor" || name == "_OutlineWidth" || name == "_OutlineSoftness"
                    || name == "_EnableUnderlay" || name.StartsWith("_Underlay", StringComparison.Ordinal)
                    || name == "_WeightNormal" || name == "_WeightBold" || name.StartsWith("_ScaleRatio", StringComparison.Ordinal)
                    || name == "_GradientScale" || name == "_Sharpness" || name.StartsWith("_TextureWidth", StringComparison.Ordinal)
                    || name.StartsWith("_TextureHeight", StringComparison.Ordinal) || name.StartsWith("_ScaleX", StringComparison.Ordinal)
                    || name.StartsWith("_ScaleY", StringComparison.Ordinal) || name.StartsWith("_PerspectiveFilter", StringComparison.Ordinal)
                    || name.StartsWith("_VertexOffset", StringComparison.Ordinal)) return "SDF 字体";
                if (name == "_EnableRecolorRGB" || name == "_EnableRecolorRGBYCP"
                    || name.StartsWith("_Recolor", StringComparison.Ordinal)
                    || name == "_EnableSplitToning" || name.StartsWith("_SplitTone", StringComparison.Ordinal)
                    || name == "_EnableBlackTint" || name.StartsWith("_BlackTint", StringComparison.Ordinal)
                    || name == "_EnableShiftHue" || name.StartsWith("_ShiftHue", StringComparison.Ordinal)
                    || name == "_EnableAddHue" || name.StartsWith("_AddHue", StringComparison.Ordinal)
                    || name == "_EnableSineGlow" || name.StartsWith("_SineGlow", StringComparison.Ordinal)) return "色彩调整";
                if (name.StartsWith("_Pixelate", StringComparison.Ordinal) || name == "_EnablePixelate"
                    || name == "_EnableSmoothPixelArt" || name.StartsWith("_SmoothPixel", StringComparison.Ordinal)
                    || name == "_EnableCheckerboard" || name.StartsWith("_Checkerboard", StringComparison.Ordinal)
                    || name == "_EnableFlame" || name.StartsWith("_Flame", StringComparison.Ordinal)
                    || name == "_EnableSmoke" || name.StartsWith("_Smoke", StringComparison.Ordinal)
                    || name == "_EnableCamouflage" || name.StartsWith("_Camouflage", StringComparison.Ordinal)
                    || name == "_EnableMetal" || name.StartsWith("_Metal", StringComparison.Ordinal)
                    || name == "_UberNoiseTexture"
                    || name.StartsWith("_Palette", StringComparison.Ordinal) || name == "_EnablePalette"
                    || name.StartsWith("_Halftone", StringComparison.Ordinal) || name == "_EnableHalftone"
                    || name.StartsWith("_TextureLayer1", StringComparison.Ordinal) || name.StartsWith("_TextureLayer2", StringComparison.Ordinal)) return "风格化";
                if (name == "_TimeMode" || name == "_CustomTime" || name == "_TimeScale"
                    || name == "_EnableTimeFPS" || name == "_TimeFPS"
                    || name == "_EnableTimeFrequency" || name == "_TimeFrequency" || name == "_TimeRange"
                    || name == "_MainTexScaleOffset"
                    || name == "_TilingMode" || name.StartsWith("_WorldTiling", StringComparison.Ordinal)
                    || name.StartsWith("_ScreenTiling", StringComparison.Ordinal)
                    || name.StartsWith("_UV", StringComparison.Ordinal)
                    || name == "_EnableSqueeze" || name.StartsWith("_Squeeze", StringComparison.Ordinal)
                    || name == "_EnableSineRotate" || name.StartsWith("_SineRotate", StringComparison.Ordinal)) return "时间与坐标";
                if (name.StartsWith("_Wind", StringComparison.Ordinal) || name == "_EnableWind"
                    || name.StartsWith("_Squish", StringComparison.Ordinal) || name == "_EnableSquish"
                    || name.StartsWith("_Wiggle", StringComparison.Ordinal) || name == "_EnableWiggle"
                    || name.StartsWith("_Vibrate", StringComparison.Ordinal) || name == "_EnableVibrate"
                    || name == "_EnableSineMove" || name.StartsWith("_SineMove", StringComparison.Ordinal)
                    || name == "_EnableSineScale" || name.StartsWith("_SineScale", StringComparison.Ordinal)) return "顶点形变";
                if (name == "_EnableShadow" || name.StartsWith("_Shadow", StringComparison.Ordinal)) return "轮廓";
                if (name == "_EnableInkSpread" || name.StartsWith("_InkSpread", StringComparison.Ordinal)
                    || name == "_EnableEnchanted" || name.StartsWith("_Enchanted", StringComparison.Ordinal)
                    || name == "_EnableShifting" || name.StartsWith("_Shifting", StringComparison.Ordinal)) return "动态表现";
                if (name == "_AlphaClip"
                    || name == "_Cutoff"
                    || name == "_QualityTier"
                    || name.StartsWith("_Fade", StringComparison.Ordinal)
                    || name.StartsWith("_DissolveEdge", StringComparison.Ordinal)
                    || name == "_EnableCustomFade" || name.StartsWith("_CustomFade", StringComparison.Ordinal)
                    || name == "_EnableFullGlowDissolve" || name.StartsWith("_FullGlowDissolve", StringComparison.Ordinal)
                    || name.StartsWith("_Stencil", StringComparison.Ordinal)
                    || name == "_ColorMask"
                    || name == "_UseUIAlphaClip") return "遮罩与输出";
                if (name == "_BlendMode") return "渲染状态";
                return "动态表现";
            }

            if (name == "_BaseMap"
                || name == "_BaseColor"
                || name == "_UseNormalMap"
                || name == "_NormalMap"
                || name == "_NormalScale"
                || name == "_UseMetallicMap"
                || name == "_MetallicMap"
                || name == "_SmoothnessMapChannel"
                || name == "_Metallic"
                || name == "_Smoothness") return "基础材质";
            if (name == "_TimeMode"
                || name == "_CustomTime"
                || name == "_TimeScale"
                || name == "_EnableTimeFPS" || name == "_TimeFPS"
                || name == "_EnableTimeFrequency" || name == "_TimeFrequency" || name == "_TimeRange"
                || name == "_MainTexScaleOffset"
                || name == "_TilingMode"
                || name.StartsWith("_WorldTiling", StringComparison.Ordinal)
                || name.StartsWith("_ScreenTiling", StringComparison.Ordinal)
                || name.StartsWith("_UV", StringComparison.Ordinal)
                || name == "_EnableSqueeze" || name.StartsWith("_Squeeze", StringComparison.Ordinal)
                || name == "_EnableSineRotate" || name.StartsWith("_SineRotate", StringComparison.Ordinal)
                || name.StartsWith("_VertexAnimation", StringComparison.Ordinal)
                || name == "_EnableVertexAnimation") return "时间与形变";
            if (name.StartsWith("_Wind", StringComparison.Ordinal) || name == "_EnableWind"
                || name.StartsWith("_Squish", StringComparison.Ordinal) || name == "_EnableSquish"
                || name.StartsWith("_Wiggle", StringComparison.Ordinal) || name == "_EnableWiggle"
                || name.StartsWith("_Vibrate", StringComparison.Ordinal) || name == "_EnableVibrate"
                || name == "_EnableSineMove" || name.StartsWith("_SineMove", StringComparison.Ordinal)
                || name == "_EnableSineScale" || name.StartsWith("_SineScale", StringComparison.Ordinal)) return "时间与形变";
            if (name == "_Occlusion"
                || name == "_UseOcclusionMap"
                || name == "_OcclusionMap"
                || name == "_UseEmission"
                || name == "_EmissionColor"
                || name == "_EmissionMap"
                || name == "_EmissionUseAlpha"
                || name == "_ReceiveShadows") return "光照输入";
            if (name == "_EnableTextureLayer1" || name.StartsWith("_TextureLayer1", StringComparison.Ordinal)
                || name == "_EnableTextureLayer2" || name.StartsWith("_TextureLayer2", StringComparison.Ordinal)
                || name == "_EnableSmoothPixelArt" || name.StartsWith("_SmoothPixel", StringComparison.Ordinal)
                || name == "_EnablePixelate" || name.StartsWith("_Pixelate", StringComparison.Ordinal)
                || name == "_EnableCheckerboard" || name.StartsWith("_Checkerboard", StringComparison.Ordinal)
                || name == "_EnableHalftone" || name.StartsWith("_Halftone", StringComparison.Ordinal)) return "风格化";
            if (name == "_EnableInnerOutline" || name.StartsWith("_InnerOutline", StringComparison.Ordinal)
                || name == "_EnableOuterOutline" || name.StartsWith("_OuterOutline", StringComparison.Ordinal)
                || name == "_EnablePixelOutline" || name.StartsWith("_PixelOutline", StringComparison.Ordinal)
                || name == "_EnableShadow" || name.StartsWith("_Shadow", StringComparison.Ordinal)) return "轮廓与阴影";
            if (name.StartsWith("_Dissolve", StringComparison.Ordinal) || name.StartsWith("_Noise", StringComparison.Ordinal)
                || name.StartsWith("_Fade", StringComparison.Ordinal)
                || name == "_EnableCustomFade" || name.StartsWith("_CustomFade", StringComparison.Ordinal)
                || name == "_EnableFullAlphaDissolve" || name.StartsWith("_FullAlphaDissolve", StringComparison.Ordinal)
                || name == "_EnableSourceAlphaDissolve" || name.StartsWith("_SourceAlphaDissolve", StringComparison.Ordinal)
                || name == "_EnableSourceGlowDissolve" || name.StartsWith("_SourceGlowDissolve", StringComparison.Ordinal)
                || name == "_EnableDirectionalAlphaFade" || name.StartsWith("_DirectionalAlphaFade", StringComparison.Ordinal)
                || name == "_EnableDirectionalGlowFade" || name.StartsWith("_DirectionalGlowFade", StringComparison.Ordinal)
                || name == "_EnableDirectionalDistortion" || name.StartsWith("_DirectionalDistortion", StringComparison.Ordinal)
                || name == "_EnableFullGlowDissolve" || name.StartsWith("_FullGlowDissolve", StringComparison.Ordinal)) return "遮罩与溶解";
            if (name == "_Surface" || name == "_AlphaClip" || name == "_Cutoff" || name == "_Cull" || name == "_QueueOffset" || name == "_QualityTier" || name == "_ResourceProfile") return "输出与质量";
            return "动态表现";
        }
        private static string PropertyHint(string name, string shaderName)
        {
            if (name == "_MainTex" && shaderName == "ES/UI/Composite URP") return "由 RawImage.texture 或 Image.sprite 提供；CanvasRenderer 会覆盖材质中的主纹理。";
            if (name == "_EnableSDF") return "读取主纹理 Alpha 作为有符号距离场；适用于 Image/RawImage 的 SDF 图集，不等同于 TMP Shader 合同。";
            if (name == "_EnableTMPCompatibility") return "读取 TMP 的 TEXCOORD1 字重/缩放信息，并兼容 Face、Outline、Underlay 属性；需使用 TMP 合同材质。";
            if (name == "_EnableUnderlay") return "兼容 TMP Underlay；也会响应 TMP 的 UNDERLAY_ON 或 UNDERLAY_INNER 关键词。";
            if (name.StartsWith("_SDF", StringComparison.Ordinal)) return "仅在启用 SDF 字体时生效。";
            if (name == "_PaletteTex") return "横向表示输入明度到目标颜色的映射；建议 Clamp、无 Mipmap，按需要选择 Point 或 Bilinear。";
            if (name == "_EnablePalette") return "按当前颜色明度采样调色板纹理；Standard 及以上质量生效。";
            if (name == "_EnableHalftone") return "在局部 UV 中生成抗锯齿网点；Standard 及以上质量生效。";
            if (name == "_AddColorContrastToggle") return "按当前颜色的加权亮度调制叠加色，接近 ESNative 的 Contrast Toggle。";
            if (name == "_AddColorMaskToggle") return "按 _AddColorMask 的 RGB×Alpha 调制叠加色；遮罩使用当前 UV 与 ST，并复用主纹理采样状态。";
            if (name == "_StrongTintContrastToggle") return "按当前颜色的加权亮度调制强制染色，接近 ESNative 的 Contrast Toggle。";
            if (name == "_StrongTintMaskToggle") return "按 _StrongTintMask 的 RGB×Alpha 调制强制染色；遮罩使用当前 UV 与 ST，并复用主纹理采样状态。";
            if (name == "_HalftoneAlphaPattern") return "启用 ESNative 兼容的透明点阵；关闭时保持既有 ES RGB 半色调视觉。";
            if (name == "_HalftoneFade") return "透明点阵从中心向外扩散的半径，可超过 1 覆盖完整局部 UV。";
            if (name == "_HalftoneFadeWidth") return "透明点阵的径向过渡宽度；运行时最小按 0.01 处理。";
            if (name == "_EnablePixelate") return "在 Sprite 局部 UV 中量化采样，旋转或 Tight Atlas 下仍保持单 Sprite 边界。";
            if (name == "_TilingMode") return shaderName == "ES/3D/Lit Composite URP"
                ? "WorldXZ 使用世界 XZ 平面投影，Screen 使用当前相机像素空间；垂直面与多相机结果必须单独复核。"
                : "世界/屏幕模式会在当前 Sprite 或 UI 子纹理内重复主纹理；单一模式避免两种空间同时启用。";
            if (name == "_WorldTilingPixelsPerUnit") return shaderName == "ES/3D/Lit Composite URP"
                ? "表示世界 XZ 平面每单位的重复次数；不是三平面映射，垂直表面可能拉伸。"
                : "表示每个世界单位重复主纹理的次数；不同对象可共享连续的世界坐标图样。";
            if (name == "_ScreenTilingPixelsPerUnit") return "表示主纹理每次重复覆盖的屏幕像素尺寸；分辨率变化时保持像素尺度语义。";
            if (name == "_EnableSmoothPixelArt") return "使用屏幕导数重建像素边缘，不增加纹理采样；与模糊同时启用时视觉目标冲突。";
            if (name == "_EnableCheckerboard") return "按连续世界坐标生成交错暗格，只修改 RGB，不改变原始 Alpha。";
            if (name == "_UberNoiseTexture") return "Flame、Smoke、Ink Spread、Camouflage、Metal、Enchanted、ESNative Fade、Custom Fade 与 Full Glow Dissolve 共用的灰度噪声；建议 Repeat、Bilinear。";
            if (name == "_EnableFullDistortion") return "使用共享噪声的两次独立采样分别驱动 XY，兼容 ESNative Full Distortion。";
            if (name == "_DistortionDirection") return "XY 分别缩放噪声造成的 UV 位移；(1,0) 仅水平，(0,1) 仅垂直，(1,1) 保持旧材质的对角线结果。";
            if (name == "_EnableDirectionalDistortion") return "按 ESNative 合同先位移 UV，再将同一方向可见度乘入 Alpha；可与其他 ESNative Fade 叠加。";
            if (name == "_EnableFullAlphaDissolve"
                || name == "_EnableSourceAlphaDissolve"
                || name == "_EnableSourceGlowDissolve"
                || name == "_EnableDirectionalAlphaFade"
                || name == "_EnableDirectionalGlowFade")
                return "保留 ESNative 同名参数，可与其他 ESNative Fade 同时启用并按固定顺序执行。";
            if (name == "_EnableFlame") return "按 ESNative 合同使用局部 UV 中心 (0.5, 0.4) 生成火焰；通常搭配空白白色主纹理。";
            if (name == "_EnableSmoke") return "按局部 UV 生成径向烟雾；启用顶点色种子后使用顶点 R 通道错开粒子图样。";
            if (name == "_SmokeVertexSeed") return "粒子系统需提供可变化的顶点红色；普通 Sprite 顶点色一致时不会产生逐粒子差异。";
            if (name == "_EnableWind" || name == "_EnableSquish" || name == "_EnableWiggle" || name == "_EnableVibrate"
                || name == "_EnableSineMove" || name == "_EnableSineScale")
                return shaderName == "ES/3D/Lit Composite URP"
                    ? "3D Lit 在对象 XY 平面形变并同步 Shadow、Depth 与 Scene Pass；法线仍来自原始网格，大幅位移需复核光照与 Bounds。"
                    : "顶点形变依赖网格密度；四顶点矩形只能表现整体边缘运动。Renderer 可添加 ESCompositeVertexMotionBounds 防止位移后被原始 Bounds 剔除。";
            if (name == "_WindAnchor") return "沿锚定方向固定低于边界的顶点；0 固定方向起点边缘，1 会固定全部顶点。";
            if (name == "_WindAnchorDirection") return "定义风摆固定边界在局部 UV 中的增长方向；(0,1) 保持旧版从底边向顶边的行为。";
            if (name == "_WindGlobalInfluence") return "场景存在 ESCompositeGlobalWind 时混合全局方向、强度和速度。";
            if (name == "_WiggleDirection") return "定义摇摆波相位沿局部 UV 的传播方向；(0,1) 保持旧版纵向传播。";
            if (name == "_GlitchScanDirection") return shaderName == "ES/3D/VFX Composite URP"
                ? "定义轻量故障条带在世界空间中的扫描方向；ESNative 精确合同仍由故障噪声缩放与速度控制。"
                : "定义轻量故障条带在局部坐标中的扫描方向；(0,1) 保持旧版横向条带。";
            if (name == "_RainbowDirection") return "定义兼容线性彩虹色带在局部坐标中的增长方向；ESNative 精确合同的径向彩虹仍由彩虹中心控制。";
            if (name == "_SquishFade") return "周期挤压的强度乘数；迁移 ESNative 时保留同名参数，运行时交互挤压使用独立 MPB 通道。";
            if (name == "_QualityTier") return "基础/标准/高质量会同步控制 ES 关键词。";
            if (name == "_ResourceProfile") return "动态完整允许 MaterialPropertyBlock 随时切换效果；材质优化仅编译当前材质已启用效果需要的资源组，适合静态组合和移动端预算。";
            if (name == "_EnableVertexAnimation") return shaderName == "ES/3D/Lit Composite URP" ? "Lit 会同步主画面、阴影和深度顶点位置。" : null;
            if (name == "_EnableFlowMap") return "RG 通道以 0.5 为静止方向。";
            if (name == "_EnableSoftParticles") return "URP Asset 或 Camera 必须提供 Depth Texture。";
            if (name == "_EnableDepthIntersection") return "与软粒子共用场景深度采样，URP 必须提供 Depth Texture。";
            if (name == "_EnableChromatic") return "会增加两次主纹理采样。";
            if (name == "_BlurMode") return "Light5Tap 使用 5 次邻域采样；Gaussian3x3 使用 9 次采样，边缘更平滑但成本更高。";
            if (name == "_EnableSharpen") return "使用中心与四方向邻域提取细节，保持中心 Alpha；与模糊同时启用时先锐化再模糊。";
            if (name == "_SharpenThreshold") return "抑制低于阈值的细节增强，可减少平坦区域噪声和压缩纹理伪影。";
            if ((name == "_TextureLayer1Texture" || name == "_TextureLayer2Texture")
                && shaderName == "ES/3D/Lit Composite URP")
                return "与 BaseMap 共用采样器状态以控制 SM3.0 采样器数量；Wrap 与 Filter 以 BaseMap 为准。";
            if ((name == "_EnableInnerOutline" || name == "_EnableOuterOutline" || name == "_EnablePixelOutline")
                && shaderName == "ES/3D/Lit Composite URP")
                return "依据 BaseMap Alpha 在当前网格覆盖范围内生成；网格边界没有透明留白时，外侧部分会被几何范围裁掉。";
            if (name == "_EnableHologram" && shaderName == "ES/3D/Lit Composite URP")
                return "使用局部 UV 扫描线并同步 Alpha Pass；与 ESNative 的世界高度/正交相机合同不是无损等价。";
            if (name == "_EnableGlitch" && shaderName == "ES/3D/Lit Composite URP")
                return "随机横向偏移共享表面 UV，主画面、阴影、深度和场景选择使用同一坐标。";
            if (name == "_EnableVertexStreams") return "ParticleSystem Renderer 顶点流需提供 Custom1.xyzw 和 Custom2.x；各通道仅作为增量。";
            if (name == "_Surface" || name == "_BlendMode" || name == "_ZWriteMode" || name == "_ZTest" || name == "_Cull" || name == "_QueueOffset") return "材质级渲染状态；MaterialPropertyBlock 无法覆盖。";
            if (name == "_TimeMode") return "场景时间受 Time.timeScale 影响；非缩放时间由 ES 运行时驱动；自定义时间由调用方写入。";
            if (name == "_CustomTime") return "选择自定义时间后生效；建议通过 MaterialPropertyBlock 或 ESCompositeURPProperties.SetTime 写入。";
            if (name == "_TimeScale") return "统一乘在当前时间源上；负值可倒放，各效果自身的速度参数仍独立生效。";
            if (name == "_EnableTimeFPS") return "在时间倍率之后按指定帧率量化，适合定格和低帧率动画。";
            if (name == "_TimeFPS") return "每秒时间采样次数；运行时会使用绝对值并限制到 0.01～240。";
            if (name == "_EnableTimeFrequency") return "在帧率量化之后将时间转换为周期正弦值。";
            if (name == "_TimeFrequency") return "控制周期时间的振荡频率，可使用负值反转相位方向。";
            if (name == "_TimeRange") return "控制周期时间围绕稳定偏移的振幅，可使用负值翻转波形。";
            if (name == "_MainTexScaleOffset") return "X/Y 为缩放，Z/W 为偏移；支持 MaterialPropertyBlock 对单个对象覆盖。";
            if (name == "_EnableUVTransform") return "在局部 UV 中执行旋转、缩放和偏移；中心默认为 Sprite/UI 的几何中心。";
            if (name == "_EnableUVDistort") return "使用无状态正弦场扰动采样坐标；不增加 Shader 变体，但会增加数学运算。";
            if (name == "_UVDistortNoiseTex") return "可选灰度噪声驱动 From/To 偏移；默认灰色与对称偏移组合为零位移。";
            if (name == "_EnableSplitToning") return "按亮度分别乘以阴影和高光色调；默认白色不会改变原色。";
            if (name == "_EnableBlackTint") return "只影响暗部；幂次越高，染色越集中在接近黑色的区域。";
            if (name == "_EnableInkSpread") return "使用当前坐标、扩散中心和共享噪声生成推进边界；可动画化距离。";
            if (name == "_InkSpreadDistance") return "沿坐标空间推进扩散边界；中心在可见区域外时可能需要大于 1 的距离。";
            if (name == "_EnableShiftHue") return "持续旋转原色相；速度使用当前统一时间源并允许负值反向。";
            if (name == "_EnableAddHue") return "依据原图明度叠加动态色相，不替换原颜色；HDR 亮度可超过 1。";
            if (name == "_AddHueMask") return "红通道与 Alpha 相乘作为叠加强度；纹理缩放/偏移可独立调整。";
            if (name == "_EnableSineGlow") return "按 ESNative 波形在最小值和扩展峰值间周期叠加辉光；频率使用当前统一时间源。";
            if (name == "_SineGlowMask") return "RGB 与 Alpha 共同调制辉光颜色；白色遮罩保持完整效果。";
            if (name == "_EnableShadow") return shaderName == "ES/3D/Lit Composite URP"
                ? "增加一次偏移 BaseMap Alpha 采样并在主体后方合成；仅能出现在当前网格覆盖范围内。"
                : "增加一次偏移主纹理采样并在主体后方合成；Sprite 建议使用 Full Rect 网格。";
            if (name == "_ShadowOffset") return "沿 Sprite/UI 局部纹理像素尺寸换算偏移，正负值决定投影方向。";
            if (name == "_EnableCustomFade") return "使用顶点 Alpha 作为主驱动；与普通 Fade 可相乘，但建议由同一动画系统统一控制。";
            if (name == "_CustomFadeFadeMask") return "红通道参与非线性透明度；共享噪声只在效果启用时额外采样。";
            if (name == "_EnableFullGlowDissolve") return "硬阈值溶解会同步影响精灵阴影，避免主体消失后残留投影。";
            if (name == "_EnableCamouflage") return "启用动画时增加一次扰动噪声采样；静态迷彩使用两次共享噪声采样。";
            if (name == "_EnableMetal") return "固定读取两次共享噪声；启用金属遮罩时再增加一次遮罩采样。";
            if (name == "_EnableEnchanted") return "读取两次反向滚动共享噪声；替换模式可能显著改变原图明度。";
            if (name == "_EnableShifting") return "不增加纹理采样；负速度可反向播放，循环相位会保持在 0 到 1。";
            if (name == "_FadeMode") return "进度 0 完全可见、1 完全消失；方向、遮罩、全纹理和源点溶解模式共享同一动画合同。";
            if (name == "_FadePosition") return "方向模式作为渐隐起点，源点溶解模式作为径向中心。";
            if (name == "_FadeNoiseTex") return "建议使用 Repeat、线性采样的灰度噪声；仅在渐隐模式启用时采样。";
            if (name == "_ReceiveShadows") return "关闭后同步 _RECEIVE_SHADOWS_OFF，材质不再应用主光实时阴影。";
            if (name == "_Surface") return "透明模式会切换 Blend/ZWrite/RenderType/Queue，并关闭 GBuffer、阴影和深度 Pass；裁剪模式由不透明表面加透明裁剪组成。";
            if (name == "_UseUIAlphaClip") return "材质关键词；必须写入 UI 独立材质，MaterialPropertyBlock 无法切换。";
            if (name == "_UseNormalMap") return "关闭时跳过法线纹理采样；开启后才显示纹理和强度。";
            if (name == "_UseMetallicMap") return "开启后使用纹理 R 通道乘金属度，并按所选通道调制光滑度。";
            if (name == "_MetallicMap") return "R 通道固定为金属度；光滑度可选择 R 或 A，纹理缩放/偏移可独立调整。";
            if (name == "_SmoothnessMapChannel") return "常规打包纹理选择 Alpha；迁移 ESNative 金属贴图时选择 Red。";
            if (name == "_UseEmission") return "关闭时跳过自发光纹理采样；开启后才显示颜色和纹理。";
            if (name == "_EmissionUseAlpha") return "开启后自发光 RGB 额外乘贴图 Alpha，用于兼容 ESNative 自发光贴图合同。";
            if (name == "_NormalMap") return "纹理导入类型应为 Normal map。";
            if (name == "_MaskTex") return "RGBA 通道对应 Renderer2D Blend Style 的遮罩过滤。";
            if (name == "_NoiseTex") return "建议使用 Repeat 包裹和线性过滤。";
            if (name == "_AlphaClip") return "透明度低于阈值的像素会被丢弃。";
            int minimumQuality = GetMinimumQualityTier(shaderName, name);
            if (minimumQuality > 0) return "需要“" + QualityName(minimumQuality) + "”质量档。";
            return null;
        }

        private static int GetMinimumQualityTier(string shaderName, string propertyName)
        {
            if (shaderName == "ES/2D/Composite URP")
            {
                if (propertyName == "_EnableBlur" || propertyName == "_EnableSharpen" || propertyName == "_EnableSparkle"
                    || propertyName == "_EnableHologram" || propertyName == "_EnableGlitch") return 2;
                if (propertyName == "_EnableChromatic" || propertyName == "_EnablePalette"
                    || propertyName == "_EnableHalftone") return 1;
                return 0;
            }

            if (shaderName == "ES/3D/Lit Composite URP")
            {
                if (propertyName == "_EnableShine" || propertyName == "_EnableSparkle"
                    || propertyName == "_EnableBurn" || propertyName == "_EnableSharpen"
                    || propertyName == "_EnableHologram" || propertyName == "_EnableGlitch"
                    || propertyName == "_EnableInnerOutline" || propertyName == "_EnableOuterOutline"
                    || propertyName == "_EnablePixelOutline") return 2;
                if (propertyName == "_EnableVertexAnimation" || propertyName == "_EnableFlowMap"
                    || propertyName == "_EnableUVDistort"
                    || propertyName == "_DissolveMode" || propertyName == "_EnableRim"
                    || propertyName == "_EnableSmoothPixelArt" || propertyName == "_EnablePixelate"
                    || propertyName == "_EnableCheckerboard" || propertyName == "_EnableFlame"
                    || propertyName == "_EnableSmoke" || propertyName == "_EnableHalftone"
                    || propertyName == "_EnableTextureLayer1" || propertyName == "_EnableTextureLayer2"
                    || propertyName == "_EnableShadow" || propertyName == "_EnableFullGlowDissolve") return 1;
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

            if (shaderName == "ES/UI/Composite URP")
            {
                if (propertyName == "_EnableBlur" || propertyName == "_EnableSharpen" || propertyName == "_EnableSparkle") return 2;
                if (propertyName == "_EnableChromatic" || propertyName == "_EnableHologram"
                    || propertyName == "_EnableGlitch" || propertyName == "_EnablePalette"
                    || propertyName == "_EnableHalftone") return 1;
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
