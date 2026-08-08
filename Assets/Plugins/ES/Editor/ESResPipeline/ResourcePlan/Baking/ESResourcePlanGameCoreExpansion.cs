using System;
using System.Collections.Generic;
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
        [MenuItem(MenuItemPathDefine.RESOURCE_DELIVERY_PATH + "ResourcePlan/显式展开并写入 GameCore 快照", false, 35)]
        private static void BakeAllMenu()
        {
            try
            {
                int changed = BakeAll();
                EditorUtility.DisplayDialog("ES ResourcePlan", "已显式展开并写入 " + changed + " 个 ResourcePlan。", "确定");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("ES ResourcePlan", "展开失败：" + exception.Message, "确定");
            }
        }

        public static int BakeAll()
        {
            ESAssetCatalogKeyPicker.RefreshForValidation();
            ESGameCoreDefinitionLocator.ClearCache();
            int changed = 0;
            List<ESResourcePlanInfo> plans = ESEditorSO.GetGroupOfType<ESResourcePlanInfo>();
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

            List<ESResourcePlanBakedExtensionEntry> extensions = BakeExtensions(plan);

            CollectRoots(plan, out List<ScriptableObject> requiredRoots, out List<ScriptableObject> optionalRoots);
            ValidateRoots(requiredRoots);
            ValidateRoots(optionalRoots);

            List<ESResourcePlanSerializedDependency> requiredDependencies = ESResourcePlanDependencyCollector.Expand(
                requiredRoots,
                ESResourcePlanDependencyCollector.DefaultMaxGameCoreDepth);
            List<ESResourcePlanSerializedDependency> optionalDependencies = ESResourcePlanDependencyCollector.Expand(
                optionalRoots,
                ESResourcePlanDependencyCollector.DefaultMaxGameCoreDepth);
            var discovered = new List<ESResourcePlanSerializedDependency>(requiredDependencies.Count + optionalDependencies.Count);
            discovered.AddRange(requiredDependencies);
            discovered.AddRange(optionalDependencies);
            int requiredDependencyCount = requiredDependencies.Count;

            var entries = new List<ESResourcePlanBakedAssetEntry>(discovered.Count);
            var entriesByKey = new Dictionary<string, ESResourcePlanBakedAssetEntry>(StringComparer.Ordinal);
            for (int i = 0; i < discovered.Count; i++)
            {
                ESResourcePlanSerializedDependency item = discovered[i];
                bool required = i < requiredDependencyCount;
                ValidateAgainstCurrentCatalog(item);
                string dedupeKey = item.Kind + "|" + (item.EnumKey != 0 ? "E:" + item.EnumKey : "S:" + item.StringKey);
                if (entriesByKey.TryGetValue(dedupeKey, out ESResourcePlanBakedAssetEntry existing))
                {
                    existing.required |= required;
                    continue;
                }

                var entry = new ESResourcePlanBakedAssetEntry
                {
                    required = required,
                    kind = item.Kind,
                    enumKey = item.EnumKey,
                    stringKey = item.StringKey,
                    guid = item.Guid,
                    localFileId = item.LocalFileId,
                    source = item.Source
                };
                entriesByKey.Add(dedupeKey, entry);
                entries.Add(entry);
            }

            string fingerprint = ESResourcePlanDependencyCollector.ComputeFingerprint(discovered);
            bool changed = !string.Equals(plan.BakedExpansionHash, fingerprint, StringComparison.Ordinal)
                || !SameEntries(plan.BakedAssets, entries)
                || !SameExtensions(plan.BakedExtensions, extensions);
            if (!changed)
                return false;

            Undo.RecordObject(plan, "Bake ResourcePlan GameCore Expansion");
            plan.ReplaceBakedAssets(entries, fingerprint);
            plan.ReplaceBakedExtensions(extensions);
            EditorUtility.SetDirty(plan);
            return true;
        }

        private static List<ESResourcePlanBakedExtensionEntry> BakeExtensions(ESResourcePlanInfo plan)
        {
            var result = new List<ESResourcePlanBakedExtensionEntry>();
            ESResourcePlanExtensionBakeCompanion companion = FindCompanion(plan);
            foreach (ESResourcePlanExtensionSourceEntry sourceEntry in companion?.sources ?? new List<ESResourcePlanExtensionSourceEntry>())
            {
                if (sourceEntry == null || sourceEntry.source == null)
                    throw new InvalidOperationException("[ESRes][Plan] 扩展资源来源为空：" + plan.name);
                IESResourcePlanBakeExtension extension = ESResourcePlanBakeExtensions.Resolve(sourceEntry.source);
                if (extension == null)
                    throw new InvalidOperationException("[ESRes][Plan] 扩展资源来源没有已注册的 Bake 扩展：" + sourceEntry.source.name);
                ESResourcePlanBakedExtensionEntry baked = extension.Bake(plan, sourceEntry);
                if (baked == null || !string.Equals(baked.providerId, extension.ProviderId, StringComparison.Ordinal)
                    || baked.schemaVersion != extension.SchemaVersion)
                    throw new InvalidOperationException("[ESRes][Plan] 扩展 Bake 输出无效：" + extension.ProviderId);
                baked.required = sourceEntry.required;
                foreach (ESResourcePlanBakedAssetEntry asset in baked.assets ?? new List<ESResourcePlanBakedAssetEntry>())
                {
                    if (asset == null || !asset.HasConfiguredKey)
                        throw new InvalidOperationException("[ESRes][Plan] 扩展 Bake 输出包含空资源 Key：" + extension.ProviderId);
                    ValidateAgainstCurrentCatalog(new ESResourcePlanSerializedDependency(null, asset.source, string.Empty, 0, asset.kind, asset.enumKey, asset.stringKey, asset.guid, asset.localFileId));
                }
                result.Add(baked);
            }
            return result;
        }

        private static ESResourcePlanExtensionBakeCompanion FindCompanion(ESResourcePlanInfo plan)
        {
            ESResourcePlanExtensionBakeCompanion result = null;
            foreach (string guid in AssetDatabase.FindAssets("t:ESResourcePlanExtensionBakeCompanion"))
            {
                ESResourcePlanExtensionBakeCompanion candidate = AssetDatabase.LoadAssetAtPath<ESResourcePlanExtensionBakeCompanion>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate == null || candidate.plan != plan) continue;
                if (result != null) throw new InvalidOperationException("[ESRes][Plan] 同一 ResourcePlan 存在多个扩展 Bake Companion：" + plan.name);
                result = candidate;
            }
            return result;
        }

        private static void ValidateRoots(IReadOnlyList<ScriptableObject> roots)
        {
            for (int i = 0; i < roots.Count; i++)
                if (roots[i] != null && !(roots[i] is IGameCoreSO))
                    throw new InvalidOperationException("[ESRes][Plan] GameCore expansion source must implement IGameCoreSO: " + roots[i].name);
        }

        private static void CollectRoots(
            ESResourcePlanInfo plan,
            out List<ScriptableObject> requiredRoots,
            out List<ScriptableObject> optionalRoots)
        {
            requiredRoots = new List<ScriptableObject>();
            optionalRoots = new List<ScriptableObject>();
            var seen = new HashSet<ScriptableObject>();
            var rootRequired = new Dictionary<ScriptableObject, bool>();

            AddRoots(plan.gameCoreSources, true, requiredRoots, optionalRoots, seen, rootRequired);
            if (plan.audioCues == null)
                return;

            for (int i = 0; i < plan.audioCues.Count; i++)
            {
                ESResourcePlanAudioCueEntry entry = plan.audioCues[i];
                if (entry == null)
                    continue;
                if (entry.cue == null)
                    throw new InvalidOperationException("[ESRes][Plan] Audio Cue entry is missing a Cue: " + plan.name);

                AddRoot(entry.cue, entry.required, requiredRoots, optionalRoots, seen, rootRequired);
            }
        }

        private static void AddRoots(
            IEnumerable<ScriptableObject> sources,
            bool required,
            List<ScriptableObject> requiredRoots,
            List<ScriptableObject> optionalRoots,
            HashSet<ScriptableObject> seen,
            Dictionary<ScriptableObject, bool> rootRequired)
        {
            if (sources == null)
                return;

            foreach (ScriptableObject source in sources)
                AddRoot(source, required, requiredRoots, optionalRoots, seen, rootRequired);
        }

        private static void AddRoot(
            ScriptableObject source,
            bool required,
            List<ScriptableObject> requiredRoots,
            List<ScriptableObject> optionalRoots,
            HashSet<ScriptableObject> seen,
            Dictionary<ScriptableObject, bool> rootRequired)
        {
            if (source == null)
                return;

            if (rootRequired.TryGetValue(source, out bool currentRequired))
            {
                rootRequired[source] = currentRequired || required;
                if (required && !currentRequired)
                {
                    optionalRoots.Remove(source);
                    requiredRoots.Add(source);
                }
            }
            else
                rootRequired.Add(source, required);

            if (seen.Add(source))
                (required ? requiredRoots : optionalRoots).Add(source);
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

        private static bool SameExtensions(IReadOnlyList<ESResourcePlanBakedExtensionEntry> left, IReadOnlyList<ESResourcePlanBakedExtensionEntry> right)
        {
            if ((left?.Count ?? 0) != (right?.Count ?? 0)) return false;
            for (int i = 0; i < (left?.Count ?? 0); i++)
            {
                ESResourcePlanBakedExtensionEntry a = left[i];
                ESResourcePlanBakedExtensionEntry b = right[i];
                if (a == null || b == null || a.required != b.required || a.providerId != b.providerId
                    || a.schemaVersion != b.schemaVersion || a.source != b.source || a.payload != b.payload
                    || !SameEntries(a.assets, b.assets)) return false;
            }
            return true;
        }
    }
}
