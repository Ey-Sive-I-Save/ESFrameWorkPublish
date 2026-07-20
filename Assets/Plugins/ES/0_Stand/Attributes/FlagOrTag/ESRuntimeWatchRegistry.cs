using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ES
{
    public static class ESRuntimeWatchRegistry
    {
        private const int MaxNestedDepth = 4;
        private const int MaxSchemesPerTargetType = 1024;
        private static readonly BindingFlags InstanceFieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly List<WatchFieldRecord> watchFields = new List<WatchFieldRecord>();
        private static readonly HashSet<FieldInfo> registeredWatchFields = new HashSet<FieldInfo>();
        private static readonly HashSet<PropertyInfo> registeredWatchProperties = new HashSet<PropertyInfo>();
        private static readonly HashSet<MethodInfo> registeredWatchMethods = new HashSet<MethodInfo>();
        private static readonly List<Entry> entries = new List<Entry>();
        private static readonly List<Type> ownerTypes = new List<Type>();
        private static readonly HashSet<string> registeredEntryKeys = new HashSet<string>();
        private static readonly Dictionary<Type, List<Entry>> entriesByOwnerType = new Dictionary<Type, List<Entry>>();
        private static readonly Dictionary<Type, List<OwnerFieldPath>> nestedPathCache = new Dictionary<Type, List<OwnerFieldPath>>();
        private static readonly Dictionary<Type, List<OwnerFieldPath>> modulePathCache = new Dictionary<Type, List<OwnerFieldPath>>();
        private static readonly Dictionary<Type, List<FieldParentEdge>> parentEdgesByChildType = new Dictionary<Type, List<FieldParentEdge>>();
        private static readonly Dictionary<Type, List<FieldParentEdge>> compatibleParentEdgesCache = new Dictionary<Type, List<FieldParentEdge>>();
        private static readonly Dictionary<Type, List<OwnerFieldPath>> monoOwnerSchemesByTargetType = new Dictionary<Type, List<OwnerFieldPath>>();
        private static readonly Dictionary<Type, List<OwnerFieldPath>> moduleOwnerSchemesByTargetType = new Dictionary<Type, List<OwnerFieldPath>>();
        private static readonly Dictionary<Type, List<OwnerFieldPath>> moduleInnerSchemesByTargetType = new Dictionary<Type, List<OwnerFieldPath>>();
        private static readonly List<HostingTypeInfo> hostingTypeInfos = new List<HostingTypeInfo>();
        private static List<Type> candidateMonoTypeCache;
        private static List<Type> candidateModuleTypeCache;
        private static int fieldGraphEdgeCount;
        private static int schemeLimitHitCount;
        private static int rejectedNonMonoOwnerCount;
        private static int rejectedInvalidPathCount;
        private static bool fieldGraphBuilt;
        private static bool entriesBuilt;

        public static int RegisteredMemberCount => watchFields.Count;
        public static int RegisteredFieldCount => registeredWatchFields.Count;
        public static int RegisteredPropertyCount => registeredWatchProperties.Count;
        public static int RegisteredMethodCount => registeredWatchMethods.Count;
        public static bool IsEntriesBuilt => entriesBuilt;
        public static bool IsFieldGraphBuilt => fieldGraphBuilt;
        public static int FieldGraphTargetTypeCount => parentEdgesByChildType.Count;
        public static int FieldGraphEdgeCount => fieldGraphEdgeCount;
        public static int SchemeLimitHitCount => schemeLimitHitCount;
        public static int RejectedNonMonoOwnerCount => rejectedNonMonoOwnerCount;
        public static int RejectedInvalidPathCount => rejectedInvalidPathCount;
        private static bool IsEditorRuntimeWatchEnabled
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
        public static string OwnerTypeSummary => ownerTypes.Count == 0
            ? "<无>"
            : string.Join("\n", ownerTypes.Select(BuildTypeInfo));

        public static string BuildTypeInfo(Type type)
        {
            if (type == null)
                return "Type:<null>";

            string assemblyName = type.Assembly != null ? type.Assembly.GetName().Name : "<no asm>";
            return $"Type:{type.Name} | NS:{type.Namespace ?? "<global>"} | ASM:{assemblyName}";
        }

        public static IReadOnlyList<Entry> Entries
        {
            get
            {
                EnsureEntriesBuilt();
                return entries;
            }
        }

        public static IReadOnlyList<Type> OwnerTypes
        {
            get
            {
                EnsureEntriesBuilt();
                return ownerTypes;
            }
        }

        public static IReadOnlyList<Entry> GetEntriesForOwnerType(Type ownerType)
        {
            EnsureEntriesBuilt();

            if (ownerType == null)
                return Array.Empty<Entry>();

            if (entriesByOwnerType.TryGetValue(ownerType, out var exact))
                return exact;

            var result = new List<Entry>();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.OwnerType != null && entry.OwnerType.IsAssignableFrom(ownerType))
                    result.Add(entry);
            }

            entriesByOwnerType[ownerType] = result;
            return result;
        }

        public static void RegisterField(ESRuntimeWatchAttribute attribute, FieldInfo fieldInfo)
        {
            if (!IsEditorRuntimeWatchEnabled)
                return;

            if (attribute == null || fieldInfo == null)
                return;

            if (fieldInfo.IsStatic || fieldInfo.IsLiteral)
                return;

            if (!registeredWatchFields.Add(fieldInfo))
                return;

            watchFields.Add(new WatchFieldRecord(attribute, fieldInfo));
            InvalidateMaterializedData();
        }

        public static void RegisterProperty(ESRuntimeWatchAttribute attribute, PropertyInfo propertyInfo)
        {
            if (!IsEditorRuntimeWatchEnabled)
                return;

            if (attribute == null || propertyInfo == null || !propertyInfo.CanRead || propertyInfo.GetIndexParameters().Length > 0)
                return;

            MethodInfo getter = propertyInfo.GetGetMethod(true);
            if (getter == null || getter.IsStatic)
                return;

            if (!registeredWatchProperties.Add(propertyInfo))
                return;

            watchFields.Add(new WatchFieldRecord(attribute, propertyInfo));
            InvalidateMaterializedData();
        }

        public static void RegisterMethod(ESRuntimeWatchAttribute attribute, MethodInfo methodInfo)
        {
            if (!IsEditorRuntimeWatchEnabled)
                return;

            if (attribute == null || methodInfo == null || methodInfo.IsStatic || methodInfo.IsSpecialName)
                return;

            if (methodInfo.IsGenericMethodDefinition || methodInfo.ContainsGenericParameters)
                return;

            ParameterInfo[] parameters = methodInfo.GetParameters();
            if (parameters.Length > 1)
                return;

            if (parameters.Length == 1 && !IsSupportedWatchMethodParameterType(parameters[0].ParameterType))
                return;

            if (!registeredWatchMethods.Add(methodInfo))
                return;

            watchFields.Add(new WatchFieldRecord(attribute, methodInfo));
            InvalidateMaterializedData();
        }

        private static bool IsSupportedWatchMethodParameterType(Type type)
        {
            if (type == null)
                return false;

            return type == typeof(string)
                   || type == typeof(bool)
                   || type.IsEnum
                   || type == typeof(int)
                   || type == typeof(float)
                   || type == typeof(double)
                   || type == typeof(long)
                   || type == typeof(short)
                   || type == typeof(byte)
                   || type == typeof(uint)
                   || type == typeof(ulong)
                   || type == typeof(ushort)
                   || type == typeof(sbyte);
        }

        private static void InvalidateMaterializedData()
        {
            if (!IsEditorRuntimeWatchEnabled)
                return;

            entriesBuilt = false;
            fieldGraphBuilt = false;
            nestedPathCache.Clear();
            modulePathCache.Clear();
            compatibleParentEdgesCache.Clear();
            monoOwnerSchemesByTargetType.Clear();
            moduleOwnerSchemesByTargetType.Clear();
            moduleInnerSchemesByTargetType.Clear();
            entriesByOwnerType.Clear();
        }

        private static void EnsureEntriesBuilt()
        {
            if (!IsEditorRuntimeWatchEnabled)
            {
                entriesBuilt = true;
                entries.Clear();
                ownerTypes.Clear();
                registeredEntryKeys.Clear();
                entriesByOwnerType.Clear();
                return;
            }

            if (entriesBuilt)
                return;

            entriesBuilt = true;
            entries.Clear();
            ownerTypes.Clear();
            registeredEntryKeys.Clear();
            entriesByOwnerType.Clear();

            for (int i = 0; i < watchFields.Count; i++)
                BuildEntriesForMember(watchFields[i].Attribute, watchFields[i].MemberInfo);
        }

        private static void BuildEntriesForMember(ESRuntimeWatchAttribute attribute, MemberInfo memberInfo)
        {
            if (attribute == null || memberInfo == null)
                return;

            Type declaringType = memberInfo.DeclaringType;
            if (declaringType == null)
                return;

            if (typeof(MonoBehaviour).IsAssignableFrom(declaringType))
            {
                AddEntry(attribute, memberInfo, declaringType, Array.Empty<FieldInfo>(), RuntimeWatchEntryKind.Field, null, Array.Empty<FieldInfo>());
                return;
            }

            if (typeof(IESModule).IsAssignableFrom(declaringType))
            {
                foreach (var path in FindModuleOwnerPaths(declaringType))
                    AddEntry(attribute, memberInfo, path.OwnerType, path.Fields, RuntimeWatchEntryKind.Module, declaringType, Array.Empty<FieldInfo>());
                return;
            }

            foreach (var path in FindMonoOwnerPaths(declaringType))
                AddEntry(attribute, memberInfo, path.OwnerType, path.Fields, RuntimeWatchEntryKind.Field, null, Array.Empty<FieldInfo>());

            foreach (var path in FindNestedModuleOwnerPaths(declaringType))
                AddEntry(attribute, memberInfo, path.OwnerType, path.Fields, RuntimeWatchEntryKind.Module, path.ModuleType, path.ModulePath);
        }

        private static void AddEntry(ESRuntimeWatchAttribute attribute, MemberInfo memberInfo, Type ownerType, FieldInfo[] ownerPath, RuntimeWatchEntryKind kind, Type moduleType, FieldInfo[] modulePath)
        {
            if (ownerType == null)
                return;

            if (!typeof(MonoBehaviour).IsAssignableFrom(ownerType))
            {
                rejectedNonMonoOwnerCount++;
                return;
            }

            if (!IsValidRuntimeWatchPath(ownerPath, kind, modulePath))
            {
                rejectedInvalidPathCount++;
                return;
            }

            string key = BuildEntryKey(ownerType, ownerPath, memberInfo, kind, moduleType, modulePath);
            if (!registeredEntryKeys.Add(key))
                return;

            if (!ownerTypes.Contains(ownerType))
                ownerTypes.Add(ownerType);

            var entry = new Entry(attribute, memberInfo, ownerType, ownerPath, key, kind, moduleType, modulePath);
            entries.Add(entry);
            if (!entriesByOwnerType.TryGetValue(ownerType, out var indexedEntries))
            {
                indexedEntries = new List<Entry>();
                entriesByOwnerType[ownerType] = indexedEntries;
            }
            indexedEntries.Add(entry);
        }

        private static IEnumerable<OwnerFieldPath> FindMonoOwnerPaths(Type targetDeclaringType)
        {
            if (nestedPathCache.TryGetValue(targetDeclaringType, out var cachedPaths))
                return cachedPaths;

            EnsureFieldGraphBuilt();
            if (!monoOwnerSchemesByTargetType.TryGetValue(targetDeclaringType, out var result))
            {
                result = FindOwnerPathsByReverseGraph(targetDeclaringType, type => typeof(MonoBehaviour).IsAssignableFrom(type));
                AddSchemes(monoOwnerSchemesByTargetType, targetDeclaringType, result);
            }

            nestedPathCache[targetDeclaringType] = result;
            return result;
        }

        private static IEnumerable<ModuleOwnerFieldPath> FindNestedModuleOwnerPaths(Type targetDeclaringType)
        {
            EnsureFieldGraphBuilt();
            if (!moduleInnerSchemesByTargetType.ContainsKey(targetDeclaringType))
            {
                var moduleInnerSchemes = FindOwnerPathsByReverseGraph(targetDeclaringType, type => typeof(IESModule).IsAssignableFrom(type));
                AddSchemes(moduleInnerSchemesByTargetType, targetDeclaringType, moduleInnerSchemes);
            }

            if (!moduleInnerSchemesByTargetType.TryGetValue(targetDeclaringType, out var moduleInnerPaths))
                yield break;

            foreach (var moduleInnerPath in moduleInnerPaths)
            {
                Type moduleType = moduleInnerPath.OwnerType;
                if (moduleType == null)
                    continue;

                foreach (var ownerPath in FindModuleOwnerPaths(moduleType))
                    yield return new ModuleOwnerFieldPath(ownerPath.OwnerType, ownerPath.Fields, moduleType, moduleInnerPath.Fields);
            }
        }

        private static IEnumerable<OwnerFieldPath> FindModuleOwnerPaths(Type targetModuleType)
        {
            if (modulePathCache.TryGetValue(targetModuleType, out var cachedPaths))
                return cachedPaths;

            EnsureFieldGraphBuilt();
            if (!moduleOwnerSchemesByTargetType.TryGetValue(targetModuleType, out var schemes))
            {
                BuildModuleFastSchemes(targetModuleType);
                moduleOwnerSchemesByTargetType.TryGetValue(targetModuleType, out schemes);
            }

            var result = schemes ?? new List<OwnerFieldPath>();
            modulePathCache[targetModuleType] = result;
            return result;
        }

        private static List<OwnerFieldPath> FindModuleInnerPaths(Type moduleType, Type targetDeclaringType)
        {
            EnsureFieldGraphBuilt();
            if (!moduleInnerSchemesByTargetType.TryGetValue(targetDeclaringType, out var schemes))
                return new List<OwnerFieldPath>();

            return schemes.Where(path => path.OwnerType == moduleType).ToList();
        }

        private static IEnumerable<Type> GetCandidateMonoTypes()
        {
            EnsureFieldGraphBuilt();
            if (candidateMonoTypeCache != null)
                return candidateMonoTypeCache;

            candidateMonoTypeCache = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type != null && !type.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(type))
                        candidateMonoTypeCache.Add(type);
                }
            }

            return candidateMonoTypeCache;
        }

        private static IEnumerable<Type> GetCandidateModuleTypes()
        {
            EnsureFieldGraphBuilt();
            return candidateModuleTypeCache;
        }

        private static void EnsureFieldGraphBuilt()
        {
            if (!IsEditorRuntimeWatchEnabled)
                return;

            if (fieldGraphBuilt)
                return;

            fieldGraphBuilt = true;
            candidateMonoTypeCache = new List<Type>();
            candidateModuleTypeCache = new List<Type>();
            parentEdgesByChildType.Clear();
            compatibleParentEdgesCache.Clear();
            monoOwnerSchemesByTargetType.Clear();
            moduleOwnerSchemesByTargetType.Clear();
            moduleInnerSchemesByTargetType.Clear();
            hostingTypeInfos.Clear();
            fieldGraphEdgeCount = 0;
            schemeLimitHitCount = 0;
            rejectedNonMonoOwnerCount = 0;
            rejectedInvalidPathCount = 0;

            foreach (Type type in EnumerateRuntimeTypes())
            {
                if (type == null)
                    continue;

                if (!type.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(type))
                    candidateMonoTypeCache.Add(type);

                if (!type.IsAbstract && typeof(IESModule).IsAssignableFrom(type))
                    candidateModuleTypeCache.Add(type);

                if (!typeof(IESModule).IsAssignableFrom(type) && TryGetHostingModuleType(type, out Type hostedModuleType))
                    hostingTypeInfos.Add(new HostingTypeInfo(type, hostedModuleType));

                foreach (FieldInfo field in type.GetFields(InstanceFieldFlags))
                {
                    if (field.DeclaringType != type)
                        continue;

                    if (field.IsStatic || field.IsLiteral)
                        continue;

                    if (IsRuntimeBackReferenceField(field))
                        continue;

                    Type fieldType = NormalizeFieldType(field.FieldType);
                    if (!CanTraverseFieldType(fieldType))
                        continue;

                    if (!parentEdgesByChildType.TryGetValue(fieldType, out var edges))
                    {
                        edges = new List<FieldParentEdge>();
                        parentEdgesByChildType[fieldType] = edges;
                    }
                    edges.Add(new FieldParentEdge(type, fieldType, field));
                    fieldGraphEdgeCount++;
                }
            }
        }

        private static bool IsRuntimeBackReferenceField(FieldInfo field)
        {
            if (field == null)
                return true;

            string fieldName = field.Name;
            if (fieldName == "MyDomain" || fieldName == "MyCore" || fieldName == "myCore")
                return true;

            if (fieldName != null && fieldName.Contains("k__BackingField"))
                return true;

            return false;
        }

        private static void BuildModuleFastSchemes(Type moduleType)
        {
            if (moduleType == null)
                return;

            var schemes = new List<OwnerFieldPath>();
            foreach (HostingTypeInfo hostingInfo in hostingTypeInfos)
            {
                if (hostingInfo.HostingType == null || hostingInfo.HostedModuleType == null)
                    continue;

                if (!hostingInfo.HostedModuleType.IsAssignableFrom(moduleType)
                    && !moduleType.IsAssignableFrom(hostingInfo.HostedModuleType))
                    continue;

                if (typeof(MonoBehaviour).IsAssignableFrom(hostingInfo.HostingType))
                {
                    schemes.Add(new OwnerFieldPath(hostingInfo.HostingType, Array.Empty<FieldInfo>()));
                }
                else if (!monoOwnerSchemesByTargetType.TryGetValue(hostingInfo.HostingType, out var hostingOwnerSchemes))
                {
                    hostingOwnerSchemes = FindOwnerPathsByReverseGraph(hostingInfo.HostingType, type => typeof(MonoBehaviour).IsAssignableFrom(type));
                    AddSchemes(monoOwnerSchemesByTargetType, hostingInfo.HostingType, hostingOwnerSchemes);
                    schemes.AddRange(hostingOwnerSchemes);
                }
                else
                {
                    schemes.AddRange(hostingOwnerSchemes);
                }
            }

            AddSchemes(moduleOwnerSchemesByTargetType, moduleType, schemes);
        }

        private static void AddSchemes(Dictionary<Type, List<OwnerFieldPath>> targetMap, Type targetType, List<OwnerFieldPath> schemes)
        {
            if (targetType == null)
                return;

            targetMap[targetType] = schemes ?? new List<OwnerFieldPath>();
        }

        private static IEnumerable<Type> EnumerateRuntimeTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!ShouldScanAssembly(assembly))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type != null && !type.IsGenericTypeDefinition)
                        yield return type;
                }
            }
        }

        private static bool ShouldScanAssembly(Assembly assembly)
        {
            if (!IsEditorRuntimeWatchEnabled)
                return false;

            if (assembly == null)
                return false;

            string assemblyName = assembly.GetName().Name;
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            if (assemblyName == "Assembly-CSharp"
                || assemblyName == "Assembly-CSharp-firstpass"
                || assemblyName == "Assembly-CSharp-Editor"
                || assemblyName == "Assembly-CSharp-Editor-firstpass"
                || assemblyName.StartsWith("ES", StringComparison.Ordinal))
            {
                return true;
            }

            for (int i = 0; i < watchFields.Count; i++)
            {
                Type declaringType = watchFields[i].MemberInfo != null ? watchFields[i].MemberInfo.DeclaringType : null;
                if (declaringType != null && declaringType.Assembly == assembly)
                    return true;
            }

            return false;
        }

        private static List<OwnerFieldPath> FindOwnerPathsByReverseGraph(Type targetType, Func<Type, bool> ownerPredicate)
        {
            var results = new List<OwnerFieldPath>();
            WalkParents(targetType, new List<FieldInfo>(), new HashSet<Type>(), results, ownerPredicate, 0);
            if (results.Count >= MaxSchemesPerTargetType)
                schemeLimitHitCount++;

            return results
                .OrderBy(path => path.Fields != null ? path.Fields.Length : 0)
                .ThenBy(path => path.OwnerType != null ? path.OwnerType.FullName : string.Empty, StringComparer.Ordinal)
                .ToList();
        }

        private static void WalkParents(Type currentType, List<FieldInfo> pathFromOwnerToCurrent, HashSet<Type> visiting, List<OwnerFieldPath> results, Func<Type, bool> ownerPredicate, int depth)
        {
            if (currentType == null || depth >= MaxNestedDepth || results.Count >= MaxSchemesPerTargetType || !visiting.Add(currentType))
                return;

            foreach (FieldParentEdge edge in GetCompatibleParentEdges(currentType))
            {
                if (results.Count >= MaxSchemesPerTargetType)
                    break;

                var nextPath = new List<FieldInfo>(pathFromOwnerToCurrent.Count + 1);
                nextPath.Add(edge.Field);
                nextPath.AddRange(pathFromOwnerToCurrent);

                if (ownerPredicate(edge.ParentType))
                    results.Add(new OwnerFieldPath(edge.ParentType, nextPath.ToArray()));

                if (typeof(MonoBehaviour).IsAssignableFrom(edge.ParentType)
                    || typeof(IESModule).IsAssignableFrom(edge.ParentType))
                    continue;

                WalkParents(edge.ParentType, nextPath, visiting, results, ownerPredicate, depth + 1);
            }

            visiting.Remove(currentType);
        }

        private static List<FieldParentEdge> GetCompatibleParentEdges(Type targetType)
        {
            if (compatibleParentEdgesCache.TryGetValue(targetType, out var cached))
                return cached;

            var result = new List<FieldParentEdge>();
            foreach (var pair in parentEdgesByChildType)
            {
                Type fieldType = pair.Key;
                if (fieldType.IsAssignableFrom(targetType))
                    result.AddRange(pair.Value);
            }

            compatibleParentEdgesCache[targetType] = result;
            return result;
        }

        private static Type NormalizeFieldType(Type type)
        {
            if (type == null)
                return null;

            if (type.IsArray)
                return null;

            return type;
        }

        private static bool CanTraverseFieldType(Type type)
        {
            if (type == null || type.IsPrimitive || type.IsEnum || type == typeof(string))
                return false;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;

            if (type.IsPointer || type.IsGenericParameter || type.ContainsGenericParameters || type.IsInterface)
                return false;

            if (type.Namespace != null && type.Namespace.StartsWith("System", StringComparison.Ordinal))
                return false;

            if (typeof(Delegate).IsAssignableFrom(type))
                return false;

            return type.IsClass || (type.IsValueType && !type.IsPrimitive && !type.IsEnum);
        }

        private static bool IsValidRuntimeWatchPath(FieldInfo[] ownerPath, RuntimeWatchEntryKind kind, FieldInfo[] modulePath)
        {
            if (ContainsRuntimeBackReference(ownerPath) || ContainsRuntimeBackReference(modulePath))
                return false;

            if (kind != RuntimeWatchEntryKind.Module)
                return true;

            if (ownerPath == null)
                return true;

            for (int i = 0; i < ownerPath.Length; i++)
            {
                FieldInfo field = ownerPath[i];
                if (field == null)
                    return false;

                Type declaringType = field.DeclaringType;
                Type fieldType = NormalizeFieldType(field.FieldType);
                if ((declaringType != null && typeof(IESModule).IsAssignableFrom(declaringType))
                    || (fieldType != null && typeof(IESModule).IsAssignableFrom(fieldType)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsRuntimeBackReference(FieldInfo[] path)
        {
            if (path == null)
                return false;

            for (int i = 0; i < path.Length; i++)
            {
                if (IsRuntimeBackReferenceField(path[i]))
                    return true;
            }

            return false;
        }

        private static bool TryGetHostingModuleType(Type type, out Type hostedModuleType)
        {
            hostedModuleType = null;
            if (type == null)
                return false;

            foreach (Type interfaceType in type.GetInterfaces())
            {
                if (!interfaceType.IsGenericType)
                    continue;

                if (interfaceType.GetGenericTypeDefinition() == typeof(IESHosting<>))
                {
                    hostedModuleType = interfaceType.GetGenericArguments()[0];
                    return hostedModuleType != null;
                }
            }

            return false;
        }

        private static string BuildEntryKey(Type ownerType, FieldInfo[] ownerPath, MemberInfo memberInfo, RuntimeWatchEntryKind kind, Type moduleType, FieldInfo[] modulePath)
        {
            string pathKey = ownerPath == null || ownerPath.Length == 0
                ? string.Empty
                : string.Join("/", ownerPath.Select(GetFieldKey));
            string modulePathKey = modulePath == null || modulePath.Length == 0
                ? string.Empty
                : string.Join("/", modulePath.Select(GetFieldKey));
            return kind + "|" + ownerType.AssemblyQualifiedName + "|" + pathKey + "|" + (moduleType != null ? moduleType.AssemblyQualifiedName : string.Empty) + "|" + modulePathKey + "|" + GetMemberKey(memberInfo);
        }

        private static string GetFieldKey(FieldInfo fieldInfo)
        {
            return fieldInfo.Module.ModuleVersionId + ":" + fieldInfo.MetadataToken;
        }

        private static string GetMemberKey(MemberInfo memberInfo)
        {
            if (memberInfo == null)
                return "<null>";

            return memberInfo.Module.ModuleVersionId + ":" + memberInfo.MetadataToken;
        }

        public static string GetMemberDisplayName(MemberInfo memberInfo, ESRuntimeWatchAttribute attribute = null, string fallback = null)
        {
            if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Label))
                return attribute.Label;

            if (memberInfo is MethodInfo methodInfo)
            {
                string buttonLabel = TryGetButtonLabel(memberInfo);
                if (!string.IsNullOrWhiteSpace(buttonLabel))
                    return buttonLabel;

                if (!string.IsNullOrWhiteSpace(fallback))
                    return fallback;

                return methodInfo.Name;
            }

            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;

            return memberInfo != null ? memberInfo.Name : "<null>";
        }

        public static string TryGetButtonLabel(MemberInfo memberInfo)
        {
            if (memberInfo == null)
                return null;

            IList<CustomAttributeData> attributeDatas;
            try
            {
                attributeDatas = memberInfo.GetCustomAttributesData();
            }
            catch
            {
                return null;
            }

            foreach (CustomAttributeData attributeData in attributeDatas)
            {
                Type attributeType = attributeData.AttributeType;
                if (attributeType == null || attributeType.FullName != "Sirenix.OdinInspector.ButtonAttribute")
                    continue;

                if (attributeData.ConstructorArguments.Count > 0)
                {
                    object ctorValue = attributeData.ConstructorArguments[0].Value;
                    if (ctorValue is string ctorText && !string.IsNullOrWhiteSpace(ctorText))
                        return ctorText;
                }

                foreach (var namedArg in attributeData.NamedArguments)
                {
                    if (namedArg.TypedValue.Value is string namedText && !string.IsNullOrWhiteSpace(namedText))
                    {
                        if (namedArg.MemberName == "Name"
                            || namedArg.MemberName == "ButtonName"
                            || namedArg.MemberName == "Text"
                            || namedArg.MemberName == "Label")
                        {
                            return namedText;
                        }
                    }
                }
            }

            return null;
        }

        private readonly struct OwnerFieldPath
        {
            private readonly Type ownerType;
            public readonly FieldInfo[] Fields;
            public Type OwnerType => ownerType ?? (Fields.Length > 0 ? Fields[0].DeclaringType : null);

            public OwnerFieldPath(FieldInfo[] fields)
            {
                ownerType = fields != null && fields.Length > 0 ? fields[0].DeclaringType : null;
                Fields = fields ?? Array.Empty<FieldInfo>();
            }

            public OwnerFieldPath(Type ownerType, FieldInfo[] fields)
            {
                this.ownerType = ownerType;
                Fields = fields ?? Array.Empty<FieldInfo>();
            }
        }

        private readonly struct WatchFieldRecord
        {
            public readonly ESRuntimeWatchAttribute Attribute;
            public readonly MemberInfo MemberInfo;

            public WatchFieldRecord(ESRuntimeWatchAttribute attribute, MemberInfo memberInfo)
            {
                Attribute = attribute;
                MemberInfo = memberInfo;
            }
        }

        private readonly struct FieldParentEdge
        {
            public readonly Type ParentType;
            public readonly Type ChildType;
            public readonly FieldInfo Field;

            public FieldParentEdge(Type parentType, Type childType, FieldInfo field)
            {
                ParentType = parentType;
                ChildType = childType;
                Field = field;
            }
        }

        private readonly struct HostingTypeInfo
        {
            public readonly Type HostingType;
            public readonly Type HostedModuleType;

            public HostingTypeInfo(Type hostingType, Type hostedModuleType)
            {
                HostingType = hostingType;
                HostedModuleType = hostedModuleType;
            }
        }

        private readonly struct ModuleOwnerFieldPath
        {
            private readonly Type ownerType;
            public readonly FieldInfo[] Fields;
            public readonly Type ModuleType;
            public readonly FieldInfo[] ModulePath;
            public Type OwnerType => ownerType ?? (Fields.Length > 0 ? Fields[0].DeclaringType : null);

            public ModuleOwnerFieldPath(Type ownerType, FieldInfo[] fields, Type moduleType, FieldInfo[] modulePath)
            {
                this.ownerType = ownerType;
                Fields = fields ?? Array.Empty<FieldInfo>();
                ModuleType = moduleType;
                ModulePath = modulePath ?? Array.Empty<FieldInfo>();
            }
        }

        public enum RuntimeWatchEntryKind
        {
            Field,
            Module
        }

        public readonly struct Entry
        {
            public readonly ESRuntimeWatchAttribute Attribute;
            public readonly MemberInfo MemberInfo;
            public readonly Type OwnerType;
            public readonly FieldInfo[] OwnerPath;
            public readonly string EntryKey;
            public readonly RuntimeWatchEntryKind Kind;
            public readonly Type ModuleType;
            public readonly FieldInfo[] ModulePath;
            public bool IsNested => OwnerPath != null && OwnerPath.Length > 0;
            public bool IsMethod => MemberInfo is MethodInfo;
            public bool RequiresManualInvoke => MemberInfo is MethodInfo methodInfo
                && (methodInfo.GetParameters().Length > 0
                    || methodInfo.ReturnType == typeof(void)
                    || !string.IsNullOrWhiteSpace(TryGetButtonLabel(MemberInfo)));
            public string DisplayName => GetMemberDisplayName(MemberInfo, Attribute, MemberPath);
            public string ActionLabel => IsMethod ? (TryGetButtonLabel(MemberInfo) ?? MemberInfo.Name) : null;
            public string MemberPath => IsNested
                ? string.Join(".", OwnerPath.Select(field => field.Name)) + (Kind == RuntimeWatchEntryKind.Module ? ".[" + (ModuleType != null ? ModuleType.Name : MemberInfo.DeclaringType.Name) + "]." + BuildModuleMemberPath() : "." + MemberInfo.Name)
                : MemberInfo.Name;
            public string RequiredTag => Attribute != null ? Attribute.RequiredTag : null;
            public string ShowIf => Attribute != null ? Attribute.ShowIf : null;

            public Entry(ESRuntimeWatchAttribute attribute, MemberInfo memberInfo, Type ownerType, FieldInfo[] ownerPath, string entryKey, RuntimeWatchEntryKind kind, Type moduleType, FieldInfo[] modulePath)
            {
                Attribute = attribute;
                MemberInfo = memberInfo;
                OwnerType = ownerType;
                OwnerPath = ownerPath ?? Array.Empty<FieldInfo>();
                EntryKey = entryKey;
                Kind = kind;
                ModuleType = moduleType;
                ModulePath = modulePath ?? Array.Empty<FieldInfo>();
            }

            private string BuildModuleMemberPath()
            {
                if (ModulePath == null || ModulePath.Length == 0)
                    return MemberInfo.Name;

                return string.Join(".", ModulePath.Select(field => field.Name)) + "." + MemberInfo.Name;
            }
        }
    }
}
