using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferTextureConfigData : ESAssetReferConfigDataBase<UnityEngine.Texture>, IESAssetConfigDataInitializer<ESAssetReferTextureConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferTextureConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferTextureConfigKey>.InitializeFromRecord(ESAssetReferTextureConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
