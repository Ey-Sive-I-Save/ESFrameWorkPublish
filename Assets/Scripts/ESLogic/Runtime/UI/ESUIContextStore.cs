using System;
using System.Collections.Generic;

namespace ES
{
    public readonly struct ESUIContextSnapshot
    {
        public ESUIContextSnapshot(
            ESUICanonicalId canonicalId,
            int schemaVersion,
            string scopeKey,
            string payload,
            DateTimeOffset savedAt)
        {
            CanonicalId = canonicalId;
            SchemaVersion = schemaVersion;
            ScopeKey = scopeKey ?? string.Empty;
            Payload = payload ?? string.Empty;
            SavedAt = savedAt;
        }

        public ESUICanonicalId CanonicalId { get; }
        public int SchemaVersion { get; }
        public string ScopeKey { get; }
        public string Payload { get; }
        public DateTimeOffset SavedAt { get; }
    }

    /// <summary>
    /// Bounded context staging store. It stores serialized, caller-owned payloads only; live
    /// Unity objects, leases and cancellation state must never be placed in a snapshot.
    /// </summary>
    public sealed class ESUIContextStore
    {
        private readonly Dictionary<string, ESUIContextSnapshot> snapshots =
            new Dictionary<string, ESUIContextSnapshot>(StringComparer.Ordinal);

        public int Count => snapshots.Count;

        public bool Stage(ESUIContextSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(snapshot.CanonicalId.Value) || snapshot.SchemaVersion < 1)
                return false;

            snapshots[MakeKey(snapshot.CanonicalId, snapshot.ScopeKey)] = snapshot;
            return true;
        }

        public bool TryTake(
            ESUICanonicalId canonicalId,
            string scopeKey,
            int expectedSchemaVersion,
            out ESUIContextSnapshot snapshot)
        {
            if (!TryPeek(canonicalId, scopeKey, expectedSchemaVersion, out snapshot)) return false;
            snapshots.Remove(MakeKey(canonicalId, scopeKey));
            return true;
        }

        /// <summary>Reads a compatible snapshot without consuming it.</summary>
        public bool TryPeek(
            ESUICanonicalId canonicalId,
            string scopeKey,
            int expectedSchemaVersion,
            out ESUIContextSnapshot snapshot)
        {
            string key = MakeKey(canonicalId, scopeKey);
            if (!snapshots.TryGetValue(key, out snapshot)) return false;
            if (expectedSchemaVersion < 1 || snapshot.SchemaVersion != expectedSchemaVersion)
            {
                snapshot = default;
                return false;
            }
            return true;
        }

        /// <summary>Consumes a snapshot only after the caller has successfully committed it.</summary>
        public bool Consume(ESUIContextSnapshot snapshot)
        {
            string key = MakeKey(snapshot.CanonicalId, snapshot.ScopeKey);
            if (!snapshots.TryGetValue(key, out ESUIContextSnapshot current) || !current.Equals(snapshot))
                return false;
            snapshots.Remove(key);
            return true;
        }

        public bool Discard(ESUICanonicalId canonicalId, string scopeKey) =>
            snapshots.Remove(MakeKey(canonicalId, scopeKey));

        public void Clear() => snapshots.Clear();

        private static string MakeKey(ESUICanonicalId canonicalId, string scopeKey)
        {
            if (string.IsNullOrWhiteSpace(canonicalId.Value))
                throw new ArgumentException("UI CanonicalId 不能为空。", nameof(canonicalId));

            return canonicalId.Value + "\u001f" + (scopeKey ?? string.Empty);
        }
    }
}
