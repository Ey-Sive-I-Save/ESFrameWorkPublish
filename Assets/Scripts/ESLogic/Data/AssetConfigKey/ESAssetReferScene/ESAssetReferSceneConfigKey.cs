using System;

namespace ES
{
    public enum ESAssetReferSceneEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferSceneConfigKey : ESAssetConfigKey<ESAssetReferSceneEnumKey> { }
}
