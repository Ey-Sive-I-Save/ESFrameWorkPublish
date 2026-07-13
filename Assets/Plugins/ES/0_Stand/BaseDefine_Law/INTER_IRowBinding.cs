using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ES
{
    public enum ESRowTargetMode
    {
        [InspectorName("一行 = 一个对象")]
        Object,

        [InspectorName("一行 = 对象内 List 的一个元素")]
        ObjectListElement
    }

    [Serializable]
    public class ESRowBindingRule
    {
        [InspectorName("行目标")]
        public ESRowTargetMode targetMode = ESRowTargetMode.Object;

        [InspectorName("行 Key 列名")]
        public string rowKeyColumnName = "key";

        [InspectorName("List 字段路径")]
        public string listFieldPath;

        [InspectorName("元素 Key 字段路径")]
        public string elementKeyFieldPath = "key";

        [InspectorName("导入时创建缺失元素")]
        public bool createMissingElement = true;

        [InspectorName("空 Key 合法")]
        public bool allowEmptyRowKey;

        public bool IsObjectRow => targetMode == ESRowTargetMode.Object;
        public bool IsListElementRow => targetMode == ESRowTargetMode.ObjectListElement;
    }

    public interface IESRowBridge
    {
        bool IsValidRowKey(string rowKey, ESRowBindingRule binding, out string reason);
        IList EnsureContainer(object owner, ESRowBindingRule binding);
        IEnumerable<object> EnumerateRows(object owner, ESRowBindingRule binding);
        bool TryGetOrCreateRow(object owner, string rowKey, ESRowBindingRule binding, out object row, out string reason);
    }

    public sealed class ESReflectionRowBridge : IESRowBridge
    {
        public bool IsValidRowKey(string rowKey, ESRowBindingRule binding, out string reason)
        {
            reason = string.Empty;
            if (binding != null && binding.allowEmptyRowKey)
                return true;

            if (!string.IsNullOrWhiteSpace(rowKey))
                return true;

            reason = "行 Key 不能为空。";
            return false;
        }

        public IList EnsureContainer(object owner, ESRowBindingRule binding)
        {
            if (owner == null || binding == null || !binding.IsListElementRow)
                return null;

            object container = ESRowBindingReflectionUtility.GetMemberValue(owner, binding.listFieldPath);
            if (container is IList list)
                return list;

            Type listType = ESRowBindingReflectionUtility.GetMemberType(owner.GetType(), binding.listFieldPath);
            if (listType == null)
                return null;

            IList created = ESRowBindingReflectionUtility.CreateListInstance(listType);
            if (created == null)
                return null;

            ESRowBindingReflectionUtility.SetMemberValue(owner, binding.listFieldPath, created);
            return created;
        }

        public IEnumerable<object> EnumerateRows(object owner, ESRowBindingRule binding)
        {
            if (owner == null)
                yield break;

            if (binding == null || binding.IsObjectRow)
            {
                yield return owner;
                yield break;
            }

            IList list = EnsureContainer(owner, binding);
            if (list == null)
                yield break;

            for (int i = 0; i < list.Count; i++)
                yield return list[i];
        }

        public bool TryGetOrCreateRow(object owner, string rowKey, ESRowBindingRule binding, out object row, out string reason)
        {
            row = null;
            reason = string.Empty;

            if (!IsValidRowKey(rowKey, binding, out reason))
                return false;

            if (owner == null)
            {
                reason = "宿主对象为空，无法定位行目标。";
                return false;
            }

            if (binding == null || binding.IsObjectRow)
            {
                row = owner;
                return true;
            }

            IList list = EnsureContainer(owner, binding);
            if (list == null)
            {
                reason = "无法获取或创建 List 容器：" + binding.listFieldPath;
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                object element = list[i];
                string elementKey = ESRowBindingReflectionUtility.GetMemberValue(element, binding.elementKeyFieldPath)?.ToString();
                if (elementKey == rowKey)
                {
                    row = element;
                    return true;
                }
            }

            if (!binding.createMissingElement)
            {
                reason = "没有找到行 Key 对应的 List 元素：" + rowKey;
                return false;
            }

            Type elementType = ESRowBindingReflectionUtility.GetListElementType(list.GetType());
            if (elementType == null)
            {
                reason = "无法推导 List 元素类型：" + binding.listFieldPath;
                return false;
            }

            row = Activator.CreateInstance(elementType);
            ESRowBindingReflectionUtility.SetMemberValue(row, binding.elementKeyFieldPath, rowKey);
            list.Add(row);
            return true;
        }
    }

    public static class ESRowBindingReflectionUtility
    {
        private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<string, MemberInfo> MemberCache = new Dictionary<string, MemberInfo>(256);
        private static readonly Dictionary<string, ESMemberPath> MemberPathCache = new Dictionary<string, ESMemberPath>(256);

        public static Type GetMemberType(Type rootType, string memberPath)
        {
            ESMemberPath path = GetOrCreateMemberPath(rootType, memberPath);
            return path != null && path.IsValid ? path.ValueType : null;
        }

        public static ESMemberPath GetOrCreateMemberPath(Type rootType, string memberPath)
        {
            if (rootType == null || string.IsNullOrWhiteSpace(memberPath))
                return null;

            string normalizedPath = NormalizeMemberPath(memberPath);
            string cacheKey = rootType.AssemblyQualifiedName + "|" + normalizedPath;
            if (MemberPathCache.TryGetValue(cacheKey, out ESMemberPath cachedPath))
                return cachedPath;

            Type currentType = rootType;
            string[] parts = normalizedPath.Split('.');
            var members = new List<MemberInfo>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                    continue;

                MemberInfo member = GetCachedFieldOrProperty(currentType, parts[i]);
                if (member == null)
                {
                    cachedPath = ESMemberPath.Invalid;
                    MemberPathCache[cacheKey] = cachedPath;
                    return cachedPath;
                }

                currentType = GetMemberType(member);
                if (currentType == null)
                {
                    cachedPath = ESMemberPath.Invalid;
                    MemberPathCache[cacheKey] = cachedPath;
                    return cachedPath;
                }

                members.Add(member);
            }

            cachedPath = new ESMemberPath(members.ToArray(), currentType);
            MemberPathCache[cacheKey] = cachedPath;
            return cachedPath;
        }

        public static object GetMemberValue(object root, string memberPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(memberPath))
                return null;

            object current = root;
            ESMemberPath path = GetOrCreateMemberPath(root.GetType(), memberPath);
            if (path == null || !path.IsValid)
                return null;

            MemberInfo[] members = path.Members;
            for (int i = 0; i < members.Length; i++)
            {
                current = GetMemberValue(current, members[i]);
                if (current == null)
                    return null;
            }

            return current;
        }

        public static bool SetMemberValue(object root, string memberPath, object value)
        {
            return SetMemberValue(root, memberPath, value, true);
        }

        public static bool SetMemberValue(object root, string memberPath, object value, bool createIntermediate)
        {
            if (root == null || string.IsNullOrWhiteSpace(memberPath))
                return false;

            object current = root;
            ESMemberPath path = GetOrCreateMemberPath(root.GetType(), memberPath);
            if (path == null || !path.IsValid || path.Members.Length == 0)
                return false;

            MemberInfo[] members = path.Members;
            for (int i = 0; i < members.Length - 1; i++)
            {
                MemberInfo member = members[i];
                object next = GetMemberValue(current, member);
                if (next == null)
                {
                    if (!createIntermediate)
                        return false;

                    Type nextType = GetMemberType(member);
                    next = CreateObjectInstance(nextType);
                    if (next == null || !SetDirectMemberValue(current, member, next))
                        return false;
                }

                current = next;
            }

            MemberInfo lastMember = members[members.Length - 1];
            if (lastMember is FieldInfo field)
            {
                field.SetValue(current, ConvertValue(value, field.FieldType));
                return true;
            }

            if (lastMember is PropertyInfo property && property.CanWrite)
            {
                property.SetValue(current, ConvertValue(value, property.PropertyType));
                return true;
            }

            return false;
        }

        public static Type GetListElementType(Type listType)
        {
            if (listType == null)
                return null;

            if (listType.IsArray)
                return listType.GetElementType();

            if (listType.IsGenericType && listType.GetGenericArguments().Length == 1)
                return listType.GetGenericArguments()[0];

            Type[] interfaces = listType.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type interfaceType = interfaces[i];
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IList<>))
                    return interfaceType.GetGenericArguments()[0];
            }

            return null;
        }

        public static IList CreateListInstance(Type listType)
        {
            if (listType == null || listType.IsArray)
                return null;

            if (!listType.IsInterface && !listType.IsAbstract)
                return Activator.CreateInstance(listType) as IList;

            Type elementType = GetListElementType(listType);
            if (elementType == null)
                return null;

            Type concreteType = typeof(List<>).MakeGenericType(elementType);
            return Activator.CreateInstance(concreteType) as IList;
        }

        public static object CreateObjectInstance(Type type)
        {
            if (type == null || type.IsAbstract || type.IsInterface)
                return null;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return null;

            if (type == typeof(string))
                return string.Empty;

            if (type.IsArray)
                return null;

            if (typeof(IList).IsAssignableFrom(type))
                return CreateListInstance(type);

            if (type.IsValueType)
                return Activator.CreateInstance(type);

            ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
            return constructor != null ? Activator.CreateInstance(type) : null;
        }

        public static void ClearCache()
        {
            MemberCache.Clear();
            MemberPathCache.Clear();
        }

        private static string NormalizeMemberPath(string memberPath)
        {
            return memberPath.Replace("[]", string.Empty);
        }

        private static MemberInfo GetCachedFieldOrProperty(Type type, string memberName)
        {
            if (type == null || string.IsNullOrWhiteSpace(memberName))
                return null;

            string cacheKey = type.AssemblyQualifiedName + "|" + memberName;
            if (MemberCache.TryGetValue(cacheKey, out MemberInfo cachedMember))
                return cachedMember;

            cachedMember = FindFieldOrProperty(type, memberName);
            MemberCache[cacheKey] = cachedMember;
            return cachedMember;
        }

        private static MemberInfo FindFieldOrProperty(Type type, string memberName)
        {
            while (type != null && type != typeof(object))
            {
                FieldInfo field = type.GetField(memberName, MemberFlags);
                if (field != null)
                    return field;

                PropertyInfo property = type.GetProperty(memberName, MemberFlags);
                if (property != null)
                    return property;

                type = type.BaseType;
            }

            return null;
        }

        private static Type GetMemberType(MemberInfo member)
        {
            if (member is FieldInfo field)
                return field.FieldType;

            if (member is PropertyInfo property)
                return property.PropertyType;

            return null;
        }

        private static object GetMemberValue(object owner, MemberInfo member)
        {
            if (member is FieldInfo field)
                return field.GetValue(owner);

            if (member is PropertyInfo property && property.CanRead)
                return property.GetValue(owner);

            return null;
        }

        private static bool SetDirectMemberValue(object owner, MemberInfo member, object value)
        {
            if (member is FieldInfo field)
            {
                field.SetValue(owner, value);
                return true;
            }

            if (member is PropertyInfo property && property.CanWrite)
            {
                property.SetValue(owner, value);
                return true;
            }

            return false;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null || targetType == null)
                return value;

            Type valueType = value.GetType();
            if (targetType.IsAssignableFrom(valueType))
                return value;

            if (targetType.IsEnum)
                return Enum.Parse(targetType, value.ToString());

            return Convert.ChangeType(value, targetType);
        }
    }

    public sealed class ESMemberPath
    {
        public static readonly ESMemberPath Invalid = new ESMemberPath(null, null);

        public readonly MemberInfo[] Members;
        public readonly Type ValueType;
        public bool IsValid => Members != null && Members.Length > 0 && ValueType != null;

        public ESMemberPath(MemberInfo[] members, Type valueType)
        {
            Members = members;
            ValueType = valueType;
        }
    }
}
