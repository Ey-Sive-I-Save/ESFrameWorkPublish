using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Recursively expands selected GameCore roots through GameCoreConfigKey edges and
    /// materializes every discovered AssetConfigKey into a read-only Plan snapshot. Runtime
    /// never reflects over GameCore data while preparing a Plan.
    /// </summary>
    internal static class ESResourcePlanGameCoreExpansion
    {
        public static int BakeAll()
        {
            ESAssetCatalogKeyPicker.RefreshForValidation();
            ESGameCoreDefinitionLocator.ClearCache();
            int changed = 0;
            List<ESResourcePlanInfo> plans = ESEditorSO.SOS.GetNewGroupOfType<ESResourcePlanInfo>();
            for (int i = 0; i < plans.Count; i++)
                if (plans[i] != null && Bake(plans[i]))
                    changed++;
            if (changed > 0)
                AssetDatabase.SaveAssets();
            return changed;
        }

        public static bool Bake(ESResourcePlanInfo plan)
        {
            if (plan == null)
                return false;

            List<ScriptableObject> roots = plan.gameCoreSources ?? new List<ScriptableObject>();
            for (int i = 0; i < roots.Count; i++)
                if (roots[i] != null && !(roots[i] is IGameCoreSO))
                    throw new InvalidOperationException("[ESRes][Plan] GameCore expansion source must implement IGameCoreSO: " + roots[i].name);

            List<ESResourcePlanSerializedDependency> discovered = ESResourcePlanDependencyCollector.Expand(
                roots,
                ESResourcePlanDependencyCollector.DefaultMaxGameCoreDepth);
            var entries = new List<ESResourcePlanBakedAssetEntry>(discovered.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESResourcePlanSerializedDependency item in discovered)
            {
                ValidateAgainstCurrentCatalog(item);
                string dedupeKey = item.Kind + "|" + (item.EnumKey != 0 ? "E:" + item.EnumKey : "S:" + item.StringKey);
                if (!seen.Add(dedupeKey))
                    continue;
                entries.Add(new ESResourcePlanBakedAssetEntry
                {
                    required = true,
                    kind = item.Kind,
                    enumKey = item.EnumKey,
                    stringKey = item.StringKey,
                    guid = item.Guid,
                    localFileId = item.LocalFileId,
                    source = item.Source
                });
            }

            string fingerprint = ESResourcePlanDependencyCollector.ComputeFingerprint(discovered);
            bool changed = !string.Equals(plan.BakedExpansionHash, fingerprint, StringComparison.Ordinal)
                || !SameEntries(plan.BakedAssets, entries);
            if (!changed)
                return false;

            Undo.RecordObject(plan, "Bake ResourcePlan GameCore Expansion");
            plan.ReplaceBakedAssets(entries, fingerprint);
            EditorUtility.SetDirty(plan);
            return true;
        }

        private static void ValidateAgainstCurrentCatalog(ESResourcePlanSerializedDependency item)
        {
            bool found = item.EnumKey != 0
                ? ESAssetRegistry.TryGetByEnum(item.Kind, item.EnumKey, out ESAssetPage page)
                : ESAssetRegistry.TryGetByString(item.Kind, item.StringKey, out page);
            if (!found || page == null)
                throw new InvalidOperationException("[ESRes][Plan] GameCore ConfigKey is absent from current Library/Catalog: " + item.Source + " -> " + item.EffectiveKey);

            if (!string.IsNullOrEmpty(item.Guid)
                && (!string.Equals(page.AssetGuid, item.Guid, StringComparison.Ordinal) || page.LocalFileId != item.LocalFileId))
                throw new InvalidOperationException("[ESRes][Plan] GameCore ConfigKey identity does not match current Catalog: " + item.Source
                    + " -> " + item.EffectiveKey + ", Expected=" + item.Guid + ":" + item.LocalFileId
                    + ", Catalog=" + page.AssetGuid + ":" + page.LocalFileId);
        }

        private static bool SameEntries(IReadOnlyList<ESResourcePlanBakedAssetEntry> left, IReadOnlyList<ESResourcePlanBakedAssetEntry> right)
        {
            if ((left?.Count ?? 0) != (right?.Count ?? 0)) return false;
            for (int i = 0; i < (left?.Count ?? 0); i++)
            {
                ESResourcePlanBakedAssetEntry a = left[i];
                ESResourcePlanBakedAssetEntry b = right[i];
                if (a == null || b == null || a.kind != b.kind || a.enumKey != b.enumKey || a.stringKey != b.stringKey
                    || a.guid != b.guid || a.localFileId != b.localFileId || a.source != b.source || a.required != b.required)
                    return false;
            }
            return true;
        }
    }
}
