using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferSprite/ESAssetReferSpriteConfigKey.cs")]
    public enum ESAssetReferSpriteEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferSpriteConfigKey : ESAssetConfigKey<ESAssetReferSpriteEnumKey> { }
}
