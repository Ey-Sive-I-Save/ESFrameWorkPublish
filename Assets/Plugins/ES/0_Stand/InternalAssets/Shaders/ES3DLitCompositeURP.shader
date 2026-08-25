Shader "ES/3D/Lit Composite URP"
{
    Properties
    {
        [HideInInspector] _ESMaterialVersion ("ES Material Version", Float) = 1
        // Base Material
        [MainTexture] _BaseMap ("基础颜色纹理", 2D) = "white" {}
        [MainColor] _BaseColor ("基础颜色", Color) = (1,1,1,1)

        // UV And Time
        _MainTexScaleOffset ("主纹理缩放/偏移", Vector) = (1,1,0,0)
        [Enum(SceneTime,0,UnscaledTime,1,CustomTime,2)] _TimeMode ("时间来源", Float) = 0
        _CustomTime ("自定义时间", Float) = 0
        _TimeScale ("时间倍率", Range(-4,4)) = 1
        [Toggle] _EnableTimeFPS ("启用时间帧率量化", Float) = 0
        _TimeFPS ("时间帧率", Range(0.01,240)) = 5
        [Toggle] _EnableTimeFrequency ("启用周期时间", Float) = 0
        _TimeFrequency ("时间周期频率", Float) = 2
        _TimeRange ("时间周期范围", Float) = 0.5
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
        [NoScaleOffset] _UVDistortNoiseTex ("UV 扰动噪声", 2D) = "gray" {}
        _UVDistortFrom ("UV 扰动起始偏移", Vector) = (-0.02,-0.02,0,0)
        _UVDistortTo ("UV 扰动目标偏移", Vector) = (0.02,0.02,0,0)
        _UVDistortFade ("UV 扰动淡入", Range(0,1)) = 1
        [Toggle] _UVDistortMaskToggle ("使用 UV 扰动遮罩", Float) = 0
        [NoScaleOffset] _UVDistortMask ("UV 扰动遮罩", 2D) = "white" {}
        [Enum(Red,0,Green,1,Blue,2,Alpha,3)] _UVDistortMaskChannel ("UV 扰动遮罩通道", Float) = 3
        [Enum(LocalUV,0,WorldXZ,1,Screen,2)] _TilingMode ("主纹理平铺空间", Float) = 0
        _WorldTilingScale ("世界平铺缩放", Vector) = (1,1,0,0)
        _WorldTilingOffset ("世界平铺偏移", Vector) = (0,0,0,0)
        _WorldTilingPixelsPerUnit ("世界平铺每单位重复数", Range(0.01,64)) = 1
        _ScreenTilingScale ("屏幕平铺缩放", Vector) = (1,1,0,0)
        _ScreenTilingOffset ("屏幕平铺偏移", Vector) = (0,0,0,0)
        _ScreenTilingPixelsPerUnit ("屏幕平铺像素尺寸", Range(1,2048)) = 128

        // Sampling And Generated Effects
        [Toggle] _EnableSmoothPixelArt ("启用平滑像素画", Float) = 0
        _SmoothPixelStrength ("平滑像素画强度", Range(0,1)) = 1
        [Toggle] _EnablePixelate ("启用像素化", Float) = 0
        _PixelateCells ("横向像素格数", Range(2,512)) = 64
        _PixelateStrength ("像素化强度", Range(0,1)) = 1
        [Toggle] _EnableCheckerboard ("启用棋盘格", Float) = 0
        _CheckerboardDarken ("棋盘格暗格保留亮度", Range(0,1)) = 0.5
        _CheckerboardTiling ("棋盘格密度", Range(0.01,64)) = 1
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
        [Toggle] _EnableHalftone ("启用半色调", Float) = 0
        _HalftoneScale ("半色调密度", Range(4,512)) = 96
        _HalftoneAngle ("半色调角度", Range(0,180)) = 45
        _HalftoneStrength ("半色调强度", Range(0,1)) = 0.75
        _HalftonePosition ("半色调中心", Vector) = (0,0,0,0)
        _HalftoneFade ("半色调扩散", Float) = 1
        _HalftoneFadeWidth ("半色调扩散宽度", Float) = 1.5
        [Toggle] _HalftoneInvert ("反转半色调", Float) = 0
        [Toggle] _HalftoneAlphaPattern ("使用 ESNative 透明点阵", Float) = 0
        [Toggle] _EnableSharpen ("启用纹理锐化", Float) = 0
        _SharpenAmount ("锐化强度", Range(0,4)) = 1
        _SharpenRadius ("锐化半径", Range(0,0.02)) = 0.001
        _SharpenThreshold ("锐化阈值", Range(0,0.5)) = 0.02
        _SharpenFade ("锐化强度淡入", Range(0,1)) = 1

        // Layered Surface Effects
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

        // Outline, Shadow And Stylized Output
        [Toggle] _EnableInnerOutline ("启用内描边", Float) = 0
        _InnerOutlineFade ("内描边淡入", Range(0,1)) = 1
        [HDR] _InnerOutlineColor ("内描边颜色", Color) = (1,0.2,0.05,1)
        _InnerOutlineWidth ("内描边宽度", Float) = 0.08
        [Toggle] _InnerOutlineDistortionToggle ("内描边扰动", Float) = 0
        _InnerOutlineDistortionIntensity ("内描边扰动强度", Vector) = (0.01,0.01,0,0)
        _InnerOutlineNoiseScale ("内描边噪声缩放", Vector) = (4,4,0,0)
        _InnerOutlineNoiseSpeed ("内描边噪声速度", Vector) = (0,0.1,0,0)
        [Toggle] _InnerOutlineTextureToggle ("内描边纹理着色", Float) = 0
        _InnerOutlineTintTexture ("内描边纹理", 2D) = "white" {}
        _InnerOutlineTextureSpeed ("内描边纹理速度", Vector) = (0.5,0,0,0)
        [Toggle] _InnerOutlineOutlineOnlyToggle ("仅显示内描边", Float) = 0
        [Toggle] _EnableOuterOutline ("启用外描边", Float) = 0
        _OuterOutlineFade ("外描边淡入", Range(0,1)) = 1
        [HDR] _OuterOutlineColor ("外描边颜色", Color) = (0,0,0,1)
        _OuterOutlineWidth ("外描边宽度", Float) = 0.005
        [Toggle] _OuterOutlineDistortionToggle ("外描边扰动", Float) = 0
        _OuterOutlineDistortionIntensity ("外描边扰动强度", Vector) = (0.01,0.01,0,0)
        _OuterOutlineNoiseScale ("外描边噪声缩放", Vector) = (4,4,0,0)
        _OuterOutlineNoiseSpeed ("外描边噪声速度", Vector) = (0,0.1,0,0)
        [Toggle] _OuterOutlineTextureToggle ("外描边纹理着色", Float) = 0
        _OuterOutlineTintTexture ("外描边纹理", 2D) = "white" {}
        _OuterOutlineTextureSpeed ("外描边纹理速度", Vector) = (0.5,0,0,0)
        [Toggle] _OuterOutlineOutlineOnlyToggle ("仅显示外描边", Float) = 0
        [Toggle] _EnablePixelOutline ("启用像素描边", Float) = 0
        _PixelOutlineFade ("像素描边淡入", Range(0,1)) = 1
        [HDR] _PixelOutlineColor ("像素描边颜色", Color) = (1,1,1,1)
        _PixelOutlineWidth ("像素描边宽度", Float) = 1
        [Toggle] _PixelOutlineTextureToggle ("像素描边纹理着色", Float) = 0
        _PixelOutlineTintTexture ("像素描边纹理", 2D) = "white" {}
        _PixelOutlineTextureSpeed ("像素描边纹理速度", Vector) = (0.5,0,0,0)
        [Toggle] _PixelOutlineOutlineOnlyToggle ("仅显示像素描边", Float) = 0
        [Toggle] _EnableShadow ("启用精灵阴影", Float) = 0
        _ShadowFade ("精灵阴影强度", Range(0,1)) = 1
        _ShadowOffset ("精灵阴影偏移", Vector) = (0.05,-0.05,0,0)
        _ShadowColor ("精灵阴影颜色", Color) = (0,0,0,0)
        [Toggle] _EnableFullGlowDissolve ("启用全局辉光溶解", Float) = 0
        _FullGlowDissolveFade ("全局辉光溶解进度", Range(0,1)) = 0.5
        _FullGlowDissolveWidth ("全局辉光溶解宽度", Float) = 0.5
        [HDR] _FullGlowDissolveEdgeColor ("全局辉光溶解边缘颜色", Color) = (11.98431,0.627451,0.627451,0)
        _FullGlowDissolveNoiseScale ("全局辉光溶解噪声缩放", Vector) = (0.1,0.1,0,0)
        [Toggle] _EnableHologram ("启用全息", Float) = 0
        _HologramFade ("全息淡入", Range(0,1)) = 1
        [HDR] _HologramColor ("全息颜色", Color) = (0.1,0.8,1,1)
        _HologramContrast ("全息对比度", Float) = 1
        [Enum(LocalUV,0,WorldProjection,1)] _HologramSpace ("全息扫描空间", Float) = 1
        _HologramDirection ("全息扫描方向", Vector) = (0,1,0,0)
        _HologramLineFrequency ("全息线频率", Float) = 80
        _HologramLineGap ("全息线间隔", Float) = 0.35
        _HologramSpeed ("全息速度", Float) = 1
        _HologramMinAlpha ("全息最低透明度", Range(0,1)) = 0.2
        _HologramDistortionOffset ("全息扰动偏移", Float) = 0.05
        _HologramDistortionDirection ("全息扰动方向", Vector) = (1,0,0,0)
        _HologramDistortionSpeed ("全息扰动速度", Float) = 2
        _HologramDistortionDensity ("全息扰动密度", Float) = 0.5
        _HologramDistortionScale ("全息扰动缩放", Float) = 10
        [Toggle] _EnableGlitch ("启用故障", Float) = 0
        _GlitchFade ("故障淡入", Range(0,1)) = 1
        _GlitchIntensity ("故障强度", Range(0,0.2)) = 0.03
        _GlitchSpeed ("故障速度", Float) = 3
        _GlitchScanDirection ("故障条带方向", Vector) = (0,1,0,0)
        _GlitchMaskMin ("故障遮罩阈值", Range(0,1)) = 0.4
        _GlitchMaskScale ("故障遮罩缩放", Vector) = (0,0.2,0,0)
        _GlitchMaskSpeed ("故障遮罩速度", Vector) = (0,4,0,0)
        _GlitchHueSpeed ("故障色相速度", Float) = 1
        _GlitchBrightness ("故障亮度", Float) = 2
        _GlitchNoiseScale ("故障噪声缩放", Vector) = (0,3,0,0)
        _GlitchNoiseSpeed ("故障噪声速度", Vector) = (0,1,0,0)
        _GlitchDistortion ("故障位移", Vector) = (0.1,0,0,0)
        _GlitchDistortionScale ("故障位移缩放", Vector) = (0,3,0,0)
        _GlitchDistortionSpeed ("故障位移速度", Vector) = (0,1,0,0)
        [Toggle] _EnableFullDistortion ("启用 ESNative 全局扰动", Float) = 0
        _FullDistortionFade ("全局扰动淡出", Range(0,1)) = 1
        _FullDistortionDistortion ("全局扰动方向强度", Vector) = (0.2,0.2,0,0)
        _FullDistortionNoiseScale ("全局扰动噪声缩放", Vector) = (0.5,0.5,0,0)

        // UV Motion
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
        [Toggle] _EnableVertexAnimation ("启用顶点动画", Float) = 0
        _VertexAnimationDirection ("顶点动画局部方向", Vector) = (0,1,0,0)
        _VertexAnimationAmplitude ("顶点动画幅度", Range(0,2)) = 0.1
        _VertexAnimationFrequency ("顶点动画频率", Range(0,20)) = 2
        _VertexAnimationSpeed ("顶点动画速度", Float) = 1
        [Enum(None,0,Red,1,Green,2,Blue,3,Alpha,4)] _VertexAnimationMask ("顶点色动画遮罩", Float) = 0
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

        // Masks And Fade
        _FadeMode ("渐隐模式", Float) = 0
        _FadeProgress ("渐隐进度", Range(0,1)) = 0
        _FadePosition ("渐隐位置", Vector) = (0.5,0.5,0,0)
        _FadeRotation ("渐隐方向", Range(0,360)) = 0
        _FadeWidth ("渐隐宽度", Range(0.001,1)) = 0.1
        [Toggle] _FadeInvert ("反转渐隐", Float) = 0
        _FadeNoiseFactor ("渐隐噪声影响", Range(0,1)) = 0.2
        _FadeNoiseScale ("渐隐噪声缩放", Vector) = (4,4,0,0)
        _FadeNoiseSpeed ("渐隐噪声速度", Vector) = (0,0,0,0)
        [NoScaleOffset] _FadeNoiseTex ("渐隐噪声", 2D) = "gray" {}
        [NoScaleOffset] _FadeMask ("渐隐遮罩", 2D) = "white" {}
        _FadeDistortionStrength ("渐隐扰动强度", Range(0,0.2)) = 0.03
        _DissolveEdgeIntensity ("溶解边缘强度", Range(0,8)) = 1
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
        // Lighting Inputs
        [Toggle] _UseNormalMap ("启用法线纹理", Float) = 0
        [Normal] _NormalMap ("法线纹理", 2D) = "bump" {}
        _NormalScale ("法线强度", Range(0,2)) = 1
        [Toggle] _UseMetallicMap ("使用金属度纹理", Float) = 0
        _MetallicMap ("金属度/光滑度纹理 (R/A)", 2D) = "white" {}
        [Enum(Red,0,Alpha,1)] _SmoothnessMapChannel ("光滑度纹理通道", Float) = 1
        _Metallic ("金属度", Range(0,1)) = 0
        _Smoothness ("光滑度", Range(0,1)) = 0.5
        [Toggle] _UseOcclusionMap ("使用环境遮挡纹理", Float) = 0
        _OcclusionMap ("环境遮挡纹理", 2D) = "white" {}
        _Occlusion ("环境遮挡强度", Range(0,1)) = 1
        [Toggle] _UseEmission ("启用自发光", Float) = 0
        [HDR] _EmissionColor ("自发光颜色", Color) = (0,0,0,1)
        _EmissionMap ("自发光纹理", 2D) = "black" {}
        [Toggle] _EmissionUseAlpha ("自发光乘纹理 Alpha", Float) = 0
        // Masks And Dissolve
        [Enum(Off,0,Noise,1,Distance,2)] _DissolveMode ("溶解模式", Float) = 0
        [NoScaleOffset] _NoiseTex ("噪声纹理", 2D) = "gray" {}
        _NoiseScale ("噪声缩放", Vector) = (1,1,1,1)
        _NoiseSpeed ("噪声速度", Vector) = (0,0,0,0)
        _DissolveProgress ("溶解进度", Range(0,1)) = 0
        _DissolveSoftness ("溶解柔和度", Range(0.001,1)) = 0.08
        [HDR] _DissolveEdgeColor ("溶解边缘颜色", Color) = (1,0.1,0.01,1)
        _DissolveEdgeWidth ("溶解边缘宽度", Range(0.001,1)) = 0.08

        // ESNative Surface Color And Material Effects
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
        [Toggle] _EnablePingPongGlow ("启用往返发光", Float) = 0
        [HDR] _GlowFrom ("往返发光起点", Color) = (1,0,0,1)
        [HDR] _GlowTo ("往返发光终点", Color) = (0,0.3,1,1)
        _GlowFrequency ("往返发光频率", Float) = 2
        _GlowIntensity ("往返发光强度", Range(0,8)) = 1
        _GlowContrast ("往返发光亮度对比", Range(0.001,8)) = 1
        _GlowFade ("往返发光淡入", Range(0,1)) = 1

        // ESNative Pattern And Status Effects
        [HideInInspector] _ESNativeStatusContract ("ESNative 精确效果合同", Float) = 0
        [NoScaleOffset] _UberNoiseTexture ("ESNative 效果共享噪声", 2D) = "white" {}
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
        [HDR] _FrozenColor ("冰冻颜色", Color) = (0.3,0.8,1,1)
        [HDR] _FrozenHighlight ("冰冻高光", Color) = (1,1,1,1)
        _FrozenDensity ("冰冻雪花密度", Range(0,1)) = 0.35
        _FrozenSpeed ("冰冻流动速度", Float) = 0.2
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
        // Dynamic Effects
        [Toggle] _EnableRim ("启用边缘光", Float) = 0
        [HDR] _RimColor ("边缘光颜色", Color) = (0.1,0.6,1,1)
        _RimPower ("边缘光幂次", Range(0.1,8)) = 3
        _RimIntensity ("边缘光强度", Range(0,8)) = 1
        [Toggle] _EnableShine ("启用扫光", Float) = 0
        [HideInInspector] _ShineFade ("ESNative 扫光强度", Range(0,1)) = 1
        [HideInInspector] _ShineSaturation ("ESNative 扫光饱和度", Range(0,1)) = 0.5
        [HideInInspector] _ShineContrast ("ESNative 扫光对比度", Float) = 2
        [HideInInspector] _ShineRotation ("ESNative 扫光旋转", Range(0,360)) = 30
        [HideInInspector] _ShineSmooth ("ESNative 扫光平滑度", Float) = 1
        [HideInInspector] _ShineFrequency ("ESNative 扫光频率", Float) = 0.3
        [Toggle] _ShineMaskToggle ("ESNative 扫光遮罩", Float) = 0
        [NoScaleOffset] _ShineMask ("ESNative 扫光遮罩纹理", 2D) = "white" {}
        [HDR] _ShineColor ("扫光颜色", Color) = (1,1,1,1)
        _ShineSpeed ("扫光速度", Float) = 1
        _ShineWidth ("扫光宽度", Float) = 0.15
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
        [Toggle] _EnableFlow ("启用纹理流动", Float) = 0
        _FlowSpeed ("流动速度", Vector) = (0,0,0,0)
        _FlowStrength ("流动强度", Range(0,1)) = 1
        [Toggle] _EnableFlowMap ("启用流向贴图", Float) = 0
        [NoScaleOffset] _FlowMap ("流向贴图", 2D) = "gray" {}
        _FlowMapScale ("流向贴图缩放/偏移", Vector) = (1,1,0,0)
        _FlowMapSpeed ("流向贴图速度", Vector) = (0,0,0,0)
        _FlowMapStrength ("流向贴图强度", Range(0,0.2)) = 0.03
        [Toggle] _EnableChromatic ("启用色差", Float) = 0
        _ChromaticOffset ("色差偏移", Range(0,0.02)) = 0.001
        _ChromaticIntensity ("色差强度", Range(0,1)) = 1
        _ChromaticEdgeOnly ("边缘色差", Range(0,1)) = 0.6
        _ChromaticAngle ("色差方向", Range(0,360)) = 0
        [Toggle] _EnableBlur ("启用纹理模糊", Float) = 0
        _BlurRadius ("模糊半径", Range(0,0.02)) = 0.001
        _BlurIntensity ("模糊强度", Range(0,1)) = 0.35
        [Toggle] _EnableBurn ("启用燃烧边缘", Float) = 0
        [HideInInspector] _BurnFade ("ESNative 燃烧强度", Range(0,1)) = 1
        [HideInInspector] _BurnPosition ("ESNative 燃烧位置", Vector) = (0,5,0,0)
        [HideInInspector] _BurnRadius ("ESNative 燃烧半径", Float) = 5
        [HideInInspector] _BurnEdgeNoiseScale ("ESNative 燃烧边缘噪声缩放", Vector) = (0.3,0.3,0,0)
        [HideInInspector] _BurnEdgeNoiseFactor ("ESNative 燃烧边缘噪声因子", Float) = 0.5
        [HideInInspector] _BurnInsideContrast ("ESNative 燃烧内部对比度", Float) = 2
        [HideInInspector] _BurnInsideColor ("ESNative 燃烧内部颜色", Color) = (0.75,0.5625,0.525,0)
        [HideInInspector] _BurnInsideNoiseColor ("ESNative 燃烧内部噪声颜色", Color) = (3084.047,257.0039,0,0)
        [HideInInspector] _BurnInsideNoiseFactor ("ESNative 燃烧内部噪声因子", Float) = 0.2
        [HideInInspector] _BurnInsideNoiseScale ("ESNative 燃烧内部噪声缩放", Vector) = (0.5,0.5,0,0)
        [HideInInspector] _BurnSwirlFactor ("ESNative 燃烧旋涡因子", Float) = 1
        [HideInInspector] _BurnSwirlNoiseScale ("ESNative 燃烧旋涡噪声缩放", Vector) = (0.1,0.1,0,0)
        [HDR] _BurnEdgeColor ("燃烧边缘颜色", Color) = (1,0.05,0,1)
        _BurnProgress ("燃烧进度", Range(0,1)) = 0
        _BurnWidth ("燃烧边缘宽度", Float) = 0.1
        // Output And Quality
        [Enum(Opaque,0,Transparent,1)] _Surface ("表面类型", Float) = 0
        [Toggle] _AlphaClip ("启用透明裁剪", Float) = 0
        _Cutoff ("透明裁剪阈值", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("剔除模式", Float) = 2
        _QueueOffset ("渲染队列偏移", Range(-50,50)) = 0
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite ("Z Write", Float) = 1
        [Toggle] _ReceiveShadows ("接收阴影", Float) = 1
        [Enum(Basic,0,Standard,1,High,2)] _QualityTier ("效果质量档位", Float) = 0
        [Enum(DynamicFull,0,MaterialOptimized,1)] _ResourceProfile ("资源编译配置", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }
        LOD 300
        Pass
        {
            Name "ForwardLit"
            // Forward fallback remains available when the renderer does not request a GBuffer.
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ES3DLitVertex
            #pragma fragment ES3DLitFragment
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma shader_feature_local _ _ES_LIT_RESOURCE_MASK_0 _ES_LIT_RESOURCE_MASK_1 _ES_LIT_RESOURCE_MASK_2 _ES_LIT_RESOURCE_MASK_3 _ES_LIT_RESOURCE_MASK_4 _ES_LIT_RESOURCE_MASK_5 _ES_LIT_RESOURCE_MASK_6 _ES_LIT_RESOURCE_MASK_7 _ES_LIT_RESOURCE_MASK_8 _ES_LIT_RESOURCE_MASK_9 _ES_LIT_RESOURCE_MASK_10 _ES_LIT_RESOURCE_MASK_11 _ES_LIT_RESOURCE_MASK_12 _ES_LIT_RESOURCE_MASK_13 _ES_LIT_RESOURCE_MASK_14 _ES_LIT_RESOURCE_MASK_15
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "ES3DLitCompositeURPCommon.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull [_Cull]
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ES3DShadowVertex
            #pragma fragment ES3DShadowFragment
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma shader_feature_local _ _ES_LIT_RESOURCE_MASK_0 _ES_LIT_RESOURCE_MASK_1 _ES_LIT_RESOURCE_MASK_2 _ES_LIT_RESOURCE_MASK_3 _ES_LIT_RESOURCE_MASK_4 _ES_LIT_RESOURCE_MASK_5 _ES_LIT_RESOURCE_MASK_6 _ES_LIT_RESOURCE_MASK_7 _ES_LIT_RESOURCE_MASK_8 _ES_LIT_RESOURCE_MASK_9 _ES_LIT_RESOURCE_MASK_10 _ES_LIT_RESOURCE_MASK_11 _ES_LIT_RESOURCE_MASK_12 _ES_LIT_RESOURCE_MASK_13 _ES_LIT_RESOURCE_MASK_14 _ES_LIT_RESOURCE_MASK_15
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "ES3DLitCompositeURPCommon.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            Cull [_Cull]
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ES3DDepthVertex
            #pragma fragment ES3DDepthFragment
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma shader_feature_local _ _ES_LIT_RESOURCE_MASK_0 _ES_LIT_RESOURCE_MASK_1 _ES_LIT_RESOURCE_MASK_2 _ES_LIT_RESOURCE_MASK_3 _ES_LIT_RESOURCE_MASK_4 _ES_LIT_RESOURCE_MASK_5 _ES_LIT_RESOURCE_MASK_6 _ES_LIT_RESOURCE_MASK_7 _ES_LIT_RESOURCE_MASK_8 _ES_LIT_RESOURCE_MASK_9 _ES_LIT_RESOURCE_MASK_10 _ES_LIT_RESOURCE_MASK_11 _ES_LIT_RESOURCE_MASK_12 _ES_LIT_RESOURCE_MASK_13 _ES_LIT_RESOURCE_MASK_14 _ES_LIT_RESOURCE_MASK_15
            #pragma multi_compile_instancing
            #include "ES3DLitCompositeURPCommon.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ES3DDepthVertex
            #pragma fragment ES3DDepthNormalsFragment
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma shader_feature_local _ _ES_LIT_RESOURCE_MASK_0 _ES_LIT_RESOURCE_MASK_1 _ES_LIT_RESOURCE_MASK_2 _ES_LIT_RESOURCE_MASK_3 _ES_LIT_RESOURCE_MASK_4 _ES_LIT_RESOURCE_MASK_5 _ES_LIT_RESOURCE_MASK_6 _ES_LIT_RESOURCE_MASK_7 _ES_LIT_RESOURCE_MASK_8 _ES_LIT_RESOURCE_MASK_9 _ES_LIT_RESOURCE_MASK_10 _ES_LIT_RESOURCE_MASK_11 _ES_LIT_RESOURCE_MASK_12 _ES_LIT_RESOURCE_MASK_13 _ES_LIT_RESOURCE_MASK_14 _ES_LIT_RESOURCE_MASK_15
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "ES3DLitCompositeURPCommon.hlsl"
            half4 ES3DDepthNormalsFragment(ES3DDepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 stylizedCoordinate;
                float hologramCoordinate;
                float2 surfaceUV = ESResolveLitSurfaceUV(
                    input.uv,
                    input.positionWS,
                    input.positionCS,
                    stylizedCoordinate,
                    hologramCoordinate);
                float edge;
                half alpha = ESComputeLitAlpha(
                    surfaceUV,
                    stylizedCoordinate,
                    hologramCoordinate,
                    input.positionWS,
                    input.vertexColor,
                    edge);
                if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
                float3 normalWS = input.normalWS;
                if (_UseNormalMap > 0.5)
                {
                    float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                    float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, surfaceUV), _NormalScale);
                    normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS));
                }
                normalWS = NormalizeNormalPerPixel(normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remappedOctNormalWS), 0);
#else
                return half4(normalWS, 0);
#endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "UniversalGBuffer" }
            ZWrite On
            ZTest LEqual
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore
            #pragma vertex ES3DLitVertex
            #pragma fragment ES3DLitGBufferFragment
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma shader_feature_local _ _ES_LIT_RESOURCE_MASK_0 _ES_LIT_RESOURCE_MASK_1 _ES_LIT_RESOURCE_MASK_2 _ES_LIT_RESOURCE_MASK_3 _ES_LIT_RESOURCE_MASK_4 _ES_LIT_RESOURCE_MASK_5 _ES_LIT_RESOURCE_MASK_6 _ES_LIT_RESOURCE_MASK_7 _ES_LIT_RESOURCE_MASK_8 _ES_LIT_RESOURCE_MASK_9 _ES_LIT_RESOURCE_MASK_10 _ES_LIT_RESOURCE_MASK_11 _ES_LIT_RESOURCE_MASK_12 _ES_LIT_RESOURCE_MASK_13 _ES_LIT_RESOURCE_MASK_14 _ES_LIT_RESOURCE_MASK_15
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "ES3DLitCompositeURPCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

            FragmentOutput ES3DLitGBufferFragment(ES3DLitVaryings input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surfaceData;
                float dissolveAlpha;
                float dissolveEdge;
                float2 stylizedCoordinate;
                float hologramCoordinate;
                float2 surfaceUV = ESResolveLitSurfaceUV(
                    input.uv,
                    input.positionWS,
                    input.positionCS,
                    stylizedCoordinate,
                    hologramCoordinate);
                ESInitializeSurface(
                    surfaceUV,
                    stylizedCoordinate,
                    hologramCoordinate,
                    input.positionWS,
                    input.vertexColor,
                    surfaceData,
                    dissolveAlpha,
                    dissolveEdge);
                if (_AlphaClip > 0.5) clip(surfaceData.alpha - _Cutoff);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(surfaceData.normalTS,
                    half3x3(input.tangentWS.xyz, cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w, input.normalWS)));
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_RECEIVE_SHADOWS_OFF)
                inputData.shadowCoord = 0;
#else
                inputData.shadowCoord = input.shadowCoord;
#endif
                inputData.fogCoord = 0;
                inputData.vertexLighting = 0;
#if defined(DYNAMICLIGHTMAP_ON)
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
#else
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
#endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);

#if defined(_ES_QUALITY_STANDARD) || defined(_ES_QUALITY_HIGH)
                if (_DissolveMode > 0.5 || (_FadeMode > 3.5 && _FadeMode < 4.5) || _FadeMode > 6.5)
                    surfaceData.emission += _DissolveEdgeColor.rgb * dissolveEdge * _DissolveEdgeIntensity;
                if (_EnableRim > 0.5)
                    surfaceData.emission += _RimColor.rgb
                        * pow(1.0 - saturate(dot(inputData.normalWS, inputData.viewDirectionWS)), _RimPower)
                        * _RimIntensity;
#endif
#if defined(_ES_QUALITY_HIGH)
                if (_ESNativeStatusContract <= 0.5 && _EnableShine > 0.5)
                {
                    float shineCoordinate = ESResolveLitShineCoordinate(
                        surfaceUV,
                        input.positionWS,
                        90.0,
                        0.0);
                    float shine = 1.0 - smoothstep(0.0, _ShineWidth,
                        abs(frac(shineCoordinate + ESCompositeTime() * _ShineSpeed) - 0.5));
                    surfaceData.emission += _ShineColor.rgb * shine * _ShineIntensity;
                }
                if (_EnableSparkle > 0.5)
                {
                    float2 sparkleCell = floor(surfaceUV * max(1.0, _SparkleScale));
                    float sparkleSeed = frac(sin(dot(sparkleCell, float2(12.9898, 78.233))) * 43758.5453);
                    float sparkleWave = 0.5 + 0.5 * sin(ESCompositeTime() * _SparkleSpeed + sparkleSeed * 6.2831853);
                    float2 sparkleLocal = frac(surfaceUV * max(1.0, _SparkleScale)) - 0.5;
                    float sparkleRadial = saturate(1.0 - length(sparkleLocal) * 2.0);
                    float sparkleCross = max(saturate(1.0 - abs(sparkleLocal.x) * 8.0), saturate(1.0 - abs(sparkleLocal.y) * 8.0));
                    float sparkleShape = saturate(sparkleRadial * 0.35 + sparkleCross * 0.65);
                    float sparkle = step(1.0 - _SparkleDensity, sparkleSeed)
                        * pow(saturate(sparkleWave * sparkleShape), max(1.0, _SparkleSharpness));
                    surfaceData.emission += _SparkleColor.rgb * sparkle * _SparkleIntensity;
                }
#endif

                BRDFData brdfData;
                InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);
                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
                half3 color = GlobalIllumination(brdfData, inputData.bakedGI, surfaceData.occlusion,
                    inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
                return BRDFDataToGbuffer(brdfData, inputData, surfaceData.smoothness,
                    surfaceData.emission + color, surfaceData.occlusion);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ES3DMetaVertex
            #pragma fragment ES3DMetaFragment
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma shader_feature_local _ _ES_LIT_RESOURCE_MASK_0 _ES_LIT_RESOURCE_MASK_1 _ES_LIT_RESOURCE_MASK_2 _ES_LIT_RESOURCE_MASK_3 _ES_LIT_RESOURCE_MASK_4 _ES_LIT_RESOURCE_MASK_5 _ES_LIT_RESOURCE_MASK_6 _ES_LIT_RESOURCE_MASK_7 _ES_LIT_RESOURCE_MASK_8 _ES_LIT_RESOURCE_MASK_9 _ES_LIT_RESOURCE_MASK_10 _ES_LIT_RESOURCE_MASK_11 _ES_LIT_RESOURCE_MASK_12 _ES_LIT_RESOURCE_MASK_13 _ES_LIT_RESOURCE_MASK_14 _ES_LIT_RESOURCE_MASK_15
            #include "ES3DLitCompositeURPCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

            // Meta Pass Contracts
            struct ES3DMetaAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
            };

            struct ES3DMetaVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            ES3DMetaVaryings ES3DMetaVertex(ES3DMetaAttributes input)
            {
                ES3DMetaVaryings output;
                output.positionCS = UnityMetaVertexPosition(
                    input.positionOS.xyz,
                    input.lightmapUV,
                    input.dynamicLightmapUV,
                    unity_LightmapST,
                    unity_DynamicLightmapST);
                output.uv = input.uv;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 ES3DMetaFragment(ES3DMetaVaryings input) : SV_Target
            {
                // Meta represents stable authored material data. Camera-, world- and
                // time-driven composite effects remain runtime-only and are not baked.
                float2 surfaceUV = ESBaseUV(input.uv);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, surfaceUV) * _BaseColor;
#if defined(_ES_QUALITY_HIGH)
                if (_EnableSharpen > 0.5)
                {
                    half4 sharpened = ESLitSharpenSample(
                        surfaceUV,
                        SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, surfaceUV)) * _BaseColor;
                    albedo = lerp(albedo, sharpened, saturate(_SharpenFade));
                }
#endif
                MetaInput meta = (MetaInput)0;
                meta.Albedo = albedo.rgb;
                meta.Emission = _UseEmission > 0.5
                    ? SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, surfaceUV).rgb * _EmissionColor.rgb
                    : 0;
                return UnityMetaFragment(meta);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ScenePickingPass"
            Tags { "LightMode" = "Picking" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles
            #pragma editor_sync_compilation
            #pragma vertex ES3DScenePickingVertex
            #pragma fragment ES3DScenePickingFragment
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma shader_feature_local _ _ES_LIT_RESOURCE_MASK_0 _ES_LIT_RESOURCE_MASK_1 _ES_LIT_RESOURCE_MASK_2 _ES_LIT_RESOURCE_MASK_3 _ES_LIT_RESOURCE_MASK_4 _ES_LIT_RESOURCE_MASK_5 _ES_LIT_RESOURCE_MASK_6 _ES_LIT_RESOURCE_MASK_7 _ES_LIT_RESOURCE_MASK_8 _ES_LIT_RESOURCE_MASK_9 _ES_LIT_RESOURCE_MASK_10 _ES_LIT_RESOURCE_MASK_11 _ES_LIT_RESOURCE_MASK_12 _ES_LIT_RESOURCE_MASK_13 _ES_LIT_RESOURCE_MASK_14 _ES_LIT_RESOURCE_MASK_15
            #pragma multi_compile_instancing
            #define SCENEPICKINGPASS
            #include "ES3DLitCompositeURPCommon.hlsl"

            float4 _SelectionID;

            struct ES3DScenePickingAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ES3DScenePickingVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ES3DScenePickingVaryings ES3DScenePickingVertex(ES3DScenePickingAttributes input)
            {
                ES3DScenePickingVaryings output = (ES3DScenePickingVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionOS = ESApplyVertexAnimation(input.positionOS.xyz, input.color, input.uv);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.positionWS = TransformObjectToWorld(positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 ES3DScenePickingFragment(ES3DScenePickingVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 stylizedCoordinate;
                float hologramCoordinate;
                float2 surfaceUV = ESResolveLitSurfaceUV(
                    input.uv,
                    input.positionWS,
                    input.positionCS,
                    stylizedCoordinate,
                    hologramCoordinate);
                float edge;
                half alpha = ESComputeLitAlpha(
                    surfaceUV,
                    stylizedCoordinate,
                    hologramCoordinate,
                    input.positionWS,
                    input.color,
                    edge);
                if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
                return unity_SelectionID;
            }
            ENDHLSL
        }
        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode" = "SceneSelectionPass" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles
            #pragma editor_sync_compilation
            #pragma vertex ES3DSceneSelectionVertex
            #pragma fragment ES3DSceneSelectionFragment
            #pragma shader_feature_local _ _ES_QUALITY_STANDARD _ES_QUALITY_HIGH
            #pragma shader_feature_local _ _ES_LIT_RESOURCE_MASK_0 _ES_LIT_RESOURCE_MASK_1 _ES_LIT_RESOURCE_MASK_2 _ES_LIT_RESOURCE_MASK_3 _ES_LIT_RESOURCE_MASK_4 _ES_LIT_RESOURCE_MASK_5 _ES_LIT_RESOURCE_MASK_6 _ES_LIT_RESOURCE_MASK_7 _ES_LIT_RESOURCE_MASK_8 _ES_LIT_RESOURCE_MASK_9 _ES_LIT_RESOURCE_MASK_10 _ES_LIT_RESOURCE_MASK_11 _ES_LIT_RESOURCE_MASK_12 _ES_LIT_RESOURCE_MASK_13 _ES_LIT_RESOURCE_MASK_14 _ES_LIT_RESOURCE_MASK_15
            #pragma multi_compile_instancing
            #define SCENESELECTIONPASS
            #include "ES3DLitCompositeURPCommon.hlsl"

            int _ObjectId;
            int _PassValue;

            struct ES3DSceneSelectionAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ES3DSceneSelectionVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ES3DSceneSelectionVaryings ES3DSceneSelectionVertex(ES3DSceneSelectionAttributes input)
            {
                ES3DSceneSelectionVaryings output = (ES3DSceneSelectionVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionOS = ESApplyVertexAnimation(input.positionOS.xyz, input.color, input.uv);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.positionWS = TransformObjectToWorld(positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 ES3DSceneSelectionFragment(ES3DSceneSelectionVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 stylizedCoordinate;
                float hologramCoordinate;
                float2 surfaceUV = ESResolveLitSurfaceUV(
                    input.uv,
                    input.positionWS,
                    input.positionCS,
                    stylizedCoordinate,
                    hologramCoordinate);
                float edge;
                half alpha = ESComputeLitAlpha(
                    surfaceUV,
                    stylizedCoordinate,
                    hologramCoordinate,
                    input.positionWS,
                    input.color,
                    edge);
                if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
                return half4(_ObjectId, _PassValue, 1.0, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "ES.EditorInternal.ESCompositeShaderGUI"
}
