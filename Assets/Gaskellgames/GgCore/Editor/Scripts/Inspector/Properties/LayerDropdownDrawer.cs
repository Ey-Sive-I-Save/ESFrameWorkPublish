#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace Gaskellgames.EditorOnly
{
    [CustomPropertyDrawer(typeof(LayerDropdown))]
    public class LayerDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, GUIContent.none, property);
            SerializedProperty layerIndex = property.FindPropertyRelative("m_LayerIndex");
            if (layerIndex != null)
            {
                layerIndex.intValue = EditorGUI.LayerField(position, label, layerIndex.intValue);
            }
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }
            EditorGUI.EndProperty();
        }
    } // class end
}
#endif