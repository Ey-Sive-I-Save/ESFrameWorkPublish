using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferAnimatorController/ESAssetReferAnimatorControllerConfigKey.cs")]
    public enum ESAssetReferAnimatorControllerEnumKey : ushort { [InspectorName("未配置")] None = 0, [InspectorName("自定义")] Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferAnimatorControllerConfigKey : ESAssetConfigKey<ESAssetReferAnimatorControllerEnumKey> { }
}
