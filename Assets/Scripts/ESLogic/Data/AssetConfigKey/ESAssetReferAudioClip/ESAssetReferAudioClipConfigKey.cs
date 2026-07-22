using System;

namespace ES
{
    public enum ESAssetReferAudioClipEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferAudioClipConfigKey : ESAssetConfigKey<ESAssetReferAudioClipEnumKey> { }
}
