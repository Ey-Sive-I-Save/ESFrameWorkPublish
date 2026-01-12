using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {
        //创建器
        public static class Creator
        {
            #region 深拷贝部分
            /// <summary>
            /// 深度克隆任意对象。
            /// 返回目标对象的独立副本；对于引用类型会递归克隆其字段与属性（基于反射）。
            /// </summary>
            /// <typeparam name="T">要克隆的目标类型。</typeparam>
            /// <param name="obj">要克隆的源对象。若为 <c>null</c> 将返回 <c>null</c>。</param>
            /// <returns>返回克隆后的对象，类型为 <typeparamref name="T"/>。</returns>
            public static T DeepClone<T>(T obj)
            {
                return (T)DeepCloneAnyObject(obj);
            }
            /// <summary>
            /// 深度克隆任意对象的入口方法（基于反射）。
            /// 支持基础值类型、字符串、数组、常见集合及自定义引用类型。
            /// 对于 <see cref="UnityEngine.Object"/>：当 <paramref name="HardUnityObject"/> 为 <c>true</c> 时会调用 <c>Instantiate</c> 创建实例（仅能在 Unity 主线程执行）；为 <c>false</c> 时保留原引用。
            /// </summary>
            /// <param name="obj">要克隆的源对象。</param>
            /// <param name="HardUnityObject">是否对 UnityEngine.Object 使用实例化拷贝（会调用 Instantiate）。注意：Instantiate 必须在主线程调用。</param>
            /// <param name="seldeDefineCreater">可选的自定义创建器，若提供则用于创建目标对象的实例。</param>
            /// <returns>克隆后的对象；若为不可克隆或输入为 <c>null</c> 则返回 <c>null</c> 或原始输入（视具体类型而定）。</returns>
            /// <remarks>
            /// - 方法使用反射实现，性能不如专用泛型实现；建议在性能敏感路径使用专用实现或预分配。
            /// - 当前实现基于递归反射，若对象图包含循环引用可能导致栈溢出或重复克隆（请在外部避免循环引用或扩展本方法以支持引用跟踪）。
            /// - 调用此方法应在 Unity 主线程中进行（若传入 Unity 对象且选择实例化）。
            /// </remarks>
            public static object DeepCloneAnyObject(object obj, bool HardUnityObject = true, Func<object> seldeDefineCreater = null)
            {
                //为NULL返回NULL
                if (obj == null)
                {
                    return null;
                }

               
                Type type = obj.GetType();
                // 如果是值类型或字符串，
                // 直接返回（值类型是不可变的，字符串是不可变引用类型）
                if (obj is string || type.IsEnum || type.IsPrimitive)
                {
                    return obj;
                }

                //如果是UnityObject -- 首次调用会实例化，否则直接引用
                if (obj is UnityEngine.Object uObj)
                {
                    if (uObj == null) return null;
                    if (HardUnityObject)
                    {
                        return UnityEngine.Object.Instantiate(uObj);
                    }
                    else
                    {
                        return obj;
                    }
                }

                // 如果是数组类型-创建数组并且把数据深拷贝后加入
                if (type.IsArray)
                {
                    var array = obj as Array;
                    Type elementType = type.GetElementType();
                    Array copiedArray = Array.CreateInstance(elementType, array.Length);
                    for (int i = 0; i < array.Length; i++)
                    {
                        copiedArray.SetValue(DeepCloneAnyObject(array.GetValue(i), false), i);
                    }
                    return copiedArray;
                }

                // 如果是集合类型（如 List、Dictionary 等） 
                if (typeof(IEnumerable).IsAssignableFrom(type))
                {
                    var count = type.GetGenericArguments().Length;
                    if (count == 1)
                    {
                        var addMethod = type.GetMethod("Add");
                        if (addMethod != null)
                        {
                            var copiedCollection = Activator.CreateInstance(type);
                            foreach (var item in (IEnumerable)obj)
                            {
                                var use = DeepCloneAnyObject(item, false);
                                addMethod.Invoke(copiedCollection, new object[] { use });
                            }
                            return copiedCollection;
                        }
                        else
                        {
                            //硬核版本
                            return DeepCloneCollection(obj, type);
                        }
                    }
                    else
                    {
                        //硬核版本
                        return DeepCloneCollection(obj, type);
                    }
                }
                //如果是结构体("已经排除了原始类型--无法通过直接赋值来拷贝结构体，因为已经作为Struct装箱了

                // 如果是普通引用类型或结构体--结合
                var clonedObject = seldeDefineCreater != null ? seldeDefineCreater.Invoke() : Activator.CreateInstance(type);
                if (clonedObject is IDeepClone deep)
                {
                    deep.DeepCloneFrom(obj);
                    return deep;
                }
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (field.IsStatic)
                        continue;

                    object fieldValue = field.GetValue(obj);
                    object clonedValue = DeepCloneAnyObject(fieldValue, false);

                    field.SetValue(clonedObject, clonedValue);
                }
                return clonedObject;
            }
            /// <summary>
            /// 针对常见集合类型（List/Dictionary/HashSet/Queue/Stack/LinkedList/ArrayList）做深拷贝的分发入口。
            /// 当 collectionType 指定或能从集合对象推断出具体泛型类型时，会调用对应的泛型专用实现。
            /// </summary>
            /// <param name="collection">要克隆的集合实例。</param>
            /// <param name="collectionType">可选的集合类型（通常可为 <c>null</c>，方法会自动推断）。</param>
            /// <param name="creator">可选的元素创建器（仅在自定义反射路径需要时使用）。</param>
            /// <returns>返回克隆后的集合对象，类型与输入集合一致（若无法克隆会返回原集合并记录警告）。</returns>
            public static object DeepCloneCollection(object collection, Type collectionType = null, Func<object> creator = null)
            {
                collectionType ??= collection.GetType();
                // 特殊处理常见集合 带泛型
                if (collectionType.IsGenericType)
                {
                    Type genericDef = collectionType.GetGenericTypeDefinition();

                    // 处理List<T>和IList<T>
                    if (genericDef == typeof(List<>) || genericDef == typeof(IList<>))
                    {
                        return DeepCloneGenericList(collection, collectionType);
                    }
                    Debug.Log("BeforeDIc");
                    // 处理Dictionary<TKey, TValue>
                    if (genericDef == typeof(Dictionary<,>))
                    {
                        return DeepCloneGenericDictionary(collection, collectionType);
                    }
                    Debug.Log("AfterDIc");
                    // 处理HashSet<T>
                    if (genericDef == typeof(HashSet<>))
                    {
                        return DeepCloneGenericHashSet(collection, collectionType);
                    }

                    // 处理队列Queue<T>
                    if (genericDef == typeof(Queue<>))
                    {
                        return DeepCloneGenericQueue(collection, collectionType);
                    }
                    //处理栈
                    if (genericDef == typeof(Stack<>))
                    {
                        return DeepCloneGenericStack(collection, collectionType);
                    }
                    //处理链表
                    if (genericDef == typeof(LinkedList<>))
                    {
                        return DeepCloneGenericLinkedList(collection, collectionType);
                    }
                }
                //处理ArrayList
                // 处理非泛型集合
                if (collectionType == typeof(ArrayList))
                {
                    return DeepCloneArrayList((ArrayList)collection);
                }

                // 回退到通用反射方法
                return DeepCloneCollectionByReflection_CantUSE(collection, collectionType, creator);
            }
            /// <summary>
            /// 深拷贝字典（Dictionary&lt;K,V&gt;）的专用实现。
            /// 使用 <see cref="IDictionary"/> 遍历键值对并递归克隆键与值。
            /// </summary>
            /// <param name="dictionary">源字典对象（可以为任意实现了 IDictionary 的实例）。</param>
            /// <param name="dictType">可选的字典类型；若为空将从实例推断。</param>
            /// <returns>返回克隆后的字典实例（类型与输入相同）。</returns>
            public static object DeepCloneGenericDictionary(object dictionary, Type dictType=null)
            {
                dictType ??= dictionary.GetType();
                Type[] genericArgs = dictType.GetGenericArguments();
                Type keyType = genericArgs[0];
                Type valueType = genericArgs[1];

                // 创建新字典实例
                object newDict = Activator.CreateInstance(dictType);
                var addMethod = dictType.GetMethod("Add");
                if (addMethod != null && dictionary is IDictionary idict)
                {
                    foreach (DictionaryEntry pair in idict)
                    {
                        var key = DeepCloneAnyObject(pair.Key, false);
                        var value = DeepCloneAnyObject(pair.Value, false);
                        addMethod.Invoke(newDict, new object[] { key, value });
                    }
                }
                else
                {

                }
                return newDict;
            }
            /// <summary>
            /// 深拷贝 List&lt;T&gt; 或其它实现 IList 的泛型列表。
            /// 会尝试使用带容量的构造函数（若存在）以减少重新分配开销；若不存在则回退到无参构造。
            /// </summary>
            /// <param name="list">源列表实例。</param>
            /// <param name="listType">可选的列表类型；若为空将从实例推断。</param>
            /// <returns>返回克隆后的列表实例（元素为递归克隆结果）。</returns>
            public static object DeepCloneGenericList(object list, Type listType=null)
            {
                listType ??= list.GetType();
                Type elementType = listType.GetGenericArguments()[0];
                int count = (int)listType.GetProperty("Count").GetValue(list);

                // 使用预分配容量优化性能
                var newList = Activator.CreateInstance(
                    listType,
                    new object[] { count }); // 指定初始容量

                var addMethod = listType.GetMethod("Add");
                
                foreach(var one in list as IEnumerable)
                {
                    addMethod.Invoke(newList, DeepCloneAnyObject(one,false)._AsArrayOnlySelf());
                }
                return newList;
            }
            /// <summary>
            /// 深拷贝 HashSet&lt;T&gt; 的实现。
            /// </summary>
            /// <param name="hashSet">源 HashSet 实例。</param>
            /// <param name="hashSetType">可选的 HashSet 类型；通常可为空以自动推断。</param>
            /// <returns>返回克隆后的 HashSet 实例。</returns>
            public static object DeepCloneGenericHashSet(object hashSet, Type hashSetType=null)
            {
                hashSetType ??= hashSet.GetType();
                // 获取泛型参数类型
                Type elementType = hashSetType.GetGenericArguments()[0];

                // 创建新HashSet实例
                object newHashSet = Activator.CreateInstance(hashSetType);

                // 获取Add方法
                MethodInfo addMethod = hashSetType.GetMethod("Add");

                // 遍历原HashSet
                foreach (var item in (IEnumerable)hashSet)
                {
                    object clonedItem = DeepCloneAnyObject(item, false);
                    addMethod.Invoke(newHashSet, new[] { clonedItem });
                }

                return newHashSet;
            }
            /// <summary>
            /// 深拷贝 Stack&lt;T&gt;。为了保持元素顺序，先将源栈元素复制到临时列表并反转再压入目标栈。
            /// </summary>
            /// <param name="stack">源栈实例。</param>
            /// <param name="stackType">可选的栈类型；若为空自动推断。</param>
            /// <param name="creator">元素的自定义创建器（可选）。</param>
            /// <returns>返回克隆后的栈实例。</returns>
            public static object DeepCloneGenericStack(object stack, Type stackType=null, Func<object> creator = null)
            {
                stackType ??= stack.GetType();
                // 获取元素类型
                Type elementType = stackType.GetGenericArguments()[0];

                // 获取源stack计数
                int count = (int)stackType.GetProperty("Count").GetValue(stack);

                // 创建新的Stack实例
                var newStack = Activator.CreateInstance(stackType, new object[] { count });

                // 获取Push方法
                MethodInfo pushMethod = stackType.GetMethod("Push");

                // 由于Stack是LIFO结构，需要反转元素顺序
                var tempList = new System.Collections.Generic.List<object>();

                // 将源栈的所有元素复制到临时列表
                foreach (var item in (IEnumerable)stack)
                {
                    tempList.Add(item);
                }

                // 反转列表（从栈底到栈顶顺序）
                tempList.Reverse();

                // 将元素推入新栈（保持原始顺序）
                foreach (var item in tempList)
                {
                    object clonedItem = DeepCloneAnyObject(item, false, seldeDefineCreater: creator);
                    pushMethod.Invoke(newStack, new[] { clonedItem });
                }

                return newStack;
            }
            /// <summary>
            /// 深拷贝 LinkedList&lt;T&gt;。
            /// 通过获取首节点并沿着 Next 节点逐个克隆 Value 并调用 AddLast 添加到目标链表。
            /// </summary>
            /// <param name="linkedList">源链表实例。</param>
            /// <param name="linkedListType">可选链表类型；若为空将自动推断。</param>
            /// <param name="creator">节点元素的自定义创建器（可选）。</param>
            /// <returns>返回克隆后的链表实例。</returns>
            public static object DeepCloneGenericLinkedList(object linkedList, Type linkedListType=null, Func<object> creator = null)
            {
                linkedListType ??= linkedList.GetType();
                // 获取元素类型
                Type elementType = linkedListType.GetGenericArguments()[0];

                // 创建新的LinkedList实例
                object newList = Activator.CreateInstance(linkedListType);

                // 获取AddLast方法（我们选择在末尾添加，保持顺序）
                MethodInfo addLastMethod = linkedListType.GetMethod("AddLast", new Type[] { elementType }) ?? linkedListType.GetMethod("AddLast");
                if (addLastMethod == null) return linkedList;

                // 获取第一个节点
                PropertyInfo firstProperty = linkedListType.GetProperty("First");
                object firstNode = firstProperty == null ? null : firstProperty.GetValue(linkedList);

                // 如果链表为空，直接返回新实例
                if (firstNode == null)
                    return newList;

                // 遍历链表
                Type nodeType = firstNode.GetType();
                PropertyInfo nextProperty = nodeType.GetProperty("Next");
                PropertyInfo valueProperty = nodeType.GetProperty("Value") ?? nodeType.GetProperty("Item") ;

                object currentNode = firstNode;
                while (currentNode != null)
                {
                    // 获取节点值（防空检查）
                    object value = valueProperty == null ? null : valueProperty.GetValue(currentNode);
                    // 深拷贝值
                    object clonedValue = DeepCloneAnyObject(value, false, creator);
                    // 添加到新链表
                    addLastMethod.Invoke(newList, new[] { clonedValue });

                    // 移动到下一个节点（防空）
                    currentNode = nextProperty == null ? null : nextProperty.GetValue(currentNode);
                }

                return newList;
            }
            /// <summary>
            /// 深拷贝 Queue&lt;T&gt;。
            /// 支持尝试使用容量构造函数以优化性能，失败则回退到默认构造。
            /// </summary>
            /// <param name="queue">源队列实例。</param>
            /// <param name="queueType">可选队列类型；若为空自动推断。</param>
            /// <param name="creator">元素的自定义创建器（可选）。</param>
            /// <returns>返回克隆后的队列实例。</returns>
            public static object DeepCloneGenericQueue(object queue, Type queueType=null, Func<object> creator = null)
            {
                queueType ??= queue.GetType();
                // 1. 获取元素类型
                Type elementType = queueType.GetGenericArguments()[0];

                // 2. 获取源队列计数和容量
                int count = (int)queueType.GetProperty("Count").GetValue(queue);
                int capacity = count; // 使用实际元素数作为初始容量

                // 3. 创建新的Queue实例（使用容量优化）
                object newQueue;
                try
                {
                    // 尝试使用容量创建实例
                    newQueue = Activator.CreateInstance(queueType, new object[] { capacity });
                }
                catch
                {
                    // 如果带参数的构造函数不可用，使用无参构造函数
                    newQueue = Activator.CreateInstance(queueType);
                }

                // 4. 获取Enqueue方法（缓存机制）
                MethodInfo enqueueMethod = queueType.GetMethod("Enqueue");

                // 5. 根据元素类型优化处理


                // 其他引用类型需要深拷贝
                if (enqueueMethod != null)
                    foreach (var item in (IEnumerable)queue)
                    {
                        object clonedItem = DeepCloneAnyObject(item, false, creator);
                        if (clonedItem != null) enqueueMethod.Invoke(newQueue, new[] { clonedItem });
                    }


                return newQueue;
            }
            /// <summary>
            /// 深拷贝非泛型 ArrayList。
            /// </summary>
            /// <param name="arrayList">源 ArrayList。</param>
            /// <returns>返回克隆后的 ArrayList，每个元素均为递归克隆结果。</returns>
            public static ArrayList DeepCloneArrayList(ArrayList arrayList)
            {
                ArrayList newList = new ArrayList(arrayList.Count);

                foreach (var item in arrayList)
                {
                    newList.Add(DeepCloneAnyObject(item, false));
                }

                return newList;
            }
            /// <summary>
            /// 反射通用深拷贝（回退方案）。
            /// 当无法匹配到预定义集合类型时使用此方法尝试通过查找 <c>Add</c> 方法逐项拷贝。
            /// 该方法目前为降级实现，性能与健壮性有限，建议仅在非关键路径使用。
            /// </summary>
            /// <param name="collection">源集合实例。</param>
            /// <param name="collectionType">集合类型；若为空将自动推断。</param>
            /// <param name="creator">元素的自定义创建器（可选）。</param>
            /// <returns>返回新集合实例或在无法复制时返回原集合并记录警告。</returns>
            public static object DeepCloneCollectionByReflection_CantUSE(object collection, Type collectionType=null, Func<object> creator=null)
            {
                collectionType ??= collection.GetType();
                // 尝试创建实例
                object newCollection = creator?.Invoke() ?? Activator.CreateInstance(collectionType);

                
                // 尝试查找Add方法
                MethodInfo addMethod = null;
                
                foreach (var method in collectionType.GetMethods())
                {
                    if (method.Name == "Add" && method.GetParameters().Length == 1)
                    {
                        addMethod = method;
                        break;
                    }
                }

                // 如果找到Add方法
                if (addMethod != null)
                {
                    foreach (var item in (IEnumerable)collection)
                    {
                        var clonedItem = DeepCloneAnyObject(item, false);
                        addMethod.Invoke(newCollection, new[] { clonedItem });
                    }
                    return newCollection;
                }
                // 尝试使用ICollection接口
              /*   if (collection is ICollection coll)
                 {
                     var newColl = (ICollection)Activator.CreateInstance(collectionType);
                     
                     // 不支持添加项的集合（如只读集合）

                         foreach (var item in coll)
                         {
                             newColl.(DeepCloneObject(item));
                         }


                     return newColl;
                 }*/

                // 最终回退方案：无法复制，返回原始集合
                Debug.LogWarning($"无法深拷贝集合类型: {collectionType.Name}");
                return collection;
            }

            #endregion

            #region CreatePath支持
            public static SelectDic<string, string, Type> CreatePaths = new();

            public class ER_CreatePath : EditorRegister_FOR_ClassAttribute<ESCreatePathAttribute>
            {
                public override void Handle(ESCreatePathAttribute attribute, Type type)
                {
                    
                    CreatePaths.AddOrSet(attribute.GroupName,attribute.MyName,type);
                }
            }

            #endregion

        }


    }
}

