using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferMeshConfigData : ESAssetReferConfigDataBase<UnityEngine.Mesh>, IESAssetConfigDataInitializer<ESAssetReferMeshConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferMeshConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferMeshConfigKey>.InitializeFromRecord(ESAssetReferMeshConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
