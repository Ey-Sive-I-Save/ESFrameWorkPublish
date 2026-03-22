using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        [TabGroup("DriverLayout", "公共部分", Order = 0)]
        [PropertyOrder(10)]
        [BoxGroup("DriverLayout/公共部分/全局设置盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/全局设置盒/全局设置", BoldTitle = true)]
        [LabelText("自动识别骨骼引用")]
        [Tooltip("BipedIK.references 缺失时自动识别骨骼引用（Humanoid 骨架）。在 Bind 阶段执行。")]
        [SerializeField] private bool autoDetectReferencesIfMissing = true;

        [PropertyOrder(11)]
        [BoxGroup("DriverLayout/公共部分/全局设置盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/全局设置盒/全局设置")]
        [LabelText("输出缺失组件提示")]
        [Tooltip("Bind 时对未挂载的可选功能组件输出 Info 级别日志提示。")]
        [SerializeField] private bool logMissingComponentHints = false;

        [PropertyOrder(12)]
        [BoxGroup("DriverLayout/公共部分/全局设置盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/全局设置盒/全局设置")]
        [LabelText("有权重无 IK 时警告")]
        [Tooltip("stateGeneralFinalIKDriverPose 有权重但 BipedIK 未就绪时，每隔 2 秒输出一次节流 Warning。")] 
        [SerializeField] private bool warnWhenPoseHasWeightButNoIK = true;

        [PropertyOrder(0)]
        [FoldoutGroup("【四肢IK】（BipedIK）", Expanded = true)]
        [LabelText("BipedIK  |  四肢 IK + LookAt 兜底")]
        [Tooltip("BipedIK：驱动左/右手脚四肢位置+旋转，含内置 LookAt 兜底。ES IK 主驱动，Driver 手动调用 UpdateBipedIK()。\n禁用后彻底跳过，性能零损失。")]
        [InlineButton("QuickAddComp_BipedIK", "添加组件")]
        [SerializeField] public bool enableBipedIK = true;

        [PropertyOrder(0)]
        [FoldoutGroup("【接地IK】（GrounderBipedIK）")]
        [LabelText("GrounderBipedIK  |  地形脚步接地")]
        [Tooltip("GrounderBipedIK：地形自适应脚步接地（需同时启用 BipedIK）。通过 BipedIK 求解器事件委托驱动。")]
        [EnableIf("enableBipedIK")]
        [InlineButton("QuickAddComp_GrounderBipedIK", "添加组件")]
        [SerializeField] internal bool enableGrounderBipedIK = true;

        [PropertyOrder(0)]
        [FoldoutGroup("【注视IK】（LookAtIK）")]
        [LabelText("LookAtIK  |  独立多骨骼注视")]
        [Tooltip("LookAtIK：头/颈/脊椎分层注视，比 BipedIK 内置 LookAt 更平滑。存在时自动覆盖兜底；不存在则自动降级。\n禁用后不初始化，性能零损失。")]
        [InlineButton("QuickAddComp_LookAtIK", "添加组件")]
        [SerializeField] internal bool enableLookAtIK = true;

        [PropertyOrder(0)]
        [FoldoutGroup("【瞄准IK】（AimIK）")]
        [LabelText("AimIK  |  骨链瞄准")]
        [Tooltip("AimIK：将骨链末端对准目标点（武器持握/身体对准）。通过 HandleAim 系列接口外部驱动，权重 0 时不影响动画。骨链只从总面板统一骨骼绑定派生。")]
        [InlineButton("QuickAddComp_AimIK", "添加组件")]
        [SerializeField] internal bool enableAimIK = true;

        [PropertyOrder(0)]
        [FoldoutGroup("【全身IK】（FullBodyBipedIK）")]
        [LabelText("FullBodyBipedIK  |  全身 IK（HitReaction/Recoil 前提）")]
        [Tooltip("FullBodyBipedIK：全身 IK 系统，HitReaction / Recoil 的前提依赖。禁用后两者均不可用。")]
        [InlineButton("QuickAddComp_FullBodyBipedIK", "添加组件")]
        [SerializeField] internal bool enableFullBodyBipedIK = true;

        [PropertyOrder(0)]
        [FoldoutGroup("【受击反馈】（HitReaction）")]
        [LabelText("HitReaction  |  受击程序动画")]
        [Tooltip("HitReaction：受击程序动画（需 FullBodyBipedIK）。禁用后 TriggerHitReaction() 调用无效。")]
        [EnableIf("enableFullBodyBipedIK")]
        [InlineButton("QuickAddComp_HitReaction", "添加组件")]
        [SerializeField] internal bool enableHitReaction = true;

        [PropertyOrder(0)]
        [FoldoutGroup("【后坐力】（Recoil）")]
        [LabelText("Recoil  |  后坐力程序动画")]
        [Tooltip("Recoil：武器后坐力程序动画（需 FullBodyBipedIK）。禁用后 TriggerRecoil() 调用无效。")]
        [EnableIf("enableFullBodyBipedIK")]
        [InlineButton("QuickAddComp_Recoil", "添加组件")]
        [SerializeField] internal bool enableRecoil = true;

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(50)]
        [BoxGroup("DriverLayout/公共部分/实时权重测试盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试", BoldTitle = true)]
        [InfoBox("运行时打开后，Driver 会直接用当前测试滑杆和测试目标覆盖状态机输出，便于不进入 FinalIK 面板就验证权重效果。四肢测试使用当前的 IK 目标点 Transform。", InfoMessageType.Warning)]
        [LabelText("启用实时权重测试")]
        [SerializeField] private bool enableRealtimeWeightTest = false;

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(51)]
        [BoxGroup("DriverLayout/公共部分/实时权重测试盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试")]
        [HorizontalGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试/四肢", LabelWidth = 70)]
        [LabelText("左手")]
        [Range(0f, 1f)]
        [ShowIf("enableRealtimeWeightTest")]
        [SerializeField] private float realtimeLeftHandWeight = 0f;

        [PropertyOrder(52)]
        [BoxGroup("DriverLayout/公共部分/实时权重测试盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试")]
        [HorizontalGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试/四肢", LabelWidth = 70)]
        [LabelText("右手")]
        [Range(0f, 1f)]
        [ShowIf("enableRealtimeWeightTest")]
        [SerializeField] private float realtimeRightHandWeight = 0f;

        [PropertyOrder(53)]
        [BoxGroup("DriverLayout/公共部分/实时权重测试盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试")]
        [HorizontalGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试/四肢", LabelWidth = 70)]
        [LabelText("左脚")]
        [Range(0f, 1f)]
        [ShowIf("enableRealtimeWeightTest")]
        [SerializeField] private float realtimeLeftFootWeight = 0f;

        [PropertyOrder(54)]
        [BoxGroup("DriverLayout/公共部分/实时权重测试盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试")]
        [HorizontalGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试/四肢", LabelWidth = 70)]
        [LabelText("右脚")]
        [Range(0f, 1f)]
        [ShowIf("enableRealtimeWeightTest")]
        [SerializeField] private float realtimeRightFootWeight = 0f;

        [PropertyOrder(55)]
        [BoxGroup("DriverLayout/公共部分/实时权重测试盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试")]
        [HorizontalGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试/注视瞄准", LabelWidth = 70)]
        [LabelText("注视权重")]
        [Range(0f, 1f)]
        [ShowIf("enableRealtimeWeightTest")]
        [SerializeField] private float realtimeLookAtWeight = 0f;

        [PropertyOrder(56)]
        [BoxGroup("DriverLayout/公共部分/实时权重测试盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试")]
        [HorizontalGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试/注视瞄准", LabelWidth = 70)]
        [LabelText("注视目标")]
        [ShowIf("enableRealtimeWeightTest")]
        [SerializeField] private Transform realtimeLookAtTarget;

        [PropertyOrder(57)]
        [BoxGroup("DriverLayout/公共部分/实时权重测试盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试")]
        [HorizontalGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试/注视瞄准", LabelWidth = 70)]
        [LabelText("瞄准权重")]
        [Range(0f, 1f)]
        [ShowIf("enableRealtimeWeightTest")]
        [SerializeField] private float realtimeAimWeight = 0f;

        [PropertyOrder(58)]
        [BoxGroup("DriverLayout/公共部分/实时权重测试盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试")]
        [HorizontalGroup("DriverLayout/公共部分/实时权重测试盒/实时权重测试/注视瞄准", LabelWidth = 70)]
        [LabelText("瞄准目标")]
        [ShowIf("enableRealtimeWeightTest")]
        [SerializeField] private Transform realtimeAimTarget;
    }
}