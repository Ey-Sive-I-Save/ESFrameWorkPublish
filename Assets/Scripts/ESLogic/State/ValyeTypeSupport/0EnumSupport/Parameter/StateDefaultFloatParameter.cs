using System;

namespace ES
{
    /// <summary>
    /// 状态机默认参数枚举 - 核心参数定义
    /// 对应StateContext的显式字段，零开销直接访问
    /// 
    /// 参数分类：
    /// 1-7: 核心运动参数
    /// 8-15: 运动阈值和状态标记
    /// </summary>
    public enum StateDefaultFloatParameter
    {
        None = 0,
        
        // ===== 核心运动参数 (1-7) =====
        /// <summary>当前帧局部空间横向速度（角色右方向，由输入驱动）</summary>
        SpeedX = 1,
        /// <summary>当前帧垂直速度（跳跃/下落）</summary>
        SpeedY = 2,
        /// <summary>当前帧局部空间前后速度（角色前方向，由输入驱动）</summary>
        SpeedZ = 3,
        /// <summary>瞄准偏航角（水平旋转）</summary>
        AimYaw = 4,
        /// <summary>瞄准俯仰角（垂直旋转）</summary>
        AimPitch = 5,
        /// <summary>水平综合速度 = sqrt(SpeedX² + SpeedZ²)</summary>
        Speed = 6,
        /// <summary>是否接地（0/1 float）</summary>
        IsGrounded = 7,
        
        // ===== 运动阈值 (8-10) =====
        /// <summary>走路速度上限（小于此值=走路动画区间）</summary>
        WalkSpeedThreshold = 8,
        /// <summary>跑步速度上限（小于此值=跑步动画区间）</summary>
        RunSpeedThreshold = 9,
        /// <summary>冲刺速度上限</summary>
        SprintSpeedThreshold = 10,
        
        // ===== 运动状态标记 (11-15) =====
        IsWalking = 11,
        IsRunning = 12,
        IsSprinting = 13,
        IsCrouching = 14,
        IsSliding = 15,
        
        // ===== 历史平均速度 (16-17) =====
        /// <summary>前0.5秒局部空间横向平均速度（用于急停/BlendTree方向保持）</summary>
        AvgSpeedX = 16,
        /// <summary>前0.5秒局部空间前后平均速度（用于急停/BlendTree方向保持）</summary>
        AvgSpeedZ = 17,
        
        // ===== 攀爬参数 (18-19) =====
        /// <summary>攀爬时沿墙面的水平输入（-1=左, 0=静止, 1=右）</summary>
        ClimbHorizontal = 18,
        /// <summary>攀爬时沿墙面的垂直输入（-1=下, 0=静止, 1=上）</summary>
        ClimbVertical = 19,
    }

}
