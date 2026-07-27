using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Actor/ESActorConfigKeyData.cs")]
    public enum ESMonsterEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
        Custom = 1
    }

    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Actor/ESActorConfigKeyData.cs")]
    public enum ESNpcEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
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
        public ScriptableObject soSource;
        public EntityMotionSharedData sharedData;
        public EntityMotionVariableData defaultVariableData;
        public GameObject prefab;
        public UnityEngine.Object extraAsset;
    }

    [Serializable]
    public sealed class ESNpcRuntimeData
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ScriptableObject soSource;
        public EntityMotionSharedData sharedData;
        public EntityMotionVariableData defaultVariableData;
        public GameObject prefab;
        public UnityEngine.Object extraAsset;
    }
}
