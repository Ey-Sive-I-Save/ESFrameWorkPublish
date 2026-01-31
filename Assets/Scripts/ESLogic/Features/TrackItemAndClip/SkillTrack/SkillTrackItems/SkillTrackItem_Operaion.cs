using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    [CreateTrackItem(TrackItemType.Skill,"Operation轨道")]
    public class SkillTrackItem_Operation: SkillTrackItem<SkillTrackClip_Operation>
    {
    
    }
    [System.Serializable,ESCreatePath("技能轨道剪辑","操作轨道剪辑")]
    public class SkillTrackClip_Operation : SkillTrackClip
    {
          [LabelText("操作描述")]
          public string OperationDescription;

          [SerializeReference,HideLabel,BoxGroup("操作内容")]
          public ESOutputOp op;
    }
}
