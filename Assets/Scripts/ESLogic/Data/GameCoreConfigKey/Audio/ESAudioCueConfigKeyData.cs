using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Audio/ESAudioCueConfigKeyData.cs")]
    public enum ESAudioCueEnumKey : ushort
    {
        [InspectorName("未配置")] None = 0,
        [InspectorName("自定义")] Custom = 1
    }

    [Serializable]
    public sealed class ESAudioCueKey : ESGameCoreConfigKey<ESAudioCueEnumKey>
    {
        public static implicit operator ESAudioCueKey(ESAudioCueEnumKey value)
            => new ESAudioCueKey { enumKey = value };

        public static implicit operator ESAudioCueKey(string value)
            => new ESAudioCueKey { stringKey = value };
    }

    /// <summary>Process-local Cue record. Stable identity remains ESAudioCueKey.</summary>
    [Serializable]
    public sealed class ESAudioCueRuntimeData : ESGameCoreRuntimeData
    {
        public string keyName;
        public string displayName;
        public ESAudioCueInfo source;

        protected override void ReleaseRuntimePayload()
        {
            keyName = null;
            displayName = null;
            source = null;
        }
    }

    public sealed class ESAudioCueConfigKeyTable : ESGameCoreConfigKeyTable<ESAudioCueRuntimeData>
    {
        public ESAudioCueConfigKeyTable(int capacity = 128) : base(capacity, "GameCore.AudioCue") { }
    }
}
