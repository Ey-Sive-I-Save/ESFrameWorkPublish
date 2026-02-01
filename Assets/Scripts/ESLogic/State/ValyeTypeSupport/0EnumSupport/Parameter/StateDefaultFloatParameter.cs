using System;

namespace ES
{
    /// <summary>
    /// 状态机默认参数枚举 - 核心参数定义
    /// 对应StateContext的显式字段，零开销直接访问
    /// </summary>
    public enum StateDefaultFloatParameter
    {
        None = 0,
        SpeedX = 1,
        SpeedY = 2,
        SpeedZ = 3,
        AimYaw = 4,
        AimPitch = 5,
        Speed = 6,
        IsGrounded = 7,
    }

}
