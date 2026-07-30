using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferScriptableObject/ESAssetReferScriptableObjectConfigKey.cs")]
    public enum ESAssetReferScriptableObjectEnumKey : ushort { [InspectorName("未配置")] None = 0, [InspectorName("自定义")] Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferScriptableObjectConfigKey : ESAssetConfigKey<ESAssetReferScriptableObjectEnumKey> { }
}
