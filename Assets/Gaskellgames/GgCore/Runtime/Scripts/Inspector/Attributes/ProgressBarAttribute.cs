using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ProgressBarAttribute : PropertyAttribute
    {
        public bool readOnly;
        public float maxValue;
        public string label;
        public byte R;
        public byte G;
        public byte B;
        public byte A;
        
        public ProgressBarAttribute(bool readOnly = false)
        {
            this.readOnly = readOnly;
            maxValue = 100;
            label = "";
            R = 028;
            G = 079;
            B = 128;
            A = 255;
        }
        
        public ProgressBarAttribute(float maxValue, bool readOnly = false)
        {
            this.readOnly = readOnly;
            this.maxValue = maxValue;
            label = "";
            R = 028;
            G = 079;
            B = 128;
            A = 255;
        }
        
        public ProgressBarAttribute(string label, bool readOnly = false)
        {
            this.readOnly = readOnly;
            maxValue = 100;
            this.label = label;
            R = 028;
            G = 079;
            B = 128;
            A = 255;
        }
        
        public ProgressBarAttribute(byte r, byte g, byte b, byte a = 255, bool readOnly = false)
        {
            this.readOnly = readOnly;
            maxValue = 100;
            label = "";
            R = r;
            G = g;
            B = b;
            A = a;
        }
        
        public ProgressBarAttribute(float maxValue, string label, bool readOnly = false)
        {
            this.readOnly = readOnly;
            this.maxValue = maxValue;
            this.label = label;
            R = 028;
            G = 079;
            B = 128;
            A = 255;
        }
        
        public ProgressBarAttribute(float maxValue, byte r, byte g, byte b, byte a = 255, bool readOnly = false)
        {
            this.readOnly = readOnly;
            this.maxValue = maxValue;
            label = "";
            R = r;
            G = g;
            B = b;
            A = a;
        }
        
        public ProgressBarAttribute(string label, byte r, byte g, byte b, byte a = 255, bool readOnly = false)
        {
            this.readOnly = readOnly;
            maxValue = 100;
            this.label = label;
            R = r;
            G = g;
            B = b;
            A = a;
        }
        
        public ProgressBarAttribute(float maxValue, string label, byte r, byte g, byte b, byte a = 255, bool readOnly = false)
        {
            this.readOnly = readOnly;
            this.maxValue = maxValue;
            this.label = label;
            R = r;
            G = g;
            B = b;
            A = a;
        }
        
    } // class end
}
