using Sirenix.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
/* 
 *   Editor的 和 RunTime 使用一套类似的标准，但是生命周期完全不同！
 *   
 * 第一类 标志特性 不需要继承性质，为特性和目标带目标执行一次即可
 * 第二类 一次继承类 ，父类定义好规范后，每个类型生成一个实例 即可4
 * 第三类 对象即时添加,
 
 
 */
public class ESAssemblyStream
{
    #region 编辑器部分
    public static Assembly[] EditorAssembies;
    public static List<Assembly> ValidEditorAssembiles = new List<Assembly>(25);
    public static Dictionary<Assembly, Type[]> EditorTypes = new Dictionary<Assembly, Type[]>(25);

    public static List<Type> EditorRegisters = new List<Type>(25);

    #region 支持的Define模板
    public static readonly Type Define_ClassAttribute = typeof(EditorRegisterFORClassAttribute<>).GetGenericTypeDefinition();
    public static readonly Type Define_FieldAttribute = typeof(EditorRegisterFORFieldAttribute<>).GetGenericTypeDefinition();
    public static readonly Type Define_MethodAttribute = typeof(EditorRegisterFORMethodAttribute<>).GetGenericTypeDefinition();
    public static readonly Type Define_Singleton = typeof(EditorRegisterFORSingleton<>).GetGenericTypeDefinition();
    #endregion

    #region 处理流
    public static List<Func<Type, bool>> Handler_Singleton = new List<Func<Type, bool>>();
    public static List<Func<MethodInfo, bool>> Handler_MethodAttribute = new List<Func<MethodInfo, bool>>();
    public static List<Func<FieldInfo, bool>> Handler_FieldAttribute = new List<Func<FieldInfo, bool>>();
    public static List<Func<Type, bool>> Handler_ClassAttribute = new List<Func<Type, bool>>();
    #endregion

    //编辑器下的程序集流
    [InitializeOnLoadMethod]
    public static void EditorInitLoad()
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

        Editor_ApplyRegisters();//应用注册器

        Editor_LoadRegisteredTypes();//加载被注册的类效果
    }

    public static void Editor_InitAssembiesAndRegisters()
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

    public static void Editor_ApplyRegisters()
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

    public static void Editor_LoadRegisteredTypes()
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
                    for(int indexAction=0;indexAction< lenForFunc_Singleton; indexAction++)
                    {
                        //可用的单例匹配
                        var func = Handler_Singleton[indexAction];
                        if (func?.Invoke(nowType)??false) {
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
                        if (func?.Invoke(nowType) ?? false)
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
                                if (func?.Invoke(infoF) ?? false)
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
    internal static void Match_ClassAttribute(Type reType, Type geneType)
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
    internal static void Match_FieldAttribute(Type reType, Type geneType)
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
    internal static void Match_MethodAttribute(Type reType, Type geneType)
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
                    if (at!=null)
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
    internal static void Match_Singleton(Type reType, Type geneType)
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

                    if (!type.IsAbstract&& type.IsSubclassOf(support))
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
    internal static bool Internal_IsValidAssembly(Assembly asm)
    {
        //很多程序集没必要考虑的！！这里写一个方法到时候
        return true;
    }

    internal static bool Internal_MatchDefineOne(Type instanceType, Type defineType, out Type matchType, int maxLevel = 3)
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void RuntimeInitLoad()
    {

    }

    public void EditorLoadNewAssembly()
    {

    }
}
