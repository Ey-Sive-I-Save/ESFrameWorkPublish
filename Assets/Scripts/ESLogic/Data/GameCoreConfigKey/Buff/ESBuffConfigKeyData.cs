using System;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ESBuffConfigKey : ESGameCoreConfigKey<ESBuffEnumKey> { }

    [Serializable]
    public sealed class ESBuffRuntimeData
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public BuffDefinitionDataInfo soSource;
        public BuffSharedData sharedData;
        public BuffVariableData defaultVariableData;
        public GameObject prefab;
        public UnityEngine.Object extraAsset;
        public ESGameCoreConfigJsonSource jsonSource;
    }

    [Serializable]
    public sealed class ESBuffConfigKeyJsonRecord : ESGameCoreConfigKeyJsonRecord { }
}
