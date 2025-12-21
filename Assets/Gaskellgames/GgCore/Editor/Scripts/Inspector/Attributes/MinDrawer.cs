#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(MinAttribute), true)]
    public class MinDrawer : GgPropertyDrawer
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
                float limitedValue = Mathf.Max(property.floatValue, AttributeAsType<MinAttribute>().min);
                if (GgGUI.FloatField(position, label, limitedValue, out float outputValue, property.hasMultipleDifferentValues))
                {
                    property.floatValue = outputValue;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                int limitedValue = Mathf.Max(property.intValue, (int)AttributeAsType<MinAttribute>().min);
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