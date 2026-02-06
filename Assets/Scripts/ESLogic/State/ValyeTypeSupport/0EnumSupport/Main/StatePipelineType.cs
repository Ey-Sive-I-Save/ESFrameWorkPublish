using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 定义动画系统支持的流水线枚举类型。
    /// 该系统采用多流水线混合架构，不同的流水线负责互斥或叠加的动画行为，最终通过Playable Graph进行混合输出。
    /// </summary>
    public enum StatePipelineType
    {
        [InspectorName("基本线")]
        Basic = 0,    // 基本线 - 跑跳下蹲等基础动作
        [InspectorName("主线")]
        Main = 1,     // 主线 - 技能、表情、交互等互相排斥的动作
        [InspectorName("Buff线")]
        Buff = 2,     // Buff线 - 特效、状态效果
        Count,        // 哨兵：用于表示流水线数量（数组长度/Mixer输入数）



        NotClear=99,  // 不清除标记 - 用于某些特殊情况，表示不清除当前流水线的动画状态
    }
}
