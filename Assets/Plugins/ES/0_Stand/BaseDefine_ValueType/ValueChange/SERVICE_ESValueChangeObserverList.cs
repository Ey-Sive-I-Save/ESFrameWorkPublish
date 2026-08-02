using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Shared observer dispatch for ValueChange sets. Subscription changes made during dispatch
    /// apply after the current pass. Normal dispatch performs no allocations.
    /// </summary>
    internal sealed class ESValueChangeObserverList<T> where T : class
    {
        private struct PendingChange
        {
            public Action<T> listener;
            public bool add;
        }

        private List<Action<T>> listeners;
        private List<PendingChange> pendingChanges;
        private bool isDispatching;

        public int Count => listeners != null ? listeners.Count : 0;

        public void Add(Action<T> listener)
        {
            if (listener == null)
                return;

            if (isDispatching)
            {
                Queue(listener, add: true);
                return;
            }

            AddNow(listener);
        }

        public void Remove(Action<T> listener)
        {
            if (listener == null || listeners == null)
                return;

            if (isDispatching)
            {
                Queue(listener, add: false);
                return;
            }

            listeners.Remove(listener);
        }

        public void Notify(T value)
        {
            if (listeners == null || listeners.Count == 0)
                return;

            isDispatching = true;
            int count = listeners.Count;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        listeners[i]?.Invoke(value);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                isDispatching = false;
                ApplyPendingChanges();
            }
        }

        /// <summary>
        /// Removes every observer while retaining the small backing buffers for the next owner.
        /// ValueChange sets use this at a host lifecycle boundary so callbacks from a previous
        /// pooled renter cannot observe or mutate the next renter.
        /// </summary>
        public void Clear()
        {
            listeners?.Clear();
            pendingChanges?.Clear();
        }

        private void Queue(Action<T> listener, bool add)
        {
            if (pendingChanges == null)
                pendingChanges = new List<PendingChange>(2);

            pendingChanges.Add(new PendingChange { listener = listener, add = add });
        }

        private void ApplyPendingChanges()
        {
            if (pendingChanges == null || pendingChanges.Count == 0)
                return;

            for (int i = 0; i < pendingChanges.Count; i++)
            {
                PendingChange change = pendingChanges[i];
                if (change.add)
                    AddNow(change.listener);
                else
                    listeners?.Remove(change.listener);
            }

            pendingChanges.Clear();
        }

        private void AddNow(Action<T> listener)
        {
            if (listeners == null)
                listeners = new List<Action<T>>(2);

            if (!listeners.Contains(listener))
                listeners.Add(listener);
        }
    }
}
