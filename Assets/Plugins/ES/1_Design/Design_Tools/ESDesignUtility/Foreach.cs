using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {
        /// <summary>
        /// 一组用于在 Transform 层级中查找/遍历的工具方法。
        /// 这些方法提供按名称、标签或 Layer 在子孙或祖先方向进行查找，并包含若干获取组件的便捷方法。
        /// </summary>
        /// <remarks>
        /// 设计注意：
        /// - 方法多数为返回第一个匹配项（short-circuit）。若需返回所有匹配项，请实现相应的 "FindAll" 变体。
        /// - 某些方法会递归遍历 Transform 层级，深/广层级可能带来性能开销；若在性能敏感路径建议使用缓存或非递归实现。
        /// - 对于标签查找，使用 <see cref="GameObject.CompareTag(string)"/> 时，传入的标签必须已在 Unity 的 Tag 管理器中注册，否则可能抛异常。
        /// - 对于名称查找，方法同时尝试使用 <see cref="Transform.Find(string)"/>（支持子路径）与递归匹配单一名称，行为在注释中说明。
        /// </remarks>
        public static class Foreach
        {
            /// <summary>
            /// 在指定 Transform 的子孙（递归）中查找第一个名称匹配的 Transform。
            /// 方法首先尝试使用 <see cref="Transform.Find(string)"/>（支持相对路径，例如 "A/B"），若未找到则以深度优先方式递归匹配子节点的 <c>name</c> 字段。
            /// </summary>
            /// <param name="me">起始 Transform（查找范围的根）。若为 <c>null</c> 将返回 <c>null</c>。</param>
            /// <param name="name">要匹配的名称（严格相等）。禁止为 <c>null</c>。</param>
            /// <param name="includeself">是否包括自身在内进行匹配（默认 false）。</param>
            /// <returns>若找到匹配的 <see cref="Transform"/> 则返回该实例；否则返回 <c>null</c>。</returns>
            /// <example>
            /// 示例：
            /// <code>
            /// var t = Foreach.FindChildTransformByName(parentTransform, "Item01", true);
            /// </code>
            /// </example>
            public static Transform FindChildTransformByName(Transform me, string name, bool includeself = false)
            {
                if (me == null) return null;
                if (includeself && me.gameObject.name == name) return me;
                if (me.childCount == 0) return null;
                Transform find = me.Find(name);
                if (find != null) return find;
                int all = me.childCount;
                for (int i = 0; i < all; i++)
                {
                    var child = me.GetChild(i);
                    find = FindChildTransformByName(child, name, false);
                    if (find != null)
                    {
                        return find;
                    }
                }
                return null;
            }
            /// <summary>
            /// 在指定 Transform 的子孙（递归）中查找第一个具有指定标签的 Transform。
            /// </summary>
            /// <param name="me">起始 Transform（查找范围的根）。若为 <c>null</c> 将返回 <c>null</c>。</param>
            /// <param name="tag">要匹配的标签（必须是已注册的 Tag 名称）。</param>
            /// <param name="includeself">是否包括自身在内进行匹配（默认 false）。</param>
            /// <returns>返回第一个匹配标签的 Transform，未找到返回 <c>null</c>。</returns>
            public static Transform FindChildTransformByTag(Transform me, string tag, bool includeself = false)
            {
                if (me == null) return null;
                if (includeself && me.gameObject.CompareTag(tag)) return me;
                if (me.childCount == 0) return null;
                int all = me.childCount;
                for (int i = 0; i < all; i++)
                {
                    var child = me.GetChild(i);
                    if (child.CompareTag(tag))
                    {
                        return child;
                    }
                    var childFind = FindChildTransformByTag(child, tag, false);
                    if (childFind != null)
                    {
                        return childFind;
                    }
                }
                return null;
            }
            /// <summary>
            /// 在指定 Transform 的子孙（递归）中查找第一个处于指定 Layer 的 Transform。
            /// </summary>
            /// <param name="me">起始 Transform（查找范围的根）。若为 <c>null</c> 将返回 <c>null</c>。</param>
            /// <param name="layer">要匹配的 Layer 索引。</param>
            /// <param name="includeself">是否包括自身在内进行匹配（默认 false）。</param>
            /// <returns>返回第一个匹配 Layer 的 Transform，未找到返回 <c>null</c>。</returns>
            public static Transform FindChildTransformByLayer(Transform me, int layer, bool includeself = false)
            {
                if (me == null) return null;
                if (includeself && me.gameObject.layer == layer) return me;
                if (me.childCount == 0) return null;
                int all = me.childCount;
                for (int i = 0; i < all; i++)
                {
                    var child = me.GetChild(i);
                    if (child.gameObject.layer == layer)
                    {
                        return child;
                    }
                    var childFind = FindChildTransformByLayer(child, layer, false);
                    if (childFind != null)
                    {
                        return childFind;
                    }
                }
                return null;
            }
            /// <summary>
            /// 向上沿父节点链（祖先方向）查找第一个名称匹配的 Transform。
            /// </summary>
            /// <param name="me">起始 Transform（从此节点往父方向查找）。若为 <c>null</c> 将返回 <c>null</c>。</param>
            /// <param name="name">要匹配的名称（严格相等）。</param>
            /// <param name="includeself">是否包括自身在内进行匹配（默认 false）。</param>
            /// <returns>返回匹配的祖先 Transform 或 <c>null</c>。</returns>
            public static Transform FindParentTransformByName(Transform me, string name, bool includeself = false)
            {
                if (me == null) return null;
                Transform t = includeself ? me : me.parent;
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
            /// 向上沿父节点链（祖先方向）查找第一个具有指定标签的 Transform。
            /// </summary>
            /// <param name="me">起始 Transform（从此节点往父方向查找）。若为 <c>null</c> 将返回 <c>null</c>。</param>
            /// <param name="tag">要匹配的标签名称。</param>
            /// <param name="includeself">是否包括自身在内进行匹配（默认 false）。</param>
            /// <returns>返回匹配的祖先 Transform 或 <c>null</c>。</returns>
            public static Transform FindParentTransformByTag(Transform me, string tag, bool includeself = false)
            {
                if (me == null) return null;
                Transform t = includeself ? me : me.parent;
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
            /// 向上沿父节点链（祖先方向）查找第一个属于指定 Layer 的 Transform。
            /// </summary>
            /// <param name="me">起始 Transform（从此节点往父方向查找）。若为 <c>null</c> 将返回 <c>null</c>。</param>
            /// <param name="layer">要匹配的 Layer 索引。</param>
            /// <param name="includeself">是否包括自身在内进行匹配（默认 false）。</param>
            /// <returns>返回匹配的祖先 Transform 或 <c>null</c>。</returns>
            public static Transform FindParentTransformByLayer(Transform me, int layer, bool includeself = false)
            {
                if (me == null) return null;
                Transform t = includeself ? me : me.parent;
                while (t != null)
                {
                    if (t.gameObject.layer == layer)
                    {
                        return t;
                    }
                    t = t.parent;
                }
                return null;
            }
            /// <summary>
            /// 在指定 Transform 的子孙中查找具有给定名称的 Transform，并返回该 Transform 上的组件 <typeparamref name="T"/>。
            /// 若未找到匹配的 Transform，可通过 <paramref name="NotMatchGet"/> 控制是否回退到调用 <see cref="Component.GetComponentInChildren{T}"/> 来查找任意子孙中的组件。
            /// </summary>
            /// <typeparam name="T">要查找的组件类型，必须继承自 <see cref="Component"/>。</typeparam>
            /// <param name="me">起始 Transform（查找范围的根）。若为 <c>null</c> 将返回 <c>null</c>。</param>
            /// <param name="name">要匹配的 Transform 名称。</param>
            /// <param name="includeself">是否包括自身在内进行匹配（默认 false）。</param>
            /// <param name="NotMatchGet">若为 <c>true</c> 且未找到指定名称的 Transform，则回退到在任意子孙中查找第一个匹配组件。</param>
            /// <returns>找到的组件实例或 <c>null</c>。</returns>
            public static T FindComponentInChildrenWithName<T>(Transform me, string name, bool includeself = false, bool NotMatchGet = false) where T : Component
            {
                if (me == null) return null;
                Transform target = FindChildTransformByName(me, name, includeself);
                if (target != null)
                {
                    return target.GetComponent<T>();
                }
                return NotMatchGet ? me.GetComponentInChildren<T>() : null;
            }
            /// <summary>
            /// 在指定 Transform 的子孙中查找第一个带有指定标签的 Transform，并返回其上的组件 <typeparamref name="T"/>。
            /// 若未匹配到标签，可通过 <paramref name="NotMatchGet"/> 回退到在任意子孙中查找组件。
            /// </summary>
            /// <typeparam name="T">要查找的组件类型，必须继承自 <see cref="Component"/>。</typeparam>
            /// <param name="me">起始 Transform（查找范围的根）。若为 <c>null</c> 将返回 <c>null</c>。</param>
            /// <param name="tag">要匹配的标签名称（需在 Unity 的 Tag 管理器中注册）。</param>
            /// <param name="includeself">是否包括自身在内进行匹配（默认 false）。</param>
            /// <param name="NotMatchGet">若为 <c>true</c> 且未找到指定标签的 Transform，则回退到在任意子孙中查找第一个匹配组件。</param>
            /// <returns>找到的组件实例或 <c>null</c>。</returns>
            public static T FindComponentInChildrenByTag<T>(Transform me, string tag, bool includeself = false, bool NotMatchGet = false) where T : Component
            {
                if (me == null) return null;
                Transform target = FindChildTransformByTag(me, tag, includeself);
                if (target != null)
                {
                    return target.GetComponent<T>();
                }
                return NotMatchGet ? me.GetComponentInChildren<T>() : null;
            }
        }
    }
}

