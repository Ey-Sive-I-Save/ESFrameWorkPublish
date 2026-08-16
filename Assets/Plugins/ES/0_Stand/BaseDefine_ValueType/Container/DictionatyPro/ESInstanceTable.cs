using System;
using System.Collections.Generic;
using System.Threading;

namespace ES
{
    internal static class ESInstanceTableTokenSource
    {
        private static long nextTableToken;

        public static ulong Allocate()
        {
            long token = Interlocked.Increment(ref nextTableToken);
            if (token <= 0)
                throw new InvalidOperationException("ESInstanceTable table token source is exhausted.");
            return (ulong)token;
        }
    }

    /// <summary>
    /// Current-process instance handle. It is never a persistence, network, catalog, or asset identity.
    /// </summary>
    public readonly struct ESInstanceHandle : IEquatable<ESInstanceHandle>
    {
        public readonly ulong tableToken;
        public readonly uint tableEpoch;
        public readonly int slot;
        public readonly uint slotGeneration;

        public bool IsValid => tableToken != 0 && tableEpoch != 0 && slot >= 0 && slotGeneration != 0;

        public ESInstanceHandle(ulong tableToken, uint tableEpoch, int slot, uint slotGeneration)
        {
            this.tableToken = tableToken;
            this.tableEpoch = tableEpoch;
            this.slot = slot;
            this.slotGeneration = slotGeneration;
        }

        public bool Equals(ESInstanceHandle other)
        {
            return tableToken == other.tableToken
                && tableEpoch == other.tableEpoch
                && slot == other.slot
                && slotGeneration == other.slotGeneration;
        }

        public override bool Equals(object obj) => obj is ESInstanceHandle other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = tableToken.GetHashCode();
                hash = (hash * 397) ^ (int)tableEpoch;
                hash = (hash * 397) ^ slot;
                return (hash * 397) ^ (int)slotGeneration;
            }
        }

        public static bool operator ==(ESInstanceHandle left, ESInstanceHandle right) => left.Equals(right);
        public static bool operator !=(ESInstanceHandle left, ESInstanceHandle right) => !left.Equals(right);

        public override string ToString()
        {
            return IsValid
                ? tableToken + ":" + tableEpoch + ":" + slot + ":" + slotGeneration
                : "Invalid";
        }
    }

    /// <summary>
    /// Fixed-capacity instance storage with stable Slot handles and dense swap-remove records.
    /// The four generic parameters are real indexed dimensions, not a wrapper convention:
    /// persistent identity, definition identity, and owner identity are supplied at creation.
    ///
    /// The base is intentionally inheritable so a domain can expose a sealed concrete table
    /// after it has established real identity, ownership, and lifecycle invariants. The
    /// storage and index state remain private to this implementation; derived types should
    /// add domain validation and operations without reaching around the table contract.
    /// </summary>
    public class ESInstanceTable<TRecord, TPersistentId, TDefinitionKey, TOwnerKey>
        where TRecord : struct
        where TPersistentId : struct, IEquatable<TPersistentId>
        where TDefinitionKey : struct, IEquatable<TDefinitionKey>
        where TOwnerKey : struct, IEquatable<TOwnerKey>
    {
        private struct Slot
        {
            public uint generation;
            public int denseIndex;
            public bool active;
            public bool retired;
            public int definitionPrevious;
            public int definitionNext;
            public int ownerPrevious;
            public int ownerNext;
        }

        private struct IndexBucket
        {
            public int firstSlot;
            public int lastSlot;
            public int count;
        }

        private readonly ulong tableToken;
        private readonly Slot[] slots;
        private readonly TRecord[] denseRecords;
        private readonly int[] denseSlots;
        private readonly TPersistentId[] densePersistentIds;
        private readonly TDefinitionKey[] denseDefinitionKeys;
        private readonly TOwnerKey[] denseOwnerKeys;
        private readonly int[] freeSlots;
        private readonly Dictionary<TPersistentId, int> slotByPersistentId;
        private readonly Dictionary<TDefinitionKey, IndexBucket> definitionBuckets;
        private readonly Dictionary<TOwnerKey, IndexBucket> ownerBuckets;

        private uint tableEpoch = 1;
        private int freeCount;
        private int denseCount;
        private int retiredCount;

        public ESInstanceTable(
            int capacity,
            IEqualityComparer<TPersistentId> persistentComparer = null,
            IEqualityComparer<TDefinitionKey> definitionComparer = null,
            IEqualityComparer<TOwnerKey> ownerComparer = null)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            tableToken = ESInstanceTableTokenSource.Allocate();
            slots = new Slot[capacity];
            denseRecords = new TRecord[capacity];
            denseSlots = new int[capacity];
            densePersistentIds = new TPersistentId[capacity];
            denseDefinitionKeys = new TDefinitionKey[capacity];
            denseOwnerKeys = new TOwnerKey[capacity];
            freeSlots = new int[capacity];
            slotByPersistentId = new Dictionary<TPersistentId, int>(capacity, persistentComparer);
            definitionBuckets = new Dictionary<TDefinitionKey, IndexBucket>(capacity, definitionComparer);
            ownerBuckets = new Dictionary<TOwnerKey, IndexBucket>(capacity, ownerComparer);
            freeCount = capacity;

            for (int index = 0; index < capacity; index++)
            {
                slots[index].generation = 1;
                slots[index].denseIndex = -1;
                slots[index].definitionPrevious = -1;
                slots[index].definitionNext = -1;
                slots[index].ownerPrevious = -1;
                slots[index].ownerNext = -1;
                freeSlots[index] = capacity - index - 1;
                denseSlots[index] = -1;
            }
        }

        public ulong TableToken => tableToken;
        public uint TableEpoch => tableEpoch;
        public int Capacity => slots.Length;
        public int Count => denseCount;
        public int AvailableCapacity => freeCount;
        public int RetiredSlotCount => retiredCount;
        public int PersistentKeyCount => slotByPersistentId.Count;
        public int DefinitionKeyCount => definitionBuckets.Count;
        public int OwnerKeyCount => ownerBuckets.Count;

        public bool TryAdd(
            TRecord record,
            TPersistentId persistentId,
            TDefinitionKey definitionKey,
            TOwnerKey ownerKey,
            out ESInstanceHandle handle)
        {
            handle = default;
            if (freeCount == 0 || slotByPersistentId.ContainsKey(persistentId))
                return false;

            int slotIndex = freeSlots[--freeCount];
            Slot slot = slots[slotIndex];
            int denseIndex = denseCount++;
            slot.active = true;
            slot.denseIndex = denseIndex;
            slot.definitionPrevious = -1;
            slot.definitionNext = -1;
            slot.ownerPrevious = -1;
            slot.ownerNext = -1;
            slots[slotIndex] = slot;

            denseRecords[denseIndex] = record;
            denseSlots[denseIndex] = slotIndex;
            densePersistentIds[denseIndex] = persistentId;
            denseDefinitionKeys[denseIndex] = definitionKey;
            denseOwnerKeys[denseIndex] = ownerKey;
            slotByPersistentId.Add(persistentId, slotIndex);
            LinkDefinition(slotIndex, definitionKey);
            LinkOwner(slotIndex, ownerKey);
            handle = new ESInstanceHandle(tableToken, tableEpoch, slotIndex, slot.generation);
            return true;
        }

        public bool IsCurrent(ESInstanceHandle handle) => TryGetSlot(handle, out _);

        public bool TryGet(ESInstanceHandle handle, out TRecord record)
        {
            if (TryGetSlot(handle, out Slot slot))
            {
                record = denseRecords[slot.denseIndex];
                return true;
            }

            record = default;
            return false;
        }

        public bool TrySet(ESInstanceHandle handle, TRecord record)
        {
            if (!TryGetSlot(handle, out Slot slot))
                return false;
            denseRecords[slot.denseIndex] = record;
            return true;
        }

        /// <summary>
        /// Reassigns the owner index without changing the current-process handle.
        /// The record remains in the same dense slot; only the intrusive owner index
        /// is relinked. Callers must treat this as the ownership boundary for moves
        /// between inventory containers, equipment slots, and world ownership.
        /// </summary>
        public bool TrySetOwner(ESInstanceHandle handle, TOwnerKey ownerKey)
        {
            if (!TryGetSlot(handle, out Slot slot))
                return false;

            int denseIndex = slot.denseIndex;
            TOwnerKey previousOwnerKey = denseOwnerKeys[denseIndex];
            if (EqualityComparer<TOwnerKey>.Default.Equals(previousOwnerKey, ownerKey))
                return true;

            UnlinkOwner(handle.slot, previousOwnerKey);
            denseOwnerKeys[denseIndex] = ownerKey;
            LinkOwner(handle.slot, ownerKey);
            return true;
        }

        public bool TryGetIdentity(
            ESInstanceHandle handle,
            out TPersistentId persistentId,
            out TDefinitionKey definitionKey,
            out TOwnerKey ownerKey)
        {
            if (TryGetSlot(handle, out Slot slot))
            {
                int denseIndex = slot.denseIndex;
                persistentId = densePersistentIds[denseIndex];
                definitionKey = denseDefinitionKeys[denseIndex];
                ownerKey = denseOwnerKeys[denseIndex];
                return true;
            }

            persistentId = default;
            definitionKey = default;
            ownerKey = default;
            return false;
        }

        public bool TryGetByPersistentId(TPersistentId persistentId, out ESInstanceHandle handle)
        {
            if (slotByPersistentId.TryGetValue(persistentId, out int slotIndex))
            {
                Slot slot = slots[slotIndex];
                if (slot.active)
                {
                    handle = new ESInstanceHandle(tableToken, tableEpoch, slotIndex, slot.generation);
                    return true;
                }
            }

            handle = default;
            return false;
        }

        public bool TryGetDefinitionBucket(
            TDefinitionKey definitionKey,
            out ESInstanceHandle first,
            out int count)
        {
            if (definitionBuckets.TryGetValue(definitionKey, out IndexBucket bucket))
            {
                first = CreateHandle(bucket.firstSlot);
                count = bucket.count;
                return true;
            }

            first = default;
            count = 0;
            return false;
        }

        public bool TryGetOwnerBucket(TOwnerKey ownerKey, out ESInstanceHandle first, out int count)
        {
            if (ownerBuckets.TryGetValue(ownerKey, out IndexBucket bucket))
            {
                first = CreateHandle(bucket.firstSlot);
                count = bucket.count;
                return true;
            }

            first = default;
            count = 0;
            return false;
        }

        public bool TryGetNextByDefinition(ESInstanceHandle current, out ESInstanceHandle next)
        {
            return TryGetNext(current, true, out next);
        }

        public bool TryGetNextByOwner(ESInstanceHandle current, out ESInstanceHandle next)
        {
            return TryGetNext(current, false, out next);
        }

        public bool TryRemove(ESInstanceHandle handle, out TRecord removed)
        {
            if (!TryGetSlot(handle, out Slot removedSlot))
            {
                removed = default;
                return false;
            }

            int removedDenseIndex = removedSlot.denseIndex;
            int lastDenseIndex = denseCount - 1;
            removed = denseRecords[removedDenseIndex];
            TPersistentId removedPersistentId = densePersistentIds[removedDenseIndex];
            TDefinitionKey removedDefinitionKey = denseDefinitionKeys[removedDenseIndex];
            TOwnerKey removedOwnerKey = denseOwnerKeys[removedDenseIndex];
            UnlinkDefinition(handle.slot, removedDefinitionKey);
            UnlinkOwner(handle.slot, removedOwnerKey);
            slotByPersistentId.Remove(removedPersistentId);

            if (removedDenseIndex != lastDenseIndex)
            {
                denseRecords[removedDenseIndex] = denseRecords[lastDenseIndex];
                densePersistentIds[removedDenseIndex] = densePersistentIds[lastDenseIndex];
                denseDefinitionKeys[removedDenseIndex] = denseDefinitionKeys[lastDenseIndex];
                denseOwnerKeys[removedDenseIndex] = denseOwnerKeys[lastDenseIndex];
                int movedSlotIndex = denseSlots[lastDenseIndex];
                denseSlots[removedDenseIndex] = movedSlotIndex;
                Slot movedSlot = slots[movedSlotIndex];
                movedSlot.denseIndex = removedDenseIndex;
                slots[movedSlotIndex] = movedSlot;
            }

            denseRecords[lastDenseIndex] = default;
            densePersistentIds[lastDenseIndex] = default;
            denseDefinitionKeys[lastDenseIndex] = default;
            denseOwnerKeys[lastDenseIndex] = default;
            denseSlots[lastDenseIndex] = -1;
            denseCount--;

            Slot releasedSlot = slots[handle.slot];
            releasedSlot.active = false;
            releasedSlot.denseIndex = -1;
            releasedSlot.definitionPrevious = -1;
            releasedSlot.definitionNext = -1;
            releasedSlot.ownerPrevious = -1;
            releasedSlot.ownerNext = -1;
            if (releasedSlot.generation >= uint.MaxValue - 1)
            {
                releasedSlot.generation = uint.MaxValue;
                releasedSlot.retired = true;
                retiredCount++;
            }
            else
            {
                releasedSlot.generation++;
                freeSlots[freeCount++] = handle.slot;
            }
            slots[handle.slot] = releasedSlot;
            return true;
        }

        public void Clear()
        {
            if (tableEpoch == uint.MaxValue)
                throw new InvalidOperationException("The instance table epoch is exhausted and cannot wrap.");

            tableEpoch++;
            denseCount = 0;
            freeCount = 0;
            retiredCount = 0;
            slotByPersistentId.Clear();
            definitionBuckets.Clear();
            ownerBuckets.Clear();
            for (int index = 0; index < slots.Length; index++)
            {
                Slot slot = slots[index];
                slot.active = false;
                slot.denseIndex = -1;
                slot.definitionPrevious = -1;
                slot.definitionNext = -1;
                slot.ownerPrevious = -1;
                slot.ownerNext = -1;
                slots[index] = slot;
                if (!slot.retired)
                    freeSlots[freeCount++] = index;
                else
                    retiredCount++;
                denseSlots[index] = -1;
                denseRecords[index] = default;
                densePersistentIds[index] = default;
                denseDefinitionKeys[index] = default;
                denseOwnerKeys[index] = default;
            }
        }

        private bool TryGetNext(ESInstanceHandle current, bool definition, out ESInstanceHandle next)
        {
            if (!TryGetSlot(current, out Slot slot))
            {
                next = default;
                return false;
            }

            int nextSlot = definition ? slot.definitionNext : slot.ownerNext;
            if (nextSlot < 0)
            {
                next = default;
                return false;
            }

            Slot candidate = slots[nextSlot];
            next = new ESInstanceHandle(tableToken, tableEpoch, nextSlot, candidate.generation);
            return true;
        }

        private ESInstanceHandle CreateHandle(int slotIndex)
        {
            if ((uint)slotIndex >= (uint)slots.Length || !slots[slotIndex].active)
                return default;
            return new ESInstanceHandle(tableToken, tableEpoch, slotIndex, slots[slotIndex].generation);
        }

        private void LinkDefinition(int slotIndex, TDefinitionKey key)
        {
            if (!definitionBuckets.TryGetValue(key, out IndexBucket bucket))
                bucket = new IndexBucket { firstSlot = -1, lastSlot = -1 };

            Slot slot = slots[slotIndex];
            slot.definitionPrevious = bucket.lastSlot;
            slot.definitionNext = -1;
            if (bucket.lastSlot >= 0)
            {
                Slot previous = slots[bucket.lastSlot];
                previous.definitionNext = slotIndex;
                slots[bucket.lastSlot] = previous;
            }
            else
            {
                bucket.firstSlot = slotIndex;
            }
            bucket.lastSlot = slotIndex;
            bucket.count++;
            slots[slotIndex] = slot;
            definitionBuckets[key] = bucket;
        }

        private void LinkOwner(int slotIndex, TOwnerKey key)
        {
            if (!ownerBuckets.TryGetValue(key, out IndexBucket bucket))
                bucket = new IndexBucket { firstSlot = -1, lastSlot = -1 };

            Slot slot = slots[slotIndex];
            slot.ownerPrevious = bucket.lastSlot;
            slot.ownerNext = -1;
            if (bucket.lastSlot >= 0)
            {
                Slot previous = slots[bucket.lastSlot];
                previous.ownerNext = slotIndex;
                slots[bucket.lastSlot] = previous;
            }
            else
            {
                bucket.firstSlot = slotIndex;
            }
            bucket.lastSlot = slotIndex;
            bucket.count++;
            slots[slotIndex] = slot;
            ownerBuckets[key] = bucket;
        }

        private void UnlinkDefinition(int slotIndex, TDefinitionKey key)
        {
            if (!definitionBuckets.TryGetValue(key, out IndexBucket bucket))
                return;
            Slot slot = slots[slotIndex];
            if (slot.definitionPrevious >= 0)
            {
                Slot previous = slots[slot.definitionPrevious];
                previous.definitionNext = slot.definitionNext;
                slots[slot.definitionPrevious] = previous;
            }
            else
            {
                bucket.firstSlot = slot.definitionNext;
            }
            if (slot.definitionNext >= 0)
            {
                Slot next = slots[slot.definitionNext];
                next.definitionPrevious = slot.definitionPrevious;
                slots[slot.definitionNext] = next;
            }
            else
            {
                bucket.lastSlot = slot.definitionPrevious;
            }
            bucket.count--;
            if (bucket.count == 0)
                definitionBuckets.Remove(key);
            else
                definitionBuckets[key] = bucket;
        }

        private void UnlinkOwner(int slotIndex, TOwnerKey key)
        {
            if (!ownerBuckets.TryGetValue(key, out IndexBucket bucket))
                return;
            Slot slot = slots[slotIndex];
            if (slot.ownerPrevious >= 0)
            {
                Slot previous = slots[slot.ownerPrevious];
                previous.ownerNext = slot.ownerNext;
                slots[slot.ownerPrevious] = previous;
            }
            else
            {
                bucket.firstSlot = slot.ownerNext;
            }
            if (slot.ownerNext >= 0)
            {
                Slot next = slots[slot.ownerNext];
                next.ownerPrevious = slot.ownerPrevious;
                slots[slot.ownerNext] = next;
            }
            else
            {
                bucket.lastSlot = slot.ownerPrevious;
            }
            bucket.count--;
            if (bucket.count == 0)
                ownerBuckets.Remove(key);
            else
                ownerBuckets[key] = bucket;
        }

        private bool TryGetSlot(ESInstanceHandle handle, out Slot slot)
        {
            if (!handle.IsValid
                || handle.tableToken != tableToken
                || handle.tableEpoch != tableEpoch
                || (uint)handle.slot >= (uint)slots.Length)
            {
                slot = default;
                return false;
            }

            slot = slots[handle.slot];
            return slot.active && !slot.retired && slot.generation == handle.slotGeneration;
        }

    }
}
