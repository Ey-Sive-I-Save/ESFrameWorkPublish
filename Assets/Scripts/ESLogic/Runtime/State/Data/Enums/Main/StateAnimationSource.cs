using UnityEngine;

namespace ES
{
    public enum StateAnimationSource
    {
        [InspectorName("无动画")]
        None = 0,

        [InspectorName("状态配置动画")]
        StateConfig = 1,

        [InspectorName("技能时间线")]
        SkillTimeline = 2,

        [InspectorName("外部系统表现")]
        External = 3
    }
}
