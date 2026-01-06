using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 双向字典，支持通过键或值快速查找
    /// </summary>
    [Serializable]
    public class BidirectionalDictionary<TKey, TValue> : IDictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<KeyValuePairInternal> seri_items = new List<KeyValuePairInternal>();
        private readonly Dictionary<TKey, TValue> forward = new Dictionary<TKey, TValue>();
        private readonly Dictionary<TValue, TKey> reverse = new();

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

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (forward.TryGetValue(key, out var value))
                {
                    return value;
                }
                return default;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (forward.TryGetValue(key, out var oldValue))
                {
                    reverse.Remove(oldValue);
                }
                forward[key] = value;
                reverse[value] = key;
            }
        }

        public ICollection<TKey> Keys => forward.Keys;
        public ICollection<TValue> Values => forward.Values;
        public int Count => forward.Count;
        public bool IsReadOnly => false;

        /// <summary>
        /// 通过值获取键
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TKey GetKey(TValue value)
        {
            if(reverse.TryGetValue(value, out var key))
            {
                return key;
            }
            return default;
        }

        /// <summary>
        /// 尝试通过值获取键
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetKey(TValue value, out TKey key)
        {
            return reverse.TryGetValue(value, out key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key)
        {
            return forward.ContainsKey(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsValue(TValue value)
        {
            return reverse.ContainsKey(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            return forward.TryGetValue(key, out value);
        }
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, TValue value)
        {
            if (key == null || value == null)
                return false;

            if (forward.ContainsKey(key))
                return false;  // ✅ 不报错，返回false
            if (reverse.ContainsKey(value))
                return false;  // ✅ 不报错，返回false

            forward.Add(key, value);
            reverse.Add(value, key);
            return true;  // ✅ 成功添加
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TKey key, TValue value)
        {

            if (key == null || value == null)
                return;

            if (forward.ContainsKey(key))
                return;
            if (reverse.ContainsKey(value))
                return;

            forward.Add(key, value);
            reverse.Add(value, key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(KeyValuePairInternal item)
        {
            Add(item.key, item.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TKey key)
        {
            if (forward.TryGetValue(key, out var value))
            {
                forward.Remove(key);
                reverse.Remove(value);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveByValue(TValue value)
        {
            if (reverse.TryGetValue(value, out var key))
            {
                reverse.Remove(value);
                forward.Remove(key);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return forward.TryGetValue(item.Key, out var value) &&
                   EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)forward).CopyTo(array, arrayIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (Contains(item))
            {
                return Remove(item.Key);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return forward.GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<KeyValuePair<TValue, TKey>> GetEnumeratorInverse()
        {
            return reverse.GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // 序列化支持
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnBeforeSerialize()
        {
            seri_items.Clear();
            foreach (var kvp in forward)
            {
                seri_items.Add(new KeyValuePairInternal(kvp.Key, kvp.Value));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnAfterDeserialize()
        {
            forward.Clear();
            reverse.Clear();

            foreach (var item in seri_items)
            {
                if (item.key != null && item.value != null)
                {
                    // 避免重复键值
                    if (!forward.ContainsKey(item.key) && !reverse.ContainsKey(item.value))
                    {
                        forward.Add(item.key, item.value);
                        reverse.Add(item.value, item.key);
                    }
                }
            }
        }
    }
}
