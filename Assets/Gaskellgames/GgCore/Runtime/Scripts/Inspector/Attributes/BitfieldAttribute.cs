using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class BitfieldAttribute : PropertyAttribute
    {
        public int length;

        public BitfieldAttribute(int length = 32)
        {
            this.length = Mathf.Clamp(length, 0, 32);
        }

    } // class end
}
