#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(RangeAttribute), true)]
    public class RangeDrawer : GgPropertyDrawer
    {
        #region GgPropertyHeight

        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            RangeAttribute attributeAsType = AttributeAsType<RangeAttribute>();
            return attributeAsType != null && attributeAsType.subLabels ? propertyHeight + (singleLineHeight * 0.4f) : propertyHeight;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGgGUI

        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            if (property.propertyType == SerializedPropertyType.Float)
            {
                RangeAttribute attributeAsType = AttributeAsType<RangeAttribute>();
                Rect sliderRect = new Rect(position.x, position.y, position.width, singleLineHeight);
                GgGUI.Slider(sliderRect, property, label, new Vector2(attributeAsType.min, attributeAsType.max));
                if (attributeAsType.subLabels)
                {
                    DrawSubLabels(GetFieldPosition(position, label));
                }
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                RangeAttribute attributeAsType = AttributeAsType<RangeAttribute>();
                Rect sliderRect = new Rect(position.x, position.y, position.width, singleLineHeight);
                GgGUI.IntSlider(sliderRect, property, label, new Vector2Int((int)attributeAsType.min, (int)attributeAsType.max));
                if (attributeAsType.subLabels) { DrawSubLabels(GetFieldPosition(position, label)); }
            }
            else
            {
                GgGUI.CustomPropertyField(position, property, label);
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions
        
        private void DrawSubLabels(Rect fieldPosition)
        {
            RangeAttribute attributeAsType = AttributeAsType<RangeAttribute>();
            float subLabelPositionY = fieldPosition.y + (singleLineHeight * 0.75f);
            float subLabelWidth = fieldPosition.width - (miniFieldWidth + standardSpacing + standardSpacing);
            Rect subLabelRect = new Rect(fieldPosition.x, subLabelPositionY, subLabelWidth, singleLineHeight);
            EditorExtensions.DrawSubLabels(subLabelRect, attributeAsType.minLabel, attributeAsType.maxLabel);
        }
        
        #endregion
        
    } // class end
}
#endif