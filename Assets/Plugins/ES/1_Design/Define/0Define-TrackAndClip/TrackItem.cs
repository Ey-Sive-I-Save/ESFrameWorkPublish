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

    /// <summary>
    /// 资产作用域内的稳定编辑器身份，不是 GameCore/RuntimeKey，不参与存档、联机或热更身份传输。
    /// </summary>
    public interface IStableTrackItem : ITrackItem
    {
        string TrackId { get; set; }
        int TrackSchema { get; set; }
        bool EnsureStableTrackIdentity();
    }

    /// <summary>
    /// Track/Clip Schema 1 是当前基线；未来升级必须在显式编辑器迁移事务中处理旧版本。
    /// </summary>
    public static class ESTrackIdentity
    {
        public const int CurrentTrackSchema = 1;
        public const int CurrentClipSchema = 1;
        public const int MaxStableIdLength = 64;

        public static string NewTrackId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string NewClipId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static bool IsValidStableId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > MaxStableIdLength)
                return false;

            for (int i = 0; i < id.Length; i++)
                if (char.IsWhiteSpace(id[i]))
                    return false;

            return true;
        }

        public static bool ValidateSequenceIdentity(
            ITrackSequence sequence,
            out int trackIssues,
            out int clipIssues,
            out int unsupportedTrackCount,
            out int unsupportedClipCount)
        {
            trackIssues = 0;
            clipIssues = 0;
            unsupportedTrackCount = 0;
            unsupportedClipCount = 0;

            IEnumerable<ITrackItem> tracks = sequence != null ? sequence.Tracks : null;
            if (tracks == null)
                return false;

            HashSet<string> usedTrackIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> usedClipIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (ITrackItem track in tracks)
            {
                if (track == null)
                {
                    trackIssues++;
                    continue;
                }

                if (track is IStableTrackItem stableTrack)
                {
                    string trackId = stableTrack.TrackId ?? string.Empty;
                    if (!IsValidStableId(trackId)
                        || stableTrack.TrackSchema <= 0
                        || stableTrack.TrackSchema > CurrentTrackSchema
                        || !usedTrackIds.Add(trackId))
                    {
                        trackIssues++;
                    }
                }
                else
                {
                    unsupportedTrackCount++;
                }

                IEnumerable<ITrackClip> clips = track.Clips;
                if (clips == null)
                    continue;

                foreach (ITrackClip clip in clips)
                {
                    if (clip == null)
                    {
                        clipIssues++;
                        continue;
                    }

                    if (clip is IStableTrackClip stableClip)
                    {
                        string clipId = stableClip.ClipId ?? string.Empty;
                        if (!IsValidStableId(clipId)
                            || stableClip.ClipSchema <= 0
                            || stableClip.ClipSchema > CurrentClipSchema
                            || !usedClipIds.Add(clipId))
                        {
                            clipIssues++;
                        }
                    }
                    else
                    {
                        unsupportedClipCount++;
                    }
                }
            }

            return trackIssues > 0 || clipIssues > 0;
        }

        public static bool HasFutureSchema(
            ITrackSequence sequence,
            out int futureTrackCount,
            out int futureClipCount)
        {
            futureTrackCount = 0;
            futureClipCount = 0;

            IEnumerable<ITrackItem> tracks = sequence != null ? sequence.Tracks : null;
            if (tracks == null)
                return false;

            foreach (ITrackItem track in tracks)
            {
                if (track == null)
                    continue;

                if (track is IStableTrackItem stableTrack
                    && stableTrack.TrackSchema > CurrentTrackSchema)
                {
                    futureTrackCount++;
                }

                IEnumerable<ITrackClip> clips = track.Clips;
                if (clips == null)
                    continue;

                foreach (ITrackClip clip in clips)
                {
                    if (clip is IStableTrackClip stableClip
                        && stableClip.ClipSchema > CurrentClipSchema)
                    {
                        futureClipCount++;
                    }
                }
            }

            return futureTrackCount > 0 || futureClipCount > 0;
        }

        public static bool RepairSequenceIdentity(
            ITrackSequence sequence,
            out int trackRepairs,
            out int clipRepairs,
            out int unsupportedTrackCount,
            out int unsupportedClipCount)
        {
            trackRepairs = 0;
            clipRepairs = 0;
            unsupportedTrackCount = 0;
            unsupportedClipCount = 0;

            IEnumerable<ITrackItem> tracks = sequence != null ? sequence.Tracks : null;
            if (tracks == null)
                return false;

            HashSet<string> usedTrackIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> usedClipIds = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;

            foreach (ITrackItem track in tracks)
            {
                if (track == null)
                {
                    continue;
                }

                if (track is IStableTrackItem stableTrack)
                {
                    if (stableTrack.TrackSchema > CurrentTrackSchema)
                        continue;

                    bool trackChanged = false;
                    string trackId = stableTrack.TrackId ?? string.Empty;
                    if (!IsValidStableId(trackId) || !usedTrackIds.Add(trackId))
                    {
                        stableTrack.TrackId = CreateUniqueId(usedTrackIds, NewTrackId);
                        trackChanged = true;
                    }

                    if (stableTrack.TrackSchema <= 0)
                    {
                        stableTrack.TrackSchema = CurrentTrackSchema;
                        trackChanged = true;
                    }

                    if (trackChanged)
                    {
                        trackRepairs++;
                        changed = true;
                    }
                }
                else
                {
                    unsupportedTrackCount++;
                }

                IEnumerable<ITrackClip> clips = track.Clips;
                if (clips == null)
                    continue;

                foreach (ITrackClip clip in clips)
                {
                    if (clip == null)
                        continue;

                    if (clip is IStableTrackClip stableClip)
                    {
                        if (stableClip.ClipSchema > CurrentClipSchema)
                            continue;

                        bool clipChanged = false;
                        string clipId = stableClip.ClipId ?? string.Empty;
                        if (!IsValidStableId(clipId) || !usedClipIds.Add(clipId))
                        {
                            stableClip.ClipId = CreateUniqueId(usedClipIds, NewClipId);
                            clipChanged = true;
                        }

                        if (stableClip.ClipSchema <= 0)
                        {
                            stableClip.ClipSchema = CurrentClipSchema;
                            clipChanged = true;
                        }

                        if (clipChanged)
                        {
                            clipRepairs++;
                            changed = true;
                        }
                    }
                    else
                    {
                        unsupportedClipCount++;
                    }
                }
            }

            return changed;
        }

        private static string CreateUniqueId(HashSet<string> usedIds, Func<string> factory)
        {
            string id;
            do
            {
                id = factory();
            }
            while (!usedIds.Add(id));

            return id;
        }
    }

    /// <summary>
    /// Track/Clip Inspector 字段声明标准。
    /// 可见序列化字段必须使用本类分组、中文 LabelText 和明确 PropertyOrder；
    /// 稳定身份、Schema 与仅用于迁移的兼容字段必须保持隐藏，不得混入业务编辑区；
    /// 仍允许用户编辑的兼容入口必须使用标准分组，并在中文标签中明确标注“兼容”。
    /// </summary>
    public static class ESTrackInspectorFieldStandard
    {
        public const string TrackOverview = "轨道概览";
        public const string TrackOverviewBasic = TrackOverview + "/基础信息";
        public const string TrackArrangement = "片段编排";
        public const string ClipOverview = "片段概览";
        public const string ClipOverviewBasic = ClipOverview + "/基础信息";
        public const string Timeline = "时间范围";
        public const string TimelineValues = Timeline + "/时间参数";
        public const string Content = "内容与资源";
        public const string Target = "目标与上下文";
        public const string Behavior = "行为参数";
        public const string Advanced = "高级设置";
        public const string Preview = "编辑器预览";

        public const int OverviewOrder = -1000;
        public const int TimelineOrder = -900;
        public const int ContentOrder = 0;
        public const int TargetOrder = 100;
        public const int BehaviorOrder = 200;
        public const int AdvancedOrder = 700;
        public const int PreviewOrder = 800;
        public const int ArrangementOrder = 900;
    }

    [Serializable]
    public abstract class TrackItemBase<TClip> : ITrackItem, IStableTrackItem where TClip : class, ITrackClip
    {
        [PropertyOrder(ESTrackInspectorFieldStandard.OverviewOrder)]
        [TitleGroup(ESTrackInspectorFieldStandard.TrackOverview, "轨道首先展示启用状态和中文名称，其后依次展示业务内容、目标、行为与预览设置。")]
        [HorizontalGroup(ESTrackInspectorFieldStandard.TrackOverviewBasic, Width = 0.24f)]
        [LabelText("启用轨道")]
        public bool enabled = true;

        [SerializeField, HideInInspector]
        private string trackId = string.Empty;

        [SerializeField, HideInInspector]
        private int trackSchema = ESTrackIdentity.CurrentTrackSchema;

        [PropertyOrder(ESTrackInspectorFieldStandard.ArrangementOrder)]
        [TitleGroup(ESTrackInspectorFieldStandard.TrackArrangement, "片段属于轨道编排内容，默认收起，避免遮挡轨道自身设置。")]
        [LabelText("轨道片段")]
        [ListDrawerSettings(DefaultExpandedState = false, DraggableItems = true, ShowFoldout = true, ShowIndexLabels = true)]
        public List<TClip> clips = new List<TClip>();
        public bool Enabled { get => enabled; set => enabled = value; }
        public IEnumerable<ITrackClip> Clips => clips;

        public string TrackId { get => trackId; set => trackId = value ?? string.Empty; }
        public int TrackSchema { get => trackSchema; set => trackSchema = value; }

        public virtual bool EnsureStableTrackIdentity()
        {
            bool changed = false;
            if (!ESTrackIdentity.IsValidStableId(trackId))
            {
                trackId = ESTrackIdentity.NewTrackId();
                changed = true;
            }

            if (trackSchema <= 0)
            {
                trackSchema = ESTrackIdentity.CurrentTrackSchema;
                changed = true;
            }

            if (clips != null)
            {
                for (int i = 0; i < clips.Count; i++)
                {
                    if (clips[i] is IStableTrackClip stableClip && stableClip.EnsureStableClipIdentity())
                        changed = true;
                }
            }

            return changed;
        }

        public virtual Color ItemBGColor { get => Color.yellow._WithAlpha(0.15f); }

        public string DisplayName
        {
            get { if (displayName == "") { return this.GetType()._GetTypeDisplayName(); } return displayName; }
            set { displayName = value; }
        }
        [PropertyOrder(ESTrackInspectorFieldStandard.OverviewOrder + 1)]
        [TitleGroup(ESTrackInspectorFieldStandard.TrackOverview)]
        [HorizontalGroup(ESTrackInspectorFieldStandard.TrackOverviewBasic)]
        [LabelText("轨道名称")]
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
