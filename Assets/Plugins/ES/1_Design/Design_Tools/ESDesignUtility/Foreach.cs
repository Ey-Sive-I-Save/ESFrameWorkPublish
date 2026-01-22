using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {

        public static class Foreach
        {
            #region  Transform查找工具
            /// <summary>
            /// 通用查找工具类。提供Transform查找、GameObject查找、Component查找等常用查找操作。
            /// </summary>
            /// <remarks>
            /// 使用说明：
            /// - Transform查找使用广度优先搜索(BFS)，避免深层级递归导致的栈溢出
            /// - 支持传入结果列表参数以减少GC分配，适合频繁调用的场景
            /// - 名称查找支持路径格式(如"Parent/Child")和单一名称
            /// - 标签查找需要标签已在Unity Tag管理器中注册
            /// - 所有方法均为线程安全的纯函数
            /// </remarks>

            /// <summary>
            /// 在子节点中查找第一个名称匹配的 Transform。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回 null。</param>
            /// <param name="name">要查找的名称。支持路径格式(如"Parent/Child")或单一名称。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <returns>找到的第一个匹配节点，未找到返回 null。</returns>
            /// <example>
            /// <code>
            /// // 查找名为 "Player" 的子节点
            /// Transform player = Foreach.FindChildByName(root, "Player");
            /// // 查找路径为 "UI/Panel" 的子节点
            /// Transform panel = Foreach.FindChildByName(root, "UI/Panel");
            /// </code>
            /// </example>
            public static Transform FindChildByName(Transform me, string name, bool includeSelf = false)
            {
                if (me == null || string.IsNullOrEmpty(name)) return null;
                if (includeSelf && me.gameObject.name == name) return me;
                if (me.childCount == 0) return null;

                // 路径查找使用 Transform.Find
                if (name.Contains('/'))
                {
                    return me.Find(name);
                }

                // 单一名称使用BFS查找
                Transform directChild = me.Find(name);
                if (directChild != null) return directChild;

                var queue = new Queue<Transform>();
                for (int i = 0; i < me.childCount; i++)
                {
                    queue.Enqueue(me.GetChild(i));
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.gameObject.name == name) return current;
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return null;
            }
            /// <summary>
            /// 在子节点中查找第一个标签匹配的 Transform。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回 null。</param>
            /// <param name="tag">要查找的标签。必须是Unity中已注册的标签名。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <returns>找到的第一个匹配节点，未找到返回 null。</returns>
            public static Transform FindChildByTag(Transform me, string tag, bool includeSelf = false)
            {
                if (me == null || string.IsNullOrEmpty(tag)) return null;
                if (includeSelf && me.gameObject.CompareTag(tag)) return me;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                for (int i = 0; i < me.childCount; i++)
                {
                    queue.Enqueue(me.GetChild(i));
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.gameObject.CompareTag(tag)) return current;
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return null;
            }
            /// <summary>
            /// 在子节点中查找第一个层级匹配的 Transform。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回 null。</param>
            /// <param name="layer">要查找的层级索引。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <returns>找到的第一个匹配节点，未找到返回 null。</returns>
            public static Transform FindChildByLayer(Transform me, int layer, bool includeSelf = false)
            {
                if (me == null) return null;
                if (includeSelf && me.gameObject.layer == layer) return me;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                for (int i = 0; i < me.childCount; i++)
                {
                    queue.Enqueue(me.GetChild(i));
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.gameObject.layer == layer) return current;
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return null;
            }
            
            /// <summary>
            /// 在子节点中查找第一个在指定层级掩码中的 Transform。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回 null。</param>
            /// <param name="layerMask">要查找的层级掩码。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <returns>找到的第一个匹配节点，未找到返回 null。</returns>
            /// <example>
            /// <code>
            /// // 查找在 Default 或 UI 层级的子节点
            /// LayerMask mask = LayerMask.GetMask("Default", "UI");
            /// Transform found = Foreach.FindChildInLayerMask(root, mask);
            /// </code>
            /// </example>
            public static Transform FindChildInLayerMask(Transform me, LayerMask layerMask, bool includeSelf = false)
            {
                if (me == null) return null;
                if (includeSelf && ((1 << me.gameObject.layer) & layerMask) != 0) return me;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                for (int i = 0; i < me.childCount; i++)
                {
                    queue.Enqueue(me.GetChild(i));
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (((1 << current.gameObject.layer) & layerMask) != 0) return current;
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return null;
            }
            /// <summary>
            /// 向上查找第一个名称匹配的父节点。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回 null。</param>
            /// <param name="name">要查找的名称。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <returns>找到的第一个匹配父节点，未找到返回 null。</returns>
            public static Transform FindParentByName(Transform me, string name, bool includeSelf = false)
            {
                if (me == null) return null;
                Transform t = includeSelf ? me : me.parent;
                while (t != null)
                {
                    if (t.gameObject.name == name)
                    {
                        return t;
                    }
                    t = t.parent;
                }
                return null;
            }
            /// <summary>
            /// 向上查找第一个标签匹配的父节点。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回 null。</param>
            /// <param name="tag">要查找的标签。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <returns>找到的第一个匹配父节点，未找到返回 null。</returns>
            public static Transform FindParentByTag(Transform me, string tag, bool includeSelf = false)
            {
                if (me == null) return null;
                Transform t = includeSelf ? me : me.parent;
                while (t != null)
                {
                    if (t.gameObject.CompareTag(tag))
                    {
                        return t;
                    }
                    t = t.parent;
                }
                return null;
            }
            /// <summary>
            /// 在子节点中查找第一个满足条件的 Transform。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回 null。</param>
            /// <param name="condition">判断条件函数。为 null 时返回 null。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <returns>找到的第一个满足条件的节点，未找到返回 null。</returns>
            /// <example>
            /// <code>
            /// // 查找激活状态的子节点
            /// Transform active = Foreach.FindChildWhere(root, t => t.gameObject.activeSelf);
            /// // 查找包含特定组件的子节点
            /// Transform hasScript = Foreach.FindChildWhere(root, t => t.GetComponent&lt;MyScript&gt;() != null);
            /// </code>
            /// </example>
            public static Transform FindChildWhere(Transform me, Func<Transform, bool> condition, bool includeSelf = false)
            {
                if (me == null || condition == null) return null;
                if (includeSelf && condition(me)) return me;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                for (int i = 0; i < me.childCount; i++)
                {
                    queue.Enqueue(me.GetChild(i));
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (condition(current)) return current;
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return null;
            }

            /// <summary>
            /// 在子节点中查找所有名称匹配的 Transform。不支持路径格式。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回空列表。</param>
            /// <param name="name">要查找的名称。不支持路径格式。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <param name="results">可选的输出列表。传入列表可减少GC分配，适合频繁调用。</param>
            /// <returns>所有匹配的节点列表。</returns>
            public static List<Transform> FindAllChildrenByName(Transform me, string name, bool includeSelf = false, List<Transform> results = null)
            {
                var list = results ?? new List<Transform>(4);
                if (me == null || string.IsNullOrEmpty(name)) return list;
                if (name.Contains('/')) return list; // 不支持路径

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                if (includeSelf) queue.Enqueue(me);
                else
                {
                    for (int i = 0; i < me.childCount; i++)
                    {
                        queue.Enqueue(me.GetChild(i));
                    }
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.gameObject.name == name) list.Add(current);
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return list;
            }

            /// <summary>
            /// 在子节点中查找所有标签匹配的 Transform。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回空列表。</param>
            /// <param name="tag">要查找的标签。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <param name="results">可选的输出列表。传入列表可减少GC分配，适合频繁调用。</param>
            /// <returns>所有匹配的节点列表。</returns>
            public static List<Transform> FindAllChildrenByTag(Transform me, string tag, bool includeSelf = false, List<Transform> results = null)
            {
                var list = results ?? new List<Transform>(4);
                if (me == null || string.IsNullOrEmpty(tag)) return list;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                if (includeSelf) queue.Enqueue(me);
                else
                {
                    for (int i = 0; i < me.childCount; i++)
                    {
                        queue.Enqueue(me.GetChild(i));
                    }
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.gameObject.CompareTag(tag)) list.Add(current);
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return list;
            }

            /// <summary>
            /// 在子节点中查找所有层级匹配的 Transform。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回空列表。</param>
            /// <param name="layer">要查找的层级索引。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <param name="results">可选的输出列表。传入列表可减少GC分配，适合频繁调用。</param>
            /// <returns>所有匹配的节点列表。</returns>
            public static List<Transform> FindAllChildrenByLayer(Transform me, int layer, bool includeSelf = false, List<Transform> results = null)
            {
                var list = results ?? new List<Transform>(4);
                if (me == null) return list;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                if (includeSelf) queue.Enqueue(me);
                else
                {
                    for (int i = 0; i < me.childCount; i++)
                    {
                        queue.Enqueue(me.GetChild(i));
                    }
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.gameObject.layer == layer) list.Add(current);
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return list;
            }

            /// <summary>
            /// 在子节点中查找所有在指定层级掩码中的 Transform。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回空列表。</param>
            /// <param name="layerMask">要查找的层级掩码。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <param name="results">可选的输出列表。传入列表可减少GC分配，适合频繁调用。</param>
            /// <returns>所有匹配的节点列表。</returns>
            public static List<Transform> FindAllChildrenInLayerMask(Transform me, LayerMask layerMask, bool includeSelf = false, List<Transform> results = null)
            {
                var list = results ?? new List<Transform>(4);
                if (me == null) return list;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                if (includeSelf) queue.Enqueue(me);
                else
                {
                    for (int i = 0; i < me.childCount; i++)
                    {
                        queue.Enqueue(me.GetChild(i));
                    }
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (((1 << current.gameObject.layer) & layerMask) != 0) list.Add(current);
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return list;
            }

            /// <summary>
            /// 在子节点中查找所有满足条件的 Transform。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回空列表。</param>
            /// <param name="condition">判断条件函数。为 null 时返回空列表。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <param name="results">可选的输出列表。传入列表可减少GC分配，适合频繁调用。</param>
            /// <returns>所有满足条件的节点列表。</returns>
            public static List<Transform> FindAllChildrenWhere(Transform me, Func<Transform, bool> condition, bool includeSelf = false, List<Transform> results = null)
            {
                var list = results ?? new List<Transform>(4);
                if (me == null || condition == null) return list;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                if (includeSelf) queue.Enqueue(me);
                else
                {
                    for (int i = 0; i < me.childCount; i++)
                    {
                        queue.Enqueue(me.GetChild(i));
                    }
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (condition(current)) list.Add(current);
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return list;
            }

            /// <summary>
            /// 获取所有子孙节点。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回空列表。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <param name="results">可选的输出列表。传入列表可减少GC分配，适合频繁调用。</param>
            /// <returns>所有子孙节点列表。</returns>
            public static List<Transform> GetAllChildren(Transform me, bool includeSelf = false, List<Transform> results = null)
            {
                var list = results ?? new List<Transform>(16);
                if (me == null) return list;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                queue.Enqueue(me);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (includeSelf || current != me) list.Add(current);
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return list;
            }

            /// <summary>
            /// 向上查找第一个层级匹配的父节点。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回 null。</param>
            /// <param name="layer">要查找的层级索引。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <returns>找到的第一个匹配父节点，未找到返回 null。</returns>
            public static Transform FindParentByLayer(Transform me, int layer, bool includeSelf = false)
            {
                if (me == null) return null;
                Transform t = includeSelf ? me : me.parent;
                while (t != null)
                {
                    if (t.gameObject.layer == layer) return t;
                    t = t.parent;
                }
                return null;
            }

            /// <summary>
            /// 向上查找第一个在指定层级掩码中的父节点。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回 null。</param>
            /// <param name="layerMask">要查找的层级掩码。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <returns>找到的第一个匹配父节点，未找到返回 null。</returns>
            public static Transform FindParentInLayerMask(Transform me, LayerMask layerMask, bool includeSelf = false)
            {
                if (me == null) return null;
                Transform t = includeSelf ? me : me.parent;
                while (t != null)
                {
                    if (((1 << t.gameObject.layer) & layerMask) != 0) return t;
                    t = t.parent;
                }
                return null;
            }

            /// <summary>
            /// 向上查找第一个满足条件的父节点。
            /// </summary>
            /// <param name="me">查找的起始节点。为 null 时返回 null。</param>
            /// <param name="condition">判断条件函数。为 null 时返回 null。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <returns>找到的第一个满足条件的父节点，未找到返回 null。</returns>
            public static Transform FindParentWhere(Transform me, Func<Transform, bool> condition, bool includeSelf = false)
            {
                if (me == null || condition == null) return null;
                Transform t = includeSelf ? me : me.parent;
                while (t != null)
                {
                    if (condition(t)) return t;
                    t = t.parent;
                }
                return null;
            }

            /// <summary>
            /// 在子节点中查找第一个包含指定组件的 Transform。
            /// </summary>
            /// <typeparam name="T">要查找的组件类型。</typeparam>
            /// <param name="me">查找的起始节点。为 null 时返回 null。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <returns>找到的第一个包含组件的节点，未找到返回 null。</returns>
            public static Transform FindChildWithComponent<T>(Transform me, bool includeSelf = false) where T : Component
            {
                if (me == null) return null;
                if (includeSelf && me.GetComponent<T>() != null) return me;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                for (int i = 0; i < me.childCount; i++)
                {
                    queue.Enqueue(me.GetChild(i));
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.GetComponent<T>() != null) return current;
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return null;
            }

            /// <summary>
            /// 在子节点中查找所有包含指定组件的 Transform。
            /// </summary>
            /// <typeparam name="T">要查找的组件类型。</typeparam>
            /// <param name="me">查找的起始节点。为 null 时返回空列表。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <param name="results">可选的输出列表。传入列表可减少GC分配，适合频繁调用。</param>
            /// <returns>所有包含组件的节点列表。</returns>
            public static List<Transform> FindAllChildrenWithComponent<T>(Transform me, bool includeSelf = false, List<Transform> results = null) where T : Component
            {
                var list = results ?? new List<Transform>(4);
                if (me == null) return list;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                if (includeSelf) queue.Enqueue(me);
                else
                {
                    for (int i = 0; i < me.childCount; i++)
                    {
                        queue.Enqueue(me.GetChild(i));
                    }
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.GetComponent<T>() != null) list.Add(current);
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return list;
            }

            /// <summary>
            /// 在子节点中查找所有指定组件。
            /// </summary>
            /// <typeparam name="T">要查找的组件类型。</typeparam>
            /// <param name="me">查找的起始节点。为 null 时返回空列表。</param>
            /// <param name="includeSelf">是否包含自身节点。默认 false。</param>
            /// <param name="results">可选的输出列表。传入列表可减少GC分配，适合频繁调用。</param>
            /// <returns>所有找到的组件列表。</returns>
            public static List<T> GetAllComponents<T>(Transform me, bool includeSelf = false, List<T> results = null) where T : Component
            {
                var list = results ?? new List<T>(4);
                if (me == null) return list;

                // 使用BFS遍历
                var queue = new Queue<Transform>();
                if (includeSelf) queue.Enqueue(me);
                else
                {
                    for (int i = 0; i < me.childCount; i++)
                    {
                        queue.Enqueue(me.GetChild(i));
                    }
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var component = current.GetComponent<T>();
                    if (component != null) list.Add(component);
                    for (int i = 0; i < current.childCount; i++)
                    {
                        queue.Enqueue(current.GetChild(i));
                    }
                }
                return list;
            }

            #endregion
        
          
        }
    }
}

