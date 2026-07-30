using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferTexture2DConfigData : ESAssetReferConfigDataBase<UnityEngine.Texture2D>, IESAssetConfigDataInitializer<ESAssetReferTexture2DConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferTexture2DConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferTexture2DConfigKey>.InitializeFromRecord(ESAssetReferTexture2DConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
