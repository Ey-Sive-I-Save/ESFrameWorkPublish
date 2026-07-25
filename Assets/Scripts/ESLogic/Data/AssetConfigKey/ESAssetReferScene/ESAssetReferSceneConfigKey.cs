using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferScene/ESAssetReferSceneConfigKey.cs")]
    public enum ESAssetReferSceneEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferSceneConfigKey : ESAssetConfigKey<ESAssetReferSceneEnumKey> { }
}
