using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class LabelTextAttribute : PropertyAttribute
    {
        public string label;
        
        public LabelTextAttribute(string label)
        {
            this.label = label;
        }
        
    } // class end
}
