using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    #region  接口
    public interface ISafe
    {
        public void ApplyBuffers();//不强制更新
        public bool AutoApplyBuffers { get; set; }
        public void SetAutoApplyBuffers(bool b);
    }
    //安全列表 接口
    public interface ISafeList<T> : IEnumerable<T>, ISafe
    {

        public void Add(T add);
        public void Remove(T remove);
        public bool Contains(T who);
        public void ApplyBuffers(bool force = false);//可选强制更新
        public void Clear();

    }

    //键-组 Key分组 T为元素类型 (不一定安全)，因为很多没有更新需求
    public interface IKeyGroup<K, Element>
    {
        public void Add(K key, Element add);

        public void Remove(K key, Element remove);

        public void AddRange(K key, IEnumerable<Element> adds);
        public void RemoveRange(K key, IEnumerable<Element> removes);
        public bool TryContains(K key, Element who);
        public IEnumerable<Element> GetGroupAsIEnumable(K key);
        public void ClearGroup(K key);
        public void Clear();

    }

    //选择 键值对 就是带分类的键值对罢了
    public interface ISelectDic<Select, K, Element>
    {
        public void AddOrSet(Select select, K key, Element add);

        public void Remove(Select select, K key);//移除只需要寻键
        public bool TryRemoveRange(Select select, IEnumerable<K> keys);
        public void ClearSelect(Select select);
        public void Clear();
    }
    #endregion

    #region  抽象
    // 抽象基类：统一 SafeList 的公共成员签名（不实现缓冲逻辑）
    [Serializable]
    public abstract class BaseSafeList<T> : ISafeList<T>
    {
        // 自动应用缓冲
        public bool AutoApplyBuffers { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; } = true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAutoApplyBuffers(bool b) => AutoApplyBuffers = b;

        // 具有缓冲的集合需要提供：
        protected abstract IEnumerable<T> _Internal_ValuesIEnumable { get; }
        public abstract void Add(T add);
        public abstract void Remove(T remove);
        public abstract bool Contains(T who);
        public abstract void ApplyBuffers(bool force = false);
        
        public abstract void Clear();

        // 默认的无参 ApplyBuffers 调用（可被子类重写）
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void ApplyBuffers() => ApplyBuffers(false);

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            if (AutoApplyBuffers) ApplyBuffers();
            return _Internal_ValuesIEnumable.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            if (AutoApplyBuffers) ApplyBuffers();
            return _Internal_ValuesIEnumable.GetEnumerator();
        }
    }
    #endregion
}

