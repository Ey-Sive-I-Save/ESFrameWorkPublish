using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferPrefab/ESAssetReferPrefabConfigKey.cs")]
    public enum ESAssetReferPrefabEnumKey : ushort { 
        
        None = 0, Custom = 1
        
         }

    [Serializable]
    public sealed class ESAssetReferPrefabConfigKey : ESAssetConfigKey<ESAssetReferPrefabEnumKey> { }
}
