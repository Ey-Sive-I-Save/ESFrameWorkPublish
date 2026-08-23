#ifndef ES_COMPOSITE_SSU_STATUS_EFFECTS_INCLUDED
#define ES_COMPOSITE_SSU_STATUS_EFFECTS_INCLUDED

float ESCompositeSampleSSUStatusNoise(float2 uv)
{
    return ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
        _UberNoiseTexture,
        sampler_UberNoiseTexture,
        uv).r);
}

half3 ESCompositeApplySSUFrozen(half3 source, float2 coordinate, float timeValue)
{
    float luminance = ESCompositeSSULuminance(source);
    float snowNoise = ESCompositeSampleSSUStatusNoise(
        coordinate * _FrozenSnowScale.xy);
    float highlightDistortionNoise = ESCompositeSampleSSUStatusNoise(
        (coordinate + timeValue * _FrozenHighlightDistortionSpeed.xy)
            * _FrozenHighlightDistortionScale.xy);
    float2 highlightUV = (
        coordinate
        + timeValue * _FrozenHighlightSpeed.xy
        + (highlightDistortionNoise - 0.25) * _FrozenHighlightDistortion.xy)
        * _FrozenHighlightScale.xy;
    float highlightNoise = ESCompositeSampleSSUStatusNoise(highlightUV);

    half3 frozen = _FrozenTint.rgb
        * (half)pow(luminance, max(_FrozenContrast, 0.001));
    frozen += _FrozenSnowColor.rgb
        * (half)max(_FrozenSnowDensity - snowNoise, 0.0)
        * (half)pow(luminance, max(_FrozenSnowContrast, 0.001));
    frozen += _FrozenHighlightColor.rgb
        * (half)max(
            (_FrozenHighlightDensity - highlightNoise)
                / max(_FrozenHighlightDensity, 0.01),
            0.0)
        * (half)pow(luminance, max(_FrozenHighlightContrast, 0.001));
    return lerp(source, frozen, saturate((half)_FrozenFade));
}

half3 ESCompositeApplySSUBurn(half3 source, float2 coordinate)
{
    float swirlNoise = ESCompositeSampleSSUStatusNoise(
        coordinate * _BurnSwirlNoiseScale.xy);
    float insideNoise = ESCompositeSampleSSUStatusNoise(
        (coordinate + (swirlNoise - 0.5) * _BurnSwirlFactor)
            * _BurnInsideNoiseScale.xy);
    float insideNoiseMask = saturate(_BurnInsideNoiseFactor - insideNoise);
    float edgeNoise = ESCompositeSampleSSUStatusNoise(
        coordinate * _BurnEdgeNoiseScale.xy);
    float burnRatio = (
        _BurnRadius
        - distance(coordinate, _BurnPosition.xy)
        + edgeNoise * _BurnEdgeNoiseFactor)
        / max(_BurnWidth, 0.01);
    float insideMask = saturate(burnRatio);
    float edgeMask = step(burnRatio, 1.0) * step(0.0, burnRatio);
    float luminance = ESCompositeSSULuminance(source);
    half3 inside = (half)pow(luminance, max(_BurnInsideContrast, 0.001))
        * (_BurnInsideColor.rgb
            + _BurnInsideNoiseColor.rgb * (half)insideNoiseMask);
    half3 burned = lerp(source, inside, insideMask);
    burned += _BurnEdgeColor.rgb * (half)edgeMask;
    return lerp(source, burned, saturate((half)_BurnFade));
}

half3 ESCompositeApplySSURainbow(half3 source, float2 coordinate, float timeValue)
{
    float noise = ESCompositeSampleSSUStatusNoise(
        coordinate * _RainbowNoiseScale.xy);
    float hue = (
        distance(coordinate, _RainbowCenter.xy)
        + noise * _RainbowNoiseFactor)
        * _RainbowDensity
        + timeValue * _RainbowSpeed;
    float3 rainbow = ESCompositeHsvToRgb(float3(hue, 1.0, 1.0));
    float3 rainbowHsv = ESCompositeRgbToHsv(rainbow);
    rainbow = ESCompositeHsvToRgb(float3(
        rainbowHsv.x,
        saturate(_RainbowSaturation),
        rainbowHsv.z * _RainbowBrightness));
    float luminance = abs(ESCompositeSSULuminance(source));
    return source + (half3)rainbow
        * (half)pow(luminance, max(_RainbowContrast, 0.001))
        * (half)saturate(_RainbowFade);
}

half3 ESCompositeApplySSUShine(
    half3 source,
    float shineCoordinate,
    float2 maskUV,
    float timeValue)
{
    float width = max(abs(_ShineFrequency * _ShineWidth), 0.0001);
    float band = saturate((
        sin(shineCoordinate * _ShineFrequency
            - timeValue * _ShineSpeed * _ShineFrequency)
        - (1.0 - width)) / width * _ShineSmooth);

    half mask = 1.0h;
    if (_ShineMaskToggle > 0.5)
    {
        half4 maskSample = SAMPLE_TEXTURE2D(
            _ShineMask,
            sampler_ShineMask,
            maskUV * _ShineMask_ST.xy + _ShineMask_ST.zw);
        mask = maskSample.r * maskSample.a;
    }
    float luminance = ESCompositeSSULuminance(source);
    half3 luminanceColor = half3(luminance, luminance, luminance);
    half3 saturatedSource = lerp(luminanceColor, source, saturate((half)_ShineSaturation));
    half3 shine = pow(max(saturatedSource, 0.0h), max((half)_ShineContrast, 0.001h))
        * _ShineColor.rgb;
    return source + shine * (half)(band * saturate(_ShineFade) * mask);
}

half3 ESCompositeApplySSUPoison(half3 source, float2 coordinate, float timeValue)
{
    float noise = ESCompositeSampleSSUStatusNoise(
        (coordinate + timeValue * _PoisonNoiseSpeed.xy)
            * _PoisonNoiseScale.xy);
    float luminance = ESCompositeSSULuminance(source);
    half3 recolored = lerp(
        source,
        _PoisonColor.rgb * (half)luminance,
        saturate((half)(_PoisonFade * _PoisonRecolorFactor)));
    float stripe = pow(
        abs(fmod(noise + timeValue * _PoisonShiftSpeed, 1.0) - 0.5),
        max(_PoisonDensity, 0.001));
    return recolored + _PoisonColor.rgb
        * (half)(stripe * _PoisonFade * _PoisonNoiseBrightness);
}

half3 ESCompositeApplySSUStatusEffects(
    half3 source,
    float2 coordinate,
    float2 maskUV,
    float shineCoordinate,
    float timeValue)
{
    half3 color = source;
    if (_EnableFrozen > 0.5)
        color = ESCompositeApplySSUFrozen(color, coordinate, timeValue);
    if (_EnableBurn > 0.5)
        color = ESCompositeApplySSUBurn(color, coordinate);
    if (_EnableRainbow > 0.5)
        color = ESCompositeApplySSURainbow(color, coordinate, timeValue);
    if (_EnableShine > 0.5)
        color = ESCompositeApplySSUShine(color, shineCoordinate, maskUV, timeValue);
    if (_EnablePoison > 0.5)
        color = ESCompositeApplySSUPoison(color, coordinate, timeValue);
    return color;
}

#endif
