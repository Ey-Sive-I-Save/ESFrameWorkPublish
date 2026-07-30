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

        public ESTagId Tag { get; }
        public object Source { get; private set; }
        public bool IsActive => owner != null;

        internal ESTagLease(ESTagCollection owner, ESTagId tag, object source)
        {
            this.owner = owner;
            Tag = tag;
            Source = source;
        }

        public bool Release()
        {
            ESTagCollection currentOwner = owner;
            owner = null;
            Source = null;
            return currentOwner != null && currentOwner.Release(Tag);
        }

        public void Dispose()
        {
            Release();
        }
    }

    /// <summary>
    /// Authored Tag grants for one ownership boundary, such as a Buff instance, equipped item,
    /// movement mode, or trigger zone. The active Catalog resolves stable identities on apply.
    /// </summary>
    [Serializable]
    public sealed class ESTagGrantConfig
    {
        [Tooltip("Stable Tag declarations to acquire while this owner is active. EnumKey/StringKey and HotSlot/Sparse are resolved by the active Catalog.")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetTagOptions))]
#endif
        public List<ESTagStableReference> tags = new List<ESTagStableReference>();

#if UNITY_EDITOR
        private IEnumerable<ValueDropdownItem<ESTagStableReference>> GetTagOptions()
        {
            return ESTagEditorCatalogCache.GetTagOptions();
        }
#endif

        public bool IsEmpty => tags == null || tags.Count == 0;

        public bool TryValidate(out string error)
        {
            error = null;
            if (tags == null)
                return true;

            var seenReferences = new HashSet<ESTagStableReference>();
            var seenRuntimeKeys = ESTagRuntimeCatalog.IsBound ? new HashSet<int>() : null;
            for (int i = 0; i < tags.Count; i++)
            {
                ESTagStableReference reference = tags[i];
                if (reference.IsEmpty || !seenReferences.Add(reference))
                {
                    error = "tags[" + i + "] is empty or duplicated.";
                    return false;
                }

                if (seenRuntimeKeys != null
                    && ESTagRuntimeCatalog.TryGetRuntimeKey(reference, out int runtimeKey)
                    && !seenRuntimeKeys.Add(runtimeKey))
                {
                    error = "tags contains multiple stable aliases for RuntimeKey " + runtimeKey + ".";
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Owns a group of acquired Tag leases. Reapplying rolls back the prior group first, and a
    /// partial failed application rolls back only leases acquired by this set.
    /// </summary>
    public sealed class ESTagLeaseSet : IDisposable
    {
        private readonly List<ESTagLease> leases = new List<ESTagLease>(4);

        public int Count => leases.Count;

        public bool TryAcquire(ESTagCollection collection, ESTagGrantConfig config, object source, out string error)
        {
            ReleaseAll();
            if (collection == null)
            {
                error = "Cannot acquire Tag grants for a null Tag collection.";
                return false;
            }

            if (config == null)
            {
                error = "Tag grant configuration is null.";
                return false;
            }

            if (!config.TryValidate(out error))
                return false;

            object leaseSource = source ?? this;
            if (config.tags != null)
            {
                for (int i = 0; i < config.tags.Count; i++)
                {
                    ESTagLease lease = collection.Acquire(config.tags[i], leaseSource);
                    if (lease == null)
                    {
                        ReleaseAll();
                        error = "Tag grant was rejected: " + config.tags[i] + ".";
                        return false;
                    }
                    leases.Add(lease);
                }
            }

            error = null;
            return true;
        }

        public void ReleaseAll()
        {
            for (int i = leases.Count - 1; i >= 0; i--)
                leases[i]?.Dispose();
            leases.Clear();
        }

        public void Dispose()
        {
            ReleaseAll();
        }
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

            if (!TryCreateGrantConfig(scope, out ESTagGrantConfig grants, out error))
                return false;

            return ownership.TryAcquire(collection, grants, source, out error);
        }

        public bool TryCreateGrantConfig(ESTagStableTransferScope scope, out ESTagGrantConfig grants, out string error)
        {
            grants = null;
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

            grants = new ESTagGrantConfig();
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

                grants.tags.Add(reference);
            }

            return true;
        }
    }
}
