using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 带过期时间的集合。
    /// 
    /// 设计目标：
    /// 1. 支持多 key 的短时有效状态。
    /// 2. 不强依赖 Time.time，调用方自行传入 now。
    /// 3. 提供懒清理和主动 Tick 两种清理方式。
    /// 4. 热路径避免 LINQ / ToList / 临时集合分配。
    /// </summary>
    [Serializable, TypeRegistryItem("定时集合")]
    public sealed class TimedSet<TKey>
    {
        [Serializable]
        private struct Entry
        {
            public float ExpireAt;
        }

        [NonSerialized] private Dictionary<TKey, Entry> _items;
        [NonSerialized] private List<TKey> _expiredBuffer;

        public TimedSet() : this(8) { }

        public TimedSet(int capacity)
        {
            _items = capacity > 0 ? new Dictionary<TKey, Entry>(capacity) : new Dictionary<TKey, Entry>(8);
            _expiredBuffer = new List<TKey>(capacity > 0 ? capacity : 8);
        }

        /// <summary>当前存储数量。可能包含尚未 Tick 或查询清理的过期项。</summary>
        public int Count => _items != null ? _items.Count : 0;

        /// <summary>是否没有任何存储项。</summary>
        public bool IsEmpty => Count == 0;

        /// <summary>
        /// 添加或刷新一个 key，过期时间 = now + duration。
        /// duration <= 0 时会被视为立即过期。
        /// </summary>
        public void Add(TKey key, float now, float duration)
        {
            EnsureRuntime();
            AddUntil(key, now + duration);
        }

        /// <summary>
        /// 添加或刷新一个 key，直接指定绝对过期时间。
        /// </summary>
        public void AddUntil(TKey key, float expireAt)
        {
            EnsureRuntime();
            _items[key] = new Entry { ExpireAt = expireAt };
        }

        /// <summary>
        /// 仅当 key 当前不存在或已过期时才添加。
        /// </summary>
        public bool TryAdd(TKey key, float now, float duration)
        {
            EnsureRuntime();
            if (Contains(key, now)) return false;
            Add(key, now, duration);
            return true;
        }

        /// <summary>
        /// 刷新一个已存在 key 的过期时间。
        /// 不存在则返回 false。
        /// </summary>
        public bool Refresh(TKey key, float now, float duration)
        {
            EnsureRuntime();
            if (!Contains(key, now)) return false;
            _items[key] = new Entry { ExpireAt = now + duration };
            return true;
        }

        /// <summary>
        /// 判断一个 key 当前是否有效。
        /// 若发现已过期，会顺手移除它。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(TKey key, float now)
        {
            EnsureRuntime();
            if (!_items.TryGetValue(key, out var entry)) return false;
            if (entry.ExpireAt > now) return true;

            _items.Remove(key);
            return false;
        }

        /// <summary>
        /// 获取剩余时间；若已过期会顺手移除。
        /// </summary>
        public bool TryGetRemaining(TKey key, float now, out float remaining)
        {
            EnsureRuntime();
            if (!_items.TryGetValue(key, out var entry))
            {
                remaining = 0f;
                return false;
            }

            remaining = entry.ExpireAt - now;
            if (remaining > 0f) return true;

            _items.Remove(key);
            remaining = 0f;
            return false;
        }

        /// <summary>
        /// 直接移除一个 key。
        /// </summary>
        public bool Remove(TKey key)
        {
            EnsureRuntime();
            return _items.Remove(key);
        }

        /// <summary>
        /// 扫描并清理所有已过期项。
        /// 这是 O(n)，适合低频 Tick 或调试场景。
        /// </summary>
        public int Tick(float now)
        {
            EnsureRuntime();
            if (_items.Count == 0) return 0;

            _expiredBuffer.Clear();
            foreach (var pair in _items)
            {
                if (pair.Value.ExpireAt <= now)
                {
                    _expiredBuffer.Add(pair.Key);
                }
            }

            int removed = 0;
            for (int i = 0; i < _expiredBuffer.Count; i++)
            {
                if (_items.Remove(_expiredBuffer[i]))
                {
                    removed++;
                }
            }

            _expiredBuffer.Clear();
            return removed;
        }

        /// <summary>清空全部元素。</summary>
        public void Clear()
        {
            EnsureRuntime();
            _items.Clear();
            _expiredBuffer.Clear();
        }

        /// <summary>
        /// 将当前有效 key 复制到外部列表。
        /// 用于调试面板、Inspector 或批处理扫描。
        /// </summary>
        public void CopyActiveKeysTo(List<TKey> buffer, float now)
        {
            EnsureRuntime();
            if (buffer == null) return;
            buffer.Clear();
            foreach (var pair in _items)
            {
                if (pair.Value.ExpireAt > now)
                {
                    buffer.Add(pair.Key);
                }
            }
        }

        /// <summary>
        /// 计算当前有效数量。该方法会扫描全部存储项，属于 O(n)。
        /// </summary>
        public int GetActiveCount(float now)
        {
            EnsureRuntime();
            int count = 0;
            foreach (var pair in _items)
            {
                if (pair.Value.ExpireAt > now) count++;
            }

            return count;
        }

        private void EnsureRuntime()
        {
            if (_items == null) _items = new Dictionary<TKey, Entry>(8);
            if (_expiredBuffer == null) _expiredBuffer = new List<TKey>(8);
        }
    }
}
