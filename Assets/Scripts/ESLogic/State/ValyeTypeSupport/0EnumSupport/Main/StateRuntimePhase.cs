using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 状态运行时阶段
    /// </summary>
    public enum StateRuntimePhase
    {
        [InspectorName("前置阶段")]
        Pre,

        [InspectorName("主阶段")]
        Main,

        [InspectorName("等待阶段（回收/衔接）")]
        Wait,

        [InspectorName("释放阶段（不占据但动画可能还未完）")]
        Released
    }
}
