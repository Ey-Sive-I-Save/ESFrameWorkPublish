#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    // <summary>
    // Code created by Gaskellgames: https://gaskellgames.com
    // </summary>
    
    [CustomPropertyDrawer(typeof(ToggleLeftAttribute), true)]
    public class ToggleLeftDrawer : GgPropertyDrawer
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
            if (property.propertyType != SerializedPropertyType.Boolean)
            {
                GgGUI.CustomPropertyField(position, property, label);
                return;
            }
            
            if (GgGUI.ToggleLeft(position, label, property.boolValue, out bool outputValue, property.hasMultipleDifferentValues))
            {
                property.boolValue = outputValue;
            }
        }

        #endregion

    } // class end
}
#endif