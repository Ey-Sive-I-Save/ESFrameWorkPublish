using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    public sealed class ESInputBindingProfile
    {
        public const int CurrentSchemaVersion = 2;

        [LabelText("档案版本")]
        public int schemaVersion = CurrentSchemaVersion;

        [LabelText("档案 ID")]
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
            ESInputBindingOverride item = FindEditableOverride(bindingId);
            item.enabled = true;
            item.bindingId = bindingId;
            item.actionId = actionId;
            item.schemeId = schemeId;
            item.bindingName = bindingName;
            item.originalPath = originalPath;
            item.overridePathEnabled = true;
            item.overridePath = overridePath;
            item.overrideVirtualControlEnabled = false;
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
            ESInputBindingOverride item = FindEditableOverride(bindingId);
            item.enabled = true;
            item.bindingId = bindingId;
            item.actionId = actionId;
            item.schemeId = schemeId;
            item.bindingName = bindingName;
            item.originalPath = string.Empty;
            item.overridePathEnabled = false;
            item.overridePath = string.Empty;
            item.overrideVirtualControlEnabled = true;
            item.overrideVirtualControlId = overrideVirtualControlId;
            return item;
        }

        public ESInputBindingOverride SetInputSystemBindingOverride(
            string bindingId,
            ESInputActionId actionId,
            string schemeId,
            string bindingName,
            string originalPath,
            string overridePath,
            string overrideInteractions,
            string overrideProcessors,
            string overrideBindingName,
            bool overrideIsComposite,
            bool overrideIsPartOfComposite)
        {
            ESInputBindingOverride item = FindEditableOverride(bindingId);
            item.enabled = true;
            item.bindingId = bindingId;
            item.actionId = actionId;
            item.schemeId = schemeId;
            item.bindingName = bindingName;
            item.originalPath = originalPath;

            item.overridePathEnabled = true;
            item.overridePath = overridePath;
            item.overrideVirtualControlEnabled = false;
            item.overrideVirtualControlId = string.Empty;

            item.overrideInteractionsEnabled = true;
            item.overrideInteractions = overrideInteractions;
            item.overrideProcessorsEnabled = true;
            item.overrideProcessors = overrideProcessors;
            item.overrideBindingNameEnabled = true;
            item.overrideBindingName = overrideBindingName;
            item.overrideCompositeFlagsEnabled = true;
            item.overrideIsComposite = overrideIsComposite;
            item.overrideIsPartOfComposite = overrideIsPartOfComposite;
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

        private ESInputBindingOverride FindEditableOverride(string bindingId)
        {
            Normalize();

            if (!string.IsNullOrEmpty(bindingId))
            {
                for (int i = 0; i < overrides.Count; i++)
                {
                    ESInputBindingOverride item = overrides[i];
                    if (item != null && string.Equals(item.bindingId, bindingId, StringComparison.Ordinal))
                        return item;
                }
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

        [LabelText("绑定 ID")]
        public string bindingId;

        [LabelText("动作 ID")]
        public ESInputActionId actionId;

        [LabelText("方案 ID")]
        public string schemeId;

        [LabelText("绑定名称")]
        public string bindingName;

        [LabelText("原始路径")]
        public string originalPath;

        [LabelText("覆盖路径")]
        public bool overridePathEnabled;

        [LabelText("路径")]
        public string overridePath;

        [LabelText("覆盖虚拟控件")]
        public bool overrideVirtualControlEnabled;

        [LabelText("虚拟控件 ID")]
        public string overrideVirtualControlId;

        [LabelText("覆盖交互参数")]
        public bool overrideInteractionsEnabled;

        [LabelText("交互参数")]
        public string overrideInteractions;

        [LabelText("覆盖处理器")]
        public bool overrideProcessorsEnabled;

        [LabelText("处理器")]
        public string overrideProcessors;

        [LabelText("覆盖绑定名称")]
        public bool overrideBindingNameEnabled;

        [LabelText("绑定名称")]
        public string overrideBindingName;

        [LabelText("覆盖组合标记")]
        public bool overrideCompositeFlagsEnabled;

        [LabelText("组合绑定")]
        public bool overrideIsComposite;

        [LabelText("组合部分")]
        public bool overrideIsPartOfComposite;
    }
}
