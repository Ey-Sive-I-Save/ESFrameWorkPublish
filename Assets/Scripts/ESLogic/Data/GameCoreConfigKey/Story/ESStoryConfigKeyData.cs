using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Story/ESStoryConfigKeyData.cs")]
    public enum ESStoryEnumKey : ushort
    {
        [InspectorName("未配置（使用 StringKey）")] None = 0
    }

    [Serializable]
    public sealed class ESStoryConfigKey : ESGameCoreConfigKey<ESStoryEnumKey>
    {
        public static implicit operator ESStoryConfigKey(string value)
            => new ESStoryConfigKey { stringKey = value };
    }

    [Serializable]
    public sealed class ESStoryDefinitionRuntimeData : ESGameCoreRuntimeData
    {
        public string definitionId;
        public int contentVersion;
        public string contentSignature;
        public ESStoryDefinitionSnapshot snapshot;

        protected override void ReleaseRuntimePayload()
        {
            definitionId = null;
            contentSignature = null;
            snapshot = null;
        }
    }

    public sealed class ESStoryConfigKeyTable : ESGameCoreConfigKeyTable<ESStoryDefinitionRuntimeData>
    {
        public ESStoryConfigKeyTable(int capacity = 128) : base(capacity, "GameCore.Story") { }
    }
}
