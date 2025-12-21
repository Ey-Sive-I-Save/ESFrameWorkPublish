#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    // <summary>
    // Code created by Gaskellgames: https://gaskellgames.com
    // </summary>

    [CustomPropertyDrawer(typeof(ShowAsTagAttribute), true)]
    public class ShowAsTagDrawer : GgPropertyDrawer
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
            if (property.GetTypeValue() != null)
            {
                Rect stringPosition = new Rect(position.x, position.y, position.width, singleLineHeight);
                GgGUI.TagField(stringPosition, label, property.GetTypeValue().ToString(), property.hasMultipleDifferentValues);
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