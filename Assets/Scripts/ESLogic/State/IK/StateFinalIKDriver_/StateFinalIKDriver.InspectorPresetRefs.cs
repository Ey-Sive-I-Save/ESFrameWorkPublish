using UnityEngine;
using RootMotion.FinalIK;
using Sirenix.OdinInspector;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        [PropertyOrder(20)]
        [BoxGroup("【四肢IK】（BipedIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【四肢IK】（BipedIK）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("BipedIK")]
        [Tooltip("提前拖入 BipedIK 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal BipedIK presetBipedIK;

        [PropertyOrder(20)]
        [BoxGroup("【接地IK】（GrounderBipedIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【接地IK】（GrounderBipedIK）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("GrounderBipedIK")]
        [Tooltip("提前拖入 GrounderBipedIK 组件。留空则自动查找。")]
        [ShowIf("@enableBipedIK && enableGrounderBipedIK")]
        [SerializeField] internal GrounderBipedIK presetGrounderBipedIK;

        [PropertyOrder(20)]
        [BoxGroup("【注视IK】（LookAtIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【注视IK】（LookAtIK）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("LookAtIK")]
        [Tooltip("提前拖入 LookAtIK 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("enableLookAtIK")]
        [SerializeField] internal LookAtIK presetLookAtIK;

        [PropertyOrder(20)]
        [BoxGroup("【瞄准IK】（AimIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("AimIK")]
        [Tooltip("提前拖入 AimIK 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("enableAimIK")]
        [SerializeField] internal AimIK presetAimIK;

        [PropertyOrder(20)]
        [BoxGroup("【全身IK】（FullBodyBipedIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【全身IK】（FullBodyBipedIK）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("FullBodyBipedIK")]
        [Tooltip("提前拖入 FullBodyBipedIK 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("enableFullBodyBipedIK")]
        [SerializeField] internal FullBodyBipedIK presetFullBodyBipedIK;

        [PropertyOrder(20)]
        [BoxGroup("【受击反馈】（HitReaction）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【受击反馈】（HitReaction）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("HitReaction")]
        [Tooltip("提前拖入 HitReaction 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction")]
        [SerializeField] internal HitReaction presetHitReaction;

        [PropertyOrder(20)]
        [BoxGroup("【后坐力】（Recoil）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("Recoil")]
        [Tooltip("提前拖入 Recoil 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil")]
        [SerializeField] internal Recoil presetRecoil;
    }
}