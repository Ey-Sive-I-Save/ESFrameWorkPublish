using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 多键双向字典：允许多个键映射到同一个值，支持通过值快速获取所有键
    /// </summary>
    [Serializable]
    public class MultiKeyDictionary<TKey, TValue> : IDictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<KeyValuePairInternal> seri_items = new List<KeyValuePairInternal>();

        private Dictionary<TKey, TValue> forward = new Dictionary<TKey, TValue>();
        private Dictionary<TValue, HashSet<TKey>> reverse = new Dictionary<TValue, HashSet<TKey>>();

        [Serializable]
        public struct KeyValuePairInternal
        {
            public TKey key;
            public TValue value;
            public KeyValuePairInternal(TKey key, TValue value)
            {
                this.key = key;
                this.value = value;
            }
        }

        #region 核心操作

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TKey key, TValue value)
        {
            if (key == null || value == null) return;
            // 如果键已存在，覆盖并维护反向映射
            if (forward.TryGetValue(key, out var oldValue))
            {
                Remove(key);
            }
            forward[key] = value;
            if (!reverse.TryGetValue(value, out var set))
            {
                set = new HashSet<TKey>();
                reverse[value] = set;
            }
            set.Add(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TKey key)
        {
            if (forward.TryGetValue(key, out var value))
            {
                forward.Remove(key);
                if (reverse.TryGetValue(value, out var set))
                {
                    set.Remove(key);
                    if (set.Count == 0) reverse.Remove(value);
                }
                return true;
            }
            return false;
        }

        /// <summary> 删除指定值下的所有键映射 </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveByValue(TValue value)
        {
            if (reverse.TryGetValue(value, out var keys))
            {
                foreach (var key in keys)
                    forward.Remove(key);
                reverse.Remove(value);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            forward.Clear();
            reverse.Clear();
        }

        /// <summary> 获取指定值对应的所有键（若不存在返回空数组）</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TKey[] GetKeysByValue(TValue value)
        {
            if (reverse.TryGetValue(value, out var set))
            {
                var arr = new TKey[set.Count];
                set.CopyTo(arr);
                return arr;
            }
            return Array.Empty<TKey>();
        }

        /// <summary> 尝试获取指定值对应的第一个键（若无返回 default）</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TKey GetFirstKeyByValue(TValue value)
        {
            if (reverse.TryGetValue(value, out var set) && set.Count > 0)
            {
                foreach (var key in set) return key;
            }
            return default;
        }

        #endregion

        #region 索引器与查询

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => forward.TryGetValue(key, out var v) ? v : default;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Add(key, value);
        }

        public ICollection<TKey> Keys => forward.Keys;
        public ICollection<TValue> Values => forward.Values;
        public int Count => forward.Count;
        public bool IsReadOnly => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key) => forward.ContainsKey(key);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsValue(TValue value) => reverse.ContainsKey(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value) => forward.TryGetValue(key, out value);

        #endregion

        #region IDictionary 接口实现（显式）

        void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => Add(key, value);
        bool IDictionary<TKey, TValue>.Remove(TKey key) => Remove(key);
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
        public bool Contains(KeyValuePair<TKey, TValue> item) => forward.TryGetValue(item.Key, out var v) && Equals(v, item.Value);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)forward).CopyTo(array, arrayIndex);
        public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => forward.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region 序列化

        public void OnBeforeSerialize()
        {
            seri_items.Clear();
            foreach (var kvp in forward)
                seri_items.Add(new KeyValuePairInternal(kvp.Key, kvp.Value));
        }

        public void OnAfterDeserialize()
        {
            forward.Clear();
            reverse.Clear();
            foreach (var item in seri_items)
            {
                if (item.key != null && item.value != null)
                {
                    Add(item.key, item.value);
                }
            }
        }

        #endregion
    }
}