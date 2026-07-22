using System;

namespace ES
{
    public enum ESAssetReferMeshEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferMeshConfigKey : ESAssetConfigKey<ESAssetReferMeshEnumKey> { }
}
