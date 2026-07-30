using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferTimelineAssetConfigData : ESAssetReferConfigDataBase<UnityEngine.Object>, IESAssetConfigDataInitializer<ESAssetReferTimelineAssetConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferTimelineAssetConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferTimelineAssetConfigKey>.InitializeFromRecord(ESAssetReferTimelineAssetConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
