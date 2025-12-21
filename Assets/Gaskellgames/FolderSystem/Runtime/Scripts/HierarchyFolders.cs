#if GASKELLGAMES
using UnityEngine;

namespace Gaskellgames.FolderSystem
{
    /// <summary>
    /// Code updated by Gaskellgames
    /// </summary>
    
    [AddComponentMenu("Gaskellgames/Folder System/Hierarchy Folders")]
    public class HierarchyFolders : GgMonoBehaviour
    {
        #region Variables
        
        public enum TextAlignment
        {
            Left,
            Center
        }
        
        [SerializeField]
        [Tooltip("Font style to be applied to HierarchyFolder text.")]
        public FontStyle textStyle = FontStyle.BoldAndItalic;
        
        [SerializeField]
        [Tooltip("Alignment to be applied to HierarchyFolder text.")]
        public TextAlignment textAlignment = TextAlignment.Left;
        
        [SerializeField]
        [Tooltip("Toggles whether to use a custom color for the text.")]
        public bool customText = false;
        
        [SerializeField]
        [Tooltip("Custom color to be used for the text.")]
        public Color32 textColor = InspectorExtensions.textNormalColor;
        
        [SerializeField]
        [Tooltip("Toggles whether to use a custom color for the icon.")]
        public bool customIcon = false;
        
        [SerializeField]
        [Tooltip("Custom color to be used for the icon.")]
        public Color32 iconColor = InspectorExtensions.textNormalColor;
        
        [SerializeField]
        [Tooltip("Toggles whether to use a custom color for the highlight (Background).")]
        public bool customHighlight = true;
        
        [SerializeField]
        [Tooltip("Custom color to be used for the highlight (Background).")]
        public Color32 highlightColor = InspectorExtensions.buttonHoverBorderColor;

        #endregion
        
    } // class end
}
        
#endif