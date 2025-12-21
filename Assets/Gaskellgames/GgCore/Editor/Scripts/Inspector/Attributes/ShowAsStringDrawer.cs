#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    // <summary>
    // Code created by Gaskellgames: https://gaskellgames.com
    // </summary>

    [CustomPropertyDrawer(typeof(ShowAsStringAttribute), true)]
    public class ShowAsStringDrawer : GgPropertyDrawer
    {
        #region GgPropertyHeight

        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            return property.GetTypeValue() != null ? singleLineHeight + standardSpacing : propertyHeight;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region OnGgGUI

        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            ShowAsStringAttribute attributeAsType = AttributeAsType<ShowAsStringAttribute>();
            object propertyValue = property.GetTypeValue();
            
            if (propertyValue != null && attributeAsType != null)
            {
                Rect stringPosition = new Rect(position.x, position.y, position.width, singleLineHeight);
                GgGUI.StringField(stringPosition, label, propertyValue.ToString(), property.hasMultipleDifferentValues, attributeAsType.greyedOut);
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