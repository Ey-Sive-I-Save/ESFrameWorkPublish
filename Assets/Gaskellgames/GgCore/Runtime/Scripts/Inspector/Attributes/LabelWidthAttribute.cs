using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class LabelWidthAttribute : PropertyAttribute
    {
        public int labelWidth;
        
        public LabelWidthAttribute(int labelWidth)
        {
            this.labelWidth = labelWidth;
        }
        
    } // class end
}
