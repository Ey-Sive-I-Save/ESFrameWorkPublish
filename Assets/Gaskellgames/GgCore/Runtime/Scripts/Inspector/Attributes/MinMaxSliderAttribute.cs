using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class MinMaxSliderAttribute : PropertyAttribute
    {
        public float min;
        public float max;
        public string minLabel;
        public string maxLabel;
        public bool subLabels;
        
        public MinMaxSliderAttribute(float min, float max)
        {
            this.min = min;
            this.max = max;
            subLabels = false;
            minLabel = "";
            maxLabel = "";
        }
        
        public MinMaxSliderAttribute(float min, float max, bool subLabels)
        {
            this.min = min;
            this.max = max;
            this.subLabels = subLabels;
            minLabel = GgMaths.RoundFloat(min, 3).ToString();
            maxLabel = GgMaths.RoundFloat(max, 3).ToString();
        }

        public MinMaxSliderAttribute(float min, float max, string minLabel, string maxLabel)
        {
            this.min = min;
            this.max = max;
            subLabels = true;
            this.minLabel = minLabel;
            this.maxLabel = maxLabel;
        }
        
    } // class end
}
