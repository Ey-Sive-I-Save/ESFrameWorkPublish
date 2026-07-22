using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferAvatarConfigData : ESAssetReferConfigDataBase<UnityEngine.Avatar>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferAvatarConfigKey key;
    }
}
