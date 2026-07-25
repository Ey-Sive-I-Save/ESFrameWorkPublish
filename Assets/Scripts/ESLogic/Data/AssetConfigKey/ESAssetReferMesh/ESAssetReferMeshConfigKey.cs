using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferMesh/ESAssetReferMeshConfigKey.cs")]
    public enum ESAssetReferMeshEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferMeshConfigKey : ESAssetConfigKey<ESAssetReferMeshEnumKey> { }
}
