#ifndef ES_COMPOSITE_ESNative_EFFECTS_INCLUDED
#define ES_COMPOSITE_ESNative_EFFECTS_INCLUDED

#ifndef ES_ROTATE_2D_INCLUDED
#define ES_ROTATE_2D_INCLUDED
float2 ESRotate2D(float2 value, float angle)
{
    float sine = sin(angle);
    float cosine = cos(angle);
    return float2(
        value.x * cosine - value.y * sine,
        value.x * sine + value.y * cosine);
}
#endif

float2 ESCompositeApplySqueezeUV(
    float2 uv,
    float2 center,
    float power,
    float2 scale,
    float fade)
{
    float2 delta = uv - center;
    float radial = pow(max(length(delta), 1e-5), clamp(power, 0.001, 8.0));
    return uv + delta * radial * clamp(scale, -8.0, 8.0) * saturate(fade);
}

float2 ESCompositeApplySineRotateUV(
    float2 uv,
    float2 pivot,
    float timeValue,
    float frequency,
    float angle,
    float fade)
{
    float radiansValue = sin(timeValue * clamp(frequency, -32.0, 32.0))
        * (clamp(angle, -720.0, 720.0) / 360.0) * 3.14159265 * saturate(fade);
    return pivot + ESRotate2D(uv - pivot, radiansValue);
}

half3 ESCompositeApplyCamouflage(
    half3 source,
    half3 baseColor,
    half3 colorA,
    float densityA,
    float smoothnessA,
    float noiseA,
    half3 colorB,
    float densityB,
    float smoothnessB,
    float noiseB,
    float contrast,
    float fade)
{
    float patternA = saturate((densityA - noiseA) / max(smoothnessA, 0.005));
    half3 layered = lerp(baseColor, colorA * (half)patternA, patternA);
    float patternB = saturate((densityB - noiseB) / max(smoothnessB, 0.005));
    layered = lerp(layered, colorB * (half)patternB, patternB);
    float luminance = ESCompositeESNativeLuminance(source);
    half3 result = layered * (half)pow(luminance, clamp(contrast, 0.001, 8.0));
    return lerp(source, result, saturate((half)fade));
}

half3 ESCompositeApplyMetal(
    half3 source,
    half3 metalColor,
    float contrast,
    half3 highlightColor,
    float highlightDensity,
    float highlightContrast,
    float noise,
    float fadeMask)
{
    float luminance = ESCompositeESNativeLuminance(source);
    float highlight = max(
        (highlightDensity - noise) / max(highlightDensity, 0.01),
        0.0);
    half3 result = highlight * highlightColor
        * (half)pow(luminance, clamp(highlightContrast, 0.001, 8.0))
        + metalColor * (half)pow(luminance, clamp(contrast, 0.001, 8.0));
    return lerp(source, result, saturate((half)fadeMask));
}

half3 ESCompositeApplyEnchanted(
    half3 source,
    float noiseA,
    float noiseB,
    float timeValue,
    half3 lowColor,
    half3 highColor,
    float rainbowEnabled,
    float rainbowSpeed,
    float rainbowDensity,
    float rainbowSaturation,
    float brightness,
    float contrast,
    float reduce,
    float fade,
    float lerpEnabled)
{
    float noiseSum = noiseA + noiseB;
    float blend = noiseSum * 0.5;
    half3 palette = lerp(lowColor, highColor, blend);
    if (rainbowEnabled > 0.5)
        palette = (half3)ESCompositeHsvToRgb(float3(
            blend * rainbowDensity + rainbowSpeed * timeValue,
            saturate(rainbowSaturation),
            1.0));
    float luminance = ESCompositeESNativeLuminance(source);
    half3 effectColor = palette * (half)(pow(luminance, clamp(contrast, 0.001, 8.0))
        * clamp(brightness, 0.0, 16.0));
    float weight = max(noiseSum - reduce, 0.0) * saturate(fade);
    return lerpEnabled > 0.5
        ? lerp(source, effectColor, saturate(weight))
        : source + effectColor * (half)weight;
}

half3 ESCompositeApplyShifting(
    half3 source,
    float timeValue,
    float speed,
    float density,
    float brightness,
    float saturation,
    float contrast,
    half3 colorA,
    half3 colorB,
    float rainbowEnabled,
    float fade)
{
    float luminance = ESCompositeESNativeLuminance(source);
    float phase = frac((luminance + timeValue * clamp(speed, -32.0, 32.0))
        * clamp(density, -32.0, 32.0));
    float safeBrightness = clamp(brightness, 0.0, 16.0);
    half3 palette = lerp(colorA, colorB, abs(phase - 0.5) * 2.0) * (half)safeBrightness;
    if (rainbowEnabled > 0.5)
        palette = (half3)ESCompositeHsvToRgb(float3(phase, saturate(saturation), safeBrightness));
    half3 result = palette * (half)pow(luminance, clamp(contrast, 0.001, 8.0));
    return lerp(source, result, saturate((half)fade));
}

float ESCompositeCustomFadeVisibility(
    float vertexAlpha,
    float mask,
    float noise,
    float smoothness,
    float noiseFactor,
    float alpha)
{
    float baseValue = saturate((vertexAlpha * 2.0 - 1.0) + mask + noise * noiseFactor);
    float exponent = clamp(smoothness, 0.001, 16.0) / max(saturate(mask), 0.05);
    return pow(max(baseValue, 0.0), exponent) * saturate(alpha);
}

half4 ESCompositeApplyFullGlowDissolve(
    half4 source,
    float noise,
    float fade,
    float width,
    half3 edgeColor,
    out half visibility)
{
    float safeFade = saturate(fade);
    visibility = (half)step(noise, safeFade);
    float edgeWidth = max(safeFade * clamp(width, 0.001, 8.0), 0.001);
    float innerThreshold = safeFade * (1.01 + edgeWidth) - edgeWidth;
    half edge = max(visibility - (half)step(noise, innerThreshold), 0.0h);
    return half4(source.rgb + edgeColor * edge, source.a * visibility);
}

#endif
