#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Sirenix.OdinInspector;
using ES;
using UnityEditor;
using UnityEngine;

namespace ES.Internal
{
    internal static class ESSoTableRuleTypeUtility
    {
        public static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            Type type = Type.GetType(typeName);
            if (type != null)
                return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        public static bool TryResolveSoDataTypes(Type sourceType, out Type packType, out Type groupType, out Type infoType, out string reason)
        {
            packType = null;
            groupType = null;
            infoType = null;
            reason = string.Empty;

            if (sourceType == null)
            {
                reason = "脚本类型为空，无法生成 Rule。";
                return false;
            }

            if (typeof(ISoDataPack).IsAssignableFrom(sourceType))
            {
                packType = sourceType;
                infoType = TryGetInfoTypeFromPack(sourceType);
            }
            else if (typeof(ISoDataGroup).IsAssignableFrom(sourceType))
            {
                groupType = sourceType;
                infoType = TryGetInfoTypeFromGroup(sourceType);
            }
            else if (typeof(ISoDataInfo).IsAssignableFrom(sourceType))
            {
                infoType = sourceType;
            }
            else
            {
                reason = "脚本不是 SoData Pack、SoData Group 或 SoData Info。";
                return false;
            }

            if (infoType == null)
            {
                reason = "无法从 Pack/Group 脚本推断 Info 类型。请确认脚本继承 SoDataPack<TInfo> 或 SoDataGroup<TInfo>。";
                return false;
            }

            if (packType == null)
                packType = FindSoDataPackType(infoType);
            if (groupType == null)
                groupType = FindSoDataGroupType(infoType);

            if (packType == null || groupType == null)
            {
                reason = "已解析 Info 类型 " + infoType.Name + "，但没有找到对应的 SoDataPack<" + infoType.Name + "> 或 SoDataGroup<" + infoType.Name + "> 实现。";
                return false;
            }

            return true;
        }

        public static Type FindSoDataPackType(Type infoType)
        {
            return FindFirstDerivedGeneric(typeof(SoDataPack<>), infoType);
        }

        public static Type FindSoDataGroupType(Type infoType)
        {
            return FindFirstDerivedGeneric(typeof(SoDataGroup<>), infoType);
        }

        private static Type TryGetInfoTypeFromPack(Type packType)
        {
            return TryGetGenericArgumentFromBase(packType, typeof(SoDataPack<>));
        }

        private static Type TryGetInfoTypeFromGroup(Type groupType)
        {
            return TryGetGenericArgumentFromBase(groupType, typeof(SoDataGroup<>));
        }

        private static Type TryGetGenericArgumentFromBase(Type type, Type openGenericBase)
        {
            Type current = type;
            while (current != null && current != typeof(object))
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericBase)
                    return current.GetGenericArguments()[0];

                current = current.BaseType;
            }

            return null;
        }

        private static Type FindFirstDerivedGeneric(Type openGenericBase, Type genericArgument)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                for (int j = 0; j < types.Length; j++)
                {
                    Type type = types[j];
                    if (type == null || type.IsAbstract)
                        continue;

                    Type argument = TryGetGenericArgumentFromBase(type, openGenericBase);
                    if (argument == genericArgument)
                        return type;
                }
            }

            return null;
        }

        public static string GuessTableType(Type type)
        {
            if (type == typeof(bool))
                return "bool";
            if (type == typeof(byte) || type == typeof(short) || type == typeof(int))
                return "int";
            if (type == typeof(long))
                return "long";
            if (type == typeof(float))
                return "float";
            if (type == typeof(double))
                return "double";
            if (type == typeof(string))
                return "string";
            if (type.IsEnum)
                return "string";
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return "string";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return "list," + GuessTableType(type.GetGenericArguments()[0]);

            return "string";
        }

        public static bool CanExpandAsNestedObject(Type type)
        {
            if (type == null)
                return false;
            if (type.IsPrimitive || type.IsEnum)
                return false;
            if (type == typeof(string) || type == typeof(decimal))
                return false;
            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Color) || type == typeof(Rect) || type == typeof(Quaternion))
                return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
                return false;
            if (type.IsAbstract || type.IsInterface)
                return false;

            return Attribute.IsDefined(type, typeof(SerializableAttribute)) || type.IsValueType;
        }

        public static ESTableValueWriteMode GuessValueWriteMode(Type type)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return ESTableValueWriteMode.UnityObjectGuid;

            if (type.IsEnum)
                return ESTableValueWriteMode.PlainValue;

            if (type.IsPrimitive || type == typeof(string))
                return ESTableValueWriteMode.PlainValue;

            return ESTableValueWriteMode.Json;
        }
    }
}
#endif
