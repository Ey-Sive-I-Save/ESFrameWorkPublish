using System;

namespace ES
{
    public enum ESAssetReferTextureEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferTextureConfigKey : ESAssetConfigKey<ESAssetReferTextureEnumKey> { }
}
