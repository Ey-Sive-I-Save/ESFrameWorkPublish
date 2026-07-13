using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    public sealed class ESInputBindingProfile
    {
        [LabelText("档案ID")]
        public string profileId = "Default";

        [LabelText("显示名称")]
        public string displayName = "默认键位";

        [LabelText("当前方案")]
        public string activeSchemeId = ESInputSchemeIds.KeyboardMouse;

        [LabelText("覆盖列表")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<ESInputBindingOverride> overrides = new List<ESInputBindingOverride>();

        public bool TryGetOverride(
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

                    if (item.actionId == actionId
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
    }

    [Serializable]
    public sealed class ESInputBindingOverride
    {
        [LabelText("启用")]
        public bool enabled = true;

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
