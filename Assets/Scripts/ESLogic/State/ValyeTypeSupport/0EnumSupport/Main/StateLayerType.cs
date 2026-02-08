using UnityEngine;

namespace ES
{
    /// <summary>
    /// 动画层级类型 - 对标Animator Layer的组织方式。
    /// 每个层级可单独设置AvatarMask与混合模式。
    /// </summary>
    public enum StateLayerType
    {
        [InspectorName("基础层")]
        Base = 0,

        [InspectorName("主层")]
        Main = 1,

        [InspectorName("Buff层")]
        Buff = 2,

        [InspectorName("上半身层")]
        UpperBody = 3,

        [InspectorName("下半身层")]
        LowerBody = 4,

        Count,

        NotClear = 99
    }
}
