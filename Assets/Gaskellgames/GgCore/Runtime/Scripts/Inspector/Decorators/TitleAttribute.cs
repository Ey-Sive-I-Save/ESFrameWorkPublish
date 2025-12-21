using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>
    
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class)]
    public class TitleAttribute : PropertyAttribute
    {
        public readonly string heading;
        public readonly string subHeading;
        public readonly Alignment alignment = Alignment.Left;
        public readonly Color32 colour;
        public Color32 darkColour => new Color32((byte)(colour.r * 0.631f),(byte)(colour.g * 0.631f),(byte)(colour.b * 0.631f), colour.a);

        public TitleAttribute(string heading = "", string subHeading = "", Alignment alignment = Alignment.Left, byte r = 179, byte g = 179, byte b = 179, byte a = 255)
#if UNITY_6000_0_OR_NEWER
            : base(true)
#endif
        {
            this.heading = heading;
            this.subHeading = subHeading;
            this.alignment = alignment;
            this.colour = new Color32(r, g, b, a);
        }

    } // class end
}
