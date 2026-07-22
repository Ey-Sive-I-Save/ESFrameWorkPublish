using UnityEngine;
using Sirenix.OdinInspector;
using RootMotion.FinalIK;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        [TabGroup("DriverLayout", "公共部分", Order = 0)]
        [PropertyOrder(24)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）", Expanded = false)]
        [InfoBox("用于角色全身姿态、武器持握、受击/后坐力承载。常规角色保持默认即可；射击、攀爬、重交互角色再调高对应链权重。", InfoMessageType.None)]
        [LabelText("使用 Driver 全身IK预设")]
        [ShowIf("enableFullBodyBipedIK")]
        [SerializeField] private bool useDriverFullBodyBipedIKSetup = true;

        [PropertyOrder(25)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）")]
        [LabelText("总权重")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyIKWeight = 1f;

        [PropertyOrder(26)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）")]
        [LabelText("求解迭代")]
        [Range(0, 10)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private int driverFullBodyIterations = 2;

        [PropertyOrder(27)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）")]
        [LabelText("启用 FABRIK 拉扯")]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private bool driverFullBodyFABRIKPass = true;

        [PropertyOrder(28)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/身体")]
        [LabelText("脊柱刚性")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodySpineStiffness = 0.45f;

        [PropertyOrder(29)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/身体")]
        [LabelText("身体垂直拉扯")]
        [Range(-1f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyPullBodyVertical = 0.35f;

        [PropertyOrder(30)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/身体")]
        [LabelText("身体水平拉扯")]
        [Range(-1f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyPullBodyHorizontal = 0.15f;

        [PropertyOrder(31)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/身体")]
        [LabelText("身体影响大腿")]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private bool driverFullBodyBodyAffectsThighs = true;

        [PropertyOrder(32)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/身体")]
        [LabelText("脊柱映射迭代")]
        [Range(1, 3)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private int driverFullBodySpineMappingIterations = 2;

        [PropertyOrder(33)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/身体")]
        [LabelText("脊柱扭转映射")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodySpineTwistWeight = 0.65f;

        [PropertyOrder(34)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/手臂链")]
        [LabelText("手臂 Pull")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyArmPull = 0.75f;

        [PropertyOrder(35)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/手臂链")]
        [LabelText("手臂 Reach")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyArmReach = 0.08f;

        [PropertyOrder(36)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/手臂链")]
        [LabelText("手臂 Push")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyArmPush = 0.15f;

        [PropertyOrder(37)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/手臂链")]
        [LabelText("手臂映射")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyArmMapping = 1f;

        [PropertyOrder(38)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/腿部链")]
        [LabelText("腿部 Pull")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyLegPull = 0.35f;

        [PropertyOrder(39)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/腿部链")]
        [LabelText("腿部 Reach")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyLegReach = 0.04f;

        [PropertyOrder(40)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/腿部链")]
        [LabelText("腿部 Push")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyLegPush = 0.08f;

        [PropertyOrder(41)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/腿部链")]
        [LabelText("腿部映射")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyLegMapping = 1f;

        [PropertyOrder(42)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/弯曲约束")]
        [LabelText("手臂弯曲约束")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyArmBendWeight = 0f;

        [PropertyOrder(43)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）/弯曲约束")]
        [LabelText("腿部弯曲约束")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        [SerializeField] private float driverFullBodyLegBendWeight = 0f;

        [PropertyOrder(44)]
        [FoldoutGroup("DriverLayout/公共部分/【全身IK】（FBBIK）")]
        [Button("应用 FBBIK 配置", ButtonSizes.Medium)]
        [ShowIf("@enableFullBodyBipedIK && useDriverFullBodyBipedIKSetup")]
        private void ApplyDriverFullBodyBipedIKFromInspector()
        {
            var fullBodyBipedIK = _refs.fullBodyBipedIK ?? presetFullBodyBipedIK ?? GetComponent<FullBodyBipedIK>();
            if (fullBodyBipedIK == null)
            {
                Debug.LogWarning("[StateFinalIKDriver] 当前未找到 FullBodyBipedIK 组件，无法应用全身IK配置。", this);
                return;
            }

            _refs.fullBodyBipedIK = fullBodyBipedIK;
            ApplyDriverFullBodyBipedIKSettings(fullBodyBipedIK);

            if (Application.isPlaying)
                ApplyFinalIKExecutionPolicy();
        }
    }
}
