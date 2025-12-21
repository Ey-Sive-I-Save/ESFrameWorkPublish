using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code updated by Gaskellgames: https://github.com/Gaskellgames
    /// Original code from vertxxyz: https://gist.github.com/vertxxyz/5a00dbca58aee033b35be2e227e80f8d
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class GraphAttribute : PropertyAttribute
    {
        public Vector2 min;
        public Vector2 max;
        public string xAxis;
        public string yAxis;

        public GraphAttribute(string xAxis = "x-axis", string yAxis = "y-axis")
        {
            this.min = -Vector2.one;
            this.max = Vector2.one;
            this.xAxis = xAxis;
            this.yAxis = yAxis;
        }
        
        public GraphAttribute(float min, float max, string xAxis = "x-axis", string yAxis = "y-axis")
        {
            this.min = Vector2.one * min;
            this.max = Vector2.one * max;
            this.xAxis = xAxis;
            this.yAxis = yAxis;
        }
        
        public GraphAttribute(float minX, float maxX, float minY, float maxY, string xAxis = "x-axis", string yAxis = "y-axis")
        {
            this.min = new Vector2(minX, minY);
            this.max = new Vector2(maxX, maxY);
            this.xAxis = xAxis;
            this.yAxis = yAxis;
        }
        
    } // class end
}
