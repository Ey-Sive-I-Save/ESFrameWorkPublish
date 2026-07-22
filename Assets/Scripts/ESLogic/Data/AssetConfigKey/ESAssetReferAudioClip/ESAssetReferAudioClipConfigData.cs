using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferAudioClipConfigData : ESAssetReferConfigDataBase<UnityEngine.AudioClip>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferAudioClipConfigKey key;
    }
}
