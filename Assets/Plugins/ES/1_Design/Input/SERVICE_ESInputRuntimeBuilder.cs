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
                        valueType = action.valueType,
                        category = action.category,
                        allowRebind = action.allowRebind,
                        longPressDuration = action.longPressDuration,
                        doublePressWindow = action.doublePressWindow,
                        displayName = string.IsNullOrEmpty(action.displayName)
                            ? ESInputDefineUtility.GetDefaultChineseName(action.id)
                            : action.displayName
                    };
                }

                if (action.bindings == null)
                    continue;

                for (int b = 0; b < action.bindings.Count; b++)
                {
                    ESInputBindingDefine binding = action.bindings[b];
                    if (binding == null || bindingIndex >= result.bindings.Length)
                        continue;

                    result.bindings[bindingIndex++] = CompileBinding(action, binding, profile);
                }
            }

            result.bindingCount = bindingIndex;
            return result;
        }

        private static ESInputCompiledBinding CompileBinding(
            ESInputActionDefine action,
            ESInputBindingDefine binding,
            ESInputBindingProfile profile)
        {
            string effectivePath = binding.path;
            string virtualControlId = binding.virtualControlId;

            if (profile != null
                && action.allowRebind
                && profile.TryGetOverride(action.id, binding.schemeId, binding.name, binding.path, out ESInputBindingOverride overrideData))
            {
                if (!string.IsNullOrEmpty(overrideData.overridePath))
                    effectivePath = overrideData.overridePath;

                if (!string.IsNullOrEmpty(overrideData.overrideVirtualControlId))
                    virtualControlId = overrideData.overrideVirtualControlId;
            }

            return new ESInputCompiledBinding
            {
                actionId = action.id,
                source = binding.source,
                schemeId = binding.schemeId,
                name = binding.name,
                originalPath = binding.path,
                effectivePath = effectivePath,
                virtualControlId = virtualControlId,
                interactions = binding.interactions,
                processors = binding.processors,
                isComposite = binding.isComposite,
                isPartOfComposite = binding.isPartOfComposite
            };
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
