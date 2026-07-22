using System;
using UnityEngine;

namespace ES
{
    public enum ESMonsterEnumKey : ushort
    {
        None = 0,
        Custom = 1
    }

    public enum ESNpcEnumKey : ushort
    {
        None = 0,
        Custom = 1
    }

    [Serializable]
    public sealed class ESMonsterConfigKey : ESGameCoreConfigKey<ESMonsterEnumKey> { }

    [Serializable]
    public sealed class ESNpcConfigKey : ESGameCoreConfigKey<ESNpcEnumKey> { }

    [Serializable]
    public sealed class ESMonsterRuntimeData
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ActorDataInfo soSource;
        public EntityMotionSharedData sharedData;
        public EntityMotionVariableData defaultVariableData;
        public GameObject prefab;
        public UnityEngine.Object extraAsset;
        public ESGameCoreConfigJsonSource jsonSource;
    }

    [Serializable]
    public sealed class ESNpcRuntimeData
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ActorDataInfo soSource;
        public EntityMotionSharedData sharedData;
        public EntityMotionVariableData defaultVariableData;
        public GameObject prefab;
        public UnityEngine.Object extraAsset;
        public ESGameCoreConfigJsonSource jsonSource;
    }

    [Serializable]
    public sealed class ESMonsterConfigKeyJsonRecord : ESGameCoreConfigKeyJsonRecord { }

    [Serializable]
    public sealed class ESNpcConfigKeyJsonRecord : ESGameCoreConfigKeyJsonRecord { }
}
