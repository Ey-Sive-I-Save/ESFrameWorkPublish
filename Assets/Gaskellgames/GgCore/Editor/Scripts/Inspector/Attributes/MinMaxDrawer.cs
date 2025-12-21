#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(MinMaxAttribute), true)]
    public class MinMaxDrawer : GgPropertyDrawer
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
            if (property.propertyType == SerializedPropertyType.Float)
            {
                MinMaxAttribute attributeAsType = AttributeAsType<MinMaxAttribute>();
                float limitedValue = property.floatValue;
                if (attributeAsType.max < property.floatValue)
                {
                    limitedValue = attributeAsType.max;
                }
                else if (property.floatValue < attributeAsType.min)
                {
                    limitedValue = attributeAsType.min;
                }

                if (GgGUI.FloatField(position, label, limitedValue, out float outputValue, property.hasMultipleDifferentValues))
                {
                    property.floatValue = outputValue;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                MinMaxAttribute attributeAsType = AttributeAsType<MinMaxAttribute>();
                int limitedValue = property.intValue;
                if ((int)attributeAsType.max < property.intValue)
                {
                    limitedValue = (int)attributeAsType.max;
                }
                else if (property.intValue < (int)attributeAsType.min)
                {
                    limitedValue = (int)attributeAsType.min;
                }

                if (GgGUI.IntField(position, label, limitedValue, out int outputValue, property.hasMultipleDifferentValues))
                {
                    property.intValue = outputValue;
                }
            }
            else
            {
                GgGUI.CustomPropertyField(position, property, label);
            }
        }

        #endregion

    } // class end
}

#endif