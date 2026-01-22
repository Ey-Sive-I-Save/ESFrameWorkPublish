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
            private static readonly ConcurrentDictionary<string, FieldInfo> FieldCache = new ConcurrentDictionary<string, FieldInfo>();
            private static readonly ConcurrentDictionary<string, PropertyInfo> PropertyCache = new ConcurrentDictionary<string, PropertyInfo>();
            private static readonly ConcurrentDictionary<string, MethodInfo> MethodCache = new ConcurrentDictionary<string, MethodInfo>();

            // 注意：BindingFlags 会影响反射结果，因此必须参与缓存键，避免不同 flags 取到错误缓存。
            private static readonly ConcurrentDictionary<(Type type, BindingFlags flags), FieldInfo[]> TypeFieldsCache = new ConcurrentDictionary<(Type, BindingFlags), FieldInfo[]>();
            private static readonly ConcurrentDictionary<(Type type, BindingFlags flags), PropertyInfo[]> TypePropertiesCache = new ConcurrentDictionary<(Type, BindingFlags), PropertyInfo[]>();
            private static readonly ConcurrentDictionary<(Type type, BindingFlags flags), MethodInfo[]> TypeMethodsCache = new ConcurrentDictionary<(Type, BindingFlags), MethodInfo[]>();
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
                // parameterTypes 可能包含 null（例如参数值为 null 时无法推断类型），因此需要容错。
                string paramsKey = parameterTypes != null
                    ? string.Join("_", Array.ConvertAll(parameterTypes, t => t?.FullName ?? "null"))
                    : "null";
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
            /// 尝试反射获取字段值（不打日志）。
            /// </summary>
            public static bool TryGetField(object obj, string fieldName, out object value)
            {
                value = null;
                if (obj == null || string.IsNullOrEmpty(fieldName)) return false;

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = $"{type.FullName}_{fieldName}";

                    FieldInfo field = FieldCache.GetOrAdd(cacheKey, _ =>
                        type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));

                    if (field == null) return false;

                    value = field.GetValue(obj);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>
            /// 尝试反射获取字段值（泛型，不打日志）。
            /// </summary>
            public static bool TryGetField<T>(object obj, string fieldName, out T value)
            {
                value = default;
                if (!TryGetField(obj, fieldName, out object raw)) return false;

                if (raw is T t)
                {
                    value = t;
                    return true;
                }

                return false;
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
                        string originalValueTypeName = value?.GetType().Name ?? "null";
                        // 尝试基本类型转换
                        value = _ConvertBasicTypes(value, field.FieldType);
                        if (value == null)
                        {
                            Debug.LogWarning($"字段类型 {field.FieldType.Name} 与值类型 {originalValueTypeName} 不兼容，且无法进行基础类型转换。");
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

            /// <summary>
            /// 尝试反射设置字段值（不打日志）。
            /// </summary>
            public static bool TrySetField(object obj, string fieldName, object value)
            {
                if (obj == null || string.IsNullOrEmpty(fieldName)) return false;

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = $"{type.FullName}_{fieldName}";

                    FieldInfo field = FieldCache.GetOrAdd(cacheKey, _ =>
                        type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));

                    if (field == null) return false;

                    // null 写入值类型（非Nullable）直接失败
                    if (value == null)
                    {
                        if (field.FieldType.IsValueType && Nullable.GetUnderlyingType(field.FieldType) == null)
                            return false;

                        field.SetValue(obj, null);
                        return true;
                    }

                    if (!field.FieldType.IsAssignableFrom(value.GetType()))
                    {
                        object converted = _ConvertBasicTypes(value, field.FieldType);
                        if (converted == null) return false;
                        value = converted;
                    }

                    field.SetValue(obj, value);
                    return true;
                }
                catch
                {
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
            /// 尝试反射获取属性值（不打日志）。
            /// </summary>
            public static bool TryGetProperty(object obj, string propertyName, out object value)
            {
                value = null;
                if (obj == null || string.IsNullOrEmpty(propertyName)) return false;

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = $"{type.FullName}_{propertyName}";

                    PropertyInfo property = PropertyCache.GetOrAdd(cacheKey, _ =>
                        type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));

                    if (property == null || !property.CanRead) return false;

                    value = property.GetValue(obj, null);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>
            /// 尝试反射获取属性值（泛型，不打日志）。
            /// </summary>
            public static bool TryGetProperty<T>(object obj, string propertyName, out T value)
            {
                value = default;
                if (!TryGetProperty(obj, propertyName, out object raw)) return false;

                if (raw is T t)
                {
                    value = t;
                    return true;
                }

                return false;
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
                        string originalValueTypeName = value?.GetType().Name ?? "null";
                        // 尝试基本类型转换
                        value = _ConvertBasicTypes(value, property.PropertyType);
                        if (value == null)
                        {
                            Debug.LogWarning($"属性类型 {property.PropertyType.Name} 与值类型 {originalValueTypeName} 不兼容，且无法进行基础类型转换。");
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

            /// <summary>
            /// 尝试反射设置属性值（不打日志）。
            /// </summary>
            public static bool TrySetProperty(object obj, string propertyName, object value)
            {
                if (obj == null || string.IsNullOrEmpty(propertyName)) return false;

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = $"{type.FullName}_{propertyName}";

                    PropertyInfo property = PropertyCache.GetOrAdd(cacheKey, _ =>
                        type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));

                    if (property == null || !property.CanWrite) return false;

                    // null 写入值类型（非Nullable）直接失败
                    if (value == null)
                    {
                        if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null)
                            return false;

                        property.SetValue(obj, null, null);
                        return true;
                    }

                    if (!property.PropertyType.IsAssignableFrom(value.GetType()))
                    {
                        object converted = _ConvertBasicTypes(value, property.PropertyType);
                        if (converted == null) return false;
                        value = converted;
                    }

                    property.SetValue(obj, value, null);
                    return true;
                }
                catch
                {
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
            /// 方法反射调用命名约定（重要）：
            /// 1) TryInvokeMethod*：只关心“是否调用成功”，不关心返回值（即便目标方法有返回值也会被忽略）。
            /// 2) TryInvokeMethodReturn*：会读取并返回目标方法的返回值（object 或强类型）。
            /// 3) ByTypes：显式指定参数类型，用于参数包含 null 或消除重载歧义。
            /// 4) ByInferredTypes：自动根据实参装填 Type[]（参数里出现 null 时无法推断，会直接失败）。
            /// 5) WithTypeHints：用 InvokeArg 同时传 Type+Value，可为 null 指定类型，最稳。
            /// </summary>
            private const BindingFlags DefaultInstanceBindFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            private static bool IsNullCompatible(ParameterInfo parameterInfo)
            {
                Type parameterType = parameterInfo.ParameterType;
                if (!parameterType.IsValueType) return true;
                return Nullable.GetUnderlyingType(parameterType) != null;
            }

            private static int ScoreParameterMatch(Type parameterType, object value)
            {
                if (value == null)
                {
                    return (!parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null) ? 1 : -1;
                }

                Type valueType = value.GetType();
                if (parameterType == valueType) return 3;
                if (parameterType.IsAssignableFrom(valueType)) return 2;
                return -1;
            }

            private static bool TryResolveMethod(Type type, string methodName, object[] parameters, bool logAmbiguous, out MethodInfo method)
            {
                method = null;

                MethodInfo[] methods = type.GetMethods(DefaultInstanceBindFlags);
                int bestScore = -1;
                MethodInfo best = null;
                bool ambiguous = false;

                int paramCount = parameters?.Length ?? 0;

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo candidate = methods[i];
                    if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal)) continue;

                    ParameterInfo[] candidateParams = candidate.GetParameters();
                    if (candidateParams.Length != paramCount) continue;

                    int score = 0;
                    bool ok = true;

                    for (int p = 0; p < candidateParams.Length; p++)
                    {
                        ParameterInfo pi = candidateParams[p];
                        object arg = parameters[p];
                        int s = ScoreParameterMatch(pi.ParameterType, arg);
                        if (s < 0)
                        {
                            ok = false;
                            break;
                        }
                        score += s;
                    }

                    if (!ok) continue;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        ambiguous = false;
                    }
                    else if (score == bestScore)
                    {
                        ambiguous = true;
                    }
                }

                if (best == null) return false;
                if (ambiguous)
                {
                    if (logAmbiguous)
                    {
                        Debug.LogWarning($"调用方法 {type.Name}.{methodName} 时存在多个重载匹配（参数可能包含 null），请使用 InvokeMethodByTypes 指定参数类型以消除歧义。");
                    }
                    return false;
                }

                method = best;
                return true;
            }

            /// <summary>
            /// 调用方法（显式指定参数类型，解决参数包含 null 或重载歧义问题）。
            /// </summary>
            public static bool InvokeMethodByTypes(object obj, string methodName, Type[] parameterTypes, object[] parameters)
            {
                if (obj == null)
                {
                    Debug.LogWarning("调用方法时，对象不能为 null");
                    return false;
                }

                if (string.IsNullOrEmpty(methodName))
                {
                    Debug.LogWarning("方法名不能为空");
                    return false;
                }

                parameterTypes = parameterTypes ?? Type.EmptyTypes;
                parameters = parameters ?? Array.Empty<object>();

                if (parameterTypes.Length != parameters.Length)
                {
                    Debug.LogWarning("parameterTypes 与 parameters 长度不一致");
                    return false;
                }

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = GenerateMethodCacheKey(type, methodName, parameterTypes);

                    MethodInfo method = MethodCache.GetOrAdd(cacheKey, _ =>
                        type.GetMethod(methodName, DefaultInstanceBindFlags, null, parameterTypes, null));

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

            /// <summary>
            /// 调用方法（泛型返回值，显式指定参数类型）。
            /// </summary>
            public static T InvokeMethodReturnByTypes<T>(object obj, string methodName, Type[] parameterTypes, object[] parameters)
            {
                if (obj == null)
                {
                    Debug.LogWarning("调用方法时，对象不能为 null");
                    return default;
                }

                if (string.IsNullOrEmpty(methodName))
                {
                    Debug.LogWarning("方法名不能为空");
                    return default;
                }

                parameterTypes = parameterTypes ?? Type.EmptyTypes;
                parameters = parameters ?? Array.Empty<object>();

                if (parameterTypes.Length != parameters.Length)
                {
                    Debug.LogWarning("parameterTypes 与 parameters 长度不一致");
                    return default;
                }

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = GenerateMethodCacheKey(type, methodName, parameterTypes);

                    MethodInfo method = MethodCache.GetOrAdd(cacheKey, _ =>
                        type.GetMethod(methodName, DefaultInstanceBindFlags, null, parameterTypes, null));

                    if (method == null)
                    {
                        Debug.LogWarning($"在类型 {type.Name} 中未找到方法: {methodName}");
                        return default;
                    }

                    object result = method.Invoke(obj, parameters);
                    if (result == null) return default;
                    return (T)result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"调用方法失败: {ex.Message}");
                    return default;
                }
            }

            /// <summary>
            /// 尝试调用方法并获取返回值（不打日志）。
            /// 说明：凡是带 Return 的方法都会读取/返回目标方法返回值；不带 Return 的方法只报告“是否调用成功”。
            /// 支持参数包含 null；如遇重载歧义建议使用 TryInvokeMethodReturnByTypes。
            /// </summary>
            public static bool TryInvokeMethodReturn(object obj, string methodName, object[] parameters, out object result)
            {
                result = null;
                if (obj == null || string.IsNullOrEmpty(methodName)) return false;

                parameters = parameters ?? Array.Empty<object>();

                try
                {
                    Type type = obj.GetType();

                    bool hasNull = false;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i] == null)
                        {
                            hasNull = true;
                            break;
                        }
                    }

                    MethodInfo method;

                    if (!hasNull)
                    {
                        Type[] paramTypes = parameters.Length > 0 ? Array.ConvertAll(parameters, p => p.GetType()) : Type.EmptyTypes;
                        string cacheKey = GenerateMethodCacheKey(type, methodName, paramTypes);
                        method = MethodCache.GetOrAdd(cacheKey, _ =>
                            type.GetMethod(methodName, DefaultInstanceBindFlags, null, paramTypes, null));
                    }
                    else
                    {
                        if (!TryResolveMethod(type, methodName, parameters, false, out method))
                            return false;
                    }

                    if (method == null) return false;

                    result = method.Invoke(obj, parameters);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            #region 方法调用（简单版）

            /// <summary>
            /// 简单版快速选择：
            /// - 不需要返回值：用 TryInvokeMethod*(...)
            /// - 需要返回值：用 TryInvokeMethodReturn*(...)
            /// - 参数含 null / 重载歧义：优先用 TryInvokeMethodReturnWithTypeHints 或 TryInvokeMethodReturnByTypes
            /// </summary>

            /// <summary>
            /// 尝试调用方法（不打日志）。不获取返回值（即便目标方法有返回值也会被忽略）。
            /// 简单版：无需手动创建 object[]。
            /// </summary>
            public static bool TryInvokeMethod(object obj, string methodName, params object[] parameters)
            {
                return TryInvokeMethodReturn(obj, methodName, parameters, out _);
            }

            /// <summary>
            /// 尝试调用方法并获取返回值（不打日志）。
            /// 简单版：无需手动创建 object[]。
            /// </summary>
            public static bool TryInvokeMethodReturn(object obj, string methodName, out object result, params object[] parameters)
            {
                return TryInvokeMethodReturn(obj, methodName, parameters, out result);
            }

            /// <summary>
            /// 尝试调用方法并获取返回值（强类型，不打日志）。
            /// 简单版：只带“返回值类型泛型”，参数用 params。
            /// </summary>
            public static bool TryInvokeMethodReturn<T>(object obj, string methodName, out T result, params object[] parameters)
            {
                return TryInvokeMethodReturn(obj, methodName, parameters, out result);
            }

            /// <summary>
            /// 尝试调用方法并获取返回值（不打日志）。
            /// ByTypes：显式指定参数类型（用于参数含 null 或消除重载歧义）。
            /// 简单版：参数用 params。
            /// </summary>
            public static bool TryInvokeMethodReturnByTypes(object obj, string methodName, Type[] parameterTypes, out object result, params object[] parameters)
            {
                return TryInvokeMethodReturnByTypes(obj, methodName, parameterTypes, parameters, out result);
            }

            /// <summary>
            /// 尝试调用方法（不打日志）。不获取返回值。
            /// ByTypes：显式指定参数类型（用于参数含 null 或消除重载歧义）。
            /// </summary>
            public static bool TryInvokeMethodByTypes(object obj, string methodName, Type[] parameterTypes, params object[] parameters)
            {
                return TryInvokeMethodReturnByTypes(obj, methodName, parameterTypes, out _, parameters);
            }

            /// <summary>
            /// 尝试调用方法并获取返回值（不打日志）。
            /// ByInferredTypes：自动装填 Type[]。
            /// 注意：参数中包含 null 时无法推断类型，将直接返回 false；此时请改用 TryInvokeMethodWithTypeHints（不取返回值）或 TryInvokeMethodReturnWithTypeHints（取返回值）。
            /// </summary>
            public static bool TryInvokeMethodReturnByInferredTypes(object obj, string methodName, out object result, params object[] parameters)
            {
                result = null;
                parameters = parameters ?? Array.Empty<object>();

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i] == null)
                        return false;
                }

                Type[] parameterTypes = parameters.Length > 0
                    ? Array.ConvertAll(parameters, p => p.GetType())
                    : Type.EmptyTypes;

                return TryInvokeMethodReturnByTypes(obj, methodName, parameterTypes, parameters, out result);
            }

            /// <summary>
            /// 尝试调用方法（不打日志）。不获取返回值。
            /// ByInferredTypes：自动装填 Type[]。
            /// </summary>
            public static bool TryInvokeMethodByInferredTypes(object obj, string methodName, params object[] parameters)
            {
                return TryInvokeMethodReturnByInferredTypes(obj, methodName, out _, parameters);
            }

            /// <summary>
            /// 尝试调用方法并获取返回值（强类型，不打日志）。
            /// ByInferredTypes：自动装填 Type[]。
            /// 注意：参数中包含 null 时无法推断类型，将直接返回 false；此时请改用 TryInvokeMethodReturnWithTypeHints。
            /// </summary>
            public static bool TryInvokeMethodReturnByInferredTypes<T>(object obj, string methodName, out T result, params object[] parameters)
            {
                result = default;
                if (!TryInvokeMethodReturnByInferredTypes(obj, methodName, out object raw, parameters))
                    return false;

                if (raw is T t)
                {
                    result = t;
                    return true;
                }

                if (raw == null)
                {
                    Type targetType = typeof(T);
                    if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                        return true;
                }

                return false;
            }

            /// <summary>
            /// 参数包装：用于“装填 Type[]”并且支持 null 指定类型（不打日志）。
            /// </summary>
            public readonly struct InvokeArg
            {
                public readonly Type Type;
                public readonly object Value;

                private InvokeArg(Type type, object value)
                {
                    Type = type;
                    Value = value;
                }

                public static InvokeArg Of<T>(T value)
                {
                    return new InvokeArg(typeof(T), value);
                }

                public static InvokeArg Of(Type type, object value)
                {
                    return new InvokeArg(type, value);
                }

                public static InvokeArg Null<T>()
                {
                    return new InvokeArg(typeof(T), null);
                }

                public static InvokeArg Null(Type type)
                {
                    return new InvokeArg(type, null);
                }
            }

            /// <summary>
            /// 尝试调用方法并获取返回值（不打日志）。
            /// WithTypeHints：使用 InvokeArg 同时传 Type+Value，适用于参数含 null 或需要精确选择重载。
            /// </summary>
            public static bool TryInvokeMethodReturnWithTypeHints(object obj, string methodName, out object result, params InvokeArg[] args)
            {
                result = null;
                args = args ?? Array.Empty<InvokeArg>();

                Type[] types = args.Length > 0 ? new Type[args.Length] : Type.EmptyTypes;
                object[] values = args.Length > 0 ? new object[args.Length] : Array.Empty<object>();

                for (int i = 0; i < args.Length; i++)
                {
                    Type t = args[i].Type;
                    if (t == null) return false;
                    types[i] = t;
                    values[i] = args[i].Value;
                }

                return TryInvokeMethodReturnByTypes(obj, methodName, types, values, out result);
            }

            /// <summary>
            /// 尝试调用方法（不打日志）。不获取返回值。
            /// WithTypeHints：使用 InvokeArg 同时传 Type+Value。
            /// </summary>
            public static bool TryInvokeMethodWithTypeHints(object obj, string methodName, params InvokeArg[] args)
            {
                return TryInvokeMethodReturnWithTypeHints(obj, methodName, out _, args);
            }

            /// <summary>
            /// 尝试调用方法并获取返回值（强类型，不打日志）。
            /// WithTypeHints：使用 InvokeArg 同时传 Type+Value。
            /// </summary>
            public static bool TryInvokeMethodReturnWithTypeHints<T>(object obj, string methodName, out T result, params InvokeArg[] args)
            {
                result = default;
                if (!TryInvokeMethodReturnWithTypeHints(obj, methodName, out object raw, args))
                    return false;

                if (raw is T t)
                {
                    result = t;
                    return true;
                }

                if (raw == null)
                {
                    Type targetType = typeof(T);
                    if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                        return true;
                }

                return false;
            }

            /// <summary>
            /// 尝试调用方法（返回 int，不打日志）。支持 out var，不用写 &lt;int&gt;。
            /// </summary>
            public static bool TryInvokeMethodReturn(object obj, string methodName, out int result, params object[] parameters)
            {
                return TryInvokeMethodReturn<int>(obj, methodName, out result, parameters);
            }

            /// <summary>
            /// 尝试调用方法（返回 float，不打日志）。支持 out var，不用写 &lt;float&gt;。
            /// </summary>
            public static bool TryInvokeMethodReturn(object obj, string methodName, out float result, params object[] parameters)
            {
                return TryInvokeMethodReturn<float>(obj, methodName, out result, parameters);
            }

            /// <summary>
            /// 尝试调用方法（返回 bool，不打日志）。支持 out var，不用写 &lt;bool&gt;。
            /// </summary>
            public static bool TryInvokeMethodReturn(object obj, string methodName, out bool result, params object[] parameters)
            {
                return TryInvokeMethodReturn<bool>(obj, methodName, out result, parameters);
            }

            /// <summary>
            /// 尝试调用方法（返回 string，不打日志）。支持 out var，不用写 &lt;string&gt;。
            /// </summary>
            public static bool TryInvokeMethodReturn(object obj, string methodName, out string result, params object[] parameters)
            {
                return TryInvokeMethodReturn<string>(obj, methodName, out result, parameters);
            }

            /// <summary>
            /// 尝试调用方法（显式指定参数类型 + 泛型返回值，不打日志）。简单版：参数用 params。
            /// </summary>
            public static bool TryInvokeMethodReturnByTypes<T>(object obj, string methodName, Type[] parameterTypes, out T result, params object[] parameters)
            {
                return TryInvokeMethodReturnByTypes(obj, methodName, parameterTypes, parameters, out result);
            }

            #endregion

            /// <summary>
            /// 尝试调用方法（泛型返回值，不打日志）。
            /// </summary>
            public static bool TryInvokeMethodReturn<T>(object obj, string methodName, object[] parameters, out T result)
            {
                result = default;
                if (!TryInvokeMethodReturn(obj, methodName, parameters, out object raw)) return false;

                if (raw is T t)
                {
                    result = t;
                    return true;
                }

                // 允许 raw 为 null 的情况：对于引用类型/Nullable，返回 true 但 result 为 default
                if (raw == null)
                {
                    Type targetType = typeof(T);
                    if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                        return true;
                }

                return false;
            }

            /// <summary>
            /// 尝试调用方法（显式指定参数类型，不打日志）。用于参数包含 null 或消除重载歧义。
            /// </summary>
            public static bool TryInvokeMethodReturnByTypes(object obj, string methodName, Type[] parameterTypes, object[] parameters, out object result)
            {
                result = null;
                if (obj == null || string.IsNullOrEmpty(methodName)) return false;

                parameterTypes = parameterTypes ?? Type.EmptyTypes;
                parameters = parameters ?? Array.Empty<object>();

                if (parameterTypes.Length != parameters.Length) return false;

                try
                {
                    Type type = obj.GetType();
                    string cacheKey = GenerateMethodCacheKey(type, methodName, parameterTypes);

                    MethodInfo method = MethodCache.GetOrAdd(cacheKey, _ =>
                        type.GetMethod(methodName, DefaultInstanceBindFlags, null, parameterTypes, null));

                    if (method == null) return false;

                    result = method.Invoke(obj, parameters);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>
            /// 尝试调用方法（显式指定参数类型 + 泛型返回值，不打日志）。
            /// </summary>
            public static bool TryInvokeMethodReturnByTypes<T>(object obj, string methodName, Type[] parameterTypes, object[] parameters, out T result)
            {
                result = default;
                if (!TryInvokeMethodReturnByTypes(obj, methodName, parameterTypes, parameters, out object raw)) return false;

                if (raw is T t)
                {
                    result = t;
                    return true;
                }

                if (raw == null)
                {
                    Type targetType = typeof(T);
                    if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                        return true;
                }

                return false;
            }

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

                if (string.IsNullOrEmpty(methodName))
                {
                    Debug.LogWarning("方法名不能为空");
                    return default;
                }

                try
                {
                    Type type = obj.GetType();
                    parameters = parameters ?? Array.Empty<object>();

                    bool hasNull = false;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i] == null)
                        {
                            hasNull = true;
                            break;
                        }
                    }

                    MethodInfo method;

                    if (!hasNull)
                    {
                        Type[] paramTypes = parameters.Length > 0 ? Array.ConvertAll(parameters, p => p.GetType()) : Type.EmptyTypes;
                        string cacheKey = GenerateMethodCacheKey(type, methodName, paramTypes);
                        method = MethodCache.GetOrAdd(cacheKey, _ =>
                            type.GetMethod(methodName, DefaultInstanceBindFlags, null, paramTypes, null));
                    }
                    else
                    {
                        // 参数含 null 时无法可靠推断重载，改为扫描并按兼容性匹配。
                        if (!TryResolveMethod(type, methodName, parameters, true, out method))
                        {
                            Debug.LogWarning($"在类型 {type.Name} 中未找到方法或重载匹配失败: {methodName}");
                            return default;
                        }
                    }

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

                if (string.IsNullOrEmpty(methodName))
                {
                    Debug.LogWarning("方法名不能为空");
                    return false;
                }

                try
                {
                    Type type = obj.GetType();
                    parameters = parameters ?? Array.Empty<object>();

                    bool hasNull = false;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i] == null)
                        {
                            hasNull = true;
                            break;
                        }
                    }

                    MethodInfo method;

                    if (!hasNull)
                    {
                        Type[] paramTypes = parameters.Length > 0 ? Array.ConvertAll(parameters, p => p.GetType()) : Type.EmptyTypes;
                        string cacheKey = GenerateMethodCacheKey(type, methodName, paramTypes);
                        method = MethodCache.GetOrAdd(cacheKey, _ =>
                            type.GetMethod(methodName, DefaultInstanceBindFlags, null, paramTypes, null));
                    }
                    else
                    {
                        if (!TryResolveMethod(type, methodName, parameters, true, out method))
                        {
                            Debug.LogWarning($"在类型 {type.Name} 中未找到方法或重载匹配失败: {methodName}");
                            return false;
                        }
                    }

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

                return TypeFieldsCache.GetOrAdd((type, bindingFlags), k => k.type.GetFields(k.flags));
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

                return TypePropertiesCache.GetOrAdd((type, bindingFlags), k => k.type.GetProperties(k.flags));
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

                return TypeMethodsCache.GetOrAdd((type, bindingFlags), k => k.type.GetMethods(k.flags));
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

