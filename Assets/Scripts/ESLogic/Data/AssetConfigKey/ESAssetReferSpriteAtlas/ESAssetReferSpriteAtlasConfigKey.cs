using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferSpriteAtlas/ESAssetReferSpriteAtlasConfigKey.cs")]
    public enum ESAssetReferSpriteAtlasEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferSpriteAtlasConfigKey : ESAssetConfigKey<ESAssetReferSpriteAtlasEnumKey> { }
}
