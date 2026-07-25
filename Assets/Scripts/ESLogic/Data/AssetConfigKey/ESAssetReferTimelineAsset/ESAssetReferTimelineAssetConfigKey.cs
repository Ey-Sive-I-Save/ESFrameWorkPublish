using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferTimelineAsset/ESAssetReferTimelineAssetConfigKey.cs")]
    public enum ESAssetReferTimelineAssetEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferTimelineAssetConfigKey : ESAssetConfigKey<ESAssetReferTimelineAssetEnumKey> { }
}
