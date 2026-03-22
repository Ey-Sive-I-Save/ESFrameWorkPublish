using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(20)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定", BoldTitle = true)]
        [InfoBox("不想进入 FinalIK 原生 Inspector 时，可在这里一次性配置通用骨骼并应用到 BipedIK / FullBodyBipedIK / LookAtIK / AimIK。AimIK 的 solver.bones 已彻底收口到这里的躯干绑定派生结果，总面板是唯一真源。", InfoMessageType.None)]
        [LabelText("启用 Driver 骨骼绑定")]
        [SerializeField] private bool useDriverBoneBinding = false;

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(21)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/根骨", LabelWidth = 70)]
        [LabelText("Root")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingRoot;

        [PropertyOrder(22)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/躯干", LabelWidth = 70)]
        [LabelText("骨盆")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingPelvis;

        [PropertyOrder(23)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/躯干", LabelWidth = 70)]
        [LabelText("脊椎")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingSpine;

        [PropertyOrder(24)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/躯干", LabelWidth = 70)]
        [LabelText("胸腔")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingChest;

        [PropertyOrder(25)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/躯干", LabelWidth = 70)]
        [LabelText("颈部")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingNeck;

        [PropertyOrder(26)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/头眼", LabelWidth = 70)]
        [LabelText("头部")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingHead;

        [PropertyOrder(27)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/头眼", LabelWidth = 70)]
        [LabelText("左眼")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingLeftEye;

        [PropertyOrder(28)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/头眼", LabelWidth = 70)]
        [LabelText("右眼")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingRightEye;

        [PropertyOrder(29)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/左臂", LabelWidth = 70)]
        [LabelText("上臂")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingLeftUpperArm;

        [PropertyOrder(30)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/左臂", LabelWidth = 70)]
        [LabelText("前臂")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingLeftForearm;

        [PropertyOrder(31)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/左臂", LabelWidth = 70)]
        [LabelText("左手")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingLeftHand;

        [PropertyOrder(32)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/右臂", LabelWidth = 70)]
        [LabelText("上臂")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingRightUpperArm;

        [PropertyOrder(33)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/右臂", LabelWidth = 70)]
        [LabelText("前臂")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingRightForearm;

        [PropertyOrder(34)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/右臂", LabelWidth = 70)]
        [LabelText("右手")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingRightHand;

        [PropertyOrder(35)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/左腿", LabelWidth = 70)]
        [LabelText("大腿")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingLeftThigh;

        [PropertyOrder(36)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/左腿", LabelWidth = 70)]
        [LabelText("小腿")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingLeftCalf;

        [PropertyOrder(37)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/左腿", LabelWidth = 70)]
        [LabelText("左脚")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingLeftFoot;

        [PropertyOrder(38)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/右腿", LabelWidth = 70)]
        [LabelText("大腿")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingRightThigh;

        [PropertyOrder(39)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/右腿", LabelWidth = 70)]
        [LabelText("小腿")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingRightCalf;

        [PropertyOrder(40)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/右腿", LabelWidth = 70)]
        [LabelText("右脚")]
        [ShowIf("useDriverBoneBinding")]
        [SerializeField] private Transform bindingRightFoot;

        [PropertyOrder(30)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定", BoldTitle = true)]
        [InfoBox("AimIK 的 solver.bones 不再提供单独输入。Driver 会严格从总面板统一骨骼绑定派生 4 节父到子骨链，末节强制为 neck；不合法时会直接拒绝应用。solver.transform 仍必须绑定到玩家自定义的枪口/枪身方向节点，而不是骨头本身。", InfoMessageType.None)]
        [ShowInInspector, ReadOnly]
        [ShowIf("enableAimIK")]
        [LabelText("派生骨链摘要")]
        private string AimDerivedChainSummary => BuildAimDerivedChainSummary();

        [PropertyOrder(31)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定")]
        [ShowInInspector, ReadOnly]
        [GUIColor("@GetAimBindingSummaryColor()")]
        [ShowIf("enableAimIK")]
        [LabelText("绑定校验")]
        private string AimBindingValidationSummary => GetAimBindingValidationSummary();

        [SerializeField, HideInInspector] private bool useDriverAimBoneChain = false;

        [SerializeField, HideInInspector] private Transform aimChainBone1;
        [SerializeField, HideInInspector] private Transform aimChainBone2;
        [SerializeField, HideInInspector] private Transform aimChainBone3;
        [SerializeField, HideInInspector] private Transform aimChainBone4;

        [PropertyOrder(35)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定")]
        [HorizontalGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定/控制", LabelWidth = 70)]
        [LabelText("瞄准方向节点")]
        [Tooltip("写入 AimIK.solver.transform。这里应指定玩家自定义的枪口/枪身方向节点，不应直接填骨头；Driver 不会自动注入。")]
        [ValidateInput(nameof(ValidateAimControlledTransform), "AimIK 缺少合法的瞄准方向节点。请绑定玩家自定义枪口/枪身方向节点，且不要直接使用躯干骨骼。")]
        [ShowIf("enableAimIK")]
        [SerializeField] private Transform aimControlledTransform;

        [PropertyOrder(36)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定")]
        [HorizontalGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定/控制", LabelWidth = 70)]
        [LabelText("极向目标")]
        [ShowIf("enableAimIK")]
        [SerializeField] private Transform aimPoleTarget;

        [PropertyOrder(37)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定")]
        [HorizontalGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定/控制", LabelWidth = 70)]
        [LabelText("极向轴")]
        [ShowIf("enableAimIK")]
        [SerializeField] private Vector3 aimPoleAxis = Vector3.up;

        [PropertyOrder(38)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定")]
        [HorizontalGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定/控制", LabelWidth = 70)]
        [LabelText("极向权重")]
        [Range(0f, 1f)]
        [ShowIf("enableAimIK")]
        [SerializeField] private float aimPoleWeight = 0f;

        [PropertyOrder(40)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定")]
        [HorizontalGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定/探头锚点", LabelWidth = 70)]
        [LabelText("左肩锚点")]
        [Tooltip("推荐填写左探头时的瞄准锚点/相机锚点。配置后，探头将优先改用锚点和视角前向来重建 Aim 目标，而不是简单横向平移目标点。")]
        [ShowIf("enableAimIK")]
        [SerializeField] private Transform aimPeekLeftAnchor;

        [PropertyOrder(41)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定")]
        [HorizontalGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定/探头锚点", LabelWidth = 70)]
        [LabelText("右肩锚点")]
        [Tooltip("推荐填写右探头时的瞄准锚点/相机锚点。与左肩锚点配合后，运行时会优先走肩位切换方案。")]
        [ShowIf("enableAimIK")]
        [SerializeField] private Transform aimPeekRightAnchor;

        [PropertyOrder(42)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定")]
        [HorizontalGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定/探头", LabelWidth = 70)]
        [LabelText("探头参考")]
        [Tooltip("旧版探头偏移使用的局部空间参考。仅当未配置左右肩锚点时，才会退回到该参考节点 + 局部偏移方案。")]
        [ShowIf("enableAimIK")]
        [SerializeField] private Transform aimPeekReferenceTransform;

        [PropertyOrder(43)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定")]
        [HorizontalGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定/探头", LabelWidth = 70)]
        [LabelText("左探头")]
        [Tooltip("旧版兜底方案：Aim 目标在参考节点局部空间下的左探头偏移。仅在未配置左/右肩锚点时生效。")]
        [ShowIf("enableAimIK")]
        [SerializeField] private Vector3 aimPeekLeftLocalOffset = new Vector3(-0.12f, 0f, 0f);

        [PropertyOrder(44)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定")]
        [HorizontalGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定/探头", LabelWidth = 70)]
        [LabelText("右探头")]
        [Tooltip("旧版兜底方案：Aim 目标在参考节点局部空间下的右探头偏移。仅在未配置左/右肩锚点时生效。")]
        [ShowIf("enableAimIK")]
        [SerializeField] private Vector3 aimPeekRightLocalOffset = new Vector3(0.12f, 0f, 0f);
    }
}