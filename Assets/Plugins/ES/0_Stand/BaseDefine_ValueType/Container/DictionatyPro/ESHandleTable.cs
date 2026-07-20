using System;
using System.Collections.Generic;
using System.Threading;

namespace ES
{
    [Serializable]
    public struct ESHandleToken : IEquatable<ESHandleToken>
    {
        public int sourceId;
        public int id;
        public int version;

        public bool IsValid
        {
            get { return sourceId > 0 && id > 0 && version > 0; }
        }

        public static ESHandleToken Invalid
        {
            get { return default(ESHandleToken); }
        }

        public ESHandleToken(int sourceId, int id, int version)
        {
            this.sourceId = sourceId;
            this.id = id;
            this.version = version;
        }

        public bool Equals(ESHandleToken other)
        {
            return sourceId == other.sourceId && id == other.id && version == other.version;
        }

        public override bool Equals(object obj)
        {
            return obj is ESHandleToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = sourceId;
                hash = (hash * 397) ^ id;
                hash = (hash * 397) ^ version;
                return hash;
            }
        }

        public override string ToString()
        {
            return IsValid ? sourceId + ":" + id + ":" + version : "Invalid";
        }
    }

    internal static class ESHandleIdSource
    {
        private static int nextSourceId;

        public static int NewSourceId()
        {
            return Interlocked.Increment(ref nextSourceId);
        }
    }

    public sealed class ESHandleGate
    {
        private const int SlotId = 1;

        private readonly int sourceId;
        private int version;
        private bool active;

        public bool IsActive
        {
            get { return active; }
        }

        public ESHandleToken Current
        {
            get { return active ? new ESHandleToken(sourceId, SlotId, version) : ESHandleToken.Invalid; }
        }

        public ESHandleGate()
        {
            sourceId = ESHandleIdSource.NewSourceId();
        }

        public bool Acquire(out ESHandleToken handle)
        {
            if (active)
            {
                handle = Current;
                return false;
            }

            version++;
            if (version <= 0)
                version = 1;

            active = true;
            handle = Current;
            return true;
        }

        public ESHandleToken Acquire()
        {
            ESHandleToken handle;
            Acquire(out handle);
            return handle;
        }

        public bool Release(ESHandleToken handle)
        {
            if (!IsCurrent(handle))
                return false;

            active = false;
            return true;
        }

        public bool IsCurrent(ESHandleToken handle)
        {
            return active
                && handle.IsValid
                && handle.sourceId == sourceId
                && handle.id == SlotId
                && handle.version == version;
        }

        public void Clear()
        {
            active = false;
        }
    }

    public sealed class ESHandleSlot<TValue>
    {
        private const int SlotId = 1;

        private readonly int sourceId;
        private TValue value;
        private int version;
        private bool active;

        public bool IsActive
        {
            get { return active; }
        }

        public ESHandleToken Current
        {
            get { return active ? new ESHandleToken(sourceId, SlotId, version) : ESHandleToken.Invalid; }
        }

        public ESHandleSlot()
        {
            sourceId = ESHandleIdSource.NewSourceId();
        }

        public bool Add(TValue value, out ESHandleToken handle)
        {
            if (IsNull(value))
            {
                handle = ESHandleToken.Invalid;
                return false;
            }

            if (active)
            {
                handle = Current;
                return false;
            }

            version++;
            if (version <= 0)
                version = 1;

            this.value = value;
            active = true;
            handle = Current;
            return true;
        }

        public ESHandleToken Add(TValue value)
        {
            ESHandleToken handle;
            Add(value, out handle);
            return handle;
        }

        public bool TryGet(ESHandleToken handle, out TValue value)
        {
            if (!IsCurrent(handle))
            {
                value = default(TValue);
                return false;
            }

            value = this.value;
            return true;
        }

        public bool Remove(ESHandleToken handle)
        {
            TValue removed;
            return Remove(handle, out removed);
        }

        public bool Remove(ESHandleToken handle, out TValue value)
        {
            if (!IsCurrent(handle))
            {
                value = default(TValue);
                return false;
            }

            value = this.value;
            this.value = default(TValue);
            active = false;
            return true;
        }

        public bool Remove(out TValue value)
        {
            if (!active)
            {
                value = default(TValue);
                return false;
            }

            value = this.value;
            this.value = default(TValue);
            active = false;
            return true;
        }

        public bool IsCurrent(ESHandleToken handle)
        {
            return active
                && handle.IsValid
                && handle.sourceId == sourceId
                && handle.id == SlotId
                && handle.version == version;
        }

        public void Clear()
        {
            value = default(TValue);
            active = false;
        }

        private static bool IsNull(TValue value)
        {
            if (typeof(TValue).IsValueType)
                return false;

            return object.ReferenceEquals(value, null);
        }
    }

    public sealed class ESHandleTable<TValue>
    {
        private readonly int sourceId;
        private readonly Dictionary<int, Entry> entries;
        private readonly Dictionary<TValue, ESHandleToken> handlesByValue;
        private readonly Stack<int> freeIds;
        private int nextId = 1;

        private struct Entry
        {
            public TValue value;
            public int version;
            public bool active;
        }

        public int Count { get; private set; }

        public ESHandleTable(int capacity = 4, IEqualityComparer<TValue> comparer = null)
        {
            if (capacity < 0)
                capacity = 0;

            sourceId = ESHandleIdSource.NewSourceId();
            entries = new Dictionary<int, Entry>(capacity);
            handlesByValue = new Dictionary<TValue, ESHandleToken>(capacity, comparer);
            freeIds = new Stack<int>(capacity);
        }

        public ESHandleToken Add(TValue value)
        {
            ESHandleToken handle;
            Add(value, out handle);
            return handle;
        }

        public bool Add(TValue value, out ESHandleToken handle)
        {
            if (IsNull(value))
            {
                handle = ESHandleToken.Invalid;
                return false;
            }

            if (handlesByValue.TryGetValue(value, out handle) && IsActive(handle))
                return false;

            int id;
            Entry entry;
            if (freeIds.Count > 0)
            {
                id = freeIds.Pop();
                entry = entries[id];
                entry.version++;
            }
            else
            {
                id = nextId++;
                entry = default(Entry);
                entry.version = 1;
            }

            entry.value = value;
            entry.active = true;
            entries[id] = entry;

            handle = new ESHandleToken(sourceId, id, entry.version);
            handlesByValue[value] = handle;
            Count++;
            return true;
        }

        public bool IsActive(ESHandleToken handle)
        {
            Entry entry;
            return TryGetEntry(handle, out entry);
        }

        public bool TryGet(ESHandleToken handle, out TValue value)
        {
            Entry entry;
            if (TryGetEntry(handle, out entry))
            {
                value = entry.value;
                return true;
            }

            value = default(TValue);
            return false;
        }

        public bool Remove(ESHandleToken handle)
        {
            TValue value;
            return Remove(handle, out value);
        }

        public bool Remove(ESHandleToken handle, out TValue value)
        {
            Entry entry;
            if (!TryGetEntry(handle, out entry))
            {
                value = default(TValue);
                return false;
            }

            value = entry.value;
            entry.active = false;
            entry.value = default(TValue);
            entries[handle.id] = entry;
            handlesByValue.Remove(value);
            freeIds.Push(handle.id);
            Count--;
            return true;
        }

        public bool Remove(TValue value)
        {
            ESHandleToken handle;
            return Remove(value, out handle);
        }

        public bool Remove(TValue value, out ESHandleToken handle)
        {
            if (IsNull(value) || !handlesByValue.TryGetValue(value, out handle))
            {
                handle = ESHandleToken.Invalid;
                return false;
            }

            return Remove(handle);
        }

        public void Clear()
        {
            entries.Clear();
            handlesByValue.Clear();
            freeIds.Clear();
            Count = 0;
        }

        private bool TryGetEntry(ESHandleToken handle, out Entry entry)
        {
            entry = default(Entry);
            if (!handle.IsValid || handle.sourceId != sourceId || !entries.TryGetValue(handle.id, out entry))
                return false;

            return entry.active && entry.version == handle.version;
        }

        private static bool IsNull(TValue value)
        {
            if (typeof(TValue).IsValueType)
                return false;

            return object.ReferenceEquals(value, null);
        }
    }
}
