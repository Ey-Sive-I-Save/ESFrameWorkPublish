using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ES
{
    /// <summary>
    /// Link 容器共用的主线程订阅表。
    /// 接收者按引用身份去重；任意增删均在下一次派发开始前提交，保证本轮快照稳定。
    /// </summary>
    internal sealed class LinkSubscriptionList<T> where T : class
    {
        private readonly struct PendingChange
        {
            public readonly T Receiver;
            public readonly bool Add;

            public PendingChange(T receiver, bool add)
            {
                Receiver = receiver;
                Add = add;
            }
        }

        public readonly List<T> ValuesNow;
        private readonly List<PendingChange> pendingChanges;
        private int dispatchDepth;

        public LinkSubscriptionList(int capacity = 4)
        {
            capacity = Math.Max(0, capacity);
            ValuesNow = new List<T>(capacity);
            pendingChanges = new List<PendingChange>(capacity);
        }

        public bool IsDispatching => dispatchDepth > 0;
        public int Count => ValuesNow.Count;

        public bool Add(T receiver)
        {
            if (receiver == null || ContainsEffective(receiver))
                return false;

            pendingChanges.Add(new PendingChange(receiver, true));
            return true;
        }

        public bool Remove(T receiver)
        {
            if (receiver == null || !ContainsEffective(receiver))
                return false;

            pendingChanges.Add(new PendingChange(receiver, false));
            return true;
        }

        public void ApplyBuffers()
        {
            if (dispatchDepth > 0)
                return;

            ApplyPendingChanges();
        }

        public void BeginDispatch()
        {
            if (dispatchDepth++ == 0)
                ApplyPendingChanges();
        }

        public void EndDispatch()
        {
            if (dispatchDepth <= 0)
                throw new InvalidOperationException("LinkSubscriptionList dispatch depth is unbalanced.");
            dispatchDepth--;
        }

        public void Reserve(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (ValuesNow.Capacity < capacity)
                ValuesNow.Capacity = capacity;
            if (pendingChanges.Capacity < capacity)
                pendingChanges.Capacity = capacity;
        }

        public void Clear()
        {
            if (!IsDispatching)
            {
                ValuesNow.Clear();
                pendingChanges.Clear();
                return;
            }

            pendingChanges.Clear();
            int count = ValuesNow.Count;
            for (int i = 0; i < count; i++)
                pendingChanges.Add(new PendingChange(ValuesNow[i], false));
        }

        private bool ContainsEffective(T receiver)
        {
            bool contains = IndexOf(receiver) >= 0;
            int count = pendingChanges.Count;
            for (int i = 0; i < count; i++)
            {
                PendingChange change = pendingChanges[i];
                if (ReferenceEquals(change.Receiver, receiver))
                    contains = change.Add;
            }

            return contains;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int IndexOf(T receiver)
        {
            int count = ValuesNow.Count;
            for (int i = 0; i < count; i++)
                if (ReferenceEquals(ValuesNow[i], receiver))
                    return i;
            return -1;
        }

        private void ApplyPendingChanges()
        {
            int count = pendingChanges.Count;
            for (int i = 0; i < count; i++)
            {
                PendingChange change = pendingChanges[i];
                int index = IndexOf(change.Receiver);
                if (change.Add)
                {
                    if (index < 0)
                        ValuesNow.Add(change.Receiver);
                }
                else if (index >= 0)
                {
                    ValuesNow.RemoveAt(index);
                }
            }

            pendingChanges.Clear();
        }
    }
}
