using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class RequiredAttribute : PropertyAttribute
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;
        
        public RequiredAttribute()
        {
            R = 255;
            G = 000;
            B = 000;
            A = 255;
        }
        
        public RequiredAttribute(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
        
    } // class end
}
