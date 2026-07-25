// Made with Amplify Shader Editor v1.9.1.8
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Sprite Shaders Ultimate/2D Lit URP SSU"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		_MainTex("MainTex", 2D) = "white" {}
		_MaskMap("Mask Map", 2D) = "white" {}
		_NormalMap("Normal Map", 2D) = "bump" {}
		_NormalIntensity("Normal Intensity", Float) = 1
		[Toggle(_VERTEXTINTFIRST_ON)] _VertexTintFirst("Vertex Tint First", Float) = 0
		[Toggle(_PIXELPERFECTSPACE_ON)] _PixelPerfectSpace("Pixel Perfect Space", Float) = 0
		[Toggle(_PIXELPERFECTUV_ON)] _PixelPerfectUV("Pixel Perfect UV", Float) = 0
		[Toggle(_SPRITESHEETFIX_ON)] _SpriteSheetFix("Sprite Sheet Fix", Float) = 0
		_SpriteSheetRect("Sprite Sheet Rect", Vector) = (0,0,1,1)
		[Toggle(_TILINGFIX_ON)] _TilingFix("Tiling Fix", Float) = 0
		[Toggle(_BAKEDMATERIAL_ON)] _BakedMaterial("Baked Material", Float) = 0
		[KeywordEnum(UV,UV_Raw,Object,Object_Scaled,World,UI_Graphic,Screen)] _ShaderSpace("Shader Space", Float) = 0
		_PixelsPerUnit("Pixels Per Unit", Float) = 100
		_ScreenWidthUnits("Screen Width Units", Float) = 10
		_RectWidth("Rect Width", Float) = 100
		_RectHeight("Rect Height", Float) = 100
		[KeywordEnum(None,Full,Mask,Dissolve,Spread)] _ShaderFading("Shader Fading", Float) = 0
		_FadingFade("Fading: Fade", Range( 0 , 1)) = 1
		_FadingPosition("Fading: Position", Vector) = (0,0,0,0)
		_FadingWidth("Fading: Width", Float) = 0.3
		_FadingNoiseFactor("Fading: Noise Factor", Float) = 0.2
		_FadingNoiseScale("Fading: Noise Scale", Vector) = (0.2,0.2,0,0)
		_FadingMask("Fading: Mask", 2D) = "white" {}
		[Toggle(_TOGGLEUNSCALEDTIME_ON)] _ToggleUnscaledTime("Toggle: Unscaled Time", Float) = 0
		[Toggle(_TOGGLECUSTOMTIME_ON)] _ToggleCustomTime("Toggle: Custom Time", Float) = 0
		_TimeValue("Time: Value", Float) = 0
		[Toggle(_TOGGLETIMESPEED_ON)] _ToggleTimeSpeed("Toggle: Time Speed", Float) = 0
		_TimeSpeed("Time: Speed", Float) = 1
		[Toggle(_TOGGLETIMEFPS_ON)] _ToggleTimeFPS("Toggle: Time FPS", Float) = 0
		_TimeFPS("Time: FPS", Float) = 5
		[Toggle(_TOGGLETIMEFREQUENCY_ON)] _ToggleTimeFrequency("Toggle: Time Frequency", Float) = 0
		_TimeFrequency("Time: Frequency", Float) = 2
		_TimeRange("Time: Range", Float) = 0.5
		_UberNoiseTexture("Uber Noise Texture", 2D) = "white" {}
		[Toggle(_ENABLESTRONGTINT_ON)] _EnableStrongTint("Enable Strong Tint", Float) = 0
		_StrongTintFade("Strong Tint: Fade", Range( 0 , 1)) = 1
		[HDR][NoAlpha]_StrongTintTint("Strong Tint: Tint", Color) = (1,1,1,1)
		[Toggle(_STRONGTINTCONTRASTTOGGLE_ON)] _StrongTintContrastToggle("Strong Tint: Contrast Toggle", Float) = 0
		_StrongTintContrast("Strong Tint: Contrast", Float) = 0
		[Toggle(_STRONGTINTMASKTOGGLE_ON)] _StrongTintMaskToggle("Strong Tint: Mask Toggle", Float) = 0
		_StrongTintMask("Strong Tint: Mask", 2D) = "white" {}
		[Toggle(_ENABLEADDCOLOR_ON)] _EnableAddColor("Enable Add Color", Float) = 0
		_AddColorFade("Add Color: Fade", Range( 0 , 1)) = 1
		[HDR]_AddColorColor("Add Color: Color", Color) = (2.996078,0,0,0)
		[Toggle(_ADDCOLORCONTRASTTOGGLE_ON)] _AddColorContrastToggle("Add Color: Contrast Toggle", Float) = 0
		_AddColorContrast("Add Color: Contrast", Float) = 0.5
		[Toggle(_ADDCOLORMASKTOGGLE_ON)] _AddColorMaskToggle("Add Color: Mask Toggle", Float) = 0
		_AddColorMask("Add Color: Mask", 2D) = "white" {}
		[Toggle(_ENABLEALPHATINT_ON)] _EnableAlphaTint("Enable Alpha Tint", Float) = 0
		_AlphaTintFade("Alpha Tint: Fade", Range( 0 , 1)) = 1
		[HDR]_AlphaTintColor("Alpha Tint: Color", Color) = (95.87451,5.019608,95.87451,0)
		_AlphaTintMinAlpha("Alpha Tint: Min Alpha", Range( 0 , 1)) = 0.02
		[Toggle(_ENABLESHADOW_ON)] _EnableShadow("Enable Shadow", Float) = 0
		_ShadowFade("Shadow: Fade", Range( 0 , 1)) = 1
		_ShadowOffset("Shadow: Offset", Vector) = (0.05,-0.05,0,0)
		_ShadowColor("Shadow: Color", Color) = (0,0,0,0)
		[Toggle(_ENABLEBRIGHTNESS_ON)] _EnableBrightness("Enable Brightness", Float) = 0
		_Brightness("Brightness", Float) = 1
		[Toggle(_ENABLECONTRAST_ON)] _EnableContrast("Enable Contrast", Float) = 0
		_Contrast("Contrast", Float) = 1
		[Toggle(_ENABLESATURATION_ON)] _EnableSaturation("Enable Saturation", Float) = 0
		_Saturation("Saturation", Float) = 1
		[Toggle(_ENABLEHUE_ON)] _EnableHue("Enable Hue", Float) = 0
		_Hue("Hue", Range( -1 , 1)) = 0
		[Toggle(_ENABLERECOLORRGB_ON)] _EnableRecolorRGB("Enable Recolor RGB", Float) = 0
		_RecolorRGBFade("Recolor RGB: Fade", Range( 0 , 1)) = 1
		[HDR]_RecolorRGBRedTint("Recolor RGB: Red Tint", Color) = (1,1,1,0.5019608)
		[HDR]_RecolorRGBGreenTint("Recolor RGB: Green Tint", Color) = (1,1,1,0.5019608)
		[HDR]_RecolorRGBBlueTint("Recolor RGB: Blue Tint", Color) = (1,1,1,0.5019608)
		[Toggle(_RECOLORRGBTEXTURETOGGLE_ON)] _RecolorRGBTextureToggle("Recolor RGB: Texture Toggle", Float) = 0
		_RecolorRGBTexture("Recolor RGB: Texture", 2D) = "white" {}
		[Toggle(_ENABLERECOLORRGBYCP_ON)] _EnableRecolorRGBYCP("Enable Recolor RGBYCP", Float) = 0
		_RecolorRGBYCPFade("Recolor RGBYCP: Fade", Range( 0 , 1)) = 1
		[HDR]_RecolorRGBYCPRedTint("Recolor RGBYCP: Red Tint", Color) = (1,1,1,0.5019608)
		[HDR]_RecolorRGBYCPYellowTint("Recolor RGBYCP: Yellow Tint", Color) = (1,1,1,0.5019608)
		[HDR]_RecolorRGBYCPGreenTint("Recolor RGBYCP: Green Tint", Color) = (1,1,1,0.5019608)
		[HDR]_RecolorRGBYCPCyanTint("Recolor RGBYCP: Cyan Tint", Color) = (1,1,1,0.5019608)
		[HDR]_RecolorRGBYCPBlueTint("Recolor RGBYCP: Blue Tint", Color) = (1,1,1,0.5019608)
		[HDR]_RecolorRGBYCPPurpleTint("Recolor RGBYCP: Purple Tint", Color) = (1,1,1,0.5019608)
		[Toggle(_RECOLORRGBYCPTEXTURETOGGLE_ON)] _RecolorRGBYCPTextureToggle("Recolor RGBYCP: Texture Toggle", Float) = 0
		_RecolorRGBYCPTexture("Recolor RGBYCP: Texture", 2D) = "white" {}
		[Toggle(_ENABLEINNEROUTLINE_ON)] _EnableInnerOutline("Enable Inner Outline", Float) = 0
		_InnerOutlineFade("Inner Outline: Fade", Range( 0 , 1)) = 1
		[HDR]_InnerOutlineColor("Inner Outline: Color", Color) = (11.98431,1.254902,1.254902,1)
		_InnerOutlineWidth("Inner Outline: Width", Float) = 0.02
		[Toggle(_INNEROUTLINEDISTORTIONTOGGLE_ON)] _InnerOutlineDistortionToggle("Inner Outline: Distortion Toggle", Float) = 0
		_InnerOutlineDistortionIntensity("Inner Outline: Distortion Intensity", Vector) = (0.01,0.01,0,0)
		_InnerOutlineNoiseScale("Inner Outline: Noise Scale", Vector) = (4,4,0,0)
		_InnerOutlineNoiseSpeed("Inner Outline: Noise Speed", Vector) = (0,0.1,0,0)
		[Toggle(_INNEROUTLINETEXTURETOGGLE_ON)] _InnerOutlineTextureToggle("Inner Outline: Texture Toggle", Float) = 0
		_InnerOutlineTintTexture("Inner Outline: Tint Texture", 2D) = "white" {}
		_InnerOutlineTextureSpeed("Inner Outline: Texture Speed", Vector) = (0.5,0,0,0)
		[Toggle(_INNEROUTLINEOUTLINEONLYTOGGLE_ON)] _InnerOutlineOutlineOnlyToggle("Inner Outline: Outline Only Toggle", Float) = 0
		[Toggle(_ENABLEOUTEROUTLINE_ON)] _EnableOuterOutline("Enable Outer Outline", Float) = 0
		_OuterOutlineFade("Outer Outline: Fade", Range( 0 , 1)) = 1
		[HDR]_OuterOutlineColor("Outer Outline: Color", Color) = (0,0,0,1)
		_OuterOutlineWidth("Outer Outline: Width", Float) = 0.04
		[Toggle(_OUTEROUTLINEDISTORTIONTOGGLE_ON)] _OuterOutlineDistortionToggle("Outer Outline: Distortion Toggle", Float) = 0
		_OuterOutlineDistortionIntensity("Outer Outline: Distortion Intensity", Vector) = (0.01,0.01,0,0)
		_OuterOutlineNoiseScale("Outer Outline: Noise Scale", Vector) = (4,4,0,0)
		_OuterOutlineNoiseSpeed("Outer Outline: Noise Speed", Vector) = (0,0.1,0,0)
		[Toggle(_OUTEROUTLINETEXTURETOGGLE_ON)] _OuterOutlineTextureToggle("Outer Outline: Texture Toggle", Float) = 0
		_OuterOutlineTintTexture("Outer Outline: Tint Texture", 2D) = "white" {}
		_OuterOutlineTextureSpeed("Outer Outline: Texture Speed", Vector) = (0.5,0,0,0)
		[Toggle(_OUTEROUTLINEOUTLINEONLYTOGGLE_ON)] _OuterOutlineOutlineOnlyToggle("Outer Outline: Outline Only Toggle", Float) = 0
		[Toggle(_ENABLEPIXELOUTLINE_ON)] _EnablePixelOutline("Enable Pixel Outline", Float) = 0
		_PixelOutlineFade("Pixel Outline: Fade", Range( 0 , 1)) = 1
		[HDR]_PixelOutlineColor("Pixel Outline: Color", Color) = (0,0,0,1)
		_PixelOutlineWidth("Pixel Outline: Width", Float) = 1
		[Toggle(_PIXELOUTLINETEXTURETOGGLE_ON)] _PixelOutlineTextureToggle("Pixel Outline: Texture Toggle", Float) = 0
		_PixelOutlineTintTexture("Pixel Outline: Tint Texture", 2D) = "white" {}
		_PixelOutlineTextureSpeed("Pixel Outline: Texture Speed", Vector) = (0.5,0,0,0)
		[Toggle(_PIXELOUTLINEOUTLINEONLYTOGGLE_ON)] _PixelOutlineOutlineOnlyToggle("Pixel Outline: Outline Only Toggle", Float) = 0
		[Toggle(_ENABLEADDHUE_ON)] _EnableAddHue("Enable Add Hue", Float) = 0
		_AddHueFade("Add Hue: Fade", Range( 0 , 1)) = 1
		_AddHueSpeed("Add Hue: Speed", Float) = 1
		_AddHueBrightness("Add Hue: Brightness", Float) = 2
		_AddHueSaturation("Add Hue: Saturation", Range( 0 , 1)) = 1
		_AddHueContrast("Add Hue: Contrast", Float) = 0.5
		[Toggle(_ADDHUEMASKTOGGLE_ON)] _AddHueMaskToggle("Add Hue: Mask Toggle", Float) = 0
		_AddHueMask("Add Hue: Mask", 2D) = "white" {}
		[Toggle(_ENABLEPINGPONGGLOW_ON)] _EnablePingPongGlow("Enable Ping-Pong Glow", Float) = 0
		_PingPongGlowFade("Ping-Pong Glow: Fade", Range( 0 , 1)) = 1
		[HDR]_PingPongGlowFrom("Ping-Pong Glow: From", Color) = (5.992157,0.1882353,0.1882353,0)
		[HDR]_PingPongGlowTo("Ping-Pong Glow: To", Color) = (0.1882353,0.1882353,5.992157,0)
		_PingPongGlowFrequency("Ping-Pong Glow: Frequency", Float) = 3
		_PingPongGlowContrast("Ping-Pong Glow: Contrast", Float) = 1
		[Toggle(_ENABLESHIFTHUE_ON)] _EnableShiftHue("Enable Shift Hue", Float) = 0
		_ShiftHueSpeed("Shift Hue: Speed", Float) = 0.5
		[Toggle(_ENABLEINKSPREAD_ON)] _EnableInkSpread("Enable Ink Spread", Float) = 0
		_InkSpreadFade("Ink Spread: Fade", Range( 0 , 1)) = 1
		[HDR]_InkSpreadColor("Ink Spread: Color", Color) = (8.47419,5.013525,0.08873497,0)
		_InkSpreadContrast("Ink Spread: Contrast", Float) = 2
		_InkSpreadDistance("Ink Spread: Distance", Float) = 3
		_InkSpreadPosition("Ink Spread: Position", Vector) = (0.5,-1,0,0)
		_InkSpreadWidth("Ink Spread: Width", Float) = 0.2
		_InkSpreadNoiseScale("Ink Spread: Noise Scale", Vector) = (0.4,0.4,0,0)
		_InkSpreadNoiseFactor("Ink Spread: Noise Factor", Float) = 0.5
		[Toggle(_ENABLEBLACKTINT_ON)] _EnableBlackTint("Enable Black Tint", Float) = 0
		_BlackTintFade("Black Tint: Fade", Range( 0 , 1)) = 1
		[HDR]_BlackTintColor("Black Tint: Color", Color) = (0,0,1,0)
		_BlackTintPower("Black Tint: Power", Float) = 4
		[Toggle(_ENABLESINEGLOW_ON)] _EnableSineGlow("Enable Sine Glow", Float) = 0
		_SineGlowFade("Sine Glow: Fade", Range( 0 , 1)) = 1
		[HDR]_SineGlowColor("Sine Glow: Color", Color) = (0,2.007843,2.996078,0)
		_SineGlowContrast("Sine Glow: Contrast", Float) = 1
		_SineGlowFrequency("Sine Glow: Frequency", Float) = 4
		_SineGlowMin("Sine Glow: Min", Float) = 0
		_SineGlowMax("Sine Glow: Max", Float) = 1
		[Toggle(_SINEGLOWMASKTOGGLE_ON)] _SineGlowMaskToggle("Sine Glow: Mask Toggle", Float) = 0
		_SineGlowMask("Sine Glow: Mask", 2D) = "white" {}
		[Toggle(_ENABLESPLITTONING_ON)] _EnableSplitToning("Enable Split Toning", Float) = 0
		_SplitToningFade("Split Toning: Fade", Range( 0 , 1)) = 1
		[HDR]_SplitToningHighlightsColor("Split Toning: Highlights Color", Color) = (1,0.1,0.1,0)
		[HDR]_SplitToningShadowsColor("Split Toning: Shadows Color", Color) = (0.1,0.4000002,1,0)
		_SplitToningContrast("Split Toning: Contrast", Float) = 1
		_SplitToningBalance("Split Toning: Balance", Float) = 1
		_SplitToningShift("Split Toning: Shift", Range( -1 , 1)) = 0
		[Toggle(_ENABLECOLORREPLACE_ON)] _EnableColorReplace("Enable Color Replace", Float) = 0
		_ColorReplaceFade("Color Replace: Fade", Range( 0 , 1)) = 1
		_ColorReplaceFromColor("Color Replace: From Color", Color) = (0,0,0,0)
		[HDR]_ColorReplaceToColor("Color Replace: To Color", Color) = (0,0,0.2,0)
		_ColorReplaceRange("Color Replace: Range", Float) = 0.05
		_ColorReplaceSmoothness("Color Replace: Smoothness", Float) = 0.1
		_ColorReplaceContrast("Color Replace: Contrast", Float) = 1
		[Toggle(_ENABLENEGATIVE_ON)] _EnableNegative("Enable Negative", Float) = 0
		_NegativeFade("Negative: Fade", Range( 0 , 1)) = 1
		[Toggle(_ENABLEHOLOGRAM_ON)] _EnableHologram("Enable Hologram", Float) = 0
		_HologramFade("Hologram: Fade", Range( 0 , 1)) = 1
		[HDR]_HologramTint("Hologram: Tint", Color) = (0.3137255,1.662745,2.996078,1)
		_HologramContrast("Hologram: Contrast", Float) = 1
		_HologramLineFrequency("Hologram: Line Frequency", Float) = 500
		_HologramLineGap("Hologram: Line Gap", Range( 0 , 5)) = 3
		_HologramLineSpeed("Hologram: Line Speed", Float) = 0.01
		_HologramMinAlpha("Hologram: Min Alpha", Range( 0 , 1)) = 0.2
		_HologramDistortionOffset("Hologram: Distortion Offset", Float) = 0.5
		_HologramDistortionSpeed("Hologram: Distortion Speed", Float) = 2
		_HologramDistortionDensity("Hologram: Distortion Density", Float) = 0.5
		_HologramDistortionScale("Hologram: Distortion Scale", Float) = 10
		[Toggle(_ENABLEGLITCH_ON)] _EnableGlitch("Enable Glitch", Float) = 0
		_GlitchFade("Glitch: Fade", Range( 0 , 1)) = 1
		_GlitchMaskMin("Glitch: Mask Min", Range( 0 , 1)) = 0.4
		_GlitchMaskScale("Glitch: Mask Scale", Vector) = (0,0.2,0,0)
		_GlitchMaskSpeed("Glitch: Mask Speed", Vector) = (0,4,0,0)
		_GlitchHueSpeed("Glitch: Hue Speed", Float) = 1
		_GlitchBrightness("Glitch: Brightness", Float) = 4
		_GlitchNoiseScale("Glitch: Noise Scale", Vector) = (0,3,0,0)
		_GlitchNoiseSpeed("Glitch: Noise Speed", Vector) = (0,1,0,0)
		_GlitchDistortion("Glitch: Distortion", Vector) = (0.1,0,0,0)
		_GlitchDistortionScale("Glitch: Distortion Scale", Vector) = (0,3,0,0)
		_GlitchDistortionSpeed("Glitch: Distortion Speed", Vector) = (0,1,0,0)
		[Toggle(_ENABLEFROZEN_ON)] _EnableFrozen("Enable Frozen", Float) = 0
		_FrozenFade("Frozen: Fade", Range( 0 , 1)) = 1
		[HDR]_FrozenTint("Frozen: Tint", Color) = (1.819608,4.611765,5.992157,0)
		_FrozenContrast("Frozen: Contrast", Float) = 2
		[HDR]_FrozenSnowColor("Frozen: Snow Color", Color) = (1.123529,1.373203,1.498039,0)
		_FrozenSnowContrast("Frozen: Snow Contrast", Float) = 1
		_FrozenSnowDensity("Frozen: Snow Density", Range( 0 , 1)) = 0.25
		_FrozenSnowScale("Frozen: Snow Scale", Vector) = (0.1,0.1,0,0)
		[HDR]_FrozenHighlightColor("Frozen: Highlight Color", Color) = (1.797647,4.604501,5.992157,1)
		_FrozenHighlightContrast("Frozen: Highlight Contrast", Float) = 2
		_FrozenHighlightDensity("Frozen: Highlight Density", Range( 0 , 1)) = 1
		_FrozenHighlightSpeed("Frozen: Highlight Speed", Vector) = (0.1,0.1,0,0)
		_FrozenHighlightScale("Frozen: Highlight Scale", Vector) = (0.2,0.2,0,0)
		_FrozenHighlightDistortion("Frozen: Highlight Distortion", Vector) = (0.5,0.5,0,0)
		_FrozenHighlightDistortionSpeed("Frozen: Highlight Distortion Speed", Vector) = (-0.05,-0.05,0,0)
		_FrozenHighlightDistortionScale("Frozen: Highlight Distortion Scale", Vector) = (0.2,0.2,0,0)
		[Toggle(_ENABLERAINBOW_ON)] _EnableRainbow("Enable Rainbow", Float) = 0
		_RainbowFade("Rainbow: Fade", Range( 0 , 1)) = 1
		_RainbowBrightness("Rainbow: Brightness", Float) = 2
		_RainbowSaturation("Rainbow: Saturation", Range( 0 , 1)) = 1
		_RainbowContrast("Rainbow: Contrast", Float) = 1
		_RainbowSpeed("Rainbow: Speed", Float) = 1
		_RainbowDensity("Rainbow: Density", Float) = 0.5
		_RainbowCenter("Rainbow: Center", Vector) = (0,0,0,0)
		_RainbowNoiseScale("Rainbow: Noise Scale", Vector) = (0.2,0.2,0,0)
		_RainbowNoiseFactor("Rainbow: Noise Factor", Float) = 0.2
		[Toggle(_ENABLECAMOUFLAGE_ON)] _EnableCamouflage("Enable Camouflage", Float) = 0
		_CamouflageFade("Camouflage: Fade", Range( 0 , 1)) = 1
		_CamouflageBaseColor("Camouflage: Base Color", Color) = (0.7450981,0.7254902,0.5686275,0)
		_CamouflageContrast("Camouflage: Contrast", Float) = 1
		_CamouflageColorA("Camouflage: Color A", Color) = (0.627451,0.5882353,0.4313726,0)
		_CamouflageDensityA("Camouflage: Density A", Range( 0 , 1)) = 0.4
		_CamouflageSmoothnessA("Camouflage: Smoothness A", Range( 0 , 1)) = 0.2
		_CamouflageNoiseScaleA("Camouflage: Noise Scale A", Vector) = (0.25,0.25,0,0)
		_CamouflageColorB("Camouflage: Color B", Color) = (0.4705882,0.4313726,0.3137255,0)
		_CamouflageDensityB("Camouflage: Density B", Range( 0 , 1)) = 0.4
		_CamouflageSmoothnessB("Camouflage: Smoothness B", Range( 0 , 1)) = 0.2
		_CamouflageNoiseScaleB("Camouflage: Noise Scale B", Vector) = (0.25,0.25,0,0)
		[Toggle(_CAMOUFLAGEANIMATIONTOGGLE_ON)] _CamouflageAnimationToggle("Camouflage: Animation Toggle", Float) = 0
		_CamouflageDistortionSpeed("Camouflage: Distortion Speed", Vector) = (0.1,0.1,0,0)
		_CamouflageDistortionIntensity("Camouflage: Distortion Intensity", Vector) = (0.1,0.1,0,0)
		_CamouflageDistortionScale("Camouflage: Distortion Scale", Vector) = (0.5,0.5,0,0)
		[Toggle(_ENABLEMETAL_ON)] _EnableMetal("Enable Metal", Float) = 0
		_MetalFade("Metal: Fade", Range( 0 , 1)) = 1
		[HDR]_MetalColor("Metal: Color", Color) = (5.992157,3.639216,0.3137255,1)
		_MetalContrast("Metal: Contrast", Float) = 2
		[HDR]_MetalHighlightColor("Metal: Highlight Color", Color) = (5.992157,3.796078,0.6588235,1)
		_MetalHighlightDensity("Metal: Highlight Density", Range( 0 , 1)) = 1
		_MetalHighlightContrast("Metal: Highlight Contrast", Float) = 2
		_MetalNoiseScale("Metal: Noise Scale", Vector) = (0.25,0.25,0,0)
		_MetalNoiseSpeed("Metal: Noise Speed", Vector) = (0.05,0.05,0,0)
		_MetalNoiseDistortionScale("Metal: Noise Distortion Scale", Vector) = (0.2,0.2,0,0)
		_MetalNoiseDistortionSpeed("Metal: Noise Distortion Speed", Vector) = (-0.05,-0.05,0,0)
		_MetalNoiseDistortion("Metal: Noise Distortion", Vector) = (0.5,0.5,0,0)
		[Toggle(_METALMASKTOGGLE_ON)] _MetalMaskToggle("Metal: Mask Toggle", Float) = 0
		[NoScaleOffset]_MetalMask("Metal: Mask", 2D) = "white" {}
		[Toggle(_ENABLESHINE_ON)] _EnableShine("Enable Shine", Float) = 0
		_ShineFade("Shine: Fade", Range( 0 , 1)) = 1
		[HDR]_ShineColor("Shine: Color", Color) = (11.98431,11.98431,11.98431,0)
		_ShineSaturation("Shine: Saturation", Range( 0 , 1)) = 0.5
		_ShineContrast("Shine: Contrast", Float) = 2
		_ShineWidth("Shine: Width", Float) = 0.1
		_ShineSpeed("Shine: Speed", Float) = 5
		_ShineRotation("Shine: Rotation", Range( 0 , 360)) = 30
		_ShineSmooth("Shine: Smoothness", Float) = 1
		_ShineFrequency("Shine: Frequency", Float) = 0.3
		[Toggle(_SHINEMASKTOGGLE_ON)] _ShineMaskToggle("Shine: Mask Toggle", Float) = 0
		[NoScaleOffset]_ShineMask("Shine: Mask", 2D) = "white" {}
		[Toggle(_ENABLEBURN_ON)] _EnableBurn("Enable Burn", Float) = 0
		_BurnFade("Burn: Fade", Range( 0 , 1)) = 1
		_BurnPosition("Burn: Position", Vector) = (0,5,0,0)
		_BurnRadius("Burn: Radius", Float) = 5
		[HDR]_BurnEdgeColor("Burn: Edge Color", Color) = (11.98431,1.129412,0.1254902,0)
		_BurnWidth("Burn: Width", Float) = 0.1
		_BurnEdgeNoiseScale("Burn: Edge Noise Scale", Vector) = (0.3,0.3,0,0)
		_BurnEdgeNoiseFactor("Burn: Edge Noise Factor", Float) = 0.5
		[HDR]_BurnInsideColor("Burn: Inside Color", Color) = (0.75,0.5625,0.525,0)
		_BurnInsideContrast("Burn: Inside Contrast", Float) = 2
		[HDR]_BurnInsideNoiseColor("Burn: Inside Noise Color", Color) = (3084.047,257.0039,0,0)
		_BurnInsideNoiseFactor("Burn: Inside Noise Factor", Float) = 0.2
		_BurnInsideNoiseScale("Burn: Inside Noise Scale", Vector) = (0.5,0.5,0,0)
		_BurnSwirlFactor("Burn: Swirl Factor", Float) = 1
		_BurnSwirlNoiseScale("Burn: Swirl Noise Scale", Vector) = (0.1,0.1,0,0)
		[Toggle(_ENABLEPOISON_ON)] _EnablePoison("Enable Poison", Float) = 0
		_PoisonFade("Poison: Fade", Range( 0 , 1)) = 1
		[HDR]_PoisonColor("Poison: Color", Color) = (0.3137255,2.996078,0.3137255,0)
		_PoisonDensity("Poison: Density", Float) = 3
		_PoisonRecolorFactor("Poison: Recolor Factor", Range( 0 , 1)) = 0.5
		_PoisonShiftSpeed("Poison: Shift Speed", Float) = 0.2
		_PoisonNoiseBrightness("Poison: Noise Brightness", Float) = 2
		_PoisonNoiseScale("Poison: Noise Scale", Vector) = (0.2,0.2,0,0)
		_PoisonNoiseSpeed("Poison: Noise Speed", Vector) = (0,-0.2,0,0)
		[Toggle(_ENABLEENCHANTED_ON)] _EnableEnchanted("Enable Enchanted", Float) = 0
		_EnchantedFade("Enchanted: Fade", Range( 0 , 1)) = 1
		_EnchantedSpeed("Enchanted: Speed", Vector) = (0,1,0,0)
		_EnchantedScale("Enchanted: Scale", Vector) = (0.1,0.1,0,0)
		_EnchantedBrightness("Enchanted: Brightness", Float) = 1
		_EnchantedContrast("Enchanted: Contrast", Float) = 0.5
		_EnchantedReduce("Enchanted: Reduce", Range( 0 , 2)) = 0
		[Toggle(_ENCHANTEDRAINBOWTOGGLE_ON)] _EnchantedRainbowToggle("Enchanted: Rainbow Toggle", Float) = 0
		_EnchantedRainbowSpeed("Enchanted: Rainbow Speed", Float) = 0.5
		_EnchantedRainbowDensity("Enchanted: Rainbow Density", Float) = 0.5
		_EnchantedRainbowSaturation("Enchanted: Rainbow Saturation", Float) = 0.8
		[HDR]_EnchantedLowColor("Enchanted: Low Color", Color) = (2.996078,0,0,0)
		[HDR]_EnchantedHighColor("Enchanted: High Color", Color) = (0,0.7098798,4.237095,0)
		[Toggle(_ENCHANTEDLERPTOGGLE_ON)] _EnchantedLerpToggle("Enchanted: Lerp Toggle", Float) = 0
		[Toggle(_ENABLESHIFTING_ON)] _EnableShifting("Enable Shifting", Float) = 0
		_ShiftingFade("Shifting: Fade", Range( 0 , 1)) = 1
		_ShiftingSpeed("Shifting: Speed", Float) = 0.5
		_ShiftingDensity("Shifting: Density", Float) = 1.5
		_ShiftingBrightness("Shifting: Brightness", Float) = 1
		_ShiftingContrast("Shifting: Contrast", Float) = 0.5
		[Toggle(_SHIFTINGRAINBOWTOGGLE_ON)] _ShiftingRainbowToggle("Shifting: Rainbow Toggle", Float) = 0
		_ShiftingSaturation("Shifting: Saturation", Float) = 0.8
		[HDR]_ShiftingColorA("Shifting: Color A", Color) = (1.498039,0,0,0)
		[HDR]_ShiftingColorB("Shifting: Color B", Color) = (1.498039,0.7490196,0,0)
		[Toggle(_ENABLETEXTURELAYER1_ON)] _EnableTextureLayer1("Enable Texture Layer 1", Float) = 0
		_TextureLayer1Fade("Texture Layer 1: Fade", Range( 0 , 1)) = 1
		_TextureLayer1Texture("Texture Layer 1: Texture", 2D) = "white" {}
		[HDR]_TextureLayer1Color("Texture Layer 1: Color", Color) = (0.4494117,1.298301,2.996079,1)
		_TextureLayer1Scale("Texture Layer 1: Scale", Vector) = (1,1,0,0)
		_TextureLayer1Offset("Texture Layer 1: Offset", Vector) = (0,0,0,0)
		[Toggle(_TEXTURELAYER1CONTRASTTOGGLE_ON)] _TextureLayer1ContrastToggle("Texture Layer 1: Contrast Toggle", Float) = 0
		_TextureLayer1Contrast("Texture Layer 1: Contrast", Float) = 1
		[Toggle(_TEXTURELAYER1SCROLLTOGGLE_ON)] _TextureLayer1ScrollToggle("Texture Layer 1: Scroll Toggle", Float) = 0
		_TextureLayer1ScrollSpeed("Texture Layer 1: Scroll Speed", Vector) = (0,1,0,0)
		[Toggle(_TEXTURELAYER1SHEETTOGGLE_ON)] _TextureLayer1SheetToggle("Texture Layer 1: Sheet Toggle", Float) = 1
		_TextureLayer1Columns("Texture Layer 1: Columns", Int) = 3
		_TextureLayer1Rows("Texture Layer 1: Rows", Int) = 3
		_TextureLayer1Speed("Texture Layer 1: Speed", Float) = 20
		_TextureLayer1StartFrame("Texture Layer 1: Start Frame", Int) = 0
		_TextureLayer1EdgeClip("Texture Layer 1: Edge Clip", Range( 0 , 1)) = 0.005
		[Toggle(_ENABLETEXTURELAYER2_ON)] _EnableTextureLayer2("Enable Texture Layer 2", Float) = 0
		_TextureLayer2Fade("Texture Layer 2: Fade", Range( 0 , 1)) = 1
		_TextureLayer2Texture("Texture Layer 2: Texture", 2D) = "white" {}
		[HDR]_TextureLayer2Color("Texture Layer 2: Color", Color) = (0.4494117,1.298301,2.996079,1)
		_TextureLayer2Scale("Texture Layer 2: Scale", Vector) = (1,1,0,0)
		_TextureLayer2Offset("Texture Layer 2: Offset", Vector) = (0,0,0,0)
		[Toggle(_TEXTURELAYER2CONTRASTTOGGLE_ON)] _TextureLayer2ContrastToggle("Texture Layer 2: Contrast Toggle", Float) = 0
		_TextureLayer2Contrast("Texture Layer 2: Contrast", Float) = 1
		[Toggle(_TEXTURELAYER2SCROLLTOGGLE_ON)] _TextureLayer2ScrollToggle("Texture Layer 2: Scroll Toggle", Float) = 0
		_TextureLayer2ScrollSpeed("Texture Layer 2: Scroll Speed", Vector) = (0,1,0,0)
		[Toggle(_TEXTURELAYER2SHEETTOGGLE_ON)] _TextureLayer2SheetToggle("Texture Layer 2: Sheet Toggle", Float) = 1
		_TextureLayer2Columns("Texture Layer 2: Columns", Int) = 3
		_TextureLayer2Rows("Texture Layer 2: Rows", Int) = 3
		_TextureLayer2Speed("Texture Layer 2: Speed", Float) = 20
		_TextureLayer2StartFrame("Texture Layer 2: Start Frame", Int) = 0
		_TextureLayer2EdgeClip("Texture Layer 2: Edge Clip", Range( 0 , 1)) = 0.005
		[Toggle(_ENABLEFULLALPHADISSOLVE_ON)] _EnableFullAlphaDissolve("Enable Full Alpha Dissolve", Float) = 0
		_FullAlphaDissolveFade("Full Alpha Dissolve: Fade", Range( 0 , 1)) = 0.5
		_FullAlphaDissolveWidth("Full Alpha Dissolve: Width", Float) = 0.5
		_FullAlphaDissolveNoiseScale("Full Alpha Dissolve: Noise Scale", Vector) = (0.1,0.1,0,0)
		[Toggle(_ENABLEFULLGLOWDISSOLVE_ON)] _EnableFullGlowDissolve("Enable Full Glow Dissolve", Float) = 0
		_FullGlowDissolveFade("Full Glow Dissolve: Fade", Range( 0 , 1)) = 0.5
		_FullGlowDissolveWidth("Full Glow Dissolve: Width", Float) = 0.5
		[HDR]_FullGlowDissolveEdgeColor("Full Glow Dissolve: Edge Color", Color) = (11.98431,0.627451,0.627451,0)
		_FullGlowDissolveNoiseScale("Full Glow Dissolve: Noise Scale", Vector) = (0.1,0.1,0,0)
		[Toggle(_ENABLESOURCEALPHADISSOLVE_ON)] _EnableSourceAlphaDissolve("Enable Source Alpha Dissolve", Float) = 0
		_SourceAlphaDissolveFade("Source Alpha Dissolve: Fade", Float) = 1
		_SourceAlphaDissolvePosition("Source Alpha Dissolve: Position", Vector) = (0,0,0,0)
		_SourceAlphaDissolveWidth("Source Alpha Dissolve: Width", Float) = 0.2
		_SourceAlphaDissolveNoiseScale("Source Alpha Dissolve: Noise Scale", Vector) = (0.3,0.3,0,0)
		_SourceAlphaDissolveNoiseFactor("Source Alpha Dissolve: Noise Factor", Float) = 0.2
		[Toggle]_SourceAlphaDissolveInvert("Source Alpha Dissolve: Invert", Float) = 0
		[Toggle(_ENABLESOURCEGLOWDISSOLVE_ON)] _EnableSourceGlowDissolve("Enable Source Glow Dissolve", Float) = 0
		_SourceGlowDissolveFade("Source Glow Dissolve: Fade", Float) = 1
		_SourceGlowDissolvePosition("Source Glow Dissolve: Position", Vector) = (0,0,0,0)
		_SourceGlowDissolveWidth("Source Glow Dissolve: Width", Float) = 0.1
		[HDR]_SourceGlowDissolveEdgeColor("Source Glow Dissolve: Edge Color", Color) = (11.98431,0.627451,0.627451,0)
		_SourceGlowDissolveNoiseScale("Source Glow Dissolve: Noise Scale", Vector) = (0.3,0.3,0,0)
		_SourceGlowDissolveNoiseFactor("Source Glow Dissolve: Noise Factor", Float) = 0.2
		[Toggle]_SourceGlowDissolveInvert("Source Glow Dissolve: Invert", Float) = 0
		[Toggle(_ENABLEHALFTONE_ON)] _EnableHalftone("Enable Halftone", Float) = 0
		_HalftoneFade("Halftone: Fade", Float) = 1
		_HalftonePosition("Halftone: Position", Vector) = (0,0,0,0)
		_HalftoneTiling("Halftone: Tiling", Float) = 4
		_HalftoneFadeWidth("Halftone: Width", Float) = 1.5
		[Toggle]_HalftoneInvert("Halftone: Invert", Float) = 0
		[Toggle(_ENABLEDIRECTIONALALPHAFADE_ON)] _EnableDirectionalAlphaFade("Enable Directional Alpha Fade", Float) = 0
		_DirectionalAlphaFadeFade("Directional Alpha Fade: Fade", Float) = 0
		_DirectionalAlphaFadeRotation("Directional Alpha Fade: Rotation", Range( 0 , 360)) = 0
		_DirectionalAlphaFadeWidth("Directional Alpha Fade: Width", Float) = 0.2
		_DirectionalAlphaFadeNoiseScale("Directional Alpha Fade: Noise Scale", Vector) = (0.3,0.3,0,0)
		_DirectionalAlphaFadeNoiseFactor("Directional Alpha Fade: Noise Factor", Float) = 0.2
		[Toggle]_DirectionalAlphaFadeInvert("Directional Alpha Fade: Invert", Float) = 0
		[Toggle(_ENABLEDIRECTIONALGLOWFADE_ON)] _EnableDirectionalGlowFade("Enable Directional Glow Fade", Float) = 0
		_DirectionalGlowFadeFade("Directional Glow Fade: Fade", Float) = 0
		_DirectionalGlowFadeRotation("Directional Glow Fade: Rotation", Range( 0 , 360)) = 0
		[HDR]_DirectionalGlowFadeEdgeColor("Directional Glow Fade: Edge Color", Color) = (11.98431,0.6901961,0.6901961,0)
		_DirectionalGlowFadeWidth("Directional Glow Fade: Width", Float) = 0.1
		_DirectionalGlowFadeNoiseScale("Directional Glow Fade: Noise Scale", Vector) = (0.4,0.4,0,0)
		_DirectionalGlowFadeNoiseFactor("Directional Glow Fade: Noise Factor", Float) = 0.2
		[Toggle]_DirectionalGlowFadeInvert("Directional Glow Fade: Invert", Float) = 0
		[Toggle(_ENABLEDIRECTIONALDISTORTION_ON)] _EnableDirectionalDistortion("Enable Directional Distortion", Float) = 0
		_DirectionalDistortionFade("Directional Distortion: Fade", Float) = 0
		_DirectionalDistortionRotation("Directional Distortion: Rotation", Range( 0 , 360)) = 0
		_DirectionalDistortionWidth("Directional Distortion: Width", Float) = 0.5
		_DirectionalDistortionNoiseScale("Directional Distortion: Noise Scale", Vector) = (0.4,0.4,0,0)
		_DirectionalDistortionNoiseFactor("Directional Distortion: Noise Factor", Float) = 0.2
		_DirectionalDistortionDistortion("Directional Distortion: Distortion", Vector) = (0,0.1,0,0)
		_DirectionalDistortionRandomDirection("Directional Distortion: Random Direction", Range( 0 , 1)) = 0.1
		_DirectionalDistortionDistortionScale("Directional Distortion: Distortion Scale", Vector) = (1,1,0,0)
		[Toggle]_DirectionalDistortionInvert("Directional Distortion: Invert", Float) = 0
		[Toggle(_ENABLEFULLDISTORTION_ON)] _EnableFullDistortion("Enable Full Distortion", Float) = 0
		_FullDistortionFade("Full Distortion: Fade", Range( 0 , 1)) = 1
		_FullDistortionDistortion("Full Distortion: Distortion", Vector) = (0.2,0.2,0,0)
		_FullDistortionNoiseScale("Full Distortion: Noise Scale", Vector) = (0.5,0.5,0,0)
		[Toggle(_ENABLEPIXELATE_ON)] _EnablePixelate("Enable Pixelate", Float) = 0
		_PixelateFade("Pixelate: Fade", Range( 0 , 1)) = 1
		_PixelatePixelsPerUnit("Pixelate: Pixels Per Unit", Float) = 100
		_PixelatePixelDensity("Pixelate: Pixel Density", Float) = 16
		[Toggle(_ENABLESQUEEZE_ON)] _EnableSqueeze("Enable Squeeze", Float) = 0
		_SqueezeFade("Squeeze: Fade", Range( 0 , 1)) = 1
		_SqueezeScale("Squeeze: Scale", Vector) = (2,0,0,0)
		_SqueezePower("Squeeze: Power", Float) = 1
		_SqueezeCenter("Squeeze: Center", Vector) = (0.5,0.5,0,0)
		[Toggle(_ENABLEUVDISTORT_ON)] _EnableUVDistort("Enable UV Distort", Float) = 0
		_UVDistortFade("UV Distort: Fade", Range( 0 , 1)) = 1
		_UVDistortFrom("UV Distort: From", Vector) = (-0.02,-0.02,0,0)
		_UVDistortTo("UV Distort: To", Vector) = (0.02,0.02,0,0)
		_UVDistortSpeed("UV Distort: Speed", Vector) = (2,2,0,0)
		_UVDistortNoiseScale("UV Distort: Noise Scale", Vector) = (0.1,0.1,0,0)
		[Toggle(_UVDISTORTMASKTOGGLE_ON)] _UVDistortMaskToggle("UV Distort: Mask Toggle", Float) = 0
		[NoScaleOffset]_UVDistortMask("UV Distort: Mask", 2D) = "white" {}
		[Toggle(_ENABLEUVSCROLL_ON)] _EnableUVScroll("Enable UV Scroll", Float) = 0
		_UVScrollSpeed("UV Scroll: Speed", Vector) = (0.2,0,0,0)
		[Toggle(_ENABLEUVROTATE_ON)] _EnableUVRotate("Enable UV Rotate", Float) = 0
		_UVRotateSpeed("UV Rotate: Speed", Float) = 1
		_UVRotatePivot("UV Rotate: Pivot", Vector) = (0.5,0.5,0,0)
		[Toggle(_ENABLESINEROTATE_ON)] _EnableSineRotate("Enable Sine Rotate", Float) = 0
		_SineRotateFade("Sine Rotate: Fade", Range( 0 , 1)) = 1
		_SineRotateAngle("Sine Rotate: Angle", Float) = 15
		_SineRotateFrequency("Sine Rotate: Frequency", Float) = 1
		_SineRotatePivot("Sine Rotate: Pivot", Vector) = (0.5,0.5,0,0)
		[Toggle(_ENABLEWIGGLE_ON)] _EnableWiggle("Enable Wiggle", Float) = 0
		_WiggleFade("Wiggle: Fade", Range( 0 , 1)) = 1
		_WiggleSpeed("Wiggle: Speed", Float) = 2
		_WiggleFrequency("Wiggle: Frequency", Float) = 2
		_WiggleOffset("Wiggle: Offset", Float) = 0.02
		[Toggle(_WIGGLEFIXEDGROUNDTOGGLE_ON)] _WiggleFixedGroundToggle("Wiggle: Fixed Ground Toggle", Float) = 0
		[Toggle(_ENABLEUVSCALE_ON)] _EnableUVScale("Enable UV Scale", Float) = 0
		_UVScaleScale("UV Scale: Scale", Vector) = (1,1,0,0)
		_UVScalePivot("UV Scale: Pivot", Vector) = (0.5,0.5,0,0)
		[Toggle(_ENABLESINEMOVE_ON)] _EnableSineMove("Enable Sine Move", Float) = 0
		_SineMoveFade("Sine Move: Fade", Range( 0 , 1)) = 1
		_SineMoveOffset("Sine Move: Offset", Vector) = (0,0.5,0,0)
		_SineMoveFrequency("Sine Move: Frequency", Vector) = (1,1,0,0)
		[Toggle(_ENABLESINESCALE_ON)] _EnableSineScale("Enable Sine Scale", Float) = 0
		_SineScaleFrequency("Sine Scale: Frequency", Float) = 2
		_SineScaleFactor("Sine Scale: Factor", Vector) = (0.2,0.2,0,0)
		[Toggle(_ENABLEVIBRATE_ON)] _EnableVibrate("Enable Vibrate", Float) = 0
		_VibrateFade("Vibrate: Fade", Range( 0 , 1)) = 1
		_VibrateOffset("Vibrate: Offset", Float) = 0.04
		_VibrateFrequency("Vibrate: Frequency", Float) = 100
		_VibrateRotation("Vibrate: Rotation", Float) = 4
		[Toggle(_ENABLEWIND_ON)] _EnableWind("Enable Wind", Float) = 0
		_WindRotation("Wind: Rotation", Float) = 0
		_WindMaxRotation("Wind: Max Rotation", Float) = 2
		_WindRotationWindFactor("Wind: Rotation Wind Factor", Float) = 1
		_WindSquishFactor("Wind: Squish Factor", Float) = 0.3
		_WindSquishWindFactor("Wind: Squish Wind Factor", Range( 0 , 1)) = 0
		[Toggle(_WINDLOCALWIND_ON)] _WindLocalWind("Wind: Local Wind", Float) = 0
		_WindNoiseScale("Wind: Noise Scale", Float) = 0.1
		_WindNoiseSpeed("Wind: Noise Speed", Float) = 1
		_WindMinIntensity("Wind: Min Intensity", Float) = -0.4
		_WindMaxIntensity("Wind: Max Intensity", Float) = 0.4
		[Toggle(_WINDHIGHQUALITYNOISE_ON)] _WindHighQualityNoise("Wind: High Quality Noise", Float) = 0
		[Toggle(_WINDISPARALLAX_ON)] _WindIsParallax("Wind: Is Parallax", Float) = 0
		_WindXPosition("Wind: X Position", Float) = 0
		_WindFlip("Wind: Flip", Float) = 0
		[Toggle(_ENABLESQUISH_ON)] _EnableSquish("Enable Squish", Float) = 0
		_SquishFade("Squish: Fade", Range( 0 , 1)) = 1
		_SquishStretch("Squish: Stretch", Float) = 0.1
		_SquishSquish("Squish: Squish", Float) = 0.1
		_SquishFlip("Squish: Flip", Range( -1 , 0)) = 0
		[Toggle(_ENABLECHECKERBOARD_ON)] _EnableCheckerboard("Enable Checkerboard", Float) = 0
		_CheckerboardDarken("Checkerboard: Darken", Range( 0 , 1)) = 0.5
		_CheckerboardTiling("Checkerboard: Tiling", Float) = 1
		[Toggle(_ENABLEFLAME_ON)] _EnableFlame("Enable Flame", Float) = 0
		_FlameBrightness("Flame: Brightness", Float) = 10
		_FlameSmooth("Flame: Smooth", Float) = 2
		_FlameRadius("Flame: Radius", Float) = 0.2
		_FlameSpeed("Flame: Speed", Vector) = (0,-0.5,0,0)
		_FlameNoiseFactor("Flame: Noise Factor", Float) = 2.5
		_FlameNoiseHeightFactor("Flame: Noise Height Factor", Float) = 1.5
		_FlameNoiseScale("Flame: Noise Scale", Vector) = (1.2,0.8,0,0)
		[Toggle(_ENABLEGAUSSIANBLUR_ON)] _EnableGaussianBlur("Enable Gaussian Blur", Float) = 0
		_GaussianBlurFade("Gaussian Blur: Fade", Range( 0 , 1)) = 1
		_GaussianBlurOffset("Gaussian Blur: Offset", Float) = 0.5
		[Toggle(_ENABLESHARPEN_ON)] _EnableSharpen("Enable Sharpen", Float) = 0
		_SharpenFade("Sharpen: Fade", Range( 0 , 1)) = 1
		_SharpenFactor("Sharpen: Factor", Float) = 4
		_SharpenOffset("Sharpen: Offset", Float) = 2
		[Toggle(_ENABLESMOOTHPIXELART_ON)] _EnableSmoothPixelArt("Enable Smooth Pixel Art", Float) = 0
		[Toggle(_ENABLESMOKE_ON)] _EnableSmoke("Enable Smoke", Float) = 0
		_SmokeAlpha("Smoke: Alpha", Range( 0 , 1)) = 1
		_SmokeSmoothness("Smoke: Smoothness", Float) = 1
		_SmokeNoiseScale("Smoke: Noise Scale", Float) = 0.5
		_SmokeNoiseFactor("Smoke: Noise Factor", Range( 0 , 1)) = 0.4
		_SmokeDarkEdge("Smoke: Dark Edge", Range( 0 , 1.5)) = 1
		[Toggle]_SmokeVertexSeed("Smoke: Vertex Seed", Float) = 0
		[Toggle(_ENABLECUSTOMFADE_ON)] _EnableCustomFade("Enable Custom Fade", Float) = 0
		_CustomFadeFadeMask("Custom Fade: Fade Mask", 2D) = "white" {}
		_CustomFadeSmoothness("Custom Fade: Smoothness", Float) = 2
		_CustomFadeNoiseScale("Custom Fade: Noise Scale", Vector) = (1,1,0,0)
		_CustomFadeNoiseFactor("Custom Fade: Noise Factor", Range( 0 , 0.5)) = 0
		_CustomFadeAlpha("Custom Fade: Alpha", Range( 0 , 1)) = 1
		[Toggle(_ENABLEWORLDTILING_ON)] _EnableWorldTiling("Enable World Tiling", Float) = 0
		_WorldTilingScale("World Tiling: Scale", Vector) = (1,1,0,0)
		_WorldTilingOffset("World Tiling: Offset", Vector) = (0,0,0,0)
		_WorldTilingPixelsPerUnit("World Tiling: Pixels Per Unit", Float) = 100
		[Toggle(_ENABLESCREENTILING_ON)] _EnableScreenTiling("Enable Screen Tiling", Float) = 0
		_ScreenTilingScale("Screen Tiling: Scale", Vector) = (1,1,0,0)
		_ScreenTilingOffset("Screen Tiling: Offset", Vector) = (0,0,0,0)
		[ASEEnd]_ScreenTilingPixelsPerUnit("Screen Tiling: Pixels Per Unit", Float) = 100
		[HideInInspector] _texcoord( "", 2D ) = "white" {}

		[HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
	}

	SubShader
	{
		LOD 0

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "UniversalMaterialType"="Lit" "ShaderGraphShader"="true" }

		Cull Off
		HLSLINCLUDE
		#pragma target 2.0
		#pragma prefer_hlslcc gles
		// ensure rendering platforms toggle list is visible

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
		ENDHLSL

		
		Pass
		{
			Name "Sprite Lit"
			Tags { "LightMode"="Universal2D" }

			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZTest LEqual
			ZWrite Off
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM

			#define ASE_SRP_VERSION 140007


			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ USE_SHAPE_LIGHT_TYPE_0
			#pragma multi_compile _ USE_SHAPE_LIGHT_TYPE_1
			#pragma multi_compile _ USE_SHAPE_LIGHT_TYPE_2
			#pragma multi_compile _ USE_SHAPE_LIGHT_TYPE_3

			#define _SURFACE_TYPE_TRANSPARENT 1

			#define SHADERPASS SHADERPASS_SPRITELIT
			#define SHADERPASS_SPRITELIT

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

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

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature_local _SHADERFADING_NONE _SHADERFADING_FULL _SHADERFADING_MASK _SHADERFADING_DISSOLVE _SHADERFADING_SPREAD
			#pragma shader_feature_local _ENABLESINESCALE_ON
			#pragma shader_feature _ENABLEVIBRATE_ON
			#pragma shader_feature _ENABLESINEMOVE_ON
			#pragma shader_feature _ENABLESQUISH_ON
			#pragma shader_feature _SPRITESHEETFIX_ON
			#pragma shader_feature_local _PIXELPERFECTUV_ON
			#pragma shader_feature _ENABLEWORLDTILING_ON
			#pragma shader_feature _ENABLESCREENTILING_ON
			#pragma shader_feature _TOGGLETIMEFREQUENCY_ON
			#pragma shader_feature _TOGGLETIMEFPS_ON
			#pragma shader_feature _TOGGLETIMESPEED_ON
			#pragma shader_feature _TOGGLEUNSCALEDTIME_ON
			#pragma shader_feature _TOGGLECUSTOMTIME_ON
			#pragma shader_feature _SHADERSPACE_UV _SHADERSPACE_UV_RAW _SHADERSPACE_OBJECT _SHADERSPACE_OBJECT_SCALED _SHADERSPACE_WORLD _SHADERSPACE_UI_GRAPHIC _SHADERSPACE_SCREEN
			#pragma shader_feature _PIXELPERFECTSPACE_ON
			#pragma shader_feature _BAKEDMATERIAL_ON
			#pragma shader_feature _VERTEXTINTFIRST_ON
			#pragma shader_feature _ENABLESHADOW_ON
			#pragma shader_feature _ENABLESTRONGTINT_ON
			#pragma shader_feature _ENABLEALPHATINT_ON
			#pragma shader_feature_local _ENABLEADDCOLOR_ON
			#pragma shader_feature_local _ENABLEHALFTONE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALGLOWFADE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALALPHAFADE_ON
			#pragma shader_feature_local _ENABLESOURCEGLOWDISSOLVE_ON
			#pragma shader_feature_local _ENABLESOURCEALPHADISSOLVE_ON
			#pragma shader_feature_local _ENABLEFULLGLOWDISSOLVE_ON
			#pragma shader_feature_local _ENABLEFULLALPHADISSOLVE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALDISTORTION_ON
			#pragma shader_feature_local _ENABLEFULLDISTORTION_ON
			#pragma shader_feature _ENABLETEXTURELAYER2_ON
			#pragma shader_feature _ENABLETEXTURELAYER1_ON
			#pragma shader_feature _ENABLESHIFTING_ON
			#pragma shader_feature _ENABLEENCHANTED_ON
			#pragma shader_feature_local _ENABLEPOISON_ON
			#pragma shader_feature_local _ENABLESHINE_ON
			#pragma shader_feature_local _ENABLERAINBOW_ON
			#pragma shader_feature_local _ENABLEBURN_ON
			#pragma shader_feature_local _ENABLEFROZEN_ON
			#pragma shader_feature_local _ENABLEMETAL_ON
			#pragma shader_feature_local _ENABLECAMOUFLAGE_ON
			#pragma shader_feature_local _ENABLEGLITCH_ON
			#pragma shader_feature_local _ENABLEHOLOGRAM_ON
			#pragma shader_feature _ENABLEPINGPONGGLOW_ON
			#pragma shader_feature_local _ENABLEPIXELOUTLINE_ON
			#pragma shader_feature_local _ENABLEOUTEROUTLINE_ON
			#pragma shader_feature_local _ENABLEINNEROUTLINE_ON
			#pragma shader_feature_local _ENABLESATURATION_ON
			#pragma shader_feature_local _ENABLESINEGLOW_ON
			#pragma shader_feature_local _ENABLEADDHUE_ON
			#pragma shader_feature_local _ENABLESHIFTHUE_ON
			#pragma shader_feature_local _ENABLEINKSPREAD_ON
			#pragma shader_feature_local _ENABLEBLACKTINT_ON
			#pragma shader_feature_local _ENABLESPLITTONING_ON
			#pragma shader_feature_local _ENABLEHUE_ON
			#pragma shader_feature_local _ENABLEBRIGHTNESS_ON
			#pragma shader_feature_local _ENABLECONTRAST_ON
			#pragma shader_feature _ENABLENEGATIVE_ON
			#pragma shader_feature_local _ENABLECOLORREPLACE_ON
			#pragma shader_feature_local _ENABLERECOLORRGBYCP_ON
			#pragma shader_feature _ENABLERECOLORRGB_ON
			#pragma shader_feature_local _ENABLEFLAME_ON
			#pragma shader_feature_local _ENABLECHECKERBOARD_ON
			#pragma shader_feature_local _ENABLECUSTOMFADE_ON
			#pragma shader_feature_local _ENABLESMOKE_ON
			#pragma shader_feature _ENABLESHARPEN_ON
			#pragma shader_feature _ENABLEGAUSSIANBLUR_ON
			#pragma shader_feature _ENABLESMOOTHPIXELART_ON
			#pragma shader_feature_local _TILINGFIX_ON
			#pragma shader_feature _ENABLEWIGGLE_ON
			#pragma shader_feature_local _ENABLEUVSCALE_ON
			#pragma shader_feature_local _ENABLEPIXELATE_ON
			#pragma shader_feature_local _ENABLEUVSCROLL_ON
			#pragma shader_feature_local _ENABLEUVROTATE_ON
			#pragma shader_feature_local _ENABLESINEROTATE_ON
			#pragma shader_feature_local _ENABLESQUEEZE_ON
			#pragma shader_feature_local _ENABLEUVDISTORT_ON
			#pragma shader_feature_local _ENABLEWIND_ON
			#pragma shader_feature_local _WINDLOCALWIND_ON
			#pragma shader_feature_local _WINDHIGHQUALITYNOISE_ON
			#pragma shader_feature_local _WINDISPARALLAX_ON
			#pragma shader_feature _UVDISTORTMASKTOGGLE_ON
			#pragma shader_feature _WIGGLEFIXEDGROUNDTOGGLE_ON
			#pragma shader_feature _RECOLORRGBTEXTURETOGGLE_ON
			#pragma shader_feature _RECOLORRGBYCPTEXTURETOGGLE_ON
			#pragma shader_feature_local _ADDHUEMASKTOGGLE_ON
			#pragma shader_feature_local _SINEGLOWMASKTOGGLE_ON
			#pragma shader_feature _INNEROUTLINETEXTURETOGGLE_ON
			#pragma shader_feature_local _INNEROUTLINEDISTORTIONTOGGLE_ON
			#pragma shader_feature _INNEROUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature _OUTEROUTLINETEXTURETOGGLE_ON
			#pragma shader_feature _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature_local _OUTEROUTLINEDISTORTIONTOGGLE_ON
			#pragma shader_feature _PIXELOUTLINETEXTURETOGGLE_ON
			#pragma shader_feature _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature _CAMOUFLAGEANIMATIONTOGGLE_ON
			#pragma shader_feature _METALMASKTOGGLE_ON
			#pragma shader_feature _SHINEMASKTOGGLE_ON
			#pragma shader_feature _ENCHANTEDLERPTOGGLE_ON
			#pragma shader_feature _ENCHANTEDRAINBOWTOGGLE_ON
			#pragma shader_feature _SHIFTINGRAINBOWTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1CONTRASTTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1SHEETTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1SCROLLTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2CONTRASTTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2SHEETTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2SCROLLTOGGLE_ON
			#pragma shader_feature _ADDCOLORCONTRASTTOGGLE_ON
			#pragma shader_feature _ADDCOLORMASKTOGGLE_ON
			#pragma shader_feature _STRONGTINTCONTRASTTOGGLE_ON
			#pragma shader_feature _STRONGTINTMASKTOGGLE_ON


			sampler2D _MainTex;
			#ifdef _TOGGLEUNSCALEDTIME_ON
			float UnscaledTime;
			#endif
			#ifdef _SHADERFADING_MASK
			sampler2D _FadingMask;
			#endif
			sampler2D _UberNoiseTexture;
			#ifdef _ENABLEWIND_ON
			float WindMinIntensity;
			float WindMaxIntensity;
			float WindNoiseScale;
			float WindTime;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			sampler2D _UVDistortMask;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			sampler2D _CustomFadeFadeMask;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			sampler2D _RecolorRGBTexture;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			sampler2D _RecolorRGBYCPTexture;
			#endif
			#ifdef _ENABLEADDHUE_ON
			sampler2D _AddHueMask;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			sampler2D _SineGlowMask;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			sampler2D _InnerOutlineTintTexture;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			sampler2D _OuterOutlineTintTexture;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			sampler2D _PixelOutlineTintTexture;
			#endif
			#ifdef _ENABLEMETAL_ON
			sampler2D _MetalMask;
			#endif
			#ifdef _ENABLESHINE_ON
			sampler2D _ShineMask;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			sampler2D _TextureLayer1Texture;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			sampler2D _TextureLayer2Texture;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			sampler2D _AddColorMask;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			sampler2D _StrongTintMask;
			#endif
			sampler2D _MaskMap;
			sampler2D _NormalMap;
			CBUFFER_START( UnityPerMaterial )
			#ifdef _ENABLESHIFTING_ON
			float4 _ShiftingColorA;
			#endif
			#ifdef _ENABLEPOISON_ON
			float4 _PoisonColor;
			#endif
			#ifdef _ENABLESPLITTONING_ON
			float4 _SplitToningHighlightsColor;
			float4 _SplitToningShadowsColor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float4 _EnchantedLowColor;
			float4 _EnchantedHighColor;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float4 _ColorReplaceToColor;
			float4 _ColorReplaceFromColor;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			float4 _RecolorRGBYCPRedTint;
			float4 _RecolorRGBYCPYellowTint;
			float4 _RecolorRGBYCPGreenTint;
			float4 _RecolorRGBYCPCyanTint;
			float4 _RecolorRGBYCPBlueTint;
			float4 _RecolorRGBYCPPurpleTint;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			float4 _RecolorRGBBlueTint;
			float4 _RecolorRGBGreenTint;
			float4 _RecolorRGBRedTint;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float4 _ShiftingColorB;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float4 _TextureLayer1Color;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float4 _TextureLayer2Color;
			#endif
			#ifdef _ENABLEBLACKTINT_ON
			float4 _BlackTintColor;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float4 _InkSpreadColor;
			#endif
			#ifdef _ENABLESHINE_ON
			float4 _ShineMask_ST;
			float4 _ShineColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenTint;
			#endif
			#ifdef _ENABLEMETAL_ON
			float4 _MetalMask_ST;
			float4 _MetalColor;
			float4 _MetalHighlightColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenHighlightColor;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float4 _CamouflageColorB;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnInsideNoiseColor;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float4 _CamouflageColorA;
			float4 _CamouflageBaseColor;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float4 _UVDistortMask_ST;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnInsideColor;
			#endif
			#ifdef _ENABLEPINGPONGGLOW_ON
			float4 _PingPongGlowTo;
			float4 _PingPongGlowFrom;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnEdgeColor;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float4 _PixelOutlineColor;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float4 _OuterOutlineColor;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float4 _InnerOutlineColor;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			float4 _SineGlowMask_ST;
			float4 _SineGlowColor;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float4 _AddHueMask_ST;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float4 _HologramTint;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float4 _FullGlowDissolveEdgeColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenSnowColor;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float4 _StrongTintTint;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float4 _AddColorMask_ST;
			float4 _AddColorColor;
			#endif
			#ifdef _ENABLESHADOW_ON
			float4 _ShadowColor;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float4 _SourceGlowDissolveEdgeColor;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float4 _DirectionalGlowFadeEdgeColor;
			#endif
			#ifdef _SPRITESHEETFIX_ON
			float4 _SpriteSheetRect;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float4 _AlphaTintColor;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float4 _StrongTintMask_ST;
			#endif
			#ifdef _SHADERFADING_MASK
			float4 _FadingMask_ST;
			#endif
			float4 _MainTex_TexelSize;
			float2 _FadingNoiseScale;
			#ifdef _SHADERFADING_SPREAD
			float2 _FadingPosition;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnEdgeNoiseScale;
			float2 _BurnPosition;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float2 _DirectionalAlphaFadeNoiseScale;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnSwirlNoiseScale;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenSnowScale;
			#endif
			#ifdef _ENABLEFLAME_ON
			float2 _FlameNoiseScale;
			float2 _FlameSpeed;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float2 _GlitchNoiseSpeed;
			float2 _GlitchNoiseScale;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			float2 _CustomFadeNoiseScale;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float2 _TextureLayer1Offset;
			float2 _TextureLayer1ScrollSpeed;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageDistortionSpeed;
			float2 _CamouflageDistortionScale;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnInsideNoiseScale;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float2 _TextureLayer1Scale;
			#endif
			#ifdef _ENABLESHADOW_ON
			float2 _ShadowOffset;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageDistortionIntensity;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float2 _SineMoveFrequency;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float2 _InnerOutlineTextureSpeed;
			float2 _InnerOutlineNoiseSpeed;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float2 _InkSpreadNoiseScale;
			float2 _InkSpreadPosition;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float2 _InnerOutlineNoiseScale;
			float2 _InnerOutlineDistortionIntensity;
			#endif
			#ifdef _ENABLEPOISON_ON
			float2 _PoisonNoiseSpeed;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float2 _HalftonePosition;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float2 _DirectionalGlowFadeNoiseScale;
			#endif
			#ifdef _ENABLEPOISON_ON
			float2 _PoisonNoiseScale;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float2 _OuterOutlineTextureSpeed;
			#endif
			#ifdef _ENABLESINESCALE_ON
			float2 _SineScaleFactor;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float2 _RainbowCenter;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float2 _OuterOutlineNoiseSpeed;
			float2 _OuterOutlineNoiseScale;
			float2 _OuterOutlineDistortionIntensity;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float2 _EnchantedSpeed;
			float2 _EnchantedScale;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float2 _PixelOutlineTextureSpeed;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float2 _RainbowNoiseScale;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageNoiseScaleA;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float2 _SineMoveOffset;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float2 _WorldTilingOffset;
			#endif
			#ifdef _ENABLEFULLALPHADISSOLVE_ON
			float2 _FullAlphaDissolveNoiseScale;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float2 _UVDistortNoiseScale;
			float2 _UVDistortTo;
			float2 _UVDistortFrom;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float2 _FullGlowDissolveNoiseScale;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float2 _GlitchMaskScale;
			float2 _GlitchMaskSpeed;
			float2 _GlitchDistortion;
			float2 _GlitchDistortionScale;
			float2 _GlitchDistortionSpeed;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortion;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float2 _SqueezeCenter;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortionScale;
			#endif
			#ifdef _ENABLESOURCEALPHADISSOLVE_ON
			float2 _SourceAlphaDissolvePosition;
			float2 _SourceAlphaDissolveNoiseScale;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float2 _ScreenTilingOffset;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionNoiseScale;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float2 _SourceGlowDissolvePosition;
			float2 _SourceGlowDissolveNoiseScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionDistortion;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float2 _ScreenTilingScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionDistortionScale;
			#endif
			#ifdef _ENABLEFULLDISTORTION_ON
			float2 _FullDistortionDistortion;
			float2 _FullDistortionNoiseScale;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortionSpeed;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseScale;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float2 _UVDistortSpeed;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float2 _SqueezeScale;
			#endif
			#ifdef _ENABLEUVSCALE_ON
			float2 _UVScaleScale;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float2 _WorldTilingScale;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageNoiseScaleB;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightScale;
			#endif
			#ifdef _ENABLEUVSCROLL_ON
			float2 _UVScrollSpeed;
			#endif
			#ifdef _ENABLEUVROTATE_ON
			float2 _UVRotatePivot;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2ScrollSpeed;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightSpeed;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2Offset;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float2 _SineRotatePivot;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseDistortionSpeed;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2Scale;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseDistortionScale;
			float2 _MetalNoiseDistortion;
			float2 _MetalNoiseSpeed;
			#endif
			#ifdef _ENABLEUVSCALE_ON
			float2 _UVScalePivot;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenHighlightDensity;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowSpeed;
			float _RainbowSaturation;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenFade;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowNoiseFactor;
			float _RainbowContrast;
			float _RainbowBrightness;
			float _RainbowDensity;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float _AddColorFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnInsideNoiseFactor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenHighlightContrast;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnEdgeNoiseFactor;
			#endif
			#ifdef _ENABLESHADOW_ON
			float _ShadowFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnSwirlFactor;
			float _BurnFade;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float _StrongTintContrast;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnRadius;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float _AlphaTintMinAlpha;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnInsideContrast;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float _AlphaTintFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnWidth;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float _StrongTintFade;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveFade;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonRecolorFactor;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowFade;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingBrightness;
			float _ShiftingSaturation;
			float _ShiftingContrast;
			float _ShiftingFade;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1EdgeClip;
			#endif
			int _TextureLayer1Columns;
			int _TextureLayer1Rows;
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1Speed;
			#endif
			int _TextureLayer1StartFrame;
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeRotation;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1Contrast;
			float _TextureLayer1Fade;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2EdgeClip;
			#endif
			int _TextureLayer2Columns;
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingDensity;
			#endif
			int _TextureLayer2Rows;
			int _TextureLayer2StartFrame;
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeInvert;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2Contrast;
			float _TextureLayer2Fade;
			#endif
			#ifdef _ENABLEFULLALPHADISSOLVE_ON
			float _FullAlphaDissolveFade;
			float _FullAlphaDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveInvert;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float _FullGlowDissolveFade;
			float _FullGlowDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEALPHADISSOLVE_ON
			float _SourceAlphaDissolveInvert;
			float _SourceAlphaDissolveFade;
			float _SourceAlphaDissolveNoiseFactor;
			float _SourceAlphaDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveNoiseFactor;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2Speed;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float _AddColorContrast;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingSpeed;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedFade;
			#endif
			#ifdef _ENABLESHINE_ON
			float _ShineSaturation;
			float _ShineContrast;
			float _ShineRotation;
			float _ShineFrequency;
			float _ShineSpeed;
			float _ShineWidth;
			float _ShineSmooth;
			float _ShineFade;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneFadeWidth;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonShiftSpeed;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneFade;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonDensity;
			float _PoisonFade;
			float _PoisonNoiseBrightness;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeFade;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneTiling;
			float _HalftoneInvert;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeWidth;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedRainbowDensity;
			float _EnchantedRainbowSpeed;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeNoiseFactor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedRainbowSaturation;
			float _EnchantedContrast;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeFade;
			float _DirectionalGlowFadeRotation;
			float _DirectionalGlowFadeInvert;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedBrightness;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeWidth;
			float _DirectionalAlphaFadeNoiseFactor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedReduce;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveWidth;
			#endif
			#ifdef _ENABLESQUISH_ON
			float _SquishStretch;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float _PixelOutlineWidth;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenSnowContrast;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float _SqueezeFade;
			float _SqueezePower;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float _UVDistortFade;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float _GlitchFade;
			float _GlitchMaskMin;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramFade;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateFrequency;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramDistortionOffset;
			float _HologramDistortionDensity;
			float _HologramDistortionSpeed;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionWidth;
			float _DirectionalDistortionNoiseFactor;
			float _DirectionalDistortionFade;
			float _DirectionalDistortionRotation;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramDistortionScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionInvert;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateAngle;
			#endif
			#ifdef _ENABLEUVROTATE_ON
			float _UVRotateSpeed;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeNoiseScale;
			float _SmokeVertexSeed;
			#endif
			#ifdef _ENABLESHARPEN_ON
			float _SharpenFade;
			float _SharpenFactor;
			float _SharpenOffset;
			#endif
			#ifdef _ENABLEGAUSSIANBLUR_ON
			float _GaussianBlurFade;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateFade;
			#endif
			#ifdef _ENABLEGAUSSIANBLUR_ON
			float _GaussianBlurOffset;
			#endif
			#ifdef _ENABLEWIGGLE_ON
			float _WiggleOffset;
			float _WiggleSpeed;
			float _WiggleFrequency;
			#endif
			#ifdef _ENABLEPIXELATE_ON
			float _PixelateFade;
			float _PixelatePixelsPerUnit;
			float _PixelatePixelDensity;
			#endif
			#ifdef _ENABLEWIGGLE_ON
			float _WiggleFade;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionRandomDirection;
			#endif
			#ifdef _ENABLEFULLDISTORTION_ON
			float _FullDistortionFade;
			#endif
			#ifdef _ENABLEWIND_ON
			float _WindSquishWindFactor;
			#endif
			#ifdef _ENABLEVIBRATE_ON
			float _VibrateFade;
			float _VibrateOffset;
			float _VibrateFrequency;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float _SineMoveFade;
			#endif
			#ifdef _TOGGLETIMEFREQUENCY_ON
			float _TimeRange;
			float _TimeFrequency;
			#endif
			#ifdef _ENABLEVIBRATE_ON
			float _VibrateRotation;
			#endif
			#ifdef _TOGGLETIMEFPS_ON
			float _TimeFPS;
			#endif
			#ifdef _TOGGLECUSTOMTIME_ON
			float _TimeValue;
			#endif
			#ifdef _ENABLESQUISH_ON
			float _SquishSquish;
			float _SquishFlip;
			float _SquishFade;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float _WorldTilingPixelsPerUnit;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float _ScreenTilingPixelsPerUnit;
			#endif
			#ifdef _TOGGLETIMESPEED_ON
			float _TimeSpeed;
			#endif
			#ifdef _ENABLESINESCALE_ON
			float _SineScaleFrequency;
			#endif
			float _FadingFade;
			float _FadingWidth;
			#ifdef _ENABLEWIND_ON
			float _WindSquishFactor;
			float _WindFlip;
			float _WindMaxRotation;
			float _WindRotation;
			float _WindNoiseSpeed;
			float _WindNoiseScale;
			float _WindXPosition;
			float _WindMaxIntensity;
			float _WindMinIntensity;
			float _WindRotationWindFactor;
			#endif
			#ifdef _SHADERFADING_SPREAD
			float _FadingNoiseFactor;
			#endif
			#ifdef _SHADERSPACE_SCREEN
			float _ScreenWidthUnits;
			#endif
			#ifdef _SHADERSPACE_UI_GRAPHIC
			float _RectHeight;
			float _RectWidth;
			#endif
			float _PixelsPerUnit;
			#ifdef _ENABLESMOKE_ON
			float _SmokeNoiseFactor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenSnowDensity;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeSmoothness;
			float _SmokeAlpha;
			#endif
			#ifdef _ENABLEPINGPONGGLOW_ON
			float _PingPongGlowContrast;
			float _PingPongGlowFade;
			float _PingPongGlowFrequency;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float _PixelOutlineFade;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float _OuterOutlineWidth;
			float _OuterOutlineFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramContrast;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float _InnerOutlineWidth;
			#endif
			#ifdef _ENABLESATURATION_ON
			float _Saturation;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			float _SineGlowMin;
			float _SineGlowMax;
			float _SineGlowFrequency;
			float _SineGlowFade;
			float _SineGlowContrast;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float _InnerOutlineFade;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramLineSpeed;
			float _HologramLineGap;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenContrast;
			#endif
			#ifdef _ENABLEMETAL_ON
			float _MetalFade;
			float _MetalContrast;
			float _MetalHighlightContrast;
			float _MetalHighlightDensity;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramLineFrequency;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageContrast;
			float _CamouflageDensityB;
			float _CamouflageSmoothnessA;
			float _CamouflageDensityA;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float _GlitchHueSpeed;
			float _GlitchBrightness;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramMinAlpha;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageSmoothnessB;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueContrast;
			float _AddHueBrightness;
			float _AddHueSaturation;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceSmoothness;
			float _ColorReplaceContrast;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			float _RecolorRGBYCPFade;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			float _RecolorRGBFade;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameBrightness;
			float _FlameSmooth;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceRange;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameRadius;
			float _FlameNoiseHeightFactor;
			#endif
			#ifdef _ENABLECHECKERBOARD_ON
			float _CheckerboardTiling;
			float _CheckerboardDarken;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			float _CustomFadeAlpha;
			float _CustomFadeSmoothness;
			float _CustomFadeNoiseFactor;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameNoiseFactor;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceFade;
			#endif
			#ifdef _ENABLENEGATIVE_ON
			float _NegativeFade;
			#endif
			#ifdef _ENABLECONTRAST_ON
			float _Contrast;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueSpeed;
			#endif
			#ifdef _ENABLESHIFTHUE_ON
			float _ShiftHueSpeed;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float _InkSpreadWidth;
			float _InkSpreadNoiseFactor;
			float _InkSpreadDistance;
			float _InkSpreadFade;
			float _InkSpreadContrast;
			#endif
			#ifdef _ENABLEBLACKTINT_ON
			float _BlackTintFade;
			float _BlackTintPower;
			#endif
			#ifdef _ENABLESPLITTONING_ON
			float _SplitToningFade;
			float _SplitToningContrast;
			float _SplitToningBalance;
			float _SplitToningShift;
			#endif
			#ifdef _ENABLEHUE_ON
			float _Hue;
			#endif
			#ifdef _ENABLEBRIGHTNESS_ON
			float _Brightness;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeDarkEdge;
			#endif
			float _NormalIntensity;
			CBUFFER_END


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 uv0 : TEXCOORD0;
				float4 color : COLOR;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 texCoord0 : TEXCOORD0;
				float4 color : TEXCOORD1;
				float4 screenPosition : TEXCOORD2;
				float3 positionWS : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			#if ETC1_EXTERNAL_ALPHA
				TEXTURE2D(_AlphaTex); SAMPLER(sampler_AlphaTex);
				float _EnableAlphaTexture;
			#endif

			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float MyCustomExpression16_g11716( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11714( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float FastNoise101_g11665( float x )
			{
				float i = floor(x);
				float f = frac(x);
				float s = sign(frac(x/2.0)-0.5);
				    
				float k = 0.5+0.5*sin(i);
				return s*f*(f-1.0)*((16.0*k-4.0)*f*(f-1.0)-1.0);
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float MyCustomExpression16_g11667( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11668( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11671( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11670( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11676( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11677( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11718( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11673( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11729( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float4 texturePointSmooth( sampler2D tex, float4 textureTexelSize, float2 uvs )
			{
				float2 size;
				size.x = textureTexelSize.z;
				size.y = textureTexelSize.w;
				float2 pixel = float2(1.0,1.0) / size;
				uvs -= pixel * float2(0.5,0.5);
				float2 uv_pixels = uvs * size;
				float2 delta_pixel = frac(uv_pixels) - float2(0.5,0.5);
				float2 ddxy = fwidth(uv_pixels);
				float2 mip = log2(ddxy) - 0.5;
				float2 clampedUV = uvs + (clamp(delta_pixel / ddxy, 0.0, 1.0) - delta_pixel) * pixel;
				return tex2Dlod(tex, float4(clampedUV,0, min(mip.x, mip.y)));
			}
			
			float MyCustomExpression16_g11755( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11757( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11761( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float3 RGBToHSV(float3 c)
			{
				float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
				float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
				float d = q.x - min( q.w, q.y );
				float e = 1.0e-10;
				return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
			}
			float3 MyCustomExpression115_g11766( float3 In, float3 From, float3 To, float Fuzziness, float Range )
			{
				float Distance = distance(From, In);
				return lerp(To, In, saturate((Distance - Range) / max(Fuzziness, 0.001)));
			}
			
			float3 HSVToRGB( float3 c )
			{
				float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
				float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
				return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
			}
			
			float MyCustomExpression16_g11789( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11773( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11800( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11808( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11846( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11843( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11845( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11835( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11837( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11828( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11830( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11831( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11823( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11821( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11822( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11817( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11849( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11854( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11852( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float2 MyCustomExpression10_g11864( float2 UV, int Width, int Height, float Tile, float2 Invert )
			{
				Tile = fmod(Tile + 0.001, Width * Height);
				float2 tileCount = float2(1, 1) / float2(Width, Height);
				float tileY = abs(Invert.y * Height - (floor(Tile * tileCount.x) + Invert.y * 1));
				 float tileX = abs(Invert.x * Width - ((Tile - Width * floor(Tile * tileCount.x)) + Invert.x * 1));
				return (UV + float2(tileX, tileY)) * tileCount;
			}
			
			float2 MyCustomExpression10_g11860( float2 UV, int Width, int Height, float Tile, float2 Invert )
			{
				Tile = fmod(Tile + 0.001, Width * Height);
				float2 tileCount = float2(1, 1) / float2(Width, Height);
				float tileY = abs(Invert.y * Height - (floor(Tile * tileCount.x) + Invert.y * 1));
				 float tileX = abs(Invert.x * Width - ((Tile - Width * floor(Tile * tileCount.x)) + Invert.x * 1));
				return (UV + float2(tileX, tileY)) * tileCount;
			}
			
			float MyCustomExpression16_g11871( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11879( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11881( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11877( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11873( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11875( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			

			VertexOutput vert ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 _ZeroVector = float2(0,0);
				float2 texCoord363 = v.uv0.xy * float2( 1,1 ) + float2( 0,0 );
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				#ifdef _ENABLESCREENTILING_ON
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult16_g11656 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11656 = ( ( ( (( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / 10.0 ) )).xy * _ScreenTilingScale ) + _ScreenTilingOffset ) * ( _ScreenTilingPixelsPerUnit * appendResult16_g11656 ) );
				#else
				float2 staticSwitch2_g11656 = texCoord363;
				#endif
				float3 ase_worldPos = TransformObjectToWorld( (v.vertex).xyz );
				#ifdef _ENABLEWORLDTILING_ON
				float2 appendResult16_g11657 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11657 = ( ( ( (ase_worldPos).xy * _WorldTilingScale ) + _WorldTilingOffset ) * ( _WorldTilingPixelsPerUnit * appendResult16_g11657 ) );
				#else
				float2 staticSwitch2_g11657 = staticSwitch2_g11656;
				#endif
				float2 originalUV460 = staticSwitch2_g11657;
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11658 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch449 = ( floor( ( originalUV460 * appendResult7_g11658 ) ) / appendResult7_g11658 );
				#else
				float2 staticSwitch449 = originalUV460;
				#endif
				float2 uvAfterPixelArt450 = staticSwitch449;
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11662 = uvAfterPixelArt450;
				float2 appendResult374 = (float2(_SpriteSheetRect.x , _SpriteSheetRect.y));
				float2 spriteRectMin376 = appendResult374;
				float2 break11_g11662 = spriteRectMin376;
				float2 appendResult375 = (float2(_SpriteSheetRect.z , _SpriteSheetRect.w));
				float2 spriteRectMax377 = appendResult375;
				float2 break10_g11662 = spriteRectMax377;
				float2 break9_g11662 = float2( 0,0 );
				float2 break8_g11662 = float2( 1,1 );
				float2 appendResult15_g11662 = (float2((break9_g11662.x + (break14_g11662.x - break11_g11662.x) * (break8_g11662.x - break9_g11662.x) / (break10_g11662.x - break11_g11662.x)) , (break9_g11662.y + (break14_g11662.y - break11_g11662.y) * (break8_g11662.y - break9_g11662.y) / (break10_g11662.y - break11_g11662.y))));
				float2 staticSwitch366 = appendResult15_g11662;
				#else
				float2 staticSwitch366 = uvAfterPixelArt450;
				#endif
				float2 fixedUV475 = staticSwitch366;
				#ifdef _ENABLESQUISH_ON
				float2 break77_g11897 = fixedUV475;
				float2 appendResult72_g11897 = (float2(( _SquishStretch * ( break77_g11897.x - 0.5 ) * _SquishFade ) , ( _SquishFade * ( break77_g11897.y + _SquishFlip ) * -_SquishSquish )));
				float2 staticSwitch198 = ( appendResult72_g11897 + _ZeroVector );
				#else
				float2 staticSwitch198 = _ZeroVector;
				#endif
				float2 temp_output_2_0_g11899 = staticSwitch198;
				#ifdef _TOGGLECUSTOMTIME_ON
				float staticSwitch44_g11661 = _TimeValue;
				#else
				float staticSwitch44_g11661 = _TimeParameters.x;
				#endif
				#ifdef _TOGGLEUNSCALEDTIME_ON
				float staticSwitch34_g11661 = UnscaledTime;
				#else
				float staticSwitch34_g11661 = staticSwitch44_g11661;
				#endif
				#ifdef _TOGGLETIMESPEED_ON
				float staticSwitch37_g11661 = ( staticSwitch34_g11661 * _TimeSpeed );
				#else
				float staticSwitch37_g11661 = staticSwitch34_g11661;
				#endif
				#ifdef _TOGGLETIMEFPS_ON
				float staticSwitch38_g11661 = ( floor( ( staticSwitch37_g11661 * _TimeFPS ) ) / _TimeFPS );
				#else
				float staticSwitch38_g11661 = staticSwitch37_g11661;
				#endif
				#ifdef _TOGGLETIMEFREQUENCY_ON
				float staticSwitch42_g11661 = ( ( sin( ( staticSwitch38_g11661 * _TimeFrequency ) ) * _TimeRange ) + 100.0 );
				#else
				float staticSwitch42_g11661 = staticSwitch38_g11661;
				#endif
				float shaderTime237 = staticSwitch42_g11661;
				float temp_output_8_0_g11899 = shaderTime237;
				#ifdef _ENABLESINEMOVE_ON
				float2 staticSwitch4_g11899 = ( ( sin( ( temp_output_8_0_g11899 * _SineMoveFrequency ) ) * _SineMoveOffset * _SineMoveFade ) + temp_output_2_0_g11899 );
				#else
				float2 staticSwitch4_g11899 = temp_output_2_0_g11899;
				#endif
				#ifdef _ENABLEVIBRATE_ON
				float temp_output_30_0_g11900 = temp_output_8_0_g11899;
				float3 rotatedValue21_g11900 = RotateAroundAxis( float3( 0,0,0 ), float3( 0,1,0 ), float3( 0,0,1 ), ( temp_output_30_0_g11900 * _VibrateRotation ) );
				float2 staticSwitch6_g11899 = ( ( sin( ( _VibrateFrequency * temp_output_30_0_g11900 ) ) * _VibrateOffset * _VibrateFade * (rotatedValue21_g11900).xy ) + staticSwitch4_g11899 );
				#else
				float2 staticSwitch6_g11899 = staticSwitch4_g11899;
				#endif
				#ifdef _ENABLESINESCALE_ON
				float2 staticSwitch10_g11899 = ( staticSwitch6_g11899 + ( (v.vertex.xyz).xy * ( ( ( sin( ( _SineScaleFrequency * temp_output_8_0_g11899 ) ) + 1.0 ) * 0.5 ) * _SineScaleFactor ) ) );
				#else
				float2 staticSwitch10_g11899 = staticSwitch6_g11899;
				#endif
				float2 temp_output_424_0 = staticSwitch10_g11899;
#ifdef _SHADERFADING_MASK
				float2 uv_FadingMask = v.uv0.xy * _FadingMask_ST.xy + _FadingMask_ST.zw;
				#endif
#ifdef _SHADERFADING_MASK
				float4 tex2DNode3_g11712 = tex2Dlod( _FadingMask, float4( uv_FadingMask, 0, 0.0) );
				#endif
				float temp_output_4_0_g11715 = max( _FadingWidth , 0.001 );
				float2 texCoord435 = v.uv0.xy * float2( 1,1 ) + float2( 0,0 );
				#ifdef _PIXELPERFECTSPACE_ON
				float2 temp_output_432_0 = (_MainTex_TexelSize).zw;
				float2 staticSwitch437 = ( floor( ( texCoord435 * temp_output_432_0 ) ) / temp_output_432_0 );
				#else
				float2 staticSwitch437 = texCoord435;
				#endif
				float2 temp_output_61_0_g11663 = staticSwitch437;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 texCoord23_g11663 = v.uv0.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERSPACE_UI_GRAPHIC
				float2 appendResult28_g11663 = (float2(_RectWidth , _RectHeight));
				#endif
				#if defined(_SHADERSPACE_UV)
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#elif defined(_SHADERSPACE_UV_RAW)
				float2 staticSwitch1_g11663 = temp_output_61_0_g11663;
				#elif defined(_SHADERSPACE_OBJECT)
				float2 staticSwitch1_g11663 = (v.vertex.xyz).xy;
				#elif defined(_SHADERSPACE_OBJECT_SCALED)
				float2 staticSwitch1_g11663 = ( (v.vertex.xyz).xy * (ase_objectScale).xy );
				#elif defined(_SHADERSPACE_WORLD)
				float2 staticSwitch1_g11663 = (ase_worldPos).xy;
				#elif defined(_SHADERSPACE_UI_GRAPHIC)
				float2 staticSwitch1_g11663 = ( texCoord23_g11663 * ( appendResult28_g11663 / _PixelsPerUnit ) );
				#elif defined(_SHADERSPACE_SCREEN)
				float2 staticSwitch1_g11663 = ( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / _ScreenWidthUnits ) );
				#else
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#endif
				float2 shaderPosition235 = staticSwitch1_g11663;
				float linValue16_g11716 = tex2Dlod( _UberNoiseTexture, float4( ( shaderPosition235 * _FadingNoiseScale ), 0, 0.0) ).r;
				float localMyCustomExpression16_g11716 = MyCustomExpression16_g11716( linValue16_g11716 );
				float clampResult14_g11715 = clamp( ( ( ( _FadingFade * ( 1.0 + temp_output_4_0_g11715 ) ) - localMyCustomExpression16_g11716 ) / temp_output_4_0_g11715 ) , 0.0 , 1.0 );
				float2 temp_output_27_0_g11713 = shaderPosition235;
				float linValue16_g11714 = tex2Dlod( _UberNoiseTexture, float4( ( temp_output_27_0_g11713 * _FadingNoiseScale ), 0, 0.0) ).r;
				float localMyCustomExpression16_g11714 = MyCustomExpression16_g11714( linValue16_g11714 );
#ifdef _SHADERFADING_SPREAD
				float clampResult3_g11713 = clamp( ( ( _FadingFade - ( distance( _FadingPosition , temp_output_27_0_g11713 ) + ( localMyCustomExpression16_g11714 * _FadingNoiseFactor ) ) ) / max( _FadingWidth , 0.001 ) ) , 0.0 , 1.0 );
				#endif
				#if defined(_SHADERFADING_NONE)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_FULL)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_MASK)
				float staticSwitch139 = ( _FadingFade * ( tex2DNode3_g11712.r * tex2DNode3_g11712.a ) );
				#elif defined(_SHADERFADING_DISSOLVE)
				float staticSwitch139 = clampResult14_g11715;
				#elif defined(_SHADERFADING_SPREAD)
				float staticSwitch139 = clampResult3_g11713;
				#else
				float staticSwitch139 = _FadingFade;
				#endif
				float fullFade123 = staticSwitch139;
				float2 lerpResult121 = lerp( float2( 0,0 ) , temp_output_424_0 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float2 staticSwitch142 = temp_output_424_0;
				#elif defined(_SHADERFADING_FULL)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_MASK)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_DISSOLVE)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_SPREAD)
				float2 staticSwitch142 = lerpResult121;
				#else
				float2 staticSwitch142 = temp_output_424_0;
				#endif
				
				o.ase_texcoord4 = screenPos;
				
				o.ase_texcoord5 = v.vertex;
				o.ase_color = v.color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = float3( staticSwitch142 ,  0.0 );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.normal = v.normal;
				v.tangent.xyz = v.tangent.xyz;

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);

				o.texCoord0 = v.uv0;
				o.color = v.color;
				o.clipPos = vertexInput.positionCS;
				o.screenPosition = vertexInput.positionNDC;
				o.positionWS = vertexInput.positionWS;
				return o;
			}

			half4 frag ( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );
				float3 positionWS = IN.positionWS.xyz;

				float2 texCoord363 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float4 screenPos = IN.ase_texcoord4;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				#ifdef _ENABLESCREENTILING_ON
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult16_g11656 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11656 = ( ( ( (( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / 10.0 ) )).xy * _ScreenTilingScale ) + _ScreenTilingOffset ) * ( _ScreenTilingPixelsPerUnit * appendResult16_g11656 ) );
				#else
				float2 staticSwitch2_g11656 = texCoord363;
				#endif
				#ifdef _ENABLEWORLDTILING_ON
				float2 appendResult16_g11657 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11657 = ( ( ( (positionWS).xy * _WorldTilingScale ) + _WorldTilingOffset ) * ( _WorldTilingPixelsPerUnit * appendResult16_g11657 ) );
				#else
				float2 staticSwitch2_g11657 = staticSwitch2_g11656;
				#endif
				float2 originalUV460 = staticSwitch2_g11657;
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11658 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch449 = ( floor( ( originalUV460 * appendResult7_g11658 ) ) / appendResult7_g11658 );
				#else
				float2 staticSwitch449 = originalUV460;
				#endif
				float2 uvAfterPixelArt450 = staticSwitch449;
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11662 = uvAfterPixelArt450;
				float2 appendResult374 = (float2(_SpriteSheetRect.x , _SpriteSheetRect.y));
				float2 spriteRectMin376 = appendResult374;
				float2 break11_g11662 = spriteRectMin376;
				float2 appendResult375 = (float2(_SpriteSheetRect.z , _SpriteSheetRect.w));
				float2 spriteRectMax377 = appendResult375;
				float2 break10_g11662 = spriteRectMax377;
				float2 break9_g11662 = float2( 0,0 );
				float2 break8_g11662 = float2( 1,1 );
				float2 appendResult15_g11662 = (float2((break9_g11662.x + (break14_g11662.x - break11_g11662.x) * (break8_g11662.x - break9_g11662.x) / (break10_g11662.x - break11_g11662.x)) , (break9_g11662.y + (break14_g11662.y - break11_g11662.y) * (break8_g11662.y - break9_g11662.y) / (break10_g11662.y - break11_g11662.y))));
				float2 staticSwitch366 = appendResult15_g11662;
				#else
				float2 staticSwitch366 = uvAfterPixelArt450;
				#endif
				float2 fixedUV475 = staticSwitch366;
				float2 temp_output_3_0_g11664 = fixedUV475;
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch117_g11665 = _WindMinIntensity;
				#else
				float staticSwitch117_g11665 = WindMinIntensity;
				#endif
				#endif
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch118_g11665 = _WindMaxIntensity;
				#else
				float staticSwitch118_g11665 = WindMaxIntensity;
				#endif
				#endif
				float4 transform62_g11665 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDISPARALLAX_ON
				float staticSwitch111_g11665 = _WindXPosition;
				#else
				float staticSwitch111_g11665 = transform62_g11665.x;
				#endif
				#endif
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch113_g11665 = _WindNoiseScale;
				#else
				float staticSwitch113_g11665 = WindNoiseScale;
				#endif
				#endif
				#ifdef _TOGGLECUSTOMTIME_ON
				float staticSwitch44_g11661 = _TimeValue;
				#else
				float staticSwitch44_g11661 = _TimeParameters.x;
				#endif
				#ifdef _TOGGLEUNSCALEDTIME_ON
				float staticSwitch34_g11661 = UnscaledTime;
				#else
				float staticSwitch34_g11661 = staticSwitch44_g11661;
				#endif
				#ifdef _TOGGLETIMESPEED_ON
				float staticSwitch37_g11661 = ( staticSwitch34_g11661 * _TimeSpeed );
				#else
				float staticSwitch37_g11661 = staticSwitch34_g11661;
				#endif
				#ifdef _TOGGLETIMEFPS_ON
				float staticSwitch38_g11661 = ( floor( ( staticSwitch37_g11661 * _TimeFPS ) ) / _TimeFPS );
				#else
				float staticSwitch38_g11661 = staticSwitch37_g11661;
				#endif
				#ifdef _TOGGLETIMEFREQUENCY_ON
				float staticSwitch42_g11661 = ( ( sin( ( staticSwitch38_g11661 * _TimeFrequency ) ) * _TimeRange ) + 100.0 );
				#else
				float staticSwitch42_g11661 = staticSwitch38_g11661;
				#endif
				float shaderTime237 = staticSwitch42_g11661;
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch125_g11665 = ( shaderTime237 * _WindNoiseSpeed );
				#else
				float staticSwitch125_g11665 = WindTime;
				#endif
				float temp_output_50_0_g11665 = ( ( staticSwitch111_g11665 * staticSwitch113_g11665 ) + staticSwitch125_g11665 );
				float x101_g11665 = temp_output_50_0_g11665;
				float localFastNoise101_g11665 = FastNoise101_g11665( x101_g11665 );
				float2 temp_cast_0 = (temp_output_50_0_g11665).xx;
				float simplePerlin2D121_g11665 = snoise( temp_cast_0*0.5 );
				simplePerlin2D121_g11665 = simplePerlin2D121_g11665*0.5 + 0.5;
				#ifdef _WINDHIGHQUALITYNOISE_ON
				float staticSwitch123_g11665 = simplePerlin2D121_g11665;
				#else
				float staticSwitch123_g11665 = ( localFastNoise101_g11665 + 0.5 );
				#endif
				float lerpResult86_g11665 = lerp( staticSwitch117_g11665 , staticSwitch118_g11665 , staticSwitch123_g11665);
				float clampResult29_g11665 = clamp( ( ( _WindRotationWindFactor * lerpResult86_g11665 ) + _WindRotation ) , -_WindMaxRotation , _WindMaxRotation );
				float2 temp_output_1_0_g11665 = temp_output_3_0_g11664;
				float temp_output_39_0_g11665 = ( temp_output_1_0_g11665.y + _WindFlip );
				float3 appendResult43_g11665 = (float3(0.5 , -_WindFlip , 0.0));
				float2 appendResult27_g11665 = (float2(0.0 , ( _WindSquishFactor * min( ( ( _WindSquishWindFactor * abs( lerpResult86_g11665 ) ) + abs( _WindRotation ) ) , _WindMaxRotation ) * temp_output_39_0_g11665 )));
				float3 rotatedValue19_g11665 = RotateAroundAxis( appendResult43_g11665, float3( ( appendResult27_g11665 + temp_output_1_0_g11665 ) ,  0.0 ), float3( 0,0,1 ), ( clampResult29_g11665 * temp_output_39_0_g11665 ) );
				float2 staticSwitch4_g11664 = (rotatedValue19_g11665).xy;
				#else
				float2 staticSwitch4_g11664 = temp_output_3_0_g11664;
				#endif
				float2 texCoord435 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				#ifdef _PIXELPERFECTSPACE_ON
				float2 temp_output_432_0 = (_MainTex_TexelSize).zw;
				float2 staticSwitch437 = ( floor( ( texCoord435 * temp_output_432_0 ) ) / temp_output_432_0 );
				#else
				float2 staticSwitch437 = texCoord435;
				#endif
				float2 temp_output_61_0_g11663 = staticSwitch437;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 texCoord23_g11663 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERSPACE_UI_GRAPHIC
				float2 appendResult28_g11663 = (float2(_RectWidth , _RectHeight));
				#endif
				#if defined(_SHADERSPACE_UV)
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#elif defined(_SHADERSPACE_UV_RAW)
				float2 staticSwitch1_g11663 = temp_output_61_0_g11663;
				#elif defined(_SHADERSPACE_OBJECT)
				float2 staticSwitch1_g11663 = (IN.ase_texcoord5.xyz).xy;
				#elif defined(_SHADERSPACE_OBJECT_SCALED)
				float2 staticSwitch1_g11663 = ( (IN.ase_texcoord5.xyz).xy * (ase_objectScale).xy );
				#elif defined(_SHADERSPACE_WORLD)
				float2 staticSwitch1_g11663 = (positionWS).xy;
				#elif defined(_SHADERSPACE_UI_GRAPHIC)
				float2 staticSwitch1_g11663 = ( texCoord23_g11663 * ( appendResult28_g11663 / _PixelsPerUnit ) );
				#elif defined(_SHADERSPACE_SCREEN)
				float2 staticSwitch1_g11663 = ( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / _ScreenWidthUnits ) );
				#else
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#endif
				float2 shaderPosition235 = staticSwitch1_g11663;
				#ifdef _ENABLEFULLDISTORTION_ON
				float2 temp_output_195_0_g11666 = shaderPosition235;
				float linValue16_g11667 = tex2D( _UberNoiseTexture, ( temp_output_195_0_g11666 * _FullDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11667 = MyCustomExpression16_g11667( linValue16_g11667 );
				float linValue16_g11668 = tex2D( _UberNoiseTexture, ( ( temp_output_195_0_g11666 + float2( 0.321,0.321 ) ) * _FullDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11668 = MyCustomExpression16_g11668( linValue16_g11668 );
				float2 appendResult189_g11666 = (float2(( localMyCustomExpression16_g11667 - 0.5 ) , ( localMyCustomExpression16_g11668 - 0.5 )));
				float2 staticSwitch83 = ( staticSwitch4_g11664 + ( ( 1.0 - _FullDistortionFade ) * appendResult189_g11666 * _FullDistortionDistortion ) );
				#else
				float2 staticSwitch83 = staticSwitch4_g11664;
				#endif
				#ifdef _ENABLEDIRECTIONALDISTORTION_ON
				float2 temp_output_182_0_g11669 = shaderPosition235;
				float linValue16_g11671 = tex2D( _UberNoiseTexture, ( temp_output_182_0_g11669 * _DirectionalDistortionDistortionScale ) ).r;
				float localMyCustomExpression16_g11671 = MyCustomExpression16_g11671( linValue16_g11671 );
				float3 rotatedValue168_g11669 = RotateAroundAxis( float3( 0,0,0 ), float3( _DirectionalDistortionDistortion ,  0.0 ), float3( 0,0,1 ), ( ( ( localMyCustomExpression16_g11671 - 0.5 ) * 2.0 * _DirectionalDistortionRandomDirection ) * PI ) );
				float3 rotatedValue136_g11669 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_182_0_g11669 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalDistortionRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11669 = rotatedValue136_g11669;
				float linValue16_g11670 = tex2D( _UberNoiseTexture, ( temp_output_182_0_g11669 * _DirectionalDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11670 = MyCustomExpression16_g11670( linValue16_g11670 );
				float clampResult154_g11669 = clamp( ( ( break130_g11669.x + break130_g11669.y + _DirectionalDistortionFade + ( localMyCustomExpression16_g11670 * _DirectionalDistortionNoiseFactor ) ) / max( _DirectionalDistortionWidth , 0.001 ) ) , 0.0 , 1.0 );
				float2 staticSwitch82 = ( staticSwitch83 + ( (rotatedValue168_g11669).xy * ( 1.0 - (( _DirectionalDistortionInvert )?( ( 1.0 - clampResult154_g11669 ) ):( clampResult154_g11669 )) ) ) );
				#else
				float2 staticSwitch82 = staticSwitch83;
				#endif
				#ifdef _ENABLEHOLOGRAM_ON
				float temp_output_8_0_g11674 = ( ( ( shaderTime237 * _HologramDistortionSpeed ) + positionWS.y ) / unity_OrthoParams.y );
				float2 temp_cast_4 = (temp_output_8_0_g11674).xx;
				float2 temp_cast_5 = (_HologramDistortionDensity).xx;
				float linValue16_g11676 = tex2D( _UberNoiseTexture, ( temp_cast_4 * temp_cast_5 ) ).r;
				float localMyCustomExpression16_g11676 = MyCustomExpression16_g11676( linValue16_g11676 );
				float clampResult75_g11674 = clamp( localMyCustomExpression16_g11676 , 0.075 , 0.6 );
				float2 temp_cast_6 = (temp_output_8_0_g11674).xx;
				float2 temp_cast_7 = (_HologramDistortionScale).xx;
				float linValue16_g11677 = tex2D( _UberNoiseTexture, ( temp_cast_6 * temp_cast_7 ) ).r;
				float localMyCustomExpression16_g11677 = MyCustomExpression16_g11677( linValue16_g11677 );
				float2 appendResult2_g11675 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float hologramFade182 = _HologramFade;
				float2 appendResult44_g11674 = (float2(( ( ( clampResult75_g11674 * ( localMyCustomExpression16_g11677 - 0.5 ) ) * _HologramDistortionOffset * ( 100.0 / appendResult2_g11675 ).x ) * hologramFade182 ) , 0.0));
				float2 staticSwitch59 = ( staticSwitch82 + appendResult44_g11674 );
				#else
				float2 staticSwitch59 = staticSwitch82;
				#endif
				#ifdef _ENABLEGLITCH_ON
				float2 temp_output_18_0_g11672 = shaderPosition235;
				float2 glitchPosition154 = temp_output_18_0_g11672;
				float linValue16_g11718 = tex2D( _UberNoiseTexture, ( ( glitchPosition154 + ( _GlitchDistortionSpeed * shaderTime237 ) ) * _GlitchDistortionScale ) ).r;
				float localMyCustomExpression16_g11718 = MyCustomExpression16_g11718( linValue16_g11718 );
				float linValue16_g11673 = tex2D( _UberNoiseTexture, ( ( temp_output_18_0_g11672 + ( _GlitchMaskSpeed * shaderTime237 ) ) * _GlitchMaskScale ) ).r;
				float localMyCustomExpression16_g11673 = MyCustomExpression16_g11673( linValue16_g11673 );
				float glitchFade152 = ( max( localMyCustomExpression16_g11673 , _GlitchMaskMin ) * _GlitchFade );
				float2 staticSwitch62 = ( staticSwitch59 + ( ( localMyCustomExpression16_g11718 - 0.5 ) * _GlitchDistortion * glitchFade152 ) );
				#else
				float2 staticSwitch62 = staticSwitch59;
				#endif
				float2 temp_output_1_0_g11719 = staticSwitch62;
				float2 temp_output_26_0_g11719 = shaderPosition235;
				float temp_output_25_0_g11719 = shaderTime237;
				#ifdef _ENABLEUVDISTORT_ON
				float linValue16_g11729 = tex2D( _UberNoiseTexture, ( ( temp_output_26_0_g11719 + ( _UVDistortSpeed * temp_output_25_0_g11719 ) ) * _UVDistortNoiseScale ) ).r;
				float localMyCustomExpression16_g11729 = MyCustomExpression16_g11729( linValue16_g11729 );
				float2 lerpResult21_g11726 = lerp( _UVDistortFrom , _UVDistortTo , localMyCustomExpression16_g11729);
				float2 appendResult2_g11728 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 uv_UVDistortMask = IN.texCoord0.xy * _UVDistortMask_ST.xy + _UVDistortMask_ST.zw;
				float4 tex2DNode3_g11727 = tex2D( _UVDistortMask, uv_UVDistortMask );
				#ifdef _UVDISTORTMASKTOGGLE_ON
				float staticSwitch29_g11726 = ( _UVDistortFade * ( tex2DNode3_g11727.r * tex2DNode3_g11727.a ) );
				#else
				float staticSwitch29_g11726 = _UVDistortFade;
				#endif
				float2 staticSwitch5_g11719 = ( temp_output_1_0_g11719 + ( lerpResult21_g11726 * ( 100.0 / appendResult2_g11728 ) * staticSwitch29_g11726 ) );
				#else
				float2 staticSwitch5_g11719 = temp_output_1_0_g11719;
				#endif
				#ifdef _ENABLESQUEEZE_ON
				float2 temp_output_1_0_g11725 = staticSwitch5_g11719;
				float2 staticSwitch7_g11719 = ( temp_output_1_0_g11725 + ( ( temp_output_1_0_g11725 - _SqueezeCenter ) * pow( distance( temp_output_1_0_g11725 , _SqueezeCenter ) , _SqueezePower ) * _SqueezeScale * _SqueezeFade ) );
				#else
				float2 staticSwitch7_g11719 = staticSwitch5_g11719;
				#endif
				#ifdef _ENABLESINEROTATE_ON
				float3 rotatedValue36_g11724 = RotateAroundAxis( float3( _SineRotatePivot ,  0.0 ), float3( staticSwitch7_g11719 ,  0.0 ), float3( 0,0,1 ), ( sin( ( temp_output_25_0_g11719 * _SineRotateFrequency ) ) * ( ( _SineRotateAngle / 360.0 ) * PI ) * _SineRotateFade ) );
				float2 staticSwitch9_g11719 = (rotatedValue36_g11724).xy;
				#else
				float2 staticSwitch9_g11719 = staticSwitch7_g11719;
				#endif
				#ifdef _ENABLEUVROTATE_ON
				float3 rotatedValue8_g11723 = RotateAroundAxis( float3( _UVRotatePivot ,  0.0 ), float3( staticSwitch9_g11719 ,  0.0 ), float3( 0,0,1 ), ( temp_output_25_0_g11719 * _UVRotateSpeed * PI ) );
				float2 staticSwitch16_g11719 = (rotatedValue8_g11723).xy;
				#else
				float2 staticSwitch16_g11719 = staticSwitch9_g11719;
				#endif
				#ifdef _ENABLEUVSCROLL_ON
				float2 staticSwitch14_g11719 = ( ( _UVScrollSpeed * temp_output_25_0_g11719 ) + staticSwitch16_g11719 );
				#else
				float2 staticSwitch14_g11719 = staticSwitch16_g11719;
				#endif
				#ifdef _ENABLEPIXELATE_ON
				float2 appendResult35_g11721 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 MultFactor30_g11721 = ( ( _PixelatePixelDensity * ( appendResult35_g11721 / _PixelatePixelsPerUnit ) ) * ( 1.0 / max( _PixelateFade , 1E-05 ) ) );
				float2 clampResult46_g11721 = clamp( ( floor( ( MultFactor30_g11721 * ( staticSwitch14_g11719 + ( float2( 0.5,0.5 ) / MultFactor30_g11721 ) ) ) ) / MultFactor30_g11721 ) , float2( 0,0 ) , float2( 1,1 ) );
				float2 staticSwitch4_g11719 = clampResult46_g11721;
				#else
				float2 staticSwitch4_g11719 = staticSwitch14_g11719;
				#endif
				#ifdef _ENABLEUVSCALE_ON
				float2 staticSwitch24_g11719 = ( ( ( staticSwitch4_g11719 - _UVScalePivot ) / _UVScaleScale ) + _UVScalePivot );
				#else
				float2 staticSwitch24_g11719 = staticSwitch4_g11719;
				#endif
				float2 temp_output_1_0_g11730 = staticSwitch24_g11719;
				#ifdef _ENABLEWIGGLE_ON
				float temp_output_7_0_g11730 = ( sin( ( _WiggleFrequency * ( temp_output_26_0_g11719.y + ( _WiggleSpeed * temp_output_25_0_g11719 ) ) ) ) * _WiggleOffset * _WiggleFade );
				#ifdef _WIGGLEFIXEDGROUNDTOGGLE_ON
				float staticSwitch18_g11730 = ( temp_output_7_0_g11730 * temp_output_1_0_g11730.y );
				#else
				float staticSwitch18_g11730 = temp_output_7_0_g11730;
				#endif
				float2 appendResult12_g11730 = (float2(staticSwitch18_g11730 , 0.0));
				float2 staticSwitch13_g11730 = ( temp_output_1_0_g11730 + appendResult12_g11730 );
				#else
				float2 staticSwitch13_g11730 = temp_output_1_0_g11730;
				#endif
				float2 temp_output_484_0 = staticSwitch13_g11730;
				float2 texCoord131 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERFADING_MASK
				float2 uv_FadingMask = IN.texCoord0.xy * _FadingMask_ST.xy + _FadingMask_ST.zw;
				#endif
#ifdef _SHADERFADING_MASK
				float4 tex2DNode3_g11712 = tex2D( _FadingMask, uv_FadingMask );
				#endif
				float temp_output_4_0_g11715 = max( _FadingWidth , 0.001 );
				float linValue16_g11716 = tex2D( _UberNoiseTexture, ( shaderPosition235 * _FadingNoiseScale ) ).r;
				float localMyCustomExpression16_g11716 = MyCustomExpression16_g11716( linValue16_g11716 );
				float clampResult14_g11715 = clamp( ( ( ( _FadingFade * ( 1.0 + temp_output_4_0_g11715 ) ) - localMyCustomExpression16_g11716 ) / temp_output_4_0_g11715 ) , 0.0 , 1.0 );
				float2 temp_output_27_0_g11713 = shaderPosition235;
				float linValue16_g11714 = tex2D( _UberNoiseTexture, ( temp_output_27_0_g11713 * _FadingNoiseScale ) ).r;
				float localMyCustomExpression16_g11714 = MyCustomExpression16_g11714( linValue16_g11714 );
#ifdef _SHADERFADING_SPREAD
				float clampResult3_g11713 = clamp( ( ( _FadingFade - ( distance( _FadingPosition , temp_output_27_0_g11713 ) + ( localMyCustomExpression16_g11714 * _FadingNoiseFactor ) ) ) / max( _FadingWidth , 0.001 ) ) , 0.0 , 1.0 );
				#endif
				#if defined(_SHADERFADING_NONE)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_FULL)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_MASK)
				float staticSwitch139 = ( _FadingFade * ( tex2DNode3_g11712.r * tex2DNode3_g11712.a ) );
				#elif defined(_SHADERFADING_DISSOLVE)
				float staticSwitch139 = clampResult14_g11715;
				#elif defined(_SHADERFADING_SPREAD)
				float staticSwitch139 = clampResult3_g11713;
				#else
				float staticSwitch139 = _FadingFade;
				#endif
				float fullFade123 = staticSwitch139;
				float2 lerpResult130 = lerp( texCoord131 , temp_output_484_0 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float2 staticSwitch145 = temp_output_484_0;
				#elif defined(_SHADERFADING_FULL)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_MASK)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_DISSOLVE)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_SPREAD)
				float2 staticSwitch145 = lerpResult130;
				#else
				float2 staticSwitch145 = temp_output_484_0;
				#endif
				#ifdef _TILINGFIX_ON
				float2 staticSwitch485 = ( ( ( staticSwitch145 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				#else
				float2 staticSwitch485 = staticSwitch145;
				#endif
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11731 = staticSwitch485;
				float2 break11_g11731 = float2( 0,0 );
				float2 break10_g11731 = float2( 1,1 );
				float2 break9_g11731 = spriteRectMin376;
				float2 break8_g11731 = spriteRectMax377;
				float2 appendResult15_g11731 = (float2((break9_g11731.x + (break14_g11731.x - break11_g11731.x) * (break8_g11731.x - break9_g11731.x) / (break10_g11731.x - break11_g11731.x)) , (break9_g11731.y + (break14_g11731.y - break11_g11731.y) * (break8_g11731.y - break9_g11731.y) / (break10_g11731.y - break11_g11731.y))));
				float2 staticSwitch371 = min( max( appendResult15_g11731 , spriteRectMin376 ) , spriteRectMax377 );
				#else
				float2 staticSwitch371 = staticSwitch485;
				#endif
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11732 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch427 = ( originalUV460 + ( floor( ( ( staticSwitch371 - uvAfterPixelArt450 ) * appendResult7_g11732 ) ) / appendResult7_g11732 ) );
				#else
				float2 staticSwitch427 = staticSwitch371;
				#endif
				float2 finalUV146 = staticSwitch427;
				float2 temp_output_1_0_g11733 = finalUV146;
				#ifdef _ENABLESMOOTHPIXELART_ON
				sampler2D tex3_g11734 = _MainTex;
				float4 textureTexelSize3_g11734 = _MainTex_TexelSize;
				float2 uvs3_g11734 = temp_output_1_0_g11733;
				float4 localtexturePointSmooth3_g11734 = texturePointSmooth( tex3_g11734 , textureTexelSize3_g11734 , uvs3_g11734 );
				float4 staticSwitch8_g11733 = localtexturePointSmooth3_g11734;
				#else
				float4 staticSwitch8_g11733 = tex2D( _MainTex, temp_output_1_0_g11733 );
				#endif
				#ifdef _ENABLEGAUSSIANBLUR_ON
				float temp_output_10_0_g11735 = ( _GaussianBlurOffset * _GaussianBlurFade * 0.005 );
				float temp_output_2_0_g11745 = temp_output_10_0_g11735;
				float2 appendResult16_g11745 = (float2(temp_output_2_0_g11745 , 0.0));
				float2 appendResult25_g11747 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11747 = ( appendResult16_g11745 * appendResult25_g11747 );
				float2 temp_output_7_0_g11735 = temp_output_1_0_g11733;
				float2 temp_output_1_0_g11745 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( 1,1 ) ) );
				float2 temp_output_1_0_g11747 = temp_output_1_0_g11745;
				float2 appendResult17_g11745 = (float2(0.0 , temp_output_2_0_g11745));
				float2 appendResult25_g11746 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11746 = ( appendResult17_g11745 * appendResult25_g11746 );
				float2 temp_output_1_0_g11746 = temp_output_1_0_g11745;
				float temp_output_2_0_g11736 = temp_output_10_0_g11735;
				float2 appendResult16_g11736 = (float2(temp_output_2_0_g11736 , 0.0));
				float2 appendResult25_g11738 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11738 = ( appendResult16_g11736 * appendResult25_g11738 );
				float2 temp_output_1_0_g11736 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( -1,1 ) ) );
				float2 temp_output_1_0_g11738 = temp_output_1_0_g11736;
				float2 appendResult17_g11736 = (float2(0.0 , temp_output_2_0_g11736));
				float2 appendResult25_g11737 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11737 = ( appendResult17_g11736 * appendResult25_g11737 );
				float2 temp_output_1_0_g11737 = temp_output_1_0_g11736;
				float temp_output_2_0_g11742 = temp_output_10_0_g11735;
				float2 appendResult16_g11742 = (float2(temp_output_2_0_g11742 , 0.0));
				float2 appendResult25_g11744 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11744 = ( appendResult16_g11742 * appendResult25_g11744 );
				float2 temp_output_1_0_g11742 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( -1,-1 ) ) );
				float2 temp_output_1_0_g11744 = temp_output_1_0_g11742;
				float2 appendResult17_g11742 = (float2(0.0 , temp_output_2_0_g11742));
				float2 appendResult25_g11743 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11743 = ( appendResult17_g11742 * appendResult25_g11743 );
				float2 temp_output_1_0_g11743 = temp_output_1_0_g11742;
				float temp_output_2_0_g11739 = temp_output_10_0_g11735;
				float2 appendResult16_g11739 = (float2(temp_output_2_0_g11739 , 0.0));
				float2 appendResult25_g11741 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11741 = ( appendResult16_g11739 * appendResult25_g11741 );
				float2 temp_output_1_0_g11739 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( 1,-1 ) ) );
				float2 temp_output_1_0_g11741 = temp_output_1_0_g11739;
				float2 appendResult17_g11739 = (float2(0.0 , temp_output_2_0_g11739));
				float2 appendResult25_g11740 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11740 = ( appendResult17_g11739 * appendResult25_g11740 );
				float2 temp_output_1_0_g11740 = temp_output_1_0_g11739;
				float4 staticSwitch3_g11733 = ( ( ( ( tex2D( _MainTex, ( temp_output_26_0_g11747 + temp_output_1_0_g11747 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11747 + temp_output_1_0_g11747 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11746 + temp_output_1_0_g11746 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11746 + temp_output_1_0_g11746 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11738 + temp_output_1_0_g11738 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11738 + temp_output_1_0_g11738 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11737 + temp_output_1_0_g11737 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11737 + temp_output_1_0_g11737 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11744 + temp_output_1_0_g11744 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11744 + temp_output_1_0_g11744 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11743 + temp_output_1_0_g11743 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11743 + temp_output_1_0_g11743 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11741 + temp_output_1_0_g11741 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11741 + temp_output_1_0_g11741 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11740 + temp_output_1_0_g11740 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11740 + temp_output_1_0_g11740 ) ) ) ) ) * 0.0625 );
				#else
				float4 staticSwitch3_g11733 = staticSwitch8_g11733;
				#endif
				#ifdef _ENABLESHARPEN_ON
				float2 temp_output_1_0_g11748 = temp_output_1_0_g11733;
				float4 tex2DNode4_g11748 = tex2D( _MainTex, temp_output_1_0_g11748 );
				float temp_output_2_0_g11749 = _SharpenOffset;
				float2 appendResult16_g11749 = (float2(temp_output_2_0_g11749 , 0.0));
				float2 appendResult25_g11751 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11751 = ( appendResult16_g11749 * appendResult25_g11751 );
				float2 temp_output_1_0_g11749 = temp_output_1_0_g11748;
				float2 temp_output_1_0_g11751 = temp_output_1_0_g11749;
				float2 appendResult17_g11749 = (float2(0.0 , temp_output_2_0_g11749));
				float2 appendResult25_g11750 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11750 = ( appendResult17_g11749 * appendResult25_g11750 );
				float2 temp_output_1_0_g11750 = temp_output_1_0_g11749;
				float4 break22_g11748 = ( tex2DNode4_g11748 - ( ( ( ( ( tex2D( _MainTex, ( temp_output_26_0_g11751 + temp_output_1_0_g11751 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11751 + temp_output_1_0_g11751 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11750 + temp_output_1_0_g11750 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11750 + temp_output_1_0_g11750 ) ) ) ) / 4.0 ) - tex2DNode4_g11748 ) * ( _SharpenFactor * _SharpenFade ) ) );
				float clampResult23_g11748 = clamp( break22_g11748.a , 0.0 , 1.0 );
				float4 appendResult24_g11748 = (float4(break22_g11748.r , break22_g11748.g , break22_g11748.b , clampResult23_g11748));
				float4 staticSwitch12_g11733 = appendResult24_g11748;
				#else
				float4 staticSwitch12_g11733 = staticSwitch3_g11733;
				#endif
				float4 temp_output_471_0 = staticSwitch12_g11733;
				#ifdef _VERTEXTINTFIRST_ON
				float4 temp_output_1_0_g11752 = temp_output_471_0;
				float4 appendResult8_g11752 = (float4(( (temp_output_1_0_g11752).rgb * (IN.ase_color).rgb ) , temp_output_1_0_g11752.a));
				float4 staticSwitch354 = appendResult8_g11752;
				#else
				float4 staticSwitch354 = temp_output_471_0;
				#endif
				float4 originalColor191 = staticSwitch354;
				float4 temp_output_1_0_g11753 = originalColor191;
				float4 temp_output_1_0_g11754 = temp_output_1_0_g11753;
				float2 temp_output_7_0_g11753 = finalUV146;
				#ifdef _ENABLESMOKE_ON
				float2 temp_output_43_0_g11754 = temp_output_7_0_g11753;
				float2 temp_cast_15 = (_SmokeNoiseScale).xx;
				float linValue16_g11755 = tex2D( _UberNoiseTexture, ( ( ( IN.ase_color.r * (( _SmokeVertexSeed )?( 5.0 ):( 0.0 )) ) + temp_output_43_0_g11754 ) * temp_cast_15 ) ).r;
				float localMyCustomExpression16_g11755 = MyCustomExpression16_g11755( linValue16_g11755 );
				float clampResult28_g11754 = clamp( ( ( ( localMyCustomExpression16_g11755 - 1.0 ) * _SmokeNoiseFactor ) + ( ( ( IN.ase_color.a / 2.5 ) - distance( temp_output_43_0_g11754 , float2( 0.5,0.5 ) ) ) * 2.5 * _SmokeSmoothness ) ) , 0.0 , 1.0 );
				float3 lerpResult34_g11754 = lerp( (temp_output_1_0_g11754).rgb , float3( 0,0,0 ) , ( ( 1.0 - clampResult28_g11754 ) * _SmokeDarkEdge ));
				float4 appendResult31_g11754 = (float4(lerpResult34_g11754 , ( clampResult28_g11754 * _SmokeAlpha * temp_output_1_0_g11754.a )));
				float4 staticSwitch2_g11753 = appendResult31_g11754;
				#else
				float4 staticSwitch2_g11753 = temp_output_1_0_g11753;
				#endif
				#ifdef _ENABLECUSTOMFADE_ON
				float4 temp_output_1_0_g11756 = staticSwitch2_g11753;
				float2 temp_output_57_0_g11756 = temp_output_7_0_g11753;
				float4 tex2DNode3_g11756 = tex2D( _CustomFadeFadeMask, temp_output_57_0_g11756 );
				float linValue16_g11757 = tex2D( _UberNoiseTexture, ( temp_output_57_0_g11756 * _CustomFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11757 = MyCustomExpression16_g11757( linValue16_g11757 );
				float clampResult37_g11756 = clamp( ( ( ( IN.ase_color.a * 2.0 ) - 1.0 ) + ( tex2DNode3_g11756.r + ( localMyCustomExpression16_g11757 * _CustomFadeNoiseFactor ) ) ) , 0.0 , 1.0 );
				float4 appendResult13_g11756 = (float4((temp_output_1_0_g11756).rgb , ( temp_output_1_0_g11756.a * pow( clampResult37_g11756 , ( _CustomFadeSmoothness / max( tex2DNode3_g11756.r , 0.05 ) ) ) * _CustomFadeAlpha )));
				float4 staticSwitch3_g11753 = appendResult13_g11756;
				#else
				float4 staticSwitch3_g11753 = staticSwitch2_g11753;
				#endif
				float4 temp_output_1_0_g11758 = staticSwitch3_g11753;
				#ifdef _ENABLECHECKERBOARD_ON
				float4 temp_output_1_0_g11759 = temp_output_1_0_g11758;
				float2 appendResult4_g11759 = (float2(positionWS.x , positionWS.y));
				float2 temp_output_44_0_g11759 = ( appendResult4_g11759 * _CheckerboardTiling * 0.5 );
				float2 break12_g11759 = step( ( ceil( temp_output_44_0_g11759 ) - temp_output_44_0_g11759 ) , float2( 0.5,0.5 ) );
				float4 appendResult42_g11759 = (float4(( (temp_output_1_0_g11759).rgb * min( ( _CheckerboardDarken + abs( ( -break12_g11759.x + break12_g11759.y ) ) ) , 1.0 ) ) , temp_output_1_0_g11759.a));
				float4 staticSwitch2_g11758 = appendResult42_g11759;
				#else
				float4 staticSwitch2_g11758 = temp_output_1_0_g11758;
				#endif
				#ifdef _ENABLEFLAME_ON
				float2 temp_output_75_0_g11760 = finalUV146;
				float linValue16_g11761 = tex2D( _UberNoiseTexture, ( ( ( shaderTime237 * _FlameSpeed ) + temp_output_75_0_g11760 ) * _FlameNoiseScale ) ).r;
				float localMyCustomExpression16_g11761 = MyCustomExpression16_g11761( linValue16_g11761 );
				float saferPower57_g11760 = abs( max( ( temp_output_75_0_g11760.y - 0.2 ) , 0.0 ) );
				float temp_output_47_0_g11760 = max( _FlameRadius , 0.01 );
				float clampResult70_g11760 = clamp( ( ( ( localMyCustomExpression16_g11761 * pow( saferPower57_g11760 , _FlameNoiseHeightFactor ) * _FlameNoiseFactor ) + ( ( temp_output_47_0_g11760 - distance( temp_output_75_0_g11760 , float2( 0.5,0.4 ) ) ) / temp_output_47_0_g11760 ) ) * _FlameSmooth ) , 0.0 , 1.0 );
				float temp_output_63_0_g11760 = ( clampResult70_g11760 * _FlameBrightness );
				float4 appendResult31_g11760 = (float4(temp_output_63_0_g11760 , temp_output_63_0_g11760 , temp_output_63_0_g11760 , clampResult70_g11760));
				float4 staticSwitch6_g11758 = ( appendResult31_g11760 * staticSwitch2_g11758 );
				#else
				float4 staticSwitch6_g11758 = staticSwitch2_g11758;
				#endif
				float4 temp_output_3_0_g11762 = staticSwitch6_g11758;
				float4 temp_output_1_0_g11794 = temp_output_3_0_g11762;
				float4 appendResult91_g11794 = (float4((temp_output_1_0_g11794).rgb , 1.0));
				float2 temp_output_1_0_g11762 = finalUV146;
				#ifdef _ENABLERECOLORRGB_ON
				#ifdef _RECOLORRGBTEXTURETOGGLE_ON
				float4 staticSwitch81_g11794 = tex2D( _RecolorRGBTexture, temp_output_1_0_g11762 );
				#else
				float4 staticSwitch81_g11794 = appendResult91_g11794;
				#endif
				float3 break82_g11794 = (staticSwitch81_g11794).xyz;
				float temp_output_84_0_g11794 = max( ( break82_g11794.x + break82_g11794.y + break82_g11794.z ) , 0.001 );
				float4 break71_g11794 = ( ( _RecolorRGBRedTint * ( break82_g11794.x / temp_output_84_0_g11794 ) ) + ( _RecolorRGBGreenTint * ( break82_g11794.y / temp_output_84_0_g11794 ) ) + ( ( break82_g11794.z / temp_output_84_0_g11794 ) * _RecolorRGBBlueTint ) );
				float3 appendResult56_g11794 = (float3(break71_g11794.r , break71_g11794.g , break71_g11794.b));
				float4 break2_g11795 = temp_output_1_0_g11794;
				float saferPower57_g11794 = abs( ( ( break2_g11795.x + break2_g11795.x + break2_g11795.y + break2_g11795.y + break2_g11795.y + break2_g11795.z ) / 6.0 ) );
				float3 lerpResult26_g11794 = lerp( (temp_output_1_0_g11794).rgb , ( appendResult56_g11794 * pow( saferPower57_g11794 , ( max( break71_g11794.a , 0.01 ) * 2.0 ) ) ) , ( staticSwitch81_g11794.w * _RecolorRGBFade ));
				float4 appendResult30_g11794 = (float4(lerpResult26_g11794 , temp_output_1_0_g11794.a));
				float4 staticSwitch43_g11762 = appendResult30_g11794;
				#else
				float4 staticSwitch43_g11762 = temp_output_3_0_g11762;
				#endif
				#ifdef _ENABLERECOLORRGBYCP_ON
				float4 temp_output_1_0_g11792 = staticSwitch43_g11762;
				#ifdef _RECOLORRGBYCPTEXTURETOGGLE_ON
				float4 staticSwitch62_g11792 = tex2D( _RecolorRGBYCPTexture, temp_output_1_0_g11762 );
				#else
				float4 staticSwitch62_g11792 = temp_output_1_0_g11792;
				#endif
				float3 hsvTorgb33_g11792 = RGBToHSV( staticSwitch62_g11792.rgb );
				float temp_output_43_0_g11792 = ( ( hsvTorgb33_g11792.x + 0.08333334 ) % 1.0 );
				float4 ifLocalVar46_g11792 = 0;
				if( temp_output_43_0_g11792 >= 0.8333333 )
				ifLocalVar46_g11792 = _RecolorRGBYCPPurpleTint;
				else
				ifLocalVar46_g11792 = _RecolorRGBYCPBlueTint;
				float4 ifLocalVar44_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.6666667 )
				ifLocalVar44_g11792 = _RecolorRGBYCPCyanTint;
				else
				ifLocalVar44_g11792 = ifLocalVar46_g11792;
				float4 ifLocalVar47_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.3333333 )
				ifLocalVar47_g11792 = _RecolorRGBYCPYellowTint;
				else
				ifLocalVar47_g11792 = _RecolorRGBYCPGreenTint;
				float4 ifLocalVar45_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.1666667 )
				ifLocalVar45_g11792 = _RecolorRGBYCPRedTint;
				else
				ifLocalVar45_g11792 = ifLocalVar47_g11792;
				float4 ifLocalVar35_g11792 = 0;
				if( temp_output_43_0_g11792 >= 0.5 )
				ifLocalVar35_g11792 = ifLocalVar44_g11792;
				else
				ifLocalVar35_g11792 = ifLocalVar45_g11792;
				float4 break55_g11792 = ifLocalVar35_g11792;
				float3 appendResult56_g11792 = (float3(break55_g11792.r , break55_g11792.g , break55_g11792.b));
				float4 break2_g11793 = temp_output_1_0_g11792;
				float saferPower57_g11792 = abs( ( ( break2_g11793.x + break2_g11793.x + break2_g11793.y + break2_g11793.y + break2_g11793.y + break2_g11793.z ) / 6.0 ) );
				float3 lerpResult26_g11792 = lerp( (temp_output_1_0_g11792).rgb , ( appendResult56_g11792 * pow( saferPower57_g11792 , max( ( break55_g11792.a * 2.0 ) , 0.01 ) ) ) , ( hsvTorgb33_g11792.z * _RecolorRGBYCPFade ));
				float4 appendResult30_g11792 = (float4(lerpResult26_g11792 , temp_output_1_0_g11792.a));
				float4 staticSwitch9_g11762 = appendResult30_g11792;
				#else
				float4 staticSwitch9_g11762 = staticSwitch43_g11762;
				#endif
				#ifdef _ENABLECOLORREPLACE_ON
				float4 temp_output_1_0_g11766 = staticSwitch9_g11762;
				float3 temp_output_2_0_g11766 = (temp_output_1_0_g11766).rgb;
				float3 In115_g11766 = temp_output_2_0_g11766;
				float3 From115_g11766 = (_ColorReplaceFromColor).rgb;
				float4 break2_g11767 = temp_output_1_0_g11766;
				float3 To115_g11766 = ( pow( ( ( break2_g11767.x + break2_g11767.x + break2_g11767.y + break2_g11767.y + break2_g11767.y + break2_g11767.z ) / 6.0 ) , max( _ColorReplaceContrast , 0.001 ) ) * (_ColorReplaceToColor).rgb );
				float Fuzziness115_g11766 = _ColorReplaceSmoothness;
				float Range115_g11766 = _ColorReplaceRange;
				float3 localMyCustomExpression115_g11766 = MyCustomExpression115_g11766( In115_g11766 , From115_g11766 , To115_g11766 , Fuzziness115_g11766 , Range115_g11766 );
				float3 lerpResult112_g11766 = lerp( temp_output_2_0_g11766 , localMyCustomExpression115_g11766 , _ColorReplaceFade);
				float4 appendResult4_g11766 = (float4(lerpResult112_g11766 , temp_output_1_0_g11766.a));
				float4 staticSwitch29_g11762 = appendResult4_g11766;
				#else
				float4 staticSwitch29_g11762 = staticSwitch9_g11762;
				#endif
				float4 temp_output_1_0_g11779 = staticSwitch29_g11762;
				#ifdef _ENABLENEGATIVE_ON
				float3 temp_output_9_0_g11779 = (temp_output_1_0_g11779).rgb;
				float3 lerpResult3_g11779 = lerp( temp_output_9_0_g11779 , ( 1.0 - temp_output_9_0_g11779 ) , _NegativeFade);
				float4 appendResult8_g11779 = (float4(lerpResult3_g11779 , temp_output_1_0_g11779.a));
				float4 staticSwitch4_g11779 = appendResult8_g11779;
				#else
				float4 staticSwitch4_g11779 = temp_output_1_0_g11779;
				#endif
				float4 temp_output_57_0_g11762 = staticSwitch4_g11779;
				#ifdef _ENABLECONTRAST_ON
				float4 temp_output_1_0_g11802 = temp_output_57_0_g11762;
				float3 temp_cast_30 = (max( _Contrast , 0.001 )).xxx;
				float4 appendResult4_g11802 = (float4(pow( (temp_output_1_0_g11802).rgb , temp_cast_30 ) , temp_output_1_0_g11802.a));
				float4 staticSwitch32_g11762 = appendResult4_g11802;
				#else
				float4 staticSwitch32_g11762 = temp_output_57_0_g11762;
				#endif
				#ifdef _ENABLEBRIGHTNESS_ON
				float4 temp_output_2_0_g11777 = staticSwitch32_g11762;
				float4 appendResult6_g11777 = (float4(( (temp_output_2_0_g11777).rgb * _Brightness ) , temp_output_2_0_g11777.a));
				float4 staticSwitch33_g11762 = appendResult6_g11777;
				#else
				float4 staticSwitch33_g11762 = staticSwitch32_g11762;
				#endif
				#ifdef _ENABLEHUE_ON
				float4 temp_output_2_0_g11778 = staticSwitch33_g11762;
				float3 hsvTorgb1_g11778 = RGBToHSV( temp_output_2_0_g11778.rgb );
				float3 hsvTorgb3_g11778 = HSVToRGB( float3(( hsvTorgb1_g11778.x + _Hue ),hsvTorgb1_g11778.y,hsvTorgb1_g11778.z) );
				float4 appendResult8_g11778 = (float4(hsvTorgb3_g11778 , temp_output_2_0_g11778.a));
				float4 staticSwitch36_g11762 = appendResult8_g11778;
				#else
				float4 staticSwitch36_g11762 = staticSwitch33_g11762;
				#endif
				#ifdef _ENABLESPLITTONING_ON
				float4 temp_output_1_0_g11796 = staticSwitch36_g11762;
				float4 break2_g11797 = temp_output_1_0_g11796;
				float temp_output_3_0_g11796 = ( ( break2_g11797.x + break2_g11797.x + break2_g11797.y + break2_g11797.y + break2_g11797.y + break2_g11797.z ) / 6.0 );
				float clampResult25_g11796 = clamp( ( ( ( ( temp_output_3_0_g11796 + _SplitToningShift ) - 0.5 ) * _SplitToningBalance ) + 0.5 ) , 0.0 , 1.0 );
				float3 lerpResult6_g11796 = lerp( (_SplitToningShadowsColor).rgb , (_SplitToningHighlightsColor).rgb , clampResult25_g11796);
				float3 lerpResult11_g11796 = lerp( (temp_output_1_0_g11796).rgb , ( lerpResult6_g11796 * pow( temp_output_3_0_g11796 , max( _SplitToningContrast , 0.001 ) ) ) , _SplitToningFade);
				float4 appendResult18_g11796 = (float4(lerpResult11_g11796 , temp_output_1_0_g11796.a));
				float4 staticSwitch30_g11762 = appendResult18_g11796;
				#else
				float4 staticSwitch30_g11762 = staticSwitch36_g11762;
				#endif
				#ifdef _ENABLEBLACKTINT_ON
				float4 temp_output_1_0_g11774 = staticSwitch30_g11762;
				float3 temp_output_4_0_g11774 = (temp_output_1_0_g11774).rgb;
				float4 break12_g11774 = temp_output_1_0_g11774;
				float3 lerpResult7_g11774 = lerp( temp_output_4_0_g11774 , ( temp_output_4_0_g11774 + (_BlackTintColor).rgb ) , pow( ( 1.0 - min( max( max( break12_g11774.r , break12_g11774.g ) , break12_g11774.b ) , 1.0 ) ) , max( _BlackTintPower , 0.001 ) ));
				float3 lerpResult13_g11774 = lerp( temp_output_4_0_g11774 , lerpResult7_g11774 , _BlackTintFade);
				float4 appendResult11_g11774 = (float4(lerpResult13_g11774 , break12_g11774.a));
				float4 staticSwitch20_g11762 = appendResult11_g11774;
				#else
				float4 staticSwitch20_g11762 = staticSwitch30_g11762;
				#endif
				#ifdef _ENABLEINKSPREAD_ON
				float4 temp_output_1_0_g11788 = staticSwitch20_g11762;
				float4 break2_g11790 = temp_output_1_0_g11788;
				float2 temp_output_65_0_g11788 = shaderPosition235;
				float linValue16_g11789 = tex2D( _UberNoiseTexture, ( temp_output_65_0_g11788 * _InkSpreadNoiseScale ) ).r;
				float localMyCustomExpression16_g11789 = MyCustomExpression16_g11789( linValue16_g11789 );
				float clampResult53_g11788 = clamp( ( ( ( _InkSpreadDistance - distance( _InkSpreadPosition , temp_output_65_0_g11788 ) ) + ( localMyCustomExpression16_g11789 * _InkSpreadNoiseFactor ) ) / max( _InkSpreadWidth , 0.001 ) ) , 0.0 , 1.0 );
				float3 lerpResult7_g11788 = lerp( (temp_output_1_0_g11788).rgb , ( (_InkSpreadColor).rgb * pow( ( ( break2_g11790.x + break2_g11790.x + break2_g11790.y + break2_g11790.y + break2_g11790.y + break2_g11790.z ) / 6.0 ) , max( _InkSpreadContrast , 0.001 ) ) ) , ( _InkSpreadFade * clampResult53_g11788 ));
				float4 appendResult9_g11788 = (float4(lerpResult7_g11788 , (temp_output_1_0_g11788).a));
				float4 staticSwitch17_g11762 = appendResult9_g11788;
				#else
				float4 staticSwitch17_g11762 = staticSwitch20_g11762;
				#endif
				float temp_output_39_0_g11762 = shaderTime237;
				#ifdef _ENABLESHIFTHUE_ON
				float4 temp_output_1_0_g11780 = staticSwitch17_g11762;
				float3 hsvTorgb15_g11780 = RGBToHSV( (temp_output_1_0_g11780).rgb );
				float3 hsvTorgb19_g11780 = HSVToRGB( float3(( ( temp_output_39_0_g11762 * _ShiftHueSpeed ) + hsvTorgb15_g11780.x ),hsvTorgb15_g11780.y,hsvTorgb15_g11780.z) );
				float4 appendResult6_g11780 = (float4(hsvTorgb19_g11780 , temp_output_1_0_g11780.a));
				float4 staticSwitch19_g11762 = appendResult6_g11780;
				#else
				float4 staticSwitch19_g11762 = staticSwitch17_g11762;
				#endif
				#ifdef _ENABLEADDHUE_ON
				float3 hsvTorgb19_g11784 = HSVToRGB( float3(( ( temp_output_39_0_g11762 * _AddHueSpeed ) % 1.0 ),_AddHueSaturation,_AddHueBrightness) );
				float4 temp_output_1_0_g11784 = staticSwitch19_g11762;
				float4 break2_g11786 = temp_output_1_0_g11784;
				float2 uv_AddHueMask = IN.texCoord0.xy * _AddHueMask_ST.xy + _AddHueMask_ST.zw;
				float4 tex2DNode3_g11785 = tex2D( _AddHueMask, uv_AddHueMask );
				#ifdef _ADDHUEMASKTOGGLE_ON
				float staticSwitch33_g11784 = ( _AddHueFade * ( tex2DNode3_g11785.r * tex2DNode3_g11785.a ) );
				#else
				float staticSwitch33_g11784 = _AddHueFade;
				#endif
				float4 appendResult6_g11784 = (float4(( ( hsvTorgb19_g11784 * pow( ( ( break2_g11786.x + break2_g11786.x + break2_g11786.y + break2_g11786.y + break2_g11786.y + break2_g11786.z ) / 6.0 ) , max( _AddHueContrast , 0.001 ) ) * staticSwitch33_g11784 ) + (temp_output_1_0_g11784).rgb ) , temp_output_1_0_g11784.a));
				float4 staticSwitch23_g11762 = appendResult6_g11784;
				#else
				float4 staticSwitch23_g11762 = staticSwitch19_g11762;
				#endif
				#ifdef _ENABLESINEGLOW_ON
				float4 temp_output_1_0_g11781 = staticSwitch23_g11762;
				float4 break2_g11782 = temp_output_1_0_g11781;
				float3 temp_output_13_0_g11781 = (_SineGlowColor).rgb;
				float2 uv_SineGlowMask = IN.texCoord0.xy * _SineGlowMask_ST.xy + _SineGlowMask_ST.zw;
				float4 tex2DNode30_g11781 = tex2D( _SineGlowMask, uv_SineGlowMask );
				#ifdef _SINEGLOWMASKTOGGLE_ON
				float3 staticSwitch27_g11781 = ( (tex2DNode30_g11781).rgb * temp_output_13_0_g11781 * tex2DNode30_g11781.a );
				#else
				float3 staticSwitch27_g11781 = temp_output_13_0_g11781;
				#endif
				float4 appendResult21_g11781 = (float4(( (temp_output_1_0_g11781).rgb + ( pow( ( ( break2_g11782.x + break2_g11782.x + break2_g11782.y + break2_g11782.y + break2_g11782.y + break2_g11782.z ) / 6.0 ) , max( _SineGlowContrast , 0.001 ) ) * staticSwitch27_g11781 * _SineGlowFade * ( ( ( sin( ( temp_output_39_0_g11762 * _SineGlowFrequency ) ) + 1.0 ) * ( _SineGlowMax - _SineGlowMin ) ) + _SineGlowMin ) ) ) , temp_output_1_0_g11781.a));
				float4 staticSwitch28_g11762 = appendResult21_g11781;
				#else
				float4 staticSwitch28_g11762 = staticSwitch23_g11762;
				#endif
				#ifdef _ENABLESATURATION_ON
				float4 temp_output_1_0_g11769 = staticSwitch28_g11762;
				float4 break2_g11770 = temp_output_1_0_g11769;
				float3 temp_cast_46 = (( ( break2_g11770.x + break2_g11770.x + break2_g11770.y + break2_g11770.y + break2_g11770.y + break2_g11770.z ) / 6.0 )).xxx;
				float3 lerpResult5_g11769 = lerp( temp_cast_46 , (temp_output_1_0_g11769).rgb , _Saturation);
				float4 appendResult8_g11769 = (float4(lerpResult5_g11769 , temp_output_1_0_g11769.a));
				float4 staticSwitch38_g11762 = appendResult8_g11769;
				#else
				float4 staticSwitch38_g11762 = staticSwitch28_g11762;
				#endif
				#ifdef _ENABLEINNEROUTLINE_ON
				float4 temp_output_15_0_g11771 = staticSwitch38_g11762;
				float3 temp_output_82_0_g11771 = (_InnerOutlineColor).rgb;
				float2 temp_output_7_0_g11771 = temp_output_1_0_g11762;
				float temp_output_179_0_g11771 = temp_output_39_0_g11762;
				#ifdef _INNEROUTLINETEXTURETOGGLE_ON
				float3 staticSwitch187_g11771 = ( (tex2D( _InnerOutlineTintTexture, ( temp_output_7_0_g11771 + ( _InnerOutlineTextureSpeed * temp_output_179_0_g11771 ) ) )).rgb * temp_output_82_0_g11771 );
				#else
				float3 staticSwitch187_g11771 = temp_output_82_0_g11771;
				#endif
				float linValue16_g11773 = tex2D( _UberNoiseTexture, ( ( ( temp_output_179_0_g11771 * _InnerOutlineNoiseSpeed ) + temp_output_7_0_g11771 ) * _InnerOutlineNoiseScale ) ).r;
				float localMyCustomExpression16_g11773 = MyCustomExpression16_g11773( linValue16_g11773 );
				#ifdef _INNEROUTLINEDISTORTIONTOGGLE_ON
				float2 staticSwitch169_g11771 = ( ( localMyCustomExpression16_g11773 - 0.5 ) * _InnerOutlineDistortionIntensity );
				#else
				float2 staticSwitch169_g11771 = float2( 0,0 );
				#endif
				float2 temp_output_131_0_g11771 = ( staticSwitch169_g11771 + temp_output_7_0_g11771 );
				float2 appendResult2_g11772 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_25_0_g11771 = ( 100.0 / appendResult2_g11772 );
				float temp_output_178_0_g11771 = ( _InnerOutlineFade * ( 1.0 - min( min( min( min( min( min( min( tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0,-1 ) ) * temp_output_25_0_g11771 ) ) ).a , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0,1 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -1,0 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 1,0 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0.705,0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -0.705,0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0.705,-0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -0.705,-0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) ) );
				float3 lerpResult176_g11771 = lerp( (temp_output_15_0_g11771).rgb , staticSwitch187_g11771 , temp_output_178_0_g11771);
				#ifdef _INNEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch188_g11771 = ( temp_output_178_0_g11771 * temp_output_15_0_g11771.a );
				#else
				float staticSwitch188_g11771 = temp_output_15_0_g11771.a;
				#endif
				float4 appendResult177_g11771 = (float4(lerpResult176_g11771 , staticSwitch188_g11771));
				float4 staticSwitch12_g11762 = appendResult177_g11771;
				#else
				float4 staticSwitch12_g11762 = staticSwitch38_g11762;
				#endif
				#ifdef _ENABLEOUTEROUTLINE_ON
				float4 temp_output_15_0_g11799 = staticSwitch12_g11762;
				float3 temp_output_82_0_g11799 = (_OuterOutlineColor).rgb;
				float2 temp_output_7_0_g11799 = temp_output_1_0_g11762;
				float temp_output_186_0_g11799 = temp_output_39_0_g11762;
				#ifdef _OUTEROUTLINETEXTURETOGGLE_ON
				float3 staticSwitch199_g11799 = ( (tex2D( _OuterOutlineTintTexture, ( temp_output_7_0_g11799 + ( _OuterOutlineTextureSpeed * temp_output_186_0_g11799 ) ) )).rgb * temp_output_82_0_g11799 );
				#else
				float3 staticSwitch199_g11799 = temp_output_82_0_g11799;
				#endif
				float temp_output_182_0_g11799 = ( ( 1.0 - temp_output_15_0_g11799.a ) * min( ( _OuterOutlineFade * 3.0 ) , 1.0 ) );
				#ifdef _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch203_g11799 = 1.0;
				#else
				float staticSwitch203_g11799 = temp_output_182_0_g11799;
				#endif
				float3 lerpResult178_g11799 = lerp( (temp_output_15_0_g11799).rgb , staticSwitch199_g11799 , staticSwitch203_g11799);
				float3 lerpResult170_g11799 = lerp( lerpResult178_g11799 , staticSwitch199_g11799 , staticSwitch203_g11799);
				float linValue16_g11800 = tex2D( _UberNoiseTexture, ( ( ( temp_output_186_0_g11799 * _OuterOutlineNoiseSpeed ) + temp_output_7_0_g11799 ) * _OuterOutlineNoiseScale ) ).r;
				float localMyCustomExpression16_g11800 = MyCustomExpression16_g11800( linValue16_g11800 );
				#ifdef _OUTEROUTLINEDISTORTIONTOGGLE_ON
				float2 staticSwitch157_g11799 = ( ( localMyCustomExpression16_g11800 - 0.5 ) * _OuterOutlineDistortionIntensity );
				#else
				float2 staticSwitch157_g11799 = float2( 0,0 );
				#endif
				float2 temp_output_131_0_g11799 = ( staticSwitch157_g11799 + temp_output_7_0_g11799 );
				float2 appendResult2_g11801 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_25_0_g11799 = ( 100.0 / appendResult2_g11801 );
				float lerpResult168_g11799 = lerp( temp_output_15_0_g11799.a , min( ( max( max( max( max( max( max( max( tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0,-1 ) ) * temp_output_25_0_g11799 ) ) ).a , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0,1 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -1,0 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 1,0 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0.705,0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -0.705,0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0.705,-0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -0.705,-0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) * 3.0 ) , 1.0 ) , _OuterOutlineFade);
				#ifdef _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch200_g11799 = ( temp_output_182_0_g11799 * lerpResult168_g11799 );
				#else
				float staticSwitch200_g11799 = lerpResult168_g11799;
				#endif
				float4 appendResult174_g11799 = (float4(lerpResult170_g11799 , staticSwitch200_g11799));
				float4 staticSwitch13_g11762 = appendResult174_g11799;
				#else
				float4 staticSwitch13_g11762 = staticSwitch12_g11762;
				#endif
				#ifdef _ENABLEPIXELOUTLINE_ON
				float4 temp_output_15_0_g11776 = staticSwitch13_g11762;
				float3 temp_output_82_0_g11776 = (_PixelOutlineColor).rgb;
				float2 temp_output_7_0_g11776 = temp_output_1_0_g11762;
				#ifdef _PIXELOUTLINETEXTURETOGGLE_ON
				float3 staticSwitch199_g11776 = ( (tex2D( _PixelOutlineTintTexture, ( temp_output_7_0_g11776 + ( _PixelOutlineTextureSpeed * temp_output_39_0_g11762 ) ) )).rgb * temp_output_82_0_g11776 );
				#else
				float3 staticSwitch199_g11776 = temp_output_82_0_g11776;
				#endif
				float temp_output_182_0_g11776 = ( ( 1.0 - temp_output_15_0_g11776.a ) * min( ( _PixelOutlineFade * 3.0 ) , 1.0 ) );
				#ifdef _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch203_g11776 = 1.0;
				#else
				float staticSwitch203_g11776 = temp_output_182_0_g11776;
				#endif
				float3 lerpResult178_g11776 = lerp( (temp_output_15_0_g11776).rgb , staticSwitch199_g11776 , staticSwitch203_g11776);
				float3 lerpResult170_g11776 = lerp( lerpResult178_g11776 , staticSwitch199_g11776 , staticSwitch203_g11776);
				float2 appendResult206_g11776 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_209_0_g11776 = ( float2( 1,1 ) / appendResult206_g11776 );
				float lerpResult168_g11776 = lerp( temp_output_15_0_g11776.a , min( ( max( max( max( tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 0,-1 ) ) * temp_output_209_0_g11776 ) ) ).a , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 0,1 ) ) * temp_output_209_0_g11776 ) ) ).a ) , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( -1,0 ) ) * temp_output_209_0_g11776 ) ) ).a ) , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 1,0 ) ) * temp_output_209_0_g11776 ) ) ).a ) * 3.0 ) , 1.0 ) , _PixelOutlineFade);
				#ifdef _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch200_g11776 = ( temp_output_182_0_g11776 * lerpResult168_g11776 );
				#else
				float staticSwitch200_g11776 = lerpResult168_g11776;
				#endif
				float4 appendResult174_g11776 = (float4(lerpResult170_g11776 , staticSwitch200_g11776));
				float4 staticSwitch48_g11762 = appendResult174_g11776;
				#else
				float4 staticSwitch48_g11762 = staticSwitch13_g11762;
				#endif
				#ifdef _ENABLEPINGPONGGLOW_ON
				float3 lerpResult15_g11763 = lerp( (_PingPongGlowFrom).rgb , (_PingPongGlowTo).rgb , ( ( sin( ( temp_output_39_0_g11762 * _PingPongGlowFrequency ) ) + 1.0 ) / 2.0 ));
				float4 temp_output_5_0_g11763 = staticSwitch48_g11762;
				float4 break2_g11764 = temp_output_5_0_g11763;
				float4 appendResult12_g11763 = (float4(( ( lerpResult15_g11763 * _PingPongGlowFade * pow( ( ( break2_g11764.x + break2_g11764.x + break2_g11764.y + break2_g11764.y + break2_g11764.y + break2_g11764.z ) / 6.0 ) , max( _PingPongGlowContrast , 0.001 ) ) ) + (temp_output_5_0_g11763).rgb ) , temp_output_5_0_g11763.a));
				float4 staticSwitch46_g11762 = appendResult12_g11763;
				#else
				float4 staticSwitch46_g11762 = staticSwitch48_g11762;
				#endif
				float4 temp_output_361_0 = staticSwitch46_g11762;
				#ifdef _ENABLEHOLOGRAM_ON
				float4 temp_output_1_0_g11804 = temp_output_361_0;
				float4 break2_g11805 = temp_output_1_0_g11804;
				float4 appendResult22_g11804 = (float4(( (_HologramTint).rgb * pow( ( ( break2_g11805.x + break2_g11805.x + break2_g11805.y + break2_g11805.y + break2_g11805.y + break2_g11805.z ) / 6.0 ) , max( _HologramContrast , 0.001 ) ) ) , ( max( pow( abs( sin( ( ( ( ( shaderTime237 * _HologramLineSpeed ) + positionWS.y ) / unity_OrthoParams.y ) * _HologramLineFrequency ) ) ) , _HologramLineGap ) , _HologramMinAlpha ) * temp_output_1_0_g11804.a )));
				float4 lerpResult37_g11804 = lerp( temp_output_1_0_g11804 , appendResult22_g11804 , hologramFade182);
				float4 staticSwitch56 = lerpResult37_g11804;
				#else
				float4 staticSwitch56 = temp_output_361_0;
				#endif
				#ifdef _ENABLEGLITCH_ON
				float4 temp_output_1_0_g11807 = staticSwitch56;
				float4 break2_g11809 = temp_output_1_0_g11807;
				float temp_output_34_0_g11807 = shaderTime237;
				float linValue16_g11808 = tex2D( _UberNoiseTexture, ( ( glitchPosition154 + ( _GlitchNoiseSpeed * temp_output_34_0_g11807 ) ) * _GlitchNoiseScale ) ).r;
				float localMyCustomExpression16_g11808 = MyCustomExpression16_g11808( linValue16_g11808 );
				float3 hsvTorgb3_g11810 = HSVToRGB( float3(( localMyCustomExpression16_g11808 + ( temp_output_34_0_g11807 * _GlitchHueSpeed ) ),1.0,1.0) );
				float3 lerpResult23_g11807 = lerp( (temp_output_1_0_g11807).rgb , ( ( ( break2_g11809.x + break2_g11809.x + break2_g11809.y + break2_g11809.y + break2_g11809.y + break2_g11809.z ) / 6.0 ) * _GlitchBrightness * hsvTorgb3_g11810 ) , glitchFade152);
				float4 appendResult27_g11807 = (float4(lerpResult23_g11807 , temp_output_1_0_g11807.a));
				float4 staticSwitch57 = appendResult27_g11807;
				#else
				float4 staticSwitch57 = staticSwitch56;
				#endif
				float4 temp_output_3_0_g11811 = staticSwitch57;
				float4 temp_output_1_0_g11841 = temp_output_3_0_g11811;
				float2 temp_output_41_0_g11811 = shaderPosition235;
				float2 temp_output_99_0_g11841 = temp_output_41_0_g11811;
				float temp_output_40_0_g11811 = shaderTime237;
				#ifdef _ENABLECAMOUFLAGE_ON
				float linValue16_g11846 = tex2D( _UberNoiseTexture, ( ( ( temp_output_40_0_g11811 * _CamouflageDistortionSpeed ) + temp_output_99_0_g11841 ) * _CamouflageDistortionScale ) ).r;
				float localMyCustomExpression16_g11846 = MyCustomExpression16_g11846( linValue16_g11846 );
				#ifdef _CAMOUFLAGEANIMATIONTOGGLE_ON
				float2 staticSwitch101_g11841 = ( ( ( localMyCustomExpression16_g11846 - 0.25 ) * _CamouflageDistortionIntensity ) + temp_output_99_0_g11841 );
				#else
				float2 staticSwitch101_g11841 = temp_output_99_0_g11841;
				#endif
				float linValue16_g11843 = tex2D( _UberNoiseTexture, ( staticSwitch101_g11841 * _CamouflageNoiseScaleA ) ).r;
				float localMyCustomExpression16_g11843 = MyCustomExpression16_g11843( linValue16_g11843 );
				float clampResult52_g11841 = clamp( ( ( _CamouflageDensityA - localMyCustomExpression16_g11843 ) / max( _CamouflageSmoothnessA , 0.005 ) ) , 0.0 , 1.0 );
				float4 lerpResult55_g11841 = lerp( _CamouflageBaseColor , ( _CamouflageColorA * clampResult52_g11841 ) , clampResult52_g11841);
				float linValue16_g11845 = tex2D( _UberNoiseTexture, ( ( staticSwitch101_g11841 + float2( 12.3,12.3 ) ) * _CamouflageNoiseScaleB ) ).r;
				float localMyCustomExpression16_g11845 = MyCustomExpression16_g11845( linValue16_g11845 );
				float clampResult65_g11841 = clamp( ( ( _CamouflageDensityB - localMyCustomExpression16_g11845 ) / max( _CamouflageSmoothnessB , 0.005 ) ) , 0.0 , 1.0 );
				float4 lerpResult68_g11841 = lerp( lerpResult55_g11841 , ( _CamouflageColorB * clampResult65_g11841 ) , clampResult65_g11841);
				float4 break2_g11844 = temp_output_1_0_g11841;
				float3 lerpResult4_g11841 = lerp( (temp_output_1_0_g11841).rgb , ( (lerpResult68_g11841).rgb * pow( ( ( break2_g11844.x + break2_g11844.x + break2_g11844.y + break2_g11844.y + break2_g11844.y + break2_g11844.z ) / 6.0 ) , max( _CamouflageContrast , 0.001 ) ) ) , _CamouflageFade);
				float4 appendResult7_g11841 = (float4(lerpResult4_g11841 , temp_output_1_0_g11841.a));
				float4 staticSwitch26_g11811 = appendResult7_g11841;
				#else
				float4 staticSwitch26_g11811 = temp_output_3_0_g11811;
				#endif
				#ifdef _ENABLEMETAL_ON
				float4 temp_output_1_0_g11834 = staticSwitch26_g11811;
				float temp_output_59_0_g11834 = temp_output_40_0_g11811;
				float2 temp_output_58_0_g11834 = temp_output_41_0_g11811;
				float linValue16_g11835 = tex2D( _UberNoiseTexture, ( ( ( temp_output_59_0_g11834 * _MetalNoiseDistortionSpeed ) + temp_output_58_0_g11834 ) * _MetalNoiseDistortionScale ) ).r;
				float localMyCustomExpression16_g11835 = MyCustomExpression16_g11835( linValue16_g11835 );
				float linValue16_g11837 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11835 - 0.25 ) * _MetalNoiseDistortion ) + ( ( temp_output_59_0_g11834 * _MetalNoiseSpeed ) + temp_output_58_0_g11834 ) ) * _MetalNoiseScale ) ).r;
				float localMyCustomExpression16_g11837 = MyCustomExpression16_g11837( linValue16_g11837 );
				float4 break2_g11836 = temp_output_1_0_g11834;
				float temp_output_5_0_g11834 = ( ( break2_g11836.x + break2_g11836.x + break2_g11836.y + break2_g11836.y + break2_g11836.y + break2_g11836.z ) / 6.0 );
				float2 uv_MetalMask = IN.texCoord0.xy * _MetalMask_ST.xy + _MetalMask_ST.zw;
				float4 tex2DNode3_g11839 = tex2D( _MetalMask, uv_MetalMask );
				#ifdef _METALMASKTOGGLE_ON
				float staticSwitch60_g11834 = ( _MetalFade * ( tex2DNode3_g11839.r * tex2DNode3_g11839.a ) );
				#else
				float staticSwitch60_g11834 = _MetalFade;
				#endif
				float4 lerpResult45_g11834 = lerp( temp_output_1_0_g11834 , ( ( max( ( ( _MetalHighlightDensity - localMyCustomExpression16_g11837 ) / max( _MetalHighlightDensity , 0.01 ) ) , 0.0 ) * _MetalHighlightColor * pow( temp_output_5_0_g11834 , max( _MetalHighlightContrast , 0.001 ) ) ) + ( pow( temp_output_5_0_g11834 , max( _MetalContrast , 0.001 ) ) * _MetalColor ) ) , staticSwitch60_g11834);
				float4 appendResult8_g11834 = (float4((lerpResult45_g11834).rgb , (temp_output_1_0_g11834).a));
				float4 staticSwitch28_g11811 = appendResult8_g11834;
				#else
				float4 staticSwitch28_g11811 = staticSwitch26_g11811;
				#endif
				#ifdef _ENABLEFROZEN_ON
				float4 temp_output_1_0_g11826 = staticSwitch28_g11811;
				float4 break2_g11827 = temp_output_1_0_g11826;
				float temp_output_7_0_g11826 = ( ( break2_g11827.x + break2_g11827.x + break2_g11827.y + break2_g11827.y + break2_g11827.y + break2_g11827.z ) / 6.0 );
				float2 temp_output_72_0_g11826 = temp_output_41_0_g11811;
				float linValue16_g11828 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11826 * _FrozenSnowScale ) ).r;
				float localMyCustomExpression16_g11828 = MyCustomExpression16_g11828( linValue16_g11828 );
				float temp_output_73_0_g11826 = temp_output_40_0_g11811;
				float linValue16_g11830 = tex2D( _UberNoiseTexture, ( ( ( temp_output_73_0_g11826 * _FrozenHighlightDistortionSpeed ) + temp_output_72_0_g11826 ) * _FrozenHighlightDistortionScale ) ).r;
				float localMyCustomExpression16_g11830 = MyCustomExpression16_g11830( linValue16_g11830 );
				float linValue16_g11831 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11830 - 0.25 ) * _FrozenHighlightDistortion ) + ( ( temp_output_73_0_g11826 * _FrozenHighlightSpeed ) + temp_output_72_0_g11826 ) ) * _FrozenHighlightScale ) ).r;
				float localMyCustomExpression16_g11831 = MyCustomExpression16_g11831( linValue16_g11831 );
				float3 lerpResult57_g11826 = lerp( (temp_output_1_0_g11826).rgb , ( ( pow( temp_output_7_0_g11826 , max( _FrozenContrast , 0.001 ) ) * (_FrozenTint).rgb ) + ( pow( temp_output_7_0_g11826 , max( _FrozenSnowContrast , 0.001 ) ) * ( (_FrozenSnowColor).rgb * max( ( _FrozenSnowDensity - localMyCustomExpression16_g11828 ) , 0.0 ) ) ) + (( max( ( ( _FrozenHighlightDensity - localMyCustomExpression16_g11831 ) / max( _FrozenHighlightDensity , 0.01 ) ) , 0.0 ) * _FrozenHighlightColor * pow( temp_output_7_0_g11826 , max( _FrozenHighlightContrast , 0.001 ) ) )).rgb ) , _FrozenFade);
				float4 appendResult26_g11826 = (float4(lerpResult57_g11826 , temp_output_1_0_g11826.a));
				float4 staticSwitch29_g11811 = appendResult26_g11826;
				#else
				float4 staticSwitch29_g11811 = staticSwitch28_g11811;
				#endif
				#ifdef _ENABLEBURN_ON
				float4 temp_output_1_0_g11820 = staticSwitch29_g11811;
				float3 temp_output_28_0_g11820 = (temp_output_1_0_g11820).rgb;
				float4 break2_g11824 = float4( temp_output_28_0_g11820 , 0.0 );
				float2 temp_output_72_0_g11820 = temp_output_41_0_g11811;
				float linValue16_g11823 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11820 * _BurnSwirlNoiseScale ) ).r;
				float localMyCustomExpression16_g11823 = MyCustomExpression16_g11823( linValue16_g11823 );
				float linValue16_g11821 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11823 - 0.5 ) * float2( 1,1 ) * _BurnSwirlFactor ) + temp_output_72_0_g11820 ) * _BurnInsideNoiseScale ) ).r;
				float localMyCustomExpression16_g11821 = MyCustomExpression16_g11821( linValue16_g11821 );
				float clampResult68_g11820 = clamp( ( _BurnInsideNoiseFactor - localMyCustomExpression16_g11821 ) , 0.0 , 1.0 );
				float linValue16_g11822 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11820 * _BurnEdgeNoiseScale ) ).r;
				float localMyCustomExpression16_g11822 = MyCustomExpression16_g11822( linValue16_g11822 );
				float temp_output_15_0_g11820 = ( ( ( _BurnRadius - distance( temp_output_72_0_g11820 , _BurnPosition ) ) + ( localMyCustomExpression16_g11822 * _BurnEdgeNoiseFactor ) ) / max( _BurnWidth , 0.01 ) );
				float clampResult18_g11820 = clamp( temp_output_15_0_g11820 , 0.0 , 1.0 );
				float3 lerpResult29_g11820 = lerp( temp_output_28_0_g11820 , ( pow( ( ( break2_g11824.x + break2_g11824.x + break2_g11824.y + break2_g11824.y + break2_g11824.y + break2_g11824.z ) / 6.0 ) , max( _BurnInsideContrast , 0.001 ) ) * ( ( (_BurnInsideNoiseColor).rgb * clampResult68_g11820 ) + (_BurnInsideColor).rgb ) ) , clampResult18_g11820);
				float3 lerpResult40_g11820 = lerp( temp_output_28_0_g11820 , ( lerpResult29_g11820 + ( ( step( temp_output_15_0_g11820 , 1.0 ) * step( 0.0 , temp_output_15_0_g11820 ) ) * (_BurnEdgeColor).rgb ) ) , _BurnFade);
				float4 appendResult43_g11820 = (float4(lerpResult40_g11820 , temp_output_1_0_g11820.a));
				float4 staticSwitch32_g11811 = appendResult43_g11820;
				#else
				float4 staticSwitch32_g11811 = staticSwitch29_g11811;
				#endif
				#ifdef _ENABLERAINBOW_ON
				float2 temp_output_42_0_g11816 = temp_output_41_0_g11811;
				float linValue16_g11817 = tex2D( _UberNoiseTexture, ( temp_output_42_0_g11816 * _RainbowNoiseScale ) ).r;
				float localMyCustomExpression16_g11817 = MyCustomExpression16_g11817( linValue16_g11817 );
				float3 hsvTorgb3_g11819 = HSVToRGB( float3(( ( ( distance( temp_output_42_0_g11816 , _RainbowCenter ) + ( localMyCustomExpression16_g11817 * _RainbowNoiseFactor ) ) * _RainbowDensity ) + ( _RainbowSpeed * temp_output_40_0_g11811 ) ),1.0,1.0) );
				float3 hsvTorgb36_g11816 = RGBToHSV( hsvTorgb3_g11819 );
				float3 hsvTorgb37_g11816 = HSVToRGB( float3(hsvTorgb36_g11816.x,_RainbowSaturation,( hsvTorgb36_g11816.z * _RainbowBrightness )) );
				float4 temp_output_1_0_g11816 = staticSwitch32_g11811;
				float4 break2_g11818 = temp_output_1_0_g11816;
				float saferPower24_g11816 = abs( ( ( break2_g11818.x + break2_g11818.x + break2_g11818.y + break2_g11818.y + break2_g11818.y + break2_g11818.z ) / 6.0 ) );
				float4 appendResult29_g11816 = (float4(( ( hsvTorgb37_g11816 * pow( saferPower24_g11816 , max( _RainbowContrast , 0.001 ) ) * _RainbowFade ) + (temp_output_1_0_g11816).rgb ) , temp_output_1_0_g11816.a));
				float4 staticSwitch34_g11811 = appendResult29_g11816;
				#else
				float4 staticSwitch34_g11811 = staticSwitch32_g11811;
				#endif
				#ifdef _ENABLESHINE_ON
				float4 temp_output_1_0_g11812 = staticSwitch34_g11811;
				float3 temp_output_57_0_g11812 = (temp_output_1_0_g11812).rgb;
				float4 break2_g11814 = temp_output_1_0_g11812;
				float3 temp_cast_69 = (( ( break2_g11814.x + break2_g11814.x + break2_g11814.y + break2_g11814.y + break2_g11814.y + break2_g11814.z ) / 6.0 )).xxx;
				float3 lerpResult92_g11812 = lerp( temp_cast_69 , temp_output_57_0_g11812 , _ShineSaturation);
				float3 temp_cast_70 = (max( max( _ShineContrast , 0.001 ) , 0.001 )).xxx;
				float3 rotatedValue69_g11812 = RotateAroundAxis( float3( 0,0,0 ), float3( ( _ShineFrequency * temp_output_41_0_g11811 ) ,  0.0 ), float3( 0,0,1 ), ( ( _ShineRotation / 180.0 ) * PI ) );
				float temp_output_103_0_g11812 = ( _ShineFrequency * _ShineWidth );
				float clampResult80_g11812 = clamp( ( ( ( sin( ( rotatedValue69_g11812.x - ( temp_output_40_0_g11811 * _ShineSpeed * _ShineFrequency ) ) ) - ( 1.0 - temp_output_103_0_g11812 ) ) / temp_output_103_0_g11812 ) * _ShineSmooth ) , 0.0 , 1.0 );
				float2 uv_ShineMask = IN.texCoord0.xy * _ShineMask_ST.xy + _ShineMask_ST.zw;
				float4 tex2DNode3_g11813 = tex2D( _ShineMask, uv_ShineMask );
				#ifdef _SHINEMASKTOGGLE_ON
				float staticSwitch98_g11812 = ( _ShineFade * ( tex2DNode3_g11813.r * tex2DNode3_g11813.a ) );
				#else
				float staticSwitch98_g11812 = _ShineFade;
				#endif
				float4 appendResult8_g11812 = (float4(( temp_output_57_0_g11812 + ( ( pow( lerpResult92_g11812 , temp_cast_70 ) * (_ShineColor).rgb ) * clampResult80_g11812 * staticSwitch98_g11812 ) ) , (temp_output_1_0_g11812).a));
				float4 staticSwitch36_g11811 = appendResult8_g11812;
				#else
				float4 staticSwitch36_g11811 = staticSwitch34_g11811;
				#endif
				#ifdef _ENABLEPOISON_ON
				float temp_output_41_0_g11847 = temp_output_40_0_g11811;
				float linValue16_g11849 = tex2D( _UberNoiseTexture, ( ( ( temp_output_41_0_g11847 * _PoisonNoiseSpeed ) + temp_output_41_0_g11811 ) * _PoisonNoiseScale ) ).r;
				float localMyCustomExpression16_g11849 = MyCustomExpression16_g11849( linValue16_g11849 );
				float3 temp_output_24_0_g11847 = (_PoisonColor).rgb;
				float4 temp_output_1_0_g11847 = staticSwitch36_g11811;
				float3 temp_output_28_0_g11847 = (temp_output_1_0_g11847).rgb;
				float4 break2_g11848 = float4( temp_output_28_0_g11847 , 0.0 );
				float3 lerpResult32_g11847 = lerp( temp_output_28_0_g11847 , ( temp_output_24_0_g11847 * ( ( break2_g11848.x + break2_g11848.x + break2_g11848.y + break2_g11848.y + break2_g11848.y + break2_g11848.z ) / 6.0 ) ) , ( _PoisonFade * _PoisonRecolorFactor ));
				float4 appendResult27_g11847 = (float4(( ( max( pow( abs( ( ( ( localMyCustomExpression16_g11849 + ( temp_output_41_0_g11847 * _PoisonShiftSpeed ) ) % 1.0 ) + -0.5 ) ) , max( _PoisonDensity , 0.001 ) ) , 0.0 ) * temp_output_24_0_g11847 * _PoisonFade * _PoisonNoiseBrightness ) + lerpResult32_g11847 ) , temp_output_1_0_g11847.a));
				float4 staticSwitch39_g11811 = appendResult27_g11847;
				#else
				float4 staticSwitch39_g11811 = staticSwitch36_g11811;
				#endif
				float4 temp_output_10_0_g11851 = staticSwitch39_g11811;
				#ifdef _ENABLEENCHANTED_ON
				float3 temp_output_12_0_g11851 = (temp_output_10_0_g11851).rgb;
				float2 temp_output_2_0_g11851 = temp_output_41_0_g11811;
				float temp_output_1_0_g11851 = temp_output_40_0_g11811;
				float2 temp_output_6_0_g11851 = ( temp_output_1_0_g11851 * _EnchantedSpeed );
				float linValue16_g11854 = tex2D( _UberNoiseTexture, ( ( ( temp_output_2_0_g11851 - ( ( temp_output_6_0_g11851 + float2( 1.234,5.6789 ) ) * float2( 0.95,1.05 ) ) ) * _EnchantedScale ) * float2( 1,1 ) ) ).r;
				float localMyCustomExpression16_g11854 = MyCustomExpression16_g11854( linValue16_g11854 );
				float linValue16_g11852 = tex2D( _UberNoiseTexture, ( ( ( temp_output_6_0_g11851 + temp_output_2_0_g11851 ) * _EnchantedScale ) * float2( 1,1 ) ) ).r;
				float localMyCustomExpression16_g11852 = MyCustomExpression16_g11852( linValue16_g11852 );
				float temp_output_36_0_g11851 = ( localMyCustomExpression16_g11854 + localMyCustomExpression16_g11852 );
				float temp_output_43_0_g11851 = ( temp_output_36_0_g11851 * 0.5 );
				float3 lerpResult42_g11851 = lerp( (_EnchantedLowColor).rgb , (_EnchantedHighColor).rgb , temp_output_43_0_g11851);
				float3 hsvTorgb53_g11851 = HSVToRGB( float3(( ( temp_output_43_0_g11851 * _EnchantedRainbowDensity ) + ( _EnchantedRainbowSpeed * temp_output_1_0_g11851 ) ),_EnchantedRainbowSaturation,1.0) );
				#ifdef _ENCHANTEDRAINBOWTOGGLE_ON
				float3 staticSwitch50_g11851 = hsvTorgb53_g11851;
				#else
				float3 staticSwitch50_g11851 = lerpResult42_g11851;
				#endif
				float4 break2_g11853 = temp_output_10_0_g11851;
				float3 temp_output_40_0_g11851 = ( staticSwitch50_g11851 * pow( ( ( break2_g11853.x + break2_g11853.x + break2_g11853.y + break2_g11853.y + break2_g11853.y + break2_g11853.z ) / 6.0 ) , max( _EnchantedContrast , 0.001 ) ) * _EnchantedBrightness );
				float temp_output_45_0_g11851 = ( max( ( temp_output_36_0_g11851 - _EnchantedReduce ) , 0.0 ) * _EnchantedFade );
				float3 lerpResult44_g11851 = lerp( temp_output_12_0_g11851 , temp_output_40_0_g11851 , temp_output_45_0_g11851);
				#ifdef _ENCHANTEDLERPTOGGLE_ON
				float3 staticSwitch47_g11851 = lerpResult44_g11851;
				#else
				float3 staticSwitch47_g11851 = ( temp_output_12_0_g11851 + ( temp_output_40_0_g11851 * temp_output_45_0_g11851 ) );
				#endif
				float4 appendResult19_g11851 = (float4(staticSwitch47_g11851 , temp_output_10_0_g11851.a));
				float4 staticSwitch11_g11851 = appendResult19_g11851;
				#else
				float4 staticSwitch11_g11851 = temp_output_10_0_g11851;
				#endif
				float4 temp_output_1_0_g11856 = staticSwitch11_g11851;
				#ifdef _ENABLESHIFTING_ON
				float4 break5_g11856 = temp_output_1_0_g11856;
				float3 appendResult32_g11856 = (float3(break5_g11856.r , break5_g11856.g , break5_g11856.b));
				float4 break2_g11857 = temp_output_1_0_g11856;
				float temp_output_4_0_g11856 = ( ( break2_g11857.x + break2_g11857.x + break2_g11857.y + break2_g11857.y + break2_g11857.y + break2_g11857.z ) / 6.0 );
				float temp_output_11_0_g11856 = ( ( ( temp_output_4_0_g11856 + ( temp_output_40_0_g11811 * _ShiftingSpeed ) ) * _ShiftingDensity ) % 1.0 );
				float3 lerpResult20_g11856 = lerp( (_ShiftingColorA).rgb , (_ShiftingColorB).rgb , ( abs( ( temp_output_11_0_g11856 - 0.5 ) ) * 2.0 ));
				float3 hsvTorgb12_g11856 = HSVToRGB( float3(temp_output_11_0_g11856,_ShiftingSaturation,_ShiftingBrightness) );
				#ifdef _SHIFTINGRAINBOWTOGGLE_ON
				float3 staticSwitch26_g11856 = hsvTorgb12_g11856;
				#else
				float3 staticSwitch26_g11856 = ( lerpResult20_g11856 * _ShiftingBrightness );
				#endif
				float3 lerpResult31_g11856 = lerp( appendResult32_g11856 , ( staticSwitch26_g11856 * pow( temp_output_4_0_g11856 , max( _ShiftingContrast , 0.001 ) ) ) , _ShiftingFade);
				float4 appendResult6_g11856 = (float4(lerpResult31_g11856 , break5_g11856.a));
				float4 staticSwitch33_g11856 = appendResult6_g11856;
				#else
				float4 staticSwitch33_g11856 = temp_output_1_0_g11856;
				#endif
				float4 temp_output_5_0_g11863 = staticSwitch33_g11856;
				#ifdef _ENABLETEXTURELAYER1_ON
				float4 break6_g11863 = temp_output_5_0_g11863;
				float3 appendResult11_g11863 = (float3(break6_g11863.r , break6_g11863.g , break6_g11863.b));
				float temp_output_27_0_g11863 = temp_output_40_0_g11811;
				#ifdef _TEXTURELAYER1SCROLLTOGGLE_ON
				float2 staticSwitch73_g11863 = ( _TextureLayer1Offset + ( _TextureLayer1ScrollSpeed * temp_output_27_0_g11863 ) );
				#else
				float2 staticSwitch73_g11863 = _TextureLayer1Offset;
				#endif
				float2 temp_output_72_0_g11863 = ( ( _TextureLayer1Scale * temp_output_41_0_g11811 ) - staticSwitch73_g11863 );
				float2 temp_cast_79 = (_TextureLayer1EdgeClip).xx;
				float2 UV10_g11864 = ( ( ( ( ( ( ( ( ( temp_output_72_0_g11863 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) ) - float2( 0.5,0.5 ) ) * ( float2( 1,1 ) - temp_cast_79 ) ) + float2( 0.5,0.5 ) ) % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				int Width10_g11864 = _TextureLayer1Columns;
				int Height10_g11864 = _TextureLayer1Rows;
				float Tile10_g11864 = round( ( ( _TextureLayer1Speed * temp_output_27_0_g11863 ) + _TextureLayer1StartFrame ) );
				float2 Invert10_g11864 = float2( 0,1 );
				float2 localMyCustomExpression10_g11864 = MyCustomExpression10_g11864( UV10_g11864 , Width10_g11864 , Height10_g11864 , Tile10_g11864 , Invert10_g11864 );
				#ifdef _TEXTURELAYER1SHEETTOGGLE_ON
				float2 staticSwitch20_g11863 = localMyCustomExpression10_g11864;
				#else
				float2 staticSwitch20_g11863 = temp_output_72_0_g11863;
				#endif
				float4 tex2DNode3_g11863 = tex2D( _TextureLayer1Texture, staticSwitch20_g11863 );
				float3 appendResult13_g11863 = (float3(tex2DNode3_g11863.r , tex2DNode3_g11863.g , tex2DNode3_g11863.b));
				float3 appendResult18_g11863 = (float3(_TextureLayer1Color.r , _TextureLayer1Color.g , _TextureLayer1Color.b));
				float3 temp_output_16_0_g11863 = ( appendResult13_g11863 * appendResult18_g11863 );
				float4 break2_g11865 = temp_output_5_0_g11863;
				#ifdef _TEXTURELAYER1CONTRASTTOGGLE_ON
				float3 staticSwitch80_g11863 = ( pow( ( ( break2_g11865.x + break2_g11865.x + break2_g11865.y + break2_g11865.y + break2_g11865.y + break2_g11865.z ) / 6.0 ) , max( _TextureLayer1Contrast , 0.001 ) ) * temp_output_16_0_g11863 );
				#else
				float3 staticSwitch80_g11863 = temp_output_16_0_g11863;
				#endif
				float3 lerpResult12_g11863 = lerp( appendResult11_g11863 , staticSwitch80_g11863 , ( tex2DNode3_g11863.a * _TextureLayer1Fade ));
				float4 appendResult14_g11863 = (float4(lerpResult12_g11863 , break6_g11863.a));
				float4 staticSwitch15_g11863 = appendResult14_g11863;
				#else
				float4 staticSwitch15_g11863 = temp_output_5_0_g11863;
				#endif
				float4 temp_output_5_0_g11859 = staticSwitch15_g11863;
				#ifdef _ENABLETEXTURELAYER2_ON
				float4 break6_g11859 = temp_output_5_0_g11859;
				float3 appendResult11_g11859 = (float3(break6_g11859.r , break6_g11859.g , break6_g11859.b));
				float temp_output_27_0_g11859 = temp_output_40_0_g11811;
				#ifdef _TEXTURELAYER2SCROLLTOGGLE_ON
				float2 staticSwitch73_g11859 = ( _TextureLayer2Offset + ( _TextureLayer2ScrollSpeed * temp_output_27_0_g11859 ) );
				#else
				float2 staticSwitch73_g11859 = _TextureLayer2Offset;
				#endif
				float2 temp_output_72_0_g11859 = ( ( _TextureLayer2Scale * temp_output_41_0_g11811 ) - staticSwitch73_g11859 );
				float2 temp_cast_82 = (_TextureLayer2EdgeClip).xx;
				float2 UV10_g11860 = ( ( ( ( ( ( ( ( ( temp_output_72_0_g11859 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) ) - float2( 0.5,0.5 ) ) * ( float2( 1,1 ) - temp_cast_82 ) ) + float2( 0.5,0.5 ) ) % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				int Width10_g11860 = _TextureLayer2Columns;
				int Height10_g11860 = _TextureLayer2Rows;
				float Tile10_g11860 = round( ( ( _TextureLayer2Speed * temp_output_27_0_g11859 ) + _TextureLayer2StartFrame ) );
				float2 Invert10_g11860 = float2( 0,1 );
				float2 localMyCustomExpression10_g11860 = MyCustomExpression10_g11860( UV10_g11860 , Width10_g11860 , Height10_g11860 , Tile10_g11860 , Invert10_g11860 );
				#ifdef _TEXTURELAYER2SHEETTOGGLE_ON
				float2 staticSwitch20_g11859 = localMyCustomExpression10_g11860;
				#else
				float2 staticSwitch20_g11859 = temp_output_72_0_g11859;
				#endif
				float4 tex2DNode3_g11859 = tex2D( _TextureLayer2Texture, staticSwitch20_g11859 );
				float3 appendResult13_g11859 = (float3(tex2DNode3_g11859.r , tex2DNode3_g11859.g , tex2DNode3_g11859.b));
				float3 appendResult18_g11859 = (float3(_TextureLayer2Color.r , _TextureLayer2Color.g , _TextureLayer2Color.b));
				float3 temp_output_16_0_g11859 = ( appendResult13_g11859 * appendResult18_g11859 );
				float4 break2_g11861 = temp_output_5_0_g11859;
				#ifdef _TEXTURELAYER2CONTRASTTOGGLE_ON
				float3 staticSwitch84_g11859 = ( pow( ( ( break2_g11861.x + break2_g11861.x + break2_g11861.y + break2_g11861.y + break2_g11861.y + break2_g11861.z ) / 6.0 ) , max( _TextureLayer2Contrast , 0.001 ) ) * temp_output_16_0_g11859 );
				#else
				float3 staticSwitch84_g11859 = temp_output_16_0_g11859;
				#endif
				float3 lerpResult12_g11859 = lerp( appendResult11_g11859 , staticSwitch84_g11859 , ( tex2DNode3_g11859.a * _TextureLayer2Fade ));
				float4 appendResult14_g11859 = (float4(lerpResult12_g11859 , break6_g11859.a));
				float4 staticSwitch15_g11859 = appendResult14_g11859;
				#else
				float4 staticSwitch15_g11859 = temp_output_5_0_g11859;
				#endif
				float4 temp_output_473_0 = staticSwitch15_g11859;
				#ifdef _ENABLEFULLDISTORTION_ON
				float4 break4_g11867 = temp_output_473_0;
				float fullDistortionAlpha164 = _FullDistortionFade;
				float4 appendResult5_g11867 = (float4(break4_g11867.r , break4_g11867.g , break4_g11867.b , ( break4_g11867.a * fullDistortionAlpha164 )));
				float4 staticSwitch77 = appendResult5_g11867;
				#else
				float4 staticSwitch77 = temp_output_473_0;
				#endif
				#ifdef _ENABLEDIRECTIONALDISTORTION_ON
				float4 break4_g11868 = staticSwitch77;
				float directionalDistortionAlpha167 = (( _DirectionalDistortionInvert )?( ( 1.0 - clampResult154_g11669 ) ):( clampResult154_g11669 ));
				float4 appendResult5_g11868 = (float4(break4_g11868.r , break4_g11868.g , break4_g11868.b , ( break4_g11868.a * directionalDistortionAlpha167 )));
				float4 staticSwitch75 = appendResult5_g11868;
				#else
				float4 staticSwitch75 = staticSwitch77;
				#endif
				float4 temp_output_1_0_g11869 = staticSwitch75;
				float4 temp_output_1_0_g11870 = temp_output_1_0_g11869;
#ifdef _ENABLEFULLALPHADISSOLVE_ON
				float temp_output_53_0_g11870 = max( _FullAlphaDissolveWidth , 0.001 );
				#endif
				float2 temp_output_18_0_g11869 = shaderPosition235;
				#ifdef _ENABLEFULLALPHADISSOLVE_ON
				float linValue16_g11871 = tex2D( _UberNoiseTexture, ( temp_output_18_0_g11869 * _FullAlphaDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11871 = MyCustomExpression16_g11871( linValue16_g11871 );
				float clampResult17_g11870 = clamp( ( ( ( _FullAlphaDissolveFade * ( 1.0 + temp_output_53_0_g11870 ) ) - localMyCustomExpression16_g11871 ) / temp_output_53_0_g11870 ) , 0.0 , 1.0 );
				float4 appendResult3_g11870 = (float4((temp_output_1_0_g11870).rgb , ( temp_output_1_0_g11870.a * clampResult17_g11870 )));
				float4 staticSwitch3_g11869 = appendResult3_g11870;
				#else
				float4 staticSwitch3_g11869 = temp_output_1_0_g11869;
				#endif
				#ifdef _ENABLEFULLGLOWDISSOLVE_ON
				float linValue16_g11879 = tex2D( _UberNoiseTexture, ( temp_output_18_0_g11869 * _FullGlowDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11879 = MyCustomExpression16_g11879( linValue16_g11879 );
				float temp_output_5_0_g11878 = localMyCustomExpression16_g11879;
				float temp_output_61_0_g11878 = step( temp_output_5_0_g11878 , _FullGlowDissolveFade );
				float temp_output_53_0_g11878 = max( ( _FullGlowDissolveFade * _FullGlowDissolveWidth ) , 0.001 );
				float4 temp_output_1_0_g11878 = staticSwitch3_g11869;
				float4 appendResult3_g11878 = (float4(( ( (_FullGlowDissolveEdgeColor).rgb * ( temp_output_61_0_g11878 - step( temp_output_5_0_g11878 , ( ( _FullGlowDissolveFade * ( 1.01 + temp_output_53_0_g11878 ) ) - temp_output_53_0_g11878 ) ) ) ) + (temp_output_1_0_g11878).rgb ) , ( temp_output_1_0_g11878.a * temp_output_61_0_g11878 )));
				float4 staticSwitch5_g11869 = appendResult3_g11878;
				#else
				float4 staticSwitch5_g11869 = staticSwitch3_g11869;
				#endif
				#ifdef _ENABLESOURCEALPHADISSOLVE_ON
				float4 temp_output_1_0_g11880 = staticSwitch5_g11869;
				float2 temp_output_76_0_g11880 = temp_output_18_0_g11869;
				float linValue16_g11881 = tex2D( _UberNoiseTexture, ( temp_output_76_0_g11880 * _SourceAlphaDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11881 = MyCustomExpression16_g11881( linValue16_g11881 );
				float clampResult17_g11880 = clamp( ( ( _SourceAlphaDissolveFade - ( distance( _SourceAlphaDissolvePosition , temp_output_76_0_g11880 ) + ( localMyCustomExpression16_g11881 * _SourceAlphaDissolveNoiseFactor ) ) ) / max( _SourceAlphaDissolveWidth , 0.001 ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11880 = (float4((temp_output_1_0_g11880).rgb , ( temp_output_1_0_g11880.a * (( _SourceAlphaDissolveInvert )?( ( 1.0 - clampResult17_g11880 ) ):( clampResult17_g11880 )) )));
				float4 staticSwitch8_g11869 = appendResult3_g11880;
				#else
				float4 staticSwitch8_g11869 = staticSwitch5_g11869;
				#endif
				#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
				float2 temp_output_90_0_g11876 = temp_output_18_0_g11869;
				float linValue16_g11877 = tex2D( _UberNoiseTexture, ( temp_output_90_0_g11876 * _SourceGlowDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11877 = MyCustomExpression16_g11877( linValue16_g11877 );
				float temp_output_65_0_g11876 = ( distance( _SourceGlowDissolvePosition , temp_output_90_0_g11876 ) + ( localMyCustomExpression16_g11877 * _SourceGlowDissolveNoiseFactor ) );
				float temp_output_75_0_g11876 = step( temp_output_65_0_g11876 , _SourceGlowDissolveFade );
				float temp_output_76_0_g11876 = step( temp_output_65_0_g11876 , ( _SourceGlowDissolveFade - max( _SourceGlowDissolveWidth , 0.001 ) ) );
				float4 temp_output_1_0_g11876 = staticSwitch8_g11869;
				float4 appendResult3_g11876 = (float4(( ( max( ( temp_output_75_0_g11876 - temp_output_76_0_g11876 ) , 0.0 ) * (_SourceGlowDissolveEdgeColor).rgb ) + (temp_output_1_0_g11876).rgb ) , ( temp_output_1_0_g11876.a * (( _SourceGlowDissolveInvert )?( ( 1.0 - temp_output_76_0_g11876 ) ):( temp_output_75_0_g11876 )) )));
				float4 staticSwitch9_g11869 = appendResult3_g11876;
				#else
				float4 staticSwitch9_g11869 = staticSwitch8_g11869;
				#endif
				#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
				float4 temp_output_1_0_g11872 = staticSwitch9_g11869;
				float2 temp_output_161_0_g11872 = temp_output_18_0_g11869;
				float3 rotatedValue136_g11872 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_161_0_g11872 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalAlphaFadeRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11872 = rotatedValue136_g11872;
				float linValue16_g11873 = tex2D( _UberNoiseTexture, ( temp_output_161_0_g11872 * _DirectionalAlphaFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11873 = MyCustomExpression16_g11873( linValue16_g11873 );
				float clampResult154_g11872 = clamp( ( ( break130_g11872.x + break130_g11872.y + _DirectionalAlphaFadeFade + ( localMyCustomExpression16_g11873 * _DirectionalAlphaFadeNoiseFactor ) ) / max( _DirectionalAlphaFadeWidth , 0.001 ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11872 = (float4((temp_output_1_0_g11872).rgb , ( temp_output_1_0_g11872.a * (( _DirectionalAlphaFadeInvert )?( ( 1.0 - clampResult154_g11872 ) ):( clampResult154_g11872 )) )));
				float4 staticSwitch11_g11869 = appendResult3_g11872;
				#else
				float4 staticSwitch11_g11869 = staticSwitch9_g11869;
				#endif
				#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
				float2 temp_output_171_0_g11874 = temp_output_18_0_g11869;
				float3 rotatedValue136_g11874 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_171_0_g11874 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalGlowFadeRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11874 = rotatedValue136_g11874;
				float linValue16_g11875 = tex2D( _UberNoiseTexture, ( temp_output_171_0_g11874 * _DirectionalGlowFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11875 = MyCustomExpression16_g11875( linValue16_g11875 );
				float temp_output_168_0_g11874 = max( ( ( break130_g11874.x + break130_g11874.y + _DirectionalGlowFadeFade + ( localMyCustomExpression16_g11875 * _DirectionalGlowFadeNoiseFactor ) ) / max( _DirectionalGlowFadeWidth , 0.001 ) ) , 0.0 );
				float temp_output_161_0_g11874 = step( 0.1 , (( _DirectionalGlowFadeInvert )?( ( 1.0 - temp_output_168_0_g11874 ) ):( temp_output_168_0_g11874 )) );
				float4 temp_output_1_0_g11874 = staticSwitch11_g11869;
				float clampResult154_g11874 = clamp( temp_output_161_0_g11874 , 0.0 , 1.0 );
				float4 appendResult3_g11874 = (float4(( ( (_DirectionalGlowFadeEdgeColor).rgb * ( temp_output_161_0_g11874 - step( 1.0 , (( _DirectionalGlowFadeInvert )?( ( 1.0 - temp_output_168_0_g11874 ) ):( temp_output_168_0_g11874 )) ) ) ) + (temp_output_1_0_g11874).rgb ) , ( temp_output_1_0_g11874.a * clampResult154_g11874 )));
				float4 staticSwitch15_g11869 = appendResult3_g11874;
				#else
				float4 staticSwitch15_g11869 = staticSwitch11_g11869;
				#endif
				#ifdef _ENABLEHALFTONE_ON
				float4 temp_output_1_0_g11882 = staticSwitch15_g11869;
				float2 temp_output_126_0_g11882 = temp_output_18_0_g11869;
				float temp_output_121_0_g11882 = max( ( ( _HalftoneFade - distance( _HalftonePosition , temp_output_126_0_g11882 ) ) / max( 0.01 , _HalftoneFadeWidth ) ) , 0.0 );
				float2 appendResult11_g11883 = (float2(temp_output_121_0_g11882 , temp_output_121_0_g11882));
				float temp_output_17_0_g11883 = length( ( (( ( abs( temp_output_126_0_g11882 ) * _HalftoneTiling ) % float2( 1,1 ) )*2.0 + -1.0) / appendResult11_g11883 ) );
				float clampResult17_g11882 = clamp( saturate( ( ( 1.0 - temp_output_17_0_g11883 ) / fwidth( temp_output_17_0_g11883 ) ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11882 = (float4((temp_output_1_0_g11882).rgb , ( temp_output_1_0_g11882.a * (( _HalftoneInvert )?( ( 1.0 - clampResult17_g11882 ) ):( clampResult17_g11882 )) )));
				float4 staticSwitch13_g11869 = appendResult3_g11882;
				#else
				float4 staticSwitch13_g11869 = staticSwitch15_g11869;
				#endif
				#ifdef _ENABLEADDCOLOR_ON
				float3 temp_output_3_0_g11885 = (_AddColorColor).rgb;
				float2 uv_AddColorMask = IN.texCoord0.xy * _AddColorMask_ST.xy + _AddColorMask_ST.zw;
				float4 tex2DNode19_g11885 = tex2D( _AddColorMask, uv_AddColorMask );
				#ifdef _ADDCOLORMASKTOGGLE_ON
				float3 staticSwitch16_g11885 = ( temp_output_3_0_g11885 * ( (tex2DNode19_g11885).rgb * tex2DNode19_g11885.a ) );
				#else
				float3 staticSwitch16_g11885 = temp_output_3_0_g11885;
				#endif
				float4 temp_output_1_0_g11885 = staticSwitch13_g11869;
				float4 break2_g11887 = temp_output_1_0_g11885;
				#ifdef _ADDCOLORCONTRASTTOGGLE_ON
				float3 staticSwitch17_g11885 = ( staticSwitch16_g11885 * pow( ( ( break2_g11887.x + break2_g11887.x + break2_g11887.y + break2_g11887.y + break2_g11887.y + break2_g11887.z ) / 6.0 ) , max( _AddColorContrast , 0.001 ) ) );
				#else
				float3 staticSwitch17_g11885 = staticSwitch16_g11885;
				#endif
				float4 appendResult6_g11885 = (float4(( ( staticSwitch17_g11885 * _AddColorFade ) + (temp_output_1_0_g11885).rgb ) , temp_output_1_0_g11885.a));
				float4 staticSwitch5_g11884 = appendResult6_g11885;
				#else
				float4 staticSwitch5_g11884 = staticSwitch13_g11869;
				#endif
				#ifdef _ENABLEALPHATINT_ON
				float4 temp_output_1_0_g11888 = staticSwitch5_g11884;
				float3 lerpResult4_g11888 = lerp( (temp_output_1_0_g11888).rgb , (_AlphaTintColor).rgb , ( ( 1.0 - temp_output_1_0_g11888.a ) * step( _AlphaTintMinAlpha , temp_output_1_0_g11888.a ) * _AlphaTintFade ));
				float4 appendResult13_g11888 = (float4(lerpResult4_g11888 , temp_output_1_0_g11888.a));
				float4 staticSwitch11_g11884 = appendResult13_g11888;
				#else
				float4 staticSwitch11_g11884 = staticSwitch5_g11884;
				#endif
				#ifdef _ENABLESTRONGTINT_ON
				float4 temp_output_1_0_g11889 = staticSwitch11_g11884;
				float3 temp_output_6_0_g11889 = (_StrongTintTint).rgb;
				float2 uv_StrongTintMask = IN.texCoord0.xy * _StrongTintMask_ST.xy + _StrongTintMask_ST.zw;
				float4 tex2DNode23_g11889 = tex2D( _StrongTintMask, uv_StrongTintMask );
				#ifdef _STRONGTINTMASKTOGGLE_ON
				float3 staticSwitch21_g11889 = ( temp_output_6_0_g11889 * ( (tex2DNode23_g11889).rgb * tex2DNode23_g11889.a ) );
				#else
				float3 staticSwitch21_g11889 = temp_output_6_0_g11889;
				#endif
				float4 break2_g11891 = temp_output_1_0_g11889;
				#ifdef _STRONGTINTCONTRASTTOGGLE_ON
				float3 staticSwitch22_g11889 = ( pow( ( ( break2_g11891.x + break2_g11891.x + break2_g11891.y + break2_g11891.y + break2_g11891.y + break2_g11891.z ) / 6.0 ) , max( _StrongTintContrast , 0.001 ) ) * staticSwitch21_g11889 );
				#else
				float3 staticSwitch22_g11889 = staticSwitch21_g11889;
				#endif
				float3 lerpResult7_g11889 = lerp( (temp_output_1_0_g11889).rgb , staticSwitch22_g11889 , _StrongTintFade);
				float4 appendResult9_g11889 = (float4(lerpResult7_g11889 , (temp_output_1_0_g11889).a));
				float4 staticSwitch7_g11884 = appendResult9_g11889;
				#else
				float4 staticSwitch7_g11884 = staticSwitch11_g11884;
				#endif
				float4 temp_output_2_0_g11892 = staticSwitch7_g11884;
				#ifdef _ENABLESHADOW_ON
				float4 break4_g11894 = temp_output_2_0_g11892;
				float3 appendResult5_g11894 = (float3(break4_g11894.r , break4_g11894.g , break4_g11894.b));
				float2 appendResult2_g11893 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float4 appendResult85_g11892 = (float4(_ShadowColor.r , _ShadowColor.g , _ShadowColor.b , ( _ShadowFade * tex2D( _MainTex, ( finalUV146 - ( ( 100.0 / appendResult2_g11893 ) * _ShadowOffset ) ) ).a )));
				float4 break6_g11894 = appendResult85_g11892;
				float3 appendResult7_g11894 = (float3(break6_g11894.r , break6_g11894.g , break6_g11894.b));
				float temp_output_11_0_g11894 = ( ( 1.0 - break4_g11894.a ) * break6_g11894.a );
				float temp_output_32_0_g11894 = ( break4_g11894.a + temp_output_11_0_g11894 );
				float4 appendResult18_g11894 = (float4(( ( ( appendResult5_g11894 * break4_g11894.a ) + ( appendResult7_g11894 * temp_output_11_0_g11894 ) ) * ( 1.0 / max( temp_output_32_0_g11894 , 0.01 ) ) ) , temp_output_32_0_g11894));
				float4 staticSwitch82_g11892 = appendResult18_g11894;
				#else
				float4 staticSwitch82_g11892 = temp_output_2_0_g11892;
				#endif
				float4 break4_g11895 = staticSwitch82_g11892;
				#ifdef _ENABLECUSTOMFADE_ON
				float staticSwitch8_g11753 = 1.0;
				#else
				float staticSwitch8_g11753 = IN.ase_color.a;
				#endif
				#ifdef _ENABLESMOKE_ON
				float staticSwitch9_g11753 = 1.0;
				#else
				float staticSwitch9_g11753 = staticSwitch8_g11753;
				#endif
				float customVertexAlpha193 = staticSwitch9_g11753;
				float4 appendResult5_g11895 = (float4(break4_g11895.r , break4_g11895.g , break4_g11895.b , ( break4_g11895.a * customVertexAlpha193 )));
				float4 temp_output_344_0 = appendResult5_g11895;
				float4 temp_output_1_0_g11896 = temp_output_344_0;
				float4 appendResult8_g11896 = (float4(( (temp_output_1_0_g11896).rgb * (IN.ase_color).rgb ) , temp_output_1_0_g11896.a));
				#ifdef _VERTEXTINTFIRST_ON
				float4 staticSwitch342 = temp_output_344_0;
				#else
				float4 staticSwitch342 = appendResult8_g11896;
				#endif
				float4 lerpResult125 = lerp( ( originalColor191 * IN.ase_color ) , staticSwitch342 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float4 staticSwitch143 = staticSwitch342;
				#elif defined(_SHADERFADING_FULL)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_MASK)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_DISSOLVE)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_SPREAD)
				float4 staticSwitch143 = lerpResult125;
				#else
				float4 staticSwitch143 = staticSwitch342;
				#endif
				float4 temp_output_7_0_g11903 = staticSwitch143;
				#ifdef _BAKEDMATERIAL_ON
				float4 appendResult2_g11903 = (float4(( (temp_output_7_0_g11903).rgb / max( temp_output_7_0_g11903.a , 1E-05 ) ) , temp_output_7_0_g11903.a));
				float4 staticSwitch6_g11903 = appendResult2_g11903;
				#else
				float4 staticSwitch6_g11903 = temp_output_7_0_g11903;
				#endif
				
				float2 temp_output_8_0_g11904 = finalUV146;
				
				float3 unpack14_g11904 = UnpackNormalScale( tex2D( _NormalMap, temp_output_8_0_g11904 ), _NormalIntensity );
				unpack14_g11904.z = lerp( 1, unpack14_g11904.z, saturate(_NormalIntensity) );
				
				float4 Color = staticSwitch6_g11903;
				float4 Mask = tex2D( _MaskMap, temp_output_8_0_g11904 );
				float3 Normal = unpack14_g11904;

				#if ETC1_EXTERNAL_ALPHA
					float4 alpha = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, IN.texCoord0.xy);
					Color.a = lerp ( Color.a, alpha.r, _EnableAlphaTexture);
				#endif

				Color *= IN.color;

				SurfaceData2D surfaceData;
				InitializeSurfaceData(Color.rgb, Color.a, Mask, surfaceData);
				InputData2D inputData;
				InitializeInputData(IN.texCoord0.xy, half2(IN.screenPosition.xy / IN.screenPosition.w), inputData);
				SETUP_DEBUG_DATA_2D(inputData, positionWS);
				return CombinedShapeLightShared(surfaceData, inputData);
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "Sprite Normal"
			Tags { "LightMode"="NormalsRendering" }

			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZTest LEqual
			ZWrite Off
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM

			#define ASE_SRP_VERSION 140007


			#pragma vertex vert
			#pragma fragment frag

			#define _SURFACE_TYPE_TRANSPARENT 1
			#define SHADERPASS SHADERPASS_SPRITENORMAL

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature_local _SHADERFADING_NONE _SHADERFADING_FULL _SHADERFADING_MASK _SHADERFADING_DISSOLVE _SHADERFADING_SPREAD
			#pragma shader_feature_local _ENABLESINESCALE_ON
			#pragma shader_feature _ENABLEVIBRATE_ON
			#pragma shader_feature _ENABLESINEMOVE_ON
			#pragma shader_feature _ENABLESQUISH_ON
			#pragma shader_feature _SPRITESHEETFIX_ON
			#pragma shader_feature_local _PIXELPERFECTUV_ON
			#pragma shader_feature _ENABLEWORLDTILING_ON
			#pragma shader_feature _ENABLESCREENTILING_ON
			#pragma shader_feature _TOGGLETIMEFREQUENCY_ON
			#pragma shader_feature _TOGGLETIMEFPS_ON
			#pragma shader_feature _TOGGLETIMESPEED_ON
			#pragma shader_feature _TOGGLEUNSCALEDTIME_ON
			#pragma shader_feature _TOGGLECUSTOMTIME_ON
			#pragma shader_feature _SHADERSPACE_UV _SHADERSPACE_UV_RAW _SHADERSPACE_OBJECT _SHADERSPACE_OBJECT_SCALED _SHADERSPACE_WORLD _SHADERSPACE_UI_GRAPHIC _SHADERSPACE_SCREEN
			#pragma shader_feature _PIXELPERFECTSPACE_ON
			#pragma shader_feature _BAKEDMATERIAL_ON
			#pragma shader_feature _VERTEXTINTFIRST_ON
			#pragma shader_feature _ENABLESHADOW_ON
			#pragma shader_feature _ENABLESTRONGTINT_ON
			#pragma shader_feature _ENABLEALPHATINT_ON
			#pragma shader_feature_local _ENABLEADDCOLOR_ON
			#pragma shader_feature_local _ENABLEHALFTONE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALGLOWFADE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALALPHAFADE_ON
			#pragma shader_feature_local _ENABLESOURCEGLOWDISSOLVE_ON
			#pragma shader_feature_local _ENABLESOURCEALPHADISSOLVE_ON
			#pragma shader_feature_local _ENABLEFULLGLOWDISSOLVE_ON
			#pragma shader_feature_local _ENABLEFULLALPHADISSOLVE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALDISTORTION_ON
			#pragma shader_feature_local _ENABLEFULLDISTORTION_ON
			#pragma shader_feature _ENABLETEXTURELAYER2_ON
			#pragma shader_feature _ENABLETEXTURELAYER1_ON
			#pragma shader_feature _ENABLESHIFTING_ON
			#pragma shader_feature _ENABLEENCHANTED_ON
			#pragma shader_feature_local _ENABLEPOISON_ON
			#pragma shader_feature_local _ENABLESHINE_ON
			#pragma shader_feature_local _ENABLERAINBOW_ON
			#pragma shader_feature_local _ENABLEBURN_ON
			#pragma shader_feature_local _ENABLEFROZEN_ON
			#pragma shader_feature_local _ENABLEMETAL_ON
			#pragma shader_feature_local _ENABLECAMOUFLAGE_ON
			#pragma shader_feature_local _ENABLEGLITCH_ON
			#pragma shader_feature_local _ENABLEHOLOGRAM_ON
			#pragma shader_feature _ENABLEPINGPONGGLOW_ON
			#pragma shader_feature_local _ENABLEPIXELOUTLINE_ON
			#pragma shader_feature_local _ENABLEOUTEROUTLINE_ON
			#pragma shader_feature_local _ENABLEINNEROUTLINE_ON
			#pragma shader_feature_local _ENABLESATURATION_ON
			#pragma shader_feature_local _ENABLESINEGLOW_ON
			#pragma shader_feature_local _ENABLEADDHUE_ON
			#pragma shader_feature_local _ENABLESHIFTHUE_ON
			#pragma shader_feature_local _ENABLEINKSPREAD_ON
			#pragma shader_feature_local _ENABLEBLACKTINT_ON
			#pragma shader_feature_local _ENABLESPLITTONING_ON
			#pragma shader_feature_local _ENABLEHUE_ON
			#pragma shader_feature_local _ENABLEBRIGHTNESS_ON
			#pragma shader_feature_local _ENABLECONTRAST_ON
			#pragma shader_feature _ENABLENEGATIVE_ON
			#pragma shader_feature_local _ENABLECOLORREPLACE_ON
			#pragma shader_feature_local _ENABLERECOLORRGBYCP_ON
			#pragma shader_feature _ENABLERECOLORRGB_ON
			#pragma shader_feature_local _ENABLEFLAME_ON
			#pragma shader_feature_local _ENABLECHECKERBOARD_ON
			#pragma shader_feature_local _ENABLECUSTOMFADE_ON
			#pragma shader_feature_local _ENABLESMOKE_ON
			#pragma shader_feature _ENABLESHARPEN_ON
			#pragma shader_feature _ENABLEGAUSSIANBLUR_ON
			#pragma shader_feature _ENABLESMOOTHPIXELART_ON
			#pragma shader_feature_local _TILINGFIX_ON
			#pragma shader_feature _ENABLEWIGGLE_ON
			#pragma shader_feature_local _ENABLEUVSCALE_ON
			#pragma shader_feature_local _ENABLEPIXELATE_ON
			#pragma shader_feature_local _ENABLEUVSCROLL_ON
			#pragma shader_feature_local _ENABLEUVROTATE_ON
			#pragma shader_feature_local _ENABLESINEROTATE_ON
			#pragma shader_feature_local _ENABLESQUEEZE_ON
			#pragma shader_feature_local _ENABLEUVDISTORT_ON
			#pragma shader_feature_local _ENABLEWIND_ON
			#pragma shader_feature_local _WINDLOCALWIND_ON
			#pragma shader_feature_local _WINDHIGHQUALITYNOISE_ON
			#pragma shader_feature_local _WINDISPARALLAX_ON
			#pragma shader_feature _UVDISTORTMASKTOGGLE_ON
			#pragma shader_feature _WIGGLEFIXEDGROUNDTOGGLE_ON
			#pragma shader_feature _RECOLORRGBTEXTURETOGGLE_ON
			#pragma shader_feature _RECOLORRGBYCPTEXTURETOGGLE_ON
			#pragma shader_feature_local _ADDHUEMASKTOGGLE_ON
			#pragma shader_feature_local _SINEGLOWMASKTOGGLE_ON
			#pragma shader_feature _INNEROUTLINETEXTURETOGGLE_ON
			#pragma shader_feature_local _INNEROUTLINEDISTORTIONTOGGLE_ON
			#pragma shader_feature _INNEROUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature _OUTEROUTLINETEXTURETOGGLE_ON
			#pragma shader_feature _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature_local _OUTEROUTLINEDISTORTIONTOGGLE_ON
			#pragma shader_feature _PIXELOUTLINETEXTURETOGGLE_ON
			#pragma shader_feature _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature _CAMOUFLAGEANIMATIONTOGGLE_ON
			#pragma shader_feature _METALMASKTOGGLE_ON
			#pragma shader_feature _SHINEMASKTOGGLE_ON
			#pragma shader_feature _ENCHANTEDLERPTOGGLE_ON
			#pragma shader_feature _ENCHANTEDRAINBOWTOGGLE_ON
			#pragma shader_feature _SHIFTINGRAINBOWTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1CONTRASTTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1SHEETTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1SCROLLTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2CONTRASTTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2SHEETTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2SCROLLTOGGLE_ON
			#pragma shader_feature _ADDCOLORCONTRASTTOGGLE_ON
			#pragma shader_feature _ADDCOLORMASKTOGGLE_ON
			#pragma shader_feature _STRONGTINTCONTRASTTOGGLE_ON
			#pragma shader_feature _STRONGTINTMASKTOGGLE_ON


			sampler2D _MainTex;
			#ifdef _TOGGLEUNSCALEDTIME_ON
			float UnscaledTime;
			#endif
			#ifdef _SHADERFADING_MASK
			sampler2D _FadingMask;
			#endif
			sampler2D _UberNoiseTexture;
			#ifdef _ENABLEWIND_ON
			float WindMinIntensity;
			float WindMaxIntensity;
			float WindNoiseScale;
			float WindTime;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			sampler2D _UVDistortMask;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			sampler2D _CustomFadeFadeMask;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			sampler2D _RecolorRGBTexture;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			sampler2D _RecolorRGBYCPTexture;
			#endif
			#ifdef _ENABLEADDHUE_ON
			sampler2D _AddHueMask;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			sampler2D _SineGlowMask;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			sampler2D _InnerOutlineTintTexture;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			sampler2D _OuterOutlineTintTexture;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			sampler2D _PixelOutlineTintTexture;
			#endif
			#ifdef _ENABLEMETAL_ON
			sampler2D _MetalMask;
			#endif
			#ifdef _ENABLESHINE_ON
			sampler2D _ShineMask;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			sampler2D _TextureLayer1Texture;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			sampler2D _TextureLayer2Texture;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			sampler2D _AddColorMask;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			sampler2D _StrongTintMask;
			#endif
			sampler2D _NormalMap;
			CBUFFER_START( UnityPerMaterial )
			#ifdef _ENABLESHIFTING_ON
			float4 _ShiftingColorA;
			#endif
			#ifdef _ENABLEPOISON_ON
			float4 _PoisonColor;
			#endif
			#ifdef _ENABLESPLITTONING_ON
			float4 _SplitToningHighlightsColor;
			float4 _SplitToningShadowsColor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float4 _EnchantedLowColor;
			float4 _EnchantedHighColor;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float4 _ColorReplaceToColor;
			float4 _ColorReplaceFromColor;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			float4 _RecolorRGBYCPRedTint;
			float4 _RecolorRGBYCPYellowTint;
			float4 _RecolorRGBYCPGreenTint;
			float4 _RecolorRGBYCPCyanTint;
			float4 _RecolorRGBYCPBlueTint;
			float4 _RecolorRGBYCPPurpleTint;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			float4 _RecolorRGBBlueTint;
			float4 _RecolorRGBGreenTint;
			float4 _RecolorRGBRedTint;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float4 _ShiftingColorB;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float4 _TextureLayer1Color;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float4 _TextureLayer2Color;
			#endif
			#ifdef _ENABLEBLACKTINT_ON
			float4 _BlackTintColor;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float4 _InkSpreadColor;
			#endif
			#ifdef _ENABLESHINE_ON
			float4 _ShineMask_ST;
			float4 _ShineColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenTint;
			#endif
			#ifdef _ENABLEMETAL_ON
			float4 _MetalMask_ST;
			float4 _MetalColor;
			float4 _MetalHighlightColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenHighlightColor;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float4 _CamouflageColorB;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnInsideNoiseColor;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float4 _CamouflageColorA;
			float4 _CamouflageBaseColor;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float4 _UVDistortMask_ST;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnInsideColor;
			#endif
			#ifdef _ENABLEPINGPONGGLOW_ON
			float4 _PingPongGlowTo;
			float4 _PingPongGlowFrom;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnEdgeColor;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float4 _PixelOutlineColor;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float4 _OuterOutlineColor;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float4 _InnerOutlineColor;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			float4 _SineGlowMask_ST;
			float4 _SineGlowColor;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float4 _AddHueMask_ST;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float4 _HologramTint;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float4 _FullGlowDissolveEdgeColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenSnowColor;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float4 _StrongTintTint;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float4 _AddColorMask_ST;
			float4 _AddColorColor;
			#endif
			#ifdef _ENABLESHADOW_ON
			float4 _ShadowColor;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float4 _SourceGlowDissolveEdgeColor;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float4 _DirectionalGlowFadeEdgeColor;
			#endif
			#ifdef _SPRITESHEETFIX_ON
			float4 _SpriteSheetRect;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float4 _AlphaTintColor;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float4 _StrongTintMask_ST;
			#endif
			#ifdef _SHADERFADING_MASK
			float4 _FadingMask_ST;
			#endif
			float4 _MainTex_TexelSize;
			float2 _FadingNoiseScale;
			#ifdef _SHADERFADING_SPREAD
			float2 _FadingPosition;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnEdgeNoiseScale;
			float2 _BurnPosition;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float2 _DirectionalAlphaFadeNoiseScale;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnSwirlNoiseScale;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenSnowScale;
			#endif
			#ifdef _ENABLEFLAME_ON
			float2 _FlameNoiseScale;
			float2 _FlameSpeed;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float2 _GlitchNoiseSpeed;
			float2 _GlitchNoiseScale;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			float2 _CustomFadeNoiseScale;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float2 _TextureLayer1Offset;
			float2 _TextureLayer1ScrollSpeed;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageDistortionSpeed;
			float2 _CamouflageDistortionScale;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnInsideNoiseScale;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float2 _TextureLayer1Scale;
			#endif
			#ifdef _ENABLESHADOW_ON
			float2 _ShadowOffset;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageDistortionIntensity;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float2 _SineMoveFrequency;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float2 _InnerOutlineTextureSpeed;
			float2 _InnerOutlineNoiseSpeed;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float2 _InkSpreadNoiseScale;
			float2 _InkSpreadPosition;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float2 _InnerOutlineNoiseScale;
			float2 _InnerOutlineDistortionIntensity;
			#endif
			#ifdef _ENABLEPOISON_ON
			float2 _PoisonNoiseSpeed;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float2 _HalftonePosition;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float2 _DirectionalGlowFadeNoiseScale;
			#endif
			#ifdef _ENABLEPOISON_ON
			float2 _PoisonNoiseScale;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float2 _OuterOutlineTextureSpeed;
			#endif
			#ifdef _ENABLESINESCALE_ON
			float2 _SineScaleFactor;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float2 _RainbowCenter;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float2 _OuterOutlineNoiseSpeed;
			float2 _OuterOutlineNoiseScale;
			float2 _OuterOutlineDistortionIntensity;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float2 _EnchantedSpeed;
			float2 _EnchantedScale;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float2 _PixelOutlineTextureSpeed;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float2 _RainbowNoiseScale;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageNoiseScaleA;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float2 _SineMoveOffset;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float2 _WorldTilingOffset;
			#endif
			#ifdef _ENABLEFULLALPHADISSOLVE_ON
			float2 _FullAlphaDissolveNoiseScale;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float2 _UVDistortNoiseScale;
			float2 _UVDistortTo;
			float2 _UVDistortFrom;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float2 _FullGlowDissolveNoiseScale;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float2 _GlitchMaskScale;
			float2 _GlitchMaskSpeed;
			float2 _GlitchDistortion;
			float2 _GlitchDistortionScale;
			float2 _GlitchDistortionSpeed;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortion;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float2 _SqueezeCenter;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortionScale;
			#endif
			#ifdef _ENABLESOURCEALPHADISSOLVE_ON
			float2 _SourceAlphaDissolvePosition;
			float2 _SourceAlphaDissolveNoiseScale;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float2 _ScreenTilingOffset;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionNoiseScale;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float2 _SourceGlowDissolvePosition;
			float2 _SourceGlowDissolveNoiseScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionDistortion;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float2 _ScreenTilingScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionDistortionScale;
			#endif
			#ifdef _ENABLEFULLDISTORTION_ON
			float2 _FullDistortionDistortion;
			float2 _FullDistortionNoiseScale;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortionSpeed;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseScale;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float2 _UVDistortSpeed;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float2 _SqueezeScale;
			#endif
			#ifdef _ENABLEUVSCALE_ON
			float2 _UVScaleScale;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float2 _WorldTilingScale;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageNoiseScaleB;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightScale;
			#endif
			#ifdef _ENABLEUVSCROLL_ON
			float2 _UVScrollSpeed;
			#endif
			#ifdef _ENABLEUVROTATE_ON
			float2 _UVRotatePivot;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2ScrollSpeed;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightSpeed;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2Offset;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float2 _SineRotatePivot;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseDistortionSpeed;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2Scale;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseDistortionScale;
			float2 _MetalNoiseDistortion;
			float2 _MetalNoiseSpeed;
			#endif
			#ifdef _ENABLEUVSCALE_ON
			float2 _UVScalePivot;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenHighlightDensity;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowSpeed;
			float _RainbowSaturation;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenFade;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowNoiseFactor;
			float _RainbowContrast;
			float _RainbowBrightness;
			float _RainbowDensity;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float _AddColorFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnInsideNoiseFactor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenHighlightContrast;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnEdgeNoiseFactor;
			#endif
			#ifdef _ENABLESHADOW_ON
			float _ShadowFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnSwirlFactor;
			float _BurnFade;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float _StrongTintContrast;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnRadius;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float _AlphaTintMinAlpha;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnInsideContrast;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float _AlphaTintFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnWidth;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float _StrongTintFade;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveFade;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonRecolorFactor;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowFade;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingBrightness;
			float _ShiftingSaturation;
			float _ShiftingContrast;
			float _ShiftingFade;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1EdgeClip;
			#endif
			int _TextureLayer1Columns;
			int _TextureLayer1Rows;
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1Speed;
			#endif
			int _TextureLayer1StartFrame;
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeRotation;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1Contrast;
			float _TextureLayer1Fade;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2EdgeClip;
			#endif
			int _TextureLayer2Columns;
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingDensity;
			#endif
			int _TextureLayer2Rows;
			int _TextureLayer2StartFrame;
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeInvert;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2Contrast;
			float _TextureLayer2Fade;
			#endif
			#ifdef _ENABLEFULLALPHADISSOLVE_ON
			float _FullAlphaDissolveFade;
			float _FullAlphaDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveInvert;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float _FullGlowDissolveFade;
			float _FullGlowDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEALPHADISSOLVE_ON
			float _SourceAlphaDissolveInvert;
			float _SourceAlphaDissolveFade;
			float _SourceAlphaDissolveNoiseFactor;
			float _SourceAlphaDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveNoiseFactor;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2Speed;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float _AddColorContrast;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingSpeed;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedFade;
			#endif
			#ifdef _ENABLESHINE_ON
			float _ShineSaturation;
			float _ShineContrast;
			float _ShineRotation;
			float _ShineFrequency;
			float _ShineSpeed;
			float _ShineWidth;
			float _ShineSmooth;
			float _ShineFade;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneFadeWidth;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonShiftSpeed;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneFade;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonDensity;
			float _PoisonFade;
			float _PoisonNoiseBrightness;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeFade;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneTiling;
			float _HalftoneInvert;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeWidth;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedRainbowDensity;
			float _EnchantedRainbowSpeed;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeNoiseFactor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedRainbowSaturation;
			float _EnchantedContrast;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeFade;
			float _DirectionalGlowFadeRotation;
			float _DirectionalGlowFadeInvert;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedBrightness;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeWidth;
			float _DirectionalAlphaFadeNoiseFactor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedReduce;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveWidth;
			#endif
			#ifdef _ENABLESQUISH_ON
			float _SquishStretch;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float _PixelOutlineWidth;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenSnowContrast;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float _SqueezeFade;
			float _SqueezePower;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float _UVDistortFade;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float _GlitchFade;
			float _GlitchMaskMin;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramFade;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateFrequency;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramDistortionOffset;
			float _HologramDistortionDensity;
			float _HologramDistortionSpeed;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionWidth;
			float _DirectionalDistortionNoiseFactor;
			float _DirectionalDistortionFade;
			float _DirectionalDistortionRotation;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramDistortionScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionInvert;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateAngle;
			#endif
			#ifdef _ENABLEUVROTATE_ON
			float _UVRotateSpeed;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeNoiseScale;
			float _SmokeVertexSeed;
			#endif
			#ifdef _ENABLESHARPEN_ON
			float _SharpenFade;
			float _SharpenFactor;
			float _SharpenOffset;
			#endif
			#ifdef _ENABLEGAUSSIANBLUR_ON
			float _GaussianBlurFade;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateFade;
			#endif
			#ifdef _ENABLEGAUSSIANBLUR_ON
			float _GaussianBlurOffset;
			#endif
			#ifdef _ENABLEWIGGLE_ON
			float _WiggleOffset;
			float _WiggleSpeed;
			float _WiggleFrequency;
			#endif
			#ifdef _ENABLEPIXELATE_ON
			float _PixelateFade;
			float _PixelatePixelsPerUnit;
			float _PixelatePixelDensity;
			#endif
			#ifdef _ENABLEWIGGLE_ON
			float _WiggleFade;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionRandomDirection;
			#endif
			#ifdef _ENABLEFULLDISTORTION_ON
			float _FullDistortionFade;
			#endif
			#ifdef _ENABLEWIND_ON
			float _WindSquishWindFactor;
			#endif
			#ifdef _ENABLEVIBRATE_ON
			float _VibrateFade;
			float _VibrateOffset;
			float _VibrateFrequency;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float _SineMoveFade;
			#endif
			#ifdef _TOGGLETIMEFREQUENCY_ON
			float _TimeRange;
			float _TimeFrequency;
			#endif
			#ifdef _ENABLEVIBRATE_ON
			float _VibrateRotation;
			#endif
			#ifdef _TOGGLETIMEFPS_ON
			float _TimeFPS;
			#endif
			#ifdef _TOGGLECUSTOMTIME_ON
			float _TimeValue;
			#endif
			#ifdef _ENABLESQUISH_ON
			float _SquishSquish;
			float _SquishFlip;
			float _SquishFade;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float _WorldTilingPixelsPerUnit;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float _ScreenTilingPixelsPerUnit;
			#endif
			#ifdef _TOGGLETIMESPEED_ON
			float _TimeSpeed;
			#endif
			#ifdef _ENABLESINESCALE_ON
			float _SineScaleFrequency;
			#endif
			float _FadingFade;
			float _FadingWidth;
			#ifdef _ENABLEWIND_ON
			float _WindSquishFactor;
			float _WindFlip;
			float _WindMaxRotation;
			float _WindRotation;
			float _WindNoiseSpeed;
			float _WindNoiseScale;
			float _WindXPosition;
			float _WindMaxIntensity;
			float _WindMinIntensity;
			float _WindRotationWindFactor;
			#endif
			#ifdef _SHADERFADING_SPREAD
			float _FadingNoiseFactor;
			#endif
			#ifdef _SHADERSPACE_SCREEN
			float _ScreenWidthUnits;
			#endif
			#ifdef _SHADERSPACE_UI_GRAPHIC
			float _RectHeight;
			float _RectWidth;
			#endif
			float _PixelsPerUnit;
			#ifdef _ENABLESMOKE_ON
			float _SmokeNoiseFactor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenSnowDensity;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeSmoothness;
			float _SmokeAlpha;
			#endif
			#ifdef _ENABLEPINGPONGGLOW_ON
			float _PingPongGlowContrast;
			float _PingPongGlowFade;
			float _PingPongGlowFrequency;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float _PixelOutlineFade;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float _OuterOutlineWidth;
			float _OuterOutlineFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramContrast;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float _InnerOutlineWidth;
			#endif
			#ifdef _ENABLESATURATION_ON
			float _Saturation;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			float _SineGlowMin;
			float _SineGlowMax;
			float _SineGlowFrequency;
			float _SineGlowFade;
			float _SineGlowContrast;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float _InnerOutlineFade;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramLineSpeed;
			float _HologramLineGap;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenContrast;
			#endif
			#ifdef _ENABLEMETAL_ON
			float _MetalFade;
			float _MetalContrast;
			float _MetalHighlightContrast;
			float _MetalHighlightDensity;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramLineFrequency;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageContrast;
			float _CamouflageDensityB;
			float _CamouflageSmoothnessA;
			float _CamouflageDensityA;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float _GlitchHueSpeed;
			float _GlitchBrightness;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramMinAlpha;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageSmoothnessB;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueContrast;
			float _AddHueBrightness;
			float _AddHueSaturation;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceSmoothness;
			float _ColorReplaceContrast;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			float _RecolorRGBYCPFade;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			float _RecolorRGBFade;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameBrightness;
			float _FlameSmooth;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceRange;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameRadius;
			float _FlameNoiseHeightFactor;
			#endif
			#ifdef _ENABLECHECKERBOARD_ON
			float _CheckerboardTiling;
			float _CheckerboardDarken;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			float _CustomFadeAlpha;
			float _CustomFadeSmoothness;
			float _CustomFadeNoiseFactor;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameNoiseFactor;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceFade;
			#endif
			#ifdef _ENABLENEGATIVE_ON
			float _NegativeFade;
			#endif
			#ifdef _ENABLECONTRAST_ON
			float _Contrast;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueSpeed;
			#endif
			#ifdef _ENABLESHIFTHUE_ON
			float _ShiftHueSpeed;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float _InkSpreadWidth;
			float _InkSpreadNoiseFactor;
			float _InkSpreadDistance;
			float _InkSpreadFade;
			float _InkSpreadContrast;
			#endif
			#ifdef _ENABLEBLACKTINT_ON
			float _BlackTintFade;
			float _BlackTintPower;
			#endif
			#ifdef _ENABLESPLITTONING_ON
			float _SplitToningFade;
			float _SplitToningContrast;
			float _SplitToningBalance;
			float _SplitToningShift;
			#endif
			#ifdef _ENABLEHUE_ON
			float _Hue;
			#endif
			#ifdef _ENABLEBRIGHTNESS_ON
			float _Brightness;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeDarkEdge;
			#endif
			float _NormalIntensity;
			CBUFFER_END


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 uv0 : TEXCOORD0;
				float4 color : COLOR;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 texCoord0 : TEXCOORD0;
				float4 color : TEXCOORD1;
				float3 normalWS : TEXCOORD2;
				float4 tangentWS : TEXCOORD3;
				float3 bitangentWS : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				float4 ase_texcoord7 : TEXCOORD7;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float MyCustomExpression16_g11716( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11714( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float FastNoise101_g11665( float x )
			{
				float i = floor(x);
				float f = frac(x);
				float s = sign(frac(x/2.0)-0.5);
				    
				float k = 0.5+0.5*sin(i);
				return s*f*(f-1.0)*((16.0*k-4.0)*f*(f-1.0)-1.0);
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float MyCustomExpression16_g11667( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11668( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11671( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11670( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11676( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11677( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11718( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11673( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11729( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float4 texturePointSmooth( sampler2D tex, float4 textureTexelSize, float2 uvs )
			{
				float2 size;
				size.x = textureTexelSize.z;
				size.y = textureTexelSize.w;
				float2 pixel = float2(1.0,1.0) / size;
				uvs -= pixel * float2(0.5,0.5);
				float2 uv_pixels = uvs * size;
				float2 delta_pixel = frac(uv_pixels) - float2(0.5,0.5);
				float2 ddxy = fwidth(uv_pixels);
				float2 mip = log2(ddxy) - 0.5;
				float2 clampedUV = uvs + (clamp(delta_pixel / ddxy, 0.0, 1.0) - delta_pixel) * pixel;
				return tex2Dlod(tex, float4(clampedUV,0, min(mip.x, mip.y)));
			}
			
			float MyCustomExpression16_g11755( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11757( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11761( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float3 RGBToHSV(float3 c)
			{
				float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
				float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
				float d = q.x - min( q.w, q.y );
				float e = 1.0e-10;
				return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
			}
			float3 MyCustomExpression115_g11766( float3 In, float3 From, float3 To, float Fuzziness, float Range )
			{
				float Distance = distance(From, In);
				return lerp(To, In, saturate((Distance - Range) / max(Fuzziness, 0.001)));
			}
			
			float3 HSVToRGB( float3 c )
			{
				float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
				float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
				return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
			}
			
			float MyCustomExpression16_g11789( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11773( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11800( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11808( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11846( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11843( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11845( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11835( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11837( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11828( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11830( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11831( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11823( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11821( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11822( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11817( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11849( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11854( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11852( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float2 MyCustomExpression10_g11864( float2 UV, int Width, int Height, float Tile, float2 Invert )
			{
				Tile = fmod(Tile + 0.001, Width * Height);
				float2 tileCount = float2(1, 1) / float2(Width, Height);
				float tileY = abs(Invert.y * Height - (floor(Tile * tileCount.x) + Invert.y * 1));
				 float tileX = abs(Invert.x * Width - ((Tile - Width * floor(Tile * tileCount.x)) + Invert.x * 1));
				return (UV + float2(tileX, tileY)) * tileCount;
			}
			
			float2 MyCustomExpression10_g11860( float2 UV, int Width, int Height, float Tile, float2 Invert )
			{
				Tile = fmod(Tile + 0.001, Width * Height);
				float2 tileCount = float2(1, 1) / float2(Width, Height);
				float tileY = abs(Invert.y * Height - (floor(Tile * tileCount.x) + Invert.y * 1));
				 float tileX = abs(Invert.x * Width - ((Tile - Width * floor(Tile * tileCount.x)) + Invert.x * 1));
				return (UV + float2(tileX, tileY)) * tileCount;
			}
			
			float MyCustomExpression16_g11871( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11879( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11881( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11877( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11873( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11875( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			

			VertexOutput vert ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 _ZeroVector = float2(0,0);
				float2 texCoord363 = v.uv0.xy * float2( 1,1 ) + float2( 0,0 );
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				#ifdef _ENABLESCREENTILING_ON
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult16_g11656 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11656 = ( ( ( (( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / 10.0 ) )).xy * _ScreenTilingScale ) + _ScreenTilingOffset ) * ( _ScreenTilingPixelsPerUnit * appendResult16_g11656 ) );
				#else
				float2 staticSwitch2_g11656 = texCoord363;
				#endif
				float3 ase_worldPos = TransformObjectToWorld( (v.vertex).xyz );
				#ifdef _ENABLEWORLDTILING_ON
				float2 appendResult16_g11657 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11657 = ( ( ( (ase_worldPos).xy * _WorldTilingScale ) + _WorldTilingOffset ) * ( _WorldTilingPixelsPerUnit * appendResult16_g11657 ) );
				#else
				float2 staticSwitch2_g11657 = staticSwitch2_g11656;
				#endif
				float2 originalUV460 = staticSwitch2_g11657;
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11658 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch449 = ( floor( ( originalUV460 * appendResult7_g11658 ) ) / appendResult7_g11658 );
				#else
				float2 staticSwitch449 = originalUV460;
				#endif
				float2 uvAfterPixelArt450 = staticSwitch449;
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11662 = uvAfterPixelArt450;
				float2 appendResult374 = (float2(_SpriteSheetRect.x , _SpriteSheetRect.y));
				float2 spriteRectMin376 = appendResult374;
				float2 break11_g11662 = spriteRectMin376;
				float2 appendResult375 = (float2(_SpriteSheetRect.z , _SpriteSheetRect.w));
				float2 spriteRectMax377 = appendResult375;
				float2 break10_g11662 = spriteRectMax377;
				float2 break9_g11662 = float2( 0,0 );
				float2 break8_g11662 = float2( 1,1 );
				float2 appendResult15_g11662 = (float2((break9_g11662.x + (break14_g11662.x - break11_g11662.x) * (break8_g11662.x - break9_g11662.x) / (break10_g11662.x - break11_g11662.x)) , (break9_g11662.y + (break14_g11662.y - break11_g11662.y) * (break8_g11662.y - break9_g11662.y) / (break10_g11662.y - break11_g11662.y))));
				float2 staticSwitch366 = appendResult15_g11662;
				#else
				float2 staticSwitch366 = uvAfterPixelArt450;
				#endif
				float2 fixedUV475 = staticSwitch366;
				#ifdef _ENABLESQUISH_ON
				float2 break77_g11897 = fixedUV475;
				float2 appendResult72_g11897 = (float2(( _SquishStretch * ( break77_g11897.x - 0.5 ) * _SquishFade ) , ( _SquishFade * ( break77_g11897.y + _SquishFlip ) * -_SquishSquish )));
				float2 staticSwitch198 = ( appendResult72_g11897 + _ZeroVector );
				#else
				float2 staticSwitch198 = _ZeroVector;
				#endif
				float2 temp_output_2_0_g11899 = staticSwitch198;
				#ifdef _TOGGLECUSTOMTIME_ON
				float staticSwitch44_g11661 = _TimeValue;
				#else
				float staticSwitch44_g11661 = _TimeParameters.x;
				#endif
				#ifdef _TOGGLEUNSCALEDTIME_ON
				float staticSwitch34_g11661 = UnscaledTime;
				#else
				float staticSwitch34_g11661 = staticSwitch44_g11661;
				#endif
				#ifdef _TOGGLETIMESPEED_ON
				float staticSwitch37_g11661 = ( staticSwitch34_g11661 * _TimeSpeed );
				#else
				float staticSwitch37_g11661 = staticSwitch34_g11661;
				#endif
				#ifdef _TOGGLETIMEFPS_ON
				float staticSwitch38_g11661 = ( floor( ( staticSwitch37_g11661 * _TimeFPS ) ) / _TimeFPS );
				#else
				float staticSwitch38_g11661 = staticSwitch37_g11661;
				#endif
				#ifdef _TOGGLETIMEFREQUENCY_ON
				float staticSwitch42_g11661 = ( ( sin( ( staticSwitch38_g11661 * _TimeFrequency ) ) * _TimeRange ) + 100.0 );
				#else
				float staticSwitch42_g11661 = staticSwitch38_g11661;
				#endif
				float shaderTime237 = staticSwitch42_g11661;
				float temp_output_8_0_g11899 = shaderTime237;
				#ifdef _ENABLESINEMOVE_ON
				float2 staticSwitch4_g11899 = ( ( sin( ( temp_output_8_0_g11899 * _SineMoveFrequency ) ) * _SineMoveOffset * _SineMoveFade ) + temp_output_2_0_g11899 );
				#else
				float2 staticSwitch4_g11899 = temp_output_2_0_g11899;
				#endif
				#ifdef _ENABLEVIBRATE_ON
				float temp_output_30_0_g11900 = temp_output_8_0_g11899;
				float3 rotatedValue21_g11900 = RotateAroundAxis( float3( 0,0,0 ), float3( 0,1,0 ), float3( 0,0,1 ), ( temp_output_30_0_g11900 * _VibrateRotation ) );
				float2 staticSwitch6_g11899 = ( ( sin( ( _VibrateFrequency * temp_output_30_0_g11900 ) ) * _VibrateOffset * _VibrateFade * (rotatedValue21_g11900).xy ) + staticSwitch4_g11899 );
				#else
				float2 staticSwitch6_g11899 = staticSwitch4_g11899;
				#endif
				#ifdef _ENABLESINESCALE_ON
				float2 staticSwitch10_g11899 = ( staticSwitch6_g11899 + ( (v.vertex.xyz).xy * ( ( ( sin( ( _SineScaleFrequency * temp_output_8_0_g11899 ) ) + 1.0 ) * 0.5 ) * _SineScaleFactor ) ) );
				#else
				float2 staticSwitch10_g11899 = staticSwitch6_g11899;
				#endif
				float2 temp_output_424_0 = staticSwitch10_g11899;
#ifdef _SHADERFADING_MASK
				float2 uv_FadingMask = v.uv0.xy * _FadingMask_ST.xy + _FadingMask_ST.zw;
				#endif
#ifdef _SHADERFADING_MASK
				float4 tex2DNode3_g11712 = tex2Dlod( _FadingMask, float4( uv_FadingMask, 0, 0.0) );
				#endif
				float temp_output_4_0_g11715 = max( _FadingWidth , 0.001 );
				float2 texCoord435 = v.uv0.xy * float2( 1,1 ) + float2( 0,0 );
				#ifdef _PIXELPERFECTSPACE_ON
				float2 temp_output_432_0 = (_MainTex_TexelSize).zw;
				float2 staticSwitch437 = ( floor( ( texCoord435 * temp_output_432_0 ) ) / temp_output_432_0 );
				#else
				float2 staticSwitch437 = texCoord435;
				#endif
				float2 temp_output_61_0_g11663 = staticSwitch437;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 texCoord23_g11663 = v.uv0.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERSPACE_UI_GRAPHIC
				float2 appendResult28_g11663 = (float2(_RectWidth , _RectHeight));
				#endif
				#if defined(_SHADERSPACE_UV)
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#elif defined(_SHADERSPACE_UV_RAW)
				float2 staticSwitch1_g11663 = temp_output_61_0_g11663;
				#elif defined(_SHADERSPACE_OBJECT)
				float2 staticSwitch1_g11663 = (v.vertex.xyz).xy;
				#elif defined(_SHADERSPACE_OBJECT_SCALED)
				float2 staticSwitch1_g11663 = ( (v.vertex.xyz).xy * (ase_objectScale).xy );
				#elif defined(_SHADERSPACE_WORLD)
				float2 staticSwitch1_g11663 = (ase_worldPos).xy;
				#elif defined(_SHADERSPACE_UI_GRAPHIC)
				float2 staticSwitch1_g11663 = ( texCoord23_g11663 * ( appendResult28_g11663 / _PixelsPerUnit ) );
				#elif defined(_SHADERSPACE_SCREEN)
				float2 staticSwitch1_g11663 = ( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / _ScreenWidthUnits ) );
				#else
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#endif
				float2 shaderPosition235 = staticSwitch1_g11663;
				float linValue16_g11716 = tex2Dlod( _UberNoiseTexture, float4( ( shaderPosition235 * _FadingNoiseScale ), 0, 0.0) ).r;
				float localMyCustomExpression16_g11716 = MyCustomExpression16_g11716( linValue16_g11716 );
				float clampResult14_g11715 = clamp( ( ( ( _FadingFade * ( 1.0 + temp_output_4_0_g11715 ) ) - localMyCustomExpression16_g11716 ) / temp_output_4_0_g11715 ) , 0.0 , 1.0 );
				float2 temp_output_27_0_g11713 = shaderPosition235;
				float linValue16_g11714 = tex2Dlod( _UberNoiseTexture, float4( ( temp_output_27_0_g11713 * _FadingNoiseScale ), 0, 0.0) ).r;
				float localMyCustomExpression16_g11714 = MyCustomExpression16_g11714( linValue16_g11714 );
#ifdef _SHADERFADING_SPREAD
				float clampResult3_g11713 = clamp( ( ( _FadingFade - ( distance( _FadingPosition , temp_output_27_0_g11713 ) + ( localMyCustomExpression16_g11714 * _FadingNoiseFactor ) ) ) / max( _FadingWidth , 0.001 ) ) , 0.0 , 1.0 );
				#endif
				#if defined(_SHADERFADING_NONE)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_FULL)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_MASK)
				float staticSwitch139 = ( _FadingFade * ( tex2DNode3_g11712.r * tex2DNode3_g11712.a ) );
				#elif defined(_SHADERFADING_DISSOLVE)
				float staticSwitch139 = clampResult14_g11715;
				#elif defined(_SHADERFADING_SPREAD)
				float staticSwitch139 = clampResult3_g11713;
				#else
				float staticSwitch139 = _FadingFade;
				#endif
				float fullFade123 = staticSwitch139;
				float2 lerpResult121 = lerp( float2( 0,0 ) , temp_output_424_0 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float2 staticSwitch142 = temp_output_424_0;
				#elif defined(_SHADERFADING_FULL)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_MASK)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_DISSOLVE)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_SPREAD)
				float2 staticSwitch142 = lerpResult121;
				#else
				float2 staticSwitch142 = temp_output_424_0;
				#endif
				
				o.ase_texcoord5 = screenPos;
				o.ase_texcoord6.xyz = ase_worldPos;
				
				o.ase_texcoord7 = v.vertex;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord6.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = float3( staticSwitch142 ,  0.0 );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.normal = v.normal;
				v.tangent.xyz = v.tangent.xyz;

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);

				o.texCoord0 = v.uv0;
				o.color = v.color;
				o.clipPos = vertexInput.positionCS;

				float3 normalWS = TransformObjectToWorldNormal( v.normal );
				o.normalWS = -GetViewForwardDir();
				float4 tangentWS = float4( TransformObjectToWorldDir( v.tangent.xyz ), v.tangent.w );
				o.tangentWS = normalize( tangentWS );
				half crossSign = (tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
				o.bitangentWS = crossSign * cross( normalWS, tangentWS.xyz ) * tangentWS.w;
				return o;
			}

			half4 frag ( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				float2 texCoord363 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float4 screenPos = IN.ase_texcoord5;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				#ifdef _ENABLESCREENTILING_ON
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult16_g11656 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11656 = ( ( ( (( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / 10.0 ) )).xy * _ScreenTilingScale ) + _ScreenTilingOffset ) * ( _ScreenTilingPixelsPerUnit * appendResult16_g11656 ) );
				#else
				float2 staticSwitch2_g11656 = texCoord363;
				#endif
				float3 ase_worldPos = IN.ase_texcoord6.xyz;
				#ifdef _ENABLEWORLDTILING_ON
				float2 appendResult16_g11657 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11657 = ( ( ( (ase_worldPos).xy * _WorldTilingScale ) + _WorldTilingOffset ) * ( _WorldTilingPixelsPerUnit * appendResult16_g11657 ) );
				#else
				float2 staticSwitch2_g11657 = staticSwitch2_g11656;
				#endif
				float2 originalUV460 = staticSwitch2_g11657;
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11658 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch449 = ( floor( ( originalUV460 * appendResult7_g11658 ) ) / appendResult7_g11658 );
				#else
				float2 staticSwitch449 = originalUV460;
				#endif
				float2 uvAfterPixelArt450 = staticSwitch449;
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11662 = uvAfterPixelArt450;
				float2 appendResult374 = (float2(_SpriteSheetRect.x , _SpriteSheetRect.y));
				float2 spriteRectMin376 = appendResult374;
				float2 break11_g11662 = spriteRectMin376;
				float2 appendResult375 = (float2(_SpriteSheetRect.z , _SpriteSheetRect.w));
				float2 spriteRectMax377 = appendResult375;
				float2 break10_g11662 = spriteRectMax377;
				float2 break9_g11662 = float2( 0,0 );
				float2 break8_g11662 = float2( 1,1 );
				float2 appendResult15_g11662 = (float2((break9_g11662.x + (break14_g11662.x - break11_g11662.x) * (break8_g11662.x - break9_g11662.x) / (break10_g11662.x - break11_g11662.x)) , (break9_g11662.y + (break14_g11662.y - break11_g11662.y) * (break8_g11662.y - break9_g11662.y) / (break10_g11662.y - break11_g11662.y))));
				float2 staticSwitch366 = appendResult15_g11662;
				#else
				float2 staticSwitch366 = uvAfterPixelArt450;
				#endif
				float2 fixedUV475 = staticSwitch366;
				float2 temp_output_3_0_g11664 = fixedUV475;
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch117_g11665 = _WindMinIntensity;
				#else
				float staticSwitch117_g11665 = WindMinIntensity;
				#endif
				#endif
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch118_g11665 = _WindMaxIntensity;
				#else
				float staticSwitch118_g11665 = WindMaxIntensity;
				#endif
				#endif
				float4 transform62_g11665 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDISPARALLAX_ON
				float staticSwitch111_g11665 = _WindXPosition;
				#else
				float staticSwitch111_g11665 = transform62_g11665.x;
				#endif
				#endif
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch113_g11665 = _WindNoiseScale;
				#else
				float staticSwitch113_g11665 = WindNoiseScale;
				#endif
				#endif
				#ifdef _TOGGLECUSTOMTIME_ON
				float staticSwitch44_g11661 = _TimeValue;
				#else
				float staticSwitch44_g11661 = _TimeParameters.x;
				#endif
				#ifdef _TOGGLEUNSCALEDTIME_ON
				float staticSwitch34_g11661 = UnscaledTime;
				#else
				float staticSwitch34_g11661 = staticSwitch44_g11661;
				#endif
				#ifdef _TOGGLETIMESPEED_ON
				float staticSwitch37_g11661 = ( staticSwitch34_g11661 * _TimeSpeed );
				#else
				float staticSwitch37_g11661 = staticSwitch34_g11661;
				#endif
				#ifdef _TOGGLETIMEFPS_ON
				float staticSwitch38_g11661 = ( floor( ( staticSwitch37_g11661 * _TimeFPS ) ) / _TimeFPS );
				#else
				float staticSwitch38_g11661 = staticSwitch37_g11661;
				#endif
				#ifdef _TOGGLETIMEFREQUENCY_ON
				float staticSwitch42_g11661 = ( ( sin( ( staticSwitch38_g11661 * _TimeFrequency ) ) * _TimeRange ) + 100.0 );
				#else
				float staticSwitch42_g11661 = staticSwitch38_g11661;
				#endif
				float shaderTime237 = staticSwitch42_g11661;
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch125_g11665 = ( shaderTime237 * _WindNoiseSpeed );
				#else
				float staticSwitch125_g11665 = WindTime;
				#endif
				float temp_output_50_0_g11665 = ( ( staticSwitch111_g11665 * staticSwitch113_g11665 ) + staticSwitch125_g11665 );
				float x101_g11665 = temp_output_50_0_g11665;
				float localFastNoise101_g11665 = FastNoise101_g11665( x101_g11665 );
				float2 temp_cast_0 = (temp_output_50_0_g11665).xx;
				float simplePerlin2D121_g11665 = snoise( temp_cast_0*0.5 );
				simplePerlin2D121_g11665 = simplePerlin2D121_g11665*0.5 + 0.5;
				#ifdef _WINDHIGHQUALITYNOISE_ON
				float staticSwitch123_g11665 = simplePerlin2D121_g11665;
				#else
				float staticSwitch123_g11665 = ( localFastNoise101_g11665 + 0.5 );
				#endif
				float lerpResult86_g11665 = lerp( staticSwitch117_g11665 , staticSwitch118_g11665 , staticSwitch123_g11665);
				float clampResult29_g11665 = clamp( ( ( _WindRotationWindFactor * lerpResult86_g11665 ) + _WindRotation ) , -_WindMaxRotation , _WindMaxRotation );
				float2 temp_output_1_0_g11665 = temp_output_3_0_g11664;
				float temp_output_39_0_g11665 = ( temp_output_1_0_g11665.y + _WindFlip );
				float3 appendResult43_g11665 = (float3(0.5 , -_WindFlip , 0.0));
				float2 appendResult27_g11665 = (float2(0.0 , ( _WindSquishFactor * min( ( ( _WindSquishWindFactor * abs( lerpResult86_g11665 ) ) + abs( _WindRotation ) ) , _WindMaxRotation ) * temp_output_39_0_g11665 )));
				float3 rotatedValue19_g11665 = RotateAroundAxis( appendResult43_g11665, float3( ( appendResult27_g11665 + temp_output_1_0_g11665 ) ,  0.0 ), float3( 0,0,1 ), ( clampResult29_g11665 * temp_output_39_0_g11665 ) );
				float2 staticSwitch4_g11664 = (rotatedValue19_g11665).xy;
				#else
				float2 staticSwitch4_g11664 = temp_output_3_0_g11664;
				#endif
				float2 texCoord435 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				#ifdef _PIXELPERFECTSPACE_ON
				float2 temp_output_432_0 = (_MainTex_TexelSize).zw;
				float2 staticSwitch437 = ( floor( ( texCoord435 * temp_output_432_0 ) ) / temp_output_432_0 );
				#else
				float2 staticSwitch437 = texCoord435;
				#endif
				float2 temp_output_61_0_g11663 = staticSwitch437;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 texCoord23_g11663 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERSPACE_UI_GRAPHIC
				float2 appendResult28_g11663 = (float2(_RectWidth , _RectHeight));
				#endif
				#if defined(_SHADERSPACE_UV)
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#elif defined(_SHADERSPACE_UV_RAW)
				float2 staticSwitch1_g11663 = temp_output_61_0_g11663;
				#elif defined(_SHADERSPACE_OBJECT)
				float2 staticSwitch1_g11663 = (IN.ase_texcoord7.xyz).xy;
				#elif defined(_SHADERSPACE_OBJECT_SCALED)
				float2 staticSwitch1_g11663 = ( (IN.ase_texcoord7.xyz).xy * (ase_objectScale).xy );
				#elif defined(_SHADERSPACE_WORLD)
				float2 staticSwitch1_g11663 = (ase_worldPos).xy;
				#elif defined(_SHADERSPACE_UI_GRAPHIC)
				float2 staticSwitch1_g11663 = ( texCoord23_g11663 * ( appendResult28_g11663 / _PixelsPerUnit ) );
				#elif defined(_SHADERSPACE_SCREEN)
				float2 staticSwitch1_g11663 = ( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / _ScreenWidthUnits ) );
				#else
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#endif
				float2 shaderPosition235 = staticSwitch1_g11663;
				#ifdef _ENABLEFULLDISTORTION_ON
				float2 temp_output_195_0_g11666 = shaderPosition235;
				float linValue16_g11667 = tex2D( _UberNoiseTexture, ( temp_output_195_0_g11666 * _FullDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11667 = MyCustomExpression16_g11667( linValue16_g11667 );
				float linValue16_g11668 = tex2D( _UberNoiseTexture, ( ( temp_output_195_0_g11666 + float2( 0.321,0.321 ) ) * _FullDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11668 = MyCustomExpression16_g11668( linValue16_g11668 );
				float2 appendResult189_g11666 = (float2(( localMyCustomExpression16_g11667 - 0.5 ) , ( localMyCustomExpression16_g11668 - 0.5 )));
				float2 staticSwitch83 = ( staticSwitch4_g11664 + ( ( 1.0 - _FullDistortionFade ) * appendResult189_g11666 * _FullDistortionDistortion ) );
				#else
				float2 staticSwitch83 = staticSwitch4_g11664;
				#endif
				#ifdef _ENABLEDIRECTIONALDISTORTION_ON
				float2 temp_output_182_0_g11669 = shaderPosition235;
				float linValue16_g11671 = tex2D( _UberNoiseTexture, ( temp_output_182_0_g11669 * _DirectionalDistortionDistortionScale ) ).r;
				float localMyCustomExpression16_g11671 = MyCustomExpression16_g11671( linValue16_g11671 );
				float3 rotatedValue168_g11669 = RotateAroundAxis( float3( 0,0,0 ), float3( _DirectionalDistortionDistortion ,  0.0 ), float3( 0,0,1 ), ( ( ( localMyCustomExpression16_g11671 - 0.5 ) * 2.0 * _DirectionalDistortionRandomDirection ) * PI ) );
				float3 rotatedValue136_g11669 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_182_0_g11669 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalDistortionRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11669 = rotatedValue136_g11669;
				float linValue16_g11670 = tex2D( _UberNoiseTexture, ( temp_output_182_0_g11669 * _DirectionalDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11670 = MyCustomExpression16_g11670( linValue16_g11670 );
				float clampResult154_g11669 = clamp( ( ( break130_g11669.x + break130_g11669.y + _DirectionalDistortionFade + ( localMyCustomExpression16_g11670 * _DirectionalDistortionNoiseFactor ) ) / max( _DirectionalDistortionWidth , 0.001 ) ) , 0.0 , 1.0 );
				float2 staticSwitch82 = ( staticSwitch83 + ( (rotatedValue168_g11669).xy * ( 1.0 - (( _DirectionalDistortionInvert )?( ( 1.0 - clampResult154_g11669 ) ):( clampResult154_g11669 )) ) ) );
				#else
				float2 staticSwitch82 = staticSwitch83;
				#endif
				#ifdef _ENABLEHOLOGRAM_ON
				float temp_output_8_0_g11674 = ( ( ( shaderTime237 * _HologramDistortionSpeed ) + ase_worldPos.y ) / unity_OrthoParams.y );
				float2 temp_cast_4 = (temp_output_8_0_g11674).xx;
				float2 temp_cast_5 = (_HologramDistortionDensity).xx;
				float linValue16_g11676 = tex2D( _UberNoiseTexture, ( temp_cast_4 * temp_cast_5 ) ).r;
				float localMyCustomExpression16_g11676 = MyCustomExpression16_g11676( linValue16_g11676 );
				float clampResult75_g11674 = clamp( localMyCustomExpression16_g11676 , 0.075 , 0.6 );
				float2 temp_cast_6 = (temp_output_8_0_g11674).xx;
				float2 temp_cast_7 = (_HologramDistortionScale).xx;
				float linValue16_g11677 = tex2D( _UberNoiseTexture, ( temp_cast_6 * temp_cast_7 ) ).r;
				float localMyCustomExpression16_g11677 = MyCustomExpression16_g11677( linValue16_g11677 );
				float2 appendResult2_g11675 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float hologramFade182 = _HologramFade;
				float2 appendResult44_g11674 = (float2(( ( ( clampResult75_g11674 * ( localMyCustomExpression16_g11677 - 0.5 ) ) * _HologramDistortionOffset * ( 100.0 / appendResult2_g11675 ).x ) * hologramFade182 ) , 0.0));
				float2 staticSwitch59 = ( staticSwitch82 + appendResult44_g11674 );
				#else
				float2 staticSwitch59 = staticSwitch82;
				#endif
				#ifdef _ENABLEGLITCH_ON
				float2 temp_output_18_0_g11672 = shaderPosition235;
				float2 glitchPosition154 = temp_output_18_0_g11672;
				float linValue16_g11718 = tex2D( _UberNoiseTexture, ( ( glitchPosition154 + ( _GlitchDistortionSpeed * shaderTime237 ) ) * _GlitchDistortionScale ) ).r;
				float localMyCustomExpression16_g11718 = MyCustomExpression16_g11718( linValue16_g11718 );
				float linValue16_g11673 = tex2D( _UberNoiseTexture, ( ( temp_output_18_0_g11672 + ( _GlitchMaskSpeed * shaderTime237 ) ) * _GlitchMaskScale ) ).r;
				float localMyCustomExpression16_g11673 = MyCustomExpression16_g11673( linValue16_g11673 );
				float glitchFade152 = ( max( localMyCustomExpression16_g11673 , _GlitchMaskMin ) * _GlitchFade );
				float2 staticSwitch62 = ( staticSwitch59 + ( ( localMyCustomExpression16_g11718 - 0.5 ) * _GlitchDistortion * glitchFade152 ) );
				#else
				float2 staticSwitch62 = staticSwitch59;
				#endif
				float2 temp_output_1_0_g11719 = staticSwitch62;
				float2 temp_output_26_0_g11719 = shaderPosition235;
				float temp_output_25_0_g11719 = shaderTime237;
				#ifdef _ENABLEUVDISTORT_ON
				float linValue16_g11729 = tex2D( _UberNoiseTexture, ( ( temp_output_26_0_g11719 + ( _UVDistortSpeed * temp_output_25_0_g11719 ) ) * _UVDistortNoiseScale ) ).r;
				float localMyCustomExpression16_g11729 = MyCustomExpression16_g11729( linValue16_g11729 );
				float2 lerpResult21_g11726 = lerp( _UVDistortFrom , _UVDistortTo , localMyCustomExpression16_g11729);
				float2 appendResult2_g11728 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 uv_UVDistortMask = IN.texCoord0.xy * _UVDistortMask_ST.xy + _UVDistortMask_ST.zw;
				float4 tex2DNode3_g11727 = tex2D( _UVDistortMask, uv_UVDistortMask );
				#ifdef _UVDISTORTMASKTOGGLE_ON
				float staticSwitch29_g11726 = ( _UVDistortFade * ( tex2DNode3_g11727.r * tex2DNode3_g11727.a ) );
				#else
				float staticSwitch29_g11726 = _UVDistortFade;
				#endif
				float2 staticSwitch5_g11719 = ( temp_output_1_0_g11719 + ( lerpResult21_g11726 * ( 100.0 / appendResult2_g11728 ) * staticSwitch29_g11726 ) );
				#else
				float2 staticSwitch5_g11719 = temp_output_1_0_g11719;
				#endif
				#ifdef _ENABLESQUEEZE_ON
				float2 temp_output_1_0_g11725 = staticSwitch5_g11719;
				float2 staticSwitch7_g11719 = ( temp_output_1_0_g11725 + ( ( temp_output_1_0_g11725 - _SqueezeCenter ) * pow( distance( temp_output_1_0_g11725 , _SqueezeCenter ) , _SqueezePower ) * _SqueezeScale * _SqueezeFade ) );
				#else
				float2 staticSwitch7_g11719 = staticSwitch5_g11719;
				#endif
				#ifdef _ENABLESINEROTATE_ON
				float3 rotatedValue36_g11724 = RotateAroundAxis( float3( _SineRotatePivot ,  0.0 ), float3( staticSwitch7_g11719 ,  0.0 ), float3( 0,0,1 ), ( sin( ( temp_output_25_0_g11719 * _SineRotateFrequency ) ) * ( ( _SineRotateAngle / 360.0 ) * PI ) * _SineRotateFade ) );
				float2 staticSwitch9_g11719 = (rotatedValue36_g11724).xy;
				#else
				float2 staticSwitch9_g11719 = staticSwitch7_g11719;
				#endif
				#ifdef _ENABLEUVROTATE_ON
				float3 rotatedValue8_g11723 = RotateAroundAxis( float3( _UVRotatePivot ,  0.0 ), float3( staticSwitch9_g11719 ,  0.0 ), float3( 0,0,1 ), ( temp_output_25_0_g11719 * _UVRotateSpeed * PI ) );
				float2 staticSwitch16_g11719 = (rotatedValue8_g11723).xy;
				#else
				float2 staticSwitch16_g11719 = staticSwitch9_g11719;
				#endif
				#ifdef _ENABLEUVSCROLL_ON
				float2 staticSwitch14_g11719 = ( ( _UVScrollSpeed * temp_output_25_0_g11719 ) + staticSwitch16_g11719 );
				#else
				float2 staticSwitch14_g11719 = staticSwitch16_g11719;
				#endif
				#ifdef _ENABLEPIXELATE_ON
				float2 appendResult35_g11721 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 MultFactor30_g11721 = ( ( _PixelatePixelDensity * ( appendResult35_g11721 / _PixelatePixelsPerUnit ) ) * ( 1.0 / max( _PixelateFade , 1E-05 ) ) );
				float2 clampResult46_g11721 = clamp( ( floor( ( MultFactor30_g11721 * ( staticSwitch14_g11719 + ( float2( 0.5,0.5 ) / MultFactor30_g11721 ) ) ) ) / MultFactor30_g11721 ) , float2( 0,0 ) , float2( 1,1 ) );
				float2 staticSwitch4_g11719 = clampResult46_g11721;
				#else
				float2 staticSwitch4_g11719 = staticSwitch14_g11719;
				#endif
				#ifdef _ENABLEUVSCALE_ON
				float2 staticSwitch24_g11719 = ( ( ( staticSwitch4_g11719 - _UVScalePivot ) / _UVScaleScale ) + _UVScalePivot );
				#else
				float2 staticSwitch24_g11719 = staticSwitch4_g11719;
				#endif
				float2 temp_output_1_0_g11730 = staticSwitch24_g11719;
				#ifdef _ENABLEWIGGLE_ON
				float temp_output_7_0_g11730 = ( sin( ( _WiggleFrequency * ( temp_output_26_0_g11719.y + ( _WiggleSpeed * temp_output_25_0_g11719 ) ) ) ) * _WiggleOffset * _WiggleFade );
				#ifdef _WIGGLEFIXEDGROUNDTOGGLE_ON
				float staticSwitch18_g11730 = ( temp_output_7_0_g11730 * temp_output_1_0_g11730.y );
				#else
				float staticSwitch18_g11730 = temp_output_7_0_g11730;
				#endif
				float2 appendResult12_g11730 = (float2(staticSwitch18_g11730 , 0.0));
				float2 staticSwitch13_g11730 = ( temp_output_1_0_g11730 + appendResult12_g11730 );
				#else
				float2 staticSwitch13_g11730 = temp_output_1_0_g11730;
				#endif
				float2 temp_output_484_0 = staticSwitch13_g11730;
				float2 texCoord131 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERFADING_MASK
				float2 uv_FadingMask = IN.texCoord0.xy * _FadingMask_ST.xy + _FadingMask_ST.zw;
				#endif
#ifdef _SHADERFADING_MASK
				float4 tex2DNode3_g11712 = tex2D( _FadingMask, uv_FadingMask );
				#endif
				float temp_output_4_0_g11715 = max( _FadingWidth , 0.001 );
				float linValue16_g11716 = tex2D( _UberNoiseTexture, ( shaderPosition235 * _FadingNoiseScale ) ).r;
				float localMyCustomExpression16_g11716 = MyCustomExpression16_g11716( linValue16_g11716 );
				float clampResult14_g11715 = clamp( ( ( ( _FadingFade * ( 1.0 + temp_output_4_0_g11715 ) ) - localMyCustomExpression16_g11716 ) / temp_output_4_0_g11715 ) , 0.0 , 1.0 );
				float2 temp_output_27_0_g11713 = shaderPosition235;
				float linValue16_g11714 = tex2D( _UberNoiseTexture, ( temp_output_27_0_g11713 * _FadingNoiseScale ) ).r;
				float localMyCustomExpression16_g11714 = MyCustomExpression16_g11714( linValue16_g11714 );
#ifdef _SHADERFADING_SPREAD
				float clampResult3_g11713 = clamp( ( ( _FadingFade - ( distance( _FadingPosition , temp_output_27_0_g11713 ) + ( localMyCustomExpression16_g11714 * _FadingNoiseFactor ) ) ) / max( _FadingWidth , 0.001 ) ) , 0.0 , 1.0 );
				#endif
				#if defined(_SHADERFADING_NONE)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_FULL)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_MASK)
				float staticSwitch139 = ( _FadingFade * ( tex2DNode3_g11712.r * tex2DNode3_g11712.a ) );
				#elif defined(_SHADERFADING_DISSOLVE)
				float staticSwitch139 = clampResult14_g11715;
				#elif defined(_SHADERFADING_SPREAD)
				float staticSwitch139 = clampResult3_g11713;
				#else
				float staticSwitch139 = _FadingFade;
				#endif
				float fullFade123 = staticSwitch139;
				float2 lerpResult130 = lerp( texCoord131 , temp_output_484_0 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float2 staticSwitch145 = temp_output_484_0;
				#elif defined(_SHADERFADING_FULL)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_MASK)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_DISSOLVE)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_SPREAD)
				float2 staticSwitch145 = lerpResult130;
				#else
				float2 staticSwitch145 = temp_output_484_0;
				#endif
				#ifdef _TILINGFIX_ON
				float2 staticSwitch485 = ( ( ( staticSwitch145 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				#else
				float2 staticSwitch485 = staticSwitch145;
				#endif
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11731 = staticSwitch485;
				float2 break11_g11731 = float2( 0,0 );
				float2 break10_g11731 = float2( 1,1 );
				float2 break9_g11731 = spriteRectMin376;
				float2 break8_g11731 = spriteRectMax377;
				float2 appendResult15_g11731 = (float2((break9_g11731.x + (break14_g11731.x - break11_g11731.x) * (break8_g11731.x - break9_g11731.x) / (break10_g11731.x - break11_g11731.x)) , (break9_g11731.y + (break14_g11731.y - break11_g11731.y) * (break8_g11731.y - break9_g11731.y) / (break10_g11731.y - break11_g11731.y))));
				float2 staticSwitch371 = min( max( appendResult15_g11731 , spriteRectMin376 ) , spriteRectMax377 );
				#else
				float2 staticSwitch371 = staticSwitch485;
				#endif
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11732 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch427 = ( originalUV460 + ( floor( ( ( staticSwitch371 - uvAfterPixelArt450 ) * appendResult7_g11732 ) ) / appendResult7_g11732 ) );
				#else
				float2 staticSwitch427 = staticSwitch371;
				#endif
				float2 finalUV146 = staticSwitch427;
				float2 temp_output_1_0_g11733 = finalUV146;
				#ifdef _ENABLESMOOTHPIXELART_ON
				sampler2D tex3_g11734 = _MainTex;
				float4 textureTexelSize3_g11734 = _MainTex_TexelSize;
				float2 uvs3_g11734 = temp_output_1_0_g11733;
				float4 localtexturePointSmooth3_g11734 = texturePointSmooth( tex3_g11734 , textureTexelSize3_g11734 , uvs3_g11734 );
				float4 staticSwitch8_g11733 = localtexturePointSmooth3_g11734;
				#else
				float4 staticSwitch8_g11733 = tex2D( _MainTex, temp_output_1_0_g11733 );
				#endif
				#ifdef _ENABLEGAUSSIANBLUR_ON
				float temp_output_10_0_g11735 = ( _GaussianBlurOffset * _GaussianBlurFade * 0.005 );
				float temp_output_2_0_g11745 = temp_output_10_0_g11735;
				float2 appendResult16_g11745 = (float2(temp_output_2_0_g11745 , 0.0));
				float2 appendResult25_g11747 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11747 = ( appendResult16_g11745 * appendResult25_g11747 );
				float2 temp_output_7_0_g11735 = temp_output_1_0_g11733;
				float2 temp_output_1_0_g11745 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( 1,1 ) ) );
				float2 temp_output_1_0_g11747 = temp_output_1_0_g11745;
				float2 appendResult17_g11745 = (float2(0.0 , temp_output_2_0_g11745));
				float2 appendResult25_g11746 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11746 = ( appendResult17_g11745 * appendResult25_g11746 );
				float2 temp_output_1_0_g11746 = temp_output_1_0_g11745;
				float temp_output_2_0_g11736 = temp_output_10_0_g11735;
				float2 appendResult16_g11736 = (float2(temp_output_2_0_g11736 , 0.0));
				float2 appendResult25_g11738 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11738 = ( appendResult16_g11736 * appendResult25_g11738 );
				float2 temp_output_1_0_g11736 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( -1,1 ) ) );
				float2 temp_output_1_0_g11738 = temp_output_1_0_g11736;
				float2 appendResult17_g11736 = (float2(0.0 , temp_output_2_0_g11736));
				float2 appendResult25_g11737 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11737 = ( appendResult17_g11736 * appendResult25_g11737 );
				float2 temp_output_1_0_g11737 = temp_output_1_0_g11736;
				float temp_output_2_0_g11742 = temp_output_10_0_g11735;
				float2 appendResult16_g11742 = (float2(temp_output_2_0_g11742 , 0.0));
				float2 appendResult25_g11744 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11744 = ( appendResult16_g11742 * appendResult25_g11744 );
				float2 temp_output_1_0_g11742 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( -1,-1 ) ) );
				float2 temp_output_1_0_g11744 = temp_output_1_0_g11742;
				float2 appendResult17_g11742 = (float2(0.0 , temp_output_2_0_g11742));
				float2 appendResult25_g11743 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11743 = ( appendResult17_g11742 * appendResult25_g11743 );
				float2 temp_output_1_0_g11743 = temp_output_1_0_g11742;
				float temp_output_2_0_g11739 = temp_output_10_0_g11735;
				float2 appendResult16_g11739 = (float2(temp_output_2_0_g11739 , 0.0));
				float2 appendResult25_g11741 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11741 = ( appendResult16_g11739 * appendResult25_g11741 );
				float2 temp_output_1_0_g11739 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( 1,-1 ) ) );
				float2 temp_output_1_0_g11741 = temp_output_1_0_g11739;
				float2 appendResult17_g11739 = (float2(0.0 , temp_output_2_0_g11739));
				float2 appendResult25_g11740 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11740 = ( appendResult17_g11739 * appendResult25_g11740 );
				float2 temp_output_1_0_g11740 = temp_output_1_0_g11739;
				float4 staticSwitch3_g11733 = ( ( ( ( tex2D( _MainTex, ( temp_output_26_0_g11747 + temp_output_1_0_g11747 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11747 + temp_output_1_0_g11747 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11746 + temp_output_1_0_g11746 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11746 + temp_output_1_0_g11746 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11738 + temp_output_1_0_g11738 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11738 + temp_output_1_0_g11738 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11737 + temp_output_1_0_g11737 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11737 + temp_output_1_0_g11737 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11744 + temp_output_1_0_g11744 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11744 + temp_output_1_0_g11744 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11743 + temp_output_1_0_g11743 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11743 + temp_output_1_0_g11743 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11741 + temp_output_1_0_g11741 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11741 + temp_output_1_0_g11741 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11740 + temp_output_1_0_g11740 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11740 + temp_output_1_0_g11740 ) ) ) ) ) * 0.0625 );
				#else
				float4 staticSwitch3_g11733 = staticSwitch8_g11733;
				#endif
				#ifdef _ENABLESHARPEN_ON
				float2 temp_output_1_0_g11748 = temp_output_1_0_g11733;
				float4 tex2DNode4_g11748 = tex2D( _MainTex, temp_output_1_0_g11748 );
				float temp_output_2_0_g11749 = _SharpenOffset;
				float2 appendResult16_g11749 = (float2(temp_output_2_0_g11749 , 0.0));
				float2 appendResult25_g11751 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11751 = ( appendResult16_g11749 * appendResult25_g11751 );
				float2 temp_output_1_0_g11749 = temp_output_1_0_g11748;
				float2 temp_output_1_0_g11751 = temp_output_1_0_g11749;
				float2 appendResult17_g11749 = (float2(0.0 , temp_output_2_0_g11749));
				float2 appendResult25_g11750 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11750 = ( appendResult17_g11749 * appendResult25_g11750 );
				float2 temp_output_1_0_g11750 = temp_output_1_0_g11749;
				float4 break22_g11748 = ( tex2DNode4_g11748 - ( ( ( ( ( tex2D( _MainTex, ( temp_output_26_0_g11751 + temp_output_1_0_g11751 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11751 + temp_output_1_0_g11751 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11750 + temp_output_1_0_g11750 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11750 + temp_output_1_0_g11750 ) ) ) ) / 4.0 ) - tex2DNode4_g11748 ) * ( _SharpenFactor * _SharpenFade ) ) );
				float clampResult23_g11748 = clamp( break22_g11748.a , 0.0 , 1.0 );
				float4 appendResult24_g11748 = (float4(break22_g11748.r , break22_g11748.g , break22_g11748.b , clampResult23_g11748));
				float4 staticSwitch12_g11733 = appendResult24_g11748;
				#else
				float4 staticSwitch12_g11733 = staticSwitch3_g11733;
				#endif
				float4 temp_output_471_0 = staticSwitch12_g11733;
				#ifdef _VERTEXTINTFIRST_ON
				float4 temp_output_1_0_g11752 = temp_output_471_0;
				float4 appendResult8_g11752 = (float4(( (temp_output_1_0_g11752).rgb * (IN.color).rgb ) , temp_output_1_0_g11752.a));
				float4 staticSwitch354 = appendResult8_g11752;
				#else
				float4 staticSwitch354 = temp_output_471_0;
				#endif
				float4 originalColor191 = staticSwitch354;
				float4 temp_output_1_0_g11753 = originalColor191;
				float4 temp_output_1_0_g11754 = temp_output_1_0_g11753;
				float2 temp_output_7_0_g11753 = finalUV146;
				#ifdef _ENABLESMOKE_ON
				float2 temp_output_43_0_g11754 = temp_output_7_0_g11753;
				float2 temp_cast_15 = (_SmokeNoiseScale).xx;
				float linValue16_g11755 = tex2D( _UberNoiseTexture, ( ( ( IN.color.r * (( _SmokeVertexSeed )?( 5.0 ):( 0.0 )) ) + temp_output_43_0_g11754 ) * temp_cast_15 ) ).r;
				float localMyCustomExpression16_g11755 = MyCustomExpression16_g11755( linValue16_g11755 );
				float clampResult28_g11754 = clamp( ( ( ( localMyCustomExpression16_g11755 - 1.0 ) * _SmokeNoiseFactor ) + ( ( ( IN.color.a / 2.5 ) - distance( temp_output_43_0_g11754 , float2( 0.5,0.5 ) ) ) * 2.5 * _SmokeSmoothness ) ) , 0.0 , 1.0 );
				float3 lerpResult34_g11754 = lerp( (temp_output_1_0_g11754).rgb , float3( 0,0,0 ) , ( ( 1.0 - clampResult28_g11754 ) * _SmokeDarkEdge ));
				float4 appendResult31_g11754 = (float4(lerpResult34_g11754 , ( clampResult28_g11754 * _SmokeAlpha * temp_output_1_0_g11754.a )));
				float4 staticSwitch2_g11753 = appendResult31_g11754;
				#else
				float4 staticSwitch2_g11753 = temp_output_1_0_g11753;
				#endif
				#ifdef _ENABLECUSTOMFADE_ON
				float4 temp_output_1_0_g11756 = staticSwitch2_g11753;
				float2 temp_output_57_0_g11756 = temp_output_7_0_g11753;
				float4 tex2DNode3_g11756 = tex2D( _CustomFadeFadeMask, temp_output_57_0_g11756 );
				float linValue16_g11757 = tex2D( _UberNoiseTexture, ( temp_output_57_0_g11756 * _CustomFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11757 = MyCustomExpression16_g11757( linValue16_g11757 );
				float clampResult37_g11756 = clamp( ( ( ( IN.color.a * 2.0 ) - 1.0 ) + ( tex2DNode3_g11756.r + ( localMyCustomExpression16_g11757 * _CustomFadeNoiseFactor ) ) ) , 0.0 , 1.0 );
				float4 appendResult13_g11756 = (float4((temp_output_1_0_g11756).rgb , ( temp_output_1_0_g11756.a * pow( clampResult37_g11756 , ( _CustomFadeSmoothness / max( tex2DNode3_g11756.r , 0.05 ) ) ) * _CustomFadeAlpha )));
				float4 staticSwitch3_g11753 = appendResult13_g11756;
				#else
				float4 staticSwitch3_g11753 = staticSwitch2_g11753;
				#endif
				float4 temp_output_1_0_g11758 = staticSwitch3_g11753;
				#ifdef _ENABLECHECKERBOARD_ON
				float4 temp_output_1_0_g11759 = temp_output_1_0_g11758;
				float2 appendResult4_g11759 = (float2(ase_worldPos.x , ase_worldPos.y));
				float2 temp_output_44_0_g11759 = ( appendResult4_g11759 * _CheckerboardTiling * 0.5 );
				float2 break12_g11759 = step( ( ceil( temp_output_44_0_g11759 ) - temp_output_44_0_g11759 ) , float2( 0.5,0.5 ) );
				float4 appendResult42_g11759 = (float4(( (temp_output_1_0_g11759).rgb * min( ( _CheckerboardDarken + abs( ( -break12_g11759.x + break12_g11759.y ) ) ) , 1.0 ) ) , temp_output_1_0_g11759.a));
				float4 staticSwitch2_g11758 = appendResult42_g11759;
				#else
				float4 staticSwitch2_g11758 = temp_output_1_0_g11758;
				#endif
				#ifdef _ENABLEFLAME_ON
				float2 temp_output_75_0_g11760 = finalUV146;
				float linValue16_g11761 = tex2D( _UberNoiseTexture, ( ( ( shaderTime237 * _FlameSpeed ) + temp_output_75_0_g11760 ) * _FlameNoiseScale ) ).r;
				float localMyCustomExpression16_g11761 = MyCustomExpression16_g11761( linValue16_g11761 );
				float saferPower57_g11760 = abs( max( ( temp_output_75_0_g11760.y - 0.2 ) , 0.0 ) );
				float temp_output_47_0_g11760 = max( _FlameRadius , 0.01 );
				float clampResult70_g11760 = clamp( ( ( ( localMyCustomExpression16_g11761 * pow( saferPower57_g11760 , _FlameNoiseHeightFactor ) * _FlameNoiseFactor ) + ( ( temp_output_47_0_g11760 - distance( temp_output_75_0_g11760 , float2( 0.5,0.4 ) ) ) / temp_output_47_0_g11760 ) ) * _FlameSmooth ) , 0.0 , 1.0 );
				float temp_output_63_0_g11760 = ( clampResult70_g11760 * _FlameBrightness );
				float4 appendResult31_g11760 = (float4(temp_output_63_0_g11760 , temp_output_63_0_g11760 , temp_output_63_0_g11760 , clampResult70_g11760));
				float4 staticSwitch6_g11758 = ( appendResult31_g11760 * staticSwitch2_g11758 );
				#else
				float4 staticSwitch6_g11758 = staticSwitch2_g11758;
				#endif
				float4 temp_output_3_0_g11762 = staticSwitch6_g11758;
				float4 temp_output_1_0_g11794 = temp_output_3_0_g11762;
				float4 appendResult91_g11794 = (float4((temp_output_1_0_g11794).rgb , 1.0));
				float2 temp_output_1_0_g11762 = finalUV146;
				#ifdef _ENABLERECOLORRGB_ON
				#ifdef _RECOLORRGBTEXTURETOGGLE_ON
				float4 staticSwitch81_g11794 = tex2D( _RecolorRGBTexture, temp_output_1_0_g11762 );
				#else
				float4 staticSwitch81_g11794 = appendResult91_g11794;
				#endif
				float3 break82_g11794 = (staticSwitch81_g11794).xyz;
				float temp_output_84_0_g11794 = max( ( break82_g11794.x + break82_g11794.y + break82_g11794.z ) , 0.001 );
				float4 break71_g11794 = ( ( _RecolorRGBRedTint * ( break82_g11794.x / temp_output_84_0_g11794 ) ) + ( _RecolorRGBGreenTint * ( break82_g11794.y / temp_output_84_0_g11794 ) ) + ( ( break82_g11794.z / temp_output_84_0_g11794 ) * _RecolorRGBBlueTint ) );
				float3 appendResult56_g11794 = (float3(break71_g11794.r , break71_g11794.g , break71_g11794.b));
				float4 break2_g11795 = temp_output_1_0_g11794;
				float saferPower57_g11794 = abs( ( ( break2_g11795.x + break2_g11795.x + break2_g11795.y + break2_g11795.y + break2_g11795.y + break2_g11795.z ) / 6.0 ) );
				float3 lerpResult26_g11794 = lerp( (temp_output_1_0_g11794).rgb , ( appendResult56_g11794 * pow( saferPower57_g11794 , ( max( break71_g11794.a , 0.01 ) * 2.0 ) ) ) , ( staticSwitch81_g11794.w * _RecolorRGBFade ));
				float4 appendResult30_g11794 = (float4(lerpResult26_g11794 , temp_output_1_0_g11794.a));
				float4 staticSwitch43_g11762 = appendResult30_g11794;
				#else
				float4 staticSwitch43_g11762 = temp_output_3_0_g11762;
				#endif
				#ifdef _ENABLERECOLORRGBYCP_ON
				float4 temp_output_1_0_g11792 = staticSwitch43_g11762;
				#ifdef _RECOLORRGBYCPTEXTURETOGGLE_ON
				float4 staticSwitch62_g11792 = tex2D( _RecolorRGBYCPTexture, temp_output_1_0_g11762 );
				#else
				float4 staticSwitch62_g11792 = temp_output_1_0_g11792;
				#endif
				float3 hsvTorgb33_g11792 = RGBToHSV( staticSwitch62_g11792.rgb );
				float temp_output_43_0_g11792 = ( ( hsvTorgb33_g11792.x + 0.08333334 ) % 1.0 );
				float4 ifLocalVar46_g11792 = 0;
				if( temp_output_43_0_g11792 >= 0.8333333 )
				ifLocalVar46_g11792 = _RecolorRGBYCPPurpleTint;
				else
				ifLocalVar46_g11792 = _RecolorRGBYCPBlueTint;
				float4 ifLocalVar44_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.6666667 )
				ifLocalVar44_g11792 = _RecolorRGBYCPCyanTint;
				else
				ifLocalVar44_g11792 = ifLocalVar46_g11792;
				float4 ifLocalVar47_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.3333333 )
				ifLocalVar47_g11792 = _RecolorRGBYCPYellowTint;
				else
				ifLocalVar47_g11792 = _RecolorRGBYCPGreenTint;
				float4 ifLocalVar45_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.1666667 )
				ifLocalVar45_g11792 = _RecolorRGBYCPRedTint;
				else
				ifLocalVar45_g11792 = ifLocalVar47_g11792;
				float4 ifLocalVar35_g11792 = 0;
				if( temp_output_43_0_g11792 >= 0.5 )
				ifLocalVar35_g11792 = ifLocalVar44_g11792;
				else
				ifLocalVar35_g11792 = ifLocalVar45_g11792;
				float4 break55_g11792 = ifLocalVar35_g11792;
				float3 appendResult56_g11792 = (float3(break55_g11792.r , break55_g11792.g , break55_g11792.b));
				float4 break2_g11793 = temp_output_1_0_g11792;
				float saferPower57_g11792 = abs( ( ( break2_g11793.x + break2_g11793.x + break2_g11793.y + break2_g11793.y + break2_g11793.y + break2_g11793.z ) / 6.0 ) );
				float3 lerpResult26_g11792 = lerp( (temp_output_1_0_g11792).rgb , ( appendResult56_g11792 * pow( saferPower57_g11792 , max( ( break55_g11792.a * 2.0 ) , 0.01 ) ) ) , ( hsvTorgb33_g11792.z * _RecolorRGBYCPFade ));
				float4 appendResult30_g11792 = (float4(lerpResult26_g11792 , temp_output_1_0_g11792.a));
				float4 staticSwitch9_g11762 = appendResult30_g11792;
				#else
				float4 staticSwitch9_g11762 = staticSwitch43_g11762;
				#endif
				#ifdef _ENABLECOLORREPLACE_ON
				float4 temp_output_1_0_g11766 = staticSwitch9_g11762;
				float3 temp_output_2_0_g11766 = (temp_output_1_0_g11766).rgb;
				float3 In115_g11766 = temp_output_2_0_g11766;
				float3 From115_g11766 = (_ColorReplaceFromColor).rgb;
				float4 break2_g11767 = temp_output_1_0_g11766;
				float3 To115_g11766 = ( pow( ( ( break2_g11767.x + break2_g11767.x + break2_g11767.y + break2_g11767.y + break2_g11767.y + break2_g11767.z ) / 6.0 ) , max( _ColorReplaceContrast , 0.001 ) ) * (_ColorReplaceToColor).rgb );
				float Fuzziness115_g11766 = _ColorReplaceSmoothness;
				float Range115_g11766 = _ColorReplaceRange;
				float3 localMyCustomExpression115_g11766 = MyCustomExpression115_g11766( In115_g11766 , From115_g11766 , To115_g11766 , Fuzziness115_g11766 , Range115_g11766 );
				float3 lerpResult112_g11766 = lerp( temp_output_2_0_g11766 , localMyCustomExpression115_g11766 , _ColorReplaceFade);
				float4 appendResult4_g11766 = (float4(lerpResult112_g11766 , temp_output_1_0_g11766.a));
				float4 staticSwitch29_g11762 = appendResult4_g11766;
				#else
				float4 staticSwitch29_g11762 = staticSwitch9_g11762;
				#endif
				float4 temp_output_1_0_g11779 = staticSwitch29_g11762;
				#ifdef _ENABLENEGATIVE_ON
				float3 temp_output_9_0_g11779 = (temp_output_1_0_g11779).rgb;
				float3 lerpResult3_g11779 = lerp( temp_output_9_0_g11779 , ( 1.0 - temp_output_9_0_g11779 ) , _NegativeFade);
				float4 appendResult8_g11779 = (float4(lerpResult3_g11779 , temp_output_1_0_g11779.a));
				float4 staticSwitch4_g11779 = appendResult8_g11779;
				#else
				float4 staticSwitch4_g11779 = temp_output_1_0_g11779;
				#endif
				float4 temp_output_57_0_g11762 = staticSwitch4_g11779;
				#ifdef _ENABLECONTRAST_ON
				float4 temp_output_1_0_g11802 = temp_output_57_0_g11762;
				float3 temp_cast_30 = (max( _Contrast , 0.001 )).xxx;
				float4 appendResult4_g11802 = (float4(pow( (temp_output_1_0_g11802).rgb , temp_cast_30 ) , temp_output_1_0_g11802.a));
				float4 staticSwitch32_g11762 = appendResult4_g11802;
				#else
				float4 staticSwitch32_g11762 = temp_output_57_0_g11762;
				#endif
				#ifdef _ENABLEBRIGHTNESS_ON
				float4 temp_output_2_0_g11777 = staticSwitch32_g11762;
				float4 appendResult6_g11777 = (float4(( (temp_output_2_0_g11777).rgb * _Brightness ) , temp_output_2_0_g11777.a));
				float4 staticSwitch33_g11762 = appendResult6_g11777;
				#else
				float4 staticSwitch33_g11762 = staticSwitch32_g11762;
				#endif
				#ifdef _ENABLEHUE_ON
				float4 temp_output_2_0_g11778 = staticSwitch33_g11762;
				float3 hsvTorgb1_g11778 = RGBToHSV( temp_output_2_0_g11778.rgb );
				float3 hsvTorgb3_g11778 = HSVToRGB( float3(( hsvTorgb1_g11778.x + _Hue ),hsvTorgb1_g11778.y,hsvTorgb1_g11778.z) );
				float4 appendResult8_g11778 = (float4(hsvTorgb3_g11778 , temp_output_2_0_g11778.a));
				float4 staticSwitch36_g11762 = appendResult8_g11778;
				#else
				float4 staticSwitch36_g11762 = staticSwitch33_g11762;
				#endif
				#ifdef _ENABLESPLITTONING_ON
				float4 temp_output_1_0_g11796 = staticSwitch36_g11762;
				float4 break2_g11797 = temp_output_1_0_g11796;
				float temp_output_3_0_g11796 = ( ( break2_g11797.x + break2_g11797.x + break2_g11797.y + break2_g11797.y + break2_g11797.y + break2_g11797.z ) / 6.0 );
				float clampResult25_g11796 = clamp( ( ( ( ( temp_output_3_0_g11796 + _SplitToningShift ) - 0.5 ) * _SplitToningBalance ) + 0.5 ) , 0.0 , 1.0 );
				float3 lerpResult6_g11796 = lerp( (_SplitToningShadowsColor).rgb , (_SplitToningHighlightsColor).rgb , clampResult25_g11796);
				float3 lerpResult11_g11796 = lerp( (temp_output_1_0_g11796).rgb , ( lerpResult6_g11796 * pow( temp_output_3_0_g11796 , max( _SplitToningContrast , 0.001 ) ) ) , _SplitToningFade);
				float4 appendResult18_g11796 = (float4(lerpResult11_g11796 , temp_output_1_0_g11796.a));
				float4 staticSwitch30_g11762 = appendResult18_g11796;
				#else
				float4 staticSwitch30_g11762 = staticSwitch36_g11762;
				#endif
				#ifdef _ENABLEBLACKTINT_ON
				float4 temp_output_1_0_g11774 = staticSwitch30_g11762;
				float3 temp_output_4_0_g11774 = (temp_output_1_0_g11774).rgb;
				float4 break12_g11774 = temp_output_1_0_g11774;
				float3 lerpResult7_g11774 = lerp( temp_output_4_0_g11774 , ( temp_output_4_0_g11774 + (_BlackTintColor).rgb ) , pow( ( 1.0 - min( max( max( break12_g11774.r , break12_g11774.g ) , break12_g11774.b ) , 1.0 ) ) , max( _BlackTintPower , 0.001 ) ));
				float3 lerpResult13_g11774 = lerp( temp_output_4_0_g11774 , lerpResult7_g11774 , _BlackTintFade);
				float4 appendResult11_g11774 = (float4(lerpResult13_g11774 , break12_g11774.a));
				float4 staticSwitch20_g11762 = appendResult11_g11774;
				#else
				float4 staticSwitch20_g11762 = staticSwitch30_g11762;
				#endif
				#ifdef _ENABLEINKSPREAD_ON
				float4 temp_output_1_0_g11788 = staticSwitch20_g11762;
				float4 break2_g11790 = temp_output_1_0_g11788;
				float2 temp_output_65_0_g11788 = shaderPosition235;
				float linValue16_g11789 = tex2D( _UberNoiseTexture, ( temp_output_65_0_g11788 * _InkSpreadNoiseScale ) ).r;
				float localMyCustomExpression16_g11789 = MyCustomExpression16_g11789( linValue16_g11789 );
				float clampResult53_g11788 = clamp( ( ( ( _InkSpreadDistance - distance( _InkSpreadPosition , temp_output_65_0_g11788 ) ) + ( localMyCustomExpression16_g11789 * _InkSpreadNoiseFactor ) ) / max( _InkSpreadWidth , 0.001 ) ) , 0.0 , 1.0 );
				float3 lerpResult7_g11788 = lerp( (temp_output_1_0_g11788).rgb , ( (_InkSpreadColor).rgb * pow( ( ( break2_g11790.x + break2_g11790.x + break2_g11790.y + break2_g11790.y + break2_g11790.y + break2_g11790.z ) / 6.0 ) , max( _InkSpreadContrast , 0.001 ) ) ) , ( _InkSpreadFade * clampResult53_g11788 ));
				float4 appendResult9_g11788 = (float4(lerpResult7_g11788 , (temp_output_1_0_g11788).a));
				float4 staticSwitch17_g11762 = appendResult9_g11788;
				#else
				float4 staticSwitch17_g11762 = staticSwitch20_g11762;
				#endif
				float temp_output_39_0_g11762 = shaderTime237;
				#ifdef _ENABLESHIFTHUE_ON
				float4 temp_output_1_0_g11780 = staticSwitch17_g11762;
				float3 hsvTorgb15_g11780 = RGBToHSV( (temp_output_1_0_g11780).rgb );
				float3 hsvTorgb19_g11780 = HSVToRGB( float3(( ( temp_output_39_0_g11762 * _ShiftHueSpeed ) + hsvTorgb15_g11780.x ),hsvTorgb15_g11780.y,hsvTorgb15_g11780.z) );
				float4 appendResult6_g11780 = (float4(hsvTorgb19_g11780 , temp_output_1_0_g11780.a));
				float4 staticSwitch19_g11762 = appendResult6_g11780;
				#else
				float4 staticSwitch19_g11762 = staticSwitch17_g11762;
				#endif
				#ifdef _ENABLEADDHUE_ON
				float3 hsvTorgb19_g11784 = HSVToRGB( float3(( ( temp_output_39_0_g11762 * _AddHueSpeed ) % 1.0 ),_AddHueSaturation,_AddHueBrightness) );
				float4 temp_output_1_0_g11784 = staticSwitch19_g11762;
				float4 break2_g11786 = temp_output_1_0_g11784;
				float2 uv_AddHueMask = IN.texCoord0.xy * _AddHueMask_ST.xy + _AddHueMask_ST.zw;
				float4 tex2DNode3_g11785 = tex2D( _AddHueMask, uv_AddHueMask );
				#ifdef _ADDHUEMASKTOGGLE_ON
				float staticSwitch33_g11784 = ( _AddHueFade * ( tex2DNode3_g11785.r * tex2DNode3_g11785.a ) );
				#else
				float staticSwitch33_g11784 = _AddHueFade;
				#endif
				float4 appendResult6_g11784 = (float4(( ( hsvTorgb19_g11784 * pow( ( ( break2_g11786.x + break2_g11786.x + break2_g11786.y + break2_g11786.y + break2_g11786.y + break2_g11786.z ) / 6.0 ) , max( _AddHueContrast , 0.001 ) ) * staticSwitch33_g11784 ) + (temp_output_1_0_g11784).rgb ) , temp_output_1_0_g11784.a));
				float4 staticSwitch23_g11762 = appendResult6_g11784;
				#else
				float4 staticSwitch23_g11762 = staticSwitch19_g11762;
				#endif
				#ifdef _ENABLESINEGLOW_ON
				float4 temp_output_1_0_g11781 = staticSwitch23_g11762;
				float4 break2_g11782 = temp_output_1_0_g11781;
				float3 temp_output_13_0_g11781 = (_SineGlowColor).rgb;
				float2 uv_SineGlowMask = IN.texCoord0.xy * _SineGlowMask_ST.xy + _SineGlowMask_ST.zw;
				float4 tex2DNode30_g11781 = tex2D( _SineGlowMask, uv_SineGlowMask );
				#ifdef _SINEGLOWMASKTOGGLE_ON
				float3 staticSwitch27_g11781 = ( (tex2DNode30_g11781).rgb * temp_output_13_0_g11781 * tex2DNode30_g11781.a );
				#else
				float3 staticSwitch27_g11781 = temp_output_13_0_g11781;
				#endif
				float4 appendResult21_g11781 = (float4(( (temp_output_1_0_g11781).rgb + ( pow( ( ( break2_g11782.x + break2_g11782.x + break2_g11782.y + break2_g11782.y + break2_g11782.y + break2_g11782.z ) / 6.0 ) , max( _SineGlowContrast , 0.001 ) ) * staticSwitch27_g11781 * _SineGlowFade * ( ( ( sin( ( temp_output_39_0_g11762 * _SineGlowFrequency ) ) + 1.0 ) * ( _SineGlowMax - _SineGlowMin ) ) + _SineGlowMin ) ) ) , temp_output_1_0_g11781.a));
				float4 staticSwitch28_g11762 = appendResult21_g11781;
				#else
				float4 staticSwitch28_g11762 = staticSwitch23_g11762;
				#endif
				#ifdef _ENABLESATURATION_ON
				float4 temp_output_1_0_g11769 = staticSwitch28_g11762;
				float4 break2_g11770 = temp_output_1_0_g11769;
				float3 temp_cast_46 = (( ( break2_g11770.x + break2_g11770.x + break2_g11770.y + break2_g11770.y + break2_g11770.y + break2_g11770.z ) / 6.0 )).xxx;
				float3 lerpResult5_g11769 = lerp( temp_cast_46 , (temp_output_1_0_g11769).rgb , _Saturation);
				float4 appendResult8_g11769 = (float4(lerpResult5_g11769 , temp_output_1_0_g11769.a));
				float4 staticSwitch38_g11762 = appendResult8_g11769;
				#else
				float4 staticSwitch38_g11762 = staticSwitch28_g11762;
				#endif
				#ifdef _ENABLEINNEROUTLINE_ON
				float4 temp_output_15_0_g11771 = staticSwitch38_g11762;
				float3 temp_output_82_0_g11771 = (_InnerOutlineColor).rgb;
				float2 temp_output_7_0_g11771 = temp_output_1_0_g11762;
				float temp_output_179_0_g11771 = temp_output_39_0_g11762;
				#ifdef _INNEROUTLINETEXTURETOGGLE_ON
				float3 staticSwitch187_g11771 = ( (tex2D( _InnerOutlineTintTexture, ( temp_output_7_0_g11771 + ( _InnerOutlineTextureSpeed * temp_output_179_0_g11771 ) ) )).rgb * temp_output_82_0_g11771 );
				#else
				float3 staticSwitch187_g11771 = temp_output_82_0_g11771;
				#endif
				float linValue16_g11773 = tex2D( _UberNoiseTexture, ( ( ( temp_output_179_0_g11771 * _InnerOutlineNoiseSpeed ) + temp_output_7_0_g11771 ) * _InnerOutlineNoiseScale ) ).r;
				float localMyCustomExpression16_g11773 = MyCustomExpression16_g11773( linValue16_g11773 );
				#ifdef _INNEROUTLINEDISTORTIONTOGGLE_ON
				float2 staticSwitch169_g11771 = ( ( localMyCustomExpression16_g11773 - 0.5 ) * _InnerOutlineDistortionIntensity );
				#else
				float2 staticSwitch169_g11771 = float2( 0,0 );
				#endif
				float2 temp_output_131_0_g11771 = ( staticSwitch169_g11771 + temp_output_7_0_g11771 );
				float2 appendResult2_g11772 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_25_0_g11771 = ( 100.0 / appendResult2_g11772 );
				float temp_output_178_0_g11771 = ( _InnerOutlineFade * ( 1.0 - min( min( min( min( min( min( min( tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0,-1 ) ) * temp_output_25_0_g11771 ) ) ).a , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0,1 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -1,0 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 1,0 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0.705,0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -0.705,0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0.705,-0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -0.705,-0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) ) );
				float3 lerpResult176_g11771 = lerp( (temp_output_15_0_g11771).rgb , staticSwitch187_g11771 , temp_output_178_0_g11771);
				#ifdef _INNEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch188_g11771 = ( temp_output_178_0_g11771 * temp_output_15_0_g11771.a );
				#else
				float staticSwitch188_g11771 = temp_output_15_0_g11771.a;
				#endif
				float4 appendResult177_g11771 = (float4(lerpResult176_g11771 , staticSwitch188_g11771));
				float4 staticSwitch12_g11762 = appendResult177_g11771;
				#else
				float4 staticSwitch12_g11762 = staticSwitch38_g11762;
				#endif
				#ifdef _ENABLEOUTEROUTLINE_ON
				float4 temp_output_15_0_g11799 = staticSwitch12_g11762;
				float3 temp_output_82_0_g11799 = (_OuterOutlineColor).rgb;
				float2 temp_output_7_0_g11799 = temp_output_1_0_g11762;
				float temp_output_186_0_g11799 = temp_output_39_0_g11762;
				#ifdef _OUTEROUTLINETEXTURETOGGLE_ON
				float3 staticSwitch199_g11799 = ( (tex2D( _OuterOutlineTintTexture, ( temp_output_7_0_g11799 + ( _OuterOutlineTextureSpeed * temp_output_186_0_g11799 ) ) )).rgb * temp_output_82_0_g11799 );
				#else
				float3 staticSwitch199_g11799 = temp_output_82_0_g11799;
				#endif
				float temp_output_182_0_g11799 = ( ( 1.0 - temp_output_15_0_g11799.a ) * min( ( _OuterOutlineFade * 3.0 ) , 1.0 ) );
				#ifdef _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch203_g11799 = 1.0;
				#else
				float staticSwitch203_g11799 = temp_output_182_0_g11799;
				#endif
				float3 lerpResult178_g11799 = lerp( (temp_output_15_0_g11799).rgb , staticSwitch199_g11799 , staticSwitch203_g11799);
				float3 lerpResult170_g11799 = lerp( lerpResult178_g11799 , staticSwitch199_g11799 , staticSwitch203_g11799);
				float linValue16_g11800 = tex2D( _UberNoiseTexture, ( ( ( temp_output_186_0_g11799 * _OuterOutlineNoiseSpeed ) + temp_output_7_0_g11799 ) * _OuterOutlineNoiseScale ) ).r;
				float localMyCustomExpression16_g11800 = MyCustomExpression16_g11800( linValue16_g11800 );
				#ifdef _OUTEROUTLINEDISTORTIONTOGGLE_ON
				float2 staticSwitch157_g11799 = ( ( localMyCustomExpression16_g11800 - 0.5 ) * _OuterOutlineDistortionIntensity );
				#else
				float2 staticSwitch157_g11799 = float2( 0,0 );
				#endif
				float2 temp_output_131_0_g11799 = ( staticSwitch157_g11799 + temp_output_7_0_g11799 );
				float2 appendResult2_g11801 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_25_0_g11799 = ( 100.0 / appendResult2_g11801 );
				float lerpResult168_g11799 = lerp( temp_output_15_0_g11799.a , min( ( max( max( max( max( max( max( max( tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0,-1 ) ) * temp_output_25_0_g11799 ) ) ).a , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0,1 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -1,0 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 1,0 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0.705,0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -0.705,0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0.705,-0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -0.705,-0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) * 3.0 ) , 1.0 ) , _OuterOutlineFade);
				#ifdef _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch200_g11799 = ( temp_output_182_0_g11799 * lerpResult168_g11799 );
				#else
				float staticSwitch200_g11799 = lerpResult168_g11799;
				#endif
				float4 appendResult174_g11799 = (float4(lerpResult170_g11799 , staticSwitch200_g11799));
				float4 staticSwitch13_g11762 = appendResult174_g11799;
				#else
				float4 staticSwitch13_g11762 = staticSwitch12_g11762;
				#endif
				#ifdef _ENABLEPIXELOUTLINE_ON
				float4 temp_output_15_0_g11776 = staticSwitch13_g11762;
				float3 temp_output_82_0_g11776 = (_PixelOutlineColor).rgb;
				float2 temp_output_7_0_g11776 = temp_output_1_0_g11762;
				#ifdef _PIXELOUTLINETEXTURETOGGLE_ON
				float3 staticSwitch199_g11776 = ( (tex2D( _PixelOutlineTintTexture, ( temp_output_7_0_g11776 + ( _PixelOutlineTextureSpeed * temp_output_39_0_g11762 ) ) )).rgb * temp_output_82_0_g11776 );
				#else
				float3 staticSwitch199_g11776 = temp_output_82_0_g11776;
				#endif
				float temp_output_182_0_g11776 = ( ( 1.0 - temp_output_15_0_g11776.a ) * min( ( _PixelOutlineFade * 3.0 ) , 1.0 ) );
				#ifdef _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch203_g11776 = 1.0;
				#else
				float staticSwitch203_g11776 = temp_output_182_0_g11776;
				#endif
				float3 lerpResult178_g11776 = lerp( (temp_output_15_0_g11776).rgb , staticSwitch199_g11776 , staticSwitch203_g11776);
				float3 lerpResult170_g11776 = lerp( lerpResult178_g11776 , staticSwitch199_g11776 , staticSwitch203_g11776);
				float2 appendResult206_g11776 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_209_0_g11776 = ( float2( 1,1 ) / appendResult206_g11776 );
				float lerpResult168_g11776 = lerp( temp_output_15_0_g11776.a , min( ( max( max( max( tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 0,-1 ) ) * temp_output_209_0_g11776 ) ) ).a , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 0,1 ) ) * temp_output_209_0_g11776 ) ) ).a ) , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( -1,0 ) ) * temp_output_209_0_g11776 ) ) ).a ) , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 1,0 ) ) * temp_output_209_0_g11776 ) ) ).a ) * 3.0 ) , 1.0 ) , _PixelOutlineFade);
				#ifdef _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch200_g11776 = ( temp_output_182_0_g11776 * lerpResult168_g11776 );
				#else
				float staticSwitch200_g11776 = lerpResult168_g11776;
				#endif
				float4 appendResult174_g11776 = (float4(lerpResult170_g11776 , staticSwitch200_g11776));
				float4 staticSwitch48_g11762 = appendResult174_g11776;
				#else
				float4 staticSwitch48_g11762 = staticSwitch13_g11762;
				#endif
				#ifdef _ENABLEPINGPONGGLOW_ON
				float3 lerpResult15_g11763 = lerp( (_PingPongGlowFrom).rgb , (_PingPongGlowTo).rgb , ( ( sin( ( temp_output_39_0_g11762 * _PingPongGlowFrequency ) ) + 1.0 ) / 2.0 ));
				float4 temp_output_5_0_g11763 = staticSwitch48_g11762;
				float4 break2_g11764 = temp_output_5_0_g11763;
				float4 appendResult12_g11763 = (float4(( ( lerpResult15_g11763 * _PingPongGlowFade * pow( ( ( break2_g11764.x + break2_g11764.x + break2_g11764.y + break2_g11764.y + break2_g11764.y + break2_g11764.z ) / 6.0 ) , max( _PingPongGlowContrast , 0.001 ) ) ) + (temp_output_5_0_g11763).rgb ) , temp_output_5_0_g11763.a));
				float4 staticSwitch46_g11762 = appendResult12_g11763;
				#else
				float4 staticSwitch46_g11762 = staticSwitch48_g11762;
				#endif
				float4 temp_output_361_0 = staticSwitch46_g11762;
				#ifdef _ENABLEHOLOGRAM_ON
				float4 temp_output_1_0_g11804 = temp_output_361_0;
				float4 break2_g11805 = temp_output_1_0_g11804;
				float4 appendResult22_g11804 = (float4(( (_HologramTint).rgb * pow( ( ( break2_g11805.x + break2_g11805.x + break2_g11805.y + break2_g11805.y + break2_g11805.y + break2_g11805.z ) / 6.0 ) , max( _HologramContrast , 0.001 ) ) ) , ( max( pow( abs( sin( ( ( ( ( shaderTime237 * _HologramLineSpeed ) + ase_worldPos.y ) / unity_OrthoParams.y ) * _HologramLineFrequency ) ) ) , _HologramLineGap ) , _HologramMinAlpha ) * temp_output_1_0_g11804.a )));
				float4 lerpResult37_g11804 = lerp( temp_output_1_0_g11804 , appendResult22_g11804 , hologramFade182);
				float4 staticSwitch56 = lerpResult37_g11804;
				#else
				float4 staticSwitch56 = temp_output_361_0;
				#endif
				#ifdef _ENABLEGLITCH_ON
				float4 temp_output_1_0_g11807 = staticSwitch56;
				float4 break2_g11809 = temp_output_1_0_g11807;
				float temp_output_34_0_g11807 = shaderTime237;
				float linValue16_g11808 = tex2D( _UberNoiseTexture, ( ( glitchPosition154 + ( _GlitchNoiseSpeed * temp_output_34_0_g11807 ) ) * _GlitchNoiseScale ) ).r;
				float localMyCustomExpression16_g11808 = MyCustomExpression16_g11808( linValue16_g11808 );
				float3 hsvTorgb3_g11810 = HSVToRGB( float3(( localMyCustomExpression16_g11808 + ( temp_output_34_0_g11807 * _GlitchHueSpeed ) ),1.0,1.0) );
				float3 lerpResult23_g11807 = lerp( (temp_output_1_0_g11807).rgb , ( ( ( break2_g11809.x + break2_g11809.x + break2_g11809.y + break2_g11809.y + break2_g11809.y + break2_g11809.z ) / 6.0 ) * _GlitchBrightness * hsvTorgb3_g11810 ) , glitchFade152);
				float4 appendResult27_g11807 = (float4(lerpResult23_g11807 , temp_output_1_0_g11807.a));
				float4 staticSwitch57 = appendResult27_g11807;
				#else
				float4 staticSwitch57 = staticSwitch56;
				#endif
				float4 temp_output_3_0_g11811 = staticSwitch57;
				float4 temp_output_1_0_g11841 = temp_output_3_0_g11811;
				float2 temp_output_41_0_g11811 = shaderPosition235;
				float2 temp_output_99_0_g11841 = temp_output_41_0_g11811;
				float temp_output_40_0_g11811 = shaderTime237;
				#ifdef _ENABLECAMOUFLAGE_ON
				float linValue16_g11846 = tex2D( _UberNoiseTexture, ( ( ( temp_output_40_0_g11811 * _CamouflageDistortionSpeed ) + temp_output_99_0_g11841 ) * _CamouflageDistortionScale ) ).r;
				float localMyCustomExpression16_g11846 = MyCustomExpression16_g11846( linValue16_g11846 );
				#ifdef _CAMOUFLAGEANIMATIONTOGGLE_ON
				float2 staticSwitch101_g11841 = ( ( ( localMyCustomExpression16_g11846 - 0.25 ) * _CamouflageDistortionIntensity ) + temp_output_99_0_g11841 );
				#else
				float2 staticSwitch101_g11841 = temp_output_99_0_g11841;
				#endif
				float linValue16_g11843 = tex2D( _UberNoiseTexture, ( staticSwitch101_g11841 * _CamouflageNoiseScaleA ) ).r;
				float localMyCustomExpression16_g11843 = MyCustomExpression16_g11843( linValue16_g11843 );
				float clampResult52_g11841 = clamp( ( ( _CamouflageDensityA - localMyCustomExpression16_g11843 ) / max( _CamouflageSmoothnessA , 0.005 ) ) , 0.0 , 1.0 );
				float4 lerpResult55_g11841 = lerp( _CamouflageBaseColor , ( _CamouflageColorA * clampResult52_g11841 ) , clampResult52_g11841);
				float linValue16_g11845 = tex2D( _UberNoiseTexture, ( ( staticSwitch101_g11841 + float2( 12.3,12.3 ) ) * _CamouflageNoiseScaleB ) ).r;
				float localMyCustomExpression16_g11845 = MyCustomExpression16_g11845( linValue16_g11845 );
				float clampResult65_g11841 = clamp( ( ( _CamouflageDensityB - localMyCustomExpression16_g11845 ) / max( _CamouflageSmoothnessB , 0.005 ) ) , 0.0 , 1.0 );
				float4 lerpResult68_g11841 = lerp( lerpResult55_g11841 , ( _CamouflageColorB * clampResult65_g11841 ) , clampResult65_g11841);
				float4 break2_g11844 = temp_output_1_0_g11841;
				float3 lerpResult4_g11841 = lerp( (temp_output_1_0_g11841).rgb , ( (lerpResult68_g11841).rgb * pow( ( ( break2_g11844.x + break2_g11844.x + break2_g11844.y + break2_g11844.y + break2_g11844.y + break2_g11844.z ) / 6.0 ) , max( _CamouflageContrast , 0.001 ) ) ) , _CamouflageFade);
				float4 appendResult7_g11841 = (float4(lerpResult4_g11841 , temp_output_1_0_g11841.a));
				float4 staticSwitch26_g11811 = appendResult7_g11841;
				#else
				float4 staticSwitch26_g11811 = temp_output_3_0_g11811;
				#endif
				#ifdef _ENABLEMETAL_ON
				float4 temp_output_1_0_g11834 = staticSwitch26_g11811;
				float temp_output_59_0_g11834 = temp_output_40_0_g11811;
				float2 temp_output_58_0_g11834 = temp_output_41_0_g11811;
				float linValue16_g11835 = tex2D( _UberNoiseTexture, ( ( ( temp_output_59_0_g11834 * _MetalNoiseDistortionSpeed ) + temp_output_58_0_g11834 ) * _MetalNoiseDistortionScale ) ).r;
				float localMyCustomExpression16_g11835 = MyCustomExpression16_g11835( linValue16_g11835 );
				float linValue16_g11837 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11835 - 0.25 ) * _MetalNoiseDistortion ) + ( ( temp_output_59_0_g11834 * _MetalNoiseSpeed ) + temp_output_58_0_g11834 ) ) * _MetalNoiseScale ) ).r;
				float localMyCustomExpression16_g11837 = MyCustomExpression16_g11837( linValue16_g11837 );
				float4 break2_g11836 = temp_output_1_0_g11834;
				float temp_output_5_0_g11834 = ( ( break2_g11836.x + break2_g11836.x + break2_g11836.y + break2_g11836.y + break2_g11836.y + break2_g11836.z ) / 6.0 );
				float2 uv_MetalMask = IN.texCoord0.xy * _MetalMask_ST.xy + _MetalMask_ST.zw;
				float4 tex2DNode3_g11839 = tex2D( _MetalMask, uv_MetalMask );
				#ifdef _METALMASKTOGGLE_ON
				float staticSwitch60_g11834 = ( _MetalFade * ( tex2DNode3_g11839.r * tex2DNode3_g11839.a ) );
				#else
				float staticSwitch60_g11834 = _MetalFade;
				#endif
				float4 lerpResult45_g11834 = lerp( temp_output_1_0_g11834 , ( ( max( ( ( _MetalHighlightDensity - localMyCustomExpression16_g11837 ) / max( _MetalHighlightDensity , 0.01 ) ) , 0.0 ) * _MetalHighlightColor * pow( temp_output_5_0_g11834 , max( _MetalHighlightContrast , 0.001 ) ) ) + ( pow( temp_output_5_0_g11834 , max( _MetalContrast , 0.001 ) ) * _MetalColor ) ) , staticSwitch60_g11834);
				float4 appendResult8_g11834 = (float4((lerpResult45_g11834).rgb , (temp_output_1_0_g11834).a));
				float4 staticSwitch28_g11811 = appendResult8_g11834;
				#else
				float4 staticSwitch28_g11811 = staticSwitch26_g11811;
				#endif
				#ifdef _ENABLEFROZEN_ON
				float4 temp_output_1_0_g11826 = staticSwitch28_g11811;
				float4 break2_g11827 = temp_output_1_0_g11826;
				float temp_output_7_0_g11826 = ( ( break2_g11827.x + break2_g11827.x + break2_g11827.y + break2_g11827.y + break2_g11827.y + break2_g11827.z ) / 6.0 );
				float2 temp_output_72_0_g11826 = temp_output_41_0_g11811;
				float linValue16_g11828 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11826 * _FrozenSnowScale ) ).r;
				float localMyCustomExpression16_g11828 = MyCustomExpression16_g11828( linValue16_g11828 );
				float temp_output_73_0_g11826 = temp_output_40_0_g11811;
				float linValue16_g11830 = tex2D( _UberNoiseTexture, ( ( ( temp_output_73_0_g11826 * _FrozenHighlightDistortionSpeed ) + temp_output_72_0_g11826 ) * _FrozenHighlightDistortionScale ) ).r;
				float localMyCustomExpression16_g11830 = MyCustomExpression16_g11830( linValue16_g11830 );
				float linValue16_g11831 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11830 - 0.25 ) * _FrozenHighlightDistortion ) + ( ( temp_output_73_0_g11826 * _FrozenHighlightSpeed ) + temp_output_72_0_g11826 ) ) * _FrozenHighlightScale ) ).r;
				float localMyCustomExpression16_g11831 = MyCustomExpression16_g11831( linValue16_g11831 );
				float3 lerpResult57_g11826 = lerp( (temp_output_1_0_g11826).rgb , ( ( pow( temp_output_7_0_g11826 , max( _FrozenContrast , 0.001 ) ) * (_FrozenTint).rgb ) + ( pow( temp_output_7_0_g11826 , max( _FrozenSnowContrast , 0.001 ) ) * ( (_FrozenSnowColor).rgb * max( ( _FrozenSnowDensity - localMyCustomExpression16_g11828 ) , 0.0 ) ) ) + (( max( ( ( _FrozenHighlightDensity - localMyCustomExpression16_g11831 ) / max( _FrozenHighlightDensity , 0.01 ) ) , 0.0 ) * _FrozenHighlightColor * pow( temp_output_7_0_g11826 , max( _FrozenHighlightContrast , 0.001 ) ) )).rgb ) , _FrozenFade);
				float4 appendResult26_g11826 = (float4(lerpResult57_g11826 , temp_output_1_0_g11826.a));
				float4 staticSwitch29_g11811 = appendResult26_g11826;
				#else
				float4 staticSwitch29_g11811 = staticSwitch28_g11811;
				#endif
				#ifdef _ENABLEBURN_ON
				float4 temp_output_1_0_g11820 = staticSwitch29_g11811;
				float3 temp_output_28_0_g11820 = (temp_output_1_0_g11820).rgb;
				float4 break2_g11824 = float4( temp_output_28_0_g11820 , 0.0 );
				float2 temp_output_72_0_g11820 = temp_output_41_0_g11811;
				float linValue16_g11823 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11820 * _BurnSwirlNoiseScale ) ).r;
				float localMyCustomExpression16_g11823 = MyCustomExpression16_g11823( linValue16_g11823 );
				float linValue16_g11821 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11823 - 0.5 ) * float2( 1,1 ) * _BurnSwirlFactor ) + temp_output_72_0_g11820 ) * _BurnInsideNoiseScale ) ).r;
				float localMyCustomExpression16_g11821 = MyCustomExpression16_g11821( linValue16_g11821 );
				float clampResult68_g11820 = clamp( ( _BurnInsideNoiseFactor - localMyCustomExpression16_g11821 ) , 0.0 , 1.0 );
				float linValue16_g11822 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11820 * _BurnEdgeNoiseScale ) ).r;
				float localMyCustomExpression16_g11822 = MyCustomExpression16_g11822( linValue16_g11822 );
				float temp_output_15_0_g11820 = ( ( ( _BurnRadius - distance( temp_output_72_0_g11820 , _BurnPosition ) ) + ( localMyCustomExpression16_g11822 * _BurnEdgeNoiseFactor ) ) / max( _BurnWidth , 0.01 ) );
				float clampResult18_g11820 = clamp( temp_output_15_0_g11820 , 0.0 , 1.0 );
				float3 lerpResult29_g11820 = lerp( temp_output_28_0_g11820 , ( pow( ( ( break2_g11824.x + break2_g11824.x + break2_g11824.y + break2_g11824.y + break2_g11824.y + break2_g11824.z ) / 6.0 ) , max( _BurnInsideContrast , 0.001 ) ) * ( ( (_BurnInsideNoiseColor).rgb * clampResult68_g11820 ) + (_BurnInsideColor).rgb ) ) , clampResult18_g11820);
				float3 lerpResult40_g11820 = lerp( temp_output_28_0_g11820 , ( lerpResult29_g11820 + ( ( step( temp_output_15_0_g11820 , 1.0 ) * step( 0.0 , temp_output_15_0_g11820 ) ) * (_BurnEdgeColor).rgb ) ) , _BurnFade);
				float4 appendResult43_g11820 = (float4(lerpResult40_g11820 , temp_output_1_0_g11820.a));
				float4 staticSwitch32_g11811 = appendResult43_g11820;
				#else
				float4 staticSwitch32_g11811 = staticSwitch29_g11811;
				#endif
				#ifdef _ENABLERAINBOW_ON
				float2 temp_output_42_0_g11816 = temp_output_41_0_g11811;
				float linValue16_g11817 = tex2D( _UberNoiseTexture, ( temp_output_42_0_g11816 * _RainbowNoiseScale ) ).r;
				float localMyCustomExpression16_g11817 = MyCustomExpression16_g11817( linValue16_g11817 );
				float3 hsvTorgb3_g11819 = HSVToRGB( float3(( ( ( distance( temp_output_42_0_g11816 , _RainbowCenter ) + ( localMyCustomExpression16_g11817 * _RainbowNoiseFactor ) ) * _RainbowDensity ) + ( _RainbowSpeed * temp_output_40_0_g11811 ) ),1.0,1.0) );
				float3 hsvTorgb36_g11816 = RGBToHSV( hsvTorgb3_g11819 );
				float3 hsvTorgb37_g11816 = HSVToRGB( float3(hsvTorgb36_g11816.x,_RainbowSaturation,( hsvTorgb36_g11816.z * _RainbowBrightness )) );
				float4 temp_output_1_0_g11816 = staticSwitch32_g11811;
				float4 break2_g11818 = temp_output_1_0_g11816;
				float saferPower24_g11816 = abs( ( ( break2_g11818.x + break2_g11818.x + break2_g11818.y + break2_g11818.y + break2_g11818.y + break2_g11818.z ) / 6.0 ) );
				float4 appendResult29_g11816 = (float4(( ( hsvTorgb37_g11816 * pow( saferPower24_g11816 , max( _RainbowContrast , 0.001 ) ) * _RainbowFade ) + (temp_output_1_0_g11816).rgb ) , temp_output_1_0_g11816.a));
				float4 staticSwitch34_g11811 = appendResult29_g11816;
				#else
				float4 staticSwitch34_g11811 = staticSwitch32_g11811;
				#endif
				#ifdef _ENABLESHINE_ON
				float4 temp_output_1_0_g11812 = staticSwitch34_g11811;
				float3 temp_output_57_0_g11812 = (temp_output_1_0_g11812).rgb;
				float4 break2_g11814 = temp_output_1_0_g11812;
				float3 temp_cast_69 = (( ( break2_g11814.x + break2_g11814.x + break2_g11814.y + break2_g11814.y + break2_g11814.y + break2_g11814.z ) / 6.0 )).xxx;
				float3 lerpResult92_g11812 = lerp( temp_cast_69 , temp_output_57_0_g11812 , _ShineSaturation);
				float3 temp_cast_70 = (max( max( _ShineContrast , 0.001 ) , 0.001 )).xxx;
				float3 rotatedValue69_g11812 = RotateAroundAxis( float3( 0,0,0 ), float3( ( _ShineFrequency * temp_output_41_0_g11811 ) ,  0.0 ), float3( 0,0,1 ), ( ( _ShineRotation / 180.0 ) * PI ) );
				float temp_output_103_0_g11812 = ( _ShineFrequency * _ShineWidth );
				float clampResult80_g11812 = clamp( ( ( ( sin( ( rotatedValue69_g11812.x - ( temp_output_40_0_g11811 * _ShineSpeed * _ShineFrequency ) ) ) - ( 1.0 - temp_output_103_0_g11812 ) ) / temp_output_103_0_g11812 ) * _ShineSmooth ) , 0.0 , 1.0 );
				float2 uv_ShineMask = IN.texCoord0.xy * _ShineMask_ST.xy + _ShineMask_ST.zw;
				float4 tex2DNode3_g11813 = tex2D( _ShineMask, uv_ShineMask );
				#ifdef _SHINEMASKTOGGLE_ON
				float staticSwitch98_g11812 = ( _ShineFade * ( tex2DNode3_g11813.r * tex2DNode3_g11813.a ) );
				#else
				float staticSwitch98_g11812 = _ShineFade;
				#endif
				float4 appendResult8_g11812 = (float4(( temp_output_57_0_g11812 + ( ( pow( lerpResult92_g11812 , temp_cast_70 ) * (_ShineColor).rgb ) * clampResult80_g11812 * staticSwitch98_g11812 ) ) , (temp_output_1_0_g11812).a));
				float4 staticSwitch36_g11811 = appendResult8_g11812;
				#else
				float4 staticSwitch36_g11811 = staticSwitch34_g11811;
				#endif
				#ifdef _ENABLEPOISON_ON
				float temp_output_41_0_g11847 = temp_output_40_0_g11811;
				float linValue16_g11849 = tex2D( _UberNoiseTexture, ( ( ( temp_output_41_0_g11847 * _PoisonNoiseSpeed ) + temp_output_41_0_g11811 ) * _PoisonNoiseScale ) ).r;
				float localMyCustomExpression16_g11849 = MyCustomExpression16_g11849( linValue16_g11849 );
				float3 temp_output_24_0_g11847 = (_PoisonColor).rgb;
				float4 temp_output_1_0_g11847 = staticSwitch36_g11811;
				float3 temp_output_28_0_g11847 = (temp_output_1_0_g11847).rgb;
				float4 break2_g11848 = float4( temp_output_28_0_g11847 , 0.0 );
				float3 lerpResult32_g11847 = lerp( temp_output_28_0_g11847 , ( temp_output_24_0_g11847 * ( ( break2_g11848.x + break2_g11848.x + break2_g11848.y + break2_g11848.y + break2_g11848.y + break2_g11848.z ) / 6.0 ) ) , ( _PoisonFade * _PoisonRecolorFactor ));
				float4 appendResult27_g11847 = (float4(( ( max( pow( abs( ( ( ( localMyCustomExpression16_g11849 + ( temp_output_41_0_g11847 * _PoisonShiftSpeed ) ) % 1.0 ) + -0.5 ) ) , max( _PoisonDensity , 0.001 ) ) , 0.0 ) * temp_output_24_0_g11847 * _PoisonFade * _PoisonNoiseBrightness ) + lerpResult32_g11847 ) , temp_output_1_0_g11847.a));
				float4 staticSwitch39_g11811 = appendResult27_g11847;
				#else
				float4 staticSwitch39_g11811 = staticSwitch36_g11811;
				#endif
				float4 temp_output_10_0_g11851 = staticSwitch39_g11811;
				#ifdef _ENABLEENCHANTED_ON
				float3 temp_output_12_0_g11851 = (temp_output_10_0_g11851).rgb;
				float2 temp_output_2_0_g11851 = temp_output_41_0_g11811;
				float temp_output_1_0_g11851 = temp_output_40_0_g11811;
				float2 temp_output_6_0_g11851 = ( temp_output_1_0_g11851 * _EnchantedSpeed );
				float linValue16_g11854 = tex2D( _UberNoiseTexture, ( ( ( temp_output_2_0_g11851 - ( ( temp_output_6_0_g11851 + float2( 1.234,5.6789 ) ) * float2( 0.95,1.05 ) ) ) * _EnchantedScale ) * float2( 1,1 ) ) ).r;
				float localMyCustomExpression16_g11854 = MyCustomExpression16_g11854( linValue16_g11854 );
				float linValue16_g11852 = tex2D( _UberNoiseTexture, ( ( ( temp_output_6_0_g11851 + temp_output_2_0_g11851 ) * _EnchantedScale ) * float2( 1,1 ) ) ).r;
				float localMyCustomExpression16_g11852 = MyCustomExpression16_g11852( linValue16_g11852 );
				float temp_output_36_0_g11851 = ( localMyCustomExpression16_g11854 + localMyCustomExpression16_g11852 );
				float temp_output_43_0_g11851 = ( temp_output_36_0_g11851 * 0.5 );
				float3 lerpResult42_g11851 = lerp( (_EnchantedLowColor).rgb , (_EnchantedHighColor).rgb , temp_output_43_0_g11851);
				float3 hsvTorgb53_g11851 = HSVToRGB( float3(( ( temp_output_43_0_g11851 * _EnchantedRainbowDensity ) + ( _EnchantedRainbowSpeed * temp_output_1_0_g11851 ) ),_EnchantedRainbowSaturation,1.0) );
				#ifdef _ENCHANTEDRAINBOWTOGGLE_ON
				float3 staticSwitch50_g11851 = hsvTorgb53_g11851;
				#else
				float3 staticSwitch50_g11851 = lerpResult42_g11851;
				#endif
				float4 break2_g11853 = temp_output_10_0_g11851;
				float3 temp_output_40_0_g11851 = ( staticSwitch50_g11851 * pow( ( ( break2_g11853.x + break2_g11853.x + break2_g11853.y + break2_g11853.y + break2_g11853.y + break2_g11853.z ) / 6.0 ) , max( _EnchantedContrast , 0.001 ) ) * _EnchantedBrightness );
				float temp_output_45_0_g11851 = ( max( ( temp_output_36_0_g11851 - _EnchantedReduce ) , 0.0 ) * _EnchantedFade );
				float3 lerpResult44_g11851 = lerp( temp_output_12_0_g11851 , temp_output_40_0_g11851 , temp_output_45_0_g11851);
				#ifdef _ENCHANTEDLERPTOGGLE_ON
				float3 staticSwitch47_g11851 = lerpResult44_g11851;
				#else
				float3 staticSwitch47_g11851 = ( temp_output_12_0_g11851 + ( temp_output_40_0_g11851 * temp_output_45_0_g11851 ) );
				#endif
				float4 appendResult19_g11851 = (float4(staticSwitch47_g11851 , temp_output_10_0_g11851.a));
				float4 staticSwitch11_g11851 = appendResult19_g11851;
				#else
				float4 staticSwitch11_g11851 = temp_output_10_0_g11851;
				#endif
				float4 temp_output_1_0_g11856 = staticSwitch11_g11851;
				#ifdef _ENABLESHIFTING_ON
				float4 break5_g11856 = temp_output_1_0_g11856;
				float3 appendResult32_g11856 = (float3(break5_g11856.r , break5_g11856.g , break5_g11856.b));
				float4 break2_g11857 = temp_output_1_0_g11856;
				float temp_output_4_0_g11856 = ( ( break2_g11857.x + break2_g11857.x + break2_g11857.y + break2_g11857.y + break2_g11857.y + break2_g11857.z ) / 6.0 );
				float temp_output_11_0_g11856 = ( ( ( temp_output_4_0_g11856 + ( temp_output_40_0_g11811 * _ShiftingSpeed ) ) * _ShiftingDensity ) % 1.0 );
				float3 lerpResult20_g11856 = lerp( (_ShiftingColorA).rgb , (_ShiftingColorB).rgb , ( abs( ( temp_output_11_0_g11856 - 0.5 ) ) * 2.0 ));
				float3 hsvTorgb12_g11856 = HSVToRGB( float3(temp_output_11_0_g11856,_ShiftingSaturation,_ShiftingBrightness) );
				#ifdef _SHIFTINGRAINBOWTOGGLE_ON
				float3 staticSwitch26_g11856 = hsvTorgb12_g11856;
				#else
				float3 staticSwitch26_g11856 = ( lerpResult20_g11856 * _ShiftingBrightness );
				#endif
				float3 lerpResult31_g11856 = lerp( appendResult32_g11856 , ( staticSwitch26_g11856 * pow( temp_output_4_0_g11856 , max( _ShiftingContrast , 0.001 ) ) ) , _ShiftingFade);
				float4 appendResult6_g11856 = (float4(lerpResult31_g11856 , break5_g11856.a));
				float4 staticSwitch33_g11856 = appendResult6_g11856;
				#else
				float4 staticSwitch33_g11856 = temp_output_1_0_g11856;
				#endif
				float4 temp_output_5_0_g11863 = staticSwitch33_g11856;
				#ifdef _ENABLETEXTURELAYER1_ON
				float4 break6_g11863 = temp_output_5_0_g11863;
				float3 appendResult11_g11863 = (float3(break6_g11863.r , break6_g11863.g , break6_g11863.b));
				float temp_output_27_0_g11863 = temp_output_40_0_g11811;
				#ifdef _TEXTURELAYER1SCROLLTOGGLE_ON
				float2 staticSwitch73_g11863 = ( _TextureLayer1Offset + ( _TextureLayer1ScrollSpeed * temp_output_27_0_g11863 ) );
				#else
				float2 staticSwitch73_g11863 = _TextureLayer1Offset;
				#endif
				float2 temp_output_72_0_g11863 = ( ( _TextureLayer1Scale * temp_output_41_0_g11811 ) - staticSwitch73_g11863 );
				float2 temp_cast_79 = (_TextureLayer1EdgeClip).xx;
				float2 UV10_g11864 = ( ( ( ( ( ( ( ( ( temp_output_72_0_g11863 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) ) - float2( 0.5,0.5 ) ) * ( float2( 1,1 ) - temp_cast_79 ) ) + float2( 0.5,0.5 ) ) % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				int Width10_g11864 = _TextureLayer1Columns;
				int Height10_g11864 = _TextureLayer1Rows;
				float Tile10_g11864 = round( ( ( _TextureLayer1Speed * temp_output_27_0_g11863 ) + _TextureLayer1StartFrame ) );
				float2 Invert10_g11864 = float2( 0,1 );
				float2 localMyCustomExpression10_g11864 = MyCustomExpression10_g11864( UV10_g11864 , Width10_g11864 , Height10_g11864 , Tile10_g11864 , Invert10_g11864 );
				#ifdef _TEXTURELAYER1SHEETTOGGLE_ON
				float2 staticSwitch20_g11863 = localMyCustomExpression10_g11864;
				#else
				float2 staticSwitch20_g11863 = temp_output_72_0_g11863;
				#endif
				float4 tex2DNode3_g11863 = tex2D( _TextureLayer1Texture, staticSwitch20_g11863 );
				float3 appendResult13_g11863 = (float3(tex2DNode3_g11863.r , tex2DNode3_g11863.g , tex2DNode3_g11863.b));
				float3 appendResult18_g11863 = (float3(_TextureLayer1Color.r , _TextureLayer1Color.g , _TextureLayer1Color.b));
				float3 temp_output_16_0_g11863 = ( appendResult13_g11863 * appendResult18_g11863 );
				float4 break2_g11865 = temp_output_5_0_g11863;
				#ifdef _TEXTURELAYER1CONTRASTTOGGLE_ON
				float3 staticSwitch80_g11863 = ( pow( ( ( break2_g11865.x + break2_g11865.x + break2_g11865.y + break2_g11865.y + break2_g11865.y + break2_g11865.z ) / 6.0 ) , max( _TextureLayer1Contrast , 0.001 ) ) * temp_output_16_0_g11863 );
				#else
				float3 staticSwitch80_g11863 = temp_output_16_0_g11863;
				#endif
				float3 lerpResult12_g11863 = lerp( appendResult11_g11863 , staticSwitch80_g11863 , ( tex2DNode3_g11863.a * _TextureLayer1Fade ));
				float4 appendResult14_g11863 = (float4(lerpResult12_g11863 , break6_g11863.a));
				float4 staticSwitch15_g11863 = appendResult14_g11863;
				#else
				float4 staticSwitch15_g11863 = temp_output_5_0_g11863;
				#endif
				float4 temp_output_5_0_g11859 = staticSwitch15_g11863;
				#ifdef _ENABLETEXTURELAYER2_ON
				float4 break6_g11859 = temp_output_5_0_g11859;
				float3 appendResult11_g11859 = (float3(break6_g11859.r , break6_g11859.g , break6_g11859.b));
				float temp_output_27_0_g11859 = temp_output_40_0_g11811;
				#ifdef _TEXTURELAYER2SCROLLTOGGLE_ON
				float2 staticSwitch73_g11859 = ( _TextureLayer2Offset + ( _TextureLayer2ScrollSpeed * temp_output_27_0_g11859 ) );
				#else
				float2 staticSwitch73_g11859 = _TextureLayer2Offset;
				#endif
				float2 temp_output_72_0_g11859 = ( ( _TextureLayer2Scale * temp_output_41_0_g11811 ) - staticSwitch73_g11859 );
				float2 temp_cast_82 = (_TextureLayer2EdgeClip).xx;
				float2 UV10_g11860 = ( ( ( ( ( ( ( ( ( temp_output_72_0_g11859 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) ) - float2( 0.5,0.5 ) ) * ( float2( 1,1 ) - temp_cast_82 ) ) + float2( 0.5,0.5 ) ) % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				int Width10_g11860 = _TextureLayer2Columns;
				int Height10_g11860 = _TextureLayer2Rows;
				float Tile10_g11860 = round( ( ( _TextureLayer2Speed * temp_output_27_0_g11859 ) + _TextureLayer2StartFrame ) );
				float2 Invert10_g11860 = float2( 0,1 );
				float2 localMyCustomExpression10_g11860 = MyCustomExpression10_g11860( UV10_g11860 , Width10_g11860 , Height10_g11860 , Tile10_g11860 , Invert10_g11860 );
				#ifdef _TEXTURELAYER2SHEETTOGGLE_ON
				float2 staticSwitch20_g11859 = localMyCustomExpression10_g11860;
				#else
				float2 staticSwitch20_g11859 = temp_output_72_0_g11859;
				#endif
				float4 tex2DNode3_g11859 = tex2D( _TextureLayer2Texture, staticSwitch20_g11859 );
				float3 appendResult13_g11859 = (float3(tex2DNode3_g11859.r , tex2DNode3_g11859.g , tex2DNode3_g11859.b));
				float3 appendResult18_g11859 = (float3(_TextureLayer2Color.r , _TextureLayer2Color.g , _TextureLayer2Color.b));
				float3 temp_output_16_0_g11859 = ( appendResult13_g11859 * appendResult18_g11859 );
				float4 break2_g11861 = temp_output_5_0_g11859;
				#ifdef _TEXTURELAYER2CONTRASTTOGGLE_ON
				float3 staticSwitch84_g11859 = ( pow( ( ( break2_g11861.x + break2_g11861.x + break2_g11861.y + break2_g11861.y + break2_g11861.y + break2_g11861.z ) / 6.0 ) , max( _TextureLayer2Contrast , 0.001 ) ) * temp_output_16_0_g11859 );
				#else
				float3 staticSwitch84_g11859 = temp_output_16_0_g11859;
				#endif
				float3 lerpResult12_g11859 = lerp( appendResult11_g11859 , staticSwitch84_g11859 , ( tex2DNode3_g11859.a * _TextureLayer2Fade ));
				float4 appendResult14_g11859 = (float4(lerpResult12_g11859 , break6_g11859.a));
				float4 staticSwitch15_g11859 = appendResult14_g11859;
				#else
				float4 staticSwitch15_g11859 = temp_output_5_0_g11859;
				#endif
				float4 temp_output_473_0 = staticSwitch15_g11859;
				#ifdef _ENABLEFULLDISTORTION_ON
				float4 break4_g11867 = temp_output_473_0;
				float fullDistortionAlpha164 = _FullDistortionFade;
				float4 appendResult5_g11867 = (float4(break4_g11867.r , break4_g11867.g , break4_g11867.b , ( break4_g11867.a * fullDistortionAlpha164 )));
				float4 staticSwitch77 = appendResult5_g11867;
				#else
				float4 staticSwitch77 = temp_output_473_0;
				#endif
				#ifdef _ENABLEDIRECTIONALDISTORTION_ON
				float4 break4_g11868 = staticSwitch77;
				float directionalDistortionAlpha167 = (( _DirectionalDistortionInvert )?( ( 1.0 - clampResult154_g11669 ) ):( clampResult154_g11669 ));
				float4 appendResult5_g11868 = (float4(break4_g11868.r , break4_g11868.g , break4_g11868.b , ( break4_g11868.a * directionalDistortionAlpha167 )));
				float4 staticSwitch75 = appendResult5_g11868;
				#else
				float4 staticSwitch75 = staticSwitch77;
				#endif
				float4 temp_output_1_0_g11869 = staticSwitch75;
				float4 temp_output_1_0_g11870 = temp_output_1_0_g11869;
#ifdef _ENABLEFULLALPHADISSOLVE_ON
				float temp_output_53_0_g11870 = max( _FullAlphaDissolveWidth , 0.001 );
				#endif
				float2 temp_output_18_0_g11869 = shaderPosition235;
				#ifdef _ENABLEFULLALPHADISSOLVE_ON
				float linValue16_g11871 = tex2D( _UberNoiseTexture, ( temp_output_18_0_g11869 * _FullAlphaDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11871 = MyCustomExpression16_g11871( linValue16_g11871 );
				float clampResult17_g11870 = clamp( ( ( ( _FullAlphaDissolveFade * ( 1.0 + temp_output_53_0_g11870 ) ) - localMyCustomExpression16_g11871 ) / temp_output_53_0_g11870 ) , 0.0 , 1.0 );
				float4 appendResult3_g11870 = (float4((temp_output_1_0_g11870).rgb , ( temp_output_1_0_g11870.a * clampResult17_g11870 )));
				float4 staticSwitch3_g11869 = appendResult3_g11870;
				#else
				float4 staticSwitch3_g11869 = temp_output_1_0_g11869;
				#endif
				#ifdef _ENABLEFULLGLOWDISSOLVE_ON
				float linValue16_g11879 = tex2D( _UberNoiseTexture, ( temp_output_18_0_g11869 * _FullGlowDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11879 = MyCustomExpression16_g11879( linValue16_g11879 );
				float temp_output_5_0_g11878 = localMyCustomExpression16_g11879;
				float temp_output_61_0_g11878 = step( temp_output_5_0_g11878 , _FullGlowDissolveFade );
				float temp_output_53_0_g11878 = max( ( _FullGlowDissolveFade * _FullGlowDissolveWidth ) , 0.001 );
				float4 temp_output_1_0_g11878 = staticSwitch3_g11869;
				float4 appendResult3_g11878 = (float4(( ( (_FullGlowDissolveEdgeColor).rgb * ( temp_output_61_0_g11878 - step( temp_output_5_0_g11878 , ( ( _FullGlowDissolveFade * ( 1.01 + temp_output_53_0_g11878 ) ) - temp_output_53_0_g11878 ) ) ) ) + (temp_output_1_0_g11878).rgb ) , ( temp_output_1_0_g11878.a * temp_output_61_0_g11878 )));
				float4 staticSwitch5_g11869 = appendResult3_g11878;
				#else
				float4 staticSwitch5_g11869 = staticSwitch3_g11869;
				#endif
				#ifdef _ENABLESOURCEALPHADISSOLVE_ON
				float4 temp_output_1_0_g11880 = staticSwitch5_g11869;
				float2 temp_output_76_0_g11880 = temp_output_18_0_g11869;
				float linValue16_g11881 = tex2D( _UberNoiseTexture, ( temp_output_76_0_g11880 * _SourceAlphaDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11881 = MyCustomExpression16_g11881( linValue16_g11881 );
				float clampResult17_g11880 = clamp( ( ( _SourceAlphaDissolveFade - ( distance( _SourceAlphaDissolvePosition , temp_output_76_0_g11880 ) + ( localMyCustomExpression16_g11881 * _SourceAlphaDissolveNoiseFactor ) ) ) / max( _SourceAlphaDissolveWidth , 0.001 ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11880 = (float4((temp_output_1_0_g11880).rgb , ( temp_output_1_0_g11880.a * (( _SourceAlphaDissolveInvert )?( ( 1.0 - clampResult17_g11880 ) ):( clampResult17_g11880 )) )));
				float4 staticSwitch8_g11869 = appendResult3_g11880;
				#else
				float4 staticSwitch8_g11869 = staticSwitch5_g11869;
				#endif
				#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
				float2 temp_output_90_0_g11876 = temp_output_18_0_g11869;
				float linValue16_g11877 = tex2D( _UberNoiseTexture, ( temp_output_90_0_g11876 * _SourceGlowDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11877 = MyCustomExpression16_g11877( linValue16_g11877 );
				float temp_output_65_0_g11876 = ( distance( _SourceGlowDissolvePosition , temp_output_90_0_g11876 ) + ( localMyCustomExpression16_g11877 * _SourceGlowDissolveNoiseFactor ) );
				float temp_output_75_0_g11876 = step( temp_output_65_0_g11876 , _SourceGlowDissolveFade );
				float temp_output_76_0_g11876 = step( temp_output_65_0_g11876 , ( _SourceGlowDissolveFade - max( _SourceGlowDissolveWidth , 0.001 ) ) );
				float4 temp_output_1_0_g11876 = staticSwitch8_g11869;
				float4 appendResult3_g11876 = (float4(( ( max( ( temp_output_75_0_g11876 - temp_output_76_0_g11876 ) , 0.0 ) * (_SourceGlowDissolveEdgeColor).rgb ) + (temp_output_1_0_g11876).rgb ) , ( temp_output_1_0_g11876.a * (( _SourceGlowDissolveInvert )?( ( 1.0 - temp_output_76_0_g11876 ) ):( temp_output_75_0_g11876 )) )));
				float4 staticSwitch9_g11869 = appendResult3_g11876;
				#else
				float4 staticSwitch9_g11869 = staticSwitch8_g11869;
				#endif
				#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
				float4 temp_output_1_0_g11872 = staticSwitch9_g11869;
				float2 temp_output_161_0_g11872 = temp_output_18_0_g11869;
				float3 rotatedValue136_g11872 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_161_0_g11872 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalAlphaFadeRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11872 = rotatedValue136_g11872;
				float linValue16_g11873 = tex2D( _UberNoiseTexture, ( temp_output_161_0_g11872 * _DirectionalAlphaFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11873 = MyCustomExpression16_g11873( linValue16_g11873 );
				float clampResult154_g11872 = clamp( ( ( break130_g11872.x + break130_g11872.y + _DirectionalAlphaFadeFade + ( localMyCustomExpression16_g11873 * _DirectionalAlphaFadeNoiseFactor ) ) / max( _DirectionalAlphaFadeWidth , 0.001 ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11872 = (float4((temp_output_1_0_g11872).rgb , ( temp_output_1_0_g11872.a * (( _DirectionalAlphaFadeInvert )?( ( 1.0 - clampResult154_g11872 ) ):( clampResult154_g11872 )) )));
				float4 staticSwitch11_g11869 = appendResult3_g11872;
				#else
				float4 staticSwitch11_g11869 = staticSwitch9_g11869;
				#endif
				#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
				float2 temp_output_171_0_g11874 = temp_output_18_0_g11869;
				float3 rotatedValue136_g11874 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_171_0_g11874 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalGlowFadeRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11874 = rotatedValue136_g11874;
				float linValue16_g11875 = tex2D( _UberNoiseTexture, ( temp_output_171_0_g11874 * _DirectionalGlowFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11875 = MyCustomExpression16_g11875( linValue16_g11875 );
				float temp_output_168_0_g11874 = max( ( ( break130_g11874.x + break130_g11874.y + _DirectionalGlowFadeFade + ( localMyCustomExpression16_g11875 * _DirectionalGlowFadeNoiseFactor ) ) / max( _DirectionalGlowFadeWidth , 0.001 ) ) , 0.0 );
				float temp_output_161_0_g11874 = step( 0.1 , (( _DirectionalGlowFadeInvert )?( ( 1.0 - temp_output_168_0_g11874 ) ):( temp_output_168_0_g11874 )) );
				float4 temp_output_1_0_g11874 = staticSwitch11_g11869;
				float clampResult154_g11874 = clamp( temp_output_161_0_g11874 , 0.0 , 1.0 );
				float4 appendResult3_g11874 = (float4(( ( (_DirectionalGlowFadeEdgeColor).rgb * ( temp_output_161_0_g11874 - step( 1.0 , (( _DirectionalGlowFadeInvert )?( ( 1.0 - temp_output_168_0_g11874 ) ):( temp_output_168_0_g11874 )) ) ) ) + (temp_output_1_0_g11874).rgb ) , ( temp_output_1_0_g11874.a * clampResult154_g11874 )));
				float4 staticSwitch15_g11869 = appendResult3_g11874;
				#else
				float4 staticSwitch15_g11869 = staticSwitch11_g11869;
				#endif
				#ifdef _ENABLEHALFTONE_ON
				float4 temp_output_1_0_g11882 = staticSwitch15_g11869;
				float2 temp_output_126_0_g11882 = temp_output_18_0_g11869;
				float temp_output_121_0_g11882 = max( ( ( _HalftoneFade - distance( _HalftonePosition , temp_output_126_0_g11882 ) ) / max( 0.01 , _HalftoneFadeWidth ) ) , 0.0 );
				float2 appendResult11_g11883 = (float2(temp_output_121_0_g11882 , temp_output_121_0_g11882));
				float temp_output_17_0_g11883 = length( ( (( ( abs( temp_output_126_0_g11882 ) * _HalftoneTiling ) % float2( 1,1 ) )*2.0 + -1.0) / appendResult11_g11883 ) );
				float clampResult17_g11882 = clamp( saturate( ( ( 1.0 - temp_output_17_0_g11883 ) / fwidth( temp_output_17_0_g11883 ) ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11882 = (float4((temp_output_1_0_g11882).rgb , ( temp_output_1_0_g11882.a * (( _HalftoneInvert )?( ( 1.0 - clampResult17_g11882 ) ):( clampResult17_g11882 )) )));
				float4 staticSwitch13_g11869 = appendResult3_g11882;
				#else
				float4 staticSwitch13_g11869 = staticSwitch15_g11869;
				#endif
				#ifdef _ENABLEADDCOLOR_ON
				float3 temp_output_3_0_g11885 = (_AddColorColor).rgb;
				float2 uv_AddColorMask = IN.texCoord0.xy * _AddColorMask_ST.xy + _AddColorMask_ST.zw;
				float4 tex2DNode19_g11885 = tex2D( _AddColorMask, uv_AddColorMask );
				#ifdef _ADDCOLORMASKTOGGLE_ON
				float3 staticSwitch16_g11885 = ( temp_output_3_0_g11885 * ( (tex2DNode19_g11885).rgb * tex2DNode19_g11885.a ) );
				#else
				float3 staticSwitch16_g11885 = temp_output_3_0_g11885;
				#endif
				float4 temp_output_1_0_g11885 = staticSwitch13_g11869;
				float4 break2_g11887 = temp_output_1_0_g11885;
				#ifdef _ADDCOLORCONTRASTTOGGLE_ON
				float3 staticSwitch17_g11885 = ( staticSwitch16_g11885 * pow( ( ( break2_g11887.x + break2_g11887.x + break2_g11887.y + break2_g11887.y + break2_g11887.y + break2_g11887.z ) / 6.0 ) , max( _AddColorContrast , 0.001 ) ) );
				#else
				float3 staticSwitch17_g11885 = staticSwitch16_g11885;
				#endif
				float4 appendResult6_g11885 = (float4(( ( staticSwitch17_g11885 * _AddColorFade ) + (temp_output_1_0_g11885).rgb ) , temp_output_1_0_g11885.a));
				float4 staticSwitch5_g11884 = appendResult6_g11885;
				#else
				float4 staticSwitch5_g11884 = staticSwitch13_g11869;
				#endif
				#ifdef _ENABLEALPHATINT_ON
				float4 temp_output_1_0_g11888 = staticSwitch5_g11884;
				float3 lerpResult4_g11888 = lerp( (temp_output_1_0_g11888).rgb , (_AlphaTintColor).rgb , ( ( 1.0 - temp_output_1_0_g11888.a ) * step( _AlphaTintMinAlpha , temp_output_1_0_g11888.a ) * _AlphaTintFade ));
				float4 appendResult13_g11888 = (float4(lerpResult4_g11888 , temp_output_1_0_g11888.a));
				float4 staticSwitch11_g11884 = appendResult13_g11888;
				#else
				float4 staticSwitch11_g11884 = staticSwitch5_g11884;
				#endif
				#ifdef _ENABLESTRONGTINT_ON
				float4 temp_output_1_0_g11889 = staticSwitch11_g11884;
				float3 temp_output_6_0_g11889 = (_StrongTintTint).rgb;
				float2 uv_StrongTintMask = IN.texCoord0.xy * _StrongTintMask_ST.xy + _StrongTintMask_ST.zw;
				float4 tex2DNode23_g11889 = tex2D( _StrongTintMask, uv_StrongTintMask );
				#ifdef _STRONGTINTMASKTOGGLE_ON
				float3 staticSwitch21_g11889 = ( temp_output_6_0_g11889 * ( (tex2DNode23_g11889).rgb * tex2DNode23_g11889.a ) );
				#else
				float3 staticSwitch21_g11889 = temp_output_6_0_g11889;
				#endif
				float4 break2_g11891 = temp_output_1_0_g11889;
				#ifdef _STRONGTINTCONTRASTTOGGLE_ON
				float3 staticSwitch22_g11889 = ( pow( ( ( break2_g11891.x + break2_g11891.x + break2_g11891.y + break2_g11891.y + break2_g11891.y + break2_g11891.z ) / 6.0 ) , max( _StrongTintContrast , 0.001 ) ) * staticSwitch21_g11889 );
				#else
				float3 staticSwitch22_g11889 = staticSwitch21_g11889;
				#endif
				float3 lerpResult7_g11889 = lerp( (temp_output_1_0_g11889).rgb , staticSwitch22_g11889 , _StrongTintFade);
				float4 appendResult9_g11889 = (float4(lerpResult7_g11889 , (temp_output_1_0_g11889).a));
				float4 staticSwitch7_g11884 = appendResult9_g11889;
				#else
				float4 staticSwitch7_g11884 = staticSwitch11_g11884;
				#endif
				float4 temp_output_2_0_g11892 = staticSwitch7_g11884;
				#ifdef _ENABLESHADOW_ON
				float4 break4_g11894 = temp_output_2_0_g11892;
				float3 appendResult5_g11894 = (float3(break4_g11894.r , break4_g11894.g , break4_g11894.b));
				float2 appendResult2_g11893 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float4 appendResult85_g11892 = (float4(_ShadowColor.r , _ShadowColor.g , _ShadowColor.b , ( _ShadowFade * tex2D( _MainTex, ( finalUV146 - ( ( 100.0 / appendResult2_g11893 ) * _ShadowOffset ) ) ).a )));
				float4 break6_g11894 = appendResult85_g11892;
				float3 appendResult7_g11894 = (float3(break6_g11894.r , break6_g11894.g , break6_g11894.b));
				float temp_output_11_0_g11894 = ( ( 1.0 - break4_g11894.a ) * break6_g11894.a );
				float temp_output_32_0_g11894 = ( break4_g11894.a + temp_output_11_0_g11894 );
				float4 appendResult18_g11894 = (float4(( ( ( appendResult5_g11894 * break4_g11894.a ) + ( appendResult7_g11894 * temp_output_11_0_g11894 ) ) * ( 1.0 / max( temp_output_32_0_g11894 , 0.01 ) ) ) , temp_output_32_0_g11894));
				float4 staticSwitch82_g11892 = appendResult18_g11894;
				#else
				float4 staticSwitch82_g11892 = temp_output_2_0_g11892;
				#endif
				float4 break4_g11895 = staticSwitch82_g11892;
				#ifdef _ENABLECUSTOMFADE_ON
				float staticSwitch8_g11753 = 1.0;
				#else
				float staticSwitch8_g11753 = IN.color.a;
				#endif
				#ifdef _ENABLESMOKE_ON
				float staticSwitch9_g11753 = 1.0;
				#else
				float staticSwitch9_g11753 = staticSwitch8_g11753;
				#endif
				float customVertexAlpha193 = staticSwitch9_g11753;
				float4 appendResult5_g11895 = (float4(break4_g11895.r , break4_g11895.g , break4_g11895.b , ( break4_g11895.a * customVertexAlpha193 )));
				float4 temp_output_344_0 = appendResult5_g11895;
				float4 temp_output_1_0_g11896 = temp_output_344_0;
				float4 appendResult8_g11896 = (float4(( (temp_output_1_0_g11896).rgb * (IN.color).rgb ) , temp_output_1_0_g11896.a));
				#ifdef _VERTEXTINTFIRST_ON
				float4 staticSwitch342 = temp_output_344_0;
				#else
				float4 staticSwitch342 = appendResult8_g11896;
				#endif
				float4 lerpResult125 = lerp( ( originalColor191 * IN.color ) , staticSwitch342 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float4 staticSwitch143 = staticSwitch342;
				#elif defined(_SHADERFADING_FULL)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_MASK)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_DISSOLVE)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_SPREAD)
				float4 staticSwitch143 = lerpResult125;
				#else
				float4 staticSwitch143 = staticSwitch342;
				#endif
				float4 temp_output_7_0_g11903 = staticSwitch143;
				#ifdef _BAKEDMATERIAL_ON
				float4 appendResult2_g11903 = (float4(( (temp_output_7_0_g11903).rgb / max( temp_output_7_0_g11903.a , 1E-05 ) ) , temp_output_7_0_g11903.a));
				float4 staticSwitch6_g11903 = appendResult2_g11903;
				#else
				float4 staticSwitch6_g11903 = temp_output_7_0_g11903;
				#endif
				
				float2 temp_output_8_0_g11904 = finalUV146;
				float3 unpack14_g11904 = UnpackNormalScale( tex2D( _NormalMap, temp_output_8_0_g11904 ), _NormalIntensity );
				unpack14_g11904.z = lerp( 1, unpack14_g11904.z, saturate(_NormalIntensity) );
				
				float4 Color = staticSwitch6_g11903;
				float3 Normal = unpack14_g11904;

				Color *= IN.color;

				return NormalsRenderingShared( Color, Normal, IN.tangentWS.xyz, IN.bitangentWS, IN.normalWS);
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "Sprite Forward"
			Tags { "LightMode"="UniversalForward" }

			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZTest LEqual
			ZWrite Off
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM

			#define ASE_SRP_VERSION 140007


			#pragma vertex vert
			#pragma fragment frag


			#define _SURFACE_TYPE_TRANSPARENT 1
			#define SHADERPASS SHADERPASS_SPRITEFORWARD

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging2D.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature_local _SHADERFADING_NONE _SHADERFADING_FULL _SHADERFADING_MASK _SHADERFADING_DISSOLVE _SHADERFADING_SPREAD
			#pragma shader_feature_local _ENABLESINESCALE_ON
			#pragma shader_feature _ENABLEVIBRATE_ON
			#pragma shader_feature _ENABLESINEMOVE_ON
			#pragma shader_feature _ENABLESQUISH_ON
			#pragma shader_feature _SPRITESHEETFIX_ON
			#pragma shader_feature_local _PIXELPERFECTUV_ON
			#pragma shader_feature _ENABLEWORLDTILING_ON
			#pragma shader_feature _ENABLESCREENTILING_ON
			#pragma shader_feature _TOGGLETIMEFREQUENCY_ON
			#pragma shader_feature _TOGGLETIMEFPS_ON
			#pragma shader_feature _TOGGLETIMESPEED_ON
			#pragma shader_feature _TOGGLEUNSCALEDTIME_ON
			#pragma shader_feature _TOGGLECUSTOMTIME_ON
			#pragma shader_feature _SHADERSPACE_UV _SHADERSPACE_UV_RAW _SHADERSPACE_OBJECT _SHADERSPACE_OBJECT_SCALED _SHADERSPACE_WORLD _SHADERSPACE_UI_GRAPHIC _SHADERSPACE_SCREEN
			#pragma shader_feature _PIXELPERFECTSPACE_ON
			#pragma shader_feature _BAKEDMATERIAL_ON
			#pragma shader_feature _VERTEXTINTFIRST_ON
			#pragma shader_feature _ENABLESHADOW_ON
			#pragma shader_feature _ENABLESTRONGTINT_ON
			#pragma shader_feature _ENABLEALPHATINT_ON
			#pragma shader_feature_local _ENABLEADDCOLOR_ON
			#pragma shader_feature_local _ENABLEHALFTONE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALGLOWFADE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALALPHAFADE_ON
			#pragma shader_feature_local _ENABLESOURCEGLOWDISSOLVE_ON
			#pragma shader_feature_local _ENABLESOURCEALPHADISSOLVE_ON
			#pragma shader_feature_local _ENABLEFULLGLOWDISSOLVE_ON
			#pragma shader_feature_local _ENABLEFULLALPHADISSOLVE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALDISTORTION_ON
			#pragma shader_feature_local _ENABLEFULLDISTORTION_ON
			#pragma shader_feature _ENABLETEXTURELAYER2_ON
			#pragma shader_feature _ENABLETEXTURELAYER1_ON
			#pragma shader_feature _ENABLESHIFTING_ON
			#pragma shader_feature _ENABLEENCHANTED_ON
			#pragma shader_feature_local _ENABLEPOISON_ON
			#pragma shader_feature_local _ENABLESHINE_ON
			#pragma shader_feature_local _ENABLERAINBOW_ON
			#pragma shader_feature_local _ENABLEBURN_ON
			#pragma shader_feature_local _ENABLEFROZEN_ON
			#pragma shader_feature_local _ENABLEMETAL_ON
			#pragma shader_feature_local _ENABLECAMOUFLAGE_ON
			#pragma shader_feature_local _ENABLEGLITCH_ON
			#pragma shader_feature_local _ENABLEHOLOGRAM_ON
			#pragma shader_feature _ENABLEPINGPONGGLOW_ON
			#pragma shader_feature_local _ENABLEPIXELOUTLINE_ON
			#pragma shader_feature_local _ENABLEOUTEROUTLINE_ON
			#pragma shader_feature_local _ENABLEINNEROUTLINE_ON
			#pragma shader_feature_local _ENABLESATURATION_ON
			#pragma shader_feature_local _ENABLESINEGLOW_ON
			#pragma shader_feature_local _ENABLEADDHUE_ON
			#pragma shader_feature_local _ENABLESHIFTHUE_ON
			#pragma shader_feature_local _ENABLEINKSPREAD_ON
			#pragma shader_feature_local _ENABLEBLACKTINT_ON
			#pragma shader_feature_local _ENABLESPLITTONING_ON
			#pragma shader_feature_local _ENABLEHUE_ON
			#pragma shader_feature_local _ENABLEBRIGHTNESS_ON
			#pragma shader_feature_local _ENABLECONTRAST_ON
			#pragma shader_feature _ENABLENEGATIVE_ON
			#pragma shader_feature_local _ENABLECOLORREPLACE_ON
			#pragma shader_feature_local _ENABLERECOLORRGBYCP_ON
			#pragma shader_feature _ENABLERECOLORRGB_ON
			#pragma shader_feature_local _ENABLEFLAME_ON
			#pragma shader_feature_local _ENABLECHECKERBOARD_ON
			#pragma shader_feature_local _ENABLECUSTOMFADE_ON
			#pragma shader_feature_local _ENABLESMOKE_ON
			#pragma shader_feature _ENABLESHARPEN_ON
			#pragma shader_feature _ENABLEGAUSSIANBLUR_ON
			#pragma shader_feature _ENABLESMOOTHPIXELART_ON
			#pragma shader_feature_local _TILINGFIX_ON
			#pragma shader_feature _ENABLEWIGGLE_ON
			#pragma shader_feature_local _ENABLEUVSCALE_ON
			#pragma shader_feature_local _ENABLEPIXELATE_ON
			#pragma shader_feature_local _ENABLEUVSCROLL_ON
			#pragma shader_feature_local _ENABLEUVROTATE_ON
			#pragma shader_feature_local _ENABLESINEROTATE_ON
			#pragma shader_feature_local _ENABLESQUEEZE_ON
			#pragma shader_feature_local _ENABLEUVDISTORT_ON
			#pragma shader_feature_local _ENABLEWIND_ON
			#pragma shader_feature_local _WINDLOCALWIND_ON
			#pragma shader_feature_local _WINDHIGHQUALITYNOISE_ON
			#pragma shader_feature_local _WINDISPARALLAX_ON
			#pragma shader_feature _UVDISTORTMASKTOGGLE_ON
			#pragma shader_feature _WIGGLEFIXEDGROUNDTOGGLE_ON
			#pragma shader_feature _RECOLORRGBTEXTURETOGGLE_ON
			#pragma shader_feature _RECOLORRGBYCPTEXTURETOGGLE_ON
			#pragma shader_feature_local _ADDHUEMASKTOGGLE_ON
			#pragma shader_feature_local _SINEGLOWMASKTOGGLE_ON
			#pragma shader_feature _INNEROUTLINETEXTURETOGGLE_ON
			#pragma shader_feature_local _INNEROUTLINEDISTORTIONTOGGLE_ON
			#pragma shader_feature _INNEROUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature _OUTEROUTLINETEXTURETOGGLE_ON
			#pragma shader_feature _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature_local _OUTEROUTLINEDISTORTIONTOGGLE_ON
			#pragma shader_feature _PIXELOUTLINETEXTURETOGGLE_ON
			#pragma shader_feature _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature _CAMOUFLAGEANIMATIONTOGGLE_ON
			#pragma shader_feature _METALMASKTOGGLE_ON
			#pragma shader_feature _SHINEMASKTOGGLE_ON
			#pragma shader_feature _ENCHANTEDLERPTOGGLE_ON
			#pragma shader_feature _ENCHANTEDRAINBOWTOGGLE_ON
			#pragma shader_feature _SHIFTINGRAINBOWTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1CONTRASTTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1SHEETTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1SCROLLTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2CONTRASTTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2SHEETTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2SCROLLTOGGLE_ON
			#pragma shader_feature _ADDCOLORCONTRASTTOGGLE_ON
			#pragma shader_feature _ADDCOLORMASKTOGGLE_ON
			#pragma shader_feature _STRONGTINTCONTRASTTOGGLE_ON
			#pragma shader_feature _STRONGTINTMASKTOGGLE_ON


			sampler2D _MainTex;
			#ifdef _TOGGLEUNSCALEDTIME_ON
			float UnscaledTime;
			#endif
			#ifdef _SHADERFADING_MASK
			sampler2D _FadingMask;
			#endif
			sampler2D _UberNoiseTexture;
			#ifdef _ENABLEWIND_ON
			float WindMinIntensity;
			float WindMaxIntensity;
			float WindNoiseScale;
			float WindTime;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			sampler2D _UVDistortMask;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			sampler2D _CustomFadeFadeMask;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			sampler2D _RecolorRGBTexture;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			sampler2D _RecolorRGBYCPTexture;
			#endif
			#ifdef _ENABLEADDHUE_ON
			sampler2D _AddHueMask;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			sampler2D _SineGlowMask;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			sampler2D _InnerOutlineTintTexture;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			sampler2D _OuterOutlineTintTexture;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			sampler2D _PixelOutlineTintTexture;
			#endif
			#ifdef _ENABLEMETAL_ON
			sampler2D _MetalMask;
			#endif
			#ifdef _ENABLESHINE_ON
			sampler2D _ShineMask;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			sampler2D _TextureLayer1Texture;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			sampler2D _TextureLayer2Texture;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			sampler2D _AddColorMask;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			sampler2D _StrongTintMask;
			#endif
			CBUFFER_START( UnityPerMaterial )
			#ifdef _ENABLESHIFTING_ON
			float4 _ShiftingColorA;
			#endif
			#ifdef _ENABLEPOISON_ON
			float4 _PoisonColor;
			#endif
			#ifdef _ENABLESPLITTONING_ON
			float4 _SplitToningHighlightsColor;
			float4 _SplitToningShadowsColor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float4 _EnchantedLowColor;
			float4 _EnchantedHighColor;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float4 _ColorReplaceToColor;
			float4 _ColorReplaceFromColor;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			float4 _RecolorRGBYCPRedTint;
			float4 _RecolorRGBYCPYellowTint;
			float4 _RecolorRGBYCPGreenTint;
			float4 _RecolorRGBYCPCyanTint;
			float4 _RecolorRGBYCPBlueTint;
			float4 _RecolorRGBYCPPurpleTint;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			float4 _RecolorRGBBlueTint;
			float4 _RecolorRGBGreenTint;
			float4 _RecolorRGBRedTint;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float4 _ShiftingColorB;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float4 _TextureLayer1Color;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float4 _TextureLayer2Color;
			#endif
			#ifdef _ENABLEBLACKTINT_ON
			float4 _BlackTintColor;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float4 _InkSpreadColor;
			#endif
			#ifdef _ENABLESHINE_ON
			float4 _ShineMask_ST;
			float4 _ShineColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenTint;
			#endif
			#ifdef _ENABLEMETAL_ON
			float4 _MetalMask_ST;
			float4 _MetalColor;
			float4 _MetalHighlightColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenHighlightColor;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float4 _CamouflageColorB;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnInsideNoiseColor;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float4 _CamouflageColorA;
			float4 _CamouflageBaseColor;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float4 _UVDistortMask_ST;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnInsideColor;
			#endif
			#ifdef _ENABLEPINGPONGGLOW_ON
			float4 _PingPongGlowTo;
			float4 _PingPongGlowFrom;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnEdgeColor;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float4 _PixelOutlineColor;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float4 _OuterOutlineColor;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float4 _InnerOutlineColor;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			float4 _SineGlowMask_ST;
			float4 _SineGlowColor;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float4 _AddHueMask_ST;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float4 _HologramTint;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float4 _FullGlowDissolveEdgeColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenSnowColor;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float4 _StrongTintTint;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float4 _AddColorMask_ST;
			float4 _AddColorColor;
			#endif
			#ifdef _ENABLESHADOW_ON
			float4 _ShadowColor;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float4 _SourceGlowDissolveEdgeColor;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float4 _DirectionalGlowFadeEdgeColor;
			#endif
			#ifdef _SPRITESHEETFIX_ON
			float4 _SpriteSheetRect;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float4 _AlphaTintColor;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float4 _StrongTintMask_ST;
			#endif
			#ifdef _SHADERFADING_MASK
			float4 _FadingMask_ST;
			#endif
			float4 _MainTex_TexelSize;
			float2 _FadingNoiseScale;
			#ifdef _SHADERFADING_SPREAD
			float2 _FadingPosition;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnEdgeNoiseScale;
			float2 _BurnPosition;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float2 _DirectionalAlphaFadeNoiseScale;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnSwirlNoiseScale;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenSnowScale;
			#endif
			#ifdef _ENABLEFLAME_ON
			float2 _FlameNoiseScale;
			float2 _FlameSpeed;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float2 _GlitchNoiseSpeed;
			float2 _GlitchNoiseScale;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			float2 _CustomFadeNoiseScale;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float2 _TextureLayer1Offset;
			float2 _TextureLayer1ScrollSpeed;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageDistortionSpeed;
			float2 _CamouflageDistortionScale;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnInsideNoiseScale;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float2 _TextureLayer1Scale;
			#endif
			#ifdef _ENABLESHADOW_ON
			float2 _ShadowOffset;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageDistortionIntensity;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float2 _SineMoveFrequency;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float2 _InnerOutlineTextureSpeed;
			float2 _InnerOutlineNoiseSpeed;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float2 _InkSpreadNoiseScale;
			float2 _InkSpreadPosition;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float2 _InnerOutlineNoiseScale;
			float2 _InnerOutlineDistortionIntensity;
			#endif
			#ifdef _ENABLEPOISON_ON
			float2 _PoisonNoiseSpeed;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float2 _HalftonePosition;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float2 _DirectionalGlowFadeNoiseScale;
			#endif
			#ifdef _ENABLEPOISON_ON
			float2 _PoisonNoiseScale;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float2 _OuterOutlineTextureSpeed;
			#endif
			#ifdef _ENABLESINESCALE_ON
			float2 _SineScaleFactor;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float2 _RainbowCenter;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float2 _OuterOutlineNoiseSpeed;
			float2 _OuterOutlineNoiseScale;
			float2 _OuterOutlineDistortionIntensity;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float2 _EnchantedSpeed;
			float2 _EnchantedScale;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float2 _PixelOutlineTextureSpeed;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float2 _RainbowNoiseScale;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageNoiseScaleA;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float2 _SineMoveOffset;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float2 _WorldTilingOffset;
			#endif
			#ifdef _ENABLEFULLALPHADISSOLVE_ON
			float2 _FullAlphaDissolveNoiseScale;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float2 _UVDistortNoiseScale;
			float2 _UVDistortTo;
			float2 _UVDistortFrom;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float2 _FullGlowDissolveNoiseScale;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float2 _GlitchMaskScale;
			float2 _GlitchMaskSpeed;
			float2 _GlitchDistortion;
			float2 _GlitchDistortionScale;
			float2 _GlitchDistortionSpeed;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortion;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float2 _SqueezeCenter;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortionScale;
			#endif
			#ifdef _ENABLESOURCEALPHADISSOLVE_ON
			float2 _SourceAlphaDissolvePosition;
			float2 _SourceAlphaDissolveNoiseScale;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float2 _ScreenTilingOffset;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionNoiseScale;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float2 _SourceGlowDissolvePosition;
			float2 _SourceGlowDissolveNoiseScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionDistortion;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float2 _ScreenTilingScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionDistortionScale;
			#endif
			#ifdef _ENABLEFULLDISTORTION_ON
			float2 _FullDistortionDistortion;
			float2 _FullDistortionNoiseScale;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortionSpeed;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseScale;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float2 _UVDistortSpeed;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float2 _SqueezeScale;
			#endif
			#ifdef _ENABLEUVSCALE_ON
			float2 _UVScaleScale;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float2 _WorldTilingScale;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageNoiseScaleB;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightScale;
			#endif
			#ifdef _ENABLEUVSCROLL_ON
			float2 _UVScrollSpeed;
			#endif
			#ifdef _ENABLEUVROTATE_ON
			float2 _UVRotatePivot;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2ScrollSpeed;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightSpeed;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2Offset;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float2 _SineRotatePivot;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseDistortionSpeed;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2Scale;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseDistortionScale;
			float2 _MetalNoiseDistortion;
			float2 _MetalNoiseSpeed;
			#endif
			#ifdef _ENABLEUVSCALE_ON
			float2 _UVScalePivot;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenHighlightDensity;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowSpeed;
			float _RainbowSaturation;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenFade;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowNoiseFactor;
			float _RainbowContrast;
			float _RainbowBrightness;
			float _RainbowDensity;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float _AddColorFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnInsideNoiseFactor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenHighlightContrast;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnEdgeNoiseFactor;
			#endif
			#ifdef _ENABLESHADOW_ON
			float _ShadowFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnSwirlFactor;
			float _BurnFade;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float _StrongTintContrast;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnRadius;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float _AlphaTintMinAlpha;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnInsideContrast;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float _AlphaTintFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnWidth;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float _StrongTintFade;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveFade;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonRecolorFactor;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowFade;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingBrightness;
			float _ShiftingSaturation;
			float _ShiftingContrast;
			float _ShiftingFade;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1EdgeClip;
			#endif
			int _TextureLayer1Columns;
			int _TextureLayer1Rows;
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1Speed;
			#endif
			int _TextureLayer1StartFrame;
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeRotation;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1Contrast;
			float _TextureLayer1Fade;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2EdgeClip;
			#endif
			int _TextureLayer2Columns;
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingDensity;
			#endif
			int _TextureLayer2Rows;
			int _TextureLayer2StartFrame;
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeInvert;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2Contrast;
			float _TextureLayer2Fade;
			#endif
			#ifdef _ENABLEFULLALPHADISSOLVE_ON
			float _FullAlphaDissolveFade;
			float _FullAlphaDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveInvert;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float _FullGlowDissolveFade;
			float _FullGlowDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEALPHADISSOLVE_ON
			float _SourceAlphaDissolveInvert;
			float _SourceAlphaDissolveFade;
			float _SourceAlphaDissolveNoiseFactor;
			float _SourceAlphaDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveNoiseFactor;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2Speed;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float _AddColorContrast;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingSpeed;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedFade;
			#endif
			#ifdef _ENABLESHINE_ON
			float _ShineSaturation;
			float _ShineContrast;
			float _ShineRotation;
			float _ShineFrequency;
			float _ShineSpeed;
			float _ShineWidth;
			float _ShineSmooth;
			float _ShineFade;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneFadeWidth;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonShiftSpeed;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneFade;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonDensity;
			float _PoisonFade;
			float _PoisonNoiseBrightness;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeFade;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneTiling;
			float _HalftoneInvert;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeWidth;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedRainbowDensity;
			float _EnchantedRainbowSpeed;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeNoiseFactor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedRainbowSaturation;
			float _EnchantedContrast;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeFade;
			float _DirectionalGlowFadeRotation;
			float _DirectionalGlowFadeInvert;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedBrightness;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeWidth;
			float _DirectionalAlphaFadeNoiseFactor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedReduce;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveWidth;
			#endif
			#ifdef _ENABLESQUISH_ON
			float _SquishStretch;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float _PixelOutlineWidth;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenSnowContrast;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float _SqueezeFade;
			float _SqueezePower;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float _UVDistortFade;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float _GlitchFade;
			float _GlitchMaskMin;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramFade;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateFrequency;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramDistortionOffset;
			float _HologramDistortionDensity;
			float _HologramDistortionSpeed;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionWidth;
			float _DirectionalDistortionNoiseFactor;
			float _DirectionalDistortionFade;
			float _DirectionalDistortionRotation;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramDistortionScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionInvert;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateAngle;
			#endif
			#ifdef _ENABLEUVROTATE_ON
			float _UVRotateSpeed;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeNoiseScale;
			float _SmokeVertexSeed;
			#endif
			#ifdef _ENABLESHARPEN_ON
			float _SharpenFade;
			float _SharpenFactor;
			float _SharpenOffset;
			#endif
			#ifdef _ENABLEGAUSSIANBLUR_ON
			float _GaussianBlurFade;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateFade;
			#endif
			#ifdef _ENABLEGAUSSIANBLUR_ON
			float _GaussianBlurOffset;
			#endif
			#ifdef _ENABLEWIGGLE_ON
			float _WiggleOffset;
			float _WiggleSpeed;
			float _WiggleFrequency;
			#endif
			#ifdef _ENABLEPIXELATE_ON
			float _PixelateFade;
			float _PixelatePixelsPerUnit;
			float _PixelatePixelDensity;
			#endif
			#ifdef _ENABLEWIGGLE_ON
			float _WiggleFade;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionRandomDirection;
			#endif
			#ifdef _ENABLEFULLDISTORTION_ON
			float _FullDistortionFade;
			#endif
			#ifdef _ENABLEWIND_ON
			float _WindSquishWindFactor;
			#endif
			#ifdef _ENABLEVIBRATE_ON
			float _VibrateFade;
			float _VibrateOffset;
			float _VibrateFrequency;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float _SineMoveFade;
			#endif
			#ifdef _TOGGLETIMEFREQUENCY_ON
			float _TimeRange;
			float _TimeFrequency;
			#endif
			#ifdef _ENABLEVIBRATE_ON
			float _VibrateRotation;
			#endif
			#ifdef _TOGGLETIMEFPS_ON
			float _TimeFPS;
			#endif
			#ifdef _TOGGLECUSTOMTIME_ON
			float _TimeValue;
			#endif
			#ifdef _ENABLESQUISH_ON
			float _SquishSquish;
			float _SquishFlip;
			float _SquishFade;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float _WorldTilingPixelsPerUnit;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float _ScreenTilingPixelsPerUnit;
			#endif
			#ifdef _TOGGLETIMESPEED_ON
			float _TimeSpeed;
			#endif
			#ifdef _ENABLESINESCALE_ON
			float _SineScaleFrequency;
			#endif
			float _FadingFade;
			float _FadingWidth;
			#ifdef _ENABLEWIND_ON
			float _WindSquishFactor;
			float _WindFlip;
			float _WindMaxRotation;
			float _WindRotation;
			float _WindNoiseSpeed;
			float _WindNoiseScale;
			float _WindXPosition;
			float _WindMaxIntensity;
			float _WindMinIntensity;
			float _WindRotationWindFactor;
			#endif
			#ifdef _SHADERFADING_SPREAD
			float _FadingNoiseFactor;
			#endif
			#ifdef _SHADERSPACE_SCREEN
			float _ScreenWidthUnits;
			#endif
			#ifdef _SHADERSPACE_UI_GRAPHIC
			float _RectHeight;
			float _RectWidth;
			#endif
			float _PixelsPerUnit;
			#ifdef _ENABLESMOKE_ON
			float _SmokeNoiseFactor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenSnowDensity;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeSmoothness;
			float _SmokeAlpha;
			#endif
			#ifdef _ENABLEPINGPONGGLOW_ON
			float _PingPongGlowContrast;
			float _PingPongGlowFade;
			float _PingPongGlowFrequency;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float _PixelOutlineFade;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float _OuterOutlineWidth;
			float _OuterOutlineFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramContrast;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float _InnerOutlineWidth;
			#endif
			#ifdef _ENABLESATURATION_ON
			float _Saturation;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			float _SineGlowMin;
			float _SineGlowMax;
			float _SineGlowFrequency;
			float _SineGlowFade;
			float _SineGlowContrast;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float _InnerOutlineFade;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramLineSpeed;
			float _HologramLineGap;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenContrast;
			#endif
			#ifdef _ENABLEMETAL_ON
			float _MetalFade;
			float _MetalContrast;
			float _MetalHighlightContrast;
			float _MetalHighlightDensity;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramLineFrequency;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageContrast;
			float _CamouflageDensityB;
			float _CamouflageSmoothnessA;
			float _CamouflageDensityA;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float _GlitchHueSpeed;
			float _GlitchBrightness;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramMinAlpha;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageSmoothnessB;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueContrast;
			float _AddHueBrightness;
			float _AddHueSaturation;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceSmoothness;
			float _ColorReplaceContrast;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			float _RecolorRGBYCPFade;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			float _RecolorRGBFade;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameBrightness;
			float _FlameSmooth;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceRange;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameRadius;
			float _FlameNoiseHeightFactor;
			#endif
			#ifdef _ENABLECHECKERBOARD_ON
			float _CheckerboardTiling;
			float _CheckerboardDarken;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			float _CustomFadeAlpha;
			float _CustomFadeSmoothness;
			float _CustomFadeNoiseFactor;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameNoiseFactor;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceFade;
			#endif
			#ifdef _ENABLENEGATIVE_ON
			float _NegativeFade;
			#endif
			#ifdef _ENABLECONTRAST_ON
			float _Contrast;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueSpeed;
			#endif
			#ifdef _ENABLESHIFTHUE_ON
			float _ShiftHueSpeed;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float _InkSpreadWidth;
			float _InkSpreadNoiseFactor;
			float _InkSpreadDistance;
			float _InkSpreadFade;
			float _InkSpreadContrast;
			#endif
			#ifdef _ENABLEBLACKTINT_ON
			float _BlackTintFade;
			float _BlackTintPower;
			#endif
			#ifdef _ENABLESPLITTONING_ON
			float _SplitToningFade;
			float _SplitToningContrast;
			float _SplitToningBalance;
			float _SplitToningShift;
			#endif
			#ifdef _ENABLEHUE_ON
			float _Hue;
			#endif
			#ifdef _ENABLEBRIGHTNESS_ON
			float _Brightness;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeDarkEdge;
			#endif
			float _NormalIntensity;
			CBUFFER_END


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 uv0 : TEXCOORD0;
				float4 color : COLOR;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 texCoord0 : TEXCOORD0;
				float4 color : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			#if ETC1_EXTERNAL_ALPHA
				TEXTURE2D( _AlphaTex ); SAMPLER( sampler_AlphaTex );
				float _EnableAlphaTexture;
			#endif

			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float MyCustomExpression16_g11716( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11714( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float FastNoise101_g11665( float x )
			{
				float i = floor(x);
				float f = frac(x);
				float s = sign(frac(x/2.0)-0.5);
				    
				float k = 0.5+0.5*sin(i);
				return s*f*(f-1.0)*((16.0*k-4.0)*f*(f-1.0)-1.0);
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float MyCustomExpression16_g11667( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11668( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11671( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11670( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11676( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11677( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11718( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11673( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11729( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float4 texturePointSmooth( sampler2D tex, float4 textureTexelSize, float2 uvs )
			{
				float2 size;
				size.x = textureTexelSize.z;
				size.y = textureTexelSize.w;
				float2 pixel = float2(1.0,1.0) / size;
				uvs -= pixel * float2(0.5,0.5);
				float2 uv_pixels = uvs * size;
				float2 delta_pixel = frac(uv_pixels) - float2(0.5,0.5);
				float2 ddxy = fwidth(uv_pixels);
				float2 mip = log2(ddxy) - 0.5;
				float2 clampedUV = uvs + (clamp(delta_pixel / ddxy, 0.0, 1.0) - delta_pixel) * pixel;
				return tex2Dlod(tex, float4(clampedUV,0, min(mip.x, mip.y)));
			}
			
			float MyCustomExpression16_g11755( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11757( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11761( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float3 RGBToHSV(float3 c)
			{
				float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
				float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
				float d = q.x - min( q.w, q.y );
				float e = 1.0e-10;
				return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
			}
			float3 MyCustomExpression115_g11766( float3 In, float3 From, float3 To, float Fuzziness, float Range )
			{
				float Distance = distance(From, In);
				return lerp(To, In, saturate((Distance - Range) / max(Fuzziness, 0.001)));
			}
			
			float3 HSVToRGB( float3 c )
			{
				float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
				float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
				return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
			}
			
			float MyCustomExpression16_g11789( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11773( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11800( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11808( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11846( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11843( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11845( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11835( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11837( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11828( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11830( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11831( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11823( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11821( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11822( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11817( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11849( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11854( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11852( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float2 MyCustomExpression10_g11864( float2 UV, int Width, int Height, float Tile, float2 Invert )
			{
				Tile = fmod(Tile + 0.001, Width * Height);
				float2 tileCount = float2(1, 1) / float2(Width, Height);
				float tileY = abs(Invert.y * Height - (floor(Tile * tileCount.x) + Invert.y * 1));
				 float tileX = abs(Invert.x * Width - ((Tile - Width * floor(Tile * tileCount.x)) + Invert.x * 1));
				return (UV + float2(tileX, tileY)) * tileCount;
			}
			
			float2 MyCustomExpression10_g11860( float2 UV, int Width, int Height, float Tile, float2 Invert )
			{
				Tile = fmod(Tile + 0.001, Width * Height);
				float2 tileCount = float2(1, 1) / float2(Width, Height);
				float tileY = abs(Invert.y * Height - (floor(Tile * tileCount.x) + Invert.y * 1));
				 float tileX = abs(Invert.x * Width - ((Tile - Width * floor(Tile * tileCount.x)) + Invert.x * 1));
				return (UV + float2(tileX, tileY)) * tileCount;
			}
			
			float MyCustomExpression16_g11871( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11879( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11881( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11877( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11873( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11875( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			

			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				float2 _ZeroVector = float2(0,0);
				float2 texCoord363 = v.uv0.xy * float2( 1,1 ) + float2( 0,0 );
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				#ifdef _ENABLESCREENTILING_ON
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult16_g11656 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11656 = ( ( ( (( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / 10.0 ) )).xy * _ScreenTilingScale ) + _ScreenTilingOffset ) * ( _ScreenTilingPixelsPerUnit * appendResult16_g11656 ) );
				#else
				float2 staticSwitch2_g11656 = texCoord363;
				#endif
				float3 ase_worldPos = TransformObjectToWorld( (v.vertex).xyz );
				#ifdef _ENABLEWORLDTILING_ON
				float2 appendResult16_g11657 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11657 = ( ( ( (ase_worldPos).xy * _WorldTilingScale ) + _WorldTilingOffset ) * ( _WorldTilingPixelsPerUnit * appendResult16_g11657 ) );
				#else
				float2 staticSwitch2_g11657 = staticSwitch2_g11656;
				#endif
				float2 originalUV460 = staticSwitch2_g11657;
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11658 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch449 = ( floor( ( originalUV460 * appendResult7_g11658 ) ) / appendResult7_g11658 );
				#else
				float2 staticSwitch449 = originalUV460;
				#endif
				float2 uvAfterPixelArt450 = staticSwitch449;
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11662 = uvAfterPixelArt450;
				float2 appendResult374 = (float2(_SpriteSheetRect.x , _SpriteSheetRect.y));
				float2 spriteRectMin376 = appendResult374;
				float2 break11_g11662 = spriteRectMin376;
				float2 appendResult375 = (float2(_SpriteSheetRect.z , _SpriteSheetRect.w));
				float2 spriteRectMax377 = appendResult375;
				float2 break10_g11662 = spriteRectMax377;
				float2 break9_g11662 = float2( 0,0 );
				float2 break8_g11662 = float2( 1,1 );
				float2 appendResult15_g11662 = (float2((break9_g11662.x + (break14_g11662.x - break11_g11662.x) * (break8_g11662.x - break9_g11662.x) / (break10_g11662.x - break11_g11662.x)) , (break9_g11662.y + (break14_g11662.y - break11_g11662.y) * (break8_g11662.y - break9_g11662.y) / (break10_g11662.y - break11_g11662.y))));
				float2 staticSwitch366 = appendResult15_g11662;
				#else
				float2 staticSwitch366 = uvAfterPixelArt450;
				#endif
				float2 fixedUV475 = staticSwitch366;
				#ifdef _ENABLESQUISH_ON
				float2 break77_g11897 = fixedUV475;
				float2 appendResult72_g11897 = (float2(( _SquishStretch * ( break77_g11897.x - 0.5 ) * _SquishFade ) , ( _SquishFade * ( break77_g11897.y + _SquishFlip ) * -_SquishSquish )));
				float2 staticSwitch198 = ( appendResult72_g11897 + _ZeroVector );
				#else
				float2 staticSwitch198 = _ZeroVector;
				#endif
				float2 temp_output_2_0_g11899 = staticSwitch198;
				#ifdef _TOGGLECUSTOMTIME_ON
				float staticSwitch44_g11661 = _TimeValue;
				#else
				float staticSwitch44_g11661 = _TimeParameters.x;
				#endif
				#ifdef _TOGGLEUNSCALEDTIME_ON
				float staticSwitch34_g11661 = UnscaledTime;
				#else
				float staticSwitch34_g11661 = staticSwitch44_g11661;
				#endif
				#ifdef _TOGGLETIMESPEED_ON
				float staticSwitch37_g11661 = ( staticSwitch34_g11661 * _TimeSpeed );
				#else
				float staticSwitch37_g11661 = staticSwitch34_g11661;
				#endif
				#ifdef _TOGGLETIMEFPS_ON
				float staticSwitch38_g11661 = ( floor( ( staticSwitch37_g11661 * _TimeFPS ) ) / _TimeFPS );
				#else
				float staticSwitch38_g11661 = staticSwitch37_g11661;
				#endif
				#ifdef _TOGGLETIMEFREQUENCY_ON
				float staticSwitch42_g11661 = ( ( sin( ( staticSwitch38_g11661 * _TimeFrequency ) ) * _TimeRange ) + 100.0 );
				#else
				float staticSwitch42_g11661 = staticSwitch38_g11661;
				#endif
				float shaderTime237 = staticSwitch42_g11661;
				float temp_output_8_0_g11899 = shaderTime237;
				#ifdef _ENABLESINEMOVE_ON
				float2 staticSwitch4_g11899 = ( ( sin( ( temp_output_8_0_g11899 * _SineMoveFrequency ) ) * _SineMoveOffset * _SineMoveFade ) + temp_output_2_0_g11899 );
				#else
				float2 staticSwitch4_g11899 = temp_output_2_0_g11899;
				#endif
				#ifdef _ENABLEVIBRATE_ON
				float temp_output_30_0_g11900 = temp_output_8_0_g11899;
				float3 rotatedValue21_g11900 = RotateAroundAxis( float3( 0,0,0 ), float3( 0,1,0 ), float3( 0,0,1 ), ( temp_output_30_0_g11900 * _VibrateRotation ) );
				float2 staticSwitch6_g11899 = ( ( sin( ( _VibrateFrequency * temp_output_30_0_g11900 ) ) * _VibrateOffset * _VibrateFade * (rotatedValue21_g11900).xy ) + staticSwitch4_g11899 );
				#else
				float2 staticSwitch6_g11899 = staticSwitch4_g11899;
				#endif
				#ifdef _ENABLESINESCALE_ON
				float2 staticSwitch10_g11899 = ( staticSwitch6_g11899 + ( (v.vertex.xyz).xy * ( ( ( sin( ( _SineScaleFrequency * temp_output_8_0_g11899 ) ) + 1.0 ) * 0.5 ) * _SineScaleFactor ) ) );
				#else
				float2 staticSwitch10_g11899 = staticSwitch6_g11899;
				#endif
				float2 temp_output_424_0 = staticSwitch10_g11899;
#ifdef _SHADERFADING_MASK
				float2 uv_FadingMask = v.uv0.xy * _FadingMask_ST.xy + _FadingMask_ST.zw;
				#endif
#ifdef _SHADERFADING_MASK
				float4 tex2DNode3_g11712 = tex2Dlod( _FadingMask, float4( uv_FadingMask, 0, 0.0) );
				#endif
				float temp_output_4_0_g11715 = max( _FadingWidth , 0.001 );
				float2 texCoord435 = v.uv0.xy * float2( 1,1 ) + float2( 0,0 );
				#ifdef _PIXELPERFECTSPACE_ON
				float2 temp_output_432_0 = (_MainTex_TexelSize).zw;
				float2 staticSwitch437 = ( floor( ( texCoord435 * temp_output_432_0 ) ) / temp_output_432_0 );
				#else
				float2 staticSwitch437 = texCoord435;
				#endif
				float2 temp_output_61_0_g11663 = staticSwitch437;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 texCoord23_g11663 = v.uv0.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERSPACE_UI_GRAPHIC
				float2 appendResult28_g11663 = (float2(_RectWidth , _RectHeight));
				#endif
				#if defined(_SHADERSPACE_UV)
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#elif defined(_SHADERSPACE_UV_RAW)
				float2 staticSwitch1_g11663 = temp_output_61_0_g11663;
				#elif defined(_SHADERSPACE_OBJECT)
				float2 staticSwitch1_g11663 = (v.vertex.xyz).xy;
				#elif defined(_SHADERSPACE_OBJECT_SCALED)
				float2 staticSwitch1_g11663 = ( (v.vertex.xyz).xy * (ase_objectScale).xy );
				#elif defined(_SHADERSPACE_WORLD)
				float2 staticSwitch1_g11663 = (ase_worldPos).xy;
				#elif defined(_SHADERSPACE_UI_GRAPHIC)
				float2 staticSwitch1_g11663 = ( texCoord23_g11663 * ( appendResult28_g11663 / _PixelsPerUnit ) );
				#elif defined(_SHADERSPACE_SCREEN)
				float2 staticSwitch1_g11663 = ( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / _ScreenWidthUnits ) );
				#else
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#endif
				float2 shaderPosition235 = staticSwitch1_g11663;
				float linValue16_g11716 = tex2Dlod( _UberNoiseTexture, float4( ( shaderPosition235 * _FadingNoiseScale ), 0, 0.0) ).r;
				float localMyCustomExpression16_g11716 = MyCustomExpression16_g11716( linValue16_g11716 );
				float clampResult14_g11715 = clamp( ( ( ( _FadingFade * ( 1.0 + temp_output_4_0_g11715 ) ) - localMyCustomExpression16_g11716 ) / temp_output_4_0_g11715 ) , 0.0 , 1.0 );
				float2 temp_output_27_0_g11713 = shaderPosition235;
				float linValue16_g11714 = tex2Dlod( _UberNoiseTexture, float4( ( temp_output_27_0_g11713 * _FadingNoiseScale ), 0, 0.0) ).r;
				float localMyCustomExpression16_g11714 = MyCustomExpression16_g11714( linValue16_g11714 );
#ifdef _SHADERFADING_SPREAD
				float clampResult3_g11713 = clamp( ( ( _FadingFade - ( distance( _FadingPosition , temp_output_27_0_g11713 ) + ( localMyCustomExpression16_g11714 * _FadingNoiseFactor ) ) ) / max( _FadingWidth , 0.001 ) ) , 0.0 , 1.0 );
				#endif
				#if defined(_SHADERFADING_NONE)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_FULL)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_MASK)
				float staticSwitch139 = ( _FadingFade * ( tex2DNode3_g11712.r * tex2DNode3_g11712.a ) );
				#elif defined(_SHADERFADING_DISSOLVE)
				float staticSwitch139 = clampResult14_g11715;
				#elif defined(_SHADERFADING_SPREAD)
				float staticSwitch139 = clampResult3_g11713;
				#else
				float staticSwitch139 = _FadingFade;
				#endif
				float fullFade123 = staticSwitch139;
				float2 lerpResult121 = lerp( float2( 0,0 ) , temp_output_424_0 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float2 staticSwitch142 = temp_output_424_0;
				#elif defined(_SHADERFADING_FULL)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_MASK)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_DISSOLVE)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_SPREAD)
				float2 staticSwitch142 = lerpResult121;
				#else
				float2 staticSwitch142 = temp_output_424_0;
				#endif
				
				o.ase_texcoord3 = screenPos;
				
				o.ase_texcoord4 = v.vertex;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3( 0, 0, 0 );
				#endif
				float3 vertexValue = float3( staticSwitch142 ,  0.0 );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.normal = v.normal;
				v.tangent.xyz = v.tangent.xyz;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( v.vertex.xyz );

				o.texCoord0 = v.uv0;
				o.color = v.color;
				o.clipPos = vertexInput.positionCS;
				o.positionWS = vertexInput.positionWS;

				return o;
			}

			half4 frag( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				float3 positionWS = IN.positionWS.xyz;

				float2 texCoord363 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float4 screenPos = IN.ase_texcoord3;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				#ifdef _ENABLESCREENTILING_ON
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult16_g11656 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11656 = ( ( ( (( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / 10.0 ) )).xy * _ScreenTilingScale ) + _ScreenTilingOffset ) * ( _ScreenTilingPixelsPerUnit * appendResult16_g11656 ) );
				#else
				float2 staticSwitch2_g11656 = texCoord363;
				#endif
				#ifdef _ENABLEWORLDTILING_ON
				float2 appendResult16_g11657 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11657 = ( ( ( (positionWS).xy * _WorldTilingScale ) + _WorldTilingOffset ) * ( _WorldTilingPixelsPerUnit * appendResult16_g11657 ) );
				#else
				float2 staticSwitch2_g11657 = staticSwitch2_g11656;
				#endif
				float2 originalUV460 = staticSwitch2_g11657;
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11658 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch449 = ( floor( ( originalUV460 * appendResult7_g11658 ) ) / appendResult7_g11658 );
				#else
				float2 staticSwitch449 = originalUV460;
				#endif
				float2 uvAfterPixelArt450 = staticSwitch449;
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11662 = uvAfterPixelArt450;
				float2 appendResult374 = (float2(_SpriteSheetRect.x , _SpriteSheetRect.y));
				float2 spriteRectMin376 = appendResult374;
				float2 break11_g11662 = spriteRectMin376;
				float2 appendResult375 = (float2(_SpriteSheetRect.z , _SpriteSheetRect.w));
				float2 spriteRectMax377 = appendResult375;
				float2 break10_g11662 = spriteRectMax377;
				float2 break9_g11662 = float2( 0,0 );
				float2 break8_g11662 = float2( 1,1 );
				float2 appendResult15_g11662 = (float2((break9_g11662.x + (break14_g11662.x - break11_g11662.x) * (break8_g11662.x - break9_g11662.x) / (break10_g11662.x - break11_g11662.x)) , (break9_g11662.y + (break14_g11662.y - break11_g11662.y) * (break8_g11662.y - break9_g11662.y) / (break10_g11662.y - break11_g11662.y))));
				float2 staticSwitch366 = appendResult15_g11662;
				#else
				float2 staticSwitch366 = uvAfterPixelArt450;
				#endif
				float2 fixedUV475 = staticSwitch366;
				float2 temp_output_3_0_g11664 = fixedUV475;
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch117_g11665 = _WindMinIntensity;
				#else
				float staticSwitch117_g11665 = WindMinIntensity;
				#endif
				#endif
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch118_g11665 = _WindMaxIntensity;
				#else
				float staticSwitch118_g11665 = WindMaxIntensity;
				#endif
				#endif
				float4 transform62_g11665 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDISPARALLAX_ON
				float staticSwitch111_g11665 = _WindXPosition;
				#else
				float staticSwitch111_g11665 = transform62_g11665.x;
				#endif
				#endif
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch113_g11665 = _WindNoiseScale;
				#else
				float staticSwitch113_g11665 = WindNoiseScale;
				#endif
				#endif
				#ifdef _TOGGLECUSTOMTIME_ON
				float staticSwitch44_g11661 = _TimeValue;
				#else
				float staticSwitch44_g11661 = _TimeParameters.x;
				#endif
				#ifdef _TOGGLEUNSCALEDTIME_ON
				float staticSwitch34_g11661 = UnscaledTime;
				#else
				float staticSwitch34_g11661 = staticSwitch44_g11661;
				#endif
				#ifdef _TOGGLETIMESPEED_ON
				float staticSwitch37_g11661 = ( staticSwitch34_g11661 * _TimeSpeed );
				#else
				float staticSwitch37_g11661 = staticSwitch34_g11661;
				#endif
				#ifdef _TOGGLETIMEFPS_ON
				float staticSwitch38_g11661 = ( floor( ( staticSwitch37_g11661 * _TimeFPS ) ) / _TimeFPS );
				#else
				float staticSwitch38_g11661 = staticSwitch37_g11661;
				#endif
				#ifdef _TOGGLETIMEFREQUENCY_ON
				float staticSwitch42_g11661 = ( ( sin( ( staticSwitch38_g11661 * _TimeFrequency ) ) * _TimeRange ) + 100.0 );
				#else
				float staticSwitch42_g11661 = staticSwitch38_g11661;
				#endif
				float shaderTime237 = staticSwitch42_g11661;
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch125_g11665 = ( shaderTime237 * _WindNoiseSpeed );
				#else
				float staticSwitch125_g11665 = WindTime;
				#endif
				float temp_output_50_0_g11665 = ( ( staticSwitch111_g11665 * staticSwitch113_g11665 ) + staticSwitch125_g11665 );
				float x101_g11665 = temp_output_50_0_g11665;
				float localFastNoise101_g11665 = FastNoise101_g11665( x101_g11665 );
				float2 temp_cast_0 = (temp_output_50_0_g11665).xx;
				float simplePerlin2D121_g11665 = snoise( temp_cast_0*0.5 );
				simplePerlin2D121_g11665 = simplePerlin2D121_g11665*0.5 + 0.5;
				#ifdef _WINDHIGHQUALITYNOISE_ON
				float staticSwitch123_g11665 = simplePerlin2D121_g11665;
				#else
				float staticSwitch123_g11665 = ( localFastNoise101_g11665 + 0.5 );
				#endif
				float lerpResult86_g11665 = lerp( staticSwitch117_g11665 , staticSwitch118_g11665 , staticSwitch123_g11665);
				float clampResult29_g11665 = clamp( ( ( _WindRotationWindFactor * lerpResult86_g11665 ) + _WindRotation ) , -_WindMaxRotation , _WindMaxRotation );
				float2 temp_output_1_0_g11665 = temp_output_3_0_g11664;
				float temp_output_39_0_g11665 = ( temp_output_1_0_g11665.y + _WindFlip );
				float3 appendResult43_g11665 = (float3(0.5 , -_WindFlip , 0.0));
				float2 appendResult27_g11665 = (float2(0.0 , ( _WindSquishFactor * min( ( ( _WindSquishWindFactor * abs( lerpResult86_g11665 ) ) + abs( _WindRotation ) ) , _WindMaxRotation ) * temp_output_39_0_g11665 )));
				float3 rotatedValue19_g11665 = RotateAroundAxis( appendResult43_g11665, float3( ( appendResult27_g11665 + temp_output_1_0_g11665 ) ,  0.0 ), float3( 0,0,1 ), ( clampResult29_g11665 * temp_output_39_0_g11665 ) );
				float2 staticSwitch4_g11664 = (rotatedValue19_g11665).xy;
				#else
				float2 staticSwitch4_g11664 = temp_output_3_0_g11664;
				#endif
				float2 texCoord435 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				#ifdef _PIXELPERFECTSPACE_ON
				float2 temp_output_432_0 = (_MainTex_TexelSize).zw;
				float2 staticSwitch437 = ( floor( ( texCoord435 * temp_output_432_0 ) ) / temp_output_432_0 );
				#else
				float2 staticSwitch437 = texCoord435;
				#endif
				float2 temp_output_61_0_g11663 = staticSwitch437;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 texCoord23_g11663 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERSPACE_UI_GRAPHIC
				float2 appendResult28_g11663 = (float2(_RectWidth , _RectHeight));
				#endif
				#if defined(_SHADERSPACE_UV)
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#elif defined(_SHADERSPACE_UV_RAW)
				float2 staticSwitch1_g11663 = temp_output_61_0_g11663;
				#elif defined(_SHADERSPACE_OBJECT)
				float2 staticSwitch1_g11663 = (IN.ase_texcoord4.xyz).xy;
				#elif defined(_SHADERSPACE_OBJECT_SCALED)
				float2 staticSwitch1_g11663 = ( (IN.ase_texcoord4.xyz).xy * (ase_objectScale).xy );
				#elif defined(_SHADERSPACE_WORLD)
				float2 staticSwitch1_g11663 = (positionWS).xy;
				#elif defined(_SHADERSPACE_UI_GRAPHIC)
				float2 staticSwitch1_g11663 = ( texCoord23_g11663 * ( appendResult28_g11663 / _PixelsPerUnit ) );
				#elif defined(_SHADERSPACE_SCREEN)
				float2 staticSwitch1_g11663 = ( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / _ScreenWidthUnits ) );
				#else
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#endif
				float2 shaderPosition235 = staticSwitch1_g11663;
				#ifdef _ENABLEFULLDISTORTION_ON
				float2 temp_output_195_0_g11666 = shaderPosition235;
				float linValue16_g11667 = tex2D( _UberNoiseTexture, ( temp_output_195_0_g11666 * _FullDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11667 = MyCustomExpression16_g11667( linValue16_g11667 );
				float linValue16_g11668 = tex2D( _UberNoiseTexture, ( ( temp_output_195_0_g11666 + float2( 0.321,0.321 ) ) * _FullDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11668 = MyCustomExpression16_g11668( linValue16_g11668 );
				float2 appendResult189_g11666 = (float2(( localMyCustomExpression16_g11667 - 0.5 ) , ( localMyCustomExpression16_g11668 - 0.5 )));
				float2 staticSwitch83 = ( staticSwitch4_g11664 + ( ( 1.0 - _FullDistortionFade ) * appendResult189_g11666 * _FullDistortionDistortion ) );
				#else
				float2 staticSwitch83 = staticSwitch4_g11664;
				#endif
				#ifdef _ENABLEDIRECTIONALDISTORTION_ON
				float2 temp_output_182_0_g11669 = shaderPosition235;
				float linValue16_g11671 = tex2D( _UberNoiseTexture, ( temp_output_182_0_g11669 * _DirectionalDistortionDistortionScale ) ).r;
				float localMyCustomExpression16_g11671 = MyCustomExpression16_g11671( linValue16_g11671 );
				float3 rotatedValue168_g11669 = RotateAroundAxis( float3( 0,0,0 ), float3( _DirectionalDistortionDistortion ,  0.0 ), float3( 0,0,1 ), ( ( ( localMyCustomExpression16_g11671 - 0.5 ) * 2.0 * _DirectionalDistortionRandomDirection ) * PI ) );
				float3 rotatedValue136_g11669 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_182_0_g11669 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalDistortionRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11669 = rotatedValue136_g11669;
				float linValue16_g11670 = tex2D( _UberNoiseTexture, ( temp_output_182_0_g11669 * _DirectionalDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11670 = MyCustomExpression16_g11670( linValue16_g11670 );
				float clampResult154_g11669 = clamp( ( ( break130_g11669.x + break130_g11669.y + _DirectionalDistortionFade + ( localMyCustomExpression16_g11670 * _DirectionalDistortionNoiseFactor ) ) / max( _DirectionalDistortionWidth , 0.001 ) ) , 0.0 , 1.0 );
				float2 staticSwitch82 = ( staticSwitch83 + ( (rotatedValue168_g11669).xy * ( 1.0 - (( _DirectionalDistortionInvert )?( ( 1.0 - clampResult154_g11669 ) ):( clampResult154_g11669 )) ) ) );
				#else
				float2 staticSwitch82 = staticSwitch83;
				#endif
				#ifdef _ENABLEHOLOGRAM_ON
				float temp_output_8_0_g11674 = ( ( ( shaderTime237 * _HologramDistortionSpeed ) + positionWS.y ) / unity_OrthoParams.y );
				float2 temp_cast_4 = (temp_output_8_0_g11674).xx;
				float2 temp_cast_5 = (_HologramDistortionDensity).xx;
				float linValue16_g11676 = tex2D( _UberNoiseTexture, ( temp_cast_4 * temp_cast_5 ) ).r;
				float localMyCustomExpression16_g11676 = MyCustomExpression16_g11676( linValue16_g11676 );
				float clampResult75_g11674 = clamp( localMyCustomExpression16_g11676 , 0.075 , 0.6 );
				float2 temp_cast_6 = (temp_output_8_0_g11674).xx;
				float2 temp_cast_7 = (_HologramDistortionScale).xx;
				float linValue16_g11677 = tex2D( _UberNoiseTexture, ( temp_cast_6 * temp_cast_7 ) ).r;
				float localMyCustomExpression16_g11677 = MyCustomExpression16_g11677( linValue16_g11677 );
				float2 appendResult2_g11675 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float hologramFade182 = _HologramFade;
				float2 appendResult44_g11674 = (float2(( ( ( clampResult75_g11674 * ( localMyCustomExpression16_g11677 - 0.5 ) ) * _HologramDistortionOffset * ( 100.0 / appendResult2_g11675 ).x ) * hologramFade182 ) , 0.0));
				float2 staticSwitch59 = ( staticSwitch82 + appendResult44_g11674 );
				#else
				float2 staticSwitch59 = staticSwitch82;
				#endif
				#ifdef _ENABLEGLITCH_ON
				float2 temp_output_18_0_g11672 = shaderPosition235;
				float2 glitchPosition154 = temp_output_18_0_g11672;
				float linValue16_g11718 = tex2D( _UberNoiseTexture, ( ( glitchPosition154 + ( _GlitchDistortionSpeed * shaderTime237 ) ) * _GlitchDistortionScale ) ).r;
				float localMyCustomExpression16_g11718 = MyCustomExpression16_g11718( linValue16_g11718 );
				float linValue16_g11673 = tex2D( _UberNoiseTexture, ( ( temp_output_18_0_g11672 + ( _GlitchMaskSpeed * shaderTime237 ) ) * _GlitchMaskScale ) ).r;
				float localMyCustomExpression16_g11673 = MyCustomExpression16_g11673( linValue16_g11673 );
				float glitchFade152 = ( max( localMyCustomExpression16_g11673 , _GlitchMaskMin ) * _GlitchFade );
				float2 staticSwitch62 = ( staticSwitch59 + ( ( localMyCustomExpression16_g11718 - 0.5 ) * _GlitchDistortion * glitchFade152 ) );
				#else
				float2 staticSwitch62 = staticSwitch59;
				#endif
				float2 temp_output_1_0_g11719 = staticSwitch62;
				float2 temp_output_26_0_g11719 = shaderPosition235;
				float temp_output_25_0_g11719 = shaderTime237;
				#ifdef _ENABLEUVDISTORT_ON
				float linValue16_g11729 = tex2D( _UberNoiseTexture, ( ( temp_output_26_0_g11719 + ( _UVDistortSpeed * temp_output_25_0_g11719 ) ) * _UVDistortNoiseScale ) ).r;
				float localMyCustomExpression16_g11729 = MyCustomExpression16_g11729( linValue16_g11729 );
				float2 lerpResult21_g11726 = lerp( _UVDistortFrom , _UVDistortTo , localMyCustomExpression16_g11729);
				float2 appendResult2_g11728 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 uv_UVDistortMask = IN.texCoord0.xy * _UVDistortMask_ST.xy + _UVDistortMask_ST.zw;
				float4 tex2DNode3_g11727 = tex2D( _UVDistortMask, uv_UVDistortMask );
				#ifdef _UVDISTORTMASKTOGGLE_ON
				float staticSwitch29_g11726 = ( _UVDistortFade * ( tex2DNode3_g11727.r * tex2DNode3_g11727.a ) );
				#else
				float staticSwitch29_g11726 = _UVDistortFade;
				#endif
				float2 staticSwitch5_g11719 = ( temp_output_1_0_g11719 + ( lerpResult21_g11726 * ( 100.0 / appendResult2_g11728 ) * staticSwitch29_g11726 ) );
				#else
				float2 staticSwitch5_g11719 = temp_output_1_0_g11719;
				#endif
				#ifdef _ENABLESQUEEZE_ON
				float2 temp_output_1_0_g11725 = staticSwitch5_g11719;
				float2 staticSwitch7_g11719 = ( temp_output_1_0_g11725 + ( ( temp_output_1_0_g11725 - _SqueezeCenter ) * pow( distance( temp_output_1_0_g11725 , _SqueezeCenter ) , _SqueezePower ) * _SqueezeScale * _SqueezeFade ) );
				#else
				float2 staticSwitch7_g11719 = staticSwitch5_g11719;
				#endif
				#ifdef _ENABLESINEROTATE_ON
				float3 rotatedValue36_g11724 = RotateAroundAxis( float3( _SineRotatePivot ,  0.0 ), float3( staticSwitch7_g11719 ,  0.0 ), float3( 0,0,1 ), ( sin( ( temp_output_25_0_g11719 * _SineRotateFrequency ) ) * ( ( _SineRotateAngle / 360.0 ) * PI ) * _SineRotateFade ) );
				float2 staticSwitch9_g11719 = (rotatedValue36_g11724).xy;
				#else
				float2 staticSwitch9_g11719 = staticSwitch7_g11719;
				#endif
				#ifdef _ENABLEUVROTATE_ON
				float3 rotatedValue8_g11723 = RotateAroundAxis( float3( _UVRotatePivot ,  0.0 ), float3( staticSwitch9_g11719 ,  0.0 ), float3( 0,0,1 ), ( temp_output_25_0_g11719 * _UVRotateSpeed * PI ) );
				float2 staticSwitch16_g11719 = (rotatedValue8_g11723).xy;
				#else
				float2 staticSwitch16_g11719 = staticSwitch9_g11719;
				#endif
				#ifdef _ENABLEUVSCROLL_ON
				float2 staticSwitch14_g11719 = ( ( _UVScrollSpeed * temp_output_25_0_g11719 ) + staticSwitch16_g11719 );
				#else
				float2 staticSwitch14_g11719 = staticSwitch16_g11719;
				#endif
				#ifdef _ENABLEPIXELATE_ON
				float2 appendResult35_g11721 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 MultFactor30_g11721 = ( ( _PixelatePixelDensity * ( appendResult35_g11721 / _PixelatePixelsPerUnit ) ) * ( 1.0 / max( _PixelateFade , 1E-05 ) ) );
				float2 clampResult46_g11721 = clamp( ( floor( ( MultFactor30_g11721 * ( staticSwitch14_g11719 + ( float2( 0.5,0.5 ) / MultFactor30_g11721 ) ) ) ) / MultFactor30_g11721 ) , float2( 0,0 ) , float2( 1,1 ) );
				float2 staticSwitch4_g11719 = clampResult46_g11721;
				#else
				float2 staticSwitch4_g11719 = staticSwitch14_g11719;
				#endif
				#ifdef _ENABLEUVSCALE_ON
				float2 staticSwitch24_g11719 = ( ( ( staticSwitch4_g11719 - _UVScalePivot ) / _UVScaleScale ) + _UVScalePivot );
				#else
				float2 staticSwitch24_g11719 = staticSwitch4_g11719;
				#endif
				float2 temp_output_1_0_g11730 = staticSwitch24_g11719;
				#ifdef _ENABLEWIGGLE_ON
				float temp_output_7_0_g11730 = ( sin( ( _WiggleFrequency * ( temp_output_26_0_g11719.y + ( _WiggleSpeed * temp_output_25_0_g11719 ) ) ) ) * _WiggleOffset * _WiggleFade );
				#ifdef _WIGGLEFIXEDGROUNDTOGGLE_ON
				float staticSwitch18_g11730 = ( temp_output_7_0_g11730 * temp_output_1_0_g11730.y );
				#else
				float staticSwitch18_g11730 = temp_output_7_0_g11730;
				#endif
				float2 appendResult12_g11730 = (float2(staticSwitch18_g11730 , 0.0));
				float2 staticSwitch13_g11730 = ( temp_output_1_0_g11730 + appendResult12_g11730 );
				#else
				float2 staticSwitch13_g11730 = temp_output_1_0_g11730;
				#endif
				float2 temp_output_484_0 = staticSwitch13_g11730;
				float2 texCoord131 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERFADING_MASK
				float2 uv_FadingMask = IN.texCoord0.xy * _FadingMask_ST.xy + _FadingMask_ST.zw;
				#endif
#ifdef _SHADERFADING_MASK
				float4 tex2DNode3_g11712 = tex2D( _FadingMask, uv_FadingMask );
				#endif
				float temp_output_4_0_g11715 = max( _FadingWidth , 0.001 );
				float linValue16_g11716 = tex2D( _UberNoiseTexture, ( shaderPosition235 * _FadingNoiseScale ) ).r;
				float localMyCustomExpression16_g11716 = MyCustomExpression16_g11716( linValue16_g11716 );
				float clampResult14_g11715 = clamp( ( ( ( _FadingFade * ( 1.0 + temp_output_4_0_g11715 ) ) - localMyCustomExpression16_g11716 ) / temp_output_4_0_g11715 ) , 0.0 , 1.0 );
				float2 temp_output_27_0_g11713 = shaderPosition235;
				float linValue16_g11714 = tex2D( _UberNoiseTexture, ( temp_output_27_0_g11713 * _FadingNoiseScale ) ).r;
				float localMyCustomExpression16_g11714 = MyCustomExpression16_g11714( linValue16_g11714 );
#ifdef _SHADERFADING_SPREAD
				float clampResult3_g11713 = clamp( ( ( _FadingFade - ( distance( _FadingPosition , temp_output_27_0_g11713 ) + ( localMyCustomExpression16_g11714 * _FadingNoiseFactor ) ) ) / max( _FadingWidth , 0.001 ) ) , 0.0 , 1.0 );
				#endif
				#if defined(_SHADERFADING_NONE)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_FULL)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_MASK)
				float staticSwitch139 = ( _FadingFade * ( tex2DNode3_g11712.r * tex2DNode3_g11712.a ) );
				#elif defined(_SHADERFADING_DISSOLVE)
				float staticSwitch139 = clampResult14_g11715;
				#elif defined(_SHADERFADING_SPREAD)
				float staticSwitch139 = clampResult3_g11713;
				#else
				float staticSwitch139 = _FadingFade;
				#endif
				float fullFade123 = staticSwitch139;
				float2 lerpResult130 = lerp( texCoord131 , temp_output_484_0 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float2 staticSwitch145 = temp_output_484_0;
				#elif defined(_SHADERFADING_FULL)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_MASK)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_DISSOLVE)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_SPREAD)
				float2 staticSwitch145 = lerpResult130;
				#else
				float2 staticSwitch145 = temp_output_484_0;
				#endif
				#ifdef _TILINGFIX_ON
				float2 staticSwitch485 = ( ( ( staticSwitch145 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				#else
				float2 staticSwitch485 = staticSwitch145;
				#endif
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11731 = staticSwitch485;
				float2 break11_g11731 = float2( 0,0 );
				float2 break10_g11731 = float2( 1,1 );
				float2 break9_g11731 = spriteRectMin376;
				float2 break8_g11731 = spriteRectMax377;
				float2 appendResult15_g11731 = (float2((break9_g11731.x + (break14_g11731.x - break11_g11731.x) * (break8_g11731.x - break9_g11731.x) / (break10_g11731.x - break11_g11731.x)) , (break9_g11731.y + (break14_g11731.y - break11_g11731.y) * (break8_g11731.y - break9_g11731.y) / (break10_g11731.y - break11_g11731.y))));
				float2 staticSwitch371 = min( max( appendResult15_g11731 , spriteRectMin376 ) , spriteRectMax377 );
				#else
				float2 staticSwitch371 = staticSwitch485;
				#endif
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11732 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch427 = ( originalUV460 + ( floor( ( ( staticSwitch371 - uvAfterPixelArt450 ) * appendResult7_g11732 ) ) / appendResult7_g11732 ) );
				#else
				float2 staticSwitch427 = staticSwitch371;
				#endif
				float2 finalUV146 = staticSwitch427;
				float2 temp_output_1_0_g11733 = finalUV146;
				#ifdef _ENABLESMOOTHPIXELART_ON
				sampler2D tex3_g11734 = _MainTex;
				float4 textureTexelSize3_g11734 = _MainTex_TexelSize;
				float2 uvs3_g11734 = temp_output_1_0_g11733;
				float4 localtexturePointSmooth3_g11734 = texturePointSmooth( tex3_g11734 , textureTexelSize3_g11734 , uvs3_g11734 );
				float4 staticSwitch8_g11733 = localtexturePointSmooth3_g11734;
				#else
				float4 staticSwitch8_g11733 = tex2D( _MainTex, temp_output_1_0_g11733 );
				#endif
				#ifdef _ENABLEGAUSSIANBLUR_ON
				float temp_output_10_0_g11735 = ( _GaussianBlurOffset * _GaussianBlurFade * 0.005 );
				float temp_output_2_0_g11745 = temp_output_10_0_g11735;
				float2 appendResult16_g11745 = (float2(temp_output_2_0_g11745 , 0.0));
				float2 appendResult25_g11747 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11747 = ( appendResult16_g11745 * appendResult25_g11747 );
				float2 temp_output_7_0_g11735 = temp_output_1_0_g11733;
				float2 temp_output_1_0_g11745 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( 1,1 ) ) );
				float2 temp_output_1_0_g11747 = temp_output_1_0_g11745;
				float2 appendResult17_g11745 = (float2(0.0 , temp_output_2_0_g11745));
				float2 appendResult25_g11746 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11746 = ( appendResult17_g11745 * appendResult25_g11746 );
				float2 temp_output_1_0_g11746 = temp_output_1_0_g11745;
				float temp_output_2_0_g11736 = temp_output_10_0_g11735;
				float2 appendResult16_g11736 = (float2(temp_output_2_0_g11736 , 0.0));
				float2 appendResult25_g11738 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11738 = ( appendResult16_g11736 * appendResult25_g11738 );
				float2 temp_output_1_0_g11736 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( -1,1 ) ) );
				float2 temp_output_1_0_g11738 = temp_output_1_0_g11736;
				float2 appendResult17_g11736 = (float2(0.0 , temp_output_2_0_g11736));
				float2 appendResult25_g11737 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11737 = ( appendResult17_g11736 * appendResult25_g11737 );
				float2 temp_output_1_0_g11737 = temp_output_1_0_g11736;
				float temp_output_2_0_g11742 = temp_output_10_0_g11735;
				float2 appendResult16_g11742 = (float2(temp_output_2_0_g11742 , 0.0));
				float2 appendResult25_g11744 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11744 = ( appendResult16_g11742 * appendResult25_g11744 );
				float2 temp_output_1_0_g11742 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( -1,-1 ) ) );
				float2 temp_output_1_0_g11744 = temp_output_1_0_g11742;
				float2 appendResult17_g11742 = (float2(0.0 , temp_output_2_0_g11742));
				float2 appendResult25_g11743 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11743 = ( appendResult17_g11742 * appendResult25_g11743 );
				float2 temp_output_1_0_g11743 = temp_output_1_0_g11742;
				float temp_output_2_0_g11739 = temp_output_10_0_g11735;
				float2 appendResult16_g11739 = (float2(temp_output_2_0_g11739 , 0.0));
				float2 appendResult25_g11741 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11741 = ( appendResult16_g11739 * appendResult25_g11741 );
				float2 temp_output_1_0_g11739 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( 1,-1 ) ) );
				float2 temp_output_1_0_g11741 = temp_output_1_0_g11739;
				float2 appendResult17_g11739 = (float2(0.0 , temp_output_2_0_g11739));
				float2 appendResult25_g11740 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11740 = ( appendResult17_g11739 * appendResult25_g11740 );
				float2 temp_output_1_0_g11740 = temp_output_1_0_g11739;
				float4 staticSwitch3_g11733 = ( ( ( ( tex2D( _MainTex, ( temp_output_26_0_g11747 + temp_output_1_0_g11747 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11747 + temp_output_1_0_g11747 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11746 + temp_output_1_0_g11746 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11746 + temp_output_1_0_g11746 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11738 + temp_output_1_0_g11738 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11738 + temp_output_1_0_g11738 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11737 + temp_output_1_0_g11737 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11737 + temp_output_1_0_g11737 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11744 + temp_output_1_0_g11744 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11744 + temp_output_1_0_g11744 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11743 + temp_output_1_0_g11743 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11743 + temp_output_1_0_g11743 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11741 + temp_output_1_0_g11741 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11741 + temp_output_1_0_g11741 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11740 + temp_output_1_0_g11740 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11740 + temp_output_1_0_g11740 ) ) ) ) ) * 0.0625 );
				#else
				float4 staticSwitch3_g11733 = staticSwitch8_g11733;
				#endif
				#ifdef _ENABLESHARPEN_ON
				float2 temp_output_1_0_g11748 = temp_output_1_0_g11733;
				float4 tex2DNode4_g11748 = tex2D( _MainTex, temp_output_1_0_g11748 );
				float temp_output_2_0_g11749 = _SharpenOffset;
				float2 appendResult16_g11749 = (float2(temp_output_2_0_g11749 , 0.0));
				float2 appendResult25_g11751 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11751 = ( appendResult16_g11749 * appendResult25_g11751 );
				float2 temp_output_1_0_g11749 = temp_output_1_0_g11748;
				float2 temp_output_1_0_g11751 = temp_output_1_0_g11749;
				float2 appendResult17_g11749 = (float2(0.0 , temp_output_2_0_g11749));
				float2 appendResult25_g11750 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11750 = ( appendResult17_g11749 * appendResult25_g11750 );
				float2 temp_output_1_0_g11750 = temp_output_1_0_g11749;
				float4 break22_g11748 = ( tex2DNode4_g11748 - ( ( ( ( ( tex2D( _MainTex, ( temp_output_26_0_g11751 + temp_output_1_0_g11751 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11751 + temp_output_1_0_g11751 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11750 + temp_output_1_0_g11750 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11750 + temp_output_1_0_g11750 ) ) ) ) / 4.0 ) - tex2DNode4_g11748 ) * ( _SharpenFactor * _SharpenFade ) ) );
				float clampResult23_g11748 = clamp( break22_g11748.a , 0.0 , 1.0 );
				float4 appendResult24_g11748 = (float4(break22_g11748.r , break22_g11748.g , break22_g11748.b , clampResult23_g11748));
				float4 staticSwitch12_g11733 = appendResult24_g11748;
				#else
				float4 staticSwitch12_g11733 = staticSwitch3_g11733;
				#endif
				float4 temp_output_471_0 = staticSwitch12_g11733;
				#ifdef _VERTEXTINTFIRST_ON
				float4 temp_output_1_0_g11752 = temp_output_471_0;
				float4 appendResult8_g11752 = (float4(( (temp_output_1_0_g11752).rgb * (IN.color).rgb ) , temp_output_1_0_g11752.a));
				float4 staticSwitch354 = appendResult8_g11752;
				#else
				float4 staticSwitch354 = temp_output_471_0;
				#endif
				float4 originalColor191 = staticSwitch354;
				float4 temp_output_1_0_g11753 = originalColor191;
				float4 temp_output_1_0_g11754 = temp_output_1_0_g11753;
				float2 temp_output_7_0_g11753 = finalUV146;
				#ifdef _ENABLESMOKE_ON
				float2 temp_output_43_0_g11754 = temp_output_7_0_g11753;
				float2 temp_cast_15 = (_SmokeNoiseScale).xx;
				float linValue16_g11755 = tex2D( _UberNoiseTexture, ( ( ( IN.color.r * (( _SmokeVertexSeed )?( 5.0 ):( 0.0 )) ) + temp_output_43_0_g11754 ) * temp_cast_15 ) ).r;
				float localMyCustomExpression16_g11755 = MyCustomExpression16_g11755( linValue16_g11755 );
				float clampResult28_g11754 = clamp( ( ( ( localMyCustomExpression16_g11755 - 1.0 ) * _SmokeNoiseFactor ) + ( ( ( IN.color.a / 2.5 ) - distance( temp_output_43_0_g11754 , float2( 0.5,0.5 ) ) ) * 2.5 * _SmokeSmoothness ) ) , 0.0 , 1.0 );
				float3 lerpResult34_g11754 = lerp( (temp_output_1_0_g11754).rgb , float3( 0,0,0 ) , ( ( 1.0 - clampResult28_g11754 ) * _SmokeDarkEdge ));
				float4 appendResult31_g11754 = (float4(lerpResult34_g11754 , ( clampResult28_g11754 * _SmokeAlpha * temp_output_1_0_g11754.a )));
				float4 staticSwitch2_g11753 = appendResult31_g11754;
				#else
				float4 staticSwitch2_g11753 = temp_output_1_0_g11753;
				#endif
				#ifdef _ENABLECUSTOMFADE_ON
				float4 temp_output_1_0_g11756 = staticSwitch2_g11753;
				float2 temp_output_57_0_g11756 = temp_output_7_0_g11753;
				float4 tex2DNode3_g11756 = tex2D( _CustomFadeFadeMask, temp_output_57_0_g11756 );
				float linValue16_g11757 = tex2D( _UberNoiseTexture, ( temp_output_57_0_g11756 * _CustomFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11757 = MyCustomExpression16_g11757( linValue16_g11757 );
				float clampResult37_g11756 = clamp( ( ( ( IN.color.a * 2.0 ) - 1.0 ) + ( tex2DNode3_g11756.r + ( localMyCustomExpression16_g11757 * _CustomFadeNoiseFactor ) ) ) , 0.0 , 1.0 );
				float4 appendResult13_g11756 = (float4((temp_output_1_0_g11756).rgb , ( temp_output_1_0_g11756.a * pow( clampResult37_g11756 , ( _CustomFadeSmoothness / max( tex2DNode3_g11756.r , 0.05 ) ) ) * _CustomFadeAlpha )));
				float4 staticSwitch3_g11753 = appendResult13_g11756;
				#else
				float4 staticSwitch3_g11753 = staticSwitch2_g11753;
				#endif
				float4 temp_output_1_0_g11758 = staticSwitch3_g11753;
				#ifdef _ENABLECHECKERBOARD_ON
				float4 temp_output_1_0_g11759 = temp_output_1_0_g11758;
				float2 appendResult4_g11759 = (float2(positionWS.x , positionWS.y));
				float2 temp_output_44_0_g11759 = ( appendResult4_g11759 * _CheckerboardTiling * 0.5 );
				float2 break12_g11759 = step( ( ceil( temp_output_44_0_g11759 ) - temp_output_44_0_g11759 ) , float2( 0.5,0.5 ) );
				float4 appendResult42_g11759 = (float4(( (temp_output_1_0_g11759).rgb * min( ( _CheckerboardDarken + abs( ( -break12_g11759.x + break12_g11759.y ) ) ) , 1.0 ) ) , temp_output_1_0_g11759.a));
				float4 staticSwitch2_g11758 = appendResult42_g11759;
				#else
				float4 staticSwitch2_g11758 = temp_output_1_0_g11758;
				#endif
				#ifdef _ENABLEFLAME_ON
				float2 temp_output_75_0_g11760 = finalUV146;
				float linValue16_g11761 = tex2D( _UberNoiseTexture, ( ( ( shaderTime237 * _FlameSpeed ) + temp_output_75_0_g11760 ) * _FlameNoiseScale ) ).r;
				float localMyCustomExpression16_g11761 = MyCustomExpression16_g11761( linValue16_g11761 );
				float saferPower57_g11760 = abs( max( ( temp_output_75_0_g11760.y - 0.2 ) , 0.0 ) );
				float temp_output_47_0_g11760 = max( _FlameRadius , 0.01 );
				float clampResult70_g11760 = clamp( ( ( ( localMyCustomExpression16_g11761 * pow( saferPower57_g11760 , _FlameNoiseHeightFactor ) * _FlameNoiseFactor ) + ( ( temp_output_47_0_g11760 - distance( temp_output_75_0_g11760 , float2( 0.5,0.4 ) ) ) / temp_output_47_0_g11760 ) ) * _FlameSmooth ) , 0.0 , 1.0 );
				float temp_output_63_0_g11760 = ( clampResult70_g11760 * _FlameBrightness );
				float4 appendResult31_g11760 = (float4(temp_output_63_0_g11760 , temp_output_63_0_g11760 , temp_output_63_0_g11760 , clampResult70_g11760));
				float4 staticSwitch6_g11758 = ( appendResult31_g11760 * staticSwitch2_g11758 );
				#else
				float4 staticSwitch6_g11758 = staticSwitch2_g11758;
				#endif
				float4 temp_output_3_0_g11762 = staticSwitch6_g11758;
				float4 temp_output_1_0_g11794 = temp_output_3_0_g11762;
				float4 appendResult91_g11794 = (float4((temp_output_1_0_g11794).rgb , 1.0));
				float2 temp_output_1_0_g11762 = finalUV146;
				#ifdef _ENABLERECOLORRGB_ON
				#ifdef _RECOLORRGBTEXTURETOGGLE_ON
				float4 staticSwitch81_g11794 = tex2D( _RecolorRGBTexture, temp_output_1_0_g11762 );
				#else
				float4 staticSwitch81_g11794 = appendResult91_g11794;
				#endif
				float3 break82_g11794 = (staticSwitch81_g11794).xyz;
				float temp_output_84_0_g11794 = max( ( break82_g11794.x + break82_g11794.y + break82_g11794.z ) , 0.001 );
				float4 break71_g11794 = ( ( _RecolorRGBRedTint * ( break82_g11794.x / temp_output_84_0_g11794 ) ) + ( _RecolorRGBGreenTint * ( break82_g11794.y / temp_output_84_0_g11794 ) ) + ( ( break82_g11794.z / temp_output_84_0_g11794 ) * _RecolorRGBBlueTint ) );
				float3 appendResult56_g11794 = (float3(break71_g11794.r , break71_g11794.g , break71_g11794.b));
				float4 break2_g11795 = temp_output_1_0_g11794;
				float saferPower57_g11794 = abs( ( ( break2_g11795.x + break2_g11795.x + break2_g11795.y + break2_g11795.y + break2_g11795.y + break2_g11795.z ) / 6.0 ) );
				float3 lerpResult26_g11794 = lerp( (temp_output_1_0_g11794).rgb , ( appendResult56_g11794 * pow( saferPower57_g11794 , ( max( break71_g11794.a , 0.01 ) * 2.0 ) ) ) , ( staticSwitch81_g11794.w * _RecolorRGBFade ));
				float4 appendResult30_g11794 = (float4(lerpResult26_g11794 , temp_output_1_0_g11794.a));
				float4 staticSwitch43_g11762 = appendResult30_g11794;
				#else
				float4 staticSwitch43_g11762 = temp_output_3_0_g11762;
				#endif
				#ifdef _ENABLERECOLORRGBYCP_ON
				float4 temp_output_1_0_g11792 = staticSwitch43_g11762;
				#ifdef _RECOLORRGBYCPTEXTURETOGGLE_ON
				float4 staticSwitch62_g11792 = tex2D( _RecolorRGBYCPTexture, temp_output_1_0_g11762 );
				#else
				float4 staticSwitch62_g11792 = temp_output_1_0_g11792;
				#endif
				float3 hsvTorgb33_g11792 = RGBToHSV( staticSwitch62_g11792.rgb );
				float temp_output_43_0_g11792 = ( ( hsvTorgb33_g11792.x + 0.08333334 ) % 1.0 );
				float4 ifLocalVar46_g11792 = 0;
				if( temp_output_43_0_g11792 >= 0.8333333 )
				ifLocalVar46_g11792 = _RecolorRGBYCPPurpleTint;
				else
				ifLocalVar46_g11792 = _RecolorRGBYCPBlueTint;
				float4 ifLocalVar44_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.6666667 )
				ifLocalVar44_g11792 = _RecolorRGBYCPCyanTint;
				else
				ifLocalVar44_g11792 = ifLocalVar46_g11792;
				float4 ifLocalVar47_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.3333333 )
				ifLocalVar47_g11792 = _RecolorRGBYCPYellowTint;
				else
				ifLocalVar47_g11792 = _RecolorRGBYCPGreenTint;
				float4 ifLocalVar45_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.1666667 )
				ifLocalVar45_g11792 = _RecolorRGBYCPRedTint;
				else
				ifLocalVar45_g11792 = ifLocalVar47_g11792;
				float4 ifLocalVar35_g11792 = 0;
				if( temp_output_43_0_g11792 >= 0.5 )
				ifLocalVar35_g11792 = ifLocalVar44_g11792;
				else
				ifLocalVar35_g11792 = ifLocalVar45_g11792;
				float4 break55_g11792 = ifLocalVar35_g11792;
				float3 appendResult56_g11792 = (float3(break55_g11792.r , break55_g11792.g , break55_g11792.b));
				float4 break2_g11793 = temp_output_1_0_g11792;
				float saferPower57_g11792 = abs( ( ( break2_g11793.x + break2_g11793.x + break2_g11793.y + break2_g11793.y + break2_g11793.y + break2_g11793.z ) / 6.0 ) );
				float3 lerpResult26_g11792 = lerp( (temp_output_1_0_g11792).rgb , ( appendResult56_g11792 * pow( saferPower57_g11792 , max( ( break55_g11792.a * 2.0 ) , 0.01 ) ) ) , ( hsvTorgb33_g11792.z * _RecolorRGBYCPFade ));
				float4 appendResult30_g11792 = (float4(lerpResult26_g11792 , temp_output_1_0_g11792.a));
				float4 staticSwitch9_g11762 = appendResult30_g11792;
				#else
				float4 staticSwitch9_g11762 = staticSwitch43_g11762;
				#endif
				#ifdef _ENABLECOLORREPLACE_ON
				float4 temp_output_1_0_g11766 = staticSwitch9_g11762;
				float3 temp_output_2_0_g11766 = (temp_output_1_0_g11766).rgb;
				float3 In115_g11766 = temp_output_2_0_g11766;
				float3 From115_g11766 = (_ColorReplaceFromColor).rgb;
				float4 break2_g11767 = temp_output_1_0_g11766;
				float3 To115_g11766 = ( pow( ( ( break2_g11767.x + break2_g11767.x + break2_g11767.y + break2_g11767.y + break2_g11767.y + break2_g11767.z ) / 6.0 ) , max( _ColorReplaceContrast , 0.001 ) ) * (_ColorReplaceToColor).rgb );
				float Fuzziness115_g11766 = _ColorReplaceSmoothness;
				float Range115_g11766 = _ColorReplaceRange;
				float3 localMyCustomExpression115_g11766 = MyCustomExpression115_g11766( In115_g11766 , From115_g11766 , To115_g11766 , Fuzziness115_g11766 , Range115_g11766 );
				float3 lerpResult112_g11766 = lerp( temp_output_2_0_g11766 , localMyCustomExpression115_g11766 , _ColorReplaceFade);
				float4 appendResult4_g11766 = (float4(lerpResult112_g11766 , temp_output_1_0_g11766.a));
				float4 staticSwitch29_g11762 = appendResult4_g11766;
				#else
				float4 staticSwitch29_g11762 = staticSwitch9_g11762;
				#endif
				float4 temp_output_1_0_g11779 = staticSwitch29_g11762;
				#ifdef _ENABLENEGATIVE_ON
				float3 temp_output_9_0_g11779 = (temp_output_1_0_g11779).rgb;
				float3 lerpResult3_g11779 = lerp( temp_output_9_0_g11779 , ( 1.0 - temp_output_9_0_g11779 ) , _NegativeFade);
				float4 appendResult8_g11779 = (float4(lerpResult3_g11779 , temp_output_1_0_g11779.a));
				float4 staticSwitch4_g11779 = appendResult8_g11779;
				#else
				float4 staticSwitch4_g11779 = temp_output_1_0_g11779;
				#endif
				float4 temp_output_57_0_g11762 = staticSwitch4_g11779;
				#ifdef _ENABLECONTRAST_ON
				float4 temp_output_1_0_g11802 = temp_output_57_0_g11762;
				float3 temp_cast_30 = (max( _Contrast , 0.001 )).xxx;
				float4 appendResult4_g11802 = (float4(pow( (temp_output_1_0_g11802).rgb , temp_cast_30 ) , temp_output_1_0_g11802.a));
				float4 staticSwitch32_g11762 = appendResult4_g11802;
				#else
				float4 staticSwitch32_g11762 = temp_output_57_0_g11762;
				#endif
				#ifdef _ENABLEBRIGHTNESS_ON
				float4 temp_output_2_0_g11777 = staticSwitch32_g11762;
				float4 appendResult6_g11777 = (float4(( (temp_output_2_0_g11777).rgb * _Brightness ) , temp_output_2_0_g11777.a));
				float4 staticSwitch33_g11762 = appendResult6_g11777;
				#else
				float4 staticSwitch33_g11762 = staticSwitch32_g11762;
				#endif
				#ifdef _ENABLEHUE_ON
				float4 temp_output_2_0_g11778 = staticSwitch33_g11762;
				float3 hsvTorgb1_g11778 = RGBToHSV( temp_output_2_0_g11778.rgb );
				float3 hsvTorgb3_g11778 = HSVToRGB( float3(( hsvTorgb1_g11778.x + _Hue ),hsvTorgb1_g11778.y,hsvTorgb1_g11778.z) );
				float4 appendResult8_g11778 = (float4(hsvTorgb3_g11778 , temp_output_2_0_g11778.a));
				float4 staticSwitch36_g11762 = appendResult8_g11778;
				#else
				float4 staticSwitch36_g11762 = staticSwitch33_g11762;
				#endif
				#ifdef _ENABLESPLITTONING_ON
				float4 temp_output_1_0_g11796 = staticSwitch36_g11762;
				float4 break2_g11797 = temp_output_1_0_g11796;
				float temp_output_3_0_g11796 = ( ( break2_g11797.x + break2_g11797.x + break2_g11797.y + break2_g11797.y + break2_g11797.y + break2_g11797.z ) / 6.0 );
				float clampResult25_g11796 = clamp( ( ( ( ( temp_output_3_0_g11796 + _SplitToningShift ) - 0.5 ) * _SplitToningBalance ) + 0.5 ) , 0.0 , 1.0 );
				float3 lerpResult6_g11796 = lerp( (_SplitToningShadowsColor).rgb , (_SplitToningHighlightsColor).rgb , clampResult25_g11796);
				float3 lerpResult11_g11796 = lerp( (temp_output_1_0_g11796).rgb , ( lerpResult6_g11796 * pow( temp_output_3_0_g11796 , max( _SplitToningContrast , 0.001 ) ) ) , _SplitToningFade);
				float4 appendResult18_g11796 = (float4(lerpResult11_g11796 , temp_output_1_0_g11796.a));
				float4 staticSwitch30_g11762 = appendResult18_g11796;
				#else
				float4 staticSwitch30_g11762 = staticSwitch36_g11762;
				#endif
				#ifdef _ENABLEBLACKTINT_ON
				float4 temp_output_1_0_g11774 = staticSwitch30_g11762;
				float3 temp_output_4_0_g11774 = (temp_output_1_0_g11774).rgb;
				float4 break12_g11774 = temp_output_1_0_g11774;
				float3 lerpResult7_g11774 = lerp( temp_output_4_0_g11774 , ( temp_output_4_0_g11774 + (_BlackTintColor).rgb ) , pow( ( 1.0 - min( max( max( break12_g11774.r , break12_g11774.g ) , break12_g11774.b ) , 1.0 ) ) , max( _BlackTintPower , 0.001 ) ));
				float3 lerpResult13_g11774 = lerp( temp_output_4_0_g11774 , lerpResult7_g11774 , _BlackTintFade);
				float4 appendResult11_g11774 = (float4(lerpResult13_g11774 , break12_g11774.a));
				float4 staticSwitch20_g11762 = appendResult11_g11774;
				#else
				float4 staticSwitch20_g11762 = staticSwitch30_g11762;
				#endif
				#ifdef _ENABLEINKSPREAD_ON
				float4 temp_output_1_0_g11788 = staticSwitch20_g11762;
				float4 break2_g11790 = temp_output_1_0_g11788;
				float2 temp_output_65_0_g11788 = shaderPosition235;
				float linValue16_g11789 = tex2D( _UberNoiseTexture, ( temp_output_65_0_g11788 * _InkSpreadNoiseScale ) ).r;
				float localMyCustomExpression16_g11789 = MyCustomExpression16_g11789( linValue16_g11789 );
				float clampResult53_g11788 = clamp( ( ( ( _InkSpreadDistance - distance( _InkSpreadPosition , temp_output_65_0_g11788 ) ) + ( localMyCustomExpression16_g11789 * _InkSpreadNoiseFactor ) ) / max( _InkSpreadWidth , 0.001 ) ) , 0.0 , 1.0 );
				float3 lerpResult7_g11788 = lerp( (temp_output_1_0_g11788).rgb , ( (_InkSpreadColor).rgb * pow( ( ( break2_g11790.x + break2_g11790.x + break2_g11790.y + break2_g11790.y + break2_g11790.y + break2_g11790.z ) / 6.0 ) , max( _InkSpreadContrast , 0.001 ) ) ) , ( _InkSpreadFade * clampResult53_g11788 ));
				float4 appendResult9_g11788 = (float4(lerpResult7_g11788 , (temp_output_1_0_g11788).a));
				float4 staticSwitch17_g11762 = appendResult9_g11788;
				#else
				float4 staticSwitch17_g11762 = staticSwitch20_g11762;
				#endif
				float temp_output_39_0_g11762 = shaderTime237;
				#ifdef _ENABLESHIFTHUE_ON
				float4 temp_output_1_0_g11780 = staticSwitch17_g11762;
				float3 hsvTorgb15_g11780 = RGBToHSV( (temp_output_1_0_g11780).rgb );
				float3 hsvTorgb19_g11780 = HSVToRGB( float3(( ( temp_output_39_0_g11762 * _ShiftHueSpeed ) + hsvTorgb15_g11780.x ),hsvTorgb15_g11780.y,hsvTorgb15_g11780.z) );
				float4 appendResult6_g11780 = (float4(hsvTorgb19_g11780 , temp_output_1_0_g11780.a));
				float4 staticSwitch19_g11762 = appendResult6_g11780;
				#else
				float4 staticSwitch19_g11762 = staticSwitch17_g11762;
				#endif
				#ifdef _ENABLEADDHUE_ON
				float3 hsvTorgb19_g11784 = HSVToRGB( float3(( ( temp_output_39_0_g11762 * _AddHueSpeed ) % 1.0 ),_AddHueSaturation,_AddHueBrightness) );
				float4 temp_output_1_0_g11784 = staticSwitch19_g11762;
				float4 break2_g11786 = temp_output_1_0_g11784;
				float2 uv_AddHueMask = IN.texCoord0.xy * _AddHueMask_ST.xy + _AddHueMask_ST.zw;
				float4 tex2DNode3_g11785 = tex2D( _AddHueMask, uv_AddHueMask );
				#ifdef _ADDHUEMASKTOGGLE_ON
				float staticSwitch33_g11784 = ( _AddHueFade * ( tex2DNode3_g11785.r * tex2DNode3_g11785.a ) );
				#else
				float staticSwitch33_g11784 = _AddHueFade;
				#endif
				float4 appendResult6_g11784 = (float4(( ( hsvTorgb19_g11784 * pow( ( ( break2_g11786.x + break2_g11786.x + break2_g11786.y + break2_g11786.y + break2_g11786.y + break2_g11786.z ) / 6.0 ) , max( _AddHueContrast , 0.001 ) ) * staticSwitch33_g11784 ) + (temp_output_1_0_g11784).rgb ) , temp_output_1_0_g11784.a));
				float4 staticSwitch23_g11762 = appendResult6_g11784;
				#else
				float4 staticSwitch23_g11762 = staticSwitch19_g11762;
				#endif
				#ifdef _ENABLESINEGLOW_ON
				float4 temp_output_1_0_g11781 = staticSwitch23_g11762;
				float4 break2_g11782 = temp_output_1_0_g11781;
				float3 temp_output_13_0_g11781 = (_SineGlowColor).rgb;
				float2 uv_SineGlowMask = IN.texCoord0.xy * _SineGlowMask_ST.xy + _SineGlowMask_ST.zw;
				float4 tex2DNode30_g11781 = tex2D( _SineGlowMask, uv_SineGlowMask );
				#ifdef _SINEGLOWMASKTOGGLE_ON
				float3 staticSwitch27_g11781 = ( (tex2DNode30_g11781).rgb * temp_output_13_0_g11781 * tex2DNode30_g11781.a );
				#else
				float3 staticSwitch27_g11781 = temp_output_13_0_g11781;
				#endif
				float4 appendResult21_g11781 = (float4(( (temp_output_1_0_g11781).rgb + ( pow( ( ( break2_g11782.x + break2_g11782.x + break2_g11782.y + break2_g11782.y + break2_g11782.y + break2_g11782.z ) / 6.0 ) , max( _SineGlowContrast , 0.001 ) ) * staticSwitch27_g11781 * _SineGlowFade * ( ( ( sin( ( temp_output_39_0_g11762 * _SineGlowFrequency ) ) + 1.0 ) * ( _SineGlowMax - _SineGlowMin ) ) + _SineGlowMin ) ) ) , temp_output_1_0_g11781.a));
				float4 staticSwitch28_g11762 = appendResult21_g11781;
				#else
				float4 staticSwitch28_g11762 = staticSwitch23_g11762;
				#endif
				#ifdef _ENABLESATURATION_ON
				float4 temp_output_1_0_g11769 = staticSwitch28_g11762;
				float4 break2_g11770 = temp_output_1_0_g11769;
				float3 temp_cast_46 = (( ( break2_g11770.x + break2_g11770.x + break2_g11770.y + break2_g11770.y + break2_g11770.y + break2_g11770.z ) / 6.0 )).xxx;
				float3 lerpResult5_g11769 = lerp( temp_cast_46 , (temp_output_1_0_g11769).rgb , _Saturation);
				float4 appendResult8_g11769 = (float4(lerpResult5_g11769 , temp_output_1_0_g11769.a));
				float4 staticSwitch38_g11762 = appendResult8_g11769;
				#else
				float4 staticSwitch38_g11762 = staticSwitch28_g11762;
				#endif
				#ifdef _ENABLEINNEROUTLINE_ON
				float4 temp_output_15_0_g11771 = staticSwitch38_g11762;
				float3 temp_output_82_0_g11771 = (_InnerOutlineColor).rgb;
				float2 temp_output_7_0_g11771 = temp_output_1_0_g11762;
				float temp_output_179_0_g11771 = temp_output_39_0_g11762;
				#ifdef _INNEROUTLINETEXTURETOGGLE_ON
				float3 staticSwitch187_g11771 = ( (tex2D( _InnerOutlineTintTexture, ( temp_output_7_0_g11771 + ( _InnerOutlineTextureSpeed * temp_output_179_0_g11771 ) ) )).rgb * temp_output_82_0_g11771 );
				#else
				float3 staticSwitch187_g11771 = temp_output_82_0_g11771;
				#endif
				float linValue16_g11773 = tex2D( _UberNoiseTexture, ( ( ( temp_output_179_0_g11771 * _InnerOutlineNoiseSpeed ) + temp_output_7_0_g11771 ) * _InnerOutlineNoiseScale ) ).r;
				float localMyCustomExpression16_g11773 = MyCustomExpression16_g11773( linValue16_g11773 );
				#ifdef _INNEROUTLINEDISTORTIONTOGGLE_ON
				float2 staticSwitch169_g11771 = ( ( localMyCustomExpression16_g11773 - 0.5 ) * _InnerOutlineDistortionIntensity );
				#else
				float2 staticSwitch169_g11771 = float2( 0,0 );
				#endif
				float2 temp_output_131_0_g11771 = ( staticSwitch169_g11771 + temp_output_7_0_g11771 );
				float2 appendResult2_g11772 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_25_0_g11771 = ( 100.0 / appendResult2_g11772 );
				float temp_output_178_0_g11771 = ( _InnerOutlineFade * ( 1.0 - min( min( min( min( min( min( min( tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0,-1 ) ) * temp_output_25_0_g11771 ) ) ).a , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0,1 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -1,0 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 1,0 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0.705,0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -0.705,0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0.705,-0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -0.705,-0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) ) );
				float3 lerpResult176_g11771 = lerp( (temp_output_15_0_g11771).rgb , staticSwitch187_g11771 , temp_output_178_0_g11771);
				#ifdef _INNEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch188_g11771 = ( temp_output_178_0_g11771 * temp_output_15_0_g11771.a );
				#else
				float staticSwitch188_g11771 = temp_output_15_0_g11771.a;
				#endif
				float4 appendResult177_g11771 = (float4(lerpResult176_g11771 , staticSwitch188_g11771));
				float4 staticSwitch12_g11762 = appendResult177_g11771;
				#else
				float4 staticSwitch12_g11762 = staticSwitch38_g11762;
				#endif
				#ifdef _ENABLEOUTEROUTLINE_ON
				float4 temp_output_15_0_g11799 = staticSwitch12_g11762;
				float3 temp_output_82_0_g11799 = (_OuterOutlineColor).rgb;
				float2 temp_output_7_0_g11799 = temp_output_1_0_g11762;
				float temp_output_186_0_g11799 = temp_output_39_0_g11762;
				#ifdef _OUTEROUTLINETEXTURETOGGLE_ON
				float3 staticSwitch199_g11799 = ( (tex2D( _OuterOutlineTintTexture, ( temp_output_7_0_g11799 + ( _OuterOutlineTextureSpeed * temp_output_186_0_g11799 ) ) )).rgb * temp_output_82_0_g11799 );
				#else
				float3 staticSwitch199_g11799 = temp_output_82_0_g11799;
				#endif
				float temp_output_182_0_g11799 = ( ( 1.0 - temp_output_15_0_g11799.a ) * min( ( _OuterOutlineFade * 3.0 ) , 1.0 ) );
				#ifdef _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch203_g11799 = 1.0;
				#else
				float staticSwitch203_g11799 = temp_output_182_0_g11799;
				#endif
				float3 lerpResult178_g11799 = lerp( (temp_output_15_0_g11799).rgb , staticSwitch199_g11799 , staticSwitch203_g11799);
				float3 lerpResult170_g11799 = lerp( lerpResult178_g11799 , staticSwitch199_g11799 , staticSwitch203_g11799);
				float linValue16_g11800 = tex2D( _UberNoiseTexture, ( ( ( temp_output_186_0_g11799 * _OuterOutlineNoiseSpeed ) + temp_output_7_0_g11799 ) * _OuterOutlineNoiseScale ) ).r;
				float localMyCustomExpression16_g11800 = MyCustomExpression16_g11800( linValue16_g11800 );
				#ifdef _OUTEROUTLINEDISTORTIONTOGGLE_ON
				float2 staticSwitch157_g11799 = ( ( localMyCustomExpression16_g11800 - 0.5 ) * _OuterOutlineDistortionIntensity );
				#else
				float2 staticSwitch157_g11799 = float2( 0,0 );
				#endif
				float2 temp_output_131_0_g11799 = ( staticSwitch157_g11799 + temp_output_7_0_g11799 );
				float2 appendResult2_g11801 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_25_0_g11799 = ( 100.0 / appendResult2_g11801 );
				float lerpResult168_g11799 = lerp( temp_output_15_0_g11799.a , min( ( max( max( max( max( max( max( max( tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0,-1 ) ) * temp_output_25_0_g11799 ) ) ).a , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0,1 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -1,0 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 1,0 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0.705,0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -0.705,0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0.705,-0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -0.705,-0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) * 3.0 ) , 1.0 ) , _OuterOutlineFade);
				#ifdef _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch200_g11799 = ( temp_output_182_0_g11799 * lerpResult168_g11799 );
				#else
				float staticSwitch200_g11799 = lerpResult168_g11799;
				#endif
				float4 appendResult174_g11799 = (float4(lerpResult170_g11799 , staticSwitch200_g11799));
				float4 staticSwitch13_g11762 = appendResult174_g11799;
				#else
				float4 staticSwitch13_g11762 = staticSwitch12_g11762;
				#endif
				#ifdef _ENABLEPIXELOUTLINE_ON
				float4 temp_output_15_0_g11776 = staticSwitch13_g11762;
				float3 temp_output_82_0_g11776 = (_PixelOutlineColor).rgb;
				float2 temp_output_7_0_g11776 = temp_output_1_0_g11762;
				#ifdef _PIXELOUTLINETEXTURETOGGLE_ON
				float3 staticSwitch199_g11776 = ( (tex2D( _PixelOutlineTintTexture, ( temp_output_7_0_g11776 + ( _PixelOutlineTextureSpeed * temp_output_39_0_g11762 ) ) )).rgb * temp_output_82_0_g11776 );
				#else
				float3 staticSwitch199_g11776 = temp_output_82_0_g11776;
				#endif
				float temp_output_182_0_g11776 = ( ( 1.0 - temp_output_15_0_g11776.a ) * min( ( _PixelOutlineFade * 3.0 ) , 1.0 ) );
				#ifdef _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch203_g11776 = 1.0;
				#else
				float staticSwitch203_g11776 = temp_output_182_0_g11776;
				#endif
				float3 lerpResult178_g11776 = lerp( (temp_output_15_0_g11776).rgb , staticSwitch199_g11776 , staticSwitch203_g11776);
				float3 lerpResult170_g11776 = lerp( lerpResult178_g11776 , staticSwitch199_g11776 , staticSwitch203_g11776);
				float2 appendResult206_g11776 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_209_0_g11776 = ( float2( 1,1 ) / appendResult206_g11776 );
				float lerpResult168_g11776 = lerp( temp_output_15_0_g11776.a , min( ( max( max( max( tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 0,-1 ) ) * temp_output_209_0_g11776 ) ) ).a , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 0,1 ) ) * temp_output_209_0_g11776 ) ) ).a ) , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( -1,0 ) ) * temp_output_209_0_g11776 ) ) ).a ) , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 1,0 ) ) * temp_output_209_0_g11776 ) ) ).a ) * 3.0 ) , 1.0 ) , _PixelOutlineFade);
				#ifdef _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch200_g11776 = ( temp_output_182_0_g11776 * lerpResult168_g11776 );
				#else
				float staticSwitch200_g11776 = lerpResult168_g11776;
				#endif
				float4 appendResult174_g11776 = (float4(lerpResult170_g11776 , staticSwitch200_g11776));
				float4 staticSwitch48_g11762 = appendResult174_g11776;
				#else
				float4 staticSwitch48_g11762 = staticSwitch13_g11762;
				#endif
				#ifdef _ENABLEPINGPONGGLOW_ON
				float3 lerpResult15_g11763 = lerp( (_PingPongGlowFrom).rgb , (_PingPongGlowTo).rgb , ( ( sin( ( temp_output_39_0_g11762 * _PingPongGlowFrequency ) ) + 1.0 ) / 2.0 ));
				float4 temp_output_5_0_g11763 = staticSwitch48_g11762;
				float4 break2_g11764 = temp_output_5_0_g11763;
				float4 appendResult12_g11763 = (float4(( ( lerpResult15_g11763 * _PingPongGlowFade * pow( ( ( break2_g11764.x + break2_g11764.x + break2_g11764.y + break2_g11764.y + break2_g11764.y + break2_g11764.z ) / 6.0 ) , max( _PingPongGlowContrast , 0.001 ) ) ) + (temp_output_5_0_g11763).rgb ) , temp_output_5_0_g11763.a));
				float4 staticSwitch46_g11762 = appendResult12_g11763;
				#else
				float4 staticSwitch46_g11762 = staticSwitch48_g11762;
				#endif
				float4 temp_output_361_0 = staticSwitch46_g11762;
				#ifdef _ENABLEHOLOGRAM_ON
				float4 temp_output_1_0_g11804 = temp_output_361_0;
				float4 break2_g11805 = temp_output_1_0_g11804;
				float4 appendResult22_g11804 = (float4(( (_HologramTint).rgb * pow( ( ( break2_g11805.x + break2_g11805.x + break2_g11805.y + break2_g11805.y + break2_g11805.y + break2_g11805.z ) / 6.0 ) , max( _HologramContrast , 0.001 ) ) ) , ( max( pow( abs( sin( ( ( ( ( shaderTime237 * _HologramLineSpeed ) + positionWS.y ) / unity_OrthoParams.y ) * _HologramLineFrequency ) ) ) , _HologramLineGap ) , _HologramMinAlpha ) * temp_output_1_0_g11804.a )));
				float4 lerpResult37_g11804 = lerp( temp_output_1_0_g11804 , appendResult22_g11804 , hologramFade182);
				float4 staticSwitch56 = lerpResult37_g11804;
				#else
				float4 staticSwitch56 = temp_output_361_0;
				#endif
				#ifdef _ENABLEGLITCH_ON
				float4 temp_output_1_0_g11807 = staticSwitch56;
				float4 break2_g11809 = temp_output_1_0_g11807;
				float temp_output_34_0_g11807 = shaderTime237;
				float linValue16_g11808 = tex2D( _UberNoiseTexture, ( ( glitchPosition154 + ( _GlitchNoiseSpeed * temp_output_34_0_g11807 ) ) * _GlitchNoiseScale ) ).r;
				float localMyCustomExpression16_g11808 = MyCustomExpression16_g11808( linValue16_g11808 );
				float3 hsvTorgb3_g11810 = HSVToRGB( float3(( localMyCustomExpression16_g11808 + ( temp_output_34_0_g11807 * _GlitchHueSpeed ) ),1.0,1.0) );
				float3 lerpResult23_g11807 = lerp( (temp_output_1_0_g11807).rgb , ( ( ( break2_g11809.x + break2_g11809.x + break2_g11809.y + break2_g11809.y + break2_g11809.y + break2_g11809.z ) / 6.0 ) * _GlitchBrightness * hsvTorgb3_g11810 ) , glitchFade152);
				float4 appendResult27_g11807 = (float4(lerpResult23_g11807 , temp_output_1_0_g11807.a));
				float4 staticSwitch57 = appendResult27_g11807;
				#else
				float4 staticSwitch57 = staticSwitch56;
				#endif
				float4 temp_output_3_0_g11811 = staticSwitch57;
				float4 temp_output_1_0_g11841 = temp_output_3_0_g11811;
				float2 temp_output_41_0_g11811 = shaderPosition235;
				float2 temp_output_99_0_g11841 = temp_output_41_0_g11811;
				float temp_output_40_0_g11811 = shaderTime237;
				#ifdef _ENABLECAMOUFLAGE_ON
				float linValue16_g11846 = tex2D( _UberNoiseTexture, ( ( ( temp_output_40_0_g11811 * _CamouflageDistortionSpeed ) + temp_output_99_0_g11841 ) * _CamouflageDistortionScale ) ).r;
				float localMyCustomExpression16_g11846 = MyCustomExpression16_g11846( linValue16_g11846 );
				#ifdef _CAMOUFLAGEANIMATIONTOGGLE_ON
				float2 staticSwitch101_g11841 = ( ( ( localMyCustomExpression16_g11846 - 0.25 ) * _CamouflageDistortionIntensity ) + temp_output_99_0_g11841 );
				#else
				float2 staticSwitch101_g11841 = temp_output_99_0_g11841;
				#endif
				float linValue16_g11843 = tex2D( _UberNoiseTexture, ( staticSwitch101_g11841 * _CamouflageNoiseScaleA ) ).r;
				float localMyCustomExpression16_g11843 = MyCustomExpression16_g11843( linValue16_g11843 );
				float clampResult52_g11841 = clamp( ( ( _CamouflageDensityA - localMyCustomExpression16_g11843 ) / max( _CamouflageSmoothnessA , 0.005 ) ) , 0.0 , 1.0 );
				float4 lerpResult55_g11841 = lerp( _CamouflageBaseColor , ( _CamouflageColorA * clampResult52_g11841 ) , clampResult52_g11841);
				float linValue16_g11845 = tex2D( _UberNoiseTexture, ( ( staticSwitch101_g11841 + float2( 12.3,12.3 ) ) * _CamouflageNoiseScaleB ) ).r;
				float localMyCustomExpression16_g11845 = MyCustomExpression16_g11845( linValue16_g11845 );
				float clampResult65_g11841 = clamp( ( ( _CamouflageDensityB - localMyCustomExpression16_g11845 ) / max( _CamouflageSmoothnessB , 0.005 ) ) , 0.0 , 1.0 );
				float4 lerpResult68_g11841 = lerp( lerpResult55_g11841 , ( _CamouflageColorB * clampResult65_g11841 ) , clampResult65_g11841);
				float4 break2_g11844 = temp_output_1_0_g11841;
				float3 lerpResult4_g11841 = lerp( (temp_output_1_0_g11841).rgb , ( (lerpResult68_g11841).rgb * pow( ( ( break2_g11844.x + break2_g11844.x + break2_g11844.y + break2_g11844.y + break2_g11844.y + break2_g11844.z ) / 6.0 ) , max( _CamouflageContrast , 0.001 ) ) ) , _CamouflageFade);
				float4 appendResult7_g11841 = (float4(lerpResult4_g11841 , temp_output_1_0_g11841.a));
				float4 staticSwitch26_g11811 = appendResult7_g11841;
				#else
				float4 staticSwitch26_g11811 = temp_output_3_0_g11811;
				#endif
				#ifdef _ENABLEMETAL_ON
				float4 temp_output_1_0_g11834 = staticSwitch26_g11811;
				float temp_output_59_0_g11834 = temp_output_40_0_g11811;
				float2 temp_output_58_0_g11834 = temp_output_41_0_g11811;
				float linValue16_g11835 = tex2D( _UberNoiseTexture, ( ( ( temp_output_59_0_g11834 * _MetalNoiseDistortionSpeed ) + temp_output_58_0_g11834 ) * _MetalNoiseDistortionScale ) ).r;
				float localMyCustomExpression16_g11835 = MyCustomExpression16_g11835( linValue16_g11835 );
				float linValue16_g11837 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11835 - 0.25 ) * _MetalNoiseDistortion ) + ( ( temp_output_59_0_g11834 * _MetalNoiseSpeed ) + temp_output_58_0_g11834 ) ) * _MetalNoiseScale ) ).r;
				float localMyCustomExpression16_g11837 = MyCustomExpression16_g11837( linValue16_g11837 );
				float4 break2_g11836 = temp_output_1_0_g11834;
				float temp_output_5_0_g11834 = ( ( break2_g11836.x + break2_g11836.x + break2_g11836.y + break2_g11836.y + break2_g11836.y + break2_g11836.z ) / 6.0 );
				float2 uv_MetalMask = IN.texCoord0.xy * _MetalMask_ST.xy + _MetalMask_ST.zw;
				float4 tex2DNode3_g11839 = tex2D( _MetalMask, uv_MetalMask );
				#ifdef _METALMASKTOGGLE_ON
				float staticSwitch60_g11834 = ( _MetalFade * ( tex2DNode3_g11839.r * tex2DNode3_g11839.a ) );
				#else
				float staticSwitch60_g11834 = _MetalFade;
				#endif
				float4 lerpResult45_g11834 = lerp( temp_output_1_0_g11834 , ( ( max( ( ( _MetalHighlightDensity - localMyCustomExpression16_g11837 ) / max( _MetalHighlightDensity , 0.01 ) ) , 0.0 ) * _MetalHighlightColor * pow( temp_output_5_0_g11834 , max( _MetalHighlightContrast , 0.001 ) ) ) + ( pow( temp_output_5_0_g11834 , max( _MetalContrast , 0.001 ) ) * _MetalColor ) ) , staticSwitch60_g11834);
				float4 appendResult8_g11834 = (float4((lerpResult45_g11834).rgb , (temp_output_1_0_g11834).a));
				float4 staticSwitch28_g11811 = appendResult8_g11834;
				#else
				float4 staticSwitch28_g11811 = staticSwitch26_g11811;
				#endif
				#ifdef _ENABLEFROZEN_ON
				float4 temp_output_1_0_g11826 = staticSwitch28_g11811;
				float4 break2_g11827 = temp_output_1_0_g11826;
				float temp_output_7_0_g11826 = ( ( break2_g11827.x + break2_g11827.x + break2_g11827.y + break2_g11827.y + break2_g11827.y + break2_g11827.z ) / 6.0 );
				float2 temp_output_72_0_g11826 = temp_output_41_0_g11811;
				float linValue16_g11828 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11826 * _FrozenSnowScale ) ).r;
				float localMyCustomExpression16_g11828 = MyCustomExpression16_g11828( linValue16_g11828 );
				float temp_output_73_0_g11826 = temp_output_40_0_g11811;
				float linValue16_g11830 = tex2D( _UberNoiseTexture, ( ( ( temp_output_73_0_g11826 * _FrozenHighlightDistortionSpeed ) + temp_output_72_0_g11826 ) * _FrozenHighlightDistortionScale ) ).r;
				float localMyCustomExpression16_g11830 = MyCustomExpression16_g11830( linValue16_g11830 );
				float linValue16_g11831 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11830 - 0.25 ) * _FrozenHighlightDistortion ) + ( ( temp_output_73_0_g11826 * _FrozenHighlightSpeed ) + temp_output_72_0_g11826 ) ) * _FrozenHighlightScale ) ).r;
				float localMyCustomExpression16_g11831 = MyCustomExpression16_g11831( linValue16_g11831 );
				float3 lerpResult57_g11826 = lerp( (temp_output_1_0_g11826).rgb , ( ( pow( temp_output_7_0_g11826 , max( _FrozenContrast , 0.001 ) ) * (_FrozenTint).rgb ) + ( pow( temp_output_7_0_g11826 , max( _FrozenSnowContrast , 0.001 ) ) * ( (_FrozenSnowColor).rgb * max( ( _FrozenSnowDensity - localMyCustomExpression16_g11828 ) , 0.0 ) ) ) + (( max( ( ( _FrozenHighlightDensity - localMyCustomExpression16_g11831 ) / max( _FrozenHighlightDensity , 0.01 ) ) , 0.0 ) * _FrozenHighlightColor * pow( temp_output_7_0_g11826 , max( _FrozenHighlightContrast , 0.001 ) ) )).rgb ) , _FrozenFade);
				float4 appendResult26_g11826 = (float4(lerpResult57_g11826 , temp_output_1_0_g11826.a));
				float4 staticSwitch29_g11811 = appendResult26_g11826;
				#else
				float4 staticSwitch29_g11811 = staticSwitch28_g11811;
				#endif
				#ifdef _ENABLEBURN_ON
				float4 temp_output_1_0_g11820 = staticSwitch29_g11811;
				float3 temp_output_28_0_g11820 = (temp_output_1_0_g11820).rgb;
				float4 break2_g11824 = float4( temp_output_28_0_g11820 , 0.0 );
				float2 temp_output_72_0_g11820 = temp_output_41_0_g11811;
				float linValue16_g11823 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11820 * _BurnSwirlNoiseScale ) ).r;
				float localMyCustomExpression16_g11823 = MyCustomExpression16_g11823( linValue16_g11823 );
				float linValue16_g11821 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11823 - 0.5 ) * float2( 1,1 ) * _BurnSwirlFactor ) + temp_output_72_0_g11820 ) * _BurnInsideNoiseScale ) ).r;
				float localMyCustomExpression16_g11821 = MyCustomExpression16_g11821( linValue16_g11821 );
				float clampResult68_g11820 = clamp( ( _BurnInsideNoiseFactor - localMyCustomExpression16_g11821 ) , 0.0 , 1.0 );
				float linValue16_g11822 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11820 * _BurnEdgeNoiseScale ) ).r;
				float localMyCustomExpression16_g11822 = MyCustomExpression16_g11822( linValue16_g11822 );
				float temp_output_15_0_g11820 = ( ( ( _BurnRadius - distance( temp_output_72_0_g11820 , _BurnPosition ) ) + ( localMyCustomExpression16_g11822 * _BurnEdgeNoiseFactor ) ) / max( _BurnWidth , 0.01 ) );
				float clampResult18_g11820 = clamp( temp_output_15_0_g11820 , 0.0 , 1.0 );
				float3 lerpResult29_g11820 = lerp( temp_output_28_0_g11820 , ( pow( ( ( break2_g11824.x + break2_g11824.x + break2_g11824.y + break2_g11824.y + break2_g11824.y + break2_g11824.z ) / 6.0 ) , max( _BurnInsideContrast , 0.001 ) ) * ( ( (_BurnInsideNoiseColor).rgb * clampResult68_g11820 ) + (_BurnInsideColor).rgb ) ) , clampResult18_g11820);
				float3 lerpResult40_g11820 = lerp( temp_output_28_0_g11820 , ( lerpResult29_g11820 + ( ( step( temp_output_15_0_g11820 , 1.0 ) * step( 0.0 , temp_output_15_0_g11820 ) ) * (_BurnEdgeColor).rgb ) ) , _BurnFade);
				float4 appendResult43_g11820 = (float4(lerpResult40_g11820 , temp_output_1_0_g11820.a));
				float4 staticSwitch32_g11811 = appendResult43_g11820;
				#else
				float4 staticSwitch32_g11811 = staticSwitch29_g11811;
				#endif
				#ifdef _ENABLERAINBOW_ON
				float2 temp_output_42_0_g11816 = temp_output_41_0_g11811;
				float linValue16_g11817 = tex2D( _UberNoiseTexture, ( temp_output_42_0_g11816 * _RainbowNoiseScale ) ).r;
				float localMyCustomExpression16_g11817 = MyCustomExpression16_g11817( linValue16_g11817 );
				float3 hsvTorgb3_g11819 = HSVToRGB( float3(( ( ( distance( temp_output_42_0_g11816 , _RainbowCenter ) + ( localMyCustomExpression16_g11817 * _RainbowNoiseFactor ) ) * _RainbowDensity ) + ( _RainbowSpeed * temp_output_40_0_g11811 ) ),1.0,1.0) );
				float3 hsvTorgb36_g11816 = RGBToHSV( hsvTorgb3_g11819 );
				float3 hsvTorgb37_g11816 = HSVToRGB( float3(hsvTorgb36_g11816.x,_RainbowSaturation,( hsvTorgb36_g11816.z * _RainbowBrightness )) );
				float4 temp_output_1_0_g11816 = staticSwitch32_g11811;
				float4 break2_g11818 = temp_output_1_0_g11816;
				float saferPower24_g11816 = abs( ( ( break2_g11818.x + break2_g11818.x + break2_g11818.y + break2_g11818.y + break2_g11818.y + break2_g11818.z ) / 6.0 ) );
				float4 appendResult29_g11816 = (float4(( ( hsvTorgb37_g11816 * pow( saferPower24_g11816 , max( _RainbowContrast , 0.001 ) ) * _RainbowFade ) + (temp_output_1_0_g11816).rgb ) , temp_output_1_0_g11816.a));
				float4 staticSwitch34_g11811 = appendResult29_g11816;
				#else
				float4 staticSwitch34_g11811 = staticSwitch32_g11811;
				#endif
				#ifdef _ENABLESHINE_ON
				float4 temp_output_1_0_g11812 = staticSwitch34_g11811;
				float3 temp_output_57_0_g11812 = (temp_output_1_0_g11812).rgb;
				float4 break2_g11814 = temp_output_1_0_g11812;
				float3 temp_cast_69 = (( ( break2_g11814.x + break2_g11814.x + break2_g11814.y + break2_g11814.y + break2_g11814.y + break2_g11814.z ) / 6.0 )).xxx;
				float3 lerpResult92_g11812 = lerp( temp_cast_69 , temp_output_57_0_g11812 , _ShineSaturation);
				float3 temp_cast_70 = (max( max( _ShineContrast , 0.001 ) , 0.001 )).xxx;
				float3 rotatedValue69_g11812 = RotateAroundAxis( float3( 0,0,0 ), float3( ( _ShineFrequency * temp_output_41_0_g11811 ) ,  0.0 ), float3( 0,0,1 ), ( ( _ShineRotation / 180.0 ) * PI ) );
				float temp_output_103_0_g11812 = ( _ShineFrequency * _ShineWidth );
				float clampResult80_g11812 = clamp( ( ( ( sin( ( rotatedValue69_g11812.x - ( temp_output_40_0_g11811 * _ShineSpeed * _ShineFrequency ) ) ) - ( 1.0 - temp_output_103_0_g11812 ) ) / temp_output_103_0_g11812 ) * _ShineSmooth ) , 0.0 , 1.0 );
				float2 uv_ShineMask = IN.texCoord0.xy * _ShineMask_ST.xy + _ShineMask_ST.zw;
				float4 tex2DNode3_g11813 = tex2D( _ShineMask, uv_ShineMask );
				#ifdef _SHINEMASKTOGGLE_ON
				float staticSwitch98_g11812 = ( _ShineFade * ( tex2DNode3_g11813.r * tex2DNode3_g11813.a ) );
				#else
				float staticSwitch98_g11812 = _ShineFade;
				#endif
				float4 appendResult8_g11812 = (float4(( temp_output_57_0_g11812 + ( ( pow( lerpResult92_g11812 , temp_cast_70 ) * (_ShineColor).rgb ) * clampResult80_g11812 * staticSwitch98_g11812 ) ) , (temp_output_1_0_g11812).a));
				float4 staticSwitch36_g11811 = appendResult8_g11812;
				#else
				float4 staticSwitch36_g11811 = staticSwitch34_g11811;
				#endif
				#ifdef _ENABLEPOISON_ON
				float temp_output_41_0_g11847 = temp_output_40_0_g11811;
				float linValue16_g11849 = tex2D( _UberNoiseTexture, ( ( ( temp_output_41_0_g11847 * _PoisonNoiseSpeed ) + temp_output_41_0_g11811 ) * _PoisonNoiseScale ) ).r;
				float localMyCustomExpression16_g11849 = MyCustomExpression16_g11849( linValue16_g11849 );
				float3 temp_output_24_0_g11847 = (_PoisonColor).rgb;
				float4 temp_output_1_0_g11847 = staticSwitch36_g11811;
				float3 temp_output_28_0_g11847 = (temp_output_1_0_g11847).rgb;
				float4 break2_g11848 = float4( temp_output_28_0_g11847 , 0.0 );
				float3 lerpResult32_g11847 = lerp( temp_output_28_0_g11847 , ( temp_output_24_0_g11847 * ( ( break2_g11848.x + break2_g11848.x + break2_g11848.y + break2_g11848.y + break2_g11848.y + break2_g11848.z ) / 6.0 ) ) , ( _PoisonFade * _PoisonRecolorFactor ));
				float4 appendResult27_g11847 = (float4(( ( max( pow( abs( ( ( ( localMyCustomExpression16_g11849 + ( temp_output_41_0_g11847 * _PoisonShiftSpeed ) ) % 1.0 ) + -0.5 ) ) , max( _PoisonDensity , 0.001 ) ) , 0.0 ) * temp_output_24_0_g11847 * _PoisonFade * _PoisonNoiseBrightness ) + lerpResult32_g11847 ) , temp_output_1_0_g11847.a));
				float4 staticSwitch39_g11811 = appendResult27_g11847;
				#else
				float4 staticSwitch39_g11811 = staticSwitch36_g11811;
				#endif
				float4 temp_output_10_0_g11851 = staticSwitch39_g11811;
				#ifdef _ENABLEENCHANTED_ON
				float3 temp_output_12_0_g11851 = (temp_output_10_0_g11851).rgb;
				float2 temp_output_2_0_g11851 = temp_output_41_0_g11811;
				float temp_output_1_0_g11851 = temp_output_40_0_g11811;
				float2 temp_output_6_0_g11851 = ( temp_output_1_0_g11851 * _EnchantedSpeed );
				float linValue16_g11854 = tex2D( _UberNoiseTexture, ( ( ( temp_output_2_0_g11851 - ( ( temp_output_6_0_g11851 + float2( 1.234,5.6789 ) ) * float2( 0.95,1.05 ) ) ) * _EnchantedScale ) * float2( 1,1 ) ) ).r;
				float localMyCustomExpression16_g11854 = MyCustomExpression16_g11854( linValue16_g11854 );
				float linValue16_g11852 = tex2D( _UberNoiseTexture, ( ( ( temp_output_6_0_g11851 + temp_output_2_0_g11851 ) * _EnchantedScale ) * float2( 1,1 ) ) ).r;
				float localMyCustomExpression16_g11852 = MyCustomExpression16_g11852( linValue16_g11852 );
				float temp_output_36_0_g11851 = ( localMyCustomExpression16_g11854 + localMyCustomExpression16_g11852 );
				float temp_output_43_0_g11851 = ( temp_output_36_0_g11851 * 0.5 );
				float3 lerpResult42_g11851 = lerp( (_EnchantedLowColor).rgb , (_EnchantedHighColor).rgb , temp_output_43_0_g11851);
				float3 hsvTorgb53_g11851 = HSVToRGB( float3(( ( temp_output_43_0_g11851 * _EnchantedRainbowDensity ) + ( _EnchantedRainbowSpeed * temp_output_1_0_g11851 ) ),_EnchantedRainbowSaturation,1.0) );
				#ifdef _ENCHANTEDRAINBOWTOGGLE_ON
				float3 staticSwitch50_g11851 = hsvTorgb53_g11851;
				#else
				float3 staticSwitch50_g11851 = lerpResult42_g11851;
				#endif
				float4 break2_g11853 = temp_output_10_0_g11851;
				float3 temp_output_40_0_g11851 = ( staticSwitch50_g11851 * pow( ( ( break2_g11853.x + break2_g11853.x + break2_g11853.y + break2_g11853.y + break2_g11853.y + break2_g11853.z ) / 6.0 ) , max( _EnchantedContrast , 0.001 ) ) * _EnchantedBrightness );
				float temp_output_45_0_g11851 = ( max( ( temp_output_36_0_g11851 - _EnchantedReduce ) , 0.0 ) * _EnchantedFade );
				float3 lerpResult44_g11851 = lerp( temp_output_12_0_g11851 , temp_output_40_0_g11851 , temp_output_45_0_g11851);
				#ifdef _ENCHANTEDLERPTOGGLE_ON
				float3 staticSwitch47_g11851 = lerpResult44_g11851;
				#else
				float3 staticSwitch47_g11851 = ( temp_output_12_0_g11851 + ( temp_output_40_0_g11851 * temp_output_45_0_g11851 ) );
				#endif
				float4 appendResult19_g11851 = (float4(staticSwitch47_g11851 , temp_output_10_0_g11851.a));
				float4 staticSwitch11_g11851 = appendResult19_g11851;
				#else
				float4 staticSwitch11_g11851 = temp_output_10_0_g11851;
				#endif
				float4 temp_output_1_0_g11856 = staticSwitch11_g11851;
				#ifdef _ENABLESHIFTING_ON
				float4 break5_g11856 = temp_output_1_0_g11856;
				float3 appendResult32_g11856 = (float3(break5_g11856.r , break5_g11856.g , break5_g11856.b));
				float4 break2_g11857 = temp_output_1_0_g11856;
				float temp_output_4_0_g11856 = ( ( break2_g11857.x + break2_g11857.x + break2_g11857.y + break2_g11857.y + break2_g11857.y + break2_g11857.z ) / 6.0 );
				float temp_output_11_0_g11856 = ( ( ( temp_output_4_0_g11856 + ( temp_output_40_0_g11811 * _ShiftingSpeed ) ) * _ShiftingDensity ) % 1.0 );
				float3 lerpResult20_g11856 = lerp( (_ShiftingColorA).rgb , (_ShiftingColorB).rgb , ( abs( ( temp_output_11_0_g11856 - 0.5 ) ) * 2.0 ));
				float3 hsvTorgb12_g11856 = HSVToRGB( float3(temp_output_11_0_g11856,_ShiftingSaturation,_ShiftingBrightness) );
				#ifdef _SHIFTINGRAINBOWTOGGLE_ON
				float3 staticSwitch26_g11856 = hsvTorgb12_g11856;
				#else
				float3 staticSwitch26_g11856 = ( lerpResult20_g11856 * _ShiftingBrightness );
				#endif
				float3 lerpResult31_g11856 = lerp( appendResult32_g11856 , ( staticSwitch26_g11856 * pow( temp_output_4_0_g11856 , max( _ShiftingContrast , 0.001 ) ) ) , _ShiftingFade);
				float4 appendResult6_g11856 = (float4(lerpResult31_g11856 , break5_g11856.a));
				float4 staticSwitch33_g11856 = appendResult6_g11856;
				#else
				float4 staticSwitch33_g11856 = temp_output_1_0_g11856;
				#endif
				float4 temp_output_5_0_g11863 = staticSwitch33_g11856;
				#ifdef _ENABLETEXTURELAYER1_ON
				float4 break6_g11863 = temp_output_5_0_g11863;
				float3 appendResult11_g11863 = (float3(break6_g11863.r , break6_g11863.g , break6_g11863.b));
				float temp_output_27_0_g11863 = temp_output_40_0_g11811;
				#ifdef _TEXTURELAYER1SCROLLTOGGLE_ON
				float2 staticSwitch73_g11863 = ( _TextureLayer1Offset + ( _TextureLayer1ScrollSpeed * temp_output_27_0_g11863 ) );
				#else
				float2 staticSwitch73_g11863 = _TextureLayer1Offset;
				#endif
				float2 temp_output_72_0_g11863 = ( ( _TextureLayer1Scale * temp_output_41_0_g11811 ) - staticSwitch73_g11863 );
				float2 temp_cast_79 = (_TextureLayer1EdgeClip).xx;
				float2 UV10_g11864 = ( ( ( ( ( ( ( ( ( temp_output_72_0_g11863 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) ) - float2( 0.5,0.5 ) ) * ( float2( 1,1 ) - temp_cast_79 ) ) + float2( 0.5,0.5 ) ) % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				int Width10_g11864 = _TextureLayer1Columns;
				int Height10_g11864 = _TextureLayer1Rows;
				float Tile10_g11864 = round( ( ( _TextureLayer1Speed * temp_output_27_0_g11863 ) + _TextureLayer1StartFrame ) );
				float2 Invert10_g11864 = float2( 0,1 );
				float2 localMyCustomExpression10_g11864 = MyCustomExpression10_g11864( UV10_g11864 , Width10_g11864 , Height10_g11864 , Tile10_g11864 , Invert10_g11864 );
				#ifdef _TEXTURELAYER1SHEETTOGGLE_ON
				float2 staticSwitch20_g11863 = localMyCustomExpression10_g11864;
				#else
				float2 staticSwitch20_g11863 = temp_output_72_0_g11863;
				#endif
				float4 tex2DNode3_g11863 = tex2D( _TextureLayer1Texture, staticSwitch20_g11863 );
				float3 appendResult13_g11863 = (float3(tex2DNode3_g11863.r , tex2DNode3_g11863.g , tex2DNode3_g11863.b));
				float3 appendResult18_g11863 = (float3(_TextureLayer1Color.r , _TextureLayer1Color.g , _TextureLayer1Color.b));
				float3 temp_output_16_0_g11863 = ( appendResult13_g11863 * appendResult18_g11863 );
				float4 break2_g11865 = temp_output_5_0_g11863;
				#ifdef _TEXTURELAYER1CONTRASTTOGGLE_ON
				float3 staticSwitch80_g11863 = ( pow( ( ( break2_g11865.x + break2_g11865.x + break2_g11865.y + break2_g11865.y + break2_g11865.y + break2_g11865.z ) / 6.0 ) , max( _TextureLayer1Contrast , 0.001 ) ) * temp_output_16_0_g11863 );
				#else
				float3 staticSwitch80_g11863 = temp_output_16_0_g11863;
				#endif
				float3 lerpResult12_g11863 = lerp( appendResult11_g11863 , staticSwitch80_g11863 , ( tex2DNode3_g11863.a * _TextureLayer1Fade ));
				float4 appendResult14_g11863 = (float4(lerpResult12_g11863 , break6_g11863.a));
				float4 staticSwitch15_g11863 = appendResult14_g11863;
				#else
				float4 staticSwitch15_g11863 = temp_output_5_0_g11863;
				#endif
				float4 temp_output_5_0_g11859 = staticSwitch15_g11863;
				#ifdef _ENABLETEXTURELAYER2_ON
				float4 break6_g11859 = temp_output_5_0_g11859;
				float3 appendResult11_g11859 = (float3(break6_g11859.r , break6_g11859.g , break6_g11859.b));
				float temp_output_27_0_g11859 = temp_output_40_0_g11811;
				#ifdef _TEXTURELAYER2SCROLLTOGGLE_ON
				float2 staticSwitch73_g11859 = ( _TextureLayer2Offset + ( _TextureLayer2ScrollSpeed * temp_output_27_0_g11859 ) );
				#else
				float2 staticSwitch73_g11859 = _TextureLayer2Offset;
				#endif
				float2 temp_output_72_0_g11859 = ( ( _TextureLayer2Scale * temp_output_41_0_g11811 ) - staticSwitch73_g11859 );
				float2 temp_cast_82 = (_TextureLayer2EdgeClip).xx;
				float2 UV10_g11860 = ( ( ( ( ( ( ( ( ( temp_output_72_0_g11859 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) ) - float2( 0.5,0.5 ) ) * ( float2( 1,1 ) - temp_cast_82 ) ) + float2( 0.5,0.5 ) ) % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				int Width10_g11860 = _TextureLayer2Columns;
				int Height10_g11860 = _TextureLayer2Rows;
				float Tile10_g11860 = round( ( ( _TextureLayer2Speed * temp_output_27_0_g11859 ) + _TextureLayer2StartFrame ) );
				float2 Invert10_g11860 = float2( 0,1 );
				float2 localMyCustomExpression10_g11860 = MyCustomExpression10_g11860( UV10_g11860 , Width10_g11860 , Height10_g11860 , Tile10_g11860 , Invert10_g11860 );
				#ifdef _TEXTURELAYER2SHEETTOGGLE_ON
				float2 staticSwitch20_g11859 = localMyCustomExpression10_g11860;
				#else
				float2 staticSwitch20_g11859 = temp_output_72_0_g11859;
				#endif
				float4 tex2DNode3_g11859 = tex2D( _TextureLayer2Texture, staticSwitch20_g11859 );
				float3 appendResult13_g11859 = (float3(tex2DNode3_g11859.r , tex2DNode3_g11859.g , tex2DNode3_g11859.b));
				float3 appendResult18_g11859 = (float3(_TextureLayer2Color.r , _TextureLayer2Color.g , _TextureLayer2Color.b));
				float3 temp_output_16_0_g11859 = ( appendResult13_g11859 * appendResult18_g11859 );
				float4 break2_g11861 = temp_output_5_0_g11859;
				#ifdef _TEXTURELAYER2CONTRASTTOGGLE_ON
				float3 staticSwitch84_g11859 = ( pow( ( ( break2_g11861.x + break2_g11861.x + break2_g11861.y + break2_g11861.y + break2_g11861.y + break2_g11861.z ) / 6.0 ) , max( _TextureLayer2Contrast , 0.001 ) ) * temp_output_16_0_g11859 );
				#else
				float3 staticSwitch84_g11859 = temp_output_16_0_g11859;
				#endif
				float3 lerpResult12_g11859 = lerp( appendResult11_g11859 , staticSwitch84_g11859 , ( tex2DNode3_g11859.a * _TextureLayer2Fade ));
				float4 appendResult14_g11859 = (float4(lerpResult12_g11859 , break6_g11859.a));
				float4 staticSwitch15_g11859 = appendResult14_g11859;
				#else
				float4 staticSwitch15_g11859 = temp_output_5_0_g11859;
				#endif
				float4 temp_output_473_0 = staticSwitch15_g11859;
				#ifdef _ENABLEFULLDISTORTION_ON
				float4 break4_g11867 = temp_output_473_0;
				float fullDistortionAlpha164 = _FullDistortionFade;
				float4 appendResult5_g11867 = (float4(break4_g11867.r , break4_g11867.g , break4_g11867.b , ( break4_g11867.a * fullDistortionAlpha164 )));
				float4 staticSwitch77 = appendResult5_g11867;
				#else
				float4 staticSwitch77 = temp_output_473_0;
				#endif
				#ifdef _ENABLEDIRECTIONALDISTORTION_ON
				float4 break4_g11868 = staticSwitch77;
				float directionalDistortionAlpha167 = (( _DirectionalDistortionInvert )?( ( 1.0 - clampResult154_g11669 ) ):( clampResult154_g11669 ));
				float4 appendResult5_g11868 = (float4(break4_g11868.r , break4_g11868.g , break4_g11868.b , ( break4_g11868.a * directionalDistortionAlpha167 )));
				float4 staticSwitch75 = appendResult5_g11868;
				#else
				float4 staticSwitch75 = staticSwitch77;
				#endif
				float4 temp_output_1_0_g11869 = staticSwitch75;
				float4 temp_output_1_0_g11870 = temp_output_1_0_g11869;
#ifdef _ENABLEFULLALPHADISSOLVE_ON
				float temp_output_53_0_g11870 = max( _FullAlphaDissolveWidth , 0.001 );
				#endif
				float2 temp_output_18_0_g11869 = shaderPosition235;
				#ifdef _ENABLEFULLALPHADISSOLVE_ON
				float linValue16_g11871 = tex2D( _UberNoiseTexture, ( temp_output_18_0_g11869 * _FullAlphaDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11871 = MyCustomExpression16_g11871( linValue16_g11871 );
				float clampResult17_g11870 = clamp( ( ( ( _FullAlphaDissolveFade * ( 1.0 + temp_output_53_0_g11870 ) ) - localMyCustomExpression16_g11871 ) / temp_output_53_0_g11870 ) , 0.0 , 1.0 );
				float4 appendResult3_g11870 = (float4((temp_output_1_0_g11870).rgb , ( temp_output_1_0_g11870.a * clampResult17_g11870 )));
				float4 staticSwitch3_g11869 = appendResult3_g11870;
				#else
				float4 staticSwitch3_g11869 = temp_output_1_0_g11869;
				#endif
				#ifdef _ENABLEFULLGLOWDISSOLVE_ON
				float linValue16_g11879 = tex2D( _UberNoiseTexture, ( temp_output_18_0_g11869 * _FullGlowDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11879 = MyCustomExpression16_g11879( linValue16_g11879 );
				float temp_output_5_0_g11878 = localMyCustomExpression16_g11879;
				float temp_output_61_0_g11878 = step( temp_output_5_0_g11878 , _FullGlowDissolveFade );
				float temp_output_53_0_g11878 = max( ( _FullGlowDissolveFade * _FullGlowDissolveWidth ) , 0.001 );
				float4 temp_output_1_0_g11878 = staticSwitch3_g11869;
				float4 appendResult3_g11878 = (float4(( ( (_FullGlowDissolveEdgeColor).rgb * ( temp_output_61_0_g11878 - step( temp_output_5_0_g11878 , ( ( _FullGlowDissolveFade * ( 1.01 + temp_output_53_0_g11878 ) ) - temp_output_53_0_g11878 ) ) ) ) + (temp_output_1_0_g11878).rgb ) , ( temp_output_1_0_g11878.a * temp_output_61_0_g11878 )));
				float4 staticSwitch5_g11869 = appendResult3_g11878;
				#else
				float4 staticSwitch5_g11869 = staticSwitch3_g11869;
				#endif
				#ifdef _ENABLESOURCEALPHADISSOLVE_ON
				float4 temp_output_1_0_g11880 = staticSwitch5_g11869;
				float2 temp_output_76_0_g11880 = temp_output_18_0_g11869;
				float linValue16_g11881 = tex2D( _UberNoiseTexture, ( temp_output_76_0_g11880 * _SourceAlphaDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11881 = MyCustomExpression16_g11881( linValue16_g11881 );
				float clampResult17_g11880 = clamp( ( ( _SourceAlphaDissolveFade - ( distance( _SourceAlphaDissolvePosition , temp_output_76_0_g11880 ) + ( localMyCustomExpression16_g11881 * _SourceAlphaDissolveNoiseFactor ) ) ) / max( _SourceAlphaDissolveWidth , 0.001 ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11880 = (float4((temp_output_1_0_g11880).rgb , ( temp_output_1_0_g11880.a * (( _SourceAlphaDissolveInvert )?( ( 1.0 - clampResult17_g11880 ) ):( clampResult17_g11880 )) )));
				float4 staticSwitch8_g11869 = appendResult3_g11880;
				#else
				float4 staticSwitch8_g11869 = staticSwitch5_g11869;
				#endif
				#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
				float2 temp_output_90_0_g11876 = temp_output_18_0_g11869;
				float linValue16_g11877 = tex2D( _UberNoiseTexture, ( temp_output_90_0_g11876 * _SourceGlowDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11877 = MyCustomExpression16_g11877( linValue16_g11877 );
				float temp_output_65_0_g11876 = ( distance( _SourceGlowDissolvePosition , temp_output_90_0_g11876 ) + ( localMyCustomExpression16_g11877 * _SourceGlowDissolveNoiseFactor ) );
				float temp_output_75_0_g11876 = step( temp_output_65_0_g11876 , _SourceGlowDissolveFade );
				float temp_output_76_0_g11876 = step( temp_output_65_0_g11876 , ( _SourceGlowDissolveFade - max( _SourceGlowDissolveWidth , 0.001 ) ) );
				float4 temp_output_1_0_g11876 = staticSwitch8_g11869;
				float4 appendResult3_g11876 = (float4(( ( max( ( temp_output_75_0_g11876 - temp_output_76_0_g11876 ) , 0.0 ) * (_SourceGlowDissolveEdgeColor).rgb ) + (temp_output_1_0_g11876).rgb ) , ( temp_output_1_0_g11876.a * (( _SourceGlowDissolveInvert )?( ( 1.0 - temp_output_76_0_g11876 ) ):( temp_output_75_0_g11876 )) )));
				float4 staticSwitch9_g11869 = appendResult3_g11876;
				#else
				float4 staticSwitch9_g11869 = staticSwitch8_g11869;
				#endif
				#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
				float4 temp_output_1_0_g11872 = staticSwitch9_g11869;
				float2 temp_output_161_0_g11872 = temp_output_18_0_g11869;
				float3 rotatedValue136_g11872 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_161_0_g11872 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalAlphaFadeRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11872 = rotatedValue136_g11872;
				float linValue16_g11873 = tex2D( _UberNoiseTexture, ( temp_output_161_0_g11872 * _DirectionalAlphaFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11873 = MyCustomExpression16_g11873( linValue16_g11873 );
				float clampResult154_g11872 = clamp( ( ( break130_g11872.x + break130_g11872.y + _DirectionalAlphaFadeFade + ( localMyCustomExpression16_g11873 * _DirectionalAlphaFadeNoiseFactor ) ) / max( _DirectionalAlphaFadeWidth , 0.001 ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11872 = (float4((temp_output_1_0_g11872).rgb , ( temp_output_1_0_g11872.a * (( _DirectionalAlphaFadeInvert )?( ( 1.0 - clampResult154_g11872 ) ):( clampResult154_g11872 )) )));
				float4 staticSwitch11_g11869 = appendResult3_g11872;
				#else
				float4 staticSwitch11_g11869 = staticSwitch9_g11869;
				#endif
				#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
				float2 temp_output_171_0_g11874 = temp_output_18_0_g11869;
				float3 rotatedValue136_g11874 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_171_0_g11874 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalGlowFadeRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11874 = rotatedValue136_g11874;
				float linValue16_g11875 = tex2D( _UberNoiseTexture, ( temp_output_171_0_g11874 * _DirectionalGlowFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11875 = MyCustomExpression16_g11875( linValue16_g11875 );
				float temp_output_168_0_g11874 = max( ( ( break130_g11874.x + break130_g11874.y + _DirectionalGlowFadeFade + ( localMyCustomExpression16_g11875 * _DirectionalGlowFadeNoiseFactor ) ) / max( _DirectionalGlowFadeWidth , 0.001 ) ) , 0.0 );
				float temp_output_161_0_g11874 = step( 0.1 , (( _DirectionalGlowFadeInvert )?( ( 1.0 - temp_output_168_0_g11874 ) ):( temp_output_168_0_g11874 )) );
				float4 temp_output_1_0_g11874 = staticSwitch11_g11869;
				float clampResult154_g11874 = clamp( temp_output_161_0_g11874 , 0.0 , 1.0 );
				float4 appendResult3_g11874 = (float4(( ( (_DirectionalGlowFadeEdgeColor).rgb * ( temp_output_161_0_g11874 - step( 1.0 , (( _DirectionalGlowFadeInvert )?( ( 1.0 - temp_output_168_0_g11874 ) ):( temp_output_168_0_g11874 )) ) ) ) + (temp_output_1_0_g11874).rgb ) , ( temp_output_1_0_g11874.a * clampResult154_g11874 )));
				float4 staticSwitch15_g11869 = appendResult3_g11874;
				#else
				float4 staticSwitch15_g11869 = staticSwitch11_g11869;
				#endif
				#ifdef _ENABLEHALFTONE_ON
				float4 temp_output_1_0_g11882 = staticSwitch15_g11869;
				float2 temp_output_126_0_g11882 = temp_output_18_0_g11869;
				float temp_output_121_0_g11882 = max( ( ( _HalftoneFade - distance( _HalftonePosition , temp_output_126_0_g11882 ) ) / max( 0.01 , _HalftoneFadeWidth ) ) , 0.0 );
				float2 appendResult11_g11883 = (float2(temp_output_121_0_g11882 , temp_output_121_0_g11882));
				float temp_output_17_0_g11883 = length( ( (( ( abs( temp_output_126_0_g11882 ) * _HalftoneTiling ) % float2( 1,1 ) )*2.0 + -1.0) / appendResult11_g11883 ) );
				float clampResult17_g11882 = clamp( saturate( ( ( 1.0 - temp_output_17_0_g11883 ) / fwidth( temp_output_17_0_g11883 ) ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11882 = (float4((temp_output_1_0_g11882).rgb , ( temp_output_1_0_g11882.a * (( _HalftoneInvert )?( ( 1.0 - clampResult17_g11882 ) ):( clampResult17_g11882 )) )));
				float4 staticSwitch13_g11869 = appendResult3_g11882;
				#else
				float4 staticSwitch13_g11869 = staticSwitch15_g11869;
				#endif
				#ifdef _ENABLEADDCOLOR_ON
				float3 temp_output_3_0_g11885 = (_AddColorColor).rgb;
				float2 uv_AddColorMask = IN.texCoord0.xy * _AddColorMask_ST.xy + _AddColorMask_ST.zw;
				float4 tex2DNode19_g11885 = tex2D( _AddColorMask, uv_AddColorMask );
				#ifdef _ADDCOLORMASKTOGGLE_ON
				float3 staticSwitch16_g11885 = ( temp_output_3_0_g11885 * ( (tex2DNode19_g11885).rgb * tex2DNode19_g11885.a ) );
				#else
				float3 staticSwitch16_g11885 = temp_output_3_0_g11885;
				#endif
				float4 temp_output_1_0_g11885 = staticSwitch13_g11869;
				float4 break2_g11887 = temp_output_1_0_g11885;
				#ifdef _ADDCOLORCONTRASTTOGGLE_ON
				float3 staticSwitch17_g11885 = ( staticSwitch16_g11885 * pow( ( ( break2_g11887.x + break2_g11887.x + break2_g11887.y + break2_g11887.y + break2_g11887.y + break2_g11887.z ) / 6.0 ) , max( _AddColorContrast , 0.001 ) ) );
				#else
				float3 staticSwitch17_g11885 = staticSwitch16_g11885;
				#endif
				float4 appendResult6_g11885 = (float4(( ( staticSwitch17_g11885 * _AddColorFade ) + (temp_output_1_0_g11885).rgb ) , temp_output_1_0_g11885.a));
				float4 staticSwitch5_g11884 = appendResult6_g11885;
				#else
				float4 staticSwitch5_g11884 = staticSwitch13_g11869;
				#endif
				#ifdef _ENABLEALPHATINT_ON
				float4 temp_output_1_0_g11888 = staticSwitch5_g11884;
				float3 lerpResult4_g11888 = lerp( (temp_output_1_0_g11888).rgb , (_AlphaTintColor).rgb , ( ( 1.0 - temp_output_1_0_g11888.a ) * step( _AlphaTintMinAlpha , temp_output_1_0_g11888.a ) * _AlphaTintFade ));
				float4 appendResult13_g11888 = (float4(lerpResult4_g11888 , temp_output_1_0_g11888.a));
				float4 staticSwitch11_g11884 = appendResult13_g11888;
				#else
				float4 staticSwitch11_g11884 = staticSwitch5_g11884;
				#endif
				#ifdef _ENABLESTRONGTINT_ON
				float4 temp_output_1_0_g11889 = staticSwitch11_g11884;
				float3 temp_output_6_0_g11889 = (_StrongTintTint).rgb;
				float2 uv_StrongTintMask = IN.texCoord0.xy * _StrongTintMask_ST.xy + _StrongTintMask_ST.zw;
				float4 tex2DNode23_g11889 = tex2D( _StrongTintMask, uv_StrongTintMask );
				#ifdef _STRONGTINTMASKTOGGLE_ON
				float3 staticSwitch21_g11889 = ( temp_output_6_0_g11889 * ( (tex2DNode23_g11889).rgb * tex2DNode23_g11889.a ) );
				#else
				float3 staticSwitch21_g11889 = temp_output_6_0_g11889;
				#endif
				float4 break2_g11891 = temp_output_1_0_g11889;
				#ifdef _STRONGTINTCONTRASTTOGGLE_ON
				float3 staticSwitch22_g11889 = ( pow( ( ( break2_g11891.x + break2_g11891.x + break2_g11891.y + break2_g11891.y + break2_g11891.y + break2_g11891.z ) / 6.0 ) , max( _StrongTintContrast , 0.001 ) ) * staticSwitch21_g11889 );
				#else
				float3 staticSwitch22_g11889 = staticSwitch21_g11889;
				#endif
				float3 lerpResult7_g11889 = lerp( (temp_output_1_0_g11889).rgb , staticSwitch22_g11889 , _StrongTintFade);
				float4 appendResult9_g11889 = (float4(lerpResult7_g11889 , (temp_output_1_0_g11889).a));
				float4 staticSwitch7_g11884 = appendResult9_g11889;
				#else
				float4 staticSwitch7_g11884 = staticSwitch11_g11884;
				#endif
				float4 temp_output_2_0_g11892 = staticSwitch7_g11884;
				#ifdef _ENABLESHADOW_ON
				float4 break4_g11894 = temp_output_2_0_g11892;
				float3 appendResult5_g11894 = (float3(break4_g11894.r , break4_g11894.g , break4_g11894.b));
				float2 appendResult2_g11893 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float4 appendResult85_g11892 = (float4(_ShadowColor.r , _ShadowColor.g , _ShadowColor.b , ( _ShadowFade * tex2D( _MainTex, ( finalUV146 - ( ( 100.0 / appendResult2_g11893 ) * _ShadowOffset ) ) ).a )));
				float4 break6_g11894 = appendResult85_g11892;
				float3 appendResult7_g11894 = (float3(break6_g11894.r , break6_g11894.g , break6_g11894.b));
				float temp_output_11_0_g11894 = ( ( 1.0 - break4_g11894.a ) * break6_g11894.a );
				float temp_output_32_0_g11894 = ( break4_g11894.a + temp_output_11_0_g11894 );
				float4 appendResult18_g11894 = (float4(( ( ( appendResult5_g11894 * break4_g11894.a ) + ( appendResult7_g11894 * temp_output_11_0_g11894 ) ) * ( 1.0 / max( temp_output_32_0_g11894 , 0.01 ) ) ) , temp_output_32_0_g11894));
				float4 staticSwitch82_g11892 = appendResult18_g11894;
				#else
				float4 staticSwitch82_g11892 = temp_output_2_0_g11892;
				#endif
				float4 break4_g11895 = staticSwitch82_g11892;
				#ifdef _ENABLECUSTOMFADE_ON
				float staticSwitch8_g11753 = 1.0;
				#else
				float staticSwitch8_g11753 = IN.color.a;
				#endif
				#ifdef _ENABLESMOKE_ON
				float staticSwitch9_g11753 = 1.0;
				#else
				float staticSwitch9_g11753 = staticSwitch8_g11753;
				#endif
				float customVertexAlpha193 = staticSwitch9_g11753;
				float4 appendResult5_g11895 = (float4(break4_g11895.r , break4_g11895.g , break4_g11895.b , ( break4_g11895.a * customVertexAlpha193 )));
				float4 temp_output_344_0 = appendResult5_g11895;
				float4 temp_output_1_0_g11896 = temp_output_344_0;
				float4 appendResult8_g11896 = (float4(( (temp_output_1_0_g11896).rgb * (IN.color).rgb ) , temp_output_1_0_g11896.a));
				#ifdef _VERTEXTINTFIRST_ON
				float4 staticSwitch342 = temp_output_344_0;
				#else
				float4 staticSwitch342 = appendResult8_g11896;
				#endif
				float4 lerpResult125 = lerp( ( originalColor191 * IN.color ) , staticSwitch342 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float4 staticSwitch143 = staticSwitch342;
				#elif defined(_SHADERFADING_FULL)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_MASK)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_DISSOLVE)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_SPREAD)
				float4 staticSwitch143 = lerpResult125;
				#else
				float4 staticSwitch143 = staticSwitch342;
				#endif
				float4 temp_output_7_0_g11903 = staticSwitch143;
				#ifdef _BAKEDMATERIAL_ON
				float4 appendResult2_g11903 = (float4(( (temp_output_7_0_g11903).rgb / max( temp_output_7_0_g11903.a , 1E-05 ) ) , temp_output_7_0_g11903.a));
				float4 staticSwitch6_g11903 = appendResult2_g11903;
				#else
				float4 staticSwitch6_g11903 = temp_output_7_0_g11903;
				#endif
				
				float4 Color = staticSwitch6_g11903;

				#if defined(DEBUG_DISPLAY)
					SurfaceData2D surfaceData;
					InitializeSurfaceData(Color.rgb, Color.a, surfaceData);
					InputData2D inputData;
					InitializeInputData(positionWS.xy, half2(IN.texCoord0.xy), inputData);
					half4 debugColor = 0;

					SETUP_DEBUG_DATA_2D(inputData, positionWS);

					if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
					{
						return debugColor;
					}
				#endif

				#if ETC1_EXTERNAL_ALPHA
					float4 alpha = SAMPLE_TEXTURE2D( _AlphaTex, sampler_AlphaTex, IN.texCoord0.xy );
					Color.a = lerp( Color.a, alpha.r, _EnableAlphaTexture );
				#endif

				Color *= IN.color;

				return Color;
			}

			ENDHLSL
		}
		
        Pass
        {
			
            Name "SceneSelectionPass"
            Tags { "LightMode"="SceneSelectionPass" }

            Cull Off

            HLSLPROGRAM

			#define ASE_SRP_VERSION 140007


			#pragma vertex vert
			#pragma fragment frag

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX
            #define SHADERPASS SHADERPASS_DEPTHONLY
			#define SCENESELECTIONPASS 1


            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_FRAG_POSITION
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature_local _SHADERFADING_NONE _SHADERFADING_FULL _SHADERFADING_MASK _SHADERFADING_DISSOLVE _SHADERFADING_SPREAD
			#pragma shader_feature_local _ENABLESINESCALE_ON
			#pragma shader_feature _ENABLEVIBRATE_ON
			#pragma shader_feature _ENABLESINEMOVE_ON
			#pragma shader_feature _ENABLESQUISH_ON
			#pragma shader_feature _SPRITESHEETFIX_ON
			#pragma shader_feature_local _PIXELPERFECTUV_ON
			#pragma shader_feature _ENABLEWORLDTILING_ON
			#pragma shader_feature _ENABLESCREENTILING_ON
			#pragma shader_feature _TOGGLETIMEFREQUENCY_ON
			#pragma shader_feature _TOGGLETIMEFPS_ON
			#pragma shader_feature _TOGGLETIMESPEED_ON
			#pragma shader_feature _TOGGLEUNSCALEDTIME_ON
			#pragma shader_feature _TOGGLECUSTOMTIME_ON
			#pragma shader_feature _SHADERSPACE_UV _SHADERSPACE_UV_RAW _SHADERSPACE_OBJECT _SHADERSPACE_OBJECT_SCALED _SHADERSPACE_WORLD _SHADERSPACE_UI_GRAPHIC _SHADERSPACE_SCREEN
			#pragma shader_feature _PIXELPERFECTSPACE_ON
			#pragma shader_feature _BAKEDMATERIAL_ON
			#pragma shader_feature _VERTEXTINTFIRST_ON
			#pragma shader_feature _ENABLESHADOW_ON
			#pragma shader_feature _ENABLESTRONGTINT_ON
			#pragma shader_feature _ENABLEALPHATINT_ON
			#pragma shader_feature_local _ENABLEADDCOLOR_ON
			#pragma shader_feature_local _ENABLEHALFTONE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALGLOWFADE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALALPHAFADE_ON
			#pragma shader_feature_local _ENABLESOURCEGLOWDISSOLVE_ON
			#pragma shader_feature_local _ENABLESOURCEALPHADISSOLVE_ON
			#pragma shader_feature_local _ENABLEFULLGLOWDISSOLVE_ON
			#pragma shader_feature_local _ENABLEFULLALPHADISSOLVE_ON
			#pragma shader_feature_local _ENABLEDIRECTIONALDISTORTION_ON
			#pragma shader_feature_local _ENABLEFULLDISTORTION_ON
			#pragma shader_feature _ENABLETEXTURELAYER2_ON
			#pragma shader_feature _ENABLETEXTURELAYER1_ON
			#pragma shader_feature _ENABLESHIFTING_ON
			#pragma shader_feature _ENABLEENCHANTED_ON
			#pragma shader_feature_local _ENABLEPOISON_ON
			#pragma shader_feature_local _ENABLESHINE_ON
			#pragma shader_feature_local _ENABLERAINBOW_ON
			#pragma shader_feature_local _ENABLEBURN_ON
			#pragma shader_feature_local _ENABLEFROZEN_ON
			#pragma shader_feature_local _ENABLEMETAL_ON
			#pragma shader_feature_local _ENABLECAMOUFLAGE_ON
			#pragma shader_feature_local _ENABLEGLITCH_ON
			#pragma shader_feature_local _ENABLEHOLOGRAM_ON
			#pragma shader_feature _ENABLEPINGPONGGLOW_ON
			#pragma shader_feature_local _ENABLEPIXELOUTLINE_ON
			#pragma shader_feature_local _ENABLEOUTEROUTLINE_ON
			#pragma shader_feature_local _ENABLEINNEROUTLINE_ON
			#pragma shader_feature_local _ENABLESATURATION_ON
			#pragma shader_feature_local _ENABLESINEGLOW_ON
			#pragma shader_feature_local _ENABLEADDHUE_ON
			#pragma shader_feature_local _ENABLESHIFTHUE_ON
			#pragma shader_feature_local _ENABLEINKSPREAD_ON
			#pragma shader_feature_local _ENABLEBLACKTINT_ON
			#pragma shader_feature_local _ENABLESPLITTONING_ON
			#pragma shader_feature_local _ENABLEHUE_ON
			#pragma shader_feature_local _ENABLEBRIGHTNESS_ON
			#pragma shader_feature_local _ENABLECONTRAST_ON
			#pragma shader_feature _ENABLENEGATIVE_ON
			#pragma shader_feature_local _ENABLECOLORREPLACE_ON
			#pragma shader_feature_local _ENABLERECOLORRGBYCP_ON
			#pragma shader_feature _ENABLERECOLORRGB_ON
			#pragma shader_feature_local _ENABLEFLAME_ON
			#pragma shader_feature_local _ENABLECHECKERBOARD_ON
			#pragma shader_feature_local _ENABLECUSTOMFADE_ON
			#pragma shader_feature_local _ENABLESMOKE_ON
			#pragma shader_feature _ENABLESHARPEN_ON
			#pragma shader_feature _ENABLEGAUSSIANBLUR_ON
			#pragma shader_feature _ENABLESMOOTHPIXELART_ON
			#pragma shader_feature_local _TILINGFIX_ON
			#pragma shader_feature _ENABLEWIGGLE_ON
			#pragma shader_feature_local _ENABLEUVSCALE_ON
			#pragma shader_feature_local _ENABLEPIXELATE_ON
			#pragma shader_feature_local _ENABLEUVSCROLL_ON
			#pragma shader_feature_local _ENABLEUVROTATE_ON
			#pragma shader_feature_local _ENABLESINEROTATE_ON
			#pragma shader_feature_local _ENABLESQUEEZE_ON
			#pragma shader_feature_local _ENABLEUVDISTORT_ON
			#pragma shader_feature_local _ENABLEWIND_ON
			#pragma shader_feature_local _WINDLOCALWIND_ON
			#pragma shader_feature_local _WINDHIGHQUALITYNOISE_ON
			#pragma shader_feature_local _WINDISPARALLAX_ON
			#pragma shader_feature _UVDISTORTMASKTOGGLE_ON
			#pragma shader_feature _WIGGLEFIXEDGROUNDTOGGLE_ON
			#pragma shader_feature _RECOLORRGBTEXTURETOGGLE_ON
			#pragma shader_feature _RECOLORRGBYCPTEXTURETOGGLE_ON
			#pragma shader_feature_local _ADDHUEMASKTOGGLE_ON
			#pragma shader_feature_local _SINEGLOWMASKTOGGLE_ON
			#pragma shader_feature _INNEROUTLINETEXTURETOGGLE_ON
			#pragma shader_feature_local _INNEROUTLINEDISTORTIONTOGGLE_ON
			#pragma shader_feature _INNEROUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature _OUTEROUTLINETEXTURETOGGLE_ON
			#pragma shader_feature _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature_local _OUTEROUTLINEDISTORTIONTOGGLE_ON
			#pragma shader_feature _PIXELOUTLINETEXTURETOGGLE_ON
			#pragma shader_feature _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
			#pragma shader_feature _CAMOUFLAGEANIMATIONTOGGLE_ON
			#pragma shader_feature _METALMASKTOGGLE_ON
			#pragma shader_feature _SHINEMASKTOGGLE_ON
			#pragma shader_feature _ENCHANTEDLERPTOGGLE_ON
			#pragma shader_feature _ENCHANTEDRAINBOWTOGGLE_ON
			#pragma shader_feature _SHIFTINGRAINBOWTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1CONTRASTTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1SHEETTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER1SCROLLTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2CONTRASTTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2SHEETTOGGLE_ON
			#pragma shader_feature _TEXTURELAYER2SCROLLTOGGLE_ON
			#pragma shader_feature _ADDCOLORCONTRASTTOGGLE_ON
			#pragma shader_feature _ADDCOLORMASKTOGGLE_ON
			#pragma shader_feature _STRONGTINTCONTRASTTOGGLE_ON
			#pragma shader_feature _STRONGTINTMASKTOGGLE_ON


			sampler2D _MainTex;
			#ifdef _TOGGLEUNSCALEDTIME_ON
			float UnscaledTime;
			#endif
			#ifdef _SHADERFADING_MASK
			sampler2D _FadingMask;
			#endif
			sampler2D _UberNoiseTexture;
			#ifdef _ENABLEWIND_ON
			float WindMinIntensity;
			float WindMaxIntensity;
			float WindNoiseScale;
			float WindTime;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			sampler2D _UVDistortMask;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			sampler2D _CustomFadeFadeMask;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			sampler2D _RecolorRGBTexture;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			sampler2D _RecolorRGBYCPTexture;
			#endif
			#ifdef _ENABLEADDHUE_ON
			sampler2D _AddHueMask;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			sampler2D _SineGlowMask;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			sampler2D _InnerOutlineTintTexture;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			sampler2D _OuterOutlineTintTexture;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			sampler2D _PixelOutlineTintTexture;
			#endif
			#ifdef _ENABLEMETAL_ON
			sampler2D _MetalMask;
			#endif
			#ifdef _ENABLESHINE_ON
			sampler2D _ShineMask;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			sampler2D _TextureLayer1Texture;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			sampler2D _TextureLayer2Texture;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			sampler2D _AddColorMask;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			sampler2D _StrongTintMask;
			#endif
			CBUFFER_START( UnityPerMaterial )
			#ifdef _ENABLESHIFTING_ON
			float4 _ShiftingColorA;
			#endif
			#ifdef _ENABLEPOISON_ON
			float4 _PoisonColor;
			#endif
			#ifdef _ENABLESPLITTONING_ON
			float4 _SplitToningHighlightsColor;
			float4 _SplitToningShadowsColor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float4 _EnchantedLowColor;
			float4 _EnchantedHighColor;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float4 _ColorReplaceToColor;
			float4 _ColorReplaceFromColor;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			float4 _RecolorRGBYCPRedTint;
			float4 _RecolorRGBYCPYellowTint;
			float4 _RecolorRGBYCPGreenTint;
			float4 _RecolorRGBYCPCyanTint;
			float4 _RecolorRGBYCPBlueTint;
			float4 _RecolorRGBYCPPurpleTint;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			float4 _RecolorRGBBlueTint;
			float4 _RecolorRGBGreenTint;
			float4 _RecolorRGBRedTint;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float4 _ShiftingColorB;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float4 _TextureLayer1Color;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float4 _TextureLayer2Color;
			#endif
			#ifdef _ENABLEBLACKTINT_ON
			float4 _BlackTintColor;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float4 _InkSpreadColor;
			#endif
			#ifdef _ENABLESHINE_ON
			float4 _ShineMask_ST;
			float4 _ShineColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenTint;
			#endif
			#ifdef _ENABLEMETAL_ON
			float4 _MetalMask_ST;
			float4 _MetalColor;
			float4 _MetalHighlightColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenHighlightColor;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float4 _CamouflageColorB;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnInsideNoiseColor;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float4 _CamouflageColorA;
			float4 _CamouflageBaseColor;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float4 _UVDistortMask_ST;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnInsideColor;
			#endif
			#ifdef _ENABLEPINGPONGGLOW_ON
			float4 _PingPongGlowTo;
			float4 _PingPongGlowFrom;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnEdgeColor;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float4 _PixelOutlineColor;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float4 _OuterOutlineColor;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float4 _InnerOutlineColor;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			float4 _SineGlowMask_ST;
			float4 _SineGlowColor;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float4 _AddHueMask_ST;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float4 _HologramTint;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float4 _FullGlowDissolveEdgeColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenSnowColor;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float4 _StrongTintTint;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float4 _AddColorMask_ST;
			float4 _AddColorColor;
			#endif
			#ifdef _ENABLESHADOW_ON
			float4 _ShadowColor;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float4 _SourceGlowDissolveEdgeColor;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float4 _DirectionalGlowFadeEdgeColor;
			#endif
			#ifdef _SPRITESHEETFIX_ON
			float4 _SpriteSheetRect;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float4 _AlphaTintColor;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float4 _StrongTintMask_ST;
			#endif
			#ifdef _SHADERFADING_MASK
			float4 _FadingMask_ST;
			#endif
			float4 _MainTex_TexelSize;
			float2 _FadingNoiseScale;
			#ifdef _SHADERFADING_SPREAD
			float2 _FadingPosition;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnEdgeNoiseScale;
			float2 _BurnPosition;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float2 _DirectionalAlphaFadeNoiseScale;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnSwirlNoiseScale;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenSnowScale;
			#endif
			#ifdef _ENABLEFLAME_ON
			float2 _FlameNoiseScale;
			float2 _FlameSpeed;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float2 _GlitchNoiseSpeed;
			float2 _GlitchNoiseScale;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			float2 _CustomFadeNoiseScale;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float2 _TextureLayer1Offset;
			float2 _TextureLayer1ScrollSpeed;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageDistortionSpeed;
			float2 _CamouflageDistortionScale;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnInsideNoiseScale;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float2 _TextureLayer1Scale;
			#endif
			#ifdef _ENABLESHADOW_ON
			float2 _ShadowOffset;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageDistortionIntensity;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float2 _SineMoveFrequency;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float2 _InnerOutlineTextureSpeed;
			float2 _InnerOutlineNoiseSpeed;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float2 _InkSpreadNoiseScale;
			float2 _InkSpreadPosition;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float2 _InnerOutlineNoiseScale;
			float2 _InnerOutlineDistortionIntensity;
			#endif
			#ifdef _ENABLEPOISON_ON
			float2 _PoisonNoiseSpeed;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float2 _HalftonePosition;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float2 _DirectionalGlowFadeNoiseScale;
			#endif
			#ifdef _ENABLEPOISON_ON
			float2 _PoisonNoiseScale;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float2 _OuterOutlineTextureSpeed;
			#endif
			#ifdef _ENABLESINESCALE_ON
			float2 _SineScaleFactor;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float2 _RainbowCenter;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float2 _OuterOutlineNoiseSpeed;
			float2 _OuterOutlineNoiseScale;
			float2 _OuterOutlineDistortionIntensity;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float2 _EnchantedSpeed;
			float2 _EnchantedScale;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float2 _PixelOutlineTextureSpeed;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float2 _RainbowNoiseScale;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageNoiseScaleA;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float2 _SineMoveOffset;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float2 _WorldTilingOffset;
			#endif
			#ifdef _ENABLEFULLALPHADISSOLVE_ON
			float2 _FullAlphaDissolveNoiseScale;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float2 _UVDistortNoiseScale;
			float2 _UVDistortTo;
			float2 _UVDistortFrom;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float2 _FullGlowDissolveNoiseScale;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float2 _GlitchMaskScale;
			float2 _GlitchMaskSpeed;
			float2 _GlitchDistortion;
			float2 _GlitchDistortionScale;
			float2 _GlitchDistortionSpeed;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortion;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float2 _SqueezeCenter;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortionScale;
			#endif
			#ifdef _ENABLESOURCEALPHADISSOLVE_ON
			float2 _SourceAlphaDissolvePosition;
			float2 _SourceAlphaDissolveNoiseScale;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float2 _ScreenTilingOffset;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionNoiseScale;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float2 _SourceGlowDissolvePosition;
			float2 _SourceGlowDissolveNoiseScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionDistortion;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float2 _ScreenTilingScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionDistortionScale;
			#endif
			#ifdef _ENABLEFULLDISTORTION_ON
			float2 _FullDistortionDistortion;
			float2 _FullDistortionNoiseScale;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortionSpeed;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseScale;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float2 _UVDistortSpeed;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float2 _SqueezeScale;
			#endif
			#ifdef _ENABLEUVSCALE_ON
			float2 _UVScaleScale;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float2 _WorldTilingScale;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageNoiseScaleB;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightScale;
			#endif
			#ifdef _ENABLEUVSCROLL_ON
			float2 _UVScrollSpeed;
			#endif
			#ifdef _ENABLEUVROTATE_ON
			float2 _UVRotatePivot;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2ScrollSpeed;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightSpeed;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2Offset;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float2 _SineRotatePivot;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseDistortionSpeed;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2Scale;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseDistortionScale;
			float2 _MetalNoiseDistortion;
			float2 _MetalNoiseSpeed;
			#endif
			#ifdef _ENABLEUVSCALE_ON
			float2 _UVScalePivot;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenHighlightDensity;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowSpeed;
			float _RainbowSaturation;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenFade;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowNoiseFactor;
			float _RainbowContrast;
			float _RainbowBrightness;
			float _RainbowDensity;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float _AddColorFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnInsideNoiseFactor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenHighlightContrast;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnEdgeNoiseFactor;
			#endif
			#ifdef _ENABLESHADOW_ON
			float _ShadowFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnSwirlFactor;
			float _BurnFade;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float _StrongTintContrast;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnRadius;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float _AlphaTintMinAlpha;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnInsideContrast;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float _AlphaTintFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnWidth;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float _StrongTintFade;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveFade;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonRecolorFactor;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowFade;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingBrightness;
			float _ShiftingSaturation;
			float _ShiftingContrast;
			float _ShiftingFade;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1EdgeClip;
			#endif
			int _TextureLayer1Columns;
			int _TextureLayer1Rows;
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1Speed;
			#endif
			int _TextureLayer1StartFrame;
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeRotation;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1Contrast;
			float _TextureLayer1Fade;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2EdgeClip;
			#endif
			int _TextureLayer2Columns;
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingDensity;
			#endif
			int _TextureLayer2Rows;
			int _TextureLayer2StartFrame;
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeInvert;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2Contrast;
			float _TextureLayer2Fade;
			#endif
			#ifdef _ENABLEFULLALPHADISSOLVE_ON
			float _FullAlphaDissolveFade;
			float _FullAlphaDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveInvert;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float _FullGlowDissolveFade;
			float _FullGlowDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEALPHADISSOLVE_ON
			float _SourceAlphaDissolveInvert;
			float _SourceAlphaDissolveFade;
			float _SourceAlphaDissolveNoiseFactor;
			float _SourceAlphaDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveNoiseFactor;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2Speed;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float _AddColorContrast;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingSpeed;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedFade;
			#endif
			#ifdef _ENABLESHINE_ON
			float _ShineSaturation;
			float _ShineContrast;
			float _ShineRotation;
			float _ShineFrequency;
			float _ShineSpeed;
			float _ShineWidth;
			float _ShineSmooth;
			float _ShineFade;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneFadeWidth;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonShiftSpeed;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneFade;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonDensity;
			float _PoisonFade;
			float _PoisonNoiseBrightness;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeFade;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneTiling;
			float _HalftoneInvert;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeWidth;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedRainbowDensity;
			float _EnchantedRainbowSpeed;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeNoiseFactor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedRainbowSaturation;
			float _EnchantedContrast;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeFade;
			float _DirectionalGlowFadeRotation;
			float _DirectionalGlowFadeInvert;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedBrightness;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeWidth;
			float _DirectionalAlphaFadeNoiseFactor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedReduce;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveWidth;
			#endif
			#ifdef _ENABLESQUISH_ON
			float _SquishStretch;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float _PixelOutlineWidth;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenSnowContrast;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float _SqueezeFade;
			float _SqueezePower;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float _UVDistortFade;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float _GlitchFade;
			float _GlitchMaskMin;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramFade;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateFrequency;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramDistortionOffset;
			float _HologramDistortionDensity;
			float _HologramDistortionSpeed;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionWidth;
			float _DirectionalDistortionNoiseFactor;
			float _DirectionalDistortionFade;
			float _DirectionalDistortionRotation;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramDistortionScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionInvert;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateAngle;
			#endif
			#ifdef _ENABLEUVROTATE_ON
			float _UVRotateSpeed;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeNoiseScale;
			float _SmokeVertexSeed;
			#endif
			#ifdef _ENABLESHARPEN_ON
			float _SharpenFade;
			float _SharpenFactor;
			float _SharpenOffset;
			#endif
			#ifdef _ENABLEGAUSSIANBLUR_ON
			float _GaussianBlurFade;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateFade;
			#endif
			#ifdef _ENABLEGAUSSIANBLUR_ON
			float _GaussianBlurOffset;
			#endif
			#ifdef _ENABLEWIGGLE_ON
			float _WiggleOffset;
			float _WiggleSpeed;
			float _WiggleFrequency;
			#endif
			#ifdef _ENABLEPIXELATE_ON
			float _PixelateFade;
			float _PixelatePixelsPerUnit;
			float _PixelatePixelDensity;
			#endif
			#ifdef _ENABLEWIGGLE_ON
			float _WiggleFade;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionRandomDirection;
			#endif
			#ifdef _ENABLEFULLDISTORTION_ON
			float _FullDistortionFade;
			#endif
			#ifdef _ENABLEWIND_ON
			float _WindSquishWindFactor;
			#endif
			#ifdef _ENABLEVIBRATE_ON
			float _VibrateFade;
			float _VibrateOffset;
			float _VibrateFrequency;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float _SineMoveFade;
			#endif
			#ifdef _TOGGLETIMEFREQUENCY_ON
			float _TimeRange;
			float _TimeFrequency;
			#endif
			#ifdef _ENABLEVIBRATE_ON
			float _VibrateRotation;
			#endif
			#ifdef _TOGGLETIMEFPS_ON
			float _TimeFPS;
			#endif
			#ifdef _TOGGLECUSTOMTIME_ON
			float _TimeValue;
			#endif
			#ifdef _ENABLESQUISH_ON
			float _SquishSquish;
			float _SquishFlip;
			float _SquishFade;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float _WorldTilingPixelsPerUnit;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float _ScreenTilingPixelsPerUnit;
			#endif
			#ifdef _TOGGLETIMESPEED_ON
			float _TimeSpeed;
			#endif
			#ifdef _ENABLESINESCALE_ON
			float _SineScaleFrequency;
			#endif
			float _FadingFade;
			float _FadingWidth;
			#ifdef _ENABLEWIND_ON
			float _WindSquishFactor;
			float _WindFlip;
			float _WindMaxRotation;
			float _WindRotation;
			float _WindNoiseSpeed;
			float _WindNoiseScale;
			float _WindXPosition;
			float _WindMaxIntensity;
			float _WindMinIntensity;
			float _WindRotationWindFactor;
			#endif
			#ifdef _SHADERFADING_SPREAD
			float _FadingNoiseFactor;
			#endif
			#ifdef _SHADERSPACE_SCREEN
			float _ScreenWidthUnits;
			#endif
			#ifdef _SHADERSPACE_UI_GRAPHIC
			float _RectHeight;
			float _RectWidth;
			#endif
			float _PixelsPerUnit;
			#ifdef _ENABLESMOKE_ON
			float _SmokeNoiseFactor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenSnowDensity;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeSmoothness;
			float _SmokeAlpha;
			#endif
			#ifdef _ENABLEPINGPONGGLOW_ON
			float _PingPongGlowContrast;
			float _PingPongGlowFade;
			float _PingPongGlowFrequency;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float _PixelOutlineFade;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float _OuterOutlineWidth;
			float _OuterOutlineFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramContrast;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float _InnerOutlineWidth;
			#endif
			#ifdef _ENABLESATURATION_ON
			float _Saturation;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			float _SineGlowMin;
			float _SineGlowMax;
			float _SineGlowFrequency;
			float _SineGlowFade;
			float _SineGlowContrast;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float _InnerOutlineFade;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramLineSpeed;
			float _HologramLineGap;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenContrast;
			#endif
			#ifdef _ENABLEMETAL_ON
			float _MetalFade;
			float _MetalContrast;
			float _MetalHighlightContrast;
			float _MetalHighlightDensity;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramLineFrequency;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageContrast;
			float _CamouflageDensityB;
			float _CamouflageSmoothnessA;
			float _CamouflageDensityA;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float _GlitchHueSpeed;
			float _GlitchBrightness;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramMinAlpha;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageSmoothnessB;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueContrast;
			float _AddHueBrightness;
			float _AddHueSaturation;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceSmoothness;
			float _ColorReplaceContrast;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			float _RecolorRGBYCPFade;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			float _RecolorRGBFade;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameBrightness;
			float _FlameSmooth;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceRange;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameRadius;
			float _FlameNoiseHeightFactor;
			#endif
			#ifdef _ENABLECHECKERBOARD_ON
			float _CheckerboardTiling;
			float _CheckerboardDarken;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			float _CustomFadeAlpha;
			float _CustomFadeSmoothness;
			float _CustomFadeNoiseFactor;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameNoiseFactor;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceFade;
			#endif
			#ifdef _ENABLENEGATIVE_ON
			float _NegativeFade;
			#endif
			#ifdef _ENABLECONTRAST_ON
			float _Contrast;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueSpeed;
			#endif
			#ifdef _ENABLESHIFTHUE_ON
			float _ShiftHueSpeed;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float _InkSpreadWidth;
			float _InkSpreadNoiseFactor;
			float _InkSpreadDistance;
			float _InkSpreadFade;
			float _InkSpreadContrast;
			#endif
			#ifdef _ENABLEBLACKTINT_ON
			float _BlackTintFade;
			float _BlackTintPower;
			#endif
			#ifdef _ENABLESPLITTONING_ON
			float _SplitToningFade;
			float _SplitToningContrast;
			float _SplitToningBalance;
			float _SplitToningShift;
			#endif
			#ifdef _ENABLEHUE_ON
			float _Hue;
			#endif
			#ifdef _ENABLEBRIGHTNESS_ON
			float _Brightness;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeDarkEdge;
			#endif
			float _NormalIntensity;
			CBUFFER_END


            struct VertexInput
			{
				float3 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};


            int _ObjectId;
            int _PassValue;

			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float MyCustomExpression16_g11716( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11714( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float FastNoise101_g11665( float x )
			{
				float i = floor(x);
				float f = frac(x);
				float s = sign(frac(x/2.0)-0.5);
				    
				float k = 0.5+0.5*sin(i);
				return s*f*(f-1.0)*((16.0*k-4.0)*f*(f-1.0)-1.0);
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float MyCustomExpression16_g11667( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11668( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11671( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11670( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11676( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11677( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11718( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11673( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11729( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float4 texturePointSmooth( sampler2D tex, float4 textureTexelSize, float2 uvs )
			{
				float2 size;
				size.x = textureTexelSize.z;
				size.y = textureTexelSize.w;
				float2 pixel = float2(1.0,1.0) / size;
				uvs -= pixel * float2(0.5,0.5);
				float2 uv_pixels = uvs * size;
				float2 delta_pixel = frac(uv_pixels) - float2(0.5,0.5);
				float2 ddxy = fwidth(uv_pixels);
				float2 mip = log2(ddxy) - 0.5;
				float2 clampedUV = uvs + (clamp(delta_pixel / ddxy, 0.0, 1.0) - delta_pixel) * pixel;
				return tex2Dlod(tex, float4(clampedUV,0, min(mip.x, mip.y)));
			}
			
			float MyCustomExpression16_g11755( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11757( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11761( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float3 RGBToHSV(float3 c)
			{
				float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
				float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
				float d = q.x - min( q.w, q.y );
				float e = 1.0e-10;
				return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
			}
			float3 MyCustomExpression115_g11766( float3 In, float3 From, float3 To, float Fuzziness, float Range )
			{
				float Distance = distance(From, In);
				return lerp(To, In, saturate((Distance - Range) / max(Fuzziness, 0.001)));
			}
			
			float3 HSVToRGB( float3 c )
			{
				float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
				float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
				return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
			}
			
			float MyCustomExpression16_g11789( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11773( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11800( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11808( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11846( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11843( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11845( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11835( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11837( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11828( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11830( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11831( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11823( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11821( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11822( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11817( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11849( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11854( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11852( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float2 MyCustomExpression10_g11864( float2 UV, int Width, int Height, float Tile, float2 Invert )
			{
				Tile = fmod(Tile + 0.001, Width * Height);
				float2 tileCount = float2(1, 1) / float2(Width, Height);
				float tileY = abs(Invert.y * Height - (floor(Tile * tileCount.x) + Invert.y * 1));
				 float tileX = abs(Invert.x * Width - ((Tile - Width * floor(Tile * tileCount.x)) + Invert.x * 1));
				return (UV + float2(tileX, tileY)) * tileCount;
			}
			
			float2 MyCustomExpression10_g11860( float2 UV, int Width, int Height, float Tile, float2 Invert )
			{
				Tile = fmod(Tile + 0.001, Width * Height);
				float2 tileCount = float2(1, 1) / float2(Width, Height);
				float tileY = abs(Invert.y * Height - (floor(Tile * tileCount.x) + Invert.y * 1));
				 float tileX = abs(Invert.x * Width - ((Tile - Width * floor(Tile * tileCount.x)) + Invert.x * 1));
				return (UV + float2(tileX, tileY)) * tileCount;
			}
			
			float MyCustomExpression16_g11871( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11879( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11881( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11877( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11873( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11875( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			

			VertexOutput vert(VertexInput v )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				float2 _ZeroVector = float2(0,0);
				float2 texCoord363 = v.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				#ifdef _ENABLESCREENTILING_ON
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult16_g11656 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11656 = ( ( ( (( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / 10.0 ) )).xy * _ScreenTilingScale ) + _ScreenTilingOffset ) * ( _ScreenTilingPixelsPerUnit * appendResult16_g11656 ) );
				#else
				float2 staticSwitch2_g11656 = texCoord363;
				#endif
				float3 ase_worldPos = TransformObjectToWorld( (v.vertex).xyz );
				#ifdef _ENABLEWORLDTILING_ON
				float2 appendResult16_g11657 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11657 = ( ( ( (ase_worldPos).xy * _WorldTilingScale ) + _WorldTilingOffset ) * ( _WorldTilingPixelsPerUnit * appendResult16_g11657 ) );
				#else
				float2 staticSwitch2_g11657 = staticSwitch2_g11656;
				#endif
				float2 originalUV460 = staticSwitch2_g11657;
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11658 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch449 = ( floor( ( originalUV460 * appendResult7_g11658 ) ) / appendResult7_g11658 );
				#else
				float2 staticSwitch449 = originalUV460;
				#endif
				float2 uvAfterPixelArt450 = staticSwitch449;
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11662 = uvAfterPixelArt450;
				float2 appendResult374 = (float2(_SpriteSheetRect.x , _SpriteSheetRect.y));
				float2 spriteRectMin376 = appendResult374;
				float2 break11_g11662 = spriteRectMin376;
				float2 appendResult375 = (float2(_SpriteSheetRect.z , _SpriteSheetRect.w));
				float2 spriteRectMax377 = appendResult375;
				float2 break10_g11662 = spriteRectMax377;
				float2 break9_g11662 = float2( 0,0 );
				float2 break8_g11662 = float2( 1,1 );
				float2 appendResult15_g11662 = (float2((break9_g11662.x + (break14_g11662.x - break11_g11662.x) * (break8_g11662.x - break9_g11662.x) / (break10_g11662.x - break11_g11662.x)) , (break9_g11662.y + (break14_g11662.y - break11_g11662.y) * (break8_g11662.y - break9_g11662.y) / (break10_g11662.y - break11_g11662.y))));
				float2 staticSwitch366 = appendResult15_g11662;
				#else
				float2 staticSwitch366 = uvAfterPixelArt450;
				#endif
				float2 fixedUV475 = staticSwitch366;
				#ifdef _ENABLESQUISH_ON
				float2 break77_g11897 = fixedUV475;
				float2 appendResult72_g11897 = (float2(( _SquishStretch * ( break77_g11897.x - 0.5 ) * _SquishFade ) , ( _SquishFade * ( break77_g11897.y + _SquishFlip ) * -_SquishSquish )));
				float2 staticSwitch198 = ( appendResult72_g11897 + _ZeroVector );
				#else
				float2 staticSwitch198 = _ZeroVector;
				#endif
				float2 temp_output_2_0_g11899 = staticSwitch198;
				#ifdef _TOGGLECUSTOMTIME_ON
				float staticSwitch44_g11661 = _TimeValue;
				#else
				float staticSwitch44_g11661 = _TimeParameters.x;
				#endif
				#ifdef _TOGGLEUNSCALEDTIME_ON
				float staticSwitch34_g11661 = UnscaledTime;
				#else
				float staticSwitch34_g11661 = staticSwitch44_g11661;
				#endif
				#ifdef _TOGGLETIMESPEED_ON
				float staticSwitch37_g11661 = ( staticSwitch34_g11661 * _TimeSpeed );
				#else
				float staticSwitch37_g11661 = staticSwitch34_g11661;
				#endif
				#ifdef _TOGGLETIMEFPS_ON
				float staticSwitch38_g11661 = ( floor( ( staticSwitch37_g11661 * _TimeFPS ) ) / _TimeFPS );
				#else
				float staticSwitch38_g11661 = staticSwitch37_g11661;
				#endif
				#ifdef _TOGGLETIMEFREQUENCY_ON
				float staticSwitch42_g11661 = ( ( sin( ( staticSwitch38_g11661 * _TimeFrequency ) ) * _TimeRange ) + 100.0 );
				#else
				float staticSwitch42_g11661 = staticSwitch38_g11661;
				#endif
				float shaderTime237 = staticSwitch42_g11661;
				float temp_output_8_0_g11899 = shaderTime237;
				#ifdef _ENABLESINEMOVE_ON
				float2 staticSwitch4_g11899 = ( ( sin( ( temp_output_8_0_g11899 * _SineMoveFrequency ) ) * _SineMoveOffset * _SineMoveFade ) + temp_output_2_0_g11899 );
				#else
				float2 staticSwitch4_g11899 = temp_output_2_0_g11899;
				#endif
				#ifdef _ENABLEVIBRATE_ON
				float temp_output_30_0_g11900 = temp_output_8_0_g11899;
				float3 rotatedValue21_g11900 = RotateAroundAxis( float3( 0,0,0 ), float3( 0,1,0 ), float3( 0,0,1 ), ( temp_output_30_0_g11900 * _VibrateRotation ) );
				float2 staticSwitch6_g11899 = ( ( sin( ( _VibrateFrequency * temp_output_30_0_g11900 ) ) * _VibrateOffset * _VibrateFade * (rotatedValue21_g11900).xy ) + staticSwitch4_g11899 );
				#else
				float2 staticSwitch6_g11899 = staticSwitch4_g11899;
				#endif
				#ifdef _ENABLESINESCALE_ON
				float2 staticSwitch10_g11899 = ( staticSwitch6_g11899 + ( (v.vertex).xy * ( ( ( sin( ( _SineScaleFrequency * temp_output_8_0_g11899 ) ) + 1.0 ) * 0.5 ) * _SineScaleFactor ) ) );
				#else
				float2 staticSwitch10_g11899 = staticSwitch6_g11899;
				#endif
				float2 temp_output_424_0 = staticSwitch10_g11899;
#ifdef _SHADERFADING_MASK
				float2 uv_FadingMask = v.ase_texcoord.xy * _FadingMask_ST.xy + _FadingMask_ST.zw;
				#endif
#ifdef _SHADERFADING_MASK
				float4 tex2DNode3_g11712 = tex2Dlod( _FadingMask, float4( uv_FadingMask, 0, 0.0) );
				#endif
				float temp_output_4_0_g11715 = max( _FadingWidth , 0.001 );
				float2 texCoord435 = v.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				#ifdef _PIXELPERFECTSPACE_ON
				float2 temp_output_432_0 = (_MainTex_TexelSize).zw;
				float2 staticSwitch437 = ( floor( ( texCoord435 * temp_output_432_0 ) ) / temp_output_432_0 );
				#else
				float2 staticSwitch437 = texCoord435;
				#endif
				float2 temp_output_61_0_g11663 = staticSwitch437;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 texCoord23_g11663 = v.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERSPACE_UI_GRAPHIC
				float2 appendResult28_g11663 = (float2(_RectWidth , _RectHeight));
				#endif
				#if defined(_SHADERSPACE_UV)
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#elif defined(_SHADERSPACE_UV_RAW)
				float2 staticSwitch1_g11663 = temp_output_61_0_g11663;
				#elif defined(_SHADERSPACE_OBJECT)
				float2 staticSwitch1_g11663 = (v.vertex).xy;
				#elif defined(_SHADERSPACE_OBJECT_SCALED)
				float2 staticSwitch1_g11663 = ( (v.vertex).xy * (ase_objectScale).xy );
				#elif defined(_SHADERSPACE_WORLD)
				float2 staticSwitch1_g11663 = (ase_worldPos).xy;
				#elif defined(_SHADERSPACE_UI_GRAPHIC)
				float2 staticSwitch1_g11663 = ( texCoord23_g11663 * ( appendResult28_g11663 / _PixelsPerUnit ) );
				#elif defined(_SHADERSPACE_SCREEN)
				float2 staticSwitch1_g11663 = ( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / _ScreenWidthUnits ) );
				#else
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#endif
				float2 shaderPosition235 = staticSwitch1_g11663;
				float linValue16_g11716 = tex2Dlod( _UberNoiseTexture, float4( ( shaderPosition235 * _FadingNoiseScale ), 0, 0.0) ).r;
				float localMyCustomExpression16_g11716 = MyCustomExpression16_g11716( linValue16_g11716 );
				float clampResult14_g11715 = clamp( ( ( ( _FadingFade * ( 1.0 + temp_output_4_0_g11715 ) ) - localMyCustomExpression16_g11716 ) / temp_output_4_0_g11715 ) , 0.0 , 1.0 );
				float2 temp_output_27_0_g11713 = shaderPosition235;
				float linValue16_g11714 = tex2Dlod( _UberNoiseTexture, float4( ( temp_output_27_0_g11713 * _FadingNoiseScale ), 0, 0.0) ).r;
				float localMyCustomExpression16_g11714 = MyCustomExpression16_g11714( linValue16_g11714 );
#ifdef _SHADERFADING_SPREAD
				float clampResult3_g11713 = clamp( ( ( _FadingFade - ( distance( _FadingPosition , temp_output_27_0_g11713 ) + ( localMyCustomExpression16_g11714 * _FadingNoiseFactor ) ) ) / max( _FadingWidth , 0.001 ) ) , 0.0 , 1.0 );
				#endif
				#if defined(_SHADERFADING_NONE)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_FULL)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_MASK)
				float staticSwitch139 = ( _FadingFade * ( tex2DNode3_g11712.r * tex2DNode3_g11712.a ) );
				#elif defined(_SHADERFADING_DISSOLVE)
				float staticSwitch139 = clampResult14_g11715;
				#elif defined(_SHADERFADING_SPREAD)
				float staticSwitch139 = clampResult3_g11713;
				#else
				float staticSwitch139 = _FadingFade;
				#endif
				float fullFade123 = staticSwitch139;
				float2 lerpResult121 = lerp( float2( 0,0 ) , temp_output_424_0 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float2 staticSwitch142 = temp_output_424_0;
				#elif defined(_SHADERFADING_FULL)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_MASK)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_DISSOLVE)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_SPREAD)
				float2 staticSwitch142 = lerpResult121;
				#else
				float2 staticSwitch142 = temp_output_424_0;
				#endif
				
				o.ase_texcoord1 = screenPos;
				o.ase_texcoord2.xyz = ase_worldPos;
				
				o.ase_texcoord.xy = v.ase_texcoord.xy;
				o.ase_texcoord3 = float4(v.vertex,1);
				o.ase_color = v.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord.zw = 0;
				o.ase_texcoord2.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = float3( staticSwitch142 ,  0.0 );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
				float3 positionWS = TransformObjectToWorld(v.vertex);
				o.clipPos = TransformWorldToHClip(positionWS);

				return o;
			}

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				float2 texCoord363 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float4 screenPos = IN.ase_texcoord1;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				#ifdef _ENABLESCREENTILING_ON
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult16_g11656 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11656 = ( ( ( (( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / 10.0 ) )).xy * _ScreenTilingScale ) + _ScreenTilingOffset ) * ( _ScreenTilingPixelsPerUnit * appendResult16_g11656 ) );
				#else
				float2 staticSwitch2_g11656 = texCoord363;
				#endif
				float3 ase_worldPos = IN.ase_texcoord2.xyz;
				#ifdef _ENABLEWORLDTILING_ON
				float2 appendResult16_g11657 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11657 = ( ( ( (ase_worldPos).xy * _WorldTilingScale ) + _WorldTilingOffset ) * ( _WorldTilingPixelsPerUnit * appendResult16_g11657 ) );
				#else
				float2 staticSwitch2_g11657 = staticSwitch2_g11656;
				#endif
				float2 originalUV460 = staticSwitch2_g11657;
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11658 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch449 = ( floor( ( originalUV460 * appendResult7_g11658 ) ) / appendResult7_g11658 );
				#else
				float2 staticSwitch449 = originalUV460;
				#endif
				float2 uvAfterPixelArt450 = staticSwitch449;
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11662 = uvAfterPixelArt450;
				float2 appendResult374 = (float2(_SpriteSheetRect.x , _SpriteSheetRect.y));
				float2 spriteRectMin376 = appendResult374;
				float2 break11_g11662 = spriteRectMin376;
				float2 appendResult375 = (float2(_SpriteSheetRect.z , _SpriteSheetRect.w));
				float2 spriteRectMax377 = appendResult375;
				float2 break10_g11662 = spriteRectMax377;
				float2 break9_g11662 = float2( 0,0 );
				float2 break8_g11662 = float2( 1,1 );
				float2 appendResult15_g11662 = (float2((break9_g11662.x + (break14_g11662.x - break11_g11662.x) * (break8_g11662.x - break9_g11662.x) / (break10_g11662.x - break11_g11662.x)) , (break9_g11662.y + (break14_g11662.y - break11_g11662.y) * (break8_g11662.y - break9_g11662.y) / (break10_g11662.y - break11_g11662.y))));
				float2 staticSwitch366 = appendResult15_g11662;
				#else
				float2 staticSwitch366 = uvAfterPixelArt450;
				#endif
				float2 fixedUV475 = staticSwitch366;
				float2 temp_output_3_0_g11664 = fixedUV475;
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch117_g11665 = _WindMinIntensity;
				#else
				float staticSwitch117_g11665 = WindMinIntensity;
				#endif
				#endif
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch118_g11665 = _WindMaxIntensity;
				#else
				float staticSwitch118_g11665 = WindMaxIntensity;
				#endif
				#endif
				float4 transform62_g11665 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDISPARALLAX_ON
				float staticSwitch111_g11665 = _WindXPosition;
				#else
				float staticSwitch111_g11665 = transform62_g11665.x;
				#endif
				#endif
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch113_g11665 = _WindNoiseScale;
				#else
				float staticSwitch113_g11665 = WindNoiseScale;
				#endif
				#endif
				#ifdef _TOGGLECUSTOMTIME_ON
				float staticSwitch44_g11661 = _TimeValue;
				#else
				float staticSwitch44_g11661 = _TimeParameters.x;
				#endif
				#ifdef _TOGGLEUNSCALEDTIME_ON
				float staticSwitch34_g11661 = UnscaledTime;
				#else
				float staticSwitch34_g11661 = staticSwitch44_g11661;
				#endif
				#ifdef _TOGGLETIMESPEED_ON
				float staticSwitch37_g11661 = ( staticSwitch34_g11661 * _TimeSpeed );
				#else
				float staticSwitch37_g11661 = staticSwitch34_g11661;
				#endif
				#ifdef _TOGGLETIMEFPS_ON
				float staticSwitch38_g11661 = ( floor( ( staticSwitch37_g11661 * _TimeFPS ) ) / _TimeFPS );
				#else
				float staticSwitch38_g11661 = staticSwitch37_g11661;
				#endif
				#ifdef _TOGGLETIMEFREQUENCY_ON
				float staticSwitch42_g11661 = ( ( sin( ( staticSwitch38_g11661 * _TimeFrequency ) ) * _TimeRange ) + 100.0 );
				#else
				float staticSwitch42_g11661 = staticSwitch38_g11661;
				#endif
				float shaderTime237 = staticSwitch42_g11661;
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch125_g11665 = ( shaderTime237 * _WindNoiseSpeed );
				#else
				float staticSwitch125_g11665 = WindTime;
				#endif
				float temp_output_50_0_g11665 = ( ( staticSwitch111_g11665 * staticSwitch113_g11665 ) + staticSwitch125_g11665 );
				float x101_g11665 = temp_output_50_0_g11665;
				float localFastNoise101_g11665 = FastNoise101_g11665( x101_g11665 );
				float2 temp_cast_0 = (temp_output_50_0_g11665).xx;
				float simplePerlin2D121_g11665 = snoise( temp_cast_0*0.5 );
				simplePerlin2D121_g11665 = simplePerlin2D121_g11665*0.5 + 0.5;
				#ifdef _WINDHIGHQUALITYNOISE_ON
				float staticSwitch123_g11665 = simplePerlin2D121_g11665;
				#else
				float staticSwitch123_g11665 = ( localFastNoise101_g11665 + 0.5 );
				#endif
				float lerpResult86_g11665 = lerp( staticSwitch117_g11665 , staticSwitch118_g11665 , staticSwitch123_g11665);
				float clampResult29_g11665 = clamp( ( ( _WindRotationWindFactor * lerpResult86_g11665 ) + _WindRotation ) , -_WindMaxRotation , _WindMaxRotation );
				float2 temp_output_1_0_g11665 = temp_output_3_0_g11664;
				float temp_output_39_0_g11665 = ( temp_output_1_0_g11665.y + _WindFlip );
				float3 appendResult43_g11665 = (float3(0.5 , -_WindFlip , 0.0));
				float2 appendResult27_g11665 = (float2(0.0 , ( _WindSquishFactor * min( ( ( _WindSquishWindFactor * abs( lerpResult86_g11665 ) ) + abs( _WindRotation ) ) , _WindMaxRotation ) * temp_output_39_0_g11665 )));
				float3 rotatedValue19_g11665 = RotateAroundAxis( appendResult43_g11665, float3( ( appendResult27_g11665 + temp_output_1_0_g11665 ) ,  0.0 ), float3( 0,0,1 ), ( clampResult29_g11665 * temp_output_39_0_g11665 ) );
				float2 staticSwitch4_g11664 = (rotatedValue19_g11665).xy;
				#else
				float2 staticSwitch4_g11664 = temp_output_3_0_g11664;
				#endif
				float2 texCoord435 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				#ifdef _PIXELPERFECTSPACE_ON
				float2 temp_output_432_0 = (_MainTex_TexelSize).zw;
				float2 staticSwitch437 = ( floor( ( texCoord435 * temp_output_432_0 ) ) / temp_output_432_0 );
				#else
				float2 staticSwitch437 = texCoord435;
				#endif
				float2 temp_output_61_0_g11663 = staticSwitch437;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 texCoord23_g11663 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERSPACE_UI_GRAPHIC
				float2 appendResult28_g11663 = (float2(_RectWidth , _RectHeight));
				#endif
				#if defined(_SHADERSPACE_UV)
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#elif defined(_SHADERSPACE_UV_RAW)
				float2 staticSwitch1_g11663 = temp_output_61_0_g11663;
				#elif defined(_SHADERSPACE_OBJECT)
				float2 staticSwitch1_g11663 = (IN.ase_texcoord3.xyz).xy;
				#elif defined(_SHADERSPACE_OBJECT_SCALED)
				float2 staticSwitch1_g11663 = ( (IN.ase_texcoord3.xyz).xy * (ase_objectScale).xy );
				#elif defined(_SHADERSPACE_WORLD)
				float2 staticSwitch1_g11663 = (ase_worldPos).xy;
				#elif defined(_SHADERSPACE_UI_GRAPHIC)
				float2 staticSwitch1_g11663 = ( texCoord23_g11663 * ( appendResult28_g11663 / _PixelsPerUnit ) );
				#elif defined(_SHADERSPACE_SCREEN)
				float2 staticSwitch1_g11663 = ( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / _ScreenWidthUnits ) );
				#else
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#endif
				float2 shaderPosition235 = staticSwitch1_g11663;
				#ifdef _ENABLEFULLDISTORTION_ON
				float2 temp_output_195_0_g11666 = shaderPosition235;
				float linValue16_g11667 = tex2D( _UberNoiseTexture, ( temp_output_195_0_g11666 * _FullDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11667 = MyCustomExpression16_g11667( linValue16_g11667 );
				float linValue16_g11668 = tex2D( _UberNoiseTexture, ( ( temp_output_195_0_g11666 + float2( 0.321,0.321 ) ) * _FullDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11668 = MyCustomExpression16_g11668( linValue16_g11668 );
				float2 appendResult189_g11666 = (float2(( localMyCustomExpression16_g11667 - 0.5 ) , ( localMyCustomExpression16_g11668 - 0.5 )));
				float2 staticSwitch83 = ( staticSwitch4_g11664 + ( ( 1.0 - _FullDistortionFade ) * appendResult189_g11666 * _FullDistortionDistortion ) );
				#else
				float2 staticSwitch83 = staticSwitch4_g11664;
				#endif
				#ifdef _ENABLEDIRECTIONALDISTORTION_ON
				float2 temp_output_182_0_g11669 = shaderPosition235;
				float linValue16_g11671 = tex2D( _UberNoiseTexture, ( temp_output_182_0_g11669 * _DirectionalDistortionDistortionScale ) ).r;
				float localMyCustomExpression16_g11671 = MyCustomExpression16_g11671( linValue16_g11671 );
				float3 rotatedValue168_g11669 = RotateAroundAxis( float3( 0,0,0 ), float3( _DirectionalDistortionDistortion ,  0.0 ), float3( 0,0,1 ), ( ( ( localMyCustomExpression16_g11671 - 0.5 ) * 2.0 * _DirectionalDistortionRandomDirection ) * PI ) );
				float3 rotatedValue136_g11669 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_182_0_g11669 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalDistortionRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11669 = rotatedValue136_g11669;
				float linValue16_g11670 = tex2D( _UberNoiseTexture, ( temp_output_182_0_g11669 * _DirectionalDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11670 = MyCustomExpression16_g11670( linValue16_g11670 );
				float clampResult154_g11669 = clamp( ( ( break130_g11669.x + break130_g11669.y + _DirectionalDistortionFade + ( localMyCustomExpression16_g11670 * _DirectionalDistortionNoiseFactor ) ) / max( _DirectionalDistortionWidth , 0.001 ) ) , 0.0 , 1.0 );
				float2 staticSwitch82 = ( staticSwitch83 + ( (rotatedValue168_g11669).xy * ( 1.0 - (( _DirectionalDistortionInvert )?( ( 1.0 - clampResult154_g11669 ) ):( clampResult154_g11669 )) ) ) );
				#else
				float2 staticSwitch82 = staticSwitch83;
				#endif
				#ifdef _ENABLEHOLOGRAM_ON
				float temp_output_8_0_g11674 = ( ( ( shaderTime237 * _HologramDistortionSpeed ) + ase_worldPos.y ) / unity_OrthoParams.y );
				float2 temp_cast_4 = (temp_output_8_0_g11674).xx;
				float2 temp_cast_5 = (_HologramDistortionDensity).xx;
				float linValue16_g11676 = tex2D( _UberNoiseTexture, ( temp_cast_4 * temp_cast_5 ) ).r;
				float localMyCustomExpression16_g11676 = MyCustomExpression16_g11676( linValue16_g11676 );
				float clampResult75_g11674 = clamp( localMyCustomExpression16_g11676 , 0.075 , 0.6 );
				float2 temp_cast_6 = (temp_output_8_0_g11674).xx;
				float2 temp_cast_7 = (_HologramDistortionScale).xx;
				float linValue16_g11677 = tex2D( _UberNoiseTexture, ( temp_cast_6 * temp_cast_7 ) ).r;
				float localMyCustomExpression16_g11677 = MyCustomExpression16_g11677( linValue16_g11677 );
				float2 appendResult2_g11675 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float hologramFade182 = _HologramFade;
				float2 appendResult44_g11674 = (float2(( ( ( clampResult75_g11674 * ( localMyCustomExpression16_g11677 - 0.5 ) ) * _HologramDistortionOffset * ( 100.0 / appendResult2_g11675 ).x ) * hologramFade182 ) , 0.0));
				float2 staticSwitch59 = ( staticSwitch82 + appendResult44_g11674 );
				#else
				float2 staticSwitch59 = staticSwitch82;
				#endif
				#ifdef _ENABLEGLITCH_ON
				float2 temp_output_18_0_g11672 = shaderPosition235;
				float2 glitchPosition154 = temp_output_18_0_g11672;
				float linValue16_g11718 = tex2D( _UberNoiseTexture, ( ( glitchPosition154 + ( _GlitchDistortionSpeed * shaderTime237 ) ) * _GlitchDistortionScale ) ).r;
				float localMyCustomExpression16_g11718 = MyCustomExpression16_g11718( linValue16_g11718 );
				float linValue16_g11673 = tex2D( _UberNoiseTexture, ( ( temp_output_18_0_g11672 + ( _GlitchMaskSpeed * shaderTime237 ) ) * _GlitchMaskScale ) ).r;
				float localMyCustomExpression16_g11673 = MyCustomExpression16_g11673( linValue16_g11673 );
				float glitchFade152 = ( max( localMyCustomExpression16_g11673 , _GlitchMaskMin ) * _GlitchFade );
				float2 staticSwitch62 = ( staticSwitch59 + ( ( localMyCustomExpression16_g11718 - 0.5 ) * _GlitchDistortion * glitchFade152 ) );
				#else
				float2 staticSwitch62 = staticSwitch59;
				#endif
				float2 temp_output_1_0_g11719 = staticSwitch62;
				float2 temp_output_26_0_g11719 = shaderPosition235;
				float temp_output_25_0_g11719 = shaderTime237;
				#ifdef _ENABLEUVDISTORT_ON
				float linValue16_g11729 = tex2D( _UberNoiseTexture, ( ( temp_output_26_0_g11719 + ( _UVDistortSpeed * temp_output_25_0_g11719 ) ) * _UVDistortNoiseScale ) ).r;
				float localMyCustomExpression16_g11729 = MyCustomExpression16_g11729( linValue16_g11729 );
				float2 lerpResult21_g11726 = lerp( _UVDistortFrom , _UVDistortTo , localMyCustomExpression16_g11729);
				float2 appendResult2_g11728 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 uv_UVDistortMask = IN.ase_texcoord.xy * _UVDistortMask_ST.xy + _UVDistortMask_ST.zw;
				float4 tex2DNode3_g11727 = tex2D( _UVDistortMask, uv_UVDistortMask );
				#ifdef _UVDISTORTMASKTOGGLE_ON
				float staticSwitch29_g11726 = ( _UVDistortFade * ( tex2DNode3_g11727.r * tex2DNode3_g11727.a ) );
				#else
				float staticSwitch29_g11726 = _UVDistortFade;
				#endif
				float2 staticSwitch5_g11719 = ( temp_output_1_0_g11719 + ( lerpResult21_g11726 * ( 100.0 / appendResult2_g11728 ) * staticSwitch29_g11726 ) );
				#else
				float2 staticSwitch5_g11719 = temp_output_1_0_g11719;
				#endif
				#ifdef _ENABLESQUEEZE_ON
				float2 temp_output_1_0_g11725 = staticSwitch5_g11719;
				float2 staticSwitch7_g11719 = ( temp_output_1_0_g11725 + ( ( temp_output_1_0_g11725 - _SqueezeCenter ) * pow( distance( temp_output_1_0_g11725 , _SqueezeCenter ) , _SqueezePower ) * _SqueezeScale * _SqueezeFade ) );
				#else
				float2 staticSwitch7_g11719 = staticSwitch5_g11719;
				#endif
				#ifdef _ENABLESINEROTATE_ON
				float3 rotatedValue36_g11724 = RotateAroundAxis( float3( _SineRotatePivot ,  0.0 ), float3( staticSwitch7_g11719 ,  0.0 ), float3( 0,0,1 ), ( sin( ( temp_output_25_0_g11719 * _SineRotateFrequency ) ) * ( ( _SineRotateAngle / 360.0 ) * PI ) * _SineRotateFade ) );
				float2 staticSwitch9_g11719 = (rotatedValue36_g11724).xy;
				#else
				float2 staticSwitch9_g11719 = staticSwitch7_g11719;
				#endif
				#ifdef _ENABLEUVROTATE_ON
				float3 rotatedValue8_g11723 = RotateAroundAxis( float3( _UVRotatePivot ,  0.0 ), float3( staticSwitch9_g11719 ,  0.0 ), float3( 0,0,1 ), ( temp_output_25_0_g11719 * _UVRotateSpeed * PI ) );
				float2 staticSwitch16_g11719 = (rotatedValue8_g11723).xy;
				#else
				float2 staticSwitch16_g11719 = staticSwitch9_g11719;
				#endif
				#ifdef _ENABLEUVSCROLL_ON
				float2 staticSwitch14_g11719 = ( ( _UVScrollSpeed * temp_output_25_0_g11719 ) + staticSwitch16_g11719 );
				#else
				float2 staticSwitch14_g11719 = staticSwitch16_g11719;
				#endif
				#ifdef _ENABLEPIXELATE_ON
				float2 appendResult35_g11721 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 MultFactor30_g11721 = ( ( _PixelatePixelDensity * ( appendResult35_g11721 / _PixelatePixelsPerUnit ) ) * ( 1.0 / max( _PixelateFade , 1E-05 ) ) );
				float2 clampResult46_g11721 = clamp( ( floor( ( MultFactor30_g11721 * ( staticSwitch14_g11719 + ( float2( 0.5,0.5 ) / MultFactor30_g11721 ) ) ) ) / MultFactor30_g11721 ) , float2( 0,0 ) , float2( 1,1 ) );
				float2 staticSwitch4_g11719 = clampResult46_g11721;
				#else
				float2 staticSwitch4_g11719 = staticSwitch14_g11719;
				#endif
				#ifdef _ENABLEUVSCALE_ON
				float2 staticSwitch24_g11719 = ( ( ( staticSwitch4_g11719 - _UVScalePivot ) / _UVScaleScale ) + _UVScalePivot );
				#else
				float2 staticSwitch24_g11719 = staticSwitch4_g11719;
				#endif
				float2 temp_output_1_0_g11730 = staticSwitch24_g11719;
				#ifdef _ENABLEWIGGLE_ON
				float temp_output_7_0_g11730 = ( sin( ( _WiggleFrequency * ( temp_output_26_0_g11719.y + ( _WiggleSpeed * temp_output_25_0_g11719 ) ) ) ) * _WiggleOffset * _WiggleFade );
				#ifdef _WIGGLEFIXEDGROUNDTOGGLE_ON
				float staticSwitch18_g11730 = ( temp_output_7_0_g11730 * temp_output_1_0_g11730.y );
				#else
				float staticSwitch18_g11730 = temp_output_7_0_g11730;
				#endif
				float2 appendResult12_g11730 = (float2(staticSwitch18_g11730 , 0.0));
				float2 staticSwitch13_g11730 = ( temp_output_1_0_g11730 + appendResult12_g11730 );
				#else
				float2 staticSwitch13_g11730 = temp_output_1_0_g11730;
				#endif
				float2 temp_output_484_0 = staticSwitch13_g11730;
				float2 texCoord131 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERFADING_MASK
				float2 uv_FadingMask = IN.ase_texcoord.xy * _FadingMask_ST.xy + _FadingMask_ST.zw;
				#endif
#ifdef _SHADERFADING_MASK
				float4 tex2DNode3_g11712 = tex2D( _FadingMask, uv_FadingMask );
				#endif
				float temp_output_4_0_g11715 = max( _FadingWidth , 0.001 );
				float linValue16_g11716 = tex2D( _UberNoiseTexture, ( shaderPosition235 * _FadingNoiseScale ) ).r;
				float localMyCustomExpression16_g11716 = MyCustomExpression16_g11716( linValue16_g11716 );
				float clampResult14_g11715 = clamp( ( ( ( _FadingFade * ( 1.0 + temp_output_4_0_g11715 ) ) - localMyCustomExpression16_g11716 ) / temp_output_4_0_g11715 ) , 0.0 , 1.0 );
				float2 temp_output_27_0_g11713 = shaderPosition235;
				float linValue16_g11714 = tex2D( _UberNoiseTexture, ( temp_output_27_0_g11713 * _FadingNoiseScale ) ).r;
				float localMyCustomExpression16_g11714 = MyCustomExpression16_g11714( linValue16_g11714 );
#ifdef _SHADERFADING_SPREAD
				float clampResult3_g11713 = clamp( ( ( _FadingFade - ( distance( _FadingPosition , temp_output_27_0_g11713 ) + ( localMyCustomExpression16_g11714 * _FadingNoiseFactor ) ) ) / max( _FadingWidth , 0.001 ) ) , 0.0 , 1.0 );
				#endif
				#if defined(_SHADERFADING_NONE)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_FULL)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_MASK)
				float staticSwitch139 = ( _FadingFade * ( tex2DNode3_g11712.r * tex2DNode3_g11712.a ) );
				#elif defined(_SHADERFADING_DISSOLVE)
				float staticSwitch139 = clampResult14_g11715;
				#elif defined(_SHADERFADING_SPREAD)
				float staticSwitch139 = clampResult3_g11713;
				#else
				float staticSwitch139 = _FadingFade;
				#endif
				float fullFade123 = staticSwitch139;
				float2 lerpResult130 = lerp( texCoord131 , temp_output_484_0 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float2 staticSwitch145 = temp_output_484_0;
				#elif defined(_SHADERFADING_FULL)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_MASK)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_DISSOLVE)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_SPREAD)
				float2 staticSwitch145 = lerpResult130;
				#else
				float2 staticSwitch145 = temp_output_484_0;
				#endif
				#ifdef _TILINGFIX_ON
				float2 staticSwitch485 = ( ( ( staticSwitch145 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				#else
				float2 staticSwitch485 = staticSwitch145;
				#endif
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11731 = staticSwitch485;
				float2 break11_g11731 = float2( 0,0 );
				float2 break10_g11731 = float2( 1,1 );
				float2 break9_g11731 = spriteRectMin376;
				float2 break8_g11731 = spriteRectMax377;
				float2 appendResult15_g11731 = (float2((break9_g11731.x + (break14_g11731.x - break11_g11731.x) * (break8_g11731.x - break9_g11731.x) / (break10_g11731.x - break11_g11731.x)) , (break9_g11731.y + (break14_g11731.y - break11_g11731.y) * (break8_g11731.y - break9_g11731.y) / (break10_g11731.y - break11_g11731.y))));
				float2 staticSwitch371 = min( max( appendResult15_g11731 , spriteRectMin376 ) , spriteRectMax377 );
				#else
				float2 staticSwitch371 = staticSwitch485;
				#endif
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11732 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch427 = ( originalUV460 + ( floor( ( ( staticSwitch371 - uvAfterPixelArt450 ) * appendResult7_g11732 ) ) / appendResult7_g11732 ) );
				#else
				float2 staticSwitch427 = staticSwitch371;
				#endif
				float2 finalUV146 = staticSwitch427;
				float2 temp_output_1_0_g11733 = finalUV146;
				#ifdef _ENABLESMOOTHPIXELART_ON
				sampler2D tex3_g11734 = _MainTex;
				float4 textureTexelSize3_g11734 = _MainTex_TexelSize;
				float2 uvs3_g11734 = temp_output_1_0_g11733;
				float4 localtexturePointSmooth3_g11734 = texturePointSmooth( tex3_g11734 , textureTexelSize3_g11734 , uvs3_g11734 );
				float4 staticSwitch8_g11733 = localtexturePointSmooth3_g11734;
				#else
				float4 staticSwitch8_g11733 = tex2D( _MainTex, temp_output_1_0_g11733 );
				#endif
				#ifdef _ENABLEGAUSSIANBLUR_ON
				float temp_output_10_0_g11735 = ( _GaussianBlurOffset * _GaussianBlurFade * 0.005 );
				float temp_output_2_0_g11745 = temp_output_10_0_g11735;
				float2 appendResult16_g11745 = (float2(temp_output_2_0_g11745 , 0.0));
				float2 appendResult25_g11747 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11747 = ( appendResult16_g11745 * appendResult25_g11747 );
				float2 temp_output_7_0_g11735 = temp_output_1_0_g11733;
				float2 temp_output_1_0_g11745 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( 1,1 ) ) );
				float2 temp_output_1_0_g11747 = temp_output_1_0_g11745;
				float2 appendResult17_g11745 = (float2(0.0 , temp_output_2_0_g11745));
				float2 appendResult25_g11746 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11746 = ( appendResult17_g11745 * appendResult25_g11746 );
				float2 temp_output_1_0_g11746 = temp_output_1_0_g11745;
				float temp_output_2_0_g11736 = temp_output_10_0_g11735;
				float2 appendResult16_g11736 = (float2(temp_output_2_0_g11736 , 0.0));
				float2 appendResult25_g11738 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11738 = ( appendResult16_g11736 * appendResult25_g11738 );
				float2 temp_output_1_0_g11736 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( -1,1 ) ) );
				float2 temp_output_1_0_g11738 = temp_output_1_0_g11736;
				float2 appendResult17_g11736 = (float2(0.0 , temp_output_2_0_g11736));
				float2 appendResult25_g11737 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11737 = ( appendResult17_g11736 * appendResult25_g11737 );
				float2 temp_output_1_0_g11737 = temp_output_1_0_g11736;
				float temp_output_2_0_g11742 = temp_output_10_0_g11735;
				float2 appendResult16_g11742 = (float2(temp_output_2_0_g11742 , 0.0));
				float2 appendResult25_g11744 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11744 = ( appendResult16_g11742 * appendResult25_g11744 );
				float2 temp_output_1_0_g11742 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( -1,-1 ) ) );
				float2 temp_output_1_0_g11744 = temp_output_1_0_g11742;
				float2 appendResult17_g11742 = (float2(0.0 , temp_output_2_0_g11742));
				float2 appendResult25_g11743 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11743 = ( appendResult17_g11742 * appendResult25_g11743 );
				float2 temp_output_1_0_g11743 = temp_output_1_0_g11742;
				float temp_output_2_0_g11739 = temp_output_10_0_g11735;
				float2 appendResult16_g11739 = (float2(temp_output_2_0_g11739 , 0.0));
				float2 appendResult25_g11741 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11741 = ( appendResult16_g11739 * appendResult25_g11741 );
				float2 temp_output_1_0_g11739 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( 1,-1 ) ) );
				float2 temp_output_1_0_g11741 = temp_output_1_0_g11739;
				float2 appendResult17_g11739 = (float2(0.0 , temp_output_2_0_g11739));
				float2 appendResult25_g11740 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11740 = ( appendResult17_g11739 * appendResult25_g11740 );
				float2 temp_output_1_0_g11740 = temp_output_1_0_g11739;
				float4 staticSwitch3_g11733 = ( ( ( ( tex2D( _MainTex, ( temp_output_26_0_g11747 + temp_output_1_0_g11747 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11747 + temp_output_1_0_g11747 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11746 + temp_output_1_0_g11746 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11746 + temp_output_1_0_g11746 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11738 + temp_output_1_0_g11738 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11738 + temp_output_1_0_g11738 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11737 + temp_output_1_0_g11737 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11737 + temp_output_1_0_g11737 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11744 + temp_output_1_0_g11744 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11744 + temp_output_1_0_g11744 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11743 + temp_output_1_0_g11743 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11743 + temp_output_1_0_g11743 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11741 + temp_output_1_0_g11741 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11741 + temp_output_1_0_g11741 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11740 + temp_output_1_0_g11740 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11740 + temp_output_1_0_g11740 ) ) ) ) ) * 0.0625 );
				#else
				float4 staticSwitch3_g11733 = staticSwitch8_g11733;
				#endif
				#ifdef _ENABLESHARPEN_ON
				float2 temp_output_1_0_g11748 = temp_output_1_0_g11733;
				float4 tex2DNode4_g11748 = tex2D( _MainTex, temp_output_1_0_g11748 );
				float temp_output_2_0_g11749 = _SharpenOffset;
				float2 appendResult16_g11749 = (float2(temp_output_2_0_g11749 , 0.0));
				float2 appendResult25_g11751 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11751 = ( appendResult16_g11749 * appendResult25_g11751 );
				float2 temp_output_1_0_g11749 = temp_output_1_0_g11748;
				float2 temp_output_1_0_g11751 = temp_output_1_0_g11749;
				float2 appendResult17_g11749 = (float2(0.0 , temp_output_2_0_g11749));
				float2 appendResult25_g11750 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11750 = ( appendResult17_g11749 * appendResult25_g11750 );
				float2 temp_output_1_0_g11750 = temp_output_1_0_g11749;
				float4 break22_g11748 = ( tex2DNode4_g11748 - ( ( ( ( ( tex2D( _MainTex, ( temp_output_26_0_g11751 + temp_output_1_0_g11751 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11751 + temp_output_1_0_g11751 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11750 + temp_output_1_0_g11750 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11750 + temp_output_1_0_g11750 ) ) ) ) / 4.0 ) - tex2DNode4_g11748 ) * ( _SharpenFactor * _SharpenFade ) ) );
				float clampResult23_g11748 = clamp( break22_g11748.a , 0.0 , 1.0 );
				float4 appendResult24_g11748 = (float4(break22_g11748.r , break22_g11748.g , break22_g11748.b , clampResult23_g11748));
				float4 staticSwitch12_g11733 = appendResult24_g11748;
				#else
				float4 staticSwitch12_g11733 = staticSwitch3_g11733;
				#endif
				float4 temp_output_471_0 = staticSwitch12_g11733;
				#ifdef _VERTEXTINTFIRST_ON
				float4 temp_output_1_0_g11752 = temp_output_471_0;
				float4 appendResult8_g11752 = (float4(( (temp_output_1_0_g11752).rgb * (IN.ase_color).rgb ) , temp_output_1_0_g11752.a));
				float4 staticSwitch354 = appendResult8_g11752;
				#else
				float4 staticSwitch354 = temp_output_471_0;
				#endif
				float4 originalColor191 = staticSwitch354;
				float4 temp_output_1_0_g11753 = originalColor191;
				float4 temp_output_1_0_g11754 = temp_output_1_0_g11753;
				float2 temp_output_7_0_g11753 = finalUV146;
				#ifdef _ENABLESMOKE_ON
				float2 temp_output_43_0_g11754 = temp_output_7_0_g11753;
				float2 temp_cast_15 = (_SmokeNoiseScale).xx;
				float linValue16_g11755 = tex2D( _UberNoiseTexture, ( ( ( IN.ase_color.r * (( _SmokeVertexSeed )?( 5.0 ):( 0.0 )) ) + temp_output_43_0_g11754 ) * temp_cast_15 ) ).r;
				float localMyCustomExpression16_g11755 = MyCustomExpression16_g11755( linValue16_g11755 );
				float clampResult28_g11754 = clamp( ( ( ( localMyCustomExpression16_g11755 - 1.0 ) * _SmokeNoiseFactor ) + ( ( ( IN.ase_color.a / 2.5 ) - distance( temp_output_43_0_g11754 , float2( 0.5,0.5 ) ) ) * 2.5 * _SmokeSmoothness ) ) , 0.0 , 1.0 );
				float3 lerpResult34_g11754 = lerp( (temp_output_1_0_g11754).rgb , float3( 0,0,0 ) , ( ( 1.0 - clampResult28_g11754 ) * _SmokeDarkEdge ));
				float4 appendResult31_g11754 = (float4(lerpResult34_g11754 , ( clampResult28_g11754 * _SmokeAlpha * temp_output_1_0_g11754.a )));
				float4 staticSwitch2_g11753 = appendResult31_g11754;
				#else
				float4 staticSwitch2_g11753 = temp_output_1_0_g11753;
				#endif
				#ifdef _ENABLECUSTOMFADE_ON
				float4 temp_output_1_0_g11756 = staticSwitch2_g11753;
				float2 temp_output_57_0_g11756 = temp_output_7_0_g11753;
				float4 tex2DNode3_g11756 = tex2D( _CustomFadeFadeMask, temp_output_57_0_g11756 );
				float linValue16_g11757 = tex2D( _UberNoiseTexture, ( temp_output_57_0_g11756 * _CustomFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11757 = MyCustomExpression16_g11757( linValue16_g11757 );
				float clampResult37_g11756 = clamp( ( ( ( IN.ase_color.a * 2.0 ) - 1.0 ) + ( tex2DNode3_g11756.r + ( localMyCustomExpression16_g11757 * _CustomFadeNoiseFactor ) ) ) , 0.0 , 1.0 );
				float4 appendResult13_g11756 = (float4((temp_output_1_0_g11756).rgb , ( temp_output_1_0_g11756.a * pow( clampResult37_g11756 , ( _CustomFadeSmoothness / max( tex2DNode3_g11756.r , 0.05 ) ) ) * _CustomFadeAlpha )));
				float4 staticSwitch3_g11753 = appendResult13_g11756;
				#else
				float4 staticSwitch3_g11753 = staticSwitch2_g11753;
				#endif
				float4 temp_output_1_0_g11758 = staticSwitch3_g11753;
				#ifdef _ENABLECHECKERBOARD_ON
				float4 temp_output_1_0_g11759 = temp_output_1_0_g11758;
				float2 appendResult4_g11759 = (float2(ase_worldPos.x , ase_worldPos.y));
				float2 temp_output_44_0_g11759 = ( appendResult4_g11759 * _CheckerboardTiling * 0.5 );
				float2 break12_g11759 = step( ( ceil( temp_output_44_0_g11759 ) - temp_output_44_0_g11759 ) , float2( 0.5,0.5 ) );
				float4 appendResult42_g11759 = (float4(( (temp_output_1_0_g11759).rgb * min( ( _CheckerboardDarken + abs( ( -break12_g11759.x + break12_g11759.y ) ) ) , 1.0 ) ) , temp_output_1_0_g11759.a));
				float4 staticSwitch2_g11758 = appendResult42_g11759;
				#else
				float4 staticSwitch2_g11758 = temp_output_1_0_g11758;
				#endif
				#ifdef _ENABLEFLAME_ON
				float2 temp_output_75_0_g11760 = finalUV146;
				float linValue16_g11761 = tex2D( _UberNoiseTexture, ( ( ( shaderTime237 * _FlameSpeed ) + temp_output_75_0_g11760 ) * _FlameNoiseScale ) ).r;
				float localMyCustomExpression16_g11761 = MyCustomExpression16_g11761( linValue16_g11761 );
				float saferPower57_g11760 = abs( max( ( temp_output_75_0_g11760.y - 0.2 ) , 0.0 ) );
				float temp_output_47_0_g11760 = max( _FlameRadius , 0.01 );
				float clampResult70_g11760 = clamp( ( ( ( localMyCustomExpression16_g11761 * pow( saferPower57_g11760 , _FlameNoiseHeightFactor ) * _FlameNoiseFactor ) + ( ( temp_output_47_0_g11760 - distance( temp_output_75_0_g11760 , float2( 0.5,0.4 ) ) ) / temp_output_47_0_g11760 ) ) * _FlameSmooth ) , 0.0 , 1.0 );
				float temp_output_63_0_g11760 = ( clampResult70_g11760 * _FlameBrightness );
				float4 appendResult31_g11760 = (float4(temp_output_63_0_g11760 , temp_output_63_0_g11760 , temp_output_63_0_g11760 , clampResult70_g11760));
				float4 staticSwitch6_g11758 = ( appendResult31_g11760 * staticSwitch2_g11758 );
				#else
				float4 staticSwitch6_g11758 = staticSwitch2_g11758;
				#endif
				float4 temp_output_3_0_g11762 = staticSwitch6_g11758;
				float4 temp_output_1_0_g11794 = temp_output_3_0_g11762;
				float4 appendResult91_g11794 = (float4((temp_output_1_0_g11794).rgb , 1.0));
				float2 temp_output_1_0_g11762 = finalUV146;
				#ifdef _ENABLERECOLORRGB_ON
				#ifdef _RECOLORRGBTEXTURETOGGLE_ON
				float4 staticSwitch81_g11794 = tex2D( _RecolorRGBTexture, temp_output_1_0_g11762 );
				#else
				float4 staticSwitch81_g11794 = appendResult91_g11794;
				#endif
				float3 break82_g11794 = (staticSwitch81_g11794).xyz;
				float temp_output_84_0_g11794 = max( ( break82_g11794.x + break82_g11794.y + break82_g11794.z ) , 0.001 );
				float4 break71_g11794 = ( ( _RecolorRGBRedTint * ( break82_g11794.x / temp_output_84_0_g11794 ) ) + ( _RecolorRGBGreenTint * ( break82_g11794.y / temp_output_84_0_g11794 ) ) + ( ( break82_g11794.z / temp_output_84_0_g11794 ) * _RecolorRGBBlueTint ) );
				float3 appendResult56_g11794 = (float3(break71_g11794.r , break71_g11794.g , break71_g11794.b));
				float4 break2_g11795 = temp_output_1_0_g11794;
				float saferPower57_g11794 = abs( ( ( break2_g11795.x + break2_g11795.x + break2_g11795.y + break2_g11795.y + break2_g11795.y + break2_g11795.z ) / 6.0 ) );
				float3 lerpResult26_g11794 = lerp( (temp_output_1_0_g11794).rgb , ( appendResult56_g11794 * pow( saferPower57_g11794 , ( max( break71_g11794.a , 0.01 ) * 2.0 ) ) ) , ( staticSwitch81_g11794.w * _RecolorRGBFade ));
				float4 appendResult30_g11794 = (float4(lerpResult26_g11794 , temp_output_1_0_g11794.a));
				float4 staticSwitch43_g11762 = appendResult30_g11794;
				#else
				float4 staticSwitch43_g11762 = temp_output_3_0_g11762;
				#endif
				#ifdef _ENABLERECOLORRGBYCP_ON
				float4 temp_output_1_0_g11792 = staticSwitch43_g11762;
				#ifdef _RECOLORRGBYCPTEXTURETOGGLE_ON
				float4 staticSwitch62_g11792 = tex2D( _RecolorRGBYCPTexture, temp_output_1_0_g11762 );
				#else
				float4 staticSwitch62_g11792 = temp_output_1_0_g11792;
				#endif
				float3 hsvTorgb33_g11792 = RGBToHSV( staticSwitch62_g11792.rgb );
				float temp_output_43_0_g11792 = ( ( hsvTorgb33_g11792.x + 0.08333334 ) % 1.0 );
				float4 ifLocalVar46_g11792 = 0;
				if( temp_output_43_0_g11792 >= 0.8333333 )
				ifLocalVar46_g11792 = _RecolorRGBYCPPurpleTint;
				else
				ifLocalVar46_g11792 = _RecolorRGBYCPBlueTint;
				float4 ifLocalVar44_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.6666667 )
				ifLocalVar44_g11792 = _RecolorRGBYCPCyanTint;
				else
				ifLocalVar44_g11792 = ifLocalVar46_g11792;
				float4 ifLocalVar47_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.3333333 )
				ifLocalVar47_g11792 = _RecolorRGBYCPYellowTint;
				else
				ifLocalVar47_g11792 = _RecolorRGBYCPGreenTint;
				float4 ifLocalVar45_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.1666667 )
				ifLocalVar45_g11792 = _RecolorRGBYCPRedTint;
				else
				ifLocalVar45_g11792 = ifLocalVar47_g11792;
				float4 ifLocalVar35_g11792 = 0;
				if( temp_output_43_0_g11792 >= 0.5 )
				ifLocalVar35_g11792 = ifLocalVar44_g11792;
				else
				ifLocalVar35_g11792 = ifLocalVar45_g11792;
				float4 break55_g11792 = ifLocalVar35_g11792;
				float3 appendResult56_g11792 = (float3(break55_g11792.r , break55_g11792.g , break55_g11792.b));
				float4 break2_g11793 = temp_output_1_0_g11792;
				float saferPower57_g11792 = abs( ( ( break2_g11793.x + break2_g11793.x + break2_g11793.y + break2_g11793.y + break2_g11793.y + break2_g11793.z ) / 6.0 ) );
				float3 lerpResult26_g11792 = lerp( (temp_output_1_0_g11792).rgb , ( appendResult56_g11792 * pow( saferPower57_g11792 , max( ( break55_g11792.a * 2.0 ) , 0.01 ) ) ) , ( hsvTorgb33_g11792.z * _RecolorRGBYCPFade ));
				float4 appendResult30_g11792 = (float4(lerpResult26_g11792 , temp_output_1_0_g11792.a));
				float4 staticSwitch9_g11762 = appendResult30_g11792;
				#else
				float4 staticSwitch9_g11762 = staticSwitch43_g11762;
				#endif
				#ifdef _ENABLECOLORREPLACE_ON
				float4 temp_output_1_0_g11766 = staticSwitch9_g11762;
				float3 temp_output_2_0_g11766 = (temp_output_1_0_g11766).rgb;
				float3 In115_g11766 = temp_output_2_0_g11766;
				float3 From115_g11766 = (_ColorReplaceFromColor).rgb;
				float4 break2_g11767 = temp_output_1_0_g11766;
				float3 To115_g11766 = ( pow( ( ( break2_g11767.x + break2_g11767.x + break2_g11767.y + break2_g11767.y + break2_g11767.y + break2_g11767.z ) / 6.0 ) , max( _ColorReplaceContrast , 0.001 ) ) * (_ColorReplaceToColor).rgb );
				float Fuzziness115_g11766 = _ColorReplaceSmoothness;
				float Range115_g11766 = _ColorReplaceRange;
				float3 localMyCustomExpression115_g11766 = MyCustomExpression115_g11766( In115_g11766 , From115_g11766 , To115_g11766 , Fuzziness115_g11766 , Range115_g11766 );
				float3 lerpResult112_g11766 = lerp( temp_output_2_0_g11766 , localMyCustomExpression115_g11766 , _ColorReplaceFade);
				float4 appendResult4_g11766 = (float4(lerpResult112_g11766 , temp_output_1_0_g11766.a));
				float4 staticSwitch29_g11762 = appendResult4_g11766;
				#else
				float4 staticSwitch29_g11762 = staticSwitch9_g11762;
				#endif
				float4 temp_output_1_0_g11779 = staticSwitch29_g11762;
				#ifdef _ENABLENEGATIVE_ON
				float3 temp_output_9_0_g11779 = (temp_output_1_0_g11779).rgb;
				float3 lerpResult3_g11779 = lerp( temp_output_9_0_g11779 , ( 1.0 - temp_output_9_0_g11779 ) , _NegativeFade);
				float4 appendResult8_g11779 = (float4(lerpResult3_g11779 , temp_output_1_0_g11779.a));
				float4 staticSwitch4_g11779 = appendResult8_g11779;
				#else
				float4 staticSwitch4_g11779 = temp_output_1_0_g11779;
				#endif
				float4 temp_output_57_0_g11762 = staticSwitch4_g11779;
				#ifdef _ENABLECONTRAST_ON
				float4 temp_output_1_0_g11802 = temp_output_57_0_g11762;
				float3 temp_cast_30 = (max( _Contrast , 0.001 )).xxx;
				float4 appendResult4_g11802 = (float4(pow( (temp_output_1_0_g11802).rgb , temp_cast_30 ) , temp_output_1_0_g11802.a));
				float4 staticSwitch32_g11762 = appendResult4_g11802;
				#else
				float4 staticSwitch32_g11762 = temp_output_57_0_g11762;
				#endif
				#ifdef _ENABLEBRIGHTNESS_ON
				float4 temp_output_2_0_g11777 = staticSwitch32_g11762;
				float4 appendResult6_g11777 = (float4(( (temp_output_2_0_g11777).rgb * _Brightness ) , temp_output_2_0_g11777.a));
				float4 staticSwitch33_g11762 = appendResult6_g11777;
				#else
				float4 staticSwitch33_g11762 = staticSwitch32_g11762;
				#endif
				#ifdef _ENABLEHUE_ON
				float4 temp_output_2_0_g11778 = staticSwitch33_g11762;
				float3 hsvTorgb1_g11778 = RGBToHSV( temp_output_2_0_g11778.rgb );
				float3 hsvTorgb3_g11778 = HSVToRGB( float3(( hsvTorgb1_g11778.x + _Hue ),hsvTorgb1_g11778.y,hsvTorgb1_g11778.z) );
				float4 appendResult8_g11778 = (float4(hsvTorgb3_g11778 , temp_output_2_0_g11778.a));
				float4 staticSwitch36_g11762 = appendResult8_g11778;
				#else
				float4 staticSwitch36_g11762 = staticSwitch33_g11762;
				#endif
				#ifdef _ENABLESPLITTONING_ON
				float4 temp_output_1_0_g11796 = staticSwitch36_g11762;
				float4 break2_g11797 = temp_output_1_0_g11796;
				float temp_output_3_0_g11796 = ( ( break2_g11797.x + break2_g11797.x + break2_g11797.y + break2_g11797.y + break2_g11797.y + break2_g11797.z ) / 6.0 );
				float clampResult25_g11796 = clamp( ( ( ( ( temp_output_3_0_g11796 + _SplitToningShift ) - 0.5 ) * _SplitToningBalance ) + 0.5 ) , 0.0 , 1.0 );
				float3 lerpResult6_g11796 = lerp( (_SplitToningShadowsColor).rgb , (_SplitToningHighlightsColor).rgb , clampResult25_g11796);
				float3 lerpResult11_g11796 = lerp( (temp_output_1_0_g11796).rgb , ( lerpResult6_g11796 * pow( temp_output_3_0_g11796 , max( _SplitToningContrast , 0.001 ) ) ) , _SplitToningFade);
				float4 appendResult18_g11796 = (float4(lerpResult11_g11796 , temp_output_1_0_g11796.a));
				float4 staticSwitch30_g11762 = appendResult18_g11796;
				#else
				float4 staticSwitch30_g11762 = staticSwitch36_g11762;
				#endif
				#ifdef _ENABLEBLACKTINT_ON
				float4 temp_output_1_0_g11774 = staticSwitch30_g11762;
				float3 temp_output_4_0_g11774 = (temp_output_1_0_g11774).rgb;
				float4 break12_g11774 = temp_output_1_0_g11774;
				float3 lerpResult7_g11774 = lerp( temp_output_4_0_g11774 , ( temp_output_4_0_g11774 + (_BlackTintColor).rgb ) , pow( ( 1.0 - min( max( max( break12_g11774.r , break12_g11774.g ) , break12_g11774.b ) , 1.0 ) ) , max( _BlackTintPower , 0.001 ) ));
				float3 lerpResult13_g11774 = lerp( temp_output_4_0_g11774 , lerpResult7_g11774 , _BlackTintFade);
				float4 appendResult11_g11774 = (float4(lerpResult13_g11774 , break12_g11774.a));
				float4 staticSwitch20_g11762 = appendResult11_g11774;
				#else
				float4 staticSwitch20_g11762 = staticSwitch30_g11762;
				#endif
				#ifdef _ENABLEINKSPREAD_ON
				float4 temp_output_1_0_g11788 = staticSwitch20_g11762;
				float4 break2_g11790 = temp_output_1_0_g11788;
				float2 temp_output_65_0_g11788 = shaderPosition235;
				float linValue16_g11789 = tex2D( _UberNoiseTexture, ( temp_output_65_0_g11788 * _InkSpreadNoiseScale ) ).r;
				float localMyCustomExpression16_g11789 = MyCustomExpression16_g11789( linValue16_g11789 );
				float clampResult53_g11788 = clamp( ( ( ( _InkSpreadDistance - distance( _InkSpreadPosition , temp_output_65_0_g11788 ) ) + ( localMyCustomExpression16_g11789 * _InkSpreadNoiseFactor ) ) / max( _InkSpreadWidth , 0.001 ) ) , 0.0 , 1.0 );
				float3 lerpResult7_g11788 = lerp( (temp_output_1_0_g11788).rgb , ( (_InkSpreadColor).rgb * pow( ( ( break2_g11790.x + break2_g11790.x + break2_g11790.y + break2_g11790.y + break2_g11790.y + break2_g11790.z ) / 6.0 ) , max( _InkSpreadContrast , 0.001 ) ) ) , ( _InkSpreadFade * clampResult53_g11788 ));
				float4 appendResult9_g11788 = (float4(lerpResult7_g11788 , (temp_output_1_0_g11788).a));
				float4 staticSwitch17_g11762 = appendResult9_g11788;
				#else
				float4 staticSwitch17_g11762 = staticSwitch20_g11762;
				#endif
				float temp_output_39_0_g11762 = shaderTime237;
				#ifdef _ENABLESHIFTHUE_ON
				float4 temp_output_1_0_g11780 = staticSwitch17_g11762;
				float3 hsvTorgb15_g11780 = RGBToHSV( (temp_output_1_0_g11780).rgb );
				float3 hsvTorgb19_g11780 = HSVToRGB( float3(( ( temp_output_39_0_g11762 * _ShiftHueSpeed ) + hsvTorgb15_g11780.x ),hsvTorgb15_g11780.y,hsvTorgb15_g11780.z) );
				float4 appendResult6_g11780 = (float4(hsvTorgb19_g11780 , temp_output_1_0_g11780.a));
				float4 staticSwitch19_g11762 = appendResult6_g11780;
				#else
				float4 staticSwitch19_g11762 = staticSwitch17_g11762;
				#endif
				#ifdef _ENABLEADDHUE_ON
				float3 hsvTorgb19_g11784 = HSVToRGB( float3(( ( temp_output_39_0_g11762 * _AddHueSpeed ) % 1.0 ),_AddHueSaturation,_AddHueBrightness) );
				float4 temp_output_1_0_g11784 = staticSwitch19_g11762;
				float4 break2_g11786 = temp_output_1_0_g11784;
				float2 uv_AddHueMask = IN.ase_texcoord.xy * _AddHueMask_ST.xy + _AddHueMask_ST.zw;
				float4 tex2DNode3_g11785 = tex2D( _AddHueMask, uv_AddHueMask );
				#ifdef _ADDHUEMASKTOGGLE_ON
				float staticSwitch33_g11784 = ( _AddHueFade * ( tex2DNode3_g11785.r * tex2DNode3_g11785.a ) );
				#else
				float staticSwitch33_g11784 = _AddHueFade;
				#endif
				float4 appendResult6_g11784 = (float4(( ( hsvTorgb19_g11784 * pow( ( ( break2_g11786.x + break2_g11786.x + break2_g11786.y + break2_g11786.y + break2_g11786.y + break2_g11786.z ) / 6.0 ) , max( _AddHueContrast , 0.001 ) ) * staticSwitch33_g11784 ) + (temp_output_1_0_g11784).rgb ) , temp_output_1_0_g11784.a));
				float4 staticSwitch23_g11762 = appendResult6_g11784;
				#else
				float4 staticSwitch23_g11762 = staticSwitch19_g11762;
				#endif
				#ifdef _ENABLESINEGLOW_ON
				float4 temp_output_1_0_g11781 = staticSwitch23_g11762;
				float4 break2_g11782 = temp_output_1_0_g11781;
				float3 temp_output_13_0_g11781 = (_SineGlowColor).rgb;
				float2 uv_SineGlowMask = IN.ase_texcoord.xy * _SineGlowMask_ST.xy + _SineGlowMask_ST.zw;
				float4 tex2DNode30_g11781 = tex2D( _SineGlowMask, uv_SineGlowMask );
				#ifdef _SINEGLOWMASKTOGGLE_ON
				float3 staticSwitch27_g11781 = ( (tex2DNode30_g11781).rgb * temp_output_13_0_g11781 * tex2DNode30_g11781.a );
				#else
				float3 staticSwitch27_g11781 = temp_output_13_0_g11781;
				#endif
				float4 appendResult21_g11781 = (float4(( (temp_output_1_0_g11781).rgb + ( pow( ( ( break2_g11782.x + break2_g11782.x + break2_g11782.y + break2_g11782.y + break2_g11782.y + break2_g11782.z ) / 6.0 ) , max( _SineGlowContrast , 0.001 ) ) * staticSwitch27_g11781 * _SineGlowFade * ( ( ( sin( ( temp_output_39_0_g11762 * _SineGlowFrequency ) ) + 1.0 ) * ( _SineGlowMax - _SineGlowMin ) ) + _SineGlowMin ) ) ) , temp_output_1_0_g11781.a));
				float4 staticSwitch28_g11762 = appendResult21_g11781;
				#else
				float4 staticSwitch28_g11762 = staticSwitch23_g11762;
				#endif
				#ifdef _ENABLESATURATION_ON
				float4 temp_output_1_0_g11769 = staticSwitch28_g11762;
				float4 break2_g11770 = temp_output_1_0_g11769;
				float3 temp_cast_46 = (( ( break2_g11770.x + break2_g11770.x + break2_g11770.y + break2_g11770.y + break2_g11770.y + break2_g11770.z ) / 6.0 )).xxx;
				float3 lerpResult5_g11769 = lerp( temp_cast_46 , (temp_output_1_0_g11769).rgb , _Saturation);
				float4 appendResult8_g11769 = (float4(lerpResult5_g11769 , temp_output_1_0_g11769.a));
				float4 staticSwitch38_g11762 = appendResult8_g11769;
				#else
				float4 staticSwitch38_g11762 = staticSwitch28_g11762;
				#endif
				#ifdef _ENABLEINNEROUTLINE_ON
				float4 temp_output_15_0_g11771 = staticSwitch38_g11762;
				float3 temp_output_82_0_g11771 = (_InnerOutlineColor).rgb;
				float2 temp_output_7_0_g11771 = temp_output_1_0_g11762;
				float temp_output_179_0_g11771 = temp_output_39_0_g11762;
				#ifdef _INNEROUTLINETEXTURETOGGLE_ON
				float3 staticSwitch187_g11771 = ( (tex2D( _InnerOutlineTintTexture, ( temp_output_7_0_g11771 + ( _InnerOutlineTextureSpeed * temp_output_179_0_g11771 ) ) )).rgb * temp_output_82_0_g11771 );
				#else
				float3 staticSwitch187_g11771 = temp_output_82_0_g11771;
				#endif
				float linValue16_g11773 = tex2D( _UberNoiseTexture, ( ( ( temp_output_179_0_g11771 * _InnerOutlineNoiseSpeed ) + temp_output_7_0_g11771 ) * _InnerOutlineNoiseScale ) ).r;
				float localMyCustomExpression16_g11773 = MyCustomExpression16_g11773( linValue16_g11773 );
				#ifdef _INNEROUTLINEDISTORTIONTOGGLE_ON
				float2 staticSwitch169_g11771 = ( ( localMyCustomExpression16_g11773 - 0.5 ) * _InnerOutlineDistortionIntensity );
				#else
				float2 staticSwitch169_g11771 = float2( 0,0 );
				#endif
				float2 temp_output_131_0_g11771 = ( staticSwitch169_g11771 + temp_output_7_0_g11771 );
				float2 appendResult2_g11772 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_25_0_g11771 = ( 100.0 / appendResult2_g11772 );
				float temp_output_178_0_g11771 = ( _InnerOutlineFade * ( 1.0 - min( min( min( min( min( min( min( tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0,-1 ) ) * temp_output_25_0_g11771 ) ) ).a , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0,1 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -1,0 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 1,0 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0.705,0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -0.705,0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0.705,-0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -0.705,-0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) ) );
				float3 lerpResult176_g11771 = lerp( (temp_output_15_0_g11771).rgb , staticSwitch187_g11771 , temp_output_178_0_g11771);
				#ifdef _INNEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch188_g11771 = ( temp_output_178_0_g11771 * temp_output_15_0_g11771.a );
				#else
				float staticSwitch188_g11771 = temp_output_15_0_g11771.a;
				#endif
				float4 appendResult177_g11771 = (float4(lerpResult176_g11771 , staticSwitch188_g11771));
				float4 staticSwitch12_g11762 = appendResult177_g11771;
				#else
				float4 staticSwitch12_g11762 = staticSwitch38_g11762;
				#endif
				#ifdef _ENABLEOUTEROUTLINE_ON
				float4 temp_output_15_0_g11799 = staticSwitch12_g11762;
				float3 temp_output_82_0_g11799 = (_OuterOutlineColor).rgb;
				float2 temp_output_7_0_g11799 = temp_output_1_0_g11762;
				float temp_output_186_0_g11799 = temp_output_39_0_g11762;
				#ifdef _OUTEROUTLINETEXTURETOGGLE_ON
				float3 staticSwitch199_g11799 = ( (tex2D( _OuterOutlineTintTexture, ( temp_output_7_0_g11799 + ( _OuterOutlineTextureSpeed * temp_output_186_0_g11799 ) ) )).rgb * temp_output_82_0_g11799 );
				#else
				float3 staticSwitch199_g11799 = temp_output_82_0_g11799;
				#endif
				float temp_output_182_0_g11799 = ( ( 1.0 - temp_output_15_0_g11799.a ) * min( ( _OuterOutlineFade * 3.0 ) , 1.0 ) );
				#ifdef _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch203_g11799 = 1.0;
				#else
				float staticSwitch203_g11799 = temp_output_182_0_g11799;
				#endif
				float3 lerpResult178_g11799 = lerp( (temp_output_15_0_g11799).rgb , staticSwitch199_g11799 , staticSwitch203_g11799);
				float3 lerpResult170_g11799 = lerp( lerpResult178_g11799 , staticSwitch199_g11799 , staticSwitch203_g11799);
				float linValue16_g11800 = tex2D( _UberNoiseTexture, ( ( ( temp_output_186_0_g11799 * _OuterOutlineNoiseSpeed ) + temp_output_7_0_g11799 ) * _OuterOutlineNoiseScale ) ).r;
				float localMyCustomExpression16_g11800 = MyCustomExpression16_g11800( linValue16_g11800 );
				#ifdef _OUTEROUTLINEDISTORTIONTOGGLE_ON
				float2 staticSwitch157_g11799 = ( ( localMyCustomExpression16_g11800 - 0.5 ) * _OuterOutlineDistortionIntensity );
				#else
				float2 staticSwitch157_g11799 = float2( 0,0 );
				#endif
				float2 temp_output_131_0_g11799 = ( staticSwitch157_g11799 + temp_output_7_0_g11799 );
				float2 appendResult2_g11801 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_25_0_g11799 = ( 100.0 / appendResult2_g11801 );
				float lerpResult168_g11799 = lerp( temp_output_15_0_g11799.a , min( ( max( max( max( max( max( max( max( tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0,-1 ) ) * temp_output_25_0_g11799 ) ) ).a , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0,1 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -1,0 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 1,0 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0.705,0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -0.705,0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0.705,-0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -0.705,-0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) * 3.0 ) , 1.0 ) , _OuterOutlineFade);
				#ifdef _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch200_g11799 = ( temp_output_182_0_g11799 * lerpResult168_g11799 );
				#else
				float staticSwitch200_g11799 = lerpResult168_g11799;
				#endif
				float4 appendResult174_g11799 = (float4(lerpResult170_g11799 , staticSwitch200_g11799));
				float4 staticSwitch13_g11762 = appendResult174_g11799;
				#else
				float4 staticSwitch13_g11762 = staticSwitch12_g11762;
				#endif
				#ifdef _ENABLEPIXELOUTLINE_ON
				float4 temp_output_15_0_g11776 = staticSwitch13_g11762;
				float3 temp_output_82_0_g11776 = (_PixelOutlineColor).rgb;
				float2 temp_output_7_0_g11776 = temp_output_1_0_g11762;
				#ifdef _PIXELOUTLINETEXTURETOGGLE_ON
				float3 staticSwitch199_g11776 = ( (tex2D( _PixelOutlineTintTexture, ( temp_output_7_0_g11776 + ( _PixelOutlineTextureSpeed * temp_output_39_0_g11762 ) ) )).rgb * temp_output_82_0_g11776 );
				#else
				float3 staticSwitch199_g11776 = temp_output_82_0_g11776;
				#endif
				float temp_output_182_0_g11776 = ( ( 1.0 - temp_output_15_0_g11776.a ) * min( ( _PixelOutlineFade * 3.0 ) , 1.0 ) );
				#ifdef _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch203_g11776 = 1.0;
				#else
				float staticSwitch203_g11776 = temp_output_182_0_g11776;
				#endif
				float3 lerpResult178_g11776 = lerp( (temp_output_15_0_g11776).rgb , staticSwitch199_g11776 , staticSwitch203_g11776);
				float3 lerpResult170_g11776 = lerp( lerpResult178_g11776 , staticSwitch199_g11776 , staticSwitch203_g11776);
				float2 appendResult206_g11776 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_209_0_g11776 = ( float2( 1,1 ) / appendResult206_g11776 );
				float lerpResult168_g11776 = lerp( temp_output_15_0_g11776.a , min( ( max( max( max( tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 0,-1 ) ) * temp_output_209_0_g11776 ) ) ).a , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 0,1 ) ) * temp_output_209_0_g11776 ) ) ).a ) , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( -1,0 ) ) * temp_output_209_0_g11776 ) ) ).a ) , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 1,0 ) ) * temp_output_209_0_g11776 ) ) ).a ) * 3.0 ) , 1.0 ) , _PixelOutlineFade);
				#ifdef _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch200_g11776 = ( temp_output_182_0_g11776 * lerpResult168_g11776 );
				#else
				float staticSwitch200_g11776 = lerpResult168_g11776;
				#endif
				float4 appendResult174_g11776 = (float4(lerpResult170_g11776 , staticSwitch200_g11776));
				float4 staticSwitch48_g11762 = appendResult174_g11776;
				#else
				float4 staticSwitch48_g11762 = staticSwitch13_g11762;
				#endif
				#ifdef _ENABLEPINGPONGGLOW_ON
				float3 lerpResult15_g11763 = lerp( (_PingPongGlowFrom).rgb , (_PingPongGlowTo).rgb , ( ( sin( ( temp_output_39_0_g11762 * _PingPongGlowFrequency ) ) + 1.0 ) / 2.0 ));
				float4 temp_output_5_0_g11763 = staticSwitch48_g11762;
				float4 break2_g11764 = temp_output_5_0_g11763;
				float4 appendResult12_g11763 = (float4(( ( lerpResult15_g11763 * _PingPongGlowFade * pow( ( ( break2_g11764.x + break2_g11764.x + break2_g11764.y + break2_g11764.y + break2_g11764.y + break2_g11764.z ) / 6.0 ) , max( _PingPongGlowContrast , 0.001 ) ) ) + (temp_output_5_0_g11763).rgb ) , temp_output_5_0_g11763.a));
				float4 staticSwitch46_g11762 = appendResult12_g11763;
				#else
				float4 staticSwitch46_g11762 = staticSwitch48_g11762;
				#endif
				float4 temp_output_361_0 = staticSwitch46_g11762;
				#ifdef _ENABLEHOLOGRAM_ON
				float4 temp_output_1_0_g11804 = temp_output_361_0;
				float4 break2_g11805 = temp_output_1_0_g11804;
				float4 appendResult22_g11804 = (float4(( (_HologramTint).rgb * pow( ( ( break2_g11805.x + break2_g11805.x + break2_g11805.y + break2_g11805.y + break2_g11805.y + break2_g11805.z ) / 6.0 ) , max( _HologramContrast , 0.001 ) ) ) , ( max( pow( abs( sin( ( ( ( ( shaderTime237 * _HologramLineSpeed ) + ase_worldPos.y ) / unity_OrthoParams.y ) * _HologramLineFrequency ) ) ) , _HologramLineGap ) , _HologramMinAlpha ) * temp_output_1_0_g11804.a )));
				float4 lerpResult37_g11804 = lerp( temp_output_1_0_g11804 , appendResult22_g11804 , hologramFade182);
				float4 staticSwitch56 = lerpResult37_g11804;
				#else
				float4 staticSwitch56 = temp_output_361_0;
				#endif
				#ifdef _ENABLEGLITCH_ON
				float4 temp_output_1_0_g11807 = staticSwitch56;
				float4 break2_g11809 = temp_output_1_0_g11807;
				float temp_output_34_0_g11807 = shaderTime237;
				float linValue16_g11808 = tex2D( _UberNoiseTexture, ( ( glitchPosition154 + ( _GlitchNoiseSpeed * temp_output_34_0_g11807 ) ) * _GlitchNoiseScale ) ).r;
				float localMyCustomExpression16_g11808 = MyCustomExpression16_g11808( linValue16_g11808 );
				float3 hsvTorgb3_g11810 = HSVToRGB( float3(( localMyCustomExpression16_g11808 + ( temp_output_34_0_g11807 * _GlitchHueSpeed ) ),1.0,1.0) );
				float3 lerpResult23_g11807 = lerp( (temp_output_1_0_g11807).rgb , ( ( ( break2_g11809.x + break2_g11809.x + break2_g11809.y + break2_g11809.y + break2_g11809.y + break2_g11809.z ) / 6.0 ) * _GlitchBrightness * hsvTorgb3_g11810 ) , glitchFade152);
				float4 appendResult27_g11807 = (float4(lerpResult23_g11807 , temp_output_1_0_g11807.a));
				float4 staticSwitch57 = appendResult27_g11807;
				#else
				float4 staticSwitch57 = staticSwitch56;
				#endif
				float4 temp_output_3_0_g11811 = staticSwitch57;
				float4 temp_output_1_0_g11841 = temp_output_3_0_g11811;
				float2 temp_output_41_0_g11811 = shaderPosition235;
				float2 temp_output_99_0_g11841 = temp_output_41_0_g11811;
				float temp_output_40_0_g11811 = shaderTime237;
				#ifdef _ENABLECAMOUFLAGE_ON
				float linValue16_g11846 = tex2D( _UberNoiseTexture, ( ( ( temp_output_40_0_g11811 * _CamouflageDistortionSpeed ) + temp_output_99_0_g11841 ) * _CamouflageDistortionScale ) ).r;
				float localMyCustomExpression16_g11846 = MyCustomExpression16_g11846( linValue16_g11846 );
				#ifdef _CAMOUFLAGEANIMATIONTOGGLE_ON
				float2 staticSwitch101_g11841 = ( ( ( localMyCustomExpression16_g11846 - 0.25 ) * _CamouflageDistortionIntensity ) + temp_output_99_0_g11841 );
				#else
				float2 staticSwitch101_g11841 = temp_output_99_0_g11841;
				#endif
				float linValue16_g11843 = tex2D( _UberNoiseTexture, ( staticSwitch101_g11841 * _CamouflageNoiseScaleA ) ).r;
				float localMyCustomExpression16_g11843 = MyCustomExpression16_g11843( linValue16_g11843 );
				float clampResult52_g11841 = clamp( ( ( _CamouflageDensityA - localMyCustomExpression16_g11843 ) / max( _CamouflageSmoothnessA , 0.005 ) ) , 0.0 , 1.0 );
				float4 lerpResult55_g11841 = lerp( _CamouflageBaseColor , ( _CamouflageColorA * clampResult52_g11841 ) , clampResult52_g11841);
				float linValue16_g11845 = tex2D( _UberNoiseTexture, ( ( staticSwitch101_g11841 + float2( 12.3,12.3 ) ) * _CamouflageNoiseScaleB ) ).r;
				float localMyCustomExpression16_g11845 = MyCustomExpression16_g11845( linValue16_g11845 );
				float clampResult65_g11841 = clamp( ( ( _CamouflageDensityB - localMyCustomExpression16_g11845 ) / max( _CamouflageSmoothnessB , 0.005 ) ) , 0.0 , 1.0 );
				float4 lerpResult68_g11841 = lerp( lerpResult55_g11841 , ( _CamouflageColorB * clampResult65_g11841 ) , clampResult65_g11841);
				float4 break2_g11844 = temp_output_1_0_g11841;
				float3 lerpResult4_g11841 = lerp( (temp_output_1_0_g11841).rgb , ( (lerpResult68_g11841).rgb * pow( ( ( break2_g11844.x + break2_g11844.x + break2_g11844.y + break2_g11844.y + break2_g11844.y + break2_g11844.z ) / 6.0 ) , max( _CamouflageContrast , 0.001 ) ) ) , _CamouflageFade);
				float4 appendResult7_g11841 = (float4(lerpResult4_g11841 , temp_output_1_0_g11841.a));
				float4 staticSwitch26_g11811 = appendResult7_g11841;
				#else
				float4 staticSwitch26_g11811 = temp_output_3_0_g11811;
				#endif
				#ifdef _ENABLEMETAL_ON
				float4 temp_output_1_0_g11834 = staticSwitch26_g11811;
				float temp_output_59_0_g11834 = temp_output_40_0_g11811;
				float2 temp_output_58_0_g11834 = temp_output_41_0_g11811;
				float linValue16_g11835 = tex2D( _UberNoiseTexture, ( ( ( temp_output_59_0_g11834 * _MetalNoiseDistortionSpeed ) + temp_output_58_0_g11834 ) * _MetalNoiseDistortionScale ) ).r;
				float localMyCustomExpression16_g11835 = MyCustomExpression16_g11835( linValue16_g11835 );
				float linValue16_g11837 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11835 - 0.25 ) * _MetalNoiseDistortion ) + ( ( temp_output_59_0_g11834 * _MetalNoiseSpeed ) + temp_output_58_0_g11834 ) ) * _MetalNoiseScale ) ).r;
				float localMyCustomExpression16_g11837 = MyCustomExpression16_g11837( linValue16_g11837 );
				float4 break2_g11836 = temp_output_1_0_g11834;
				float temp_output_5_0_g11834 = ( ( break2_g11836.x + break2_g11836.x + break2_g11836.y + break2_g11836.y + break2_g11836.y + break2_g11836.z ) / 6.0 );
				float2 uv_MetalMask = IN.ase_texcoord.xy * _MetalMask_ST.xy + _MetalMask_ST.zw;
				float4 tex2DNode3_g11839 = tex2D( _MetalMask, uv_MetalMask );
				#ifdef _METALMASKTOGGLE_ON
				float staticSwitch60_g11834 = ( _MetalFade * ( tex2DNode3_g11839.r * tex2DNode3_g11839.a ) );
				#else
				float staticSwitch60_g11834 = _MetalFade;
				#endif
				float4 lerpResult45_g11834 = lerp( temp_output_1_0_g11834 , ( ( max( ( ( _MetalHighlightDensity - localMyCustomExpression16_g11837 ) / max( _MetalHighlightDensity , 0.01 ) ) , 0.0 ) * _MetalHighlightColor * pow( temp_output_5_0_g11834 , max( _MetalHighlightContrast , 0.001 ) ) ) + ( pow( temp_output_5_0_g11834 , max( _MetalContrast , 0.001 ) ) * _MetalColor ) ) , staticSwitch60_g11834);
				float4 appendResult8_g11834 = (float4((lerpResult45_g11834).rgb , (temp_output_1_0_g11834).a));
				float4 staticSwitch28_g11811 = appendResult8_g11834;
				#else
				float4 staticSwitch28_g11811 = staticSwitch26_g11811;
				#endif
				#ifdef _ENABLEFROZEN_ON
				float4 temp_output_1_0_g11826 = staticSwitch28_g11811;
				float4 break2_g11827 = temp_output_1_0_g11826;
				float temp_output_7_0_g11826 = ( ( break2_g11827.x + break2_g11827.x + break2_g11827.y + break2_g11827.y + break2_g11827.y + break2_g11827.z ) / 6.0 );
				float2 temp_output_72_0_g11826 = temp_output_41_0_g11811;
				float linValue16_g11828 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11826 * _FrozenSnowScale ) ).r;
				float localMyCustomExpression16_g11828 = MyCustomExpression16_g11828( linValue16_g11828 );
				float temp_output_73_0_g11826 = temp_output_40_0_g11811;
				float linValue16_g11830 = tex2D( _UberNoiseTexture, ( ( ( temp_output_73_0_g11826 * _FrozenHighlightDistortionSpeed ) + temp_output_72_0_g11826 ) * _FrozenHighlightDistortionScale ) ).r;
				float localMyCustomExpression16_g11830 = MyCustomExpression16_g11830( linValue16_g11830 );
				float linValue16_g11831 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11830 - 0.25 ) * _FrozenHighlightDistortion ) + ( ( temp_output_73_0_g11826 * _FrozenHighlightSpeed ) + temp_output_72_0_g11826 ) ) * _FrozenHighlightScale ) ).r;
				float localMyCustomExpression16_g11831 = MyCustomExpression16_g11831( linValue16_g11831 );
				float3 lerpResult57_g11826 = lerp( (temp_output_1_0_g11826).rgb , ( ( pow( temp_output_7_0_g11826 , max( _FrozenContrast , 0.001 ) ) * (_FrozenTint).rgb ) + ( pow( temp_output_7_0_g11826 , max( _FrozenSnowContrast , 0.001 ) ) * ( (_FrozenSnowColor).rgb * max( ( _FrozenSnowDensity - localMyCustomExpression16_g11828 ) , 0.0 ) ) ) + (( max( ( ( _FrozenHighlightDensity - localMyCustomExpression16_g11831 ) / max( _FrozenHighlightDensity , 0.01 ) ) , 0.0 ) * _FrozenHighlightColor * pow( temp_output_7_0_g11826 , max( _FrozenHighlightContrast , 0.001 ) ) )).rgb ) , _FrozenFade);
				float4 appendResult26_g11826 = (float4(lerpResult57_g11826 , temp_output_1_0_g11826.a));
				float4 staticSwitch29_g11811 = appendResult26_g11826;
				#else
				float4 staticSwitch29_g11811 = staticSwitch28_g11811;
				#endif
				#ifdef _ENABLEBURN_ON
				float4 temp_output_1_0_g11820 = staticSwitch29_g11811;
				float3 temp_output_28_0_g11820 = (temp_output_1_0_g11820).rgb;
				float4 break2_g11824 = float4( temp_output_28_0_g11820 , 0.0 );
				float2 temp_output_72_0_g11820 = temp_output_41_0_g11811;
				float linValue16_g11823 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11820 * _BurnSwirlNoiseScale ) ).r;
				float localMyCustomExpression16_g11823 = MyCustomExpression16_g11823( linValue16_g11823 );
				float linValue16_g11821 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11823 - 0.5 ) * float2( 1,1 ) * _BurnSwirlFactor ) + temp_output_72_0_g11820 ) * _BurnInsideNoiseScale ) ).r;
				float localMyCustomExpression16_g11821 = MyCustomExpression16_g11821( linValue16_g11821 );
				float clampResult68_g11820 = clamp( ( _BurnInsideNoiseFactor - localMyCustomExpression16_g11821 ) , 0.0 , 1.0 );
				float linValue16_g11822 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11820 * _BurnEdgeNoiseScale ) ).r;
				float localMyCustomExpression16_g11822 = MyCustomExpression16_g11822( linValue16_g11822 );
				float temp_output_15_0_g11820 = ( ( ( _BurnRadius - distance( temp_output_72_0_g11820 , _BurnPosition ) ) + ( localMyCustomExpression16_g11822 * _BurnEdgeNoiseFactor ) ) / max( _BurnWidth , 0.01 ) );
				float clampResult18_g11820 = clamp( temp_output_15_0_g11820 , 0.0 , 1.0 );
				float3 lerpResult29_g11820 = lerp( temp_output_28_0_g11820 , ( pow( ( ( break2_g11824.x + break2_g11824.x + break2_g11824.y + break2_g11824.y + break2_g11824.y + break2_g11824.z ) / 6.0 ) , max( _BurnInsideContrast , 0.001 ) ) * ( ( (_BurnInsideNoiseColor).rgb * clampResult68_g11820 ) + (_BurnInsideColor).rgb ) ) , clampResult18_g11820);
				float3 lerpResult40_g11820 = lerp( temp_output_28_0_g11820 , ( lerpResult29_g11820 + ( ( step( temp_output_15_0_g11820 , 1.0 ) * step( 0.0 , temp_output_15_0_g11820 ) ) * (_BurnEdgeColor).rgb ) ) , _BurnFade);
				float4 appendResult43_g11820 = (float4(lerpResult40_g11820 , temp_output_1_0_g11820.a));
				float4 staticSwitch32_g11811 = appendResult43_g11820;
				#else
				float4 staticSwitch32_g11811 = staticSwitch29_g11811;
				#endif
				#ifdef _ENABLERAINBOW_ON
				float2 temp_output_42_0_g11816 = temp_output_41_0_g11811;
				float linValue16_g11817 = tex2D( _UberNoiseTexture, ( temp_output_42_0_g11816 * _RainbowNoiseScale ) ).r;
				float localMyCustomExpression16_g11817 = MyCustomExpression16_g11817( linValue16_g11817 );
				float3 hsvTorgb3_g11819 = HSVToRGB( float3(( ( ( distance( temp_output_42_0_g11816 , _RainbowCenter ) + ( localMyCustomExpression16_g11817 * _RainbowNoiseFactor ) ) * _RainbowDensity ) + ( _RainbowSpeed * temp_output_40_0_g11811 ) ),1.0,1.0) );
				float3 hsvTorgb36_g11816 = RGBToHSV( hsvTorgb3_g11819 );
				float3 hsvTorgb37_g11816 = HSVToRGB( float3(hsvTorgb36_g11816.x,_RainbowSaturation,( hsvTorgb36_g11816.z * _RainbowBrightness )) );
				float4 temp_output_1_0_g11816 = staticSwitch32_g11811;
				float4 break2_g11818 = temp_output_1_0_g11816;
				float saferPower24_g11816 = abs( ( ( break2_g11818.x + break2_g11818.x + break2_g11818.y + break2_g11818.y + break2_g11818.y + break2_g11818.z ) / 6.0 ) );
				float4 appendResult29_g11816 = (float4(( ( hsvTorgb37_g11816 * pow( saferPower24_g11816 , max( _RainbowContrast , 0.001 ) ) * _RainbowFade ) + (temp_output_1_0_g11816).rgb ) , temp_output_1_0_g11816.a));
				float4 staticSwitch34_g11811 = appendResult29_g11816;
				#else
				float4 staticSwitch34_g11811 = staticSwitch32_g11811;
				#endif
				#ifdef _ENABLESHINE_ON
				float4 temp_output_1_0_g11812 = staticSwitch34_g11811;
				float3 temp_output_57_0_g11812 = (temp_output_1_0_g11812).rgb;
				float4 break2_g11814 = temp_output_1_0_g11812;
				float3 temp_cast_69 = (( ( break2_g11814.x + break2_g11814.x + break2_g11814.y + break2_g11814.y + break2_g11814.y + break2_g11814.z ) / 6.0 )).xxx;
				float3 lerpResult92_g11812 = lerp( temp_cast_69 , temp_output_57_0_g11812 , _ShineSaturation);
				float3 temp_cast_70 = (max( max( _ShineContrast , 0.001 ) , 0.001 )).xxx;
				float3 rotatedValue69_g11812 = RotateAroundAxis( float3( 0,0,0 ), float3( ( _ShineFrequency * temp_output_41_0_g11811 ) ,  0.0 ), float3( 0,0,1 ), ( ( _ShineRotation / 180.0 ) * PI ) );
				float temp_output_103_0_g11812 = ( _ShineFrequency * _ShineWidth );
				float clampResult80_g11812 = clamp( ( ( ( sin( ( rotatedValue69_g11812.x - ( temp_output_40_0_g11811 * _ShineSpeed * _ShineFrequency ) ) ) - ( 1.0 - temp_output_103_0_g11812 ) ) / temp_output_103_0_g11812 ) * _ShineSmooth ) , 0.0 , 1.0 );
				float2 uv_ShineMask = IN.ase_texcoord.xy * _ShineMask_ST.xy + _ShineMask_ST.zw;
				float4 tex2DNode3_g11813 = tex2D( _ShineMask, uv_ShineMask );
				#ifdef _SHINEMASKTOGGLE_ON
				float staticSwitch98_g11812 = ( _ShineFade * ( tex2DNode3_g11813.r * tex2DNode3_g11813.a ) );
				#else
				float staticSwitch98_g11812 = _ShineFade;
				#endif
				float4 appendResult8_g11812 = (float4(( temp_output_57_0_g11812 + ( ( pow( lerpResult92_g11812 , temp_cast_70 ) * (_ShineColor).rgb ) * clampResult80_g11812 * staticSwitch98_g11812 ) ) , (temp_output_1_0_g11812).a));
				float4 staticSwitch36_g11811 = appendResult8_g11812;
				#else
				float4 staticSwitch36_g11811 = staticSwitch34_g11811;
				#endif
				#ifdef _ENABLEPOISON_ON
				float temp_output_41_0_g11847 = temp_output_40_0_g11811;
				float linValue16_g11849 = tex2D( _UberNoiseTexture, ( ( ( temp_output_41_0_g11847 * _PoisonNoiseSpeed ) + temp_output_41_0_g11811 ) * _PoisonNoiseScale ) ).r;
				float localMyCustomExpression16_g11849 = MyCustomExpression16_g11849( linValue16_g11849 );
				float3 temp_output_24_0_g11847 = (_PoisonColor).rgb;
				float4 temp_output_1_0_g11847 = staticSwitch36_g11811;
				float3 temp_output_28_0_g11847 = (temp_output_1_0_g11847).rgb;
				float4 break2_g11848 = float4( temp_output_28_0_g11847 , 0.0 );
				float3 lerpResult32_g11847 = lerp( temp_output_28_0_g11847 , ( temp_output_24_0_g11847 * ( ( break2_g11848.x + break2_g11848.x + break2_g11848.y + break2_g11848.y + break2_g11848.y + break2_g11848.z ) / 6.0 ) ) , ( _PoisonFade * _PoisonRecolorFactor ));
				float4 appendResult27_g11847 = (float4(( ( max( pow( abs( ( ( ( localMyCustomExpression16_g11849 + ( temp_output_41_0_g11847 * _PoisonShiftSpeed ) ) % 1.0 ) + -0.5 ) ) , max( _PoisonDensity , 0.001 ) ) , 0.0 ) * temp_output_24_0_g11847 * _PoisonFade * _PoisonNoiseBrightness ) + lerpResult32_g11847 ) , temp_output_1_0_g11847.a));
				float4 staticSwitch39_g11811 = appendResult27_g11847;
				#else
				float4 staticSwitch39_g11811 = staticSwitch36_g11811;
				#endif
				float4 temp_output_10_0_g11851 = staticSwitch39_g11811;
				#ifdef _ENABLEENCHANTED_ON
				float3 temp_output_12_0_g11851 = (temp_output_10_0_g11851).rgb;
				float2 temp_output_2_0_g11851 = temp_output_41_0_g11811;
				float temp_output_1_0_g11851 = temp_output_40_0_g11811;
				float2 temp_output_6_0_g11851 = ( temp_output_1_0_g11851 * _EnchantedSpeed );
				float linValue16_g11854 = tex2D( _UberNoiseTexture, ( ( ( temp_output_2_0_g11851 - ( ( temp_output_6_0_g11851 + float2( 1.234,5.6789 ) ) * float2( 0.95,1.05 ) ) ) * _EnchantedScale ) * float2( 1,1 ) ) ).r;
				float localMyCustomExpression16_g11854 = MyCustomExpression16_g11854( linValue16_g11854 );
				float linValue16_g11852 = tex2D( _UberNoiseTexture, ( ( ( temp_output_6_0_g11851 + temp_output_2_0_g11851 ) * _EnchantedScale ) * float2( 1,1 ) ) ).r;
				float localMyCustomExpression16_g11852 = MyCustomExpression16_g11852( linValue16_g11852 );
				float temp_output_36_0_g11851 = ( localMyCustomExpression16_g11854 + localMyCustomExpression16_g11852 );
				float temp_output_43_0_g11851 = ( temp_output_36_0_g11851 * 0.5 );
				float3 lerpResult42_g11851 = lerp( (_EnchantedLowColor).rgb , (_EnchantedHighColor).rgb , temp_output_43_0_g11851);
				float3 hsvTorgb53_g11851 = HSVToRGB( float3(( ( temp_output_43_0_g11851 * _EnchantedRainbowDensity ) + ( _EnchantedRainbowSpeed * temp_output_1_0_g11851 ) ),_EnchantedRainbowSaturation,1.0) );
				#ifdef _ENCHANTEDRAINBOWTOGGLE_ON
				float3 staticSwitch50_g11851 = hsvTorgb53_g11851;
				#else
				float3 staticSwitch50_g11851 = lerpResult42_g11851;
				#endif
				float4 break2_g11853 = temp_output_10_0_g11851;
				float3 temp_output_40_0_g11851 = ( staticSwitch50_g11851 * pow( ( ( break2_g11853.x + break2_g11853.x + break2_g11853.y + break2_g11853.y + break2_g11853.y + break2_g11853.z ) / 6.0 ) , max( _EnchantedContrast , 0.001 ) ) * _EnchantedBrightness );
				float temp_output_45_0_g11851 = ( max( ( temp_output_36_0_g11851 - _EnchantedReduce ) , 0.0 ) * _EnchantedFade );
				float3 lerpResult44_g11851 = lerp( temp_output_12_0_g11851 , temp_output_40_0_g11851 , temp_output_45_0_g11851);
				#ifdef _ENCHANTEDLERPTOGGLE_ON
				float3 staticSwitch47_g11851 = lerpResult44_g11851;
				#else
				float3 staticSwitch47_g11851 = ( temp_output_12_0_g11851 + ( temp_output_40_0_g11851 * temp_output_45_0_g11851 ) );
				#endif
				float4 appendResult19_g11851 = (float4(staticSwitch47_g11851 , temp_output_10_0_g11851.a));
				float4 staticSwitch11_g11851 = appendResult19_g11851;
				#else
				float4 staticSwitch11_g11851 = temp_output_10_0_g11851;
				#endif
				float4 temp_output_1_0_g11856 = staticSwitch11_g11851;
				#ifdef _ENABLESHIFTING_ON
				float4 break5_g11856 = temp_output_1_0_g11856;
				float3 appendResult32_g11856 = (float3(break5_g11856.r , break5_g11856.g , break5_g11856.b));
				float4 break2_g11857 = temp_output_1_0_g11856;
				float temp_output_4_0_g11856 = ( ( break2_g11857.x + break2_g11857.x + break2_g11857.y + break2_g11857.y + break2_g11857.y + break2_g11857.z ) / 6.0 );
				float temp_output_11_0_g11856 = ( ( ( temp_output_4_0_g11856 + ( temp_output_40_0_g11811 * _ShiftingSpeed ) ) * _ShiftingDensity ) % 1.0 );
				float3 lerpResult20_g11856 = lerp( (_ShiftingColorA).rgb , (_ShiftingColorB).rgb , ( abs( ( temp_output_11_0_g11856 - 0.5 ) ) * 2.0 ));
				float3 hsvTorgb12_g11856 = HSVToRGB( float3(temp_output_11_0_g11856,_ShiftingSaturation,_ShiftingBrightness) );
				#ifdef _SHIFTINGRAINBOWTOGGLE_ON
				float3 staticSwitch26_g11856 = hsvTorgb12_g11856;
				#else
				float3 staticSwitch26_g11856 = ( lerpResult20_g11856 * _ShiftingBrightness );
				#endif
				float3 lerpResult31_g11856 = lerp( appendResult32_g11856 , ( staticSwitch26_g11856 * pow( temp_output_4_0_g11856 , max( _ShiftingContrast , 0.001 ) ) ) , _ShiftingFade);
				float4 appendResult6_g11856 = (float4(lerpResult31_g11856 , break5_g11856.a));
				float4 staticSwitch33_g11856 = appendResult6_g11856;
				#else
				float4 staticSwitch33_g11856 = temp_output_1_0_g11856;
				#endif
				float4 temp_output_5_0_g11863 = staticSwitch33_g11856;
				#ifdef _ENABLETEXTURELAYER1_ON
				float4 break6_g11863 = temp_output_5_0_g11863;
				float3 appendResult11_g11863 = (float3(break6_g11863.r , break6_g11863.g , break6_g11863.b));
				float temp_output_27_0_g11863 = temp_output_40_0_g11811;
				#ifdef _TEXTURELAYER1SCROLLTOGGLE_ON
				float2 staticSwitch73_g11863 = ( _TextureLayer1Offset + ( _TextureLayer1ScrollSpeed * temp_output_27_0_g11863 ) );
				#else
				float2 staticSwitch73_g11863 = _TextureLayer1Offset;
				#endif
				float2 temp_output_72_0_g11863 = ( ( _TextureLayer1Scale * temp_output_41_0_g11811 ) - staticSwitch73_g11863 );
				float2 temp_cast_79 = (_TextureLayer1EdgeClip).xx;
				float2 UV10_g11864 = ( ( ( ( ( ( ( ( ( temp_output_72_0_g11863 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) ) - float2( 0.5,0.5 ) ) * ( float2( 1,1 ) - temp_cast_79 ) ) + float2( 0.5,0.5 ) ) % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				int Width10_g11864 = _TextureLayer1Columns;
				int Height10_g11864 = _TextureLayer1Rows;
				float Tile10_g11864 = round( ( ( _TextureLayer1Speed * temp_output_27_0_g11863 ) + _TextureLayer1StartFrame ) );
				float2 Invert10_g11864 = float2( 0,1 );
				float2 localMyCustomExpression10_g11864 = MyCustomExpression10_g11864( UV10_g11864 , Width10_g11864 , Height10_g11864 , Tile10_g11864 , Invert10_g11864 );
				#ifdef _TEXTURELAYER1SHEETTOGGLE_ON
				float2 staticSwitch20_g11863 = localMyCustomExpression10_g11864;
				#else
				float2 staticSwitch20_g11863 = temp_output_72_0_g11863;
				#endif
				float4 tex2DNode3_g11863 = tex2D( _TextureLayer1Texture, staticSwitch20_g11863 );
				float3 appendResult13_g11863 = (float3(tex2DNode3_g11863.r , tex2DNode3_g11863.g , tex2DNode3_g11863.b));
				float3 appendResult18_g11863 = (float3(_TextureLayer1Color.r , _TextureLayer1Color.g , _TextureLayer1Color.b));
				float3 temp_output_16_0_g11863 = ( appendResult13_g11863 * appendResult18_g11863 );
				float4 break2_g11865 = temp_output_5_0_g11863;
				#ifdef _TEXTURELAYER1CONTRASTTOGGLE_ON
				float3 staticSwitch80_g11863 = ( pow( ( ( break2_g11865.x + break2_g11865.x + break2_g11865.y + break2_g11865.y + break2_g11865.y + break2_g11865.z ) / 6.0 ) , max( _TextureLayer1Contrast , 0.001 ) ) * temp_output_16_0_g11863 );
				#else
				float3 staticSwitch80_g11863 = temp_output_16_0_g11863;
				#endif
				float3 lerpResult12_g11863 = lerp( appendResult11_g11863 , staticSwitch80_g11863 , ( tex2DNode3_g11863.a * _TextureLayer1Fade ));
				float4 appendResult14_g11863 = (float4(lerpResult12_g11863 , break6_g11863.a));
				float4 staticSwitch15_g11863 = appendResult14_g11863;
				#else
				float4 staticSwitch15_g11863 = temp_output_5_0_g11863;
				#endif
				float4 temp_output_5_0_g11859 = staticSwitch15_g11863;
				#ifdef _ENABLETEXTURELAYER2_ON
				float4 break6_g11859 = temp_output_5_0_g11859;
				float3 appendResult11_g11859 = (float3(break6_g11859.r , break6_g11859.g , break6_g11859.b));
				float temp_output_27_0_g11859 = temp_output_40_0_g11811;
				#ifdef _TEXTURELAYER2SCROLLTOGGLE_ON
				float2 staticSwitch73_g11859 = ( _TextureLayer2Offset + ( _TextureLayer2ScrollSpeed * temp_output_27_0_g11859 ) );
				#else
				float2 staticSwitch73_g11859 = _TextureLayer2Offset;
				#endif
				float2 temp_output_72_0_g11859 = ( ( _TextureLayer2Scale * temp_output_41_0_g11811 ) - staticSwitch73_g11859 );
				float2 temp_cast_82 = (_TextureLayer2EdgeClip).xx;
				float2 UV10_g11860 = ( ( ( ( ( ( ( ( ( temp_output_72_0_g11859 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) ) - float2( 0.5,0.5 ) ) * ( float2( 1,1 ) - temp_cast_82 ) ) + float2( 0.5,0.5 ) ) % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				int Width10_g11860 = _TextureLayer2Columns;
				int Height10_g11860 = _TextureLayer2Rows;
				float Tile10_g11860 = round( ( ( _TextureLayer2Speed * temp_output_27_0_g11859 ) + _TextureLayer2StartFrame ) );
				float2 Invert10_g11860 = float2( 0,1 );
				float2 localMyCustomExpression10_g11860 = MyCustomExpression10_g11860( UV10_g11860 , Width10_g11860 , Height10_g11860 , Tile10_g11860 , Invert10_g11860 );
				#ifdef _TEXTURELAYER2SHEETTOGGLE_ON
				float2 staticSwitch20_g11859 = localMyCustomExpression10_g11860;
				#else
				float2 staticSwitch20_g11859 = temp_output_72_0_g11859;
				#endif
				float4 tex2DNode3_g11859 = tex2D( _TextureLayer2Texture, staticSwitch20_g11859 );
				float3 appendResult13_g11859 = (float3(tex2DNode3_g11859.r , tex2DNode3_g11859.g , tex2DNode3_g11859.b));
				float3 appendResult18_g11859 = (float3(_TextureLayer2Color.r , _TextureLayer2Color.g , _TextureLayer2Color.b));
				float3 temp_output_16_0_g11859 = ( appendResult13_g11859 * appendResult18_g11859 );
				float4 break2_g11861 = temp_output_5_0_g11859;
				#ifdef _TEXTURELAYER2CONTRASTTOGGLE_ON
				float3 staticSwitch84_g11859 = ( pow( ( ( break2_g11861.x + break2_g11861.x + break2_g11861.y + break2_g11861.y + break2_g11861.y + break2_g11861.z ) / 6.0 ) , max( _TextureLayer2Contrast , 0.001 ) ) * temp_output_16_0_g11859 );
				#else
				float3 staticSwitch84_g11859 = temp_output_16_0_g11859;
				#endif
				float3 lerpResult12_g11859 = lerp( appendResult11_g11859 , staticSwitch84_g11859 , ( tex2DNode3_g11859.a * _TextureLayer2Fade ));
				float4 appendResult14_g11859 = (float4(lerpResult12_g11859 , break6_g11859.a));
				float4 staticSwitch15_g11859 = appendResult14_g11859;
				#else
				float4 staticSwitch15_g11859 = temp_output_5_0_g11859;
				#endif
				float4 temp_output_473_0 = staticSwitch15_g11859;
				#ifdef _ENABLEFULLDISTORTION_ON
				float4 break4_g11867 = temp_output_473_0;
				float fullDistortionAlpha164 = _FullDistortionFade;
				float4 appendResult5_g11867 = (float4(break4_g11867.r , break4_g11867.g , break4_g11867.b , ( break4_g11867.a * fullDistortionAlpha164 )));
				float4 staticSwitch77 = appendResult5_g11867;
				#else
				float4 staticSwitch77 = temp_output_473_0;
				#endif
				#ifdef _ENABLEDIRECTIONALDISTORTION_ON
				float4 break4_g11868 = staticSwitch77;
				float directionalDistortionAlpha167 = (( _DirectionalDistortionInvert )?( ( 1.0 - clampResult154_g11669 ) ):( clampResult154_g11669 ));
				float4 appendResult5_g11868 = (float4(break4_g11868.r , break4_g11868.g , break4_g11868.b , ( break4_g11868.a * directionalDistortionAlpha167 )));
				float4 staticSwitch75 = appendResult5_g11868;
				#else
				float4 staticSwitch75 = staticSwitch77;
				#endif
				float4 temp_output_1_0_g11869 = staticSwitch75;
				float4 temp_output_1_0_g11870 = temp_output_1_0_g11869;
#ifdef _ENABLEFULLALPHADISSOLVE_ON
				float temp_output_53_0_g11870 = max( _FullAlphaDissolveWidth , 0.001 );
				#endif
				float2 temp_output_18_0_g11869 = shaderPosition235;
				#ifdef _ENABLEFULLALPHADISSOLVE_ON
				float linValue16_g11871 = tex2D( _UberNoiseTexture, ( temp_output_18_0_g11869 * _FullAlphaDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11871 = MyCustomExpression16_g11871( linValue16_g11871 );
				float clampResult17_g11870 = clamp( ( ( ( _FullAlphaDissolveFade * ( 1.0 + temp_output_53_0_g11870 ) ) - localMyCustomExpression16_g11871 ) / temp_output_53_0_g11870 ) , 0.0 , 1.0 );
				float4 appendResult3_g11870 = (float4((temp_output_1_0_g11870).rgb , ( temp_output_1_0_g11870.a * clampResult17_g11870 )));
				float4 staticSwitch3_g11869 = appendResult3_g11870;
				#else
				float4 staticSwitch3_g11869 = temp_output_1_0_g11869;
				#endif
				#ifdef _ENABLEFULLGLOWDISSOLVE_ON
				float linValue16_g11879 = tex2D( _UberNoiseTexture, ( temp_output_18_0_g11869 * _FullGlowDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11879 = MyCustomExpression16_g11879( linValue16_g11879 );
				float temp_output_5_0_g11878 = localMyCustomExpression16_g11879;
				float temp_output_61_0_g11878 = step( temp_output_5_0_g11878 , _FullGlowDissolveFade );
				float temp_output_53_0_g11878 = max( ( _FullGlowDissolveFade * _FullGlowDissolveWidth ) , 0.001 );
				float4 temp_output_1_0_g11878 = staticSwitch3_g11869;
				float4 appendResult3_g11878 = (float4(( ( (_FullGlowDissolveEdgeColor).rgb * ( temp_output_61_0_g11878 - step( temp_output_5_0_g11878 , ( ( _FullGlowDissolveFade * ( 1.01 + temp_output_53_0_g11878 ) ) - temp_output_53_0_g11878 ) ) ) ) + (temp_output_1_0_g11878).rgb ) , ( temp_output_1_0_g11878.a * temp_output_61_0_g11878 )));
				float4 staticSwitch5_g11869 = appendResult3_g11878;
				#else
				float4 staticSwitch5_g11869 = staticSwitch3_g11869;
				#endif
				#ifdef _ENABLESOURCEALPHADISSOLVE_ON
				float4 temp_output_1_0_g11880 = staticSwitch5_g11869;
				float2 temp_output_76_0_g11880 = temp_output_18_0_g11869;
				float linValue16_g11881 = tex2D( _UberNoiseTexture, ( temp_output_76_0_g11880 * _SourceAlphaDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11881 = MyCustomExpression16_g11881( linValue16_g11881 );
				float clampResult17_g11880 = clamp( ( ( _SourceAlphaDissolveFade - ( distance( _SourceAlphaDissolvePosition , temp_output_76_0_g11880 ) + ( localMyCustomExpression16_g11881 * _SourceAlphaDissolveNoiseFactor ) ) ) / max( _SourceAlphaDissolveWidth , 0.001 ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11880 = (float4((temp_output_1_0_g11880).rgb , ( temp_output_1_0_g11880.a * (( _SourceAlphaDissolveInvert )?( ( 1.0 - clampResult17_g11880 ) ):( clampResult17_g11880 )) )));
				float4 staticSwitch8_g11869 = appendResult3_g11880;
				#else
				float4 staticSwitch8_g11869 = staticSwitch5_g11869;
				#endif
				#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
				float2 temp_output_90_0_g11876 = temp_output_18_0_g11869;
				float linValue16_g11877 = tex2D( _UberNoiseTexture, ( temp_output_90_0_g11876 * _SourceGlowDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11877 = MyCustomExpression16_g11877( linValue16_g11877 );
				float temp_output_65_0_g11876 = ( distance( _SourceGlowDissolvePosition , temp_output_90_0_g11876 ) + ( localMyCustomExpression16_g11877 * _SourceGlowDissolveNoiseFactor ) );
				float temp_output_75_0_g11876 = step( temp_output_65_0_g11876 , _SourceGlowDissolveFade );
				float temp_output_76_0_g11876 = step( temp_output_65_0_g11876 , ( _SourceGlowDissolveFade - max( _SourceGlowDissolveWidth , 0.001 ) ) );
				float4 temp_output_1_0_g11876 = staticSwitch8_g11869;
				float4 appendResult3_g11876 = (float4(( ( max( ( temp_output_75_0_g11876 - temp_output_76_0_g11876 ) , 0.0 ) * (_SourceGlowDissolveEdgeColor).rgb ) + (temp_output_1_0_g11876).rgb ) , ( temp_output_1_0_g11876.a * (( _SourceGlowDissolveInvert )?( ( 1.0 - temp_output_76_0_g11876 ) ):( temp_output_75_0_g11876 )) )));
				float4 staticSwitch9_g11869 = appendResult3_g11876;
				#else
				float4 staticSwitch9_g11869 = staticSwitch8_g11869;
				#endif
				#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
				float4 temp_output_1_0_g11872 = staticSwitch9_g11869;
				float2 temp_output_161_0_g11872 = temp_output_18_0_g11869;
				float3 rotatedValue136_g11872 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_161_0_g11872 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalAlphaFadeRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11872 = rotatedValue136_g11872;
				float linValue16_g11873 = tex2D( _UberNoiseTexture, ( temp_output_161_0_g11872 * _DirectionalAlphaFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11873 = MyCustomExpression16_g11873( linValue16_g11873 );
				float clampResult154_g11872 = clamp( ( ( break130_g11872.x + break130_g11872.y + _DirectionalAlphaFadeFade + ( localMyCustomExpression16_g11873 * _DirectionalAlphaFadeNoiseFactor ) ) / max( _DirectionalAlphaFadeWidth , 0.001 ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11872 = (float4((temp_output_1_0_g11872).rgb , ( temp_output_1_0_g11872.a * (( _DirectionalAlphaFadeInvert )?( ( 1.0 - clampResult154_g11872 ) ):( clampResult154_g11872 )) )));
				float4 staticSwitch11_g11869 = appendResult3_g11872;
				#else
				float4 staticSwitch11_g11869 = staticSwitch9_g11869;
				#endif
				#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
				float2 temp_output_171_0_g11874 = temp_output_18_0_g11869;
				float3 rotatedValue136_g11874 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_171_0_g11874 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalGlowFadeRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11874 = rotatedValue136_g11874;
				float linValue16_g11875 = tex2D( _UberNoiseTexture, ( temp_output_171_0_g11874 * _DirectionalGlowFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11875 = MyCustomExpression16_g11875( linValue16_g11875 );
				float temp_output_168_0_g11874 = max( ( ( break130_g11874.x + break130_g11874.y + _DirectionalGlowFadeFade + ( localMyCustomExpression16_g11875 * _DirectionalGlowFadeNoiseFactor ) ) / max( _DirectionalGlowFadeWidth , 0.001 ) ) , 0.0 );
				float temp_output_161_0_g11874 = step( 0.1 , (( _DirectionalGlowFadeInvert )?( ( 1.0 - temp_output_168_0_g11874 ) ):( temp_output_168_0_g11874 )) );
				float4 temp_output_1_0_g11874 = staticSwitch11_g11869;
				float clampResult154_g11874 = clamp( temp_output_161_0_g11874 , 0.0 , 1.0 );
				float4 appendResult3_g11874 = (float4(( ( (_DirectionalGlowFadeEdgeColor).rgb * ( temp_output_161_0_g11874 - step( 1.0 , (( _DirectionalGlowFadeInvert )?( ( 1.0 - temp_output_168_0_g11874 ) ):( temp_output_168_0_g11874 )) ) ) ) + (temp_output_1_0_g11874).rgb ) , ( temp_output_1_0_g11874.a * clampResult154_g11874 )));
				float4 staticSwitch15_g11869 = appendResult3_g11874;
				#else
				float4 staticSwitch15_g11869 = staticSwitch11_g11869;
				#endif
				#ifdef _ENABLEHALFTONE_ON
				float4 temp_output_1_0_g11882 = staticSwitch15_g11869;
				float2 temp_output_126_0_g11882 = temp_output_18_0_g11869;
				float temp_output_121_0_g11882 = max( ( ( _HalftoneFade - distance( _HalftonePosition , temp_output_126_0_g11882 ) ) / max( 0.01 , _HalftoneFadeWidth ) ) , 0.0 );
				float2 appendResult11_g11883 = (float2(temp_output_121_0_g11882 , temp_output_121_0_g11882));
				float temp_output_17_0_g11883 = length( ( (( ( abs( temp_output_126_0_g11882 ) * _HalftoneTiling ) % float2( 1,1 ) )*2.0 + -1.0) / appendResult11_g11883 ) );
				float clampResult17_g11882 = clamp( saturate( ( ( 1.0 - temp_output_17_0_g11883 ) / fwidth( temp_output_17_0_g11883 ) ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11882 = (float4((temp_output_1_0_g11882).rgb , ( temp_output_1_0_g11882.a * (( _HalftoneInvert )?( ( 1.0 - clampResult17_g11882 ) ):( clampResult17_g11882 )) )));
				float4 staticSwitch13_g11869 = appendResult3_g11882;
				#else
				float4 staticSwitch13_g11869 = staticSwitch15_g11869;
				#endif
				#ifdef _ENABLEADDCOLOR_ON
				float3 temp_output_3_0_g11885 = (_AddColorColor).rgb;
				float2 uv_AddColorMask = IN.ase_texcoord.xy * _AddColorMask_ST.xy + _AddColorMask_ST.zw;
				float4 tex2DNode19_g11885 = tex2D( _AddColorMask, uv_AddColorMask );
				#ifdef _ADDCOLORMASKTOGGLE_ON
				float3 staticSwitch16_g11885 = ( temp_output_3_0_g11885 * ( (tex2DNode19_g11885).rgb * tex2DNode19_g11885.a ) );
				#else
				float3 staticSwitch16_g11885 = temp_output_3_0_g11885;
				#endif
				float4 temp_output_1_0_g11885 = staticSwitch13_g11869;
				float4 break2_g11887 = temp_output_1_0_g11885;
				#ifdef _ADDCOLORCONTRASTTOGGLE_ON
				float3 staticSwitch17_g11885 = ( staticSwitch16_g11885 * pow( ( ( break2_g11887.x + break2_g11887.x + break2_g11887.y + break2_g11887.y + break2_g11887.y + break2_g11887.z ) / 6.0 ) , max( _AddColorContrast , 0.001 ) ) );
				#else
				float3 staticSwitch17_g11885 = staticSwitch16_g11885;
				#endif
				float4 appendResult6_g11885 = (float4(( ( staticSwitch17_g11885 * _AddColorFade ) + (temp_output_1_0_g11885).rgb ) , temp_output_1_0_g11885.a));
				float4 staticSwitch5_g11884 = appendResult6_g11885;
				#else
				float4 staticSwitch5_g11884 = staticSwitch13_g11869;
				#endif
				#ifdef _ENABLEALPHATINT_ON
				float4 temp_output_1_0_g11888 = staticSwitch5_g11884;
				float3 lerpResult4_g11888 = lerp( (temp_output_1_0_g11888).rgb , (_AlphaTintColor).rgb , ( ( 1.0 - temp_output_1_0_g11888.a ) * step( _AlphaTintMinAlpha , temp_output_1_0_g11888.a ) * _AlphaTintFade ));
				float4 appendResult13_g11888 = (float4(lerpResult4_g11888 , temp_output_1_0_g11888.a));
				float4 staticSwitch11_g11884 = appendResult13_g11888;
				#else
				float4 staticSwitch11_g11884 = staticSwitch5_g11884;
				#endif
				#ifdef _ENABLESTRONGTINT_ON
				float4 temp_output_1_0_g11889 = staticSwitch11_g11884;
				float3 temp_output_6_0_g11889 = (_StrongTintTint).rgb;
				float2 uv_StrongTintMask = IN.ase_texcoord.xy * _StrongTintMask_ST.xy + _StrongTintMask_ST.zw;
				float4 tex2DNode23_g11889 = tex2D( _StrongTintMask, uv_StrongTintMask );
				#ifdef _STRONGTINTMASKTOGGLE_ON
				float3 staticSwitch21_g11889 = ( temp_output_6_0_g11889 * ( (tex2DNode23_g11889).rgb * tex2DNode23_g11889.a ) );
				#else
				float3 staticSwitch21_g11889 = temp_output_6_0_g11889;
				#endif
				float4 break2_g11891 = temp_output_1_0_g11889;
				#ifdef _STRONGTINTCONTRASTTOGGLE_ON
				float3 staticSwitch22_g11889 = ( pow( ( ( break2_g11891.x + break2_g11891.x + break2_g11891.y + break2_g11891.y + break2_g11891.y + break2_g11891.z ) / 6.0 ) , max( _StrongTintContrast , 0.001 ) ) * staticSwitch21_g11889 );
				#else
				float3 staticSwitch22_g11889 = staticSwitch21_g11889;
				#endif
				float3 lerpResult7_g11889 = lerp( (temp_output_1_0_g11889).rgb , staticSwitch22_g11889 , _StrongTintFade);
				float4 appendResult9_g11889 = (float4(lerpResult7_g11889 , (temp_output_1_0_g11889).a));
				float4 staticSwitch7_g11884 = appendResult9_g11889;
				#else
				float4 staticSwitch7_g11884 = staticSwitch11_g11884;
				#endif
				float4 temp_output_2_0_g11892 = staticSwitch7_g11884;
				#ifdef _ENABLESHADOW_ON
				float4 break4_g11894 = temp_output_2_0_g11892;
				float3 appendResult5_g11894 = (float3(break4_g11894.r , break4_g11894.g , break4_g11894.b));
				float2 appendResult2_g11893 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float4 appendResult85_g11892 = (float4(_ShadowColor.r , _ShadowColor.g , _ShadowColor.b , ( _ShadowFade * tex2D( _MainTex, ( finalUV146 - ( ( 100.0 / appendResult2_g11893 ) * _ShadowOffset ) ) ).a )));
				float4 break6_g11894 = appendResult85_g11892;
				float3 appendResult7_g11894 = (float3(break6_g11894.r , break6_g11894.g , break6_g11894.b));
				float temp_output_11_0_g11894 = ( ( 1.0 - break4_g11894.a ) * break6_g11894.a );
				float temp_output_32_0_g11894 = ( break4_g11894.a + temp_output_11_0_g11894 );
				float4 appendResult18_g11894 = (float4(( ( ( appendResult5_g11894 * break4_g11894.a ) + ( appendResult7_g11894 * temp_output_11_0_g11894 ) ) * ( 1.0 / max( temp_output_32_0_g11894 , 0.01 ) ) ) , temp_output_32_0_g11894));
				float4 staticSwitch82_g11892 = appendResult18_g11894;
				#else
				float4 staticSwitch82_g11892 = temp_output_2_0_g11892;
				#endif
				float4 break4_g11895 = staticSwitch82_g11892;
				#ifdef _ENABLECUSTOMFADE_ON
				float staticSwitch8_g11753 = 1.0;
				#else
				float staticSwitch8_g11753 = IN.ase_color.a;
				#endif
				#ifdef _ENABLESMOKE_ON
				float staticSwitch9_g11753 = 1.0;
				#else
				float staticSwitch9_g11753 = staticSwitch8_g11753;
				#endif
				float customVertexAlpha193 = staticSwitch9_g11753;
				float4 appendResult5_g11895 = (float4(break4_g11895.r , break4_g11895.g , break4_g11895.b , ( break4_g11895.a * customVertexAlpha193 )));
				float4 temp_output_344_0 = appendResult5_g11895;
				float4 temp_output_1_0_g11896 = temp_output_344_0;
				float4 appendResult8_g11896 = (float4(( (temp_output_1_0_g11896).rgb * (IN.ase_color).rgb ) , temp_output_1_0_g11896.a));
				#ifdef _VERTEXTINTFIRST_ON
				float4 staticSwitch342 = temp_output_344_0;
				#else
				float4 staticSwitch342 = appendResult8_g11896;
				#endif
				float4 lerpResult125 = lerp( ( originalColor191 * IN.ase_color ) , staticSwitch342 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float4 staticSwitch143 = staticSwitch342;
				#elif defined(_SHADERFADING_FULL)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_MASK)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_DISSOLVE)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_SPREAD)
				float4 staticSwitch143 = lerpResult125;
				#else
				float4 staticSwitch143 = staticSwitch342;
				#endif
				float4 temp_output_7_0_g11903 = staticSwitch143;
				#ifdef _BAKEDMATERIAL_ON
				float4 appendResult2_g11903 = (float4(( (temp_output_7_0_g11903).rgb / max( temp_output_7_0_g11903.a , 1E-05 ) ) , temp_output_7_0_g11903.a));
				float4 staticSwitch6_g11903 = appendResult2_g11903;
				#else
				float4 staticSwitch6_g11903 = temp_output_7_0_g11903;
				#endif
				
				float4 Color = staticSwitch6_g11903;

				half4 outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				return outColor;
			}

            ENDHLSL
        }

		
        Pass
        {
			
            Name "ScenePickingPass"
            Tags { "LightMode"="Picking" }

			Cull Off

            HLSLPROGRAM

			#define ASE_SRP_VERSION 140007


			#pragma vertex vert
			#pragma fragment frag

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX
            #define SHADERPASS SHADERPASS_DEPTHONLY
			#define SCENEPICKINGPASS 1


            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

        	#define ASE_NEEDS_VERT_POSITION
        	#define ASE_NEEDS_FRAG_POSITION
        	#define ASE_NEEDS_FRAG_COLOR
        	#pragma shader_feature_local _SHADERFADING_NONE _SHADERFADING_FULL _SHADERFADING_MASK _SHADERFADING_DISSOLVE _SHADERFADING_SPREAD
        	#pragma shader_feature_local _ENABLESINESCALE_ON
        	#pragma shader_feature _ENABLEVIBRATE_ON
        	#pragma shader_feature _ENABLESINEMOVE_ON
        	#pragma shader_feature _ENABLESQUISH_ON
        	#pragma shader_feature _SPRITESHEETFIX_ON
        	#pragma shader_feature_local _PIXELPERFECTUV_ON
        	#pragma shader_feature _ENABLEWORLDTILING_ON
        	#pragma shader_feature _ENABLESCREENTILING_ON
        	#pragma shader_feature _TOGGLETIMEFREQUENCY_ON
        	#pragma shader_feature _TOGGLETIMEFPS_ON
        	#pragma shader_feature _TOGGLETIMESPEED_ON
        	#pragma shader_feature _TOGGLEUNSCALEDTIME_ON
        	#pragma shader_feature _TOGGLECUSTOMTIME_ON
        	#pragma shader_feature _SHADERSPACE_UV _SHADERSPACE_UV_RAW _SHADERSPACE_OBJECT _SHADERSPACE_OBJECT_SCALED _SHADERSPACE_WORLD _SHADERSPACE_UI_GRAPHIC _SHADERSPACE_SCREEN
        	#pragma shader_feature _PIXELPERFECTSPACE_ON
        	#pragma shader_feature _BAKEDMATERIAL_ON
        	#pragma shader_feature _VERTEXTINTFIRST_ON
        	#pragma shader_feature _ENABLESHADOW_ON
        	#pragma shader_feature _ENABLESTRONGTINT_ON
        	#pragma shader_feature _ENABLEALPHATINT_ON
        	#pragma shader_feature_local _ENABLEADDCOLOR_ON
        	#pragma shader_feature_local _ENABLEHALFTONE_ON
        	#pragma shader_feature_local _ENABLEDIRECTIONALGLOWFADE_ON
        	#pragma shader_feature_local _ENABLEDIRECTIONALALPHAFADE_ON
        	#pragma shader_feature_local _ENABLESOURCEGLOWDISSOLVE_ON
        	#pragma shader_feature_local _ENABLESOURCEALPHADISSOLVE_ON
        	#pragma shader_feature_local _ENABLEFULLGLOWDISSOLVE_ON
        	#pragma shader_feature_local _ENABLEFULLALPHADISSOLVE_ON
        	#pragma shader_feature_local _ENABLEDIRECTIONALDISTORTION_ON
        	#pragma shader_feature_local _ENABLEFULLDISTORTION_ON
        	#pragma shader_feature _ENABLETEXTURELAYER2_ON
        	#pragma shader_feature _ENABLETEXTURELAYER1_ON
        	#pragma shader_feature _ENABLESHIFTING_ON
        	#pragma shader_feature _ENABLEENCHANTED_ON
        	#pragma shader_feature_local _ENABLEPOISON_ON
        	#pragma shader_feature_local _ENABLESHINE_ON
        	#pragma shader_feature_local _ENABLERAINBOW_ON
        	#pragma shader_feature_local _ENABLEBURN_ON
        	#pragma shader_feature_local _ENABLEFROZEN_ON
        	#pragma shader_feature_local _ENABLEMETAL_ON
        	#pragma shader_feature_local _ENABLECAMOUFLAGE_ON
        	#pragma shader_feature_local _ENABLEGLITCH_ON
        	#pragma shader_feature_local _ENABLEHOLOGRAM_ON
        	#pragma shader_feature _ENABLEPINGPONGGLOW_ON
        	#pragma shader_feature_local _ENABLEPIXELOUTLINE_ON
        	#pragma shader_feature_local _ENABLEOUTEROUTLINE_ON
        	#pragma shader_feature_local _ENABLEINNEROUTLINE_ON
        	#pragma shader_feature_local _ENABLESATURATION_ON
        	#pragma shader_feature_local _ENABLESINEGLOW_ON
        	#pragma shader_feature_local _ENABLEADDHUE_ON
        	#pragma shader_feature_local _ENABLESHIFTHUE_ON
        	#pragma shader_feature_local _ENABLEINKSPREAD_ON
        	#pragma shader_feature_local _ENABLEBLACKTINT_ON
        	#pragma shader_feature_local _ENABLESPLITTONING_ON
        	#pragma shader_feature_local _ENABLEHUE_ON
        	#pragma shader_feature_local _ENABLEBRIGHTNESS_ON
        	#pragma shader_feature_local _ENABLECONTRAST_ON
        	#pragma shader_feature _ENABLENEGATIVE_ON
        	#pragma shader_feature_local _ENABLECOLORREPLACE_ON
        	#pragma shader_feature_local _ENABLERECOLORRGBYCP_ON
        	#pragma shader_feature _ENABLERECOLORRGB_ON
        	#pragma shader_feature_local _ENABLEFLAME_ON
        	#pragma shader_feature_local _ENABLECHECKERBOARD_ON
        	#pragma shader_feature_local _ENABLECUSTOMFADE_ON
        	#pragma shader_feature_local _ENABLESMOKE_ON
        	#pragma shader_feature _ENABLESHARPEN_ON
        	#pragma shader_feature _ENABLEGAUSSIANBLUR_ON
        	#pragma shader_feature _ENABLESMOOTHPIXELART_ON
        	#pragma shader_feature_local _TILINGFIX_ON
        	#pragma shader_feature _ENABLEWIGGLE_ON
        	#pragma shader_feature_local _ENABLEUVSCALE_ON
        	#pragma shader_feature_local _ENABLEPIXELATE_ON
        	#pragma shader_feature_local _ENABLEUVSCROLL_ON
        	#pragma shader_feature_local _ENABLEUVROTATE_ON
        	#pragma shader_feature_local _ENABLESINEROTATE_ON
        	#pragma shader_feature_local _ENABLESQUEEZE_ON
        	#pragma shader_feature_local _ENABLEUVDISTORT_ON
        	#pragma shader_feature_local _ENABLEWIND_ON
        	#pragma shader_feature_local _WINDLOCALWIND_ON
        	#pragma shader_feature_local _WINDHIGHQUALITYNOISE_ON
        	#pragma shader_feature_local _WINDISPARALLAX_ON
        	#pragma shader_feature _UVDISTORTMASKTOGGLE_ON
        	#pragma shader_feature _WIGGLEFIXEDGROUNDTOGGLE_ON
        	#pragma shader_feature _RECOLORRGBTEXTURETOGGLE_ON
        	#pragma shader_feature _RECOLORRGBYCPTEXTURETOGGLE_ON
        	#pragma shader_feature_local _ADDHUEMASKTOGGLE_ON
        	#pragma shader_feature_local _SINEGLOWMASKTOGGLE_ON
        	#pragma shader_feature _INNEROUTLINETEXTURETOGGLE_ON
        	#pragma shader_feature_local _INNEROUTLINEDISTORTIONTOGGLE_ON
        	#pragma shader_feature _INNEROUTLINEOUTLINEONLYTOGGLE_ON
        	#pragma shader_feature _OUTEROUTLINETEXTURETOGGLE_ON
        	#pragma shader_feature _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
        	#pragma shader_feature_local _OUTEROUTLINEDISTORTIONTOGGLE_ON
        	#pragma shader_feature _PIXELOUTLINETEXTURETOGGLE_ON
        	#pragma shader_feature _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
        	#pragma shader_feature _CAMOUFLAGEANIMATIONTOGGLE_ON
        	#pragma shader_feature _METALMASKTOGGLE_ON
        	#pragma shader_feature _SHINEMASKTOGGLE_ON
        	#pragma shader_feature _ENCHANTEDLERPTOGGLE_ON
        	#pragma shader_feature _ENCHANTEDRAINBOWTOGGLE_ON
        	#pragma shader_feature _SHIFTINGRAINBOWTOGGLE_ON
        	#pragma shader_feature _TEXTURELAYER1CONTRASTTOGGLE_ON
        	#pragma shader_feature _TEXTURELAYER1SHEETTOGGLE_ON
        	#pragma shader_feature _TEXTURELAYER1SCROLLTOGGLE_ON
        	#pragma shader_feature _TEXTURELAYER2CONTRASTTOGGLE_ON
        	#pragma shader_feature _TEXTURELAYER2SHEETTOGGLE_ON
        	#pragma shader_feature _TEXTURELAYER2SCROLLTOGGLE_ON
        	#pragma shader_feature _ADDCOLORCONTRASTTOGGLE_ON
        	#pragma shader_feature _ADDCOLORMASKTOGGLE_ON
        	#pragma shader_feature _STRONGTINTCONTRASTTOGGLE_ON
        	#pragma shader_feature _STRONGTINTMASKTOGGLE_ON


			sampler2D _MainTex;
			#ifdef _TOGGLEUNSCALEDTIME_ON
			float UnscaledTime;
			#endif
			#ifdef _SHADERFADING_MASK
			sampler2D _FadingMask;
			#endif
			sampler2D _UberNoiseTexture;
			#ifdef _ENABLEWIND_ON
			float WindMinIntensity;
			float WindMaxIntensity;
			float WindNoiseScale;
			float WindTime;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			sampler2D _UVDistortMask;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			sampler2D _CustomFadeFadeMask;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			sampler2D _RecolorRGBTexture;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			sampler2D _RecolorRGBYCPTexture;
			#endif
			#ifdef _ENABLEADDHUE_ON
			sampler2D _AddHueMask;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			sampler2D _SineGlowMask;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			sampler2D _InnerOutlineTintTexture;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			sampler2D _OuterOutlineTintTexture;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			sampler2D _PixelOutlineTintTexture;
			#endif
			#ifdef _ENABLEMETAL_ON
			sampler2D _MetalMask;
			#endif
			#ifdef _ENABLESHINE_ON
			sampler2D _ShineMask;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			sampler2D _TextureLayer1Texture;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			sampler2D _TextureLayer2Texture;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			sampler2D _AddColorMask;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			sampler2D _StrongTintMask;
			#endif
			CBUFFER_START( UnityPerMaterial )
			#ifdef _ENABLESHIFTING_ON
			float4 _ShiftingColorA;
			#endif
			#ifdef _ENABLEPOISON_ON
			float4 _PoisonColor;
			#endif
			#ifdef _ENABLESPLITTONING_ON
			float4 _SplitToningHighlightsColor;
			float4 _SplitToningShadowsColor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float4 _EnchantedLowColor;
			float4 _EnchantedHighColor;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float4 _ColorReplaceToColor;
			float4 _ColorReplaceFromColor;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			float4 _RecolorRGBYCPRedTint;
			float4 _RecolorRGBYCPYellowTint;
			float4 _RecolorRGBYCPGreenTint;
			float4 _RecolorRGBYCPCyanTint;
			float4 _RecolorRGBYCPBlueTint;
			float4 _RecolorRGBYCPPurpleTint;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			float4 _RecolorRGBBlueTint;
			float4 _RecolorRGBGreenTint;
			float4 _RecolorRGBRedTint;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float4 _ShiftingColorB;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float4 _TextureLayer1Color;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float4 _TextureLayer2Color;
			#endif
			#ifdef _ENABLEBLACKTINT_ON
			float4 _BlackTintColor;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float4 _InkSpreadColor;
			#endif
			#ifdef _ENABLESHINE_ON
			float4 _ShineMask_ST;
			float4 _ShineColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenTint;
			#endif
			#ifdef _ENABLEMETAL_ON
			float4 _MetalMask_ST;
			float4 _MetalColor;
			float4 _MetalHighlightColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenHighlightColor;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float4 _CamouflageColorB;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnInsideNoiseColor;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float4 _CamouflageColorA;
			float4 _CamouflageBaseColor;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float4 _UVDistortMask_ST;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnInsideColor;
			#endif
			#ifdef _ENABLEPINGPONGGLOW_ON
			float4 _PingPongGlowTo;
			float4 _PingPongGlowFrom;
			#endif
			#ifdef _ENABLEBURN_ON
			float4 _BurnEdgeColor;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float4 _PixelOutlineColor;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float4 _OuterOutlineColor;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float4 _InnerOutlineColor;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			float4 _SineGlowMask_ST;
			float4 _SineGlowColor;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float4 _AddHueMask_ST;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float4 _HologramTint;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float4 _FullGlowDissolveEdgeColor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float4 _FrozenSnowColor;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float4 _StrongTintTint;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float4 _AddColorMask_ST;
			float4 _AddColorColor;
			#endif
			#ifdef _ENABLESHADOW_ON
			float4 _ShadowColor;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float4 _SourceGlowDissolveEdgeColor;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float4 _DirectionalGlowFadeEdgeColor;
			#endif
			#ifdef _SPRITESHEETFIX_ON
			float4 _SpriteSheetRect;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float4 _AlphaTintColor;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float4 _StrongTintMask_ST;
			#endif
			#ifdef _SHADERFADING_MASK
			float4 _FadingMask_ST;
			#endif
			float4 _MainTex_TexelSize;
			float2 _FadingNoiseScale;
			#ifdef _SHADERFADING_SPREAD
			float2 _FadingPosition;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnEdgeNoiseScale;
			float2 _BurnPosition;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float2 _DirectionalAlphaFadeNoiseScale;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnSwirlNoiseScale;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenSnowScale;
			#endif
			#ifdef _ENABLEFLAME_ON
			float2 _FlameNoiseScale;
			float2 _FlameSpeed;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float2 _GlitchNoiseSpeed;
			float2 _GlitchNoiseScale;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			float2 _CustomFadeNoiseScale;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float2 _TextureLayer1Offset;
			float2 _TextureLayer1ScrollSpeed;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageDistortionSpeed;
			float2 _CamouflageDistortionScale;
			#endif
			#ifdef _ENABLEBURN_ON
			float2 _BurnInsideNoiseScale;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float2 _TextureLayer1Scale;
			#endif
			#ifdef _ENABLESHADOW_ON
			float2 _ShadowOffset;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageDistortionIntensity;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float2 _SineMoveFrequency;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float2 _InnerOutlineTextureSpeed;
			float2 _InnerOutlineNoiseSpeed;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float2 _InkSpreadNoiseScale;
			float2 _InkSpreadPosition;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float2 _InnerOutlineNoiseScale;
			float2 _InnerOutlineDistortionIntensity;
			#endif
			#ifdef _ENABLEPOISON_ON
			float2 _PoisonNoiseSpeed;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float2 _HalftonePosition;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float2 _DirectionalGlowFadeNoiseScale;
			#endif
			#ifdef _ENABLEPOISON_ON
			float2 _PoisonNoiseScale;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float2 _OuterOutlineTextureSpeed;
			#endif
			#ifdef _ENABLESINESCALE_ON
			float2 _SineScaleFactor;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float2 _RainbowCenter;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float2 _OuterOutlineNoiseSpeed;
			float2 _OuterOutlineNoiseScale;
			float2 _OuterOutlineDistortionIntensity;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float2 _EnchantedSpeed;
			float2 _EnchantedScale;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float2 _PixelOutlineTextureSpeed;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float2 _RainbowNoiseScale;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageNoiseScaleA;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float2 _SineMoveOffset;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float2 _WorldTilingOffset;
			#endif
			#ifdef _ENABLEFULLALPHADISSOLVE_ON
			float2 _FullAlphaDissolveNoiseScale;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float2 _UVDistortNoiseScale;
			float2 _UVDistortTo;
			float2 _UVDistortFrom;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float2 _FullGlowDissolveNoiseScale;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float2 _GlitchMaskScale;
			float2 _GlitchMaskSpeed;
			float2 _GlitchDistortion;
			float2 _GlitchDistortionScale;
			float2 _GlitchDistortionSpeed;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortion;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float2 _SqueezeCenter;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortionScale;
			#endif
			#ifdef _ENABLESOURCEALPHADISSOLVE_ON
			float2 _SourceAlphaDissolvePosition;
			float2 _SourceAlphaDissolveNoiseScale;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float2 _ScreenTilingOffset;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionNoiseScale;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float2 _SourceGlowDissolvePosition;
			float2 _SourceGlowDissolveNoiseScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionDistortion;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float2 _ScreenTilingScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float2 _DirectionalDistortionDistortionScale;
			#endif
			#ifdef _ENABLEFULLDISTORTION_ON
			float2 _FullDistortionDistortion;
			float2 _FullDistortionNoiseScale;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightDistortionSpeed;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseScale;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float2 _UVDistortSpeed;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float2 _SqueezeScale;
			#endif
			#ifdef _ENABLEUVSCALE_ON
			float2 _UVScaleScale;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float2 _WorldTilingScale;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float2 _CamouflageNoiseScaleB;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightScale;
			#endif
			#ifdef _ENABLEUVSCROLL_ON
			float2 _UVScrollSpeed;
			#endif
			#ifdef _ENABLEUVROTATE_ON
			float2 _UVRotatePivot;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2ScrollSpeed;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float2 _FrozenHighlightSpeed;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2Offset;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float2 _SineRotatePivot;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseDistortionSpeed;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float2 _TextureLayer2Scale;
			#endif
			#ifdef _ENABLEMETAL_ON
			float2 _MetalNoiseDistortionScale;
			float2 _MetalNoiseDistortion;
			float2 _MetalNoiseSpeed;
			#endif
			#ifdef _ENABLEUVSCALE_ON
			float2 _UVScalePivot;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenHighlightDensity;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowSpeed;
			float _RainbowSaturation;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenFade;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowNoiseFactor;
			float _RainbowContrast;
			float _RainbowBrightness;
			float _RainbowDensity;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float _AddColorFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnInsideNoiseFactor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenHighlightContrast;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnEdgeNoiseFactor;
			#endif
			#ifdef _ENABLESHADOW_ON
			float _ShadowFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnSwirlFactor;
			float _BurnFade;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float _StrongTintContrast;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnRadius;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float _AlphaTintMinAlpha;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnInsideContrast;
			#endif
			#ifdef _ENABLEALPHATINT_ON
			float _AlphaTintFade;
			#endif
			#ifdef _ENABLEBURN_ON
			float _BurnWidth;
			#endif
			#ifdef _ENABLESTRONGTINT_ON
			float _StrongTintFade;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveFade;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonRecolorFactor;
			#endif
			#ifdef _ENABLERAINBOW_ON
			float _RainbowFade;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingBrightness;
			float _ShiftingSaturation;
			float _ShiftingContrast;
			float _ShiftingFade;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1EdgeClip;
			#endif
			int _TextureLayer1Columns;
			int _TextureLayer1Rows;
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1Speed;
			#endif
			int _TextureLayer1StartFrame;
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeRotation;
			#endif
			#ifdef _ENABLETEXTURELAYER1_ON
			float _TextureLayer1Contrast;
			float _TextureLayer1Fade;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2EdgeClip;
			#endif
			int _TextureLayer2Columns;
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingDensity;
			#endif
			int _TextureLayer2Rows;
			int _TextureLayer2StartFrame;
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeInvert;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2Contrast;
			float _TextureLayer2Fade;
			#endif
			#ifdef _ENABLEFULLALPHADISSOLVE_ON
			float _FullAlphaDissolveFade;
			float _FullAlphaDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveInvert;
			#endif
			#ifdef _ENABLEFULLGLOWDISSOLVE_ON
			float _FullGlowDissolveFade;
			float _FullGlowDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEALPHADISSOLVE_ON
			float _SourceAlphaDissolveInvert;
			float _SourceAlphaDissolveFade;
			float _SourceAlphaDissolveNoiseFactor;
			float _SourceAlphaDissolveWidth;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveNoiseFactor;
			#endif
			#ifdef _ENABLETEXTURELAYER2_ON
			float _TextureLayer2Speed;
			#endif
			#ifdef _ENABLEADDCOLOR_ON
			float _AddColorContrast;
			#endif
			#ifdef _ENABLESHIFTING_ON
			float _ShiftingSpeed;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedFade;
			#endif
			#ifdef _ENABLESHINE_ON
			float _ShineSaturation;
			float _ShineContrast;
			float _ShineRotation;
			float _ShineFrequency;
			float _ShineSpeed;
			float _ShineWidth;
			float _ShineSmooth;
			float _ShineFade;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneFadeWidth;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonShiftSpeed;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneFade;
			#endif
			#ifdef _ENABLEPOISON_ON
			float _PoisonDensity;
			float _PoisonFade;
			float _PoisonNoiseBrightness;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeFade;
			#endif
			#ifdef _ENABLEHALFTONE_ON
			float _HalftoneTiling;
			float _HalftoneInvert;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeWidth;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedRainbowDensity;
			float _EnchantedRainbowSpeed;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeNoiseFactor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedRainbowSaturation;
			float _EnchantedContrast;
			#endif
			#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
			float _DirectionalGlowFadeFade;
			float _DirectionalGlowFadeRotation;
			float _DirectionalGlowFadeInvert;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedBrightness;
			#endif
			#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
			float _DirectionalAlphaFadeWidth;
			float _DirectionalAlphaFadeNoiseFactor;
			#endif
			#ifdef _ENABLEENCHANTED_ON
			float _EnchantedReduce;
			#endif
			#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
			float _SourceGlowDissolveWidth;
			#endif
			#ifdef _ENABLESQUISH_ON
			float _SquishStretch;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float _PixelOutlineWidth;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenSnowContrast;
			#endif
			#ifdef _ENABLESQUEEZE_ON
			float _SqueezeFade;
			float _SqueezePower;
			#endif
			#ifdef _ENABLEUVDISTORT_ON
			float _UVDistortFade;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float _GlitchFade;
			float _GlitchMaskMin;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramFade;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateFrequency;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramDistortionOffset;
			float _HologramDistortionDensity;
			float _HologramDistortionSpeed;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionWidth;
			float _DirectionalDistortionNoiseFactor;
			float _DirectionalDistortionFade;
			float _DirectionalDistortionRotation;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramDistortionScale;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionInvert;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateAngle;
			#endif
			#ifdef _ENABLEUVROTATE_ON
			float _UVRotateSpeed;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeNoiseScale;
			float _SmokeVertexSeed;
			#endif
			#ifdef _ENABLESHARPEN_ON
			float _SharpenFade;
			float _SharpenFactor;
			float _SharpenOffset;
			#endif
			#ifdef _ENABLEGAUSSIANBLUR_ON
			float _GaussianBlurFade;
			#endif
			#ifdef _ENABLESINEROTATE_ON
			float _SineRotateFade;
			#endif
			#ifdef _ENABLEGAUSSIANBLUR_ON
			float _GaussianBlurOffset;
			#endif
			#ifdef _ENABLEWIGGLE_ON
			float _WiggleOffset;
			float _WiggleSpeed;
			float _WiggleFrequency;
			#endif
			#ifdef _ENABLEPIXELATE_ON
			float _PixelateFade;
			float _PixelatePixelsPerUnit;
			float _PixelatePixelDensity;
			#endif
			#ifdef _ENABLEWIGGLE_ON
			float _WiggleFade;
			#endif
			#ifdef _ENABLEDIRECTIONALDISTORTION_ON
			float _DirectionalDistortionRandomDirection;
			#endif
			#ifdef _ENABLEFULLDISTORTION_ON
			float _FullDistortionFade;
			#endif
			#ifdef _ENABLEWIND_ON
			float _WindSquishWindFactor;
			#endif
			#ifdef _ENABLEVIBRATE_ON
			float _VibrateFade;
			float _VibrateOffset;
			float _VibrateFrequency;
			#endif
			#ifdef _ENABLESINEMOVE_ON
			float _SineMoveFade;
			#endif
			#ifdef _TOGGLETIMEFREQUENCY_ON
			float _TimeRange;
			float _TimeFrequency;
			#endif
			#ifdef _ENABLEVIBRATE_ON
			float _VibrateRotation;
			#endif
			#ifdef _TOGGLETIMEFPS_ON
			float _TimeFPS;
			#endif
			#ifdef _TOGGLECUSTOMTIME_ON
			float _TimeValue;
			#endif
			#ifdef _ENABLESQUISH_ON
			float _SquishSquish;
			float _SquishFlip;
			float _SquishFade;
			#endif
			#ifdef _ENABLEWORLDTILING_ON
			float _WorldTilingPixelsPerUnit;
			#endif
			#ifdef _ENABLESCREENTILING_ON
			float _ScreenTilingPixelsPerUnit;
			#endif
			#ifdef _TOGGLETIMESPEED_ON
			float _TimeSpeed;
			#endif
			#ifdef _ENABLESINESCALE_ON
			float _SineScaleFrequency;
			#endif
			float _FadingFade;
			float _FadingWidth;
			#ifdef _ENABLEWIND_ON
			float _WindSquishFactor;
			float _WindFlip;
			float _WindMaxRotation;
			float _WindRotation;
			float _WindNoiseSpeed;
			float _WindNoiseScale;
			float _WindXPosition;
			float _WindMaxIntensity;
			float _WindMinIntensity;
			float _WindRotationWindFactor;
			#endif
			#ifdef _SHADERFADING_SPREAD
			float _FadingNoiseFactor;
			#endif
			#ifdef _SHADERSPACE_SCREEN
			float _ScreenWidthUnits;
			#endif
			#ifdef _SHADERSPACE_UI_GRAPHIC
			float _RectHeight;
			float _RectWidth;
			#endif
			float _PixelsPerUnit;
			#ifdef _ENABLESMOKE_ON
			float _SmokeNoiseFactor;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenSnowDensity;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeSmoothness;
			float _SmokeAlpha;
			#endif
			#ifdef _ENABLEPINGPONGGLOW_ON
			float _PingPongGlowContrast;
			float _PingPongGlowFade;
			float _PingPongGlowFrequency;
			#endif
			#ifdef _ENABLEPIXELOUTLINE_ON
			float _PixelOutlineFade;
			#endif
			#ifdef _ENABLEOUTEROUTLINE_ON
			float _OuterOutlineWidth;
			float _OuterOutlineFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramContrast;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float _InnerOutlineWidth;
			#endif
			#ifdef _ENABLESATURATION_ON
			float _Saturation;
			#endif
			#ifdef _ENABLESINEGLOW_ON
			float _SineGlowMin;
			float _SineGlowMax;
			float _SineGlowFrequency;
			float _SineGlowFade;
			float _SineGlowContrast;
			#endif
			#ifdef _ENABLEINNEROUTLINE_ON
			float _InnerOutlineFade;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramLineSpeed;
			float _HologramLineGap;
			#endif
			#ifdef _ENABLEFROZEN_ON
			float _FrozenContrast;
			#endif
			#ifdef _ENABLEMETAL_ON
			float _MetalFade;
			float _MetalContrast;
			float _MetalHighlightContrast;
			float _MetalHighlightDensity;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageFade;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramLineFrequency;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageContrast;
			float _CamouflageDensityB;
			float _CamouflageSmoothnessA;
			float _CamouflageDensityA;
			#endif
			#ifdef _ENABLEGLITCH_ON
			float _GlitchHueSpeed;
			float _GlitchBrightness;
			#endif
			#ifdef _ENABLEHOLOGRAM_ON
			float _HologramMinAlpha;
			#endif
			#ifdef _ENABLECAMOUFLAGE_ON
			float _CamouflageSmoothnessB;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueContrast;
			float _AddHueBrightness;
			float _AddHueSaturation;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceSmoothness;
			float _ColorReplaceContrast;
			#endif
			#ifdef _ENABLERECOLORRGBYCP_ON
			float _RecolorRGBYCPFade;
			#endif
			#ifdef _ENABLERECOLORRGB_ON
			float _RecolorRGBFade;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameBrightness;
			float _FlameSmooth;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceRange;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameRadius;
			float _FlameNoiseHeightFactor;
			#endif
			#ifdef _ENABLECHECKERBOARD_ON
			float _CheckerboardTiling;
			float _CheckerboardDarken;
			#endif
			#ifdef _ENABLECUSTOMFADE_ON
			float _CustomFadeAlpha;
			float _CustomFadeSmoothness;
			float _CustomFadeNoiseFactor;
			#endif
			#ifdef _ENABLEFLAME_ON
			float _FlameNoiseFactor;
			#endif
			#ifdef _ENABLECOLORREPLACE_ON
			float _ColorReplaceFade;
			#endif
			#ifdef _ENABLENEGATIVE_ON
			float _NegativeFade;
			#endif
			#ifdef _ENABLECONTRAST_ON
			float _Contrast;
			#endif
			#ifdef _ENABLEADDHUE_ON
			float _AddHueSpeed;
			#endif
			#ifdef _ENABLESHIFTHUE_ON
			float _ShiftHueSpeed;
			#endif
			#ifdef _ENABLEINKSPREAD_ON
			float _InkSpreadWidth;
			float _InkSpreadNoiseFactor;
			float _InkSpreadDistance;
			float _InkSpreadFade;
			float _InkSpreadContrast;
			#endif
			#ifdef _ENABLEBLACKTINT_ON
			float _BlackTintFade;
			float _BlackTintPower;
			#endif
			#ifdef _ENABLESPLITTONING_ON
			float _SplitToningFade;
			float _SplitToningContrast;
			float _SplitToningBalance;
			float _SplitToningShift;
			#endif
			#ifdef _ENABLEHUE_ON
			float _Hue;
			#endif
			#ifdef _ENABLEBRIGHTNESS_ON
			float _Brightness;
			#endif
			#ifdef _ENABLESMOKE_ON
			float _SmokeDarkEdge;
			#endif
			float _NormalIntensity;
			CBUFFER_END


            struct VertexInput
			{
				float3 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

            float4 _SelectionID;

			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			
			float MyCustomExpression16_g11716( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11714( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float FastNoise101_g11665( float x )
			{
				float i = floor(x);
				float f = frac(x);
				float s = sign(frac(x/2.0)-0.5);
				    
				float k = 0.5+0.5*sin(i);
				return s*f*(f-1.0)*((16.0*k-4.0)*f*(f-1.0)-1.0);
			}
			
			float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }
			float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }
			float snoise( float2 v )
			{
				const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
				float2 i = floor( v + dot( v, C.yy ) );
				float2 x0 = v - i + dot( i, C.xx );
				float2 i1;
				i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
				float4 x12 = x0.xyxy + C.xxzz;
				x12.xy -= i1;
				i = mod2D289( i );
				float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
				float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
				m = m * m;
				m = m * m;
				float3 x = 2.0 * frac( p * C.www ) - 1.0;
				float3 h = abs( x ) - 0.5;
				float3 ox = floor( x + 0.5 );
				float3 a0 = x - ox;
				m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
				float3 g;
				g.x = a0.x * x0.x + h.x * x0.y;
				g.yz = a0.yz * x12.xz + h.yz * x12.yw;
				return 130.0 * dot( m, g );
			}
			
			float MyCustomExpression16_g11667( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11668( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11671( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11670( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11676( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11677( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11718( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11673( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11729( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float4 texturePointSmooth( sampler2D tex, float4 textureTexelSize, float2 uvs )
			{
				float2 size;
				size.x = textureTexelSize.z;
				size.y = textureTexelSize.w;
				float2 pixel = float2(1.0,1.0) / size;
				uvs -= pixel * float2(0.5,0.5);
				float2 uv_pixels = uvs * size;
				float2 delta_pixel = frac(uv_pixels) - float2(0.5,0.5);
				float2 ddxy = fwidth(uv_pixels);
				float2 mip = log2(ddxy) - 0.5;
				float2 clampedUV = uvs + (clamp(delta_pixel / ddxy, 0.0, 1.0) - delta_pixel) * pixel;
				return tex2Dlod(tex, float4(clampedUV,0, min(mip.x, mip.y)));
			}
			
			float MyCustomExpression16_g11755( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11757( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11761( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float3 RGBToHSV(float3 c)
			{
				float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
				float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
				float d = q.x - min( q.w, q.y );
				float e = 1.0e-10;
				return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
			}
			float3 MyCustomExpression115_g11766( float3 In, float3 From, float3 To, float Fuzziness, float Range )
			{
				float Distance = distance(From, In);
				return lerp(To, In, saturate((Distance - Range) / max(Fuzziness, 0.001)));
			}
			
			float3 HSVToRGB( float3 c )
			{
				float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
				float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
				return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
			}
			
			float MyCustomExpression16_g11789( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11773( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11800( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11808( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11846( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11843( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11845( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11835( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11837( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11828( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11830( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11831( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11823( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11821( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11822( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11817( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11849( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11854( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11852( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float2 MyCustomExpression10_g11864( float2 UV, int Width, int Height, float Tile, float2 Invert )
			{
				Tile = fmod(Tile + 0.001, Width * Height);
				float2 tileCount = float2(1, 1) / float2(Width, Height);
				float tileY = abs(Invert.y * Height - (floor(Tile * tileCount.x) + Invert.y * 1));
				 float tileX = abs(Invert.x * Width - ((Tile - Width * floor(Tile * tileCount.x)) + Invert.x * 1));
				return (UV + float2(tileX, tileY)) * tileCount;
			}
			
			float2 MyCustomExpression10_g11860( float2 UV, int Width, int Height, float Tile, float2 Invert )
			{
				Tile = fmod(Tile + 0.001, Width * Height);
				float2 tileCount = float2(1, 1) / float2(Width, Height);
				float tileY = abs(Invert.y * Height - (floor(Tile * tileCount.x) + Invert.y * 1));
				 float tileX = abs(Invert.x * Width - ((Tile - Width * floor(Tile * tileCount.x)) + Invert.x * 1));
				return (UV + float2(tileX, tileY)) * tileCount;
			}
			
			float MyCustomExpression16_g11871( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11879( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11881( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11877( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11873( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			
			float MyCustomExpression16_g11875( float linValue )
			{
				#ifdef UNITY_COLORSPACE_GAMMA
				return linValue;
				#else
				linValue = max(linValue, half3(0.h, 0.h, 0.h));
				return max(1.055h * pow(linValue, 0.416666667h) - 0.055h, 0.h);
				#endif
			}
			

			VertexOutput vert(VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				float2 _ZeroVector = float2(0,0);
				float2 texCoord363 = v.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				#ifdef _ENABLESCREENTILING_ON
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult16_g11656 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11656 = ( ( ( (( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / 10.0 ) )).xy * _ScreenTilingScale ) + _ScreenTilingOffset ) * ( _ScreenTilingPixelsPerUnit * appendResult16_g11656 ) );
				#else
				float2 staticSwitch2_g11656 = texCoord363;
				#endif
				float3 ase_worldPos = TransformObjectToWorld( (v.vertex).xyz );
				#ifdef _ENABLEWORLDTILING_ON
				float2 appendResult16_g11657 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11657 = ( ( ( (ase_worldPos).xy * _WorldTilingScale ) + _WorldTilingOffset ) * ( _WorldTilingPixelsPerUnit * appendResult16_g11657 ) );
				#else
				float2 staticSwitch2_g11657 = staticSwitch2_g11656;
				#endif
				float2 originalUV460 = staticSwitch2_g11657;
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11658 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch449 = ( floor( ( originalUV460 * appendResult7_g11658 ) ) / appendResult7_g11658 );
				#else
				float2 staticSwitch449 = originalUV460;
				#endif
				float2 uvAfterPixelArt450 = staticSwitch449;
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11662 = uvAfterPixelArt450;
				float2 appendResult374 = (float2(_SpriteSheetRect.x , _SpriteSheetRect.y));
				float2 spriteRectMin376 = appendResult374;
				float2 break11_g11662 = spriteRectMin376;
				float2 appendResult375 = (float2(_SpriteSheetRect.z , _SpriteSheetRect.w));
				float2 spriteRectMax377 = appendResult375;
				float2 break10_g11662 = spriteRectMax377;
				float2 break9_g11662 = float2( 0,0 );
				float2 break8_g11662 = float2( 1,1 );
				float2 appendResult15_g11662 = (float2((break9_g11662.x + (break14_g11662.x - break11_g11662.x) * (break8_g11662.x - break9_g11662.x) / (break10_g11662.x - break11_g11662.x)) , (break9_g11662.y + (break14_g11662.y - break11_g11662.y) * (break8_g11662.y - break9_g11662.y) / (break10_g11662.y - break11_g11662.y))));
				float2 staticSwitch366 = appendResult15_g11662;
				#else
				float2 staticSwitch366 = uvAfterPixelArt450;
				#endif
				float2 fixedUV475 = staticSwitch366;
				#ifdef _ENABLESQUISH_ON
				float2 break77_g11897 = fixedUV475;
				float2 appendResult72_g11897 = (float2(( _SquishStretch * ( break77_g11897.x - 0.5 ) * _SquishFade ) , ( _SquishFade * ( break77_g11897.y + _SquishFlip ) * -_SquishSquish )));
				float2 staticSwitch198 = ( appendResult72_g11897 + _ZeroVector );
				#else
				float2 staticSwitch198 = _ZeroVector;
				#endif
				float2 temp_output_2_0_g11899 = staticSwitch198;
				#ifdef _TOGGLECUSTOMTIME_ON
				float staticSwitch44_g11661 = _TimeValue;
				#else
				float staticSwitch44_g11661 = _TimeParameters.x;
				#endif
				#ifdef _TOGGLEUNSCALEDTIME_ON
				float staticSwitch34_g11661 = UnscaledTime;
				#else
				float staticSwitch34_g11661 = staticSwitch44_g11661;
				#endif
				#ifdef _TOGGLETIMESPEED_ON
				float staticSwitch37_g11661 = ( staticSwitch34_g11661 * _TimeSpeed );
				#else
				float staticSwitch37_g11661 = staticSwitch34_g11661;
				#endif
				#ifdef _TOGGLETIMEFPS_ON
				float staticSwitch38_g11661 = ( floor( ( staticSwitch37_g11661 * _TimeFPS ) ) / _TimeFPS );
				#else
				float staticSwitch38_g11661 = staticSwitch37_g11661;
				#endif
				#ifdef _TOGGLETIMEFREQUENCY_ON
				float staticSwitch42_g11661 = ( ( sin( ( staticSwitch38_g11661 * _TimeFrequency ) ) * _TimeRange ) + 100.0 );
				#else
				float staticSwitch42_g11661 = staticSwitch38_g11661;
				#endif
				float shaderTime237 = staticSwitch42_g11661;
				float temp_output_8_0_g11899 = shaderTime237;
				#ifdef _ENABLESINEMOVE_ON
				float2 staticSwitch4_g11899 = ( ( sin( ( temp_output_8_0_g11899 * _SineMoveFrequency ) ) * _SineMoveOffset * _SineMoveFade ) + temp_output_2_0_g11899 );
				#else
				float2 staticSwitch4_g11899 = temp_output_2_0_g11899;
				#endif
				#ifdef _ENABLEVIBRATE_ON
				float temp_output_30_0_g11900 = temp_output_8_0_g11899;
				float3 rotatedValue21_g11900 = RotateAroundAxis( float3( 0,0,0 ), float3( 0,1,0 ), float3( 0,0,1 ), ( temp_output_30_0_g11900 * _VibrateRotation ) );
				float2 staticSwitch6_g11899 = ( ( sin( ( _VibrateFrequency * temp_output_30_0_g11900 ) ) * _VibrateOffset * _VibrateFade * (rotatedValue21_g11900).xy ) + staticSwitch4_g11899 );
				#else
				float2 staticSwitch6_g11899 = staticSwitch4_g11899;
				#endif
				#ifdef _ENABLESINESCALE_ON
				float2 staticSwitch10_g11899 = ( staticSwitch6_g11899 + ( (v.vertex).xy * ( ( ( sin( ( _SineScaleFrequency * temp_output_8_0_g11899 ) ) + 1.0 ) * 0.5 ) * _SineScaleFactor ) ) );
				#else
				float2 staticSwitch10_g11899 = staticSwitch6_g11899;
				#endif
				float2 temp_output_424_0 = staticSwitch10_g11899;
#ifdef _SHADERFADING_MASK
				float2 uv_FadingMask = v.ase_texcoord.xy * _FadingMask_ST.xy + _FadingMask_ST.zw;
				#endif
#ifdef _SHADERFADING_MASK
				float4 tex2DNode3_g11712 = tex2Dlod( _FadingMask, float4( uv_FadingMask, 0, 0.0) );
				#endif
				float temp_output_4_0_g11715 = max( _FadingWidth , 0.001 );
				float2 texCoord435 = v.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				#ifdef _PIXELPERFECTSPACE_ON
				float2 temp_output_432_0 = (_MainTex_TexelSize).zw;
				float2 staticSwitch437 = ( floor( ( texCoord435 * temp_output_432_0 ) ) / temp_output_432_0 );
				#else
				float2 staticSwitch437 = texCoord435;
				#endif
				float2 temp_output_61_0_g11663 = staticSwitch437;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 texCoord23_g11663 = v.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERSPACE_UI_GRAPHIC
				float2 appendResult28_g11663 = (float2(_RectWidth , _RectHeight));
				#endif
				#if defined(_SHADERSPACE_UV)
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#elif defined(_SHADERSPACE_UV_RAW)
				float2 staticSwitch1_g11663 = temp_output_61_0_g11663;
				#elif defined(_SHADERSPACE_OBJECT)
				float2 staticSwitch1_g11663 = (v.vertex).xy;
				#elif defined(_SHADERSPACE_OBJECT_SCALED)
				float2 staticSwitch1_g11663 = ( (v.vertex).xy * (ase_objectScale).xy );
				#elif defined(_SHADERSPACE_WORLD)
				float2 staticSwitch1_g11663 = (ase_worldPos).xy;
				#elif defined(_SHADERSPACE_UI_GRAPHIC)
				float2 staticSwitch1_g11663 = ( texCoord23_g11663 * ( appendResult28_g11663 / _PixelsPerUnit ) );
				#elif defined(_SHADERSPACE_SCREEN)
				float2 staticSwitch1_g11663 = ( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / _ScreenWidthUnits ) );
				#else
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#endif
				float2 shaderPosition235 = staticSwitch1_g11663;
				float linValue16_g11716 = tex2Dlod( _UberNoiseTexture, float4( ( shaderPosition235 * _FadingNoiseScale ), 0, 0.0) ).r;
				float localMyCustomExpression16_g11716 = MyCustomExpression16_g11716( linValue16_g11716 );
				float clampResult14_g11715 = clamp( ( ( ( _FadingFade * ( 1.0 + temp_output_4_0_g11715 ) ) - localMyCustomExpression16_g11716 ) / temp_output_4_0_g11715 ) , 0.0 , 1.0 );
				float2 temp_output_27_0_g11713 = shaderPosition235;
				float linValue16_g11714 = tex2Dlod( _UberNoiseTexture, float4( ( temp_output_27_0_g11713 * _FadingNoiseScale ), 0, 0.0) ).r;
				float localMyCustomExpression16_g11714 = MyCustomExpression16_g11714( linValue16_g11714 );
#ifdef _SHADERFADING_SPREAD
				float clampResult3_g11713 = clamp( ( ( _FadingFade - ( distance( _FadingPosition , temp_output_27_0_g11713 ) + ( localMyCustomExpression16_g11714 * _FadingNoiseFactor ) ) ) / max( _FadingWidth , 0.001 ) ) , 0.0 , 1.0 );
				#endif
				#if defined(_SHADERFADING_NONE)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_FULL)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_MASK)
				float staticSwitch139 = ( _FadingFade * ( tex2DNode3_g11712.r * tex2DNode3_g11712.a ) );
				#elif defined(_SHADERFADING_DISSOLVE)
				float staticSwitch139 = clampResult14_g11715;
				#elif defined(_SHADERFADING_SPREAD)
				float staticSwitch139 = clampResult3_g11713;
				#else
				float staticSwitch139 = _FadingFade;
				#endif
				float fullFade123 = staticSwitch139;
				float2 lerpResult121 = lerp( float2( 0,0 ) , temp_output_424_0 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float2 staticSwitch142 = temp_output_424_0;
				#elif defined(_SHADERFADING_FULL)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_MASK)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_DISSOLVE)
				float2 staticSwitch142 = lerpResult121;
				#elif defined(_SHADERFADING_SPREAD)
				float2 staticSwitch142 = lerpResult121;
				#else
				float2 staticSwitch142 = temp_output_424_0;
				#endif
				
				o.ase_texcoord1 = screenPos;
				o.ase_texcoord2.xyz = ase_worldPos;
				
				o.ase_texcoord.xy = v.ase_texcoord.xy;
				o.ase_texcoord3 = float4(v.vertex,1);
				o.ase_color = v.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord.zw = 0;
				o.ase_texcoord2.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = float3( staticSwitch142 ,  0.0 );
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
				float3 positionWS = TransformObjectToWorld(v.vertex);
				o.clipPos = TransformWorldToHClip(positionWS);

				return o;
			}

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				float2 texCoord363 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float4 screenPos = IN.ase_texcoord1;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				#ifdef _ENABLESCREENTILING_ON
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 appendResult16_g11656 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11656 = ( ( ( (( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / 10.0 ) )).xy * _ScreenTilingScale ) + _ScreenTilingOffset ) * ( _ScreenTilingPixelsPerUnit * appendResult16_g11656 ) );
				#else
				float2 staticSwitch2_g11656 = texCoord363;
				#endif
				float3 ase_worldPos = IN.ase_texcoord2.xyz;
				#ifdef _ENABLEWORLDTILING_ON
				float2 appendResult16_g11657 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 staticSwitch2_g11657 = ( ( ( (ase_worldPos).xy * _WorldTilingScale ) + _WorldTilingOffset ) * ( _WorldTilingPixelsPerUnit * appendResult16_g11657 ) );
				#else
				float2 staticSwitch2_g11657 = staticSwitch2_g11656;
				#endif
				float2 originalUV460 = staticSwitch2_g11657;
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11658 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch449 = ( floor( ( originalUV460 * appendResult7_g11658 ) ) / appendResult7_g11658 );
				#else
				float2 staticSwitch449 = originalUV460;
				#endif
				float2 uvAfterPixelArt450 = staticSwitch449;
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11662 = uvAfterPixelArt450;
				float2 appendResult374 = (float2(_SpriteSheetRect.x , _SpriteSheetRect.y));
				float2 spriteRectMin376 = appendResult374;
				float2 break11_g11662 = spriteRectMin376;
				float2 appendResult375 = (float2(_SpriteSheetRect.z , _SpriteSheetRect.w));
				float2 spriteRectMax377 = appendResult375;
				float2 break10_g11662 = spriteRectMax377;
				float2 break9_g11662 = float2( 0,0 );
				float2 break8_g11662 = float2( 1,1 );
				float2 appendResult15_g11662 = (float2((break9_g11662.x + (break14_g11662.x - break11_g11662.x) * (break8_g11662.x - break9_g11662.x) / (break10_g11662.x - break11_g11662.x)) , (break9_g11662.y + (break14_g11662.y - break11_g11662.y) * (break8_g11662.y - break9_g11662.y) / (break10_g11662.y - break11_g11662.y))));
				float2 staticSwitch366 = appendResult15_g11662;
				#else
				float2 staticSwitch366 = uvAfterPixelArt450;
				#endif
				float2 fixedUV475 = staticSwitch366;
				float2 temp_output_3_0_g11664 = fixedUV475;
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch117_g11665 = _WindMinIntensity;
				#else
				float staticSwitch117_g11665 = WindMinIntensity;
				#endif
				#endif
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch118_g11665 = _WindMaxIntensity;
				#else
				float staticSwitch118_g11665 = WindMaxIntensity;
				#endif
				#endif
				float4 transform62_g11665 = mul(GetWorldToObjectMatrix(),float4( 0,0,0,1 ));
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDISPARALLAX_ON
				float staticSwitch111_g11665 = _WindXPosition;
				#else
				float staticSwitch111_g11665 = transform62_g11665.x;
				#endif
				#endif
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch113_g11665 = _WindNoiseScale;
				#else
				float staticSwitch113_g11665 = WindNoiseScale;
				#endif
				#endif
				#ifdef _TOGGLECUSTOMTIME_ON
				float staticSwitch44_g11661 = _TimeValue;
				#else
				float staticSwitch44_g11661 = _TimeParameters.x;
				#endif
				#ifdef _TOGGLEUNSCALEDTIME_ON
				float staticSwitch34_g11661 = UnscaledTime;
				#else
				float staticSwitch34_g11661 = staticSwitch44_g11661;
				#endif
				#ifdef _TOGGLETIMESPEED_ON
				float staticSwitch37_g11661 = ( staticSwitch34_g11661 * _TimeSpeed );
				#else
				float staticSwitch37_g11661 = staticSwitch34_g11661;
				#endif
				#ifdef _TOGGLETIMEFPS_ON
				float staticSwitch38_g11661 = ( floor( ( staticSwitch37_g11661 * _TimeFPS ) ) / _TimeFPS );
				#else
				float staticSwitch38_g11661 = staticSwitch37_g11661;
				#endif
				#ifdef _TOGGLETIMEFREQUENCY_ON
				float staticSwitch42_g11661 = ( ( sin( ( staticSwitch38_g11661 * _TimeFrequency ) ) * _TimeRange ) + 100.0 );
				#else
				float staticSwitch42_g11661 = staticSwitch38_g11661;
				#endif
				float shaderTime237 = staticSwitch42_g11661;
				#ifdef _ENABLEWIND_ON
				#ifdef _WINDLOCALWIND_ON
				float staticSwitch125_g11665 = ( shaderTime237 * _WindNoiseSpeed );
				#else
				float staticSwitch125_g11665 = WindTime;
				#endif
				float temp_output_50_0_g11665 = ( ( staticSwitch111_g11665 * staticSwitch113_g11665 ) + staticSwitch125_g11665 );
				float x101_g11665 = temp_output_50_0_g11665;
				float localFastNoise101_g11665 = FastNoise101_g11665( x101_g11665 );
				float2 temp_cast_0 = (temp_output_50_0_g11665).xx;
				float simplePerlin2D121_g11665 = snoise( temp_cast_0*0.5 );
				simplePerlin2D121_g11665 = simplePerlin2D121_g11665*0.5 + 0.5;
				#ifdef _WINDHIGHQUALITYNOISE_ON
				float staticSwitch123_g11665 = simplePerlin2D121_g11665;
				#else
				float staticSwitch123_g11665 = ( localFastNoise101_g11665 + 0.5 );
				#endif
				float lerpResult86_g11665 = lerp( staticSwitch117_g11665 , staticSwitch118_g11665 , staticSwitch123_g11665);
				float clampResult29_g11665 = clamp( ( ( _WindRotationWindFactor * lerpResult86_g11665 ) + _WindRotation ) , -_WindMaxRotation , _WindMaxRotation );
				float2 temp_output_1_0_g11665 = temp_output_3_0_g11664;
				float temp_output_39_0_g11665 = ( temp_output_1_0_g11665.y + _WindFlip );
				float3 appendResult43_g11665 = (float3(0.5 , -_WindFlip , 0.0));
				float2 appendResult27_g11665 = (float2(0.0 , ( _WindSquishFactor * min( ( ( _WindSquishWindFactor * abs( lerpResult86_g11665 ) ) + abs( _WindRotation ) ) , _WindMaxRotation ) * temp_output_39_0_g11665 )));
				float3 rotatedValue19_g11665 = RotateAroundAxis( appendResult43_g11665, float3( ( appendResult27_g11665 + temp_output_1_0_g11665 ) ,  0.0 ), float3( 0,0,1 ), ( clampResult29_g11665 * temp_output_39_0_g11665 ) );
				float2 staticSwitch4_g11664 = (rotatedValue19_g11665).xy;
				#else
				float2 staticSwitch4_g11664 = temp_output_3_0_g11664;
				#endif
				float2 texCoord435 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				#ifdef _PIXELPERFECTSPACE_ON
				float2 temp_output_432_0 = (_MainTex_TexelSize).zw;
				float2 staticSwitch437 = ( floor( ( texCoord435 * temp_output_432_0 ) ) / temp_output_432_0 );
				#else
				float2 staticSwitch437 = texCoord435;
				#endif
				float2 temp_output_61_0_g11663 = staticSwitch437;
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float2 texCoord23_g11663 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERSPACE_UI_GRAPHIC
				float2 appendResult28_g11663 = (float2(_RectWidth , _RectHeight));
				#endif
				#if defined(_SHADERSPACE_UV)
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#elif defined(_SHADERSPACE_UV_RAW)
				float2 staticSwitch1_g11663 = temp_output_61_0_g11663;
				#elif defined(_SHADERSPACE_OBJECT)
				float2 staticSwitch1_g11663 = (IN.ase_texcoord3.xyz).xy;
				#elif defined(_SHADERSPACE_OBJECT_SCALED)
				float2 staticSwitch1_g11663 = ( (IN.ase_texcoord3.xyz).xy * (ase_objectScale).xy );
				#elif defined(_SHADERSPACE_WORLD)
				float2 staticSwitch1_g11663 = (ase_worldPos).xy;
				#elif defined(_SHADERSPACE_UI_GRAPHIC)
				float2 staticSwitch1_g11663 = ( texCoord23_g11663 * ( appendResult28_g11663 / _PixelsPerUnit ) );
				#elif defined(_SHADERSPACE_SCREEN)
				float2 staticSwitch1_g11663 = ( ( (ase_screenPosNorm).xy * (_ScreenParams).xy ) / ( _ScreenParams.x / _ScreenWidthUnits ) );
				#else
				float2 staticSwitch1_g11663 = ( temp_output_61_0_g11663 / ( _PixelsPerUnit * (_MainTex_TexelSize).xy ) );
				#endif
				float2 shaderPosition235 = staticSwitch1_g11663;
				#ifdef _ENABLEFULLDISTORTION_ON
				float2 temp_output_195_0_g11666 = shaderPosition235;
				float linValue16_g11667 = tex2D( _UberNoiseTexture, ( temp_output_195_0_g11666 * _FullDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11667 = MyCustomExpression16_g11667( linValue16_g11667 );
				float linValue16_g11668 = tex2D( _UberNoiseTexture, ( ( temp_output_195_0_g11666 + float2( 0.321,0.321 ) ) * _FullDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11668 = MyCustomExpression16_g11668( linValue16_g11668 );
				float2 appendResult189_g11666 = (float2(( localMyCustomExpression16_g11667 - 0.5 ) , ( localMyCustomExpression16_g11668 - 0.5 )));
				float2 staticSwitch83 = ( staticSwitch4_g11664 + ( ( 1.0 - _FullDistortionFade ) * appendResult189_g11666 * _FullDistortionDistortion ) );
				#else
				float2 staticSwitch83 = staticSwitch4_g11664;
				#endif
				#ifdef _ENABLEDIRECTIONALDISTORTION_ON
				float2 temp_output_182_0_g11669 = shaderPosition235;
				float linValue16_g11671 = tex2D( _UberNoiseTexture, ( temp_output_182_0_g11669 * _DirectionalDistortionDistortionScale ) ).r;
				float localMyCustomExpression16_g11671 = MyCustomExpression16_g11671( linValue16_g11671 );
				float3 rotatedValue168_g11669 = RotateAroundAxis( float3( 0,0,0 ), float3( _DirectionalDistortionDistortion ,  0.0 ), float3( 0,0,1 ), ( ( ( localMyCustomExpression16_g11671 - 0.5 ) * 2.0 * _DirectionalDistortionRandomDirection ) * PI ) );
				float3 rotatedValue136_g11669 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_182_0_g11669 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalDistortionRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11669 = rotatedValue136_g11669;
				float linValue16_g11670 = tex2D( _UberNoiseTexture, ( temp_output_182_0_g11669 * _DirectionalDistortionNoiseScale ) ).r;
				float localMyCustomExpression16_g11670 = MyCustomExpression16_g11670( linValue16_g11670 );
				float clampResult154_g11669 = clamp( ( ( break130_g11669.x + break130_g11669.y + _DirectionalDistortionFade + ( localMyCustomExpression16_g11670 * _DirectionalDistortionNoiseFactor ) ) / max( _DirectionalDistortionWidth , 0.001 ) ) , 0.0 , 1.0 );
				float2 staticSwitch82 = ( staticSwitch83 + ( (rotatedValue168_g11669).xy * ( 1.0 - (( _DirectionalDistortionInvert )?( ( 1.0 - clampResult154_g11669 ) ):( clampResult154_g11669 )) ) ) );
				#else
				float2 staticSwitch82 = staticSwitch83;
				#endif
				#ifdef _ENABLEHOLOGRAM_ON
				float temp_output_8_0_g11674 = ( ( ( shaderTime237 * _HologramDistortionSpeed ) + ase_worldPos.y ) / unity_OrthoParams.y );
				float2 temp_cast_4 = (temp_output_8_0_g11674).xx;
				float2 temp_cast_5 = (_HologramDistortionDensity).xx;
				float linValue16_g11676 = tex2D( _UberNoiseTexture, ( temp_cast_4 * temp_cast_5 ) ).r;
				float localMyCustomExpression16_g11676 = MyCustomExpression16_g11676( linValue16_g11676 );
				float clampResult75_g11674 = clamp( localMyCustomExpression16_g11676 , 0.075 , 0.6 );
				float2 temp_cast_6 = (temp_output_8_0_g11674).xx;
				float2 temp_cast_7 = (_HologramDistortionScale).xx;
				float linValue16_g11677 = tex2D( _UberNoiseTexture, ( temp_cast_6 * temp_cast_7 ) ).r;
				float localMyCustomExpression16_g11677 = MyCustomExpression16_g11677( linValue16_g11677 );
				float2 appendResult2_g11675 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float hologramFade182 = _HologramFade;
				float2 appendResult44_g11674 = (float2(( ( ( clampResult75_g11674 * ( localMyCustomExpression16_g11677 - 0.5 ) ) * _HologramDistortionOffset * ( 100.0 / appendResult2_g11675 ).x ) * hologramFade182 ) , 0.0));
				float2 staticSwitch59 = ( staticSwitch82 + appendResult44_g11674 );
				#else
				float2 staticSwitch59 = staticSwitch82;
				#endif
				#ifdef _ENABLEGLITCH_ON
				float2 temp_output_18_0_g11672 = shaderPosition235;
				float2 glitchPosition154 = temp_output_18_0_g11672;
				float linValue16_g11718 = tex2D( _UberNoiseTexture, ( ( glitchPosition154 + ( _GlitchDistortionSpeed * shaderTime237 ) ) * _GlitchDistortionScale ) ).r;
				float localMyCustomExpression16_g11718 = MyCustomExpression16_g11718( linValue16_g11718 );
				float linValue16_g11673 = tex2D( _UberNoiseTexture, ( ( temp_output_18_0_g11672 + ( _GlitchMaskSpeed * shaderTime237 ) ) * _GlitchMaskScale ) ).r;
				float localMyCustomExpression16_g11673 = MyCustomExpression16_g11673( linValue16_g11673 );
				float glitchFade152 = ( max( localMyCustomExpression16_g11673 , _GlitchMaskMin ) * _GlitchFade );
				float2 staticSwitch62 = ( staticSwitch59 + ( ( localMyCustomExpression16_g11718 - 0.5 ) * _GlitchDistortion * glitchFade152 ) );
				#else
				float2 staticSwitch62 = staticSwitch59;
				#endif
				float2 temp_output_1_0_g11719 = staticSwitch62;
				float2 temp_output_26_0_g11719 = shaderPosition235;
				float temp_output_25_0_g11719 = shaderTime237;
				#ifdef _ENABLEUVDISTORT_ON
				float linValue16_g11729 = tex2D( _UberNoiseTexture, ( ( temp_output_26_0_g11719 + ( _UVDistortSpeed * temp_output_25_0_g11719 ) ) * _UVDistortNoiseScale ) ).r;
				float localMyCustomExpression16_g11729 = MyCustomExpression16_g11729( linValue16_g11729 );
				float2 lerpResult21_g11726 = lerp( _UVDistortFrom , _UVDistortTo , localMyCustomExpression16_g11729);
				float2 appendResult2_g11728 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 uv_UVDistortMask = IN.ase_texcoord.xy * _UVDistortMask_ST.xy + _UVDistortMask_ST.zw;
				float4 tex2DNode3_g11727 = tex2D( _UVDistortMask, uv_UVDistortMask );
				#ifdef _UVDISTORTMASKTOGGLE_ON
				float staticSwitch29_g11726 = ( _UVDistortFade * ( tex2DNode3_g11727.r * tex2DNode3_g11727.a ) );
				#else
				float staticSwitch29_g11726 = _UVDistortFade;
				#endif
				float2 staticSwitch5_g11719 = ( temp_output_1_0_g11719 + ( lerpResult21_g11726 * ( 100.0 / appendResult2_g11728 ) * staticSwitch29_g11726 ) );
				#else
				float2 staticSwitch5_g11719 = temp_output_1_0_g11719;
				#endif
				#ifdef _ENABLESQUEEZE_ON
				float2 temp_output_1_0_g11725 = staticSwitch5_g11719;
				float2 staticSwitch7_g11719 = ( temp_output_1_0_g11725 + ( ( temp_output_1_0_g11725 - _SqueezeCenter ) * pow( distance( temp_output_1_0_g11725 , _SqueezeCenter ) , _SqueezePower ) * _SqueezeScale * _SqueezeFade ) );
				#else
				float2 staticSwitch7_g11719 = staticSwitch5_g11719;
				#endif
				#ifdef _ENABLESINEROTATE_ON
				float3 rotatedValue36_g11724 = RotateAroundAxis( float3( _SineRotatePivot ,  0.0 ), float3( staticSwitch7_g11719 ,  0.0 ), float3( 0,0,1 ), ( sin( ( temp_output_25_0_g11719 * _SineRotateFrequency ) ) * ( ( _SineRotateAngle / 360.0 ) * PI ) * _SineRotateFade ) );
				float2 staticSwitch9_g11719 = (rotatedValue36_g11724).xy;
				#else
				float2 staticSwitch9_g11719 = staticSwitch7_g11719;
				#endif
				#ifdef _ENABLEUVROTATE_ON
				float3 rotatedValue8_g11723 = RotateAroundAxis( float3( _UVRotatePivot ,  0.0 ), float3( staticSwitch9_g11719 ,  0.0 ), float3( 0,0,1 ), ( temp_output_25_0_g11719 * _UVRotateSpeed * PI ) );
				float2 staticSwitch16_g11719 = (rotatedValue8_g11723).xy;
				#else
				float2 staticSwitch16_g11719 = staticSwitch9_g11719;
				#endif
				#ifdef _ENABLEUVSCROLL_ON
				float2 staticSwitch14_g11719 = ( ( _UVScrollSpeed * temp_output_25_0_g11719 ) + staticSwitch16_g11719 );
				#else
				float2 staticSwitch14_g11719 = staticSwitch16_g11719;
				#endif
				#ifdef _ENABLEPIXELATE_ON
				float2 appendResult35_g11721 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 MultFactor30_g11721 = ( ( _PixelatePixelDensity * ( appendResult35_g11721 / _PixelatePixelsPerUnit ) ) * ( 1.0 / max( _PixelateFade , 1E-05 ) ) );
				float2 clampResult46_g11721 = clamp( ( floor( ( MultFactor30_g11721 * ( staticSwitch14_g11719 + ( float2( 0.5,0.5 ) / MultFactor30_g11721 ) ) ) ) / MultFactor30_g11721 ) , float2( 0,0 ) , float2( 1,1 ) );
				float2 staticSwitch4_g11719 = clampResult46_g11721;
				#else
				float2 staticSwitch4_g11719 = staticSwitch14_g11719;
				#endif
				#ifdef _ENABLEUVSCALE_ON
				float2 staticSwitch24_g11719 = ( ( ( staticSwitch4_g11719 - _UVScalePivot ) / _UVScaleScale ) + _UVScalePivot );
				#else
				float2 staticSwitch24_g11719 = staticSwitch4_g11719;
				#endif
				float2 temp_output_1_0_g11730 = staticSwitch24_g11719;
				#ifdef _ENABLEWIGGLE_ON
				float temp_output_7_0_g11730 = ( sin( ( _WiggleFrequency * ( temp_output_26_0_g11719.y + ( _WiggleSpeed * temp_output_25_0_g11719 ) ) ) ) * _WiggleOffset * _WiggleFade );
				#ifdef _WIGGLEFIXEDGROUNDTOGGLE_ON
				float staticSwitch18_g11730 = ( temp_output_7_0_g11730 * temp_output_1_0_g11730.y );
				#else
				float staticSwitch18_g11730 = temp_output_7_0_g11730;
				#endif
				float2 appendResult12_g11730 = (float2(staticSwitch18_g11730 , 0.0));
				float2 staticSwitch13_g11730 = ( temp_output_1_0_g11730 + appendResult12_g11730 );
				#else
				float2 staticSwitch13_g11730 = temp_output_1_0_g11730;
				#endif
				float2 temp_output_484_0 = staticSwitch13_g11730;
				float2 texCoord131 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
#ifdef _SHADERFADING_MASK
				float2 uv_FadingMask = IN.ase_texcoord.xy * _FadingMask_ST.xy + _FadingMask_ST.zw;
				#endif
#ifdef _SHADERFADING_MASK
				float4 tex2DNode3_g11712 = tex2D( _FadingMask, uv_FadingMask );
				#endif
				float temp_output_4_0_g11715 = max( _FadingWidth , 0.001 );
				float linValue16_g11716 = tex2D( _UberNoiseTexture, ( shaderPosition235 * _FadingNoiseScale ) ).r;
				float localMyCustomExpression16_g11716 = MyCustomExpression16_g11716( linValue16_g11716 );
				float clampResult14_g11715 = clamp( ( ( ( _FadingFade * ( 1.0 + temp_output_4_0_g11715 ) ) - localMyCustomExpression16_g11716 ) / temp_output_4_0_g11715 ) , 0.0 , 1.0 );
				float2 temp_output_27_0_g11713 = shaderPosition235;
				float linValue16_g11714 = tex2D( _UberNoiseTexture, ( temp_output_27_0_g11713 * _FadingNoiseScale ) ).r;
				float localMyCustomExpression16_g11714 = MyCustomExpression16_g11714( linValue16_g11714 );
#ifdef _SHADERFADING_SPREAD
				float clampResult3_g11713 = clamp( ( ( _FadingFade - ( distance( _FadingPosition , temp_output_27_0_g11713 ) + ( localMyCustomExpression16_g11714 * _FadingNoiseFactor ) ) ) / max( _FadingWidth , 0.001 ) ) , 0.0 , 1.0 );
				#endif
				#if defined(_SHADERFADING_NONE)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_FULL)
				float staticSwitch139 = _FadingFade;
				#elif defined(_SHADERFADING_MASK)
				float staticSwitch139 = ( _FadingFade * ( tex2DNode3_g11712.r * tex2DNode3_g11712.a ) );
				#elif defined(_SHADERFADING_DISSOLVE)
				float staticSwitch139 = clampResult14_g11715;
				#elif defined(_SHADERFADING_SPREAD)
				float staticSwitch139 = clampResult3_g11713;
				#else
				float staticSwitch139 = _FadingFade;
				#endif
				float fullFade123 = staticSwitch139;
				float2 lerpResult130 = lerp( texCoord131 , temp_output_484_0 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float2 staticSwitch145 = temp_output_484_0;
				#elif defined(_SHADERFADING_FULL)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_MASK)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_DISSOLVE)
				float2 staticSwitch145 = lerpResult130;
				#elif defined(_SHADERFADING_SPREAD)
				float2 staticSwitch145 = lerpResult130;
				#else
				float2 staticSwitch145 = temp_output_484_0;
				#endif
				#ifdef _TILINGFIX_ON
				float2 staticSwitch485 = ( ( ( staticSwitch145 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				#else
				float2 staticSwitch485 = staticSwitch145;
				#endif
				#ifdef _SPRITESHEETFIX_ON
				float2 break14_g11731 = staticSwitch485;
				float2 break11_g11731 = float2( 0,0 );
				float2 break10_g11731 = float2( 1,1 );
				float2 break9_g11731 = spriteRectMin376;
				float2 break8_g11731 = spriteRectMax377;
				float2 appendResult15_g11731 = (float2((break9_g11731.x + (break14_g11731.x - break11_g11731.x) * (break8_g11731.x - break9_g11731.x) / (break10_g11731.x - break11_g11731.x)) , (break9_g11731.y + (break14_g11731.y - break11_g11731.y) * (break8_g11731.y - break9_g11731.y) / (break10_g11731.y - break11_g11731.y))));
				float2 staticSwitch371 = min( max( appendResult15_g11731 , spriteRectMin376 ) , spriteRectMax377 );
				#else
				float2 staticSwitch371 = staticSwitch485;
				#endif
				#ifdef _PIXELPERFECTUV_ON
				float2 appendResult7_g11732 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 staticSwitch427 = ( originalUV460 + ( floor( ( ( staticSwitch371 - uvAfterPixelArt450 ) * appendResult7_g11732 ) ) / appendResult7_g11732 ) );
				#else
				float2 staticSwitch427 = staticSwitch371;
				#endif
				float2 finalUV146 = staticSwitch427;
				float2 temp_output_1_0_g11733 = finalUV146;
				#ifdef _ENABLESMOOTHPIXELART_ON
				sampler2D tex3_g11734 = _MainTex;
				float4 textureTexelSize3_g11734 = _MainTex_TexelSize;
				float2 uvs3_g11734 = temp_output_1_0_g11733;
				float4 localtexturePointSmooth3_g11734 = texturePointSmooth( tex3_g11734 , textureTexelSize3_g11734 , uvs3_g11734 );
				float4 staticSwitch8_g11733 = localtexturePointSmooth3_g11734;
				#else
				float4 staticSwitch8_g11733 = tex2D( _MainTex, temp_output_1_0_g11733 );
				#endif
				#ifdef _ENABLEGAUSSIANBLUR_ON
				float temp_output_10_0_g11735 = ( _GaussianBlurOffset * _GaussianBlurFade * 0.005 );
				float temp_output_2_0_g11745 = temp_output_10_0_g11735;
				float2 appendResult16_g11745 = (float2(temp_output_2_0_g11745 , 0.0));
				float2 appendResult25_g11747 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11747 = ( appendResult16_g11745 * appendResult25_g11747 );
				float2 temp_output_7_0_g11735 = temp_output_1_0_g11733;
				float2 temp_output_1_0_g11745 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( 1,1 ) ) );
				float2 temp_output_1_0_g11747 = temp_output_1_0_g11745;
				float2 appendResult17_g11745 = (float2(0.0 , temp_output_2_0_g11745));
				float2 appendResult25_g11746 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11746 = ( appendResult17_g11745 * appendResult25_g11746 );
				float2 temp_output_1_0_g11746 = temp_output_1_0_g11745;
				float temp_output_2_0_g11736 = temp_output_10_0_g11735;
				float2 appendResult16_g11736 = (float2(temp_output_2_0_g11736 , 0.0));
				float2 appendResult25_g11738 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11738 = ( appendResult16_g11736 * appendResult25_g11738 );
				float2 temp_output_1_0_g11736 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( -1,1 ) ) );
				float2 temp_output_1_0_g11738 = temp_output_1_0_g11736;
				float2 appendResult17_g11736 = (float2(0.0 , temp_output_2_0_g11736));
				float2 appendResult25_g11737 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11737 = ( appendResult17_g11736 * appendResult25_g11737 );
				float2 temp_output_1_0_g11737 = temp_output_1_0_g11736;
				float temp_output_2_0_g11742 = temp_output_10_0_g11735;
				float2 appendResult16_g11742 = (float2(temp_output_2_0_g11742 , 0.0));
				float2 appendResult25_g11744 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11744 = ( appendResult16_g11742 * appendResult25_g11744 );
				float2 temp_output_1_0_g11742 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( -1,-1 ) ) );
				float2 temp_output_1_0_g11744 = temp_output_1_0_g11742;
				float2 appendResult17_g11742 = (float2(0.0 , temp_output_2_0_g11742));
				float2 appendResult25_g11743 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11743 = ( appendResult17_g11742 * appendResult25_g11743 );
				float2 temp_output_1_0_g11743 = temp_output_1_0_g11742;
				float temp_output_2_0_g11739 = temp_output_10_0_g11735;
				float2 appendResult16_g11739 = (float2(temp_output_2_0_g11739 , 0.0));
				float2 appendResult25_g11741 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11741 = ( appendResult16_g11739 * appendResult25_g11741 );
				float2 temp_output_1_0_g11739 = ( temp_output_7_0_g11735 + ( temp_output_10_0_g11735 * float2( 1,-1 ) ) );
				float2 temp_output_1_0_g11741 = temp_output_1_0_g11739;
				float2 appendResult17_g11739 = (float2(0.0 , temp_output_2_0_g11739));
				float2 appendResult25_g11740 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11740 = ( appendResult17_g11739 * appendResult25_g11740 );
				float2 temp_output_1_0_g11740 = temp_output_1_0_g11739;
				float4 staticSwitch3_g11733 = ( ( ( ( tex2D( _MainTex, ( temp_output_26_0_g11747 + temp_output_1_0_g11747 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11747 + temp_output_1_0_g11747 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11746 + temp_output_1_0_g11746 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11746 + temp_output_1_0_g11746 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11738 + temp_output_1_0_g11738 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11738 + temp_output_1_0_g11738 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11737 + temp_output_1_0_g11737 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11737 + temp_output_1_0_g11737 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11744 + temp_output_1_0_g11744 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11744 + temp_output_1_0_g11744 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11743 + temp_output_1_0_g11743 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11743 + temp_output_1_0_g11743 ) ) ) ) + ( ( tex2D( _MainTex, ( temp_output_26_0_g11741 + temp_output_1_0_g11741 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11741 + temp_output_1_0_g11741 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11740 + temp_output_1_0_g11740 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11740 + temp_output_1_0_g11740 ) ) ) ) ) * 0.0625 );
				#else
				float4 staticSwitch3_g11733 = staticSwitch8_g11733;
				#endif
				#ifdef _ENABLESHARPEN_ON
				float2 temp_output_1_0_g11748 = temp_output_1_0_g11733;
				float4 tex2DNode4_g11748 = tex2D( _MainTex, temp_output_1_0_g11748 );
				float temp_output_2_0_g11749 = _SharpenOffset;
				float2 appendResult16_g11749 = (float2(temp_output_2_0_g11749 , 0.0));
				float2 appendResult25_g11751 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11751 = ( appendResult16_g11749 * appendResult25_g11751 );
				float2 temp_output_1_0_g11749 = temp_output_1_0_g11748;
				float2 temp_output_1_0_g11751 = temp_output_1_0_g11749;
				float2 appendResult17_g11749 = (float2(0.0 , temp_output_2_0_g11749));
				float2 appendResult25_g11750 = (float2(_MainTex_TexelSize.x , _MainTex_TexelSize.y));
				float2 temp_output_26_0_g11750 = ( appendResult17_g11749 * appendResult25_g11750 );
				float2 temp_output_1_0_g11750 = temp_output_1_0_g11749;
				float4 break22_g11748 = ( tex2DNode4_g11748 - ( ( ( ( ( tex2D( _MainTex, ( temp_output_26_0_g11751 + temp_output_1_0_g11751 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11751 + temp_output_1_0_g11751 ) ) ) + ( tex2D( _MainTex, ( temp_output_26_0_g11750 + temp_output_1_0_g11750 ) ) + tex2D( _MainTex, ( -temp_output_26_0_g11750 + temp_output_1_0_g11750 ) ) ) ) / 4.0 ) - tex2DNode4_g11748 ) * ( _SharpenFactor * _SharpenFade ) ) );
				float clampResult23_g11748 = clamp( break22_g11748.a , 0.0 , 1.0 );
				float4 appendResult24_g11748 = (float4(break22_g11748.r , break22_g11748.g , break22_g11748.b , clampResult23_g11748));
				float4 staticSwitch12_g11733 = appendResult24_g11748;
				#else
				float4 staticSwitch12_g11733 = staticSwitch3_g11733;
				#endif
				float4 temp_output_471_0 = staticSwitch12_g11733;
				#ifdef _VERTEXTINTFIRST_ON
				float4 temp_output_1_0_g11752 = temp_output_471_0;
				float4 appendResult8_g11752 = (float4(( (temp_output_1_0_g11752).rgb * (IN.ase_color).rgb ) , temp_output_1_0_g11752.a));
				float4 staticSwitch354 = appendResult8_g11752;
				#else
				float4 staticSwitch354 = temp_output_471_0;
				#endif
				float4 originalColor191 = staticSwitch354;
				float4 temp_output_1_0_g11753 = originalColor191;
				float4 temp_output_1_0_g11754 = temp_output_1_0_g11753;
				float2 temp_output_7_0_g11753 = finalUV146;
				#ifdef _ENABLESMOKE_ON
				float2 temp_output_43_0_g11754 = temp_output_7_0_g11753;
				float2 temp_cast_15 = (_SmokeNoiseScale).xx;
				float linValue16_g11755 = tex2D( _UberNoiseTexture, ( ( ( IN.ase_color.r * (( _SmokeVertexSeed )?( 5.0 ):( 0.0 )) ) + temp_output_43_0_g11754 ) * temp_cast_15 ) ).r;
				float localMyCustomExpression16_g11755 = MyCustomExpression16_g11755( linValue16_g11755 );
				float clampResult28_g11754 = clamp( ( ( ( localMyCustomExpression16_g11755 - 1.0 ) * _SmokeNoiseFactor ) + ( ( ( IN.ase_color.a / 2.5 ) - distance( temp_output_43_0_g11754 , float2( 0.5,0.5 ) ) ) * 2.5 * _SmokeSmoothness ) ) , 0.0 , 1.0 );
				float3 lerpResult34_g11754 = lerp( (temp_output_1_0_g11754).rgb , float3( 0,0,0 ) , ( ( 1.0 - clampResult28_g11754 ) * _SmokeDarkEdge ));
				float4 appendResult31_g11754 = (float4(lerpResult34_g11754 , ( clampResult28_g11754 * _SmokeAlpha * temp_output_1_0_g11754.a )));
				float4 staticSwitch2_g11753 = appendResult31_g11754;
				#else
				float4 staticSwitch2_g11753 = temp_output_1_0_g11753;
				#endif
				#ifdef _ENABLECUSTOMFADE_ON
				float4 temp_output_1_0_g11756 = staticSwitch2_g11753;
				float2 temp_output_57_0_g11756 = temp_output_7_0_g11753;
				float4 tex2DNode3_g11756 = tex2D( _CustomFadeFadeMask, temp_output_57_0_g11756 );
				float linValue16_g11757 = tex2D( _UberNoiseTexture, ( temp_output_57_0_g11756 * _CustomFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11757 = MyCustomExpression16_g11757( linValue16_g11757 );
				float clampResult37_g11756 = clamp( ( ( ( IN.ase_color.a * 2.0 ) - 1.0 ) + ( tex2DNode3_g11756.r + ( localMyCustomExpression16_g11757 * _CustomFadeNoiseFactor ) ) ) , 0.0 , 1.0 );
				float4 appendResult13_g11756 = (float4((temp_output_1_0_g11756).rgb , ( temp_output_1_0_g11756.a * pow( clampResult37_g11756 , ( _CustomFadeSmoothness / max( tex2DNode3_g11756.r , 0.05 ) ) ) * _CustomFadeAlpha )));
				float4 staticSwitch3_g11753 = appendResult13_g11756;
				#else
				float4 staticSwitch3_g11753 = staticSwitch2_g11753;
				#endif
				float4 temp_output_1_0_g11758 = staticSwitch3_g11753;
				#ifdef _ENABLECHECKERBOARD_ON
				float4 temp_output_1_0_g11759 = temp_output_1_0_g11758;
				float2 appendResult4_g11759 = (float2(ase_worldPos.x , ase_worldPos.y));
				float2 temp_output_44_0_g11759 = ( appendResult4_g11759 * _CheckerboardTiling * 0.5 );
				float2 break12_g11759 = step( ( ceil( temp_output_44_0_g11759 ) - temp_output_44_0_g11759 ) , float2( 0.5,0.5 ) );
				float4 appendResult42_g11759 = (float4(( (temp_output_1_0_g11759).rgb * min( ( _CheckerboardDarken + abs( ( -break12_g11759.x + break12_g11759.y ) ) ) , 1.0 ) ) , temp_output_1_0_g11759.a));
				float4 staticSwitch2_g11758 = appendResult42_g11759;
				#else
				float4 staticSwitch2_g11758 = temp_output_1_0_g11758;
				#endif
				#ifdef _ENABLEFLAME_ON
				float2 temp_output_75_0_g11760 = finalUV146;
				float linValue16_g11761 = tex2D( _UberNoiseTexture, ( ( ( shaderTime237 * _FlameSpeed ) + temp_output_75_0_g11760 ) * _FlameNoiseScale ) ).r;
				float localMyCustomExpression16_g11761 = MyCustomExpression16_g11761( linValue16_g11761 );
				float saferPower57_g11760 = abs( max( ( temp_output_75_0_g11760.y - 0.2 ) , 0.0 ) );
				float temp_output_47_0_g11760 = max( _FlameRadius , 0.01 );
				float clampResult70_g11760 = clamp( ( ( ( localMyCustomExpression16_g11761 * pow( saferPower57_g11760 , _FlameNoiseHeightFactor ) * _FlameNoiseFactor ) + ( ( temp_output_47_0_g11760 - distance( temp_output_75_0_g11760 , float2( 0.5,0.4 ) ) ) / temp_output_47_0_g11760 ) ) * _FlameSmooth ) , 0.0 , 1.0 );
				float temp_output_63_0_g11760 = ( clampResult70_g11760 * _FlameBrightness );
				float4 appendResult31_g11760 = (float4(temp_output_63_0_g11760 , temp_output_63_0_g11760 , temp_output_63_0_g11760 , clampResult70_g11760));
				float4 staticSwitch6_g11758 = ( appendResult31_g11760 * staticSwitch2_g11758 );
				#else
				float4 staticSwitch6_g11758 = staticSwitch2_g11758;
				#endif
				float4 temp_output_3_0_g11762 = staticSwitch6_g11758;
				float4 temp_output_1_0_g11794 = temp_output_3_0_g11762;
				float4 appendResult91_g11794 = (float4((temp_output_1_0_g11794).rgb , 1.0));
				float2 temp_output_1_0_g11762 = finalUV146;
				#ifdef _ENABLERECOLORRGB_ON
				#ifdef _RECOLORRGBTEXTURETOGGLE_ON
				float4 staticSwitch81_g11794 = tex2D( _RecolorRGBTexture, temp_output_1_0_g11762 );
				#else
				float4 staticSwitch81_g11794 = appendResult91_g11794;
				#endif
				float3 break82_g11794 = (staticSwitch81_g11794).xyz;
				float temp_output_84_0_g11794 = max( ( break82_g11794.x + break82_g11794.y + break82_g11794.z ) , 0.001 );
				float4 break71_g11794 = ( ( _RecolorRGBRedTint * ( break82_g11794.x / temp_output_84_0_g11794 ) ) + ( _RecolorRGBGreenTint * ( break82_g11794.y / temp_output_84_0_g11794 ) ) + ( ( break82_g11794.z / temp_output_84_0_g11794 ) * _RecolorRGBBlueTint ) );
				float3 appendResult56_g11794 = (float3(break71_g11794.r , break71_g11794.g , break71_g11794.b));
				float4 break2_g11795 = temp_output_1_0_g11794;
				float saferPower57_g11794 = abs( ( ( break2_g11795.x + break2_g11795.x + break2_g11795.y + break2_g11795.y + break2_g11795.y + break2_g11795.z ) / 6.0 ) );
				float3 lerpResult26_g11794 = lerp( (temp_output_1_0_g11794).rgb , ( appendResult56_g11794 * pow( saferPower57_g11794 , ( max( break71_g11794.a , 0.01 ) * 2.0 ) ) ) , ( staticSwitch81_g11794.w * _RecolorRGBFade ));
				float4 appendResult30_g11794 = (float4(lerpResult26_g11794 , temp_output_1_0_g11794.a));
				float4 staticSwitch43_g11762 = appendResult30_g11794;
				#else
				float4 staticSwitch43_g11762 = temp_output_3_0_g11762;
				#endif
				#ifdef _ENABLERECOLORRGBYCP_ON
				float4 temp_output_1_0_g11792 = staticSwitch43_g11762;
				#ifdef _RECOLORRGBYCPTEXTURETOGGLE_ON
				float4 staticSwitch62_g11792 = tex2D( _RecolorRGBYCPTexture, temp_output_1_0_g11762 );
				#else
				float4 staticSwitch62_g11792 = temp_output_1_0_g11792;
				#endif
				float3 hsvTorgb33_g11792 = RGBToHSV( staticSwitch62_g11792.rgb );
				float temp_output_43_0_g11792 = ( ( hsvTorgb33_g11792.x + 0.08333334 ) % 1.0 );
				float4 ifLocalVar46_g11792 = 0;
				if( temp_output_43_0_g11792 >= 0.8333333 )
				ifLocalVar46_g11792 = _RecolorRGBYCPPurpleTint;
				else
				ifLocalVar46_g11792 = _RecolorRGBYCPBlueTint;
				float4 ifLocalVar44_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.6666667 )
				ifLocalVar44_g11792 = _RecolorRGBYCPCyanTint;
				else
				ifLocalVar44_g11792 = ifLocalVar46_g11792;
				float4 ifLocalVar47_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.3333333 )
				ifLocalVar47_g11792 = _RecolorRGBYCPYellowTint;
				else
				ifLocalVar47_g11792 = _RecolorRGBYCPGreenTint;
				float4 ifLocalVar45_g11792 = 0;
				if( temp_output_43_0_g11792 <= 0.1666667 )
				ifLocalVar45_g11792 = _RecolorRGBYCPRedTint;
				else
				ifLocalVar45_g11792 = ifLocalVar47_g11792;
				float4 ifLocalVar35_g11792 = 0;
				if( temp_output_43_0_g11792 >= 0.5 )
				ifLocalVar35_g11792 = ifLocalVar44_g11792;
				else
				ifLocalVar35_g11792 = ifLocalVar45_g11792;
				float4 break55_g11792 = ifLocalVar35_g11792;
				float3 appendResult56_g11792 = (float3(break55_g11792.r , break55_g11792.g , break55_g11792.b));
				float4 break2_g11793 = temp_output_1_0_g11792;
				float saferPower57_g11792 = abs( ( ( break2_g11793.x + break2_g11793.x + break2_g11793.y + break2_g11793.y + break2_g11793.y + break2_g11793.z ) / 6.0 ) );
				float3 lerpResult26_g11792 = lerp( (temp_output_1_0_g11792).rgb , ( appendResult56_g11792 * pow( saferPower57_g11792 , max( ( break55_g11792.a * 2.0 ) , 0.01 ) ) ) , ( hsvTorgb33_g11792.z * _RecolorRGBYCPFade ));
				float4 appendResult30_g11792 = (float4(lerpResult26_g11792 , temp_output_1_0_g11792.a));
				float4 staticSwitch9_g11762 = appendResult30_g11792;
				#else
				float4 staticSwitch9_g11762 = staticSwitch43_g11762;
				#endif
				#ifdef _ENABLECOLORREPLACE_ON
				float4 temp_output_1_0_g11766 = staticSwitch9_g11762;
				float3 temp_output_2_0_g11766 = (temp_output_1_0_g11766).rgb;
				float3 In115_g11766 = temp_output_2_0_g11766;
				float3 From115_g11766 = (_ColorReplaceFromColor).rgb;
				float4 break2_g11767 = temp_output_1_0_g11766;
				float3 To115_g11766 = ( pow( ( ( break2_g11767.x + break2_g11767.x + break2_g11767.y + break2_g11767.y + break2_g11767.y + break2_g11767.z ) / 6.0 ) , max( _ColorReplaceContrast , 0.001 ) ) * (_ColorReplaceToColor).rgb );
				float Fuzziness115_g11766 = _ColorReplaceSmoothness;
				float Range115_g11766 = _ColorReplaceRange;
				float3 localMyCustomExpression115_g11766 = MyCustomExpression115_g11766( In115_g11766 , From115_g11766 , To115_g11766 , Fuzziness115_g11766 , Range115_g11766 );
				float3 lerpResult112_g11766 = lerp( temp_output_2_0_g11766 , localMyCustomExpression115_g11766 , _ColorReplaceFade);
				float4 appendResult4_g11766 = (float4(lerpResult112_g11766 , temp_output_1_0_g11766.a));
				float4 staticSwitch29_g11762 = appendResult4_g11766;
				#else
				float4 staticSwitch29_g11762 = staticSwitch9_g11762;
				#endif
				float4 temp_output_1_0_g11779 = staticSwitch29_g11762;
				#ifdef _ENABLENEGATIVE_ON
				float3 temp_output_9_0_g11779 = (temp_output_1_0_g11779).rgb;
				float3 lerpResult3_g11779 = lerp( temp_output_9_0_g11779 , ( 1.0 - temp_output_9_0_g11779 ) , _NegativeFade);
				float4 appendResult8_g11779 = (float4(lerpResult3_g11779 , temp_output_1_0_g11779.a));
				float4 staticSwitch4_g11779 = appendResult8_g11779;
				#else
				float4 staticSwitch4_g11779 = temp_output_1_0_g11779;
				#endif
				float4 temp_output_57_0_g11762 = staticSwitch4_g11779;
				#ifdef _ENABLECONTRAST_ON
				float4 temp_output_1_0_g11802 = temp_output_57_0_g11762;
				float3 temp_cast_30 = (max( _Contrast , 0.001 )).xxx;
				float4 appendResult4_g11802 = (float4(pow( (temp_output_1_0_g11802).rgb , temp_cast_30 ) , temp_output_1_0_g11802.a));
				float4 staticSwitch32_g11762 = appendResult4_g11802;
				#else
				float4 staticSwitch32_g11762 = temp_output_57_0_g11762;
				#endif
				#ifdef _ENABLEBRIGHTNESS_ON
				float4 temp_output_2_0_g11777 = staticSwitch32_g11762;
				float4 appendResult6_g11777 = (float4(( (temp_output_2_0_g11777).rgb * _Brightness ) , temp_output_2_0_g11777.a));
				float4 staticSwitch33_g11762 = appendResult6_g11777;
				#else
				float4 staticSwitch33_g11762 = staticSwitch32_g11762;
				#endif
				#ifdef _ENABLEHUE_ON
				float4 temp_output_2_0_g11778 = staticSwitch33_g11762;
				float3 hsvTorgb1_g11778 = RGBToHSV( temp_output_2_0_g11778.rgb );
				float3 hsvTorgb3_g11778 = HSVToRGB( float3(( hsvTorgb1_g11778.x + _Hue ),hsvTorgb1_g11778.y,hsvTorgb1_g11778.z) );
				float4 appendResult8_g11778 = (float4(hsvTorgb3_g11778 , temp_output_2_0_g11778.a));
				float4 staticSwitch36_g11762 = appendResult8_g11778;
				#else
				float4 staticSwitch36_g11762 = staticSwitch33_g11762;
				#endif
				#ifdef _ENABLESPLITTONING_ON
				float4 temp_output_1_0_g11796 = staticSwitch36_g11762;
				float4 break2_g11797 = temp_output_1_0_g11796;
				float temp_output_3_0_g11796 = ( ( break2_g11797.x + break2_g11797.x + break2_g11797.y + break2_g11797.y + break2_g11797.y + break2_g11797.z ) / 6.0 );
				float clampResult25_g11796 = clamp( ( ( ( ( temp_output_3_0_g11796 + _SplitToningShift ) - 0.5 ) * _SplitToningBalance ) + 0.5 ) , 0.0 , 1.0 );
				float3 lerpResult6_g11796 = lerp( (_SplitToningShadowsColor).rgb , (_SplitToningHighlightsColor).rgb , clampResult25_g11796);
				float3 lerpResult11_g11796 = lerp( (temp_output_1_0_g11796).rgb , ( lerpResult6_g11796 * pow( temp_output_3_0_g11796 , max( _SplitToningContrast , 0.001 ) ) ) , _SplitToningFade);
				float4 appendResult18_g11796 = (float4(lerpResult11_g11796 , temp_output_1_0_g11796.a));
				float4 staticSwitch30_g11762 = appendResult18_g11796;
				#else
				float4 staticSwitch30_g11762 = staticSwitch36_g11762;
				#endif
				#ifdef _ENABLEBLACKTINT_ON
				float4 temp_output_1_0_g11774 = staticSwitch30_g11762;
				float3 temp_output_4_0_g11774 = (temp_output_1_0_g11774).rgb;
				float4 break12_g11774 = temp_output_1_0_g11774;
				float3 lerpResult7_g11774 = lerp( temp_output_4_0_g11774 , ( temp_output_4_0_g11774 + (_BlackTintColor).rgb ) , pow( ( 1.0 - min( max( max( break12_g11774.r , break12_g11774.g ) , break12_g11774.b ) , 1.0 ) ) , max( _BlackTintPower , 0.001 ) ));
				float3 lerpResult13_g11774 = lerp( temp_output_4_0_g11774 , lerpResult7_g11774 , _BlackTintFade);
				float4 appendResult11_g11774 = (float4(lerpResult13_g11774 , break12_g11774.a));
				float4 staticSwitch20_g11762 = appendResult11_g11774;
				#else
				float4 staticSwitch20_g11762 = staticSwitch30_g11762;
				#endif
				#ifdef _ENABLEINKSPREAD_ON
				float4 temp_output_1_0_g11788 = staticSwitch20_g11762;
				float4 break2_g11790 = temp_output_1_0_g11788;
				float2 temp_output_65_0_g11788 = shaderPosition235;
				float linValue16_g11789 = tex2D( _UberNoiseTexture, ( temp_output_65_0_g11788 * _InkSpreadNoiseScale ) ).r;
				float localMyCustomExpression16_g11789 = MyCustomExpression16_g11789( linValue16_g11789 );
				float clampResult53_g11788 = clamp( ( ( ( _InkSpreadDistance - distance( _InkSpreadPosition , temp_output_65_0_g11788 ) ) + ( localMyCustomExpression16_g11789 * _InkSpreadNoiseFactor ) ) / max( _InkSpreadWidth , 0.001 ) ) , 0.0 , 1.0 );
				float3 lerpResult7_g11788 = lerp( (temp_output_1_0_g11788).rgb , ( (_InkSpreadColor).rgb * pow( ( ( break2_g11790.x + break2_g11790.x + break2_g11790.y + break2_g11790.y + break2_g11790.y + break2_g11790.z ) / 6.0 ) , max( _InkSpreadContrast , 0.001 ) ) ) , ( _InkSpreadFade * clampResult53_g11788 ));
				float4 appendResult9_g11788 = (float4(lerpResult7_g11788 , (temp_output_1_0_g11788).a));
				float4 staticSwitch17_g11762 = appendResult9_g11788;
				#else
				float4 staticSwitch17_g11762 = staticSwitch20_g11762;
				#endif
				float temp_output_39_0_g11762 = shaderTime237;
				#ifdef _ENABLESHIFTHUE_ON
				float4 temp_output_1_0_g11780 = staticSwitch17_g11762;
				float3 hsvTorgb15_g11780 = RGBToHSV( (temp_output_1_0_g11780).rgb );
				float3 hsvTorgb19_g11780 = HSVToRGB( float3(( ( temp_output_39_0_g11762 * _ShiftHueSpeed ) + hsvTorgb15_g11780.x ),hsvTorgb15_g11780.y,hsvTorgb15_g11780.z) );
				float4 appendResult6_g11780 = (float4(hsvTorgb19_g11780 , temp_output_1_0_g11780.a));
				float4 staticSwitch19_g11762 = appendResult6_g11780;
				#else
				float4 staticSwitch19_g11762 = staticSwitch17_g11762;
				#endif
				#ifdef _ENABLEADDHUE_ON
				float3 hsvTorgb19_g11784 = HSVToRGB( float3(( ( temp_output_39_0_g11762 * _AddHueSpeed ) % 1.0 ),_AddHueSaturation,_AddHueBrightness) );
				float4 temp_output_1_0_g11784 = staticSwitch19_g11762;
				float4 break2_g11786 = temp_output_1_0_g11784;
				float2 uv_AddHueMask = IN.ase_texcoord.xy * _AddHueMask_ST.xy + _AddHueMask_ST.zw;
				float4 tex2DNode3_g11785 = tex2D( _AddHueMask, uv_AddHueMask );
				#ifdef _ADDHUEMASKTOGGLE_ON
				float staticSwitch33_g11784 = ( _AddHueFade * ( tex2DNode3_g11785.r * tex2DNode3_g11785.a ) );
				#else
				float staticSwitch33_g11784 = _AddHueFade;
				#endif
				float4 appendResult6_g11784 = (float4(( ( hsvTorgb19_g11784 * pow( ( ( break2_g11786.x + break2_g11786.x + break2_g11786.y + break2_g11786.y + break2_g11786.y + break2_g11786.z ) / 6.0 ) , max( _AddHueContrast , 0.001 ) ) * staticSwitch33_g11784 ) + (temp_output_1_0_g11784).rgb ) , temp_output_1_0_g11784.a));
				float4 staticSwitch23_g11762 = appendResult6_g11784;
				#else
				float4 staticSwitch23_g11762 = staticSwitch19_g11762;
				#endif
				#ifdef _ENABLESINEGLOW_ON
				float4 temp_output_1_0_g11781 = staticSwitch23_g11762;
				float4 break2_g11782 = temp_output_1_0_g11781;
				float3 temp_output_13_0_g11781 = (_SineGlowColor).rgb;
				float2 uv_SineGlowMask = IN.ase_texcoord.xy * _SineGlowMask_ST.xy + _SineGlowMask_ST.zw;
				float4 tex2DNode30_g11781 = tex2D( _SineGlowMask, uv_SineGlowMask );
				#ifdef _SINEGLOWMASKTOGGLE_ON
				float3 staticSwitch27_g11781 = ( (tex2DNode30_g11781).rgb * temp_output_13_0_g11781 * tex2DNode30_g11781.a );
				#else
				float3 staticSwitch27_g11781 = temp_output_13_0_g11781;
				#endif
				float4 appendResult21_g11781 = (float4(( (temp_output_1_0_g11781).rgb + ( pow( ( ( break2_g11782.x + break2_g11782.x + break2_g11782.y + break2_g11782.y + break2_g11782.y + break2_g11782.z ) / 6.0 ) , max( _SineGlowContrast , 0.001 ) ) * staticSwitch27_g11781 * _SineGlowFade * ( ( ( sin( ( temp_output_39_0_g11762 * _SineGlowFrequency ) ) + 1.0 ) * ( _SineGlowMax - _SineGlowMin ) ) + _SineGlowMin ) ) ) , temp_output_1_0_g11781.a));
				float4 staticSwitch28_g11762 = appendResult21_g11781;
				#else
				float4 staticSwitch28_g11762 = staticSwitch23_g11762;
				#endif
				#ifdef _ENABLESATURATION_ON
				float4 temp_output_1_0_g11769 = staticSwitch28_g11762;
				float4 break2_g11770 = temp_output_1_0_g11769;
				float3 temp_cast_46 = (( ( break2_g11770.x + break2_g11770.x + break2_g11770.y + break2_g11770.y + break2_g11770.y + break2_g11770.z ) / 6.0 )).xxx;
				float3 lerpResult5_g11769 = lerp( temp_cast_46 , (temp_output_1_0_g11769).rgb , _Saturation);
				float4 appendResult8_g11769 = (float4(lerpResult5_g11769 , temp_output_1_0_g11769.a));
				float4 staticSwitch38_g11762 = appendResult8_g11769;
				#else
				float4 staticSwitch38_g11762 = staticSwitch28_g11762;
				#endif
				#ifdef _ENABLEINNEROUTLINE_ON
				float4 temp_output_15_0_g11771 = staticSwitch38_g11762;
				float3 temp_output_82_0_g11771 = (_InnerOutlineColor).rgb;
				float2 temp_output_7_0_g11771 = temp_output_1_0_g11762;
				float temp_output_179_0_g11771 = temp_output_39_0_g11762;
				#ifdef _INNEROUTLINETEXTURETOGGLE_ON
				float3 staticSwitch187_g11771 = ( (tex2D( _InnerOutlineTintTexture, ( temp_output_7_0_g11771 + ( _InnerOutlineTextureSpeed * temp_output_179_0_g11771 ) ) )).rgb * temp_output_82_0_g11771 );
				#else
				float3 staticSwitch187_g11771 = temp_output_82_0_g11771;
				#endif
				float linValue16_g11773 = tex2D( _UberNoiseTexture, ( ( ( temp_output_179_0_g11771 * _InnerOutlineNoiseSpeed ) + temp_output_7_0_g11771 ) * _InnerOutlineNoiseScale ) ).r;
				float localMyCustomExpression16_g11773 = MyCustomExpression16_g11773( linValue16_g11773 );
				#ifdef _INNEROUTLINEDISTORTIONTOGGLE_ON
				float2 staticSwitch169_g11771 = ( ( localMyCustomExpression16_g11773 - 0.5 ) * _InnerOutlineDistortionIntensity );
				#else
				float2 staticSwitch169_g11771 = float2( 0,0 );
				#endif
				float2 temp_output_131_0_g11771 = ( staticSwitch169_g11771 + temp_output_7_0_g11771 );
				float2 appendResult2_g11772 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_25_0_g11771 = ( 100.0 / appendResult2_g11772 );
				float temp_output_178_0_g11771 = ( _InnerOutlineFade * ( 1.0 - min( min( min( min( min( min( min( tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0,-1 ) ) * temp_output_25_0_g11771 ) ) ).a , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0,1 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -1,0 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 1,0 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0.705,0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -0.705,0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( 0.705,-0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11771 + ( ( _InnerOutlineWidth * float2( -0.705,-0.705 ) ) * temp_output_25_0_g11771 ) ) ).a ) ) );
				float3 lerpResult176_g11771 = lerp( (temp_output_15_0_g11771).rgb , staticSwitch187_g11771 , temp_output_178_0_g11771);
				#ifdef _INNEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch188_g11771 = ( temp_output_178_0_g11771 * temp_output_15_0_g11771.a );
				#else
				float staticSwitch188_g11771 = temp_output_15_0_g11771.a;
				#endif
				float4 appendResult177_g11771 = (float4(lerpResult176_g11771 , staticSwitch188_g11771));
				float4 staticSwitch12_g11762 = appendResult177_g11771;
				#else
				float4 staticSwitch12_g11762 = staticSwitch38_g11762;
				#endif
				#ifdef _ENABLEOUTEROUTLINE_ON
				float4 temp_output_15_0_g11799 = staticSwitch12_g11762;
				float3 temp_output_82_0_g11799 = (_OuterOutlineColor).rgb;
				float2 temp_output_7_0_g11799 = temp_output_1_0_g11762;
				float temp_output_186_0_g11799 = temp_output_39_0_g11762;
				#ifdef _OUTEROUTLINETEXTURETOGGLE_ON
				float3 staticSwitch199_g11799 = ( (tex2D( _OuterOutlineTintTexture, ( temp_output_7_0_g11799 + ( _OuterOutlineTextureSpeed * temp_output_186_0_g11799 ) ) )).rgb * temp_output_82_0_g11799 );
				#else
				float3 staticSwitch199_g11799 = temp_output_82_0_g11799;
				#endif
				float temp_output_182_0_g11799 = ( ( 1.0 - temp_output_15_0_g11799.a ) * min( ( _OuterOutlineFade * 3.0 ) , 1.0 ) );
				#ifdef _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch203_g11799 = 1.0;
				#else
				float staticSwitch203_g11799 = temp_output_182_0_g11799;
				#endif
				float3 lerpResult178_g11799 = lerp( (temp_output_15_0_g11799).rgb , staticSwitch199_g11799 , staticSwitch203_g11799);
				float3 lerpResult170_g11799 = lerp( lerpResult178_g11799 , staticSwitch199_g11799 , staticSwitch203_g11799);
				float linValue16_g11800 = tex2D( _UberNoiseTexture, ( ( ( temp_output_186_0_g11799 * _OuterOutlineNoiseSpeed ) + temp_output_7_0_g11799 ) * _OuterOutlineNoiseScale ) ).r;
				float localMyCustomExpression16_g11800 = MyCustomExpression16_g11800( linValue16_g11800 );
				#ifdef _OUTEROUTLINEDISTORTIONTOGGLE_ON
				float2 staticSwitch157_g11799 = ( ( localMyCustomExpression16_g11800 - 0.5 ) * _OuterOutlineDistortionIntensity );
				#else
				float2 staticSwitch157_g11799 = float2( 0,0 );
				#endif
				float2 temp_output_131_0_g11799 = ( staticSwitch157_g11799 + temp_output_7_0_g11799 );
				float2 appendResult2_g11801 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_25_0_g11799 = ( 100.0 / appendResult2_g11801 );
				float lerpResult168_g11799 = lerp( temp_output_15_0_g11799.a , min( ( max( max( max( max( max( max( max( tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0,-1 ) ) * temp_output_25_0_g11799 ) ) ).a , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0,1 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -1,0 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 1,0 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0.705,0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -0.705,0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( 0.705,-0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) , tex2D( _MainTex, ( temp_output_131_0_g11799 + ( ( _OuterOutlineWidth * float2( -0.705,-0.705 ) ) * temp_output_25_0_g11799 ) ) ).a ) * 3.0 ) , 1.0 ) , _OuterOutlineFade);
				#ifdef _OUTEROUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch200_g11799 = ( temp_output_182_0_g11799 * lerpResult168_g11799 );
				#else
				float staticSwitch200_g11799 = lerpResult168_g11799;
				#endif
				float4 appendResult174_g11799 = (float4(lerpResult170_g11799 , staticSwitch200_g11799));
				float4 staticSwitch13_g11762 = appendResult174_g11799;
				#else
				float4 staticSwitch13_g11762 = staticSwitch12_g11762;
				#endif
				#ifdef _ENABLEPIXELOUTLINE_ON
				float4 temp_output_15_0_g11776 = staticSwitch13_g11762;
				float3 temp_output_82_0_g11776 = (_PixelOutlineColor).rgb;
				float2 temp_output_7_0_g11776 = temp_output_1_0_g11762;
				#ifdef _PIXELOUTLINETEXTURETOGGLE_ON
				float3 staticSwitch199_g11776 = ( (tex2D( _PixelOutlineTintTexture, ( temp_output_7_0_g11776 + ( _PixelOutlineTextureSpeed * temp_output_39_0_g11762 ) ) )).rgb * temp_output_82_0_g11776 );
				#else
				float3 staticSwitch199_g11776 = temp_output_82_0_g11776;
				#endif
				float temp_output_182_0_g11776 = ( ( 1.0 - temp_output_15_0_g11776.a ) * min( ( _PixelOutlineFade * 3.0 ) , 1.0 ) );
				#ifdef _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch203_g11776 = 1.0;
				#else
				float staticSwitch203_g11776 = temp_output_182_0_g11776;
				#endif
				float3 lerpResult178_g11776 = lerp( (temp_output_15_0_g11776).rgb , staticSwitch199_g11776 , staticSwitch203_g11776);
				float3 lerpResult170_g11776 = lerp( lerpResult178_g11776 , staticSwitch199_g11776 , staticSwitch203_g11776);
				float2 appendResult206_g11776 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float2 temp_output_209_0_g11776 = ( float2( 1,1 ) / appendResult206_g11776 );
				float lerpResult168_g11776 = lerp( temp_output_15_0_g11776.a , min( ( max( max( max( tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 0,-1 ) ) * temp_output_209_0_g11776 ) ) ).a , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 0,1 ) ) * temp_output_209_0_g11776 ) ) ).a ) , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( -1,0 ) ) * temp_output_209_0_g11776 ) ) ).a ) , tex2D( _MainTex, ( temp_output_7_0_g11776 + ( ( _PixelOutlineWidth * float2( 1,0 ) ) * temp_output_209_0_g11776 ) ) ).a ) * 3.0 ) , 1.0 ) , _PixelOutlineFade);
				#ifdef _PIXELOUTLINEOUTLINEONLYTOGGLE_ON
				float staticSwitch200_g11776 = ( temp_output_182_0_g11776 * lerpResult168_g11776 );
				#else
				float staticSwitch200_g11776 = lerpResult168_g11776;
				#endif
				float4 appendResult174_g11776 = (float4(lerpResult170_g11776 , staticSwitch200_g11776));
				float4 staticSwitch48_g11762 = appendResult174_g11776;
				#else
				float4 staticSwitch48_g11762 = staticSwitch13_g11762;
				#endif
				#ifdef _ENABLEPINGPONGGLOW_ON
				float3 lerpResult15_g11763 = lerp( (_PingPongGlowFrom).rgb , (_PingPongGlowTo).rgb , ( ( sin( ( temp_output_39_0_g11762 * _PingPongGlowFrequency ) ) + 1.0 ) / 2.0 ));
				float4 temp_output_5_0_g11763 = staticSwitch48_g11762;
				float4 break2_g11764 = temp_output_5_0_g11763;
				float4 appendResult12_g11763 = (float4(( ( lerpResult15_g11763 * _PingPongGlowFade * pow( ( ( break2_g11764.x + break2_g11764.x + break2_g11764.y + break2_g11764.y + break2_g11764.y + break2_g11764.z ) / 6.0 ) , max( _PingPongGlowContrast , 0.001 ) ) ) + (temp_output_5_0_g11763).rgb ) , temp_output_5_0_g11763.a));
				float4 staticSwitch46_g11762 = appendResult12_g11763;
				#else
				float4 staticSwitch46_g11762 = staticSwitch48_g11762;
				#endif
				float4 temp_output_361_0 = staticSwitch46_g11762;
				#ifdef _ENABLEHOLOGRAM_ON
				float4 temp_output_1_0_g11804 = temp_output_361_0;
				float4 break2_g11805 = temp_output_1_0_g11804;
				float4 appendResult22_g11804 = (float4(( (_HologramTint).rgb * pow( ( ( break2_g11805.x + break2_g11805.x + break2_g11805.y + break2_g11805.y + break2_g11805.y + break2_g11805.z ) / 6.0 ) , max( _HologramContrast , 0.001 ) ) ) , ( max( pow( abs( sin( ( ( ( ( shaderTime237 * _HologramLineSpeed ) + ase_worldPos.y ) / unity_OrthoParams.y ) * _HologramLineFrequency ) ) ) , _HologramLineGap ) , _HologramMinAlpha ) * temp_output_1_0_g11804.a )));
				float4 lerpResult37_g11804 = lerp( temp_output_1_0_g11804 , appendResult22_g11804 , hologramFade182);
				float4 staticSwitch56 = lerpResult37_g11804;
				#else
				float4 staticSwitch56 = temp_output_361_0;
				#endif
				#ifdef _ENABLEGLITCH_ON
				float4 temp_output_1_0_g11807 = staticSwitch56;
				float4 break2_g11809 = temp_output_1_0_g11807;
				float temp_output_34_0_g11807 = shaderTime237;
				float linValue16_g11808 = tex2D( _UberNoiseTexture, ( ( glitchPosition154 + ( _GlitchNoiseSpeed * temp_output_34_0_g11807 ) ) * _GlitchNoiseScale ) ).r;
				float localMyCustomExpression16_g11808 = MyCustomExpression16_g11808( linValue16_g11808 );
				float3 hsvTorgb3_g11810 = HSVToRGB( float3(( localMyCustomExpression16_g11808 + ( temp_output_34_0_g11807 * _GlitchHueSpeed ) ),1.0,1.0) );
				float3 lerpResult23_g11807 = lerp( (temp_output_1_0_g11807).rgb , ( ( ( break2_g11809.x + break2_g11809.x + break2_g11809.y + break2_g11809.y + break2_g11809.y + break2_g11809.z ) / 6.0 ) * _GlitchBrightness * hsvTorgb3_g11810 ) , glitchFade152);
				float4 appendResult27_g11807 = (float4(lerpResult23_g11807 , temp_output_1_0_g11807.a));
				float4 staticSwitch57 = appendResult27_g11807;
				#else
				float4 staticSwitch57 = staticSwitch56;
				#endif
				float4 temp_output_3_0_g11811 = staticSwitch57;
				float4 temp_output_1_0_g11841 = temp_output_3_0_g11811;
				float2 temp_output_41_0_g11811 = shaderPosition235;
				float2 temp_output_99_0_g11841 = temp_output_41_0_g11811;
				float temp_output_40_0_g11811 = shaderTime237;
				#ifdef _ENABLECAMOUFLAGE_ON
				float linValue16_g11846 = tex2D( _UberNoiseTexture, ( ( ( temp_output_40_0_g11811 * _CamouflageDistortionSpeed ) + temp_output_99_0_g11841 ) * _CamouflageDistortionScale ) ).r;
				float localMyCustomExpression16_g11846 = MyCustomExpression16_g11846( linValue16_g11846 );
				#ifdef _CAMOUFLAGEANIMATIONTOGGLE_ON
				float2 staticSwitch101_g11841 = ( ( ( localMyCustomExpression16_g11846 - 0.25 ) * _CamouflageDistortionIntensity ) + temp_output_99_0_g11841 );
				#else
				float2 staticSwitch101_g11841 = temp_output_99_0_g11841;
				#endif
				float linValue16_g11843 = tex2D( _UberNoiseTexture, ( staticSwitch101_g11841 * _CamouflageNoiseScaleA ) ).r;
				float localMyCustomExpression16_g11843 = MyCustomExpression16_g11843( linValue16_g11843 );
				float clampResult52_g11841 = clamp( ( ( _CamouflageDensityA - localMyCustomExpression16_g11843 ) / max( _CamouflageSmoothnessA , 0.005 ) ) , 0.0 , 1.0 );
				float4 lerpResult55_g11841 = lerp( _CamouflageBaseColor , ( _CamouflageColorA * clampResult52_g11841 ) , clampResult52_g11841);
				float linValue16_g11845 = tex2D( _UberNoiseTexture, ( ( staticSwitch101_g11841 + float2( 12.3,12.3 ) ) * _CamouflageNoiseScaleB ) ).r;
				float localMyCustomExpression16_g11845 = MyCustomExpression16_g11845( linValue16_g11845 );
				float clampResult65_g11841 = clamp( ( ( _CamouflageDensityB - localMyCustomExpression16_g11845 ) / max( _CamouflageSmoothnessB , 0.005 ) ) , 0.0 , 1.0 );
				float4 lerpResult68_g11841 = lerp( lerpResult55_g11841 , ( _CamouflageColorB * clampResult65_g11841 ) , clampResult65_g11841);
				float4 break2_g11844 = temp_output_1_0_g11841;
				float3 lerpResult4_g11841 = lerp( (temp_output_1_0_g11841).rgb , ( (lerpResult68_g11841).rgb * pow( ( ( break2_g11844.x + break2_g11844.x + break2_g11844.y + break2_g11844.y + break2_g11844.y + break2_g11844.z ) / 6.0 ) , max( _CamouflageContrast , 0.001 ) ) ) , _CamouflageFade);
				float4 appendResult7_g11841 = (float4(lerpResult4_g11841 , temp_output_1_0_g11841.a));
				float4 staticSwitch26_g11811 = appendResult7_g11841;
				#else
				float4 staticSwitch26_g11811 = temp_output_3_0_g11811;
				#endif
				#ifdef _ENABLEMETAL_ON
				float4 temp_output_1_0_g11834 = staticSwitch26_g11811;
				float temp_output_59_0_g11834 = temp_output_40_0_g11811;
				float2 temp_output_58_0_g11834 = temp_output_41_0_g11811;
				float linValue16_g11835 = tex2D( _UberNoiseTexture, ( ( ( temp_output_59_0_g11834 * _MetalNoiseDistortionSpeed ) + temp_output_58_0_g11834 ) * _MetalNoiseDistortionScale ) ).r;
				float localMyCustomExpression16_g11835 = MyCustomExpression16_g11835( linValue16_g11835 );
				float linValue16_g11837 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11835 - 0.25 ) * _MetalNoiseDistortion ) + ( ( temp_output_59_0_g11834 * _MetalNoiseSpeed ) + temp_output_58_0_g11834 ) ) * _MetalNoiseScale ) ).r;
				float localMyCustomExpression16_g11837 = MyCustomExpression16_g11837( linValue16_g11837 );
				float4 break2_g11836 = temp_output_1_0_g11834;
				float temp_output_5_0_g11834 = ( ( break2_g11836.x + break2_g11836.x + break2_g11836.y + break2_g11836.y + break2_g11836.y + break2_g11836.z ) / 6.0 );
				float2 uv_MetalMask = IN.ase_texcoord.xy * _MetalMask_ST.xy + _MetalMask_ST.zw;
				float4 tex2DNode3_g11839 = tex2D( _MetalMask, uv_MetalMask );
				#ifdef _METALMASKTOGGLE_ON
				float staticSwitch60_g11834 = ( _MetalFade * ( tex2DNode3_g11839.r * tex2DNode3_g11839.a ) );
				#else
				float staticSwitch60_g11834 = _MetalFade;
				#endif
				float4 lerpResult45_g11834 = lerp( temp_output_1_0_g11834 , ( ( max( ( ( _MetalHighlightDensity - localMyCustomExpression16_g11837 ) / max( _MetalHighlightDensity , 0.01 ) ) , 0.0 ) * _MetalHighlightColor * pow( temp_output_5_0_g11834 , max( _MetalHighlightContrast , 0.001 ) ) ) + ( pow( temp_output_5_0_g11834 , max( _MetalContrast , 0.001 ) ) * _MetalColor ) ) , staticSwitch60_g11834);
				float4 appendResult8_g11834 = (float4((lerpResult45_g11834).rgb , (temp_output_1_0_g11834).a));
				float4 staticSwitch28_g11811 = appendResult8_g11834;
				#else
				float4 staticSwitch28_g11811 = staticSwitch26_g11811;
				#endif
				#ifdef _ENABLEFROZEN_ON
				float4 temp_output_1_0_g11826 = staticSwitch28_g11811;
				float4 break2_g11827 = temp_output_1_0_g11826;
				float temp_output_7_0_g11826 = ( ( break2_g11827.x + break2_g11827.x + break2_g11827.y + break2_g11827.y + break2_g11827.y + break2_g11827.z ) / 6.0 );
				float2 temp_output_72_0_g11826 = temp_output_41_0_g11811;
				float linValue16_g11828 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11826 * _FrozenSnowScale ) ).r;
				float localMyCustomExpression16_g11828 = MyCustomExpression16_g11828( linValue16_g11828 );
				float temp_output_73_0_g11826 = temp_output_40_0_g11811;
				float linValue16_g11830 = tex2D( _UberNoiseTexture, ( ( ( temp_output_73_0_g11826 * _FrozenHighlightDistortionSpeed ) + temp_output_72_0_g11826 ) * _FrozenHighlightDistortionScale ) ).r;
				float localMyCustomExpression16_g11830 = MyCustomExpression16_g11830( linValue16_g11830 );
				float linValue16_g11831 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11830 - 0.25 ) * _FrozenHighlightDistortion ) + ( ( temp_output_73_0_g11826 * _FrozenHighlightSpeed ) + temp_output_72_0_g11826 ) ) * _FrozenHighlightScale ) ).r;
				float localMyCustomExpression16_g11831 = MyCustomExpression16_g11831( linValue16_g11831 );
				float3 lerpResult57_g11826 = lerp( (temp_output_1_0_g11826).rgb , ( ( pow( temp_output_7_0_g11826 , max( _FrozenContrast , 0.001 ) ) * (_FrozenTint).rgb ) + ( pow( temp_output_7_0_g11826 , max( _FrozenSnowContrast , 0.001 ) ) * ( (_FrozenSnowColor).rgb * max( ( _FrozenSnowDensity - localMyCustomExpression16_g11828 ) , 0.0 ) ) ) + (( max( ( ( _FrozenHighlightDensity - localMyCustomExpression16_g11831 ) / max( _FrozenHighlightDensity , 0.01 ) ) , 0.0 ) * _FrozenHighlightColor * pow( temp_output_7_0_g11826 , max( _FrozenHighlightContrast , 0.001 ) ) )).rgb ) , _FrozenFade);
				float4 appendResult26_g11826 = (float4(lerpResult57_g11826 , temp_output_1_0_g11826.a));
				float4 staticSwitch29_g11811 = appendResult26_g11826;
				#else
				float4 staticSwitch29_g11811 = staticSwitch28_g11811;
				#endif
				#ifdef _ENABLEBURN_ON
				float4 temp_output_1_0_g11820 = staticSwitch29_g11811;
				float3 temp_output_28_0_g11820 = (temp_output_1_0_g11820).rgb;
				float4 break2_g11824 = float4( temp_output_28_0_g11820 , 0.0 );
				float2 temp_output_72_0_g11820 = temp_output_41_0_g11811;
				float linValue16_g11823 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11820 * _BurnSwirlNoiseScale ) ).r;
				float localMyCustomExpression16_g11823 = MyCustomExpression16_g11823( linValue16_g11823 );
				float linValue16_g11821 = tex2D( _UberNoiseTexture, ( ( ( ( localMyCustomExpression16_g11823 - 0.5 ) * float2( 1,1 ) * _BurnSwirlFactor ) + temp_output_72_0_g11820 ) * _BurnInsideNoiseScale ) ).r;
				float localMyCustomExpression16_g11821 = MyCustomExpression16_g11821( linValue16_g11821 );
				float clampResult68_g11820 = clamp( ( _BurnInsideNoiseFactor - localMyCustomExpression16_g11821 ) , 0.0 , 1.0 );
				float linValue16_g11822 = tex2D( _UberNoiseTexture, ( temp_output_72_0_g11820 * _BurnEdgeNoiseScale ) ).r;
				float localMyCustomExpression16_g11822 = MyCustomExpression16_g11822( linValue16_g11822 );
				float temp_output_15_0_g11820 = ( ( ( _BurnRadius - distance( temp_output_72_0_g11820 , _BurnPosition ) ) + ( localMyCustomExpression16_g11822 * _BurnEdgeNoiseFactor ) ) / max( _BurnWidth , 0.01 ) );
				float clampResult18_g11820 = clamp( temp_output_15_0_g11820 , 0.0 , 1.0 );
				float3 lerpResult29_g11820 = lerp( temp_output_28_0_g11820 , ( pow( ( ( break2_g11824.x + break2_g11824.x + break2_g11824.y + break2_g11824.y + break2_g11824.y + break2_g11824.z ) / 6.0 ) , max( _BurnInsideContrast , 0.001 ) ) * ( ( (_BurnInsideNoiseColor).rgb * clampResult68_g11820 ) + (_BurnInsideColor).rgb ) ) , clampResult18_g11820);
				float3 lerpResult40_g11820 = lerp( temp_output_28_0_g11820 , ( lerpResult29_g11820 + ( ( step( temp_output_15_0_g11820 , 1.0 ) * step( 0.0 , temp_output_15_0_g11820 ) ) * (_BurnEdgeColor).rgb ) ) , _BurnFade);
				float4 appendResult43_g11820 = (float4(lerpResult40_g11820 , temp_output_1_0_g11820.a));
				float4 staticSwitch32_g11811 = appendResult43_g11820;
				#else
				float4 staticSwitch32_g11811 = staticSwitch29_g11811;
				#endif
				#ifdef _ENABLERAINBOW_ON
				float2 temp_output_42_0_g11816 = temp_output_41_0_g11811;
				float linValue16_g11817 = tex2D( _UberNoiseTexture, ( temp_output_42_0_g11816 * _RainbowNoiseScale ) ).r;
				float localMyCustomExpression16_g11817 = MyCustomExpression16_g11817( linValue16_g11817 );
				float3 hsvTorgb3_g11819 = HSVToRGB( float3(( ( ( distance( temp_output_42_0_g11816 , _RainbowCenter ) + ( localMyCustomExpression16_g11817 * _RainbowNoiseFactor ) ) * _RainbowDensity ) + ( _RainbowSpeed * temp_output_40_0_g11811 ) ),1.0,1.0) );
				float3 hsvTorgb36_g11816 = RGBToHSV( hsvTorgb3_g11819 );
				float3 hsvTorgb37_g11816 = HSVToRGB( float3(hsvTorgb36_g11816.x,_RainbowSaturation,( hsvTorgb36_g11816.z * _RainbowBrightness )) );
				float4 temp_output_1_0_g11816 = staticSwitch32_g11811;
				float4 break2_g11818 = temp_output_1_0_g11816;
				float saferPower24_g11816 = abs( ( ( break2_g11818.x + break2_g11818.x + break2_g11818.y + break2_g11818.y + break2_g11818.y + break2_g11818.z ) / 6.0 ) );
				float4 appendResult29_g11816 = (float4(( ( hsvTorgb37_g11816 * pow( saferPower24_g11816 , max( _RainbowContrast , 0.001 ) ) * _RainbowFade ) + (temp_output_1_0_g11816).rgb ) , temp_output_1_0_g11816.a));
				float4 staticSwitch34_g11811 = appendResult29_g11816;
				#else
				float4 staticSwitch34_g11811 = staticSwitch32_g11811;
				#endif
				#ifdef _ENABLESHINE_ON
				float4 temp_output_1_0_g11812 = staticSwitch34_g11811;
				float3 temp_output_57_0_g11812 = (temp_output_1_0_g11812).rgb;
				float4 break2_g11814 = temp_output_1_0_g11812;
				float3 temp_cast_69 = (( ( break2_g11814.x + break2_g11814.x + break2_g11814.y + break2_g11814.y + break2_g11814.y + break2_g11814.z ) / 6.0 )).xxx;
				float3 lerpResult92_g11812 = lerp( temp_cast_69 , temp_output_57_0_g11812 , _ShineSaturation);
				float3 temp_cast_70 = (max( max( _ShineContrast , 0.001 ) , 0.001 )).xxx;
				float3 rotatedValue69_g11812 = RotateAroundAxis( float3( 0,0,0 ), float3( ( _ShineFrequency * temp_output_41_0_g11811 ) ,  0.0 ), float3( 0,0,1 ), ( ( _ShineRotation / 180.0 ) * PI ) );
				float temp_output_103_0_g11812 = ( _ShineFrequency * _ShineWidth );
				float clampResult80_g11812 = clamp( ( ( ( sin( ( rotatedValue69_g11812.x - ( temp_output_40_0_g11811 * _ShineSpeed * _ShineFrequency ) ) ) - ( 1.0 - temp_output_103_0_g11812 ) ) / temp_output_103_0_g11812 ) * _ShineSmooth ) , 0.0 , 1.0 );
				float2 uv_ShineMask = IN.ase_texcoord.xy * _ShineMask_ST.xy + _ShineMask_ST.zw;
				float4 tex2DNode3_g11813 = tex2D( _ShineMask, uv_ShineMask );
				#ifdef _SHINEMASKTOGGLE_ON
				float staticSwitch98_g11812 = ( _ShineFade * ( tex2DNode3_g11813.r * tex2DNode3_g11813.a ) );
				#else
				float staticSwitch98_g11812 = _ShineFade;
				#endif
				float4 appendResult8_g11812 = (float4(( temp_output_57_0_g11812 + ( ( pow( lerpResult92_g11812 , temp_cast_70 ) * (_ShineColor).rgb ) * clampResult80_g11812 * staticSwitch98_g11812 ) ) , (temp_output_1_0_g11812).a));
				float4 staticSwitch36_g11811 = appendResult8_g11812;
				#else
				float4 staticSwitch36_g11811 = staticSwitch34_g11811;
				#endif
				#ifdef _ENABLEPOISON_ON
				float temp_output_41_0_g11847 = temp_output_40_0_g11811;
				float linValue16_g11849 = tex2D( _UberNoiseTexture, ( ( ( temp_output_41_0_g11847 * _PoisonNoiseSpeed ) + temp_output_41_0_g11811 ) * _PoisonNoiseScale ) ).r;
				float localMyCustomExpression16_g11849 = MyCustomExpression16_g11849( linValue16_g11849 );
				float3 temp_output_24_0_g11847 = (_PoisonColor).rgb;
				float4 temp_output_1_0_g11847 = staticSwitch36_g11811;
				float3 temp_output_28_0_g11847 = (temp_output_1_0_g11847).rgb;
				float4 break2_g11848 = float4( temp_output_28_0_g11847 , 0.0 );
				float3 lerpResult32_g11847 = lerp( temp_output_28_0_g11847 , ( temp_output_24_0_g11847 * ( ( break2_g11848.x + break2_g11848.x + break2_g11848.y + break2_g11848.y + break2_g11848.y + break2_g11848.z ) / 6.0 ) ) , ( _PoisonFade * _PoisonRecolorFactor ));
				float4 appendResult27_g11847 = (float4(( ( max( pow( abs( ( ( ( localMyCustomExpression16_g11849 + ( temp_output_41_0_g11847 * _PoisonShiftSpeed ) ) % 1.0 ) + -0.5 ) ) , max( _PoisonDensity , 0.001 ) ) , 0.0 ) * temp_output_24_0_g11847 * _PoisonFade * _PoisonNoiseBrightness ) + lerpResult32_g11847 ) , temp_output_1_0_g11847.a));
				float4 staticSwitch39_g11811 = appendResult27_g11847;
				#else
				float4 staticSwitch39_g11811 = staticSwitch36_g11811;
				#endif
				float4 temp_output_10_0_g11851 = staticSwitch39_g11811;
				#ifdef _ENABLEENCHANTED_ON
				float3 temp_output_12_0_g11851 = (temp_output_10_0_g11851).rgb;
				float2 temp_output_2_0_g11851 = temp_output_41_0_g11811;
				float temp_output_1_0_g11851 = temp_output_40_0_g11811;
				float2 temp_output_6_0_g11851 = ( temp_output_1_0_g11851 * _EnchantedSpeed );
				float linValue16_g11854 = tex2D( _UberNoiseTexture, ( ( ( temp_output_2_0_g11851 - ( ( temp_output_6_0_g11851 + float2( 1.234,5.6789 ) ) * float2( 0.95,1.05 ) ) ) * _EnchantedScale ) * float2( 1,1 ) ) ).r;
				float localMyCustomExpression16_g11854 = MyCustomExpression16_g11854( linValue16_g11854 );
				float linValue16_g11852 = tex2D( _UberNoiseTexture, ( ( ( temp_output_6_0_g11851 + temp_output_2_0_g11851 ) * _EnchantedScale ) * float2( 1,1 ) ) ).r;
				float localMyCustomExpression16_g11852 = MyCustomExpression16_g11852( linValue16_g11852 );
				float temp_output_36_0_g11851 = ( localMyCustomExpression16_g11854 + localMyCustomExpression16_g11852 );
				float temp_output_43_0_g11851 = ( temp_output_36_0_g11851 * 0.5 );
				float3 lerpResult42_g11851 = lerp( (_EnchantedLowColor).rgb , (_EnchantedHighColor).rgb , temp_output_43_0_g11851);
				float3 hsvTorgb53_g11851 = HSVToRGB( float3(( ( temp_output_43_0_g11851 * _EnchantedRainbowDensity ) + ( _EnchantedRainbowSpeed * temp_output_1_0_g11851 ) ),_EnchantedRainbowSaturation,1.0) );
				#ifdef _ENCHANTEDRAINBOWTOGGLE_ON
				float3 staticSwitch50_g11851 = hsvTorgb53_g11851;
				#else
				float3 staticSwitch50_g11851 = lerpResult42_g11851;
				#endif
				float4 break2_g11853 = temp_output_10_0_g11851;
				float3 temp_output_40_0_g11851 = ( staticSwitch50_g11851 * pow( ( ( break2_g11853.x + break2_g11853.x + break2_g11853.y + break2_g11853.y + break2_g11853.y + break2_g11853.z ) / 6.0 ) , max( _EnchantedContrast , 0.001 ) ) * _EnchantedBrightness );
				float temp_output_45_0_g11851 = ( max( ( temp_output_36_0_g11851 - _EnchantedReduce ) , 0.0 ) * _EnchantedFade );
				float3 lerpResult44_g11851 = lerp( temp_output_12_0_g11851 , temp_output_40_0_g11851 , temp_output_45_0_g11851);
				#ifdef _ENCHANTEDLERPTOGGLE_ON
				float3 staticSwitch47_g11851 = lerpResult44_g11851;
				#else
				float3 staticSwitch47_g11851 = ( temp_output_12_0_g11851 + ( temp_output_40_0_g11851 * temp_output_45_0_g11851 ) );
				#endif
				float4 appendResult19_g11851 = (float4(staticSwitch47_g11851 , temp_output_10_0_g11851.a));
				float4 staticSwitch11_g11851 = appendResult19_g11851;
				#else
				float4 staticSwitch11_g11851 = temp_output_10_0_g11851;
				#endif
				float4 temp_output_1_0_g11856 = staticSwitch11_g11851;
				#ifdef _ENABLESHIFTING_ON
				float4 break5_g11856 = temp_output_1_0_g11856;
				float3 appendResult32_g11856 = (float3(break5_g11856.r , break5_g11856.g , break5_g11856.b));
				float4 break2_g11857 = temp_output_1_0_g11856;
				float temp_output_4_0_g11856 = ( ( break2_g11857.x + break2_g11857.x + break2_g11857.y + break2_g11857.y + break2_g11857.y + break2_g11857.z ) / 6.0 );
				float temp_output_11_0_g11856 = ( ( ( temp_output_4_0_g11856 + ( temp_output_40_0_g11811 * _ShiftingSpeed ) ) * _ShiftingDensity ) % 1.0 );
				float3 lerpResult20_g11856 = lerp( (_ShiftingColorA).rgb , (_ShiftingColorB).rgb , ( abs( ( temp_output_11_0_g11856 - 0.5 ) ) * 2.0 ));
				float3 hsvTorgb12_g11856 = HSVToRGB( float3(temp_output_11_0_g11856,_ShiftingSaturation,_ShiftingBrightness) );
				#ifdef _SHIFTINGRAINBOWTOGGLE_ON
				float3 staticSwitch26_g11856 = hsvTorgb12_g11856;
				#else
				float3 staticSwitch26_g11856 = ( lerpResult20_g11856 * _ShiftingBrightness );
				#endif
				float3 lerpResult31_g11856 = lerp( appendResult32_g11856 , ( staticSwitch26_g11856 * pow( temp_output_4_0_g11856 , max( _ShiftingContrast , 0.001 ) ) ) , _ShiftingFade);
				float4 appendResult6_g11856 = (float4(lerpResult31_g11856 , break5_g11856.a));
				float4 staticSwitch33_g11856 = appendResult6_g11856;
				#else
				float4 staticSwitch33_g11856 = temp_output_1_0_g11856;
				#endif
				float4 temp_output_5_0_g11863 = staticSwitch33_g11856;
				#ifdef _ENABLETEXTURELAYER1_ON
				float4 break6_g11863 = temp_output_5_0_g11863;
				float3 appendResult11_g11863 = (float3(break6_g11863.r , break6_g11863.g , break6_g11863.b));
				float temp_output_27_0_g11863 = temp_output_40_0_g11811;
				#ifdef _TEXTURELAYER1SCROLLTOGGLE_ON
				float2 staticSwitch73_g11863 = ( _TextureLayer1Offset + ( _TextureLayer1ScrollSpeed * temp_output_27_0_g11863 ) );
				#else
				float2 staticSwitch73_g11863 = _TextureLayer1Offset;
				#endif
				float2 temp_output_72_0_g11863 = ( ( _TextureLayer1Scale * temp_output_41_0_g11811 ) - staticSwitch73_g11863 );
				float2 temp_cast_79 = (_TextureLayer1EdgeClip).xx;
				float2 UV10_g11864 = ( ( ( ( ( ( ( ( ( temp_output_72_0_g11863 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) ) - float2( 0.5,0.5 ) ) * ( float2( 1,1 ) - temp_cast_79 ) ) + float2( 0.5,0.5 ) ) % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				int Width10_g11864 = _TextureLayer1Columns;
				int Height10_g11864 = _TextureLayer1Rows;
				float Tile10_g11864 = round( ( ( _TextureLayer1Speed * temp_output_27_0_g11863 ) + _TextureLayer1StartFrame ) );
				float2 Invert10_g11864 = float2( 0,1 );
				float2 localMyCustomExpression10_g11864 = MyCustomExpression10_g11864( UV10_g11864 , Width10_g11864 , Height10_g11864 , Tile10_g11864 , Invert10_g11864 );
				#ifdef _TEXTURELAYER1SHEETTOGGLE_ON
				float2 staticSwitch20_g11863 = localMyCustomExpression10_g11864;
				#else
				float2 staticSwitch20_g11863 = temp_output_72_0_g11863;
				#endif
				float4 tex2DNode3_g11863 = tex2D( _TextureLayer1Texture, staticSwitch20_g11863 );
				float3 appendResult13_g11863 = (float3(tex2DNode3_g11863.r , tex2DNode3_g11863.g , tex2DNode3_g11863.b));
				float3 appendResult18_g11863 = (float3(_TextureLayer1Color.r , _TextureLayer1Color.g , _TextureLayer1Color.b));
				float3 temp_output_16_0_g11863 = ( appendResult13_g11863 * appendResult18_g11863 );
				float4 break2_g11865 = temp_output_5_0_g11863;
				#ifdef _TEXTURELAYER1CONTRASTTOGGLE_ON
				float3 staticSwitch80_g11863 = ( pow( ( ( break2_g11865.x + break2_g11865.x + break2_g11865.y + break2_g11865.y + break2_g11865.y + break2_g11865.z ) / 6.0 ) , max( _TextureLayer1Contrast , 0.001 ) ) * temp_output_16_0_g11863 );
				#else
				float3 staticSwitch80_g11863 = temp_output_16_0_g11863;
				#endif
				float3 lerpResult12_g11863 = lerp( appendResult11_g11863 , staticSwitch80_g11863 , ( tex2DNode3_g11863.a * _TextureLayer1Fade ));
				float4 appendResult14_g11863 = (float4(lerpResult12_g11863 , break6_g11863.a));
				float4 staticSwitch15_g11863 = appendResult14_g11863;
				#else
				float4 staticSwitch15_g11863 = temp_output_5_0_g11863;
				#endif
				float4 temp_output_5_0_g11859 = staticSwitch15_g11863;
				#ifdef _ENABLETEXTURELAYER2_ON
				float4 break6_g11859 = temp_output_5_0_g11859;
				float3 appendResult11_g11859 = (float3(break6_g11859.r , break6_g11859.g , break6_g11859.b));
				float temp_output_27_0_g11859 = temp_output_40_0_g11811;
				#ifdef _TEXTURELAYER2SCROLLTOGGLE_ON
				float2 staticSwitch73_g11859 = ( _TextureLayer2Offset + ( _TextureLayer2ScrollSpeed * temp_output_27_0_g11859 ) );
				#else
				float2 staticSwitch73_g11859 = _TextureLayer2Offset;
				#endif
				float2 temp_output_72_0_g11859 = ( ( _TextureLayer2Scale * temp_output_41_0_g11811 ) - staticSwitch73_g11859 );
				float2 temp_cast_82 = (_TextureLayer2EdgeClip).xx;
				float2 UV10_g11860 = ( ( ( ( ( ( ( ( ( temp_output_72_0_g11859 % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) ) - float2( 0.5,0.5 ) ) * ( float2( 1,1 ) - temp_cast_82 ) ) + float2( 0.5,0.5 ) ) % float2( 1,1 ) ) + float2( 1,1 ) ) % float2( 1,1 ) );
				int Width10_g11860 = _TextureLayer2Columns;
				int Height10_g11860 = _TextureLayer2Rows;
				float Tile10_g11860 = round( ( ( _TextureLayer2Speed * temp_output_27_0_g11859 ) + _TextureLayer2StartFrame ) );
				float2 Invert10_g11860 = float2( 0,1 );
				float2 localMyCustomExpression10_g11860 = MyCustomExpression10_g11860( UV10_g11860 , Width10_g11860 , Height10_g11860 , Tile10_g11860 , Invert10_g11860 );
				#ifdef _TEXTURELAYER2SHEETTOGGLE_ON
				float2 staticSwitch20_g11859 = localMyCustomExpression10_g11860;
				#else
				float2 staticSwitch20_g11859 = temp_output_72_0_g11859;
				#endif
				float4 tex2DNode3_g11859 = tex2D( _TextureLayer2Texture, staticSwitch20_g11859 );
				float3 appendResult13_g11859 = (float3(tex2DNode3_g11859.r , tex2DNode3_g11859.g , tex2DNode3_g11859.b));
				float3 appendResult18_g11859 = (float3(_TextureLayer2Color.r , _TextureLayer2Color.g , _TextureLayer2Color.b));
				float3 temp_output_16_0_g11859 = ( appendResult13_g11859 * appendResult18_g11859 );
				float4 break2_g11861 = temp_output_5_0_g11859;
				#ifdef _TEXTURELAYER2CONTRASTTOGGLE_ON
				float3 staticSwitch84_g11859 = ( pow( ( ( break2_g11861.x + break2_g11861.x + break2_g11861.y + break2_g11861.y + break2_g11861.y + break2_g11861.z ) / 6.0 ) , max( _TextureLayer2Contrast , 0.001 ) ) * temp_output_16_0_g11859 );
				#else
				float3 staticSwitch84_g11859 = temp_output_16_0_g11859;
				#endif
				float3 lerpResult12_g11859 = lerp( appendResult11_g11859 , staticSwitch84_g11859 , ( tex2DNode3_g11859.a * _TextureLayer2Fade ));
				float4 appendResult14_g11859 = (float4(lerpResult12_g11859 , break6_g11859.a));
				float4 staticSwitch15_g11859 = appendResult14_g11859;
				#else
				float4 staticSwitch15_g11859 = temp_output_5_0_g11859;
				#endif
				float4 temp_output_473_0 = staticSwitch15_g11859;
				#ifdef _ENABLEFULLDISTORTION_ON
				float4 break4_g11867 = temp_output_473_0;
				float fullDistortionAlpha164 = _FullDistortionFade;
				float4 appendResult5_g11867 = (float4(break4_g11867.r , break4_g11867.g , break4_g11867.b , ( break4_g11867.a * fullDistortionAlpha164 )));
				float4 staticSwitch77 = appendResult5_g11867;
				#else
				float4 staticSwitch77 = temp_output_473_0;
				#endif
				#ifdef _ENABLEDIRECTIONALDISTORTION_ON
				float4 break4_g11868 = staticSwitch77;
				float directionalDistortionAlpha167 = (( _DirectionalDistortionInvert )?( ( 1.0 - clampResult154_g11669 ) ):( clampResult154_g11669 ));
				float4 appendResult5_g11868 = (float4(break4_g11868.r , break4_g11868.g , break4_g11868.b , ( break4_g11868.a * directionalDistortionAlpha167 )));
				float4 staticSwitch75 = appendResult5_g11868;
				#else
				float4 staticSwitch75 = staticSwitch77;
				#endif
				float4 temp_output_1_0_g11869 = staticSwitch75;
				float4 temp_output_1_0_g11870 = temp_output_1_0_g11869;
#ifdef _ENABLEFULLALPHADISSOLVE_ON
				float temp_output_53_0_g11870 = max( _FullAlphaDissolveWidth , 0.001 );
				#endif
				float2 temp_output_18_0_g11869 = shaderPosition235;
				#ifdef _ENABLEFULLALPHADISSOLVE_ON
				float linValue16_g11871 = tex2D( _UberNoiseTexture, ( temp_output_18_0_g11869 * _FullAlphaDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11871 = MyCustomExpression16_g11871( linValue16_g11871 );
				float clampResult17_g11870 = clamp( ( ( ( _FullAlphaDissolveFade * ( 1.0 + temp_output_53_0_g11870 ) ) - localMyCustomExpression16_g11871 ) / temp_output_53_0_g11870 ) , 0.0 , 1.0 );
				float4 appendResult3_g11870 = (float4((temp_output_1_0_g11870).rgb , ( temp_output_1_0_g11870.a * clampResult17_g11870 )));
				float4 staticSwitch3_g11869 = appendResult3_g11870;
				#else
				float4 staticSwitch3_g11869 = temp_output_1_0_g11869;
				#endif
				#ifdef _ENABLEFULLGLOWDISSOLVE_ON
				float linValue16_g11879 = tex2D( _UberNoiseTexture, ( temp_output_18_0_g11869 * _FullGlowDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11879 = MyCustomExpression16_g11879( linValue16_g11879 );
				float temp_output_5_0_g11878 = localMyCustomExpression16_g11879;
				float temp_output_61_0_g11878 = step( temp_output_5_0_g11878 , _FullGlowDissolveFade );
				float temp_output_53_0_g11878 = max( ( _FullGlowDissolveFade * _FullGlowDissolveWidth ) , 0.001 );
				float4 temp_output_1_0_g11878 = staticSwitch3_g11869;
				float4 appendResult3_g11878 = (float4(( ( (_FullGlowDissolveEdgeColor).rgb * ( temp_output_61_0_g11878 - step( temp_output_5_0_g11878 , ( ( _FullGlowDissolveFade * ( 1.01 + temp_output_53_0_g11878 ) ) - temp_output_53_0_g11878 ) ) ) ) + (temp_output_1_0_g11878).rgb ) , ( temp_output_1_0_g11878.a * temp_output_61_0_g11878 )));
				float4 staticSwitch5_g11869 = appendResult3_g11878;
				#else
				float4 staticSwitch5_g11869 = staticSwitch3_g11869;
				#endif
				#ifdef _ENABLESOURCEALPHADISSOLVE_ON
				float4 temp_output_1_0_g11880 = staticSwitch5_g11869;
				float2 temp_output_76_0_g11880 = temp_output_18_0_g11869;
				float linValue16_g11881 = tex2D( _UberNoiseTexture, ( temp_output_76_0_g11880 * _SourceAlphaDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11881 = MyCustomExpression16_g11881( linValue16_g11881 );
				float clampResult17_g11880 = clamp( ( ( _SourceAlphaDissolveFade - ( distance( _SourceAlphaDissolvePosition , temp_output_76_0_g11880 ) + ( localMyCustomExpression16_g11881 * _SourceAlphaDissolveNoiseFactor ) ) ) / max( _SourceAlphaDissolveWidth , 0.001 ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11880 = (float4((temp_output_1_0_g11880).rgb , ( temp_output_1_0_g11880.a * (( _SourceAlphaDissolveInvert )?( ( 1.0 - clampResult17_g11880 ) ):( clampResult17_g11880 )) )));
				float4 staticSwitch8_g11869 = appendResult3_g11880;
				#else
				float4 staticSwitch8_g11869 = staticSwitch5_g11869;
				#endif
				#ifdef _ENABLESOURCEGLOWDISSOLVE_ON
				float2 temp_output_90_0_g11876 = temp_output_18_0_g11869;
				float linValue16_g11877 = tex2D( _UberNoiseTexture, ( temp_output_90_0_g11876 * _SourceGlowDissolveNoiseScale ) ).r;
				float localMyCustomExpression16_g11877 = MyCustomExpression16_g11877( linValue16_g11877 );
				float temp_output_65_0_g11876 = ( distance( _SourceGlowDissolvePosition , temp_output_90_0_g11876 ) + ( localMyCustomExpression16_g11877 * _SourceGlowDissolveNoiseFactor ) );
				float temp_output_75_0_g11876 = step( temp_output_65_0_g11876 , _SourceGlowDissolveFade );
				float temp_output_76_0_g11876 = step( temp_output_65_0_g11876 , ( _SourceGlowDissolveFade - max( _SourceGlowDissolveWidth , 0.001 ) ) );
				float4 temp_output_1_0_g11876 = staticSwitch8_g11869;
				float4 appendResult3_g11876 = (float4(( ( max( ( temp_output_75_0_g11876 - temp_output_76_0_g11876 ) , 0.0 ) * (_SourceGlowDissolveEdgeColor).rgb ) + (temp_output_1_0_g11876).rgb ) , ( temp_output_1_0_g11876.a * (( _SourceGlowDissolveInvert )?( ( 1.0 - temp_output_76_0_g11876 ) ):( temp_output_75_0_g11876 )) )));
				float4 staticSwitch9_g11869 = appendResult3_g11876;
				#else
				float4 staticSwitch9_g11869 = staticSwitch8_g11869;
				#endif
				#ifdef _ENABLEDIRECTIONALALPHAFADE_ON
				float4 temp_output_1_0_g11872 = staticSwitch9_g11869;
				float2 temp_output_161_0_g11872 = temp_output_18_0_g11869;
				float3 rotatedValue136_g11872 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_161_0_g11872 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalAlphaFadeRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11872 = rotatedValue136_g11872;
				float linValue16_g11873 = tex2D( _UberNoiseTexture, ( temp_output_161_0_g11872 * _DirectionalAlphaFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11873 = MyCustomExpression16_g11873( linValue16_g11873 );
				float clampResult154_g11872 = clamp( ( ( break130_g11872.x + break130_g11872.y + _DirectionalAlphaFadeFade + ( localMyCustomExpression16_g11873 * _DirectionalAlphaFadeNoiseFactor ) ) / max( _DirectionalAlphaFadeWidth , 0.001 ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11872 = (float4((temp_output_1_0_g11872).rgb , ( temp_output_1_0_g11872.a * (( _DirectionalAlphaFadeInvert )?( ( 1.0 - clampResult154_g11872 ) ):( clampResult154_g11872 )) )));
				float4 staticSwitch11_g11869 = appendResult3_g11872;
				#else
				float4 staticSwitch11_g11869 = staticSwitch9_g11869;
				#endif
				#ifdef _ENABLEDIRECTIONALGLOWFADE_ON
				float2 temp_output_171_0_g11874 = temp_output_18_0_g11869;
				float3 rotatedValue136_g11874 = RotateAroundAxis( float3( 0,0,0 ), float3( temp_output_171_0_g11874 ,  0.0 ), float3( 0,0,1 ), ( ( ( _DirectionalGlowFadeRotation / 180.0 ) + -0.25 ) * PI ) );
				float3 break130_g11874 = rotatedValue136_g11874;
				float linValue16_g11875 = tex2D( _UberNoiseTexture, ( temp_output_171_0_g11874 * _DirectionalGlowFadeNoiseScale ) ).r;
				float localMyCustomExpression16_g11875 = MyCustomExpression16_g11875( linValue16_g11875 );
				float temp_output_168_0_g11874 = max( ( ( break130_g11874.x + break130_g11874.y + _DirectionalGlowFadeFade + ( localMyCustomExpression16_g11875 * _DirectionalGlowFadeNoiseFactor ) ) / max( _DirectionalGlowFadeWidth , 0.001 ) ) , 0.0 );
				float temp_output_161_0_g11874 = step( 0.1 , (( _DirectionalGlowFadeInvert )?( ( 1.0 - temp_output_168_0_g11874 ) ):( temp_output_168_0_g11874 )) );
				float4 temp_output_1_0_g11874 = staticSwitch11_g11869;
				float clampResult154_g11874 = clamp( temp_output_161_0_g11874 , 0.0 , 1.0 );
				float4 appendResult3_g11874 = (float4(( ( (_DirectionalGlowFadeEdgeColor).rgb * ( temp_output_161_0_g11874 - step( 1.0 , (( _DirectionalGlowFadeInvert )?( ( 1.0 - temp_output_168_0_g11874 ) ):( temp_output_168_0_g11874 )) ) ) ) + (temp_output_1_0_g11874).rgb ) , ( temp_output_1_0_g11874.a * clampResult154_g11874 )));
				float4 staticSwitch15_g11869 = appendResult3_g11874;
				#else
				float4 staticSwitch15_g11869 = staticSwitch11_g11869;
				#endif
				#ifdef _ENABLEHALFTONE_ON
				float4 temp_output_1_0_g11882 = staticSwitch15_g11869;
				float2 temp_output_126_0_g11882 = temp_output_18_0_g11869;
				float temp_output_121_0_g11882 = max( ( ( _HalftoneFade - distance( _HalftonePosition , temp_output_126_0_g11882 ) ) / max( 0.01 , _HalftoneFadeWidth ) ) , 0.0 );
				float2 appendResult11_g11883 = (float2(temp_output_121_0_g11882 , temp_output_121_0_g11882));
				float temp_output_17_0_g11883 = length( ( (( ( abs( temp_output_126_0_g11882 ) * _HalftoneTiling ) % float2( 1,1 ) )*2.0 + -1.0) / appendResult11_g11883 ) );
				float clampResult17_g11882 = clamp( saturate( ( ( 1.0 - temp_output_17_0_g11883 ) / fwidth( temp_output_17_0_g11883 ) ) ) , 0.0 , 1.0 );
				float4 appendResult3_g11882 = (float4((temp_output_1_0_g11882).rgb , ( temp_output_1_0_g11882.a * (( _HalftoneInvert )?( ( 1.0 - clampResult17_g11882 ) ):( clampResult17_g11882 )) )));
				float4 staticSwitch13_g11869 = appendResult3_g11882;
				#else
				float4 staticSwitch13_g11869 = staticSwitch15_g11869;
				#endif
				#ifdef _ENABLEADDCOLOR_ON
				float3 temp_output_3_0_g11885 = (_AddColorColor).rgb;
				float2 uv_AddColorMask = IN.ase_texcoord.xy * _AddColorMask_ST.xy + _AddColorMask_ST.zw;
				float4 tex2DNode19_g11885 = tex2D( _AddColorMask, uv_AddColorMask );
				#ifdef _ADDCOLORMASKTOGGLE_ON
				float3 staticSwitch16_g11885 = ( temp_output_3_0_g11885 * ( (tex2DNode19_g11885).rgb * tex2DNode19_g11885.a ) );
				#else
				float3 staticSwitch16_g11885 = temp_output_3_0_g11885;
				#endif
				float4 temp_output_1_0_g11885 = staticSwitch13_g11869;
				float4 break2_g11887 = temp_output_1_0_g11885;
				#ifdef _ADDCOLORCONTRASTTOGGLE_ON
				float3 staticSwitch17_g11885 = ( staticSwitch16_g11885 * pow( ( ( break2_g11887.x + break2_g11887.x + break2_g11887.y + break2_g11887.y + break2_g11887.y + break2_g11887.z ) / 6.0 ) , max( _AddColorContrast , 0.001 ) ) );
				#else
				float3 staticSwitch17_g11885 = staticSwitch16_g11885;
				#endif
				float4 appendResult6_g11885 = (float4(( ( staticSwitch17_g11885 * _AddColorFade ) + (temp_output_1_0_g11885).rgb ) , temp_output_1_0_g11885.a));
				float4 staticSwitch5_g11884 = appendResult6_g11885;
				#else
				float4 staticSwitch5_g11884 = staticSwitch13_g11869;
				#endif
				#ifdef _ENABLEALPHATINT_ON
				float4 temp_output_1_0_g11888 = staticSwitch5_g11884;
				float3 lerpResult4_g11888 = lerp( (temp_output_1_0_g11888).rgb , (_AlphaTintColor).rgb , ( ( 1.0 - temp_output_1_0_g11888.a ) * step( _AlphaTintMinAlpha , temp_output_1_0_g11888.a ) * _AlphaTintFade ));
				float4 appendResult13_g11888 = (float4(lerpResult4_g11888 , temp_output_1_0_g11888.a));
				float4 staticSwitch11_g11884 = appendResult13_g11888;
				#else
				float4 staticSwitch11_g11884 = staticSwitch5_g11884;
				#endif
				#ifdef _ENABLESTRONGTINT_ON
				float4 temp_output_1_0_g11889 = staticSwitch11_g11884;
				float3 temp_output_6_0_g11889 = (_StrongTintTint).rgb;
				float2 uv_StrongTintMask = IN.ase_texcoord.xy * _StrongTintMask_ST.xy + _StrongTintMask_ST.zw;
				float4 tex2DNode23_g11889 = tex2D( _StrongTintMask, uv_StrongTintMask );
				#ifdef _STRONGTINTMASKTOGGLE_ON
				float3 staticSwitch21_g11889 = ( temp_output_6_0_g11889 * ( (tex2DNode23_g11889).rgb * tex2DNode23_g11889.a ) );
				#else
				float3 staticSwitch21_g11889 = temp_output_6_0_g11889;
				#endif
				float4 break2_g11891 = temp_output_1_0_g11889;
				#ifdef _STRONGTINTCONTRASTTOGGLE_ON
				float3 staticSwitch22_g11889 = ( pow( ( ( break2_g11891.x + break2_g11891.x + break2_g11891.y + break2_g11891.y + break2_g11891.y + break2_g11891.z ) / 6.0 ) , max( _StrongTintContrast , 0.001 ) ) * staticSwitch21_g11889 );
				#else
				float3 staticSwitch22_g11889 = staticSwitch21_g11889;
				#endif
				float3 lerpResult7_g11889 = lerp( (temp_output_1_0_g11889).rgb , staticSwitch22_g11889 , _StrongTintFade);
				float4 appendResult9_g11889 = (float4(lerpResult7_g11889 , (temp_output_1_0_g11889).a));
				float4 staticSwitch7_g11884 = appendResult9_g11889;
				#else
				float4 staticSwitch7_g11884 = staticSwitch11_g11884;
				#endif
				float4 temp_output_2_0_g11892 = staticSwitch7_g11884;
				#ifdef _ENABLESHADOW_ON
				float4 break4_g11894 = temp_output_2_0_g11892;
				float3 appendResult5_g11894 = (float3(break4_g11894.r , break4_g11894.g , break4_g11894.b));
				float2 appendResult2_g11893 = (float2(_MainTex_TexelSize.z , _MainTex_TexelSize.w));
				float4 appendResult85_g11892 = (float4(_ShadowColor.r , _ShadowColor.g , _ShadowColor.b , ( _ShadowFade * tex2D( _MainTex, ( finalUV146 - ( ( 100.0 / appendResult2_g11893 ) * _ShadowOffset ) ) ).a )));
				float4 break6_g11894 = appendResult85_g11892;
				float3 appendResult7_g11894 = (float3(break6_g11894.r , break6_g11894.g , break6_g11894.b));
				float temp_output_11_0_g11894 = ( ( 1.0 - break4_g11894.a ) * break6_g11894.a );
				float temp_output_32_0_g11894 = ( break4_g11894.a + temp_output_11_0_g11894 );
				float4 appendResult18_g11894 = (float4(( ( ( appendResult5_g11894 * break4_g11894.a ) + ( appendResult7_g11894 * temp_output_11_0_g11894 ) ) * ( 1.0 / max( temp_output_32_0_g11894 , 0.01 ) ) ) , temp_output_32_0_g11894));
				float4 staticSwitch82_g11892 = appendResult18_g11894;
				#else
				float4 staticSwitch82_g11892 = temp_output_2_0_g11892;
				#endif
				float4 break4_g11895 = staticSwitch82_g11892;
				#ifdef _ENABLECUSTOMFADE_ON
				float staticSwitch8_g11753 = 1.0;
				#else
				float staticSwitch8_g11753 = IN.ase_color.a;
				#endif
				#ifdef _ENABLESMOKE_ON
				float staticSwitch9_g11753 = 1.0;
				#else
				float staticSwitch9_g11753 = staticSwitch8_g11753;
				#endif
				float customVertexAlpha193 = staticSwitch9_g11753;
				float4 appendResult5_g11895 = (float4(break4_g11895.r , break4_g11895.g , break4_g11895.b , ( break4_g11895.a * customVertexAlpha193 )));
				float4 temp_output_344_0 = appendResult5_g11895;
				float4 temp_output_1_0_g11896 = temp_output_344_0;
				float4 appendResult8_g11896 = (float4(( (temp_output_1_0_g11896).rgb * (IN.ase_color).rgb ) , temp_output_1_0_g11896.a));
				#ifdef _VERTEXTINTFIRST_ON
				float4 staticSwitch342 = temp_output_344_0;
				#else
				float4 staticSwitch342 = appendResult8_g11896;
				#endif
				float4 lerpResult125 = lerp( ( originalColor191 * IN.ase_color ) , staticSwitch342 , fullFade123);
				#if defined(_SHADERFADING_NONE)
				float4 staticSwitch143 = staticSwitch342;
				#elif defined(_SHADERFADING_FULL)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_MASK)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_DISSOLVE)
				float4 staticSwitch143 = lerpResult125;
				#elif defined(_SHADERFADING_SPREAD)
				float4 staticSwitch143 = lerpResult125;
				#else
				float4 staticSwitch143 = staticSwitch342;
				#endif
				float4 temp_output_7_0_g11903 = staticSwitch143;
				#ifdef _BAKEDMATERIAL_ON
				float4 appendResult2_g11903 = (float4(( (temp_output_7_0_g11903).rgb / max( temp_output_7_0_g11903.a , 1E-05 ) ) , temp_output_7_0_g11903.a));
				float4 staticSwitch6_g11903 = appendResult2_g11903;
				#else
				float4 staticSwitch6_g11903 = temp_output_7_0_g11903;
				#endif
				
				float4 Color = staticSwitch6_g11903;
				half4 outColor = _SelectionID;
				return outColor;
			}

            ENDHLSL
        }
		
	}
	CustomEditor "SpriteShadersUltimate.SSUShaderGUI"
	Fallback "Hidden/InternalErrorShader"
	
}
/*ASEBEGIN
Version=19108
Node;AmplifyShaderEditor.TexturePropertyNode;496;1090.845,979.2128;Inherit;True;Property;_MainTex;MainTex;0;0;Create;True;0;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RelayNode;105;1425.709,1004.581;Inherit;False;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;157;1638.393,1000.485;Inherit;False;spriteTexture;-1;True;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;411;-3081.191,-3627.324;Inherit;False;157;spriteTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;363;-3105.9,-3835.053;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FunctionNode;483;-2758.191,-3709.324;Inherit;False;_ScreenTiling;604;;11656;f5939d1b891718b468aa402ddf2c75e0;0;2;1;FLOAT2;0,0;False;12;SAMPLER2D;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;482;-2458.77,-3622.03;Inherit;False;_WorldTiling;599;;11657;5075a3cd4854af640aa8d277732c8893;0;2;1;FLOAT2;0,0;False;12;SAMPLER2D;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;460;-2186.147,-3620.607;Inherit;False;originalUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexelSizeNode;438;1931.978,1144.547;Inherit;False;-1;1;0;SAMPLER2D;;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;432;2227.661,1200.654;Inherit;False;False;False;True;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;435;2252.064,903.7132;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector4Node;373;-2003.491,-3335.527;Inherit;False;Property;_SpriteSheetRect;Sprite Sheet Rect;9;0;Create;True;0;0;0;False;0;False;0,0,1,1;0,0,1,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FunctionNode;469;-1956.314,-3488.134;Inherit;False;_PixelArtUV_1;-1;;11658;0e4f4d9760e013e4ea49a4cc7c42c155;0;2;1;FLOAT2;0,0;False;2;SAMPLER2D;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;434;2534.932,1066.922;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;374;-1729.456,-3326.936;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;449;-1697.482,-3566.466;Inherit;False;Property;_Keyword1;Keyword 1;7;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Reference;427;True;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;375;-1731.491,-3207.527;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;376;-1493.884,-3326.405;Inherit;False;spriteRectMin;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;377;-1494.584,-3224.105;Inherit;False;spriteRectMax;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;450;-1448.75,-3526.053;Inherit;False;uvAfterPixelArt;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FloorOpNode;433;2745.127,1095.848;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;341;2716.61,1541.034;Inherit;False;ShaderTime;26;;11661;06a15e67904f217499045f361bad56e7;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;436;2945.127,1149.848;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;370;-1198.361,-3311.903;Inherit;False;Remap2D;-1;;11662;f79f855c0a5c94649b58f3d8127375ae;0;5;13;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;1,1;False;5;FLOAT2;0,0;False;6;FLOAT2;1,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;101;2238.417,1562.63;Inherit;True;Property;_UberNoiseTexture;Uber Noise Texture;37;0;Create;True;0;0;0;False;0;False;b8d18cd117976254d94a812a0bfc336e;b8d18cd117976254d94a812a0bfc336e;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.StaticSwitch;366;-905.3608,-3353.903;Inherit;False;Property;_SpriteSheetFix;Sprite Sheet Fix;8;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;441;3148.675,1260.326;Inherit;False;157;spriteTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.StaticSwitch;437;3093.581,1032.267;Inherit;False;Property;_PixelPerfectSpace;Pixel Perfect Space;6;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;237;2948.598,1594.428;Inherit;False;shaderTime;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;439;3389.449,1182.896;Inherit;False;ShaderSpace;13;;11663;be729ef05db9c224caec82a3516038dc;0;2;61;FLOAT2;0,0;False;3;SAMPLER2D;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;477;-582.6621,-3244.321;Inherit;False;237;shaderTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;475;-584.1226,-3343.809;Inherit;False;fixedUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RelayNode;99;2506.903,1635.005;Inherit;False;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;159;2660.32,1715.519;Inherit;False;uberNoiseTexture;-1;True;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.FunctionNode;365;-372.9774,-3275.82;Inherit;False;_UberInteractive;532;;11664;f8a4d7008519ad249b29e4a9381f963f;0;2;9;FLOAT;0;False;3;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;235;3662.626,1220.644;Inherit;False;shaderPosition;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;253;-334.9839,-3014.146;Inherit;False;235;shaderPosition;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RelayNode;84;-178.5299,-3184.649;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;184;-340.3444,-2931.6;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.FunctionNode;79;-47.4363,-2946.944;Inherit;False;_FullDistortion;466;;11666;62960fe27c1c398408207bb462ffd10e;0;3;195;FLOAT2;0,0;False;160;FLOAT2;0,0;False;194;SAMPLER2D;;False;2;FLOAT2;174;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;254;496.0161,-2937.146;Inherit;False;235;shaderPosition;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;188;476.926,-2831.78;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.StaticSwitch;83;355.5295,-3077.305;Inherit;False;Property;_EnableShine;Enable Shine;465;0;Create;True;0;0;0;False;0;False;1;0;0;True;;Toggle;2;Key0;Key1;Reference;77;True;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;81;747.3577,-2918.135;Inherit;False;_DirectionalDistortion;454;;11669;30e6ac39427ee11419083602d572972f;0;3;182;FLOAT2;0,0;False;160;FLOAT2;0,0;False;181;SAMPLER2D;;False;2;FLOAT2;174;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;243;665.8691,-664.6964;Inherit;False;235;shaderPosition;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;244;685.5539,-740.5018;Inherit;False;237;shaderTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;53;1165.579,-2580.498;Inherit;False;Property;_HologramFade;Hologram: Fade;199;0;Create;True;0;0;0;False;0;False;1;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;82;1064.56,-3052.917;Inherit;False;Property;_EnableShine;Enable Shine;453;0;Create;True;0;0;0;False;0;False;1;0;0;True;;Toggle;2;Key0;Key1;Reference;75;True;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;160;658.3505,-580.0461;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;255;1530.016,-2640.146;Inherit;False;237;shaderTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;173;1547.656,-2458.612;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.RelayNode;38;1602.103,-2721.81;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;174;1562.277,-2362.367;Inherit;False;157;spriteTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.FunctionNode;102;919.0109,-667.4209;Inherit;False;_GlitchPre;214;;11672;b8ad29d751d87bd4d9cbf14898be6163;0;3;19;FLOAT;0;False;18;FLOAT2;0,0;False;16;SAMPLER2D;;False;2;FLOAT;15;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;182;1537.252,-2554.561;Inherit;False;hologramFade;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;208;4401.642,1937.264;Inherit;False;Property;_FadingNoiseScale;Fading: Noise Scale;24;0;Create;True;0;0;0;False;0;False;0.2,0.2;0.2,0.2;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode;228;4626.359,2471.966;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;219;4515.66,1836.189;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.FunctionNode;52;1839.482,-2552.931;Inherit;False;_HologramUV;207;;11674;7c71b1b031ffcbe48805e17b94671163;0;5;77;FLOAT;0;False;55;FLOAT;0;False;76;SAMPLER2D;;False;37;FLOAT2;0,0;False;39;SAMPLER2D;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;261;4535.53,1746.504;Inherit;False;235;shaderPosition;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;122;3995.099,1279.389;Inherit;False;Property;_FadingFade;Fading: Fade;20;0;Create;True;0;0;0;False;0;False;1;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;152;1246.533,-755.1426;Inherit;False;glitchFade;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;154;1243.538,-600.6849;Inherit;False;glitchPosition;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;204;4801.255,1506.884;Inherit;True;Property;_FadingMask;Fading: Mask;25;0;Create;True;0;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RangedFloatNode;230;4630.415,2681.855;Inherit;False;Property;_FadingNoiseFactor;Fading: Noise Factor;23;0;Create;True;0;0;0;False;0;False;0.2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;210;4416.092,2114.768;Inherit;False;Property;_FadingWidth;Fading: Width;22;0;Create;True;0;0;0;False;0;False;0.3;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;229;4660.594,2553.942;Inherit;False;Property;_FadingPosition;Fading: Position;21;0;Create;True;0;0;0;False;0;False;0,0;0.2,0.2;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.StaticSwitch;59;2242.011,-2636.393;Inherit;False;Property;_EnableShine;Enable Shine;198;0;Create;True;0;0;0;False;0;False;1;0;0;True;;Toggle;2;Key0;Key1;Reference;56;True;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;292;5091.561,1459.408;Inherit;False;ShaderMasker;-1;;11712;3d25b55dbfdd24f48b9bd371bdde0e97;0;2;1;FLOAT;0;False;2;SAMPLER2D;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;256;2394.016,-2741.146;Inherit;False;237;shaderTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;231;4899.456,2440.854;Inherit;False;_UberSpreadFade;-1;;11713;777ca8ab10170fb48b24b7cd1c44f075;0;7;27;FLOAT2;0,0;False;22;FLOAT;0;False;18;SAMPLER2D;0;False;25;FLOAT2;0,0;False;23;FLOAT2;0,0;False;21;FLOAT;0;False;26;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;155;2400.558,-2511.178;Inherit;False;154;glitchPosition;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;175;2375.652,-2434.015;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;153;2413.1,-2367.982;Inherit;False;152;glitchFade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;223;4905.316,1828.167;Inherit;False;_UberDissolveFade;-1;;11715;cb957eb9b67f4f243aa8ba0547208263;0;5;21;FLOAT2;0,0;False;1;FLOAT;0;False;16;SAMPLER2D;0;False;18;FLOAT2;0,0;False;20;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;139;5964.391,1365.661;Inherit;False;Property;_ShaderFading;Shader Fading;19;0;Create;True;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;5;None;Full;Mask;Dissolve;Spread;Create;True;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;103;2715.721,-2556.586;Inherit;False;_GlitchUV;226;;11717;2addb21417fb5d745a5abfe02cbcd453;0;5;23;FLOAT;0;False;13;FLOAT2;0,0;False;22;SAMPLER2D;;False;3;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;259;2999.016,-2403.146;Inherit;False;237;shaderTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;123;6281.453,1414.289;Inherit;False;fullFade;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;176;2993.115,-2238.526;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.StaticSwitch;62;3040.934,-2600.272;Inherit;False;Property;_EnableShine;Enable Shine;213;0;Create;True;0;0;0;False;0;False;1;0;0;True;;Toggle;2;Key0;Key1;Reference;57;True;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;258;2972.016,-2323.146;Inherit;False;235;shaderPosition;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;177;3015.115,-2149.526;Inherit;False;157;spriteTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.FunctionNode;484;3382.41,-2373.518;Inherit;False;_UberTransformUV;471;;11719;894b1de51a5f4c74cbe7828262f1344b;0;5;25;FLOAT;0;False;26;FLOAT2;0,0;False;1;FLOAT2;0,0;False;18;SAMPLER2D;0;False;3;SAMPLER2D;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;131;3266.866,-2134.612;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;129;3305.944,-1988.403;Inherit;False;123;fullFade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;130;3651.881,-2106.533;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;145;3940.446,-2146.193;Inherit;False;Property;_UberFading;Uber Fading;19;0;Create;True;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;4;NONE;Key1;Key2;Key3;Reference;139;True;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleRemainderNode;486;4222.693,-2046.305;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;487;4449.801,-1972.639;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleRemainderNode;488;4664.06,-2056.074;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;1,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;485;4859.071,-2156.215;Inherit;False;Property;_TilingFix;Tiling Fix;10;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;381;5159.958,-1852.951;Inherit;False;377;spriteRectMax;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;380;5166.152,-1941.533;Inherit;False;376;spriteRectMin;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;378;5475.994,-2043.135;Inherit;False;Remap2D;-1;;11731;f79f855c0a5c94649b58f3d8127375ae;0;5;13;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;1,1;False;5;FLOAT2;0,0;False;6;FLOAT2;1,1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;382;5739.72,-1830.532;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMinOpNode;383;5870.72,-1934.532;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;463;6361.842,-2067.504;Inherit;False;460;originalUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;442;6299.205,-1884.911;Inherit;False;157;spriteTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.StaticSwitch;371;6107.095,-2156.984;Inherit;False;Property;_SpriteSheetFix1;Sprite Sheet Fix;8;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Reference;366;False;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;452;6307.805,-1976.09;Inherit;False;450;uvAfterPixelArt;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;470;6618.802,-2035.91;Inherit;False;_PixelArtUV_2;-1;;11732;4b65626ba2313ca40a96813b19044794;0;4;21;FLOAT2;0,0;False;17;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;SAMPLER2D;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;427;7006.263,-2158.381;Inherit;False;Property;_PixelPerfectUV;Pixel Perfect UV;7;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;146;7280.32,-2157.36;Inherit;False;finalUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;189;-2234.721,-201.4758;Inherit;False;157;spriteTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;147;-2226.428,-390.3136;Inherit;False;146;finalUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;471;-1990.589,-315.832;Inherit;False;_UberSample;570;;11733;1028d755b36e2b04da25c3b882a2e2ec;0;2;1;FLOAT2;0,0;False;2;SAMPLER2D;;False;1;COLOR;0
Node;AmplifyShaderEditor.VertexColorNode;358;-1921.027,-115.7738;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FunctionNode;356;-1686.027,-106.7738;Inherit;False;ColorMultiply;-1;;11752;1f51da7edd80c06488c56d28bc096dec;0;2;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.StaticSwitch;354;-1350.718,-125.6577;Inherit;False;Property;_Keyword0;Keyword 0;5;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Reference;342;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;179;-1036.103,27.02582;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;149;-977.8615,164.14;Inherit;False;146;finalUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;191;-1018.041,-121.917;Inherit;False;originalColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;180;-687.1025,-126.9742;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;240;-683.451,-246.0232;Inherit;False;237;shaderTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;148;-614.0953,-401.0159;Inherit;False;146;finalUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;343;-737.796,35.8288;Inherit;False;_UberCustomAlpha;581;;11753;d68af6e3188f53845b23cf6e39df15fe;0;3;1;COLOR;0,0,0,0;False;6;SAMPLER2D;0;False;7;FLOAT2;0,0;False;2;FLOAT;12;COLOR;0
Node;AmplifyShaderEditor.FunctionNode;239;-404.7228,-125.1053;Inherit;False;_UberGenerated;555;;11758;52defa3f7cca25740a6a77f065edb382;0;4;10;FLOAT;0;False;8;SAMPLER2D;0;False;7;FLOAT2;0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;242;-432.6598,-617.0601;Inherit;False;235;shaderPosition;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;181;-423.344,-436.9742;Inherit;False;157;spriteTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;178;-455.0759,-519.7366;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;183;-37.83691,-91.99512;Inherit;False;182;hologramFade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;361;-136.8644,-329.1863;Inherit;False;_UberColor;65;;11762;db48f560e502b78409f7fbe481a93597;0;6;39;FLOAT;0;False;40;FLOAT2;0,0;False;1;FLOAT2;0,0;False;24;SAMPLER2D;0;False;3;COLOR;0,0,0,0;False;5;SAMPLER2D;0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.FunctionNode;51;183.7499,-168.0946;Inherit;False;_Hologram;200;;11804;76082a965d84d0e4da33b2cff51b3691;0;3;42;FLOAT;0;False;40;FLOAT;0;False;1;COLOR;1,1,1,1;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;162;704.0067,-153.0455;Inherit;False;152;glitchFade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;56;438.7324,-454.4984;Inherit;False;Property;_EnableHologram;Enable Hologram;198;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;163;668.7452,-235.3598;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;161;687.0067,-317.0453;Inherit;False;154;glitchPosition;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;104;973.7388,-316.5438;Inherit;False;_Glitch;220;;11807;97a01281f94bcc04fbb9a7c1cd328f08;0;5;34;FLOAT;0;False;31;FLOAT2;0,0;False;33;SAMPLER2D;;False;29;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;164;250.219,-2894.672;Inherit;False;fullDistortionAlpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;246;1400.383,-493.8317;Inherit;False;235;shaderPosition;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;57;1278.486,-397.6114;Inherit;False;Property;_EnableGlitch;Enable Glitch;213;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;247;1456.499,-578.4069;Inherit;False;237;shaderTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;171;1364.183,-273.4383;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.FunctionNode;473;1656.25,-345.9998;Inherit;False;_UberEffect;231;;11811;93c7a07f758a0814998210619e8ad1cb;0;4;40;FLOAT;0;False;41;FLOAT2;0,0;False;3;COLOR;0,0,0,0;False;37;SAMPLER2D;0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;166;1853.428,-195.4143;Inherit;False;164;fullDistortionAlpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;167;1037.158,-2834.03;Inherit;False;directionalDistortionAlpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;78;2104.106,-267.8359;Inherit;False;AlphaMultiply;-1;;11867;d24974f7959982d48aab81e9e7692f35;0;2;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.StaticSwitch;77;2492.977,-348.4961;Inherit;False;Property;_EnableFullDistortion;Enable Full Distortion;465;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;168;2616.17,-223.2014;Inherit;False;167;directionalDistortionAlpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;76;3014.405,-219.2272;Inherit;False;AlphaMultiply;-1;;11868;d24974f7959982d48aab81e9e7692f35;0;2;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;249;3627.646,-39.2937;Inherit;False;235;shaderPosition;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;75;3434.708,-336.5002;Inherit;False;Property;_EnableDirectionalDistortion;Enable Directional Distortion;453;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;169;3611.012,-138.045;Inherit;False;159;uberNoiseTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;480;3955.587,-349.0083;Inherit;False;146;finalUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;479;3931.587,-267.0083;Inherit;False;157;spriteTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.FunctionNode;248;3912.165,-179.9706;Inherit;False;_UberFading;394;;11869;f8f5d1f402d6b694f9c47ef65b4ae91d;0;3;18;FLOAT2;0,0;False;1;COLOR;0,0,0,0;False;17;SAMPLER2D;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;193;-400.3044,37.52343;Inherit;False;customVertexAlpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;196;4178.044,-19.14569;Inherit;False;193;customVertexAlpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;478;4200.417,-206.2577;Inherit;False;_UberColorFinal;38;;11884;6ac57aba23ea6404ba71b6806ea93971;0;3;14;FLOAT2;0,0;False;15;SAMPLER2D;;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.VertexColorNode;348;4610.214,-299.2399;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;476;3958.333,566.3407;Inherit;False;475;fixedUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;200;3977.696,393.365;Inherit;False;Constant;_ZeroVector;Zero Vector;67;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.FunctionNode;344;4548.086,-90.46628;Inherit;False;AlphaMultiply;-1;;11895;d24974f7959982d48aab81e9e7692f35;0;2;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.FunctionNode;353;4889.3,-222.093;Inherit;False;ColorMultiply;-1;;11896;1f51da7edd80c06488c56d28bc096dec;0;2;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.FunctionNode;199;4171.966,521.7499;Inherit;False;_Squish;550;;11897;6d6a73cc3433bad4186f7028cad3d98c;0;2;82;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;194;4622.439,201.8054;Inherit;False;191;originalColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;251;4509.569,562.3449;Inherit;False;237;shaderTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;195;4877.096,136.7432;Inherit;False;TintVertex;-1;;11898;b0b94dd27c0f3da49a89feecae766dcc;0;1;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StaticSwitch;342;5134.784,-128.8904;Inherit;False;Property;_VertexTintFirst;Vertex Tint First;5;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT4;0,0,0,0;False;0;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;4;FLOAT4;0,0,0,0;False;5;FLOAT4;0,0,0,0;False;6;FLOAT4;0,0,0,0;False;7;FLOAT4;0,0,0,0;False;8;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;124;4962.062,258.7927;Inherit;False;123;fullFade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;198;4453.426,400.9801;Inherit;False;Property;_EnableSquish;Enable Squish;549;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;424;4759.72,401.6135;Inherit;False;_UberTransformOffset;516;;11899;ee5e9e731457b2342bdb306bdb8d2401;0;2;8;FLOAT;0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;125;5403.405,74.92608;Inherit;False;3;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;2;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;141;4826.724,532.0556;Inherit;False;123;fullFade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;121;5091.355,506.223;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;143;5663.995,-69.69315;Inherit;False;Property;_UberFading;Uber Fading;19;0;Create;True;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;4;NONE;Key1;Key2;Key3;Reference;139;True;True;All;9;1;FLOAT4;0,0,0,0;False;0;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;4;FLOAT4;0,0,0,0;False;5;FLOAT4;0,0,0,0;False;6;FLOAT4;0,0,0,0;False;7;FLOAT4;0,0,0,0;False;8;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.FunctionNode;340;6010.595,-19.64009;Inherit;False;BakingHandler;11;;11903;f63dfe0dc7c747c43b593d357b168fa0;0;1;7;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.FunctionNode;494;6161.298,123.2754;Inherit;False;_2DLitStuff;1;;11904;851662d67a92ce04d84817ff63c501f2;0;1;8;FLOAT2;0,0;False;2;COLOR;0;FLOAT3;5
Node;AmplifyShaderEditor.GetLocalVarNode;187;489.4036,-2756.916;Inherit;False;157;spriteTexture;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;495;5957.067,137.7232;Inherit;False;146;finalUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;142;6116.678,268.3106;Inherit;False;Property;_UberFading;Uber Fading;19;0;Create;True;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;4;NONE;Key1;Key2;Key3;Reference;139;True;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;503;6391.947,101.1744;Float;False;True;-1;2;SpriteShadersUltimate.SSUShaderGUI;0;17;Sprite Shaders Ultimate/2D Lit URP SSU;199187dac283dbe4a8cb1ea611d70c58;True;Sprite Lit;0;0;Sprite Lit;6;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;Hidden/InternalErrorShader;0;0;Standard;3;Vertex Position;1;0;Debug Display;0;0;External Alpha;0;0;0;5;True;True;True;True;True;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;504;6391.947,101.1744;Float;False;False;-1;2;ASEMaterialInspector;0;1;New Amplify Shader;199187dac283dbe4a8cb1ea611d70c58;True;Sprite Normal;0;1;Sprite Normal;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=NormalsRendering;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;505;6391.947,101.1744;Float;False;False;-1;2;ASEMaterialInspector;0;1;New Amplify Shader;199187dac283dbe4a8cb1ea611d70c58;True;Sprite Forward;0;2;Sprite Forward;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalForward;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;506;6391.947,101.1744;Float;False;False;-1;2;ASEMaterialInspector;0;1;New Amplify Shader;199187dac283dbe4a8cb1ea611d70c58;True;SceneSelectionPass;0;3;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;507;6391.947,101.1744;Float;False;False;-1;2;ASEMaterialInspector;0;1;New Amplify Shader;199187dac283dbe4a8cb1ea611d70c58;True;ScenePickingPass;0;4;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
WireConnection;105;0;496;0
WireConnection;157;0;105;0
WireConnection;483;1;363;0
WireConnection;483;12;411;0
WireConnection;482;1;483;0
WireConnection;482;12;411;0
WireConnection;460;0;482;0
WireConnection;438;0;157;0
WireConnection;432;0;438;0
WireConnection;469;1;460;0
WireConnection;469;2;411;0
WireConnection;434;0;435;0
WireConnection;434;1;432;0
WireConnection;374;0;373;1
WireConnection;374;1;373;2
WireConnection;449;1;460;0
WireConnection;449;0;469;0
WireConnection;375;0;373;3
WireConnection;375;1;373;4
WireConnection;376;0;374;0
WireConnection;377;0;375;0
WireConnection;450;0;449;0
WireConnection;433;0;434;0
WireConnection;436;0;433;0
WireConnection;436;1;432;0
WireConnection;370;13;450;0
WireConnection;370;5;376;0
WireConnection;370;6;377;0
WireConnection;366;1;450;0
WireConnection;366;0;370;0
WireConnection;437;1;435;0
WireConnection;437;0;436;0
WireConnection;237;0;341;0
WireConnection;439;61;437;0
WireConnection;439;3;441;0
WireConnection;475;0;366;0
WireConnection;99;0;101;0
WireConnection;159;0;99;0
WireConnection;365;9;477;0
WireConnection;365;3;475;0
WireConnection;235;0;439;0
WireConnection;84;0;365;0
WireConnection;79;195;253;0
WireConnection;79;160;84;0
WireConnection;79;194;184;0
WireConnection;83;1;84;0
WireConnection;83;0;79;174
WireConnection;81;182;254;0
WireConnection;81;160;83;0
WireConnection;81;181;188;0
WireConnection;82;1;83;0
WireConnection;82;0;81;174
WireConnection;38;0;82;0
WireConnection;102;19;244;0
WireConnection;102;18;243;0
WireConnection;102;16;160;0
WireConnection;182;0;53;0
WireConnection;52;77;255;0
WireConnection;52;55;182;0
WireConnection;52;76;173;0
WireConnection;52;37;38;0
WireConnection;52;39;174;0
WireConnection;152;0;102;15
WireConnection;154;0;102;0
WireConnection;59;1;38;0
WireConnection;59;0;52;0
WireConnection;292;1;122;0
WireConnection;292;2;204;0
WireConnection;231;27;261;0
WireConnection;231;22;122;0
WireConnection;231;18;228;0
WireConnection;231;25;208;0
WireConnection;231;23;229;0
WireConnection;231;21;210;0
WireConnection;231;26;230;0
WireConnection;223;21;261;0
WireConnection;223;1;122;0
WireConnection;223;16;219;0
WireConnection;223;18;208;0
WireConnection;223;20;210;0
WireConnection;139;1;122;0
WireConnection;139;0;122;0
WireConnection;139;2;292;0
WireConnection;139;3;223;0
WireConnection;139;4;231;0
WireConnection;103;23;256;0
WireConnection;103;13;155;0
WireConnection;103;22;175;0
WireConnection;103;3;153;0
WireConnection;103;1;59;0
WireConnection;123;0;139;0
WireConnection;62;1;59;0
WireConnection;62;0;103;0
WireConnection;484;25;259;0
WireConnection;484;26;258;0
WireConnection;484;1;62;0
WireConnection;484;18;176;0
WireConnection;484;3;177;0
WireConnection;130;0;131;0
WireConnection;130;1;484;0
WireConnection;130;2;129;0
WireConnection;145;1;484;0
WireConnection;145;0;130;0
WireConnection;145;2;130;0
WireConnection;145;3;130;0
WireConnection;145;4;130;0
WireConnection;486;0;145;0
WireConnection;487;0;486;0
WireConnection;488;0;487;0
WireConnection;485;1;145;0
WireConnection;485;0;488;0
WireConnection;378;13;485;0
WireConnection;378;2;380;0
WireConnection;378;3;381;0
WireConnection;382;0;378;0
WireConnection;382;1;380;0
WireConnection;383;0;382;0
WireConnection;383;1;381;0
WireConnection;371;1;485;0
WireConnection;371;0;383;0
WireConnection;470;21;463;0
WireConnection;470;17;452;0
WireConnection;470;1;371;0
WireConnection;470;2;442;0
WireConnection;427;1;371;0
WireConnection;427;0;470;0
WireConnection;146;0;427;0
WireConnection;471;1;147;0
WireConnection;471;2;189;0
WireConnection;356;1;471;0
WireConnection;356;2;358;0
WireConnection;354;1;471;0
WireConnection;354;0;356;0
WireConnection;191;0;354;0
WireConnection;343;1;191;0
WireConnection;343;6;179;0
WireConnection;343;7;149;0
WireConnection;239;10;240;0
WireConnection;239;8;180;0
WireConnection;239;7;148;0
WireConnection;239;1;343;0
WireConnection;361;39;240;0
WireConnection;361;40;242;0
WireConnection;361;1;148;0
WireConnection;361;24;178;0
WireConnection;361;3;239;0
WireConnection;361;5;181;0
WireConnection;51;42;240;0
WireConnection;51;40;183;0
WireConnection;51;1;361;0
WireConnection;56;1;361;0
WireConnection;56;0;51;0
WireConnection;104;34;244;0
WireConnection;104;31;161;0
WireConnection;104;33;163;0
WireConnection;104;29;162;0
WireConnection;104;1;56;0
WireConnection;164;0;79;0
WireConnection;57;1;56;0
WireConnection;57;0;104;0
WireConnection;473;40;247;0
WireConnection;473;41;246;0
WireConnection;473;3;57;0
WireConnection;473;37;171;0
WireConnection;167;0;81;0
WireConnection;78;1;473;0
WireConnection;78;2;166;0
WireConnection;77;1;473;0
WireConnection;77;0;78;0
WireConnection;76;1;77;0
WireConnection;76;2;168;0
WireConnection;75;1;77;0
WireConnection;75;0;76;0
WireConnection;248;18;249;0
WireConnection;248;1;75;0
WireConnection;248;17;169;0
WireConnection;193;0;343;12
WireConnection;478;14;480;0
WireConnection;478;15;479;0
WireConnection;478;3;248;0
WireConnection;344;1;478;0
WireConnection;344;2;196;0
WireConnection;353;1;344;0
WireConnection;353;2;348;0
WireConnection;199;82;200;0
WireConnection;199;1;476;0
WireConnection;195;1;194;0
WireConnection;342;1;353;0
WireConnection;342;0;344;0
WireConnection;198;1;200;0
WireConnection;198;0;199;0
WireConnection;424;8;251;0
WireConnection;424;2;198;0
WireConnection;125;0;195;0
WireConnection;125;1;342;0
WireConnection;125;2;124;0
WireConnection;121;1;424;0
WireConnection;121;2;141;0
WireConnection;143;1;342;0
WireConnection;143;0;125;0
WireConnection;143;2;125;0
WireConnection;143;3;125;0
WireConnection;143;4;125;0
WireConnection;340;7;143;0
WireConnection;494;8;495;0
WireConnection;142;1;424;0
WireConnection;142;0;121;0
WireConnection;142;2;121;0
WireConnection;142;3;121;0
WireConnection;142;4;121;0
WireConnection;503;1;340;0
WireConnection;503;2;494;0
WireConnection;503;3;494;5
WireConnection;503;4;142;0
ASEEND*/
//CHKSM=F9A16A2ADDFC6F3B6CB9B69C5DE10E4C5B7338C8
