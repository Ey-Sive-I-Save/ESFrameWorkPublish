using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferSpriteAtlasConfigData : ESAssetReferConfigDataBase<UnityEngine.U2D.SpriteAtlas>, IESAssetConfigDataInitializer<ESAssetReferSpriteAtlasConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferSpriteAtlasConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferSpriteAtlasConfigKey>.InitializeFromRecord(ESAssetReferSpriteAtlasConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
