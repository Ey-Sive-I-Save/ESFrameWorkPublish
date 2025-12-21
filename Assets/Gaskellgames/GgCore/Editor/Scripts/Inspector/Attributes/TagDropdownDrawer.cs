#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(TagDropdownAttribute), true)]
    public class TagDropdownDrawer : GgPropertyDrawer
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
            
            string propertyString = property.stringValue;
            if (string.IsNullOrEmpty(propertyString)) {propertyString = "Untagged"; } // first index is a special case: Untagged
            string[] displayNames = InternalEditorUtility.tags;
            int index = Mathf.Max(0, Array.IndexOf(displayNames, propertyString));
            
            if (GgGUI.EnumField(position, label, index, out int outputValue, displayNames, property.hasMultipleDifferentValues))
            {
                // adjust the actual string value of the property based on the selection
                property.stringValue = 1 <= outputValue ? displayNames[outputValue] : "";
            }
        }

        #endregion
        
    } // class end
}

#endif