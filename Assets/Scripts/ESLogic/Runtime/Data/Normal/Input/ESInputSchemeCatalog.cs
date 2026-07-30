using System;
using System.Collections.Generic;

namespace ES
{
    /// <summary>
    /// Stable identity catalog for input schemes. Built-in schemes expose both an enum and a
    /// StringKey; project, DLC, and platform extensions use a StringKey-only declaration.
    /// RuntimeKey is a process-local acceleration value and is never persisted in a keybinding
    /// profile.
    /// </summary>
    public sealed class ESInputSchemeCatalog
    {
        public const string Scope = "Input.Scheme";

        private readonly ESKeyCatalog keyCatalog;

        private ESInputSchemeCatalog()
        {
            keyCatalog = new ESKeyCatalog(Scope + ".Catalog", Scope);
        }

        public string SchemaHash => keyCatalog.SchemaHash;
        public IReadOnlyList<ESKeyCatalogEntry> Entries => keyCatalog.Entries;
        public ESKeyCatalogHandshake CreateHandshake() => keyCatalog.CreateHandshake();

        public static bool TryCreate(
            IList<ESInputSchemeDefine> schemes,
            out ESInputSchemeCatalog catalog,
            out string error)
        {
            catalog = new ESInputSchemeCatalog();
            if (schemes == null || schemes.Count == 0)
            {
                error = "Input scheme list is empty.";
                catalog = null;
                return false;
            }

            HashSet<ushort> enumKeys = new HashSet<ushort>();
            HashSet<string> stringKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < schemes.Count; i++)
            {
                ESInputSchemeDefine scheme = schemes[i];
                if (scheme == null)
                {
                    error = "Input scheme[" + i + "] is null.";
                    catalog = null;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(scheme.schemeId))
                {
                    error = "Input scheme[" + i + "] has no StringKey schemeId.";
                    catalog = null;
                    return false;
                }

                if (!TryResolveStableEnumKey(scheme, out ushort enumKey, out error))
                {
                    catalog = null;
                    return false;
                }
                if (enumKey != 0 && !enumKeys.Add(enumKey))
                {
                    error = "Input scheme EnumKey is declared more than once: " + enumKey + ".";
                    catalog = null;
                    return false;
                }
                if (!stringKeys.Add(scheme.schemeId))
                {
                    error = "Input scheme StringKey is declared more than once: " + scheme.schemeId + ".";
                    catalog = null;
                    return false;
                }

                catalog.keyCatalog.Declare(new ESKeyDeclaration
                {
                    key = new ESStableKey(Scope, enumKey, scheme.schemeId),
                    kind = ESKeyCatalogKind.Config,
                    valueKind = ESKeyValueKind.Object,
                    storagePolicy = ESKeyStoragePolicy.HotSlot,
                    schemaSignature = "device=" + (byte)scheme.deviceKind
                                      + "|group=" + (scheme.bindingGroup ?? string.Empty),
                    declaredBy = typeof(ESInputSchemeDefine).FullName
                });
            }

            if (!catalog.keyCatalog.TryBuild(out error))
            {
                catalog = null;
                return false;
            }

            return true;
        }

        public bool TryGetRuntimeKey(string schemeId, out int runtimeKey)
        {
            if (string.IsNullOrWhiteSpace(schemeId))
            {
                runtimeKey = 0;
                return false;
            }

            ushort enumKey = 0;
            if (ESInputSchemeIds.TryGetBuiltInEnumKey(schemeId, out ESInputSchemeEnumKey builtIn))
                enumKey = (ushort)builtIn;

            return keyCatalog.TryGetRuntimeKey(new ESStableKey(Scope, enumKey, schemeId), out runtimeKey);
        }

        public bool IsCompatibleWith(ESKeyCatalogHandshake peer, out string error)
        {
            return keyCatalog.IsCompatibleWith(peer, out error);
        }

        public void CopyUsageSnapshots(List<ESKeyCatalogUsageSnapshot> destination)
        {
            keyCatalog.CopyUsageSnapshots(destination);
        }

        private static bool TryResolveStableEnumKey(
            ESInputSchemeDefine scheme,
            out ushort enumKey,
            out string error)
        {
            enumKey = 0;
            error = null;

            ESInputSchemeEnumKey declared = scheme.enumKey;
            if (declared == ESInputSchemeEnumKey.None)
            {
                // Old assets stored only StringKey. Infer reserved built-in aliases during the
                // read path; OnValidate later writes the explicit editor alias back to the asset.
                if (ESInputSchemeIds.TryGetBuiltInEnumKey(scheme.schemeId, out ESInputSchemeEnumKey inferred))
                    enumKey = (ushort)inferred;
                return true;
            }

            if (!ESInputSchemeIds.TryGetCanonicalStringKey(declared, out string canonicalStringKey))
            {
                error = "Input scheme " + scheme.schemeId + " uses an unsupported EnumKey: " + declared + ".";
                return false;
            }
            if (!string.Equals(canonicalStringKey, scheme.schemeId, StringComparison.Ordinal))
            {
                error = "Input scheme EnumKey/StringKey aliases disagree: Enum " + declared
                        + " requires StringKey " + canonicalStringKey + ", actual " + scheme.schemeId + ".";
                return false;
            }

            enumKey = (ushort)declared;
            return true;
        }
    }
}
