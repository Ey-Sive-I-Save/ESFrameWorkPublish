
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    [ESCreatePath("数据信息", "技能数据信息")]
    public class SKillDataInfo : SoDataInfo, IEditorTrackSupport_GetSequence
    {
        [LabelText("标准技能序列")]
        public SkillTrackSequence sequence;

        public ITrackSequence Sequence => sequence;

        public TrackItemType trackItemType => TrackItemType.Skill;

        public string trackName=> name;

        public override void OnEditorApply()
        {
            base.OnEditorApply();
            
        }
    }
    }

//ES已修正