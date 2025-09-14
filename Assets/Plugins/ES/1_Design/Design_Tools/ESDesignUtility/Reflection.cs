using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {
        //反射
        public static class Reflection
        {
            public static T EasyGetField<T>(object o, string field)
            {
                FieldInfo fieldInfo = o.GetType().GetField(field);
                if (fieldInfo != null)
                {
                    return (T)fieldInfo.GetValue(o);
                }
                return default;
            }
            public static T EasyGetProperty<T>(object o, string field)
            {
                PropertyInfo propertyInfo = o.GetType().GetProperty(field);
                if (propertyInfo != null)
                {
                    return (T)propertyInfo.GetValue(o);
                }
                return default;
            }
            public static void EasySetField<T>(object o, string field, T t)
            {
                FieldInfo fieldInfo = o.GetType().GetField(field);
                if (fieldInfo != null && fieldInfo.FieldType.IsAssignableFrom(typeof(T)))
                {
                    fieldInfo.SetValue(o, t);
                }
            }
            public static void EasyHandleField<T>(object o, string field, T t, EnumCollect.HandleTwoNumber func, Type type = null)
            {
                FieldInfo fieldInfo = o.GetType().GetField(field);
                if (fieldInfo != null)
                {
                    //开始处理 
                    if (t is float f && fieldInfo.FieldType.IsAssignableFrom(typeof(float)))
                    {
                        float left = Convert.ToSingle(fieldInfo.GetValue(o));
                        float right = f;
                        fieldInfo.SetValue(o, ESDesignUtility.Function.FunctionForHandleTwoFloat(left, right, func));
                    }
                    else if (t is int i && fieldInfo.FieldType.IsAssignableFrom(typeof(int)))
                    {
                        int left = (int)Convert.ToSingle(fieldInfo.GetValue(o));
                        int right = i;
                        fieldInfo.SetValue(o, (int)ESDesignUtility.Function.FunctionForHandleTwoFloat(left, right, func));
                    }
                    else if (t is int e && typeof(Enum).IsAssignableFrom(fieldInfo.FieldType))
                    {

                        int left = (int)Convert.ToSingle(fieldInfo.GetValue(o));
                        int right = e.GetHashCode();
                        fieldInfo.SetValue(o, (int)ESDesignUtility.Function.FunctionForHandleTwoFloat(left, right, func));
                    }
                    else if (t is int lay && typeof(LayerMask).IsAssignableFrom(fieldInfo.FieldType))
                    {

                        int left = (LayerMask)fieldInfo.GetValue(o);//LayerMask.GetMask((LayerMask.LayerToName((LayerMask)fieldInfo.GetValue(o))));
                        int right = lay;
                        fieldInfo.SetValue(o, (LayerMask)ESDesignUtility.Function.FunctionForHandleTwoFloat(left, right, func));
                    }


                }
            }
            public static void EasySetProperty<T>(object o, string field, T t)
            {
                PropertyInfo propertyInfo = o.GetType().GetProperty(field);
                if (propertyInfo != null && propertyInfo.PropertyType.IsAssignableFrom(typeof(T)))
                {
                    propertyInfo.SetValue(o, t);
                }
            }
            public static void EasyHandleProperty<T>(object o, string field, T t, EnumCollect.HandleTwoNumber func)
            {
                PropertyInfo propertyInfo = o.GetType().GetProperty(field);
                if (propertyInfo != null)
                {
                    //开始处理 
                    if (t is float f && propertyInfo.PropertyType.IsAssignableFrom(typeof(float)))
                    {
                        float left = (int)Convert.ToSingle(propertyInfo.GetValue(o));
                        float right = f;
                        propertyInfo.SetValue(o, ESDesignUtility.Function.FunctionForHandleTwoFloat(left, right, func));
                    }
                    //开始处理 
                    else if (t is int i && propertyInfo.PropertyType.IsAssignableFrom(typeof(int)))
                    {
                        int left = Convert.ToInt32(propertyInfo.GetValue(o));
                        int right = i;
                        propertyInfo.SetValue(o, (int)ESDesignUtility.Function.FunctionForHandleTwoFloat(left, right, func));
                    }
                    else if (t is int e && typeof(Enum).IsAssignableFrom(propertyInfo.PropertyType))
                    {
                        Debug.Log("Yes");
                        int left = (int)Convert.ToSingle(propertyInfo.GetValue(o));
                        int right = e.GetHashCode();
                        propertyInfo.SetValue(o, (int)ESDesignUtility.Function.FunctionForHandleTwoFloat(left, right, func));
                    }

                }
            }
            public static T EasyGetMethod<T>(object o, string method) where T : Delegate
            {
                MethodInfo methodInfo = o.GetType().GetMethod(method);
                if (methodInfo != null)
                {
                    return (T)methodInfo.CreateDelegate(typeof(T));
                }
                return default;
            }
            public static void EasyInvokeMethod(object o, string method, params object[] objects)
            {
                MethodInfo methodInfo = o.GetType().GetMethod(method);
                if (methodInfo != null)
                {

                    methodInfo.Invoke(o, objects);
                }
            }
        }
    }
}

