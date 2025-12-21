using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class MinAttribute : PropertyAttribute
    {
        public float min;

        public MinAttribute(float min)
        {
            this.min = min;
        }

    } // class end
}
