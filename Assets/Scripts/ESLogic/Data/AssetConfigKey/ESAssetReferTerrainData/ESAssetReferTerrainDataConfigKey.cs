using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferTerrainData/ESAssetReferTerrainDataConfigKey.cs")]
    public enum ESAssetReferTerrainDataEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferTerrainDataConfigKey : ESAssetConfigKey<ESAssetReferTerrainDataEnumKey> { }
}
