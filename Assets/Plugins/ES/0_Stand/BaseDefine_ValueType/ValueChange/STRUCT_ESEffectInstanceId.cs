using System;

namespace ES
{
    /// <summary>
    /// Stable identity supplied by an owning runtime effect for diagnostics, grouping and lifecycle.
    /// It is distinct from a ValueChange token and is never a RuntimeKey or asset identity.
    /// </summary>
    public struct ESEffectInstanceId : IEquatable<ESEffectInstanceId>
    {
        public readonly ulong value;

        public bool IsValid => value != 0UL;

        public ESEffectInstanceId(ulong value)
        {
            this.value = value;
        }

        public bool Equals(ESEffectInstanceId other)
        {
            return value == other.value;
        }

        public override bool Equals(object obj)
        {
            return obj is ESEffectInstanceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? value.ToString() : "Invalid";
        }
    }

    /// <summary>
    /// Domain-owned release endpoint for an EffectLease. Implementations validate the slot generation
    /// so copied or stale leases are harmless.
    /// </summary>
    public interface IESEffectLeaseOwner
    {
        bool IsEffectActive(int effectSlot, int generation);
        bool ReleaseEffect(int effectSlot, int generation);
        bool TryAddEffectFloat(
            int effectSlot,
            int generation,
            ESFloatValueChangeSet set,
            ESFloatValueChangeOp op,
            float value,
            int sourceId,
            int priority,
            bool enabled,
            out ESValueChangeToken token);
        bool TryAddEffectPermit(
            int effectSlot,
            int generation,
            ESPermitSet set,
            ESPermitLaw law,
            int sourceId,
            int priority,
            bool enabled,
            out ESValueChangeToken token);
    }

    /// <summary>
    /// Allocation-free lifecycle handle used by AttributeRuntime-level APIs. The owning domain stores
    /// modifier bindings; disposing the lease only asks that domain to release its slot.
    /// </summary>
    public struct ESEffectLease : IDisposable
    {
        private IESEffectLeaseOwner owner;
        private readonly int effectSlot;
        private readonly int generation;

        /// <summary>
        /// Whether this lease still owns its exact slot generation. A copied or stale lease is
        /// invalid as soon as its original owner releases or reuses that slot.
        /// </summary>
        public bool IsValid => owner != null
                               && effectSlot >= 0
                               && generation > 0
                               && owner.IsEffectActive(effectSlot, generation);

        public ESEffectLease(IESEffectLeaseOwner owner, int effectSlot, int generation)
        {
            this.owner = owner;
            this.effectSlot = effectSlot;
            this.generation = generation;
        }

        public bool TryRelease()
        {
            IESEffectLeaseOwner target = owner;
            owner = null;
            return target != null && target.ReleaseEffect(effectSlot, generation);
        }

        /// <summary>
        /// Adds one float modifier only while this exact lease generation is active. The owner id
        /// remains an implementation detail, so a delayed writer cannot attach to a reused slot.
        /// </summary>
        public bool TryAddFloat(
            ESFloatValueChangeSet set,
            ESFloatValueChangeOp op,
            float value,
            int sourceId,
            int priority,
            bool enabled,
            out ESValueChangeToken token)
        {
            IESEffectLeaseOwner target = owner;
            if (target == null)
            {
                token = ESValueChangeToken.Invalid;
                return false;
            }

            return target.TryAddEffectFloat(
                effectSlot, generation, set, op, value, sourceId, priority, enabled, out token);
        }

        /// <summary>Permit counterpart of <see cref="TryAddFloat"/>.</summary>
        public bool TryAddPermit(
            ESPermitSet set,
            ESPermitLaw law,
            int sourceId,
            int priority,
            bool enabled,
            out ESValueChangeToken token)
        {
            IESEffectLeaseOwner target = owner;
            if (target == null)
            {
                token = ESValueChangeToken.Invalid;
                return false;
            }

            return target.TryAddEffectPermit(
                effectSlot, generation, set, law, sourceId, priority, enabled, out token);
        }

        public void Dispose()
        {
            TryRelease();
        }
    }
}
