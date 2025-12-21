#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>
    
    [CustomPropertyDrawer(typeof(Password))]
    public class PasswordDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // open property and get reference to instance
            EditorGUI.BeginProperty(position, label, property);

            // get reference to SerializeFields
            SerializedProperty password = property.FindPropertyRelative("password");

            // draw property
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            if (password != null)
            {
                password.stringValue = EditorGUI.PasswordField(position, password.stringValue);
            }

            // close property
            EditorGUI.EndProperty();
        }

    } // class end
}
#endif
