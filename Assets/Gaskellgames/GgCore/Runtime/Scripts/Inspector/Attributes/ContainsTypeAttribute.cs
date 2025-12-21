using System;
using UnityEngine;

namespace Gaskellgames
{  
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ContainsTypeAttribute : PropertyAttribute
    {
         public Type type;
        public bool allowSceneObjects;

        public ContainsTypeAttribute(Type type, bool allowSceneObjects = true)
        {
            this.type = type;
            this.allowSceneObjects = allowSceneObjects;
        }

    } // class end
}
