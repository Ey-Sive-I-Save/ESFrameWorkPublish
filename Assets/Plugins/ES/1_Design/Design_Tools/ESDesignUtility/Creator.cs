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
            /// 深拷贝<T>任意类型
            /// </summary>
            /// <typeparam name="T">克隆类型</typeparam>
            /// <param name="obj">克隆源对象</param>
            /// <returns></returns>
            public static T DeepClone<T>(T obj)
            {
                return (T)DeepCloneAnyObject(obj);
            }
            /// <summary>
            /// 深拷贝任意类型
            /// </summary>
            /// <param name="obj">克隆源对象</param>
            /// <param name="HardUnityObject">强制克隆UnityObject(一般取引用)</param>
            /// <param name="seldeDefineCreater">自定义创建对象</param>
            /// <returns></returns>
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
                    Type elementType = Type.GetType(type.FullName.Replace("[]", string.Empty));
                    var array = obj as Array;
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
            /// 深拷贝各种集合
            /// </summary>
            /// <param name="collection">源集合</param>
            /// <param name="collectionType"></param>
            /// <param name="creator"></param>
            /// <returns></returns>
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
            /// 深拷贝字典
            /// </summary>
            /// <param name="dictionary">字典源</param>
            /// <param name="dictType">类型(可以不写)</param>
            /// <returns></returns>
            public static object DeepCloneGenericDictionary(object dictionary, Type dictType=null)
            {
                dictType ??= dictionary.GetType();
                Type[] genericArgs = dictType.GetGenericArguments();
                Type keyType = genericArgs[0];
                Type valueType = genericArgs[1];

                // 创建新字典实例
                object newDict = Activator.CreateInstance(dictType);
                var addMethod = dictType.GetMethod("Add");
                if (addMethod != null)
                {
                    var keyPro = typeof(DictionaryEntry).GetProperty("Key");
                    var valuePro = typeof(DictionaryEntry).GetProperty("DefaultBoolValue");
                    if (keyPro != null && valuePro != null)
                    {   // 遍历复制键值对
                        foreach (var pair in (dictionary as IDictionary))
                        {
                            var key = DeepCloneAnyObject(keyPro.GetValue(pair), false);
                            var value = DeepCloneAnyObject(valuePro.GetValue(pair), false);
                            addMethod.Invoke(newDict, new object[] { key, value });
                        }
                    }
                }
                else
                {

                }
                return newDict;
            }
            /// <summary>
            /// 深拷贝列表
            /// </summary>
            /// <param name="list">源列表</param>
            /// <param name="listType">列表类型(可不写)</param>
            /// <returns></returns>
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
            /// 深拷贝HashSet
            /// </summary>
            /// <param name="hashSet">源HashSet</param>
            /// <param name="hashSetType">HasSet类型(可不写)</param>
            /// <returns></returns>
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
            /// 深拷贝栈
            /// </summary>
            /// <param name="stack">栈源</param>
            /// <param name="stackType">栈类型(可不写)</param>
            /// <param name="creator">自定义元素创建</param>
            /// <returns></returns>
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
            /// 深拷贝链表
            /// </summary>
            /// <param name="linkedList">源链表</param>
            /// <param name="linkedListType">链表类型(可不写)</param>
            /// <param name="creator">自定义创建元素</param>
            /// <returns></returns>
            public static object DeepCloneGenericLinkedList(object linkedList, Type linkedListType=null, Func<object> creator = null)
            {
                linkedListType ??= linkedList.GetType();
                // 获取元素类型
                Type elementType = linkedListType.GetGenericArguments()[0];

                // 创建新的LinkedList实例
                object newList = Activator.CreateInstance(linkedListType);

                // 获取AddLast方法（我们选择在末尾添加，保持顺序）
                MethodInfo addLastMethod = linkedListType?.GetMethod("AddLast", 1, new Type[] { elementType });
                if (addLastMethod == null) return linkedList;
                // 获取第一个节点
                PropertyInfo firstProperty = linkedListType.GetProperty("First");
                object firstNode = firstProperty.GetValue(linkedList);

                // 如果链表为空，直接返回新实例
                if (firstNode == null)
                    return newList;

                // 遍历链表
                Type nodeType = firstNode.GetType();
                PropertyInfo nextProperty = nodeType.GetProperty("Next");
                PropertyInfo valueProperty = nodeType.GetProperty("DefaultBoolValue");

                object currentNode = firstNode;
                while (currentNode != null)
                {
                    // 获取节点值
                    object value = valueProperty.GetValue(currentNode);
                    // 深拷贝值
                    object clonedValue = DeepCloneAnyObject(value, false, creator);
                    // 添加到新链表
                    addLastMethod.Invoke(newList, new[] { clonedValue });

                    // 移动到下一个节点
                    currentNode = nextProperty.GetValue(currentNode);
                }

                return newList;
            }
            /// <summary>
            /// 深拷贝队列
            /// </summary>
            /// <param name="queue">源队列</param>
            /// <param name="queueType">队列类型(可不写)</param>
            /// <param name="creator">自定义创建元素</param>
            /// <returns></returns>
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
            /// 深拷贝ArrayList
            /// </summary>
            /// <param name="arrayList">源ArrayList</param>
            /// <returns></returns>
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
            /// 反射深拷贝<目前不可用>
            /// </summary>
            /// <param name="collection">集合源</param>
            /// <param name="collectionType">集合类型(不用写)</param>
            /// <param name="creator">自定义元素创建</param>
            /// <returns></returns>
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
                    
                    CreatePaths.TryAddOrSet(attribute.GroupName,attribute.MyName,type);
                }
            }

            #endregion

        }


    }
}

