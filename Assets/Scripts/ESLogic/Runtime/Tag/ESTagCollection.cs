using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
        private ulong ownHotMask;
        private HashSet<int> ownSparseTags;
        private ESTagTagChangeDebugInfo lastChange;
        private ESTagTagRejectedDebugInfo lastRejected;
        private ESTagObserverExceptionDebugInfo lastObserverException;
        private LinkReceiveList<ESTagCountChangedLink> countChangedLinks;
        private LinkReceiveList<ESTagPresenceChangedLink> presenceChangedLinks;
        private List<KeyValuePair<ESTagId, int>> clearBuffer;
        private Queue<ESTagTagChangeDebugInfo> notificationQueue;
        private ulong generation;
        private bool isClearing;
        private bool isNotifying;
        private bool clearRequested;
        private bool clearDiagnosticsRequested;
        private bool disposed;

        public ESTagCollection() { }

        /// <summary>Registers a Count-change Link receiver. Duplicate registrations are rejected.</summary>
        public bool AddCountChangedReceiver(IReceiveLink<ESTagCountChangedLink> receiver)
        {
            if (receiver == null)
                return false;

            return GetOrCreateCountChangedLinks().AddReceiver(receiver);
        }

        /// <summary>Unregisters a Count-change Link receiver. During dispatch it takes effect next round.</summary>
        public bool RemoveCountChangedReceiver(IReceiveLink<ESTagCountChangedLink> receiver)
        {
            return receiver != null && countChangedLinks != null && countChangedLinks.RemoveReceiver(receiver);
        }

        /// <summary>Registers a presence-change Link receiver. Duplicate registrations are rejected.</summary>
        public bool AddPresenceChangedReceiver(IReceiveLink<ESTagPresenceChangedLink> receiver)
        {
            if (receiver == null)
                return false;

            return GetOrCreatePresenceChangedLinks().AddReceiver(receiver);
        }

        /// <summary>Unregisters a presence-change Link receiver. During dispatch it takes effect next round.</summary>
        public bool RemovePresenceChangedReceiver(IReceiveLink<ESTagPresenceChangedLink> receiver)
        {
            return receiver != null && presenceChangedLinks != null && presenceChangedLinks.RemoveReceiver(receiver);
        }

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
            if (!TryAcquireCore(tag, out ulong leaseGeneration))
                return null;

            return new ESTagLease(this, tag, leaseGeneration, source);
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

        /// <summary>
        /// Sets the Tag contribution owned directly by this collection's Host. The operation is
        /// idempotent and handle-free; disabling it removes only the Host's own single increment.
        /// External systems must use ESTagLease or ESTagLeaseSet instead.
        /// </summary>
        public bool SetTag(ESGameTag tag, bool active)
        {
            return SetTag(ESTagId.FromInt32((ushort)tag), active);
        }

        /// <summary>Sets one handle-free Host-owned Tag contribution.</summary>
        public bool SetTag(ESTagStableReference reference, bool active)
        {
            ThrowIfDisposed();
            if (!ESTagRuntimeCatalog.TryGetRuntimeKey(reference, out int runtimeKey))
            {
                RecordRejected(ESTagId.Invalid, reference.ToString(),
                    ESTagRuntimeCatalog.IsBound
                        ? "Stable Tag reference is not registered by the active Tag Catalog."
                        : "Tag Catalog is not bound.");
                return false;
            }

            return SetTag(ESTagId.FromInt32(runtimeKey), active);
        }

        /// <summary>Sets one handle-free Host-owned Tag contribution.</summary>
        public bool SetTag(ESTagId tag, bool active)
        {
            ThrowIfDisposed();
            if (active)
            {
                if (HasOwnTag(tag))
                    return true;
                if (isClearing)
                {
                    RecordRejected(tag, GetStableKeyOrEmpty(tag),
                        "Tag writes are rejected while the collection is clearing.");
                    return false;
                }
                if (!CanWrite(tag))
                {
                    RecordRejected(tag, GetStableKeyOrEmpty(tag),
                        "Tag is not runtime-available in the active Tag Catalog.");
                    return false;
                }

                ulong currentGeneration = generation;
                SetOwnTagState(tag, true);
                if (!TryAdd(tag))
                {
                    if (currentGeneration == generation)
                        SetOwnTagState(tag, false);
                    return false;
                }

                return currentGeneration == generation && HasOwnTag(tag);
            }

            if (!HasOwnTag(tag))
                return true;

            SetOwnTagState(tag, false);
            int previous = GetCount(tag);
            if (previous <= 0)
                return false;

            int current = previous - 1;
            SetCountUnchecked(tag, current);
            NotifyChanged(tag, previous, current);
            return !HasOwnTag(tag);
        }

        /// <summary>Returns whether the Host itself currently supplies this Tag.</summary>
        public bool HasOwnTag(ESTagId tag)
        {
            if (IsHot(tag))
                return (ownHotMask & (1UL << tag.Value)) != 0UL;

            return ownSparseTags != null && ownSparseTags.Contains(tag.Value);
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
            ClearCore(false);
        }

        /// <summary>
        /// Ends one pooled lifetime. Old Leases become permanently stale, counts and diagnostics
        /// are cleared, and the collection retains its internal capacity and registered receivers.
        /// </summary>
        public void ResetForReuse()
        {
            ThrowIfDisposed();
            ClearCore(true);
        }

        private void ClearCore(bool resetDiagnostics)
        {
            if (isClearing)
            {
                clearRequested = true;
                clearDiagnosticsRequested |= resetDiagnostics;
                return;
            }

            isClearing = true;
            clearDiagnosticsRequested = resetDiagnostics;
            try
            {
                do
                {
                    clearRequested = false;
                    unchecked { generation++; }
                    clearBuffer?.Clear();
                    for (ushort value = ESTagIdRange.EnumStart; value <= ESTagIdRange.CoreRuntimeEnd; value++)
                    {
                        ESTagId tag = ESTagId.FromInt32(value);
                        int count = hotTags.GetCount(tag);
                        if (count > 0)
                            AddClearEntry(tag, count);
                    }

                    if (sparseCounts != null)
                    {
                        foreach (KeyValuePair<int, int> pair in sparseCounts)
                        {
                            if (pair.Value > 0)
                                AddClearEntry(ESTagId.FromInt32(pair.Key), pair.Value);
                        }
                    }

                    ownHotMask = 0UL;
                    ownSparseTags?.Clear();
                    hotTags.Clear();
                    sparseCounts?.Clear();
                    if (clearBuffer != null)
                    {
                        for (int i = 0; i < clearBuffer.Count; i++)
                            NotifyChanged(clearBuffer[i].Key, clearBuffer[i].Value, 0);
                    }
                } while (clearRequested);

                if (clearDiagnosticsRequested)
                {
                    lastChange = default;
                    lastRejected = default;
                    lastObserverException = default;
                }
            }
            finally
            {
                clearBuffer?.Clear();
                clearDiagnosticsRequested = false;
                isClearing = false;
            }
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
                lastRejected,
                lastObserverException);
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

        internal bool IsLeaseGenerationCurrent(ulong leaseGeneration)
        {
            return !disposed && generation == leaseGeneration;
        }

        internal bool Release(ESTagId tag, ulong leaseGeneration)
        {
            if (!IsLeaseGenerationCurrent(leaseGeneration))
                return false;

            int previous = GetCount(tag);
            if (previous <= 0)
                return false;

            int current = previous - 1;
            SetCountUnchecked(tag, current);
            NotifyChanged(tag, previous, current);
            return true;
        }

        /// <summary>
        /// Allocation-free acquisition path for ESTagLeaseSet. The token is intentionally
        /// internal: it is owned by one LeaseSet and never exposed as a copyable public handle.
        /// </summary>
        internal bool TryAcquireToken(ESTagStableReference reference, out ESTagLeaseToken token)
        {
            token = default;
            if (!ESTagRuntimeCatalog.TryGetRuntimeKey(reference, out int runtimeKey))
            {
                RecordRejected(ESTagId.Invalid, reference.ToString(),
                    ESTagRuntimeCatalog.IsBound
                        ? "Stable Tag reference is not registered by the active Tag Catalog."
                        : "Tag Catalog is not bound.");
                return false;
            }

            ESTagId tag = ESTagId.FromInt32(runtimeKey);
            if (!TryAcquireCore(tag, out ulong leaseGeneration))
                return false;

            token = new ESTagLeaseToken(this, tag, leaseGeneration);
            return true;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            Clear();
            disposed = true;
            countChangedLinks?.Clear();
            presenceChangedLinks?.Clear();
        }

        private bool TryAdd(ESTagId tag)
        {
            if (disposed || isClearing || !CanWrite(tag))
            {
                RecordRejected(tag, GetStableKeyOrEmpty(tag),
                    isClearing
                        ? "Tag writes are rejected while the collection is clearing."
                        : "Tag is not runtime-available in the active Tag Catalog.");
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

        private bool TryAcquireCore(ESTagId tag, out ulong leaseGeneration)
        {
            leaseGeneration = generation;
            if (!TryAdd(tag))
                return false;

            // A synchronous observer can clear this Collection during NotifyChanged. Do not
            // publish a handle that could otherwise later affect the next generation.
            return leaseGeneration == generation;
        }

        private bool CanWrite(ESTagId tag)
        {
            return tag.IsValid
                   && ESTagRuntimeCatalog.TryGetEntry(tag, out ESTagBakeTable.Entry entry)
                   && entry.availability == ESTagAvailability.Runtime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHot(ESTagId tag)
        {
            return tag.Value >= ESTagIdRange.EnumStart && tag.Value <= ESTagIdRange.CoreRuntimeEnd;
        }

        private void SetOwnTagState(ESTagId tag, bool active)
        {
            if (IsHot(tag))
            {
                ulong bit = 1UL << tag.Value;
                if (active)
                    ownHotMask |= bit;
                else
                    ownHotMask &= ~bit;
                return;
            }

            if (active)
            {
                ownSparseTags ??= new HashSet<int>();
                ownSparseTags.Add(tag.Value);
            }
            else
            {
                ownSparseTags?.Remove(tag.Value);
            }
        }

        private void AddClearEntry(ESTagId tag, int count)
        {
            clearBuffer ??= new List<KeyValuePair<ESTagId, int>>(8);
            clearBuffer.Add(new KeyValuePair<ESTagId, int>(tag, count));
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

            ESTagTagChangeDebugInfo change = new ESTagTagChangeDebugInfo(tag, previous, current);
            lastChange = change;
            if (isNotifying)
            {
                notificationQueue ??= new Queue<ESTagTagChangeDebugInfo>(4);
                notificationQueue.Enqueue(change);
                return;
            }

            isNotifying = true;
            try
            {
                DispatchChange(change);
                while (notificationQueue != null && notificationQueue.Count > 0)
                    DispatchChange(notificationQueue.Dequeue());
            }
            finally
            {
                notificationQueue?.Clear();
                isNotifying = false;
            }
        }

        private void DispatchChange(ESTagTagChangeDebugInfo change)
        {
            if (countChangedLinks != null && countChangedLinks.SubscriberCount > 0)
                countChangedLinks.SendLink(new ESTagCountChangedLink(change.Tag, change.PreviousCount, change.CurrentCount));
            if ((change.PreviousCount == 0) != (change.CurrentCount == 0)
                && presenceChangedLinks != null
                && presenceChangedLinks.SubscriberCount > 0)
            {
                presenceChangedLinks.SendLink(new ESTagPresenceChangedLink(change.Tag, change.CurrentCount > 0));
            }
        }

        private LinkReceiveList<ESTagCountChangedLink> GetOrCreateCountChangedLinks()
        {
            if (countChangedLinks != null)
                return countChangedLinks;

            countChangedLinks = new LinkReceiveList<ESTagCountChangedLink>(2)
            {
                OnReceiverException = RecordCountChangedReceiverException
            };
            return countChangedLinks;
        }

        private LinkReceiveList<ESTagPresenceChangedLink> GetOrCreatePresenceChangedLinks()
        {
            if (presenceChangedLinks != null)
                return presenceChangedLinks;

            presenceChangedLinks = new LinkReceiveList<ESTagPresenceChangedLink>(2)
            {
                OnReceiverException = RecordPresenceChangedReceiverException
            };
            return presenceChangedLinks;
        }

        private void RecordCountChangedReceiverException(
            IReceiveLink<ESTagCountChangedLink> _, ESTagCountChangedLink change, Exception exception)
        {
            RecordObserverException(change.Tag, "TagCountChanged", exception);
        }

        private void RecordPresenceChangedReceiverException(
            IReceiveLink<ESTagPresenceChangedLink> _, ESTagPresenceChangedLink change, Exception exception)
        {
            RecordObserverException(change.Tag, "TagPresenceChanged", exception);
        }

        private void RecordObserverException(ESTagId tag, string eventName, Exception exception)
        {
            lastObserverException = new ESTagObserverExceptionDebugInfo(tag, eventName, exception);
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

    /// <summary>Immutable Link payload for one Tag reference-count transition.</summary>
    public readonly struct ESTagCountChangedLink
    {
        public ESTagId Tag { get; }
        public int PreviousCount { get; }
        public int CurrentCount { get; }

        public ESTagCountChangedLink(ESTagId tag, int previousCount, int currentCount)
        {
            Tag = tag;
            PreviousCount = previousCount;
            CurrentCount = currentCount;
        }
    }

    /// <summary>Immutable Link payload for one Tag present/absent transition.</summary>
    public readonly struct ESTagPresenceChangedLink
    {
        public ESTagId Tag { get; }
        public bool IsPresent { get; }

        public ESTagPresenceChangedLink(ESTagId tag, bool isPresent)
        {
            Tag = tag;
            IsPresent = isPresent;
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
            default,
            default);

        public string SchemaHash { get; }
        public string RuntimeLayoutHash { get; }
        public ulong HotMask { get; }
        public IReadOnlyList<ESTagDebugEntry> HotTags { get; }
        public IReadOnlyList<ESTagDebugEntry> SparseTags { get; }
        public ESTagTagChangeDebugInfo LastChange { get; }
        public ESTagTagRejectedDebugInfo LastRejected { get; }
        public ESTagObserverExceptionDebugInfo LastObserverException { get; }

        internal ESTagDebugSnapshot(
            string schemaHash,
            string runtimeLayoutHash,
            ulong hotMask,
            IReadOnlyList<ESTagDebugEntry> hotTags,
            IReadOnlyList<ESTagDebugEntry> sparseTags,
            ESTagTagChangeDebugInfo lastChange,
            ESTagTagRejectedDebugInfo lastRejected,
            ESTagObserverExceptionDebugInfo lastObserverException)
        {
            SchemaHash = schemaHash ?? string.Empty;
            RuntimeLayoutHash = runtimeLayoutHash ?? string.Empty;
            HotMask = hotMask;
            HotTags = hotTags ?? Array.Empty<ESTagDebugEntry>();
            SparseTags = sparseTags ?? Array.Empty<ESTagDebugEntry>();
            LastChange = lastChange;
            LastRejected = lastRejected;
            LastObserverException = lastObserverException;
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

    public struct ESTagObserverExceptionDebugInfo
    {
        public ESTagId Tag { get; }
        public string EventName { get; }
        public string ExceptionType { get; }
        public string Message { get; }
        public bool IsValid => !string.IsNullOrEmpty(EventName);

        internal ESTagObserverExceptionDebugInfo(ESTagId tag, string eventName, Exception exception)
        {
            Tag = tag;
            EventName = eventName ?? string.Empty;
            ExceptionType = exception?.GetType().FullName ?? string.Empty;
            Message = exception?.Message ?? string.Empty;
        }
    }
}
