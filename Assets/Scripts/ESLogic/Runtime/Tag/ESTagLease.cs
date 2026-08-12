using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace ES
{
    /// <summary>
    /// One acquired Tag increment. The Lease retains its optional source for lifecycle ownership
    /// and diagnostics; ESTagCollection never retains that source.
    /// </summary>
    public sealed class ESTagLease : IDisposable
    {
        private ESTagCollection owner;
        private readonly ulong generation;

        public ESTagId Tag { get; }
        public object Source { get; private set; }
        public bool IsActive => owner != null && owner.IsLeaseGenerationCurrent(generation);

        internal ESTagLease(ESTagCollection owner, ESTagId tag, ulong generation, object source)
        {
            this.owner = owner;
            Tag = tag;
            this.generation = generation;
            Source = source;
        }

        public bool Release()
        {
            ESTagCollection currentOwner = owner;
            owner = null;
            Source = null;
            return currentOwner != null && currentOwner.Release(Tag, generation);
        }

        public void Dispose()
        {
            Release();
        }
    }

    /// <summary>
    /// Internal, value-type Lease record used only by ESTagLeaseSet. It avoids one managed
    /// ESTagLease allocation per configured Tag while retaining the same generation safety.
    /// It is not a public handle because copied value handles cannot safely own disposal.
    /// </summary>
    internal readonly struct ESTagLeaseToken
    {
        private readonly ESTagCollection owner;
        private readonly ulong generation;

        public ESTagId Tag { get; }
        public bool IsActive => owner != null && owner.IsLeaseGenerationCurrent(generation);

        internal ESTagLeaseToken(ESTagCollection owner, ESTagId tag, ulong generation)
        {
            this.owner = owner;
            Tag = tag;
            this.generation = generation;
        }

        public bool Release()
        {
            return owner != null && owner.Release(Tag, generation);
        }
    }

    /// <summary>
    /// Owns the Tag Leases written by one lifecycle. It accepts the owner's direct Tag list rather
    /// than a second configuration wrapper. A failed replacement never releases the prior set.
    /// </summary>
    public sealed class ESTagLeaseSet : IDisposable
    {
        // A LeaseSet is commonly embedded in every Item/Buff runtime but is often never used.
        // Do not allocate its backing lists until the first successful application.
        private List<ESTagLeaseToken> leases;
        private List<ESTagLeaseToken> releaseBuffer;
        private List<ESTagLeaseToken> applyBuffer;
        private ESTagCollection activeCollection;
        private object activeSource;
        private bool isApplying;
        private bool isReleasing;

        public ESTagLeaseSet()
        {
        }

        public ESTagLeaseSet(int initialCapacity)
        {
            EnsureCapacity(initialCapacity);
        }

        /// <summary>Optional lifecycle source retained by this LeaseSet while it owns Tags.</summary>
        public object Source => activeSource;

        public int Count
        {
            get
            {
                if (leases == null)
                    return 0;

                int activeCount = 0;
                for (int i = 0; i < leases.Count; i++)
                {
                    if (leases[i].IsActive)
                        activeCount++;
                }

                return activeCount;
            }
        }

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= 0)
                return;

            EnsureListCapacity(ref leases, capacity);
            EnsureListCapacity(ref releaseBuffer, capacity);
            EnsureListCapacity(ref applyBuffer, capacity);
        }

        /// <summary>Validates a direct authored Tag list without creating a configuration wrapper.</summary>
        public static bool TryValidateTags(IReadOnlyList<ESTagStableReference> tags, out string error)
        {
            error = null;
            if (tags == null)
                return true;

            for (int i = 0; i < tags.Count; i++)
            {
                ESTagStableReference reference = tags[i];
                if (reference.IsEmpty)
                {
                    error = "tags[" + i + "] is empty or duplicated.";
                    return false;
                }

                int runtimeKey = 0;
                bool hasRuntimeKey = ESTagRuntimeCatalog.IsBound
                                     && ESTagRuntimeCatalog.TryGetRuntimeKey(reference, out runtimeKey);
                for (int previousIndex = 0; previousIndex < i; previousIndex++)
                {
                    ESTagStableReference previous = tags[previousIndex];
                    if (reference.Equals(previous))
                    {
                        error = "tags[" + i + "] is empty or duplicated.";
                        return false;
                    }

                    if (hasRuntimeKey
                        && ESTagRuntimeCatalog.TryGetRuntimeKey(previous, out int previousRuntimeKey)
                        && previousRuntimeKey == runtimeKey)
                    {
                        error = "tags contains multiple stable aliases for RuntimeKey " + runtimeKey + ".";
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Applies an owner's direct Tag list with rollback-safe ownership. The existing active
        /// Leases remain untouched until every new reference has passed validation and acquired
        /// successfully. This does not make Link notifications atomic: receivers can observe
        /// staged additions before the old Leases are released.
        /// </summary>
        public bool TryApply(
            ESTagCollection collection,
            IReadOnlyList<ESTagStableReference> tags,
            object source,
            out string error)
        {
            if (isApplying || isReleasing)
            {
                error = "Reentrant ESTagLeaseSet.TryApply is not allowed while this LeaseSet is applying or releasing.";
                return false;
            }

            if (!TryPrepareApply(collection, tags, out error))
                return false;

            if (ReferenceEquals(activeCollection, collection) && MatchesValidatedTags(tags))
            {
                activeSource = Count > 0 ? source ?? this : null;
                error = null;
                return true;
            }

            isApplying = true;
            try
            {
                if (tags != null)
                {
                    for (int i = 0; i < tags.Count; i++)
                    {
                        if (!collection.TryAcquireToken(tags[i], out ESTagLeaseToken token))
                        {
                            ReleaseApplyBuffer();
                            error = "Tag application was rejected: " + tags[i] + ".";
                            return false;
                        }
                        GetOrCreateApplyBuffer().Add(token);
                    }
                }

                // A callback can clear the collection while a Tag is being applied. In that case
                // every staged Lease is stale and the old set must remain the only valid state.
                for (int i = 0; applyBuffer != null && i < applyBuffer.Count; i++)
                {
                    if (!applyBuffer[i].IsActive)
                    {
                        ReleaseApplyBuffer();
                        error = "Tag collection was reset while applying Tags.";
                        return false;
                    }
                }

                ReleaseAllCore();
                for (int i = 0; applyBuffer != null && i < applyBuffer.Count; i++)
                {
                    if (!applyBuffer[i].IsActive)
                    {
                        ReleaseApplyBuffer();
                        error = "Tag collection was reset while replacing active Tags.";
                        return false;
                    }
                }

                bool hasAppliedTokens = applyBuffer != null && applyBuffer.Count > 0;
                if (hasAppliedTokens)
                {
                    GetOrCreateLeases().AddRange(applyBuffer);
                    applyBuffer.Clear();
                }
                activeCollection = hasAppliedTokens ? collection : null;
                activeSource = hasAppliedTokens ? source ?? this : null;

                error = null;
                return true;
            }
            finally
            {
                if (applyBuffer != null && applyBuffer.Count > 0)
                    ReleaseApplyBuffer();
                isApplying = false;
            }
        }

        public void ReleaseAll()
        {
            if (isApplying || isReleasing)
                return;

            ReleaseAllCore();
        }

        public void Dispose()
        {
            ReleaseAll();
        }

        /// <summary>Returns true only for a currently valid Lease; stale pre-reset Leases do not count.</summary>
        public bool Contains(ESTagId tag)
        {
            if (leases == null)
                return false;

            for (int i = 0; i < leases.Count; i++)
            {
                ESTagLeaseToken lease = leases[i];
                if (lease.IsActive && lease.Tag == tag)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Compares active Leases against a direct Tag list under the current Catalog. It allocates
        /// no mirror collection and is intended for lifecycle idempotence checks.
        /// </summary>
        public bool MatchesTags(IReadOnlyList<ESTagStableReference> tags)
        {
            if (!ESTagRuntimeCatalog.IsBound || !TryValidateTags(tags, out _))
                return false;

            return MatchesValidatedTags(tags);
        }

        private bool MatchesValidatedTags(IReadOnlyList<ESTagStableReference> tags)
        {
            int expectedCount = tags != null ? tags.Count : 0;
            if (Count != expectedCount)
                return false;

            for (int i = 0; i < expectedCount; i++)
            {
                if (!ESTagRuntimeCatalog.TryGetRuntimeKey(tags[i], out int runtimeKey)
                    || !Contains(ESTagId.FromInt32(runtimeKey)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates a stable presence snapshot without Tags currently owned by this LeaseSet.
        /// This is a cold save/network boundary helper: aggregate presence cannot reconstruct
        /// ownership when a definition Tag and a temporary Tag share the same identity.
        /// </summary>
        public bool TryCreateSnapshotWithoutOwnedTags(
            ESTagCollection collection,
            ESTagStableTransferScope scope,
            out ESTagStableSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            if (collection == null)
            {
                error = "Cannot create a Tag snapshot from a null collection.";
                return false;
            }

            if (Count > 0 && !ReferenceEquals(activeCollection, collection))
            {
                error = "Cannot filter a Tag snapshot with a LeaseSet owned by another collection.";
                return false;
            }

            if (!collection.TryCreateStableSnapshot(scope, out ESTagStableSnapshot aggregate, out error))
                return false;

            var remainingTags = new List<ESTagStableReference>(aggregate.Tags.Count);
            for (int i = 0; i < aggregate.Tags.Count; i++)
            {
                ESTagStableReference reference = aggregate.Tags[i];
                if (ESTagRuntimeCatalog.TryGetRuntimeKey(reference, out int runtimeKey)
                    && Contains(ESTagId.FromInt32(runtimeKey)))
                {
                    continue;
                }

                remainingTags.Add(reference);
            }

            snapshot = new ESTagStableSnapshot(aggregate.SchemaHash, remainingTags);
            error = null;
            return true;
        }

        private void ReleaseAllCore()
        {
            if (leases == null || leases.Count == 0)
            {
                activeCollection = null;
                activeSource = null;
                return;
            }

            isReleasing = true;
            List<ESTagLeaseToken> buffer = GetOrCreateReleaseBuffer();
            buffer.AddRange(leases);
            leases.Clear();
            activeCollection = null;
            activeSource = null;
            try
            {
                for (int i = buffer.Count - 1; i >= 0; i--)
                    buffer[i].Release();
            }
            finally
            {
                buffer.Clear();
                isReleasing = false;
            }
        }

        private static bool TryPrepareApply(
            ESTagCollection collection,
            IReadOnlyList<ESTagStableReference> tags,
            out string error)
        {
            if (collection == null)
            {
                error = "Cannot apply Tags to a null Tag collection.";
                return false;
            }

            if (!TryValidateTags(tags, out error))
                return false;

            if (tags == null || tags.Count == 0)
            {
                error = null;
                return true;
            }

            if (!ESTagRuntimeCatalog.IsBound)
            {
                error = "Tag Catalog is not bound.";
                return false;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                if (!ESTagRuntimeCatalog.TryGetRuntimeKey(tags[i], out int runtimeKey)
                    || !ESTagRuntimeCatalog.TryGetEntry(ESTagId.FromInt32(runtimeKey), out ESTagBakeTable.Entry entry)
                    || entry.availability != ESTagAvailability.Runtime)
                {
                    error = "Tag is not runtime-available: " + tags[i] + ".";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private void ReleaseApplyBuffer()
        {
            if (applyBuffer == null)
                return;

            for (int i = applyBuffer.Count - 1; i >= 0; i--)
                applyBuffer[i].Release();
            applyBuffer.Clear();
        }

        private List<ESTagLeaseToken> GetOrCreateLeases()
        {
            return leases ??= new List<ESTagLeaseToken>(4);
        }

        private List<ESTagLeaseToken> GetOrCreateReleaseBuffer()
        {
            return releaseBuffer ??= new List<ESTagLeaseToken>(4);
        }

        private List<ESTagLeaseToken> GetOrCreateApplyBuffer()
        {
            return applyBuffer ??= new List<ESTagLeaseToken>(4);
        }

        private static void EnsureListCapacity(
            ref List<ESTagLeaseToken> buffer,
            int capacity)
        {
            if (buffer == null)
            {
                buffer = new List<ESTagLeaseToken>(capacity);
            }
            else if (buffer.Capacity < capacity)
            {
                buffer.Capacity = capacity;
            }
        }
    }

    /// <summary>Definition-owned Tag application state shared by Entity, Item, and future Tag Hosts.</summary>
    public enum ESTagDefinitionState : byte
    {
        Empty = 0,
        Pending = 1,
        Applied = 2,
        Failed = 3,
    }

    /// <summary>
    /// Stable Tag payload for save or network boundaries. It intentionally represents presence
    /// only: ownership counts and transient sources must be rebuilt by their own domains.
    /// </summary>
    [Serializable]
    public sealed class ESTagStableSnapshot
    {
        [SerializeField] private string schemaHash;
        [SerializeField] private List<ESTagStableReference> tags;

        public string SchemaHash => schemaHash ?? string.Empty;
        public IReadOnlyList<ESTagStableReference> Tags => tags ?? (IReadOnlyList<ESTagStableReference>)Array.Empty<ESTagStableReference>();

        public ESTagStableSnapshot()
        {
            schemaHash = string.Empty;
            tags = new List<ESTagStableReference>();
        }

        public ESTagStableSnapshot(string schemaHash, IEnumerable<ESTagStableReference> tags)
        {
            this.schemaHash = schemaHash ?? string.Empty;
            this.tags = tags != null ? new List<ESTagStableReference>(tags) : new List<ESTagStableReference>();
        }

        public bool TryRestoreTo(ESTagCollection collection, ESTagStableTransferScope scope, ESTagLeaseSet ownership, object source, out string error)
        {
            if (collection == null)
            {
                error = "Cannot restore a stable Tag snapshot to a null Tag collection.";
                return false;
            }

            if (ownership == null)
            {
                error = "Stable Tag restore requires an explicit LeaseSet ownership boundary.";
                return false;
            }

            if (!TryGetRestorableTags(scope, out List<ESTagStableReference> tags, out error))
                return false;

            return ownership.TryApply(collection, tags, source, out error);
        }

        public bool TryGetRestorableTags(ESTagStableTransferScope scope, out List<ESTagStableReference> tags, out string error)
        {
            tags = null;
            error = null;
            if (scope != ESTagStableTransferScope.SaveGame && scope != ESTagStableTransferScope.Network)
            {
                error = "A stable Tag restore requires exactly one scope: SaveGame or Network.";
                return false;
            }

            if (!ESTagRuntimeCatalog.IsBound)
            {
                error = "Tag Catalog is not bound; a stable Tag snapshot cannot be restored.";
                return false;
            }

            if (string.IsNullOrEmpty(SchemaHash)
                || !string.Equals(SchemaHash, ESTagRuntimeCatalog.SchemaHash, StringComparison.Ordinal))
            {
                error = "Stable Tag snapshot SchemaHash does not match the active Tag Catalog.";
                return false;
            }

            tags = new List<ESTagStableReference>(Tags.Count);
            var seenRuntimeKeys = new HashSet<int>();
            for (int i = 0; i < Tags.Count; i++)
            {
                ESTagStableReference reference = Tags[i];
                if (reference.IsEmpty
                    || !ESTagRuntimeCatalog.TryGetRuntimeKey(reference, out int runtimeKey)
                    || !seenRuntimeKeys.Add(runtimeKey)
                    || !ESTagRuntimeCatalog.TryGetEntry(ESTagId.FromInt32(runtimeKey), out ESTagBakeTable.Entry entry)
                    || entry.availability != ESTagAvailability.Runtime
                    || (entry.stableTransferScopes & scope) == 0)
                {
                    error = "Stable Tag snapshot contains an invalid or non-transferable stable Tag: " + reference + ".";
                    return false;
                }

                tags.Add(reference);
            }

            return true;
        }
    }
}
