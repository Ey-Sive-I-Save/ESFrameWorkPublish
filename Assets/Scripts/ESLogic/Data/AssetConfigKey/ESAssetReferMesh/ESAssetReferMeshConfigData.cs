using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferMeshConfigData : ESAssetReferConfigDataBase<UnityEngine.Mesh>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferMeshConfigKey key;
    }
}
