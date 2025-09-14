using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /*
       三层容器 
     */
    [Serializable, TypeRegistryItem("标准选组 键值对")]
    public class SelectDic<Select, Key, Element> : ISelectDic<Select, Key, Element>
    {
        [SerializeReference]
        [LabelText(@"@  Editor_ShowDes ", icon: SdfIconType.ListColumnsReverse), GUIColor("Editor_ShowColor")]
        public Dictionary<Select, Dictionary<Key, Element>> Contents = new Dictionary<Select, Dictionary<Key, Element>>(3);

        public static readonly Dictionary<Key, Element> NULL = new Dictionary<Key, Element>();
        #region 编辑器
        public virtual string Editor_ShowDes => "标准选择键值对";
        public virtual Color Editor_ShowColor => Color.yellow;
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAddOrSet(Select t, Key k, Element e)
        {
            if (e == null) return;
            if (Contents.TryGetValue(t, out var dic))
            {
                dic[k] = e;
            }
            else
            {
                Contents.Add(t, new Dictionary<Key, Element>(3) { { k, e } });
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAddOnly(Select t, Key k, Element e)
        {
            if (e == null) return;
            if (Contents.TryGetValue(t, out var dic))
            {
                if (Contents.TryGetValue(t, out _))
                {

                }
                else
                {
                    dic.Add(k, e);
                }
            }
            else
            {
                Contents.Add(t, new Dictionary<Key, Element>(3) { { k, e } });
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemove(Select t, Key k)
        {
            if (Contents.TryGetValue(t, out var dic))
            {
                dic.Remove(k);
            }
            else
            {
#if UNITY_EDITOR
                throw new Exception("IOC没有这种键");
#endif
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Element GetElement(Select t, Key k, Element ifError = default)
        {
            if (Contents.TryGetValue(t, out var dic))
            {
                if (dic.TryGetValue(k, out var value))
                {
                    return value;
                }
            }
            return ifError;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Dictionary<Key, Element> GetDic(Select k)
        {
            if (Contents.TryGetValue(k, out var dic))
            {
                return dic;
            }
            return NULL;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAddOrSetRange(Select t, IEnumerable<KeyValuePair<Key, Element>> keyValues)
        {
            if (Contents.TryGetValue(t, out var dic))
            {
                foreach (var kv in keyValues)
                {
                    dic[kv.Key] = kv.Value;
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAddRangeOnly(Select t, IEnumerable<KeyValuePair<Key, Element>> keyValues)
        {
            if (Contents.TryGetValue(t, out var dic))
            {
                foreach (var (k, v) in keyValues)
                {
                    if (dic.TryGetValue(k, out _))
                    {

                    }
                    else
                    {
                        dic.Add(k, v);
                    }
                }
            }
        }
        public void TryRemoveRange(Select t, IEnumerable<Key> keys)
        {
            if (keys != null)
                if (Contents.TryGetValue(t, out var dic))
                {
                    foreach (var i in keys)
                    {
                        dic.Remove(i);
                    }
                }
                else
                {
#if UNITY_EDITOR
                    throw new Exception("IOC没有这种键");
#endif
                }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearSelect(Select t)
        {
            if (Contents.TryGetValue(t, out var dic))
            {
                dic.Clear();
            }
            else
            {
#if UNITY_EDITOR
                throw new Exception("没有这种Select");
#endif
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            foreach (var (i, k) in Contents)
            {
                k.Clear();
            }
            Contents.Clear();
        }
    }

    [Serializable, TypeRegistryItem("字符串选组 键值对")]
    public class SelectDic_StringKey<TypeSelect_, Element> : SelectDic<TypeSelect_, string, Element>
    {

    }
    [Serializable, TypeRegistryItem("字符串选组/键 值")]
    public class SelectDic_StringSelectAndKey<Element> : SelectDic<string, string, Element>
    {

    }
    [Serializable, TypeRegistryItem("字符串(分组+键) 映射至Type")]
    public class SelectDic_StringsToType : SelectDic_StringSelectAndKey<Type>
    {

    }
}

