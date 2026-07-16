using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    public sealed class ESInputBindingProfile
    {
        public const int CurrentSchemaVersion = 1;

        [LabelText("档案版本")]
        public int schemaVersion = CurrentSchemaVersion;

        [LabelText("档案ID")]
        public string profileId = "Default";

        [LabelText("显示名称")]
        public string displayName = "默认键位";

        [LabelText("当前方案")]
        public string activeSchemeId = ESInputSchemeIds.KeyboardMouse;

        [LabelText("覆盖列表")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<ESInputBindingOverride> overrides = new List<ESInputBindingOverride>();

        public void Normalize()
        {
            if (schemaVersion <= 0)
                schemaVersion = CurrentSchemaVersion;

            if (string.IsNullOrEmpty(profileId))
                profileId = "Default";

            if (string.IsNullOrEmpty(displayName))
                displayName = "默认键位";

            if (string.IsNullOrEmpty(activeSchemeId))
                activeSchemeId = ESInputSchemeIds.KeyboardMouse;

            if (overrides == null)
                overrides = new List<ESInputBindingOverride>();
        }

        public ESInputBindingOverride SetPathOverride(
            string bindingId,
            ESInputActionId actionId,
            string schemeId,
            string bindingName,
            string originalPath,
            string overridePath)
        {
            ESInputBindingOverride item = FindEditableOverride(bindingId, actionId, schemeId, bindingName, originalPath);
            item.enabled = true;
            item.bindingId = bindingId;
            item.actionId = actionId;
            item.schemeId = schemeId;
            item.bindingName = bindingName;
            item.originalPath = originalPath;
            item.overridePath = overridePath;
            item.overrideVirtualControlId = string.Empty;
            return item;
        }

        public ESInputBindingOverride SetVirtualControlOverride(
            string bindingId,
            ESInputActionId actionId,
            string schemeId,
            string bindingName,
            string overrideVirtualControlId)
        {
            ESInputBindingOverride item = FindEditableOverride(bindingId, actionId, schemeId, bindingName, string.Empty);
            item.enabled = true;
            item.bindingId = bindingId;
            item.actionId = actionId;
            item.schemeId = schemeId;
            item.bindingName = bindingName;
            item.originalPath = string.Empty;
            item.overridePath = string.Empty;
            item.overrideVirtualControlId = overrideVirtualControlId;
            return item;
        }

        public bool RemoveOverride(string bindingId)
        {
            if (overrides == null || string.IsNullOrEmpty(bindingId))
                return false;

            for (int i = overrides.Count - 1; i >= 0; i--)
            {
                ESInputBindingOverride item = overrides[i];
                if (item != null && string.Equals(item.bindingId, bindingId, StringComparison.Ordinal))
                {
                    overrides.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public bool TryGetOverride(
            string bindingId,
            ESInputActionId actionId,
            string schemeId,
            string bindingName,
            string originalPath,
            out ESInputBindingOverride result)
        {
            if (overrides != null)
            {
                for (int i = 0; i < overrides.Count; i++)
                {
                    ESInputBindingOverride item = overrides[i];
                    if (item == null || !item.enabled)
                        continue;

                    if (!string.IsNullOrEmpty(bindingId)
                        && string.Equals(item.bindingId, bindingId, StringComparison.Ordinal))
                    {
                        result = item;
                        return true;
                    }

                    if (string.IsNullOrEmpty(item.bindingId)
                        && item.actionId == actionId
                        && string.Equals(item.schemeId, schemeId, StringComparison.Ordinal)
                        && string.Equals(item.bindingName, bindingName, StringComparison.Ordinal)
                        && string.Equals(item.originalPath, originalPath, StringComparison.Ordinal))
                    {
                        result = item;
                        return true;
                    }
                }
            }

            result = null;
            return false;
        }

        private ESInputBindingOverride FindEditableOverride(
            string bindingId,
            ESInputActionId actionId,
            string schemeId,
            string bindingName,
            string originalPath)
        {
            Normalize();

            for (int i = 0; i < overrides.Count; i++)
            {
                ESInputBindingOverride item = overrides[i];
                if (item == null)
                    continue;

                if (!string.IsNullOrEmpty(bindingId)
                    && string.Equals(item.bindingId, bindingId, StringComparison.Ordinal))
                    return item;

                if (string.IsNullOrEmpty(item.bindingId)
                    && item.actionId == actionId
                    && string.Equals(item.schemeId, schemeId, StringComparison.Ordinal)
                    && string.Equals(item.bindingName, bindingName, StringComparison.Ordinal)
                    && string.Equals(item.originalPath, originalPath, StringComparison.Ordinal))
                    return item;
            }

            ESInputBindingOverride created = new ESInputBindingOverride();
            overrides.Add(created);
            return created;
        }
    }

    [Serializable]
    public sealed class ESInputBindingOverride
    {
        [LabelText("启用")]
        public bool enabled = true;

        [LabelText("绑定ID")]
        public string bindingId;

        [LabelText("动作ID")]
        public ESInputActionId actionId;

        [LabelText("方案ID")]
        public string schemeId;

        [LabelText("绑定名称")]
        public string bindingName;

        [LabelText("原始路径")]
        public string originalPath;

        [LabelText("覆盖路径")]
        public string overridePath;

        [LabelText("覆盖虚拟控件")]
        public string overrideVirtualControlId;
    }
}
