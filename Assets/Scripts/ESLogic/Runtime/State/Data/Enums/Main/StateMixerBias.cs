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

    /// <summary>
    /// 独占模式：激活后同层其他普通状态权重立即归零（淡出中的状态除外），
    /// 独占所有权重直到该状态退出或被更高优先级的独占状态打断。
    /// </summary>
    [InspectorName("独占")]
    Dominant = 5
}
}