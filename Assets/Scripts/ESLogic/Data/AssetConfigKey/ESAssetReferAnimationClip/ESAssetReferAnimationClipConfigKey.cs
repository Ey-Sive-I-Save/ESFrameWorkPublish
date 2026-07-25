using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferAnimationClip/ESAssetReferAnimationClipConfigKey.cs")]
    public enum ESAssetReferAnimationClipEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferAnimationClipConfigKey : ESAssetConfigKey<ESAssetReferAnimationClipEnumKey> { }
}
