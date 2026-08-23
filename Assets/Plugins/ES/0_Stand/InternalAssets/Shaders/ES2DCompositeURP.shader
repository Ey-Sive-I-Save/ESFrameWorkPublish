Shader "ES/2D/Composite URP"
{
    Properties
    {
        [HideInInspector] _ESMaterialVersion ("ES Material Version", Float) = 1
        // Base Input
        [PerRendererData] _MainTex ("主纹理", 2D) = "white" {}
        _Color ("基础颜色", Color) = (1,1,1,1)
        _MainTexScaleOffset ("主纹理缩放/偏移", Vector) = (1,1,0,0)
        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        [HideInInspector] _SpriteUVTransformX ("Sprite UV Transform X", Vector) = (1,0,0,0)
        [HideInInspector] _SpriteUVTransformY ("Sprite UV Transform Y", Vector) = (0,1,0,0)
        [HideInInspector] _SpriteUVTransformValid ("Sprite UV Transform Valid", Float) = 0
        _VertexColorStrength ("顶点色强度", Range(0,1)) = 1
        [NoScaleOffset] _MaskTex ("2D 光照遮罩", 2D) = "white" {}
        [NoScaleOffset] _NormalMap ("2D 法线纹理", 2D) = "bump" {}
        _NormalScale ("2D 法线强度", Range(0,2)) = 1

        // SpriteRenderer Compatibility
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        // Time And Coordinates
        [Enum(UV,0,World,1,Screen,2)] _CoordinateMode ("坐标空间", Float) = 0
        [Enum(SceneTime,0,UnscaledTime,1,CustomTime,2)] _TimeMode ("时间来源", Float) = 0
        _CustomTime ("自定义时间", Float) = 0
        _TimeScale ("时间倍率", Range(-4,4)) = 1
        [Toggle] _EnableTimeFPS ("启用时间帧率量化", Float) = 0
        _TimeFPS ("时间帧率", Range(0.01,240)) = 5
        [Toggle] _EnableTimeFrequency ("启用周期时间", Float) = 0
        _TimeFrequency ("时间周期频率", Float) = 2
        _TimeRange ("时间周期范围", Float) = 0.5
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

        // SSU UV Motion
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

        // Vertex Motion
        [Toggle] _EnableWind ("启用风摆", Float) = 0
        _WindDirection ("风摆方向", Vector) = (1,0,0,0)
        _WindAmplitude ("风摆幅度", Range(0,2)) = 0.05
        _WindFrequency ("风摆频率", Range(0,32)) = 4
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
        _VibrateAmplitude ("震动幅度", Range(0,0.5)) = 0.02
        _VibrateDirection ("震动主方向", Vector) = (1,0,0,0)
        _VibrateSpeed ("震动速度", Range(0,32)) = 12

        [Toggle] _EnableSineMove ("启用正弦移动", Float) = 0
        _SineMoveFade ("正弦移动强度", Range(0,1)) = 1
        _SineMoveOffset ("正弦移动偏移", Vector) = (0,0.5,0,0)
        _SineMoveFrequency ("正弦移动频率", Vector) = (1,1,0,0)
        [Toggle] _EnableSineScale ("启用正弦缩放", Float) = 0
        _SineScaleFrequency ("正弦缩放频率", Float) = 2
        _SineScaleFactor ("正弦缩放幅度", Vector) = (0.2,0.2,0,0)

        // Time And Coordinates - Sequence Animation
        [Enum(Off,0,Sequence,1)] _AnimationMode ("序列帧模式", Float) = 0
        _SequenceColumns ("序列帧列数", Float) = 1
        _SequenceRows ("序列帧行数", Float) = 1
        _SequenceFrame ("序列帧当前帧", Float) = 0
        _SequenceSpeed ("序列帧速度", Float) = 0

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

        // SSU Alpha And Dissolve
        // SSU Composable Fade Stack
        [Toggle] _EnableFullAlphaDissolve ("启用 SSU 全局透明溶解", Float) = 0
        _FullAlphaDissolveFade ("全局透明溶解进度", Float) = 0.5
        _FullAlphaDissolveWidth ("全局透明溶解宽度", Float) = 0.5
        _FullAlphaDissolveNoiseScale ("全局透明溶解噪声缩放", Vector) = (0.1,0.1,0,0)
        [Toggle] _EnableSourceAlphaDissolve ("启用 SSU 源点透明溶解", Float) = 0
        _SourceAlphaDissolveFade ("源点透明溶解进度", Float) = 1
        _SourceAlphaDissolvePosition ("源点透明溶解位置", Vector) = (0,0,0,0)
        _SourceAlphaDissolveWidth ("源点透明溶解宽度", Float) = 0.2
        _SourceAlphaDissolveNoiseScale ("源点透明溶解噪声缩放", Vector) = (0.3,0.3,0,0)
        _SourceAlphaDissolveNoiseFactor ("源点透明溶解噪声影响", Float) = 0.2
        [Toggle] _SourceAlphaDissolveInvert ("反转源点透明溶解", Float) = 0
        [Toggle] _EnableSourceGlowDissolve ("启用 SSU 源点辉光溶解", Float) = 0
        _SourceGlowDissolveFade ("源点辉光溶解进度", Float) = 1
        _SourceGlowDissolvePosition ("源点辉光溶解位置", Vector) = (0,0,0,0)
        _SourceGlowDissolveWidth ("源点辉光溶解宽度", Float) = 0.1
        [HDR] _SourceGlowDissolveEdgeColor ("源点辉光溶解边缘颜色", Color) = (11.98431,0.627451,0.627451,0)
        _SourceGlowDissolveNoiseScale ("源点辉光溶解噪声缩放", Vector) = (0.3,0.3,0,0)
        _SourceGlowDissolveNoiseFactor ("源点辉光溶解噪声影响", Float) = 0.2
        [Toggle] _SourceGlowDissolveInvert ("反转源点辉光溶解", Float) = 0
        [Toggle] _EnableDirectionalAlphaFade ("启用 SSU 方向透明渐隐", Float) = 0
        _DirectionalAlphaFadeFade ("方向透明渐隐进度", Float) = 0
        _DirectionalAlphaFadeRotation ("方向透明渐隐角度", Range(0,360)) = 0
        _DirectionalAlphaFadeWidth ("方向透明渐隐宽度", Float) = 0.2
        _DirectionalAlphaFadeNoiseScale ("方向透明渐隐噪声缩放", Vector) = (0.3,0.3,0,0)
        _DirectionalAlphaFadeNoiseFactor ("方向透明渐隐噪声影响", Float) = 0.2
        [Toggle] _DirectionalAlphaFadeInvert ("反转方向透明渐隐", Float) = 0
        [Toggle] _EnableDirectionalGlowFade ("启用 SSU 方向辉光渐隐", Float) = 0
        _DirectionalGlowFadeFade ("方向辉光渐隐进度", Float) = 0
        _DirectionalGlowFadeRotation ("方向辉光渐隐角度", Range(0,360)) = 0
        [HDR] _DirectionalGlowFadeEdgeColor ("方向辉光渐隐边缘颜色", Color) = (11.98431,0.6901961,0.6901961,0)
        _DirectionalGlowFadeWidth ("方向辉光渐隐宽度", Float) = 0.1
        _DirectionalGlowFadeNoiseScale ("方向辉光渐隐噪声缩放", Vector) = (0.4,0.4,0,0)
        _DirectionalGlowFadeNoiseFactor ("方向辉光渐隐噪声影响", Float) = 0.2
        [Toggle] _DirectionalGlowFadeInvert ("反转方向辉光渐隐", Float) = 0
        [Toggle] _EnableDirectionalDistortion ("启用 SSU 方向扰动渐隐", Float) = 0
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
        [Toggle] _EnableShadow ("启用精灵阴影", Float) = 0
        _ShadowFade ("精灵阴影强度", Range(0,1)) = 1
        _ShadowOffset ("精灵阴影偏移", Vector) = (0.05,-0.05,0,0)
        _ShadowColor ("精灵阴影颜色", Color) = (0,0,0,0)
        [Toggle] _EnableColorReplace ("启用颜色替换", Float) = 0
        _ReplaceFrom ("替换源颜色", Color) = (0,0,0,1)
        [HDR] _ReplaceTo ("替换目标颜色", Color) = (1,1,1,1)
        _ReplaceRange ("替换范围", Range(0,1)) = 0.1
        _ReplaceSoftness ("替换柔和度", Range(0.001,1)) = 0.1
        _ReplaceContrast ("替换亮度对比", Range(0.001,8)) = 1
        _ReplaceFade ("替换强度", Range(0,1)) = 1
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

        // SSU Pattern And Material Effects
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
        [HideInInspector] _RainbowFade ("SSU 彩虹强度", Range(0,1)) = 1
        [HideInInspector] _RainbowSaturation ("SSU 彩虹饱和度", Range(0,1)) = 1
        [HideInInspector] _RainbowContrast ("SSU 彩虹对比度", Float) = 1
        [HideInInspector] _RainbowCenter ("SSU 彩虹中心", Vector) = (0,0,0,0)
        [HideInInspector] _RainbowNoiseScale ("SSU 彩虹噪声缩放", Vector) = (0.2,0.2,0,0)
        [HideInInspector] _RainbowNoiseFactor ("SSU 彩虹噪声因子", Float) = 0.2

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
        [Toggle] _HalftoneAlphaPattern ("使用 SSU 透明点阵", Float) = 0

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

        // Outlines
        [Toggle] _EnableInnerOutline ("启用内描边", Float) = 0
        [HDR] _InnerOutlineColor ("内描边颜色", Color) = (1,0.2,0.05,1)
        _InnerOutlineWidth ("内描边宽度", Float) = 0.08
        [HideInInspector] _InnerOutlineFade ("SSU 内描边淡入", Range(0,1)) = 1
        [HideInInspector][Toggle] _InnerOutlineDistortionToggle ("SSU 内描边扰动", Float) = 0
        [HideInInspector] _InnerOutlineDistortionIntensity ("SSU 内描边扰动强度", Vector) = (0.01,0.01,0,0)
        [HideInInspector] _InnerOutlineNoiseScale ("SSU 内描边噪声缩放", Vector) = (4,4,0,0)
        [HideInInspector] _InnerOutlineNoiseSpeed ("SSU 内描边噪声速度", Vector) = (0,0.1,0,0)
        [HideInInspector][Toggle] _InnerOutlineTextureToggle ("SSU 内描边纹理着色", Float) = 0
        [HideInInspector] _InnerOutlineTintTexture ("SSU 内描边纹理", 2D) = "white" {}
        [HideInInspector] _InnerOutlineTextureSpeed ("SSU 内描边纹理速度", Vector) = (0.5,0,0,0)
        [HideInInspector][Toggle] _InnerOutlineOutlineOnlyToggle ("SSU 仅显示内描边", Float) = 0
        [Toggle] _EnableOuterOutline ("启用外描边", Float) = 0
        [HDR] _OuterOutlineColor ("外描边颜色", Color) = (0,0,0,1)
        _OuterOutlineWidth ("外描边宽度", Float) = 0.005
        [HideInInspector] _OuterOutlineFade ("SSU 外描边淡入", Range(0,1)) = 1
        [HideInInspector][Toggle] _OuterOutlineDistortionToggle ("SSU 外描边扰动", Float) = 0
        [HideInInspector] _OuterOutlineDistortionIntensity ("SSU 外描边扰动强度", Vector) = (0.01,0.01,0,0)
        [HideInInspector] _OuterOutlineNoiseScale ("SSU 外描边噪声缩放", Vector) = (4,4,0,0)
        [HideInInspector] _OuterOutlineNoiseSpeed ("SSU 外描边噪声速度", Vector) = (0,0.1,0,0)
        [HideInInspector][Toggle] _OuterOutlineTextureToggle ("SSU 外描边纹理着色", Float) = 0
        [HideInInspector] _OuterOutlineTintTexture ("SSU 外描边纹理", 2D) = "white" {}
        [HideInInspector] _OuterOutlineTextureSpeed ("SSU 外描边纹理速度", Vector) = (0.5,0,0,0)
        [HideInInspector][Toggle] _OuterOutlineOutlineOnlyToggle ("SSU 仅显示外描边", Float) = 0
        [Toggle] _EnablePixelOutline ("启用像素描边", Float) = 0
        [HDR] _PixelOutlineColor ("像素描边颜色", Color) = (1,1,1,1)
        _PixelOutlineWidth ("像素描边宽度", Float) = 1
        [HideInInspector] _PixelOutlineFade ("SSU 像素描边淡入", Range(0,1)) = 1
        [HideInInspector][Toggle] _PixelOutlineTextureToggle ("SSU 像素描边纹理着色", Float) = 0
        [HideInInspector] _PixelOutlineTintTexture ("SSU 像素描边纹理", 2D) = "white" {}
        [HideInInspector] _PixelOutlineTextureSpeed ("SSU 像素描边纹理速度", Vector) = (0.5,0,0,0)
        [HideInInspector][Toggle] _PixelOutlineOutlineOnlyToggle ("SSU 仅显示像素描边", Float) = 0

        // Dynamic Effects
        [Toggle] _EnableShine ("启用扫光", Float) = 0
        [HDR] _ShineColor ("扫光颜色", Color) = (1,1,1,1)
        _ShineSpeed ("扫光速度", Float) = 1
        _ShineWidth ("扫光宽度", Float) = 0.15
        [Enum(CompatibleDefault,0,LocalUV,1,WorldProjection,2)] _ShineSpace ("扫光空间", Float) = 0
        _ShineDirection ("扫光方向", Vector) = (0,0,0,0)
        _ShineAngle ("扫光角度（方向为零时）", Range(0,360)) = 30
        _ShineIntensity ("扫光强度", Range(0,8)) = 1
        [HideInInspector] _ShineFade ("SSU 扫光强度", Range(0,1)) = 1
        [HideInInspector] _ShineSaturation ("SSU 扫光饱和度", Range(0,1)) = 0.5
        [HideInInspector] _ShineContrast ("SSU 扫光对比度", Float) = 2
        [HideInInspector] _ShineRotation ("SSU 扫光旋转", Range(0,360)) = 30
        [HideInInspector] _ShineSmooth ("SSU 扫光平滑度", Float) = 1
        [HideInInspector] _ShineFrequency ("SSU 扫光频率", Float) = 0.3
        [Toggle] _ShineMaskToggle ("SSU 扫光遮罩", Float) = 0
        [NoScaleOffset] _ShineMask ("SSU 扫光遮罩纹理", 2D) = "white" {}
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

        [Toggle] _EnableDistortion ("启用噪声扰动", Float) = 0
        [NoScaleOffset] _NoiseTex ("噪声纹理", 2D) = "gray" {}
        _NoiseScale ("噪声缩放", Vector) = (1,1,0,0)
        _NoiseSpeed ("噪声速度", Vector) = (0,0,0,0)
        _DistortionStrength ("扰动强度", Range(0,0.2)) = 0.02
        _DistortionDirection ("扰动方向与轴强度", Vector) = (1,1,0,0)
        [Toggle] _EnableFullDistortion ("启用 SSU 全局扰动", Float) = 0
        _FullDistortionFade ("全局扰动淡出", Range(0,1)) = 1
        _FullDistortionDistortion ("全局扰动方向强度", Vector) = (0.2,0.2,0,0)
        _FullDistortionNoiseScale ("全局扰动噪声缩放", Vector) = (0.5,0.5,0,0)

        [Toggle] _EnableHologram ("启用全息", Float) = 0
        [HDR] _HologramColor ("全息颜色", Color) = (0.1,0.8,1,1)
        [HideInInspector] _HologramFade ("SSU 全息淡入", Range(0,1)) = 1
        [HideInInspector] _HologramContrast ("SSU 全息对比度", Float) = 1
        [Enum(LocalUV,0,WorldProjection,1)] _HologramSpace ("全息扫描空间", Float) = 1
        _HologramDirection ("全息扫描方向", Vector) = (0,1,0,0)
        _HologramLineFrequency ("全息线频率", Float) = 80
        _HologramLineGap ("全息线间隔", Float) = 0.35
        _HologramSpeed ("全息速度", Float) = 1
        _HologramMinAlpha ("全息最低透明度", Range(0,1)) = 0.2
        [HideInInspector] _HologramDistortionOffset ("SSU 全息扰动偏移", Float) = 0.5
        _HologramDistortionDirection ("全息扰动方向", Vector) = (1,0,0,0)
        [HideInInspector] _HologramDistortionSpeed ("SSU 全息扰动速度", Float) = 2
        [HideInInspector] _HologramDistortionDensity ("SSU 全息扰动密度", Float) = 0.5
        [HideInInspector] _HologramDistortionScale ("SSU 全息扰动缩放", Float) = 10
        [Toggle] _EnableGlitch ("启用故障", Float) = 0
        _GlitchIntensity ("故障强度", Range(0,0.2)) = 0.03
        _GlitchSpeed ("故障速度", Float) = 3
        _GlitchScanDirection ("故障条带方向", Vector) = (0,1,0,0)
        [HideInInspector] _GlitchFade ("SSU 故障淡入", Range(0,1)) = 1
        [HideInInspector] _GlitchMaskMin ("SSU 故障遮罩下限", Range(0,1)) = 0.4
        [HideInInspector] _GlitchMaskScale ("SSU 故障遮罩缩放", Vector) = (0,0.2,0,0)
        [HideInInspector] _GlitchMaskSpeed ("SSU 故障遮罩速度", Vector) = (0,4,0,0)
        [HideInInspector] _GlitchHueSpeed ("SSU 故障色相速度", Float) = 1
        [HideInInspector] _GlitchBrightness ("SSU 故障亮度", Float) = 4
        [HideInInspector] _GlitchNoiseScale ("SSU 故障噪声缩放", Vector) = (0,3,0,0)
        [HideInInspector] _GlitchNoiseSpeed ("SSU 故障噪声速度", Vector) = (0,1,0,0)
        [HideInInspector] _GlitchDistortion ("SSU 故障位移", Vector) = (0.1,0,0,0)
        [HideInInspector] _GlitchDistortionScale ("SSU 故障位移缩放", Vector) = (0,3,0,0)
        [HideInInspector] _GlitchDistortionSpeed ("SSU 故障位移速度", Vector) = (0,1,0,0)

        // Status Effects
        [HideInInspector] _SSUStatusContract ("SSU 精确效果合同", Float) = 0
        [Toggle] _EnableFrozen ("启用冰冻", Float) = 0
        [HideInInspector] _FrozenFade ("SSU 冰冻强度", Range(0,1)) = 1
        [HideInInspector] _FrozenTint ("SSU 冰冻色调", Color) = (1.819608,4.611765,5.992157,0)
        [HideInInspector] _FrozenContrast ("SSU 冰冻对比度", Float) = 2
        [HideInInspector] _FrozenSnowColor ("SSU 冰冻雪花颜色", Color) = (1.123529,1.373203,1.498039,0)
        [HideInInspector] _FrozenSnowContrast ("SSU 冰冻雪花对比度", Float) = 1
        [HideInInspector] _FrozenSnowDensity ("SSU 冰冻雪花密度", Range(0,1)) = 0.25
        [HideInInspector] _FrozenSnowScale ("SSU 冰冻雪花缩放", Vector) = (0.1,0.1,0,0)
        [HideInInspector] _FrozenHighlightColor ("SSU 冰冻高光颜色", Color) = (1.797647,4.604501,5.992157,1)
        [HideInInspector] _FrozenHighlightContrast ("SSU 冰冻高光对比度", Float) = 2
        [HideInInspector] _FrozenHighlightDensity ("SSU 冰冻高光密度", Range(0,1)) = 1
        [HideInInspector] _FrozenHighlightSpeed ("SSU 冰冻高光速度", Vector) = (0.1,0.1,0,0)
        [HideInInspector] _FrozenHighlightScale ("SSU 冰冻高光缩放", Vector) = (0.2,0.2,0,0)
        [HideInInspector] _FrozenHighlightDistortion ("SSU 冰冻高光扰动", Vector) = (0.5,0.5,0,0)
        [HideInInspector] _FrozenHighlightDistortionSpeed ("SSU 冰冻高光扰动速度", Vector) = (-0.05,-0.05,0,0)
        [HideInInspector] _FrozenHighlightDistortionScale ("SSU 冰冻高光扰动缩放", Vector) = (0.2,0.2,0,0)
        [HDR] _FrozenColor ("冰冻颜色", Color) = (0.3,0.8,1,1)
        [HDR] _FrozenHighlight ("冰冻高光", Color) = (1,1,1,1)
        _FrozenDensity ("冰冻雪花密度", Range(0,1)) = 0.35
        _FrozenSpeed ("冰冻流动速度", Float) = 0.2
        [Toggle] _EnableBurn ("启用燃烧", Float) = 0
        [HideInInspector] _BurnFade ("SSU 燃烧强度", Range(0,1)) = 1
        [HideInInspector] _BurnPosition ("SSU 燃烧位置", Vector) = (0,5,0,0)
        [HideInInspector] _BurnRadius ("SSU 燃烧半径", Float) = 5
        [HideInInspector] _BurnEdgeNoiseScale ("SSU 燃烧边缘噪声缩放", Vector) = (0.3,0.3,0,0)
        [HideInInspector] _BurnEdgeNoiseFactor ("SSU 燃烧边缘噪声因子", Float) = 0.5
        [HideInInspector] _BurnInsideContrast ("SSU 燃烧内部对比度", Float) = 2
        [HideInInspector] _BurnInsideNoiseColor ("SSU 燃烧内部噪声颜色", Color) = (3084.047,257.0039,0,0)
        [HideInInspector] _BurnInsideNoiseFactor ("SSU 燃烧内部噪声因子", Float) = 0.2
        [HideInInspector] _BurnInsideNoiseScale ("SSU 燃烧内部噪声缩放", Vector) = (0.5,0.5,0,0)
        [HideInInspector] _BurnSwirlFactor ("SSU 燃烧旋涡因子", Float) = 1
        [HideInInspector] _BurnSwirlNoiseScale ("SSU 燃烧旋涡噪声缩放", Vector) = (0.1,0.1,0,0)
        [HDR] _BurnEdgeColor ("燃烧边缘颜色", Color) = (1,0.1,0.01,1)
        [HDR] _BurnInsideColor ("燃烧内部颜色", Color) = (0.2,0.02,0,1)
        _BurnProgress ("燃烧进度", Range(0,1)) = 0
        _BurnWidth ("燃烧边缘宽度", Float) = 0.1
        [Toggle] _EnablePoison ("启用中毒", Float) = 0
        [HideInInspector] _PoisonFade ("SSU 中毒强度", Range(0,1)) = 1
        [HideInInspector] _PoisonRecolorFactor ("SSU 中毒重着色因子", Range(0,1)) = 0.5
        [HideInInspector] _PoisonShiftSpeed ("SSU 中毒条纹速度", Float) = 0.2
        [HideInInspector] _PoisonNoiseBrightness ("SSU 中毒噪声亮度", Float) = 2
        [HideInInspector] _PoisonNoiseScale ("SSU 中毒噪声缩放", Vector) = (0.2,0.2,0,0)
        [HideInInspector] _PoisonNoiseSpeed ("SSU 中毒噪声速度", Vector) = (0,-0.2,0,0)
        [HDR] _PoisonColor ("中毒颜色", Color) = (0.2,1,0.1,1)
        _PoisonDensity ("中毒密度", Float) = 3
        _PoisonSpeed ("中毒速度", Float) = 1

        // Output Control
        [Toggle] _AlphaClip ("启用透明裁剪", Float) = 0
        _Cutoff ("透明裁剪阈值", Range(0,1)) = 0.01
        [Enum(Alpha,0,Additive,1,Premultiplied,2,Multiply,3)] _BlendMode ("混合模式", Float) = 0
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 10
        [HideInInspector] _BlendOp ("Blend Operation", Float) = 0
        [Enum(Basic,0,Standard,1,High,2)] _QualityTier ("效果质量档位", Float) = 2
        [Enum(DynamicFull,0,MaterialOptimized,1)] _ResourceProfile ("资源编译配置", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        BlendOp [_BlendOp]
        Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/InputData2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"
            #include "ESCompositeRecolor.hlsl"
            #include "ESCompositeColorTransform.hlsl"
            #include "ESCompositeFade.hlsl"
            #include "ESCompositeSampling.hlsl"
            #include "ESCompositeGenerated.hlsl"

            // No resource-mask keyword means the dynamic MPB-safe variant. Optimized
            // materials select one mask, where bits are UV=1, Fade=2, Surface=4, Layers=8.
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
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_AlphaTex); SAMPLER(sampler_AlphaTex);
            #if defined(ES_SPRITE_COMPILE_SURFACE_RESOURCES)
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
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
            TEXTURE2D(_FadeMask); SAMPLER(sampler_FadeMask);
            TEXTURE2D(_FadeNoiseTex); SAMPLER(sampler_FadeNoiseTex);
            TEXTURE2D(_CustomFadeFadeMask); SAMPLER(sampler_CustomFadeFadeMask);
            #endif
            #if defined(ES_SPRITE_COMPILE_LAYER_RESOURCES)
            TEXTURE2D(_PaletteTex); SAMPLER(sampler_PaletteTex);
            TEXTURE2D(_TextureLayer1Texture); SAMPLER(sampler_TextureLayer1Texture);
            TEXTURE2D(_TextureLayer2Texture); SAMPLER(sampler_TextureLayer2Texture);
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
                #define _NoiseTex _MainTex
                #define sampler_NoiseTex sampler_MainTex
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
                float _NormalScale;
                float4 _MainTexScaleOffset;
                float _VertexColorStrength;
                float _CoordinateMode;
                float _SpriteUVTransformValid;
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
                float _AnimationMode;
                float _SequenceColumns;
                float _SequenceRows;
                float _SequenceFrame;
                float _SequenceSpeed;
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
                half4 _AddColor;
                float _AddColorFade;
                float _AddColorContrastToggle;
                float _AddColorContrast;
                float _AddColorMaskToggle;
                float4 _AddColorMask_ST;
                float _EnableAddColor;
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
                float _EnableShadow;
                float _ShadowFade;
                float4 _ShadowOffset;
                half4 _ShadowColor;
                float _EnableColorReplace;
                half4 _ReplaceFrom;
                half4 _ReplaceTo;
                float _ReplaceRange;
                float _ReplaceSoftness;
                float _ReplaceContrast;
                float _ReplaceFade;
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
                float _EnableFlow;
                float4 _FlowSpeed;
                float _FlowStrength;
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
                float _EnableDistortion;
                float4 _NoiseScale;
                float4 _NoiseSpeed;
                float _DistortionStrength;
                float4 _DistortionDirection;
                float _EnableFullDistortion;
                float _FullDistortionFade;
                float4 _FullDistortionDistortion;
                float4 _FullDistortionNoiseScale;
                float _EnableHologram;
                float _HologramFade;
                half4 _HologramColor;
                float _HologramContrast;
                float _HologramSpace;
                float4 _HologramDirection;
                float _HologramLineFrequency;
                float _HologramLineGap;
                float _HologramSpeed;
                float _HologramMinAlpha;
                float _HologramDistortionOffset;
                float4 _HologramDistortionDirection;
                float _HologramDistortionSpeed;
                float _HologramDistortionDensity;
                float _HologramDistortionScale;
                float _EnableGlitch;
                float _GlitchIntensity;
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
                float _SSUStatusContract;
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
                float _AlphaClip;
                float _Cutoff;
                float _BlendMode;
                float PixelSnap;
            CBUFFER_END

            // Vertex Contracts
            struct ESAttributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ESVaryings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
                half2 lightingUV : TEXCOORD3;
                half4 vertexColor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct ESNormalsAttributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 tangent : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ESNormalsVaryings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
                half3 tangentWS : TEXCOORD4;
                half3 bitangentWS : TEXCOORD5;
                half4 vertexColor : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _ESUnscaledTime;
            float _ESUnscaledTimeValid;
            float4 _ESCompositeGlobalWind;
            float _ESCompositeGlobalWindValid;
            float _EnableExternalAlpha;
            half4 _RendererColor;

            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            // Time, Color And UV Helpers
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

            half ESInsideSpriteUV(float2 uv)
            {
                float2 inside = step(float2(0, 0), uv) * step(uv, float2(1, 1));
                return (half)(inside.x * inside.y);
            }

            half4 ESSampleMainTexture(float2 uv)
            {
                bool wrapTiledUV = _TilingMode > 0.5 && _AnimationMode < 0.5;
                if (wrapTiledUV) uv = frac(uv);
                float2 atlasUV = ESLocalToAtlasUV(uv);
                half inside = wrapTiledUV ? 1.0h : ESInsideSpriteUV(uv);
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUV) * inside;
                #if defined(ETC1_EXTERNAL_ALPHA)
                half externalAlpha = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, atlasUV).r * inside;
                color.a = lerp(color.a, externalAlpha, _EnableExternalAlpha);
                #endif
                return color;
            }

            float ESRandom(float2 value)
            {
                return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
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
            #include "ESCompositeSSUEffects.hlsl"
            #include "ESCompositeSSUStatusEffects.hlsl"
            #include "ESCompositeSSUStylizedEffects.hlsl"
            #include "ESCompositeSSUFadeStack.hlsl"

            half ESMinOutlineAlpha8(float2 uv, float2 width)
            {
                const float diagonal = 0.705;
                half value = 1.0h;
                value = min(value, ESSampleMainTexture(uv + float2(0, -width.y)).a);
                value = min(value, ESSampleMainTexture(uv + float2(0, width.y)).a);
                value = min(value, ESSampleMainTexture(uv + float2(-width.x, 0)).a);
                value = min(value, ESSampleMainTexture(uv + float2(width.x, 0)).a);
                value = min(value, ESSampleMainTexture(uv + width * diagonal).a);
                value = min(value, ESSampleMainTexture(uv + float2(-width.x, width.y) * diagonal).a);
                value = min(value, ESSampleMainTexture(uv + float2(width.x, -width.y) * diagonal).a);
                value = min(value, ESSampleMainTexture(uv - width * diagonal).a);
                return value;
            }

            half ESMaxOutlineAlpha8(float2 uv, float2 width)
            {
                const float diagonal = 0.705;
                half value = 0.0h;
                value = max(value, ESSampleMainTexture(uv + float2(0, -width.y)).a);
                value = max(value, ESSampleMainTexture(uv + float2(0, width.y)).a);
                value = max(value, ESSampleMainTexture(uv + float2(-width.x, 0)).a);
                value = max(value, ESSampleMainTexture(uv + float2(width.x, 0)).a);
                value = max(value, ESSampleMainTexture(uv + width * diagonal).a);
                value = max(value, ESSampleMainTexture(uv + float2(-width.x, width.y) * diagonal).a);
                value = max(value, ESSampleMainTexture(uv + float2(width.x, -width.y) * diagonal).a);
                value = max(value, ESSampleMainTexture(uv - width * diagonal).a);
                return value;
            }

            half ESMaxOutlineAlpha4(float2 uv, float2 width)
            {
                half value = 0.0h;
                value = max(value, ESSampleMainTexture(uv + float2(0, -width.y)).a);
                value = max(value, ESSampleMainTexture(uv + float2(0, width.y)).a);
                value = max(value, ESSampleMainTexture(uv + float2(-width.x, 0)).a);
                value = max(value, ESSampleMainTexture(uv + float2(width.x, 0)).a);
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

            void ESApplySSUExactOutlines(
                float2 uv,
                float timeValue,
                inout half4 color)
            {
                float2 spritePixels = ESSpritePixelSize();
                if (_EnableInnerOutline > 0.5)
                {
                    float2 distortedUV = ESCompositeSSUOutlineDistortedUV(
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
                    color = ESCompositeApplySSUInnerOutline(
                        color,
                        minimumAlpha,
                        tint,
                        _InnerOutlineFade,
                        _InnerOutlineOutlineOnlyToggle);
                }
                if (_EnableOuterOutline > 0.5)
                {
                    float2 distortedUV = ESCompositeSSUOutlineDistortedUV(
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
                    color = ESCompositeApplySSUOuterOutline(
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
                    color = ESCompositeApplySSUOuterOutline(
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

            float3 ESRgbToHsv(float3 color)
            {
                float4 constants = float4(0, -1.0 / 3.0, 2.0 / 3.0, -1);
                float4 p = lerp(float4(color.bg, constants.wz), float4(color.gb, constants.xy), step(color.b, color.g));
                float4 q = lerp(float4(p.xyw, color.r), float4(color.r, p.yzx), step(p.x, color.r));
                float delta = q.x - min(q.w, q.y);
                return float3(abs(q.z + (q.w - q.y) / (6 * delta + 1e-5)), delta / (q.x + 1e-5), q.x);
            }

            float3 ESHsvToRgb(float3 color)
            {
                float3 p = abs(frac(color.xxx + float3(0, 1.0 / 3.0, 2.0 / 3.0)) * 6 - 3);
                return color.z * lerp(float3(1, 1, 1), saturate(p - 1), color.y);
            }

            float2 ESSequenceUV(float2 uv, float timeValue)
            {
                float columns = max(1, _SequenceColumns);
                float rows = max(1, _SequenceRows);
                float frame = max(0, floor(_SequenceFrame + (_AnimationMode > 0.5 ? timeValue * _SequenceSpeed : 0)));
                float2 cellSize = 1 / float2(columns, rows);
                float2 cell = float2(fmod(frame, columns), rows - 1 - fmod(floor(frame / columns), rows));
                return uv * cellSize + cell * cellSize;
            }

            float2 ESOutlineUV(float2 uv, float2 offset)
            {
                return uv + offset;
            }
            half4 ESBlurSample(float2 uv, half4 center)
            {
                float2 delta = rcp(ESSpritePixelSize()) * (_BlurRadius * 512.0);
                half4 result = center * 0.4h;
                result += ESSampleMainTexture(uv + float2(delta.x, 0)) * 0.15h;
                result += ESSampleMainTexture(uv - float2(delta.x, 0)) * 0.15h;
                result += ESSampleMainTexture(uv + float2(0, delta.y)) * 0.15h;
                result += ESSampleMainTexture(uv - float2(0, delta.y)) * 0.15h;
                return result;
            }

            half4 ESGaussianBlurSample(float2 uv, half4 center)
            {
                float2 delta = rcp(ESSpritePixelSize()) * (_BlurRadius * 512.0);
                half4 axis = ESSampleMainTexture(uv + float2(delta.x, 0));
                axis += ESSampleMainTexture(uv - float2(delta.x, 0));
                axis += ESSampleMainTexture(uv + float2(0, delta.y));
                axis += ESSampleMainTexture(uv - float2(0, delta.y));
                half4 diagonal = ESSampleMainTexture(uv + delta);
                diagonal += ESSampleMainTexture(uv + float2(delta.x, -delta.y));
                diagonal += ESSampleMainTexture(uv + float2(-delta.x, delta.y));
                diagonal += ESSampleMainTexture(uv - delta);
                return ESCompositeGaussian3x3(center, axis, diagonal);
            }

            half4 ESSharpenSample(float2 uv, half4 center)
            {
                float2 delta = rcp(ESSpritePixelSize()) * (_SharpenRadius * 512.0);
                half4 axis = ESSampleMainTexture(uv + float2(delta.x, 0));
                axis += ESSampleMainTexture(uv - float2(delta.x, 0));
                axis += ESSampleMainTexture(uv + float2(0, delta.y));
                axis += ESSampleMainTexture(uv - float2(0, delta.y));
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

            float3 ESApplyPalette(float3 color)
            {
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                float3 mapped = SAMPLE_TEXTURE2D(
                    _PaletteTex,
                    sampler_PaletteTex,
                    float2(saturate(luminance), saturate(_PaletteRow))).rgb;
                return lerp(color, mapped, saturate(_PaletteStrength));
            }

            float3 ESApplyHalftone(float3 color, float2 uv, out float visibility)
            {
                float angle = radians(_HalftoneAngle);
                float2 directionX = float2(cos(angle), -sin(angle));
                float2 directionY = float2(sin(angle), cos(angle));
                float2 rotated = float2(dot(uv, directionX), dot(uv, directionY));
                float2 cell = frac(rotated * max(4, _HalftoneScale)) - 0.5;
                float luminance = saturate(dot(color, float3(0.2126, 0.7152, 0.0722)));
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
                visibility = saturate((1.0 - ssuDistance) / ssuAA);
                visibility = _HalftoneInvert > 0.5 ? 1.0 - visibility : visibility;
                return color * lerp(1.0, 1.0 - ink, saturate(_HalftoneStrength));
            }

            // Vertex Stage
            float4 ESApplyPixelSnap(float4 positionCS)
            {
                if (PixelSnap < 0.5) return positionCS;
                float2 halfScreen = max(_ScreenParams.xy * 0.5, float2(1.0, 1.0));
                float2 normalized = positionCS.xy / max(positionCS.w, 1e-6);
                positionCS.xy = round(normalized * halfScreen) / halfScreen * positionCS.w;
                return positionCS;
            }

            ESVaryings ESVertex(ESAttributes input)
            {
                ESVaryings output = (ESVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float2 localUV = ESAtlasToLocalUV(input.uv);
                float3 positionOS = ESApplyVertexMotion(input.positionOS.xyz, localUV);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionHCS = ESApplyPixelSnap(positionInputs.positionCS);
                output.positionWS = positionInputs.positionWS;
                output.screenPosition = ComputeScreenPos(output.positionHCS);
                output.lightingUV = half2(output.screenPosition.xy / max(output.screenPosition.w, 1e-4));
                output.uv = localUV;
                output.uv = output.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.uv = output.uv * _MainTexScaleOffset.xy + _MainTexScaleOffset.zw;
                output.color = lerp(half4(1, 1, 1, 1), input.color, _VertexColorStrength)
                    * _Color * _RendererColor;
                output.vertexColor = input.color;
                return output;
            }

            ESNormalsVaryings ESNormalsVertex(ESNormalsAttributes input)
            {
                ESNormalsVaryings output = (ESNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float2 localUV = ESAtlasToLocalUV(input.uv);
                float3 positionOS = ESApplyVertexMotion(input.positionOS.xyz, localUV);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionHCS = ESApplyPixelSnap(positionInputs.positionCS);
                output.positionWS = positionInputs.positionWS;
                output.screenPosition = ComputeScreenPos(output.positionHCS);
                output.uv = localUV;
                output.uv = output.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.uv = output.uv * _MainTexScaleOffset.xy + _MainTexScaleOffset.zw;
                output.color = lerp(half4(1, 1, 1, 1), input.color, _VertexColorStrength)
                    * _Color * _RendererColor;
                output.vertexColor = input.color;
                output.normalWS = -GetViewForwardDir();
                float3 tangentOS = input.tangent.xyz;
                if (_EnableWiggle > 0.5)
                    tangentOS.xy = ESRotate2D(tangentOS.xy, ESWiggleAngle(localUV, ESGetTime()));
                output.tangentWS = TransformObjectToWorldDir(tangentOS);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangent.w;
                return output;
            }

            // Fragment Stage
            half4 ESComputeCompositeColor(ESVaryings input, out float2 sampledUV)
            {
                float timeValue = ESGetTime();
                float2 baseUV = input.uv;
                float2 screenUV = input.screenPosition.xy / max(input.screenPosition.w, 1e-4);
                baseUV = ESCompositeResolveTilingUV(
                    baseUV,
                    input.positionWS.xy,
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
                    baseUV = ESCompositeTransformUV(
                        baseUV,
                        _UVPivot.xy,
                        _UVScale.xy,
                        _UVOffset.xy,
                        _UVRotation + _UVRotationSpeed * timeValue);
                if (_EnableUVDistort > 0.5)
                {
                    baseUV = ESCompositeUVDistort(
                        baseUV,
                        _UVDistortFrequency.xy,
                        _UVDistortSpeed.xy,
                        _UVDistortAmount,
                        timeValue);
                    float2 distortNoiseUV = baseUV * _UVDistortFrequency.xy + _UVDistortSpeed.xy * timeValue;
                    half distortNoise = SAMPLE_TEXTURE2D(
                        _UVDistortNoiseTex,
                        sampler_UVDistortNoiseTex,
                        distortNoiseUV).r;
                    half distortMask = 1.0h;
                    if (_UVDistortMaskToggle > 0.5)
                        distortMask = ESCompositeSelectChannel(
                            SAMPLE_TEXTURE2D(_UVDistortMask, sampler_UVDistortMask, baseUV),
                            _UVDistortMaskChannel);
                    baseUV = ESCompositeUVDistortNoise(
                        baseUV,
                        distortNoise,
                        _UVDistortFrom.xy,
                        _UVDistortTo.xy,
                        _UVDistortFade,
                        distortMask);
                }
                float2 uv = _AnimationMode > 0.5 ? ESSequenceUV(baseUV, timeValue) : baseUV;
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
                float2 coordinate = uv;
                if (_CoordinateMode > 0.5 && _CoordinateMode < 1.5)
                    coordinate = input.positionWS.xy;
                else if (_CoordinateMode > 1.5)
                    coordinate = input.screenPosition.xy / max(input.screenPosition.w, 1e-4);

                float2 noiseUV = coordinate * _NoiseScale.xy + _NoiseSpeed.xy * timeValue;
                half noise = 0.5h;
                if (_EnableDistortion > 0.5
                    || (_SSUStatusContract <= 0.5
                        && (_EnableFrozen > 0.5 || _EnableBurn > 0.5 || _EnablePoison > 0.5)))
                {
                    noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                }
                half fadeNoise = 0.5h;
                if ((_FadeMode > 2.5 && _FadeMode < 3.5)
                    || (_FadeMode > 4.5 && _FadeMode < 5.5)
                    || (_FadeMode > 0.5 && _FadeNoiseFactor > 0.0001))
                {
                    float2 fadeNoiseUV = coordinate * _FadeNoiseScale.xy + _FadeNoiseSpeed.xy * timeValue;
                    fadeNoise = SAMPLE_TEXTURE2D(_FadeNoiseTex, sampler_FadeNoiseTex, fadeNoiseUV).r;
                }

                if (_EnableDistortion > 0.5)
                    uv += (noise - 0.5) * _DistortionStrength * _DistortionDirection.xy;
                if (_EnableFullDistortion > 0.5)
                {
                    float fullNoiseX = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        coordinate * _FullDistortionNoiseScale.xy).r);
                    float fullNoiseY = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        (coordinate + 0.321) * _FullDistortionNoiseScale.xy).r);
                    uv += (1.0 - saturate(_FullDistortionFade))
                        * (float2(fullNoiseX, fullNoiseY) - 0.5)
                        * _FullDistortionDistortion.xy;
                }
                uv = ESCompositeApplySSUDirectionalDistortionUV(uv, coordinate);
                if (_EnableFlow > 0.5) uv += _FlowSpeed.xy * timeValue * _FlowStrength;

                float fadeMask = 1.0;
                float fadeVisibility = 1.0;
                float fadeEdge = 0.0;
                if (_FadeMode > 0.5)
                {
                    if (_FadeMode < 1.5 || (_FadeMode > 3.5 && _FadeMode < 5.5))
                        fadeMask = ESCompositeDirectionalFadeMask(coordinate, _FadePosition.xy, _FadeRotation);
                    else if (_FadeMode < 2.5)
                        fadeMask = SAMPLE_TEXTURE2D(_FadeMask, sampler_FadeMask, uv).r;
                    else if (_FadeMode < 5.5)
                        fadeMask = fadeNoise;
                    else
                        fadeMask = ESCompositeSourceFadeMask(coordinate, _FadePosition.xy);

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

                float2 stylizedCoordinate = uv;
                float hologramCoordinate = 0.0;
            #if !defined(_ES_QUALITY_BASIC) && !defined(_ES_QUALITY_STANDARD)
                if (_SSUStatusContract > 0.5)
                {
                    if (_EnableHologram > 0.5)
                    {
                        hologramCoordinate = ESCompositeResolveSSUHologramCoordinate(
                            stylizedCoordinate,
                            input.positionWS);
                        uv = ESCompositeApplySSUHologramUV(
                            uv,
                            hologramCoordinate,
                            _MainTex_TexelSize.z,
                            timeValue);
                    }
                    if (_EnableGlitch > 0.5)
                        uv = ESCompositeApplySSUGlitchUV(uv, stylizedCoordinate, timeValue);
                }
                else if (_EnableGlitch > 0.5)
                {
                    float glitchScanCoordinate = ESCompositeDirectionalCoordinate2D(
                        stylizedCoordinate,
                        _GlitchScanDirection.xy,
                        float2(0.0, 1.0));
                    float glitch = (ESRandom(floor(glitchScanCoordinate * _GlitchSpeed
                        + timeValue * _GlitchSpeed)) - 0.5) * _GlitchIntensity;
                    float2 glitchDirection = _GlitchDistortion.xy;
                    float glitchDirectionLength = length(glitchDirection);
                    glitchDirection = glitchDirectionLength > 0.0001
                        ? glitchDirection / glitchDirectionLength
                        : float2(1.0, 0.0);
                    uv += glitchDirection * glitch;
                }
            #endif
                if (_EnableSmoothPixelArt > 0.5)
                    uv = ESCompositeSmoothPixelUV(uv, ESSpritePixelSize(), _SmoothPixelStrength);
                if (_EnablePixelate > 0.5) uv = ESPixelateUV(uv);

                half4 sampledSource = ESSampleMainTexture(uv);
                half4 processedSource = sampledSource;
            #if !defined(_ES_QUALITY_BASIC) && !defined(_ES_QUALITY_STANDARD)
                if (_EnableSharpen > 0.5 && _SharpenFade > 0.0001 && _SharpenAmount > 0.0001)
                {
                    half4 sharpened = ESSharpenSample(uv, sampledSource);
                    processedSource = lerp(processedSource, sharpened, saturate(_SharpenFade));
                }
                if (_EnableBlur > 0.5 && _BlurIntensity > 0.0001 && _BlurRadius > 0.0001)
                {
                    half4 blurred = _BlurMode > 0.5
                        ? ESGaussianBlurSample(uv, sampledSource)
                        : ESBlurSample(uv, sampledSource);
                    processedSource = lerp(processedSource, blurred, saturate(_BlurIntensity));
                }
            #endif
            #if !defined(_ES_QUALITY_BASIC)
                if (_EnableChromatic > 0.5
                    && _ChromaticIntensity > 0.0001
                    && abs(_ChromaticOffset) > 0.000001)
                {
                    float2 chromaDir = float2(cos(radians(_ChromaticAngle)), sin(radians(_ChromaticAngle)));
                    float2 localCoord = frac(stylizedCoordinate);
                    float edgeFactor = saturate(length(localCoord - 0.5) * 2.0);
                    float amount = _ChromaticOffset * lerp(1.0, edgeFactor, _ChromaticEdgeOnly);
                    half3 chroma = processedSource.rgb;
                    chroma.r = ESSampleMainTexture(uv + chromaDir * amount).r;
                    chroma.b = ESSampleMainTexture(uv - chromaDir * amount).b;
                    processedSource.rgb = lerp(
                        processedSource.rgb,
                        chroma,
                        saturate(_ChromaticIntensity));
                }
            #endif
                half4 source = processedSource * input.color;
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
                    source.a = processedSource.a * _Color.a * _RendererColor.a * customFadeVisibility;
                }
                float alpha = source.a;
                float3 color = source.rgb;
                float ssuFadeVisibility;
                color = ESCompositeApplySSUFadeStackColor(
                    color,
                    coordinate,
                    ssuFadeVisibility);
                alpha *= ssuFadeVisibility;
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
                    half4 smoke = ESCompositeApplySmoke(
                        half4(color, alpha),
                        smokeMask,
                        _SmokeAlpha,
                        _SmokeDarkEdge);
                    color = smoke.rgb;
                    alpha = smoke.a;
                }
                if (_EnableCheckerboard > 0.5)
                    color = ESCompositeApplyCheckerboard(
                        color,
                        input.positionWS.xy,
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
                    color *= flameMask * _FlameBrightness;
                    alpha *= flameMask;
                }

                if (_FadeMode > 0.5)
                {
                    if ((_FadeMode > 3.5 && _FadeMode < 4.5) || _FadeMode > 6.5)
                        color += _DissolveEdgeColor.rgb * fadeEdge * _DissolveEdgeIntensity;
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
                    color += addTint * _AddColorFade;
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
                    color = lerp(color, strongTint, saturate(_StrongTintFade));
                }
                if (_EnableColorReplace > 0.5)
                {
                    float colorDistance = distance(color, _ReplaceFrom.rgb);
                    float replace = 1 - smoothstep(_ReplaceRange, _ReplaceRange + _ReplaceSoftness, colorDistance);
                    float replaceLuminance = ESCompositeSSULuminance(color);
                    half3 replaceTarget = _ReplaceTo.rgb
                        * (half)pow(replaceLuminance, max(_ReplaceContrast, 0.001));
                    color = lerp(color, replaceTarget, saturate(replace * _ReplaceFade));
                }
                if (_EnableRecolorRGB > 0.5)
                {
                    half mask = 1.0h;
                    if (_RecolorRGBMaskToggle > 0.5)
                        mask = ESCompositeSelectChannel(
                            SAMPLE_TEXTURE2D(_RecolorRGBMask, sampler_RecolorRGBMask, uv),
                            _RecolorRGBMaskChannel);
                    half3 recolored = ESCompositeRecolorRGB(
                        color,
                        _RecolorRed.rgb,
                        _RecolorGreen.rgb,
                        _RecolorBlue.rgb);
                    color = lerp(color, recolored, saturate(_RecolorRGBStrength * mask));
                }
                if (_EnableRecolorRGBYCP > 0.5)
                {
                    half mask = 1.0h;
                    if (_RecolorRGBYCPMaskToggle > 0.5)
                        mask = ESCompositeSelectChannel(
                            SAMPLE_TEXTURE2D(_RecolorRGBYCPMask, sampler_RecolorRGBYCPMask, uv),
                            _RecolorRGBYCPMaskChannel);
                    half3 recolored = ESCompositeRecolorRGBYCP(
                        color,
                        _RecolorRGBYCPRed.rgb,
                        _RecolorRGBYCPGreen.rgb,
                        _RecolorRGBYCPBlue.rgb,
                        _RecolorRGBYCPYellow.rgb,
                        _RecolorRGBYCPCyan.rgb,
                        _RecolorRGBYCPPurple.rgb);
                    color = lerp(color, recolored, saturate(_RecolorRGBYCPStrength * mask));
                }
                if (_EnableBrightness > 0.5) color *= _Brightness;
                if (_EnableContrast > 0.5) color = (color - 0.5) * _Contrast + 0.5;
                if (_EnableHue > 0.5)
                {
                    float3 hsv = ESRgbToHsv(color);
                    hsv.x = frac(hsv.x + _Hue);
                    color = ESHsvToRgb(hsv);
                }
                if (_EnableSplitToning > 0.5)
                    color = ESCompositeSplitTone(
                        color,
                        _SplitToneShadows.rgb,
                        _SplitToneHighlights.rgb,
                        _SplitToneBalance,
                        _SplitToneStrength,
                        _SplitToneContrast,
                        _SplitToneShift);
                if (_EnableBlackTint > 0.5)
                    color = ESCompositeApplyBlackTint(color, _BlackTintColor.rgb, _BlackTintPower, _BlackTintFade);
                if (_EnableInkSpread > 0.5)
                {
                    float inkNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        coordinate * _InkSpreadNoiseScale.xy).r);
                    float inkMask = saturate((
                        _InkSpreadDistance
                        - distance(_InkSpreadPosition.xy, coordinate)
                        + inkNoise * _InkSpreadNoiseFactor)
                        / max(_InkSpreadWidth, 0.001));
                    color = ESCompositeApplyInkSpread(
                        color,
                        _InkSpreadColor.rgb,
                        _InkSpreadContrast,
                        _InkSpreadFade,
                        inkMask);
                }
                if (_EnableShiftHue > 0.5)
                    color = ESCompositeApplyShiftHue(color, timeValue, _ShiftHueSpeed);
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
                    color = ESCompositeApplyAddHue(
                        color,
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
                    color = ESCompositeApplySineGlow(
                        color,
                        sineGlowColor,
                        timeValue,
                        _SineGlowContrast,
                        _SineGlowFrequency,
                        _SineGlowMin,
                        _SineGlowMax,
                        _SineGlowFade);
                }
            #if !defined(_ES_QUALITY_BASIC) && !defined(_ES_QUALITY_STANDARD)
                if (_SSUStatusContract > 0.5)
                {
                    half4 exactColor = half4(color, alpha);
                    if (_EnableInnerOutline > 0.5 || _EnableOuterOutline > 0.5 || _EnablePixelOutline > 0.5)
                        ESApplySSUExactOutlines(uv, timeValue, exactColor);
                    if (_EnableHologram > 0.5)
                        exactColor = ESCompositeApplySSUHologramColor(
                            exactColor,
                            hologramCoordinate,
                            timeValue);
                    if (_EnableGlitch > 0.5)
                        exactColor.rgb = ESCompositeApplySSUGlitchColor(
                            exactColor.rgb,
                            stylizedCoordinate,
                            timeValue);
                    color = exactColor.rgb;
                    alpha = exactColor.a;
                }
            #endif
                if (_EnableCamouflage > 0.5)
                {
                    float2 camouflageUV = coordinate;
                    if (_CamouflageAnimationToggle > 0.5)
                    {
                        float distortionNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                            _UberNoiseTexture,
                            sampler_UberNoiseTexture,
                            (coordinate + timeValue * _CamouflageDistortionSpeed.xy)
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
                    color = ESCompositeApplyCamouflage(
                        color,
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
                        (coordinate + timeValue * _MetalNoiseDistortionSpeed.xy)
                            * _MetalNoiseDistortionScale.xy).r);
                    float2 metalNoiseUV = (
                        (metalDistortionNoise - 0.25) * _MetalNoiseDistortion.xy
                        + coordinate
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
                    color = ESCompositeApplyMetal(
                        color,
                        _MetalColor.rgb,
                        _MetalContrast,
                        _MetalHighlightColor.rgb,
                        _MetalHighlightDensity,
                        _MetalHighlightContrast,
                        metalNoise,
                        _MetalFade * metalMask);
                }
                if (_SSUStatusContract > 0.5)
                {
                    float2 exactShineSource = coordinate;
                    if (_ShineSpace > 0.5 && _ShineSpace < 1.5)
                        exactShineSource = uv;
                    else if (_ShineSpace > 1.5)
                        exactShineSource = input.positionWS.xy;
                    float exactShineCoordinate = ESCompositeShineCoordinate2D(
                        exactShineSource,
                        _ShineDirection.xy,
                        _ShineRotation);
                    color = ESCompositeApplySSUStatusEffects(
                        color,
                        coordinate,
                        uv,
                        exactShineCoordinate,
                        timeValue);
                }
                if (_EnableSaturation > 0.5)
                {
                    float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                    color = lerp(luminance.xxx, color, _Saturation);
                }
                if (_EnableNegative > 0.5) color = lerp(color, 1 - color, _NegativeFade);
                if (_SSUStatusContract <= 0.5 && _EnableRainbow > 0.5)
                {
                    float rainbowCoordinate = ESCompositeDirectionalCoordinate2D(
                        coordinate,
                        _RainbowDirection.xy,
                        float2(0.0, 1.0));
                    float hue = frac(rainbowCoordinate * _RainbowDensity + timeValue * _RainbowSpeed);
                    float3 rainbow = ESHsvToRgb(float3(hue, 1, 1));
                    color = lerp(color, rainbow * _RainbowBrightness, 0.5);
                }
            #if !defined(_ES_QUALITY_BASIC)
                if (_EnablePalette > 0.5) color = ESApplyPalette(color);
                if (_EnableHalftone > 0.5)
                {
                    float halftoneVisibility;
                    color = ESApplyHalftone(color, uv, halftoneVisibility);
                    alpha *= lerp(1.0, halftoneVisibility, step(0.5, _HalftoneAlphaPattern));
                }
                color = ESApplyTextureLayers(color, uv, timeValue);
            #endif

                if (_SSUStatusContract <= 0.5 && _EnableInnerOutline > 0.5)
                {
                    half positive = ESSampleMainTexture(ESOutlineUV(uv, float2(_InnerOutlineWidth, 0))).a;
                    half negative = ESSampleMainTexture(ESOutlineUV(uv, float2(-_InnerOutlineWidth, 0))).a;
                    float edge = saturate(source.a - min(positive, negative));
                    color = lerp(color, _InnerOutlineColor.rgb, edge);
                }
                if (_SSUStatusContract <= 0.5
                    && (_EnableOuterOutline > 0.5 || _EnablePixelOutline > 0.5))
                {
                    float width = _EnablePixelOutline > 0.5 ? _PixelOutlineWidth / 1024 : _OuterOutlineWidth;
                    half around = 0;
                    around = max(around, ESSampleMainTexture(uv + float2(width, 0)).a);
                    around = max(around, ESSampleMainTexture(uv - float2(width, 0)).a);
                    around = max(around, ESSampleMainTexture(uv + float2(0, width)).a);
                    around = max(around, ESSampleMainTexture(uv - float2(0, width)).a);
                    float edge = saturate(around - source.a);
                    float3 outlineColor = _EnablePixelOutline > 0.5 ? _PixelOutlineColor.rgb : _OuterOutlineColor.rgb;
                    color = lerp(color, outlineColor, edge);
                    alpha = max(alpha, around);
                }
                if (_FadeMode > 0.5)
                    alpha *= fadeVisibility;
                if (_SSUStatusContract <= 0.5 && _EnableShine > 0.5)
                {
                    float2 shineSource = coordinate;
                    if (_ShineSpace > 0.5 && _ShineSpace < 1.5)
                        shineSource = uv;
                    else if (_ShineSpace > 1.5)
                        shineSource = input.positionWS.xy;
                    float shineCoordinate = ESCompositeShineCoordinate2D(
                        shineSource,
                        _ShineDirection.xy,
                        _ShineAngle);
                    float shine = 1 - smoothstep(
                        0,
                        _ShineWidth,
                        abs(frac(shineCoordinate + timeValue * _ShineSpeed) - 0.5));
                    color += _ShineColor.rgb * shine * _ShineIntensity;
                }
            #if !defined(_ES_QUALITY_BASIC) && !defined(_ES_QUALITY_STANDARD)
                if (_EnableSparkle > 0.5)
                {
                    float2 sparkleCell = floor(coordinate * max(1.0, _SparkleScale));
                    float sparkleSeed = ESRandom(sparkleCell);
                    float sparkleWave = 0.5 + 0.5 * sin(timeValue * _SparkleSpeed + sparkleSeed * 6.2831853);
                    float2 sparkleLocal = frac(coordinate * max(1.0, _SparkleScale)) - 0.5;
                    float sparkleRadial = saturate(1.0 - length(sparkleLocal) * 2.0);
                    float sparkleCross = max(saturate(1.0 - abs(sparkleLocal.x) * 8.0), saturate(1.0 - abs(sparkleLocal.y) * 8.0));
                    float sparkleShape = saturate(sparkleRadial * 0.35 + sparkleCross * 0.65);
                    float sparkle = step(1.0 - _SparkleDensity, sparkleSeed)
                        * pow(saturate(sparkleWave * sparkleShape), max(1.0, _SparkleSharpness));
                    color += _SparkleColor.rgb * sparkle * _SparkleIntensity;
                }
            #endif
                if (_EnablePingPongGlow > 0.5)
                {
                    float wave = 0.5 + 0.5 * sin(timeValue * _GlowFrequency);
                    float glowLuminance = ESCompositeSSULuminance(color);
                    color += lerp(_GlowFrom.rgb, _GlowTo.rgb, wave)
                        * (half)pow(glowLuminance, max(_GlowContrast, 0.001))
                        * _GlowIntensity
                        * _GlowFade;
                }
            #if !defined(_ES_QUALITY_BASIC) && !defined(_ES_QUALITY_STANDARD)
                if (_SSUStatusContract <= 0.5 && _EnableHologram > 0.5)
                {
                    float2 legacyHologramDirection = _HologramDirection.xy;
                    float legacyHologramDirectionLength = length(legacyHologramDirection);
                    legacyHologramDirection = legacyHologramDirectionLength > 0.0001
                        ? legacyHologramDirection / legacyHologramDirectionLength
                        : float2(0.0, 1.0);
                    float legacyHologramCoordinate = dot(coordinate, legacyHologramDirection);
                    float scanLine = step(
                        _HologramLineGap,
                        frac(legacyHologramCoordinate * _HologramLineFrequency + timeValue * _HologramSpeed));
                    color = lerp(color, _HologramColor.rgb, saturate((half)_HologramFade) * 0.55h);
                    alpha *= lerp(1.0, max(_HologramMinAlpha, scanLine), saturate(_HologramFade));
                }
            #endif
                if (_SSUStatusContract <= 0.5)
                {
                    if (_EnableFrozen > 0.5)
                    {
                        float snow = smoothstep(1 - _FrozenDensity, 1, noise);
                        color = lerp(color, _FrozenColor.rgb, 0.65);
                        color += _FrozenHighlight.rgb * snow * (0.5 + 0.5 * sin(timeValue * _FrozenSpeed + noise * 6));
                    }
                    if (_EnableBurn > 0.5)
                    {
                        float burn = smoothstep(_BurnProgress - _BurnWidth, _BurnProgress + _BurnWidth, noise);
                        color = lerp(_BurnInsideColor.rgb, _BurnEdgeColor.rgb, burn);
                        alpha *= step(_BurnProgress - 0.02, noise);
                    }
                    if (_EnablePoison > 0.5)
                    {
                        float poison = 0.5 + 0.5 * sin(timeValue * _PoisonSpeed + noise * _PoisonDensity * 6);
                        color = lerp(color, _PoisonColor.rgb, saturate(poison * 0.45));
                    }
                }
                if (_EnableEnchanted > 0.5)
                {
                    float2 enchantedScroll = timeValue * _EnchantedSpeed.xy;
                    float enchantedNoiseA = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        (coordinate - (enchantedScroll + float2(1.234, 5.6789)) * float2(0.95, 1.05))
                            * _EnchantedScale.xy).r);
                    float enchantedNoiseB = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        (coordinate + enchantedScroll) * _EnchantedScale.xy).r);
                    color = ESCompositeApplyEnchanted(
                        color,
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
                    color = ESCompositeApplyShifting(
                        color,
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
                if (_EnableAlphaTint > 0.5)
                {
                    float alphaTintWeight = (1.0 - saturate(alpha))
                        * step(_AlphaTintMin, alpha)
                        * saturate(_AlphaTintFade);
                    color = lerp(color, _AlphaTint.rgb, alphaTintWeight);
                }
                half fullGlowVisibility = 1.0h;
                if (_EnableFullGlowDissolve > 0.5)
                {
                    float fullGlowNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
                        _UberNoiseTexture,
                        sampler_UberNoiseTexture,
                        coordinate * _FullGlowDissolveNoiseScale.xy).r);
                    half4 dissolved = ESCompositeApplyFullGlowDissolve(
                        half4(color, alpha),
                        fullGlowNoise,
                        _FullGlowDissolveFade,
                        _FullGlowDissolveWidth,
                        _FullGlowDissolveEdgeColor.rgb,
                        fullGlowVisibility);
                    color = dissolved.rgb;
                    alpha = dissolved.a;
                }
                if (_EnableShadow > 0.5)
                {
                    float2 shadowUVOffset = _ShadowOffset.xy * 100.0 / ESSpritePixelSize();
                    half shadowTintAlpha = _EnableCustomFade > 0.5
                        ? _Color.a * _RendererColor.a
                        : input.color.a;
                    half shadowAlpha = ESSampleMainTexture(uv - shadowUVOffset).a
                        * saturate((half)_ShadowFade)
                        * shadowTintAlpha
                        * saturate((half)fadeVisibility)
                        * saturate((half)customFadeVisibility)
                        * saturate((half)ssuFadeVisibility)
                        * fullGlowVisibility;
                    half4 shadowed = ESCompositeApplySpriteShadow(
                        half4(color, alpha),
                        _ShadowColor.rgb,
                        shadowAlpha);
                    color = shadowed.rgb;
                    alpha = shadowed.a;
                }
                if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
                sampledUV = uv;
                return half4(color, alpha);
            }

            half4 ESForwardFragment(ESVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 sampledUV;
                half4 color = ESComputeCompositeColor(input, sampledUV);
                if (_BlendMode > 1.5 && _BlendMode < 2.5) color.rgb *= color.a;
                return color;
            }

            half4 ES2DLightFragment(ESVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 sampledUV;
                half4 composite = ESComputeCompositeColor(input, sampledUV);
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, sampledUV);
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(composite.rgb, composite.a, mask, surfaceData);
                InitializeInputData(sampledUV, input.lightingUV, inputData);
                half4 color = CombinedShapeLightShared(surfaceData, inputData);
                if (_BlendMode > 1.5 && _BlendMode < 2.5) color.rgb *= color.a;
                return color;
            }

            half4 ESNormalsFragment(ESNormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ESVaryings compositeInput = (ESVaryings)0;
                compositeInput.positionHCS = input.positionHCS;
                compositeInput.uv = input.uv;
                compositeInput.color = input.color;
                compositeInput.vertexColor = input.vertexColor;
                compositeInput.positionWS = input.positionWS;
                compositeInput.screenPosition = input.screenPosition;
                float2 sampledUV;
                half4 composite = ESComputeCompositeColor(compositeInput, sampledUV);
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, sampledUV),
                    _NormalScale);
                return NormalsRenderingShared(
                    composite,
                    normalTS,
                    input.tangentWS,
                    input.bitangentWS,
                    input.normalWS);
            }
        ENDHLSL

        Pass
        {
            Name "ES2DComposite"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ESVertex
            #pragma fragment ES2DLightFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma shader_feature_local _ _ES_QUALITY_BASIC _ES_QUALITY_STANDARD
            #pragma shader_feature_local _ _ES_SPRITE_RESOURCE_MASK_0 _ES_SPRITE_RESOURCE_MASK_1 _ES_SPRITE_RESOURCE_MASK_2 _ES_SPRITE_RESOURCE_MASK_3 _ES_SPRITE_RESOURCE_MASK_4 _ES_SPRITE_RESOURCE_MASK_5 _ES_SPRITE_RESOURCE_MASK_6 _ES_SPRITE_RESOURCE_MASK_7 _ES_SPRITE_RESOURCE_MASK_8 _ES_SPRITE_RESOURCE_MASK_9 _ES_SPRITE_RESOURCE_MASK_10 _ES_SPRITE_RESOURCE_MASK_11 _ES_SPRITE_RESOURCE_MASK_12 _ES_SPRITE_RESOURCE_MASK_13 _ES_SPRITE_RESOURCE_MASK_14 _ES_SPRITE_RESOURCE_MASK_15
            ENDHLSL
        }

        Pass
        {
            Name "NormalsRendering"
            Tags { "LightMode"="NormalsRendering" }
            Blend One Zero
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ESNormalsVertex
            #pragma fragment ESNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #pragma shader_feature_local _ _ES_QUALITY_BASIC _ES_QUALITY_STANDARD
            #pragma shader_feature_local _ _ES_SPRITE_RESOURCE_MASK_0 _ES_SPRITE_RESOURCE_MASK_1 _ES_SPRITE_RESOURCE_MASK_2 _ES_SPRITE_RESOURCE_MASK_3 _ES_SPRITE_RESOURCE_MASK_4 _ES_SPRITE_RESOURCE_MASK_5 _ES_SPRITE_RESOURCE_MASK_6 _ES_SPRITE_RESOURCE_MASK_7 _ES_SPRITE_RESOURCE_MASK_8 _ES_SPRITE_RESOURCE_MASK_9 _ES_SPRITE_RESOURCE_MASK_10 _ES_SPRITE_RESOURCE_MASK_11 _ES_SPRITE_RESOURCE_MASK_12 _ES_SPRITE_RESOURCE_MASK_13 _ES_SPRITE_RESOURCE_MASK_14 _ES_SPRITE_RESOURCE_MASK_15
            ENDHLSL
        }

        Pass
        {
            Name "ES2DForwardFallback"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ESVertex
            #pragma fragment ESForwardFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #pragma shader_feature_local _ _ES_QUALITY_BASIC _ES_QUALITY_STANDARD
            #pragma shader_feature_local _ _ES_SPRITE_RESOURCE_MASK_0 _ES_SPRITE_RESOURCE_MASK_1 _ES_SPRITE_RESOURCE_MASK_2 _ES_SPRITE_RESOURCE_MASK_3 _ES_SPRITE_RESOURCE_MASK_4 _ES_SPRITE_RESOURCE_MASK_5 _ES_SPRITE_RESOURCE_MASK_6 _ES_SPRITE_RESOURCE_MASK_7 _ES_SPRITE_RESOURCE_MASK_8 _ES_SPRITE_RESOURCE_MASK_9 _ES_SPRITE_RESOURCE_MASK_10 _ES_SPRITE_RESOURCE_MASK_11 _ES_SPRITE_RESOURCE_MASK_12 _ES_SPRITE_RESOURCE_MASK_13 _ES_SPRITE_RESOURCE_MASK_14 _ES_SPRITE_RESOURCE_MASK_15
            ENDHLSL
        }

        Pass
        {
            Name "ScenePickingPass"
            Tags { "LightMode"="Picking" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma editor_sync_compilation
            #pragma vertex ESVertex
            #pragma fragment ES2DScenePickingFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #pragma shader_feature_local _ _ES_QUALITY_BASIC _ES_QUALITY_STANDARD
            #pragma shader_feature_local _ _ES_SPRITE_RESOURCE_MASK_0 _ES_SPRITE_RESOURCE_MASK_1 _ES_SPRITE_RESOURCE_MASK_2 _ES_SPRITE_RESOURCE_MASK_3 _ES_SPRITE_RESOURCE_MASK_4 _ES_SPRITE_RESOURCE_MASK_5 _ES_SPRITE_RESOURCE_MASK_6 _ES_SPRITE_RESOURCE_MASK_7 _ES_SPRITE_RESOURCE_MASK_8 _ES_SPRITE_RESOURCE_MASK_9 _ES_SPRITE_RESOURCE_MASK_10 _ES_SPRITE_RESOURCE_MASK_11 _ES_SPRITE_RESOURCE_MASK_12 _ES_SPRITE_RESOURCE_MASK_13 _ES_SPRITE_RESOURCE_MASK_14 _ES_SPRITE_RESOURCE_MASK_15

            float4 _SelectionID;

            half4 ES2DScenePickingFragment(ESVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 sampledUV;
                half4 color = ESComputeCompositeColor(input, sampledUV);
                if (_AlphaClip > 0.5) clip(color.a - _Cutoff);
                return unity_SelectionID;
            }
            ENDHLSL
        }

        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode"="SceneSelectionPass" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma editor_sync_compilation
            #pragma vertex ESVertex
            #pragma fragment ES2DSceneSelectionFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #pragma shader_feature_local _ _ES_QUALITY_BASIC _ES_QUALITY_STANDARD
            #pragma shader_feature_local _ _ES_SPRITE_RESOURCE_MASK_0 _ES_SPRITE_RESOURCE_MASK_1 _ES_SPRITE_RESOURCE_MASK_2 _ES_SPRITE_RESOURCE_MASK_3 _ES_SPRITE_RESOURCE_MASK_4 _ES_SPRITE_RESOURCE_MASK_5 _ES_SPRITE_RESOURCE_MASK_6 _ES_SPRITE_RESOURCE_MASK_7 _ES_SPRITE_RESOURCE_MASK_8 _ES_SPRITE_RESOURCE_MASK_9 _ES_SPRITE_RESOURCE_MASK_10 _ES_SPRITE_RESOURCE_MASK_11 _ES_SPRITE_RESOURCE_MASK_12 _ES_SPRITE_RESOURCE_MASK_13 _ES_SPRITE_RESOURCE_MASK_14 _ES_SPRITE_RESOURCE_MASK_15

            int _ObjectId;
            int _PassValue;

            half4 ES2DSceneSelectionFragment(ESVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 sampledUV;
                half4 color = ESComputeCompositeColor(input, sampledUV);
                if (_AlphaClip > 0.5) clip(color.a - _Cutoff);
                return half4(_ObjectId, _PassValue, 1.0, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "ES.EditorInternal.ESCompositeShaderGUI"
}
