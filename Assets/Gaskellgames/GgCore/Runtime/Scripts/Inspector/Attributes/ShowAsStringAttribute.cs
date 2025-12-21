using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ShowAsStringAttribute : PropertyAttribute
    {
        public bool greyedOut;

        public ShowAsStringAttribute(bool greyedOut = false)
        {
            this.greyedOut = greyedOut;
        }

    } // class end
}
