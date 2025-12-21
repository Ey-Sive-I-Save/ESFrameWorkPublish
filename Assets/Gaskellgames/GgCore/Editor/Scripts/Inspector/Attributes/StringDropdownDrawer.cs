#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(StringDropdownAttribute), true)]
    public class StringDropdownDrawer : GgPropertyDrawer
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
            if (property.propertyType != SerializedPropertyType.String)
            {
                GgGUI.CustomPropertyField(position, property, label);
                return;
            }
            
            string[] displayNames = AttributeAsType<StringDropdownAttribute>().list;
            int index = Mathf.Max(0, Array.IndexOf(displayNames, property.stringValue));
            if (GgGUI.EnumField(position, label, index, out int outputValue, displayNames, property.hasMultipleDifferentValues))
            {
                property.stringValue = displayNames[outputValue];
            }
        }

        #endregion
        
    } // class end
}

#endif