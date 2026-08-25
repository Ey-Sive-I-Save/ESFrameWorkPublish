Shader "ES/UI/Composite URP"
{
    Properties
    {
        [HideInInspector] _ESMaterialVersion ("ES Material Version", Float) = 1
        // Base Input
        [PerRendererData] _MainTex ("主纹理", 2D) = "white" {}
        _Color ("颜色", Color) = (1,1,1,1)
        _VertexColorStrength ("顶点色强度", Range(0,1)) = 1

        // SDF Text
        [Toggle] _EnableSDF ("启用 SDF 字体", Float) = 0
        _SDFThreshold ("SDF 字面阈值", Range(0,1)) = 0.5
        _SDFSoftness ("SDF 边缘柔和度", Range(0.25,4)) = 1
        _SDFOutlineWidth ("SDF 描边宽度", Range(0,0.5)) = 0
        _SDFOutlineSoftness ("SDF 描边柔和度", Range(0.25,4)) = 1
        [HDR] _SDFOutlineColor ("SDF 描边颜色", Color) = (0,0,0,1)
        _SDFGlowWidth ("SDF 辉光宽度", Range(0,0.5)) = 0
        [HDR] _SDFGlowColor ("SDF 辉光颜色", Color) = (0,0,0,0)

        // TextMeshPro Compatibility
        [Toggle] _EnableTMPCompatibility ("启用 TMP 材质合同", Float) = 0
        [HDR] _FaceColor ("TMP 字面颜色", Color) = (1,1,1,1)
        _FaceDilate ("TMP 字面扩张", Range(-1,1)) = 0
        [HDR] _OutlineColor ("TMP 描边颜色", Color) = (0,0,0,1)
        _OutlineWidth ("TMP 描边宽度", Range(0,1)) = 0
        _OutlineSoftness ("TMP 描边柔和度", Range(0,1)) = 0
        [Toggle] _EnableUnderlay ("启用 TMP 底衬", Float) = 0
        [HDR] _UnderlayColor ("TMP 底衬颜色", Color) = (0,0,0,0.5)
        _UnderlayOffsetX ("TMP 底衬 X 偏移", Range(-1,1)) = 0
        _UnderlayOffsetY ("TMP 底衬 Y 偏移", Range(-1,1)) = 0
        _UnderlayDilate ("TMP 底衬扩张", Range(-1,1)) = 0
        _UnderlaySoftness ("TMP 底衬柔和度", Range(0,1)) = 0
        _WeightNormal ("TMP 常规字重", Float) = 0
        _WeightBold ("TMP 粗体字重", Float) = 0.5
        [HideInInspector] _ScaleRatioA ("TMP Scale Ratio A", Float) = 1
        [HideInInspector] _ScaleRatioB ("TMP Scale Ratio B", Float) = 1
        [HideInInspector] _ScaleRatioC ("TMP Scale Ratio C", Float) = 1
        [HideInInspector] _GradientScale ("TMP Gradient Scale", Float) = 5
        [HideInInspector] _Sharpness ("TMP Sharpness", Range(-1,1)) = 0
        [HideInInspector] _TextureWidth ("TMP Texture Width", Float) = 512
        [HideInInspector] _TextureHeight ("TMP Texture Height", Float) = 512
        [HideInInspector] _ScaleX ("TMP Scale X", Float) = 1
        [HideInInspector] _ScaleY ("TMP Scale Y", Float) = 1
        [HideInInspector] _PerspectiveFilter ("TMP Perspective Filter", Range(0,1)) = 0.875
        [HideInInspector] _VertexOffsetX ("TMP Vertex Offset X", Float) = 0
        [HideInInspector] _VertexOffsetY ("TMP Vertex Offset Y", Float) = 0
        [HideInInspector] _MaskSoftnessX ("TMP Mask Softness X", Float) = 0
        [HideInInspector] _MaskSoftnessY ("TMP Mask Softness Y", Float) = 0
        [HideInInspector] _UIMaskSoftnessX ("UI Mask Softness X", Float) = 0
        [HideInInspector] _UIMaskSoftnessY ("UI Mask Softness Y", Float) = 0
        [HideInInspector] _ShaderFlags ("TMP Shader Flags", Float) = 0
        [HideInInspector] _ClipRect ("TMP Clip Rect", Vector) = (-32767,-32767,32767,32767)
        [HideInInspector] _CullMode ("TMP Cull Mode", Float) = 0

        // Color Remapping
        [Toggle] _EnableRecolorRGB ("启用 RGB 重映色", Float) = 0
        [HDR] _RecolorRed ("红色通道目标色", Color) = (1,0,0,1)
        [HDR] _RecolorGreen ("绿色通道目标色", Color) = (0,1,0,1)
        [HDR] _RecolorBlue ("蓝色通道目标色", Color) = (0,0,1,1)
        _RecolorRGBStrength ("RGB 重映色强度", Range(0,1)) = 1
        [Toggle] _RecolorRGBMaskToggle ("使用 RGB 重映色遮罩", Float) = 0
        [NoScaleOffset] _RecolorRGBMask ("RGB 重映色遮罩", 2D) = "white" {}
        [Enum(Red,0,Green,1,Blue,2,Alpha,3)] _RecolorRGBMaskChannel ("RGB 遮罩通道", Float) = 3
        [Toggle] _EnableRecolorRGBYCP ("启用 RGBYCP 重映色", Float) = 0
        [HDR] _RecolorRGBYCPRed ("RGBYCP 红色目标色", Color) = (1,0,0,1)
        [HDR] _RecolorRGBYCPGreen ("RGBYCP 绿色目标色", Color) = (0,1,0,1)
        [HDR] _RecolorRGBYCPBlue ("RGBYCP 蓝色目标色", Color) = (0,0,1,1)
        [HDR] _RecolorRGBYCPYellow ("RGBYCP 黄色目标色", Color) = (1,1,0,1)
        [HDR] _RecolorRGBYCPCyan ("RGBYCP 青色目标色", Color) = (0,1,1,1)
        [HDR] _RecolorRGBYCPPurple ("RGBYCP 紫色目标色", Color) = (1,0,1,1)
        _RecolorRGBYCPStrength ("RGBYCP 重映色强度", Range(0,1)) = 1
        [Toggle] _RecolorRGBYCPMaskToggle ("使用 RGBYCP 重映色遮罩", Float) = 0
        [NoScaleOffset] _RecolorRGBYCPMask ("RGBYCP 重映色遮罩", 2D) = "white" {}
        [Enum(Red,0,Green,1,Blue,2,Alpha,3)] _RecolorRGBYCPMaskChannel ("RGBYCP 遮罩通道", Float) = 3

        // Color Adjustments
        [Toggle] _EnableAddColor ("启用叠加颜色", Float) = 0
        [HDR] _AddColor ("叠加颜色", Color) = (1,0,0,1)
        _AddColorFade ("叠加颜色强度", Range(0,1)) = 1
        [Toggle] _AddColorContrastToggle ("叠加颜色使用对比度", Float) = 0
        _AddColorContrast ("叠加颜色对比度", Float) = 0.5
        [Toggle] _AddColorMaskToggle ("叠加颜色使用遮罩", Float) = 0
        _AddColorMask ("叠加颜色遮罩", 2D) = "white" {}
        [Toggle] _EnableStrongTint ("启用强制染色", Float) = 0
        [HDR] _StrongTint ("强制染色", Color) = (1,1,1,1)
        _StrongTintFade ("强制染色强度", Range(0,1)) = 1
        [Toggle] _StrongTintContrastToggle ("强制染色使用对比度", Float) = 0
        _StrongTintContrast ("强制染色对比度", Float) = 0
        [Toggle] _StrongTintMaskToggle ("强制染色使用遮罩", Float) = 0
        _StrongTintMask ("强制染色遮罩", 2D) = "white" {}
        [Toggle] _EnableAlphaTint ("启用透明染色", Float) = 0
        [HDR] _AlphaTint ("透明染色", Color) = (1,1,1,1)
        _AlphaTintMin ("透明染色最低透明度", Range(0,1)) = 0.02
        _AlphaTintFade ("透明染色强度", Range(0,1)) = 1
        [Toggle] _EnableColorReplace ("启用颜色替换", Float) = 0
        _ReplaceFrom ("替换源颜色", Color) = (0,0,0,1)
        [HDR] _ReplaceTo ("替换目标颜色", Color) = (1,1,1,1)
        _ReplaceRange ("替换范围", Range(0,1)) = 0.1
        _ReplaceSoftness ("替换柔和度", Range(0.001,1)) = 0.1
        _ReplaceContrast ("替换亮度对比", Range(0.001,8)) = 1
        _ReplaceFade ("替换强度", Range(0,1)) = 1
        [Toggle] _EnableBrightness ("启用亮度", Float) = 0
        _Brightness ("亮度", Range(0,4)) = 1
        [Toggle] _EnableContrast ("启用对比度", Float) = 0
        _Contrast ("对比度", Range(0,4)) = 1
        [Toggle] _EnableSaturation ("启用饱和度", Float) = 0
        _Saturation ("饱和度", Range(0,4)) = 1
        [Toggle] _EnableHue ("启用色相偏移", Float) = 0
        _Hue ("色相偏移", Range(-1,1)) = 0
        [Toggle] _EnableSplitToning ("启用分离色调", Float) = 0
        [HDR] _SplitToneShadows ("阴影色调", Color) = (1,1,1,1)
        [HDR] _SplitToneHighlights ("高光色调", Color) = (1,1,1,1)
        _SplitToneBalance ("分离色调平衡", Range(-1,1)) = 0
        _SplitToneStrength ("分离色调强度", Range(0,1)) = 1
        _SplitToneContrast ("分离色调对比", Range(0.001,8)) = 1
        _SplitToneShift ("分离色调亮度偏移", Range(-1,1)) = 0
        [Toggle] _EnableBlackTint ("启用暗部染色", Float) = 0
        _BlackTintFade ("暗部染色强度", Range(0,1)) = 1
        [HDR] _BlackTintColor ("暗部染色颜色", Color) = (0,0,1,0)
        _BlackTintPower ("暗部染色幂次", Range(0.001,16)) = 4
        [Toggle] _EnableInkSpread ("启用墨水扩散", Float) = 0
        _InkSpreadFade ("墨水扩散强度", Range(0,1)) = 1
        [HDR] _InkSpreadColor ("墨水扩散颜色", Color) = (8.47419,5.013525,0.08873497,0)
        _InkSpreadContrast ("墨水扩散对比度", Range(0.001,8)) = 2
        _InkSpreadDistance ("墨水扩散距离", Float) = 3
        _InkSpreadPosition ("墨水扩散中心", Vector) = (0.5,-1,0,0)
        _InkSpreadWidth ("墨水扩散宽度", Range(0.001,4)) = 0.2
        _InkSpreadNoiseScale ("墨水噪声缩放", Vector) = (0.4,0.4,0,0)
        _InkSpreadNoiseFactor ("墨水噪声影响", Range(0,4)) = 0.5
        [Toggle] _EnableShiftHue ("启用动态色相偏移", Float) = 0
        _ShiftHueSpeed ("动态色相速度", Float) = 0.5
        [Toggle] _EnableAddHue ("启用动态色相叠加", Float) = 0
        _AddHueFade ("动态色相叠加强度", Range(0,1)) = 1
        _AddHueSpeed ("动态色相叠加速度", Float) = 1
        _AddHueBrightness ("动态色相叠加亮度", Range(0,16)) = 2
        _AddHueSaturation ("动态色相叠加饱和度", Range(0,1)) = 1
        _AddHueContrast ("动态色相叠加对比度", Range(0.001,8)) = 0.5
        [Toggle] _AddHueMaskToggle ("使用动态色相叠加遮罩", Float) = 0
        _AddHueMask ("动态色相叠加遮罩", 2D) = "white" {}
        [Toggle] _EnableSineGlow ("启用正弦辉光", Float) = 0
        _SineGlowFade ("正弦辉光强度", Range(0,1)) = 1
        [HDR] _SineGlowColor ("正弦辉光颜色", Color) = (0,2.007843,2.996078,0)
        _SineGlowContrast ("正弦辉光对比度", Range(0.001,8)) = 1
        _SineGlowFrequency ("正弦辉光频率", Float) = 4
        _SineGlowMin ("正弦辉光下限", Float) = 0
        _SineGlowMax ("正弦辉光上限", Float) = 1
        [Toggle] _SineGlowMaskToggle ("使用正弦辉光遮罩", Float) = 0
        _SineGlowMask ("正弦辉光遮罩", 2D) = "white" {}

        // ESNative Pattern And Material Effects
        [Toggle] _EnableCamouflage ("启用迷彩", Float) = 0
        _CamouflageFade ("迷彩强度", Range(0,1)) = 1
        _CamouflageBaseColor ("迷彩基础颜色", Color) = (0.7450981,0.7254902,0.5686275,0)
        _CamouflageContrast ("迷彩对比度", Float) = 1
        _CamouflageColorA ("迷彩颜色 A", Color) = (0.627451,0.5882353,0.4313726,0)
        _CamouflageDensityA ("迷彩密度 A", Range(0,1)) = 0.4
        _CamouflageSmoothnessA ("迷彩柔和度 A", Range(0,1)) = 0.2
        _CamouflageNoiseScaleA ("迷彩噪声缩放 A", Vector) = (0.25,0.25,0,0)
        _CamouflageColorB ("迷彩颜色 B", Color) = (0.4705882,0.4313726,0.3137255,0)
        _CamouflageDensityB ("迷彩密度 B", Range(0,1)) = 0.4
        _CamouflageSmoothnessB ("迷彩柔和度 B", Range(0,1)) = 0.2
        _CamouflageNoiseScaleB ("迷彩噪声缩放 B", Vector) = (0.25,0.25,0,0)
        [Toggle] _CamouflageAnimationToggle ("启用迷彩动画", Float) = 0
        _CamouflageDistortionSpeed ("迷彩扰动速度", Vector) = (0.1,0.1,0,0)
        _CamouflageDistortionIntensity ("迷彩扰动强度", Vector) = (0.1,0.1,0,0)
        _CamouflageDistortionScale ("迷彩扰动缩放", Vector) = (0.5,0.5,0,0)
        [Toggle] _EnableMetal ("启用金属着色", Float) = 0
        _MetalFade ("金属着色强度", Range(0,1)) = 1
        [HDR] _MetalColor ("金属基础颜色", Color) = (5.992157,3.639216,0.3137255,1)
        _MetalContrast ("金属对比度", Float) = 2
        [HDR] _MetalHighlightColor ("金属高光颜色", Color) = (5.992157,3.796078,0.6588235,1)
        _MetalHighlightDensity ("金属高光密度", Range(0,1)) = 1
        _MetalHighlightContrast ("金属高光对比度", Float) = 2
        _MetalNoiseScale ("金属噪声缩放", Vector) = (0.25,0.25,0,0)
        _MetalNoiseSpeed ("金属噪声速度", Vector) = (0.05,0.05,0,0)
        _MetalNoiseDistortionScale ("金属噪声扰动缩放", Vector) = (0.2,0.2,0,0)
        _MetalNoiseDistortionSpeed ("金属噪声扰动速度", Vector) = (-0.05,-0.05,0,0)
        _MetalNoiseDistortion ("金属噪声扰动强度", Vector) = (0.5,0.5,0,0)
        [Toggle] _MetalMaskToggle ("使用金属遮罩", Float) = 0
        [NoScaleOffset] _MetalMask ("金属遮罩", 2D) = "white" {}
        [Toggle] _EnableEnchanted ("启用附魔流光", Float) = 0
        _EnchantedFade ("附魔流光强度", Range(0,1)) = 1
        _EnchantedSpeed ("附魔流光速度", Vector) = (0,1,0,0)
        _EnchantedScale ("附魔噪声缩放", Vector) = (0.1,0.1,0,0)
        _EnchantedBrightness ("附魔流光亮度", Float) = 1
        _EnchantedContrast ("附魔流光对比度", Float) = 0.5
        _EnchantedReduce ("附魔流光削减", Range(0,2)) = 0
        [Toggle] _EnchantedRainbowToggle ("附魔使用彩虹", Float) = 0
        _EnchantedRainbowSpeed ("附魔彩虹速度", Float) = 0.5
        _EnchantedRainbowDensity ("附魔彩虹密度", Float) = 0.5
        _EnchantedRainbowSaturation ("附魔彩虹饱和度", Float) = 0.8
        [HDR] _EnchantedLowColor ("附魔低值颜色", Color) = (2.996078,0,0,0)
        [HDR] _EnchantedHighColor ("附魔高值颜色", Color) = (0,0.7098798,4.237095,0)
        [Toggle] _EnchantedLerpToggle ("附魔使用替换混合", Float) = 0
        [Toggle] _EnableShifting ("启用明度流变", Float) = 0
        _ShiftingFade ("明度流变强度", Range(0,1)) = 1
        _ShiftingSpeed ("明度流变速度", Float) = 0.5
        _ShiftingDensity ("明度流变密度", Float) = 1.5
        _ShiftingBrightness ("明度流变亮度", Float) = 1
        _ShiftingContrast ("明度流变对比度", Float) = 0.5
        [Toggle] _ShiftingRainbowToggle ("明度流变使用彩虹", Float) = 0
        _ShiftingSaturation ("明度流变饱和度", Float) = 0.8
        [HDR] _ShiftingColorA ("明度流变颜色 A", Color) = (1.498039,0,0,0)
        [HDR] _ShiftingColorB ("明度流变颜色 B", Color) = (1.498039,0.7490196,0,0)
        [Toggle] _EnableNegative ("启用负片", Float) = 0
        _NegativeFade ("负片强度", Range(0,1)) = 1
        [Toggle] _EnableRainbow ("启用彩虹渐变", Float) = 0
        _RainbowSpeed ("彩虹速度", Float) = 1
        _RainbowDensity ("彩虹密度", Float) = 1
        _RainbowDirection ("彩虹色带方向", Vector) = (0,1,0,0)
        _RainbowBrightness ("彩虹亮度", Float) = 1
        [HideInInspector] _RainbowFade ("ESNative 彩虹强度", Range(0,1)) = 1
        [HideInInspector] _RainbowSaturation ("ESNative 彩虹饱和度", Range(0,1)) = 1
        [HideInInspector] _RainbowContrast ("ESNative 彩虹对比度", Float) = 1
        [HideInInspector] _RainbowCenter ("ESNative 彩虹中心", Vector) = (0,0,0,0)
        [HideInInspector] _RainbowNoiseScale ("ESNative 彩虹噪声缩放", Vector) = (0.2,0.2,0,0)
        [HideInInspector] _RainbowNoiseFactor ("ESNative 彩虹噪声因子", Float) = 0.2
        [Toggle] _EnableShadow ("启用精灵阴影", Float) = 0
        _ShadowFade ("精灵阴影强度", Range(0,1)) = 1
        _ShadowOffset ("精灵阴影偏移", Vector) = (0.05,-0.05,0,0)
        _ShadowColor ("精灵阴影颜色", Color) = (0,0,0,0)

        // UV Transform
        [Toggle] _EnableUVTransform ("启用 UV 变换", Float) = 0
        _UVPivot ("UV 变换中心", Vector) = (0.5,0.5,0,0)
        _UVScale ("UV 缩放", Vector) = (1,1,0,0)
        _UVOffset ("UV 偏移", Vector) = (0,0,0,0)
        _UVRotation ("UV 旋转", Range(-360,360)) = 0
        _UVRotationSpeed ("UV 旋转速度", Float) = 0
        [Toggle] _EnableUVDistort ("启用 UV 扰动", Float) = 0
        _UVDistortFrequency ("UV 扰动频率", Vector) = (1,1,0,0)
        _UVDistortSpeed ("UV 扰动速度", Vector) = (0,0,0,0)
        _UVDistortAmount ("UV 扰动强度", Range(0,0.2)) = 0
        [NoScaleOffset] _UVDistortNoiseTex ("UV 扰动噪声纹理", 2D) = "gray" {}
        _UVDistortFrom ("UV 扰动起始偏移", Vector) = (-0.02,-0.02,0,0)
        _UVDistortTo ("UV 扰动目标偏移", Vector) = (0.02,0.02,0,0)
        _UVDistortFade ("UV 扰动淡入", Range(0,1)) = 1
        [Toggle] _UVDistortMaskToggle ("使用 UV 扰动遮罩", Float) = 0
        [NoScaleOffset] _UVDistortMask ("UV 扰动遮罩", 2D) = "white" {}
        [Enum(Red,0,Green,1,Blue,2,Alpha,3)] _UVDistortMaskChannel ("UV 扰动遮罩通道", Float) = 3

        // ESNative UV Motion
        [Toggle] _EnableSqueeze ("启用径向挤压", Float) = 0
        _SqueezeFade ("径向挤压强度", Range(0,1)) = 1
        _SqueezeScale ("径向挤压缩放", Vector) = (2,0,0,0)
        _SqueezePower ("径向挤压幂次", Float) = 1
        _SqueezeCenter ("径向挤压中心", Vector) = (0.5,0.5,0,0)
        [Toggle] _EnableSineRotate ("启用正弦旋转", Float) = 0
        _SineRotateFade ("正弦旋转强度", Range(0,1)) = 1
        _SineRotateAngle ("正弦旋转角度", Float) = 15
        _SineRotateFrequency ("正弦旋转频率", Float) = 1
        _SineRotatePivot ("正弦旋转中心", Vector) = (0.5,0.5,0,0)

        // Masks And Dissolve
        _FadeMode ("渐隐模式", Float) = 0
        _FadeProgress ("渐隐进度", Range(0,1)) = 0
        _FadePosition ("渐隐位置", Vector) = (0.5,0.5,0,0)
        _FadeRotation ("渐隐方向", Range(0,360)) = 0
        _FadeWidth ("渐隐宽度", Range(0.001,1)) = 0.1
        [Toggle] _FadeInvert ("反转渐隐", Float) = 0
        _FadeNoiseFactor ("渐隐噪声影响", Range(0,1)) = 0.2
        _FadeNoiseScale ("渐隐噪声缩放", Vector) = (4,4,0,0)
        _FadeNoiseSpeed ("渐隐噪声速度", Vector) = (0,0,0,0)
        [NoScaleOffset] _FadeNoiseTex ("渐隐噪声纹理", 2D) = "gray" {}
        [NoScaleOffset] _FadeMask ("渐隐遮罩", 2D) = "white" {}
        [HDR] _DissolveEdgeColor ("溶解边缘颜色", Color) = (1,0.15,0.01,1)
        _DissolveEdgeWidth ("溶解边缘宽度", Range(0.001,1)) = 0.08
        _DissolveEdgeIntensity ("溶解边缘强度", Range(0,8)) = 1
        _FadeDistortionStrength ("方向扰动强度", Range(0,0.2)) = 0.03

        // ESNative Alpha And Dissolve
        // ESNative Composable Fade Stack
        [Toggle] _EnableFullAlphaDissolve ("启用 ESNative 全局透明溶解", Float) = 0
        _FullAlphaDissolveFade ("全局透明溶解进度", Float) = 0.5
        _FullAlphaDissolveWidth ("全局透明溶解宽度", Float) = 0.5
        _FullAlphaDissolveNoiseScale ("全局透明溶解噪声缩放", Vector) = (0.1,0.1,0,0)
        [Toggle] _EnableSourceAlphaDissolve ("启用 ESNative 源点透明溶解", Float) = 0
        _SourceAlphaDissolveFade ("源点透明溶解进度", Float) = 1
        _SourceAlphaDissolvePosition ("源点透明溶解位置", Vector) = (0,0,0,0)
        _SourceAlphaDissolveWidth ("源点透明溶解宽度", Float) = 0.2
        _SourceAlphaDissolveNoiseScale ("源点透明溶解噪声缩放", Vector) = (0.3,0.3,0,0)
        _SourceAlphaDissolveNoiseFactor ("源点透明溶解噪声影响", Float) = 0.2
        [Toggle] _SourceAlphaDissolveInvert ("反转源点透明溶解", Float) = 0
        [Toggle] _EnableSourceGlowDissolve ("启用 ESNative 源点辉光溶解", Float) = 0
        _SourceGlowDissolveFade ("源点辉光溶解进度", Float) = 1
        _SourceGlowDissolvePosition ("源点辉光溶解位置", Vector) = (0,0,0,0)
        _SourceGlowDissolveWidth ("源点辉光溶解宽度", Float) = 0.1
        [HDR] _SourceGlowDissolveEdgeColor ("源点辉光溶解边缘颜色", Color) = (11.98431,0.627451,0.627451,0)
        _SourceGlowDissolveNoiseScale ("源点辉光溶解噪声缩放", Vector) = (0.3,0.3,0,0)
        _SourceGlowDissolveNoiseFactor ("源点辉光溶解噪声影响", Float) = 0.2
        [Toggle] _SourceGlowDissolveInvert ("反转源点辉光溶解", Float) = 0
        [Toggle] _EnableDirectionalAlphaFade ("启用 ESNative 方向透明渐隐", Float) = 0
        _DirectionalAlphaFadeFade ("方向透明渐隐进度", Float) = 0
        _DirectionalAlphaFadeRotation ("方向透明渐隐角度", Range(0,360)) = 0
        _DirectionalAlphaFadeWidth ("方向透明渐隐宽度", Float) = 0.2
        _DirectionalAlphaFadeNoiseScale ("方向透明渐隐噪声缩放", Vector) = (0.3,0.3,0,0)
        _DirectionalAlphaFadeNoiseFactor ("方向透明渐隐噪声影响", Float) = 0.2
        [Toggle] _DirectionalAlphaFadeInvert ("反转方向透明渐隐", Float) = 0
        [Toggle] _EnableDirectionalGlowFade ("启用 ESNative 方向辉光渐隐", Float) = 0
        _DirectionalGlowFadeFade ("方向辉光渐隐进度", Float) = 0
        _DirectionalGlowFadeRotation ("方向辉光渐隐角度", Range(0,360)) = 0
        [HDR] _DirectionalGlowFadeEdgeColor ("方向辉光渐隐边缘颜色", Color) = (11.98431,0.6901961,0.6901961,0)
        _DirectionalGlowFadeWidth ("方向辉光渐隐宽度", Float) = 0.1
        _DirectionalGlowFadeNoiseScale ("方向辉光渐隐噪声缩放", Vector) = (0.4,0.4,0,0)
        _DirectionalGlowFadeNoiseFactor ("方向辉光渐隐噪声影响", Float) = 0.2
        [Toggle] _DirectionalGlowFadeInvert ("反转方向辉光渐隐", Float) = 0
        [Toggle] _EnableDirectionalDistortion ("启用 ESNative 方向扰动渐隐", Float) = 0
        _DirectionalDistortionFade ("方向扰动渐隐进度", Float) = 0
        _DirectionalDistortionRotation ("方向扰动渐隐角度", Range(0,360)) = 0
        _DirectionalDistortionWidth ("方向扰动渐隐宽度", Float) = 0.5
        _DirectionalDistortionNoiseScale ("方向扰动渐隐噪声缩放", Vector) = (0.4,0.4,0,0)
        _DirectionalDistortionNoiseFactor ("方向扰动渐隐噪声影响", Float) = 0.2
        _DirectionalDistortionDistortion ("方向扰动向量", Vector) = (0,0.1,0,0)
        _DirectionalDistortionRandomDirection ("方向扰动随机角度", Range(0,1)) = 0.1
        _DirectionalDistortionDistortionScale ("方向扰动随机噪声缩放", Vector) = (1,1,0,0)
        [Toggle] _DirectionalDistortionInvert ("反转方向扰动渐隐", Float) = 0

        [Toggle] _EnableCustomFade ("启用自定义渐隐", Float) = 0
        _CustomFadeFadeMask ("自定义渐隐遮罩", 2D) = "white" {}
        _CustomFadeSmoothness ("自定义渐隐柔和度", Float) = 2
        _CustomFadeNoiseScale ("自定义渐隐噪声缩放", Vector) = (1,1,0,0)
        _CustomFadeNoiseFactor ("自定义渐隐噪声影响", Range(0,0.5)) = 0
        _CustomFadeAlpha ("自定义渐隐透明度", Range(0,1)) = 1
        [Toggle] _EnableFullGlowDissolve ("启用全局辉光溶解", Float) = 0
        _FullGlowDissolveFade ("全局辉光溶解进度", Range(0,1)) = 0.5
        _FullGlowDissolveWidth ("全局辉光溶解宽度", Float) = 0.5
        [HDR] _FullGlowDissolveEdgeColor ("全局辉光溶解边缘颜色", Color) = (11.98431,0.627451,0.627451,0)
        _FullGlowDissolveNoiseScale ("全局辉光溶解噪声缩放", Vector) = (0.1,0.1,0,0)

        // Stylization
        [Enum(LocalUV,0,World,1,Screen,2)] _TilingMode ("主纹理平铺空间", Float) = 0
        _WorldTilingScale ("世界平铺缩放", Vector) = (1,1,0,0)
        _WorldTilingOffset ("世界平铺偏移", Vector) = (0,0,0,0)
        _WorldTilingPixelsPerUnit ("世界平铺每单位重复数", Range(0.01,64)) = 1
        _ScreenTilingScale ("屏幕平铺缩放", Vector) = (1,1,0,0)
        _ScreenTilingOffset ("屏幕平铺偏移", Vector) = (0,0,0,0)
        _ScreenTilingPixelsPerUnit ("屏幕平铺像素尺寸", Range(1,2048)) = 128
        [Toggle] _EnableSmoothPixelArt ("启用平滑像素画", Float) = 0
        _SmoothPixelStrength ("平滑像素画强度", Range(0,1)) = 1
        [Toggle] _EnablePixelate ("启用像素化", Float) = 0
        _PixelateCells ("横向像素格数", Range(2,512)) = 64
        _PixelateStrength ("像素化强度", Range(0,1)) = 1
        [Toggle] _EnableCheckerboard ("启用棋盘格", Float) = 0
        _CheckerboardDarken ("棋盘格暗格保留亮度", Range(0,1)) = 0.5
        _CheckerboardTiling ("棋盘格密度", Range(0.01,64)) = 1
        [NoScaleOffset] _UberNoiseTexture ("Generated 效果噪声", 2D) = "white" {}
        [Toggle] _EnableFlame ("启用火焰", Float) = 0
        _FlameBrightness ("火焰亮度", Range(0,16)) = 10
        _FlameSmooth ("火焰柔和度", Range(0,8)) = 2
        _FlameRadius ("火焰半径", Range(0.01,1)) = 0.2
        _FlameCenter ("火焰中心", Vector) = (0.5,0.4,0,0)
        _FlameDirection ("火焰方向", Vector) = (0,1,0,0)
        _FlameSpeed ("火焰速度", Vector) = (0,-0.5,0,0)
        _FlameNoiseFactor ("火焰噪声影响", Range(0,8)) = 2.5
        _FlameNoiseHeightFactor ("火焰高度影响", Range(0,4)) = 1.5
        _FlameNoiseScale ("火焰噪声缩放", Vector) = (1.2,0.8,0,0)
        [Toggle] _EnableSmoke ("启用烟雾", Float) = 0
        _SmokeAlpha ("烟雾透明度", Range(0,1)) = 1
        _SmokeSmoothness ("烟雾柔和度", Range(0,4)) = 1
        _SmokeNoiseScale ("烟雾噪声缩放", Range(0.01,8)) = 0.5
        _SmokeSpeed ("烟雾流动速度", Vector) = (0,0,0,0)
        _SmokeNoiseFactor ("烟雾噪声影响", Range(0,1)) = 0.4
        _SmokeDarkEdge ("烟雾暗边", Range(0,1.5)) = 1
        [Toggle] _SmokeVertexSeed ("烟雾使用顶点色种子", Float) = 0
        [Toggle] _EnablePalette ("启用调色板映射", Float) = 0
        [NoScaleOffset] _PaletteTex ("调色板纹理", 2D) = "white" {}
        _PaletteRow ("调色板采样行", Range(0,1)) = 0.5
        _PaletteStrength ("调色板强度", Range(0,1)) = 1
        [Toggle] _EnableHalftone ("启用半色调", Float) = 0
        _HalftoneScale ("半色调密度", Range(4,512)) = 96
        _HalftoneAngle ("半色调角度", Range(0,180)) = 45
        _HalftoneStrength ("半色调强度", Range(0,1)) = 0.75
        _HalftonePosition ("半色调中心", Vector) = (0,0,0,0)
        _HalftoneFade ("半色调扩散", Float) = 1
        _HalftoneFadeWidth ("半色调扩散宽度", Float) = 1.5
        [Toggle] _HalftoneInvert ("反转半色调", Float) = 0
        [Toggle] _HalftoneAlphaPattern ("使用 ESNative 透明点阵", Float) = 0

        // Texture Layers
        [Toggle] _EnableTextureLayer1 ("启用纹理层 1", Float) = 0
        _TextureLayer1Fade ("纹理层 1 淡入", Range(0,1)) = 1
        _TextureLayer1Texture ("纹理层 1 贴图", 2D) = "white" {}
        [HDR] _TextureLayer1Color ("纹理层 1 颜色", Color) = (1,1,1,1)
        _TextureLayer1Scale ("纹理层 1 缩放", Vector) = (1,1,0,0)
        _TextureLayer1Offset ("纹理层 1 偏移", Vector) = (0,0,0,0)
        [Toggle] _TextureLayer1ScrollToggle ("纹理层 1 滚动", Float) = 0
        _TextureLayer1ScrollSpeed ("纹理层 1 滚动速度", Vector) = (0,1,0,0)
        [Toggle] _TextureLayer1SheetToggle ("纹理层 1 序列帧", Float) = 0
        _TextureLayer1Columns ("纹理层 1 列数", Range(1,64)) = 1
        _TextureLayer1Rows ("纹理层 1 行数", Range(1,64)) = 1
        _TextureLayer1Speed ("纹理层 1 序列帧速度", Float) = 0
        _TextureLayer1StartFrame ("纹理层 1 起始帧", Float) = 0
        _TextureLayer1EdgeClip ("纹理层 1 边缘裁剪", Range(0,0.49)) = 0.005
        [Toggle] _TextureLayer1ContrastToggle ("纹理层 1 对比度", Float) = 0
        _TextureLayer1Contrast ("纹理层 1 对比度值", Range(0.01,4)) = 1
        [Toggle] _EnableTextureLayer2 ("启用纹理层 2", Float) = 0
        _TextureLayer2Fade ("纹理层 2 淡入", Range(0,1)) = 1
        _TextureLayer2Texture ("纹理层 2 贴图", 2D) = "white" {}
        [HDR] _TextureLayer2Color ("纹理层 2 颜色", Color) = (1,1,1,1)
        _TextureLayer2Scale ("纹理层 2 缩放", Vector) = (1,1,0,0)
        _TextureLayer2Offset ("纹理层 2 偏移", Vector) = (0,0,0,0)
        [Toggle] _TextureLayer2ScrollToggle ("纹理层 2 滚动", Float) = 0
        _TextureLayer2ScrollSpeed ("纹理层 2 滚动速度", Vector) = (0,1,0,0)
        [Toggle] _TextureLayer2SheetToggle ("纹理层 2 序列帧", Float) = 0
        _TextureLayer2Columns ("纹理层 2 列数", Range(1,64)) = 1
        _TextureLayer2Rows ("纹理层 2 行数", Range(1,64)) = 1
        _TextureLayer2Speed ("纹理层 2 序列帧速度", Float) = 0
        _TextureLayer2StartFrame ("纹理层 2 起始帧", Float) = 0
        _TextureLayer2EdgeClip ("纹理层 2 边缘裁剪", Range(0,0.49)) = 0.005
        [Toggle] _TextureLayer2ContrastToggle ("纹理层 2 对比度", Float) = 0
        _TextureLayer2Contrast ("纹理层 2 对比度值", Range(0.01,4)) = 1

        // Composite Outlines
        [Toggle] _EnableInnerOutline ("启用内描边", Float) = 0
        [HDR] _InnerOutlineColor ("内描边颜色", Color) = (1,0.2,0.05,1)
        _InnerOutlineWidth ("内描边宽度", Float) = 0.08
        [HideInInspector] _InnerOutlineFade ("ESNative 内描边淡入", Range(0,1)) = 1
        [HideInInspector][Toggle] _InnerOutlineDistortionToggle ("ESNative 内描边扰动", Float) = 0
        [HideInInspector] _InnerOutlineDistortionIntensity ("ESNative 内描边扰动强度", Vector) = (0.01,0.01,0,0)
        [HideInInspector] _InnerOutlineNoiseScale ("ESNative 内描边噪声缩放", Vector) = (4,4,0,0)
        [HideInInspector] _InnerOutlineNoiseSpeed ("ESNative 内描边噪声速度", Vector) = (0,0.1,0,0)
        [HideInInspector][Toggle] _InnerOutlineTextureToggle ("ESNative 内描边纹理着色", Float) = 0
        [HideInInspector] _InnerOutlineTintTexture ("ESNative 内描边纹理", 2D) = "white" {}
        [HideInInspector] _InnerOutlineTextureSpeed ("ESNative 内描边纹理速度", Vector) = (0.5,0,0,0)
        [HideInInspector][Toggle] _InnerOutlineOutlineOnlyToggle ("ESNative 仅显示内描边", Float) = 0
        [Toggle] _EnableOuterOutline ("启用外描边", Float) = 0
        [HDR] _OuterOutlineColor ("外描边颜色", Color) = (0,0,0,1)
        _OuterOutlineWidth ("外描边宽度", Float) = 0.005
        [HideInInspector] _OuterOutlineFade ("ESNative 外描边淡入", Range(0,1)) = 1
        [HideInInspector][Toggle] _OuterOutlineDistortionToggle ("ESNative 外描边扰动", Float) = 0
        [HideInInspector] _OuterOutlineDistortionIntensity ("ESNative 外描边扰动强度", Vector) = (0.01,0.01,0,0)
        [HideInInspector] _OuterOutlineNoiseScale ("ESNative 外描边噪声缩放", Vector) = (4,4,0,0)
        [HideInInspector] _OuterOutlineNoiseSpeed ("ESNative 外描边噪声速度", Vector) = (0,0.1,0,0)
        [HideInInspector][Toggle] _OuterOutlineTextureToggle ("ESNative 外描边纹理着色", Float) = 0
        [HideInInspector] _OuterOutlineTintTexture ("ESNative 外描边纹理", 2D) = "white" {}
        [HideInInspector] _OuterOutlineTextureSpeed ("ESNative 外描边纹理速度", Vector) = (0.5,0,0,0)
        [HideInInspector][Toggle] _OuterOutlineOutlineOnlyToggle ("ESNative 仅显示外描边", Float) = 0
        [Toggle] _EnablePixelOutline ("启用像素描边", Float) = 0
        [HDR] _PixelOutlineColor ("像素描边颜色", Color) = (1,1,1,1)
        _PixelOutlineWidth ("像素描边宽度", Float) = 1
        [HideInInspector] _PixelOutlineFade ("ESNative 像素描边淡入", Range(0,1)) = 1
        [HideInInspector][Toggle] _PixelOutlineTextureToggle ("ESNative 像素描边纹理着色", Float) = 0
        [HideInInspector] _PixelOutlineTintTexture ("ESNative 像素描边纹理", 2D) = "white" {}
        [HideInInspector] _PixelOutlineTextureSpeed ("ESNative 像素描边纹理速度", Vector) = (0.5,0,0,0)
        [HideInInspector][Toggle] _PixelOutlineOutlineOnlyToggle ("ESNative 仅显示像素描边", Float) = 0

        // Time And Coordinates
        _MainTexScaleOffset ("主纹理缩放/偏移", Vector) = (1,1,0,0)
        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        [HideInInspector] _SpriteUVTransformX ("Sprite UV Transform X", Vector) = (1,0,0,0)
        [HideInInspector] _SpriteUVTransformY ("Sprite UV Transform Y", Vector) = (0,1,0,0)
        [HideInInspector] _SpriteUVTransformValid ("Sprite UV Transform Valid", Float) = 0
        [Enum(SceneTime,0,UnscaledTime,1,CustomTime,2)] _TimeMode ("时间来源", Float) = 0
        _CustomTime ("自定义时间", Float) = 0
        _TimeScale ("时间倍率", Range(-4,4)) = 1
        [Toggle] _EnableTimeFPS ("启用时间帧率量化", Float) = 0
        _TimeFPS ("时间帧率", Range(0.01,240)) = 5
        [Toggle] _EnableTimeFrequency ("启用周期时间", Float) = 0
        _TimeFrequency ("时间周期频率", Float) = 2
        _TimeRange ("时间周期范围", Float) = 0.5

        // Vertex Motion
        [Toggle] _EnableWind ("启用风摆", Float) = 0
        _WindDirection ("风摆方向", Vector) = (1,0,0,0)
        _WindAmplitude ("风摆幅度", Range(0,64)) = 4
        _WindFrequency ("风摆频率", Range(0,32)) = 0.05
        _WindSpeed ("风摆速度", Range(0,8)) = 1
        _WindAnchor ("风摆固定边界", Range(0,1)) = 0
        _WindAnchorDirection ("风摆锚定方向", Vector) = (0,1,0,0)
        _WindGlobalInfluence ("全局风影响", Range(0,1)) = 1
        [Toggle] _EnableSquish ("启用挤压", Float) = 0
        _SquishAmount ("挤压幅度", Range(0,0.8)) = 0.15
        _SquishDirection ("挤压方向", Vector) = (1,0,0,0)
        _SquishSpeed ("挤压速度", Range(0,8)) = 2
        _SquishFade ("挤压强度", Range(0,1)) = 1
        [HideInInspector] _ESInteractiveWindRotation ("Interactive Wind Rotation", Float) = 0
        [HideInInspector] _ESInteractiveWindHeight ("Interactive Wind Height", Float) = 1
        [HideInInspector] _ESInteractiveSquish ("Interactive Squish", Float) = 0
        [HideInInspector] _ESWindPhaseOffset ("Wind Phase Offset", Float) = 0
        [Toggle] _EnableWiggle ("启用摇摆", Float) = 0
        _WiggleAmplitude ("摇摆角度", Range(0,45)) = 6
        _WiggleFrequency ("摇摆相位频率", Range(0,16)) = 2
        _WiggleDirection ("摇摆相位方向", Vector) = (0,1,0,0)
        _WiggleSpeed ("摇摆速度", Range(0,8)) = 2
        [Toggle] _EnableVibrate ("启用震动", Float) = 0
        _VibrateAmplitude ("震动幅度", Range(0,32)) = 2
        _VibrateDirection ("震动主方向", Vector) = (1,0,0,0)
        _VibrateSpeed ("震动速度", Range(0,32)) = 12
        [Toggle] _EnableSineMove ("启用正弦移动", Float) = 0
        _SineMoveFade ("正弦移动强度", Range(0,1)) = 1
        _SineMoveOffset ("正弦移动偏移", Vector) = (0,0.5,0,0)
        _SineMoveFrequency ("正弦移动频率", Vector) = (1,1,0,0)
        [Toggle] _EnableSineScale ("启用正弦缩放", Float) = 0
        _SineScaleFrequency ("正弦缩放频率", Float) = 2
        _SineScaleFactor ("正弦缩放幅度", Vector) = (0.2,0.2,0,0)
        // Dynamic Effects
        [Toggle] _EnableFlow ("启用纹理流动", Float) = 0
        _FlowSpeed ("流动速度", Vector) = (0,0,0,0)
        _FlowStrength ("流动强度", Range(0,1)) = 1
        [Toggle] _EnableShine ("启用扫光", Float) = 0
        [HDR] _ShineColor ("扫光颜色", Color) = (1,1,1,1)
        _ShineSpeed ("扫光速度", Float) = 1
        _ShineWidth ("扫光宽度", Float) = 0.15
        [Enum(CompatibleDefault,0,LocalUV,1,WorldProjection,2)] _ShineSpace ("扫光空间", Float) = 0
        _ShineDirection ("扫光方向", Vector) = (0,0,0,0)
        _ShineAngle ("扫光角度（方向为零时）", Range(0,360)) = 30
        _ShineIntensity ("扫光强度", Range(0,8)) = 1
        [HideInInspector] _ShineFade ("ESNative 扫光强度", Range(0,1)) = 1
        [HideInInspector] _ShineSaturation ("ESNative 扫光饱和度", Range(0,1)) = 0.5
        [HideInInspector] _ShineContrast ("ESNative 扫光对比度", Float) = 2
        [HideInInspector] _ShineRotation ("ESNative 扫光旋转", Range(0,360)) = 30
        [HideInInspector] _ShineSmooth ("ESNative 扫光平滑度", Float) = 1
        [HideInInspector] _ShineFrequency ("ESNative 扫光频率", Float) = 0.3
        [Toggle] _ShineMaskToggle ("ESNative 扫光遮罩", Float) = 0
        [NoScaleOffset] _ShineMask ("ESNative 扫光遮罩纹理", 2D) = "white" {}
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
        [Enum(Light5Tap,0,Gaussian3x3,1)] _BlurMode ("模糊核", Float) = 0
        [Toggle] _EnableSharpen ("启用锐化", Float) = 0
        _SharpenAmount ("锐化强度", Range(0,4)) = 1
        _SharpenRadius ("锐化半径", Range(0,0.02)) = 0.001
        _SharpenThreshold ("锐化阈值", Range(0,0.5)) = 0.02
        _SharpenFade ("锐化强度淡入", Range(0,1)) = 1
        [Toggle] _EnablePingPongGlow ("启用往返发光", Float) = 0
        [HDR] _GlowFrom ("往返发光起点", Color) = (1,0,0,1)
        [HDR] _GlowTo ("往返发光终点", Color) = (0,0.3,1,1)
        _GlowFrequency ("往返发光频率", Float) = 2
        _GlowIntensity ("往返发光强度", Range(0,8)) = 1
        _GlowContrast ("往返发光亮度对比", Range(0.001,8)) = 1
        _GlowFade ("往返发光淡入", Range(0,1)) = 1
        [Toggle] _EnableFullDistortion ("启用 ESNative 全局扰动", Float) = 0
        _FullDistortionFade ("全局扰动淡出", Range(0,1)) = 1
        _FullDistortionDistortion ("全局扰动方向强度", Vector) = (0.2,0.2,0,0)
        _FullDistortionNoiseScale ("全局扰动噪声缩放", Vector) = (0.5,0.5,0,0)
        [Toggle] _EnableHologram ("启用全息", Float) = 0
        [HDR] _HologramColor ("全息颜色", Color) = (0.1,0.8,1,1)
        _HologramFrequency ("全息线频率", Float) = 60
        _HologramSpeed ("全息速度", Float) = 1
        [HideInInspector] _HologramFade ("ESNative 全息淡入", Range(0,1)) = 1
        [HideInInspector] _HologramContrast ("ESNative 全息对比度", Float) = 1
        [Enum(LocalUV,0,WorldProjection,1)] _HologramSpace ("全息扫描空间", Float) = 1
        _HologramDirection ("全息扫描方向", Vector) = (0,1,0,0)
        [HideInInspector] _HologramLineFrequency ("ESNative 全息线频率", Float) = 500
        [HideInInspector] _HologramLineGap ("ESNative 全息线间隔", Float) = 3
        [HideInInspector] _HologramMinAlpha ("ESNative 全息最低透明度", Range(0,1)) = 0.2
        [HideInInspector] _HologramDistortionOffset ("ESNative 全息扰动偏移", Float) = 0.5
        _HologramDistortionDirection ("全息扰动方向", Vector) = (1,0,0,0)
        [HideInInspector] _HologramDistortionSpeed ("ESNative 全息扰动速度", Float) = 2
        [HideInInspector] _HologramDistortionDensity ("ESNative 全息扰动密度", Float) = 0.5
        [HideInInspector] _HologramDistortionScale ("ESNative 全息扰动缩放", Float) = 10
        [Toggle] _EnableGlitch ("启用故障", Float) = 0
        _GlitchAmount ("故障强度", Range(0,0.2)) = 0.02
        _GlitchSpeed ("故障速度", Float) = 3
        _GlitchScanDirection ("故障条带方向", Vector) = (0,1,0,0)
        [HideInInspector] _GlitchFade ("ESNative 故障淡入", Range(0,1)) = 1
        [HideInInspector] _GlitchMaskMin ("ESNative 故障遮罩下限", Range(0,1)) = 0.4
        [HideInInspector] _GlitchMaskScale ("ESNative 故障遮罩缩放", Vector) = (0,0.2,0,0)
        [HideInInspector] _GlitchMaskSpeed ("ESNative 故障遮罩速度", Vector) = (0,4,0,0)
        [HideInInspector] _GlitchHueSpeed ("ESNative 故障色相速度", Float) = 1
        [HideInInspector] _GlitchBrightness ("ESNative 故障亮度", Float) = 4
        [HideInInspector] _GlitchNoiseScale ("ESNative 故障噪声缩放", Vector) = (0,3,0,0)
        [HideInInspector] _GlitchNoiseSpeed ("ESNative 故障噪声速度", Vector) = (0,1,0,0)
        [HideInInspector] _GlitchDistortion ("ESNative 故障位移", Vector) = (0.1,0,0,0)
        [HideInInspector] _GlitchDistortionScale ("ESNative 故障位移缩放", Vector) = (0,3,0,0)
        [HideInInspector] _GlitchDistortionSpeed ("ESNative 故障位移速度", Vector) = (0,1,0,0)

        // Status Effects
        [HideInInspector] _ESNativeStatusContract ("ESNative 精确效果合同", Float) = 0
        [Toggle] _EnableFrozen ("启用冰冻", Float) = 0
        [HideInInspector] _FrozenFade ("ESNative 冰冻强度", Range(0,1)) = 1
        [HideInInspector] _FrozenTint ("ESNative 冰冻色调", Color) = (1.819608,4.611765,5.992157,0)
        [HideInInspector] _FrozenContrast ("ESNative 冰冻对比度", Float) = 2
        [HideInInspector] _FrozenSnowColor ("ESNative 冰冻雪花颜色", Color) = (1.123529,1.373203,1.498039,0)
        [HideInInspector] _FrozenSnowContrast ("ESNative 冰冻雪花对比度", Float) = 1
        [HideInInspector] _FrozenSnowDensity ("ESNative 冰冻雪花密度", Range(0,1)) = 0.25
        [HideInInspector] _FrozenSnowScale ("ESNative 冰冻雪花缩放", Vector) = (0.1,0.1,0,0)
        [HideInInspector] _FrozenHighlightColor ("ESNative 冰冻高光颜色", Color) = (1.797647,4.604501,5.992157,1)
        [HideInInspector] _FrozenHighlightContrast ("ESNative 冰冻高光对比度", Float) = 2
        [HideInInspector] _FrozenHighlightDensity ("ESNative 冰冻高光密度", Range(0,1)) = 1
        [HideInInspector] _FrozenHighlightSpeed ("ESNative 冰冻高光速度", Vector) = (0.1,0.1,0,0)
        [HideInInspector] _FrozenHighlightScale ("ESNative 冰冻高光缩放", Vector) = (0.2,0.2,0,0)
        [HideInInspector] _FrozenHighlightDistortion ("ESNative 冰冻高光扰动", Vector) = (0.5,0.5,0,0)
        [HideInInspector] _FrozenHighlightDistortionSpeed ("ESNative 冰冻高光扰动速度", Vector) = (-0.05,-0.05,0,0)
        [HideInInspector] _FrozenHighlightDistortionScale ("ESNative 冰冻高光扰动缩放", Vector) = (0.2,0.2,0,0)
        [HDR] _FrozenColor ("冰冻颜色", Color) = (0.3,0.8,1,1)
        [HDR] _FrozenHighlight ("冰冻高光", Color) = (1,1,1,1)
        _FrozenDensity ("冰冻雪花密度", Range(0,1)) = 0.35
        _FrozenSpeed ("冰冻流动速度", Float) = 0.2
        [Toggle] _EnableBurn ("启用燃烧", Float) = 0
        [HideInInspector] _BurnFade ("ESNative 燃烧强度", Range(0,1)) = 1
        [HideInInspector] _BurnPosition ("ESNative 燃烧位置", Vector) = (0,5,0,0)
        [HideInInspector] _BurnRadius ("ESNative 燃烧半径", Float) = 5
        [HideInInspector] _BurnEdgeNoiseScale ("ESNative 燃烧边缘噪声缩放", Vector) = (0.3,0.3,0,0)
        [HideInInspector] _BurnEdgeNoiseFactor ("ESNative 燃烧边缘噪声因子", Float) = 0.5
        [HideInInspector] _BurnInsideContrast ("ESNative 燃烧内部对比度", Float) = 2
        [HideInInspector] _BurnInsideNoiseColor ("ESNative 燃烧内部噪声颜色", Color) = (3084.047,257.0039,0,0)
        [HideInInspector] _BurnInsideNoiseFactor ("ESNative 燃烧内部噪声因子", Float) = 0.2
        [HideInInspector] _BurnInsideNoiseScale ("ESNative 燃烧内部噪声缩放", Vector) = (0.5,0.5,0,0)
        [HideInInspector] _BurnSwirlFactor ("ESNative 燃烧旋涡因子", Float) = 1
        [HideInInspector] _BurnSwirlNoiseScale ("ESNative 燃烧旋涡噪声缩放", Vector) = (0.1,0.1,0,0)
        [HDR] _BurnEdgeColor ("燃烧边缘颜色", Color) = (1,0.1,0.01,1)
        [HDR] _BurnInsideColor ("燃烧内部颜色", Color) = (0.2,0.02,0,1)
        _BurnProgress ("燃烧进度", Range(0,1)) = 0
        _BurnWidth ("燃烧边缘宽度", Float) = 0.1
        [Toggle] _EnablePoison ("启用中毒", Float) = 0
        [HideInInspector] _PoisonFade ("ESNative 中毒强度", Range(0,1)) = 1
        [HideInInspector] _PoisonRecolorFactor ("ESNative 中毒重着色因子", Range(0,1)) = 0.5
        [HideInInspector] _PoisonShiftSpeed ("ESNative 中毒条纹速度", Float) = 0.2
        [HideInInspector] _PoisonNoiseBrightness ("ESNative 中毒噪声亮度", Float) = 2
        [HideInInspector] _PoisonNoiseScale ("ESNative 中毒噪声缩放", Vector) = (0.2,0.2,0,0)
        [HideInInspector] _PoisonNoiseSpeed ("ESNative 中毒噪声速度", Vector) = (0,-0.2,0,0)
        [HDR] _PoisonColor ("中毒颜色", Color) = (0.2,1,0.1,1)
        _PoisonDensity ("中毒密度", Float) = 3
        _PoisonSpeed ("中毒速度", Float) = 1

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
        [Enum(Basic,0,Standard,1,High,2)] _QualityTier ("效果质量档位", Float) = 0
        [Enum(DynamicFull,0,MaterialOptimized,1)] _ResourceProfile ("资源编译配置", Float) = 0

        // Render State
        [Enum(Alpha,0,Additive,1,Premultiply,2,Multiply,3)] _BlendMode ("混合模式", Float) = 0
        [HideInInspector] _SrcBlend ("源混合因子", Float) = 5
        [HideInInspector] _DstBlend ("目标混合因子", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull [_CullMode]
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "UIForward"
            // SRPDefaultUnlit is accepted by Universal Renderer and Renderer2D without 2D-light semantics.
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ESUIVertex
            #pragma fragment ESUIFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma shader_feature_local _ _ES_SPRITE_RESOURCE_MASK_0 _ES_SPRITE_RESOURCE_MASK_1 _ES_SPRITE_RESOURCE_MASK_2 _ES_SPRITE_RESOURCE_MASK_3 _ES_SPRITE_RESOURCE_MASK_4 _ES_SPRITE_RESOURCE_MASK_5 _ES_SPRITE_RESOURCE_MASK_6 _ES_SPRITE_RESOURCE_MASK_7 _ES_SPRITE_RESOURCE_MASK_8 _ES_SPRITE_RESOURCE_MASK_9 _ES_SPRITE_RESOURCE_MASK_10 _ES_SPRITE_RESOURCE_MASK_11 _ES_SPRITE_RESOURCE_MASK_12 _ES_SPRITE_RESOURCE_MASK_13 _ES_SPRITE_RESOURCE_MASK_14 _ES_SPRITE_RESOURCE_MASK_15
            #pragma shader_feature_local _ OUTLINE_ON
            #pragma shader_feature_local _ UNDERLAY_ON UNDERLAY_INNER
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ESCompositeRecolor.hlsl"
            #include "ESCompositeColorTransform.hlsl"
            #include "ESCompositeFade.hlsl"
            #include "ESCompositeSampling.hlsl"
            #include "ESCompositeGenerated.hlsl"

            // No mask keyword is the dynamic MPB-safe variant. Optimized materials use
            // one mask keyword, where bits are UV=1, Fade=2, Surface=4, Layers=8.
            #if defined(_ES_SPRITE_RESOURCE_MASK_0) || defined(_ES_SPRITE_RESOURCE_MASK_1) || defined(_ES_SPRITE_RESOURCE_MASK_2) || defined(_ES_SPRITE_RESOURCE_MASK_3) || defined(_ES_SPRITE_RESOURCE_MASK_4) || defined(_ES_SPRITE_RESOURCE_MASK_5) || defined(_ES_SPRITE_RESOURCE_MASK_6) || defined(_ES_SPRITE_RESOURCE_MASK_7) || defined(_ES_SPRITE_RESOURCE_MASK_8) || defined(_ES_SPRITE_RESOURCE_MASK_9) || defined(_ES_SPRITE_RESOURCE_MASK_10) || defined(_ES_SPRITE_RESOURCE_MASK_11) || defined(_ES_SPRITE_RESOURCE_MASK_12) || defined(_ES_SPRITE_RESOURCE_MASK_13) || defined(_ES_SPRITE_RESOURCE_MASK_14) || defined(_ES_SPRITE_RESOURCE_MASK_15)
                #define ES_SPRITE_RESOURCE_PROFILE_OPTIMIZED 1
            #endif
            #if !defined(ES_SPRITE_RESOURCE_PROFILE_OPTIMIZED) || defined(_ES_SPRITE_RESOURCE_MASK_1) || defined(_ES_SPRITE_RESOURCE_MASK_3) || defined(_ES_SPRITE_RESOURCE_MASK_5) || defined(_ES_SPRITE_RESOURCE_MASK_7) || defined(_ES_SPRITE_RESOURCE_MASK_9) || defined(_ES_SPRITE_RESOURCE_MASK_11) || defined(_ES_SPRITE_RESOURCE_MASK_13) || defined(_ES_SPRITE_RESOURCE_MASK_15)
                #define ES_SPRITE_COMPILE_UV_RESOURCES 1
            #endif
            #if !defined(ES_SPRITE_RESOURCE_PROFILE_OPTIMIZED) || defined(_ES_SPRITE_RESOURCE_MASK_2) || defined(_ES_SPRITE_RESOURCE_MASK_3) || defined(_ES_SPRITE_RESOURCE_MASK_6) || defined(_ES_SPRITE_RESOURCE_MASK_7) || defined(_ES_SPRITE_RESOURCE_MASK_10) || defined(_ES_SPRITE_RESOURCE_MASK_11) || defined(_ES_SPRITE_RESOURCE_MASK_14) || defined(_ES_SPRITE_RESOURCE_MASK_15)
                #define ES_SPRITE_COMPILE_FADE_RESOURCES 1
            #endif
            #if !defined(ES_SPRITE_RESOURCE_PROFILE_OPTIMIZED) || defined(_ES_SPRITE_RESOURCE_MASK_4) || defined(_ES_SPRITE_RESOURCE_MASK_5) || defined(_ES_SPRITE_RESOURCE_MASK_6) || defined(_ES_SPRITE_RESOURCE_MASK_7) || defined(_ES_SPRITE_RESOURCE_MASK_12) || defined(_ES_SPRITE_RESOURCE_MASK_13) || defined(_ES_SPRITE_RESOURCE_MASK_14) || defined(_ES_SPRITE_RESOURCE_MASK_15)
                #define ES_SPRITE_COMPILE_SURFACE_RESOURCES 1
            #endif
            #if !defined(ES_SPRITE_RESOURCE_PROFILE_OPTIMIZED) || defined(_ES_SPRITE_RESOURCE_MASK_8) || defined(_ES_SPRITE_RESOURCE_MASK_9) || defined(_ES_SPRITE_RESOURCE_MASK_10) || defined(_ES_SPRITE_RESOURCE_MASK_11) || defined(_ES_SPRITE_RESOURCE_MASK_12) || defined(_ES_SPRITE_RESOURCE_MASK_13) || defined(_ES_SPRITE_RESOURCE_MASK_14) || defined(_ES_SPRITE_RESOURCE_MASK_15)
                #define ES_SPRITE_COMPILE_LAYER_RESOURCES 1
            #endif

            // Texture Resources
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            #if defined(ES_SPRITE_COMPILE_LAYER_RESOURCES)
            TEXTURE2D(_PaletteTex); SAMPLER(sampler_PaletteTex);
            TEXTURE2D(_TextureLayer1Texture); SAMPLER(sampler_TextureLayer1Texture);
            TEXTURE2D(_TextureLayer2Texture); SAMPLER(sampler_TextureLayer2Texture);
            #endif
            #if defined(ES_SPRITE_COMPILE_SURFACE_RESOURCES)
            TEXTURE2D(_AddColorMask);
            TEXTURE2D(_StrongTintMask);
            #define sampler_AddColorMask sampler_MainTex
            #define sampler_StrongTintMask sampler_MainTex
            TEXTURE2D(_RecolorRGBMask); SAMPLER(sampler_RecolorRGBMask);
            TEXTURE2D(_RecolorRGBYCPMask); SAMPLER(sampler_RecolorRGBYCPMask);
            TEXTURE2D(_AddHueMask); SAMPLER(sampler_AddHueMask);
            TEXTURE2D(_SineGlowMask); SAMPLER(sampler_SineGlowMask);
            TEXTURE2D(_MetalMask); SAMPLER(sampler_MetalMask);
            TEXTURE2D(_ShineMask);
            TEXTURE2D(_InnerOutlineTintTexture);
            TEXTURE2D(_OuterOutlineTintTexture);
            TEXTURE2D(_PixelOutlineTintTexture);
            #define sampler_ShineMask sampler_MainTex
            #endif
            #if !defined(ES_SPRITE_COMPILE_SURFACE_RESOURCES)
            #define _InnerOutlineTintTexture _MainTex
            #define _OuterOutlineTintTexture _MainTex
            #define _PixelOutlineTintTexture _MainTex
            #endif
            #if defined(ES_SPRITE_COMPILE_FADE_RESOURCES)
            TEXTURE2D(_FadeNoiseTex); SAMPLER(sampler_FadeNoiseTex);
            TEXTURE2D(_FadeMask); SAMPLER(sampler_FadeMask);
            TEXTURE2D(_CustomFadeFadeMask); SAMPLER(sampler_CustomFadeFadeMask);
            #endif
            #if defined(ES_SPRITE_COMPILE_UV_RESOURCES)
            TEXTURE2D(_UVDistortNoiseTex); SAMPLER(sampler_UVDistortNoiseTex);
            TEXTURE2D(_UVDistortMask); SAMPLER(sampler_UVDistortMask);
            #endif
            #if defined(ES_SPRITE_COMPILE_FADE_RESOURCES) || defined(ES_SPRITE_COMPILE_SURFACE_RESOURCES)
            TEXTURE2D(_UberNoiseTexture); SAMPLER(sampler_UberNoiseTexture);
            #endif

            // Keep optimized variants syntactically complete without retaining optional bindings.
            // Enabling a missing group through MPB is intentionally unsupported in this profile.
            #if !defined(ES_SPRITE_COMPILE_UV_RESOURCES)
                #define _UVDistortNoiseTex _MainTex
                #define sampler_UVDistortNoiseTex sampler_MainTex
                #define _UVDistortMask _MainTex
                #define sampler_UVDistortMask sampler_MainTex
            #endif
            #if !defined(ES_SPRITE_COMPILE_FADE_RESOURCES)
                #define _FadeMask _MainTex
                #define sampler_FadeMask sampler_MainTex
                #define _FadeNoiseTex _MainTex
                #define sampler_FadeNoiseTex sampler_MainTex
                #define _CustomFadeFadeMask _MainTex
                #define sampler_CustomFadeFadeMask sampler_MainTex
            #endif
            #if !defined(ES_SPRITE_COMPILE_SURFACE_RESOURCES)
                #define _AddColorMask _MainTex
                #define sampler_AddColorMask sampler_MainTex
                #define _StrongTintMask _MainTex
                #define sampler_StrongTintMask sampler_MainTex
                #define _RecolorRGBMask _MainTex
                #define sampler_RecolorRGBMask sampler_MainTex
                #define _RecolorRGBYCPMask _MainTex
                #define sampler_RecolorRGBYCPMask sampler_MainTex
                #define _AddHueMask _MainTex
                #define sampler_AddHueMask sampler_MainTex
                #define _SineGlowMask _MainTex
                #define sampler_SineGlowMask sampler_MainTex
                #define _MetalMask _MainTex
                #define sampler_MetalMask sampler_MainTex
                #define _ShineMask _MainTex
                #define sampler_ShineMask sampler_MainTex
            #endif
            #if !defined(ES_SPRITE_COMPILE_LAYER_RESOURCES)
                #define _PaletteTex _MainTex
                #define sampler_PaletteTex sampler_MainTex
                #define _TextureLayer1Texture _MainTex
                #define sampler_TextureLayer1Texture sampler_MainTex
                #define _TextureLayer2Texture _MainTex
                #define sampler_TextureLayer2Texture sampler_MainTex
            #endif
            #if !defined(ES_SPRITE_COMPILE_FADE_RESOURCES) && !defined(ES_SPRITE_COMPILE_SURFACE_RESOURCES)
                #define _UberNoiseTexture _MainTex
                #define sampler_UberNoiseTexture sampler_MainTex
            #endif

            // Per-Material State
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _SpriteUVRect;
                float4 _SpriteUVTransformX;
                float4 _SpriteUVTransformY;
                half4 _Color;
                half4 _SDFOutlineColor;
                half4 _SDFGlowColor;
                float4 _MainTexScaleOffset;
                float _EnableSDF;
                float _SDFThreshold;
                float _SDFSoftness;
                float _SDFOutlineWidth;
                float _SDFOutlineSoftness;
                float _SDFGlowWidth;
                float _EnableTMPCompatibility;
                half4 _FaceColor;
                float _FaceDilate;
                half4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineSoftness;
                float _EnableUnderlay;
                half4 _UnderlayColor;
                float _UnderlayOffsetX;
                float _UnderlayOffsetY;
                float _UnderlayDilate;
                float _UnderlaySoftness;
                float _WeightNormal;
                float _WeightBold;
                float _ScaleRatioA;
                float _ScaleRatioB;
                float _ScaleRatioC;
                float _GradientScale;
                float _Sharpness;
                float _TextureWidth;
                float _TextureHeight;
                float _ScaleX;
                float _ScaleY;
                float _PerspectiveFilter;
                float _VertexOffsetX;
                float _VertexOffsetY;
                float _MaskSoftnessX;
                float _MaskSoftnessY;
                float _UIMaskSoftnessX;
                float _UIMaskSoftnessY;
                float _EnableRecolorRGB;
                half4 _RecolorRed;
                half4 _RecolorGreen;
                half4 _RecolorBlue;
                float _RecolorRGBStrength;
                float _RecolorRGBMaskToggle;
                float _RecolorRGBMaskChannel;
                float _EnableRecolorRGBYCP;
                half4 _RecolorRGBYCPRed;
                half4 _RecolorRGBYCPGreen;
                half4 _RecolorRGBYCPBlue;
                half4 _RecolorRGBYCPYellow;
                half4 _RecolorRGBYCPCyan;
                half4 _RecolorRGBYCPPurple;
                float _RecolorRGBYCPStrength;
                float _RecolorRGBYCPMaskToggle;
                float _RecolorRGBYCPMaskChannel;
                float _EnableAddColor;
                half4 _AddColor;
                float _AddColorFade;
                float _AddColorContrastToggle;
                float _AddColorContrast;
                float _AddColorMaskToggle;
                float4 _AddColorMask_ST;
                float _EnableStrongTint;
                half4 _StrongTint;
                float _StrongTintFade;
                float _StrongTintContrastToggle;
                float _StrongTintContrast;
                float _StrongTintMaskToggle;
                float4 _StrongTintMask_ST;
                float _EnableAlphaTint;
                half4 _AlphaTint;
                float _AlphaTintMin;
                float _AlphaTintFade;
                float _EnableColorReplace;
                half4 _ReplaceFrom;
                half4 _ReplaceTo;
                float _ReplaceRange;
                float _ReplaceSoftness;
                float _ReplaceContrast;
                float _ReplaceFade;
                float _EnableBrightness;
                float _Brightness;
                float _EnableContrast;
                float _Contrast;
                float _EnableSaturation;
                float _Saturation;
                float _EnableHue;
                float _Hue;
                float _EnableSplitToning;
                half4 _SplitToneShadows;
                half4 _SplitToneHighlights;
                float _SplitToneBalance;
                float _SplitToneStrength;
                float _SplitToneContrast;
                float _SplitToneShift;
                float _EnableBlackTint;
                float _BlackTintFade;
                half4 _BlackTintColor;
                float _BlackTintPower;
                float _EnableInkSpread;
                float _InkSpreadFade;
                half4 _InkSpreadColor;
                float _InkSpreadContrast;
                float _InkSpreadDistance;
                float4 _InkSpreadPosition;
                float _InkSpreadWidth;
                float4 _InkSpreadNoiseScale;
                float _InkSpreadNoiseFactor;
                float _EnableShiftHue;
                float _ShiftHueSpeed;
                float _EnableAddHue;
                float _AddHueFade;
                float _AddHueSpeed;
                float _AddHueBrightness;
                float _AddHueSaturation;
                float _AddHueContrast;
                float _AddHueMaskToggle;
                float4 _AddHueMask_ST;
                float _EnableSineGlow;
                float _SineGlowFade;
                half4 _SineGlowColor;
                float _SineGlowContrast;
                float _SineGlowFrequency;
                float _SineGlowMin;
                float _SineGlowMax;
                float _SineGlowMaskToggle;
                float4 _SineGlowMask_ST;
                float _EnableCamouflage;
                float _CamouflageFade;
                half4 _CamouflageBaseColor;
                float _CamouflageContrast;
                half4 _CamouflageColorA;
                float _CamouflageDensityA;
                float _CamouflageSmoothnessA;
                float4 _CamouflageNoiseScaleA;
                half4 _CamouflageColorB;
                float _CamouflageDensityB;
                float _CamouflageSmoothnessB;
                float4 _CamouflageNoiseScaleB;
                float _CamouflageAnimationToggle;
                float4 _CamouflageDistortionSpeed;
                float4 _CamouflageDistortionIntensity;
                float4 _CamouflageDistortionScale;
                float _EnableMetal;
                float _MetalFade;
                half4 _MetalColor;
                float _MetalContrast;
                half4 _MetalHighlightColor;
                float _MetalHighlightDensity;
                float _MetalHighlightContrast;
                float4 _MetalNoiseScale;
                float4 _MetalNoiseSpeed;
                float4 _MetalNoiseDistortionScale;
                float4 _MetalNoiseDistortionSpeed;
                float4 _MetalNoiseDistortion;
                float _MetalMaskToggle;
                float _EnableEnchanted;
                float _EnchantedFade;
                float4 _EnchantedSpeed;
                float4 _EnchantedScale;
                float _EnchantedBrightness;
                float _EnchantedContrast;
                float _EnchantedReduce;
                float _EnchantedRainbowToggle;
                float _EnchantedRainbowSpeed;
                float _EnchantedRainbowDensity;
                float _EnchantedRainbowSaturation;
                half4 _EnchantedLowColor;
                half4 _EnchantedHighColor;
                float _EnchantedLerpToggle;
                float _EnableShifting;
                float _ShiftingFade;
                float _ShiftingSpeed;
                float _ShiftingDensity;
                float _ShiftingBrightness;
                float _ShiftingContrast;
                float _ShiftingRainbowToggle;
                float _ShiftingSaturation;
                half4 _ShiftingColorA;
                half4 _ShiftingColorB;
                float _EnableNegative;
                float _NegativeFade;
                float _EnableRainbow;
                float _RainbowSpeed;
                float _RainbowDensity;
                float4 _RainbowDirection;
                float _RainbowBrightness;
                float _EnableShadow;
                float _ShadowFade;
                float4 _ShadowOffset;
                half4 _ShadowColor;
                float _EnablePixelate;
                float _PixelateCells;
                float _PixelateStrength;
                float _TilingMode;
                float4 _WorldTilingScale;
                float4 _WorldTilingOffset;
                float _WorldTilingPixelsPerUnit;
                float4 _ScreenTilingScale;
                float4 _ScreenTilingOffset;
                float _ScreenTilingPixelsPerUnit;
                float _EnableSmoothPixelArt;
                float _SmoothPixelStrength;
                float _EnableCheckerboard;
                float _CheckerboardDarken;
                float _CheckerboardTiling;
                float _EnableFlame;
                float _FlameBrightness;
                float _FlameSmooth;
                float _FlameRadius;
                float4 _FlameCenter;
                float4 _FlameDirection;
                float4 _FlameSpeed;
                float _FlameNoiseFactor;
                float _FlameNoiseHeightFactor;
                float4 _FlameNoiseScale;
                float _EnableSmoke;
                float _SmokeAlpha;
                float _SmokeSmoothness;
                float _SmokeNoiseScale;
                float4 _SmokeSpeed;
                float _SmokeNoiseFactor;
                float _SmokeDarkEdge;
                float _SmokeVertexSeed;
                float _EnablePalette;
                float _PaletteRow;
                float _PaletteStrength;
                float _EnableHalftone;
                float _HalftoneScale;
                float _HalftoneAngle;
                float _HalftoneStrength;
                float4 _HalftonePosition;
                float _HalftoneFade;
                float _HalftoneFadeWidth;
                float _HalftoneInvert;
                float _HalftoneAlphaPattern;
                float _EnableTextureLayer1;
                float _TextureLayer1Fade;
                half4 _TextureLayer1Color;
                float4 _TextureLayer1Scale;
                float4 _TextureLayer1Offset;
                float _TextureLayer1ScrollToggle;
                float4 _TextureLayer1ScrollSpeed;
                float _TextureLayer1SheetToggle;
                float _TextureLayer1Columns;
                float _TextureLayer1Rows;
                float _TextureLayer1Speed;
                float _TextureLayer1StartFrame;
                float _TextureLayer1EdgeClip;
                float _TextureLayer1ContrastToggle;
                float _TextureLayer1Contrast;
                float _EnableTextureLayer2;
                float _TextureLayer2Fade;
                half4 _TextureLayer2Color;
                float4 _TextureLayer2Scale;
                float4 _TextureLayer2Offset;
                float _TextureLayer2ScrollToggle;
                float4 _TextureLayer2ScrollSpeed;
                float _TextureLayer2SheetToggle;
                float _TextureLayer2Columns;
                float _TextureLayer2Rows;
                float _TextureLayer2Speed;
                float _TextureLayer2StartFrame;
                float _TextureLayer2EdgeClip;
                float _TextureLayer2ContrastToggle;
                float _TextureLayer2Contrast;
                float _EnableInnerOutline;
                float _InnerOutlineFade;
                half4 _InnerOutlineColor;
                float _InnerOutlineWidth;
                float _InnerOutlineDistortionToggle;
                float4 _InnerOutlineDistortionIntensity;
                float4 _InnerOutlineNoiseScale;
                float4 _InnerOutlineNoiseSpeed;
                float _InnerOutlineTextureToggle;
                float4 _InnerOutlineTextureSpeed;
                float _InnerOutlineOutlineOnlyToggle;
                float _EnableOuterOutline;
                float _OuterOutlineFade;
                half4 _OuterOutlineColor;
                float _OuterOutlineWidth;
                float _OuterOutlineDistortionToggle;
                float4 _OuterOutlineDistortionIntensity;
                float4 _OuterOutlineNoiseScale;
                float4 _OuterOutlineNoiseSpeed;
                float _OuterOutlineTextureToggle;
                float4 _OuterOutlineTextureSpeed;
                float _OuterOutlineOutlineOnlyToggle;
                float _EnablePixelOutline;
                float _PixelOutlineFade;
                half4 _PixelOutlineColor;
                float _PixelOutlineWidth;
                float _PixelOutlineTextureToggle;
                float4 _PixelOutlineTextureSpeed;
                float _PixelOutlineOutlineOnlyToggle;
                float _TimeMode;
                float _CustomTime;
                float _TimeScale;
                float _EnableTimeFPS;
                float _TimeFPS;
                float _EnableTimeFrequency;
                float _TimeFrequency;
                float _TimeRange;
                float _EnableUVTransform;
                float4 _UVPivot;
                float4 _UVScale;
                float4 _UVOffset;
                float _UVRotation;
                float _UVRotationSpeed;
                float _EnableUVDistort;
                float4 _UVDistortFrequency;
                float4 _UVDistortSpeed;
                float _UVDistortAmount;
                float4 _UVDistortFrom;
                float4 _UVDistortTo;
                float _UVDistortFade;
                float _UVDistortMaskToggle;
                float _UVDistortMaskChannel;
                float _EnableSqueeze;
                float _SqueezeFade;
                float4 _SqueezeScale;
                float _SqueezePower;
                float4 _SqueezeCenter;
                float _EnableSineRotate;
                float _SineRotateFade;
                float _SineRotateAngle;
                float _SineRotateFrequency;
                float4 _SineRotatePivot;
                float _FadeMode;
                float _FadeProgress;
                float4 _FadePosition;
                float _FadeRotation;
                float _FadeWidth;
                float _FadeInvert;
                float _FadeNoiseFactor;
                float4 _FadeNoiseScale;
                float4 _FadeNoiseSpeed;
                half4 _DissolveEdgeColor;
                float _DissolveEdgeWidth;
                float _DissolveEdgeIntensity;
                float _FadeDistortionStrength;
                float _EnableFullAlphaDissolve;
                float _FullAlphaDissolveFade;
                float _FullAlphaDissolveWidth;
                float4 _FullAlphaDissolveNoiseScale;
                float _EnableSourceAlphaDissolve;
                float _SourceAlphaDissolveFade;
                float4 _SourceAlphaDissolvePosition;
                float _SourceAlphaDissolveWidth;
                float4 _SourceAlphaDissolveNoiseScale;
                float _SourceAlphaDissolveNoiseFactor;
                float _SourceAlphaDissolveInvert;
                float _EnableSourceGlowDissolve;
                float _SourceGlowDissolveFade;
                float4 _SourceGlowDissolvePosition;
                float _SourceGlowDissolveWidth;
                half4 _SourceGlowDissolveEdgeColor;
                float4 _SourceGlowDissolveNoiseScale;
                float _SourceGlowDissolveNoiseFactor;
                float _SourceGlowDissolveInvert;
                float _EnableDirectionalAlphaFade;
                float _DirectionalAlphaFadeFade;
                float _DirectionalAlphaFadeRotation;
                float _DirectionalAlphaFadeWidth;
                float4 _DirectionalAlphaFadeNoiseScale;
                float _DirectionalAlphaFadeNoiseFactor;
                float _DirectionalAlphaFadeInvert;
                float _EnableDirectionalGlowFade;
                float _DirectionalGlowFadeFade;
                float _DirectionalGlowFadeRotation;
                half4 _DirectionalGlowFadeEdgeColor;
                float _DirectionalGlowFadeWidth;
                float4 _DirectionalGlowFadeNoiseScale;
                float _DirectionalGlowFadeNoiseFactor;
                float _DirectionalGlowFadeInvert;
                float _EnableDirectionalDistortion;
                float _DirectionalDistortionFade;
                float _DirectionalDistortionRotation;
                float _DirectionalDistortionWidth;
                float4 _DirectionalDistortionNoiseScale;
                float _DirectionalDistortionNoiseFactor;
                float4 _DirectionalDistortionDistortion;
                float _DirectionalDistortionRandomDirection;
                float4 _DirectionalDistortionDistortionScale;
                float _DirectionalDistortionInvert;
                float _EnableCustomFade;
                float _CustomFadeSmoothness;
                float4 _CustomFadeNoiseScale;
                float _CustomFadeNoiseFactor;
                float _CustomFadeAlpha;
                float _EnableFullGlowDissolve;
                float _FullGlowDissolveFade;
                float _FullGlowDissolveWidth;
                half4 _FullGlowDissolveEdgeColor;
                float4 _FullGlowDissolveNoiseScale;
                float _EnableWind;
                float4 _WindDirection;
                float _WindAmplitude;
                float _WindFrequency;
                float _WindSpeed;
                float _WindAnchor;
                float4 _WindAnchorDirection;
                float _WindGlobalInfluence;
                float _EnableSquish;
                float _SquishAmount;
                float4 _SquishDirection;
                float _SquishSpeed;
                float _SquishFade;
                float _ESInteractiveWindRotation;
                float _ESInteractiveWindHeight;
                float _ESInteractiveSquish;
                float _ESWindPhaseOffset;
                float _EnableWiggle;
                float _WiggleAmplitude;
                float _WiggleFrequency;
                float4 _WiggleDirection;
                float _WiggleSpeed;
                float _EnableVibrate;
                float _VibrateAmplitude;
                float4 _VibrateDirection;
                float _VibrateSpeed;
                float _EnableSineMove;
                float _SineMoveFade;
                float4 _SineMoveOffset;
                float4 _SineMoveFrequency;
                float _EnableSineScale;
                float _SineScaleFrequency;
                float4 _SineScaleFactor;
                float _SpriteUVTransformValid;
                float _VertexColorStrength;
                float _EnableFlow;
                float4 _FlowSpeed;
                float _FlowStrength;
                float _EnableShine;
                half4 _ShineColor;
                float _ShineSpeed;
                float _ShineWidth;
                float _ShineSpace;
                float4 _ShineDirection;
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
                float _BlurMode;
                float _EnableSharpen;
                float _SharpenAmount;
                float _SharpenRadius;
                float _SharpenThreshold;
                float _SharpenFade;
                float _EnablePingPongGlow;
                half4 _GlowFrom;
                half4 _GlowTo;
                float _GlowFrequency;
                float _GlowIntensity;
                float _GlowContrast;
                float _GlowFade;
                float _EnableFullDistortion;
                float _FullDistortionFade;
                float4 _FullDistortionDistortion;
                float4 _FullDistortionNoiseScale;
                float _EnableHologram;
                half4 _HologramColor;
                float _HologramFrequency;
                float _HologramSpeed;
                float _HologramFade;
                float _HologramContrast;
                float _HologramSpace;
                float4 _HologramDirection;
                float _HologramLineFrequency;
                float _HologramLineGap;
                float _HologramMinAlpha;
                float _HologramDistortionOffset;
                float4 _HologramDistortionDirection;
                float _HologramDistortionSpeed;
                float _HologramDistortionDensity;
                float _HologramDistortionScale;
                float _EnableGlitch;
                float _GlitchAmount;
                float _GlitchSpeed;
                float4 _GlitchScanDirection;
                float _GlitchFade;
                float _GlitchMaskMin;
                float4 _GlitchMaskScale;
                float4 _GlitchMaskSpeed;
                float _GlitchHueSpeed;
                float _GlitchBrightness;
                float4 _GlitchNoiseScale;
                float4 _GlitchNoiseSpeed;
                float4 _GlitchDistortion;
                float4 _GlitchDistortionScale;
                float4 _GlitchDistortionSpeed;
                float _EnableFrozen;
                float _ESNativeStatusContract;
                float _FrozenFade;
                half4 _FrozenTint;
                float _FrozenContrast;
                half4 _FrozenSnowColor;
                float _FrozenSnowContrast;
                float _FrozenSnowDensity;
                float4 _FrozenSnowScale;
                half4 _FrozenHighlightColor;
                float _FrozenHighlightContrast;
                float _FrozenHighlightDensity;
                float4 _FrozenHighlightSpeed;
                float4 _FrozenHighlightScale;
                float4 _FrozenHighlightDistortion;
                float4 _FrozenHighlightDistortionSpeed;
                float4 _FrozenHighlightDistortionScale;
                half4 _FrozenColor;
                half4 _FrozenHighlight;
                float _FrozenDensity;
                float _FrozenSpeed;
                float _EnableBurn;
                float _BurnFade;
                float4 _BurnPosition;
                float _BurnRadius;
                float4 _BurnEdgeNoiseScale;
                float _BurnEdgeNoiseFactor;
                float _BurnInsideContrast;
                half4 _BurnInsideNoiseColor;
                float _BurnInsideNoiseFactor;
                float4 _BurnInsideNoiseScale;
                float _BurnSwirlFactor;
                float4 _BurnSwirlNoiseScale;
                half4 _BurnEdgeColor;
                half4 _BurnInsideColor;
                float _BurnProgress;
                float _BurnWidth;
                float _EnablePoison;
                float _PoisonFade;
                float _PoisonRecolorFactor;
                float _PoisonShiftSpeed;
                float _PoisonNoiseBrightness;
                float4 _PoisonNoiseScale;
                float4 _PoisonNoiseSpeed;
                half4 _PoisonColor;
                float _PoisonDensity;
                float _PoisonSpeed;
                float _RainbowFade;
                float _RainbowSaturation;
                float _RainbowContrast;
                float4 _RainbowCenter;
                float4 _RainbowNoiseScale;
                float _RainbowNoiseFactor;
                float _ShineFade;
                float _ShineSaturation;
                float _ShineContrast;
                float _ShineRotation;
                float _ShineSmooth;
                float _ShineFrequency;
                float _ShineMaskToggle;
                float4 _ShineMask_ST;
                float _BlendMode;
                float _AlphaClip;
                float _Cutoff;
                half4 _TextureSampleAdd;
                float4 _ClipRect;
            CBUFFER_END

            float _ESUnscaledTime;
            float _ESUnscaledTimeValid;
            float4 _ESCompositeGlobalWind;
            float _ESCompositeGlobalWindValid;

            // Time And Sampling Helpers
            float2 ESSpriteUVSize()
            {
                if (_SpriteUVTransformValid > 0.5)
                {
                    return max(float2(
                        length(float2(_SpriteUVTransformX.x, _SpriteUVTransformY.x)),
                        length(float2(_SpriteUVTransformX.y, _SpriteUVTransformY.y))),
                        float2(1e-5, 1e-5));
                }
                return max(_SpriteUVRect.zw - _SpriteUVRect.xy, float2(1e-5, 1e-5));
            }

            float2 ESAtlasToLocalUV(float2 uv)
            {
                if (_SpriteUVTransformValid > 0.5)
                {
                    float2 delta = uv - float2(_SpriteUVTransformX.z, _SpriteUVTransformY.z);
                    float determinant = _SpriteUVTransformX.x * _SpriteUVTransformY.y
                        - _SpriteUVTransformX.y * _SpriteUVTransformY.x;
                    float inverseDeterminant = rcp(max(abs(determinant), 1e-6)) * (determinant < 0 ? -1 : 1);
                    return float2(
                        (delta.x * _SpriteUVTransformY.y - delta.y * _SpriteUVTransformX.y) * inverseDeterminant,
                        (delta.y * _SpriteUVTransformX.x - delta.x * _SpriteUVTransformY.x) * inverseDeterminant);
                }
                return (uv - _SpriteUVRect.xy) / ESSpriteUVSize();
            }

            float2 ESSpritePixelSize()
            {
                if (_SpriteUVTransformValid > 0.5)
                {
                    return max(float2(
                        length(float2(
                            _SpriteUVTransformX.x * _MainTex_TexelSize.z,
                            _SpriteUVTransformY.x * _MainTex_TexelSize.w)),
                        length(float2(
                            _SpriteUVTransformX.y * _MainTex_TexelSize.z,
                            _SpriteUVTransformY.y * _MainTex_TexelSize.w))),
                        float2(1, 1));
                }
                return max(_MainTex_TexelSize.zw * ESSpriteUVSize(), float2(1, 1));
            }

            float2 ESLocalToAtlasUV(float2 uv)
            {
                if (_SpriteUVTransformValid > 0.5)
                {
                    float2 local = saturate(uv);
                    return float2(
                        dot(_SpriteUVTransformX.xy, local) + _SpriteUVTransformX.z,
                        dot(_SpriteUVTransformY.xy, local) + _SpriteUVTransformY.z);
                }
                return _SpriteUVRect.xy + saturate(uv) * ESSpriteUVSize();
            }

            half4 ESSampleUITexture(float2 uv)
            {
                if (_TilingMode > 0.5) uv = frac(uv);
                float2 inside = step(float2(0, 0), uv) * step(uv, float2(1, 1));
                half insideMask = _TilingMode > 0.5 ? 1.0h : (half)(inside.x * inside.y);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ESLocalToAtlasUV(uv)) * insideMask;
            }

            float ESGetTime()
            {
                float baseTime = _TimeMode > 1.5
                    ? _CustomTime
                    : (_TimeMode > 0.5 ? (_ESUnscaledTimeValid > 0.5 ? _ESUnscaledTime : _Time.y) : _Time.y);
                float timeValue = baseTime * _TimeScale;
                if (_EnableTimeFPS > 0.5)
                {
                    float fps = max(abs(_TimeFPS), 0.01);
                    timeValue = floor(timeValue * fps) / fps;
                }
                if (_EnableTimeFrequency > 0.5)
                    timeValue = sin(timeValue * _TimeFrequency) * _TimeRange + 100.0;
                return timeValue;
            }

            #include "ESCompositeSpriteVertexMotion.hlsl"
            #include "ESCompositeESNativeEffects.hlsl"
            #include "ESCompositeESNativeStatusEffects.hlsl"
            #include "ESCompositeESNativeStylizedEffects.hlsl"
            #include "ESCompositeESNativeFadeStack.hlsl"

            half ESMinOutlineAlpha8(float2 uv, float2 width)
            {
                const float diagonal = 0.705;
                half value = 1.0h;
                value = min(value, ESSampleUITexture(uv + float2(0, -width.y)).a);
                value = min(value, ESSampleUITexture(uv + float2(0, width.y)).a);
                value = min(value, ESSampleUITexture(uv + float2(-width.x, 0)).a);
                value = min(value, ESSampleUITexture(uv + float2(width.x, 0)).a);
                value = min(value, ESSampleUITexture(uv + width * diagonal).a);
                value = min(value, ESSampleUITexture(uv + float2(-width.x, width.y) * diagonal).a);
                value = min(value, ESSampleUITexture(uv + float2(width.x, -width.y) * diagonal).a);
                value = min(value, ESSampleUITexture(uv - width * diagonal).a);
                return value;
            }

            half ESMaxOutlineAlpha8(float2 uv, float2 width)
            {
                const float diagonal = 0.705;
                half value = 0.0h;
                value = max(value, ESSampleUITexture(uv + float2(0, -width.y)).a);
                value = max(value, ESSampleUITexture(uv + float2(0, width.y)).a);
                value = max(value, ESSampleUITexture(uv + float2(-width.x, 0)).a);
                value = max(value, ESSampleUITexture(uv + float2(width.x, 0)).a);
                value = max(value, ESSampleUITexture(uv + width * diagonal).a);
                value = max(value, ESSampleUITexture(uv + float2(-width.x, width.y) * diagonal).a);
                value = max(value, ESSampleUITexture(uv + float2(width.x, -width.y) * diagonal).a);
                value = max(value, ESSampleUITexture(uv - width * diagonal).a);
                return value;
            }

            half ESMaxOutlineAlpha4(float2 uv, float2 width)
            {
                half value = 0.0h;
                value = max(value, ESSampleUITexture(uv + float2(0, -width.y)).a);
                value = max(value, ESSampleUITexture(uv + float2(0, width.y)).a);
                value = max(value, ESSampleUITexture(uv + float2(-width.x, 0)).a);
                value = max(value, ESSampleUITexture(uv + float2(width.x, 0)).a);
                return value;
            }

            half3 ESOutlineTextureTint(
                TEXTURE2D_PARAM(textureName, samplerName),
                float2 uv,
                float2 speed,
                float timeValue,
                half3 tint,
                float enabled)
            {
                if (enabled > 0.5)
                    tint *= SAMPLE_TEXTURE2D(
                        textureName,
                        samplerName,
                        uv + speed * timeValue).rgb;
                return tint;
            }

            void ESApplyESNativeExactOutlines(
                float2 uv,
                float timeValue,
                inout half4 color)
            {
                float2 spritePixels = ESSpritePixelSize();
                if (_EnableInnerOutline > 0.5)
                {
                    float2 distortedUV = ESCompositeESNativeOutlineDistortedUV(
                        uv,
                        _InnerOutlineDistortionToggle,
                        _InnerOutlineDistortionIntensity.xy,
                        _InnerOutlineNoiseScale.xy,
                        _InnerOutlineNoiseSpeed.xy,
                        timeValue);
                    half minimumAlpha = ESMinOutlineAlpha8(
                        distortedUV,
                        max(_InnerOutlineWidth, 0.0) * 100.0 / spritePixels);
                    half3 tint = ESOutlineTextureTint(
                        TEXTURE2D_ARGS(_InnerOutlineTintTexture, sampler_MainTex),
                        uv,
                        _InnerOutlineTextureSpeed.xy,
                        timeValue,
                        _InnerOutlineColor.rgb,
                        _InnerOutlineTextureToggle);
                    color = ESCompositeApplyESNativeInnerOutline(
                        color,
                        minimumAlpha,
                        tint,
                        _InnerOutlineFade,
                        _InnerOutlineOutlineOnlyToggle);
                }
                if (_EnableOuterOutline > 0.5)
                {
                    float2 distortedUV = ESCompositeESNativeOutlineDistortedUV(
                        uv,
                        _OuterOutlineDistortionToggle,
                        _OuterOutlineDistortionIntensity.xy,
                        _OuterOutlineNoiseScale.xy,
                        _OuterOutlineNoiseSpeed.xy,
                        timeValue);
                    half maximumAlpha = ESMaxOutlineAlpha8(
                        distortedUV,
                        max(_OuterOutlineWidth, 0.0) * 100.0 / spritePixels);
                    half3 tint = ESOutlineTextureTint(
                        TEXTURE2D_ARGS(_OuterOutlineTintTexture, sampler_MainTex),
                        uv,
                        _OuterOutlineTextureSpeed.xy,
                        timeValue,
                        _OuterOutlineColor.rgb,
                        _OuterOutlineTextureToggle);
                    color = ESCompositeApplyESNativeOuterOutline(
                        color,
                        maximumAlpha,
                        tint,
                        _OuterOutlineFade,
                        _OuterOutlineOutlineOnlyToggle);
                }
                if (_EnablePixelOutline > 0.5)
                {
                    half maximumAlpha = ESMaxOutlineAlpha4(
                        uv,
                        max(_PixelOutlineWidth, 0.0) / spritePixels);
                    half3 tint = ESOutlineTextureTint(
                        TEXTURE2D_ARGS(_PixelOutlineTintTexture, sampler_MainTex),
                        uv,
                        _PixelOutlineTextureSpeed.xy,
                        timeValue,
                        _PixelOutlineColor.rgb,
                        _PixelOutlineTextureToggle);
                    color = ESCompositeApplyESNativeOuterOutline(
                        color,
                        maximumAlpha,
                        tint,
                        _PixelOutlineFade,
                        _PixelOutlineOutlineOnlyToggle);
                }
            }

            float2 ESLayerUV(float2 baseUV, float4 scale, float4 offset, float4 scrollSpeed,
                float scrollToggle, float sheetToggle, float columns, float rows, float speed,
                float startFrame, float edgeClip, float timeValue)
            {
                float2 uv = baseUV * scale.xy - offset.xy;
                if (scrollToggle > 0.5) uv -= scrollSpeed.xy * timeValue;
                if (sheetToggle > 0.5)
                {
                    float2 sheet = max(float2(1, 1), round(float2(columns, rows)));
                    float frame = max(0, floor(startFrame + timeValue * speed));
                    float frameCount = max(1, sheet.x * sheet.y);
                    frame = fmod(frame, frameCount);
                    float2 tile = float2(fmod(frame, sheet.x), floor(frame / sheet.x));
                    float2 local = frac(uv);
                    float edge = saturate(edgeClip);
                    local = lerp(edge.xx, (1 - edge).xx, local);
                    uv = (local + tile) / sheet;
                }
                return frac(uv);
            }

            half3 ESApplyLayerContrast(half3 layerColor, half3 baseColor, float enabled, float contrast)
            {
                if (enabled > 0.5)
                {
                    float luminance = dot(baseColor, half3(0.2126h, 0.7152h, 0.0722h));
                    layerColor *= pow(max(luminance, 0.001), max(contrast, 0.01));
                }
                return layerColor;
            }

            half3 ESApplyTextureLayers(half3 baseColor, float2 uv, float timeValue)
            {
                half3 color = baseColor;
                if (_EnableTextureLayer1 > 0.5)
                {
                    float2 layerUV = ESLayerUV(uv, _TextureLayer1Scale, _TextureLayer1Offset,
                        _TextureLayer1ScrollSpeed, _TextureLayer1ScrollToggle, _TextureLayer1SheetToggle,
                        _TextureLayer1Columns, _TextureLayer1Rows, _TextureLayer1Speed,
                        _TextureLayer1StartFrame, _TextureLayer1EdgeClip, timeValue);
                    half4 layerSample = SAMPLE_TEXTURE2D(_TextureLayer1Texture, sampler_TextureLayer1Texture, layerUV);
                    half3 layerColor = ESApplyLayerContrast(layerSample.rgb * _TextureLayer1Color.rgb, color,
                        _TextureLayer1ContrastToggle, _TextureLayer1Contrast);
                    color = lerp(color, layerColor, saturate(layerSample.a * _TextureLayer1Fade));
                }
                if (_EnableTextureLayer2 > 0.5)
                {
                    float2 layerUV = ESLayerUV(uv, _TextureLayer2Scale, _TextureLayer2Offset,
                        _TextureLayer2ScrollSpeed, _TextureLayer2ScrollToggle, _TextureLayer2SheetToggle,
                        _TextureLayer2Columns, _TextureLayer2Rows, _TextureLayer2Speed,
                        _TextureLayer2StartFrame, _TextureLayer2EdgeClip, timeValue);
                    half4 layerSample = SAMPLE_TEXTURE2D(_TextureLayer2Texture, sampler_TextureLayer2Texture, layerUV);
                    half3 layerColor = ESApplyLayerContrast(layerSample.rgb * _TextureLayer2Color.rgb, color,
                        _TextureLayer2ContrastToggle, _TextureLayer2Contrast);
                    color = lerp(color, layerColor, saturate(layerSample.a * _TextureLayer2Fade));
                }
                return color;
            }

            half4 ESBlurSample(float2 uv, half4 center)
            {
                float2 delta = rcp(ESSpritePixelSize()) * (_BlurRadius * 512.0);
                half4 result = center * 0.4h;
                result += ESSampleUITexture(uv + float2(delta.x, 0)) * 0.15h;
                result += ESSampleUITexture(uv - float2(delta.x, 0)) * 0.15h;
                result += ESSampleUITexture(uv + float2(0, delta.y)) * 0.15h;
                result += ESSampleUITexture(uv - float2(0, delta.y)) * 0.15h;
                return result;
            }

            half4 ESGaussianBlurSample(float2 uv, half4 center)
            {
                float2 delta = rcp(ESSpritePixelSize()) * (_BlurRadius * 512.0);
                half4 axis = ESSampleUITexture(uv + float2(delta.x, 0));
                axis += ESSampleUITexture(uv - float2(delta.x, 0));
                axis += ESSampleUITexture(uv + float2(0, delta.y));
                axis += ESSampleUITexture(uv - float2(0, delta.y));
                half4 diagonal = ESSampleUITexture(uv + delta);
                diagonal += ESSampleUITexture(uv + float2(delta.x, -delta.y));
                diagonal += ESSampleUITexture(uv + float2(-delta.x, delta.y));
                diagonal += ESSampleUITexture(uv - delta);
                return ESCompositeGaussian3x3(center, axis, diagonal);
            }

            half4 ESSharpenSample(float2 uv, half4 center)
            {
                float2 delta = rcp(ESSpritePixelSize()) * (_SharpenRadius * 512.0);
                half4 axis = ESSampleUITexture(uv + float2(delta.x, 0));
                axis += ESSampleUITexture(uv - float2(delta.x, 0));
                axis += ESSampleUITexture(uv + float2(0, delta.y));
                axis += ESSampleUITexture(uv - float2(0, delta.y));
                return ESCompositeSharpen5(center, axis * 0.25h, _SharpenAmount, _SharpenThreshold);
            }

            float2 ESPixelateUV(float2 uv)
            {
                float2 spritePixels = ESSpritePixelSize();
                float cellsX = max(2, _PixelateCells);
                float2 cells = float2(cellsX, max(2, cellsX * spritePixels.y / spritePixels.x));
                float2 snapped = (floor(uv * cells) + 0.5) / cells;
                return lerp(uv, snapped, saturate(_PixelateStrength));
            }

            half3 ESApplyPalette(half3 color)
            {
                float luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                half3 mapped = SAMPLE_TEXTURE2D(
                    _PaletteTex,
                    sampler_PaletteTex,
                    float2(saturate(luminance), saturate(_PaletteRow))).rgb;
                return lerp(color, mapped, saturate(_PaletteStrength));
            }

            half3 ESApplyHalftone(half3 color, float2 uv, out half visibility)
            {
                float angle = radians(_HalftoneAngle);
                float2 directionX = float2(cos(angle), -sin(angle));
                float2 directionY = float2(sin(angle), cos(angle));
                float2 rotated = float2(dot(uv, directionX), dot(uv, directionY));
                float2 cell = frac(rotated * max(4, _HalftoneScale)) - 0.5;
                float luminance = saturate(dot(color, half3(0.2126h, 0.7152h, 0.0722h)));
                float radius = sqrt(1.0 - luminance) * 0.5;
                float distanceToCenter = length(cell);
                float antialias = max(fwidth(distanceToCenter), 0.001);
                float ink = 1.0 - smoothstep(radius - antialias, radius + antialias, distanceToCenter);
                float radialFade = max(
                    (_HalftoneFade - distance(_HalftonePosition.xy, uv))
                    / max(abs(_HalftoneFadeWidth), 0.01),
                    0.0001);
                float2 ssuCell = (frac(abs(uv) * max(4, _HalftoneScale)) * 2.0 - 1.0) / radialFade;
                float ssuDistance = length(ssuCell);
                float ssuAA = max(fwidth(ssuDistance), 0.0001);
                visibility = (half)saturate((1.0 - ssuDistance) / ssuAA);
                visibility = _HalftoneInvert > 0.5 ? 1.0h - visibility : visibility;
                return color * lerp(1.0, 1.0 - ink, saturate(_HalftoneStrength));
            }

            half4 ESApplySDF(half4 sampleColor)
            {
                float distanceValue = sampleColor.a;
                float faceSoftness = max(fwidth(distanceValue) * max(_SDFSoftness, 0.25), 1e-5);
                float outlineSoftness = max(fwidth(distanceValue) * max(_SDFOutlineSoftness, 0.25), 1e-5);
                float face = smoothstep(_SDFThreshold - faceSoftness, _SDFThreshold + faceSoftness, distanceValue);
                float outlineThreshold = _SDFThreshold - _SDFOutlineWidth;
                float outline = smoothstep(
                    outlineThreshold - outlineSoftness,
                    outlineThreshold + outlineSoftness,
                    distanceValue);
                float glowThreshold = outlineThreshold - _SDFGlowWidth;
                float glow = smoothstep(
                    glowThreshold - outlineSoftness * 2.0,
                    glowThreshold + outlineSoftness * 2.0,
                    distanceValue);
                float outlineBand = saturate(outline - face);
                float glowBand = saturate(glow - outline);
                half3 color = sampleColor.rgb * face
                    + _SDFOutlineColor.rgb * outlineBand
                    + _SDFGlowColor.rgb * glowBand;
                half alpha = saturate(max(
                    face,
                    max(outline * _SDFOutlineColor.a, glow * _SDFGlowColor.a)));
                return half4(color, alpha);
            }

            half4 ESApplyTMP(half4 sampleColor, float2 uv, float2 sdfData)
            {
                float scale = max(sdfData.x, 0.0001);
                float weight = lerp(_WeightNormal, _WeightBold, sdfData.y) / 4.0;
                weight = (weight + _FaceDilate) * _ScaleRatioA * 0.5;
                scale /= 1 + (_OutlineSoftness * _ScaleRatioA * scale);
                float bias = (0.5 - weight) * scale - 0.5;
                float outline = _OutlineWidth * _ScaleRatioA * 0.5 * scale;
                float distanceValue = sampleColor.a * scale;
                float outerAlpha = saturate(distanceValue - (bias - outline));
                float faceBlend = saturate(distanceValue - (bias + outline));
                half4 color = lerp(_OutlineColor, _FaceColor, faceBlend) * outerAlpha;
                color.rgb *= sampleColor.rgb;

                float underlayEnabled = _EnableUnderlay;
#if defined(UNDERLAY_ON) || defined(UNDERLAY_INNER)
                underlayEnabled = 1;
#endif
                if (underlayEnabled > 0.5)
                {
                    float2 textureSize = max(float2(_TextureWidth, _TextureHeight), float2(1, 1));
                    float2 underlayOffset = -float2(_UnderlayOffsetX, _UnderlayOffsetY)
                        * (_ScaleRatioC * max(_GradientScale, 0.0001)) / textureSize;
                    float underlayScale = max(sdfData.x, 0.0001);
                    underlayScale /= 1 + (_UnderlaySoftness * _ScaleRatioC * underlayScale);
                    float underlayBias = (0.5 - weight) * underlayScale - 0.5
                        - (_UnderlayDilate * _ScaleRatioC * 0.5 * underlayScale);
                    float underlayDistance = ESSampleUITexture(uv + underlayOffset).a * underlayScale;
                    float underlayAlpha = saturate(underlayDistance - underlayBias);
#if defined(UNDERLAY_INNER)
                    underlayAlpha = (1 - underlayAlpha) * faceBlend;
#endif
                    half alpha = _UnderlayColor.a * underlayAlpha * (1 - color.a);
                    color.rgb += _UnderlayColor.rgb * alpha;
                    color.a += alpha;
                }
                return color;
            }

            half ESResolveUIShadowAlpha(half sourceAlpha, float2 sdfData)
            {
                if (_EnableTMPCompatibility > 0.5)
                {
                    float scale = max(sdfData.x, 0.0001);
                    float weight = lerp(_WeightNormal, _WeightBold, sdfData.y) / 4.0;
                    weight = (weight + _FaceDilate) * _ScaleRatioA * 0.5;
                    scale /= 1 + (_OutlineSoftness * _ScaleRatioA * scale);
                    float bias = (0.5 - weight) * scale - 0.5;
                    float outline = _OutlineWidth * _ScaleRatioA * 0.5 * scale;
                    return (half)saturate(sourceAlpha * scale - (bias - outline));
                }
                if (_EnableSDF > 0.5)
                    return ESApplySDF(half4(0.0h, 0.0h, 0.0h, sourceAlpha)).a;
                return sourceAlpha;
            }

            // Vertex Contracts
            struct ESUIAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 sdfData : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ESUIVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float4 worldPosition : TEXCOORD1;
                float2 sdfData : TEXCOORD2;
                half4 mask : TEXCOORD3;
                float2 tilingPositionWS : TEXCOORD4;
                half4 vertexColor : TEXCOORD5;
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
                float2 localUV = ESAtlasToLocalUV(input.uv);
                float3 positionOS = input.positionOS.xyz;
                if (_EnableTMPCompatibility > 0.5)
                    positionOS.xy += float2(_VertexOffsetX, _VertexOffsetY);
                positionOS = ESApplyVertexMotion(positionOS, localUV);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.worldPosition = float4(positionOS, 1);
                output.tilingPositionWS = TransformObjectToWorld(positionOS).xy;
                output.uv = localUV;
                output.uv = output.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.uv = output.uv * _MainTexScaleOffset.xy + _MainTexScaleOffset.zw;
                output.color = lerp(half4(1, 1, 1, 1), input.color, _VertexColorStrength) * _Color;
                output.vertexColor = input.color;
                float2 uiScale = _EnableTMPCompatibility > 0.5
                    ? float2(_ScaleX, _ScaleY)
                    : float2(1, 1);
                float2 pixelSize = output.positionCS.w;
                pixelSize /= max(float2(0.0001, 0.0001),
                    uiScale * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy)));
                float sdfScale = rsqrt(max(dot(pixelSize, pixelSize), 0.000001));
                sdfScale *= abs(input.sdfData.y) * max(_GradientScale, 0.0001) * max(_Sharpness + 1, 0.0001);
                if (UNITY_MATRIX_P[3][3] == 0)
                    sdfScale *= lerp(1 - saturate(_PerspectiveFilter), 1, saturate(abs(input.normalOS.z)));
                output.sdfData = float2(max(sdfScale, 0.0001), step(input.sdfData.y, 0));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                half2 maskSoftness = _EnableTMPCompatibility > 0.5
                    ? half2(_MaskSoftnessX, _MaskSoftnessY)
                    : half2(_UIMaskSoftnessX, _UIMaskSoftnessY);
                output.mask = half4(
                    positionOS.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * maskSoftness + abs(pixelSize.xy)));
                return output;
            }

            // Fragment Stage
            half4 ESUIFragment(ESUIVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                float timeValue = ESGetTime();
                float2 screenUV = input.positionCS.xy / max(_ScreenParams.xy, float2(1.0, 1.0));
                uv = ESCompositeResolveTilingUV(
                    uv,
                    input.tilingPositionWS,
                    screenUV,
                    _ScreenParams.xy,
                    _TilingMode,
                    _WorldTilingScale.xy,
                    _WorldTilingOffset.xy,
                    _WorldTilingPixelsPerUnit,
                    _ScreenTilingScale.xy,
                    _ScreenTilingOffset.xy,
                    _ScreenTilingPixelsPerUnit);
                if (_EnableUVTransform > 0.5)
                    uv = ESCompositeTransformUV(
                        uv,
                        _UVPivot.xy,
                        _UVScale.xy,
                        _UVOffset.xy,
                        _UVRotation + _UVRotationSpeed * timeValue);
                if (_EnableUVDistort > 0.5)
                {
                    uv = ESCompositeUVDistort(
                        uv,
                        _UVDistortFrequency.xy,
                        _UVDistortSpeed.xy,
                        _UVDistortAmount,
                        timeValue);
                    float2 distortNoiseUV = uv * _UVDistortFrequency.xy + _UVDistortSpeed.xy * timeValue;
                    half distortNoise = SAMPLE_TEXTURE2D(
                        _UVDistortNoiseTex,
                        sampler_UVDistortNoiseTex,
                        distortNoiseUV).r;
                    half distortMask = 1.0h;
                    if (_UVDistortMaskToggle > 0.5)
                        distortMask = ESCompositeSelectChannel(
                            SAMPLE_TEXTURE2D(_UVDistortMask, sampler_UVDistortMask, uv),
                            _UVDistortMaskChannel);
                    uv = ESCompositeUVDistortNoise(
                        uv,
                        distortNoise,
                        _UVDistortFrom.xy,
                        _UVDistortTo.xy,
                        _UVDistortFade,
                        distortMask);
                }
                if (_EnableSqueeze > 0.5)
                    uv = ESCompositeApplySqueezeUV(
                        uv,
                        _SqueezeCenter.xy,
                        _SqueezePower,
                        _SqueezeScale.xy,
                        _SqueezeFade);
                if (_EnableSineRotate > 0.5)
                    uv = ESCompositeApplySineRotateUV(
                        uv,
                        _SineRotatePivot.xy,
                        timeValue,
                        _SineRotateFrequency,
                        _SineRotateAngle,
                        _SineRotateFade);
                float2 fadeCoordinate = uv;
                if (_EnableFullDistortion > 0.5)
                {
                    float fullNoiseX = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        fadeCoordinate * _FullDistortionNoiseScale.xy).r);
                    float fullNoiseY = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        (fadeCoordinate + 0.321) * _FullDistortionNoiseScale.xy).r);
                    uv += (1.0 - saturate(_FullDistortionFade))
                        * (float2(fullNoiseX, fullNoiseY) - 0.5)
                        * _FullDistortionDistortion.xy;
                }
                uv = ESCompositeApplyESNativeDirectionalDistortionUV(uv, fadeCoordinate);
                if (_EnableFlow > 0.5) uv += _FlowSpeed.xy * timeValue * _FlowStrength;

                float fadeMask = 1.0;
                float fadeVisibility = 1.0;
                float fadeEdge = 0.0;
                half fadeNoise = 0.5h;
                if (_FadeMode > 0.5)
                {
                    if ((_FadeMode > 2.5 && _FadeMode < 3.5)
                        || (_FadeMode > 4.5 && _FadeMode < 5.5)
                        || _FadeNoiseFactor > 0.0001)
                    {
                        float2 fadeNoiseUV = fadeCoordinate * _FadeNoiseScale.xy + _FadeNoiseSpeed.xy * timeValue;
                        fadeNoise = SAMPLE_TEXTURE2D(_FadeNoiseTex, sampler_FadeNoiseTex, fadeNoiseUV).r;
                    }
                    if (_FadeMode < 1.5 || (_FadeMode > 3.5 && _FadeMode < 5.5))
                        fadeMask = ESCompositeDirectionalFadeMask(fadeCoordinate, _FadePosition.xy, _FadeRotation);
                    else if (_FadeMode < 2.5)
                        fadeMask = SAMPLE_TEXTURE2D(_FadeMask, sampler_FadeMask, uv).r;
                    else if (_FadeMode < 5.5)
                        fadeMask = fadeNoise;
                    else
                        fadeMask = ESCompositeSourceFadeMask(fadeCoordinate, _FadePosition.xy);

                    if (!(_FadeMode > 2.5 && _FadeMode < 3.5))
                        fadeMask = ESCompositeApplyFadeNoise(fadeMask, fadeNoise, _FadeNoiseFactor);
                    ESCompositeEvaluateFade(
                        fadeMask,
                        _FadeProgress,
                        _FadeWidth,
                        _DissolveEdgeWidth,
                        _FadeInvert,
                        fadeVisibility,
                        fadeEdge);
                    if (_FadeMode > 4.5 && _FadeMode < 5.5)
                    {
                        float2 direction = ESCompositeFadeDirection(_FadeRotation);
                        float2 perpendicular = float2(-direction.y, direction.x);
                        uv += perpendicular * (fadeNoise * 2.0 - 1.0) * fadeEdge * _FadeDistortionStrength;
                    }
                }

                float2 ssuStylizedCoordinate = uv;
                float hologramCoordinate = 0.0;
            #if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
                if (_ESNativeStatusContract > 0.5)
                {
                    if (_EnableHologram > 0.5)
                    {
                        hologramCoordinate = ESCompositeResolveESNativeHologramCoordinate(
                            ssuStylizedCoordinate,
                            float3(input.tilingPositionWS, 0.0));
                        uv = ESCompositeApplyESNativeHologramUV(
                            uv,
                            hologramCoordinate,
                            _MainTex_TexelSize.z,
                            timeValue);
                    }
                    if (_EnableGlitch > 0.5)
                        uv = ESCompositeApplyESNativeGlitchUV(
                            uv,
                            ssuStylizedCoordinate,
                            timeValue);
                }
                else if (_EnableGlitch > 0.5)
                {
                    float glitchScanCoordinate = ESCompositeDirectionalCoordinate2D(
                        ssuStylizedCoordinate,
                        _GlitchScanDirection.xy,
                        float2(0.0, 1.0));
                    float2 glitchScanDirection = _GlitchScanDirection.xy;
                    float glitchScanLength = length(glitchScanDirection);
                    glitchScanDirection = glitchScanLength > 0.0001
                        ? glitchScanDirection / glitchScanLength
                        : float2(0.0, 1.0);
                    float2 glitchPerpendicular = float2(
                        glitchScanDirection.y,
                        -glitchScanDirection.x);
                    float2 glitchCoordinate = float2(
                        dot(ssuStylizedCoordinate, glitchPerpendicular),
                        glitchScanCoordinate);
                    float2 glitchCell = floor(
                        glitchCoordinate * 100 + timeValue * _GlitchSpeed);
                    float glitch = frac(sin(dot(glitchCell, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
                    float2 glitchDirection = _GlitchDistortion.xy;
                    float glitchDirectionLength = length(glitchDirection);
                    glitchDirection = glitchDirectionLength > 0.0001
                        ? glitchDirection / glitchDirectionLength
                        : float2(1.0, 0.0);
                    uv += glitchDirection * glitch * _GlitchAmount;
                }
            #endif
                if (_EnableSmoothPixelArt > 0.5)
                    uv = ESCompositeSmoothPixelUV(uv, ESSpritePixelSize(), _SmoothPixelStrength);
                if (_EnablePixelate > 0.5) uv = ESPixelateUV(uv);

                half4 sampledColor = ESSampleUITexture(uv);
                half4 color = sampledColor + _TextureSampleAdd;
            #if defined(_ES_QUALITY_HIGH)
                if (_EnableSharpen > 0.5 && _SharpenFade > 0.0001 && _SharpenAmount > 0.0001)
                {
                    half4 sharpened = ESSharpenSample(uv, sampledColor) + _TextureSampleAdd;
                    color = lerp(color, sharpened, saturate(_SharpenFade));
                }
                if (_EnableBlur > 0.5 && _BlurIntensity > 0.0001 && _BlurRadius > 0.0001)
                {
                    half4 blurred = _BlurMode > 0.5
                        ? ESGaussianBlurSample(uv, sampledColor)
                        : ESBlurSample(uv, sampledColor);
                    color = lerp(color, blurred + _TextureSampleAdd, saturate(_BlurIntensity));
                }
            #endif
            #if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
                if (_EnableChromatic > 0.5
                    && _ChromaticIntensity > 0.0001
                    && abs(_ChromaticOffset) > 0.000001)
                {
                    float2 chromaDir = float2(cos(radians(_ChromaticAngle)), sin(radians(_ChromaticAngle)));
                    float2 localCoord = frac(ssuStylizedCoordinate);
                    float edgeFactor = saturate(length(localCoord - 0.5) * 2.0);
                    float amount = _ChromaticOffset * lerp(1.0, edgeFactor, _ChromaticEdgeOnly);
                    half3 chroma = color.rgb;
                    chroma.r = (ESSampleUITexture(uv + chromaDir * amount) + _TextureSampleAdd).r;
                    chroma.b = (ESSampleUITexture(uv - chromaDir * amount) + _TextureSampleAdd).b;
                    color.rgb = lerp(color.rgb, chroma, saturate(_ChromaticIntensity));
                }
            #endif
                if (_EnableTMPCompatibility > 0.5)
                    color = ESApplyTMP(color, uv, input.sdfData);
                else if (_EnableSDF > 0.5)
                    color = ESApplySDF(color);
                half untintedAlpha = color.a;
                color *= input.color;
                float customFadeVisibility = 1.0;
                if (_EnableCustomFade > 0.5)
                {
                    half customFadeMask = SAMPLE_TEXTURE2D(
                        _CustomFadeFadeMask,
                        sampler_CustomFadeFadeMask,
                        uv).r;
                    half customFadeNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        uv * _CustomFadeNoiseScale.xy).r);
                    float customFadeVertexAlpha = lerp(1.0, input.vertexColor.a, _VertexColorStrength);
                    customFadeVisibility = ESCompositeCustomFadeVisibility(
                        customFadeVertexAlpha,
                        customFadeMask,
                        customFadeNoise,
                        _CustomFadeSmoothness,
                        _CustomFadeNoiseFactor,
                        _CustomFadeAlpha);
                    color.a = untintedAlpha * _Color.a * customFadeVisibility;
                }
                float ssuFadeVisibility;
                color.rgb = ESCompositeApplyESNativeFadeStackColor(
                    color.rgb,
                    fadeCoordinate,
                    ssuFadeVisibility);
                color.a *= ssuFadeVisibility;
                if (_EnableSmoke > 0.5)
                {
                    float seed = _SmokeVertexSeed > 0.5 ? input.vertexColor.r * 5.0 : 0.0;
                    float2 smokeNoiseUV = (
                        input.uv + timeValue * _SmokeSpeed.xy + seed)
                        * max(_SmokeNoiseScale, 0.01);
                    float smokeNoise = ESCompositePerceptualNoise(
                        SAMPLE_TEXTURE2D(_UberNoiseTexture, sampler_UberNoiseTexture, smokeNoiseUV).r);
                    float smokeMask = ESCompositeSmokeMask(
                        smokeNoise,
                        input.uv,
                        input.vertexColor.a,
                        _SmokeNoiseFactor,
                        _SmokeSmoothness);
                    color = ESCompositeApplySmoke(color, smokeMask, _SmokeAlpha, _SmokeDarkEdge);
                }
                if (_EnableCheckerboard > 0.5)
                    color.rgb = ESCompositeApplyCheckerboard(
                        color.rgb,
                        input.tilingPositionWS,
                        _CheckerboardTiling,
                        _CheckerboardDarken);
                if (_EnableFlame > 0.5)
                {
                    float2 flameNoiseUV = (input.uv + timeValue * _FlameSpeed.xy) * _FlameNoiseScale.xy;
                    float flameNoise = ESCompositePerceptualNoise(
                        SAMPLE_TEXTURE2D(_UberNoiseTexture, sampler_UberNoiseTexture, flameNoiseUV).r);
                    float flameMask = ESCompositeFlameMask(
                        flameNoise,
                        input.uv,
                        _FlameCenter.xy,
                        _FlameDirection.xy,
                        _FlameRadius,
                        _FlameSmooth,
                        _FlameNoiseFactor,
                        _FlameNoiseHeightFactor);
                    color.rgb *= flameMask * _FlameBrightness;
                    color.a *= flameMask;
                }
                if (_EnableAddColor > 0.5)
                {
                    half3 addTint = _AddColor.rgb;
                    if (_AddColorMaskToggle > 0.5)
                    {
                        half4 maskSample = SAMPLE_TEXTURE2D(
                            _AddColorMask,
                            sampler_AddColorMask,
                            uv * _AddColorMask_ST.xy + _AddColorMask_ST.zw);
                        addTint *= maskSample.rgb * maskSample.a;
                    }
                    if (_AddColorContrastToggle > 0.5)
                    {
                        half luminance = max((color.r * 2.0h + color.g * 3.0h + color.b) / 6.0h, 0.0h);
                        addTint *= pow(luminance, max((half)_AddColorContrast, 0.001h));
                    }
                    color.rgb += addTint * _AddColorFade;
                }
                if (_EnableStrongTint > 0.5)
                {
                    half3 strongTint = _StrongTint.rgb;
                    if (_StrongTintMaskToggle > 0.5)
                    {
                        half4 maskSample = SAMPLE_TEXTURE2D(
                            _StrongTintMask,
                            sampler_StrongTintMask,
                            uv * _StrongTintMask_ST.xy + _StrongTintMask_ST.zw);
                        strongTint *= maskSample.rgb * maskSample.a;
                    }
                    if (_StrongTintContrastToggle > 0.5)
                    {
                        half luminance = max((color.r * 2.0h + color.g * 3.0h + color.b) / 6.0h, 0.0h);
                        strongTint *= pow(luminance, max((half)_StrongTintContrast, 0.001h));
                    }
                    color.rgb = lerp(color.rgb, strongTint, saturate(_StrongTintFade));
                }
                if (_EnableColorReplace > 0.5)
                {
                    float colorDistance = distance(color.rgb, _ReplaceFrom.rgb);
                    float replace = 1.0 - smoothstep(
                        _ReplaceRange,
                        _ReplaceRange + _ReplaceSoftness,
                        colorDistance);
                    float replaceLuminance = ESCompositeESNativeLuminance(color.rgb);
                    half3 replaceTarget = _ReplaceTo.rgb
                        * (half)pow(replaceLuminance, max(_ReplaceContrast, 0.001));
                    color.rgb = lerp(
                        color.rgb,
                        replaceTarget,
                        saturate(replace * _ReplaceFade));
                }
                if (_EnableRecolorRGB > 0.5)
                {
                    half mask = 1.0h;
                    if (_RecolorRGBMaskToggle > 0.5)
                        mask = ESCompositeSelectChannel(
                            SAMPLE_TEXTURE2D(_RecolorRGBMask, sampler_RecolorRGBMask, uv),
                            _RecolorRGBMaskChannel);
                    half3 recolored = ESCompositeRecolorRGB(
                        color.rgb,
                        _RecolorRed.rgb,
                        _RecolorGreen.rgb,
                        _RecolorBlue.rgb);
                    color.rgb = lerp(color.rgb, recolored, saturate(_RecolorRGBStrength * mask));
                }
                if (_EnableRecolorRGBYCP > 0.5)
                {
                    half mask = 1.0h;
                    if (_RecolorRGBYCPMaskToggle > 0.5)
                        mask = ESCompositeSelectChannel(
                            SAMPLE_TEXTURE2D(_RecolorRGBYCPMask, sampler_RecolorRGBYCPMask, uv),
                            _RecolorRGBYCPMaskChannel);
                    half3 recolored = ESCompositeRecolorRGBYCP(
                        color.rgb,
                        _RecolorRGBYCPRed.rgb,
                        _RecolorRGBYCPGreen.rgb,
                        _RecolorRGBYCPBlue.rgb,
                        _RecolorRGBYCPYellow.rgb,
                        _RecolorRGBYCPCyan.rgb,
                        _RecolorRGBYCPPurple.rgb);
                    color.rgb = lerp(color.rgb, recolored, saturate(_RecolorRGBYCPStrength * mask));
                }
                if (_EnableBrightness > 0.5)
                    color.rgb *= _Brightness;
                if (_EnableContrast > 0.5)
                    color.rgb = (color.rgb - 0.5h) * _Contrast + 0.5h;
                if (_EnableHue > 0.5)
                {
                    float3 hsv = ESCompositeRgbToHsv(color.rgb);
                    hsv.x = frac(hsv.x + _Hue);
                    color.rgb = ESCompositeHsvToRgb(hsv);
                }
                if (_EnableSplitToning > 0.5)
                    color.rgb = ESCompositeSplitTone(
                        color.rgb,
                        _SplitToneShadows.rgb,
                        _SplitToneHighlights.rgb,
                        _SplitToneBalance,
                        _SplitToneStrength,
                        _SplitToneContrast,
                        _SplitToneShift);
                if (_EnableBlackTint > 0.5)
                    color.rgb = ESCompositeApplyBlackTint(
                        color.rgb,
                        _BlackTintColor.rgb,
                        _BlackTintPower,
                        _BlackTintFade);
                if (_EnableInkSpread > 0.5)
                {
                    float inkNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        uv * _InkSpreadNoiseScale.xy).r);
                    float inkMask = saturate((
                        _InkSpreadDistance
                        - distance(_InkSpreadPosition.xy, uv)
                        + inkNoise * _InkSpreadNoiseFactor)
                        / max(_InkSpreadWidth, 0.001));
                    color.rgb = ESCompositeApplyInkSpread(
                        color.rgb,
                        _InkSpreadColor.rgb,
                        _InkSpreadContrast,
                        _InkSpreadFade,
                        inkMask);
                }
                if (_EnableShiftHue > 0.5)
                    color.rgb = ESCompositeApplyShiftHue(color.rgb, timeValue, _ShiftHueSpeed);
                if (_EnableAddHue > 0.5)
                {
                    float addHueMask = 1.0;
                    if (_AddHueMaskToggle > 0.5)
                    {
                        half4 maskSample = SAMPLE_TEXTURE2D(
                            _AddHueMask,
                            sampler_AddHueMask,
                            uv * _AddHueMask_ST.xy + _AddHueMask_ST.zw);
                        addHueMask = maskSample.r * maskSample.a;
                    }
                    color.rgb = ESCompositeApplyAddHue(
                        color.rgb,
                        timeValue,
                        _AddHueSpeed,
                        _AddHueSaturation,
                        _AddHueBrightness,
                        _AddHueContrast,
                        _AddHueFade * addHueMask);
                }
                if (_EnableSineGlow > 0.5)
                {
                    half3 sineGlowColor = _SineGlowColor.rgb;
                    if (_SineGlowMaskToggle > 0.5)
                    {
                        half4 maskSample = SAMPLE_TEXTURE2D(
                            _SineGlowMask,
                            sampler_SineGlowMask,
                            uv * _SineGlowMask_ST.xy + _SineGlowMask_ST.zw);
                        sineGlowColor *= maskSample.rgb * maskSample.a;
                    }
                    color.rgb = ESCompositeApplySineGlow(
                        color.rgb,
                        sineGlowColor,
                        timeValue,
                        _SineGlowContrast,
                        _SineGlowFrequency,
                        _SineGlowMin,
                        _SineGlowMax,
                        _SineGlowFade);
                }
            #if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
                if (_ESNativeStatusContract > 0.5)
                {
                    if (_EnableTMPCompatibility < 0.5 && _EnableSDF < 0.5
                        && (_EnableInnerOutline > 0.5 || _EnableOuterOutline > 0.5 || _EnablePixelOutline > 0.5))
                        ESApplyESNativeExactOutlines(uv, timeValue, color);
                    if (_EnableHologram > 0.5)
                        color = ESCompositeApplyESNativeHologramColor(
                            color,
                            hologramCoordinate,
                            timeValue);
                    if (_EnableGlitch > 0.5)
                        color.rgb = ESCompositeApplyESNativeGlitchColor(
                            color.rgb,
                            ssuStylizedCoordinate,
                            timeValue);
                }
            #endif
                if (_EnableCamouflage > 0.5)
                {
                    float2 camouflageUV = uv;
                    if (_CamouflageAnimationToggle > 0.5)
                    {
                        float distortionNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                            _UberNoiseTexture,
                            sampler_UberNoiseTexture,
                            (uv + timeValue * _CamouflageDistortionSpeed.xy)
                                * _CamouflageDistortionScale.xy).r);
                        camouflageUV += (distortionNoise - 0.25) * _CamouflageDistortionIntensity.xy;
                    }
                    float camouflageNoiseA = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        camouflageUV * _CamouflageNoiseScaleA.xy).r);
                    float camouflageNoiseB = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        (camouflageUV + 12.3) * _CamouflageNoiseScaleB.xy).r);
                    color.rgb = ESCompositeApplyCamouflage(
                        color.rgb,
                        _CamouflageBaseColor.rgb,
                        _CamouflageColorA.rgb,
                        _CamouflageDensityA,
                        _CamouflageSmoothnessA,
                        camouflageNoiseA,
                        _CamouflageColorB.rgb,
                        _CamouflageDensityB,
                        _CamouflageSmoothnessB,
                        camouflageNoiseB,
                        _CamouflageContrast,
                        _CamouflageFade);
                }
                if (_EnableMetal > 0.5)
                {
                    float metalDistortionNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        (uv + timeValue * _MetalNoiseDistortionSpeed.xy)
                            * _MetalNoiseDistortionScale.xy).r);
                    float2 metalNoiseUV = (
                        (metalDistortionNoise - 0.25) * _MetalNoiseDistortion.xy
                        + uv
                        + timeValue * _MetalNoiseSpeed.xy) * _MetalNoiseScale.xy;
                    float metalNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        metalNoiseUV).r);
                    half metalMask = 1.0h;
                    if (_MetalMaskToggle > 0.5)
                    {
                        half4 metalMaskSample = SAMPLE_TEXTURE2D(_MetalMask, sampler_MetalMask, uv);
                        metalMask = metalMaskSample.r * metalMaskSample.a;
                    }
                    color.rgb = ESCompositeApplyMetal(
                        color.rgb,
                        _MetalColor.rgb,
                        _MetalContrast,
                        _MetalHighlightColor.rgb,
                        _MetalHighlightDensity,
                        _MetalHighlightContrast,
                        metalNoise,
                        _MetalFade * metalMask);
                }
                if (_ESNativeStatusContract > 0.5)
                {
                    float2 exactShineSource = uv;
                    if (_ShineSpace > 1.5)
                        exactShineSource = input.tilingPositionWS;
                    float exactShineCoordinate = ESCompositeShineCoordinate2D(
                        exactShineSource,
                        _ShineDirection.xy,
                        _ShineRotation);
                    color.rgb = ESCompositeApplyESNativeStatusEffects(
                        color.rgb,
                        uv,
                        uv,
                        exactShineCoordinate,
                        timeValue);
                }
                if (_EnableEnchanted > 0.5)
                {
                    float2 enchantedScroll = timeValue * _EnchantedSpeed.xy;
                    float enchantedNoiseA = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        (uv - (enchantedScroll + float2(1.234, 5.6789)) * float2(0.95, 1.05))
                            * _EnchantedScale.xy).r);
                    float enchantedNoiseB = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        (uv + enchantedScroll) * _EnchantedScale.xy).r);
                    color.rgb = ESCompositeApplyEnchanted(
                        color.rgb,
                        enchantedNoiseA,
                        enchantedNoiseB,
                        timeValue,
                        _EnchantedLowColor.rgb,
                        _EnchantedHighColor.rgb,
                        _EnchantedRainbowToggle,
                        _EnchantedRainbowSpeed,
                        _EnchantedRainbowDensity,
                        _EnchantedRainbowSaturation,
                        _EnchantedBrightness,
                        _EnchantedContrast,
                        _EnchantedReduce,
                        _EnchantedFade,
                        _EnchantedLerpToggle);
                }
                if (_EnableShifting > 0.5)
                    color.rgb = ESCompositeApplyShifting(
                        color.rgb,
                        timeValue,
                        _ShiftingSpeed,
                        _ShiftingDensity,
                        _ShiftingBrightness,
                        _ShiftingSaturation,
                        _ShiftingContrast,
                        _ShiftingColorA.rgb,
                        _ShiftingColorB.rgb,
                        _ShiftingRainbowToggle,
                        _ShiftingFade);
                if (_EnableSaturation > 0.5)
                {
                    float luminance = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
                    color.rgb = lerp(luminance.xxx, color.rgb, _Saturation);
                }
                if (_EnableNegative > 0.5)
                    color.rgb = lerp(color.rgb, 1.0h - color.rgb, _NegativeFade);
                if (_ESNativeStatusContract <= 0.5 && _EnableRainbow > 0.5)
                {
                    float rainbowCoordinate = ESCompositeDirectionalCoordinate2D(
                        uv,
                        _RainbowDirection.xy,
                        float2(0.0, 1.0));
                    float hue = frac(rainbowCoordinate * _RainbowDensity + timeValue * _RainbowSpeed);
                    half3 rainbow = (half3)ESCompositeHsvToRgb(float3(hue, 1.0, 1.0));
                    color.rgb = lerp(color.rgb, rainbow * _RainbowBrightness, 0.5h);
                }
                if (_EnableTMPCompatibility < 0.5 && _EnableSDF < 0.5)
                {
                    if (_ESNativeStatusContract <= 0.5 && _EnableInnerOutline > 0.5)
                    {
                        half positive = ESSampleUITexture(uv + float2(_InnerOutlineWidth, 0.0)).a;
                        half negative = ESSampleUITexture(uv - float2(_InnerOutlineWidth, 0.0)).a;
                        half neighborAlpha = min(positive, negative) * input.color.a;
                        float edge = saturate(color.a - neighborAlpha);
                        color.rgb = lerp(color.rgb, _InnerOutlineColor.rgb, edge);
                    }
                    if (_ESNativeStatusContract <= 0.5
                        && (_EnableOuterOutline > 0.5 || _EnablePixelOutline > 0.5))
                    {
                        float2 width = _EnablePixelOutline > 0.5
                            ? rcp(ESSpritePixelSize()) * _PixelOutlineWidth
                            : _OuterOutlineWidth.xx;
                        half around = 0.0h;
                        around = max(around, ESSampleUITexture(uv + float2(width.x, 0.0)).a);
                        around = max(around, ESSampleUITexture(uv - float2(width.x, 0.0)).a);
                        around = max(around, ESSampleUITexture(uv + float2(0.0, width.y)).a);
                        around = max(around, ESSampleUITexture(uv - float2(0.0, width.y)).a);
                        around *= input.color.a * saturate((half)customFadeVisibility);
                        float edge = saturate(around - color.a);
                        half3 outlineColor = _EnablePixelOutline > 0.5
                            ? _PixelOutlineColor.rgb
                            : _OuterOutlineColor.rgb;
                        color.rgb = lerp(color.rgb, outlineColor, edge);
                        color.a = max(color.a, around);
                    }
                }
                if (_FadeMode > 0.5)
                {
                    color.a *= fadeVisibility;
                    if ((_FadeMode > 3.5 && _FadeMode < 4.5) || _FadeMode > 6.5)
                        color.rgb += _DissolveEdgeColor.rgb * fadeEdge * _DissolveEdgeIntensity;
                }
                if (_ESNativeStatusContract <= 0.5 && _EnableShine > 0.5)
                {
                    float2 shineSource = _ShineSpace > 1.5
                        ? input.tilingPositionWS
                        : uv;
                    float shineCoordinate = ESCompositeShineCoordinate2D(
                        shineSource,
                        _ShineDirection.xy,
                        _ShineAngle);
                    float shine = 1.0 - smoothstep(
                        0.0,
                        _ShineWidth,
                        abs(frac(shineCoordinate + ESGetTime() * _ShineSpeed) - 0.5));
                    color.rgb += _ShineColor.rgb * shine * _ShineIntensity;
                }
            #if defined(_ES_QUALITY_HIGH)
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
            #endif
                if (_EnablePingPongGlow > 0.5)
                {
                    float wave = 0.5 + 0.5 * sin(timeValue * _GlowFrequency);
                    float glowLuminance = ESCompositeESNativeLuminance(color.rgb);
                    color.rgb += lerp(_GlowFrom.rgb, _GlowTo.rgb, wave)
                        * (half)pow(glowLuminance, max(_GlowContrast, 0.001))
                        * _GlowIntensity
                        * _GlowFade;
                }
            #if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
                if (_ESNativeStatusContract <= 0.5 && _EnableHologram > 0.5)
                {
                    float2 legacyHologramDirection = _HologramDirection.xy;
                    float legacyHologramDirectionLength = length(legacyHologramDirection);
                    legacyHologramDirection = legacyHologramDirectionLength > 0.0001
                        ? legacyHologramDirection / legacyHologramDirectionLength
                        : float2(0.0, 1.0);
                    float legacyHologramCoordinate = dot(uv, legacyHologramDirection);
                    float hologram = 0.5 + 0.5 * sin(
                        legacyHologramCoordinate * _HologramFrequency + ESGetTime() * _HologramSpeed);
                    color.rgb = lerp(
                        color.rgb,
                        _HologramColor.rgb,
                        hologram * saturate((half)_HologramFade));
                }
                color.rgb = ESApplyTextureLayers(color.rgb, uv, ESGetTime());
                if (_EnablePalette > 0.5) color.rgb = ESApplyPalette(color.rgb);
                if (_EnableHalftone > 0.5)
                {
                    half halftoneVisibility;
                    color.rgb = ESApplyHalftone(color.rgb, uv, halftoneVisibility);
                    color.a *= lerp(1.0h, halftoneVisibility, step(0.5, _HalftoneAlphaPattern));
                }
            #endif
                if (_ESNativeStatusContract <= 0.5
                    && (_EnableFrozen > 0.5 || _EnableBurn > 0.5 || _EnablePoison > 0.5))
                {
                    float statusNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        uv * float2(1.37, 1.91) + timeValue * float2(0.07, -0.05)).r);
                    if (_EnableFrozen > 0.5)
                    {
                        float snow = smoothstep(1.0 - _FrozenDensity, 1.0, statusNoise);
                        color.rgb = lerp(color.rgb, _FrozenColor.rgb, 0.65h);
                        color.rgb += _FrozenHighlight.rgb * snow
                            * (0.5 + 0.5 * sin(timeValue * _FrozenSpeed + statusNoise * 6.0));
                    }
                    if (_EnableBurn > 0.5)
                    {
                        float burn = smoothstep(
                            _BurnProgress - _BurnWidth,
                            _BurnProgress + _BurnWidth,
                            statusNoise);
                        color.rgb = lerp(_BurnInsideColor.rgb, _BurnEdgeColor.rgb, burn);
                        color.a *= step(_BurnProgress - 0.02, statusNoise);
                    }
                    if (_EnablePoison > 0.5)
                    {
                        float poison = 0.5 + 0.5 * sin(
                            timeValue * _PoisonSpeed + statusNoise * _PoisonDensity * 6.0);
                        color.rgb = lerp(color.rgb, _PoisonColor.rgb, saturate(poison * 0.45));
                    }
                }
                if (_EnableAlphaTint > 0.5)
                {
                    float alphaTintWeight = (1.0 - saturate(color.a))
                        * step(_AlphaTintMin, color.a)
                        * saturate(_AlphaTintFade);
                    color.rgb = lerp(color.rgb, _AlphaTint.rgb, alphaTintWeight);
                }
                half fullGlowVisibility = 1.0h;
                if (_EnableFullGlowDissolve > 0.5)
                {
                    float fullGlowNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        uv * _FullGlowDissolveNoiseScale.xy).r);
                    color = ESCompositeApplyFullGlowDissolve(
                        color,
                        fullGlowNoise,
                        _FullGlowDissolveFade,
                        _FullGlowDissolveWidth,
                        _FullGlowDissolveEdgeColor.rgb,
                        fullGlowVisibility);
                }
                if (_EnableShadow > 0.5)
                {
                    float2 shadowUVOffset = _ShadowOffset.xy * 100.0 / ESSpritePixelSize();
                    half shadowSourceAlpha = ESSampleUITexture(uv - shadowUVOffset).a;
                    half shadowTintAlpha = _EnableCustomFade > 0.5
                        ? _Color.a
                        : input.color.a;
                    half shadowAlpha = ESResolveUIShadowAlpha(shadowSourceAlpha, input.sdfData)
                        * saturate((half)_ShadowFade)
                        * shadowTintAlpha
                        * saturate((half)fadeVisibility)
                        * saturate((half)customFadeVisibility)
                        * saturate((half)ssuFadeVisibility)
                        * fullGlowVisibility;
                    color = ESCompositeApplySpriteShadow(color, _ShadowColor.rgb, shadowAlpha);
                }
            #ifdef UNITY_UI_CLIP_RECT
                half2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                color *= mask.x * mask.y;
            #endif
            #if defined(UNITY_UI_ALPHACLIP)
                clip(color.a - 0.001);
            #endif
                if (_AlphaClip > 0.5) clip(color.a - _Cutoff);
                if (_BlendMode > 1.5 && _BlendMode < 2.5) color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "ES.EditorInternal.ESCompositeShaderGUI"
}
