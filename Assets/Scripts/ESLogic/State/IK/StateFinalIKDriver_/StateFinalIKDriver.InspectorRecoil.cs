using UnityEngine;
using RootMotion.FinalIK;
using Sirenix.OdinInspector;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        [PropertyOrder(30)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置", BoldTitle = true)]
        [InfoBox("可在 Driver 内直接生成常见枪械后坐力配置，自动连接 FullBodyBipedIK 与 AimIK，减少对 Recoil 原生 Inspector 的依赖。", InfoMessageType.None)]
        [LabelText("启用 Driver 配置")]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil")]
        [SerializeField] private bool useDriverRecoilSetup = false;

        [PropertyOrder(31)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/参数", LabelWidth = 92)]
        [LabelText("整体权重")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil && useDriverRecoilSetup")]
        [SerializeField] private float driverRecoilWeight = 1f;

        [PropertyOrder(32)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/参数", LabelWidth = 92)]
        [LabelText("主手")]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil && useDriverRecoilSetup")]
        [SerializeField] private Recoil.Handedness driverRecoilHandedness = Recoil.Handedness.Right;

        [PropertyOrder(33)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/参数", LabelWidth = 92)]
        [LabelText("双手持枪")]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil && useDriverRecoilSetup")]
        [SerializeField] private bool driverRecoilTwoHanded = true;

        [PropertyOrder(34)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/参数", LabelWidth = 92)]
        [LabelText("脉冲时长")]
        [Range(0.05f, 0.6f)]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil && useDriverRecoilSetup")]
        [SerializeField] private float driverRecoilDuration = 0.18f;

        [PropertyOrder(35)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/参数", LabelWidth = 92)]
        [LabelText("混合时长")]
        [Range(0f, 0.3f)]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil && useDriverRecoilSetup")]
        [SerializeField] private float driverRecoilBlendTime = 0.08f;

        [PropertyOrder(36)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/参数", LabelWidth = 92)]
        [LabelText("强度随机")]
        [Range(0f, 1f)]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil && useDriverRecoilSetup")]
        [SerializeField] private float driverRecoilMagnitudeRandom = 0.08f;

        [PropertyOrder(37)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/偏移", LabelWidth = 92)]
        [LabelText("主手位移")]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil && useDriverRecoilSetup")]
        [SerializeField] private Vector3 driverRecoilPrimaryOffset = new Vector3(0f, 0.02f, -0.06f);

        [PropertyOrder(38)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/偏移", LabelWidth = 92)]
        [LabelText("副手位移")]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil && useDriverRecoilSetup")]
        [SerializeField] private Vector3 driverRecoilSecondaryOffset = new Vector3(0f, 0.01f, -0.035f);

        [PropertyOrder(39)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/偏移", LabelWidth = 92)]
        [LabelText("身体位移")]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil && useDriverRecoilSetup")]
        [SerializeField] private Vector3 driverRecoilBodyOffset = new Vector3(0f, 0f, -0.015f);

        [PropertyOrder(40)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/偏移", LabelWidth = 92)]
        [LabelText("手部旋转")]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil && useDriverRecoilSetup")]
        [SerializeField] private Vector3 driverRecoilHandRotationOffset = new Vector3(-8f, 0f, 0f);

        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/偏移", LabelWidth = 92)]
        [LabelText("旋转随机")]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil && useDriverRecoilSetup")]
        [SerializeField] private Vector3 driverRecoilRotationRandom = new Vector3(1.5f, 0.8f, 0.8f);
    }
}