using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferAudioClip/ESAssetReferAudioClipConfigKey.cs")]
    public enum ESAssetReferAudioClipEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferAudioClipConfigKey : ESAssetConfigKey<ESAssetReferAudioClipEnumKey> { }
}
