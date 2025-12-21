#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gaskellgames.EditorOnly
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com unless otherwise stated
    /// </summary>
	
    public static class PropertyDrawerExtensions
    {
        #region SerializedPropertyObjectType

        /// <summary>
        /// Gets the Object Type of a SerializedProperty value.
        /// </summary>
        /// <param name="property">The SerializedProperty to find the value type for.</param>
        /// <param name="type">The Type if found.</param>
        /// <returns></returns>
        public static bool TryGetObjectType(this SerializedProperty property, out Type type)
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                string typeAsString = property.type;
                Match match = Regex.Match(typeAsString, @"PPtr<\$(.*?)>");
                if (match.Success) { typeAsString = match.Groups[1].Value; }
                type = typeof(Object).Assembly.GetType("UnityEngine." + typeAsString);
                if (type != null && !string.IsNullOrEmpty(type.ToString())) { return true; }
                    
                // TODO - get none unity namespace Object type
                return false;
            }

            type = null;
            return false;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region FindCustomPropertyDrawers

        /// <summary>
        /// Code updated by Gaskellgames: https://github.com/Gaskellgames
        /// Original code created by ghysc: https://github.com/ghysc/SwitchAttribute
        /// </summary>
        
        private static Dictionary<Type, PropertyDrawer> customDrawers = new Dictionary<Type, PropertyDrawer>();

        /// <summary>
        /// Returns the custom property drawer for given property, if one exists.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="customDrawer"></param>
        /// <returns></returns>
        public static bool TryFindCustomPropertyDrawer(SerializedProperty property, out PropertyDrawer customDrawer)
        {
            if (!property.TryGetPropertyType(out Type propertyType))
            {
                customDrawer = null;
                return false;
            }
            
            // cache value for future checks
            if (!customDrawers.ContainsKey(propertyType))
            {
                if (TryFindCustomDrawer(propertyType, out PropertyDrawer foundDrawer))
                {
                    customDrawers.Add(propertyType, foundDrawer);
                }
            }
            
            customDrawer = customDrawers[propertyType];
            return customDrawers[propertyType] != null;
        }
        
        /// <summary>
        /// Gets the Type of a serialized property using reflection.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="propertyType"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        private static bool TryGetPropertyType(this SerializedProperty property, out Type propertyType, BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Instance)
        {
            if (property?.serializedObject?.targetObject == null)
            {
                propertyType = null;
                return false;
            }
            Type parentType = property.serializedObject.targetObject.GetType();
            string[] splitPropertyPath = property.propertyPath.Split('.');
            FieldInfo fieldInfo = parentType.GetField(splitPropertyPath[0], flags);

            if (fieldInfo == null)
            {
                propertyType = null;
                return false;
            }
            Type fieldType = fieldInfo.FieldType;
            Type arrayType = fieldType.GetElementType();
            
            for (int i = 1; i < splitPropertyPath.Length; i++)
            {
                if (fieldType.IsArray && i + 2 < splitPropertyPath.Length && splitPropertyPath[i] == "Array")
                {
                    string dataString = splitPropertyPath[i + 1];
                    Regex pattern = new Regex(@"^data\[\d+\]$");
                    Match match = pattern.Match(dataString);
                    if (!match.Success) { continue; }
                    if (i + 1 == splitPropertyPath.Length - 1) { break; }
                    if (arrayType != null) { fieldInfo = arrayType.GetField(splitPropertyPath[i + 2], flags); }
                    i += 2;
                }
                else
                {
                    fieldInfo = fieldType.GetField(splitPropertyPath[i], flags);
                }
            }

            if (fieldInfo == null)
            {
                propertyType = null;
                return false;
            }
            
            propertyType = fieldInfo.FieldType;
            return true;
        }
        
        /// <summary>
        /// Returns custom property drawer for type if one could be found, or null if 
        /// no custom property drawer could be found. Does not use cached values, so it's resource intensive.
        /// </summary>
        private static bool TryFindCustomDrawer(Type propertyType, out PropertyDrawer customDrawer)
        {
            foreach (Assembly assem in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type candidate in assem.GetTypes())
                {
                    FieldInfo typeField = typeof(CustomPropertyDrawer).GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo childField = typeof(CustomPropertyDrawer).GetField("m_UseForChildren", BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (Attribute a in candidate.GetCustomAttributes(typeof(CustomPropertyDrawer)))
                    {
                        if (a.GetType().IsSubclassOf(typeof(CustomPropertyDrawer)) || a.GetType() == typeof(CustomPropertyDrawer))
                        {
                            CustomPropertyDrawer drawerAttribute = (CustomPropertyDrawer)a;
                            Type drawerType = (Type)typeField.GetValue(drawerAttribute);
                            if (drawerType == propertyType ||
                                ((bool)childField.GetValue(drawerAttribute) && propertyType.IsSubclassOf(drawerType)) ||
                                ((bool)childField.GetValue(drawerAttribute) && TypeExtensions.IsGenericSubclass(drawerType, propertyType)))
                            {
                                if (candidate.IsSubclassOf(typeof(PropertyDrawer)))
                                {
                                    customDrawer = (PropertyDrawer)Activator.CreateInstance(candidate);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
			
            customDrawer = null;
            return true;
        }

        #endregion

    } // class end
}

#endif