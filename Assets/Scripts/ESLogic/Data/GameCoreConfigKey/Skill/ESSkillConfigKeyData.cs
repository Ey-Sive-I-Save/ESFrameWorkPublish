using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Skill/ESSkillConfigKeyData.cs")]
    public enum ESSkillEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
        Custom = 1
    }

    [Serializable]
    public sealed class ESSkillConfigKey : ESGameCoreConfigKey<ESSkillEnumKey>
    {
        public static implicit operator ESSkillConfigKey(ESSkillEnumKey value)
            => new ESSkillConfigKey { enumKey = value };

        public static implicit operator ESSkillConfigKey(string value)
            => new ESSkillConfigKey { stringKey = value };
    }

    [Serializable]
    public sealed class ESSkillRuntimeData : ESGameCoreRuntimeData
    {
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public SkillDefinitionDataInfo soSource;
        public SkillTrackProcessInfo trackProcess;
        public StateAniDataInfo baseStateInfo;
        public GameObject prefab;
        public UnityEngine.Object extraAsset;

        protected override void ReleaseRuntimePayload()
        {
            soSource = null;
            trackProcess = null;
            baseStateInfo = null;
            prefab = null;
            extraAsset = null;
        }

    }

    /// <summary>Skill 领域表。调用方只提供技能领域参数，不需要手工创建 ESSkillRuntimeData。</summary>
    public sealed class ESSkillConfigKeyTable : ESGameCoreConfigKeyTable<ESSkillRuntimeData>
    {
        public ESSkillConfigKeyTable(int capacity = 128) : base(capacity, "GameCore.Skill") { }

        public int Inject(ESSkillEnumKey key, ESSkillRuntimeData data, string debugName = null)
            => CommitRetained((ESSkillConfigKey)key, data, debugName);

        public bool TryInject(
            ESSkillEnumKey key,
            ESSkillRuntimeData data,
            out int runtimeKey,
            string debugName = null)
            => TryCommitRetained((ESSkillConfigKey)key, data, out runtimeKey, debugName);

        public int InjectWith(
            ESSkillConfigKey key,
            SkillTrackProcessInfo trackProcess = null,
            StateAniDataInfo baseStateInfo = null,
            string displayName = null,
            GameObject prefab = null,
            UnityEngine.Object extraAsset = null,
            string sourcePackage = null,
            string version = null)
        {
            ValidateKey(key);
            ESSkillRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                CreateRuntimeData(
                    runtimeData, key, trackProcess, baseStateInfo,
                    displayName, prefab, extraAsset, sourcePackage, version);
                return CommitRetained(key, runtimeData, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        public bool TryInjectWith(
            ESSkillConfigKey key,
            out int runtimeKey,
            SkillTrackProcessInfo trackProcess = null,
            StateAniDataInfo baseStateInfo = null,
            string displayName = null,
            GameObject prefab = null,
            UnityEngine.Object extraAsset = null,
            string sourcePackage = null,
            string version = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured)
                return false;

            if (!TryAcquireRetained(key, out ESSkillRuntimeData runtimeData))
                return false;
            try
            {
                CreateRuntimeData(
                    runtimeData, key, trackProcess, baseStateInfo,
                    displayName, prefab, extraAsset, sourcePackage, version);
                return TryCommitRetained(key, runtimeData, out runtimeKey, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        private static ESSkillRuntimeData CreateRuntimeData(
            ESSkillRuntimeData runtimeData,
            ESSkillConfigKey key,
            SkillTrackProcessInfo trackProcess,
            StateAniDataInfo baseStateInfo,
            string displayName,
            GameObject prefab,
            UnityEngine.Object extraAsset,
            string sourcePackage,
            string version)
        {
            string keyName = ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
            runtimeData.keyName = keyName;
            runtimeData.displayName = string.IsNullOrWhiteSpace(displayName) ? keyName : displayName;
            runtimeData.sourcePackage = sourcePackage ?? string.Empty;
            runtimeData.version = version ?? string.Empty;
            runtimeData.trackProcess = trackProcess;
            runtimeData.baseStateInfo = baseStateInfo;
            runtimeData.prefab = prefab;
            runtimeData.extraAsset = extraAsset;
            return runtimeData;
        }

        private static void ValidateKey(ESSkillConfigKey key)
        {
            if (key == null || !key.IsConfigured)
                throw new InvalidOperationException("Skill InjectWith 必须提供有效 ConfigKey。");
        }
    }
}
