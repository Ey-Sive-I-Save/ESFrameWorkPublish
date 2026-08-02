using System;

namespace ES
{
    /// <summary>
    /// Character fixed-slot support that is not generated. The generated partial contains the
    /// typed IDs and stable-key mappings projected from GameCore.
    /// </summary>
    public static partial class ESCharacterAttributeCatalog
    {
        /// <summary>Migrates the former generic default scope and marks legacy fixed rows for code generation.</summary>
        public static void EnsureCharacterScope(ESSuperAttributeTable table)
        {
            if (table == null)
                return;

            bool changed = false;
            if (string.IsNullOrEmpty(table.catalogScope)
                || string.Equals(table.catalogScope, ESSuperAttributeCatalog.DefaultScope, StringComparison.Ordinal))
            {
                table.catalogScope = ESAttributeBakeTable.CharacterScope;
                changed = true;
            }

            if (!string.Equals(table.catalogScope, ESAttributeBakeTable.CharacterScope, StringComparison.Ordinal))
                return;

            table.floatAttributes ??= new System.Collections.Generic.List<ESSuperFloatAttributeDefinition>();
            table.permitAttributes ??= new System.Collections.Generic.List<ESSuperPermitAttributeDefinition>();
            if (MergeMissingFixedDefaults(table))
                changed = true;

            if (PopulateLegacyFixedApiNames(table))
                changed = true;

            if (changed)
                table.InvalidateCache();
        }

        /// <summary>
        /// The compiled default projection is allowed to add a newly introduced built-in Character
        /// attribute to an older GameCore asset. It never overwrites a row already identified by
        /// either stable alias: authored GameCore data stays authoritative after first creation.
        /// </summary>
        private static bool MergeMissingFixedDefaults(ESSuperAttributeTable table)
        {
            ESSuperAttributeTable defaults = CreateDefaultSuperAttributeTable();
            bool changed = false;
            if (defaults.floatAttributes != null)
            {
                for (int i = 0; i < defaults.floatAttributes.Count; i++)
                {
                    ESSuperFloatAttributeDefinition definition = defaults.floatAttributes[i];
                    if (definition != null && !ContainsFloatIdentity(table.floatAttributes, definition))
                    {
                        table.floatAttributes.Add(Clone(definition));
                        changed = true;
                    }
                }
            }

            if (defaults.permitAttributes != null)
            {
                for (int i = 0; i < defaults.permitAttributes.Count; i++)
                {
                    ESSuperPermitAttributeDefinition definition = defaults.permitAttributes[i];
                    if (definition != null && !ContainsPermitIdentity(table.permitAttributes, definition))
                    {
                        table.permitAttributes.Add(Clone(definition));
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static bool ContainsFloatIdentity(
            System.Collections.Generic.List<ESSuperFloatAttributeDefinition> definitions,
            ESSuperFloatAttributeDefinition candidate)
        {
            if (definitions == null)
                return false;

            for (int i = 0; i < definitions.Count; i++)
            {
                ESSuperFloatAttributeDefinition existing = definitions[i];
                if (existing != null && HasSharedStableIdentity(existing.enumKey, existing.StringKey, candidate.enumKey, candidate.StringKey))
                    return true;
            }

            return false;
        }

        private static bool ContainsPermitIdentity(
            System.Collections.Generic.List<ESSuperPermitAttributeDefinition> definitions,
            ESSuperPermitAttributeDefinition candidate)
        {
            if (definitions == null)
                return false;

            for (int i = 0; i < definitions.Count; i++)
            {
                ESSuperPermitAttributeDefinition existing = definitions[i];
                if (existing != null && HasSharedStableIdentity(existing.enumKey, existing.StringKey, candidate.enumKey, candidate.StringKey))
                    return true;
            }

            return false;
        }

        private static bool HasSharedStableIdentity(ushort leftEnum, string leftKey, ushort rightEnum, string rightKey)
        {
            return (leftEnum != 0 && leftEnum == rightEnum)
                   || (!string.IsNullOrEmpty(leftKey) && string.Equals(leftKey, rightKey, StringComparison.Ordinal));
        }

        private static ESSuperFloatAttributeDefinition Clone(ESSuperFloatAttributeDefinition value)
        {
            return new ESSuperFloatAttributeDefinition
            {
                enumKey = value.enumKey,
                key = value.key,
                storagePolicy = value.storagePolicy,
                fixedApiName = value.fixedApiName,
                displayName = value.displayName,
                overrideBaseValue = value.overrideBaseValue,
                baseValue = value.baseValue,
                minValue = value.minValue,
                maxValue = value.maxValue,
                formula = value.formula,
                migrationKey = value.migrationKey
            };
        }

        private static ESSuperPermitAttributeDefinition Clone(ESSuperPermitAttributeDefinition value)
        {
            return new ESSuperPermitAttributeDefinition
            {
                enumKey = value.enumKey,
                key = value.key,
                storagePolicy = value.storagePolicy,
                fixedApiName = value.fixedApiName,
                displayName = value.displayName,
                overrideFallbackValue = value.overrideFallbackValue,
                fallbackValue = value.fallbackValue,
                formula = value.formula,
                migrationKey = value.migrationKey
            };
        }

        private static bool PopulateLegacyFixedApiNames(ESSuperAttributeTable table)
        {
            bool changed = false;
            if (table.floatAttributes != null)
            {
                for (int i = 0; i < table.floatAttributes.Count; i++)
                {
                    ESSuperFloatAttributeDefinition definition = table.floatAttributes[i];
                    if (definition == null || !string.IsNullOrWhiteSpace(definition.fixedApiName)
                        || !TryGetFloatId(definition.enumKey, out ESCharacterFloatAttributeId id))
                        continue;

                    definition.fixedApiName = id.ToString();
                    changed = true;
                }
            }

            if (table.permitAttributes != null)
            {
                for (int i = 0; i < table.permitAttributes.Count; i++)
                {
                    ESSuperPermitAttributeDefinition definition = table.permitAttributes[i];
                    if (definition == null || !string.IsNullOrWhiteSpace(definition.fixedApiName)
                        || !TryGetPermitId(definition.enumKey, out ESCharacterPermitAttributeId id))
                        continue;

                    definition.fixedApiName = id.ToString();
                    changed = true;
                }
            }

            return changed;
        }
    }
}
