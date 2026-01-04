using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    [CreateTrackItem(TrackItemType.Skill,"GameObject轨道")]
    public class SkillTrackItem_GameObject : SkillTrackItem<SkillTrackClip_GameObject>
    {
        public override Color ItemBGColor => Color.green._WithAlpha(0.35f);
    }
    [System.Serializable,ESCreatePath("技能轨道剪辑","游戏对象轨道剪辑")]
    public class SkillTrackClip_GameObject : SkillTrackClip
    {
         [LabelText("激活状态")]
         public bool Activate=true;

    }
}
