using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferVideoClipConfigData : ESAssetReferConfigDataBase<UnityEngine.Video.VideoClip>, IESAssetConfigDataInitializer<ESAssetReferVideoClipConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferVideoClipConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferVideoClipConfigKey>.InitializeFromRecord(ESAssetReferVideoClipConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
