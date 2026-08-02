using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Cached editor catalog for Unity SerializeReference candidates.
    ///
    /// The editor assembly flow invalidates the cache, but actual type enumeration remains lazy.
    /// Unity TypeCache remains the source of derived types and Odin's
    /// TypeRegistryItemAttribute remains the source of business menu metadata. ES only
    /// normalizes, filters, sorts and caches the result for its own selectors.
    /// </summary>
    internal static class ESTypeCatalog
    {
        private const string UnregisteredGroupName = "未登记类型";
        private static readonly Dictionary<Type, Catalog> catalogsByBaseType
            = new Dictionary<Type, Catalog>(16);
        private static readonly Dictionary<Type, Entry> entriesByType
            = new Dictionary<Type, Entry>(128);
        private static readonly Catalog emptyCatalog = new Catalog(
            new List<Entry>(0));
        private static int cacheGeneration;

        /// <summary>
        /// Called by ESAssemblyStream after editor assemblies are available. The catalog remains
        /// lazy: assembly flow only invalidates old descriptors and never scans every base type.
        /// </summary>
        internal static void OnAssemblyStream()
        {
            Clear();
        }

        public static Catalog Get(Type baseType)
        {
            if (baseType == null)
                return emptyCatalog;

            if (catalogsByBaseType.TryGetValue(baseType, out Catalog catalog))
                return catalog;

            var entries = new List<Entry>(16);
            var collected = new HashSet<Type>();
            AddCandidate(baseType, collected, entries);

            foreach (Type candidate in TypeCache.GetTypesDerivedFrom(baseType))
                AddCandidate(candidate, collected, entries);

            entries.Sort(Entry.Compare);
            catalog = new Catalog(entries);
            catalogsByBaseType.Add(baseType, catalog);
            return catalog;
        }

        public static Entry GetEntry(Type type)
        {
            if (type == null)
                return default(Entry);

            return Describe(type);
        }

        public static string GetDisplayName(Type type)
        {
            if (type == null)
                return "多态配置";

            return GetEntry(type).DisplayName;
        }

        public static void Clear()
        {
            catalogsByBaseType.Clear();
            entriesByType.Clear();
            cacheGeneration++;
        }

        /// <summary>
        /// Returns cache-only telemetry for editor diagnostics. This method never calls
        /// TypeCache and therefore never turns a cold selector into a full type scan.
        /// </summary>
        internal static CacheDiagnostics GetCacheDiagnostics()
        {
            return new CacheDiagnostics(
                catalogsByBaseType.Count,
                entriesByType.Count,
                cacheGeneration);
        }

        private static void AddCandidate(
            Type candidate,
            HashSet<Type> collected,
            List<Entry> entries)
        {
            if (!CanCreate(candidate) || !collected.Add(candidate))
                return;

            entries.Add(Describe(candidate));
        }

        private static bool CanCreate(Type type)
        {
            if (type == null
                || !type.IsClass
                || type.IsAbstract
                || type.IsGenericTypeDefinition
                || type.ContainsGenericParameters
                || !type.IsSerializable
                || typeof(UnityEngine.Object).IsAssignableFrom(type)
                || (type.Namespace != null
                    && type.Namespace.StartsWith("UnityEditor", StringComparison.Ordinal))
                || type.IsDefined(typeof(ObsoleteAttribute), false))
                return false;

            return type.GetConstructor(
                       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                       binder: null,
                       types: Type.EmptyTypes,
                       modifiers: null) != null;
        }

        private static Entry Describe(Type type)
        {
            if (entriesByType.TryGetValue(type, out Entry descriptor))
                return descriptor;

            TypeRegistryItemAttribute registryItem
                = type.GetCustomAttribute<TypeRegistryItemAttribute>(false);
            string registryName = registryItem?.Name;
            bool registered = !string.IsNullOrWhiteSpace(registryName);
            string groupPath;
            string displayName;

            if (registered)
            {
                string normalized = registryName.Trim().Trim('/');
                int separator = normalized.LastIndexOf('/');
                groupPath = separator > 0
                    ? normalized.Substring(0, separator)
                    : null;
                displayName = separator >= 0
                    ? normalized.Substring(separator + 1)
                    : normalized;
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = BuildDisplayName(type);
            }
            else
            {
                groupPath = UnregisteredGroupName;
                displayName = BuildDisplayName(type);
            }

            string assemblyName = type.Assembly.GetName().Name;
            string fullName = type.FullName ?? type.Name;
            string subtitle = registered ? type.Name : "未使用 TypeRegistryItem 登记";
            string tooltip = fullName + "\n程序集：" + assemblyName;
            string keywords = (registryName ?? string.Empty)
                              + " " + type.Name
                              + " " + fullName;

            descriptor = new Entry(
                type,
                displayName,
                groupPath,
                subtitle,
                tooltip,
                keywords,
                registered ? null : "未登记");
            entriesByType.Add(type, descriptor);
            return descriptor;
        }

        private static string BuildDisplayName(Type type)
        {
            return SplitWords(TrimCommonPrefix(type.Name));
        }

        private static string TrimCommonPrefix(string name)
        {
            if (name.StartsWith("ES", StringComparison.Ordinal) && name.Length > 2)
                return name.Substring(2);
            if (name.StartsWith("Op", StringComparison.Ordinal) && name.Length > 2)
                return name.Substring(2);
            return name;
        }

        private static string SplitWords(string value)
        {
            var builder = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current == '_')
                {
                    if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
                        builder.Append(' ');
                    continue;
                }

                if (i > 0 && char.IsUpper(current) && char.IsLower(value[i - 1]))
                    builder.Append(' ');
                builder.Append(current);
            }

            return builder.ToString().Trim();
        }

        internal sealed class Catalog
        {
            public readonly IReadOnlyList<Entry> Entries;

            public Catalog(List<Entry> entries)
            {
                Entries = entries;
            }

            public int Count => Entries.Count;
        }

        internal readonly struct CacheDiagnostics
        {
            public readonly int CatalogCount;
            public readonly int DescriptorCount;
            public readonly int Generation;

            public bool IsWarm => CatalogCount > 0;

            public CacheDiagnostics(int catalogCount, int descriptorCount, int generation)
            {
                CatalogCount = catalogCount;
                DescriptorCount = descriptorCount;
                Generation = generation;
            }
        }

        internal readonly struct Entry
        {
            public readonly Type Type;
            public readonly string DisplayName;
            public readonly string GroupPath;
            public readonly string Subtitle;
            public readonly string Tooltip;
            public readonly string Keywords;
            public readonly string Badge;

            public Entry(
                Type type,
                string displayName,
                string groupPath,
                string subtitle,
                string tooltip,
                string keywords,
                string badge)
            {
                Type = type;
                DisplayName = displayName;
                GroupPath = groupPath;
                Subtitle = subtitle;
                Tooltip = tooltip;
                Keywords = keywords;
                Badge = badge;
            }

            public static int Compare(Entry left, Entry right)
            {
                int groupCompare = string.Compare(
                    left.GroupPath,
                    right.GroupPath,
                    StringComparison.Ordinal);
                if (groupCompare != 0)
                    return groupCompare;

                int nameCompare = string.Compare(
                    left.DisplayName,
                    right.DisplayName,
                    StringComparison.Ordinal);
                return nameCompare != 0
                    ? nameCompare
                    : string.Compare(
                        left.Type.FullName,
                        right.Type.FullName,
                        StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Registers ESTypeCatalog with the project's ES assembly stream instead of attaching a
    /// second global editor-load entry point to the type catalog itself.
    /// </summary>
    public sealed class ESTypeCatalogAssemblyStreamInitializer : ES.EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESTypeCatalog.OnAssemblyStream();
        }
    }
}
