using System;

namespace ES
{
    public enum ESAssetReferAvatarEnumKey : ushort { None = 0, Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferAvatarConfigKey : ESAssetConfigKey<ESAssetReferAvatarEnumKey> { }
}
