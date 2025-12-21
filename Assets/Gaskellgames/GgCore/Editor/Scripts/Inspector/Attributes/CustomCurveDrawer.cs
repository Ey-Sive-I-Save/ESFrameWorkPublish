#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomPropertyDrawer(typeof(CustomCurveAttribute), true)]
    public class CustomCurveDrawer : GgPropertyDrawer
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
            if (property.propertyType != SerializedPropertyType.AnimationCurve)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            
            Color32 lineColor = new Color32(AttributeAsType<CustomCurveAttribute>().R,
                AttributeAsType<CustomCurveAttribute>().G,
                AttributeAsType<CustomCurveAttribute>().B,
                AttributeAsType<CustomCurveAttribute>().A);

            GgGUI.CurveField(position, property, label, lineColor);
        }
        
        #endregion

    } // class end
}

#endif