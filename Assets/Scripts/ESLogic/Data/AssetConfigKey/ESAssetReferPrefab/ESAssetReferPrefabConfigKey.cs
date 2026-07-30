using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferPrefab/ESAssetReferPrefabConfigKey.cs")]
    public enum ESAssetReferPrefabEnumKey : ushort { 
        
        [InspectorName("未配置")] None = 0,
        [InspectorName("自定义")] Custom = 1
        
         }

    [Serializable]
    public sealed class ESAssetReferPrefabConfigKey : ESAssetConfigKey<ESAssetReferPrefabEnumKey> { }
}
