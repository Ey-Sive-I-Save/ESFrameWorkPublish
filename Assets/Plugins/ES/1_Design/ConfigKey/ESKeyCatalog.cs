using System;
using System.Collections.Generic;
using System.Text;

namespace ES
{
    /// <summary>
    /// A stable business key may expose an enum alias, a string alias, or both. When both are
    /// present, a catalog binds them to one definition and one process-local RuntimeKey.
    /// </summary>
    [Serializable]
    public struct ESStableKey : IEquatable<ESStableKey>
    {
        public string scope;
        public ushort enumKey;
        public string stringKey;

        public ESStableKey(string scope, ushort enumKey, string stringKey)
        {
            this.scope = scope;
            this.enumKey = enumKey;
            this.stringKey = stringKey;
        }

        public bool HasEnumKey => enumKey != 0;
        public bool HasStringKey => !string.IsNullOrEmpty(stringKey);
        public bool IsConfigured => HasEnumKey || HasStringKey;

        public bool Equals(ESStableKey other)
        {
            return enumKey == other.enumKey
                   && string.Equals(scope, other.scope, StringComparison.Ordinal)
                   && string.Equals(stringKey, other.stringKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ESStableKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = enumKey;
                hash = (hash * 397) ^ (scope != null ? StringComparer.Ordinal.GetHashCode(scope) : 0);
                hash = (hash * 397) ^ (stringKey != null ? StringComparer.Ordinal.GetHashCode(stringKey) : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return Describe(scope, enumKey, stringKey);
        }

        public static bool Matches(ESStableKey left, ESStableKey right)
        {
            if (!string.Equals(left.scope, right.scope, StringComparison.Ordinal))
                return false;

            bool enumMatches = left.HasEnumKey && right.HasEnumKey && left.enumKey == right.enumKey;
            bool stringMatches = left.HasStringKey && right.HasStringKey
                                 && string.Equals(left.stringKey, right.stringKey, StringComparison.Ordinal);

            if (left.HasEnumKey && right.HasEnumKey && !enumMatches)
                return false;
            if (left.HasStringKey && right.HasStringKey && !stringMatches)
                return false;

            return enumMatches || stringMatches;
        }

        public static string Describe(string scope, ushort enumKey, string stringKey)
        {
            string identity;
            if (enumKey != 0 && !string.IsNullOrEmpty(stringKey))
                identity = "Enum=" + enumKey + " | String=" + stringKey;
            else if (enumKey != 0)
                identity = "Enum=" + enumKey;
            else
                identity = "String=" + (stringKey ?? string.Empty);

            return string.IsNullOrEmpty(scope) ? identity : scope + "/" + identity;
        }
    }

    public enum ESKeyCatalogKind : byte
    {
        Config = 0,
        Attribute = 1,
        GameTag = 2,
        Asset = 3
    }

    /// <summary>Storage policy changes allocation only. It never changes stable-key authority.</summary>
    public enum ESKeyStoragePolicy : byte
    {
        Default = 0,
        HotSlot = 1,
        Sparse = 2
    }

    /// <summary>Shared value vocabulary used in catalog schema validation.</summary>
    public enum ESKeyValueKind : byte
    {
        None = 0,
        Flag = 1,
        Int = 2,
        Float = 3,
        String = 4,
        Object = 5
    }

    [Flags]
    public enum ESKeyUsageKind : byte
    {
        None = 0,
        Declared = 1,
        Read = 2,
        Write = 4
    }

    /// <summary>
    /// Complete declaration metadata. schemaSignature must carry domain-specific details such as
    /// default/min/max/formula/migration revision; consumers are not allowed to own those rules.
    /// </summary>
    [Serializable]
    public struct ESKeyDeclaration
    {
        public ESStableKey key;
        public ESKeyCatalogKind kind;
        public ESKeyValueKind valueKind;
        public ESKeyStoragePolicy storagePolicy;
        public string schemaSignature;
        public string declaredBy;

        public bool IsConfigured => key.IsConfigured;

        internal bool HasSameSchema(ESKeyDeclaration other)
        {
            return kind == other.kind
                   && valueKind == other.valueKind
                   && storagePolicy == other.storagePolicy
                   && string.Equals(schemaSignature ?? string.Empty, other.schemaSignature ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Serializable]
    public struct ESKeyCatalogEntry
    {
        public ESStableKey key;
        public int runtimeKey;
        public ESKeyCatalogKind kind;
        public ESKeyValueKind valueKind;
        public ESKeyStoragePolicy storagePolicy;
        public string schemaSignature;
    }

    [Serializable]
    public struct ESKeyCatalogIssue
    {
        public string message;
        public ESStableKey key;
    }

    [Serializable]
    public struct ESKeyCatalogHandshake
    {
        public string catalogName;
        public string schemaHash;
    }

    /// <summary>Structured editor diagnostic snapshot. Owners are sorted, comma-separated source names.</summary>
    public struct ESKeyCatalogUsageSnapshot
    {
        public ESKeyCatalogEntry entry;
        public string declaredBy;
        public string readBy;
        public string writtenBy;

        public bool IsUnused => string.IsNullOrEmpty(readBy) && string.IsNullOrEmpty(writtenBy);
    }

    /// <summary>
    /// Deterministic catalog for stable business keys. Build sorts stable identities before dense
    /// RuntimeKeys are assigned, so registration order never becomes part of the runtime ABI.
    /// RuntimeKeys are deliberately omitted from this catalog's serialized/network contract.
    /// </summary>
    public sealed class ESKeyCatalog
    {
        private sealed class MutableEntry
        {
            public ESKeyDeclaration declaration;
            public readonly HashSet<string> declarers = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> readers = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> writers = new HashSet<string>(StringComparer.Ordinal);
            public int runtimeKey;
        }

        private readonly string catalogName;
        private readonly string requiredScope;
        private readonly List<ESKeyDeclaration> pendingDeclarations = new List<ESKeyDeclaration>(64);
        private readonly List<ESKeyCatalogEntry> entries = new List<ESKeyCatalogEntry>(64);
        private readonly List<MutableEntry> mutableEntries = new List<MutableEntry>(64);
        private readonly Dictionary<string, int> entryByEnum = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> entryByString = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<ESKeyCatalogIssue> issues = new List<ESKeyCatalogIssue>(8);
        private bool isBuilt;
        private ulong schemaHash;

        public ESKeyCatalog(string catalogName, string requiredScope = null)
        {
            if (string.IsNullOrWhiteSpace(catalogName))
                throw new ArgumentException("Catalog name must be configured.", nameof(catalogName));

            this.catalogName = catalogName;
            this.requiredScope = requiredScope;
        }

        public string CatalogName => catalogName;
        public string RequiredScope => requiredScope;
        public bool IsBuilt => isBuilt;
        public int Count => entries.Count;
        public string SchemaHash => schemaHash.ToString("X16");
        public IReadOnlyList<ESKeyCatalogEntry> Entries => entries;
        public IReadOnlyList<ESKeyCatalogIssue> Issues => issues;

        public void Clear()
        {
            pendingDeclarations.Clear();
            entries.Clear();
            mutableEntries.Clear();
            entryByEnum.Clear();
            entryByString.Clear();
            issues.Clear();
            schemaHash = 0UL;
            isBuilt = false;
        }

        public void Declare(ESKeyDeclaration declaration)
        {
            if (isBuilt)
                throw new InvalidOperationException("Catalog is already built. Clear it before declaring a new schema.");
            if (!declaration.IsConfigured)
                throw new InvalidOperationException("A catalog declaration must contain EnumKey or StringKey.");
            if (string.IsNullOrWhiteSpace(declaration.key.scope))
                throw new InvalidOperationException("A catalog declaration must contain a stable scope.");
            if (!string.IsNullOrEmpty(requiredScope)
                && !string.Equals(requiredScope, declaration.key.scope, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Declaration scope does not match catalog scope: " + declaration.key.scope);
            }

            pendingDeclarations.Add(declaration);
        }

        public bool TryBuild(out string error)
        {
            ClearBuildOutput();
            error = null;

            pendingDeclarations.Sort(CompareDeclarations);
            for (int i = 0; i < pendingDeclarations.Count; i++)
            {
                ESKeyDeclaration declaration = pendingDeclarations[i];
                if (!TryMerge(declaration, out string mergeError))
                {
                    AddIssue(declaration.key, mergeError);
                    continue;
                }
            }

            if (issues.Count > 0)
            {
                error = BuildIssueText();
                return false;
            }

            mutableEntries.Sort(CompareMutableEntries);
            for (int i = 0; i < mutableEntries.Count; i++)
            {
                MutableEntry mutable = mutableEntries[i];
                mutable.runtimeKey = i + 1;
                ESKeyCatalogEntry entry = new ESKeyCatalogEntry
                {
                    key = mutable.declaration.key,
                    runtimeKey = mutable.runtimeKey,
                    kind = mutable.declaration.kind,
                    valueKind = mutable.declaration.valueKind,
                    storagePolicy = mutable.declaration.storagePolicy,
                    schemaSignature = mutable.declaration.schemaSignature ?? string.Empty
                };
                entries.Add(entry);
                if (entry.key.HasEnumKey)
                    entryByEnum.Add(GetEnumLookupKey(entry.key.scope, entry.key.enumKey), i);
                if (entry.key.HasStringKey)
                    entryByString.Add(GetStringLookupKey(entry.key.scope, entry.key.stringKey), i);
            }

            schemaHash = ComputeSchemaHash();
            isBuilt = true;
            return true;
        }

        public void BuildOrThrow()
        {
            if (!TryBuild(out string error))
                throw new InvalidOperationException(error);
        }

        public bool TryGetRuntimeKey(ESStableKey key, out int runtimeKey)
        {
            runtimeKey = 0;
            if (!isBuilt || !MatchesCatalogScope(key))
                return false;

            int enumIndex = -1;
            int stringIndex = -1;
            if (key.HasEnumKey && !entryByEnum.TryGetValue(GetEnumLookupKey(key.scope, key.enumKey), out enumIndex))
                return false;
            if (key.HasStringKey && !entryByString.TryGetValue(GetStringLookupKey(key.scope, key.stringKey), out stringIndex))
                return false;
            if (enumIndex >= 0 && stringIndex >= 0 && enumIndex != stringIndex)
                return false;

            int index = enumIndex >= 0 ? enumIndex : stringIndex;
            if (index < 0 || (uint)index >= (uint)entries.Count)
                return false;

            runtimeKey = entries[index].runtimeKey;
            return true;
        }

        public bool TryGetEntry(int runtimeKey, out ESKeyCatalogEntry entry)
        {
            int index = runtimeKey - 1;
            if (isBuilt && (uint)index < (uint)entries.Count)
            {
                entry = entries[index];
                return true;
            }

            entry = default;
            return false;
        }

        /// <summary>Editor/diagnostic path only. Do not call this on a hot per-frame read.</summary>
        public bool RecordUsage(ESStableKey key, ESKeyUsageKind usage, string owner)
        {
            if (!TryGetRuntimeKey(key, out int runtimeKey))
                return false;

            return RecordUsage(runtimeKey, usage, owner);
        }

        /// <summary>Editor/diagnostic path for a RuntimeKey already resolved at a system boundary.</summary>
        public bool RecordUsage(int runtimeKey, ESKeyUsageKind usage, string owner)
        {
            int index = runtimeKey - 1;
            if (!isBuilt || (uint)index >= (uint)mutableEntries.Count)
                return false;

            MutableEntry entry = mutableEntries[index];
            string source = string.IsNullOrEmpty(owner) ? "<unspecified>" : owner;
            if ((usage & ESKeyUsageKind.Declared) != 0) entry.declarers.Add(source);
            if ((usage & ESKeyUsageKind.Read) != 0) entry.readers.Add(source);
            if ((usage & ESKeyUsageKind.Write) != 0) entry.writers.Add(source);
            return true;
        }

        public string GetUsageReport()
        {
            if (!isBuilt)
                return "Catalog has not been built.";

            StringBuilder builder = new StringBuilder(entries.Count * 96);
            for (int i = 0; i < entries.Count; i++)
            {
                MutableEntry mutable = mutableEntries[i];
                builder.Append(entries[i].runtimeKey).Append(' ')
                    .Append(entries[i].key).Append(" declared=")
                    .Append(Join(mutable.declarers)).Append(" read=")
                    .Append(Join(mutable.readers)).Append(" write=")
                    .Append(Join(mutable.writers));
                if (mutable.readers.Count == 0 && mutable.writers.Count == 0)
                    builder.Append(" unused");
                builder.AppendLine();
            }

            return builder.ToString();
        }

        public void CopyUsageSnapshots(List<ESKeyCatalogUsageSnapshot> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (!isBuilt)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                MutableEntry mutable = mutableEntries[i];
                destination.Add(new ESKeyCatalogUsageSnapshot
                {
                    entry = entries[i],
                    declaredBy = Join(mutable.declarers),
                    readBy = Join(mutable.readers),
                    writtenBy = Join(mutable.writers)
                });
            }
        }

        public ESKeyCatalogHandshake CreateHandshake()
        {
            if (!isBuilt)
                throw new InvalidOperationException("Catalog must be built before a handshake is created.");

            return new ESKeyCatalogHandshake { catalogName = catalogName, schemaHash = SchemaHash };
        }

        public bool IsCompatibleWith(ESKeyCatalogHandshake peer, out string error)
        {
            if (!isBuilt)
            {
                error = "Local catalog is not built.";
                return false;
            }

            if (!string.Equals(catalogName, peer.catalogName, StringComparison.Ordinal))
            {
                error = "Catalog name mismatch. local=" + catalogName + ", peer=" + peer.catalogName;
                return false;
            }

            if (!string.Equals(SchemaHash, peer.schemaHash, StringComparison.OrdinalIgnoreCase))
            {
                error = "Catalog schema mismatch. local=" + SchemaHash + ", peer=" + (peer.schemaHash ?? string.Empty);
                return false;
            }

            error = null;
            return true;
        }

        private void ClearBuildOutput()
        {
            entries.Clear();
            mutableEntries.Clear();
            entryByEnum.Clear();
            entryByString.Clear();
            issues.Clear();
            schemaHash = 0UL;
            isBuilt = false;
        }

        private bool TryMerge(ESKeyDeclaration declaration, out string error)
        {
            error = null;
            int enumIndex = -1;
            int stringIndex = -1;
            for (int i = 0; i < mutableEntries.Count; i++)
            {
                ESStableKey existing = mutableEntries[i].declaration.key;
                if (!string.Equals(declaration.key.scope, existing.scope, StringComparison.Ordinal))
                    continue;
                if (declaration.key.HasEnumKey && existing.HasEnumKey && declaration.key.enumKey == existing.enumKey)
                    enumIndex = i;
                if (declaration.key.HasStringKey && existing.HasStringKey
                    && string.Equals(declaration.key.stringKey, existing.stringKey, StringComparison.Ordinal))
                    stringIndex = i;
            }

            if (enumIndex >= 0 && stringIndex >= 0 && enumIndex != stringIndex)
            {
                error = "EnumKey and StringKey are already bound to different definitions.";
                return false;
            }

            int targetIndex = enumIndex >= 0 ? enumIndex : stringIndex;
            if (targetIndex < 0)
            {
                MutableEntry created = new MutableEntry { declaration = declaration };
                if (!string.IsNullOrEmpty(declaration.declaredBy))
                    created.declarers.Add(declaration.declaredBy);
                mutableEntries.Add(created);
                return true;
            }

            MutableEntry existingEntry = mutableEntries[targetIndex];
            if (!ESStableKey.Matches(existingEntry.declaration.key, declaration.key))
            {
                error = "EnumKey and StringKey must keep their existing one-to-one alias binding.";
                return false;
            }

            if (!existingEntry.declaration.HasSameSchema(declaration))
            {
                error = "The same stable key has conflicting kind/value/storage/schema declarations.";
                return false;
            }

            ESStableKey mergedKey = existingEntry.declaration.key;
            if (!mergedKey.HasEnumKey) mergedKey.enumKey = declaration.key.enumKey;
            if (!mergedKey.HasStringKey) mergedKey.stringKey = declaration.key.stringKey;
            existingEntry.declaration.key = mergedKey;
            if (!string.IsNullOrEmpty(declaration.declaredBy))
                existingEntry.declarers.Add(declaration.declaredBy);
            return true;
        }

        private bool MatchesCatalogScope(ESStableKey key)
        {
            return string.IsNullOrEmpty(requiredScope)
                   || string.Equals(requiredScope, key.scope, StringComparison.Ordinal);
        }

        private ulong ComputeSchemaHash()
        {
            ulong hash = ESKeyHash.Fnv1A64("ESKeyCatalog/v1");
            hash = ESKeyHash.Append(hash, catalogName);
            for (int i = 0; i < entries.Count; i++)
            {
                ESKeyCatalogEntry entry = entries[i];
                hash = ESKeyHash.Append(hash, entry.key.scope);
                hash = ESKeyHash.Append(hash, entry.key.enumKey);
                hash = ESKeyHash.Append(hash, entry.key.stringKey);
                hash = ESKeyHash.Append(hash, (byte)entry.kind);
                hash = ESKeyHash.Append(hash, (byte)entry.valueKind);
                hash = ESKeyHash.Append(hash, (byte)entry.storagePolicy);
                hash = ESKeyHash.Append(hash, entry.schemaSignature);
            }

            return hash;
        }

        private void AddIssue(ESStableKey key, string message)
        {
            issues.Add(new ESKeyCatalogIssue { key = key, message = message });
        }

        private string BuildIssueText()
        {
            StringBuilder builder = new StringBuilder("[ESKeyCatalog] Build failed: ");
            for (int i = 0; i < issues.Count; i++)
            {
                if (i > 0) builder.Append(" | ");
                builder.Append(issues[i].key).Append(": ").Append(issues[i].message);
            }

            return builder.ToString();
        }

        private static int CompareDeclarations(ESKeyDeclaration left, ESKeyDeclaration right)
        {
            return CompareStableKeys(left.key, right.key);
        }

        private static int CompareMutableEntries(MutableEntry left, MutableEntry right)
        {
            return CompareStableKeys(left.declaration.key, right.declaration.key);
        }

        private static int CompareStableKeys(ESStableKey left, ESStableKey right)
        {
            int scope = string.CompareOrdinal(left.scope, right.scope);
            if (scope != 0) return scope;
            int enumValue = left.enumKey.CompareTo(right.enumKey);
            return enumValue != 0 ? enumValue : string.CompareOrdinal(left.stringKey, right.stringKey);
        }

        private static string Join(HashSet<string> values)
        {
            if (values == null || values.Count == 0)
                return "-";

            List<string> ordered = new List<string>(values);
            ordered.Sort(StringComparer.Ordinal);
            return string.Join(",", ordered);
        }

        private static string GetEnumLookupKey(string scope, ushort enumKey)
        {
            return (scope ?? string.Empty) + "\u001F" + enumKey;
        }

        private static string GetStringLookupKey(string scope, string stringKey)
        {
            return (scope ?? string.Empty) + "\u001F" + (stringKey ?? string.Empty);
        }
    }

    internal static class ESKeyHash
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Fnv1A64(string value)
        {
            return Append(OffsetBasis, value);
        }

        public static ulong Append(ulong hash, string value)
        {
            if (value == null)
                return Append(hash, (byte)0);

            for (int i = 0; i < value.Length; i++)
            {
                char valueChar = value[i];
                hash = Append(hash, (byte)valueChar);
                hash = Append(hash, (byte)(valueChar >> 8));
            }

            return Append(hash, (byte)0xFF);
        }

        public static ulong Append(ulong hash, ushort value)
        {
            hash = Append(hash, (byte)value);
            return Append(hash, (byte)(value >> 8));
        }

        public static ulong Append(ulong hash, byte value)
        {
            return (hash ^ value) * Prime;
        }
    }
}
