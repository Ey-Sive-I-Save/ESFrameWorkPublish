using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferTexture2D/ESAssetReferTexture2DConfigKey.cs")]
    public enum ESAssetReferTexture2DEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferTexture2DConfigKey : ESAssetConfigKey<ESAssetReferTexture2DEnumKey> { }
}
