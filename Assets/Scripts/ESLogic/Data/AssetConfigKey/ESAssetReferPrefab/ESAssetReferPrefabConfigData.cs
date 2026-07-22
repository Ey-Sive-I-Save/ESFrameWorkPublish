using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferPrefabConfigData : ESAssetReferConfigDataBase<UnityEngine.GameObject>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferPrefabConfigKey key;
    }
}
