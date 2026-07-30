using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferAvatarConfigData : ESAssetReferConfigDataBase<UnityEngine.Avatar>, IESAssetConfigDataInitializer<ESAssetReferAvatarConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferAvatarConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferAvatarConfigKey>.InitializeFromRecord(ESAssetReferAvatarConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
