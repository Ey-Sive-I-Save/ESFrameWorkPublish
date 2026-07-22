using System;

namespace ES
{
    public enum ESAssetReferTexture2DEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferTexture2DConfigKey : ESAssetConfigKey<ESAssetReferTexture2DEnumKey> { }
}
