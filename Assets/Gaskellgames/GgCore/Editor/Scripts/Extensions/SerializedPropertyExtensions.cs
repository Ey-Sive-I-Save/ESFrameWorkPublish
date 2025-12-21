#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class SerializedPropertyExtensions
    {
        #region Comparisons

        /// <summary>
        /// Checks is the SerializedProperty equals the comparison object value
        /// </summary>
        /// <param name="property"></param>
        /// <param name="comparison"></param>
        /// <returns></returns>
        public static bool Equals(this SerializedProperty property, object comparison)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Generic:
                    GgLogs.Log(null, GgLogType.Error, "PropertyType [Generic] not supported: defaulting to false.");
                    break;
                
                case SerializedPropertyType.Integer:
                    return property.intValue == (int)comparison;
                
                case SerializedPropertyType.Boolean:
                    return property.boolValue == (bool)comparison;
                
                case SerializedPropertyType.Float:
                    return Mathf.Approximately(property.floatValue, (float)comparison);
                
                case SerializedPropertyType.String:
                    return property.stringValue == (string)comparison;
                
                case SerializedPropertyType.Color:
                    return property.colorValue == (Color)comparison;
                
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue == (Object)comparison;
                
                case SerializedPropertyType.LayerMask:
                    GgLogs.Log(null, GgLogType.Error, "PropertyType [LayerMask] not supported: defaulting to false.");
                    break;
                
                case SerializedPropertyType.Enum:
                    return property.intValue == (int)comparison;
                
                case SerializedPropertyType.Vector2:
                    return property.vector2Value == (Vector2)comparison;
                
                case SerializedPropertyType.Vector3:
                    return property.vector3Value == (Vector3)comparison;
                
                case SerializedPropertyType.Vector4:
                    return property.vector4Value == (Vector4)comparison;
                
                case SerializedPropertyType.Rect:
                    return property.rectValue == (Rect)comparison;
                
                case SerializedPropertyType.ArraySize:
                    return property.arraySize == (int)comparison;
                
                case SerializedPropertyType.Character:
                    return property.stringValue == ((char)comparison).ToString();
                
                case SerializedPropertyType.AnimationCurve:
                    return property.animationCurveValue.Equals((AnimationCurve)comparison);
                
                case SerializedPropertyType.Bounds:
                    return property.boundsValue == (Bounds)comparison;
                
                case SerializedPropertyType.Gradient:
                    GgLogs.Log(null, GgLogType.Error, "PropertyType [Gradient] not supported: defaulting to false.");
                    break;
                
                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue == (Quaternion)comparison;
                
                case SerializedPropertyType.ExposedReference:
                    GgLogs.Log(null, GgLogType.Error, "PropertyType [ExposedReference] not supported: defaulting to false.");
                    break;
                
                case SerializedPropertyType.FixedBufferSize:
                    GgLogs.Log(null, GgLogType.Error, "PropertyType [FixedBufferSize] not supported: defaulting to false.");
                    break;
                
                case SerializedPropertyType.Vector2Int:
                    return property.vector2IntValue == (Vector2Int)comparison;
                
                case SerializedPropertyType.Vector3Int:
                    return property.vector3IntValue == (Vector3Int)comparison;
                
                case SerializedPropertyType.RectInt:
                    return property.rectIntValue.Equals((RectInt)comparison);
                
                case SerializedPropertyType.BoundsInt:
                    return property.boundsIntValue == (BoundsInt)comparison;
                
                case SerializedPropertyType.ManagedReference:
                    GgLogs.Log(null, GgLogType.Error, "PropertyType [ManagedReference] not supported: defaulting to false.");
                    break;
                
                case SerializedPropertyType.Hash128:
                    return property.hash128Value == (Hash128)comparison;
                
                default:
                    GgLogs.Log(null, GgLogType.Error, "PropertyType [Unknown] not supported: defaulting to false.");
                    break;
            }
            
            // default value is false
            return false;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Get Field

        public static SerializedProperty GetField(this SerializedProperty property, string field)
        {
            // get the full relative property path of the field (inc. nested hiding)
            // Note: Use standard method when dealing with arrays
            string propertyPath = property.propertyPath;
            Regex replaceSearch = new Regex(property.name + "$");
            string conditionPath = replaceSearch.Replace(propertyPath, _ => field);
            SerializedProperty sourcePropertyValue = property.serializedObject.FindProperty(conditionPath);

            // if FindProperty failed => fall back to the standard method
            if (sourcePropertyValue == null)
            {
                // standard method (does not support nesting)
                sourcePropertyValue = property.serializedObject.FindProperty(field);
            }

            if (sourcePropertyValue == null)
            {
                GgLogs.Log(null, GgLogType.Warning, "SourcePropertyValue is Null");
            }
            return sourcePropertyValue;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region GetTypeValue

        /// <summary>
        /// Get the Type value for a SerializedProperty (e.g if the SerializedPropertyType is Integer, an int value will be returned)
        /// </summary>
        /// <param name="property"></param>
        /// <returns> Null if not able to get value for SerializedProperty's type </returns>
        public static object GetTypeValue(this SerializedProperty property)
        {
            if (property == null) { return null; }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Generic:
                    return null; // cannot handle Generic
                case SerializedPropertyType.Integer:
                    return property.intValue;
                case SerializedPropertyType.Boolean:
                    return property.boolValue;
                case SerializedPropertyType.Float:
                    return property.floatValue;
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Color:
                    return (Color32)property.colorValue;
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue;
                case SerializedPropertyType.LayerMask:
                    return (LayerMask)property.intValue;
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex;
                case SerializedPropertyType.Vector2:
                    return property.vector2Value;
                case SerializedPropertyType.Vector3:
                    return property.vector3Value;
                case SerializedPropertyType.Vector4:
                    return property.vector4Value;
                case SerializedPropertyType.Rect:
                    return property.rectValue;
                case SerializedPropertyType.ArraySize:
                    return property.arraySize;
                case SerializedPropertyType.Character:
                    return (char)property.intValue;
                case SerializedPropertyType.AnimationCurve:
                    return property.animationCurveValue;
                case SerializedPropertyType.Bounds:
                    return property.boundsValue;
                case SerializedPropertyType.Gradient:
                    PropertyInfo propertyInfo = typeof(SerializedProperty).GetProperty("gradientValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    return propertyInfo?.GetValue(property, null) as Gradient;
                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue;
                case SerializedPropertyType.ExposedReference:
                    return property.exposedReferenceValue;
                case SerializedPropertyType.FixedBufferSize:
                    return property.fixedBufferSize;
                case SerializedPropertyType.Vector2Int:
                    return property.vector2IntValue;
                case SerializedPropertyType.Vector3Int:
                    return property.vector3IntValue;
                case SerializedPropertyType.RectInt:
                    return property.rectIntValue;
                case SerializedPropertyType.BoundsInt:
                    return property.boundsIntValue;
                case SerializedPropertyType.ManagedReference:
                    return property.managedReferenceValue;
                case SerializedPropertyType.Hash128:
                    return property.hash128Value;
                default:
                    return null; // other?
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Get / Set Value
        
        /// <summary>
        /// Get/Set SerializedProperty Value code updated by Gaskellgames
        /// 
        /// Modified code from 'Unity Answers', in order to apply on nested List of type T or Array.
        /// douduck08: https://github.com/douduck08
        ///
        /// Original author: HiddenMonk & Johannes Deml
        /// Ref: http://answers.unity3d.com/questions/627090/convert-serializedproperty-to-custom-class.html
        /// </summary>
        
        private static readonly Regex Rgx = new Regex(@"\[\d+\]", RegexOptions.Compiled);

        public static T GetValue<T>(this SerializedProperty property) where T : class
        {
            object obj = property.serializedObject.targetObject;
            string path = property.propertyPath.Replace(".Array.data", "");
            string[] fieldStructure = path.Split('.');
            for (int i = 0; i < fieldStructure.Length; i++)
            {
                if (fieldStructure[i].Contains("["))
                {
                    int index = Convert.ToInt32(new string(fieldStructure[i].Where(char.IsDigit).ToArray()));
                    obj = GetFieldValueWithIndex(Rgx.Replace(fieldStructure[i], ""), obj, index);
                }
                else
                {
                    obj = GetFieldValue(fieldStructure[i], obj);
                }
            }

            return (T)obj;
        }

        public static bool SetValue<T>(this SerializedProperty property, T value) where T : class
        {
            object obj = property.serializedObject.targetObject;
            string path = property.propertyPath.Replace(".Array.data", "");
            string[] fieldStructure = path.Split('.');
            for (int i = 0; i < fieldStructure.Length - 1; i++)
            {
                if (fieldStructure[i].Contains("["))
                {
                    int index = Convert.ToInt32(new string(fieldStructure[i].Where(char.IsDigit).ToArray()));
                    obj = GetFieldValueWithIndex(Rgx.Replace(fieldStructure[i], ""), obj, index);
                }
                else
                {
                    obj = GetFieldValue(fieldStructure[i], obj);
                }
            }

            string fieldName = fieldStructure.Last();
            if (fieldName.Contains("["))
            {
                int index = Convert.ToInt32(new string(fieldName.Where(char.IsDigit).ToArray()));
                return SetFieldValueWithIndex(Rgx.Replace(fieldName, ""), obj, index, value);
            }
            else
            {
                return SetFieldValue(fieldName, obj, value);
            }
        }

        private static object GetFieldValue(string fieldName, object obj, BindingFlags bindings = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, bindings);
            if (field != null)
            {
                return field.GetValue(obj);
            }

            return default(object);
        }

        private static object GetFieldValueWithIndex(string fieldName, object obj, int index, BindingFlags bindings = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, bindings);
            if (field != null)
            {
                object list = field.GetValue(obj);
                if (list.GetType().IsArray)
                {
                    return ((object[])list)[index];
                }
                else if (list is IEnumerable)
                {
                    return ((IList)list)[index];
                }
            }

            return default(object);
        }

        public static bool SetFieldValue(string fieldName, object obj, object value, bool includeAllBases = false, BindingFlags bindings = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, bindings);
            if (field != null)
            {
                field.SetValue(obj, value);
                return true;
            }

            return false;
        }

        public static bool SetFieldValueWithIndex(string fieldName, object obj, int index, object value, bool includeAllBases = false, BindingFlags bindings = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, bindings);
            if (field != null)
            {
                object list = field.GetValue(obj);
                if (list.GetType().IsArray)
                {
                    ((object[])list)[index] = value;
                    return true;
                }
                else if (list is IEnumerable)
                {
                    ((IList)list)[index] = value;
                    return true;
                }
            }

            return false;
        }
        
        #endregion
        
    } // class end
}
#endif