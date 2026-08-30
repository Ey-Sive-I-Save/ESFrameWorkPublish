using System;
using System.Reflection;

namespace WellFired.Command.Unity.Runtime.UnityGui
{
    public static class GuiLayoutReflectionHelper
    {    
        private static FieldInfo GetFieldInfo(Type type, string fieldName)
        {
            FieldInfo fieldInfo;
            do
            {
                fieldInfo = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                type = type.BaseType;
            }
            while (fieldInfo == null && type != null);
            return fieldInfo;
        }
 
        public static object GetFieldValue(object obj, string fieldName)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            
            var objType = obj.GetType();
            var fieldInfo = GetFieldInfo(objType, fieldName);
            
            if (fieldInfo == null)
                throw new ArgumentOutOfRangeException(nameof(fieldName),
                    $"Couldn't find field {fieldName} in type {objType.FullName}");
            
            return fieldInfo.GetValue(obj);
        }
        
        public static void SetFieldValue(object obj, string fieldName, object val)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            var objType = obj.GetType();
            var fieldInfo = GetFieldInfo(objType, fieldName);
            if (fieldInfo == null)
                throw new ArgumentOutOfRangeException(nameof(fieldName),
                    $"Couldn't find field {fieldName} in type {objType.FullName}");
            fieldInfo.SetValue(obj, val);
        }
    }
}