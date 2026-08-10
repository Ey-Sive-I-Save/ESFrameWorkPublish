using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Action/ESSkillTrackConfigKeyData.cs")]
    public enum ESSkillTrackEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
        Custom = 1,
    }

    [Serializable]
    public sealed class ESSkillTrackConfigKey : ESGameCoreConfigKey<ESSkillTrackEnumKey>
    {
        public static implicit operator ESSkillTrackConfigKey(ESSkillTrackEnumKey value)
            => new ESSkillTrackConfigKey { enumKey = value };

        public static implicit operator ESSkillTrackConfigKey(string value)
            => new ESSkillTrackConfigKey { stringKey = value };
    }

    [Serializable]
    public sealed class ESSkillTrackRuntimeData : ESGameCoreRuntimeData
    {
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;

        protected override void ReleaseRuntimePayload()
        {
            keyName = null;
            displayName = null;
            sourcePackage = null;
            version = null;
        }
    }

    public sealed class ESSkillTrackConfigKeyTable : ESGameCoreConfigKeyTable<ESSkillTrackRuntimeData>
    {
        public ESSkillTrackConfigKeyTable(int capacity = 128) : base(capacity, "GameCore.SkillTrack") { }

        public int InjectWith(
            ESSkillTrackConfigKey key,
            string displayName = null,
            string sourcePackage = null,
            string version = null)
        {
            ValidateKey(key);
            ESSkillTrackRuntimeData runtimeData = AcquireRetained(key);
            try
            {
                string keyName = ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
                runtimeData.keyName = keyName;
                runtimeData.displayName = string.IsNullOrWhiteSpace(displayName) ? keyName : displayName;
                runtimeData.sourcePackage = sourcePackage ?? string.Empty;
                runtimeData.version = version ?? string.Empty;
                return CommitRetained(key, runtimeData, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        public bool TryInjectWith(
            ESSkillTrackConfigKey key,
            out int runtimeKey,
            string displayName = null,
            string sourcePackage = null,
            string version = null)
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured)
                return false;

            if (TryGet(key, out _))
            {
                return TryGetRuntimeKey(key, out runtimeKey);
            }

            if (!TryAcquireRetained(key, out ESSkillTrackRuntimeData runtimeData))
                return false;
            try
            {
                string keyName = ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
                runtimeData.keyName = keyName;
                runtimeData.displayName = string.IsNullOrWhiteSpace(displayName) ? keyName : displayName;
                runtimeData.sourcePackage = sourcePackage ?? string.Empty;
                runtimeData.version = version ?? string.Empty;
                return TryCommitRetained(key, runtimeData, out runtimeKey, runtimeData.displayName);
            }
            catch
            {
                AbandonRetained(runtimeData);
                throw;
            }
        }

        private static void ValidateKey(ESSkillTrackConfigKey key)
        {
            if (key == null || !key.IsConfigured)
                throw new InvalidOperationException("SkillTrack InjectWith 必须提供有效 ConfigKey。");
        }
    }

    public static class ESSkillTrackGameCoreTable
    {
        public static ESSkillTrackConfigKeyTable Table => ESRuntimeDataGameCore.SkillTracks;

        public static void Inject(ESSkillTrackConfigKey key, string displayName = null)
        {
            if (key == null || !key.IsConfigured)
                throw new InvalidOperationException("SkillTrack 必须显式配置 EnumKey 或 StringKey。");

            Table.TryInjectWith(key, out _, displayName);
        }
    }
}
