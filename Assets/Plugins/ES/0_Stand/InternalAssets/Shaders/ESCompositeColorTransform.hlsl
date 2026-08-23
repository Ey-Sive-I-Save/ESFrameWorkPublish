#ifndef ES_COMPOSITE_COLOR_TRANSFORM_INCLUDED
#define ES_COMPOSITE_COLOR_TRANSFORM_INCLUDED

// Shared, keyword-free transforms used by the 2D and UI composite shaders.
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

float2 ESCompositeTransformUV(
    float2 uv,
    float2 pivot,
    float2 scale,
    float2 offset,
    float rotationDegrees)
{
    float2 scaleSign = lerp(float2(-1.0, -1.0), float2(1.0, 1.0), step(float2(0.0, 0.0), scale));
    float2 safeScale = scaleSign * max(abs(scale), float2(1e-4, 1e-4));
    float2 centered = (uv - pivot) / safeScale;
    float angle = radians(rotationDegrees);
    float sine = sin(angle);
    float cosine = cos(angle);
    float2 rotated = float2(
        centered.x * cosine - centered.y * sine,
        centered.x * sine + centered.y * cosine);
    return rotated + pivot + offset;
}

float2 ESCompositeUVDistort(float2 uv, float2 frequency, float2 speed, float amount, float timeValue)
{
    float2 phase = (uv * max(abs(frequency), float2(0.001, 0.001)) + speed * timeValue) * 6.2831853;
    float2 wave = float2(sin(phase.x + cos(phase.y)), cos(phase.y + sin(phase.x)));
    return uv + wave * amount;
}

float2 ESCompositeUVDistortNoise(float2 uv, float noise, float2 fromOffset, float2 toOffset, float fade, float mask)
{
    return uv + lerp(fromOffset, toOffset, saturate(noise)) * saturate(fade) * saturate(mask);
}

half3 ESCompositeSplitTone(
    half3 color,
    half3 shadowColor,
    half3 highlightColor,
    float balance,
    float strength,
    float contrast,
    float shift)
{
    float luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
    float shiftedLuminance = saturate(luminance + shift);
    float contrastedLuminance = saturate(
        (shiftedLuminance - 0.5) * max(contrast, 0.001) + 0.5);
    float shadowWeight = 1.0 - smoothstep(
        0.0,
        0.5,
        saturate(contrastedLuminance + balance * 0.5));
    float highlightWeight = smoothstep(
        0.5,
        1.0,
        saturate(contrastedLuminance + balance * 0.5));
    half3 toned = lerp(color, color * shadowColor, shadowWeight * saturate(strength));
    toned = lerp(toned, toned * highlightColor, highlightWeight * saturate(strength));
    return toned;
}

float2 ESCompositeResolveShineDirection2D(float2 explicitDirection, float fallbackDegrees)
{
    float directionLength = length(explicitDirection);
    if (directionLength > 0.0001)
        return explicitDirection / directionLength;

    float angle = radians(fallbackDegrees);
    return float2(cos(angle), sin(angle));
}

float3 ESCompositeResolveShineDirection3D(float3 explicitDirection, float3 fallbackDirection)
{
    float directionLength = length(explicitDirection);
    if (directionLength > 0.0001)
        return explicitDirection / directionLength;

    float fallbackLength = max(length(fallbackDirection), 0.0001);
    return fallbackDirection / fallbackLength;
}

float ESCompositeShineCoordinate2D(
    float2 coordinate,
    float2 explicitDirection,
    float fallbackDegrees)
{
    return dot(
        coordinate,
        ESCompositeResolveShineDirection2D(explicitDirection, fallbackDegrees));
}

float ESCompositeShineCoordinate3D(
    float3 positionWS,
    float3 explicitDirection,
    float3 fallbackDirection)
{
    return dot(
        positionWS,
        ESCompositeResolveShineDirection3D(explicitDirection, fallbackDirection));
}

float ESCompositeSSULuminance(float3 color)
{
    return max((color.r * 2.0 + color.g * 3.0 + color.b) / 6.0, 0.0);
}

float3 ESCompositeRgbToHsv(float3 color)
{
    float4 k = float4(0.0, -0.333333333, 0.666666667, -1.0);
    float4 p = lerp(float4(color.bg, k.wz), float4(color.gb, k.xy), step(color.b, color.g));
    float4 q = lerp(float4(p.xyw, color.r), float4(color.r, p.yzx), step(p.x, color.r));
    float delta = q.x - min(q.w, q.y);
    float epsilon = 1e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * delta + epsilon)), delta / (q.x + epsilon), q.x);
}

float3 ESCompositeHsvToRgb(float3 color)
{
    float3 rgb = saturate(abs(frac(color.xxx + float3(0.0, 0.666666667, 0.333333333)) * 6.0 - 3.0) - 1.0);
    return color.z * lerp(float3(1.0, 1.0, 1.0), rgb, color.y);
}

half3 ESCompositeApplyBlackTint(half3 source, half3 tintColor, float power, float fade)
{
    float darkWeight = pow(1.0 - saturate(max(source.r, max(source.g, source.b))), max(power, 0.001));
    half3 tinted = source + tintColor * (half)darkWeight;
    return lerp(source, tinted, saturate((half)fade));
}

half3 ESCompositeApplyInkSpread(
    half3 source,
    half3 inkColor,
    float contrast,
    float fade,
    float spreadMask)
{
    float luminance = ESCompositeSSULuminance(source);
    half3 target = inkColor * (half)pow(luminance, max(contrast, 0.001));
    return lerp(source, target, saturate((half)(fade * spreadMask)));
}

half3 ESCompositeApplyShiftHue(half3 source, float timeValue, float speed)
{
    float3 hsv = ESCompositeRgbToHsv(source);
    hsv.x = frac(hsv.x + timeValue * speed);
    return (half3)ESCompositeHsvToRgb(hsv);
}

half3 ESCompositeApplyAddHue(
    half3 source,
    float timeValue,
    float speed,
    float saturation,
    float brightness,
    float contrast,
    float fadeMask)
{
    float3 hueColor = ESCompositeHsvToRgb(float3(frac(timeValue * speed), saturate(saturation), max(brightness, 0.0)));
    float luminance = ESCompositeSSULuminance(source);
    return source + (half3)(hueColor * pow(luminance, max(contrast, 0.001)) * saturate(fadeMask));
}

half3 ESCompositeApplySineGlow(
    half3 source,
    half3 glowColor,
    float timeValue,
    float contrast,
    float frequency,
    float minimum,
    float maximum,
    float fade)
{
    float luminance = ESCompositeSSULuminance(source);
    float wave = (sin(timeValue * frequency) + 1.0) * (maximum - minimum) + minimum;
    return source + glowColor * (half)(pow(luminance, max(contrast, 0.001)) * fade * wave);
}

half4 ESCompositeApplySpriteShadow(half4 foreground, half3 shadowColor, half shadowAlpha)
{
    half behindAlpha = (1.0h - saturate(foreground.a)) * saturate(shadowAlpha);
    half outputAlpha = foreground.a + behindAlpha;
    half3 outputColor = (foreground.rgb * foreground.a + shadowColor * behindAlpha)
        / max(outputAlpha, 0.01h);
    return half4(outputColor, outputAlpha);
}

#endif
