using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferPrefabConfigData : ESAssetReferConfigDataBase<UnityEngine.GameObject>, IESAssetConfigDataInitializer<ESAssetReferPrefabConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferPrefabConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferPrefabConfigKey>.InitializeFromRecord(ESAssetReferPrefabConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
