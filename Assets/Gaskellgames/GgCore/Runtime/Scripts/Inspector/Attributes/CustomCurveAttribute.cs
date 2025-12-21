using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class CustomCurveAttribute : PropertyAttribute
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;
 
        public CustomCurveAttribute()
        {
            R = 000;
            G = 179;
            B = 000;
            A = 255;
        }
 
        public CustomCurveAttribute(byte r = 000, byte g = 000, byte b = 000, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
        
    } // class end
}
