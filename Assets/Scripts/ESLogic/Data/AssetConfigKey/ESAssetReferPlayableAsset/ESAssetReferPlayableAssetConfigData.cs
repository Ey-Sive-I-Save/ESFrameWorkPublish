using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferPlayableAssetConfigData : ESAssetReferConfigDataBase<UnityEngine.Playables.PlayableAsset>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferPlayableAssetConfigKey key;
    }
}
