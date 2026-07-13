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
        [LabelText("BipedIK（预设输入）")]
        [Tooltip("这是预设输入槽位，不是运行时连接状态。留空会在 Bind 时自动查找/自动添加；运行时请看“诊断/已解析求解器”中的连接结果。")]
        [HideInPlayMode]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal BipedIK presetBipedIK;

        [PropertyOrder(20)]
        [BoxGroup("【接地IK】（GrounderBipedIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【接地IK】（GrounderBipedIK）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("GrounderBipedIK（预设输入）")]
        [Tooltip("预设输入槽位。留空会在 Bind 时自动查找；运行时连接状态请看诊断面板。")]
        [HideInPlayMode]
        [ShowIf("@enableBipedIK && enableGrounderBipedIK")]
        [SerializeField] internal GrounderBipedIK presetGrounderBipedIK;

        [PropertyOrder(20)]
        [BoxGroup("【注视IK】（LookAtIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【注视IK】（LookAtIK）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("LookAtIK（预设输入）")]
        [Tooltip("预设输入槽位。留空会在 Bind 时自动查找/自动添加；运行时连接状态请看诊断面板。")]
        [HideInPlayMode]
        [ShowIf("enableLookAtIK")]
        [SerializeField] internal LookAtIK presetLookAtIK;

        [PropertyOrder(20)]
        [BoxGroup("【瞄准IK】（AimIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("AimIK（预设输入）")]
        [Tooltip("预设输入槽位。留空会在 Bind 时自动查找/自动添加；运行时连接状态请看诊断面板。")]
        [HideInPlayMode]
        [ShowIf("enableAimIK")]
        [SerializeField] internal AimIK presetAimIK;

        [PropertyOrder(20)]
        [BoxGroup("【全身IK】（FullBodyBipedIK）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【全身IK】（FullBodyBipedIK）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("FullBodyBipedIK（预设输入）")]
        [Tooltip("预设输入槽位。留空会在 Bind 时自动查找/自动添加；运行时连接状态请看诊断面板。")]
        [HideInPlayMode]
        [ShowIf("enableFullBodyBipedIK")]
        [SerializeField] internal FullBodyBipedIK presetFullBodyBipedIK;

        [PropertyOrder(20)]
        [BoxGroup("【受击反馈】（HitReaction）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【受击反馈】（HitReaction）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("HitReaction（预设输入）")]
        [Tooltip("预设输入槽位。留空会在 Bind 时自动查找/自动添加；运行时连接状态请看诊断面板。")]
        [HideInPlayMode]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction")]
        [SerializeField] internal HitReaction presetHitReaction;

        [PropertyOrder(20)]
        [BoxGroup("【后坐力】（Recoil）/基础设置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/基础设置盒/基础设置", BoldTitle = true)]
        [LabelText("Recoil（预设输入）")]
        [Tooltip("预设输入槽位。留空会在 Bind 时自动查找/自动添加；运行时连接状态请看诊断面板。")]
        [HideInPlayMode]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil")]
        [SerializeField] internal Recoil presetRecoil;
    }
}