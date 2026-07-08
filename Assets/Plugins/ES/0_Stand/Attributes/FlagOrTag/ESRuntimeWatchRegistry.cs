using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ES
{
    public static class ESRuntimeWatchRegistry
    {
        private static readonly List<Entry> entries = new List<Entry>();
        private static readonly List<Type> ownerTypes = new List<Type>();
        private static readonly HashSet<FieldInfo> registeredFields = new HashSet<FieldInfo>();

        public static IReadOnlyList<Entry> Entries => entries;
        public static IReadOnlyList<Type> OwnerTypes => ownerTypes;

        public static void RegisterField(ESRuntimeWatchAttribute attribute, FieldInfo fieldInfo)
        {
            if (attribute == null || fieldInfo == null || registeredFields.Contains(fieldInfo))
                return;

            Type ownerType = fieldInfo.DeclaringType;
            if (ownerType == null || !typeof(MonoBehaviour).IsAssignableFrom(ownerType))
                return;

            if (fieldInfo.IsStatic || fieldInfo.IsLiteral)
                return;

            registeredFields.Add(fieldInfo);
            if (!ownerTypes.Contains(ownerType))
                ownerTypes.Add(ownerType);

            entries.Add(new Entry(attribute, fieldInfo, ownerType));
        }

        public readonly struct Entry
        {
            public readonly ESRuntimeWatchAttribute Attribute;
            public readonly FieldInfo FieldInfo;
            public readonly Type OwnerType;

            public Entry(ESRuntimeWatchAttribute attribute, FieldInfo fieldInfo, Type ownerType)
            {
                Attribute = attribute;
                FieldInfo = fieldInfo;
                OwnerType = ownerType;
            }
        }
    }
}
