using System;

namespace ES
{
    /// <summary>
    /// Optional authored mechanism for a Buff definition. It is shared configuration only: never
    /// retain per-target state here, because one definition can be active on many owners at once.
    /// </summary>
    [Serializable]
    public abstract class ESBuffLogic
    {
        /// <summary>
        /// Returns one isolated runtime for a single active Buff. Complex implementations should
        /// rent the returned object from their own ESSimplePool.
        /// </summary>
        public abstract ESBuffLogicRuntime RentRuntime();
    }

    /// <summary>
    /// Per-active-Buff state for an optional <see cref="ESBuffLogic"/>. The Buff framework owns
    /// its attach, lifecycle calls and release; custom logic owns only resources it creates.
    /// </summary>
    public abstract class ESBuffLogicRuntime : IPoolableAuto
    {
        public bool IsRecycled { get; set; }

        /// <summary>The sole active Buff instance this runtime may access.</summary>
        public ESActiveBuffRuntime Buff { get; private set; }

        public Entity Owner => Buff != null ? Buff.Owner : null;
        public ESRuntimeTargetPack Target => Buff != null ? Buff.TargetPack : null;
        public ESOpSupport Support => Buff != null ? Buff.Support : null;

        /// <summary>Return false to reject application and trigger the normal Buff rollback path.</summary>
        public virtual bool OnApply() => true;

        /// <summary>Runs after stack, duration or level changes and related ValueChange refreshes.</summary>
        public virtual void OnRefresh() { }

        /// <summary>Runs on the Buff definition's existing Tick mode and interval.</summary>
        public virtual void OnTick(float deltaTime) { }

        /// <summary>Runs only for a normally removed Buff, before the configured Remove Op.</summary>
        public virtual void OnRemove() { }

        /// <summary>
        /// Always runs while the Buff still owns its Tag, ValueChange and Support resources. Release
        /// subscriptions, leases and tokens here; it also runs for Apply rollback and pool cleanup.
        /// </summary>
        protected virtual void OnRelease() { }

        /// <summary>Return this runtime to its concrete pool. Do not call this from a lifecycle hook.</summary>
        public abstract void TryAutoPushedToPool();

        /// <summary>Concrete pools call this before marking the instance recycled.</summary>
        public virtual void OnResetAsPoolable()
        {
            Buff = null;
        }

        internal void Attach(ESActiveBuffRuntime buff)
        {
            if (buff == null)
                throw new ArgumentNullException(nameof(buff));
            if (IsRecycled)
                throw new InvalidOperationException("ESBuffLogicRuntime is still in its pool. Rent it before attaching it to a Buff.");
            if (Buff != null)
                throw new InvalidOperationException("ESBuffLogicRuntime is already attached to an active Buff.");

            Buff = buff;
        }

        internal void ReleaseAndReturnToPool()
        {
            try
            {
                OnRelease();
            }
            finally
            {
                Buff = null;
                TryAutoPushedToPool();
            }
        }
    }
}
