#ifndef ES_COMPOSITE_ESNative_FADE_STACK_INCLUDED
#define ES_COMPOSITE_ESNative_FADE_STACK_INCLUDED

float2 ESCompositeESNativeRotate(float2 value, float rotationDegrees)
{
    float angle = ((rotationDegrees / 180.0) - 0.25) * 3.14159265;
    float sine;
    float cosine;
    sincos(angle, sine, cosine);
    return float2(
        value.x * cosine - value.y * sine,
        value.x * sine + value.y * cosine);
}

float ESCompositeESNativeDirectionalRatio(
    float2 coordinate,
    float rotation,
    float fade,
    float width,
    float noise,
    float noiseFactor)
{
    float2 rotated = ESCompositeESNativeRotate(coordinate, rotation);
    return (rotated.x + rotated.y + fade + noise * noiseFactor) / max(width, 0.001);
}

float ESCompositeESNativeResolveInvert(float value, float invert)
{
    return lerp(value, 1.0 - value, step(0.5, invert));
}

float2 ESCompositeApplyESNativeDirectionalDistortionUV(float2 uv, float2 coordinate)
{
    if (_EnableDirectionalDistortion < 0.5)
        return uv;

    float directionNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
        _UberNoiseTexture,
        sampler_UberNoiseTexture,
        coordinate * _DirectionalDistortionDistortionScale.xy).r);
    float angle = (directionNoise - 0.5) * 2.0
        * saturate(_DirectionalDistortionRandomDirection) * 3.14159265;
    float sine;
    float cosine;
    sincos(angle, sine, cosine);
    float2 distortion = _DirectionalDistortionDistortion.xy;
    distortion = float2(
        distortion.x * cosine - distortion.y * sine,
        distortion.x * sine + distortion.y * cosine);

    float fadeNoise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
        _UberNoiseTexture,
        sampler_UberNoiseTexture,
        coordinate * _DirectionalDistortionNoiseScale.xy).r);
    float rawVisibility = saturate(ESCompositeESNativeDirectionalRatio(
        coordinate,
        _DirectionalDistortionRotation,
        _DirectionalDistortionFade,
        _DirectionalDistortionWidth,
        fadeNoise,
        _DirectionalDistortionNoiseFactor));
    float visibility = ESCompositeESNativeResolveInvert(
        rawVisibility,
        _DirectionalDistortionInvert);
    return uv + distortion * (1.0 - visibility);
}

half3 ESCompositeApplyESNativeFadeStackColor(
    half3 color,
    float2 coordinate,
    out float visibility)
{
    visibility = 1.0;

    if (_EnableDirectionalDistortion > 0.5)
    {
        float noise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
            _UberNoiseTexture,
            sampler_UberNoiseTexture,
            coordinate * _DirectionalDistortionNoiseScale.xy).r);
        float rawVisibility = saturate(ESCompositeESNativeDirectionalRatio(
            coordinate,
            _DirectionalDistortionRotation,
            _DirectionalDistortionFade,
            _DirectionalDistortionWidth,
            noise,
            _DirectionalDistortionNoiseFactor));
        visibility *= ESCompositeESNativeResolveInvert(
            rawVisibility,
            _DirectionalDistortionInvert);
    }

    // ESNative applies these effects in this fixed order after sampling the sprite.
    if (_EnableFullAlphaDissolve > 0.5)
    {
        float width = max(_FullAlphaDissolveWidth, 0.001);
        float noise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
            _UberNoiseTexture,
            sampler_UberNoiseTexture,
            coordinate * _FullAlphaDissolveNoiseScale.xy).r);
        visibility *= saturate(
            (_FullAlphaDissolveFade * (1.0 + width) - noise) / width);
    }

    if (_EnableSourceAlphaDissolve > 0.5)
    {
        float noise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
            _UberNoiseTexture,
            sampler_UberNoiseTexture,
            coordinate * _SourceAlphaDissolveNoiseScale.xy).r);
        float distanceWithNoise = distance(_SourceAlphaDissolvePosition.xy, coordinate)
            + noise * _SourceAlphaDissolveNoiseFactor;
        float sourceVisibility = saturate(
            (_SourceAlphaDissolveFade - distanceWithNoise)
            / max(_SourceAlphaDissolveWidth, 0.001));
        visibility *= ESCompositeESNativeResolveInvert(
            sourceVisibility,
            _SourceAlphaDissolveInvert);
    }

    if (_EnableSourceGlowDissolve > 0.5)
    {
        float noise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
            _UberNoiseTexture,
            sampler_UberNoiseTexture,
            coordinate * _SourceGlowDissolveNoiseScale.xy).r);
        float distanceWithNoise = distance(_SourceGlowDissolvePosition.xy, coordinate)
            + noise * _SourceGlowDissolveNoiseFactor;
        float outer = step(distanceWithNoise, _SourceGlowDissolveFade);
        float inner = step(
            distanceWithNoise,
            _SourceGlowDissolveFade - max(_SourceGlowDissolveWidth, 0.001));
        color += max(outer - inner, 0.0) * _SourceGlowDissolveEdgeColor.rgb;
        visibility *= lerp(
            outer,
            1.0 - inner,
            step(0.5, _SourceGlowDissolveInvert));
    }

    if (_EnableDirectionalAlphaFade > 0.5)
    {
        float noise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
            _UberNoiseTexture,
            sampler_UberNoiseTexture,
            coordinate * _DirectionalAlphaFadeNoiseScale.xy).r);
        float directionalVisibility = saturate(ESCompositeESNativeDirectionalRatio(
            coordinate,
            _DirectionalAlphaFadeRotation,
            _DirectionalAlphaFadeFade,
            _DirectionalAlphaFadeWidth,
            noise,
            _DirectionalAlphaFadeNoiseFactor));
        visibility *= ESCompositeESNativeResolveInvert(
            directionalVisibility,
            _DirectionalAlphaFadeInvert);
    }

    if (_EnableDirectionalGlowFade > 0.5)
    {
        float noise = ESCompositePerceptualNoise(SAMPLE_TEXTURE2D(
            _UberNoiseTexture,
            sampler_UberNoiseTexture,
            coordinate * _DirectionalGlowFadeNoiseScale.xy).r);
        float ratio = max(ESCompositeESNativeDirectionalRatio(
            coordinate,
            _DirectionalGlowFadeRotation,
            _DirectionalGlowFadeFade,
            _DirectionalGlowFadeWidth,
            noise,
            _DirectionalGlowFadeNoiseFactor), 0.0);
        float resolvedRatio = ESCompositeESNativeResolveInvert(
            ratio,
            _DirectionalGlowFadeInvert);
        float directionalVisibility = step(0.1, resolvedRatio);
        color += _DirectionalGlowFadeEdgeColor.rgb
            * (directionalVisibility - step(1.0, resolvedRatio));
        visibility *= saturate(directionalVisibility);
    }

    return color;
}

#endif
