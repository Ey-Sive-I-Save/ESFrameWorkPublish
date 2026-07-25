using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferAvatar/ESAssetReferAvatarConfigKey.cs")]
    public enum ESAssetReferAvatarEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferAvatarConfigKey : ESAssetConfigKey<ESAssetReferAvatarEnumKey> { }
}
