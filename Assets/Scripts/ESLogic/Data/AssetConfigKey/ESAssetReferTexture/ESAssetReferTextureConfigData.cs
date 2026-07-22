using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferTextureConfigData : ESAssetReferConfigDataBase<UnityEngine.Texture>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferTextureConfigKey key;
    }
}
