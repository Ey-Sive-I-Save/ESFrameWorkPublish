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
        [InspectorName("默认运行")]
        Running,
        
        [InspectorName("返还阶段（可额外容纳其他动作）")]
        Returning,
        
        [InspectorName("释放阶段（不占据但动画可能还未完）")]
        Released
    }
}
