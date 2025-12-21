#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(MaxAttribute), true)]
    public class MaxDrawer : GgPropertyDrawer
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
                float limitedValue = Mathf.Min(property.floatValue, AttributeAsType<MaxAttribute>().max);
                if (GgGUI.FloatField(position, label, limitedValue, out float outputValue, property.hasMultipleDifferentValues))
                {
                    property.floatValue = outputValue;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                int limitedValue = Mathf.Min(property.intValue, (int)AttributeAsType<MaxAttribute>().max);
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