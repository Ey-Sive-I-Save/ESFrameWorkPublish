using System;

namespace ES
{
    public enum ESAssetReferSpriteAtlasEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferSpriteAtlasConfigKey : ESAssetConfigKey<ESAssetReferSpriteAtlasEnumKey> { }
}
