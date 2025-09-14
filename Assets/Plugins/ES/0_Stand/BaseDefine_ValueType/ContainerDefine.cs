using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    public interface ISafe
    {
        public void TryApplyBuffers();//不强制更新
        public bool AutoApplyBuffers { get; set; }
        public void SetAutoApplyBuffers(bool b);
    }
    //安全列表 接口
    public interface ISafeList<T> : IEnumerable<T>, ISafe
    {
        public IEnumerable<T> ValuesIEnumable { get; }
        public void TryAdd(T add);
        public void TryRemove(T remove);
        public bool TryContains(T who);
        public void ApplyBuffers(bool force = false);//可选强制更新
        public void Clear();
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            if (AutoApplyBuffers) TryApplyBuffers();
            return ValuesIEnumable.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            if (AutoApplyBuffers) TryApplyBuffers();
            return ValuesIEnumable.GetEnumerator();
        }
    }

    //键-组 Key分组 T为元素类型 (不一定安全)，因为很多没有更新需求
    public interface IKeyGroup<K, Element>
    {
        public void TryAdd(K key, Element add);

        public void TryRemove(K key, Element remove);

        public void TryAddRange(K key, IEnumerable<Element> adds);
        public void TryRemoveRange(K key, IEnumerable<Element> removes);
        public bool TryContains(K key, Element who);
        public IEnumerable<Element> GetGroup(K key);
        public void ClearGroup(K key);
        public void Clear();

    }

    //选择 键值对 就是带分类的键值对罢了
    public interface ISelectDic<Select, K, Element>
    {
        public void TryAddOrSet(Select select, K key, Element add);

        public void TryRemove(Select select, K key);//移除只需要寻键
        public void TryRemoveRange(Select select, IEnumerable<K> keys);
        public void ClearSelect(Select select);
        public void Clear();
    }
}

