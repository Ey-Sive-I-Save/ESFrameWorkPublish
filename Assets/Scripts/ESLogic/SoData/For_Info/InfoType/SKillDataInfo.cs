
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
 namespace ES{ 
     [ESCreatePath("数据信息", "技能数据信息")]
     public class SKillDataInfo : SoDataInfo,EditorTrackSupport_GetSequence
     {
         [LabelText("标准技能序列")]
         public SkillTrackSequence sequence;

        public ITrackSequence Sequence => sequence;
    }
    public interface EditorTrackSupport_GetSequence
    {
        public ITrackSequence Sequence{get;}
    }
    [Serializable]
    public class SkillTrackSequence : ITrackSequence
    {
        public List<SkillTrackItem> tracks =new();

        public IEnumerable<ITrackItem> Tracks => tracks;

        public List<Type> UseableTrackTypes =>new List<Type>();


        [Button("初始化")]
        public void InitByEditor()
        {
#if UNITY_EDITOR
            if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("初始化技能轨道", "清除已经做出的修改并且重置为默认状态"))
            {
                tracks.Clear();
            }
            #endif
        }
    }
    [Serializable]
    public class SkillTrackItem : ITrackItem
    {
        public bool enabled=true;
        public List<SkillNode> nodes=new List<SkillNode>();
          public bool Enabled { get =>enabled; set => enabled=value; }

        public IEnumerable<ITrackNode> Nodes => nodes;

        public Color ItemBGColor {get=>Color.yellow._WithAlpha(0.6f);}

    }
    [Serializable]
    public class SkillNode : ITrackNode
    {
        public string name="节点";
        public float startTime=0;
        public float durationTime=1;

        public string Name { get => name; set => name=value; }
        public float StartTime { get => startTime; set => startTime=value; }
        public float DurationTime { get => durationTime; set =>  durationTime=value; }

    }
    }

//ES已修正