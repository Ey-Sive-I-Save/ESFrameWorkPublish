using System;
using System.Collections.Generic;
using System.Globalization;

namespace ES
{
    /// <summary>
    /// Catalog for attribute definitions. A catalog owns stable identity, value type, storage
    /// policy and schema validation; ESSuperAttributeTable supplies authored definitions. Runtime
    /// instances own only their base-value overrides and active modifiers. RuntimeKey is a dense
    /// catalog index and is never a save/network value.
    /// </summary>
    public sealed class ESSuperAttributeCatalog
    {
        public const string DefaultScope = "Attribute";

        private readonly ESKeyCatalog keyCatalog;
        private readonly Dictionary<int, ESSuperFloatAttributeDefinition> floatByRuntimeKey;
        private readonly Dictionary<int, ESSuperPermitAttributeDefinition> permitByRuntimeKey;

        private ESSuperAttributeCatalog(string scope, int capacity)
        {
            keyCatalog = new ESKeyCatalog(scope + ".Catalog", scope);
            floatByRuntimeKey = new Dictionary<int, ESSuperFloatAttributeDefinition>(capacity);
            permitByRuntimeKey = new Dictionary<int, ESSuperPermitAttributeDefinition>(capacity);
        }

        public string Scope => keyCatalog.RequiredScope;
        public string SchemaHash => keyCatalog.SchemaHash;
        public ESKeyCatalogHandshake CreateHandshake() => keyCatalog.CreateHandshake();
        public IReadOnlyList<ESKeyCatalogEntry> Entries => keyCatalog.Entries;

        public static bool TryCreate(
            string scope,
            IList<ESSuperFloatAttributeDefinition> floatDefinitions,
            IList<ESSuperPermitAttributeDefinition> permitDefinitions,
            out ESSuperAttributeCatalog catalog,
            out string error)
        {
            scope = string.IsNullOrEmpty(scope) ? DefaultScope : scope;
            int capacity = (floatDefinitions != null ? floatDefinitions.Count : 0)
                           + (permitDefinitions != null ? permitDefinitions.Count : 0);
            catalog = new ESSuperAttributeCatalog(scope, capacity);

            if (!ValidateDefinitionIdentities(floatDefinitions, permitDefinitions, out error)
                || !DeclareFloats(catalog.keyCatalog, scope, floatDefinitions, out error)
                || !DeclarePermits(catalog.keyCatalog, scope, permitDefinitions, out error)
                || !catalog.keyCatalog.TryBuild(out error))
            {
                catalog = null;
                return false;
            }

            AddFloatRuntimeMap(catalog, floatDefinitions);
            AddPermitRuntimeMap(catalog, permitDefinitions);
            return true;
        }

        public bool TryGetRuntimeKey(ushort enumKey, string stringKey, out int runtimeKey)
        {
            return keyCatalog.TryGetRuntimeKey(new ESStableKey(Scope, enumKey, stringKey), out runtimeKey);
        }

        public bool TryGetFloatDefinition(int runtimeKey, out ESSuperFloatAttributeDefinition definition)
        {
            return floatByRuntimeKey.TryGetValue(runtimeKey, out definition);
        }

        public bool TryGetPermitDefinition(int runtimeKey, out ESSuperPermitAttributeDefinition definition)
        {
            return permitByRuntimeKey.TryGetValue(runtimeKey, out definition);
        }

        public bool TryGetEntry(int runtimeKey, out ESKeyCatalogEntry entry)
        {
            return keyCatalog.TryGetEntry(runtimeKey, out entry);
        }

        public bool TryResolveFloatBase(int runtimeKey, float fallbackValue, out float value)
        {
            value = fallbackValue;
            if (!TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition))
                return false;

            if (definition.overrideBaseValue)
                value = definition.baseValue;
            return true;
        }

        public bool TryResolvePermitFallback(int runtimeKey, bool fallbackValue, out bool value)
        {
            value = fallbackValue;
            if (!TryGetPermitDefinition(runtimeKey, out ESSuperPermitAttributeDefinition definition))
                return false;

            if (definition.overrideFallbackValue)
                value = definition.fallbackValue;
            return true;
        }

        /// <summary>Editor diagnostic hook. Runtime gameplay code must not call this per frame.</summary>
        public bool RecordUsage(int runtimeKey, ESKeyUsageKind usage, string owner)
        {
            return keyCatalog.RecordUsage(runtimeKey, usage, owner);
        }

        public string GetUsageReport()
        {
            return keyCatalog.GetUsageReport();
        }

        public void CopyUsageSnapshots(List<ESKeyCatalogUsageSnapshot> destination)
        {
            keyCatalog.CopyUsageSnapshots(destination);
        }

        public bool IsCompatibleWith(ESKeyCatalogHandshake peer, out string error)
        {
            return keyCatalog.IsCompatibleWith(peer, out error);
        }

        private static bool ValidateDefinitionIdentities(
            IList<ESSuperFloatAttributeDefinition> floatDefinitions,
            IList<ESSuperPermitAttributeDefinition> permitDefinitions,
            out string error)
        {
            Dictionary<ushort, string> enumOwners = new Dictionary<ushort, string>();
            Dictionary<string, string> stringOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            error = null;

            if (floatDefinitions != null)
            {
                for (int i = 0; i < floatDefinitions.Count; i++)
                {
                    ESSuperFloatAttributeDefinition definition = floatDefinitions[i];
                    if (definition != null
                        && !TryTrackIdentity(enumOwners, stringOwners, definition.enumKey, definition.StringKey, "Float[" + i + "]", out error))
                        return false;
                }
            }

            if (permitDefinitions != null)
            {
                for (int i = 0; i < permitDefinitions.Count; i++)
                {
                    ESSuperPermitAttributeDefinition definition = permitDefinitions[i];
                    if (definition != null
                        && !TryTrackIdentity(enumOwners, stringOwners, definition.enumKey, definition.StringKey, "Permit[" + i + "]", out error))
                        return false;
                }
            }

            return true;
        }

        private static bool TryTrackIdentity(
            Dictionary<ushort, string> enumOwners,
            Dictionary<string, string> stringOwners,
            ushort enumKey,
            string stringKey,
            string owner,
            out string error)
        {
            if (enumKey != 0 && enumOwners.TryGetValue(enumKey, out string enumOwner))
            {
                error = "Attribute EnumKey is declared more than once: " + enumKey + " (" + enumOwner + ", " + owner + ").";
                return false;
            }

            if (!string.IsNullOrEmpty(stringKey) && stringOwners.TryGetValue(stringKey, out string stringOwner))
            {
                error = "Attribute StringKey is declared more than once: " + stringKey + " (" + stringOwner + ", " + owner + ").";
                return false;
            }

            if (enumKey != 0)
                enumOwners.Add(enumKey, owner);
            if (!string.IsNullOrEmpty(stringKey))
                stringOwners.Add(stringKey, owner);

            error = null;
            return true;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool DeclareFloats(
            ESKeyCatalog keyCatalog,
            string scope,
            IList<ESSuperFloatAttributeDefinition> definitions,
            out string error)
        {
            error = null;
            if (definitions == null)
                return true;

            for (int i = 0; i < definitions.Count; i++)
            {
                ESSuperFloatAttributeDefinition definition = definitions[i];
                if (definition == null)
                    continue;
                if (definition.enumKey == 0 && string.IsNullOrEmpty(definition.StringKey))
                {
                    error = "Float definition[" + i + "] has no EnumKey or StringKey.";
                    return false;
                }
                if (definition.minValue > definition.maxValue)
                {
                    error = "Float definition has minValue > maxValue: " + definition.StringKey;
                    return false;
                }
                if (float.IsNaN(definition.minValue) || float.IsNaN(definition.maxValue))
                {
                    error = "Float definition bounds cannot be NaN: " + definition.StringKey;
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(definition.formula))
                {
                    error = "Float definition formula is not supported. Use a runtime ValueChange producer instead: " + definition.StringKey;
                    return false;
                }

                keyCatalog.Declare(new ESKeyDeclaration
                {
                    key = new ESStableKey(scope, definition.enumKey, definition.StringKey),
                    kind = ESKeyCatalogKind.Attribute,
                    valueKind = ESKeyValueKind.Float,
                    storagePolicy = definition.storagePolicy,
                    schemaSignature = "base=" + FormatFloat(definition.baseValue)
                                      + "|override=" + definition.overrideBaseValue
                                      + "|min=" + FormatFloat(definition.minValue)
                                      + "|max=" + FormatFloat(definition.maxValue)
                                      + "|formula=" + (definition.formula ?? string.Empty)
                                      + "|migration=" + (definition.migrationKey ?? string.Empty),
                    declaredBy = typeof(ESSuperFloatAttributeDefinition).FullName
                });
            }

            return true;
        }

        private static bool DeclarePermits(
            ESKeyCatalog keyCatalog,
            string scope,
            IList<ESSuperPermitAttributeDefinition> definitions,
            out string error)
        {
            error = null;
            if (definitions == null)
                return true;

            for (int i = 0; i < definitions.Count; i++)
            {
                ESSuperPermitAttributeDefinition definition = definitions[i];
                if (definition == null)
                    continue;
                if (definition.enumKey == 0 && string.IsNullOrEmpty(definition.StringKey))
                {
                    error = "Permit definition[" + i + "] has no EnumKey or StringKey.";
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(definition.formula))
                {
                    error = "Permit definition formula is not supported. Use a runtime ValueChange producer instead: " + definition.StringKey;
                    return false;
                }

                keyCatalog.Declare(new ESKeyDeclaration
                {
                    key = new ESStableKey(scope, definition.enumKey, definition.StringKey),
                    kind = ESKeyCatalogKind.Attribute,
                    valueKind = ESKeyValueKind.Flag,
                    storagePolicy = definition.storagePolicy,
                    schemaSignature = "fallback=" + definition.fallbackValue
                                      + "|override=" + definition.overrideFallbackValue
                                      + "|formula=" + (definition.formula ?? string.Empty)
                                      + "|migration=" + (definition.migrationKey ?? string.Empty),
                    declaredBy = typeof(ESSuperPermitAttributeDefinition).FullName
                });
            }

            return true;
        }

        private static void AddFloatRuntimeMap(ESSuperAttributeCatalog catalog, IList<ESSuperFloatAttributeDefinition> definitions)
        {
            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Count; i++)
            {
                ESSuperFloatAttributeDefinition definition = definitions[i];
                if (definition != null
                    && catalog.TryGetRuntimeKey(definition.enumKey, definition.StringKey, out int runtimeKey))
                    catalog.floatByRuntimeKey[runtimeKey] = definition;
            }
        }

        private static void AddPermitRuntimeMap(ESSuperAttributeCatalog catalog, IList<ESSuperPermitAttributeDefinition> definitions)
        {
            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Count; i++)
            {
                ESSuperPermitAttributeDefinition definition = definitions[i];
                if (definition != null
                    && catalog.TryGetRuntimeKey(definition.enumKey, definition.StringKey, out int runtimeKey))
                    catalog.permitByRuntimeKey[runtimeKey] = definition;
            }
        }
    }
}
