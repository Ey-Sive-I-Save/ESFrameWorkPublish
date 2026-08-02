using System;

namespace ES
{
    /// <summary>
    /// Optional authored mechanism for a Buff definition. It owns configured rules and lifecycle
    /// decisions, but never per-target state: one definition can be active on many owners at once.
    /// </summary>
    [Serializable]
    public abstract class ESBuffLogic
    {
        /// <summary>
        /// Returns one isolated runtime for a single active Buff. Complex implementations should
        /// rent the returned object from their own ESSimplePool.
        /// </summary>
        public abstract ESBuffLogicRuntime RentRuntime();

        /// <summary>Return false to reject application and trigger the normal Buff rollback path.</summary>
        public virtual bool OnApply(ESBuffLogicRuntime runtime) => true;

        /// <summary>Runs after stack, duration or level changes and related ValueChange refreshes.</summary>
        public virtual void OnRefresh(ESBuffLogicRuntime runtime) { }

        /// <summary>Runs on the Buff definition's existing Tick mode and interval.</summary>
        public virtual void OnTick(ESBuffLogicRuntime runtime, float deltaTime) { }

        /// <summary>Runs only for a normally removed Buff, before the configured Remove Op.</summary>
        public virtual void OnRemove(ESBuffLogicRuntime runtime) { }

        /// <summary>
        /// Always runs while the Buff still owns its Tag, ValueChange and Support resources. Release
        /// subscriptions, leases and tokens stored by <paramref name="runtime"/> here. It also runs
        /// for Apply rollback and pool cleanup.
        /// </summary>
        public virtual void OnRelease(ESBuffLogicRuntime runtime) { }
    }

    /// <summary>
    /// Per-active-Buff state and resource container for an optional <see cref="ESBuffLogic"/>.
    /// The definition owns mechanism decisions; this runtime owns only one Buff instance's mutable
    /// state and resources.
    /// </summary>
    public abstract class ESBuffLogicRuntime : IPoolableAuto
    {
        public bool IsRecycled { get; set; }

        /// <summary>The sole active Buff instance this runtime may access.</summary>
        public ESActiveBuffRuntime Buff { get; private set; }

        public Entity Owner => Buff != null ? Buff.Owner : null;
        public ESRuntimeTargetPack Target => Buff != null ? Buff.TargetPack : null;
        public ESOpSupport Support => Buff != null ? Buff.Support : null;

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

        internal void DetachAndReturnToPool()
        {
            Buff = null;
            TryAutoPushedToPool();
        }
    }
}
