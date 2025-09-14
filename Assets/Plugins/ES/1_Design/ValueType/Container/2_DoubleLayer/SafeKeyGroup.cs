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
    [Serializable,TypeRegistryItem("安全键组")]
    public  class SafeKeyGroup<Key, Element> : IKeyGroup<Key, Element> ,ISafe
    {
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
        public void TryApplyBuffers()
        {
            foreach(var (i,k) in Groups)
            {
                k.ApplyBuffers();
            }
        }
        public static SafeNormalList<Element> TryAddInternal(SafeNormalList<Element> list, Element e)
        {
            list.TryAdd(e);
            return list;
        }
        public static SafeNormalList<Element> TryAddRangeInternal(SafeNormalList<Element> list, IEnumerable<Element> es)
        {
            list.TryAddRange(es);
            return list;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAdd(Key k, Element e)
        {

            if (e == null) return;
            if (Groups.TryGetValue(k, out var list))
            {
                list.TryAdd(e);
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
                list.TryAdd(e);
                return false;
            }
            else
            {
                Groups.Add(k, TryAddInternal(new SafeNormalList<Element>(), e));
                return true;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemove(Key k, Element e)
        {
            if (Groups.TryGetValue(k, out var list))
            {
                list.TryRemove(e);
            }


#if UNITY_EDITOR
            else
                throw new Exception("KeyGroup没有这种键");
#endif

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAddRange(Key k, IEnumerable<Element> es)
        {
            if (es == null) return;
            if (Groups.TryGetValue(k, out var list))
            {
                list.TryAddRange(es);
            }
            else
            {
                Groups.Add(k, TryAddRangeInternal(new SafeNormalList<Element>(), es));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemoveRange(Key k, IEnumerable<Element> es)
        {
            if (es == null) return;
            if (Groups.TryGetValue(k, out var list))
            {
                foreach (var i in es)
                {
                    list.TryRemove(i);
                }
            }

#if UNITY_EDITOR
            else
                throw new Exception("KeyGroup没有这种键");
#endif
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<Element> GetGroup(Key key)
        {
            if (Groups.TryGetValue(key, out var list))
            {
                if(AutoApplyBuffers)list.TryApplyBuffers();
                return list;
            }
            return NULL;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SafeNormalList<Element> GetGroupDirectly(Key key,bool applyBuffer=true)
        {
            if (Groups.TryGetValue(key, out var list))
            {
                if (applyBuffer) list.TryApplyBuffers();
                return list;
            }
            return NULL;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            foreach(var (i,k) in Groups)
            {
                k.Clear();
            }
            Groups.Clear();
        }
    }

    [Serializable, TypeRegistryItem("类型匹配安全键组")/*类型全匹配安全列表IOC*/]
    public class SafeTypeMatchKeyGroup<Element> : SafeKeyGroup<Type,Element>
    {
        public override string Editor_ShowDes => "类型匹配 键组";
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAdd<T>(T e) where T :Element
        {
            TryAdd(typeof(T), e);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemove<T>(T e) where T : Element
        {
            TryRemove(typeof(T),e);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<Element> GetMayStayGroup<T>(T e) where T : Element
        {
            return GetGroup(typeof(T));
        }
    }


   

}

