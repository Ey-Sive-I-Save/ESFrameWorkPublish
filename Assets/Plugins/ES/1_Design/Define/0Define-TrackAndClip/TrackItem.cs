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
        public bool SortClipsByTime();

        public IEnumerable<Type> SupportedClipTypes();

        List<IEditorTimeSampler> CreateSamplers(ITrackSequence sequence);
#if UNITY_EDITOR
        List<IEditorTimeSampler> CreateEditorSamplers(ITrackSequence sequence, object editorTarget);
#endif

    }

    [Serializable]
    public abstract class TrackItemBase<TClip> : ITrackItem where TClip : class, ITrackClip
    {
        [TitleGroup("轨道设置", "控制当前轨道是否参与预览/运行，以及轨道在编辑器中的显示名称。")]
        [HorizontalGroup("轨道设置/基础", Width = 70)]
        [LabelText("启用")]
        public bool enabled = true;

        [TitleGroup("轨道设置")]
        [LabelText("片段列表")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true, ShowIndexLabels = true)]
        public List<TClip> clips = new List<TClip>();
        public bool Enabled { get => enabled; set => enabled = value; }
        public IEnumerable<ITrackClip> Clips => clips;

        public virtual Color ItemBGColor { get => Color.yellow._WithAlpha(0.15f); }

        public string DisplayName
        {
            get { if (displayName == "") { return this.GetType()._GetTypeDisplayName(); } return displayName; }
            set { displayName = value; }
        }
        [TitleGroup("轨道设置")]
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

        public bool SortClipsByTime()
        {
            if (clips == null || clips.Count <= 1)
                return false;

            bool changed = false;
            for (int i = 1; i < clips.Count; i++)
            {
                TClip previous = clips[i - 1];
                TClip current = clips[i];
                float previousStart = previous != null ? previous.StartTime : float.MaxValue;
                float currentStart = current != null ? current.StartTime : float.MaxValue;
                if (currentStart < previousStart)
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
                return false;

            clips.Sort((a, b) =>
            {
                float aStart = a != null ? a.StartTime : float.MaxValue;
                float bStart = b != null ? b.StartTime : float.MaxValue;
                int startCompare = aStart.CompareTo(bStart);
                if (startCompare != 0)
                    return startCompare;

                float aEnd = a != null ? a.StartTime + Mathf.Max(0f, a.DurationTime) : float.MaxValue;
                float bEnd = b != null ? b.StartTime + Mathf.Max(0f, b.DurationTime) : float.MaxValue;
                return aEnd.CompareTo(bEnd);
            });
            return true;
        }

        public IEnumerable<Type> SupportedClipTypes() => new Type[] { typeof(TClip) };


        public virtual List<IEditorTimeSampler> CreateSamplers(ITrackSequence sequence)
        {
            var list = new List<IEditorTimeSampler>();
            if (clips == null)
                return list;

            foreach (var clip in clips)
            {
                if (clip == null || !clip.Enabled)
                    continue;

                var clipSampler = clip.CreateSampler(sequence, this);
                if (clipSampler != null)
                {
                    list.Add(clipSampler);
                }
            }
            return list;
        }

#if UNITY_EDITOR
        public virtual List<IEditorTimeSampler> CreateEditorSamplers(ITrackSequence sequence, object editorTarget)
        {
            var list = new List<IEditorTimeSampler>();
            list.Add(CreateTrackEditorSampler(editorTarget, false));
            if (clips == null)
                return list;

            foreach (var clip in clips)
            {
                if (clip == null || !clip.Enabled)
                    continue;

                var clipSampler = clip.CreateEditorSampler(sequence, this, editorTarget);
                if (clipSampler != null)
                    list.Add(new TrackClipEditorSampler(clip, clipSampler));
            }

            return list;
        }

        protected virtual TrackEditorSampler CreateTrackEditorSampler(object editorTarget, bool ownsEditorTarget)
        {
            return new TrackEditorSampler(this, editorTarget, ownsEditorTarget);
        }
#endif
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
