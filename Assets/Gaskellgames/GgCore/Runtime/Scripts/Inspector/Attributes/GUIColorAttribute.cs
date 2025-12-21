using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class GUIColorAttribute : PropertyAttribute
    {
        public GUIColorTarget target;
        
        public byte R;
        public byte G;
        public byte B;
        public byte A;
 
        public GUIColorAttribute()
        {
            R = 223;
            G = 179;
            B = 000;
            A = 255;
            target = GUIColorTarget.All;
        }
 
        public GUIColorAttribute(byte r = 000, byte g = 028, byte b = 045, byte a = 255, GUIColorTarget target = GUIColorTarget.All)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            this.target = target;
        }
        
    } // class end
}
