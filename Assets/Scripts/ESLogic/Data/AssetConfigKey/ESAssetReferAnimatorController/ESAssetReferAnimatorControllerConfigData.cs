using System;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferAnimatorControllerConfigData : ESAssetReferConfigDataBase<UnityEngine.RuntimeAnimatorController>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferAnimatorControllerConfigKey key;
    }
}
