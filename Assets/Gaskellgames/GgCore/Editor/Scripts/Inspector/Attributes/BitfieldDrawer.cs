#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(BitfieldAttribute), true)]
    public class BitfieldDrawer : GgPropertyDrawer
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
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            
            string bitfieldString = property.hasMultipleDifferentValues
                ? "--------------------------------------"
                : GgMaths.BitfieldAsString(property.intValue, AttributeAsType<BitfieldAttribute>().length);
            GgGUI.StringField(position, label, bitfieldString, property.hasMultipleDifferentValues, true);
        }

        #endregion

    } // class end
}
#endif