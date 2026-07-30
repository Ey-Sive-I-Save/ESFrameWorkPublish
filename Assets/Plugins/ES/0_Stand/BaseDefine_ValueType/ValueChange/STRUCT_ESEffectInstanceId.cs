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
        bool ReleaseEffect(int effectSlot, int generation);
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

        public bool IsValid => owner != null && effectSlot >= 0 && generation > 0;

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

        public void Dispose()
        {
            TryRelease();
        }
    }
}
