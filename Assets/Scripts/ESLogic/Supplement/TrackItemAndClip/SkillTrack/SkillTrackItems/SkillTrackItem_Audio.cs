using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    [CreateTrackItem(TrackItemType.Skill,"Audio轨道")]
    public class SkillTrackItem_Audio : SkillTrackItem<SkillTrackClip_Audio>
    {
   
    }

    [System.Serializable,ESCreatePath("技能轨道剪辑","音频轨道剪辑")]
    public class SkillTrackClip_Audio : SkillTrackClip
    {
        [LabelText("音频剪辑")]
        public AudioClip audioClip;
    }
}
