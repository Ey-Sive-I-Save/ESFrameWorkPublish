#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class GgMenuTree
    {
        public string header = "";
        public Color underlineColor = new Color32(179, 179, 179, 255);
        public List<GgMenuTreePage> pages;
        
        private int selectionIndex;

        public int SelectionIndex
        {
            get => selectionIndex;
            internal set => selectionIndex = value;
        }
        
    } // class end
}

#endif