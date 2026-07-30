using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferAnimatorControllerConfigData : ESAssetReferConfigDataBase<UnityEngine.RuntimeAnimatorController>, IESAssetConfigDataInitializer<ESAssetReferAnimatorControllerConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferAnimatorControllerConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferAnimatorControllerConfigKey>.InitializeFromRecord(ESAssetReferAnimatorControllerConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
