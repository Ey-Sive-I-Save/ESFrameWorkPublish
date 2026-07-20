using System.Collections.Generic;
using ES;

namespace ES.Internal
{
    public static class ESInputUtility
    {
        public static ESInputBindingProfile BakeProfiles(params ESInputBindingProfile[] profiles)
        {
            return ESInputProfileBaker.Bake(profiles);
        }

        public static ESInputBindingProfile BakeProfiles(IList<ESInputBindingProfile> profiles)
        {
            return ESInputProfileBaker.Bake(profiles);
        }

        public static ESInputRuntimeBuildResult BuildRuntime(
            IESInputRuntimeConfigSource config,
            string fallbackSchemeId,
            params ESInputBindingProfile[] profileLayers)
        {
            EnsureConfigBindingIds(config);
            ESInputBindingProfile effectiveProfile = BakeProfiles(profileLayers);
            if (string.IsNullOrEmpty(effectiveProfile.activeSchemeId))
                effectiveProfile.activeSchemeId = fallbackSchemeId;
            return ESInputRuntimeBuilder.Build(config, effectiveProfile, fallbackSchemeId);
        }

        public static ESInputRuntimeBuildResult BuildRuntime(
            IESInputRuntimeConfigSource config,
            string fallbackSchemeId,
            IList<ESInputBindingProfile> profileLayers)
        {
            EnsureConfigBindingIds(config);
            ESInputBindingProfile effectiveProfile = BakeProfiles(profileLayers);
            if (string.IsNullOrEmpty(effectiveProfile.activeSchemeId))
                effectiveProfile.activeSchemeId = fallbackSchemeId;
            return ESInputRuntimeBuilder.Build(config, effectiveProfile, fallbackSchemeId);
        }

        public static ESInputBindingProfile LoadProfileOrDefault(string filePath = null)
        {
            return ESInputProfileIO.LoadOrCreateDefault(filePath);
        }

        public static void SaveProfile(ESInputBindingProfile profile, string filePath = null)
        {
            ESInputProfileIO.Save(profile, filePath);
        }

        public static string ToJson(ESInputBindingProfile profile, bool prettyPrint = false)
        {
            return ESInputProfileIO.ToJson(profile, prettyPrint);
        }

        public static ESInputBindingProfile FromJsonOrDefault(string json)
        {
            return ESInputProfileIO.FromJsonOrDefault(json);
        }

        public static ESInputBindingProfile CreateDefaultProfile()
        {
            return ESInputProfileIO.CreateDefaultProfile();
        }

        public static bool ResetBindingToDefault(ESInputBindingProfile profile, string bindingId)
        {
            return profile != null && profile.RemoveOverride(bindingId);
        }

        public static void ResetAllBindingsToDefault(ESInputBindingProfile profile)
        {
            if (profile == null)
                return;

            profile.Normalize();
            profile.overrides.Clear();
        }

        public static string MakeBindingId(ESInputActionDefine action, ESInputBindingDefine binding, int duplicateIndex = 0)
        {
            return ESInputBindingKeyUtility.MakeBindingId(action, binding, duplicateIndex);
        }

        public static void EnsureBindingName(ESInputBindingDefine binding)
        {
            ESInputBindingKeyUtility.EnsureBindingName(binding);
        }

        public static void EnsureConfigBindingIds(IESInputRuntimeConfigSource config)
        {
            if (config == null)
                return;

            for (int i = 0; i < config.ActionCount; i++)
            {
                if (!config.TryGetActionDefine(i, out ESInputActionDefine action) || action == null || action.bindings == null)
                    continue;

                action.NormalizeTriggerSettings();

                Dictionary<string, int> duplicateCounters = new Dictionary<string, int>();
                for (int b = 0; b < action.bindings.Count; b++)
                {
                    ESInputBindingDefine binding = action.bindings[b];
                    if (binding == null)
                        continue;

                    ESInputBindingKeyUtility.EnsureBindingName(binding);
                    if (!string.IsNullOrEmpty(binding.bindingId))
                        continue;

                    string baseKey = ESInputBindingKeyUtility.MakeBindingBaseKey(action, binding);
                    duplicateCounters.TryGetValue(baseKey, out int duplicateIndex);
                    duplicateCounters[baseKey] = duplicateIndex + 1;
                    binding.bindingId = ESInputBindingKeyUtility.MakeBindingId(action, binding, duplicateIndex);
                }
            }
        }
    }
}
