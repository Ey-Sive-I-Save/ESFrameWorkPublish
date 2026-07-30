using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferSpriteConfigData : ESAssetReferConfigDataBase<UnityEngine.Sprite>, IESAssetConfigDataInitializer<ESAssetReferSpriteConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferSpriteConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferSpriteConfigKey>.InitializeFromRecord(ESAssetReferSpriteConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
