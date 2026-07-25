using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferTexture/ESAssetReferTextureConfigKey.cs")]
    public enum ESAssetReferTextureEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferTextureConfigKey : ESAssetConfigKey<ESAssetReferTextureEnumKey> { }
}
