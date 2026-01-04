
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
 namespace ES{ 
     [ESCreatePath("数据信息", "技能数据信息")]
     public class SKillDataInfo : SoDataInfo,IEditorTrackSupport_GetSequence
     {
         [LabelText("标准技能序列")]
         public SkillTrackSequence sequence;

        public ITrackSequence Sequence => sequence;

        public TrackItemType trackItemType => TrackItemType.Skill;
    }
    public interface IEditorTrackSupport_GetSequence
    {
        public ITrackSequence Sequence{get;}
        public TrackItemType trackItemType{get;}
    }
    [Serializable]
    public class SkillTrackSequence : TrackSequenceBase<ISkillTrackItem>
    {
        [Button("初始化")]
        public override void InitByEditor()
        {
#if UNITY_EDITOR
            if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("初始化技能轨道", "清除已经做出的修改并且重置为默认状态"))
            {
                tracks_.Clear();
            }
            #endif
        }
    }

    public interface ISkillTrackItem : ITrackItem
    {

    }
    
    [Serializable]
    public class SkillTrackItem<SkillTrackClipT> : TrackItemBase<SkillTrackClipT>,ISkillTrackItem where SkillTrackClipT : SkillTrackClip
    {
       
    }
    
    [Serializable,ESCreatePath("轨道项","技能标准轨道")]
    public class SkillTrackItemStand : SkillTrackItem<SkillTrackClip>
    {
         
    }

    [Serializable]
    public class SkillTrackClip : TrackClipBase
    {
       
    }
    }

//ES已修正