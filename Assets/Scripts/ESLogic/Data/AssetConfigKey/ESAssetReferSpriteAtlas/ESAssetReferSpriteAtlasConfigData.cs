using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferSpriteAtlasConfigData : ESAssetReferConfigDataBase<UnityEngine.U2D.SpriteAtlas>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferSpriteAtlasConfigKey key;
    }
}
