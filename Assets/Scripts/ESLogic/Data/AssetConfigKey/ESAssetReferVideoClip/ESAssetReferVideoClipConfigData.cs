using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferVideoClipConfigData : ESAssetReferConfigDataBase<UnityEngine.Video.VideoClip>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferVideoClipConfigKey key;
    }
}
