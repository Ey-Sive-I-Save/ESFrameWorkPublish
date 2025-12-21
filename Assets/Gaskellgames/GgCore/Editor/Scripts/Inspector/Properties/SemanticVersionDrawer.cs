#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomPropertyDrawer(typeof(SemanticVersion))]
    public class SemanticVersionDrawer : PropertyDrawer
    {
        #region variables

        private SerializedProperty major;
        private SerializedProperty minor;
        private SerializedProperty patch;
        
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
            major = property.FindPropertyRelative("major");
            minor = property.FindPropertyRelative("minor");
            patch = property.FindPropertyRelative("patch");
            
            // draw property
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label.text, label.tooltip));
            GUILayout.Space(2);
            EditorGUIUtility.labelWidth = 35;
            major.intValue = EditorGUILayout.IntField(new GUIContent("Major"), major.intValue, GUILayout.Width(70), GUILayout.ExpandWidth(true));
            minor.intValue = EditorGUILayout.IntField(new GUIContent("Minor"), minor.intValue, GUILayout.Width(70), GUILayout.ExpandWidth(true));
            patch.intValue = EditorGUILayout.IntField(new GUIContent("Patch"), patch.intValue, GUILayout.Width(70), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            // close property
            EditorGUI.EndProperty();
        }

        #endregion

    } // class end
}
#endif
