using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ES
{
    public static class ESAssemblyLoadTiming
    {
        [InspectorName("-100 程序集加载后")]
        public static readonly int AfterAssembliesLoaded = -100;

        [InspectorName("0 首场景加载前")]
        public static readonly int BeforeFirstSceneLoad = 0;

        [InspectorName("50 首场景加载后")]
        public static readonly int AfterFirstSceneLoad = 50;
    }

    /// <summary>
    /// ES 程序集流。
    /// 这里只保留 Editor 程序集流：用于编辑器启动、刷新后扫描指定程序集并执行编辑器注册器。
    /// Runtime 程序集流已经废弃，不再提供运行时注册入口。
    /// </summary>
    public static class ESAssemblyStream
    {
#if UNITY_EDITOR
        private static readonly string[] EditorValidAssemblyNames =
        {
            "ES_Design",
            "ES_Stand",
            "ES_Editor",
            "ES_Logic",
            "Assembly-CSharp-Editor",
            "Assembly-CSharp-Editor-firstpass",
            "Assembly-CSharp",
            "Assembly-CSharp-firstpass",
            "NewAssem"
        };

        private static readonly BindingFlags MemberFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // TypeCache returns declared members. The reflection fallback must use the same scope.
        private static readonly BindingFlags DeclaredMemberFlags = MemberFlags | BindingFlags.DeclaredOnly;

        private static readonly Type DefineClassAttribute = typeof(EditorRegister_FOR_ClassAttribute<>).GetGenericTypeDefinition();
        private static readonly Type DefineFieldAttribute = typeof(EditorRegister_FOR_FieldAttribute<>).GetGenericTypeDefinition();
        private static readonly Type DefinePropertyAttribute = typeof(EditorRegister_FOR_PropertyAttribute<>).GetGenericTypeDefinition();
        private static readonly Type DefineMethodAttribute = typeof(EditorRegister_FOR_MethodAttribute<>).GetGenericTypeDefinition();
        private static readonly Type DefineSingleton = typeof(EditorRegister_FOR_Singleton<>).GetGenericTypeDefinition();
        private static readonly Type DefineAsSubclass = typeof(EditorRegister_FOR_AsSubclass<>).GetGenericTypeDefinition();

        [InitializeOnLoadMethod]
        private static void EditorInitLoad()
        {
            try
            {
                DateTime startTime = DateTime.Now;
                StringBuilder performanceReport = new(1024);
                EditorAssemblyPart.Execute(performanceReport);
                performanceReport.Insert(0, "[ESAssemblyStream] Editor Assembly Stream completed, elapsed " + (DateTime.Now - startTime).TotalMilliseconds.ToString("F2") + " ms\n");
                Debug.Log(performanceReport.ToString());
#if false
                Debug.Log("[ESAssemblyStream] Editor 程序集流完成，耗时 " + (DateTime.Now - startTime).TotalMilliseconds.ToString("F2") + " ms");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError("[ESAssemblyStream] Editor 程序集流执行失败：\n" + ex);
            }
        }

        private static readonly long SlowOperationThresholdTicks = Math.Max(1, Stopwatch.Frequency / 10000);

        private static void AppendSlowOperation(StringBuilder performanceReport, long startTimestamp, string category, int? order = null, Type registerType = null, Type targetType = null)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsedTicks < SlowOperationThresholdTicks)
            {
                return;
            }

            performanceReport.Append("[ESAssemblyStream][Slow] ").Append(category);
            if (order.HasValue)
            {
                performanceReport.Append(" | Order=").Append(order.Value);
            }

            if (registerType != null)
            {
                performanceReport.Append(" | Register=").Append(registerType.FullName);
            }

            if (targetType != null)
            {
                performanceReport.Append(" | Target=").Append(targetType.FullName);
            }

            performanceReport.Append(" | ").Append(elapsedTicks * 1000d / Stopwatch.Frequency).Append(" ms\n");
        }

        private static class EditorAssemblyPart
        {
            private static readonly List<Assembly> ValidAssemblies = new(32);
            private static readonly Dictionary<Assembly, Type[]> AssemblyTypes = new(32);
            private static readonly List<ESAS_EditorRegister_AB> Registers = new(32);

            private static readonly Dictionary<int, List<Func<Type, bool>>> SingletonHandlers = new(16);
            private static readonly Dictionary<int, List<Func<Type, bool>>> SubclassHandlers = new(16);
            private static readonly Dictionary<int, List<Func<Type, bool>>> ClassAttributeHandlers = new(16);
            private static readonly Dictionary<int, List<Func<FieldInfo, bool>>> FieldAttributeHandlers = new(16);
            private static readonly Dictionary<int, List<Func<PropertyInfo, bool>>> PropertyAttributeHandlers = new(16);
            private static readonly Dictionary<int, List<Func<MethodInfo, bool>>> MethodAttributeHandlers = new(16);

            // TypeCache supplies the exact declared targets. A failed query deliberately falls back to
            // the original full scan, so editor registration is never skipped because of cache failure.
            private static readonly Dictionary<int, TypeCandidateLookup> TypeCandidates = new(16);
            private static readonly Dictionary<int, MemberCandidateLookup<FieldInfo>> FieldCandidates = new(16);
            private static readonly Dictionary<int, MemberCandidateLookup<MethodInfo>> MethodCandidates = new(16);

            private static readonly MethodInfo GetTypesWithAttributeMethod = FindTypeCacheMethod("GetTypesWithAttribute");
            private static readonly MethodInfo GetTypesDerivedFromMethod = FindTypeCacheMethod("GetTypesDerivedFrom");
            private static readonly MethodInfo GetFieldsWithAttributeMethod = FindTypeCacheMethod("GetFieldsWithAttribute");
            private static readonly MethodInfo GetMethodsWithAttributeMethod = FindTypeCacheMethod("GetMethodsWithAttribute");

            private sealed class TypeCandidateLookup
            {
                public bool RequiresFullScan;
                public readonly HashSet<Type> Types = new();
            }

            private sealed class MemberCandidateLookup<TMember> where TMember : MemberInfo
            {
                public bool RequiresFullScan;
                public readonly Dictionary<Type, List<TMember>> MembersByDeclaringType = new();
            }

            public static void Execute(StringBuilder performanceReport)
            {
                long timestamp = Stopwatch.GetTimestamp();
                ClearCache();
                AppendSlowOperation(performanceReport, timestamp, "Clear cache");

                timestamp = Stopwatch.GetTimestamp();
                CollectAssembliesAndRegisters();
                AppendSlowOperation(performanceReport, timestamp, "Collect assemblies and registers");

                timestamp = Stopwatch.GetTimestamp();
                BuildHandlers(performanceReport);
                AppendSlowOperation(performanceReport, timestamp, "Build register handlers");

                timestamp = Stopwatch.GetTimestamp();
                ApplyHandlers(performanceReport);
                AppendSlowOperation(performanceReport, timestamp, "Apply all handlers");

                timestamp = Stopwatch.GetTimestamp();
                ClearCache();
                AppendSlowOperation(performanceReport, timestamp, "Clear cache");
            }

            private static void ClearCache()
            {
                ValidAssemblies.Clear();
                AssemblyTypes.Clear();
                Registers.Clear();
                SingletonHandlers.Clear();
                SubclassHandlers.Clear();
                ClassAttributeHandlers.Clear();
                FieldAttributeHandlers.Clear();
                PropertyAttributeHandlers.Clear();
                MethodAttributeHandlers.Clear();
                TypeCandidates.Clear();
                FieldCandidates.Clear();
                MethodCandidates.Clear();
            }

            private static void CollectAssembliesAndRegisters()
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

                for (int i = 0; i < assemblies.Length; i++)
                {
                    Assembly assembly = assemblies[i];
                    if (!IsValidAssembly(assembly))
                    {
                        continue;
                    }

                    Type[] types = GetTypesSafely(assembly);
                    ValidAssemblies.Add(assembly);
                    AssemblyTypes[assembly] = types;

                    for (int j = 0; j < types.Length; j++)
                    {
                        Type type = types[j];
                        if (type == null || type.IsAbstract || !typeof(ESAS_EditorRegister_AB).IsAssignableFrom(type))
                        {
                            continue;
                        }

                        if (Activator.CreateInstance(type) is ESAS_EditorRegister_AB register)
                        {
                            Registers.Add(register);
                        }
                    }
                }

                ValidAssemblies.Sort((a, b) => GetAssemblyOrder(a).CompareTo(GetAssemblyOrder(b)));
                Registers.Sort((a, b) => a.Order.CompareTo(b.Order));
            }

            private static void BuildHandlers(StringBuilder performanceReport)
            {
                for (int i = 0; i < Registers.Count; i++)
                {
                    ESAS_EditorRegister_AB register = Registers[i];
                    Type registerType = register.GetType();

                    if (!TryFindRegisterBase(registerType, out Type registerBaseType))
                    {
                        continue;
                    }

                    Type genericDefine = registerBaseType.GetGenericTypeDefinition();
                    if (genericDefine == DefineClassAttribute)
                    {
                        AddClassAttributeHandler(register, registerType, registerBaseType, performanceReport);
                    }
                    else if (genericDefine == DefineFieldAttribute)
                    {
                        AddFieldAttributeHandler(register, registerType, registerBaseType, performanceReport);
                    }
                    else if (genericDefine == DefinePropertyAttribute)
                    {
                        AddPropertyAttributeHandler(register, registerType, registerBaseType, performanceReport);
                    }
                    else if (genericDefine == DefineMethodAttribute)
                    {
                        AddMethodAttributeHandler(register, registerType, registerBaseType, performanceReport);
                    }
                    else if (genericDefine == DefineSingleton)
                    {
                        AddSingletonHandler(register, registerType, registerBaseType, performanceReport);
                    }
                    else if (genericDefine == DefineAsSubclass)
                    {
                        AddSubclassHandler(register, registerType, registerBaseType, performanceReport);
                    }
                }
            }

            private static void ApplyHandlers(StringBuilder performanceReport)
            {
                List<int> orders = CollectOrders();
                orders.Sort();

                for (int i = 0; i < orders.Count; i++)
                {
                    int order = orders[i];
                    long timestamp = Stopwatch.GetTimestamp();
                    ApplyTypeHandlers(order);
                    AppendSlowOperation(performanceReport, timestamp, "Type handler scan", order);

                    timestamp = Stopwatch.GetTimestamp();
                    ApplyMemberHandlers(order);
                    AppendSlowOperation(performanceReport, timestamp, "Member attribute scan", order);
                }
            }

            private static void ApplyTypeHandlers(int order)
            {
                SingletonHandlers.TryGetValue(order, out List<Func<Type, bool>> singletonHandlers);
                SubclassHandlers.TryGetValue(order, out List<Func<Type, bool>> subclassHandlers);
                ClassAttributeHandlers.TryGetValue(order, out List<Func<Type, bool>> classAttributeHandlers);

                bool hasSingleton = singletonHandlers != null && singletonHandlers.Count > 0;
                bool hasSubclass = subclassHandlers != null && subclassHandlers.Count > 0;
                bool hasClassAttribute = classAttributeHandlers != null && classAttributeHandlers.Count > 0;

                if (!hasSingleton && !hasSubclass && !hasClassAttribute)
                {
                    return;
                }

                bool hasCachedCandidates = TypeCandidates.TryGetValue(order, out TypeCandidateLookup candidates) && !candidates.RequiresFullScan;

                for (int i = 0; i < ValidAssemblies.Count; i++)
                {
                    Type[] types = AssemblyTypes[ValidAssemblies[i]];
                    for (int j = 0; j < types.Length; j++)
                    {
                        Type type = types[j];
                        if (type == null || (hasCachedCandidates && !candidates.Types.Contains(type)))
                        {
                            continue;
                        }

                        InvokeTypeHandlers(type, singletonHandlers);
                        InvokeTypeHandlers(type, subclassHandlers);
                        InvokeTypeHandlers(type, classAttributeHandlers);
                    }
                }
            }

            private static void ApplyMemberHandlers(int order)
            {
                FieldAttributeHandlers.TryGetValue(order, out List<Func<FieldInfo, bool>> fieldHandlers);
                PropertyAttributeHandlers.TryGetValue(order, out List<Func<PropertyInfo, bool>> propertyHandlers);
                MethodAttributeHandlers.TryGetValue(order, out List<Func<MethodInfo, bool>> methodHandlers);

                bool hasField = fieldHandlers != null && fieldHandlers.Count > 0;
                bool hasProperty = propertyHandlers != null && propertyHandlers.Count > 0;
                bool hasMethod = methodHandlers != null && methodHandlers.Count > 0;

                bool hasCachedFields = FieldCandidates.TryGetValue(order, out MemberCandidateLookup<FieldInfo> fieldCandidates) && !fieldCandidates.RequiresFullScan;
                bool hasCachedMethods = MethodCandidates.TryGetValue(order, out MemberCandidateLookup<MethodInfo> methodCandidates) && !methodCandidates.RequiresFullScan;

                if (!hasField && !hasProperty && !hasMethod)
                {
                    return;
                }

                for (int i = 0; i < ValidAssemblies.Count; i++)
                {
                    Type[] types = AssemblyTypes[ValidAssemblies[i]];
                    for (int j = 0; j < types.Length; j++)
                    {
                        Type type = types[j];
                        if (type == null)
                        {
                            continue;
                        }

                        if (hasField)
                        {
                            if (hasCachedFields)
                            {
                                if (fieldCandidates.MembersByDeclaringType.TryGetValue(type, out List<FieldInfo> fields))
                                {
                                    for (int f = 0; f < fields.Count; f++)
                                    {
                                        InvokeMemberHandlers(fields[f], fieldHandlers);
                                    }
                                }
                            }
                            else
                            {
                                FieldInfo[] fields = type.GetFields(DeclaredMemberFlags);
                                for (int f = 0; f < fields.Length; f++)
                                {
                                    InvokeMemberHandlers(fields[f], fieldHandlers);
                                }
                            }
                        }

                        if (hasProperty)
                        {
                            PropertyInfo[] properties = type.GetProperties(MemberFlags);
                            for (int p = 0; p < properties.Length; p++)
                            {
                                PropertyInfo property = properties[p];
                                if (property.CanRead && property.GetIndexParameters().Length == 0)
                                {
                                    InvokeMemberHandlers(property, propertyHandlers);
                                }
                            }
                        }

                        if (hasMethod)
                        {
                            if (hasCachedMethods)
                            {
                                if (methodCandidates.MembersByDeclaringType.TryGetValue(type, out List<MethodInfo> methods))
                                {
                                    for (int m = 0; m < methods.Count; m++)
                                    {
                                        InvokeMemberHandlers(methods[m], methodHandlers);
                                    }
                                }
                            }
                            else
                            {
                                MethodInfo[] methods = type.GetMethods(DeclaredMemberFlags);
                                for (int m = 0; m < methods.Length; m++)
                                {
                                    InvokeMemberHandlers(methods[m], methodHandlers);
                                }
                            }
                        }
                    }
                }
            }

            private static void AddSingletonHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType, StringBuilder performanceReport)
            {
                Type targetType = registerBaseType.GetGenericArguments()[0];
                MethodInfo handleMethod = GetHandleMethod(registerType);
                AddTypeCandidates(register.Order, GetTypesDerivedFrom(targetType));

                AddHandler(SingletonHandlers, register.Order, type =>
                {
                    if (type.IsAbstract || !targetType.IsAssignableFrom(type))
                    {
                        return false;
                    }

                    long timestamp = Stopwatch.GetTimestamp();
                    object instance = Activator.CreateInstance(type);
                    handleMethod.Invoke(register, new[] { instance });
                    AppendSlowOperation(performanceReport, timestamp, "Singleton registration", register.Order, registerType, type);
                    return true;
                });
            }

            private static void AddSubclassHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType, StringBuilder performanceReport)
            {
                Type targetType = registerBaseType.GetGenericArguments()[0];
                MethodInfo handleMethod = GetHandleMethod(registerType);
                AddTypeCandidates(register.Order, GetTypesDerivedFrom(targetType));

                AddHandler(SubclassHandlers, register.Order, type =>
                {
                    if (type.IsAbstract || type == targetType || !targetType.IsAssignableFrom(type))
                    {
                        return false;
                    }

                    long timestamp = Stopwatch.GetTimestamp();
                    handleMethod.Invoke(register, new object[] { type });
                    AppendSlowOperation(performanceReport, timestamp, "Subclass registration", register.Order, registerType, type);
                    return true;
                });
            }

            private static void AddClassAttributeHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType, StringBuilder performanceReport)
            {
                Type attributeType = registerBaseType.GetGenericArguments()[0];
                MethodInfo handleMethod = GetHandleMethod(registerType);
                AddTypeCandidates(register.Order, GetTypesWithAttribute(attributeType));

                AddHandler(ClassAttributeHandlers, register.Order, type =>
                {
                    Attribute attribute = type.GetCustomAttribute(attributeType);
                    if (attribute == null)
                    {
                        return false;
                    }

                    long timestamp = Stopwatch.GetTimestamp();
                    handleMethod.Invoke(register, new object[] { attribute, type });
                    AppendSlowOperation(performanceReport, timestamp, "Class attribute registration", register.Order, registerType, type);
                    return true;
                });
            }

            private static void AddFieldAttributeHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType, StringBuilder performanceReport)
            {
                Type attributeType = registerBaseType.GetGenericArguments()[0];
                MethodInfo handleMethod = GetHandleMethod(registerType);
                AddMemberCandidates(FieldCandidates, register.Order, GetMembersWithAttribute<FieldInfo>(GetFieldsWithAttributeMethod, attributeType));

                AddHandler(FieldAttributeHandlers, register.Order, field =>
                {
                    Attribute attribute = field.GetCustomAttribute(attributeType);
                    if (attribute == null)
                    {
                        return false;
                    }

                    long timestamp = Stopwatch.GetTimestamp();
                    handleMethod.Invoke(register, new object[] { attribute, field });
                    AppendSlowOperation(performanceReport, timestamp, "Field attribute registration", register.Order, registerType, field.DeclaringType);
                    return true;
                });
            }

            private static void AddPropertyAttributeHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType, StringBuilder performanceReport)
            {
                Type attributeType = registerBaseType.GetGenericArguments()[0];
                MethodInfo handleMethod = GetHandleMethod(registerType);

                AddHandler(PropertyAttributeHandlers, register.Order, property =>
                {
                    Attribute attribute = property.GetCustomAttribute(attributeType);
                    if (attribute == null)
                    {
                        return false;
                    }

                    long timestamp = Stopwatch.GetTimestamp();
                    handleMethod.Invoke(register, new object[] { attribute, property });
                    AppendSlowOperation(performanceReport, timestamp, "Property attribute registration", register.Order, registerType, property.DeclaringType);
                    return true;
                });
            }

            private static void AddMethodAttributeHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType, StringBuilder performanceReport)
            {
                Type attributeType = registerBaseType.GetGenericArguments()[0];
                MethodInfo handleMethod = GetHandleMethod(registerType);
                AddMemberCandidates(MethodCandidates, register.Order, GetMembersWithAttribute<MethodInfo>(GetMethodsWithAttributeMethod, attributeType));

                AddHandler(MethodAttributeHandlers, register.Order, method =>
                {
                    Attribute attribute = method.GetCustomAttribute(attributeType);
                    if (attribute == null)
                    {
                        return false;
                    }

                    long timestamp = Stopwatch.GetTimestamp();
                    handleMethod.Invoke(register, new object[] { attribute, method });
                    AppendSlowOperation(performanceReport, timestamp, "Method attribute registration", register.Order, registerType, method.DeclaringType);
                    return true;
                });
            }

            private static MethodInfo GetHandleMethod(Type registerType)
            {
                return registerType.GetMethod("Handle", MemberFlags);
            }

            private static void InvokeTypeHandlers(Type type, List<Func<Type, bool>> handlers)
            {
                if (handlers == null)
                {
                    return;
                }

                for (int i = 0; i < handlers.Count; i++)
                {
                    handlers[i](type);
                }
            }

            private static void InvokeMemberHandlers<TMember>(TMember member, List<Func<TMember, bool>> handlers)
            {
                if (handlers == null)
                {
                    return;
                }

                for (int i = 0; i < handlers.Count; i++)
                {
                    if (handlers[i](member))
                    {
                        break;
                    }
                }
            }

            private static MethodInfo FindTypeCacheMethod(string name)
            {
                return typeof(TypeCache).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(method => method.Name == name && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);
            }

            private static Type[] GetTypesDerivedFrom(Type baseType)
            {
                Type[] types = GetTypeCacheItems<Type>(GetTypesDerivedFromMethod, baseType);
                if (types == null)
                {
                    return null;
                }

                HashSet<Type> result = new();
                if (IsValidAssembly(baseType.Assembly))
                {
                    result.Add(baseType);
                }

                for (int i = 0; i < types.Length; i++)
                {
                    if (types[i] != null && IsValidAssembly(types[i].Assembly))
                    {
                        result.Add(types[i]);
                    }
                }

                return result.ToArray();
            }

            private static Type[] GetTypesWithAttribute(Type attributeType)
            {
                Type[] directTypes = GetTypeCacheItems<Type>(GetTypesWithAttributeMethod, attributeType);
                if (directTypes == null)
                {
                    return null;
                }

                HashSet<Type> result = new();
                bool includeDerivedTypes = IsClassAttributeInherited(attributeType);
                for (int i = 0; i < directTypes.Length; i++)
                {
                    Type type = directTypes[i];
                    if (type == null)
                    {
                        continue;
                    }

                    if (IsValidAssembly(type.Assembly))
                    {
                        result.Add(type);
                    }

                    if (!includeDerivedTypes)
                    {
                        continue;
                    }

                    Type[] derivedTypes = GetTypesDerivedFrom(type);
                    if (derivedTypes == null)
                    {
                        return null;
                    }

                    for (int d = 0; d < derivedTypes.Length; d++)
                    {
                        result.Add(derivedTypes[d]);
                    }
                }

                return result.ToArray();
            }

            private static bool IsClassAttributeInherited(Type attributeType)
            {
                AttributeUsageAttribute usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();
                return usage == null || (usage.Inherited && (usage.ValidOn & AttributeTargets.Class) != 0);
            }

            private static TMember[] GetMembersWithAttribute<TMember>(MethodInfo typeCacheMethod, Type attributeType) where TMember : MemberInfo
            {
                TMember[] members = GetTypeCacheItems<TMember>(typeCacheMethod, attributeType);
                if (members == null)
                {
                    return null;
                }

                return members.Where(member => member?.DeclaringType != null && IsValidAssembly(member.DeclaringType.Assembly)).ToArray();
            }

            private static T[] GetTypeCacheItems<T>(MethodInfo typeCacheMethod, Type genericArgument)
            {
                if (typeCacheMethod == null || genericArgument == null)
                {
                    return null;
                }

                try
                {
                    object result = typeCacheMethod.MakeGenericMethod(genericArgument).Invoke(null, null);
                    return result is IEnumerable<T> items ? items.ToArray() : null;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ESAssemblyStream] TypeCache query failed. Falling back to reflection: " + ex.Message);
                    return null;
                }
            }

            private static void AddTypeCandidates(int order, Type[] types)
            {
                if (!TypeCandidates.TryGetValue(order, out TypeCandidateLookup candidates))
                {
                    candidates = new TypeCandidateLookup();
                    TypeCandidates.Add(order, candidates);
                }

                if (types == null)
                {
                    candidates.RequiresFullScan = true;
                    return;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    if (types[i] != null)
                    {
                        candidates.Types.Add(types[i]);
                    }
                }
            }

            private static void AddMemberCandidates<TMember>(Dictionary<int, MemberCandidateLookup<TMember>> map, int order, TMember[] members) where TMember : MemberInfo
            {
                if (!map.TryGetValue(order, out MemberCandidateLookup<TMember> candidates))
                {
                    candidates = new MemberCandidateLookup<TMember>();
                    map.Add(order, candidates);
                }

                if (members == null)
                {
                    candidates.RequiresFullScan = true;
                    return;
                }

                for (int i = 0; i < members.Length; i++)
                {
                    TMember member = members[i];
                    Type declaringType = member?.DeclaringType;
                    if (declaringType == null)
                    {
                        continue;
                    }

                    if (!candidates.MembersByDeclaringType.TryGetValue(declaringType, out List<TMember> group))
                    {
                        group = new List<TMember>();
                        candidates.MembersByDeclaringType.Add(declaringType, group);
                    }

                    group.Add(member);
                }
            }

            private static void AddHandler<T>(Dictionary<int, List<T>> map, int order, T handler)
            {
                if (!map.TryGetValue(order, out List<T> handlers))
                {
                    handlers = new List<T>(4);
                    map.Add(order, handlers);
                }

                handlers.Add(handler);
            }

            private static List<int> CollectOrders()
            {
                HashSet<int> orders = new();
                AddOrders(orders, SingletonHandlers);
                AddOrders(orders, SubclassHandlers);
                AddOrders(orders, ClassAttributeHandlers);
                AddOrders(orders, FieldAttributeHandlers);
                AddOrders(orders, PropertyAttributeHandlers);
                AddOrders(orders, MethodAttributeHandlers);
                return orders.ToList();
            }

            private static void AddOrders<T>(HashSet<int> orders, Dictionary<int, List<T>> map)
            {
                foreach (int order in map.Keys)
                {
                    orders.Add(order);
                }
            }

            private static bool TryFindRegisterBase(Type type, out Type registerBaseType)
            {
                Type current = type;
                while (current != null && current != typeof(object))
                {
                    if (current.IsGenericType)
                    {
                        Type define = current.GetGenericTypeDefinition();
                        if (define == DefineClassAttribute ||
                            define == DefineFieldAttribute ||
                            define == DefinePropertyAttribute ||
                            define == DefineMethodAttribute ||
                            define == DefineSingleton ||
                            define == DefineAsSubclass)
                        {
                            registerBaseType = current;
                            return true;
                        }
                    }

                    current = current.BaseType;
                }

                registerBaseType = null;
                return false;
            }

            private static Type[] GetTypesSafely(Assembly assembly)
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(type => type != null).ToArray();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ESAssemblyStream] 获取程序集类型失败：" + assembly.GetName().Name + "，" + ex.Message);
                    return Array.Empty<Type>();
                }
            }

            private static bool IsValidAssembly(Assembly assembly)
            {
                string name = assembly.GetName().Name;
                return Array.IndexOf(EditorValidAssemblyNames, name) >= 0;
            }

            private static int GetAssemblyOrder(Assembly assembly)
            {
                string name = assembly.GetName().Name;
                int index = Array.IndexOf(EditorValidAssemblyNames, name);
                return index < 0 ? int.MaxValue : index;
            }
        }
#endif
    }
}
