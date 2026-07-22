using System;

namespace ES
{
    public enum ESAssetReferPrefabEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferPrefabConfigKey : ESAssetConfigKey<ESAssetReferPrefabEnumKey> { }
}
