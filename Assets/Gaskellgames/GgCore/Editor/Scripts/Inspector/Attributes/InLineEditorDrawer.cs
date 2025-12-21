#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(InLineEditorAttribute), true)]
    public class InLineEditorDrawer : GgPropertyDrawer
    {
        #region Variables
        
        private Editor inLineEditor;

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region GgPropertyHeight

        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                (fieldInfo.FieldType == typeof(Component) || fieldInfo.FieldType == typeof(ScriptableObject)))
            {
                inLineEditor = Editor.CreateEditor(property.objectReferenceValue);
                return propertyHeight - (singleLineHeight + standardSpacing);
            }
            
            return propertyHeight;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region OnGgGUI

        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (!TryDrawInLineEditor(property, label))
                {
                    GgGUI.CustomPropertyField(position, property, label);
                }
            }
            else
            {
                GgGUI.CustomPropertyField(position, property, label);
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Functions

        private bool TryDrawInLineEditor(SerializedProperty property, GUIContent label)
        {
            if (fieldInfo.FieldType == typeof(Component))
            {
                property.isExpanded = EditorExtensions.BeginFoldoutObjectGroupNestable<Component>(property, label, property.isExpanded, EditorStyles.helpBox);
                if (property.isExpanded)
                {
                    DrawInLineEditor(property);
                }
                EditorExtensions.EndFoldoutGroupNestable();
                return true;
            }
            
            if (fieldInfo.FieldType == typeof(ScriptableObject))
            {
                property.isExpanded = EditorExtensions.BeginFoldoutObjectGroupNestable<ScriptableObject>(property, label, property.isExpanded, EditorStyles.helpBox);
                if (property.isExpanded)
                {
                    DrawInLineEditor(property);
                }
                EditorExtensions.EndFoldoutGroupNestable();
                return true;
            }
            
            return false;
        }

        private void DrawInLineEditor(SerializedProperty property)
        {
            if (property.objectReferenceValue)
            {
                if (inLineEditor)
                {
                    EditorExtensions.DrawInspectorLine(InspectorExtensions.backgroundShadowColor, -2);
                    if (inLineEditor.target)
                    {
                        inLineEditor.OnInspectorGUI(true);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Error: InLineEditor asset cannot be created.", MessageType.Error);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Warning: Reference object asset is null.", MessageType.Warning);
            }
        }

        #endregion

    } // class end
}

#endif