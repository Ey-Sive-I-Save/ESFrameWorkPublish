namespace ES
{
    // ============================================================================
    // 文件：StateBase.cs
    // 作用：StateBase 的最小壳文件，仅用于保留类型入口；具体实现拆分在多个 partial 文件中。
    //
    // Public：本文件仅声明 public partial class StateBase（无额外 public 成员）。
    // Private/Internal：无。
    //
    // 实现分布：
    // - StateBase.Core.cs
    //   - 生命周期入口与基础状态：OnStateEnter/OnStateUpdate/OnStateExit、activationTime、baseStatus。
    //   - SharedData 缓存：basicConfig/animConfig/calculator 等缓存字段刷新与 gating（是否有动画、是否启用IK/MatchTarget等）。
    //   - ResolvedConfig：运行时覆盖/Phase覆盖后的配置汇总（Ensure/Refresh）。
    //   - 运行时替换动画计算器：SetAnimationCalculator（不改 sharedData 的前提下切换 calculator，必要时销毁重建 runtime）。
    //
    // - StateBase.Pooling.cs
    //   - 对象池/回收：状态实例的重置、回收到池、池化生命周期约束。
    //   - 与 Playable/Runtime 的释放协作：通常会在回收前确保 DestroyPlayable 等清理流程已执行。
    //
    // - StateBase.Animation.cs
    //   - PlayableGraph 生命周期：CreatePlayable/DestroyPlayable。
    //   - 外部管线权重：PlayableWeight、SetPlayableWeight、层级 Mixer 槽位绑定/解绑、权重写入。
    //   - 每帧动画运行时刷新：UpdateAnimationRuntime/ImmediateUpdateAnimationRuntime（权重、IK曲线/MatchTarget进度等）。
    //   - 自动退出判定：ShouldAutoExit、UntilAnimationEnd 的完成检测（含 sequenceCompleted / 标准时长）。
    //
    // - StateBase.Progress.cs
    //   - 进度/循环/阶段：hasEnterTime、normalizedProgress、totalProgress、loopCount。
    //   - 按标准时长推进进度、触发循环事件、自动阶段评估、动画事件触发（若接入配置）。
    //
    // - StateBase.IK.cs
    //   - IK 运行时开关与目标权重：Enable/Disable、目标权重设置、平滑参数、进入/退出时配置应用。
    //   - 与动画运行时的耦合点：由 Animation.cs 的每帧刷新驱动具体权重更新。
    //
    // - StateBase.MatchTarget.cs
    //   - MatchTarget 生命周期：Start/Cancel/Complete、进度范围与完成回调。
    //   - 与动画运行时的耦合点：由 Animation.cs 的每帧刷新/进度检查触发完成。
    //
    // - StateBase.Debug.cs
    //   - 仅调试：日志、断言、可视化/诊断信息（不应影响 Release 热路径）。
    // ============================================================================
    public partial class StateBase
    {
        
    }
}
 

