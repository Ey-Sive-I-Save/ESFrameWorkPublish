#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(InLineButtonAttribute), true)]
    public class InLineButtonDrawer : GgPropertyDrawer
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
            InLineButtonAttribute attributeAsType = AttributeAsType<InLineButtonAttribute>();
            
            Rect buttonPosition = new Rect(position.xMax - singleLineHeight, position.y, singleLineHeight, singleLineHeight);
            Rect propertyPosition = new Rect(position.x, position.y, position.width - (singleLineHeight + standardSpacing), singleLineHeight);
            GUIContent guiContent = new GUIContent(EditorGUIUtility.IconContent("Button Icon").image, attributeAsType.methodName.NicifyName());
            
            GgGUI.CustomPropertyField(propertyPosition, property, label);
            if (!GUI.Button(buttonPosition, guiContent, EditorStyles.iconButton)) { return; }
            InvokeMethod(property, attributeAsType.methodName);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions
        
        private void InvokeMethod(SerializedProperty property, string methodName)
        {
            Object[] targets = property.serializedObject.targetObjects;
            foreach (Object target in targets)
            {
                Type type = target.GetType();
                MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (method == null)
                {
                    GgLogs.Log(null, GgLogType.Error, "Unable to find method '{0}' on type '{1}'.", methodName, type.Name);
                }
                else if (0 < method.GetParameters().Length)
                {
                    GgLogs.Log(null, GgLogType.Error, "Method '{0}' on type '{1}' cannot be called as it requires at least one parameter.", methodName, type.Name);
                }
                else
                {
                    method?.Invoke(target, null);
                }
            }
        }

        #endregion
        
    } // class end
}

#endif