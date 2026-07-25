using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferAnimatorController/ESAssetReferAnimatorControllerConfigKey.cs")]
    public enum ESAssetReferAnimatorControllerEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferAnimatorControllerConfigKey : ESAssetConfigKey<ESAssetReferAnimatorControllerEnumKey> { }
}
