using System;
using System.Collections.Generic;

namespace ES
{
    public interface IESInputRuntimeConfigSource
    {
        int ActionCount { get; }
        bool TryGetActionDefine(int index, out ESInputActionDefine action);
    }

    public struct ESInputCompiledBinding
    {
        public string bindingId;
        public ESInputActionId actionId;
        public ESInputBindingSource source;
        public string schemeId;
        public string name;
        public string originalPath;
        public string effectivePath;
        public string virtualControlId;
        public string interactions;
        public string processors;
        public bool isComposite;
        public bool isPartOfComposite;
    }

    public sealed class ESInputRuntimeBuildResult
    {
        public ESInputRuntimeCache cache;
        public ESInputCompiledBinding[] bindings;
        public int bindingCount;
        public string activeSchemeId;

        public ESInputRuntimeBuildResult(int actionCapacity, int bindingCapacity)
        {
            cache = new ESInputRuntimeCache(actionCapacity);
            bindings = new ESInputCompiledBinding[bindingCapacity < 0 ? 0 : bindingCapacity];
        }
    }

    public static class ESInputRuntimeBuilder
    {
        private sealed class OverrideLookup
        {
            public readonly Dictionary<string, ESInputBindingOverride> byBindingId = new Dictionary<string, ESInputBindingOverride>(StringComparer.Ordinal);
        }

        public static ESInputRuntimeBuildResult Build(
            IESInputRuntimeConfigSource source,
            ESInputBindingProfile profile,
            string fallbackSchemeId)
        {
            if (source == null)
                return new ESInputRuntimeBuildResult(1, 0);

            int actionCount = source.ActionCount;
            int maxIndex = 0;
            int bindingCapacity = 0;

            for (int i = 0; i < actionCount; i++)
            {
                if (!source.TryGetActionDefine(i, out ESInputActionDefine action) || action == null)
                    continue;

                int actionIndex = (int)action.id;
                if (action.id != ESInputActionId.Dynamic && actionIndex > maxIndex)
                    maxIndex = actionIndex;

                if (action.bindings != null)
                    bindingCapacity += action.bindings.Count;
            }

            string activeSchemeId = profile != null && !string.IsNullOrEmpty(profile.activeSchemeId)
                ? profile.activeSchemeId
                : fallbackSchemeId;

            ESInputRuntimeBuildResult result = new ESInputRuntimeBuildResult(maxIndex + 1, bindingCapacity);
            result.activeSchemeId = activeSchemeId;

            OverrideLookup overrideLookup = BuildOverrideLookup(profile);
            int bindingIndex = 0;
            for (int i = 0; i < actionCount; i++)
            {
                if (!source.TryGetActionDefine(i, out ESInputActionDefine action) || action == null)
                    continue;

                int actionIndex = (int)action.id;
                if (action.id != ESInputActionId.Dynamic && result.cache.IsValidIndex(actionIndex))
                {
                    result.cache.metas[actionIndex] = new ESInputActionMeta
                    {
                        id = action.id,
                        actionName = action.actionName,
                        valueType = action.valueType,
                        category = action.category,
                        allowRebind = action.allowRebind,
                        triggerType = action.triggerType,
                        triggerFeatures = action.GetEffectiveTriggerFeatures(),
                        pressPolicy = action.pressPolicy,
                        longPressDuration = action.longPressDuration,
                        doublePressWindow = action.doublePressWindow,
                        displayName = string.IsNullOrEmpty(action.displayName)
                            ? ESInputDefineUtility.GetDefaultChineseName(action.id)
                            : action.displayName
                    };
                }

                if (action.bindings == null)
                    continue;

                Dictionary<string, int> duplicateCounters = new Dictionary<string, int>();
                for (int b = 0; b < action.bindings.Count; b++)
                {
                    ESInputBindingDefine binding = action.bindings[b];
                    if (binding == null || bindingIndex >= result.bindings.Length)
                        continue;

                    result.bindings[bindingIndex++] = CompileBinding(action, binding, overrideLookup, duplicateCounters);
                }
            }

            result.bindingCount = bindingIndex;
            return result;
        }

        private static ESInputCompiledBinding CompileBinding(
            ESInputActionDefine action,
            ESInputBindingDefine binding,
            OverrideLookup overrideLookup,
            Dictionary<string, int> duplicateCounters)
        {
            string bindingId = ResolveBindingId(action, binding, duplicateCounters);
            string effectivePath = binding.path;
            string virtualControlId = binding.virtualControlId;
            string effectiveName = binding.name;
            string interactions = binding.interactions;
            string processors = binding.processors;
            bool isComposite = binding.isComposite;
            bool isPartOfComposite = binding.isPartOfComposite;

            if (action.allowRebind
                && TryGetOverride(overrideLookup, bindingId, out ESInputBindingOverride overrideData))
            {
                if (overrideData.overridePathEnabled)
                    effectivePath = overrideData.overridePath;

                if (overrideData.overrideVirtualControlEnabled)
                    virtualControlId = overrideData.overrideVirtualControlId;

                if (overrideData.overrideInteractionsEnabled)
                    interactions = overrideData.overrideInteractions;

                if (overrideData.overrideProcessorsEnabled)
                    processors = overrideData.overrideProcessors;

                if (overrideData.overrideBindingNameEnabled)
                    effectiveName = overrideData.overrideBindingName;

                if (overrideData.overrideCompositeFlagsEnabled)
                {
                    isComposite = overrideData.overrideIsComposite;
                    isPartOfComposite = overrideData.overrideIsPartOfComposite;
                }
            }

            return new ESInputCompiledBinding
            {
                bindingId = bindingId,
                actionId = action.id,
                source = binding.source,
                schemeId = binding.schemeId,
                name = effectiveName,
                originalPath = binding.path,
                effectivePath = effectivePath,
                virtualControlId = virtualControlId,
                interactions = interactions,
                processors = processors,
                isComposite = isComposite,
                isPartOfComposite = isPartOfComposite
            };
        }

        private static string ResolveBindingId(
            ESInputActionDefine action,
            ESInputBindingDefine binding,
            Dictionary<string, int> duplicateCounters)
        {
            if (binding != null && !string.IsNullOrEmpty(binding.bindingId))
                return binding.bindingId;

            ESInputBindingKeyUtility.EnsureBindingName(binding);
            string baseKey = ESInputBindingKeyUtility.MakeBindingBaseKey(action, binding);
            int duplicateIndex = 0;
            if (duplicateCounters != null)
            {
                duplicateCounters.TryGetValue(baseKey, out duplicateIndex);
                duplicateCounters[baseKey] = duplicateIndex + 1;
            }

            return ESInputBindingKeyUtility.MakeBindingId(action, binding, duplicateIndex);
        }

        private static OverrideLookup BuildOverrideLookup(ESInputBindingProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            profile.Normalize();
            if (profile.overrides == null || profile.overrides.Count == 0)
            {
                return null;
            }

            OverrideLookup lookup = new OverrideLookup();
            for (int i = 0; i < profile.overrides.Count; i++)
            {
                ESInputBindingOverride item = profile.overrides[i];
                if (item == null || !item.enabled)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(item.bindingId))
                    continue;

                lookup.byBindingId[item.bindingId] = item;
            }

            return lookup;
        }

        private static bool TryGetOverride(
            OverrideLookup lookup,
            string bindingId,
            out ESInputBindingOverride result)
        {
            if (lookup != null && !string.IsNullOrEmpty(bindingId))
                return lookup.byBindingId.TryGetValue(bindingId, out result);

            result = null;
            return false;
        }
    }

    public sealed class ESInputActionDefineListSource : IESInputRuntimeConfigSource
    {
        private readonly IList<ESInputActionDefine> actions;

        public ESInputActionDefineListSource(IList<ESInputActionDefine> actions)
        {
            this.actions = actions;
        }

        public int ActionCount
        {
            get { return actions == null ? 0 : actions.Count; }
        }

        public bool TryGetActionDefine(int index, out ESInputActionDefine action)
        {
            if (actions != null && index >= 0 && index < actions.Count)
            {
                action = actions[index];
                return action != null;
            }

            action = null;
            return false;
        }
    }
}
