using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ES
{
    /// <summary>
    /// Fixed-capacity recent-item history. Adding to a full history removes the oldest item.
    /// Items are read and enumerated from oldest to newest. This type is not thread-safe.
    /// </summary>
    public sealed class ESRecentHistory<T> : IReadOnlyList<T>
    {
        private readonly ESFixedQueue<T> items;

        public ESRecentHistory(int capacity)
        {
            items = new ESFixedQueue<T>(capacity);
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return items.Count; }
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return items.Capacity; }
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return items.IsEmpty; }
        }

        public bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return items.IsFull; }
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return items[index]; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            if (items.IsFull)
                items.TryDequeue(out _);
            items.TryEnqueue(item);
        }

        /// <summary>
        /// Adds an item and returns true when the full history removed its oldest item.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(T item, out T removedOldest)
        {
            removedOldest = default;
            bool removed = false;
            if (items.IsFull)
                removed = items.TryDequeue(out removedOldest);
            items.TryEnqueue(item);
            return removed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetOldest(out T item)
        {
            return items.TryPeekOldest(out item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLatest(out T item)
        {
            return items.TryPeekNewest(out item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAt(int index, out T item)
        {
            return items.TryGetAt(index, out item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T item)
        {
            return items.Contains(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(T[] destination, int destinationIndex = 0)
        {
            items.CopyTo(destination, destinationIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            items.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            return new Enumerator(items);
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
            private ESFixedQueue<T>.Enumerator inner;

            internal Enumerator(ESFixedQueue<T> items)
            {
                inner = items.GetEnumerator();
            }

            public T Current => inner.Current;

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                return inner.MoveNext();
            }

            public void Reset()
            {
                inner.Reset();
            }

            public void Dispose()
            {
                inner.Dispose();
            }
        }
    }
}
