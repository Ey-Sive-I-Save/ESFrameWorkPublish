using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 同层状态在 Mixer 中的权重偏置。
    /// 仅影响同层动画混合分配与最终排序，不参与合并/打断规则。
    /// </summary>
    public enum StateMixerBias : byte
    {
        [InspectorName("背景")]
        Background = 0,

        [InspectorName("偏低")]
        Low = 1,

        [InspectorName("标准")]
        Normal = 2,

        [InspectorName("偏高")]
        High = 3,

        [InspectorName("关键")]
        Critical = 4,
    }
}