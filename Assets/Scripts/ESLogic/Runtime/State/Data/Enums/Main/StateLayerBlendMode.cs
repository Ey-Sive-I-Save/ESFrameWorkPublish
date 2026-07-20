using UnityEngine;

namespace ES
{
    /// <summary>
    /// 层级混合模式 - 对标Animator Layer的混合方式。
    /// </summary>
    public enum StateLayerBlendMode
    {
        [InspectorName("覆盖")]
        Override = 0,

        [InspectorName("叠加")]
        Additive = 1
    }
}
