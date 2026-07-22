using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferTimelineAssetConfigData : ESAssetReferConfigDataBase<UnityEngine.Object>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferTimelineAssetConfigKey key;
    }
}
