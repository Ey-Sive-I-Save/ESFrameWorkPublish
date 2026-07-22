using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferMaterialConfigData : ESAssetReferConfigDataBase<UnityEngine.Material>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferMaterialConfigKey key;
    }
}
