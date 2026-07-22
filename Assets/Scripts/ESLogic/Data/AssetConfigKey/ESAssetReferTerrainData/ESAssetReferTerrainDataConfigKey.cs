using System;

namespace ES
{
    public enum ESAssetReferTerrainDataEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferTerrainDataConfigKey : ESAssetConfigKey<ESAssetReferTerrainDataEnumKey> { }
}
