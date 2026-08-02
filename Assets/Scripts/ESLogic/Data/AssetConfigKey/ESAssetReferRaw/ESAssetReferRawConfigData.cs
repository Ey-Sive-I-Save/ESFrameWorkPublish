using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferRawConfigData : ESAssetReferConfigDataBase<UnityEngine.TextAsset>, IESAssetConfigDataInitializer<ESAssetReferRawConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferRawConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferRawConfigKey>.InitializeFromRecord(ESAssetReferRawConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
