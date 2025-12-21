#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(HighlightAttribute), true)]
    public class HighlightDrawer : GgPropertyDrawer
    {
        #region GgPropertyHeight
        
        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            return propertyHeight;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGgGUI
        
        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            GgGUI.CustomPropertyField(position, property, label);

            HighlightAttribute attributeAsType = AttributeAsType<HighlightAttribute>();
            
            Color32 outlineColor = new Color32(attributeAsType.R, attributeAsType.G, attributeAsType.B, attributeAsType.A);
            Rect topBorder = new Rect(position.xMin - 1, position.yMin - 1, position.width + 2, 1);
            EditorGUI.DrawRect(topBorder, outlineColor);
            Rect bottomBorder = new Rect(position.xMin - 1, position.yMax, position.width + 2, 1);
            EditorGUI.DrawRect(bottomBorder, outlineColor);
            Rect leftBorder = new Rect(position.xMin - 1, position.yMin - 1, 1, position.height + 2);
            EditorGUI.DrawRect(leftBorder, outlineColor);
            Rect rightBorder = new Rect(position.xMax, position.yMin - 1, 1, position.height + 2);
            EditorGUI.DrawRect(rightBorder, outlineColor);
        }
        
        #endregion
        
    } // class end
}

#endif