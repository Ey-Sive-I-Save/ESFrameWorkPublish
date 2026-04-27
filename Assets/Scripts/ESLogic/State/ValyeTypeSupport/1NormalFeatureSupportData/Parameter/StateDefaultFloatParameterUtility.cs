namespace ES
{
    /// <summary>
    /// 默认浮点参数的共享映射工具，避免多处硬编码重复维护。
    /// </summary>
    public static class StateDefaultFloatParameterUtility
    {
        private static readonly string[] Names =
        {
            null,
            "SpeedX",
            "SpeedY",
            "SpeedZ",
            "AimYaw",
            "AimPitch",
            "Speed",
            "IsGrounded",
            "WalkSpeedThreshold",
            "RunSpeedThreshold",
            "SprintSpeedThreshold",
            "IsWalking",
            "IsRunning",
            "IsSprinting",
            "IsCrouching",
            "IsSliding",
            "AvgSpeedX",
            "AvgSpeedZ",
            "ClimbHorizontal",
            "ClimbVertical"
        };

        public static bool TryGetName(StateDefaultFloatParameter parameter, out string name)
        {
            int index = (int)parameter;
            if ((uint)index < (uint)Names.Length)
            {
                name = Names[index];
                return !string.IsNullOrEmpty(name);
            }

            name = null;
            return false;
        }
    }
}