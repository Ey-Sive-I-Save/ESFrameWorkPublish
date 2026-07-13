using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 稳定句柄列表。
    /// 
    /// 设计目标：
    /// 1. 插入 / 删除不移动其他元素。
    /// 2. 删除后可复用空槽，避免持续扩容。
    /// 3. 通过 index + version 防止旧句柄误命中新对象。
    /// 4. 热路径不依赖 LINQ、yield、ToList 之类分配型写法。
    /// </summary>
    [Serializable, TypeRegistryItem("稳定ID列表")]
    public sealed class StableIdList<T> : IEnumerable<T>
    {
        /// <summary>
        /// 外部稳定句柄。
        /// index 指向槽位，version 用于识别槽位是否已被复用。
        /// </summary>
        [Serializable]
        public struct StableId : IEquatable<StableId>
        {
            public int Index;
            public int Version;

            public StableId(int index, int version)
            {
                Index = index;
                Version = version;
            }

            public bool IsValid => Index >= 0 && Version >= 0;

            public bool Equals(StableId other) => Index == other.Index && Version == other.Version;
            public override bool Equals(object obj) => obj is StableId other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    return (Index * 397) ^ Version;
                }
            }
            public override string ToString() => $"StableId(Index={Index}, Version={Version})";

            public static StableId Invalid => new StableId(-1, -1);
        }

        [Serializable]
        private struct Slot
        {
            public T Value;
            public int Version;
            public int NextFree;
            public bool Alive;
        }

        [NonSerialized] private List<Slot> _slots;
        [NonSerialized] private int _freeHead;
        [NonSerialized] private int _aliveCount;

        public StableIdList() : this(8) { }

        public StableIdList(int capacity)
        {
            _slots = capacity > 0 ? new List<Slot>(capacity) : new List<Slot>(8);
            _freeHead = -1;
            _aliveCount = 0;
        }

        /// <summary>当前存活元素数量。</summary>
        public int Count => _aliveCount;

        /// <summary>当前槽位总数（包含空槽）。</summary>
        public int SlotCount => _slots != null ? _slots.Count : 0;

        /// <summary>预分配容量，避免运行时扩容。</summary>
        public int Capacity => _slots != null ? _slots.Capacity : 0;

        /// <summary>是否存在可复用空槽。</summary>
        public bool HasFreeSlot => _freeHead >= 0;

        /// <summary>
        /// 放入元素并返回稳定句柄。
        /// 优先复用空槽；没有空槽时追加新槽。
        /// </summary>
        public StableId Add(T value)
        {
            EnsureRuntime();
            int index;
            Slot slot;

            if (_freeHead >= 0)
            {
                index = _freeHead;
                slot = _slots[index];
                _freeHead = slot.NextFree;
            }
            else
            {
                index = _slots.Count;
                slot = default;
                _slots.Add(slot);
            }

            slot.Value = value;
            slot.Alive = true;
            slot.NextFree = -1;
            _slots[index] = slot;

            _aliveCount++;
            return new StableId(index, slot.Version);
        }

        /// <summary>
        /// 通过稳定句柄删除元素。
        /// 旧句柄会因 version 变化而失效。
        /// </summary>
        public bool Remove(StableId id)
        {
            EnsureRuntime();
            if (!IsAlive(id)) return false;

            Slot slot = _slots[id.Index];
            slot.Value = default;
            slot.Alive = false;
            slot.Version++;
            slot.NextFree = _freeHead;
            _slots[id.Index] = slot;
            _freeHead = id.Index;

            _aliveCount--;
            return true;
        }

        /// <summary>清空所有元素，并重置空槽链。</summary>
        public void Clear()
        {
            EnsureRuntime();
            _slots.Clear();
            _freeHead = -1;
            _aliveCount = 0;
        }

        /// <summary>确保内部槽位容量足够。</summary>
        public void EnsureCapacity(int capacity)
        {
            EnsureRuntime();
            if (capacity > _slots.Capacity) _slots.Capacity = capacity;
        }

        /// <summary>判断句柄是否仍然有效。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlive(StableId id)
        {
            return IsAlive(id.Index, id.Version);
        }

        /// <summary>判断指定槽位与版本是否仍然有效。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlive(int index, int version)
        {
            EnsureRuntime();
            if ((uint)index >= (uint)_slots.Count) return false;
            Slot slot = _slots[index];
            return slot.Alive && slot.Version == version;
        }

        /// <summary>尝试读取句柄对应的值。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(StableId id, out T value)
        {
            EnsureRuntime();
            if (!IsAlive(id))
            {
                value = default;
                return false;
            }

            value = _slots[id.Index].Value;
            return true;
        }

        /// <summary>尝试直接按槽位读取值，适合调试或内部工具。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(int index, int version, out T value)
        {
            EnsureRuntime();
            if (!IsAlive(index, version))
            {
                value = default;
                return false;
            }

            value = _slots[index].Value;
            return true;
        }

        /// <summary>直接访问槽位中的值。调用方必须先确保句柄有效。</summary>
        public T GetUnsafe(StableId id)
        {
            EnsureRuntime();
            return _slots[id.Index].Value;
        }

        /// <summary>
        /// 遍历当前存活元素。
        /// 注意：这是便捷枚举，不用于极端热路径。
        /// </summary>
        public Enumerator GetEnumerator()
        {
            EnsureRuntime();
            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            EnsureRuntime();
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            EnsureRuntime();
            return new Enumerator(this);
        }

        private void EnsureRuntime()
        {
            if (_slots != null) return;
            _slots = new List<Slot>(8);
            _freeHead = -1;
            _aliveCount = 0;
        }

        public struct Enumerator : IEnumerator<T>
        {
            private readonly StableIdList<T> _owner;
            private int _index;
            private T _current;

            internal Enumerator(StableIdList<T> owner)
            {
                _owner = owner;
                _index = -1;
                _current = default;
            }

            public bool MoveNext()
            {
                while (++_index < _owner._slots.Count)
                {
                    var slot = _owner._slots[_index];
                    if (!slot.Alive) continue;
                    _current = slot.Value;
                    return true;
                }

                _current = default;
                return false;
            }

            public T Current => _current;
            object IEnumerator.Current => _current;
            public void Dispose() { }
            public void Reset() => _index = -1;
        }
    }
}
