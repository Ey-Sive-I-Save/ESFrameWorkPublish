using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public struct SkillRuntimeBuildContext
    {
        public readonly ITrackSequence Sequence;
        public readonly int TrackIndex;
        public readonly int ClipIndex;

        public SkillRuntimeBuildContext(ITrackSequence sequence, int trackIndex, int clipIndex)
        {
            Sequence = sequence;
            TrackIndex = trackIndex;
            ClipIndex = clipIndex;
        }
    }

    public interface ISkillRuntimeTrackPlayer
    {
        void OnSkillEnter(EntityState_Skill state, ref SkillRuntimeTrackState trackState);
        void Tick(EntityState_Skill state, ref SkillRuntimeTrackState trackState, float time, float deltaTime);
        void OnSkillExit(EntityState_Skill state, ref SkillRuntimeTrackState trackState);
    }

    public interface ISkillRuntimeClipPlayer
    {
        void OnClipEnter(EntityState_Skill state, ref SkillRuntimeClipState clipState);
        void Tick(EntityState_Skill state, ref SkillRuntimeClipState clipState, float time, float deltaTime);
        void OnClipExit(EntityState_Skill state, ref SkillRuntimeClipState clipState);
    }

    public interface ISkillRuntimeTrackCompiler
    {
        ISkillRuntimeTrackPlayer CreateRuntimeTrackPlayer(SkillRuntimeBuildContext context);
    }

    public interface ISkillRuntimeClipCompiler
    {
        ISkillRuntimeClipPlayer CreateRuntimeClipPlayer(SkillRuntimeBuildContext context);
    }

    public struct SkillRuntimeTrackState
    {
        public bool IsRunning;
        public int CurrentClipIndex;
        public int NextEnterEventIndex;
        public int NextExitEventIndex;
        public int ActiveClipCount;
        public int[] ActiveClipIndices;
        public object UserData;

        public void EnsureActiveClipCapacity(int clipCount)
        {
            int required = Mathf.Max(clipCount, 1);
            if (ActiveClipIndices == null || ActiveClipIndices.Length < required)
                ActiveClipIndices = new int[required];
        }

        public bool AddActiveClipIndex(int clipIndex)
        {
            if (clipIndex < 0)
                return false;

            EnsureActiveClipCapacity(ActiveClipCount + 1);
            for (int i = 0; i < ActiveClipCount; i++)
            {
                if (ActiveClipIndices[i] == clipIndex)
                    return false;
            }

            ActiveClipIndices[ActiveClipCount++] = clipIndex;
            return true;
        }

        public bool RemoveActiveClipIndex(int clipIndex)
        {
            if (ActiveClipIndices == null || clipIndex < 0)
                return false;

            for (int i = 0; i < ActiveClipCount; i++)
            {
                if (ActiveClipIndices[i] != clipIndex)
                    continue;

                int last = ActiveClipCount - 1;
                ActiveClipIndices[i] = ActiveClipIndices[last];
                ActiveClipIndices[last] = 0;
                ActiveClipCount = last;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            IsRunning = false;
            CurrentClipIndex = -1;
            NextEnterEventIndex = 0;
            NextExitEventIndex = 0;
            ActiveClipCount = 0;
            UserData = null;
        }
    }

    public struct SkillRuntimeClipState
    {
        public bool IsInside;
        public bool HasEntered;
        public object UserData;

        public void Reset()
        {
            IsInside = false;
            HasEntered = false;
            UserData = null;
        }
    }

    public sealed class SkillSequenceRuntimeCache
    {
        private static readonly TrackRuntimeData[] EmptyTracks = new TrackRuntimeData[0];

        private static readonly Dictionary<ITrackSequence, SkillSequenceRuntimeCache> CacheBySequence =
            new Dictionary<ITrackSequence, SkillSequenceRuntimeCache>(32);

#if UNITY_EDITOR
        private static readonly Dictionary<ITrackSequence, int> EditorVersions =
            new Dictionary<ITrackSequence, int>(32);
#endif

        public readonly ITrackSequence Sequence;
        public readonly TrackRuntimeData[] Tracks;
        public readonly float Duration;
        public readonly AnimationTimelineRuntimeData AnimationTimeline;

#if UNITY_EDITOR
        public readonly int EditorVersion;
#endif

        private SkillSequenceRuntimeCache(ITrackSequence sequence)
        {
            Sequence = sequence;
#if UNITY_EDITOR
            EditorVersion = GetEditorVersion(sequence);
#endif
            Tracks = BuildTracks(sequence, out Duration);
            AnimationTimeline = AnimationTimelineRuntimeData.Build(sequence);
        }

        public static SkillSequenceRuntimeCache GetOrBuild(ITrackSequence sequence)
        {
            if (sequence == null)
                return null;

            if (CacheBySequence.TryGetValue(sequence, out var cache) && cache != null)
            {
#if UNITY_EDITOR
                if (cache.EditorVersion == GetEditorVersion(sequence))
                    return cache;
#else
                return cache;
#endif
            }

            cache = new SkillSequenceRuntimeCache(sequence);
            CacheBySequence[sequence] = cache;
            return cache;
        }

#if UNITY_EDITOR
        public static void NotifySequenceChanged(ITrackSequence sequence)
        {
            if (sequence == null)
                return;

            int version = GetEditorVersion(sequence);
            EditorVersions[sequence] = version + 1;
            CacheBySequence.Remove(sequence);
        }
#endif

        private static TrackRuntimeData[] BuildTracks(ITrackSequence sequence, out float duration)
        {
            duration = 0f;
            if (sequence == null || sequence.Tracks == null)
                return EmptyTracks;

            var tracks = new List<TrackRuntimeData>(8);
            int trackIndex = 0;
            foreach (var track in sequence.Tracks)
            {
                if (track == null || !track.Enabled)
                    continue;

                var trackData = new TrackRuntimeData(track, sequence, trackIndex);
                tracks.Add(trackData);

                if (trackData.EndTime > duration)
                    duration = trackData.EndTime;

                trackIndex++;
            }

            return tracks.Count > 0 ? tracks.ToArray() : EmptyTracks;
        }

#if UNITY_EDITOR
        private static int GetEditorVersion(ITrackSequence sequence)
        {
            if (sequence == null)
                return 0;

            return EditorVersions.TryGetValue(sequence, out int version) ? version : 0;
        }
#endif

        public sealed class TrackRuntimeData
        {
            private static readonly ClipRuntimeData[] EmptyClips = new ClipRuntimeData[0];

            public readonly ITrackItem Track;
            public readonly ClipRuntimeData[] Clips;
            public readonly ClipRuntimeEvent[] EnterEvents;
            public readonly ClipRuntimeEvent[] ExitEvents;
            public readonly ISkillRuntimeTrackPlayer Player;
            public readonly float EndTime;

            public TrackRuntimeData(ITrackItem track, ITrackSequence sequence, int trackIndex)
            {
                Track = track;
                Player = track is ISkillRuntimeTrackCompiler compiler
                    ? compiler.CreateRuntimeTrackPlayer(new SkillRuntimeBuildContext(sequence, trackIndex, -1))
                    : null;

                Clips = BuildClips(track, sequence, trackIndex, out EndTime);
                BuildClipEvents(Clips, out EnterEvents, out ExitEvents);
            }

            private static ClipRuntimeData[] BuildClips(ITrackItem track, ITrackSequence sequence, int trackIndex, out float endTime)
            {
                endTime = 0f;
                if (track == null || track.Clips == null)
                    return EmptyClips;

                var clips = new List<ClipRuntimeData>(8);
                int clipIndex = 0;
                foreach (var clip in track.Clips)
                {
                    if (clip == null || !clip.Enabled)
                        continue;

                    var clipData = new ClipRuntimeData(clip, sequence, trackIndex, clipIndex);
                    clips.Add(clipData);

                    if (clipData.EndTime > endTime)
                        endTime = clipData.EndTime;

                    clipIndex++;
                }

                return clips.Count > 0 ? clips.ToArray() : EmptyClips;
            }

            private static void BuildClipEvents(ClipRuntimeData[] clips, out ClipRuntimeEvent[] enterEvents, out ClipRuntimeEvent[] exitEvents)
            {
                if (clips == null || clips.Length == 0)
                {
                    enterEvents = ClipRuntimeEvent.EmptyArray;
                    exitEvents = ClipRuntimeEvent.EmptyArray;
                    return;
                }

                enterEvents = new ClipRuntimeEvent[clips.Length];
                exitEvents = new ClipRuntimeEvent[clips.Length];
                for (int i = 0; i < clips.Length; i++)
                {
                    ClipRuntimeData clip = clips[i];
                    enterEvents[i] = new ClipRuntimeEvent(i, clip.StartTime);
                    exitEvents[i] = new ClipRuntimeEvent(i, clip.EndTime);
                }

                Array.Sort(enterEvents, ClipRuntimeEvent.Comparer);
                Array.Sort(exitEvents, ClipRuntimeEvent.Comparer);
            }
        }

        public sealed class AnimationTimelineRuntimeData
        {
            public static readonly AnimationTimelineRuntimeData Empty = new AnimationTimelineRuntimeData(
                new AnimationClip[0],
                new float[0],
                new float[0],
                new float[0],
                new float[0],
                new bool[0],
                new string[0],
                0f);

            public readonly AnimationClip[] Clips;
            public readonly float[] StartTimes;
            public readonly float[] Durations;
            public readonly float[] ClipStartOffsets;
            public readonly float[] PlaybackSpeeds;
            public readonly bool[] LoopClips;
            public readonly string[] ClipMarkers;
            public readonly float TotalDuration;
            public bool HasClips => Clips != null && Clips.Length > 0;

            private AnimationTimelineRuntimeData(
                AnimationClip[] clips,
                float[] startTimes,
                float[] durations,
                float[] clipStartOffsets,
                float[] playbackSpeeds,
                bool[] loopClips,
                string[] clipMarkers,
                float totalDuration)
            {
                Clips = clips;
                StartTimes = startTimes;
                Durations = durations;
                ClipStartOffsets = clipStartOffsets;
                PlaybackSpeeds = playbackSpeeds;
                LoopClips = loopClips;
                ClipMarkers = clipMarkers;
                TotalDuration = totalDuration;
            }

            public static AnimationTimelineRuntimeData Build(ITrackSequence sequence)
            {
                if (sequence == null || sequence.Tracks == null)
                    return Empty;

                var entries = new List<TimelineClipEntry>(8);
                float maxEndTime = 0f;

                foreach (ITrackItem track in sequence.Tracks)
                {
                    if (track is not SkillTrackItem_Animation animationTrack || !animationTrack.Enabled || animationTrack.clips == null)
                        continue;

                    for (int i = 0; i < animationTrack.clips.Count; i++)
                    {
                        SkillTrackClip_Animation clip = animationTrack.clips[i];
                        if (clip == null || !clip.Enabled || clip.AnimationClipName == null || clip.DurationTime <= 0.0001f)
                            continue;

                        float startTime = Mathf.Max(0f, clip.StartTime);
                        float duration = Mathf.Max(0.0001f, clip.DurationTime);
                        entries.Add(new TimelineClipEntry(
                            clip.AnimationClipName,
                            startTime,
                            duration,
                            Mathf.Max(0f, clip.clipStartOffset),
                            Mathf.Max(0.01f, clip.playbackSpeed),
                            clip.loopClip,
                            clip.clipMarker,
                            entries.Count));
                        maxEndTime = Mathf.Max(maxEndTime, startTime + duration);
                    }
                }

                if (entries.Count == 0)
                    return Empty;

                entries.Sort(TimelineClipEntryComparer.Instance);

                int count = entries.Count;
                AnimationClip[] clipArray = new AnimationClip[count];
                float[] startArray = new float[count];
                float[] durationArray = new float[count];
                float[] offsetArray = new float[count];
                float[] speedArray = new float[count];
                bool[] loopArray = new bool[count];
                string[] markerArray = new string[count];

                for (int i = 0; i < count; i++)
                {
                    TimelineClipEntry entry = entries[i];
                    clipArray[i] = entry.Clip;
                    startArray[i] = entry.StartTime;
                    durationArray[i] = entry.Duration;
                    offsetArray[i] = entry.ClipStartOffset;
                    speedArray[i] = entry.PlaybackSpeed;
                    loopArray[i] = entry.LoopClip;
                    markerArray[i] = !string.IsNullOrWhiteSpace(entry.Marker)
                        ? entry.Marker
                        : BuildClipMarker(entry.Clip, i);
                }

                return new AnimationTimelineRuntimeData(
                    clipArray,
                    startArray,
                    durationArray,
                    offsetArray,
                    speedArray,
                    loopArray,
                    markerArray,
                    maxEndTime);
            }

            private static string BuildClipMarker(AnimationClip clip, int index)
            {
                return clip != null && !string.IsNullOrWhiteSpace(clip.name)
                    ? clip.name
                    : "Clip" + index;
            }

            private readonly struct TimelineClipEntry
            {
                public readonly AnimationClip Clip;
                public readonly float StartTime;
                public readonly float Duration;
                public readonly float ClipStartOffset;
                public readonly float PlaybackSpeed;
                public readonly bool LoopClip;
                public readonly string Marker;
                public readonly int SourceIndex;

                public TimelineClipEntry(AnimationClip clip, float startTime, float duration, float clipStartOffset, float playbackSpeed, bool loopClip, string marker, int sourceIndex)
                {
                    Clip = clip;
                    StartTime = startTime;
                    Duration = duration;
                    ClipStartOffset = clipStartOffset;
                    PlaybackSpeed = playbackSpeed;
                    LoopClip = loopClip;
                    Marker = marker;
                    SourceIndex = sourceIndex;
                }
            }

            private sealed class TimelineClipEntryComparer : IComparer<TimelineClipEntry>
            {
                public static readonly TimelineClipEntryComparer Instance = new TimelineClipEntryComparer();

                public int Compare(TimelineClipEntry x, TimelineClipEntry y)
                {
                    int startCompare = x.StartTime.CompareTo(y.StartTime);
                    return startCompare != 0 ? startCompare : x.SourceIndex.CompareTo(y.SourceIndex);
                }
            }
        }

        public readonly struct ClipRuntimeEvent
        {
            public static readonly ClipRuntimeEvent[] EmptyArray = new ClipRuntimeEvent[0];
            public static readonly IComparer<ClipRuntimeEvent> Comparer = new TimeComparer();

            public readonly int ClipIndex;
            public readonly float Time;

            public ClipRuntimeEvent(int clipIndex, float time)
            {
                ClipIndex = clipIndex;
                Time = time;
            }

            private sealed class TimeComparer : IComparer<ClipRuntimeEvent>
            {
                public int Compare(ClipRuntimeEvent x, ClipRuntimeEvent y)
                {
                    int timeCompare = x.Time.CompareTo(y.Time);
                    return timeCompare != 0 ? timeCompare : x.ClipIndex.CompareTo(y.ClipIndex);
                }
            }
        }

        public sealed class ClipRuntimeData
        {
            public readonly ITrackClip Clip;
            public readonly ISkillRuntimeClipPlayer Player;
            public readonly float StartTime;
            public readonly float EndTime;

            public ClipRuntimeData(ITrackClip clip, ITrackSequence sequence, int trackIndex, int clipIndex)
            {
                Clip = clip;
                StartTime = clip.StartTime;
                EndTime = clip.StartTime + Mathf.Max(0f, clip.DurationTime);
                Player = clip is ISkillRuntimeClipCompiler compiler
                    ? compiler.CreateRuntimeClipPlayer(new SkillRuntimeBuildContext(sequence, trackIndex, clipIndex))
                    : null;
            }
        }
    }
}
