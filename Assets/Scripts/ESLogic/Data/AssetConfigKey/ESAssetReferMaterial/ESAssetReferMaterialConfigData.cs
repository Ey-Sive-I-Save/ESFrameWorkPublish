using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferMaterialConfigData : ESAssetReferConfigDataBase<UnityEngine.Material>, IESAssetConfigDataInitializer<ESAssetReferMaterialConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferMaterialConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferMaterialConfigKey>.InitializeFromRecord(ESAssetReferMaterialConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
