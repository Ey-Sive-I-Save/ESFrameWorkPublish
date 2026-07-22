using System;

namespace ES
{
    public enum ESAssetReferAnimatorControllerEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferAnimatorControllerConfigKey : ESAssetConfigKey<ESAssetReferAnimatorControllerEnumKey> { }
}
