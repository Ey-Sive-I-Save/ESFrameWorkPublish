using System;

namespace ES
{
    public enum ESAssetReferAnimationClipEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferAnimationClipConfigKey : ESAssetConfigKey<ESAssetReferAnimationClipEnumKey> { }
}
