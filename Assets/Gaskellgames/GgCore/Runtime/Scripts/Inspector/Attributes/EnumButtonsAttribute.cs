using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class EnumButtonsAttribute : PropertyAttribute
    {
        public bool enumFlags;

        public EnumButtonsAttribute(bool enumFlags = false)
        {
            this.enumFlags = enumFlags;
        }

    } // class end
}

