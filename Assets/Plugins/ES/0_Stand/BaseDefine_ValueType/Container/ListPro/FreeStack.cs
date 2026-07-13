using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// List-backed stack that allows removing elements from the middle.
    /// The last element is treated as the top.
    /// </summary>
    [Serializable, TypeRegistryItem("Free Stack")]
    public class FreeStack<T> : IReadOnlyList<T>
    {
        [LabelText("Elements")]
        public List<T> values = new List<T>(8);

        [NonSerialized]
        private int version;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return values == null ? 0 : values.Count; }
        }

        public int Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return version; }
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Count == 0; }
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                EnsureList();
                return values[index];
            }
        }

        public T Top
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!TryPeekTop(out var value))
                    throw new InvalidOperationException("FreeStack is empty.");

                return value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(T value)
        {
            EnsureList();
            values.Add(value);
            version++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T PopTop()
        {
            if (!TryPopTop(out var value))
                throw new InvalidOperationException("FreeStack is empty.");

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPopTop(out T value)
        {
            if (values == null || values.Count == 0)
            {
                value = default;
                return false;
            }

            int index = values.Count - 1;
            value = values[index];
            values.RemoveAt(index);
            version++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeekTop(out T value)
        {
            if (values == null || values.Count == 0)
            {
                value = default;
                return false;
            }

            value = values[values.Count - 1];
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T value)
        {
            return values != null && values.Contains(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf(T value)
        {
            return values == null ? -1 : values.IndexOf(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOf(T value)
        {
            return values == null ? -1 : values.LastIndexOf(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(T value)
        {
            if (values == null)
                return false;

            int index = values.LastIndexOf(value);
            if (index < 0)
                return false;

            values.RemoveAt(index);
            version++;
            return true;
        }

        public int RemoveAll(T value)
        {
            if (values == null || values.Count == 0)
                return 0;

            int removed = 0;
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = values.Count - 1; i >= 0; i--)
            {
                if (!comparer.Equals(values[i], value))
                    continue;

                values.RemoveAt(i);
                removed++;
            }

            if (removed > 0)
                version++;

            return removed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAt(int index)
        {
            if (values == null || index < 0 || index >= values.Count)
                return false;

            values.RemoveAt(index);
            version++;
            return true;
        }

        public bool RemoveAbove(T value)
        {
            int index = LastIndexOf(value);
            if (index < 0)
                return false;

            return RemoveRange(index + 1, Count - index - 1);
        }

        public bool RemoveWithAbove(T value)
        {
            int index = LastIndexOf(value);
            if (index < 0)
                return false;

            return RemoveRange(index, Count - index);
        }

        public bool RemoveRange(int index, int count)
        {
            if (values == null || values.Count == 0 || count < 0)
                return false;

            if (index < 0 || index > values.Count)
                return false;

            if (count == 0)
                return true;

            if (index == values.Count)
                return false;

            int available = values.Count - index;
            if (count > available)
                count = available;

            values.RemoveRange(index, count);
            version++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetStackIndex(T value)
        {
            return LastIndexOf(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (values == null || values.Count == 0)
                return;

            values.Clear();
            version++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<T> GetRawList()
        {
            EnsureList();
            return values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureList()
        {
            if (values == null)
                values = new List<T>(8);
        }

        public List<T>.Enumerator GetEnumerator()
        {
            EnsureList();
            return values.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
