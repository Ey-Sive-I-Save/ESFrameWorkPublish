using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class AudioExtensions
    {
        /// <summary>
        /// Returns the paused state of the audio source
        /// </summary>
        /// <param name="audioSource"></param>
        /// <returns></returns>
        public static bool IsPaused(this AudioSource audioSource)
        {
            return !audioSource.isPlaying && audioSource.time != 0;
        }
        
        /// <summary>
        /// Get the accurate current playback time of the sampled audioSource to the nearest integer (i.e the active audio source's clip)
        /// </summary>
        /// <param name="audioSource"></param>
        /// <param name="beatsPerMinute"></param>
        /// <returns></returns>
        public static int GetCurrentSampledTimeInt(this AudioSource audioSource, int beatsPerMinute)
        {
            return Mathf.FloorToInt(audioSource.GetCurrentSampledTime(beatsPerMinute));
        }

        /// <summary>
        /// Get the accurate current playback time of the sampled audioSource given the clip's beatsPerMinute (i.e the active audio source's clip)
        /// </summary>
        /// <param name="audioSource"></param>
        /// <param name="beatsPerMinute"></param>
        /// <returns></returns>
        public static float GetCurrentSampledTime(this AudioSource audioSource, int beatsPerMinute)
        {
            float audioSourceTimeSamples = audioSource.timeSamples;
            float audioClipFrequency = audioSource.clip.frequency;
            float beatInterval = SecondsPerBeat(beatsPerMinute);

            return audioSourceTimeSamples / (audioClipFrequency * beatInterval);
        }
        
        /// <summary>
        /// Get the accurate total time of the sampled audio clip to the nearest integer (i.e the active audio source's clip)
        /// </summary>
        /// <param name="clip"></param>
        /// <returns></returns>
        public static int GetTotalSampledTimeInt(this AudioClip clip, int beatsPerMinute)
        {
            return Mathf.FloorToInt(clip.GetTotalSampledTime(beatsPerMinute));
        }

        /// <summary>
        /// Get the accurate total time of the sampled audio clip given the clip's beatsPerMinute (i.e the active audio source's clip)
        /// </summary>
        /// <param name="clip"></param>
        /// <param name="beatsPerMinute"></param>
        /// <returns></returns>
        public static float GetTotalSampledTime(this AudioClip clip, int beatsPerMinute)
        {
            float audioSourceTimeSamples = clip.samples;
            float audioClipFrequency = clip.frequency;
            float beatInterval = SecondsPerBeat(beatsPerMinute);

            return audioSourceTimeSamples / (audioClipFrequency * beatInterval);
        }

        /// <summary>
        /// Get the time in seconds between each beat.
        /// The step can be used to get beat timings for non-full beats [e.g step = 0.5 for a half beat]
        /// </summary>
        /// <param name="beatsPerMinute"></param>
        /// <param name="step"></param>
        /// <returns></returns>
        public static float SecondsPerBeat(float beatsPerMinute, float step = 1)
        {
            const float minute = 60f;
            return minute / (beatsPerMinute * step);
        }
        
        /// <summary>
        /// Convert from a linear volume value (0 to 1.2) to a Logarithmic volume value (-80db to 20db), where 0db is equal to a linear value of 1;
        /// </summary>
        /// <param name="linearValue"></param>
        /// <returns></returns>
        public static float LinearToLogarithmicVolume(float linearValue)
        {
            float logarithmicValue;
            
            if (1.0f <= linearValue)
            {
                // calculate 'over' volume (0db - 20db) by remapping 1.0 - 1.2 range;
                float remappedValue = GgMaths.RemapFloat(linearValue - 1, 0.0f, 0.2f, 0.0f, 20.0f);
                logarithmicValue = GgMaths.RoundFloat(remappedValue, 3);
            }
            else if (linearValue <= 0.0f)
            {
                // remap negative infinity to -80db
                logarithmicValue = -80.0f;
            }
            else
            {
                // direct conversion of linear (0 - 1) to log (-80db - 0db)
                logarithmicValue = GgMaths.RoundFloat(Mathf.Log10(linearValue) * 20, 3);
            }

            return logarithmicValue;
        }

        /// <summary>
        /// Convert from a Logarithmic volume value (-80db to 20db) to a linear volume value (0 to 1.2), where 0db is equal to a linear value of 1;
        /// </summary>
        /// <param name="logarithmicValue"></param>
        /// <returns></returns>
        public static float LogarithmicToLinearVolume(float logarithmicValue)
        {
            float linearValue;
            
            if (0 <= logarithmicValue)
            {
                // calculate 'over' volume (1.0 -1.2) by remapping 0db - 20db range;
                float remappedValue = GgMaths.RemapFloat(logarithmicValue, 0.0f, 20.0f, 0.0f, 0.2f);
                linearValue = GgMaths.RoundFloat(1 + remappedValue, 3);
            }
            else
            {
                // direct conversion of log (-80db - 0db) to linear (0 - 1)
                linearValue = GgMaths.RoundFloat(Mathf.Pow(10, logarithmicValue / 20), 3);
            }

            return linearValue;
        }

    } // class end
}
