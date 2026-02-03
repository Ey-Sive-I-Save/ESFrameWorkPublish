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
        SpeedX = 1,              // X轴速度（横向）
        SpeedY = 2,              // Y轴速度（垂直，跳跃/下落）
        SpeedZ = 3,              // Z轴速度（前后）
        AimYaw = 4,              // 瞄准偏航角
        AimPitch = 5,            // 瞄准俯仰角
        Speed = 6,               // 总速度（magnitude）
        IsGrounded = 7,          // 是否接地（0/1）
        
        // ===== 运动阈值 (8-10) =====
        WalkSpeedThreshold = 8,  // 走路速度阈值（默认 2.0）
        RunSpeedThreshold = 9,   // 跑步速度阈值（默认 5.0）
        SprintSpeedThreshold = 10, // 冲刺速度阈值（默认 8.0）
        
        // ===== 运动状态标记 (11-15) =====
        IsWalking = 11,          // 是否在走路（0/1）
        IsRunning = 12,          // 是否在跑步（0/1）
        IsSprinting = 13,        // 是否在冲刺（0/1）
        IsCrouching = 14,        // 是否蹲伏（0/1）
        IsSliding = 15,          // 是否滑行（0/1）
    }

}
