using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Action/ESActionConfigKeyData.cs")]
    public enum ESActionEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
        Custom = 1,
    }

    [Serializable]
    public sealed class ESActionConfigKey : ESGameCoreConfigKey<ESActionEnumKey>
    {
        public static implicit operator ESActionConfigKey(ESActionEnumKey value)
            => new ESActionConfigKey { enumKey = value };

        public static implicit operator ESActionConfigKey(string value)
            => new ESActionConfigKey { stringKey = value };
    }

    [Serializable]
    public sealed class ESActionRuntimeData : ESGameCoreRuntimeData
    {
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESActionConfigKey actionKey;
        public ActionTemplateDataInfo soSource;
        public ESActionCategory category;
        public bool allowBufferedInput = true;
        public float globalInputBufferWindow;
        public List<ESActionPhaseData> phases = new List<ESActionPhaseData>();
        public List<ESActionComboTransitionData> comboTransitions = new List<ESActionComboTransitionData>();
        public List<ESActionCancelRuleData> cancelRules = new List<ESActionCancelRuleData>();
        public List<ESActionPresentationBindingData> presentationBindings = new List<ESActionPresentationBindingData>();

        protected override void ReleaseRuntimePayload()
        {
            keyName = null;
            displayName = null;
            sourcePackage = null;
            version = null;
            actionKey = null;
            soSource = null;
            phases.Clear();
            comboTransitions.Clear();
            cancelRules.Clear();
            presentationBindings.Clear();
        }
    }

    public sealed class ESActionConfigKeyTable : ESGameCoreConfigKeyTable<ESActionRuntimeData>
    {
        public ESActionConfigKeyTable(int capacity = 64) : base(capacity, "GameCore.Action") { }

        public int InjectWith(
            ESActionConfigKey key,
            ActionTemplateDataInfo soSource,
            ESActionCategory category,
            IReadOnlyList<ESActionPhaseData> phases,
            IReadOnlyList<ESActionComboTransitionData> comboTransitions,
            IReadOnlyList<ESActionCancelRuleData> cancelRules,
            IReadOnlyList<ESActionPresentationBindingData> presentationBindings,
            bool allowBufferedInput,
            float globalInputBufferWindow,
            string displayName = null,
            string sourcePackage = null,
            string version = null)
        {
            ValidateKey(key);
            ESActionRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                string keyName = ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
                runtimeData.keyName = keyName;
                runtimeData.displayName = string.IsNullOrWhiteSpace(displayName) ? keyName : displayName;
                runtimeData.sourcePackage = sourcePackage ?? string.Empty;
                runtimeData.version = version ?? string.Empty;
                runtimeData.actionKey = CopyKey(key);
                runtimeData.soSource = soSource;
                runtimeData.category = category;
                runtimeData.allowBufferedInput = allowBufferedInput;
                runtimeData.globalInputBufferWindow = globalInputBufferWindow;
                runtimeData.phases.Clear();
                if (phases != null)
                    runtimeData.phases.AddRange(phases);
                runtimeData.comboTransitions.Clear();
                if (comboTransitions != null)
                    runtimeData.comboTransitions.AddRange(comboTransitions);
                runtimeData.cancelRules.Clear();
                if (cancelRules != null)
                    runtimeData.cancelRules.AddRange(cancelRules);
                runtimeData.presentationBindings.Clear();
                if (presentationBindings != null)
                    runtimeData.presentationBindings.AddRange(presentationBindings);
                return CommitRetained(key, runtimeData, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        public bool TryInjectWith(
            ESActionConfigKey key,
            out int runtimeKey,
            ActionTemplateDataInfo soSource,
            ESActionCategory category,
            IReadOnlyList<ESActionPhaseData> phases,
            IReadOnlyList<ESActionComboTransitionData> comboTransitions,
            IReadOnlyList<ESActionCancelRuleData> cancelRules,
            IReadOnlyList<ESActionPresentationBindingData> presentationBindings,
            bool allowBufferedInput,
            float globalInputBufferWindow,
            string displayName = null,
            string sourcePackage = null,
            string version = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured)
                return false;

            if (!TryAcquireRetained(key, out ESActionRuntimeData runtimeData))
                return false;
            try
            {
                string keyName = ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
                runtimeData.keyName = keyName;
                runtimeData.displayName = string.IsNullOrWhiteSpace(displayName) ? keyName : displayName;
                runtimeData.sourcePackage = sourcePackage ?? string.Empty;
                runtimeData.version = version ?? string.Empty;
                runtimeData.actionKey = CopyKey(key);
                runtimeData.soSource = soSource;
                runtimeData.category = category;
                runtimeData.allowBufferedInput = allowBufferedInput;
                runtimeData.globalInputBufferWindow = globalInputBufferWindow;
                runtimeData.phases.Clear();
                if (phases != null)
                    runtimeData.phases.AddRange(phases);
                runtimeData.comboTransitions.Clear();
                if (comboTransitions != null)
                    runtimeData.comboTransitions.AddRange(comboTransitions);
                runtimeData.cancelRules.Clear();
                if (cancelRules != null)
                    runtimeData.cancelRules.AddRange(cancelRules);
                runtimeData.presentationBindings.Clear();
                if (presentationBindings != null)
                    runtimeData.presentationBindings.AddRange(presentationBindings);
                return TryCommitRetained(key, runtimeData, out runtimeKey, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        private static void ValidateKey(ESActionConfigKey key)
        {
            if (key == null || !key.IsConfigured)
                throw new InvalidOperationException("Action InjectWith 必须提供有效 ConfigKey。");
        }

        private static ESActionConfigKey CopyKey(ESActionConfigKey key)
        {
            return new ESActionConfigKey
            {
                enumKey = (ESActionEnumKey)key.EnumKeyInt,
                stringKey = key.StringKey,
                definitionGuid = key.definitionGuid,
                definitionLocalFileId = key.definitionLocalFileId,
                definitionTypeName = key.definitionTypeName,
            };
        }
    }
}
