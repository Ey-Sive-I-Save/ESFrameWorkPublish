using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        [PropertyOrder(40)]
        [BoxGroup("【四肢IK】（BipedIK）/辅助节点盒", ShowLabel = false)]
        [TitleGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点", BoldTitle = true)]
        [HorizontalGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点/手", LabelWidth = 60)]
        [LabelText("左手")]
        [Tooltip("左手 IK 目标点。留空则自动创建（在层级可见，可在 Scene 视图拖动调试）。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preGoalLH;

        [PropertyOrder(41)]
        [BoxGroup("【四肢IK】（BipedIK）/辅助节点盒", ShowLabel = false)]
        [TitleGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点")]
        [HorizontalGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点/手", LabelWidth = 60)]
        [LabelText("右手")]
        [Tooltip("右手 IK 目标点。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preGoalRH;

        [PropertyOrder(42)]
        [BoxGroup("【四肢IK】（BipedIK）/辅助节点盒", ShowLabel = false)]
        [TitleGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点")]
        [HorizontalGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点/脚", LabelWidth = 60)]
        [LabelText("左脚")]
        [Tooltip("左脚 IK 目标点。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preGoalLF;

        [PropertyOrder(43)]
        [BoxGroup("【四肢IK】（BipedIK）/辅助节点盒", ShowLabel = false)]
        [TitleGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点")]
        [HorizontalGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点/脚", LabelWidth = 60)]
        [LabelText("右脚")]
        [Tooltip("右脚 IK 目标点。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preGoalRF;

        [PropertyOrder(44)]
        [BoxGroup("【四肢IK】（BipedIK）/辅助节点盒", ShowLabel = false)]
        [TitleGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点")]
        [HorizontalGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点/肘", LabelWidth = 60)]
        [LabelText("左肘")]
        [Tooltip("左肘弯曲方向提示（Hint），控制肘部朝向。留空则自动创建（HideInHierarchy）。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preHintLH;

        [PropertyOrder(45)]
        [BoxGroup("【四肢IK】（BipedIK）/辅助节点盒", ShowLabel = false)]
        [TitleGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点")]
        [HorizontalGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点/肘", LabelWidth = 60)]
        [LabelText("右肘")]
        [Tooltip("右肘弯曲方向提示（Hint），控制肘部朝向。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preHintRH;

        [PropertyOrder(46)]
        [BoxGroup("【四肢IK】（BipedIK）/辅助节点盒", ShowLabel = false)]
        [TitleGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点")]
        [HorizontalGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点/膝", LabelWidth = 60)]
        [LabelText("左膝")]
        [Tooltip("左膝弯曲方向提示（Hint），控制膝盖朝向。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preHintLF;

        [PropertyOrder(47)]
        [BoxGroup("【四肢IK】（BipedIK）/辅助节点盒", ShowLabel = false)]
        [TitleGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点")]
        [HorizontalGroup("【四肢IK】（BipedIK）/辅助节点盒/辅助节点/膝", LabelWidth = 60)]
        [LabelText("右膝")]
        [Tooltip("右膝弯曲方向提示（Hint），控制膝盖朝向。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preHintRF;
    }
}