using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    internal static partial class ESCompositeCodingHelper
    {
        #region Help Catalog

        private static readonly Dictionary<string, string> CommonPropertyDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "_AddColor", "设置直接叠加到原颜色上的 HDR 颜色。" },
            { "_AddColorFade", "控制叠加颜色的混合强度，0 不叠加，1 完全按设定值叠加。" },
            { "_AlphaClip", "决定是否按裁剪阈值丢弃低透明度像素。" },
            { "_AlphaTint", "设置透明染色使用的目标颜色。" },
            { "_AlphaTintMin", "设置透明染色参与计算的最低透明度基准。" },
            { "_AlphaTintFade", "控制透明染色对低透明度像素的最大混合强度。" },
            { "_AddHueFade", "控制动态色相叠加的整体强度。" },
            { "_AddHueSpeed", "控制动态色相沿色环变化的速度，可使用负值反向。" },
            { "_AddHueBrightness", "控制生成色相的 HDR 亮度。" },
            { "_AddHueSaturation", "控制生成色相的饱和度。" },
            { "_AddHueContrast", "控制原图亮度对色相叠加强度的影响曲线。" },
            { "_AddHueMaskToggle", "决定是否读取动态色相遮罩；关闭时跳过遮罩采样。" },
            { "_AddHueMask", "使用红通道与 Alpha 的乘积限制动态色相叠加区域。" },
            { "_BlackTintFade", "控制暗部染色与原颜色的混合比例。" },
            { "_BlackTintColor", "设置叠加到原图暗部的 HDR 颜色。" },
            { "_BlackTintPower", "控制暗部染色向最暗区域集中的程度。" },
            { "_BurnEdgeColor", "设置燃烧或溶解交界处的 HDR 边缘颜色。" },
            { "_BurnInsideColor", "设置 2D 燃烧区域内部的颜色。" },
            { "_BurnProgress", "推进燃烧边界在噪声场中的位置。" },
            { "_BurnWidth", "控制燃烧高亮边缘的过渡宽度。" },
            { "_ChromaticAngle", "设置红蓝通道发生偏移的 UV 方向角。" },
            { "_ChromaticEdgeOnly", "控制色差是否集中在纹理边缘；0 为全图等量，1 为边缘增强。" },
            { "_ChromaticIntensity", "控制原颜色与 RGB 分离结果的混合比例。" },
            { "_ChromaticOffset", "设置红蓝通道相对原始 UV 的最大偏移距离。" },
            { "_Color", "设置与主纹理及顶点色相乘的基础颜色和透明度。" },
            { "_CoordinateMode", "选择效果坐标使用模型 UV、世界 XZ 或屏幕坐标。" },
            { "_CustomTime", "在自定义时间模式下提供由业务代码控制的时间值。" },
            { "_Cutoff", "设置透明裁剪阈值；透明度低于该值的像素会被丢弃。" },
            { "_DissolveColor", "设置 VFX 溶解边缘叠加的 HDR 颜色。" },
            { "_DissolveEdgeColor", "设置 Lit 或 2D 溶解边界的 HDR 颜色。" },
            { "_DissolveEdgeWidth", "控制 Lit 或 2D 溶解边界的可见宽度。" },
            { "_DissolveSoftness", "控制 Lit 溶解透明过渡的柔和程度。" },
            { "_DissolveWidth", "控制 VFX 溶解透明过渡及边缘区域的宽度。" },
            { "_EmissionColor", "设置不受场景光照衰减的 HDR 自发光颜色。" },
            { "_EmissionMap", "提供逐像素自发光纹理，并与自发光颜色相乘。" },
            { "_EnableAlphaTint", "决定是否按当前透明度向指定颜色染色。" },
            { "_EnablePingPongGlow", "决定是否在两种 HDR 颜色之间循环往返发光。" },
            { "_FadeNoiseFactor", "控制渐隐遮罩向噪声形状混合的比例。" },
            { "_FadePosition", "设置方向渐隐的坐标中心。" },
            { "_FadeWidth", "控制渐隐从可见到透明的过渡宽度。" },
            { "_FlowSpeed", "Vector4 的 XY 设置主纹理 UV 每秒流动的方向和速度。" },
            { "_FlowStrength", "缩放纹理流动速度，0 保持静止，1 使用完整速度。" },
            { "_FrozenColor", "设置冰冻状态覆盖主体的颜色。" },
            { "_FrozenDensity", "控制冰晶高光在噪声场中的出现比例。" },
            { "_FrozenHighlight", "设置冰晶闪烁部分叠加的 HDR 高光颜色。" },
            { "_FrozenSpeed", "控制冰晶高光随时间闪烁的速度。" },
            { "_GlitchAmount", "设置故障效果造成的最大横向 UV 偏移。" },
            { "_GlitchSpeed", "控制故障图样切换或移动的时间速度。" },
            { "_GlitchScanDirection", "控制轻量故障条带的扫描轴；默认 (0,1,0) 保持横向条带。" },
            { "_WindAnchorDirection", "控制风摆锚定边界在局部 UV 中的增长方向。" },
            { "_WiggleDirection", "控制摇摆波相位在局部 UV 中的传播方向。" },
            { "_GlitchFade", "控制故障遮罩、色相变化和 UV 位移的整体参与度。" },
            { "_GlitchMaskMin", "设置故障遮罩的最低参与度，避免低噪声区域完全静止。" },
            { "_GlitchMaskScale", "设置共享噪声纹理用于故障遮罩时的 UV 缩放。" },
            { "_GlitchMaskSpeed", "设置故障遮罩噪声沿 XY 方向的滚动速度。" },
            { "_GlitchHueSpeed", "控制故障区域随时间旋转色相的速度。" },
            { "_GlitchBrightness", "控制故障着色结果的 HDR 亮度倍率。" },
            { "_GlitchNoiseScale", "设置颜色故障噪声的 UV 缩放。" },
            { "_GlitchNoiseSpeed", "设置颜色故障噪声的独立滚动速度。" },
            { "_GlitchDistortion", "设置故障 UV 位移的方向向量。" },
            { "_GlitchDistortionScale", "设置位移噪声的 UV 缩放。" },
            { "_GlitchDistortionSpeed", "设置位移噪声的独立滚动速度。" },
            { "_GlowFrequency", "控制往返发光每秒循环的角频率。" },
            { "_GlowFrom", "设置往返发光插值的起点 HDR 颜色。" },
            { "_GlowIntensity", "控制往返发光叠加到原颜色上的亮度。" },
            { "_GlowContrast", "控制往返发光随源颜色亮度变化的对比幂次。" },
            { "_GlowFade", "控制往返发光的整体淡入强度。" },
            { "_GlowTo", "设置往返发光插值的终点 HDR 颜色。" },
            { "_SplitToneContrast", "控制分离色调按亮度分区时的对比幂次。" },
            { "_SplitToneShift", "在分离色调前偏移源亮度分区位置。" },
            { "_HologramColor", "设置全息扫描线覆盖使用的 HDR 颜色。" },
            { "_HologramFrequency", "控制 VFX 或 UI 兼容全息扫描线沿所选方向的密度。" },
            { "_HologramGap", "控制 VFX 全息扫描线中不可见间隔的比例。" },
            { "_HologramLineFrequency", "控制精确全息扫描线沿所选方向的密度。" },
            { "_HologramLineGap", "控制精确全息扫描线中不可见间隔的比例。" },
            { "_HologramMinAlpha", "设置全息扫描线间隔区域保留的最低透明度。" },
            { "_HologramSpeed", "控制全息扫描线沿坐标移动的速度。" },
            { "_HologramFade", "控制全息颜色、扫描可见度和扰动的整体参与度。" },
            { "_HologramContrast", "控制扫描线明暗过渡的对比度。" },
            { "_HologramSpace", "精确合同时选择局部 UV 或世界投影作为扫描线坐标。" },
            { "_HologramDirection", "设置全息扫描投影方向；局部空间使用 XY，世界空间使用 XYZ，零向量回退为向上。" },
            { "_HologramDistortionOffset", "设置活跃扫描分段产生的最大 UV 偏移。" },
            { "_HologramDistortionDirection", "设置全息扰动在 UV 空间中的位移方向；零向量回退为水平向右。" },
            { "_HologramDistortionSpeed", "控制全息扰动分段随时间推进的速度。" },
            { "_HologramDistortionDensity", "控制发生全息横向扰动的扫描分段比例。" },
            { "_HologramDistortionScale", "控制全息扰动沿扫描坐标的分段密度。" },
            { "_InnerOutlineColor", "设置 2D 图形内部轮廓线的颜色。" },
            { "_InnerOutlineWidth", "设置内部轮廓采样相对纹理像素的宽度。" },
            { "_InnerOutlineFade", "控制内描边覆盖强度。" },
            { "_InnerOutlineDistortionToggle", "启用内描边采样坐标的噪声扰动。" },
            { "_InnerOutlineDistortionIntensity", "设置内描边扰动在 XY 方向的最大偏移。" },
            { "_InnerOutlineNoiseScale", "设置内描边扰动噪声的 UV 缩放。" },
            { "_InnerOutlineNoiseSpeed", "设置内描边扰动噪声的滚动速度。" },
            { "_InnerOutlineTextureToggle", "启用内描边独立纹理着色。" },
            { "_InnerOutlineTintTexture", "提供内描边使用的独立 RGB 着色纹理。" },
            { "_InnerOutlineTextureSpeed", "设置内描边着色纹理的滚动速度。" },
            { "_InnerOutlineOutlineOnlyToggle", "仅输出内描边区域的透明度。" },
            { "_OuterOutlineFade", "控制外描边覆盖强度。" },
            { "_OuterOutlineDistortionToggle", "启用外描边采样坐标的噪声扰动。" },
            { "_OuterOutlineDistortionIntensity", "设置外描边扰动在 XY 方向的最大偏移。" },
            { "_OuterOutlineNoiseScale", "设置外描边扰动噪声的 UV 缩放。" },
            { "_OuterOutlineNoiseSpeed", "设置外描边扰动噪声的滚动速度。" },
            { "_OuterOutlineTextureToggle", "启用外描边独立纹理着色。" },
            { "_OuterOutlineTintTexture", "提供外描边使用的独立 RGB 着色纹理。" },
            { "_OuterOutlineTextureSpeed", "设置外描边着色纹理的滚动速度。" },
            { "_OuterOutlineOutlineOnlyToggle", "仅输出外描边区域的透明度。" },
            { "_PixelOutlineFade", "控制像素描边覆盖强度。" },
            { "_PixelOutlineTextureToggle", "启用像素描边独立纹理着色。" },
            { "_PixelOutlineTintTexture", "提供像素描边使用的独立 RGB 着色纹理。" },
            { "_PixelOutlineTextureSpeed", "设置像素描边着色纹理的滚动速度。" },
            { "_PixelOutlineOutlineOnlyToggle", "仅输出像素描边区域的透明度。" },
            { "_InkSpreadFade", "控制墨水扩散颜色覆盖原图的强度。" },
            { "_InkSpreadColor", "设置扩散区域使用的 HDR 墨水颜色。" },
            { "_InkSpreadContrast", "控制原图亮度在墨水颜色中的对比曲线。" },
            { "_InkSpreadDistance", "推进墨水扩散边界相对中心的距离。" },
            { "_InkSpreadPosition", "设置墨水扩散在局部坐标中的中心。" },
            { "_InkSpreadWidth", "控制墨水扩散边界的过渡宽度。" },
            { "_InkSpreadNoiseScale", "设置共享噪声纹理在扩散坐标中的缩放。" },
            { "_InkSpreadNoiseFactor", "控制噪声对规则圆形扩散边界的扰动程度。" },
            { "_NegativeFade", "控制原颜色向负片颜色过渡的比例。" },
            { "_NoiseScale", "设置噪声采样坐标的缩放；Vector4 的后续分量可作为静态偏移。" },
            { "_NoiseSpeed", "Vector4 的 XY 控制噪声纹理随当前时间源移动的方向和速度。" },
            { "_NoiseTex", "提供扰动、溶解或状态效果使用的灰度噪声。" },
            { "_NormalScale", "控制法线纹理对最终光照法线的影响强度。" },
            { "_Occlusion", "控制环境遮挡纹理压低间接光的强度。" },
            { "_OcclusionMap", "使用绿色通道提供逐像素环境遮挡。" },
            { "_OuterOutlineColor", "设置扩展到原透明区域外部的轮廓颜色。" },
            { "_OuterOutlineWidth", "设置外部轮廓向周围纹理像素扩展的宽度。" },
            { "_PixelOutlineColor", "设置硬边像素轮廓的颜色。" },
            { "_PixelOutlineWidth", "设置四方向硬边像素轮廓的纹理采样宽度。" },
            { "_PoisonColor", "设置中毒状态周期性叠加的颜色。" },
            { "_PoisonDensity", "控制中毒波纹在噪声场中的空间密度。" },
            { "_PoisonSpeed", "控制中毒颜色随时间脉动的速度。" },
            { "_RainbowBrightness", "控制彩虹渐变叠加后的亮度。" },
            { "_RainbowDensity", "控制彩虹色带沿坐标重复的密度。" },
            { "_RainbowDirection", "控制兼容线性彩虹色带在局部坐标中的增长方向。" },
            { "_RainbowSpeed", "控制彩虹色带随当前时间源移动的速度。" },
            { "_ReceiveShadows", "决定 Lit 材质是否应用主光源实时阴影衰减。" },
            { "_ReplaceFrom", "设置颜色替换要匹配的源颜色。" },
            { "_ReplaceRange", "设置源颜色可被匹配的颜色距离范围。" },
            { "_ReplaceSoftness", "控制颜色替换在匹配边界处的过渡柔和度。" },
            { "_ReplaceContrast", "控制替换目标颜色随源亮度变化的对比幂次。" },
            { "_ReplaceFade", "控制颜色替换的整体混合强度。" },
            { "_ReplaceTo", "设置匹配成功后输出的目标颜色。" },
            { "_RimColor", "设置视角边缘叠加的 HDR 轮廓光颜色。" },
            { "_RimIntensity", "控制视角边缘光叠加到输出颜色的强度。" },
            { "_RimPower", "控制边缘光向轮廓集中的曲线；值越高，亮边越窄。" },
            { "_SequenceColumns", "设置序列帧图集的横向列数，Shader 至少按 1 列处理。" },
            { "_SequenceRows", "设置序列帧图集的纵向行数，Shader 至少按 1 行处理。" },
            { "_FlameCenter", "设置局部 UV 中的火焰轮廓中心；默认 (0.5, 0.4) 保持旧版形状。" },
            { "_FlameDirection", "设置火焰向上收束的局部 UV 方向；零向量回退为向上。" },
            { "_ShineAngle", "设置 2D 或 UI 扫光带的兼容方向角；仅在扫光方向为零向量时使用。" },
            { "_ShineColor", "设置扫光带叠加的 HDR 颜色。" },
            { "_ShineDirection", "设置扫光投影方向；2D/UI 使用 XY，Lit/VFX 使用 XYZ，零向量回退到各 Shader 的兼容默认方向。" },
            { "_ShineIntensity", "控制扫光带叠加到输出颜色的亮度。" },
            { "_ShineRotation", "设置 SSU 精确扫光的兼容旋转角；仅在扫光方向为零向量时使用。" },
            { "_ShineSpace", "选择扫光坐标空间：兼容默认保持旧材质行为，局部 UV 跟随纹理，世界投影跨表面连续。" },
            { "_ShineSpeed", "控制扫光带沿指定方向移动的速度。" },
            { "_ShineWidth", "控制单条扫光带的可见宽度。" },
            { "_SmokeSpeed", "设置烟雾噪声在局部 UV 中的流动速度；零向量保持旧版静止噪声。" },
            { "_SquishDirection", "设置局部顶点空间中的挤压主轴；零向量回退为 X 轴。" },
            { "_VibrateDirection", "设置局部顶点空间中的震动主轴；Shader 同时沿其垂直轴组合位移。" },
            { "_ShadowFade", "控制偏移精灵阴影的不透明度。" },
            { "_ShadowOffset", "设置阴影相对主体的局部纹理偏移方向与距离。" },
            { "_ShadowColor", "设置主体后方单次投影使用的颜色。" },
            { "_ShiftHueSpeed", "控制原图色相随当前时间源旋转的速度，可使用负值反向。" },
            { "_SineGlowFade", "控制正弦辉光叠加到原颜色的整体强度。" },
            { "_SineGlowColor", "设置周期性叠加的 HDR 辉光颜色。" },
            { "_SineGlowContrast", "控制原图亮度对辉光强度的影响曲线。" },
            { "_SineGlowFrequency", "控制正弦辉光使用当前时间源振荡的速度。" },
            { "_SineGlowMin", "设置 SSU 正弦波形在最低相位使用的值。" },
            { "_SineGlowMax", "设置 SSU 正弦波形的范围参数；实际峰值遵循兼容公式。" },
            { "_SineGlowMaskToggle", "决定是否读取正弦辉光遮罩；关闭时跳过遮罩采样。" },
            { "_SineGlowMask", "使用 RGB 与 Alpha 共同调制正弦辉光颜色。" },
            { "_SparkleColor", "设置程序化闪点叠加的 HDR 颜色。" },
            { "_SparkleDensity", "控制可生成闪点的随机网格比例。" },
            { "_SparkleIntensity", "控制程序化闪点叠加到输出颜色的亮度。" },
            { "_SparkleScale", "控制闪点网格在 UV 空间中的密度。" },
            { "_SparkleSharpness", "控制闪点随时间和形状衰减的锐利程度。" },
            { "_SparkleSpeed", "控制闪点亮度随当前时间源闪烁的速度。" },
            { "_StrongTint", "设置强制染色使用的目标 HDR 颜色。" },
            { "_StrongTintFade", "控制原颜色向强制染色目标过渡的比例。" },
            { "_UseOcclusionMap", "决定是否读取环境遮挡纹理；关闭时不执行该纹理采样。" },
            { "_UseUIAlphaClip", "决定是否启用 Unity UI 的 0.001 固定阈值透明裁剪关键词。" },
            { "_VertexColorStrength", "控制顶点色参与最终颜色的比例；0 忽略顶点色，1 完整使用。" }
        };
        private static readonly Dictionary<string, PropertyHelp> HelpByProperty = CreateHelpTable();

        private static PropertyHelp ResolveHelp(Shader shader, MaterialProperty property, string displayName)
        {
            string shaderName = shader != null ? shader.name : string.Empty;
            string key = shaderName + ":" + property.name;
            PropertyHelp help;
            if (HelpByProperty.TryGetValue(key, out help))
                return help;

            string type = property.type == MaterialProperty.PropType.Color ? "颜色"
                : property.type == MaterialProperty.PropType.Vector ? "向量"
                : property.type == MaterialProperty.PropType.Texture ? "纹理" : "浮点/范围";
            string target = shaderName == "ES/UI/Composite URP" ? "UI Graphic / Image 独立材质" : "Renderer（Sprite、Mesh 或 VFX）";
            string mode = shaderName == "ES/UI/Composite URP" ? "缓存的 Material 实例" : "MaterialPropertyBlock";
            string semantic = DescribeFallback(property.name, displayName, type, shaderName);
            return new PropertyHelp(
                displayName,
                semantic,
                type,
                target,
                mode,
                "在不修改共享材质的前提下，仅为当前对象写入“" + displayName + "”。",
                property.name);
        }

        private static string DescribeFallback(string propertyName, string displayName, string type, string shaderName)
        {
            if (CommonPropertyDescriptions.TryGetValue(propertyName, out string description))
                return description;
            if (propertyName.IndexOf("Color", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "设置“" + displayName + "”的颜色值；HDR 颜色会作为发光或效果叠加参与最终输出。";
            if (propertyName.IndexOf("Tex", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Map", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "设置“" + displayName + "”使用的纹理资源；运行时可按对象替换而不改共享材质。";
            if (propertyName.IndexOf("Enable", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Use", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("AlphaClip", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "控制“" + displayName + "”效果是否启用；0 表示关闭，1 表示启用。";
            if (propertyName.IndexOf("Progress", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "推进“" + displayName + "”对应的效果阶段，通常使用 0 到 1 的归一化值。";
            if (propertyName.IndexOf("Speed", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "控制“" + displayName + "”随时间变化的速度；可使用负值反向播放。";
            if (propertyName.IndexOf("Width", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Intensity", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Amount", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Strength", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "调节“" + displayName + "”的作用幅度；请结合材质预览确认边界值。";
            if (propertyName.IndexOf("Scale", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Density", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Frequency", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "调节“" + displayName + "”的空间采样密度；过高的频率可能造成闪烁或噪声过密。";
            if (propertyName.IndexOf("Mode", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "选择“" + displayName + "”的算法分支；推荐使用对应 ES 强枚举保持数值稳定。";
            if (type == "向量")
                return "设置“" + displayName + "”的多维参数；不同分量分别控制对应方向、范围或坐标。";
            return "设置“" + displayName + "”的运行时数值，用于按对象覆盖“" + shaderName + "”的材质默认参数。";
        }

        private static Dictionary<string, PropertyHelp> CreateHelpTable()
        {
            var map = new Dictionary<string, PropertyHelp>();
            AddHelp(map, "ES/2D/Composite URP", "_MainTex", "主纹理", "2D 精灵的基础采样纹理。", "纹理");
            AddHelp(map, "ES/2D/Composite URP", "_Color", "颜色", "与主纹理相乘的对象颜色和透明度。", "颜色");
            AddHelp(map, "ES/2D/Composite URP", "_AnimationMode", "动画模式", "选择静态显示或按时间推进的序列帧模式。", "枚举");
            AddHelp(map, "ES/2D/Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/2D/Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率；负值可倒放，各效果速度仍由各自参数控制。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableTimeFPS", "启用时间帧率量化", "在时间倍率之后按指定帧率离散时间。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_TimeFPS", "时间帧率", "每秒时间采样次数，运行时限制为 0.01～240。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableTimeFrequency", "启用周期时间", "在帧率量化之后将时间转换为正弦周期。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_TimeFrequency", "时间周期频率", "控制周期时间的振荡频率。", "浮点");
            AddHelp(map, "ES/2D/Composite URP", "_TimeRange", "时间周期范围", "控制周期时间的振幅。", "浮点");
            AddHelp(map, "ES/2D/Composite URP", "_MainTexScaleOffset", "主纹理缩放/偏移", "用 Vector4 的 XY 设置缩放、ZW 设置 UV 偏移。", "向量");
            AddHelp(map, "ES/2D/Composite URP", "_SequenceFrame", "序列帧当前帧", "指定序列帧动画当前使用的帧索引。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_SequenceSpeed", "序列帧速度", "控制序列帧按场景时间自动推进的速度。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_FadeMode", "渐隐模式", "选择方向透明、纹理遮罩、全纹理溶解、方向发光/扰动或源点溶解。进度 0 可见、1 消失。", "枚举");
            AddHelp(map, "ES/2D/Composite URP", "_FadeProgress", "渐隐进度", "控制渐隐/遮罩/溶解效果推进到的归一化位置。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_FadeMask", "渐隐遮罩", "为遮罩模式提供逐像素的灰度控制纹理。", "纹理");
            AddHelp(map, "ES/2D/Composite URP", "_FadeNoiseTex", "渐隐噪声纹理", "为溶解和边缘扰动提供灰度噪声；建议 Repeat 和线性采样。", "纹理");
            AddHelp(map, "ES/2D/Composite URP", "_FadeRotation", "渐隐方向", "方向模式的旋转角度；源点模式使用渐隐位置作为径向中心。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_DissolveEdgeColor", "溶解边缘颜色", "设置溶解边界的发光颜色。", "颜色");
            AddHelp(map, "ES/2D/Composite URP", "_EnableAddColor", "启用叠加颜色", "开启额外颜色叠加层。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableStrongTint", "启用强制染色", "开启覆盖原始颜色的强制染色。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableColorReplace", "启用颜色替换", "按颜色距离将指定颜色替换为目标颜色。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableBrightness", "启用亮度", "开启对象亮度调整。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_Brightness", "亮度", "控制最终颜色的整体亮度倍率。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableContrast", "启用对比度", "开启颜色对比度调整。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_Contrast", "对比度", "控制颜色相对中性灰的对比度。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableSaturation", "启用饱和度", "开启颜色饱和度调整。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_Saturation", "饱和度", "控制颜色的鲜艳程度。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableHue", "启用色相偏移", "开启 HSV 色相旋转。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_Hue", "色相偏移", "控制颜色在色环上的旋转量。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableBlackTint", "启用暗部染色", "只对原图暗部叠加 HDR 染色，保留亮部。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableInkSpread", "启用墨水扩散", "从局部坐标中心按距离和共享噪声推进墨水颜色。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableShiftHue", "启用动态色相偏移", "使用统一时间源持续旋转原图色相。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableAddHue", "启用动态色相叠加", "按原图亮度叠加随时间变化的 HDR 色相，可选遮罩。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableSineGlow", "启用正弦辉光", "按 SSU 兼容波形周期叠加 HDR 辉光，可选彩色遮罩。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableSqueeze", "启用径向挤压", "在主纹理采样前围绕中心重映射 UV；会与序列帧、平铺和其他 UV 效果叠加。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableSineRotate", "启用正弦旋转", "按统一时间源围绕指定中心往复旋转 UV，不改变几何边界。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableSineMove", "启用正弦移动", "在顶点阶段沿 XY 偏移网格；Renderer Bounds 过小时可能被错误剔除。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableSineScale", "启用正弦缩放", "在顶点阶段按单向正弦波缩放网格；请为最大缩放预留 Bounds。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableCustomFade", "启用自定义渐隐", "以专用遮罩、共享噪声和材质透明度控制最终可见性，并同步约束精灵阴影。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableFullGlowDissolve", "启用全局辉光溶解", "以共享噪声推进完整溶解并叠加 HDR 边缘色；最终可见性也会约束阴影。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableCamouflage", "启用迷彩", "用两层共享噪声生成三色迷彩图案；动画关闭时跳过扰动噪声采样。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableMetal", "启用金属着色", "按原图明度和动态噪声生成金属基色与高光；可选专用遮罩。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableEnchanted", "启用附魔流光", "用两层滚动噪声叠加 HDR 流光，可在双色与彩虹模式间切换。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableShifting", "启用明度流变", "根据原图明度和统一时间源生成双色或彩虹流变。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableShadow", "启用精灵阴影", "额外采样一次偏移后的主纹理 Alpha，在主体后方合成阴影。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableNegative", "启用负片", "开启颜色反相效果。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableRainbow", "启用彩虹渐变", "开启沿坐标和时间变化的彩虹染色。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableInnerOutline", "启用内描边", "在精灵内部边缘绘制描边。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableOuterOutline", "启用外描边", "在精灵外部扩展透明区域绘制描边。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnablePixelOutline", "启用像素描边", "按 SSU 四方向像素邻域绘制硬边描边。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableShine", "启用扫光", "开启沿指定角度移动的扫光带。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_ShineMaskToggle", "使用扫光遮罩", "SSU 合同模式下以遮罩纹理的 R 与 A 通道限制扫光。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_ShineMask", "扫光遮罩", "SSU 合同模式使用的独立扫光遮罩纹理。", "纹理");
            AddHelp(map, "ES/2D/Composite URP", "_ShineIntensity", "扫光强度", "控制扫光叠加到最终颜色的强度。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableSparkle", "启用亮晶晶", "开启程序化闪点和闪烁高光。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_SparkleIntensity", "亮晶晶强度", "控制闪点叠加亮度。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableFlow", "启用纹理流动", "按时间推进主纹理 UV。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_FlowSpeed", "流动速度", "Vector2 的 XY 作为 UV 流动方向和速度。", "向量");
            AddHelp(map, "ES/2D/Composite URP", "_EnableChromatic", "启用色差", "通过 RGB 通道偏移产生轻量色差。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_ChromaticOffset", "色差偏移", "控制红蓝通道的 UV 偏移；会增加两次纹理采样。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableBlur", "启用纹理模糊", "对主纹理执行轻量五点模糊，不读取屏幕背景。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_BlurRadius", "模糊半径", "控制五点采样的偏移范围。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_BlurIntensity", "模糊强度", "控制原图与模糊结果的混合比例。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_BlurMode", "模糊核", "选择轻量五点核或更平滑、采样成本更高的 3x3 Gaussian 核。", "枚举");
            AddHelp(map, "ES/2D/Composite URP", "_TilingMode", "主纹理平铺空间", "选择局部 UV、连续世界空间或屏幕空间；单一模式避免空间语义冲突。", "枚举");
            AddHelp(map, "ES/2D/Composite URP", "_WorldTilingPixelsPerUnit", "世界平铺每单位重复数", "控制世界坐标中每个单位重复主纹理的次数。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_ScreenTilingPixelsPerUnit", "屏幕平铺像素尺寸", "控制屏幕空间每次重复覆盖的像素尺寸。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableSmoothPixelArt", "启用平滑像素画", "使用导数重建像素边缘，不增加纹理采样；不建议与 Blur 同时使用。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableCheckerboard", "启用棋盘格", "按连续世界坐标叠加交错暗格，只修改 RGB。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_UberNoiseTexture", "共享效果噪声", "Flame、Smoke、Ink Spread、Camouflage、Metal、Enchanted、Custom Fade 与 Full Glow Dissolve 共用；建议 Repeat、Bilinear。", "纹理");
            AddHelp(map, "ES/2D/Composite URP", "_EnableFlame", "启用火焰", "按局部 UV 中心、半径和滚动噪声生成火焰；通常搭配空白白色主纹理。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_FlameBrightness", "火焰亮度", "控制火焰乘到 RGB 的亮度；Alpha 仍由火焰遮罩控制。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableSmoke", "启用烟雾", "使用噪声、局部径向距离和顶点色生成烟雾透明度与暗边。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_SmokeVertexSeed", "烟雾使用顶点色种子", "使用顶点 R 通道错开粒子烟雾噪声；需实际提供变化的顶点色。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableSharpen", "启用纹理锐化", "用中心与四方向邻域增强细节；仅高质量档执行。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_SharpenAmount", "锐化强度", "控制高频细节叠加量；过高可能产生亮边。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_SharpenRadius", "锐化半径", "控制邻域采样距离。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_SharpenThreshold", "锐化阈值", "抑制平坦区域和低幅噪声的细节增强。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_SharpenFade", "锐化混合", "控制原图与锐化结果的混合比例。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableDistortion", "启用噪声扰动", "开启噪声驱动的 UV 扰动。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_DistortionStrength", "扰动强度", "控制噪声扰动造成的 UV 偏移量。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_DistortionDirection", "扰动方向与轴强度", "分别缩放噪声造成的 XY 位移；(1,1) 保持旧材质结果。", "向量");
            AddHelp(map, "ES/2D/Composite URP", "_EnableHologram", "启用全息", "普通材质使用兼容扫描线；SSU 精确合同使用世界高度、对比度、最低透明度和双噪声横向扰动。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_HologramColor", "全息颜色", "设置全息覆盖层的颜色。", "颜色");
            AddHelp(map, "ES/2D/Composite URP", "_EnableGlitch", "启用故障", "普通材质使用兼容抖动；SSU 精确合同使用独立遮罩、颜色噪声和位移噪声。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_GlitchIntensity", "故障强度", "控制故障效果的最大 UV 偏移。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableFrozen", "启用冰冻", "开启冰冻颜色和冰晶高光效果。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableBurn", "启用燃烧", "开启按噪声推进的燃烧边缘和裁剪。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_BurnProgress", "燃烧进度", "控制燃烧边缘在噪声场中的推进位置。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnablePoison", "启用中毒", "开启周期性中毒染色效果。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BaseMap", "基础颜色纹理", "URP Lit 表面的基础颜色采样纹理。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BaseColor", "基础颜色", "URP Lit 表面的基础颜色和透明度。", "颜色");
            AddHelp(map, "ES/3D/Lit Composite URP", "_NormalMap", "法线纹理", "改变光照法线方向的法线贴图。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_UseNormalMap", "启用法线纹理", "开启后才采样法线纹理；关闭时使用顶点法线并节省一次纹理采样。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_UseMetallicMap", "使用金属度纹理", "开启后使用纹理 R 通道调制金属度、A 通道调制光滑度。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_MetallicMap", "金属度/光滑度纹理", "R 通道为金属度，A 通道为光滑度；与材质标量相乘。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_Metallic", "金属度", "控制表面从绝缘体到金属的反射响应。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_Smoothness", "光滑度", "控制高光的锐利程度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_UseEmission", "启用自发光", "开启后才采样并叠加自发光纹理。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_DissolveMode", "溶解模式", "选择噪声溶解或距离溶解算法；需要标准或高质量档。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_DissolveProgress", "溶解进度", "控制模型被溶解掉的归一化进度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableRim", "启用边缘光", "按视角边缘为模型增加轮廓光；需要标准或高质量档。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_RimIntensity", "边缘光强度", "控制轮廓光的叠加强度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableShine", "启用扫光", "开启沿模型表面移动的扫光高光；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_ShineMaskToggle", "使用扫光遮罩", "SSU 合同模式下以基础 UV 采样遮罩的 R 与 A 通道限制扫光。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_ShineMask", "扫光遮罩", "SSU 合同模式使用的独立扫光遮罩纹理。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_ShineIntensity", "扫光强度", "控制扫光高光的叠加强度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableSparkle", "启用亮晶晶", "在高质量档位下开启程序化闪点。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableFlow", "启用纹理流动", "沿顶点 UV 推进主纹理采样。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableChromatic", "启用色差", "对基础颜色纹理执行轻量 RGB 分离。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableBlur", "启用纹理模糊", "对基础颜色纹理执行轻量五点模糊。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BlurRadius", "模糊半径", "控制 Lit 基础颜色纹理的采样偏移。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BlurIntensity", "模糊强度", "控制 Lit 基础颜色的柔化比例。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableSmoothPixelArt", "启用平滑像素画", "使用屏幕导数重建基础纹理像素边缘；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnablePixelate", "启用像素化", "按基础纹理宽高比量化 UV；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableCheckerboard", "启用棋盘格", "按世界 XZ 坐标生成连续棋盘底纹，只修改 Albedo。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableFlame", "启用火焰", "复用共享噪声生成火焰颜色与 Alpha，阴影和深度路径保持同轮廓。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableSmoke", "启用烟雾", "复用共享噪声生成烟雾暗边与 Alpha，可用顶点 R 通道错开图样。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableHalftone", "启用半色调", "在 PBR 光照前对 Albedo 叠加抗锯齿网点；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableSharpen", "启用纹理锐化", "使用中心与四方向邻域增强基础纹理细节；仅高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableTextureLayer1", "启用纹理层 1", "在基础表面效果后叠加第一层 RGBA 纹理；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableTextureLayer2", "启用纹理层 2", "在纹理层 1 后叠加第二层 RGBA 纹理；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TextureLayer1Texture", "纹理层 1 贴图", "与基础纹理共用采样器状态以控制 SM3.0 采样器数量；Wrap 与 Filter 语义以 BaseMap 为准。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TextureLayer2Texture", "纹理层 2 贴图", "与基础纹理共用采样器状态以控制 SM3.0 采样器数量；Wrap 与 Filter 语义以 BaseMap 为准。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableInnerOutline", "启用内描边", "在基础纹理 Alpha 内侧采样八方向邻域，可选扰动、纹理着色和仅描边输出；仅高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableOuterOutline", "启用外描边", "在网格覆盖范围内采样八方向邻域并扩展基础 Alpha，可独立扰动与着色；仅高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnablePixelOutline", "启用像素描边", "按基础纹理像素尺寸采样八方向邻域；仅高质量档生效，并优先于外描边宽度与颜色。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableShadow", "启用精灵阴影", "在当前网格覆盖范围内合成偏移的基础 Alpha；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableFullGlowDissolve", "启用全局辉光溶解", "共享噪声驱动硬阈值与辉光边缘，并同步主画面、深度和阴影轮廓；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableHologram", "启用全息", "在局部 UV 或世界高度空间生成扫描线、HDR 颜色、最低透明度与分段扰动；所有 Alpha Pass 使用同一坐标，仅高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableGlitch", "启用故障", "使用独立遮罩、颜色噪声与位移噪声驱动色相、亮度和方向位移；所有 Alpha Pass 使用同一坐标，仅高质量档生效。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableBurn", "启用燃烧边缘", "开启溶解/燃烧交界处的边缘着色；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_AlphaClip", "启用透明裁剪", "按 Cutoff 阈值丢弃低透明度像素。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_QualityTier", "效果质量档位", "基础保留 Lit 主体；标准启用形变、流向、溶解、纹理层、精灵阴影和生成式图样；高质量再启用描边、全息、故障、锐化、扫光、闪点和燃烧边缘。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_ResourceProfile", "资源编译配置", "动态完整保留所有资源路径并支持 MPB 随时切换效果；材质优化根据当前材质开关选择唯一资源掩码，减少未使用纹理绑定。优化后若在运行时修改材质效果开关，需要调用 ES3DLitCompositeURPProperties.RefreshResourceProfile。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率；负值可倒放，各效果速度仍由各自参数控制。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableTimeFPS", "启用时间帧率量化", "在时间倍率之后按指定帧率离散时间。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TimeFPS", "时间帧率", "每秒时间采样次数，运行时限制为 0.01～240。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableTimeFrequency", "启用周期时间", "在帧率量化之后将时间转换为正弦周期。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TimeFrequency", "时间周期频率", "控制周期时间的振荡频率。", "浮点");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TimeRange", "时间周期范围", "控制周期时间的振幅。", "浮点");
            AddHelp(map, "ES/3D/Lit Composite URP", "_MainTexScaleOffset", "主纹理缩放/偏移", "用 Vector4 的 XY 设置缩放、ZW 设置 UV 偏移。", "向量");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableVertexAnimation", "启用顶点动画", "在局部空间执行正弦顶点位移；标准/高质量档生效，并同步 Forward、ShadowCaster、DepthOnly 与 DepthNormals。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_VertexAnimationDirection", "顶点动画局部方向", "XYZ 定义局部空间位移方向；零向量自动回退为局部 Y 轴。", "向量");
            AddHelp(map, "ES/3D/Lit Composite URP", "_VertexAnimationAmplitude", "顶点动画幅度", "控制顶点沿局部方向移动的最大距离。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_VertexAnimationFrequency", "顶点动画频率", "控制波形在模型局部坐标中的疏密。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_VertexAnimationSpeed", "顶点动画速度", "控制正弦波随所选时间来源推进的速度。", "浮点");
            AddHelp(map, "ES/3D/Lit Composite URP", "_VertexAnimationMask", "顶点色动画遮罩", "选择顶点色通道限制形变；不使用遮罩时所有顶点等强度位移。", "强枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableFlowMap", "启用流向贴图", "用流向贴图 RG 通道扭曲主纹理 UV；标准/高质量档生效，并与透明裁剪的阴影和深度路径保持一致。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_FlowMap", "流向贴图", "RG 通道按 0.5 为静止方向解码；建议关闭 sRGB 并使用可平铺纹理。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_FlowMapScale", "流向贴图缩放/偏移", "Vector4 的 XY 控制流向纹理缩放，ZW 控制静态偏移。", "向量");
            AddHelp(map, "ES/3D/Lit Composite URP", "_FlowMapSpeed", "流向贴图速度", "Vector2 的 XY 控制流向纹理自身随时间移动的方向和速度。", "向量");
            AddHelp(map, "ES/3D/Lit Composite URP", "_FlowMapStrength", "流向贴图强度", "控制流向纹理对主纹理 UV 的最大偏移。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableAddColor", "启用叠加颜色", "在进入 PBR 光照前向 Lit 表面颜色叠加 HDR 颜色。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableStrongTint", "启用强制染色", "在进入 PBR 光照前按强度把 Lit 表面染成目标颜色。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableAlphaTint", "启用透明染色", "按当前表面透明度混合染色，但不改变 Alpha、阴影或深度合同。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableColorReplace", "启用颜色替换", "按 RGB 距离和柔和度替换 Lit 基础颜色。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableRecolorRGB", "启用 RGB 重映色", "按 RGB 通道权重重建 Lit 基础颜色，可用纹理通道遮罩。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableRecolorRGBYCP", "启用 RGBYCP 重映色", "按红绿蓝黄青紫六组权重重建 Lit 基础颜色，可用纹理通道遮罩。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableBrightness", "启用亮度", "在进入 PBR 光照前调整 Lit 基础颜色亮度。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableContrast", "启用对比度", "围绕中性灰调整 Lit 基础颜色对比度。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableSaturation", "启用饱和度", "调整 Lit 基础颜色的饱和程度。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableHue", "启用色相偏移", "在 HSV 色环中偏移 Lit 基础颜色。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableSplitToning", "启用分离色调", "按表面明度分别施加阴影和高光色调。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableBlackTint", "启用暗部染色", "只对 Lit 基础颜色的暗部叠加 HDR 染色。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableInkSpread", "启用墨水扩散", "按 UV 距离与共享噪声推进墨水染色；噪声仅在开关开启时采样。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableShiftHue", "启用动态色相偏移", "使用统一时间源持续旋转 Lit 基础颜色色相。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableAddHue", "启用动态色相叠加", "按原色明度生成动态色相并写入 Emission，可用纹理遮罩。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableSineGlow", "启用正弦辉光", "按统一时间源生成正弦辉光并写入 Emission，可用彩色遮罩。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableCamouflage", "启用迷彩", "用共享噪声生成三色迷彩；静态与动态采样都受父开关门控。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableMetal", "启用金属着色", "按基础颜色明度和动态噪声生成风格化金属着色，不替代 PBR 金属度参数。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableFrozen", "启用冰冻", "叠加冰冻色，并把冰晶高光写入 Emission。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnablePoison", "启用中毒", "使用共享状态噪声和统一时间源生成周期性中毒染色。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableEnchanted", "启用附魔流光", "使用双向共享噪声生成双色或彩虹附魔流光。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableShifting", "启用明度流变", "按基础颜色明度和统一时间源生成双色或彩虹流变。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableNegative", "启用负片", "按强度把 Lit 基础颜色混合到负片结果。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableRainbow", "启用彩虹渐变", "按 UV 纵坐标和统一时间源混合彩虹色。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnablePingPongGlow", "启用往返发光", "在两个 HDR 颜色之间周期往返并写入 Emission。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_UberNoiseTexture", "SSU 效果共享噪声", "供墨水、迷彩、金属、冰冻、中毒与附魔共享；建议关闭 sRGB 并使用 Repeat。", "纹理");
            AddHelp(map, "ES/3D/VFX Composite URP", "_MainTex", "VFX 主纹理", "粒子或特效卡片的主采样纹理。", "纹理");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableSequence", "启用序列帧", "把主纹理按行列切分并选择当前帧；关闭时保持原始 UV。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SequencePlayback", "序列帧播放方式", "选择手动帧、按当前时间源播放，或读取 ParticleSystem Custom1.z 作为帧号偏移。", "强枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SequenceColumns", "序列帧列数", "设置图集横向帧数；Shader 会向下取整并保证至少为 1。", "浮点/整数语义");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SequenceRows", "序列帧行数", "设置图集纵向帧数；帧顺序从左到右、从上到下。", "浮点/整数语义");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SequenceFrame", "序列帧起始帧", "手动模式直接选择此帧；时间和顶点流模式以此值作为基础偏移。", "浮点/帧号");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SequenceSpeed", "序列帧速度", "按时间播放时每秒推进的帧数；允许负值反向播放。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnablePolarUV", "启用极坐标 UV", "把笛卡尔 UV 转换为角度与半径坐标；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_PolarCenter", "极坐标中心", "设置极坐标转换的 UV 中心，通常使用 (0.5, 0.5)。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_PolarRadialScale", "极坐标径向缩放", "缩放极坐标结果的半径轴；负值可反向径向流动。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_PolarAngularScale", "极坐标角向缩放", "控制纹理沿圆周重复的次数和方向。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_PolarRotationSpeed", "极坐标旋转速度", "按当前时间源推进角度轴，生成旋转或涡流动画。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableVertexStreams", "启用粒子顶点流", "读取 ParticleSystem Renderer 的 Custom1.xyzw 与 Custom2.x；所有通道均为增量，零值不覆盖材质基础值。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexStreamUVStrength", "Custom1 XY · UV 偏移", "缩放 Custom1.xy 对每粒子主纹理 UV 的偏移量。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexStreamFrameStrength", "Custom1 Z · 帧号偏移", "缩放 Custom1.z 对序列帧基础帧号的增量。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexStreamDissolveStrength", "Custom1 W · 溶解增量", "缩放 Custom1.w 并加到材质溶解进度，最终限制在 0 到 1。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexStreamEmissionStrength", "Custom2 X · 自发光增量", "把 Custom2.x 作为每粒子自发光倍率增量；零值保持材质自发光。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_NoiseTex", "VFX 噪声纹理", "驱动扰动、溶解和故障的噪声来源。", "纹理");
            AddHelp(map, "ES/3D/VFX Composite URP", "_Distortion", "扰动强度", "控制噪声对 VFX UV 的偏移量；非零值需要标准或高质量档。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DistortionDirection", "扰动方向与轴强度", "分别缩放噪声造成的 XY 位移；(1,1) 保持旧材质结果。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableFlow", "启用纹理流动", "按时间推进 VFX 主纹理 UV。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableShine", "启用扫光", "开启沿可配置方向移动的扫光；可选择序列帧局部 UV 或世界投影空间，需要标准或高质量档。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableSparkle", "启用亮晶晶", "开启程序化闪点叠加；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableChromatic", "启用色差", "通过 RGB 通道偏移产生轻量色差；标准/高质量档执行，并增加两次主纹理采样。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableBlur", "启用纹理模糊", "对 VFX 主纹理执行轻量五点模糊；仅高质量档执行，并限制采样在当前序列帧内。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_BlurRadius", "模糊半径", "控制 VFX 主纹理的采样偏移。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_BlurIntensity", "模糊强度", "控制 VFX 主纹理的柔化比例。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DissolveMode", "VFX 溶解模式", "选择普通溶解或带边缘光的溶解；需要标准或高质量档。", "枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DissolveProgress", "VFX 溶解进度", "控制特效透明区域的推进位置。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableHologram", "VFX 全息开关", "为特效卡片叠加扫描线全息效果；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableGlitch", "VFX 故障开关", "为特效卡片增加随机横向故障偏移；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_QualityTier", "VFX 效果质量档位", "基础保留序列帧、粒子流和径向遮罩；标准启用形变、噪声、溶解、视角与深度效果；高质量再启用模糊、闪点、全息和故障。", "枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率；负值可倒放，各效果速度仍由各自参数控制。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableTimeFPS", "启用时间帧率量化", "在时间倍率之后按指定帧率离散时间。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_TimeFPS", "时间帧率", "每秒时间采样次数，运行时限制为 0.01～240。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableTimeFrequency", "启用周期时间", "在帧率量化之后将时间转换为正弦周期。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_TimeFrequency", "时间周期频率", "控制周期时间的振荡频率。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_TimeRange", "时间周期范围", "控制周期时间的振幅。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_MainTexScaleOffset", "主纹理缩放/偏移", "用 Vector4 的 XY 设置缩放、ZW 设置 UV 偏移。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableVertexAnimation", "启用顶点动画", "在局部空间执行正弦顶点位移；标准/高质量档生效，适合网格特效和卡片摆动。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexAnimationDirection", "顶点动画局部方向", "XYZ 定义局部空间位移方向；零向量自动回退为局部 Y 轴。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexAnimationAmplitude", "顶点动画幅度", "控制顶点沿局部方向移动的最大距离。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexAnimationFrequency", "顶点动画频率", "控制波形在模型局部坐标中的疏密。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexAnimationSpeed", "顶点动画速度", "控制正弦波随所选时间来源推进的速度。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexAnimationMask", "顶点色动画遮罩", "选择顶点色通道限制形变；不使用遮罩时所有顶点等强度位移。", "强枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableFlowMap", "启用流向贴图", "用流向贴图 RG 通道扭曲主纹理 UV；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FlowMap", "流向贴图", "RG 通道按 0.5 为静止方向解码；建议关闭 sRGB 并使用可平铺纹理。", "纹理");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FlowMapScale", "流向贴图缩放/偏移", "Vector4 的 XY 控制流向纹理缩放，ZW 控制静态偏移。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FlowMapSpeed", "流向贴图速度", "Vector2 的 XY 控制流向纹理自身随时间移动的方向和速度。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FlowMapStrength", "流向贴图强度", "控制流向纹理对 VFX 主纹理 UV 的最大偏移。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableSoftParticles", "启用软粒子", "按相机深度柔化 VFX 与场景几何的交界；标准/高质量档生效，URP 必须开启 Depth Texture。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SoftParticleNear", "软粒子起始距离", "控制交界处从完全透明开始恢复的深度间隔。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SoftParticleFar", "软粒子结束距离", "控制透明过渡结束的深度间隔，必须大于起始距离。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableRadialMask", "启用径向遮罩", "按原始主 UV 到指定中心的距离控制透明度；基础档可用。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_RadialMaskCenter", "径向遮罩中心", "设置圆形遮罩的 UV 中心。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_RadialMaskRadius", "径向遮罩半径", "设置完整可见区域的半径。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_RadialMaskSoftness", "径向遮罩柔和度", "设置从可见到透明的过渡宽度。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_RadialMaskInvert", "反转径向遮罩", "反转遮罩内外区域，制作圆环外扩或空心冲击波。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableFresnelMask", "启用菲涅尔遮罩", "按视线与表面法线夹角控制透明度和附加发光；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableRim", "启用边缘光", "按视线与表面法线夹角叠加边缘光；需要标准或高质量档。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelPower", "菲涅尔幂次", "控制菲涅尔从正面到边缘的曲线集中程度。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelMin", "菲涅尔起点", "设置重映射的最低阈值；超过起点后开始显现。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelMax", "菲涅尔终点", "设置重映射的最高阈值；应高于起点。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelAlphaInfluence", "菲涅尔透明度影响", "0 只增加颜色，1 完全用菲涅尔遮罩乘透明度。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelColor", "菲涅尔颜色", "设置视角边缘附加发光的 HDR 颜色。", "颜色");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelIntensity", "菲涅尔发光强度", "控制菲涅尔颜色叠加强度；设为 0 时只作为透明遮罩。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableDepthIntersection", "启用深度交界发光", "在 VFX 接近场景几何时叠加交界光；与软粒子共用一次深度采样，要求 URP Depth Texture。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DepthIntersectionColor", "深度交界颜色", "设置 VFX 与场景几何接触区域的 HDR 发光颜色。", "颜色");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DepthIntersectionDistance", "深度交界距离", "设置交界发光从接触面向外衰减的眼空间距离。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DepthIntersectionIntensity", "深度交界强度", "控制交界发光叠加强度。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_BlendMode", "混合模式", "选择透明、叠加、预乘透明或正片叠底；会同步底层 Blend 因子。", "强枚举/材质状态");
            AddHelp(map, "ES/3D/VFX Composite URP", "_ZWriteMode", "深度写入", "控制 Pass 是否写入深度；透明 VFX 通常关闭，封闭网格特效可按需开启。", "强枚举/材质状态");
            AddHelp(map, "ES/3D/VFX Composite URP", "_ZTest", "深度测试", "控制片元与相机深度缓冲的比较方式；常规 VFX 使用小于等于。", "强枚举/材质状态");
            AddHelp(map, "ES/3D/VFX Composite URP", "_Cull", "剔除模式", "选择双面、剔除正面或剔除背面；卡片粒子通常使用双面。", "强枚举/材质状态");
            AddHelp(map, "ES/3D/VFX Composite URP", "_QueueOffset", "渲染队列偏移", "在 Transparent 3000 基础上调整 -50 到 50，用于控制透明特效排序。", "整数/材质状态");
            AddHelp(map, "ES/UI/Composite URP", "_MainTex", "UI 主纹理", "由 CanvasRenderer 按对象提供；RawImage 使用 texture，Image 使用 sprite。", "纹理");
            AddHelp(map, "ES/UI/Composite URP", "_Color", "UI 颜色", "与 UI 顶点颜色和主纹理相乘的颜色。", "颜色");
            AddHelp(map, "ES/UI/Composite URP", "_EnableAddColor", "UI 叠加颜色", "向 UI 最终颜色叠加 HDR 颜色。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableStrongTint", "UI 强制染色", "按强度将 UI 原色替换为指定颜色。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableAlphaTint", "UI 透明染色", "按当前最终透明度混合染色，不改变 Canvas、Mask 或裁剪合同。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableColorReplace", "UI 颜色替换", "按 RGB 距离和柔和度替换指定颜色。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableBrightness", "UI 亮度", "调整 UI 最终 RGB 亮度倍率。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableContrast", "UI 对比度", "围绕中性灰调整 UI 颜色对比度。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSaturation", "UI 饱和度", "调整 UI 最终颜色的饱和程度。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableHue", "UI 色相偏移", "在 HSV 色环上偏移 UI 颜色。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableNegative", "UI 负片", "按强度将 UI 颜色混合到负片结果。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableRainbow", "UI 彩虹渐变", "按局部纵坐标和统一时间源混合彩虹色。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableInnerOutline", "UI 内描边", "在普通 Image/RawImage 的透明边缘内侧着色；TMP/SDF 请使用各自专用描边。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableOuterOutline", "UI 外描边", "扩张普通 UI 图像透明边缘；TMP/SDF 模式下为保护距离场结果而跳过。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnablePixelOutline", "UI 像素描边", "按源 Sprite 实际像素尺寸和四方向邻域生成硬边描边；TMP/SDF 模式下跳过。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnablePingPongGlow", "UI 往返发光", "在两个 HDR 颜色之间周期往返叠加发光。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableFrozen", "UI 冰冻", "复用共享效果噪声叠加冰冻色与晶体高光。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableBurn", "UI 燃烧", "复用共享效果噪声推进燃烧颜色和透明度，最终仍受 UI 裁剪。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnablePoison", "UI 中毒", "复用共享效果噪声生成周期性中毒染色。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableBlackTint", "UI 暗部染色", "只对 UI 图像暗部叠加 HDR 染色。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableInkSpread", "UI 墨水扩散", "从 UI 局部坐标中心按距离和共享噪声推进墨水颜色。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableShiftHue", "UI 动态色相偏移", "使用统一时间源持续旋转 UI 原色相。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableAddHue", "UI 动态色相叠加", "按原图亮度叠加随时间变化的 HDR 色相，可选遮罩。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSineGlow", "UI 正弦辉光", "按 SSU 兼容波形周期叠加 HDR 辉光，可选彩色遮罩。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSqueeze", "UI 径向挤压", "在 UI 主纹理采样前围绕中心重映射 UV。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSineRotate", "UI 正弦旋转", "按统一时间源旋转 UI 采样 UV，不扩大 RectTransform 几何边界。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSineMove", "UI 正弦移动", "在顶点阶段移动 UI 网格；可能越出 Mask 或 RectMask2D 的裁切区域。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSineScale", "UI 正弦缩放", "在顶点阶段周期缩放 UI 网格；请检查 Canvas 裁切边界。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableCustomFade", "UI 自定义渐隐", "在 TMP/SDF 轮廓求值后控制最终可见性；专用遮罩不参与距离场计算。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableFullGlowDissolve", "UI 全局辉光溶解", "按共享噪声推进溶解并叠加 HDR 边缘色，仍受 UI Clip Rect 裁切。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableCamouflage", "UI 迷彩", "用两层共享噪声生成三色迷彩；动画关闭时不采样扰动层。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableMetal", "UI 金属着色", "按 UI 原色明度和动态噪声生成金属高光，可选遮罩。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableEnchanted", "UI 附魔流光", "叠加双色或彩虹滚动流光，适合卡牌和稀有度强调。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableShifting", "UI 明度流变", "按图像明度生成双色或彩虹动态流变。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableShadow", "UI 精灵阴影", "额外采样一次偏移后的主纹理 Alpha；会继续受 UI Clip Rect 裁切。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableHologram", "UI 全息开关", "普通材质使用兼容扫描线；SSU 精确合同使用世界高度扫描、颜色对比和双噪声扰动。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableGlitch", "UI 故障开关", "普通材质使用兼容抖动；SSU 精确合同使用独立遮罩、颜色噪声和位移噪声。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableFlow", "UI 纹理流动", "按时间推进 UI 主纹理 UV。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableShine", "UI 扫光", "在 UI 表面叠加可控方向的扫光。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_ShineMaskToggle", "UI 扫光遮罩", "SSU 合同模式下以遮罩纹理的 R 与 A 通道限制扫光。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_ShineMask", "UI 扫光遮罩纹理", "SSU 合同模式使用的独立扫光遮罩。", "纹理");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSparkle", "UI 亮晶晶", "在 UI 上叠加程序化闪点。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableChromatic", "UI 色差", "对 UI 主纹理执行轻量 RGB 分离。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableBlur", "UI 纹理模糊", "对 UI 主纹理执行轻量五点模糊，不等于背景毛玻璃。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_BlurMode", "UI 模糊核", "选择轻量五点核或更平滑、采样成本更高的 3x3 Gaussian 核。", "枚举");
            AddHelp(map, "ES/UI/Composite URP", "_TilingMode", "UI 主纹理平铺空间", "选择 UI 局部、世界或屏幕空间重复主纹理；结果仍限制在当前 UI 子纹理。", "枚举");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSmoothPixelArt", "UI 平滑像素画", "使用导数重建像素边缘；与 UI 模糊同时启用会互相抵消。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableCheckerboard", "UI 棋盘格", "按连续世界坐标叠加交错暗格，只修改 RGB。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_UberNoiseTexture", "UI 共享效果噪声", "Flame、Smoke、Ink Spread、Camouflage、Metal、Enchanted、Custom Fade 与 Full Glow Dissolve 共用；建议 Repeat、Bilinear。", "纹理");
            AddHelp(map, "ES/UI/Composite URP", "_EnableFlame", "UI 火焰", "按 UI 局部 UV 生成径向火焰遮罩和亮度。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSmoke", "UI 烟雾", "按 UI 局部 UV 生成烟雾透明度与暗边。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSharpen", "UI 纹理锐化", "增强 UI 主纹理细节并保持中心 Alpha；仅高质量档执行。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_SharpenAmount", "UI 锐化强度", "控制高频细节叠加量。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_SharpenRadius", "UI 锐化半径", "控制四方向邻域采样距离。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_SharpenThreshold", "UI 锐化阈值", "抑制低幅噪声和压缩伪影。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_SharpenFade", "UI 锐化混合", "控制原图与锐化结果的混合比例。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_FadeMode", "UI 渐隐模式", "使用与 2D Composite 相同的方向、噪声、遮罩和源点溶解合同。", "枚举");
            AddHelp(map, "ES/UI/Composite URP", "_FadeProgress", "UI 渐隐进度", "进度 0 保持可见，进度 1 完全消失。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_FadeMask", "UI 渐隐遮罩", "纹理遮罩模式使用的灰度控制纹理。", "纹理");
            AddHelp(map, "ES/UI/Composite URP", "_BlurRadius", "UI 模糊半径", "控制 UI 主纹理的采样偏移。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_BlurIntensity", "UI 模糊强度", "控制 UI 主纹理的柔化比例。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_GlitchSpeed", "故障速度", "控制 UI 故障图样随时间变化的速度。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_AlphaClip", "UI 透明裁剪", "按阈值裁剪 UI 像素。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/UI/Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率；负值可倒放，各效果速度仍由各自参数控制。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_EnableTimeFPS", "启用时间帧率量化", "在时间倍率之后按指定帧率离散时间。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_TimeFPS", "时间帧率", "每秒时间采样次数，运行时限制为 0.01～240。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_EnableTimeFrequency", "启用周期时间", "在帧率量化之后将时间转换为正弦周期。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_TimeFrequency", "时间周期频率", "控制周期时间的振荡频率。", "浮点");
            AddHelp(map, "ES/UI/Composite URP", "_TimeRange", "时间周期范围", "控制周期时间的振幅。", "浮点");
            AddHelp(map, "ES/UI/Composite URP", "_MainTexScaleOffset", "主纹理缩放/偏移", "用 Vector4 的 XY 设置缩放、ZW 设置 UV 偏移。", "向量");
            return map;
        }

        private static void AddHelp(Dictionary<string, PropertyHelp> map, string shader, string property, string title, string description, string type)
        {
            map[shader + ":" + property] = new PropertyHelp(title, description, type,
                shader == "ES/UI/Composite URP" ? "UI Graphic / Image 独立材质" : "Renderer（Sprite、Mesh 或 VFX）",
                shader == "ES/UI/Composite URP" ? "缓存的 Material 实例" : "MaterialPropertyBlock",
                "按对象覆盖材质默认值，不修改共享材质。", "设置“" + title + "”的运行时值。");
        }

        private sealed class PropertyHelp
        {
            internal readonly string Title;
            internal readonly string Description;
            internal readonly string TypeLabel;
            internal readonly string TargetLabel;
            internal readonly string WriteMode;
            internal readonly string RecommendedUsage;
            internal readonly string Summary;

            internal PropertyHelp(
                string title,
                string description,
                string typeLabel,
                string targetLabel,
                string writeMode,
                string recommendedUsage,
                string summary)
            {
                Title = title;
                Description = description;
                TypeLabel = typeLabel;
                TargetLabel = targetLabel;
                WriteMode = writeMode;
                RecommendedUsage = recommendedUsage;
                Summary = summary;
            }
        }

        #endregion
    }
}
