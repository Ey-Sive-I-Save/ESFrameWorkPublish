using System;

namespace ES
{
    public enum ESAssetReferMaterialEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferMaterialConfigKey : ESAssetConfigKey<ESAssetReferMaterialEnumKey> { }
}
