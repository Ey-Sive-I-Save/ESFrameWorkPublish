using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferTerrainDataConfigData : ESAssetReferConfigDataBase<UnityEngine.TerrainData>, IESAssetConfigDataInitializer<ESAssetReferTerrainDataConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferTerrainDataConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferTerrainDataConfigKey>.InitializeFromRecord(ESAssetReferTerrainDataConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
