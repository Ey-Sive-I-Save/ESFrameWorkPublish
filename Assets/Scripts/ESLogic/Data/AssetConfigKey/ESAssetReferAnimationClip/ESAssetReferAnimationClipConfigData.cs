using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferAnimationClipConfigData : ESAssetReferConfigDataBase<UnityEngine.AnimationClip>, IESAssetConfigDataInitializer<ESAssetReferAnimationClipConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferAnimationClipConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferAnimationClipConfigKey>.InitializeFromRecord(ESAssetReferAnimationClipConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
