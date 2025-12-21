#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(MinMaxSliderAttribute), true)]
    public class MinMaxSliderDrawer : GgPropertyDrawer
    {
        #region GgPropertyHeight

        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            MinMaxSliderAttribute attributeAsType = AttributeAsType<MinMaxSliderAttribute>();
            return attributeAsType != null && attributeAsType.subLabels ? propertyHeight + (singleLineHeight * 0.4f) : propertyHeight;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGgGUI

        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            // draw label, min max slider and float fields
            if (fieldInfo.FieldType == typeof(Vector2))
            {
                MinMaxSliderAttribute attributeAsType = AttributeAsType<MinMaxSliderAttribute>();
                Vector2 minMax = new Vector2(attributeAsType.min, attributeAsType.max);
                if (GgGUI.MinMaxSlider(position, label, property.vector2Value, out Vector2 outputValue, minMax, property.hasMultipleDifferentValues))
                {
                    property.vector2Value = outputValue;
                }
                if (attributeAsType.subLabels) { DrawSubLabels(GetFieldPosition(position, label)); }
            }
            else if (fieldInfo.FieldType == typeof(Vector2Int))
            {
                MinMaxSliderAttribute attributeAsType = AttributeAsType<MinMaxSliderAttribute>();
                Vector2Int minMax = new Vector2Int((int)attributeAsType.min, (int)attributeAsType.max);
                if (GgGUI.MinMaxSliderInt(position, label, property.vector2IntValue, out Vector2Int outputValue, minMax, property.hasMultipleDifferentValues))
                {
                    property.vector2IntValue = outputValue;
                }
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
            MinMaxSliderAttribute attributeAsType = AttributeAsType<MinMaxSliderAttribute>();
            float subLabelPositionX = fieldPosition.x + (miniFieldWidth + standardSpacing);
            float subLabelPositionY = fieldPosition.y + (singleLineHeight * 0.75f);
            float subLabelWidth = fieldPosition.width - ((miniFieldWidth + standardSpacing) * 2);
            Rect subLabelRect = new Rect(subLabelPositionX, subLabelPositionY, subLabelWidth, singleLineHeight);
            EditorExtensions.DrawSubLabels(subLabelRect, attributeAsType.minLabel, attributeAsType.maxLabel);
        }
        
        #endregion
        
    } // class end
}

#endif