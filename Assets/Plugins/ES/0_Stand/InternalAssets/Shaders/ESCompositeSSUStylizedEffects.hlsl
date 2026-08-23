#ifndef ES_COMPOSITE_SSU_STYLIZED_EFFECTS_INCLUDED
#define ES_COMPOSITE_SSU_STYLIZED_EFFECTS_INCLUDED

float ESCompositeSampleSSUStylizedNoise(float2 uv)
{
    return ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
        _UberNoiseTexture,
        sampler_UberNoiseTexture,
        uv).r);
}

float ESCompositeSSUStylizedLuminance(float3 color)
{
    return (color.r * 2.0 + color.g * 3.0 + color.b) / 6.0;
}

float ESCompositeResolveSSUHologramCoordinate(
    float2 localCoordinate,
    float3 positionWS)
{
    float2 localDirection = _HologramDirection.xy;
    float localDirectionLength = length(localDirection);
    localDirection = localDirectionLength > 0.0001
        ? localDirection / localDirectionLength
        : float2(0.0, 1.0);
    float3 worldDirection = _HologramDirection.xyz;
    float worldDirectionLength = length(worldDirection);
    worldDirection = worldDirectionLength > 0.0001
        ? worldDirection / worldDirectionLength
        : float3(0.0, 1.0, 0.0);
    float localProjection = dot(frac(localCoordinate), localDirection);
    float worldProjection = dot(positionWS, worldDirection);
    float orthographicHeight = max(abs(unity_OrthoParams.y), 0.0001);
    float normalizedWorldProjection = lerp(
        worldProjection,
        worldProjection / orthographicHeight,
        unity_OrthoParams.w);
    return lerp(
        localProjection,
        normalizedWorldProjection,
        step(0.5, _HologramSpace));
}

float2 ESCompositeApplySSUHologramUV(
    float2 uv,
    float hologramCoordinate,
    float textureWidth,
    float timeValue)
{
    float safeFade = saturate(_HologramFade);
    float safeTextureWidth = max(abs(textureWidth), 1.0);
    float scanHeight = hologramCoordinate
        + timeValue * _HologramDistortionSpeed;
    float densityNoise = clamp(
        ESCompositeSampleSSUStylizedNoise(scanHeight.xx * _HologramDistortionDensity),
        0.075,
        0.6);
    float offsetNoise = ESCompositeSampleSSUStylizedNoise(
        scanHeight.xx * _HologramDistortionScale) - 0.5;
    float2 distortionDirection = _HologramDistortionDirection.xy;
    float distortionDirectionLength = length(distortionDirection);
    distortionDirection = distortionDirectionLength > 0.0001
        ? distortionDirection / distortionDirectionLength
        : float2(1.0, 0.0);
    uv += distortionDirection * densityNoise * offsetNoise
        * _HologramDistortionOffset * (100.0 / safeTextureWidth) * safeFade;
    return uv;
}

float ESCompositeSSUGlitchFade(float2 coordinate, float timeValue)
{
    float maskNoise = ESCompositeSampleSSUStylizedNoise(
        (coordinate + _GlitchMaskSpeed.xy * timeValue) * _GlitchMaskScale.xy);
    return saturate(max(maskNoise, saturate(_GlitchMaskMin)) * saturate(_GlitchFade));
}

float2 ESCompositeApplySSUGlitchUV(float2 uv, float2 coordinate, float timeValue)
{
    float distortionNoise = ESCompositeSampleSSUStylizedNoise(
        (coordinate + _GlitchDistortionSpeed.xy * timeValue)
            * _GlitchDistortionScale.xy) - 0.5;
    return uv + distortionNoise * _GlitchDistortion.xy
        * ESCompositeSSUGlitchFade(coordinate, timeValue);
}

half4 ESCompositeApplySSUHologramColor(
    half4 source,
    float hologramCoordinate,
    float timeValue)
{
    float luminance = ESCompositeSSUStylizedLuminance(source.rgb);
    float scanHeight = hologramCoordinate + timeValue * _HologramSpeed;
    float lineFrequency = max(abs(_HologramLineFrequency), 0.001);
    float lineGap = clamp(_HologramLineGap, 0.001, 8.0);
    float minimumAlpha = saturate(_HologramMinAlpha);
    float safeFade = saturate(_HologramFade);
    half lineAlpha = (half)max(
        pow(abs(sin(scanHeight * lineFrequency)), lineGap),
        minimumAlpha);
    half4 hologram = half4(
        _HologramColor.rgb * (half)pow(luminance, clamp(_HologramContrast, 0.001, 8.0)),
        lineAlpha * source.a);
    return lerp(source, hologram, (half)safeFade);
}

half3 ESCompositeApplySSUGlitchColor(
    half3 source,
    float2 coordinate,
    float timeValue)
{
    float colorNoise = ESCompositeSampleSSUStylizedNoise(
        (coordinate + _GlitchNoiseSpeed.xy * timeValue) * _GlitchNoiseScale.xy);
    half3 hueColor = (half3)ESCompositeHsvToRgb(float3(
        colorNoise + timeValue * _GlitchHueSpeed,
        1.0,
        1.0));
    half3 glitchColor = (half)ESCompositeSSUStylizedLuminance(source)
        * (half)clamp(_GlitchBrightness, 0.0, 16.0) * hueColor;
    return lerp(
        source,
        glitchColor,
        (half)ESCompositeSSUGlitchFade(coordinate, timeValue));
}

float2 ESCompositeSSUOutlineDistortedUV(
    float2 uv,
    float enabled,
    float2 intensity,
    float2 noiseScale,
    float2 noiseSpeed,
    float timeValue)
{
    if (enabled <= 0.5)
        return uv;
    float noise = ESCompositeSampleSSUStylizedNoise(
        (uv + timeValue * noiseSpeed) * noiseScale) - 0.5;
    return uv + noise * intensity;
}

half4 ESCompositeApplySSUInnerOutline(
    half4 source,
    half minimumNeighbourAlpha,
    half3 tint,
    float fade,
    float outlineOnly)
{
    half edge = (half)saturate(fade) * (1.0h - minimumNeighbourAlpha);
    half4 result = source;
    result.rgb = lerp(source.rgb, tint, edge);
    if (outlineOnly > 0.5)
        result.a = edge * source.a;
    return result;
}

half4 ESCompositeApplySSUOuterOutline(
    half4 source,
    half maximumNeighbourAlpha,
    half3 tint,
    float fade,
    float outlineOnly)
{
    half safeFade = (half)saturate(fade);
    half outside = (1.0h - source.a) * min(safeFade * 3.0h, 1.0h);
    half colorWeight = outlineOnly > 0.5 ? 1.0h : outside;
    half expandedAlpha = lerp(
        source.a,
        min(maximumNeighbourAlpha * 3.0h, 1.0h),
        safeFade);
    half4 result;
    half3 firstTint = lerp(source.rgb, tint, colorWeight);
    result.rgb = lerp(firstTint, tint, colorWeight);
    result.a = outlineOnly > 0.5 ? outside * expandedAlpha : expandedAlpha;
    return result;
}

#endif
