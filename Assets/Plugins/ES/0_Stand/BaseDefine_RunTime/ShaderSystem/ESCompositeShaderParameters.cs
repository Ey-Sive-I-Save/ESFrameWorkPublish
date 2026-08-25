using UnityEngine;
using UnityEngine.Rendering;

namespace ES
{
    /// <summary>
    /// ES/2D/Composite URP 的稳定强枚举参数入口。
    /// 数值必须与 Shader 属性中的 Enum 顺序保持一致；业务层不直接散落字符串。
    /// </summary>
    public enum ES2DCompositeAnimationMode
    {
        无 = 0,
        序列帧 = 1
    }

    public enum ES2DCompositeFadeMode
    {
        无 = 0,
        渐隐 = 1,
        遮罩 = 2,
        溶解 = 3,
        方向发光 = 4,
        方向扰动 = 5,
        源点透明溶解 = 6,
        源点发光溶解 = 7
    }

    public enum ESCompositeFadeMode
    {
        无 = 0,
        方向透明渐隐 = 1,
        纹理遮罩 = 2,
        全纹理透明溶解 = 3,
        方向发光渐隐 = 4,
        方向扰动 = 5,
        源点透明溶解 = 6,
        源点发光溶解 = 7
    }

    public enum ES2DCompositeCoordinateMode
    {
        UV = 0,
        世界空间 = 1,
        屏幕空间 = 2
    }

    public enum ESCompositeTilingMode
    {
        局部UV = 0,
        世界空间 = 1,
        屏幕空间 = 2
    }

    public enum ESCompositeProjectionSpace
    {
        兼容默认 = 0,
        局部UV = 1,
        世界投影 = 2
    }

    public enum ES2DCompositeTimeMode
    {
        场景时间 = 0,
        非缩放时间 = 1,
        自定义时间 = 2
    }

    public enum ESCompositeTimeMode
    {
        场景时间 = 0,
        非缩放时间 = 1,
        自定义时间 = 2
    }

    public enum ESCompositeTextureLayer
    {
        层一 = 1,
        层二 = 2
    }

    public enum ESCompositeTextureChannel
    {
        红色 = 0,
        绿色 = 1,
        蓝色 = 2,
        透明 = 3
    }

    public enum ES2DCompositeBlendMode
    {
        透明混合 = 0,
        叠加 = 1,
        预乘透明 = 2,
        正片叠底 = 3
    }

    public static class ESCompositeURPProperties
    {
        private const string QualityStandardKeyword = "_ES_QUALITY_STANDARD";
        private const string QualityHighKeyword = "_ES_QUALITY_HIGH";
        private const string QualityBasicKeyword = "_ES_QUALITY_BASIC";
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int TimeFPSEnabled = Shader.PropertyToID("_EnableTimeFPS");
        public static readonly int TimeFPS = Shader.PropertyToID("_TimeFPS");
        public static readonly int TimeFrequencyEnabled = Shader.PropertyToID("_EnableTimeFrequency");
        public static readonly int TimeFrequency = Shader.PropertyToID("_TimeFrequency");
        public static readonly int TimeRange = Shader.PropertyToID("_TimeRange");
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");
        public static readonly int SpriteUVRect = Shader.PropertyToID("_SpriteUVRect");
        public static readonly int SpriteUVTransformX = Shader.PropertyToID("_SpriteUVTransformX");
        public static readonly int SpriteUVTransformY = Shader.PropertyToID("_SpriteUVTransformY");
        public static readonly int SpriteUVTransformValid = Shader.PropertyToID("_SpriteUVTransformValid");
        public static readonly int UVTransformEnabled = Shader.PropertyToID("_EnableUVTransform");
        public static readonly int UVPivot = Shader.PropertyToID("_UVPivot");
        public static readonly int UVScale = Shader.PropertyToID("_UVScale");
        public static readonly int UVOffset = Shader.PropertyToID("_UVOffset");
        public static readonly int UVRotation = Shader.PropertyToID("_UVRotation");
        public static readonly int UVRotationSpeed = Shader.PropertyToID("_UVRotationSpeed");
        public static readonly int UVDistortEnabled = Shader.PropertyToID("_EnableUVDistort");
        public static readonly int UVDistortFrequency = Shader.PropertyToID("_UVDistortFrequency");
        public static readonly int UVDistortSpeed = Shader.PropertyToID("_UVDistortSpeed");
        public static readonly int UVDistortAmount = Shader.PropertyToID("_UVDistortAmount");
        public static readonly int UVDistortNoiseTexture = Shader.PropertyToID("_UVDistortNoiseTex");
        public static readonly int UVDistortFrom = Shader.PropertyToID("_UVDistortFrom");
        public static readonly int UVDistortTo = Shader.PropertyToID("_UVDistortTo");
        public static readonly int UVDistortFade = Shader.PropertyToID("_UVDistortFade");
        public static readonly int UVDistortMaskEnabled = Shader.PropertyToID("_UVDistortMaskToggle");
        public static readonly int UVDistortMask = Shader.PropertyToID("_UVDistortMask");
        public static readonly int UVDistortMaskChannel = Shader.PropertyToID("_UVDistortMaskChannel");
        public static readonly int FadeMode = Shader.PropertyToID("_FadeMode");
        public static readonly int FadeProgress = Shader.PropertyToID("_FadeProgress");
        public static readonly int FadePosition = Shader.PropertyToID("_FadePosition");
        public static readonly int FadeRotation = Shader.PropertyToID("_FadeRotation");
        public static readonly int FadeWidth = Shader.PropertyToID("_FadeWidth");
        public static readonly int FadeInvert = Shader.PropertyToID("_FadeInvert");
        public static readonly int FadeNoiseFactor = Shader.PropertyToID("_FadeNoiseFactor");
        public static readonly int FadeNoiseScale = Shader.PropertyToID("_FadeNoiseScale");
        public static readonly int FadeNoiseSpeed = Shader.PropertyToID("_FadeNoiseSpeed");
        public static readonly int FadeNoiseTexture = Shader.PropertyToID("_FadeNoiseTex");
        public static readonly int FadeMaskTexture = Shader.PropertyToID("_FadeMask");
        public static readonly int FadeEdgeColor = Shader.PropertyToID("_DissolveEdgeColor");
        public static readonly int FadeEdgeWidth = Shader.PropertyToID("_DissolveEdgeWidth");
        public static readonly int FadeEdgeIntensity = Shader.PropertyToID("_DissolveEdgeIntensity");
        public static readonly int FadeDistortionStrength = Shader.PropertyToID("_FadeDistortionStrength");
        public static readonly int NoiseTexture = Shader.PropertyToID("_NoiseTex");
        public static readonly int NoiseScale = Shader.PropertyToID("_NoiseScale");
        public static readonly int NoiseSpeed = Shader.PropertyToID("_NoiseSpeed");
        public static readonly int LegacyDistortionEnabled = Shader.PropertyToID("_EnableDistortion");
        public static readonly int LegacyDistortionStrength = Shader.PropertyToID("_DistortionStrength");
        public static readonly int DistortionDirection = Shader.PropertyToID("_DistortionDirection");
        public static readonly int AlphaClipEnabled = Shader.PropertyToID("_AlphaClip");
        public static readonly int Cutoff = Shader.PropertyToID("_Cutoff");
        public static readonly int BlurEnabled = Shader.PropertyToID("_EnableBlur");
        public static readonly int BlurRadius = Shader.PropertyToID("_BlurRadius");
        public static readonly int BlurIntensity = Shader.PropertyToID("_BlurIntensity");
        public static readonly int BlurMode = Shader.PropertyToID("_BlurMode");
        public static readonly int SharpenEnabled = Shader.PropertyToID("_EnableSharpen");
        public static readonly int SharpenAmount = Shader.PropertyToID("_SharpenAmount");
        public static readonly int SharpenRadius = Shader.PropertyToID("_SharpenRadius");
        public static readonly int SharpenThreshold = Shader.PropertyToID("_SharpenThreshold");
        public static readonly int SharpenFade = Shader.PropertyToID("_SharpenFade");
        public static readonly int TilingMode = Shader.PropertyToID("_TilingMode");
        public static readonly int WorldTilingScale = Shader.PropertyToID("_WorldTilingScale");
        public static readonly int WorldTilingOffset = Shader.PropertyToID("_WorldTilingOffset");
        public static readonly int WorldTilingPixelsPerUnit = Shader.PropertyToID("_WorldTilingPixelsPerUnit");
        public static readonly int ScreenTilingScale = Shader.PropertyToID("_ScreenTilingScale");
        public static readonly int ScreenTilingOffset = Shader.PropertyToID("_ScreenTilingOffset");
        public static readonly int ScreenTilingPixelsPerUnit = Shader.PropertyToID("_ScreenTilingPixelsPerUnit");
        public static readonly int SmoothPixelArtEnabled = Shader.PropertyToID("_EnableSmoothPixelArt");
        public static readonly int SmoothPixelStrength = Shader.PropertyToID("_SmoothPixelStrength");
        public static readonly int PixelateEnabled = Shader.PropertyToID("_EnablePixelate");
        public static readonly int PixelateCells = Shader.PropertyToID("_PixelateCells");
        public static readonly int PixelateStrength = Shader.PropertyToID("_PixelateStrength");
        public static readonly int CheckerboardEnabled = Shader.PropertyToID("_EnableCheckerboard");
        public static readonly int CheckerboardDarken = Shader.PropertyToID("_CheckerboardDarken");
        public static readonly int CheckerboardTiling = Shader.PropertyToID("_CheckerboardTiling");
        public static readonly int UberNoiseTexture = Shader.PropertyToID("_UberNoiseTexture");
        public static readonly int ESNativeStatusContract = Shader.PropertyToID("_ESNativeStatusContract");
        public static readonly int ESNativeExactContract = ESNativeStatusContract;
        public static readonly int FrozenFade = Shader.PropertyToID("_FrozenFade");
        public static readonly int FrozenTint = Shader.PropertyToID("_FrozenTint");
        public static readonly int FrozenContrast = Shader.PropertyToID("_FrozenContrast");
        public static readonly int FrozenSnowColor = Shader.PropertyToID("_FrozenSnowColor");
        public static readonly int FrozenSnowContrast = Shader.PropertyToID("_FrozenSnowContrast");
        public static readonly int FrozenSnowDensity = Shader.PropertyToID("_FrozenSnowDensity");
        public static readonly int FrozenSnowScale = Shader.PropertyToID("_FrozenSnowScale");
        public static readonly int FrozenHighlightColor = Shader.PropertyToID("_FrozenHighlightColor");
        public static readonly int FrozenHighlightContrast = Shader.PropertyToID("_FrozenHighlightContrast");
        public static readonly int FrozenHighlightDensity = Shader.PropertyToID("_FrozenHighlightDensity");
        public static readonly int FrozenHighlightSpeed = Shader.PropertyToID("_FrozenHighlightSpeed");
        public static readonly int FrozenHighlightScale = Shader.PropertyToID("_FrozenHighlightScale");
        public static readonly int FrozenHighlightDistortion = Shader.PropertyToID("_FrozenHighlightDistortion");
        public static readonly int FrozenHighlightDistortionSpeed = Shader.PropertyToID("_FrozenHighlightDistortionSpeed");
        public static readonly int FrozenHighlightDistortionScale = Shader.PropertyToID("_FrozenHighlightDistortionScale");
        public static readonly int BurnFade = Shader.PropertyToID("_BurnFade");
        public static readonly int BurnPosition = Shader.PropertyToID("_BurnPosition");
        public static readonly int BurnRadius = Shader.PropertyToID("_BurnRadius");
        public static readonly int BurnEdgeNoiseScale = Shader.PropertyToID("_BurnEdgeNoiseScale");
        public static readonly int BurnEdgeNoiseFactor = Shader.PropertyToID("_BurnEdgeNoiseFactor");
        public static readonly int BurnInsideContrast = Shader.PropertyToID("_BurnInsideContrast");
        public static readonly int BurnInsideNoiseColor = Shader.PropertyToID("_BurnInsideNoiseColor");
        public static readonly int BurnInsideNoiseFactor = Shader.PropertyToID("_BurnInsideNoiseFactor");
        public static readonly int BurnInsideNoiseScale = Shader.PropertyToID("_BurnInsideNoiseScale");
        public static readonly int BurnSwirlFactor = Shader.PropertyToID("_BurnSwirlFactor");
        public static readonly int BurnSwirlNoiseScale = Shader.PropertyToID("_BurnSwirlNoiseScale");
        public static readonly int RainbowFade = Shader.PropertyToID("_RainbowFade");
        public static readonly int RainbowSaturation = Shader.PropertyToID("_RainbowSaturation");
        public static readonly int RainbowContrast = Shader.PropertyToID("_RainbowContrast");
        public static readonly int RainbowCenter = Shader.PropertyToID("_RainbowCenter");
        public static readonly int RainbowNoiseScale = Shader.PropertyToID("_RainbowNoiseScale");
        public static readonly int RainbowNoiseFactor = Shader.PropertyToID("_RainbowNoiseFactor");
        public static readonly int ShineFade = Shader.PropertyToID("_ShineFade");
        public static readonly int ShineSaturation = Shader.PropertyToID("_ShineSaturation");
        public static readonly int ShineContrast = Shader.PropertyToID("_ShineContrast");
        public static readonly int ShineRotation = Shader.PropertyToID("_ShineRotation");
        public static readonly int ShineSmooth = Shader.PropertyToID("_ShineSmooth");
        public static readonly int ShineFrequency = Shader.PropertyToID("_ShineFrequency");
        public static readonly int ShineMaskEnabled = Shader.PropertyToID("_ShineMaskToggle");
        public static readonly int ShineMask = Shader.PropertyToID("_ShineMask");
        public static readonly int ShineMaskScaleOffset = Shader.PropertyToID("_ShineMask_ST");
        public static readonly int ShineEnabled = Shader.PropertyToID("_EnableShine");
        public static readonly int ShineColor = Shader.PropertyToID("_ShineColor");
        public static readonly int ShineSpeed = Shader.PropertyToID("_ShineSpeed");
        public static readonly int ShineWidth = Shader.PropertyToID("_ShineWidth");
        public static readonly int ShineSpace = Shader.PropertyToID("_ShineSpace");
        public static readonly int ShineDirection = Shader.PropertyToID("_ShineDirection");
        public static readonly int ShineAngle = Shader.PropertyToID("_ShineAngle");
        public static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
        public static readonly int PoisonFade = Shader.PropertyToID("_PoisonFade");
        public static readonly int PoisonRecolorFactor = Shader.PropertyToID("_PoisonRecolorFactor");
        public static readonly int PoisonShiftSpeed = Shader.PropertyToID("_PoisonShiftSpeed");
        public static readonly int PoisonNoiseBrightness = Shader.PropertyToID("_PoisonNoiseBrightness");
        public static readonly int PoisonNoiseScale = Shader.PropertyToID("_PoisonNoiseScale");
        public static readonly int PoisonNoiseSpeed = Shader.PropertyToID("_PoisonNoiseSpeed");
        public static readonly int FlameEnabled = Shader.PropertyToID("_EnableFlame");
        public static readonly int FlameBrightness = Shader.PropertyToID("_FlameBrightness");
        public static readonly int FlameSmooth = Shader.PropertyToID("_FlameSmooth");
        public static readonly int FlameRadius = Shader.PropertyToID("_FlameRadius");
        public static readonly int FlameCenter = Shader.PropertyToID("_FlameCenter");
        public static readonly int FlameDirection = Shader.PropertyToID("_FlameDirection");
        public static readonly int FlameSpeed = Shader.PropertyToID("_FlameSpeed");
        public static readonly int FlameNoiseFactor = Shader.PropertyToID("_FlameNoiseFactor");
        public static readonly int FlameNoiseHeightFactor = Shader.PropertyToID("_FlameNoiseHeightFactor");
        public static readonly int FlameNoiseScale = Shader.PropertyToID("_FlameNoiseScale");
        public static readonly int SmokeEnabled = Shader.PropertyToID("_EnableSmoke");
        public static readonly int SmokeAlpha = Shader.PropertyToID("_SmokeAlpha");
        public static readonly int SmokeSmoothness = Shader.PropertyToID("_SmokeSmoothness");
        public static readonly int SmokeNoiseScale = Shader.PropertyToID("_SmokeNoiseScale");
        public static readonly int SmokeSpeed = Shader.PropertyToID("_SmokeSpeed");
        public static readonly int SmokeNoiseFactor = Shader.PropertyToID("_SmokeNoiseFactor");
        public static readonly int SmokeDarkEdge = Shader.PropertyToID("_SmokeDarkEdge");
        public static readonly int SmokeVertexSeed = Shader.PropertyToID("_SmokeVertexSeed");
        public static readonly int HalftoneEnabled = Shader.PropertyToID("_EnableHalftone");
        public static readonly int HalftoneScale = Shader.PropertyToID("_HalftoneScale");
        public static readonly int HalftoneAngle = Shader.PropertyToID("_HalftoneAngle");
        public static readonly int HalftoneStrength = Shader.PropertyToID("_HalftoneStrength");
        public static readonly int HalftonePosition = Shader.PropertyToID("_HalftonePosition");
        public static readonly int HalftoneFade = Shader.PropertyToID("_HalftoneFade");
        public static readonly int HalftoneFadeWidth = Shader.PropertyToID("_HalftoneFadeWidth");
        public static readonly int HalftoneInvert = Shader.PropertyToID("_HalftoneInvert");
        public static readonly int HalftoneAlphaPattern = Shader.PropertyToID("_HalftoneAlphaPattern");
        public static readonly int UnscaledTime = Shader.PropertyToID("_ESUnscaledTime");
        public static readonly int UnscaledTimeValid = Shader.PropertyToID("_ESUnscaledTimeValid");
        public static readonly int WindEnabled = Shader.PropertyToID("_EnableWind");
        public static readonly int WindDirection = Shader.PropertyToID("_WindDirection");
        public static readonly int WindAmplitude = Shader.PropertyToID("_WindAmplitude");
        public static readonly int WindFrequency = Shader.PropertyToID("_WindFrequency");
        public static readonly int WindSpeed = Shader.PropertyToID("_WindSpeed");
        public static readonly int WindAnchor = Shader.PropertyToID("_WindAnchor");
        public static readonly int WindAnchorDirection = Shader.PropertyToID("_WindAnchorDirection");
        public static readonly int WindGlobalInfluence = Shader.PropertyToID("_WindGlobalInfluence");
        public static readonly int SquishEnabled = Shader.PropertyToID("_EnableSquish");
        public static readonly int SquishAmount = Shader.PropertyToID("_SquishAmount");
        public static readonly int SquishDirection = Shader.PropertyToID("_SquishDirection");
        public static readonly int SquishSpeed = Shader.PropertyToID("_SquishSpeed");
        public static readonly int SquishFade = Shader.PropertyToID("_SquishFade");
        public static readonly int InteractiveWindRotation = Shader.PropertyToID("_ESInteractiveWindRotation");
        public static readonly int InteractiveWindHeight = Shader.PropertyToID("_ESInteractiveWindHeight");
        public static readonly int InteractiveSquish = Shader.PropertyToID("_ESInteractiveSquish");
        public static readonly int WindPhaseOffset = Shader.PropertyToID("_ESWindPhaseOffset");
        public static readonly int WiggleEnabled = Shader.PropertyToID("_EnableWiggle");
        public static readonly int WiggleAmplitude = Shader.PropertyToID("_WiggleAmplitude");
        public static readonly int WiggleFrequency = Shader.PropertyToID("_WiggleFrequency");
        public static readonly int WiggleDirection = Shader.PropertyToID("_WiggleDirection");
        public static readonly int WiggleSpeed = Shader.PropertyToID("_WiggleSpeed");
        public static readonly int VibrateEnabled = Shader.PropertyToID("_EnableVibrate");
        public static readonly int VibrateAmplitude = Shader.PropertyToID("_VibrateAmplitude");
        public static readonly int VibrateDirection = Shader.PropertyToID("_VibrateDirection");
        public static readonly int VibrateSpeed = Shader.PropertyToID("_VibrateSpeed");
        public static readonly int GlobalWind = Shader.PropertyToID("_ESCompositeGlobalWind");
        public static readonly int GlobalWindValid = Shader.PropertyToID("_ESCompositeGlobalWindValid");
        public static readonly int TextureLayer1Enabled = Shader.PropertyToID("_EnableTextureLayer1");
        public static readonly int TextureLayer1Fade = Shader.PropertyToID("_TextureLayer1Fade");
        public static readonly int TextureLayer1Texture = Shader.PropertyToID("_TextureLayer1Texture");
        public static readonly int TextureLayer1Color = Shader.PropertyToID("_TextureLayer1Color");
        public static readonly int TextureLayer1Scale = Shader.PropertyToID("_TextureLayer1Scale");
        public static readonly int TextureLayer1Offset = Shader.PropertyToID("_TextureLayer1Offset");
        public static readonly int TextureLayer1ScrollEnabled = Shader.PropertyToID("_TextureLayer1ScrollToggle");
        public static readonly int TextureLayer1ScrollSpeed = Shader.PropertyToID("_TextureLayer1ScrollSpeed");
        public static readonly int TextureLayer1SheetEnabled = Shader.PropertyToID("_TextureLayer1SheetToggle");
        public static readonly int TextureLayer1Columns = Shader.PropertyToID("_TextureLayer1Columns");
        public static readonly int TextureLayer1Rows = Shader.PropertyToID("_TextureLayer1Rows");
        public static readonly int TextureLayer1Speed = Shader.PropertyToID("_TextureLayer1Speed");
        public static readonly int TextureLayer1StartFrame = Shader.PropertyToID("_TextureLayer1StartFrame");
        public static readonly int TextureLayer1EdgeClip = Shader.PropertyToID("_TextureLayer1EdgeClip");
        public static readonly int TextureLayer1ContrastEnabled = Shader.PropertyToID("_TextureLayer1ContrastToggle");
        public static readonly int TextureLayer1Contrast = Shader.PropertyToID("_TextureLayer1Contrast");
        public static readonly int TextureLayer2Enabled = Shader.PropertyToID("_EnableTextureLayer2");
        public static readonly int TextureLayer2Fade = Shader.PropertyToID("_TextureLayer2Fade");
        public static readonly int TextureLayer2Texture = Shader.PropertyToID("_TextureLayer2Texture");
        public static readonly int TextureLayer2Color = Shader.PropertyToID("_TextureLayer2Color");
        public static readonly int TextureLayer2Scale = Shader.PropertyToID("_TextureLayer2Scale");
        public static readonly int TextureLayer2Offset = Shader.PropertyToID("_TextureLayer2Offset");
        public static readonly int TextureLayer2ScrollEnabled = Shader.PropertyToID("_TextureLayer2ScrollToggle");
        public static readonly int TextureLayer2ScrollSpeed = Shader.PropertyToID("_TextureLayer2ScrollSpeed");
        public static readonly int TextureLayer2SheetEnabled = Shader.PropertyToID("_TextureLayer2SheetToggle");
        public static readonly int TextureLayer2Columns = Shader.PropertyToID("_TextureLayer2Columns");
        public static readonly int TextureLayer2Rows = Shader.PropertyToID("_TextureLayer2Rows");
        public static readonly int TextureLayer2Speed = Shader.PropertyToID("_TextureLayer2Speed");
        public static readonly int TextureLayer2StartFrame = Shader.PropertyToID("_TextureLayer2StartFrame");
        public static readonly int TextureLayer2EdgeClip = Shader.PropertyToID("_TextureLayer2EdgeClip");
        public static readonly int TextureLayer2ContrastEnabled = Shader.PropertyToID("_TextureLayer2ContrastToggle");
        public static readonly int TextureLayer2Contrast = Shader.PropertyToID("_TextureLayer2Contrast");
        public static readonly int RecolorRGBEnabled = Shader.PropertyToID("_EnableRecolorRGB");
        public static readonly int RecolorRed = Shader.PropertyToID("_RecolorRed");
        public static readonly int RecolorGreen = Shader.PropertyToID("_RecolorGreen");
        public static readonly int RecolorBlue = Shader.PropertyToID("_RecolorBlue");
        public static readonly int RecolorRGBStrength = Shader.PropertyToID("_RecolorRGBStrength");
        public static readonly int RecolorRGBMaskEnabled = Shader.PropertyToID("_RecolorRGBMaskToggle");
        public static readonly int RecolorRGBMask = Shader.PropertyToID("_RecolorRGBMask");
        public static readonly int RecolorRGBMaskChannel = Shader.PropertyToID("_RecolorRGBMaskChannel");
        public static readonly int RecolorRGBYCPEnabled = Shader.PropertyToID("_EnableRecolorRGBYCP");
        public static readonly int RecolorRGBYCPRed = Shader.PropertyToID("_RecolorRGBYCPRed");
        public static readonly int RecolorRGBYCPGreen = Shader.PropertyToID("_RecolorRGBYCPGreen");
        public static readonly int RecolorRGBYCPBlue = Shader.PropertyToID("_RecolorRGBYCPBlue");
        public static readonly int RecolorRGBYCPYellow = Shader.PropertyToID("_RecolorRGBYCPYellow");
        public static readonly int RecolorRGBYCPCyan = Shader.PropertyToID("_RecolorRGBYCPCyan");
        public static readonly int RecolorRGBYCPPurple = Shader.PropertyToID("_RecolorRGBYCPPurple");
        public static readonly int RecolorRGBYCPStrength = Shader.PropertyToID("_RecolorRGBYCPStrength");
        public static readonly int RecolorRGBYCPMaskEnabled = Shader.PropertyToID("_RecolorRGBYCPMaskToggle");
        public static readonly int RecolorRGBYCPMask = Shader.PropertyToID("_RecolorRGBYCPMask");
        public static readonly int RecolorRGBYCPMaskChannel = Shader.PropertyToID("_RecolorRGBYCPMaskChannel");
        public static readonly int AddColorEnabled = Shader.PropertyToID("_EnableAddColor");
        public static readonly int AddColor = Shader.PropertyToID("_AddColor");
        public static readonly int AddColorFade = Shader.PropertyToID("_AddColorFade");
        public static readonly int AddColorContrastEnabled = Shader.PropertyToID("_AddColorContrastToggle");
        public static readonly int AddColorContrast = Shader.PropertyToID("_AddColorContrast");
        public static readonly int AddColorMaskEnabled = Shader.PropertyToID("_AddColorMaskToggle");
        public static readonly int AddColorMask = Shader.PropertyToID("_AddColorMask");
        public static readonly int AddColorMaskScaleOffset = Shader.PropertyToID("_AddColorMask_ST");
        public static readonly int StrongTintEnabled = Shader.PropertyToID("_EnableStrongTint");
        public static readonly int StrongTint = Shader.PropertyToID("_StrongTint");
        public static readonly int StrongTintFade = Shader.PropertyToID("_StrongTintFade");
        public static readonly int StrongTintContrastEnabled = Shader.PropertyToID("_StrongTintContrastToggle");
        public static readonly int StrongTintContrast = Shader.PropertyToID("_StrongTintContrast");
        public static readonly int StrongTintMaskEnabled = Shader.PropertyToID("_StrongTintMaskToggle");
        public static readonly int StrongTintMask = Shader.PropertyToID("_StrongTintMask");
        public static readonly int StrongTintMaskScaleOffset = Shader.PropertyToID("_StrongTintMask_ST");
        public static readonly int AlphaTintEnabled = Shader.PropertyToID("_EnableAlphaTint");
        public static readonly int AlphaTint = Shader.PropertyToID("_AlphaTint");
        public static readonly int AlphaTintMin = Shader.PropertyToID("_AlphaTintMin");
        public static readonly int AlphaTintFade = Shader.PropertyToID("_AlphaTintFade");
        public static readonly int ColorReplaceEnabled = Shader.PropertyToID("_EnableColorReplace");
        public static readonly int ReplaceFrom = Shader.PropertyToID("_ReplaceFrom");
        public static readonly int ReplaceTo = Shader.PropertyToID("_ReplaceTo");
        public static readonly int ReplaceRange = Shader.PropertyToID("_ReplaceRange");
        public static readonly int ReplaceSoftness = Shader.PropertyToID("_ReplaceSoftness");
        public static readonly int ReplaceContrast = Shader.PropertyToID("_ReplaceContrast");
        public static readonly int ReplaceFade = Shader.PropertyToID("_ReplaceFade");
        public static readonly int BrightnessEnabled = Shader.PropertyToID("_EnableBrightness");
        public static readonly int Brightness = Shader.PropertyToID("_Brightness");
        public static readonly int ContrastEnabled = Shader.PropertyToID("_EnableContrast");
        public static readonly int Contrast = Shader.PropertyToID("_Contrast");
        public static readonly int SaturationEnabled = Shader.PropertyToID("_EnableSaturation");
        public static readonly int Saturation = Shader.PropertyToID("_Saturation");
        public static readonly int HueEnabled = Shader.PropertyToID("_EnableHue");
        public static readonly int Hue = Shader.PropertyToID("_Hue");
        public static readonly int SplitToningEnabled = Shader.PropertyToID("_EnableSplitToning");
        public static readonly int SplitToneShadows = Shader.PropertyToID("_SplitToneShadows");
        public static readonly int SplitToneHighlights = Shader.PropertyToID("_SplitToneHighlights");
        public static readonly int SplitToneBalance = Shader.PropertyToID("_SplitToneBalance");
        public static readonly int SplitToneStrength = Shader.PropertyToID("_SplitToneStrength");
        public static readonly int SplitToneContrast = Shader.PropertyToID("_SplitToneContrast");
        public static readonly int SplitToneShift = Shader.PropertyToID("_SplitToneShift");
        public static readonly int SpriteShadowEnabled = Shader.PropertyToID("_EnableShadow");
        public static readonly int SpriteShadowFade = Shader.PropertyToID("_ShadowFade");
        public static readonly int SpriteShadowOffset = Shader.PropertyToID("_ShadowOffset");
        public static readonly int SpriteShadowColor = Shader.PropertyToID("_ShadowColor");
        public static readonly int BlackTintEnabled = Shader.PropertyToID("_EnableBlackTint");
        public static readonly int BlackTintFade = Shader.PropertyToID("_BlackTintFade");
        public static readonly int BlackTintColor = Shader.PropertyToID("_BlackTintColor");
        public static readonly int BlackTintPower = Shader.PropertyToID("_BlackTintPower");
        public static readonly int InkSpreadEnabled = Shader.PropertyToID("_EnableInkSpread");
        public static readonly int InkSpreadFade = Shader.PropertyToID("_InkSpreadFade");
        public static readonly int InkSpreadColor = Shader.PropertyToID("_InkSpreadColor");
        public static readonly int InkSpreadContrast = Shader.PropertyToID("_InkSpreadContrast");
        public static readonly int InkSpreadDistance = Shader.PropertyToID("_InkSpreadDistance");
        public static readonly int InkSpreadPosition = Shader.PropertyToID("_InkSpreadPosition");
        public static readonly int InkSpreadWidth = Shader.PropertyToID("_InkSpreadWidth");
        public static readonly int InkSpreadNoiseScale = Shader.PropertyToID("_InkSpreadNoiseScale");
        public static readonly int InkSpreadNoiseFactor = Shader.PropertyToID("_InkSpreadNoiseFactor");
        public static readonly int ShiftHueEnabled = Shader.PropertyToID("_EnableShiftHue");
        public static readonly int ShiftHueSpeed = Shader.PropertyToID("_ShiftHueSpeed");
        public static readonly int AddHueEnabled = Shader.PropertyToID("_EnableAddHue");
        public static readonly int AddHueFade = Shader.PropertyToID("_AddHueFade");
        public static readonly int AddHueSpeed = Shader.PropertyToID("_AddHueSpeed");
        public static readonly int AddHueBrightness = Shader.PropertyToID("_AddHueBrightness");
        public static readonly int AddHueSaturation = Shader.PropertyToID("_AddHueSaturation");
        public static readonly int AddHueContrast = Shader.PropertyToID("_AddHueContrast");
        public static readonly int AddHueMaskEnabled = Shader.PropertyToID("_AddHueMaskToggle");
        public static readonly int AddHueMask = Shader.PropertyToID("_AddHueMask");
        public static readonly int AddHueMaskScaleOffset = Shader.PropertyToID("_AddHueMask_ST");
        public static readonly int SineGlowEnabled = Shader.PropertyToID("_EnableSineGlow");
        public static readonly int SineGlowFade = Shader.PropertyToID("_SineGlowFade");
        public static readonly int SineGlowColor = Shader.PropertyToID("_SineGlowColor");
        public static readonly int SineGlowContrast = Shader.PropertyToID("_SineGlowContrast");
        public static readonly int SineGlowFrequency = Shader.PropertyToID("_SineGlowFrequency");
        public static readonly int SineGlowMin = Shader.PropertyToID("_SineGlowMin");
        public static readonly int SineGlowMax = Shader.PropertyToID("_SineGlowMax");
        public static readonly int SineGlowMaskEnabled = Shader.PropertyToID("_SineGlowMaskToggle");
        public static readonly int SineGlowMask = Shader.PropertyToID("_SineGlowMask");
        public static readonly int SineGlowMaskScaleOffset = Shader.PropertyToID("_SineGlowMask_ST");
        public static readonly int SqueezeEnabled = Shader.PropertyToID("_EnableSqueeze");
        public static readonly int SqueezeFade = Shader.PropertyToID("_SqueezeFade");
        public static readonly int SqueezeScale = Shader.PropertyToID("_SqueezeScale");
        public static readonly int SqueezePower = Shader.PropertyToID("_SqueezePower");
        public static readonly int SqueezeCenter = Shader.PropertyToID("_SqueezeCenter");
        public static readonly int SineRotateEnabled = Shader.PropertyToID("_EnableSineRotate");
        public static readonly int SineRotateFade = Shader.PropertyToID("_SineRotateFade");
        public static readonly int SineRotateAngle = Shader.PropertyToID("_SineRotateAngle");
        public static readonly int SineRotateFrequency = Shader.PropertyToID("_SineRotateFrequency");
        public static readonly int SineRotatePivot = Shader.PropertyToID("_SineRotatePivot");
        public static readonly int SineMoveEnabled = Shader.PropertyToID("_EnableSineMove");
        public static readonly int SineMoveFade = Shader.PropertyToID("_SineMoveFade");
        public static readonly int SineMoveOffset = Shader.PropertyToID("_SineMoveOffset");
        public static readonly int SineMoveFrequency = Shader.PropertyToID("_SineMoveFrequency");
        public static readonly int SineScaleEnabled = Shader.PropertyToID("_EnableSineScale");
        public static readonly int SineScaleFrequency = Shader.PropertyToID("_SineScaleFrequency");
        public static readonly int SineScaleFactor = Shader.PropertyToID("_SineScaleFactor");
        public static readonly int FullAlphaDissolveEnabled = Shader.PropertyToID("_EnableFullAlphaDissolve");
        public static readonly int FullAlphaDissolveFade = Shader.PropertyToID("_FullAlphaDissolveFade");
        public static readonly int FullAlphaDissolveWidth = Shader.PropertyToID("_FullAlphaDissolveWidth");
        public static readonly int FullAlphaDissolveNoiseScale = Shader.PropertyToID("_FullAlphaDissolveNoiseScale");
        public static readonly int SourceAlphaDissolveEnabled = Shader.PropertyToID("_EnableSourceAlphaDissolve");
        public static readonly int SourceAlphaDissolveFade = Shader.PropertyToID("_SourceAlphaDissolveFade");
        public static readonly int SourceAlphaDissolvePosition = Shader.PropertyToID("_SourceAlphaDissolvePosition");
        public static readonly int SourceAlphaDissolveWidth = Shader.PropertyToID("_SourceAlphaDissolveWidth");
        public static readonly int SourceAlphaDissolveNoiseScale = Shader.PropertyToID("_SourceAlphaDissolveNoiseScale");
        public static readonly int SourceAlphaDissolveNoiseFactor = Shader.PropertyToID("_SourceAlphaDissolveNoiseFactor");
        public static readonly int SourceAlphaDissolveInvert = Shader.PropertyToID("_SourceAlphaDissolveInvert");
        public static readonly int SourceGlowDissolveEnabled = Shader.PropertyToID("_EnableSourceGlowDissolve");
        public static readonly int SourceGlowDissolveFade = Shader.PropertyToID("_SourceGlowDissolveFade");
        public static readonly int SourceGlowDissolvePosition = Shader.PropertyToID("_SourceGlowDissolvePosition");
        public static readonly int SourceGlowDissolveWidth = Shader.PropertyToID("_SourceGlowDissolveWidth");
        public static readonly int SourceGlowDissolveEdgeColor = Shader.PropertyToID("_SourceGlowDissolveEdgeColor");
        public static readonly int SourceGlowDissolveNoiseScale = Shader.PropertyToID("_SourceGlowDissolveNoiseScale");
        public static readonly int SourceGlowDissolveNoiseFactor = Shader.PropertyToID("_SourceGlowDissolveNoiseFactor");
        public static readonly int SourceGlowDissolveInvert = Shader.PropertyToID("_SourceGlowDissolveInvert");
        public static readonly int DirectionalAlphaFadeEnabled = Shader.PropertyToID("_EnableDirectionalAlphaFade");
        public static readonly int DirectionalAlphaFadeFade = Shader.PropertyToID("_DirectionalAlphaFadeFade");
        public static readonly int DirectionalAlphaFadeRotation = Shader.PropertyToID("_DirectionalAlphaFadeRotation");
        public static readonly int DirectionalAlphaFadeWidth = Shader.PropertyToID("_DirectionalAlphaFadeWidth");
        public static readonly int DirectionalAlphaFadeNoiseScale = Shader.PropertyToID("_DirectionalAlphaFadeNoiseScale");
        public static readonly int DirectionalAlphaFadeNoiseFactor = Shader.PropertyToID("_DirectionalAlphaFadeNoiseFactor");
        public static readonly int DirectionalAlphaFadeInvert = Shader.PropertyToID("_DirectionalAlphaFadeInvert");
        public static readonly int DirectionalGlowFadeEnabled = Shader.PropertyToID("_EnableDirectionalGlowFade");
        public static readonly int DirectionalGlowFadeFade = Shader.PropertyToID("_DirectionalGlowFadeFade");
        public static readonly int DirectionalGlowFadeRotation = Shader.PropertyToID("_DirectionalGlowFadeRotation");
        public static readonly int DirectionalGlowFadeEdgeColor = Shader.PropertyToID("_DirectionalGlowFadeEdgeColor");
        public static readonly int DirectionalGlowFadeWidth = Shader.PropertyToID("_DirectionalGlowFadeWidth");
        public static readonly int DirectionalGlowFadeNoiseScale = Shader.PropertyToID("_DirectionalGlowFadeNoiseScale");
        public static readonly int DirectionalGlowFadeNoiseFactor = Shader.PropertyToID("_DirectionalGlowFadeNoiseFactor");
        public static readonly int DirectionalGlowFadeInvert = Shader.PropertyToID("_DirectionalGlowFadeInvert");
        public static readonly int DirectionalDistortionEnabled = Shader.PropertyToID("_EnableDirectionalDistortion");
        public static readonly int DirectionalDistortionFade = Shader.PropertyToID("_DirectionalDistortionFade");
        public static readonly int DirectionalDistortionRotation = Shader.PropertyToID("_DirectionalDistortionRotation");
        public static readonly int DirectionalDistortionWidth = Shader.PropertyToID("_DirectionalDistortionWidth");
        public static readonly int DirectionalDistortionNoiseScale = Shader.PropertyToID("_DirectionalDistortionNoiseScale");
        public static readonly int DirectionalDistortionNoiseFactor = Shader.PropertyToID("_DirectionalDistortionNoiseFactor");
        public static readonly int DirectionalDistortionAmount = Shader.PropertyToID("_DirectionalDistortionDistortion");
        public static readonly int DirectionalDistortionRandomDirection = Shader.PropertyToID("_DirectionalDistortionRandomDirection");
        public static readonly int DirectionalDistortionScale = Shader.PropertyToID("_DirectionalDistortionDistortionScale");
        public static readonly int DirectionalDistortionInvert = Shader.PropertyToID("_DirectionalDistortionInvert");
        public static readonly int CustomFadeEnabled = Shader.PropertyToID("_EnableCustomFade");
        public static readonly int CustomFadeMask = Shader.PropertyToID("_CustomFadeFadeMask");
        public static readonly int CustomFadeSmoothness = Shader.PropertyToID("_CustomFadeSmoothness");
        public static readonly int CustomFadeNoiseScale = Shader.PropertyToID("_CustomFadeNoiseScale");
        public static readonly int CustomFadeNoiseFactor = Shader.PropertyToID("_CustomFadeNoiseFactor");
        public static readonly int CustomFadeAlpha = Shader.PropertyToID("_CustomFadeAlpha");
        public static readonly int FullGlowDissolveEnabled = Shader.PropertyToID("_EnableFullGlowDissolve");
        public static readonly int FullGlowDissolveFade = Shader.PropertyToID("_FullGlowDissolveFade");
        public static readonly int FullGlowDissolveWidth = Shader.PropertyToID("_FullGlowDissolveWidth");
        public static readonly int FullGlowDissolveEdgeColor = Shader.PropertyToID("_FullGlowDissolveEdgeColor");
        public static readonly int FullGlowDissolveNoiseScale = Shader.PropertyToID("_FullGlowDissolveNoiseScale");
        public static readonly int FullDistortionEnabled = Shader.PropertyToID("_EnableFullDistortion");
        public static readonly int FullDistortionFade = Shader.PropertyToID("_FullDistortionFade");
        public static readonly int FullDistortionAmount = Shader.PropertyToID("_FullDistortionDistortion");
        public static readonly int FullDistortionNoiseScale = Shader.PropertyToID("_FullDistortionNoiseScale");
        public static readonly int CamouflageEnabled = Shader.PropertyToID("_EnableCamouflage");
        public static readonly int CamouflageFade = Shader.PropertyToID("_CamouflageFade");
        public static readonly int CamouflageBaseColor = Shader.PropertyToID("_CamouflageBaseColor");
        public static readonly int CamouflageContrast = Shader.PropertyToID("_CamouflageContrast");
        public static readonly int CamouflageColorA = Shader.PropertyToID("_CamouflageColorA");
        public static readonly int CamouflageDensityA = Shader.PropertyToID("_CamouflageDensityA");
        public static readonly int CamouflageSmoothnessA = Shader.PropertyToID("_CamouflageSmoothnessA");
        public static readonly int CamouflageNoiseScaleA = Shader.PropertyToID("_CamouflageNoiseScaleA");
        public static readonly int CamouflageColorB = Shader.PropertyToID("_CamouflageColorB");
        public static readonly int CamouflageDensityB = Shader.PropertyToID("_CamouflageDensityB");
        public static readonly int CamouflageSmoothnessB = Shader.PropertyToID("_CamouflageSmoothnessB");
        public static readonly int CamouflageNoiseScaleB = Shader.PropertyToID("_CamouflageNoiseScaleB");
        public static readonly int CamouflageAnimationEnabled = Shader.PropertyToID("_CamouflageAnimationToggle");
        public static readonly int CamouflageDistortionSpeed = Shader.PropertyToID("_CamouflageDistortionSpeed");
        public static readonly int CamouflageDistortionIntensity = Shader.PropertyToID("_CamouflageDistortionIntensity");
        public static readonly int CamouflageDistortionScale = Shader.PropertyToID("_CamouflageDistortionScale");
        public static readonly int MetalEnabled = Shader.PropertyToID("_EnableMetal");
        public static readonly int MetalFade = Shader.PropertyToID("_MetalFade");
        public static readonly int MetalColor = Shader.PropertyToID("_MetalColor");
        public static readonly int MetalContrast = Shader.PropertyToID("_MetalContrast");
        public static readonly int MetalHighlightColor = Shader.PropertyToID("_MetalHighlightColor");
        public static readonly int MetalHighlightDensity = Shader.PropertyToID("_MetalHighlightDensity");
        public static readonly int MetalHighlightContrast = Shader.PropertyToID("_MetalHighlightContrast");
        public static readonly int MetalNoiseScale = Shader.PropertyToID("_MetalNoiseScale");
        public static readonly int MetalNoiseSpeed = Shader.PropertyToID("_MetalNoiseSpeed");
        public static readonly int MetalNoiseDistortionScale = Shader.PropertyToID("_MetalNoiseDistortionScale");
        public static readonly int MetalNoiseDistortionSpeed = Shader.PropertyToID("_MetalNoiseDistortionSpeed");
        public static readonly int MetalNoiseDistortion = Shader.PropertyToID("_MetalNoiseDistortion");
        public static readonly int MetalMaskEnabled = Shader.PropertyToID("_MetalMaskToggle");
        public static readonly int MetalMask = Shader.PropertyToID("_MetalMask");
        public static readonly int EnchantedEnabled = Shader.PropertyToID("_EnableEnchanted");
        public static readonly int EnchantedFade = Shader.PropertyToID("_EnchantedFade");
        public static readonly int EnchantedSpeed = Shader.PropertyToID("_EnchantedSpeed");
        public static readonly int EnchantedScale = Shader.PropertyToID("_EnchantedScale");
        public static readonly int EnchantedBrightness = Shader.PropertyToID("_EnchantedBrightness");
        public static readonly int EnchantedContrast = Shader.PropertyToID("_EnchantedContrast");
        public static readonly int EnchantedReduce = Shader.PropertyToID("_EnchantedReduce");
        public static readonly int EnchantedRainbowEnabled = Shader.PropertyToID("_EnchantedRainbowToggle");
        public static readonly int EnchantedRainbowSpeed = Shader.PropertyToID("_EnchantedRainbowSpeed");
        public static readonly int EnchantedRainbowDensity = Shader.PropertyToID("_EnchantedRainbowDensity");
        public static readonly int EnchantedRainbowSaturation = Shader.PropertyToID("_EnchantedRainbowSaturation");
        public static readonly int EnchantedLowColor = Shader.PropertyToID("_EnchantedLowColor");
        public static readonly int EnchantedHighColor = Shader.PropertyToID("_EnchantedHighColor");
        public static readonly int EnchantedLerpEnabled = Shader.PropertyToID("_EnchantedLerpToggle");
        public static readonly int ShiftingEnabled = Shader.PropertyToID("_EnableShifting");
        public static readonly int ShiftingFade = Shader.PropertyToID("_ShiftingFade");
        public static readonly int ShiftingSpeed = Shader.PropertyToID("_ShiftingSpeed");
        public static readonly int ShiftingDensity = Shader.PropertyToID("_ShiftingDensity");
        public static readonly int ShiftingBrightness = Shader.PropertyToID("_ShiftingBrightness");
        public static readonly int ShiftingContrast = Shader.PropertyToID("_ShiftingContrast");
        public static readonly int ShiftingRainbowEnabled = Shader.PropertyToID("_ShiftingRainbowToggle");
        public static readonly int ShiftingSaturation = Shader.PropertyToID("_ShiftingSaturation");
        public static readonly int ShiftingColorA = Shader.PropertyToID("_ShiftingColorA");
        public static readonly int ShiftingColorB = Shader.PropertyToID("_ShiftingColorB");
        public static readonly int NegativeEnabled = Shader.PropertyToID("_EnableNegative");
        public static readonly int NegativeFade = Shader.PropertyToID("_NegativeFade");
        public static readonly int RainbowEnabled = Shader.PropertyToID("_EnableRainbow");
        public static readonly int RainbowSpeed = Shader.PropertyToID("_RainbowSpeed");
        public static readonly int RainbowDensity = Shader.PropertyToID("_RainbowDensity");
        public static readonly int RainbowDirection = Shader.PropertyToID("_RainbowDirection");
        public static readonly int RainbowBrightness = Shader.PropertyToID("_RainbowBrightness");
        public static readonly int InnerOutlineEnabled = Shader.PropertyToID("_EnableInnerOutline");
        public static readonly int InnerOutlineColor = Shader.PropertyToID("_InnerOutlineColor");
        public static readonly int InnerOutlineWidth = Shader.PropertyToID("_InnerOutlineWidth");
        public static readonly int InnerOutlineFade = Shader.PropertyToID("_InnerOutlineFade");
        public static readonly int InnerOutlineDistortionEnabled = Shader.PropertyToID("_InnerOutlineDistortionToggle");
        public static readonly int InnerOutlineDistortionIntensity = Shader.PropertyToID("_InnerOutlineDistortionIntensity");
        public static readonly int InnerOutlineNoiseScale = Shader.PropertyToID("_InnerOutlineNoiseScale");
        public static readonly int InnerOutlineNoiseSpeed = Shader.PropertyToID("_InnerOutlineNoiseSpeed");
        public static readonly int InnerOutlineTextureEnabled = Shader.PropertyToID("_InnerOutlineTextureToggle");
        public static readonly int InnerOutlineTintTexture = Shader.PropertyToID("_InnerOutlineTintTexture");
        public static readonly int InnerOutlineTextureSpeed = Shader.PropertyToID("_InnerOutlineTextureSpeed");
        public static readonly int InnerOutlineOnly = Shader.PropertyToID("_InnerOutlineOutlineOnlyToggle");
        public static readonly int OuterOutlineEnabled = Shader.PropertyToID("_EnableOuterOutline");
        public static readonly int OuterOutlineColor = Shader.PropertyToID("_OuterOutlineColor");
        public static readonly int OuterOutlineWidth = Shader.PropertyToID("_OuterOutlineWidth");
        public static readonly int OuterOutlineFade = Shader.PropertyToID("_OuterOutlineFade");
        public static readonly int OuterOutlineDistortionEnabled = Shader.PropertyToID("_OuterOutlineDistortionToggle");
        public static readonly int OuterOutlineDistortionIntensity = Shader.PropertyToID("_OuterOutlineDistortionIntensity");
        public static readonly int OuterOutlineNoiseScale = Shader.PropertyToID("_OuterOutlineNoiseScale");
        public static readonly int OuterOutlineNoiseSpeed = Shader.PropertyToID("_OuterOutlineNoiseSpeed");
        public static readonly int OuterOutlineTextureEnabled = Shader.PropertyToID("_OuterOutlineTextureToggle");
        public static readonly int OuterOutlineTintTexture = Shader.PropertyToID("_OuterOutlineTintTexture");
        public static readonly int OuterOutlineTextureSpeed = Shader.PropertyToID("_OuterOutlineTextureSpeed");
        public static readonly int OuterOutlineOnly = Shader.PropertyToID("_OuterOutlineOutlineOnlyToggle");
        public static readonly int PixelOutlineEnabled = Shader.PropertyToID("_EnablePixelOutline");
        public static readonly int PixelOutlineColor = Shader.PropertyToID("_PixelOutlineColor");
        public static readonly int PixelOutlineWidth = Shader.PropertyToID("_PixelOutlineWidth");
        public static readonly int PixelOutlineFade = Shader.PropertyToID("_PixelOutlineFade");
        public static readonly int PixelOutlineTextureEnabled = Shader.PropertyToID("_PixelOutlineTextureToggle");
        public static readonly int PixelOutlineTintTexture = Shader.PropertyToID("_PixelOutlineTintTexture");
        public static readonly int PixelOutlineTextureSpeed = Shader.PropertyToID("_PixelOutlineTextureSpeed");
        public static readonly int PixelOutlineOnly = Shader.PropertyToID("_PixelOutlineOutlineOnlyToggle");
        public static readonly int HologramEnabled = Shader.PropertyToID("_EnableHologram");
        public static readonly int HologramColor = Shader.PropertyToID("_HologramColor");
        public static readonly int HologramLineFrequency = Shader.PropertyToID("_HologramLineFrequency");
        public static readonly int HologramLineGap = Shader.PropertyToID("_HologramLineGap");
        public static readonly int HologramSpeed = Shader.PropertyToID("_HologramSpeed");
        public static readonly int HologramMinAlpha = Shader.PropertyToID("_HologramMinAlpha");
        public static readonly int HologramFade = Shader.PropertyToID("_HologramFade");
        public static readonly int HologramContrast = Shader.PropertyToID("_HologramContrast");
        public static readonly int HologramSpace = Shader.PropertyToID("_HologramSpace");
        public static readonly int HologramDirection = Shader.PropertyToID("_HologramDirection");
        public static readonly int HologramDistortionOffset = Shader.PropertyToID("_HologramDistortionOffset");
        public static readonly int HologramDistortionDirection = Shader.PropertyToID("_HologramDistortionDirection");
        public static readonly int HologramDistortionSpeed = Shader.PropertyToID("_HologramDistortionSpeed");
        public static readonly int HologramDistortionDensity = Shader.PropertyToID("_HologramDistortionDensity");
        public static readonly int HologramDistortionScale = Shader.PropertyToID("_HologramDistortionScale");
        public static readonly int GlitchEnabled = Shader.PropertyToID("_EnableGlitch");
        public static readonly int GlitchIntensity = Shader.PropertyToID("_GlitchIntensity");
        public static readonly int GlitchSpeed = Shader.PropertyToID("_GlitchSpeed");
        public static readonly int GlitchScanDirection = Shader.PropertyToID("_GlitchScanDirection");
        public static readonly int GlitchFade = Shader.PropertyToID("_GlitchFade");
        public static readonly int GlitchMaskMin = Shader.PropertyToID("_GlitchMaskMin");
        public static readonly int GlitchMaskScale = Shader.PropertyToID("_GlitchMaskScale");
        public static readonly int GlitchMaskSpeed = Shader.PropertyToID("_GlitchMaskSpeed");
        public static readonly int GlitchHueSpeed = Shader.PropertyToID("_GlitchHueSpeed");
        public static readonly int GlitchBrightness = Shader.PropertyToID("_GlitchBrightness");
        public static readonly int GlitchNoiseScale = Shader.PropertyToID("_GlitchNoiseScale");
        public static readonly int GlitchNoiseSpeed = Shader.PropertyToID("_GlitchNoiseSpeed");
        public static readonly int GlitchDistortion = Shader.PropertyToID("_GlitchDistortion");
        public static readonly int GlitchDistortionScale = Shader.PropertyToID("_GlitchDistortionScale");
        public static readonly int GlitchDistortionSpeed = Shader.PropertyToID("_GlitchDistortionSpeed");
        public static readonly int PingPongGlowEnabled = Shader.PropertyToID("_EnablePingPongGlow");
        public static readonly int GlowFrom = Shader.PropertyToID("_GlowFrom");
        public static readonly int GlowTo = Shader.PropertyToID("_GlowTo");
        public static readonly int GlowFrequency = Shader.PropertyToID("_GlowFrequency");
        public static readonly int GlowIntensity = Shader.PropertyToID("_GlowIntensity");
        public static readonly int GlowContrast = Shader.PropertyToID("_GlowContrast");
        public static readonly int GlowFade = Shader.PropertyToID("_GlowFade");
        public static readonly int FrozenEnabled = Shader.PropertyToID("_EnableFrozen");
        public static readonly int FrozenColor = Shader.PropertyToID("_FrozenColor");
        public static readonly int FrozenHighlight = Shader.PropertyToID("_FrozenHighlight");
        public static readonly int FrozenDensity = Shader.PropertyToID("_FrozenDensity");
        public static readonly int FrozenSpeed = Shader.PropertyToID("_FrozenSpeed");
        public static readonly int BurnEnabled = Shader.PropertyToID("_EnableBurn");
        public static readonly int BurnEdgeColor = Shader.PropertyToID("_BurnEdgeColor");
        public static readonly int BurnInsideColor = Shader.PropertyToID("_BurnInsideColor");
        public static readonly int BurnProgress = Shader.PropertyToID("_BurnProgress");
        public static readonly int BurnWidth = Shader.PropertyToID("_BurnWidth");
        public static readonly int PoisonEnabled = Shader.PropertyToID("_EnablePoison");
        public static readonly int PoisonColor = Shader.PropertyToID("_PoisonColor");
        public static readonly int PoisonDensity = Shader.PropertyToID("_PoisonDensity");
        public static readonly int PoisonSpeed = Shader.PropertyToID("_PoisonSpeed");

        public static void SetTime(MaterialPropertyBlock block, ESCompositeTimeMode mode, float timeScale = 1f, float customTime = 0f)
        {
            if (block == null) return;
            block.SetFloat(TimeMode, (float)mode);
            block.SetFloat(TimeScale, Mathf.Clamp(timeScale, -4f, 4f));
            block.SetFloat(CustomTime, customTime);
        }

        public static void SetTimeModifiers(
            MaterialPropertyBlock block,
            bool quantizeToFPS,
            float framesPerSecond,
            bool useFrequency,
            float frequency,
            float range)
        {
            if (block == null) return;
            block.SetFloat(TimeFPSEnabled, quantizeToFPS ? 1f : 0f);
            block.SetFloat(TimeFPS, Mathf.Clamp(Mathf.Abs(framesPerSecond), 0.01f, 240f));
            block.SetFloat(TimeFrequencyEnabled, useFrequency ? 1f : 0f);
            block.SetFloat(TimeFrequency, frequency);
            block.SetFloat(TimeRange, range);
        }

        public static void SetAlphaClip(MaterialPropertyBlock block, bool enabled, float cutoff)
        {
            if (block == null) return;
            block.SetFloat(AlphaClipEnabled, enabled ? 1f : 0f);
            block.SetFloat(Cutoff, Mathf.Clamp01(cutoff));
        }

        public static void SetNoise(
            MaterialPropertyBlock block,
            Texture texture,
            Vector2 scale,
            Vector2 speed)
        {
            SetNoise(
                block,
                texture,
                new Vector4(scale.x, scale.y, 0f, 0f),
                new Vector4(speed.x, speed.y, 0f, 0f));
        }

        public static void SetNoise(
            MaterialPropertyBlock block,
            Texture texture,
            Vector4 scale,
            Vector4 speed)
        {
            if (block == null) return;
            if (texture != null) block.SetTexture(NoiseTexture, texture);
            block.SetVector(NoiseScale, scale);
            block.SetVector(NoiseSpeed, speed);
        }

        public static void SetLegacyDistortion(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            float strength)
        {
            SetLegacyDistortion(
                block, enabled, noiseTexture, noiseScale, noiseSpeed, strength, Vector2.one);
        }

        public static void SetLegacyDistortion(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            float strength,
            Vector2 direction)
        {
            if (block == null) return;
            block.SetFloat(LegacyDistortionEnabled, enabled ? 1f : 0f);
            SetNoise(block, noiseTexture, noiseScale, noiseSpeed);
            block.SetFloat(LegacyDistortionStrength, Mathf.Clamp(strength, 0f, 0.2f));
            block.SetVector(DistortionDirection, new Vector4(
                Mathf.Clamp(direction.x, -4f, 4f),
                Mathf.Clamp(direction.y, -4f, 4f),
                0f,
                0f));
        }

        public static void SetMainTextureTransform(MaterialPropertyBlock block, Vector2 scale, Vector2 offset)
        {
            if (block == null) return;
            block.SetVector(MainTexScaleOffset, new Vector4(scale.x, scale.y, offset.x, offset.y));
        }

        public static void SetUVTransform(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 pivot,
            Vector2 scale,
            Vector2 offset,
            float rotationDegrees,
            bool distortionEnabled = false,
            Vector2 distortionFrequency = default,
            Vector2 distortionSpeed = default,
            float distortionAmount = 0f,
            float rotationSpeed = 0f,
            Texture distortionNoiseTexture = null,
            Vector2 distortionFrom = default,
            Vector2 distortionTo = default,
            float distortionFade = 1f,
            Texture distortionMask = null,
            ESCompositeTextureChannel distortionMaskChannel = ESCompositeTextureChannel.透明)
        {
            if (block == null) return;
            block.SetFloat(UVTransformEnabled, enabled ? 1f : 0f);
            block.SetVector(UVPivot, new Vector4(pivot.x, pivot.y, 0f, 0f));
            block.SetVector(UVScale, new Vector4(ClampSignedScale(scale.x), ClampSignedScale(scale.y), 0f, 0f));
            block.SetVector(UVOffset, new Vector4(offset.x, offset.y, 0f, 0f));
            block.SetFloat(UVRotation, Mathf.Repeat(rotationDegrees + 180f, 360f) - 180f);
            block.SetFloat(UVRotationSpeed, rotationSpeed);
            block.SetFloat(UVDistortEnabled, distortionEnabled ? 1f : 0f);
            if (distortionFrequency == Vector2.zero) distortionFrequency = Vector2.one;
            block.SetVector(UVDistortFrequency, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(distortionFrequency.x)),
                Mathf.Max(0.001f, Mathf.Abs(distortionFrequency.y)),
                0f,
                0f));
            block.SetVector(UVDistortSpeed, new Vector4(distortionSpeed.x, distortionSpeed.y, 0f, 0f));
            block.SetFloat(UVDistortAmount, Mathf.Clamp(distortionAmount, 0f, 0.2f));
            if (distortionNoiseTexture != null) block.SetTexture(UVDistortNoiseTexture, distortionNoiseTexture);
            if (distortionFrom == Vector2.zero && distortionTo == Vector2.zero)
            {
                distortionFrom = new Vector2(-0.02f, -0.02f);
                distortionTo = new Vector2(0.02f, 0.02f);
            }
            block.SetVector(UVDistortFrom, new Vector4(distortionFrom.x, distortionFrom.y, 0f, 0f));
            block.SetVector(UVDistortTo, new Vector4(distortionTo.x, distortionTo.y, 0f, 0f));
            block.SetFloat(UVDistortFade, Mathf.Clamp01(distortionFade));
            block.SetFloat(UVDistortMaskEnabled, distortionMask != null ? 1f : 0f);
            if (distortionMask != null) block.SetTexture(UVDistortMask, distortionMask);
            block.SetFloat(UVDistortMaskChannel, (float)distortionMaskChannel);
        }

        public static void SetSplitToning(
            MaterialPropertyBlock block,
            bool enabled,
            Color shadows,
            Color highlights,
            float balance = 0f,
            float strength = 1f,
            float contrast = 1f,
            float shift = 0f)
        {
            if (block == null) return;
            block.SetFloat(SplitToningEnabled, enabled ? 1f : 0f);
            block.SetColor(SplitToneShadows, shadows);
            block.SetColor(SplitToneHighlights, highlights);
            block.SetFloat(SplitToneBalance, Mathf.Clamp(balance, -1f, 1f));
            block.SetFloat(SplitToneStrength, Mathf.Clamp01(strength));
            block.SetFloat(SplitToneContrast, Mathf.Clamp(contrast, 0.001f, 8f));
            block.SetFloat(SplitToneShift, Mathf.Clamp(shift, -1f, 1f));
        }

        public static void SetAlphaTint(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float minimumAlpha = 0.02f,
            float fade = 1f)
        {
            if (block == null) return;
            block.SetFloat(AlphaTintEnabled, enabled ? 1f : 0f);
            block.SetColor(AlphaTint, color);
            block.SetFloat(AlphaTintMin, Mathf.Clamp01(minimumAlpha));
            block.SetFloat(AlphaTintFade, Mathf.Clamp01(fade));
        }

        public static void SetColorReplace(
            MaterialPropertyBlock block,
            bool enabled,
            Color from,
            Color to,
            float range = 0.1f,
            float softness = 0.1f,
            float contrast = 1f,
            float fade = 1f)
        {
            if (block == null) return;
            block.SetFloat(ColorReplaceEnabled, enabled ? 1f : 0f);
            block.SetColor(ReplaceFrom, from);
            block.SetColor(ReplaceTo, to);
            block.SetFloat(ReplaceRange, Mathf.Clamp01(range));
            block.SetFloat(ReplaceSoftness, Mathf.Clamp(softness, 0.001f, 1f));
            block.SetFloat(ReplaceContrast, Mathf.Clamp(contrast, 0.001f, 8f));
            block.SetFloat(ReplaceFade, Mathf.Clamp01(fade));
        }

        public static void SetPingPongGlow(
            MaterialPropertyBlock block,
            bool enabled,
            Color from,
            Color to,
            float frequency = 2f,
            float intensity = 1f,
            float contrast = 1f,
            float fade = 1f)
        {
            if (block == null) return;
            block.SetFloat(PingPongGlowEnabled, enabled ? 1f : 0f);
            block.SetColor(GlowFrom, from);
            block.SetColor(GlowTo, to);
            block.SetFloat(GlowFrequency, Mathf.Clamp(frequency, -128f, 128f));
            block.SetFloat(GlowIntensity, Mathf.Clamp(intensity, 0f, 8f));
            block.SetFloat(GlowContrast, Mathf.Clamp(contrast, 0.001f, 8f));
            block.SetFloat(GlowFade, Mathf.Clamp01(fade));
        }

        public static void SetSpriteShadow(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 offset,
            Color color,
            float fade = 1f)
        {
            if (block == null) return;
            block.SetFloat(SpriteShadowEnabled, enabled ? 1f : 0f);
            block.SetVector(SpriteShadowOffset, new Vector4(
                Mathf.Clamp(offset.x, -32f, 32f),
                Mathf.Clamp(offset.y, -32f, 32f),
                0f,
                0f));
            block.SetColor(SpriteShadowColor, color);
            block.SetFloat(SpriteShadowFade, Mathf.Clamp01(fade));
        }

        public static void SetOutlines(
            MaterialPropertyBlock block,
            bool innerEnabled,
            Color innerColor,
            float innerWidth,
            bool outerEnabled,
            Color outerColor,
            float outerWidth,
            bool pixelEnabled,
            Color pixelColor,
            float pixelWidth)
        {
            if (block == null) return;
            block.SetFloat(InnerOutlineEnabled, innerEnabled ? 1f : 0f);
            block.SetColor(InnerOutlineColor, innerColor);
            block.SetFloat(InnerOutlineWidth, Mathf.Clamp(innerWidth, 0f, 1f));
            block.SetFloat(OuterOutlineEnabled, outerEnabled ? 1f : 0f);
            block.SetColor(OuterOutlineColor, outerColor);
            block.SetFloat(OuterOutlineWidth, Mathf.Clamp(outerWidth, 0f, 0.05f));
            block.SetFloat(PixelOutlineEnabled, pixelEnabled ? 1f : 0f);
            block.SetColor(PixelOutlineColor, pixelColor);
            block.SetFloat(PixelOutlineWidth, Mathf.Clamp(pixelWidth, 0f, 4f));
        }

        public static void SetInnerOutline(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float width,
            float fade,
            bool distortionEnabled,
            Vector2 distortionIntensity,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            bool textureEnabled,
            Texture tintTexture,
            Vector2 textureSpeed,
            bool outlineOnly)
        {
            if (block == null) return;
            block.SetFloat(InnerOutlineEnabled, enabled ? 1f : 0f);
            block.SetColor(InnerOutlineColor, color);
            block.SetFloat(InnerOutlineWidth, width);
            block.SetFloat(InnerOutlineFade, Mathf.Clamp01(fade));
            block.SetFloat(InnerOutlineDistortionEnabled, distortionEnabled ? 1f : 0f);
            block.SetVector(InnerOutlineDistortionIntensity, ToVector4(distortionIntensity));
            block.SetVector(InnerOutlineNoiseScale, ToVector4(noiseScale));
            block.SetVector(InnerOutlineNoiseSpeed, ToVector4(noiseSpeed));
            bool useTexture = textureEnabled && tintTexture != null;
            block.SetFloat(InnerOutlineTextureEnabled, useTexture ? 1f : 0f);
            if (tintTexture != null) block.SetTexture(InnerOutlineTintTexture, tintTexture);
            block.SetVector(InnerOutlineTextureSpeed, ToVector4(textureSpeed));
            block.SetFloat(InnerOutlineOnly, outlineOnly ? 1f : 0f);
        }

        public static void SetOuterOutline(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float width,
            float fade,
            bool distortionEnabled,
            Vector2 distortionIntensity,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            bool textureEnabled,
            Texture tintTexture,
            Vector2 textureSpeed,
            bool outlineOnly)
        {
            if (block == null) return;
            block.SetFloat(OuterOutlineEnabled, enabled ? 1f : 0f);
            block.SetColor(OuterOutlineColor, color);
            block.SetFloat(OuterOutlineWidth, width);
            block.SetFloat(OuterOutlineFade, Mathf.Clamp01(fade));
            block.SetFloat(OuterOutlineDistortionEnabled, distortionEnabled ? 1f : 0f);
            block.SetVector(OuterOutlineDistortionIntensity, ToVector4(distortionIntensity));
            block.SetVector(OuterOutlineNoiseScale, ToVector4(noiseScale));
            block.SetVector(OuterOutlineNoiseSpeed, ToVector4(noiseSpeed));
            bool useTexture = textureEnabled && tintTexture != null;
            block.SetFloat(OuterOutlineTextureEnabled, useTexture ? 1f : 0f);
            if (tintTexture != null) block.SetTexture(OuterOutlineTintTexture, tintTexture);
            block.SetVector(OuterOutlineTextureSpeed, ToVector4(textureSpeed));
            block.SetFloat(OuterOutlineOnly, outlineOnly ? 1f : 0f);
        }

        public static void SetPixelOutline(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float width,
            float fade,
            bool textureEnabled,
            Texture tintTexture,
            Vector2 textureSpeed,
            bool outlineOnly)
        {
            if (block == null) return;
            block.SetFloat(PixelOutlineEnabled, enabled ? 1f : 0f);
            block.SetColor(PixelOutlineColor, color);
            block.SetFloat(PixelOutlineWidth, width);
            block.SetFloat(PixelOutlineFade, Mathf.Clamp01(fade));
            bool useTexture = textureEnabled && tintTexture != null;
            block.SetFloat(PixelOutlineTextureEnabled, useTexture ? 1f : 0f);
            if (tintTexture != null) block.SetTexture(PixelOutlineTintTexture, tintTexture);
            block.SetVector(PixelOutlineTextureSpeed, ToVector4(textureSpeed));
            block.SetFloat(PixelOutlineOnly, outlineOnly ? 1f : 0f);
        }

        public static void SetShine(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float speed,
            float width,
            float intensity,
            Vector3 direction,
            float fallbackAngle = 30f)
        {
            if (block == null) return;
            block.SetFloat(ShineEnabled, enabled ? 1f : 0f);
            block.SetColor(ShineColor, color);
            block.SetFloat(ShineSpeed, Mathf.Clamp(speed, -128f, 128f));
            block.SetFloat(ShineWidth, Mathf.Clamp(width, 0.001f, 8f));
            block.SetFloat(ShineIntensity, Mathf.Clamp(intensity, 0f, 8f));
            block.SetFloat(ShineSpace, (float)ESCompositeProjectionSpace.兼容默认);
            block.SetVector(ShineDirection, new Vector4(direction.x, direction.y, direction.z, 0f));
            float resolvedAngle = direction.x * direction.x + direction.y * direction.y > 0.000001f
                ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                : fallbackAngle;
            resolvedAngle = Mathf.Repeat(resolvedAngle, 360f);
            block.SetFloat(ShineAngle, resolvedAngle);
            block.SetFloat(ShineRotation, resolvedAngle);
        }

        public static void SetShine(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float speed,
            float width,
            float intensity,
            Vector3 direction,
            ESCompositeProjectionSpace space,
            float fallbackAngle = 30f)
        {
            SetShine(
                block, enabled, color, speed, width, intensity,
                direction, fallbackAngle);
            if (block == null) return;
            block.SetFloat(ShineSpace, Mathf.Clamp((float)space, 0f, 2f));
        }

        public static void SetHologram(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float lineFrequency,
            float lineGap,
            float speed,
            float minAlpha)
        {
            if (block == null) return;
            block.SetFloat(HologramEnabled, enabled ? 1f : 0f);
            block.SetColor(HologramColor, color);
            block.SetFloat(HologramLineFrequency, Mathf.Clamp(lineFrequency, 0.01f, 2048f));
            block.SetFloat(HologramLineGap, Mathf.Clamp01(lineGap));
            block.SetFloat(HologramSpeed, Mathf.Clamp(speed, -128f, 128f));
            block.SetFloat(HologramMinAlpha, Mathf.Clamp01(minAlpha));
            block.SetVector(HologramDirection, new Vector4(0f, 1f, 0f, 0f));
            block.SetVector(HologramDistortionDirection, new Vector4(1f, 0f, 0f, 0f));
        }

        public static void SetHologram(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float lineFrequency,
            float lineGap,
            float speed,
            float minAlpha,
            float fade,
            float contrast,
            ES3DLitHologramSpace space,
            float distortionOffset,
            float distortionSpeed,
            float distortionDensity,
            float distortionScale)
        {
            SetHologram(
                block, enabled, color, lineFrequency, lineGap, speed, minAlpha,
                fade, contrast, space, distortionOffset, distortionSpeed,
                distortionDensity, distortionScale, Vector3.zero, Vector2.zero);
        }

        public static void SetHologram(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float lineFrequency,
            float lineGap,
            float speed,
            float minAlpha,
            float fade,
            float contrast,
            ES3DLitHologramSpace space,
            float distortionOffset,
            float distortionSpeed,
            float distortionDensity,
            float distortionScale,
            Vector3 scanDirection,
            Vector2 distortionDirection)
        {
            if (block == null) return;
            block.SetFloat(HologramEnabled, enabled ? 1f : 0f);
            block.SetColor(HologramColor, color);
            block.SetFloat(HologramLineFrequency, lineFrequency);
            block.SetFloat(HologramLineGap, lineGap);
            block.SetFloat(HologramSpeed, speed);
            block.SetFloat(HologramMinAlpha, Mathf.Clamp01(minAlpha));
            block.SetFloat(HologramFade, Mathf.Clamp01(fade));
            block.SetFloat(HologramContrast, contrast);
            block.SetFloat(HologramSpace, (float)space);
            block.SetVector(HologramDirection, new Vector4(
                scanDirection.x, scanDirection.y, scanDirection.z, 0f));
            block.SetFloat(HologramDistortionOffset, distortionOffset);
            block.SetVector(HologramDistortionDirection, ToVector4(distortionDirection));
            block.SetFloat(HologramDistortionSpeed, distortionSpeed);
            block.SetFloat(HologramDistortionDensity, distortionDensity);
            block.SetFloat(HologramDistortionScale, distortionScale);
        }

        public static void SetGlitch(MaterialPropertyBlock block, bool enabled, float intensity, float speed)
        {
            if (block == null) return;
            block.SetFloat(GlitchEnabled, enabled ? 1f : 0f);
            block.SetFloat(GlitchIntensity, Mathf.Clamp(intensity, 0f, 0.2f));
            block.SetFloat(GlitchSpeed, Mathf.Clamp(speed, -128f, 128f));
            block.SetVector(GlitchScanDirection, new Vector4(0f, 1f, 0f, 0f));
        }

        public static void SetGlitchScanDirection(MaterialPropertyBlock block, Vector3 direction)
        {
            if (block == null) return;
            Vector3 normalized = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector3.up;
            block.SetVector(GlitchScanDirection, new Vector4(
                normalized.x, normalized.y, normalized.z, 0f));
        }

        public static void SetGlitch(
            MaterialPropertyBlock block,
            bool enabled,
            float intensity,
            float speed,
            float fade,
            float maskMin,
            Vector2 maskScale,
            Vector2 maskSpeed,
            float hueSpeed,
            float brightness,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            Vector2 distortion,
            Vector2 distortionScale,
            Vector2 distortionSpeed)
        {
            if (block == null) return;
            block.SetFloat(GlitchEnabled, enabled ? 1f : 0f);
            block.SetFloat(GlitchIntensity, intensity);
            block.SetFloat(GlitchSpeed, speed);
            block.SetFloat(GlitchFade, Mathf.Clamp01(fade));
            block.SetFloat(GlitchMaskMin, Mathf.Clamp01(maskMin));
            block.SetVector(GlitchMaskScale, ToVector4(maskScale));
            block.SetVector(GlitchMaskSpeed, ToVector4(maskSpeed));
            block.SetFloat(GlitchHueSpeed, hueSpeed);
            block.SetFloat(GlitchBrightness, brightness);
            block.SetVector(GlitchNoiseScale, ToVector4(noiseScale));
            block.SetVector(GlitchNoiseSpeed, ToVector4(noiseSpeed));
            block.SetVector(GlitchDistortion, ToVector4(distortion));
            block.SetVector(GlitchDistortionScale, ToVector4(distortionScale));
            block.SetVector(GlitchDistortionSpeed, ToVector4(distortionSpeed));
            block.SetVector(GlitchScanDirection, new Vector4(0f, 1f, 0f, 0f));
        }

        public static void SetBlackTint(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float power = 4f,
            float fade = 1f)
        {
            if (block == null) return;
            block.SetFloat(BlackTintEnabled, enabled ? 1f : 0f);
            block.SetColor(BlackTintColor, color);
            block.SetFloat(BlackTintPower, Mathf.Clamp(power, 0.001f, 16f));
            block.SetFloat(BlackTintFade, Mathf.Clamp01(fade));
        }

        public static void SetInkSpread(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            Color color,
            float contrast,
            float fade,
            float distance,
            Vector2 position,
            float width,
            Vector2 noiseScale,
            float noiseFactor)
        {
            if (block == null) return;
            block.SetFloat(InkSpreadEnabled, enabled ? 1f : 0f);
            if (noiseTexture != null) block.SetTexture(UberNoiseTexture, noiseTexture);
            block.SetColor(InkSpreadColor, color);
            block.SetFloat(InkSpreadContrast, Mathf.Clamp(contrast, 0.001f, 8f));
            block.SetFloat(InkSpreadFade, Mathf.Clamp01(fade));
            block.SetFloat(InkSpreadDistance, Mathf.Clamp(distance, -32f, 32f));
            block.SetVector(InkSpreadPosition, new Vector4(position.x, position.y, 0f, 0f));
            block.SetFloat(InkSpreadWidth, Mathf.Clamp(width, 0.001f, 4f));
            block.SetVector(InkSpreadNoiseScale, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.x)),
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.y)),
                0f,
                0f));
            block.SetFloat(InkSpreadNoiseFactor, Mathf.Clamp(noiseFactor, 0f, 4f));
        }

        public static void SetShiftHue(MaterialPropertyBlock block, bool enabled, float speed)
        {
            if (block == null) return;
            block.SetFloat(ShiftHueEnabled, enabled ? 1f : 0f);
            block.SetFloat(ShiftHueSpeed, Mathf.Clamp(speed, -32f, 32f));
        }

        public static void SetAddHue(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            float speed,
            float brightness,
            float saturation,
            float contrast,
            Texture mask = null,
            Vector2 maskScale = default,
            Vector2 maskOffset = default)
        {
            if (block == null) return;
            if (maskScale == Vector2.zero) maskScale = Vector2.one;
            block.SetFloat(AddHueEnabled, enabled ? 1f : 0f);
            block.SetFloat(AddHueFade, Mathf.Clamp01(fade));
            block.SetFloat(AddHueSpeed, Mathf.Clamp(speed, -32f, 32f));
            block.SetFloat(AddHueBrightness, Mathf.Clamp(brightness, 0f, 16f));
            block.SetFloat(AddHueSaturation, Mathf.Clamp01(saturation));
            block.SetFloat(AddHueContrast, Mathf.Clamp(contrast, 0.001f, 8f));
            block.SetFloat(AddHueMaskEnabled, mask != null ? 1f : 0f);
            if (mask != null) block.SetTexture(AddHueMask, mask);
            block.SetVector(AddHueMaskScaleOffset, new Vector4(
                ClampSignedScale(maskScale.x),
                ClampSignedScale(maskScale.y),
                maskOffset.x,
                maskOffset.y));
        }

        public static void SetSineGlow(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float contrast,
            float frequency,
            float minimum,
            float maximum,
            float fade = 1f,
            Texture mask = null,
            Vector2 maskScale = default,
            Vector2 maskOffset = default)
        {
            if (block == null) return;
            if (maskScale == Vector2.zero) maskScale = Vector2.one;
            block.SetFloat(SineGlowEnabled, enabled ? 1f : 0f);
            block.SetColor(SineGlowColor, color);
            block.SetFloat(SineGlowContrast, Mathf.Clamp(contrast, 0.001f, 8f));
            block.SetFloat(SineGlowFrequency, Mathf.Clamp(frequency, -32f, 32f));
            block.SetFloat(SineGlowMin, Mathf.Clamp(minimum, -8f, 8f));
            block.SetFloat(SineGlowMax, Mathf.Clamp(maximum, -8f, 8f));
            block.SetFloat(SineGlowFade, Mathf.Clamp01(fade));
            block.SetFloat(SineGlowMaskEnabled, mask != null ? 1f : 0f);
            if (mask != null) block.SetTexture(SineGlowMask, mask);
            block.SetVector(SineGlowMaskScaleOffset, new Vector4(
                ClampSignedScale(maskScale.x),
                ClampSignedScale(maskScale.y),
                maskOffset.x,
                maskOffset.y));
        }

        public static void SetSqueeze(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 center,
            Vector2 scale,
            float power = 1f,
            float fade = 1f)
        {
            if (block == null) return;
            block.SetFloat(SqueezeEnabled, enabled ? 1f : 0f);
            block.SetVector(SqueezeCenter, new Vector4(center.x, center.y, 0f, 0f));
            block.SetVector(SqueezeScale, new Vector4(
                Mathf.Clamp(scale.x, -8f, 8f),
                Mathf.Clamp(scale.y, -8f, 8f),
                0f,
                0f));
            block.SetFloat(SqueezePower, Mathf.Clamp(power, 0.001f, 8f));
            block.SetFloat(SqueezeFade, Mathf.Clamp01(fade));
        }

        public static void SetSineRotate(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 pivot,
            float angle,
            float frequency,
            float fade = 1f)
        {
            if (block == null) return;
            block.SetFloat(SineRotateEnabled, enabled ? 1f : 0f);
            block.SetVector(SineRotatePivot, new Vector4(pivot.x, pivot.y, 0f, 0f));
            block.SetFloat(SineRotateAngle, Mathf.Clamp(angle, -720f, 720f));
            block.SetFloat(SineRotateFrequency, Mathf.Clamp(frequency, -32f, 32f));
            block.SetFloat(SineRotateFade, Mathf.Clamp01(fade));
        }

        public static void SetSineMove(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 offset,
            Vector2 frequency,
            float fade = 1f)
        {
            if (block == null) return;
            block.SetFloat(SineMoveEnabled, enabled ? 1f : 0f);
            block.SetVector(SineMoveOffset, new Vector4(
                Mathf.Clamp(offset.x, -8f, 8f),
                Mathf.Clamp(offset.y, -8f, 8f),
                0f,
                0f));
            block.SetVector(SineMoveFrequency, new Vector4(
                Mathf.Clamp(frequency.x, -32f, 32f),
                Mathf.Clamp(frequency.y, -32f, 32f),
                0f,
                0f));
            block.SetFloat(SineMoveFade, Mathf.Clamp01(fade));
        }

        public static void SetSineScale(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 factor,
            float frequency)
        {
            if (block == null) return;
            block.SetFloat(SineScaleEnabled, enabled ? 1f : 0f);
            block.SetVector(SineScaleFactor, new Vector4(
                Mathf.Clamp(factor.x, -4f, 4f),
                Mathf.Clamp(factor.y, -4f, 4f),
                0f,
                0f));
            block.SetFloat(SineScaleFrequency, Mathf.Clamp(frequency, -32f, 32f));
        }

        public static void SetFullAlphaDissolve(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            float width,
            Vector2 noiseScale)
        {
            if (block == null) return;
            block.SetFloat(FullAlphaDissolveEnabled, enabled ? 1f : 0f);
            block.SetFloat(FullAlphaDissolveFade, Mathf.Clamp(fade, -8f, 8f));
            block.SetFloat(FullAlphaDissolveWidth, Mathf.Clamp(width, 0.001f, 8f));
            block.SetVector(FullAlphaDissolveNoiseScale, ToVector4(noiseScale));
        }

        public static void SetSourceAlphaDissolve(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            Vector2 position,
            float width,
            Vector2 noiseScale,
            float noiseFactor,
            bool invert)
        {
            if (block == null) return;
            block.SetFloat(SourceAlphaDissolveEnabled, enabled ? 1f : 0f);
            block.SetFloat(SourceAlphaDissolveFade, Mathf.Clamp(fade, -8f, 8f));
            block.SetVector(SourceAlphaDissolvePosition, ToVector4(position));
            block.SetFloat(SourceAlphaDissolveWidth, Mathf.Clamp(width, 0.001f, 8f));
            block.SetVector(SourceAlphaDissolveNoiseScale, ToVector4(noiseScale));
            block.SetFloat(SourceAlphaDissolveNoiseFactor, Mathf.Clamp(noiseFactor, -4f, 4f));
            block.SetFloat(SourceAlphaDissolveInvert, invert ? 1f : 0f);
        }

        public static void SetSourceGlowDissolve(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            Vector2 position,
            float width,
            Color edgeColor,
            Vector2 noiseScale,
            float noiseFactor,
            bool invert)
        {
            if (block == null) return;
            block.SetFloat(SourceGlowDissolveEnabled, enabled ? 1f : 0f);
            block.SetFloat(SourceGlowDissolveFade, Mathf.Clamp(fade, -8f, 8f));
            block.SetVector(SourceGlowDissolvePosition, ToVector4(position));
            block.SetFloat(SourceGlowDissolveWidth, Mathf.Clamp(width, 0.001f, 8f));
            block.SetColor(SourceGlowDissolveEdgeColor, edgeColor);
            block.SetVector(SourceGlowDissolveNoiseScale, ToVector4(noiseScale));
            block.SetFloat(SourceGlowDissolveNoiseFactor, Mathf.Clamp(noiseFactor, -4f, 4f));
            block.SetFloat(SourceGlowDissolveInvert, invert ? 1f : 0f);
        }

        public static void SetDirectionalAlphaFade(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            float rotation,
            float width,
            Vector2 noiseScale,
            float noiseFactor,
            bool invert)
        {
            if (block == null) return;
            block.SetFloat(DirectionalAlphaFadeEnabled, enabled ? 1f : 0f);
            block.SetFloat(DirectionalAlphaFadeFade, Mathf.Clamp(fade, -8f, 8f));
            block.SetFloat(DirectionalAlphaFadeRotation, Mathf.Repeat(rotation, 360f));
            block.SetFloat(DirectionalAlphaFadeWidth, Mathf.Clamp(width, 0.001f, 8f));
            block.SetVector(DirectionalAlphaFadeNoiseScale, ToVector4(noiseScale));
            block.SetFloat(DirectionalAlphaFadeNoiseFactor, Mathf.Clamp(noiseFactor, -4f, 4f));
            block.SetFloat(DirectionalAlphaFadeInvert, invert ? 1f : 0f);
        }

        public static void SetDirectionalGlowFade(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            float rotation,
            float width,
            Color edgeColor,
            Vector2 noiseScale,
            float noiseFactor,
            bool invert)
        {
            if (block == null) return;
            block.SetFloat(DirectionalGlowFadeEnabled, enabled ? 1f : 0f);
            block.SetFloat(DirectionalGlowFadeFade, Mathf.Clamp(fade, -8f, 8f));
            block.SetFloat(DirectionalGlowFadeRotation, Mathf.Repeat(rotation, 360f));
            block.SetFloat(DirectionalGlowFadeWidth, Mathf.Clamp(width, 0.001f, 8f));
            block.SetColor(DirectionalGlowFadeEdgeColor, edgeColor);
            block.SetVector(DirectionalGlowFadeNoiseScale, ToVector4(noiseScale));
            block.SetFloat(DirectionalGlowFadeNoiseFactor, Mathf.Clamp(noiseFactor, -4f, 4f));
            block.SetFloat(DirectionalGlowFadeInvert, invert ? 1f : 0f);
        }

        public static void SetDirectionalDistortion(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            float rotation,
            float width,
            Vector2 noiseScale,
            float noiseFactor,
            Vector2 distortion,
            float randomDirection,
            Vector2 distortionScale,
            bool invert)
        {
            if (block == null) return;
            block.SetFloat(DirectionalDistortionEnabled, enabled ? 1f : 0f);
            block.SetFloat(DirectionalDistortionFade, Mathf.Clamp(fade, -8f, 8f));
            block.SetFloat(DirectionalDistortionRotation, Mathf.Repeat(rotation, 360f));
            block.SetFloat(DirectionalDistortionWidth, Mathf.Clamp(width, 0.001f, 8f));
            block.SetVector(DirectionalDistortionNoiseScale, ToVector4(noiseScale));
            block.SetFloat(DirectionalDistortionNoiseFactor, Mathf.Clamp(noiseFactor, -4f, 4f));
            block.SetVector(DirectionalDistortionAmount, ToVector4(distortion));
            block.SetFloat(DirectionalDistortionRandomDirection, Mathf.Clamp01(randomDirection));
            block.SetVector(DirectionalDistortionScale, ToVector4(distortionScale));
            block.SetFloat(DirectionalDistortionInvert, invert ? 1f : 0f);
        }

        public static void SetCustomFade(
            MaterialPropertyBlock block,
            bool enabled,
            Texture mask,
            float smoothness,
            Vector2 noiseScale,
            float noiseFactor,
            float alpha)
        {
            if (block == null) return;
            block.SetFloat(CustomFadeEnabled, enabled ? 1f : 0f);
            if (mask != null) block.SetTexture(CustomFadeMask, mask);
            block.SetFloat(CustomFadeSmoothness, Mathf.Clamp(smoothness, 0.001f, 16f));
            block.SetVector(CustomFadeNoiseScale, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.x)),
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.y)),
                0f,
                0f));
            block.SetFloat(CustomFadeNoiseFactor, Mathf.Clamp(noiseFactor, 0f, 0.5f));
            block.SetFloat(CustomFadeAlpha, Mathf.Clamp01(alpha));
        }

        public static void SetFullGlowDissolve(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            float width,
            Color edgeColor,
            Vector2 noiseScale)
        {
            if (block == null) return;
            block.SetFloat(FullGlowDissolveEnabled, enabled ? 1f : 0f);
            block.SetFloat(FullGlowDissolveFade, Mathf.Clamp01(fade));
            block.SetFloat(FullGlowDissolveWidth, Mathf.Clamp(width, 0.001f, 8f));
            block.SetColor(FullGlowDissolveEdgeColor, edgeColor);
            block.SetVector(FullGlowDissolveNoiseScale, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.x)),
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.y)),
                0f,
                0f));
        }

        public static void SetFullDistortion(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            Vector2 distortion,
            Vector2 noiseScale)
        {
            if (block == null) return;
            block.SetFloat(FullDistortionEnabled, enabled ? 1f : 0f);
            block.SetFloat(FullDistortionFade, Mathf.Clamp01(fade));
            block.SetVector(FullDistortionAmount, new Vector4(
                Mathf.Clamp(distortion.x, -4f, 4f),
                Mathf.Clamp(distortion.y, -4f, 4f),
                0f,
                0f));
            block.SetVector(FullDistortionNoiseScale, new Vector4(
                Mathf.Clamp(noiseScale.x, -128f, 128f),
                Mathf.Clamp(noiseScale.y, -128f, 128f),
                0f,
                0f));
        }

        public static void SetCamouflage(
            MaterialPropertyBlock block,
            bool enabled,
            Color baseColor,
            Color colorA,
            float densityA,
            float smoothnessA,
            Vector2 noiseScaleA,
            Color colorB,
            float densityB,
            float smoothnessB,
            Vector2 noiseScaleB,
            float contrast,
            float fade,
            bool animated,
            Vector2 distortionSpeed,
            Vector2 distortionIntensity,
            Vector2 distortionScale)
        {
            if (block == null) return;
            block.SetFloat(CamouflageEnabled, enabled ? 1f : 0f);
            block.SetColor(CamouflageBaseColor, baseColor);
            block.SetColor(CamouflageColorA, colorA);
            block.SetFloat(CamouflageDensityA, Mathf.Clamp01(densityA));
            block.SetFloat(CamouflageSmoothnessA, Mathf.Clamp(smoothnessA, 0.005f, 1f));
            block.SetVector(CamouflageNoiseScaleA, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(noiseScaleA.x)),
                Mathf.Max(0.001f, Mathf.Abs(noiseScaleA.y)), 0f, 0f));
            block.SetColor(CamouflageColorB, colorB);
            block.SetFloat(CamouflageDensityB, Mathf.Clamp01(densityB));
            block.SetFloat(CamouflageSmoothnessB, Mathf.Clamp(smoothnessB, 0.005f, 1f));
            block.SetVector(CamouflageNoiseScaleB, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(noiseScaleB.x)),
                Mathf.Max(0.001f, Mathf.Abs(noiseScaleB.y)), 0f, 0f));
            block.SetFloat(CamouflageContrast, Mathf.Clamp(contrast, 0.001f, 8f));
            block.SetFloat(CamouflageFade, Mathf.Clamp01(fade));
            block.SetFloat(CamouflageAnimationEnabled, animated ? 1f : 0f);
            block.SetVector(CamouflageDistortionSpeed, new Vector4(
                Mathf.Clamp(distortionSpeed.x, -32f, 32f),
                Mathf.Clamp(distortionSpeed.y, -32f, 32f), 0f, 0f));
            block.SetVector(CamouflageDistortionIntensity, new Vector4(
                Mathf.Clamp(distortionIntensity.x, -4f, 4f),
                Mathf.Clamp(distortionIntensity.y, -4f, 4f), 0f, 0f));
            block.SetVector(CamouflageDistortionScale, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(distortionScale.x)),
                Mathf.Max(0.001f, Mathf.Abs(distortionScale.y)), 0f, 0f));
        }

        public static void SetMetal(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float contrast,
            Color highlightColor,
            float highlightDensity,
            float highlightContrast,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            Vector2 distortionScale,
            Vector2 distortionSpeed,
            Vector2 distortion,
            float fade = 1f,
            Texture mask = null)
        {
            if (block == null) return;
            block.SetFloat(MetalEnabled, enabled ? 1f : 0f);
            block.SetColor(MetalColor, color);
            block.SetFloat(MetalContrast, Mathf.Clamp(contrast, 0.001f, 8f));
            block.SetColor(MetalHighlightColor, highlightColor);
            block.SetFloat(MetalHighlightDensity, Mathf.Clamp01(highlightDensity));
            block.SetFloat(MetalHighlightContrast, Mathf.Clamp(highlightContrast, 0.001f, 8f));
            block.SetVector(MetalNoiseScale, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.x)),
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.y)), 0f, 0f));
            block.SetVector(MetalNoiseSpeed, new Vector4(
                Mathf.Clamp(noiseSpeed.x, -32f, 32f),
                Mathf.Clamp(noiseSpeed.y, -32f, 32f), 0f, 0f));
            block.SetVector(MetalNoiseDistortionScale, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(distortionScale.x)),
                Mathf.Max(0.001f, Mathf.Abs(distortionScale.y)), 0f, 0f));
            block.SetVector(MetalNoiseDistortionSpeed, new Vector4(
                Mathf.Clamp(distortionSpeed.x, -32f, 32f),
                Mathf.Clamp(distortionSpeed.y, -32f, 32f), 0f, 0f));
            block.SetVector(MetalNoiseDistortion, new Vector4(
                Mathf.Clamp(distortion.x, -4f, 4f),
                Mathf.Clamp(distortion.y, -4f, 4f), 0f, 0f));
            block.SetFloat(MetalFade, Mathf.Clamp01(fade));
            block.SetFloat(MetalMaskEnabled, mask != null ? 1f : 0f);
            if (mask != null) block.SetTexture(MetalMask, mask);
        }

        public static void SetEnchanted(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 speed,
            Vector2 scale,
            float brightness,
            float contrast,
            float reduce,
            Color lowColor,
            Color highColor,
            bool rainbow,
            float rainbowSpeed,
            float rainbowDensity,
            float rainbowSaturation,
            bool replace,
            float fade = 1f)
        {
            if (block == null) return;
            block.SetFloat(EnchantedEnabled, enabled ? 1f : 0f);
            block.SetVector(EnchantedSpeed, new Vector4(
                Mathf.Clamp(speed.x, -32f, 32f),
                Mathf.Clamp(speed.y, -32f, 32f), 0f, 0f));
            block.SetVector(EnchantedScale, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                Mathf.Max(0.001f, Mathf.Abs(scale.y)), 0f, 0f));
            block.SetFloat(EnchantedBrightness, Mathf.Clamp(brightness, 0f, 16f));
            block.SetFloat(EnchantedContrast, Mathf.Clamp(contrast, 0.001f, 8f));
            block.SetFloat(EnchantedReduce, Mathf.Clamp(reduce, 0f, 2f));
            block.SetColor(EnchantedLowColor, lowColor);
            block.SetColor(EnchantedHighColor, highColor);
            block.SetFloat(EnchantedRainbowEnabled, rainbow ? 1f : 0f);
            block.SetFloat(EnchantedRainbowSpeed, Mathf.Clamp(rainbowSpeed, -32f, 32f));
            block.SetFloat(EnchantedRainbowDensity, Mathf.Clamp(rainbowDensity, -32f, 32f));
            block.SetFloat(EnchantedRainbowSaturation, Mathf.Clamp01(rainbowSaturation));
            block.SetFloat(EnchantedLerpEnabled, replace ? 1f : 0f);
            block.SetFloat(EnchantedFade, Mathf.Clamp01(fade));
        }

        public static void SetShifting(
            MaterialPropertyBlock block,
            bool enabled,
            float speed,
            float density,
            float brightness,
            float contrast,
            float saturation,
            Color colorA,
            Color colorB,
            bool rainbow,
            float fade = 1f)
        {
            if (block == null) return;
            block.SetFloat(ShiftingEnabled, enabled ? 1f : 0f);
            block.SetFloat(ShiftingSpeed, Mathf.Clamp(speed, -32f, 32f));
            block.SetFloat(ShiftingDensity, Mathf.Clamp(density, -32f, 32f));
            block.SetFloat(ShiftingBrightness, Mathf.Clamp(brightness, 0f, 16f));
            block.SetFloat(ShiftingContrast, Mathf.Clamp(contrast, 0.001f, 8f));
            block.SetFloat(ShiftingSaturation, Mathf.Clamp01(saturation));
            block.SetColor(ShiftingColorA, colorA);
            block.SetColor(ShiftingColorB, colorB);
            block.SetFloat(ShiftingRainbowEnabled, rainbow ? 1f : 0f);
            block.SetFloat(ShiftingFade, Mathf.Clamp01(fade));
        }

        public static void SetRainbow(
            MaterialPropertyBlock block,
            bool enabled,
            float speed,
            float density,
            float brightness)
        {
            SetRainbow(block, enabled, speed, density, brightness, Vector2.up);
        }

        public static void SetRainbow(
            MaterialPropertyBlock block,
            bool enabled,
            float speed,
            float density,
            float brightness,
            Vector2 direction)
        {
            if (block == null) return;
            Vector2 normalized = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector2.up;
            block.SetFloat(RainbowEnabled, enabled ? 1f : 0f);
            block.SetFloat(RainbowSpeed, Mathf.Clamp(speed, -128f, 128f));
            block.SetFloat(RainbowDensity, Mathf.Clamp(density, -128f, 128f));
            block.SetVector(RainbowDirection, new Vector4(normalized.x, normalized.y, 0f, 0f));
            block.SetFloat(RainbowBrightness, Mathf.Max(0f, brightness));
        }

        public static void SetFade(
            MaterialPropertyBlock block,
            ESCompositeFadeMode mode,
            float progress,
            float width,
            Vector2 position,
            float rotationDegrees,
            bool invert,
            float noiseFactor,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            Texture noiseTexture = null,
            Texture maskTexture = null,
            Color? edgeColor = null,
            float edgeWidth = 0.08f,
            float edgeIntensity = 1f,
            float distortionStrength = 0.03f)
        {
            if (block == null) return;
            ESCompositeFadeMode resolvedMode = mode == ESCompositeFadeMode.纹理遮罩 && maskTexture == null
                ? ESCompositeFadeMode.无
                : mode;
            block.SetFloat(FadeMode, (float)resolvedMode);
            block.SetFloat(FadeProgress, Mathf.Clamp01(progress));
            block.SetFloat(FadeWidth, Mathf.Clamp(width, 0.001f, 1f));
            block.SetVector(FadePosition, new Vector4(position.x, position.y, 0f, 0f));
            block.SetFloat(FadeRotation, Mathf.Repeat(rotationDegrees, 360f));
            block.SetFloat(FadeInvert, invert ? 1f : 0f);
            block.SetFloat(FadeNoiseFactor, Mathf.Clamp01(noiseFactor));
            block.SetVector(FadeNoiseScale, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.x)),
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.y)),
                0f,
                0f));
            block.SetVector(FadeNoiseSpeed, new Vector4(noiseSpeed.x, noiseSpeed.y, 0f, 0f));
            if (noiseTexture != null) block.SetTexture(FadeNoiseTexture, noiseTexture);
            if (maskTexture != null) block.SetTexture(FadeMaskTexture, maskTexture);
            block.SetColor(FadeEdgeColor, edgeColor ?? new Color(1f, 0.15f, 0.01f, 1f));
            block.SetFloat(FadeEdgeWidth, Mathf.Clamp(edgeWidth, 0.001f, 1f));
            block.SetFloat(FadeEdgeIntensity, Mathf.Clamp(edgeIntensity, 0f, 8f));
            block.SetFloat(FadeDistortionStrength, Mathf.Clamp(distortionStrength, 0f, 0.2f));
        }

        public static void SetSampling(
            MaterialPropertyBlock block,
            bool blurEnabled,
            bool gaussianBlur,
            float blurRadius,
            float blurIntensity,
            bool sharpenEnabled,
            float sharpenAmount,
            float sharpenRadius,
            float sharpenThreshold,
            float sharpenFade = 1f)
        {
            if (block == null) return;
            block.SetFloat(BlurEnabled, blurEnabled ? 1f : 0f);
            block.SetFloat(BlurMode, gaussianBlur ? 1f : 0f);
            block.SetFloat(BlurRadius, Mathf.Clamp(blurRadius, 0f, 0.02f));
            block.SetFloat(BlurIntensity, Mathf.Clamp01(blurIntensity));
            block.SetFloat(SharpenEnabled, sharpenEnabled ? 1f : 0f);
            block.SetFloat(SharpenAmount, Mathf.Clamp(sharpenAmount, 0f, 4f));
            block.SetFloat(SharpenRadius, Mathf.Clamp(sharpenRadius, 0f, 0.02f));
            block.SetFloat(SharpenThreshold, Mathf.Clamp(sharpenThreshold, 0f, 0.5f));
            block.SetFloat(SharpenFade, Mathf.Clamp01(sharpenFade));
        }

        public static void SetTiling(
            MaterialPropertyBlock block,
            ESCompositeTilingMode mode,
            Vector2 worldScale,
            Vector2 worldOffset,
            float worldPixelsPerUnit,
            Vector2 screenScale,
            Vector2 screenOffset,
            float screenPixelsPerTile)
        {
            if (block == null) return;
            block.SetFloat(TilingMode, Mathf.Clamp((float)mode, 0f, 2f));
            block.SetVector(WorldTilingScale, new Vector4(ClampSignedScale(worldScale.x), ClampSignedScale(worldScale.y), 0f, 0f));
            block.SetVector(WorldTilingOffset, new Vector4(worldOffset.x, worldOffset.y, 0f, 0f));
            block.SetFloat(WorldTilingPixelsPerUnit, Mathf.Clamp(worldPixelsPerUnit, 0.01f, 64f));
            block.SetVector(ScreenTilingScale, new Vector4(ClampSignedScale(screenScale.x), ClampSignedScale(screenScale.y), 0f, 0f));
            block.SetVector(ScreenTilingOffset, new Vector4(screenOffset.x, screenOffset.y, 0f, 0f));
            block.SetFloat(ScreenTilingPixelsPerUnit, Mathf.Clamp(screenPixelsPerTile, 1f, 2048f));
        }

        public static void SetGeneratedStylization(
            MaterialPropertyBlock block,
            bool smoothPixelArt,
            float smoothPixelStrength,
            bool checkerboard,
            float checkerboardDarken,
            float checkerboardTiling)
        {
            if (block == null) return;
            block.SetFloat(SmoothPixelArtEnabled, smoothPixelArt ? 1f : 0f);
            block.SetFloat(SmoothPixelStrength, Mathf.Clamp01(smoothPixelStrength));
            block.SetFloat(CheckerboardEnabled, checkerboard ? 1f : 0f);
            block.SetFloat(CheckerboardDarken, Mathf.Clamp01(checkerboardDarken));
            block.SetFloat(CheckerboardTiling, Mathf.Clamp(checkerboardTiling, 0.01f, 64f));
        }

        public static void SetRasterStylization(
            MaterialPropertyBlock block,
            bool pixelate,
            float pixelateCells,
            float pixelateStrength,
            bool halftone,
            float halftoneScale,
            float halftoneAngle,
            float halftoneStrength)
        {
            if (block == null) return;
            block.SetFloat(PixelateEnabled, pixelate ? 1f : 0f);
            block.SetFloat(PixelateCells, Mathf.Clamp(pixelateCells, 2f, 512f));
            block.SetFloat(PixelateStrength, Mathf.Clamp01(pixelateStrength));
            block.SetFloat(HalftoneEnabled, halftone ? 1f : 0f);
            block.SetFloat(HalftoneScale, Mathf.Clamp(halftoneScale, 4f, 512f));
            block.SetFloat(HalftoneAngle, Mathf.Clamp(halftoneAngle, 0f, 180f));
            block.SetFloat(HalftoneStrength, Mathf.Clamp01(halftoneStrength));
        }

        public static void SetHalftoneESNativeAlpha(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 position,
            float fade,
            float fadeWidth,
            bool invert)
        {
            if (block == null) return;
            block.SetFloat(HalftoneAlphaPattern, enabled ? 1f : 0f);
            block.SetVector(HalftonePosition, new Vector4(position.x, position.y, 0f, 0f));
            block.SetFloat(HalftoneFade, fade);
            block.SetFloat(HalftoneFadeWidth, Mathf.Max(Mathf.Abs(fadeWidth), 0.01f));
            block.SetFloat(HalftoneInvert, invert ? 1f : 0f);
        }

        public static void SetFlame(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float brightness,
            float smoothness,
            float radius,
            Vector2 speed,
            float noiseFactor,
            float noiseHeightFactor,
            Vector2 noiseScale)
        {
            if (block == null) return;
            block.SetFloat(FlameEnabled, enabled ? 1f : 0f);
            if (noiseTexture != null) block.SetTexture(UberNoiseTexture, noiseTexture);
            block.SetFloat(FlameBrightness, Mathf.Clamp(brightness, 0f, 16f));
            block.SetFloat(FlameSmooth, Mathf.Clamp(smoothness, 0f, 8f));
            block.SetFloat(FlameRadius, Mathf.Clamp(radius, 0.01f, 1f));
            block.SetVector(FlameSpeed, new Vector4(speed.x, speed.y, 0f, 0f));
            block.SetFloat(FlameNoiseFactor, Mathf.Clamp(noiseFactor, 0f, 8f));
            block.SetFloat(FlameNoiseHeightFactor, Mathf.Clamp(noiseHeightFactor, 0f, 4f));
            block.SetVector(FlameNoiseScale, new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.x)),
                Mathf.Max(0.001f, Mathf.Abs(noiseScale.y)),
                0f,
                0f));
            block.SetVector(FlameDirection, new Vector4(0f, 1f, 0f, 0f));
            block.SetVector(FlameCenter, new Vector4(0.5f, 0.4f, 0f, 0f));
        }

        public static void SetFlame(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float brightness,
            float smoothness,
            float radius,
            Vector2 speed,
            float noiseFactor,
            float noiseHeightFactor,
            Vector2 noiseScale,
            Vector2 direction,
            Vector2 center)
        {
            SetFlame(
                block, enabled, noiseTexture, brightness, smoothness, radius,
                speed, noiseFactor, noiseHeightFactor, noiseScale);
            if (block == null) return;
            Vector2 normalized = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector2.up;
            block.SetVector(FlameDirection, new Vector4(normalized.x, normalized.y, 0f, 0f));
            block.SetVector(FlameCenter, new Vector4(center.x, center.y, 0f, 0f));
        }

        public static void SetSmoke(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float alpha,
            float smoothness,
            float noiseScale,
            float noiseFactor,
            float darkEdge,
            bool vertexSeed)
        {
            if (block == null) return;
            block.SetFloat(SmokeEnabled, enabled ? 1f : 0f);
            if (noiseTexture != null) block.SetTexture(UberNoiseTexture, noiseTexture);
            block.SetFloat(SmokeAlpha, Mathf.Clamp01(alpha));
            block.SetFloat(SmokeSmoothness, Mathf.Clamp(smoothness, 0f, 4f));
            block.SetFloat(SmokeNoiseScale, Mathf.Clamp(noiseScale, 0.01f, 8f));
            block.SetFloat(SmokeNoiseFactor, Mathf.Clamp01(noiseFactor));
            block.SetFloat(SmokeDarkEdge, Mathf.Clamp(darkEdge, 0f, 1.5f));
            block.SetFloat(SmokeVertexSeed, vertexSeed ? 1f : 0f);
            block.SetVector(SmokeSpeed, Vector4.zero);
        }

        public static void SetSmoke(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float alpha,
            float smoothness,
            float noiseScale,
            float noiseFactor,
            float darkEdge,
            bool vertexSeed,
            Vector2 speed)
        {
            SetSmoke(
                block, enabled, noiseTexture, alpha, smoothness,
                noiseScale, noiseFactor, darkEdge, vertexSeed);
            if (block == null) return;
            block.SetVector(SmokeSpeed, new Vector4(speed.x, speed.y, 0f, 0f));
        }

        private static float ClampSignedScale(float value)
        {
            if (Mathf.Abs(value) >= 0.0001f) return value;
            return value < 0f ? -0.0001f : 0.0001f;
        }

        public static void SetWind(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 direction,
            float amplitude,
            float frequency,
            float speed,
            float anchor = 0f,
            float globalInfluence = 1f)
        {
            SetWind(
                block, enabled, direction, amplitude, frequency, speed,
                anchor, globalInfluence, Vector2.up);
        }

        public static void SetWind(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 direction,
            float amplitude,
            float frequency,
            float speed,
            float anchor,
            float globalInfluence,
            Vector2 anchorDirection)
        {
            if (block == null) return;
            Vector2 normalized = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            Vector2 normalizedAnchor = anchorDirection.sqrMagnitude > 0.000001f
                ? anchorDirection.normalized
                : Vector2.up;
            block.SetFloat(WindEnabled, enabled ? 1f : 0f);
            block.SetVector(WindDirection, new Vector4(normalized.x, normalized.y, 0f, 0f));
            block.SetFloat(WindAmplitude, Mathf.Max(0f, amplitude));
            block.SetFloat(WindFrequency, Mathf.Max(0f, frequency));
            block.SetFloat(WindSpeed, Mathf.Max(0f, speed));
            block.SetFloat(WindAnchor, Mathf.Clamp01(anchor));
            block.SetVector(WindAnchorDirection, new Vector4(
                normalizedAnchor.x, normalizedAnchor.y, 0f, 0f));
            block.SetFloat(WindGlobalInfluence, Mathf.Clamp01(globalInfluence));
        }

        public static void SetSquish(MaterialPropertyBlock block, bool enabled, float amount, float speed)
        {
            if (block == null) return;
            block.SetFloat(SquishEnabled, enabled ? 1f : 0f);
            block.SetFloat(SquishAmount, Mathf.Clamp(amount, 0f, 0.8f));
            block.SetFloat(SquishSpeed, Mathf.Max(0f, speed));
            block.SetVector(SquishDirection, new Vector4(1f, 0f, 0f, 0f));
        }

        public static void SetSquish(
            MaterialPropertyBlock block,
            bool enabled,
            float amount,
            float speed,
            Vector2 direction)
        {
            SetSquish(block, enabled, amount, speed);
            if (block == null) return;
            Vector2 normalized = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector2.right;
            block.SetVector(SquishDirection, new Vector4(normalized.x, normalized.y, 0f, 0f));
        }

        public static void SetSquishFade(MaterialPropertyBlock block, float fade)
        {
            if (block == null) return;
            block.SetFloat(SquishFade, Mathf.Clamp01(fade));
        }

        public static void SetInteractiveWind(MaterialPropertyBlock block, float rotationDegrees, float height)
        {
            if (block == null) return;
            block.SetFloat(InteractiveWindRotation, Mathf.Clamp(rotationDegrees, -89f, 89f));
            block.SetFloat(InteractiveWindHeight, Mathf.Max(0.0001f, Mathf.Abs(height)));
        }

        public static void SetInteractiveSquish(MaterialPropertyBlock block, float amount)
        {
            if (block == null) return;
            block.SetFloat(InteractiveSquish, Mathf.Clamp(amount, -0.8f, 0.8f));
        }

        public static void SetWindPhaseOffset(MaterialPropertyBlock block, float value)
        {
            if (block == null) return;
            block.SetFloat(WindPhaseOffset, value);
        }

        public static void SetWiggle(
            MaterialPropertyBlock block,
            bool enabled,
            float amplitudeDegrees,
            float frequency,
            float speed)
        {
            SetWiggle(block, enabled, amplitudeDegrees, frequency, speed, Vector2.up);
        }

        public static void SetWiggle(
            MaterialPropertyBlock block,
            bool enabled,
            float amplitudeDegrees,
            float frequency,
            float speed,
            Vector2 phaseDirection)
        {
            if (block == null) return;
            Vector2 normalized = phaseDirection.sqrMagnitude > 0.000001f
                ? phaseDirection.normalized
                : Vector2.up;
            block.SetFloat(WiggleEnabled, enabled ? 1f : 0f);
            block.SetFloat(WiggleAmplitude, Mathf.Clamp(amplitudeDegrees, 0f, 45f));
            block.SetFloat(WiggleFrequency, Mathf.Max(0f, frequency));
            block.SetVector(WiggleDirection, new Vector4(normalized.x, normalized.y, 0f, 0f));
            block.SetFloat(WiggleSpeed, Mathf.Max(0f, speed));
        }

        public static void SetVibrate(MaterialPropertyBlock block, bool enabled, float amplitude, float speed)
        {
            if (block == null) return;
            block.SetFloat(VibrateEnabled, enabled ? 1f : 0f);
            block.SetFloat(VibrateAmplitude, Mathf.Max(0f, amplitude));
            block.SetFloat(VibrateSpeed, Mathf.Max(0f, speed));
            block.SetVector(VibrateDirection, new Vector4(1f, 0f, 0f, 0f));
        }

        public static void SetVibrate(
            MaterialPropertyBlock block,
            bool enabled,
            float amplitude,
            float speed,
            Vector2 direction)
        {
            SetVibrate(block, enabled, amplitude, speed);
            if (block == null) return;
            Vector2 normalized = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector2.right;
            block.SetVector(VibrateDirection, new Vector4(normalized.x, normalized.y, 0f, 0f));
        }

        public static void SetGlobalWind(Vector2 direction, float strength, float speed)
        {
            Vector2 normalized = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            Shader.SetGlobalVector(GlobalWind, new Vector4(
                normalized.x,
                normalized.y,
                Mathf.Max(0f, strength),
                Mathf.Max(0f, speed)));
            Shader.SetGlobalFloat(GlobalWindValid, 1f);
        }

        public static void ClearGlobalWind()
        {
            Shader.SetGlobalVector(GlobalWind, new Vector4(1f, 0f, 1f, 1f));
            Shader.SetGlobalFloat(GlobalWindValid, 0f);
        }

        public static void SetUnscaledTime(float unscaledTime)
        {
            Shader.SetGlobalFloat(UnscaledTime, unscaledTime);
            Shader.SetGlobalFloat(UnscaledTimeValid, 1f);
        }

        public static void SetTextureLayer(
            MaterialPropertyBlock block,
            ESCompositeTextureLayer layer,
            bool enabled,
            Texture texture,
            Color color,
            Vector2 scale,
            Vector2 offset,
            bool scroll,
            Vector2 scrollSpeed,
            bool sheet,
            int columns,
            int rows,
            float speed,
            int startFrame,
            float edgeClip,
            bool contrast,
            float contrastValue,
            float fade = 1f)
        {
            if (block == null) return;
            bool valid = enabled && texture != null;
            bool first = layer == ESCompositeTextureLayer.层一;
            block.SetFloat(first ? TextureLayer1Enabled : TextureLayer2Enabled, valid ? 1f : 0f);
            if (texture != null) block.SetTexture(first ? TextureLayer1Texture : TextureLayer2Texture, texture);
            block.SetColor(first ? TextureLayer1Color : TextureLayer2Color, color);
            block.SetVector(first ? TextureLayer1Scale : TextureLayer2Scale, new Vector4(scale.x, scale.y, 0f, 0f));
            block.SetVector(first ? TextureLayer1Offset : TextureLayer2Offset, new Vector4(offset.x, offset.y, 0f, 0f));
            block.SetFloat(first ? TextureLayer1ScrollEnabled : TextureLayer2ScrollEnabled, scroll ? 1f : 0f);
            block.SetVector(first ? TextureLayer1ScrollSpeed : TextureLayer2ScrollSpeed, new Vector4(scrollSpeed.x, scrollSpeed.y, 0f, 0f));
            block.SetFloat(first ? TextureLayer1SheetEnabled : TextureLayer2SheetEnabled, sheet ? 1f : 0f);
            block.SetFloat(first ? TextureLayer1Columns : TextureLayer2Columns, Mathf.Clamp(columns, 1, 64));
            block.SetFloat(first ? TextureLayer1Rows : TextureLayer2Rows, Mathf.Clamp(rows, 1, 64));
            block.SetFloat(first ? TextureLayer1Speed : TextureLayer2Speed, speed);
            block.SetFloat(first ? TextureLayer1StartFrame : TextureLayer2StartFrame, Mathf.Max(0, startFrame));
            block.SetFloat(first ? TextureLayer1EdgeClip : TextureLayer2EdgeClip, Mathf.Clamp(edgeClip, 0f, 0.49f));
            block.SetFloat(first ? TextureLayer1ContrastEnabled : TextureLayer2ContrastEnabled, contrast ? 1f : 0f);
            block.SetFloat(first ? TextureLayer1Contrast : TextureLayer2Contrast, Mathf.Clamp(contrastValue, 0.01f, 4f));
            block.SetFloat(first ? TextureLayer1Fade : TextureLayer2Fade, Mathf.Clamp01(fade));
        }

        public static void SetRecolorRGB(
            MaterialPropertyBlock block,
            bool enabled,
            Color red,
            Color green,
            Color blue,
            float strength = 1f,
            Texture mask = null,
            ESCompositeTextureChannel maskChannel = ESCompositeTextureChannel.透明)
        {
            if (block == null) return;
            block.SetFloat(RecolorRGBEnabled, enabled ? 1f : 0f);
            block.SetColor(RecolorRed, red);
            block.SetColor(RecolorGreen, green);
            block.SetColor(RecolorBlue, blue);
            block.SetFloat(RecolorRGBStrength, Mathf.Clamp01(strength));
            block.SetFloat(RecolorRGBMaskEnabled, mask != null ? 1f : 0f);
            if (mask != null) block.SetTexture(RecolorRGBMask, mask);
            block.SetFloat(RecolorRGBMaskChannel, (float)maskChannel);
        }

        public static void SetRecolorRGBYCP(
            MaterialPropertyBlock block,
            bool enabled,
            Color red,
            Color green,
            Color blue,
            Color yellow,
            Color cyan,
            Color purple,
            float strength = 1f,
            Texture mask = null,
            ESCompositeTextureChannel maskChannel = ESCompositeTextureChannel.透明)
        {
            if (block == null) return;
            block.SetFloat(RecolorRGBYCPEnabled, enabled ? 1f : 0f);
            block.SetColor(RecolorRGBYCPRed, red);
            block.SetColor(RecolorRGBYCPGreen, green);
            block.SetColor(RecolorRGBYCPBlue, blue);
            block.SetColor(RecolorRGBYCPYellow, yellow);
            block.SetColor(RecolorRGBYCPCyan, cyan);
            block.SetColor(RecolorRGBYCPPurple, purple);
            block.SetFloat(RecolorRGBYCPStrength, Mathf.Clamp01(strength));
            block.SetFloat(RecolorRGBYCPMaskEnabled, mask != null ? 1f : 0f);
            if (mask != null) block.SetTexture(RecolorRGBYCPMask, mask);
            block.SetFloat(RecolorRGBYCPMaskChannel, (float)maskChannel);
        }

        public static void SetESNativeColorTint(
            MaterialPropertyBlock block,
            bool addEnabled,
            Color addColor,
            float addFade,
            bool addContrastEnabled,
            float addContrast,
            Texture addMask,
            bool strongEnabled,
            Color strongColor,
            float strongFade,
            bool strongContrastEnabled,
            float strongContrast,
            Texture strongMask)
        {
            if (block == null) return;
            block.SetFloat(AddColorEnabled, addEnabled ? 1f : 0f);
            block.SetColor(AddColor, addColor);
            block.SetFloat(AddColorFade, Mathf.Clamp01(addFade));
            block.SetFloat(AddColorContrastEnabled, addContrastEnabled ? 1f : 0f);
            block.SetFloat(AddColorContrast, Mathf.Max(addContrast, 0.001f));
            block.SetFloat(AddColorMaskEnabled, addMask != null ? 1f : 0f);
            if (addMask != null) block.SetTexture(AddColorMask, addMask);

            block.SetFloat(StrongTintEnabled, strongEnabled ? 1f : 0f);
            block.SetColor(StrongTint, strongColor);
            block.SetFloat(StrongTintFade, Mathf.Clamp01(strongFade));
            block.SetFloat(StrongTintContrastEnabled, strongContrastEnabled ? 1f : 0f);
            block.SetFloat(StrongTintContrast, Mathf.Max(strongContrast, 0.001f));
            block.SetFloat(StrongTintMaskEnabled, strongMask != null ? 1f : 0f);
            if (strongMask != null) block.SetTexture(StrongTintMask, strongMask);
        }

        public static void SetESNativeExactContract(
            MaterialPropertyBlock block,
            bool enabled,
            Texture shineMask = null)
        {
            if (block == null) return;
            block.SetFloat(ESNativeExactContract, enabled ? 1f : 0f);
            block.SetFloat(ShineMaskEnabled, shineMask != null ? 1f : 0f);
            if (shineMask != null) block.SetTexture(ShineMask, shineMask);
        }

        public static void SetESNativeStatusContract(
            MaterialPropertyBlock block,
            bool enabled,
            Texture shineMask = null)
        {
            SetESNativeExactContract(block, enabled, shineMask);
        }

        private static Vector4 ToVector4(Vector2 value)
        {
            return new Vector4(value.x, value.y, 0f, 0f);
        }

        internal static void ApplyQuality(Material material, int propertyId, ESCompositeQualityTier quality)
        {
            if (material == null) return;
            int tier = Mathf.Clamp((int)quality, 0, 2);
            material.SetFloat(propertyId, tier);
            SetKeyword(material, QualityStandardKeyword, tier == 1);
            SetKeyword(material, QualityHighKeyword, tier == 2);
        }

        internal static void ApplyHighDefaultQuality(Material material, int propertyId, ESCompositeQualityTier quality)
        {
            if (material == null) return;
            int tier = Mathf.Clamp((int)quality, 0, 2);
            material.SetFloat(propertyId, tier);
            SetKeyword(material, QualityBasicKeyword, tier == 0);
            SetKeyword(material, QualityStandardKeyword, tier == 1);
            SetKeyword(material, QualityHighKeyword, false);
        }

        internal static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (material == null) return;
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        internal static bool RefreshSpriteResourceProfile(
            Material material,
            int resourceProfileId,
            string[] surfaceResourceSwitches)
        {
            if (material == null || !material.HasProperty(resourceProfileId)) return false;

            bool optimized = material.GetFloat(resourceProfileId) > 0.5f;
            int mask = 0;
            if (optimized)
            {
                if (IsMaterialSwitchEnabled(material, "_EnableUVDistort")) mask |= 1;
                if (GetMaterialFloat(material, "_FadeMode") > 0.5f
                    || IsMaterialSwitchEnabled(material, "_EnableCustomFade")
                    || IsAnyMaterialSwitchEnabled(material, EsNativeFadeResourceSwitches)) mask |= 2;
                if (IsAnyMaterialSwitchEnabled(material, surfaceResourceSwitches)) mask |= 4;
                if (IsMaterialSwitchEnabled(material, "_EnablePalette")
                    || IsMaterialSwitchEnabled(material, "_EnableTextureLayer1")
                    || IsMaterialSwitchEnabled(material, "_EnableTextureLayer2")) mask |= 8;
            }

            bool changed = false;
            for (int i = 0; i < 16; i++)
            {
                string keyword = "_ES_SPRITE_RESOURCE_MASK_" + i;
                bool enabled = optimized && i == mask;
                if (material.IsKeywordEnabled(keyword) == enabled) continue;
                SetKeyword(material, keyword, enabled);
                changed = true;
            }
            return changed;
        }

        private static readonly string[] EsNativeFadeResourceSwitches =
        {
            "_EnableFullAlphaDissolve",
            "_EnableSourceAlphaDissolve",
            "_EnableSourceGlowDissolve",
            "_EnableDirectionalAlphaFade",
            "_EnableDirectionalGlowFade",
            "_EnableDirectionalDistortion"
        };

        private static bool IsAnyMaterialSwitchEnabled(Material material, string[] propertyNames)
        {
            if (propertyNames == null) return false;
            for (int i = 0; i < propertyNames.Length; i++)
                if (IsMaterialSwitchEnabled(material, propertyNames[i])) return true;
            return false;
        }

        private static bool IsMaterialSwitchEnabled(Material material, string propertyName)
        {
            return GetMaterialFloat(material, propertyName) > 0.5f;
        }

        private static float GetMaterialFloat(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) ? material.GetFloat(propertyName) : 0f;
        }
    }

    public enum ES3DCompositeDissolveMode
    {
        无 = 0,
        噪声溶解 = 1,
        距离溶解 = 2
    }

    public enum ES3DLitResourceProfile
    {
        动态完整 = 0,
        材质优化 = 1
    }

    public enum ESSpriteCompositeResourceProfile
    {
        动态完整 = 0,
        材质优化 = 1
    }

    public enum ES3DVFXDissolveMode
    {
        无 = 0,
        溶解 = 1,
        溶解加边缘光 = 2
    }

    public enum ES3DVFXSequencePlaybackMode
    {
        手动帧 = 0,
        时间播放 = 1,
        顶点流帧号 = 2
    }

    public enum ES3DVFXBlendMode
    {
        透明混合 = 0,
        叠加 = 1,
        预乘透明 = 2,
        正片叠底 = 3
    }

    public enum ES3DVFXDepthWriteMode
    {
        关闭 = 0,
        开启 = 1
    }

    public enum ES3DVFXDepthTestMode
    {
        禁用 = 0,
        从不 = 1,
        小于 = 2,
        等于 = 3,
        小于等于 = 4,
        大于 = 5,
        不等于 = 6,
        大于等于 = 7,
        始终 = 8
    }

    public enum ES3DVFXCullMode
    {
        双面 = 0,
        剔除正面 = 1,
        剔除背面 = 2
    }

    public enum ESUICompositeEffectMode
    {
        无 = 0,
        全息 = 1,
        故障 = 2,
        全息与故障 = 3
    }

    public enum ESUICompositeBlendMode
    {
        透明混合 = 0,
        叠加 = 1,
        预乘透明 = 2,
        正片叠底 = 3
    }

    public enum ESCompositeQualityTier
    {
        基础 = 0,
        标准 = 1,
        高质量 = 2
    }

    public enum ESCompositeVertexColorMask
    {
        无 = 0,
        红色通道 = 1,
        绿色通道 = 2,
        蓝色通道 = 3,
        透明通道 = 4
    }

    public static class ES2DCompositeURPProperties
    {
        public const string ShaderName = "ES/2D/Composite URP";
        private static readonly string[] SurfaceResourceSwitches =
        {
            "_ESNativeStatusContract",
            "_EnableDistortion", "_EnableFrozen", "_EnableBurn", "_EnablePoison",
            "_EnableSmoke", "_EnableFlame", "_EnableInkSpread", "_EnableCamouflage",
            "_EnableMetal", "_EnableEnchanted", "_EnableFullGlowDissolve", "_EnableFullDistortion",
            "_EnableAddColor", "_EnableStrongTint",
            "_EnableRecolorRGB", "_EnableRecolorRGBYCP", "_EnableAddHue", "_EnableSineGlow"
        };
        public static readonly int UVTransformEnabled = ESCompositeURPProperties.UVTransformEnabled;
        public static readonly int UVPivot = ESCompositeURPProperties.UVPivot;
        public static readonly int UVScale = ESCompositeURPProperties.UVScale;
        public static readonly int UVOffset = ESCompositeURPProperties.UVOffset;
        public static readonly int UVRotation = ESCompositeURPProperties.UVRotation;
        public static readonly int UVRotationSpeed = ESCompositeURPProperties.UVRotationSpeed;
        public static readonly int UVDistortEnabled = ESCompositeURPProperties.UVDistortEnabled;
        public static readonly int UVDistortFrequency = ESCompositeURPProperties.UVDistortFrequency;
        public static readonly int UVDistortSpeed = ESCompositeURPProperties.UVDistortSpeed;
        public static readonly int UVDistortAmount = ESCompositeURPProperties.UVDistortAmount;
        public static readonly int UVDistortNoiseTexture = ESCompositeURPProperties.UVDistortNoiseTexture;
        public static readonly int UVDistortFrom = ESCompositeURPProperties.UVDistortFrom;
        public static readonly int UVDistortTo = ESCompositeURPProperties.UVDistortTo;
        public static readonly int UVDistortFade = ESCompositeURPProperties.UVDistortFade;
        public static readonly int UVDistortMaskEnabled = ESCompositeURPProperties.UVDistortMaskEnabled;
        public static readonly int UVDistortMask = ESCompositeURPProperties.UVDistortMask;
        public static readonly int UVDistortMaskChannel = ESCompositeURPProperties.UVDistortMaskChannel;
        public static readonly int SplitToningEnabled = ESCompositeURPProperties.SplitToningEnabled;
        public static readonly int SplitToneShadows = ESCompositeURPProperties.SplitToneShadows;
        public static readonly int SplitToneHighlights = ESCompositeURPProperties.SplitToneHighlights;
        public static readonly int SplitToneBalance = ESCompositeURPProperties.SplitToneBalance;
        public static readonly int SplitToneStrength = ESCompositeURPProperties.SplitToneStrength;
        public static readonly int SplitToneContrast = ESCompositeURPProperties.SplitToneContrast;
        public static readonly int SplitToneShift = ESCompositeURPProperties.SplitToneShift;
        public static readonly int SpriteShadowEnabled = ESCompositeURPProperties.SpriteShadowEnabled;
        public static readonly int SpriteShadowFade = ESCompositeURPProperties.SpriteShadowFade;
        public static readonly int SpriteShadowOffset = ESCompositeURPProperties.SpriteShadowOffset;
        public static readonly int SpriteShadowColor = ESCompositeURPProperties.SpriteShadowColor;
        public static readonly int BlackTintEnabled = ESCompositeURPProperties.BlackTintEnabled;
        public static readonly int BlackTintFade = ESCompositeURPProperties.BlackTintFade;
        public static readonly int BlackTintColor = ESCompositeURPProperties.BlackTintColor;
        public static readonly int BlackTintPower = ESCompositeURPProperties.BlackTintPower;
        public static readonly int InkSpreadEnabled = ESCompositeURPProperties.InkSpreadEnabled;
        public static readonly int InkSpreadFade = ESCompositeURPProperties.InkSpreadFade;
        public static readonly int InkSpreadColor = ESCompositeURPProperties.InkSpreadColor;
        public static readonly int InkSpreadContrast = ESCompositeURPProperties.InkSpreadContrast;
        public static readonly int InkSpreadDistance = ESCompositeURPProperties.InkSpreadDistance;
        public static readonly int InkSpreadPosition = ESCompositeURPProperties.InkSpreadPosition;
        public static readonly int InkSpreadWidth = ESCompositeURPProperties.InkSpreadWidth;
        public static readonly int InkSpreadNoiseScale = ESCompositeURPProperties.InkSpreadNoiseScale;
        public static readonly int InkSpreadNoiseFactor = ESCompositeURPProperties.InkSpreadNoiseFactor;
        public static readonly int ShiftHueEnabled = ESCompositeURPProperties.ShiftHueEnabled;
        public static readonly int ShiftHueSpeed = ESCompositeURPProperties.ShiftHueSpeed;
        public static readonly int AddHueEnabled = ESCompositeURPProperties.AddHueEnabled;
        public static readonly int AddHueFade = ESCompositeURPProperties.AddHueFade;
        public static readonly int AddHueSpeed = ESCompositeURPProperties.AddHueSpeed;
        public static readonly int AddHueBrightness = ESCompositeURPProperties.AddHueBrightness;
        public static readonly int AddHueSaturation = ESCompositeURPProperties.AddHueSaturation;
        public static readonly int AddHueContrast = ESCompositeURPProperties.AddHueContrast;
        public static readonly int AddHueMaskEnabled = ESCompositeURPProperties.AddHueMaskEnabled;
        public static readonly int AddHueMask = ESCompositeURPProperties.AddHueMask;
        public static readonly int AddHueMaskScaleOffset = ESCompositeURPProperties.AddHueMaskScaleOffset;
        public static readonly int SineGlowEnabled = ESCompositeURPProperties.SineGlowEnabled;
        public static readonly int SineGlowFade = ESCompositeURPProperties.SineGlowFade;
        public static readonly int SineGlowColor = ESCompositeURPProperties.SineGlowColor;
        public static readonly int SineGlowContrast = ESCompositeURPProperties.SineGlowContrast;
        public static readonly int SineGlowFrequency = ESCompositeURPProperties.SineGlowFrequency;
        public static readonly int SineGlowMin = ESCompositeURPProperties.SineGlowMin;
        public static readonly int SineGlowMax = ESCompositeURPProperties.SineGlowMax;
        public static readonly int SineGlowMaskEnabled = ESCompositeURPProperties.SineGlowMaskEnabled;
        public static readonly int SineGlowMask = ESCompositeURPProperties.SineGlowMask;
        public static readonly int SineGlowMaskScaleOffset = ESCompositeURPProperties.SineGlowMaskScaleOffset;
        public static readonly int SqueezeEnabled = ESCompositeURPProperties.SqueezeEnabled;
        public static readonly int SqueezeFade = ESCompositeURPProperties.SqueezeFade;
        public static readonly int SqueezeScale = ESCompositeURPProperties.SqueezeScale;
        public static readonly int SqueezePower = ESCompositeURPProperties.SqueezePower;
        public static readonly int SqueezeCenter = ESCompositeURPProperties.SqueezeCenter;
        public static readonly int SineRotateEnabled = ESCompositeURPProperties.SineRotateEnabled;
        public static readonly int SineRotateFade = ESCompositeURPProperties.SineRotateFade;
        public static readonly int SineRotateAngle = ESCompositeURPProperties.SineRotateAngle;
        public static readonly int SineRotateFrequency = ESCompositeURPProperties.SineRotateFrequency;
        public static readonly int SineRotatePivot = ESCompositeURPProperties.SineRotatePivot;
        public static readonly int SineMoveEnabled = ESCompositeURPProperties.SineMoveEnabled;
        public static readonly int SineMoveFade = ESCompositeURPProperties.SineMoveFade;
        public static readonly int SineMoveOffset = ESCompositeURPProperties.SineMoveOffset;
        public static readonly int SineMoveFrequency = ESCompositeURPProperties.SineMoveFrequency;
        public static readonly int SineScaleEnabled = ESCompositeURPProperties.SineScaleEnabled;
        public static readonly int SineScaleFrequency = ESCompositeURPProperties.SineScaleFrequency;
        public static readonly int SineScaleFactor = ESCompositeURPProperties.SineScaleFactor;
        public static readonly int CustomFadeEnabled = ESCompositeURPProperties.CustomFadeEnabled;
        public static readonly int CustomFadeMask = ESCompositeURPProperties.CustomFadeMask;
        public static readonly int CustomFadeSmoothness = ESCompositeURPProperties.CustomFadeSmoothness;
        public static readonly int CustomFadeNoiseScale = ESCompositeURPProperties.CustomFadeNoiseScale;
        public static readonly int CustomFadeNoiseFactor = ESCompositeURPProperties.CustomFadeNoiseFactor;
        public static readonly int CustomFadeAlpha = ESCompositeURPProperties.CustomFadeAlpha;
        public static readonly int FullGlowDissolveEnabled = ESCompositeURPProperties.FullGlowDissolveEnabled;
        public static readonly int FullGlowDissolveFade = ESCompositeURPProperties.FullGlowDissolveFade;
        public static readonly int FullGlowDissolveWidth = ESCompositeURPProperties.FullGlowDissolveWidth;
        public static readonly int FullGlowDissolveEdgeColor = ESCompositeURPProperties.FullGlowDissolveEdgeColor;
        public static readonly int FullGlowDissolveNoiseScale = ESCompositeURPProperties.FullGlowDissolveNoiseScale;
        public static readonly int CamouflageEnabled = ESCompositeURPProperties.CamouflageEnabled;
        public static readonly int CamouflageFade = ESCompositeURPProperties.CamouflageFade;
        public static readonly int CamouflageBaseColor = ESCompositeURPProperties.CamouflageBaseColor;
        public static readonly int CamouflageContrast = ESCompositeURPProperties.CamouflageContrast;
        public static readonly int CamouflageColorA = ESCompositeURPProperties.CamouflageColorA;
        public static readonly int CamouflageDensityA = ESCompositeURPProperties.CamouflageDensityA;
        public static readonly int CamouflageSmoothnessA = ESCompositeURPProperties.CamouflageSmoothnessA;
        public static readonly int CamouflageNoiseScaleA = ESCompositeURPProperties.CamouflageNoiseScaleA;
        public static readonly int CamouflageColorB = ESCompositeURPProperties.CamouflageColorB;
        public static readonly int CamouflageDensityB = ESCompositeURPProperties.CamouflageDensityB;
        public static readonly int CamouflageSmoothnessB = ESCompositeURPProperties.CamouflageSmoothnessB;
        public static readonly int CamouflageNoiseScaleB = ESCompositeURPProperties.CamouflageNoiseScaleB;
        public static readonly int CamouflageAnimationEnabled = ESCompositeURPProperties.CamouflageAnimationEnabled;
        public static readonly int CamouflageDistortionSpeed = ESCompositeURPProperties.CamouflageDistortionSpeed;
        public static readonly int CamouflageDistortionIntensity = ESCompositeURPProperties.CamouflageDistortionIntensity;
        public static readonly int CamouflageDistortionScale = ESCompositeURPProperties.CamouflageDistortionScale;
        public static readonly int MetalEnabled = ESCompositeURPProperties.MetalEnabled;
        public static readonly int MetalFade = ESCompositeURPProperties.MetalFade;
        public static readonly int MetalColor = ESCompositeURPProperties.MetalColor;
        public static readonly int MetalContrast = ESCompositeURPProperties.MetalContrast;
        public static readonly int MetalHighlightColor = ESCompositeURPProperties.MetalHighlightColor;
        public static readonly int MetalHighlightDensity = ESCompositeURPProperties.MetalHighlightDensity;
        public static readonly int MetalHighlightContrast = ESCompositeURPProperties.MetalHighlightContrast;
        public static readonly int MetalNoiseScale = ESCompositeURPProperties.MetalNoiseScale;
        public static readonly int MetalNoiseSpeed = ESCompositeURPProperties.MetalNoiseSpeed;
        public static readonly int MetalNoiseDistortionScale = ESCompositeURPProperties.MetalNoiseDistortionScale;
        public static readonly int MetalNoiseDistortionSpeed = ESCompositeURPProperties.MetalNoiseDistortionSpeed;
        public static readonly int MetalNoiseDistortion = ESCompositeURPProperties.MetalNoiseDistortion;
        public static readonly int MetalMaskEnabled = ESCompositeURPProperties.MetalMaskEnabled;
        public static readonly int MetalMask = ESCompositeURPProperties.MetalMask;
        public static readonly int EnchantedEnabled = ESCompositeURPProperties.EnchantedEnabled;
        public static readonly int EnchantedFade = ESCompositeURPProperties.EnchantedFade;
        public static readonly int EnchantedSpeed = ESCompositeURPProperties.EnchantedSpeed;
        public static readonly int EnchantedScale = ESCompositeURPProperties.EnchantedScale;
        public static readonly int EnchantedBrightness = ESCompositeURPProperties.EnchantedBrightness;
        public static readonly int EnchantedContrast = ESCompositeURPProperties.EnchantedContrast;
        public static readonly int EnchantedReduce = ESCompositeURPProperties.EnchantedReduce;
        public static readonly int EnchantedRainbowEnabled = ESCompositeURPProperties.EnchantedRainbowEnabled;
        public static readonly int EnchantedRainbowSpeed = ESCompositeURPProperties.EnchantedRainbowSpeed;
        public static readonly int EnchantedRainbowDensity = ESCompositeURPProperties.EnchantedRainbowDensity;
        public static readonly int EnchantedRainbowSaturation = ESCompositeURPProperties.EnchantedRainbowSaturation;
        public static readonly int EnchantedLowColor = ESCompositeURPProperties.EnchantedLowColor;
        public static readonly int EnchantedHighColor = ESCompositeURPProperties.EnchantedHighColor;
        public static readonly int EnchantedLerpEnabled = ESCompositeURPProperties.EnchantedLerpEnabled;
        public static readonly int ShiftingEnabled = ESCompositeURPProperties.ShiftingEnabled;
        public static readonly int ShiftingFade = ESCompositeURPProperties.ShiftingFade;
        public static readonly int ShiftingSpeed = ESCompositeURPProperties.ShiftingSpeed;
        public static readonly int ShiftingDensity = ESCompositeURPProperties.ShiftingDensity;
        public static readonly int ShiftingBrightness = ESCompositeURPProperties.ShiftingBrightness;
        public static readonly int ShiftingContrast = ESCompositeURPProperties.ShiftingContrast;
        public static readonly int ShiftingRainbowEnabled = ESCompositeURPProperties.ShiftingRainbowEnabled;
        public static readonly int ShiftingSaturation = ESCompositeURPProperties.ShiftingSaturation;
        public static readonly int ShiftingColorA = ESCompositeURPProperties.ShiftingColorA;
        public static readonly int ShiftingColorB = ESCompositeURPProperties.ShiftingColorB;
        public const string Lit3DShaderName = "ES/3D/Lit Composite URP";
        public const string Vfx3DShaderName = "ES/3D/VFX Composite URP";
        public static readonly int AnimationMode = Shader.PropertyToID("_AnimationMode");
        public static readonly int FadeMode = ESCompositeURPProperties.FadeMode;
        public static readonly int FadeProgress = ESCompositeURPProperties.FadeProgress;
        public static readonly int FadePosition = ESCompositeURPProperties.FadePosition;
        public static readonly int FadeRotation = ESCompositeURPProperties.FadeRotation;
        public static readonly int FadeWidth = ESCompositeURPProperties.FadeWidth;
        public static readonly int FadeInvert = ESCompositeURPProperties.FadeInvert;
        public static readonly int FadeNoiseFactor = ESCompositeURPProperties.FadeNoiseFactor;
        public static readonly int FadeNoiseScale = ESCompositeURPProperties.FadeNoiseScale;
        public static readonly int FadeNoiseSpeed = ESCompositeURPProperties.FadeNoiseSpeed;
        public static readonly int FadeNoiseTexture = ESCompositeURPProperties.FadeNoiseTexture;
        public static readonly int FadeMaskTexture = ESCompositeURPProperties.FadeMaskTexture;
        public static readonly int FadeEdgeColor = ESCompositeURPProperties.FadeEdgeColor;
        public static readonly int FadeEdgeWidth = ESCompositeURPProperties.FadeEdgeWidth;
        public static readonly int FadeEdgeIntensity = ESCompositeURPProperties.FadeEdgeIntensity;
        public static readonly int FadeDistortionStrength = ESCompositeURPProperties.FadeDistortionStrength;
        public static readonly int CoordinateMode = Shader.PropertyToID("_CoordinateMode");
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int TimeFPSEnabled = ESCompositeURPProperties.TimeFPSEnabled;
        public static readonly int TimeFPS = ESCompositeURPProperties.TimeFPS;
        public static readonly int TimeFrequencyEnabled = ESCompositeURPProperties.TimeFrequencyEnabled;
        public static readonly int TimeFrequency = ESCompositeURPProperties.TimeFrequency;
        public static readonly int TimeRange = ESCompositeURPProperties.TimeRange;
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");
        public static readonly int SpriteUVRect = Shader.PropertyToID("_SpriteUVRect");
        public static readonly int SpriteUVTransformX = Shader.PropertyToID("_SpriteUVTransformX");
        public static readonly int SpriteUVTransformY = Shader.PropertyToID("_SpriteUVTransformY");
        public static readonly int SpriteUVTransformValid = Shader.PropertyToID("_SpriteUVTransformValid");
        public static readonly int SequenceColumns = Shader.PropertyToID("_SequenceColumns");
        public static readonly int SequenceRows = Shader.PropertyToID("_SequenceRows");
        public static readonly int SequenceFrame = Shader.PropertyToID("_SequenceFrame");
        public static readonly int SequenceSpeed = Shader.PropertyToID("_SequenceSpeed");
        public static readonly int GlowIntensity = Shader.PropertyToID("_GlowIntensity");
        public static readonly int GlowContrast = ESCompositeURPProperties.GlowContrast;
        public static readonly int GlowFade = ESCompositeURPProperties.GlowFade;
        public static readonly int ShineEnabled = Shader.PropertyToID("_EnableShine");
        public static readonly int ShineColor = Shader.PropertyToID("_ShineColor");
        public static readonly int ShineSpeed = Shader.PropertyToID("_ShineSpeed");
        public static readonly int ShineWidth = Shader.PropertyToID("_ShineWidth");
        public static readonly int ShineSpace = ESCompositeURPProperties.ShineSpace;
        public static readonly int ShineDirection = Shader.PropertyToID("_ShineDirection");
        public static readonly int ShineAngle = Shader.PropertyToID("_ShineAngle");
        public static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
        public static readonly int SparkleEnabled = Shader.PropertyToID("_EnableSparkle");
        public static readonly int SparkleColor = Shader.PropertyToID("_SparkleColor");
        public static readonly int SparkleScale = Shader.PropertyToID("_SparkleScale");
        public static readonly int SparkleSpeed = Shader.PropertyToID("_SparkleSpeed");
        public static readonly int SparkleDensity = Shader.PropertyToID("_SparkleDensity");
        public static readonly int SparkleSharpness = Shader.PropertyToID("_SparkleSharpness");
        public static readonly int SparkleIntensity = Shader.PropertyToID("_SparkleIntensity");
        public static readonly int FlowEnabled = Shader.PropertyToID("_EnableFlow");
        public static readonly int FlowSpeed = Shader.PropertyToID("_FlowSpeed");
        public static readonly int FlowStrength = Shader.PropertyToID("_FlowStrength");
        public static readonly int ChromaticEnabled = Shader.PropertyToID("_EnableChromatic");
        public static readonly int ChromaticOffset = Shader.PropertyToID("_ChromaticOffset");
        public static readonly int ChromaticIntensity = Shader.PropertyToID("_ChromaticIntensity");
        public static readonly int ChromaticEdgeOnly = Shader.PropertyToID("_ChromaticEdgeOnly");
        public static readonly int ChromaticAngle = Shader.PropertyToID("_ChromaticAngle");
        public static readonly int BlurEnabled = ESCompositeURPProperties.BlurEnabled;
        public static readonly int BlurRadius = ESCompositeURPProperties.BlurRadius;
        public static readonly int BlurIntensity = ESCompositeURPProperties.BlurIntensity;
        public static readonly int BlurMode = ESCompositeURPProperties.BlurMode;
        public static readonly int SharpenEnabled = ESCompositeURPProperties.SharpenEnabled;
        public static readonly int SharpenAmount = ESCompositeURPProperties.SharpenAmount;
        public static readonly int SharpenRadius = ESCompositeURPProperties.SharpenRadius;
        public static readonly int SharpenThreshold = ESCompositeURPProperties.SharpenThreshold;
        public static readonly int SharpenFade = ESCompositeURPProperties.SharpenFade;
        public static readonly int TilingMode = ESCompositeURPProperties.TilingMode;
        public static readonly int WorldTilingScale = ESCompositeURPProperties.WorldTilingScale;
        public static readonly int WorldTilingOffset = ESCompositeURPProperties.WorldTilingOffset;
        public static readonly int WorldTilingPixelsPerUnit = ESCompositeURPProperties.WorldTilingPixelsPerUnit;
        public static readonly int ScreenTilingScale = ESCompositeURPProperties.ScreenTilingScale;
        public static readonly int ScreenTilingOffset = ESCompositeURPProperties.ScreenTilingOffset;
        public static readonly int ScreenTilingPixelsPerUnit = ESCompositeURPProperties.ScreenTilingPixelsPerUnit;
        public static readonly int SmoothPixelArtEnabled = ESCompositeURPProperties.SmoothPixelArtEnabled;
        public static readonly int SmoothPixelStrength = ESCompositeURPProperties.SmoothPixelStrength;
        public static readonly int CheckerboardEnabled = ESCompositeURPProperties.CheckerboardEnabled;
        public static readonly int CheckerboardDarken = ESCompositeURPProperties.CheckerboardDarken;
        public static readonly int CheckerboardTiling = ESCompositeURPProperties.CheckerboardTiling;
        public static readonly int UberNoiseTexture = ESCompositeURPProperties.UberNoiseTexture;
        public static readonly int ESNativeStatusContract = ESCompositeURPProperties.ESNativeStatusContract;
        public static readonly int ESNativeExactContract = ESCompositeURPProperties.ESNativeExactContract;
        public static readonly int FlameEnabled = ESCompositeURPProperties.FlameEnabled;
        public static readonly int FlameBrightness = ESCompositeURPProperties.FlameBrightness;
        public static readonly int FlameSmooth = ESCompositeURPProperties.FlameSmooth;
        public static readonly int FlameRadius = ESCompositeURPProperties.FlameRadius;
        public static readonly int FlameSpeed = ESCompositeURPProperties.FlameSpeed;
        public static readonly int FlameNoiseFactor = ESCompositeURPProperties.FlameNoiseFactor;
        public static readonly int FlameNoiseHeightFactor = ESCompositeURPProperties.FlameNoiseHeightFactor;
        public static readonly int FlameNoiseScale = ESCompositeURPProperties.FlameNoiseScale;
        public static readonly int FlameCenter = ESCompositeURPProperties.FlameCenter;
        public static readonly int FlameDirection = ESCompositeURPProperties.FlameDirection;
        public static readonly int SmokeEnabled = ESCompositeURPProperties.SmokeEnabled;
        public static readonly int SmokeAlpha = ESCompositeURPProperties.SmokeAlpha;
        public static readonly int SmokeSmoothness = ESCompositeURPProperties.SmokeSmoothness;
        public static readonly int SmokeNoiseScale = ESCompositeURPProperties.SmokeNoiseScale;
        public static readonly int SmokeNoiseFactor = ESCompositeURPProperties.SmokeNoiseFactor;
        public static readonly int SmokeDarkEdge = ESCompositeURPProperties.SmokeDarkEdge;
        public static readonly int SmokeVertexSeed = ESCompositeURPProperties.SmokeVertexSeed;
        public static readonly int SmokeSpeed = ESCompositeURPProperties.SmokeSpeed;
        public static readonly int SquishDirection = ESCompositeURPProperties.SquishDirection;
        public static readonly int VibrateDirection = ESCompositeURPProperties.VibrateDirection;
        public static readonly int PixelateEnabled = Shader.PropertyToID("_EnablePixelate");
        public static readonly int PixelateCells = Shader.PropertyToID("_PixelateCells");
        public static readonly int PixelateStrength = Shader.PropertyToID("_PixelateStrength");
        public static readonly int PaletteEnabled = Shader.PropertyToID("_EnablePalette");
        public static readonly int PaletteTexture = Shader.PropertyToID("_PaletteTex");
        public static readonly int PaletteRow = Shader.PropertyToID("_PaletteRow");
        public static readonly int PaletteStrength = Shader.PropertyToID("_PaletteStrength");
        public static readonly int HalftoneEnabled = Shader.PropertyToID("_EnableHalftone");
        public static readonly int HalftoneScale = Shader.PropertyToID("_HalftoneScale");
        public static readonly int HalftoneAngle = Shader.PropertyToID("_HalftoneAngle");
        public static readonly int HalftoneStrength = Shader.PropertyToID("_HalftoneStrength");
        public static readonly int HalftonePosition = ESCompositeURPProperties.HalftonePosition;
        public static readonly int HalftoneFade = ESCompositeURPProperties.HalftoneFade;
        public static readonly int HalftoneFadeWidth = ESCompositeURPProperties.HalftoneFadeWidth;
        public static readonly int HalftoneInvert = ESCompositeURPProperties.HalftoneInvert;
        public static readonly int HalftoneAlphaPattern = ESCompositeURPProperties.HalftoneAlphaPattern;
        public static readonly int QualityTier = Shader.PropertyToID("_QualityTier");
        public static readonly int ResourceProfile = Shader.PropertyToID("_ResourceProfile");
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int MaskTexture = Shader.PropertyToID("_MaskTex");
        public static readonly int NormalMap = Shader.PropertyToID("_NormalMap");
        public static readonly int NormalScale = Shader.PropertyToID("_NormalScale");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int VertexColorStrength = Shader.PropertyToID("_VertexColorStrength");
        public static readonly int DistortionEnabled = ESCompositeURPProperties.LegacyDistortionEnabled;
        public static readonly int NoiseTexture = ESCompositeURPProperties.NoiseTexture;
        public static readonly int NoiseScale = ESCompositeURPProperties.NoiseScale;
        public static readonly int NoiseSpeed = ESCompositeURPProperties.NoiseSpeed;
        public static readonly int DistortionStrength = Shader.PropertyToID("_DistortionStrength");
        public static readonly int DistortionDirection = ESCompositeURPProperties.DistortionDirection;
        public static readonly int AlphaClip = ESCompositeURPProperties.AlphaClipEnabled;
        public static readonly int Cutoff = ESCompositeURPProperties.Cutoff;
        public static readonly int InnerOutlineEnabled = ESCompositeURPProperties.InnerOutlineEnabled;
        public static readonly int InnerOutlineColor = ESCompositeURPProperties.InnerOutlineColor;
        public static readonly int InnerOutlineWidth = ESCompositeURPProperties.InnerOutlineWidth;
        public static readonly int InnerOutlineFade = ESCompositeURPProperties.InnerOutlineFade;
        public static readonly int InnerOutlineDistortionEnabled = ESCompositeURPProperties.InnerOutlineDistortionEnabled;
        public static readonly int InnerOutlineDistortionIntensity = ESCompositeURPProperties.InnerOutlineDistortionIntensity;
        public static readonly int InnerOutlineNoiseScale = ESCompositeURPProperties.InnerOutlineNoiseScale;
        public static readonly int InnerOutlineNoiseSpeed = ESCompositeURPProperties.InnerOutlineNoiseSpeed;
        public static readonly int InnerOutlineTextureEnabled = ESCompositeURPProperties.InnerOutlineTextureEnabled;
        public static readonly int InnerOutlineTintTexture = ESCompositeURPProperties.InnerOutlineTintTexture;
        public static readonly int InnerOutlineTextureSpeed = ESCompositeURPProperties.InnerOutlineTextureSpeed;
        public static readonly int InnerOutlineOnly = ESCompositeURPProperties.InnerOutlineOnly;
        public static readonly int OuterOutlineEnabled = ESCompositeURPProperties.OuterOutlineEnabled;
        public static readonly int OuterOutlineColor = ESCompositeURPProperties.OuterOutlineColor;
        public static readonly int OuterOutlineWidth = ESCompositeURPProperties.OuterOutlineWidth;
        public static readonly int OuterOutlineFade = ESCompositeURPProperties.OuterOutlineFade;
        public static readonly int OuterOutlineDistortionEnabled = ESCompositeURPProperties.OuterOutlineDistortionEnabled;
        public static readonly int OuterOutlineDistortionIntensity = ESCompositeURPProperties.OuterOutlineDistortionIntensity;
        public static readonly int OuterOutlineNoiseScale = ESCompositeURPProperties.OuterOutlineNoiseScale;
        public static readonly int OuterOutlineNoiseSpeed = ESCompositeURPProperties.OuterOutlineNoiseSpeed;
        public static readonly int OuterOutlineTextureEnabled = ESCompositeURPProperties.OuterOutlineTextureEnabled;
        public static readonly int OuterOutlineTintTexture = ESCompositeURPProperties.OuterOutlineTintTexture;
        public static readonly int OuterOutlineTextureSpeed = ESCompositeURPProperties.OuterOutlineTextureSpeed;
        public static readonly int OuterOutlineOnly = ESCompositeURPProperties.OuterOutlineOnly;
        public static readonly int PixelOutlineEnabled = ESCompositeURPProperties.PixelOutlineEnabled;
        public static readonly int PixelOutlineColor = ESCompositeURPProperties.PixelOutlineColor;
        public static readonly int PixelOutlineWidth = ESCompositeURPProperties.PixelOutlineWidth;
        public static readonly int PixelOutlineFade = ESCompositeURPProperties.PixelOutlineFade;
        public static readonly int PixelOutlineTextureEnabled = ESCompositeURPProperties.PixelOutlineTextureEnabled;
        public static readonly int PixelOutlineTintTexture = ESCompositeURPProperties.PixelOutlineTintTexture;
        public static readonly int PixelOutlineTextureSpeed = ESCompositeURPProperties.PixelOutlineTextureSpeed;
        public static readonly int PixelOutlineOnly = ESCompositeURPProperties.PixelOutlineOnly;
        public static readonly int HologramEnabled = ESCompositeURPProperties.HologramEnabled;
        public static readonly int HologramColor = ESCompositeURPProperties.HologramColor;
        public static readonly int HologramLineFrequency = ESCompositeURPProperties.HologramLineFrequency;
        public static readonly int HologramLineGap = ESCompositeURPProperties.HologramLineGap;
        public static readonly int HologramSpeed = ESCompositeURPProperties.HologramSpeed;
        public static readonly int HologramMinAlpha = ESCompositeURPProperties.HologramMinAlpha;
        public static readonly int HologramFade = ESCompositeURPProperties.HologramFade;
        public static readonly int HologramContrast = ESCompositeURPProperties.HologramContrast;
        public static readonly int HologramSpace = ESCompositeURPProperties.HologramSpace;
        public static readonly int HologramDirection = ESCompositeURPProperties.HologramDirection;
        public static readonly int HologramDistortionOffset = ESCompositeURPProperties.HologramDistortionOffset;
        public static readonly int HologramDistortionDirection = ESCompositeURPProperties.HologramDistortionDirection;
        public static readonly int HologramDistortionSpeed = ESCompositeURPProperties.HologramDistortionSpeed;
        public static readonly int HologramDistortionDensity = ESCompositeURPProperties.HologramDistortionDensity;
        public static readonly int HologramDistortionScale = ESCompositeURPProperties.HologramDistortionScale;
        public static readonly int GlitchEnabled = ESCompositeURPProperties.GlitchEnabled;
        public static readonly int GlitchIntensity = ESCompositeURPProperties.GlitchIntensity;
        public static readonly int GlitchSpeed = ESCompositeURPProperties.GlitchSpeed;
        public static readonly int GlitchScanDirection = ESCompositeURPProperties.GlitchScanDirection;
        public static readonly int GlitchFade = ESCompositeURPProperties.GlitchFade;
        public static readonly int GlitchMaskMin = ESCompositeURPProperties.GlitchMaskMin;
        public static readonly int GlitchMaskScale = ESCompositeURPProperties.GlitchMaskScale;
        public static readonly int GlitchMaskSpeed = ESCompositeURPProperties.GlitchMaskSpeed;
        public static readonly int GlitchHueSpeed = ESCompositeURPProperties.GlitchHueSpeed;
        public static readonly int GlitchBrightness = ESCompositeURPProperties.GlitchBrightness;
        public static readonly int GlitchNoiseScale = ESCompositeURPProperties.GlitchNoiseScale;
        public static readonly int GlitchNoiseSpeed = ESCompositeURPProperties.GlitchNoiseSpeed;
        public static readonly int GlitchDistortion = ESCompositeURPProperties.GlitchDistortion;
        public static readonly int GlitchDistortionScale = ESCompositeURPProperties.GlitchDistortionScale;
        public static readonly int GlitchDistortionSpeed = ESCompositeURPProperties.GlitchDistortionSpeed;
        public static readonly int BurnEnabled = Shader.PropertyToID("_EnableBurn");
        public static readonly int PoisonEnabled = Shader.PropertyToID("_EnablePoison");
        public static readonly int FrozenEnabled = Shader.PropertyToID("_EnableFrozen");
        public static readonly int BlendMode = Shader.PropertyToID("_BlendMode");
        public static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        public static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        public static readonly int BlendOp = Shader.PropertyToID("_BlendOp");

        public static void SetMotionEffects(MaterialPropertyBlock block, bool sparkle, bool flow, bool chromatic, Vector2 flowSpeed, float flowStrength, float sparkleIntensity, float chromaticOffset, float chromaticIntensity)
        {
            if (block == null) return;
            block.SetFloat(SparkleEnabled, sparkle ? 1f : 0f);
            block.SetFloat(FlowEnabled, flow ? 1f : 0f);
            block.SetVector(FlowSpeed, new Vector4(flowSpeed.x, flowSpeed.y, 0f, 0f));
            block.SetFloat(FlowStrength, Mathf.Clamp01(flowStrength));
            block.SetFloat(SparkleIntensity, Mathf.Max(0f, sparkleIntensity));
            block.SetFloat(ChromaticEnabled, chromatic ? 1f : 0f);
            block.SetFloat(ChromaticOffset, Mathf.Max(0f, chromaticOffset));
            block.SetFloat(ChromaticIntensity, Mathf.Clamp01(chromaticIntensity));
        }

        public static void SetStylization(
            MaterialPropertyBlock block,
            bool pixelate,
            float pixelateCells,
            float pixelateStrength,
            bool palette,
            Texture paletteTexture,
            float paletteRow,
            float paletteStrength,
            bool halftone,
            float halftoneScale,
            float halftoneAngle,
            float halftoneStrength)
        {
            if (block == null) return;
            block.SetFloat(PixelateEnabled, pixelate ? 1f : 0f);
            block.SetFloat(PixelateCells, Mathf.Clamp(pixelateCells, 2f, 512f));
            block.SetFloat(PixelateStrength, Mathf.Clamp01(pixelateStrength));
            block.SetFloat(PaletteEnabled, palette ? 1f : 0f);
            if (paletteTexture != null) block.SetTexture(PaletteTexture, paletteTexture);
            block.SetFloat(PaletteRow, Mathf.Clamp01(paletteRow));
            block.SetFloat(PaletteStrength, Mathf.Clamp01(paletteStrength));
            block.SetFloat(HalftoneEnabled, halftone ? 1f : 0f);
            block.SetFloat(HalftoneScale, Mathf.Clamp(halftoneScale, 4f, 512f));
            block.SetFloat(HalftoneAngle, Mathf.Repeat(halftoneAngle, 180f));
            block.SetFloat(HalftoneStrength, Mathf.Clamp01(halftoneStrength));
        }

        public static void SetQuality(Material material, ESCompositeQualityTier quality)
        {
            ESCompositeURPProperties.ApplyHighDefaultQuality(material, QualityTier, quality);
        }

        public static void SetResourceProfile(Material material, ESSpriteCompositeResourceProfile profile)
        {
            if (material == null || !material.HasProperty(ResourceProfile)) return;
            material.SetFloat(ResourceProfile, profile == ESSpriteCompositeResourceProfile.材质优化 ? 1f : 0f);
            RefreshResourceProfile(material);
        }

        public static bool RefreshResourceProfile(Material material)
        {
            return ESCompositeURPProperties.RefreshSpriteResourceProfile(
                material,
                ResourceProfile,
                SurfaceResourceSwitches);
        }

        public static void SetModes(MaterialPropertyBlock block,
            ES2DCompositeAnimationMode animationMode,
            ES2DCompositeFadeMode fadeMode,
            ES2DCompositeCoordinateMode coordinateMode,
            ES2DCompositeTimeMode timeMode)
        {
            if (block == null) return;
            block.SetFloat(AnimationMode, (float)animationMode);
            block.SetFloat(FadeMode, (float)fadeMode);
            block.SetFloat(CoordinateMode, (float)coordinateMode);
            block.SetFloat(TimeMode, (float)timeMode);
        }

        public static void SetUVTransform(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 pivot,
            Vector2 scale,
            Vector2 offset,
            float rotationDegrees,
            bool distortionEnabled = false,
            Vector2 distortionFrequency = default,
            Vector2 distortionSpeed = default,
            float distortionAmount = 0f,
            float rotationSpeed = 0f,
            Texture distortionNoiseTexture = null,
            Vector2 distortionFrom = default,
            Vector2 distortionTo = default,
            float distortionFade = 1f,
            Texture distortionMask = null,
            ESCompositeTextureChannel distortionMaskChannel = ESCompositeTextureChannel.透明)
        {
            ESCompositeURPProperties.SetUVTransform(
                block, enabled, pivot, scale, offset, rotationDegrees,
                distortionEnabled, distortionFrequency, distortionSpeed, distortionAmount, rotationSpeed,
                distortionNoiseTexture, distortionFrom, distortionTo, distortionFade,
                distortionMask, distortionMaskChannel);
        }

        public static void SetSplitToning(
            MaterialPropertyBlock block,
            bool enabled,
            Color shadows,
            Color highlights,
            float balance = 0f,
            float strength = 1f,
            float contrast = 1f,
            float shift = 0f)
        {
            ESCompositeURPProperties.SetSplitToning(
                block, enabled, shadows, highlights, balance, strength, contrast, shift);
        }

        public static void SetSpriteShadow(MaterialPropertyBlock block, bool enabled, Vector2 offset, Color color, float fade = 1f)
        {
            ESCompositeURPProperties.SetSpriteShadow(block, enabled, offset, color, fade);
        }

        public static void SetBlackTint(MaterialPropertyBlock block, bool enabled, Color color, float power = 4f, float fade = 1f)
        {
            ESCompositeURPProperties.SetBlackTint(block, enabled, color, power, fade);
        }

        public static void SetInkSpread(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            Color color,
            float contrast,
            float fade,
            float distance,
            Vector2 position,
            float width,
            Vector2 noiseScale,
            float noiseFactor)
        {
            ESCompositeURPProperties.SetInkSpread(
                block, enabled, noiseTexture, color, contrast, fade,
                distance, position, width, noiseScale, noiseFactor);
        }

        public static void SetShiftHue(MaterialPropertyBlock block, bool enabled, float speed)
        {
            ESCompositeURPProperties.SetShiftHue(block, enabled, speed);
        }

        public static void SetAddHue(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            float speed,
            float brightness,
            float saturation,
            float contrast,
            Texture mask = null,
            Vector2 maskScale = default,
            Vector2 maskOffset = default)
        {
            ESCompositeURPProperties.SetAddHue(
                block, enabled, fade, speed, brightness, saturation, contrast,
                mask, maskScale, maskOffset);
        }

        public static void SetSineGlow(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float contrast,
            float frequency,
            float minimum,
            float maximum,
            float fade = 1f,
            Texture mask = null,
            Vector2 maskScale = default,
            Vector2 maskOffset = default)
        {
            ESCompositeURPProperties.SetSineGlow(
                block, enabled, color, contrast, frequency, minimum, maximum,
                fade, mask, maskScale, maskOffset);
        }

        public static void SetFade(MaterialPropertyBlock block, float progress, float width)
        {
            if (block == null) return;
            block.SetFloat(FadeProgress, Mathf.Clamp01(progress));
            block.SetFloat(FadeWidth, Mathf.Clamp(width, 0.001f, 1f));
        }

        public static void SetFade(
            MaterialPropertyBlock block,
            ESCompositeFadeMode mode,
            float progress,
            float width,
            Vector2 position,
            float rotationDegrees,
            bool invert,
            float noiseFactor,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            Texture noiseTexture = null,
            Texture maskTexture = null,
            Color? edgeColor = null,
            float edgeWidth = 0.08f,
            float edgeIntensity = 1f,
            float distortionStrength = 0.03f)
        {
            ESCompositeURPProperties.SetFade(
                block, mode, progress, width, position, rotationDegrees, invert,
                noiseFactor, noiseScale, noiseSpeed, noiseTexture, maskTexture,
                edgeColor, edgeWidth, edgeIntensity, distortionStrength);
        }

        public static void SetSampling(
            MaterialPropertyBlock block,
            bool blurEnabled,
            bool gaussianBlur,
            float blurRadius,
            float blurIntensity,
            bool sharpenEnabled,
            float sharpenAmount,
            float sharpenRadius,
            float sharpenThreshold,
            float sharpenFade = 1f)
        {
            ESCompositeURPProperties.SetSampling(
                block, blurEnabled, gaussianBlur, blurRadius, blurIntensity,
                sharpenEnabled, sharpenAmount, sharpenRadius, sharpenThreshold, sharpenFade);
        }

        public static void SetTiling(
            MaterialPropertyBlock block,
            ESCompositeTilingMode mode,
            Vector2 worldScale,
            Vector2 worldOffset,
            float worldPixelsPerUnit,
            Vector2 screenScale,
            Vector2 screenOffset,
            float screenPixelsPerTile)
        {
            ESCompositeURPProperties.SetTiling(
                block, mode, worldScale, worldOffset, worldPixelsPerUnit,
                screenScale, screenOffset, screenPixelsPerTile);
        }

        public static void SetGeneratedStylization(
            MaterialPropertyBlock block,
            bool smoothPixelArt,
            float smoothPixelStrength,
            bool checkerboard,
            float checkerboardDarken,
            float checkerboardTiling)
        {
            ESCompositeURPProperties.SetGeneratedStylization(
                block, smoothPixelArt, smoothPixelStrength,
                checkerboard, checkerboardDarken, checkerboardTiling);
        }

        public static void SetFlame(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float brightness,
            float smoothness,
            float radius,
            Vector2 speed,
            float noiseFactor,
            float noiseHeightFactor,
            Vector2 noiseScale)
        {
            ESCompositeURPProperties.SetFlame(
                block, enabled, noiseTexture, brightness, smoothness, radius,
                speed, noiseFactor, noiseHeightFactor, noiseScale);
        }

        public static void SetFlame(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float brightness,
            float smoothness,
            float radius,
            Vector2 speed,
            float noiseFactor,
            float noiseHeightFactor,
            Vector2 noiseScale,
            Vector2 direction,
            Vector2 center)
        {
            ESCompositeURPProperties.SetFlame(
                block, enabled, noiseTexture, brightness, smoothness, radius,
                speed, noiseFactor, noiseHeightFactor, noiseScale, direction, center);
        }

        public static void SetSmoke(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float alpha,
            float smoothness,
            float noiseScale,
            float noiseFactor,
            float darkEdge,
            bool vertexSeed)
        {
            ESCompositeURPProperties.SetSmoke(
                block, enabled, noiseTexture, alpha, smoothness,
                noiseScale, noiseFactor, darkEdge, vertexSeed);
        }

        public static void SetSmoke(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float alpha,
            float smoothness,
            float noiseScale,
            float noiseFactor,
            float darkEdge,
            bool vertexSeed,
            Vector2 speed)
        {
            ESCompositeURPProperties.SetSmoke(
                block, enabled, noiseTexture, alpha, smoothness,
                noiseScale, noiseFactor, darkEdge, vertexSeed, speed);
        }

        /// <summary>
        /// 仅写入实例属性；不会改变承载材质的质量 Keyword 或资源编译配置。
        /// 首次为运行时实例启用精确合同，优先使用 TrySetESNativeExactContract。
        /// </summary>
        public static void SetESNativeExactContract(MaterialPropertyBlock block, bool enabled, Texture shineMask = null)
        {
            ESCompositeURPProperties.SetESNativeExactContract(block, enabled, shineMask);
        }

        /// <summary>
        /// 初始化供 MaterialPropertyBlock 动态切换 ESNative 效果使用的材质。
        /// 此方法会修改材质 Keyword，应在实例初始化或材质创建阶段调用，不应逐帧调用共享材质。
        /// </summary>
        public static bool PrepareMaterialForDynamicESNative(Material material)
        {
            if (material == null || !material.HasProperty(ESNativeExactContract)) return false;
            SetQuality(material, ESCompositeQualityTier.高质量);
            SetResourceProfile(material, ESSpriteCompositeResourceProfile.动态完整);
            return true;
        }

        /// <summary>
        /// 在确认材质已进入高质量、动态完整变体后写入实例级 ESNative 精确合同。
        /// </summary>
        public static bool TrySetESNativeExactContract(
            Material material,
            MaterialPropertyBlock block,
            bool enabled,
            Texture shineMask = null)
        {
            if (material == null || block == null || !material.HasProperty(ESNativeExactContract)) return false;
            if (enabled && !PrepareMaterialForDynamicESNative(material)) return false;
            SetESNativeExactContract(block, enabled, shineMask);
            return true;
        }

        public static void SetESNativeExactContract(Material material, bool enabled, Texture shineMask = null)
        {
            if (material == null || !material.HasProperty(ESNativeExactContract)) return;
            material.SetFloat(ESNativeExactContract, enabled ? 1f : 0f);
            if (material.HasProperty(ESCompositeURPProperties.ShineMaskEnabled))
                material.SetFloat(ESCompositeURPProperties.ShineMaskEnabled, shineMask != null ? 1f : 0f);
            if (shineMask != null && material.HasProperty(ESCompositeURPProperties.ShineMask))
                material.SetTexture(ESCompositeURPProperties.ShineMask, shineMask);
            if (enabled) SetQuality(material, ESCompositeQualityTier.高质量);
            RefreshResourceProfile(material);
        }

        public static void SetInnerOutline(
            MaterialPropertyBlock block, bool enabled, Color color, float width, float fade,
            bool distortionEnabled, Vector2 distortionIntensity, Vector2 noiseScale, Vector2 noiseSpeed,
            bool textureEnabled, Texture tintTexture, Vector2 textureSpeed, bool outlineOnly)
        {
            ESCompositeURPProperties.SetInnerOutline(
                block, enabled, color, width, fade,
                distortionEnabled, distortionIntensity, noiseScale, noiseSpeed,
                textureEnabled, tintTexture, textureSpeed, outlineOnly);
        }

        public static void SetOuterOutline(
            MaterialPropertyBlock block, bool enabled, Color color, float width, float fade,
            bool distortionEnabled, Vector2 distortionIntensity, Vector2 noiseScale, Vector2 noiseSpeed,
            bool textureEnabled, Texture tintTexture, Vector2 textureSpeed, bool outlineOnly)
        {
            ESCompositeURPProperties.SetOuterOutline(
                block, enabled, color, width, fade,
                distortionEnabled, distortionIntensity, noiseScale, noiseSpeed,
                textureEnabled, tintTexture, textureSpeed, outlineOnly);
        }

        public static void SetPixelOutline(
            MaterialPropertyBlock block, bool enabled, Color color, float width, float fade,
            bool textureEnabled, Texture tintTexture, Vector2 textureSpeed, bool outlineOnly)
        {
            ESCompositeURPProperties.SetPixelOutline(
                block, enabled, color, width, fade,
                textureEnabled, tintTexture, textureSpeed, outlineOnly);
        }

        public static void SetShine(
            MaterialPropertyBlock block, bool enabled, Color color,
            float speed, float width, float intensity,
            Vector2 direction, float fallbackAngle = 30f)
        {
            ESCompositeURPProperties.SetShine(
                block, enabled, color, speed, width, intensity,
                new Vector3(direction.x, direction.y, 0f), fallbackAngle);
        }

        public static void SetShine(
            MaterialPropertyBlock block, bool enabled, Color color,
            float speed, float width, float intensity,
            Vector2 direction, ESCompositeProjectionSpace space,
            float fallbackAngle = 30f)
        {
            ESCompositeURPProperties.SetShine(
                block, enabled, color, speed, width, intensity,
                new Vector3(direction.x, direction.y, 0f), space, fallbackAngle);
        }

        public static void SetSquish(
            MaterialPropertyBlock block, bool enabled, float amount, float speed)
        {
            ESCompositeURPProperties.SetSquish(block, enabled, amount, speed);
        }

        public static void SetSquish(
            MaterialPropertyBlock block, bool enabled, float amount, float speed, Vector2 direction)
        {
            ESCompositeURPProperties.SetSquish(block, enabled, amount, speed, direction);
        }

        public static void SetVibrate(
            MaterialPropertyBlock block, bool enabled, float amplitude, float speed)
        {
            ESCompositeURPProperties.SetVibrate(block, enabled, amplitude, speed);
        }

        public static void SetVibrate(
            MaterialPropertyBlock block, bool enabled, float amplitude, float speed, Vector2 direction)
        {
            ESCompositeURPProperties.SetVibrate(block, enabled, amplitude, speed, direction);
        }

        public static void SetNormalMap(MaterialPropertyBlock block, Texture normalMap, float scale = 1f)
        {
            if (block == null) return;
            if (normalMap != null) block.SetTexture(NormalMap, normalMap);
            block.SetFloat(NormalScale, Mathf.Clamp(scale, 0f, 2f));
        }

        public static void SetDistortion(
            MaterialPropertyBlock block, bool enabled, Texture noiseTexture,
            Vector2 noiseScale, Vector2 noiseSpeed, float strength)
        {
            ESCompositeURPProperties.SetLegacyDistortion(
                block, enabled, noiseTexture, noiseScale, noiseSpeed, strength);
        }

        public static void SetDistortion(
            MaterialPropertyBlock block, bool enabled, Texture noiseTexture,
            Vector2 noiseScale, Vector2 noiseSpeed, float strength, Vector2 direction)
        {
            ESCompositeURPProperties.SetLegacyDistortion(
                block, enabled, noiseTexture, noiseScale, noiseSpeed, strength, direction);
        }

        public static void SetAlphaClip(MaterialPropertyBlock block, bool enabled, float cutoff)
        {
            ESCompositeURPProperties.SetAlphaClip(block, enabled, cutoff);
        }

        public static void SetHologram(
            MaterialPropertyBlock block, bool enabled, Color color,
            float lineFrequency, float lineGap, float speed, float minAlpha,
            float fade, float contrast, ES3DLitHologramSpace space,
            float distortionOffset, float distortionSpeed, float distortionDensity, float distortionScale)
        {
            ESCompositeURPProperties.SetHologram(
                block, enabled, color, lineFrequency, lineGap, speed, minAlpha,
                fade, contrast, space, distortionOffset, distortionSpeed,
                distortionDensity, distortionScale);
        }

        public static void SetHologram(
            MaterialPropertyBlock block, bool enabled, Color color,
            float lineFrequency, float lineGap, float speed, float minAlpha,
            float fade, float contrast, ES3DLitHologramSpace space,
            float distortionOffset, float distortionSpeed, float distortionDensity, float distortionScale,
            Vector3 scanDirection,
            Vector2 distortionDirection)
        {
            ESCompositeURPProperties.SetHologram(
                block, enabled, color, lineFrequency, lineGap, speed, minAlpha,
                fade, contrast, space, distortionOffset, distortionSpeed,
                distortionDensity, distortionScale, scanDirection, distortionDirection);
        }

        public static void SetGlitch(
            MaterialPropertyBlock block, bool enabled, float intensity, float speed,
            float fade, float maskMin, Vector2 maskScale, Vector2 maskSpeed,
            float hueSpeed, float brightness, Vector2 noiseScale, Vector2 noiseSpeed,
            Vector2 distortion, Vector2 distortionScale, Vector2 distortionSpeed)
        {
            ESCompositeURPProperties.SetGlitch(
                block, enabled, intensity, speed, fade, maskMin,
                maskScale, maskSpeed, hueSpeed, brightness,
                noiseScale, noiseSpeed, distortion, distortionScale, distortionSpeed);
        }

        public static void SetGlitchScanDirection(MaterialPropertyBlock block, Vector3 direction)
        {
            ESCompositeURPProperties.SetGlitchScanDirection(block, direction);
        }

        public static void SetEffectToggles(MaterialPropertyBlock block, bool hologram, bool glitch, bool frozen, bool burn, bool poison)
        {
            if (block == null) return;
            block.SetFloat(HologramEnabled, hologram ? 1f : 0f);
            block.SetFloat(GlitchEnabled, glitch ? 1f : 0f);
            block.SetFloat(FrozenEnabled, frozen ? 1f : 0f);
            block.SetFloat(BurnEnabled, burn ? 1f : 0f);
            block.SetFloat(PoisonEnabled, poison ? 1f : 0f);
        }

        public static void SetBlendMode(Material material, ES2DCompositeBlendMode blendMode)
        {
            if (material == null) return;
            switch (blendMode)
            {
                case ES2DCompositeBlendMode.叠加:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.One);
                    break;
                case ES2DCompositeBlendMode.预乘透明:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
                case ES2DCompositeBlendMode.正片叠底:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.DstColor);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.Zero);
                    break;
                default:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
            }

            material.SetFloat(BlendMode, (float)blendMode);
            material.SetFloat(BlendOp, (float)UnityEngine.Rendering.BlendOp.Add);
        }
    }

    public enum ES3DLitSurfaceMode
    {
        不透明 = 0,
        透明裁剪 = 1,
        透明混合 = 2
    }

    public enum ES3DLitHologramSpace
    {
        局部UV = 0,
        世界投影 = 1,
        世界高度 = 1
    }

    public static class ES3DLitCompositeURPProperties
    {
        public const string ShaderName = "ES/3D/Lit Composite URP";
        public static readonly int ESNativeStatusContract = ESCompositeURPProperties.ESNativeStatusContract;
        public static readonly int ESNativeExactContract = ESCompositeURPProperties.ESNativeExactContract;
        private const int GeometryQueue = (int)RenderQueue.Geometry;
        private const int AlphaTestQueue = (int)RenderQueue.AlphaTest;
        private const int TransparentQueue = (int)RenderQueue.Transparent;
        private const int UVResourceBit = 1;
        private const int FadeResourceBit = 2;
        private const int SurfaceResourceBit = 4;
        private const int LayerResourceBit = 8;
        private static readonly string[] ResourceMaskKeywords =
        {
            "_ES_LIT_RESOURCE_MASK_0", "_ES_LIT_RESOURCE_MASK_1",
            "_ES_LIT_RESOURCE_MASK_2", "_ES_LIT_RESOURCE_MASK_3",
            "_ES_LIT_RESOURCE_MASK_4", "_ES_LIT_RESOURCE_MASK_5",
            "_ES_LIT_RESOURCE_MASK_6", "_ES_LIT_RESOURCE_MASK_7",
            "_ES_LIT_RESOURCE_MASK_8", "_ES_LIT_RESOURCE_MASK_9",
            "_ES_LIT_RESOURCE_MASK_10", "_ES_LIT_RESOURCE_MASK_11",
            "_ES_LIT_RESOURCE_MASK_12", "_ES_LIT_RESOURCE_MASK_13",
            "_ES_LIT_RESOURCE_MASK_14", "_ES_LIT_RESOURCE_MASK_15"
        };
        private static readonly string[] SurfaceResourceSwitches =
        {
            "_ESNativeStatusContract",
            "_EnableAddColor", "_EnableStrongTint", "_EnableAlphaTint", "_EnableColorReplace",
            "_EnableRecolorRGB", "_EnableRecolorRGBYCP", "_EnableBrightness", "_EnableContrast",
            "_EnableSaturation", "_EnableHue", "_EnableSplitToning", "_EnableBlackTint",
            "_EnableInkSpread", "_EnableShiftHue", "_EnableAddHue", "_EnableSineGlow",
            "_EnableCamouflage", "_EnableMetal", "_EnableFrozen", "_EnablePoison",
            "_EnableEnchanted", "_EnableShifting", "_EnableNegative", "_EnableRainbow",
            "_EnablePingPongGlow", "_EnableSmoke", "_EnableFlame", "_EnableFullGlowDissolve",
            "_EnableBurn", "_EnableFullDistortion", "_EnableInnerOutline", "_EnableOuterOutline", "_EnablePixelOutline",
            "_EnableGlitch"
        };
        private static readonly string[] FadeResourceSwitches =
        {
            "_EnableFullAlphaDissolve",
            "_EnableSourceAlphaDissolve",
            "_EnableSourceGlowDissolve",
            "_EnableDirectionalAlphaFade",
            "_EnableDirectionalGlowFade",
            "_EnableDirectionalDistortion"
        };
        public static readonly int DissolveMode = Shader.PropertyToID("_DissolveMode");
        public static readonly int DissolveProgress = Shader.PropertyToID("_DissolveProgress");
        public static readonly int RimIntensity = Shader.PropertyToID("_RimIntensity");
        public static readonly int ShineEnabled = Shader.PropertyToID("_EnableShine");
        public static readonly int ShineColor = Shader.PropertyToID("_ShineColor");
        public static readonly int ShineSpeed = Shader.PropertyToID("_ShineSpeed");
        public static readonly int ShineWidth = Shader.PropertyToID("_ShineWidth");
        public static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
        public static readonly int ShineSpace = ESCompositeURPProperties.ShineSpace;
        public static readonly int ShineDirection = Shader.PropertyToID("_ShineDirection");
        public static readonly int AlphaTintFade = ESCompositeURPProperties.AlphaTintFade;
        public static readonly int ReplaceContrast = ESCompositeURPProperties.ReplaceContrast;
        public static readonly int ReplaceFade = ESCompositeURPProperties.ReplaceFade;
        public static readonly int SplitToneContrast = ESCompositeURPProperties.SplitToneContrast;
        public static readonly int SplitToneShift = ESCompositeURPProperties.SplitToneShift;
        public static readonly int GlowContrast = ESCompositeURPProperties.GlowContrast;
        public static readonly int GlowFade = ESCompositeURPProperties.GlowFade;
        public static readonly int SparkleEnabled = Shader.PropertyToID("_EnableSparkle");
        public static readonly int SparkleColor = Shader.PropertyToID("_SparkleColor");
        public static readonly int SparkleScale = Shader.PropertyToID("_SparkleScale");
        public static readonly int SparkleSpeed = Shader.PropertyToID("_SparkleSpeed");
        public static readonly int SparkleDensity = Shader.PropertyToID("_SparkleDensity");
        public static readonly int SparkleSharpness = Shader.PropertyToID("_SparkleSharpness");
        public static readonly int SparkleIntensity = Shader.PropertyToID("_SparkleIntensity");
        public static readonly int FlowEnabled = Shader.PropertyToID("_EnableFlow");
        public static readonly int FlowSpeed = Shader.PropertyToID("_FlowSpeed");
        public static readonly int FlowStrength = Shader.PropertyToID("_FlowStrength");
        public static readonly int FlowMapEnabled = Shader.PropertyToID("_EnableFlowMap");
        public static readonly int FlowMap = Shader.PropertyToID("_FlowMap");
        public static readonly int FlowMapScale = Shader.PropertyToID("_FlowMapScale");
        public static readonly int FlowMapSpeed = Shader.PropertyToID("_FlowMapSpeed");
        public static readonly int FlowMapStrength = Shader.PropertyToID("_FlowMapStrength");
        public static readonly int VertexAnimationEnabled = Shader.PropertyToID("_EnableVertexAnimation");
        public static readonly int VertexAnimationDirection = Shader.PropertyToID("_VertexAnimationDirection");
        public static readonly int VertexAnimationAmplitude = Shader.PropertyToID("_VertexAnimationAmplitude");
        public static readonly int VertexAnimationFrequency = Shader.PropertyToID("_VertexAnimationFrequency");
        public static readonly int VertexAnimationSpeed = Shader.PropertyToID("_VertexAnimationSpeed");
        public static readonly int VertexAnimationMask = Shader.PropertyToID("_VertexAnimationMask");
        public static readonly int ChromaticEnabled = Shader.PropertyToID("_EnableChromatic");
        public static readonly int ChromaticOffset = Shader.PropertyToID("_ChromaticOffset");
        public static readonly int ChromaticIntensity = Shader.PropertyToID("_ChromaticIntensity");
        public static readonly int ChromaticEdgeOnly = Shader.PropertyToID("_ChromaticEdgeOnly");
        public static readonly int ChromaticAngle = Shader.PropertyToID("_ChromaticAngle");
        public static readonly int BlurEnabled = Shader.PropertyToID("_EnableBlur");
        public static readonly int BlurRadius = Shader.PropertyToID("_BlurRadius");
        public static readonly int BlurIntensity = Shader.PropertyToID("_BlurIntensity");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int NormalMapEnabled = Shader.PropertyToID("_UseNormalMap");
        public static readonly int NormalMap = Shader.PropertyToID("_NormalMap");
        public static readonly int NormalScale = Shader.PropertyToID("_NormalScale");
        public static readonly int MetallicMapEnabled = Shader.PropertyToID("_UseMetallicMap");
        public static readonly int MetallicMap = Shader.PropertyToID("_MetallicMap");
        public static readonly int SmoothnessMapChannel = Shader.PropertyToID("_SmoothnessMapChannel");
        public static readonly int Metallic = Shader.PropertyToID("_Metallic");
        public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        public static readonly int Occlusion = Shader.PropertyToID("_Occlusion");
        public static readonly int OcclusionMapEnabled = Shader.PropertyToID("_UseOcclusionMap");
        public static readonly int OcclusionMap = Shader.PropertyToID("_OcclusionMap");
        public static readonly int EmissionEnabled = Shader.PropertyToID("_UseEmission");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        public static readonly int EmissionUseAlpha = Shader.PropertyToID("_EmissionUseAlpha");
        public static readonly int RimEnabled = Shader.PropertyToID("_EnableRim");
        public static readonly int RimColor = Shader.PropertyToID("_RimColor");
        public static readonly int RimPower = Shader.PropertyToID("_RimPower");
        public static readonly int BurnEnabled = Shader.PropertyToID("_EnableBurn");
        public static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        public static readonly int Cutoff = ESCompositeURPProperties.Cutoff;
        public static readonly int NoiseTexture = ESCompositeURPProperties.NoiseTexture;
        public static readonly int NoiseScale = ESCompositeURPProperties.NoiseScale;
        public static readonly int NoiseSpeed = ESCompositeURPProperties.NoiseSpeed;
        public static readonly int DissolveSoftness = Shader.PropertyToID("_DissolveSoftness");
        public static readonly int Surface = Shader.PropertyToID("_Surface");
        public static readonly int Cull = Shader.PropertyToID("_Cull");
        public static readonly int QueueOffset = Shader.PropertyToID("_QueueOffset");
        public static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        public static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        public static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        public static readonly int ReceiveShadows = Shader.PropertyToID("_ReceiveShadows");
        public static readonly int QualityTier = Shader.PropertyToID("_QualityTier");
        public static readonly int ResourceProfile = Shader.PropertyToID("_ResourceProfile");
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int TimeFPSEnabled = ESCompositeURPProperties.TimeFPSEnabled;
        public static readonly int TimeFPS = ESCompositeURPProperties.TimeFPS;
        public static readonly int TimeFrequencyEnabled = ESCompositeURPProperties.TimeFrequencyEnabled;
        public static readonly int TimeFrequency = ESCompositeURPProperties.TimeFrequency;
        public static readonly int TimeRange = ESCompositeURPProperties.TimeRange;
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");
        public static readonly int UVTransformEnabled = ESCompositeURPProperties.UVTransformEnabled;
        public static readonly int UVDistortEnabled = ESCompositeURPProperties.UVDistortEnabled;
        public static readonly int FadeMode = ESCompositeURPProperties.FadeMode;
        public static readonly int FadeProgress = ESCompositeURPProperties.FadeProgress;
        public static readonly int TilingMode = ESCompositeURPProperties.TilingMode;
        public static readonly int SqueezeEnabled = ESCompositeURPProperties.SqueezeEnabled;
        public static readonly int SineRotateEnabled = ESCompositeURPProperties.SineRotateEnabled;
        public static readonly int WindEnabled = ESCompositeURPProperties.WindEnabled;
        public static readonly int WindAnchorDirection = ESCompositeURPProperties.WindAnchorDirection;
        public static readonly int SquishEnabled = ESCompositeURPProperties.SquishEnabled;
        public static readonly int SquishDirection = ESCompositeURPProperties.SquishDirection;
        public static readonly int SquishFade = ESCompositeURPProperties.SquishFade;
        public static readonly int InteractiveWindRotation = ESCompositeURPProperties.InteractiveWindRotation;
        public static readonly int InteractiveWindHeight = ESCompositeURPProperties.InteractiveWindHeight;
        public static readonly int InteractiveSquish = ESCompositeURPProperties.InteractiveSquish;
        public static readonly int WindPhaseOffset = ESCompositeURPProperties.WindPhaseOffset;
        public static readonly int WiggleEnabled = ESCompositeURPProperties.WiggleEnabled;
        public static readonly int WiggleDirection = ESCompositeURPProperties.WiggleDirection;
        public static readonly int VibrateEnabled = ESCompositeURPProperties.VibrateEnabled;
        public static readonly int VibrateDirection = ESCompositeURPProperties.VibrateDirection;
        public static readonly int SineMoveEnabled = ESCompositeURPProperties.SineMoveEnabled;
        public static readonly int SineScaleEnabled = ESCompositeURPProperties.SineScaleEnabled;
        public static readonly int CustomFadeEnabled = ESCompositeURPProperties.CustomFadeEnabled;
        public static readonly int SmoothPixelArtEnabled = ESCompositeURPProperties.SmoothPixelArtEnabled;
        public static readonly int SmoothPixelStrength = ESCompositeURPProperties.SmoothPixelStrength;
        public static readonly int PixelateEnabled = ESCompositeURPProperties.PixelateEnabled;
        public static readonly int PixelateCells = ESCompositeURPProperties.PixelateCells;
        public static readonly int PixelateStrength = ESCompositeURPProperties.PixelateStrength;
        public static readonly int CheckerboardEnabled = ESCompositeURPProperties.CheckerboardEnabled;
        public static readonly int CheckerboardDarken = ESCompositeURPProperties.CheckerboardDarken;
        public static readonly int CheckerboardTiling = ESCompositeURPProperties.CheckerboardTiling;
        public static readonly int FlameEnabled = ESCompositeURPProperties.FlameEnabled;
        public static readonly int FlameCenter = ESCompositeURPProperties.FlameCenter;
        public static readonly int FlameDirection = ESCompositeURPProperties.FlameDirection;
        public static readonly int SmokeEnabled = ESCompositeURPProperties.SmokeEnabled;
        public static readonly int SmokeSpeed = ESCompositeURPProperties.SmokeSpeed;
        public static readonly int HalftoneEnabled = ESCompositeURPProperties.HalftoneEnabled;
        public static readonly int HalftoneScale = ESCompositeURPProperties.HalftoneScale;
        public static readonly int HalftoneAngle = ESCompositeURPProperties.HalftoneAngle;
        public static readonly int HalftoneStrength = ESCompositeURPProperties.HalftoneStrength;
        public static readonly int HalftonePosition = ESCompositeURPProperties.HalftonePosition;
        public static readonly int HalftoneFade = ESCompositeURPProperties.HalftoneFade;
        public static readonly int HalftoneFadeWidth = ESCompositeURPProperties.HalftoneFadeWidth;
        public static readonly int HalftoneInvert = ESCompositeURPProperties.HalftoneInvert;
        public static readonly int HalftoneAlphaPattern = ESCompositeURPProperties.HalftoneAlphaPattern;
        public static readonly int SharpenEnabled = ESCompositeURPProperties.SharpenEnabled;
        public static readonly int SharpenAmount = ESCompositeURPProperties.SharpenAmount;
        public static readonly int SharpenRadius = ESCompositeURPProperties.SharpenRadius;
        public static readonly int SharpenThreshold = ESCompositeURPProperties.SharpenThreshold;
        public static readonly int SharpenFade = ESCompositeURPProperties.SharpenFade;
        public static readonly int TextureLayer1Enabled = ESCompositeURPProperties.TextureLayer1Enabled;
        public static readonly int TextureLayer1Fade = ESCompositeURPProperties.TextureLayer1Fade;
        public static readonly int TextureLayer1Texture = ESCompositeURPProperties.TextureLayer1Texture;
        public static readonly int TextureLayer1Color = ESCompositeURPProperties.TextureLayer1Color;
        public static readonly int TextureLayer1Scale = ESCompositeURPProperties.TextureLayer1Scale;
        public static readonly int TextureLayer1Offset = ESCompositeURPProperties.TextureLayer1Offset;
        public static readonly int TextureLayer1ScrollEnabled = ESCompositeURPProperties.TextureLayer1ScrollEnabled;
        public static readonly int TextureLayer1ScrollSpeed = ESCompositeURPProperties.TextureLayer1ScrollSpeed;
        public static readonly int TextureLayer1SheetEnabled = ESCompositeURPProperties.TextureLayer1SheetEnabled;
        public static readonly int TextureLayer1Columns = ESCompositeURPProperties.TextureLayer1Columns;
        public static readonly int TextureLayer1Rows = ESCompositeURPProperties.TextureLayer1Rows;
        public static readonly int TextureLayer1Speed = ESCompositeURPProperties.TextureLayer1Speed;
        public static readonly int TextureLayer1StartFrame = ESCompositeURPProperties.TextureLayer1StartFrame;
        public static readonly int TextureLayer1EdgeClip = ESCompositeURPProperties.TextureLayer1EdgeClip;
        public static readonly int TextureLayer1ContrastEnabled = ESCompositeURPProperties.TextureLayer1ContrastEnabled;
        public static readonly int TextureLayer1Contrast = ESCompositeURPProperties.TextureLayer1Contrast;
        public static readonly int TextureLayer2Enabled = ESCompositeURPProperties.TextureLayer2Enabled;
        public static readonly int TextureLayer2Fade = ESCompositeURPProperties.TextureLayer2Fade;
        public static readonly int TextureLayer2Texture = ESCompositeURPProperties.TextureLayer2Texture;
        public static readonly int TextureLayer2Color = ESCompositeURPProperties.TextureLayer2Color;
        public static readonly int TextureLayer2Scale = ESCompositeURPProperties.TextureLayer2Scale;
        public static readonly int TextureLayer2Offset = ESCompositeURPProperties.TextureLayer2Offset;
        public static readonly int TextureLayer2ScrollEnabled = ESCompositeURPProperties.TextureLayer2ScrollEnabled;
        public static readonly int TextureLayer2ScrollSpeed = ESCompositeURPProperties.TextureLayer2ScrollSpeed;
        public static readonly int TextureLayer2SheetEnabled = ESCompositeURPProperties.TextureLayer2SheetEnabled;
        public static readonly int TextureLayer2Columns = ESCompositeURPProperties.TextureLayer2Columns;
        public static readonly int TextureLayer2Rows = ESCompositeURPProperties.TextureLayer2Rows;
        public static readonly int TextureLayer2Speed = ESCompositeURPProperties.TextureLayer2Speed;
        public static readonly int TextureLayer2StartFrame = ESCompositeURPProperties.TextureLayer2StartFrame;
        public static readonly int TextureLayer2EdgeClip = ESCompositeURPProperties.TextureLayer2EdgeClip;
        public static readonly int TextureLayer2ContrastEnabled = ESCompositeURPProperties.TextureLayer2ContrastEnabled;
        public static readonly int TextureLayer2Contrast = ESCompositeURPProperties.TextureLayer2Contrast;
        public static readonly int InnerOutlineEnabled = ESCompositeURPProperties.InnerOutlineEnabled;
        public static readonly int InnerOutlineColor = ESCompositeURPProperties.InnerOutlineColor;
        public static readonly int InnerOutlineWidth = ESCompositeURPProperties.InnerOutlineWidth;
        public static readonly int InnerOutlineFade = ESCompositeURPProperties.InnerOutlineFade;
        public static readonly int InnerOutlineDistortionEnabled = ESCompositeURPProperties.InnerOutlineDistortionEnabled;
        public static readonly int InnerOutlineDistortionIntensity = ESCompositeURPProperties.InnerOutlineDistortionIntensity;
        public static readonly int InnerOutlineNoiseScale = ESCompositeURPProperties.InnerOutlineNoiseScale;
        public static readonly int InnerOutlineNoiseSpeed = ESCompositeURPProperties.InnerOutlineNoiseSpeed;
        public static readonly int InnerOutlineTextureEnabled = ESCompositeURPProperties.InnerOutlineTextureEnabled;
        public static readonly int InnerOutlineTintTexture = ESCompositeURPProperties.InnerOutlineTintTexture;
        public static readonly int InnerOutlineTextureSpeed = ESCompositeURPProperties.InnerOutlineTextureSpeed;
        public static readonly int InnerOutlineOnly = ESCompositeURPProperties.InnerOutlineOnly;
        public static readonly int OuterOutlineEnabled = ESCompositeURPProperties.OuterOutlineEnabled;
        public static readonly int OuterOutlineColor = ESCompositeURPProperties.OuterOutlineColor;
        public static readonly int OuterOutlineWidth = ESCompositeURPProperties.OuterOutlineWidth;
        public static readonly int OuterOutlineFade = ESCompositeURPProperties.OuterOutlineFade;
        public static readonly int OuterOutlineDistortionEnabled = ESCompositeURPProperties.OuterOutlineDistortionEnabled;
        public static readonly int OuterOutlineDistortionIntensity = ESCompositeURPProperties.OuterOutlineDistortionIntensity;
        public static readonly int OuterOutlineNoiseScale = ESCompositeURPProperties.OuterOutlineNoiseScale;
        public static readonly int OuterOutlineNoiseSpeed = ESCompositeURPProperties.OuterOutlineNoiseSpeed;
        public static readonly int OuterOutlineTextureEnabled = ESCompositeURPProperties.OuterOutlineTextureEnabled;
        public static readonly int OuterOutlineTintTexture = ESCompositeURPProperties.OuterOutlineTintTexture;
        public static readonly int OuterOutlineTextureSpeed = ESCompositeURPProperties.OuterOutlineTextureSpeed;
        public static readonly int OuterOutlineOnly = ESCompositeURPProperties.OuterOutlineOnly;
        public static readonly int PixelOutlineEnabled = ESCompositeURPProperties.PixelOutlineEnabled;
        public static readonly int PixelOutlineColor = ESCompositeURPProperties.PixelOutlineColor;
        public static readonly int PixelOutlineWidth = ESCompositeURPProperties.PixelOutlineWidth;
        public static readonly int PixelOutlineFade = ESCompositeURPProperties.PixelOutlineFade;
        public static readonly int PixelOutlineTextureEnabled = ESCompositeURPProperties.PixelOutlineTextureEnabled;
        public static readonly int PixelOutlineTintTexture = ESCompositeURPProperties.PixelOutlineTintTexture;
        public static readonly int PixelOutlineTextureSpeed = ESCompositeURPProperties.PixelOutlineTextureSpeed;
        public static readonly int PixelOutlineOnly = ESCompositeURPProperties.PixelOutlineOnly;
        public static readonly int SpriteShadowEnabled = ESCompositeURPProperties.SpriteShadowEnabled;
        public static readonly int SpriteShadowFade = ESCompositeURPProperties.SpriteShadowFade;
        public static readonly int SpriteShadowOffset = ESCompositeURPProperties.SpriteShadowOffset;
        public static readonly int SpriteShadowColor = ESCompositeURPProperties.SpriteShadowColor;
        public static readonly int FullGlowDissolveEnabled = ESCompositeURPProperties.FullGlowDissolveEnabled;
        public static readonly int FullGlowDissolveFade = ESCompositeURPProperties.FullGlowDissolveFade;
        public static readonly int FullGlowDissolveWidth = ESCompositeURPProperties.FullGlowDissolveWidth;
        public static readonly int FullGlowDissolveEdgeColor = ESCompositeURPProperties.FullGlowDissolveEdgeColor;
        public static readonly int FullGlowDissolveNoiseScale = ESCompositeURPProperties.FullGlowDissolveNoiseScale;
        public static readonly int HologramEnabled = ESCompositeURPProperties.HologramEnabled;
        public static readonly int HologramColor = ESCompositeURPProperties.HologramColor;
        public static readonly int HologramLineFrequency = ESCompositeURPProperties.HologramLineFrequency;
        public static readonly int HologramLineGap = ESCompositeURPProperties.HologramLineGap;
        public static readonly int HologramSpeed = ESCompositeURPProperties.HologramSpeed;
        public static readonly int HologramMinAlpha = ESCompositeURPProperties.HologramMinAlpha;
        public static readonly int HologramFade = ESCompositeURPProperties.HologramFade;
        public static readonly int HologramContrast = ESCompositeURPProperties.HologramContrast;
        public static readonly int HologramSpace = ESCompositeURPProperties.HologramSpace;
        public static readonly int HologramDirection = ESCompositeURPProperties.HologramDirection;
        public static readonly int HologramDistortionOffset = ESCompositeURPProperties.HologramDistortionOffset;
        public static readonly int HologramDistortionDirection = ESCompositeURPProperties.HologramDistortionDirection;
        public static readonly int HologramDistortionSpeed = ESCompositeURPProperties.HologramDistortionSpeed;
        public static readonly int HologramDistortionDensity = ESCompositeURPProperties.HologramDistortionDensity;
        public static readonly int HologramDistortionScale = ESCompositeURPProperties.HologramDistortionScale;
        public static readonly int GlitchEnabled = ESCompositeURPProperties.GlitchEnabled;
        public static readonly int GlitchIntensity = ESCompositeURPProperties.GlitchIntensity;
        public static readonly int GlitchSpeed = ESCompositeURPProperties.GlitchSpeed;
        public static readonly int GlitchScanDirection = ESCompositeURPProperties.GlitchScanDirection;
        public static readonly int GlitchFade = ESCompositeURPProperties.GlitchFade;
        public static readonly int GlitchMaskMin = ESCompositeURPProperties.GlitchMaskMin;
        public static readonly int GlitchMaskScale = ESCompositeURPProperties.GlitchMaskScale;
        public static readonly int GlitchMaskSpeed = ESCompositeURPProperties.GlitchMaskSpeed;
        public static readonly int GlitchHueSpeed = ESCompositeURPProperties.GlitchHueSpeed;
        public static readonly int GlitchBrightness = ESCompositeURPProperties.GlitchBrightness;
        public static readonly int GlitchNoiseScale = ESCompositeURPProperties.GlitchNoiseScale;
        public static readonly int GlitchNoiseSpeed = ESCompositeURPProperties.GlitchNoiseSpeed;
        public static readonly int GlitchDistortion = ESCompositeURPProperties.GlitchDistortion;
        public static readonly int GlitchDistortionScale = ESCompositeURPProperties.GlitchDistortionScale;
        public static readonly int GlitchDistortionSpeed = ESCompositeURPProperties.GlitchDistortionSpeed;

        public static void SetDissolve(MaterialPropertyBlock block, ES3DCompositeDissolveMode mode, float progress)
        {
            if (block == null) return;
            block.SetFloat(DissolveMode, (float)mode);
            block.SetFloat(DissolveProgress, progress);
        }

        public static void SetDissolve(
            MaterialPropertyBlock block,
            ES3DCompositeDissolveMode mode,
            float progress,
            float softness,
            Texture noiseTexture,
            Vector4 noiseScale,
            Vector4 noiseSpeed)
        {
            if (block == null) return;
            SetDissolve(block, mode, progress);
            block.SetFloat(DissolveSoftness, Mathf.Clamp(softness, 0.001f, 1f));
            ESCompositeURPProperties.SetNoise(block, noiseTexture, noiseScale, noiseSpeed);
        }

        public static void SetNormalMap(MaterialPropertyBlock block, bool enabled, Texture texture, float scale = 1f)
        {
            if (block == null) return;
            bool useTexture = enabled && texture != null;
            block.SetFloat(NormalMapEnabled, useTexture ? 1f : 0f);
            if (texture != null) block.SetTexture(NormalMap, texture);
            block.SetFloat(NormalScale, Mathf.Clamp(scale, 0f, 2f));
        }

        public static void SetOcclusionMap(MaterialPropertyBlock block, bool enabled, Texture texture, float strength = 1f)
        {
            if (block == null) return;
            bool useTexture = enabled && texture != null;
            block.SetFloat(OcclusionMapEnabled, useTexture ? 1f : 0f);
            if (texture != null) block.SetTexture(OcclusionMap, texture);
            block.SetFloat(Occlusion, Mathf.Clamp01(strength));
        }

        public static void SetEmission(
            MaterialPropertyBlock block, bool enabled, Color color,
            Texture texture = null, bool useTextureAlpha = false)
        {
            if (block == null) return;
            block.SetFloat(EmissionEnabled, enabled ? 1f : 0f);
            block.SetColor(EmissionColor, color);
            if (texture != null) block.SetTexture(EmissionMap, texture);
            block.SetFloat(EmissionUseAlpha, useTextureAlpha ? 1f : 0f);
        }

        public static void SetRim(
            MaterialPropertyBlock block, bool enabled, Color color,
            float power, float intensity)
        {
            if (block == null) return;
            block.SetFloat(RimEnabled, enabled ? 1f : 0f);
            block.SetColor(RimColor, color);
            block.SetFloat(RimPower, Mathf.Clamp(power, 0.1f, 8f));
            block.SetFloat(RimIntensity, Mathf.Clamp(intensity, 0f, 8f));
        }

        public static void SetAlphaClip(MaterialPropertyBlock block, bool enabled, float cutoff)
        {
            ESCompositeURPProperties.SetAlphaClip(block, enabled, cutoff);
        }

        public static void SetEffects(MaterialPropertyBlock block, bool rim, bool burn, float rimIntensity, float shineIntensity)
        {
            if (block == null) return;
            block.SetFloat(RimEnabled, rim ? 1f : 0f);
            block.SetFloat(BurnEnabled, burn ? 1f : 0f);
            block.SetFloat(RimIntensity, rimIntensity);
            block.SetFloat(ShineIntensity, shineIntensity);
        }

        public static void SetMotionEffects(MaterialPropertyBlock block, bool sparkle, bool flow, bool chromatic, Vector2 flowSpeed, float flowStrength, float sparkleIntensity, float chromaticOffset, float chromaticIntensity)
        {
            if (block == null) return;
            block.SetFloat(SparkleEnabled, sparkle ? 1f : 0f);
            block.SetFloat(FlowEnabled, flow ? 1f : 0f);
            block.SetVector(FlowSpeed, new Vector4(flowSpeed.x, flowSpeed.y, 0f, 0f));
            block.SetFloat(FlowStrength, Mathf.Clamp01(flowStrength));
            block.SetFloat(SparkleIntensity, Mathf.Max(0f, sparkleIntensity));
            block.SetFloat(ChromaticEnabled, chromatic ? 1f : 0f);
            block.SetFloat(ChromaticOffset, Mathf.Max(0f, chromaticOffset));
            block.SetFloat(ChromaticIntensity, Mathf.Clamp01(chromaticIntensity));
        }

        public static void SetUVTransform(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 pivot,
            Vector2 scale,
            Vector2 offset,
            float rotationDegrees,
            bool distortionEnabled = false,
            Vector2 distortionFrequency = default,
            Vector2 distortionSpeed = default,
            float distortionAmount = 0f,
            float rotationSpeed = 0f,
            Texture distortionNoiseTexture = null,
            Vector2 distortionFrom = default,
            Vector2 distortionTo = default,
            float distortionFade = 1f,
            Texture distortionMask = null,
            ESCompositeTextureChannel distortionMaskChannel = ESCompositeTextureChannel.透明)
        {
            ESCompositeURPProperties.SetUVTransform(
                block, enabled, pivot, scale, offset, rotationDegrees,
                distortionEnabled, distortionFrequency, distortionSpeed, distortionAmount,
                rotationSpeed, distortionNoiseTexture, distortionFrom, distortionTo,
                distortionFade, distortionMask, distortionMaskChannel);
        }

        public static void SetFade(
            MaterialPropertyBlock block,
            ESCompositeFadeMode mode,
            float progress,
            float width,
            Vector2 position,
            float rotationDegrees,
            bool invert,
            float noiseFactor,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            Texture noiseTexture = null,
            Texture maskTexture = null,
            Color? edgeColor = null,
            float edgeWidth = 0.08f,
            float edgeIntensity = 1f,
            float distortionStrength = 0.03f)
        {
            ESCompositeURPProperties.SetFade(
                block, mode, progress, width, position, rotationDegrees, invert,
                noiseFactor, noiseScale, noiseSpeed, noiseTexture, maskTexture,
                edgeColor, edgeWidth, edgeIntensity, distortionStrength);
        }

        public static void SetTiling(
            MaterialPropertyBlock block,
            ESCompositeTilingMode mode,
            Vector2 worldScale,
            Vector2 worldOffset,
            float worldPixelsPerUnit,
            Vector2 screenScale,
            Vector2 screenOffset,
            float screenPixelsPerTile)
        {
            ESCompositeURPProperties.SetTiling(
                block, mode, worldScale, worldOffset, worldPixelsPerUnit,
                screenScale, screenOffset, screenPixelsPerTile);
        }

        public static void SetSampling(
            MaterialPropertyBlock block,
            bool blurEnabled,
            float blurRadius,
            float blurIntensity,
            bool sharpenEnabled,
            float sharpenAmount,
            float sharpenRadius,
            float sharpenThreshold,
            float sharpenFade = 1f)
        {
            ESCompositeURPProperties.SetSampling(
                block, blurEnabled, false, blurRadius, blurIntensity,
                sharpenEnabled, sharpenAmount, sharpenRadius, sharpenThreshold, sharpenFade);
        }

        public static void SetGeneratedStylization(
            MaterialPropertyBlock block,
            bool smoothPixelArt,
            float smoothPixelStrength,
            bool checkerboard,
            float checkerboardDarken,
            float checkerboardTiling)
        {
            ESCompositeURPProperties.SetGeneratedStylization(
                block, smoothPixelArt, smoothPixelStrength,
                checkerboard, checkerboardDarken, checkerboardTiling);
        }

        public static void SetRasterStylization(
            MaterialPropertyBlock block,
            bool pixelate,
            float pixelateCells,
            float pixelateStrength,
            bool halftone,
            float halftoneScale,
            float halftoneAngle,
            float halftoneStrength)
        {
            ESCompositeURPProperties.SetRasterStylization(
                block, pixelate, pixelateCells, pixelateStrength,
                halftone, halftoneScale, halftoneAngle, halftoneStrength);
        }

        public static void SetFlame(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float brightness,
            float smoothness,
            float radius,
            Vector2 speed,
            float noiseFactor,
            float noiseHeightFactor,
            Vector2 noiseScale)
        {
            ESCompositeURPProperties.SetFlame(
                block, enabled, noiseTexture, brightness, smoothness, radius,
                speed, noiseFactor, noiseHeightFactor, noiseScale);
        }

        public static void SetFlame(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float brightness,
            float smoothness,
            float radius,
            Vector2 speed,
            float noiseFactor,
            float noiseHeightFactor,
            Vector2 noiseScale,
            Vector2 direction,
            Vector2 center)
        {
            ESCompositeURPProperties.SetFlame(
                block, enabled, noiseTexture, brightness, smoothness, radius,
                speed, noiseFactor, noiseHeightFactor, noiseScale, direction, center);
        }

        public static void SetSmoke(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float alpha,
            float smoothness,
            float noiseScale,
            float noiseFactor,
            float darkEdge,
            bool vertexSeed)
        {
            ESCompositeURPProperties.SetSmoke(
                block, enabled, noiseTexture, alpha, smoothness,
                noiseScale, noiseFactor, darkEdge, vertexSeed);
        }

        public static void SetSmoke(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float alpha,
            float smoothness,
            float noiseScale,
            float noiseFactor,
            float darkEdge,
            bool vertexSeed,
            Vector2 speed)
        {
            ESCompositeURPProperties.SetSmoke(
                block, enabled, noiseTexture, alpha, smoothness,
                noiseScale, noiseFactor, darkEdge, vertexSeed, speed);
        }

        public static void SetTextureLayer(
            MaterialPropertyBlock block,
            ESCompositeTextureLayer layer,
            bool enabled,
            Texture texture,
            Color color,
            Vector2 scale,
            Vector2 offset,
            bool scroll,
            Vector2 scrollSpeed,
            bool sheet,
            int columns,
            int rows,
            float speed,
            int startFrame,
            float edgeClip,
            bool contrast,
            float contrastValue,
            float fade = 1f)
        {
            ESCompositeURPProperties.SetTextureLayer(
                block, layer, enabled, texture, color, scale, offset,
                scroll, scrollSpeed, sheet, columns, rows, speed, startFrame,
                edgeClip, contrast, contrastValue, fade);
        }

        /// <summary>
        /// 仅写入实例属性；不会改变承载材质的质量 Keyword 或资源编译配置。
        /// 首次为运行时实例启用精确合同，优先使用 TrySetESNativeExactContract。
        /// </summary>
        public static void SetESNativeExactContract(MaterialPropertyBlock block, bool enabled, Texture shineMask = null)
        {
            ESCompositeURPProperties.SetESNativeExactContract(block, enabled, shineMask);
        }

        /// <summary>
        /// 初始化供 MaterialPropertyBlock 动态切换 ESNative 效果使用的材质。
        /// 此方法会修改材质 Keyword，应在实例初始化或材质创建阶段调用，不应逐帧调用共享材质。
        /// </summary>
        public static bool PrepareMaterialForDynamicESNative(Material material)
        {
            if (material == null || !material.HasProperty(ESNativeExactContract)) return false;
            SetQuality(material, ESCompositeQualityTier.高质量);
            SetResourceProfile(material, ES3DLitResourceProfile.动态完整);
            return true;
        }

        /// <summary>
        /// 在确认材质已进入高质量、动态完整变体后写入实例级 ESNative 精确合同。
        /// </summary>
        public static bool TrySetESNativeExactContract(
            Material material,
            MaterialPropertyBlock block,
            bool enabled,
            Texture shineMask = null)
        {
            if (material == null || block == null || !material.HasProperty(ESNativeExactContract)) return false;
            if (enabled && !PrepareMaterialForDynamicESNative(material)) return false;
            SetESNativeExactContract(block, enabled, shineMask);
            return true;
        }

        public static void SetESNativeExactContract(Material material, bool enabled, Texture shineMask = null)
        {
            if (material == null || !material.HasProperty(ESNativeExactContract)) return;
            material.SetFloat(ESNativeExactContract, enabled ? 1f : 0f);
            if (material.HasProperty(ESCompositeURPProperties.ShineMaskEnabled))
                material.SetFloat(ESCompositeURPProperties.ShineMaskEnabled, shineMask != null ? 1f : 0f);
            if (shineMask != null && material.HasProperty(ESCompositeURPProperties.ShineMask))
                material.SetTexture(ESCompositeURPProperties.ShineMask, shineMask);
            if (enabled) SetQuality(material, ESCompositeQualityTier.高质量);
            RefreshResourceProfile(material);
        }

        public static void SetOutlines(
            MaterialPropertyBlock block,
            bool innerEnabled,
            Color innerColor,
            float innerWidth,
            bool outerEnabled,
            Color outerColor,
            float outerWidth,
            bool pixelEnabled,
            Color pixelColor,
            float pixelWidth)
        {
            ESCompositeURPProperties.SetOutlines(
                block, innerEnabled, innerColor, innerWidth,
                outerEnabled, outerColor, outerWidth,
                pixelEnabled, pixelColor, pixelWidth);
        }

        public static void SetInnerOutline(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float width,
            float fade,
            bool distortionEnabled,
            Vector2 distortionIntensity,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            bool textureEnabled,
            Texture tintTexture,
            Vector2 textureSpeed,
            bool outlineOnly)
        {
            ESCompositeURPProperties.SetInnerOutline(
                block, enabled, color, width, fade,
                distortionEnabled, distortionIntensity, noiseScale, noiseSpeed,
                textureEnabled, tintTexture, textureSpeed, outlineOnly);
        }

        public static void SetOuterOutline(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float width,
            float fade,
            bool distortionEnabled,
            Vector2 distortionIntensity,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            bool textureEnabled,
            Texture tintTexture,
            Vector2 textureSpeed,
            bool outlineOnly)
        {
            ESCompositeURPProperties.SetOuterOutline(
                block, enabled, color, width, fade,
                distortionEnabled, distortionIntensity, noiseScale, noiseSpeed,
                textureEnabled, tintTexture, textureSpeed, outlineOnly);
        }

        public static void SetPixelOutline(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float width,
            float fade,
            bool textureEnabled,
            Texture tintTexture,
            Vector2 textureSpeed,
            bool outlineOnly)
        {
            ESCompositeURPProperties.SetPixelOutline(
                block, enabled, color, width, fade,
                textureEnabled, tintTexture, textureSpeed, outlineOnly);
        }

        public static void SetSpriteShadow(MaterialPropertyBlock block, bool enabled, Vector2 offset, Color color, float fade = 1f)
        {
            ESCompositeURPProperties.SetSpriteShadow(block, enabled, offset, color, fade);
        }

        public static void SetFullGlowDissolve(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            float width,
            Color edgeColor,
            Vector2 noiseScale)
        {
            ESCompositeURPProperties.SetFullGlowDissolve(block, enabled, fade, width, edgeColor, noiseScale);
        }

        public static void SetShine(
            MaterialPropertyBlock block, bool enabled, Color color,
            float speed, float width, float intensity, Vector3 direction)
        {
            ESCompositeURPProperties.SetShine(
                block, enabled, color, speed, width, intensity, direction);
        }

        public static void SetShine(
            MaterialPropertyBlock block, bool enabled, Color color,
            float speed, float width, float intensity, Vector3 direction,
            ESCompositeProjectionSpace space)
        {
            ESCompositeURPProperties.SetShine(
                block, enabled, color, speed, width, intensity, direction, space);
        }

        public static void SetHologram(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float lineFrequency,
            float lineGap,
            float speed,
            float minAlpha)
        {
            ESCompositeURPProperties.SetHologram(
                block, enabled, color, lineFrequency, lineGap, speed, minAlpha);
        }

        public static void SetHologram(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float lineFrequency,
            float lineGap,
            float speed,
            float minAlpha,
            float fade,
            float contrast,
            ES3DLitHologramSpace space,
            float distortionOffset,
            float distortionSpeed,
            float distortionDensity,
            float distortionScale)
        {
            ESCompositeURPProperties.SetHologram(
                block, enabled, color, lineFrequency, lineGap, speed, minAlpha,
                fade, contrast, space, distortionOffset, distortionSpeed,
                distortionDensity, distortionScale);
        }

        public static void SetHologram(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float lineFrequency,
            float lineGap,
            float speed,
            float minAlpha,
            float fade,
            float contrast,
            ES3DLitHologramSpace space,
            float distortionOffset,
            float distortionSpeed,
            float distortionDensity,
            float distortionScale,
            Vector3 scanDirection,
            Vector2 distortionDirection)
        {
            ESCompositeURPProperties.SetHologram(
                block, enabled, color, lineFrequency, lineGap, speed, minAlpha,
                fade, contrast, space, distortionOffset, distortionSpeed,
                distortionDensity, distortionScale, scanDirection, distortionDirection);
        }

        public static void SetGlitch(MaterialPropertyBlock block, bool enabled, float intensity, float speed)
        {
            ESCompositeURPProperties.SetGlitch(block, enabled, intensity, speed);
        }

        public static void SetGlitchScanDirection(MaterialPropertyBlock block, Vector3 direction)
        {
            ESCompositeURPProperties.SetGlitchScanDirection(block, direction);
        }

        public static void SetGlitch(
            MaterialPropertyBlock block,
            bool enabled,
            float intensity,
            float speed,
            float fade,
            float maskMin,
            Vector2 maskScale,
            Vector2 maskSpeed,
            float hueSpeed,
            float brightness,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            Vector2 distortion,
            Vector2 distortionScale,
            Vector2 distortionSpeed)
        {
            ESCompositeURPProperties.SetGlitch(
                block, enabled, intensity, speed, fade, maskMin,
                maskScale, maskSpeed, hueSpeed, brightness,
                noiseScale, noiseSpeed, distortion, distortionScale, distortionSpeed);
        }

        public static void SetWind(MaterialPropertyBlock block, bool enabled, Vector2 direction, float amplitude, float frequency, float speed, float anchor = 0f, float globalInfluence = 1f)
        {
            ESCompositeURPProperties.SetWind(block, enabled, direction, amplitude, frequency, speed, anchor, globalInfluence);
        }

        public static void SetWind(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 direction,
            float amplitude,
            float frequency,
            float speed,
            float anchor,
            float globalInfluence,
            Vector2 anchorDirection)
        {
            ESCompositeURPProperties.SetWind(
                block, enabled, direction, amplitude, frequency, speed,
                anchor, globalInfluence, anchorDirection);
        }

        public static void SetSquish(MaterialPropertyBlock block, bool enabled, float amount, float speed)
        {
            ESCompositeURPProperties.SetSquish(block, enabled, amount, speed);
        }

        public static void SetSquish(
            MaterialPropertyBlock block, bool enabled, float amount, float speed, Vector2 direction)
        {
            ESCompositeURPProperties.SetSquish(block, enabled, amount, speed, direction);
        }

        public static void SetSquishFade(MaterialPropertyBlock block, float fade)
        {
            ESCompositeURPProperties.SetSquishFade(block, fade);
        }

        public static void SetInteractiveWind(MaterialPropertyBlock block, float rotationDegrees, float height)
        {
            ESCompositeURPProperties.SetInteractiveWind(block, rotationDegrees, height);
        }

        public static void SetInteractiveSquish(MaterialPropertyBlock block, float amount)
        {
            ESCompositeURPProperties.SetInteractiveSquish(block, amount);
        }

        public static void SetWindPhaseOffset(MaterialPropertyBlock block, float value)
        {
            ESCompositeURPProperties.SetWindPhaseOffset(block, value);
        }

        public static void SetWiggle(MaterialPropertyBlock block, bool enabled, float amplitudeDegrees, float frequency, float speed)
        {
            ESCompositeURPProperties.SetWiggle(block, enabled, amplitudeDegrees, frequency, speed);
        }

        public static void SetWiggle(
            MaterialPropertyBlock block,
            bool enabled,
            float amplitudeDegrees,
            float frequency,
            float speed,
            Vector2 phaseDirection)
        {
            ESCompositeURPProperties.SetWiggle(
                block, enabled, amplitudeDegrees, frequency, speed, phaseDirection);
        }

        public static void SetVibrate(MaterialPropertyBlock block, bool enabled, float amplitude, float speed)
        {
            ESCompositeURPProperties.SetVibrate(block, enabled, amplitude, speed);
        }

        public static void SetVibrate(
            MaterialPropertyBlock block, bool enabled, float amplitude, float speed, Vector2 direction)
        {
            ESCompositeURPProperties.SetVibrate(block, enabled, amplitude, speed, direction);
        }

        public static void SetSineMove(MaterialPropertyBlock block, bool enabled, Vector2 offset, Vector2 frequency, float fade = 1f)
        {
            ESCompositeURPProperties.SetSineMove(block, enabled, offset, frequency, fade);
        }

        public static void SetSineScale(MaterialPropertyBlock block, bool enabled, Vector2 factor, float frequency)
        {
            ESCompositeURPProperties.SetSineScale(block, enabled, factor, frequency);
        }

        public static void SetSqueeze(MaterialPropertyBlock block, bool enabled, Vector2 center, Vector2 scale, float power = 1f, float fade = 1f)
        {
            ESCompositeURPProperties.SetSqueeze(block, enabled, center, scale, power, fade);
        }

        public static void SetSineRotate(MaterialPropertyBlock block, bool enabled, Vector2 pivot, float angle, float frequency, float fade = 1f)
        {
            ESCompositeURPProperties.SetSineRotate(block, enabled, pivot, angle, frequency, fade);
        }

        public static void SetCustomFade(MaterialPropertyBlock block, bool enabled, Texture mask, float smoothness, Vector2 noiseScale, float noiseFactor, float alpha)
        {
            ESCompositeURPProperties.SetCustomFade(block, enabled, mask, smoothness, noiseScale, noiseFactor, alpha);
        }

        public static void SetFlowMap(MaterialPropertyBlock block, bool enabled, Texture texture, Vector2 scale, Vector2 offset, Vector2 speed, float strength)
        {
            if (block == null) return;
            block.SetFloat(FlowMapEnabled, enabled ? 1f : 0f);
            block.SetTexture(FlowMap, texture);
            block.SetVector(FlowMapScale, new Vector4(scale.x, scale.y, offset.x, offset.y));
            block.SetVector(FlowMapSpeed, new Vector4(speed.x, speed.y, 0f, 0f));
            block.SetFloat(FlowMapStrength, Mathf.Max(0f, strength));
        }

        public static void SetVertexAnimation(MaterialPropertyBlock block, bool enabled, Vector3 localDirection, float amplitude, float frequency, float speed, ESCompositeVertexColorMask mask)
        {
            if (block == null) return;
            block.SetFloat(VertexAnimationEnabled, enabled ? 1f : 0f);
            block.SetVector(VertexAnimationDirection, new Vector4(localDirection.x, localDirection.y, localDirection.z, 0f));
            block.SetFloat(VertexAnimationAmplitude, Mathf.Max(0f, amplitude));
            block.SetFloat(VertexAnimationFrequency, Mathf.Max(0f, frequency));
            block.SetFloat(VertexAnimationSpeed, speed);
            block.SetFloat(VertexAnimationMask, (float)mask);
        }

        public static void SetSurfaceFeatures(MaterialPropertyBlock block, bool normalMap, bool metallicMap, bool occlusionMap, bool emission)
        {
            if (block == null) return;
            block.SetFloat(NormalMapEnabled, normalMap ? 1f : 0f);
            block.SetFloat(MetallicMapEnabled, metallicMap ? 1f : 0f);
            block.SetFloat(OcclusionMapEnabled, occlusionMap ? 1f : 0f);
            block.SetFloat(EmissionEnabled, emission ? 1f : 0f);
        }

        public static void SetSurfaceFeatures(MaterialPropertyBlock block, bool normalMap, bool occlusionMap, bool emission)
        {
            SetSurfaceFeatures(block, normalMap, false, occlusionMap, emission);
        }

        public static void SetMetallicMap(MaterialPropertyBlock block, bool enabled, Texture texture, float metallic, float smoothness)
        {
            if (block == null) return;
            bool useTexture = enabled && texture != null;
            block.SetFloat(MetallicMapEnabled, useTexture ? 1f : 0f);
            if (texture != null) block.SetTexture(MetallicMap, texture);
            block.SetFloat(Metallic, Mathf.Clamp01(metallic));
            block.SetFloat(Smoothness, Mathf.Clamp01(smoothness));
        }

        public static void SetQuality(Material material, ESCompositeQualityTier quality)
        {
            ESCompositeURPProperties.ApplyQuality(material, QualityTier, quality);
        }

        /// <summary>
        /// 动态完整保留所有资源路径，适合 MaterialPropertyBlock 在运行时切换效果。
        /// 材质优化只编译当前材质已启用效果需要的资源组，后续修改材质开关时必须刷新配置。
        /// </summary>
        public static void SetResourceProfile(Material material, ES3DLitResourceProfile profile)
        {
            if (material == null || !material.HasProperty(ResourceProfile)) return;
            material.SetFloat(ResourceProfile, profile == ES3DLitResourceProfile.材质优化 ? 1f : 0f);
            RefreshResourceProfile(material);
        }

        public static bool RefreshResourceProfile(Material material)
        {
            if (material == null || !material.HasProperty(ResourceProfile)) return false;

            bool optimized = material.GetFloat(ResourceProfile) > 0.5f;
            int mask = 0;
            if (optimized)
            {
                if (IsEnabled(material, "_EnableUVDistort") || IsEnabled(material, "_EnableFlowMap"))
                    mask |= UVResourceBit;
                if (GetFloat(material, "_FadeMode") > 0.5f
                    || GetFloat(material, "_DissolveMode") > 0.5f
                    || IsEnabled(material, "_EnableCustomFade")
                    || IsAnyEnabled(material, FadeResourceSwitches))
                    mask |= FadeResourceBit;
                if (IsAnyEnabled(material, SurfaceResourceSwitches))
                    mask |= SurfaceResourceBit;
                if (IsEnabled(material, "_EnableTextureLayer1") || IsEnabled(material, "_EnableTextureLayer2"))
                    mask |= LayerResourceBit;
            }

            bool changed = false;
            for (int i = 0; i < ResourceMaskKeywords.Length; i++)
            {
                bool enabled = optimized && i == mask;
                string keyword = ResourceMaskKeywords[i];
                if (material.IsKeywordEnabled(keyword) == enabled) continue;
                ESCompositeURPProperties.SetKeyword(material, keyword, enabled);
                changed = true;
            }
            return changed;
        }

        private static bool IsAnyEnabled(Material material, string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
                if (IsEnabled(material, propertyNames[i])) return true;
            return false;
        }

        private static bool IsEnabled(Material material, string propertyName)
        {
            return GetFloat(material, propertyName) > 0.5f;
        }

        private static float GetFloat(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) ? material.GetFloat(propertyName) : 0f;
        }

        public static void SetReceiveShadows(Material material, bool receiveShadows)
        {
            if (material == null) return;
            material.SetFloat(ReceiveShadows, receiveShadows ? 1f : 0f);
            ESCompositeURPProperties.SetKeyword(material, "_RECEIVE_SHADOWS_OFF", !receiveShadows);
        }

        public static void SetSurfaceMode(
            Material material,
            ES3DLitSurfaceMode mode,
            int queueOffset = 0,
            CullMode cullMode = CullMode.Back)
        {
            if (material == null) return;

            int offset = Mathf.Clamp(queueOffset, -50, 50);
            bool transparent = mode == ES3DLitSurfaceMode.透明混合;
            bool alphaClip = mode == ES3DLitSurfaceMode.透明裁剪;
            material.SetFloat(Surface, transparent ? 1f : 0f);
            material.SetFloat(AlphaClip, alphaClip ? 1f : 0f);
            material.SetFloat(Cull, Mathf.Clamp((int)cullMode, (int)CullMode.Off, (int)CullMode.Back));
            material.SetFloat(QueueOffset, offset);
            material.SetFloat(SrcBlend, transparent
                ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha
                : (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat(DstBlend, transparent
                ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
                : (float)UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat(ZWrite, transparent ? 0f : 1f);

            string renderType = transparent ? "Transparent" : (alphaClip ? "TransparentCutout" : "Opaque");
            int queue = transparent ? TransparentQueue : (alphaClip ? AlphaTestQueue : GeometryQueue);
            material.SetOverrideTag("RenderType", renderType);
            material.renderQueue = queue + offset;
            SetOpaquePasses(material, !transparent);
        }

        internal static void SetOpaquePasses(Material material, bool enabled)
        {
            material.SetShaderPassEnabled("GBuffer", enabled);
            material.SetShaderPassEnabled("ShadowCaster", enabled);
            material.SetShaderPassEnabled("DepthOnly", enabled);
            material.SetShaderPassEnabled("DepthNormals", enabled);
            material.SetShaderPassEnabled("Meta", enabled);
        }
    }

    public static class ES3DVFXCompositeURPProperties
    {
        public const string ShaderName = "ES/3D/VFX Composite URP";
        private const int TransparentQueue = (int)RenderQueue.Transparent;
        public static readonly int SequenceEnabled = Shader.PropertyToID("_EnableSequence");
        public static readonly int SequencePlayback = Shader.PropertyToID("_SequencePlayback");
        public static readonly int SequenceColumns = Shader.PropertyToID("_SequenceColumns");
        public static readonly int SequenceRows = Shader.PropertyToID("_SequenceRows");
        public static readonly int SequenceFrame = Shader.PropertyToID("_SequenceFrame");
        public static readonly int SequenceSpeed = Shader.PropertyToID("_SequenceSpeed");
        public static readonly int PolarUVEnabled = Shader.PropertyToID("_EnablePolarUV");
        public static readonly int PolarCenter = Shader.PropertyToID("_PolarCenter");
        public static readonly int PolarRadialScale = Shader.PropertyToID("_PolarRadialScale");
        public static readonly int PolarAngularScale = Shader.PropertyToID("_PolarAngularScale");
        public static readonly int PolarRotationSpeed = Shader.PropertyToID("_PolarRotationSpeed");
        public static readonly int VertexStreamsEnabled = Shader.PropertyToID("_EnableVertexStreams");
        public static readonly int VertexStreamUVStrength = Shader.PropertyToID("_VertexStreamUVStrength");
        public static readonly int VertexStreamFrameStrength = Shader.PropertyToID("_VertexStreamFrameStrength");
        public static readonly int VertexStreamDissolveStrength = Shader.PropertyToID("_VertexStreamDissolveStrength");
        public static readonly int VertexStreamEmissionStrength = Shader.PropertyToID("_VertexStreamEmissionStrength");
        public static readonly int DissolveMode = Shader.PropertyToID("_DissolveMode");
        public static readonly int DissolveProgress = Shader.PropertyToID("_DissolveProgress");
        public static readonly int HologramEnabled = Shader.PropertyToID("_EnableHologram");
        public static readonly int HologramFrequency = Shader.PropertyToID("_HologramFrequency");
        public static readonly int HologramGap = Shader.PropertyToID("_HologramGap");
        public static readonly int GlitchEnabled = Shader.PropertyToID("_EnableGlitch");
        public static readonly int GlitchAmount = Shader.PropertyToID("_GlitchAmount");
        public static readonly int ESNativeStatusContract = ESCompositeURPProperties.ESNativeStatusContract;
        public static readonly int ESNativeExactContract = ESCompositeURPProperties.ESNativeExactContract;
        public static readonly int HologramColor = ESCompositeURPProperties.HologramColor;
        public static readonly int HologramLineFrequency = ESCompositeURPProperties.HologramLineFrequency;
        public static readonly int HologramLineGap = ESCompositeURPProperties.HologramLineGap;
        public static readonly int HologramSpeed = ESCompositeURPProperties.HologramSpeed;
        public static readonly int HologramMinAlpha = ESCompositeURPProperties.HologramMinAlpha;
        public static readonly int HologramFade = ESCompositeURPProperties.HologramFade;
        public static readonly int HologramContrast = ESCompositeURPProperties.HologramContrast;
        public static readonly int HologramSpace = ESCompositeURPProperties.HologramSpace;
        public static readonly int HologramDirection = ESCompositeURPProperties.HologramDirection;
        public static readonly int HologramDistortionOffset = ESCompositeURPProperties.HologramDistortionOffset;
        public static readonly int HologramDistortionDirection = ESCompositeURPProperties.HologramDistortionDirection;
        public static readonly int HologramDistortionSpeed = ESCompositeURPProperties.HologramDistortionSpeed;
        public static readonly int HologramDistortionDensity = ESCompositeURPProperties.HologramDistortionDensity;
        public static readonly int HologramDistortionScale = ESCompositeURPProperties.HologramDistortionScale;
        public static readonly int GlitchSpeed = ESCompositeURPProperties.GlitchSpeed;
        public static readonly int GlitchScanDirection = ESCompositeURPProperties.GlitchScanDirection;
        public static readonly int GlitchFade = ESCompositeURPProperties.GlitchFade;
        public static readonly int GlitchMaskMin = ESCompositeURPProperties.GlitchMaskMin;
        public static readonly int GlitchMaskScale = ESCompositeURPProperties.GlitchMaskScale;
        public static readonly int GlitchMaskSpeed = ESCompositeURPProperties.GlitchMaskSpeed;
        public static readonly int GlitchHueSpeed = ESCompositeURPProperties.GlitchHueSpeed;
        public static readonly int GlitchBrightness = ESCompositeURPProperties.GlitchBrightness;
        public static readonly int GlitchNoiseScale = ESCompositeURPProperties.GlitchNoiseScale;
        public static readonly int GlitchNoiseSpeed = ESCompositeURPProperties.GlitchNoiseSpeed;
        public static readonly int GlitchDistortion = ESCompositeURPProperties.GlitchDistortion;
        public static readonly int GlitchDistortionScale = ESCompositeURPProperties.GlitchDistortionScale;
        public static readonly int GlitchDistortionSpeed = ESCompositeURPProperties.GlitchDistortionSpeed;
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int VertexColorStrength = Shader.PropertyToID("_VertexColorStrength");
        public static readonly int NoiseTexture = ESCompositeURPProperties.NoiseTexture;
        public static readonly int NoiseScale = ESCompositeURPProperties.NoiseScale;
        public static readonly int NoiseSpeed = ESCompositeURPProperties.NoiseSpeed;
        public static readonly int Distortion = Shader.PropertyToID("_Distortion");
        public static readonly int DistortionDirection = ESCompositeURPProperties.DistortionDirection;
        public static readonly int SparkleEnabled = Shader.PropertyToID("_EnableSparkle");
        public static readonly int ShineEnabled = Shader.PropertyToID("_EnableShine");
        public static readonly int ShineColor = Shader.PropertyToID("_ShineColor");
        public static readonly int ShineSpeed = Shader.PropertyToID("_ShineSpeed");
        public static readonly int ShineWidth = Shader.PropertyToID("_ShineWidth");
        public static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
        public static readonly int ShineSpace = ESCompositeURPProperties.ShineSpace;
        public static readonly int ShineDirection = Shader.PropertyToID("_ShineDirection");
        public static readonly int DissolveWidth = Shader.PropertyToID("_DissolveWidth");
        public static readonly int DissolveColor = Shader.PropertyToID("_DissolveColor");
        public static readonly int RimEnabled = Shader.PropertyToID("_EnableRim");
        public static readonly int RimColor = Shader.PropertyToID("_RimColor");
        public static readonly int RimPower = Shader.PropertyToID("_RimPower");
        public static readonly int RimIntensity = Shader.PropertyToID("_RimIntensity");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int AlphaClip = ESCompositeURPProperties.AlphaClipEnabled;
        public static readonly int Cutoff = ESCompositeURPProperties.Cutoff;
        public static readonly int SparkleColor = Shader.PropertyToID("_SparkleColor");
        public static readonly int SparkleScale = Shader.PropertyToID("_SparkleScale");
        public static readonly int SparkleSpeed = Shader.PropertyToID("_SparkleSpeed");
        public static readonly int SparkleDensity = Shader.PropertyToID("_SparkleDensity");
        public static readonly int SparkleSharpness = Shader.PropertyToID("_SparkleSharpness");
        public static readonly int SparkleIntensity = Shader.PropertyToID("_SparkleIntensity");
        public static readonly int FlowEnabled = Shader.PropertyToID("_EnableFlow");
        public static readonly int FlowSpeed = Shader.PropertyToID("_FlowSpeed");
        public static readonly int FlowStrength = Shader.PropertyToID("_FlowStrength");
        public static readonly int FlowMapEnabled = Shader.PropertyToID("_EnableFlowMap");
        public static readonly int FlowMap = Shader.PropertyToID("_FlowMap");
        public static readonly int FlowMapScale = Shader.PropertyToID("_FlowMapScale");
        public static readonly int FlowMapSpeed = Shader.PropertyToID("_FlowMapSpeed");
        public static readonly int FlowMapStrength = Shader.PropertyToID("_FlowMapStrength");
        public static readonly int VertexAnimationEnabled = Shader.PropertyToID("_EnableVertexAnimation");
        public static readonly int VertexAnimationDirection = Shader.PropertyToID("_VertexAnimationDirection");
        public static readonly int VertexAnimationAmplitude = Shader.PropertyToID("_VertexAnimationAmplitude");
        public static readonly int VertexAnimationFrequency = Shader.PropertyToID("_VertexAnimationFrequency");
        public static readonly int VertexAnimationSpeed = Shader.PropertyToID("_VertexAnimationSpeed");
        public static readonly int VertexAnimationMask = Shader.PropertyToID("_VertexAnimationMask");
        public static readonly int SoftParticlesEnabled = Shader.PropertyToID("_EnableSoftParticles");
        public static readonly int SoftParticleNear = Shader.PropertyToID("_SoftParticleNear");
        public static readonly int SoftParticleFar = Shader.PropertyToID("_SoftParticleFar");
        public static readonly int RadialMaskEnabled = Shader.PropertyToID("_EnableRadialMask");
        public static readonly int RadialMaskCenter = Shader.PropertyToID("_RadialMaskCenter");
        public static readonly int RadialMaskRadius = Shader.PropertyToID("_RadialMaskRadius");
        public static readonly int RadialMaskSoftness = Shader.PropertyToID("_RadialMaskSoftness");
        public static readonly int RadialMaskInvert = Shader.PropertyToID("_RadialMaskInvert");
        public static readonly int FresnelMaskEnabled = Shader.PropertyToID("_EnableFresnelMask");
        public static readonly int FresnelPower = Shader.PropertyToID("_FresnelPower");
        public static readonly int FresnelMin = Shader.PropertyToID("_FresnelMin");
        public static readonly int FresnelMax = Shader.PropertyToID("_FresnelMax");
        public static readonly int FresnelAlphaInfluence = Shader.PropertyToID("_FresnelAlphaInfluence");
        public static readonly int FresnelColor = Shader.PropertyToID("_FresnelColor");
        public static readonly int FresnelIntensity = Shader.PropertyToID("_FresnelIntensity");
        public static readonly int DepthIntersectionEnabled = Shader.PropertyToID("_EnableDepthIntersection");
        public static readonly int DepthIntersectionColor = Shader.PropertyToID("_DepthIntersectionColor");
        public static readonly int DepthIntersectionDistance = Shader.PropertyToID("_DepthIntersectionDistance");
        public static readonly int DepthIntersectionIntensity = Shader.PropertyToID("_DepthIntersectionIntensity");
        public static readonly int ChromaticEnabled = Shader.PropertyToID("_EnableChromatic");
        public static readonly int ChromaticOffset = Shader.PropertyToID("_ChromaticOffset");
        public static readonly int ChromaticIntensity = Shader.PropertyToID("_ChromaticIntensity");
        public static readonly int ChromaticEdgeOnly = Shader.PropertyToID("_ChromaticEdgeOnly");
        public static readonly int ChromaticAngle = Shader.PropertyToID("_ChromaticAngle");
        public static readonly int BlurEnabled = Shader.PropertyToID("_EnableBlur");
        public static readonly int BlurRadius = Shader.PropertyToID("_BlurRadius");
        public static readonly int BlurIntensity = Shader.PropertyToID("_BlurIntensity");
        public static readonly int QualityTier = Shader.PropertyToID("_QualityTier");
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int TimeFPSEnabled = ESCompositeURPProperties.TimeFPSEnabled;
        public static readonly int TimeFPS = ESCompositeURPProperties.TimeFPS;
        public static readonly int TimeFrequencyEnabled = ESCompositeURPProperties.TimeFrequencyEnabled;
        public static readonly int TimeFrequency = ESCompositeURPProperties.TimeFrequency;
        public static readonly int TimeRange = ESCompositeURPProperties.TimeRange;
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");
        public static readonly int BlendMode = Shader.PropertyToID("_BlendMode");
        public static readonly int ZWriteMode = Shader.PropertyToID("_ZWriteMode");
        public static readonly int ZTest = Shader.PropertyToID("_ZTest");
        public static readonly int Cull = Shader.PropertyToID("_Cull");
        public static readonly int QueueOffset = Shader.PropertyToID("_QueueOffset");
        public static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        public static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        public static readonly int BlendOp = Shader.PropertyToID("_BlendOp");

        public static void SetSequence(MaterialPropertyBlock block, bool enabled, ES3DVFXSequencePlaybackMode playback, int columns, int rows, float frame, float speed)
        {
            if (block == null) return;
            block.SetFloat(SequenceEnabled, enabled ? 1f : 0f);
            block.SetFloat(SequencePlayback, (float)playback);
            block.SetFloat(SequenceColumns, Mathf.Max(1, columns));
            block.SetFloat(SequenceRows, Mathf.Max(1, rows));
            block.SetFloat(SequenceFrame, frame);
            block.SetFloat(SequenceSpeed, speed);
        }

        public static void SetPolarUV(MaterialPropertyBlock block, bool enabled, Vector2 center, float radialScale, float angularScale, float rotationSpeed)
        {
            if (block == null) return;
            block.SetFloat(PolarUVEnabled, enabled ? 1f : 0f);
            block.SetVector(PolarCenter, new Vector4(center.x, center.y, 0f, 0f));
            block.SetFloat(PolarRadialScale, radialScale);
            block.SetFloat(PolarAngularScale, angularScale);
            block.SetFloat(PolarRotationSpeed, rotationSpeed);
        }

        public static void SetVertexStreamControls(MaterialPropertyBlock block, bool enabled, float uvStrength, float frameStrength, float dissolveStrength, float emissionStrength)
        {
            if (block == null) return;
            block.SetFloat(VertexStreamsEnabled, enabled ? 1f : 0f);
            block.SetFloat(VertexStreamUVStrength, Mathf.Clamp01(uvStrength));
            block.SetFloat(VertexStreamFrameStrength, Mathf.Clamp01(frameStrength));
            block.SetFloat(VertexStreamDissolveStrength, Mathf.Clamp01(dissolveStrength));
            block.SetFloat(VertexStreamEmissionStrength, Mathf.Max(0f, emissionStrength));
        }

        public static void SetRadialMask(MaterialPropertyBlock block, bool enabled, Vector2 center, float radius, float softness, bool invert)
        {
            if (block == null) return;
            block.SetFloat(RadialMaskEnabled, enabled ? 1f : 0f);
            block.SetVector(RadialMaskCenter, new Vector4(center.x, center.y, 0f, 0f));
            block.SetFloat(RadialMaskRadius, Mathf.Max(0f, radius));
            block.SetFloat(RadialMaskSoftness, Mathf.Max(0.001f, softness));
            block.SetFloat(RadialMaskInvert, invert ? 1f : 0f);
        }

        public static void SetFresnelMask(MaterialPropertyBlock block, bool enabled, float power, Vector2 remap, float alphaInfluence, Color color, float intensity)
        {
            if (block == null) return;
            float minimum = Mathf.Clamp01(Mathf.Min(remap.x, remap.y));
            float maximum = Mathf.Clamp01(Mathf.Max(remap.x, remap.y));
            block.SetFloat(FresnelMaskEnabled, enabled ? 1f : 0f);
            block.SetFloat(FresnelPower, Mathf.Max(0.1f, power));
            block.SetFloat(FresnelMin, minimum);
            block.SetFloat(FresnelMax, Mathf.Max(minimum + 0.0001f, maximum));
            block.SetFloat(FresnelAlphaInfluence, Mathf.Clamp01(alphaInfluence));
            block.SetColor(FresnelColor, color);
            block.SetFloat(FresnelIntensity, Mathf.Max(0f, intensity));
        }

        public static void SetDepthInteraction(MaterialPropertyBlock block, bool softParticles, float nearDistance, float farDistance, bool intersection, Color intersectionColor, float intersectionDistance, float intersectionIntensity)
        {
            if (block == null) return;
            SetSoftParticles(block, softParticles, nearDistance, farDistance);
            block.SetFloat(DepthIntersectionEnabled, intersection ? 1f : 0f);
            block.SetColor(DepthIntersectionColor, intersectionColor);
            block.SetFloat(DepthIntersectionDistance, Mathf.Max(0.001f, intersectionDistance));
            block.SetFloat(DepthIntersectionIntensity, Mathf.Max(0f, intersectionIntensity));
        }

        public static void SetMotionEffects(MaterialPropertyBlock block, bool sparkle, bool flow, bool chromatic, Vector2 flowSpeed, float flowStrength, float sparkleIntensity, float chromaticOffset, float chromaticIntensity)
        {
            if (block == null) return;
            block.SetFloat(SparkleEnabled, sparkle ? 1f : 0f);
            block.SetFloat(FlowEnabled, flow ? 1f : 0f);
            block.SetVector(FlowSpeed, new Vector4(flowSpeed.x, flowSpeed.y, 0f, 0f));
            block.SetFloat(FlowStrength, Mathf.Clamp01(flowStrength));
            block.SetFloat(SparkleIntensity, Mathf.Max(0f, sparkleIntensity));
            block.SetFloat(ChromaticEnabled, chromatic ? 1f : 0f);
            block.SetFloat(ChromaticOffset, Mathf.Max(0f, chromaticOffset));
            block.SetFloat(ChromaticIntensity, Mathf.Clamp01(chromaticIntensity));
        }

        public static void SetFlowMap(MaterialPropertyBlock block, bool enabled, Texture texture, Vector2 scale, Vector2 offset, Vector2 speed, float strength)
        {
            if (block == null) return;
            block.SetFloat(FlowMapEnabled, enabled ? 1f : 0f);
            block.SetTexture(FlowMap, texture);
            block.SetVector(FlowMapScale, new Vector4(scale.x, scale.y, offset.x, offset.y));
            block.SetVector(FlowMapSpeed, new Vector4(speed.x, speed.y, 0f, 0f));
            block.SetFloat(FlowMapStrength, Mathf.Max(0f, strength));
        }

        public static void SetVertexAnimation(MaterialPropertyBlock block, bool enabled, Vector3 localDirection, float amplitude, float frequency, float speed, ESCompositeVertexColorMask mask)
        {
            if (block == null) return;
            block.SetFloat(VertexAnimationEnabled, enabled ? 1f : 0f);
            block.SetVector(VertexAnimationDirection, new Vector4(localDirection.x, localDirection.y, localDirection.z, 0f));
            block.SetFloat(VertexAnimationAmplitude, Mathf.Max(0f, amplitude));
            block.SetFloat(VertexAnimationFrequency, Mathf.Max(0f, frequency));
            block.SetFloat(VertexAnimationSpeed, speed);
            block.SetFloat(VertexAnimationMask, (float)mask);
        }

        public static void SetSoftParticles(MaterialPropertyBlock block, bool enabled, float nearDistance, float farDistance)
        {
            if (block == null) return;
            float near = Mathf.Max(0f, nearDistance);
            block.SetFloat(SoftParticlesEnabled, enabled ? 1f : 0f);
            block.SetFloat(SoftParticleNear, near);
            block.SetFloat(SoftParticleFar, Mathf.Max(near + 0.001f, farDistance));
        }

        public static void SetDissolve(MaterialPropertyBlock block, ES3DVFXDissolveMode mode, float progress)
        {
            if (block == null) return;
            block.SetFloat(DissolveMode, (float)mode);
            block.SetFloat(DissolveProgress, progress);
        }

        public static void SetDissolve(
            MaterialPropertyBlock block,
            ES3DVFXDissolveMode mode,
            float progress,
            float width,
            Color color,
            Texture noiseTexture,
            Vector2 noiseScale,
            Vector2 noiseSpeed)
        {
            if (block == null) return;
            SetDissolve(block, mode, progress);
            block.SetFloat(DissolveWidth, Mathf.Clamp(width, 0.001f, 1f));
            block.SetColor(DissolveColor, color);
            ESCompositeURPProperties.SetNoise(block, noiseTexture, noiseScale, noiseSpeed);
        }

        public static void SetNoiseDistortion(
            MaterialPropertyBlock block,
            Texture noiseTexture,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            float distortion)
        {
            SetNoiseDistortion(
                block, noiseTexture, noiseScale, noiseSpeed, distortion, Vector2.one);
        }

        public static void SetNoiseDistortion(
            MaterialPropertyBlock block,
            Texture noiseTexture,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            float distortion,
            Vector2 direction)
        {
            if (block == null) return;
            ESCompositeURPProperties.SetNoise(block, noiseTexture, noiseScale, noiseSpeed);
            block.SetFloat(Distortion, Mathf.Clamp(distortion, 0f, 0.2f));
            block.SetVector(DistortionDirection, new Vector4(
                Mathf.Clamp(direction.x, -4f, 4f),
                Mathf.Clamp(direction.y, -4f, 4f),
                0f,
                0f));
        }

        public static void SetRim(
            MaterialPropertyBlock block, bool enabled, Color color,
            float power, float intensity)
        {
            if (block == null) return;
            block.SetFloat(RimEnabled, enabled ? 1f : 0f);
            block.SetColor(RimColor, color);
            block.SetFloat(RimPower, Mathf.Clamp(power, 0.1f, 8f));
            block.SetFloat(RimIntensity, Mathf.Clamp(intensity, 0f, 8f));
        }

        public static void SetEmission(MaterialPropertyBlock block, Color color)
        {
            if (block == null) return;
            block.SetColor(EmissionColor, color);
        }

        public static void SetAlphaClip(MaterialPropertyBlock block, bool enabled, float cutoff)
        {
            ESCompositeURPProperties.SetAlphaClip(block, enabled, cutoff);
        }

        public static void SetFlags(MaterialPropertyBlock block, bool hologram, bool glitch)
        {
            if (block == null) return;
            block.SetFloat(HologramEnabled, hologram ? 1f : 0f);
            block.SetFloat(GlitchEnabled, glitch ? 1f : 0f);
        }

        /// <summary>配置使用兼容默认空间的 VFX 扫光。</summary>
        public static void SetShine(
            MaterialPropertyBlock block, bool enabled, Color color,
            float speed, float width, float intensity, Vector3 direction)
        {
            ESCompositeURPProperties.SetShine(
                block, enabled, color, speed, width, intensity, direction);
        }

        /// <summary>配置可选择局部 UV 或世界投影空间的 VFX 扫光。</summary>
        public static void SetShine(
            MaterialPropertyBlock block, bool enabled, Color color,
            float speed, float width, float intensity, Vector3 direction,
            ESCompositeProjectionSpace space)
        {
            ESCompositeURPProperties.SetShine(
                block, enabled, color, speed, width, intensity, direction, space);
        }

        /// <summary>配置 VFX 原生轻量全息公式。</summary>
        public static void SetHologram(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float frequency,
            float gap,
            float speed,
            float minAlpha)
        {
            SetHologram(block, enabled, color, frequency, gap, speed, minAlpha, Vector3.zero);
        }

        public static void SetHologram(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float frequency,
            float gap,
            float speed,
            float minAlpha,
            Vector3 direction)
        {
            if (block == null) return;
            block.SetFloat(HologramEnabled, enabled ? 1f : 0f);
            block.SetColor(HologramColor, color);
            block.SetFloat(HologramFrequency, Mathf.Max(0.01f, frequency));
            block.SetFloat(HologramGap, Mathf.Clamp01(gap));
            block.SetFloat(HologramSpeed, Mathf.Clamp(speed, -128f, 128f));
            block.SetFloat(HologramMinAlpha, Mathf.Clamp01(minAlpha));
            block.SetVector(HologramDirection, new Vector4(
                direction.x, direction.y, direction.z, 0f));
        }

        /// <summary>配置启用 ESNative 精确合同时使用的完整全息参数。</summary>
        public static void SetESNativeExactHologram(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float lineFrequency,
            float lineGap,
            float speed,
            float minAlpha,
            float fade,
            float contrast,
            ES3DLitHologramSpace space,
            float distortionOffset,
            float distortionSpeed,
            float distortionDensity,
            float distortionScale)
        {
            ESCompositeURPProperties.SetHologram(
                block, enabled, color, lineFrequency, lineGap, speed, minAlpha,
                fade, contrast, space, distortionOffset, distortionSpeed,
                distortionDensity, distortionScale);
        }

        public static void SetESNativeExactHologram(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float lineFrequency,
            float lineGap,
            float speed,
            float minAlpha,
            float fade,
            float contrast,
            ES3DLitHologramSpace space,
            float distortionOffset,
            float distortionSpeed,
            float distortionDensity,
            float distortionScale,
            Vector3 scanDirection,
            Vector2 distortionDirection)
        {
            ESCompositeURPProperties.SetHologram(
                block, enabled, color, lineFrequency, lineGap, speed, minAlpha,
                fade, contrast, space, distortionOffset, distortionSpeed,
                distortionDensity, distortionScale, scanDirection, distortionDirection);
        }

        /// <summary>配置 VFX 原生轻量故障公式。</summary>
        public static void SetGlitch(
            MaterialPropertyBlock block,
            bool enabled,
            float amount,
            float speed)
        {
            SetGlitch(block, enabled, amount, speed, Vector2.zero);
        }

        public static void SetGlitch(
            MaterialPropertyBlock block,
            bool enabled,
            float amount,
            float speed,
            Vector2 direction)
        {
            if (block == null) return;
            block.SetFloat(GlitchEnabled, enabled ? 1f : 0f);
            block.SetFloat(GlitchAmount, Mathf.Clamp(amount, 0f, 0.2f));
            block.SetFloat(GlitchSpeed, Mathf.Clamp(speed, -128f, 128f));
            block.SetVector(GlitchScanDirection, new Vector4(0f, 1f, 0f, 0f));
            block.SetVector(GlitchDistortion, new Vector4(direction.x, direction.y, 0f, 0f));
        }

        public static void SetGlitchScanDirection(MaterialPropertyBlock block, Vector3 direction)
        {
            ESCompositeURPProperties.SetGlitchScanDirection(block, direction);
        }

        /// <summary>配置启用 ESNative 精确合同时使用的完整故障参数。</summary>
        public static void SetESNativeExactGlitch(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            float maskMin,
            Vector2 maskScale,
            Vector2 maskSpeed,
            float hueSpeed,
            float brightness,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            Vector2 distortion,
            Vector2 distortionScale,
            Vector2 distortionSpeed)
        {
            if (block == null) return;
            block.SetFloat(GlitchEnabled, enabled ? 1f : 0f);
            block.SetFloat(GlitchFade, Mathf.Clamp01(fade));
            block.SetFloat(GlitchMaskMin, Mathf.Clamp01(maskMin));
            block.SetVector(GlitchMaskScale, new Vector4(maskScale.x, maskScale.y, 0f, 0f));
            block.SetVector(GlitchMaskSpeed, new Vector4(maskSpeed.x, maskSpeed.y, 0f, 0f));
            block.SetFloat(GlitchHueSpeed, hueSpeed);
            block.SetFloat(GlitchBrightness, brightness);
            block.SetVector(GlitchNoiseScale, new Vector4(noiseScale.x, noiseScale.y, 0f, 0f));
            block.SetVector(GlitchNoiseSpeed, new Vector4(noiseSpeed.x, noiseSpeed.y, 0f, 0f));
            block.SetVector(GlitchDistortion, new Vector4(distortion.x, distortion.y, 0f, 0f));
            block.SetVector(GlitchDistortionScale, new Vector4(distortionScale.x, distortionScale.y, 0f, 0f));
            block.SetVector(GlitchDistortionSpeed, new Vector4(distortionSpeed.x, distortionSpeed.y, 0f, 0f));
        }

        /// <summary>
        /// 仅写入实例属性；不会改变承载材质的质量 Keyword。
        /// 首次为运行时实例启用精确合同，优先使用 TrySetESNativeExactContract。
        /// </summary>
        public static void SetESNativeExactContract(MaterialPropertyBlock block, bool enabled)
        {
            ESCompositeURPProperties.SetESNativeExactContract(block, enabled);
        }

        /// <summary>
        /// 初始化供 MaterialPropertyBlock 动态切换 ESNative 效果使用的 VFX 材质。
        /// 此方法会修改材质 Keyword，应在实例初始化或材质创建阶段调用，不应逐帧调用共享材质。
        /// </summary>
        public static bool PrepareMaterialForDynamicESNative(Material material)
        {
            if (material == null || !material.HasProperty(ESNativeExactContract)) return false;
            SetQuality(material, ESCompositeQualityTier.高质量);
            return true;
        }

        public static bool TrySetESNativeExactContract(
            Material material,
            MaterialPropertyBlock block,
            bool enabled)
        {
            if (material == null || block == null || !material.HasProperty(ESNativeExactContract)) return false;
            if (enabled && !PrepareMaterialForDynamicESNative(material)) return false;
            SetESNativeExactContract(block, enabled);
            return true;
        }

        public static void SetESNativeExactContract(Material material, bool enabled)
        {
            if (material == null || !material.HasProperty(ESNativeExactContract)) return;
            material.SetFloat(ESNativeExactContract, enabled ? 1f : 0f);
            if (enabled) PrepareMaterialForDynamicESNative(material);
        }

        public static void SetQuality(Material material, ESCompositeQualityTier quality)
        {
            ESCompositeURPProperties.ApplyQuality(material, QualityTier, quality);
        }

        public static void SetRenderState(
            Material material,
            ES3DVFXBlendMode blendMode,
            ES3DVFXDepthWriteMode depthWrite,
            ES3DVFXDepthTestMode depthTest,
            ES3DVFXCullMode cullMode,
            int queueOffset = 0)
        {
            if (material == null) return;
            SetBlendMode(material, blendMode);
            SetDepthWrite(material, depthWrite);
            SetDepthTest(material, depthTest);
            SetCullMode(material, cullMode);
            SetQueueOffset(material, queueOffset);
        }

        public static void SetBlendMode(Material material, ES3DVFXBlendMode blendMode)
        {
            if (material == null) return;
            switch (blendMode)
            {
                case ES3DVFXBlendMode.叠加:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.One);
                    break;
                case ES3DVFXBlendMode.预乘透明:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
                case ES3DVFXBlendMode.正片叠底:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.DstColor);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.Zero);
                    break;
                default:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
            }
            material.SetFloat(BlendMode, (float)blendMode);
            material.SetFloat(BlendOp, (float)UnityEngine.Rendering.BlendOp.Add);
            material.SetOverrideTag("RenderType", "Transparent");
        }

        public static void SetDepthWrite(Material material, ES3DVFXDepthWriteMode depthWrite)
        {
            if (material == null) return;
            material.SetFloat(ZWriteMode, (float)depthWrite);
        }

        public static void SetDepthTest(Material material, ES3DVFXDepthTestMode depthTest)
        {
            if (material == null) return;
            material.SetFloat(ZTest, (float)depthTest);
        }

        public static void SetCullMode(Material material, ES3DVFXCullMode cullMode)
        {
            if (material == null) return;
            material.SetFloat(Cull, (float)cullMode);
        }

        public static void SetQueueOffset(Material material, int queueOffset)
        {
            if (material == null) return;
            int offset = Mathf.Clamp(queueOffset, -50, 50);
            material.SetFloat(QueueOffset, offset);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = TransparentQueue + offset;
        }
    }

    public static class ESUICompositeURPProperties
    {
        public const string ShaderName = "ES/UI/Composite URP";
        public static readonly int ShineSpace = ESCompositeURPProperties.ShineSpace;
        public static readonly int FlameCenter = ESCompositeURPProperties.FlameCenter;
        public static readonly int FlameDirection = ESCompositeURPProperties.FlameDirection;
        public static readonly int SmokeSpeed = ESCompositeURPProperties.SmokeSpeed;
        public static readonly int SquishDirection = ESCompositeURPProperties.SquishDirection;
        public static readonly int VibrateDirection = ESCompositeURPProperties.VibrateDirection;
        private static readonly string[] SurfaceResourceSwitches =
        {
            "_ESNativeStatusContract",
            "_EnableFrozen", "_EnableBurn", "_EnablePoison", "_EnableSmoke", "_EnableFlame",
            "_EnableInkSpread", "_EnableCamouflage", "_EnableMetal", "_EnableEnchanted",
            "_EnableFullGlowDissolve", "_EnableFullDistortion", "_EnableAddColor", "_EnableStrongTint",
            "_EnableRecolorRGB", "_EnableRecolorRGBYCP",
            "_EnableAddHue", "_EnableSineGlow"
        };
        public static readonly int AddColorEnabled = ESCompositeURPProperties.AddColorEnabled;
        public static readonly int AddColor = ESCompositeURPProperties.AddColor;
        public static readonly int AddColorFade = ESCompositeURPProperties.AddColorFade;
        public static readonly int AddColorContrastEnabled = ESCompositeURPProperties.AddColorContrastEnabled;
        public static readonly int AddColorContrast = ESCompositeURPProperties.AddColorContrast;
        public static readonly int AddColorMaskEnabled = ESCompositeURPProperties.AddColorMaskEnabled;
        public static readonly int AddColorMask = ESCompositeURPProperties.AddColorMask;
        public static readonly int StrongTintEnabled = ESCompositeURPProperties.StrongTintEnabled;
        public static readonly int StrongTint = ESCompositeURPProperties.StrongTint;
        public static readonly int StrongTintFade = ESCompositeURPProperties.StrongTintFade;
        public static readonly int StrongTintContrastEnabled = ESCompositeURPProperties.StrongTintContrastEnabled;
        public static readonly int StrongTintContrast = ESCompositeURPProperties.StrongTintContrast;
        public static readonly int StrongTintMaskEnabled = ESCompositeURPProperties.StrongTintMaskEnabled;
        public static readonly int StrongTintMask = ESCompositeURPProperties.StrongTintMask;
        public static readonly int AlphaTintEnabled = ESCompositeURPProperties.AlphaTintEnabled;
        public static readonly int AlphaTint = ESCompositeURPProperties.AlphaTint;
        public static readonly int AlphaTintMin = ESCompositeURPProperties.AlphaTintMin;
        public static readonly int AlphaTintFade = ESCompositeURPProperties.AlphaTintFade;
        public static readonly int ColorReplaceEnabled = ESCompositeURPProperties.ColorReplaceEnabled;
        public static readonly int ReplaceFrom = ESCompositeURPProperties.ReplaceFrom;
        public static readonly int ReplaceTo = ESCompositeURPProperties.ReplaceTo;
        public static readonly int ReplaceRange = ESCompositeURPProperties.ReplaceRange;
        public static readonly int ReplaceSoftness = ESCompositeURPProperties.ReplaceSoftness;
        public static readonly int ReplaceContrast = ESCompositeURPProperties.ReplaceContrast;
        public static readonly int ReplaceFade = ESCompositeURPProperties.ReplaceFade;
        public static readonly int BrightnessEnabled = ESCompositeURPProperties.BrightnessEnabled;
        public static readonly int Brightness = ESCompositeURPProperties.Brightness;
        public static readonly int ContrastEnabled = ESCompositeURPProperties.ContrastEnabled;
        public static readonly int Contrast = ESCompositeURPProperties.Contrast;
        public static readonly int SaturationEnabled = ESCompositeURPProperties.SaturationEnabled;
        public static readonly int Saturation = ESCompositeURPProperties.Saturation;
        public static readonly int HueEnabled = ESCompositeURPProperties.HueEnabled;
        public static readonly int Hue = ESCompositeURPProperties.Hue;
        public static readonly int NegativeEnabled = ESCompositeURPProperties.NegativeEnabled;
        public static readonly int NegativeFade = ESCompositeURPProperties.NegativeFade;
        public static readonly int RainbowEnabled = ESCompositeURPProperties.RainbowEnabled;
        public static readonly int RainbowSpeed = ESCompositeURPProperties.RainbowSpeed;
        public static readonly int RainbowDensity = ESCompositeURPProperties.RainbowDensity;
        public static readonly int RainbowDirection = ESCompositeURPProperties.RainbowDirection;
        public static readonly int RainbowBrightness = ESCompositeURPProperties.RainbowBrightness;
        public static readonly int InnerOutlineEnabled = ESCompositeURPProperties.InnerOutlineEnabled;
        public static readonly int InnerOutlineColor = ESCompositeURPProperties.InnerOutlineColor;
        public static readonly int InnerOutlineWidth = ESCompositeURPProperties.InnerOutlineWidth;
        public static readonly int InnerOutlineFade = ESCompositeURPProperties.InnerOutlineFade;
        public static readonly int InnerOutlineDistortionEnabled = ESCompositeURPProperties.InnerOutlineDistortionEnabled;
        public static readonly int InnerOutlineDistortionIntensity = ESCompositeURPProperties.InnerOutlineDistortionIntensity;
        public static readonly int InnerOutlineNoiseScale = ESCompositeURPProperties.InnerOutlineNoiseScale;
        public static readonly int InnerOutlineNoiseSpeed = ESCompositeURPProperties.InnerOutlineNoiseSpeed;
        public static readonly int InnerOutlineTextureEnabled = ESCompositeURPProperties.InnerOutlineTextureEnabled;
        public static readonly int InnerOutlineTintTexture = ESCompositeURPProperties.InnerOutlineTintTexture;
        public static readonly int InnerOutlineTextureSpeed = ESCompositeURPProperties.InnerOutlineTextureSpeed;
        public static readonly int InnerOutlineOnly = ESCompositeURPProperties.InnerOutlineOnly;
        public static readonly int OuterOutlineEnabled = ESCompositeURPProperties.OuterOutlineEnabled;
        public static readonly int OuterOutlineColor = ESCompositeURPProperties.OuterOutlineColor;
        public static readonly int OuterOutlineWidth = ESCompositeURPProperties.OuterOutlineWidth;
        public static readonly int OuterOutlineFade = ESCompositeURPProperties.OuterOutlineFade;
        public static readonly int OuterOutlineDistortionEnabled = ESCompositeURPProperties.OuterOutlineDistortionEnabled;
        public static readonly int OuterOutlineDistortionIntensity = ESCompositeURPProperties.OuterOutlineDistortionIntensity;
        public static readonly int OuterOutlineNoiseScale = ESCompositeURPProperties.OuterOutlineNoiseScale;
        public static readonly int OuterOutlineNoiseSpeed = ESCompositeURPProperties.OuterOutlineNoiseSpeed;
        public static readonly int OuterOutlineTextureEnabled = ESCompositeURPProperties.OuterOutlineTextureEnabled;
        public static readonly int OuterOutlineTintTexture = ESCompositeURPProperties.OuterOutlineTintTexture;
        public static readonly int OuterOutlineTextureSpeed = ESCompositeURPProperties.OuterOutlineTextureSpeed;
        public static readonly int OuterOutlineOnly = ESCompositeURPProperties.OuterOutlineOnly;
        public static readonly int PixelOutlineEnabled = ESCompositeURPProperties.PixelOutlineEnabled;
        public static readonly int PixelOutlineColor = ESCompositeURPProperties.PixelOutlineColor;
        public static readonly int PixelOutlineWidth = ESCompositeURPProperties.PixelOutlineWidth;
        public static readonly int PixelOutlineFade = ESCompositeURPProperties.PixelOutlineFade;
        public static readonly int PixelOutlineTextureEnabled = ESCompositeURPProperties.PixelOutlineTextureEnabled;
        public static readonly int PixelOutlineTintTexture = ESCompositeURPProperties.PixelOutlineTintTexture;
        public static readonly int PixelOutlineTextureSpeed = ESCompositeURPProperties.PixelOutlineTextureSpeed;
        public static readonly int PixelOutlineOnly = ESCompositeURPProperties.PixelOutlineOnly;
        public static readonly int PingPongGlowEnabled = ESCompositeURPProperties.PingPongGlowEnabled;
        public static readonly int GlowFrom = ESCompositeURPProperties.GlowFrom;
        public static readonly int GlowTo = ESCompositeURPProperties.GlowTo;
        public static readonly int GlowFrequency = ESCompositeURPProperties.GlowFrequency;
        public static readonly int GlowIntensity = ESCompositeURPProperties.GlowIntensity;
        public static readonly int GlowContrast = ESCompositeURPProperties.GlowContrast;
        public static readonly int GlowFade = ESCompositeURPProperties.GlowFade;
        public static readonly int FrozenEnabled = ESCompositeURPProperties.FrozenEnabled;
        public static readonly int FrozenColor = ESCompositeURPProperties.FrozenColor;
        public static readonly int FrozenHighlight = ESCompositeURPProperties.FrozenHighlight;
        public static readonly int FrozenDensity = ESCompositeURPProperties.FrozenDensity;
        public static readonly int FrozenSpeed = ESCompositeURPProperties.FrozenSpeed;
        public static readonly int BurnEnabled = ESCompositeURPProperties.BurnEnabled;
        public static readonly int BurnEdgeColor = ESCompositeURPProperties.BurnEdgeColor;
        public static readonly int BurnInsideColor = ESCompositeURPProperties.BurnInsideColor;
        public static readonly int BurnProgress = ESCompositeURPProperties.BurnProgress;
        public static readonly int BurnWidth = ESCompositeURPProperties.BurnWidth;
        public static readonly int PoisonEnabled = ESCompositeURPProperties.PoisonEnabled;
        public static readonly int PoisonColor = ESCompositeURPProperties.PoisonColor;
        public static readonly int PoisonDensity = ESCompositeURPProperties.PoisonDensity;
        public static readonly int PoisonSpeed = ESCompositeURPProperties.PoisonSpeed;
        public static readonly int UVTransformEnabled = ESCompositeURPProperties.UVTransformEnabled;
        public static readonly int UVPivot = ESCompositeURPProperties.UVPivot;
        public static readonly int UVScale = ESCompositeURPProperties.UVScale;
        public static readonly int UVOffset = ESCompositeURPProperties.UVOffset;
        public static readonly int UVRotation = ESCompositeURPProperties.UVRotation;
        public static readonly int UVRotationSpeed = ESCompositeURPProperties.UVRotationSpeed;
        public static readonly int UVDistortEnabled = ESCompositeURPProperties.UVDistortEnabled;
        public static readonly int UVDistortFrequency = ESCompositeURPProperties.UVDistortFrequency;
        public static readonly int UVDistortSpeed = ESCompositeURPProperties.UVDistortSpeed;
        public static readonly int UVDistortAmount = ESCompositeURPProperties.UVDistortAmount;
        public static readonly int UVDistortNoiseTexture = ESCompositeURPProperties.UVDistortNoiseTexture;
        public static readonly int UVDistortFrom = ESCompositeURPProperties.UVDistortFrom;
        public static readonly int UVDistortTo = ESCompositeURPProperties.UVDistortTo;
        public static readonly int UVDistortFade = ESCompositeURPProperties.UVDistortFade;
        public static readonly int UVDistortMaskEnabled = ESCompositeURPProperties.UVDistortMaskEnabled;
        public static readonly int UVDistortMask = ESCompositeURPProperties.UVDistortMask;
        public static readonly int UVDistortMaskChannel = ESCompositeURPProperties.UVDistortMaskChannel;
        public static readonly int SplitToningEnabled = ESCompositeURPProperties.SplitToningEnabled;
        public static readonly int SplitToneShadows = ESCompositeURPProperties.SplitToneShadows;
        public static readonly int SplitToneHighlights = ESCompositeURPProperties.SplitToneHighlights;
        public static readonly int SplitToneBalance = ESCompositeURPProperties.SplitToneBalance;
        public static readonly int SplitToneStrength = ESCompositeURPProperties.SplitToneStrength;
        public static readonly int SplitToneContrast = ESCompositeURPProperties.SplitToneContrast;
        public static readonly int SplitToneShift = ESCompositeURPProperties.SplitToneShift;
        public static readonly int SpriteShadowEnabled = ESCompositeURPProperties.SpriteShadowEnabled;
        public static readonly int SpriteShadowFade = ESCompositeURPProperties.SpriteShadowFade;
        public static readonly int SpriteShadowOffset = ESCompositeURPProperties.SpriteShadowOffset;
        public static readonly int SpriteShadowColor = ESCompositeURPProperties.SpriteShadowColor;
        public static readonly int BlackTintEnabled = ESCompositeURPProperties.BlackTintEnabled;
        public static readonly int BlackTintFade = ESCompositeURPProperties.BlackTintFade;
        public static readonly int BlackTintColor = ESCompositeURPProperties.BlackTintColor;
        public static readonly int BlackTintPower = ESCompositeURPProperties.BlackTintPower;
        public static readonly int InkSpreadEnabled = ESCompositeURPProperties.InkSpreadEnabled;
        public static readonly int InkSpreadFade = ESCompositeURPProperties.InkSpreadFade;
        public static readonly int InkSpreadColor = ESCompositeURPProperties.InkSpreadColor;
        public static readonly int InkSpreadContrast = ESCompositeURPProperties.InkSpreadContrast;
        public static readonly int InkSpreadDistance = ESCompositeURPProperties.InkSpreadDistance;
        public static readonly int InkSpreadPosition = ESCompositeURPProperties.InkSpreadPosition;
        public static readonly int InkSpreadWidth = ESCompositeURPProperties.InkSpreadWidth;
        public static readonly int InkSpreadNoiseScale = ESCompositeURPProperties.InkSpreadNoiseScale;
        public static readonly int InkSpreadNoiseFactor = ESCompositeURPProperties.InkSpreadNoiseFactor;
        public static readonly int ShiftHueEnabled = ESCompositeURPProperties.ShiftHueEnabled;
        public static readonly int ShiftHueSpeed = ESCompositeURPProperties.ShiftHueSpeed;
        public static readonly int AddHueEnabled = ESCompositeURPProperties.AddHueEnabled;
        public static readonly int AddHueFade = ESCompositeURPProperties.AddHueFade;
        public static readonly int AddHueSpeed = ESCompositeURPProperties.AddHueSpeed;
        public static readonly int AddHueBrightness = ESCompositeURPProperties.AddHueBrightness;
        public static readonly int AddHueSaturation = ESCompositeURPProperties.AddHueSaturation;
        public static readonly int AddHueContrast = ESCompositeURPProperties.AddHueContrast;
        public static readonly int AddHueMaskEnabled = ESCompositeURPProperties.AddHueMaskEnabled;
        public static readonly int AddHueMask = ESCompositeURPProperties.AddHueMask;
        public static readonly int AddHueMaskScaleOffset = ESCompositeURPProperties.AddHueMaskScaleOffset;
        public static readonly int SineGlowEnabled = ESCompositeURPProperties.SineGlowEnabled;
        public static readonly int SineGlowFade = ESCompositeURPProperties.SineGlowFade;
        public static readonly int SineGlowColor = ESCompositeURPProperties.SineGlowColor;
        public static readonly int SineGlowContrast = ESCompositeURPProperties.SineGlowContrast;
        public static readonly int SineGlowFrequency = ESCompositeURPProperties.SineGlowFrequency;
        public static readonly int SineGlowMin = ESCompositeURPProperties.SineGlowMin;
        public static readonly int SineGlowMax = ESCompositeURPProperties.SineGlowMax;
        public static readonly int SineGlowMaskEnabled = ESCompositeURPProperties.SineGlowMaskEnabled;
        public static readonly int SineGlowMask = ESCompositeURPProperties.SineGlowMask;
        public static readonly int SineGlowMaskScaleOffset = ESCompositeURPProperties.SineGlowMaskScaleOffset;
        public static readonly int SqueezeEnabled = ESCompositeURPProperties.SqueezeEnabled;
        public static readonly int SqueezeFade = ESCompositeURPProperties.SqueezeFade;
        public static readonly int SqueezeScale = ESCompositeURPProperties.SqueezeScale;
        public static readonly int SqueezePower = ESCompositeURPProperties.SqueezePower;
        public static readonly int SqueezeCenter = ESCompositeURPProperties.SqueezeCenter;
        public static readonly int SineRotateEnabled = ESCompositeURPProperties.SineRotateEnabled;
        public static readonly int SineRotateFade = ESCompositeURPProperties.SineRotateFade;
        public static readonly int SineRotateAngle = ESCompositeURPProperties.SineRotateAngle;
        public static readonly int SineRotateFrequency = ESCompositeURPProperties.SineRotateFrequency;
        public static readonly int SineRotatePivot = ESCompositeURPProperties.SineRotatePivot;
        public static readonly int SineMoveEnabled = ESCompositeURPProperties.SineMoveEnabled;
        public static readonly int SineMoveFade = ESCompositeURPProperties.SineMoveFade;
        public static readonly int SineMoveOffset = ESCompositeURPProperties.SineMoveOffset;
        public static readonly int SineMoveFrequency = ESCompositeURPProperties.SineMoveFrequency;
        public static readonly int SineScaleEnabled = ESCompositeURPProperties.SineScaleEnabled;
        public static readonly int SineScaleFrequency = ESCompositeURPProperties.SineScaleFrequency;
        public static readonly int SineScaleFactor = ESCompositeURPProperties.SineScaleFactor;
        public static readonly int CustomFadeEnabled = ESCompositeURPProperties.CustomFadeEnabled;
        public static readonly int CustomFadeMask = ESCompositeURPProperties.CustomFadeMask;
        public static readonly int CustomFadeSmoothness = ESCompositeURPProperties.CustomFadeSmoothness;
        public static readonly int CustomFadeNoiseScale = ESCompositeURPProperties.CustomFadeNoiseScale;
        public static readonly int CustomFadeNoiseFactor = ESCompositeURPProperties.CustomFadeNoiseFactor;
        public static readonly int CustomFadeAlpha = ESCompositeURPProperties.CustomFadeAlpha;
        public static readonly int FullGlowDissolveEnabled = ESCompositeURPProperties.FullGlowDissolveEnabled;
        public static readonly int FullGlowDissolveFade = ESCompositeURPProperties.FullGlowDissolveFade;
        public static readonly int FullGlowDissolveWidth = ESCompositeURPProperties.FullGlowDissolveWidth;
        public static readonly int FullGlowDissolveEdgeColor = ESCompositeURPProperties.FullGlowDissolveEdgeColor;
        public static readonly int FullGlowDissolveNoiseScale = ESCompositeURPProperties.FullGlowDissolveNoiseScale;
        public static readonly int CamouflageEnabled = ESCompositeURPProperties.CamouflageEnabled;
        public static readonly int CamouflageFade = ESCompositeURPProperties.CamouflageFade;
        public static readonly int CamouflageBaseColor = ESCompositeURPProperties.CamouflageBaseColor;
        public static readonly int CamouflageContrast = ESCompositeURPProperties.CamouflageContrast;
        public static readonly int CamouflageColorA = ESCompositeURPProperties.CamouflageColorA;
        public static readonly int CamouflageDensityA = ESCompositeURPProperties.CamouflageDensityA;
        public static readonly int CamouflageSmoothnessA = ESCompositeURPProperties.CamouflageSmoothnessA;
        public static readonly int CamouflageNoiseScaleA = ESCompositeURPProperties.CamouflageNoiseScaleA;
        public static readonly int CamouflageColorB = ESCompositeURPProperties.CamouflageColorB;
        public static readonly int CamouflageDensityB = ESCompositeURPProperties.CamouflageDensityB;
        public static readonly int CamouflageSmoothnessB = ESCompositeURPProperties.CamouflageSmoothnessB;
        public static readonly int CamouflageNoiseScaleB = ESCompositeURPProperties.CamouflageNoiseScaleB;
        public static readonly int CamouflageAnimationEnabled = ESCompositeURPProperties.CamouflageAnimationEnabled;
        public static readonly int CamouflageDistortionSpeed = ESCompositeURPProperties.CamouflageDistortionSpeed;
        public static readonly int CamouflageDistortionIntensity = ESCompositeURPProperties.CamouflageDistortionIntensity;
        public static readonly int CamouflageDistortionScale = ESCompositeURPProperties.CamouflageDistortionScale;
        public static readonly int MetalEnabled = ESCompositeURPProperties.MetalEnabled;
        public static readonly int MetalFade = ESCompositeURPProperties.MetalFade;
        public static readonly int MetalColor = ESCompositeURPProperties.MetalColor;
        public static readonly int MetalContrast = ESCompositeURPProperties.MetalContrast;
        public static readonly int MetalHighlightColor = ESCompositeURPProperties.MetalHighlightColor;
        public static readonly int MetalHighlightDensity = ESCompositeURPProperties.MetalHighlightDensity;
        public static readonly int MetalHighlightContrast = ESCompositeURPProperties.MetalHighlightContrast;
        public static readonly int MetalNoiseScale = ESCompositeURPProperties.MetalNoiseScale;
        public static readonly int MetalNoiseSpeed = ESCompositeURPProperties.MetalNoiseSpeed;
        public static readonly int MetalNoiseDistortionScale = ESCompositeURPProperties.MetalNoiseDistortionScale;
        public static readonly int MetalNoiseDistortionSpeed = ESCompositeURPProperties.MetalNoiseDistortionSpeed;
        public static readonly int MetalNoiseDistortion = ESCompositeURPProperties.MetalNoiseDistortion;
        public static readonly int MetalMaskEnabled = ESCompositeURPProperties.MetalMaskEnabled;
        public static readonly int MetalMask = ESCompositeURPProperties.MetalMask;
        public static readonly int EnchantedEnabled = ESCompositeURPProperties.EnchantedEnabled;
        public static readonly int EnchantedFade = ESCompositeURPProperties.EnchantedFade;
        public static readonly int EnchantedSpeed = ESCompositeURPProperties.EnchantedSpeed;
        public static readonly int EnchantedScale = ESCompositeURPProperties.EnchantedScale;
        public static readonly int EnchantedBrightness = ESCompositeURPProperties.EnchantedBrightness;
        public static readonly int EnchantedContrast = ESCompositeURPProperties.EnchantedContrast;
        public static readonly int EnchantedReduce = ESCompositeURPProperties.EnchantedReduce;
        public static readonly int EnchantedRainbowEnabled = ESCompositeURPProperties.EnchantedRainbowEnabled;
        public static readonly int EnchantedRainbowSpeed = ESCompositeURPProperties.EnchantedRainbowSpeed;
        public static readonly int EnchantedRainbowDensity = ESCompositeURPProperties.EnchantedRainbowDensity;
        public static readonly int EnchantedRainbowSaturation = ESCompositeURPProperties.EnchantedRainbowSaturation;
        public static readonly int EnchantedLowColor = ESCompositeURPProperties.EnchantedLowColor;
        public static readonly int EnchantedHighColor = ESCompositeURPProperties.EnchantedHighColor;
        public static readonly int EnchantedLerpEnabled = ESCompositeURPProperties.EnchantedLerpEnabled;
        public static readonly int ShiftingEnabled = ESCompositeURPProperties.ShiftingEnabled;
        public static readonly int ShiftingFade = ESCompositeURPProperties.ShiftingFade;
        public static readonly int ShiftingSpeed = ESCompositeURPProperties.ShiftingSpeed;
        public static readonly int ShiftingDensity = ESCompositeURPProperties.ShiftingDensity;
        public static readonly int ShiftingBrightness = ESCompositeURPProperties.ShiftingBrightness;
        public static readonly int ShiftingContrast = ESCompositeURPProperties.ShiftingContrast;
        public static readonly int ShiftingRainbowEnabled = ESCompositeURPProperties.ShiftingRainbowEnabled;
        public static readonly int ShiftingSaturation = ESCompositeURPProperties.ShiftingSaturation;
        public static readonly int ShiftingColorA = ESCompositeURPProperties.ShiftingColorA;
        public static readonly int ShiftingColorB = ESCompositeURPProperties.ShiftingColorB;
        public static readonly int FadeMode = ESCompositeURPProperties.FadeMode;
        public static readonly int FadeProgress = ESCompositeURPProperties.FadeProgress;
        public static readonly int FadePosition = ESCompositeURPProperties.FadePosition;
        public static readonly int FadeRotation = ESCompositeURPProperties.FadeRotation;
        public static readonly int FadeWidth = ESCompositeURPProperties.FadeWidth;
        public static readonly int FadeInvert = ESCompositeURPProperties.FadeInvert;
        public static readonly int FadeNoiseFactor = ESCompositeURPProperties.FadeNoiseFactor;
        public static readonly int FadeNoiseScale = ESCompositeURPProperties.FadeNoiseScale;
        public static readonly int FadeNoiseSpeed = ESCompositeURPProperties.FadeNoiseSpeed;
        public static readonly int FadeNoiseTexture = ESCompositeURPProperties.FadeNoiseTexture;
        public static readonly int FadeMaskTexture = ESCompositeURPProperties.FadeMaskTexture;
        public static readonly int FadeEdgeColor = ESCompositeURPProperties.FadeEdgeColor;
        public static readonly int FadeEdgeWidth = ESCompositeURPProperties.FadeEdgeWidth;
        public static readonly int FadeEdgeIntensity = ESCompositeURPProperties.FadeEdgeIntensity;
        public static readonly int FadeDistortionStrength = ESCompositeURPProperties.FadeDistortionStrength;
        public static readonly int BlurMode = ESCompositeURPProperties.BlurMode;
        public static readonly int SharpenEnabled = ESCompositeURPProperties.SharpenEnabled;
        public static readonly int SharpenAmount = ESCompositeURPProperties.SharpenAmount;
        public static readonly int SharpenRadius = ESCompositeURPProperties.SharpenRadius;
        public static readonly int SharpenThreshold = ESCompositeURPProperties.SharpenThreshold;
        public static readonly int SharpenFade = ESCompositeURPProperties.SharpenFade;
        private const string UIAlphaClipKeyword = "UNITY_UI_ALPHACLIP";
        public static readonly int HologramEnabled = ESCompositeURPProperties.HologramEnabled;
        public static readonly int HologramColor = ESCompositeURPProperties.HologramColor;
        public static readonly int HologramLineFrequency = ESCompositeURPProperties.HologramLineFrequency;
        public static readonly int HologramLineGap = ESCompositeURPProperties.HologramLineGap;
        public static readonly int HologramSpeed = ESCompositeURPProperties.HologramSpeed;
        public static readonly int HologramMinAlpha = ESCompositeURPProperties.HologramMinAlpha;
        public static readonly int HologramFade = ESCompositeURPProperties.HologramFade;
        public static readonly int HologramContrast = ESCompositeURPProperties.HologramContrast;
        public static readonly int HologramSpace = ESCompositeURPProperties.HologramSpace;
        public static readonly int HologramDirection = ESCompositeURPProperties.HologramDirection;
        public static readonly int HologramDistortionOffset = ESCompositeURPProperties.HologramDistortionOffset;
        public static readonly int HologramDistortionDirection = ESCompositeURPProperties.HologramDistortionDirection;
        public static readonly int HologramDistortionSpeed = ESCompositeURPProperties.HologramDistortionSpeed;
        public static readonly int HologramDistortionDensity = ESCompositeURPProperties.HologramDistortionDensity;
        public static readonly int HologramDistortionScale = ESCompositeURPProperties.HologramDistortionScale;
        public static readonly int GlitchEnabled = ESCompositeURPProperties.GlitchEnabled;
        public static readonly int GlitchIntensity = ESCompositeURPProperties.GlitchIntensity;
        public static readonly int GlitchSpeed = ESCompositeURPProperties.GlitchSpeed;
        public static readonly int GlitchScanDirection = ESCompositeURPProperties.GlitchScanDirection;
        public static readonly int GlitchFade = ESCompositeURPProperties.GlitchFade;
        public static readonly int GlitchMaskMin = ESCompositeURPProperties.GlitchMaskMin;
        public static readonly int GlitchMaskScale = ESCompositeURPProperties.GlitchMaskScale;
        public static readonly int GlitchMaskSpeed = ESCompositeURPProperties.GlitchMaskSpeed;
        public static readonly int GlitchHueSpeed = ESCompositeURPProperties.GlitchHueSpeed;
        public static readonly int GlitchBrightness = ESCompositeURPProperties.GlitchBrightness;
        public static readonly int GlitchNoiseScale = ESCompositeURPProperties.GlitchNoiseScale;
        public static readonly int GlitchNoiseSpeed = ESCompositeURPProperties.GlitchNoiseSpeed;
        public static readonly int GlitchDistortion = ESCompositeURPProperties.GlitchDistortion;
        public static readonly int GlitchDistortionScale = ESCompositeURPProperties.GlitchDistortionScale;
        public static readonly int GlitchDistortionSpeed = ESCompositeURPProperties.GlitchDistortionSpeed;
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int VertexColorStrength = Shader.PropertyToID("_VertexColorStrength");
        public static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        public static readonly int Cutoff = ESCompositeURPProperties.Cutoff;
        public static readonly int UseUIAlphaClip = Shader.PropertyToID("_UseUIAlphaClip");
        public static readonly int TimeMode = Shader.PropertyToID("_TimeMode");
        public static readonly int CustomTime = Shader.PropertyToID("_CustomTime");
        public static readonly int TimeScale = Shader.PropertyToID("_TimeScale");
        public static readonly int TimeFPSEnabled = ESCompositeURPProperties.TimeFPSEnabled;
        public static readonly int TimeFPS = ESCompositeURPProperties.TimeFPS;
        public static readonly int TimeFrequencyEnabled = ESCompositeURPProperties.TimeFrequencyEnabled;
        public static readonly int TimeFrequency = ESCompositeURPProperties.TimeFrequency;
        public static readonly int TimeRange = ESCompositeURPProperties.TimeRange;
        public static readonly int MainTexScaleOffset = Shader.PropertyToID("_MainTexScaleOffset");
        public static readonly int SpriteUVRect = Shader.PropertyToID("_SpriteUVRect");
        public static readonly int SpriteUVTransformX = Shader.PropertyToID("_SpriteUVTransformX");
        public static readonly int SpriteUVTransformY = Shader.PropertyToID("_SpriteUVTransformY");
        public static readonly int SpriteUVTransformValid = Shader.PropertyToID("_SpriteUVTransformValid");
        public static readonly int SparkleEnabled = Shader.PropertyToID("_EnableSparkle");
        public static readonly int ShineEnabled = Shader.PropertyToID("_EnableShine");
        public static readonly int ShineColor = Shader.PropertyToID("_ShineColor");
        public static readonly int ShineSpeed = Shader.PropertyToID("_ShineSpeed");
        public static readonly int ShineWidth = Shader.PropertyToID("_ShineWidth");
        public static readonly int ShineDirection = Shader.PropertyToID("_ShineDirection");
        public static readonly int ShineAngle = Shader.PropertyToID("_ShineAngle");
        public static readonly int ShineIntensity = Shader.PropertyToID("_ShineIntensity");
        public static readonly int SparkleColor = Shader.PropertyToID("_SparkleColor");
        public static readonly int SparkleScale = Shader.PropertyToID("_SparkleScale");
        public static readonly int SparkleSpeed = Shader.PropertyToID("_SparkleSpeed");
        public static readonly int SparkleDensity = Shader.PropertyToID("_SparkleDensity");
        public static readonly int SparkleSharpness = Shader.PropertyToID("_SparkleSharpness");
        public static readonly int SparkleIntensity = Shader.PropertyToID("_SparkleIntensity");
        public static readonly int FlowEnabled = Shader.PropertyToID("_EnableFlow");
        public static readonly int FlowSpeed = Shader.PropertyToID("_FlowSpeed");
        public static readonly int FlowStrength = Shader.PropertyToID("_FlowStrength");
        public static readonly int ChromaticEnabled = Shader.PropertyToID("_EnableChromatic");
        public static readonly int ChromaticOffset = Shader.PropertyToID("_ChromaticOffset");
        public static readonly int ChromaticIntensity = Shader.PropertyToID("_ChromaticIntensity");
        public static readonly int ChromaticEdgeOnly = Shader.PropertyToID("_ChromaticEdgeOnly");
        public static readonly int ChromaticAngle = Shader.PropertyToID("_ChromaticAngle");
        public static readonly int BlurEnabled = ESCompositeURPProperties.BlurEnabled;
        public static readonly int BlurRadius = ESCompositeURPProperties.BlurRadius;
        public static readonly int BlurIntensity = ESCompositeURPProperties.BlurIntensity;
        public static readonly int TilingMode = ESCompositeURPProperties.TilingMode;
        public static readonly int WorldTilingScale = ESCompositeURPProperties.WorldTilingScale;
        public static readonly int WorldTilingOffset = ESCompositeURPProperties.WorldTilingOffset;
        public static readonly int WorldTilingPixelsPerUnit = ESCompositeURPProperties.WorldTilingPixelsPerUnit;
        public static readonly int ScreenTilingScale = ESCompositeURPProperties.ScreenTilingScale;
        public static readonly int ScreenTilingOffset = ESCompositeURPProperties.ScreenTilingOffset;
        public static readonly int ScreenTilingPixelsPerUnit = ESCompositeURPProperties.ScreenTilingPixelsPerUnit;
        public static readonly int SmoothPixelArtEnabled = ESCompositeURPProperties.SmoothPixelArtEnabled;
        public static readonly int SmoothPixelStrength = ESCompositeURPProperties.SmoothPixelStrength;
        public static readonly int CheckerboardEnabled = ESCompositeURPProperties.CheckerboardEnabled;
        public static readonly int CheckerboardDarken = ESCompositeURPProperties.CheckerboardDarken;
        public static readonly int CheckerboardTiling = ESCompositeURPProperties.CheckerboardTiling;
        public static readonly int UberNoiseTexture = ESCompositeURPProperties.UberNoiseTexture;
        public static readonly int ESNativeStatusContract = ESCompositeURPProperties.ESNativeStatusContract;
        public static readonly int ESNativeExactContract = ESCompositeURPProperties.ESNativeExactContract;
        public static readonly int FlameEnabled = ESCompositeURPProperties.FlameEnabled;
        public static readonly int FlameBrightness = ESCompositeURPProperties.FlameBrightness;
        public static readonly int FlameSmooth = ESCompositeURPProperties.FlameSmooth;
        public static readonly int FlameRadius = ESCompositeURPProperties.FlameRadius;
        public static readonly int FlameSpeed = ESCompositeURPProperties.FlameSpeed;
        public static readonly int FlameNoiseFactor = ESCompositeURPProperties.FlameNoiseFactor;
        public static readonly int FlameNoiseHeightFactor = ESCompositeURPProperties.FlameNoiseHeightFactor;
        public static readonly int FlameNoiseScale = ESCompositeURPProperties.FlameNoiseScale;
        public static readonly int SmokeEnabled = ESCompositeURPProperties.SmokeEnabled;
        public static readonly int SmokeAlpha = ESCompositeURPProperties.SmokeAlpha;
        public static readonly int SmokeSmoothness = ESCompositeURPProperties.SmokeSmoothness;
        public static readonly int SmokeNoiseScale = ESCompositeURPProperties.SmokeNoiseScale;
        public static readonly int SmokeNoiseFactor = ESCompositeURPProperties.SmokeNoiseFactor;
        public static readonly int SmokeDarkEdge = ESCompositeURPProperties.SmokeDarkEdge;
        public static readonly int SmokeVertexSeed = ESCompositeURPProperties.SmokeVertexSeed;
        public static readonly int SDFEnabled = Shader.PropertyToID("_EnableSDF");
        public static readonly int SDFThreshold = Shader.PropertyToID("_SDFThreshold");
        public static readonly int SDFSoftness = Shader.PropertyToID("_SDFSoftness");
        public static readonly int SDFOutlineWidth = Shader.PropertyToID("_SDFOutlineWidth");
        public static readonly int SDFOutlineSoftness = Shader.PropertyToID("_SDFOutlineSoftness");
        public static readonly int SDFOutlineColor = Shader.PropertyToID("_SDFOutlineColor");
        public static readonly int SDFGlowWidth = Shader.PropertyToID("_SDFGlowWidth");
        public static readonly int SDFGlowColor = Shader.PropertyToID("_SDFGlowColor");
        public static readonly int TMPCompatibility = Shader.PropertyToID("_EnableTMPCompatibility");
        public static readonly int TMPFaceColor = Shader.PropertyToID("_FaceColor");
        public static readonly int TMPFaceDilate = Shader.PropertyToID("_FaceDilate");
        public static readonly int TMPOutlineColor = Shader.PropertyToID("_OutlineColor");
        public static readonly int TMPOutlineWidth = Shader.PropertyToID("_OutlineWidth");
        public static readonly int TMPOutlineSoftness = Shader.PropertyToID("_OutlineSoftness");
        public static readonly int TMPUnderlayEnabled = Shader.PropertyToID("_EnableUnderlay");
        public static readonly int TMPUnderlayColor = Shader.PropertyToID("_UnderlayColor");
        public static readonly int TMPUnderlayOffsetX = Shader.PropertyToID("_UnderlayOffsetX");
        public static readonly int TMPUnderlayOffsetY = Shader.PropertyToID("_UnderlayOffsetY");
        public static readonly int TMPUnderlayDilate = Shader.PropertyToID("_UnderlayDilate");
        public static readonly int TMPUnderlaySoftness = Shader.PropertyToID("_UnderlaySoftness");
        public static readonly int TMPWeightNormal = Shader.PropertyToID("_WeightNormal");
        public static readonly int TMPWeightBold = Shader.PropertyToID("_WeightBold");
        public static readonly int TMPScaleRatioA = Shader.PropertyToID("_ScaleRatioA");
        public static readonly int TMPScaleRatioB = Shader.PropertyToID("_ScaleRatioB");
        public static readonly int TMPScaleRatioC = Shader.PropertyToID("_ScaleRatioC");
        public static readonly int TMPGradientScale = Shader.PropertyToID("_GradientScale");
        public static readonly int TMPSharpness = Shader.PropertyToID("_Sharpness");
        public static readonly int TMPTextureWidth = Shader.PropertyToID("_TextureWidth");
        public static readonly int TMPTextureHeight = Shader.PropertyToID("_TextureHeight");
        public static readonly int QualityTier = Shader.PropertyToID("_QualityTier");
        public static readonly int ResourceProfile = Shader.PropertyToID("_ResourceProfile");
        public static readonly int BlendMode = Shader.PropertyToID("_BlendMode");
        public static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        public static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        public static readonly int PixelateEnabled = Shader.PropertyToID("_EnablePixelate");
        public static readonly int PixelateCells = Shader.PropertyToID("_PixelateCells");
        public static readonly int PixelateStrength = Shader.PropertyToID("_PixelateStrength");
        public static readonly int PaletteEnabled = Shader.PropertyToID("_EnablePalette");
        public static readonly int PaletteTexture = Shader.PropertyToID("_PaletteTex");
        public static readonly int PaletteRow = Shader.PropertyToID("_PaletteRow");
        public static readonly int PaletteStrength = Shader.PropertyToID("_PaletteStrength");
        public static readonly int HalftoneEnabled = Shader.PropertyToID("_EnableHalftone");
        public static readonly int HalftoneScale = Shader.PropertyToID("_HalftoneScale");
        public static readonly int HalftoneAngle = Shader.PropertyToID("_HalftoneAngle");
        public static readonly int HalftoneStrength = Shader.PropertyToID("_HalftoneStrength");
        public static readonly int HalftonePosition = ESCompositeURPProperties.HalftonePosition;
        public static readonly int HalftoneFade = ESCompositeURPProperties.HalftoneFade;
        public static readonly int HalftoneFadeWidth = ESCompositeURPProperties.HalftoneFadeWidth;
        public static readonly int HalftoneInvert = ESCompositeURPProperties.HalftoneInvert;
        public static readonly int HalftoneAlphaPattern = ESCompositeURPProperties.HalftoneAlphaPattern;

        public static void SetMotionEffects(MaterialPropertyBlock block, bool sparkle, bool flow, bool chromatic, Vector2 flowSpeed, float flowStrength, float sparkleIntensity, float chromaticOffset, float chromaticIntensity)
        {
            if (block == null) return;
            block.SetFloat(SparkleEnabled, sparkle ? 1f : 0f);
            block.SetFloat(FlowEnabled, flow ? 1f : 0f);
            block.SetVector(FlowSpeed, new Vector4(flowSpeed.x, flowSpeed.y, 0f, 0f));
            block.SetFloat(FlowStrength, Mathf.Clamp01(flowStrength));
            block.SetFloat(SparkleIntensity, Mathf.Max(0f, sparkleIntensity));
            block.SetFloat(ChromaticEnabled, chromatic ? 1f : 0f);
            block.SetFloat(ChromaticOffset, Mathf.Max(0f, chromaticOffset));
            block.SetFloat(ChromaticIntensity, Mathf.Clamp01(chromaticIntensity));
        }

        public static void SetUVTransform(
            MaterialPropertyBlock block,
            bool enabled,
            Vector2 pivot,
            Vector2 scale,
            Vector2 offset,
            float rotationDegrees,
            bool distortionEnabled = false,
            Vector2 distortionFrequency = default,
            Vector2 distortionSpeed = default,
            float distortionAmount = 0f,
            float rotationSpeed = 0f,
            Texture distortionNoiseTexture = null,
            Vector2 distortionFrom = default,
            Vector2 distortionTo = default,
            float distortionFade = 1f,
            Texture distortionMask = null,
            ESCompositeTextureChannel distortionMaskChannel = ESCompositeTextureChannel.透明)
        {
            ESCompositeURPProperties.SetUVTransform(
                block, enabled, pivot, scale, offset, rotationDegrees,
                distortionEnabled, distortionFrequency, distortionSpeed, distortionAmount, rotationSpeed,
                distortionNoiseTexture, distortionFrom, distortionTo, distortionFade,
                distortionMask, distortionMaskChannel);
        }

        public static void SetSplitToning(
            MaterialPropertyBlock block,
            bool enabled,
            Color shadows,
            Color highlights,
            float balance = 0f,
            float strength = 1f,
            float contrast = 1f,
            float shift = 0f)
        {
            ESCompositeURPProperties.SetSplitToning(
                block, enabled, shadows, highlights, balance, strength, contrast, shift);
        }

        public static void SetSpriteShadow(MaterialPropertyBlock block, bool enabled, Vector2 offset, Color color, float fade = 1f)
        {
            ESCompositeURPProperties.SetSpriteShadow(block, enabled, offset, color, fade);
        }

        public static void SetBlackTint(MaterialPropertyBlock block, bool enabled, Color color, float power = 4f, float fade = 1f)
        {
            ESCompositeURPProperties.SetBlackTint(block, enabled, color, power, fade);
        }

        public static void SetInkSpread(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            Color color,
            float contrast,
            float fade,
            float distance,
            Vector2 position,
            float width,
            Vector2 noiseScale,
            float noiseFactor)
        {
            ESCompositeURPProperties.SetInkSpread(
                block, enabled, noiseTexture, color, contrast, fade,
                distance, position, width, noiseScale, noiseFactor);
        }

        public static void SetShiftHue(MaterialPropertyBlock block, bool enabled, float speed)
        {
            ESCompositeURPProperties.SetShiftHue(block, enabled, speed);
        }

        public static void SetAddHue(
            MaterialPropertyBlock block,
            bool enabled,
            float fade,
            float speed,
            float brightness,
            float saturation,
            float contrast,
            Texture mask = null,
            Vector2 maskScale = default,
            Vector2 maskOffset = default)
        {
            ESCompositeURPProperties.SetAddHue(
                block, enabled, fade, speed, brightness, saturation, contrast,
                mask, maskScale, maskOffset);
        }

        public static void SetSineGlow(
            MaterialPropertyBlock block,
            bool enabled,
            Color color,
            float contrast,
            float frequency,
            float minimum,
            float maximum,
            float fade = 1f,
            Texture mask = null,
            Vector2 maskScale = default,
            Vector2 maskOffset = default)
        {
            ESCompositeURPProperties.SetSineGlow(
                block, enabled, color, contrast, frequency, minimum, maximum,
                fade, mask, maskScale, maskOffset);
        }

        public static void SetFade(
            MaterialPropertyBlock block,
            ESCompositeFadeMode mode,
            float progress,
            float width,
            Vector2 position,
            float rotationDegrees,
            bool invert,
            float noiseFactor,
            Vector2 noiseScale,
            Vector2 noiseSpeed,
            Texture noiseTexture = null,
            Texture maskTexture = null,
            Color? edgeColor = null,
            float edgeWidth = 0.08f,
            float edgeIntensity = 1f,
            float distortionStrength = 0.03f)
        {
            ESCompositeURPProperties.SetFade(
                block, mode, progress, width, position, rotationDegrees, invert,
                noiseFactor, noiseScale, noiseSpeed, noiseTexture, maskTexture,
                edgeColor, edgeWidth, edgeIntensity, distortionStrength);
        }

        public static void SetSampling(
            MaterialPropertyBlock block,
            bool blurEnabled,
            bool gaussianBlur,
            float blurRadius,
            float blurIntensity,
            bool sharpenEnabled,
            float sharpenAmount,
            float sharpenRadius,
            float sharpenThreshold,
            float sharpenFade = 1f)
        {
            ESCompositeURPProperties.SetSampling(
                block, blurEnabled, gaussianBlur, blurRadius, blurIntensity,
                sharpenEnabled, sharpenAmount, sharpenRadius, sharpenThreshold, sharpenFade);
        }

        public static void SetTiling(
            MaterialPropertyBlock block,
            ESCompositeTilingMode mode,
            Vector2 worldScale,
            Vector2 worldOffset,
            float worldPixelsPerUnit,
            Vector2 screenScale,
            Vector2 screenOffset,
            float screenPixelsPerTile)
        {
            ESCompositeURPProperties.SetTiling(
                block, mode, worldScale, worldOffset, worldPixelsPerUnit,
                screenScale, screenOffset, screenPixelsPerTile);
        }

        public static void SetGeneratedStylization(
            MaterialPropertyBlock block,
            bool smoothPixelArt,
            float smoothPixelStrength,
            bool checkerboard,
            float checkerboardDarken,
            float checkerboardTiling)
        {
            ESCompositeURPProperties.SetGeneratedStylization(
                block, smoothPixelArt, smoothPixelStrength,
                checkerboard, checkerboardDarken, checkerboardTiling);
        }

        public static void SetFlame(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float brightness,
            float smoothness,
            float radius,
            Vector2 speed,
            float noiseFactor,
            float noiseHeightFactor,
            Vector2 noiseScale)
        {
            ESCompositeURPProperties.SetFlame(
                block, enabled, noiseTexture, brightness, smoothness, radius,
                speed, noiseFactor, noiseHeightFactor, noiseScale);
        }

        public static void SetSmoke(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float alpha,
            float smoothness,
            float noiseScale,
            float noiseFactor,
            float darkEdge,
            bool vertexSeed)
        {
            ESCompositeURPProperties.SetSmoke(
                block, enabled, noiseTexture, alpha, smoothness,
                noiseScale, noiseFactor, darkEdge, vertexSeed);
        }

        /// <summary>
        /// 仅写入实例属性；不会改变承载材质的质量 Keyword 或资源编译配置。
        /// 首次为运行时实例启用精确合同，优先使用 TrySetESNativeExactContract。
        /// </summary>
        public static void SetESNativeExactContract(MaterialPropertyBlock block, bool enabled, Texture shineMask = null)
        {
            ESCompositeURPProperties.SetESNativeExactContract(block, enabled, shineMask);
        }

        /// <summary>
        /// 初始化供 MaterialPropertyBlock 动态切换 ESNative 效果使用的材质。
        /// 此方法会修改材质 Keyword，应在实例初始化或材质创建阶段调用，不应逐帧调用共享材质。
        /// </summary>
        public static bool PrepareMaterialForDynamicESNative(Material material)
        {
            if (material == null || !material.HasProperty(ESNativeExactContract)) return false;
            SetQuality(material, ESCompositeQualityTier.高质量);
            SetResourceProfile(material, ESSpriteCompositeResourceProfile.动态完整);
            return true;
        }

        /// <summary>
        /// 在确认材质已进入高质量、动态完整变体后写入实例级 ESNative 精确合同。
        /// </summary>
        public static bool TrySetESNativeExactContract(
            Material material,
            MaterialPropertyBlock block,
            bool enabled,
            Texture shineMask = null)
        {
            if (material == null || block == null || !material.HasProperty(ESNativeExactContract)) return false;
            if (enabled && !PrepareMaterialForDynamicESNative(material)) return false;
            SetESNativeExactContract(block, enabled, shineMask);
            return true;
        }

        public static void SetESNativeExactContract(Material material, bool enabled, Texture shineMask = null)
        {
            if (material == null || !material.HasProperty(ESNativeExactContract)) return;
            material.SetFloat(ESNativeExactContract, enabled ? 1f : 0f);
            if (material.HasProperty(ESCompositeURPProperties.ShineMaskEnabled))
                material.SetFloat(ESCompositeURPProperties.ShineMaskEnabled, shineMask != null ? 1f : 0f);
            if (shineMask != null && material.HasProperty(ESCompositeURPProperties.ShineMask))
                material.SetTexture(ESCompositeURPProperties.ShineMask, shineMask);
            if (enabled) SetQuality(material, ESCompositeQualityTier.高质量);
            RefreshResourceProfile(material);
        }

        public static void SetInnerOutline(
            MaterialPropertyBlock block, bool enabled, Color color, float width, float fade,
            bool distortionEnabled, Vector2 distortionIntensity, Vector2 noiseScale, Vector2 noiseSpeed,
            bool textureEnabled, Texture tintTexture, Vector2 textureSpeed, bool outlineOnly)
        {
            ESCompositeURPProperties.SetInnerOutline(
                block, enabled, color, width, fade,
                distortionEnabled, distortionIntensity, noiseScale, noiseSpeed,
                textureEnabled, tintTexture, textureSpeed, outlineOnly);
        }

        public static void SetOuterOutline(
            MaterialPropertyBlock block, bool enabled, Color color, float width, float fade,
            bool distortionEnabled, Vector2 distortionIntensity, Vector2 noiseScale, Vector2 noiseSpeed,
            bool textureEnabled, Texture tintTexture, Vector2 textureSpeed, bool outlineOnly)
        {
            ESCompositeURPProperties.SetOuterOutline(
                block, enabled, color, width, fade,
                distortionEnabled, distortionIntensity, noiseScale, noiseSpeed,
                textureEnabled, tintTexture, textureSpeed, outlineOnly);
        }

        public static void SetPixelOutline(
            MaterialPropertyBlock block, bool enabled, Color color, float width, float fade,
            bool textureEnabled, Texture tintTexture, Vector2 textureSpeed, bool outlineOnly)
        {
            ESCompositeURPProperties.SetPixelOutline(
                block, enabled, color, width, fade,
                textureEnabled, tintTexture, textureSpeed, outlineOnly);
        }

        public static void SetShine(
            MaterialPropertyBlock block, bool enabled, Color color,
            float speed, float width, float intensity,
            Vector2 direction, float fallbackAngle = 30f)
        {
            ESCompositeURPProperties.SetShine(
                block, enabled, color, speed, width, intensity,
                new Vector3(direction.x, direction.y, 0f), fallbackAngle);
        }

        public static void SetHologram(
            MaterialPropertyBlock block, bool enabled, Color color,
            float lineFrequency, float lineGap, float speed, float minAlpha,
            float fade, float contrast, ES3DLitHologramSpace space,
            float distortionOffset, float distortionSpeed, float distortionDensity, float distortionScale)
        {
            ESCompositeURPProperties.SetHologram(
                block, enabled, color, lineFrequency, lineGap, speed, minAlpha,
                fade, contrast, space, distortionOffset, distortionSpeed,
                distortionDensity, distortionScale);
        }

        public static void SetHologram(
            MaterialPropertyBlock block, bool enabled, Color color,
            float lineFrequency, float lineGap, float speed, float minAlpha,
            float fade, float contrast, ES3DLitHologramSpace space,
            float distortionOffset, float distortionSpeed, float distortionDensity, float distortionScale,
            Vector3 scanDirection,
            Vector2 distortionDirection)
        {
            ESCompositeURPProperties.SetHologram(
                block, enabled, color, lineFrequency, lineGap, speed, minAlpha,
                fade, contrast, space, distortionOffset, distortionSpeed,
                distortionDensity, distortionScale, scanDirection, distortionDirection);
        }

        public static void SetGlitch(
            MaterialPropertyBlock block, bool enabled, float intensity, float speed,
            float fade, float maskMin, Vector2 maskScale, Vector2 maskSpeed,
            float hueSpeed, float brightness, Vector2 noiseScale, Vector2 noiseSpeed,
            Vector2 distortion, Vector2 distortionScale, Vector2 distortionSpeed)
        {
            ESCompositeURPProperties.SetGlitch(
                block, enabled, intensity, speed, fade, maskMin,
                maskScale, maskSpeed, hueSpeed, brightness,
                noiseScale, noiseSpeed, distortion, distortionScale, distortionSpeed);
        }

        public static void SetGlitchScanDirection(MaterialPropertyBlock block, Vector3 direction)
        {
            ESCompositeURPProperties.SetGlitchScanDirection(block, direction);
        }

        public static void SetEffects(MaterialPropertyBlock block, ESUICompositeEffectMode mode)
        {
            if (block == null) return;
            block.SetFloat(HologramEnabled, mode == ESUICompositeEffectMode.全息 || mode == ESUICompositeEffectMode.全息与故障 ? 1f : 0f);
            block.SetFloat(GlitchEnabled, mode == ESUICompositeEffectMode.故障 || mode == ESUICompositeEffectMode.全息与故障 ? 1f : 0f);
        }

        public static void SetSDF(
            MaterialPropertyBlock block,
            bool enabled,
            float threshold = 0.5f,
            float softness = 1f,
            float outlineWidth = 0f,
            float outlineSoftness = 1f,
            Color? outlineColor = null,
            float glowWidth = 0f,
            Color? glowColor = null)
        {
            if (block == null) return;
            block.SetFloat(SDFEnabled, enabled ? 1f : 0f);
            block.SetFloat(SDFThreshold, Mathf.Clamp01(threshold));
            block.SetFloat(SDFSoftness, Mathf.Clamp(softness, 0.25f, 4f));
            block.SetFloat(SDFOutlineWidth, Mathf.Clamp(outlineWidth, 0f, 0.5f));
            block.SetFloat(SDFOutlineSoftness, Mathf.Clamp(outlineSoftness, 0.25f, 4f));
            block.SetColor(SDFOutlineColor, outlineColor ?? UnityEngine.Color.black);
            block.SetFloat(SDFGlowWidth, Mathf.Clamp(glowWidth, 0f, 0.5f));
            block.SetColor(SDFGlowColor, glowColor ?? UnityEngine.Color.clear);
        }

        public static void SetTMP(
            MaterialPropertyBlock block,
            bool enabled,
            Color faceColor,
            float faceDilate,
            Color outlineColor,
            float outlineWidth,
            float outlineSoftness,
            bool underlay,
            Color underlayColor,
            Vector2 underlayOffset,
            float underlayDilate,
            float underlaySoftness,
            float weightNormal,
            float weightBold,
            float scaleRatioA,
            float scaleRatioB,
            float scaleRatioC,
            float gradientScale,
            float sharpness,
            Vector2 textureSize)
        {
            if (block == null) return;
            block.SetFloat(TMPCompatibility, enabled ? 1f : 0f);
            block.SetColor(TMPFaceColor, faceColor);
            block.SetFloat(TMPFaceDilate, Mathf.Clamp(faceDilate, -1f, 1f));
            block.SetColor(TMPOutlineColor, outlineColor);
            block.SetFloat(TMPOutlineWidth, Mathf.Clamp01(outlineWidth));
            block.SetFloat(TMPOutlineSoftness, Mathf.Clamp01(outlineSoftness));
            block.SetFloat(TMPUnderlayEnabled, underlay ? 1f : 0f);
            block.SetColor(TMPUnderlayColor, underlayColor);
            block.SetFloat(TMPUnderlayOffsetX, Mathf.Clamp(underlayOffset.x, -1f, 1f));
            block.SetFloat(TMPUnderlayOffsetY, Mathf.Clamp(underlayOffset.y, -1f, 1f));
            block.SetFloat(TMPUnderlayDilate, Mathf.Clamp(underlayDilate, -1f, 1f));
            block.SetFloat(TMPUnderlaySoftness, Mathf.Clamp01(underlaySoftness));
            block.SetFloat(TMPWeightNormal, weightNormal);
            block.SetFloat(TMPWeightBold, weightBold);
            block.SetFloat(TMPScaleRatioA, Mathf.Max(0f, scaleRatioA));
            block.SetFloat(TMPScaleRatioB, Mathf.Max(0f, scaleRatioB));
            block.SetFloat(TMPScaleRatioC, Mathf.Max(0f, scaleRatioC));
            block.SetFloat(TMPGradientScale, Mathf.Max(0.0001f, gradientScale));
            block.SetFloat(TMPSharpness, Mathf.Clamp(sharpness, -1f, 1f));
            block.SetFloat(TMPTextureWidth, Mathf.Max(1f, textureSize.x));
            block.SetFloat(TMPTextureHeight, Mathf.Max(1f, textureSize.y));
        }

        public static void SetStylization(
            MaterialPropertyBlock block,
            bool pixelate,
            float pixelateCells,
            float pixelateStrength,
            bool palette,
            Texture paletteTexture,
            float paletteRow,
            float paletteStrength,
            bool halftone,
            float halftoneScale,
            float halftoneAngle,
            float halftoneStrength)
        {
            if (block == null) return;
            block.SetFloat(PixelateEnabled, pixelate ? 1f : 0f);
            block.SetFloat(PixelateCells, Mathf.Clamp(pixelateCells, 2f, 512f));
            block.SetFloat(PixelateStrength, Mathf.Clamp01(pixelateStrength));
            block.SetFloat(PaletteEnabled, palette ? 1f : 0f);
            if (paletteTexture != null) block.SetTexture(PaletteTexture, paletteTexture);
            block.SetFloat(PaletteRow, Mathf.Clamp01(paletteRow));
            block.SetFloat(PaletteStrength, Mathf.Clamp01(paletteStrength));
            block.SetFloat(HalftoneEnabled, halftone ? 1f : 0f);
            block.SetFloat(HalftoneScale, Mathf.Clamp(halftoneScale, 4f, 512f));
            block.SetFloat(HalftoneAngle, Mathf.Repeat(halftoneAngle, 180f));
            block.SetFloat(HalftoneStrength, Mathf.Clamp01(halftoneStrength));
        }

        public static void SetFlame(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float brightness,
            float smoothness,
            float radius,
            Vector2 speed,
            float noiseFactor,
            float noiseHeightFactor,
            Vector2 noiseScale,
            Vector2 direction,
            Vector2 center)
        {
            ESCompositeURPProperties.SetFlame(
                block, enabled, noiseTexture, brightness, smoothness, radius,
                speed, noiseFactor, noiseHeightFactor, noiseScale, direction, center);
        }

        public static void SetSmoke(
            MaterialPropertyBlock block,
            bool enabled,
            Texture noiseTexture,
            float alpha,
            float smoothness,
            float noiseScale,
            float noiseFactor,
            float darkEdge,
            bool vertexSeed,
            Vector2 speed)
        {
            ESCompositeURPProperties.SetSmoke(
                block, enabled, noiseTexture, alpha, smoothness,
                noiseScale, noiseFactor, darkEdge, vertexSeed, speed);
        }

        public static void SetShine(
            MaterialPropertyBlock block, bool enabled, Color color,
            float speed, float width, float intensity,
            Vector2 direction, ESCompositeProjectionSpace space,
            float fallbackAngle = 30f)
        {
            ESCompositeURPProperties.SetShine(
                block, enabled, color, speed, width, intensity,
                new Vector3(direction.x, direction.y, 0f), space, fallbackAngle);
        }

        public static void SetSquish(
            MaterialPropertyBlock block, bool enabled, float amount, float speed)
        {
            ESCompositeURPProperties.SetSquish(block, enabled, amount, speed);
        }

        public static void SetSquish(
            MaterialPropertyBlock block, bool enabled, float amount, float speed, Vector2 direction)
        {
            ESCompositeURPProperties.SetSquish(block, enabled, amount, speed, direction);
        }

        public static void SetVibrate(
            MaterialPropertyBlock block, bool enabled, float amplitude, float speed)
        {
            ESCompositeURPProperties.SetVibrate(block, enabled, amplitude, speed);
        }

        public static void SetVibrate(
            MaterialPropertyBlock block, bool enabled, float amplitude, float speed, Vector2 direction)
        {
            ESCompositeURPProperties.SetVibrate(block, enabled, amplitude, speed, direction);
        }

        public static void SetQuality(Material material, ESCompositeQualityTier quality)
        {
            ESCompositeURPProperties.ApplyQuality(material, QualityTier, quality);
        }

        public static void SetResourceProfile(Material material, ESSpriteCompositeResourceProfile profile)
        {
            if (material == null || !material.HasProperty(ResourceProfile)) return;
            material.SetFloat(ResourceProfile, profile == ESSpriteCompositeResourceProfile.材质优化 ? 1f : 0f);
            RefreshResourceProfile(material);
        }

        public static bool RefreshResourceProfile(Material material)
        {
            return ESCompositeURPProperties.RefreshSpriteResourceProfile(
                material,
                ResourceProfile,
                SurfaceResourceSwitches);
        }

        public static void SetBlendMode(Material material, ESUICompositeBlendMode blendMode)
        {
            if (material == null) return;
            switch (blendMode)
            {
                case ESUICompositeBlendMode.叠加:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.One);
                    break;
                case ESUICompositeBlendMode.预乘透明:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
                case ESUICompositeBlendMode.正片叠底:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.DstColor);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.Zero);
                    break;
                default:
                    material.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat(DstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
            }
            material.SetFloat(BlendMode, (float)blendMode);
        }

        /// <summary>
        /// 设置 Unity UI 的固定阈值透明裁剪。
        /// 该能力由本地 Shader Keyword 控制，必须作用于当前 Graphic 缓存的独立 Material，不能使用 MaterialPropertyBlock。
        /// </summary>
        public static void SetUIAlphaClip(Material material, bool enabled)
        {
            if (material == null) return;
            material.SetFloat(UseUIAlphaClip, enabled ? 1f : 0f);
            ESCompositeURPProperties.SetKeyword(material, UIAlphaClipKeyword, enabled);
        }

        public static void SetAlphaClip(MaterialPropertyBlock block, bool enabled, float cutoff)
        {
            ESCompositeURPProperties.SetAlphaClip(block, enabled, cutoff);
        }
    }

    internal static class ESCompositeShaderTimeDriver
    {
        private const string DriverObjectName = "ES Composite Shader Time Driver";
        private static GameObject driverObject;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (driverObject != null) return;
            driverObject = new GameObject(DriverObjectName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(driverObject);
            driverObject.AddComponent<DriverBehaviour>();
        }

        private sealed class DriverBehaviour : MonoBehaviour
        {
            private void OnEnable()
            {
                Publish();
            }

            private void Update()
            {
                Publish();
            }

            private static void Publish()
            {
                ESCompositeURPProperties.SetUnscaledTime(Time.unscaledTime);
            }
        }
    }
}
