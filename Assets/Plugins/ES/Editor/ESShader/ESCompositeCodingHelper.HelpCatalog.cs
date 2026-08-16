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
            { "_GlowFrequency", "控制往返发光每秒循环的角频率。" },
            { "_GlowFrom", "设置往返发光插值的起点 HDR 颜色。" },
            { "_GlowIntensity", "控制往返发光叠加到原颜色上的亮度。" },
            { "_GlowTo", "设置往返发光插值的终点 HDR 颜色。" },
            { "_HologramColor", "设置全息扫描线覆盖使用的 HDR 颜色。" },
            { "_HologramFrequency", "控制 VFX 或 UI 全息扫描线沿纵向的密度。" },
            { "_HologramGap", "控制 VFX 全息扫描线中不可见间隔的比例。" },
            { "_HologramLineFrequency", "控制 2D 全息扫描线沿纵向的密度。" },
            { "_HologramLineGap", "控制 2D 全息扫描线中不可见间隔的比例。" },
            { "_HologramMinAlpha", "设置全息扫描线间隔区域保留的最低透明度。" },
            { "_HologramSpeed", "控制全息扫描线沿坐标移动的速度。" },
            { "_InnerOutlineColor", "设置 2D 图形内部轮廓线的颜色。" },
            { "_InnerOutlineWidth", "设置内部轮廓采样相对纹理像素的宽度。" },
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
            { "_PixelOutlineWidth", "设置硬边像素轮廓的纹理采样宽度。" },
            { "_PoisonColor", "设置中毒状态周期性叠加的颜色。" },
            { "_PoisonDensity", "控制中毒波纹在噪声场中的空间密度。" },
            { "_PoisonSpeed", "控制中毒颜色随时间脉动的速度。" },
            { "_RainbowBrightness", "控制彩虹渐变叠加后的亮度。" },
            { "_RainbowDensity", "控制彩虹色带沿坐标重复的密度。" },
            { "_RainbowSpeed", "控制彩虹色带随当前时间源移动的速度。" },
            { "_ReceiveShadows", "决定 Lit 材质是否应用主光源实时阴影衰减。" },
            { "_ReplaceFrom", "设置颜色替换要匹配的源颜色。" },
            { "_ReplaceRange", "设置源颜色可被匹配的颜色距离范围。" },
            { "_ReplaceSoftness", "控制颜色替换在匹配边界处的过渡柔和度。" },
            { "_ReplaceTo", "设置匹配成功后输出的目标颜色。" },
            { "_RimColor", "设置视角边缘叠加的 HDR 轮廓光颜色。" },
            { "_RimIntensity", "控制视角边缘光叠加到输出颜色的强度。" },
            { "_RimPower", "控制边缘光向轮廓集中的曲线；值越高，亮边越窄。" },
            { "_SequenceColumns", "设置序列帧图集的横向列数，Shader 至少按 1 列处理。" },
            { "_SequenceRows", "设置序列帧图集的纵向行数，Shader 至少按 1 行处理。" },
            { "_ShineAngle", "设置 2D 或 UI 扫光带在 UV 空间中的方向角。" },
            { "_ShineColor", "设置扫光带叠加的 HDR 颜色。" },
            { "_ShineDirection", "设置 Lit 扫光在世界表面投影使用的方向。" },
            { "_ShineIntensity", "控制扫光带叠加到输出颜色的亮度。" },
            { "_ShineSpeed", "控制扫光带沿指定方向移动的速度。" },
            { "_ShineWidth", "控制单条扫光带的可见宽度。" },
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
            AddHelp(map, "ES/2D/Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率，各效果速度仍由各自参数控制。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_MainTexScaleOffset", "主纹理缩放/偏移", "用 Vector4 的 XY 设置缩放、ZW 设置 UV 偏移。", "向量");
            AddHelp(map, "ES/2D/Composite URP", "_SequenceFrame", "序列帧当前帧", "指定序列帧动画当前使用的帧索引。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_SequenceSpeed", "序列帧速度", "控制序列帧按场景时间自动推进的速度。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_FadeMode", "渐隐模式", "选择无、方向遮罩、纹理遮罩或噪声溶解。", "枚举");
            AddHelp(map, "ES/2D/Composite URP", "_FadeProgress", "渐隐进度", "控制渐隐/遮罩/溶解效果推进到的归一化位置。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_FadeMask", "渐隐遮罩", "为遮罩模式提供逐像素的灰度控制纹理。", "纹理");
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
            AddHelp(map, "ES/2D/Composite URP", "_EnableNegative", "启用负片", "开启颜色反相效果。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableRainbow", "启用彩虹渐变", "开启沿坐标和时间变化的彩虹染色。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableInnerOutline", "启用内描边", "在精灵内部边缘绘制描边。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableOuterOutline", "启用外描边", "在精灵外部扩展透明区域绘制描边。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnablePixelOutline", "启用像素描边", "使用像素宽度绘制硬边描边。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableShine", "启用扫光", "开启沿指定角度移动的扫光带。", "开关");
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
            AddHelp(map, "ES/2D/Composite URP", "_EnableDistortion", "启用噪声扰动", "开启噪声驱动的 UV 扰动。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_DistortionStrength", "扰动强度", "控制噪声扰动造成的 UV 偏移量。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableHologram", "启用全息", "开启扫描线与最低透明度控制的全息效果。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_HologramColor", "全息颜色", "设置全息覆盖层的颜色。", "颜色");
            AddHelp(map, "ES/2D/Composite URP", "_EnableGlitch", "启用故障", "开启基于坐标和时间的随机 UV 横向抖动。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_GlitchIntensity", "故障强度", "控制故障效果的最大 UV 偏移。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableFrozen", "启用冰冻", "开启冰冻颜色和冰晶高光效果。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableBurn", "启用燃烧", "开启按噪声推进的燃烧边缘和裁剪。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_BurnProgress", "燃烧进度", "控制燃烧边缘在噪声场中的推进位置。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnablePoison", "启用中毒", "开启周期性中毒染色效果。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BaseMap", "基础颜色纹理", "URP Lit 表面的基础颜色采样纹理。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BaseColor", "基础颜色", "URP Lit 表面的基础颜色和透明度。", "颜色");
            AddHelp(map, "ES/3D/Lit Composite URP", "_NormalMap", "法线纹理", "改变光照法线方向的法线贴图。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_UseNormalMap", "启用法线纹理", "开启后才采样法线纹理；关闭时使用顶点法线并节省一次纹理采样。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_Metallic", "金属度", "控制表面从绝缘体到金属的反射响应。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_Smoothness", "光滑度", "控制高光的锐利程度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_UseEmission", "启用自发光", "开启后才采样并叠加自发光纹理。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_DissolveMode", "溶解模式", "选择噪声溶解或距离溶解算法；需要标准或高质量档。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_DissolveProgress", "溶解进度", "控制模型被溶解掉的归一化进度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableRim", "启用边缘光", "按视角边缘为模型增加轮廓光；需要标准或高质量档。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_RimIntensity", "边缘光强度", "控制轮廓光的叠加强度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableShine", "启用扫光", "开启沿模型表面移动的扫光高光；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_ShineIntensity", "扫光强度", "控制扫光高光的叠加强度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableSparkle", "启用亮晶晶", "在高质量档位下开启程序化闪点。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableFlow", "启用纹理流动", "沿顶点 UV 推进主纹理采样。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableChromatic", "启用色差", "对基础颜色纹理执行轻量 RGB 分离。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableBlur", "启用纹理模糊", "对基础颜色纹理执行轻量五点模糊。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BlurRadius", "模糊半径", "控制 Lit 基础颜色纹理的采样偏移。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BlurIntensity", "模糊强度", "控制 Lit 基础颜色的柔化比例。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableBurn", "启用燃烧边缘", "开启溶解/燃烧交界处的边缘着色；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_AlphaClip", "启用透明裁剪", "按 Cutoff 阈值丢弃低透明度像素。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_QualityTier", "效果质量档位", "基础保留 Lit 主体；标准启用形变、流向、溶解和边缘光；高质量再启用扫光、闪点和燃烧边缘。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率，各效果速度仍由各自参数控制。", "浮点/范围");
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
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableFlow", "启用纹理流动", "按时间推进 VFX 主纹理 UV。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableShine", "启用扫光", "开启沿 VFX 卡片高度方向移动的扫光；需要标准或高质量档。", "开关");
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
            AddHelp(map, "ES/3D/VFX Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率，各效果速度仍由各自参数控制。", "浮点/范围");
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
            AddHelp(map, "ES/UI/Composite URP", "_EnableHologram", "UI 全息开关", "在 UI 上叠加动态扫描线。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableGlitch", "UI 故障开关", "在 UI 上叠加随机横向抖动。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableFlow", "UI 纹理流动", "按时间推进 UI 主纹理 UV。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableShine", "UI 扫光", "在 UI 表面叠加可控方向的扫光。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSparkle", "UI 亮晶晶", "在 UI 上叠加程序化闪点。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableChromatic", "UI 色差", "对 UI 主纹理执行轻量 RGB 分离。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableBlur", "UI 纹理模糊", "对 UI 主纹理执行轻量五点模糊，不等于背景毛玻璃。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_BlurRadius", "UI 模糊半径", "控制 UI 主纹理的采样偏移。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_BlurIntensity", "UI 模糊强度", "控制 UI 主纹理的柔化比例。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_GlitchSpeed", "故障速度", "控制 UI 故障图样随时间变化的速度。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_AlphaClip", "UI 透明裁剪", "按阈值裁剪 UI 像素。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/UI/Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率，各效果速度仍由各自参数控制。", "浮点/范围");
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
