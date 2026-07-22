using System;

namespace ES
{
    public enum ESAssetReferSpriteEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferSpriteConfigKey : ESAssetConfigKey<ESAssetReferSpriteEnumKey> { }
}
