using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                EditorAssemblyPart.Execute();
                Debug.Log("[ESAssemblyStream] Editor 程序集流完成，耗时 " + (DateTime.Now - startTime).TotalMilliseconds.ToString("F2") + " ms");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ESAssemblyStream] Editor 程序集流执行失败：\n" + ex);
            }
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

            public static void Execute()
            {
                ClearCache();
                CollectAssembliesAndRegisters();
                BuildHandlers();
                ApplyHandlers();
                ClearCache();
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

            private static void BuildHandlers()
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
                        AddClassAttributeHandler(register, registerType, registerBaseType);
                    }
                    else if (genericDefine == DefineFieldAttribute)
                    {
                        AddFieldAttributeHandler(register, registerType, registerBaseType);
                    }
                    else if (genericDefine == DefinePropertyAttribute)
                    {
                        AddPropertyAttributeHandler(register, registerType, registerBaseType);
                    }
                    else if (genericDefine == DefineMethodAttribute)
                    {
                        AddMethodAttributeHandler(register, registerType, registerBaseType);
                    }
                    else if (genericDefine == DefineSingleton)
                    {
                        AddSingletonHandler(register, registerType, registerBaseType);
                    }
                    else if (genericDefine == DefineAsSubclass)
                    {
                        AddSubclassHandler(register, registerType, registerBaseType);
                    }
                }
            }

            private static void ApplyHandlers()
            {
                List<int> orders = CollectOrders();
                orders.Sort();

                for (int i = 0; i < orders.Count; i++)
                {
                    int order = orders[i];
                    ApplyTypeHandlers(order);
                    ApplyMemberHandlers(order);
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
                            FieldInfo[] fields = type.GetFields(MemberFlags);
                            for (int f = 0; f < fields.Length; f++)
                            {
                                InvokeMemberHandlers(fields[f], fieldHandlers);
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
                            MethodInfo[] methods = type.GetMethods(MemberFlags);
                            for (int m = 0; m < methods.Length; m++)
                            {
                                InvokeMemberHandlers(methods[m], methodHandlers);
                            }
                        }
                    }
                }
            }

            private static void AddSingletonHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType)
            {
                Type targetType = registerBaseType.GetGenericArguments()[0];
                MethodInfo handleMethod = GetHandleMethod(registerType);

                AddHandler(SingletonHandlers, register.Order, type =>
                {
                    if (type.IsAbstract || !targetType.IsAssignableFrom(type))
                    {
                        return false;
                    }

                    object instance = Activator.CreateInstance(type);
                    handleMethod.Invoke(register, new[] { instance });
                    return true;
                });
            }

            private static void AddSubclassHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType)
            {
                Type targetType = registerBaseType.GetGenericArguments()[0];
                MethodInfo handleMethod = GetHandleMethod(registerType);

                AddHandler(SubclassHandlers, register.Order, type =>
                {
                    if (type.IsAbstract || type == targetType || !targetType.IsAssignableFrom(type))
                    {
                        return false;
                    }

                    handleMethod.Invoke(register, new object[] { type });
                    return true;
                });
            }

            private static void AddClassAttributeHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType)
            {
                Type attributeType = registerBaseType.GetGenericArguments()[0];
                MethodInfo handleMethod = GetHandleMethod(registerType);

                AddHandler(ClassAttributeHandlers, register.Order, type =>
                {
                    Attribute attribute = type.GetCustomAttribute(attributeType);
                    if (attribute == null)
                    {
                        return false;
                    }

                    handleMethod.Invoke(register, new object[] { attribute, type });
                    return true;
                });
            }

            private static void AddFieldAttributeHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType)
            {
                Type attributeType = registerBaseType.GetGenericArguments()[0];
                MethodInfo handleMethod = GetHandleMethod(registerType);

                AddHandler(FieldAttributeHandlers, register.Order, field =>
                {
                    Attribute attribute = field.GetCustomAttribute(attributeType);
                    if (attribute == null)
                    {
                        return false;
                    }

                    handleMethod.Invoke(register, new object[] { attribute, field });
                    return true;
                });
            }

            private static void AddPropertyAttributeHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType)
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

                    handleMethod.Invoke(register, new object[] { attribute, property });
                    return true;
                });
            }

            private static void AddMethodAttributeHandler(ESAS_EditorRegister_AB register, Type registerType, Type registerBaseType)
            {
                Type attributeType = registerBaseType.GetGenericArguments()[0];
                MethodInfo handleMethod = GetHandleMethod(registerType);

                AddHandler(MethodAttributeHandlers, register.Order, method =>
                {
                    Attribute attribute = method.GetCustomAttribute(attributeType);
                    if (attribute == null)
                    {
                        return false;
                    }

                    handleMethod.Invoke(register, new object[] { attribute, method });
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
