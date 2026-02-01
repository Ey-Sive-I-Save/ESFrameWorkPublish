using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 状态持续时间模式
    /// </summary>
    public enum StateDurationMode
    {
        [InspectorName("无限持续")]
        Infinite,

        [InspectorName("按动画结束")]
        UntilAnimationEnd,

        [InspectorName("定时")]
        Timed
    }
}
