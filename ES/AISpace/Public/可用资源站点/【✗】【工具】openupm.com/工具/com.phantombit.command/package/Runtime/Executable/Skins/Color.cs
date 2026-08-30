using JetBrains.Annotations;

namespace WellFired.Command.Skins
{
    public struct Color
    {
        [PublicAPI]
        public byte Red { get; }
        
        [PublicAPI]
        public byte Green { get; }
        
        [PublicAPI]
        public byte Blue { get; }

        public Color(byte red, byte green, byte blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }
    }
}