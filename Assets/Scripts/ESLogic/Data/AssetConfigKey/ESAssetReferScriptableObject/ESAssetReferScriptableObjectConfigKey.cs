using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferScriptableObject/ESAssetReferScriptableObjectConfigKey.cs")]
    public enum ESAssetReferScriptableObjectEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferScriptableObjectConfigKey : ESAssetConfigKey<ESAssetReferScriptableObjectEnumKey> { }
}
