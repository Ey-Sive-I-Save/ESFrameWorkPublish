using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        [PropertyOrder(30)]
        [FoldoutGroup("DriverLayout/公共部分/受击反馈/配置")]
        [InfoBox("可在 Driver 内直接生成人形角色常用的受击点配置，减少对 HitReaction 原生 Inspector 的依赖。复杂部位映射仍可手动覆盖。", InfoMessageType.None)]
        [LabelText("启用 Driver 配置")]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction")]
        [SerializeField] private bool useDriverHitReactionSetup = false;

        [PropertyOrder(31)]
        [FoldoutGroup("DriverLayout/公共部分/受击反馈/配置")]
        [HorizontalGroup("DriverLayout/公共部分/受击反馈/配置/参数", LabelWidth = 92)]
        [LabelText("整体权重")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction && useDriverHitReactionSetup")]
        [SerializeField] private float driverHitReactionWeight = 1f;

        [PropertyOrder(32)]
        [FoldoutGroup("DriverLayout/公共部分/受击反馈/配置")]
        [HorizontalGroup("DriverLayout/公共部分/受击反馈/配置/参数", LabelWidth = 92)]
        [LabelText("受击时长")]
        [Range(0.05f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction && useDriverHitReactionSetup")]
        [SerializeField] private float driverHitReactionDuration = 0.3f;

        [PropertyOrder(33)]
        [FoldoutGroup("DriverLayout/公共部分/受击反馈/配置")]
        [HorizontalGroup("DriverLayout/公共部分/受击反馈/配置/参数", LabelWidth = 92)]
        [LabelText("抬升倍率")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction && useDriverHitReactionSetup")]
        [SerializeField] private float driverHitReactionUpForce = 0.2f;

        [PropertyOrder(34)]
        [FoldoutGroup("DriverLayout/公共部分/受击反馈/配置")]
        [HorizontalGroup("DriverLayout/公共部分/受击反馈/配置/参数", LabelWidth = 92)]
        [LabelText("头部扭转角")]
        [Range(0f, 45f)]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction && useDriverHitReactionSetup")]
        [SerializeField] private float driverHitReactionHeadAngle = 14f;

        [PropertyOrder(35)]
        [FoldoutGroup("DriverLayout/公共部分/受击反馈/配置")]
        [HorizontalGroup("DriverLayout/公共部分/受击反馈/配置/碰撞体", LabelWidth = 72)]
        [LabelText("身体")]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction && useDriverHitReactionSetup")]
        [SerializeField] private Collider hitBodyCollider;

        [PropertyOrder(36)]
        [FoldoutGroup("DriverLayout/公共部分/受击反馈/配置")]
        [HorizontalGroup("DriverLayout/公共部分/受击反馈/配置/碰撞体", LabelWidth = 72)]
        [LabelText("头部")]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction && useDriverHitReactionSetup")]
        [SerializeField] private Collider hitHeadCollider;

        [PropertyOrder(37)]
        [FoldoutGroup("DriverLayout/公共部分/受击反馈/配置")]
        [HorizontalGroup("DriverLayout/公共部分/受击反馈/配置/碰撞体", LabelWidth = 72)]
        [LabelText("左臂")]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction && useDriverHitReactionSetup")]
        [SerializeField] private Collider hitLeftArmCollider;

        [PropertyOrder(38)]
        [FoldoutGroup("DriverLayout/公共部分/受击反馈/配置")]
        [HorizontalGroup("DriverLayout/公共部分/受击反馈/配置/碰撞体", LabelWidth = 72)]
        [LabelText("右臂")]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction && useDriverHitReactionSetup")]
        [SerializeField] private Collider hitRightArmCollider;

        [PropertyOrder(39)]
        [FoldoutGroup("DriverLayout/公共部分/受击反馈/配置")]
        [HorizontalGroup("DriverLayout/公共部分/受击反馈/配置/碰撞体", LabelWidth = 72)]
        [LabelText("左腿")]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction && useDriverHitReactionSetup")]
        [SerializeField] private Collider hitLeftLegCollider;

        [PropertyOrder(40)]
        [FoldoutGroup("DriverLayout/公共部分/受击反馈/配置")]
        [HorizontalGroup("DriverLayout/公共部分/受击反馈/配置/碰撞体", LabelWidth = 72)]
        [LabelText("右腿")]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction && useDriverHitReactionSetup")]
        [SerializeField] private Collider hitRightLegCollider;
    }
}
