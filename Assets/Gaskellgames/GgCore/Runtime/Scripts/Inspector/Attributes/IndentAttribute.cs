using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class IndentAttribute : PropertyAttribute
    {
        public bool specificIndentLevel;
        
        public int indentLevel;

        public IndentAttribute()
        {
            this.specificIndentLevel = false;
            this.indentLevel = 0;
        }

        public IndentAttribute(int indentLevel)
        {
            this.specificIndentLevel = true;
            this.indentLevel = indentLevel;
        }

    } // class end
}
