using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [Serializable]
    public class HierarchyData
    {
        [Tooltip("")]
        public int indentLevel;
        
        [Tooltip("")]
        public bool hasChild;
        
        [Tooltip("")]
        public bool isFinalChild;
        
        [Tooltip("")]
        public List<bool> parentIsFinalChild;
        
        [Tooltip("")]
        public List<Type> components;
        
        [Tooltip("")]
        public int componentCount;

        public HierarchyData(int indentLevel, bool hasChild, List<bool> parentIsFinalChild, bool isFinalChild, List<Type> components)
        {
            this.indentLevel = indentLevel;
            this.hasChild = hasChild;
            this.parentIsFinalChild = parentIsFinalChild;
            this.isFinalChild = isFinalChild;
            this.components = components;
            this.componentCount = components.Count;
        }

    } // class end
}
