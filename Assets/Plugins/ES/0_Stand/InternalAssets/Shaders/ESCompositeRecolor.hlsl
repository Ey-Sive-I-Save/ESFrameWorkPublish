#ifndef ES_COMPOSITE_RECOLOR_INCLUDED
#define ES_COMPOSITE_RECOLOR_INCLUDED

half ESCompositeSelectChannel(half4 value, half channel)
{
    if (channel < 0.5h) return value.r;
    if (channel < 1.5h) return value.g;
    if (channel < 2.5h) return value.b;
    return value.a;
}

half3 ESCompositeRecolorRGB(half3 source, half3 redColor, half3 greenColor, half3 blueColor)
{
    return source.r * redColor + source.g * greenColor + source.b * blueColor;
}

half3 ESCompositeRecolorRGBYCP(
    half3 source,
    half3 redColor,
    half3 greenColor,
    half3 blueColor,
    half3 yellowColor,
    half3 cyanColor,
    half3 purpleColor)
{
    half redWeight = max(source.r - max(source.g, source.b), 0.0h);
    half greenWeight = max(source.g - max(source.r, source.b), 0.0h);
    half blueWeight = max(source.b - max(source.r, source.g), 0.0h);
    half yellowWeight = max(min(source.r, source.g) - source.b, 0.0h);
    half cyanWeight = max(min(source.g, source.b) - source.r, 0.0h);
    half purpleWeight = max(min(source.r, source.b) - source.g, 0.0h);
    half neutralWeight = min(source.r, min(source.g, source.b));

    return neutralWeight.xxx
        + redColor * redWeight
        + greenColor * greenWeight
        + blueColor * blueWeight
        + yellowColor * yellowWeight
        + cyanColor * cyanWeight
        + purpleColor * purpleWeight;
}

#endif
