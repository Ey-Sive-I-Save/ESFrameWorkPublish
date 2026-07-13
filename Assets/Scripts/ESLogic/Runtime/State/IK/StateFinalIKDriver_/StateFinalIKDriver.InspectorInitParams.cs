using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        [PropertyOrder(13)]
        [BoxGroup("DriverLayout/公共部分/全局设置盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/全局设置盒/全局设置")]
        [LabelText("热插拔重试间隔 (秒)")]
        [Tooltip("运行时动态挂载 BipedIK 后的重绑轮询间隔。0 = 每帧重试（不推荐）。")]
        [SerializeField] private float rebindInterval = 0.5f;

        [PropertyOrder(40)]
        [BoxGroup("【瞄准IK】（AimIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/基础设置盒/基础设置")]
        [InfoBox("外部 API 驱动的 AimIK 若长时间未收到心跳则自动将权重衰减到 0，防止状态切换后 IK 卡住。\n超时阈值 = 0 时禁用心跳。", InfoMessageType.None)]
        [LabelText("超时阈值 (秒)")]
        [Tooltip("距上次 HandleAim / HandleAimTarget 调用超出此时长后开始衰减。0 = 禁用心跳。")]
        [Range(0f, 5f)]
        [ShowIf("enableAimIK")]
        [SerializeField] private float aimIKHeartbeatTimeout = 0.5f;

        [PropertyOrder(41)]
        [BoxGroup("【瞄准IK】（AimIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/基础设置盒/基础设置")]
        [LabelText("权重平滑时长 (秒)")]
        [Tooltip("AimIK 独立权重平滑时长。0 = 关闭平滑，直接生效。")]
        [Range(0f, 1f)]
        [ShowIf("enableAimIK")]
        [SerializeField] private float aimWeightSmoothTime = 0.12f;

        [PropertyOrder(42)]
        [BoxGroup("【瞄准IK】（AimIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/基础设置盒/基础设置")]
        [LabelText("LerpingRate 回归时长 (秒)")]
        [Tooltip("AimIK 独立临时 lerpingRate 回归时长。0 = 立即回归到 1。")]
        [Range(0f, 2f)]
        [ShowIf("enableAimIK")]
        [SerializeField] private float aimLerpingRateRecoverTime = 0.2f;

        [PropertyOrder(43)]
        [BoxGroup("【瞄准IK】（AimIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/基础设置盒/基础设置")]
        [LabelText("衰减持续时间 (秒)")]
        [Tooltip("心跳超时后，AimIK 会把目标权重设为 0，并按该时长换算出一个临时 lerpingRate 去逼近 0；临时 lerpingRate 随后会自动回归到 1。")]
        [Range(0.05f, 2f)]
        [ShowIf("enableAimIK")]
        [SerializeField] private float aimIKDecayDuration = 0.3f;

        [PropertyOrder(30)]
        [TitleGroup("【注视IK】（LookAtIK）/基础设置")]
        [InfoBox("Bind 完成后自动应用，也可在运行时点击下方按钮重新应用。", InfoMessageType.None)]
        [LabelText("权重平滑时长 (秒)")]
        [Tooltip("LookAtIK 独立权重平滑时长。0 = 关闭平滑，直接生效。")]
        [Range(0f, 1f)][ShowIf("enableLookAtIK")]
        [SerializeField] private float lookAtWeightSmoothTime = 0.12f;

        [PropertyOrder(31)]
        [TitleGroup("【注视IK】（LookAtIK）/基础设置")]
        [LabelText("LerpingRate 回归时长 (秒)")]
        [Tooltip("LookAtIK 独立临时 lerpingRate 回归时长。0 = 立即回归到 1。")]
        [Range(0f, 2f)][ShowIf("enableLookAtIK")]
        [SerializeField] private float lookAtLerpingRateRecoverTime = 0.2f;

        [PropertyOrder(32)]
        [TitleGroup("【注视IK】（LookAtIK）/基础设置")]
        [LabelText("整体触发权重")]
        [Tooltip("LookAtIK.solver.IKPositionWeight。就绪时将此权重写入求解器。")]
        [Range(0f, 1f)][ShowIf("enableLookAtIK")]
        [SerializeField] private float initLookAtWeight = 1f;

        [PropertyOrder(33)]
        [TitleGroup("【注视IK】（LookAtIK）/基础设置")]
        [LabelText("头部权重")][Tooltip("LookAtIK.solver.head.weight。")]
        [Range(0f, 1f)][ShowIf("enableLookAtIK")]
        [SerializeField] private float initLookAtHeadWeight = 1f;

        [PropertyOrder(34)]
        [TitleGroup("【注视IK】（LookAtIK）/基础设置")]
        [LabelText("眼睛权重")][Tooltip("LookAtIK.solver.head.eyesWeight。")]
        [Range(0f, 1f)][ShowIf("enableLookAtIK")]
        [SerializeField] private float initLookAtEyesWeight = 1f;

        [PropertyOrder(35)]
        [TitleGroup("【注视IK】（LookAtIK）/基础设置")]
        [LabelText("脊椎权重")][Tooltip("LookAtIK.solver.spine[] 每块骨骼统一设置的权重值。")]
        [Range(0f, 1f)][ShowIf("enableLookAtIK")]
        [SerializeField] private float initLookAtSpineWeight = 0.5f;

        [PropertyOrder(36)]
        [TitleGroup("【注视IK】（LookAtIK）/基础设置")]
        [LabelText("限制转动 (ClampWeight)")][Tooltip("LookAtIK.solver.clampWeight。0=不限，1=完全锁死。")]
        [Range(0f, 1f)][ShowIf("enableLookAtIK")]
        [SerializeField] private float initLookAtClampWeight = 0.5f;

        [PropertyOrder(42)]
        [TitleGroup("【瞄准IK】（AimIK）/基础设置")]
        [LabelText("允许初始就带权重")]
        [Tooltip("关闭时，AimIK 在 Bind/初始化时强制从 0 权重开始，只有外部真正调用 HandleAim 后才会拉起。打开时才使用下方的初始权重。")]
        [ShowIf("enableAimIK")]
        [SerializeField] private bool useInitAimWeightOnBind = false;

        [PropertyOrder(43)]
        [TitleGroup("【瞄准IK】（AimIK）/基础设置")]
        [LabelText("整体触发权重")][Tooltip("AimIK.solver.IKPositionWeight。默认建议保持 0，由外部模块在真正进入瞄准时拉起。")]
        [Range(0f, 1f)][ShowIf("@enableAimIK && useInitAimWeightOnBind")]
        [SerializeField] private float initAimWeight = 0f;

        [PropertyOrder(44)]
        [TitleGroup("【瞄准IK】（AimIK）/基础设置")]
        [LabelText("限制转动 (ClampWeight)")][Tooltip("AimIK.solver.clampWeight。限制骨链最大偏转角度。")]
        [Range(0f, 1f)][ShowIf("enableAimIK")]
        [SerializeField] private float initAimClampWeight = 0.5f;

        [PropertyOrder(45)]
        [TitleGroup("【瞄准IK】（AimIK）/基础设置")]
        [LabelText("瞄准轴 (Aim Axis)")]
        [Tooltip("AimIK.solver.axis：骨链末端骨骼指向目标的本地方向。常用：Forward=(0,0,1)、Right=(1,0,0)。")]
        [ShowIf("enableAimIK")]
        [SerializeField] private Vector3 initAimAxis = Vector3.forward;

        [PropertyOrder(30)]
        [TitleGroup("【接地IK】（GrounderBipedIK）/基础设置")]
        [LabelText("权重平滑时长 (秒)")]
        [Tooltip("Grounder 独立权重平滑时长。0 = 关闭平滑，直接生效。")]
        [Range(0f, 1f)][ShowIf("@enableBipedIK && enableGrounderBipedIK")]
        [SerializeField] private float grounderWeightSmoothTime = 0.12f;

        [PropertyOrder(31)]
        [TitleGroup("【接地IK】（GrounderBipedIK）/基础设置")]
        [LabelText("LerpingRate 回归时长 (秒)")]
        [Tooltip("Grounder 独立临时 lerpingRate 回归时长。0 = 立即回归到 1。")]
        [Range(0f, 2f)][ShowIf("@enableBipedIK && enableGrounderBipedIK")]
        [SerializeField] private float grounderLerpingRateRecoverTime = 0.2f;

        [PropertyOrder(32)]
        [TitleGroup("【接地IK】（GrounderBipedIK）/基础设置")]
        [LabelText("整体权重")][Tooltip("GrounderBipedIK.solver.weight。0=关闭，1=完全接地。")]
        [Range(0f, 1f)][ShowIf("@enableBipedIK && enableGrounderBipedIK")]
        [SerializeField] private float initGrounderWeight = 1f;

        [PropertyOrder(33)]
        [TitleGroup("【接地IK】（GrounderBipedIK）/基础设置")]
        [LabelText("最大步伐高度 (MaxStep)")][Tooltip("GrounderBipedIK.solver.maxStep。脚部最大可适应的地面高度差。建议 0.3~0.8。")]
        [Range(0f, 2f)][ShowIf("@enableBipedIK && enableGrounderBipedIK")]
        [SerializeField] private float initGrounderMaxStep = 0.5f;

        [PropertyOrder(34)]
        [TitleGroup("【接地IK】（GrounderBipedIK）/基础设置")]
        [LabelText("接地速度 (Speed)")][Tooltip("GrounderBipedIK.solver.speed。脚部跟随地面高度变化的速度。建议 2~5。")]
        [Range(0f, 20f)][ShowIf("@enableBipedIK && enableGrounderBipedIK")]
        [SerializeField] private float initGrounderSpeed = 3f;

        [PropertyOrder(30)]
        [TitleGroup("【四肢IK】（BipedIK）/基础设置")]
        [LabelText("权重平滑时长 (秒)")]
        [Tooltip("四肢 IK 独立权重平滑时长。0 = 关闭平滑，直接生效。")]
        [Range(0f, 1f)]
        [SerializeField] private float limbWeightSmoothTime = 0.12f;

        [PropertyOrder(31)]
        [TitleGroup("【四肢IK】（BipedIK）/基础设置")]
        [LabelText("LerpingRate 回归时长 (秒)")]
        [Tooltip("四肢 IK 独立临时 lerpingRate 回归时长。0 = 立即回归到 1。")]
        [Range(0f, 2f)]
        [SerializeField] private float limbLerpingRateRecoverTime = 0.2f;

        [PropertyOrder(32)]
        [TitleGroup("【四肢IK】（BipedIK）/基础设置")]
        [LabelText("脚部旋转权重倍率")]
        [Tooltip("脚部旋转权重倍率。0=只驱动位置（最稳）。建议从 0 开始逐步调至 0.2~1。")]
        [Range(0f, 1f)]
        [SerializeField] private float footRotationWeightMultiplier = 0.2f;

        [PropertyOrder(33)]
        [TitleGroup("【四肢IK】（BipedIK）/基础设置")]
        [LabelText("从 Pose 驱动目标点")]
        [Tooltip("true: pose 变化时将目标写入 Goal Transform（可视化）。\nfalse: 不覆盖，让你手动拖动目标点作为 IK 输入。")]
        [SerializeField] private bool driveGoalTargetsFromPose = true;

        [PropertyOrder(34)]
        [TitleGroup("【四肢IK】（BipedIK）/基础设置")]
        [LabelText("身体权重")]
        [Tooltip("无注视目标时，身体骨骼跟随量。建议 0.3~0.6，过高会让身体持续前倾。")]
        [Range(0f, 1f)]
        [SerializeField] private float bipedLookAtDefaultBodyWeight = 0.5f;

        [PropertyOrder(35)]
        [TitleGroup("【四肢IK】（BipedIK）/基础设置")]
        [HorizontalGroup("【四肢IK】（BipedIK）/基础设置/HW", LabelWidth = 80)]
        [LabelText("头部权重")]
        [Tooltip("无注视目标时，头部骨骼跟随量。建议 0.8~1.0。")]
        [Range(0f, 1f)]
        [SerializeField] private float bipedLookAtDefaultHeadWeight = 1.0f;

        [PropertyOrder(36)]
        [TitleGroup("【四肢IK】（BipedIK）/基础设置")]
        [HorizontalGroup("【四肢IK】（BipedIK）/基础设置/HW", LabelWidth = 80)]
        [LabelText("眼部权重")]
        [Tooltip("无注视目标时，眼部骨骼跟随量。建议 0.8~1.0。")]
        [Range(0f, 1f)]
        [SerializeField] private float bipedLookAtDefaultEyesWeight = 1.0f;

        [PropertyOrder(37)]
        [TitleGroup("【四肢IK】（BipedIK）/基础设置")]
        [LabelText("限制权重 (ClampWeight)")]
        [Tooltip("限制骨骼最大偏转角度（0=不限，1=完全锁死朝前）。建议 0.4~0.6。")]
        [Range(0f, 1f)]
        [SerializeField] private float bipedLookAtDefaultClampWeight = 0.5f;
    }
}