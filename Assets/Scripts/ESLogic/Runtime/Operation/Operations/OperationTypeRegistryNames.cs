namespace ES
{
    /// <summary>
    /// Operation 在 Odin TypeRegistry 中的标准分组路径。
    /// 新增 Operation 不要随手写路径，统一从这里取，避免菜单分类失控。
    /// </summary>
    public static class OperationTypeRegistryNames
    {
        public const string Root = "";

        public const string Common = "00 常用通用";

        public const string ReleaseFlow = "01 状态机";
        public const string ReleaseCost = "02 技能释放";
        public const string ReleaseCancel = "02 技能释放";
        public const string ReleaseCount = "02 技能释放";

        public const string Targeting = "03 目标选择";
        public const string TargetingUser = "03 目标选择";
        public const string TargetingMain = "03 目标选择";
        public const string TargetingList = "03 目标选择";
        public const string TargetingFilter = "03 目标选择";
        public const string TargetingWriteBack = "03 目标选择";

        public const string ConditionBranch = "04 条件分支";
        public const string Condition = "04 条件分支";
        public const string Branch = "04 条件分支";

        public const string CombatHit = "05 战斗结算";
        public const string HitDetection = "05 战斗结算";
        public const string Damage = "05 战斗结算";
        public const string Heal = "05 战斗结算";
        public const string Shield = "05 战斗结算";
        public const string HitReaction = "05 战斗结算";

        public const string AttributeResource = "06 上下文数值";
        public const string Attribute = "07 属性资源";
        public const string Resource = "07 属性资源";
        public const string Value = "06 上下文数值";

        public const string BuffState = "08 状态标签";
        public const string Buff = "08 状态标签";
        public const string StateTag = "08 状态标签";
        public const string ControlState = "08 状态标签";

        public const string MovementPhysics = "09 位移朝向";
        public const string Movement = "09 位移朝向";
        public const string Rotation = "09 位移朝向";
        public const string Physics = "10 物理";
        public const string RootMotion = "09 位移朝向";

        public const string AnimationAction = "11 动画控制";
        public const string Animator = "11 动画控制";
        public const string ActionPhase = "11 动画控制";
        public const string Timeline = "12 时间轴";

        public const string GameObjectVfx = "13 对象与特效";
        public const string GameObject = "13 对象与特效";
        public const string Transform = "13 对象与特效";
        public const string Vfx = "13 对象与特效";

        public const string Audio = "14 音频";
        public const string AudioOneShot = "14 音频";
        public const string AudioLoop = "14 音频";

        public const string CameraFeedback = "15 相机反馈";
        public const string Camera = "15 相机反馈";
        public const string CameraShake = "15 相机反馈";
        public const string CameraFocus = "15 相机反馈";

        public const string DurationChanneling = "16 持续引导";
        public const string Duration = "16 持续引导";
        public const string Channeling = "16 持续引导";
        public const string Aura = "16 持续引导";
        public const string Buffer = "16 持续引导";

        public const string EventCallback = "17 事件回调";
        public const string Event = "17 事件回调";
        public const string Callback = "17 事件回调";
        public const string Message = "17 事件回调";

        public const string Debug = "90 调试工具";
        public const string DebugExamples = "91 示例";
        public const string DebugAssert = "90 调试工具";
        public const string DebugGizmos = "90 调试工具";

        public const string LogRuntimeTargetName = "打印运行目标包";
    }
}
