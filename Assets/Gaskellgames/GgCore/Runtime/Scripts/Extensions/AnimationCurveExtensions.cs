using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class AnimationCurveExtensions
    {
        public static AnimationCurve Logarithmic(float timeStart, float valueStart, float timeEnd, float valueEnd)
        {
            // calculate timeScale: convert [timeStart, timeEnd] relative to [0, 1].
            float timeScale = Mathf.Abs(timeEnd - timeStart);

            // calculate valueScale: convert [valueStart, valueEnd] relative to [0, 1].
            float valueScale = Mathf.Abs(valueEnd - valueStart);

            // apply scale to 'default' log values
            if (timeStart > timeEnd && valueStart < valueEnd)
            {
                return new AnimationCurve(new Keyframe[10]
                    {
                        new Keyframe(timeStart, valueStart),
                        new Keyframe(timeStart + (0.500f * timeScale), 0.004f * valueScale),
                        new Keyframe(timeStart + (0.250f * timeScale), 0.008f * valueScale),
                        new Keyframe(timeStart + (0.125f * timeScale), 0.016f * valueScale),
                        new Keyframe(timeStart + (0.063f * timeScale), 0.031f * valueScale),
                        new Keyframe(timeStart + (0.032f * timeScale), 0.063f * valueScale),
                        new Keyframe(timeStart + (0.016f * timeScale), 0.125f * valueScale),
                        new Keyframe(timeStart + (0.008f * timeScale), 0.250f * valueScale),
                        new Keyframe(timeStart + (0.004f * timeScale), 0.500f * valueScale),
                        new Keyframe(timeEnd, valueEnd)
                    }
                );
            }
            else if (timeStart > timeEnd && valueStart > valueEnd)
            {
                return new AnimationCurve(new Keyframe[10]
                    {
                        new Keyframe(timeStart, valueStart),
                        new Keyframe(timeStart + (0.500f * timeScale), 0.500f * valueScale),
                        new Keyframe(timeStart + (0.250f * timeScale), 0.250f * valueScale),
                        new Keyframe(timeStart + (0.125f * timeScale), 0.125f * valueScale),
                        new Keyframe(timeStart + (0.063f * timeScale), 0.063f * valueScale),
                        new Keyframe(timeStart + (0.032f * timeScale), 0.032f * valueScale),
                        new Keyframe(timeStart + (0.016f * timeScale), 0.016f * valueScale),
                        new Keyframe(timeStart + (0.008f * timeScale), 0.008f * valueScale),
                        new Keyframe(timeStart + (0.004f * timeScale), 0.004f * valueScale),
                        new Keyframe(timeEnd, valueEnd)
                    }
                );
            }
            else if (timeStart < timeEnd && valueStart < valueEnd)
            {
                return new AnimationCurve(new Keyframe[10]
                    {
                        new Keyframe(timeStart, valueStart),
                        new Keyframe(timeStart + (0.004f * timeScale), 0.004f * valueScale),
                        new Keyframe(timeStart + (0.008f * timeScale), 0.008f * valueScale),
                        new Keyframe(timeStart + (0.016f * timeScale), 0.016f * valueScale),
                        new Keyframe(timeStart + (0.031f * timeScale), 0.031f * valueScale),
                        new Keyframe(timeStart + (0.063f * timeScale), 0.063f * valueScale),
                        new Keyframe(timeStart + (0.125f * timeScale), 0.125f * valueScale),
                        new Keyframe(timeStart + (0.250f * timeScale), 0.250f * valueScale),
                        new Keyframe(timeStart + (0.500f * timeScale), 0.500f * valueScale),
                        new Keyframe(timeEnd, valueEnd)
                    }
                );
            }
            else // timeStart < timeEnd && valueStart > valueEnd
            {
                return new AnimationCurve(new Keyframe[10]
                    {
                        new Keyframe(timeStart, valueStart),
                        new Keyframe(timeStart + (0.004f * timeScale), 0.500f * valueScale),
                        new Keyframe(timeStart + (0.008f * timeScale), 0.250f * valueScale),
                        new Keyframe(timeStart + (0.016f * timeScale), 0.125f * valueScale),
                        new Keyframe(timeStart + (0.031f * timeScale), 0.063f * valueScale),
                        new Keyframe(timeStart + (0.063f * timeScale), 0.032f * valueScale),
                        new Keyframe(timeStart + (0.125f * timeScale), 0.016f * valueScale),
                        new Keyframe(timeStart + (0.250f * timeScale), 0.008f * valueScale),
                        new Keyframe(timeStart + (0.500f * timeScale), 0.004f * valueScale),
                        new Keyframe(timeEnd, valueEnd)
                    }
                );
            }
        }

    } // class end
}
