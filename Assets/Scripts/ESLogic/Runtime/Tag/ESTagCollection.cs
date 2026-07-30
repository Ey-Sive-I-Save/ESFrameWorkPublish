using System;
using System.Collections.Generic;

namespace ES
{
    /// <summary>
    /// Reusable runtime Tag container. It owns aggregate counts, storage, conditions, snapshots,
    /// and generic change events. It has no knowledge of Entity, business scope, or permissions.
    /// </summary>
    public sealed class ESTagCollection : IDisposable
    {
        private ESTagRefCountSet64 hotTags;
        private Dictionary<int, int> sparseCounts;
        private ESTagTagChangeDebugInfo lastChange;
        private ESTagTagRejectedDebugInfo lastRejected;
        private bool disposed;

        public event Action<ESTagId, int, int> OnTagCountChanged;
        public event Action<ESTagId, bool> OnTagPresenceChanged;

        public ESTagCollection() { }

        public bool IsDisposed => disposed;
        public ESTagMask64 HotMask => hotTags.ActiveMask;

        public void Warmup()
        {
            ThrowIfDisposed();
            hotTags.Warmup();
        }

        public ESTagLease Acquire(ESGameTag tag, object source = null)
        {
            return Acquire(ESTagId.FromInt32((ushort)tag), source);
        }

        public ESTagLease Acquire(ESTagId tag, object source = null)
        {
            if (!TryAdd(tag))
                return null;

            return new ESTagLease(this, tag, source);
        }

        public ESTagLease Acquire(ESTagStableReference reference, object source = null)
        {
            if (!ESTagRuntimeCatalog.TryGetRuntimeKey(reference, out int runtimeKey))
            {
                RecordRejected(ESTagId.Invalid, reference.ToString(),
                    ESTagRuntimeCatalog.IsBound
                        ? "Stable Tag reference is not registered by the active Tag Catalog."
                        : "Tag Catalog is not bound.");
                return null;
            }

            return Acquire(ESTagId.FromInt32(runtimeKey), source);
        }

        public bool TryAcquireStringKey(string stableKey, object source, out ESTagLease lease)
        {
            lease = null;
            if (!ESTagRuntimeCatalog.TryGetRuntimeKey(stableKey, out int runtimeKey))
            {
                RecordRejected(ESTagId.Invalid, stableKey,
                    ESTagRuntimeCatalog.IsBound
                        ? "Stable StringKey is not registered by the active Tag Catalog."
                        : "Tag Catalog is not bound.");
                return false;
            }

            lease = Acquire(ESTagId.FromInt32(runtimeKey), source);
            return lease != null;
        }

        public bool Has(ESTagId tag)
        {
            return GetCount(tag) > 0;
        }

        public int GetCount(ESTagId tag)
        {
            if (IsHot(tag))
                return hotTags.GetCount(tag);

            return sparseCounts != null && sparseCounts.TryGetValue(tag.Value, out int count) ? count : 0;
        }

        public bool HasAny(ESTagMask64 mask)
        {
            return hotTags.Overlaps(mask);
        }

        public bool HasAll(ESTagMask64 mask)
        {
            return hotTags.HasAll(mask);
        }

        public bool Matches(ESTagConditionRuntime condition)
        {
            return TryMatches(condition, out bool matches, out _) && matches;
        }

        public bool Matches(ESTagConditionConfig config)
        {
            return TryMatches(config, out bool matches, out _) && matches;
        }

        public bool TryMatches(ESTagConditionConfig config, out bool matches, out string error)
        {
            matches = false;
            if (config == null)
            {
                error = "Tag condition configuration is null.";
                return false;
            }

            if (!config.TryGetRuntime(out ESTagConditionRuntime runtime, out error))
                return false;

            return TryMatches(runtime, out matches, out error);
        }

        public bool TryMatches(ESTagConditionRuntime condition, out bool matches, out string error)
        {
            ThrowIfDisposed();
            matches = false;
            error = null;
            if (!condition.MatchesCore(hotTags.ActiveMask.Bits))
                return true;

            if (!condition.HasExtensionConditions)
            {
                matches = true;
                return true;
            }

            if (!condition.TryValidateActiveCatalog(out error))
                return false;

            int[] required = condition.RequiredExtensions;
            for (int i = 0; i < required.Length; i++)
            {
                if (!HasSparseRuntimeKey(required[i]))
                    return true;
            }

            int[] requiredAny = condition.RequiredAnyExtensions;
            if (requiredAny != null && requiredAny.Length > 0)
            {
                bool hasAny = false;
                for (int i = 0; i < requiredAny.Length; i++)
                {
                    if (!HasSparseRuntimeKey(requiredAny[i]))
                        continue;

                    hasAny = true;
                    break;
                }

                if (!hasAny)
                    return true;
            }

            int[] forbidden = condition.ForbiddenExtensions;
            for (int i = 0; i < forbidden.Length; i++)
            {
                if (HasSparseRuntimeKey(forbidden[i]))
                    return true;
            }

            matches = true;
            return true;
        }

        public void Clear()
        {
            ThrowIfDisposed();
            var active = new List<KeyValuePair<ESTagId, int>>(8);
            for (ushort value = ESTagIdRange.EnumStart; value <= ESTagIdRange.CoreRuntimeEnd; value++)
            {
                ESTagId tag = ESTagId.FromInt32(value);
                int count = hotTags.GetCount(tag);
                if (count > 0)
                    active.Add(new KeyValuePair<ESTagId, int>(tag, count));
            }

            if (sparseCounts != null)
            {
                foreach (KeyValuePair<int, int> pair in sparseCounts)
                {
                    if (pair.Value > 0)
                        active.Add(new KeyValuePair<ESTagId, int>(ESTagId.FromInt32(pair.Key), pair.Value));
                }
            }

            hotTags.Clear();
            sparseCounts?.Clear();
            for (int i = 0; i < active.Count; i++)
                NotifyChanged(active[i].Key, active[i].Value, 0);
        }

        public ESTagDebugSnapshot GetDebugSnapshot()
        {
            var hot = new List<ESTagDebugEntry>(8);
            for (ushort value = ESTagIdRange.EnumStart; value <= ESTagIdRange.CoreRuntimeEnd; value++)
            {
                ESTagId tag = ESTagId.FromInt32(value);
                int count = hotTags.GetCount(tag);
                if (count > 0)
                    hot.Add(CreateDebugEntry(tag, count, false));
            }

            var sparse = new List<ESTagDebugEntry>(sparseCounts != null ? sparseCounts.Count : 0);
            if (sparseCounts != null)
            {
                var runtimeKeys = new List<int>(sparseCounts.Keys);
                runtimeKeys.Sort();
                for (int i = 0; i < runtimeKeys.Count; i++)
                {
                    int runtimeKey = runtimeKeys[i];
                    int count = sparseCounts[runtimeKey];
                    if (count > 0)
                        sparse.Add(CreateDebugEntry(ESTagId.FromInt32(runtimeKey), count, true));
                }
            }

            return new ESTagDebugSnapshot(
                ESTagRuntimeCatalog.SchemaHash,
                ESTagRuntimeCatalog.RuntimeLayoutHash,
                hotTags.ActiveMask.Bits,
                hot.ToArray(),
                sparse.ToArray(),
                lastChange,
                lastRejected);
        }

        public bool TryCreateStableSnapshot(ESTagStableTransferScope scope, out ESTagStableSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            if (scope != ESTagStableTransferScope.SaveGame && scope != ESTagStableTransferScope.Network)
            {
                error = "A stable Tag snapshot requires exactly one scope: SaveGame or Network.";
                return false;
            }

            if (!ESTagRuntimeCatalog.IsBound)
            {
                error = "Tag Catalog is not bound; a stable Tag snapshot cannot declare its SchemaHash.";
                return false;
            }

            var stableTags = new List<ESTagStableReference>(8);
            AddTransferableHotTags(scope, stableTags);
            AddTransferableSparseTags(scope, stableTags);
            stableTags.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
            snapshot = new ESTagStableSnapshot(ESTagRuntimeCatalog.SchemaHash, stableTags);
            return true;
        }

        internal bool Release(ESTagId tag)
        {
            if (disposed)
                return false;

            int previous = GetCount(tag);
            if (previous <= 0)
                return false;

            int current = previous - 1;
            SetCountUnchecked(tag, current);
            NotifyChanged(tag, previous, current);
            return true;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            Clear();
            disposed = true;
            OnTagCountChanged = null;
            OnTagPresenceChanged = null;
        }

        private bool TryAdd(ESTagId tag)
        {
            if (disposed || !CanWrite(tag))
            {
                RecordRejected(tag, GetStableKeyOrEmpty(tag), "Tag is not runtime-available in the active Tag Catalog.");
                return false;
            }

            int previous = GetCount(tag);
            if ((IsHot(tag) && previous >= byte.MaxValue) || (!IsHot(tag) && previous == int.MaxValue))
                return false;

            int current = previous + 1;
            SetCountUnchecked(tag, current);
            NotifyChanged(tag, previous, current);
            return true;
        }

        private bool CanWrite(ESTagId tag)
        {
            return tag.IsValid
                   && ESTagRuntimeCatalog.TryGetEntry(tag, out ESTagBakeTable.Entry entry)
                   && entry.availability == ESTagAvailability.Runtime;
        }

        private bool IsHot(ESTagId tag)
        {
            return ESTagRuntimeCatalog.TryGetStorageTier(tag, out ESTagStorageTier tier)
                   ? tier == ESTagStorageTier.HotSlot
                   : tag.Value >= ESTagIdRange.EnumStart && tag.Value <= ESTagIdRange.CoreRuntimeEnd;
        }

        private void SetCountUnchecked(ESTagId tag, int count)
        {
            if (IsHot(tag))
            {
                hotTags.Warmup();
                hotTags.SetCount(tag, (byte)count);
                return;
            }

            sparseCounts ??= new Dictionary<int, int>(4);
            if (count == 0)
                sparseCounts.Remove(tag.Value);
            else
                sparseCounts[tag.Value] = count;
        }

        private bool HasSparseRuntimeKey(int runtimeKey)
        {
            return sparseCounts != null
                   && sparseCounts.TryGetValue(runtimeKey, out int count)
                   && count > 0;
        }

        private void AddTransferableHotTags(ESTagStableTransferScope scope, List<ESTagStableReference> stableTags)
        {
            for (ushort value = ESTagIdRange.EnumStart; value <= ESTagIdRange.CoreRuntimeEnd; value++)
            {
                ESTagId tag = ESTagId.FromInt32(value);
                if (hotTags.GetCount(tag) <= 0
                    || !ESTagRuntimeCatalog.TryGetEntry(tag, out ESTagBakeTable.Entry entry)
                    || (entry.stableTransferScopes & scope) == 0
                    || !ESTagRuntimeCatalog.TryGetStableReference(tag, out ESTagStableReference reference))
                {
                    continue;
                }

                stableTags.Add(reference);
            }
        }

        private void AddTransferableSparseTags(ESTagStableTransferScope scope, List<ESTagStableReference> stableTags)
        {
            if (sparseCounts == null)
                return;

            foreach (KeyValuePair<int, int> pair in sparseCounts)
            {
                if (pair.Value <= 0)
                    continue;

                ESTagId tag = ESTagId.FromInt32(pair.Key);
                if (ESTagRuntimeCatalog.TryGetEntry(tag, out ESTagBakeTable.Entry entry)
                    && (entry.stableTransferScopes & scope) != 0
                    && ESTagRuntimeCatalog.TryGetStableReference(tag, out ESTagStableReference reference))
                {
                    stableTags.Add(reference);
                }
            }
        }

        private ESTagDebugEntry CreateDebugEntry(ESTagId tag, int count, bool isSparse)
        {
            string stableReference = ESTagRuntimeCatalog.TryGetStableReference(tag, out ESTagStableReference reference)
                ? reference.ToString()
                : string.Empty;
            return new ESTagDebugEntry(tag, stableReference, count, isSparse);
        }

        private void NotifyChanged(ESTagId tag, int previous, int current)
        {
            if (previous == current)
                return;

            lastChange = new ESTagTagChangeDebugInfo(tag, previous, current);
            OnTagCountChanged?.Invoke(tag, previous, current);
            if ((previous == 0) != (current == 0))
                OnTagPresenceChanged?.Invoke(tag, current > 0);
        }

        private void RecordRejected(ESTagId tag, string stableReference, string reason)
        {
            lastRejected = new ESTagTagRejectedDebugInfo(tag, stableReference, reason);
        }

        private static string GetStableKeyOrEmpty(ESTagId tag)
        {
            return ESTagRuntimeCatalog.TryGetStableReference(tag, out ESTagStableReference reference)
                ? reference.ToString()
                : string.Empty;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ESTagCollection));
        }
    }

    public sealed class ESTagDebugSnapshot
    {
        public static readonly ESTagDebugSnapshot Empty = new ESTagDebugSnapshot(
            string.Empty,
            string.Empty,
            0UL,
            Array.Empty<ESTagDebugEntry>(),
            Array.Empty<ESTagDebugEntry>(),
            default,
            default);

        public string SchemaHash { get; }
        public string RuntimeLayoutHash { get; }
        public ulong HotMask { get; }
        public IReadOnlyList<ESTagDebugEntry> HotTags { get; }
        public IReadOnlyList<ESTagDebugEntry> SparseTags { get; }
        public ESTagTagChangeDebugInfo LastChange { get; }
        public ESTagTagRejectedDebugInfo LastRejected { get; }

        internal ESTagDebugSnapshot(
            string schemaHash,
            string runtimeLayoutHash,
            ulong hotMask,
            IReadOnlyList<ESTagDebugEntry> hotTags,
            IReadOnlyList<ESTagDebugEntry> sparseTags,
            ESTagTagChangeDebugInfo lastChange,
            ESTagTagRejectedDebugInfo lastRejected)
        {
            SchemaHash = schemaHash ?? string.Empty;
            RuntimeLayoutHash = runtimeLayoutHash ?? string.Empty;
            HotMask = hotMask;
            HotTags = hotTags ?? Array.Empty<ESTagDebugEntry>();
            SparseTags = sparseTags ?? Array.Empty<ESTagDebugEntry>();
            LastChange = lastChange;
            LastRejected = lastRejected;
        }
    }

    public sealed class ESTagDebugEntry
    {
        public ESTagId Tag { get; }
        public string StableReference { get; }
        public int Count { get; }
        public bool IsSparse { get; }

        internal ESTagDebugEntry(ESTagId tag, string stableReference, int count, bool isSparse)
        {
            Tag = tag;
            StableReference = stableReference ?? string.Empty;
            Count = count;
            IsSparse = isSparse;
        }
    }

    public struct ESTagTagChangeDebugInfo
    {
        public ESTagId Tag { get; }
        public int PreviousCount { get; }
        public int CurrentCount { get; }
        public bool IsValid => Tag.IsValid;

        internal ESTagTagChangeDebugInfo(ESTagId tag, int previousCount, int currentCount)
        {
            Tag = tag;
            PreviousCount = previousCount;
            CurrentCount = currentCount;
        }
    }

    public struct ESTagTagRejectedDebugInfo
    {
        public ESTagId Tag { get; }
        public string StableReference { get; }
        public string Reason { get; }
        public bool IsValid => !string.IsNullOrEmpty(Reason);

        internal ESTagTagRejectedDebugInfo(ESTagId tag, string stableReference, string reason)
        {
            Tag = tag;
            StableReference = stableReference ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}
