using System;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ESAssetReferScriptableObjectConfigData : ESAssetReferConfigDataBase<ScriptableObject>
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ESAssetReferScriptableObjectConfigKey key;
    }
}
