using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ES
{
    /// <summary>
    /// Fixed-capacity ring buffer ordered from oldest to newest. Capacity is allocated once
    /// and never grows at runtime. Use TryEnqueue to reject overflow or EnqueueOverwrite to
    /// keep the newest items. This type is not thread-safe.
    ///
    /// Concrete enumeration is allocation-free after warmup. Enumerating through an interface
    /// can box the struct enumerator and is not intended for steady hot paths.
    /// </summary>
    public sealed class ESRingBuffer<T> : IReadOnlyList<T>
    {
        private readonly T[] items;
        private readonly IEqualityComparer<T> comparer;
        private int head;
        private int count;
        private int version;

        public ESRingBuffer(int capacity, IEqualityComparer<T> comparer = null)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

            items = new T[capacity];
            this.comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return count; }
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return items.Length; }
        }

        public int AvailableCapacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return items.Length - count; }
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return count == 0; }
        }

        public bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return count == items.Length; }
        }

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return items[GetPhysicalIndex(index)];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(T item)
        {
            if (count == items.Length)
                return false;

            items[GetPhysicalIndex(count)] = item;
            count++;
            version++;
            return true;
        }

        /// <summary>
        /// Enqueues an item without growing the buffer. Returns true and exposes the removed
        /// oldest item when the buffer was full; otherwise returns false and outputs default.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EnqueueOverwrite(T item, out T removedOldest)
        {
            if (count < items.Length)
            {
                removedOldest = default;
                items[GetPhysicalIndex(count)] = item;
                count++;
                version++;
                return false;
            }

            removedOldest = items[head];
            items[head] = item;
            head = GetNextIndex(head);
            version++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T item)
        {
            if (count == 0)
            {
                item = default;
                return false;
            }

            item = items[head];
            items[head] = default;
            head = GetNextIndex(head);
            count--;
            if (count == 0)
                head = 0;
            version++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekOldest(out T item)
        {
            if (count == 0)
            {
                item = default;
                return false;
            }

            item = items[head];
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekNewest(out T item)
        {
            if (count == 0)
            {
                item = default;
                return false;
            }

            item = items[GetPhysicalIndex(count - 1)];
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAt(int index, out T item)
        {
            if ((uint)index >= (uint)count)
            {
                item = default;
                return false;
            }

            item = items[GetPhysicalIndex(index)];
            return true;
        }

        public bool Contains(T item)
        {
            for (int i = 0; i < count; i++)
            {
                if (comparer.Equals(items[GetPhysicalIndex(i)], item))
                    return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(T item)
        {
            return TryRemove(item, out _);
        }

        public bool TryRemove(T item, out T removed)
        {
            for (int i = 0; i < count; i++)
            {
                if (comparer.Equals(items[GetPhysicalIndex(i)], item))
                    return TryRemoveAt(i, out removed);
            }

            removed = default;
            return false;
        }

        public bool TryRemoveAt(int index, out T removed)
        {
            if ((uint)index >= (uint)count)
            {
                removed = default;
                return false;
            }

            if (index == 0)
                return TryDequeue(out removed);

            int removedIndex = GetPhysicalIndex(index);
            removed = items[removedIndex];
            for (int i = index; i < count - 1; i++)
                items[GetPhysicalIndex(i)] = items[GetPhysicalIndex(i + 1)];

            items[GetPhysicalIndex(count - 1)] = default;
            count--;
            version++;
            return true;
        }

        public void CopyTo(T[] destination, int destinationIndex = 0)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if ((uint)destinationIndex > (uint)destination.Length)
                throw new ArgumentOutOfRangeException(nameof(destinationIndex));
            if (destination.Length - destinationIndex < count)
                throw new ArgumentException("The destination does not have enough available capacity.", nameof(destination));
            if (count == 0)
                return;

            int firstCount = Math.Min(count, items.Length - head);
            Array.Copy(items, head, destination, destinationIndex, firstCount);
            int remaining = count - firstCount;
            if (remaining > 0)
                Array.Copy(items, 0, destination, destinationIndex + firstCount, remaining);
        }

        public void Clear()
        {
            if (count == 0)
            {
                head = 0;
                return;
            }

            for (int i = 0; i < count; i++)
                items[GetPhysicalIndex(i)] = default;

            head = 0;
            count = 0;
            version++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetPhysicalIndex(int logicalIndex)
        {
            int untilEnd = items.Length - head;
            return logicalIndex < untilEnd ? head + logicalIndex : logicalIndex - untilEnd;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetNextIndex(int index)
        {
            index++;
            return index == items.Length ? 0 : index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public struct Enumerator : IEnumerator<T>
        {
            private readonly ESRingBuffer<T> buffer;
            private readonly int expectedVersion;
            private int index;
            private T current;

            internal Enumerator(ESRingBuffer<T> buffer)
            {
                this.buffer = buffer;
                expectedVersion = buffer.version;
                index = -1;
                current = default;
            }

            public T Current
            {
                get
                {
                    ValidateVersion();
                    if ((uint)index >= (uint)buffer.count)
                        throw new InvalidOperationException("The enumerator is not positioned on an item.");
                    return current;
                }
            }

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                ValidateVersion();
                int next = index + 1;
                if (next >= buffer.count)
                {
                    index = buffer.count;
                    current = default;
                    return false;
                }

                index = next;
                current = buffer.items[buffer.GetPhysicalIndex(next)];
                return true;
            }

            public void Reset()
            {
                ValidateVersion();
                index = -1;
                current = default;
            }

            public void Dispose()
            {
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ValidateVersion()
            {
                if (expectedVersion != buffer.version)
                    throw new InvalidOperationException("The ring buffer changed during enumeration.");
            }
        }
    }
}
