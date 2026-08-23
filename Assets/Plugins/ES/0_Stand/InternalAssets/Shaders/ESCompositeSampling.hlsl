#ifndef ES_COMPOSITE_SAMPLING_INCLUDED
#define ES_COMPOSITE_SAMPLING_INCLUDED

half4 ESCompositeGaussian3x3(half4 center, half4 axisSum, half4 diagonalSum)
{
    return center * 0.25h + axisSum * 0.125h + diagonalSum * 0.0625h;
}

half4 ESCompositeSharpen5(half4 center, half4 axisAverage, float amount, float threshold)
{
    half3 detail = center.rgb - axisAverage.rgb;
    float detailMagnitude = max(abs(detail.r), max(abs(detail.g), abs(detail.b)));
    float thresholdWidth = max(fwidth(detailMagnitude), 0.001);
    float response = smoothstep(max(0.0, threshold), max(0.0, threshold) + thresholdWidth, detailMagnitude);
    half4 result = center;
    result.rgb = max(0.0h, center.rgb + detail * max(0.0, amount) * response);
    return result;
}

#endif
