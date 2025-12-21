using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class StringDropdownAttribute : PropertyAttribute
    {
        public string[] list { get; private set; }
        
        public StringDropdownAttribute(params string[] list)
        {
            this.list = list;
        }
        
    } // class end
}
