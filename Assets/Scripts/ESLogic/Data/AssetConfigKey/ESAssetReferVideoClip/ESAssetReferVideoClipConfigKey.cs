using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferVideoClip/ESAssetReferVideoClipConfigKey.cs")]
    public enum ESAssetReferVideoClipEnumKey : ushort { [InspectorName("未配置")] None = 0, [InspectorName("自定义")] Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferVideoClipConfigKey : ESAssetConfigKey<ESAssetReferVideoClipEnumKey> { }
}
