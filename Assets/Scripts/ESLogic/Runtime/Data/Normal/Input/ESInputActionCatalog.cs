using System;
using System.Collections.Generic;
using System.Globalization;

namespace ES
{
    /// <summary>
    /// Stable identity catalog for input actions and rebind binding ids. The legacy
    /// <see cref="ESInputActionId"/> remains a zero-based HotSlot index for fixed actions;
    /// this catalog maps it to a non-zero stable EnumKey so Move(0) is never mistaken for an
    /// unconfigured key. Dynamic actions are StringKey-only by design.
    /// </summary>
    public sealed class ESInputActionCatalog
    {
        public const string Scope = "Input.Action";

        private readonly ESKeyCatalog keyCatalog;

        private ESInputActionCatalog()
        {
            keyCatalog = new ESKeyCatalog(Scope + ".Catalog", Scope);
        }

        public string SchemaHash => keyCatalog.SchemaHash;
        public IReadOnlyList<ESKeyCatalogEntry> Entries => keyCatalog.Entries;
        public ESKeyCatalogHandshake CreateHandshake() => keyCatalog.CreateHandshake();

        public static bool TryCreate(
            IList<ESInputActionDefine> actions,
            out ESInputActionCatalog catalog,
            out string error)
        {
            return TryCreate(actions, null, out catalog, out error);
        }

        /// <summary>
        /// Builds action identity together with an optional authoritative scheme catalog. Generic
        /// runtime sources may omit the scheme catalog, but every ESInputConfig must pass one so
        /// a binding cannot reference an undeclared persisted scheme StringKey.
        /// </summary>
        public static bool TryCreate(
            IList<ESInputActionDefine> actions,
            ESInputSchemeCatalog schemeCatalog,
            out ESInputActionCatalog catalog,
            out string error)
        {
            catalog = new ESInputActionCatalog();
            if (actions == null)
            {
                error = "Input action list is null.";
                catalog = null;
                return false;
            }

            HashSet<ushort> enumKeys = new HashSet<ushort>();
            HashSet<string> actionNames = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> bindingIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < actions.Count; i++)
            {
                ESInputActionDefine action = actions[i];
                if (action == null)
                {
                    error = "Input action[" + i + "] is null.";
                    catalog = null;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(action.actionName))
                {
                    error = "Input action[" + i + "] has no StringKey actionName.";
                    catalog = null;
                    return false;
                }

                ushort enumKey = 0;
                if (action.id != ESInputActionId.Dynamic)
                {
                    if (!TryGetStableEnumKey(action.id, out enumKey))
                    {
                        error = "Input action[" + i + "] has an unsupported EnumKey: " + action.id + ".";
                        catalog = null;
                        return false;
                    }

                    if (!enumKeys.Add(enumKey))
                    {
                        error = "Input action EnumKey is declared more than once: " + action.id + ".";
                        catalog = null;
                        return false;
                    }
                }

                if (!actionNames.Add(action.actionName))
                {
                    error = "Input action StringKey is declared more than once: " + action.actionName + ".";
                    catalog = null;
                    return false;
                }

                if (!TryValidateBindings(action, bindingIds, schemeCatalog, out error))
                {
                    catalog = null;
                    return false;
                }

                catalog.keyCatalog.Declare(new ESKeyDeclaration
                {
                    key = new ESStableKey(Scope, enumKey, action.actionName),
                    kind = ESKeyCatalogKind.Config,
                    valueKind = ToValueKind(action.valueType),
                    storagePolicy = action.id == ESInputActionId.Dynamic
                        ? ESKeyStoragePolicy.Sparse
                        : ESKeyStoragePolicy.HotSlot,
                    schemaSignature = BuildActionSchemaSignature(action),
                    declaredBy = typeof(ESInputActionDefine).FullName
                });
            }

            if (!catalog.keyCatalog.TryBuild(out error))
            {
                catalog = null;
                return false;
            }

            return true;
        }

        public bool TryGetRuntimeKey(ESInputActionId actionId, string actionName, out int runtimeKey)
        {
            ushort enumKey = 0;
            if (actionId != ESInputActionId.Dynamic && !TryGetStableEnumKey(actionId, out enumKey))
            {
                runtimeKey = 0;
                return false;
            }

            return keyCatalog.TryGetRuntimeKey(new ESStableKey(Scope, enumKey, actionName), out runtimeKey);
        }

        public bool IsCompatibleWith(ESKeyCatalogHandshake peer, out string error)
        {
            return keyCatalog.IsCompatibleWith(peer, out error);
        }

        public void CopyUsageSnapshots(List<ESKeyCatalogUsageSnapshot> destination)
        {
            keyCatalog.CopyUsageSnapshots(destination);
        }

        public static bool TryGetStableEnumKey(ESInputActionId actionId, out ushort stableEnumKey)
        {
            if (actionId == ESInputActionId.Dynamic)
            {
                stableEnumKey = 0;
                return false;
            }

            int legacyHotSlot = (int)actionId;
            if (legacyHotSlot < 0 || legacyHotSlot >= ushort.MaxValue)
            {
                stableEnumKey = 0;
                return false;
            }

            stableEnumKey = (ushort)(legacyHotSlot + 1);
            return true;
        }

        private static bool TryValidateBindings(
            ESInputActionDefine action,
            HashSet<string> bindingIds,
            ESInputSchemeCatalog schemeCatalog,
            out string error)
        {
            error = null;
            if (action.bindings == null)
                return true;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                ESInputBindingDefine binding = action.bindings[i];
                if (binding == null)
                {
                    error = "Input action " + action.actionName + " has null binding[" + i + "].";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(binding.bindingId))
                {
                    error = "Input action " + action.actionName + " binding[" + i + "] has no stable bindingId.";
                    return false;
                }
                if (!bindingIds.Add(binding.bindingId))
                {
                    error = "Input bindingId is declared more than once: " + binding.bindingId + ".";
                    return false;
                }
                if (schemeCatalog != null && !schemeCatalog.TryGetRuntimeKey(binding.schemeId, out _))
                {
                    error = "Input action " + action.actionName + " binding " + binding.bindingId
                            + " references undeclared scheme StringKey: " + (binding.schemeId ?? string.Empty) + ".";
                    return false;
                }
            }

            return true;
        }

        private static ESKeyValueKind ToValueKind(ESInputValueType valueType)
        {
            switch (valueType)
            {
                case ESInputValueType.Button:
                    return ESKeyValueKind.Flag;
                case ESInputValueType.Axis:
                    return ESKeyValueKind.Float;
                default:
                    return ESKeyValueKind.Object;
            }
        }

        private static string BuildActionSchemaSignature(ESInputActionDefine action)
        {
            List<string> bindings = new List<string>(action.bindings != null ? action.bindings.Count : 0);
            if (action.bindings != null)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    ESInputBindingDefine binding = action.bindings[i];
                    if (binding == null)
                        continue;
                    bindings.Add((binding.bindingId ?? string.Empty)
                                 + "@" + (binding.schemeId ?? string.Empty)
                                 + "|" + (byte)binding.source
                                 + "|" + (binding.path ?? string.Empty)
                                 + "|" + (binding.virtualControlId ?? string.Empty)
                                 + "|" + (binding.interactions ?? string.Empty)
                                 + "|" + (binding.processors ?? string.Empty)
                                 + "|" + binding.isComposite
                                 + "|" + binding.isPartOfComposite);
                }
            }
            bindings.Sort(StringComparer.Ordinal);
            return "value=" + (byte)action.valueType
                   + "|category=" + (byte)action.category
                   + "|rebind=" + action.allowRebind
                   + "|trigger=" + (int)action.GetEffectiveTriggerFeatures()
                   + "|press=" + (byte)action.pressPolicy
                   + "|long=" + action.longPressDuration.ToString("R", CultureInfo.InvariantCulture)
                   + "|double=" + action.doublePressWindow.ToString("R", CultureInfo.InvariantCulture)
                   + "|bindings=" + string.Join(";", bindings);
        }
    }
}
