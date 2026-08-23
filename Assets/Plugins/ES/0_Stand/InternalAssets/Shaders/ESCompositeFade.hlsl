#ifndef ES_COMPOSITE_FADE_INCLUDED
#define ES_COMPOSITE_FADE_INCLUDED

float2 ESCompositeFadeDirection(float rotationDegrees)
{
    float angle = radians(rotationDegrees);
    return float2(cos(angle), sin(angle));
}

float ESCompositeDirectionalFadeMask(float2 coordinate, float2 origin, float rotationDegrees)
{
    return saturate(dot(coordinate - origin, ESCompositeFadeDirection(rotationDegrees)) + 0.5);
}

float ESCompositeSourceFadeMask(float2 coordinate, float2 sourcePosition)
{
    return saturate(length(coordinate - sourcePosition) * 1.41421356);
}

float ESCompositeApplyFadeNoise(float mask, float noise, float noiseFactor)
{
    return saturate(mask + (noise - 0.5) * saturate(noiseFactor));
}

void ESCompositeEvaluateFade(
    float mask,
    float progress,
    float width,
    float edgeWidth,
    float invert,
    out float visibility,
    out float edge)
{
    float resolvedMask = lerp(mask, 1.0 - mask, saturate(invert));
    float safeWidth = max(width, 0.001);
    if (progress <= 0.0001)
    {
        visibility = 1.0;
        edge = 0.0;
        return;
    }
    if (progress >= 0.9999)
    {
        visibility = 0.0;
        edge = 0.0;
        return;
    }
    visibility = smoothstep(progress - safeWidth, progress + safeWidth, resolvedMask);
    edge = 1.0 - smoothstep(0.0, max(edgeWidth, 0.001), abs(resolvedMask - progress));
}

#endif
