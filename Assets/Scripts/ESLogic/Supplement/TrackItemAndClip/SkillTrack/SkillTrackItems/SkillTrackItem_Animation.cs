using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    [CreateTrackItem(TrackItemType.Skill, "Animation轨道")]
    public class SkillTrackItem_Animation : SkillTrackItem<SkillTrackClip_Animation>
    {
        public string AnimationIM = "这是一个动画轨道";
        override public Color ItemBGColor => Color.cyan._WithAlpha(0.35f);
    }
    [System.Serializable, ESCreatePath("技能轨道剪辑", "动画轨道剪辑")]
    public class SkillTrackClip_Animation : SkillTrackClip
    {
        [LabelText("动画剪辑")]
        public AnimationClip AnimationClipName;
    }
}
