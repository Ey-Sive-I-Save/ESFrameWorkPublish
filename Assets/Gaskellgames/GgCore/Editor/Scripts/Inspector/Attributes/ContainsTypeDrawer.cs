#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomPropertyDrawer(typeof(ContainsTypeAttribute), true)]
    public class ContainsTypeDrawer : GgPropertyDrawer
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
            
            if (GgGUI.ObjectField(position, label, property.objectReferenceValue, out Object outputValue, fieldInfo.FieldType, property.hasMultipleDifferentValues, AttributeAsType<ContainsTypeAttribute>().allowSceneObjects))
            {
                GameObject gameObjectReference = outputValue as GameObject;
                if (ContainsComponentOfType(AttributeAsType<ContainsTypeAttribute>().type, gameObjectReference))
                {
                    property.objectReferenceValue = outputValue;
                }
                else if (gameObjectReference)
                {
                    GgLogs.Log(null, GgLogType.Warning, "Reference object [{0}] does not contain component of type [{1}]", gameObjectReference.name, AttributeAsType<ContainsTypeAttribute>().type);
                }
                else
                {
                    property.objectReferenceValue = null;
                }
            }
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions

        private bool ContainsComponentOfType(Type type, GameObject gameObject)
        {
            if (type == null || gameObject == null) { return false; }
            
            var component = gameObject.GetComponent(type);
            return component != null;
        }

        #endregion

    } // class end
}
#endif