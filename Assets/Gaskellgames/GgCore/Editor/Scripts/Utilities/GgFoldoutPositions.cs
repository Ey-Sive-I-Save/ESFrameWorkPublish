#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    [Serializable]
    public class GgFoldoutPositions
    {
        public Rect outline;
            
        public Rect background;
            
        public Rect header;
            
        public Rect label;
            
        public Rect field;
            
        public Rect subHeader;
            
        public Rect subHeaderBackground;
            
        public Rect content;
        
    } // class end
}

#endif