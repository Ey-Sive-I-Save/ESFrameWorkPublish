using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferSceneConfigData : ESAssetReferConfigDataBase<UnityEngine.Object>, IESAssetConfigDataInitializer<ESAssetReferSceneConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferSceneConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferSceneConfigKey>.InitializeFromRecord(ESAssetReferSceneConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
