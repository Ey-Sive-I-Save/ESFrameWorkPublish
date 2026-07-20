using UnityEngine;

namespace ES
{
    public enum StateAnimationSource
    {
        [InspectorName("无")]
        None = 0,

        [InspectorName("状态配置")]
        StateConfig = 1,

        [InspectorName("技能时间线")]
        SkillTimeline = 2,

        [InspectorName("外部系统")]
        External = 3
    }
}
