using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferPlayableAsset/ESAssetReferPlayableAssetConfigKey.cs")]
    public enum ESAssetReferPlayableAssetEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferPlayableAssetConfigKey : ESAssetConfigKey<ESAssetReferPlayableAssetEnumKey> { }
}
