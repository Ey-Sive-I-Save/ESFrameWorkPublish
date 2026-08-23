#ifndef ES_3D_LIT_COMPOSITE_SSU_SURFACE_INCLUDED
#define ES_3D_LIT_COMPOSITE_SSU_SURFACE_INCLUDED

void ESApplyLitSSUSurfaceEffects(
    float2 uv,
    float3 positionWS,
    half alpha,
    inout half3 color,
    inout half3 emission)
{
    float timeValue = ESCompositeTime();

    // Color transforms preserve the Lit surface contract and do not alter alpha.
    if (_EnableAddColor > 0.5)
    {
        half3 addTint = _AddColor.rgb;
        if (_AddColorMaskToggle > 0.5)
        {
            half4 maskSample = SAMPLE_TEXTURE2D(
                _AddColorMask,
                sampler_BaseMap,
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
                sampler_BaseMap,
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
        float replace = 1.0 - smoothstep(
            _ReplaceRange,
            _ReplaceRange + max(_ReplaceSoftness, 0.001),
            colorDistance);
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
    if (_EnableBrightness > 0.5)
        color *= max(_Brightness, 0.0);
    if (_EnableContrast > 0.5)
        color = (color - 0.5h) * max(_Contrast, 0.0) + 0.5h;
    if (_EnableHue > 0.5)
    {
        float3 hsv = ESCompositeRgbToHsv(color);
        hsv.x = frac(hsv.x + _Hue);
        color = (half3)ESCompositeHsvToRgb(hsv);
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
        color = ESCompositeApplyBlackTint(
            color,
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
        color = ESCompositeApplyInkSpread(
            color,
            _InkSpreadColor.rgb,
            _InkSpreadContrast,
            _InkSpreadFade,
            inkMask);
    }
    if (_EnableShiftHue > 0.5)
        color = ESCompositeApplyShiftHue(color, timeValue, _ShiftHueSpeed);

    // Glow families remain unlit by contributing their additive portion to emission.
    if (_EnableAddHue > 0.5)
    {
        float mask = 1.0;
        if (_AddHueMaskToggle > 0.5)
        {
            half4 maskSample = SAMPLE_TEXTURE2D(
                _AddHueMask,
                sampler_AddHueMask,
                uv * _AddHueMask_ST.xy + _AddHueMask_ST.zw);
            mask = maskSample.r * maskSample.a;
        }
        half3 withAddHue = ESCompositeApplyAddHue(
            color,
            timeValue,
            _AddHueSpeed,
            _AddHueSaturation,
            _AddHueBrightness,
            _AddHueContrast,
            _AddHueFade * mask);
        emission += max(withAddHue - color, 0.0h);
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
        half3 withSineGlow = ESCompositeApplySineGlow(
            color,
            sineGlowColor,
            timeValue,
            _SineGlowContrast,
            _SineGlowFrequency,
            _SineGlowMin,
            _SineGlowMax,
            _SineGlowFade);
        emission += max(withSineGlow - color, 0.0h);
    }

    // Pattern and material effects sample only while their parent switch is enabled.
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
            half4 maskSample = SAMPLE_TEXTURE2D(_MetalMask, sampler_MetalMask, uv);
            metalMask = maskSample.r * maskSample.a;
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
        float exactShineCoordinate = ESResolveLitShineCoordinate(
            uv,
            positionWS,
            _ShineRotation,
            1.0);
        color = ESCompositeApplySSUStatusEffects(
            color,
            uv,
            uv,
            exactShineCoordinate,
            timeValue);
    }
    if (_EnableSaturation > 0.5)
    {
        float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
        color = lerp(luminance.xxx, color, max(_Saturation, 0.0));
    }
    if (_EnableNegative > 0.5)
        color = lerp(color, 1.0h - color, saturate(_NegativeFade));
    if (_SSUStatusContract <= 0.5 && _EnableRainbow > 0.5)
    {
        float rainbowCoordinate = ESCompositeDirectionalCoordinate2D(
            uv,
            _RainbowDirection.xy,
            float2(0.0, 1.0));
        float hue = frac(rainbowCoordinate * _RainbowDensity + timeValue * _RainbowSpeed);
        half3 rainbow = (half3)ESCompositeHsvToRgb(float3(hue, 1.0, 1.0));
        color = lerp(color, rainbow * _RainbowBrightness, 0.5h);
    }
    if (_EnablePingPongGlow > 0.5)
    {
        float wave = 0.5 + 0.5 * sin(timeValue * _GlowFrequency);
        float glowLuminance = ESCompositeSSULuminance(color);
        emission += lerp(_GlowFrom.rgb, _GlowTo.rgb, wave)
            * (half)pow(glowLuminance, max(_GlowContrast, 0.001))
            * _GlowIntensity
            * _GlowFade;
    }

    float statusNoise = 0.0;
    if (_SSUStatusContract <= 0.5 && (_EnableFrozen > 0.5 || _EnablePoison > 0.5))
        statusNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
            _UberNoiseTexture,
            sampler_UberNoiseTexture,
            uv).r);
    if (_SSUStatusContract <= 0.5 && _EnableFrozen > 0.5)
    {
        float snow = smoothstep(1.0 - _FrozenDensity, 1.0, statusNoise);
        color = lerp(color, _FrozenColor.rgb, 0.65h);
        emission += _FrozenHighlight.rgb * snow
            * (0.5 + 0.5 * sin(timeValue * _FrozenSpeed + statusNoise * 6.0));
    }
    if (_SSUStatusContract <= 0.5 && _EnablePoison > 0.5)
    {
        float poison = 0.5 + 0.5 * sin(
            timeValue * _PoisonSpeed + statusNoise * _PoisonDensity * 6.0);
        color = lerp(color, _PoisonColor.rgb, saturate(poison * 0.45));
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
}

#endif
