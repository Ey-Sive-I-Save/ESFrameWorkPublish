using Unity.VisualScripting;
using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {
        //遍历/递归器
        public static class Foreach
        {
            /// <summary>
            /// 查询一个确定名字的子变换
            /// </summary>
            /// <param name="me">开始变换</param>
            /// <param name="name">查询名</param>
            /// <param name="includeself">包括自己</param>
            /// <returns></returns>
            public static Transform FindChildTransformByName(Transform me, string name, bool includeself = false)
            {
                if (me == null) return default;
                if (includeself && me.gameObject.name == name) return me;
                if (me.childCount == 0) return null;
                Transform find = me.Find(name);
                if (find != null) return find;
                int all = me.childCount;
                for (int i = 0; i < all; i++)
                {
                    find = FindChildTransformByName(me.GetChild(i), name);
                    if (find != null)
                    {
                        return find;
                    }
                }
                return default;
            }
            /// <summary>
            /// 查询一个确定标签的子变换
            /// </summary>
            /// <param name="me">开始变换</param>
            /// <param name="tag">查询标签</param>
            /// <param name="includeself">包括自己</param>
            /// <returns></returns>
            public static Transform FindChildTransformByTag(Transform me, string tag, bool includeself = false)
            {
                if (me == null) return default;
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
                    var childFind = FindChildTransformByTag(me.GetChild(i), tag);
                    if (childFind != null)
                    {
                        return childFind;
                    }
                }
                return default;
            }
            /// <summary>
              /// 查询一个确定层的子变换
              /// </summary>
              /// <param name="me">开始变换</param>
              /// <param name="layer">查询Layer</param>
              /// <param name="includeself">包括自己</param>
              /// <returns></returns>
            public static Transform FindChildTransformByLayer(Transform me, int layer, bool includeself = false)
            {
                if (me == null) return default;
                if (includeself && me.gameObject.layer == (layer)) return me;
                if (me.childCount == 0) return null;
                int all = me.childCount;
                for (int i = 0; i < all; i++)
                {
                    var child = me.GetChild(i);
                    if (child.gameObject.layer == (layer))
                    {
                        return child;
                    }
                    var childFind = FindChildTransformByLayer(me.GetChild(i), layer);
                    if (childFind != null)
                    {
                        return childFind;
                    }
                }
                return default;
            }
            /// <summary>
            /// 查询一个确定名字的父变换
            /// </summary>
            /// <param name="me">开始变换</param>
            /// <param name="name">查询名</param>
            /// <param name="includeself">包括自己</param>
            /// <returns></returns>
            public static Transform FindParentTransformByName(Transform me, string name, bool inculdeself = false)
            {
                Transform t = inculdeself ? me : me.parent;
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
            /// 查询一个确定标签的父变换
            /// </summary>
            /// <param name="me">开始变换</param>
            /// <param name="tag">查询标签</param>
            /// <param name="includeself">包括自己</param>
            /// <returns></returns>
            public static Transform FindParentTransformByTag(Transform me, string tag, bool inculdeself = false)
            {
                Transform t = inculdeself ? me : me.parent;
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
            /// 查询一个确定层的父变换
            /// </summary>
            /// <param name="me">开始变换</param>
            /// <param name="layer">查询Layer</param>
            /// <param name="includeself">包括自己</param>
            /// <returns></returns>
            public static Transform FindParentTransformByLayer(Transform me, int layer, bool inculdeself = false)
            {
                Transform t = inculdeself ? me : me.parent;
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
            /// 获得特定名字子对象身上的脚本
            /// </summary>
            /// <typeparam name="T">脚本类型</typeparam>
            /// <param name="me">开始变换</param>
            /// <param name="name">查询名</param>
            /// <param name="includeself">包括自己</param>
            /// <param name="NotMatchGet">如果不匹配，就忽略名字条件</param>
            /// <returns></returns>
            public static T FindComponentInChildrenWithName<T>(Transform me, string name, bool includeself = false, bool NotMatchGet = false) where T : Component
            {
                Transform target = FindChildTransformByName(me, name, includeself);
                if (target != null)
                {
                    return target.GetComponent<T>();
                }
                return NotMatchGet ? me.GetComponentInChildren<T>() : null;
            }
            /// <summary>
            /// 获得特定标签子对象身上的脚本
            /// </summary>
            /// <typeparam name="T">脚本类型</typeparam>
            /// <param name="me">开始变换</param>
            /// <param name="tag">查询标签</param>
            /// <param name="includeself">包括自己</param>
            /// <param name="NotMatchGet">如果不匹配，就忽略标签条件</param>
            /// <returns></returns>
            public static T FindComponentInChildrenByTag<T>(Transform me, string tag, bool includeself = false, bool NotMatchGet = false)   where T : Component
            {
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

