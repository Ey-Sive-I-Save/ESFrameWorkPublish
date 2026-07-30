using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferPlayableAssetConfigData : ESAssetReferConfigDataBase<UnityEngine.Playables.PlayableAsset>, IESAssetConfigDataInitializer<ESAssetReferPlayableAssetConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferPlayableAssetConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferPlayableAssetConfigKey>.InitializeFromRecord(ESAssetReferPlayableAssetConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
