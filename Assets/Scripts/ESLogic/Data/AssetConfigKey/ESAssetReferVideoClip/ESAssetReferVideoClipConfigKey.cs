using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferVideoClip/ESAssetReferVideoClipConfigKey.cs")]
    public enum ESAssetReferVideoClipEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferVideoClipConfigKey : ESAssetConfigKey<ESAssetReferVideoClipEnumKey> { }
}
