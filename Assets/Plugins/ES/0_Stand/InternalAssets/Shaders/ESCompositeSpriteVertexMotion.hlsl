#ifndef ES_COMPOSITE_SPRITE_VERTEX_MOTION_INCLUDED
#define ES_COMPOSITE_SPRITE_VERTEX_MOTION_INCLUDED

float ESWiggleAngle(float2 localUV, float timeValue)
{
    float phaseCoordinate = ESCompositeDirectionalCoordinate2D(
        localUV,
        _WiggleDirection.xy,
        float2(0, 1));
    return radians(_WiggleAmplitude)
        * sin(timeValue * _WiggleSpeed + phaseCoordinate * _WiggleFrequency);
}

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

float2 ESNormalizeDirection(float2 value, float2 fallback)
{
    float lengthSquared = dot(value, value);
    if (lengthSquared > 1e-8) return value * rsqrt(lengthSquared);
    float fallbackLengthSquared = dot(fallback, fallback);
    return fallbackLengthSquared > 1e-8
        ? fallback * rsqrt(fallbackLengthSquared)
        : float2(1, 0);
}

float2 ESScaleAlongDirection(float2 position, float2 direction, float alongScale, float acrossScale)
{
    float2 axis = ESNormalizeDirection(direction, float2(1, 0));
    float2 perpendicular = float2(-axis.y, axis.x);
    float along = dot(position, axis) * alongScale;
    float across = dot(position, perpendicular) * acrossScale;
    return axis * along + perpendicular * across;
}

float ESVertexAnchorCoordinate(float2 localUV)
{
    float2 axis = ESNormalizeDirection(_WindAnchorDirection.xy, float2(0, 1));
    float halfExtent = max(0.5 * (abs(axis.x) + abs(axis.y)), 1e-4);
    return saturate(dot(localUV - 0.5, axis) / (2.0 * halfExtent) + 0.5);
}

float3 ESApplyVertexMotion(float3 positionOS, float2 localUV)
{
    float timeValue = ESGetTime();
    float2 basePosition = positionOS.xy;
    if (_EnableSineMove > 0.5)
    {
        float2 moveFrequency = clamp(_SineMoveFrequency.xy, -32.0, 32.0);
        float2 moveOffset = clamp(_SineMoveOffset.xy, -8.0, 8.0);
        positionOS.xy += sin(timeValue * moveFrequency)
            * moveOffset * saturate(_SineMoveFade);
    }
    if (_EnableSquish > 0.5)
    {
        float squishWave = _SquishSpeed > 0.0001
            ? sin(timeValue * _SquishSpeed)
            : 1.0;
        float scaleX = max(0.2, 1.0 + squishWave * _SquishAmount * saturate(_SquishFade));
        positionOS.xy = ESScaleAlongDirection(
            positionOS.xy,
            _SquishDirection.xy,
            scaleX,
            rcp(scaleX));
    }
    if (_EnableWiggle > 0.5)
        positionOS.xy = ESRotate2D(positionOS.xy, ESWiggleAngle(localUV, timeValue));
    if (_EnableWind > 0.5)
    {
        float globalBlend = saturate(_WindGlobalInfluence) * step(0.5, _ESCompositeGlobalWindValid);
        float2 localDirection = ESNormalizeDirection(_WindDirection.xy, float2(1, 0));
        float2 globalDirection = ESNormalizeDirection(_ESCompositeGlobalWind.xy, localDirection);
        float2 direction = ESNormalizeDirection(
            lerp(localDirection, globalDirection, globalBlend),
            localDirection);
        float strength = lerp(1.0, max(0, _ESCompositeGlobalWind.z), globalBlend);
        float speed = lerp(1.0, max(0, _ESCompositeGlobalWind.w), globalBlend);
        float anchorCoordinate = ESVertexAnchorCoordinate(localUV);
        float anchorMask = saturate((anchorCoordinate - _WindAnchor) / max(1.0 - _WindAnchor, 1e-4));
        float phase = timeValue * _WindSpeed * speed
            + (dot(positionOS.xy, float2(-direction.y, direction.x)) + _ESWindPhaseOffset) * _WindFrequency;
        positionOS.xy += direction * sin(phase) * _WindAmplitude * strength * anchorMask;
    }
    if (abs(_ESInteractiveWindRotation) > 0.0001)
    {
        float anchorCoordinate = ESVertexAnchorCoordinate(localUV);
        float anchorMask = saturate((anchorCoordinate - _WindAnchor) / max(1.0 - _WindAnchor, 1e-4));
        float angle = radians(clamp(_ESInteractiveWindRotation, -89.0, 89.0)) * anchorMask;
        float bendHeight = max(abs(_ESInteractiveWindHeight), 0.0001);
        positionOS.x += sin(angle) * bendHeight * anchorMask;
        positionOS.y -= (1.0 - cos(angle)) * bendHeight * anchorMask;
    }
    if (abs(_ESInteractiveSquish) > 0.0001)
    {
        float squish = clamp(_ESInteractiveSquish, -0.8, 0.8);
        float scaleX = max(0.2, 1.0 + squish);
        positionOS.xy = ESScaleAlongDirection(
            positionOS.xy,
            _SquishDirection.xy,
            scaleX,
            rcp(scaleX));
    }
    if (_EnableVibrate > 0.5)
    {
        float phase = timeValue * _VibrateSpeed;
        float2 axis = ESNormalizeDirection(_VibrateDirection.xy, float2(1, 0));
        float2 perpendicular = float2(-axis.y, axis.x);
        positionOS.xy += (
            axis * sin(phase * 1.37)
            + perpendicular * cos(phase * 1.91)) * _VibrateAmplitude;
    }
    if (_EnableSineScale > 0.5)
    {
        float frequency = clamp(_SineScaleFrequency, -32.0, 32.0);
        float2 factor = clamp(_SineScaleFactor.xy, -4.0, 4.0);
        float wave = (sin(frequency * timeValue) + 1.0) * 0.5;
        positionOS.xy += basePosition * wave * factor;
    }
    return positionOS;
}

#endif
