using System;
using UnityEngine;

namespace ES
{
    public enum ESShotEnumKey : ushort
    {
        None = 0,
        Custom = 1
    }

    [Serializable]
    public sealed class ESShotConfigKey : ESGameCoreConfigKey<ESShotEnumKey> { }

    [Serializable]
    public sealed class ESShotRuntimeData
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ItemDataInfo soSource;
        public ItemShotSharedData sharedData;
        public ItemShotVariableData defaultVariableData;
        public GameObject prefab;
        public UnityEngine.Object extraAsset;
        public ESGameCoreConfigJsonSource jsonSource;
    }

    [Serializable]
    public sealed class ESShotConfigKeyJsonRecord : ESGameCoreConfigKeyJsonRecord { }
}
