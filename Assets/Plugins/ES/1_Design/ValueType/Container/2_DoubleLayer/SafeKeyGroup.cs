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
    /// <summary>
    /// 相比于KeyGroup , 键使用 SafeNormalList 进行存储和管理
    /// 适用于高频更新场景
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Element"></typeparam>
    [Serializable, TypeRegistryItem("安全键组")]
    public class SafeKeyGroup<Key, Element> : IKeyGroup<Key, Element>, ISafe
    {
        /// <summary>
        /// 是否在访问不存在的键时自动加入字典
        /// </summary>
        public bool _autoCreateOnAccess = true;

        [SerializeReference]
        [LabelText(@"@ Editor_ShowDes ", icon: SdfIconType.ListColumnsReverse), GUIColor("Editor_ShowColor")]
        public Dictionary<Key, SafeNormalList<Element>> Groups = new Dictionary<Key, SafeNormalList<Element>>();
        public readonly static SafeNormalList<Element> NULL = new SafeNormalList<Element>();
        #region 编辑器专属
        public virtual string Editor_ShowDes => "键组";
        public virtual Color Editor_ShowColor => Color.yellow;
        #endregion

        public bool AutoApplyBuffers { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; } = true;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAutoApplyBuffers(bool b) => AutoApplyBuffers = b;
        public void ApplyBuffers()
        {

            foreach (var (i, k) in Groups)
            {
                k.ApplyBuffers();
            }
        }
        public static SafeNormalList<Element> TryAddInternal(SafeNormalList<Element> list, Element e)
        {
            list.Add(e);
            return list;
        }
        public static SafeNormalList<Element> AddRangeInternal(SafeNormalList<Element> list, IEnumerable<Element> es)
        {
            list.AddRange(es);
            return list;
        }
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
                Groups.Add(k, TryAddInternal(new SafeNormalList<Element>(), e));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddAndBackIsNewList(Key k, Element e)
        {

            if (e == null) return false;
            if (Groups.TryGetValue(k, out var list))
            {
                list.Add(e);
                return false;
            }
            else
            {
                Groups.Add(k, TryAddInternal(new SafeNormalList<Element>(), e));
                return true;
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
                Groups.Add(k, AddRangeInternal(new SafeNormalList<Element>(), es));
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
                if (AutoApplyBuffers) list.ApplyBuffers();
                return list;
            }
            if (_autoCreateOnAccess)
            {
                var newList = new SafeNormalList<Element>();
                Groups.Add(key, newList);
                return newList;
            }
            return NULL;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SafeNormalList<Element> GetGroupDirectly(Key key, bool applyBuffer = true)
        {
            if (Groups.TryGetValue(key, out var list))
            {
                if (applyBuffer) list.ApplyBuffers();
                return list;
            }
            if (_autoCreateOnAccess)
            {
                var newList = new SafeNormalList<Element>();
                Groups.Add(key, newList);
                return newList;
            }
            return NULL;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryContains(Key key, Element who)
        {
            if (Groups.TryGetValue(key, out var list))
            {
                return Enumerable.Contains(list, who);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            foreach (var (i, k) in Groups)
            {
                k.Clear();
            }
            Groups.Clear();
        }

        public void SetAutoCreateOnAccess(bool autoCreate)
        {
            _autoCreateOnAccess = autoCreate;
        }
    }

    [Serializable, TypeRegistryItem("类型匹配安全键组")/*类型全匹配安全列表*/]
    public class SafeTypeMatchKeyGroup<Element> : SafeKeyGroup<Type, Element>
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
                list.ApplyBuffers();
                int count = list.ValuesNow.Count;
                for (int i = 0; i < count; i++)
                {
                    if ( list.ValuesNow[i] is T t)
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

