using System;
using System.Collections;
using System.Collections.Generic;

namespace ES
{
    /// <summary>
    /// 高性能集合：List + indexMap（swap-back remove），支持 O(1) Add/Remove/Contains，迭代为连续内存。
    /// 注意：不要直接修改 Items 列表，否则会破坏索引映射。
    /// </summary>
    public sealed class SwapBackSet<T> : IEnumerable<T>
    {
        private readonly List<T> _items;
        private readonly Dictionary<T, int> _indexMap;

        public int Count => _items.Count;

        public List<T> Items => _items;

        public SwapBackSet(int capacity = 16, IEqualityComparer<T> comparer = null)
        {
            if (capacity < 0) capacity = 0;
            _items = new List<T>(capacity);
            _indexMap = new Dictionary<T, int>(capacity, comparer ?? EqualityComparer<T>.Default);
        }

        public bool Contains(T item)
        {
            return item != null && _indexMap.ContainsKey(item);
        }

        public bool Add(T item)
        {
            if (item == null) return false;
            if (_indexMap.ContainsKey(item)) return false;

            int index = _items.Count;
            _items.Add(item);
            _indexMap.Add(item, index);
            return true;
        }

        public bool Remove(T item)
        {
            if (item == null) return false;
            if (!_indexMap.TryGetValue(item, out int index)) return false;

            int lastIndex = _items.Count - 1;
            var lastItem = _items[lastIndex];

            _items.RemoveAt(lastIndex);
            _indexMap.Remove(item);

            if (index != lastIndex)
            {
                _items[index] = lastItem;
                _indexMap[lastItem] = index;
            }

            return true;
        }

        public void Clear()
        {
            _items.Clear();
            _indexMap.Clear();
        }

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
