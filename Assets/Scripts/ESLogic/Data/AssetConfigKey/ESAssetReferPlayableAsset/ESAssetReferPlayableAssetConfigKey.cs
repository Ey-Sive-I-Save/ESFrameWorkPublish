using System;

namespace ES
{
    public enum ESAssetReferPlayableAssetEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferPlayableAssetConfigKey : ESAssetConfigKey<ESAssetReferPlayableAssetEnumKey> { }
}
