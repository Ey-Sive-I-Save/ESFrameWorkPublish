using System;
using UnityEngine;

namespace ES
{
    public enum ESWeaponEnumKey : ushort
    {
        None = 0,
        Custom = 1
    }

    [Serializable]
    public sealed class ESWeaponConfigKey : ESGameCoreConfigKey<ESWeaponEnumKey> { }

    [Serializable]
    public sealed class ESWeaponRuntimeData
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public ItemDataInfo soSource;
        public ItemWeaponSharedData sharedData;
        public ItemWeaponVariableData defaultVariableData;
        public GameObject prefab;
        public UnityEngine.Object extraAsset;
        public ESGameCoreConfigJsonSource jsonSource;
    }

    [Serializable]
    public sealed class ESWeaponConfigKeyJsonRecord : ESGameCoreConfigKeyJsonRecord { }
}
