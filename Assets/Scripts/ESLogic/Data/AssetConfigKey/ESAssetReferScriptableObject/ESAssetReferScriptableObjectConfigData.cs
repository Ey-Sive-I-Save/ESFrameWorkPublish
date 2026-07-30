using System;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferScriptableObjectConfigData : ESAssetReferConfigDataBase<ScriptableObject>, IESAssetConfigDataInitializer<ESAssetReferScriptableObjectConfigKey>
    {
        [NonSerialized] public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferScriptableObjectConfigKey key;

        void IESAssetConfigDataInitializer<ESAssetReferScriptableObjectConfigKey>.InitializeFromRecord(ESAssetReferScriptableObjectConfigKey configKey, in ESAssetConfigRecord record)
            => ESAssetConfigDataInitialization.Initialize(this, configKey, in record, ref keyName, ref displayName, ref sourcePackage, ref version, ref key);
    }
}
