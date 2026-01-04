using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    public interface ITrackItem
    {
        public bool Enabled { get; set; }
        public IEnumerable<ITrackClip> Clips { get; }
        public Color ItemBGColor { get; }

        public string DisplayName { get; set; }

        public bool TryAddTrackClip(ITrackClip item);
        public bool TryRemoveTrackClip(ITrackClip item);

        public IEnumerable<Type> SupprtedClipTypes();
    }
    
    [Serializable]
    public abstract class TrackItemBase<TClip> : ITrackItem where TClip : class, ITrackClip
    {
        [LabelText("启用")]
        public bool enabled = true;
        [FoldoutGroup("轨道片段管理")]
        public List<TClip> clips = new List<TClip>();
        public bool Enabled { get => enabled; set => enabled = value; }
        public IEnumerable<ITrackClip> Clips => clips;

        public virtual Color ItemBGColor { get => Color.yellow._WithAlpha(0.15f); }

        public string DisplayName
        {
            get { if (displayName == "") { return this.GetType()._GetTypeDisplayName(); } return displayName; }
            set { displayName = value; }
        }
        [LabelText("显示名称")]
        public string displayName = "";
        public bool TryAddTrackClip(ITrackClip item)
        {
            if (item is TClip tItem)
            {
                if (!clips.Contains(tItem))
                {
                    clips.Add(tItem);
                    return true;
                }
            }
            return false;
        }
        public bool TryRemoveTrackClip(ITrackClip item)
        {
            if (item is TClip tItem)
            {
                return clips.Remove(tItem);
            }
            return false;
        }

        public IEnumerable<Type> SupprtedClipTypes() => new Type[] { typeof(TClip) };
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
        public CreateTrackItemAttribute(TrackItemType type, string name = "")
        {
            itemType = type;
            menuName = name;
        }
    }

}
