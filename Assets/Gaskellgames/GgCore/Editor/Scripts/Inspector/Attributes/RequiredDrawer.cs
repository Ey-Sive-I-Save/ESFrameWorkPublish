#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(RequiredAttribute), true)]
    public class RequiredDrawer : GgPropertyDrawer
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
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && !property.hasMultipleDifferentValues)
            {
                RequiredAttribute attributeAsType = AttributeAsType<RequiredAttribute>();
                GUI.backgroundColor = new Color32(attributeAsType.R, attributeAsType.G, attributeAsType.B, attributeAsType.A);
                GgGUI.CustomPropertyField(position, property, label);
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