#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    // <summary>
    // Code created by Gaskellgames: https://gaskellgames.com
    // </summary>
    
    [CustomPropertyDrawer(typeof(AssetsOnlyAttribute), true)]
    public class AssetsOnlyDrawer : GgPropertyDrawer
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
            // quick return if wrong type
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            
            if (GgGUI.ObjectField(position, label, property.objectReferenceValue, out Object outputValue, fieldInfo.FieldType, property.hasMultipleDifferentValues, false))
            {
                property.objectReferenceValue = outputValue;
            }
        }
        
        #endregion
        
    } // class end
}
#endif