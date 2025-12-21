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

    [CustomPropertyDrawer(typeof(OnValueChangedAttribute), true)]
    public class OnValueChangedDrawer : GgPropertyDrawer
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
            EditorGUI.BeginChangeCheck();
            GgGUI.CustomPropertyField(position, property, label);
            if (EditorGUI.EndChangeCheck())
            {
                OnValueChangedAttribute attributeAsType = AttributeAsType<OnValueChangedAttribute>();
                Object[] targets = property.serializedObject.targetObjects;
                foreach (Object target in targets)
                {
                    Type type = target.GetType();
                    MethodInfo method = type.GetMethod(attributeAsType.methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (method == null)
                    {
                        GgLogs.Log(null, GgLogType.Error, "Unable to find method '{0}' on type '{1}'.", attributeAsType.methodName, type.Name);
                    }
                    else if (0 < method.GetParameters().Length)
                    {
                        GgLogs.Log(null, GgLogType.Error, "Method '{0}' on type '{1}' cannot be called as it requires at least one parameter.", attributeAsType.methodName, type.Name);
                    }
                    else
                    {
                        InvokeMethodNextFrame(method, target, null);
                    }
                }
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Functions

        private async void InvokeMethodNextFrame(MethodInfo method, object target, object[] parameters)
        {
            if (await GgTask.WaitUntilNextFrame() != TaskResultType.Complete) { return; }
            method?.Invoke(target, parameters);
        }

        #endregion

    } // class end
}

#endif