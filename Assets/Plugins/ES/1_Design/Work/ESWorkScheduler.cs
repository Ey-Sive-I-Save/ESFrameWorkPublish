using System;
using System.Runtime.CompilerServices;

namespace ES
{
    [Serializable]
    public struct ESWorkHandle : IEquatable<ESWorkHandle>
    {
        public int id;
        public int version;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return id > 0 && version > 0; }
        }

        public ESWorkHandle(int id, int version)
        {
            this.id = id;
            this.version = version;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ESWorkHandle other)
        {
            return id == other.id && version == other.version;
        }

        public override bool Equals(object obj)
        {
            return obj is ESWorkHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (id * 397) ^ version;
            }
        }

        public static ESWorkHandle Invalid => new ESWorkHandle(0, 0);
    }

    /// <summary>
    /// 通用额度调度器。只负责三类额度数据、安全注册、移除、排序和更新期保护，不规定任务接口和执行方法。
    /// </summary>
    public sealed class ESWorkScheduler<TTask>
    {
        private struct Entry
        {
            public TTask task;
            public int order;
            public int sequence;
            public int id;
            public int version;
            public bool alive;
        }

        private Entry[] entries;
        private Entry[] pendingAdds;
        private int count;
        private int pendingAddCount;
        private bool isUpdating;
        private bool dirtySort;
        private bool dirtyCompact;
        private int nextId = 1;
        private int nextVersion = 1;
        private int nextSequence = 1;

        public int self;
        public int world;
        public int other;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return count; }
        }

        public bool IsUpdating
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return isUpdating; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ResetWork(int value = 100)
        {
            self = value;
            world = value;
            other = value;
            return HasWork;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ResetWork(int selfValue, int worldValue, int otherValue)
        {
            self = selfValue;
            world = worldValue;
            other = otherValue;
            return HasWork;
        }

        public bool HasWork
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return self > 0 || world > 0 || other > 0; }
        }

        public bool Reset(int value = 100)
        {
            isUpdating = false;
            ApplyChanges();
            isUpdating = true;
            return ResetWork(value);
        }

        public bool Reset(int selfValue, int worldValue, int otherValue)
        {
            isUpdating = false;
            ApplyChanges();
            isUpdating = true;
            return ResetWork(selfValue, worldValue, otherValue);
        }

        public void Warmup(int capacity, int pendingCapacity = 4)
        {
            if (capacity < 0) capacity = 0;
            if (pendingCapacity < 0) pendingCapacity = 0;

            if (entries == null || entries.Length < capacity)
                entries = new Entry[capacity];

            if (pendingAdds == null || pendingAdds.Length < pendingCapacity)
                pendingAdds = new Entry[pendingCapacity];
        }

        public void Clear()
        {
            if (entries != null && count > 0)
                Array.Clear(entries, 0, count);

            if (pendingAdds != null && pendingAddCount > 0)
                Array.Clear(pendingAdds, 0, pendingAddCount);

            count = 0;
            pendingAddCount = 0;
            isUpdating = false;
            dirtySort = false;
            dirtyCompact = false;
        }

        public ESWorkHandle Register(TTask task, int order)
        {
            int id = NextId();
            int version = NextVersion();
            Entry entry = new Entry
            {
                task = task,
                order = order,
                sequence = nextSequence++,
                id = id,
                version = version,
                alive = true
            };

            if (IsUpdating)
            {
                EnsurePendingCapacity(pendingAddCount + 1);
                pendingAdds[pendingAddCount++] = entry;
                return new ESWorkHandle(id, version);
            }

            AddEntry(entry);
            dirtySort = true;
            return new ESWorkHandle(id, version);
        }

        public bool Unregister(ESWorkHandle handle)
        {
            if (!handle.IsValid)
                return false;

            int index = IndexOf(handle);
            if (index < 0)
                return RemovePending(handle);

            ref Entry entry = ref entries[index];
            entry.alive = false;
            entry.task = default;
            entry.version = NextVersion();
            dirtyCompact = true;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldContinue()
        {
            return HasWork;
        }

        public void ApplyChanges()
        {
            if (isUpdating)
                return;

            if (pendingAddCount > 0)
            {
                EnsureEntryCapacity(count + pendingAddCount);
                for (int i = 0; i < pendingAddCount; i++)
                    AddEntry(pendingAdds[i]);

                Array.Clear(pendingAdds, 0, pendingAddCount);
                pendingAddCount = 0;
                dirtySort = true;
            }

            if (dirtyCompact)
                Compact();

            if (dirtySort)
                Sort();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAliveAt(int index)
        {
            return (uint)index < (uint)count && entries[index].alive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAlive(int index, out TTask task)
        {
            if ((uint)index < (uint)count && entries[index].alive)
            {
                task = entries[index].task;
                return true;
            }

            task = default;
            return false;
        }

        /// <summary>
        /// 固定表热路径读取。调用方需保证当前遍历期间不会移除活跃任务；动态移除场景请使用 TryGetAlive。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TTask Get(int index)
        {
            return entries[index].task;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TTask GetRef(int index)
        {
            return ref entries[index].task;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOrder(int index)
        {
            return entries[index].order;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AddEntry(Entry entry)
        {
            EnsureEntryCapacity(count + 1);
            int index = count++;
            entries[index] = entry;
            return index;
        }

        private void Compact()
        {
            int write = 0;
            for (int read = 0; read < count; read++)
            {
                if (!entries[read].alive)
                    continue;

                if (write != read)
                    entries[write] = entries[read];

                write++;
            }

            if (write < count)
                Array.Clear(entries, write, count - write);

            count = write;
            dirtyCompact = false;
        }

        private void Sort()
        {
            if (count > 1)
                Array.Sort(entries, 0, count, EntryOrderComparer.Instance);

            dirtySort = false;
        }

        private void EnsureEntryCapacity(int target)
        {
            if (entries == null)
            {
                entries = new Entry[target < 4 ? 4 : target];
                return;
            }

            if (entries.Length >= target)
                return;

            int next = entries.Length == 0 ? 4 : entries.Length * 2;
            if (next < target)
                next = target;

            Array.Resize(ref entries, next);
        }

        private void EnsurePendingCapacity(int target)
        {
            if (pendingAdds == null)
            {
                pendingAdds = new Entry[target < 4 ? 4 : target];
                return;
            }

            if (pendingAdds.Length >= target)
                return;

            int next = pendingAdds.Length == 0 ? 4 : pendingAdds.Length * 2;
            if (next < target)
                next = target;

            Array.Resize(ref pendingAdds, next);
        }

        private int NextVersion()
        {
            if (nextVersion == int.MaxValue)
                nextVersion = 1;

            return nextVersion++;
        }

        private int NextId()
        {
            if (nextId == int.MaxValue)
                nextId = 1;

            return nextId++;
        }

        private int IndexOf(ESWorkHandle handle)
        {
            for (int i = 0; i < count; i++)
            {
                Entry entry = entries[i];
                if (!entry.alive)
                    continue;

                if (entry.id == handle.id && entry.version == handle.version)
                    return i;
            }

            for (int i = 0; i < pendingAddCount; i++)
            {
                Entry entry = pendingAdds[i];
                if (entry.id == handle.id && entry.version == handle.version)
                    return -2;
            }

            return -1;
        }

        private bool RemovePending(ESWorkHandle handle)
        {
            for (int i = 0; i < pendingAddCount; i++)
            {
                Entry entry = pendingAdds[i];
                if (entry.id != handle.id || entry.version != handle.version)
                    continue;

                int last = pendingAddCount - 1;
                if (i != last)
                    pendingAdds[i] = pendingAdds[last];

                pendingAdds[last] = default;
                pendingAddCount--;
                return true;
            }

            return false;
        }

        private sealed class EntryOrderComparer : System.Collections.Generic.IComparer<Entry>
        {
            public static readonly EntryOrderComparer Instance = new EntryOrderComparer();

            public int Compare(Entry x, Entry y)
            {
                int orderCompare = x.order.CompareTo(y.order);
                return orderCompare != 0 ? orderCompare : x.sequence.CompareTo(y.sequence);
            }
        }
    }
}
