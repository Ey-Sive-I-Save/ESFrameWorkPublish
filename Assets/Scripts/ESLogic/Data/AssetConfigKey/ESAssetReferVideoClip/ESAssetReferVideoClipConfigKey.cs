using System;

namespace ES
{
    public enum ESAssetReferVideoClipEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferVideoClipConfigKey : ESAssetConfigKey<ESAssetReferVideoClipEnumKey> { }
}
