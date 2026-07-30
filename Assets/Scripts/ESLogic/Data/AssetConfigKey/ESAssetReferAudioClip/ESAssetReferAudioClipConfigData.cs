using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferAudioClipConfigData : ESAssetReferConfigDataBase<UnityEngine.AudioClip>, IESAssetConfigDataInitializer<ESAssetReferAudioClipConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferAudioClipConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferAudioClipConfigKey>.InitializeFromRecord(ESAssetReferAudioClipConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
