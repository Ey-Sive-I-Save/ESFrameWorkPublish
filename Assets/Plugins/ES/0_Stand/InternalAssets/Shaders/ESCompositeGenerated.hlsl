#ifndef ES_COMPOSITE_GENERATED_INCLUDED
#define ES_COMPOSITE_GENERATED_INCLUDED

float2 ESCompositeResolveTilingUV(
    float2 localUV,
    float2 positionWS,
    float2 screenUV,
    float2 screenSize,
    float tilingMode,
    float2 worldScale,
    float2 worldOffset,
    float worldPixelsPerUnit,
    float2 screenScale,
    float2 screenOffset,
    float screenPixelsPerTile)
{
    if (tilingMode > 0.5 && tilingMode < 1.5)
        return frac(positionWS * worldScale * max(worldPixelsPerUnit, 0.001) + worldOffset);
    if (tilingMode > 1.5)
        return frac((screenUV * screenSize / max(screenPixelsPerTile, 1.0)) * screenScale + screenOffset);
    return localUV;
}

float2 ESCompositeSmoothPixelUV(float2 uv, float2 textureSize, float strength)
{
    float2 pixel = uv * max(textureSize, float2(1.0, 1.0));
    float2 transition = max(fwidth(pixel), float2(0.0001, 0.0001));
    float2 reconstructed = floor(pixel)
        + saturate((frac(pixel) - 0.5) / transition + 0.5);
    return lerp(uv, reconstructed / max(textureSize, float2(1.0, 1.0)), saturate(strength));
}

float ESCompositeDirectionalCoordinate2D(
    float2 coordinate,
    float2 direction,
    float2 fallbackDirection)
{
    float directionLength = length(direction);
    float2 resolvedDirection = directionLength > 0.0001
        ? direction / directionLength
        : normalize(fallbackDirection);
    return dot(coordinate, resolvedDirection);
}

float ESCompositeDirectionalCoordinate3D(
    float3 coordinate,
    float3 direction,
    float3 fallbackDirection)
{
    float directionLength = length(direction);
    float3 resolvedDirection = directionLength > 0.0001
        ? direction / directionLength
        : normalize(fallbackDirection);
    return dot(coordinate, resolvedDirection);
}

half3 ESCompositeApplyCheckerboard(half3 color, float2 coordinate, float tiling, float darken)
{
    float2 cell = floor(coordinate * max(tiling, 0.01) * 0.5);
    half alternate = (half)(frac((cell.x + cell.y) * 0.5) * 2.0);
    return color * lerp(saturate((half)darken), 1.0h, alternate);
}

float ESCompositePerceptualNoise(float value)
{
    #if defined(UNITY_COLORSPACE_GAMMA)
    return value;
    #else
    value = max(value, 0.0);
    return max(1.055 * pow(value, 0.416666667) - 0.055, 0.0);
    #endif
}

float ESCompositeSmokeMask(float noise, float2 uv, float vertexAlpha, float noiseFactor, float smoothness)
{
    return saturate(
        (noise - 1.0) * noiseFactor
        + ((vertexAlpha / 2.5) - distance(uv, float2(0.5, 0.5))) * 2.5 * smoothness);
}

half4 ESCompositeApplySmoke(half4 source, float mask, float smokeAlpha, float darkEdge)
{
    source.rgb = lerp(source.rgb, 0.0h, (1.0h - (half)mask) * max((half)darkEdge, 0.0h));
    source.a *= (half)mask * saturate((half)smokeAlpha);
    return source;
}

float ESCompositeFlameMask(
    float noise,
    float2 uv,
    float2 center,
    float2 direction,
    float radius,
    float smoothness,
    float noiseFactor,
    float noiseHeightFactor)
{
    float directionLength = length(direction);
    float2 resolvedDirection = directionLength > 0.0001
        ? direction / directionLength
        : float2(0.0, 1.0);
    float safeRadius = max(radius, 0.01);
    float heightCoordinate = dot(uv - center, resolvedDirection) + 0.2;
    float height = pow(abs(max(heightCoordinate, 0.0)), max(noiseHeightFactor, 0.0));
    float radial = (safeRadius - distance(uv, center)) / safeRadius;
    return saturate((noise * height * noiseFactor + radial) * smoothness);
}

#endif
