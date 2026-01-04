using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public interface ITrackItem
    {
        public bool Enabled{get;set;}
        public IEnumerable<ITrackClip> Clips{get;}
        public Color ItemBGColor{get;}

        public string DisplayName{get;set;}
    }
    //每类轨道的枚举
    public enum TrackItemType
    {
        Skill,
        Buff,
        Custom,
    }

    public class CreateTrackItemAttribute : Attribute
    {
        public TrackItemType itemType;
        public string menuName;
        public CreateTrackItemAttribute(TrackItemType type,string name="")
        {
            itemType = type;
            menuName = name;
        }
    }
    
}
