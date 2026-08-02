using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferRaw/ESAssetReferRawConfigKey.cs")]
    public enum ESAssetReferRawEnumKey : ushort
    {
        [InspectorName("未配置")] None = 0,
        [InspectorName("自定义")] Custom = 1
    }

    [Serializable]
    public sealed class ESAssetReferRawConfigKey : ESAssetConfigKey<ESAssetReferRawEnumKey> { }
}
