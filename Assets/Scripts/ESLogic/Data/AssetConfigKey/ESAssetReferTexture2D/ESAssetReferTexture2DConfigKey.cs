using System;
using UnityEngine;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/AssetConfigKey/ESAssetReferTexture2D/ESAssetReferTexture2DConfigKey.cs")]
    public enum ESAssetReferTexture2DEnumKey : ushort { [InspectorName("未配置")] None = 0, [InspectorName("自定义")] Custom = 1 }

    [Serializable]
    public sealed class ESAssetReferTexture2DConfigKey : ESAssetConfigKey<ESAssetReferTexture2DEnumKey> { }
}
