using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Weapon/ESWeaponConfigKeyData.cs")]
    public enum ESWeaponEnumKey : ushort
    {
        [InspectorName("未配置")]
        None = 0,
        [InspectorName("自定义")]
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
    }
}
