using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        [PropertyOrder(10)]
        [FoldoutGroup("【四肢IK】（BipedIK）")]
        [InfoBox("Bind 时若目标组件缺失则自动 AddComponent。AimIK / HitReaction / Recoil 可进一步使用 Driver 配置生成常用默认参数。", InfoMessageType.None)]
        [LabelText("BipedIK")]
        [Tooltip("Bind 时若未找到 BipedIK，自动 AddComponent<BipedIK>()。Humanoid 骨架支持 AutoDetectReferences，零配置可用。")]
        [EnableIf("enableBipedIK")]
        [SerializeField] private bool autoAddBipedIK = false;

        [PropertyOrder(10)]
        [FoldoutGroup("【接地IK】（GrounderBipedIK）")]
        [LabelText("GrounderBipedIK")]
        [Tooltip("Bind 时若未找到 GrounderBipedIK，自动 AddComponent<GrounderBipedIK>()。OnEnable 时自动扫描 BipedIK solver，可安全自动添加。")]
        [EnableIf("@enableBipedIK && enableGrounderBipedIK")]
        [SerializeField] private bool autoAddGrounderBipedIK = false;

        [PropertyOrder(10)]
        [FoldoutGroup("【注视IK】（LookAtIK）")]
        [LabelText("LookAtIK")]
        [Tooltip("Bind 时若未找到 LookAtIK，自动 AddComponent<LookAtIK>()，Humanoid 骨架自动填充 solver.head 和 solver.spine。")]
        [EnableIf("enableLookAtIK")]
        [SerializeField] private bool autoAddLookAtIK = false;

        [PropertyOrder(10)]
        [FoldoutGroup("【瞄准IK】（AimIK）")]
        [LabelText("AimIK")]
        [Tooltip("Bind 时若未找到 AimIK，自动 AddComponent<AimIK>()。\n注意：仍需手动配置 solver.bones 骨链和 solver.axis 瞄准轴。")]
        [EnableIf("enableAimIK")]
        [SerializeField] private bool autoAddAimIK = false;

        [PropertyOrder(10)]
        [FoldoutGroup("【全身IK】（FullBodyBipedIK）")]
        [LabelText("FullBodyBipedIK")]
        [Tooltip("Bind 时若未找到 FullBodyBipedIK，自动 AddComponent<FullBodyBipedIK>() 并调用 AutoDetectReferences。")]
        [EnableIf("enableFullBodyBipedIK")]
        [SerializeField] private bool autoAddFullBodyBipedIK = false;

        [PropertyOrder(10)]
        [FoldoutGroup("【受击反馈】（HitReaction）")]
        [LabelText("HitReaction")]
        [Tooltip("Bind 时若未找到 HitReaction，自动 AddComponent<HitReaction>()。若启用 Driver 配置，可自动生成常用人形受击点和曲线。")]
        [EnableIf("@enableFullBodyBipedIK && enableHitReaction")]
        [SerializeField] private bool autoAddHitReaction = false;

        [PropertyOrder(10)]
        [FoldoutGroup("【后坐力】（Recoil）")]
        [LabelText("Recoil")]
        [Tooltip("Bind 时若未找到 Recoil，自动 AddComponent<Recoil>()。若启用 Driver 配置，可自动生成常用枪械后坐力参数。")]
        [EnableIf("@enableFullBodyBipedIK && enableRecoil")]
        [SerializeField] private bool autoAddRecoil = false;
    }
}