using System;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Process-local Attribute Catalog binding. Stable Enum/String identities are resolved during
    /// startup; runtime values only retain the resulting current-process keys.
    /// </summary>
    public static class ESAttributeRuntimeCatalog
    {
        private static ESAttributeBakeTable table;
        private static ESSuperAttributeCatalog character;
        private static ESSuperAttributeCatalog item;
        private static string schemaHash;

        public static event Action CatalogBound;

        public static bool IsBound => table != null;
        public static string SchemaHash => schemaHash ?? string.Empty;
        public static ESSuperAttributeCatalog Character => character;
        public static ESSuperAttributeCatalog Item => item;

        // Domain Reload can be disabled in the Editor. Unity still invokes this before every
        // runtime session, which prevents the previous session's Catalog and subscribers from
        // surviving into a newly baked schema.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            table = null;
            character = null;
            item = null;
            schemaHash = null;
            CatalogBound = null;
        }

        public static void Bind(ESAttributeBakeTable source, string expectedSchemaHash)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryValidate(out string validationError))
                throw new InvalidOperationException("[ESAttributeCatalog] Catalog validation failed: " + validationError);
            if (string.IsNullOrEmpty(expectedSchemaHash))
                throw new InvalidOperationException("[ESAttributeCatalog] Expected SchemaHash is required.");

            string candidateSchemaHash = source.SchemaHash;
            if (!string.Equals(candidateSchemaHash, expectedSchemaHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("[ESAttributeCatalog] SchemaHash mismatch. Expected="
                                                    + expectedSchemaHash + " Actual=" + candidateSchemaHash);
            }
            if (!source.TryBuildCatalogs(out ESSuperAttributeCatalog nextCharacter, out ESSuperAttributeCatalog nextItem, out string error))
                throw new InvalidOperationException("[ESAttributeCatalog] Catalog build failed: " + error);

            if (table != null && !string.Equals(schemaHash, candidateSchemaHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "[ESAttributeCatalog] A different SchemaHash cannot replace the active Catalog in-process. Restart or explicitly migrate the session.");
            }

            table = source;
            character = nextCharacter;
            item = nextItem;
            schemaHash = candidateSchemaHash;
            CatalogBound?.Invoke();
        }

        public static bool TryGet(string scope, out ESSuperAttributeCatalog catalog)
        {
            if (string.Equals(scope, ESAttributeBakeTable.CharacterScope, StringComparison.Ordinal))
            {
                catalog = character;
                return catalog != null;
            }
            if (string.Equals(scope, ESAttributeBakeTable.ItemScope, StringComparison.Ordinal))
            {
                catalog = item;
                return catalog != null;
            }

            catalog = null;
            return false;
        }
    }
}
