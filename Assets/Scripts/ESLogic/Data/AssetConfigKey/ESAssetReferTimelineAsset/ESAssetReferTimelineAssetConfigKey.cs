using System;

namespace ES
{
    public enum ESAssetReferTimelineAssetEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferTimelineAssetConfigKey : ESAssetConfigKey<ESAssetReferTimelineAssetEnumKey> { }
}
