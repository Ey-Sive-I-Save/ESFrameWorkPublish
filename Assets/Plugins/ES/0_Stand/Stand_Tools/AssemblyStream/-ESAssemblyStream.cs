

using Sirenix.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

using static PlasticGui.WorkspaceWindow.Merge.MergeInProgress;



/* 
 *   Editor的 和 RunTime 使用一套类似的标准，但是生命周期完全不同！
 *   
 * 第一类 标志特性 不需要继承性质，为特性和目标带目标执行一次即可
 * 第二类 一次继承类 ，父类定义好规范后，每个类型生成一个实例 即可4
 * 第三类 对象即时添加,
 
 
 */
namespace ES
{
    public enum ESAssemblyLoadTiming : byte
    {
        /// <summary>
        /// 阶段0 ： 初始化程序集后
        /// </summary>
        [InspectorName("0：初始化程序集后")]
        _0_AfterInitAssemliesLoaded,
        /// <summary>
        /// 阶段1 ： 场景开始加载前
        /// </summary>
        [InspectorName("1：场景开始加载前")]
        _1_BeforeFirstSceneLoad,
        /// <summary>
        /// 阶段2 ： 场景加载完成后(已经全部Awake)
        /// </summary>
        [InspectorName("2：场景加载完成后")]
        _2_AfterFirstSceneLoad,
    }
    public class ESAssemblyStream
    {
        private static List<string> RuntimeValidAssebliesName = new List<string>() {
    "ES_Design","ES_Stand"
    };
        private static List<string> EditorValidAssebliesName = new List<string>() {
    "ES_Design","ES_Stand"
    };
        //编辑器部分
        private class EditorPart
        {
            #region 编辑器部分

            private static Assembly[] EditorAssembies;
            private static List<Assembly> ValidEditorAssembiles = new List<Assembly>(25);
            private static Dictionary<Assembly, Type[]> EditorTypes = new Dictionary<Assembly, Type[]>(25);

            private static List<Type> EditorRegisters = new List<Type>(25);

            #region 支持的Define模板
            private static readonly Type Define_ClassAttribute = typeof(EditorRegister_FOR_ClassAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_FieldAttribute = typeof(EditorRegister_FOR_FieldAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_MethodAttribute = typeof(EditorRegister_FOR_MethodAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_Singleton = typeof(EditorRegister_FOR_Singleton<>).GetGenericTypeDefinition();
            #endregion

            #region 处理流
            private static List<Func<Type, bool>> Handler_Singleton = new List<Func<Type, bool>>();
            private static List<Func<MethodInfo, bool>> Handler_MethodAttribute = new List<Func<MethodInfo, bool>>();
            private static List<Func<FieldInfo, bool>> Handler_FieldAttribute = new List<Func<FieldInfo, bool>>();
            private static List<Func<Type, bool>> Handler_ClassAttribute = new List<Func<Type, bool>>();
            #endregion


            private static bool TEST_Enable = false;
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

                Handler_Singleton.Clear();
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
                            if (t.IsSubclassOf(typeof(ESAS_EditorRegister_AB)))
                            {
                                EditorRegisters.Add(t);
                            }
                        }

                    }
                }
            }

            private static void Editor_ApplyRegisters()
            {
                for (int i = 0; i < EditorRegisters.Count; i++)
                {
                    Type re = EditorRegisters[i];
                    if (re.IsAbstract) continue;//必须可以创建！
                                                //这里为了性能，选择直接展开写
                    int maxLevel = 3;
                    var nowType = re;
                    while (maxLevel > 0 && nowType != null && nowType != typeof(object))
                    {
                        if (nowType.IsGenericType)
                        {
                            // 3. 获取泛型定义，并与目标定义比较
                            Type genericDefinition = nowType.GetGenericTypeDefinition();
                            if (genericDefinition == Define_ClassAttribute)
                            {
                                Match_ClassAttribute(re, nowType);
                                break;
                            }
                            if (genericDefinition == Define_FieldAttribute)
                            {
                                Match_FieldAttribute(re, nowType);
                                break;
                            }
                            if (genericDefinition == Define_MethodAttribute)
                            {
                                Match_MethodAttribute(re, nowType);
                                break;
                            }
                            if (genericDefinition == Define_Singleton)
                            {
                                Match_Singleton(re, nowType);
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
                            #region 单例类型
                            int lenForFunc_Singleton = Handler_Singleton.Count;
                            for (int indexAction = 0; indexAction < lenForFunc_Singleton; indexAction++)
                            {
                                //可用的单例匹配
                                var func = Handler_Singleton[indexAction];
                                if (func.Invoke(nowType))
                                {
                                    break;
                                }
                            }
                            #endregion

                            #region 类特性
                            int lenForFunc_ClassAttribute = Handler_ClassAttribute.Count;
                            for (int indexAction = 0; indexAction < lenForFunc_ClassAttribute; indexAction++)
                            {
                                //可用的单例匹配
                                var func = Handler_ClassAttribute[indexAction];
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
                                    int lenForFunc_FieldAttribute = Handler_FieldAttribute.Count;
                                    for (int indexAction = 0; indexAction < lenForFunc_FieldAttribute; indexAction++)
                                    {
                                        //可用的单例匹配
                                        var func = Handler_FieldAttribute[indexAction];
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
                                    int lenForFunc_FieldAttribute = Handler_MethodAttribute.Count;
                                    for (int indexAction = 0; indexAction < lenForFunc_FieldAttribute; indexAction++)
                                    {
                                        //可用的单例匹配
                                        var func = Handler_MethodAttribute[indexAction];
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
            private static void Match_ClassAttribute(Type reType, Type geneType)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type supportAttribute = types[0];

                    try
                    {
                        var register = Activator.CreateInstance(reType);
                        MethodInfo info = reType.GetMethod("Handle");
                        Handler_ClassAttribute.Add((classType) =>
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
            private static void Match_FieldAttribute(Type reType, Type geneType)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type supportAttribute = types[0];

                    try
                    {
                        var register = Activator.CreateInstance(reType);
                        MethodInfo info = reType.GetMethod("Handle");
                        Handler_FieldAttribute.Add((fieldINFO) =>
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
            private static void Match_MethodAttribute(Type reType, Type geneType)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type supportAttribute = types[0];

                    try
                    {
                        var register = Activator.CreateInstance(reType);
                        MethodInfo info = reType.GetMethod("Handle");
                        Handler_MethodAttribute.Add((methodINFO) =>
                        {
                            var at = methodINFO.GetCustomAttribute(supportAttribute);
                            if (at != null)
                            {
                                info.Invoke(register, new object[] { at, methodINFO });
                                return true;//拦截
                            }
                            return false;
                        });
                    }
                    catch (Exception ex) // 捕获其他未预料到的异常
                    {
                        Console.WriteLine($"EditorRegister捕获方法特性失败: 原始注册器类型{reType},泛型类型{geneType}, 支持特性{supportAttribute},信息{ex.Message}");
                    }
                }
            }
            private static void Match_Singleton(Type reType, Type geneType)
            {
                var types = geneType.GetGenericArguments();
                if (types.Length > 0)
                {
                    Type support = types[0];

                    try
                    {
                        var register = Activator.CreateInstance(reType);
                        MethodInfo info = reType.GetMethod("Handle");
                        Handler_Singleton.Add((type) =>
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
            private static bool Internal_IsValidAssembly(Assembly asm)
            {
                var name_ = asm.GetName().Name;
                //Debug.Log("Valid??"+name_);
                if (EditorValidAssebliesName.Contains(name_)) return true;
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


            #endregion


        }

        public class RunTimePart
        {
            private static Assembly[] InitRuntimeAssembies;
            private static List<Assembly> ValidRuntimeAssembiles = new List<Assembly>(25);
            private static Dictionary<Assembly, Type[]> RuntimeTypes = new Dictionary<Assembly, Type[]>(25);

            private static ES.KeyGroup<ESAssemblyLoadTiming, ESAS_RuntimeRegister_AB> InitRuntimeRegisters = new KeyGroup<ESAssemblyLoadTiming, ESAS_RuntimeRegister_AB>();
            private static Queue<ESAS_RuntimeRegister_AB> HotRuntimeRegisters = new Queue<ESAS_RuntimeRegister_AB>(20);
            private static Queue<Assembly> WaitHotLoadingAssembies = new Queue<Assembly>(4);
            #region 支持的Define模板
            private static readonly Type Define_ClassAttribute = typeof(RuntimeRegister_FOR_ClassAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_FieldAttribute = typeof(RuntimeRegister_FOR_FieldAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_MethodAttribute = typeof(RuntimeRegister_FOR_MethodAttribute<>).GetGenericTypeDefinition();
            private static readonly Type Define_Singleton = typeof(RuntimeRegister_FOR_Singleton<>).GetGenericTypeDefinition();
            #endregion

            #region 处理流
            private static KeyGroup<ESAssemblyLoadTiming, Func<Type, bool>> InitHandler_Singleton = new KeyGroup<ESAssemblyLoadTiming, Func<Type, bool>>();
            private static KeyGroup<ESAssemblyLoadTiming, Func<MethodInfo, bool>> InitHandler_MethodAttribute = new KeyGroup<ESAssemblyLoadTiming, Func<MethodInfo, bool>>();
            private static KeyGroup<ESAssemblyLoadTiming, Func<FieldInfo, bool>> InitHandler_FieldAttribute = new KeyGroup<ESAssemblyLoadTiming, Func<FieldInfo, bool>>();
            private static KeyGroup<ESAssemblyLoadTiming, Func<Type, bool>> InitHandler_ClassAttribute = new KeyGroup<ESAssemblyLoadTiming, Func<Type, bool>>();


            private static KeyGroup<ESAssemblyLoadTiming, Func<Type, bool>> HotHandler_Singleton = new KeyGroup<ESAssemblyLoadTiming, Func<Type, bool>>();
            private static KeyGroup<ESAssemblyLoadTiming, Func<MethodInfo, bool>> HotHandler_MethodAttribute = new KeyGroup<ESAssemblyLoadTiming, Func<MethodInfo, bool>>();
            private static KeyGroup<ESAssemblyLoadTiming, Func<FieldInfo, bool>> HotHandler_FieldAttribute = new KeyGroup<ESAssemblyLoadTiming, Func<FieldInfo, bool>>();
            private static KeyGroup<ESAssemblyLoadTiming, Func<Type, bool>> HotHandler_ClassAttribute = new KeyGroup<ESAssemblyLoadTiming, Func<Type, bool>>();
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
                InitHandler_MethodAttribute.Clear();
                InitHandler_FieldAttribute.Clear();
                InitHandler_ClassAttribute.Clear();
                InitRuntime_InitAssembiesAndAllRegisters();//初始化注册器--

                Debug.Log("完成全部初始化" + InitRuntimeRegisters.ToStringAllContent());

                DateTime startEditorStreamTime = DateTime.Now;
                //专属功能
                InitRuntime_ApplyThisTimingRegisters(ESAssemblyLoadTiming._0_AfterInitAssemliesLoaded);//应用注册器

                InitRuntime_LoadRegisteredTypes(ESAssemblyLoadTiming._0_AfterInitAssemliesLoaded);

                Debug.Log("第零阶段耗时" + (DateTime.Now - startEditorStreamTime));

                //等待注册
                AppDomain.CurrentDomain.AssemblyLoad += HotRuntimeLoadNewAssembly;
            }
            private static void HotRuntimeLoadNewAssembly(object sender, AssemblyLoadEventArgs args)
            {
                var asm = args.LoadedAssembly;
                WaitHotLoadingAssembies.Enqueue(asm);
                HotRuntime_ApplyThisAssemblyTimingRegisters(asm, ESAssemblyLoadTiming._0_AfterInitAssemliesLoaded);
                HotRuntime_ApplyThisAssemblyTimingRegisters(asm, ESAssemblyLoadTiming._1_BeforeFirstSceneLoad);
                HotRuntime_ApplyThisAssemblyTimingRegisters(asm, ESAssemblyLoadTiming._2_AfterFirstSceneLoad);
            }
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
            private static void RuntimeInitLoad_111_BeforeSceneLoad()
            {
                DateTime startEditorStreamTime = DateTime.Now;
                //专属功能
                InitRuntime_ApplyThisTimingRegisters(ESAssemblyLoadTiming._1_BeforeFirstSceneLoad);//应用注册器

                InitRuntime_LoadRegisteredTypes(ESAssemblyLoadTiming._1_BeforeFirstSceneLoad);

                Debug.Log("第一阶段耗时" + (DateTime.Now - startEditorStreamTime));
            }

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            private static void RuntimeInitLoad_222_AfterSceneLoad()
            {
                DateTime startEditorStreamTime = DateTime.Now;
                //专属功能
                InitRuntime_ApplyThisTimingRegisters(ESAssemblyLoadTiming._2_AfterFirstSceneLoad);//应用注册器

                InitRuntime_LoadRegisteredTypes(ESAssemblyLoadTiming._2_AfterFirstSceneLoad);
                Debug.Log("第二阶段耗时" + (DateTime.Now - startEditorStreamTime));
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
                                InitRuntimeRegisters.TryAdd(register.LoadTiming, register);
                            }
                        }

                    }
                }
            }

            private static void InitRuntime_ApplyThisTimingRegisters(ESAssemblyLoadTiming timing)
            {
                var reList = InitRuntimeRegisters.GetGroup(timing);
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
                                Match_ClassAttribute(timing, register, reType, nowType);
                                break;
                            }
                            if (genericDefinition == Define_FieldAttribute)
                            {
                                Match_FieldAttribute(timing, register, reType, nowType);
                                break;
                            }
                            if (genericDefinition == Define_MethodAttribute)
                            {
                                Match_MethodAttribute(timing, register, reType, nowType);
                                break;
                            }
                            if (genericDefinition == Define_Singleton)
                            {
                                Match_Singleton(timing, register, reType, nowType);
                                break;
                            }
                        }
                        maxLevel--;
                        nowType = nowType.BaseType; // 继续向上查找
                    }

                }
            }

            private static void InitRuntime_LoadRegisteredTypes(ESAssemblyLoadTiming timing)
            {
                var handles_singleton = InitHandler_Singleton.GetGroup(timing);
                var handler_classAttribute = InitHandler_ClassAttribute.GetGroup(timing);
                var handler_fieldAttribute = InitHandler_FieldAttribute.GetGroup(timing);
                var handler_methodAttribute = InitHandler_MethodAttribute.GetGroup(timing);
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
                            #region 单例类型
                            if (handles_singleton != null)
                            {
                                int lenForFunc_Singleton = handles_singleton.Count;
                                for (int indexAction = 0; indexAction < lenForFunc_Singleton; indexAction++)
                                {

                                    //可用的单例匹配
                                    var func = handles_singleton[indexAction];
                                    Debug.Log("匹配测试" + func);
                                    if (func.Invoke(nowType))
                                    {
                                        break;
                                    }
                                }
                            }
                            #endregion

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
            private static void HotRuntime_ApplyThisAssemblyTimingRegisters(Assembly assembly, ESAssemblyLoadTiming timing)
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
                            HotApplyRegister(register, timing, false);
                        }
                    }

                }

                //再应用
            }

            private static void OneTiming_HotLoadAllAssemblesRegistedTypes(ESAssemblyLoadTiming timing)
            {
                var handles_singleton = HotHandler_Singleton.GetGroup(timing);
                var handler_classAttribute = HotHandler_ClassAttribute.GetGroup(timing);
                var handler_fieldAttribute = HotHandler_FieldAttribute.GetGroup(timing);
                var handler_methodAttribute = HotHandler_MethodAttribute.GetGroup(timing);
                while (WaitHotLoadingAssembies.Count > 0)
                {
                    var hotASM = WaitHotLoadingAssembies.Dequeue();
                    var asm = hotASM;
                    if (RuntimeTypes.TryGetValue(asm, out var types))
                    {
                        int lenForTypes = types.Length;
                        //一组类型
                        for (int indexType = 0; indexType < lenForTypes; indexType++)
                        {
                            Type nowType = types[indexType];
                            #region 单例类型
                            if (handles_singleton != null)
                            {
                                int lenForFunc_Singleton = handles_singleton.Count;
                                for (int indexAction = 0; indexAction < lenForFunc_Singleton; indexAction++)
                                {

                                    //可用的单例匹配
                                    var func = handles_singleton[indexAction];
                                    Debug.Log("匹配测试" + func);
                                    if (func.Invoke(nowType))
                                    {
                                        break;
                                    }
                                }
                            }
                            #endregion

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
                OneTiming_HotLoadAllAssemblesRegistedTypes( ESAssemblyLoadTiming._0_AfterInitAssemliesLoaded);
                OneTiming_HotLoadAllAssemblesRegistedTypes(ESAssemblyLoadTiming._1_BeforeFirstSceneLoad);
                OneTiming_HotLoadAllAssemblesRegistedTypes(ESAssemblyLoadTiming._2_AfterFirstSceneLoad);
            }


        #endregion

            #region 便捷方法
        private static void HotApplyRegister(ESAS_RuntimeRegister_AB register, ESAssemblyLoadTiming timing, bool init = true)
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
                        Match_ClassAttribute(timing, register, reType, nowType, init);
                        break;
                    }
                    if (genericDefinition == Define_FieldAttribute)
                    {
                        Match_FieldAttribute(timing, register, reType, nowType, init);
                        break;
                    }
                    if (genericDefinition == Define_MethodAttribute)
                    {
                        Match_MethodAttribute(timing, register, reType, nowType, init);
                        break;
                    }
                    if (genericDefinition == Define_Singleton)
                    {
                        Match_Singleton(timing, register, reType, nowType, init);
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
        private static void Match_ClassAttribute(ESAssemblyLoadTiming timing, ESAS_RuntimeRegister_AB register, Type reType, Type geneType, bool init = true)
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
                    if (init) InitHandler_ClassAttribute.TryAdd(timing, func);
                    else HotHandler_ClassAttribute.TryAdd(timing, func);
                }
                catch (Exception ex) // 捕获其他未预料到的异常
                {
                    Console.WriteLine($"RuntimeRegister捕获字段特性失败: 原始注册器类型{reType},泛型类型{geneType}, 支持特性{supportAttribute},信息{ex.Message}");
                }
            }
        }
        private static void Match_FieldAttribute(ESAssemblyLoadTiming timing, ESAS_RuntimeRegister_AB register, Type reType, Type geneType, bool init = true)
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
                    if (init) InitHandler_FieldAttribute.TryAdd(timing, func);
                    else HotHandler_FieldAttribute.TryAdd(timing, func);
                }
                catch (Exception ex) // 捕获其他未预料到的异常
                {
                    Console.WriteLine($"RuntimeRegister捕获字段特性失败: 原始注册器类型{reType},泛型类型{geneType}, 支持特性{supportAttribute},信息{ex.Message}");
                }
            }
        }
        private static void Match_MethodAttribute(ESAssemblyLoadTiming timing, ESAS_RuntimeRegister_AB register, Type reType, Type geneType, bool init = true)
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
                    if (init) InitHandler_MethodAttribute.TryAdd(timing, func);
                    else HotHandler_MethodAttribute.TryAdd(timing, func);
                }
                catch (Exception ex) // 捕获其他未预料到的异常
                {
                    Console.WriteLine($"RuntimeRegister捕获方法特性失败: 原始注册器类型{reType},泛型类型{geneType}, 支持特性{supportAttribute},信息{ex.Message}");
                }
            }
        }
        private static void Match_Singleton(ESAssemblyLoadTiming timing, ESAS_RuntimeRegister_AB register, Type reType, Type geneType, bool init = true)
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
                        if (!type.IsAbstract && type.IsSubclassOf(type))
                        {
                            var singleton = Activator.CreateInstance(type);
                            info.Invoke(register, new object[] { singleton });
                            return true;//拦截
                        }
                        return false;
                    };
                    if (init) InitHandler_Singleton.TryAdd(timing, func);
                    else HotHandler_Singleton.TryAdd(timing, func);
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

        #endregion
        private static bool Internal_IsValidAssembly(Assembly asm)
        {
            var name_ = asm.GetName().Name;
            //Debug.Log("Valid??"+name_);
            if (RuntimeValidAssebliesName.Contains(name_)) return true;
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
}
}