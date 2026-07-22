using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferTerrainDataConfigData : ESAssetReferConfigDataBase<UnityEngine.TerrainData>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferTerrainDataConfigKey key;
    }
}
