using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferMaterial/ESAssetReferMaterialConfigKey.cs")]
    public enum ESAssetReferMaterialEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferMaterialConfigKey : ESAssetConfigKey<ESAssetReferMaterialEnumKey> { }
}
