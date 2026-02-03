using System;
using UnityEngine;

namespace ES
{
    [Serializable]
    public struct StateParameter
    {
        public StateDefaultFloatParameter EnumValue;
        public string StringValue;

        public StateParameter(StateDefaultFloatParameter enumValue)
        {
            EnumValue = enumValue;
            StringValue = null;
        }

        public StateParameter(string stringValue)
        {
            EnumValue = StateDefaultFloatParameter.None;
            StringValue = stringValue;
        }

        public string GetStringKey
        {
            get
            {
                if (EnumValue != StateDefaultFloatParameter.None)
                {
                    // 硬编码映射，零GC
                    switch (EnumValue)
                    {
                        // 核心运动参数 (1-7)
                        case StateDefaultFloatParameter.SpeedX: return "SpeedX";
                        case StateDefaultFloatParameter.SpeedY: return "SpeedY";
                        case StateDefaultFloatParameter.SpeedZ: return "SpeedZ";
                        case StateDefaultFloatParameter.AimYaw: return "AimYaw";
                        case StateDefaultFloatParameter.AimPitch: return "AimPitch";
                        case StateDefaultFloatParameter.Speed: return "Speed";
                        case StateDefaultFloatParameter.IsGrounded: return "IsGrounded";
                        
                        // 运动阈值 (8-10)
                        case StateDefaultFloatParameter.WalkSpeedThreshold: return "WalkSpeedThreshold";
                        case StateDefaultFloatParameter.RunSpeedThreshold: return "RunSpeedThreshold";
                        case StateDefaultFloatParameter.SprintSpeedThreshold: return "SprintSpeedThreshold";
                        
                        // 运动状态标记 (11-15)
                        case StateDefaultFloatParameter.IsWalking: return "IsWalking";
                        case StateDefaultFloatParameter.IsRunning: return "IsRunning";
                        case StateDefaultFloatParameter.IsSprinting: return "IsSprinting";
                        case StateDefaultFloatParameter.IsCrouching: return "IsCrouching";
                        case StateDefaultFloatParameter.IsSliding: return "IsSliding";
                        
                        default: return "None";
                    }
                }
                return StringValue ?? string.Empty;
            }
        }

        public static implicit operator StateParameter(StateDefaultFloatParameter e) => new StateParameter(e);
        public static implicit operator StateParameter(string s) => new StateParameter(s);
    }
}
