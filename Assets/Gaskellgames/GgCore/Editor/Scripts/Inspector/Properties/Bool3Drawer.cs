#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomPropertyDrawer(typeof(Bool3))]
    public class Bool3Drawer : PropertyDrawer
    {
        #region variables

        private SerializedProperty x;
        private SerializedProperty y;
        private SerializedProperty z;
        
        private float singleLine = EditorGUIUtility.singleLineHeight;
        
        #endregion

        //----------------------------------------------------------------------------------------------------
        
        #region Property Height
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label) - (singleLine * 1.1f);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region OnGUI

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // open property and get reference to instance
            EditorGUI.BeginProperty(position, label, property);

            // get reference to SerializeFields
            x = property.FindPropertyRelative("x");
            y = property.FindPropertyRelative("y");
            z = property.FindPropertyRelative("z");
            
            // draw property
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label.text, label.tooltip));
            GUILayout.Space(2);
            x.boolValue = EditorGUILayout.ToggleLeft("X", x.boolValue, GUILayout.Width(28), GUILayout.ExpandWidth(false));
            y.boolValue = EditorGUILayout.ToggleLeft("Y", y.boolValue, GUILayout.Width(28), GUILayout.ExpandWidth(false));
            z.boolValue = EditorGUILayout.ToggleLeft("Z", z.boolValue, GUILayout.Width(28), GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();

            // close property
            EditorGUI.EndProperty();
        }

        #endregion

    } // class end
}
#endif
