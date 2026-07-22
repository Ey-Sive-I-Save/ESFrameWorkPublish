using System;
using System.Collections.Generic;
using ES;

namespace ES.Internal
{
    public static class ESInputProfileBaker
    {
        private const string EffectiveProfileId = "Effective";

        public static ESInputBindingProfile Bake(params ESInputBindingProfile[] profiles)
        {
            return Bake((IList<ESInputBindingProfile>)profiles, EffectiveProfileId, "烘焙输入覆盖");
        }

        public static ESInputBindingProfile Bake(
            IList<ESInputBindingProfile> profiles,
            string profileId = EffectiveProfileId,
            string displayName = "烘焙输入覆盖")
        {
            ESInputBindingProfile result = ESInputProfileIO.CreateDefaultProfile();
            result.profileId = string.IsNullOrEmpty(profileId) ? EffectiveProfileId : profileId;
            result.displayName = string.IsNullOrEmpty(displayName) ? "烘焙输入覆盖" : displayName;
            result.overrides.Clear();

            if (profiles == null || profiles.Count == 0)
            {
                result.Normalize();
                return result;
            }

            Dictionary<string, int> indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int p = 0; p < profiles.Count; p++)
            {
                ESInputBindingProfile profile = profiles[p];
                if (profile == null)
                    continue;

                profile.Normalize();
                if (!string.IsNullOrEmpty(profile.activeSchemeId))
                    result.activeSchemeId = profile.activeSchemeId;

                if (profile.overrides == null)
                    continue;

                for (int i = 0; i < profile.overrides.Count; i++)
                {
                    ESInputBindingOverride source = profile.overrides[i];
                    if (source == null)
                        continue;

                    string key = MakeOverrideKey(source);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (indexByKey.TryGetValue(key, out int existingIndex))
                    {
                        ApplyOverride(result.overrides[existingIndex], source);
                    }
                    else
                    {
                        ESInputBindingOverride created = CloneOverride(source);
                        indexByKey.Add(key, result.overrides.Count);
                        result.overrides.Add(created);
                    }
                }
            }

            result.Normalize();
            return result;
        }

        public static ESInputBindingOverride CloneOverride(ESInputBindingOverride source)
        {
            ESInputBindingOverride target = new ESInputBindingOverride();
            ApplyOverride(target, source);
            return target;
        }

        public static void ApplyOverride(ESInputBindingOverride target, ESInputBindingOverride source)
        {
            if (target == null || source == null)
                return;

            target.enabled = source.enabled;
            target.bindingId = source.bindingId;
            target.actionId = source.actionId;
            target.schemeId = source.schemeId;
            target.bindingName = source.bindingName;
            target.originalPath = source.originalPath;
            target.overridePathEnabled = source.overridePathEnabled;
            target.overridePath = source.overridePath;
            target.overrideVirtualControlEnabled = source.overrideVirtualControlEnabled;
            target.overrideVirtualControlId = source.overrideVirtualControlId;
            target.overrideInteractionsEnabled = source.overrideInteractionsEnabled;
            target.overrideInteractions = source.overrideInteractions;
            target.overrideProcessorsEnabled = source.overrideProcessorsEnabled;
            target.overrideProcessors = source.overrideProcessors;
            target.overrideBindingNameEnabled = source.overrideBindingNameEnabled;
            target.overrideBindingName = source.overrideBindingName;
            target.overrideCompositeFlagsEnabled = source.overrideCompositeFlagsEnabled;
            target.overrideIsComposite = source.overrideIsComposite;
            target.overrideIsPartOfComposite = source.overrideIsPartOfComposite;
        }

        private static string MakeOverrideKey(ESInputBindingOverride item)
        {
            return item != null && !string.IsNullOrEmpty(item.bindingId)
                ? "id:" + item.bindingId
                : string.Empty;
        }
    }
}
