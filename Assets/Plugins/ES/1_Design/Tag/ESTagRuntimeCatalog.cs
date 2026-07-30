using System;

namespace ES
{
    /// <summary>
    /// Process-local binding for the single authoritative Tag Catalog.
    /// Stable identity may be EnumKey, StringKey, or both aliases. RuntimeKey values are valid
    /// only while this SchemaHash and runtime layout stay bound.
    /// </summary>
    public static class ESTagRuntimeCatalog
    {
        private static ESTagBakeTable table;
        private static string schemaHash;
        private static string runtimeLayoutHash;

        public static bool IsBound => table != null;
        public static string SchemaHash => schemaHash ?? string.Empty;
        public static string RuntimeLayoutHash => runtimeLayoutHash ?? string.Empty;

        public static void Bind(ESTagBakeTable source, string expectedSchemaHash)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryValidate(out string validationError))
                throw new InvalidOperationException("[ESTagCatalog] Catalog validation failed: " + validationError);

            string candidateSchemaHash = source.SchemaHash;
            string candidateLayoutHash = source.RuntimeLayoutHash;
            if (string.IsNullOrEmpty(expectedSchemaHash))
                throw new InvalidOperationException("[ESTagCatalog] Expected SchemaHash is required.");
            if (!string.Equals(candidateSchemaHash, expectedSchemaHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("[ESTagCatalog] SchemaHash mismatch. Expected="
                                                    + expectedSchemaHash + " Actual=" + candidateSchemaHash);
            }

            ValidateCoreAliases(source);
            if (table != null)
            {
                if (!string.Equals(schemaHash, candidateSchemaHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("[ESTagCatalog] A different SchemaHash cannot replace the active Catalog in-process. Restart or explicitly migrate the session.");
                }

                if (!string.Equals(runtimeLayoutHash, candidateLayoutHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("[ESTagCatalog] RuntimeKey layout changed under the active Schema. Existing Entity extension tags would become ambiguous.");
                }
            }

            table = source;
            schemaHash = candidateSchemaHash;
            runtimeLayoutHash = candidateLayoutHash;
        }

        public static bool TryGetRuntimeKey(string stableKey, out int runtimeKey)
        {
            if (table != null && table.TryGetRuntimeKey(stableKey, out runtimeKey))
                return true;

            runtimeKey = 0;
            return false;
        }

        public static bool TryGetRuntimeKey(ESTagStableReference reference, out int runtimeKey)
        {
            if (table != null && table.TryGetRuntimeKey(reference, out runtimeKey))
                return true;

            runtimeKey = 0;
            return false;
        }

        public static bool IsRuntimeAvailableTag(ESTagId tag)
        {
            if (table == null || !table.TryGetEntry(tag, out ESTagBakeTable.Entry entry))
                return false;

            return entry.availability == ESTagAvailability.Runtime;
        }

        public static bool TryGetStorageTier(ESTagId tag, out ESTagStorageTier storageTier)
        {
            if (table != null && table.TryGetEntry(tag, out ESTagBakeTable.Entry entry))
            {
                storageTier = entry.storageTier;
                return true;
            }

            storageTier = default;
            return false;
        }

        public static bool TryGetEntry(ESTagId tag, out ESTagBakeTable.Entry entry)
        {
            if (table != null)
                return table.TryGetEntry(tag, out entry);

            entry = default;
            return false;
        }

        public static bool TryGetStableReference(ESTagId tag, out ESTagStableReference reference)
        {
            if (table != null)
                return table.TryGetStableReference(tag, out reference);

            reference = default;
            return false;
        }

        /// <summary>
        /// Returns the explicit Catalog migration target for a deprecated stable identity. Normal
        /// runtime lookup intentionally does not apply this mapping implicitly.
        /// </summary>
        public static bool TryGetDeprecatedReplacement(ESTagStableReference obsolete, out ESTagStableReference replacement)
        {
            if (table != null)
                return table.TryGetDeprecatedReplacement(obsolete, out replacement);

            replacement = default;
            return false;
        }

        private static void ValidateCoreAliases(ESTagBakeTable source)
        {
            for (ushort value = ESGameTagCatalog.FirstDefinedValue; value <= ESGameTagCatalog.LastDefinedValue; value++)
            {
                if (!source.TryGetId(ESTagEnumGroup.Primary, value, out ESTagId runtimeTag)
                    || !source.TryGetEntry(runtimeTag, out ESTagBakeTable.Entry entry)
                    || entry.enumGroup != ESTagEnumGroup.Primary
                    || entry.enumValue != value)
                {
                    throw new InvalidOperationException("[ESTagCatalog] Missing or invalid primary EnumKey alias for ESGameTag value " + value + ".");
                }
            }
        }
    }
}
