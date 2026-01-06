using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /* 键-组 值为类型列表 
    通常没有 遍历需求，就算有 也是低频事件式
     */
    [Serializable, TypeRegistryItem("键组")]
    public class KeyGroup<Key, Element> : IKeyGroup<Key, Element>
    {
        /// <summary>
        /// 是否在访问不存在的键时自动加入字典
        /// </summary>
        public bool _autoCreateOnAccess = true;


        [SerializeReference]
        [LabelText(@"@ Editor_ShowDes ", icon: SdfIconType.ListColumnsReverse), GUIColor("Editor_ShowColor")]
        public Dictionary<Key, List<Element>> Groups = new Dictionary<Key, List<Element>>();
        public readonly static List<Element> NULL = new List<Element>();


        #region 编辑器专属
        public virtual string Editor_ShowDes => "键组";
        public virtual Color Editor_ShowColor => Color.yellow;
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Key k, Element e)
        {

            if (e == null) return;
            if (Groups.TryGetValue(k, out var list))
            {
                list.Add(e);
            }
            else
            {
                Groups.Add(k, new List<Element>() { e });
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(Key k, Element e)
        {
            if (Groups.TryGetValue(k, out var list))
            {
                list.Remove(e);
            }


#if UNITY_EDITOR
            else
                throw new Exception("KeyGroup没有这种键");
#endif

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(Key k, IEnumerable<Element> es)
        {
            if (es == null) return;
            if (Groups.TryGetValue(k, out var list))
            {
                list.AddRange(es);
            }
            else
            {
                Groups.Add(k, es.ToList());
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(Key k, IEnumerable<Element> es)
        {
            if (es == null) return;
            if (Groups.TryGetValue(k, out var list))
            {
                foreach (var i in es)
                {
                    list.Remove(i);
                }
            }

#if UNITY_EDITOR
            else
                throw new Exception("KeyGroup没有这种键");
#endif
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<Element> GetGroupAsIEnumable(Key key)
        {
            if (Groups.TryGetValue(key, out var list))
            {
                return list;
            }
            if (_autoCreateOnAccess)
            {
                var newList = new List<Element>();
                Groups.Add(key, newList);
                return newList;
            }
            return NULL;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<Element> GetGroupDirectly(Key key)
        {
            if (Groups.TryGetValue(key, out var list))
            {
                return list;
            }
            if (_autoCreateOnAccess)
            {
                var newList = new List<Element>();
                Groups.Add(key, newList);
                return newList;
            }
            return NULL;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<T> GetGroup<T>(Key key)
        {
            if (Groups.TryGetValue(key, out var list))
            {
                var users = new List<T>();
                var len = list.Count;
                for (int i = 0; i < len; i++)
                {
                    if (list[i] is T t)
                    {
                        users.Add(t);
                    }
                }
                return users;
            }
            return null;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryContains(Key key, Element who)
        {
            if (Groups.TryGetValue(key, out var list))
            {
                return list.Contains(who);
            }
            return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearGroup(Key key)
        {
            if (Groups.TryGetValue(key, out var list))
            {
                list.Clear();
            }
        }

        public void Clear()
        {
            foreach (var (i, k) in Groups)
            {
                k.Clear();
            }
            Groups.Clear();
        }
        public string ToStringAllContent()
        {
            string s = "KeyGroup ：{  ";
            var keys = Groups.Keys;
            foreach (var i in keys)
            {
                var group = GetGroupDirectly(i);
                string onegroup = "Group : " + i + "\n{";
                if (group != null)
                {
                    foreach (var item in group)
                    {
                        onegroup += item.ToString();
                    }
                }
                onegroup += "}\n";
                s += onegroup;
            }
            return s += "}";
        }
        public void SetAutoCreateOnAccess(bool autoCreate)
        {
            _autoCreateOnAccess = autoCreate;
        }
    }

    [Serializable, TypeRegistryItem("类型键-组")]
    public class TypeMatchKeyGroup<Element> : KeyGroup<Type, Element>
    {

        public override string Editor_ShowDes => "类型匹配 键组";
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(T e) where T : Element
        {
            Add(typeof(T), e);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>(T e) where T : Element
        {
            Remove(typeof(T), e);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<Element> GetGroupAsIEnumable<T>() where T : Element
        {
            return base.GetGroupAsIEnumable(typeof(T));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<T> GetNewGroupOfType<T>()
        {
            var listR = new List<T>(3);
            if (Groups.TryGetValue(typeof(T), out var list))
            {
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    if (list[i] is T t)
                    {
                        listR.Add(t);
                    }
                }
                return listR;
            }
            return null;
        }

    }



}

