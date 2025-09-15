using System;
using System.Collections.Concurrent;
using System.Reflection;
using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {

        //反射
        public static class Reflection
        {
            #region 缓存表
            public static readonly ConcurrentDictionary<string, FieldInfo> FieldCache = new ConcurrentDictionary<string, FieldInfo>();
            public static readonly ConcurrentDictionary<string, PropertyInfo> PropertyCache = new ConcurrentDictionary<string, PropertyInfo>();
            public static readonly ConcurrentDictionary<string, MethodInfo> MethodCache = new ConcurrentDictionary<string, MethodInfo>();
            public static readonly ConcurrentDictionary<Type, FieldInfo[]> TypeFieldsCache = new ConcurrentDictionary<Type, FieldInfo[]>();
            public static readonly ConcurrentDictionary<Type, PropertyInfo[]> TypePropertiesCache = new ConcurrentDictionary<Type, PropertyInfo[]>();
            public static readonly ConcurrentDictionary<Type, MethodInfo[]> TypeMethodsCache = new ConcurrentDictionary<Type, MethodInfo[]>();
            #endregion

            #region 缓存键构造
            /// <summary>
            /// 构建查询键
            /// </summary>
            /// <param name="type">类型</param>
            /// <param name="memberName">成员名</param>
            /// <returns></returns>
            public static string GenerateCacheKey(Type type, string memberName)
            {
                return $"{type.FullName}_{memberName}";
            }
            /// <summary>
            /// 构建重载方法名键
            /// </summary>
            /// <param name="type">类型</param>
            /// <param name="methodName">方法名</param>
            /// <param name="parameterTypes">重载参数</param>
            /// <returns></returns>
            public static string GenerateMethodCacheKey(Type type, string methodName, Type[] parameterTypes)
            {
                string typeKey = type.FullName;
                string paramsKey = parameterTypes != null ? string.Join("_", Array.ConvertAll(parameterTypes, t => t.FullName)) : "null";
                return $"{typeKey}_{methodName}_{paramsKey}";
            }
            #endregion

            #region object字段操作
            /// <summary>
            /// 直接反射获得字段值
            /// </summary>
            /// <param name="obj">对象</param>
            /// <param name="fieldName">字段名</param>
            /// <returns></returns>
            public static object GetField(object obj, string fieldName)
            {
                if (obj == null)
                {
                    Debug.LogWarning("获取字段值时，对象不能为 null");
                    return null;
                }

                if (string.IsNullOrEmpty(fieldName))
                {
                    Debug.LogWarning("字段名不能为空");
                    return null;
                }

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = $"{type.FullName}_{fieldName}";

                    FieldInfo field = FieldCache.GetOrAdd(cacheKey, key =>
                    {
                        FieldInfo info = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        return info;
                    });

                    if (field == null)
                    {
                        Debug.LogWarning($"在类型 {type.Name} 中未找到字段: {fieldName}");
                        return null;
                    }

                    return field.GetValue(obj);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"获取字段值失败: {ex.Message}");
                    return null;
                }
            }
            /// <summary>
            /// 直接反射设置字段值
            /// </summary>
            /// <param name="obj">对象</param>
            /// <param name="fieldName">字段名</param>
            /// <param name="value">值</param>
            /// <returns></returns>
            public static bool SetField(object obj, string fieldName, object value)
            {
                if (obj == null)
                {
                    Debug.LogWarning("设置字段值时，对象不能为 null");
                    return false;
                }

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = $"{type.FullName}_{fieldName}";

                    FieldInfo field = FieldCache.GetOrAdd(cacheKey, key =>
                    {
                        FieldInfo info = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        return info;
                    });

                    if (field == null)
                    {
                        Debug.LogWarning($"在类型 {type.Name} 中未找到字段: {fieldName}");
                        return false;
                    }

                    // 检查类型兼容性
                    if (value != null && !field.FieldType.IsAssignableFrom(value.GetType()))
                    {
                        // 尝试基本类型转换
                        value = _ConvertBasicTypes(value, field.FieldType);
                        if (value == null)
                        {
                            Debug.LogWarning($"字段类型 {field.FieldType.Name} 与值类型 {value.GetType().Name} 不兼容");
                            return false;
                        }
                    }

                    field.SetValue(obj, value);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"设置字段值失败: {ex.Message}");
                    return false;
                }
            }
            #endregion

            #region object属性操作
            /// <summary>
            /// 直接反射获得属性值
            /// </summary>
            /// <param name="obj">对象</param>
            /// <param name="propertyName">属性名</param>
            /// <returns></returns>
            public static object GetProperty(object obj, string propertyName)
            {
                if (obj == null)
                {
                    Debug.LogWarning("获取属性值时，对象不能为 null");
                    return null;
                }

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = $"{type.FullName}_{propertyName}";

                    PropertyInfo property = PropertyCache.GetOrAdd(cacheKey, key =>
                    {
                        return type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    });

                    if (property == null)
                    {
                        Debug.LogWarning($"在类型 {type.Name} 中未找到属性: {propertyName}");
                        return null;
                    }

                    if (!property.CanRead)
                    {
                        Debug.LogWarning($"属性 {propertyName} 不可读");
                        return null;
                    }

                    return property.GetValue(obj, null);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"获取属性值失败: {ex.Message}");
                    return null;
                }
            }
            /// <summary>
            /// 直接反射设置属性值
            /// </summary>
            /// <param name="obj">对象</param>
            /// <param name="propertyName">属性名</param>
            /// <returns></returns>
            public static bool SetProperty(object obj, string propertyName, object value)
            {
                if (obj == null)
                {
                    Debug.LogWarning("设置属性值时，对象不能为 null");
                    return false;
                }

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = $"{type.FullName}_{propertyName}";

                    PropertyInfo property = PropertyCache.GetOrAdd(cacheKey, key =>
                    {
                        return type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    });

                    if (property == null)
                    {
                        Debug.LogWarning($"在类型 {type.Name} 中未找到属性: {propertyName}");
                        return false;
                    }

                    if (!property.CanWrite)
                    {
                        Debug.LogWarning($"属性 {propertyName} 不可写");
                        return false;
                    }

                    // 检查类型兼容性
                    if (value != null && !property.PropertyType.IsAssignableFrom(value.GetType()))
                    {
                        // 尝试基本类型转换
                        value = _ConvertBasicTypes(value, property.PropertyType);
                        if (value == null)
                        {
                            Debug.LogWarning($"属性类型 {property.PropertyType.Name} 与值类型 {value.GetType().Name} 不兼容");
                            return false;
                        }
                    }

                    property.SetValue(obj, value, null);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"设置属性值失败: {ex.Message}");
                    return false;
                }
            }
            #endregion

            #region 辅助方法

            /// <summary>
            /// 基本类型转换
            /// </summary>
            private static object _ConvertBasicTypes(object value, Type targetType)
            {
                if (value == null)
                    return null;

                try
                {
                    // 处理可空类型
                    if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        targetType = Nullable.GetUnderlyingType(targetType);
                    }

                    // 字符串转换
                    if (targetType == typeof(string))
                    {
                        return value.ToString();
                    }

                    // 数值类型转换
                    if (targetType == typeof(int) || targetType == typeof(float) ||
                        targetType == typeof(double) || targetType == typeof(long))
                    {
                        return Convert.ChangeType(value, targetType);
                    }

                    // 枚举转换
                    if (targetType.IsEnum)
                    {
                        if (value is string strValue)
                        {
                            return Enum.Parse(targetType, strValue);
                        }
                        return Enum.ToObject(targetType, value);
                    }

                    // 如果已经是目标类型或可赋值类型，直接返回
                    if (targetType.IsAssignableFrom(value.GetType()))
                    {
                        return value;
                    }

                    return null;
                }
                catch
                {
                    return null;
                }
            }
            #endregion

            #region 字段泛型操作
            /// <summary>
            /// 泛型反射获得字段值
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="obj"></param>
            /// <param name="fieldName">字段名</param>
            /// <returns></returns>
            public static T GetField<T>(object obj, string fieldName)
            {
                if (obj == null)
                {
                    Debug.LogWarning("获取字段值时，对象不能为 null");
                    return default;
                }

                if (string.IsNullOrEmpty(fieldName))
                {
                    Debug.LogWarning("字段名不能为空");
                    return default;
                }

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = GenerateCacheKey(type, fieldName);

                    // 从缓存获取或添加 FieldInfo
                    FieldInfo field = FieldCache.GetOrAdd(cacheKey, key =>
                    {
                        FieldInfo info = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        return info;
                    });

                    if (field == null)
                    {
                        Debug.LogWarning($"在类型 {type.Name} 中未找到字段: {fieldName}");
                        return default;
                    }

                    // 检查类型兼容性
                    if (!typeof(T).IsAssignableFrom(field.FieldType))
                    {
                        Debug.LogWarning($"字段类型 {field.FieldType.Name} 与目标类型 {typeof(T).Name} 不兼容");
                        return default;
                    }

                    return (T)field.GetValue(obj);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"获取字段值失败: {ex.Message}");
                    return default;
                }
            }
            /// <summary>
            /// 泛型反射设置字段值
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="obj"></param>
            /// <param name="fieldName">字段名</param>
            /// <param name="value"></param>
            /// <returns></returns>
            public static bool SetField<T>(object obj, string fieldName, T value)
            {
                if (obj == null)
                {
                    Debug.LogWarning("设置字段值时，对象不能为 null");
                    return false;
                }

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = GenerateCacheKey(type, fieldName);

                    // 从缓存获取或添加 FieldInfo
                    FieldInfo field = FieldCache.GetOrAdd(cacheKey, key =>
                    {
                        FieldInfo info = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        return info;
                    });

                    if (field == null)
                    {
                        Debug.LogWarning($"在类型 {type.Name} 中未找到字段: {fieldName}");
                        return false;
                    }

                    // 检查类型兼容性
                    if (!field.FieldType.IsAssignableFrom(typeof(T)))
                    {
                        Debug.LogWarning($"字段类型 {field.FieldType.Name} 与值类型 {typeof(T).Name} 不兼容");
                        return false;
                    }

                    field.SetValue(obj, value);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"设置字段值失败: {ex.Message}");
                    return false;
                }
            }
            #endregion

            #region 属性泛型操作
            /// <summary>
            /// 泛型获得属性值
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="obj"></param>
            /// <param name="propertyName">属性名</param>
            /// <returns></returns>
            public static T GetProperty<T>(object obj, string propertyName)
            {
                if (obj == null)
                {
                    Debug.LogWarning("获取属性值时，对象不能为 null");
                    return default;
                }

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = GenerateCacheKey(type, propertyName);

                    // 从缓存获取或添加 PropertyInfo
                    PropertyInfo property = PropertyCache.GetOrAdd(cacheKey, key =>
                    {
                        return type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    });

                    if (property == null)
                    {
                        Debug.LogWarning($"在类型 {type.Name} 中未找到属性: {propertyName}");
                        return default;
                    }

                    if (!property.CanRead)
                    {
                        Debug.LogWarning($"属性 {propertyName} 不可读");
                        return default;
                    }

                    // 检查类型兼容性
                    if (!typeof(T).IsAssignableFrom(property.PropertyType))
                    {
                        Debug.LogWarning($"属性类型 {property.PropertyType.Name} 与目标类型 {typeof(T).Name} 不兼容");
                        return default;
                    }

                    return (T)property.GetValue(obj);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"获取属性值失败: {ex.Message}");
                    return default;
                }
            }
            /// <summary>
            /// 泛型设置属性值
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="obj"></param>
            /// <param name="propertyName">属性名</param>
            /// <param name="value"></param>
            /// <returns></returns>
            public static bool SetProperty<T>(object obj, string propertyName, T value)
            {
                if (obj == null)
                {
                    Debug.LogWarning("设置属性值时，对象不能为 null");
                    return false;
                }

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = GenerateCacheKey(type, propertyName);

                    // 从缓存获取或添加 PropertyInfo
                    PropertyInfo property = PropertyCache.GetOrAdd(cacheKey, key =>
                    {
                        return type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    });

                    if (property == null)
                    {
                        Debug.LogWarning($"在类型 {type.Name} 中未找到属性: {propertyName}");
                        return false;
                    }

                    if (!property.CanWrite)
                    {
                        Debug.LogWarning($"属性 {propertyName} 不可写");
                        return false;
                    }

                    // 检查类型兼容性
                    if (!property.PropertyType.IsAssignableFrom(typeof(T)))
                    {
                        Debug.LogWarning($"属性类型 {property.PropertyType.Name} 与值类型 {typeof(T).Name} 不兼容");
                        return false;
                    }

                    property.SetValue(obj, value);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"设置属性值失败: {ex.Message}");
                    return false;
                }
            }
            #endregion

            #region 方法操作
            /// <summary>
            /// 调用方法(泛型返回值)
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="obj"></param>
            /// <param name="methodName">方法名</param>
            /// <param name="parameters">参数...</param>
            /// <returns></returns>
            public static T InvokeMethodReturn<T>(object obj, string methodName, params object[] parameters)
            {
                if (obj == null)
                {
                    Debug.LogWarning("调用方法时，对象不能为 null");
                    return default;
                }

                try
                {
                    Type type = obj.GetType();
                    Type[] paramTypes = parameters != null ? Array.ConvertAll(parameters, p => p?.GetType()) : Type.EmptyTypes;
                    string cacheKey = GenerateMethodCacheKey(type, methodName, paramTypes);

                    // 从缓存获取或添加 MethodInfo
                    MethodInfo method = MethodCache.GetOrAdd(cacheKey, key =>
                    {
                        return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, paramTypes, null);
                    });

                    if (method == null)
                    {
                        Debug.LogWarning($"在类型 {type.Name} 中未找到方法: {methodName}");
                        return default;
                    }

                    object result = method.Invoke(obj, parameters);

                    // 处理返回值
                    if (result == null)
                    {
                        if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
                        {
                            Debug.LogWarning($"方法返回 null，但目标类型 {typeof(T).Name} 是不可空值类型");
                            return default;
                        }
                        return default;
                    }

                    return (T)result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"调用方法失败: {ex.Message}");
                    return default;
                }
            }
            /// <summary>
            /// 调用方法
            /// </summary>
            /// <param name="obj"></param>
            /// <param name="methodName"></param>
            /// <param name="parameters"></param>
            /// <returns></returns>
            public static bool InvokeMethod(object obj, string methodName, params object[] parameters)
            {
                if (obj == null)
                {
                    Debug.LogWarning("调用方法时，对象不能为 null");
                    return false;
                }

                try
                {
                    Type type = obj.GetType();
                    Type[] paramTypes = parameters != null ? Array.ConvertAll(parameters, p => p?.GetType()) : Type.EmptyTypes;
                    string cacheKey = GenerateMethodCacheKey(type, methodName, paramTypes);

                    // 从缓存获取或添加 MethodInfo
                    MethodInfo method = MethodCache.GetOrAdd(cacheKey, key =>
                    {
                        return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, paramTypes, null);
                    });

                    if (method == null)
                    {
                        Debug.LogWarning($"在类型 {type.Name} 中未找到方法: {methodName}");
                        return false;
                    }

                    method.Invoke(obj, parameters);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"调用方法失败: {ex.Message}");
                    return false;
                }
            }
            #endregion

            #region 类型信息获取
            /// <summary>
            /// 获得一个类型的所有字段
            /// </summary>
            /// <param name="type"></param>
            /// <param name="bindingFlags">要求字段的可见性</param>
            /// <returns></returns>
            public static FieldInfo[] GetFields(Type type, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            {
                if (type == null)
                {
                    Debug.LogWarning("类型不能为 null");
                    return Array.Empty<FieldInfo>();
                }

                return TypeFieldsCache.GetOrAdd(type, t => t.GetFields(bindingFlags));
            }
            /// <summary>
            /// 获得一个属性的所有属性
            /// </summary>
            /// <param name="type"></param>
            /// <param name="bindingFlags">要求属性的可见性</param>
            /// <returns></returns>
            public static PropertyInfo[] GetProperties(Type type, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            {
                if (type == null)
                {
                    Debug.LogWarning("类型不能为 null");
                    return Array.Empty<PropertyInfo>();
                }

                return TypePropertiesCache.GetOrAdd(type, t => t.GetProperties(bindingFlags));
            }
            /// <summary>
            /// 获得一个属性的所有方法
            /// </summary>
            /// <param name="type"></param>
            /// <param name="bindingFlags">要求方法的可见性</param>
            /// <returns></returns>
            public static MethodInfo[] GetMethods(Type type, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            {
                if (type == null)
                {
                    Debug.LogWarning("类型不能为 null");
                    return Array.Empty<MethodInfo>();
                }

                return TypeMethodsCache.GetOrAdd(type, t => t.GetMethods(bindingFlags));
            }

            #endregion

            #region 工具方法

            /// <summary>
            /// 清空所有缓存
            /// </summary>
            public static void ClearAllCache()
            {
                FieldCache.Clear();
                PropertyCache.Clear();
                MethodCache.Clear();
                TypeFieldsCache.Clear();
                TypePropertiesCache.Clear();
                TypeMethodsCache.Clear();
                Debug.Log("已清空反射缓存");
            }
            /// <summary>
            /// 获取缓存统计信息
            /// </summary>
            public static string GetCacheCountStates()
            {
                return $"字段缓存: {FieldCache.Count}, 属性缓存: {PropertyCache.Count}, 方法缓存: {MethodCache.Count}, " +
                       $"类型字段缓存: {TypeFieldsCache.Count}, 类型属性缓存: {TypePropertiesCache.Count}, 类型方法缓存: {TypeMethodsCache.Count}";
            }

            #endregion


        }
    }
}

