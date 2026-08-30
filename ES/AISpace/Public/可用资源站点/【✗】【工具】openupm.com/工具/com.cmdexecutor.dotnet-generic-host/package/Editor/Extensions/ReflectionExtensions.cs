using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Microsoft.Extensions.Hosting.Unity.Editor
{
    internal static class ReflectionExtensions
    {
        public static void SetFieldValue<T>(this SerializedProperty property, T value)
        {
            if (property.serializedObject == null)
                return;

            if (property.serializedObject.targetObject == null)
                return;

            var type = property.serializedObject.targetObject.GetType();
            var field = type.GetField(property.propertyPath, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null)
                throw new ArgumentNullException(property.propertyPath);

            field.SetValue(property.serializedObject.targetObject, value);
        }

        public static T GetFieldValue<T>(this SerializedProperty property)
        {
            if (property.serializedObject == null)
                return default;

            if (property.serializedObject.targetObject == null)
                return default;

            var type = property.serializedObject.targetObject.GetType();
            var field = type.GetField(property.propertyPath, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null)
                throw new ArgumentNullException(property.propertyPath);

            return (T) field.GetValue(property.serializedObject.targetObject);
        }

        // Gets value from SerializedProperty - even if value is nested
        public static object GetValue(this UnityEditor.SerializedProperty property)
        {
            object obj = property.serializedObject.targetObject;

            foreach (var path in property.propertyPath.Split('.'))
            {
                var type = obj.GetType();
                var field = type.GetField(path);
                
                if(field == null)
                    continue;
                
                obj = field.GetValue(obj);
            }

            return obj;
        }

        // Sets value from SerializedProperty - even if value is nested
        public static void SetValue(this UnityEditor.SerializedProperty property, object val)
        {
            object obj = property.serializedObject.targetObject;

            var list = new List<KeyValuePair<FieldInfo, object>>();

            foreach (var path in property.propertyPath.Split('.'))
            {
                var type = obj.GetType();
                var field = type.GetField(path);
                list.Add(new KeyValuePair<FieldInfo, object>(field, obj));
                obj = field.GetValue(obj);
            }

            // Now set values of all objects, from child to parent
            for (var i = list.Count - 1; i >= 0; --i)
            {
                list[i].Key.SetValue(list[i].Value, val);
                // New 'val' object will be parent of current 'val' object
                val = list[i].Value;
            }
        }

        public static Type[] GetInterfaces(this Type type, bool includeInherited)
        {
            if (includeInherited || type.BaseType == null)
                return type.GetInterfaces();

            return type.GetInterfaces().Except(type.BaseType.GetInterfaces()).ToArray();
        }
    }
}