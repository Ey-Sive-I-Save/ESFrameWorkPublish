

using Sirenix.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;




/* 
 *   Editor的 和 RunTime 使用一套类似的标准，但是生命周期完全不同！
 *   
 * 第一类 标志特性 不需要继承性质，为特性和目标带目标执行一次即可
 * 第二类 一次继承类 ，父类定义好规范后，每个类型生成一个实例 即可4
 * 第三类 对象即时添加,
 
 
 */
namespace ES
{
    public enum ESAssemblyLoadTiming : short
    {
        /// <summary>
        /// 阶段0 ： 初始化程序集后
        /// </summary>
        [InspectorName("-100：初始化程序集后")]
        _0_AfterInitAssemliesLoaded = -100,
        /// <summary>
        /// 阶段1 ： 场景开始加载前
        /// </summary>
        [InspectorName("0：场景开始加载前")]
        _1_BeforeFirstSceneLoad = 0,
        /// <summary>
        /// 阶段2 ： 场景加载完成后(已经全部Awake)
        /// </summary>
        [InspectorName("50：场景加载完成后")]
        _2_AfterFirstSceneLoad = 50,
    }
    public class ESAssemblyStream
    {
        private static List<string> EditorValidAssebliesName = new List<string>() {
    "ES_Design","ES_Stand","Assembly-CSharp-Editor","Assembly-CSharp-Editor-firstpass","ES_Logic","Assembly-CSharp","Assembly-CSharp-firstpass","NewAssem"
    };
        private static List<string> RuntimeValidAssebliesName = new List<string>() {
  "ES_Design","ES_Stand","Assembly-CSharp-Editor","Assembly-CSharp-Editor-firstpass","ES_Logic","Assembly-CSharp","Assembly-CSharp-firstpass","NewAssem"
   
    };
#if UNITY_EDITOR
        //编辑器部分
        private class EditorPart
        {
            #region 编辑器部分

            private static Assembly[] EditorAssembies;
            private static List<Assembly> ValidEditorAssembiles = new List<Assembly>(25);
            private static Dictionary<Assembly, Type[]> EditorTypes = new Dictionary<Assembly, Type[]>(25);

            private static List<ESAS_EditorRegister_AB> EditorRegisters = new List<ESAS_EditorRegister_AB>(25);

            #region 支持的Define模板
            private static readonly Type Define_ClassAttribute = typeof(EditorRegister_FOR_ClassAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_FieldAttribute = typeof(EditorRegister_FOR_FieldAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_MethodAttribute = typeof(EditorRegister_FOR_MethodAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_Singleton = typeof(EditorRegister_FOR_Singleton<>).GetGenericTypeDefinition();
            private static readonly Type Define_AsSubClass = typeof(EditorRegister_FOR_AsSubclass<>).GetGenericTypeDefinition();

            #endregion

            #region 处理流
            private static KeyGroup<int, Func<Type, bool>> Handler_Singleton = new KeyGroup<int, Func<Type, bool>>();
            private static KeyGroup<int, Func<Type, bool>> Handler_SubClass = new();

            private static KeyGroup<int, Func<MethodInfo, bool>> Handler_MethodAttribute = new();
            private static KeyGroup<int, Func<FieldInfo, bool>> Handler_FieldAttribute = new();
            private static KeyGroup<int, Func<Type, bool>> Handler_ClassAttribute = new();
            #endregion


            private static bool TEST_Enable = true;
            private static int SimulateForeachTimes = 1;

            //编辑器下的程序集流
            [InitializeOnLoadMethod]
            private static void EditorInitLoad()
            {

                //对于Editor来说，使用Loaded委托是无意义的
                EditorAssembies = AppDomain.CurrentDomain.GetAssemblies();
                //清空 程序集->类型[] 字典
                EditorTypes.Clear();
                //清空 可用的注册器类型
                EditorRegisters.Clear();
                ValidEditorAssembiles.Clear();
                Handler_Singleton.Clear();
                Handler_SubClass.Clear();
                Handler_MethodAttribute.Clear();
                Handler_FieldAttribute.Clear();
                Handler_ClassAttribute.Clear();
                Editor_InitAssembiesAndRegisters();//初始化注册器

                DateTime startEditorStreamTime = DateTime.Now;

                if (TEST_Enable)
                {
                    Editor_ApplyRegisters();//应用注册器

                    Editor_LoadRegisteredTypes();//加载被注册的类效果
                }
                else
                {
                    for (int i = 0; i < SimulateForeachTimes; i++)
                    {
                        Editor_TestForeach();//模拟遍历
                    }
                }
                Debug.Log("耗时" + (DateTime.Now - startEditorStreamTime));
            }

            private static void Editor_InitAssembiesAndRegisters()
            {

                var listRE = new List<ESAS_EditorRegister_AB>();
                for (int IndexASM = 0; IndexASM < EditorAssembies.Length; IndexASM++)
                {
                    var asm = EditorAssembies[IndexASM];
                    if (Internal_IsValidAssembly(asm))
                    {
                        //可用的程序集 
                        Type[] types;
                        EditorTypes.Add(asm, types = asm.GetTypes());
                        ValidEditorAssembiles.Add(asm);
                        for (int IndexType = 0; IndexType < types.Length; IndexType++)
                        {
                            Type t = types[IndexType];
                            if (!t.IsAbstract && t.IsSubclassOf(typeof(ESAS_EditorRegister_AB)))
                            {

                                var s = Activator.CreateInstance(t) as ESAS_EditorRegister_AB;
                                if (s != null)
                                {
                                    listRE.Add(s);
                                }

                            }
                        }

                    }
                }
                ValidEditorAssembiles.OrderBy((Assembly asm) => Internal_GetIndexForAssembly(asm));
                //SORT
                EditorRegisters = listRE.OrderBy((f) => { return f.Order; }).ToList();
            }

            private static void Editor_ApplyRegisters()
            {
                for (int i = 0; i < EditorRegisters.Count; i++)
                {
                    var register = EditorRegisters[i];
                    var re = register.GetType();

                    //这里为了性能，选择直接展开写
                    int maxLevel = 3;
                    var nowType = re;
                    while (maxLevel > 0 && nowType != typeof(object))
                    {
                        if (nowType.IsGenericType)
                        {
                            // 3. 获取泛型定义，并与目标定义比较
                            Type genericDefinition = nowType.GetGenericTypeDefinition();
                            if (genericDefinition == Define_ClassAttribute)
                            {
                                Match_ClassAttribute(re, register, nowType);
                                break;
                            }
                            if (genericDefinition == Define_FieldAttribute)
                            {
                                Match_FieldAttribute(re, register, nowType);
                                break;
                            }
                            if (genericDefinition == Define_MethodAttribute)
                            {
                                Match_MethodAttribute(re, register, nowType);
                                break;
                            }
                            if (genericDefinition == Define_Singleton)
                            {
                                Match_Singleton(re, register, nowType);
                                break;
                            }
                            if (genericDefinition == Define_AsSubClass)
                            {
                                Match_SubClass(re, register, nowType);
                                break;
                            }
                        }
                        maxLevel--;
                        nowType = nowType.BaseType; // 继续向上查找
                    }

                }
            }

            private static void Editor_LoadRegisteredTypes()
            {
                for (int orderIndex = -100; orderIndex <= 100; orderIndex++)
                {
                    var Handler_SingletonPart = Handler_Singleton.GetGroupDirectly(orderIndex);
                    var Handler_SubClassPart = Handler_SubClass.GetGroupDirectly(orderIndex);
                    var Handler_ClassAttributePart = Handler_ClassAttribute.GetGroupDirectly(orderIndex);
                    var Handler_FieldAttributePart = Handler_FieldAttribute.GetGroupDirectly(orderIndex);
                    var Handler_MethodAttributePart = Handler_MethodAttribute.GetGroupDirectly(orderIndex);
                    if (Handler_SingletonPart.Count == 0 && Handler_SubClassPart.Count == 0 && Handler_ClassAttributePart.Count == 0 && Handler_FieldAttributePart.Count == 0 && Handler_MethodAttributePart.Count == 0)
                    {
                        continue;
                    }
                    //有效程序集
                    for (int indexASM = 0; indexASM < ValidEditorAssembiles.Count; indexASM++)
                    {
                        var asm = ValidEditorAssembiles[indexASM];
                        if (EditorTypes.TryGetValue(asm, out var types))
                        {
                            int lenForFunc_Singleton = Handler_SingletonPart.Count;

                            for (int indexAction = 0; indexAction < lenForFunc_Singleton; indexAction++)
                            {
                                //可用的单例匹配
                                //因为需要排序！！！
                                var func = Handler_SingletonPart[indexAction];
                                int lenForTypes = types.Length;
                                for (int indexType = 0; indexType < lenForTypes; indexType++)
                                {
                                    Type nowType = types[indexType];

                                    func.Invoke(nowType);
                                }
                            }

                            int lenForFunc_SubClass = Handler_SubClassPart.Count;

                            for (int indexAction = 0; indexAction < lenForFunc_SubClass; indexAction++)
                            {
                                //可用的单例匹配
                                //因为需要排序！！！
                                var func = Handler_SubClassPart[indexAction];
                                int lenForTypes = types.Length;
                                for (int indexType = 0; indexType < lenForTypes; indexType++)
                                {
                                    Type nowType = types[indexType];

                                    func.Invoke(nowType);
                                }
                            }


                            int lenForTypesOut = types.Length;
                            //一组类型
                            for (int indexType = 0; indexType < lenForTypesOut; indexType++)
                            {
                                Type nowType = types[indexType];

                                #region 类特性
                                int lenForFunc_ClassAttribute = Handler_ClassAttributePart.Count;
                                for (int indexAction = 0; indexAction < lenForFunc_ClassAttribute; indexAction++)
                                {
                                    //可用的单例匹配
                                    var func = Handler_ClassAttributePart[indexAction];
                                    if (func.Invoke(nowType))
                                    {
                                        break;
                                    }
                                }
                                #endregion
                                {
                                    #region 字段特性
                                    var Fields = nowType.GetFields();
                                    for (int indexField = 0; indexField < Fields.Length; indexField++)
                                    {
                                        var infoF = Fields[indexField];
                                        int lenForFunc_FieldAttribute = Handler_FieldAttributePart.Count;
                                        for (int indexAction = 0; indexAction < lenForFunc_FieldAttribute; indexAction++)
                                        {
                                            //可用的单例匹配
                                            var func = Handler_FieldAttributePart[indexAction];
                                            if (func.Invoke(infoF))
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                                    #endregion

                                {
                                    #region 字段特性
                                    var Methods = nowType.GetMethods();
                                    for (int indexField = 0; indexField < Methods.Length; indexField++)
                                    {
                                        var infoM = Methods[indexField];
                                        int lenForFunc_FieldAttribute = Handler_MethodAttributePart.Count;
                                        for (int indexAction = 0; indexAction < lenForFunc_FieldAttribute; indexAction++)
                                        {
                                            //可用的单例匹配
                                            var func = Handler_MethodAttributePart[indexAction];
                                            if (func?.Invoke(infoM) ?? false)
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                                    #endregion
                            }
                        }
                    }
                }
            }
            private static void Editor_TestForeach()
            {
                //有效程序集
                for (int indexASM = 0; indexASM < ValidEditorAssembiles.Count; indexASM++)
                {
                    var asm = ValidEditorAssembiles[indexASM];
                    if (EditorTypes.TryGetValue(asm, out var types))
                    {
                        int lenForTypes = types.Length;
                        //一组类型
                        for (int indexType = 0; indexType < lenForTypes; indexType++)
                        {
                            Type nowType = types[indexType];
                            {
                                if (!nowType.IsAbstract && nowType.IsSubclassOf(typeof(UnityEngine.Object)))
                                {

                                }
                                nowType.GetAttribute<InspectorNameAttribute>();
                                #region 字段特性
                                var Fields = nowType.GetFields();
                                for (int indexField = 0; indexField < Fields.Length; indexField++)
                                {
                                    var infoF = Fields[indexField];
                                    infoF.GetAttribute<InspectorNameAttribute>();
                                }
                            }
                            #endregion

                            {
                                #region 字段特性
                                var Methods = nowType.GetMethods();
                                for (int indexField = 0; indexField < Methods.Length; indexField++)
                                {
                                    var infoM = Methods[indexField];
                                    infoM.GetAttribute<InspectorNameAttribute>();
                                }
                            }
                            #endregion
                        }
                    }
                }
            }
            private static void Match_ClassAttribute(Type reType, ESAS_EditorRegister_AB register, Type geneType)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type supportAttribute = types[0];

                    try
                    {
                        MethodInfo info = reType.GetMethod("Handle");
                        Handler_ClassAttribute.Add(register.Order, (classType) =>
                        {

                            var at = classType.GetCustomAttribute(supportAttribute);
                            if (at != null)
                            {
                                info.Invoke(register, new object[] { at, classType });
                                return true;//拦截
                            }
                            return false;
                        });
                    }
                    catch (Exception ex) // 捕获其他未预料到的异常
                    {
                        Console.WriteLine($"EditorRegister捕获字段特性失败: 原始注册器类型{reType},泛型类型{geneType}, 支持特性{supportAttribute},信息{ex.Message}");
                    }
                }
            }
            private static void Match_FieldAttribute(Type reType, ESAS_EditorRegister_AB register, Type geneType)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type supportAttribute = types[0];

                    try
                    {
                        MethodInfo info = reType.GetMethod("Handle");
                        Handler_FieldAttribute.Add(register.Order, (fieldINFO) =>
                        {
                            var at = fieldINFO.GetCustomAttribute(supportAttribute);
                            if (at != null)
                            {
                                info.Invoke(register, new object[] { at, fieldINFO });
                                return true;//拦截
                            }
                            return false;
                        });
                    }
                    catch (Exception ex) // 捕获其他未预料到的异常
                    {
                        Console.WriteLine($"EditorRegister捕获字段特性失败: 原始注册器类型{reType},泛型类型{geneType}, 支持特性{supportAttribute},信息{ex.Message}");
                    }
                }
            }
            private static void Match_MethodAttribute(Type reType, ESAS_EditorRegister_AB register, Type geneType)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type supportAttribute = types[0];

                    try
                    {
                        MethodInfo info = reType.GetMethod("Handle");
                        Handler_MethodAttribute.Add(register.Order, ((methodINFO) =>
                        {
                            var at = methodINFO.GetCustomAttribute(supportAttribute);
                            if (at != null)
                            {
                                info.Invoke(register, new object[] { at, methodINFO });
                                return true;//拦截
                            }
                            return false;
                        }));
                    }
                    catch (Exception ex) // 捕获其他未预料到的异常
                    {
                        Console.WriteLine($"EditorRegister捕获方法特性失败: 原始注册器类型{reType},泛型类型{geneType}, 支持特性{supportAttribute},信息{ex.Message}");
                    }
                }
            }
            private static void Match_Singleton(Type reType, ESAS_EditorRegister_AB register, Type geneType)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type support = types[0];

                    try
                    {
                        MethodInfo info = reType.GetMethod("Handle");
                        Handler_Singleton.Add(register.Order, (type) =>
                        {

                            if (!type.IsAbstract && type.IsSubclassOf(support))
                            {
                                var singleton = Activator.CreateInstance(type);
                                info.Invoke(register, new object[] { singleton });
                                return true;//拦截
                            }
                            return false;
                        });
                        // 如果创建成功，在这里使用 instance
                        // Console.WriteLine($"实例创建成功: {instance.GetType()}");
                    }
                    catch (ArgumentNullException ex)
                    {
                        Console.WriteLine($"类型参数为 null: {ex.Message}");
                    }
                    catch (ArgumentException ex)
                    {
                        Console.WriteLine($"类型参数无效（例如是开放泛型）: {ex.Message}");
                    }
                    catch (NotSupportedException ex)
                    {
                        Console.WriteLine($"不支持创建该类型的实例: {ex.Message}");
                    }
                    catch (TargetInvocationException ex)
                    {
                        // 这是一个包装异常，真正的错误在 InnerException 中
                        Console.WriteLine($"构造函数抛出异常: {ex.InnerException?.Message}");
                    }
                    catch (MissingMethodException ex)
                    {
                        Console.WriteLine($"未找到匹配的构造函数（例如没有无参构造函数）: {ex.Message}");
                    }
                    catch (MemberAccessException ex)
                    {
                        Console.WriteLine($"访问构造函数被拒绝（权限问题或抽象类）: {ex.Message}");
                    }
                    catch (TypeLoadException ex)
                    {
                        Console.WriteLine($"类型加载失败: {ex.Message}");
                    }
                    catch (Exception ex) // 捕获其他未预料到的异常
                    {
                        Console.WriteLine($"EditorRegister捕获单例继承失败: 原始注册器类型{reType},泛型类型{geneType}, 支持单例{support},信息{ex.Message}");
                    }
                }
            }

            private static void Match_SubClass(Type reType, ESAS_EditorRegister_AB register, Type geneType)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type support = types[0];

                    try
                    {
                        MethodInfo info = reType.GetMethod("Handle");
                        Handler_SubClass.Add(register.Order, (type) =>
                        {

                            if (!type.IsAbstract && type.IsSubclassOf(support))
                            {
                                info.Invoke(register, new object[] { type });
                                return true;//拦截
                            }
                            return false;
                        });
                        // 如果创建成功，在这里使用 instance
                        // Console.WriteLine($"实例创建成功: {instance.GetType()}");
                    }
                    catch (ArgumentNullException ex)
                    {
                        Console.WriteLine($"类型参数为 null: {ex.Message}");
                    }
                    catch (ArgumentException ex)
                    {
                        Console.WriteLine($"类型参数无效（例如是开放泛型）: {ex.Message}");
                    }
                    catch (NotSupportedException ex)
                    {
                        Console.WriteLine($"不支持创建该类型的实例: {ex.Message}");
                    }
                    catch (TargetInvocationException ex)
                    {
                        // 这是一个包装异常，真正的错误在 InnerException 中
                        Console.WriteLine($"构造函数抛出异常: {ex.InnerException?.Message}");
                    }
                    catch (MissingMethodException ex)
                    {
                        Console.WriteLine($"未找到匹配的构造函数（例如没有无参构造函数）: {ex.Message}");
                    }
                    catch (MemberAccessException ex)
                    {
                        Console.WriteLine($"访问构造函数被拒绝（权限问题或抽象类）: {ex.Message}");
                    }
                    catch (TypeLoadException ex)
                    {
                        Console.WriteLine($"类型加载失败: {ex.Message}");
                    }
                    catch (Exception ex) // 捕获其他未预料到的异常
                    {
                        Console.WriteLine($"EditorRegister捕获单例继承失败: 原始注册器类型{reType},泛型类型{geneType}, 支持单例{support},信息{ex.Message}");
                    }
                }
            }

            private static bool Internal_IsValidAssembly(Assembly asm)
            {
                var name_ = asm.GetName().Name;
                if (EditorValidAssebliesName.Contains(name_)) { return true; }
                //很多程序集没必要考虑的！！这里写一个方法到时候
                return false;
            }
            private static int Internal_GetIndexForAssembly(Assembly asm)
            {
                var name_ = asm.GetName().Name;
                if (EditorValidAssebliesName.Contains(name_)) { return EditorValidAssebliesName.IndexOf(name_); }
                //很多程序集没必要考虑的！！这里写一个方法到时候
                return -1;
            }

            private static bool Internal_MatchDefineOne(Type instanceType, Type defineType, out Type matchType, int maxLevel = 3)
            {
                var currentBaseType = instanceType;
                while (maxLevel > 0 && currentBaseType != null && currentBaseType != typeof(object))
                {
                    // 2. 检查当前基类是否是泛型类型
                    if (currentBaseType.IsGenericType)
                    {
                        // 3. 获取泛型定义，并与目标定义比较
                        Type genericDefinition = currentBaseType.GetGenericTypeDefinition();
                        if (genericDefinition == defineType)
                        {
                            matchType = currentBaseType; // 找到了封闭的泛型基类
                            return true;
                        }
                    }
                    maxLevel--;
                    currentBaseType = currentBaseType.BaseType; // 继续向上查找
                }
                matchType = null;
                return false;
            }


            #endregion


        }
#endif
        public class RunTimePart
        {
            private static Assembly[] InitRuntimeAssembies;
            private static List<Assembly> ValidRuntimeAssembiles = new List<Assembly>(25);
            private static Dictionary<Assembly, Type[]> RuntimeTypes = new Dictionary<Assembly, Type[]>(25);

            private static ES.KeyGroup<int, ESAS_RuntimeRegister_AB> InitRuntimeRegisters = new KeyGroup<int, ESAS_RuntimeRegister_AB>();
            private static Queue<ESAS_RuntimeRegister_AB> HotRuntimeRegisters = new Queue<ESAS_RuntimeRegister_AB>(20);
            private static Queue<Assembly> WaitHotLoadingAssembies = new Queue<Assembly>(4);
            #region 支持的Define模板
            private static readonly Type Define_ClassAttribute = typeof(RuntimeRegister_FOR_ClassAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_FieldAttribute = typeof(RuntimeRegister_FOR_FieldAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_MethodAttribute = typeof(RuntimeRegister_FOR_MethodAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_Singleton = typeof(RuntimeRegister_FOR_Singleton<>).GetGenericTypeDefinition();

            private static readonly Type Define_AsSubClass = typeof(RuntimeRegister_FOR_AsSubclass<>).GetGenericTypeDefinition();


            #endregion

            #region 处理流
            private static KeyGroup<int, Func<Type, bool>> InitHandler_Singleton = new KeyGroup<int, Func<Type, bool>>();
            private static KeyGroup<int, Func<Type, bool>> InitHandler_AsSubclass = new KeyGroup<int, Func<Type, bool>>();
            private static KeyGroup<int, Func<MethodInfo, bool>> InitHandler_MethodAttribute = new KeyGroup<int, Func<MethodInfo, bool>>();
            private static KeyGroup<int, Func<FieldInfo, bool>> InitHandler_FieldAttribute = new KeyGroup<int, Func<FieldInfo, bool>>();
            private static KeyGroup<int, Func<Type, bool>> InitHandler_ClassAttribute = new KeyGroup<int, Func<Type, bool>>();


            private static KeyGroup<int, Func<Type, bool>> HotHandler_Singleton = new KeyGroup<int, Func<Type, bool>>();
            private static KeyGroup<int, Func<Type, bool>> HotHandler_AsSubclass = new KeyGroup<int, Func<Type, bool>>();
            private static KeyGroup<int, Func<MethodInfo, bool>> HotHandler_MethodAttribute = new KeyGroup<int, Func<MethodInfo, bool>>();
            private static KeyGroup<int, Func<FieldInfo, bool>> HotHandler_FieldAttribute = new KeyGroup<int, Func<FieldInfo, bool>>();
            private static KeyGroup<int, Func<Type, bool>> HotHandler_ClassAttribute = new KeyGroup<int, Func<Type, bool>>();
            #endregion

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
            private static void RuntimeInitLoad_000_AfterAssembliesLoaded()
            {
                //可以进行默认的加载捏
                InitRuntimeAssembies = AppDomain.CurrentDomain.GetAssemblies();

                RuntimeTypes.Clear();
                //清空 可用的注册器类型
                InitRuntimeRegisters.Clear();

                InitHandler_Singleton.Clear();
                InitHandler_AsSubclass.Clear();
                InitHandler_MethodAttribute.Clear();
                InitHandler_FieldAttribute.Clear();
                InitHandler_ClassAttribute.Clear();


                InitRuntime_InitAssembiesAndAllRegisters();//初始化注册器--

                Debug.Log("完成全部初始化" + InitRuntimeRegisters.ToStringAllContent());

                DateTime startEditorStreamTime = DateTime.Now;
                //专属功能
                InitRuntime_ApplyThisTimingRegisters(ESAssemblyLoadTiming._0_AfterInitAssemliesLoaded.GetHashCode());//应用注册器

                InitRuntime_LoadRegisteredTypes(ESAssemblyLoadTiming._0_AfterInitAssemliesLoaded.GetHashCode());

                Debug.Log("第零阶段耗时" + (DateTime.Now - startEditorStreamTime));

                //插入 -100 到 0
                for (int i = ESAssemblyLoadTiming._0_AfterInitAssemliesLoaded.GetHashCode() + 1; i < ESAssemblyLoadTiming._1_BeforeFirstSceneLoad.GetHashCode(); i++)
                {
                    InitRuntime_ApplyThisTimingRegisters(i);//应用注册器

                    InitRuntime_LoadRegisteredTypes(i);
                }


                //等待注册
                AppDomain.CurrentDomain.AssemblyLoad += HotRuntimeLoadNewAssembly;
            }
            private static void HotRuntimeLoadNewAssembly(object sender, AssemblyLoadEventArgs args)
            {
                var asm = args.LoadedAssembly;
                WaitHotLoadingAssembies.Enqueue(asm);
                HotHandler_AsSubclass.Clear();
                HotHandler_Singleton.Clear();
                HotHandler_FieldAttribute.Clear();
                HotHandler_MethodAttribute.Clear();
                HotHandler_ClassAttribute.Clear();

                HotRuntime_ApplyThisAssemblyTimingRegisters(asm, ESAssemblyLoadTiming._0_AfterInitAssemliesLoaded.GetHashCode());
                OneTiming_HotLoadAllAssemblesRegistedTypes(ESAssemblyLoadTiming._0_AfterInitAssemliesLoaded.GetHashCode());
                  for (int i = ESAssemblyLoadTiming._0_AfterInitAssemliesLoaded.GetHashCode() + 1; i < ESAssemblyLoadTiming._1_BeforeFirstSceneLoad.GetHashCode(); i++)
                {
                  HotRuntime_ApplyThisAssemblyTimingRegisters(asm, i);
                  OneTiming_HotLoadAllAssemblesRegistedTypes(i);
                }


                HotRuntime_ApplyThisAssemblyTimingRegisters(asm, ESAssemblyLoadTiming._1_BeforeFirstSceneLoad.GetHashCode());
                  HotRuntime_ApplyThisAssemblyTimingRegisters(asm, ESAssemblyLoadTiming._1_BeforeFirstSceneLoad.GetHashCode());
                    for (int i = ESAssemblyLoadTiming._1_BeforeFirstSceneLoad.GetHashCode() + 1; i < ESAssemblyLoadTiming._2_AfterFirstSceneLoad.GetHashCode(); i++)
                {
                  HotRuntime_ApplyThisAssemblyTimingRegisters(asm, i);
                    OneTiming_HotLoadAllAssemblesRegistedTypes(i);
                }


                HotRuntime_ApplyThisAssemblyTimingRegisters(asm, ESAssemblyLoadTiming._2_AfterFirstSceneLoad.GetHashCode());
                  HotRuntime_ApplyThisAssemblyTimingRegisters(asm, ESAssemblyLoadTiming._2_AfterFirstSceneLoad.GetHashCode());
                    for (int i = ESAssemblyLoadTiming._2_AfterFirstSceneLoad.GetHashCode() + 1; i <=100; i++)
                {
                  HotRuntime_ApplyThisAssemblyTimingRegisters(asm, i);
                    OneTiming_HotLoadAllAssemblesRegistedTypes(i);
                }
                 WaitHotLoadingAssembies.Dequeue();
            }
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
            private static void RuntimeInitLoad_111_BeforeSceneLoad()
            {
                DateTime startEditorStreamTime = DateTime.Now;
                //专属功能
                InitRuntime_ApplyThisTimingRegisters(ESAssemblyLoadTiming._1_BeforeFirstSceneLoad.GetHashCode());//应用注册器

                InitRuntime_LoadRegisteredTypes(ESAssemblyLoadTiming._1_BeforeFirstSceneLoad.GetHashCode());

                Debug.Log("第一阶段耗时" + (DateTime.Now - startEditorStreamTime));
                //插入 1 到 50
                for (int i = ESAssemblyLoadTiming._1_BeforeFirstSceneLoad.GetHashCode() + 1; i < ESAssemblyLoadTiming._2_AfterFirstSceneLoad.GetHashCode(); i++)
                {
                    InitRuntime_ApplyThisTimingRegisters(i);//应用注册器

                    InitRuntime_LoadRegisteredTypes(i);
                }
            }

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            private static void RuntimeInitLoad_222_AfterSceneLoad()
            {
                DateTime startEditorStreamTime = DateTime.Now;
                //专属功能
                InitRuntime_ApplyThisTimingRegisters(ESAssemblyLoadTiming._2_AfterFirstSceneLoad.GetHashCode());//应用注册器

                InitRuntime_LoadRegisteredTypes(ESAssemblyLoadTiming._2_AfterFirstSceneLoad.GetHashCode());
                Debug.Log("第二阶段耗时" + (DateTime.Now - startEditorStreamTime));

                //插入 51 到 100
                for (int i = ESAssemblyLoadTiming._2_AfterFirstSceneLoad.GetHashCode() + 1; i <= 100; i++)
                {
                    InitRuntime_ApplyThisTimingRegisters(i);//应用注册器

                    InitRuntime_LoadRegisteredTypes(i);
                }
                Debug.Log("终止" + (DateTime.Now - startEditorStreamTime));
            }

            #region Init流程

            private static void InitRuntime_InitAssembiesAndAllRegisters()
            {
                for (int IndexASM = 0; IndexASM < InitRuntimeAssembies.Length; IndexASM++)
                {
                    var asm = InitRuntimeAssembies[IndexASM];
                    if (Internal_IsValidAssembly(asm))
                    {
                        //可用的程序集
                        Type[] types;
                        RuntimeTypes.Add(asm, types = asm.GetTypes());
                        ValidRuntimeAssembiles.Add(asm);
                        for (int IndexType = 0; IndexType < types.Length; IndexType++)
                        {
                            Type reType = types[IndexType];
                            if (reType.IsSubclassOf(typeof(ESAS_RuntimeRegister_AB)))
                            {
                                if (reType.IsAbstract) continue;
                                var register = Activator.CreateInstance(reType) as ESAS_RuntimeRegister_AB;
                                InitRuntimeRegisters.Add(register.LoadTiming.GetHashCode(), register);
                            }
                        }

                    }
                }
            }

            private static void InitRuntime_ApplyThisTimingRegisters(int timing)
            {
                var reList = InitRuntimeRegisters.GetGroupDirectly(timing.GetHashCode());
                if (reList.Count == 0) return;
                for (int i = 0; i < reList.Count; i++)
                {
                    var register = reList[i];
                    var reType = register.GetType();

                    int maxLevel = 3;
                    var nowType = reType;
                    while (maxLevel > 0 && nowType != null && nowType != typeof(object))
                    {
                        if (nowType.IsGenericType)
                        {
                            // 3. 获取泛型定义，并与目标定义比较
                            Type genericDefinition = nowType.GetGenericTypeDefinition();
                            if (genericDefinition == Define_ClassAttribute)
                            {
                                Match_ClassAttribute(timing.GetHashCode(), register, reType, nowType);
                                break;
                            }
                            if (genericDefinition == Define_FieldAttribute)
                            {
                                Match_FieldAttribute(timing.GetHashCode(), register, reType, nowType);
                                break;
                            }
                            if (genericDefinition == Define_MethodAttribute)
                            {
                                Match_MethodAttribute(timing.GetHashCode(), register, reType, nowType);
                                break;
                            }
                            if (genericDefinition == Define_Singleton)
                            {
                                Match_Singleton(timing.GetHashCode(), register, reType, nowType);
                                break;
                            }
                            if (genericDefinition == Define_AsSubClass)
                            {
                                Match_SubClass(timing.GetHashCode(), register, reType, nowType);
                                break;
                            }
                        }
                        maxLevel--;
                        nowType = nowType.BaseType; // 继续向上查找
                    }

                }
            }

            private static void InitRuntime_LoadRegisteredTypes(int timing)
            {
                var handles_singleton = InitHandler_Singleton.GetGroupDirectly(timing.GetHashCode());
                var handler_classAttribute = InitHandler_ClassAttribute.GetGroupDirectly(timing.GetHashCode());
                var handler_fieldAttribute = InitHandler_FieldAttribute.GetGroupDirectly(timing.GetHashCode());
                var handler_methodAttribute = InitHandler_MethodAttribute.GetGroupDirectly(timing.GetHashCode());
                var Handler_SubClassPart = InitHandler_AsSubclass.GetGroupDirectly(timing.GetHashCode());
                if (handles_singleton.Count == 0 && handler_classAttribute.Count == 0 && handler_fieldAttribute.Count == 0 && handler_methodAttribute.Count == 0)
                {
                    return;
                }
                //有效程序集
                for (int indexASM = 0; indexASM < ValidRuntimeAssembiles.Count; indexASM++)
                {
                    var asm = ValidRuntimeAssembiles[indexASM];
                    if (RuntimeTypes.TryGetValue(asm, out var types))
                    {
{
                        int lenForFunc_Singleton = handles_singleton.Count;
                        for (int indexAction = 0; indexAction < lenForFunc_Singleton; indexAction++)
                        {
                            //可用的单例匹配
                            //因为需要排序！！！
                            var func = handles_singleton[indexAction];
                            int lenForTypes2 = types.Length;
                            for (int indexType = 0; indexType < lenForTypes2; indexType++)
                            {
                                Type nowType = types[indexType];

                                func.Invoke(nowType);
                            }
                        }

                        int lenForFunc_SubClass = Handler_SubClassPart.Count;

                        for (int indexAction = 0; indexAction < lenForFunc_SubClass; indexAction++)
                        {
                            //可用的单例匹配
                            //因为需要排序！！！
                            var func = Handler_SubClassPart[indexAction];
                            int lenForTypes2 = types.Length;
                            for (int indexType = 0; indexType < lenForTypes2; indexType++)
                            {
                                Type nowType = types[indexType];

                                func.Invoke(nowType);
                            }
                        }
}

                        int lenForTypes = types.Length;
                        //一组类型
                        for (int indexType = 0; indexType < lenForTypes; indexType++)
                        {
                            Type nowType = types[indexType];

                            #region 类特性
                            if (handler_classAttribute != null)
                            {
                                int lenForFunc_ClassAttribute = handler_classAttribute.Count;
                                for (int indexAction = 0; indexAction < lenForFunc_ClassAttribute; indexAction++)
                                {
                                    //可用的单例匹配
                                    var func = handler_classAttribute[indexAction];
                                    if (func.Invoke(nowType))
                                    {
                                        break;
                                    }
                                }
                            }
                            #endregion
                            {
                                #region 字段特性
                                if (handler_fieldAttribute != null)
                                {
                                    var Fields = nowType.GetFields();
                                    for (int indexField = 0; indexField < Fields.Length; indexField++)
                                    {
                                        var infoF = Fields[indexField];
                                        int lenForFunc_FieldAttribute = handler_fieldAttribute.Count;
                                        for (int indexAction = 0; indexAction < lenForFunc_FieldAttribute; indexAction++)
                                        {
                                            //可用的单例匹配
                                            var func = handler_fieldAttribute[indexAction];
                                            if (func.Invoke(infoF))
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion

                            {
                                #region 字段特性
                                if (handler_methodAttribute != null)
                                {
                                    var Methods = nowType.GetMethods();
                                    for (int indexField = 0; indexField < Methods.Length; indexField++)
                                    {
                                        var infoM = Methods[indexField];
                                        int lenForFunc_FieldAttribute = handler_methodAttribute.Count;
                                        for (int indexAction = 0; indexAction < lenForFunc_FieldAttribute; indexAction++)
                                        {
                                            //可用的单例匹配
                                            var func = handler_methodAttribute[indexAction];
                                            if (func?.Invoke(infoM) ?? false)
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion
                        }
                    }
                }
            }

            #endregion

            #region Hot流程
            private static void HotRuntime_ApplyThisAssemblyTimingRegisters(Assembly assembly, int timing)
            {
                //先查询
                var asm = assembly;
                if (Internal_IsValidAssembly(asm))
                {
                    //可用的程序集
                    Type[] types;
                    RuntimeTypes.Add(asm, types = asm.GetTypes());
                    ValidRuntimeAssembiles.Add(asm);
                    for (int IndexType = 0; IndexType < types.Length; IndexType++)
                    {
                        Type reType = types[IndexType];
                        if (reType.IsSubclassOf(typeof(ESAS_RuntimeRegister_AB)))
                        {
                            if (reType.IsAbstract) continue;
                            var register = Activator.CreateInstance(reType) as ESAS_RuntimeRegister_AB;
                            HotApplyRegister(register, timing.GetHashCode(), false);
                        }
                    }

                }

                //再应用
            }

            private static void OneTiming_HotLoadAllAssemblesRegistedTypes(int timing)
            {
                var handles_singleton = HotHandler_Singleton.GetGroupDirectly(timing.GetHashCode());
                var handler_classAttribute = HotHandler_ClassAttribute.GetGroupDirectly(timing.GetHashCode());
                var handler_fieldAttribute = HotHandler_FieldAttribute.GetGroupDirectly(timing.GetHashCode());
                var handler_methodAttribute = HotHandler_MethodAttribute.GetGroupDirectly(timing.GetHashCode());
                var handler_SubClassPart = HotHandler_AsSubclass.GetGroupDirectly(timing.GetHashCode());
                while (WaitHotLoadingAssembies.Count > 0)
                {
                    var hotASM = WaitHotLoadingAssembies.Peek();
                    var asm = hotASM;
                    if (RuntimeTypes.TryGetValue(asm, out var types))
                    {
    int lenForFunc_Singleton = handles_singleton.Count;
                        for (int indexAction = 0; indexAction < lenForFunc_Singleton; indexAction++)
                        {
                            //可用的单例匹配
                            //因为需要排序！！！
                            var func = handles_singleton[indexAction];
                            int lenForTypes2 = types.Length;
                            for (int indexType = 0; indexType < lenForTypes2; indexType++)
                            {
                                Type nowType = types[indexType];

                                func.Invoke(nowType);
                            }
                        }

                        int lenForFunc_SubClass = handler_SubClassPart.Count;

                        for (int indexAction = 0; indexAction < lenForFunc_SubClass; indexAction++)
                        {
                            //可用的单例匹配
                            //因为需要排序！！！
                            var func = handler_SubClassPart[indexAction];
                            int lenForTypes2 = types.Length;
                            for (int indexType = 0; indexType < lenForTypes2; indexType++)
                            {
                                Type nowType = types[indexType];

                                func.Invoke(nowType);
                            }
                        }
                        
                        int lenForTypes = types.Length;
                        //一组类型
                        for (int indexType = 0; indexType < lenForTypes; indexType++)
                        {
                            Type nowType = types[indexType];
                            #region 类特性
                            if (handler_classAttribute != null)
                            {
                                int lenForFunc_ClassAttribute = handler_classAttribute.Count;
                                for (int indexAction = 0; indexAction < lenForFunc_ClassAttribute; indexAction++)
                                {
                                    //可用的单例匹配
                                    var func = handler_classAttribute[indexAction];
                                    if (func.Invoke(nowType))
                                    {
                                        break;
                                    }
                                }
                            }
                            #endregion
                            {
                                #region 字段特性
                                if (handler_fieldAttribute != null)
                                {
                                    var Fields = nowType.GetFields();
                                    for (int indexField = 0; indexField < Fields.Length; indexField++)
                                    {
                                        var infoF = Fields[indexField];
                                        int lenForFunc_FieldAttribute = handler_fieldAttribute.Count;
                                        for (int indexAction = 0; indexAction < lenForFunc_FieldAttribute; indexAction++)
                                        {
                                            //可用的单例匹配
                                            var func = handler_fieldAttribute[indexAction];
                                            if (func.Invoke(infoF))
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion

                            {
                                #region 字段特性
                                if (handler_methodAttribute != null)
                                {
                                    var Methods = nowType.GetMethods();
                                    for (int indexField = 0; indexField < Methods.Length; indexField++)
                                    {
                                        var infoM = Methods[indexField];
                                        int lenForFunc_FieldAttribute = handler_methodAttribute.Count;
                                        for (int indexAction = 0; indexAction < lenForFunc_FieldAttribute; indexAction++)
                                        {
                                            //可用的单例匹配
                                            var func = handler_methodAttribute[indexAction];
                                            if (func?.Invoke(infoM) ?? false)
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }

                                #endregion
                            }
                        }
                    }
                }
            }

            public static void HotLoadAllAssemblesRegistedTypes()
            {
               
            }


            #endregion

            #region 便捷方法
            private static void HotApplyRegister(ESAS_RuntimeRegister_AB register, int timing, bool init = false)
            {
                var reType = register.GetType();

                int maxLevel = 3;
                var nowType = reType;
                while (maxLevel > 0 && nowType != null && nowType != typeof(object))
                {
                    if (nowType.IsGenericType)
                    {
                        // 3. 获取泛型定义，并与目标定义比较
                        Type genericDefinition = nowType.GetGenericTypeDefinition();
                        if (genericDefinition == Define_ClassAttribute)
                        {
                            Match_ClassAttribute(timing.GetHashCode(), register, reType, nowType, init);
                            break;
                        }
                        if (genericDefinition == Define_FieldAttribute)
                        {
                            Match_FieldAttribute(timing.GetHashCode(), register, reType, nowType, init);
                            break;
                        }
                        if (genericDefinition == Define_MethodAttribute)
                        {
                            Match_MethodAttribute(timing.GetHashCode(), register, reType, nowType, init);
                            break;
                        }
                        if (genericDefinition == Define_Singleton)
                        {
                            Match_Singleton(timing.GetHashCode(), register, reType, nowType, init);
                            break;
                        }
                        if (genericDefinition == Define_AsSubClass)
                        {
                            Match_SubClass(timing.GetHashCode(), register, reType, nowType, init);
                            break;
                        }
                    }
                    maxLevel--;
                    nowType = nowType.BaseType; // 继续向上查找
                }
            }


            #endregion

            #region 测试仅
            private static void Runtime_TestForeach()
            {
                //有效程序集
                for (int indexASM = 0; indexASM < ValidRuntimeAssembiles.Count; indexASM++)
                {
                    var asm = ValidRuntimeAssembiles[indexASM];
                    if (RuntimeTypes.TryGetValue(asm, out var types))
                    {
                        int lenForTypes = types.Length;
                        //一组类型
                        for (int indexType = 0; indexType < lenForTypes; indexType++)
                        {
                            Type nowType = types[indexType];
                            {
                                if (!nowType.IsAbstract && nowType.IsSubclassOf(typeof(UnityEngine.Object)))
                                {

                                }
                                nowType.GetAttribute<InspectorNameAttribute>();
                                #region 字段特性
                                var Fields = nowType.GetFields();
                                for (int indexField = 0; indexField < Fields.Length; indexField++)
                                {
                                    var infoF = Fields[indexField];
                                    infoF.GetAttribute<InspectorNameAttribute>();
                                }
                            }
                                #endregion

                            {
                                #region 字段特性
                                var Methods = nowType.GetMethods();
                                for (int indexField = 0; indexField < Methods.Length; indexField++)
                                {
                                    var infoM = Methods[indexField];
                                    infoM.GetAttribute<InspectorNameAttribute>();
                                }
                            }
                                #endregion
                        }
                    }
                }
            }
            #endregion

            #region 匹配注册
            private static void Match_ClassAttribute(int timing, ESAS_RuntimeRegister_AB register, Type reType, Type geneType, bool init = true)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type supportAttribute = types[0];

                    try
                    {
                        MethodInfo info = reType.GetMethod("Handle");
                        Func<Type, bool> func = (Type classType) =>
                        {
                            var at = classType.GetCustomAttribute(supportAttribute);
                            if (at != null)
                            {
                                info.Invoke(register, new object[] { at, classType });
                                return true;//拦截
                            }
                            return false;
                        };
                        if (init) InitHandler_ClassAttribute.Add(timing.GetHashCode(), func);
                        else HotHandler_ClassAttribute.Add(timing.GetHashCode(), func);
                    }
                    catch (Exception ex) // 捕获其他未预料到的异常
                    {
                        Console.WriteLine($"RuntimeRegister捕获字段特性失败: 原始注册器类型{reType},泛型类型{geneType}, 支持特性{supportAttribute},信息{ex.Message}");
                    }
                }
            }
            private static void Match_FieldAttribute(int timing, ESAS_RuntimeRegister_AB register, Type reType, Type geneType, bool init = true)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type supportAttribute = types[0];

                    try
                    {
                        MethodInfo info = reType.GetMethod("Handle");
                        Func<FieldInfo, bool> func = (fieldINFO) =>
                        {
                            var at = fieldINFO.GetCustomAttribute(supportAttribute);
                            if (at != null)
                            {
                                info.Invoke(register, new object[] { at, fieldINFO });
                                return true;//拦截
                            }
                            return false;
                        };
                        if (init) InitHandler_FieldAttribute.Add(timing.GetHashCode(), func);
                        else HotHandler_FieldAttribute.Add(timing.GetHashCode(), func);
                    }
                    catch (Exception ex) // 捕获其他未预料到的异常
                    {
                        Console.WriteLine($"RuntimeRegister捕获字段特性失败: 原始注册器类型{reType},泛型类型{geneType}, 支持特性{supportAttribute},信息{ex.Message}");
                    }
                }
            }
            private static void Match_MethodAttribute(int timing, ESAS_RuntimeRegister_AB register, Type reType, Type geneType, bool init = true)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type supportAttribute = types[0];

                    try
                    {
                        MethodInfo info = reType.GetMethod("Handle");
                        Func<MethodInfo, bool> func = (methodINFO) =>
                        {
                            var at = methodINFO.GetCustomAttribute(supportAttribute);
                            if (at != null)
                            {
                                info.Invoke(register, new object[] { at, methodINFO });
                                return true;//拦截
                            }
                            return false;
                        };
                        if (init) InitHandler_MethodAttribute.Add(timing.GetHashCode(), func);
                        else HotHandler_MethodAttribute.Add(timing.GetHashCode(), func);
                    }
                    catch (Exception ex) // 捕获其他未预料到的异常
                    {
                        Console.WriteLine($"RuntimeRegister捕获方法特性失败: 原始注册器类型{reType},泛型类型{geneType}, 支持特性{supportAttribute},信息{ex.Message}");
                    }
                }
            }
            private static void Match_Singleton(int timing, ESAS_RuntimeRegister_AB register, Type reType, Type geneType, bool init = true)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type support = types[0];
                    try
                    {

                        MethodInfo info = reType.GetMethod("Handle");
                        Func<Type, bool> func = (type) =>
                        {
                            if (!type.IsAbstract && type.IsSubclassOf(support))
                            {
                                var singleton = Activator.CreateInstance(type);

                                info.Invoke(register, new object[] { singleton });
                                return true;//拦截
                            }
                            return false;
                        };
                        if (init) InitHandler_Singleton.Add(timing.GetHashCode(), func);
                        else HotHandler_Singleton.Add(timing.GetHashCode(), func);
                        // 如果创建成功，在这里使用 instance
                        // Console.WriteLine($"实例创建成功: {instance.GetType()}");
                    }
                    catch (ArgumentNullException ex)
                    {
                        Console.WriteLine($"类型参数为 null: {ex.Message}");
                    }
                    catch (ArgumentException ex)
                    {
                        Console.WriteLine($"类型参数无效（例如是开放泛型）: {ex.Message}");
                    }
                    catch (NotSupportedException ex)
                    {
                        Console.WriteLine($"不支持创建该类型的实例: {ex.Message}");
                    }
                    catch (TargetInvocationException ex)
                    {
                        // 这是一个包装异常，真正的错误在 InnerException 中
                        Console.WriteLine($"构造函数抛出异常: {ex.InnerException?.Message}");
                    }
                    catch (MissingMethodException ex)
                    {
                        Console.WriteLine($"未找到匹配的构造函数（例如没有无参构造函数）: {ex.Message}");
                    }
                    catch (MemberAccessException ex)
                    {
                        Console.WriteLine($"访问构造函数被拒绝（权限问题或抽象类）: {ex.Message}");
                    }
                    catch (TypeLoadException ex)
                    {
                        Console.WriteLine($"类型加载失败: {ex.Message}");
                    }
                    catch (Exception ex) // 捕获其他未预料到的异常
                    {
                        Console.WriteLine($"RuntimeRegister捕获单例继承失败: 原始注册器类型{reType},泛型类型{geneType}, 支持单例{support},信息{ex.Message}");
                    }
                }
            }

            private static void Match_SubClass(int timing, ESAS_RuntimeRegister_AB register, Type reType, Type geneType, bool init = true)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type support = types[0];

                    try
                    {
                        MethodInfo info = reType.GetMethod("Handle");
                        Func<Type, bool> func = (type) =>
                      {

                          if (!type.IsAbstract && type.IsSubclassOf(support))
                          {
                              info.Invoke(register, new object[] { type });
                              return true;//拦截
                          }
                          return false;
                      };
                        if (init) InitHandler_AsSubclass.Add(timing.GetHashCode(), func);
                        else HotHandler_AsSubclass.Add(timing.GetHashCode(), func);
                        // 如果创建成功，在这里使用 instance
                        // Console.WriteLine($"实例创建成功: {instance.GetType()}");
                    }
                    catch (ArgumentNullException ex)
                    {
                        Console.WriteLine($"类型参数为 null: {ex.Message}");
                    }
                    catch (ArgumentException ex)
                    {
                        Console.WriteLine($"类型参数无效（例如是开放泛型）: {ex.Message}");
                    }
                    catch (NotSupportedException ex)
                    {
                        Console.WriteLine($"不支持创建该类型的实例: {ex.Message}");
                    }
                    catch (TargetInvocationException ex)
                    {
                        // 这是一个包装异常，真正的错误在 InnerException 中
                        Console.WriteLine($"构造函数抛出异常: {ex.InnerException?.Message}");
                    }
                    catch (MissingMethodException ex)
                    {
                        Console.WriteLine($"未找到匹配的构造函数（例如没有无参构造函数）: {ex.Message}");
                    }
                    catch (MemberAccessException ex)
                    {
                        Console.WriteLine($"访问构造函数被拒绝（权限问题或抽象类）: {ex.Message}");
                    }
                    catch (TypeLoadException ex)
                    {
                        Console.WriteLine($"类型加载失败: {ex.Message}");
                    }
                    catch (Exception ex) // 捕获其他未预料到的异常
                    {
                        Console.WriteLine($"EditorRegister捕获单例继承失败: 原始注册器类型{reType},泛型类型{geneType}, 支持单例{support},信息{ex.Message}");
                    }
                }
            }


            #endregion
            private static bool Internal_IsValidAssembly(Assembly asm)
            {
                var name_ = asm.GetName().Name;

                if (RuntimeValidAssebliesName.Contains(name_))
                {
                    Debug.Log("RUNTIME可用" + name_);
                    return true;
                }
                //很多程序集没必要考虑的！！这里写一个方法到时候
                return false;
            }

            private static bool Internal_MatchDefineOne(Type instanceType, Type defineType, out Type matchType, int maxLevel = 3)
            {
                var currentBaseType = instanceType;
                while (maxLevel > 0 && currentBaseType != null && currentBaseType != typeof(object))
                {
                    // 2. 检查当前基类是否是泛型类型
                    if (currentBaseType.IsGenericType)
                    {
                        // 3. 获取泛型定义，并与目标定义比较
                        Type genericDefinition = currentBaseType.GetGenericTypeDefinition();
                        if (genericDefinition == defineType)
                        {
                            matchType = currentBaseType; // 找到了封闭的泛型基类
                            return true;
                        }
                    }
                    maxLevel--;
                    currentBaseType = currentBaseType.BaseType; // 继续向上查找
                }
                matchType = null;
                return false;
            }


        }

        #region  统一规范排序


        #endregion
    }
}