using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferTexture2DConfigData : ESAssetReferConfigDataBase<UnityEngine.Texture2D>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferTexture2DConfigKey key;
    }
}
